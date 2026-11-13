namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, PASS GROUP P5: zones, rulebook-action changes, created/copied cards, granted abilities, provenance records, hidden information. Every card was read letter by letter against its scan
// (the scan is authoritative where the catalogue's Oracle text differs; each difference is noted at the card).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md; none conflicts with it):
//   * "WAS NOT IN PLAY WHEN THIS COMBAT SEGMENT BEGAN" (Headbutt, a PROVENANCE record): as each battle's Combat Segment begins (the Engage window closes, or engagement opens none) the engine notes
//     which cards are in play and the event number it began at (EmperorP5State.SegmentInPlay / SegmentStartEvent). A card was not in play then when it is not in that note, or when it has entered
//     play since (a "cardEnteredPlay", "cardCreated", "reserveRecruited" or "equipped" event for it after the segment began: a card that left and came back is a new entry).
//   * "YOU HAVE TAKEN A [KEYWORD] ACTION THIS PHASE" (Daigotsu Yutaka's Dark Virtue record): counted where an action's effects resolve (RunAbilityEffects), so a wholly negated action does not count
//     (Mendon's ruling), per player and per printed phase instance (PrintedPhases.Instance).
//   * A PERFORMER-SIDE ACTION BAN BY KEYWORD, and one BY ARMY FORCE (Yutaka: "may not perform Bushido Virtue actions"; Raido no Oni: "may not perform Battle actions while your current army has
//     higher total Force than the current enemy army"): read in ValidateAbility for the card that performs the action by the printed rules (ActionPerformerPerRules), armies totalled as the
//     battle would resolve them (ArmyForces).
//   * AN ITEM USED FROM A HAND (Tsubute: "destroy this card or discard it from your hand"): its second way to pay is a second catalog entry sharing the printed action's use, whose source is the
//     Item in its player's hand (P5SourceZoneAllows, AbilitySourceZoneAllows' hook) and whose cost discards it.
//   * CONDITIONAL PLAYER ABILITIES GRANTED BY A CARD IN PLAY OR AN EVENT (The Bite of Winter, Economic Repercussions: "all players have the ability ..."; Starting on the Path, Never Enough Soldiers:
//     "until your Nth turn from now begins, you have the ability ..."): player abilities listed only while the granting card is in play (Assault in the Jungle's PlayerAbilityDormant shape), the
//     Events staying in Zone.EventInPlay with ExpiresAtTurnsTaken.
//   * EQUIP FROM A DISCARD PILE (Seek the Stain, Never Enough Soldiers: rulebook-action changes): the Equip rulebook action's own shapes (Limited: a Personality at home; Battle: a Spell to an
//     unbowed opposed Shugenja) with the attachment taken from the discard pile, paying all costs, any number of times per turn.
//   * STATIC -1F FOR EVERY PERSONALITY AND FOLLOWER (The Bite of Winter) and -2 GOLD COST FOR OTHER PLAYERS' PERSONALITIES AND FOLLOWERS IN PLAY (Economic Repercussions): P5Force / P5GoldCost hooks.
// ======================================================================================================================================================================================
public static class EmperorP5Abilities
{
    public const string NaraProvinces = "p5-nara-provinces";
    public const string YamaderaRefill = "p5-yamadera-refill", YamaderaBow = "p5-yamadera-bow";
    public const string HeadbuttBow = "p5-headbutt-destroy", HeadbuttFresh = "p5-headbutt-destroy-fresh";
    public const string TsubuteDestroy = "p5-tsubute-attack", TsubuteHand = "p5-tsubute-attack-hand";
    public const string JoutaAttack = "p5-jouta-attack", JoutaDeadAttack = "p5-jouta-attack-dead";
    public const string YutakaReturn = "p5-yutaka-return", YutakaReturnDark = "p5-yutaka-return-dark";
    public const string TokikoHome = "p5-tokiko-home", TokikoBattlefield = "p5-tokiko-battlefield";
    public const string DisruptingBow = "p5-disrupting-communication";
    public const string BiteEnter = "p5-bite-winter-enter", BiteDiscard = "p5-bite-winter-discard";
    public const string EconEnter = "p5-econ-enter", EconDiscardFavor = "p5-econ-discard-favor", EconDiscardPay = "p5-econ-discard-pay";
    public const string StartingPathRing = "p5-starting-path-ring";
    public const string NesEquip = "p5-nes-equip", NesEquipOpen = "p5-nes-equip-open", NesEquipBattle = "p5-nes-equip-battle";
    public const string SeekStainLimited = "p5-seek-stain-equip-limited", SeekStainBattle = "p5-seek-stain-equip-battle";
    public const string SeekStainKolat = "p5-seek-stain-kolat", SeekStainNinja = "p5-seek-stain-ninja", SeekStainShadowlands = "p5-seek-stain-shadowlands";
    public const string GuestsDiscard = "p5-perplexing-guests-discard", GuestsCreate = "p5-perplexing-guests-create";
    public const string MistrustNegate = "p5-inciting-mistrust-negate", MistrustDiscard = "p5-inciting-mistrust-discard";

    // Target and performer categories.
    public const string EnemyUnitCardLowerForceThanSelf = "p5-enemy-unit-card-lower-force-than-self";
    public const string EnemyCardLtePerformerNoAttachments = "p5-enemy-card-lte-performer-without-attachments";
    public const string EnemyUnitCard = "p5-enemy-unit-card";
    public const string BiteOfWinterTitled = "p5-card-titled-bite-of-winter";
    public const string EconTitled = "p5-card-titled-economic-repercussions";
    public const string OwnPersonalityWithKeywordAnywhere = "p5-own-personality-with-keyword-anywhere";
    public const string OwnPersonalityAtHome = "p5-own-personality-at-home";
    public const string OwnPersonalityWithKeywordsAtHome = "p5-own-personality-with-keywords-at-home";
    public const string OwnUnbowedOpposedWithKeywordsAtBattle = "p5-own-unbowed-opposed-with-keywords-at-battle";

    // Effects (CardDefinition.Effect).
    public const string RaidoEffect = "p5-raido-no-oni";
    public const string YutakaEffect = "p5-yutaka-no-bushido";
    public const string TokikoEffect = "p5-tokiko-phoenix-force";
    public const string BiteOfWinterEffect = "p5-bite-of-winter";
    public const string EconEffect = "p5-economic-repercussions";
    public const string StartingPathEffect = "p5-starting-on-the-path";
    public const string NesEffect = "p5-never-enough-soldiers";
    public const string PlainOfGlassEffect = "p5-plain-of-glass";

    public const string BiteOfWinterTitle = "The Bite of Winter";
    public const string EconTitle = "Economic Repercussions";

    public static Dictionary<string, AbilityDefinition> AddEmperorP5(this Dictionary<string, AbilityDefinition> all)
    {
        // Kakita Nara (oracle-l5r-4206, the scan): "You may use your Stronghold's abilities even if it is bowed." is Colonial Harbor's trait (her Effect, TirelessStronghold). "Political Open: Discard
        // the Imperial Favor: Give each of your provinces +2 strength." (The catalogue prints "Favor Political Open"; the scan has no Favor designator.) Until the end of the turn (ruling R3).
        all[NaraProvinces] = new("Open", "Political Open: Discard the Imperial Favor: Give each of your provinces +2 strength", "none", ActionKeywords: ["Political"], Costs: [new(CostVerbs.DiscardFavor)]);
        // Hida Yamadera (oracle-l5r-3242): "Reaction: After Yamadera enters play from a province: Refill it face-up." (Agasha Ryo's shape) and "Battle: Target an enemy card with lower Force: Bow it."
        all[YamaderaRefill] = new("Reaction", "After Yamadera enters play from a province: Refill it face-up", "none", Trigger: "own-enters-play");
        all[YamaderaBow] = new("Battle", "Target an enemy card with lower Force: Bow it", EnemyUnitCardLowerForceThanSelf);
        // Headbutt (oracle-l5r-3010): "Battle: Choose your performing Berserker, bow him unless he was not in play when this Combat Segment began, and target an enemy card without attachments and with
        // equal or lower Force: Destroy it." The two ways the cost stands (bowed, or not owed) are two entries sharing one use.
        all[HeadbuttBow] = new("Battle", "Choose your performing Berserker, bow him (he was in play when this Combat Segment began), and target an enemy card without attachments and with equal or lower Force: Destroy it",
            EnemyCardLtePerformerNoAttachments, Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Berserker"], UsageKey: "p5-headbutt", Costs: ActionCost.BowPerformer);
        all[HeadbuttFresh] = new("Battle", "Choose your performing Berserker who was not in play when this Combat Segment began and target an enemy card without attachments and with equal or lower Force: Destroy it",
            EnemyCardLtePerformerNoAttachments, Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Berserker"], UsageKey: "p5-headbutt");
        // Tsubute (oracle-l5r-8772, an Item; "Will only attach to a Ninja" is its AttachRequiresKeyword): "Battle: Choose your performing Ninja, and destroy this card or discard it from your hand:
        // Ranged 3 Attack." The two ways to pay share the printed action's use.
        all[TsubuteDestroy] = new("Battle", "Choose your performing Ninja and destroy this card: Ranged 3 Attack", "attack", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ninja"],
            UsageKey: "p5-tsubute", Costs: [new(CostVerbs.Sacrifice)]);
        all[TsubuteHand] = new("Battle", "Choose your performing Ninja and discard this card from your hand: Ranged 3 Attack", "attack", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ninja"],
            UsageKey: "p5-tsubute", Costs: [new(CostVerbs.Discard, CostSubject.Source)]);
        // Shiba Jouta - exp (oracle-l5r-6766): "Battle: Even if Jouta is dead, choose your performing Shugenja, who may be at any location unless Jouta is dead: Ranged 5 Attack." Alive, Jouta is at the
        // battle (the Rules of Location for his own action) and the Shugenja anywhere; dead, the action is taken from the dead pile and the Shugenja must be at the battle. One use.
        all[JoutaAttack] = new("Battle", "Choose your performing Shugenja, who may be at any location: Ranged 5 Attack", "attack", Performer: OwnPersonalityWithKeywordAnywhere, RequiredKeywords: ["Shugenja"],
            UsageKey: "p5-jouta-attack");
        all[JoutaDeadAttack] = new("Battle", "Even if Jouta is dead, choose your performing Shugenja: Ranged 5 Attack", "attack", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Shugenja"],
            UsageKey: "p5-jouta-attack", FromDead: true, SourceUnitMayBeAway: true);
        // Daigotsu Yutaka (oracle-l5r-10086): "After Yutaka enters play: Lose 1 Honor." (EntryHonorLoss) "Yutaka may not perform Bushido Virtue actions." (his Effect) "Battle: If Yutaka is dead and he
        // would be opposed: Bring him into play at the current battlefield, paying 3 less Gold if you have taken a Dark Virtue action this phase." One use, two prices.
        all[YutakaReturn] = new("Battle", "If Yutaka is dead and he would be opposed: Bring him into play at the current battlefield, paying all costs", "source-if-dead",
            FromDead: true, UsageKey: "p5-yutaka-return", Cost: GoldCostRule.TargetRecruit(), Absent: true);
        all[YutakaReturnDark] = new("Battle", "If Yutaka is dead and he would be opposed: Bring him into play at the current battlefield, paying 3 less Gold (you have taken a Dark Virtue action this phase)", "source-if-dead",
            FromDead: true, UsageKey: "p5-yutaka-return", Cost: GoldCostRule.TargetRecruit(-3, 0), Absent: true);
        // Tokiko (oracle-l5r-8613): "Tokiko has +1F while a Phoenix Clan player controls her." (her Effect) "Battle/Open: Destroy Tokiko and target your dead Personality: Bring him into play in your home or
        // at the current battlefield, paying all costs but paying 7 less Gold." The destination is the player's choice: two entries sharing one use (the battlefield only in a battle).
        all[TokikoHome] = new("BattleOrOpen", "Destroy Tokiko and target your dead Personality: Bring him into play in your home, paying all costs but paying 7 less Gold", "own-dead-personality",
            UsageKey: "p5-tokiko", Cost: GoldCostRule.TargetRecruit(-7, 0), Costs: [new(CostVerbs.Sacrifice)]);
        all[TokikoBattlefield] = new("Battle", "Destroy Tokiko and target your dead Personality: Bring him into play at the current battlefield, paying all costs but paying 7 less Gold", "own-dead-personality",
            UsageKey: "p5-tokiko", Cost: GoldCostRule.TargetRecruit(-7, 0), Costs: [new(CostVerbs.Sacrifice)], Absent: true);
        // Disrupting Communication (oracle-l5r-2035): "Battle: Bow your performing Scout and target an enemy card: Bow it. The enemy leader discards a card of his choice, or a random card if you have
        // Reconnaissance." An enemy card is a card in an enemy unit (core rules, Enemy): a Terrain is not one.
        all[DisruptingBow] = new("Battle", "Bow your performing Scout and target an enemy card: Bow it. The enemy leader discards a card of his choice, or a random card if you have Reconnaissance", EnemyUnitCard,
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Scout"], Costs: ActionCost.BowPerformer);
        // The Bite of Winter (oracle-l5r-7972): "Personalities and Followers have -1F. All players have the ability, 'Open: Bow your Stronghold: Discard a card titled The Bite of Winter from play.'
        // Limited: Put this card into play."
        all[BiteEnter] = new("Limited", "Put this card into play", "none");
        all[BiteDiscard] = new("Open", "The Bite of Winter: Bow your Stronghold: Discard a card titled The Bite of Winter from play", BiteOfWinterTitled, PlayerAbility: true, Costs: ActionCost.BowStronghold);
        // Economic Repercussions (oracle-l5r-2280, the scan): "Other players' Personalities and Followers in play have -2 Gold Cost and all players have the ability, 'Political Limited: Discard the
        // Imperial Favor or pay 6 Gold: Discard a card titled Economic Repercussions from play.' Limited: Put this card into play." (The catalogue prints "Favor Political Limited ... to discard"; the
        // scan has no Favor designator and puts both ways to pay before the colon.) The two ways to pay share the granted ability's use.
        all[EconEnter] = new("Limited", "Put this card into play", "none");
        all[EconDiscardFavor] = new("Limited", "Economic Repercussions: Political Limited: Discard the Imperial Favor: Discard a card titled Economic Repercussions from play", EconTitled,
            PlayerAbility: true, UsageKey: "p5-econ-discard", ActionKeywords: ["Political"], Costs: [new(CostVerbs.DiscardFavor)]);
        all[EconDiscardPay] = new("Limited", "Economic Repercussions: Political Limited: Pay 6 Gold: Discard a card titled Economic Repercussions from play", EconTitled,
            PlayerAbility: true, UsageKey: "p5-econ-discard", ActionKeywords: ["Political"], Cost: GoldCostRule.Fixed(6));
        // Starting on the Path (oracle-l5r-10032, an Event; the scan): "Until your second turn from now begins, you have the ability, 'Limited: Target a Ring in your hand: Put it into play.'" A Ring
        // put into play by this text did not enter by its own printed text, so it does not count towards an Enlightenment Victory (core rules, Winning the Game) -- the catalogue's later wording says so.
        all[StartingPathRing] = new("Limited", "Starting on the Path: Target a Ring in your hand: Put it into play", "own-ring-in-hand",
            PlayerAbility: true);
        // Never Enough Soldiers (oracle-l5r-5534, an Event; the scan): "Until your Events Phase three turns from now begins, you may target Followers in your discard pile with the Equip rulebook
        // action." (The catalogue reads "until your turn three turns from now begins".) The
        // rulebook Equip (Limited): "Any number of times per turn, choose your performing Personality and target an attachment card: Attach it to him, paying all costs" -- the Personality at home.
        all[NesEquip] = new("Limited", "Never Enough Soldiers: Equip a Follower from your discard pile to your performing Personality, paying all costs", "attachment-in-discard", PlayerAbility: true,
            Performer: OwnPersonalityAtHome, Cost: GoldCostRule.AttachCost(), UsesPerTurn: AbilityUseLimit.Unlimited, Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, Types: [CardType.Follower]));
        // The same Equip where a card changes when equipping a Follower may be taken (TimingGrants.cs: The Plain of Glass's Battle, Glory of the Shogun's Battle/Open): the Open equip in
        // another player's action segment (a Personality at home) and the Battle equip (a Personality at the current battlefield) -- each only while such a grant stands.
        all[NesEquipOpen] = new("Open", "Never Enough Soldiers: Equip a Follower from your discard pile to your performing Personality, paying all costs (Open)", "attachment-in-discard", PlayerAbility: true,
            Performer: OwnPersonalityAtHome, Cost: GoldCostRule.AttachCost(), UsesPerTurn: AbilityUseLimit.Unlimited, Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, Types: [CardType.Follower]));
        all[NesEquipBattle] = new("Battle", "Never Enough Soldiers: Equip a Follower from your discard pile to your performing Personality at the current battlefield, paying all costs (Battle)", "attachment-in-discard", PlayerAbility: true,
            Performer: "own-personality-explicitly-at-battle", Cost: GoldCostRule.AttachCost(), UsesPerTurn: AbilityUseLimit.Unlimited, Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, Types: [CardType.Follower]));
        // Seek the Stain (oracle-l5r-6559, a Spell): "You may attach this card from your discard pile to your Void Shugenja." -- the Equip rulebook actions with this card in the discard pile: Equip
        // (Limited) to a Void Shugenja at home, Equip (Battle) to an unbowed opposed Void Shugenja at the battle; paying all costs, any number of times per turn.
        all[SeekStainLimited] = new("Limited", "Equip this card from your discard pile to your Void Shugenja, paying all costs", "none", FromDiscard: true, Performer: OwnPersonalityWithKeywordsAtHome,
            RequiredKeywords: ["Shugenja", "Void"], Cost: GoldCostRule.AttachCost(), UsesPerTurn: AbilityUseLimit.Unlimited, Attach: new(AttachSource.Self, AttachPile.Own, AttachWielder.Performer));
        all[SeekStainBattle] = new("Battle", "Equip this card from your discard pile to your unbowed opposed Void Shugenja, paying all costs", "none", FromDiscard: true, Performer: OwnUnbowedOpposedWithKeywordsAtBattle,
            RequiredKeywords: ["Shugenja", "Void"], Cost: GoldCostRule.AttachCost(), UsesPerTurn: AbilityUseLimit.Unlimited, Attach: new(AttachSource.Self, AttachPile.Own, AttachWielder.Performer));
        // "Open: Bow this card and target another player's Personality: You find out his dark secret. Give him Kolat, Ninja, or Shadowlands." (Asako Tsunefusa's three choices of one action; the
        // "dark secret" is flavour; the keyword lasts until the end of the turn.)
        all[SeekStainKolat] = new("Open", "Bow this card and target another player's Personality: Give him Kolat", "enemy-personality", UsageKey: "p5-seek-stain-secret", Costs: ActionCost.BowSource);
        all[SeekStainNinja] = new("Open", "Bow this card and target another player's Personality: Give him Ninja", "enemy-personality", UsageKey: "p5-seek-stain-secret", Costs: ActionCost.BowSource);
        all[SeekStainShadowlands] = new("Open", "Bow this card and target another player's Personality: Give him Shadowlands", "enemy-personality", UsageKey: "p5-seek-stain-secret", Costs: ActionCost.BowSource);
        // Perplexing Guests (oracle-l5r-5928, the scan): "Reaction: After another player's action targets your Personality: Discard him." and "Battle: Create a 1F/1C/1PH Monk Personality on your side
        // at the current battlefield. Remove him from the game when the turn ends." (The catalogue's erratum adds "If there are any enemy units at the current battlefield"; the scan has no condition.)
        // The Proxy is Guest, oracle-l5r-9998. It brings a Personality into play at the battle, so it needs no presence there (core rules, the Exception to Presence).
        all[GuestsDiscard] = new("Reaction", "After another player's action targets your Personality: Discard him", "none", Trigger: ReactionTriggers.TargetedByAction);
        all[GuestsCreate] = new("Battle", "Create a 1F/1C/1PH Monk Personality on your side at the current battlefield. Remove him from the game when the turn ends", "none",
            Creates: new(CardType.Personality, Force: 1, Chi: 1, PersonalHonor: 1, Keywords: ["Monk"], ProxyId: "oracle-l5r-9998", AtBattlefield: true), Absent: true);
        // Inciting Mistrust (oracle-l5r-3733): "Reaction: After another player's Melee or Ranged Attack targets your Ninja: Show the card in the current battlefield's province. If it is a Personality,
        // negate the Melee or Ranged Attack's effects." and "Battle: Discard a card in the current battlefield's province. Draw a card."
        all[MistrustNegate] = new("Reaction", "After another player's Melee or Ranged Attack targets your Ninja: Show the card in the current battlefield's province. If it is a Personality, negate the Attack's effects", "none",
            Trigger: ReactionTriggers.TargetedByAction);
        all[MistrustDiscard] = new("Battle", "Discard a card in the current battlefield's province. Draw a card", "none");
        return all.AddEmperorP5Zones(); // EmperorP5Zones.cs
    }
}

/// <summary>What pass group P5 (issue #503) keeps between commands.</summary>
public sealed class EmperorP5State
{
    /// <summary>The battle whose Combat Segment the note below is for, the event number it began at and the cards in play then (Headbutt).</summary>
    public int SegmentBattle { get; set; } = -1;
    public int SegmentStartEvent { get; set; }
    public List<string> SegmentInPlay { get; set; } = [];
    /// <summary>The printed phase instance in which each player last took a Dark Virtue action (Daigotsu Yutaka); -1 for none.</summary>
    public int[] DarkVirtuePhase { get; set; } = [-1, -1];
    /// <summary>Unsettling Doubts: cards whose abilities were permanently removed (while they stay in play).</summary>
    public List<string> AbilitiesRemoved { get; set; } = [];
    /// <summary>Blessings of the Shi-Tien Yen-Wang: the turn its start-of-turn trait was last asked.</summary>
    public int BlessingsAskedTurn { get; set; } = -1;
    /// <summary>Step Through the Void: units removed from the game, put back before the turn ends.</summary>
    public List<SteppedUnit> SteppedUnits { get; set; } = [];
    /// <summary>The Price of Honor: Personalities the rulebook seppuku action does not affect this turn.</summary>
    public List<SeppukuBar> SeppukuBars { get; set; } = [];
    /// <summary>Scouting the Pass, The Price of Honor: cards of another player's hand a player looked at (private to him).</summary>
    public List<HandSight> HandSights { get; set; } = [];
    /// <summary>Searching for Answers: Personalities discarded after their battle ends; and the one its look just put into play (his entry windows open when the look closes).</summary>
    public List<DiscardAfterBattle> DiscardAfterBattle { get; set; } = [];
    public string? LookEntryId { get; set; }
    /// <summary>Stolen Merchandise: Holdings with a copied Gold-producing trait for the turn.</summary>
    public List<StolenTrait> StolenTraits { get; set; } = [];
    /// <summary>Learning: other players' Strategies a player may play with an additional action.</summary>
    public List<string> Borrowed { get; set; } = [];
    /// <summary>Scouting the Pass: the battle whose first-action performer limit it set.</summary>
    public int ScoutingBattle { get; set; } = -1;
    /// <summary>The Plain of Glass: the first Conqueror chosen in a battle whose second Conquest is still being chosen.</summary>
    public string? FirstConquerorId { get; set; }
}

public sealed partial class MatchEngine
{
    private EmperorP5State P5 => _state.P5;

    private static readonly string[] P5EntryEventTypes = ["cardEnteredPlay", "cardCreated", "reserveRecruited", "equipped", "personalityOverlayed"];

    // ---- records -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The Combat Segment of the current battle begins (CloseEngageWindow, or EngageBattle when no Engage window opens).
    private void MarkCombatSegmentBegan()
    {
        P5.SegmentBattle = _state.BattleNumber;
        P5.SegmentStartEvent = _state.Events.Count;
        P5.SegmentInPlay = _state.Cards.Values.Where(InPlay).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
    }

    // "He was not in play when this Combat Segment began": not in play at its start, or entered play since.
    internal bool NotInPlayWhenCombatSegmentBegan(CardInstance card) =>
        P5.SegmentBattle == _state.BattleNumber && (!P5.SegmentInPlay.Contains(card.Id)
            || _state.Events.Skip(P5.SegmentStartEvent).Any(e => e.CardId == card.Id && P5EntryEventTypes.Contains(e.Type)));

    // RunAbilityEffects: the action's effects are resolving (a wholly negated action never gets here).
    private void RecordP5ActionTaken(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (!IsAction(ability) || TraitReactions.Is(command.AbilityId ?? "")) return;
        if (ActionCarriesKeyword(ability, source, "Dark Virtue")) P5.DarkVirtuePhase[command.PlayerId] = PhaseInstance();
    }

    internal bool TookDarkVirtueActionThisPhase(int playerId) => P5.DarkVirtuePhase[playerId] == PhaseInstance();

    // ---- static effects --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private int BitesOfWinterInPlay() => _state.Cards.Values.Count(c => InPlay(c) && Definition(c).Effect == EmperorP5Abilities.BiteOfWinterEffect);

    // PersonalityForce's and FollowerForce's hook: The Bite of Winter's -1F (each copy in play), Tokiko's +1F while a Phoenix Clan player controls her.
    private int P5Force(CardInstance card)
    {
        var type = Definition(card).Type;
        if (type is not (CardType.Personality or CardType.Follower)) return 0;
        var force = -BitesOfWinterInPlay();
        if (type == CardType.Personality && Definition(card).Effect == EmperorP5Abilities.TokikoEffect && Player(card.OwnerId).Clan == "Phoenix") force += 1;
        return force;
    }

    // CardGoldCost's hook: Economic Repercussions' "Other players' Personalities and Followers in play have -2 Gold Cost" (each copy in play, for the players other than its controller).
    private int P5GoldCost(CardInstance card)
    {
        if (!InPlay(card) || Definition(card).Type is not (CardType.Personality or CardType.Follower)) return 0;
        return -2 * _state.Cards.Values.Count(c => InPlay(c) && Definition(c).Effect == EmperorP5Abilities.EconEffect && c.OwnerId != card.OwnerId);
    }

    // ---- The Plain of Glass: the second Conquest ------------------------------------------------------------------------------------------------------------------------------------------

    private bool PlainOfGlassConquestTwice(int playerId) => Definition(Card(Player(playerId).StrongholdId)).Effect == EmperorP5Abilities.PlainOfGlassEffect;

    // ResolveConqueror's hook: after the first of two Conquests is chosen, the second is chosen among the others (with one left, it is that one). True when the choice was reopened or the
    // resolution already finished here.
    private bool ReopenConquerorChoiceForPlainOfGlass(PendingConqueror pending, string chosenId)
    {
        if (!PlainOfGlassConquestTwice(pending.PlayerId) || P5.FirstConquerorId is not null) return false;
        var others = pending.CandidateIds.Where(id => id != chosenId).ToArray();
        if (others.Length == 0) return false;
        if (others.Length == 1)
        {
            FinishBattleResolution(pending.PlayerId, pending.ProvinceIndex, Army(pending.PlayerId, pending.ProvinceIndex).ToArray(), chosenId, others[0]);
            return true;
        }
        P5.FirstConquerorId = chosenId;
        _state.PendingConqueror = new PendingConqueror { PlayerId = pending.PlayerId, ProvinceIndex = pending.ProvinceIndex, CandidateIds = others };
        _state.PendingChoice = new("conqueror", pending.PlayerId, null, "The Plain of Glass: choose a second Conqueror personality to straighten.");
        _state.PriorityPlayerId = pending.PlayerId;
        Emit("conquerorChoice", "The Plain of Glass: choose a second Conqueror personality to straighten.", pending.PlayerId);
        return true;
    }

    // ---- player abilities that exist only while granted -------------------------------------------------------------------------------------------------------------------------------------

    private bool ActiveP5Event(int playerId, string effect) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.EventInPlay && Definition(c).Effect == effect);

    private bool P5PlayerAbilityDormant(string abilityId, int playerId) => abilityId switch
    {
        EmperorP5Abilities.BiteDiscard => BitesOfWinterInPlay() == 0,
        EmperorP5Abilities.EconDiscardFavor or EmperorP5Abilities.EconDiscardPay => !_state.Cards.Values.Any(c => InPlay(c) && Definition(c).Name == EmperorP5Abilities.EconTitle),
        EmperorP5Abilities.StartingPathRing => !ActiveP5Event(playerId, EmperorP5Abilities.StartingPathEffect),
        EmperorP5Abilities.NesEquip or EmperorP5Abilities.NesEquipOpen or EmperorP5Abilities.NesEquipBattle => !ActiveP5Event(playerId, EmperorP5Abilities.NesEffect),
        _ => false
    };

    // ---- Events -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsEmperorP5Event(string effect) => effect is EmperorP5Abilities.StartingPathEffect or EmperorP5Abilities.NesEffect;

    // ResolveEvent's hook: the Event stays in play for its duration (Assault in the Jungle's shape), and the province is refilled as for every Event.
    private bool ResolveEmperorP5Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        province.CardId = null; card.ProvinceIndex = null;
        card.Zone = Zone.EventInPlay;
        if (Definition(card).Effect == EmperorP5Abilities.StartingPathEffect)
        {
            card.ExpiresAtTurnsTaken = player.TurnsTaken + 2;
            Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play until {player.Name}'s second turn from now begins: he may put a Ring from his hand into play.", player.Id, card.Id);
        }
        else
        {
            card.ExpiresAtEventsPhaseTurnsTaken = player.TurnsTaken + 3;
            Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play until {player.Name}'s Events Phase three turns from now begins: he may Equip Followers from his discard pile.", player.Id, card.Id);
        }
        Refill(player, province);
        return false;
    }

    // ---- source zones, targets, performers ----------------------------------------------------------------------------------------------------------------------------------------------------

    // AbilitySourceZoneAllows' hook: Tsubute's second way to pay is used from its player's hand. Null for every other ability.
    private bool? P5SourceZoneAllows(AbilityDefinition ability, CardInstance source, PlayerState player) =>
        ReferenceEquals(ability, AbilityCatalog.All[EmperorP5Abilities.TsubuteHand]) ? SourceInHand(source, player) : null;

    private bool P5TargetAllows(string target, GameCommand command, PlayerState player, CardInstance? source, CardInstance card)
    {
        var own = card.OwnerId == player.Id; var type = Definition(card).Type;
        return target switch
        {
            // Hida Yamadera: an enemy Personality or Follower whose Force is lower than Yamadera's (a card with Force; a Terrain is not an enemy card).
            EmperorP5Abilities.EnemyUnitCardLowerForceThanSelf => !own && (type == CardType.Personality || type == CardType.Follower && card.Zone == Zone.Attached) && source is not null && CardForce(card) < CardForce(source),
            // Headbutt: "an enemy card without attachments and with equal or lower Force" than the performing Berserker (enemy-card-without-attachments' reading).
            EmperorP5Abilities.EnemyCardLtePerformerNoAttachments => !own && (type == CardType.Follower || type == CardType.Personality && !HasAttachments(card))
                && command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer) && CardForce(card) <= CardForce(performer),
            // Disrupting Communication: any card in an enemy unit (the Personality or an attachment).
            EmperorP5Abilities.EnemyUnitCard => !own && (type == CardType.Personality || card.Zone == Zone.Attached),
            EmperorP5Abilities.BiteOfWinterTitled => Definition(card).Name == EmperorP5Abilities.BiteOfWinterTitle,
            EmperorP5Abilities.EconTitled => Definition(card).Name == EmperorP5Abilities.EconTitle,
            _ => P5ZoneTargetAllows(target, command, player, source, card) // EmperorP5Zones.cs
        };
    }

    private bool P5PerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle)
    {
        var personality = Definition(performer).Type == CardType.Personality && InPlay(performer);
        return ability.Performer switch
        {
            // Shiba Jouta alive: "your performing Shugenja, who may be at any location" (home or any battlefield not yet resolved), bowed or not.
            EmperorP5Abilities.OwnPersonalityWithKeywordAnywhere => personality && HasKeywords(performer, ability.RequiredKeywords) && AtAnyLocation(performer),
            // Equip (Limited): "your performing Personality" at home.
            EmperorP5Abilities.OwnPersonalityAtHome => personality && performer.Zone == Zone.Home,
            EmperorP5Abilities.OwnPersonalityWithKeywordsAtHome => personality && performer.Zone == Zone.Home && HasKeywords(performer, ability.RequiredKeywords),
            // Equip (Battle): "your performing unbowed opposed Shugenja" at the current battlefield.
            EmperorP5Abilities.OwnUnbowedOpposedWithKeywordsAtBattle => personality && !performer.Bowed && AtBattle(performer) && HasKeywords(performer, ability.RequiredKeywords)
                && Army(1 - performer.OwnerId, _state.CurrentBattleProvince!.Value).Any(),
            _ => false
        };
    }

    private static bool IsP5Performer(string? performer) => performer is EmperorP5Abilities.OwnPersonalityWithKeywordAnywhere or EmperorP5Abilities.OwnPersonalityAtHome
        or EmperorP5Abilities.OwnPersonalityWithKeywordsAtHome or EmperorP5Abilities.OwnUnbowedOpposedWithKeywordsAtBattle;

    private string?[]? P5PerformerCandidates(AbilityDefinition ability, int playerId, bool actingAsBattle) =>
        !IsP5Performer(ability.Performer) ? null
        : _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && P5PerformerAllows(ability, c, actingAsBattle)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    private static bool P5BowsTarget(string abilityId) => abilityId is EmperorP5Abilities.YamaderaBow or EmperorP5Abilities.DisruptingBow;
    private static bool P5DestroysTarget(string abilityId) => abilityId is EmperorP5Abilities.HeadbuttBow or EmperorP5Abilities.HeadbuttFresh;

    private static int? P5AttackStrength(string abilityId) => abilityId switch
    {
        EmperorP5Abilities.TsubuteDestroy or EmperorP5Abilities.TsubuteHand => 3,
        EmperorP5Abilities.JoutaAttack or EmperorP5Abilities.JoutaDeadAttack => 5,
        EmperorP5ZoneAbilities.FoundriesAttack => 4,
        _ => null
    };
    private static string? P5AttackKind(string abilityId) => P5AttackStrength(abilityId) is null ? null : "Ranged";

    // ---- the targeted-by-action Reactions --------------------------------------------------------------------------------------------------------------------------------------------------

    private bool P5TargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        var command = subject.Command;
        switch (abilityId)
        {
            // Perplexing Guests: another player's action targets one of the reactor's own Personalities.
            case EmperorP5Abilities.GuestsDiscard:
                return command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality;
            // Inciting Mistrust: another player's Melee or Ranged Attack targets one of the reactor's own Ninja.
            case EmperorP5Abilities.MistrustNegate:
                return subject.IsAttack && command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Keyword(affected, "Ninja")
                    && AttackKind(command.AbilityId!) is "Melee" or "Ranged";
            default:
                return false;
        }
    }

    // ---- validation ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateEmperorP5(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Daigotsu Yutaka and Raido no Oni: bans on what they may perform (the card that performs by the printed rules).
        if (IsAction(ability) && ActionPerformerPerRules(command) is { } performer && InPlay(performer))
        {
            if (Definition(performer).Effect == EmperorP5Abilities.YutakaEffect)
                Require(!ActionCarriesKeyword(ability, source, "Bushido Virtue"), $"{Definition(performer).Name} may not perform Bushido Virtue actions.");
            var actingAsBattle = ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;
            if (Definition(performer).Effect == EmperorP5Abilities.RaidoEffect && actingAsBattle && _state.CurrentBattleProvince is int battle)
            {
                var attacker = _state.ActivePlayerId; var defender = 1 - attacker;
                var (attackerForce, defenderForce) = ArmyForces(Army(attacker, battle).ToArray(), Army(defender, battle).ToArray(), attacker, defender);
                var (mine, theirs) = performer.OwnerId == attacker ? (attackerForce, defenderForce) : (defenderForce, attackerForce);
                Require(mine <= theirs, $"{Definition(performer).Name} may not perform Battle actions while your current army has higher total Force than the current enemy army ({mine} to {theirs}).");
            }
        }
        ValidateEmperorP5Zones(id, ability, command, player, source); // EmperorP5Zones.cs
        switch (id)
        {
            case EmperorP5Abilities.YamaderaRefill:
                Require(_state.LastProvinceEntryCardId == source!.Id && _state.LastProvinceEntryProvinceIndex is not null, "Yamadera did not enter play from a province.");
                break;
            case EmperorP5Abilities.HeadbuttBow when command.PerformerId is { } bowId && _state.Cards.TryGetValue(bowId, out var bowed):
                Require(!NotInPlayWhenCombatSegmentBegan(bowed), $"{Definition(bowed).Name} was not in play when this Combat Segment began: he is not bowed (use the other way).");
                Require(!bowed.Bowed, $"{Definition(bowed).Name} is already bowed, so he cannot be bowed to pay this cost.");
                break;
            case EmperorP5Abilities.HeadbuttFresh when command.PerformerId is { } freshId && _state.Cards.TryGetValue(freshId, out var fresh):
                Require(NotInPlayWhenCombatSegmentBegan(fresh), $"{Definition(fresh).Name} was in play when this Combat Segment began: he must be bowed.");
                break;
            case EmperorP5Abilities.TsubuteDestroy:
                Require(source!.Zone == Zone.Attached, "Tsubute must be attached to be destroyed.");
                break;
            case EmperorP5Abilities.JoutaAttack:
                Require(InPlay(source!), "Jouta is dead: use his action from the dead pile.");
                break;
            case EmperorP5Abilities.YutakaReturn:
            case EmperorP5Abilities.YutakaReturnDark:
                {
                    Require(_state.CurrentBattleProvince is int yutakaBattle && Army(1 - player.Id, yutakaBattle).Any(), "Yutaka would not be opposed at the current battlefield.");
                    var dark = TookDarkVirtueActionThisPhase(player.Id);
                    Require(id == EmperorP5Abilities.YutakaReturnDark ? dark : !dark, dark ? "You have taken a Dark Virtue action this phase: Yutaka pays 3 less Gold." : "You have not taken a Dark Virtue action this phase.");
                    var quote = QuotePurchase(player.Id, source!.Id, command.ClanDiscount, command.ClanAllegiance, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                    break;
                }
            case EmperorP5Abilities.TokikoHome:
            case EmperorP5Abilities.TokikoBattlefield:
                if (command.TargetId is { } tokikoTarget && _state.Cards.ContainsKey(tokikoTarget))
                {
                    var quote = QuotePurchase(player.Id, tokikoTarget, command.ClanDiscount, command.ClanAllegiance, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                }
                break;
            case EmperorP5Abilities.BiteEnter:
            case EmperorP5Abilities.EconEnter:
                break;
            case EmperorP5Abilities.BiteDiscard:
                Require(BitesOfWinterInPlay() > 0, "No card titled The Bite of Winter is in play.");
                Require(!Card(player.StrongholdId).Bowed, "Your Stronghold is already bowed.");
                break;
            case EmperorP5Abilities.EconDiscardFavor:
            case EmperorP5Abilities.EconDiscardPay:
                Require(!P5PlayerAbilityDormant(id, player.Id), "No card titled Economic Repercussions is in play.");
                break;
            case EmperorP5Abilities.StartingPathRing:
                Require(ActiveP5Event(player.Id, EmperorP5Abilities.StartingPathEffect), "You do not have this ability.");
                // Brotherhood Schism: "Rings will not enter play if their owner has already put a Ring into play this turn."
                Require(_state.RingSchismTurn != _state.TurnNumber || player.RingPutIntoPlayTurn != _state.TurnNumber, "Brotherhood Schism: you already put a Ring into play this turn.");
                break;
            case EmperorP5Abilities.NesEquip:
            case EmperorP5Abilities.NesEquipOpen:
            case EmperorP5Abilities.NesEquipBattle:
                Require(ActiveP5Event(player.Id, EmperorP5Abilities.NesEffect), "You do not have this ability.");
                // The Equip rulebook action's own timing for a Follower (Limited on the player's own turn; Open or Battle only while a card grants it).
                if (id == EmperorP5Abilities.NesEquipOpen) Require(player.Id != _state.ActivePlayerId, "On your own turn this is the Limited Equip.");
                if (command.TargetId is { } nesTarget && _state.Cards.TryGetValue(nesTarget, out var nesFollower) && EquipTimingError(player.Id, nesFollower) is { } timing) Require(false, timing);
                break;
        }
    }

    // ---- execution: null when the id is not this group's; false when its effects are done; true when a decision or window opened and the action waits ------------------------------------------

    private bool? RunEmperorP5(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorP5Abilities.NaraProvinces:
                foreach (var province in player.Provinces.Where(p => !p.Destroyed).ToArray()) GiveProvinceStrengthThisTurn(player.Id, province.Index, 2, Definition(source!).Name);
                return false;
            case EmperorP5Abilities.YamaderaRefill:
                RefillProvinceFaceUp(player, _state.LastProvinceEntryProvinceIndex!.Value, source!);
                return false;
            case EmperorP5Abilities.YamaderaBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case EmperorP5Abilities.HeadbuttBow:
            case EmperorP5Abilities.HeadbuttFresh:
                DestroyTargetCard(target!, player.Id);
                return false;
            case EmperorP5Abilities.TsubuteDestroy:
            case EmperorP5Abilities.TsubuteHand:
            case EmperorP5Abilities.JoutaAttack:
            case EmperorP5Abilities.JoutaDeadAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId, consumedGrant);
            case EmperorP5Abilities.YutakaReturn:
            case EmperorP5Abilities.YutakaReturnDark:
                if (source!.Zone != Zone.Dead) { Emit("raiseFailed", $"{Definition(source).Name} is no longer dead.", player.Id, source.Id); return false; }
                return BringDeadPersonalityIntoPlay(player, source, Definition(source).Name, _state.CurrentBattleProvince);
            case EmperorP5Abilities.TokikoHome:
            case EmperorP5Abilities.TokikoBattlefield:
                if (target!.Zone != Zone.Dead) { Emit("raiseFailed", $"{Definition(target).Name} is no longer dead.", player.Id, target.Id); return false; }
                return BringDeadPersonalityIntoPlay(player, target, Definition(source!).Name, id == EmperorP5Abilities.TokikoBattlefield ? _state.CurrentBattleProvince : null);
            case EmperorP5Abilities.DisruptingBow:
                {
                    if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                    var leader = Player(1 - player.Id);
                    if (leader.Hand.Count == 0) { Emit("discardNothing", $"{Definition(source!).Name}: {leader.Name} has no card to discard.", leader.Id, source!.Id); return false; }
                    if (HasReconnaissanceAtCurrentBattlefield(player.Id))
                    {
                        var hand = leader.Hand.ToList();
                        var random = new StableRandom(_state.RngState);
                        random.Shuffle(hand);
                        _state.RngState = random.State;
                        Emit("randomDiscard", $"{Definition(source!).Name}: {player.Name} has Reconnaissance, so {leader.Name} discards a card at random.", leader.Id, source!.Id);
                        DiscardFate(leader, Card(hand[0]));
                        return false;
                    }
                    return BeginDeciderChoice(DeciderSpecs.KimiDiscard, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            case EmperorP5Abilities.BiteEnter:
            case EmperorP5Abilities.EconEnter:
                player.Hand.Remove(source!.Id); source.Zone = Zone.Home; source.FaceUp = true; source.ProvinceIndex = null;
                Emit("cardEnteredPlay", $"{player.Name} puts {Definition(source).Name} into play.", player.Id, source.Id);
                return false;
            case EmperorP5Abilities.BiteDiscard:
            case EmperorP5Abilities.EconDiscardFavor:
            case EmperorP5Abilities.EconDiscardPay:
                if (InPlay(target!))
                {
                    ResetCardState(target!);
                    target!.Zone = Zone.FateDiscard; target.ProvinceIndex = null; target.FaceUp = true;
                    Emit("discardedFromPlay", $"{Definition(target).Name} was discarded from play ({player.Name}).", target.OwnerId, target.Id);
                }
                return false;
            case EmperorP5Abilities.StartingPathRing:
                player.Hand.Remove(target!.Id);
                target.Zone = Zone.Home; target.ProvinceIndex = null; target.FaceUp = true; target.Bowed = false;
                RingEntersPlay(player, target, false, source, id); // issue #699 (WlRings.cs)
                Emit("cardEnteredPlay", $"{player.Name} put {Definition(target).Name} into play (Starting on the Path); while it remains in play it does not count towards an Enlightenment Victory.", player.Id, target.Id);
                return false;
            case EmperorP5Abilities.SeekStainKolat:
            case EmperorP5Abilities.SeekStainNinja:
            case EmperorP5Abilities.SeekStainShadowlands:
                GrantKeyword(target!, id == EmperorP5Abilities.SeekStainKolat ? "Kolat" : id == EmperorP5Abilities.SeekStainNinja ? "Ninja" : "Shadowlands", source!.Id, GrantDuration.Turn);
                return false;
            case EmperorP5Abilities.GuestsDiscard:
                {
                    var him = Card(HeldAction().TargetId!);
                    if (InPlay(him)) DiscardPersonalityFromPlay(him, Definition(source!).Name);
                    return false;
                }
            case EmperorP5Abilities.GuestsCreate:
                {
                    var name = Definition(source!).Name;
                    return ExecuteCreate(ability, ability.Creates!, command, player, () =>
                    {
                        var created = Card($"created-{_state.CreatedCardSequence:D4}");
                        ScheduleTimedRemoval(created, TimedRemovalWhen.TurnEnds, player.Id, name);
                    });
                }
            case EmperorP5Abilities.MistrustNegate:
                {
                    var name = Definition(source!).Name;
                    if (CurrentBattlefieldProvince() is { CardId: { } shownId })
                    {
                        var shown = Card(shownId);
                        Emit("cardShown", $"{name}: {Player(player.Id).Name} shows the card in the current battlefield's province: {Definition(shown).Name}.", player.Id, shown.Id);
                        RecordShown(player.Id, shown, source!, "Province", "reaction"); // the public shown-cards record
                        if (Definition(shown).Type == CardType.Personality) NegateHeldAction(source!);
                    }
                    else Emit("cardShown", $"{name}: the current battlefield's province holds no card.", player.Id, source!.Id);
                    return false;
                }
            case EmperorP5Abilities.MistrustDiscard:
                DiscardCurrentProvinceCard();
                Draw(player);
                return false;
            default:
                return RunEmperorP5Zones(id, ability, command, player, source, target, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants); // EmperorP5Zones.cs
        }
    }
}

// This property keeps its original JSON name and tail position in old saved MatchState payloads.
public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1005)]
    public EmperorP5State P5 { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP5Hooks()
    {
        EngineHooks.Abilities.Register("AddEmperorP5", 3000, static abilities => abilities.AddEmperorP5());
        EngineHooks.AttackStrengthBefore.Register("P5AttackStrength", 900, static (engine, context) => P5AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("P5AttackKind", 500, static context => P5AttackKind(context.abilityId));
        EngineHooks.PerformerAllows.Register("P5PerformerAllows", 700, static (engine, context) => engine.P5PerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.TargetAllows.Register("P5TargetAllows", 1500, static (engine, context) => engine.P5TargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PerformerCandidates.Register("P5PerformerCandidates", 100, static (engine, context) => engine.P5PerformerCandidates(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.DestroysTarget.Register("P5DestroysTarget", 100, static context => P5DestroysTarget(context.abilityId));
        EngineHooks.BowsTarget.Register("P5BowsTarget", 200, static context => P5BowsTarget(context.abilityId));
        EngineHooks.PersonalityForce.Register("P5Force", 600, static (engine, context) => engine.P5Force(context.card));
        EngineHooks.TargetedReaction.Register("P5TargetedReactionApplies", 800, static (engine, context) => engine.P5TargetedReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorP5", 1400, static (engine, context) => engine.ValidateEmperorP5(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorP5", 2200, static (engine, context) => engine.RunEmperorP5(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
