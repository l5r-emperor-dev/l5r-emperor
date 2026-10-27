using L5R.Core;
using static Batch336Support;
using static RegressionAssertions;

// Fallow Lands (oracle-l5r-9865, issue #1124): "If this Region's own province is under attack and its controller has presence there, after a Melee or Ranged Attack is targeted: -2 strength".
// Mendon's ruling (2026-10-27): it reacts to an Attack that targets the province too, not only one that targets a card. Position: the economy table, player 1 the Attacker (Lion), player 0 the
// Defender (Crane) with Fallow Lands on province 0 -- the battlefield -- and a Personality there; every step goes through QueryActions and the commands the engine offers.
internal static class FallowLandsReactionTests
{
    private const string FallowLands = "oracle-l5r-9865", Reaction = "fallow-lands-reaction", ProvinceAttacker = "training-province-attacker", ProvinceAttack = "training-province-attack";

    public static void Run(TestRunner runner) => runner.Run("fallow-lands-reaction", [
                ("An Attack on the province: the window opens, Fallow Lands takes 2 off, and the province loses 2 less strength", AttackOnTheProvince),
        ("An Attack on the province offers no other Attack reaction, and without Fallow Lands it opens no window as before", ProvinceAttackWindowIsNarrow),
                ("Fallow Lands only opens the window for the Attack on its own province while its controller has presence there", ProvinceGates),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, Decline(e));
    }

    // The table with Fallow Lands (player 0, province 0) and, unless `presence` is false, a Defender Personality at the battlefield.
    private static MatchState Position(bool presence = true, int regionProvince = 0, int regionOwner = 0)
    {
        var s = Table(Phase.Battle, 1);
        var region = Add(s, FallowLands, regionOwner, Zone.Region, province: regionProvince);
        s.Players[regionOwner].Provinces[regionProvince].RegionId = region.Id;
        if (presence) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        return s;
    }

    private static CardInstance Region(MatchEngine e) => e.State.Cards.Values.Single(c => c.DefinitionId == FallowLands);

    // The window is held by whoever may react first; the Defender uses Fallow Lands when it is his turn to act, as a player would. The reaction is the last one on offer, so the window closes and the Attack resolves.
    private static void UseFallowLands(MatchEngine e)
    {
        if (e.State.PriorityPlayerId != 0) Accept(e, Decline(e));
        Accept(e, Enabled(e, 0, Reaction, Region(e).Id).Command!);
    }

    // The attacker's Province attack (a Ranged 3 Attack at the battlefield's province): how much strength the Defender's province has after the whole Attack resolves.
    private static (MatchEngine E, int Strength) ProvinceAttackOn(MatchState s, bool react)
    {
        var attacker = Add(s, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.ProvinceStrengthForTest(0, 0);
        var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == ProvinceAttack);
        Accept(e, option.Command! with { TargetId = null, Province = 0 });
        if (react) { Eq(e.State.PendingAttack?.TargetId, null, "the pending Attack names no card"); UseFallowLands(e); }
        Settle(e);
        return (e, before - e.ProvinceStrengthForTest(0, 0));
    }

    private static void AttackOnTheProvince()
    {
        var control = Position();
        var (c, lost) = ProvinceAttackOn(control, react: false);
        // A control with the window opened and declined: the same loss as an Attack that never had one.
        Eq(c.State.PendingChoice, null, "the window has closed");
        Check(lost == 3, "declining Fallow Lands the province loses the Attack's full 3 strength; lost " + lost);

        var s = Position();
        var attacker = Add(s, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == ProvinceAttack).Command! with { TargetId = null, Province = 0 });
        Eq(e.State.PendingChoice?.Type, "attackReaction", "an Attack on the province opens the reaction window when Fallow Lands can react");
        Check(e.State.PendingAttack is { TargetId: null }, "the pending Attack names no card");

        var (reacted, lostReacted) = ProvinceAttackOn(Position(), react: true);
        Check(reacted.State.PendingAttack is null, "the Attack has resolved");
        Check(lostReacted == 1, "with Fallow Lands' -2 the province loses 1 strength, not 3; lost " + lostReacted);
    }

    private static void ProvinceAttackWindowIsNarrow()
    {
        // Another Attack reaction in the Defender's hand (Strong Defenses, -3 strength) is not usable against a province: it needs a card to be targeted.
        var strongDefenses = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (c.Abilities ?? []).Contains("strong-defenses-reaction")).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
        var s = Position();
        var held = Add(s, strongDefenses, 0, Zone.Hand);
        var attacker = Add(s, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == ProvinceAttack).Command! with { TargetId = null, Province = 0 });
        Eq(e.State.PendingChoice?.Type, "attackReaction");
        Check(CanUse(e, 0, Reaction, Region(e).Id) || e.State.PriorityPlayerId != 0, "Fallow Lands is the reaction on offer");
        if (e.State.PriorityPlayerId != 0) Accept(e, Decline(e));
        Check(CanUse(e, 0, Reaction, Region(e).Id), "Fallow Lands is offered to its controller");
        Check(!CanUse(e, 0, "strong-defenses-reaction", held.Id), "Strong Defenses is not offered against an Attack that targets no card");
        Reject(e, Use(e, 0, "strong-defenses-reaction", held.Id));

        // Without Fallow Lands nothing can react: no window, the Attack resolves at once as it always did.
        var bare = Table(Phase.Battle, 1);
        Add(bare, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker2 = Add(bare, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(bare);
        var before = e2.ProvinceStrengthForTest(0, 0);
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker2.Id && a.Command?.AbilityId == ProvinceAttack).Command! with { TargetId = null, Province = 0 });
        Check(e2.State.PendingChoice is null && e2.State.PendingAttack is null, "no window without a reactor");
        Eq(before - e2.ProvinceStrengthForTest(0, 0), 3, "the province loses the full strength");

        // No presence at the battlefield: Fallow Lands cannot react, so no window opens for the province Attack either.
        var empty = Position(presence: false);
        var attacker3 = Add(empty, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(empty);
        Accept(e3, e3.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker3.Id && a.Command?.AbilityId == ProvinceAttack).Command! with { TargetId = null, Province = 0 });
        Check(e3.State.PendingChoice is null, "no window while the Defender has no presence");
    }

    private static MatchEngine ProvinceAttackWith(MatchState s)
    {
        var attacker = Add(s, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == ProvinceAttack).Command! with { TargetId = null, Province = 0 });
        return e;
    }

    private static void ProvinceGates()
    {
        Eq(ProvinceAttackWith(Position()).State.PendingChoice?.Type, "attackReaction", "control: the window opens");
        Eq(ProvinceAttackWith(Position(presence: false)).State.PendingChoice, null, "no window while the Defender has no presence at the battlefield");
        Eq(ProvinceAttackWith(Position(regionProvince: 1)).State.PendingChoice, null, "no window for a Fallow Lands on another province");
        Eq(ProvinceAttackWith(Position(regionOwner: 1)).State.PendingChoice, null, "no window for the Attacker's own Fallow Lands");
    }
}
