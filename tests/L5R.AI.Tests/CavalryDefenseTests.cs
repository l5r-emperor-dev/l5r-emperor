using L5R.AI;
using L5R.Core;

// Issue #946: Mendon's defense guidance for an attacker that commits no infantry but has Cavalry at home.
internal static class CavalryDefenseTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("DefensePlanner: parity needs Force / threat + 0.1 per hand card and Battle action to reach 1; one army per sub-army that reaches 1 alone; leftovers stay home unless they print a Battle action", Planner),
        ("Bot: with no infantry assigned against it, it commits its infantry to the most valuable province when it can match the attacker's possible Cavalry, and passes when it cannot or there is none", BotDefends),
        ("Bot: when its attacker's Cavalry (with any army already assigned) can destroy every Province it has left, it defends with everything where that stops it, else at its strongest Province; not when the Cavalry cannot take them all", AllProvincesThreatened),
        ("Bot (issue #1211): a last stand against a dozen Cavalry with eight defenders, where every Province can still be taken, is decided in moments, not hours", LastStandIsBounded),
        ("Bot attack: the Infantry Maneuvers Segment assigns only infantry; the Cavalry waits and is assigned in the Cavalry Maneuvers Segment to join the infantry already sent", CavalryWaits),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static DefensePlanner.Unit Foot(string id, int force, bool action = false) => new(id, force, true, action);
    private static DefensePlanner.Unit Horse(string id, int force, bool action = false) => new(id, force, false, action);

    private static void Planner()
    {
        Check(DefensePlanner.Plan([Foot("a", 8)], [0, 1], 10, 0) is null, "8 against 10 with nothing in hand: 0.8 never reaches 1");
        Check(DefensePlanner.Plan([Foot("a", 8)], [0, 1], 10, 1) is null, "one card makes 0.9");
        Check(DefensePlanner.Plan([Foot("a", 8)], [0, 1], 10, 2) is { } two && two["a"] == 0, "two cards make exactly 1.0: defend");
        Check(DefensePlanner.Plan([Foot("a", 8, true)], [0, 1], 10, 1) is not null, "a printed Battle action on a defending unit counts like a card");
        Check(DefensePlanner.Plan([Foot("a", 10)], [0], 10, 0) is not null, "equal Force is parity");
        Check(DefensePlanner.Plan([Horse("a", 12)], [0], 10, 0) is null, "only Cavalry at home: it is held back for the Cavalry Maneuvers Segment, so nothing is committed now");
        Check(DefensePlanner.Plan([Foot("a", 12)], [0], 0, 0) is null, "no possible Cavalry army, no defense");

        // Three units of 6 against 6: three sub-armies, one per province, in value order.
        var three = DefensePlanner.Plan([Foot("a", 6), Foot("b", 6), Foot("c", 6)], [2, 0, 1], 6, 0)!;
        Check(three["a"] == 2 && three["b"] == 0 && three["c"] == 1, "each unit is its own army at the next most valuable province");
        // Only two provinces: the third unit is a leftover and stays home...
        var two2 = DefensePlanner.Plan([Foot("a", 6), Foot("b", 6), Foot("c", 6)], [2, 0], 6, 0)!;
        Check(two2.Count == 2 && !two2.ContainsKey("c"), "no province left for a third army: it stays home");
        // ...and a Battle action takes it into the first army.
        var action = DefensePlanner.Plan([Foot("a", 6), Foot("b", 6), Foot("c", 3, true)], [2, 0], 6, 0)!;
        Check(action["c"] == 2 && action["a"] == 2 && action["b"] == 0, "a leftover with a Battle action joins the first army (got " + string.Join(",", action.Select(p => p.Key + ">" + p.Value)) + ")");
        // The hand backs the first army only: 4 + 4 against 6 with one card each.. the first reaches 0.67+0.1*... never, the second needs 6 alone.
        var hand = DefensePlanner.Plan([Foot("a", 5), Foot("b", 4)], [0, 1], 6, 1)!;
        Check(hand.Count == 2 && hand["a"] == 0 && hand["b"] == 0, "5 + 4 fall short of a second army of 6 on their own: both defend the first province (got " + string.Join(",", hand.Select(p => p.Key + ">" + p.Value)) + ")");
        // Cavalry defenders are held back for the Cavalry Maneuvers Segment, however strong, but their Force counts toward parity.
        var mixed = DefensePlanner.Plan([Horse("h", 9), Foot("f", 6)], [0, 1], 6, 0)!;
        Check(mixed.Count == 1 && mixed["f"] == 0, "the Cavalry is not assigned now");
        var joined = DefensePlanner.Plan([Horse("h", 6), Foot("f", 4)], [0, 1], 10, 0)!;
        Check(joined.Count == 1 && joined["f"] == 0, "4 infantry alone are short of 10, but with the held-back Cavalry the defense is on par: the infantry holds the most valuable province");
        Check(DefensePlanner.Plan([Horse("h", 3), Foot("f", 4)], [0, 1], 10, 0) is null, "even with the Cavalry it cannot reach parity: nobody is committed");
    }

    private static CardDefinition Infantry => CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force >= 4 && d.Keywords?.Contains("Cavalry") != true);
    private static CardView Unit(string id, int owner, int force, CardDefinition def) => new(id, def, owner, Zone.Home, true, false, null, null, force);

    private static void BotDefends()
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var cheap = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost == 3);
        var dear = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost >= 7);
        var cavalry = CardCatalog.Get("training-cavalry");
        ProvinceView[] Provinces() =>
        [
            new(0, 5, false, null, null), new(1, 5, false, new CardView("p1", cheap, 0, Zone.Province, true, false, 1, null, cheap.Force), null),
            new(2, 5, false, new CardView("p2", dear, 0, Zone.Province, true, false, 2, null, dear.Force), null),
            new(3, 5, false, new CardView("hidden-p3", null, 0, Zone.Province, false, false, 3, null, 0), null)
        ];
        MatchView Table(int ownForce, int horses, int horseForce, int hand = 0) => view with
        {
            Phase = Phase.DefenseAssignment,
            ActivePlayerId = 1,
            PriorityPlayerId = 0,
            PendingChoice = null,
            Players = [view.Players[0] with { Home = [Unit("own-1", 0, ownForce, Infantry)], Army = [], Provinces = Provinces(), HandCount = hand, Hand = [] },
                view.Players[1] with { Home = Enumerable.Range(0, horses).Select(i => Unit("h-" + i, 1, horseForce, cavalry)).ToArray(), Army = [] }]
        };
        ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
        ActionOption Defend(MatchView v) => new("assignDefense", "Defend", true, null, null, TargetIds: ["own-1"], Provinces: [0, 1, 2, 3]);

        var even = Table(8, 2, 4);
        var chosen = TrainingOpponent.Choose(even, [Pass(even), Defend(even)]);
        Check(chosen.Command?.Type == "assignDefense" && chosen.Command.Assignments?["own-1"] == 2, "8 against a possible 8 Cavalry: it defends the province holding the 7-Gold Personality (got " + chosen.Command?.Type + ")");

        var weak = Table(5, 2, 4);
        Check(TrainingOpponent.Choose(weak, [Pass(weak), Defend(weak)]).Command?.Type == "pass", "5 against 8 with an empty hand cannot reach 1: it keeps its units home");
        var tricks = Table(5, 2, 4, hand: 4);
        Check(TrainingOpponent.Choose(tricks, [Pass(tricks), Defend(tricks)]).Command?.Type == "assignDefense", "5 / 8 = 0.625 + 0.4 from four cards reaches 1: it defends");
        var none = Table(8, 0, 0);
        Check(TrainingOpponent.Choose(none, [Pass(none), Defend(none)]).Command?.Type == "pass", "no Cavalry at home and nobody attacking: nothing to defend against");
        Check(TrainingOpponent.Choose(even, [Pass(even), Defend(even)], new BotContext { UseCavalryDefense = false }).Command?.Type == "pass", "switched off, it plays as before");

        // Mendon, 2026-10-17: with its last Province left and the attacker's Cavalry able to destroy it, it defends with everything even when it cannot reach parity.
        MatchView LastProvince(int ownForce, int horses, int horseForce, int strength) => Table(ownForce, horses, horseForce) with
        {
            Players = [Table(ownForce, horses, horseForce).Players[0] with { Provinces = [new(0, 5, true, null, null), new(1, strength, false, null, null)] }, Table(ownForce, horses, horseForce).Players[1]]
        };
        var last = LastProvince(2, 2, 4, 5);
        var stand = TrainingOpponent.Choose(last, [Pass(last), Defend(last)]);
        Check(stand.Command?.Type == "assignDefense" && stand.Command.Assignments?.Count == 1 && stand.Command.Assignments.Values.Single() == 1, "2 against a possible 8 at its last Province (strength 5): it still defends with everything (got " + stand.Command?.Type + ")");
        var safe = LastProvince(2, 1, 4, 5);
        Check(TrainingOpponent.Choose(safe, [Pass(safe), Defend(safe)]).Command?.Type == "pass", "the Cavalry (4) cannot destroy a strength-5 Province, so the last-stand rule does not apply");
        Check(TrainingOpponent.Choose(last, [Pass(last), Defend(last)], new BotContext { UseCavalryDefense = false }).Command?.Type == "pass", "switched off, the last stand is off too");
    }

    private static void AllProvincesThreatened()
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var cavalry = CardCatalog.Get("training-cavalry");
        MatchView Table(int ownForce, int[] horses, int[] strengths, CardView[]? attackers = null) => view with
        {
            Phase = Phase.DefenseAssignment,
            ActivePlayerId = 1,
            PriorityPlayerId = 0,
            PendingChoice = null,
            Players = [view.Players[0] with { Home = [Unit("own-1", 0, ownForce, Infantry)], Army = [], HandCount = 0, Hand = [], Provinces = strengths.Select((st, i) => new ProvinceView(i, st, false, null, null)).ToArray() },
                view.Players[1] with { Home = horses.Select((f, i) => Unit("h-" + i, 1, f, cavalry)).ToArray(), Army = attackers ?? [] }]
        };
        ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
        ActionOption Defend(int provinces) => new("assignDefense", "Defend", true, null, null, TargetIds: ["own-1"], Provinces: Enumerable.Range(0, provinces).ToArray());

        // Two Provinces of 3, two horses of 4: each horse takes one. 4 defenders on a Province need both horses (8 > 7), so only one Province can fall.
        var two = Table(4, [4, 4], [3, 3]);
        var blocked = TrainingOpponent.Choose(two, [Pass(two), Defend(2)]);
        Check(blocked.Command?.Type == "assignDefense" && blocked.Command.Assignments?.Count == 1, "two horses threaten both Provinces: it defends (got " + blocked.Command?.Type + ")");
        // One horse cannot take two Provinces.
        var one = Table(1, [4], [3, 3]);
        Check(TrainingOpponent.Choose(one, [Pass(one), Defend(2)]).Command?.Type == "pass", "a single horse cannot destroy both Provinces: no last stand");
        // Too weak to stop it: everything goes to the strongest Province.
        var hopeless = Table(1, [20, 20], [3, 5]);
        var strongest = TrainingOpponent.Choose(hopeless, [Pass(hopeless), Defend(2)]);
        Check(strongest.Command?.Type == "assignDefense" && strongest.Command.Assignments?["own-1"] == 1, "it cannot stop two huge horses, and defends the strongest Province anyway");
        // An infantry army already at Province 0 (5 > 3) falls anyway; the one horse then takes Province 1, so the defense goes to Province 1.
        var sent = Unit("a-foot", 1, 5, Infantry) with { Zone = Zone.Battlefield, ProvinceIndex = 0 };
        var mixed = Table(4, [4], [3, 3], [sent]);
        var covered = TrainingOpponent.Choose(mixed, [Pass(mixed), Defend(2)]);
        Check(covered.Command?.Type == "assignDefense" && covered.Command.Assignments?["own-1"] == 1, "with its infantry already at Province 0 the horse would take Province 1: it defends there (got " + covered.Command?.Type + ")");
        Check(TrainingOpponent.Choose(two, [Pass(two), Defend(2)], new BotContext { UseCavalryDefense = false }).Command?.Type == "pass", "switched off, it plays as before");
    }

    private static void LastStandIsBounded()
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var cavalry = CardCatalog.Get("training-cavalry");
        // 12 horses of 2 against four Provinces of 3 and eight defenders of 1: each Province needs three horses once two defenders stand on it, so the Cavalry can still take all four
        // under the most even defence. The old search tried every horse subset at every Province for each of the 4^8 assignments and ran for hours.
        var table = view with
        {
            Phase = Phase.DefenseAssignment, ActivePlayerId = 1, PriorityPlayerId = 0, PendingChoice = null,
            Players = [view.Players[0] with { Home = Enumerable.Range(0, 8).Select(i => Unit("own-" + i, 0, 1, Infantry)).ToArray(), Army = [], HandCount = 0, Hand = [], Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceView(i, 3, false, null, null)).ToArray() },
                view.Players[1] with { Home = Enumerable.Range(0, 12).Select(i => Unit("h-" + i, 1, 2, cavalry)).ToArray(), Army = [] }]
        };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var choice = TrainingOpponent.Choose(table, [new("pass", "Pass", true, null, new("pass", 0, table.Version)), new("assignDefense", "Defend", true, null, null, TargetIds: Enumerable.Range(0, 8).Select(i => "own-" + i).ToArray(), Provinces: [0, 1, 2, 3])]);
        Check(clock.Elapsed < TimeSpan.FromSeconds(10), $"the last stand took {clock.Elapsed.TotalSeconds:0.0} s");
        Check(choice.Command?.Type == "assignDefense" && choice.Command.Assignments?.Count == 8, "every defender stands (got " + choice.Command?.Type + ")");
    }

    private static void CavalryWaits()
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var cavalry = CardCatalog.Get("training-cavalry");
        var foot = Infantry;
        ProvinceView[] Provinces() => Enumerable.Range(0, 4).Select(i => new ProvinceView(i, 3, false, null, null)).ToArray();
        MatchView Table(Phase phase, CardView[] home, CardView[] army) => view with
        {
            Phase = phase,
            ActivePlayerId = 0,
            PriorityPlayerId = 0,
            PendingChoice = null,
            Players = [view.Players[0] with { Home = home, Army = army, Hand = [], HandCount = 0 }, view.Players[1] with { Home = [], Army = [], Provinces = Provinces() }]
        };
        ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
        ActionOption Attack(MatchView v, params string[] ids) => new("attack", "Attack", true, null, null, TargetIds: ids, Provinces: [0, 1, 2, 3]);

        var infantry = Table(Phase.InfantryDeclaration, [Unit("foot", 0, 6, foot), Unit("horse", 0, 6, cavalry)], []);
        var first = TrainingOpponent.Choose(infantry, [Pass(infantry), Attack(infantry, "foot", "horse")]);
        Check(first.Command?.Type == "attack" && first.Command.Assignments?.Keys.SequenceEqual(["foot"]) == true, "Infantry segment: only the infantry is assigned (got " + string.Join(",", first.Command?.Assignments?.Keys.ToArray() ?? []) + ")");
        var held = TrainingOpponent.Choose(infantry, [Pass(infantry), Attack(infantry, "foot", "horse")], new BotContext { UseCavalryAttack = false });
        Check(held.Command?.Assignments?.ContainsKey("horse") == true, "switched off, the Cavalry goes with the infantry as before");

        var onlyHorse = Table(Phase.InfantryDeclaration, [Unit("horse", 0, 9, cavalry)], []);
        Check(TrainingOpponent.Choose(onlyHorse, [Pass(onlyHorse), Attack(onlyHorse, "horse")]).Command?.Type == "pass", "Only Cavalry at home: nothing is assigned in the Infantry segment");

        var sent = Unit("foot", 0, 6, foot) with { Zone = Zone.Battlefield, ProvinceIndex = 0 };
        var second = Table(Phase.CavalryDeclaration, [Unit("horse", 0, 6, cavalry)], [sent]);
        var chosen = TrainingOpponent.Choose(second, [Pass(second), Attack(second, "horse")]);
        Check(chosen.Command?.Type == "attack" && chosen.Command.Assignments?.Keys.SequenceEqual(["horse"]) == true, "Cavalry segment: the Cavalry is assigned (got " + chosen.Command?.Type + ")");
    }
}
