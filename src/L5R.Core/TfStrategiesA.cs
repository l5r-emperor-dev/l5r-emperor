namespace L5R.Core;

// ======================================================================================================================================================================================
// STRATEGY WAVE 109 (issue #622): ten Strategies carrying a Twenty Festivals label whose earliest Emperor-era printing is wired from its OWN scan (Aftermath, Coils of Madness, Gates of Chaos,
// Emperor Edition), with the PRINTED wording rather than Oracle's later template.
//   * AWED WITNESS (657): "Iaijutsu Battle: Choose your performing unbowed Personality and target an enemy Personality: Your Personality duels the target. Bow the duel's loser. The duel's
//     winner's controller may create a 2F/2C/2PH Samurai Personality with that player's Clan alignment on his side of the current battlefield." Reckless Duel's challenge (not refusable: the scan
//     says "duels"), the loser bowed, and -- before the outcome applies, like the delay decisions -- the winner's controller is asked whether to create Witness (oracle-l5r-10018, Emperor Edition's
//     Proxy scan, 2F/2C/2PH Samurai, with its creator's Clan alignment): the answer is an Either/Or (the generic presentation) and the duel then resolves again with it. A tie has no winner, so no question.
//   * A WARRIOR'S BRUTALITY (10927): "Battle: Give your target opposed Personality +3F." is exactly Crushing Strength's action (crushing-force): reused unchanged.
//   * ABSOLUTION (10928): "Interrupt: Negate an Event's effects." plays like a Reaction before the Event resolves (Emperor Edition rules: an Interrupt is a Reaction to "before an action's effects
//     resolve"), Forging Destiny's window ("p3BeforeEventReaction", EmperorP3Windows.cs). Kharmic is the engine's own keyword action (kharmic-discard).
//   * BREAKING THE RHYTHM (10931): "As a Focus Effect, if you win this duel, gain 1 Honor and give the winner +1F." and "Absent Battle: Negate a target Personality's current Force bonuses or
//     penalties (you choose)." The choice is made by the action played (one ability for each, as Creating Order's two halves), and negates what is on the Personality when it resolves: his Force
//     modifiers and tokens (a later bonus or penalty is not "current" and is not touched).
//   * DEATH OF THE WINDS (10544) / DIRECTING THE BATTLE (10935): deck searches for a non-Unique Battle Strategy. Directing the Battle's "if there are any enemy units" is its entry condition
//     (Your Clan Needs You!'s shape); Death of the Winds also needs "your unbowed Tactician" as its target and grants an additional action scoped to the card it found ("to play it").
//   * FEIGN DEATH (2503): printed as a Reaction "After the resolution of an action or a battle that destroyed a performing Personality you own": two Reactions (an action's and a battle's
//     resolution) sharing one use; its target is the destroyed Personality, who must be one that very resolution destroyed (Mendon's R-022 reading of the trigger).
//   * GRASP THE SWORDS (10940): +2F/+2C to your target Personality with a Weapon, and an additional action if he is a Kensai.
//   * HIDDEN STOREHOUSE OF HOUSE RAFIQ (10732): "If you (after playing this Strategy) have fewer cards in your hand than each other player, draw two cards."
//   * HOLD THE WALLS (10941): a Terrain; "Attacking Personalities and Followers have -1F" at its battlefield (TerrainForceAdjustment) and its Battle play replaces a Terrain already there
//     (Looting the Ruins' shape).
//   * IVORY MAGISTRATE OUTPOST (10734): Open: straighten your target Magistrate; Battle: straighten your target Personality.
// ======================================================================================================================================================================================

public static class TfStrategiesAAbilities
{
    public const string AbsolutionNegate = "absolution-negate";
    public const string BreakingFocus = "breaking-rhythm-focus", BreakingBonuses = "breaking-rhythm-bonuses", BreakingPenalties = "breaking-rhythm-penalties";
    public const string WindsSearch = "death-winds-search", DirectingSearch = "directing-battle-search";
    public const string FeignAction = "feign-death-action", FeignBattle = "feign-death-battle";
    public const string GraspBoost = "grasp-swords-boost";
    public const string StorehouseDraw = "hidden-storehouse-draw";
    public const string OutpostOpen = "ivory-outpost-open", OutpostBattle = "ivory-outpost-battle";
    public const string WitnessDuel = "awed-witness-duel";
    /// <summary>The Either/Or the duel's winner's controller answers: create Witness, or not.</summary>
    public const string WitnessSpec = "awed-witness-create";
    /// <summary>Hold the Walls' Effect: a Terrain read by TerrainForceAdjustment and by the Terrain replacement of PlayStrategy.</summary>
    public const string HoldTheWallsEffect = "hold-the-walls";
    public const string OwnPersonalityWithWeapon = "tfa-own-personality-with-weapon";
    /// <summary>The id of the additional action Death of the Winds grants (scoped to the card it found); an id, not a Tag: a tagged grant restricts which action may spend it.</summary>
    public const string WindsGrantTag = "death-of-the-winds";

    public static SearchSpec WindsSearchSpec { get; } = new("death-of-the-winds", "Search your Fate deck for a non-Unique Battle Strategy",
        [new(SearchDeck.Fate, new("non-Unique Battle Strategy", [CardType.Strategy], NonUnique: true, BattleStrategy: true), SearchEffect.ToHand)]);
    public static SearchSpec DirectingSearchSpec { get; } = new("directing-the-battle", "Search your Fate deck for a non-unique Battle Strategy",
        [new(SearchDeck.Fate, new("non-unique Battle Strategy", [CardType.Strategy], NonUnique: true, BattleStrategy: true), SearchEffect.ToHand)]);

    public static Dictionary<string, AbilityDefinition> AddTfStrategiesA(this Dictionary<string, AbilityDefinition> all)
    {
        all[AbsolutionNegate] = new("Reaction", "Interrupt: Before an Event resolves: Negate its effects", "none", Trigger: EmperorP3Abilities.BeforeEventTrigger);
        all[BreakingFocus] = new("FocusEffect", "As a Focus Effect: If you win this duel, gain 1 Honor and give the winner +1F", "none");
        all[BreakingBonuses] = new("Battle", "Absent Battle: Negate a target Personality's current Force bonuses", "personality", Absent: true);
        all[BreakingPenalties] = new("Battle", "Absent Battle: Negate a target Personality's current Force penalties", "personality", Absent: true);
        all[WindsSearch] = new("Battle", "Target your unbowed Tactician: Search your Fate deck for a non-Unique Battle Strategy and take an additional action to play it", "own-unbowed-card-with-keyword",
            RequiredKeywords: ["Tactician"], Search: WindsSearchSpec);
        all[DirectingSearch] = new("Battle", "If there are any enemy units at this battlefield, search your Fate deck for a non-unique Battle Strategy. Show it and put it in your hand", "none", Search: DirectingSearchSpec);
        all[FeignAction] = new("Reaction", "After the resolution of an action that destroyed a performing Personality you own: Bring him into play (in your home), ignoring costs and Honor Requirement. Bow him. Dishonor him. Lose 2 Honor",
            "own-dead-personality", Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "feign-death");
        all[FeignBattle] = new("Reaction", "After the resolution of a battle that destroyed a performing Personality you own: Bring him into play (in your home), ignoring costs and Honor Requirement. Bow him. Dishonor him. Lose 2 Honor",
            "own-dead-personality", Trigger: MatchEngine.AfterResolutionTrigger, UsageKey: "feign-death");
        all[GraspBoost] = new("Battle", "Give your target Personality with a Weapon +2F/+2C. If he is a Kensai, you may take an additional action", OwnPersonalityWithWeapon);
        all[StorehouseDraw] = new("Battle", "If you (after playing this Strategy) have fewer cards in your hand than each other player, draw two cards", "none");
        all[OutpostOpen] = new("Open", "Straighten your target Magistrate", "own-personality-with-keyword", RequiredKeywords: ["Magistrate"]);
        all[OutpostBattle] = new("Battle", "Straighten your target Personality", "own-personality");
        all[WitnessDuel] = new("Battle", "Choose your performing unbowed Personality and target an enemy Personality: Your Personality duels the target. Bow the duel's loser. The duel's winner's controller may create a 2F/2C/2PH Samurai Personality with that player's Clan alignment on his side of the current battlefield",
            "enemy-personality", Performer: "own-unbowed-personality-at-battle", IssuesDuel: true,
            Creates: new CreatedCardSpec(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 2, Keywords: ["Samurai"], ProxyId: "oracle-l5r-10018", AtBattlefield: true) { ControllerClan = true });
        return all;
    }
}

/// <summary>Awed Witness's decision in progress: the duel it was asked for, and the winner's controller's answer.</summary>
public sealed class TfStrategiesAState
{
    public string? WitnessAskedDuelId { get; set; }
    public bool WitnessCreate { get; set; }
}

public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1340)]
    public TfStrategiesAState TfStrategiesA { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // ---- Targets ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // TargetAllows' hook: Grasp the Swords' "your target Personality with a Weapon" (location is the ordinary Battle rule, checked before this).
    private bool TfStrategiesATargetAllows(AbilityDefinition ability, PlayerState player, CardInstance target) =>
        ability.Target == TfStrategiesAAbilities.OwnPersonalityWithWeapon && Definition(target).Type == CardType.Personality && target.OwnerId == player.Id
            && Attachments(target.Id).Any(a => Keyword(a, "Weapon"));

    // ---- Validation ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ValidateLate's hook. Directing the Battle's printed entry condition; Feign Death's target is a Personality the very resolution that opened the window destroyed.
    private void ValidateTfStrategiesA(string id, GameCommand command, PlayerState player)
    {
        switch (id)
        {
            case TfStrategiesAAbilities.DirectingSearch:
                Require(_state.CurrentBattleProvince is int battle && Army(1 - player.Id, battle).Any(), "Directing the Battle requires enemy units at this battlefield.");
                break;
            case TfStrategiesAAbilities.FeignAction:
                Require(command.TargetId is { } actionVictim && _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == DestroyedKind && a.CardId == actionVictim),
                    "Choose a Personality of yours that action destroyed.");
                break;
            case TfStrategiesAAbilities.FeignBattle:
                Require(command.TargetId is { } battleVictim && _state.PendingAfterResolution is { } resolution && resolution.DestroyedCardIds.Contains(battleVictim),
                    "Choose a Personality of yours that battle's resolution destroyed.");
                break;
        }
    }

    // ---- Effects ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunTfStrategiesA(RunHookContext context)
    {
        var (id, command, player, source, target) = (context.id, context.command, context.player, context.source, context.target);
        switch (id)
        {
            // Absolution: Forging Destiny's negation of the Event whose window is open.
            case TfStrategiesAAbilities.AbsolutionNegate:
                if (P3.EventWindowCardId is { } eventId)
                {
                    P3.EventNegatedCardId = eventId;
                    Emit("eventNegated", $"{Definition(source!).Name}: the effects of {Definition(Card(eventId)).Name} are negated.", player.Id, eventId);
                }
                return false;
            // Breaking the Rhythm's Absent Battle: the Force bonuses, or the Force penalties, on him now.
            case TfStrategiesAAbilities.BreakingBonuses:
            case TfStrategiesAAbilities.BreakingPenalties:
                NegateCurrentForce(target!, id == TfStrategiesAAbilities.BreakingBonuses);
                return false;
            case TfStrategiesAAbilities.WindsSearch:
            case TfStrategiesAAbilities.DirectingSearch:
                SpendHandPlayedCard(player, source);
                BeginSearch(context.ability.Search!, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case TfStrategiesAAbilities.FeignAction:
            case TfStrategiesAAbilities.FeignBattle:
                {
                    var name = Definition(source!).Name;
                    SpendHandPlayedCard(player, source);
                    return BringDeadPersonalityIntoPlay(player, target!, name, null, entered =>
                    {
                        if (!StalwartNegatesBow(entered, player.Id)) entered.Bowed = true;
                        Dishonor(entered, player.Id);
                        GainHonor(player.Id, -2, name, source);
                    });
                }
            case TfStrategiesAAbilities.GraspBoost:
                ApplyModifier(target!, source!.Id, id, "Force", 2);
                ApplyModifier(target!, source.Id, id, "Chi", 2);
                if (Keyword(target!, "Kensai")) GrantAdditionalAction(player.Id, null);
                return false;
            case TfStrategiesAAbilities.StorehouseDraw:
                {
                    // "After playing this Strategy": the card itself is no longer in the hand it was played from.
                    var mine = player.Hand.Count(h => h != source?.Id);
                    if (_state.Players.Where(p => p.Id != player.Id).All(p => p.Hand.Count > mine)) { Draw(player); Draw(player); }
                    else Emit("storehouseNothing", $"{Definition(source!).Name}: {player.Name} does not have fewer cards in hand than each other player.", player.Id, source!.Id);
                    return false;
                }
            case TfStrategiesAAbilities.OutpostOpen:
            case TfStrategiesAAbilities.OutpostBattle:
                Straighten(target!);
                return false;
            default:
                return null;
        }
    }

    // "Negate a target Personality's current Force bonuses or penalties": every Force modifier and token of that sign on him now, and the Force half of a two-stat token (-1F/-1C Poison keeps its
    // Chi). What is applied to him later is not current, and is not touched. The Force of the cards attached to him is theirs, not an effect on him.
    private void NegateCurrentForce(CardInstance card, bool bonuses)
    {
        bool Signed(int amount) => bonuses ? amount > 0 : amount < 0;
        var modifiers = card.Modifiers.RemoveAll(m => m.Stat == "Force" && Signed(m.Amount));
        var twoStat = card.Tokens.Where(t => t.Stat == "Force" && Signed(t.Amount) && t.Stat2 is not null).ToArray();
        foreach (var token in twoStat) card.Tokens[card.Tokens.IndexOf(token)] = token with { Stat = token.Stat2, Amount = token.Amount2, Stat2 = null, Amount2 = 0 };
        var tokens = card.Tokens.RemoveAll(t => t.Stat == "Force" && Signed(t.Amount)) + twoStat.Length;
        Emit("forceNegated", $"{Definition(card).Name}: {modifiers + tokens} current Force {(bonuses ? "bonus" : "penalty")}{(modifiers + tokens == 1 ? "" : bonuses ? "es" : "ies")} negated.", card.OwnerId, card.Id);
    }

    // ---- Focus Effect ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // ApplyDuelFocusEffectCore's hook. "If you win this duel": the Focus Effect's owner's Personality won; the winner is then his own Personality, who gets +1F.
    private void ApplyTfStrategiesAFocusEffect(string abilityId, int ownerId, int? winnerPlayerId)
    {
        if (abilityId != TfStrategiesAAbilities.BreakingFocus || winnerPlayerId != ownerId || _state.PendingDuel?.WinnerId is not { } winnerId) return;
        GainHonor(ownerId, 1, "Breaking the Rhythm");
        if (InPlay(Card(winnerId))) ApplyModifier(Card(winnerId), winnerId, abilityId, "Force", 1);
    }

    // ---- Death of the Winds: "take an additional action to play it" ---------------------------------------------------------------------------------------------------------------

    // FinishSearch's hook: the card Death of the Winds found is in his hand, and the player may take one additional action to play it (a grant scoped to that card).
    private void TfStrategiesAFinishSearch(PendingSearch search)
    {
        if (search.SearchId != TfStrategiesAAbilities.WindsSearchSpec.Id || search.SelectedCardId is not { } foundId || !_state.Cards.TryGetValue(foundId, out var found) || found.Zone != Zone.Hand) return;
        GrantAdditionalAction(search.PlayerId, [foundId], id: TfStrategiesAAbilities.WindsGrantTag);
    }

    // FinishDeferredAction's hook: the player who was just granted that action keeps priority to take it.
    private bool TfStrategiesAKeepsPriority(int playerId, string sourceCardId) =>
        _state.Cards.TryGetValue(sourceCardId, out var source) && (Definition(source).Abilities ?? []).Contains(TfStrategiesAAbilities.WindsSearch)
            && _state.AdditionalActions.Any(g => g.PlayerId == playerId && g.Id == TfStrategiesAAbilities.WindsGrantTag);

    // ---- Hold the Walls: "Attacking Personalities and Followers have -1F" ------------------------------------------------------------------------------------------------------------

    // TerrainForceAdjustment's hook: a Personality, or a Follower attached to one, standing at a battlefield where Hold the Walls is in play, in the Attacker's army.
    private int HoldTheWallsForceAdjustment(CardInstance card, int battlefield)
    {
        var walls = TerrainsAtBattlefield(battlefield).Count(t => Definition(t).Effect == TfStrategiesAAbilities.HoldTheWallsEffect);
        if (walls == 0) return 0;
        var type = Definition(card).Type;
        if (type != CardType.Personality && !(type == CardType.Follower && card.Zone == Zone.Attached)) return 0;
        var unit = card.Zone == Zone.Attached ? Card(card.AttachedTo) : card;
        return unit.OwnerId == _state.ActivePlayerId ? -walls : 0;
    }

    // ---- Awed Witness ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // ResolveDuel's hook, after the duel's delay decisions and before its outcome applies: the winner's controller is asked once whether to create Witness. True when the question opened (the
    // duel resolves again from the top once it is answered, as after a delay decision).
    private bool TfBeginWitnessDecision(PendingDuel duel, string? winnerId)
    {
        if (duel.AbilityId != TfStrategiesAAbilities.WitnessDuel || winnerId is null || duel.SourceCardId is not { } sourceId || _state.TfStrategiesA.WitnessAskedDuelId == duel.DuelId) return false;
        _state.TfStrategiesA.WitnessAskedDuelId = duel.DuelId; _state.TfStrategiesA.WitnessCreate = false;
        var winner = Card(winnerId);
        return OpenEitherOr(TfStrategiesAAbilities.WitnessSpec, Card(sourceId), duel.ChallengerPlayerId, winner.OwnerId, winner, "duel");
    }

    private string? TfEitherOrPrompt(PendingEitherOr p, string source) =>
        p.SpecId == TfStrategiesAAbilities.WitnessSpec ? $"{source}: {Player(p.DeciderPlayerId).Name}, your Personality won the duel. Create a 2F/2C/2PH Samurai Personality with your Clan alignment on your side of the battlefield?" : null;

    private EitherOrOption[]? TfEitherOrOptions(PendingEitherOr p) => p.SpecId != TfStrategiesAAbilities.WitnessSpec ? null :
        [new("create", "Create Witness", "A 2F/2C/2PH Samurai Personality with your Clan alignment enters play on your side of the current battlefield.", EitherOrKind.Accept, 0, _state.CurrentBattleProvince is not null, "There is no current battlefield."),
         new("decline", "Do not create him", "Nothing is created.", EitherOrKind.Choose, 0, true, null)];

    private bool TfApplyEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != TfStrategiesAAbilities.WitnessSpec) return false;
        _state.TfStrategiesA.WitnessCreate = option.Key == "create";
        Emit("eitherOrChosen", option.Key == "create" ? $"{decider.Name} will create Witness ({Definition(source).Name})." : $"{decider.Name} will not create Witness ({Definition(source).Name}).", decider.Id, source.Id);
        return true;
    }

    // The answer is in: the duel resolves again, and this time applies its outcome.
    private bool TfFinishEitherOr(PendingEitherOr p)
    {
        if (p.SpecId != TfStrategiesAAbilities.WitnessSpec) return false;
        _state.PendingChoice = null; _state.ConsecutivePasses = 0;
        ResolveDuel();
        return true;
    }

    // ApplyDuelOutcome's case: "Bow the duel's loser." and, if the winner's controller chose to, Witness enters play for him at the current battlefield.
    private void TfWitnessDuelOutcome(string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        foreach (var id in loserIds) if (InPlay(Card(id)) && !StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
        var create = _state.TfStrategiesA.WitnessCreate;
        _state.TfStrategiesA.WitnessCreate = false; _state.TfStrategiesA.WitnessAskedDuelId = null;
        if (!create || winnerId is null || _state.CurrentBattleProvince is not int battle) return;
        var winnerPlayer = Player(Card(winnerId).OwnerId);
        var spec = AbilityCatalog.All[TfStrategiesAAbilities.WitnessDuel].Creates!;
        var scan = CreatedCards.Resolve(spec);
        var printed = DerivePrinted(scan, spec, new GameCommand("duel", challengerPlayerId, 0), winnerPlayer);
        // Made inside the duel: no entering-play window over the pending duel (Sword for Hire's assassin is the same).
        CreatePersonality(winnerPlayer.Id, scan, true, battle, out _, printed);
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTfStrategiesAHooks()
    {
        EngineHooks.Abilities.Register("AddTfStrategiesA", 11700, static abilities => abilities.AddTfStrategiesA());
        EngineHooks.Searches.Register("TfStrategiesASearches", 11700, static () => [TfStrategiesAAbilities.WindsSearchSpec, TfStrategiesAAbilities.DirectingSearchSpec]);
        EngineHooks.TargetAllows.Register("TfStrategiesATargetAllows", 11700, static (engine, context) => engine.TfStrategiesATargetAllows(context.ability, context.player, context.target));
        EngineHooks.ValidateLate.Register("ValidateTfStrategiesA", 11700, static (engine, context) => engine.ValidateTfStrategiesA(context.id, context.command, context.player));
        EngineHooks.RunEffects.Register("RunTfStrategiesA", 11700, static (engine, context) => engine.RunTfStrategiesA(context));
        EngineHooks.EitherOrPrompt.Register("TfStrategiesAEitherOrPrompt", 11700, static (engine, p, source, affected) => engine.TfEitherOrPrompt(p, source));
        EngineHooks.EitherOrOptions.Register("TfStrategiesAEitherOrOptions", 11700, static (engine, p, affected, source, payable) => engine.TfEitherOrOptions(p));
        EngineHooks.ApplyEitherOr.Register("TfStrategiesAApplyEitherOr", 11700, static (engine, p, option, source, decider, affected, sourceIds) => engine.TfApplyEitherOr(p, option, source, decider));
        EngineHooks.FinishEitherOr.Register("TfStrategiesAFinishEitherOr", 11700, static (engine, p) => engine.TfFinishEitherOr(p));
    }
}
