using L5R.Core;
using static Batch336Support;
using static RegressionAssertions;

// Core rules (Abilities, 31 Jan 2013; issue #1131): "any single copy of a Reaction action (that is, from a single given source) may not be taken more than once to the same trigger, even if a more
// general effect makes it possible to take the Reaction more than once per turn." Mendon's reading (2026-10-27): per source, so two identical copies each react once (issues #898, #900).
// Position: the economy table, player 1 (the Attacker) with Isawa Kaname at the battlefield makes a Ranged 3 Attack on player 0's Personality; the Training reactor keeps the window open.
internal static class ReactionPerTriggerTests
{
    private const string KanameBoost = "kaname-attack-boost";

    public static void Run(TestRunner runner) => runner.Run("reaction-per-trigger", [
        ("A copy that has taken a Reaction to a trigger cannot take it again to that trigger, even when its once-per-turn use is given back; a second copy still can", OneUsePerSource),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static string Def(string ability) => CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();

    private static (MatchEngine E, CardInstance[] Kanames) Attacked(int copies)
    {
        var s = Table(Phase.Battle, 1);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var kanames = Enumerable.Range(0, copies).Select(_ => Add(s, Def(KanameBoost), 1, Zone.Battlefield, province: 0)).ToArray();
        Add(s, "training-attack-reactor", 0, Zone.Home); // a Reaction of the other player keeps the window open
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-honor-cost-attack", strategy.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack opens the window");
        return (e, kanames);
    }

    // Back to the first player's turn to act in the window: the other player passes.
    private static void PassToOwner(MatchEngine e)
    {
        if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the window is still open");
        Eq(e.State.PriorityPlayerId, 1, "the owner holds priority");
    }

    private static void OneUsePerSource()
    {
        var (e, kanames) = Attacked(copies: 2);
        PassToOwner(e);
        Accept(e, Enabled(e, 1, KanameBoost, kanames[0].Id).Command!);
        PassToOwner(e);
        // Something gives the first copy its use back ("a more general effect"): it is still refused this trigger.
        e = Edit(e, s => s.Cards[kanames[0].Id].AbilityUses.Clear());
        Check(!CanUse(e, 1, KanameBoost, kanames[0].Id), "the copy that already reacted to this trigger is not offered again");
        Reject(e, Use(e, 1, KanameBoost, kanames[0].Id));
        // A second copy is a separate source: it reacts once, and once only.
        Check(CanUse(e, 1, KanameBoost, kanames[1].Id), "the other copy may react to the same trigger");
        Accept(e, Enabled(e, 1, KanameBoost, kanames[1].Id).Command!);
        e = Edit(e, s => { foreach (var k in kanames) s.Cards[k.Id].AbilityUses.Clear(); });
        Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice, null, "each copy has reacted once, so neither is offered again and the window closes");

        // Control: without the record the same give-back would let the first copy react again, so the refusal above is the rule at work.
        var (c, ck) = Attacked(copies: 2);
        PassToOwner(c);
        Accept(c, Enabled(c, 1, KanameBoost, ck[0].Id).Command!);
        PassToOwner(c);
        c = Edit(c, s => { s.Cards[ck[0].Id].AbilityUses.Clear(); s.ReactionsTakenThisTrigger.Clear(); });
        Check(CanUse(c, 1, KanameBoost, ck[0].Id), "control: with the use given back and no record, it would be offered");
    }
}
