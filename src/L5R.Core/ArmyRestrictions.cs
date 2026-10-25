namespace L5R.Core;

// Issue #821: "will not attack or defend with" -- assignment rules over ONE SIDE of a battlefield (an army), built on the P11 card-restriction store (CardRestrictions.cs).
//
// P11's NoPair judges every card standing at the battlefield, on both sides, so it cannot say "they may stand opposed". These two kinds judge only the cards of the assigned card's OWN side:
//
//   SoleUnit     Hida Kaiji - exp: "Will not attack or defend with other Personalities, and other Personalities will not attack or defend with Kaiji." A card that carries it is never assigned into an
//                army that holds another Personality of its side, and no Personality of its side is assigned into an army that holds it. The opposing army is never considered (Mendon, 2026-10-10).
//   NoSameArmy   Isawa Ryuzo: "They will not attack or defend in the same army." A stored pair (AddSameArmyPair): the two Personalities are never assigned into one army. A card may be in several pairs
//                (Ryuzo is not Unique, so up to three pairs can stack on one Personality), and a pair does not touch the pairs the same cards have with others.
//
// THE PRIMITIVE is SideUnitsAt / SideUnitCount: the Personalities of a card's own side at a battlefield, those already standing there plus those the same command assigns there. Every consult point reads it.
//
// "Will not assign" removes the assignment, nothing else: a Personality may still be MOVED to the battlefield (an Engage or Battle action, a Reaction), because the rule is about being assigned.
// RequireArmyMayAssign judges the assignment command; ArmyAssignmentTemplate keeps the offered "assign everyone" command legal, so an offered command is never one the engine would refuse.
public sealed partial class MatchEngine
{
    // ---- the primitive ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The other Personalities on this card's side at the battlefield: those already standing there, plus those the same command assigns there (assignments: Personality id -> battlefield).</summary>
    internal IEnumerable<CardInstance> SideUnitsAt(CardInstance card, int battlefield, IReadOnlyDictionary<string, int>? assignments = null)
    {
        IEnumerable<CardInstance> units = Army(card.OwnerId, battlefield).Where(c => c.Id != card.Id);
        if (assignments is not null)
            units = units.Concat(assignments.Where(a => a.Value == battlefield && a.Key != card.Id && _state.Cards.TryGetValue(a.Key, out var other) && other.OwnerId == card.OwnerId
                && Definition(other).Type == CardType.Personality).Select(a => Card(a.Key)));
        return units.DistinctBy(c => c.Id);
    }

    /// <summary>How many other Personalities of the card's own side are at the battlefield (or are being assigned there by this command).</summary>
    internal int SideUnitCount(CardInstance card, int battlefield, IReadOnlyDictionary<string, int>? assignments = null) => SideUnitsAt(card, battlefield, assignments).Count();

    /// <summary>Two Personalities that will not attack or defend in the same army (Isawa Ryuzo). Each call stores one more pair: they stack.</summary>
    internal string AddSameArmyPair(CardInstance a, CardInstance b, RestrictionUntil until = RestrictionUntil.Turn, string? note = null)
    {
        var id = AddCardRestriction(RestrictionKind.NoSameArmy, a, until, null, b.Id);
        if (note is not null) Emit("restrictionAdded", note, a.OwnerId, a.Id);
        return id;
    }

    private bool ArmyRestrictionsExist() =>
        Restrictions.Items.Any(r => r.Kind == RestrictionKind.NoSameArmy)
        || _state.Cards.Values.Any(c => c.Zone != Zone.Hand && InPlay(c) && CardRestrictionCatalog.For(Definition(c).Id).Any(s => s.Kind == RestrictionKind.SoleUnit));

    private bool SameArmyPaired(CardInstance a, CardInstance b) =>
        RestrictionsOn(a).Any(r => r.Kind == RestrictionKind.NoSameArmy && r.OtherCardId == b.Id) || RestrictionsOn(b).Any(r => r.Kind == RestrictionKind.NoSameArmy && r.OtherCardId == a.Id);

    /// <summary>Why these two cards of one side may not be in one army, or null.</summary>
    private string? ArmyConflict(CardInstance card, CardInstance other)
    {
        if (HasRestriction(card, RestrictionKind.SoleUnit)) return new BarNotice($"{Definition(card).Name} attacking or defending with other Personalities", Definition(card).Name).Message;
        if (HasRestriction(other, RestrictionKind.SoleUnit)) return new BarNotice($"Attacking or defending alongside {Definition(other).Name}", Definition(other).Name).Message;
        if (SameArmyPaired(card, other)) return $"{Definition(card).Name} and {Definition(other).Name} will not attack or defend in the same army.";
        return null;
    }

    // ---- the assignment command ------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>MatchEngine.Assign's check (through RequireRestrictionPairsMayAssign): the card may not join an army it conflicts with, whether the other unit stands there or is assigned by this same command.</summary>
    private void RequireArmyMayAssign(CardInstance card, int battlefield, IReadOnlyDictionary<string, int> assignments)
    {
        if (!ArmyRestrictionsExist()) return;
        foreach (var other in SideUnitsAt(card, battlefield, assignments))
        {
            var conflict = ArmyConflict(card, other);
            Require(conflict is null, conflict ?? "");
        }
    }

    // ---- the offered command -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The "assign everyone" command's units at one battlefield, cut down to a set that conflicts with nothing: those already in the army are kept in mind, units are admitted unrestricted first and a
    /// sole unit last (so it is offered with the rest only when it can stand with them, which it never can). Order is preserved.</summary>
    internal string[] ArmyAssignmentTemplate(string[] unitIds, int playerId, int battlefield)
    {
        if (unitIds.Length == 0 || !ArmyRestrictionsExist()) return unitIds;
        var kept = new List<CardInstance>(Army(playerId, battlefield));
        var admitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in unitIds.OrderBy(i => HasRestriction(Card(i), RestrictionKind.SoleUnit) ? 1 : 0))
        {
            var card = Card(id);
            if (kept.Any(k => k.Id != card.Id && ArmyConflict(card, k) is not null)) continue;
            kept.Add(card); admitted.Add(id);
        }
        return unitIds.Where(admitted.Contains).ToArray();
    }

    /// <summary>The sole units left out of the template: each is offered on its own (a lone Hida Kaiji), when no other Personality of its side is already at that battlefield.</summary>
    internal IEnumerable<string> ArmyAssignmentSoloUnits(string[] unitIds, string[] template, int playerId, int battlefield)
    {
        if (!ArmyRestrictionsExist() || Army(playerId, battlefield).Any()) yield break;
        foreach (var id in unitIds)
            if (!template.Contains(id) && HasRestriction(Card(id), RestrictionKind.SoleUnit)) yield return id;
    }
}
