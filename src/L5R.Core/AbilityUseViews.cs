namespace L5R.Core;

/// <summary>
/// One printed ability of a face-up card in play, as the client marks it on the card art (issue #1309).
/// Used counts this turn's uses (0 or 1 for a once-per-game ability); Max is the effective limit now, extra uses included.
/// Spent: no use is left. Permanent: it will not come back this game (once per game, or a first-turn-only ability after that turn).
/// Public information: both players watched the uses happen. A use left is not a promise that the ability is legal now.
/// </summary>
public sealed record AbilityUseView(string Key, string[] AbilityIds, int Used, int Max, bool Spent, bool Permanent);

public sealed partial class MatchEngine
{
    // Abilities printed "during your first turn (only)": the box greys for good once that player's first turn is over, used or not.
    private static readonly HashSet<string> FirstTurnOnlyAbilities = new(StringComparer.Ordinal) { "border-first", WlHoldingsAbilities.BorderKeepRefresh };

    private AbilityUseView[]? AbilityUseViews(CardInstance card, CardDefinition def)
    {
        if (!card.FaceUp || !InPlay(card) || def.Abilities is not { Length: > 0 } ids) return null;
        var owner = Player(card.OwnerId);
        var firstTurnOver = owner.TurnsTaken > 1 || owner.TurnsTaken == 1 && _state.ActivePlayerId != owner.Id;
        var rows = AbilityRegionCards.Groups(ids)
            .Select(group =>
            {
                var ability = AbilityCatalog.All[group.Ids[0]];
                var key = group.Key;
                var abilityIds = group.Ids;
                if (abilityIds.Any(FirstTurnOnlyAbilities.Contains) && firstTurnOver) return new AbilityUseView(key, abilityIds, 0, 0, true, true);
                if (BespokeUseView(card, key, abilityIds) is { } bespoke) return bespoke;
                if (ability.OncePerGame || ability.UsesPer == Period.Game)
                {
                    var used = ability.OncePerGame ? card.AbilityUses.ContainsKey(key) : AbilityUseSpent(card.AbilityUses, ability, key);
                    return new AbilityUseView(key, abilityIds, used ? 1 : 0, 1, used, used);
                }
                if (ability.UsesPer is not null)
                {
                    var spentPeriod = AbilityUseSpent(card.AbilityUses, ability, key);
                    return new AbilityUseView(key, abilityIds, spentPeriod ? 1 : 0, 1, spentPeriod, false);
                }
                var max = EffectiveUsesPerTurn(card.AbilityUses, ability);
                var usedThisTurn = Math.Min(AbilityUsesThisTurn(card.AbilityUses, ability, key), max);
                var spent = AbilityUseSpent(card.AbilityUses, ability, key)
                    && !UnsafePassageWaives(new("useAbility", card.OwnerId, _state.Version, CardId: card.Id, AbilityId: abilityIds[0]), card);
                return new AbilityUseView(key, abilityIds, usedThisTurn, max, spent, false);
            }).ToArray();
        return rows.Length == 0 ? null : rows;
    }

    // Limits enforced by a card's own code rather than AbilityDefinition (AbilityRegionCards.BespokeLimited). Only meaningful during the battle they count.
    private AbilityUseView? BespokeUseView(CardInstance card, string key, string[] abilityIds)
    {
        var inBattle = _state.CurrentBattleProvince is not null;
        if (abilityIds.Any(id => id.StartsWith("tsukimi-", StringComparison.Ordinal)))
        {
            var used = inBattle ? TsukimiUsesThisBattle(card) : 0;
            return new AbilityUseView(key, abilityIds, used, 2, used >= 2, false);
        }
        if (abilityIds.Contains("shadows-lair-move"))
        {
            var used = inBattle && card.AbilityUses.TryGetValue(ShadowsLairBattleKey, out var battle) && battle == _state.BattleNumber;
            return new AbilityUseView(key, abilityIds, used ? 1 : 0, 1, used, false);
        }
        return null;
    }

    /// <summary>A face-up Tactician Personality in play: Tactical actions he performed this turn and his cap; null otherwise (issue #1309).</summary>
    private TacticalUseView? TacticalUseView(CardInstance card, CardDefinition def) =>
        card.FaceUp && InPlay(card) && def.Type == CardType.Personality && Keyword(card, "Tactician")
            ? new TacticalUseView(TacticalActionsThisTurn(card.Id), TacticalCap(card)) : null;

    /// <summary>Issue #1329: the printed keywords of a face-up card that are removed now (permanently or for a while), in printed order; null when none.</summary>
    private static string[]? RemovedPrintedKeywordsView(CardInstance card, CardDefinition def)
    {
        if (!card.FaceUp || card.RemovedKeywords.Count == 0 || def.Keywords is not { Length: > 0 } printed) return null;
        var removed = printed.Where(k => card.RemovedKeywords.Contains(k, StringComparer.Ordinal)).ToArray();
        return removed.Length == 0 ? null : removed;
    }
}

/// <summary>Tactical actions a Tactician performed this turn against his cap (one, or three for Moto Taigo). Public.</summary>
public sealed record TacticalUseView(int Used, int Cap);

/// <summary>
/// Issue #1309: which printed abilities the client marks on the card art. A limited (not "any number of times") player-usable ability of an in-play card,
/// grouped by the limit it spends; plus printed Tacticians. Shared by the engine view, the region tool (tools/AbilityRegions) and its coverage test.
/// </summary>
public static class AbilityRegionCards
{
    public sealed record Group(string Key, string[] Ids);
    public sealed record Row(CardDefinition Card, Group[] Groups, bool PrintedTactician)
    {
        /// <summary>Issue #1329: a Personality's printed keywords, each located on the scan so it can be covered while removed.</summary>
        public string[] Keywords => Card.Type == CardType.Personality ? Card.Keywords ?? [] : [];
    }

    public static readonly CardType[] InPlayTypes = [CardType.Stronghold, CardType.Holding, CardType.Personality, CardType.Follower, CardType.Item, CardType.Spell, CardType.Celestial, CardType.Region, CardType.Ring];

    // Printed limits the engine enforces in the card's own code, with AbilityDefinition left "any number of times" (MatchEngine.BespokeUseView):
    // Shiba Tsukimi's "twice per battle" Reactions and The Shadow's Lair's "once per battle".
    public static readonly HashSet<string> BespokeLimited = new(StringComparer.Ordinal) { "tsukimi-home", "tsukimi-battlefield", "shadows-lair-move" };

    public static Group[] Groups(IEnumerable<string> abilityIds) => abilityIds.Distinct()
        .Where(id => AbilityCatalog.All.TryGetValue(id, out var a) && !TraitReactions.Is(id) && a.Timing is not ("FocusEffect" or "BeforeResolution")
            && (a.UsesPerTurn != AbilityUseLimit.Unlimited || a.OncePerGame || a.UsesPer is not null || BespokeLimited.Contains(id)))
        .GroupBy(id => AbilityCatalog.All[id].UsageKey ?? id).Select(g => new Group(g.Key, g.ToArray())).ToArray();

    public static IEnumerable<Row> Rows() => CardCatalog.PlayableFromDecks.Where(c => InPlayTypes.Contains(c.Type))
        .Select(c => new Row(c, Groups(c.Abilities ?? []), c.Type == CardType.Personality && (c.Keywords ?? []).Contains("Tactician")))
        .Where(r => r.Groups.Length > 0 || r.PrintedTactician || r.Keywords.Length > 0).OrderBy(r => r.Card.Id, StringComparer.Ordinal);
}
