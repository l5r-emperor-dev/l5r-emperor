using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #1114: the Celestial audit. Each case is a defect the audit found (or the reported Doji's Guidance confusion), driven through QueryActions and the commands it offers.
// Player 0 is the Crane, player 1 the Lion (Batch336Support.Table).
internal static class CelestialAuditTests
{
    private const string DojisGuidance = "oracle-l5r-2181", CourtesyOfTheCrane = "oracle-l5r-7996", Ebisu = "oracle-l5r-2276", DestructivePriorities = "oracle-l5r-1983",
        Benten = "oracle-l5r-959", Megumi = "oracle-l5r-10028", Daigotsu = "oracle-l5r-1788", Ryoshun = "oracle-l5r-6425", Hantei = "oracle-l5r-10417", Suitengu = "oracle-l5r-9948";

    public static void Run(TestRunner runner) => runner.Run("celestial-audit", [
        ("Doji's Guidance: with no Political Strategy in the discard pile its Open says so; The Courtesy of the Crane is returned to the hand", DojiReasonAndReturn),
        ("Ebisu's Honesty: the rightmost province is destroyed as a cost (refused with none left), and Destructive Priorities is not offered on its unnegatable gain", EbisuCostAndUnnegatableGain),
        ("Benten's Blessing: entering play afresh and choosing no one gives its earlier choices nothing", BentenReentryForgets),
        ("Megumi's Guidance: its Open half shields a Personality at home; a dead Personality is never its performer", MegumiOpenHalf),
        ("Daigotsu's Guidance discarding the opponent's Ryoshun's Guidance destroys his Personalities left at 0 Chi", DaigotsuDiscardsOpponentsRyoshun),
        ("Hantei's Guidance's Political Interrupt negates the rulebook Lobby", HanteiNegatesLobby),
        ("Suitengu's Blessing: a Naval grant does not come back when the Celestial enters play again", SuitenguReentryForgets),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone) => Batch336Support.Add(s, def, owner, zone, false, null, null);
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && (a.Command?.AbilityId == ability || a.Command is null && a.Label.StartsWith(AbilityCatalog.All[ability].Label[..20], StringComparison.Ordinal))).ToArray();

    private static void DojiReasonAndReturn()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var doji = Add(s, DojisGuidance, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        var none = Listed(e, 0, "dojis-guidance-return").Single();
        Check(!none.Enabled && none.Reason == "Your Fate discard pile has no non-Unique Strategy with a Political ability.", "the player is told why: " + none.Reason);
        CardInstance courtesy = null!;
        e = Edit(e, st => courtesy = Add(st, CourtesyOfTheCrane, 0, Zone.FateDiscard));
        Accept(e, Offer(e, 0, "dojis-guidance-return", doji.Id, courtesy.Id));
        Eq(e.State.Cards[courtesy.Id].Zone, Zone.Hand, "the Courtesy is back in the hand");
        Eq(e.State.Cards[doji.Id].Zone, Zone.DynastyDiscard, "and the Celestial was discarded");
    }

    private static void EbisuCostAndUnnegatableGain()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var ebisu = Add(s, Ebisu, 0, Zone.Celestial);
        Add(s, DestructivePriorities, 0, Zone.Home);
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home); courtier.GrantedKeywords.Add(new("Courtier", "test-fixture", GrantDuration.Permanent));
        var e = MatchEngine.FromTestState(s);
        var rightmost = e.State.Players[0].Provinces.Where(p => !p.Destroyed).Max(p => p.Index);
        var honor = e.State.Players[0].Honor;
        Accept(e, Enabled(e, 0, "ebisu-destroy-province", ebisu.Id).Command!);
        Check(e.State.PendingChoice?.Type != "honorChangeReaction", "Destructive Priorities is not offered on a gain that will not be negated");
        Check(e.State.Players[0].Provinces.Single(p => p.Index == rightmost).Destroyed, "the rightmost province was destroyed");
        Eq(e.State.Players[0].Honor, honor + 4, "4 Honor");
        var none = Table(Phase.Action, 0); none.PriorityPlayerId = 0;
        var spent = Add(none, Ebisu, 0, Zone.Celestial);
        foreach (var province in none.Players[0].Provinces) province.Destroyed = true;
        var bare = MatchEngine.FromTestState(none);
        Check(!Listed(bare, 0, "ebisu-destroy-province").Any(a => a.Enabled), "no province to destroy: the cost cannot be paid");
        var refused = bare.Execute(new GameCommand("useAbility", 0, bare.Version, CardId: spent.Id, AbilityId: "ebisu-destroy-province"));
        Check(!refused.Accepted && refused.Error == "You have no province left to destroy.", "refused: " + refused.Error);
    }

    private static void BentenReentryForgets()
    {
        var s = Table(Phase.Action, 0);
        var chosen = Add(s, "training-crane-samurai", 0, Zone.Home);
        var benten = Add(s, Benten, 0, Zone.Province); s.Players[0].Provinces[2].CardId = benten.Id; benten.ProvinceIndex = 2; benten.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var printed = CardCatalog.Get("training-crane-samurai").PersonalHonor;
        int PersonalHonor(MatchEngine m) => (int)typeof(MatchEngine).GetMethod("CardPersonalHonor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(m, [m.State.Cards[chosen.Id]])!;
        e.CelestialEntryForTest(0, 2, benten.Id, pauseReveal: false);
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: chosen.Id));
        Accept(e, new("budgetSelectConfirm", 0, e.Version));
        Eq(PersonalHonor(e), printed + 1, "chosen: +1 Personal Honor");
        // It leaves play and comes back through a province; this time nobody is chosen.
        var again = Edit(e, st => { var b = st.Cards[benten.Id]; b.Zone = Zone.Province; b.FaceUp = true; b.ProvinceIndex = 1; st.Players[0].Provinces[1].CardId = b.Id; });
        Eq(PersonalHonor(again), printed, "out of play: no bonus");
        again.CelestialEntryForTest(0, 1, benten.Id, pauseReveal: false);
        Accept(again, new GameCommand("pass", again.State.PriorityPlayerId, again.Version));
        Eq(again.State.PendingChoice?.Type, Wave829Abilities.BentenType, "the new entry asks again");
        Accept(again, new("budgetSelectConfirm", 0, again.Version));
        Eq(PersonalHonor(again), printed, "choosing no one this time gives the earlier choice nothing");
    }

    private static void MegumiOpenHalf()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var megumi = Add(s, Megumi, 0, Zone.Celestial);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home);
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        var options = Listed(e, 0, "megumis-guidance-shield").Where(a => a.Enabled).ToArray();
        Check(options.Any(a => a.Command!.PerformerId == home.Id), "his Personality at home performs the Open half");
        Check(options.All(a => a.Command!.PerformerId != dead.Id), "a dead Personality never does");
        Accept(e, options.First(a => a.Command!.PerformerId == home.Id).Command!);
        Eq(e.State.Cards[megumi.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
    }

    private static void DaigotsuDiscardsOpponentsRyoshun()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        Add(s, Ryoshun, 1, Zone.Celestial);
        var frail = Add(s, "training-lion-samurai", 1, Zone.Home);
        frail.Modifiers.Add(new("test-source", "test", "Chi", -CardCatalog.Get("training-lion-samurai").Chi));
        var daigotsu = Add(s, Daigotsu, 0, Zone.Province); s.Players[0].Provinces[2].CardId = daigotsu.Id; daigotsu.ProvinceIndex = 2; daigotsu.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Eq(e.State.Cards[frail.Id].Zone, Zone.Home, "Ryoshun's +1C keeps him alive");
        e.CelestialEntryForTest(0, 2, daigotsu.Id, pauseReveal: false);
        Check(e.State.Cards.Values.All(c => c.DefinitionId != Ryoshun || c.Zone != Zone.Celestial), "Daigotsu's Guidance discarded the Kami Celestial");
        Check(e.State.Cards[frail.Id].Zone != Zone.Home, "his Personality at 0 Chi was destroyed");
    }

    private static void HanteiNegatesLobby()
    {
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1; s.FavorPlayerId = null;
        foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; // player abilities (Lobby) need a published Stronghold
        s.Players[1].Honor = s.Players[0].Honor + 5;
        var lobbyist = Add(s, "training-lion-samurai", 1, Zone.Home);
        var hantei = Add(s, Hantei, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        var lobby = e.QueryActions(1).First(a => a.Enabled && a.Command?.AbilityId == "lobby" && (a.TargetIds?.Contains(lobbyist.Id) ?? false));
        Accept(e, lobby.Command! with { TargetId = lobbyist.Id });
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "Lobby is a Political action: the Interrupt's window opens");
        var negate = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "w740-hantei-negate" && a.Command.CardId == hantei.Id);
        Check(negate is not null, "Hantei's Guidance is offered against Lobby");
        Accept(e, negate!.Command!);
        for (var guard = 0; e.State.PendingChoice is not null && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.FavorPlayerId, null, "the Lobby was negated: nobody took the Imperial Favor");
    }

    private static void SuitenguReentryForgets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var suitengu = Add(s, Suitengu, 0, Zone.Province); s.Players[0].Provinces[2].CardId = suitengu.Id; suitengu.ProvinceIndex = 2; suitengu.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        e.CelestialEntryForTest(0, 2, suitengu.Id, pauseReveal: false);
        if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Accept(e, Offer(e, 0, "pe-suitengus-blessing-naval", suitengu.Id, mine.Id));
        Check(e.HasKeyword(mine.Id, "Naval"), "Naval while it is in play");
        var again = Edit(e, st => { var c = st.Cards[suitengu.Id]; c.Zone = Zone.Province; c.ProvinceIndex = 1; st.Players[0].Provinces[1].CardId = c.Id; });
        again.CelestialEntryForTest(0, 1, suitengu.Id, pauseReveal: false);
        Check(!again.HasKeyword(mine.Id, "Naval"), "a fresh entry grants nothing its earlier time in play granted");
    }
}
