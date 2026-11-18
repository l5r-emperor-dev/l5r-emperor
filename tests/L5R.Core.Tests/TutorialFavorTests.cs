using L5R.Core;

// The Imperial Favor lesson: Favors (Kyuden Otomo - exp) against Dragon Destruction, played through the runner the way a player would. A rules or card change that breaks something a step shows (Lobby's Honor test and
// once-per-turn use, R-097's bar while holding the Favor, the Favor's two abilities, Dazzling Attire, Sosuke's bow, Kyuden Otomo's Favor tokens, Dainagon's Open Lobby) fails here instead of in front of a new player.
internal static class TutorialFavorTests
{
    private const string Lesson = "imperial-favor";

    public static void Run(TestRunner runner) => runner.Run("tutorial-favor", [
        ("The lesson plays through using only what each step offers, and the engine reaches the Favor, tokens and bows every step names", FinishLesson),
        ("The lesson opens in your Action phase with the table the script names: 13 Honor against 8, two Favor tokens, nobody holding the Favor", OpensAsCrane),
        ("The rules the notes state hold in the engine: no second Lobby this turn (Dainagon included, R-098), no Lobby while holding the Favor (R-097)", RulesTheNotesState),
        ("Every move step says what to click and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move", BackUndoesTheLastStep),
        ("Imperial Favor is built and follows Reactions", Curriculum),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static readonly Zone[] InPlay = [Zone.Home, Zone.Battlefield, Zone.Attached, Zone.Stronghold];
    private static CardInstance One(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).Single(c => InPlay.Contains(c.Zone));
    private static bool Has(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);
    private static int Tokens(TutorialRunner runner) { var state = runner.Engine.State; return state.Cards[state.Players[0].StrongholdId].Tokens.Count(t => t.Type == "Favor"); }

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/tgt={a.Command?.TargetId}/cost={a.Command?.CostOption}/{(a.Enabled ? "on" : "off")}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null && a.Command.Type != "concede").ToArray();
        Expect(offered.Length >= 1, $"{step}: the step offers its move, got {Offered(runner.Actions())}");
        var option = step == "sosuke-hiroto" || step == "favor-home" ? offered.FirstOrDefault(a => a.Command!.CostOption == "favor-token") ?? offered[0]
            : step is "favor-cycle" or "sosuke-sword" ? offered.FirstOrDefault(a => a.Command!.CostOption is null) ?? offered[0] : offered[0];
        Expect(offered.Select(a => a.Command!.AbilityId ?? a.Command.Type).Distinct().Count() == 1, $"{step}: the step offers exactly one kind of move, got {Offered(offered)}");
        var command = option.Command!;
        if (option.TargetIds is { Length: > 0 } targets && command.TargetId is null) command = command with { TargetId = targets[0] };
        if (step is "sosuke-hiroto" or "favor-home") command = command with { CostOption = "favor-token" };
        if (step == "favor-defend")
        {
            var sosuke = Cards(runner, 0, TutorialCards.DaidojiSosuke).First(c => c.Zone == Zone.Home && !c.Bowed).Id;
            command = command with { Assignments = new Dictionary<string, int> { [sosuke] = 0 } };
        }
        if (option.Gold?.Payments.FirstOrDefault() is { } payment && command.SourceIds is not { Length: > 0 }) command = command with { SourceIds = payment.SourceIds };
        return command;
    }

    /// <summary>A step's move may open a choice that is still yours (which card in Sosuke's target's unit to bow): the runner offers it under the same step.</summary>
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
        var nara = One(runner, 0, TutorialCards.KakitaNara); var dainagon = One(runner, 0, TutorialCards.DojiDainagon); var attire = One(runner, 0, TutorialCards.DazzlingAttire);
        var sword = One(runner, 1, TutorialCards.UnparalleledSword); var hiroto = One(runner, 1, TutorialCards.TogashiHiroto); var gobashi = One(runner, 1, TutorialCards.MirumotoGobashi);
        switch (step)
        {
            case "lobby-nara":
                Expect(state.Players[0].Honor == 13 && state.Players[1].Honor == 8, "13 Honor against 8");
                Expect(CardCatalog.Get(TutorialCards.KakitaNara).PersonalHonor == 3, "Kakita Nara has 3 Personal Honor");
                break;
            case "favor-abilities":
                Expect(state.FavorPlayerId == 0 && nara.Bowed, "Nara bowed and you hold the Favor");
                Expect(Tokens(runner) == 2, "Kyuden Otomo's Reaction was declined: still two tokens");
                break;
            case "dazzling-attire":
                Expect(state.FavorPlayerId is null && Has(runner, 0, TutorialCards.TheEmpressAddress, Zone.FateDiscard) && Has(runner, 0, TutorialCards.MokshaPatam, Zone.Hand), "the Favor and The Empress' Address were discarded for Moksha Patam");
                Expect(Tokens(runner) == 2, "the Favor itself paid, not a token");
                break;
            case "end-turn":
                Expect(state.FavorPlayerId == 0 && attire.Bowed && !dainagon.Bowed, "Dazzling Attire bowed to take the Favor; Dainagon is still straightened");
                break;
            case "dragon-turn":
                Expect(state is { ActivePlayerId: 1, Phase: Phase.Action, PriorityPlayerId: 0 }, "the Dragon's Action phase, your opportunity to act");
                Expect(hiroto.Tokens.Count(t => t.Type == "Fire") == 7, $"Deadly Discipline gave Hiroto two more Fire tokens (5 to 7): {hiroto.Tokens.Count(t => t.Type == "Fire")}");
                Expect(Has(runner, 0, TutorialCards.HomeGuard, Zone.Hand), "you drew Home Guard in your End phase");
                Expect(state.FavorPlayerId == 0 && nara.Bowed && attire.Bowed, "you still hold the Favor; Nara and the Attire are still bowed");
                break;
            case "dainagon-lobby":
                Expect(sword.Bowed && !One(runner, 1, TutorialCards.MirumotoYumaru).Bowed, "Sosuke bowed the Unparalleled Sword, not Yumaru");
                Expect(state.FavorPlayerId is null && Tokens(runner) == 2, "the Imperial Favor paid for Sosuke");
                break;
            case "otomo-tokens":
                Expect(dainagon.Bowed && state.FavorPlayerId is null && Tokens(runner) == 3, $"Dainagon bowed to Lobby and Kyuden Otomo turned the Favor into a third token: {Tokens(runner)}");
                break;
            case "pass-dragon":
                Expect(hiroto.Bowed && Tokens(runner) == 2, "the second Sosuke bowed Hiroto with a token");
                break;
            case "favor-defend":
                Expect(gobashi.Zone == Zone.Battlefield && state.Cards.Values.Count(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality) == 1, "Gobashi attacks alone");
                break;
            case "favor-resolve":
                Expect(gobashi.Zone == Zone.Home && Tokens(runner) == 1, "a token paid for the Favor's Battle ability and Gobashi went home");
                break;
            case "favor-done":
                Expect(state.Players[0].Provinces[0] is { Destroyed: false }, "your Province 1 stands");
                break;
        }
    }

    private static TutorialRunner PlayTo(string stepId)
    {
        var runner = TutorialRunner.Start(Lesson);
        for (var guard = 0; !runner.Finished && runner.Coach.StepId != stepId && guard < 60; guard++)
            if (runner.Coach.Button is not null) runner.Continue(); else Play(runner);
        return runner;
    }

    private static void OpensAsCrane()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { Phase: Phase.Action, ActivePlayerId: 0, PriorityPlayerId: 0 }, "it is your Action phase");
        Expect(state.FavorPlayerId is null && Tokens(runner) == 2, "the Favor starts held by no one; Kyuden Otomo has two Favor tokens");
        foreach (var card in new[] { TutorialCards.BorderKeep, TutorialCards.Marketplace, TutorialCards.LuxuriousSilk, TutorialCards.RemoteVillage, TutorialCards.DojiDainagon, TutorialCards.KakitaNara })
            Expect(Has(runner, 0, card, Zone.Home), "you have " + CardCatalog.Get(card).Name);
        Expect(Cards(runner, 0, TutorialCards.DaidojiSosuke).Count(c => c.Zone == Zone.Home) == 2 && Cards(runner, 0, TutorialCards.DaidojiSosuke).Count() == 2, "two Daidoji Sosuke, and no third one in the decks");
        Expect(One(runner, 0, TutorialCards.DazzlingAttire).AttachedTo == One(runner, 0, TutorialCards.DojiDainagon).Id, "Dainagon wears Dazzling Attire");
        foreach (var card in new[] { TutorialCards.GoldMine, TutorialCards.FarmersMarket, TutorialCards.IncenseMill, TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.MirumotoYumaru, TutorialCards.TogashiHiroto, TutorialCards.MirumotoGobashi })
            Expect(Has(runner, 1, card, Zone.Home), "the Dragon have " + CardCatalog.Get(card).Name);
        Expect(One(runner, 1, TutorialCards.TogashiHiroto).Tokens.Count(t => t.Type == "Fire") == 5, "Hiroto carries 5 Fire tokens");
        Expect(Cards(runner, 1, TutorialCards.BladeOfPerfection).Count(c => c.AttachedTo == One(runner, 1, TutorialCards.MirumotoGobashi).Id) == 2, "Gobashi (a Kensai) carries two Blades of Perfection");
        Expect(new[] { TutorialCards.TheHostsAdvantage, TutorialCards.RequestAuthorization, TutorialCards.DismissingTheCur, TutorialCards.TheEmpressAddress }.All(d => Has(runner, 0, d, Zone.Hand)) && state.Players[0].Hand.Count == 4, "your four Fate cards");
        Expect(new[] { TutorialCards.DeadlyDiscipline, TutorialCards.HundredFoldCut, TutorialCards.Revenge }.All(d => Has(runner, 1, d, Zone.Hand)) && state.Players[1].Hand.Count == 3, "the Dragon's three Fate cards");
        Expect(state.Players.All(p => p.Provinces.All(pr => pr.CardId is { } id && !state.Cards[id].FaceUp)), "every Province is face down");
        // The card facts the notes quote.
        Expect(CardCatalog.Get(TutorialCards.MirumotoGobashi).Keywords!.Contains("Kensai"), "Gobashi is a Kensai, so two Weapons are legal");
        Expect(CardCatalog.Get(TutorialCards.DojiDainagon).Text.Contains("Lobby as an Open action"), "Dainagon's text");
        Expect(CardCatalog.Get(TutorialCards.DazzlingAttire).Text.Contains("Take the Imperial Favor"), "Dazzling Attire's text");
    }

    private static void RulesTheNotesState()
    {
        // After Nara's Lobby, no second Lobby this turn: not the player's, not Dainagon's (R-098).
        static bool Open(ActionOption[] options, string ability) => options.Any(a => a.Enabled && a.Command?.AbilityId == ability);
        // Discard the Favor (as the next step does), then Lobby is still spent for the turn.
        var cycled = PlayTo("dazzling-attire");
        Expect(cycled.Engine.State.FavorPlayerId is null, "the Favor was discarded");
        var options = cycled.Engine.QueryActions(0);
        Expect(!Open(options, "lobby") && !Open(options, "dainagon-lobby"), "no second Lobby this turn, Dainagon's included (R-098): " + Offered(options.Where(a => a.Command?.AbilityId is "lobby" or "dainagon-lobby").ToArray()));
        // On the Dragon's turn, while you hold the Favor, Dainagon may not Lobby (R-097); once Sosuke spends it she may.
        var dragonTurn = PlayTo("dainagon-open");
        Expect(dragonTurn.Engine.State.FavorPlayerId == 0 && !Open(dragonTurn.Engine.QueryActions(0), "dainagon-lobby"), "holding the Favor bars Dainagon's Lobby (R-097)");
        var spent = PlayTo("dainagon-lobby");
        Expect(Open(spent.Engine.QueryActions(0), "dainagon-lobby"), "with only Favor tokens, Dainagon may Lobby");
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
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "Imperial Favor is built");
        Expect(TutorialCurriculum.After("reactions")?.Id == Lesson, "Imperial Favor follows Reactions");
    }
}
