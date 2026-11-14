using L5R.AI;
using L5R.Core;

// Issue #1206 (strategy brief step 3, family C; RULE_FAMILIES.md C; PLAYER_MODEL.md 3.3): the Guard on constructed positions. The attack stands when home holds at likely; the weakest unit whose
// group still razes is kept back when that turns the threat; nobody attacks when only the whole army at home holds; a race, a lethal attack, and a lead with two Provinces to spare let the attack stand.
internal static class GuardTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, bool bowed = false, int? province = null) => new(id, id, owner, force, force, bowed, false, province, [], [], 2, 0, false, false, 0, 0, 2, null);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int hand = 0) => new(id, units, provinces, 4, 4, 20, false, hand, [], [], 0);
    private static Read ReadOf(LedgerSide me, LedgerSide them) => new(new Ledger(me.Id, 5, Phase.InfantryDeclaration, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static Dictionary<string, int> A(params (string, int)[] a) => a.ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal);

    public static readonly (string, Action)[] Tests =
    [
        ("home holds: my 8 and 3 attack their Strength 5 with 2 at home against their 4 and 2 (6 straightened against 2 + Strength 7): the home holds, the attack stands", () =>
        {
            var me = Side(0, [U("m8", 0, 8), U("m3", 0, 3), U("m2", 0, 2)], [(0, 7), (1, 7)]); var them = Side(1, [U("t4", 1, 4), U("t2", 1, 2)], [(0, 5), (1, 6)]);
            var plan = Guard.Of(ReadOf(me, them), A(("m8", 0), ("m3", 0)), Posture.Hold, false);
            Check(plan.Stands && plan.Why.Contains("holds"), $"stands: {plan.Why} | {string.Join(" || ", plan.Log)}");
        }),
        ("a unit kept back: my 8, 5 and 3 attack their Strength 3 (their 5 and 2 do not defend, the attack razes anyway); home empty against their 7 straightened and my Strength 2: a Province is lost; the 3 kept back (3 + 2) does not hold, the 5 (5 + 2 = 7, not beaten by 7) does, and 8 + 3 still razes", () =>
        {
            var me = Side(0, [U("m8", 0, 8), U("m5", 0, 5), U("m3", 0, 3)], [(0, 2), (1, 7)]); var them = Side(1, [U("t5", 1, 5), U("t2", 1, 2)], [(0, 3), (1, 6)]);
            var plan = Guard.Of(ReadOf(me, them), A(("m8", 0), ("m5", 0), ("m3", 0)), Posture.Hold, false);
            Check(!plan.Stands && !plan.Nobody, $"something is kept back: {plan.Why} | {string.Join(" || ", plan.Log)}");
            Check(plan.Kept.SequenceEqual(["m5"]), $"the 5 is kept (the 3 would not hold, the 8 is needed to raze): kept {string.Join(",", plan.Kept)} | {string.Join(" || ", plan.Log)}");
            Check(plan.After.Likely.ProvincesLost == 0 && plan.Before.Likely.ProvincesLost > 0, $"the threat turns: before {plan.Before.Likely}, after {plan.After.Likely}");
            // when their whole home can defend and the attack only just razes, no unit can be spared: the Guard holds everything (only the army at home holds)
            var tight = Guard.Of(ReadOf(Side(0, [U("m8", 0, 8), U("m5", 0, 5), U("m3", 0, 3)], [(0, 4), (1, 7)]), Side(1, [U("t5", 1, 5), U("t4", 1, 4)], [(0, 5), (1, 6)])), A(("m8", 0), ("m5", 0), ("m3", 0)), Posture.Hold, false);
            Check(tight.Nobody, $"16 against 9 + 5 razes only whole: nobody goes: {tight.Why}");
        }),
        ("nobody attacks: my 6 alone against their Strength 5 (razes), home empty against their 7: a Province is lost; only the 6 at home holds (6 + 4 < 7? 6 + Strength 4 = 10 > 7 holds): nobody goes", () =>
        {
            var me = Side(0, [U("m6", 0, 6)], [(0, 4), (1, 7)]); var them = Side(1, [U("t7", 1, 7)], [(0, 5)]);
            var plan = Guard.Of(ReadOf(me, them), A(("m6", 0)), Posture.Hold, false);
            Check(plan.Nobody && plan.Why.Contains("nobody"), $"nobody: {plan.Why} | {string.Join(" || ", plan.Log)}");
        }),
        ("the loss accepted: the same position in a race stands; a lethal attack stands; ahead 4 to 1 the Province is a price (only the hold turns it, and the attack would be broken)", () =>
        {
            var me = Side(0, [U("m6", 0, 6)], [(0, 4), (1, 7)]); var them = Side(1, [U("t7", 1, 7)], [(0, 5)]);
            Check(Guard.Of(ReadOf(me, them), A(("m6", 0)), Posture.Race, false).Stands, "a race accepts the loss");
            Check(Guard.Of(ReadOf(me, them), A(("m6", 0)), Posture.Hold, true).Stands, "a lethal attack stands");
            var last = Guard.Of(ReadOf(Side(0, [U("m9", 0, 9), U("m3", 0, 3)], [(0, 4), (1, 7)]), Side(1, [U("t7", 1, 7)], [(0, 2)])), A(("m9", 0), ("m3", 0)), Posture.Hold, false);
            Check(last.Stands && last.Why.Contains("ends the game"), $"their last Province razes at likely: nothing is kept back even when the Contender's lethal plan is silent: {last.Why}");
            var ahead = Side(0, [U("m6", 0, 6)], [(0, 4), (1, 7), (2, 7), (3, 7)]);
            var plan = Guard.Of(ReadOf(ahead, them), A(("m6", 0)), Posture.Hold, false);
            Check(plan.Stands && plan.Why.Contains("accepted"), $"ahead with Provinces to spare the attack stands: {plan.Why}");
        }),
        ("the Cavalry declaration: my 8 already at their Strength 9 Province (it does not raze and cannot be recalled), my 6 to go to Strength 5; home would be empty against their 7: the Guard answers (it once kept back the fixed group forever) and keeps the 6 home", () =>
        {
            var me = Side(0, [U("m8", 0, 8, province: 0), U("m6", 0, 6)], [(0, 4), (1, 7)]); var them = Side(1, [U("t7", 1, 7)], [(0, 9), (1, 5)]);
            var run = Task.Run(() => Guard.Of(ReadOf(me, them), A(("m6", 1)), Posture.Hold, false));
            Check(run.Wait(TimeSpan.FromSeconds(20)), "the Guard returns within 20 s with a fixed group that cannot be kept back");
            Check(run.Result.Nobody && run.Result.Kept.SequenceEqual(["m6"]), $"the 6 stays home: {run.Result.Why} | {string.Join(" || ", run.Result.Log)}");
        }),
        ("the attack is not what loses the Province: even the whole army at home loses one (their 12 against my 2 and Strength 4): the attack stands", () =>
        {
            var me = Side(0, [U("m2", 0, 2)], [(0, 4), (1, 7)]); var them = Side(1, [U("t12", 1, 12)], [(0, 1)]);
            var plan = Guard.Of(ReadOf(me, them), A(("m2", 0)), Posture.Hold, false);
            Check(plan.Stands && plan.Why.Contains("not what loses"), $"stands: {plan.Why} | {string.Join(" || ", plan.Log)}");
        }),
    ];
}
