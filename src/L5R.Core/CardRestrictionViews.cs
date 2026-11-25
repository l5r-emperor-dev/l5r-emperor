namespace L5R.Core;

/// <summary>Public explanation, never a client legality instruction. Missing historical source metadata stays null.</summary>
public sealed record CardRestrictionView(string Kind, string Label, string Duration, string? SourceName = null, string? SourceCardId = null, int? ByPlayer = null, bool Protective = false);

public sealed partial class MatchEngine
{
    private static bool PublicRestrictionSource(CardInstance card) => card.FaceUp && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Stronghold or Zone.Region or Zone.DynastyDiscard or Zone.FateDiscard or Zone.Dead or Zone.RemovedFromGame;

    private bool RestrictionMovementNegationBarred(CardInstance card, int? by) => by is int player && (IsImmune(card, ImmunityKind.MoveNegation, player) || SledOf(card) is { } sled && player != sled.OwnerId);

    private CardRestrictionView[]? RestrictionViews(CardInstance card)
    {
        if (!card.FaceUp || !InPlay(card)) return null;
        var rows = new List<CardRestrictionView>();
        string RelationName(string? id, string fallback) => id is not null && _state.Cards.TryGetValue(id, out var related) && PublicRestrictionSource(related) ? Definition(related).Name : fallback;
        CardRestrictionView Row(string kind, string label, string duration, string? source = null, int? by = null, bool protection = false)
        {
            // Source strings in older effects may be instance ids, ability ids or public names.
            // Never expose an instance id/name for a card whose face is concealed.
            if (source is not null && _state.Cards.TryGetValue(source, out var origin))
                return new(kind, label, duration, PublicRestrictionSource(origin) ? Definition(origin).Name : null, PublicRestrictionSource(origin) ? origin.Id : null, by, protection);
            return new(kind, label, duration, source is not null && CardCatalog.Cards.Values.Any(d => d.Name == source) ? source : null, null, by, protection);
        }
        foreach (var movement in card.MovementLocks.Where(l => l.Turn == _state.TurnNumber && MovementLockHolds(l) && !IsImmune(card, ImmunityKind.Prevention, l.ByPlayer)))
        {
            var until = movement.NextOnly ? "Next matching movement this turn" : "Until turn ends";
            if (movement.Battlefield is not null) until += "; while the source remains at the specified battlefield";
            foreach (var kind in new[] { MovementKinds.Home, MovementKinds.ToBattlefield, MovementKinds.Assign, MovementKinds.AssignAttack })
                if ((movement.Kinds & kind) != 0 && (IsAssignKind(kind) || !RestrictionMovementNegationBarred(card, movement.ByPlayer) && !HiromiUnnegatable()) && !(IsAssignKind(kind) && IsImmune(card, ImmunityKind.AssignPrevention, movement.ByPlayer)))
                    rows.Add(Row("move", kind switch { MovementKinds.Home => "Movement home is negated", MovementKinds.ToBattlefield => "Movement to battlefields is negated", MovementKinds.Assign => "Cannot be assigned", _ => "Cannot be assigned to attack" }, until, movement.Source, movement.ByPlayer));
        }
        var actor = card.Zone == Zone.Attached && card.AttachedTo is { } host && _state.Cards.TryGetValue(host, out var parent) ? parent : card;
        foreach (var subject in actor == card ? new[] { card } : new[] { card, actor })
            foreach (var action in subject.ActionLocks.Where(l => LockInForce(l) && !IsImmune(subject, ImmunityKind.Prevention, LockSourcePlayer(l.Source))))
                rows.Add(Row("act", "Cannot perform actions", action.Duration == "Turn" ? "Until turn ends" : action.Duration == "Battle" ? "Until battle ends" : "Until phase ends", action.Source));
        if (card.StraightenBlockedTurn == _state.TurnNumber) rows.Add(Row("straighten", "Next straightening is negated", "Next attempt this turn"));
        if (card.StraightenLockedTurn == _state.TurnNumber) rows.Add(Row("straighten", "Cannot be straightened by effects", "Until turn ends"));
        foreach (var hold in card.StraightenHolds.Where(h => h.WhileInPlay is not { } source || _state.Cards.TryGetValue(source, out var held) && KeptInPlay(held)))
            rows.Add(Row("straighten", "Will not straighten", hold.WhileInPlay is not null ? "While the source remains in play" : "Until " + DescribePoint(hold.Until, hold.Owner) + (hold.Skip > 0 ? $" ({hold.Skip} earlier occurrence(s) remaining)" : ""), hold.Source));
        if (!card.PrintedHoldsSeeded && PrintedStraightenTurn(Definition(card)) is > 0 and var printedTurn && printedTurn - 1 - Player(card.OwnerId).TurnsTaken >= 0)
            rows.Add(Row("straighten", "Will not straighten", $"Before controller's turn {printedTurn}", card.Id));
        // Exact registered rule kinds and arguments, not interpretations of printed text.
        foreach (var rule in RestrictionsOn(card))
        {
            if (rule.Kind is RestrictionKind.NoAttack or RestrictionKind.NoDefend && rule.ByPlayer is int by && by != card.OwnerId && (IsImmune(card, ImmunityKind.AssignPrevention, by) || IsImmune(card, ImmunityKind.Prevention, by))) continue;
            var stored = Restrictions.Items.FirstOrDefault(r => r.Id == rule.StoredId);
            var duration = stored is null ? "While the printed condition applies" : stored.Until switch { RestrictionUntil.Turn => "Until turn ends", RestrictionUntil.Phase => "Until phase ends", RestrictionUntil.Permanent => "For the rest of the game", RestrictionUntil.OwnersNextTurnBegins => "Until the specified player's next turn begins", _ => "While in play" };
            var label = rule.Kind switch { RestrictionKind.NoAttack => "Cannot be assigned to attack", RestrictionKind.NoDefend => "Cannot be assigned to defend", RestrictionKind.NoChallenge => "Will not issue challenges", RestrictionKind.NoAttach => "Attachment restriction: " + rule.Arg, RestrictionKind.NoDishonor => "Will not become dishonorable", RestrictionKind.NoPair => rule.Arg == "family" ? "Cannot be assigned to the same battlefield as another Personality with the same family name" : "Cannot be assigned to the same battlefield as " + RelationName(rule.OtherCardId, "the affected Personality"), RestrictionKind.NoSameArmy => "Cannot be assigned to the same army as " + RelationName(rule.OtherCardId, "the affected Personality"), RestrictionKind.SoleUnit => "Cannot be assigned to an army with another Personality", RestrictionKind.SpellLimit => $"Spell action limit: {rule.Max}", _ => null };
            if (label is not null) rows.Add(Row(rule.Kind is RestrictionKind.NoAttack or RestrictionKind.NoDefend ? "move" : "rule", label, duration, stored is null ? card.Id : null, rule.ByPlayer, rule.Kind == RestrictionKind.NoDishonor));
        }
        // Issue #1341: Scars of War's game-long mark (EmperorP4Watches.cs), one row per copy on this Holding; the client draws it as the dishonor fan.
        foreach (var mark in P4.ScarsMarks.Where(m => m.CardId == card.Id))
            rows.Add(Row("honorLoss", "Until the game ends, after each time it bows or an ability on it resolves, its controller loses 1 Honor", "For the rest of the game", mark.Source));
        foreach (var bar in card.TargetBars.Where(b => TargetBarHolds(card, b)))
            rows.Add(Row("target", "Cannot be targeted by actions performed by " + RelationName(bar.ActorCardId, "the affected performer") + (bar.WhileAttachmentKeyword is { } keyword ? $" while carrying a {keyword} attachment" : ""), "Until turn ends"));
        foreach (var effect in _state.NegatedEffects.Where(e => e.CardId == card.Id))
        {
            if (effect.Kind == "move" && RestrictionMovementNegationBarred(card, effect.ByPlayer)) continue;
            var label = effect.Kind switch { "destroy" => "Next destruction from the pending action is negated", "bow" => "Next bowing from the pending action is negated", "move" => "Next movement from the pending action is negated", "straighten" => "Next straightening from the pending action is negated", "all" => RestrictionMovementNegationBarred(card, effect.ByPlayer) ? "Effects of the pending action are negated except protected movement" : "Effects of the pending action are negated", _ => null };
            if (label is not null) rows.Add(Row(effect.Kind is "move" or "straighten" ? effect.Kind : "protect", label, "During the pending action", by: effect.ByPlayer, protection: effect.Kind is "destroy" or "bow" or "all"));
        }
        foreach (var bar in BarRulesOn(card).Where(b => !b.Forced))
            rows.Add(Row("target", "Cannot be targeted by matching " + (bar.Actor.ActionKeyword is null ? "actions" : bar.Actor.ActionKeyword + " actions") + (bar.Actor.Players == "others" ? " from other players" : "") + "; printed performer conditions apply", "While the targeting rule applies", bar.Label));
        if (ForceContributionNegated(card)) rows.Add(Row("contribute", "Does not contribute Force", "Until this battle ends"));
        if (CombatSegmentShielded(card)) rows.Add(Row("protect", "Will not be destroyed during the Combat Segment", "Until the Combat Segment ends", protection: true));
        var battleSubject = card.Zone == Zone.Attached && card.AttachedTo is { } battleHost && _state.Cards.TryGetValue(battleHost, out var head) ? head : card;
        if (battleSubject.Zone == Zone.Battlefield && battleSubject.ProvinceIndex == _state.CurrentBattleProvince && (_state.ResolutionWillNotBowCardIds.Contains(card.Id) || _state.ResolutionWillNotBowCardIds.Contains(battleSubject.Id) || _state.ResolutionWillNotBowPlayerIds.Contains(card.OwnerId)))
            rows.Add(Row("protect", "Will not bow from this battle's resolution", "Until this battle ends", protection: true));
        if (SasakoUnitWillNotBow(battleSubject) || ThundersStaminaUnitWillNotBow(battleSubject))
            rows.Add(Row("protect", "Will not bow from battle resolution while the printed conditions apply", "While the printed conditions apply", protection: true));
        foreach (var immunity in (_state.Immunities ?? []).Where(i => ImmunityLive(i) && StoredImmunityCovers(i, card, null, null)))
        {
            var label = immunity.Kind switch { ImmunityKind.Bow => "Protected from bowing", ImmunityKind.Destroy => "Protected from destruction", ImmunityKind.DestroyInResolution => "Protected from destruction in battle resolution", ImmunityKind.MoveNegation => "Protected from movement negation", ImmunityKind.Prevention => "Protected from prevention effects", ImmunityKind.ForcePenalty => "Protected from Force penalties", ImmunityKind.AssignPrevention => "Protected from assignment prevention", _ => null };
            if (label is null) continue;
            label += immunity.Source switch { ImmunitySource.OtherPlayers => " from other players' cards", ImmunitySource.OtherTerrains => " from other Terrains", _ => " from card effects" };
            var until = immunity.Until switch { ImmunityUntil.Turn => "Until turn ends", ImmunityUntil.Phase => "Until phase ends", ImmunityUntil.Permanent => "For the rest of the game", ImmunityUntil.WhileInPlay => "While its source remains in play", _ => "Until the specified turn boundary" };
            rows.Add(Row("protect", label, until, immunity.OwnerCardId, protection: true));
        }
        if (card.ForcePenaltiesNegatedTurn == _state.TurnNumber) rows.Add(Row("protect", "Force penalties are negated", "Until turn ends", protection: true));
        if (card.ForceModifiersNegatedTurn == _state.TurnNumber) rows.Add(Row("protect", "Force bonuses and penalties from actions are negated", "Until turn ends", protection: true));
        return rows.Count == 0 ? null : rows.Distinct().ToArray();
    }
}
