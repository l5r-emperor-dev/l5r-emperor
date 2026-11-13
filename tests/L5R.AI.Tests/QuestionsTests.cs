using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.5): the three questions and the posture on constructed positions. The gates of PLAYER_MODEL.md 3.1 (nothing exceeds Strength;
// nobody unbowed to defend); the sufficient group is the smallest that razes; a split exists when two groups each exceed a Province; the lone immune unit when every shown answer is
// conditional; the clocks at the current rates; the postures of RULE_FAMILIES.md family G in their order.
internal static class QuestionsTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, bool bowed = false, int attachments = 0) => new(id, id, owner, force, force, bowed, false, null, [], [], 2, 0, false, false, attachments, 0, 0);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int honor = 10, int hand = 0, int gold = 4) => new(id, units, provinces, gold, gold, honor, false, hand, [], []);
    private static Read ReadOf(LedgerSide me, LedgerSide them, Belief? belief = null) => new(new Ledger(me.Id, 7, Phase.AttackDeclaration, me, them), belief ?? new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);

    public static readonly (string, Action)[] Tests =
    [
        ("gate: no group exceeds Strength even undefended -> hold; nobody unbowed to defend -> attack", () =>
        {
            var weak = ReadOf(Side(0, [U("m1", 0, 3), U("m2", 0, 2)], [(0, 7)]), Side(1, [U("t1", 1, 2)], [(0, 7)]));
            var a = Questions.Of(weak);
            Check(!a.Opportunity.Attacks && a.Opportunity.Gate.StartsWith("hold: no group"), $"5 cannot exceed 7: {a.Opportunity}");
            var open = ReadOf(Side(0, [U("m1", 0, 8)], [(0, 7)]), Side(1, [U("t1", 1, 9, bowed: true)], [(0, 5), (1, 5)]));
            var b = Questions.Of(open);
            Check(b.Opportunity.Attacks && b.Opportunity.Gate.StartsWith("attack"), $"nobody unbowed: attack with the 8: {b.Opportunity}");
            Check(b.Posture == Posture.Punish, $"their Personalities are bowed: punish, not {b.Posture}");
        }),
        ("the sufficient group is the smallest that razes; everything is also offered; the small group's exposure is the lower", () =>
        {
            // their bowed 9 straightens for the counter-attack: an attack that empties my home exposes a Province of Strength 5
            var me = Side(0, [U("m1", 0, 9), U("m2", 0, 4), U("m3", 0, 2)], [(0, 5), (1, 5), (2, 5)]); var them = Side(1, [U("t1", 1, 2), U("t2", 1, 9, bowed: true)], [(0, 5), (1, 6), (2, 7)]);
            var a = Questions.Of(ReadOf(me, them));
            var sufficient = a.Opportunity.Options.FirstOrDefault(o => o.Label == "sufficient");
            Check(sufficient is not null && sufficient.Assignment.Count == 1 && sufficient.Assignment.ContainsKey("m1"), $"the 9 alone razes Strength 5 past the 2: {string.Join(" | ", a.Opportunity.Options.Select(o => o.ToString()))}");
            Check(a.Opportunity.Options.Any(o => o.Label == "everything" && o.Assignment.Count == 3), "everything is a candidate too");
            var everything = a.Opportunity.Options.Single(o => o.Label == "everything");
            Check(sufficient!.ExposureCost < everything.ExposureCost && a.Opportunity.Attacks, $"the small group leaves a home that holds; everything leaves none: {sufficient} / {everything}");
        }),
        ("a split across two Provinces is a candidate when each group exceeds its Strength; the defender's reply is the best for it", () =>
        {
            var me = Side(0, [U("m1", 0, 8), U("m2", 0, 8)], [(0, 7)]); var them = Side(1, [U("t1", 1, 3)], [(0, 5), (1, 5)]);
            var a = Questions.Of(ReadOf(me, them));
            var split = a.Opportunity.Options.FirstOrDefault(o => o.Label == "split");
            Check(split is not null && split.Assignment.Values.Distinct().Count() == 2, $"8 and 8 at two Provinces of 5: {string.Join(" | ", a.Opportunity.Options.Select(o => o.ToString()))}");
            Check(split!.ExpectedProvinces >= 1.0, $"one 8 is undefended whatever the 3 does: at least one Province expected: {split}");
        }),
        ("the lone immune unit: every answer they have shown is conditional and one unit fails all of them", () =>
        {
            var me = Side(0, [U("m1", 0, 6), U("m2", 0, 3)], [(0, 7)]); var them = Side(1, [U("t1", 1, 4)], [(0, 5)]);
            var belief = new Belief(4, Belief.Prior, 2, [[new("force-at-most", 4, "Force 4 or less")]], 1);
            var a = Questions.Of(ReadOf(me, them, belief));
            var lone = a.Opportunity.Options.FirstOrDefault(o => o.Label == "lone");
            Check(lone is not null && lone.Assignment.Count == 1 && lone.Assignment.ContainsKey("m1"), $"the 6 fails 'Force 4 or less' and goes alone: {string.Join(" | ", a.Opportunity.Options.Select(o => o.ToString()))}");
            var none = Questions.Of(ReadOf(me, them, belief with { AnswersSeen = 2 }));
            Check(none.Opportunity.Options.All(o => o.Label != "lone"), "an unconditional answer shown: nobody is immune");
        }),
        ("clocks: Honor at its rate, the drain at theirs, the military at the attack's expected Provinces; 99 when a route is closed", () =>
        {
            var me = Side(0, [U("m1", 0, 9)], [(0, 7)], honor: 34); var them = Side(1, [], [(0, 5), (1, 5)], honor: -14);
            var a = Questions.Of(ReadOf(me, them), new Trend(3, -3, 0, 0));
            Check(a.Clocks.MyHonor == 2, $"34 at +3 a turn reaches 40 in 2: {a.Clocks}");
            Check(a.Clocks.MyDrain == 2, $"-14 at -3 a turn reaches -20 in 2: {a.Clocks}");
            Check(a.Clocks.TheirHonor == Clocks.Never && a.Clocks.TheirDrain == Clocks.Never, $"their Honor falls and mine rises: both of their Honor routes are closed: {a.Clocks}");
            Check(a.Clocks.MyMilitary >= 2 && a.Clocks.MyMilitary < Clocks.Never, $"two Provinces at the attack's rate: {a.Clocks}");
        }),
        ("postures in family G's order: lock when my Honor clock beats their fastest and home holds; drain when the drain is my route; race when my military clock is a turn shorter; press on a razing attack; build when their answers beat my units", () =>
        {
            var quietThem = Side(1, [U("t1", 1, 2)], [(0, 7), (1, 7), (2, 7), (3, 7)], honor: 10);
            var lockMe = Side(0, [U("m1", 0, 5)], [(0, 7), (1, 7)], honor: 34);
            Check(Questions.Of(ReadOf(lockMe, quietThem), new Trend(3, 0, 0, 0)).Posture == Posture.Lock, "Honor 34 at +3 against a quiet opponent: lock");
            var drainThem = quietThem with { Honor = -14 };
            Check(Questions.Of(ReadOf(lockMe with { Honor = 10 }, drainThem), new Trend(0, -3, 0, 0)).Posture == Posture.Drain, "their Honor -14 at -3 a turn: drain");
            var raceMe = Side(0, [U("m1", 0, 12), U("m2", 0, 10)], [(0, 7), (1, 7)], honor: 10); var raceThem = Side(1, [U("t1", 1, 2)], [(0, 5)], honor: 10);
            var race = Questions.Of(ReadOf(raceMe, raceThem));
            Check(race.Posture == Posture.Press, $"their last Province falls to my best attack: press, not {race.Posture} ({race.Why})");
            var pressMe = Side(0, [U("m1", 0, 12), U("m2", 0, 10)], [(0, 7), (1, 7), (2, 7)], honor: 10); var pressThem = Side(1, [U("t1", 1, 2)], [(0, 5), (1, 5), (2, 5)], honor: 10);
            var press = Questions.Of(ReadOf(pressMe, pressThem));
            Check(press.Posture is Posture.Press or Posture.Race, $"22 against 2 + 5 on three Provinces: press or race, not {press.Posture} ({press.Why})");
            var buildMe = Side(0, [U("m1", 0, 4), U("m2", 0, 3)], [(0, 7), (1, 7), (2, 7)], honor: 10); var buildThem = Side(1, [U("t1", 1, 8), U("t2", 1, 6)], [(0, 7), (1, 7), (2, 7)], honor: 10);
            var build = Questions.Of(ReadOf(buildMe, buildThem));
            Check(build.Posture is Posture.Build or Posture.Hold && !build.Opportunity.Attacks, $"7 against 14 + 7: no attack; build or hold, not {build.Posture} ({build.Why}): {build.Opportunity}");
        }),
    ];
}
