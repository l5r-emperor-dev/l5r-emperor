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
    ];
}
