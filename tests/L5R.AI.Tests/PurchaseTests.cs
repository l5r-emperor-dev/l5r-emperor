using L5R.AI;
using L5R.Core;

// Issue #1206 (strategy brief step 3, family F; RULE_FAMILIES.md F; PLAYER_MODEL.md 3.6): the Dynasty purchase on constructed positions. A body that makes home hold is bought before a Holding;
// a body that turns next turn's attack from fails to falls is bought; pressing buys bodies; with nobody across the table or home holding anyway, a Holding that pays back is bought; otherwise
// bodies, a Gold producer first; a free Holding always.
internal static class PurchaseTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, bool bowed = false, int? province = null) => new(id, id, owner, force, force, bowed, false, province, [], [], 2, 0, false, false, 0, 0, 2, null);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int gold = 4, int hand = 0) => new(id, units, provinces, gold, gold, 20, false, hand, [], [], 0);
    private static Read ReadOf(LedgerSide me, LedgerSide them) => new(new Ledger(me.Id, 4, Phase.Dynasty, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static PurchaseCandidate Body(string id, int cost, int force, int production = 0) => new(id, id, CardType.Personality, cost, force, production, U(id, 0, force));
    private static PurchaseCandidate Holding(string id, int cost, int production) => new(id, id, CardType.Holding, cost, 0, production, null);
    private static string Log(PurchasePlan p) => $"{p.Choice?.Name ?? "none"} line {p.Line}: {p.Why} | {string.Join(" || ", p.Log)}";

    public static readonly (string, Action)[] Tests =
    [
        ("line 1: their 11 straightened against my 3 at home and Strength 5 loses a Province at likely; the 2 for 2 Gold still loses (11 against 5 + 5), the 4 for 4 Gold holds (7 + 5), and is bought before the fair Holding", () =>
        {
            var me = Side(0, [U("i3", 0, 3)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t11", 1, 11, bowed: true)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Holding("farm", 3, 3), Body("b4", 4, 4), Body("b2", 2, 2)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "b4" && plan.Line == 1, $"the 4 that makes home hold: {Log(plan)}");
        }),
        ("line 1 picks the cheapest body that holds: against their 9, my 3 at home with Strength 5 loses; the 2 for 2 Gold makes 10 and holds, the 6 for 6 Gold is not needed", () =>
        {
            var me = Side(0, [U("i3", 0, 3)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t9", 1, 9, bowed: true)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b6", 6, 6), Body("b2", 2, 2)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "b2" && plan.Line == 1, $"the cheapest holding body: {Log(plan)}");
        }),
        ("line 2: they have no Personality; a Holding costing 3 producing 2 pays back over 6 turns and is bought before a body (6 Gold does not buy both)", () =>
        {
            var me = Side(0, [U("i3", 0, 3)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b4", 4, 4), Holding("mine", 3, 2)], Posture.Build, 6);
            Check(plan.Choice?.Id == "mine" && plan.Line == 2, $"the Holding while they have nobody: {Log(plan)}");
        }),
        ("line 6: home holds at likely (their 4 against my 6 and Strength 5) and they have a Personality: a body before a Holding that pays back only over six turns (PM 3.6: once they have a Personality, Personalities first)", () =>
        {
            var me = Side(0, [U("i6", 0, 6)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t4", 1, 4)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b3", 3, 3), Holding("estate", 6, 2)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "b3" && plan.Line == 6, $"a body once they have a Personality: {Log(plan)}");
        }),
        ("line 5: building (their answers beat my units) while home holds: the Holding that pays back comes before a body; one that does not pay back in 3 turns does not", () =>
        {
            var me = Side(0, [U("i6", 0, 6)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t4", 1, 4)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b3", 3, 3), Holding("farm", 2, 3)], Posture.Build, 6);
            Check(plan.Choice?.Id == "farm" && plan.Line == 5, $"the paying Holding while building: {Log(plan)}");
            var poor = Purchase.Of(ReadOf(me, them), [Body("b3", 3, 3), Holding("shrine", 5, 1)], Posture.Build, 3);
            Check(poor.Choice?.Id == "b3" && poor.Line == 6, $"a body when the Holding does not pay back: {Log(poor)}");
        }),
        ("line 4: pressing, a body is bought before a Holding that pays back", () =>
        {
            var me = Side(0, [U("i8", 0, 8), U("i5", 0, 5)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t4", 1, 4)], [(0, 4), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Holding("farm", 2, 3), Body("b3", 3, 3)], Posture.Press, 6);
            Check(plan.Choice?.Id == "b3" && plan.Line == 4, $"pressing buys the body: {Log(plan)}");
        }),
        ("line 6 prefers a Gold producer within 2 Force of the best: against their 14 no body alone holds; the 3 that produces 1 Gold comes before the 4 that produces none, but against their 16 a 0 that produces Gold does not come before a 6", () =>
        {
            var me = Side(0, [U("i3", 0, 3)], [(0, 5), (1, 6)], gold: 8); var them = Side(1, [U("t14", 1, 14, bowed: true)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b4", 4, 4), Body("g3", 4, 3, production: 1)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "g3" && plan.Line == 6, $"the producer first when close in Force: {Log(plan)}");
            var far = Purchase.Of(ReadOf(me, Side(1, [U("t16", 1, 16, bowed: true)], [(0, 6), (1, 6)])), [Body("b6", 6, 6), Body("g0", 4, 0, production: 1)], Posture.Hold, 6);
            Check(far.Choice?.Id == "b6" && far.Line == 6, $"the 6 before a 0 that produces Gold: {Log(far)}");
        }),
        ("line 5b: home holds and I am level on Personalities: a Holding costing 2 producing 3 comes before a body; behind on Personalities the body comes first", () =>
        {
            var me = Side(0, [U("i6", 0, 6)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t4", 1, 4)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b3", 3, 3), Holding("farm", 2, 3)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "farm" && plan.Line == 5, $"the quick Holding when level: {Log(plan)}");
            var behind = Purchase.Of(ReadOf(me, Side(1, [U("t4", 1, 4), U("t1", 1, 1)], [(0, 6), (1, 6)])), [Body("b3", 3, 3), Holding("farm", 2, 3)], Posture.Hold, 6);
            Check(behind.Choice?.Id == "b3" && behind.Line == 6, $"the body when behind: {Log(behind)}");
        }),
        ("moot: 12 Gold pays for the whole affordable row (a 6 body and a 2 Holding), so the plan says the order does not matter", () =>
        {
            var me = Side(0, [U("i6", 0, 6)], [(0, 5), (1, 6)], gold: 12); var them = Side(1, [U("t4", 1, 4)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b6", 6, 6), Holding("farm", 2, 3)], Posture.Hold, 6);
            Check(plan.Moot, $"moot: {Log(plan)}");
        }),
        ("line 6 buys Force for the Gold: with 7 Gold, two 3-Force bodies for 3 each (6 Force) come before one 4-Force body for 6; the first pick is one of the two 3s", () =>
        {
            var me = Side(0, [U("i4", 0, 4)], [(0, 7), (1, 7)], gold: 7); var them = Side(1, [U("t5", 1, 5)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("kisho", 3, 3), Body("natsu", 3, 3), Body("sugo", 6, 4)], Posture.Hold, 6);
            Check(plan.Choice?.Id is "kisho" or "natsu" && plan.Line == 6, $"a 3 of the pair, not the lone 4: {Log(plan)}");
        }),
        ("line 6 passes on utility bodies: a 1-Force for 2 and a 0-Force for 3 are targets, not security; a Holding producing 3 for 3 comes first", () =>
        {
            var me = Side(0, [], [(0, 7), (1, 7)], gold: 7); var them = Side(1, [U("t4", 1, 4)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("kouki", 3, 0), Body("hachiro", 2, 1), Holding("village", 3, 3)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "village" && plan.Line == 6, $"the Holding over utility bodies: {Log(plan)}");
        }),
        ("line 0: a free Holding is taken first whatever else the row offers", () =>
        {
            var me = Side(0, [U("i3", 0, 3)], [(0, 5), (1, 6)], gold: 6); var them = Side(1, [U("t11", 1, 11, bowed: true)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b4", 4, 4), Holding("free", 0, 1)], Posture.Hold, 6);
            Check(plan.Choice?.Id == "free" && plan.Line == 0, $"the free Holding: {Log(plan)}");
        }),
        ("nothing affordable: the plan is silent", () =>
        {
            var me = Side(0, [U("i3", 0, 3)], [(0, 5), (1, 6)], gold: 1); var them = Side(1, [U("t9", 1, 9)], [(0, 6), (1, 6)]);
            var plan = Purchase.Of(ReadOf(me, them), [Body("b4", 4, 4), Holding("farm", 3, 3)], Posture.Hold, 6);
            Check(plan.Silent, $"silent: {Log(plan)}");
        }),
    ];
}
