using L5R.AI;
using L5R.Core;

// Issue #1185: the planning bot's turn plan. See TrainingOpponent.Planner.cs.
internal static class PlannerTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Planner (issue #1185): the purchase slate respects the shape of the Gold (a 5 and a 3 cannot pay for two 4-cost cards), and the plan is rebuilt at the start of a turn, when the phase moves on and when the table changes, but not when nothing did", SlateAndRebuilds),
        ("Planner (issue #1185, slice 2): the attack decision is a swing estimate (my Force plus what my hand adds, against theirs plus a guessed enemy hand), so the planner attacks a battle it expects to win that the champion's printed-Force rule passes on, holds when a full enemy hand makes it risky, and counts a +Force card in hand", SwingAttack),
        ("Planner (issue #1185): the end-of-Dynasty discard keeps what can be bought next turn (information plans the turn) and discards only a card too dear to pay for, or a non-Personality when behind in Personalities with none on offer", DiscardRule),
        ("Research arms (issue #1185): every switch reaches the context and the Contender has them all off", ResearchSwitches),
        ("R1 (issue #1185): discard v2 keeps what the Gold can buy together next turn and flushes the rest, where the first rule kept every card affordable alone", Rec1DiscardTogether),
        ("R2 (issue #1185): the attack estimate can be split into its halves: add-only never cancels the champion's attack, veto-only never adds one", Rec2AttackHalves),
        ("R3 (issue #1185): once the opponent has 30 Honor an attack is made only when the estimate wins by a margin", Rec3HonorGuard),
        ("R4 (issue #1185): the opponent's only possible defender is bowed in the Action phase when the attack then wins, and nothing changes with two defenders", Rec4PresenceDenial),
        ("R5 (issue #1185): no Gold or card is spent on a battle the whole hand cannot turn, and the last Province is defended with everything", Rec5NoSpendLost),
        ("R7 (issue #1185): on the first turn two Holdings are planned ahead of a Personality the Stronghold alone cannot pay for", Rec7OpenHoldings),
        ("Contender (issue #1185): the named configuration keeps the harmless planner and triage parts on, leaves the parts that lost games off (each switchable), and behaves as the champion does on attacks until the estimate is switched on", ContenderConfiguration),
        ("Planner (issue #1185): when the Gold can pay for the whole province row the plan buys it all, and Gold a likely attack will need for Battle tricks is held back from the purchases", ClearTheRowAndReserve),
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
        return view with
        {
            Players = [view.Players[0] with { Home = view.Players[0].Home.Append(mine).ToArray(), Hand = hand, HandCount = hand.Length },
            view.Players[1] with { Provinces = provinces, HandCount = enemyHand }]
        };
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

    // End of the Dynasty phase with `row` in the provinces (capacity: a 5 and a 3 = 8 Gold). Returns what the planner does when offered each card's discard or ending the phase.
    private static string AtDiscard(CardView[] row, int ownBodies, int turnsTaken = 5)
    {
        var view = Table(Phase.DynastyDiscard, 5, 4); // one enemy Personality
        var provinces = view.Players[0].Provinces.Select((p, i) => p with { Card = i < row.Length ? row[i] : null }).ToArray();
        var home = view.Players[0].Home.Concat(Enumerable.Range(0, ownBodies).Select(i => new CardView("own" + i, BattlePolicyTests.Person(3), 0, Zone.Home, true, false, null, null, 3))).ToArray();
        var v = view with { Players = [view.Players[0] with { Provinces = provinces, Home = home, TurnsTaken = turnsTaken }, view.Players[1]] };
        var actions = row.Select(c => new ActionOption("discardProvince", "Discard " + c.Id, true, null, null, c.Id)).Append(new ActionOption("endDynasty", "End", true, null)).ToArray();
        var command = TrainingOpponent.Choose(v, actions, new BotContext { UsePlanner = true }).Command;
        return command?.Type == "discardProvince" ? "discard " + command.CardId : command?.Type ?? "none";
    }

    private static CardView Card(string id, CardType type, int cost, int production = 0)
    {
        var basis = type switch { CardType.Holding => CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding), CardType.Event => CardCatalog.Cards.Values.First(d => d.Type == CardType.Event), _ => BattlePolicyTests.Person(4) };
        var def = basis with { Clan = "Unaligned", GoldCost = cost, GoldProduction = production, Effect = "" };
        return new(id, def, 0, Zone.Province, true, false, null, null, def.Force);
    }

    private static string SlateFor(MatchView v, BotContext memory)
    {
        TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v)], memory);
        return memory.PlanLog[^1].Split("slate [")[1].Split(']')[0];
    }

    private static MatchView DynastyRow(CardView[] row, params CardView[] hand)
    {
        var view = Table(Phase.Dynasty, 5, hand.Length > 0 ? 9 : 0);
        var provinces = view.Players[0].Provinces.Select((p, i) => p with { Strength = 4, Card = i < row.Length ? row[i] : null }).ToArray();
        return view with { Players = [view.Players[0] with { Provinces = provinces, Hand = hand, HandCount = hand.Length }, view.Players[1]] };
    }

    private static void DiscardRule()
    {
        Check(AtDiscard([Card("P1", CardType.Personality, 4), Card("P2", CardType.Personality, 5)], 0) == "endDynasty", "two affordable Personalities are kept, even when behind: they plan the next turn");
        Check(AtDiscard([Card("P1", CardType.Personality, 4), Card("H1", CardType.Holding, 3, 2)], 0) == "endDynasty", "a Personality and a Holding are both kept");
        Check(AtDiscard([Card("P1", CardType.Personality, 4), Card("P9", CardType.Personality, 20)], 0) == "discard P9", "a card too dear for all of the bot's production is the one it discards");
        Check(AtDiscard([Card("H1", CardType.Holding, 3, 2), Card("H2", CardType.Holding, 4, 2)], 0).StartsWith("discard"), "behind in Personalities with only Holdings on offer: it digs for the Personalities it needs");
        Check(AtDiscard([Card("H1", CardType.Holding, 3, 2), Card("H2", CardType.Holding, 4, 2)], 3) == "endDynasty", "ahead in Personalities, affordable Holdings are kept");
        // A 4-cost Holding that would make it forgo an affordable 5-cost Personality (5 + 4 is more than the 8 it makes): discarded early, or later when digging; kept later when not behind.
        CardView[] blocking = [Card("P1", CardType.Personality, 5), Card("H4", CardType.Holding, 4, 2)];
        Check(AtDiscard(blocking, 3, turnsTaken: 2) == "discard H4", "early in the game the Holding that blocks a Personality is discarded, though the bot is not behind");
        Check(AtDiscard(blocking, 0, turnsTaken: 6) == "discard H4", "mid game and digging for Personalities, the blocking Holding is discarded");
        Check(AtDiscard(blocking, 3, turnsTaken: 6) == "endDynasty", "mid game and not behind, the same Holding is kept");
    }

    private static void ClearTheRowAndReserve()
    {
        // The Gold (a 5 and a 3) can pay for the whole row, including a card the value scoring would pass over (an Event): the plan buys it all rather than leave anything to discard.
        var row = new[] { Card("P1", CardType.Personality, 4), Card("E1", CardType.Event, 2) };
        var slate = SlateFor(DynastyRow(row), new BotContext { UsePlanner = true });
        Check(slate.Split(',').Contains("P1") && slate.Split(',').Contains("E1"), "the whole row is bought when the Gold's shape can pay for it: [" + slate + "]");

        // The opponent has a Force 9 army against Provinces of Strength 4 and the bot holds a 2-Gold Battle trick: its 3 stays untapped for their turn, so the 5 and 3 no longer pay for both cards.
        var boost = CardCatalog.Cards.Values.First(d => d.Effect == "force-three") with { GoldCost = 2 };
        var trick = new CardView("boost", boost, 0, Zone.Hand, true, false, null, null, 0);
        var pair = new[] { Card("P1", CardType.Personality, 5), Card("P2", CardType.Personality, 3) };
        Check(SlateFor(DynastyRow(pair), new BotContext { UsePlanner = true }).Split(',').Length == 2, "with nothing to hold Gold for, both Personalities are bought");
        var held = SlateFor(DynastyRow(pair, trick), new BotContext { UsePlanner = true });
        Check(held.Split(',').Length == 1, "a likely attack and a trick in hand: Gold is held back, so only one Personality is planned: [" + held + "]");
    }

    private static void ContenderConfiguration()
    {
        var contender = BotContext.Contender();
        Check(contender.UsePlanner && contender.UsePlannerTriage && !contender.PlannerAttack && !contender.PlannerDiscard && !contender.PlannerReserve && !contender.TriageWonCap, "the Contender keeps the harmless parts and leaves the parts that lost games off");
        Check(BotContext.Contender(attack: true).PlannerAttack && BotContext.Contender(discard: true).PlannerDiscard && BotContext.Contender(reserve: true).PlannerReserve && BotContext.Contender(wonCap: true).TriageWonCap, "each experimental part switches on by itself");
        // With the attack estimate off, the Contender attacks as the champion does (a battle it wins but cannot raze is passed on); with it on, it declares.
        Check(Declare(AttackTable(5, 0), BotContext.Contender()) == "pass", "Contender: the champion's attack rule applies while the estimate is off");
        Check(Declare(AttackTable(5, 0), BotContext.Contender(attack: true)) == "declareAttack", "Contender+attack: the swing estimate declares the battle it expects to win");
        // The purchase slate is on: a plan is made at the Dynasty phase.
        var memory = BotContext.Contender(); var dynasty = Table(Phase.Dynasty, 5, 0);
        TrainingOpponent.Choose(dynasty, [Buy(dynasty, "A"), Buy(dynasty, "B"), BattlePolicyTests.Pass(dynasty)], memory);
        Check(memory.PlanLog.Count == 1, "the Contender plans its purchases");
    }

    // ---- research arms (docs/bots/experiments/2026-11-08-research-arms.md) ----

    private static MatchView WithEnemyHonor(MatchView v, int honor) => v with { Players = [v.Players[0], v.Players[1] with { Honor = honor }] };

    private static string DiscardWith(BotContext memory, CardView[] row)
    {
        var view = Table(Phase.DynastyDiscard, 5, 4);
        var provinces = view.Players[0].Provinces.Select((p, i) => p with { Card = i < row.Length ? row[i] : null }).ToArray();
        var v = view with { Players = [view.Players[0] with { Provinces = provinces }, view.Players[1]] };
        var actions = row.Select(c => new ActionOption("discardProvince", "Discard " + c.Id, true, null, null, c.Id)).Append(new ActionOption("endDynasty", "End", true, null)).ToArray();
        var command = TrainingOpponent.Choose(v, actions, memory).Command;
        return command?.Type == "discardProvince" ? "discard " + command.CardId : command?.Type ?? "none";
    }

    private static void ResearchSwitches()
    {
        var c = BotContext.Contender(discard2: true, addOnly: true, honorGuard: true, presence: true, noSpendLost: true, openHoldings: true, proclaimBoost: true);
        Check(c.PlannerDiscard2 && c.AttackAddOnly && c.PlannerAttack && c.HonorGuard && c.PresenceDenial && c.NoSpendLost && c.OpenHoldings && c.ProclaimBoost, "each research switch reaches the context (add-only implies the attack estimate)");
        var plain = BotContext.Contender();
        Check(!plain.PlannerDiscard2 && !plain.AttackAddOnly && !plain.AttackVetoOnly && !plain.HonorGuard && !plain.PresenceDenial && !plain.NoSpendLost && !plain.OpenHoldings && !plain.ProclaimBoost, "the Contender has every research switch off");
    }

    private static void Rec1DiscardTogether()
    {
        // Four 5-cost Personalities against Gold for one (a 5 and a 3): each is affordable alone, so the first rule keeps all four; the second keeps what can be bought together and flushes the rest.
        CardView[] row = [Card("P1", CardType.Personality, 5), Card("P2", CardType.Personality, 5), Card("P3", CardType.Personality, 5), Card("P4", CardType.Personality, 5)];
        Check(DiscardWith(BotContext.Contender(discard: true), row) == "endDynasty", "the first discard rule keeps four cards that cannot all be bought");
        Check(DiscardWith(BotContext.Contender(discard2: true), row).StartsWith("discard"), "discard v2 flushes what the Gold cannot buy together");
        Check(DiscardWith(BotContext.Contender(discard2: true), [Card("P1", CardType.Personality, 4), Card("H1", CardType.Holding, 3, 2)]) == "endDynasty", "what can be bought together is kept");
    }

    private static void Rec2AttackHalves()
    {
        // Force 7 against Force 4 and Strength 6 with a full enemy hand: the champion attacks (7 beats both), the estimate says the guessed tricks swing it back.
        var risky = AttackTable(7, 5);
        Check(Declare(risky, new BotContext()) == "declareAttack", "the champion attacks Force 7 into Strength 6 and Force 4");
        Check(Declare(risky, BotContext.Contender(vetoOnly: true)) == "pass", "veto-only: the estimate cancels it");
        Check(Declare(risky, BotContext.Contender(addOnly: true)) == "declareAttack", "add-only: the estimate never cancels the champion's attack");
        // Force 5 against Force 4 with no enemy hand: the champion cannot raze, the estimate wins the battle.
        var winnable = AttackTable(5, 0);
        Check(Declare(winnable, BotContext.Contender(addOnly: true)) == "declareAttack", "add-only: the estimate adds an attack the champion would pass on");
        Check(Declare(winnable, BotContext.Contender(vetoOnly: true)) == "pass", "veto-only: never adds one");
    }

    private static void Rec3HonorGuard()
    {
        var risky = AttackTable(7, 5);
        Check(Declare(WithEnemyHonor(risky, 20), BotContext.Contender(honorGuard: true)) == "declareAttack", "the guard is off while the opponent is below 30 Honor");
        Check(Declare(WithEnemyHonor(risky, 32), BotContext.Contender(honorGuard: true)) == "pass", "at 32 Honor an attack the estimate does not win is passed on");
        Check(Declare(WithEnemyHonor(AttackTable(12, 0), 32), BotContext.Contender(honorGuard: true)) == "declareAttack", "at 32 Honor an attack the estimate wins by a margin still goes");
    }

    private static void Rec4PresenceDenial()
    {
        var v = AttackTable(8, 0) with { Phase = Phase.Action };
        string Play(MatchView view, BotContext memory)
        {
            var bow = BattlePolicyTests.Use(view, "training-bow-at-battle", "unit", "foe", ["foe"]);
            return TrainingOpponent.Choose(view, [BattlePolicyTests.Pass(view), bow], memory).Command?.Type ?? "none";
        }
        Check(Play(v, BotContext.Contender(presence: true)) == "useAbility", "the opponent's only defender is bowed in the Action phase when the attack then wins");
        var two = v with { Players = [v.Players[0], v.Players[1] with { Home = v.Players[1].Home.Append(new CardView("foe2", BattlePolicyTests.Person(3), 1, Zone.Home, true, false, null, null, 3)).ToArray() }] };
        Check(Play(two, BotContext.Contender(presence: true)) == Play(two, BotContext.Contender()), "with two possible defenders the switch changes nothing");
    }

    private static MatchView NoHand(MatchView v) => v with { Players = [v.Players[0] with { Hand = [], HandCount = 0 }, v.Players[1]] };

    private static void Rec5NoSpendLost()
    {
        var costly = new GoldQuote(2, "Printed", "", null, []);
        string Chose(MatchView v, BotContext memory)
        {
            var bow = BattlePolicyTests.Use(v, "training-bow-at-battle", "own-main", null, ["enemy-main"]) with { Gold = costly };
            return TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), bow], memory).Command?.Type ?? "none";
        }
        var lost = NoHand(BattlePolicyTests.Battle(3, 12, 5));
        Check(Chose(lost, BotContext.Contender(noSpendLost: true)) == "pass", "a costly action the whole hand cannot turn into a win is not played");
        // Defending the bot's last Province is always done with everything.
        var last = NoHand(BattlePolicyTests.Battle(3, 12, 5, botAttacks: false));
        last = last with { Players = [last.Players[0] with { Provinces = last.Players[0].Provinces.Select((p, i) => i == 0 ? p : p with { Destroyed = true }).ToArray() }, last.Players[1]] };
        Check(Chose(last, BotContext.Contender(noSpendLost: true)) == "useAbility", "the last Province is defended with everything");
    }

    private static void Rec7OpenHoldings()
    {
        // First turn, Gold of 5 and two 3s, a row of an 8-cost Personality (more than the Stronghold alone makes, and it takes all the Gold) and two 4-cost Holdings producing 3.
        var row = new[] { Card("P1", CardType.Personality, 8), Card("H1", CardType.Holding, 4, 3), Card("H2", CardType.Holding, 4, 3) };
        var view = DynastyRow(row);
        var second = new CardView("hold2", view.Players[0].Home[0].Definition, 0, Zone.Home, true, false, null, null, 0);
        view = view with { Players = [view.Players[0] with { Home = view.Players[0].Home.Append(second).ToArray(), TurnsTaken = 1 }, view.Players[1]] };
        var baseline = SlateFor(view, BotContext.Contender()).Split(',');
        var open = SlateFor(view, BotContext.Contender(openHoldings: true)).Split(',');
        Check(baseline.Contains("P1") && !baseline.Contains("H1"), "without the switch the planned buy is the Personality: " + string.Join(",", baseline));
        Check(open.Contains("H1") && open.Contains("H2") && !open.Contains("P1"), "on the first turn the switch buys the two Holdings: " + string.Join(",", open));
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
