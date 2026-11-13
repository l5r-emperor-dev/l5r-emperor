using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.5, question 2): threat(home) on constructed positions. Their army straightens before it attacks (a bowed unit counts its full
// Force, a spent ability is fresh); mine does not (a unit that attacked stays bowed and cannot defend); they pick the attack worst for me, I the defence best for me; a split they can
// make across two Provinces costs me one when I can only cover one; a send-home in my hand with a body at home turns a loss into a hold; the tiers follow RULE_FAMILIES.md family C.
internal static class ThreatTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static CardDefinition Person(int force) => BattlePolicyTests.Person(force);
    private static LedgerUnit U(string id, int owner, int force, bool bowed = false, int? province = null, string[]? abilities = null, string[]? spent = null, int followers = 0, int followerForce = 0) =>
        new(id, id, owner, force, force + followerForce, bowed, false, province, (abilities ?? []).Select(EffectDescriptors.Of).ToArray(), spent ?? [], 2, 0, false, false, followers, followers, 0);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int goldNow = 0, int capacity = 0, int hand = 0, Answer[]? handAnswers = null) => new(id, units, provinces, goldNow, capacity, 10, false, hand, handAnswers ?? [], []);
    private static Read ReadOf(LedgerSide me, LedgerSide them) => new(new Ledger(me.Id, 5, Phase.Dynasty, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static string FirstAbility(Func<EffectDescriptor, bool> test) => AbilityCatalog.All.Keys.Where(k => !k.StartsWith("training-") && !k.StartsWith("belief-")).Select(EffectDescriptors.Of).Where(d => d.IsBattle && test(d)).OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;

    public static readonly (string, Action)[] Tests =
    [
        ("their army straightens: a bowed 6 counts its full Force next turn and razes my weakest Province past my unbowed home; a 6 of mine that attacked stays bowed and does not defend", () =>
        {
            var mine = new[] { U("m-attacked", 0, 6, bowed: true), U("m-home", 0, 2) };
            var theirs = new[] { U("t-bowed", 1, 6, bowed: true), U("t-up", 1, 3) };
            var me = Side(0, mine, [(0, 5), (1, 7)]); var them = Side(1, theirs, [(0, 7)], capacity: 6);
            var read = ReadOf(me, them);
            var home = Threat.Defenders(mine); Check(home.Length == 1 && home[0].Id == "m-home", "only the unbowed unit at home defends");
            var army = Threat.Straightened(theirs); Check(army.All(u => !u.Bowed) && army.Sum(u => u.UnitForce) == 9, "their whole army straightens to 9");
            var t = Threat.Of(read, home, army);
            Check(t.AttackForce == 9 && t.HomeForce == 2 && t.WeakestStrength == 5, $"9 against 2 + 5: {t}");
            Check(t.Open.ProvincesLost == 1 && t.Open.Tier == 0, $"9 > 2 + 5 with nobody to stop it: {t.Open}");
            var holds = Threat.Of(read, Threat.Defenders([U("m-up", 0, 6, bowed: false), U("m-home", 0, 2)]), army);
            Check(holds.Open.ProvincesLost == 0 && holds.Open.Tier >= 1, $"had the 6 stayed home, 9 is not more than 8 + 5: {holds.Open}");
        }),
        ("a spent ability is fresh on their turn, and a unit my attack is expected to destroy is left out", () =>
        {
            var bow = FirstAbility(x => x.Class == EffectClass.Bow && x.Side == TargetSide.Enemy && x.Conditions.Length == 0 && x.GoldCost == 0);
            var theirs = new[] { U("t-bower", 1, 4, bowed: true, abilities: [bow], spent: [bow]), U("t-big", 1, 5, bowed: true) };
            var army = Threat.Straightened(theirs);
            Check(army.Single(u => u.Id == "t-bower").Spent.Length == 0 && army.Single(u => u.Id == "t-bower").UsableBattle.Any(), "the bow is usable again on their turn");
            var without = Threat.Straightened(theirs, ["t-big"]); Check(without.Length == 1 && without[0].Id == "t-bower", "the unit my attack kills does not come");
            var me = Side(0, [U("m-home", 0, 5)], [(0, 4)]); var them = Side(1, theirs, [(0, 7)], capacity: 4);
            var read = ReadOf(me, them);
            var t = Threat.Of(read, Threat.Defenders(me.Units), army);
            Check(t.Open.ProvincesLost == 1, $"9 with a bow against my lone 5 at Strength 4: the bow takes the 5 out and 9 > 0 + 4: {t.Open}");
            var t2 = Threat.Of(read, Threat.Defenders(me.Units), without);
            Check(t2.Open.ProvincesLost == 0, $"the 4 alone, bowing my 5, is not more than 0 + 4: {t2.Open}");
        }),
        ("a split across two Provinces costs me one when I can cover only one; all-in is answered by everybody at that battlefield and the attackers die", () =>
        {
            var theirs = new[] { U("t1", 1, 8), U("t2", 1, 8) }; var mine = new[] { U("m1", 0, 12) };
            var me = Side(0, mine, [(0, 5), (1, 5)]); var them = Side(1, theirs, [(0, 7)], capacity: 4);
            var t = Threat.Of(ReadOf(me, them), Threat.Defenders(mine), Threat.Straightened(theirs));
            Check(t.Candidates == 3, $"two all-ins and one split: {t.Candidates}");
            Check(t.Open.ProvincesLost == 1 && t.Open.Attack.Contains(';') && t.Open.AttackersLost == 1, $"all-in 16 is not more than 12 + 5; 8 at each Province, my 12 holds one and kills, the other falls: {t.Open}");
            var single = Threat.Of(ReadOf(me, them), Threat.Defenders(mine), Threat.Straightened([U("t1", 1, 8)]));
            Check(single.Open.ProvincesLost == 0 && single.Open.AttackersLost == 1 && single.Open.Tier == 3, $"one 8 into my 12: held and destroyed, the best tier: {single.Open}");
        }),
        ("a send-home in my hand with a body at home turns a loss into a hold, and nobody at home leaves the card unplayable", () =>
        {
            var sendHome = FirstAbility(x => x.Class == EffectClass.SendHome && x.Side == TargetSide.Enemy && x.Conditions.Length == 0 && x.GoldCost == 0);
            var hand = new[] { new Answer(EffectDescriptors.Of(sendHome), "card", true, 0) };
            var theirs = new[] { U("t1", 1, 6), U("t2", 1, 2) };
            var withBody = Side(0, [U("m1", 0, 1)], [(0, 4)], hand: 1, handAnswers: hand); var them = Side(1, theirs, [(0, 7)], capacity: 4);
            var t = Threat.Of(ReadOf(withBody, them), Threat.Defenders(withBody.Units), Threat.Straightened(theirs));
            Check(t.Open.ProvincesLost == 0 && t.Open.Tier >= 1, $"8 against 1 + 4, but the 6 is sent home and 2 is not more than 1 + 4: {t.Open}");
            var noBody = Side(0, [], [(0, 4)], hand: 1, handAnswers: hand);
            var t2 = Threat.Of(ReadOf(noBody, them), Threat.Defenders(noBody.Units), Threat.Straightened(theirs));
            Check(t2.Open.ProvincesLost == 1, $"the same card with nobody to unlock it: the Province falls: {t2.Open}");
        }),
        ("the tiers order outcomes for the defender: lose a Province < hold and lose a defender < hold and lose nobody < hold and destroy an attacker; the worst hand never reads better than the open one", () =>
        {
            var lose = new ThreatCase(1, 0, 0, "", ""); var holdLose = new ThreatCase(0, 1, 0, "", ""); var hold = new ThreatCase(0, 0, 0, "", ""); var kill = new ThreatCase(0, 0, 1, "", ""); var trade = new ThreatCase(0, 1, 1, "", "");
            Check(lose.Tier == 0 && holdLose.Tier == 1 && hold.Tier == 2 && kill.Tier == 3 && trade.Tier == 1, "tiers are cumulative: a kill that cost a defender is tier 1");
            Check(ThreatCase.Compare(holdLose, lose) > 0 && ThreatCase.Compare(hold, holdLose) > 0 && ThreatCase.Compare(kill, hold) > 0, "compare orders by Provinces, then defenders lost, then attackers destroyed");
            Check(ThreatCase.Compare(lose, kill) < 0 && ThreatCase.Compare(hold, hold) == 0, "a lost Province is worse than anything held");
            var theirs = new[] { U("t1", 1, 5), U("t2", 1, 4) }; var mine = new[] { U("m1", 0, 6), U("m2", 0, 2) };
            var me = Side(0, mine, [(0, 4), (1, 6)]); var them = Side(1, theirs, [(0, 7)], capacity: 6, hand: 6);
            var t = Threat.Of(ReadOf(me, them), Threat.Defenders(mine), Threat.Straightened(theirs));
            Check(ThreatCase.Compare(t.Open, t.Worst) >= 0 && ThreatCase.Compare(t.Likely, t.Worst) >= 0, $"the upper bound is no better for me than the open hand: open {t.Open} likely {t.Likely} worst {t.Worst}");
        }),
        ("no army or no Province means no threat", () =>
        {
            var me = Side(0, [U("m1", 0, 3)], [(0, 4)]); var them = Side(1, [], [(0, 7)]);
            var t = Threat.Of(ReadOf(me, them), Threat.Defenders(me.Units), Threat.Straightened(them.Units));
            Check(t.Candidates == 0 && t.Open.ProvincesLost == 0 && t.HomeForce == 3 && t.Cell == "000" && t.Gap == -7, $"nothing to attack with: {t}");
        }),
    ];
}
