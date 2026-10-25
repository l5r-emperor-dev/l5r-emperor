namespace L5R.Core;

// The Shadow's Embrace pass (issue #476), batch 1: the execution/validation helpers of the 22 Personalities this batch wires (personality-wave-74) and the
// small general primitives they needed. The declarative data (ability ids, targets, costs) is in CardAbilities.cs; the traits are each card's Effect. Every
// record below expires by comparison with the turn (or battle) number, and is cleared where it is drained.
//
//   * A DELAYED PHASE-END AND TURN-END WATCH (MatchState.MisakiWatches): Bayushi Misaki's "Before this phase ends, straighten him. Before this turn ends, if
//     the Personality was not assigned to attack this turn, dishonor him and his controller loses Honor equal to his base Personal Honor" -- an Open action,
//     so "this phase" is the Action Phase (SetPhase leaving it); the turn-end half reads CardInstance.AssignedToAttackTurn (Tamori Tamoko's own check).
//   * A NEXT-PERFORMED-ACTION NEGATION (MatchState.NextPerformedActionNegations): Bayushi Waru's "negate the effects of the next action the target performs
//     (this turn)" -- keyed on the action's performer (ActionPerformerIdOrNull), spent by that action as it is announced; the action is still taken and its
//     costs paid (ResolveNegatedAction), and a Protection it carries leaves it standing (the mark is still spent).
//   * A BATTLE-SCOPED TARGET SHIELD (MatchState.BattleTargetShields): Hiruma Nikaru's "Negate the effects of the next Battle action this battle another player
//     takes which targets Nikaru" -- Hibernation's shield (TargetShield.cs) for one battle.
//   * A TURN-END REMOVAL (MatchState.TurnEndRemovals): Agasha Kurou's "Remove it from the game before the turn ends".
//   * A NON-EXPERIENCED OVERLAY (Goju Bunoro): the Experienced overlay's own state transfer (TransferOverlayState) onto the target from a face-up Personality in
//     his controller's province, the target refilling that province (face-down, the ordinary refill). The overlaying card does not enter play (core rules,
//     Experienced), so no entering-play effect fires; the target leaving play is an Overconfident trigger.
//   * STATIC TRAITS read where the rule they change is decided: Atosa's Clan alignment for Gaijin (StaticKeywordRules.cs, Seppun Nagi's shape), Bayushi Tarou's
//     entry discount (ForgottenLegacyEntryDiscount's price term), Chiisai's three restrictions (AttachmentLegalTarget/AttachmentAllowed, RequireCanPerform,
//     RequireLegalAttackTarget), Hida Bakari's -4F (PersonalityForce), Hiruma Hikazu's resolution bow (FinishBattleResolution), Nikaru's duel stat
//     (DuelStatValue), Kakita Ichigiku's challenge (IssueChallenge, ResolveDuel) and Kazehime's hand size (HandLimitFor).

/// <summary>Bayushi Misaki: <paramref name="TargetId"/> is straightened when the phase ends, then checked when turn <paramref name="Turn"/> ends.</summary>
public sealed record MisakiWatch(string TargetId, string SourceCardId, int ActingPlayerId, int Turn, bool Straightened = false, string Kind = "");

/// <summary>Bayushi Waru: the next action <paramref name="CardId"/> performs on turn <paramref name="Turn"/> is negated.</summary>
public sealed record NextPerformedActionNegation(string CardId, int Turn, string Source);

/// <summary>Hiruma Nikaru: the next Battle action of another player's that targets <paramref name="CardId"/> in battle <paramref name="Battle"/> is negated.</summary>
public sealed record BattleTargetShield(string CardId, int Battle, int Turn, string Source);

/// <summary>Agasha Kurou: <paramref name="CardId"/> is removed from the game as turn <paramref name="Turn"/> ends.</summary>
public sealed record TurnEndRemoval(string CardId, int Turn, string Source);

public sealed partial class MatchEngine
{
    private const string AtosaGaijinClanEffect = "atosa-gaijin-clan";
    private const string TarouCourtierDiscountEffect = "tarou-courtier-discount";
    private const string ChiisaiRestrictionsEffect = "chiisai-restrictions";
    private const string BakariUnopposedEffect = "bakari-unopposed-penalty";
    private const string HikazuFollowerResolutionEffect = "hikazu-follower-resolution";
    private const string NikaruForceDuelStatEffect = "nikaru-force-duel-stat";
    private const string IchigikuChallengeEffect = "ichigiku-dishonorable-challenge";
    private const string Se1BowCardEffect = "se1-bow-card:";
    private const string Se1OverlayEffect = "se1-overlay:";
    private const string Se1DeclineEffect = "se1-decline";

    // ---- validation ----

    private void ValidateShadowsEmbraceBatch1(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        var target = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var chosen) ? chosen : null;
        switch (id)
        {
            // Bayushi Misaki: "If it is another player's turn, target one of his Personalities."
            case "misaki-straighten":
                Require(_state.ActivePlayerId != player.Id, "Bayushi Misaki acts only on another player's turn.");
                if (target is not null) Require(target.OwnerId == _state.ActivePlayerId, "Target one of the active player's Personalities.");
                break;
            // Chiisai: "an enemy Personality with 5 or lower Force".
            case "chiisai-charge":
                if (target is not null) Require(CardForce(target) <= 5, "The target must have 5 or lower Force.");
                break;
            // Goju Bunoro: "a non-Unique enemy Personality".
            case "bunoro-displace":
                if (target is not null) Require(!Keyword(target, "Unique"), "Target a non-Unique enemy Personality.");
                break;
            // Kaiu Fumiko: "Force LOWER than this battlefield's province's strength" (the shared category allows equal).
            case "fumiko-bow-raise":
            case "fumiko-bow-lower":
                if (target is not null && _state.CurrentBattleProvince is not null)
                    Require(CardForce(target) < CurrentProvinceStrength(),
                        "The target's Force must be lower than this battlefield's province's strength.");
                break;
            // Iuchi Haruma: "Bow one of Haruma's Spells" -- a cost must fully happen (core rules, Pay Costs), so the Spell must be unbowed.
            case "haruma-attack":
                if (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var spell))
                    Require(!spell.Bowed, $"{Definition(spell).Name} is already bowed, so it cannot be bowed to pay this cost.");
                break;
            // Kaiu Fumiko: "After YOUR Ranged Attack is targeted".
            case "fumiko-attack-boost":
                Require(_state.PendingAttack is { Kind: "Ranged" } attack && attack.PlayerId == player.Id, "Kaiu Fumiko only reacts to your own Ranged Attack.");
                break;
        }
    }

    // ---- effects ----

    private bool RunShadowsEmbraceBatch1Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target,
        bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var name = source is null ? "An action" : Definition(source).Name;
        switch (id)
        {
            // Agasha Kurou: the removal is queued and the additional action (scoped to the Spell) granted, then the attach -- whose entering-play window, if any,
            // is where the action pauses (Moto Jun-Ni's ExecuteAttach). The Gold was paid by PayForAbility (AttachCost).
            case "kurou-attach-spell":
                _state.TurnEndRemovals.Add(new(target!.Id, _state.TurnNumber, name));
                GrantAdditionalAction(player.Id, [target.Id]);
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            case "keigo-draw":
                Draw(player);
                return false;
            case "hayate-cavalry": GrantKeyword(source!, "Cavalry", source!.Id, GrantDuration.Turn); return false;
            case "hayate-naval": GrantKeyword(source!, "Naval", source!.Id, GrantDuration.Turn); return false;
            case "hayate-tactician": GrantKeyword(source!, "Tactician", source!.Id, GrantDuration.Turn); return false;
            // Asako Jirou: the condition is read first; then the keyword is given (for the turn).
            case "jirou-kolat":
            case "jirou-ninja":
            case "jirou-shadowlands":
                if (EffectConditionMet(id, target))
                    foreach (var usageKey in PrintedActionUsageKeys(target!)) DisableAbility(target!, usageKey, name);
                GrantKeyword(target!, id switch { "jirou-kolat" => "Kolat", "jirou-ninja" => "Ninja", _ => "Shadowlands" }, source!.Id, GrantDuration.Turn);
                return false;
            case "misaki-straighten":
                _state.MisakiWatches.Add(new(target!.Id, source!.Id, player.Id, _state.TurnNumber));
                Emit("misakiWatch", $"{name}: {Definition(target).Name} will be straightened before this phase ends, and dishonored before the turn ends unless he is assigned to attack.", player.Id, target.Id);
                return false;
            case "waru-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (ControlsKeyword(player.Id, "Courtier"))
                {
                    _state.NextPerformedActionNegations.RemoveAll(n => n.Turn != _state.TurnNumber);
                    _state.NextPerformedActionNegations.Add(new(target!.Id, _state.TurnNumber, name));
                    Emit("nextActionNegationRaised", $"{name}: the effects of the next action {Definition(target).Name} performs this turn will be negated.", player.Id, target.Id);
                }
                return false;
            case "chiisai-charge":
                MoveHome(target!, false);
                return false;
            case "hitaka-destroy-jade":
                DestroyTargetCard(target!, player.Id);
                return false;
            // Chuda Hitaka: Hitaka was destroyed as the cost; the Oni enters at home (a Limited action has no battlefield), priced by the TargetRecruit rule.
            case "hitaka-recruit-oni":
                {
                    var quote = QuotePurchase(player.Id, target!.Id, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                    return BringDeadPersonalityIntoPlay(player, target, name, null);
                }
            // Daidoji Sosuke: his whole unit if he is Mantis Clan; otherwise the acting player chooses the card without attachments in his unit (Asako Kaitoko's reading:
            // him when he has none, else one of his Followers without attachments).
            case "sosuke-tariffs":
                {
                    if (HasClanAffiliation(target!, "Mantis"))
                    {
                        BowUnitByEffect(target!, player.Id);
                        return false;
                    }
                    var candidates = UnitCardsWithoutAttachments(target!); // Makito's reading (issue #498): any card of the unit without attachments, not only Followers.
                    var options = candidates.Select(c => new EffectOption(c.Id, $"Bow {Definition(c).Name}", [Se1BowCardEffect + c.Id])).ToArray();
                    return BeginShadowsEmbraceBatch1Choice(command, options, $"{name}: choose the card without attachments in {Definition(target!).Name}'s unit to bow.",
                        consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            // Daigotsu Arakan: Costly Feud's shape -- the penalty, then the bow if his Force is now 0 or he is Lion Clan (read after the penalty).
            case "arakan-weaken":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                if (InPlay(target!) && (CardForce(target!) == 0 || HasClanAffiliation(target!, "Lion")) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "rafu-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // Iuchi Haruma: the Spell was bowed as the cost; +2F first, then the Attack.
            case "haruma-attack":
                ApplyModifier(source!, source!.Id, id, "Force", 2);
                return ResolveOrDeferAttack(id, player, target?.Id, source.Id, consumedGrant);
            // Goju Bunoro: every card in the target's controller's provinces is turned face-up (a Personality there is then public information), then the acting
            // player may choose one face-up Personality there to overlay on the target -- or not.
            case "bunoro-displace":
                {
                    var owner = Player(target!.OwnerId);
                    foreach (var province in owner.Provinces.Where(p => !p.Destroyed && p.CardId is not null))
                    {
                        var card = Card(province.CardId);
                        if (card.FaceUp) continue;
                        card.FaceUp = true;
                        Emit("provinceRevealed", $"{name}: {Definition(card).Name} in {owner.Name}'s province {province.Index + 1} is turned face-up.", player.Id, card.Id);
                    }
                    if (!InPlay(target)) return false;
                    var overlays = owner.Provinces.Where(p => !p.Destroyed && p.CardId is not null).Select(p => Card(p.CardId)).Where(c => BunoroMayOverlay(c, target)).ToArray();
                    if (overlays.Length == 0) return false;
                    var options = overlays.Select(c => new EffectOption(c.Id, $"Overlay {Definition(c).Name} on {Definition(target).Name}", [Se1OverlayEffect + c.Id]))
                        .Append(new EffectOption("decline", "Do not overlay", [Se1DeclineEffect])).ToArray();
                    return BeginShadowsEmbraceBatch1Choice(command, options, $"{name}: you may overlay a face-up Personality in a province on {Definition(target).Name}.",
                        consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case "nikaru-exp-shield":
                _state.BattleTargetShields.RemoveAll(s => s.Turn != _state.TurnNumber || s.Battle != _state.BattleNumber);
                _state.BattleTargetShields.Add(new(source!.Id, _state.BattleNumber, _state.TurnNumber, name));
                Emit("targetShieldRaised", $"The next Battle action of another player's that targets {name} this battle will be negated.", player.Id, source.Id);
                return false;
            // Kaiu Fumiko: the held Attack gains +2 strength, then her controller gains 1 Honor (from an action she performed: a dishonorable Fumiko is rehonored).
            case "fumiko-attack-boost":
                _state.PendingAttack!.Strength += 2;
                GainHonorFromPerformer(source!, 1, name);
                return false;
            case "fumiko-bow-raise":
            case "fumiko-bow-lower":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (CurrentProvinceIndex() is int battleProvince)
                    GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, battleProvince, id == "fumiko-bow-raise" ? 2 : -2, name);
                return false;
            default:
                throw new InvalidOperationException($"Unknown The Shadow's Embrace batch 1 ability '{id}'.");
        }
    }

    // One printed action split into several catalog entries (a shared UsageKey) is one ability; only the card's own printed (base) actions.
    private IEnumerable<string> PrintedActionUsageKeys(CardInstance card) =>
        (Definition(card).Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && IsAction(d)).Select(AbilityUsageKey).Distinct(StringComparer.Ordinal).ToArray();

    // A resolution-time choice (ForgottenLegacyBatch1.cs's engine-listed options): nothing is asked with one option (it simply happens) or none.
    private bool BeginShadowsEmbraceBatch1Choice(GameCommand command, EffectOption[] options, string prompt, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (options.Length == 0) return false;
        if (options.Length == 1)
        {
            foreach (var effect in options[0].Effects) ApplyShadowsEmbraceBatch1BranchEffect(effect, command);
            return false;
        }
        return BeginListedEffectChoice(command, options, prompt, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    // ApplyBranchEffect's arm for this batch's listed options; false for any other effect.
    private bool ApplyShadowsEmbraceBatch1BranchEffect(string effect, GameCommand command)
    {
        if (effect == Se1DeclineEffect) return true;
        if (effect.StartsWith(Se1BowCardEffect, StringComparison.Ordinal))
        {
            var card = Card(effect[Se1BowCardEffect.Length..]);
            if (InPlay(card) && !StalwartNegatesBow(card, command.PlayerId)) card.Bowed = true;
            return true;
        }
        if (effect.StartsWith(Se1OverlayEffect, StringComparison.Ordinal))
        {
            if (command.TargetId is { } targetId) BunoroOverlay(Card(effect[Se1OverlayEffect.Length..]), Card(targetId), command.CardId is { } sourceId ? Definition(Card(sourceId)).Name : "An action");
            return true;
        }
        return false;
    }

    // ---- Goju Bunoro's overlay ----

    // A face-up Personality in the target's controller's province that could be overlaid on him: the same control restrictions a Experienced overlay keeps (Loyal,
    // Unique/Singular against the controller's other cards in play).
    private bool BunoroMayOverlay(CardInstance card, CardInstance target) =>
        card.Zone == Zone.Province && card.FaceUp && card.OwnerId == target.OwnerId && Definition(card).Type == CardType.Personality
        && LoyaltyAllowsControl(card, Player(target.OwnerId).Clan)
        && !((Keyword(card, "Unique") || Keyword(card, "Singular")) && _state.Cards.Values.Any(c => c.Id != target.Id && c.OwnerId == target.OwnerId && InPlay(c)
            && string.Equals(Experienced.BaseTitle(Definition(c).Name), Experienced.BaseTitle(Definition(card).Name), StringComparison.OrdinalIgnoreCase)));

    private void BunoroOverlay(CardInstance replacement, CardInstance target, string sourceName)
    {
        if (!InPlay(target) || !BunoroMayOverlay(replacement, target)) return;
        var owner = Player(target.OwnerId);
        var province = owner.Provinces.Single(p => p.CardId == replacement.Id);
        province.CardId = null;
        QueueOverconfidentDraw(target); // he leaves play
        TransferOverlayState(target, replacement);
        // "refilling the province with the target": he fills it the way a province is refilled, face-down.
        target.Zone = Zone.Province; target.ProvinceIndex = province.Index; target.FaceUp = false; target.AttachedTo = null;
        province.CardId = target.Id;
        Emit("personalityOverlayed", $"{sourceName}: {Definition(replacement).Name} is overlaid on {Definition(target).Name}, who refills {owner.Name}'s province {province.Index + 1}.",
            owner.Id, replacement.Id);
    }

    // ---- the one-shot negations (ContinueUseAbility, as an action is announced with its targets) ----

    private bool ShadowsEmbraceBatch1Negates(GameCommand command)
    {
        if (command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || !IsAction(ability)) return false;
        // Hiruma Nikaru: the next Battle action this battle of another player's that targets him.
        if (IsBattleAction(command))
        {
            var shielded = ActionTargetIds(command).Where(t => _state.Cards.ContainsKey(t)).Select(Card).FirstOrDefault(c => c.OwnerId != command.PlayerId && InPlay(c)
                && _state.BattleTargetShields.Any(s => s.CardId == c.Id && s.Battle == _state.BattleNumber && s.Turn == _state.TurnNumber));
            if (shielded is not null)
            {
                _state.BattleTargetShields.RemoveAll(s => s.CardId == shielded.Id);
                if (ActionProtected(command))
                    Emit("actionProtected", $"Protection: {Player(command.PlayerId).Name}'s action on {Definition(shielded).Name} cannot be negated (his shield is spent).", shielded.OwnerId, shielded.Id);
                else
                {
                    Emit("actionNegated", $"{Definition(shielded).Name}'s shield: {Player(command.PlayerId).Name}'s action ({ability.Label}) is negated.", shielded.OwnerId, shielded.Id);
                    ResolveNegatedAction(command);
                    return true;
                }
            }
        }
        // Bayushi Waru: the next action the marked Personality performs this turn.
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        if (ActionPerformerIdOrNull(ability, command, source) is { } performerId
            && _state.NextPerformedActionNegations.FirstOrDefault(n => n.CardId == performerId && n.Turn == _state.TurnNumber) is { } mark)
        {
            _state.NextPerformedActionNegations.Remove(mark);
            if (ActionProtected(command))
            {
                Emit("actionProtected", $"Protection: the action {Definition(Card(performerId)).Name} performs cannot be negated ({mark.Source}'s negation is spent).", command.PlayerId, performerId);
                return false;
            }
            Emit("actionNegated", $"{mark.Source}: the action {Definition(Card(performerId)).Name} performs ({ability.Label}) is negated.", command.PlayerId, performerId);
            ResolveNegatedAction(command);
            return true;
        }
        return false;
    }

    // ---- phase end and turn end ----

    // Bayushi Misaki: "Before this phase ends, straighten him" -- her Open action's phase is the Action Phase.
    private void ResolveShadowsEmbraceBatch1PhaseEnd(Phase next)
    {
        if (_state.Phase != Phase.Action || next == Phase.Action) return;
        for (var i = 0; i < _state.MisakiWatches.Count; i++)
        {
            var watch = _state.MisakiWatches[i];
            if (watch.Turn != _state.TurnNumber || watch.Straightened) continue;
            _state.MisakiWatches[i] = watch with { Straightened = true };
            var target = Card(watch.TargetId);
            if (InPlay(target)) Straighten(target);
        }
    }

    private void ResolveShadowsEmbraceBatch1TurnEnd()
    {
        // Bayushi Misaki: "if the Personality was not assigned to attack this turn, dishonor him and his controller loses Honor equal to his base Personal Honor."
        foreach (var watch in _state.MisakiWatches.Where(w => w.Turn == _state.TurnNumber).ToArray())
        {
            var target = Card(watch.TargetId);
            if (!InPlay(target) || target.AssignedToAttackTurn == _state.TurnNumber) continue;
            var misaki = _state.Cards.TryGetValue(watch.SourceCardId, out var found) ? found : null;
            if (watch.Kind == Wave859Abilities.KazushigeKind) { Wave859KazushigeTurnEnd(watch, misaki); continue; } // issue #859: Tamori Kazushige's "gain 2 Honor"
            Dishonor(target, watch.ActingPlayerId);
            GainHonor(target.OwnerId, -Definition(target).PersonalHonor, misaki is null ? "Bayushi Misaki" : Definition(misaki).Name, misaki);
        }
        _state.MisakiWatches.Clear();
        // Agasha Kurou: "Remove it from the game before the turn ends." Wherever the Spell went after he attached it (in play, or back in a discard pile).
        foreach (var removal in _state.TurnEndRemovals.Where(r => r.Turn == _state.TurnNumber).ToArray())
        {
            var spell = Card(removal.CardId);
            if (spell.Zone is Zone.Attached or Zone.FateDiscard) RemoveFromGame(spell);
        }
        _state.TurnEndRemovals.Clear();
        _state.NextPerformedActionNegations.Clear();
        _state.BattleTargetShields.Clear();
    }

    // ---- entering play ----

    // Atosa: "After Atosa enters play: Lose 2 Honor [her EntryHonorLoss] and discard all other Sensei Personalities you control." -- the ones in play (a province card
    // is not controlled), with their attachments discarded along with them.
    private void ApplyShadowsEmbraceBatch1EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != AtosaGaijinClanEffect) return;
        foreach (var other in _state.Cards.Values.Where(c => c.Id != card.Id && c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Sensei")).ToArray())
            DiscardPersonalityFromPlay(other, Definition(card).Name);
    }

    private void DiscardPersonalityFromPlay(CardInstance personality, string sourceName)
    {
        foreach (var attachment in Attachments(personality.Id).ToArray())
        {
            RemoveAttachmentGrants(personality, attachment.Id);
            attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.FaceUp = true; attachment.Tokens.Clear();
        }
        personality.Zone = Zone.DynastyDiscard; personality.ProvinceIndex = null; personality.FaceUp = true; personality.Tokens.Clear();
        Emit("cardDiscarded", $"{Definition(personality).Name} was discarded ({sourceName}).", personality.OwnerId, personality.Id);
    }

    // ---- static traits ----

    // Atosa: "Your Gaijin Personalities in and out of play (including Atosa) have your Clan alignment." In play, a rule over her controller's Gaijin Personalities
    // wherever they are; out of play, her trait still reaches the one card it is printed on (core rules, Traits) -- so she has it in her province, when bought.
    private bool AtosaCovers(CardInstance card) => Definition(card).Type == CardType.Personality && Keyword(card, "Gaijin");
    private bool AtosaCoversHerself(CardInstance card) => Definition(card).Effect == AtosaGaijinClanEffect;

    // Chiisai: "Will not attach Items." Matsu Sango prints the same (StatTermCards.cs, issue #672).
    private bool WillNotAttachItems(CardDefinition attachment, CardInstance target) => attachment.Type == CardType.Item && (Definition(target).Effect == ChiisaiRestrictionsEffect || StatTermCardWillNotAttachItems(target));

    // Chiisai: "Melee Attacks may not target Chiisai's Followers."
    private bool IsChiisaisFollower(CardInstance card) =>
        Definition(card).Type == CardType.Follower && card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host)
        && Definition(host).Effect == ChiisaiRestrictionsEffect;

    // Chiisai: "May not perform Iaijutsu or Political actions." Burning Dreams' performer reading (RequireBurningDreamsAllows): the chosen Personality/Follower
    // performer, else the source card (an Item's or Spell's action through its wielder); the rulebook Lobby is performed by the Personality it bows.
    private void RequireChiisaiAllows(GameCommand command, AbilityDefinition ability, CardInstance? source)
    {
        if (!IsAction(ability) || command.AbilityId is not { } id) return;
        var political = id is "lobby" or "dainagon-lobby" || ActionCarriesKeyword(ability, source, "Political");
        if (!political && !ActionCarriesKeyword(ability, source, "Iaijutsu")) return;
        var performers = new List<CardInstance>();
        if (id == "lobby") { if (command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var lobbying)) performers.Add(lobbying); }
        else
        {
            var named = ability.Performer is not null && command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var chosen) ? chosen : null;
            var actor = named is not null && Definition(named).Type is CardType.Personality or CardType.Follower ? named : source;
            if (actor is { Zone: Zone.Attached, AttachedTo: { } hostId } && Definition(actor).Type is CardType.Item or CardType.Spell) actor = Card(hostId);
            if (actor is not null) performers.Add(actor);
            if (ability.Slots is not null) performers.AddRange(PerformingSlotCards(command, ability));
        }
        foreach (var performer in performers)
            if (Definition(performer).Effect == ChiisaiRestrictionsEffect)
                RequireNotBarred(false, $"{Definition(performer).Name} performing Iaijutsu or Political actions", "Chiisai");
    }

    // Hida Bakari: "Bakari has -4F while he is unopposed." -- at a battlefield where the enemy army has no unit (core rules, Opposed); at home he is neither.
    private int ShadowsEmbraceBatch1Force(CardInstance card) =>
        Definition(card).Effect == BakariUnopposedEffect && card.Zone == Zone.Battlefield && card.ProvinceIndex is int province && !Army(1 - card.OwnerId, province).Any() ? -4 : 0;

    // Hiruma Hikazu: "Battle resolution will not bow Personalities with unbowed Followers in Hikazu's army. (Their Followers still bow.)" -- read on the army about to
    // be bowed by its resolution, while Hikazu stands in it.
    private HashSet<string> HikazuSparedPersonalities(CardInstance[] army)
    {
        var spared = new HashSet<string>(StringComparer.Ordinal);
        if (!army.Any(c => c.Zone == Zone.Battlefield && Definition(c).Effect == HikazuFollowerResolutionEffect)) return spared;
        foreach (var personality in army.Where(c => c.Zone == Zone.Battlefield && Definition(c).Type == CardType.Personality))
            if (Attachments(personality.Id).Any(a => Definition(a).Type == CardType.Follower && !a.Bowed)) spared.Add(personality.Id);
        return spared;
    }

    // Hiruma Nikaru (Shadow's Embrace): "Nikaru uses his Force as his duel stat in duels from other players' cards." A duel is from the card whose action issued it
    // (PendingDuel.SourceCardId), else from its challenger's player.
    private int? NikaruDuelStat(CardInstance card)
    {
        if (Definition(card).Effect != NikaruForceDuelStatEffect || _state.PendingDuel is not { } duel) return null;
        var from = duel.SourceCardId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var source) ? source.OwnerId : duel.ChallengerPlayerId;
        return from != card.OwnerId ? CardForce(card) : null;
    }

    // Kakita Ichigiku: "After Ichigiku challenges a dishonorable Personality during a Combat Segment: The challenge will not be refused; destroy that duel's loser in
    // addition to other consequences of losing." A trait (mandatory), read as the challenge is issued; the loser (both, in a tie) is destroyed after the duel's own
    // outcome.
    private bool IchigikuChallenge(CardInstance challenger, CardInstance challenged) =>
        Definition(challenger).Effect == IchigikuChallengeEffect && challenged.Dishonorable && _state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null;

    private void IchigikuDestroyLosers(List<string> loserIds, int challengerPlayerId)
    {
        var losers = loserIds.Where(_state.Cards.ContainsKey).Select(Card).Where(InPlay).ToArray();
        if (losers.Length > 0) DestroyUnits(losers, false, challengerPlayerId);
    }

    // Kakita Tadanobu: "If it is your turn, the winner's controller draws a card. Bow the duel's loser." A tie has no winner (both lose).
    private void TadanobuDuelOutcome(string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        if (challengerPlayerId == _state.ActivePlayerId && winnerId is not null && _state.Cards.TryGetValue(winnerId, out var winner)) Draw(Player(winner.OwnerId));
        foreach (var loserId in loserIds)
        {
            var loser = Card(loserId);
            if (InPlay(loser) && !StalwartNegatesBow(loser, challengerPlayerId)) loser.Bowed = true;
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterShadowsEmbraceBatch1Hooks()
    {
        EngineHooks.PersonalityForce.Register("ShadowsEmbraceBatch1Force", 1300, static (engine, context) => engine.ShadowsEmbraceBatch1Force(context.card));
        EngineHooks.ValidateLate.Register("ValidateShadowsEmbraceBatch1", 400, static (engine, context) => engine.ValidateShadowsEmbraceBatch1(context.id, context.command, context.player, context.source));
    }
}
