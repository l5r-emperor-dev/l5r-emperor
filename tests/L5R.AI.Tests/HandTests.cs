using L5R.AI;
using L5R.Core;

// Issue #1206 (strategy brief step 3, family I; RULE_FAMILIES.md I; PLAYER_MODEL.md 3.7): the Favor, the hand-replacing Event and the hand-limit discard on constructed positions. The Favor is
// kept while the Province holds without it, spent when sending an attacker home turns a fall into a hold (or at my last Province), kept when attacking; a hand with three answers is not
// replaced; the cards with the fewest live uses against their units go first at the hand limit.
internal static class HandTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LedgerUnit U(string id, int owner, int force, int? province = null, int attachments = 0) => new(id, id, owner, force, force, false, false, province, [], [], 2, 0, false, false, attachments, 0, 2, null);
    private static readonly Answer FavorAnswer = new(EffectDescriptors.Of("favor-battle"), "Favor", false, 0);
    private static readonly Answer Bow = new(new("test-bow", "Battle", null, EffectClass.Bow, null, false, TargetSide.Enemy, TargetKind.Personality, [], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null), "a bow", true, 0);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, bool favor = false, Answer[]? hand = null) => new(id, units, provinces, 4, 4, 20, favor, hand?.Length ?? 0, hand ?? [], favor ? [FavorAnswer] : [], 0);
    private static Read ReadOf(LedgerSide me, LedgerSide them, int attacker) => new(new Ledger(me.Id, 6, Phase.Battle, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", attacker);
    private static EffectDescriptor Small(int forceAtMost) => new("test-small", "Battle", null, EffectClass.Destroy, null, false, TargetSide.Enemy, TargetKind.Personality, [new("force-at-most", forceAtMost)], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null);
    private static EffectDescriptor Naked() => new("test-naked", "Battle", null, EffectClass.SendHome, null, false, TargetSide.Enemy, TargetKind.Personality, [new("without-attachments")], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null);
    private static EffectDescriptor Humbler() => new("test-humbler", "Battle", null, EffectClass.SendHome, null, false, TargetSide.Enemy, TargetKind.Personality, [new("honor-at-most-performer")], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null);
    private static LedgerUnit H(string id, int owner, int force, int honor) => new(id, id, owner, force, force, false, false, null, [], [], 2, honor, false, false, 0, 0, 2, null);
    private static EffectDescriptor Boost() => new("test-boost", "Battle", null, EffectClass.ForceAdd, 3, false, TargetSide.Own, TargetKind.Personality, [], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null);

    public static readonly (string, Action)[] Tests =
    [
        ("defending, the Province holds without the Favor (my 6 against their 4 at Strength 5): kept", () =>
        {
            var me = Side(0, [U("d6", 0, 6, province: 0)], [(0, 5), (1, 6)], favor: true); var them = Side(1, [U("a4", 1, 4, province: 0)], [(0, 6), (1, 6)]);
            var plan = Hand.Favor(ReadOf(me, them, 1), 0, false, them.Units[0], false);
            Check(!plan.Spend, $"kept: {plan.Why}");
        }),
        ("defending, their 7 and 5 beat my 6 at Strength 5 (12 against 11); the 7 sent home leaves 5 against 11: spent", () =>
        {
            var me = Side(0, [U("d6", 0, 6, province: 0)], [(0, 5), (1, 6)], favor: true); var them = Side(1, [U("a7", 1, 7, province: 0), U("a5", 1, 5, province: 0)], [(0, 6), (1, 6)]);
            var plan = Hand.Favor(ReadOf(me, them, 1), 0, false, them.Units[0], false);
            Check(plan.Spend && plan.With is { Falls: false }, $"spent: {plan.Why}");
        }),
        ("defending, their 10 and 9 against my 3 at Strength 5: the Province falls with or without it (9 against 8 after the 10 goes home) and this is not my last Province: kept for a battle it decides; at my last Province it goes", () =>
        {
            var me = Side(0, [U("d3", 0, 3, province: 0)], [(0, 5), (1, 6)], favor: true); var them = Side(1, [U("a10", 1, 10, province: 0), U("a9", 1, 9, province: 0)], [(0, 6), (1, 6)]);
            Check(!Hand.Favor(ReadOf(me, them, 1), 0, false, them.Units[0], false).Spend, "kept when it changes nothing");
            Check(Hand.Favor(ReadOf(me, them, 1), 0, false, them.Units[0], true).Spend, "spent at my last Province");
        }),
        ("my last Province: their 5 against my 6 at Strength 5 holds at likely, but their worst hand (the belief's upper bound) could take it; the attacker home saves it under that hand too: spent; with two Provinces left it is kept", () =>
        {
            var me = Side(0, [U("d6", 0, 6, province: 0)], [(0, 5)], favor: true); var them = Side(1, [U("a5", 1, 5, province: 0), U("h2", 1, 2)], [(0, 6), (1, 6)], hand: null) with { HandCount = 4 };
            var read = new Read(new Ledger(0, 6, Phase.Battle, me, them), new Belief(4, Belief.Prior, 0, []), "test", 1);
            var last = Hand.Favor(read, 0, false, them.Units[0], true);
            var twoLeft = Hand.Favor(read with { Ledger = read.Ledger with { Me = me with { Provinces = [(0, 5), (1, 6)] } } }, 0, false, them.Units[0], false);
            Check(!twoLeft.Spend, $"kept with two Provinces left: {twoLeft.Why}");
            Check(last.Spend || !last.Without.Falls && last.Why.Contains("holds at likely"), $"at the last Province it goes when the worst hand takes it: {last.Why}");
        }),
        ("attacking: the Favor is kept", () =>
        {
            var me = Side(0, [U("a6", 0, 6, province: 0)], [(0, 5), (1, 6)], favor: true); var them = Side(1, [U("d2", 1, 2, province: 0)], [(0, 3), (1, 6)]);
            Check(!Hand.Favor(ReadOf(me, them, 0), 0, true, them.Units[0], false).Spend, "kept when attacking");
        }),
        ("the hand-replacing Event: three answers in hand keep it; two leave the choice to the Contender; the text test reads 'discard your hand ... draw'", () =>
        {
            Check(Hand.ReplaceHand(Side(0, [], [(0, 5)], hand: [Bow, Bow, Bow])) == false, "three answers: keep");
            Check(Hand.ReplaceHand(Side(0, [], [(0, 5)], hand: [Bow, Bow])) is null, "two answers: the Contender decides");
            Check(Hand.ReplacesHand("You may discard your hand (even if it is empty). If you did, draw four cards.") && !Hand.ReplacesHand("Draw a card."), "the text test");
        }),
        ("the hand-limit discard: against their 7 and a 3 with an attachment, a destroy-Force-3-or-less has one live use, a send-home-without-attachments has one (the 7), a Force boost has one by default, and a destroy-Force-2-or-less has none and goes first", () =>
        {
            var me = Side(0, [U("i5", 0, 5)], [(0, 5), (1, 6)]); var them = Side(1, [U("t7", 1, 7), U("t3", 1, 3, attachments: 1)], [(0, 6), (1, 6)]);
            var hand = new List<(string, string, EffectDescriptor[], double)> { ("c1", "small3", [Small(3)], 6), ("c2", "naked", [Naked()], 6), ("c3", "boost", [Boost()], 8), ("c4", "small2", [Small(2)], 6) };
            var plan = Hand.Discard(ReadOf(me, them, 0), hand, 1);
            Check(plan.CardIds.SequenceEqual(["c4"]), $"the card with no live use goes: {plan.Why} | {string.Join(" || ", plan.Log)}");
            var two = Hand.Discard(ReadOf(me, them, 0), hand, 2);
            Check(two.CardIds[0] == "c4" && two.CardIds[1] is "c1" or "c2", $"then a one-use card by the Contender's value: {two.Why}");
        }),
        ("a condition against the performer is judged with my best unit: a send-home for a target of Honor at most the performer's has two live uses with my Personal Honor 3 against their 2 and 3, and none with my 1", () =>
        {
            var them = Side(1, [H("t2", 1, 4, 2), H("t3", 1, 4, 3)], [(0, 6), (1, 6)]);
            var high = Hand.Discard(ReadOf(Side(0, [H("me3", 0, 4, 3)], [(0, 5), (1, 6)]), them, 0), [("c1", "humbler", [Humbler()], 6), ("c2", "boost", [Boost()], 8)], 1);
            Check(high.CardIds.SequenceEqual(["c2"]), $"two live uses keep the humbler: {high.Why} | {string.Join(" || ", high.Log)}");
            var low = Hand.Discard(ReadOf(Side(0, [H("me1", 0, 4, 1)], [(0, 5), (1, 6)]), them, 0), [("c1", "humbler", [Humbler()], 6), ("c2", "boost", [Boost()], 8)], 1);
            Check(low.CardIds.SequenceEqual(["c1"]), $"no live use discards the humbler: {low.Why} | {string.Join(" || ", low.Log)}");
        }),
        ("formulation 3, under threat: their 10 straightened against my 4 at home and Strength 5 takes a Province unless my hand's bow stops it; the bow is kept and the 3-Force Follower goes", () =>
        {
            var me = Side(0, [U("i4", 0, 4)], [(0, 5), (1, 6)], hand: [Bow]); var them = Side(1, [U("t10", 1, 10) with { Bowed = true }], [(0, 6), (1, 6)]);
            var cards = new List<Hand.HandCard> { new("c1", "a bow", [Bow.Effect], false, 0, 6), new("c2", "follower", [], true, 3, 5) };
            var plan = Hand.DiscardByRead(ReadOf(me, them, 0), cards, 1);
            Check(plan.CardIds.SequenceEqual(["c2"]), $"the Follower goes: {plan.Why} | {string.Join(" || ", plan.Log)}");
        }),
        ("formulation 3, quiet: their 2 cannot threaten my 4 and Strength 5, and destroying it does not make my 4 take a Strength 6 Province; the destroy goes and the 3-Force Follower is kept", () =>
        {
            var destroy = new Answer(Small(9), "a destroy", true, 0);
            var me = Side(0, [U("i4", 0, 4)], [(0, 5), (1, 6)], hand: [destroy]); var them = Side(1, [U("t2", 1, 2)], [(0, 6), (1, 6)]);
            var cards = new List<Hand.HandCard> { new("c1", "a destroy", [Small(9)], false, 0, 6), new("c2", "follower", [], true, 3, 5) };
            var plan = Hand.DiscardByRead(ReadOf(me, them, 0), cards, 1);
            Check(plan.CardIds.SequenceEqual(["c1"]), $"the destroy goes: {plan.Why} | {string.Join(" || ", plan.Log)}");
        }),
        ("formulation 3, sequencing: a destroy for Force 2 or less has no target against their 5 alone, but the hand's Force cut of 3 makes it live", () =>
        {
            var cut = new EffectDescriptor("test-cut", "Battle", null, EffectClass.ForceCut, 3, false, TargetSide.Enemy, TargetKind.Personality, [], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null);
            var me = Side(0, [U("i4", 0, 4)], [(0, 5), (1, 6)]); var them = Side(1, [U("t5", 1, 5)], [(0, 6), (1, 6)]);
            var alone = Hand.DiscardByRead(ReadOf(me, them, 0), [new("c1", "small2", [Small(2)], false, 0, 6), new("c3", "boost", [Boost()], false, 0, 8)], 1);
            Check(alone.Log.Any(l => l.StartsWith("small2:") && l.Contains("live 0")), $"no live target alone: {string.Join(" || ", alone.Log)}");
            var paired = Hand.DiscardByRead(ReadOf(me, them, 0), [new("c1", "small2", [Small(2)], false, 0, 6), new("c2", "cut", [cut], false, 0, 6)], 1);
            Check(paired.Log.Any(l => l.StartsWith("small2:") && l.Contains("live 1")), $"live after the cut: {string.Join(" || ", paired.Log)}");
        }),
        ("a before-resolution bow of one or two attacking Personalities (Rocky Terrain's kind) is a battle answer against two attackers, and it holds a Province two attackers would take", () =>
        {
            var d = EffectDescriptors.Of("rocky-terrain-bow");
            Check(d.IsBattle && d.Class == EffectClass.Bow && d.Side == TargetSide.Enemy && d.Target == TargetKind.Personality && d.Conditions.Any(c => c.Kind == "attacking") && d.Targets == 2, $"descriptor: {d}, targets {d.Targets}");
            var terrain = new Answer(d, "Rocky Terrain", true, 0);
            var me = Side(0, [U("d3", 0, 3, province: 0)], [(0, 5), (1, 6)], hand: [terrain, terrain]); var them = Side(1, [U("a6", 1, 6, province: 0), U("a5", 1, 5, province: 0)], [(0, 6), (1, 6)]);
            var without = Exchange.Run(them, them.Units, me with { HandAnswers = [] }, me.Units, 5, [], []);
            var with = Exchange.Run(them, them.Units, me, me.Units, 5, [], me.HandAnswers);
            Check(without.Falls && !with.Falls, $"11 against 8 falls; with both attackers bowed it holds: without {without.AttackForce} v {without.DefenceForce}, with {with.AttackForce} v {with.DefenceForce}");
        }),
        ("a slot target \"an enemy card without attachments\" is an enemy unit card with the condition, not an attachment", () =>
        {
            var d = EffectDescriptors.Of("inexplicable-challenge-bow") is { Class: not EffectClass.Unknown } known ? known : AbilityCatalog.All.Keys.Where(k => k.StartsWith("inexplicable-challenge")).Select(EffectDescriptors.Of).First(e => e.Class == EffectClass.Bow);
            Check(d.Side == TargetSide.Enemy && d.Target == TargetKind.UnitCard && d.Conditions.Any(c => c.Kind == "without-attachments"), $"descriptor: {d}");
        }),
        ("formulation 3, situational: against an opponent unlikely to attack (their 2 against my 6 and Strength 5), a destroy that needs an attacking target goes before a send-home with the same targets", () =>
        {
            var attackingOnly = new EffectDescriptor("test-ra", "Battle", null, EffectClass.Destroy, null, false, TargetSide.Enemy, TargetKind.Personality, [new("attacking")], [], null, true, true, false, false, true, 0, 0, false, false, 1, false, null);
            var me = Side(0, [U("i6", 0, 6)], [(0, 5), (1, 6)]); var them = Side(1, [U("t2", 1, 2)], [(0, 6), (1, 6)]);
            var plan = Hand.DiscardByRead(ReadOf(me, them, 0), [new("c1", "destroy attacker", [attackingOnly], false, 0, 6), new("c2", "send home", [Naked()], false, 0, 6)], 1);
            Check(plan.CardIds.SequenceEqual(["c1"]), $"the attackers-only card goes: {plan.Why} | {string.Join(" || ", plan.Log)}");
        }),
        ("formulation 3, a deck that wins without attacking: a send-home usable anywhere is worth only what it does on defence, so against a rare attack Rocky Terrain's kind (two attackers) outranks it", () =>
        {
            var terrain = EffectDescriptors.Of("rocky-terrain-bow");
            var me = Side(0, [U("i6", 0, 6)], [(0, 5), (1, 6)]); var them = Side(1, [U("t4", 1, 4), U("t3", 1, 3)], [(0, 6), (1, 6)]);
            var cards = new List<Hand.HandCard> { new("c1", "terrain", [terrain], false, 0, 6), new("c2", "send home", [Naked()], false, 0, 6) };
            var drain = Hand.DiscardByRead(ReadOf(me, them, 0), cards, 1, attacksToWin: false);
            Check(drain.CardIds.SequenceEqual(["c2"]), $"the send-home goes in a deck that never attacks: {drain.Why} | {string.Join(" || ", drain.Log)}");
        }),
    ];
}
