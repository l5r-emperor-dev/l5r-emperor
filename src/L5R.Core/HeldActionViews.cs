namespace L5R.Core;

/// <summary>
/// Issue #1333: an action a player holds that a named or chosen card must take, so the client can say so ("Take the First Action Using ...").
/// Kind "first": the limit on that player's first Battle action (PerformerId: Naval Invasion, Scouting the Pass, Shosuro Toson; ActionKeyword: Jade's Roar;
/// MustPass: Rebuilding Kyuden Hida). Kind "additional": one or more identical additional actions held (Count), with what limits them: the Personality that
/// must perform (PerformerId), the cards it must come from (ScopeCardIds; FromDiscard when they are in a discard pile), the keyword it must carry, its own
/// printed abilities only (BaseAbilityOnly), named abilities only (ScopeAbilityIds), and whether it may be declined. Source names the card that made it.
/// PUBLIC: every grant and limit comes from an announced action, and both seats see the same list.
/// </summary>
public sealed record HeldActionView(int PlayerId, string Kind, string? SourceCardId, string? SourceName, string? PerformerId, string[]? ScopeCardIds,
    string? ActionKeyword, bool MustPass, bool BaseAbilityOnly, bool FromDiscard, string[]? ScopeAbilityIds, bool MayDecline, int Count);

public sealed partial class MatchEngine
{
    private HeldActionView[]? BuildHeldActions()
    {
        var held = new List<HeldActionView>();
        if (_state.FirstActionRestriction is { } limit)
        {
            var source = limit.MustPass ? "Rebuilding Kyuden Hida" : limit.ActionKeyword is not null ? "Jade's Roar" : limit.Reason ?? FirstPerformerSource();
            held.Add(new(limit.PlayerId, "first", null, source, limit.PerformerId, null, limit.ActionKeyword, limit.MustPass, false, false, null, false, 1));
        }
        foreach (var group in _state.AdditionalActions
            .Where(g => g.ScopeCardIds is not null || g.PerformerId is not null || g.RequiredActionKeyword is not null || g.ScopeAbilityIds is not null)
            .GroupBy(g => (g.PlayerId, Scope: string.Join(",", g.ScopeCardIds ?? []), g.PerformerId, g.RequiredActionKeyword, g.BaseAbilityOnly, g.FromDiscard, Abilities: string.Join(",", g.ScopeAbilityIds ?? []), g.Mandatory, g.SourceCardId)))
        {
            var grant = group.First();
            var source = grant.SourceCardId is { } id && _state.Cards.TryGetValue(id, out var card) ? Definition(card).Name : null;
            held.Add(new(grant.PlayerId, "additional", grant.SourceCardId, source, grant.PerformerId, grant.ScopeCardIds, grant.RequiredActionKeyword, false,
                grant.BaseAbilityOnly, grant.FromDiscard, grant.ScopeAbilityIds, !grant.Mandatory, group.Count()));
        }
        return held.Count == 0 ? null : held.ToArray();
    }
}
