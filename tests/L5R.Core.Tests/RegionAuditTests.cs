using L5R.Core;

// Region audit: every Region card in the catalogue is put face down in a Province and revealed by the real Events Phase. It must attach to that Province and, unless its own text says
// the Province is not refilled, the Province must refill face down from the Dynasty deck. A second Region revealed in a Province that already has one is discarded and still refills.
internal static class RegionAuditTests
{
    public static void Run(TestRunner runner) => runner.Run("region-audit", [
        ("Every catalogue Region is revealed by the Events Phase, attaches to its Province and the Province refills", EveryRegionAttachesAndRefills),
        ("A Region revealed in a Province that already holds a Region is discarded and the Province refills", SecondRegionIsDiscarded),
        ("A Region already face up in a Province (refilled face up by Border Keep) attaches and refills at the next Events Phase, like an Event", FaceUpRegionAttaches),
        ("The Distant Reaches attaches in a Province, refills it, and can then bow attachments", DistantReachesEndToEnd),
        ("The Distant Reaches' action, after it attached through the Events Phase: bows one or two attachments (own or enemy), not none or three, bows itself as the cost and is then not offered again", DistantReachesAction),
    ]);

    private const string DistantReaches = "oracle-l5r-8030";

    // Regions whose printed text changes what an ordinary reveal does. Anything else must attach and refill.
    private static readonly HashSet<string> NoRefillByText = ["oracle-l5r-10331"]; // Kokure's Lands: "This Province does not hold Dynasty cards."

    private static bool IsAudited(CardDefinition c) => c.Type == CardType.Region && !c.Id.StartsWith("training-", StringComparison.Ordinal);

    private static (MatchEngine E, CardInstance Region, string Filler) Reveal(string id, bool occupied = false, bool faceUp = false)
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        foreach (var p in s.Players[0].Provinces) if (p.CardId is string c) s.Cards[c].FaceUp = false;
        var region = Batch336Support.Add(s, id, 0, Zone.Province, province: 1);
        var province = s.Players[0].Provinces[1];
        if (province.CardId is string old) { s.Cards[old].Zone = Zone.DynastyDeck; s.Cards[old].ProvinceIndex = null; s.Players[0].DynastyDeck.Insert(0, old); }
        province.CardId = region.Id; region.FaceUp = faceUp;
        if (occupied)
        {
            var first = Batch336Support.Add(s, id, 0, Zone.Region, province: 1); province.RegionId = first.Id;
        }
        var filler = s.Players[0].DynastyDeck[0];
        // Only province 1 is revealed: every other province is left as it is, face down, so a failure is about this Region alone.
        return (MatchEngine.FromTestState(s), region, filler);
    }

    private static string Describe(MatchEngine e, CardInstance region, int province)
    {
        var p = e.State.Players[0].Provinces[province];
        return $"zone={e.State.Cards[region.Id].Zone} faceUp={e.State.Cards[region.Id].FaceUp} regionId={p.RegionId} cardId={p.CardId} phase={e.State.Phase} pending={e.State.PendingChoice?.Type}";
    }

    private static void EveryRegionAttachesAndRefills()
    {
        var regions = CardCatalog.Cards.Values.Where(IsAudited).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Batch336Support.Check(regions.Length >= 20, $"expected the catalogue's Regions, found {regions.Length}");
        var failures = new List<string>();
        foreach (var def in regions)
        {
            try
            {
                var (e, region, _) = Reveal(def.Id);
                e.EventsPhaseForTest(0);
                var p = e.State.Players[0].Provinces[1];
                var card = e.State.Cards[region.Id];
                if (def.Effect == "tested-land") { Batch336Support.Check(e.State.PendingChoice is { Type: "testedLandProvince" }, "Tested Land asks which enemy Province it attaches to"); continue; } // it attaches to an enemy Province
                var attached = p.RegionId == region.Id && card.Zone == Zone.Region && card.FaceUp && card.ProvinceIndex == 1;
                if (!attached) { failures.Add($"{def.Name} ({def.Id}) did not attach: {Describe(e, region, 1)}"); continue; }
                if (NoRefillByText.Contains(def.Id)) continue;
                var refilled = p.CardId is string id && id != region.Id && e.State.Cards[id].Zone == Zone.Province && e.State.Cards[id].ProvinceIndex == 1;
                if (!refilled) failures.Add($"{def.Name} ({def.Id}) attached but Province 2 did not refill: {Describe(e, region, 1)}");
            }
            catch (Exception ex) { failures.Add($"{def.Name} ({def.Id}) threw {ex.GetType().Name}: {ex.Message}"); }
        }
        Batch336Support.Check(failures.Count == 0, $"{failures.Count} of {regions.Length} Regions failed:\n  " + string.Join("\n  ", failures));
    }

    private static void SecondRegionIsDiscarded()
    {
        var failures = new List<string>();
        foreach (var def in CardCatalog.Cards.Values.Where(IsAudited).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            try
            {
                var (e, region, _) = Reveal(def.Id, occupied: true);
                var firstId = e.State.Players[0].Provinces[1].RegionId;
                e.EventsPhaseForTest(0);
                var p = e.State.Players[0].Provinces[1];
                var card = e.State.Cards[region.Id];
                if (def.Effect == "tested-land" || p.RegionId != firstId) continue; // a Region with its own attach rule (Tested Land chooses an enemy Province) is not the plain case
                if (card.Zone != Zone.DynastyDiscard) failures.Add($"{def.Name} ({def.Id}) second copy was not discarded: {Describe(e, region, 1)}");
                else if (!NoRefillByText.Contains(def.Id) && (p.CardId is not string id || id == region.Id)) failures.Add($"{def.Name} ({def.Id}) Province did not refill after the discard: {Describe(e, region, 1)}");
            }
            catch (Exception ex) { failures.Add($"{def.Name} ({def.Id}) threw {ex.GetType().Name}: {ex.Message}"); }
        }
        Batch336Support.Check(failures.Count == 0, string.Join("\n  ", failures));
    }

    private static void FaceUpRegionAttaches()
    {
        var failures = new List<string>();
        foreach (var def in CardCatalog.Cards.Values.Where(IsAudited).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            try
            {
                var (e, region, _) = Reveal(def.Id, faceUp: true);
                e.EventsPhaseForTest(0);
                var p = e.State.Players[0].Provinces[1];
                if (def.Effect == "tested-land") continue; // chooses an enemy Province; covered by its own tests
                if (p.RegionId != region.Id) failures.Add($"{def.Name} ({def.Id}) face up in its Province never attached: {Describe(e, region, 1)}");
                else if (!NoRefillByText.Contains(def.Id) && (p.CardId is not string id || id == region.Id)) failures.Add($"{def.Name} ({def.Id}) attached but did not refill: {Describe(e, region, 1)}");
            }
            catch (Exception ex) { failures.Add($"{def.Name} ({def.Id}) threw {ex.GetType().Name}: {ex.Message}"); }
        }
        Batch336Support.Check(failures.Count == 0, string.Join("\n  ", failures));
    }

    private static void DistantReachesEndToEnd()
    {
        var (e, region, _) = Reveal(DistantReaches);
        e.EventsPhaseForTest(0);
        var p = e.State.Players[0].Provinces[1];
        Batch336Support.Check(p.RegionId == region.Id, "The Distant Reaches attached: " + Describe(e, region, 1));
        Batch336Support.Check(p.CardId is not null && p.CardId != region.Id, "and the Province refilled: " + Describe(e, region, 1));
    }

    private static void DistantReachesAction()
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        foreach (var p in s.Players[0].Provinces) if (p.CardId is string c) s.Cards[c].FaceUp = false;
        var region = Batch336Support.Add(s, DistantReaches, 0, Zone.Province, province: 1);
        if (s.Players[0].Provinces[1].CardId is string old) { s.Cards[old].Zone = Zone.DynastyDeck; s.Cards[old].ProvinceIndex = null; s.Players[0].DynastyDeck.Insert(0, old); }
        s.Players[0].Provinces[1].CardId = region.Id; region.FaceUp = false;
        var mine = Batch336Support.Add(s, "training-water-shugenja", 0, Zone.Home);
        var theirs = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Home);
        var a1 = Batch336Support.Add(s, "oracle-l5r-8366", 0, Zone.Attached, parent: mine.Id);
        var a2 = Batch336Support.Add(s, "oracle-l5r-8366", 1, Zone.Attached, parent: theirs.Id);
        var a3 = Batch336Support.Add(s, "oracle-l5r-8366", 0, Zone.Attached, parent: mine.Id);
        var e = MatchEngine.FromTestState(s);
        e.EventsPhaseForTest(0);
        Batch336Support.Check(e.State.Players[0].Provinces[1].RegionId == region.Id && e.State.Phase == Phase.Action, "attached and the Action phase began");
        Batch336Support.Check(Batch336Support.CanUse(e, 0, "distant-reaches-bow", region.Id), "the action is offered once the Region attached");
        Batch336Support.Check(!Batch336Support.CanUse(e, 1, "distant-reaches-bow", region.Id), "and not to the opponent");
        // none and three attachments are refused; the state does not change
        Batch336Support.Check(Batch336Support.Rejection(e, Batch336Support.Use(e, 0, "distant-reaches-bow", region.Id, selections: [Batch336Support.Pick("target", a1.Id, a2.Id, a3.Id)])).Length > 0, "three attachments are refused");
        Batch336Support.Check(!e.State.Cards[region.Id].Bowed && !e.State.Cards[a1.Id].Bowed, "a refused use costs nothing");
        // one own and one enemy attachment
        { var r = e.Execute(Batch336Support.Use(e, 0, "distant-reaches-bow", region.Id, selections: [Batch336Support.Pick("target", a1.Id, a2.Id)])); Batch336Support.Check(r.Accepted, r.Error ?? "rejected"); }
        Batch336Support.Check(e.State.Cards[region.Id].Bowed, "the Region bowed as the cost");
        Batch336Support.Check(e.State.Cards[a1.Id].Bowed && e.State.Cards[a2.Id].Bowed, "both chosen attachments bowed");
        Batch336Support.Check(!e.State.Cards[a3.Id].Bowed, "the third was not touched");
        Batch336Support.Check(!Batch336Support.CanUse(e, 0, "distant-reaches-bow", region.Id), "the bowed Region offers nothing more");
    }
}
