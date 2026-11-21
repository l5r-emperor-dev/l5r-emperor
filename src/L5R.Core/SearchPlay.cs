using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #667, CLUSTER "SEARCH-PLAY" (personality-wave-125: Gozu, Hyobuko, Masaya; item-wave-43: Heart of Fudo - exp2, The Sacred Rosary of the Constrictors, The Topaz Armor - exp).
// Every card was read letter by letter against its scan. Mendon's rulings are built as stated; the readings built in beyond them are in docs/decisions/2026-10-05-667-search-play.md.
//
// New primitives (each in its own SearchPlay*.cs file; the shared engine files carry short, commented hooks into them):
//   * A PLAY OWED AFTER A SEARCH (SearchPlayGozu.cs). A search whose finding must then be played ("search ..., show it, then take an additional Battle action to play it, if legal; if not,
//     discard it"): the found card goes to the hand (SearchEffect.ToHand), and when the search finishes the engine asks QueryActions whether the player could play it now. If so he is granted a
//     MANDATORY additional action scoped to it and the play is OWED (MatchState.SearchPlayOwes): the pass, the decline of the additional action and every other action are refused (and listed
//     disabled) until the card has been played (Mendon's ruling: "the terrain play is mandatory and the player may not pass"). If not, the card is discarded.
//   * A SEARCH THAT PUTS A CELESTIAL INTO PLAY WITH NO PROVINCE (SearchPlayRosary.cs): SearchEffect "sp-celestial-into-play", through the shared Celestial entry (MatchEngine.EnterCelestialPlay);
//     the place searched (the Dynasty deck or the Dynasty discard pile) is remembered on the search, and a discard-pile source removes the Item from the game.
//   * A TWO-OF-ELEVEN KEYWORD CHOICE AS AN ENTRY TRAIT (SearchPlayTopaz.cs): a queued Event-choice style decision asked twice, whose pair is granted to the wielder like the Spirit cards' grants
//     (GrantDuration.Attachment, so it ends when the Armor leaves play for any reason, and follows the Armor on a transfer).
//   * A PROVINCE RECRUIT AS AN OPEN ACTION (SearchPlayMasaya.cs): Rise of the Dark Naga's shared recruit entry sequence, taken by the face-up province card itself, and the return of the
//     Personality to the top of his owner's Dynasty deck as the turn ends (SearchPlayReturns).
//   * A PERSONALITY DISCARDED FROM PLAY AS A COST (Hyobuko), and a wielder-dependent attach price (Heart of Fudo to Hyobuko, The Topaz Armor to a Topaz Champion).
// ======================================================================================================================================================================================

public static class SearchPlayAbilities
{
    public const string GozuDestroy = "sp-gozu-destroy", GozuDestroyDiscard = "sp-gozu-destroy-discard", GozuUsageKey = "sp-gozu", GozuGrantId = "sp-gozu-found-terrain";
    public const string HyobukoSearch = "sp-hyobuko-search", HyobukoEffect = "sp-hyobuko-fudo";
    public const string MasayaRecruit = "sp-masaya-recruit";
    public const string FudoFetch = "sp-fudo-fetch";
    public const string RosaryCelestial = "sp-rosary-celestial";
    public const string TopazEffect = "sp-topaz-armor";
    public const string Gozu = "oracle-l5r-2887", Hyobuko = "oracle-l5r-3534", Masaya = "oracle-l5r-4853", HeartOfFudoExp2 = "oracle-l5r-10523", SacredRosary = "oracle-l5r-9982", TopazArmor = "oracle-l5r-9980";

    public static Dictionary<string, AbilityDefinition> AddSearchPlay(this Dictionary<string, AbilityDefinition> all)
    {
        // Gozu (oracle-l5r-2887): "Battle: Target a Terrain: Destroy it. You may discard a card. If you did, search your Fate deck for a Terrain, show it, then take an additional Battle action to play
        // it, if legal; if not, discard it." Two ways of taking the one action sharing one use (Ikoma Shika - exp's shape): the discard rides on the command (CardIds) and is paid as the action is
        // announced, Mendon's "once the optional action of discarding a card is selected".
        all[GozuDestroy] = new("Battle", "Target a Terrain: Destroy it", "terrain-at-battle", UsageKey: GozuUsageKey);
        all[GozuDestroyDiscard] = new("Battle", "Target a Terrain: Destroy it. Discard a card: search your Fate deck for a Terrain, show it, then take an additional Battle action to play it, if legal; if not, discard it",
            "terrain-at-battle", UsageKey: GozuUsageKey, Costs: [new(CostVerbs.Discard, CostSubject.Chosen)]);
        // Hyobuko (oracle-l5r-3534): "Limited: Discard Hyobuko: He searches for an ancient relic. Search your discard pile, then Fate deck, for a card titled Heart of Fudo. Show it and put it in your hand."
        all[HyobukoSearch] = new("Limited", "Discard Hyobuko: Search your discard pile, then Fate deck, for a card titled Heart of Fudo. Show it and put it in your hand", "none", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Masaya (oracle-l5r-4853): "Open: If Masaya is face-up in one of your provinces: Bring him into play, paying all costs and paying 2 less Gold. Before the turn ends, put him on top of your Dynasty deck."
        // The target is Masaya himself (Rise of the Dark Naga's province-card category; ValidateSearchPlay requires the source); the recruit price is the ordinary one less 2 (never below 0).
        all[MasayaRecruit] = new("Open", "If Masaya is face-up in one of your provinces: Bring him into play, paying all costs and paying 2 less Gold. Before the turn ends, put him on top of your Dynasty deck",
            "own-personality-in-province-with-keyword", RequiredKeywords: ["Samurai"], Cost: GoldCostRule.TargetRecruit(-2, 0), FromProvince: true);
        // Heart of Fudo - exp2 (oracle-l5r-10523): "Limited: If this Personality is unbowed, search your Fate deck for a card and put it into your hand. Bow this Personality. Destroy this Item."
        all[FudoFetch] = new("Limited", "If this Personality is unbowed, search your Fate deck for a card and put it into your hand. Bow this Personality. Destroy this Item", "none");
        // The Sacred Rosary of the Constrictors (oracle-l5r-9982): "Limited: Discard all your Celestials in play. Search your Dynasty deck or discard pile for a Celestial. Put it into play. If you
        // searched your discard pile for it, remove this card from the game."
        all[RosaryCelestial] = new("Limited", "Discard all your Celestials in play. Search your Dynasty deck or discard pile for a Celestial. Put it into play. If you searched your discard pile for it, remove this card from the game", "none");
        return all;
    }
}

public static class SearchPlaySpecs
{
    public const string GozuId = "sp-gozu-terrain", HyobukoId = "sp-hyobuko-relic", FudoId = "sp-fudo-card", RosaryId = "sp-rosary-celestial";
    /// <summary>The Rosary's effect on the found Celestial: put it into play with no province (MatchEngine.SearchPlayApplyEffect).</summary>
    public const string CelestialIntoPlay = "sp-celestial-into-play";

    private static readonly SearchCriteria TerrainCard = new("Terrain", [CardType.Strategy], ["Terrain"]);
    private static readonly SearchCriteria HeartOfFudoCard = new("card titled Heart of Fudo", Titles: ["Heart of Fudo"]);
    private static readonly SearchCriteria CelestialCard = new("Celestial", [CardType.Celestial]);

    /// <summary>Gozu: "search your Fate deck for a Terrain, show it". A search must take a card it can (Pass only when none qualifies); the found card then goes to the hand and is played (SearchPlayGozu.cs).</summary>
    public static SearchSpec Gozu { get; } = new(GozuId, "Search your Fate deck for a Terrain", [new(SearchDeck.Fate, TerrainCard, SearchEffect.ToHand)]);

    /// <summary>Hyobuko: "Search your discard pile, then Fate deck, for a card titled Heart of Fudo. Show it and put it in your hand." An ordered search (R-011): the first place holding a Heart of Fudo.</summary>
    public static SearchSpec Hyobuko { get; } = new(HyobukoId, "Search your discard pile, then Fate deck, for a card titled Heart of Fudo",
        [new(SearchDeck.FateDiscard, HeartOfFudoCard, SearchEffect.ToHand), new(SearchDeck.Fate, HeartOfFudoCard, SearchEffect.ToHand)], Ordered: true);

    /// <summary>Heart of Fudo - exp2: "search your Fate deck for a card and put it into your hand."</summary>
    public static SearchSpec Fudo { get; } = new(FudoId, "Search your Fate deck for a card", [new(SearchDeck.Fate, new("card"), SearchEffect.ToHand)]);

    /// <summary>The Sacred Rosary of the Constrictors: "Search your Dynasty deck or discard pile for a Celestial. Put it into play." The searcher chooses the place.</summary>
    public static SearchSpec Rosary { get; } = new(RosaryId, "Search your Dynasty deck or discard pile for a Celestial",
        [new(SearchDeck.Dynasty, CelestialCard, CelestialIntoPlay), new(SearchDeck.DynastyDiscard, CelestialCard, CelestialIntoPlay)]);

    public static IEnumerable<SearchSpec> All => [Gozu, Hyobuko, Fudo, Rosary];
}

/// <summary>A Personality to be put on top of his owner's Dynasty deck as the turn ends (Masaya), recorded on <paramref name="Turn"/>.</summary>
public sealed record SearchPlayReturn(int PlayerId, string CardId, int Turn);

/// <summary>A play owed after a search: <paramref name="PlayerId"/> must play the card <paramref name="CardId"/> (in his hand) as his next action.</summary>
public sealed record SearchPlayOwed(int PlayerId, string CardId, string SourceName);

public sealed partial class MatchState
{
    /// <summary>Plays owed after a search (Gozu's found Terrain), until the card has been played or can no longer be.</summary>
    [JsonPropertyOrder(9650)]
    public List<SearchPlayOwed> SearchPlayOwes { get; set; } = [];
    /// <summary>The two keywords last chosen for each The Topaz Armor (by the Armor's card id), granted to its wielder while it is attached.</summary>
    [JsonPropertyOrder(9651)]
    public Dictionary<string, List<string>> SearchPlayTopazKeywords { get; set; } = [];
    /// <summary>Personalities owed to the top of their owners' Dynasty decks as the turn ends (Masaya).</summary>
    [JsonPropertyOrder(9652)]
    public List<SearchPlayReturn> SearchPlayReturns { get; set; } = [];
}

public sealed partial class MatchEngine
{
    // ---- the hooks into the shared engine files ----------------------------------------------------------------------------------------------------------------------------------------

    // The abilities that carry the card an optional cost spends on CardIds (the same three touchpoints Ikoma Shika - exp uses: validation, the offered actions, the label).
    private static bool SearchPlayCarriesCostCard(string abilityId) => abilityId == SearchPlayAbilities.GozuDestroyDiscard;

    private string?[]? SearchPlayCostCardChoices(string abilityId, PlayerState player, CardInstance? source) => abilityId == SearchPlayAbilities.GozuDestroyDiscard
        ? player.Hand.Where(h => h != source?.Id).Select(h => (string?)h).DefaultIfEmpty(null).ToArray() : null;

    private static string? SearchPlayCostCardVerb(string abilityId) => abilityId == SearchPlayAbilities.GozuDestroyDiscard ? "discards" : null;

    // ValidateEarly: the conditions of taking each action that the declarative data cannot say.
    private void ValidateSearchPlay(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Gozu's optional discard: a card in the acting player's hand other than the card being used (Gozu is in play, so any hand card).
            case SearchPlayAbilities.GozuDestroyDiscard:
                Require(ChosenCostCard(command) is { } discard && discard.Zone == Zone.Hand && player.Hand.Contains(discard.Id) && discard.Id != source?.Id, "Choose a card in your hand to discard.");
                break;
            // Masaya: "If Masaya is face-up in one of your provinces" -- the action is his own, so the card brought into play is the source itself.
            case SearchPlayAbilities.MasayaRecruit:
                Require(source is not null && source.Zone == Zone.Province && source.FaceUp && source.OwnerId == player.Id && command.TargetId == source.Id, "Masaya must be face-up in one of your provinces.");
                Require(_state.Phase == Phase.Action, "Masaya's Open action is taken in the Action Phase.");
                break;
        }
    }

    // The abilities that destroy their target (an "after another player's action targets ..." Reaction reads it).
    private static bool SearchPlayDestroysTarget(string abilityId) => abilityId is SearchPlayAbilities.GozuDestroy or SearchPlayAbilities.GozuDestroyDiscard;

    // ---- the execution switch ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this cluster's. false: the effects are done. true: a decision opened (a search) and the action finishes from there.
    private bool? RunSearchPlay(RunHookContext context)
    {
        var (id, command, player, source, target) = (context.id, context.command, context.player, context.source, context.target);
        switch (id)
        {
            case SearchPlayAbilities.GozuDestroy:
                SearchPlayDestroyTerrain(player, target!);
                return false;
            case SearchPlayAbilities.GozuDestroyDiscard:
                SearchPlayDestroyTerrain(player, target!);
                BeginSearch(SearchPlaySpecs.Gozu, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case SearchPlayAbilities.FudoFetch:
                return SearchPlayFudoFetch(context);
            case SearchPlayAbilities.HyobukoSearch:
                BeginSearch(SearchPlaySpecs.Hyobuko, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case SearchPlayAbilities.RosaryCelestial:
                return SearchPlayRosary(context);
            case SearchPlayAbilities.MasayaRecruit:
                return SearchPlayMasayaRecruit(player, source!, command);
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSearchPlayHooks()
    {
        EngineHooks.Abilities.Register("AddSearchPlay", 13400, static abilities => abilities.AddSearchPlay());
        EngineHooks.Searches.Register("SearchPlaySpecs", 13400, static () => SearchPlaySpecs.All);
        EngineHooks.DestroysTarget.Register("SearchPlayDestroysTarget", 13400, static context => SearchPlayDestroysTarget(context.abilityId));
        EngineHooks.ValidateEarly.Register("ValidateSearchPlay", 13400, static (engine, context) => engine.ValidateSearchPlay(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunSearchPlay", 13400, static (engine, context) => engine.RunSearchPlay(context));
    }
}
