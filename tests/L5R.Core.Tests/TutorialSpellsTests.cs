using L5R.Core;

// Issue #1076, the Spells and Shugenja lesson: Phoenix against Scorpion Lethargy is played through the runner the way a player would, so a rules or card change that breaks the lesson
// (a Spell that attaches to a non-Shugenja, Equip (Battle) losing its additional action, Library of Rebirth's Gold or Sansesuke's redirect stopping) fails here instead of in front of a new player.
internal static class TutorialSpellsTests
{
    private const string Lesson = "spells";

    public static void Run(TestRunner runner) => runner.Run("tutorial-spells", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in your Action phase: six Spells in hand, your cards in play, two Provinces face up and two face down, 10 Honor against 3", OpensInTheActionPhase),
        ("Only a Shugenja may take a Spell: Equip (Limited) never offers Shiba Sansesuke, and Equip (Battle) is not open outside a battle", OnlyShugenjaTakeSpells),
        ("The Earth's Hunger bows Isawa Tomohiro, who is not Earth, and Library of Rebirth's Reaction pays 4 of Strength of the Tsunami's 7", ElementsAndLibrary),
        ("The numbers the lesson quotes are the catalogue's and the engine's", QuotedNumbers),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move, at every step of the lesson", BackUndoesTheLastStep),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold or Zone.Attached);
    private static string Id(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).First(c => c.Zone == zone).Id;
    private static string Province(TutorialRunner runner, int owner, int index) => runner.Engine.State.Players[owner].Provinces[index].CardId!;

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/perf={a.Command?.PerformerId}/tgt={a.Command?.TargetId}/targets={string.Join(",", a.TargetIds ?? [])}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing");
        var state = runner.Engine.State;
        string Own(string definition) => InPlay(runner, 0, definition).Id;
        string Hand(string definition) => Id(runner, 0, definition, Zone.Hand);
        GameCommand Equip(string spell, string wielder, params string[] sources)
        {
            var option = offered.FirstOrDefault(a => (a.Command!.CardId == Hand(spell) || a.CardId == Hand(spell)) && (a.TargetIds?.Contains(Own(wielder)) ?? true))
                ?? throw new Exception($"{step}: no option equips {spell}: {Offered(offered)}");
            return option.Command! with { CardId = option.Command.CardId ?? Hand(spell), TargetId = Own(wielder), SourceIds = sources };
        }
        switch (step)
        {
            case "equip-flood": return Equip(TutorialCards.ScouringFlood, TutorialCards.IsawaTomohiro, Own(TutorialCards.BorderKeep));
            case "equip-swell": return Equip(TutorialCards.TheSwellOfStrength, TutorialCards.Natsumi, Own(TutorialCards.SilverMine));
            case "buy-archive": return offered.Single(a => a.Type == "purchase").Command! with { SourceIds = [] };
            case "buy-silver-mine": return offered.First(a => a.Type == "purchase").Command! with { SourceIds = [Own(TutorialCards.LargeFarm)] };
            case "defend":
                Expect(offered.Length == 1, $"defend: only Province 1 is offered, got {Offered(offered)}");
                return offered.Single().Command! with { Assignments = new[] { TutorialCards.IsawaTomohiro, TutorialCards.Natsumi, TutorialCards.ShibaSansesuke }.ToDictionary(Own, _ => 0) };
            case "equip-hunger":
            case "equip-tsunami":
                {
                    var (spell, wielder) = step == "equip-hunger" ? (TutorialCards.TheEarthsHunger, TutorialCards.IsawaTomohiro) : (TutorialCards.StrengthOfTheTsunami, TutorialCards.Natsumi);
                    var option = offered.FirstOrDefault(a => a.Command!.AbilityId == "equip-battle" && (a.Command.PerformerId is null || a.Command.PerformerId == Own(wielder)))
                        ?? throw new Exception($"{step}: no Equip (Battle) for {wielder}: {Offered(offered)}");
                    var sources = option.TargetGold?.GetValueOrDefault(Hand(spell))?.Payments.FirstOrDefault()?.SourceIds ?? option.Gold?.Payments.FirstOrDefault()?.SourceIds ?? [];
                    return option.Command! with { PerformerId = Own(wielder), TargetId = Hand(spell), SourceIds = sources };
                }
            case "destroy-waru":
            case "utoro-home":
                {
                    // The table submits a single-target option as it stands: the step's narrowed target list and the command's own target must both be the one card the note names.
                    var target = InPlay(runner, 1, step == "destroy-waru" ? TutorialCards.BayushiWaru : TutorialCards.BayushiUtoro).Id;
                    Expect(offered.Length == 1 && offered[0].TargetIds is [var only] && only == target && offered[0].Command!.TargetId == target, $"{step}: the only target offered is the one the note names: {Offered(offered)}");
                    return offered[0].Command!;
                }
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {Offered(offered)}");
                return offered[0].Command!;
        }
    }

    private static void PlayUntil(TutorialRunner runner, string stepId)
    {
        for (var guard = 0; runner.Coach.StepId != stepId && !runner.Finished && guard < 200; guard++)
        {
            if (runner.Coach.Button is not null) { Expect(runner.Continue(), "the button works"); continue; }
            var result = runner.Execute(CommandFor(runner)); Expect(result.Accepted, $"{runner.Coach.StepId}: {result.Error}");
        }
        Expect(runner.Coach.StepId == stepId, $"reached {stepId}");
    }

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            var state = runner.Engine.State;
            switch (coach.StepId)
            {
                case "elements":
                    Expect(InPlay(runner, 0, TutorialCards.ScouringFlood).AttachedTo == InPlay(runner, 0, TutorialCards.IsawaTomohiro).Id, "Scouring Flood is attached to Isawa Tomohiro");
                    break;
                case "swell-traits":
                    Expect(InPlay(runner, 0, TutorialCards.TheSwellOfStrength).AttachedTo == InPlay(runner, 0, TutorialCards.Natsumi).Id && InPlay(runner, 0, TutorialCards.SilverMine).Bowed, "The Swell of Strength is on Natsumi, paid by the Silver Mine");
                    break;
                case "buy-archive":
                    Expect(state is { ActivePlayerId: 0, Phase: Phase.Dynasty }, "you skipped your attack and are in your Dynasty phase");
                    break;
                case "limited":
                    Expect(InPlay(runner, 0, TutorialCards.WhisperingArchive) is not null && Cards(runner, 0, TutorialCards.SilverMine).Count(c => c.Zone == Zone.Home) == 2, "you bought the Whispering Archive and a second Silver Mine");
                    Expect(!state.Cards[state.Players[0].StrongholdId].Bowed, "and your Stronghold is still straightened");
                    Expect(state is { ActivePlayerId: 1, Phase: Phase.Action, PriorityPlayerId: 0 }, "it is the Scorpion's Action phase and your turn to act");
                    Expect(runner.Engine.QueryActions(0).Where(a => a.Type == "equip").All(a => !a.Enabled), "the engine refuses Equip (Limited) on the Scorpion's turn");
                    break;
                case "defend":
                    Expect(state.Players[1].Provinces.Count(p => p.Destroyed) == 0, "nothing is destroyed yet");
                    Expect(new[] { TutorialCards.BayushiHurunayi, TutorialCards.BayushiUtoro, TutorialCards.BayushiWaru }.All(d => InPlay(runner, 1, d) is { Zone: Zone.Battlefield, ProvinceIndex: 0 }), "the Scorpion attacks your Province 1 with all three Personalities");
                    break;
                case "equip-battle":
                    Expect(state is { Phase: Phase.Battle, PriorityPlayerId: 0 }, "the battle has begun and the defender (you) acts first");
                    break;
                case "destroy-waru":
                    Expect(InPlay(runner, 0, TutorialCards.TheEarthsHunger).AttachedTo == InPlay(runner, 0, TutorialCards.IsawaTomohiro).Id, "The Earth's Hunger is attached to Tomohiro");
                    break;
                case "redirect":
                    Expect(Cards(runner, 1, TutorialCards.BayushiWaru).Single().Zone == Zone.Dead, "Bayushi Waru was destroyed");
                    Expect(InPlay(runner, 0, TutorialCards.IsawaTomohiro).Bowed && !InPlay(runner, 0, TutorialCards.TheEarthsHunger).Bowed, "Tomohiro is not Earth, so he bowed and the Spell did not");
                    break;
                case "equip-tsunami":
                    Expect(InPlay(runner, 0, TutorialCards.ShibaSansesuke).Bowed && !InPlay(runner, 0, TutorialCards.Natsumi).Bowed, "the bow landed on Sansesuke and Natsumi is unbowed");
                    Expect(InPlay(runner, 1, TutorialCards.BayushiHurunayi).Bowed, "Hurunayi bowed himself to pay for it");
                    break;
                case "done":
                    Expect(InPlay(runner, 0, TutorialCards.StrengthOfTheTsunami).AttachedTo == InPlay(runner, 0, TutorialCards.Natsumi).Id, "Strength of the Tsunami is on Natsumi");
                    Expect(state.Cards[state.Players[0].StrongholdId].Bowed, "Library of Rebirth bowed to pay for it");
                    Expect(InPlay(runner, 1, TutorialCards.BayushiUtoro).Zone == Zone.Home, "Bayushi Utoro was sent home");
                    var fighting = state.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).ToArray();
                    Expect(fighting is [{ Bowed: true } hurunayi] && hurunayi.DefinitionId == TutorialCards.BayushiHurunayi, "the only Scorpion Personality left at the battlefield is Hurunayi, bowed");
                    var natsumi = runner.Engine.GetView(0).Players[0].Army.Concat(runner.Engine.GetView(0).Players[0].Home).First(c => c.Definition?.Id == TutorialCards.Natsumi);
                    Expect(natsumi.Force == 11, $"Natsumi has 11 Force, the note says; the engine shows {natsumi.Force}");
                    break;
            }
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended");
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
        Expect(runner.Engine.WinnerId is null, "the game goes on");
    }

    private static void OpensInTheActionPhase()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { ActivePlayerId: 0, Phase: Phase.Action, PriorityPlayerId: 0 }, "the table is at your Action phase with priority");
        Expect(state.Players[0].Clan == "Phoenix" && state.Players[1].Clan == "Scorpion" && state.Players[0].Honor == 10 && state.Players[1].Honor == 3, "Phoenix on 10 Honor against Scorpion on 3");
        Expect(state.Cards[state.Players[0].StrongholdId].DefinitionId == TutorialCards.LibraryOfRebirth && state.Cards[state.Players[1].StrongholdId].DefinitionId == TutorialCards.TowersOfTheYogo, "Library of Rebirth against The Towers of the Yogo");
        var hand = state.Players[0].Hand.Select(id => state.Cards[id].DefinitionId).Order().ToArray();
        Expect(hand.SequenceEqual(new[] { TutorialCards.ObscuredPathways, TutorialCards.PeaceOfTheKami, TutorialCards.ScouringFlood, TutorialCards.TheEarthsHunger, TutorialCards.StrengthOfTheTsunami, TutorialCards.TheSwellOfStrength }.Order()), "six Spells in hand");
        Expect(hand.All(d => CardCatalog.Get(d).Type == CardType.Spell), "they are all Spells");
        var home = state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home).Select(c => c.DefinitionId).Order().ToArray();
        Expect(home.SequenceEqual(new[] { TutorialCards.SilverMine, TutorialCards.BorderKeep, TutorialCards.SmallFarm, TutorialCards.LargeFarm, TutorialCards.ShibaSansesuke, TutorialCards.IsawaTomohiro, TutorialCards.Natsumi }.Order()), "your Holdings and three Personalities are in play");
        Expect(state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold).All(c => !c.Bowed), "and straightened");
        var provinces = Enumerable.Range(0, 4).Select(i => state.Cards[Province(runner, 0, i)]).ToArray();
        Expect(provinces.Select(c => c.DefinitionId).SequenceEqual([TutorialCards.WhisperingArchive, TutorialCards.SilverMine, TutorialCards.AgashaKodo, TutorialCards.AsakoMegu]), "your Provinces hold Whispering Archive, Silver Mine, Agasha Kodo and Asako Megu");
        Expect(provinces[0].FaceUp && provinces[1].FaceUp && !provinces[2].FaceUp && !provinces[3].FaceUp, "the first two are face up and the last two face down");
        var foeHome = state.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Home).Select(c => c.DefinitionId).Order().ToArray();
        Expect(foeHome.SequenceEqual(new[] { TutorialCards.GeishaHouse, TutorialCards.SmallFarm, TutorialCards.RemoteVillage, TutorialCards.BayushiHurunayi, TutorialCards.BayushiUtoro, TutorialCards.BayushiWaru }.Order()), "the Scorpion has its Holdings and three Personalities");
        Expect(Cards(runner, 1, TutorialCards.SeppunHeavyElite).Single() is { Zone: Zone.Attached } elite && elite.AttachedTo == InPlay(runner, 1, TutorialCards.BayushiUtoro).Id, "Bayushi Utoro has the Seppun Heavy Elite");
    }

    private static void OnlyShugenjaTakeSpells()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        var sansesuke = InPlay(runner, 0, TutorialCards.ShibaSansesuke).Id;
        var equips = runner.Engine.QueryActions(0).Where(a => a.Type == "equip" && a.Enabled && state.Cards[a.CardId ?? a.Command!.CardId!].DefinitionId is var d && CardCatalog.Get(d).Type == CardType.Spell).ToArray();
        Expect(equips.Length > 0, "Equip (Limited) is open for your Spells");
        Expect(equips.All(a => a.TargetIds is { Length: > 0 } targets && !targets.Contains(sansesuke)), "Shiba Sansesuke is never offered for a Spell");
        Expect(equips.All(a => a.TargetIds!.All(id => CardCatalog.Get(state.Cards[id].DefinitionId).Keywords!.Contains("Shugenja"))), "only Shugenja are offered");
        Expect(!CardCatalog.Get(TutorialCards.ShibaSansesuke).Keywords!.Contains("Shugenja") && CardCatalog.Get(TutorialCards.IsawaTomohiro).Keywords!.Contains("Shugenja") && CardCatalog.Get(TutorialCards.Natsumi).Keywords!.Contains("Shugenja"), "Tomohiro and Natsumi are Shugenja; Sansesuke is not");
        Expect(new[] { TutorialCards.BayushiHurunayi, TutorialCards.BayushiUtoro, TutorialCards.BayushiWaru }.All(d => !CardCatalog.Get(d).Keywords!.Contains("Shugenja")), "no Scorpion Personality is a Shugenja");
        Expect(runner.Engine.QueryActions(0).Where(a => a.Command?.AbilityId == "equip-battle").All(a => !a.Enabled), "Equip (Battle) is not open outside a battle");
        var flood = runner.Engine.QueryActions(0).First(a => a.Type == "equip" && a.Enabled && (a.CardId ?? a.Command!.CardId) == Id(runner, 0, TutorialCards.ScouringFlood, Zone.Hand)).Command!;
        Expect(!runner.Engine.Execute(flood with { TargetId = sansesuke, SourceIds = [InPlay(runner, 0, TutorialCards.BorderKeep).Id] }).Accepted, "the engine refuses Scouring Flood on Sansesuke");
    }

    private static void ElementsAndLibrary()
    {
        Expect(!CardCatalog.Get(TutorialCards.IsawaTomohiro).Keywords!.Contains("Earth") && CardCatalog.Get(TutorialCards.IsawaTomohiro).Keywords!.Contains("Air") && CardCatalog.Get(TutorialCards.IsawaTomohiro).Keywords!.Contains("Void"), "Tomohiro is Air and Void, not Earth");
        Expect(CardCatalog.Get(TutorialCards.ScouringFlood).Keywords!.Contains("Water") && !CardCatalog.Get(TutorialCards.IsawaTomohiro).Keywords!.Contains("Water"), "Scouring Flood is Water and Tomohiro is not");
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "equip-tsunami");
        var natsumi = InPlay(runner, 0, TutorialCards.Natsumi).Id; var tsunami = Id(runner, 0, TutorialCards.StrengthOfTheTsunami, Zone.Hand);
        var option = runner.Actions().First(a => a.Enabled && a.Command!.AbilityId == "equip-battle");
        var quote = option.TargetGold?.GetValueOrDefault(tsunami) ?? option.Gold;
        Expect(quote is { Cost: 7 }, $"Strength of the Tsunami costs 7 with the Whispering Archive, quoted {quote?.Cost}");
        var library = runner.Engine.State.Players[0].StrongholdId;
        Expect(quote!.Sources is { } sources && sources.Any(s => s.Produced == 4 && s.CardId == library && s.Id != library), "Library of Rebirth's Reaction is offered as a 4-Gold source of its own");
    }

    private static void QuotedNumbers()
    {
        Expect(CardCatalog.Get(TutorialCards.ScouringFlood).GoldCost == 2 && CardCatalog.Get(TutorialCards.TheSwellOfStrength).GoldCost == 3 && CardCatalog.Get(TutorialCards.TheEarthsHunger).GoldCost == 1 && CardCatalog.Get(TutorialCards.StrengthOfTheTsunami).GoldCost == 8, "Spell costs: 2, 3, 1 and 8");
        Expect(CardCatalog.Get(TutorialCards.WhisperingArchive).GoldCost == 0 && CardCatalog.Get(TutorialCards.SilverMine).GoldCost == 2, "Whispering Archive costs 0 and Silver Mine 2");
        Expect(CardCatalog.Get(TutorialCards.BorderKeep).GoldProduction == 2 && CardCatalog.Get(TutorialCards.LargeFarm).GoldProduction == 2 && CardCatalog.Get(TutorialCards.LibraryOfRebirth).GoldProduction == 4, "Border Keep and Large Farm make 2, Library of Rebirth 4");
        Expect(CardCatalog.Get(TutorialCards.Natsumi).Force + 2 + 6 == 11, "Natsumi: 3 Force, +2 from The Swell of Strength and +6 from Strength of the Tsunami");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var steps = TutorialLessons.Get(Lesson).Steps;
        Expect(steps.Length is >= 15 and <= 40, $"the lesson is between 15 and 40 steps, got {steps.Length}");
        Expect(steps.Select(s => s.Id).Distinct().Count() == steps.Length, "step ids are unique");
        foreach (var step in steps) Expect(step.IsAction ? step.Instruction is { Length: > 0 } && step.Button is null : step.Button is { Length: > 0 }, $"{step.Id}: a move says what to click, a note has a button");
        Expect(steps[^1].Button == "Tutorial Menu" && steps[^1].Body.EndsWith("Select the Next Tutorial to continue learning."), "the last step offers the menu and tells the player to select the Next Tutorial");
        Expect(TutorialCurriculum.TrackOf(Lesson)?.Title == "Level Up Your Play", "the lesson belongs to Level Up Your Play");
        Expect(TutorialCurriculum.All.Single(e => e.Id == Lesson).Built, "the menu entry is built");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        Expect(runner.Back() is null, "there is nothing to go back to at the first step");
        var guard = 0;
        while (!runner.Finished && guard++ < 200)
        {
            var before = runner.Coach.StepId; var version = runner.Engine.GetView(TutorialRunner.Seat).Version;
            if (runner.Coach.Button is not null) Expect(runner.Continue(), "the button works");
            else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{before}: the move is accepted");
            if (runner.Finished) break;
            var back = runner.Back();
            Expect(back is not null && back.Coach.StepId == before && back.Engine.GetView(TutorialRunner.Seat).Version == version, $"going back from the step after {before} shows {before} as it was");
            runner = back!;
            if (runner.Coach.Button is not null) Expect(runner.Continue(), "the button works again");
            else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{before}: the move is accepted again");
        }
        Expect(runner.Finished, "the lesson still reaches its end after going back at every step");
    }
}
