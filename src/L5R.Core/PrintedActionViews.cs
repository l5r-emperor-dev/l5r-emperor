namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Verified alternate execution branches of one printed action. UsageKey alone is NOT
    // a print identity (Brawl/Folklore have distinct paragraphs sharing a limit).
    // Keep unverified future keys separate; expand this presentation-only list with print evidence.
    private static readonly HashSet<string> PrintedActionAliasKeys = new(StringComparer.Ordinal)
    {
        "p5-jouta-attack", "p5-tokiko", "p5-yutaka-return", "kikugoro-look", "celestial-sword-crane-open", "atsumichi-bow-straighten", "tenzan-penalty", "hayate-give", "jirou-accuse", "fumiko-bow", "tadanobu-duel", "kakihara-force-bonus",
        "p5-tsubute", "wlpa-dijuro", "isul-toggle", "wl1-karachu-give", "nori-bow", "hawado-move", "yoyugi-lock", "ikke-response", "kinto-exp-move", "hakige", "p5-foundries-discard", "wl-green-lake-dojo", "wl-merchant-outpost", "hajime-move",
        "hawado-exp-move", "heiwa-force", "izuna-secret", "umiko-attack", "hailung-attach", "p5-stolen-merchandise", "tsunefusa-give", "yorisada-toggle", "tsukimi-reflexes", "tamasine-boost", "kusugi-province", "wl-tao-of-fudo", "red-hunger", "beseech-sakkaku",
        "aya-keyword", "temple-elements-give", "ring-of-earth-strength", "w739-hidden-equip", "w739-tachikaze-attach", "tsujiken-exp-toggle", "yayu-attack", "p5-seek-stain-secret", "wl-pc-arakan", "wl-pc-iaimiko", "promo-mahatsu-element", "qalyar-recon",
        "nitoshi-inexp-look", "genji-province", "w741-whisper", "kazuko-fear", "sakonoko-negate", "proxy677-kaxt-create-home", "shakash", "osawa", "mitoshi-bow-straighten", "demiyah-gain-lose", "hama-toggle", "w737-reverse-fate", "horibe-bow-straighten",
        "horume-gain-lose", "jakuei-bow-favor", "pf-balash", "pf-yumi", "wind-of-the-moon-open", "celestial-sword-mantis-battle", "asako-house-guard-force", "waru-bow", "furu-bow-straighten", "tsudoken-exp-bow-straighten", "tsukasa-bow-straighten",
        "fubatsu-plate", "pcc-yotsu-toggle", "wl-horde", "wl-scouts", "pe-might-of-the-sun", "w821-governor-force", "wlpt-izuna-bow", "wlpt-tetsaka-keyword", "wlpt-daito-bribe", "wlpt-tansen", "chikata-toggle", "sakata-token", "flanking-unit-move",
        "promo-d-all-within", "pe-turquoise-court", "second-city-open"
    };
    private PrintedActionView[]? PrintedActionBranches(CardInstance card, CardDefinition def)
    {
        if (!card.FaceUp || !InPlay(card) || def.Abilities is not { Length: > 0 } ids) return null;
        return ids.Distinct().Where(id => AbilityCatalog.All.ContainsKey(id) && !TraitReactions.Is(id))
            .Where(id => AbilityCatalog.All[id].Timing is not ("FocusEffect" or "BeforeResolution")).Select(id =>
        {
            var ability = AbilityCatalog.All[id];
            var key = ability.UsageKey ?? id;
            var spent = ability.OncePerGame ? card.AbilityUses.ContainsKey(key)
                : AbilityUseSpent(card.AbilityUses, ability, key) && !UnsafePassageWaives(new("useAbility", card.OwnerId, _state.Version, CardId: card.Id, AbilityId: id), card);
            return new PrintedActionView([id], ability.Label, ability.Timing, spent);
        }).ToArray();
    }
    private PrintedActionView[]? PrintedActionViews(CardInstance card, CardDefinition def)
    {
        var branches = PrintedActionBranches(card, def);
        return branches?.GroupBy(row => AbilityCatalog.All[row.AbilityIds[0]].UsageKey is { } key && PrintedActionAliasKeys.Contains(key) ? "alias:" + key : "id:" + row.AbilityIds[0])
            .Select(group => new PrintedActionView(group.SelectMany(row => row.AbilityIds).ToArray(), string.Join(" / ", group.Select(row => row.Label).Distinct()),
                string.Join(" / ", group.Select(row => row.Timing).Distinct()), group.All(row => row.Spent))).ToArray();
    }
}
