using L5R.Core;
using B = Batch336Support;

// Issue #1294: an action that ends in a deck search counts toward the Rings' entry conditions like any other, and a Battle action is refused (not a crash) between battles.
internal static class DeferredActionCountTests
{
    private const string MartialInstruction = "oracle-l5r-4843", SteadfastDefense = "oracle-l5r-7477", NerveStrike = "oracle-l5r-5532", RingOfAir = "oracle-l5r-6333";

    public static void Run(TestRunner runner) => runner.Run("deferred-action-counts", [
        ("Martial Instruction's search, once over, counts as a resolved Kiho action toward Ring of Air", MartialInstructionCountsForAir),
        ("Between battles a Battle action is refused with a reason instead of crashing the action query", BattleActionBetweenBattles),
    ]);

    private static void MartialInstructionCountsForAir()
    {
        var s = B.Table(Phase.Action, 0);
        var monk = B.Add(s, "training-monk", 0, Zone.Home);
        var strategy = B.Add(s, MartialInstruction, 0, Zone.Hand);
        var deck = B.Add(s, SteadfastDefense, 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, deck.Id);
        s.Players[0].SpellKihoActionsStamp = s.TurnNumber.ToString();
        s.Players[0].SpellKihoActionTitles = ["Spinning Heel Kick", "Nerve Strike"]; // two Kiho of different titles already resolved this turn
        B.Add(s, RingOfAir, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var r = e.Execute(B.Enabled(e, 0, "martial-instruction-search", strategy.Id, monk.Id).Command!);
        B.Check(r.Accepted, r.Error ?? "");
        B.Eq(e.State.Players[0].SpellKihoActionTitles.Count, 2, "nothing is counted while the search is still open");
        r = e.Execute(new GameCommand("selectSearchCard", 0, e.Version, Choice: SteadfastDefense));
        B.Check(r.Accepted, r.Error ?? "");
        B.Check(e.State.Players[0].SpellKihoActionTitles.Contains("Martial Instruction"), "the resolved Martial Instruction is the third Kiho title");
        B.Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is { } ids && ids.Any(id => e.State.Cards[id].DefinitionId == RingOfAir)), "Ring of Air's put-into-play is granted");
    }

    private static void BattleActionBetweenBattles()
    {
        var s = B.Table(Phase.Battle, 0);
        s.CurrentBattleProvince = null;
        B.Add(s, "training-monk", 0, Zone.Home);
        var strike = B.Add(s, NerveStrike, 0, Zone.Hand);
        B.Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var options = e.QueryActions(0).Where(a => a.CardId == strike.Id).ToArray();
        B.Check(options.All(a => !a.Enabled), "Nerve Strike cannot be taken with no battle in progress");
    }
}
