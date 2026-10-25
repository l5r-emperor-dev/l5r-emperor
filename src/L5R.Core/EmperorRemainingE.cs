namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group E: the Spells of spell-wave-23 (Capturing the Soul, Cleansing the Path, Consecration, Contemplate the Void, Hanabi, I Give You My Name,
// Obscured Pathways, The Kami's Blessing, Thunder's Favor, Unnatural Flood, Warded Paths) and the Kami Celestials of celestial-wave-8 (the nine "Guidance" cards). Chikushudo's Trickery and Summon Swamp
// Spirits stay unwired: see tools/content/ee_remE_blockers.json.
//
// The declarative ability data is here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file, each naming its card.
//
// New primitives (each reads the core rules; none conflicts with them):
//   * COSTS: "discard your hand (even if empty)" (CostVerbs.DiscardHand), "bow every card chosen for a slot" (CostVerbs.BowSlotCards -- the other performing Shugenja of Capturing the Soul) and a bow owed only
//     "if he is home" (ActionCost.IfSubjectHome -- Obscured Pathways).
//   * SEARCH: places searched in order ("your discard pile, then Fate deck", SearchSpec.Ordered), the Dynasty discard pile as a searchable place (SearchDeck.DynastyDiscard), a Personality put into play by a
//     search ignoring its cost (SearchEffect.PutIntoPlay, with the entering-play window opened as the search ends), a card put into a chosen province face-up (SearchEffect.ToProvinceFaceUp), a Gold Cost ceiling read
//     from the action (PendingSearch.GoldLimit) and an Honor loss owed however the search ends (PendingSearch.HonorLossOnFinish).
//   * WATCHES: Consecration's "after the first time this turn a Personality assigns or moves to an attacking army at one of those provinces' battlefields" (MatchState.ConsecrationWatches).
//   * CELESTIAL TRAITS, all read live off the Celestials in play (either player's, as printed): Akodo's Guidance (Lion Clan players may target defending enemy units with the rulebook Favor Battle action),
//     Bayushi's Guidance (a Scorpion Clan player's turn begins: the others lose 1 Honor), Daigotsu's Guidance (a Celestial enters play: every Kami Celestial is discarded), Doji's Guidance (Crane Clan players take
//     the Favor Battle action with no unit at the battlefield), Hida's Guidance (the first straightening of a Personality each battle is negated when his enemy leader is a Crab Clan player), Shiba's Guidance
//     (a battle resolution in which a Phoenix Clan player destroyed provinces or cards: he draws), Shinjo's Guidance (+1F to Unicorn Clan Personalities with an attachment), Togashi's Guidance (+2 maximum hand
//     size for Dragon Clan players) and Yoritomo's Guidance (after engaging, a Mantis Clan Attacker's battlefield province is reduced to strength 5).
//   * WARDED PATHS: +3F while its Shugenja defends; a Defender with an Earth Shugenja carrying it chooses the order in which the battles are fought (the battle-selection priority).
//   * A Ring put into play by another card's text does not count for Enlightenment (core rules: each Ring must have most recently entered play via its own printed text) -- Togashi's Guidance.
public sealed record ConsecrationWatch(int PlayerId, int[] Provinces, int Turn, string SourceId);

public static class EmperorRemainingEAbilities
{
    /// <summary>"Choose one or two of your provinces" (Consecration): the indices of the acting player's own provinces, carried in GameCommand.Provinces.</summary>
    public const string OwnProvincesTarget = "own-provinces-one-or-two";

    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingE(this Dictionary<string, AbilityDefinition> all)
    {
        // ---- Spells (spell-wave-23) ----
        // Capturing the Soul (oracle-l5r-1216, a Ritual Spell): "Limited: Bow this Shugenja and 0 or more of your other performing Shugenja, target a Personality, and remove this card from the game: If the
        // total Chi of all your performing Shugenja is higher than three times the Chi of the Personality, remove him (and his attachments) from the game." The wielder performs it (named, since the Spell leaves
        // play as a cost); the other performing Shugenja are a performing slot of any size, bowed as a cost each.
        all["capturing-soul-remove"] = new("Limited", "Ritual Limited: Bow this Shugenja and 0 or more of your other performing Shugenja, target a Personality, and remove this card from the game: If the total Chi of all your performing Shugenja is higher than three times the Chi of the Personality, remove him (and his attachments) from the game", "slots",
            Performer: "own-wielder",
            Slots: [new("others", "own-unbowed-shugenja-other-than-wielder", "Your other performing Shugenja", 0, 6, Performing: true), new("target", "personality", "A Personality")],
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.BowSlotCards, CostSubject.Slot("others")), new(CostVerbs.Remove)]);
        // Cleansing the Path (oracle-l5r-1416, a Fire Spell): "Battle: Destroy this card: Ranged 4 Attack, or Ranged 5 Attack if the performing Shugenja is Fire." Thunder's Wrath's shape: the Spell is destroyed
        // as the cost, so its wielder (the performer of a Spell's action) is named to be read afterwards.
        all["cleansing-path-attack"] = new("Battle", "Destroy this card: Ranged 4 Attack, or Ranged 5 Attack if the performing Shugenja is Fire", "attack", Performer: "own-wielder", Costs: [new(CostVerbs.Sacrifice)]);
        // Consecration (oracle-l5r-1458, an Earth Spell): "Open: Bow this card: Choose one or two of your provinces. After the first time this turn a Personality assigns or moves to an attacking army at one of
        // those provinces' battlefields, gain 2 Honor."
        all["consecration-watch"] = new("Open", "Bow this card: Choose one or two of your provinces. After the first time this turn a Personality assigns or moves to an attacking army at one of those provinces' battlefields, gain 2 Honor", OwnProvincesTarget,
            Costs: ActionCost.BowSource);
        // Contemplate the Void (oracle-l5r-1471, a Void Spell): "Limited: Bow this Shugenja, destroy this card, and discard your hand (even if empty): Draw four cards."
        all["contemplate-void-draw"] = new("Limited", "Bow this Shugenja, destroy this card, and discard your hand (even if empty): Draw four cards", "none",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice), new(CostVerbs.DiscardHand)]);
        // Hanabi (oracle-l5r-2954, a Unique Spell): "Political Limited: Bow this Shugenja and destroy this card: Gain 4 Honor."
        all["hanabi-honor"] = new("Limited", "Political Limited: Bow this Shugenja and destroy this card: Gain 4 Honor", "none", Performer: "own-wielder", ActionKeywords: ["Political"],
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)]);
        // I Give You My Name (oracle-l5r-3541, a Maho Spell): "Limited: Bow this Shugenja and destroy your target Human Personality: Search your Dynasty deck or discard pile for a non-Unique Oni with Gold Cost
        // lower than or equal to the Human's Gold Cost plus the performer's Chi. Put the Oni into play, ignoring Gold cost. Lose 2 Honor."
        all["i-give-you-my-name-search"] = new("Limited", "Bow this Shugenja and destroy your target Human Personality: Search your Dynasty deck or discard pile for a non-Unique Oni with Gold Cost lower than or equal to the Human's Gold Cost plus the performer's Chi. Put the Oni into play, ignoring Gold cost. Lose 2 Honor", "own-human-personality",
            Performer: "own-wielder", Search: EmperorRemainingESearches.IGiveYouMyName, Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice, CostSubject.Target)]);
        // Obscured Pathways (oracle-l5r-5649, an Earth Forest Spell): "While this Shugenja is Earth, this card's Battle ability may be used even if this unit is not at the current battlefield. Battle: Bow this
        // Shugenja if he is home, bow this card and target an attacking Personality: Move him home."
        all["obscured-pathways-move"] = new("Battle", "Bow this Shugenja if he is home, bow this card and target an attacking Personality: Move him home", "attacking-personality",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder, IfSubjectHome: true), new(CostVerb.Bow)], SourceUnitMayBeAwayIfWielder: "Earth");
        // The Kami's Blessing (oracle-l5r-8175, a Kami Spell): "Battle/Open: Bow this card: Give this Shugenja a Force bonus equal to his Chi." and "Battle: Bow this card and target one or two Samurai: Give each of them
        // a Force bonus equal to this Shugenja's Chi."
        all["kami-blessing-self"] = new("BattleOrOpen", "Bow this card: Give this Shugenja a Force bonus equal to his Chi", "none", Costs: ActionCost.BowSource);
        all["kami-blessing-samurai"] = new("Battle", "Bow this card and target one or two Samurai: Give each of them a Force bonus equal to this Shugenja's Chi", "slots", RequiredKeywords: ["Samurai"],
            Slots: [new("samurai", "personality-with-keyword", "One or two Samurai", 1, 2)], Costs: ActionCost.BowSource);
        // Thunder's Favor (oracle-l5r-8473, a Thunder Spell): "Battle: Bow this card: Ranged 4 Attack, or Ranged 6 Attack if this Shugenja is Thunder."
        all["thunders-favor-attack"] = new("Battle", "Bow this card: Ranged 4 Attack, or Ranged 6 Attack if this Shugenja is Thunder", "attack", Costs: ActionCost.BowSource);
        // Unnatural Flood (oracle-l5r-8997, a Unique Water Spell): "Battle: Bow this Shugenja and target an enemy Personality: Move him home; if this Shugenja is Water, this movement will not be negated. If the
        // target moved, bow him and negate his (future) movement."
        all["unnatural-flood-move"] = new("Battle", "Bow this Shugenja and target an enemy Personality: Move him home; if this Shugenja is Water, this movement will not be negated. If the target moved, bow him and negate his (future) movement", "enemy-personality",
            Costs: ActionCost.BowWielder);

        // ---- Celestials (celestial-wave-8) ----
        // Akodo's Guidance (oracle-l5r-311): "Battle: If you are a Lion Clan player, discard this card and choose your performing Personality: Melee 4 Attack." (The Favor trait is its Effect.)
        all["akodo-guidance-melee"] = new("Battle", "If you are a Lion Clan player, discard this card and choose your performing Personality: Melee 4 Attack", "attack", Performer: "own-personality-at-battle",
            Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Bayushi's Guidance (oracle-l5r-934): "Open: If you are a Scorpion Clan player, discard this card and target one or more face-up Personalities in provinces: Put each one at the bottom of its owner's deck in
        // any order. Refill the provinces face-up." The order is the order the targets were named in.
        all["bayushi-guidance-return"] = new("Open", "If you are a Scorpion Clan player, discard this card and target one or more face-up Personalities in provinces: Put each one at the bottom of its owner's deck in any order. Refill the provinces face-up", "slots",
            Slots: [new("personalities", "face-up-personality-in-province", "One or more face-up Personalities in provinces", 1, 8)], Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Daigotsu's Guidance (oracle-l5r-1788): "Battle: If you are a Spider Clan player, discard this card and target an enemy unit: Destroy all bowed cards in it. Bow it."
        all["daigotsu-guidance-destroy"] = new("Battle", "If you are a Spider Clan player, discard this card and target an enemy unit: Destroy all bowed cards in it. Bow it", "enemy-unit", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Doji's Guidance (oracle-l5r-2181): "Open: If you are a Crane Clan player, discard this card: Put a non-Unique Strategy with a Political ability in your discard pile into your hand."
        all["dojis-guidance-return"] = new("Open", "If you are a Crane Clan player, discard this card: Put a non-Unique Strategy with a Political ability in your discard pile into your hand", "own-nonunique-political-strategy-in-discard",
            Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Hida's Guidance (oracle-l5r-3250): "Battle: If you are a Crab Clan player, discard this card and target a Personality: Bow him. Bow his unit if he is attacking."
        all["hidas-guidance-bow"] = new("Battle", "If you are a Crab Clan player, discard this card and target a Personality: Bow him. Bow his unit if he is attacking", "personality", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Shiba's Guidance (oracle-l5r-6836): "Battle/Open: If you are a Phoenix Clan player, discard this card: Put a non-Unique Spell or Kiho in your discard pile into your hand."
        all["shibas-guidance-return"] = new("BattleOrOpen", "If you are a Phoenix Clan player, discard this card: Put a non-Unique Spell or Kiho in your discard pile into your hand", "own-nonunique-spell-or-kiho-in-discard",
            Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Shinjo's Guidance (oracle-l5r-6978): "Limited: Discard this card: Search your discard pile, then Dynasty deck, for Moto Naleesh and put her into one of your provinces, face-up, discarding the card that
        // was there."
        all["shinjos-guidance-search"] = new("Limited", "Discard this card: Search your discard pile, then Dynasty deck, for Moto Naleesh and put her into one of your provinces, face-up, discarding the card that was there", "none",
            Search: EmperorRemainingESearches.ShinjosGuidance, Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Togashi's Guidance (oracle-l5r-8603): "Battle/Open: If you are a Dragon Clan player, discard this card: You may either put a Ring in your hand into play, or search your discard pile, then Fate deck,
        // for a Ring and put it into your hand." The two halves are two abilities of one card (the discard is the cost of either).
        all["togashis-guidance-ring-play"] = new("BattleOrOpen", "If you are a Dragon Clan player, discard this card: Put a Ring in your hand into play", "own-ring-in-hand", Costs: [new(CostVerbs.DiscardFromPlay)]);
        all["togashis-guidance-ring-search"] = new("BattleOrOpen", "If you are a Dragon Clan player, discard this card: Search your discard pile, then Fate deck, for a Ring and put it into your hand", "none",
            Search: EmperorRemainingESearches.TogashisGuidance, Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Yoritomo's Guidance (oracle-l5r-9662): "Limited: If you are a Mantis Clan player, discard this card and target your discarded Port Holding: Put it into play (bowed), paying all costs but paying 2 less
        // Gold." (The engage trait is its Effect.)
        all["yoritomos-guidance-port"] = new("Limited", "If you are a Mantis Clan player, discard this card and target your discarded Port Holding: Put it into play (bowed), paying all costs but paying 2 less Gold", "own-discarded-port-holding",
            Cost: GoldCostRule.TargetPrinted(-2, 0), Costs: [new(CostVerbs.DiscardFromPlay)]);
        return all;
    }
}

public static class EmperorRemainingESearches
{
    private static readonly SearchCriteria NonUniqueOni = new("non-Unique Oni", [CardType.Personality], ["Oni"], NonUnique: true);
    private static readonly SearchCriteria MotoNaleesh = new("Moto Naleesh", [CardType.Personality], Titles: ["Moto Naleesh"]);
    private static readonly SearchCriteria RingCard = new("Ring", [CardType.Ring]);

    /// <summary>I Give You My Name: "Search your Dynasty deck or discard pile for a non-Unique Oni ... Put the Oni into play, ignoring Gold cost." (The Gold Cost ceiling is the action's: PendingSearch.GoldLimit.)</summary>
    public static SearchSpec IGiveYouMyName { get; } = new("i-give-you-my-name", "Search your Dynasty deck or discard pile for a non-Unique Oni",
    [
        new(SearchDeck.Dynasty, NonUniqueOni, SearchEffect.PutIntoPlay),
        new(SearchDeck.DynastyDiscard, NonUniqueOni, SearchEffect.PutIntoPlay)
    ]);

    /// <summary>Shinjo's Guidance: "Search your discard pile, then Dynasty deck, for Moto Naleesh and put her into one of your provinces, face-up, discarding the card that was there."</summary>
    public static SearchSpec ShinjosGuidance { get; } = new("shinjos-guidance", "Search your discard pile, then Dynasty deck, for Moto Naleesh",
    [
        new(SearchDeck.DynastyDiscard, MotoNaleesh, SearchEffect.ToProvinceFaceUp),
        new(SearchDeck.Dynasty, MotoNaleesh, SearchEffect.ToProvinceFaceUp)
    ], Ordered: true);

    /// <summary>Togashi's Guidance: "search your discard pile, then Fate deck, for a Ring and put it into your hand."</summary>
    public static SearchSpec TogashisGuidance { get; } = new("togashis-guidance", "Search your discard pile, then Fate deck, for a Ring",
    [
        new(SearchDeck.FateDiscard, RingCard, SearchEffect.ToHand),
        new(SearchDeck.Fate, RingCard, SearchEffect.ToHand)
    ], Ordered: true);

    public static IEnumerable<SearchSpec> All => [IGiveYouMyName, ShinjosGuidance, TogashisGuidance];
}

public sealed partial class MatchEngine
{
    // ---- Effect keys (the Effect field of the published cards) ---------------------------------------------------------------------------------------
    internal const string WardedPathsEffect = "warded-paths";
    internal const string AkodosGuidanceEffect = "akodos-guidance";
    internal const string BayushisGuidanceEffect = "bayushis-guidance";
    internal const string DaigotsusGuidanceEffect = "daigotsus-guidance";
    internal const string DojisGuidanceEffect = "dojis-guidance";
    internal const string HidasGuidanceEffect = "hidas-guidance";
    internal const string ShibasGuidanceEffect = "shibas-guidance";
    internal const string ShinjosGuidanceEffect = "shinjos-guidance";
    internal const string TogashisGuidanceEffect = "togashis-guidance";
    internal const string YoritomosGuidanceEffect = "yoritomos-guidance";

    // A Ring a Togashi's Guidance put into play: it does not count for Enlightenment while it stays in play.
    private const string NoEnlightenmentKey = "ee-e:no-enlightenment";
    // Hida's Guidance: the battle (MatchState.BattleNumber) in which a Personality's first straightening was already negated.
    private const string HidaStraightenedBattleKey = "ee-e:hida-straightened-battle";
    // A Ring entered play this action through another card's text (recorded on the action for "after one of your Rings enters play").
    internal const string RingEnteredKind = "ring-entered";
    // I Give You My Name: the Human's Gold Cost, read before the cost destroys him.
    private int _humanGoldCostForSearch;

    // Test seams (issue #492): straighten a card as an effect would; a province's current strength.
    internal bool StraightenForTest(string cardId) { Straighten(Card(cardId)); return !Card(cardId).Bowed; }
    internal int ProvinceStrengthForTest(int owner, int index) => EffectiveProvinceStrength(Player(owner).Provinces.Single(p => p.Index == index));

    // How many Celestials in play (either player's) carry the Effect.
    private int CelestialsInPlay(string effect) => _state.Cards.Values.Count(c => c.Zone == Zone.Celestial && Definition(c).Effect == effect);

    // ---- Static Force: Warded Paths and Shinjo's Guidance (PersonalityForce's hook) ---------------------------------------------------------------------
    private int EmperorRemainingEForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) return 0;
        var force = 0;
        // Warded Paths: "While this Shugenja is defending, he has +3F." (One for each Spell carrying it.)
        if (card.Zone == Zone.Battlefield && IsDefendingUnit(card)) force += 3 * StaticAttachedWithEffect(card, WardedPathsEffect).Count();
        // Shinjo's Guidance: "Unicorn Clan Personalities have +1F while they have an attachment." (Either player's, one for each Celestial.)
        if (HasClanAffiliation(card, "Unicorn") && HasAttachments(card)) force += CelestialsInPlay(ShinjosGuidanceEffect);
        return force;
    }

    // ---- Togashi's Guidance: "Dragon Clan players' maximum hand size is increased by two" ------------------------------------------------------------------
    private IEnumerable<HandLimitModifier> EmperorRemainingEHandLimit(int playerId) =>
        Player(playerId).Clan == "Dragon"
            ? _state.Cards.Values.Where(c => c.Zone == Zone.Celestial && Definition(c).Effect == TogashisGuidanceEffect).OrderBy(c => c.Id, StringComparer.Ordinal)
                .Select(c => new HandLimitModifier(c.Id, Definition(c).Name, 2))
            : [];

    // Enlightenment: a Ring put into play by another card's text is not one that "most recently entered play via its own printed text".
    // Issue #699 (WlRings.cs): read off the Ring's entry record (how it LAST entered play); a Ring recruited by its own text again counts afresh.
    private bool CountsForEnlightenment(CardInstance ring) => RingLastEnteredByOwnText(ring);

    // ---- The rulebook Favor Political Battle action: Akodo's and Doji's Guidance ----------------------------------------------------------------------------
    // Akodo's Guidance: "Lion Clan players may target defending enemy units with the rulebook Favor Political Battle action." (An extra legal target, beside the attacking enemy Personalities.)
    private bool AkodoAllowsDefendingFavorTarget(string abilityId, PlayerState player, CardInstance target) =>
        abilityId == "favor-battle" && player.Clan == "Lion" && CelestialsInPlay(AkodosGuidanceEffect) > 0 && target.OwnerId != player.Id && IsDefendingUnit(target);

    // Doji's Guidance: "Crane Clan players may take the rulebook Favor Political Battle action even if they control no units at the current battlefield."
    private bool DojiWaivesFavorPresence(string abilityId, PlayerState player) =>
        abilityId == "favor-battle" && player.Clan == "Crane" && CelestialsInPlay(DojisGuidanceEffect) > 0;

    // ---- Hida's Guidance (Straighten's hook) -----------------------------------------------------------------------------------------------------------------
    // "Before the first straightening each battle of a Personality whose enemy leader is a Crab Clan player: Negate that straightening." The enemy leader of a Personality is the other player (two players),
    // and a straightening that would change nothing (he is not bowed) is not one.
    private bool HidaNegatesStraightening(CardInstance card)
    {
        if (_state.Phase != Phase.Battle || !card.Bowed || Definition(card).Type != CardType.Personality || !InPlay(card)) return false;
        if (CelestialsInPlay(HidasGuidanceEffect) == 0 || Player(1 - card.OwnerId).Clan != "Crab") return false;
        if (CurrentActionUnnegatable()) return false; // Suitengu's Torch: the protected action's effects will not be negated.
        if (card.AbilityUses.TryGetValue(HidaStraightenedBattleKey, out var battle) && battle == _state.BattleNumber) return false;
        card.AbilityUses[HidaStraightenedBattleKey] = _state.BattleNumber;
        Emit("straightenNegated", $"{Definition(card).Name}'s first straightening this battle was negated (Hida's Guidance).", card.OwnerId, card.Id);
        return true;
    }

    // ---- Daigotsu's Guidance (ResolveCelestial's hook) ---------------------------------------------------------------------------------------------------------
    // "After a Celestial enters play (including this one): Discard all Kami Celestials in play." A trait, so it is applied at once, for every Daigotsu's Guidance in play.
    private void ApplyDaigotsusGuidanceTrait()
    {
        if (CelestialsInPlay(DaigotsusGuidanceEffect) == 0) return;
        foreach (var kami in _state.Cards.Values.Where(c => c.Zone == Zone.Celestial && Keyword(c, "Kami")).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            kami.Zone = Zone.DynastyDiscard;
            Emit("celestialDiscarded", $"{Definition(kami).Name} was discarded (Daigotsu's Guidance).", kami.OwnerId, kami.Id);
        }
    }

    // ---- Bayushi's Guidance (StartTurn's hook) -----------------------------------------------------------------------------------------------------------------
    // "After a player's turn begins, if he is Scorpion Clan: Each other player loses 1 Honor." (Once for each Bayushi's Guidance in play.)
    private void ApplyBayushisGuidanceTrait(PlayerState turnPlayer)
    {
        if (turnPlayer.Clan != "Scorpion") return;
        foreach (var guidance in _state.Cards.Values.Where(c => c.Zone == Zone.Celestial && Definition(c).Effect == BayushisGuidanceEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            foreach (var other in _state.Players.Where(p => p.Id != turnPlayer.Id))
                GainHonor(other.Id, -1, "Bayushi's Guidance", guidance);
    }

    // ---- Shiba's Guidance (the resolution's hook) ---------------------------------------------------------------------------------------------------------------
    // "After a battle resolution ends in which a Phoenix Clan player destroyed one or more provinces or enemy cards: He draws a card." A trait; the resolution's own facts (who destroyed what) are the ones
    // the after-resolution Reactions read.
    private void ApplyShibasGuidanceTrait(int[] destroyedEnemyUnits, int? provinceDestroyedBy)
    {
        var guidances = CelestialsInPlay(ShibasGuidanceEffect);
        if (guidances == 0) return;
        foreach (var player in _state.Players.Where(p => p.Clan == "Phoenix" && (destroyedEnemyUnits[p.Id] > 0 || provinceDestroyedBy == p.Id)))
            for (var i = 0; i < guidances; i++) { Emit("guidanceTrait", $"{player.Name} destroyed cards or a province in the resolution: Shiba's Guidance draws a card.", player.Id); Draw(player); }
    }

    // ---- Yoritomo's Guidance (SelectBattle's hook) -----------------------------------------------------------------------------------------------------------
    // "After engaging, if the Attacker is a Mantis Clan player: Reduce the current battlefield's province's strength to 5." Reduce (a lasting change of the province's strength, like every lowering of it):
    // a province already at 5 or less is left alone.
    private void ApplyYoritomosGuidanceAfterEngaging(int provinceIndex)
    {
        if (CelestialsInPlay(YoritomosGuidanceEffect) == 0 || Player(_state.ActivePlayerId).Clan != "Mantis") return;
        if (BattlefieldProvince(provinceIndex) is not { } province) return; // a battlefield without a province has no strength (issue #492, rulings pass R1)
        var current = EffectiveProvinceStrength(province);
        if (current <= 5) return;
        GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, provinceIndex, 5 - current, "Yoritomo's Guidance"); // reduced to 5 until end of turn (issue #492 ruling R3)
    }

    // ---- Consecration (Assign's and MoveToBattlefield's hook) ---------------------------------------------------------------------------------------------------
    // "After the first time this turn a Personality assigns or moves to an attacking army at one of those provinces' battlefields, gain 2 Honor." The battlefields are the Defender's provinces, so a watch of
    // the Attacker's never fires; each watch fires once.
    private void FireConsecrationWatches(CardInstance personality, int province)
    {
        if (_state.ConsecrationWatches.Count == 0 || Definition(personality).Type != CardType.Personality || personality.OwnerId != _state.ActivePlayerId) return;
        var defender = 1 - _state.ActivePlayerId;
        foreach (var watch in _state.ConsecrationWatches.Where(w => w.PlayerId == defender && w.Turn == _state.TurnNumber && w.Provinces.Contains(province)).ToArray())
        {
            _state.ConsecrationWatches.Remove(watch);
            var watcher = _state.Cards.TryGetValue(watch.SourceId, out var spell) ? spell : null;
            var watchName = watcher is null ? "Consecration" : Definition(watcher).Name; // Asahina Shigemitsu - exp (issue #737, Wave737.cs) sets the same watch
            Emit("consecration", $"{Definition(personality).Name} joined the attacking army at province {province + 1} ({watchName}).", defender, personality.Id);
            GainHonor(defender, 2, watchName, watcher);
        }
    }

    // ---- Warded Paths (the battle-selection hook) --------------------------------------------------------------------------------------------------------------
    // "While this Shugenja is Earth and you are the Defender, you decide the order in which battles resolve": the Defender picks the next battlefield instead of the Attacker.
    private int BattleSelectionChooser(int attacker)
    {
        var defender = 1 - attacker;
        var decides = _state.Cards.Values.Any(c => c.OwnerId == defender && InPlay(c) && Definition(c).Effect == WardedPathsEffect && WielderOf(c) is { } shugenja && Keyword(shugenja, "Earth"));
        return decides ? defender : attacker;
    }

    private void SetPhaseAfterManeuvers(int attacker)
    {
        if (_state.UnresolvedBattlefields.Count > 0) SetPhase(Phase.BattleSelection, BattleSelectionChooser(attacker));
        else BeginPhaseAfterAttack(attacker); // issue #667
    }

    // ---- Target categories -------------------------------------------------------------------------------------------------------------------------------
    // In-play categories (ValidateAbility's fallback; the QueryActions default lists every card in play, this judges each).
    private bool EmperorRemainingETargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        return category switch
        {
            // Capturing the Soul: "0 or more of your other performing Shugenja" -- unbowed (bowing them is a cost), other than the Spell's own Shugenja.
            "own-unbowed-shugenja-other-than-wielder" => personality && own && !target.Bowed && Keyword(target, "Shugenja") && (source is null || target.Id != source.AttachedTo),
            // The Kami's Blessing: "one or two Samurai" -- either player's Personalities carrying the ability's RequiredKeywords.
            "personality-with-keyword" => personality && HasKeywords(target, AbilityCatalog.All[command.AbilityId!].RequiredKeywords),
            // I Give You My Name: "your target Human Personality" (a Human is a Personality without the Nonhuman keyword).
            "own-human-personality" => personality && own && !Keyword(target, "Nonhuman"),
            _ => false
        };
    }

    // Out-of-play categories: judged (and listed) by one predicate.
    private static bool IsEmperorRemainingEOutOfPlayTarget(string category) =>
        category is "own-nonunique-political-strategy-in-discard" or "own-nonunique-spell-or-kiho-in-discard" or "own-ring-in-hand" or "own-discarded-port-holding";

    private bool EmperorRemainingEOutOfPlayTargetAllows(string category, int playerId, CardInstance card) => card.OwnerId == playerId && category switch
    {
        // Doji's Guidance: "a non-Unique Strategy with a Political ability in your discard pile".
        "own-nonunique-political-strategy-in-discard" => Definition(card).Type == CardType.Strategy && !Keyword(card, "Unique") && card.Zone == Zone.FateDiscard && HasPoliticalAbility(card),
        // Shiba's Guidance: "a non-Unique Spell or Kiho in your discard pile" (Fate cards: a Spell, or a Strategy carrying Kiho).
        "own-nonunique-spell-or-kiho-in-discard" => !Keyword(card, "Unique") && card.Zone == Zone.FateDiscard && (Definition(card).Type == CardType.Spell || Definition(card).Type == CardType.Strategy && Keyword(card, "Kiho")),
        // Togashi's Guidance: "a Ring in your hand".
        "own-ring-in-hand" => Definition(card).Type == CardType.Ring && card.Zone == Zone.Hand && Player(playerId).Hand.Contains(card.Id),
        // Yoritomo's Guidance: "your discarded Port Holding".
        "own-discarded-port-holding" => Definition(card).Type == CardType.Holding && Keyword(card, "Port") && card.Zone == Zone.DynastyDiscard,
        _ => false
    };

    private void ValidateEmperorRemainingEOutOfPlayTarget(string category, PlayerState player, CardInstance target) =>
        Require(EmperorRemainingEOutOfPlayTargetAllows(category, player.Id, target), category switch
        {
            "own-nonunique-political-strategy-in-discard" => "Choose a non-Unique Strategy with a Political ability in your Fate discard pile.",
            "own-nonunique-spell-or-kiho-in-discard" => "Choose a non-Unique Spell or Kiho in your Fate discard pile.",
            "own-ring-in-hand" => "Choose a Ring in your hand.",
            _ => "Choose one of your discarded Port Holdings."
        });

    // Why the action is unavailable when its out-of-play category has no card at all (issue #1114: Doji's Guidance said only "No legal target or performer.").
    private static string? EmperorRemainingENoCandidateReason(string category) => category switch
    {
        "own-nonunique-political-strategy-in-discard" => "Your Fate discard pile has no non-Unique Strategy with a Political ability.",
        "own-nonunique-spell-or-kiho-in-discard" => "Your Fate discard pile has no non-Unique Spell or Kiho.",
        "own-ring-in-hand" => "You have no Ring in your hand.",
        "own-discarded-port-holding" => "Your Dynasty discard pile has no Port Holding.",
        _ => null
    };

    // QueryActions' candidate list for an out-of-play category; null for any other.
    private string?[]? EmperorRemainingECandidates(string category, int playerId) =>
        IsEmperorRemainingEOutOfPlayTarget(category)
            ? _state.Cards.Values.Where(c => EmperorRemainingEOutOfPlayTargetAllows(category, playerId, c)).Select(c => (string?)c.Id).ToArray()
            : null;

    // Consecration's province set (like Hidden Falls Dojo's, but the acting player's OWN provinces).
    private void ValidateOwnProvinceSet(GameCommand command, PlayerState player)
    {
        Require(command.TargetId is null && (command.CardIds is null || command.CardIds.Length == 0), "This action targets provinces, not cards.");
        var chosen = command.Provinces ?? (command.Province is int single ? [single] : []);
        Require(chosen.Length is 1 or 2 && chosen.Distinct().Count() == chosen.Length, "Choose one or two different provinces.");
        Require(chosen.All(i => player.Provinces.Any(p => p.Index == i && !p.Destroyed)), "Choose provinces of yours that have not been destroyed.");
    }

    private IEnumerable<ActionOption> OwnProvinceSetOptions(string id, AbilityDefinition ability, CardInstance? source, int playerId)
    {
        var candidates = Player(playerId).Provinces.Where(p => !p.Destroyed).Select(p => p.Index).Order().ToArray();
        var sets = candidates.Select(p => new[] { p }).Concat(candidates.SelectMany((a, i) => candidates.Skip(i + 1).Select(b => new[] { a, b }))).ToArray();
        if (sets.Length == 0) sets = [[]];
        foreach (var set in sets)
        {
            var command = new GameCommand("useAbility", playerId, _state.Version, source?.Id, AbilityId: id, Provinces: set.Length == 0 ? null : set, Province: set.Length == 0 ? null : set[0]);
            string? error = null;
            error = TryValidateAbility(command);
            var label = ability.Label + (set.Length == 0 ? "" : $" · province{(set.Length == 1 ? "" : "s")} {string.Join(" and ", set.Select(p => p + 1))}");
            yield return new("useAbility", label, error is null, error, error is null ? command : null, source?.Id, null, set.Length == 0 ? null : set);
        }
    }

    // ---- Validation ----------------------------------------------------------------------------------------------------------------------------------------
    private void ValidateEmperorRemainingE(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // "If you are a [Clan] Clan player": a condition on taking the action at all.
            case "akodo-guidance-melee": Require(player.Clan == "Lion", "You must be a Lion Clan player."); break;
            case "bayushi-guidance-return": Require(player.Clan == "Scorpion", "You must be a Scorpion Clan player."); break;
            case "daigotsu-guidance-destroy": Require(player.Clan == "Spider", "You must be a Spider Clan player."); break;
            case "dojis-guidance-return": Require(player.Clan == "Crane", "You must be a Crane Clan player."); break;
            case "hidas-guidance-bow": Require(player.Clan == "Crab", "You must be a Crab Clan player."); break;
            case "shibas-guidance-return": Require(player.Clan == "Phoenix", "You must be a Phoenix Clan player."); break;
            case "yoritomos-guidance-port": Require(player.Clan == "Mantis", "You must be a Mantis Clan player."); break;
            case "togashis-guidance-ring-play":
                Require(player.Clan == "Dragon", "You must be a Dragon Clan player.");
                // Brotherhood Schism: "Rings will not enter play if their owner has already put a Ring into play this turn."
                Require(_state.RingSchismTurn != _state.TurnNumber || player.RingPutIntoPlayTurn != _state.TurnNumber, "Brotherhood Schism: you already put a Ring into play this turn.");
                break;
            case "togashis-guidance-ring-search": Require(player.Clan == "Dragon", "You must be a Dragon Clan player."); break;
        }
    }

    // Read before the costs are paid (RunAbilityEffects): what a cost is about to remove.
    private void CaptureBeforeCostsForEmperorRemainingE(string id, GameCommand command)
    {
        if (id == "i-give-you-my-name-search" && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var human)) _humanGoldCostForSearch = CardGoldCost(human);
    }

    // ---- Attacks ---------------------------------------------------------------------------------------------------------------------------------------------
    // sourceId is the performing Personality (ActionPerformer): Cleansing the Path and Thunder's Favor read his keyword.
    private int? EmperorRemainingEAttackStrength(string abilityId, string? sourceId) => abilityId switch
    {
        "cleansing-path-attack" => 4 + (sourceId is not null && Keyword(Card(sourceId), "Fire") ? 1 : 0),
        "thunders-favor-attack" => 4 + (sourceId is not null && Keyword(Card(sourceId), "Thunder") ? 2 : 0),
        "akodo-guidance-melee" => 4,
        _ => null
    };

    private static string EmperorRemainingEAttackKind(string abilityId) => abilityId switch
    {
        "cleansing-path-attack" or "thunders-favor-attack" => "Ranged",
        "akodo-guidance-melee" => "Melee",
        _ => ""
    };

    // "move" for the actions that move their target (negatable by the movement-negating Reactions); a bow for the bowing ones.
    private static string? EmperorRemainingEMoveKind(string abilityId) => abilityId is "obscured-pathways-move" or "unnatural-flood-move" ? "move" : null;

    // ---- Execution -------------------------------------------------------------------------------------------------------------------------------------------
    // null: not one of this batch's abilities. false: its effects are done, the action finishes as usual. true: a window or a search opened and the action waits.
    private bool? RunEmperorRemainingE(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, IEnumerable<string>? bowedBeforeAction)
    {
        switch (id)
        {
            // Capturing the Soul: the Spell was removed from the game and the Shugenja bowed as costs; the wielder (the named performer) and each chosen Shugenja add their Chi.
            case "capturing-soul-remove":
                {
                    var wielder = Card(command.PerformerId!);
                    var performing = SlotCards(command, "others").Prepend(wielder).ToArray();
                    var victim = SlotCards(command, "target").Single();
                    var total = performing.Sum(PersonalityChi); var needed = 3 * PersonalityChi(victim);
                    if (total > needed && InPlay(victim)) RemoveUnitFromGame(victim, Definition(source!).Name);
                    else Emit("effectFailed", $"Capturing the Soul: the performing Shugenja's total Chi ({total}) is not higher than three times {Definition(victim).Name}'s Chi ({needed}).", player.Id, victim.Id);
                    return false;
                }
            case "cleansing-path-attack":
            case "thunders-favor-attack":
            case "akodo-guidance-melee":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            // Consecration.
            case "consecration-watch":
                {
                    var chosen = (command.Provinces ?? (command.Province is int single ? [single] : [])).Distinct().Order().ToArray();
                    _state.ConsecrationWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
                    _state.ConsecrationWatches.Add(new(player.Id, chosen, _state.TurnNumber, source!.Id));
                    Emit("watchSet", $"{Definition(source).Name}: after the first time this turn a Personality assigns or moves to an attacking army at province {string.Join(" or ", chosen.Select(p => p + 1))}'s battlefield, {player.Name} gains 2 Honor.", player.Id, source.Id);
                    return false;
                }
            case "contemplate-void-draw":
                for (var i = 0; i < 4; i++) Draw(player);
                return false;
            // Hanabi: an action performed by the Shugenja, so a dishonorable one is rehonored instead (core rules, Dishonor).
            case "hanabi-honor":
                GainHonorFromPerformer(Card(command.PerformerId!), 4, "Hanabi");
                return false;
            // I Give You My Name: the Human was destroyed as a cost; the search (its ceiling, and the Honor loss owed however it ends) opens and the action finishes when it does.
            case "i-give-you-my-name-search":
                {
                    BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                    var search = _state.PendingSearch!;
                    search.GoldLimit = _humanGoldCostForSearch + PersonalityChi(Card(command.PerformerId!));
                    search.HonorLossOnFinish = 2;
                    return true;
                }
            case "obscured-pathways-move":
                MoveHome(target!, false);
                return false;
            // The Kami's Blessing: the Shugenja's Chi, as it is now.
            case "kami-blessing-self":
                {
                    var wielder = WielderOf(source!)!;
                    ApplyModifier(wielder, source!.Id, id, "Force", PersonalityChi(wielder));
                    return false;
                }
            case "kami-blessing-samurai":
                {
                    var chi = PersonalityChi(WielderOf(source!)!);
                    foreach (var samurai in SlotCards(command, "samurai").ToArray()) ApplyModifier(samurai, source!.Id, id, "Force", chi);
                    return false;
                }
            // Unnatural Flood: "this movement will not be negated" while its Shugenja is Water (the mark holds for the movement alone; the bow that follows can still be negated).
            case "unnatural-flood-move":
                {
                    var previousKey = _state.UnnegatableActionKey;
                    if (WielderOf(source!) is { } floodShugenja && Keyword(floodShugenja, "Water")) _state.UnnegatableActionKey = ActionKey(command);
                    var zoneBefore = target!.Zone;
                    try { MoveHome(target, false); }
                    finally { _state.UnnegatableActionKey = previousKey; }
                    if (zoneBefore == Zone.Battlefield && target.Zone == Zone.Home)
                    {
                        if (!StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                        LockMovement(target, Definition(source!).Name, MovementKinds.Home | MovementKinds.ToBattlefield);
                    }
                    return false;
                }
            // ---- Celestials ----
            case "bayushi-guidance-return": BayushisGuidanceReturn(command, source!); return false;
            case "daigotsu-guidance-destroy":
                // "Destroy all bowed cards in it": a bowed Personality takes his whole unit with him; otherwise each bowed attachment. Then "bow it": what is left of the unit.
                if (target!.Bowed) DestroyTargetCard(target, player.Id);
                else foreach (var bowedAttachment in Attachments(target.Id).Where(a => a.Bowed).ToArray()) if (bowedAttachment.Zone == Zone.Attached) DestroyTargetCard(bowedAttachment, player.Id);
                if (InPlay(target)) BowUnitByEffect(target, player.Id);
                return false;
            case "dojis-guidance-return":
            case "shibas-guidance-return":
                ReturnToHand(target!);
                return false;
            case "hidas-guidance-bow":
                {
                    var attacking = IsAttacking(target!); // as he stands when the action resolves, before he is bowed
                    if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                    if (attacking) BowUnitByEffect(target!, player.Id, except: target!.Id); // his unit: the rest of its cards bow (the Personality already did or was prevented)
                    return false;
                }
            case "shinjos-guidance-search":
            case "togashis-guidance-ring-search":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            // Togashi's Guidance: a Ring in the hand is put into play (by this card's text, not its own: it does not count for Enlightenment).
            case "togashis-guidance-ring-play":
                player.Hand.Remove(target!.Id);
                target.Zone = Zone.Home; target.ProvinceIndex = null; target.FaceUp = true;
                RingEntersPlay(player, target, false, source, id); // issue #699 (WlRings.cs)
                Emit("cardEnteredPlay", $"{player.Name} put {Definition(target).Name} into play (Togashi's Guidance); it does not count towards an Enlightenment Victory.", player.Id, target.Id);
                return false;
            // Yoritomo's Guidance: the discarded Port Holding enters play bowed (its Gold, 2 less, was paid as the action's cost).
            case "yoritomos-guidance-port":
                {
                    ResetCardState(target!);
                    target!.Zone = Zone.Home; target.ProvinceIndex = null; target.FaceUp = true; target.Bowed = true;
                    Emit("cardEnteredPlay", $"{player.Name} put {Definition(target).Name} into play bowed (Yoritomo's Guidance).", player.Id, target.Id);
                    return OpenEntryReactionWindow(player.Id, target, 1 - player.Id) || OpenOverconfidentDrawWindow(1 - player.Id);
                }
            default:
                return null;
        }
    }

    // A unit leaves the game: his attachments, then he (core rules: when a Personality leaves play, his whole unit leaves play the same way). Not a destruction.
    private void RemoveUnitFromGame(CardInstance personality, string cause)
    {
        foreach (var attachment in Attachments(personality.Id).ToArray()) RemoveFromGame(attachment);
        RemoveFromGame(personality);
        Emit("unitRemovedFromGame", $"{Definition(personality).Name}'s unit was removed from the game ({cause}).", personality.OwnerId, personality.Id);
    }

    // Bayushi's Guidance: each chosen face-up Personality leaves its province for the bottom of its owner's deck, in the order they were named; then every emptied province is refilled face-up.
    private void BayushisGuidanceReturn(GameCommand command, CardInstance source)
    {
        var emptied = new List<(PlayerState Owner, ProvinceState Province)>();
        foreach (var card in SlotCards(command, "personalities").ToArray())
        {
            if (card.Zone != Zone.Province) continue;
            var owner = Player(card.OwnerId);
            var province = owner.Provinces.Single(p => p.CardId == card.Id);
            province.CardId = null; card.ProvinceIndex = null;
            Emit("provinceCardRemoved", $"{Definition(card).Name} left {owner.Name}'s province {province.Index + 1} ({Definition(source).Name}).", owner.Id, card.Id);
            ToOwnerDeck(card, "Bottom");
            emptied.Add((owner, province));
        }
        foreach (var (owner, province) in emptied) RefillProvinceFaceUp(owner, province);
    }

    // The top of the owner's Dynasty deck fills the province face-up (a destroyed province, or an empty deck, refills nothing).
    private void RefillProvinceFaceUp(PlayerState owner, ProvinceState province)
    {
        if (province.Destroyed || province.CardId is not null || owner.DynastyDeck.Count == 0 || HoldsNoDynasty(province)) return; // issue #503: Illusory Defense's province
        var refillId = owner.DynastyDeck[0]; owner.DynastyDeck.RemoveAt(0);
        var refill = Card(refillId);
        province.CardId = refillId; refill.Zone = Zone.Province; refill.ProvinceIndex = province.Index; refill.FaceUp = true;
        Emit("provinceRefilled", $"{owner.Name} refilled province {province.Index + 1} face up with {Definition(refill).Name}.", owner.Id, refillId);
        AfterProvinceRefilledP6(owner, province, faceUp: true); // issue #503: Shosuro Ietsuno
    }

    // ---- Search helpers (DeckSearch.cs' hooks) ---------------------------------------------------------------------------------------------------------------
    // The place an ordered search goes on to after the current one; null when the search is not ordered or this is its last place.
    private static SearchDeckOption? NextOrderedSearchPlace(SearchSpec spec, SearchDeckOption current)
    {
        if (!spec.Ordered) return null;
        var index = Array.IndexOf(spec.Decks, current);
        return index >= 0 && index + 1 < spec.Decks.Length ? spec.Decks[index + 1] : null;
    }

    // I Give You My Name: "Put the Oni into play, ignoring Gold cost." A fresh entry at home (the card may have been dead); the Honor Requirement and the price are both ignored, as for every recruit an effect makes.
    private void EnterSearchedPersonality(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        player.DynastyDeck.Remove(card.Id);
        if (card.OwnerId != player.Id) TakeControl(card, player.Id); // P20: a card found in the OTHER player's discard pile is controlled by the searcher while it is in play
        ResetCardState(card);
        EnterPlayFromRecruit(player, card, null, Definition(Card(search.SourceCardId)).Name);
        search.EnteredCardId = card.Id;
    }

    // Shinjo's Guidance: the found card goes into a province of the searcher's choice.
    private void BeginSearchProvincePlacement(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        var provinces = player.Provinces.Where(p => !p.Destroyed && !HoldsNoDynasty(p)).ToArray(); // issue #503
        if (provinces.Length == 0)
        {
            Emit("searchFailed", $"{player.Name} has no province to put {Definition(card).Name} into.", player.Id);
            FinishSearch(search);
            return;
        }
        search.Stage = SearchStage.ChooseProvince;
        _state.PendingChoice = new("searchRegionProvince", search.PlayerId, search.SourceCardId, $"{Definition(card).Name}: choose one of your provinces to put her into, face-up, discarding the card that is there.");
    }

    private void ChooseSearchProvincePlacement(PendingSearch search, GameCommand command)
    {
        Require(command.Type == "chooseSearchProvince" && command.Province is not null, "Choose a province to put the card into.");
        var player = Player(search.PlayerId); var card = Card(search.SelectedCardId);
        var province = player.Provinces.FirstOrDefault(p => p.Index == command.Province);
        Require(province is not null && !province.Destroyed && !HoldsNoDynasty(province), "Choose one of your own provinces that has not been destroyed (and can hold Dynasty cards)."); // issue #503
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        player.DynastyDeck.Remove(card.Id);
        if (province!.CardId is { } oldId && _state.Cards.TryGetValue(oldId, out var old))
        {
            old.Zone = Zone.DynastyDiscard; old.ProvinceIndex = null; old.FaceUp = true;
            Emit("provinceDiscard", $"{Definition(old).Name} was discarded from {player.Name}'s province {province.Index + 1} (Shinjo's Guidance).", player.Id, old.Id);
        }
        ResetCardState(card);
        card.Zone = Zone.Province; card.ProvinceIndex = province.Index; card.FaceUp = true; card.Bowed = false;
        province.CardId = card.Id;
        Emit("provinceFilled", $"{Definition(card).Name} was put into {player.Name}'s province {province.Index + 1} face up.", player.Id, card.Id);
        FinishSearch(search);
    }

    private IEnumerable<ActionOption> SearchProvincePlacementActions(PendingSearch search, int playerId)
    {
        var card = Card(search.SelectedCardId);
        foreach (var province in Player(playerId).Provinces.Where(p => !p.Destroyed && !HoldsNoDynasty(p))) // issue #503
            yield return new("chooseSearchProvince", $"Put {Definition(card).Name} into province {province.Index + 1}, face-up, discarding the card there", true, null,
                new GameCommand("chooseSearchProvince", playerId, _state.Version, Province: province.Index), card.Id, Provinces: [province.Index]);
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingEHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingE", 800, static abilities => abilities.AddEmperorRemainingE());
        EngineHooks.Searches.Register("EmperorRemainingESearches", 100, static () => EmperorRemainingESearches.All);
        EngineHooks.AttackStrengthBefore.Register("EmperorRemainingEAttackStrength", 200, static (engine, context) => engine.EmperorRemainingEAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("EmperorRemainingEAttackKind", 600, static context => NullIfEmpty(EmperorRemainingEAttackKind(context.abilityId)));
        EngineHooks.TargetAllows.Register("EmperorRemainingETargetAllows", 400, static (engine, context) => engine.EmperorRemainingETargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PersonalityForce.Register("EmperorRemainingEForce", 200, static (engine, context) => engine.EmperorRemainingEForce(context.card));
        EngineHooks.ValidateEarly.Register("ValidateEmperorRemainingE", 900, static (engine, context) => engine.ValidateEmperorRemainingE(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorRemainingE", 300, static (engine, context) => engine.RunEmperorRemainingE(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.bowedBeforeAction));
    }
}
