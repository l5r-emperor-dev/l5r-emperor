using L5R.AI;
using L5R.Core;

// Issue #1206 (strategy brief step 3, family B; RULE_FAMILIES.md B): the Cavalry fork on constructed positions. A seat without Cavalry has no fork; with Cavalry and a defender who can
// answer a probe, nothing is declared in the Infantry segment and the Cavalry goes alone to the weakest uncovered Province, only as much of it as exceeds Strength; at the Cavalry
// declaration the Cavalry reinforces a battlefield its infantry is losing when that razes, and does not take an uncovered Province their own Cavalry can still reach.
internal static class ForkTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, bool cavalry = false, bool bowed = false, int? province = null) => new(id, id, owner, force, force, bowed, cavalry, province, [], [], 2, 0, false, false, 0, 0, 2, null);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int hand = 0) => new(id, units, provinces, 4, 4, 20, false, hand, [], [], 0);
    private static Read ReadOf(LedgerSide me, LedgerSide them, Phase phase = Phase.InfantryDeclaration) => new(new Ledger(me.Id, 5, phase, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static string Log(ForkPlan p) => $"{p.Option} / {p.FollowUp}: {p.Why} | {string.Join(" || ", p.Log)}";

    public static readonly (string, Action)[] Tests =
    [
        ("no Cavalry: the fork does not apply at either declaration", () =>
        {
            var me = Side(0, [U("i6", 0, 6), U("i3", 0, 3)], [(0, 7), (1, 7)]); var them = Side(1, [U("t6", 1, 6)], [(0, 4), (1, 6)]);
            Check(Fork.AtInfantry(ReadOf(me, them), null, Posture.Hold) is null, "no Cavalry, no fork at the Infantry declaration");
            Check(Fork.AtCavalry(ReadOf(me, them, Phase.CavalryDeclaration), Posture.Hold) is null, "no Cavalry, no fork at the Cavalry declaration");
        }),
        ("the fork: my 6 (infantry) with Cavalry 5 and 4 against their 6 at home and Provinces of Strength 4 and 6. Against the 6 the probe is declared: if they defend, 15 beats 10 and their 6 dies; if not, the infantry razes and the Cavalry takes the other Province. Against a 12 at home the probe would die, so nothing is declared and the Cavalry goes alone to the Strength 4 Province, which no infantry can defend", () =>
        {
            var me = Side(0, [U("i6", 0, 6), U("c5", 0, 5, cavalry: true), U("c4", 0, 4, cavalry: true)], [(0, 7), (1, 7)]);
            var probe = Fork.AtInfantry(ReadOf(me, Side(1, [U("t6", 1, 6)], [(0, 4), (1, 6)])), new Dictionary<string, int> { ["i6"] = 0 }, Posture.Hold);
            Check(probe is not null && probe.Infantry.Count == 1 && probe.Infantry.ContainsKey("i6") && probe.FollowUp.Contains("reinforcing"), $"the probe is declared, the Cavalry reinforces its worst case: {(probe is null ? "null" : Log(probe))}");
            var alone = Fork.AtInfantry(ReadOf(me, Side(1, [U("t12", 1, 12)], [(0, 4), (1, 6)])), new Dictionary<string, int> { ["i6"] = 0 }, Posture.Hold);
            Check(alone is not null && alone.Option == "nothing" && alone.Infantry.Count == 0, $"nothing in the Infantry segment against the 12: {(alone is null ? "null" : Log(alone))}");
            Check(alone!.Cavalry.Count > 0 && alone.Cavalry.Values.All(p => p == 0), $"the Cavalry follows to the Strength 4 Province: {Log(alone)}");
            Check(alone.Forced > 0.5, $"the forced value is a Province's worth: {alone.Forced:0.00} | {Log(alone)}");
        }),
        ("the sufficient Cavalry: at the Cavalry declaration with nothing declared, Cavalry 5 and 4 against Provinces of Strength 4 and 6 and their bowed 9 at home (it attacks next turn): the 5 alone exceeds 4 and the 4 stays home, where it holds my Strength 7 Province against the 9", () =>
        {
            var me = Side(0, [U("c5", 0, 5, cavalry: true), U("c4", 0, 4, cavalry: true)], [(0, 7), (1, 7)]); var them = Side(1, [U("t9", 1, 9, bowed: true)], [(0, 4), (1, 6)]);
            var plan = Fork.AtCavalry(ReadOf(me, them, Phase.CavalryDeclaration), Posture.Hold);
            Check(plan is not null && plan.Cavalry.Count == 1 && plan.Cavalry.TryGetValue("c5", out var p) && p == 0, $"the 5 alone to the Strength 4 Province: {(plan is null ? "null" : Log(plan))}");
        }),
        ("reinforce: my 7 is at their Strength 5 Province against their placed 6 (7 against 11: it dies); my Cavalry 5 at home makes 12 and razes; the other Province (Strength 5) is not taken by the 5 alone: the Cavalry reinforces", () =>
        {
            var me = Side(0, [U("i7", 0, 7, province: 0), U("c5", 0, 5, cavalry: true)], [(0, 7), (1, 7)]); var them = Side(1, [U("t6", 1, 6, province: 0)], [(0, 5), (1, 5)]);
            var plan = Fork.AtCavalry(ReadOf(me, them, Phase.CavalryDeclaration), Posture.Hold);
            Check(plan is not null && plan.Cavalry.Count == 1 && plan.Cavalry.TryGetValue("c5", out var p) && p == 0, $"the 5 reinforces P1: {(plan is null ? "null" : Log(plan))}");
        }),
        ("their Cavalry can still arrive: my 10 at their Strength 5 Province undefended (it razes), my Cavalry 4 at home, their unbowed Cavalry 6 at home, their other Province at Strength 3: the 4 alone there dies to the 6 that follows; it reinforces instead (14 against anything they bring)", () =>
        {
            var me = Side(0, [U("i10", 0, 10, province: 0), U("c4", 0, 4, cavalry: true)], [(0, 7), (1, 7)]); var them = Side(1, [U("k6", 1, 6, cavalry: true)], [(0, 5), (1, 3)]);
            var plan = Fork.AtCavalry(ReadOf(me, them, Phase.CavalryDeclaration), Posture.Hold);
            Check(plan is not null && !plan.Cavalry.Values.Contains(1), $"the Strength 3 Province is not attacked by the 4 alone: {(plan is null ? "null" : Log(plan))}");
            Check(plan!.Cavalry.TryGetValue("c4", out var p) && p == 0, $"the 4 reinforces P1: {Log(plan)}");
        }),
        ("an all-Cavalry army: nothing is declared in the Infantry segment and the plan is not null", () =>
        {
            var me = Side(0, [U("c7", 0, 7, cavalry: true), U("c5", 0, 5, cavalry: true)], [(0, 7), (1, 7)]); var them = Side(1, [U("t6", 1, 6)], [(0, 4), (1, 6)]);
            var plan = Fork.AtInfantry(ReadOf(me, them), null, Posture.Press);
            Check(plan is not null && plan.Infantry.Count == 0 && plan.Option == "nothing", $"nothing in the Infantry segment: {(plan is null ? "null" : Log(plan))}");
        }),
    ];
}
