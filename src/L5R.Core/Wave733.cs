using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #733, wave batch 2: the recruit-gate, Proclaim and Honor cards, wired on the merged P3 (recruit gates), P14 (Honor-change channel), P18 (duel rules), P10 (keyword rules), P2 (cost terms) and P13 (rule flags)
// primitives. Every legality, price and Honor change is decided in Core. Ability ids are "w733-". Readings for Mendon to confirm or correct are in docs/decisions/2026-10-08-wave-batch-2-recruit-gates-and-honor.md.
// Waves personality-wave-151, follower-wave-63, strategy-wave-135 and stronghold-wave-47 (tools/content/package_wave_733.py).
//
//   Asp Skirmisher           "Asp Skirmisher will not join a player who controls a Human Personality."                    the P3 Effect will-not-join-human-controller
//   Bayushi Hamada           "Crab Clan players may control Hamada." / "+1F for each of your Personalities who is Crab Clan or a Courtier."   P3 Effect crab-players-may-control; EngineHooks.PersonalityForce
//   Suzume Shindo            "will not join a Spider Clan player." / "You have the first opportunity to focus or strike in duels where Shindo is the challenger."   P3 Effect; DuelRuleRegistry (P18)
//   Ikoma Natsu - exp        "will not join a Unicorn Clan player." / "Open: Give a target Personality Cavalry."          P3 Effect; w733-natsu-cavalry
//   Ichiro Otani             "will not enter play if your Family Honor is 0 or above." / "Battle: Otani challenges a target enemy Personality to a duel of Force. Items do not give bonuses ... Bow the duel's loser."   P3 Effect; w733-otani-duel (P18 ExcludeItemBonuses)
//   Tsi Hayamizu             "will not join a Scorpion Clan player." / "+1F for each Mine you control."                   P3 Effect; EngineHooks.PersonalityForce
//   Kimura, Forgiver of Sins "enters play ignoring Gold Cost if you control the Ring of Air." / "Air Limited: Discard a card. Increase the Honor you gain from your next Proclaim this turn to equal the card's Focus Value."   entry term; w733-kimura-proclaim
//   Yoritomo Yashinko - exp  "Crane Clan players may Proclaim Yashinko." / ":bow: Produce 3 Gold."                          P3 Effect crane-players-may-proclaim; a bowing payment token (P1)
//   Court Scribe             "Political Open, bow: Increase the next Honor gain or loss (this turn) from your cards by 1 (Proclaiming gains are not from your cards)."   w733-scribe-honor
//   Invocation               "Limited: Until your next turn begins, Honor gains are reduced by 1 unless the player controls a Spirit."   w733-invocation (P14 each-gain modifier)
//   Delicate Gamble          "Political Limited: Bow your target unbowed Courtier. Discard the top card of another player's Fate deck. A target player gains or loses Honor equal to its Focus Value or 3, whichever is lower. If he lost Honor, he ignores Honor Requirements this game."   w733-gamble
//   The False Path           the Stronghold: Monk and Fudo, Fudo's price and Honor Requirement, no Honor loss from Fudo cards, no Enlightenment victory, and the Fudo Ring search   keyword rules (P10), entry term, recruit gate (P3), Honor-change hook (P14), w733-falsepath-search
//
// Issue #835: Merchant Patronage and The Heavens' Blessing were wired afterwards in Wave835.cs, on Mendon's rulings of 2026-10-11 (docs/decisions/2026-10-11-twelve-ruled-cards.md); the "not wired" note here is the state when this wave merged.
// Not wired, with a blocker note (tools/content/pe_wave_733_blockers.json): Zansho - exp (the scan's "Zansho's target unbowed Follower" does not say whose Follower: a ruling), Merchant Patronage (a Political Interrupt
// before an action on a Holding, and the acting player choosing who gains or loses its Honor), The Heavens' Blessing (a game-long payment to draw from a Strategy's own effect in an Action Phase) and Marriage of the
// Emerald Champion (an Event that targets, holds a dishonor bar and an Honor shield while in play, and discards itself on its controller's second turn).
public static class Wave733Abilities
{
    public const string NatsuCavalry = "w733-natsu-cavalry", OtaniDuel = "w733-otani-duel", KimuraProclaim = "w733-kimura-proclaim", ScribeHonor = "w733-scribe-honor", Invocation = "w733-invocation",
        Gamble = "w733-gamble", FalsePathSearch = "w733-falsepath-search";

    public const string OwnUnbowedCourtier = "w733-own-unbowed-courtier";

    public const string Shindo = "oracle-l5r-10450", Kimura = "oracle-l5r-10451", Hamada = "oracle-l5r-10308", Hayamizu = "oracle-l5r-10503", Yashinko = "oracle-l5r-10860", FalsePath = "oracle-l5r-10561";

    /// <summary>The Stronghold's Effect: the keyword rules and the Honor rules of The False Path name it.</summary>
    public const string FalsePathEffect = "w733-false-path";

    // "Search your discard pile or deck for a Fudo Ring": the discard pile first, then the Fate deck (Mendon's R4 ruling on ordered searches); Rings are Fate cards.
    public static readonly SearchCriteria FudoRing = new("Fudo Ring", Types: [CardType.Ring], Keywords: ["Fudo"]);
    public static readonly SearchSpec FudoRingSearch = new("w733-fudo-ring", "Search your discard pile or Fate deck for a Fudo Ring",
        [new SearchDeckOption(SearchDeck.FateDiscard, FudoRing, SearchEffect.ToHand), new SearchDeckOption(SearchDeck.Fate, FudoRing, SearchEffect.ToHand)], Ordered: true);

    public static Dictionary<string, AbilityDefinition> AddWave733(this Dictionary<string, AbilityDefinition> all)
    {
        all[NatsuCavalry] = new("Open", "Open: Give a target Personality Cavalry", "personality");
        all[OtaniDuel] = new("Battle", "Battle: Otani challenges a target enemy Personality to a duel of Force. Items do not give bonuses to Force during the duel. Bow the duel's loser", "enemy-personality",
            IssuesDuel: true, DuelStat: "Force");
        all[KimuraProclaim] = new("Limited", "Air Limited: Discard a card. Increase the Honor you gain from your next Proclaim this turn to equal the card's Focus Value", "slots",
            Slots: [new("discard", "hand", "A card from your hand, to discard (its Focus Value is the Proclaim's Honor)") { CostOnly = true }],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))], ActionKeywords: ["Air"]);
        all[ScribeHonor] = new("Open", "Political Open, bow this card: Increase the next Honor gain or loss (this turn) from your cards by 1", "none", Costs: ActionCost.BowSource, ActionKeywords: ["Political"]);
        all[Invocation] = new("Limited", "Limited: Until your next turn begins, Honor gains are reduced by 1 unless the player controls a Spirit", "none");
        // The branch is the shape only: the discard comes first and the four options (who, gain or lose, how much) are written when the action resolves (RunWave733).
        all[Gamble] = new("Limited", "Political Limited: Bow your target unbowed Courtier. Discard the top card of another player's Fate deck. A target player gains or loses Honor equal to the discarded card's Focus Value or 3",
            OwnUnbowedCourtier, ActionKeywords: ["Political"], Costs: [new(CostVerb.Bow, CostSubject.Target)],
            Branch: new ConditionalChoice(BranchCondition.Always, HonorChoice.Options(3), HonorChoice.None));
        all[FalsePathSearch] = new("Limited", "Limited, bow this card: Search your discard pile or deck for a Fudo Ring, show it, and put it in your hand", "none", Costs: ActionCost.BowSource, Search: FudoRingSearch);
        return all;
    }
}

/// <summary>A one-shot Honor boost a card of Wave 733 stored: Kind "scribe" (the next gain or loss from this player's cards is one larger) or "kimura" (the next Proclaim's gain is at least Value).</summary>
public sealed class Wave733HonorBoost
{
    public string Kind { get; set; } = "";
    public int PlayerId { get; set; }
    public int Turn { get; set; }
    public int Value { get; set; }
}

public sealed partial class MatchState
{
    // 22910-22919 is reserved for batch 2 of the wirable-card waves (issue #733). Omitted until a boost is stored.
    [JsonPropertyOrder(22910)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Wave733HonorBoost>? Wave733HonorBoosts { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> Wave733InPlay(int playerId, string definitionId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.DefinitionId == definitionId && InPlay(c));

    private bool Wave733ControlsFalsePath(int playerId) => Wave733InPlay(playerId, Wave733Abilities.FalsePath).Any();

    // ---- strength: Hamada, Hayamizu ---------------------------------------------------------------------------------------------------------------------------------------------------

    private int Wave733Force(CardInstance card)
    {
        if (!InPlay(card)) return 0;
        switch (card.DefinitionId)
        {
            // "+1F for each of your Personalities who is Crab Clan or a Courtier": Hamada himself counts if he is one (he is a Scorpion Samurai, so he does not).
            case Wave733Abilities.Hamada:
                return _state.Cards.Values.Count(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Type == CardType.Personality && (HasClanAffiliation(c, "Crab") || Keyword(c, "Courtier")));
            // "+1F for each Mine you control": Holdings with the Mine keyword in play.
            case Wave733Abilities.Hayamizu:
                return _state.Cards.Values.Count(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Type == CardType.Holding && Keyword(c, "Mine"));
            default:
                return 0;
        }
    }

    // ---- entry terms: Kimura's Ring, The False Path's Fudo discount ---------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CostTerm> Wave733Entry(EntryCostContext context)
    {
        if (context.Kind is not (EntryCostKind.Recruit or EntryCostKind.Reserve)) yield break;
        if (context.Card.DefinitionId == Wave733Abilities.Kimura && ControlsRing(context.PayerId, "Ring of Air")) yield return new(0, "Ring of Air", IgnoreCost: true);
        // "Fudo Personalities join you for 2 less Gold": a Personality with the Fudo keyword (printed or given by this Stronghold) joining its controller.
        if (Definition(context.Card).Type == CardType.Personality && Keyword(context.Card, "Fudo") && Wave733ControlsFalsePath(context.PayerId)) yield return new(-2, "The False Path");
    }

    // ---- recruit gate: The False Path ignores the Honor Requirement of your Fudo cards ------------------------------------------------------------------------------------------

    private RecruitGateResult? Wave733RecruitGate(RecruitGateContext context) =>
        context.Kind is RecruitGateKind.Recruit or RecruitGateKind.Reserve && Keyword(context.Card, "Fudo") && Wave733ControlsFalsePath(context.PlayerId)
            ? new RecruitGateResult(IgnoreHonorRequirement: true) : null;

    // ---- Enlightenment: The False Path --------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"You may not win by Enlightenment": read by EnlightenmentBarred beside the stored rule flags.</summary>
    internal bool Wave733EnlightenmentBarred(int playerId) => Wave733ControlsFalsePath(playerId);

    // ---- Honor changes: The False Path, Court Scribe, Kimura ---------------------------------------------------------------------------------------------------------------------------

    private HonorChangeResult? Wave733HonorChange(HonorChangeContext context)
    {
        if (context.Stage != HonorChangeStage.Standing) return null;
        var amount = context.Amount;
        // "You do not lose Honor from Fudo cards you own": a loss whose source is a Fudo card of the affected player's, while he controls The False Path.
        if (amount < 0 && context.SourceCardId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var source) && source.OwnerId == context.PlayerId && Keyword(source, "Fudo")
            && Wave733ControlsFalsePath(context.PlayerId)) return new HonorChangeResult(0);
        var boosts = _state.Wave733HonorBoosts;
        if (boosts is null) return null;
        // Kimura: the Honor of the next Proclaim this turn is raised to the discarded card's Focus Value (never lowered: "increase").
        if (context.IsProclaim && amount > 0 && boosts.FirstOrDefault(b => b.Kind == "kimura" && b.PlayerId == context.PlayerId && b.Turn == _state.TurnNumber) is { } kimura)
        {
            boosts.Remove(kimura);
            if (boosts.Count == 0) _state.Wave733HonorBoosts = null;
            return kimura.Value > amount ? new HonorChangeResult(kimura.Value) : null;
        }
        // Court Scribe: the next gain or loss from a card of the Scribe's controller (either player's Honor), this turn; Proclaiming gains are not from a card.
        if (!context.IsProclaim && context.SourcePlayerId is { } sourcePlayer
            && boosts.FirstOrDefault(b => b.Kind == "scribe" && b.PlayerId == sourcePlayer && b.Turn == _state.TurnNumber) is { } scribe)
        {
            boosts.Remove(scribe);
            if (boosts.Count == 0) _state.Wave733HonorBoosts = null;
            return new HonorChangeResult(amount + Math.Sign(amount));
        }
        return null;
    }

    private void Wave733AddBoost(string kind, int playerId, int value = 0) =>
        (_state.Wave733HonorBoosts ??= []).Add(new() { Kind = kind, PlayerId = playerId, Turn = _state.TurnNumber, Value = value });

    // ---- payment: Yoritomo Yashinko - exp ---------------------------------------------------------------------------------------------------------------------------------------------

    // ":bow: Produce 3 Gold": a trait of a Personality in play, a bowing payment source for any payment.
    private IEnumerable<PaymentToken> Wave733Tokens(PaymentTokenContext context)
    {
        foreach (var yashinko in Wave733InPlay(context.PlayerId, Wave733Abilities.Yashinko).Where(c => !c.Bowed))
            yield return new(yashinko.Id + "#yashinko", yashinko, Wave733Abilities.Yashinko, 3, "bow-reaction", "Yoritomo Yashinko: bow her, produce 3 Gold", null, Bows: true);
    }

    // ---- target legality ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave733TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        return context.ability.Target == Wave733Abilities.OwnUnbowedCourtier && InPlay(target) && Definition(target).Type == CardType.Personality && target.OwnerId == context.player.Id
            && !target.Bowed && Keyword(target, "Courtier");
    }

    // ---- effects ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave733(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave733Abilities.FalsePathSearch:
                BeginSearch(c.ability.Search!, source!, player.Id, "ability", c.bowedBeforeAction);
                return true;
            case Wave733Abilities.NatsuCavalry:
                GrantKeyword(target!, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case Wave733Abilities.KimuraProclaim:
                Wave733AddBoost("kimura", player.Id, FocusValueOf(SlotCards(c.command, "discard").Single()));
                return false;
            case Wave733Abilities.ScribeHonor:
                Wave733AddBoost("scribe", player.Id);
                return false;
            case Wave733Abilities.Invocation:
                // "Until your next turn begins": through the opponent's turn, lapsing when the turn number moves on again.
                SpendHandPlayedCard(player, source);
                AddHonorModifier(new HonorModifier
                {
                    OwnerPlayerId = player.Id,
                    Kind = HonorModifierKinds.EachGain,
                    Delta = -1,
                    Filters = [HonorModifierFilters.UnlessControlsPrefix + "Spirit"],
                    UntilTurn = _state.TurnNumber + 1,
                    SourceCardId = source?.Id
                });
                return false;
            case Wave733Abilities.Gamble:
                {
                    // The discard comes first, so the choice is made knowing the card; "another player's" Fate deck is the opponent's in a two-player game.
                    SpendHandPlayedCard(player, source);
                    var focus = DiscardTopOfFateDeck(Player(1 - player.Id));
                    if (focus is null) return false;
                    var amount = Math.Min(focus.Value, 3);
                    if (amount == 0) return false;
                    if (!BeginConditionalChoice(c.command, c.ability, c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants)) return false;
                    _state.PendingEffectChoice!.Options =
                    [
                        new("self-gain", $"You gain {amount} Honor", [$"w733-gamble:self:{amount}"]),
                        new("opponent-lose", $"Your opponent loses {amount} Honor", [$"w733-gamble:opponent:{-amount}"]),
                        new("opponent-gain", $"Your opponent gains {amount} Honor", [$"w733-gamble:opponent:{amount}"]),
                        new("self-lose", $"You lose {amount} Honor", [$"w733-gamble:self:{-amount}"])
                    ];
                    return true;
                }
            default:
                return null;
        }
    }

    // A chosen Delicate Gamble option: the Honor change, and "if he lost Honor, he ignores Honor Requirements this game".
    private bool Wave733BranchEffect(string effect, GameCommand command, CardInstance source)
    {
        if (!effect.StartsWith("w733-gamble:", StringComparison.Ordinal)) return false;
        var parts = effect.Split(':');
        var affected = parts[1] == "self" ? command.PlayerId : 1 - command.PlayerId;
        var amount = int.Parse(parts[2]);
        var before = Player(affected).Honor;
        GainHonor(affected, amount, Definition(source).Name, source);
        if (amount < 0 && Player(affected).Honor < before) IgnoreHonorRequirementsForRestOfGame(affected, Definition(source).Name);
        return true;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave733Hooks()
    {
        EngineHooks.Abilities.Register("Wave733Abilities", 13400, static abilities => abilities.AddWave733());
        EngineHooks.Searches.Register("Wave733Searches", 13400, static () => [Wave733Abilities.FudoRingSearch]);
        EngineHooks.PersonalityForce.Register("Wave733Force", 13400, static (engine, context) => engine.Wave733Force(context.card));
        EngineHooks.EntryCostTerms.Register("Wave733Entry", 13400, static context => context.Engine.Wave733Entry(context));
        EngineHooks.RecruitGate.Register("Wave733RecruitGate", 13400, static (engine, context) => engine.Wave733RecruitGate(context));
        EngineHooks.HonorChange.Register("Wave733HonorChange", 13400, static context => context.Engine.Wave733HonorChange(context));
        EngineHooks.PaymentTokens.Register("Wave733PaymentTokens", 13400, static (engine, context) => engine.Wave733Tokens(context));
        EngineHooks.TargetAllows.Register("Wave733TargetAllows", 13400, static (engine, context) => engine.Wave733TargetAllows(context));
        EngineHooks.RunEffects.Register("Wave733RunEffects", 13400, static (engine, context) => engine.RunWave733(context));
        EngineHooks.BranchEffects.Register("Wave733BranchEffects", 13400, static (engine, effect, command, source, _) => engine.Wave733BranchEffect(effect, command, source));
        // Otani: "Items do not give bonuses or positive Modifiers to Force during the duel." Shindo: "You have the first opportunity to focus or strike in duels where Shindo is the challenger."
        DuelRuleRegistry.ForAbility("Wave733OtaniDuelRules", 13400, Wave733Abilities.OtaniDuel, new DuelRules { ExcludeItemBonuses = true });
        DuelRuleRegistry.Register("Wave733ShindoDuelRules", 13400, static context =>
            context.Challenger.DefinitionId == Wave733Abilities.Shindo ? new DuelRules { FirstToFocusPlayerId = context.ChallengerPlayerId } : null);
        // The False Path: "Your non-Unique Monk and Fudo Personalities in and out of play have Monk and Fudo." Two rules, so a Monk gains Fudo and a Fudo Personality gains Monk.
        var personality = new KeywordCondition(KeywordConditionKind.SubjectIsType, "Personality");
        var notUnique = new KeywordCondition(KeywordConditionKind.SubjectLacksKeyword, "Unique");
        KeywordRuleCatalog.Register(new KeywordRule("w733-false-path-fudo", KeywordRuleScope.Controller, ["Fudo"], SourceEffect: Wave733Abilities.FalsePathEffect, InAndOutOfPlay: true,
            AllOf: [personality, notUnique, new(KeywordConditionKind.SubjectHasKeyword, "Monk")]));
        KeywordRuleCatalog.Register(new KeywordRule("w733-false-path-monk", KeywordRuleScope.Controller, ["Monk"], SourceEffect: Wave733Abilities.FalsePathEffect, InAndOutOfPlay: true,
            AllOf: [personality, notUnique, new(KeywordConditionKind.SubjectHasKeyword, "Fudo")]));
    }
}
