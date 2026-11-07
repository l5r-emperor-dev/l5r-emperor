using L5R.AI;
using L5R.Core;

// Issue #1185: the planning bot's turn plan. See TrainingOpponent.Planner.cs.
internal static class PlannerTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Planner (issue #1185): the purchase slate respects the shape of the Gold (a 5 and a 3 cannot pay for two 4-cost cards), and the plan is rebuilt at the start of a turn, when the phase moves on and when the table changes, but not when nothing did", SlateAndRebuilds),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static MatchView Table(Phase phase, int turn, int enemyForce)
    {
        var personality = BattlePolicyTests.Person(4) with { Clan = "Unaligned", GoldCost = 4 };
        var holding = CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding) with { GoldProduction = 3, Effect = "", GoldCost = 2 };
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        CardView Row(string id) => new(id, personality, 0, Zone.Province, true, false, null, null, personality.Force);
        var stronghold = view.Players[0].Stronghold with { Definition = view.Players[0].Stronghold.Definition! with { GoldProduction = 5, Effect = "" } };
        var provinces = view.Players[0].Provinces.Select((p, i) => i == 0 ? p with { Card = Row("A") } : i == 1 ? p with { Card = Row("B") } : p with { Card = null }).ToArray();
        var home = new[] { new CardView("hold", holding, 0, Zone.Home, true, false, null, null, 0) };
        var foes = enemyForce == 0 ? [] : new[] { new CardView("foe", BattlePolicyTests.Person(enemyForce), 1, Zone.Home, true, false, null, null, enemyForce) };
        var self = view.Players[0] with { Stronghold = stronghold, Provinces = provinces, Home = home, Army = [], Hand = [], HandCount = 0, TurnsTaken = 5 };
        return view with { Phase = phase, TurnNumber = turn, PendingChoice = null, ActivePlayerId = 0, PriorityPlayerId = 0, Players = [self, view.Players[1] with { Home = foes, Army = [] }] };
    }

    private static ActionOption Buy(MatchView v, string id) =>
        new("purchase", "Buy " + id, true, null, new("purchase", 0, v.Version, id, ["hold"]), id, null, Gold: new GoldQuote(4, "Printed", "", null, []));

    private static void SlateAndRebuilds()
    {
        var memory = new BotContext { UsePlanner = true };
        var dynasty = Table(Phase.Dynasty, 5, 0);
        TrainingOpponent.Choose(dynasty, [Buy(dynasty, "A"), Buy(dynasty, "B"), BattlePolicyTests.Pass(dynasty)], memory);
        var first = memory.PlanLog.Single();
        // Sources are 5 (Stronghold) and 3 (Holding): one 4-cost card fits the 5, the other cannot be paid from the 3, though the total (8) would cover both.
        Check(first.Contains("[5+3]"), "the plan names the Gold and its shape: " + first);
        var inSlate = new[] { "A", "B" }.Count(id => first.Contains("slate [") && first.Split("slate [")[1].Split(']')[0].Split(',').Contains(id));
        Check(inSlate == 1, "only one of two 4-cost cards fits Gold of 5 and 3: " + first);

        TrainingOpponent.Choose(dynasty, [Buy(dynasty, "A"), BattlePolicyTests.Pass(dynasty)], memory);
        Check(memory.PlanLog.Count == 1, "the same table does not make a new plan");

        var attack = Table(Phase.AttackDeclaration, 5, 0);
        TrainingOpponent.Choose(attack, [BattlePolicyTests.Pass(attack)], memory);
        Check(memory.PlanLog.Count == 2 && memory.PlanLog[1].Contains("phase attack"), "the attack phase beginning re-thinks the plan: " + string.Join(" | ", memory.PlanLog));

        var stronger = Table(Phase.AttackDeclaration, 5, 9);
        TrainingOpponent.Choose(stronger, [BattlePolicyTests.Pass(stronger)], memory);
        Check(memory.PlanLog.Count == 3 && memory.PlanLog[2].Contains("table changed"), "an enemy army appearing re-thinks the plan: " + string.Join(" | ", memory.PlanLog));

        var next = Table(Phase.Action, 6, 0);
        TrainingOpponent.Choose(next, [BattlePolicyTests.Pass(next)], memory);
        Check(memory.PlanLog.Count == 4 && memory.PlanLog[3].Contains("turn start"), "a new turn makes a new plan: " + string.Join(" | ", memory.PlanLog));

        var champion = new BotContext();
        TrainingOpponent.Choose(dynasty, [Buy(dynasty, "A"), BattlePolicyTests.Pass(dynasty)], champion);
        Check(champion.PlanLog.Count == 0, "with the planner off nothing is planned");
    }
}
