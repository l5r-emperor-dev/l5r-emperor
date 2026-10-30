using System.Reflection;
using L5R.Core;
using static RegressionAssertions;

// Issue #734, wave batch 3 (src/L5R.Core/Wave734.cs; waves personality-wave-148, item-wave-61, spell-wave-68, strategy-wave-132, holding-wave-66, event-wave-33): the stat-term and Experienced cards,
// proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane, player 1 the Lion; Battle phase, player 1 the Attacker at province 0) and a
// refused command must leave the state unchanged.
internal static partial class Wave734Tests
{
    private const string Kachiko = "oracle-l5r-10481", Nitoshi = "oracle-l5r-10480", Hoturi = "oracle-l5r-10448", Hantei = "oracle-l5r-10500", Norimichi = "oracle-l5r-10651", Tsuke = "oracle-l5r-10475",
        Karasu = "oracle-l5r-10506", Kokujin = "oracle-l5r-10486", Mumoku = "oracle-l5r-10322", Chizuru = "oracle-l5r-10871", Tanihara = "oracle-l5r-10312", Seki = "oracle-l5r-10457", JiYun = "oracle-l5r-10329",
        Armor = "oracle-l5r-10529", Kshatriya = "oracle-l5r-10691", Hammer = "oracle-l5r-10908", Barrier = "oracle-l5r-10354", Ready = "oracle-l5r-10389", Alliance = "oracle-l5r-10965",
        Warded = "oracle-l5r-10404", Farm = "oracle-l5r-10829", Riding = "oracle-l5r-10605";
    private static readonly string[] Wired = [Nitoshi, Hoturi, Hantei, Norimichi, Tsuke, Karasu, Mumoku, Chizuru, Tanihara, Seki, JiYun, Armor, Kshatriya, Hammer, Barrier, Ready, Alliance, Farm, Riding];
    private const string Lion = "training-lion-samurai", Crane = "training-crane-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, int province = 0, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province);
    private static int Force(MatchEngine e, CardInstance card) => e.CardForceForTest(card.Id);
    private static MatchState Battle() => Batch336Support.Table(Phase.Battle, 1);
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);
    // A copy of a training card under its own id with the keywords given (the training cards carry none of the keywords the published cards read).
    private static CardInstance Variant(MatchState s, string baseId, string id, int owner, Zone zone, params string[] keywords)
    {
        var card = Add(s, baseId, owner, zone, province: zone == Zone.Battlefield ? 0 : null);
        card.Printed = CardCatalog.Get(baseId) with { Id = id, Keywords = keywords };
        return card;
    }

    public static void Run(TestRunner runner) => runner.Run("wave-734", [
        ("Published as printed: nineteen cards in six profiles with their stats, abilities and effects; Kokujin and Warded Premises are not published (Kachiko was wired afterwards, issue #778)", PublishedShape),
        ("Mumoku no Oni and Shosuro Tanihara: while attacking, the Province reads no higher than its printed strength", ProvinceCeiling),
        ("Usagi Seki: +5F only while another player's action is resolving", SekiResolving),
        ("Isawa Norimichi: other Earth Shugenja of his controller +1F; Battle: this Province +2 or -2 strength", NorimichiTerms),
        ("Kshatriya Artifacts: opposing Fudo and Fallen Personalities have -1F for each Item", KshatriyaTerm),
        ("Stonemason's Hammer: +1F to a Siege wielder; Battle, bow: this Province +2 strength", HammerTerms),
        ("Well-Defended Farm: Ashigaru Personalities and Followers +1F while opposed; produces 2 Gold", FarmTerms),
        ("Riding to the Rescue: Cavalry Personalities and Followers +1F while attacking and opposed, for the turn", RidingTerm),
        ("Armor of the Mad: its wielder has a maximum Personal Honor of 0; Battle, bow: a Madness token on one or two enemy cards", ArmorTerms),
        ("Utaku Ji-Yun: dishonorable Paragons keep their Personal Honor; after engaging, an opposing Personality may not use his abilities", JiYunTerms),
        ("Bayushi Nitoshi: after he enters play every player loses 3 Honor", NitoshiEntry),
        ("Hantei XVI: the Honor Requirement is ignored while his controller has the Favor; after entering play a non-Unique Political Strategy is searched for", HanteiTerms),
        ("Shosuro Chizuru: discard a card for a Force or Personal Honor bonus equal to its Focus Value; the target's Personal Honor is capped at 4", ChizuruTerms),
        ("Ready the Defenses: other players' units at the province's battlefield -1F; the player's own +1F when the performer is a Tactician", ReadyTerms),
        ("Unseemly Alliance: bows a Courtier; every Personality has -1F for each Clan alignment in its army", AllianceTerms),
        ("Doji Hoturi: an Iaijutsu duel the honorable may refuse by being dishonored and moved home; the loser is destroyed", HoturiDuel),
        ("Isawa Tsuke: a Fire Limited duel both players must focus in at least once; the loser is destroyed", TsukeDuel),
        ("Iuchi Karasu: Home Battle switches two Personalities of one player and straightens those who are neither Fallen nor Nonhuman", KarasuSwitch),
        ("The Sky's Barrier: an attacking Personality becomes Elite and contributes no Force while unbowed; Air gains 2 Honor", BarrierTerms),
    ]);

    // ---- the tests ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Nitoshi] = (CardType.Personality, ["w734-nitoshi-attack"], "w734-nitoshi"),
            [Hoturi] = (CardType.Personality, ["w734-hoturi-duel"], "none"),
            [Hantei] = (CardType.Personality, ["w734-hantei-search"], "favor-ignores-honor-requirement"),
            [Norimichi] = (CardType.Personality, ["w734-norimichi-raise", "w734-norimichi-lower"], "w734-norimichi"),
            [Tsuke] = (CardType.Personality, ["w734-tsuke-duel"], "none"),
            [Karasu] = (CardType.Personality, ["w734-karasu-switch"], "none"),
            [Mumoku] = (CardType.Personality, [], "w734-province-cap"),
            [Chizuru] = (CardType.Personality, ["w734-chizuru-force", "w734-chizuru-honor"], "w734-chizuru"),
            [Tanihara] = (CardType.Personality, [], "w734-province-cap"),
            [Seki] = (CardType.Personality, [], "w734-seki"),
            [JiYun] = (CardType.Personality, ["w734-jiyun-lock"], "w734-jiyun"),
            [Armor] = (CardType.Item, ["w734-armor-madness"], "w734-armor"),
            [Kshatriya] = (CardType.Item, [], "w734-kshatriya"),
            [Hammer] = (CardType.Item, ["w734-hammer-province"], "w734-hammer"),
            [Barrier] = (CardType.Spell, ["w734-barrier"], "none"),
            [Ready] = (CardType.Strategy, ["w734-ready-defenses"], "none"),
            [Alliance] = (CardType.Strategy, ["w734-alliance-bow"], "none"),
            [Farm] = (CardType.Holding, [], "w734-farm"),
            [Riding] = (CardType.Event, [], "w734-riding"),
        };
        Eq(expected.Count, Wired.Length, "nineteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!string.IsNullOrWhiteSpace(d.Text) && !d.CreatedOnly, $"{d.Name} prints its text and is playable");
        }
        var mumoku = CardCatalog.Get(Mumoku);
        Check(mumoku.Force == 10 && mumoku.Chi == 3 && mumoku.GoldCost == 11 && mumoku.EntryHonorLoss == 4 && mumoku.Keywords!.Contains("Elite"), "Mumoku no Oni: 10F, 3C, 11 Gold, loses 4 Honor, Elite");
        var seki = CardCatalog.Get(Seki);
        Check(seki.Force == 2 && seki.Chi == 2 && seki.GoldCost == 4, "Seki's stats");
        Check(CardCatalog.Get(Armor).AttachGrantsKeyword == "Fallen", "Armor of the Mad gives its wielder Fallen");
        Check(CardCatalog.Get(Farm).GoldProduction == 2, "Well-Defended Farm produces 2 Gold");
        Check(CardCatalog.Cards.ContainsKey(Kokujin) && CardCatalog.Cards.ContainsKey(Warded), "Kokujin was wired afterwards (issue #827); Warded Premises was wired afterwards (Wave835.cs, issue #835) (Kachiko was wired afterwards, issue #778)");
        Check(TraitReactions.Is("w734-hantei-search") && !TraitReactions.Is("w734-jiyun-lock"), "Hantei's search is a trait; Ji-Yun's Reaction is not");
        Check(Experienced.Level(CardCatalog.Get(Hoturi).Keywords) == -1 && Experienced.IsInexperienced(CardCatalog.Get(Hoturi).Keywords) && Experienced.Level(CardCatalog.Get(Tsuke).Keywords) == 1, "Inexperienced is level -1, ExperiencedCoM is level 1");
    }

    private static void ProvinceCeiling()
    {
        foreach (var id in new[] { Mumoku, Tanihara })
        {
            var s = Battle();
            var defender = s.Players[0].Provinces[0];
            var printed = defender.PrintedStrength;
            defender.Strength = printed + 5; // raised above the printed strength
            var oni = Bf(s, id, 1);
            var e = Engine(s);
            Eq(e.ProvinceStrengthForTest(0, 0), printed, $"{CardCatalog.Get(id).Name} attacking: the raise reads as the printed strength");
            // Not attacking (his controller defends), and not at that battlefield: no ceiling.
            var d = Battle();
            d.Players[1].Provinces[0].Strength = d.Players[1].Provinces[0].PrintedStrength + 5;
            Bf(d, id, 0);
            Eq(Engine(d).ProvinceStrengthForTest(1, 0), d.Players[1].Provinces[0].PrintedStrength + 5, "defending: no ceiling");
            var away = Battle();
            away.Players[0].Provinces[0].Strength = printed + 5;
            Bf(away, id, 1, province: 1);
            Eq(Engine(away).ProvinceStrengthForTest(0, 0), printed + 5, "attacking another battlefield: this Province is untouched");
            // Below the printed strength nothing is raised: a ceiling is never a floor.
            var low = Battle();
            low.Players[0].Provinces[0].Strength = printed - 2;
            Bf(low, id, 1);
            Eq(Engine(low).ProvinceStrengthForTest(0, 0), printed - 2, "a lowered Province keeps its lowered strength");
        }
    }

    private static void SekiResolving()
    {
        var s = Battle();
        var seki = Bf(s, Seki, 0);
        var e = Engine(s);
        Eq(Force(e, seki), 2, "Seki's printed Force outside any action");
        // Another player's action resolving: the Attacker's command is the running one.
        var field = typeof(MatchEngine).GetField("_runningCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(e, new GameCommand("useAbility", 1, e.Version, AbilityId: "training-weapon-attack"));
        Eq(Force(e, seki), 7, "+5F while another player's action resolves");
        field.SetValue(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "training-weapon-attack"));
        Eq(Force(e, seki), 2, "his own controller's action gives nothing");
        field.SetValue(e, null);
        Eq(Force(e, seki), 2, "and nothing again once no action resolves");
        // End to end: a Melee 6 Attack (Nitoshi's) fails to destroy him but destroys an ordinary Force-2 Personality.
        var a = Battle();
        var nitoshi = Bf(a, Nitoshi, 1);
        var seki2 = Bf(a, Seki, 0);
        var ae = Engine(a);
        Accept(ae, Batch336Support.Offer(ae, 1, "w734-nitoshi-attack", nitoshi.Id, seki2.Id));
        PassWindow(ae);
        Check(ae.State.Cards[seki2.Id].Zone == Zone.Battlefield, "Melee 6 does not destroy Seki: 2F + 5F = 7F");
        var b = Battle();
        var nitoshi2 = Bf(b, Nitoshi, 1);
        var plain = Bf(b, Crane, 0);
        Batch336Support.Force(plain, 2 - CardCatalog.Get(Crane).Force);
        var be = Engine(b);
        Accept(be, Batch336Support.Offer(be, 1, "w734-nitoshi-attack", nitoshi2.Id, plain.Id));
        PassWindow(be);
        Check(be.State.Cards[plain.Id].Zone != Zone.Battlefield, "Melee 6 destroys an ordinary 2F Personality");
    }

    private static void PassWindow(MatchEngine e)
    {
        for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" }; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void NorimichiTerms()
    {
        var s = Battle();
        var master = Bf(s, Norimichi, 0);
        var earth = Variant(s, "training-shugenja", "w734-earth", 0, Zone.Home, "Shugenja", "Earth");
        var fire = Variant(s, "training-shugenja", "w734-fire", 0, Zone.Home, "Shugenja", "Fire");
        var enemyEarth = Variant(s, "training-shugenja", "w734-earth2", 1, Zone.Home, "Shugenja", "Earth");
        var e = Engine(s);
        var printedEarth = CardCatalog.Get("training-shugenja").Force;
        Eq(Force(e, earth), printedEarth + 1, "his other Earth Shugenja +1F");
        Eq(Force(e, master), CardCatalog.Get(Norimichi).Force, "not himself");
        Eq(Force(e, fire), CardCatalog.Get("training-shugenja").Force, "not a Fire Shugenja");
        Eq(Force(e, enemyEarth), printedEarth, "not the other player's Shugenja");
        // Battle: this Province +2 or -2 strength.
        var baseStrength = e.ProvinceStrengthForTest(0, 0);
        var raise = Engine(Battle());
        var rs = Battle(); rs.PriorityPlayerId = 0; var m2 = Bf(rs, Norimichi, 0);
        raise = Engine(rs);
        Accept(raise, Batch336Support.Use(raise, 0, "w734-norimichi-raise", m2.Id));
        Eq(raise.ProvinceStrengthForTest(0, 0), baseStrength + 2, "+2 strength");
        var ls = Battle(); ls.PriorityPlayerId = 0; var m3 = Bf(ls, Norimichi, 0);
        var lower = Engine(ls);
        Accept(lower, Batch336Support.Use(lower, 0, "w734-norimichi-lower", m3.Id));
        Eq(lower.ProvinceStrengthForTest(0, 0), baseStrength - 2, "-2 strength");
    }

    private static void KshatriyaTerm()
    {
        var s = Battle();
        var fallen = Bf(s, "training-lion-samurai", 1);
        fallen.Printed = CardCatalog.Get("training-lion-samurai") with { Id = "w734-fallen", Keywords = ["Fallen"] };
        var human = Bf(s, "training-lion-samurai", 1);
        var wielder = Bf(s, Crane, 0);
        var item = Add(s, Kshatriya, 0, Zone.Attached, parent: wielder.Id);
        var item2 = Add(s, Kshatriya, 0, Zone.Attached, parent: wielder.Id);
        var e = Engine(s);
        var printed = CardCatalog.Get("training-lion-samurai").Force;
        Eq(Force(e, fallen), printed - 2, "two Items: -2F to the opposing Fallen Personality");
        Eq(Force(e, human), printed, "a plain Personality is untouched");
        // Not opposing: the Fallen Personality is at home.
        var home = Battle();
        var athome = Add(home, "training-lion-samurai", 1, Zone.Home);
        athome.Printed = CardCatalog.Get("training-lion-samurai") with { Id = "w734-fallen2", Keywords = ["Fallen"] };
        var w2 = Bf(home, Crane, 0);
        Add(home, Kshatriya, 0, Zone.Attached, parent: w2.Id);
        Eq(Force(Engine(home), athome), printed, "not opposing: no penalty");
    }

    private static void HammerTerms()
    {
        var s = Battle(); s.PriorityPlayerId = 0;
        var siege = Bf(s, Crane, 0);
        siege.Printed = CardCatalog.Get(Crane) with { Id = "w734-siege", Keywords = ["Siege"] };
        var plain = Bf(s, Crane, 0);
        var h1 = Add(s, Hammer, 0, Zone.Attached, province: 0, parent: siege.Id);
        var h2 = Add(s, Hammer, 0, Zone.Attached, province: 0, parent: plain.Id);
        var e = Engine(s);
        var def = CardCatalog.Get(Hammer);
        Eq(Force(e, siege), CardCatalog.Get(Crane).Force + def.Force + 1, "a Siege wielder: the Item's +2F and +1F");
        Eq(Force(e, plain), CardCatalog.Get(Crane).Force + def.Force, "a wielder who is not Siege: only the Item's +2F");
        var strength = e.ProvinceStrengthForTest(0, 0);
        Accept(e, Batch336Support.Use(e, 0, "w734-hammer-province", h1.Id));
        Eq(e.ProvinceStrengthForTest(0, 0), strength + 2, "this Province +2 strength");
        Check(e.State.Cards[h1.Id].Bowed, "the Hammer bowed to pay");
    }

    private static void FarmTerms()
    {
        var s = Battle();
        var ashigaru = Bf(s, Crane, 0);
        ashigaru.Printed = CardCatalog.Get(Crane) with { Id = "w734-ashigaru", Keywords = ["Ashigaru"] };
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: ashigaru.Id);
        follower.Printed = CardCatalog.Get("training-follower") with { Id = "w734-ashigaru-follower", Keywords = ["Ashigaru"] };
        var other = Bf(s, Crane, 0);
        Add(s, Farm, 0, Zone.Home);
        Bf(s, Lion, 1);
        var e = Engine(s);
        Eq(Force(e, ashigaru), CardCatalog.Get(Crane).Force + 1, "an opposed Ashigaru Personality +1F");
        Eq(Force(e, follower), CardCatalog.Get("training-follower").Force + 1, "an opposed Ashigaru Follower +1F");
        Eq(Force(e, other), CardCatalog.Get(Crane).Force, "a Personality who is not Ashigaru");
        // Unopposed: no bonus.
        var u = Battle();
        var lone = Bf(u, Crane, 0);
        lone.Printed = CardCatalog.Get(Crane) with { Id = "w734-ashigaru2", Keywords = ["Ashigaru"] };
        Add(u, Farm, 0, Zone.Home);
        Eq(Force(Engine(u), lone), CardCatalog.Get(Crane).Force, "unopposed: nothing");
    }

    private static void RidingTerm()
    {
        var s = Battle();
        var rider = Bf(s, Lion, 1);
        rider.Printed = CardCatalog.Get(Lion) with { Id = "w734-rider", Keywords = ["Cavalry"] };
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: rider.Id);
        follower.Printed = CardCatalog.Get("training-follower") with { Id = "w734-rider-follower", Keywords = ["Cavalry"] };
        var foot = Bf(s, Lion, 1);
        var defender = Bf(s, Crane, 0);
        var event1 = Add(s, Riding, 1, Zone.EventInPlay);
        var e = Engine(s);
        Eq(Force(e, rider), CardCatalog.Get(Lion).Force + 1, "an attacking, opposed Cavalry Personality +1F");
        Eq(Force(e, follower), CardCatalog.Get("training-follower").Force + 1, "and his Cavalry Follower");
        Eq(Force(e, foot), CardCatalog.Get(Lion).Force, "a Personality who is not Cavalry");
        // Defending (the other player's Cavalry): nothing, and an Event of the other player gives nothing.
        defender.Printed = CardCatalog.Get(Crane) with { Id = "w734-defender", Keywords = ["Cavalry"] };
        Eq(Force(Engine(s), defender), CardCatalog.Get(Crane).Force, "a defending Cavalry Personality has no bonus");
        // Unopposed: no bonus.
        var u = Battle();
        var lone = Bf(u, Lion, 1);
        lone.Printed = CardCatalog.Get(Lion) with { Id = "w734-rider2", Keywords = ["Cavalry"] };
        Add(u, Riding, 1, Zone.EventInPlay);
        Eq(Force(Engine(u), lone), CardCatalog.Get(Lion).Force, "unopposed: nothing");
    }
}
