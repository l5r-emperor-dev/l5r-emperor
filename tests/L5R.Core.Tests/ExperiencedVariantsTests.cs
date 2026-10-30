using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Issue #672, primitive P23: the "ExperiencedCoM" and "Inexperienced" forms of the Experienced keyword (Experienced.cs), and the two Inexperienced Personalities of Coils of Madness that
// prove them (ExperiencedVariants.cs): Hida O-Ushi and Tsuruchi.
internal static class ExperiencedVariantsTests
{
    private const string OUshi = "oracle-l5r-10437", Tsuruchi = "oracle-l5r-10469";
    private const string NitoshiBase = "oracle-l5r-850", NitoshiInexperienced = "oracle-l5r-10248";
    private const string Bow = ExperiencedVariantAbilities.OUshiBow, Arrows = ExperiencedVariantAbilities.TsuruchiArrows, Attack = ExperiencedVariantAbilities.TsuruchiAttack;

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Level: none 0, Inexperienced -1, Experienced 1, ExperiencedCoM 1, Experienced N is N; the highest form applies", Levels),
            ("Base title: \" - inexperienced\", \" - exp\", \" - expN\" and \" Experienced[ N]\" are all removed; a plain title is unchanged", BaseTitles),
            ("Deck names: each level of one title is its own construction card, and an ExperiencedCoM card keeps its catalogue name", DeckNames),
            ("Deck construction accepts one Unique copy of each version of a title, and still refuses two of one", DeckConstruction),
            ("In play, ExperiencedCoM satisfies an Experienced keyword check, and removing Experienced removes it", KeywordInPlay),
            ("Overlay: an Experienced version overlays the Inexperienced or the plain one, the plain one overlays the Inexperienced one and not the reverse; ExperiencedCoM overlays level 0 but not Experienced", OverlayLegality),
            ("Published as printed: wave 128 carries O-Ushi and Tsuruchi with their Coils of Madness stats, keywords and abilities, and its manifest names them", PublishedShape),
            ("Hida O-Ushi: Limited, bows a target Personality with lower Force (either player's); equal or higher Force is refused and nothing changes; once per turn", OUshiBows),
            ("Hida O-Ushi: a Stalwart Personality's bowing is negated, and the action is still spent", OUshiStalwart),
            ("Tsuruchi, Open: Arrow tokens until he has exactly five (from none, from three, and none more at five); a token gives no Force", TsuruchiTokens),
            ("Tsuruchi, Battle: the engine lists zero to all of his tokens; the number chosen is destroyed and the Ranged Attack has twice that strength", TsuruchiAttackChoice),
            ("Tsuruchi, Battle: only his player answers, only with an offered number; a refused answer changes nothing", TsuruchiChoiceGuards),
            ("Tsuruchi, Battle: three times per turn, and not a fourth; with no token nothing is asked and the Attack has strength 0", TsuruchiThreeTimes),
            ("Tsuruchi, Battle: the Attack still opens its reaction window at the chosen strength, and the action finishes when the window closes", TsuruchiDeferredAttack),
        ];
        runner.Run("experienced-variants", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // The catalogue's deck pool as the deck builder reads it (CustomPractice): catalogue names, with a published card's own keywords.
    private sealed record PoolCard(string Id, string Name, string[] Types, string[] DeckTypes, string[] Keywords);
    private sealed record PoolFile(PoolCard[] Cards);
    private static readonly Lazy<DeckCardInfo[]> PoolCards = new(() =>
    {
        var file = System.Text.Json.JsonSerializer.Deserialize<PoolFile>(File.ReadAllText(Path.Combine(RepoRoot(), "content", "deck-pool.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return file.Cards.Select(c => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, CardCatalog.Cards.GetValueOrDefault(c.Id)?.Keywords?.ToArray() ?? c.Keywords,
            CardCatalog.Cards.ContainsKey(c.Id), CardCatalog.Cards.GetValueOrDefault(c.Id)?.CreatedOnly ?? false)).ToArray();
    });
    private static DeckCardInfo[] Pool() => PoolCards.Value;

    private static void Levels()
    {
        Eq(Experienced.Level([]), 0, "no keyword");
        Eq(Experienced.Level(["Unique", "Inexperienced"]), -1, "Inexperienced is level -1 (Comprehensive Rules)");
        Eq(Experienced.Level(["Experienced"]), 1, "Experienced");
        Eq(Experienced.Level(["ExperiencedCoM"]), 1, "ExperiencedCoM");
        Eq(Experienced.Level(["Experienced CoM"]), 1, "Experienced CoM, spaced");
        Eq(Experienced.Level(["experiencedcom"]), 1, "case is ignored, as for the other forms");
        Eq(Experienced.Level(["Experienced 3"]), 3, "Experienced 3");
        Eq(Experienced.Level(["ExperiencedCoM", "Experienced 2"]), 2, "the highest form applies");
        Check(Experienced.IsInexperienced(["Unique", "Inexperienced"]) && !Experienced.IsInexperienced(["Experienced"]) && !Experienced.IsInexperienced(null), "IsInexperienced");
        // The catalogue's own records: every card it lists with either form.
        var pool = Pool().ToDictionary(c => c.Id);
        Eq(pool["oracle-l5r-10481"].ExperienceLevel, 1, "Bayushi Kachiko (ExperiencedCoM)");
        Eq(pool["oracle-l5r-10475"].ExperienceLevel, 1, "Isawa Tsuke (ExperiencedCoM)");
        Eq(pool[OUshi].ExperienceLevel, -1, "Hida O-Ushi - inexperienced");
        Eq(pool[Tsuruchi].ExperienceLevel, -1, "Tsuruchi - inexperienced");
    }

    private static void BaseTitles()
    {
        Eq(Experienced.BaseTitle("Hida O-Ushi - inexperienced"), "Hida O-Ushi", "inexperienced");
        Eq(Experienced.BaseTitle("Hida O-Ushi - Inexperienced "), "Hida O-Ushi", "case and trailing space");
        Eq(Experienced.BaseTitle("Bayushi Nitoshi - exp"), "Bayushi Nitoshi", "exp");
        Eq(Experienced.BaseTitle("Mirumoto Yozo - exp2"), "Mirumoto Yozo", "exp2");
        Eq(Experienced.BaseTitle("Bayushi Nitoshi Experienced"), "Bayushi Nitoshi", "Experienced");
        Eq(Experienced.BaseTitle("Bayushi Nitoshi Experienced 2"), "Bayushi Nitoshi", "Experienced 2");
        Eq(Experienced.BaseTitle("Bayushi Nitoshi"), "Bayushi Nitoshi", "plain");
        Eq(Experienced.BaseTitle("Bayushi Kachiko"), "Bayushi Kachiko", "the ExperiencedCoM cards have no suffix in the catalogue");
        // The three catalogue versions of one Personality share one title.
        var pool = Pool().ToDictionary(c => c.Id);
        var titles = new[] { NitoshiBase, NitoshiInexperienced, "oracle-l5r-10480" }.Select(id => Experienced.BaseTitle(pool[id].Name)).Distinct().ToArray();
        Check(titles.SequenceEqual(["Bayushi Nitoshi"]), $"one title: {string.Join(" | ", titles)}");
    }

    private static void DeckNames()
    {
        Eq(Experienced.DeckName("Hida O-Ushi - inexperienced", ["Unique", "Inexperienced"]), "Hida O-Ushi - inexperienced", "inexperienced");
        Eq(Experienced.DeckName("Bayushi Nitoshi", ["Unique"]), "Bayushi Nitoshi", "plain");
        Eq(Experienced.DeckName("Bayushi Nitoshi - exp", ["Unique", "Experienced"]), "Bayushi Nitoshi - exp", "legacy exp suffix");
        Eq(Experienced.DeckName("Bayushi Nitoshi", ["Unique", "Experienced"]), "Bayushi Nitoshi Experienced", "a raw title gains the qualifier");
        Eq(Experienced.DeckName("Bayushi Kachiko", ["Unique", "ExperiencedCoM"]), "Bayushi Kachiko", "ExperiencedCoM keeps its catalogue name");
        Eq(Experienced.DeckName("Bayushi Kachiko", ["Experienced", "ExperiencedCoM"]), "Bayushi Kachiko Experienced", "the plain keyword still qualifies the name");
        // The whole pool still has one construction name per card (DeckWorkshop's dictionary would throw otherwise), and the three Nitoshis are three of them.
        var pool = Pool().Where(DeckWorkshop.MayBeInDeck).ToArray();
        Eq(pool.Select(c => c.DeckName).Distinct(StringComparer.OrdinalIgnoreCase).Count(), pool.Length, "distinct deck names");
        var byId = pool.ToDictionary(c => c.Id);
        Eq(new[] { NitoshiBase, NitoshiInexperienced, "oracle-l5r-10480" }.Select(id => byId[id].DeckName).Distinct().Count(), 3, "three Nitoshi deck names");
        Eq(byId["oracle-l5r-10481"].DeckName, "Bayushi Kachiko", "Kachiko's deck name is unchanged");
        Eq(byId["oracle-l5r-10475"].DeckName, "Isawa Tsuke", "Tsuke's deck name is unchanged");
    }

    private static void DeckConstruction()
    {
        var pool = Pool().Where(DeckWorkshop.MayBeInDeck).ToArray();
        var byName = pool.ToDictionary(c => c.DeckName, StringComparer.OrdinalIgnoreCase);
        var byId = pool.ToDictionary(c => c.Id);
        string[] names = [byId[NitoshiInexperienced].DeckName, byId[NitoshiBase].DeckName, byId["oracle-l5r-10480"].DeckName, byId["oracle-l5r-10481"].DeckName];
        var deck = new Deck("Emperor", names.Select(n => new DeckEntry(n, 1)).ToArray());
        var legality = DeckFormat.CheckLegality(deck, name => byName[name].Keywords);
        Check(legality.IsLegal, "one of each version of a Unique title, and a Unique ExperiencedCoM card, are legal together");
        var doubled = new Deck("Emperor", [new(names[0], 2), new(names[3], 2)]);
        var refused = DeckFormat.CheckLegality(doubled, name => byName[name].Keywords);
        Check(!refused.IsLegal && refused.Violations.Length == 2 && refused.Violations.All(v => v.Limit == 1), "two copies of a Unique version are still refused");
        // The workshop resolves each version by its own name and keeps them apart.
        var workshop = new DeckWorkshop(pool);
        foreach (var name in names) workshop.Adjust(name, 1);
        foreach (var name in names) Eq(workshop.Count(name), 1, name);
    }

    private static void KeywordInPlay()
    {
        var s = Table(Phase.Action, 0);
        var plain = Add(s, "training-paneki-base", 0, Zone.Home);
        var coils = Add(s, "training-unique-personality", 0, Zone.Home);
        coils.GrantedKeywords = [new("ExperiencedCoM", "test", "Permanent")];
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(plain.Id, "Experienced"), "a plain Personality is not Experienced");
        Check(e.HasKeyword(coils.Id, "Experienced"), "ExperiencedCoM is the Experienced keyword");
        e = Edit(e, st => st.Cards[coils.Id].RemovedKeywords = ["Experienced"]);
        Check(!e.HasKeyword(coils.Id, "Experienced") && !e.HasKeyword(coils.Id, "ExperiencedCoM"), "losing Experienced loses the Coils of Madness form too");
    }

    // A Dynasty Phase with one version of Bayushi Nitoshi at home and another face-up in a province.
    private static string? OverlayError(string inProvince, string[] provinceKeywords, string inPlay, string[] playKeywords, out (int Higher, int Lower) levels)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].Clan = "Scorpion";
        var higher = s.Cards[s.Players[0].Provinces[0].CardId!];
        higher.DefinitionId = inProvince; higher.FaceUp = true; higher.GrantedKeywords = provinceKeywords.Select(k => new GrantedKeyword(k, "test", "Permanent")).ToList();
        var lower = Add(s, inPlay, 0, Zone.Home);
        lower.GrantedKeywords = playKeywords.Select(k => new GrantedKeyword(k, "test", "Permanent")).ToList();
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuoteOverlay(0, higher.Id, lower.Id);
        levels = (quote.HigherLevel, quote.LowerLevel);
        var offered = e.QueryActions(0).Any(a => a.Type == "overlay" && a.Enabled && a.Command?.CardId == higher.Id && a.Command.TargetId == lower.Id);
        Check(offered == quote.Error is null, "the offered action agrees with the quote");
        if (quote.Error is null)
        {
            Accept(e, new GameCommand("overlay", 0, e.Version, CardId: higher.Id, TargetId: lower.Id, SourceIds: []));
            Check(e.State.Cards[higher.Id].Zone == Zone.Home && e.State.Cards[lower.Id].Zone == Zone.RemovedFromGame, "the overlay happened");
        }
        else
        {
            var before = e.Version;
            Reject(e, new GameCommand("overlay", 0, e.Version, CardId: higher.Id, TargetId: lower.Id, SourceIds: []));
            Check(e.Version == before && e.State.Cards[lower.Id].Zone == Zone.Home && e.State.Cards[higher.Id].Zone == Zone.Province, "a refused overlay changes nothing");
        }
        return quote.Error;
    }

    private static void OverlayLegality()
    {
        const string level = "The overlaying Personality must have a higher Experience Level.";
        // "X - exp" over "X - inexperienced", and over "X": the Experienced version is the plain card with the keyword (the catalogue's own - exp is not published yet).
        Check(OverlayError(NitoshiBase, ["Experienced"], NitoshiInexperienced, [], out var a) is null && a == (1, -1), "Experienced overlays Inexperienced");
        Check(OverlayError(NitoshiInexperienced, ["Experienced"], NitoshiBase, [], out var b) is null && b == (1, 0), "Experienced overlays the plain version");
        // "X - inexperienced" is level -1, below "X" (level 0): the plain card overlays it, and it overlays nothing.
        Check(OverlayError(NitoshiBase, [], NitoshiInexperienced, [], out var c) is null && c == (0, -1), "plain over Inexperienced");
        Eq(OverlayError(NitoshiInexperienced, [], NitoshiBase, [], out _), level, "Inexperienced over plain");
        // Inexperienced never overlays an Experienced version.
        Eq(OverlayError(NitoshiInexperienced, [], NitoshiBase, ["Experienced"], out var d), level, "Inexperienced over Experienced"); Check(d == (-1, 1), "-1 over 1");
        // ExperiencedCoM is level 1: over level 0 it overlays, over Experienced (level 1) it does not, and Experienced 2 overlays it.
        Check(OverlayError(NitoshiBase, ["ExperiencedCoM"], NitoshiInexperienced, [], out var f) is null && f == (1, -1), "ExperiencedCoM overlays level -1");
        Eq(OverlayError(NitoshiBase, ["ExperiencedCoM"], NitoshiBase, ["Experienced"], out var g), level, "ExperiencedCoM over Experienced"); Check(g == (1, 1), "1 over 1");
        Eq(OverlayError(NitoshiBase, ["Experienced"], NitoshiBase, ["ExperiencedCoM"], out _), level, "Experienced over ExperiencedCoM");
        Check(OverlayError(NitoshiBase, ["Experienced 2"], NitoshiBase, ["ExperiencedCoM"], out var h) is null && h == (2, 1), "Experienced 2 overlays ExperiencedCoM");
        // A different title is never overlaid, whatever the levels.
        Eq(OverlayError(NitoshiBase, ["Experienced"], OUshi, [], out _), "Experienced Personalities may only overlay a Personality with the same title.", "another title");
    }

    private static void PublishedShape()
    {
        var oushi = CardCatalog.Get(OUshi); var tsuruchi = CardCatalog.Get(Tsuruchi);
        Check(oushi is { Name: "Hida O-Ushi", Type: CardType.Personality, Clan: "Crab", Force: 3, Chi: 2, GoldCost: 6, PersonalHonor: 1, HonorRequirement: null, Effect: "none" }, "O-Ushi's stats");
        Check(oushi.Keywords!.SequenceEqual(["Loyal", "Unique", "Bully", "Crab Clan", "Inexperienced", "Samurai"]), "O-Ushi's keywords");
        Check(oushi.Abilities!.SequenceEqual([Bow]) && oushi.Text == "Limited: Bow a target Personality with lower Force.", "O-Ushi's ability");
        Check(tsuruchi is { Name: "Tsuruchi", Type: CardType.Personality, Clan: "Mantis", Force: 3, Chi: 3, GoldCost: 8, PersonalHonor: 2, HonorRequirement: 2, Effect: "none" }, "Tsuruchi's stats");
        Check(tsuruchi.Keywords!.SequenceEqual(["Unique", "Daimyo", "Inexperienced", "Mantis Clan", "Master Bowman", "Samurai", "Scout", "Wasp Clan"]), "Tsuruchi's keywords");
        Check(tsuruchi.Abilities!.SequenceEqual([Arrows, Attack]), "Tsuruchi's abilities");
        Check(AbilityCatalog.All[Bow] is { Timing: "Limited", Target: "personality-force-lt-self", UsesPerTurn: 1 }, "the bow is a Limited action");
        Check(AbilityCatalog.All[Arrows] is { Timing: "Open", Target: "none", UsesPerTurn: 1 }, "the tokens are an Open action");
        Check(AbilityCatalog.All[Attack] is { Timing: "Battle", Target: "attack", UsesPerTurn: 3 }, "the Attack is a Battle action, three times per turn");
        Eq(Experienced.Level(oushi.Keywords), -1, "O-Ushi's level"); Eq(Experienced.Level(tsuruchi.Keywords), -1, "Tsuruchi's level");
        var manifest = File.ReadAllLines(Path.Combine(RepoRoot(), "tests", "L5R.Core.Tests", "PublishedWaves", "personality-wave-128.txt")).Where(l => l.Trim().Length > 0).ToArray();
        Check(manifest.SequenceEqual([OUshi, Tsuruchi]), $"the manifest: {string.Join(",", manifest)}");
    }

    private static GameCommand Use(MatchEngine e, CardInstance card, string ability, string? target = null) => new("useAbility", 0, e.Version, CardId: card.Id, TargetId: target, AbilityId: ability);

    private static void OUshiBows()
    {
        var s = Table(Phase.Action, 0);
        var oushi = Add(s, OUshi, 0, Zone.Home); // Force 3
        var weakEnemy = Add(s, "training-lion-courtier", 1, Zone.Home); // Force 2
        var weakOwn = Add(s, "training-samurai", 0, Zone.Home); // Force 2
        var equal = Add(s, "training-crane-samurai", 1, Zone.Home); // Force 3
        var strong = Add(s, "training-lion-samurai", 1, Zone.Home); // Force 4
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: weakEnemy.Id);
        var e = MatchEngine.FromTestState(s);
        foreach (var illegal in new[] { equal.Id, strong.Id, oushi.Id, follower.Id })
        {
            var before = e.Version;
            Reject(e, Use(e, oushi, Bow, illegal));
            Check(e.Version == before && e.State.Cards.Values.All(c => !c.Bowed || c.Zone != Zone.Home || CardCatalog.Get(c.DefinitionId).Type != CardType.Personality), $"a refused target changes nothing ({illegal})");
        }
        Reject(e, Use(e, oushi, Bow)); // a target is required
        var offered = Options(e, 0, Bow, oushi.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().Order().ToArray();
        Check(offered.SequenceEqual(new[] { weakEnemy.Id, weakOwn.Id }.Order()), $"offered targets: {string.Join(",", offered)}");
        Accept(e, Use(e, oushi, Bow, weakEnemy.Id));
        Check(e.State.Cards[weakEnemy.Id].Bowed && !e.State.Cards[oushi.Id].Bowed, "the target is bowed; O-Ushi pays no cost");
        Check(e.State.PendingChoice is null, "the action finished");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Reject(e, Use(e, oushi, Bow, weakOwn.Id)); // once per turn
        // Limited: not in the Battle phase, and not on the other player's turn.
        var battle = Table(Phase.Battle, 0); var there = Add(battle, OUshi, 0, Zone.Battlefield, province: 0); var foe = Add(battle, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(battle);
        Reject(be, Use(be, there, Bow, foe.Id));
        var theirs = Table(Phase.Action, 1); theirs.PriorityPlayerId = 0; var mine = Add(theirs, OUshi, 0, Zone.Home); var other = Add(theirs, "training-lion-courtier", 1, Zone.Home);
        var te = MatchEngine.FromTestState(theirs);
        Reject(te, Use(te, mine, Bow, other.Id));
        // A Force penalty makes a higher-Force Personality a legal target: the comparison is the Force they have now.
        var weakened = Table(Phase.Action, 0); var o2 = Add(weakened, OUshi, 0, Zone.Home); var big = Add(weakened, "training-lion-samurai", 1, Zone.Home); Force(big, -2);
        var we = MatchEngine.FromTestState(weakened);
        Accept(we, Use(we, o2, Bow, big.Id));
        Check(we.State.Cards[big.Id].Bowed, "Force 4 - 2 is lower than 3");
    }

    private static void OUshiStalwart()
    {
        var s = Table(Phase.Action, 0);
        var oushi = Add(s, OUshi, 0, Zone.Home);
        var stalwart = Add(s, "training-stalwart-personality", 1, Zone.Home); // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, oushi, Bow, stalwart.Id));
        Check(!e.State.Cards[stalwart.Id].Bowed, "Stalwart negated the first bowing by an enemy effect this turn");
        Check(e.State.PendingChoice is null, "the action finished");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e, oushi.Id), "the action was spent although the bowing was negated");
    }

    private static bool CanUse(MatchEngine e, string cardId) => Options(e, 0, Bow, cardId).Any(o => o.Enabled);

    private static void TsuruchiTokens()
    {
        foreach (var start in new[] { 0, 3, 5 })
        {
            var s = Table(Phase.Action, 0);
            var tsuruchi = Add(s, Tsuruchi, 0, Zone.Home);
            for (var i = 0; i < start; i++) tsuruchi.Tokens.Add(new CardToken($"fixture-arrow-{i}", "Arrow", "test-fixture", null, 0));
            tsuruchi.Tokens.Add(new CardToken("fixture-fire", "Fire", "test-fixture", null, 0)); // another kind of token is not counted
            var e = MatchEngine.FromTestState(s);
            var force = e.CardForceForTest(tsuruchi.Id);
            Accept(e, Use(e, tsuruchi, Arrows));
            var tokens = e.State.Cards[tsuruchi.Id].Tokens;
            Eq(tokens.Count(t => t.Type == "Arrow"), 5, $"exactly five from {start}");
            Eq(tokens.Count, 6, "the other token is untouched");
            Check(tokens.Where(t => t.Type == "Arrow").All(t => t.Stat is null && t.Amount == 0), "an Arrow token carries no bonus");
            Eq(e.CardForceForTest(tsuruchi.Id), force, "his Force is unchanged");
            e = Edit(e, st => st.PriorityPlayerId = 0);
            Reject(e, Use(e, tsuruchi, Arrows)); // once per turn
        }
        // An Open action: his player may take it in the other player's Action phase too.
        var theirs = Table(Phase.Action, 1); theirs.PriorityPlayerId = 0;
        var away = Add(theirs, Tsuruchi, 0, Zone.Home);
        var te = MatchEngine.FromTestState(theirs);
        Accept(te, Use(te, away, Arrows));
        Eq(te.State.Cards[away.Id].Tokens.Count, 5, "five on the other player's turn");
    }

    private static (MatchEngine Engine, CardInstance Tsuruchi) Battle(int arrows, Action<MatchState>? more = null)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var tsuruchi = Add(s, Tsuruchi, 0, Zone.Battlefield, province: 0);
        for (var i = 0; i < arrows; i++) tsuruchi.Tokens.Add(new CardToken($"fixture-arrow-{i}", "Arrow", "test-fixture", null, 0));
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), tsuruchi);
    }

    private static ActionOption[] Answers(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEitherOr").ToArray();
    private static int ArrowCount(MatchEngine e, CardInstance tsuruchi) => e.State.Cards[tsuruchi.Id].Tokens.Count(t => t.Type == "Arrow");

    private static void TsuruchiAttackChoice()
    {
        foreach (var (destroy, force, dies) in new[] { (2, 4, true), (2, 5, false), (0, 1, false), (5, 10, true), (3, 6, true) })
        {
            CardInstance target = null!;
            var (e, tsuruchi) = Battle(5, s => { target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(target, force - 4); });
            Accept(e, Use(e, tsuruchi, Attack, target.Id));
            Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 0 } && e.State.PendingEitherOr is { SpecId: ExperiencedVariantAbilities.ArrowSpec, DeciderPlayerId: 0 }, "the number is asked of his player");
            Eq(ArrowCount(e, tsuruchi), 5, "nothing is destroyed before the choice");
            var answers = Answers(e, 0);
            Check(answers.Select(a => a.Command!.Choice).SequenceEqual(["0", "1", "2", "3", "4", "5"]) && answers.All(a => a.Enabled), "zero to five, each a complete command");
            Eq(Answers(e, 1).Length, 0, "the other player has no decision");
            Check(e.GetView(1).PendingEitherOr is { ViewerDecides: false } seen && seen.Options.Length == 6, "the other player sees the open decision");
            Accept(e, answers[destroy].Command!);
            Eq(ArrowCount(e, tsuruchi), 5 - destroy, $"{destroy} destroyed");
            Eq(e.State.Cards[target.Id].Zone == Zone.Battlefield, !dies, $"Ranged {2 * destroy} against Force {force}");
            Check(e.State.PendingChoice is null && e.State.PendingEitherOr is null && e.State.PriorityPlayerId == 1, "the action finished and priority passed");
            Check(e.State.ExperiencedVariants is { ArrowsDestroyed: 0 } cleared && cleared.ArrowNegated.Count == 0, "nothing of the decision is left in the state");
        }
        // No target: the tokens are still destroyed and the Attack is made at nothing.
        var (ne, nt) = Battle(2, s => Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0));
        Accept(ne, Use(ne, nt, Attack));
        Check(Answers(ne, 0).Select(a => a.Command!.Choice).SequenceEqual(["0", "1", "2"]), "zero to two");
        Accept(ne, Answers(ne, 0)[2].Command!);
        Check(ArrowCount(ne, nt) == 0 && ne.State.PendingChoice is null, "both destroyed; the action finished");
        // The state survives a save in the middle of the decision.
        var (se, st) = Battle(3, s => Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0));
        Accept(se, Use(se, st, Attack));
        se = MatchEngine.FromTestState(se.State);
        Accept(se, Answers(se, 0)[1].Command!);
        Check(ArrowCount(se, st) == 2 && se.State.PendingChoice is null, "resumed from a copy of the state");
    }

    private static void TsuruchiChoiceGuards()
    {
        CardInstance target = null!;
        var (e, tsuruchi) = Battle(2, s => target = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0));
        Accept(e, Use(e, tsuruchi, Attack, target.Id));
        var before = e.Version;
        Reject(e, new GameCommand("chooseEitherOr", 1, e.Version, CardId: tsuruchi.Id, Choice: "2")); // not his decision
        Reject(e, new GameCommand("chooseEitherOr", 0, e.Version, CardId: tsuruchi.Id, Choice: "3")); // more than he has
        Reject(e, new GameCommand("chooseEitherOr", 0, e.Version, CardId: tsuruchi.Id, Choice: "-1"));
        Reject(e, new GameCommand("chooseEitherOr", 0, e.Version, CardId: tsuruchi.Id));
        Reject(e, new GameCommand("pass", 0, e.Version)); // the decision must be answered
        Check(e.Version == before && ArrowCount(e, tsuruchi) == 2 && e.State.Cards[target.Id].Zone == Zone.Battlefield && e.State.PendingChoice is { Type: "eitherOr" }, "refused answers change nothing");
        // Illegal announcements: his own card as the Attack's target, or a card that is not at the battle.
        CardInstance home = null!, own = null!;
        var (ie, it) = Battle(2, s => { Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); home = Add(s, "training-lion-courtier", 1, Zone.Home); own = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0); });
        var version = ie.Version;
        Reject(ie, Use(ie, it, Attack, home.Id));
        Reject(ie, Use(ie, it, Attack, own.Id));
        Check(ie.Version == version && ArrowCount(ie, it) == 2 && ie.State.PendingChoice is null, "a refused announcement asks nothing");
        // A Battle action: not in the Action phase.
        var action = Table(Phase.Action, 0); var idle = Add(action, Tsuruchi, 0, Zone.Home);
        var ae = MatchEngine.FromTestState(action);
        Reject(ae, Use(ae, idle, Attack));
    }

    private static void TsuruchiThreeTimes()
    {
        CardInstance target = null!;
        var (e, tsuruchi) = Battle(5, s => { target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); });
        foreach (var destroy in new[] { 1, 2, 2 }) // Ranged 2, 4, 4 against Force 6
        {
            Accept(e, Use(e, tsuruchi, Attack, target.Id));
            Accept(e, Answers(e, 0)[destroy].Command!);
            Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "Force 6 survives");
            e = Edit(e, st => { st.PriorityPlayerId = 0; st.ConsecutivePasses = 0; });
        }
        Eq(ArrowCount(e, tsuruchi), 0, "all five spent over three uses");
        var before = e.Version;
        Reject(e, Use(e, tsuruchi, Attack, target.Id)); // a fourth use
        Check(e.Version == before && !Options(e, 0, Attack, tsuruchi.Id).Any(o => o.Enabled), "three times per turn");
        // With no token: no decision, a Ranged 0 Attack, and it still counts as a use.
        CardInstance weak = null!;
        var (ze, zt) = Battle(0, s => { weak = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); Force(weak, -2); });
        Accept(ze, Use(ze, zt, Attack, weak.Id));
        Check(ze.State.PendingChoice is null && ze.State.PendingEitherOr is null, "nothing is asked");
        Check(ze.State.Cards[weak.Id].Zone == Zone.Battlefield, "an Attack of strength 0 destroys nothing, as for every Attack (ResolveAttackNow)");
        ze = Edit(ze, st => st.PriorityPlayerId = 0);
        Accept(ze, Use(ze, zt, Attack, weak.Id)); Check(ze.State.PendingChoice is null, "a second use, still unasked");
        ze = Edit(ze, st => st.PriorityPlayerId = 0);
        Accept(ze, Use(ze, zt, Attack));
        ze = Edit(ze, st => st.PriorityPlayerId = 0);
        Reject(ze, Use(ze, zt, Attack)); // each counted as a use
        // The next turn the three uses are back.
        e = Edit(e, st => { st.TurnNumber++; st.Cards[tsuruchi.Id].Tokens.Add(new CardToken("fixture-arrow-next", "Arrow", "test-fixture", null, 0)); });
        Check(Options(e, 0, Attack, tsuruchi.Id).Any(o => o.Enabled), "usable again next turn");
    }

    private static void TsuruchiDeferredAttack()
    {
        CardInstance target = null!;
        var (e, tsuruchi) = Battle(4, s =>
        {
            target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            Add(s, "training-attack-reactor", 1, Zone.Battlefield, province: 0); // a Reaction to the Attack keeps its window open
        });
        Accept(e, Use(e, tsuruchi, Attack, target.Id));
        Accept(e, Answers(e, 0)[3].Command!);
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack opens its reaction window");
        Check(e.State.PendingAttack is { Strength: 6, Kind: "Ranged", TargetId: var id } && id == target.Id, "Ranged 6 at the announced target");
        Eq(ArrowCount(e, tsuruchi), 1, "three destroyed before the Attack");
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "not yet resolved");
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: "attackReaction" }; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "declineReaction").Command!);
        Check(e.State.Cards[target.Id].Zone != Zone.Battlefield, "Ranged 6 destroyed the Force 4 Personality");
        Check(e.State.PendingChoice is null && e.State.PendingAttack is null && e.State.PriorityPlayerId == 1, "the action finished and priority passed");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(Options(e, 0, Attack, tsuruchi.Id).Any(o => o.Enabled), "one use of three is spent");
    }
}
