using L5R.Core;

// The Duels lesson: Crane against Scorpion, played through the runner the way a player would. A rules or card change that breaks something a step shows (a refused Sanctioned Duel, Reckless Duel's focusing, the Duelist's
// swap, the Loyalty of the Scorpion's delay, a tie won by a Duelist) fails here instead of in front of a new player.
internal static class TutorialDuelsTests
{
    private const string Lesson = "special-duels";

    public static void Run(TestRunner runner) => runner.Run("tutorial-duels", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in the Crane's Action phase with the cards the notes name, Crane on 11 Honor", OpensAsCrane),
        ("The numbers the notes quote hold: 11 against 12 before the Duelist's swap, 14 against 12 after it, and 9 against 9 won by the Duelist", QuotedNumbers),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Duels is built and follows Attacks", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold or Zone.Attached);
    private static bool Has(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.Type}/{a.Command?.AbilityId}/{a.Command?.Choice}/{a.Label}/card={a.CardId}/tgt={a.Command?.TargetId}/{(a.Enabled ? "on" : "off")}"));

    private static string Yours(TutorialRunner runner, string definition) => Cards(runner, 0, definition).First(c => c.Zone is Zone.Hand or Zone.Focused).Id;

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede").ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing: " + Offered(runner.Actions().ToArray()));
        switch (step)
        {
            case "assign": return offered.First(a => a.Command!.Type == "attack").Command!;
            case "duelist":
                var stain = Yours(runner, TutorialCards.AStainCleansed); var retribution = Yours(runner, TutorialCards.Retribution);
                return offered.FirstOrDefault(a => a.Command!.Type == "duelistRefocus" && a.Command.CardId == stain && a.Command.TargetId == retribution)?.Command ?? throw new Exception($"{step}: the swap is not offered: {Offered(offered)}");
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {Offered(offered)}");
                return offered[0].Command!;
        }
    }

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 100; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            CheckTable(runner, coach.StepId);
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended: " + runner.Coach.StepId);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
    }

    /// <summary>What the engine shows at the start of each step, checked against what the words of that step say (and the card text behind them).</summary>
    private static void CheckTable(TutorialRunner runner, string step)
    {
        var state = runner.Engine.State;
        var makoto = Cards(runner, 0, TutorialCards.DojiMakoto).Single();
        var nitoshi = Cards(runner, 1, TutorialCards.BayushiNitoshi).Single(c => c.Zone != Zone.Hand);
        switch (step)
        {
            case "refused":
                Expect(nitoshi.Dishonorable && nitoshi.Zone == Zone.Home, "Nitoshi refused: he is dishonored and still at home");
                Expect(state.Players[0].Honor == 13, $"you gained 2 Honor for the refusal (11 to 13), the engine shows {state.Players[0].Honor}");
                break;
            case "reckless":
                Expect(runner.Engine.CardForceForTest(makoto.Id) == 4 && runner.Engine.CardForceForTest(nitoshi.Id) == 8, $"the Law of Darkness Dojo's -4 leaves Makoto on 4 Force (5, +3 from Reserve Weapon, -4) against Nitoshi's 8, the engine shows {runner.Engine.CardForceForTest(makoto.Id)} and {runner.Engine.CardForceForTest(nitoshi.Id)}");
                break;
            case "focus-courtesy":
                Expect(state.PendingDuel is { Stage: "Focusing" } && state.PendingDuel.ChallengedFocused.Count == 1, "Nitoshi, the challenged Personality, focused first");
                break;
            case "focus-deck":
                Expect(state.PendingDuel!.ChallengedFocused.Count == 2 && state.PendingDuel.ChallengerFocused.Count == 1, "Nitoshi focused twice, you once");
                break;
            case "duelist":
                Expect(state.PendingDuel is { Stage: "DuelistRefocus" }, "the duel is paused on the Duelist's swap");
                Expect(Has(runner, 0, TutorialCards.CourtesyOfTheCrane, Zone.Focused) && Has(runner, 0, TutorialCards.AStainCleansed, Zone.Focused), "Courtesy and A Stain Cleansed are your focused cards");
                break;
            case "outcome":
                var result = runner.Engine.GetView(0).LastDuel?.Result ?? throw new Exception("the duel has a result");
                Expect(result.ChallengerTotal == 14 && result.ChallengedTotal == 12 && result.WinnerId == makoto.Id, $"14 against 12, Makoto wins; the engine shows {result.ChallengerTotal} against {result.ChallengedTotal}");
                break;
            case "consequences":
                Expect(runner.Engine.GetView(0).LastDuel?.Result?.Provinces.Any(p => p.PlayerId == 0 && p.After - p.Before == 2) == true, "The Courtesy of the Crane gave your Provinces +2 strength");
                Expect(!nitoshi.Bowed && nitoshi.Zone == Zone.Battlefield, "Nitoshi's loss was delayed: he is still unbowed at the battle");
                break;
            case "makoto-duel":
                Expect(state.PriorityPlayerId == 0 && state.Phase == Phase.Battle, "it is your action in the battle");
                break;
            case "reserve":
                Expect(state.PendingDuel!.ChallengedFocused.Count == 1 && Has(runner, 1, TutorialCards.VeteranSkirmishers, Zone.Focused), "Nitoshi focused Veteran Skirmishers from his hand");
                Expect(state.Players[0].Hand.Count == 0, "you have no cards in hand");
                break;
            case "tie":
                var tie = runner.Engine.GetView(0).LastDuel?.Result ?? throw new Exception("the second duel has a result");
                Expect(tie.ChallengerTotal == 9 && tie.ChallengedTotal == 9 && tie.WinnerId == makoto.Id, $"9 against 9, won by Makoto the Duelist; the engine shows {tie.ChallengerTotal} against {tie.ChallengedTotal}");
                Expect(Has(runner, 1, TutorialCards.BayushiNitoshi, Zone.Dead), "Nitoshi was destroyed at once");
                break;
        }
    }

    private static TutorialRunner PlayTo(string step)
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; runner.Coach.StepId != step && guard < 100; guard++)
        {
            if (runner.Coach.Button is not null) runner.Continue(); else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{runner.Coach.StepId}: the move works");
        }
        Expect(runner.Coach.StepId == step, "reached " + step);
        return runner;
    }

    private static void QuotedNumbers()
    {
        // Before the swap Makoto trails 11 to 12; the Duelist's swap is what wins it. Keeping the focused cards loses the duel.
        var runner = PlayTo("duelist");
        var keep = runner.Engine.QueryActions(0).First(a => a.Enabled && a.Command?.Type == "duelistRefocus" && a.Command.CardId is null).Command!;
        Expect(runner.Engine.Execute(keep).Accepted, "keeping the focused cards is accepted");
        var order = runner.Engine.QueryActions(0).First(a => a.Enabled && a.Command?.Type == "orderDuelFocusEffects").Command!;
        Expect(runner.Engine.Execute(order).Accepted, "ordering the Focus Effects is accepted");
        var decide = runner.Engine.QueryActions(1).FirstOrDefault(a => a.Enabled && a.Label.Contains("delay", StringComparison.OrdinalIgnoreCase) && a.Type != "pass");
        if (decide is not null) Expect(runner.Engine.Execute(decide.Command!).Accepted, "the Scorpion's delay choice is accepted");
        var lost = runner.Engine.GetView(0).LastDuel?.Result ?? throw new Exception("the duel has a result");
        Expect(lost.ChallengerTotal == 11 && lost.ChallengedTotal == 12 && lost.WinnerId != Cards(runner, 0, TutorialCards.DojiMakoto).Single().Id, $"without the swap Makoto loses 11 to 12; the engine shows {lost.ChallengerTotal} to {lost.ChallengedTotal}");
        // The card text the notes quote.
        Expect(CardCatalog.Get(TutorialCards.CourtesyOfTheCrane).Focus == 4 && CardCatalog.Get(TutorialCards.Retribution).Focus == 4 && CardCatalog.Get(TutorialCards.AStainCleansed).Focus == 1, "Courtesy and Retribution have Focus Value 4, A Stain Cleansed 1");
        Expect(CardCatalog.Get(TutorialCards.LoyaltyOfTheScorpion).Focus == 2 && CardCatalog.Get(TutorialCards.Focus).Focus == 5 && CardCatalog.Get(TutorialCards.VeteranSkirmishers).Focus == 3 && CardCatalog.Get(TutorialCards.CrushingStrength).Focus == 1, "Loyalty 2, Focus 5, Veteran Skirmishers 3, Crushing Strength 1");
        Expect(CardCatalog.Get(TutorialCards.DojiMakoto).Chi == 6 && CardCatalog.Get(TutorialCards.BayushiNitoshi).Chi == 5, "Makoto has 6 Chi and Nitoshi 5");
    }

    private static void OpensAsCrane()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0 }, "it is the Crane's Action phase");
        Expect(state.Players[0].Honor == 11, "the Crane starts on 11 Honor");
        Expect(state.Players[1].Honor == CardCatalog.Get(TutorialCards.LawOfDarknessDojo).StartingHonor, "the Scorpion starts on its Stronghold's starting Honor");
        foreach (var holding in new[] { TutorialCards.ShelteredPort, TutorialCards.Marketplace, TutorialCards.SmallFarm, TutorialCards.LargeFarm, TutorialCards.BorderKeep, TutorialCards.DaidojiMarketplace }) Expect(Has(runner, 0, holding, Zone.Home), "the Crane holds " + holding);
        Expect(InPlay(runner, 0, TutorialCards.ReserveWeapon).AttachedTo == InPlay(runner, 0, TutorialCards.DojiMakoto).Id, "Reserve Weapon is attached to Makoto");
        Expect(new[] { TutorialCards.SanctionedDuel, TutorialCards.Retribution, TutorialCards.CourtesyOfTheCrane, TutorialCards.RecklessDuel }.All(d => Has(runner, 0, d, Zone.Hand)) && state.Players[0].Hand.Count == 4, "the Crane holds exactly the four cards named");
        foreach (var holding in new[] { TutorialCards.GeishaHouse, TutorialCards.SmallFarm, TutorialCards.LargeFarm, TutorialCards.ProsperousVillage }) Expect(Has(runner, 1, holding, Zone.Home), "the Scorpion holds " + holding);
        Expect(Cards(runner, 1, TutorialCards.GeishaHouse).Count(c => c.Zone == Zone.Home) == 2, "two Geisha Houses");
        Expect(Cards(runner, 1, TutorialCards.VeteranSkirmishers).Any(c => c.Zone == Zone.Attached && c.AttachedTo == InPlay(runner, 1, TutorialCards.BayushiNitoshi).Id), "Veteran Skirmishers is attached to Nitoshi");
        Expect(Has(runner, 1, TutorialCards.LoyaltyOfTheScorpion, Zone.Hand) && Has(runner, 1, TutorialCards.VeteranSkirmishers, Zone.Hand) && state.Players[1].Hand.Count == 2, "the Scorpion holds Loyalty of the Scorpion and a Veteran Skirmishers");
        Expect(CardCatalog.Get(TutorialCards.DojiMakoto).Keywords.Contains("Duelist") && !CardCatalog.Get(TutorialCards.BayushiNitoshi).Keywords.Contains("Duelist"), "Makoto is a Duelist and Nitoshi is not");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson);
        var steps = 0; string? lastButton = null;
        for (var guard = 0; !runner.Finished && guard < 100; guard++)
        {
            var coach = runner.Coach; steps++; lastButton = coach.Button;
            if (coach.Button is not null) { runner.Continue(); continue; }
            Expect(!string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: a move step says what to click");
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{coach.StepId}: the move works");
        }
        Expect(runner.Finished && steps >= 25, $"the lesson ran to its end in {steps} steps");
        Expect(lastButton == "Tutorial Menu", "the last note returns to the menu");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && guard < 100; guard++)
        {
            var before = runner.Coach.StepId; var button = runner.Coach.Button is not null;
            if (button) runner.Continue(); else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{before}: the move works");
            if (runner.Finished) break;
            var back = runner.Back();
            Expect(back is not null && back.Coach.StepId == before, $"Back from {runner.Coach.StepId} returns to {before}");
            runner = back!;
            if (button) runner.Continue(); else runner.Execute(CommandFor(runner));
        }
    }

    private static void Curriculum()
    {
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "Duels is built");
        Expect(TutorialCurriculum.After("special-attacks")?.Id == Lesson, "Duels follows Attacks");
    }
}
