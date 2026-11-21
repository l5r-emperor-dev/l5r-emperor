using L5R.Core;
using static Batch336Support;
using static RegressionAssertions;

// Conquest once per turn (issue #1147, ruling R-091): the rulebook Conquest ability is not Repeatable, so it is used once per turn across every battlefield. Once a Conqueror is kept unbowed, no
// other Conqueror is that turn. The Plain of Glass's two uses are both in the same battle.
// Position: the economy table, player 0 attacks player 0's opponent at provinces 0 and 1 with one Conqueror at each, against an empty defence.
internal static class ConquestPerTurnTests
{
    private const string PlainOfGlass = "oracle-l5r-8260";

    public static void Run(TestRunner runner) => runner.Run("conquest-per-turn", [
        ("A Conqueror kept unbowed at the first battle leaves none for a later battle that turn; a turn with no Conquest used still has it at the second battle", OncePerTurn),
        ("The Plain of Glass: its two Conquerors at the first battle spend the turn's Conquest, so a third at the next battle bows", PlainOfGlassSpendsTheTurn),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    private static MatchState TwoBattles()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = 0;
        s.UnresolvedBattlefields = [0, 1]; s.BattleNumber = 1;
        return s;
    }

    private static CardInstance Conqueror(MatchState s, int province) => Batch336Support.Add(s, "training-conqueror", 0, Zone.Battlefield, false, province, null);

    // Both players pass: the current battle resolves; then the next battle is chosen.
    private static void Resolve(MatchEngine e)
    {
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
    }

    private static void NextBattle(MatchEngine e, int province)
    {
        Eq(e.State.Phase, Phase.BattleSelection, "the first battle is over");
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: province));
    }

    private static void OncePerTurn()
    {
        var s = TwoBattles();
        var first = Conqueror(s, 0);
        var second = Conqueror(s, 1);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Check(!e.State.Cards[first.Id].Bowed && e.State.Cards[first.Id].Zone == Zone.Home, "the first battle's Conqueror returned home unbowed");
        NextBattle(e, 1);
        Resolve(e);
        Check(e.State.Cards[second.Id].Bowed, "the second battle's Conqueror bowed: the turn's Conquest was spent");

        // No Conqueror at the first battle: the Conquest is still there for the second.
        var t = TwoBattles();
        Batch336Support.Add(t, "training-lion-samurai", 0, Zone.Battlefield, false, 0, null);
        var only = Conqueror(t, 1);
        var f = MatchEngine.FromTestState(t);
        Resolve(f);
        NextBattle(f, 1);
        Resolve(f);
        Check(!f.State.Cards[only.Id].Bowed, "with no Conquest used yet, the second battle's Conqueror is kept unbowed");
    }

    private static void PlainOfGlassSpendsTheTurn()
    {
        var s = TwoBattles();
        s.Cards[s.Players[0].StrongholdId].DefinitionId = PlainOfGlass;
        var a = Conqueror(s, 0);
        var b = Conqueror(s, 0);
        var c = Conqueror(s, 1);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Check(!e.State.Cards[a.Id].Bowed && !e.State.Cards[b.Id].Bowed, "both Conquerors of the first battle were kept unbowed");
        NextBattle(e, 1);
        Resolve(e);
        Check(e.State.Cards[c.Id].Bowed, "the next battle's Conqueror bowed");
    }
}
