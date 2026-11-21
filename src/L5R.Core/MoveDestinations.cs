namespace L5R.Core;

/// <summary>A move whose destination is a rule of the ability, not a per-card special case (issue #336): WHO moves and WHERE to.
/// Movers is "target", "source", "source-and-target" (Sea Troll Predator moves the Personality it targets and itself, together), "performer" or
/// "performer-and-target" (the card an ability names as its performer, for "choose your performing X ... move them both").
/// Destination is "home", "current-battlefield" (both need no choice), or one of the two rules a player chooses between:
/// "adjacent-unresolved-battlefield" (an unresolved battlefield next to the current one) and "unresolved-battlefield" (any other
/// unresolved battlefield). For a chosen destination the engine lists the legal battlefields (ActionOption.Provinces) and the command
/// names one in GameCommand.Province -- the same province indices the attack-assignment flow already draws with arrows.</summary>
public sealed record MoveSpec(string Movers, string Destination)
{
    public const string Target = "target", Source = "source", SourceAndTarget = "source-and-target", Performer = "performer", PerformerAndTarget = "performer-and-target";
    /// <summary>Every card of the ability's performing slot moves ("Move the targets to the current battlefield", Dislodge the Foe).</summary>
    public const string PerformingSlot = "performing-slot";
    /// <summary>The Personality that performed the Battle action an "after a Battle action resolves" Reaction reacts to (MatchState.BattleActionReactionPerformerId): Shosuro
    /// Hawado - exp's "target a Personality that performed it: Move him from his current location to another" (issue #453).</summary>
    public const string BattleActionPerformer = "battle-action-performer";
    /// <summary>The Personality the ability's source is attached to (issue #690, P24: Befriend the Tides' "Move this Shugenja ... to an adjacent battlefield" on a Spell).</summary>
    public const string Wielder = "wielder";
    public const string Home = "home", CurrentBattlefield = "current-battlefield", AdjacentUnresolved = "adjacent-unresolved-battlefield", AnyUnresolved = "unresolved-battlefield";
    /// <summary>Issue #503 (Shosuro Keirei: "Move Keirei from the current battlefield to a different battlefield"): any other battlefield of the Attack Phase, resolved or not.</summary>
    public const string AnyOther = "any-other-battlefield";
    /// <summary>Issue #622 (Back to the Front: "a battlefield he assigned to this turn"): any battlefield that exists, the current one included; the ability's own validation keeps the ones the mover was assigned to (IvoryMovement.cs).</summary>
    public const string AssignedThisTurn = "assigned-this-turn";
    /// <summary>Ring of Water ("a battlefield with one or more enemy units"): any battlefield still to be fought, the current one included, where the opposing player has a unit. Needs the acting player to say who the enemy is.</summary>
    public const string EnemyBattlefield = "battlefield-with-enemy-units";
    public bool ChoosesBattlefield => Destination is AdjacentUnresolved or AnyUnresolved or AnyOther or AssignedThisTurn or EnemyBattlefield;
}

public sealed partial class MatchEngine
{
    private MoveDestinationView? MovementPreview(string abilityId, AbilityDefinition ability, CardInstance? source, string? performer)
    {
        // Hawado's target pays the removal cost; only Hawado moves. Her existing effect and validation are unchanged.
        if (abilityId == "hawado-move-battlefield") return new(1 - _state.ActivePlayerId, source is null ? [] : [source.Id], false);
        if (ability.Move is not { ChoosesBattlefield: true } move) return null;
        var fixedId = move.Movers == MoveSpec.Wielder ? source?.AttachedTo : move.Movers is MoveSpec.Source or MoveSpec.SourceAndTarget ? source?.Id :
            move.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget ? performer : null;
        return new(1 - _state.ActivePlayerId, fixedId is null ? [] : [fixedId],
            move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget);
    }

    // The battlefields next to `current` (Mendon's ruling, issue #336): a battlefield is identified by the DEFENDER's province it is fought over, so
    // adjacency follows the DEFENDER's province line -- never the line of whichever player happens to own the card or take the action. Destroyed
    // provinces slide their neighbours together (AdjacentProvinces). The one place adjacency between battlefields is decided.
    // (AdjacentBattlefields is in EmperorRulingR1.cs: a battlefield without a province has none, and battlefields sharing a province form a line between the flanking provinces -- R-081.)

    // The battlefields a chosen destination may name right now (empty for a destination that needs no choice, and when no battle is
    // under way). A battlefield is identified by the DEFENDER's province it is fought over, so adjacency is the defender's province line
    // (destroyed provinces slide together, AdjacentProvinces), and "unresolved" is a battlefield still to be fought this Attack Phase.
    private int[] MoveDestinations(MoveSpec move, int? actingPlayerId = null)
    {
        if (!move.ChoosesBattlefield || _state.CurrentBattleProvince is not int current) return [];
        var unresolved = _state.UnresolvedBattlefields;
        if (move.Destination == MoveSpec.EnemyBattlefield)
            return actingPlayerId is int enemyOf ? unresolved.Append(current).Distinct().Where(p => Army(1 - enemyOf, p).Any()).Order().ToArray() : [];
        if (move.Destination == MoveSpec.AnyOther) return P2OtherBattlefields(current); // issue #503 (EmperorP2Keirei.cs)
        if (move.Destination == MoveSpec.AssignedThisTurn) return IvoryExistingBattlefields(current); // issue #622 (IvoryMovement.cs)
        return move.Destination == MoveSpec.AdjacentUnresolved
            ? AdjacentBattlefields(current).Where(unresolved.Contains).ToArray()
            : unresolved.Where(p => p != current).ToArray();
    }

    // ValidateAbility's check: a chosen destination must be one of the listed battlefields, and every other kind names none.
    private void ValidateMove(GameCommand command, AbilityDefinition ability)
    {
        if (ability.Move is not { } move) return;
        if (move.ChoosesBattlefield)
            Require(command.Province is int province && MoveDestinations(move, command.PlayerId).Contains(province), move.Destination == MoveSpec.EnemyBattlefield ? "Choose a battlefield with one or more enemy units." : move.Destination == MoveSpec.AdjacentUnresolved
                ? "Choose an adjacent unresolved battlefield." : move.Destination == MoveSpec.AnyOther ? "Choose a different battlefield." : move.Destination == MoveSpec.AssignedThisTurn ? "Choose a battlefield he was assigned to this turn." : "Choose another unresolved battlefield.");
        else Require(command.Province is null, "This move names no battlefield.");
    }

    // The effect of a move: every mover goes to the destination, the ability's own source (or its named performer) first and its target
    // last (the arrival of the target is the one a "moved an enemy Personality to this battlefield" Reaction watches for). EACH movement is its
    // own effect and negatable on its own (Mendon's ruling, issue #336): a negation of one mover's movement (Tamori Tamoko, a Champion of Thunder's
    // "all") leaves the others' movement alone unless an effect negates theirs too, and a negation of the whole action stops all of them (the
    // deferred action then never reaches here). Returns whether the TARGET moved (false when its move was negated, or there was none).
    private bool ExecuteMove(GameCommand command, AbilityDefinition ability, CardInstance? source, CardInstance? target)
    {
        var move = ability.Move!;
        var battlefield = move.Destination == MoveSpec.CurrentBattlefield ? _state.CurrentBattleProvince : command.Province;
        if (move.Movers is MoveSpec.Source or MoveSpec.SourceAndTarget) MoveByRule(source!, move, battlefield);
        if (move.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget) MoveByRule(Card(command.PerformerId), move, battlefield);
        if (move.Movers == MoveSpec.Wielder && source is not null && WielderOf(source) is { } wielder) MoveByRule(wielder, move, battlefield); // issue #690 (P24)
        if (move.Movers == MoveSpec.PerformingSlot) foreach (var mover in PerformingSlotCards(command, ability).ToArray()) MoveByRule(mover, move, battlefield);
        if (move.Movers == MoveSpec.BattleActionPerformer && _state.BattleActionReactionPerformerId is { } reactedPerformerId && _state.Cards.TryGetValue(reactedPerformerId, out var reactedPerformer) && InPlay(reactedPerformer))
            MoveByRule(reactedPerformer, move, battlefield);
        // An optional target (Kuni Shinoda's "the target, if any") leaves only the source to move.
        return move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget && target is not null && MoveByRule(target, move, battlefield);
    }

    private bool MoveByRule(CardInstance card, MoveSpec move, int? battlefield)
    {
        if (move.Destination == MoveSpec.Home)
        {
            var before = card.Zone;
            MoveHome(card, false);
            return before != card.Zone;
        }
        return TryMoveToBattlefield(card, battlefield!.Value);
    }
}
