using L5R.AI;
using L5R.Core;

// Issue #1185: the planning bot's turn plan. See TrainingOpponent.Planner.cs.
internal static class PlannerTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Planner (issue #1185): the purchase slate respects the shape of the Gold (a 5 and a 3 cannot pay for two 4-cost cards), and the plan is rebuilt at the start of a turn, when the phase moves on and when the table changes, but not when nothing did", SlateAndRebuilds),
        ("Planner (issue #1185, slice 2): the attack decision is a swing estimate (my Force plus what my hand adds, against theirs plus a guessed enemy hand), so the planner attacks a battle it expects to win that the champion's printed-Force rule passes on, holds when a full enemy hand makes it risky, and counts a +Force card in hand", SwingAttack),
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

    // Attack declaration: my Personality (Force `own`) against one enemy Personality (Force 4) and Provinces of Strength 6, an enemy hand of `enemyHand` cards, and `hand` in mine.
    private static MatchView AttackTable(int own, int enemyHand, params CardView[] hand)
    {
        var view = Table(Phase.AttackDeclaration, 5, 4);
        var mine = new CardView("unit", BattlePolicy_Person(own), 0, Zone.Home, true, false, null, null, own);
        var provinces = view.Players[1].Provinces.Select(p => p with { Strength = 6, Destroyed = false, Card = null }).ToArray();
        return view with { Players = [view.Players[0] with { Home = view.Players[0].Home.Append(mine).ToArray(), Hand = hand, HandCount = hand.Length },
            view.Players[1] with { Provinces = provinces, HandCount = enemyHand }] };
    }
    private static CardDefinition BattlePolicy_Person(int force) => BattlePolicyTests.Person(force);

    private static string Declare(MatchView v, BotContext memory)
    {
        var declare = new ActionOption("declareAttack", "Declare an attack", true, null);
        return TrainingOpponent.Choose(v, [declare, BattlePolicyTests.Pass(v)], memory).Command?.Type ?? "none";
    }

    private static void SwingAttack()
    {
        var plan = () => new BotContext { UsePlanner = true };
        // Force 5 against Force 4 and no enemy hand: the battle is won (net +1) though no Province falls (Strength 6). The champion needs to beat Strength AND defenders, so it passes.
        Check(Declare(AttackTable(5, 0), new BotContext()) == "pass", "the champion does not attack a battle it cannot raze");
        var calm = plan();
        Check(Declare(AttackTable(5, 0), calm) == "declareAttack", "the planner attacks a battle it expects to win: " + string.Join(" | ", calm.PlanLog));
        // The same board with five cards in the enemy hand: their guessed tricks swing it back (a hand that big is a risk), so the planner holds.
        var risky = plan();
        Check(Declare(AttackTable(5, 5), risky) == "pass", "a full enemy hand makes the same attack too risky: " + string.Join(" | ", risky.PlanLog));
        // My own hand counts: Force 3 against 4 loses, but a +3 Force card in hand wins it.
        var bonus = CardCatalog.Cards.Values.First(d => d.Effect == "force-three");
        var card = new CardView("boost", bonus, 0, Zone.Hand, true, false, null, null, 0);
        Check(Declare(AttackTable(3, 0), plan()) == "pass", "Force 3 against 4 with nothing in hand does not attack");
        var boosted = plan();
        Check(Declare(AttackTable(3, 0, card), boosted) == "declareAttack", "a +3 Force card in hand turns it into a win: " + string.Join(" | ", boosted.PlanLog));
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
