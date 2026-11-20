using L5R.Core;

// A once-per-turn ability a card has used is public: both players watched it. The view says so on the card for either seat, so a person (and a bot) can see which printed abilities a card still has this turn.
internal static class SpentAbilityViewTests
{
    public static void Run(TestRunner runner) => runner.Run("spent-ability-view", [
        ("Printed paragraph aliases and traits do not invent extra numbered actions", PrintedParagraphs),
        ("Printed action status covers public zones, limits, shared keys, extra uses and hidden cards", ProjectionLimits),
        ("Unsafe Passage clears the printed action marker during its live grant", UnsafeWaiver),
        ("A card's used-up printed abilities show in both seats' views this turn and clear on the next", SpentShowsToBothSeats),
    ]);

    private static void PrintedParagraphs()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var sakataDef = CardCatalog.Cards.Values.First(d => d.Abilities?.Contains("sakata-fire-token") == true);
        var sakata = Batch336Support.Add(s, sakataDef.Id, 0, Zone.Home);
        sakata.AbilityUses["sakata-token"] = s.TurnNumber;
        foreach (var seat in new[] { 0, 1 })
        {
            var view = MatchEngine.FromTestState(s).GetView(seat).Players[0].Home.Single(c => c.Id == sakata.Id);
            Batch336Support.Check(view.PrintedActions is { Length: 2 } && view.PrintedActions[0].AbilityIds.SequenceEqual(new[] { "sakata-fire-token", "sakata-sake-token" }) && view.PrintedActions[0].Spent && !view.PrintedActions[1].Spent, "Sakata has two printed actions, not three engine choices");
            Batch336Support.Check(view.SpentAbilityIds!.Contains("sakata-fire-token") && view.SpentAbilityIds.Contains("sakata-sake-token"), "legacy branch IDs remain present");
        }
        var stronghold = s.Cards[s.Players[0].StrongholdId]; stronghold.DefinitionId = "oracle-l5r-4646";
        var otomo = MatchEngine.FromTestState(s).GetView(0).Players[0].Stronghold;
        Batch336Support.Check(otomo.PrintedActions is { Length: 1 } && otomo.PrintedActions[0].AbilityIds.Contains("otomo-courtier-token"), "Otomo's trait is not a numbered action");
    }
    private static void ProjectionLimits()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Stronghold && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && !a.OncePerGame && a.UsesPerTurn == 1) == true);
        var card = s.Cards[s.Players[0].StrongholdId]; card.DefinitionId = def.Id;
        var id = def.Abilities!.First(id => AbilityCatalog.All.TryGetValue(id, out var a) && !a.OncePerGame && a.UsesPerTurn == 1);
        var key = AbilityCatalog.All[id].UsageKey ?? id;
        card.AbilityUses[key] = s.TurnNumber;
        foreach (var seat in new[] { 0, 1 })
        {
            var row = MatchEngine.FromTestState(s).GetView(seat).Players[0].Stronghold;
            Batch336Support.Check(row.PrintedActions!.Single(a => a.AbilityIds.Contains(id)).Spent, "Stronghold status is public");
            var json = System.Text.Json.JsonSerializer.Serialize(row);
            Batch336Support.Check(System.Text.Json.JsonSerializer.Serialize(System.Text.Json.JsonSerializer.Deserialize<CardView>(json)!.PrintedActions) == System.Text.Json.JsonSerializer.Serialize(row.PrintedActions), "wire status round trips");
        }
        card.AbilityUses[MatchEngine.SecondUseKey] = s.TurnNumber;
        Batch336Support.Check(!MatchEngine.FromTestState(s).GetView(0).Players[0].Stronghold.PrintedActions!.Single(a => a.AbilityIds.Contains(id)).Spent, "an additional use clears in the same turn");
        var once = CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.OncePerGame) == true);
        var holding = Batch336Support.Add(s, once.Id, 0, Zone.Home);
        var onceId = once.Abilities!.First(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.OncePerGame);
        holding.AbilityUses[AbilityCatalog.All[onceId].UsageKey ?? onceId] = s.TurnNumber - 1;
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Players[0].Home.Single(c => c.Id == holding.Id).PrintedActions!.Single(a => a.AbilityIds.Contains(onceId)).Spent, "once-per-game does not reset with the turn");
        var repeat = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.UsesPerTurn == AbilityUseLimit.Unlimited && !a.OncePerGame && a.UsesPer is null) == true);
        var repeated = Batch336Support.Add(s, repeat.Id, 0, Zone.Home);
        foreach (var ability in repeat.Abilities!) repeated.AbilityUses[AbilityCatalog.All[ability].UsageKey ?? ability] = s.TurnNumber;
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Players[0].Home.Single(c => c.Id == repeated.Id).PrintedActions!.Where(a => AbilityCatalog.All[a.AbilityIds[0]].UsesPerTurn == 0).All(a => !a.Spent), "Repeatable remains unspent");
        repeated.FaceUp = false;
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Players[0].Home.Single(c => c.Id == repeated.Id).PrintedActions is null, "face-down status is absent");
        repeated.Zone = Zone.Hand; repeated.FaceUp = true; s.Players[0].Hand.Add(repeated.Id);
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Players[0].Hand.Single(c => c.Id == repeated.Id).PrintedActions is null, "owner hand status is absent");
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(1).Players[0].Hand.All(c => c.Id != repeated.Id && c.PrintedActions is null), "opponent hand has neither identity nor status");
    }
    private static void UnsafeWaiver()
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.CurrentBattleProvince = 0;
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Follower && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Battle" && a.UsesPerTurn == 1 && !a.OncePerGame) == true);
        var hostDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var host = Batch336Support.Add(s, hostDef.Id, 0, Zone.Battlefield); host.ProvinceIndex = 0;
        var card = Batch336Support.Add(s, def.Id, 0, Zone.Attached); card.AttachedTo = host.Id;
        var id = def.Abilities!.First(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Battle" && a.UsesPerTurn == 1 && !a.OncePerGame);
        card.AbilityUses[AbilityCatalog.All[id].UsageKey ?? id] = s.TurnNumber;
        bool Spent() => MatchEngine.FromTestState(s).GetView(1).Players[0].Army.Single(c => c.Id == card.Id).PrintedActions!.Single(a => a.AbilityIds.Contains(id)).Spent;
        Batch336Support.Check(Spent(), "attached action begins spent");
        s.AdditionalActions.Add(new(0, [card.Id], Battle: 0, Tag: "tfsb-unsafe-passage"));
        Batch336Support.Check(!Spent(), "live Unsafe Passage use clears the marker");
        s.AdditionalActions.Clear(); Batch336Support.Check(Spent(), "consuming the grant restores the marker");
    }
    private static void SpentShowsToBothSeats()
    {
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Battle" && a.UsesPerTurn == 1) == true);
        var id = def.Abilities!.First(i => AbilityCatalog.All.TryGetValue(i, out var a) && a.Timing == "Battle" && a.UsesPerTurn == 1);
        var s = Batch336Support.Table(Phase.Action, 0);
        var card = Batch336Support.Add(s, def.Id, 1, Zone.Home);
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Players[1].Home.First(c => c.Id == card.Id).SpentAbilityIds is null, "a card that has used nothing lists nothing");
        s.Cards[card.Id].AbilityUses[AbilityCatalog.All[id].UsageKey ?? id] = s.TurnNumber;
        var e = MatchEngine.FromTestState(s);
        foreach (var seat in new[] { 0, 1 })
            Batch336Support.Check(e.GetView(seat).Players[1].Home.First(c => c.Id == card.Id).SpentAbilityIds?.Contains(id) == true, $"seat {seat} sees {id} as spent");
        s.TurnNumber++;
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Players[1].Home.First(c => c.Id == card.Id).SpentAbilityIds is null, "the next turn it is available again");
    }
}
