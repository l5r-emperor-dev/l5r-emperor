using L5R.Core;

// The Rings and Enlightenment lesson: Dragon Enlightenment against Scorpion, played through the runner the way a player would, then played out to the Enlightenment Victory (and to a loss).
// A rules or card change that breaks what a step says (Hizumi's 6 Force against a strength-7 province, his -4, the Ring of Fire's entry after a destroyed province, the five-Ring win) fails here.
internal static class TutorialRingsTests
{
    private const string Lesson = "rings";

    public static void Run(TestRunner runner) => runner.Run("tutorial-rings", [
        ("The lesson opens in your Action phase with the script's table: Dragon on 9 Honor with one Province and four Rings, Scorpion on 2 with everyone bowed", OpensLateInTheGame),
        ("The scripted steps play through with only what each offers: Hizumi destroys the province at strength 3 and the Ring of Fire enters play by its own text", ScriptedPart),
        ("Once played out, every move is yours, the lesson does not play the Scorpion, and surviving to your next turn wins by Enlightenment and ends the lesson", WinsByEnlightenment),
        ("A lost game shows the loss and offers the lesson again", LossOffersRestart),
        ("Rings and Enlightenment is built and is the last lesson: Imperial Favor's Next Tutorial leads to it, and it offers none", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static readonly Zone[] InPlay = [Zone.Home, Zone.Battlefield, Zone.Attached, Zone.Stronghold];
    private static CardInstance? InPlayCard(TutorialRunner runner, int owner, string definition) =>
        runner.Engine.State.Cards.Values.SingleOrDefault(c => c.OwnerId == owner && c.DefinitionId == definition && InPlay.Contains(c.Zone));
    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.AbilityId}/{(a.Enabled ? "on" : "off")}"));
    private static readonly string[] Rings = [TutorialCards.RingOfTheVoid, TutorialCards.RingOfEarth, TutorialCards.RingOfWater, TutorialCards.RingOfAir, TutorialCards.RingOfFire];

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede").ToArray();
        Expect(offered.Length >= 1, $"{step}: the step offers its move, got {Offered(runner.Actions())}");
        var option = step is "rg-engage" or "rg-assign" ? offered[0] : offered.Length == 1 ? offered[0] : throw new Exception($"{step}: the step offers exactly its one move, got {Offered(offered)}");
        var command = option.Command!;
        if (step == "rg-assign")
            command = command with { Assignments = new Dictionary<string, int> { [InPlayCard(runner, 0, TutorialCards.TogashiHizumi)!.Id] = 2 } };
        if (option.TargetIds is { Length: > 0 } targets && command.TargetId is null) command = command with { TargetId = targets[0] };
        return command;
    }

    private static TutorialRunner ToFreePlay()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>();
        for (var guard = 0; !runner.FreePlay && guard < 40; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            CheckTable(runner, coach.StepId);
            Expect(coach.Button is not null || !string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: a move step says what to click");
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.FreePlay && runner.Coach.StepId == "rg-survive", "the lesson reached the played-out step: " + runner.Coach.StepId);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once: " + string.Join(",", seen));
        Expect(seen.Count + 1 is >= 15 and <= 30, $"the lesson is 15-30 steps, it has {seen.Count + 1}");
        return runner;
    }

    private static void CheckTable(TutorialRunner runner, string step)
    {
        var state = runner.Engine.State;
        switch (step)
        {
            case "rg-declare":
                Expect(state is { Phase: Phase.AttackDeclaration, ActivePlayerId: 0 }, $"your Attack phase, engine {state.Phase}");
                break;
            case "rg-hizumi":
                Expect(InPlayCard(runner, 0, TutorialCards.TogashiHizumi) is { Zone: Zone.Battlefield }, "Hizumi is at the battle");
                Expect(state.CurrentBattleProvince is int field && runner.Engine.ProvinceStrengthForTest(1, field) == 7, "the province has strength 7");
                Expect(CardCatalog.Get(TutorialCards.TogashiHizumi).Force == 6, "Hizumi has 6 Force");
                Expect(!state.Cards.Values.Any(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield), "nobody defends");
                break;
            case "rg-pass":
                var lowered = state.CurrentBattleProvince ?? -1;
                Expect(lowered >= 0 && runner.Engine.ProvinceStrengthForTest(1, lowered) == 3, $"the province has strength 3 after -4, engine {(lowered >= 0 ? runner.Engine.ProvinceStrengthForTest(1, lowered) : -1)}");
                break;
            case "rg-fire-play":
                Expect(state.Players[1].Provinces.Count(p => p.Destroyed) == 1, "Hizumi destroyed the province");
                Expect(state.Players[0].Hand.Any(id => state.Cards[id].DefinitionId == TutorialCards.RingOfFire), "the Ring of Fire is still in your hand");
                break;
        }
    }

    private static void OpensLateInTheGame()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, $"it is your Action phase, engine {state.Phase}");
        Expect(state.Players[0].Honor == 9 && state.Players[1].Honor == 2, "9 Honor against 2");
        Expect(state.Players[0].Clan == "Dragon" && state.Players[1].Clan == "Scorpion", "Dragon against Scorpion");
        Expect(state.Players[0].Provinces.Count(p => !p.Destroyed) == 1, "you have one Province left");
        var last = state.Players[0].Provinces.Single(p => !p.Destroyed);
        Expect(last.CardId is { } osawa && state.Cards[osawa].DefinitionId == TutorialCards.TogashiOsawaExp, "it holds Togashi Osawa");
        foreach (var ring in Rings.Take(4)) Expect(InPlayCard(runner, 0, ring) is { Bowed: false }, CardCatalog.Get(ring).Name + " is in play");
        foreach (var card in new[] { TutorialCards.TogashiHizumi, TutorialCards.TogashiKasuru, TutorialCards.TogashiTsukagi, TutorialCards.TempleOfHarmony, TutorialCards.TattoosAndTrinkets })
            Expect(InPlayCard(runner, 0, card) is { Bowed: false }, "you have " + CardCatalog.Get(card).Name);
        foreach (var card in new[] { TutorialCards.BayushiWaru, TutorialCards.ShosuroKoshiba, TutorialCards.ShosuroTsuji })
            Expect(InPlayCard(runner, 1, card) is { Bowed: true }, CardCatalog.Get(card).Name + " is bowed");
        Expect(state.Players[0].Hand.Select(id => state.Cards[id].DefinitionId).OrderBy(d => d)
            .SequenceEqual(new[] { TutorialCards.RingOfFire, TutorialCards.NerveStrike, TutorialCards.GrapplingTheSnake, TutorialCards.DeadlyDiscipline }.OrderBy(d => d)), "your hand: the Ring of Fire and three Kiho");
        Expect(state.Players[1].Hand.Count == 5, "the Scorpion hold five cards");
        Expect(state.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.FateDiscard) == 7, "seven cards in your Fate discard pile");
        Expect(state.Players[1].Provinces.All(p => p.CardId is { } id && !state.Cards[id].FaceUp), "the Scorpion's provinces are face down");
    }

    private static void ScriptedPart()
    {
        var runner = ToFreePlay();
        var state = runner.Engine.State;
        var fire = InPlayCard(runner, 0, TutorialCards.RingOfFire);
        Expect(fire is not null, "the Ring of Fire entered play");
        Expect(runner.Coach is { FreePlay: true, Button: null, CanGoBack: false }, "the played-out step has no button and no Back");
        Expect(runner.Engine.WinnerId is null, "nobody has won yet: the win comes as your next turn starts");
        Expect(Rings.All(r => InPlayCard(runner, 0, r) is not null), "all five Rings are in play");
        _ = state;
    }

    /// <summary>Both sides only pass (the client's Hard opponent plays the Scorpion in the real lesson; here the Scorpion attacks nothing), so you survive to your next turn.</summary>
    private static void WinsByEnlightenment()
    {
        var runner = ToFreePlay();
        var engine = runner.Engine;
        var everything = engine.QueryActions(0);
        Expect(runner.Actions().Length == everything.Length && runner.Actions().Zip(everything).All(p => p.First.Enabled == p.Second.Enabled), "every move the engine offers is yours");
        var startTurn = engine.State.TurnNumber;
        for (var guard = 0; engine.WinnerId is null && guard < 400; guard++)
        {
            var seat = engine.PriorityPlayerId;
            var options = (seat == 0 ? runner.Actions() : engine.QueryActions(1)).Where(a => a.Enabled && a.Command is not null && a.Type != "concede").ToArray();
            var pick = options.FirstOrDefault(a => a.Type is "declineReaction" or "pass" or "passAllBattles" or "endTurn") ?? options.FirstOrDefault(a => a.Type != "declareAttack" && a.Type != "useAbility") ?? options.FirstOrDefault();
            Expect(pick is not null, $"seat {seat} has a move at {engine.State.Phase}");
            var result = seat == 0 ? runner.Execute(pick!.Command!) : engine.Execute(pick!.Command!);
            Expect(result.Accepted, $"seat {seat} {pick.Type}: {result.Error}");
            if (seat == 1 && engine.WinnerId is null) Expect(runner.Coach.StepId == "rg-survive", "the lesson doesn't move on while the game is played");
        }
        Expect(engine.WinnerId == 0, $"you won, engine winner {engine.WinnerId} ({engine.State.VictoryReason})");
        Expect(engine.State.TurnNumber == startTurn + 2, "at the start of your next turn");
        Expect(engine.State.VictoryReason?.Contains("Enlightenment", StringComparison.OrdinalIgnoreCase) == true, "by Enlightenment: " + engine.State.VictoryReason);
        var coach = runner.Coach;
        Expect(coach.Body == "CONGRATULATIONS! YOU HAVE COMPLETED ALL OF THE TUTORIALS!" && coach.Button == "Tutorial Menu" && coach.NextLessonId is null && !coach.Restart, "the win note offers only the menu");
        Expect(runner.Continue() && runner.Finished, "its button ends the lesson");
    }

    private static void LossOffersRestart()
    {
        var runner = ToFreePlay();
        var concede = runner.Actions().Single(a => a.Type == "concede" && a.Enabled);
        Expect(runner.Execute(concede.Command!).Accepted, "you may concede");
        Expect(runner.Engine.WinnerId == 1, "the Scorpion win");
        var coach = runner.Coach;
        Expect(coach.Body == "YOU LOSE! TRY AGAIN!" && coach.Button == "Restart Tutorial" && coach.Restart && coach.NextLessonId is null, "the loss note offers the lesson again");
        Expect(runner.LostFreePlay && !runner.Finished, "the client restarts the lesson from that button");
    }

    private static void Curriculum()
    {
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "the menu starts it");
        Expect(TutorialLessons.All[^1].Id == Lesson, "it is the last lesson");
        Expect(TutorialCurriculum.After("imperial-favor")?.Id == Lesson, "it follows Imperial Favor");
        var levelUp = TutorialCurriculum.TrackOf(Lesson)!.Entries;
        Expect(levelUp[^1].Id == Lesson, "it ends Level Up Your Play");
        // Every other lesson's Next Tutorial starts the next built lesson, in the menu's order.
        var lessons = TutorialLessons.All;
        for (var i = 0; i + 1 < lessons.Length; i++)
            Expect(TutorialCurriculum.After(lessons[i].Id) is { Built: true } next && next.Id == lessons[i + 1].Id, $"{lessons[i].Id}'s Next Tutorial starts {lessons[i + 1].Id}, got {TutorialCurriculum.After(lessons[i].Id)?.Id}");
    }
}
