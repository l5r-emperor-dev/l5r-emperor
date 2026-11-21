using L5R.Core;

// Issue #1150, the Keywords 2 lesson: Phoenix against Dragon (Unique, Singular, Loyal, Sensei), Scorpion against Dragon (Destined, Brash, Expendable, Reserve, Terrain, Overconfident), Scorpion against Crane (Kharmic,
// Experienced and Inexperienced), played through the runner the way a player would. A rules or card change that breaks a keyword the lesson shows fails here instead of in front of a new player.
internal static class TutorialKeywords2Tests
{
    private const string Lesson = "keywords-2";

    public static void Run(TestRunner runner) => runner.Run("tutorial-keywords-2", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens at your Dynasty phase as Phoenix against a Dragon mirror with the cards the notes name", OpensAsPhoenix),
        ("Unique stops a second copy entering play, Loyal stops another Clan's player recruiting, and a new Sensei discards the old one", UniqueLoyalAndSensei),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move, across the table changes too", BackUndoesTheLastStep),
        ("Keywords 2 is built and follows Keywords 1", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold or Zone.Attached or Zone.Region);
    private static bool Has(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/tgt={a.Command?.TargetId}/targets={string.Join(",", a.TargetIds ?? [])}/{(a.Enabled ? "on" : "off:" + a.Reason)}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing: " + Offered(runner.Actions().ToArray()));
        string Foe(string definition) => InPlay(runner, 1, definition).Id;
        ActionOption First(Func<ActionOption, bool> match) => offered.FirstOrDefault(match) ?? throw new Exception($"{step}: no option matches: {Offered(offered)}");
        switch (step)
        {
            case "destined":
                return First(a => a.Command is { Type: "purchase", ClanDiscount: true, ClanAllegiance: false }).Command!;
            case "brash":
            case "overconfident-1":
            case "overconfident-2":
                return First(a => a.Command?.Choice == "draw").Command!;
            case "block-supply-lines":
                {
                    var option = First(a => a.Command!.AbilityId == "block-supply");
                    return option.Command! with { TargetId = Foe(TutorialCards.TogashiNoboru) };
                }
            case "reserve":
                return First(a => a.Command is { Type: "recruitReserve", ClanDiscount: true, ClanAllegiance: false }).Command!;
            case "marsh-bows":
                {
                    var option = First(a => a.Command!.AbilityId == "thick-marsh-entry");
                    return option.Command! with { TargetId = Foe(TutorialCards.KitsukiMinori) };
                }
            case "kharmic":
                return First(a => a.Command!.AbilityId == "kharmic-discard").Command! with { TargetId = Cards(runner, 0, TutorialCards.TravelingMarket).First(c => c.Zone == Zone.Province).Id };
            case "overlay":
                return First(a => runner.Engine.State.Cards[a.CardId ?? a.Command!.CardId!].DefinitionId == TutorialCards.BayushiNitoshiExp).Command!;
            default:
                Expect(offered.Length == 1 || step is "peddler-1", $"{step}: the step offers exactly its one move, got {Offered(offered)}");
                return offered[0].Command!;
        }
    }

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
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
            case "unique":
                Expect(InPlay(runner, 0, TutorialCards.TemplesOfGiseiToshi).Zone == Zone.Home && InPlay(runner, 0, TutorialCards.ShindenShorai).Zone == Zone.Region && Has(runner, 0, TutorialCards.ImperialGift, Zone.DynastyDiscard)
                    && Has(runner, 0, TutorialCards.AsakoRikateExp, Zone.Province), "a Unique Holding in play, a Unique Region on a Province, a Unique Event in the discard pile and a Unique Personality in a Province");
                Expect(Has(runner, 1, TutorialCards.TemplesOfGiseiToshi, Zone.Home) && Has(runner, 1, TutorialCards.IsawaSanetomo, Zone.Province), "the Dragon player has the same cards");
                break;
            case "unique-hand":
                Expect(new[] { TutorialCards.Renewal, TutorialCards.BladesOfTheBlackDragon, TutorialCards.EliteSentry, TutorialCards.RingOfWater, TutorialCards.Hanabi }.All(d => Has(runner, 0, d, Zone.Hand)), "the five Unique cards are in hand");
                break;
            case "peddler-2":
                Expect(Has(runner, 0, TutorialCards.TravelingPeddler, Zone.Home), "a Traveling Peddler is in play");
                var second = runner.Engine.QueryActions(0).Where(a => a.Type == "purchase" && runner.Engine.State.Cards[a.CardId ?? a.Command!.CardId!].DefinitionId == TutorialCards.TravelingPeddler).ToArray();
                Expect(second.Length > 0 && second.All(a => !a.Enabled), "the second Singular Traveling Peddler cannot be recruited: " + Offered(second));
                break;
            case "loyal":
                Expect(runner.Engine.QueryActions(0).Any(a => a.Enabled && a.Type == "purchase" && runner.Engine.State.Cards[a.CardId ?? a.Command!.CardId!].DefinitionId == TutorialCards.IsawaSanetomo), "a Phoenix player may recruit Sanetomo");
                break;
            case "destined":
                Expect(state.Players[0].Hand.Count == 2 && state.Players[0].Clan == "Scorpion" && state.Players[1].Clan == "Dragon", "Scorpion against Dragon with Floating Reserve and Thick Marsh in hand");
                break;
            case "end-turn":
                Expect(Has(runner, 0, TutorialCards.YogoTakashi, Zone.Home) && state.Players[0].Hand.Count == 3 && Has(runner, 0, TutorialCards.BlockSupplyLines, Zone.Hand), "Takashi's Destined draw brought Block Supply Lines, and nothing asked first");
                break;
            case "brash":
                Expect(state.PendingChoice is { Type: "brashDraw" } && Cards(runner, 1, TutorialCards.TogashiNoboru).Single().Zone == Zone.Battlefield && Cards(runner, 1, TutorialCards.KitsukiMinori).Any(c => c.Zone == Zone.Battlefield), "both Dragon Personalities attack and the Brash draw is offered");
                break;
            case "defend":
                Expect(state.Players[0].Hand.Count == 4, $"the Brash draw added a card, the hand holds {state.Players[0].Hand.Count}");
                break;
            case "block-supply-lines":
                Expect(state.Phase == Phase.Battle && state.PriorityPlayerId == 0, "the battle opens with your first action");
                break;
            case "expendable":
                Expect(Has(runner, 0, TutorialCards.YogoTakashi, Zone.Dead) && Has(runner, 0, TutorialCards.CelestialSwordOfTheDragonExp, Zone.Hand), "A Stain Cleansed destroyed Takashi and his Expendable draw brought the Celestial Sword");
                Expect(InPlay(runner, 1, TutorialCards.TogashiNoboru).Zone == Zone.Home && InPlay(runner, 1, TutorialCards.TogashiNoboru).Bowed, "Block Supply Lines sent Togashi Noboru home, bowed");
                break;
            case "reserve":
                Expect(runner.Actions().Any(a => a.Enabled && a.Command?.Type == "recruitReserve"), "Wateru may be recruited from Reserve");
                break;
            case "interrupt":
                Expect(Has(runner, 0, TutorialCards.BayushiWateru, Zone.Battlefield) && state.PendingChoice is { Type: "entryReaction" }, "Wateru is in the battle and her Interrupt is offered");
                break;
            case "terrain":
                Expect(state.AdditionalActions.Any(g => g.PlayerId == 0) && state.PriorityPlayerId == 0, "the Interrupt granted an additional Battle action and it is yours");
                break;
            case "marsh-bows":
                Expect(InPlay(runner, 0, TutorialCards.ThickMarsh).Zone == Zone.Battlefield && state.AdditionalActions.All(g => g.PlayerId != 0), "Thick Marsh is in play at the battlefield and spent the additional action");
                break;
            case "pass-battle":
                Expect(InPlay(runner, 1, TutorialCards.KitsukiMinori).Bowed && state.PriorityPlayerId == 0 && state.Events.Last().Message.Contains("passed"), "Minori is bowed and the Dragon has passed");
                break;
            case "overconfident-1":
                Expect(Has(runner, 1, TutorialCards.KitsukiMinori, Zone.Dead) && state.PendingChoice is { Type: "overconfidentDraw", PlayerId: 0 }, "Minori was destroyed, and you may draw");
                break;
            case "overconfident-2":
                Expect(Cards(runner, 1, TutorialCards.KitsukiMinori).Count(c => c.Zone == Zone.Home) == 1 && state.PendingChoice is { Type: "overconfidentDraw", PlayerId: 0 }, "the second Minori entered play and you may draw");
                break;
            case "experience-intro":
                Expect(state.Players[0].Clan == "Scorpion" && state.Players[1].Clan == "Crane" && state.Phase == Phase.Action, "Scorpion against Crane in your Action phase");
                Expect(Experienced.Level(Definition(runner, TutorialCards.BayushiNitoshiInexperienced).Keywords) == -1 && Experienced.Level(Definition(runner, TutorialCards.BayushiNitoshi).Keywords) == 0
                    && Experienced.Level(Definition(runner, TutorialCards.BayushiNitoshiExp).Keywords) == 1 && Experienced.Level(Definition(runner, TutorialCards.DojiHakusekiExp2).Keywords) == 2, "Inexperienced -1, none 0, Experienced 1, Experienced 2");
                break;
            case "to-dynasty":
                Expect(Has(runner, 0, TutorialCards.TravelingMarket, Zone.DynastyDiscard) && state.Players[0].Provinces[0].CardId is { } refill && state.Cards[refill].DefinitionId == TutorialCards.BayushiNitoshiExp && state.Cards[refill].FaceUp,
                    "Kharmic discarded Traveling Market and refilled the Province face-up with Bayushi Nitoshi (Experienced)");
                break;
            case "end-dynasty":
                Expect(Has(runner, 0, TutorialCards.BayushiNitoshiExp, Zone.Home) && Has(runner, 0, TutorialCards.BayushiNitoshiInexperienced, Zone.RemovedFromGame), "the Overlay put the Experienced Nitoshi in play and removed the Inexperienced one from the game");
                break;
            case "hakuseki":
                Expect(state.Players[1].Provinces[0].CardId is { } hakuseki && state.Cards[hakuseki].DefinitionId == TutorialCards.DojiHakusekiExp2 && state.Cards[hakuseki].FaceUp, "Doji Hakuseki is face-up in the Crane's first Province");
                break;
        }
    }

    private static CardDefinition Definition(TutorialRunner runner, string id) => CardCatalog.Get(id);

    private static void OpensAsPhoenix()
    {
        var runner = TutorialRunner.Start(Lesson); var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Dynasty, ActivePlayerId: 0, PriorityPlayerId: 0 }, $"it opens in your Dynasty phase: {state.Phase} {state.ActivePlayerId}/{state.PriorityPlayerId}");
        Expect(state.Players[0].Clan == "Phoenix" && state.Players[1].Clan == "Dragon", "Phoenix against Dragon");
        Expect(new[] { TutorialCards.IsawaSanetomo, TutorialCards.TravelingPeddler, TutorialCards.AsakoRikateExp }.All(d => Has(runner, 0, d, Zone.Province)) && Cards(runner, 0, TutorialCards.TravelingPeddler).Count(c => c.Zone == Zone.Province) == 2, "Sanetomo, two Peddlers and Rikate in the Provinces");
        Expect(state.Players[0].Provinces.All(p => p.CardId is { } id && state.Cards[id].FaceUp), "the Provinces are face-up");
        Expect(Cards(runner, 0, TutorialCards.SmallFarm).Count(c => c.Zone == Zone.Home) == 2 && Has(runner, 0, TutorialCards.LargeFarm, Zone.Home) && Has(runner, 0, TutorialCards.ProsperousVillage, Zone.Home)
            && Has(runner, 0, TutorialCards.SilverMine, Zone.Home) && Has(runner, 0, TutorialCards.SilkWorks, Zone.Home), "seven Holdings in play");
        Expect(state.Players[0].Provinces[2].RegionId is not null, "Shinden Shorai is attached to the third Province");
    }

    /// <summary>The rules the first table shows, run on the same table: a second Unique copy, a Dragon player and a Sensei.</summary>
    private static void UniqueLoyalAndSensei()
    {
        var runner = TutorialRunner.Start(Lesson);
        Expect(runner.Engine.QueryActions(0).Any(a => a.Enabled && a.Type == "purchase" && (a.CardId ?? a.Command?.CardId) is { } id && runner.Engine.State.Cards[id].DefinitionId == TutorialCards.AsakoRikateExp), "Rikate may be recruited while you control no copy");
        runner.Engine.TutorialPutInPlay(0, TutorialCards.AsakoRikateExp);
        var rikate = runner.Engine.QueryActions(0).Where(a => a.Type == "purchase" && (a.CardId ?? a.Command?.CardId) is { } id && runner.Engine.State.Cards[id].DefinitionId == TutorialCards.AsakoRikateExp).ToArray();
        Expect(rikate.Length > 0 && rikate.All(a => !a.Enabled), "Rikate will not enter play for a player who controls a Unique card with that title: " + Offered(rikate));
        Expect(Cards(runner, 1, TutorialCards.AsakoRikateExp).Any(c => c.Zone == Zone.Province), "the Dragon player's copy is untouched");

        runner = TutorialRunner.Start(Lesson);
        runner.Engine.TutorialState.Players[0].Clan = "Dragon";
        var sanetomo = runner.Engine.QueryActions(0).Where(a => a.Type == "purchase" && (a.CardId ?? a.Command?.CardId) is { } id && runner.Engine.State.Cards[id].DefinitionId == TutorialCards.IsawaSanetomo).ToArray();
        Expect(sanetomo.Length > 0 && sanetomo.All(a => !a.Enabled), "a Dragon player may not recruit the Loyal Phoenix Sanetomo: " + Offered(sanetomo));

        runner = TutorialRunner.Start(Lesson);
        runner.Engine.TutorialPutInPlay(0, TutorialCards.IsawaSanetomo);
        var old = Cards(runner, 0, TutorialCards.IsawaSanetomo).First(c => c.Zone == Zone.Home).Id;
        var buy = runner.Engine.QueryActions(0).First(a => a.Enabled && a.Type == "purchase" && (a.CardId ?? a.Command?.CardId) is { } id && runner.Engine.State.Cards[id].DefinitionId == TutorialCards.IsawaSanetomo);
        Expect(runner.Engine.Execute(buy.Command!).Accepted, "Sanetomo is recruited");
        for (var guard = 0; runner.Engine.State.PendingChoice is not null && guard < 5; guard++) Expect(runner.Engine.Execute(runner.Engine.QueryActions(0).First(x => x.Enabled && x.Type == "declineReaction").Command!).Accepted, "the Proclaim window is passed");
        Expect(runner.Engine.State.Cards[old].Zone != Zone.Home && Cards(runner, 0, TutorialCards.IsawaSanetomo).Count(c => c.Zone == Zone.Home) == 1, "entering play discarded the other Sensei Personality, the same card included: " + string.Join(", ", Cards(runner, 0, TutorialCards.IsawaSanetomo).Select(c => c.Id + ":" + c.Zone)) + " old=" + old + " pending=" + runner.Engine.State.PendingChoice?.Type + " events=" + string.Join(" | ", runner.Engine.State.Events.TakeLast(5).Select(e => e.Message)));
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson); var last = "";
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; last = coach.StepId;
            Expect(coach.Body.Length > 20, $"{coach.StepId}: it says something");
            Expect(coach.Button is not null || !string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: it has a button or tells the player what to click");
            if (coach.Button is not null) { runner.Continue(); continue; }
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{coach.StepId}: the move works");
        }
        Expect(last == "duelist", "the last step is the Duelist note");
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
        var second = TutorialCurriculum.All.Single(e => e.Id == "keywords-2");
        Expect(second.Title == "Keywords 2" && second.Built, "Keywords 2 is built");
        Expect(TutorialCurriculum.After("keywords")?.Id == "keywords-2" && TutorialCurriculum.After("keywords-2")?.Id == "traits-actions", "Keywords 2 follows Keywords 1");
    }
}
