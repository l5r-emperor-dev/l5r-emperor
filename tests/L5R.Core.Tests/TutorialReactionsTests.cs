using L5R.Core;

// The Reactions lesson: Mantis against Unicorn, played through the runner the way a player would. A rules or card change that breaks something a step shows (Chrysanthemum Blossom on assignment, the announce-and-order
// window of R-039, Armor of the Heavens reacting inside it, Ryoko's penalty resolving before any Battle action, Naval Invasion's first action, Surrounded!'s +2, Lishan's reaction to an announcement) fails here.
internal static class TutorialReactionsTests
{
    private const string Lesson = "reactions";

    public static void Run(TestRunner runner) => runner.Run("tutorial-reactions", [
        ("The lesson plays through using only what each step offers, and the engine reaches the Honor, Force and bows every step names", FinishLesson),
        ("The lesson opens in your Action phase with the script's table: Mantis on 8 Honor against Unicorn on 13, every Province face down", OpensAsMantis),
        ("Every move step says what to click and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Reactions is built, follows Dishonor and precedes Imperial Favor", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static readonly Zone[] InPlay = [Zone.Home, Zone.Battlefield, Zone.Attached, Zone.Stronghold];
    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance? InPlayCard(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).SingleOrDefault(c => InPlay.Contains(c.Zone));
    private static int Force(TutorialRunner runner, int owner, string definition)
    {
        var id = InPlayCard(runner, owner, definition)!.Id;
        return runner.Engine.GetView(0).Players[owner].Army.Concat(runner.Engine.GetView(0).Players[owner].Home).Single(c => c.Id == id).Force;
    }
    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.AbilityId}/{a.Label}/{(a.Enabled ? "on" : "off")}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId; var state = runner.Engine.State;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede").ToArray();
        Expect(offered.Length >= 1, $"{step}: the step offers its move, got {Offered(runner.Actions())}");
        bool PerformedBy(ActionOption a, string definition) => a.Command!.PerformerId is { } id && state.Cards[id].DefinitionId == definition;
        var option = step switch
        {
            "rx-announce-naval" => offered.Single(a => PerformedBy(a, TutorialCards.YoritomoNakoshi)),
            "rx-announce-surrounded" => offered.Single(a => PerformedBy(a, TutorialCards.TsuruchiTomaru)),
            _ => offered.Length == 1 ? offered[0] : throw new Exception($"{step}: the step offers exactly its one move, got {Offered(offered)}")
        };
        var command = option.Command!;
        if (step == "rx-assign")
            command = command with { Assignments = new[] { TutorialCards.TsuruchiTomaru, TutorialCards.YoritomoNakoshi }.ToDictionary(d => InPlayCard(runner, 0, d)!.Id, _ => 0) };
        if (option.TargetIds is { Length: > 0 } targets && command.TargetId is null) command = command with { TargetId = targets[0] };
        return command;
    }

    private static void Play(TutorialRunner runner)
    {
        var result = runner.Execute(CommandFor(runner));
        Expect(result.Accepted, $"{runner.Coach.StepId}: {result.Error}");
    }

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 60; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            CheckTable(runner, coach.StepId);
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            Play(runner);
        }
        Expect(runner.Finished, "the lesson ended: " + runner.Coach.StepId);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once: " + string.Join(",", seen));
        Expect(seen.Count is >= 15 and <= 30, $"the lesson is 15-30 steps, it has {seen.Count}");
    }

    /// <summary>What the engine shows at the start of each step, checked against what the words of that step say.</summary>
    private static void CheckTable(TutorialRunner runner, string step)
    {
        var state = runner.Engine.State;
        int Unicorn() => state.Players[1].Honor;
        switch (step)
        {
            case "rx-declare":
                Expect(state is { Phase: Phase.AttackDeclaration, ActivePlayerId: 0 }, "your Attack phase");
                break;
            case "rx-blossom":
                var ryoko = InPlayCard(runner, 1, TutorialCards.UtakuRyoko)!; var lishan = InPlayCard(runner, 1, TutorialCards.UtakuLishan)!;
                Expect(ryoko.Zone == Zone.Battlefield && lishan.Zone == Zone.Battlefield, "the Unicorn defend with Ryoko and Lishan");
                Expect(ryoko.Tokens.Count == 1 && Unicorn() == 14, $"Chrysanthemum Blossom: a +1F token and 13 to 14 Honor (engine: {ryoko.Tokens.Count} tokens, {Unicorn()} Honor)");
                Expect(state.Phase == Phase.BattleSelection, "no battle has been chosen yet");
                break;
            case "rx-announce":
                Expect(state.PendingReactionOrder is { Stage: "Announce" }, "the engage window runs the announce-and-order procedure (R-039)");
                break;
            case "rx-armor":
                Expect(Unicorn() == 16, $"Armor of the Heavens: 14 to 16 Honor, engine {Unicorn()}");
                Expect(Force(runner, 1, TutorialCards.UtakuLishan) == 5, $"Lishan: 4 + Armor of the Heavens 3 - 2 = 5, engine {Force(runner, 1, TutorialCards.UtakuLishan)}");
                Expect(InPlayCard(runner, 0, TutorialCards.Khalimpehjiak)!.Bowed, "Khalimpeh-jiak bowed for its Engage");
                Expect(state.Events.Any(e => e.Type == "surroundedBonus"), "Surrounded! resolved first");
                break;
            case "rx-ryoko-done":
                Expect(Force(runner, 0, TutorialCards.TsuruchiTomaru) == 3, $"Tomaru: 4 + Khalimpeh-jiak 2 - Ryoko's 3 = 3, engine {Force(runner, 0, TutorialCards.TsuruchiTomaru)}");
                Expect(InPlayCard(runner, 1, TutorialCards.UtakuRyoko) is not null, "Ryoko is still in play when her Reaction resolves");
                Expect(state.PendingReactionOrder is null, "the window has closed");
                break;
            case "rx-surety":
                Expect(InPlayCard(runner, 1, TutorialCards.UtakuRyoko) is null, "Strategic Strike destroyed Ryoko");
                Expect(InPlayCard(runner, 0, TutorialCards.YoritomoNakoshi)!.Bowed && InPlayCard(runner, 0, TutorialCards.TsuruchiTomaru)!.Bowed, "Nakoshi bowed for the Strike; Surety of Purpose bowed Tomaru");
                Expect(Unicorn() == 18, $"Surety of Purpose: 16 to 18 Honor, engine {Unicorn()}");
                Expect(state.Events.Any(e => e.Message.Contains("Utaku Lishan") && e.Message.Contains("announce a Battle action")), "Lishan reacted to the announcement");
                break;
            case "rx-done":
                Expect(InPlayCard(runner, 0, TutorialCards.TsuruchiTomaru) is null && InPlayCard(runner, 0, TutorialCards.YoritomoNakoshi) is null, "your attackers were destroyed");
                Expect(InPlayCard(runner, 1, TutorialCards.UtakuLishan) is not null && !state.Players[1].Provinces[0].Destroyed, "Lishan stands and the Unicorn's Province 1 was not destroyed");
                Expect(state.Players[0].Honor == 8, "you are still on 8 Honor");
                Expect(Unicorn() == 24, $"the winning Defender gains 2 Honor for each of the 3 cards destroyed (18 to 24), engine {Unicorn()}");
                break;
        }
    }

    private static void OpensAsMantis()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, "it is your Action phase");
        Expect(state.Players[0].Honor == 8 && state.Players[1].Honor == 13, "8 Honor against 13");
        Expect(state.Players[0].Clan == "Mantis" && state.Players[1].Clan == "Unicorn", "Mantis against Unicorn");
        foreach (var card in new[] { TutorialCards.KobunePort, TutorialCards.SeasideBazaar, TutorialCards.ColonialHarbor, TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.TsuruchiTomaru, TutorialCards.YoritomoNakoshi })
            Expect(InPlayCard(runner, 0, card) is not null, "you have " + CardCatalog.Get(card).Name);
        foreach (var card in new[] { TutorialCards.BorderKeep, TutorialCards.SilkWorks, TutorialCards.SmallFarm, TutorialCards.ProsperousVillage, TutorialCards.Stables, TutorialCards.UtakuRyoko, TutorialCards.UtakuLishan })
            Expect(InPlayCard(runner, 1, card) is not null, "the Unicorn have " + CardCatalog.Get(card).Name);
        Expect(InPlayCard(runner, 0, TutorialCards.Khalimpehjiak)!.AttachedTo == InPlayCard(runner, 0, TutorialCards.TsuruchiTomaru)!.Id, "Tomaru carries Khalimpeh-jiak");
        Expect(InPlayCard(runner, 1, TutorialCards.ChrysanthemumBlossom)!.AttachedTo == InPlayCard(runner, 1, TutorialCards.UtakuRyoko)!.Id && InPlayCard(runner, 1, TutorialCards.ArmorOfTheHeavens)!.AttachedTo == InPlayCard(runner, 1, TutorialCards.UtakuLishan)!.Id, "the Unicorn's attachments");
        Expect(state.Players[0].Hand.Select(id => state.Cards[id].DefinitionId).OrderBy(d => d).SequenceEqual(new[] { TutorialCards.Surrounded, TutorialCards.StrategicStrike }.OrderBy(d => d)), "your hand: Surrounded! and Strategic Strike");
        Expect(state.Players[1].Hand.Select(id => state.Cards[id].DefinitionId).SequenceEqual([TutorialCards.SuretyOfPurpose]), "the Unicorn's hand: Surety of Purpose");
        Expect(state.Players.All(p => p.Provinces.All(pr => pr.CardId is { } id && !state.Cards[id].FaceUp)), "every Province is face down");
        // The card facts the notes quote.
        Expect(CardCatalog.Get(TutorialCards.YoritomoNakoshi).Keywords!.Contains("Naval") && CardCatalog.Get(TutorialCards.YoritomoNakoshi).Keywords!.Contains("Scout"), "Nakoshi is Naval and a Scout");
        Expect(!CardCatalog.Get(TutorialCards.TsuruchiTomaru).Keywords!.Contains("Naval") && CardCatalog.Get(TutorialCards.TsuruchiTomaru).Keywords!.Contains("Scout"), "Tomaru is a Scout, not Naval: one Naval card against none");
        Expect(CardCatalog.Get(TutorialCards.UtakuRyoko).PersonalHonor == 3 && CardCatalog.Get(TutorialCards.UtakuLishan).PersonalHonor == 3 && CardCatalog.Get(TutorialCards.TsuruchiTomaru).PersonalHonor == 2, "Personal Honor: Ryoko 3, Lishan 3, Tomaru 2");
        Expect(CardCatalog.Get(TutorialCards.UtakuLishan).Keywords!.Contains("Paragon"), "Lishan is a Paragon");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson);
        string? lastButton = null;
        for (var guard = 0; !runner.Finished && guard < 60; guard++)
        {
            var coach = runner.Coach; lastButton = coach.Button;
            if (coach.Button is not null) { runner.Continue(); continue; }
            Expect(!string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: a move step says what to click");
            Play(runner);
        }
        Expect(lastButton == "Tutorial Menu", "the last note returns to the menu");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && guard < 60; guard++)
        {
            var before = runner.Coach.StepId; var button = runner.Coach.Button is not null;
            if (button) runner.Continue(); else Play(runner);
            if (runner.Finished) break;
            var back = runner.Back();
            Expect(back is not null && back.Coach.StepId == before, $"Back from {runner.Coach.StepId} returns to {before}");
            runner = back!;
            if (button) runner.Continue(); else Play(runner);
        }
    }

    private static void Curriculum()
    {
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "Reactions is built");
        Expect(TutorialCurriculum.After("special-dishonor")?.Id == Lesson && TutorialCurriculum.After(Lesson)?.Id == "imperial-favor", "Reactions sits between Dishonor and Imperial Favor");
    }
}
