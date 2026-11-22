using L5R.Core;

// Issue #1309: the client greys a used printed ability on the card art and counts multi-use abilities ("1X"). The view must give the
// right count and limit, keep once-per-game and first-turn-only abilities grey for good, and say nothing about a face-down card.
internal static class AbilityUseViewTests
{
    public static void Run(TestRunner runner) => runner.Run("ability-use-view", [
        ("A twice-per-turn ability counts each use, is spent at the second and clears next turn", TwicePerTurnCounts),
        ("Border Keep: the first-turn refresh greys for good after the owner's first turn; the once-per-game refresh stays grey once used", BorderKeepPermanence),
        ("A face-down card shows no ability uses", FaceDownHidden),
        ("A Tactician's Tactical actions this turn show against his cap, in both seats", TacticianUses),
        ("Every extra-use source (Fires of War mark, Centering the Soul, Sycophant) raises the shown limit, so the box clears", ExtraUsesRaiseTheLimit),
    ]);

    private const string Tomaru = "oracle-l5r-8893", BorderKeep = "oracle-l5r-1106";

    private static CardView View(MatchState s, int seat, string id) =>
        MatchEngine.FromTestState(s).GetView(seat).Players.SelectMany(p => p.Home).First(c => c.Id == id);

    private static AbilityUseView Use(MatchState s, string cardId, string key, int seat = 0) =>
        View(s, seat, cardId).AbilityUses?.FirstOrDefault(u => u.Key == key) ?? throw new Exception($"no use row for {key}");

    private static void TwicePerTurnCounts()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var card = Batch336Support.Add(s, Tomaru, 1, Zone.Home);
        var key = AbilityCatalog.All["tomaru-attack-3"].UsageKey ?? "tomaru-attack-3";
        var fresh = Use(s, card.Id, key);
        Batch336Support.Check(fresh is { Used: 0, Max: 2, Spent: false, Permanent: false }, $"unused: {fresh}");
        card.AbilityUses[key] = s.TurnNumber; card.AbilityUses["count:" + key] = s.TurnNumber * 1024 + 1;
        var once = Use(s, card.Id, key);
        Batch336Support.Check(once is { Used: 1, Max: 2, Spent: false }, $"after one use: {once}");
        card.AbilityUses["count:" + key] = s.TurnNumber * 1024 + 2;
        foreach (var seat in new[] { 0, 1 })
            Batch336Support.Check(Use(s, card.Id, key, seat) is { Used: 2, Max: 2, Spent: true, Permanent: false }, $"seat {seat} sees both uses spent");
        s.TurnNumber++;
        Batch336Support.Check(Use(s, card.Id, key) is { Used: 0, Spent: false }, "the next turn it is fresh");
    }

    private static void BorderKeepPermanence()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var keep = Batch336Support.Add(s, BorderKeep, 0, Zone.Home);
        var owner = s.Players[0];
        owner.TurnsTaken = 1;
        Batch336Support.Check(Use(s, keep.Id, "border-first") is { Spent: false, Permanent: false }, "available during the owner's first turn");
        Batch336Support.Check(Use(s, keep.Id, "border-game") is { Spent: false, Permanent: false }, "once-per-game refresh unused");
        s.ActivePlayerId = 1;
        Batch336Support.Check(Use(s, keep.Id, "border-first") is { Spent: true, Permanent: true }, "greyed once the owner's first turn has ended, though never used");
        keep.AbilityUses["border-game"] = s.TurnNumber;
        s.TurnNumber += 5; owner.TurnsTaken = 4; s.ActivePlayerId = 0;
        Batch336Support.Check(Use(s, keep.Id, "border-game") is { Used: 1, Spent: true, Permanent: true }, "the once-per-game refresh stays grey turns later");
    }

    private static void FaceDownHidden()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var card = Batch336Support.Add(s, Tomaru, 1, Zone.Home);
        card.FaceUp = false;
        foreach (var seat in new[] { 0, 1 })
        {
            var view = MatchEngine.FromTestState(s).GetView(seat).Players.SelectMany(p => p.Home).FirstOrDefault(c => c.Id == card.Id);
            Batch336Support.Check(view is null || view.AbilityUses is null && view.TacticalUses is null, $"seat {seat} learns nothing from a face-down card");
        }
    }

    private static void ExtraUsesRaiseTheLimit()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var political = CardCatalog.PlayableFromDecks.First(d => d.Type == CardType.Personality && AbilityRegionCards.Groups(d.Abilities ?? []) is [var g]
            && AbilityCatalog.All[g.Ids[0]] is { UsesPerTurn: 1, OncePerGame: false } a && (a.ActionKeywords ?? []).Contains("Political"));
        var card = Batch336Support.Add(s, political.Id, 0, Zone.Home);
        var key = AbilityRegionCards.Groups(political.Abilities!)[0].Key;
        card.AbilityUses[key] = s.TurnNumber;
        Batch336Support.Check(Use(s, card.Id, key) is { Used: 1, Max: 1, Spent: true }, "one use spends a once-per-turn ability");
        foreach (var (mark, value, source) in new[] {
            (MatchEngine.SecondUseKey, s.TurnNumber, "The Fires of War / additional-time mark"),
            ("p3:additional-use", s.TurnNumber * 1024 + 1, "Centering the Soul"),
            (WlItemsMiscFollowerAbilities.SycophantsMark, s.TurnNumber, "Sycophant (Political)") })
        {
            card.AbilityUses[mark] = value;
            var view = Use(s, card.Id, key);
            Batch336Support.Check(view is { Max: 2, Spent: false }, $"{source} gives a second use: {view}");
            card.AbilityUses.Remove(mark);
        }
    }

    private static void TacticianUses()
    {
        var def = CardCatalog.PlayableFromDecks.First(d => d.Type == CardType.Personality && (d.Keywords ?? []).Contains("Tactician"));
        var s = Batch336Support.Table(Phase.Battle, 0);
        var card = Batch336Support.Add(s, def.Id, 0, Zone.Home);
        Batch336Support.Check(View(s, 0, card.Id).TacticalUses is { Used: 0, Cap: 1 }, "an unused Tactician");
        s.P1.TacticalActions[$"{card.Id}|{s.TurnNumber}"] = 1;
        foreach (var seat in new[] { 0, 1 })
            Batch336Support.Check(View(s, seat, card.Id).TacticalUses is { Used: 1, Cap: 1 }, $"seat {seat} sees his Tactical action used");
        s.TurnNumber++;
        Batch336Support.Check(View(s, 0, card.Id).TacticalUses is { Used: 0 }, "fresh next turn");
    }
}

// Issue #1309: every printed ability the client greys must have its box on the scan, or a used ability would silently show nothing.
// A newly wired card fails here until tools/content/ability_regions/build.py has located it (or overrides.json supplies the box).
internal static class AbilityRegionCoverageTests
{
    public static void Run(TestRunner runner) => runner.Run("ability-region-coverage", [
        ("Every marked printed ability and printed Tactician has a box in ability-regions.json", EveryAbilityHasABox),
    ]);

    private static void EveryAbilityHasABox()
    {
        var path = Path.Combine(Batch336Support.RepoRoot(), "src", "L5R.Client", "Content", "ability-regions.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var cards = doc.RootElement.GetProperty("cards");
        var missing = new List<string>();
        foreach (var row in AbilityRegionCards.Rows())
        {
            var file = row.Card.ArtworkKey is { } art ? art[(art.LastIndexOf('/') + 1)..] : "";
            if (!cards.TryGetProperty(file, out var entry)) { missing.Add($"{row.Card.Name} ({row.Card.Id}): no entry"); continue; }
            var keys = entry.GetProperty("a").EnumerateArray().Select(a => a.GetProperty("k").GetString()).ToHashSet();
            missing.AddRange(row.Groups.Where(g => !keys.Contains(g.Key)).Select(g => $"{row.Card.Name} ({row.Card.Id}): {g.Key}"));
            if (row.PrintedTactician && !entry.TryGetProperty("t", out _)) missing.Add($"{row.Card.Name} ({row.Card.Id}): Tactician keyword");
        }
        Batch336Support.Check(missing.Count == 0, "missing boxes; run python tools/content/ability_regions/build.py:\n" + string.Join("\n", missing.Take(20)));
    }
}
