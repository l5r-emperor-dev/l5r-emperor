using L5R.Core;

// The Cost and Effect lesson: Scorpion against Dragon, played through the runner the way a player would. A rules or card change that breaks something a step shows (Mysterious Deaths' 6 Gold and its three effects,
// The Merchant's District's bow and condition, Khalimpeh-jiak's -2F and the Melee 4 that follows, Revenge's bow-then-destroy, the Towers ability and At Any Cost's recruit) fails here.
internal static class TutorialCostEffectTests
{
    private const string Lesson = "cost-effect";

    public static void Run(TestRunner runner) => runner.Run("tutorial-cost-effect", [
        ("The lesson plays through using only what each step offers, and the engine reaches the Gold, Honor, Force and destructions every step names", FinishLesson),
        ("The lesson opens in your Action phase with the script's table: Scorpion on 2 Honor against Dragon on 8, your Ninja made, every Province face down", OpensAsScorpion),
        ("Mysterious Deaths may not be paid with The Merchant's District, and the District's condition bars it with five cards in hand", RefusesWhatTheStepForbids),
        ("Every move step says what to click and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Cost and Effect is built, follows Traits and Actions and precedes Special Actions - Attacks", Curriculum),
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

    /// <summary>The Holdings that pay for Mysterious Deaths: Prosperous Village (5) and the Border Keep (2), leaving Bamboo Harvesters and the Geisha House (5 Gold) for At Any Cost.</summary>
    private static readonly string[] DeathsPayment = [TutorialCards.ProsperousVillage, TutorialCards.BorderKeep];

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId; var state = runner.Engine.State;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede").ToArray();
        Expect(offered.Length >= 1, $"{step}: the step offers its move, got {Offered(runner.Actions())}");
        var option = step switch
        {
            "ce-any-cost-play" => offered.Single(a => a.Command!.ClanDiscount && !a.Command.ClanAllegiance),
            _ => offered.Length == 1 ? offered[0] : throw new Exception($"{step}: the step offers exactly its one move, got {Offered(offered)}")
        };
        var command = option.Command!;
        string Mine(string definition) => InPlayCard(runner, 0, definition)!.Id;
        if (step == "ce-assign")
            command = command with { Assignments = new[] { TutorialCards.ShosuroKoshiba, TutorialCards.BayushiKasumi }.ToDictionary(Mine, _ => 0) };
        if (step == "ce-deaths-play")
            command = command with { SourceIds = [.. DeathsPayment.Select(Mine)] };
        if (step == "ce-revenge")
            command = command with { TargetId = InPlayCard(runner, 1, TutorialCards.UnparalleledSword)!.Id, PerformerId = command.PerformerId is null ? null : Mine(TutorialCards.BayushiKasumi) };
        if (step == "ce-towers")
            command = command with { TargetId = InPlayCard(runner, 1, TutorialCards.MirumotoGobashi)!.Id };
        if (option.TargetIds is { Length: > 0 } targets && command.TargetId is null) command = command with { TargetId = targets[0] };
        if (step == "ce-any-cost-play" && command.SourceIds is not { Length: > 0 })
        {
            var quote = option.TargetGold?.GetValueOrDefault(command.TargetId!) ?? option.Gold;
            command = command with { SourceIds = quote?.Payments.FirstOrDefault()?.SourceIds ?? [] };
        }
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
        var you = state.Players[0];
        int HandCount() => you.Hand.Count;
        switch (step)
        {
            case "ce-deaths-effects":
                Expect(CardCatalog.Get(TutorialCards.MysteriousDeaths).GoldCost == 6, "Mysterious Deaths costs 6");
                Expect(CardCatalog.Get(TutorialCards.TogashiMeiyu).GoldCost == 5, "Meiyu costs 5");
                Expect(CardCatalog.Get(TutorialCards.MirumotoGobashi).GoldCost + CardCatalog.Get(TutorialCards.UnparalleledSword).GoldCost + CardCatalog.Get(TutorialCards.Khalimpehjiak).GoldCost == 15, "Gobashi's unit totals 15");
                Expect(CardCatalog.Get(TutorialCards.ShosuroKoshiba).GoldCost + CardCatalog.Get(TutorialCards.NightingaleBlade).GoldCost > 0 && CardCatalog.Get(TutorialCards.BayushiKasumi).GoldCost > 0, "your other units cost more than the Ninja");
                Expect(InPlayCard(runner, 0, TutorialCards.Ninja) is { } ninja && CardCatalog.Get(ninja.DefinitionId).GoldCost == 0, "your Ninja is in play and costs 0");
                break;
            case "ce-deaths-done":
                Expect(InPlayCard(runner, 0, TutorialCards.Ninja) is null && InPlayCard(runner, 1, TutorialCards.TogashiMeiyu) is null, "the Ninja and Meiyu were destroyed");
                Expect(InPlayCard(runner, 0, TutorialCards.ShosuroKoshiba) is not null && InPlayCard(runner, 0, TutorialCards.BayushiKasumi) is not null && InPlayCard(runner, 1, TutorialCards.MirumotoGobashi) is not null, "no one else was");
                Expect(Cards(runner, 0, TutorialCards.MysteriousDeaths).Single().Zone == Zone.RemovedFromGame, "Mysterious Deaths was removed from the game");
                Expect(you.Honor == -2, $"2 - 4 = -2 Honor, engine {you.Honor}");
                Expect(!InPlayCard(runner, 0, TutorialCards.MerchantsDistrict)!.Bowed, "the District is unbowed");
                break;
            case "ce-district":
                Expect(HandCount() == 1, $"you hold one card, engine {HandCount()}");
                break;
            case "ce-any-cost":
                Expect(InPlayCard(runner, 0, TutorialCards.MerchantsDistrict)!.Bowed, "the District bowed as its cost");
                Expect(you.Hand.Select(id => state.Cards[id].DefinitionId).Contains(TutorialCards.AtAnyCost), "you drew At Any Cost");
                Expect(CardCatalog.Get(TutorialCards.AtAnyCost).GoldCost == 0 && CardCatalog.Get(TutorialCards.Revenge).GoldCost == 0, "At Any Cost and Revenge cost 0");
                break;
            case "ce-declare":
                Expect(state is { Phase: Phase.AttackDeclaration, ActivePlayerId: 0 }, $"your Attack phase, engine {state.Phase}");
                break;
            case "ce-jiak":
                Expect(InPlayCard(runner, 1, TutorialCards.MirumotoGobashi)!.Zone == Zone.Battlefield, "Gobashi defends");
                Expect(InPlayCard(runner, 1, TutorialCards.Khalimpehjiak)!.Bowed, "Khalimpeh-jiak bowed as its cost");
                Expect(Force(runner, 0, TutorialCards.ShosuroKoshiba) == 4, $"Koshiba: 3 + 3 - 2 = 4, engine {Force(runner, 0, TutorialCards.ShosuroKoshiba)}");
                break;
            case "ce-sword":
                Expect(InPlayCard(runner, 0, TutorialCards.ShosuroKoshiba) is null && InPlayCard(runner, 0, TutorialCards.NightingaleBlade) is null, "Koshiba and his Blade are gone");
                Expect(state.PriorityPlayerId == 0, "your Battle action");
                break;
            case "ce-towers":
                Expect(InPlayCard(runner, 1, TutorialCards.UnparalleledSword) is null, "Revenge destroyed the Sword");
                Expect(!InPlayCard(runner, 0, TutorialCards.BayushiKasumi)!.Bowed, "performing Revenge didn't bow Kasumi");
                break;
            case "ce-any-cost-play":
                Expect(InPlayCard(runner, 0, TutorialCards.BayushiKasumi)!.Bowed && InPlayCard(runner, 1, TutorialCards.MirumotoGobashi)!.Bowed, "Kasumi bowed to bow Gobashi");
                Expect(CardCatalog.Get(TutorialCards.ShosuroKoshiba).GoldCost == 5, "Koshiba costs 5");
                break;
            case "ce-resolve":
                Expect(InPlayCard(runner, 0, TutorialCards.ShosuroKoshiba) is { Zone: Zone.Battlefield } koshiba && !state.Cards.Values.Any(c => c.AttachedTo == koshiba.Id), "Koshiba is back at the battle, without his Blade");
                break;
            case "ce-done":
                Expect(InPlayCard(runner, 1, TutorialCards.MirumotoGobashi) is null && InPlayCard(runner, 1, TutorialCards.Khalimpehjiak) is null, "the losing army was destroyed");
                Expect(InPlayCard(runner, 0, TutorialCards.ShosuroKoshiba) is not null && InPlayCard(runner, 0, TutorialCards.BayushiKasumi) is not null, "your army stands");
                Expect(you.Honor == 2, $"2 Honor for each of the 2 cards destroyed: -2 to 2, engine {you.Honor}");
                break;
        }
    }

    private static void OpensAsScorpion()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, "it is your Action phase");
        Expect(state.Players[0].Honor == 2 && state.Players[1].Honor == 8, "2 Honor against 8");
        Expect(state.Players[0].Clan == "Scorpion" && state.Players[1].Clan == "Dragon", "Scorpion against Dragon");
        foreach (var card in new[] { TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.GeishaHouse, TutorialCards.ProsperousVillage, TutorialCards.MerchantsDistrict, TutorialCards.ShosuroKoshiba, TutorialCards.BayushiKasumi, TutorialCards.Ninja })
            Expect(InPlayCard(runner, 0, card) is not null, "you have " + CardCatalog.Get(card).Name);
        Expect(InPlayCard(runner, 0, TutorialCards.OverlookedPalace) is null, "the Overlooked Palace was destroyed to make the Ninja");
        foreach (var card in new[] { TutorialCards.GoldMine, TutorialCards.SmallFarm, TutorialCards.LargeFarm, TutorialCards.BorderKeep, TutorialCards.MirumotoGobashi, TutorialCards.TogashiMeiyu })
            Expect(InPlayCard(runner, 1, card) is not null, "the Dragon have " + CardCatalog.Get(card).Name);
        Expect(InPlayCard(runner, 0, TutorialCards.NightingaleBlade)!.AttachedTo == InPlayCard(runner, 0, TutorialCards.ShosuroKoshiba)!.Id, "Koshiba carries the Nightingale Blade");
        var gobashi = InPlayCard(runner, 1, TutorialCards.MirumotoGobashi)!.Id;
        Expect(InPlayCard(runner, 1, TutorialCards.UnparalleledSword)!.AttachedTo == gobashi && InPlayCard(runner, 1, TutorialCards.Khalimpehjiak)!.AttachedTo == gobashi, "Gobashi carries the Sword and Khalimpeh-jiak");
        Expect(state.Players[0].Hand.Select(id => state.Cards[id].DefinitionId).OrderBy(d => d).SequenceEqual(new[] { TutorialCards.MysteriousDeaths, TutorialCards.Revenge }.OrderBy(d => d)), "your hand: Mysterious Deaths and Revenge");
        Expect(state.Cards[state.Players[0].FateDeck[0]].DefinitionId == TutorialCards.AtAnyCost, "At Any Cost is on top of your Fate deck");
        Expect(state.Players[1].Hand.Select(id => state.Cards[id].DefinitionId).SequenceEqual([TutorialCards.SuperiorReach]), "the Dragon's hand: Superior Reach");
        Expect(state.Players.All(p => p.Provinces.All(pr => pr.CardId is { } id && !state.Cards[id].FaceUp)), "every Province is face down");
        // The card facts the notes quote.
        Expect(CardCatalog.Get(TutorialCards.ShosuroKoshiba).Force == 3 && CardCatalog.Get(TutorialCards.NightingaleBlade).Force == 3, "Koshiba 3F, the Blade +3F");
        Expect(CardCatalog.Get(TutorialCards.BayushiKasumi).Keywords!.Contains("Elite") && CardCatalog.Get(TutorialCards.BayushiKasumi).Keywords!.Contains("Samurai") && CardCatalog.Get(TutorialCards.BayushiKasumi).Keywords!.Contains("Scorpion Clan"), "Kasumi is an Elite Scorpion Clan Samurai");
        Expect(CardCatalog.Get(TutorialCards.ShosuroKoshiba).Keywords!.Contains("Samurai"), "Koshiba is a Samurai");
        Expect(CardCatalog.Get(TutorialCards.Khalimpehjiak).Type == CardType.Item && CardCatalog.Get(TutorialCards.UnparalleledSword).Type == CardType.Item, "Gobashi has Items, no Followers");
    }

    private static void RefusesWhatTheStepForbids()
    {
        var runner = TutorialRunner.Start(Lesson);
        while (runner.Coach.StepId != "ce-deaths-play") runner.Continue();
        var option = runner.Actions().Single(a => a.Enabled && a.Command?.AbilityId == TutorialLessonCostEffect.Deaths);
        string Mine(string definition) => InPlayCard(runner, 0, definition)!.Id;
        Expect(!runner.Execute(option.Command! with { SourceIds = [Mine(TutorialCards.ProsperousVillage), Mine(TutorialCards.MerchantsDistrict)] }).Accepted, "the District may not pay for Mysterious Deaths here");
        Expect(runner.Coach.StepId == "ce-deaths-play", "a refused move leaves the step where it was");
        // The District's condition is checked by the engine, not by the lesson: with five cards in hand it is refused.
        var engine = runner.Engine;
        engine.TutorialDraw(0, 4);
        Expect(engine.State.Players[0].Hand.Count == 6, "six cards in hand");
        Expect(!engine.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == TutorialLessonCostEffect.District), "the District's condition bars it with more than four cards in hand");
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
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "Cost and Effect is built");
        Expect(TutorialCurriculum.After("traits-actions")?.Id == Lesson && TutorialCurriculum.After(Lesson)?.Id == "special-attacks", "Cost and Effect sits between Traits and Actions and Special Actions - Attacks");
    }
}
