namespace L5R.Core;

// Seeds of Decay pass (issue #453), batch 2: the small general primitives its cards needed, each read live from a record on MatchState and
// expiring by comparison (a turn number, a phase instance, a player's turn count), so nothing has to be cleared.
//
//   * Timed province strength (ProvinceStrengthEffect): a bonus the core rules end with the turn ("Give this battlefield's province a strength bonus
//     equal to your Courtier's Chi", "+3 strength if the target is dishonorable") or a standing rule for the turn ("+2 strength for each dishonorable
//     Personality attacking it"). Counted by EffectiveProvinceStrength beside the permanent path (ProvinceState.Strength, which "permanently give
//     each of your provinces +1 strength" keeps using) and under the same ceilings (Times of Strife, Ancient Feud).
//   * Assignment watches (AssignmentWatch): "after the next time this turn he is assigned to attack [or is destroyed]" -- fired once from Assign()
//     (attackers only) and, for the kinds that name it, from MarkDead.
//   * Sentinel watches: "after the next Dynasty Phase begins" for Sentinel Spirit, read against what the turn did (OpposedTurn, AssignedToAttackTurn).
//   * Action allowances (ActionAllowance): "each player may only take two more non-Reaction actions this phase" (Urgency), ForcedPass's shape.
//   * Proclaim locks and the Proclaim negation window: "may not take the Proclaim Reaction until your next turn begins" (Yogo Ugimori),
//     "Before a Proclaim action resolves: Negate its effects" (Turning the Story), and "even if you have already used the Proclaim ability once this
//     turn" (Kakita Yusugi) -- Proclaim being a command of its own, not a catalogued ability.
//   * Extra End Phase draws (Ingenuity), an Honor-loss record on the resolved action (An Insult Answered), a performer's own "+3C until it ends"
//     (Doji Yuka), a Personal Honor static (Akodo Hotaka) and the Honor Victory threshold he lowers.

/// <summary>A province strength effect for one turn. Kind "flat": Amount on the province at (PlayerId, ProvinceIndex). Kind "per-dishonorable-attacker":
/// Amount for each dishonorable enemy Personality attacking each of PlayerId's provinces (Imperial Protection).</summary>
public sealed record ProvinceStrengthEffect(int PlayerId, int? ProvinceIndex, string Kind, int Amount, int Turn, string Source, int? UntilBattle = null);

/// <summary>"After the next time this turn he is assigned to attack [or is destroyed]": Kind names the card's own consequence (SeedsOfDecayBatch2.cs).</summary>
public sealed record AssignmentWatch(string CardId, string Kind, int BeneficiaryPlayerId, int Turn, string Source);

/// <summary>Sentinel Spirit: the Spirit chosen on another player's turn, judged when the next Dynasty Phase begins.</summary>
public sealed record SentinelWatch(int PlayerId, string SpiritId, int Turn);

/// <summary>Urgency: how many more non-Reaction actions PlayerId may take in the phase numbered PhaseInstance.</summary>
public sealed class ActionAllowance
{
    public int PlayerId { get; set; }
    public int PhaseInstance { get; set; }
    public int Remaining { get; set; }
}

/// <summary>Yogo Ugimori: PlayerId may not Proclaim until OwnerId's turn count reaches UntilTurnsTaken (his next turn beginning).</summary>
public sealed record ProclaimLock(int PlayerId, int OwnerId, int UntilTurnsTaken);

/// <summary>A Proclaim waiting for the "before a Proclaim action resolves" window (Turning the Story): the command and the Proclaim window it interrupted.</summary>
public sealed class HeldProclaim
{
    public GameCommand Command { get; set; } = null!;
    public PendingChoice Window { get; set; } = null!;
    public int ConsecutivePasses { get; set; }
    public bool Negated { get; set; }
}

public sealed partial class MatchEngine
{
    public const string ProvinceEffectFlat = "flat";
    public const string ProvinceEffectPerDishonorableAttacker = "per-dishonorable-attacker";
    public const string WatchImperialProtection = "imperial-protection";
    public const string WatchFairWarning = "fair-warning";

    // Test seam (L5R.Core.Tests has InternalsVisibleTo): a Personality's current Personal Honor.
    internal int PersonalHonorForTest(string cardId) => CardPersonalHonor(Card(cardId));

    // ---- Timed province strength ----

    private void GiveProvinceStrengthThisTurn(int playerId, int provinceIndex, int amount, string source)
    {
        if (!Player(playerId).Provinces.Any(p => p.Index == provinceIndex)) return; // a battlefield without a province has no strength to change (issue #492, rulings pass R1 with R3)
        if (ProvinceStrengthChangeNegated(playerId, provinceIndex, source)) return; // P12 (ImmunityLedger.cs)
        _state.ProvinceStrengthEffects.Add(new(playerId, provinceIndex, ProvinceEffectFlat, amount, _state.TurnNumber, source));
        Emit("provinceStrengthRaised", $"{source}: {Player(playerId).Name}'s province {provinceIndex + 1} has {amount:+#;-#;0} strength this turn.", playerId);
    }

    // The part of a province's current strength that ends with the turn.
    private int TimedProvinceStrength(ProvinceState province) => TimedProvinceStrengthParts(province).Sum(part => part.Amount);

    // Each timed effect on the province with its source's name, for the strength sum above and for the modifier the view shows beside the province.
    private IEnumerable<(string Source, int Amount)> TimedProvinceStrengthParts(ProvinceState province)
    {
        var ownerId = _state.Players.First(p => p.Provinces.Contains(province)).Id;
        // UntilBattle (Kaiu Hideaki, Emperor Edition Personality pass, issue #477 batch 2): a strength that lasts only until that battle's resolution begins (ExpireUntilResolutionProvinceStrength).
        foreach (var effect in _state.ProvinceStrengthEffects.Where(e => e.Turn == _state.TurnNumber && e.PlayerId == ownerId && (e.UntilBattle is null || e.UntilBattle == _state.BattleNumber)))
        {
            if (effect.Kind == ProvinceEffectFlat) { if (effect.ProvinceIndex == province.Index) yield return (effect.Source, effect.Amount); }
            else if (effect.Kind == ProvinceEffectPerDishonorableAttacker)
                yield return (effect.Source, effect.Amount * _state.Cards.Values.Count(c => c.OwnerId != ownerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == province.Index
                    && Definition(c).Type == CardType.Personality && IsAttacking(c) && MatchesTargetStatus(c, TargetStatus.IsDishonorable)));
        }
    }

    // ---- Assignment watches ----

    private void FireAssignedToAttackWatches(CardInstance card)
    {
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == card.Id && w.Turn == _state.TurnNumber && w.Kind != WatchVeiledMenace).ToArray()) // Veiled Menace (issue #492) fires on either side's assignment (FireVeiledMenaceAssigned).
        {
            // Social Grace (The Shadow's Embrace pass, issue #476, batch 4): "after EACH time he is assigned to attack this turn" -- it stays for the turn.
            if (watch.Kind != WatchSocialGrace) _state.AssignmentWatches.Remove(watch);
            ResolveAssignmentWatch(watch, card, "was assigned to attack");
        }
    }

    private void FireDestroyedWatches(CardInstance card)
    {
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == card.Id && w.Turn == _state.TurnNumber && w.Kind == WatchFairWarning).ToArray())
        {
            _state.AssignmentWatches.Remove(watch);
            ResolveAssignmentWatch(watch, card, "was destroyed");
        }
    }

    private void ResolveAssignmentWatch(AssignmentWatch watch, CardInstance card, string what)
    {
        switch (watch.Kind)
        {
            // Imperial Protection: "dishonor him and his controller loses Honor equal to his base Personal Honor".
            case WatchImperialProtection:
                Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what}.", watch.BeneficiaryPlayerId, card.Id);
                Dishonor(card, watch.BeneficiaryPlayerId);
                GainHonor(card.OwnerId, -Definition(card).PersonalHonor, watch.Source);
                break;
            // Midday Shadow Court (Stronghold pass, issue #486 batch 1): "dishonor him and his controller loses 1 Honor".
            case WatchMiddayShadow:
                Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what}.", watch.BeneficiaryPlayerId, card.Id);
                Dishonor(card, watch.BeneficiaryPlayerId);
                GainHonor(card.OwnerId, -1, watch.Source);
                break;
            // Fair Warning: "gain 2 Honor". Social Grace (The Shadow's Embrace pass, issue #476, batch 4): the same, each time.
            case WatchSocialGrace:
            case WatchFairWarning:
                Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what}.", watch.BeneficiaryPlayerId, card.Id);
                GainHonor(watch.BeneficiaryPlayerId, 2, watch.Source);
                break;
            // Words of Consecration (issue #622, IvorySpells.cs): "gain 1 Honor, or 2 Honor if you control a Temple".
            case IvorySpellAbilities.WordsOfConsecrationWatchKind:
                ResolveWordsOfConsecrationWatch(watch, card);
                break;
            // Bayushi Kachiko (issue #778, Kachiko.cs): "his controller loses 2 Honor".
            case KachikoAbilities.WatchKind:
                ResolveKachikoWatch(watch, card, what);
                break;
            // Akumasa (issue #737, Wave737.cs): "After the target attacks this turn, draw a card."
            case Wave737Abilities.AkumasaWatchKind:
                Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what}.", watch.BeneficiaryPlayerId, card.Id);
                Draw(Player(watch.BeneficiaryPlayerId));
                break;
            // Veiled Menace (Emperor Edition remaining-cards pass, issue #492, group D): "After each time he is assigned (this turn), his controller loses 1 Honor."
            case WatchVeiledMenace:
                Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what}.", watch.BeneficiaryPlayerId, card.Id);
                GainHonor(card.OwnerId, -1, watch.Source);
                break;
        }
    }

    // ---- Sentinel Spirit ----

    // Both armies at a resolving battle were opposed by the other: each card there remembers the turn.
    private void MarkOpposed(CardInstance[] attackers, CardInstance[] defenders)
    {
        if (attackers.Length == 0 || defenders.Length == 0) return;
        foreach (var card in attackers.Concat(defenders)) card.OpposedTurn = _state.TurnNumber;
    }

    private void ResolveSentinelWatches()
    {
        var due = _state.SentinelWatches.Where(w => w.Turn == _state.TurnNumber).ToArray();
        _state.SentinelWatches.RemoveAll(w => w.Turn <= _state.TurnNumber);
        foreach (var watch in due)
        {
            if (!_state.Cards.TryGetValue(watch.SpiritId, out var spirit)) continue;
            if (spirit.OpposedTurn == _state.TurnNumber) GainHonor(watch.PlayerId, 2, "Sentinel Spirit", spirit);
            var anyAssignedToAttack = _state.Cards.Values.Any(c => c.OwnerId == _state.ActivePlayerId && c.AssignedToAttackTurn == _state.TurnNumber);
            var lowest = _state.Players.Where(p => p.Id != watch.PlayerId).All(p => Player(watch.PlayerId).Honor < p.Honor);
            if (!anyAssignedToAttack && lowest) GainHonor(watch.PlayerId, 3, "Sentinel Spirit", spirit);
        }
    }

    // ---- Action allowances (Urgency) ----

    private static readonly string ActionAllowanceReason = new BarNotice("Taking more non-Reaction actions this phase", "Urgency").Message;

    private void LimitActionsThisPhase(int playerId, int count, string source)
    {
        var existing = _state.ActionAllowances.FirstOrDefault(a => a.PlayerId == playerId && a.PhaseInstance == PhaseInstance());
        if (existing is null) _state.ActionAllowances.Add(new ActionAllowance { PlayerId = playerId, PhaseInstance = PhaseInstance(), Remaining = count });
        else existing.Remaining = Math.Min(existing.Remaining, count);
        Emit("actionsLimited", $"{source}: {Player(playerId).Name} may take {count} more non-Reaction actions this phase.", playerId);
    }

    private ActionAllowance? CurrentActionAllowance(int playerId) => _state.ActionAllowances.FirstOrDefault(a => a.PlayerId == playerId && a.PhaseInstance == PhaseInstance());

    // A non-Reaction action: an ability command whose ability is not a Reaction, a played Strategy, a Reserve recruit, an equip.
    private static bool IsNonReactionActionCommand(GameCommand command) =>
        command.Type is "playStrategy" or "recruitReserve" or "equip"
        || command.Type == "useAbility" && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && ability.Timing != "Reaction";

    // Apply()'s check: past the allowance the command is refused; within it, it is counted.
    private void EnforceActionAllowance(GameCommand command)
    {
        if (!IsNonReactionActionCommand(command) || CurrentActionAllowance(command.PlayerId) is not { } allowance) return;
        if (allowance.Remaining <= 0) throw new RuleViolation(ActionAllowanceReason);
        allowance.Remaining--;
    }

    private ActionOption[] ApplyActionAllowanceToOptions(int playerId, ActionOption[] options)
    {
        if (CurrentActionAllowance(playerId) is not { Remaining: <= 0 }) return options;
        return options.Select(o => o.Command is not null && IsNonReactionActionCommand(o.Command) ? o with { Enabled = false, Reason = ActionAllowanceReason } : o).ToArray();
    }

    // ---- Proclaim: the turn limit, locks and the negation window ----

    private bool ProclaimLocked(int playerId) => _state.ProclaimLocks.Any(l => l.PlayerId == playerId && Player(l.OwnerId).TurnsTaken < l.UntilTurnsTaken);

    // Kakita Yusugi: "even if you have already used the Proclaim ability once this turn".
    private bool ProclaimAllowed(PlayerState player, CardDefinition entering, CardInstance? card = null) =>
        (player.ProclaimUsedTurn != _state.TurnNumber || entering.Effect == "proclaim-ignores-turn-limit") && !ProclaimLocked(player.Id) && (card is null || ProclaimGateFor(card, player).RefuseReason is null); // P3 (issue #695, RecruitGates695.cs)

    private string? ProclaimRejection(PlayerState player, CardDefinition entering, string? enteringId = null) =>
        ReactionsBarred() ? "Cherry Blossom Festival: no players may take Reactions." // issue #492, group F
        : ProclaimLocked(player.Id) ? "You may not take the Proclaim Reaction until the locking player's next turn begins."
        : enteringId is not null && player.ProclaimedCardId == enteringId ? "This Personality has already been Proclaimed."
        : player.ProclaimUsedTurn == _state.TurnNumber && entering.Effect != "proclaim-ignores-turn-limit" ? "Already used Proclaim this turn." : null;

    private const string BeforeProclaimTrigger = "before-proclaim-resolves";

    // Turning the Story: the opponent holds a "before a Proclaim action resolves" Reaction in hand -- the Proclaim waits for their answer.
    private bool DeferProclaimForNegation(GameCommand command)
    {
        var reactingPlayerId = 1 - command.PlayerId;
        if (!AnyOutOfPlayReactionAvailable(reactingPlayerId, BeforeProclaimTrigger)) return false;
        _state.HeldProclaim = new HeldProclaim { Command = command, Window = _state.PendingChoice!, ConsecutivePasses = _state.ConsecutivePasses };
        _state.PendingChoice = new("proclaimNegateReaction", reactingPlayerId, null, "Before this Proclaim resolves: you may play a Reaction, or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = reactingPlayerId;
        Emit("reactionWindow", "Before-Proclaim reaction window opened.", reactingPlayerId);
        return true;
    }

    private void NegateHeldProclaim(CardInstance source)
    {
        var held = _state.HeldProclaim ?? throw new RuleViolation("No Proclaim is waiting.");
        held.Negated = true;
        Emit("proclaimNegated", $"{Definition(source).Name}: the Proclaim's effects are negated.", source.OwnerId, source.Id);
    }

    // The window's close: the Proclaim is taken (its once-per-turn use spent) and its Honor gained unless negated; the Proclaim window comes back as it was.
    private void ResolveHeldProclaim()
    {
        var held = _state.HeldProclaim!; _state.HeldProclaim = null;
        _state.PendingChoice = held.Window; _state.ConsecutivePasses = held.ConsecutivePasses;
        FinishProclaim(held.Command, held.Negated);
    }

    private void FinishProclaim(GameCommand command, bool negated)
    {
        var player = Player(command.PlayerId);
        var card = Card(_state.PendingChoice!.CardId);
        player.ProclaimUsedTurn = _state.TurnNumber; player.ProclaimedCardId = card.Id;
        RecordFact(FactKinds.Proclaimed, player.Id, card.Id, null, null, negated ? "negated" : null); // P6 (AfterFacts.cs): "after you Proclaim"
        // Proclaim is a Reaction (Mendon's R4 ruling), so its gain is from an action's effects: Rhetoric and the like may be played on it.
        if (!negated) { var before = _proclaimAction; _proclaimAction = ($"{player.Id}|proclaim|{card.Id}", player.Id); try { GainHonor(player.Id, Definition(card).PersonalHonor + HoteiProclaimBonus(player.Id, card) + Wave829PersonalHonor(card), "Proclaim"); /* Hotei's Contentment (issue #503) */ } finally { _proclaimAction = before; } }
        else Emit("proclaimNegated", $"{player.Name}'s Proclaim resolved with its effects negated.", player.Id, card.Id);
        ApplyPendingEntryEffects();
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = 1 - command.PlayerId;
        SettleReactionWindow(); // issue #639: no prompt for a reaction nobody can take
    }

    private IEnumerable<ActionOption> BeforeProclaimReactionOptions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "proclaimNegateReaction" } pending || pending.PlayerId != playerId) yield break;
        foreach (var (card, abilityId) in OutOfPlayReactions(playerId, BeforeProclaimTrigger))
            yield return new("useAbility", AbilityCatalog.All[abilityId].Label, true, null, new GameCommand("useAbility", playerId, _state.Version, CardId: card.Id, AbilityId: abilityId), card.Id);
    }

    // ---- Ingenuity: "draw an additional card during your next End Phase" ----

    private void DrawExtraEndPhaseCards(PlayerState player)
    {
        var extra = _state.ExtraEndPhaseDraws[player.Id]; _state.ExtraEndPhaseDraws[player.Id] = 0;
        for (var i = 0; i < extra; i++) Draw(player);
    }

    // ---- An Insult Answered: the Honor an action cost a player ----

    private void RecordHonorLoss(int playerId)
    {
        if (_runningCommand is null && _state.PendingAttack is null && _state.PendingDuel is null) return;
        if (!_state.CurrentActionHonorLosers.Contains(playerId)) _state.CurrentActionHonorLosers.Add(playerId);
    }

    private bool LastActionCostHonor(int playerId) => _state.LastResolvedAction is { } facts && facts.HonorLosers.Contains(playerId);

    private CardInstance? LastActionPerformer() =>
        _state.LastResolvedAction is { PerformerId: string performerId } && _state.Cards.TryGetValue(performerId, out var performer) && InPlay(performer)
            && Definition(performer).Type == CardType.Personality ? performer : null;

    // ---- Doji Yuka: "After Yuka performs a Political action: Give her +3C until it ends" ----

    private const string YukaPoliticalChi = "yuka-political-chi";

    private void BeginPerformerActionTraits(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (!IsAction(ability)) return;
        var performer = ability.Performer is not null ? (command.PerformerId is null ? null : ActingPersonality(Card(command.PerformerId))) : source is null ? null : ActingPersonality(source);
        if (performer is null || Definition(performer).Effect != YukaPoliticalChi || !ActionCarriesKeyword(ability, source, "Political")) return;
        ApplyModifier(performer, performer.Id, YukaPoliticalChi, "Chi", 3);
    }

    private void EndPerformerActionTraits()
    {
        foreach (var card in _state.Cards.Values) card.Modifiers.RemoveAll(m => m.Effect == YukaPoliticalChi);
    }

    // ---- Akodo Hotaka ----

    // "+1PH if any other player is Dragon Clan".
    private int StaticPersonalHonorBonus(CardInstance card) =>
        (Definition(card).Effect == "hotaka-paragon" && _state.Players.Any(p => p.Id != card.OwnerId && p.Clan == "Dragon") ? 1 : 0) + P3PersonalHonorBonus(card) + IvoryPoliticalPersonalHonorBonus(card) + PromoItemsPersonalHonorBonus(card) + Wave829PersonalHonor(card); // + Utaku Lishan (issue #503); Oriole Imperial Vanguard (issue #622); Kimono of the Turquoise Champion, The Emerald Armor (PromoEmperorItems.cs)

    // "The amount of Honor you need in order to win an Honor Victory is reduced by Hotaka's Personal Honor minus one" -- each copy in play.
    private int HonorVictoryThreshold(int playerId) =>
        40 - _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == "hotaka-paragon").Sum(c => CardPersonalHonor(c) - 1);

    // "Hotaka may not perform Dark Virtue actions": the Personality performing the action (named, or the source resolved to its wielder) may carry a ban.
    private void RequirePerformerActionKeywordAllowed(GameCommand command, AbilityDefinition ability, CardInstance? source)
    {
        if (!IsAction(ability)) return;
        var performer = ability.Performer is not null ? (command.PerformerId is null ? null : ActingPersonality(Card(command.PerformerId))) : source is null ? null : ActingPersonality(source);
        if (performer is null || Definition(performer).Effect != "hotaka-paragon") return;
        Require(!ActionCarriesKeyword(ability, source, "Dark Virtue"), $"{Definition(performer).Name} may not perform Dark Virtue actions.");
    }

    // ---- Manipulate Energies: give or remove Cavalry, Naval or Tactician permanently ----

    private void GiveKeywordPermanently(CardInstance card, string keyword, string source)
    {
        card.RemovedKeywords.RemoveAll(k => k == keyword);
        GrantKeyword(card, keyword, source, GrantDuration.Permanent);
        Emit("keywordGranted", $"{Definition(card).Name} permanently has {keyword}.", card.OwnerId, card.Id);
    }

    private void RemoveKeywordPermanently(CardInstance card, string keyword)
    {
        card.GrantedKeywords.RemoveAll(g => g.Keyword == keyword);
        if (!card.RemovedKeywords.Contains(keyword)) card.RemovedKeywords.Add(keyword);
        Emit("keywordRemoved", $"{Definition(card).Name} permanently loses {keyword}.", card.OwnerId, card.Id);
    }

    // ---- Sharing Gossip ----

    private int SharingGossipCount(CardInstance source) =>
        _state.Cards.Values.Count(c => c.Zone == Zone.FateDiscard && Definition(c).Name == "Sharing Gossip") + (PlayedFromHand(source) ? 1 : 0);

    // ---- Brotherhood Schism: "Rings will not enter play if their owner has already put a Ring into play this turn" ----

    private static readonly string[] RingRecruitIds = ["ring-of-air-recruit", "ring-of-water-recruit", "ring-of-earth-recruit", "ring-of-fire-recruit", "ring-of-void-recruit",
        "false-ring-of-air-recruit", "false-ring-of-earth-recruit", "false-ring-of-water-recruit"];

    private void RequireRingMayEnterPlay(string id, PlayerState player)
    {
        if (!RingRecruitIds.Contains(id)) return;
        Require(_state.RingSchismTurn != _state.TurnNumber || player.RingPutIntoPlayTurn != _state.TurnNumber, "Brotherhood Schism: you already put a Ring into play this turn.");
    }

    // ---- The batch's execution cases (RunAbilityEffects delegates here) ----

    private void RunSeedsBatch2Effect(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        var performer = command.PerformerId is null ? null : Card(command.PerformerId);
        switch (id)
        {
            case "courting-trouble-overconfident": GiveKeywordPermanently(target!, "Overconfident", source!.Id); break;
            case "out-of-my-sight-brash": GiveKeywordPermanently(target!, "Brash", source!.Id); break;
            case "debt-to-the-kaiu-strength":
                foreach (var province in player.Provinces.Where(p => !p.Destroyed)) province.Strength += 1;
                Emit("provinceStrengthRaised", $"{player.Name}'s provinces permanently have +1 strength (Debt to the Kaiu).", player.Id);
                break;
            case "imperial-protection-watch":
                _state.AssignmentWatches.Add(new(target!.Id, WatchImperialProtection, player.Id, _state.TurnNumber, "Imperial Protection"));
                Emit("watchSet", $"Imperial Protection is watching {Definition(target).Name}.", player.Id, target.Id);
                break;
            case "imperial-protection-provinces":
                _state.ProvinceStrengthEffects.Add(new(player.Id, null, ProvinceEffectPerDishonorableAttacker, 2, _state.TurnNumber, "Imperial Protection"));
                Emit("provinceStrengthRaised", $"{player.Name}'s provinces have +2 strength for each dishonorable Personality attacking them this turn.", player.Id);
                break;
            case "incriminating-testimony-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (CurrentProvinceIndex() is int testimonyProvince) GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, testimonyProvince, PersonalityChi(performer!), "Incriminating Testimony");
                break;
            case "ingenuity-dishonor":
                foreach (var dishonored in SlotCards(command, "targets").ToArray()) Dishonor(dishonored);
                _state.ExtraEndPhaseDraws[player.Id]++;
                Emit("extraDrawQueued", $"{player.Name} draws an additional card during the next End Phase (Ingenuity).", player.Id);
                break;
            case "testimony-dishonor": Dishonor(target!); break;
            case "testimony-rehonor": Rehonor(target!); break;
            case "sharing-gossip-honor": GainHonor(player.Id, SharingGossipCount(source!), "Sharing Gossip", source); break;
            case "urgency-limit":
                LimitActionsThisPhase(0, 2, "Urgency"); LimitActionsThisPhase(1, 2, "Urgency");
                break;
            case "urgency-draw": Draw(player); break;
            case "turning-the-story-negate": NegateHeldProclaim(source!); break;
            case "fair-warning-watch":
                _state.AssignmentWatches.Add(new(target!.Id, WatchFairWarning, player.Id, _state.TurnNumber, "Fair Warning"));
                Emit("watchSet", $"Fair Warning is watching {Definition(target).Name}.", player.Id, target.Id);
                break;
            case "sentinel-spirit-watch":
                _state.SentinelWatches.Add(new(player.Id, performer!.Id, _state.TurnNumber));
                Emit("watchSet", $"Sentinel Spirit: {Definition(performer).Name} stands watch until the next Dynasty Phase begins.", player.Id, performer.Id);
                break;
            case "manipulate-energies-give-cavalry": GiveKeywordPermanently(target!, "Cavalry", source!.Id); break;
            case "manipulate-energies-give-naval": GiveKeywordPermanently(target!, "Naval", source!.Id); break;
            case "manipulate-energies-give-tactician": GiveKeywordPermanently(target!, "Tactician", source!.Id); break;
            case "manipulate-energies-remove-cavalry": RemoveKeywordPermanently(target!, "Cavalry"); break;
            case "manipulate-energies-remove-naval": RemoveKeywordPermanently(target!, "Naval"); break;
            case "manipulate-energies-remove-tactician": RemoveKeywordPermanently(target!, "Tactician"); break;
            case "dragon-tattoo-exp-imbue":
                performer!.PerformedActionFromTitles.Add(Definition(source!).Name);
                GrantAbility(performer, "dragon-tattoo-fire-token", source!.Id, GrantDuration.Permanent);
                Emit("abilityGranted", $"{Definition(performer).Name} is imbued with the mystical power of the Dragon.", player.Id, performer.Id);
                break;
            case "dragon-tattoo-fire-token": CreateToken(source!, "Fire", id, "Force", 1); break;
            case "brotherhood-schism-rings":
                _state.RingSchismTurn = _state.TurnNumber;
                Emit("ringsBarred", "Brotherhood Schism: until the turn ends, Rings will not enter play for a player who already put one into play this turn.", player.Id);
                break;
            case "brotherhood-schism-shuffle":
                {
                    var owner = Player(target!.OwnerId);
                    target.Zone = Zone.DynastyDeck; target.FaceUp = false; target.ProvinceIndex = null;
                    owner.DynastyDeck.Add(target.Id);
                    ShuffleDeck(owner, "Dynasty");
                    Emit("eventShuffled", $"{Definition(target).Name} was shuffled into {owner.Name}'s Dynasty deck (Brotherhood Schism).", player.Id, target.Id);
                    break;
                }
            case "baiko-shame": GainHonor(target!.OwnerId, -1, "Yasuki Baiko", source); break;
            case "baiko-favor": TakeImperialFavor(player.Id); Emit("favorTaken", $"{player.Name} took the Imperial Favor (Yasuki Baiko).", player.Id, source?.Id); break;
            case "tsujiken-exp-bow": if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true; break;
            case "tsujiken-exp-straighten": Straighten(target!); break;
            case "tsujiken-exp-draw": Draw(player); break;
            case "tsujiken-exp-dishonor": Dishonor(target!); GainHonor(target!.OwnerId, -1, "Yasuki Tsujiken", source); break;
            case "ugimori-proclaim-lock":
                {
                    var locked = command.TargetPlayerId!.Value;
                    _state.ProclaimLocks.Add(new(locked, player.Id, player.TurnsTaken + 1));
                    Emit("proclaimLocked", $"{Player(locked).Name} may not take the Proclaim Reaction until {player.Name}'s next turn begins (Yogo Ugimori).", player.Id, source?.Id);
                    break;
                }
            case "shibata-exp-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (EffectConditionMet(id, target) && CurrentProvinceIndex() is int shibataProvince) GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, shibataProvince, 3, "Bayushi Shibata");
                break;
            case "insult-answered-duel": break; // The duel itself is issued generically (IssuesDuel); its outcome is in Dueling.cs's ApplyDuelOutcome.
            default: throw new RuleViolation("Ability effect is not implemented.");
        }
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredProclaimNegateReaction(string type, GameCommand command)
    {
        if (type != "proclaimNegateReaction") return false;
        // Turning the Story (issue #453, batch 2): the one reacting player answers -- the Reaction (negating the held Proclaim) or a pass -- and the
        // Proclaim then resolves, negated or not, back inside the Proclaim window it interrupted (SeedsOfDecayBatch2.cs).
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the before-Proclaim reaction window first.");
        if (command.Type == "useAbility") UseAbility(command);
        else Emit("reactionPassed", $"{Player(command.PlayerId).Name} passed the reaction opportunity.", command.PlayerId);
        // The window closes the instant the one reacting player has answered: a pass, or the Reaction that negated the Proclaim.
        if (_state.HeldProclaim is { } held && (command.Type != "useAbility" || held.Negated)) { _state.PendingChoice = null; _state.ConsecutivePasses = 0; ResolveHeldProclaim(); }
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSeedsOfDecayBatch2Hooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredProclaimNegateReaction", 4200, static (engine, type, command) => engine.HandleRegisteredProclaimNegateReaction(type, command));
    }
}
