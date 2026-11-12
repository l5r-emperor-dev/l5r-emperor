using L5R.Core;

// A once-per-turn ability a card has used is public: both players watched it. The view says so on the card for either seat, so a person (and a bot) can see which printed abilities a card still has this turn.
internal static class SpentAbilityViewTests
{
    public static void Run(TestRunner runner) => runner.Run("spent-ability-view", [
        ("A card's used-up printed abilities show in both seats' views this turn and clear on the next", SpentShowsToBothSeats),
    ]);

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
