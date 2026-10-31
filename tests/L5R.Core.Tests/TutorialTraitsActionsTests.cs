using L5R.Core;

// The Traits and Actions lesson: Mantis against Crane, played through the runner the way a player would. A rules or card change that breaks something a step shows (the Border Keep refill, Minori's Force, Lobby,
// Naval Invasion, Unorthodox Attack, Munefusa sending a Personality home, Back to the Front) fails here instead of in front of a new player.
internal static class TutorialTraitsActionsTests
{
    private const string Lesson = "traits-actions";

    public static void Run(TestRunner runner) => runner.Run("tutorial-traits-actions", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in your Action phase as Mantis against Crane with the cards the notes name", OpensAsMantis),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Traits and Actions is built and follows Keywords 2", Curriculum),
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
            case "choose-provinces":
                return First(a => a.Command!.AbilityId == "border-game").Command! with { CardIds = [.. Enumerable.Range(0, 4).Select(i => runner.Engine.State.Players[0].Provinces[i].CardId!)] };
            case "naval":
                return First(a => a.Command!.AbilityId == "naval-invasion").Command! with { PerformerId = InPlay(runner, 0, TutorialCards.YoritomoHama).Id };
            case "bow-chiisai":
                return First(a => a.Command!.AbilityId == "unorthodox-attack").Command! with { PerformerId = InPlay(runner, 0, TutorialCards.YoritomoHama).Id, TargetId = InPlay(runner, 1, TutorialCards.Chiisai).Id };
            case "back-to-the-front":
                return First(a => a.Command!.AbilityId == "back-to-the-front-move").Command! with { PerformerId = InPlay(runner, 0, TutorialCards.YoritomoHama).Id, Province = 0 };
            case "assign-attackers":
                return First(a => a.Command?.Type == "attack").Command! with
                {
                    Assignments = new Dictionary<string, int> { [InPlay(runner, 0, TutorialCards.YoritomoMinori).Id] = 0, [InPlay(runner, 0, TutorialCards.YoritomoHama).Id] = 0 }
                };
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

    /// <summary>What the engine shows at the start of each step, checked against what the words of that step say (and the Comprehensive Rules and card text behind them).</summary>
    private static void CheckTable(TutorialRunner runner, string step)
    {
        var state = runner.Engine.State;
        switch (step)
        {
            case "border-keep":
                Expect(!InPlay(runner, 0, TutorialCards.BorderKeep).Bowed && Has(runner, 1, TutorialCards.BorderKeep, Zone.Home), "each player has an unbowed Border Keep in play");
                break;
            case "minori":
                Expect(Force(runner, 0, TutorialCards.YoritomoMinori) == 4, "Minori has her printed 4 Force outside an army");
                break;
            case "limited":
                Expect(state is { Phase: Phase.Action, ActivePlayerId: 0 } && state.Players[0].Provinces.All(p => p.CardId is { } id && !state.Cards[id].FaceUp), "it is your Action phase and your Provinces are face down");
                break;
            case "use-border-keep":
                {
                    var border = runner.Engine.QueryActions(0).Where(a => a.Command?.AbilityId is "border-first" or "border-game").ToArray();
                    Expect(border.Any(a => a.Command!.AbilityId == "border-game" && a.Enabled) && border.All(a => a.Command!.AbilityId != "border-first" || !a.Enabled), "the once per game ability is open and the first turn one is not: " + Offered(border));
                    break;
                }
            case "opponent-lobbies":
                Expect(InPlay(runner, 1, TutorialCards.DojiDainagon).Bowed && state.FavorPlayerId == 1, "Dainagon bowed to Lobby and the Crane holds the Imperial Favor");
                Expect(state.Players[0].Provinces.All(p => p.CardId is { } id && state.Cards[id].FaceUp) && Cards(runner, 0, TutorialCards.SmallFarm).Count(c => c.Zone == Zone.Province) == 0, "the Border Keep refilled your Provinces face up");
                break;
            case "pass-to-attack":
                Expect(state.Phase == Phase.Action, "you may still pass in your Action phase");
                break;
            case "force":
                Expect(Force(runner, 0, TutorialCards.YoritomoMinori) == 5 && Force(runner, 0, TutorialCards.YoritomoHama) == 4, "Minori has 5 Force with Hama in her army, and Hama gains nothing");
                Expect(Has(runner, 1, TutorialCards.Chiisai, Zone.Battlefield) && Has(runner, 1, TutorialCards.AsahinaMunefusa, Zone.Battlefield) && !InPlay(runner, 1, TutorialCards.Chiisai).Bowed, "Chiisai and Munefusa defend, unbowed");
                break;
            case "naval":
                Expect(state.Phase == Phase.Battle, "the battle is engaged and Naval Invasion is offered");
                break;
            case "bow-chiisai":
                Expect(state.Phase == Phase.Battle && state.PriorityPlayerId == 0 && !InPlay(runner, 1, TutorialCards.Chiisai).Bowed, "you have the first Battle action and Chiisai is unbowed");
                break;
            case "hama-home":
                Expect(InPlay(runner, 1, TutorialCards.Chiisai).Bowed, "Unorthodox Attack bowed Chiisai");
                Expect(InPlay(runner, 0, TutorialCards.YoritomoHama).Zone == Zone.Home && Force(runner, 0, TutorialCards.YoritomoMinori) == 4, "Munefusa sent Hama home and Minori is back to 4 Force");
                break;
            case "presence":
                Expect(InPlay(runner, 0, TutorialCards.YoritomoHama).Zone == Zone.Battlefield && Force(runner, 0, TutorialCards.YoritomoMinori) == 5, "Back to the Front returned Hama and Minori has 5 Force again");
                break;
        }
    }

    private static void OpensAsMantis()
    {
        var runner = TutorialRunner.Start(Lesson); var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, $"it opens in your Action phase: {state.Phase} {state.ActivePlayerId}/{state.PriorityPlayerId}");
        Expect(state.Players[0].Clan == "Mantis" && state.Players[1].Clan == "Crane" && state.Players[1].Honor == 10, "Mantis against Crane with 10 Honor");
        Expect(Has(runner, 0, TutorialCards.BackToTheFront, Zone.Hand) && Has(runner, 0, TutorialCards.UnorthodoxAttack, Zone.Hand), "Back to the Front and Unorthodox Attack are in your hand");
        Expect(new[] { TutorialCards.BorderKeep, TutorialCards.YoritomoMinori, TutorialCards.YoritomoHama }.All(d => Has(runner, 0, d, Zone.Home)), "Border Keep, Minori and Hama are in play");
        Expect(new[] { TutorialCards.BorderKeep, TutorialCards.DojiDainagon, TutorialCards.AsahinaMunefusa, TutorialCards.Chiisai }.All(d => Has(runner, 1, d, Zone.Home)), "the Crane has a Border Keep, Dainagon, Munefusa and Chiisai");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson); var last = "";
        for (var guard = 0; !runner.Finished && guard < 100; guard++)
        {
            var coach = runner.Coach; last = coach.StepId;
            Expect(coach.Body.Length > 20, $"{coach.StepId}: it says something");
            Expect(coach.Button is not null || !string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: it has a button or tells the player what to click");
            if (coach.Button is not null) { runner.Continue(); continue; }
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{coach.StepId}: the move works");
        }
        Expect(last == "congratulations" && runner.Coach.Button is not null || runner.Finished, "the lesson ends on its last note");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && guard < 100; guard++)
        {
            var coach = runner.Coach;
            var back = runner.Back();
            if (back is not null) Expect(back.Coach.Step == coach.Step - 1, $"{coach.StepId}: Back goes one step back");
            if (coach.Button is not null) { runner.Continue(); continue; }
            var result = runner.Execute(CommandFor(runner)); Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
    }

    private static void Curriculum()
    {
        var entry = TutorialCurriculum.All.Single(e => e.Id == "traits-actions");
        Expect(entry.Title == "Traits and Actions" && entry.Built, "Traits and Actions is built");
        Expect(TutorialCurriculum.After("keywords-2")?.Id == "traits-actions" && TutorialCurriculum.After("traits-actions")?.Id == "cost-effect", "it follows Keywords 2");
    }
}
