using L5R.Core;
internal static class RestrictionViewTests
{
    public static void Run(TestRunner runner) => runner.Run("restriction-view", [
        ("Movement restrictions expire and hidden sources are masked for both seats", MovementPrivacy),
        ("Movement immunity preserves assignment markers and views emit nothing", MovementImmunity),
        ("Attachment and host locks retain their own immunity subjects", AttachmentLockSubjects),
        ("Printed straighten holds appear without seeding state", UnseededHold),
        ("Conditional locks disappear when their source leaves its battlefield", ConditionalMovement),
        ("Pending movement negation respects immunity and view metadata round trips", PendingMovement),
        ("Relationship labels identify public cards and mask concealed performers", RelationshipPrivacy),
        ("Whole-unit resolution protection is visible on attachments only at the current battle", UnitResolutionProtection),
    ]);
    private static void UnitResolutionProtection()
    {
        var state = Batch336Support.Table(Phase.Battle, 0); state.CurrentBattleProvince = 0;
        var heroDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var itemDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Item);
        var hero = Batch336Support.Add(state, heroDef.Id, 0, Zone.Battlefield); hero.ProvinceIndex = 0;
        var item = Batch336Support.Add(state, itemDef.Id, 0, Zone.Attached); item.AttachedTo = hero.Id;
        state.ResolutionWillNotBowCardIds.Add(hero.Id);
        CardRestrictionView[] Rows() => MatchEngine.FromTestState(state).GetView(0).Players[0].Army.Single(c => c.Id == item.Id).Restrictions ?? [];
        Batch336Support.Check(Rows().Any(r => r.Label == "Will not bow from this battle's resolution"), "the unit's attachment shares its head's protection");
        state.CurrentBattleProvince = 1;
        Batch336Support.Check(Rows().All(r => r.Label != "Will not bow from this battle's resolution"), "protection from a different battle is not presented");
    }
    private static void RelationshipPrivacy()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var defs = CardCatalog.Cards.Values.Where(d => d.Type == CardType.Personality).Take(2).ToArray();
        var target = Batch336Support.Add(state, defs[0].Id, 0, Zone.Home);
        var actor = Batch336Support.Add(state, defs[1].Id, 1, Zone.Home); actor.FaceUp = true;
        target.TargetBars.Add(new(actor.Id, state.TurnNumber));
        state.Restrictions.Items.Add(new("pair-view", RestrictionKind.NoSameArmy, CardId: target.Id,
            OtherCardId: actor.Id, UntilTurn: state.TurnNumber));
        foreach (var seat in new[] { 0, 1 })
        {
            var rows = MatchEngine.FromTestState(state).GetView(seat).Players[0].Home.Single(c => c.Id == target.Id).Restrictions!;
            Batch336Support.Check(rows.Any(r => r.Kind == "target" && r.Label.Contains(defs[1].Name)) && rows.Any(r => r.Kind == "rule" && r.Label.StartsWith("Cannot be assigned") && r.Label.Contains(defs[1].Name)), "public named relationships are understandable");
        }
        actor.Zone = Zone.Hand; // FaceUp is deliberately still true.
        foreach (var seat in new[] { 0, 1 })
        {
            var rows = MatchEngine.FromTestState(state).GetView(seat).Players[0].Home.Single(c => c.Id == target.Id).Restrictions!;
            Batch336Support.Check(rows.All(r => !r.Label.Contains(defs[1].Name) && r.SourceName != defs[1].Name && r.SourceCardId != actor.Id), "relationship sentences cannot reveal a concealed card");
        }
    }
    private static void PendingMovement()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var definition = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var card = Batch336Support.Add(state, definition.Id, 0, Zone.Home);
        state.NegatedEffects.Add(new("move", card.Id, 1));
        state.Immunities = [new() { CardId = card.Id, Kind = ImmunityKind.MoveNegation, Source = ImmunitySource.Any, UntilTurn = state.TurnNumber }];
        var projected = MatchEngine.FromTestState(state).GetView(0).Players[0].Home.Single(c => c.Id == card.Id);
        Batch336Support.Check(projected.Restrictions?.All(r => r.Kind != "move") != false, "immune movement has no negated-movement marker");
        var json = System.Text.Json.JsonSerializer.Serialize(projected);
        var restored = System.Text.Json.JsonSerializer.Deserialize<CardView>(json)!;
        Batch336Support.Check(restored.Restrictions!.SequenceEqual(projected.Restrictions!), "public restriction rows retain semantics in JSON");
        state.Immunities = [];
        Batch336Support.Check(MatchEngine.FromTestState(state).GetView(1).Players[0].Home.Single(c => c.Id == card.Id).Restrictions!.Any(r => r.Kind == "move" && r.Duration == "During the pending action"), "unprotected pending movement displays its real lifetime");
    }
    private static void MovementPrivacy()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var definition = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var target = Batch336Support.Add(state, definition.Id, 0, Zone.Home);
        var source = Batch336Support.Add(state, definition.Id, 1, Zone.Home); source.FaceUp = true; source.Zone = Zone.Hand;
        target.MovementLocks.Add(new(source.Id, state.TurnNumber, MovementKinds.ToBattlefield));
        foreach (var seat in new[] { 0, 1 })
        {
            var view = MatchEngine.FromTestState(state).GetView(seat).Players[0].Home.Single(c => c.Id == target.Id);
            Batch336Support.Check(view.Restrictions is [{ Kind: "move", SourceName: null, SourceCardId: null }], "hidden source never appears in effect metadata");
        }
        state.TurnNumber++;
        Batch336Support.Check(MatchEngine.FromTestState(state).GetView(0).Players[0].Home.Single(c => c.Id == target.Id).Restrictions is null, "expired lock has no indicator");
    }
    private static void ConditionalMovement()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var definition = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var card = Batch336Support.Add(state, definition.Id, 0, Zone.Home);
        card.MovementLocks.Add(new("condition", state.TurnNumber, MovementKinds.ToBattlefield, WhileCardAtBattlefield: "missing", Battlefield: 0));
        Batch336Support.Check(MatchEngine.FromTestState(state).GetView(0).Players[0].Home.Single(c => c.Id == card.Id).Restrictions is null, "absent conditional source cannot leave a stale indicator");
    }
    private static void MovementImmunity()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var card = Batch336Support.Add(state, def.Id, 0, Zone.Home);
        card.MovementLocks.Add(new("fixture", state.TurnNumber, MovementKinds.ToBattlefield | MovementKinds.Assign, ByPlayer: 1));
        state.Immunities = [new() { CardId = card.Id, Kind = ImmunityKind.MoveNegation, Source = ImmunitySource.Any, UntilTurn = state.TurnNumber }];
        var engine = MatchEngine.FromTestState(state); var count = engine.State.Events.Count;
        for (var i = 0; i < 4; i++)
        {
            var rows = engine.GetView(i % 2).Players[0].Home.Single(c => c.Id == card.Id).Restrictions!;
            Batch336Support.Check(rows.Any(r => r.Label == "Cannot be assigned") && !rows.Any(r => r.Label == "Movement to battlefields is negated"), "movement immunity does not remove assignment restrictions");
        }
        Batch336Support.Check(engine.State.Events.Count == count, "projection must never emit events");
    }
    private static void AttachmentLockSubjects()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var heroDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality);
        var itemDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Item);
        var hero = Batch336Support.Add(state, heroDef.Id, 0, Zone.Home);
        var item = Batch336Support.Add(state, itemDef.Id, 0, Zone.Attached); item.AttachedTo = hero.Id;
        var source = Batch336Support.Add(state, heroDef.Id, 1, Zone.Home);
        hero.ActionLocks.Add(new(source.Id, "Turn")); item.ActionLocks.Add(new(source.Id, "Turn"));
        state.Immunities = [new() { CardId = item.Id, Kind = ImmunityKind.Prevention, Source = ImmunitySource.Any, UntilTurn = state.TurnNumber }];
        var card = MatchEngine.FromTestState(state).GetView(0).Players[0].Home.Single(c => c.Id == item.Id);
        Batch336Support.Check(card.ActionLocked && card.Restrictions!.Any(r => r.Kind == "act"), "a nonimmune host's lock still applies to its immune attachment");
    }
    private static void UnseededHold()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var def = CardCatalog.Cards.Values.First(d => d.Name == "Bamboo Harvesters");
        var card = Batch336Support.Add(state, def.Id, 0, Zone.Home);
        state.Players[0].TurnsTaken = 0;
        var engine = MatchEngine.FromTestState(state);
        Batch336Support.Check(engine.GetView(0).Players[0].Home.Single(c => c.Id == card.Id).Restrictions!.Any(r => r.Kind == "straighten"), "unseeded printed hold has an indicator");
        Batch336Support.Check(!engine.State.Cards[card.Id].PrintedHoldsSeeded, "view must not seed a printed hold");
    }

}
