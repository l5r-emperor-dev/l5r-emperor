using L5R.AI;
using L5R.Core;

// Issue #1206 (strategy brief step 3, family E; RULE_FAMILIES.md E; PLAYER_MODEL.md 3.5): the Fight on constructed battle positions. Pass while the battle as it stands gives the result wanted
// (cards unused); a free printed destroy is taken in a won battle and a send-home never (kill, do not repel); the cheapest sufficient action, printed before hand; late Force waits while they have an
// unbowed unit here and have not passed; a lost battle spends nothing; a deciding battle fights on; when several actions are needed the first disables, the send-home comes last; the target
// carrying unspent abilities first.
internal static class FightTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, bool bowed = false, int? province = 0, string[]? abilities = null, string[]? spent = null, int chi = 2) =>
        new(id, id, owner, force, force, bowed, false, province, (abilities ?? []).Select(EffectDescriptors.Of).ToArray(), spent ?? [], chi, 0, false, false, 0, 0, 2, null);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int goldNow = 4, int hand = 0, Answer[]? handAnswers = null) =>
        new(id, units, provinces, goldNow, goldNow, 20, false, hand, handAnswers ?? [], [], 0);
    private static Read ReadOf(LedgerSide me, LedgerSide them) => new(new Ledger(me.Id, 5, Phase.Battle, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static EffectDescriptor E(string id, EffectClass cls, int? magnitude = null, bool bowsSource = false, int gold = 0, EffectClass[]? secondary = null) =>
        new(id, "Battle", null, cls, magnitude, false, cls == EffectClass.ForceAdd ? TargetSide.Own : TargetSide.Enemy, cls == EffectClass.ForceAdd ? TargetKind.None : TargetKind.Personality, [], secondary ?? [], null, true, true, bowsSource, false, false, gold, 0, false, false, 1, false, null);
    private static FightOption Printed(string key, string unit, EffectClass cls, string? target, int? magnitude = null, bool bowsSource = false) => new(key, new(E(key, cls, magnitude, bowsSource), unit, false, 0), target);
    private static FightOption Hand(string key, EffectClass cls, string? target, int gold = 1, int? magnitude = null) => new(key, new(E(key, cls, magnitude, gold: gold), "card " + key, true, gold), target);
    /// <summary>A catalogue ability that neutralises in battle, for a unit that "carries unspent abilities".</summary>
    private static string Neutraliser() => AbilityCatalog.All.Keys.Where(k => !k.StartsWith("training-") && !k.StartsWith("belief-")).Select(EffectDescriptors.Of).First(d => d.IsBattle && d.Neutralises && d.Class == EffectClass.Bow).Id;

    public static readonly (string, Action)[] Tests =
    [
        ("won as it stands: defending 6 against 4 at Strength 3, a send-home in hand: pass, the card unused", () =>
        {
            var me = Side(0, [U("m6", 0, 6)], [(0, 3)]); var them = Side(1, [U("t4", 1, 4)], [(0, 5)]);
            var plan = Fight.Of(ReadOf(me, them), 0, false, false, false, [Hand("sendhome", EffectClass.SendHome, "t4")]);
            Check(plan.Pass && plan.Why.Contains("won as it stands"), $"pass expected: {plan.Why}");
            Check(Fight.Tier(Exchange.Run(them, them.Units, me, me.Units, 3, [], []), false) == 3, "6 against 4 holds and destroys the attacker: tier 3");
        }),
        ("kill, do not repel: the same won battle with a free printed destroy: the destroy is taken; the send-home never", () =>
        {
            var me = Side(0, [U("m6", 0, 6)], [(0, 3)]); var them = Side(1, [U("t4", 1, 4)], [(0, 5)]);
            var plan = Fight.Of(ReadOf(me, them), 0, false, false, false, [Hand("sendhome", EffectClass.SendHome, "t4"), Printed("kill", "m6", EffectClass.Destroy, "t4")]);
            Check(plan.Pick == "kill", $"the free destroy: {plan.Pick} ({plan.Why})");
            var bowOnly = Fight.Of(ReadOf(me, them), 0, false, false, false, [Hand("sendhome", EffectClass.SendHome, "t4"), Printed("bow", "m6", EffectClass.Bow, "t4")]);
            Check(bowOnly.Pass, $"a free bow that changes nothing (the attacker dies anyway) is not spent: {bowOnly.Pick} ({bowOnly.Why})");
        }),
        ("the cheapest sufficient action: defending 4 against 7 at Strength 3 (falls); a printed bow and a hand send-home both restore it: the printed bow", () =>
        {
            var me = Side(0, [U("m4", 0, 4)], [(0, 3)]); var them = Side(1, [U("t7", 1, 7)], [(0, 5)]);
            var plan = Fight.Of(ReadOf(me, them), 0, false, false, false, [Hand("sendhome", EffectClass.SendHome, "t7"), Printed("bow", "m4", EffectClass.Bow, "t7")]);
            Check(plan.Pick == "bow" && plan.Why.Contains("alone"), $"the printed bow: {plan.Pick} ({plan.Why})");
            var handOnly = Fight.Of(ReadOf(me, them), 0, false, false, false, [Hand("sendhome", EffectClass.SendHome, "t7")]);
            Check(handOnly.Pick == "sendhome", $"with only the hand card, it is played: {handOnly.Pick} ({handOnly.Why})");
        }),
        ("late Force: attacking 5 against 3 at Strength 3 (held), a +2 from hand would take it: it waits while they have an unbowed unit here and have not passed, and is added after their pass", () =>
        {
            var force = Hand("force2", EffectClass.ForceAdd, null, magnitude: 2);
            var me = Side(0, [U("m5", 0, 5)], [(0, 3)], hand: 1, handAnswers: [force.Answer]); var them = Side(1, [U("t3", 1, 3)], [(0, 3)]);
            var wait = Fight.Of(ReadOf(me, them), 0, true, false, false, [force]);
            Check(wait.Pass && wait.Why.Contains("late Force"), $"waits: {wait.Pick} ({wait.Why})");
            var now = Fight.Of(ReadOf(me, them), 0, true, true, false, [force]);
            Check(now.Pick == "force2", $"after their pass the Force is added: {now.Pick} ({now.Why})");
            var bowed = Fight.Of(ReadOf(me, Side(1, [U("t3", 1, 3, bowed: true)], [(0, 5)])), 0, true, false, false, [force]);
            Check(bowed.Pick == "force2", $"with no unbowed defender (Strength 5, 5 does not beat it alone) the Force is added at once: {bowed.Pick} ({bowed.Why})");
        }),
        ("a lost battle spends nothing when nothing lifts it; a card is spent to save the Province or the army (tier 0 to 1 or 2), not to save defenders behind a held Province; a free table ability lifts any tier", () =>
        {
            var sendhome = Hand("sendhome", EffectClass.SendHome, "t6a");
            var me = Side(0, [U("m2", 0, 2)], [(0, 3)], hand: 1, handAnswers: [sendhome.Answer]); var them = Side(1, [U("t6a", 1, 6), U("t6b", 1, 6)], [(0, 5)]);
            var lost = Fight.Of(ReadOf(me, them), 0, false, false, false, [sendhome]);
            Check(lost.Pass && lost.Why.Contains("spend nothing"), $"lost: {lost.Pick} ({lost.Why})");
            var cut = Hand("cut4", EffectClass.ForceCut, "t8", gold: 2, magnitude: 4);
            var me3 = Side(0, [U("m3", 0, 3)], [(0, 3)], hand: 1, handAnswers: [cut.Answer]); var them8 = Side(1, [U("t8", 1, 8)], [(0, 5)]);
            var ordinary = Fight.Of(ReadOf(me3, them8), 0, false, false, false, [cut]);
            Check(ordinary.Pick == "cut4", $"the cut saves the Province (tier 0 to 1) for one card: spent: {ordinary.Pick} ({ordinary.Why})");
            var deciding = Fight.Of(ReadOf(me3, them8), 0, false, false, true, [cut]);
            Check(deciding.Pick == "cut4", $"on the last Province the cut is played: {deciding.Pick} ({deciding.Why})");
            // holding but losing the defenders (tier 1): one card that saves them (tier 2) is spent, the result wanted; a free table bow is preferred to it
            var saves = Hand("save", EffectClass.Bow, "t5", gold: 1);
            var me4 = Side(0, [U("m4", 0, 4)], [(0, 3)], hand: 1, handAnswers: [saves.Answer]); var them5 = Side(1, [U("t5", 1, 5)], [(0, 5)]);
            var holds = Fight.Of(ReadOf(me4, them5), 0, false, false, false, [saves]);
            Check(holds.Pick == "save" && holds.Why.Contains("alone"), $"5 against 4 at Strength 3 holds the Province but the defender dies; the bow saves him alone: {holds.Pick} ({holds.Why})");
            var tableBow = new FightOption("tablebow", new(E("tablebow", EffectClass.Bow), "Holding", false, 0), "t5");
            var free = Fight.Of(ReadOf(me4, them5), 0, false, false, false, [saves, tableBow]);
            Check(free.Pick == "tablebow", $"a Holding's free bow does the same at no cost and comes first: {free.Pick} ({free.Why})");
            // a card that only lifts a lost tier short of the want is spent only to save the Province or the army: holding at tier 1 with a card that cannot reach 2 passes
            var cutSmall = Hand("cut1", EffectClass.ForceCut, "t5", gold: 1, magnitude: 1);
            var me4b = Side(0, [U("m4", 0, 4)], [(0, 3)], hand: 1, handAnswers: [cutSmall.Answer]);
            var short1 = Fight.Of(ReadOf(me4b, them5), 0, false, false, false, [cutSmall]);
            Check(short1.Pass, $"a -1 leaves 4 against 4 (a tie: both die) and reaches no better tier for the card: {short1.Pick} ({short1.Why})");
            // attacking and losing the army (tier 0): a hand send-home of the only defender wins the battle (tier 2) and is spent, though the Province still stands
            var away = Hand("away", EffectClass.SendHome, "d8", gold: 2);
            var meA = Side(0, [U("a5", 0, 5), U("a2", 0, 2)], [(0, 3)], hand: 1, handAnswers: [away.Answer]); var themD = Side(1, [U("d8", 1, 8)], [(0, 9)]);
            var army = Fight.Of(ReadOf(meA, themD), 0, true, false, false, [away]);
            Check(army.Pick == "away", $"7 against 8: the army dies; the send-home saves it (tier 0 to 2) for a card: {army.Pick} ({army.Why})");
            // attacking 2 against 6 at Strength 6: the Province is out of reach, but a hand destroy on the 6 and then a +5 win the battle (tier 2): the sequence is started with the destroy
            var kill6 = Hand("kill6", EffectClass.Destroy, "d6", gold: 2); var plus5 = Hand("plus5", EffectClass.ForceAdd, null, gold: 0, magnitude: 5);
            var meB = Side(0, [U("a2", 0, 2)], [(0, 3)], hand: 2, handAnswers: [kill6.Answer, plus5.Answer]); var themB = Side(1, [U("d6", 1, 6), U("d4", 1, 4)], [(0, 6)]);
            var seq = Fight.Of(ReadOf(meB, themB), 0, true, false, false, [kill6, plus5]);
            Check(seq.Pick == "kill6" && seq.Why.Contains("first of several") && seq.Why.Contains("tier 2"), $"the destroy first, towards the won battle: {seq.Pick} ({seq.Why})");
        }),
        ("several actions needed: defending 4 against 6 and 5 at Strength 3; a printed bow on the 6 and a hand send-home on the 5 each leave the defenders dying; both hold with nobody lost: the bow first, the send-home last", () =>
        {
            var sendhome = Hand("sendhome", EffectClass.SendHome, "t5");
            var me = Side(0, [U("m4", 0, 4)], [(0, 3)], hand: 1, handAnswers: [sendhome.Answer]); var them = Side(1, [U("t6", 1, 6), U("t5", 1, 5)], [(0, 5)]);
            var plan = Fight.Of(ReadOf(me, them), 0, false, false, false, [sendhome, Printed("bow", "m4", EffectClass.Bow, "t6")]);
            Check(plan.Pick == "bow" && plan.Why.Contains("first of several"), $"the bow first: {plan.Pick} ({plan.Why})");
            var after = Side(1, [U("t6", 1, 6, bowed: true), U("t5", 1, 5)], [(0, 5)]);
            var next = Fight.Of(ReadOf(me, after), 0, false, false, false, [sendhome]);
            Check(next.Pick == "sendhome" && next.Why.Contains("alone"), $"then the send-home restores it alone: {next.Pick} ({next.Why})");
        }),
        ("the target: of two equal attackers the one carrying an unspent Battle ability is bowed first", () =>
        {
            var ability = Neutraliser();
            var me = Side(0, [U("m4", 0, 4)], [(0, 3)]); var them = Side(1, [U("plain", 1, 5), U("carrier", 1, 5, abilities: [ability])], [(0, 5)]);
            var plan = Fight.Of(ReadOf(me, them), 0, false, false, false, [Printed("bowPlain", "m4", EffectClass.Bow, "plain"), Printed("bowCarrier", "m4", EffectClass.Bow, "carrier")]);
            Check(plan.Pick == "bowCarrier", $"the carrier first: {plan.Pick} ({plan.Why}) | {string.Join(" || ", plan.Log)}");
        }),
    ];
}
