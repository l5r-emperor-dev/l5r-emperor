using L5R.Core;

// Issue #1075, the Recruiting Personalities lesson: Crab Control against Classic Crane is played through the runner the way a player would, so a rules or card change that breaks the lesson
// (a Clan Discount that stops defaulting, an Overlay that stops carrying attachments, a Loyal card that becomes recruitable) fails here instead of in front of a new player.
internal static class TutorialRecruitingTests
{
    private const string Lesson = "recruiting";

    public static void Run(TestRunner runner) => runner.Run("tutorial-recruiting", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in your Dynasty phase a couple of turns in: Crab on -3 Honor, Crane on 1, the Provinces face up, nothing in anyone's hand", OpensAtTheDynastyPhase),
        ("Yoritomo Sachina is Loyal to Mantis: the engine refuses her to the Crab player, and discarding her ends the recruiting, so the lesson discards her last", SachinaCannotBeRecruited),
        ("Baiko must be paid exactly 6 Gold at full cost and Tanimura with the Clan Discount: a different mode or a payment of another amount is refused", RecruitingIsExact),
        ("The Overlay carries the Pillowbook and the -1 Chi token to the Experienced Yasuki Tsujiken and removes the old one from the game", OverlayCarriesOver),
        ("The Crane player recruits Kenshi with Clan Allegiance and Proclaims her, then Tsunehiko with the Clan Discount and the Marketplace, and discards Utaku Mai, paying with exactly the cards the notes name", CraneRecruits),
        ("The numbers the lesson quotes are the catalogue's and the engine's", QuotedNumbers),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu, with the next lesson offered beside it", EveryStepSaysWhatToDo),
        ("Every lesson but the last offers the next one on its final note, and the last offers none", NextLessonIsOffered),
        ("Back rebuilds the table as it was before the last button or move, at every step of the lesson", BackUndoesTheLastStep),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance One(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First();
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Stronghold);
    private static bool InZone(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);
    private static int Honor(TutorialRunner runner, int seat) => runner.Engine.State.Players[seat].Honor;
    private static string Province(TutorialRunner runner, int owner, int index) => runner.Engine.State.Players[owner].Provinces[index].CardId!;

    /// <summary>Gold sources of yours (by definition) that produce exactly the given amount for a Personality.</summary>
    private static string[] Sources(TutorialRunner runner, params string[] definitions) => definitions.Select(d => InPlay(runner, 0, d).Id).ToArray();

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing");
        var palaces = runner.Engine.State.Players[0].StrongholdId;
        switch (step)
        {
            case "proclaim-baiko":
                return offered.First(a => a.Type == "purchase" && !a.Command!.ClanDiscount && !a.Command.ClanAllegiance).Command! with { SourceIds = [palaces, InPlay(runner, 0, TutorialCards.BorderKeep).Id] };
            case "recruit-tanimura":
                return offered.First(a => a.Type == "purchase" && a.Command!.ClanDiscount).Command! with { SourceIds = Sources(runner, TutorialCards.BambooHarvesters, TutorialCards.IronMine, TutorialCards.SmallFarm) };
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {string.Join(", ", offered.Select(a => a.Label))}");
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
                case "proclaimed":
                    Expect(InZone(runner, 0, TutorialCards.YasukiBaiko, Zone.Home) && Honor(runner, 0) == -2, "Baiko joined you and Proclaiming her took your Honor from -3 to -2");
                    break;
                case "out-of-gold":
                    Expect(InZone(runner, 0, TutorialCards.YasukiTanimura, Zone.Home), "Tanimura joined you");
                    Expect(Honor(runner, 0) == -2, "the Clan Discount did not Proclaim him");
                    Expect(state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).All(c => c.Bowed), "every Gold source is bowed: you are out of Gold");
                    break;
                case "overlaid":
                    Expect(InZone(runner, 0, TutorialCards.YasukiTsujikenExp, Zone.Home) && !Cards(runner, 0, TutorialCards.YasukiTsujiken).Any(c => c.Zone is Zone.Home), "the Experienced Tsujiken replaced the old one");
                    break;
                case "pass-turn":
                    Expect(InZone(runner, 0, TutorialCards.YoritomoSachina, Zone.DynastyDiscard) && state.Phase == Phase.DynastyDiscard, "Sachina was discarded, which started the Dynasty discard step");
                    break;
                case "crane-turn":
                    Expect(state is { ActivePlayerId: 1, Phase: Phase.AttackDeclaration } && Honor(runner, 1) == 1, "it is the Crane's turn on 1 Honor, with its Provinces face up");
                    Expect(Enumerable.Range(0, 4).All(i => state.Cards[Province(runner, 1, i)].FaceUp), "the Crane's four Provinces are face up");
                    Expect(runner.Coach.Emphasis == TutorialEmphasis.HonorRequirement, "the Honor Requirements are pointed at");
                    break;
                case "kenshi":
                    Expect(InZone(runner, 1, TutorialCards.DaidojiKenshi, Zone.Home) && Honor(runner, 1) == 3, "Kenshi joined the Crane and Proclaiming her took its Honor to 3");
                    Expect(InPlay(runner, 1, TutorialCards.FoeStronghold).Bowed && InPlay(runner, 1, TutorialCards.Marketplace).Bowed && !InPlay(runner, 1, TutorialCards.BorderKeep).Bowed, "the Crane paid with its Stronghold and Marketplace");
                    break;
                case "crane-done":
                    Expect(InZone(runner, 1, TutorialCards.DaidojiTsunehiko, Zone.Home) && Cards(runner, 1, TutorialCards.Marketplace).Count(c => c.Zone == Zone.Home) == 2, "Tsunehiko joined and the second Marketplace was bought");
                    Expect(InZone(runner, 1, TutorialCards.UtakuMai, Zone.DynastyDiscard) && Honor(runner, 1) == 3, "Utaku Mai was discarded and Tsunehiko was not Proclaimed");
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

    private static void OpensAtTheDynastyPhase()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state.Players[0].Clan == "Crab" && state.Players[1].Clan == "Crane" && Honor(runner, 0) == -3 && Honor(runner, 1) == 1, "Crab on -3 Honor against Crane on 1");
        Expect(state.Players.All(p => p.Hand.Count == 0) && state.Cards.Values.All(c => c.Zone != Zone.Hand), "nobody has a hand");
        Expect(state.Players[0].StrongholdId is var palaces && state.Cards[palaces].DefinitionId == TutorialCards.YasukiPalaces, "you play Yasuki Palaces");
        Expect(runner.Continue(), "the welcome note's button works");
        state = runner.Engine.State;
        Expect(state is { ActivePlayerId: 0, Phase: Phase.Dynasty } && state.Players[0].TurnsTaken >= 3, "the table is at your Dynasty phase, a couple of turns in");
        Expect(Enumerable.Range(0, 4).All(i => state.Cards[Province(runner, 0, i)].FaceUp), "your four Provinces are face up");
        Expect(new[] { TutorialCards.YasukiTanimura, TutorialCards.YoritomoSachina, TutorialCards.YasukiTsujikenExp, TutorialCards.YasukiBaiko }.SequenceEqual(Enumerable.Range(0, 4).Select(i => state.Cards[Province(runner, 0, i)].DefinitionId)), "Tanimura, Sachina, Tsujiken (Experienced) and Baiko");
        var home = state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home).Select(c => c.DefinitionId).Order().ToArray();
        Expect(home.SequenceEqual(new[] { TutorialCards.BorderKeep, TutorialCards.BambooHarvesters, TutorialCards.IronMine, TutorialCards.SmallFarm, TutorialCards.YasukiTsujiken }.Order()), "Border Keep, Bamboo Harvesters, Iron Mine, Small Farm and Yasuki Tsujiken are in play");
        var tsujiken = InPlay(runner, 0, TutorialCards.YasukiTsujiken);
        Expect(Cards(runner, 0, TutorialCards.Pillowbook).Single() is { Zone: Zone.Attached } pillow && pillow.AttachedTo == tsujiken.Id, "the Pillowbook is attached to Tsujiken");
        Expect(tsujiken.Tokens is [{ Stat: "Chi", Amount: -1 }], "and he carries a -1 Chi token");
        Expect(Cards(runner, 1, TutorialCards.Marketplace).Count(c => c.Zone == Zone.Home) == 1 && InPlay(runner, 1, TutorialCards.ProsperousVillage) is not null && InPlay(runner, 1, TutorialCards.LargeFarm) is not null, "the Crane has its Marketplace, Prosperous Village and Large Farm");
        Expect(new[] { TutorialCards.UtakuMai, TutorialCards.DaidojiKenshi, TutorialCards.DaidojiTsunehiko, TutorialCards.Marketplace }.SequenceEqual(Enumerable.Range(0, 4).Select(i => state.Cards[Province(runner, 1, i)].DefinitionId)), "the Crane's Provinces hold Utaku Mai, Kenshi, Tsunehiko and a Marketplace");
    }

    private static void SachinaCannotBeRecruited()
    {
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "sachina");
        var sachina = Province(runner, 0, 1);
        var buy = runner.Engine.QueryActions(0).Where(a => a.Type == "purchase" && a.CardId == sachina).ToArray();
        Expect(buy.Length > 0 && buy.All(a => !a.Enabled && a.Reason == "Loyal Personalities can only be recruited by a Clan they are aligned with."), "the engine refuses Sachina to the Crab player because she is Loyal, and says why");
        Expect(CardCatalog.Get(TutorialCards.YoritomoSachina).Keywords!.Contains("Loyal") && CardCatalog.Get(TutorialCards.YoritomoSachina).Keywords!.Contains("Mantis Clan"), "she is Loyal and Mantis Clan");
        // Discarding her first would end the recruiting: nothing else could be bought afterwards.
        Expect(runner.Engine.Execute(runner.Engine.QueryActions(0).First(a => a.Type == "discardProvince" && a.CardId == sachina).Command!).Accepted, "she can be discarded");
        Expect(runner.Engine.QueryActions(0).All(a => a.Type != "purchase" && a.Type != "overlay"), "but once she is, there is no more recruiting this turn");
    }

    private static void RecruitingIsExact()
    {
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "proclaim-baiko");
        var keep = InPlay(runner, 0, TutorialCards.BorderKeep).Id; var palaces = runner.Engine.State.Players[0].StrongholdId; var harvesters = InPlay(runner, 0, TutorialCards.BambooHarvesters).Id;
        var offered = runner.Actions().Where(a => a.Enabled).ToArray();
        Expect(offered.All(a => a.Type == "purchase" && a.CardId == Province(runner, 0, 3)), "only Baiko can be recruited");
        var full = offered.First(a => !a.Command!.ClanDiscount && !a.Command.ClanAllegiance).Command!;
        Expect(offered.Any(a => a.Command!.ClanDiscount), "the Clan Discount is offered too");
        Expect(offered.First(a => !a.Command!.ClanDiscount && !a.Command.ClanAllegiance).Quote is { ProclaimEligible: true, Cost: 6 }, "Baiko's full cost is 6 and she is eligible to Proclaim");
        Expect(!runner.Execute(offered.First(a => a.Command!.ClanDiscount).Command! with { SourceIds = [palaces, keep] }).Accepted, "the Clan Discount is refused for Baiko");
        Expect(!runner.Execute(full with { SourceIds = [palaces, keep, harvesters] }).Accepted && !runner.Execute(full with { SourceIds = [palaces] }).Accepted, "overpaying and underpaying are refused");
        Expect(runner.Execute(full with { SourceIds = [palaces, keep] }).Accepted && runner.Coach.StepId == "proclaimed", "exactly 6 at full cost is accepted");
        PlayUntil(runner, "recruit-tanimura");
        var tanimura = runner.Actions().Where(a => a.Enabled).ToArray();
        Expect(tanimura.Length == 1 && tanimura[0].Command!.ClanDiscount && tanimura[0].Quote is { Cost: 6, ProclaimEligible: false }, "after Baiko, Tanimura is offered only with the Clan Discount, for 6, and cannot Proclaim");
        var rest = Sources(runner, TutorialCards.BambooHarvesters, TutorialCards.IronMine, TutorialCards.SmallFarm);
        Expect(!runner.Execute(tanimura[0].Command! with { SourceIds = rest[..2] }).Accepted, "less than 6 is refused");
        Expect(runner.Execute(tanimura[0].Command! with { SourceIds = rest }).Accepted && Honor(runner, 0) == -2, "6 Gold with the Clan Discount recruits him without Proclaiming");
    }

    private static void OverlayCarriesOver()
    {
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "overlay");
        var old = InPlay(runner, 0, TutorialCards.YasukiTsujiken);
        var offered = runner.Actions().Where(a => a.Enabled).ToArray();
        Expect(offered.Length == 1 && offered[0].Type == "overlay" && offered[0].Command!.TargetId == old.Id, "only the Overlay onto the Tsujiken in play is offered");
        Expect(runner.Execute(offered[0].Command!).Accepted, "the Overlay is accepted");
        var experienced = InPlay(runner, 0, TutorialCards.YasukiTsujikenExp);
        Expect(Cards(runner, 0, TutorialCards.Pillowbook).Single().AttachedTo == experienced.Id, "the Pillowbook moved to the Experienced Tsujiken");
        Expect(experienced.Tokens is [{ Stat: "Chi", Amount: -1 }], "and so did the -1 Chi token");
        Expect(Cards(runner, 0, TutorialCards.YasukiTsujiken).Single(c => c.Id == old.Id).Zone == Zone.RemovedFromGame, "the old Tsujiken is removed from the game");
        Expect(runner.Engine.State.Players[0].Provinces[2].CardId is { } refill && !runner.Engine.State.Cards[refill].FaceUp, "the Province was refilled face down");
    }

    private static void CraneRecruits()
    {
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "allegiance");
        var state = runner.Engine.State;
        Expect(Honor(runner, 1) == 1 && state is { ActivePlayerId: 1 }, "the Crane's turn begins on 1 Honor");
        var mai = state.Cards[Province(runner, 1, 0)]; var kenshi = state.Cards[Province(runner, 1, 1)]; var tsunehiko = state.Cards[Province(runner, 1, 2)];
        var quote = runner.Engine.QuotePurchase(1, mai.Id, false, true);
        Expect(quote.Error is not null, "Utaku Mai cannot be recruited at all: her Honor Requirement is 5 and she is not Crane Clan, so Clan Allegiance does not help");
        Expect(runner.Engine.QuotePurchase(1, kenshi.Id).Error is not null && runner.Engine.QuotePurchase(1, kenshi.Id, false, true) is { Error: null, Cost: 7, ProclaimEligible: true }, "Kenshi needs Clan Allegiance and then costs 5 + 2 = 7");
        Expect(runner.Engine.QuotePurchase(1, tsunehiko.Id) is { Error: null, Cost: 8 } && runner.Engine.QuotePurchase(1, tsunehiko.Id, true) is { Error: null, Cost: 6 }, "Tsunehiko meets his Honor Requirement: 8 Gold, 6 with the Clan Discount");
        PlayUntil(runner, "crane-done");
        Expect(Honor(runner, 1) == 3, "Kenshi's Proclaim made it 3, and no Proclaim for Tsunehiko");
    }

    private static void QuotedNumbers()
    {
        Expect(CardCatalog.Get(TutorialCards.YasukiBaiko).GoldCost == 6 && CardCatalog.Get(TutorialCards.YasukiTanimura).GoldCost == 8 && CardCatalog.Get(TutorialCards.YasukiTsujikenExp).GoldCost == 11, "the notes' costs: 6, 8 and 11");
        Expect(6 + 8 + 11 == 25, "which total 25");
        Expect(CardCatalog.Get(TutorialCards.YasukiBaiko).PersonalHonor == 1 && CardCatalog.Get(TutorialCards.DaidojiKenshi).PersonalHonor == 2, "Baiko has 1 Personal Honor and Kenshi 2");
        Expect(CardCatalog.Get(TutorialCards.UtakuMai).HonorRequirement == 5 && CardCatalog.Get(TutorialCards.DaidojiKenshi).HonorRequirement == 4 && CardCatalog.Get(TutorialCards.DaidojiTsunehiko).HonorRequirement == 0, "Honor Requirements: Mai 5, Kenshi 4, Tsunehiko 0 (below the Crane's 1)");
        Expect(CardCatalog.Get(TutorialCards.YasukiBaiko).HonorRequirement is null && CardCatalog.Get(TutorialCards.YasukiTanimura).HonorRequirement is null && CardCatalog.Get(TutorialCards.YasukiTsujikenExp).HonorRequirement is null, "your Personalities have none");
        Expect(CardCatalog.Get(TutorialCards.DaidojiKenshi).GoldCost + 2 == 7 && CardCatalog.Get(TutorialCards.DaidojiTsunehiko).GoldCost - 2 == 6, "Kenshi with Clan Allegiance is 7 and Tsunehiko with the Clan Discount 6");
        Expect(CardCatalog.Get(TutorialCards.YasukiPalaces).GoldProduction - 1 + 2 + 2 + 3 + 1 == 12, "your Gold sources make 12 for Personalities: Stronghold 4 (1 less), Border Keep 2, Bamboo Harvesters 2, Iron Mine 3 (Crab), Small Farm 1");
        Expect(CardCatalog.Get(TutorialCards.FoeStronghold).GoldProduction + 3 == 7, "the Crane's Stronghold and Marketplace (3 for a Crane player) make 7");
        var runner = TutorialRunner.Start(Lesson);
        Expect(runner.Engine.State.Cards[runner.Engine.State.Players[0].StrongholdId].DefinitionId == TutorialCards.YasukiPalaces && runner.Engine.State.ActivePlayerId == 0, "Crab Control (you, first) against Classic Crane");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var steps = TutorialLessons.Get(Lesson).Steps;
        Expect(steps.Length is >= 15 and <= 40, $"the lesson is between 15 and 40 steps, got {steps.Length}");
        Expect(steps.Select(s => s.Id).Distinct().Count() == steps.Length, "step ids are unique");
        foreach (var step in steps) Expect(step.IsAction ? step.Instruction is { Length: > 0 } && step.Button is null : step.Button is { Length: > 0 }, $"{step.Id}: a move says what to click, a note has a button");
        Expect(steps[^1].Button == "Tutorial Menu" && steps[^1].Body.EndsWith("Select the Next Tutorial to continue learning."), "the last step offers the menu and tells the player to select the Next Tutorial");
        Expect(steps.Count(s => s.Emphasis == TutorialEmphasis.Cost) == 1 && steps.Count(s => s.Emphasis == TutorialEmphasis.HonorRequirement) == 2, "the Gold Cost and the Honor Requirements are pointed at");
        Expect(TutorialCurriculum.TrackOf(Lesson)?.Title == "Level Up Your Play", "the lesson belongs to Level Up Your Play, so the menu returns there");
    }

    private static void NextLessonIsOffered()
    {
        var all = TutorialCurriculum.All.ToArray();
        Expect(TutorialCurriculum.After("first-turn")?.Id == "dynasty-deck" && TutorialCurriculum.After("dynasty-deck")?.Id == "recruiting" && TutorialCurriculum.After("recruiting")?.Id == "spells", "The Basics, then Events Regions and Celestials, then Recruiting, then Spells");
        Expect(TutorialCurriculum.After(all[^1].Id) is null, "nothing follows the last lesson");
        foreach (var lesson in TutorialLessons.All.Where(l => !l.Steps[^1].FreePlay)) // the Rings lesson ends by playing the game out; TutorialRingsTests checks its win and loss notes
        {
            var runner = TutorialRunner.Start(lesson.Id);
            var steps = lesson.Steps;
            Expect(steps[^1].Body.EndsWith("Select the Next Tutorial to continue learning."), $"{lesson.Id}: the last note tells the player to select the Next Tutorial");
            Expect(steps[^1].Button == "Tutorial Menu", $"{lesson.Id}: and its button is Tutorial Menu");
        }
        var recruiting = TutorialRunner.Start(Lesson);
        PlayUntil(recruiting, "done");
        Expect(recruiting.Coach is { NextLessonId: "spells", NextTitle: "Spells and Shugenja" }, "the last note offers Spells and Shugenja next");
        Expect(TutorialRunner.Start(Lesson).Coach.NextLessonId is null, "an ordinary note offers no next lesson");
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
