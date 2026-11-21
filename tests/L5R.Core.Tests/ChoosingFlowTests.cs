using L5R.Core;

// Issue #1202: the client asks a multi-part action one question at a time (performer, target, which ability, how to pay). It can only do that if Core names what each answer means: a Stolen Merchandise
// option says what it copies (ChoiceLabel), a cost option says how it is paid (AlternativeCost.Label) once per performer. These cases pin what the client's steps read.
internal static class ChoosingFlowTests
{
    private const string GovernorsCourt = "oracle-l5r-2878", StolenMerchandise = "oracle-l5r-7494";
    private const string TemplesOfGisei = "oracle-l5r-7898", TurquoiseCourt = "oracle-l5r-10760", GreenLakeDojo = "oracle-l5r-10265";

    public static void Run(TestRunner runner) => runner.Run("choosing-flow", [
        ("Governor's Court offers each way to pay once per Courtier, each named, and only as an action against the other player", GovernorsCourtOptions),
        ("Stolen Merchandise names what each option copies, so a client can ask which ability after the Holding", StolenMerchandiseLabels),
        ("Every ability with cost options offers distinctly named ways to pay", CostOptionsAreNamed),
    ]);

    private static void GovernorsCourtOptions()
    {
        var s = Batch336Support.Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var court = Batch336Support.Add(s, GovernorsCourt, 0, Zone.Home);
        var kodo = Batch336Support.Add(s, "training-courtier-personality", 0, Zone.Home);
        var other = Batch336Support.Add(s, "training-courtier-personality", 0, Zone.Home);
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var enabled = e.QueryActions(0).Where(o => o.CardId == court.Id && o.Enabled).ToArray();
        Batch336Support.Check(enabled.All(o => o.AlternativeCost is not null && o.Command?.TargetPlayerId == 1), "every usable option is a way to pay, aimed at the other player");
        var pairs = enabled.Select(o => (o.AlternativeCost!.PerformerId, o.AlternativeCost.Id)).ToHashSet();
        Batch336Support.Check(pairs.SetEquals([(kodo.Id, "bow-courtier"), (kodo.Id, "bow-source"), (other.Id, "bow-courtier"), (other.Id, "bow-source")]),
            "each Courtier can pay either way: " + string.Join(", ", pairs));
        Batch336Support.Check(enabled.Select(o => o.AlternativeCost!.Label).ToHashSet().SetEquals(["Bow the Courtier", "Bow this card"]), "the ways to pay are named for the player");
        Batch336Support.Check(enabled.Length == 4, $"no duplicate options for a client to list twice, found {enabled.Length}");
        // The disabled "you" variants carry no command, so a client cannot submit them.
        Batch336Support.Check(e.QueryActions(0).Where(o => o.CardId == court.Id && !o.Enabled).All(o => o.Command is null), "an unavailable variant has no command");

        // A bowed Courtier cannot bow himself to pay, but can still perform and bow the Court.
        s.Cards[kodo.Id].Bowed = true;
        var bowed = MatchEngine.FromTestState(s).QueryActions(0).Where(o => o.CardId == court.Id && o.Enabled && o.AlternativeCost!.PerformerId == kodo.Id).Select(o => o.AlternativeCost!.Id).ToArray();
        Batch336Support.Check(bowed.SequenceEqual(["bow-source"]), "a bowed Courtier pays only by bowing the Court: " + string.Join(", ", bowed));
    }

    private static void StolenMerchandiseLabels()
    {
        var s = Batch336Support.Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var stolen = Batch336Support.Add(s, StolenMerchandise, 0, Zone.Home);
        var temples = Batch336Support.Add(s, TemplesOfGisei, 1, Zone.Home);
        var turquoise = Batch336Support.Add(s, TurquoiseCourt, 1, Zone.Home);
        var dojo = Batch336Support.Add(s, GreenLakeDojo, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var options = e.QueryActions(0).Where(o => o.CardId == stolen.Id && o.Enabled).ToArray();
        Batch336Support.Check(options.Length > 0 && options.All(o => !string.IsNullOrWhiteSpace(o.ChoiceLabel) && o.Command?.Choice is not null), "every usable option says what it copies");
        foreach (var holding in new[] { temples, turquoise, dojo })
        {
            var forHolding = options.Where(o => o.TargetIds?.Contains(holding.Id) == true && o.Command!.AbilityId == EmperorP5HiddenAbilities.StolenAbility).ToArray();
            Batch336Support.Check(forHolding.Length >= 2, $"{CardCatalog.Get(holding.DefinitionId).Name} has several abilities to copy, found {forHolding.Length}");
            Batch336Support.Check(forHolding.Select(o => o.ChoiceLabel).Distinct().Count() == forHolding.Length, $"{CardCatalog.Get(holding.DefinitionId).Name}: the choices read differently");
            Batch336Support.Check(forHolding.All(o => o.Command!.TargetId == holding.Id || o.TargetIds!.Length == 1), "an option belongs to one Holding");
        }
        Batch336Support.Check(options.Count(o => o.TargetIds?.Contains(temples.Id) == true && o.Command!.AbilityId == EmperorP5HiddenAbilities.StolenAbility) == 3, "Temples of Gisei Toshi's three abilities are three choices");
    }

    private static void CostOptionsAreNamed()
    {
        var withCosts = AbilityCatalog.All.Where(a => a.Value.CostOptions is { Length: > 1 }).ToArray();
        Batch336Support.Check(withCosts.Length >= 5, $"expected the catalogue's abilities with a choice of cost, found {withCosts.Length}");
        var failures = new List<string>();
        foreach (var (id, ability) in withCosts)
        {
            var options = ability.CostOptions!;
            if (options.Select(o => o.Id).Distinct().Count() != options.Length) failures.Add($"{id}: two cost options share an id");
            if (options.Any(o => string.IsNullOrWhiteSpace(o.Label))) failures.Add($"{id}: a cost option has no label");
            if (options.Select(o => o.Label).Distinct().Count() != options.Length) failures.Add($"{id}: two cost options read the same");
        }
        Batch336Support.Check(failures.Count == 0, string.Join("\n  ", failures));
    }
}
