namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>The tutorial's table (issue #935): a prepared offline match against the practice opponent, never reachable from a room or a deck.
    /// Lion (you) and Crane (the bot) on the pinned profile's decks, with the cards the lesson meets put on top in the order it needs them. The match is built the way
    /// <see cref="CreatePublished"/> builds one, but stops before the first turn: both Fate hands are empty and every province is still face down.
    /// Lion starts with more Honor (7 against 6) and goes first, as it would in a real match; Crane, the second player, starts with the bowed Bamboo Harvesters.</summary>
    internal static MatchEngine CreateTutorial(TutorialSetup setup)
    {
        const ulong seed = 935;
        var state = new MatchState { Players = [new() { Id = 0, Name = "You", Clan = setup.You.Clan }, new() { Id = 1, Name = "Opponent", Clan = setup.Foe.Clan }] };
        var nextId = 0; var ids = OpaqueCardIds(seed);
        CardInstance Add(int owner, string definition, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id = ids[nextId++], DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp, Bowed = bowed };
            state.Cards.Add(card.Id, card); return card;
        }
        var sides = new[] { setup.You, setup.Foe };
        for (var seat = 0; seat < 2; seat++)
        {
            var side = sides[seat]; var player = state.Players[seat];
            player.Honor = side.Honor ?? CardCatalog.Get(side.StrongholdId).StartingHonor;
            player.StrongholdId = Add(seat, side.StrongholdId, Zone.Stronghold, true).Id;
            foreach (var home in side.Home)
            {
                var placed = Add(seat, home.DefinitionId, Zone.Home, true, home.Bowed);
                foreach (var attachment in home.Attachments ?? []) { var worn = Add(seat, attachment, Zone.Attached, true); worn.AttachedTo = placed.Id; }
                if (home.ChiToken != 0) placed.Tokens.Add(new CardToken("token-tutorial-" + placed.Id, "Poison", "Tutorial", "Chi", home.ChiToken));
            }
            foreach (var id in TutorialCards.Deck(side.DynastyTop, side, dynasty: true)) player.DynastyDeck.Add(Add(seat, id, Zone.DynastyDeck).Id);
            foreach (var id in TutorialCards.Deck(side.FateTop, side, dynasty: false)) player.FateDeck.Add(Add(seat, id, Zone.FateDeck).Id);
            var strength = CardCatalog.Get(side.StrongholdId).ProvinceStrength;
            player.Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceState { Index = i, Strength = strength, PrintedStrength = strength }).ToList();
            foreach (var id in side.DynastyDiscard ?? []) Add(seat, id, Zone.DynastyDiscard, true);
            foreach (var (index, id) in side.Regions ?? []) { var region = Add(seat, id, Zone.Region, true); region.ProvinceIndex = index; player.Provinces[index].RegionId = region.Id; }
        }
        state.ActivePlayerId = setup.FirstPlayer; state.PriorityPlayerId = setup.FirstPlayer; state.TurnNumber = 1 + 2 * setup.TurnsTaken;
        foreach (var player in state.Players) player.TurnsTaken = setup.TurnsTaken;
        OrderCardsById(state);
        var engine = new MatchEngine(state, seed, TutorialScenario) { _tutorialNoEndDraw = setup.NoEndDraw };
        foreach (var player in state.Players) foreach (var province in player.Provinces) engine.Refill(player, province);
        engine.Emit("tutorialSetup", "Tutorial table: every province is face down and nobody has drawn Fate cards yet.");
        return engine;
    }

    public const string TutorialScenario = "tutorial-v2";

    private bool _tutorialNoEndDraw;
    private readonly HashSet<(Phase Phase, int PlayerId)> _tutorialHolds = [];
    private Action? _tutorialResume;

    /// <summary>The lesson asks the engine to stop where a phase has just been reached (Straighten after its cards straightened, Events after the provinces were revealed, End before the draw),
    /// so the note can talk about that moment. Tutorial-only; no rules change. <see cref="TutorialResume"/> carries on from there.</summary>
    internal void TutorialHoldAt(Phase phase, int playerId) => _tutorialHolds.Add((phase, playerId));
    private readonly HashSet<(int PlayerId, int Province, bool After)> _tutorialRevealHolds = [];
    /// <summary>The lesson asks the Events phase to stop at one province: <paramref name="after"/> false stops before the card is turned face up, true stops with it face up and not yet resolved
    /// (so a note can show it first). Tutorial-only; no rules change. <see cref="TutorialResume"/> carries on from there.</summary>
    internal void TutorialHoldReveal(int playerId, int province, bool after) => _tutorialRevealHolds.Add((playerId, province, after));
    private bool TutorialRevealHold(int playerId, int province, bool after, Action resume)
    {
        if (!_tutorialRevealHolds.Remove((playerId, province, after))) return false;
        _tutorialResume = resume; return true;
    }
    private readonly HashSet<(int PlayerId, int Province)> _tutorialFaceDown = [];
    /// <summary>The next Events phase leaves this province's card face down (Spells and Shugenja opens with two of your cards still hidden). Tutorial-only; no rules change.</summary>
    internal void TutorialKeepFaceDown(int playerId, int province) => _tutorialFaceDown.Add((playerId, province));
    internal bool TutorialHeld => _tutorialResume is not null;
    /// <summary>The live state, for the script's own reads (<see cref="State"/> is a full copy). Never mutate it.</summary>
    internal MatchState TutorialState => _state;
    private bool TutorialHold(Phase phase, int playerId, Action resume)
    {
        if (!_tutorialHolds.Remove((phase, playerId))) return false;
        _tutorialResume = resume; return true;
    }

    /// <summary>Starts the first turn of the paused table; it stops where a hold was asked for.</summary>
    internal void TutorialBeginTurn() => StartTurn();

    /// <summary>Carries on from a hold.</summary>
    internal void TutorialResume() { var resume = _tutorialResume; _tutorialResume = null; resume?.Invoke(); }

    /// <summary>"Pretend we forgot to draw": the tutorial's own draw of the opening hand, from a deck ordered for it.</summary>
    internal void TutorialDraw(int playerId, int count) { for (var i = 0; i < count; i++) Draw(Player(playerId)); }

    /// <summary>Read-only lookups for the tutorial script (the card behind an id, where a named card is), kept on the engine so the script never copies the whole state.</summary>
    internal string TutorialDefinitionOf(string cardId) => _state.Cards[cardId].DefinitionId;
    internal string[] TutorialCardIds(int owner, string definitionId, params Zone[] zones) =>
        _state.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definitionId && (zones.Length == 0 || zones.Contains(c.Zone))).Select(c => c.Id).ToArray();
    internal string? TutorialProvinceCardId(int owner, int index) => Player(owner).Provinces[index].CardId;
    internal string TutorialStrongholdId(int owner) => Player(owner).StrongholdId;

    private int _tutorialAdded;
    /// <summary>The lesson turns these Personalities of this player dishonorable, as an opponent's card would have (Keywords: Seppuku). Tutorial-only.</summary>
    internal void TutorialDishonor(int owner, params string[] definitionIds)
    {
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == owner && c.Zone is Zone.Home or Zone.Battlefield && definitionIds.Contains(c.DefinitionId))) card.Dishonorable = true;
    }

    /// <summary>Puts a Personality of this player into play at home, honorable, unbowed and face up, with these attachments on it; one that lies dead is brought back, otherwise a new card is made. Tutorial-only.</summary>
    internal void TutorialPutInPlay(int owner, string definitionId, params string[] attachments)
    {
        CardInstance Make(string definition, Zone zone)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id = "tutorial-added-" + _tutorialAdded++, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = true };
            _state.Cards.Add(card.Id, card); return card;
        }
        var host = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == owner && c.DefinitionId == definitionId && c.Zone == Zone.Dead) ?? Make(definitionId, Zone.Home);
        host.Zone = Zone.Home; host.FaceUp = true; host.Bowed = false; host.Dishonorable = false;
        foreach (var attachment in attachments) Make(attachment, Zone.Attached).AttachedTo = host.Id;
        Emit("tutorialPlaced", "The tutorial put a Personality into play.");
    }
}
