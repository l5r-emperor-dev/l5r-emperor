using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #699, the "rings" cluster (spell-wave-57, celestial-wave-29, WlRings.cs): how a Ring last entered play (own text or another card's effect), what that does to the Enlightenment Victory,
// the trigger "after your Ring enters play", Witness the Untold and Fudo's Assurances. Each scenario is an explicit position on the economy table (Batch336Support.Table) driven only through
// QueryActions and the commands the engine offers; rejections leave the state unchanged (Reject).
internal static class WlRingsTests
{
    private const string Witness = "oracle-l5r-10357", FudoAssurances = "oracle-l5r-10416", Yung = "oracle-l5r-9697", TogashisGuidance = "oracle-l5r-8603", DragonSword = "oracle-l5r-10112",
        StartingPath = "oracle-l5r-10032", Chukage = "oracle-l5r-533",
        RingOfAir = "oracle-l5r-6333", RingOfEarth = "oracle-l5r-6334", RingOfFire = "oracle-l5r-6335", RingOfVoid = "oracle-l5r-6338", RingOfWater = "oracle-l5r-6339",
        FalseAir = "oracle-l5r-10531", FalseEarth = "oracle-l5r-10532", FalseFire = "oracle-l5r-10533", FalseVoid = "oracle-l5r-10534", FalseWater = "oracle-l5r-10535",
        Shugenja = "training-shugenja";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Action, int actor = 0) { var s = Batch336Support.Table(phase, actor); s.PriorityPlayerId = actor; return s; }
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null) => Batch336Support.Use(e, player, ability, cardId, targetId);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static void RunUntil(MatchEngine e, Func<bool> stop) => Batch336Support.RunUntil(e, stop);
    private static RingEntryRecord? Entry(MatchEngine e, string ringId) => e.State.WlRings.Entries.TryGetValue(ringId, out var r) ? r : null;
    private static bool Marked(MatchEngine e, string ringId) => e.State.Cards[ringId].AbilityUses.ContainsKey("ee-e:no-enlightenment");

    public static void Run(TestRunner runner) => runner.Run("wl-rings", [
        ("Published shape: Witness the Untold (spell-wave-57) and Fudo's Assurances (celestial-wave-29) print their scans' stats, keywords, text and abilities and ship art and provenance", PublishedShape),
        ("Provenance: a Ring put into play by its own text (Ring of the Void's recruit, False Ring of Fire's Open) is recorded as own text, with the Ring as its source", OwnTextEntries),
        ("Provenance: Togashi's Guidance, Starting on the Path and the entry searches of Celestial Sword of the Dragon and Asako Chukage record another card's effect, naming the card", OtherCardEntries),
        ("Provenance: the record is overwritten at every entry, so a Ring that re-enters by its own text counts again", ReentryOverwrites),
        ("Brotherhood Schism / Losing the Way: an entry search may not put a second Ring into play this turn, but may still take it into the hand", SchismScenario),
        ("Enlightenment: five Rings count only while each last entered play by its own text; a Ring another card put into play, or a False Ring, does not", EnlightenmentScenario),
        ("Trigger: 'after your Ring enters play' is offered to the Ring's controller only, on every entry path, and stays optional", TriggerScenario),
        ("Witness the Untold: bows the Spell after your Ring entered play to look at the top three cards of your Dynasty or Fate deck and put them back in any order", WitnessScenario),
        ("Fudo's Assurances: straightens any Ring; destroys a Province only with five Fudo Rings that last entered play by their own text", FudoScenario),
        ("An already wired Ring reaction keeps working on the new trigger: Yung searches after your Ring enters play", YungStillWorks),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Manifest(string wave, string setName, string id)
    {
        var listed = File.ReadAllLines(Path.Combine(Batch336Support.RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(listed.Length == 1 && listed[0] == id, $"{wave} lists exactly {id}");
        Check(File.Exists(Path.Combine(Batch336Support.RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(Batch336Support.RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, 1, $"{wave} has a provenance row");
        Eq(sources[0].GetProperty("printing_set")[0].GetString(), setName, $"{wave}: reviewed printing");
    }

    private static void PublishedShape()
    {
        Manifest("spell-wave-57", "Torn Asunder", Witness);
        Manifest("celestial-wave-29", "Coils of Madness", FudoAssurances);
        var w = CardCatalog.Get(Witness);
        Check(w.Type == CardType.Spell && w.Name == "Witness the Untold" && w.GoldCost == 0 && w.Focus == 2 && w.Keywords!.SequenceEqual(["Void"]), "Witness the Untold: Void Spell, 0 Gold, Focus 2");
        Check(w.Abilities!.SequenceEqual([WlRingsAbilities.WitnessLook]) && w.Text!.StartsWith("Reaction: After your Ring enters play, bow this card"), "its one Reaction and the scan's text");
        var a = AbilityCatalog.All[WlRingsAbilities.WitnessLook];
        Check(a.Timing == "Reaction" && a.Trigger == ReactionTriggers.RingEntersPlay && a.Look == WlRingsAbilities.WitnessLookSpec, "listens to the new trigger and looks");
        var f = CardCatalog.Get(FudoAssurances);
        Check(f.Type == CardType.Celestial && f.Name == "Fudo's Assurances" && f.Keywords!.SequenceEqual(["Unique", "Fudo"]), "Fudo's Assurances: a Unique Fudo Celestial");
        Check(f.Abilities!.SequenceEqual([WlRingsAbilities.FudoStraighten, WlRingsAbilities.FudoDestroy]), "its two actions");
        Eq(AbilityCatalog.All[WlRingsAbilities.FudoStraighten].Timing, "BattleOrOpen", "Battle/Open");
        Eq(AbilityCatalog.All[WlRingsAbilities.FudoDestroy].Timing, "Open", "Open");
        foreach (var yung in new[] { "yung-search-ring", "introspection-search" }) Eq(AbilityCatalog.All[yung].Trigger, ReactionTriggers.RingEntersPlay, $"{yung} uses the shared trigger");
    }

    // ---- provenance -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OwnTextEntries()
    {
        var s = Table(Phase.Action, 0);
        var ring = Add(s, RingOfVoid, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Entry(e, ring.Id) is null, "no record before it enters play");
        e = Batch336Support.GrantVoid(e, 0, ring.Id); Accept(e, Use(e, 0, "ring-of-void-recruit", ring.Id));
        Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "in play");
        var record = Entry(e, ring.Id)!;
        Check(record.Mode == WlRingsState.OwnText && record.SourceCardId == ring.Id && record.AbilityId == "ring-of-void-recruit" && record.Turn == e.State.TurnNumber, "own text, its own recruit, this turn");
        Check(!Marked(e, ring.Id), "no Enlightenment mark");
        // False Ring of Fire's own Open: also its own text (and its Fudo keyword is what keeps it from the Enlightenment count).
        var t = Table(Phase.Action, 0);
        var fire = Add(t, FalseFire, 0, Zone.Hand); t.Players[0].MaxHandBonus = 2;
        var fe = MatchEngine.FromTestState(t);
        Accept(fe, Enabled(fe, 0, WlSpellsRingsAbilities.FalseFireRecruit, fire.Id).Command!);
        Check(Entry(fe, fire.Id) is { Mode: WlRingsState.OwnText, AbilityId: WlSpellsRingsAbilities.FalseFireRecruit }, "False Ring of Fire: own text");
        // A rejected recruit leaves no record.
        var u = Table(Phase.Action, 0);
        var cold = Add(u, FalseFire, 0, Zone.Hand);
        var ue = MatchEngine.FromTestState(u);
        Reject(ue, Use(ue, 0, WlSpellsRingsAbilities.FalseFireRecruit, cold.Id));
        Check(Entry(ue, cold.Id) is null, "nothing recorded for a refused recruit");
    }

    // Player abilities (Starting on the Path's granted action) are enabled only with a published Stronghold (the economy fixture's are scenario-only).
    private static MatchState Published(MatchState s) { foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }
    private static GameCommand StartingPathOffer(MatchEngine e, CardInstance ring) =>
        e.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "p5-starting-path-ring" && a.Enabled).Command! with { TargetId = ring.Id };

    private static MatchEngine TogashiRig(out CardInstance togashi, out CardInstance ring, Action<MatchState>? more = null)
    {
        var s = Table(Phase.Action, 0); s.Players[0].Clan = "Dragon";
        togashi = Add(s, TogashisGuidance, 0, Zone.Celestial);
        ring = Add(s, RingOfAir, 0, Zone.Hand);
        more?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void OtherCardEntries()
    {
        // Togashi's Guidance.
        var e = TogashiRig(out var togashi, out var ring);
        Accept(e, Offer(e, 0, "togashis-guidance-ring-play", togashi.Id, ring.Id));
        Check(Entry(e, ring.Id) is { Mode: WlRingsState.OtherCard, SourceCardId: { } by, AbilityId: "togashis-guidance-ring-play" } && by == togashi.Id, "Togashi's Guidance: another card's effect, naming the Celestial and its ability");
        Check(Marked(e, ring.Id), "and the older mark follows");
        // Starting on the Path.
        var s = Published(Table(Phase.Action, 0));
        var ringP = Add(s, RingOfAir, 0, Zone.Hand);
        var path = Add(s, StartingPath, 0, Zone.EventInPlay); path.ExpiresAtTurnsTaken = s.Players[0].TurnsTaken + 2;
        var pe = MatchEngine.FromTestState(s);
        Accept(pe, StartingPathOffer(pe, ringP));
        Check(Entry(pe, ringP.Id) is { Mode: WlRingsState.OtherCard, AbilityId: "p5-starting-path-ring" }, "Starting on the Path: another card's effect");
        // Celestial Sword of the Dragon's search and Asako Chukage's: entry searches, which enter outside any action.
        var (sword, swordRing) = SwordSearch(out var swordCard);
        Check(Entry(sword, swordRing) is { Mode: WlRingsState.OtherCard } r1 && r1.SourceCardId == swordCard, "Celestial Sword of the Dragon's search: another card's effect, naming the Sword");
        Check(sword.State.WlRings.SearchEnteredRingId is null, "the search's hand-over is cleared once the window decision is made");
        var (chukage, chukageRing, chukageId) = ChukageSearch();
        Check(Entry(chukage, chukageRing) is { Mode: WlRingsState.OtherCard } r2 && r2.SourceCardId == chukageId, "Asako Chukage's search: another card's effect, naming Chukage");
    }

    // A Dragon Clan Personality equips Celestial Sword of the Dragon; its entry search takes a Ring of Air from the discard pile into play. Returns the engine, the Ring's id and the Sword's id.
    private static (MatchEngine, string) SwordSearch(out string swordId, Action<MatchState>? more = null)
    {
        var s = Table(Phase.Action, 0); s.Players[0].Clan = "Dragon";
        var dragon = Add(s, "training-tattooed", 0, Zone.Home); dragon.GrantedKeywords.Add(new("Dragon Clan", "test-fixture", GrantDuration.Permanent));
        var sword = Add(s, DragonSword, 0, Zone.Hand); swordId = sword.Id;
        var inDiscard = Add(s, RingOfAir, 0, Zone.FateDiscard);
        more?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, CardCatalog.Get(DragonSword).GoldCost).First().SourceIds;
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: sword.Id, SourceIds: payment, TargetId: dragon.Id));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfAir));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseSearchDestination" && a.Command!.Choice == "play").Command!);
        return (e, inDiscard.Id);
    }

    // Asako Chukage is purchased; his entry search finds a Ring of Water in the hand and puts it into play.
    private static (MatchEngine, string, string) ChukageSearch(Action<MatchState>? more = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = Chukage; s.Cards[cardId].FaceUp = true;
        var inHand = Add(s, RingOfWater, 0, Zone.Hand);
        more?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(Chukage).GoldCost).First().SourceIds));
        for (var guard = 0; e.State.PendingChoice is { Type: "proclaim" or "entryReaction" } && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, e.QueryActions(0).First(a => a.Type == "passSearch" && a.Command!.Choice is null).Command!); // the discard pile holds no Ring of Water
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfWater));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseSearchDestination" && a.Command!.Choice == "play").Command!);
        return (e, inHand.Id, cardId);
    }

    private static void ReentryOverwrites()
    {
        var e = TogashiRig(out var togashi, out var ring);
        Accept(e, Offer(e, 0, "togashis-guidance-ring-play", togashi.Id, ring.Id));
        Check(Entry(e, ring.Id)!.Mode == WlRingsState.OtherCard, "first entry: another card's effect");
        // The Ring is returned to the hand (Tomb of Sun Tao's shape) and recruited by its own text on a later turn.
        var s = e.State;
        var back = s.Cards[ring.Id]; back.DefinitionId = RingOfVoid; back.Zone = Zone.Hand; s.Players[0].Hand.Add(back.Id); s.Players[0].RingPutIntoPlayTurn = -1; s.TurnNumber += 2; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var again = MatchEngine.FromTestState(s);
        again = Batch336Support.GrantVoid(again, 0, ring.Id); Accept(again, Use(again, 0, "ring-of-void-recruit", ring.Id));
        var record = Entry(again, ring.Id)!;
        Check(record.Mode == WlRingsState.OwnText && record.SourceCardId == ring.Id && record.Turn == again.State.TurnNumber, "second entry: its own text, this turn");
        Check(!Marked(again, ring.Id), "the older mark is cleared as well");
        // And the other way round: a Ring recorded as own text, then put into play by another card, loses the standing.
        var t = TogashiRig(out var togashi2, out var ring2, st => { });
        var st2 = t.State; st2.WlRings.Entries[ring2.Id] = new RingEntryRecord(WlRingsState.OwnText, ring2.Id, "ring-of-void-recruit", 1);
        var t2 = MatchEngine.FromTestState(st2);
        Accept(t2, Offer(t2, 0, "togashis-guidance-ring-play", togashi2.Id, ring2.Id));
        Eq(Entry(t2, ring2.Id)!.Mode, WlRingsState.OtherCard, "overwritten by another card's effect");
        Eq(t2.State.WlRings.Entries.Count, 1, "one record per Ring");
    }

    private static void SchismScenario()
    {
        var s = Table(Phase.Action, 0); s.Players[0].Clan = "Dragon";
        var dragon = Add(s, "training-tattooed", 0, Zone.Home); dragon.GrantedKeywords.Add(new("Dragon Clan", "test-fixture", GrantDuration.Permanent));
        var sword = Add(s, DragonSword, 0, Zone.Hand); var ring = Add(s, RingOfAir, 0, Zone.FateDiscard);
        s.RingSchismTurn = s.TurnNumber; s.Players[0].RingPutIntoPlayTurn = s.TurnNumber; // a Ring already entered play this turn
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: sword.Id, SourceIds: e.PaymentOptions(0, CardCatalog.Get(DragonSword).GoldCost).First().SourceIds, TargetId: dragon.Id));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfAir));
        var play = e.QueryActions(0).Single(a => a.Type == "chooseSearchDestination" && a.Label.Contains("into play"));
        Check(!play.Enabled && play.Reason is not null, "the 'into play' choice is refused while Rings will not enter play");
        Reject(e, new GameCommand("chooseSearchDestination", 0, e.Version, Choice: "play"));
        Check(e.State.Cards[ring.Id].Zone == Zone.FateDiscard && Entry(e, ring.Id) is null, "nothing entered play");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseSearchDestination" && a.Enabled && a.Command!.Choice == "hand").Command!);
        Eq(e.State.Cards[ring.Id].Zone, Zone.Hand, "the hand is still allowed");
    }

    // ---- Enlightenment --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine FiveRings(Action<MatchState, Dictionary<string, CardInstance>>? edit = null, string[]? defs = null)
    {
        var s = Table(Phase.Dynasty, 1);
        var rings = new Dictionary<string, CardInstance>();
        foreach (var def in defs ?? [RingOfAir, RingOfWater, RingOfVoid, RingOfEarth, RingOfFire]) rings[def] = Add(s, def, 0, Zone.Home);
        edit?.Invoke(s, rings);
        return MatchEngine.FromTestState(s);
    }

    private static bool WonAtTurnStart(MatchEngine e)
    {
        RunUntil(e, () => e.State.WinnerId is not null || e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action);
        return e.State.WinnerId == 0;
    }

    private static void EnlightenmentScenario()
    {
        Check(WonAtTurnStart(FiveRings()), "five Rings with no record (they were always in play): the Enlightenment Victory");
        Check(WonAtTurnStart(FiveRings((s, r) => { foreach (var ring in r.Values) s.WlRings.Entries[ring.Id] = new RingEntryRecord(WlRingsState.OwnText, ring.Id, "recruit", 1); })), "five Rings, each last entered by its own text: victory");
        Check(!WonAtTurnStart(FiveRings((s, r) => s.WlRings.Entries[r[RingOfFire].Id] = new RingEntryRecord(WlRingsState.OtherCard, "any-card", "any-ability", 1))), "one Ring another card put into play: no victory");
        Check(!WonAtTurnStart(FiveRings(defs: [RingOfAir, RingOfWater, RingOfVoid, RingOfEarth])), "four Rings: no victory");
        Check(!WonAtTurnStart(FiveRings(defs: [RingOfAir, RingOfWater, RingOfVoid, RingOfEarth, FalseFire])), "a False Ring in place of the fifth: Fudo never counts");
        // Through the action: Togashi's Guidance puts the fifth Ring into play and it does not count at the start of the turn (its own recruit counts it again: ReentryOverwrites).
        var s = Table(Phase.Action, 0); s.Players[0].Clan = "Dragon";
        foreach (var def in new[] { RingOfWater, RingOfAir, RingOfEarth, RingOfFire }) Add(s, def, 0, Zone.Home);
        var togashi = Add(s, TogashisGuidance, 0, Zone.Celestial); var voidRing = Add(s, RingOfVoid, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "togashis-guidance-ring-play", togashi.Id, voidRing.Id));
        var st = e.State; st.Phase = Phase.Dynasty; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; st.PendingChoice = null; st.AdditionalActions.Clear();
        Check(!WonAtTurnStart(MatchEngine.FromTestState(st)), "the Ring Togashi's Guidance put into play does not count at the start of the turn");
        var forgiven = e.State; forgiven.WlRings.Entries[voidRing.Id] = new RingEntryRecord(WlRingsState.OwnText, voidRing.Id, "ring-of-void-recruit", 1); forgiven.Cards[voidRing.Id].AbilityUses.Remove("ee-e:no-enlightenment");
        forgiven.Phase = Phase.Dynasty; forgiven.ActivePlayerId = 1; forgiven.PriorityPlayerId = 1; forgiven.PendingChoice = null; forgiven.AdditionalActions.Clear();
        Check(WonAtTurnStart(MatchEngine.FromTestState(forgiven)), "with its record showing its own text the same five Rings win");
    }

    // ---- the trigger ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine WitnessRig(Action<MatchState, CardInstance>? more = null, int actor = 0, bool opponentRing = false)
    {
        var s = Table(Phase.Action, actor);
        var host = Add(s, Shugenja, 0, Zone.Home); Attach(s, Witness, host);
        Add(s, RingOfVoid, opponentRing ? 1 : 0, Zone.Hand);
        more?.Invoke(s, host);
        return MatchEngine.FromTestState(s);
    }

    private static CardInstance SpellOf(MatchEngine e, int owner = 0) => e.State.Cards.Values.First(c => c.DefinitionId == Witness && c.OwnerId == owner);
    private static CardInstance HandRing(MatchEngine e, int owner) => e.State.Cards.Values.First(c => c.DefinitionId == RingOfVoid && c.OwnerId == owner);
    private static bool Offered(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Enabled);

    private static void TriggerScenario()
    {
        // Own text: the Reaction opens for the controller, may be declined, and the state stays as it was.
        var e = WitnessRig();
        var witness = SpellOf(e);
        Check(!CanUse(e, 0, WlRingsAbilities.WitnessLook, witness.Id), "no Ring has entered play yet");
        e = Batch336Support.GrantVoid(e, 0, HandRing(e, 0).Id); Accept(e, Use(e, 0, "ring-of-void-recruit", HandRing(e, 0).Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for the Ring's entry");
        Check(Offered(e, 0, WlRingsAbilities.WitnessLook), "Witness the Untold is offered");
        Accept(e, e.QueryActions(0).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
        for (var guard = 0; e.State.PendingChoice is { } && guard < 4; guard++) Accept(e, e.QueryActions(e.State.PendingChoice.PlayerId).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
        Check(!e.State.Cards[witness.Id].Bowed && e.State.PendingChoice is null && e.State.PendingLook is null, "declined: the Spell is still unbowed, nothing was looked at");
        // The opponent's Ring entering play: not "your Ring".
        var o = WitnessRig(actor: 1, opponentRing: true);
        o = Batch336Support.GrantVoid(o, 1, HandRing(o, 1).Id); Accept(o, Use(o, 1, "ring-of-void-recruit", HandRing(o, 1).Id));
        Check(!Offered(o, 0, WlRingsAbilities.WitnessLook) && !Offered(o, 1, WlRingsAbilities.WitnessLook), "an opponent's Ring does not trigger my Witness the Untold");
        // Another card's effect: Togashi's Guidance.
        var t = TogashiRig(out var togashi, out var ring, st => { var h = Add(st, Shugenja, 0, Zone.Home); Attach(st, Witness, h); });
        Accept(t, Offer(t, 0, "togashis-guidance-ring-play", togashi.Id, ring.Id));
        Check(Offered(t, 0, WlRingsAbilities.WitnessLook), "Togashi's Guidance: offered");
        // Another card's effect, Starting on the Path.
        var s = Published(Table(Phase.Action, 0));
        var ringP = Add(s, RingOfAir, 0, Zone.Hand);
        var path = Add(s, StartingPath, 0, Zone.EventInPlay); path.ExpiresAtTurnsTaken = s.Players[0].TurnsTaken + 2;
        var hostP = Add(s, Shugenja, 0, Zone.Home); Attach(s, Witness, hostP);
        var pe = MatchEngine.FromTestState(s);
        Accept(pe, StartingPathOffer(pe, ringP));
        Check(Offered(pe, 0, WlRingsAbilities.WitnessLook), "Starting on the Path: offered");
        // False Ring of Fire's Open.
        var f = Table(Phase.Action, 0); f.Players[0].MaxHandBonus = 2;
        var fire = Add(f, FalseFire, 0, Zone.Hand); var hostF = Add(f, Shugenja, 0, Zone.Home); Attach(f, Witness, hostF);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Enabled(fe, 0, WlSpellsRingsAbilities.FalseFireRecruit, fire.Id).Command!);
        Check(Offered(fe, 0, WlRingsAbilities.WitnessLook), "False Ring of Fire: offered");
        // Entry searches: outside every action, so the window comes from the search finishing.
        var (swordE, _) = SwordSearch(out _, st => { var h = Add(st, Shugenja, 0, Zone.Home); Attach(st, Witness, h); });
        Eq(swordE.State.PendingChoice?.Type, "afterActionResolvedReaction", "Celestial Sword of the Dragon: the window opens when its search puts the Ring into play");
        Check(Offered(swordE, 0, WlRingsAbilities.WitnessLook), "Celestial Sword of the Dragon: offered");
        var passing = swordE.QueryActions(0).First(a => a.Type == "declineReaction" && a.Enabled).Command!;
        Accept(swordE, passing);
        Check(swordE.State.PendingChoice is null && swordE.State.PendingSearch is null, "passing closes the window and play goes on");
        var (chuE, _, _) = ChukageSearch(st => { var h = Add(st, Shugenja, 0, Zone.Home); Attach(st, Witness, h); });
        Eq(chuE.State.PendingChoice?.Type, "afterActionResolvedReaction", "Asako Chukage: the window opens");
        Check(Offered(chuE, 0, WlRingsAbilities.WitnessLook), "Asako Chukage: offered");
        Accept(chuE, chuE.QueryActions(0).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
        Check(chuE.State.PendingChoice is null, "and passable");
        // A Ring a search took into the HAND enters no play: no window.
        var (noRing, _) = SwordSearchToHand();
        Check(noRing.State.PendingChoice is null, "a Ring taken into the hand opens no window");
    }

    private static (MatchEngine, string) SwordSearchToHand()
    {
        var s = Table(Phase.Action, 0); s.Players[0].Clan = "Dragon";
        var dragon = Add(s, "training-tattooed", 0, Zone.Home); dragon.GrantedKeywords.Add(new("Dragon Clan", "test-fixture", GrantDuration.Permanent));
        var sword = Add(s, DragonSword, 0, Zone.Hand); var inDiscard = Add(s, RingOfAir, 0, Zone.FateDiscard);
        var h = Add(s, Shugenja, 0, Zone.Home); Attach(s, Witness, h);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: sword.Id, SourceIds: e.PaymentOptions(0, CardCatalog.Get(DragonSword).GoldCost).First().SourceIds, TargetId: dragon.Id));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfAir));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseSearchDestination" && a.Command!.Choice == "hand").Command!);
        return (e, inDiscard.Id);
    }

    // ---- Witness the Untold ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WitnessScenario()
    {
        var e = WitnessRig((s, _) =>
        {
            foreach (var def in new[] { RingOfAir, RingOfEarth, RingOfFire }) { var c = Add(s, def, 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, c.Id); }
        });
        var witness = SpellOf(e);
        var top = e.State.Players[0].FateDeck.Take(3).ToArray();
        var deckSize = e.State.Players[0].FateDeck.Count; var dynastySize = e.State.Players[0].DynastyDeck.Count;
        e = Batch336Support.GrantVoid(e, 0, HandRing(e, 0).Id); Accept(e, Use(e, 0, "ring-of-void-recruit", HandRing(e, 0).Id));
        Accept(e, Enabled(e, 0, WlRingsAbilities.WitnessLook, witness.Id).Command!);
        Check(e.State.Cards[witness.Id].Bowed, "the Spell bowed as the cost");
        Eq(e.State.PendingChoice?.Type, "lookDeck", "the Dynasty or Fate deck is chosen first");
        Eq(e.QueryActions(0).Count(a => a.Type == "chooseLookDeck"), 2, "your own two decks only");
        Accept(e, new GameCommand("chooseLookDeck", 0, e.Version, Choice: "Fate", TargetPlayerId: 0));
        var view = e.GetView(0).PendingLook!;
        Eq(view.Cards!.Length, 3, "the top three cards"); Eq(view.Requested, 3, "three requested");
        Check(view.Rules!.Length == 0 && view.CanReorder, "nothing but a reorder");
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["2:top", "0:top", "1:top"]));
        Eq(string.Join(",", e.State.Players[0].FateDeck.Take(3)), string.Join(",", new[] { top[2], top[0], top[1] }), "put back in the chosen order");
        Eq(e.State.Players[0].FateDeck.Count, deckSize, "no card left the deck"); Eq(e.State.Players[0].DynastyDeck.Count, dynastySize, "the Dynasty deck is untouched");
        Check(e.State.PendingLook is null && e.State.PendingChoice is null || e.State.PendingChoice is { Type: "afterActionResolvedReaction" }, "the look is over");
        // The Dynasty deck is the other choice.
        var d = WitnessRig();
        d = Batch336Support.GrantVoid(d, 0, HandRing(d, 0).Id); Accept(d, Use(d, 0, "ring-of-void-recruit", HandRing(d, 0).Id));
        Accept(d, Enabled(d, 0, WlRingsAbilities.WitnessLook, SpellOf(d).Id).Command!);
        Accept(d, new GameCommand("chooseLookDeck", 0, d.Version, Choice: "Dynasty", TargetPlayerId: 0));
        Eq(d.GetView(0).PendingLook!.Deck, "Dynasty", "the Dynasty deck");
        Eq(d.GetView(1).PendingLook, null, "the opponent sees nothing of the look");
        // A bowed Witness, one in a hand, and one that is not the Ring's controller's are not offered.
        var b = WitnessRig((s, _) => s.Cards.Values.Single(c => c.DefinitionId == Witness).Bowed = true);
        b = Batch336Support.GrantVoid(b, 0, HandRing(b, 0).Id); Accept(b, Use(b, 0, "ring-of-void-recruit", HandRing(b, 0).Id));
        Check(!Offered(b, 0, WlRingsAbilities.WitnessLook), "a bowed Spell cannot pay its cost");
        var o = WitnessRig((s, h) => { var spell = s.Cards.Values.Single(c => c.DefinitionId == Witness); spell.OwnerId = 1; });
        o = Batch336Support.GrantVoid(o, 0, HandRing(o, 0).Id); Accept(o, Use(o, 0, "ring-of-void-recruit", HandRing(o, 0).Id));
        Check(!Offered(o, 0, WlRingsAbilities.WitnessLook) && !Offered(o, 1, WlRingsAbilities.WitnessLook), "the other player's Spell does not answer my Ring");
        // Rejected use leaves the state unchanged: no Ring has entered play.
        var r = WitnessRig();
        Reject(r, Use(r, 0, WlRingsAbilities.WitnessLook, SpellOf(r).Id));
    }

    // ---- Fudo's Assurances ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine FudoRig(Phase phase, out CardInstance fudo, Action<MatchState>? more = null, string[]? fudoRings = null, int? marked = null)
    {
        var s = Table(phase, 0);
        if (phase == Phase.Battle) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        fudo = Add(s, FudoAssurances, 0, Zone.Celestial);
        var rings = (fudoRings ?? [FalseAir, FalseEarth, FalseFire, FalseVoid, FalseWater]).Select(def => Add(s, def, 0, Zone.Home)).ToArray();
        if (marked is int i) s.WlRings.Entries[rings[i].Id] = new RingEntryRecord(WlRingsState.OtherCard, "x", "y", 1);
        more?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static GameCommand DestroyCommand(MatchEngine e, CardInstance fudo, int owner, int province) =>
        Use(e, 0, WlRingsAbilities.FudoDestroy, fudo.Id) with { TargetPlayerId = owner, Province = province };

    private static void FudoScenario()
    {
        // Straighten a Ring: any Ring in play, either player's, Battle or Open.
        foreach (var phase in new[] { Phase.Action, Phase.Battle })
        {
            var e = FudoRig(phase, out var fudo, s => { Add(s, RingOfAir, 1, Zone.Home, bowed: true); Add(s, RingOfFire, 0, Zone.Home, bowed: true); }, fudoRings: []);
            var enemyRing = e.State.Cards.Values.Single(c => c.DefinitionId == RingOfAir); var ownRing = e.State.Cards.Values.Single(c => c.DefinitionId == RingOfFire);
            Accept(e, Offer(e, 0, WlRingsAbilities.FudoStraighten, fudo.Id, enemyRing.Id));
            Check(!e.State.Cards[enemyRing.Id].Bowed, $"{phase}: the enemy's Ring is straightened");
            Check(e.State.Cards[ownRing.Id].Bowed, $"{phase}: nothing else was");
        }
        var inHand = FudoRig(Phase.Action, out var f2, s => Add(s, RingOfAir, 0, Zone.Hand), fudoRings: []);
        Reject(inHand, Use(inHand, 0, WlRingsAbilities.FudoStraighten, f2.Id, inHand.State.Players[0].Hand.Single()));
        // Five Fudo Rings that last entered play by their own text: a Province is destroyed (the enemy's, then the acting player's own is also legal).
        var five = FudoRig(Phase.Action, out var fudo5);
        Check(CanUse(five, 0, WlRingsAbilities.FudoDestroy, fudo5.Id), "five Fudo Rings: offered");
        var before = five.State.Players[1].Provinces.Count(p => p.Destroyed);
        Accept(five, DestroyCommand(five, fudo5, 1, 2));
        Check(five.State.Players[1].Provinces[2].Destroyed && five.State.Players[1].Provinces.Count(p => p.Destroyed) == before + 1, "the named Province is destroyed");
        Check(five.State.Players[0].Provinces.All(p => !p.Destroyed), "and only that one");
        Reject(five, DestroyCommand(five, fudo5, 1, 2)); // already destroyed (and the use is spent)
        // Four, or five of which one last entered play by another card's effect, or real Rings: refused, nothing changes.
        var four = FudoRig(Phase.Action, out var fudo4, fudoRings: [FalseAir, FalseEarth, FalseFire, FalseVoid]);
        Check(!CanUse(four, 0, WlRingsAbilities.FudoDestroy, fudo4.Id), "four Fudo Rings: not offered"); Reject(four, DestroyCommand(four, fudo4, 1, 2));
        var cheated = FudoRig(Phase.Action, out var fudoC, marked: 3);
        Check(!CanUse(cheated, 0, WlRingsAbilities.FudoDestroy, fudoC.Id), "one Ring last entered play by another card's effect: not offered"); Reject(cheated, DestroyCommand(cheated, fudoC, 1, 2));
        var real = FudoRig(Phase.Action, out var fudoR, fudoRings: [FalseAir, FalseEarth, FalseFire, FalseVoid, RingOfWater]);
        Check(!CanUse(real, 0, WlRingsAbilities.FudoDestroy, fudoR.Id), "a real Ring is not a Fudo Ring"); Reject(real, DestroyCommand(real, fudoR, 1, 2));
        var enemyRings = FudoRig(Phase.Action, out var fudoE, s => { foreach (var def in new[] { FalseAir, FalseEarth, FalseFire, FalseVoid, FalseWater }) Add(s, def, 1, Zone.Home); }, fudoRings: []);
        Reject(enemyRings, DestroyCommand(enemyRings, fudoE, 1, 2));
        // Open, not Battle: refused in the Battle Phase.
        var battle = FudoRig(Phase.Battle, out var fudoB);
        Check(!CanUse(battle, 0, WlRingsAbilities.FudoDestroy, fudoB.Id), "an Open action is not taken in a battle");
        // A Ring that re-enters by its own text counts again.
        var again = FudoRig(Phase.Action, out var fudoA, marked: 3);
        var st = again.State; st.WlRings.Entries[st.Cards.Values.Where(c => c.DefinitionId == FalseVoid).Single().Id] = new RingEntryRecord(WlRingsState.OwnText, "z", "wl-false-ring-of-void-recruit", 2);
        var fixedUp = MatchEngine.FromTestState(st);
        Accept(fixedUp, DestroyCommand(fixedUp, fudoA, 1, 0));
        Check(fixedUp.State.Players[1].Provinces[0].Destroyed, "own-text again: the Province is destroyed");
    }

    // ---- the existing reactions -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void YungStillWorks()
    {
        var s = Table(Phase.Action, 0);
        var yung = Add(s, Yung, 0, Zone.Home); var ring = Add(s, RingOfVoid, 0, Zone.Hand);
        var air = Add(s, RingOfAir, 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, air.Id);
        var e = MatchEngine.FromTestState(s);
        Check(AbilityCatalog.All["yung-search-ring"].Trigger == ReactionTriggers.RingEntersPlay, "Yung is on the new trigger");
        Check(!CanUse(e, 0, "yung-search-ring", yung.Id), "no Ring has entered play");
        e = Batch336Support.GrantVoid(e, 0, ring.Id); Accept(e, Use(e, 0, "ring-of-void-recruit", ring.Id));
        Accept(e, Enabled(e, 0, "yung-search-ring", yung.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "searchSelect", "his search opens");
    }
}
