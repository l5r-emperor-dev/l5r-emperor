using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #737, wave batch 6 (src/L5R.Core/Wave737.cs): the after-event window and period watch cards, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the
// Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave737Tests
{
    private const string Akumasa = Wave737Abilities.Akumasa, Shigemitsu = Wave737Abilities.Shigemitsu, Tadama = Wave737Abilities.Tadama, Ikeuchi = Wave737Abilities.Ikeuchi, Shibatsu = Wave737Abilities.Shibatsu,
        Taigo = Wave737Abilities.Taigo, Kuroko = Wave737Abilities.Kuroko, Katsuta = Wave737Abilities.Katsuta, ReverseFate = Wave737Abilities.ReverseFate, CrystalTears = Wave737Abilities.CrystalTears,
        Stipend = Wave737Abilities.Stipend, EndToHostilities = Wave737Abilities.EndToHostilities, Weather = Wave737Abilities.UnseasonableWeather, Natsu = Wave737Abilities.Natsu, Jungle = Wave737Abilities.VerdantJungle;
    private const string KoanRobes = "oracle-l5r-10525", ImperialDeployment = "oracle-l5r-10378";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Marketplace = "training-marketplace";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static GameCommand Forged(MatchEngine e, string ability, string cardId, string targetId) => new("useAbility", 0, e.Version, CardId: cardId, TargetId: targetId, AbilityId: ability);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static void Madness(CardInstance card) => card.Tokens.Add(new CardToken("w737-madness-" + card.Id, "Madness", "test"));

    public static void Run(TestRunner runner) => runner.Run("wave-737", [
        ("Published as printed: fifteen cards in seven profiles with their stats, abilities and effects; Koan's Robes and Imperial Deployment are not published", PublishedShape),
        ("Akumasa: loses 2 Honor on entering play; Open watches an enemy Personality with a Madness token and draws a card the first time he is assigned to attack this turn", AkumasaCard),
        ("Asahina Shigemitsu - exp: Open, bow watches one or two of your provinces and gains 2 Honor the first time this turn a Personality is assigned to attack one of them", ShigemitsuCard),
        ("Hida Tadama: the first time each phase he is destroyed he returns to his controller's home bowed, ignoring costs; the second time in a phase he stays dead", TadamaCard),
        ("Hiruma Ikeuchi: with Reconnaissance, the first destruction of one of his Followers by another player's effect each battle is negated; without it, or from his own player's effect, it is not", IkeuchiCard),
        ("Iweko Shibatsu: Spider Clan players ignore his Honor Requirement; +3 Honor on entering play, -10 on leaving it; he gains no Clan alignment; Limited, bow gains 1 Honor", ShibatsuCard),
        ("Susumu Kuroko: +2 Honor after you Proclaim Susumu Takuan, no one else; Political Limited, bow takes the Imperial Favor", KurokoCard),
        ("Yogo Katsuta: loses 2 Honor on entering play; a Personality's seppuku costs its controller 1 Honor while she is in play", KatsutaCard),
        ("An End to Hostilities: from then on each player draws one more card at the end of his turn if he put no other cards in his hand that turn", EndToHostilitiesCard),
        ("Unseasonable Weather: an Event in play until its owner's next turn begins; each Personality assigned to attack meanwhile cannot straighten in his controller's next Straighten Phase", WeatherCard),
        ("Natsu-Togumara's Guidance: the chosen Personality's unit is not bowed by battle resolution while the Celestial stays; Battle, discard moves a Personality of yours to the battle and straightens his unit", NatsuCard),
        ("Magistrate's Stipend: produces 2 Gold, 3 for a Magistrate; after you announce a non-Iaijutsu action a performing Magistrate gets +1C until it ends", StipendCard),
        ("The Crystal Tears: replaces the Terrain at the battlefield, bows the chosen card and straightens it when the Terrain is destroyed", CrystalTearsCard),
        ("Verdant Jungle: your Nonhuman Personalities recruited from its Province cost 1 less; its Interrupt negates a movement of Personalities into an attacking army at its battlefield", JungleCard),
        ("Moto Taigo - exp: loses 3 Honor on entering play; his Interrupt destroys a card without attachments in a unit an action moved", TaigoCard),
        ("Reverse Fate: its Void Shugenja's Interrupt gives the first card an action discards +1FV or -1FV", ReverseFateCard),
        ("Verdant Jungle: its Interrupt negates the movement of a Personality into an attacking army at its Province's battlefield, and only there", JungleInterruptCard),
    ]);

    // ---- fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The first province card of the economy scenario becomes the named card, face up, in the Dynasty phase (Honor 40, eight Marketplaces).
    private static (MatchEngine Engine, CardInstance Card) Recruit(string definition)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var card = s.Cards[s.Players[0].Provinces[0].CardId!];
        card.DefinitionId = definition; card.FaceUp = true;
        s.Players[0].Honor = 40;
        for (var i = 0; i < 8; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, e.State.Cards[card.Id]);
    }

    private static void PurchaseAll(MatchEngine e, CardInstance card) =>
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));

    // Player 1's Infantry Maneuvers: the given units are assigned to the given provinces.
    private static MatchEngine Assign(MatchState s, params (CardInstance Card, int Province)[] assignments)
    {
        s.Phase = Phase.InfantryDeclaration; s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("attack", 1, e.Version, Assignments: assignments.ToDictionary(a => a.Card.Id, a => a.Province)));
        return e;
    }

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect, int? EntryLoss)>
        {
            [Akumasa] = (CardType.Personality, ["w737-akumasa-watch"], "none", 2),
            [Shigemitsu] = (CardType.Personality, ["w737-shigemitsu-watch"], "none", null),
            [Tadama] = (CardType.Personality, [], "w737-tadama", null),
            [Ikeuchi] = (CardType.Personality, [], "w737-ikeuchi", null),
            [Shibatsu] = (CardType.Personality, ["w737-shibatsu-honor"], "w737-shibatsu", null),
            [Taigo] = (CardType.Personality, ["w737-taigo-interrupt"], "none", 3),
            [Kuroko] = (CardType.Personality, ["w737-kuroko-favor"], "w737-kuroko", null),
            [Katsuta] = (CardType.Personality, [], "w737-katsuta", 2),
            [ReverseFate] = (CardType.Spell, ["w737-reverse-fate-plus", "w737-reverse-fate-minus"], "none", null),
            [CrystalTears] = (CardType.Strategy, ["w737-crystal-tears-play"], "none", null),
            [Stipend] = (CardType.Holding, ["w737-stipend-announce"], "w737-stipend", null),
            [EndToHostilities] = (CardType.Event, [], "w737-end-to-hostilities", null),
            [Weather] = (CardType.Event, [], "w737-unseasonable-weather", null),
            [Natsu] = (CardType.Celestial, ["w737-natsu-move"], "w737-natsu", null),
            [Jungle] = (CardType.Region, ["w737-jungle-interrupt"], "w737-verdant-jungle", null)
        };
        Eq(expected.Count, 15, "fifteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect && d.EntryHonorLoss == want.EntryLoss, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        Eq(CardCatalog.Get(Stipend).GoldProduction, 2, "Magistrate's Stipend produces its printed 2 Gold");
        Eq(CardCatalog.Get(Shibatsu).HonorRequirementWaiverClan, "Spider", "Shibatsu's Honor Requirement waiver names the Spider Clan");
        foreach (var id in new[] { ImperialDeployment }) Check(CardCatalog.Cards.ContainsKey(id), $"{id} was wired afterwards (Wave835.cs, issue #835)"); // Koan's Robes was wired by issue #827 (Wave827.cs)
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w737-", StringComparison.Ordinal)), 11, "eleven w737 abilities");
    }

    private static void AkumasaCard()
    {
        var (recruit, card) = Recruit(Akumasa);
        var honor = Honor(recruit, 0);
        PurchaseAll(recruit, card);
        Eq(Honor(recruit, 0), honor - 2, "Akumasa costs 2 Honor on entering play");

        // Open, on the other player's turn: only an enemy Personality with a Madness token is a target.
        var s = Batch336Support.Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var akumasa = Add(s, Akumasa, 0, Zone.Home);
        var mad = Fx(s, LionSamurai, "w737-mad", 1, Zone.Home); Madness(mad);
        var sane = Fx(s, LionSamurai, "w737-sane", 1, Zone.Home);
        var ownMad = Fx(s, CraneSamurai, "w737-own-mad", 0, Zone.Home); Madness(ownMad);
        var e = MatchEngine.FromTestState(s);
        var enabled = Batch336Support.Enabled(e, 0, Wave737Abilities.AkumasaWatch, akumasa.Id);
        Check(enabled.TargetIds!.Contains(mad.Id) && !enabled.TargetIds.Contains(sane.Id) && !enabled.TargetIds.Contains(ownMad.Id), "only the enemy Personality with a Madness token is a target");
        NoChange(e, Forged(e, Wave737Abilities.AkumasaWatch, akumasa.Id, sane.Id), "no Madness token");
        NoChange(e, Forged(e, Wave737Abilities.AkumasaWatch, akumasa.Id, ownMad.Id), "his own Personality");
        Accept(e, Batch336Support.Offer(e, 0, Wave737Abilities.AkumasaWatch, akumasa.Id, mad.Id));
        Check(e.State.AssignmentWatches.Any(w => w.CardId == mad.Id && w.Kind == Wave737Abilities.AkumasaWatchKind), "a watch is set on him");

        // The turn goes on: he is assigned to attack, so Akumasa's controller draws once, and not a second time.
        var hand = e.State.Players[0].Hand.Count;
        var attack = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        var other = Fx(e.State, LionSamurai, "w737-other", 1, Zone.Home);
        attack = Batch336Support.Edit(attack, st => { });
        Accept(attack, new GameCommand("attack", 1, attack.Version, Assignments: new Dictionary<string, int> { [mad.Id] = 0 }));
        Eq(attack.State.Players[0].Hand.Count, hand + 1, "Akumasa's controller drew a card");
        Check(!attack.State.AssignmentWatches.Any(w => w.Kind == Wave737Abilities.AkumasaWatchKind), "the watch is spent");
        _ = other;
    }

    private static void ShigemitsuCard()
    {
        var s = Batch336Support.Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var shigemitsu = Add(s, Shigemitsu, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var options = Batch336Support.Options(e, 0, Wave737Abilities.ShigemitsuWatch, shigemitsu.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.Provinces is { Length: 1 }) && options.Any(o => o.Provinces is { Length: 2 }), "one option per set of one or two provinces");
        Accept(e, options.First(o => o.Provinces is { Length: 2 } p && p[0] == 0 && p[1] == 2).Command!);
        Check(e.State.Cards[shigemitsu.Id].Bowed, "Shigemitsu bowed as the cost");
        var watch = e.State.ConsecrationWatches.Single();
        Check(watch.PlayerId == 0 && watch.Provinces.SequenceEqual([0, 2]) && watch.SourceId == shigemitsu.Id, "a watch on provinces 1 and 3, owned by him");

        // A Personality attacking province 3: +2 Honor, once. Another province: nothing.
        MatchEngine Attack(int province, out int before)
        {
            var st = Batch336Support.Table(Phase.InfantryDeclaration, 1); st.PriorityPlayerId = 1;
            var a = Add(st, LionSamurai, 1, Zone.Home); var b = Add(st, "training-lion-veteran", 1, Zone.Home);
            st.ConsecrationWatches.Add(new(0, [0, 2], st.TurnNumber, shigemitsu.Id));
            var eng = MatchEngine.FromTestState(st);
            before = Honor(eng, 0);
            Accept(eng, new GameCommand("attack", 1, eng.Version, Assignments: new Dictionary<string, int> { [a.Id] = province, [b.Id] = province }));
            return eng;
        }
        var hit = Attack(2, out var hitBefore);
        Eq(Honor(hit, 0), hitBefore + 2, "attacking a chosen province: +2 Honor, once for two attackers");
        var miss = Attack(1, out var missBefore);
        Eq(Honor(miss, 0), missBefore, "another province: nothing");
    }

    private static void TadamaCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var tadama = Add(s, Tadama, 0, Zone.Home);
        tadama.Bowed = false;
        var e = MatchEngine.FromTestState(s);
        e.SweepFactsForTest(); // stamps entry turns
        var dead = Batch336Support.Edit(e, st => st.Cards[tadama.Id].Zone = Zone.Dead);
        dead.SweepFactsForTest();
        var back = dead.State.Cards[tadama.Id];
        Check(back.Zone == Zone.Home && back.Bowed, "destroyed, he came back to his controller's home, bowed: " + back.Zone);
        // A second destruction in the same phase: he stays dead.
        var again = Batch336Support.Edit(dead, st => st.Cards[tadama.Id].Zone = Zone.Dead);
        again.SweepFactsForTest();
        Eq(again.State.Cards[tadama.Id].Zone, Zone.Dead, "the second time in a phase he stays dead");
        // A later phase: he may return again.
        var later = Batch336Support.Edit(again, st => { st.Phase = Phase.Dynasty; });
        later.SweepFactsForTest();
        Eq(later.State.Cards[tadama.Id].Zone, Zone.Dead, "still dead until he is next destroyed");
        var third = Batch336Support.Edit(later, st => { st.Cards[tadama.Id].Zone = Zone.Home; st.Phase = Phase.Dynasty; });
        third.SweepFactsForTest();
        var fourth = Batch336Support.Edit(third, st => st.Cards[tadama.Id].Zone = Zone.Dead);
        fourth.SweepFactsForTest();
        Eq(fourth.State.Cards[tadama.Id].Zone, Zone.Home, "in a new phase the first destruction is his again");
        // Leaving play any other way than dying does not bring him back.
        var h = Batch336Support.Table(Phase.Action, 0);
        var kept = Add(h, Tadama, 0, Zone.Home);
        var f = MatchEngine.FromTestState(h);
        f.SweepFactsForTest();
        var hand = Batch336Support.Edit(f, st => st.Cards[kept.Id].Zone = Zone.RemovedFromGame);
        hand.SweepFactsForTest();
        Eq(hand.State.Cards[kept.Id].Zone, Zone.RemovedFromGame, "removed from the game is not destroyed");
    }

    private static void IkeuchiCard()
    {
        // Reconnaissance is held by the Attacker at a Defender's province: Ikeuchi is player 1's, attacking player 0's province 0.
        MatchEngine Rig(bool recon, out CardInstance follower, out CardInstance second)
        {
            var s = Batch336Support.Table(Phase.Battle, 1);
            var ikeuchi = Add(s, Ikeuchi, 1, Zone.Battlefield, province: 0);
            follower = Fx(s, "training-follower", "w737-follower", 1, Zone.Attached, parent: ikeuchi.Id);
            second = Fx(s, "training-follower", "w737-follower-2", 1, Zone.Attached, parent: ikeuchi.Id);
            Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            s.Players[0].Provinces[0].Reconnaissance[1] = recon;
            return MatchEngine.FromTestState(s);
        }
        // With Reconnaissance: the first destruction by another player's effect this battle is negated; the next is not.
        var e = Rig(true, out var follower, out var second);
        Check(e.TryConsumeWatch(WatchKind.Destroy, e.CardForTest(follower.Id), 0), "the first Follower's destruction is negated");
        Check(!e.TryConsumeWatch(WatchKind.Destroy, e.CardForTest(second.Id), 0), "the second, in the same battle, is not");
        // Without Reconnaissance nothing is negated and the use is not spent.
        var f = Rig(false, out var follower2, out _);
        Check(!f.TryConsumeWatch(WatchKind.Destroy, f.CardForTest(follower2.Id), 0), "no Reconnaissance, no negation");
        // His own player's effect never is.
        var g = Rig(true, out var follower3, out _);
        Check(!g.TryConsumeWatch(WatchKind.Destroy, g.CardForTest(follower3.Id), 1), "his controller's own effect is not negated");
        // Ikeuchi himself is not a Follower: his destruction is not covered.
        var h = Rig(true, out _, out _);
        var ike = h.State.Cards.Values.First(c => c.DefinitionId == Ikeuchi);
        Check(!h.TryConsumeWatch(WatchKind.Destroy, h.CardForTest(ike.Id), 0), "only his Followers are covered");
    }

    private static void ShibatsuCard()
    {
        // A Spider Clan player ignores his Honor Requirement; another Clan's player does not.
        foreach (var (clan, ok) in new[] { ("Spider", true), ("Crane", false) })
        {
            var (e, card) = Recruit(Shibatsu);
            Batch336Support.Edit(e, st => { st.Players[0].Clan = clan; st.Players[0].Honor = 0; });
            var f = Batch336Support.Edit(e, st => { st.Players[0].Clan = clan; st.Players[0].Honor = 1; });
            var quote = f.QuotePurchase(0, card.Id);
            Check((quote.Error is null || !quote.Error.Contains("Honor Requirement")) == ok, $"a {clan} player {(ok ? "ignores" : "meets")} the Honor Requirement: {quote.Error}");
        }
        // +3 Honor on entering play, and the Clan block.
        var (r, shibatsu) = Recruit(Shibatsu);
        var g = Batch336Support.Edit(r, st => { st.Players[0].Clan = "Spider"; });
        var honor = Honor(g, 0);
        PurchaseAll(g, g.State.Cards[shibatsu.Id]);
        Eq(Honor(g, 0), honor + 3, "+3 Honor on entering play");
        var inPlay = g.State.Cards[shibatsu.Id];
        Check(Call(g, "KeywordGainBlocked", inPlay, "Crane Clan") is true, "he will not gain a Clan alignment");
        Check(Call(g, "KeywordGainBlocked", inPlay, "Samurai") is false, "other keywords are unaffected");
        // Limited, bow: gain 1 Honor.
        var s = Batch336Support.Table(Phase.Action, 0);
        var sh = Add(s, Shibatsu, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s); var h2 = Honor(e2, 0);
        Accept(e2, Batch336Support.Use(e2, 0, Wave737Abilities.ShibatsuHonor, sh.Id));
        Eq(Honor(e2, 0), h2 + 1, "+1 Honor");
        Check(e2.State.Cards[sh.Id].Bowed, "bowed as the cost");
        // Leaving play: -10 Honor.
        e2.SweepFactsForTest();
        var gone = Batch336Support.Edit(e2, st => st.Cards[sh.Id].Zone = Zone.Dead);
        var h3 = Honor(gone, 0);
        gone.SweepFactsForTest();
        Eq(Honor(gone, 0), h3 - 10, "-10 Honor after he leaves play");
    }

    private static void KurokoCard()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Dynasty; s.Players[0].Clan = "Crane";
        var takuan = Fx(s, CraneSamurai, "w737-takuan", 0, Zone.Home, d => d with { Name = "Susumu Takuan" });
        var other = Fx(s, CraneSamurai, "w737-other-proclaimed", 0, Zone.Home, d => d with { Name = "Someone Else" });
        var kuroko = Add(s, Kuroko, 0, Zone.Home);
        s.PendingChoice = new("proclaim", 0, takuan.Id, "After entry: Proclaim for Honor or pass.");
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, new GameCommand("proclaim", 0, e.Version));
        // The Proclaim itself gains Honor; Kuroko adds 2 more for Takuan.
        var proclaimGain = Honor(e, 0) - before;
        Check(proclaimGain >= 2, "Takuan's Proclaim gave Honor, with Kuroko's 2 among it: +" + proclaimGain);
        var t = MatchEngine.CreateEconomyScenario().State;
        t.ActivePlayerId = 0; t.PriorityPlayerId = 0; t.Phase = Phase.Dynasty; t.Players[0].Clan = "Crane";
        var o = Fx(t, CraneSamurai, "w737-other2", 0, Zone.Home, d => d with { Name = "Someone Else" });
        Add(t, Kuroko, 0, Zone.Home);
        t.PendingChoice = new("proclaim", 0, o.Id, "After entry: Proclaim for Honor or pass.");
        var f = MatchEngine.FromTestState(t);
        var control = Honor(f, 0);
        Accept(f, new GameCommand("proclaim", 0, f.Version));
        Eq(proclaimGain - (Honor(f, 0) - control), 2, "another Personality's Proclaim does not give Kuroko's 2");
        _ = other;
        // Political Limited, bow: take the Imperial Favor.
        var u = Batch336Support.Table(Phase.Action, 0);
        var k = Add(u, Kuroko, 0, Zone.Home);
        u.FavorPlayerId = 1;
        var g = MatchEngine.FromTestState(u);
        Accept(g, Batch336Support.Use(g, 0, Wave737Abilities.KurokoFavor, k.Id));
        Eq(g.State.FavorPlayerId, 0, "Kuroko's controller took the Imperial Favor");
        Check(g.State.Cards[k.Id].Bowed, "bowed as the cost");
    }

    private static void KatsutaCard()
    {
        var (recruit, card) = Recruit(Katsuta);
        var honor = Honor(recruit, 0);
        PurchaseAll(recruit, card);
        Eq(Honor(recruit, 0), honor - 2, "Katsuta costs 2 Honor on entering play");
        // Seppuku: Order Seppuku on a dishonorable Personality of the other player's: his controller loses 1 Honor.
        var s = Batch336Support.Table(Phase.Action, 0);
        Add(s, Katsuta, 1, Zone.Home);
        var victim = Fx(s, CraneSamurai, "w737-victim", 0, Zone.Home, d => d with { Keywords = ["Samurai"] }); victim.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, new GameCommand("orderSeppuku", 0, e.Version, CardId: victim.Id));
        Eq(Honor(e, 0), before - 1, "his controller lost 1 Honor after the seppuku, though Katsuta is another player's");
        // Without Katsuta in play nothing is lost.
        var t = Batch336Support.Table(Phase.Action, 0);
        var v2 = Fx(t, CraneSamurai, "w737-victim2", 0, Zone.Home, d => d with { Keywords = ["Samurai"] }); v2.Dishonorable = true;
        var f = MatchEngine.FromTestState(t);
        var control = Honor(f, 0);
        Accept(f, new GameCommand("orderSeppuku", 0, f.Version, CardId: v2.Id));
        Eq(Honor(f, 0), control, "no Katsuta, no loss");
    }

    private static void EndToHostilitiesCard()
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, EndToHostilities, 0, Zone.Province, province: 1);
        ev.FaceUp = true; s.Players[0].Provinces[1].CardId = ev.Id;
        var e = MatchEngine.FromTestState(s);
        var real = (PlayerState)Call(e, "Player", 0)!;
        Eq(Call(e, "ResolveWave737Event", real, real.Provinces[1], Call(e, "Card", ev.Id)), false, "the Event resolves and the reveal carries on");
        var rule = e.State.TurnScopedRules.Single();
        Check(rule.Kind == TurnRuleKinds.EndTurnExtraDraw && rule.Amount == 1 && rule.Counter == TurnCounters.CardsIntoHand && rule.Max == 0 && rule.PlayerId is null, "an each-player extra draw while no card went into the hand");
        Check(e.State.Cards[ev.Id].Zone != Zone.EventInPlay, "the Event is spent, the rule stands");
        // At the end of a turn in which the player put no other card in his hand he draws two; otherwise one.
        MatchEngine EndTurnOf(bool putCard, out int drawn)
        {
            var t = Batch336Support.Table(Phase.Action, 0);
            var card = Add(t, EndToHostilities, 0, Zone.Province, province: 1); card.FaceUp = true; t.Players[0].Provinces[1].CardId = card.Id;
            var eng = MatchEngine.FromTestState(t);
            var pl = (PlayerState)Call(eng, "Player", 0)!;
            Call(eng, "ResolveWave737Event", pl, pl.Provinces[1], Call(eng, "Card", card.Id));
            if (putCard) Call(eng, "Draw", pl);
            var before = eng.State.Players[0].Hand.Count;
            Call(eng, "EndTurn");
            drawn = eng.State.Players[0].Hand.Count - before;
            return eng;
        }
        EndTurnOf(false, out var quiet);
        EndTurnOf(true, out var busy);
        Eq(quiet - busy, 1, $"a quiet turn draws one more card than a busy one ({quiet} against {busy})");
    }

    private static void WeatherCard()
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, Weather, 0, Zone.Province, province: 1);
        ev.FaceUp = true; s.Players[0].Provinces[1].CardId = ev.Id;
        var e = MatchEngine.FromTestState(s);
        var real = (PlayerState)Call(e, "Player", 0)!;
        Eq(Call(e, "ResolveWave737Event", real, real.Provinces[1], Call(e, "Card", ev.Id)), false, "the Event resolves and the reveal carries on");
        Eq(e.State.Cards[ev.Id].Zone, Zone.EventInPlay, "it is in play");
        Check(e.State.Cards[ev.Id].ExpiresAtTurnsTaken == e.State.Players[0].TurnsTaken + 1, "until its owner's next turn begins");
        // An attacker (player 1's, on his turn) assigned to attack cannot straighten in his controller's next Straighten Phase.
        var t = Batch336Support.Table(Phase.InfantryDeclaration, 1); t.PriorityPlayerId = 1;
        var w = Add(t, Weather, 0, Zone.EventInPlay);
        var attacker = Add(t, LionSamurai, 1, Zone.Home);
        var f = Assign(t, (attacker, 0));
        Check(f.State.Cards[attacker.Id].StraightenHolds.Any(h => h.Until == StraightenPoint.StraightenPhaseEnds && h.Owner == 1), "a hold until the end of his controller's next Straighten Phase");
        // Without the Event in play nothing holds him.
        var u = Batch336Support.Table(Phase.InfantryDeclaration, 1); u.PriorityPlayerId = 1;
        var free = Add(u, LionSamurai, 1, Zone.Home);
        var g = Assign(u, (free, 0));
        Check(g.State.Cards[free.Id].StraightenHolds.Count == 0, "no Event, no hold");
        _ = w;
    }

    private static void NatsuCard()
    {
        // The entry choice: a Personality, kept in the saved state.
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var personality = Add(s, CraneSamurai, 0, Zone.Home);
        var celestial = Add(s, Natsu, 0, Zone.Province);
        s.Players[0].Provinces[2].CardId = celestial.Id; celestial.ProvinceIndex = 2; celestial.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        e.CelestialEntryForTest(0, 2, celestial.Id, pauseReveal: true);
        Check(e.State.PendingEventChoice is { Kind: "p6-celestial" }, "the entry asks which Personality");
        var answer = e.QueryActions(0).First(a => a.Type == "eventChoice" && a.Command!.TargetId == personality.Id);
        Accept(e, answer.Command!);
        Eq(e.State.NatsuChoices![celestial.Id], personality.Id, "the choice is recorded");
        Check(JsonSerializer.Serialize(e.State).Contains("NatsuChoices"), "and saved");

        // The resolution: the chosen attacker comes home unbowed, the other bows.
        var t = Batch336Support.Table(Phase.Battle, 0);
        var chosen = Fx(t, CraneSamurai, "w737-chosen", 0, Zone.Battlefield, province: 0);
        var plain = Fx(t, CraneSamurai, "w737-plain", 0, Zone.Battlefield, province: 0);
        Fx(t, LionSamurai, "w737-defender", 1, Zone.Battlefield, province: 0);
        var cel = Add(t, Natsu, 0, Zone.Celestial);
        t.NatsuChoices = new() { [cel.Id] = chosen.Id };
        var f = MatchEngine.FromTestState(t);
        Call(f, "FinishBattleResolution", 0, 0, new[] { (CardInstance)Call(f, "Card", chosen.Id)!, (CardInstance)Call(f, "Card", plain.Id)! }, null, null);
        Check(!f.State.Cards[chosen.Id].Bowed && f.State.Cards[plain.Id].Bowed, "the chosen unit was not bowed by the resolution, the other was");

        // Battle, discard: only with enemy units at the battlefield; moves a Personality of yours there and straightens his unit.
        var u = Batch336Support.Table(Phase.Battle, 1); u.PriorityPlayerId = 0;
        var celestial2 = Add(u, Natsu, 0, Zone.Celestial);
        var home = Fx(u, CraneSamurai, "w737-home", 0, Zone.Home, bowed: true);
        var follower = Fx(u, "training-follower", "w737-home-follower", 0, Zone.Attached, bowed: true, parent: home.Id);
        var foe = Fx(u, LionSamurai, "w737-foe", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(u);
        Check(Batch336Support.Enabled(g, 0, Wave737Abilities.NatsuMove, celestial2.Id).TargetIds!.Contains(home.Id), "your Personality at home is a target");
        Accept(g, Batch336Support.Offer(g, 0, Wave737Abilities.NatsuMove, celestial2.Id, home.Id));
        Eq(g.State.Cards[celestial2.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        Check(g.State.Cards[home.Id].Zone == Zone.Battlefield && g.State.Cards[home.Id].ProvinceIndex == 0, "he moved to the current battlefield");
        Check(!g.State.Cards[home.Id].Bowed && !g.State.Cards[follower.Id].Bowed, "and his unit was straightened");
        // No enemy units: not usable.
        var v = Batch336Support.Table(Phase.Battle, 1); v.PriorityPlayerId = 0;
        var celestial3 = Add(v, Natsu, 0, Zone.Celestial);
        Add(v, CraneSamurai, 0, Zone.Home);
        var h = MatchEngine.FromTestState(v);
        Check(!Batch336Support.CanUse(h, 0, Wave737Abilities.NatsuMove, celestial3.Id), "with no enemy unit at the battlefield it cannot be used");
        _ = foe;
    }

    private static void StipendCard()
    {
        // Production: 2 Gold, 3 when paying for a Magistrate.
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var holding = Add(s, Stipend, 0, Zone.Home);
        var magistrate = Fx(s, CraneSamurai, "w737-magistrate", 0, Zone.Hand, d => d with { Keywords = ["Magistrate"], GoldCost = 3, Type = CardType.Personality });
        var plain = Fx(s, CraneSamurai, "w737-plain-recruit", 0, Zone.Hand, d => d with { GoldCost = 3 });
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(holding.Id, 0), 2, "it produces its printed 2 Gold");
        Eq(e.GoldProduced(holding.Id, 0, new PaymentSubject(e.State.Cards[magistrate.Id], PaymentKind.Recruit)), 3, "3 Gold when paying for a Magistrate");
        Eq(e.GoldProduced(holding.Id, 0, new PaymentSubject(e.State.Cards[plain.Id], PaymentKind.Recruit)), 2, "2 Gold for anything else");

        // The announce window: a performing Magistrate gets +1C until the action ends; a performer who is not a Magistrate opens nothing.
        MatchEngine Table(bool magistrate, out CardInstance stipend, out CardInstance actor)
        {
            var t = Batch336Support.Table(Phase.Action, 0);
            stipend = Add(t, Stipend, 0, Zone.Home);
            actor = Fx(t, CraneSamurai, "w737-actor", 0, Zone.Home, d => d with { Keywords = magistrate ? ["Magistrate", "Samurai"] : ["Samurai"] });
            actor.GrantedAbilities.Add(new("training-plain-open", "test-fixture", GrantDuration.Permanent));
            return MatchEngine.FromTestState(t);
        }
        var f = Table(true, out var watcher, out var mag);
        var chi = f.CardChiForTest(mag.Id);
        Accept(f, Batch336Support.Use(f, 0, "training-plain-open", mag.Id));
        Eq(f.State.PendingChoice?.Type, "announceReaction", "the announce window opens for the Stipend");
        var offered = f.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == watcher.Id && a.Enabled && a.Command!.AbilityId == Wave737Abilities.StipendAnnounce).ToArray();
        Check(offered.Length > 0 && offered.All(a => a.Command!.PerformerId == mag.Id), "offered for the performing Magistrate");
        Accept(f, offered[0].Command!);
        Check(f.State.Cards[mag.Id].Modifiers.Any(m => m.Stat == "Chi" && m.Amount == 1), "+1C while the announced action is unresolved");
        for (var guard = 0; guard < 4 && f.State.PendingChoice is { Type: "announceReaction" }; guard++) Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        Check(!f.State.Cards[mag.Id].Modifiers.Any(m => m.Stat == "Chi" && m.Amount == 1), "and gone when it ended");
        Eq(f.CardChiForTest(mag.Id), chi, "Chi is back where it was");
        var g = Table(false, out _, out var plainActor);
        Accept(g, Batch336Support.Use(g, 0, "training-plain-open", plainActor.Id));
        Check(g.State.PendingChoice is not { Type: "announceReaction" }, "a performer who is not a Magistrate opens no window");
    }

    private static void CrystalTearsCard()
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var tears = Add(s, CrystalTears, 0, Zone.Hand);
        var old = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var strong = Fx(s, LionSamurai, "w737-strong", 1, Zone.Battlefield, d => d with { Force = 5 }, province: 0);
        var weak = Fx(s, LionSamurai, "w737-weak", 1, Zone.Battlefield, d => d with { Force = 2 }, province: 0);
        var ninja = Fx(s, LionSamurai, "w737-ninja", 1, Zone.Battlefield, d => d with { Force = 1, Keywords = ["Ninja"] }, province: 0);
        Fx(s, CraneSamurai, "w737-mine", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var options = Batch336Support.Options(e, 0, Wave737Abilities.CrystalTearsPlay, tears.Id);
        Check(options.Any(o => o.Enabled), "it is offered from the hand");
        // The target is optional; a weak Personality with no keyword is not a legal choice, a strong one and a Ninja are.
        var strongPick = Batch336Support.Use(e, 0, Wave737Abilities.CrystalTearsPlay, tears.Id, selections: Batch336Support.Pick(Wave737Abilities.CrystalTearsSlot, strong.Id));
        NoChange(e, Batch336Support.Use(e, 0, Wave737Abilities.CrystalTearsPlay, tears.Id, selections: Batch336Support.Pick(Wave737Abilities.CrystalTearsSlot, weak.Id)), "a Personality with 3 or less Force and no keyword");
        Accept(e, strongPick);
        Eq(e.State.Cards[tears.Id].Zone, Zone.Battlefield, "the Terrain is in play");
        Eq(e.State.Cards[old.Id].Zone, Zone.FateDiscard, "the Terrain that was there was destroyed");
        Check(e.State.Cards[strong.Id].Bowed, "the chosen Personality is bowed");
        Eq(e.State.CrystalTearsTargets![tears.Id], strong.Id, "the target is remembered");
        // After this Terrain is destroyed: he straightens.
        var ended = Batch336Support.Edit(e, st => { });
        Call(ended, "DiscardTerrain", ended.State.Cards[tears.Id]);
        ended.SweepFactsForTest();
        Check(!ended.State.Cards[strong.Id].Bowed, "when the Crystal Tears was destroyed he straightened");
        Check(ended.State.CrystalTearsTargets is null, "and the record is cleared");
        // No target at all is allowed.
        var t = Batch336Support.Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var tears2 = Add(t, CrystalTears, 0, Zone.Hand);
        Fx(t, LionSamurai, "w737-foe2", 1, Zone.Battlefield, province: 0);
        Fx(t, CraneSamurai, "w737-mine2", 0, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Use(f, 0, Wave737Abilities.CrystalTearsPlay, tears2.Id));
        Eq(f.State.Cards[tears2.Id].Zone, Zone.Battlefield, "played with no target");
        Check(f.State.CrystalTearsTargets is null, "nothing to straighten later");
        _ = ninja;
    }

    private static void JungleCard()
    {
        // The recruit discount: only a Nonhuman Personality bought from the Region's Province, by its owner.
        MatchEngine Rig(string keyword, int regionProvince, out CardInstance card)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            card = s.Cards[s.Players[0].Provinces[0].CardId!];
            card.DefinitionId = LionSamurai; card.FaceUp = true;
            var def = CardCatalog.Get(LionSamurai);
            card.Printed = def with { Id = "w737-recruit", Keywords = keyword == "" ? [] : [keyword], GoldCost = 4 };
            for (var i = 0; i < 8; i++) Add(s, Marketplace, 0, Zone.Home);
            var region = Add(s, Jungle, 0, Zone.Region, province: regionProvince);
            region.ProvinceIndex = regionProvince;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("Nonhuman", 0, out var nonhuman);
        var plain = Rig("", 0, out var human);
        var away = Rig("Nonhuman", 2, out var elsewhere);
        var discounted = e.QuotePurchase(0, nonhuman.Id).Cost;
        Eq(plain.QuotePurchase(0, human.Id).Cost - discounted, 1, "a Nonhuman from this Province costs 1 less than a Human");
        Eq(away.QuotePurchase(0, elsewhere.Id).Cost - discounted, 1, "and 1 less than the same Nonhuman from another Province");
    }

    // ---- P8: the Interrupts ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string DefinitionWith(string ability) => CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains(ability) == true).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();

    private static GameCommand Reaction(MatchEngine e, int player, string ability)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability).ToArray();
        Check(offers.Length > 0, $"{ability} was not offered to player {player}: " + string.Join("; ", e.QueryActions(player).Select(o => $"{o.Type}/{o.Label}/{o.Enabled}/{o.Reason}")));
        return offers[0].Command!;
    }

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" or "attackReaction" }, "the window closed");
    }

    // Player 1 (the Attacker) moves player 0's Personality from the current battlefield 1 to battlefield 3.
    private static (MatchState S, CardInstance Source, CardInstance Target) MoveRig()
    {
        var s = Batch336Support.Table(); s.PriorityPlayerId = 1; s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 3];
        var source = Add(s, "training-move-source", 1, Zone.Battlefield, province: 1);
        var target = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        return (s, source, target);
    }

    private static void TaigoCard()
    {
        var (recruit, card) = Recruit(Taigo);
        var honor = Honor(recruit, 0);
        PurchaseAll(recruit, card);
        Eq(Honor(recruit, 0), honor - 3, "Taigo costs 3 Honor on entering play");

        var (s, source, target) = MoveRig();
        var taigo = Add(s, Taigo, 0, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        var move = Batch336Support.Enabled(e, 1, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 };
        Accept(e, move);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for Taigo's controller: the action moves a unit");
        Accept(e, Reaction(e, 0, Wave737Abilities.TaigoInterrupt));
        Check(e.State.Cards[taigo.Id].Bowed, "Taigo bowed as the cost");
        ClosePassing(e);
        Eq(e.State.Cards[target.Id].ProvinceIndex, 3, "the action moved the unit");
        if (e.State.PendingEventChoice is null) e.OpenQueuedChoicesForTest(e.State.PriorityPlayerId);
        Check(e.State.PendingChoice is { Type: "eventChoice" } && e.State.PendingEventChoice is { Kind: "p8-watch", PlayerId: 0 }, "Taigo's controller is asked which card to destroy");
        var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Check(options.Any(a => a.Command!.TargetId == target.Id) && options.Any(a => a.Command!.Choice == "none"), "the moved unit's card or none");
        Accept(e, options.First(a => a.Command!.TargetId == target.Id).Command!);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the card without attachments was destroyed");

        // A unit with an attachment is not itself a choice, but its attachment is: the Follower can be destroyed, the host cannot.
        var (s2, source2, target2) = MoveRig();
        Add(s2, Taigo, 0, Zone.Battlefield, province: 1);
        var follower = Fx(s2, "training-follower", "w737-taigo-follower", 0, Zone.Attached, parent: target2.Id);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Batch336Support.Enabled(f, 1, "training-move-anywhere", source2.Id).Command! with { TargetId = target2.Id, Province = 3 });
        Accept(f, Reaction(f, 0, Wave737Abilities.TaigoInterrupt));
        ClosePassing(f);
        if (f.State.PendingEventChoice is null) f.OpenQueuedChoicesForTest(f.State.PriorityPlayerId);
        var choices = f.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Check(choices.Any(a => a.Command!.TargetId == follower.Id) && !choices.Any(a => a.Command!.TargetId == target2.Id), "only the card without attachments is offered");

        // An action that moves nothing opens no window for Taigo.
        var t = Batch336Support.Table(); t.PriorityPlayerId = 1;
        var fires = Add(t, DefinitionWith("fires-of-turmoil"), 1, Zone.Hand);
        var victim = Add(t, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        Add(t, Taigo, 0, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(t);
        Accept(g, Batch336Support.Offer(g, 1, "fires-of-turmoil", fires.Id, victim.Id));
        Check(g.State.PendingChoice is not { Type: "beforeActionReaction" }, "no unit moves, so no Interrupt window: " + g.State.PendingChoice?.Type);
    }

    private static void ReverseFateCard()
    {
        foreach (var (ability, delta) in new[] { (Wave737Abilities.ReverseFatePlus, 1), (Wave737Abilities.ReverseFateMinus, -1) })
        {
            var s = Batch336Support.Table(); s.PriorityPlayerId = 1;
            var fires = Add(s, DefinitionWith("fires-of-turmoil"), 1, Zone.Hand);
            var victim = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            var host = Fx(s, CraneSamurai, "w737-void-shugenja", 0, Zone.Battlefield, d => d with { Keywords = ["Shugenja", "Void"] }, province: 0);
            var spell = Add(s, ReverseFate, 0, Zone.Attached, province: 0, parent: host.Id);
            var topId = s.Players[1].FateDeck[0];
            var printed = CardCatalog.Get(s.Cards[topId].DefinitionId).Focus;
            var e = MatchEngine.FromTestState(s);
            Accept(e, Batch336Support.Offer(e, 1, "fires-of-turmoil", fires.Id, victim.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the Void Shugenja's Spell: the action discards a top card");
            Accept(e, Reaction(e, 0, ability));
            Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as the cost");
            ClosePassing(e);
            Eq(e.State.Cards[topId].Zone, Zone.FateDiscard, "the top card was discarded");
            Eq(e.FocusValueForTest(topId), Math.Max(0, printed + delta), $"its Focus Value read outside a duel moved by {delta}");
            Eq(CardCatalog.Get(e.State.Cards[topId].DefinitionId).Focus, printed, "the printed value a duel reads is untouched");
        }
        // Not a Void Shugenja, or an action that discards nothing: no window.
        var t = Batch336Support.Table(); t.PriorityPlayerId = 1;
        var fires2 = Add(t, DefinitionWith("fires-of-turmoil"), 1, Zone.Hand);
        var victim2 = Add(t, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var water = Fx(t, CraneSamurai, "w737-water-shugenja", 0, Zone.Battlefield, d => d with { Keywords = ["Shugenja", "Water"] }, province: 0);
        Add(t, ReverseFate, 0, Zone.Attached, province: 0, parent: water.Id);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Offer(f, 1, "fires-of-turmoil", fires2.Id, victim2.Id));
        Check(f.State.PendingChoice is not { Type: "beforeActionReaction" }, "a Shugenja who is not Void cannot use it");
        var (u, source, target) = MoveRig();
        var vh = Fx(u, CraneSamurai, "w737-void2", 0, Zone.Battlefield, d => d with { Keywords = ["Shugenja", "Void"] }, province: 1);
        Add(u, ReverseFate, 0, Zone.Attached, province: 1, parent: vh.Id);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Batch336Support.Enabled(g, 1, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 });
        Check(g.State.PendingChoice is not { Type: "beforeActionReaction" }, "an action that discards nothing opens no window");
    }

    private static void JungleInterruptCard()
    {
        // Player 1 attacks player 0's province 0 and moves his own Personality from home to the battlefield; player 0's Region at province 0 negates it.
        MatchEngine Rig(int regionProvince, out CardInstance mover, out CardInstance source)
        {
            var s = Batch336Support.Table(); s.PriorityPlayerId = 1; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0, 2];
            source = Add(s, "training-move-source", 1, Zone.Battlefield, province: 0);
            mover = Add(s, "training-battle-bower", 1, Zone.Home);
            Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            var region = Add(s, Jungle, 0, Zone.Region, province: regionProvince);
            region.ProvinceIndex = regionProvince;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(0, out var mover, out var source);
        Accept(e, Batch336Support.Offer(e, 1, "training-move-current", source.Id, mover.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the Region's owner: units move to its Province's battlefield");
        var region0 = e.State.Cards.Values.Single(c => c.DefinitionId == Jungle);
        Accept(e, Reaction(e, 0, Wave737Abilities.JungleInterrupt));
        Check(e.State.Cards[region0.Id].Bowed, "the Region bowed as the cost");
        ClosePassing(e);
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the movement into the attacking army was negated: he stays home");
        // A battlefield that is not this Province's: no window.
        var f = Rig(2, out var mover2, out var source2);
        Accept(f, Batch336Support.Offer(f, 1, "training-move-current", source2.Id, mover2.Id));
        Check(f.State.PendingChoice is not { Type: "beforeActionReaction" }, "another Province's Region opens no window");
        Eq(f.State.Cards[mover2.Id].Zone, Zone.Battlefield, "and the move happened");
    }
}
