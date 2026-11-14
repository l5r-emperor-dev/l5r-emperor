using L5R.AI;
using L5R.Core;

// Issue #1206 (RULE_FAMILIES.md family D, PLAYER_MODEL.md 3.4): the defence assignment read from the Read on constructed positions, a case per branch of the rule card
// (docs/bots/experiments/2026-11-14-conan-family-d.md): the last Province takes the highest hold even at a cost in units; nobody when nothing holds; kill with the smallest option that
// holds and destroys; presence when only an answer in hand holds it; their last Province conceded when my attack razes it next turn; the cross-Province order; 0 to 1 Force units stay
// home unless they are the presence body.
internal static class DefenceTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, int? province = null, bool bowed = false) => new(id, id, owner, force, force, bowed, false, province, [], [], 2, 0, false, false, 0, 0, 0);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int hand = 0, Answer[]? handAnswers = null, int gold = 4) => new(id, units, provinces, gold, gold, 10, false, hand, handAnswers ?? [], []);
    private static Read ReadOf(LedgerSide me, LedgerSide them) => new(new Ledger(me.Id, 6, Phase.DefenseAssignment, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static string FirstAbility(Func<EffectDescriptor, bool> test) => AbilityCatalog.All.Keys.Where(k => !k.StartsWith("training-") && !k.StartsWith("belief-")).Select(EffectDescriptors.Of).Where(d => d.IsBattle && test(d)).OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;

    public static readonly (string, Action)[] Tests =
    [
        ("nobody when nothing holds: 14 Force against my 3 and 2 at Strength 5; everything when it holds and kills", () =>
        {
            var them = Side(1, [U("t1", 1, 8, province: 0), U("t2", 1, 6, province: 0)], [(0, 7), (1, 7)]); var me = Side(0, [U("m1", 0, 3), U("m2", 0, 2)], [(0, 5), (1, 5)]);
            var plan = Defence.Of(ReadOf(me, them), null, [0, 1]);
            Check(plan.Nobody && plan.Why.Contains("nothing holds"), $"5 + 5 is not 14: {plan}");
            var strong = Side(0, [U("m1", 0, 9), U("m2", 0, 7)], [(0, 5), (1, 5)]);
            var kill = Defence.Of(ReadOf(strong, them), null, [0, 1]);
            Check(kill.Assignments.Count == 2 && kill.Why.Contains("kill"), $"16 beats 14: everybody, and the attackers die: {kill}");
        }),
        ("the smallest option that holds: one 9 holds Strength 5 against 6 on Force; the 1 Force utility unit stays home", () =>
        {
            var them = Side(1, [U("t1", 1, 6, province: 1)], [(0, 7)]); var me = Side(0, [U("m1", 0, 9), U("m2", 0, 4), U("m3", 0, 1)], [(0, 5), (1, 5)]);
            var plan = Defence.Of(ReadOf(me, them), null, [0, 1]);
            Check(plan.Assignments.Count >= 1 && plan.Assignments.ContainsKey("m1") && !plan.Assignments.ContainsKey("m3") && plan.Assignments.Values.All(p => p == 1), $"the 9 (with the 4 at most) at P2, the 1 at home: {plan}");
            Check(plan.Why.Contains("kill") || plan.Why.Contains("smallest"), plan.Why);
        }),
        ("presence: a send-home in hand with one body holds what Force cannot; the body goes alone", () =>
        {
            var sendHome = FirstAbility(x => x.Class == EffectClass.SendHome && x.Side == TargetSide.Enemy && x.Conditions.Length == 0 && x.GoldCost == 0);
            var hand = new[] { new Answer(EffectDescriptors.Of(sendHome), "card", true, 0) };
            // 7 and 5 attack; two 2 Force bodies at home. With the 7 sent home, one body holds (5 is not more than 2 + 5) and dies; two bodies hold and both die (4 < 5): the fewest bodies
            // behind on Provinces (2 against 3), so a Province is not a price to pay and the card is spent to hold
            var them = Side(1, [U("t1", 1, 7, province: 0), U("t2", 1, 5, province: 0)], [(0, 7), (1, 7), (2, 7)]); var me = Side(0, [U("m1", 0, 2), U("m2", 0, 2)], [(0, 5), (1, 5)], hand: 1, handAnswers: hand);
            var plan = Defence.Of(ReadOf(me, them), null, [0, 1]);
            Check(!plan.Nobody && plan.Assignments.Count == 1 && plan.Why.Contains("presence"), $"the 7 is sent home, 5 is not more than one body plus 5: presence: {plan}");
            var noHand = Defence.Of(ReadOf(me with { HandAnswers = [], HandCount = 0 }, them), null, [0, 1]);
            Check(noHand.Nobody, $"without the card 12 beats 4 + 5: nobody: {noHand}");
            // three bodies beat the 5 once the 7 is gone (6 > 5): defend to kill with the smallest group that does
            var three = Side(0, [U("m1", 0, 2), U("m2", 0, 2), U("m3", 0, 2)], [(0, 5), (1, 5)], hand: 1, handAnswers: hand);
            var kill = Defence.Of(ReadOf(three, them), null, [0, 1]);
            Check(kill.Assignments.Count == 3 && kill.Why.Contains("kill"), $"6 beats 5: kill with all three: {kill}");
            // ahead on Provinces (3 against 2) and still holding 2 after the loss: the same hold would only spend the card for no kill, so the Province is conceded (PLAYER_MODEL.md 3.3)
            var ahead = Defence.Of(ReadOf(me with { Provinces = [(0, 5), (1, 5), (2, 5)] }, them with { Provinces = [(0, 7), (1, 7)] }), null, [0, 1, 2]);
            Check(ahead.Nobody && ahead.Why.Contains("concede"), $"ahead on Provinces: concede rather than spend the card: {ahead}");
        }),
        ("the last Province takes the highest hold even at a cost; their last Province is conceded when my attack razes it next turn", () =>
        {
            var them = Side(1, [U("t1", 1, 9, province: 0), U("t2", 1, 3, province: 0)], [(0, 7), (1, 7)]); var me = Side(0, [U("m1", 0, 5), U("m2", 0, 3)], [(0, 5)]);
            var plan = Defence.Of(ReadOf(me, them), null, [0]);
            Check(plan.Assignments.Count == 2 && plan.Why.Contains("last Province"), $"12 against 8 + 5: everybody for the highest hold: {plan}");
            var concede = Defence.Of(ReadOf(me with { Provinces = [(0, 5), (1, 5)] }, them with { Provinces = [(0, 7)] }), null, [0], concedeLast: true);
            Check(concede.Nobody && concede.Why.Contains("their last Province"), $"their last Province falls to me next turn: concede: {concede}");
        }),
        ("my last Province when nothing holds at likely: the body goes anyway (it unlocks the hand and narrows the margin); a hold that loses six to kill three is conceded while ahead, taken when behind", () =>
        {
            var them = Side(1, [U("t1", 1, 12, province: 0), U("t2", 1, 10, province: 0)], [(0, 7), (1, 7)]); var me = Side(0, [U("m1", 0, 4), U("m2", 0, 2)], [(0, 7)]);
            var last = Defence.Of(ReadOf(me, them), null, [0]);
            Check(last.Assignments.Count == 2 && last.Why.Contains("last Province"), $"22 against 6 + 7 falls under every hand, but the bodies narrow it and unlock the hand: {last}");
            var six = Side(0, Enumerable.Range(1, 6).Select(i => U($"m{i}", 0, 3)).ToArray(), [(0, 5), (1, 5), (2, 5)]); var three = Side(1, [U("t1", 1, 8, province: 0), U("t2", 1, 7, province: 0), U("t3", 1, 7, province: 0)], [(0, 7), (1, 7)]);
            var costly = Defence.Of(ReadOf(six, three), null, [0, 1, 2]);
            Check(costly.Nobody && costly.Why.Contains("more units than it destroys"), $"18 against 22 + 5: holding needs the whole army and the exchange has it die: concede while ahead: {costly}");
            var behind = Defence.Of(ReadOf(six with { Provinces = [(0, 5), (1, 5)] }, three with { Provinces = [(0, 7), (1, 7), (2, 7)] }), null, [0, 1]);
            Check(!behind.Nobody, $"behind on Provinces the same hold is taken at its cost: {behind}");
        }),
        ("safe on Strength: a 5 against Strength 6 needs nobody, unless one body can destroy it without a loss", () =>
        {
            var them = Side(1, [U("t1", 1, 5, province: 0)], [(0, 7), (1, 7)]);
            var weak = Defence.Of(ReadOf(Side(0, [U("m1", 0, 2), U("m2", 0, 1)], [(0, 6), (1, 6)]), them), null, [0, 1]);
            Check(weak.Nobody && weak.Why.Contains("safe"), $"5 is not more than 6 and my bodies would only die: {weak}");
            var sweep = Defence.Of(ReadOf(Side(0, [U("m1", 0, 7), U("m2", 0, 1)], [(0, 6), (1, 6)]), them), null, [0, 1]);
            Check(sweep.Assignments.Count == 1 && sweep.Assignments.ContainsKey("m1") && sweep.Why.Contains("destroys"), $"the 7 beats the 5 and kills it: one body: {sweep}");
        }),
        ("across Provinces: the bigger attack is answered first and the units used are gone for the next", () =>
        {
            var them = Side(1, [U("t1", 1, 6, province: 0), U("t2", 1, 3, province: 1)], [(0, 7)]); var me = Side(0, [U("m1", 0, 7), U("m2", 0, 2)], [(0, 5), (1, 5), (2, 5)]);
            var plan = Defence.Of(ReadOf(me, them), null, [0, 1]);
            Check(plan.Assignments.TryGetValue("m1", out var p1) && p1 == 0, $"the 7 answers the 6 at P1: {plan}");
            Check(plan.Log.Length == 2 && plan.Log[0].StartsWith("P1") && plan.Log[1].StartsWith("P2"), $"two Provinces read, the bigger first: {string.Join(" || ", plan.Log)}");
        }),
    ];
}
