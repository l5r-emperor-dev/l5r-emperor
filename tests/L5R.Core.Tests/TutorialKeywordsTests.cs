using L5R.Core;

// Issue #1078, the Keywords lesson: Crane against Scorpion, then Spider against Unicorn, then Spider against a lone Stalwart Personality, played through the runner the way a player would. A rules or card change that
// breaks a keyword the lesson shows (Seppuku, Kensai's second Weapon, Cavalry, Naval Invasion, Tactical Advantage, Elite, Conquest, Stalwart) fails here instead of in front of a new player.
internal static class TutorialKeywordsTests
{
    private const string Lesson = "keywords";

    public static void Run(TestRunner runner) => runner.Run("tutorial-keywords", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in your Action phase as Crane against Scorpion with four Personalities in play", OpensAsCrane),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move, across the table changes too", BackUndoesTheLastStep),
        ("Keywords 1 and Keywords 2 are both listed, the first built and the second not yet", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold or Zone.Attached);
    private static string Id(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).First(c => c.Zone == zone).Id;

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/perf={a.Command?.PerformerId}/tgt={a.Command?.TargetId}/targets={string.Join(",", a.TargetIds ?? [])}/{(a.Enabled ? "on" : "off:" + a.Reason)}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing: " + Offered(runner.Actions().ToArray()));
        string Own(string definition) => InPlay(runner, 0, definition).Id;
        string Foe(string definition) => InPlay(runner, 1, definition).Id;
        switch (step)
        {
            case "seppuku-1":
            case "seppuku-2":
            case "seppuku-3":
                {
                    var target = new[] { TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi }.SelectMany(d => Cards(runner, 0, d)).FirstOrDefault(c => c.Zone == Zone.Home)?.Id ?? throw new Exception($"{step}: none of the three is at home: " + string.Join(", ", runner.Engine.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Dead or Zone.Battlefield).Select(c => c.DefinitionId + ":" + c.Zone)));
                    var option = offered.FirstOrDefault(a => a.Command!.Type == "orderSeppuku" && (a.CardId == target || a.Command.CardId == target)) ?? throw new Exception($"{step}: no seppuku option for the target: {Offered(offered)}");
                    return option.Command!;
                }
            case "assign-attackers":
                return offered.Single().Command! with { Assignments = new[] { TutorialCards.DaigotsuShiraume, TutorialCards.DaigotsuShikenuro, TutorialCards.DaigotsuHotako, TutorialCards.Ohaba }.ToDictionary(Own, _ => 0) };
            case "naval":
                return offered.First(a => a.Command!.AbilityId == "naval-invasion").Command! with { PerformerId = Own(TutorialCards.DaigotsuHotako) };
            case "paragon":
                {
                    var option = offered.FirstOrDefault(a => a.Command!.AbilityId == "paragon-strength-attack" && a.Command.PerformerId == Own(TutorialCards.DaigotsuHotako)) ?? throw new Exception($"{step}: no option plays it: {Offered(offered)}");
                    return option.Command! with { TargetId = Foe(TutorialCards.HorseArchers) };
                }
            case "unorthodox":
                {
                    var option = offered.FirstOrDefault(a => a.Command!.AbilityId == "unorthodox-attack" && (a.TargetIds?.Contains(Foe(TutorialCards.BayushiKasumi)) ?? false)) ?? throw new Exception($"{step}: no option plays it: {Offered(offered)}");
                    return option.Command! with { TargetId = Foe(TutorialCards.BayushiKasumi) };
                }
            case "conqueror":
                return runner.Actions().First(a => a.Enabled && a.Command?.Type == "chooseConqueror").Command!;
            case "paid-off-1":
            case "paid-off-2":
                {
                    var option = offered.FirstOrDefault(a => a.Command!.AbilityId == "paid-bow") ?? throw new Exception($"{step}: no option plays it: {Offered(offered)}");
                    return option.Command! with { TargetId = Foe(TutorialCards.ShibaJikaro) };
                }
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {Offered(offered)}");
                return offered[0].Command!;
        }
    }

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>(); string? chosen = null;
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            CheckTable(runner, coach.StepId, ref chosen);
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended: " + runner.Coach.StepId);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
    }

    private static CardView ViewOf(TutorialRunner runner, string definition) => runner.Engine.GetView(0).Players.SelectMany(p => p.Army.Concat(p.Home)).First(c => c.Definition?.Id == definition);
    private static CardInstance Mine(TutorialRunner runner, string definition) => InPlay(runner, 0, definition);

    /// <summary>What the engine shows at the start of each step, checked against what the words of that step say (and the Comprehensive Rules behind them).</summary>
    private static void CheckTable(TutorialRunner runner, string step, ref string? chosen)
    {
        var state = runner.Engine.State; var hotako = TutorialCards.DaigotsuHotako;
        switch (step)
        {
            case "seppuku-1":
                Expect(new[] { TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi, TutorialCards.KakitaMaratai }.All(d => Mine(runner, d).Dishonorable), "all four Personalities are dishonorable");
                var offered = runner.Actions().Where(a => a.Enabled && a.Command?.Type == "orderSeppuku").Select(a => a.CardId ?? a.Command!.CardId).ToHashSet();
                Expect(offered.SetEquals(new[] { TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi }.Select(d => Mine(runner, d).Id)), "seppuku is offered for the Courtier, the Shugenja and the Samurai, and not for Kakita Maratai");
                break;
            case "seppuku-result":
                Expect(state.Players[0].Honor == 8, $"seppuku cost no Honor, the engine shows {state.Players[0].Honor}");
                Expect(new[] { TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi }.All(d => Cards(runner, 0, d).Any(c => c.Zone == Zone.Dead)), "the three Personalities who committed seppuku are dead");
                Expect(Mine(runner, TutorialCards.KakitaMaratai) is { Dishonorable: true, Zone: Zone.Home }, "Kakita Maratai is still dishonorable and in play");
                break;
            case "shugenja":
                Expect(Mine(runner, TutorialCards.AsahinaKonomi) is { Dishonorable: false, Zone: Zone.Home } && Mine(runner, TutorialCards.ScentOfDishonor).AttachedTo == Mine(runner, TutorialCards.AsahinaKonomi).Id, "Konomi is back, honorable, with Scent of Dishonor attached");
                break;
            case "kensai":
                Expect(new[] { TutorialCards.BladeOfPerfection, TutorialCards.Hunger }.All(d => Mine(runner, d).AttachedTo == Mine(runner, TutorialCards.Ohaba).Id), "Ohaba carries two Weapons");
                Expect(state.Players[0].Clan == "Spider" && state.Players[1].Clan == "Unicorn", "the table is Spider against Unicorn");
                break;
            case "maneuvers":
                Expect(InPlay(runner, 1, TutorialCards.BayushiKasumi).Zone == Zone.Battlefield && InPlay(runner, 1, TutorialCards.MotoTaha).Zone == Zone.Battlefield, "Kasumi and Moto Taha both defend");
                break;
            case "paragon":
                Expect(runner.Actions().Any(a => a.Enabled && a.Command?.AbilityId == "paragon-strength-attack" && a.Command.PerformerId == Mine(runner, hotako).Id), "Hotako may play A Paragon's Strength");
                Expect(runner.Engine.QueryActions(0).Any(a => !a.Enabled && a.Reason?.Contains("Naval Invasion") == true), "Naval Invasion bars every other performer from the first action");
                break;
            case "tactician":
                Expect(!Cards(runner, 1, TutorialCards.HorseArchers).Any(c => c.Zone is Zone.Battlefield or Zone.Attached), "the Horse Archers were shot down");
                Expect(state.Players[0].Honor == 6, $"{string.Join(" | ", state.Events.TakeLast(12).Select(e => e.Message))} A Paragon's Strength cost 4 Honor, the engine shows {state.Players[0].Honor}");
                break;
            case "overwhelmed":
                Expect(ViewOf(runner, TutorialCards.MotoTaha).Force == 6, $"Moto Taha has 1 Force and +5 from Tactical Advantage, the engine shows {ViewOf(runner, TutorialCards.MotoTaha).Force}");
                Expect(!runner.Engine.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "tactical-advantage"), "a Tactician performs one Tactical action per turn");
                break;
            case "elite":
                Expect(InPlay(runner, 1, TutorialCards.BayushiKasumi).Bowed, "Bayushi Kasumi is bowed");
                Expect(ViewOf(runner, TutorialCards.BayushiKasumi).UnitForce == 4, $"a bowed Elite Personality still contributes his 4 Force, the engine shows {ViewOf(runner, TutorialCards.BayushiKasumi).UnitForce}");
                break;
            case "conqueror":
                Expect(runner.Actions().Count(a => a.Enabled && a.Command?.Type == "chooseConqueror") == 2, "both Conquerors may be chosen");
                var pick = runner.Actions().First(a => a.Enabled && a.Command?.Type == "chooseConqueror"); chosen = pick.CardId ?? pick.Command!.TargetId ?? pick.Command.CardId;
                break;
            case "conqueror-result":
                Expect(state.Players[1].Provinces[0].Destroyed, "Province 1 was destroyed");
                var picked = chosen; var conquerors = Cards(runner, 0, TutorialCards.DaigotsuShiraume).Concat(Cards(runner, 0, TutorialCards.DaigotsuShikenuro)).ToArray();
                Expect(!conquerors.Single(c => c.Id == picked).Bowed, "the chosen Conqueror did not bow");
                Expect(conquerors.Single(c => c.Id != picked).Bowed, "the other Conqueror bowed from the resolution");
                break;
            case "stalwart-intro":
                Expect(InPlay(runner, 1, TutorialCards.ShibaJikaro).Zone == Zone.Home && Mine(runner, TutorialCards.ShinomenMarsh).Zone == Zone.Home && Cards(runner, 0, TutorialCards.SmallFarm).Count(c => c.Zone == Zone.Home) == 1, "the Stalwart table is drawn at the intro: Jikaro, one Marsh and one Small Farm");
                break;
            case "paid-off-1":
                Expect(state.Players[1].Clan == "Unicorn" && !InPlay(runner, 1, TutorialCards.ShibaJikaro).Bowed && state.Players[0].Hand.Count == 2, "a lone unbowed Shiba Jikaro and two Paid Offs");
                break;
            case "paid-off-2":
                Expect(!InPlay(runner, 1, TutorialCards.ShibaJikaro).Bowed, "Stalwart negated the first bowing");
                Expect(state.Players[0].Hand.Count == 1, $"the first Paid Off was still played and paid for, the hand holds {state.Players[0].Hand.Count}");
                break;
            case "congratulations":
                Expect(InPlay(runner, 1, TutorialCards.ShibaJikaro).Bowed, "the second bowing worked");
                break;
        }
    }

    private static void OpensAsCrane()
    {
        var runner = TutorialRunner.Start(Lesson); var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, $"it opens in your Action phase: {state.Phase} {state.ActivePlayerId}/{state.PriorityPlayerId}");
        Expect(state.Players[0].Clan == "Crane" && state.Players[1].Clan == "Scorpion", "Crane against Scorpion");
        Expect(new[] { TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi, TutorialCards.KakitaMaratai }.All(d => InPlay(runner, 0, d).Zone == Zone.Home), "your four Personalities are in play");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson); var last = "";
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; last = coach.StepId;
            Expect(coach.Title.Length > 0 && coach.Body.Length > 0, $"{coach.StepId} has a title and words");
            if (coach.Button is not null) { runner.Continue(); continue; }
            Expect(coach.Instruction is { Length: > 0 }, $"{coach.StepId}: an action step says what to click");
            var result = runner.Execute(CommandFor(runner)); Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(last == "congratulations", "the lesson ends on the congratulations note");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
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
        var first = TutorialCurriculum.All.Single(e => e.Id == "keywords"); var second = TutorialCurriculum.All.Single(e => e.Id == "keywords-2");
        Expect(first.Title == "Keywords 1" && first.Built && second.Title == "Keywords 2" && !second.Built, "Keywords 1 is built, Keywords 2 is listed");
        Expect(TutorialCurriculum.After("keywords")?.Id == "keywords-2", "Keywords 2 follows Keywords 1");
    }
}
