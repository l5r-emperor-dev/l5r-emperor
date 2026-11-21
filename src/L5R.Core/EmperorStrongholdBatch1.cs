namespace L5R.Core;

// Emperor Edition Stronghold pass (issue #486), batch 1: the seven Strongholds wired in stronghold-wave-17 (Eternal Victory Dojo, Hidden Falls Dojo, Keep of the Dead, Koshin Keep,
// Kyuden Otomo - exp, Library of Rebirth, Midday Shadow Court). Halls of the Forgotten stays unwired: see tools/content/ee_sh_batch1_blockers.json.
// The declarative ability data lives here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file, each naming its card.
//
// New primitives:
//   * A DECK-CONSTRUCTION RULE PRINTED ON A STRONGHOLD (Eternal Victory Dojo): DeckWorkshop.StrategyCopyRuleOf / BattleStrategyOf, read by DeckWorkshop.Review.
//   * A GOLD SOURCE THAT IS A REACTION TO A KIND OF PAYMENT, EVEN IF ITS CARD IS BOWED (Library of Rebirth): AbilityDefinition.PaymentWhen / PaymentEvenIfBowed, beside Sakuma's
//     alternate source (GoldQuotes.cs) but for a card that also has a printed production.
//   * A DELAYED DRAW AT THE NEXT END PHASE, WHOSE EVER IT IS (Eternal Victory Dojo): MatchState.PendingEndPhaseDraws, drawn as the End Phase begins.
//   * A RESTRICTED ADDITIONAL ACTION (Koshin Keep): AdditionalActionGrant.RangedTargetId -- "an additional Ranged Attack Battle action that may only target the same card".
//   * FAVOR TOKENS AND A FAVOR-DISCARD SUBSTITUTION (Kyuden Otomo - exp): every action that discards the Imperial Favor as a cost may instead discard a Favor token from the
//     Stronghold (a cost option of the command); every way of taking the Favor is recorded on the action (TakeImperialFavor) for "after you take the Imperial Favor".
//   * A PROVINCE-SET TARGET AND AN ADDITIONAL INFANTRY MANEUVERS SEGMENT (Hidden Falls Dojo): GameCommand.Provinces, MatchState.HiddenFallsGrants / ExtraInfantrySegment.
//   * A WATCH THAT FIRES ON EITHER SIDE'S ASSIGNMENT OR THE END OF AN ACTION THE TARGET PERFORMED (Midday Shadow Court): a new AssignmentWatch kind.
public sealed record HiddenFallsGrant(int PlayerId, int[] Provinces, int Turn);

public static class EmperorStrongholdBatch1Abilities
{
    public const string HiddenFallsTarget = "defender-provinces";

    public static Dictionary<string, AbilityDefinition> AddEmperorStrongholdBatch1(this Dictionary<string, AbilityDefinition> all)
    {
        // Eternal Victory Dojo (oracle-l5r-2388): "Reaction: After your Tactical action resolves: Draw a card after the next End Phase begins." The after-action-resolved window
        // (Jong-Ho's shape without a performer); the draw is owed until the next End Phase begins, whichever player's it is (MatchState.PendingEndPhaseDraws).
        all["eternal-victory-draw"] = new("Reaction", "After your Tactical action resolves: Draw a card after the next End Phase begins", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Hidden Falls Dojo (oracle-l5r-3257): "Recon Open: Target one or two provinces: You have Reconnaissance at their battlefields. After each Cavalry Maneuvers Segment there is an
        // additional Infantry Maneuvers Segment in which only your Scout Personalities may be assigned and only to the targeted provinces' battlefields."
        all["hidden-falls-recon"] = new("Open", "Recon Open: Target one or two provinces: You have Reconnaissance at their battlefields. After each Cavalry Maneuvers Segment there is an additional Infantry Maneuvers Segment in which only your Scout Personalities may be assigned and only to the targeted provinces' battlefields",
            EmperorStrongholdBatch1Abilities.HiddenFallsTarget, ActionKeywords: ["Recon"]);
        // Keep of the Dead (oracle-l5r-4310): "Battle: Choose your performing Commander: If any enemy units are at the current battlefield or he assigned there, move him there.
        // Straighten his unit if he moved." Charge Into Danger's condition ("if he assigned to, or would be opposed at, the current battlefield"), read in the effects block.
        all["keep-of-dead-move"] = new("Battle", "Choose your performing Commander: If any enemy units are at the current battlefield or he assigned there, move him there. Straighten his unit if he moved", "none",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Commander"], MovesToBattle: true);
        // Koshin Keep (oracle-l5r-4541): "Recon Reaction: After engaging: You have Reconnaissance at the current battlefield. Until this battle ends, your Ranged Attacks that do not destroy
        // their target have the additional effects, 'reduce the target's Force by the Ranged Attack's strength and you may take an additional Ranged Attack Battle action that may only target
        // the same card'."
        all["koshin-keep-recon"] = new("Reaction", "Recon Reaction: After engaging: You have Reconnaissance at the current battlefield. Until this battle ends, your Ranged Attacks that do not destroy their target have the additional effects, \"reduce the target's Force by the Ranged Attack's strength and you may take an additional Ranged Attack Battle action that may only target the same card\"", "none",
            Trigger: "after-engaging", ActionKeywords: ["Recon"]);
        // Kyuden Otomo - exp (oracle-l5r-4646): "After you take the Imperial Favor: You may discard it; if you did, add a Favor token to this card." A trait with a trigger: applied through the
        // after-action-resolved window (the player may pass), usable while the Stronghold is bowed (a trait is), as often as the Favor is taken.
        all["otomo-favor-token"] = new("Reaction", "After you take the Imperial Favor: You may discard it; if you did, add a Favor token to this card", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved, UsesPerTurn: AbilityUseLimit.Unlimited);
        // "Political Open: Bow your performing honorable Courtier: Add a Favor token to this card."
        all["otomo-courtier-token"] = new("Open", "Political Open: Bow your performing honorable Courtier: Add a Favor token to this card", "none",
            Performer: "own-unbowed-honorable-personality-with-keyword", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer, ActionKeywords: ["Political"]);
        // Library of Rebirth (oracle-l5r-4724): "Reaction: Even if this card is bowed, when paying for a Spell during the Combat Segment: Produce 4 Gold." An alternate Gold source that does not
        // bow its card (no bow is printed) and is usable while the card is bowed; never an action of its own (an unknown trigger is never enabled, Sakuma's shape).
        all["library-rebirth-gold"] = new("Reaction", "Even if this card is bowed, when paying for a Spell during the Combat Segment: Produce 4 Gold", "none", Trigger: "paying-gold-cost",
            PaymentProduction: 4, PaymentBows: false, PaymentWhen: LibraryOfRebirthPaymentWhen, PaymentEvenIfBowed: true);
        // "Battle: Choose your performing Shugenja: Straighten him. You may take an additional Battle action."
        all["library-rebirth-straighten"] = new("Battle", "Choose your performing Shugenja: Straighten him. You may take an additional Battle action", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Shugenja"]);
        // Midday Shadow Court (oracle-l5r-5037): "Political Open: Even if this card is bowed, target another player's Personality: After the next time this turn he is assigned, or an action that
        // he performed ends, dishonor him and his controller loses 1 Honor." A watch (AssignmentWatches), fired by an assignment to either side or by the end of an action he performed.
        all["midday-shadow-watch"] = new("Open", "Political Open: Even if this card is bowed, target another player's Personality: After the next time this turn he is assigned, or an action that he performed ends, dishonor him and his controller loses 1 Honor", "enemy-personality",
            ActionKeywords: ["Political"]);
        return all;
    }

    /// <summary>Library of Rebirth's payment condition: paying for a Spell (equipped or attached) during a Combat Segment.</summary>
    public const string LibraryOfRebirthPaymentWhen = "spell-in-combat";
}

public sealed partial class MatchEngine
{
    // ---- Effect keys --------------------------------------------------------------------------------------------------------------------------------
    internal const string EternalVictoryDojoEffect = "eternal-victory-dojo";
    internal const string KeepOfTheDeadEffect = "keep-of-the-dead";
    internal const string KyudenOtomoExpEffect = "kyuden-otomo-exp";
    internal const string MiddayShadowCourtEffect = "midday-shadow-court";
    internal const string FavorTokenCostOption = "favor-token";
    internal const string FavorTokenType = "Favor";
    private const string KoshinKeepBattleKey = "koshin-keep:battle";
    public const string WatchMiddayShadow = "midday-shadow";

    private CardInstance StrongholdOf(int playerId) => Card(Player(playerId).StrongholdId);
    private bool StrongholdHasEffect(int playerId, string effect) => Definition(StrongholdOf(playerId)).Effect == effect;

    // "Even if bowed" for the batch's own abilities: Midday Shadow Court's Political Open, and Kyuden Otomo - exp's Favor trait (a trait is not stopped by a bow).
    private static bool StrongholdBatch1EvenIfBowed(string id) => id is "midday-shadow-watch" or "otomo-favor-token";

    // ---- Keep of the Dead ---------------------------------------------------------------------------------------------------------------------------
    // "You ignore Honor Requirements on your Followers": read wherever a Follower's Honor Requirement is checked against the Personality it would attach to.
    private bool IgnoresFollowerHonorRequirement(CardInstance wielder) => StrongholdHasEffect(wielder.OwnerId, KeepOfTheDeadEffect);

    // "If any enemy units are at the current battlefield or he assigned there, move him there. Straighten his unit if he moved." The condition (Charge Into Danger's, read as it resolves); the move
    // itself is in RunAbilityEffects' case (the move-to-battle scan reads that switch).
    private bool KeepOfTheDeadMayMove(PlayerState player, CardInstance commander)
    {
        var battlefield = _state.CurrentBattleProvince!.Value;
        if (!AtBattle(commander) && Army(1 - player.Id, battlefield).Any()) return true;
        Emit("effectFailed", AtBattle(commander) ? $"{Definition(commander).Name} is already at the current battlefield." : $"{Definition(commander).Name} did not move: no enemy units are at the current battlefield.", player.Id, commander.Id);
        return false;
    }

    // ---- Library of Rebirth ---------------------------------------------------------------------------------------------------------------------------
    // The reactive Gold source of a card (Library of Rebirth) for this payment: its ability, when the payment meets the ability's condition and its use is unspent. Unlike Sakuma's
    // alternate source it belongs to a card that also has a printed production, and it may be used while the card is bowed.
    private (string AbilityId, int Amount)? ReactiveGoldSource(CardInstance card, PaymentSubject? paying)
    {
        if (paying is null || card.Zone is not (Zone.Home or Zone.Stronghold or Zone.Battlefield or Zone.Celestial or Zone.Attached)) return null; // a Celestial too: Kenro-ji-jin's Bounty (issue #653); an attached Item: Mantle of the Jade Champion
        foreach (var id in AbilityIdsOf(card))
            if (AbilityCatalog.All.TryGetValue(id, out var ability) && ability.PaymentWhen is { } when && ability.PaymentProduction is int amount
                && !BarredByCherryBlossom(id) /* Cherry Blossom Festival (R4) */ && (ability.PaymentEvenIfBowed || !card.Bowed)
                && ReactivePaymentConditionMet(when, paying)
                && !(card.AbilityUses.TryGetValue(ability.UsageKey ?? id, out var used) && used == _state.TurnNumber)
                && !AbilityDisabled(card, id))
                return (id, amount);
        return null;
    }

    private bool ReactivePaymentConditionMet(string when, PaymentSubject paying) => when switch
    {
        // "Paying for a Spell during the Combat Segment": a Spell equipped or attached (a payment for the card itself, never an action of a Spell already in play).
        EmperorStrongholdBatch1Abilities.LibraryOfRebirthPaymentWhen => _state.Phase == Phase.Battle && paying.Kind == PaymentKind.Equip && Definition(paying.Card).Type == CardType.Spell,
        PromoEmperorCelestialAbilities.PayingForFarmWhen => PromoEmperorPaymentCondition(when, paying), // Kenro-ji-jin's Bounty (issue #653)
        PromoEmperorItemsAbilities.PayingForSpellWhen => paying.Kind == PaymentKind.Equip && Definition(paying.Card).Type == CardType.Spell, // Mantle of the Jade Champion: "when paying for a Spell"
        _ => EmperorRemainingFPaymentConditionMet(when, paying) // Luxurious Silk (issue #492, group F): paying for an action.
    };

    // (Issue #501: the reactive source is a payment token of its own, "<card>#reaction", chosen beside or instead of the card's bow and spent by UsePaymentTokens, EmperorMechE.cs.)

    // ---- Midday Shadow Court ---------------------------------------------------------------------------------------------------------------------------
    // "When a Lobby action checks Family Honor, your Honor is treated as 20 points higher."
    private int LobbyHonorBonus(PlayerState player) => (StrongholdHasEffect(player.Id, MiddayShadowCourtEffect) ? 20 : 0) + PromoItemsLobbyHonorBonus(player); // + The Blessed Mantle of the Greensnakes (PromoEmperorItems.cs)

    // "After the next time this turn he is assigned": either side's assignment.
    private void FireMiddayShadowAssigned(CardInstance card)
    {
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == card.Id && w.Kind == WatchMiddayShadow && w.Turn == _state.TurnNumber).ToArray())
        {
            _state.AssignmentWatches.Remove(watch);
            ResolveAssignmentWatch(watch, card, "was assigned");
        }
    }

    // "... or an action that he performed ends": called where an action is truly done (CompleteAbilityResolution, a deferred Attack's ResolveAttack), with its command.
    private void MiddayShadowActionEnded(GameCommand? command)
    {
        if (command is null || !_state.AssignmentWatches.Any(w => w.Kind == WatchMiddayShadow)) return;
        // The rulebook Lobby is performed by the Personality it bows (its target); every other action by its performer (ActionPerformingPersonality).
        var performer = command.AbilityId == "lobby" && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var lobbying) ? lobbying : ActionPerformingPersonality(command);
        if (performer is not null) MiddayShadowPerformerActionEnded(performer);
    }

    private void MiddayShadowPerformerActionEnded(CardInstance performer)
    {
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == performer.Id && w.Kind == WatchMiddayShadow && w.Turn == _state.TurnNumber).ToArray())
        {
            _state.AssignmentWatches.Remove(watch);
            ResolveAssignmentWatch(watch, performer, "finished an action he performed");
        }
    }

    // ---- Eternal Victory Dojo ---------------------------------------------------------------------------------------------------------------------------
    // "Draw a card after the next End Phase begins": drawn as the End Phase begins (EndTurn), whichever player's it is.
    private void ResolvePendingEndPhaseDraws()
    {
        var due = _state.PendingEndPhaseDraws.ToArray();
        _state.PendingEndPhaseDraws.Clear();
        foreach (var playerId in due)
        {
            if (_state.WinnerId is not null) return;
            Emit("delayedDraw", $"{Player(playerId).Name} draws a card as the End Phase begins (Eternal Victory Dojo).", playerId);
            Draw(Player(playerId));
        }
    }

    // ---- Kyuden Otomo - exp ---------------------------------------------------------------------------------------------------------------------------
    // The one place the Imperial Favor is taken: recorded on the action that took it ("After you take the Imperial Favor").
    private void TakeImperialFavor(int playerId)
    {
        _state.FavorPlayerId = playerId;
        if (!_state.CurrentActionFavorTakers.Contains(playerId)) _state.CurrentActionFavorTakers.Add(playerId);
    }

    // The actions that discard the Imperial Favor as a cost ("as a condition upon further effects"), whichever pass wired them.
    // Issue #501 (EmperorMechE.cs): the Imperial Favor discard is a cost verb (CostVerbs.DiscardFavor) declared on the ability; every hard-coded Favor-discard ability was migrated to it.
    private static bool IsFavorDiscardAbility(string id) => DeclaresFavorDiscard(id);

    // A Favor token the player may discard in place of the Imperial Favor: on their Kyuden Otomo - exp.
    private CardInstance? FavorTokenSource(int playerId) =>
        StrongholdHasEffect(playerId, KyudenOtomoExpEffect) && TokenCount(StrongholdOf(playerId), FavorTokenType) > 0 ? StrongholdOf(playerId) : null;

    private static readonly CostOption FavorTokenOption = new(FavorTokenCostOption, "Discard a Favor token instead", CostVerb.Bow, CostSubject.Stronghold);
    // Issue #1275: how the plain Imperial Favor discard is labelled when a Favor token is offered beside it (view only: its command carries no cost option).
    private static readonly CostOption ImperialFavorOption = new("imperial-favor", "Discard the Imperial Favor", CostVerbs.DiscardFavor, CostSubject.Source);

    // Validation of the Favor discard cost: the Imperial Favor, or (with the cost option) a Favor token.
    private void RequireFavorDiscard(GameCommand command, PlayerState player)
    {
        if (FavorDiscardRejection(command, player) is { } why) throw new RuleViolation(why);
    }

    // RequireFavorDiscard's reason as a value (null when the discard is allowed), for ValidateAbilityResult (issue #890).
    private string? FavorDiscardRejection(GameCommand command, PlayerState player)
    {
        if (command.CostOption == FavorTokenCostOption) return FavorTokenSource(player.Id) is not null ? null : "Your Kyuden Otomo has no Favor token to discard.";
        return _state.FavorPlayerId == player.Id ? null : "You must control the Imperial Favor to discard it.";
    }

    // The cost itself.
    private void DiscardImperialFavorCost(GameCommand command, PlayerState player)
    {
        if (command.CostOption == FavorTokenCostOption && FavorTokenSource(player.Id) is { } keep)
        {
            var token = keep.Tokens.First(t => t.Type == FavorTokenType);
            keep.Tokens.Remove(token);
            Emit("favorTokenDiscarded", $"{player.Name} discarded a Favor token from {Definition(keep).Name}.", player.Id, keep.Id);
            return;
        }
        ReleaseImperialFavor(player.Id); // issue #667 (FavorMempo.cs): the one helper every Favor discard goes through
        Emit("favorDiscarded", $"{player.Name} discarded the Imperial Favor.", player.Id);
    }

    // "After you take the Imperial Favor" (AfterActionResolvedApplies): the Favor is still the Reaction's controller's, and the action that resolved was one that took it for him.
    private bool OtomoFavorTraitApplies(int playerId) =>
        _state.LastResolvedAction is { } facts && facts.FavorTakers.Contains(playerId) && _state.FavorPlayerId == playerId;

    // ---- Koshin Keep ---------------------------------------------------------------------------------------------------------------------------------
    private bool KoshinKeepActive(int playerId) =>
        _state.Phase == Phase.Battle && StrongholdOf(playerId).AbilityUses.TryGetValue(KoshinKeepBattleKey, out var battle) && battle == _state.BattleNumber
        && _state.Cards.Values.Any(c => c.Id == Player(playerId).StrongholdId && Definition(c).Abilities?.Contains("koshin-keep-recon") == true);

    // "Your Ranged Attacks that do not destroy their target have the additional effects": called where an Attack resolves (ResolveAttackNow).
    private void AfterKoshinKeepAttack(string abilityId, int playerId, string? targetId, int strength, bool targetInPlayBefore)
    {
        if (targetId is null || strength <= 0 || !targetInPlayBefore || !KoshinKeepActive(playerId) || AttackKind(abilityId) != "Ranged") return;
        var target = Card(targetId);
        if (!InPlay(target)) return; // destroyed: not an Attack that "does not destroy its target"
        var keep = StrongholdOf(playerId);
        if (Definition(target).Type is CardType.Personality or CardType.Follower)
        {
            Emit("koshinKeep", $"{Definition(keep).Name}: the Ranged Attack did not destroy {Definition(target).Name}; his Force is reduced by {strength}.", playerId, target.Id);
            ApplyModifier(target, keep.Id, "koshin-keep-recon", "Force", -strength);
        }
        GrantAdditionalAction(playerId, null, rangedTargetId: target.Id);
    }

    // The target an Attack command names: the ordinary TargetId of an Attack ability, or -- Kahoku - exp's second selection, whose TargetId is another target -- the first CardIds entry.
    private static string? AttackCommandTarget(GameCommand command) =>
        command.AbilityId is { } id && AttackKind(id) != "" ? TargetsThroughAttackPipeline(id) ? command.TargetId : command.CardIds is { Length: > 0 } ids ? ids[0] : null : null;

    private bool RangedTargetGrantMatches(AdditionalActionGrant grant, GameCommand? command) =>
        grant.RangedTargetId is null || command?.AbilityId is { } id && AttackKind(id) == "Ranged" && AttackCommandTarget(command) == grant.RangedTargetId;

    // A grant that limits WHICH action may spend it: Koshin Keep's "additional Ranged Attack that may only target the same card" and Honor Never Falls' "additional Battle action to use one of his base abilities" (issue #496).
    private static bool RestrictedGrant(AdditionalActionGrant grant) => grant.RangedTargetId is not null || grant.BaseAbilityOnly
        || grant.RequiredActionKeyword is not null || grant.Tag is not null || grant.ScopeAbilityIds is not null; // issue #721: a grant scoped to named abilities; issue #501 (EmperorMechA.cs): Inokuchi's Iaijutsu, Beyond the Line's Recon ability

    private static string RestrictedGrantMessage(AdditionalActionGrant[] held) => held.Any(g => g.RangedTargetId is not null)
        ? "Koshin Keep: the additional action must be a Ranged Attack that targets the same card."
        : held.Any(g => g.Tag == EmperorMechAAbilities.BeyondTheLineTag) ? "Beyond the Line: the additional action must use a Limited or Open Recon ability as a Battle action that targets the current battlefield's province."
        : held.Any(g => g.ScopeAbilityIds is not null) ? ScopeAbilityMessage(held.First(g => g.ScopeAbilityIds is not null))
        : held.Any(g => g.RequiredActionKeyword is not null) ? $"The additional action must be {held.First(g => g.RequiredActionKeyword is not null).RequiredActionKeyword}{(held.Any(g => g.PerformerId is not null) ? " and performed by the named Personality" : "")}."
        : "Honor Never Falls: the additional action must use one of the Personality's base abilities.";

    // The commands that are not abilities (Equip, a Terrain played from a hand, a Reserve recruit): none is a Ranged Attack or a Personality's base ability, so none may be taken while only a restricted grant is held.
    private void RequireNoRestrictedBurst(int playerId)
    {
        var held = _state.AdditionalActions.Where(g => g.PlayerId == playerId && Forfeitable(g)).ToArray();
        Require(held.Length == 0 || held.Any(g => !RestrictedGrant(g)), RestrictedGrantMessage(held));
    }

    // While the only additional actions a player holds are restricted ones, the action he takes must be one they allow (or a Reaction, which never uses up the burst).
    private void RequireRestrictedBurstAllows(AbilityDefinition ability, GameCommand command)
    {
        var held = _state.AdditionalActions.Where(g => g.PlayerId == command.PlayerId && Forfeitable(g)).ToArray();
        if (held.Length == 0 || held.Any(g => !RestrictedGrant(g)) || !ForfeitsAdditionalActions(ability)) return;
        Require(held.Any(g => RangedTargetGrantMatches(g, command) && PerformerGrantMatches(g, command)), RestrictedGrantMessage(held));
    }

    // ---- Hidden Falls Dojo ----------------------------------------------------------------------------------------------------------------------------
    private bool HiddenFallsActive(int playerId) => _state.HiddenFallsGrants.Any(g => g.PlayerId == playerId && g.Turn == _state.TurnNumber);
    private int[] HiddenFallsProvinces(int playerId) =>
        _state.HiddenFallsGrants.Where(g => g.PlayerId == playerId && g.Turn == _state.TurnNumber).SelectMany(g => g.Provinces).Distinct().Order().ToArray();

    // The provinces a Recon Open can meaningfully name: the Defender's (a battlefield sits at each of them).
    private IEnumerable<int> HiddenFallsCandidateProvinces() =>
        Player(1 - _state.ActivePlayerId).Provinces.Where(p => !p.Destroyed).Select(p => p.Index).Order();

    private void ValidateHiddenFallsTargets(GameCommand command)
    {
        Require(command.TargetId is null && (command.CardIds is null || command.CardIds.Length == 0), "This action targets provinces, not cards.");
        var chosen = command.Provinces ?? (command.Province is int single ? [single] : []);
        Require(chosen.Length is 1 or 2 && chosen.Distinct().Count() == chosen.Length, "Choose one or two different provinces.");
        var legal = HiddenFallsCandidateProvinces().ToHashSet();
        Require(chosen.All(legal.Contains), "Choose provinces of the Defender that have not been destroyed.");
    }

    // The effect: Reconnaissance at their battlefields (held until the battle there resolves or the turn ends) and the additional Infantry Maneuvers Segment.
    private void HiddenFallsRecon(PlayerState player, GameCommand command, CardInstance source)
    {
        var chosen = (command.Provinces ?? (command.Province is int single ? [single] : [])).Distinct().Order().ToArray();
        _state.HiddenFallsGrants.RemoveAll(g => g.Turn != _state.TurnNumber);
        _state.HiddenFallsGrants.Add(new(player.Id, chosen, _state.TurnNumber));
        foreach (var province in chosen) GrantReconnaissance(player.Id, 1 - _state.ActivePlayerId, province);
        Emit("hiddenFalls", $"{Definition(source).Name}: {player.Name} has Reconnaissance at the battlefield{(chosen.Length == 1 ? "" : "s")} of province{(chosen.Length == 1 ? "" : "s")} {string.Join(" and ", chosen.Select(p => p + 1))}, and an additional Infantry Maneuvers Segment follows each Cavalry Maneuvers Segment for their Scouts.", player.Id, source.Id);
    }

    // Reconnaissance lasts only while a battlefield can exist: cleared at turn's end (a battle's own resolution already clears its province's).
    private void ClearReconnaissanceAtTurnEnd()
    {
        foreach (var owner in _state.Players)
            foreach (var province in owner.Provinces) province.Reconnaissance = new bool[2];
        R1.AdditionalReconnaissance.Clear(); // additional battlefields' too (issue #492, rulings pass R1)
        _state.HiddenFallsGrants.Clear();
        _state.ExtraInfantrySegment = false;
    }

    // Whether a player can do anything in his step of the additional segment: he targeted provinces, and has a Scout at home who could be assigned to one of them. A step nobody could act in is
    // skipped rather than prompted (the same outcome: nobody is assigned).
    private bool HiddenFallsStepPlayable(int playerId, bool attack)
    {
        var (units, provinces) = HiddenFallsAssignmentChoices(playerId, attack);
        return HiddenFallsActive(playerId) && units.Length > 0 && provinces.Length > 0;
    }

    // After a Cavalry Maneuvers Segment: whether an additional Infantry Maneuvers Segment follows (a player who targeted provinces, with a Scout who could be assigned there).
    private bool BeginHiddenFallsSegment()
    {
        var attacker = _state.ActivePlayerId;
        if (_state.ExtraInfantrySegment || !HiddenFallsStepPlayable(attacker, true) && !HiddenFallsStepPlayable(1 - attacker, false)) return false;
        _state.ExtraInfantrySegment = true;
        Emit("extraManeuvers", "An additional Infantry Maneuvers Segment begins (Hidden Falls Dojo): only the Scouts of the player who targeted the provinces may be assigned, only to those battlefields.");
        AdvanceExtraInfantry(attackerStep: true);
        return true;
    }

    // Steps through the additional segment: the Attacker's step, then the Defender's; a step nobody may act in is skipped. Ends by choosing the next battle (or the Attack Phase's end).
    private void AdvanceExtraInfantry(bool attackerStep)
    {
        var attacker = _state.ActivePlayerId; var defender = 1 - attacker;
        if (attackerStep)
        {
            if (HiddenFallsStepPlayable(attacker, true)) { SetPhase(Phase.InfantryDeclaration, attacker); return; }
            attackerStep = false;
        }
        if (!attackerStep && HiddenFallsStepPlayable(defender, false)) { SetPhase(Phase.DefenseAssignment, defender); return; }
        _state.ExtraInfantrySegment = false;
        SetPhaseAfterManeuvers(attacker); // Warded Paths (Emperor Edition remaining-cards pass, issue #492, group E): the battle order may be the Defender's.
    }

    // Assign's rule in the additional segment: the assigning player's Scouts, to the provinces he targeted.
    private void RequireHiddenFallsAssignment(CardInstance card, int province)
    {
        Require(HiddenFallsActive(card.OwnerId), "Only the player who used Hidden Falls Dojo may assign in the additional Infantry Maneuvers Segment.");
        Require(Keyword(card, "Scout"), $"{Definition(card).Name}: only Scout Personalities may be assigned in the additional Infantry Maneuvers Segment.");
        Require(HiddenFallsProvinces(card.OwnerId).Contains(province), "Only to the targeted provinces' battlefields.");
    }

    // The QueryActions listing of the additional segment: the acting player's Scouts, the provinces he targeted (still to be fought).
    private (string[] Units, int[] Provinces) HiddenFallsAssignmentChoices(int playerId, bool attack)
    {
        var provinces = HiddenFallsProvinces(playerId).Where(p => attack ? Player(1 - _state.ActivePlayerId).Provinces.Any(q => q.Index == p && !q.Destroyed) : _state.UnresolvedBattlefields.Contains(p)).ToArray();
        var units = _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Home && (!c.Bowed || MayAssignBowed(c)) && Definition(c).Type == CardType.Personality
            && !IsCavalryUnit(c) && Keyword(c, "Scout")).Select(c => c.Id).ToArray();
        return (units, provinces);
    }

    // The offered options for Hidden Falls Dojo's Recon Open: one for each set of one or two of the Defender's provinces.
    private IEnumerable<ActionOption> HiddenFallsOptions(string id, AbilityDefinition ability, CardInstance? source, int playerId)
    {
        var candidates = HiddenFallsCandidateProvinces().ToArray();
        var sets = candidates.Select(p => new[] { p }).Concat(candidates.SelectMany((a, i) => candidates.Skip(i + 1).Select(b => new[] { a, b }))).ToArray();
        if (sets.Length == 0) sets = [[]];
        foreach (var set in sets)
        {
            var command = new GameCommand("useAbility", playerId, _state.Version, source?.Id, AbilityId: id, Provinces: set.Length == 0 ? null : set, Province: set.Length == 0 ? null : set[0]);
            string? error = null;
            error = TryValidateAbility(command);
            var label = ability.Label + (set.Length == 0 ? "" : $" · province{(set.Length == 1 ? "" : "s")} {string.Join(" and ", set.Select(p => p + 1))}");
            yield return new("useAbility", label, error is null, error, error is null ? command : null, source?.Id, null, set.Length == 0 ? null : set);
        }
    }

    // ---- validation / execution hooks --------------------------------------------------------------------------------------------------------------------
    private void ValidateEmperorStrongholdBatch1(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (IsAction(ability)) RequireRestrictedBurstAllows(ability, command);
        switch (id)
        {
            // "You may discard it": the Imperial Favor must be the Reaction's controller's.
            case "otomo-favor-token":
                Require(_state.FavorPlayerId == player.Id, "You do not control the Imperial Favor.");
                break;
        }
    }

    // null: not one of this batch's abilities. false: its effects are done, the action finishes as usual.
    private bool? RunEmperorStrongholdBatch1(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case "eternal-victory-draw":
                _state.PendingEndPhaseDraws.Add(player.Id);
                Emit("delayedDrawQueued", $"{Definition(source!).Name}: {player.Name} will draw a card after the next End Phase begins.", player.Id, source!.Id);
                return false;
            case "hidden-falls-recon":
                HiddenFallsRecon(player, command, source!);
                return false;
            case "koshin-keep-recon":
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                source!.AbilityUses[KoshinKeepBattleKey] = _state.BattleNumber;
                Emit("koshinKeep", $"{Definition(source).Name}: until this battle ends, {player.Name}'s Ranged Attacks that do not destroy their target reduce its Force by their strength and allow an additional Ranged Attack on the same card.", player.Id, source.Id);
                return false;
            case "otomo-favor-token":
                ReleaseImperialFavor(player.Id); // issue #667 (FavorMempo.cs)
                Emit("favorDiscarded", $"{player.Name} discarded the Imperial Favor.", player.Id);
                CreateToken(source!, FavorTokenType, id);
                return false;
            case "otomo-courtier-token":
                CreateToken(source!, FavorTokenType, id);
                return false;
            case "library-rebirth-straighten":
                Straighten(Card(command.PerformerId!));
                GrantAdditionalAction(player.Id, null);
                return false;
            case "midday-shadow-watch":
                _state.AssignmentWatches.Add(new(target!.Id, WatchMiddayShadow, player.Id, _state.TurnNumber, Definition(source!).Name));
                Emit("watchSet", $"{Definition(source!).Name}: after the next time this turn {Definition(target).Name} is assigned, or an action he performed ends, he is dishonored and his controller loses 1 Honor.", player.Id, target.Id);
                return false;
            default:
                return null;
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorStrongholdBatch1Hooks()
    {
        EngineHooks.Abilities.Register("AddEmperorStrongholdBatch1", 200, static abilities => abilities.AddEmperorStrongholdBatch1());
        EngineHooks.ValidateEarly.Register("ValidateEmperorStrongholdBatch1", 400, static (engine, context) => engine.ValidateEmperorStrongholdBatch1(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorStrongholdBatch1", 400, static (engine, context) => engine.RunEmperorStrongholdBatch1(context.id, context.ability, context.command, context.player, context.source, context.target) is not null ? false : null);
    }
}
