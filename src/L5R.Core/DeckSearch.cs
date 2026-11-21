using System.Text.Json.Serialization;

namespace L5R.Core;

// Deck search (issue #312): ONE reusable "search a deck, select a legal card, show it, apply the effect,
// shuffle" flow, private to the searching player. First consumers: Ikoma Shizuka (a choice of two decks,
// each with its own criteria and its own effect) and Imperial Gift (one deck, one criteria). The catalogue
// has ~44 more cards that search a deck or a discard pile; the pieces below are shaped for that family:
//
//   1. SearchSpec/SearchCriteria are DECLARATIVE data on the ability (AbilityDefinition.Search) or, for an
//      Event, looked up from its Effect (SearchCatalog.ForEventEffect). The engine evaluates the criteria per
//      candidate; the client never filters or decides legality.
//   2. The flow is one pending state (MatchState.PendingSearch) driven by four PendingChoice types, in
//      exactly this order:  optional deck choice -> candidate list -> select (or pass when nothing
//      qualifies) -> SHOW the card to the opponent -> APPLY the card's own effect -> SHUFFLE the searched
//      deck (also when the player passed) -> the action resolves and play continues.
//   3. Hidden information: the searcher's MatchView.PendingSearch lists the deck's remaining cards GROUPED
//      by card with a count, sorted by type then name (never deck order), each with an eligibility flag and
//      a short reason. The opponent gets none of it -- only public events that a search happened and, after
//      selection, the deliberately shown card (MatchView.ShownCards). The RNG state and the deck order never
//      leave the engine.
//
// The shown-cards record is keyed by a per-match sequence number, NOT the card's instance id (see
// docs/decisions/2026-09-18-deck-search-primitive.md): an instance id would be a handle the opponent could
// try to resolve elsewhere, and the record needs only the definition to be reviewable.

/// <summary>The two decks a search can target, as they appear on the wire.</summary>
public static class SearchDeck
{
    public const string Dynasty = "Dynasty";
    public const string Fate = "Fate";
    /// <summary>The searcher's own Fate discard pile (The Shadow's Embrace pass, issue #476 batch 6: Discovering the Temple's "search your Fate deck or discard
    /// pile"). A public pile: its cards are listed like a deck's, it is never shuffled, and a card taken from it simply leaves the pile.</summary>
    public const string FateDiscard = "FateDiscard";
    /// <summary>The searcher's own Dynasty discard pile, the dead Personalities included (Emperor Edition remaining-cards pass, issue #492: I Give You My Name's "Dynasty deck or discard pile", Shinjo's
    /// Guidance's "discard pile, then Dynasty deck"). A public pile, listed like a deck, never shuffled; a card taken from it leaves it by changing zone.</summary>
    public const string DynastyDiscard = "DynastyDiscard";
    /// <summary>The searcher's own hand (Emperor Edition follow-ups, issue #498: Asako Chukage's "search your discard pile, then hand, then deck"). Known to its owner, listed like a discard pile, never
    /// shuffled; a card taken from it leaves the hand by changing zone (put into play), or simply stays there (put into the hand).</summary>
    public const string Hand = "Hand";
    // P20 (SearchExtensions.cs, issue #728): the searcher's own province cards (face-up or face-down: only the searcher sees the list), and either player's discard piles.
    public const string OwnProvinces = "OwnProvinces", AnyPlayerFateDiscard = "AnyPlayerFateDiscard", AnyPlayerDynastyDiscard = "AnyPlayerDynastyDiscard";
}

/// <summary>What happens to the chosen card once it has been shown (the card's own printed effect).</summary>
public static class SearchEffect
{
    /// <summary>Put it into the searcher's hand (Imperial Gift; the Fate half of Ikoma Shizuka before her discard).</summary>
    public const string ToHand = "to-hand";
    /// <summary>Put it into the hand, then the searcher discards a card of their choice (Ikoma Shizuka's Terrain).</summary>
    public const string ToHandThenDiscard = "to-hand-then-discard";
    /// <summary>Attach the Region to one of the searcher's own provinces that has none, paying its Gold cost; a
    /// Region that cannot be attached (no free province, or it cannot be paid) goes to the Dynasty discard pile.</summary>
    public const string AttachRegion = "attach-region";
    /// <summary>Versatile Farm (The Shadow's Embrace pass, issue #476 batch 6): "put it into play, unbowed, paying 2 less Gold" -- a Holding recruited from the
    /// searched deck at its entry price less <see cref="SearchSpec.RecruitDiscount"/>, paid through the one payment path (a ChoosePayment stage when it costs
    /// anything); a card whose price cannot be paid does not enter play and stays in the deck, which is shuffled as always.</summary>
    public const string RecruitUnbowed = "recruit-unbowed";
    /// <summary>Meeting the Keepers (Emperor Edition remaining-cards pass, issue #492, group B): "put it on top of the deck after shuffling" -- the card stays in the searched deck, which is
    /// shuffled as every search ends, and only then is put on top (PendingSearch.PutOnTopId).</summary>
    public const string PutOnTopAfterShuffle = "put-on-top-after-shuffle";
    /// <summary>Sacrifice of Pawns (Emperor Edition remaining-cards pass, group C, issue #492): "bring him into play, paying all costs but paying two more Gold. (Discard him if you are unable to play him.)" -- a Personality
    /// of the searched Dynasty deck recruited at home for his whole entry price plus 2, or discarded (EmperorRemainingC.cs).</summary>
    public const string RecruitPersonality = "recruit-personality";
    /// <summary>I Give You My Name (issue #492): "Put the Oni into play, ignoring Gold cost" -- a Personality enters the searcher's home free (no Honor Requirement, no price).</summary>
    public const string PutIntoPlay = "put-into-play";
    /// <summary>Shinjo's Guidance (issue #492): "put her into one of your provinces, face-up, discarding the card that was there" -- the searcher picks the province.</summary>
    public const string ToProvinceFaceUp = "to-province-face-up";
    /// <summary>Celestial Sword of the Dragon, Asako Chukage (issue #498): "Put it into play or your hand" -- the searcher chooses (SearchStage.ChooseDestination); a Ring put into play by the search enters
    /// through the Ring entry path marked so it does not count towards an Enlightenment Victory while it remains in play (EmperorFollowupsSearch.cs).</summary>
    public const string RingIntoPlayOrHand = "ring-into-play-or-hand";
    /// <summary>In Search of Guidance (issue #503): "Refill this province with him, face-up" -- the found card is held until the revealed Event leaves its province, then refills it face-up (EmperorP6.cs).</summary>
    public const string RefillSourceProvinceFaceUp = "refill-source-province-face-up";
    // P20 (SearchExtensions.cs, issue #728). See that file for each disposition.
    public const string AttachToPersonality = "attach-to-personality", Bottom = "bottom", CelestialIntoPlay = "celestial-into-play", RecruitFromProvinceIgnoringCosts = "recruit-from-province-ignoring-costs",
        PlayAsAdditionalAction = "play-as-additional-action", Discard = "discard", RemoveFromGame = "remove-from-game";
}

/// <summary>The stage a pending search is in; also names which PendingChoice type is open.</summary>
public static class SearchStage
{
    public const string ChooseDeck = "ChooseDeck";           // PendingChoice "searchDeck"
    public const string Select = "Select";                   // PendingChoice "searchSelect"
    public const string ChooseProvince = "ChooseProvince";   // PendingChoice "searchRegionProvince"
    public const string ChooseDiscard = "ChooseDiscard";     // PendingChoice "searchDiscard"
    public const string ChoosePayment = "ChoosePayment";     // PendingChoice "searchRecruitPay" (issue #476 batch 6, Versatile Farm)
    public const string ChooseDestination = "ChooseDestination"; // PendingChoice "searchDestination" (issue #498: "put it into play or your hand")
    public const string ChooseAttachTarget = "ChooseAttachTarget"; // PendingChoice "searchAttachTarget" (P20: AttachToPersonality's follow-up target slot)
    public const string OtherPlayerChooses = "OtherPlayerChooses"; // PendingChoice "shownChoice" (P25), decided by the other player (P20)
}

/// <summary>One numeric condition on a candidate card (Focus Value, Gold Cost, ...). Stat is one of
/// Focus, GoldCost, Force, Chi, PersonalHonor; Op is one of &lt;=, &gt;=, &lt;, &gt;, ==. The right-hand side is a
/// constant; a comparison against the performer's own stat ("a lower Focus Value than your Personality's Chi",
/// Refuge of the Three Sisters) is a natural extension and is not built yet.</summary>
public sealed record SearchComparison(string Stat, string Op, int Value);

/// <summary>Who may be selected. Every condition that is set must hold (Keywords is "all of"). Noun is the short
/// display name of what is being searched for ("Region", "Terrain", "Item") used in prompts and reasons.</summary>
public sealed record SearchCriteria(string Noun, CardType[]? Types = null, string[]? Keywords = null,
    string[]? ExcludedKeywords = null, bool NonUnique = false, string[]? Titles = null, SearchComparison[]? Comparisons = null,
    // "A card with Thunder or an element keyword" (Naka Mahatsu - exp, Seeds of Decay, issue #453): at least one of these keywords ("any of"), where Keywords is "all of".
    string[]? AnyKeywords = null,
    // "A ... Holding with another title" (Versatile Farm, issue #476 batch 6): titles that may NOT be chosen.
    string[]? ExcludedTitles = null,
    // "A Kiho Battle Strategy" (Martial Instruction, issue #492): a card with a printed Battle action -- an ability whose timing is Battle or Battle/Open.
    bool BattleStrategy = false,
    // "A Strategy with a Political ability" (Gaining Advantage, issue #503): one of the card's printed actions is an action with this keyword (EmperorP2Search.cs).
    // "A card with a Recon ability" (Daidoji Akeha, issue #503): one of the card's printed actions carries the keyword (its own tag, or the card's keyword).
    string? AbilityKeyword = null,
    // P20 (SearchExtensions.cs): "a Gold-producing Holding" (HasGoldProducingTrait: a printed Gold production or a Gold-producing trait), and "cards with different titles" (a condition on the SET picked).
    bool GoldProducing = false, bool DifferentTitles = false);

/// <summary>One searchable deck, the criteria that deck's candidates must meet, and what happens to the chosen
/// card. A card that lets the player pick the deck (Ikoma Shizuka) lists two, each with its own criteria.</summary>
public sealed record SearchDeckOption(string Deck, SearchCriteria Criteria, string Effect);

/// <summary>A whole search: Optional is true only for a printed "you may search" (Pass is then legal even when a
/// card qualifies); every other search must take an eligible card and may pass only when none qualifies.</summary>
public sealed record SearchSpec(string Id, string Label, SearchDeckOption[] Decks, bool Optional = false, int RecruitDiscount = 0,
    // "Search your discard pile, then Fate deck" (Togashi's Guidance, Shinjo's Guidance; issue #492): the places are searched IN ORDER -- no choice of deck, the first is searched first. Mendon's ruling
    // (R4): "If a card satisfies the search criteria, the player must take it from the first source found. If multiple cards in a searched location satisfy the search condition, the player may choose ...
    // If the first search location has no card that satisfies the search conditions, they may search any named additional locations. Always reshuffle a deck after it is searched." So the next place is
    // opened (and its cards shown) only when the current one held no qualifying card and the searcher goes on; a deck that was searched is reshuffled, found or not, as the search leaves it.
    bool Ordered = false,
    // Asahina Kitiaru - exp (issue #679, Mendon's ruling): "Show it and put it in your hand. Discard a card." -- the card just found MAY be the card discarded (a ToHandThenDiscard search otherwise excludes it, issue #368).
    bool DiscardMayBeFound = false,
    // P20 (SearchExtensions.cs, issue #728). ChooseOne: the searcher picks the source (the default of a spec with several; Ordered still forces the first, R-011). SelectCount: take between Min and Max cards
    // (null: exactly one, as always). OtherPlayerChooses: after the picks are shown the OTHER player chooses one of them.
    bool ChooseOne = false, SearchSelectCount? SelectCount = null, SearchOtherChoice? OtherPlayerChooses = null);

public static class SearchCatalog
{
    /// <summary>Ikoma Shizuka (oracle-l5r-10292): "Search your Dynasty or Fate deck. If you find a Region, attach it
    /// to one of your provinces, paying all costs. If you find a Terrain, put it in your hand and discard a
    /// card." A Terrain is a Strategy carrying the Terrain keyword.</summary>
    public static SearchSpec IkomaShizuka { get; } = new("ikoma-shizuka", "Search your Dynasty or Fate deck",
    [
        new(SearchDeck.Dynasty, new("Region", [CardType.Region]), SearchEffect.AttachRegion),
        new(SearchDeck.Fate, new("Terrain", [CardType.Strategy], ["Terrain"]), SearchEffect.ToHandThenDiscard)
    ], DiscardMayBeFound: true); // Mendon, issue #757: the Terrain just found may be the card discarded (reverses #368).

    /// <summary>Imperial Gift (oracle-l5r-3694): "Gain 2 Honor and search your Fate deck for an Item, show it, and
    /// put it into your hand." The Honor is the Event's own step; this is the search half.</summary>
    public static SearchSpec ImperialGift { get; } = new("imperial-gift", "Search your Fate deck for an Item",
        [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)]);

    // Training fixtures (never in a playable deck): they exercise the criteria vocabulary and the optional flag
    // ahead of any published card needing them.
    /// <summary>Fate deck, a non-Unique Follower or Item that costs 2 Gold or less: Types + NonUnique + a GoldCost comparison.</summary>
    public static SearchSpec TrainingCheap { get; } = new("training-cheap", "Search your Fate deck for a cheap non-Unique Follower or Item",
        [new(SearchDeck.Fate, new("cheap non-Unique Follower or Item", [CardType.Follower, CardType.Item], NonUnique: true,
            Comparisons: [new("GoldCost", "<=", 2)]), SearchEffect.ToHand)]);
    /// <summary>Fate deck, a Strategy with Focus Value 2 or less: a Focus comparison.</summary>
    public static SearchSpec TrainingFocus { get; } = new("training-focus", "Search your Fate deck for a low-Focus Strategy",
        [new(SearchDeck.Fate, new("Strategy with Focus 2 or less", [CardType.Strategy], Comparisons: [new("Focus", "<=", 2)]), SearchEffect.ToHand)]);
    /// <summary>Fate deck, a Weapon Item, and the player MAY decline: the printed "you may search" shape.</summary>
    public static SearchSpec TrainingOptional { get; } = new("training-optional", "You may search your Fate deck for a Weapon",
        [new(SearchDeck.Fate, new("Weapon", [CardType.Item], ["Weapon"]), SearchEffect.ToHand)], Optional: true);

    /// <summary>Naka Mahatsu - exp (oracle-l5r-10093, Seeds of Decay, issue #453): "Search your Fate deck for a card with Thunder or an element keyword. Show it. Put it in your hand."</summary>
    public static SearchSpec NakaMahatsu { get; } = new("naka-mahatsu", "Search your Fate deck for a card with Thunder or an element keyword",
        [new(SearchDeck.Fate, new("card with Thunder or an element keyword", AnyKeywords: ["Thunder", "Air", "Earth", "Fire", "Water", "Void"]), SearchEffect.ToHand)]);

    /// <summary>Discovering the Temple (oracle-l5r-9791, an Event; issue #476 batch 6): "Search your Fate deck or discard pile for a card titled Blood of the Preserver,
    /// show it, and put it into your hand." The searcher picks the deck or the discard pile.</summary>
    public static SearchSpec DiscoveringTheTemple { get; } = new("discovering-the-temple", "Search your Fate deck or discard pile for Blood of the Preserver",
    [
        new(SearchDeck.Fate, new("card titled Blood of the Preserver", Titles: ["Blood of the Preserver"]), SearchEffect.ToHand),
        new(SearchDeck.FateDiscard, new("card titled Blood of the Preserver", Titles: ["Blood of the Preserver"]), SearchEffect.ToHand)
    ]);

    /// <summary>Versatile Farm (oracle-l5r-9801, issue #476 batch 6): "Search your Dynasty deck for a non-Unique Farm Holding with another title and put it into play,
    /// unbowed, paying 2 less Gold."</summary>
    public static SearchSpec VersatileFarm { get; } = new("versatile-farm", "Search your Dynasty deck for a non-Unique Farm Holding with another title",
        [new(SearchDeck.Dynasty, new("non-Unique Farm Holding with another title", [CardType.Holding], ["Farm"], NonUnique: true, ExcludedTitles: ["Versatile Farm"]),
            SearchEffect.RecruitUnbowed)], RecruitDiscount: 2);

    /// <summary>Yung (oracle-l5r-9697, Emperor Edition Personality pass, issue #477 batch 6): "Search your Fate deck for a Ring, show it, and put it in your hand."</summary>
    public static SearchSpec YungRing { get; } = new("yung-ring", "Search your Fate deck for a Ring",
        [new(SearchDeck.Fate, new("Ring", [CardType.Ring]), SearchEffect.ToHand)]);

    /// <summary>Introspection (oracle-l5r-3793, Emperor Edition remaining-cards pass, issue #492, group B): "Search your Fate deck for a card. Put it in your hand." Nothing is said to show it, so it is
    /// put into the hand without being shown; a search's own record still shows the card (the one flow every search shares).</summary>
    public static SearchSpec Introspection { get; } = new("introspection", "Search your Fate deck for a card",
        [new(SearchDeck.Fate, new("card"), SearchEffect.ToHand)]);

    /// <summary>Martial Instruction (oracle-l5r-4843): "Search your Fate deck for a Kiho Battle Strategy, show it, and put it in your hand."</summary>
    public static SearchSpec MartialInstruction { get; } = new("martial-instruction", "Search your Fate deck for a Kiho Battle Strategy",
        [new(SearchDeck.Fate, new("Kiho Battle Strategy", [CardType.Strategy], ["Kiho"], BattleStrategy: true), SearchEffect.ToHand)]);

    /// <summary>Meeting the Keepers (oracle-l5r-5018): "Search your Dynasty deck for an Event, show it, and put it on top of the deck after shuffling."</summary>
    public static SearchSpec MeetingTheKeepers { get; } = new("meeting-the-keepers", "Search your Dynasty deck for an Event",
        [new(SearchDeck.Dynasty, new("Event", [CardType.Event]), SearchEffect.PutOnTopAfterShuffle)]);

    /// <summary>Sacrifice of Pawns (oracle-l5r-6439, issue #492): "search your Dynasty deck for a Personality and bring him into play, paying all costs but paying two more Gold."</summary>
    public static SearchSpec SacrificeOfPawns { get; } = new("sacrifice-of-pawns", "Search your Dynasty deck for a Personality",
        [new(SearchDeck.Dynasty, new("Personality", [CardType.Personality]), SearchEffect.RecruitPersonality)]);

    public static IReadOnlyDictionary<string, SearchSpec> All { get; } = EngineHooks.Catalogue(new[] { IkomaShizuka, ImperialGift, TrainingCheap, TrainingFocus, TrainingOptional, NakaMahatsu, YungRing,
            DiscoveringTheTemple, VersatileFarm, Introspection, MartialInstruction, MeetingTheKeepers, SacrificeOfPawns, EmperorRemainingFSearches.RefugeOfTheThreeSisters }, EngineHooks.Searches, static entry => entry.Id);

    /// <summary>The search an Event carries in its Effect (an Event has no abilities of its own to hang a spec on).</summary>
    public static SearchSpec? ForEventEffect(string effect) => effect == "imperial-gift" ? ImperialGift : null;
}

/// <summary>Engine-private context of the search in progress (MatchState.PendingSearch). Never sent to a client
/// as-is: the searcher's MatchView carries a SearchView derived from it, everyone else gets nothing.</summary>
public sealed class PendingSearch
{
    public string SearchId { get; set; } = "";
    /// <summary>The action (the command) whose effects started this search, so the Honor it changes when the search ends is still "from an action's effects" (Mendon's R4 ruling, EmperorRulingR4Honor.cs).</summary>
    public GameCommand? Command { get; set; }
    /// <summary>The card whose text started the search (Ikoma Shizuka, or the revealed Event).</summary>
    public string SourceCardId { get; set; } = "";
    public int PlayerId { get; set; }
    /// <summary>A <see cref="SearchStage"/> value.</summary>
    public string Stage { get; set; } = SearchStage.Select;
    /// <summary>The <see cref="SearchDeck"/> being searched; null while the deck is still to be chosen.</summary>
    public string? Deck { get; set; }
    /// <summary>The instance picked from the deck once one has been selected. Still physically in the deck list
    /// while a follow-up choice (a Region's province) is pending, so the deck and the card zones never disagree.</summary>
    public string? SelectedCardId { get; set; }
    /// <summary>Meeting the Keepers (issue #492): the card put on top of the searched deck once it has been shuffled.</summary>
    public string? PutOnTopId { get; set; }
    /// <summary>"ability" (a Limited action: the action resolves afterwards) or "event" (an Events Phase reveal:
    /// the Event is discarded and the reveal loop resumes afterwards).</summary>
    public string Origin { get; set; } = "ability";
    /// <summary>Ability origin only: every card that was already bowed when the action was announced, so that once the
    /// search has finished and the action has resolved, "after your action bowed a Personality" Reactions (Tamori Ruya,
    /// Togashi Hizumi) see exactly the Personalities this action bowed (Shizuka's own cost) -- the check the tail of an
    /// ordinary action does, which a deferred action would otherwise skip. Null when none was captured.</summary>
    public List<string>? BowedBefore { get; set; }
    // Emperor Edition remaining-cards pass (issue #492), group E (EmperorRemainingE.cs). GoldLimit: "with Gold Cost lower than or equal to ..." where the number is read from the action (I Give You My Name);
    // HonorLossOnFinish: "Lose 2 Honor" after the search whether or not a card was found; EnteredCardId: the Personality the search put into play, whose entering-play window opens as the search ends.
    public int? GoldLimit { get; set; }
    public int HonorLossOnFinish { get; set; }
    public string? EnteredCardId { get; set; }
    /// <summary>Emperor Edition remaining-cards pass (issue #492), group F (Refuge of the Three Sisters: "a card with a lower Focus Value than your Personality's Chi"): a card qualifies only with a
    /// printed Focus Value below this, read off the Personality when the action was taken. Null for every other search.</summary>
    public int? FocusBelow { get; set; }
    /// <summary>Emperor Edition follow-ups (issue #498): an "entry" search (an "After this card enters play" trait queued behind the entry windows) hands priority back to this player when it ends.</summary>
    public int? ResumeTo { get; set; }
    /// <summary>Issue #503 (Delving into History): a card qualifies only if it shares one of these keywords (the performer's); null for no such condition.</summary>
    public string[]? SharedKeywords { get; set; }
    // P20 (SearchExtensions.cs, issue #728). All omitted from a saved game until a search uses them; 22800-22899 is reserved for P20.
    /// <summary>The instances taken by a multi-card selection, in the order picked (every one is shown first).</summary>
    [JsonPropertyOrder(22800)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PickedCardIds { get; set; }
    /// <summary>ShownCard.Sequence of each picked card, parallel to <see cref="PickedCardIds"/>: what the other player chooses among.</summary>
    [JsonPropertyOrder(22801)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? ShownSequences { get; set; }
    /// <summary>The cards put on the bottom of the searched deck once it has been shuffled (SearchEffect.Bottom).</summary>
    [JsonPropertyOrder(22802)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PutOnBottomIds { get; set; }
}

/// <summary>One entry of the public, append-only record of cards shown to the other player. Deliberately holds
/// the definition id and NOT the card's instance id.</summary>
public sealed record ShownCard(int Sequence, int PlayerId, string DefinitionId, string SourceCardId, string SourceDefinitionId,
    string Deck, string Context, int TurnNumber, long EventSequence);

/// <summary>Public: what one player showed, for both players' MatchView.ShownCards. <paramref name="Sequence"/> is
/// the record's own per-match key (1, 2, 3, ...); <paramref name="EventSequence"/> is the sequence number of the
/// public "cardShown" event, so a client can line the record up with the event log. There is no instance id.</summary>
public sealed record ShownCardView(int Sequence, int ShownByPlayerId, CardDefinition Definition, string? SourceCardId,
    string SourceName, string Deck, string Context, int TurnNumber, long EventSequence);

/// <summary>One deck a Shizuka-style card lets the searcher pick. The deck size is hidden information and is never sent;
/// no contents are revealed until a deck is chosen.</summary>
public sealed record SearchDeckChoiceView(string Deck, string Label, string Criteria);

/// <summary>One line of the searcher's deck viewer: a distinct card, how many copies remain, and whether it may be
/// chosen (with a short reason when it may not). Grouped, sorted by type then name -- never deck order.</summary>
public sealed record SearchEntryView(string DefinitionId, CardDefinition Definition, int Count, bool Eligible, string? Reason);

/// <summary>PRIVATE to the searching player (null in everyone else's view). Stage says which decision is open:
/// ChooseDeck (DeckChoices), Select (Entries; CanPass is true only when nothing qualifies, or the search is
/// Optional), ChooseProvince / ChooseDiscard (Selected is the shown card; the choices are ordinary actions).</summary>
public sealed record SearchView(string Stage, string SourceCardId, string SourceName, string Label, string Prompt,
    string? Deck, string? Criteria, SearchDeckChoiceView[]? DeckChoices, SearchEntryView[]? Entries,
    bool Optional, bool CanPass, CardDefinition? Selected,
    // P20 (SearchExtensions.cs): how many cards the searcher takes (a selection of several), whether their titles must differ, and the name of a place that is not a deck. Omitted from the view for a classic search.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MinSelect = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxSelect = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? DifferentTitles = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Place = null);

public sealed partial class MatchEngine
{
    /// <summary>The Choice of a "passSearch" that ends an ordered search where no card qualified, instead of going on to the next place.</summary>
    public const string SearchStopChoice = "stop";

    private static bool IsSearchChoice(string type) => type is "searchDeck" or "searchSelect" or "searchRegionProvince" or "searchDiscard" or "searchRecruitPay" or "searchDestination" or "searchAttachTarget";

    private sealed record SearchGroup(string DefinitionId, CardInstance Card, int Count, bool Eligible, string? Reason);

    private static string Article(string noun) => noun.Length > 0 && "AEIOUaeiou".Contains(noun[0]) ? $"an {noun}" : $"a {noun}";

    // A discard pile is not a list of its own (its cards are known by their zone): the searched "deck" is then a fresh list of the pile's cards, and taking a card from it
    // is its change of zone (removing it from this copy is harmless).
    private List<string> SearchDeckList(PlayerState player, string deck) => deck switch
    {
        SearchDeck.Dynasty => player.DynastyDeck,
        SearchDeck.FateDiscard => _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.FateDiscard).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList(),
        SearchDeck.DynastyDiscard => _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone is Zone.DynastyDiscard or Zone.Dead).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList(),
        SearchDeck.Hand => player.Hand.ToList(), // issue #498: a copy, like a discard pile's -- a card leaves the hand by changing zone
        SearchDeck.OwnProvinces or SearchDeck.AnyPlayerFateDiscard or SearchDeck.AnyPlayerDynastyDiscard => SearchExtList(player, deck), // P20 (SearchExtensions.cs): copies, like a discard pile's
        _ => player.FateDeck
    };

    // "your Fate deck" / "your Fate discard pile": how a searched place is named in prompts and the log.
    private static string SearchPlace(string deck) => deck == SearchDeck.FateDiscard ? "Fate discard pile" : deck == SearchDeck.DynastyDiscard ? "Dynasty discard pile" : deck == SearchDeck.Hand ? "hand"
        : SearchExtPlace(deck) ?? $"{deck} deck";

    private SearchSpec SearchSpecOf(PendingSearch search) => SearchCatalog.All.TryGetValue(search.SearchId, out var spec) ? spec
        : throw new RuleViolation("Unknown search.");

    private SearchDeckOption SearchOptionOf(PendingSearch search) =>
        SearchSpecOf(search).Decks.Single(d => d.Deck == search.Deck);

    // ---- the flow ----

    /// <summary>Starts a search for <paramref name="playerId"/> on behalf of <paramref name="source"/>. Opens the deck
    /// choice when the spec offers more than one deck, otherwise goes straight to the candidate list.</summary>
    private void BeginSearch(SearchSpec spec, CardInstance source, int playerId, string origin, IEnumerable<string>? bowedBefore = null)
    {
        var player = Player(playerId);
        ValidateSearchSpec(spec); // P20 (SearchExtensions.cs): a spec combining things the flow cannot do is refused before anything changes
        var search = new PendingSearch { SearchId = spec.Id, SourceCardId = source.Id, PlayerId = playerId, Origin = origin, BowedBefore = bowedBefore?.ToList(), Command = _runningCommand };
        _state.PendingSearch = search;
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
        Emit("searchStarted", $"{player.Name} searches with {Definition(source).Name}.", playerId, source.Id);
        if (spec.Decks.Length > 1 && !spec.Ordered)
        {
            search.Stage = SearchStage.ChooseDeck;
            _state.PendingChoice = new("searchDeck", playerId, source.Id, $"{Definition(source).Name}: choose which deck to search.");
            return;
        }
        OpenSearchSelection(search, spec.Decks[0]);
    }

    private void OpenSearchSelection(PendingSearch search, SearchDeckOption option)
    {
        var player = Player(search.PlayerId); var source = Card(search.SourceCardId);
        search.Deck = option.Deck; search.Stage = SearchStage.Select;
        _state.PendingChoice = new("searchSelect", search.PlayerId, source.Id,
            $"{Definition(source).Name}: choose {Article(option.Criteria.Noun)} from your {SearchPlace(option.Deck)}, or pass if none qualifies.");
        Emit("deckSearched", $"{player.Name} searches their {SearchPlace(option.Deck)}.", player.Id);
    }

    private void ResolveSearchCommand(GameCommand command)
    {
        var search = _state.PendingSearch ?? throw new RuleViolation("No search is in progress.");
        Require(command.PlayerId == search.PlayerId, "Only the searching player may answer this search.");
        switch (search.Stage)
        {
            case SearchStage.ChooseDeck: ChooseSearchDeck(search, command); break;
            case SearchStage.Select: SelectOrPassSearch(search, command); break;
            case SearchStage.ChooseProvince: ChooseSearchRegionProvince(search, command); break;
            case SearchStage.ChooseDiscard: ChooseSearchDiscard(search, command); break;
            case SearchStage.ChoosePayment: ChooseSearchRecruitPayment(search, command); break; // ShadowsEmbraceBatch6.cs
            case SearchStage.ChooseDestination: ChooseSearchDestination(search, command); break; // EmperorFollowupsSearch.cs (issue #498)
            case SearchStage.ChooseAttachTarget: ChooseSearchAttachTarget(search, command); break; // SearchExtensions.cs (P20)
            default: throw new RuleViolation("Unknown search stage.");
        }
    }

    private void ChooseSearchDeck(PendingSearch search, GameCommand command)
    {
        Require(command.Type == "chooseSearchDeck", "Choose which deck to search.");
        var option = SearchSpecOf(search).Decks.FirstOrDefault(d => d.Deck == command.Choice);
        Require(option is not null, "Choose the Dynasty or Fate deck.");
        OpenSearchSelection(search, option!);
    }

    private void SelectOrPassSearch(PendingSearch search, GameCommand command)
    {
        var spec = SearchSpecOf(search); var option = SearchOptionOf(search);
        var groups = SearchGroups(search);
        var player = Player(search.PlayerId);
        if (command.Type == "passSearch")
        {
            var anyEligible = groups.Any(g => g.Eligible);
            // Pass is legal ONLY when nothing qualifies (or the printed text says "you may search"): a player
            // cannot skip a card they are obliged to take -- in an ordered search ("your discard pile, then Fate deck") too: he takes it from the first place found (Mendon's R4 ruling).
            var nextPlace = NextOrderedSearchPlace(spec, option);
            Require(!anyEligible || spec.Optional || SearchMayTakeNone(spec), "A card in the deck qualifies, so you must choose one.");
            // Nothing qualified here: he may search the next named place (the default of a pass), or stop -- "stop" is the pass's Choice.
            if (!anyEligible && nextPlace is not null && command.Choice != SearchStopChoice)
            {
                Emit("searchPassed", $"{player.Name} found no eligible card in their {SearchPlace(search.Deck!)}.", player.Id);
                ShuffleDeck(player, search.Deck!); // a deck that was searched is reshuffled (a discard pile is not) before the next place is opened
                OpenSearchSelection(search, nextPlace);
                return;
            }
            Emit("searchPassed", anyEligible
                ? $"{player.Name} chose not to take a card from their {SearchPlace(search.Deck!)}."
                : $"{player.Name} found no eligible card in their {SearchPlace(search.Deck!)}.", player.Id);
            FinishSearch(search);
            return;
        }
        if (spec.SelectCount is not null) { SelectManySearch(search, spec, option, groups, command); return; } // P20 (SearchExtensions.cs): a selection of several cards
        Require(command.Type == "selectSearchCard" && !string.IsNullOrEmpty(command.Choice), "Choose a card from the deck list.");
        var group = groups.FirstOrDefault(g => g.DefinitionId == command.Choice);
        Require(group is not null, "That card is not in the deck being searched.");
        Require(group!.Eligible, group.Reason ?? "That card does not meet this search's criteria.");
        var card = group.Card;
        search.SelectedCardId = card.Id;
        // 1. SHOW: the opponent sees the card before anything is done with it.
        ShowSearchedCard(search, card);
        // 2. APPLY the card's own effect to it (3. the shuffle follows in FinishSearch, once the effect is done).
        switch (option.Effect)
        {
            case SearchEffect.ToHand:
                MoveSearchedCardToHand(search, card);
                FinishSearch(search);
                break;
            case SearchEffect.ToHandThenDiscard:
                MoveSearchedCardToHand(search, card);
                // Mendon's ruling (issue #368, reversed for Ikoma Shizuka in #757; a spec's DiscardMayBeFound lifts it): the card just found cannot be the card discarded to its own effect (it is not
                // "a card in your hand" for this purpose). When no OTHER card is in the hand the discard cannot be satisfied: nothing is
                // discarded and there is no error -- the search simply carries on to its shuffle.
                if (SearchDiscardCandidates(search).Length == 0)
                {
                    Emit("searchDiscardSkipped", $"{player.Name} has no other card in hand to discard.", player.Id);
                    FinishSearch(search);
                    break;
                }
                search.Stage = SearchStage.ChooseDiscard;
                _state.PendingChoice = new("searchDiscard", search.PlayerId, search.SourceCardId, $"{Definition(Card(search.SourceCardId)).Name}: discard a card from your hand{(SearchCatalog.All.TryGetValue(search.SearchId, out var kept) && kept.DiscardMayBeFound ? "." : " other than the card you found.")}");
                break;
            case SearchEffect.AttachRegion:
                BeginRegionAttach(search, card);
                break;
            case SearchEffect.RecruitUnbowed:
                BeginSearchRecruit(search, card); // ShadowsEmbraceBatch6.cs
                break;
            case SearchEffect.PutOnTopAfterShuffle:
                search.PutOnTopId = card.Id;
                FinishSearch(search);
                break;
            case SearchEffect.RecruitPersonality:
                BeginPersonalityRecruit(search, card); // EmperorRemainingC.cs
                break;
            // Emperor Edition remaining-cards pass (issue #492), group E (EmperorRemainingE.cs).
            case SearchEffect.PutIntoPlay:
                EnterSearchedPersonality(search, card);
                FinishSearch(search);
                break;
            case SearchEffect.ToProvinceFaceUp:
                BeginSearchProvincePlacement(search, card);
                break;
            // Emperor Edition follow-ups (issue #498): "Put it into play or your hand" (EmperorFollowupsSearch.cs).
            case SearchEffect.RingIntoPlayOrHand:
                BeginSearchDestination(search, card);
                break;
            case SearchEffect.RefillSourceProvinceFaceUp: // issue #503 (EmperorP6.cs)
                HoldSearchedRefill(search, card);
                FinishSearch(search);
                break;
            default:
                if (!SearchPlayApplyEffect(search, card, option.Effect) && !WlItemsMiscApplySearchEffect(search, card, option.Effect) && !SearchExtApplyEffect(search, card, option.Effect) && !Wave739ApplySearchEffect(search, card, option.Effect)) throw new RuleViolation("Unknown search effect."); // issue #667 (SearchPlayRosary.cs): a Celestial put into play with no province; issue #674 (WlItemsMiscEvents.cs): Changing the Game's removal for Honor
                break;
        }
    }

    private void ShowSearchedCard(PendingSearch search, CardInstance card) => RecordShown(search.PlayerId, card, Card(search.SourceCardId), search.Deck!, "search");

    /// <summary>Appends a card to the public shown-cards record (the opponent reviews it from MatchView.ShownCards) and
    /// announces it. <paramref name="how"/> completes the record's context ("search", "look").</summary>
    private void RecordShown(int playerId, CardInstance card, CardInstance source, string deck, string how)
    {
        var player = Player(playerId);
        var context = $"{Definition(source).Name}, {SearchPlace(deck)} {how}";
        var eventSequence = _state.Events.Count + 1L;
        _state.ShownCards.Add(new ShownCard(_state.ShownCards.Count + 1, player.Id, card.DefinitionId, source.Id, source.DefinitionId, deck, context, _state.TurnNumber, eventSequence));
        // The event carries no card id: the shown card may end up in a hidden hand, and the record is keyed by sequence.
        Emit("cardShown", $"{player.Name} shows {Definition(card).Name} ({context}).", player.Id);
    }

    private void MoveSearchedCardToHand(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        if (search.Deck == SearchDeck.Hand) { Emit("searchToHand", $"{player.Name} kept {Definition(card).Name} in their hand.", player.Id); return; } // issue #498: it is already there
        if (P2CardIntoHandBlocked(player.Id)) return; // issue #503: The Scorpion Wall is Finished -- it stays in the searched deck
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        card.Zone = Zone.Hand; player.Hand.Add(card.Id); NoteCardIntoHand(player.Id); // Isawa Kimi - exp 2 (issue #477, batch 5)
        Emit("searchToHand", $"{player.Name} put {Definition(card).Name} into their hand.", player.Id);
    }

    // Ikoma Shizuka's Region half. "Attach it to one of your provinces, paying all costs": a province with no Region
    // (the Region enters through the same AttachRegionToProvince the Events Phase reveal uses), the Region's own Gold
    // cost through the ONE payment path (a Fixed GoldQuote and Pay). A Region that cannot be attached -- every
    // province already has one, or its cost cannot be paid -- is discarded, like any Region that fails to attach.
    private void BeginRegionAttach(PendingSearch search, CardInstance region)
    {
        var player = Player(search.PlayerId);
        var free = player.Provinces.Where(p => !p.Destroyed && p.RegionId is null).ToArray();
        var quote = QuoteRegionGold(player.Id, region);
        var payable = quote is null || quote.Payments.Length > 0;
        if (free.Length == 0 || !payable)
        {
            SearchDeckList(player, search.Deck!).Remove(region.Id);
            region.Zone = Zone.DynastyDiscard; region.ProvinceIndex = null; region.FaceUp = true;
            Emit("regionDiscarded", free.Length == 0
                ? $"{Definition(region).Name} was discarded: none of {player.Name}'s provinces can take a Region."
                : $"{Definition(region).Name} was discarded: {player.Name} cannot pay its {quote!.Cost} Gold cost.", player.Id, region.Id);
            FinishSearch(search);
            return;
        }
        // Exactly one place to put it and nothing to pay: nothing to decide, so it just attaches.
        if (free.Length == 1 && quote is null)
        {
            SearchDeckList(player, search.Deck!).Remove(region.Id);
            AttachRegionToProvince(player, free[0], region);
            FinishSearch(search);
            return;
        }
        search.Stage = SearchStage.ChooseProvince;
        _state.PendingChoice = new("searchRegionProvince", search.PlayerId, search.SourceCardId,
            $"{Definition(region).Name}: choose one of your provinces without a Region to attach it to.");
    }

    private void ChooseSearchRegionProvince(PendingSearch search, GameCommand command)
    {
        if (SearchOptionOf(search).Effect == SearchEffect.ToProvinceFaceUp) { ChooseSearchProvincePlacement(search, command); return; } // issue #492, group E
        Require(command.Type == "chooseSearchProvince" && command.Province is not null, "Choose a province to attach the Region to.");
        var player = Player(search.PlayerId); var region = Card(search.SelectedCardId);
        var province = player.Provinces.FirstOrDefault(p => p.Index == command.Province);
        Require(province is not null && !province.Destroyed, "Choose one of your own provinces that has not been destroyed.");
        Require(province!.RegionId is null, "That province already has a Region attached.");
        // The one payment path: the same Gold quote the option carried, validated and charged by Pay().
        var quote = QuoteRegionGold(player.Id, region);
        if (quote is not null) Pay(player.Id, region, quote.Cost, command.SourceIds);
        else ValidatePayment(player.Id, 0, command.SourceIds);
        SearchDeckList(player, search.Deck!).Remove(region.Id);
        AttachRegionToProvince(player, province, region);
        FinishSearch(search);
    }

    private void ChooseSearchDiscard(PendingSearch search, GameCommand command)
    {
        var player = Player(search.PlayerId);
        Require(command.Type == "chooseSearchDiscard" && command.TargetId is not null && player.Hand.Contains(command.TargetId), "Choose a card from your hand to discard.");
        Require(command.TargetId != search.SelectedCardId || SearchCatalog.All.TryGetValue(search.SearchId, out var spec) && spec.DiscardMayBeFound, "The card you found cannot be the one you discard to its own effect.");
        DiscardFate(player, Card(command.TargetId));
        FinishSearch(search);
    }

    /// <summary>The hand cards that may be discarded after a Terrain is found: every card except the one just found (issue #368).</summary>
    private string[] SearchDiscardCandidates(PendingSearch search) =>
        Player(search.PlayerId).Hand.Where(id => id != search.SelectedCardId || SearchCatalog.All.TryGetValue(search.SearchId, out var spec) && spec.DiscardMayBeFound).ToArray();

    // 3. SHUFFLE, then the action resolves and play continues. Reached from every path out of a search: a card
    // taken and its effect done, a Region discarded, and a pass.
    private void FinishSearch(PendingSearch search)
    {
        TfStrategiesAFinishSearch(search); // Death of the Winds (issue #622, TfStrategiesA.cs): an additional action to play the card it found
        SearchPlayFinishSearch(search); // Gozu (issue #667, SearchPlayGozu.cs): the Terrain it found is played now (an owed additional action) or discarded
        // Emperor Edition remaining-cards pass (issue #492), group E: I Give You My Name's "Lose 2 Honor" (whether or not a card was found), then the entering-play window of a Personality it put into play.
        if (search.HonorLossOnFinish > 0) GainHonor(search.PlayerId, -search.HonorLossOnFinish, Definition(Card(search.SourceCardId)).Name, Card(search.SourceCardId));
        ShuffleDeck(Player(search.PlayerId), search.Deck!);
        if (search.PutOnTopId is { } topId)
        {
            var deck = SearchDeckList(Player(search.PlayerId), search.Deck!);
            if (deck.Remove(topId)) { deck.Insert(0, topId); Emit("searchedCardOnTop", $"{Player(search.PlayerId).Name} put the found card on top of their {SearchPlace(search.Deck!)} after shuffling.", search.PlayerId); }
        }
        SearchExtPutOnBottom(search); // P20: SearchEffect.Bottom, after the shuffle
        _state.PendingSearch = null;
        if (ContinueWisdomGained(search)) return; // issue #622 (IvorySpells.cs): the other player's search, or the Event's end.
        if (search.EnteredCardId is { } enteredId && _state.WinnerId is null)
        {
            _state.PendingChoice = null;
            if (OpenRecruitEntryWindows(Player(search.PlayerId), Card(enteredId), false, null)) return;
        }
        if (WlRingEntrySearchFinished(search)) return; // issue #699 (WlRings.cs): a Ring the entry search put into play opens "after your Ring enters play"
        // Emperor Edition follow-ups (issue #498): an "After this card enters play" search hands priority back where its entry left it (EmperorFollowupsSearch.cs).
        if (search.Origin == EntrySearchOrigin) { _state.PendingChoice = null; ResumeAfterDeferredWindow(search.ResumeTo ?? search.PlayerId); return; }
        FinishDeferredAction(search.PlayerId, search.Origin, search.SourceCardId, search.BowedBefore);
    }

    // The tail every deferred action ends with (a deck search, a look at the top cards): the pending choice is over and
    // the action that opened it now resolves like any other. A Limited action gets the ordinary tail of a resolved action --
    // a chance to react to it having bowed a Personality (Shizuka bowed herself as the cost; bowedBefore is the snapshot of
    // what was already bowed when it was announced), a deferred Overconfident/Times of Strife window, then priority to the
    // opponent (the same tail Asako Rikate's window ends with, plus the bow window it does not need). An Event is discarded,
    // its province refilled, and the Events Phase reveal resumes.
    private void FinishDeferredAction(int playerId, string origin, string sourceCardId, List<string>? bowedBefore)
    {
        _state.PendingChoice = null;
        P3AfterDeferredAction(playerId, sourceCardId); // issue #503: an action that ended in a search or a look has ended (a Reaction lockout ends)
        if (origin == "event") { ResumeEventAfterDeferral(playerId, sourceCardId); return; }
        // Issue #1294: the action has resolved, so it counts toward the Rings' entry conditions like any other (Martial Instruction is a Kiho action for Ring of Air). Its timing is the one
        // its source's actions carry; in the Battle phase only a Battle action can have been taken.
        if (_state.Cards.TryGetValue(sourceCardId, out var deferredSource))
            NoteResolvedActionCounts(Player(playerId), _state.Phase == Phase.Battle ? "Battle" : "Open", deferredSource);
        if (bowedBefore is not null && OpenBowReactionWindow(playerId, bowedBefore.ToHashSet(StringComparer.Ordinal), 1 - playerId)) return;
        if (OpenOverconfidentDrawWindow(1 - playerId)) return;
        NextAction(playerId, TfStrategiesAKeepsPriority(playerId, sourceCardId) || SearchPlayKeepsPriority(playerId)); // Death of the Winds (issue #622) and Gozu (issue #667): the player takes the additional action it granted
    }

    // An Event's own resolution once its deferred text is done (Imperial Gift's search, Signs and Portents' look): the Event
    // is discarded and the province refills, exactly as ResolveEvent's default case does, then the reveal loop resumes.
    private void ResumeEventAfterDeferral(int playerId, string sourceCardId)
    {
        var player = Player(playerId); var card = Card(sourceCardId);
        var province = player.Provinces[card.ProvinceIndex!.Value];
        province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
        Emit("eventResolved", $"{Definition(card).Name} resolved and was discarded.", player.Id, card.Id);
        if (!RefillWithSearchedSensei(player, province, card)) Refill(player, province); // In Search of Guidance (issue #503, EmperorP6.cs)
        ContinueEventsReveal(player);
    }

    /// <summary>Deterministic shuffle from the persisted generator (see MatchState.RngState): reads the current state,
    /// shuffles, and stores the advanced state, so the sequence is the same across State copies, replay and a
    /// server restart. Announces only that it happened.</summary>
    private void ShuffleDeck(PlayerState player, string deck)
    {
        if (deck is SearchDeck.FateDiscard or SearchDeck.DynastyDiscard or SearchDeck.Hand or SearchDeck.OwnProvinces or SearchDeck.AnyPlayerFateDiscard or SearchDeck.AnyPlayerDynastyDiscard) return; // a discard pile is not shuffled (issue #476 batch 6), nor is a hand (issue #498)
        var random = new StableRandom(_state.RngState);
        random.Shuffle(SearchDeckList(player, deck));
        _state.RngState = random.State;
        Emit("deckShuffled", $"{player.Name} shuffled their {deck} deck.", player.Id);
    }

    // ---- candidates and eligibility ----

    /// <summary>The searched deck's remaining cards grouped by definition (one representative instance, the lowest id,
    /// so which copy stands for the group never depends on deck order), with the engine's own eligibility verdict.
    /// Sorted by type then name then id: presentation order, never the deck's order.</summary>
    private List<SearchGroup> SearchGroups(PendingSearch search)
    {
        var option = SearchOptionOf(search);
        return SearchDeckList(Player(search.PlayerId), search.Deck!).Select(Card).GroupBy(c => c.DefinitionId, StringComparer.Ordinal)
            .Select(g =>
            {
                var representative = g.OrderBy(c => c.Id, StringComparer.Ordinal).First();
                var (eligible, reason) = SearchEligibility(option.Criteria, representative, search.GoldLimit);
                if (eligible && search.FocusBelow is int below && SearchStat(representative, "Focus") >= below) (eligible, reason) = (false, $"Focus {SearchStat(representative, "Focus")}, needs < {below}");
                (eligible, reason) = P2SearchFilter(search, representative, eligible, reason); // issue #503: Delving into History
                (eligible, reason) = SearchExtGate(search, option, representative, eligible, reason); // P20 (SearchExtensions.cs): what the disposition can do with a card from this place
                return new SearchGroup(g.Key, representative, g.Count(), eligible, reason);
            })
            .OrderBy(g => (int)Definition(g.Card).Type).ThenBy(g => Definition(g.Card).Name, StringComparer.Ordinal).ThenBy(g => g.DefinitionId, StringComparer.Ordinal).ToList();
    }

    private (bool Eligible, string? Reason) SearchEligibility(SearchCriteria criteria, CardInstance card, int? goldLimit = null)
    {
        var def = Definition(card);
        var notA = $"Not {Article(criteria.Noun)}";
        if (criteria.Types is { Length: > 0 } && !criteria.Types.Contains(def.Type)) return (false, notA);
        if (criteria.Keywords is { Length: > 0 } && !criteria.Keywords.All(k => Keyword(card, k))) return (false, notA);
        if (criteria.AnyKeywords is { Length: > 0 } && !criteria.AnyKeywords.Any(k => Keyword(card, k))) return (false, notA);
        if (criteria.BattleStrategy && !(Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var battle) && battle.Timing is "Battle" or "BattleOrOpen") && !Keyword(card, "Terrain")) return (false, notA); // a Terrain's printed "Battle:" is how it is played (issue #622)
        if (criteria.AbilityKeyword is { } abilityKeyword && !HasAbilityWithKeyword(card, abilityKeyword)) return (false, notA); // issue #503: Gaining Advantage
        if (criteria.GoldProducing && !HasGoldProducingTrait(def)) return (false, notA); // P20
        if (criteria.NonUnique && Keyword(card, "Unique")) return (false, "Unique cards cannot be chosen");
        if (criteria.ExcludedKeywords is { Length: > 0 } && criteria.ExcludedKeywords.FirstOrDefault(k => Keyword(card, k)) is string excluded)
            return (false, $"{excluded} cards cannot be chosen");
        if (criteria.Titles is { Length: > 0 } && !criteria.Titles.Contains(def.Name, StringComparer.OrdinalIgnoreCase)) return (false, notA);
        if (criteria.ExcludedTitles is { Length: > 0 } && criteria.ExcludedTitles.Contains(def.Name, StringComparer.OrdinalIgnoreCase)) return (false, $"{def.Name} cannot be chosen");
        foreach (var comparison in criteria.Comparisons ?? [])
        {
            var actual = SearchStat(card, comparison.Stat);
            var holds = comparison.Op switch
            {
                "<=" => actual <= comparison.Value,
                ">=" => actual >= comparison.Value,
                "<" => actual < comparison.Value,
                ">" => actual > comparison.Value,
                "==" => actual == comparison.Value,
                _ => throw new RuleViolation("Unknown search comparison.")
            };
            if (!holds) return (false, $"{comparison.Stat} {actual}, needs {comparison.Op} {comparison.Value}");
        }
        // The Gold Cost ceiling an action names (I Give You My Name, issue #492).
        if (goldLimit is int limit && def.GoldCost > limit) return (false, $"GoldCost {def.GoldCost}, needs <= {limit}");
        return (true, null);
    }

    private static int SearchStat(CardInstance card, string stat)
    {
        var def = Definition(card);
        return stat switch
        {
            "Focus" => def.Focus,
            "GoldCost" => def.GoldCost,
            "Force" => def.Force,
            "Chi" => def.Chi,
            "PersonalHonor" => def.PersonalHonor,
            _ => throw new RuleViolation("Unknown search statistic.")
        };
    }

    // ---- what each player is allowed to see ----

    /// <summary>The searching player's private deck viewer, or null for anyone else.</summary>
    private SearchView? BuildSearchView(int viewerId)
    {
        if (_state.PendingSearch is not { } search || search.PlayerId != viewerId || _state.PendingChoice is null) return null;
        var spec = SearchSpecOf(search); var player = Player(search.PlayerId); var source = Card(search.SourceCardId);
        var sourceName = Definition(source).Name;
        if (search.Stage == SearchStage.ChooseDeck)
            return new(search.Stage, source.Id, sourceName, spec.Label, _state.PendingChoice.Prompt, null, null,
                spec.Decks.Select(d => new SearchDeckChoiceView(d.Deck, $"Search your {SearchPlace(d.Deck)}", d.Criteria.Noun)).ToArray(), null, spec.Optional, false, null);
        var option = SearchOptionOf(search);
        var selected = search.SelectedCardId is null ? null : Copy(Definition(Card(search.SelectedCardId)));
        if (search.Stage != SearchStage.Select)
            return SearchExtView(spec, option, new(search.Stage, source.Id, sourceName, spec.Label, _state.PendingChoice.Prompt, search.Deck, option.Criteria.Noun, null, null, spec.Optional, false, selected));
        var groups = SearchGroups(search);
        return SearchExtView(spec, option, new(search.Stage, source.Id, sourceName, spec.Label, _state.PendingChoice.Prompt, search.Deck, option.Criteria.Noun, null,
            groups.Select(g => new SearchEntryView(g.DefinitionId, Copy(Definition(g.Card)), g.Count, g.Eligible, g.Reason)).ToArray(),
            spec.Optional || SearchMayTakeNone(spec), spec.Optional || SearchMayTakeNone(spec) || groups.All(g => !g.Eligible), null));
    }

    /// <summary>The public shown-cards record, identical in both players' views.</summary>
    private ShownCardView[] BuildShownCards() => _state.ShownCards.Select(s =>
        new ShownCardView(s.Sequence, s.PlayerId, Copy(CardCatalog.Get(s.DefinitionId)), s.SourceCardId, CardCatalog.Get(s.SourceDefinitionId).Name,
            s.Deck, s.Context, s.TurnNumber, s.EventSequence)).ToArray();

    // ---- the legal actions of the searching player ----

    /// <summary>The searcher's legal actions while a search is open, so a bot, the training AI or any legal-actions-driven
    /// client can always answer it and can never deadlock: the deck choice; one select action per ELIGIBLE entry, plus
    /// Pass only when nothing qualifies (or the search is Optional); a province per option (with the Gold quote and the
    /// engine's suggested payment); a hand card to discard.</summary>
    private IEnumerable<ActionOption> SearchActions(int playerId)
    {
        if (_state.PendingSearch is not { } search || search.PlayerId != playerId) yield break;
        GameCommand Command(string type, string? target = null, string? choice = null, int? province = null, string[]? sources = null) =>
            new(type, playerId, _state.Version, TargetId: target, Choice: choice, Province: province, SourceIds: sources);
        var spec = SearchSpecOf(search); var player = Player(playerId);
        switch (search.Stage)
        {
            case SearchStage.ChooseDeck:
                foreach (var d in spec.Decks)
                    yield return new("chooseSearchDeck", $"Search your {SearchPlace(d.Deck)} for {Article(d.Criteria.Noun)}", true, null, Command("chooseSearchDeck", choice: d.Deck));
                break;
            case SearchStage.Select when spec.SelectCount is not null: // P20 (SearchExtensions.cs): a selection of several cards
                foreach (var many in SearchSelectManyActions(search, spec, SearchGroups(search), playerId)) yield return many;
                break;
            case SearchStage.Select:
                var groups = SearchGroups(search);
                foreach (var g in groups.Where(g => g.Eligible))
                    yield return new("selectSearchCard", $"Take {Definition(g.Card).Name}{(g.Count > 1 ? $" ({g.Count} in the deck)" : "")}", true, null, Command("selectSearchCard", choice: g.DefinitionId));
                if (groups.All(g => !g.Eligible) && NextOrderedSearchPlace(spec, SearchOptionOf(search)) is { } nextPlace)
                {
                    // Mendon's R4 ruling: only a place with no qualifying card is left behind; the next is then searched, or the search stopped.
                    yield return new("passSearch", $"No card qualifies in the {SearchPlace(search.Deck!)}: search the {SearchPlace(nextPlace.Deck)} next", true, null, Command("passSearch"));
                    yield return new("passSearch", "No card qualifies: stop searching (the deck is still shuffled)", true, null, Command("passSearch", choice: SearchStopChoice));
                }
                else if (spec.Optional || groups.All(g => !g.Eligible))
                    yield return new("passSearch", groups.Any(g => g.Eligible) ? "Take no card (the deck is still shuffled)" : "No card qualifies: pass (the deck is still shuffled)", true, null, Command("passSearch"));
                break;
            case SearchStage.ChooseProvince when SearchOptionOf(search).Effect == SearchEffect.ToProvinceFaceUp: // issue #492, group E
                foreach (var placement in SearchProvincePlacementActions(search, playerId)) yield return placement;
                break;
            case SearchStage.ChooseProvince:
                var region = Card(search.SelectedCardId);
                var quote = QuoteRegionGold(playerId, region);
                foreach (var p in player.Provinces.Where(p => !p.Destroyed && p.RegionId is null))
                    yield return new("chooseSearchProvince", $"Attach {Definition(region).Name} to province {p.Index + 1}{(quote is null ? "" : $" · {quote.Cost} Gold")}", true, null,
                        Command("chooseSearchProvince", province: p.Index, sources: quote is null ? [] : quote.Payments.FirstOrDefault()?.SourceIds), region.Id, Provinces: [p.Index], Gold: quote);
                break;
            case SearchStage.ChooseDiscard:
                foreach (var id in SearchDiscardCandidates(search))
                    yield return new("chooseSearchDiscard", $"Discard {Definition(Card(id)).Name}", true, null, Command("chooseSearchDiscard", target: id), id);
                break;
            case SearchStage.ChoosePayment:
                foreach (var option in SearchRecruitPaymentActions(search, playerId)) yield return option; // ShadowsEmbraceBatch6.cs
                break;
            case SearchStage.ChooseDestination:
                foreach (var option in SearchDestinationActions(search, playerId)) yield return option; // EmperorFollowupsSearch.cs (issue #498)
                break;
            case SearchStage.ChooseAttachTarget:
                foreach (var option in SearchAttachActions(search, playerId)) yield return option; // SearchExtensions.cs (P20)
                break;
        }
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredSearchChoice(string type, GameCommand command)
    {
        if (!IsSearchChoice(type)) return false;
        WithResumedAction(() => ResolveSearchCommand(command));
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterDeckSearchHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredSearchChoice", 100, static (engine, type, command) => engine.HandleRegisteredSearchChoice(type, command));
    }
}
