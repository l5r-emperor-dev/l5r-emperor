using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #829: eleven cards Mendon's rulings of 2026-10-11 (second batch) unblocked (docs/decisions/2026-10-11-farm-wedding-koku-batch.md; rulings R-054..R-062 in docs/rules/RULINGS.md). Ability ids are "w829-".
//
//   Well-Tended Farm         ":bow:: Produce 1 Gold. / Limited, :bow:: Gain 1 Honor."                                   a Holding's printed production and one Limited bow ability
//   One Koku                 "Reaction: When paying a Gold cost: Produce one Gold."                                    a payment token for a Strategy IN HAND, discarded when chosen (Near Miss's shape)
//   Shrine to the Heavens    ":bow:: Produce 2 Gold. / After one of your Celestials is revealed in a Province, you may choose one of your Celestials in play. If you do, it is not discarded..."
//                            a trait: each Shrine in play adds one P6 Celestial entry decision (CelestialEntryRegistry), checked only when a Celestial is revealed
//   Cross-Clan Wedding       a province-reveal Event (P5): the owner gives a Personality; if the other player now controls it, THAT player gives one of his other Personalities back
//   The Onyx Championship    a province-reveal Event (P5): a Shugenja permanently gains Shadowlands and the Maho Limited that destroys another Personality to recruit an Oni from the discard pile (dead Oni too)
//   Minor Blackmail          a province-reveal Event (P5): a standing watch (not timed) for the chosen Personality performing an action or being assigned to a battlefield
//   Caught Unawares          "Open: If you control no Personalities, target one or two Personalities: Bow them. Negate their straightening (this turn). If it is not your turn, draw a card. /
//                            Battle: Target an enemy card with no unbowed attachments: Bow it."
//   The Duty of the Crab     a Focus Effect that may delay the loss of a duel, and a Battle recruit at the current battlefield from a province or the discard pile
//   Stone of Remembrance     a Holding that counts as zero cards toward the minimum deck size, and a Reaction that removes it to negate a destruction
//   Benten's Blessing        a Celestial: up to two face-up Personalities in or out of play gain +1PH while it remains in play (a multi-pick decision), and a redirecting Reaction
//   Jizo's Mercy             a Celestial: a Battle move home, and a Battle that replaces the destruction of the Province in this battle's resolution with 4 Honor
public static class Wave829Abilities
{
    public const string FarmHonor = "w829-farm-honor", StoneNegate = "w829-stone-negate", CaughtOpen = "w829-caught-open", CaughtBattle = "w829-caught-battle", KokuGold = "w829-koku-gold",
        DutyFocus = "w829-duty-focus", DutyBattle = "w829-duty-battle", BentenRedirect = "w829-benten-redirect", JizoHome = "w829-jizo-home", JizoMercy = "w829-jizo-mercy", OnyxMaho = "w829-onyx-maho";
    public const string Farm = "oracle-l5r-9292", Stone = "oracle-l5r-7501", Shrine = "oracle-l5r-7229", Wedding = "oracle-l5r-1576", Blackmail = "oracle-l5r-5061", Onyx = "oracle-l5r-8242",
        Caught = "oracle-l5r-1239", Koku = "oracle-l5r-5709", Duty = "oracle-l5r-8036", Benten = "oracle-l5r-959", Jizo = "oracle-l5r-10563";
    public const string StoneEffect = "w829-stone", ShrineEffect = "w829-shrine", WeddingEffect = "w829-wedding", BlackmailEffect = "w829-blackmail", OnyxEffect = "w829-onyx", DutyEffect = "w829-duty",
        BentenEffect = "w829-benten", JizoEffect = "w829-jizo";
    public const string KokuKind = "koku";
    public const string BentenPerformer = "w829-benten-performer", OtherOwnPersonality = "w829-other-own-personality";
    public const string NoUnbowedAttachments = "w829-enemy-card-no-unbowed-attachments", CrabRecruit = "w829-crab-province-or-discard", CaughtSlot = "targets", OnyxTarget = "target", OnyxSacrifice = "sacrifice";
    public const string WeddingType = "w829WeddingGive", WeddingCommand = "w829WeddingGiveChoose", BentenType = "w829BentenChoice";
    /// <summary>Benten's Blessing answers with the multi-select actions Sleep uses (MatchState.PendingBudgetSelect), so the client's multi-select panel renders it.</summary>
    public const string BentenAdd = "budgetSelectAdd", BentenClear = "budgetSelectClear", BentenConfirm = "budgetSelectConfirm";

    public static Dictionary<string, AbilityDefinition> AddWave829(this Dictionary<string, AbilityDefinition> all)
    {
        // Well-Tended Farm (oracle-l5r-9292): "Limited, :bow:: Gain 1 Honor." (its ":bow:: Produce 1 Gold." is the Holding's printed production)
        all[FarmHonor] = new("Limited", "Limited, bow this card: Gain 1 Honor", "none", Costs: ActionCost.BowSource);
        // Stone of Remembrance (oracle-l5r-7501): "Reaction: After another player's action targets one of your cards, remove this card from the game and then tear it in half to negate the target's destruction from the action's effects."
        all[StoneNegate] = new("Reaction", "After another player's action targets one of your cards, remove this card from the game and then tear it in half to negate the target's destruction from the action's effects", "none",
            Trigger: ReactionTriggers.TargetedByAction, Costs: ActionCost.RemoveSource);
        // Caught Unawares (oracle-l5r-1239)
        all[CaughtOpen] = new("Open", "Open: If you control no Personalities, target one or two Personalities: Bow them. Negate their straightening (this turn). If it is not your turn, draw a card", "slots",
            Slots: [new(CaughtSlot, "personality", "One or two Personalities to bow", 1, 2)]);
        all[CaughtBattle] = new("Battle", "Battle: Target an enemy card with no unbowed attachments: Bow it", NoUnbowedAttachments);
        // One Koku (oracle-l5r-5709): "Reaction: When paying a Gold cost: Produce one Gold." Chosen inside a payment from the hand, and discarded as the payment is made.
        all[KokuGold] = new("Reaction", "When paying a Gold cost: Produce one Gold", "none", Trigger: "paying-gold-cost", UsageKey: "w829-koku");
        // The Duty of the Crab (oracle-l5r-8036)
        all[DutyFocus] = new("FocusEffect", "As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins", "none");
        all[DutyBattle] = new("Battle", "Battle: If you control any units at the current battlefield, choose your performing Crab Clan Personality who is face-up in your provinces or discarded (not dead): Bring him into play there, paying all costs",
            CrabRecruit, Cost: GoldCostRule.TargetRecruit());
        // Benten's Blessing (oracle-l5r-959): "Reaction: When another player's action would target one of your Personalities, target your performing Personality and discard this Celestial to have the action target him instead, if legal."
        all[BentenRedirect] = new("Reaction", "When another player's action would target one of your Personalities, target your performing Personality and discard this Celestial to have the action target him instead, if legal", "none",
            Trigger: ReactionTriggers.TargetedByAction, Performer: BentenPerformer, Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Jizo's Mercy (oracle-l5r-10563)
        all[JizoHome] = new("Battle", "Battle: Move a target attacking Personality home", "attacking");
        all[JizoMercy] = new("Battle", "Battle: Discard this Celestial. In this battle's resolution, before you destroy the Province, gain 4 Honor instead", "none"); // the discard is printed after the colon: an effect (Mendon, issue #1114)
        // The Onyx Championship (oracle-l5r-8242): the ability it permanently grants. Mendon: a usual recruit action, from the discard pile (dead Oni included).
        all[OnyxMaho] = new("Limited", "Maho Limited, bow this Shugenja: Destroy your other target Personality to recruit your target Oni Personality from your discard pile (dead ones included), paying all costs. Lose 3 Honor", "slots",
            Slots: [new(OnyxTarget, EffectPrimitiveTargets.CardInAnyDiscard("Oni", ownOnly: true), "Your Oni Personality in your discard pile"), new(OnyxSacrifice, OtherOwnPersonality, "Your other Personality to destroy")], // the recruited Oni is the first slot: the recruit cost reads it
            Costs: [.. ActionCost.BowSource, new(CostVerbs.Sacrifice, CostSubject.Slot(OnyxSacrifice))], Cost: GoldCostRule.TargetRecruit(), ActionKeywords: ["Maho"]);
        return all;
    }
}

/// <summary>A Minor Blackmail watch: when <see cref="PersonalityId"/> performs an action that resolves, or is assigned to a battlefield, he is dishonored and his controller loses 2 Honor.</summary>
public sealed class BlackmailWatch
{
    public string EventId { get; set; } = "";
    public string PersonalityId { get; set; } = "";
    public int PlayerId { get; set; }
}

/// <summary>Cross-Clan Wedding's second effect, waiting for the other player: give one of your Personalities (not the one just received) to <see cref="TakerId"/>.</summary>
public sealed class WeddingGive
{
    public string EventId { get; set; } = "";
    public string ReceivedId { get; set; } = "";
    public int GiverId { get; set; }
    public int TakerId { get; set; }
}

/// <summary>Benten's Blessing: the Celestial's choice of up to two Personalities, and who it is waiting on.</summary>
public sealed class BentenPick
{
    public string CelestialId { get; set; } = "";
    public int PlayerId { get; set; }
}

public sealed class Wave829State
{
    /// <summary>The Celestial that entered play by being revealed in a Province (Shrine to the Heavens asks only for that entry), until its entry decisions are over.</summary>
    public string? RevealedCelestialId { get; set; }
    public List<BlackmailWatch> Blackmail { get; set; } = [];
    public WeddingGive? Wedding { get; set; }
    /// <summary>Benten's Blessing cards whose entry choice is still to be asked, in the order they entered.</summary>
    public List<BentenPick> BentenQueue { get; set; } = [];
    /// <summary>Benten's Blessing card id to the Personalities it chose: +1 Personal Honor each while it remains in play.</summary>
    public Dictionary<string, List<string>> BentenChosen { get; set; } = [];
    /// <summary>Jizo's Mercy: the battlefields whose resolution this turn replaces the destruction of the Province with 4 Honor, by the player who played it.</summary>
    public List<JizoMark> Jizo { get; set; } = [];
    /// <summary>Benten's Blessing: the Personalities ticked so far in the open decision (zero to two), until it is confirmed.</summary>
    public List<string> BentenSelected { get; set; } = [];
    /// <summary>The priority and pass count a decision opened after a command took over, restored when it is answered.</summary>
    public int? ResumePriority { get; set; }
    public int ResumePasses { get; set; }
}

public sealed class JizoMark
{
    public int Battlefield { get; set; }
    public int PlayerId { get; set; }
    public int Turn { get; set; }
}

public sealed partial class MatchState
{
    // 23500-23599 is reserved for issue #829.
    [JsonPropertyOrder(23500)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Wave829State? Wave829 { get; set; }
}

public sealed partial class MatchEngine
{
    private Wave829State W829 => _state.Wave829 ??= new();
    private int OpponentOf(int playerId) => _state.Players.First(p => p.Id != playerId).Id;

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave829Validate(ValidationHookContext context)
    {
        var (id, command, player, source) = (context.id, context.command, context.player, context.source);
        switch (id)
        {
            // "If you control no Personalities": none in play (a Personality of yours at home or at a battlefield counts).
            case Wave829Abilities.CaughtOpen:
                Require(!_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality), "You control a Personality.");
                if (command.Selections?.FirstOrDefault(s => s.Slot == Wave829Abilities.CaughtSlot) is { } picked)
                    Require(picked.CardIds is { Length: > 0 and <= 2 } && picked.CardIds.Distinct().Count() == picked.CardIds.Length && picked.CardIds.All(i => _state.Cards.TryGetValue(i, out var c) && InPlay(c) && Definition(c).Type == CardType.Personality),
                        "Target one or two Personalities in play.");
                break;
            // "If you control any units at the current battlefield" and "choose your performing Crab Clan Personality face-up in your provinces or discarded".
            case Wave829Abilities.DutyBattle:
                Require(_state.CurrentBattleProvince is int battlefield && _state.Cards.Values.Any(c => c.OwnerId == player.Id && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && Definition(c).Type == CardType.Personality),
                    "You control no units at the current battlefield.");
                if (command.TargetId is { } crabId && _state.Cards.TryGetValue(crabId, out var crab))
                {
                    Require(Wave829CrabCandidate(player.Id, crab), "Choose a Crab Clan Personality of yours, face-up in a province or in your discard pile (not dead).");
                    var quote = QuotePurchase(player.Id, crab.Id, fromDeadPile: crab.Zone != Zone.Province);
                    Require(quote.Error is null, quote.Error ?? "");
                }
                break;
            // "target your performing Personality ... to have the action target him instead, if legal": another Personality of his controller's than the one the action targets.
            case Wave829Abilities.BentenRedirect:
                Require(command.PerformerId is { } redirectTo && HeldAction() is { } held && Wave829RedirectTo(player.Id, held, redirectTo), "Choose another of your Personalities that the action can legally target.");
                break;
            // "Battle: Discard this Celestial ...": only the attacking player may play it.
            case Wave829Abilities.JizoMercy:
                Require(_state.CurrentBattleProvince is not null && player.Id == _state.ActivePlayerId, "Only the attacking player may play this.");
                break;
            // The Onyx Championship: the Personality destroyed is another of his controller's Personalities (any location); the Oni is recruited from the discard pile (a Personality), paying all costs.
            case Wave829Abilities.OnyxMaho:
                {
                    var sacrifice = command.Selections?.FirstOrDefault(s => s.Slot == Wave829Abilities.OnyxSacrifice)?.CardIds?.FirstOrDefault();
                    Require(sacrifice is null || source is null || sacrifice != source.Id, "Choose another Personality to destroy.");
                    if (command.Selections?.FirstOrDefault(s => s.Slot == Wave829Abilities.OnyxTarget)?.CardIds?.FirstOrDefault() is { } oniId && _state.Cards.TryGetValue(oniId, out var oni))
                    {
                        Require(Definition(oni).Type == CardType.Personality && IsDiscardedPersonality(oni), "Choose an Oni Personality in your discard pile.");
                        var quote = QuotePurchase(player.Id, oni.Id, fromDeadPile: true);
                        Require(quote.Error is null, quote.Error ?? "");
                    }
                    break;
                }
        }
    }

    // ---- targets ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave829CrabCandidate(int playerId, CardInstance c) =>
        c.OwnerId == playerId && Definition(c).Type == CardType.Personality && IsClan(c, "Crab") && (c.Zone == Zone.Province && c.FaceUp || c.Zone is Zone.DynastyDiscard or Zone.FateDiscard);

    private bool IsClan(CardInstance c, string clan) => Definition(c).Clan == clan || Definition(c).Clans?.Contains(clan) == true || Keyword(c, clan + " Clan");

    // TargetAllows' hook: "an enemy card with no unbowed attachments" is in play, and has none of its attachments unbowed.
    private bool Wave829TargetAllows(string category, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        Wave829Abilities.NoUnbowedAttachments => InPlay(target) && target.OwnerId != player.Id && !Attachments(target.Id).Any(a => !a.Bowed),
        // "Destroy your other target Personality": one of his controller's Personalities, at any location, other than the Shugenja who performs it.
        Wave829Abilities.OtherOwnPersonality => InPlay(target) && target.OwnerId == player.Id && Definition(target).Type == CardType.Personality && source is not null && target.Id != source.Id,
        _ => false
    };

    // QueryActions' candidates for the two categories (the recruit's lie out of play).
    private string?[]? Wave829Candidates(string category, int playerId) => category != Wave829Abilities.CrabRecruit ? null
        : _state.Cards.Values.Where(c => Wave829CrabCandidate(playerId, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    private static bool Wave829IsOutOfPlayTarget(string category) => category == Wave829Abilities.CrabRecruit;

    // ---- effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave829(RunHookContext c)
    {
        var (id, command, player, source, target) = (c.id, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case Wave829Abilities.FarmHonor:
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            // Stone of Remembrance: it was removed from the game as the cost; the tear is shown by the client from this event.
            case Wave829Abilities.StoneNegate:
                Emit("cardTorn", $"{Definition(source!).Name} is torn in half: the destruction of {Definition(Card(HeldAction().TargetId!)).Name} from the action's effects is negated.", player.Id, source!.Id);
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case Wave829Abilities.CaughtOpen:
                foreach (var victim in SlotCards(command, Wave829Abilities.CaughtSlot).Distinct().ToArray())
                {
                    if (!InPlay(victim) || StalwartNegatesBow(victim, player.Id)) continue;
                    victim.Bowed = true;
                    LockStraightening(victim);
                }
                if (_state.ActivePlayerId != player.Id) Draw(player);
                return false;
            case Wave829Abilities.CaughtBattle:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case Wave829Abilities.BentenRedirect:
                RedirectAction(HeldAction(), command.PerformerId!, source!);
                return false;
            case Wave829Abilities.JizoHome:
                MoveHome(target!, false);
                return false;
            case Wave829Abilities.JizoMercy:
                if (_state.CurrentBattleProvince is int battlefield)
                {
                    W829.Jizo.RemoveAll(m => m.Turn != _state.TurnNumber);
                    W829.Jizo.Add(new JizoMark { Battlefield = battlefield, PlayerId = player.Id, Turn = _state.TurnNumber });
                    Emit("jizoMercy", $"{Definition(source!).Name}: in this battle's resolution, {player.Name} gains 4 Honor instead of destroying the Province.", player.Id, source!.Id);
                }
                WlItemsMiscDiscardCelestial(source!); // "Discard this Celestial." -- an effect, so a negated action leaves it in play (issue #1114)
                return false;
            // The Duty of the Crab: from a province (refilled) or the discard pile, at the current battlefield.
            case Wave829Abilities.DutyBattle:
                {
                    var crab = target!;
                    var battlefield2 = _state.CurrentBattleProvince!.Value;
                    var name = Definition(source!).Name;
                    var quote = QuotePurchase(player.Id, crab.Id, fromDeadPile: crab.Zone != Zone.Province);
                    Require(quote.Error is null && Definition(crab).Type == CardType.Personality, quote.Error ?? "");
                    SpendHandPlayedCard(player, source);
                    if (crab.Zone == Zone.Province) return BringProvincePersonalityIntoPlay(player, crab, quote, battlefield2, name);
                    return BringDeadPersonalityIntoPlay(player, crab, name, battlefield2);
                }
            // The Onyx Championship's Maho: the sacrifice was paid as a cost; the Oni comes into play paying all costs, then 3 Honor are lost.
            case Wave829Abilities.OnyxMaho:
                {
                    var oni = SlotCards(command, Wave829Abilities.OnyxTarget).First();
                    if (!IsDiscardedPersonality(oni)) { Emit("raiseFailed", $"{Definition(oni).Name} is no longer in the discard pile.", player.Id, oni.Id); return false; }
                    var paused = BringDeadPersonalityIntoPlay(player, oni, Definition(source!).Name, null);
                    GainHonor(player.Id, -3, Definition(source!).Name, source);
                    return paused;
                }
            default:
                return null;
        }
    }

    // ---- One Koku: a Strategy in hand as a Gold source ----------------------------------------------------------------------------------------------------------------------------------

    // PaymentTokens' contribution: each One Koku in the payer's hand may be discarded to produce 1 Gold toward any Gold cost (Near Miss's shape, without its attachment condition).
    private IEnumerable<PaymentToken> Wave829PaymentTokens(int playerId, PaymentSubject? paying)
    {
        if (paying is null) yield break; // like every Reaction Gold source: a payment with no subject (an internal one) has none
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Hand && c.DefinitionId == Wave829Abilities.Koku && c.Id != paying.Card.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
            if (PromoPaymentUsable(card, Wave829Abilities.KokuGold, true))
                yield return new(card.Id + PaymentTokenMark + Wave829Abilities.KokuKind, card, Wave829Abilities.KokuGold, 1, "reaction",
                    $"{Definition(card).Name}: discard it from your hand, produce 1 Gold", null, OnUse: static (engine, token, player) => engine.Wave829UseKoku(token, player), AllowedZones: [Zone.Hand]);
    }

    private void Wave829UseKoku(PaymentToken token, int playerId)
    {
        token.Card.AbilityUses[AbilityCatalog.All[Wave829Abilities.KokuGold].UsageKey ?? Wave829Abilities.KokuGold] = _state.TurnNumber;
        Emit("goldSourceUsed", $"{Definition(token.Card).Name} was discarded to produce 1 Gold.", playerId, token.Card.Id);
        DiscardFate(Player(playerId), token.Card);
    }

    // ---- Shrine to the Heavens ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "After one of your Celestials is revealed in a Province, you may choose one of your Celestials in play. If you do, it is not discarded because of the other Celestial entering play." Repeatable: each
    // Shrine in play is one more decision (the nth hook answers only while the player has n Shrines), asked only for a Celestial revealed in a Province; the choice is made when it is revealed, so a Shrine
    // destroyed afterwards triggers no discard.
    private CelestialEntryChoice? Wave829ShrineEntry(CelestialEntryContext context, int nth)
    {
        if (_state.Wave829?.RevealedCelestialId != context.Celestial.Id) return null;
        if (_state.Cards.Values.Count(c => c.OwnerId == context.Player.Id && InPlay(c) && Definition(c).Effect == Wave829Abilities.ShrineEffect) < nth) return null;
        var spared = _state.CelestialDiscardDebts?.FirstOrDefault(d => d.CelestialId == context.Celestial.Id)?.Spared ?? [];
        var candidates = _state.Cards.Values.Where(c => c.OwnerId == context.Player.Id && c.Zone == Zone.Celestial && c.Id != context.Celestial.Id && !spared.Contains(c.Id))
            .Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        return new($"Shrine to the Heavens: choose a Celestial that {Definition(context.Celestial).Name} entering play does not discard", candidates, static (_, _, _) => { }, Optional: true, SparesFromDiscard: true);
    }

    // ---- Cross-Clan Wedding --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave829WeddingOpen() => _state.PendingChoice is { Type: Wave829Abilities.WeddingType };

    private IEnumerable<CardInstance> WeddingPersonalities(int playerId, string? except = null) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && c.Id != except).OrderBy(c => c.Id, StringComparer.Ordinal);

    // The first effect: "choose a Personality you control and another player. The player takes control of the Personality."
    private RevealEventAsk? Wave829WeddingResolve(RevealedEvent revealed)
    {
        var candidates = WeddingPersonalities(revealed.Card.OwnerId).Select(c => c.Id).ToArray();
        return candidates.Length == 0 ? null : new($"{Definition(revealed.Card).Name}: choose a Personality you control to give to {Player(OpponentOf(revealed.Card.OwnerId)).Name}", candidates, Optional: false);
    }

    private void Wave829WeddingAnswer(RevealedEvent revealed, string? targetId)
    {
        if (targetId is null || !_state.Cards.TryGetValue(targetId, out var given)) return;
        var owner = revealed.Card.OwnerId; var other = OpponentOf(owner); var name = Definition(revealed.Card).Name;
        // A give by the controller of his own card is never stopped by a negation; Loyal still refuses it.
        if (!TransferUnitControl(given, other, name, checkLoyal: true, fromEffect: false)) return;
        // "If he now controls the Personality, he chooses another Personality he controls; take control of him." (Mendon: the second effect is the other player's, and not the Personality just received.)
        if (!WeddingPersonalities(other, given.Id).Any()) { Emit("weddingNothing", $"{name}: {Player(other).Name} controls no other Personality to give back.", other, revealed.Card.Id); return; }
        W829.Wedding = new WeddingGive { EventId = revealed.Card.Id, ReceivedId = given.Id, GiverId = other, TakerId = owner };
        _state.PendingChoice = new(Wave829Abilities.WeddingType, other, revealed.Card.Id, $"{name}: choose a Personality you control (not {Definition(given).Name}) to give to {Player(owner).Name}.");
        _state.PriorityPlayerId = other; _state.ConsecutivePasses = 0;
    }

    private IEnumerable<ActionOption> Wave829WeddingActions(int playerId)
    {
        if (_state.PendingChoice is not { } pending || pending.PlayerId != playerId || W829.Wedding is not { } wedding) return [];
        return WeddingPersonalities(playerId, wedding.ReceivedId).Select(c => new ActionOption(Wave829Abilities.WeddingCommand, $"Give {Definition(c).Name}", true, null,
            new GameCommand(Wave829Abilities.WeddingCommand, playerId, _state.Version, TargetId: c.Id), c.Id, DecisionCandidateIds: [c.Id], DecisionLabel: $"Give {Definition(c).Name} to {Player(wedding.TakerId).Name}")).ToArray();
    }

    private void Wave829AnswerWedding(GameCommand command)
    {
        var wedding = W829.Wedding ?? throw new RuleViolation("No Cross-Clan Wedding decision is open.");
        Require(command.Type == Wave829Abilities.WeddingCommand && command.PlayerId == wedding.GiverId, "Choose one of your Personalities to give.");
        Require(command.TargetId is not null && WeddingPersonalities(wedding.GiverId, wedding.ReceivedId).Any(c => c.Id == command.TargetId), "Choose a Personality you control, other than the one you just received.");
        var eventCard = Card(wedding.EventId);
        _state.PendingChoice = null; W829.Wedding = null;
        TransferUnitControl(Card(command.TargetId!), wedding.TakerId, Definition(eventCard).Name, checkLoyal: true, fromEffect: false);
        ContinueEventsReveal(Player(eventCard.OwnerId));
    }

    // ---- The Onyx Championship ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private RevealEventAsk? Wave829OnyxResolve(RevealedEvent revealed)
    {
        var candidates = _state.Cards.Values.Where(c => c.OwnerId == revealed.Card.OwnerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Shugenja")).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        return candidates.Length == 0 ? null : new($"{Definition(revealed.Card).Name}: choose one of your Shugenja to permanently give Shadowlands and the Maho ability", candidates, Optional: false);
    }

    private void Wave829OnyxAnswer(RevealedEvent revealed, string? targetId)
    {
        if (targetId is null || !_state.Cards.TryGetValue(targetId, out var shugenja) || !InPlay(shugenja)) return;
        GrantKeyword(shugenja, "Shadowlands", revealed.Card.Id, GrantDuration.Permanent);
        GrantAbility(shugenja, Wave829Abilities.OnyxMaho, revealed.Card.Id, GrantDuration.Permanent);
        Emit("abilityGranted", $"{Definition(revealed.Card).Name}: {Definition(shugenja).Name} permanently gains Shadowlands and {AbilityCatalog.All[Wave829Abilities.OnyxMaho].Timing}: {AbilityCatalog.All[Wave829Abilities.OnyxMaho].Label}", revealed.Card.OwnerId, shugenja.Id);
    }

    // ---- Minor Blackmail -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private RevealEventAsk? Wave829BlackmailResolve(RevealedEvent revealed)
    {
        var candidates = _state.Cards.Values.Where(c => c.OwnerId != revealed.Card.OwnerId && InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        return candidates.Length == 0 ? null : new($"{Definition(revealed.Card).Name}: choose another player's Personality in play", candidates, Optional: false);
    }

    private void Wave829BlackmailAnswer(RevealedEvent revealed, string? targetId)
    {
        if (targetId is null || !_state.Cards.TryGetValue(targetId, out var chosen) || !InPlay(chosen)) return;
        W829.Blackmail.Add(new BlackmailWatch { EventId = revealed.Card.Id, PersonalityId = chosen.Id, PlayerId = revealed.Card.OwnerId });
        Emit("watchSet", $"{Definition(revealed.Card).Name}: after the next time {Definition(chosen).Name}'s unit is assigned or an action resolves that he performed, he is dishonored and {Player(chosen.OwnerId).Name} loses 2 Honor.", revealed.Card.OwnerId, chosen.Id);
    }

    // The watch has no timer: it waits until the chosen Personality performs an action that resolves (a target does not count), or is assigned to a battlefield on either side.
    private void Wave829Fire(string personalityId)
    {
        if (_state.Wave829 is not { Blackmail.Count: > 0 } w) return;
        foreach (var watch in w.Blackmail.Where(b => b.PersonalityId == personalityId).ToArray())
        {
            w.Blackmail.Remove(watch);
            if (!_state.Cards.TryGetValue(personalityId, out var victim) || !InPlay(victim)) continue;
            var name = _state.Cards.TryGetValue(watch.EventId, out var eventCard) ? Definition(eventCard).Name : "Minor Blackmail";
            Emit("blackmail", $"{name}: {Definition(victim).Name} is dishonored and {Player(victim.OwnerId).Name} loses 2 Honor.", watch.PlayerId, victim.Id);
            Dishonor(victim, watch.PlayerId);
            GainHonor(victim.OwnerId, -2, name, _state.Cards.GetValueOrDefault(watch.EventId));
        }
    }

    private void Wave829ActionResolved(ResolvedActionFacts facts)
    {
        if (_state.Wave829 is not { Blackmail.Count: > 0 }) return;
        var performer = facts.PerformerId ?? (facts.SourceId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var source) && ActingPersonality(source) is { } acting && Definition(acting).Type == CardType.Personality ? acting.Id : null);
        if (performer is not null) Wave829Fire(performer);
    }

    private void Wave829Assigned(CardInstance card) => Wave829Fire(card.Id);

    // ---- Jizo's Mercy ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ContinueResolveBattle's question, asked when the attacker's army would destroy the Province: true when Jizo's Mercy was played for this battlefield this turn (the Province stays; 4 Honor instead).
    private bool Wave829JizoReplacesDestruction(int attacker, int battlefield)
    {
        if (_state.Wave829 is not { Jizo.Count: > 0 } w) return false;
        w.Jizo.RemoveAll(m => m.Turn != _state.TurnNumber);
        var mark = w.Jizo.FirstOrDefault(m => m.Battlefield == battlefield && m.PlayerId == attacker);
        if (mark is null) return false;
        w.Jizo.Remove(mark);
        GainHonor(attacker, 4, "Jizo's Mercy");
        Emit("jizoMercy", $"Jizo's Mercy: the Province is not destroyed; {Player(attacker).Name} gains 4 Honor instead.", attacker);
        return true;
    }

    // ---- Stone of Remembrance and Benten's Blessing: the targeted Reactions --------------------------------------------------------------------------------------------------

    private bool Wave829StoneApplies(CardInstance stone, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != stone.OwnerId && affected.OwnerId == stone.OwnerId && affected.Id != stone.Id && InPlay(affected) && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!) || DestroysAttachmentTarget(subject.Command.AbilityId!));

    // "Target your performing Personality ... to have the action target him instead, if legal": another Personality of the reactor's than the one targeted, which the action can legally target.
    private bool Wave829RedirectTo(int playerId, ReactionSubject held, string performerId) =>
        _state.Cards.TryGetValue(performerId, out var to) && to.OwnerId == playerId && InPlay(to) && Definition(to).Type == CardType.Personality && to.Id != held.TargetId && ActionAcceptsTarget(held, to.Id);

    internal IEnumerable<CardInstance> Wave829RedirectPerformers(int playerId, AbilityDefinition reaction) =>
        OpenReactionSubject() is { } held ? _state.Cards.Values.Where(c => Wave829RedirectTo(playerId, held, c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal) : [];

    private bool Wave829BentenApplies(CardInstance benten, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != benten.OwnerId && affected.OwnerId == benten.OwnerId && InPlay(affected) && Definition(affected).Type == CardType.Personality
        && _state.Cards.Values.Any(c => Wave829RedirectTo(benten.OwnerId, subject, c.Id));

    // ---- Benten's Blessing: the multi-pick decision ----------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Choose zero to two face-up Personalities in or out of play": in play, in a discard pile (the dead pile included) or face-up in a Province; never in a deck or face down.</summary>
    private IEnumerable<CardInstance> BentenCandidates() =>
        _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && (InPlay(c) || c.Zone == Zone.Province && c.FaceUp || c.Zone is Zone.DynastyDiscard or Zone.FateDiscard or Zone.Dead))
            .OrderBy(c => c.OwnerId).ThenBy(c => c.Id, StringComparer.Ordinal);

    // Shrine to the Heavens asks only for a Celestial revealed in a Province: the reveal marks it (null: a Celestial put into play with no Province). Kept in the saved state only while a Shrine could ask.
    private void Wave829CelestialRevealed(PlayerState player, CardInstance? revealed)
    {
        if (revealed is not null && _state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Effect == Wave829Abilities.ShrineEffect)) W829.RevealedCelestialId = revealed.Id;
        else if (_state.Wave829 is not null) W829.RevealedCelestialId = null;
    }

    private void Wave829CelestialEntered(PlayerState player, CardInstance card)
    {
        if (Definition(card).Effect != Wave829Abilities.BentenEffect) return;
        W829.BentenChosen.Remove(card.Id); // a fresh entry asks afresh: what it chose while last in play does not come back (issue #1114)
        W829.BentenQueue.Add(new BentenPick { CelestialId = card.Id, PlayerId = player.Id });
    }

    private int Wave829PersonalHonor(CardInstance card)
    {
        if (_state.Wave829 is not { BentenChosen.Count: > 0 } w) return 0;
        return w.BentenChosen.Count(kv => kv.Value.Contains(card.Id) && _state.Cards.TryGetValue(kv.Key, out var celestial) && celestial.Zone == Zone.Celestial);
    }

    private void Wave829Open(string type, int playerId, string cardId, string prompt)
    {
        W829.ResumePriority = _state.PriorityPlayerId; W829.ResumePasses = _state.ConsecutivePasses;
        _state.PendingChoice = new(type, playerId, cardId, prompt);
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
    }

    private void Wave829Resume()
    {
        if (W829.ResumePriority is int priority) { _state.PriorityPlayerId = priority; _state.ConsecutivePasses = W829.ResumePasses; }
        W829.ResumePriority = null;
    }

    // Run once after every command: the first Benten's Blessing that has entered play and not yet been asked opens its decision (nothing opens in the middle of an effect).
    private void Wave829AfterCommand()
    {
        if (_state.Wave829 is not { BentenQueue.Count: > 0 } w || _state.WinnerId is not null || _state.PendingChoice is not null || _state.PendingDuel is not null) return;
        while (w.BentenQueue.Count > 0)
        {
            var next = w.BentenQueue[0];
            if (!_state.Cards.TryGetValue(next.CelestialId, out var celestial) || celestial.Zone != Zone.Celestial || !BentenCandidates().Any()) { w.BentenQueue.RemoveAt(0); continue; }
            Wave829Open(Wave829Abilities.BentenType, next.PlayerId, celestial.Id, $"{Definition(celestial).Name}: choose zero to two face-up Personalities, in or out of play. They have +1 Personal Honor while it remains in play.");
            return;
        }
    }

    // The decision is Sleep's multi-select shape (Main.BudgetSelect.cs): the ENGINE keeps the selection, every click is one command, and the client's multi-select panel renders it from the three action
    // types below ("budgetSelectAdd" toggles one Personality, "budgetSelectClear" empties the selection, "budgetSelectConfirm" applies it). A click on a card on the table and a row of the list are the
    // same command, and the row highlights the card; nothing takes effect until Confirm.
    private IEnumerable<ActionOption> Wave829BentenActions(int playerId)
    {
        if (_state.PendingChoice is not { } pending || pending.PlayerId != playerId || pending.CardId is null) return [];
        var name = Definition(Card(pending.CardId)).Name;
        var selected = W829.BentenSelected;
        GameCommand Toggle(string id) => new(Wave829Abilities.BentenAdd, playerId, _state.Version, TargetId: id);
        var options = BentenCandidates().Select(c =>
        {
            var chosen = selected.Contains(c.Id);
            var full = !chosen && selected.Count >= 2;
            var label = $"{(chosen ? "Remove" : "Add")} {Definition(c).Name} ({Player(c.OwnerId).Name}, {BentenWhere(c)})";
            return new ActionOption(Wave829Abilities.BentenAdd, label, !full, full ? "Two Personalities are already chosen." : null, full ? null : Toggle(c.Id), c.Id, DecisionCandidateIds: [c.Id]);
        }).ToList();
        options.Add(new ActionOption(Wave829Abilities.BentenClear, "Clear selection", selected.Count > 0, selected.Count > 0 ? null : "Nothing is chosen.",
            selected.Count > 0 ? new GameCommand(Wave829Abilities.BentenClear, playerId, _state.Version) : null));
        options.Add(new ActionOption(Wave829Abilities.BentenConfirm, $"Confirm selection ({selected.Count} of 2)", true, null, new GameCommand(Wave829Abilities.BentenConfirm, playerId, _state.Version),
            DecisionCandidateIds: [.. selected], DecisionLabel: $"{name}: chosen {selected.Count} of 2 (zero to two)"));
        return options;
    }

    private static string BentenWhere(CardInstance c) => c.Zone switch
    {
        Zone.Province => "in a province",
        Zone.Dead => "dead",
        Zone.DynastyDiscard or Zone.FateDiscard => "discarded",
        _ => "in play"
    };

    private void Wave829AnswerBenten(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.PlayerId == pending.PlayerId && command.Type is Wave829Abilities.BentenAdd or Wave829Abilities.BentenClear or Wave829Abilities.BentenConfirm, "Choose up to two Personalities for Benten's Blessing, then confirm.");
        var celestial = Card(pending.CardId!);
        var selected = W829.BentenSelected;
        switch (command.Type)
        {
            case Wave829Abilities.BentenAdd:
                {
                    var id = command.TargetId;
                    Require(id is not null && BentenCandidates().Any(c => c.Id == id), "Choose a face-up Personality, in or out of play.");
                    if (!selected.Remove(id!))
                    {
                        Require(selected.Count < 2, "Two Personalities are already chosen.");
                        selected.Add(id!);
                    }
                    return;
                }
            case Wave829Abilities.BentenClear:
                selected.Clear();
                return;
        }
        var chosen = selected.Where(i => BentenCandidates().Any(c => c.Id == i)).ToArray();
        selected.Clear();
        _state.PendingChoice = null;
        W829.BentenQueue.RemoveAll(b => b.CelestialId == celestial.Id);
        if (chosen.Length > 0)
        {
            W829.BentenChosen[celestial.Id] = [.. chosen];
            Emit("bentenChosen", $"{Definition(celestial).Name}: {string.Join(" and ", chosen.Select(i => Definition(Card(i)).Name))} have +1 Personal Honor while it remains in play.", pending.PlayerId, celestial.Id);
        }
        else Emit("bentenChosen", $"{Definition(celestial).Name}: no Personality was chosen.", pending.PlayerId, celestial.Id);
        Wave829Resume();
    }

    // ---- registration -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave829Hooks()
    {
        EngineHooks.Abilities.Register("Wave829Abilities", 13829, static abilities => abilities.AddWave829());
        EngineHooks.ValidateEarly.Register("Wave829Validate", 13829, static (engine, context) => engine.Wave829Validate(context));
        EngineHooks.RunEffects.Register("Wave829RunEffects", 13829, static (engine, context) => engine.RunWave829(context));
        EngineHooks.PerformerAllows.Register("Wave829PerformerAllows", 13829, static (engine, context) => context.ability.Performer == Wave829Abilities.BentenPerformer && engine.OpenReactionSubject() is { } held && engine.Wave829RedirectTo(context.performer.OwnerId, held, context.performer.Id));
        EngineHooks.TargetAllows.Register("Wave829TargetAllows", 13829, static (engine, context) => engine.Wave829TargetAllows(context.ability.Target, context.player, context.source, context.target));
        EngineHooks.PaymentTokens.Register("Wave829PaymentTokens", 16829, static (engine, context) => engine.Wave829PaymentTokens(context.PlayerId, context.Paying));
        EngineHooks.TargetedReaction.Register("Wave829Targeted", 13829, static (engine, context) => context.abilityId switch
        {
            Wave829Abilities.StoneNegate => engine.Wave829StoneApplies(context.card, context.subject, context.affected),
            Wave829Abilities.BentenRedirect => engine.Wave829BentenApplies(context.card, context.subject, context.affected),
            _ => false
        });
        EngineHooks.AfterActionTraits.Register("Wave829Blackmail", 13829, static (engine, facts) => engine.Wave829ActionResolved(facts));
        CelestialEntryRegistry.Register("Wave829Shrine1", 13829, static (engine, context) => engine.Wave829ShrineEntry(context, 1));
        CelestialEntryRegistry.Register("Wave829Shrine2", 13830, static (engine, context) => engine.Wave829ShrineEntry(context, 2));
        CelestialEntryRegistry.Register("Wave829Shrine3", 13831, static (engine, context) => engine.Wave829ShrineEntry(context, 3));
        ProvinceRevealEvents.Register(Wave829Abilities.WeddingEffect, static (engine, revealed) => engine.Wave829WeddingResolve(revealed), static (engine, revealed, _, target) => engine.Wave829WeddingAnswer(revealed, target));
        ProvinceRevealEvents.Register(Wave829Abilities.OnyxEffect, static (engine, revealed) => engine.Wave829OnyxResolve(revealed), static (engine, revealed, _, target) => engine.Wave829OnyxAnswer(revealed, target));
        ProvinceRevealEvents.Register(Wave829Abilities.BlackmailEffect, static (engine, revealed) => engine.Wave829BlackmailResolve(revealed), static (engine, revealed, _, target) => engine.Wave829BlackmailAnswer(revealed, target));
        EngineHooks.PendingChoice.Register("Wave829Pending", 13829, static (engine, type, command) =>
        {
            switch (type)
            {
                case Wave829Abilities.WeddingType: engine.Wave829AnswerWedding(command); return true;
                case Wave829Abilities.BentenType: engine.Wave829AnswerBenten(command); return true;
                default: return false;
            }
        });
        EngineHooks.PendingActions.Register("Wave829PendingActions", 13829, static (engine, context) => context.Choice.Type switch
        {
            Wave829Abilities.WeddingType => engine.Wave829WeddingActions(context.PlayerId),
            Wave829Abilities.BentenType => engine.Wave829BentenActions(context.PlayerId),
            _ => null
        });
    }
}
