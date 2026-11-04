using L5R.Core;

// The Attacks lesson: Dragon against Mantis, played through the runner the way a player would. A rules or card change that breaks something a step shows (Mantis attacking, the Reconnaissance bonus, a Follower
// protecting its Personality, a Ranged Attack with no target, Strong Defenses, Michi's Melee Attack) fails here instead of in front of a new player.
internal static class TutorialAttacksTests
{
    private const string Lesson = "special-attacks";

    public static void Run(TestRunner runner) => runner.Run("tutorial-attacks", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in Mantis's Action phase with the cards the notes name, Mantis first on 8 Honor each", OpensAsMantis),
        ("The numbers the notes quote hold: Haruhiko's Ranged 5 would destroy Torazu, only Strong Defenses saves him, and Michi's Melee 4 may target Tomaru or the Scout but never Haruhiko or Michi", QuotedNumbers),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Attacks is built and follows Traits and Actions", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold or Zone.Attached);
    private static bool Has(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);
    private static int Force(TutorialRunner runner, int owner, string definition) => runner.Engine.CardForceForTest(InPlay(runner, owner, definition).Id);

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/tgt={a.Command?.TargetId}/{(a.Enabled ? "on" : "off")}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing: " + Offered(runner.Actions().ToArray()));
        ActionOption First(Func<ActionOption, bool> match) => offered.FirstOrDefault(match) ?? throw new Exception($"{step}: no option matches: {Offered(offered)}");
        switch (step)
        {
            case "defend":
                return First(a => a.Command?.Type == "assignDefense").Command! with
                {
                    Assignments = new Dictionary<string, int> { [InPlay(runner, 0, TutorialCards.MirumotoMichi).Id] = 0, [InPlay(runner, 0, TutorialCards.TogashiTorazu).Id] = 0 }
                };
            case "melee":
                return First(a => a.Command!.AbilityId == "unparalleled-sword-attack").Command! with { TargetId = InPlay(runner, 1, TutorialCards.TsuruchiTomaru).Id };
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
        switch (step)
        {
            case "attacked":
                Expect(state.Phase == Phase.DefenseAssignment && Has(runner, 1, TutorialCards.TsuruchiTomaru, Zone.Battlefield) && Has(runner, 1, TutorialCards.YoritomoHaruhiko, Zone.Battlefield), "Mantis attacked with both Personalities");
                break;
            case "followers":
                Expect(state.Phase == Phase.Battle && InPlay(runner, 0, TutorialCards.VeteranSkirmishers).AttachedTo == InPlay(runner, 0, TutorialCards.MirumotoMichi).Id, "the battle is on and Veteran Skirmishers is attached to Michi");
                break;
            case "pass-first":
                Expect(state.PriorityPlayerId == 0 && state.Phase == Phase.Battle, "the defender holds the first action");
                break;
            case "no-target":
                Expect(Has(runner, 0, TutorialCards.VeteranSkirmishers, Zone.Attached) && !InPlay(runner, 0, TutorialCards.VeteranSkirmishers).Bowed, "Tomaru's Attack with no target destroyed nothing: " + string.Join("; ", Cards(runner, 0, TutorialCards.VeteranSkirmishers).Select(c => c.Zone + "/" + c.Bowed)));
                break;
            case "strong-defenses":
                Expect(Force(runner, 0, TutorialCards.TogashiTorazu) == 4, "Torazu has 4 Force (four Fire tokens)");
                break;
            case "blocked":
                Expect(Has(runner, 0, TutorialCards.TogashiTorazu, Zone.Battlefield), "Strong Defenses saved Torazu");
                break;
            case "melee-result":
                Expect(Has(runner, 1, TutorialCards.TsuruchiTomaru, Zone.Dead), "Michi's Melee 4 destroyed Tomaru, whose Force is 4");
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
        // Without Strong Defenses the Ranged 5 (Haruhiko's 3, +2 with Reconnaissance) destroys Torazu, whose Force is 4.
        var runner = PlayTo("strong-defenses");
        var decline = runner.Engine.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Type == "declineReaction") ?? throw new Exception("the Reaction can be declined");
        Expect(runner.Engine.Execute(decline.Command!).Accepted, "declining the Reaction is accepted");
        Expect(Has(runner, 0, TutorialCards.TogashiTorazu, Zone.Dead), "unanswered, Haruhiko's Ranged 5 destroys Torazu");
        // Michi's Melee 4: Tomaru (4 Force) and the Scout may be targeted; Haruhiko and Michi's own side may not.
        runner = PlayTo("melee");
        var targets = runner.Actions().Where(a => a.Enabled && a.Command?.AbilityId == "unparalleled-sword-attack").SelectMany(a => a.TargetIds ?? []).ToHashSet();
        Expect(targets.Contains(InPlay(runner, 1, TutorialCards.TsuruchiTomaru).Id), "Tomaru can be targeted");
        Expect(!targets.Contains(InPlay(runner, 1, TutorialCards.YoritomoHaruhiko).Id), "Haruhiko has a Follower, so he cannot be targeted");
        Expect(Force(runner, 1, TutorialCards.TsuruchiTomaru) == 4, "Tomaru has 4 Force, the Melee 4's strength");
    }

    private static void OpensAsMantis()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 1 }, "it is Mantis's Action phase");
        Expect(state.Players[0].Honor == 8 && state.Players[1].Honor == 8, "both start on 8 Honor");
        Expect(Force(runner, 0, TutorialCards.MirumotoMichi) == 8, "Michi is 8 Force with the Unparalleled Sword");
        Expect(Force(runner, 0, TutorialCards.TogashiTorazu) == 4, "Torazu is 4 Force from four Fire tokens");
        Expect(Cards(runner, 0, TutorialCards.TogashiTorazu).Single(c => c.Zone == Zone.Home).Tokens.Count(t => t.Type == "Fire") == 4, "four Fire tokens");
        Expect(Has(runner, 0, TutorialCards.StrongDefenses, Zone.Hand) && state.Players[0].Hand.Count == 1, "Strong Defenses is the only card in your hand");
        foreach (var holding in new[] { TutorialCards.BorderKeep, TutorialCards.GoldMine, TutorialCards.LargeFarm, TutorialCards.SmallFarm }) Expect(Has(runner, 0, holding, Zone.Home), "Dragon holds " + holding);
        foreach (var holding in new[] { TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.KobunePort, TutorialCards.CopperMine, TutorialCards.Marketplace }) Expect(Has(runner, 1, holding, Zone.Home), "Mantis holds " + holding);
        Expect(Has(runner, 1, TutorialCards.TsuruchiTomaru, Zone.Home) && Has(runner, 1, TutorialCards.YoritomoHaruhiko, Zone.Home), "Mantis has Tomaru and Haruhiko");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson);
        var steps = 0;
        for (var guard = 0; !runner.Finished && guard < 100; guard++)
        {
            var coach = runner.Coach; steps++;
            if (coach.Button is not null) { runner.Continue(); continue; }
            Expect(!string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: a move step says what to click");
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{coach.StepId}: the move works");
        }
        Expect(runner.Finished && steps > 5, "the lesson ran to its end");
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
        var ids = TutorialCurriculum.All.Select(e => e.Id).ToList();
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "Attacks is built");
        Expect(ids.IndexOf(Lesson) > ids.IndexOf("traits-actions"), "Attacks comes after Traits and Actions");
    }
}
