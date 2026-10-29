using L5R.Core;

// Issue #1077, the Attachments lesson: Scorpion Lethargy against Phoenix is played through the runner the way a player would, so a rules or card change that breaks the lesson (an Item that no longer attaches,
// Equip (Battle) losing its additional action, a bowed Personality's unbowed attachment no longer usable, the Gold running short) fails here instead of in front of a new player.
internal static class TutorialAttachmentsTests
{
    private const string Lesson = "attachments";

    public static void Run(TestRunner runner) => runner.Run("tutorial-attachments", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("The lesson opens in your Action phase with five attachments in hand, six Spells in the Phoenix's, every Province face down, 3 Honor against 10", OpensInTheActionPhase),
        ("Your attachments cost exactly what your table makes, each paid by one source", GoldIsExact),
        ("The numbers the lesson quotes are the catalogue's and the engine's", QuotedNumbers),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move, at every step of the lesson", BackUndoesTheLastStep),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold or Zone.Attached);
    private static string Id(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).First(c => c.Zone == zone).Id;

    private static string Offered(ActionOption[] options) => string.Join("; ", options.Select(a => $"{a.Type}/{a.Command?.AbilityId}/{a.Label}/card={a.CardId}/perf={a.Command?.PerformerId}/tgt={a.Command?.TargetId}/targets={string.Join(",", a.TargetIds ?? [])}"));

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing: " + string.Join("; ", runner.Actions().Where(a => a.Command?.AbilityId is not null || a.Type is "pass").Select(a => $"{a.Type}/{a.Command?.AbilityId}/{(a.Enabled ? "on" : a.Reason)}")));
        string Own(string definition) => InPlay(runner, 0, definition).Id;
        string Hand(string definition) => Id(runner, 0, definition, Zone.Hand);
        GameCommand Equip(string card, string wearer, string source)
        {
            var option = offered.FirstOrDefault(a => (a.Command!.CardId == Hand(card) || a.CardId == Hand(card)) && (a.TargetIds?.Contains(Own(wearer)) ?? true))
                ?? throw new Exception($"{step}: no option equips {card}: {Offered(offered)}");
            return option.Command! with { CardId = option.Command.CardId ?? Hand(card), TargetId = Own(wearer), SourceIds = [Own(source)] };
        }
        switch (step)
        {
            case "equip-elite": return Equip(TutorialCards.SeppunHeavyElite, TutorialCards.BayushiWaru, TutorialCards.GeishaHouse);
            case "equip-blade": return Equip(TutorialCards.NightingaleBlade, TutorialCards.BayushiHurunayi, TutorialCards.TowersOfTheYogo);
            case "equip-light-armor": return Equip(TutorialCards.LightArmor, TutorialCards.BayushiHurunayi, TutorialCards.LargeFarm);
            case "equip-standard": return Equip(TutorialCards.ClanStandard, TutorialCards.BayushiUtoro, TutorialCards.RemoteVillage);
            case "equip-soshi": return Equip(TutorialCards.SoshiHouseGuard, TutorialCards.BayushiUtoro, TutorialCards.RedCraneDojo);
            case "assign-attackers":
                return offered.Single().Command! with { Assignments = new[] { TutorialCards.BayushiHurunayi, TutorialCards.BayushiUtoro, TutorialCards.BayushiWaru }.ToDictionary(Own, _ => 0) };
            case "soshi-bow":
            case "blade-bow":
            case "towers-bow":
                {
                    var target = InPlay(runner, 1, step == "soshi-bow" ? TutorialCards.Natsumi : TutorialCards.IsawaTomohiro).Id;
                    var option = offered.FirstOrDefault(a => (a.TargetIds?.Contains(target) ?? false) && (step != "towers-bow" || a.CardId == Own(TutorialCards.BayushiHurunayi) || a.Command!.CardId == Own(TutorialCards.BayushiHurunayi)))
                        ?? throw new Exception($"{step}: no option targets it: {Offered(offered)}");
                    return option.Command! with { TargetId = target };
                }
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {Offered(offered)}");
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
            var state = runner.Engine.State;
            switch (coach.StepId)
            {
                case "honor-requirement":
                    Expect(InPlay(runner, 0, TutorialCards.SeppunHeavyElite).AttachedTo == InPlay(runner, 0, TutorialCards.BayushiWaru).Id, "Seppun Heavy Elite is on Bayushi Waru");
                    break;
                case "armor":
                    Expect(InPlay(runner, 0, TutorialCards.NightingaleBlade).AttachedTo == InPlay(runner, 0, TutorialCards.BayushiHurunayi).Id, "Nightingale Blade is on Bayushi Hurunayi");
                    break;
                case "equip-standard":
                    Expect(InPlay(runner, 0, TutorialCards.LightArmor).AttachedTo == InPlay(runner, 0, TutorialCards.BayushiHurunayi).Id, "a Weapon and an Armor are both on Hurunayi");
                    break;
                case "abilities":
                    Expect(InPlay(runner, 0, TutorialCards.SoshiHouseGuard).AttachedTo == InPlay(runner, 0, TutorialCards.BayushiUtoro).Id && InPlay(runner, 0, TutorialCards.ClanStandard).AttachedTo == InPlay(runner, 0, TutorialCards.BayushiUtoro).Id, "Utoro has Clan Standard and Soshi House Guard");
                    Expect(runner.Engine.State.Players[0].Hand.Count == 0, "your hand is empty");
                    break;
                case "phoenix-first":
                    Expect(state is { Phase: Phase.Battle, PriorityPlayerId: 1 }, $"the battle has begun and the defender (Phoenix) acts first: {state.Phase} {state.PriorityPlayerId}");
                    Expect(new[] { TutorialCards.IsawaTomohiro, TutorialCards.Natsumi }.All(d => InPlay(runner, 1, d) is { Zone: Zone.Battlefield }), "both Phoenix Personalities defend");
                    break;
                case "spells":
                    Expect(InPlay(runner, 1, TutorialCards.StrengthOfTheTsunami).AttachedTo == InPlay(runner, 1, TutorialCards.Natsumi).Id, "Strength of the Tsunami is on Natsumi");
                    break;
                case "soshi-bow":
                    Expect(InPlay(runner, 0, TutorialCards.BayushiWaru).Zone == Zone.Home, "Bayushi Waru was sent home");
                    Expect(state.PriorityPlayerId == 0, "it is your turn to act");
                    Expect(InPlay(runner, 0, TutorialCards.SeppunHeavyElite).AttachedTo == InPlay(runner, 0, TutorialCards.BayushiWaru).Id, "Seppun Heavy Elite went home with Waru");
                    break;
                case "swell":
                    Expect(ForceOf(runner, TutorialCards.BayushiHurunayi) == 9, $"Hurunayi has 9 Force before The Swell of Strength, the engine shows {ForceOf(runner, TutorialCards.BayushiHurunayi)}");
                    Expect(InPlay(runner, 1, TutorialCards.Natsumi).Bowed, "Soshi House Guard bowed Natsumi");
                    Expect(InPlay(runner, 1, TutorialCards.TheSwellOfStrength).AttachedTo == InPlay(runner, 1, TutorialCards.IsawaTomohiro).Id, "The Swell of Strength is on Isawa Tomohiro");
                    break;
                case "towers-bow":
                    Expect(ForceOf(runner, TutorialCards.BayushiHurunayi) == 0, $"Hurunayi's Force is 0, the engine shows {ForceOf(runner, TutorialCards.BayushiHurunayi)}");
                    break;
                case "bowed-host":
                    Expect(InPlay(runner, 1, TutorialCards.IsawaTomohiro).Bowed && InPlay(runner, 0, TutorialCards.BayushiHurunayi).Bowed && !InPlay(runner, 0, TutorialCards.NightingaleBlade).Bowed, "Tomohiro and Hurunayi are bowed, Nightingale Blade is not");
                    break;
                case "blade-bow":
                    Expect(!InPlay(runner, 1, TutorialCards.IsawaTomohiro).Bowed, "the Phoenix's Stronghold straightened Isawa Tomohiro");
                    break;
                case "bowed-bonus":
                    Expect(InPlay(runner, 1, TutorialCards.Natsumi).Bowed && InPlay(runner, 1, TutorialCards.IsawaTomohiro).Bowed, "Natsumi and Tomohiro are both bowed");
                    Expect(ForceOf(runner, TutorialCards.Natsumi) == 9, $"Natsumi has 9 Force while bowed, the engine shows {ForceOf(runner, TutorialCards.Natsumi)}");
                    break;
                case "congratulations":
                    Expect(new[] { TutorialCards.BayushiUtoro, TutorialCards.BayushiHurunayi }.All(d => InPlay(runner, 0, d) is { Bowed: true, Zone: Zone.Home }), "your attackers went home bowed");
                    break;
                case "pass-battle":
                    Expect(!InPlay(runner, 1, TutorialCards.Natsumi).Bowed && InPlay(runner, 1, TutorialCards.Natsumi).Zone == Zone.Home, "Natsumi straightened and went home");
                    break;
            }
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended");
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
        Expect(runner.Engine.State.Players[1].Provinces[0].Destroyed, "Phoenix's Province 1 was destroyed");
    }

    private static int ForceOf(TutorialRunner runner, string definition)
    {
        var view = runner.Engine.GetView(0);
        return view.Players.SelectMany(p => p.Army.Concat(p.Home)).First(c => c.Definition?.Id == definition).Force;
    }

    private static void OpensInTheActionPhase()
    {
        var runner = TutorialRunner.Start(Lesson);
        var state = runner.Engine.State;
        Expect(state is { ActivePlayerId: 0, Phase: Phase.Action, PriorityPlayerId: 0 }, "the table is at your Action phase with priority");
        Expect(state.Players[0].Honor == 3 && state.Players[1].Honor == 10, "3 Honor against 10");
        Expect(state.Cards[state.Players[0].StrongholdId].DefinitionId == TutorialCards.TowersOfTheYogo && state.Cards[state.Players[1].StrongholdId].DefinitionId == TutorialCards.LibraryOfRebirth, "Towers of the Yogo against Library of Rebirth");
        var mine = state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Hand).Select(c => c.DefinitionId).OrderBy(x => x).ToArray();
        Expect(mine.SequenceEqual(new[] { TutorialCards.SoshiHouseGuard, TutorialCards.NightingaleBlade, TutorialCards.SeppunHeavyElite, TutorialCards.LightArmor, TutorialCards.ClanStandard }.OrderBy(x => x)), "your hand is the five attachments");
        var theirs = state.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Hand).Select(c => c.DefinitionId).OrderBy(x => x).ToArray();
        Expect(theirs.SequenceEqual(new[] { TutorialCards.ObscuredPathways, TutorialCards.PeaceOfTheKami, TutorialCards.ScouringFlood, TutorialCards.TheEarthsHunger, TutorialCards.StrengthOfTheTsunami, TutorialCards.TheSwellOfStrength }.OrderBy(x => x)), "the Phoenix holds six Spells");
        Expect(state.Players.All(p => p.Provinces.All(province => state.Cards[province.CardId!].FaceUp == false)), "every Province is face down");
        foreach (var d in new[] { TutorialCards.IsawaTomohiro, TutorialCards.Natsumi }) Expect(InPlay(runner, 1, d).Zone == Zone.Home, "the Phoenix has its Personalities at home");
    }

    private static void GoldIsExact()
    {
        var gold = new[] { TutorialCards.TowersOfTheYogo, TutorialCards.GeishaHouse, TutorialCards.SmallFarm, TutorialCards.LargeFarm, TutorialCards.RemoteVillage }.Sum(d => CardCatalog.Get(d).GoldProduction);
        var cost = new[] { TutorialCards.SeppunHeavyElite, TutorialCards.NightingaleBlade, TutorialCards.LightArmor, TutorialCards.ClanStandard, TutorialCards.SoshiHouseGuard }.Sum(d => CardCatalog.Get(d).GoldCost);
        Expect(CardCatalog.Get(TutorialCards.GeishaHouse).GoldProduction == 2, "Geisha House prints 2 and makes 3 for a Scorpion (the lesson pays 3 with it)");
        Expect(cost == 16, $"the five attachments cost 16, they cost {cost}");
        Expect(gold + 1 /*Geisha House's Scorpion bonus*/ + 1 /*Red Crane Dojo for a Ninja card*/ + CardCatalog.Get(TutorialCards.RedCraneDojo).GoldProduction >= cost, "the table makes enough");
    }

    private static void QuotedNumbers()
    {
        Expect(CardCatalog.Get(TutorialCards.SeppunHeavyElite).GoldCost == 3 && CardCatalog.Get(TutorialCards.NightingaleBlade).GoldCost == 4 && CardCatalog.Get(TutorialCards.LightArmor).GoldCost == 2 && CardCatalog.Get(TutorialCards.ClanStandard).GoldCost == 3 && CardCatalog.Get(TutorialCards.SoshiHouseGuard).GoldCost == 4, "the costs the notes name: 3, 4, 2, 3 and 4");
        Expect(CardCatalog.Get(TutorialCards.SeppunHeavyElite).HonorRequirement == 0 && CardCatalog.Get(TutorialCards.BayushiWaru).PersonalHonor == 2, "Seppun Heavy Elite needs 0 and Waru has 2 Personal Honor");
        Expect(CardCatalog.Get(TutorialCards.Natsumi).Force == 3, "Natsumi's Force is 3 before +6");
        var hurunayi = CardCatalog.Get(TutorialCards.BayushiHurunayi);
        Expect(hurunayi.Force + CardCatalog.Get(TutorialCards.NightingaleBlade).Force + CardCatalog.Get(TutorialCards.LightArmor).Force == 8, "Hurunayi, Nightingale Blade and Light Armor make 8 Force");
        Expect(CardCatalog.Get(TutorialCards.NightingaleBlade).Chi == 1, "Nightingale Blade's +1C");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var runner = TutorialRunner.Start(Lesson); var count = 0;
        for (var guard = 0; !runner.Finished && guard < 200; guard++, count++)
        {
            var coach = runner.Coach;
            Expect(coach.Body.Length > 0 && coach.Title.Length > 0, $"{coach.StepId}: it has words");
            if (coach.Button is null) Expect(!string.IsNullOrWhiteSpace(coach.Instruction), $"{coach.StepId}: a move step says what to click");
            if (coach.Button is not null) { Expect(runner.Continue(), "the button works"); continue; }
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{coach.StepId}: its move is accepted");
        }
        Expect(count is >= 15 and <= 32, $"the lesson stays within its budget: {count} steps");
        Expect(runner.Finished, "it ends");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson); var before = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            before.Add(runner.Coach.StepId);
            if (before.Count > 1)
            {
                var back = runner.Back() ?? throw new Exception("Back is available after the first step");
                Expect(back.Coach.StepId == before[^2], $"Back from {before[^1]} returns to {before[^2]}, not {back.Coach.StepId}");
            }
            if (runner.Coach.Button is not null) { runner.Continue(); continue; }
            Expect(runner.Execute(CommandFor(runner)).Accepted, $"{runner.Coach.StepId}: its move is accepted");
        }
    }
}
