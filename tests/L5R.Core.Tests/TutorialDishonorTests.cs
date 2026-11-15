using L5R.Core;

// The Dishonor lesson: Crab Control against Dragon, played through the runner the way a player would. A rules or card change that breaks something a step shows (Jekku's Honor loss, an honorable Seppuku, a "Gain 3 Honor"
// that rehonors instead, a dishonorable death costing base Personal Honor, the -20 loss at the Dragon's End Phase) fails here instead of in front of a new player.
internal static class TutorialDishonorTests
{
    private const string Lesson = "special-dishonor";

    public static void Run(TestRunner runner) => runner.Run("tutorial-dishonor", [
        ("The lesson plays through using only what each step offers, and the engine reaches the Honor and status every step names", FinishLesson),
        ("The lesson opens in the Crab's Action phase with the table the notes name: Crab on 2 Honor with the Imperial Favor, Dragon on -17", OpensAsCrab),
        ("After the lesson the Dragon lose by Dishonor at the beginning of their next End Phase, as the last note says", DragonLosesAtEndPhase),
        ("Every move step says what to click and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Dishonor is built and follows Duels", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance One(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).Single();
    private static bool Has(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/tgt={a.Command?.TargetId}/{(a.Enabled ? "on" : "off")}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede").ToArray();
        Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {Offered(runner.Actions())}");
        var option = offered[0]; var command = option.Command!;
        if (step == "jekku")
        {
            var farm = Cards(runner, 0, TutorialCards.SmallFarm).Single(c => c.Zone == Zone.Home).Id;
            return command with { SourceIds = [farm] };
        }
        if (option.Gold?.Payments.FirstOrDefault() is { } payment && command.SourceIds is not { Length: > 0 }) command = command with { SourceIds = payment.SourceIds };
        return command;
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
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended: " + runner.Coach.StepId);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
    }

    /// <summary>What the engine shows at the start of each step, checked against what the words of that step say.</summary>
    private static void CheckTable(TutorialRunner runner, string step)
    {
        var state = runner.Engine.State;
        var gobashi = One(runner, 1, TutorialCards.MirumotoGobashi); var meiyu = One(runner, 1, TutorialCards.TogashiMeiyu); var iemasa = One(runner, 0, TutorialCards.KaiuIemasa);
        int You() => state.Players[0].Honor; int Dragon() => state.Players[1].Honor;
        switch (step)
        {
            case "honorable":
                Expect(!gobashi.Dishonorable && !meiyu.Dishonorable, "both Dragon Personalities start honorable");
                break;
            case "dishonored":
                Expect(gobashi.Dishonorable && gobashi.Zone == Zone.Home, "Gobashi is dishonorable");
                Expect(Dragon() == -18, $"Jekku's text cost the Dragon 1 Honor (-17 to -18), the engine shows {Dragon()}");
                Expect(Cards(runner, 0, TutorialCards.SmallFarm).Single(c => c.Zone == Zone.Home).Bowed, "the Small Farm paid the 1 Gold");
                Expect(state.PriorityPlayerId == 1, "the Dragon hold the next opportunity to act (they use it for the Seppuku)");
                break;
            case "seppuku-honorable":
                Expect(gobashi.Zone == Zone.Dead && !gobashi.Dishonorable, "Gobashi is dead and honorable");
                Expect(Dragon() == -18, $"an honorable death costs nothing: the Dragon are still on -18, the engine shows {Dragon()}");
                break;
            case "iemasa":
                Expect(iemasa.Dishonorable, "Kaiu Iemasa starts dishonorable");
                Expect(state.PriorityPlayerId == 0, "it is your action after the Seppuku");
                break;
            case "rehonored":
                Expect(!iemasa.Dishonorable, "Iemasa was rehonored");
                Expect(You() == 2, $"the rehonoring replaced \"Gain 3 Honor\": you are still on 2, the engine shows {You()}");
                break;
            case "dishonorable-dead":
                Expect(meiyu.Dishonorable && meiyu.Zone == Zone.Home, "Meiyu is dishonorable");
                Expect(Dragon() == -18, "the Slanderer's dishonoring cost no Honor");
                Expect(Has(runner, 0, TutorialCards.Slanderer, Zone.Home) && One(runner, 0, TutorialCards.Slanderer).Bowed, "the Slanderer bowed for its ability");
                break;
            case "dead-pile":
                Expect(meiyu.Zone == Zone.Dead && meiyu.Dishonorable, "Meiyu is dishonorably dead");
                Expect(gobashi.Zone == Zone.Dead && !gobashi.Dishonorable, "Gobashi is honorably dead");
                Expect(Dragon() == -20, $"Meiyu's base Personal Honor of 2 took the Dragon to -20, the engine shows {Dragon()}");
                Expect(You() == 0, $"The Heir's Wrath cost you 2 Honor (2 to 0), the engine shows {You()}");
                Expect(state.FavorPlayerId is null, "the Imperial Favor was discarded");
                Expect(state.Players[1].DishonorLossPending && runner.Engine.WinnerId is null, "the Dragon's loss is pending, not yet applied");
                break;
        }
    }

    private static TutorialRunner PlayToEnd()
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && guard < 60; guard++)
            if (runner.Coach.Button is not null) runner.Continue(); else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{runner.Coach.StepId}: the move works");
        return runner;
    }

    private static void OpensAsCrab()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, "it is the Crab's Action phase");
        Expect(state.Players[0].Honor == 2 && state.Players[1].Honor == -17, $"Crab on 2 and Dragon on -17, the engine shows {state.Players[0].Honor} and {state.Players[1].Honor}");
        Expect(state.FavorPlayerId == 0, "you hold the Imperial Favor");
        foreach (var card in new[] { TutorialCards.Slanderer, TutorialCards.IronMine, TutorialCards.ProsperousVillage, TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.SmallFarm, TutorialCards.YasukiJekku, TutorialCards.YasukiTono, TutorialCards.KaiuIemasa })
            Expect(Has(runner, 0, card, Zone.Home), "the Crab have " + CardCatalog.Get(card).Name);
        foreach (var card in new[] { TutorialCards.BorderKeep, TutorialCards.GoldMine, TutorialCards.LargeFarm, TutorialCards.SmallFarm, TutorialCards.ProsperousVillage, TutorialCards.TogashiMeiyu, TutorialCards.MirumotoGobashi })
            Expect(Has(runner, 1, card, Zone.Home), "the Dragon have " + CardCatalog.Get(card).Name);
        Expect(new[] { TutorialCards.RememberYourAncestors, TutorialCards.DismissingTheCur, TutorialCards.TheHeirsWrath }.All(d => Has(runner, 0, d, Zone.Hand)) && state.Players[0].Hand.Count == 3, "the Crab hold exactly the three Fate cards named");
        Expect(state.Players.All(p => p.Provinces.All(pr => pr.CardId is { } id && !state.Cards[id].FaceUp)), "every Province is face down");
        // The card facts the notes quote.
        Expect(CardCatalog.Get(TutorialCards.MirumotoGobashi).PersonalHonor == 2 && CardCatalog.Get(TutorialCards.TogashiMeiyu).PersonalHonor == 2, "Gobashi and Meiyu both have 2 Personal Honor");
        Expect(CardCatalog.Get(TutorialCards.TogashiMeiyu).GoldCost == 5, "Meiyu's Gold Cost is 5");
        Expect(CardCatalog.Get(TutorialCards.MirumotoGobashi).Keywords.Contains("Samurai"), "Gobashi is a Samurai");
        var meiyu = CardCatalog.Get(TutorialCards.TogashiMeiyu).Keywords;
        Expect(meiyu.Contains("Monk") && !meiyu.Contains("Samurai") && !meiyu.Contains("Courtier") && !meiyu.Contains("Shugenja"), "Meiyu is a Monk and none of the three Seppuku keywords");
        Expect(CardCatalog.Get(TutorialCards.YasukiJekku).Keywords.Contains("Courtier") && CardCatalog.Get(TutorialCards.YasukiTono).Keywords.Contains("Courtier"), "Jekku and Tono are Courtiers");
        // Meiyu is not offered to Order Seppuku even once dishonorable (checked at the end of the lesson's Slanderer step).
        var atSlanderer = runner;
        for (var guard = 0; atSlanderer.Coach.StepId != "dishonorable-dead" && guard < 60; guard++)
            if (atSlanderer.Coach.Button is not null) atSlanderer.Continue(); else Expect(atSlanderer.Execute(CommandFor(atSlanderer)).Accepted, $"{atSlanderer.Coach.StepId}: the move works");
        var meiyuId = One(atSlanderer, 1, TutorialCards.TogashiMeiyu).Id;
        Expect(!atSlanderer.Engine.QueryActions(1).Any(a => a.Enabled && a.Command is { Type: "orderSeppuku" } c && c.CardId == meiyuId), "the Dragon cannot order the dishonorable Monk to commit Seppuku");
    }

    /// <summary>The last note says the Dragon lose at the beginning of their next End Phase. Play on past the lesson, every player passing, until someone wins.</summary>
    private static void DragonLosesAtEndPhase()
    {
        var runner = PlayToEnd();
        var engine = runner.Engine;
        Phase? phaseAtWin = null;
        for (var guard = 0; engine.WinnerId is null && guard < 400; guard++)
        {
            var seat = engine.PriorityPlayerId; var before = engine.State;
            var options = engine.QueryActions(seat).Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede" && a.Command.Type != "orderSeppuku").ToArray();
            var pick = options.FirstOrDefault(a => a.Type is "pass" or "declineReaction" or "declineAttack" or "endDynasty" or "declineAdditionalAction")
                ?? options.FirstOrDefault(a => a.Command!.Type is "discardHand" or "discard") ?? options.FirstOrDefault();
            Expect(pick is not null, $"someone can act at {before.Phase}: " + Offered(engine.QueryActions(seat)));
            Expect(engine.Execute(pick!.Command!).Accepted, $"{pick.Type} at {before.Phase} is accepted");
            if (engine.WinnerId is not null) phaseAtWin = engine.State.Phase;
            Expect(engine.WinnerId is not null || engine.State.ActivePlayerId == 1 || engine.State.Phase != Phase.End || before.ActivePlayerId == 0, "no loss before the Dragon's End Phase");
        }
        Expect(engine.WinnerId == 0, $"you win by Dishonor; winner {engine.WinnerId}");
        Expect(engine.State.ActivePlayerId == 1, $"the loss came on the Dragon's turn (at {phaseAtWin})");
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
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{coach.StepId}: the move works");
        }
        Expect(lastButton == "Tutorial Menu", "the last note returns to the menu");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && guard < 60; guard++)
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
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "Dishonor is built");
        Expect(TutorialCurriculum.After("special-duels")?.Id == Lesson, "Dishonor follows Duels");
    }
}
