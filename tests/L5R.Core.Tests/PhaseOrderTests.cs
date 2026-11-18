using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// The phase-order marker (issue #667): the default order behaves as the hard wiring did, The Deciding Moment and Nocturnal Attack put the Dynasty Phase before the Attack Phase for ONE turn, the
// marker is public in both seats' views, an illegal order is refused, and the following turn runs in the default order again. Driven only through QueryActions and the commands the engine offers.
internal static class PhaseOrderTests
{
    private const string DecidingMoment = "oracle-l5r-10119", Nocturnal = "oracle-l5r-10385";
    private static readonly string[] DynastyFirst = ["Straighten", "Events", "Action", "Dynasty", "Attack", "End"];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null) => Batch336Support.Add(s, def, owner, zone, bowed, province);
    private static MatchState Table(Phase phase = Phase.Action, int actor = 0) => Batch336Support.Table(phase, actor);

    public static void Run(TestRunner runner) => runner.Run("phase-order", [
        ("Published shape: both Strategies carry their printed stats, keywords, scan text and art, and the wave manifest pins them", PublishedShape),
        ("The data: only the Attack and Dynasty Phases may be switched; every other order is illegal", RulesData),
        ("The default order is the turn as it always was: Action, Attack (declined or fought), Dynasty, End, and the marker reads default throughout", DefaultOrderScenario),
        ("The Deciding Moment: refused without one of its three conditions (nothing changes), allowed with each", ConditionScenario),
        ("#1283: each seat's view says whether a player wins by Honor or Enlightenment when their next turn begins, by the turn start's own tests", WinsWhenTurnBeginsScenario),
        ("The Deciding Moment reorders this turn only: Dynasty before Attack, the marker public to both seats, the turn ends with the Attack Phase, and the next turn is default", ReorderThisTurnScenario),
        ("The reorder ends the turn after a declined attack too, and the Attack Phase is still the optional declare-or-decline decision", DeclinedAttackScenario),
        ("An order is refused once a phase it moves has begun, and a second reorder is a harmless no-op", RefusedLateScenario),
        ("The Deciding Moment's Battle half draws a card; the card is Unique (one per deck)", BattleHalfScenario),
        ("Nocturnal Attack bows a performing Ninja Personality for the same reorder; a non-Ninja or bowed Personality cannot", NocturnalScenario),
        ("The marker survives a serialised state and a Wave Man still straightens as the Attack Phase ends, in either order", SerialisationAndWaveManScenario),
    ]);

    // ---- helpers ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Take(MatchEngine e, string type)
    {
        var who = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
        var option = e.QueryActions(who).FirstOrDefault(a => a.Type == type && a.Enabled && a.Command is not null);
        Check(option is not null, $"{type} was not offered to player {who} in {e.State.Phase}: " + string.Join(", ", e.QueryActions(who).Select(a => a.Type)));
        Accept(e, option!.Command!);
    }

    private static string[] Order(MatchEngine e, int seat) => e.GetView(seat).PhaseOrder ?? PhaseOrderRules.Default;

    private static void Play(MatchEngine e, int player, string ability, CardInstance card, string? performerId = null) =>
        Accept(e, Batch336Support.Use(e, player, ability, card.Id, performerId: performerId));

    private static MatchEngine ActionTable(Action<MatchState, CardInstance>? edit = null, int actor = 0, string card = DecidingMoment)
    {
        var s = Table(Phase.Action, actor);
        var strategy = Add(s, card, actor, Zone.Hand);
        edit?.Invoke(s, strategy);
        return MatchEngine.FromTestState(s);
    }

    // ---- shape -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(Batch336Support.RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-116.txt"));
        Check(listed.Contains(DecidingMoment) && listed.Contains(Nocturnal) && listed.Length == 2, "strategy-wave-116 manifest lists both");
        foreach (var id in new[] { DecidingMoment, Nocturnal }) Check(File.Exists(Path.Combine(Batch336Support.RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        var d = CardCatalog.Get(DecidingMoment);
        Check(d.Type == CardType.Strategy && d.Name == "The Deciding Moment" && d.GoldCost == 0 && d.Focus == 4 && d.Keywords!.SequenceEqual(["Unique"]), "The Deciding Moment: printed stats");
        Check(d.Abilities!.SequenceEqual([PhaseOrderAbilities.DecidingMomentOrder, PhaseOrderAbilities.DecidingMomentDraw]), "The Deciding Moment: its two halves");
        var n = CardCatalog.Get(Nocturnal);
        Check(n.Type == CardType.Strategy && n.Name == "Nocturnal Attack" && n.GoldCost == 0 && n.Focus == 1 && n.Keywords!.SequenceEqual(["Ninja"]), "Nocturnal Attack: printed stats");
        Check(n.Abilities!.SequenceEqual([PhaseOrderAbilities.NocturnalAttack]), "Nocturnal Attack: its Limited action");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(Batch336Support.RepoRoot(), "content/published/strategy-wave-116-sources.json"))).RootElement.EnumerateArray()
            .Select(row => row.GetProperty("printing_set")[0].GetString()).ToArray();
        Check(sources.SequenceEqual(["Promotional&ndash;Emperor", "Torn Asunder"]), "the printings are the reviewed ones: " + string.Join(",", sources));
    }

    private static void RulesData()
    {
        Check(PhaseOrderRules.AllowedSwaps.SequenceEqual([("Attack", "Dynasty")]), "only Attack and Dynasty may be switched");
        Check(PhaseOrderRules.IsLegal(PhaseOrderRules.Default) && PhaseOrderRules.IsLegal(DynastyFirst), "the default and the Dynasty-first order are legal");
        Eq(PhaseOrderRules.LegalOrders().Count(), 2, "exactly two legal orders");
        foreach (var bad in new[]
        {
            new[] { "Events", "Straighten", "Action", "Attack", "Dynasty", "End" }, // Straighten is not first
            new[] { "Straighten", "Events", "Attack", "Action", "Dynasty", "End" }, // the Action Phase may not follow the Attack Phase
            new[] { "Straighten", "Events", "Action", "Attack", "End", "Dynasty" }, // the End Phase is last
            new[] { "Dynasty", "Events", "Action", "Attack", "Straighten", "End" },
            new[] { "Straighten", "Events", "Action", "Attack", "Dynasty" }, // too short
            new[] { "Straighten", "Events", "Action", "Attack", "Dynasty", "Dynasty" }, // a phase twice
        })
            Check(!PhaseOrderRules.IsLegal(bad), "illegal: " + string.Join(",", bad));
        Check(!PhaseOrderRules.IsLegal(null), "no order is not legal");
        Eq(PhaseOrderRules.NameOf(Phase.Battle), "Attack", "every Attack segment is the Attack Phase");
        Eq(PhaseOrderRules.NameOf(Phase.DynastyDiscard), "Dynasty", "the province discard is the Dynasty Phase");
        Check(PhaseOrderRules.NameOf(Phase.Finished) is null, "a finished match has no printed phase");
    }

    // ---- the default ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DefaultOrderScenario()
    {
        {
            // Pass, pass: the Attack Phase; decline it: the Dynasty Phase; end it: the turn ends.
            var e = ActionTable();
            var turn = e.State.TurnNumber;
            Check(e.State.PhaseOrder.IsDefault && e.GetView(0).PhaseOrder is null && e.GetView(1).PhaseOrder is null, "the default marker is not sent");
            Take(e, "pass"); Take(e, "pass");
            Eq(e.State.Phase, Phase.AttackDeclaration, "the Attack Phase follows the Action Phase");
            Take(e, "pass");
            Eq(e.State.Phase, Phase.Dynasty, "a declined attack is followed by the Dynasty Phase");
            Check(e.State.PhaseOrder.IsDefault, "default throughout");
            Take(e, "endDynasty");
            Check(e.State.Phase != Phase.Dynasty && e.State.PhaseOrder.Begun.Contains("End") || e.State.TurnNumber > turn, "ending the Dynasty Phase ends the turn");
        }
        {
            // A fought attack ends in the Dynasty Phase as well.
            var s = Table(Phase.Action, 1);
            Add(s, "training-lion-samurai", 1, Zone.Home);
            Add(s, "training-crane-samurai", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Take(e, "pass"); Take(e, "pass");
            Batch336Support.RunUntil(e, () => e.State.Phase == Phase.Dynasty);
            Check(e.State.PhaseOrder.IsDefault, "the fought Attack Phase leaves the default order");
        }
    }

    // ---- the card --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ConditionScenario()
    {
        {
            var e = ActionTable();
            var card = e.State.Cards.Values.Single(c => c.DefinitionId == DecidingMoment);
            var before = JsonSerializer.Serialize(e.State);
            var opt = Batch336Support.Options(e, 0, PhaseOrderAbilities.DecidingMomentOrder, card.Id).Single();
            Check(!opt.Enabled && opt.Reason is not null, "no condition: the Limited is listed disabled with its reason");
            Eq(JsonSerializer.Serialize(e.State), before, "nothing changed");
            var refused = e.Execute(new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: PhaseOrderAbilities.DecidingMomentOrder));
            Check(!refused.Accepted, "a forced command is refused too");
            Eq(JsonSerializer.Serialize(e.State), before, "a refused command changes nothing");
        }
        // Another player has 40 or more Honor.
        {
            var e = ActionTable((s, _) => s.Players[1].Honor = 40);
            Check(Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "the other player at 40 Honor");
        }
        {
            var e = ActionTable((s, _) => s.Players[1].Honor = 39);
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "39 is not enough");
        }
        // The player has -20 or lower Honor (the other player's Honor does not count for it).
        {
            var e = ActionTable((s, _) => s.Players[0].Honor = -20);
            Check(Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "-20 Honor");
        }
        {
            var e = ActionTable((s, _) => s.Players[0].Honor = -19);
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "-19 is not enough");
        }
        {
            var e = ActionTable((s, _) => s.Players[1].Honor = -25);
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "only the player's own Honor is tested against -20");
        }
        {
            var e = ActionTable((s, _) => s.Players[0].Honor = 45);
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "the player's own 40 Honor is not 'another player'");
        }
        // Five Rings in play under the other player's control.
        {
            var e = ActionTable((s, _) => { foreach (var element in new[] { "Fire", "Water", "Air", "Earth", "Void" }) Ring(s, element, 1); });
            Check(Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "the other player's five Rings");
        }
        {
            var e = ActionTable((s, _) => { foreach (var element in new[] { "Fire", "Water", "Air", "Earth" }) Ring(s, element, 1); });
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "four Rings are not five");
        }
        {
            var e = ActionTable((s, _) => { foreach (var element in new[] { "Fire", "Water", "Air", "Earth", "Void" }) Ring(s, element, 0); });
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.DecidingMomentOrder), "the player's own five Rings are not another player's");
        }
    }

    private static void WinsWhenTurnBeginsScenario()
    {
        bool Flag(MatchEngine e, int player, int seat) => e.GetView(seat).Players[player].WinsWhenTurnBegins;
        var five = new[] { "Fire", "Water", "Air", "Earth", "Void" };
        Check(!Flag(ActionTable(), 1, 0) && !Flag(ActionTable(), 0, 0), "an ordinary table: nobody is about to win");
        var forty = ActionTable((s, _) => s.Players[1].Honor = 40);
        Check(Flag(forty, 1, 0) && Flag(forty, 1, 1) && !Flag(forty, 0, 0), "40 Honor: public in both seats, for that player only");
        Check(!Flag(ActionTable((s, _) => s.Players[1].Honor = 39), 1, 0), "39 Honor is not enough");
        Check(Flag(ActionTable((s, _) => { foreach (var element in five) Ring(s, element, 1); }), 1, 0), "five elemental Rings in play");
        Check(!Flag(ActionTable((s, _) => { foreach (var element in five[..4]) Ring(s, element, 1); }), 1, 0), "four Rings are not five");
        // The flag is the turn start's own test: the player it marks does win when that turn begins.
        var e = ActionTable((s, _) => s.Players[1].Honor = 40);
        for (var steps = 0; e.WinnerId is null && e.State.ActivePlayerId == 0 && steps < 400; steps++)
        {
            var act = e.QueryActions(e.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Type is "pass" or "endDynasty" or "declineAttack" or "endTurn" or "discardHand");
            if (act is null || !e.Execute(new GameCommand(act.Type, e.PriorityPlayerId, e.Version)).Accepted) break;
        }
        Check(e.WinnerId == 1, $"the flagged player wins as their turn begins (winner {e.WinnerId?.ToString() ?? "none"}, phase {e.State.Phase})");
    }

    private static CardInstance Ring(MatchState s, string element, int owner)
    {
        var id = element switch { "Fire" => "training-ring-of-fire-stub", "Earth" => "training-ring-of-earth-stub", "Void" => "training-ring-of-void", _ => "training-ring-of-" + element.ToLowerInvariant() };
        return Add(s, id, owner, Zone.Home);
    }

    private static void ReorderThisTurnScenario()
    {
        var e = ActionTable((s, _) => { s.Players[0].Honor = -20; Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); });
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == DecidingMoment);
        var turn = e.State.TurnNumber;
        Play(e, 0, PhaseOrderAbilities.DecidingMomentOrder, card);
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        Check(Order(e, 0).SequenceEqual(DynastyFirst) && Order(e, 1).SequenceEqual(DynastyFirst), "the marker is in both seats' views");
        Eq(e.State.PhaseOrder.ChangedTurn, turn, "stamped with the turn");
        Check(e.State.Events.Any(ev => ev.Type == "phaseOrderChanged"), "the change is announced");
        // The Action Phase is over: the Dynasty Phase begins, not the Attack Phase.
        Take(e, "pass"); Take(e, "pass");
        Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase comes before the Attack Phase");
        Eq(e.State.ActivePlayerId, 0, "the turn's player");
        Check(Order(e, 1).SequenceEqual(DynastyFirst), "still marked in the Dynasty Phase");
        Take(e, "endDynasty");
        Eq(e.State.Phase, Phase.AttackDeclaration, "ending the Dynasty Phase begins the Attack Phase");
        Check(e.QueryActions(0).Any(a => a.Type == "declareAttack"), "the attack can still be declared");
        Check(Order(e, 0).SequenceEqual(DynastyFirst), "marked during the Attack Phase");
        // Fight it: the end of the Attack Phase ends the turn (no second Dynasty Phase), and the marker is back to default.
        Batch336Support.RunUntil(e, () => e.State.TurnNumber > turn || e.State.ActivePlayerId == 1, () =>
            Check(e.State.Phase != Phase.Dynasty, "the Dynasty Phase did not come a second time"));
        Check(e.State.PhaseOrder.IsDefault && e.GetView(0).PhaseOrder is null && e.GetView(1).PhaseOrder is null, "the next turn runs in the default order: the marker is reset and not sent");
        Eq(e.State.ActivePlayerId, 1, "the other player's turn");
        // And that turn does run in the default order.
        Batch336Support.RunUntil(e, () => e.State.Phase == Phase.Action);
        Take(e, "pass"); Take(e, "pass");
        Eq(e.State.Phase, Phase.AttackDeclaration, "the next turn's Action Phase is followed by the Attack Phase");
    }

    private static void DeclinedAttackScenario()
    {
        var e = ActionTable((s, _) => s.Players[0].Honor = -30);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == DecidingMoment);
        var turn = e.State.TurnNumber;
        Play(e, 0, PhaseOrderAbilities.DecidingMomentOrder, card);
        Take(e, "pass"); Take(e, "pass");
        Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase first");
        Take(e, "endDynasty");
        Eq(e.State.Phase, Phase.AttackDeclaration, "then the Attack Phase");
        Check(e.QueryActions(0).Any(a => a.Type == "declareAttack" && a.Enabled), "declaring is on offer");
        Take(e, "pass"); // declining
        Check(e.State.TurnNumber > turn || e.State.ActivePlayerId == 1 || e.State.Phase is Phase.End or Phase.Straighten or Phase.Events or Phase.Action, "declining the attack ends the turn");
        Check(e.State.PhaseOrder.IsDefault, "and the marker is back to default");
        Check(e.State.Events.Count(ev => ev.Type == "phaseOrderReset") == 1, "its reset is announced once");
    }

    private static void RefusedLateScenario()
    {
        // A phase the order moves has already begun: refused, nothing changes.
        foreach (var begun in new[] { "Dynasty", "Attack" })
        {
            var e = ActionTable((s, _) => { s.Players[1].Honor = 45; s.PhaseOrder.Begun.Add(begun); });
            var card = e.State.Cards.Values.Single(c => c.DefinitionId == DecidingMoment);
            var before = JsonSerializer.Serialize(e.State);
            var opt = Batch336Support.Options(e, 0, PhaseOrderAbilities.DecidingMomentOrder, card.Id).Single();
            Check(!opt.Enabled, $"refused after the {begun} Phase began: " + opt.Reason);
            Check(!e.Execute(new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: PhaseOrderAbilities.DecidingMomentOrder)).Accepted, "a forced command is refused too");
            Eq(JsonSerializer.Serialize(e.State), before, $"nothing changed ({begun})");
        }
        // The engine itself rejects an illegal order that is asked of it.
        {
            var e = ActionTable();
            Check(PhaseOrderRules.IsLegal(e.State.PhaseOrder.Order), "the live marker is always a legal order");
        }
        // A second reorder in the same turn is allowed and changes nothing more.
        {
            var s = Table(Phase.Action, 0);
            s.Players[1].Honor = 40;
            var first = Add(s, DecidingMoment, 0, Zone.Hand);
            var ninja = Add(s, "training-crane-samurai", 0, Zone.Home);
            ninja.GrantedKeywords.Add(new("Ninja", "test-fixture", GrantDuration.Permanent));
            var second = Add(s, Nocturnal, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Play(e, 0, PhaseOrderAbilities.DecidingMomentOrder, first);
            Check(Order(e, 0).SequenceEqual(DynastyFirst), "reordered");
            Eq(e.State.Events.Count(ev => ev.Type == "phaseOrderChanged"), 1, "announced once");
            // Limited actions are once per turn per player's own limits; a second Limited needs the first to have been spent. Whether or not it is offered, the order stays Dynasty-first.
            Check(e.State.PhaseOrder.Order.SequenceEqual(DynastyFirst), "and stays so");
            _ = second;
        }
    }

    private static void BattleHalfScenario()
    {
        var s = Table(Phase.Battle, 1);
        var card = Add(s, DecidingMoment, 1, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var drawn = Add(s, "training-crane-samurai", 1, Zone.FateDeck);
        s.Players[1].FateDeck.Clear(); s.Players[1].FateDeck.Add(drawn.Id);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[1].Hand.Count;
        Check(!Batch336Support.CanUse(e, 1, PhaseOrderAbilities.DecidingMomentOrder, card.Id), "the Limited half is not a Battle action");
        Play(e, 1, PhaseOrderAbilities.DecidingMomentDraw, card);
        Eq(e.State.Players[1].Hand.Count, hand, "the Strategy left the hand and a card was drawn: net zero");
        Check(e.State.Players[1].Hand.Contains(drawn.Id) && e.State.Players[1].FateDeck.Count == 0, "the top card of the Fate deck was drawn");
        Check(e.State.PhaseOrder.IsDefault, "the Battle half does not reorder the turn");
        // Unique: one copy per deck.
        var two = DeckFormat.CheckLegality(new Deck("Emperor", [new("The Deciding Moment", 2)]), _ => CardCatalog.Get(DecidingMoment).Keywords!);
        Check(!two.IsLegal && two.Violations.Single().Limit == 1, "a second copy breaks the Unique limit");
        var one = DeckFormat.CheckLegality(new Deck("Emperor", [new("The Deciding Moment", 1)]), _ => CardCatalog.Get(DecidingMoment).Keywords!);
        Check(one.IsLegal, "one copy is legal");
    }

    private static void NocturnalScenario()
    {
        MatchEngine Build(bool ninja, bool bowed, out CardInstance card, out CardInstance personality)
        {
            var s = Table(Phase.Action, 0);
            card = Add(s, Nocturnal, 0, Zone.Hand);
            personality = Add(s, "training-crane-samurai", 0, Zone.Home, bowed);
            if (ninja) personality.GrantedKeywords.Add(new("Ninja", "test-fixture", GrantDuration.Permanent));
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(true, false, out var card, out var ninja);
            Check(Batch336Support.CanUse(e, 0, PhaseOrderAbilities.NocturnalAttack, card.Id, ninja.Id), "a Ninja can perform it");
            Play(e, 0, PhaseOrderAbilities.NocturnalAttack, card, ninja.Id);
            Check(e.State.Cards[ninja.Id].Bowed, "the performer bowed");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
            Check(Order(e, 1).SequenceEqual(DynastyFirst), "the Attack Phase option comes after the Dynasty Phase");
            Take(e, "pass"); Take(e, "pass");
            Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase is next");
            Take(e, "endDynasty");
            Eq(e.State.Phase, Phase.AttackDeclaration, "then the option to attack");
        }
        {
            var e = Build(false, false, out var card, out var plain);
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.NocturnalAttack, card.Id, plain.Id), "a Personality that is not a Ninja cannot perform it");
        }
        {
            var e = Build(true, true, out var card, out var bowedNinja);
            Check(!Batch336Support.CanUse(e, 0, PhaseOrderAbilities.NocturnalAttack, card.Id, bowedNinja.Id), "a bowed Ninja cannot pay the bow");
            Check(e.State.PhaseOrder.IsDefault, "and nothing changed");
        }
    }

    private static void SerialisationAndWaveManScenario()
    {
        var e = ActionTable((s, _) => s.Players[1].Honor = 40);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == DecidingMoment);
        Play(e, 0, PhaseOrderAbilities.DecidingMomentOrder, card);
        var round = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        Check(round.State.PhaseOrder.Order.SequenceEqual(DynastyFirst) && round.State.PhaseOrder.ChangedTurn == e.State.PhaseOrder.ChangedTurn, "the marker survives serialisation");
        Take(round, "pass"); Take(round, "pass");
        Eq(round.State.Phase, Phase.Dynasty, "and a restored match keeps running in the changed order");
        // Wave Man (issue #279): straightened as the Attack Phase ends -- in the default order as the Dynasty Phase begins, in the changed one as the turn is about to end.
        foreach (var changed in new[] { false, true })
        {
            var s = Table(Phase.Action, 0);
            var host = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
            var wave = Batch336Support.Add(s, "oracle-l5r-9255", 0, Zone.Attached, true, host.ProvinceIndex, host.Id);
            if (changed) s.PhaseOrder.Order = [.. DynastyFirst];
            var w = MatchEngine.FromTestState(s);
            Take(w, "pass"); Take(w, "pass");
            if (changed) { Check(w.State.Cards[host.Id].Bowed, "the unit is still bowed as the Dynasty Phase begins first"); Take(w, "endDynasty"); }
            else Check(w.State.Cards[host.Id].Bowed, "the unit is still bowed as the Attack Phase begins");
            Take(w, "pass"); // decline the attack
            Check(!w.State.Cards[host.Id].Bowed && !w.State.Cards[wave.Id].Bowed, $"Wave Man straightened his unit as the Attack Phase ended (Dynasty first: {changed})");
        }
    }
}
