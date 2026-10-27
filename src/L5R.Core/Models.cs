using System.Text.Json.Serialization;

namespace L5R.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardType { Stronghold, Holding, Personality, Strategy, Follower, Item, Spell, Event, Celestial, Region, Ring }
[JsonConverter(typeof(JsonStringEnumConverter))]
// AttackDeclaration is the Declaration Segment (the Attacker declares an attack or declines, and declining ends the
// Attack Phase); InfantryDeclaration/DefenseAssignment is the Infantry Maneuvers segment that follows a declared attack;
// CavalryDeclaration/CavalryDefense is the Cavalry Maneuvers segment after it (core rules Attack Phase, steps A-C).
public enum Phase { Straighten, Events, Action, AttackDeclaration, InfantryDeclaration, DefenseAssignment, CavalryDeclaration, CavalryDefense, BattleSelection, Battle, Dynasty, DynastyDiscard, End, Finished }
[JsonConverter(typeof(JsonStringEnumConverter))]
// RemovedFromGame (issue #184 -- core rules Glossary): a card here can never be targeted or used
// again under any circumstance, distinct from Dead/DynastyDiscard/FateDiscard -- both Fate and
// Dynasty cards mix into this one zone, matching the single shared "Removed from Game" pile the
// client will render (a Dead/DynastyDiscard/FateDiscard card is always specifically one or the
// other; this one is neither, and both kinds of card can end up here).
// Focused (issue #204 -- Dueling): a Fate card played face-down next to a dueling Personality. Not
// "played" in the ordinary sense (no ability on it resolves except a "Focus Effect" one, later, in
// the duel's own resolution order) -- visible to its own owner immediately, to both players once the
// duel's own Struck reveal happens (PendingDuel.Struck), same shape as a private Hand card that
// becomes public.
// EventInPlay (Suspicions, the first Event): an Event's printed text normally resolves once, in the
// Events Phase, then goes straight to DynastyDiscard (core rules Glossary -- "resolves and is
// discarded"). An Event whose own text is an ongoing/"until the end of the game" effect instead sits
// here -- a physical reminder area next to a player's Personalities (the client's own future
// "sideboard" rendering, not yet built) -- rather than the discard pile, for as long as that effect
// stays active. Nothing yet moves a card OUT of this zone (Suspicions' own effect never ends), but the
// intended shape for a future card that does end it is: move to DynastyDiscard, same as an ordinary
// Event. Public zone, not hidden information, like RemovedFromGame.
// Region (the first being Low Market/Fallow Lands): attaches to a PROVINCE, not to another card --
// deliberately a distinct Zone from Attached (which always resolves its parent via
// CardInstance.AttachedTo/Card(...)) so Attachments()/DestroyAttachment()/AtBattle() never misfire on
// it. ProvinceState.RegionId is the province-side pointer (the province "owns" the Region, mirroring
// CardId for the Dynasty card sitting there); the Region's own AttachedTo stays null and its
// ProvinceIndex names which province it is attached to. Public, face-up, like a Holding/Personality
// once revealed. Visually renders like an ordinary attachment despite the different host.
// Celestial (the first being Lady Matsu's Rage): unlike Event/Region, enters play the instant it is
// revealed in ANY phase (not deferred to the controller's own next Events Phase) and never
// resolves-once-then-discards or attaches to anything -- it just stays in play, in the player's own
// right-side sideboard alongside Rings and reminders (a visual grouping only; Rings themselves still
// use Zone.Home internally), until something removes it. A player may control more than one Celestial
// only in passing -- entering play immediately discards every OTHER Celestial that same player
// controls, so at most one survives at rest.
// Underneath (issue #503, The New Foundries): a card set face-up under a Stronghold -- public, not in play (EmperorP5Zones.cs).
// Resolving (issue #917, ResolutionArea.cs): the temporary resolution area of rules step A -- a Strategy or Ring from the hand sits here, face up and out of play, from announcement until its action ends. Appended last: the enum serializes by name.
public enum Zone { Stronghold, Home, Province, DynastyDeck, FateDeck, Hand, DynastyDiscard, FateDiscard, Dead, Battlefield, Attached, RemovedFromGame, Focused, EventInPlay, Region, Celestial, Underneath, Resolving }

public sealed record CardDefinition(string Id, string Name, CardType Type, string Clan = "Unaligned", int GoldCost = 0,
    int GoldProduction = 0, int Force = 0, int Chi = 0, int PersonalHonor = 0, int? HonorRequirement = null,
    int ProvinceStrength = 0, int StartingHonor = 0, string Effect = "none", string Text = "",
    bool ScenarioOnly = true, string? ArtworkKey = null, string[]? Keywords = null,
    string[]? Abilities = null, int Focus = 0,
    // Overrides Clan for a Personality aligned with more than one Clan; null means "just Clan".
    string[]? Clans = null,
    // An attachment's own "Will only/not attach to X" restriction (Item/Follower/Spell alike).
    // AttachRequiresKeyword/AttachExcludesKeyword usually name a printed keyword, checked against the
    // target Personality -- "Human" is a special case (core rules Glossary: no keyword is ever
    // printed for it, so it means "does not have Nonhuman"). AttachRequiresName is the rarer
    // named-Personality restriction (e.g. "Attaches to Yotsu Ueda").
    string? AttachRequiresKeyword = null, string? AttachExcludesKeyword = null, string? AttachRequiresName = null,
    // "This Personality has X" (Commander's Steed's shape): an attachment that grants its wielder a
    // keyword for as long as it stays attached -- reuses the existing GrantedKeyword("Attachment")
    // lifecycle (already cleaned up on detach/destroy; see DestroyAttachment), just never exercised by
    // a published card until now.
    string? AttachGrantsKeyword = null,
    // Seppun Heavy Elite's shape: grants its wielder the EFFECT Elite has (Force still counts while
    // bowed, see BattleForce's EliteEffect() check) without actually granting the Elite keyword
    // itself -- a real keyword grant would satisfy any future ability that checks specifically for
    // the printed Elite keyword (the same printed-vs-granted distinction Justice of the Crane's own
    // text draws for Magistrate). Deliberately not folded into AttachGrantsKeyword/GrantedKeywords.
    bool AttachGrantsEliteEffect = false,
    // Passive Ranged/Melee Attack-strength modifier applied to an incoming Attack TARGETING the
    // scoped card(s) -- Destrier's shape ("Melee and Ranged Attacks targeting this Follower have
    // -3 strength"), generalized. AttackDefenseKind is "Melee", "Ranged", or "Both" (null: grants
    // none); AttackDefenseAmount is signed (negative protects, matching every printed card seen so
    // far, but nothing here assumes it can't be positive, e.g. a card that makes attacks easier to
    // land against it). AttackDefenseScope is "Self" (only this card itself, Destrier's own shape),
    // "Wielder" (an Item's default when its text does not say "unit" -- conveys to the Personality
    // wielding it, not the Item itself), or "Unit" (every card in the unit -- Singh Remnants'/
    // Elephant Cavalry's shape). Which scope applies is decided per card when it is wired, from its
    // own printed text -- the engine does not infer it from Type.
    string? AttackDefenseKind = null, int AttackDefenseAmount = 0, string AttackDefenseScope = "Self",
    // Mandatory "after this card enters play" text with NO printed "Reaction:" prefix (core rules
    // convention, issue #210) -- e.g. every Spider Clan "Lose X Honor" card: applied automatically,
    // never offered as a choice, unlike an actual Reaction (which IS optional and must be offered).
    // Checked unconditionally in OpenEntryReactionWindow, the same way Destined's own mandatory draw
    // already is -- a data field rather than an ability id, since there is no decision to route
    // through UseAbility at all.
    int? EntryHonorLoss = null,
    // Mirumoto Yozo, Experienced 2's own second trait: "after your Iaijutsu action resolves, give
    // any Personality who performed it +2F" -- a passive, ongoing check (issue #207's own Dragon
    // Clan review follow-up), separate from her entry trait (discard-other-sensei), which already
    // occupies the single-valued Effect field above.
    bool GrantsIaijutsuBonus = false,
    // Mirumoto Hikaro's own second trait: "before you focus in a duel involving your Dragon Clan
    // Personality, once per duel, you may focus from your discard pile instead (revealed to the
    // opponent immediately; removed from the game after the duel ends)" -- same reasoning as above,
    // separate from her own entry trait.
    bool GrantsDiscardPileFocus = false,
    // Kitsu Akai's own Limited action (issue #257): this definition is minted at runtime by a card
    // effect (MatchEngine.CreatePersonality), never drawn from a deck or a Province -- the Lion
    // Ancestor's own printed text, "May not be included in decks." DeckWorkshop.Review flags a
    // CreatedOnly card if one somehow ends up selected anyway (the same soft-warning convention
    // every other deck-legality rule in this engine already uses, never a hard block). Also the
    // signal MatchEngine.DestroyUnits reads to redirect a destroyed created card to
    // Zone.RemovedFromGame instead of an ordinary discard pile once destruction otherwise finishes,
    // since a created card has no physical printed card to return to any pile.
    bool CreatedOnly = false,
    // Akodo Chiyo ("Crab Clan players may ignore Chiyo's Honor Requirement") and Kitsu Ririko
    // ("Ririko enters play ignoring Honor Requirement if you are a Unicorn Clan player") -- one shared
    // controller-clan-conditional waiver (issue #299, Batch E): when the player recruiting this
    // Personality belongs to the named Clan (PlayerState.Clan, the same value every existing
    // Clan-conditional passive -- crane-clan-discount, mantis-clan-discount, attacking-vs-dragon-bonus --
    // already compares against), MatchEngine.QuotePurchase skips its Family Honor >= Honor Requirement
    // check entirely, with no Clan-allegiance surcharge (that is a different, separate waiver route).
    // A data field rather than an Effect string, since Effect is single-valued and each of these cards
    // has its own separate reason to occupy it (or, like Ririko, none at all).
    string? HonorRequirementWaiverClan = null,
    // "You may include up to N copies of this card in your deck" (Fading Shadows 10; issue #337): the deck-construction copy
    // limit this card prints in place of the usual 3 (1 for a Unique card). Read only by DeckWorkshop's soft legality note;
    // null is every card without such a line. It is a per-card limit, so it never changes what any other card is held to.
    int? DeckCopyLimit = null,
    // Bo of Water: the Elite effect (never the Elite keyword) goes to the wielder only while he has this keyword ("While this Shugenja is Water, he contributes Force even if bowed").
    string? AttachGrantsEliteEffectWhileWielderHas = null);

/// <summary>One applied Force/Chi change, in application order, so a future card can negate a
/// specific prior effect instead of only ever resetting a flat running total. Permanent: "permanently give him +2F" (Spirit of the Berserker) -- kept when the
/// turn ends (FinishTurn), unlike every other modifier, and reset with the card when it goes back to a hand or a deck. SuppressedTurn: the turn a temporary prevention ("negate all current and new Force
/// penalties on him", Tireless Efforts; Creating Order) hides the modifier for (it lasts that turn; it does not end the modifier) -- it keeps its source, amount and Permanent state, counts for nothing
/// (StatModifier) during SuppressedTurn, and applies again from the next turn on, unless its own duration ended first (FinishTurn sweeps a turn's modifier whether or not it is hidden).</summary>
public sealed record AppliedModifier(string Source, string Effect, string Stat, int Amount, bool Permanent = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SuppressedTurn = null);

/// <summary>One negation a "before an action resolves" reaction has set on a card for the deferred command:
/// Kind is "destroy", "move", "straighten", "bow" (each consumed by its first match) or "all" (Champion of
/// Thunder: every effect the action applies to that card, live for the whole re-invocation). ByPlayer (issue #667, PerformersSled.cs): the player whose Reaction set it, stamped when that
/// Reaction's effects finish (Winter Sled: other players' cards' effects will not negate the Personality's movement).</summary>
public sealed record NegatedEffect(string Kind, string CardId, int? ByPlayer = null);

/// <summary>A reaction window set aside while a Reaction played inside it (an action, like any other) has a window of its own to deal
/// with -- a before-action window, a forced discard, Unimpeachable Name, an "after this action" window. PendingChoice holds one window,
/// so the waiting one is kept here, with the transient state that belongs to it, and reopened as it was when the new one is done
/// (see ReactionsAreActions.cs). ReactingPlayerId is who was playing the Reaction, i.e. who held priority in it.</summary>
public sealed class SuspendedWindow
{
    public PendingChoice Choice { get; set; } = null!;
    public int ReactingPlayerId { get; set; }
    public int ConsecutivePasses { get; set; }
    public GameCommand? NegatableAbility { get; set; }
    public bool ActionNegated { get; set; }
    public List<NegatedEffect> NegatedEffects { get; set; } = [];
    public GameCommand? OutOfPlayActionReaction { get; set; }
    public int? HonorLossReductionPlayerId { get; set; }
    public GameCommand? ForcedDiscardContinuation { get; set; }
    public string? ForcedDiscardResume { get; set; }
    public List<string> BowReactionCandidateIds { get; set; } = [];
    public string? MoveReactionTargetId { get; set; }
    public string? NikutaiReactionTargetId { get; set; }
    // The window's own target and the targets still to come (issue #336, TargetWindows.cs): a Reaction that opens a window of its own must not clobber them.
    public string? ReactionWindowTargetId { get; set; }
    public List<string> ReactionWindowRemaining { get; set; } = [];
    // The record of the action an after-action window is about (issue #898): a Reaction played in the window records its own resolution, and the window's Reactions still read the action's.
    public ResolvedActionFacts? LastResolvedAction { get; set; }
    // The watchers of this single-card window still to be asked after it (ReactionWatcherQueue.cs).
    public List<WatcherTurn> WatcherQueue { get; set; } = [];
}

/// <summary>A keyword granted by another card rather than printed. Duration is "Attachment"
/// (lasts until that attachment moves or is destroyed), "Turn" (cleared at turn end, the default
/// for an action-granted keyword), or "Permanent" (only when the granting text says so).</summary>
public sealed record GrantedKeyword(string Keyword, string Source, string Duration, string? CopiedFrom = null, string? CopiedFromName = null);

/// <summary>An ability a card has because another card's effect gave it (as opposed to one printed on it), the ability twin of
/// GrantedKeyword. Duration is "Permanent" (only when the granting text says so; it survives the card dying and returning),
/// "Turn" (cleared at turn end) or "Copy" (a copied ability, kept until the copying ability is used again -- Source is that
/// copying ability's id; CopiedFrom/CopiedFromName say which card it was copied from). "Battle" ends with the battle;
/// "Attachment" ends when the granting attachment leaves its host. Source is the granting card instance, or the copying
/// ability id for a copy. See AbilityGrants.cs and CardCopy.cs.</summary>
public sealed record GrantedAbility(string AbilityId, string Source, string Duration, string? CopiedFrom = null, string? CopiedFromName = null);

/// <summary>Read-only explanation of an already enforced granted effect. This is not an
/// executable ability definition. Older snapshots may omit it; the client must not invent one.</summary>
public sealed record GrantedEffectView(string Name, string Text, string Source, string Duration, string Kind = "Ability");

/// <summary>"Protection" (issue #212: Tamori Takeshi's "Effects of actions the target performs
/// will not be negated") -- while any Protection sits in a card's own list, no card of the
/// opposing kind (Effortless Counterattack's own Additional-Action negation, Attack-strength
/// negation) may negate an action performed by it. Same Duration convention as GrantedKeyword
/// ("Turn" is the default -- core rules: an effect lasts until the end of the active player's own
/// turn unless its own text says otherwise, e.g. "Permanent" or "until this battle ends"); cleared
/// the same place ("Turn") FinishTurn already clears GrantedKeywords -- regardless of the card's
/// own Zone, matching a Permanent GrantedKeyword's own survival through death and return.</summary>
public sealed record Protection(string Source, string Duration);

/// <summary>"May not perform actions" (issue #336: Daigotsu Hotako's Fear Open, and the same gap on Tamori House Guard and
/// Hiruma Nikaru): while any ActionLock sits in a card's list it cannot be the performer of any action -- its own, an
/// action on one of its attachments, or one naming it as the performer -- yet it can still be targeted. Duration follows
/// the GrantedKeyword/Protection convention: "Turn" (the default -- until the end of the turn) is swept by FinishTurn.</summary>
public sealed record ActionLock(string Source, string Duration);

/// <summary>The moments of a turn a straighten hold can wait for. Each happens during the active player's turn; a hold names whose turn counts.</summary>
public enum StraightenPoint
{
    TurnBegins, StraightenPhaseEnds, EventsRevealEnds, ActionPhaseEnds, TurnEnds,
    // "until the phase ends" (Ronin Ambushers, issue #453 batch 5): released when the phase in progress gives way to a different one (the whole
    // Attack-declaration-through-battle-resolution stretch counts as one phase, the War Weariness convention). Owner is always null.
    PhaseEnds
}

/// <summary>"May not straighten until X" (issue #415). <see cref="Owner"/> is the player whose turn counts (null: whoever's turn it is when the point comes, "this turn" and "the
/// next Straighten Phase"); <see cref="Until"/> is the point; <see cref="Skip"/> is how many of those points must pass with the hold still standing before it is released (0: the
/// next one releases it). <see cref="WhileInPlay"/> ties the hold to a card ("while this card remains in play"): it ends when that card leaves play.</summary>
public sealed class StraightenHold
{
    public string Source { get; set; } = "";
    public int? Owner { get; set; }
    public StraightenPoint Until { get; set; }
    public int Skip { get; set; }
    public string? WhileInPlay { get; set; }
}

/// <summary>A physical marker on a card, not a card itself (core rules Glossary — Token). Type
/// identifies the kind of token (e.g. "Fire", "Madness") for other effects to count/reference; it
/// does not grant its host a keyword. Stat/Amount, when set, is a persistent bonus/penalty carried
/// by this specific token -- unlike a Modifier, it is not cleared at turn end, only when the token
/// itself is destroyed or its host card leaves play.</summary>
// Issue #503 (Shosuro Keirei's "-1F/-1C Poison tokens"): a token may carry a second stat bonus/penalty (Stat2/Amount2); StatModifier reads both.
// Issue #690 (P24, EffectPrimitivesTokens.cs): and a third (Stat3/Amount3: A Doomseeker's Tomb's "+1F/+1C/+1PH token"); absent from every older save, where it reads as none.
public sealed record CardToken(string Id, string Type, string Source, string? Stat = null, int Amount = 0, string? Stat2 = null, int Amount2 = 0, string? Stat3 = null, int Amount3 = 0);

public sealed class CardInstance
{
    public string Id { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    // A created card whose printed values belong to this one instance (Venerated Sensei's Clan, Your Name is Mine's title and Gold Cost): the definition every rule reads
    // through MatchEngine.Definition, instead of the catalogue's. Null for every card whose printed values are the catalogue's, which is nearly all of them.
    public CardDefinition? Printed { get; set; }
    public int OwnerId { get; set; }
    // A match-unique number a Personality is given the first time it is in play (issue #611), so the table can tell identical units apart without
    // reading the opaque card id. 0 until then; it stays with the card if it leaves play and returns.
    public int UnitNumber { get; set; }
    // Control follows the Personality (issue #352): a card attached to a Personality is controlled by that Personality's controller, and a card
    // brought into play from an ENEMY discard or dead pile is controlled by the player who brought it. While such a card is in play OwnerId is
    // its CONTROLLER (every "you control" check in the engine reads OwnerId); this is its real OWNER, whose pile it returns to the moment it
    // leaves play. Null for every card in its owner's control, which is nearly all of them.
    public int? OriginalOwnerId { get; set; }
    public Zone Zone { get; set; }
    public int? ProvinceIndex { get; set; }
    public bool FaceUp { get; set; }
    // P21 (ZoneExtensions.cs, issue #697): the card this one sits UNDER (Zone.Underneath with a host: The Tachikaze's Followers); null for the cards under the owner's Stronghold and for every card not under a host.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HostCardId { get; set; }
    // P21: the turn this card entered play (stamped after the command that put it in play, cleared once it leaves play); null while not in play.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EnteredPlayTurn { get; set; }
    // Issue #492 (Mendon's ruling R5): "Terrain may not be bowed or straightened." A Terrain (a Strategy with the Terrain keyword) is therefore never bowed, whichever path tries: every one of the
    // hundred-odd places that set a card bowed goes through this one property.
    private bool _bowed;
    public bool Bowed { get => _bowed; set => _bowed = value && !IsTerrain(); }
    private bool IsTerrain() => (Printed ?? (CardCatalog.Cards.TryGetValue(DefinitionId, out var printed) ? printed : null)) is { Type: CardType.Strategy, Keywords: { } keywords } && keywords.Contains("Terrain");
    public string? AttachedTo { get; set; }
    // Mastering the Elements (issue #838, rules lead): the keywords, trait and abilities it copied last only while it stays in play; set when it copies, cleared (with Printed) by SweepSpellCopies once it has left play.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CopiedSpellRules { get; set; }
    public List<AppliedModifier> Modifiers { get; set; } = [];
    public List<GrantedKeyword> GrantedKeywords { get; set; } = [];
    // Abilities gained from another card's effect (Fukuzo's copied ability; a permanent grant). Usable exactly like a printed
    // ability -- MatchEngine.AbilityIdsOf is the one place that adds them to Definition.Abilities.
    public List<GrantedAbility> GrantedAbilities { get; set; } = [];
    // Keywords removed from this specific card by permanent or ongoing effects. Overlay transfers
    // these removals just like granted keywords, so the replacement Personality retains its state.
    public List<string> RemovedKeywords { get; set; } = [];
    // Clan affiliations removed from this specific Personality by card effects. When every printed
    // affiliation is removed, the Personality is Unaligned for all rules checks.
    public List<string> RemovedClanAffiliations { get; set; } = [];
    public List<Protection> Protections { get; set; } = [];
    public List<ActionLock> ActionLocks { get; set; } = [];
    public List<CardToken> Tokens { get; set; } = [];
    public bool Dishonorable { get; set; }
    public int StraightenBlockedTurn { get; set; } = -1;
    // Tireless Efforts (Second City): "Negate all current and new Force penalties on him" -- the turn it was played; while TurnNumber equals it, a Force penalty
    // (a negative Force modifier or token) is not applied to this card. Reset with the card like the other per-turn flags.
    public int ForcePenaltiesNegatedTurn { get; set; } = -1;
    // Creating Order (Forgotten Legacy pass, issue #467 batch 3): "negate all current and new Force bonuses and penalties on him from actions" -- the turn it was played;
    // while TurnNumber equals it, no Force modifier or Force token from a resolving action is applied to this card (ForgottenLegacyBatch3.cs).
    public int ForceModifiersNegatedTurn { get; set; } = -1;
    // Consideration (Forgotten Legacy pass, issue #467 batch 3): "After the next time this turn another player's action targets him, negate its effects" -- the turn the
    // shield was raised; spent by the first such action that turn (ForgottenLegacyBatch3.cs, TurnTargetShieldNegates).
    public int TargetShieldTurn { get; set; } = -1;
    // Deep Roots (The Shadow's Embrace pass, issue #476 batch 3): "Negate ... effects which prevent him from contributing Force. He contributes Force even while bowed" -- the turn it
    // was played; while TurnNumber equals it, he contributes his Force to his army while bowed and no contribution negation applies to him (ShadowsEmbraceBatch3.cs).
    public int DeepRootsTurn { get; set; } = -1;
    // "May not be straightened this turn" (issue #411, Basic Lesson's Stronghold): every effect that would straighten this card is refused for the whole of the turn with this number,
    // unlike the one-shot StraightenBlockedTurn above, which negates the next attempt only. Not swept: it simply stops holding when the turn number moves on, which is after the
    // End phase, so the next Straighten phase (which is not an effect) is untouched.
    public int StraightenLockedTurn { get; set; } = -1;
    // Timed straighten holds (issue #415, StraightenHolds.cs): "negate its straightening until [a point in a player's turn]" -- Hibernation, Visitation, Bamboo Harvesters' "will not
    // straighten before your second turn". While any hold stands the card is not straightened by the Straighten phase or by any effect; PrintedHoldsSeeded records that the holds a
    // printed trait implies were created for this stay in play.
    public List<StraightenHold> StraightenHolds { get; set; } = [];
    public bool PrintedHoldsSeeded { get; set; }
    // Movement locks (issue #415, MovementLocks.cs): kinds of movement refused until the end of a given turn.
    public List<MovementLock> MovementLocks { get; set; } = [];
    // Hibernation: "after the next time this game another player's Battle action targets it, negate the action's effects" -- set when it hibernates, spent by that action.
    public bool TargetShield { get; set; }
    // Superior Reach (issue #420, TargetBars.cs): actions the named cards perform may not target this card, for the turn and while the condition holds.
    public List<TargetBar> TargetBars { get; set; } = [];
    // Never Safe (issue #417): the number of the battle in which this card was played, so that "after this battle ends, if this card is in your discard pile" is offered for that battle only.
    public int ReturnAfterBattle { get; set; } = -1;
    // Tamori Tamoko's own Open action (issue #207 follow-up): "assigned to attack ... this turn" --
    // set once, in Assign(), the turn this card is assigned during Infantry/Cavalry Maneuvers.
    public int AssignedToAttackTurn { get; set; } = -1;
    public Dictionary<string, int> AbilityUses { get; set; } = [];
    // Emperor Edition remaining-cards pass (issue #492), group G: the Personality this card was attached to when it was last destroyed (Ikiryo's "the Personality to whom this card was last attached", Blade of Perfection's "its Personality").
    public string? LastHostId { get; set; }
    // "Until your second turn from now begins" (issue #259 -- Rebuilding Kyuden Hida/Times of
    // Strife/War Weariness, the first Events with a bounded ongoing duration; Suspicions' own
    // "until the game ends" needs no expiry at all). Stamped with the OWNING player's own
    // PlayerState.TurnsTaken value, +2, when a qualifying Event enters Zone.EventInPlay -- a
    // per-player turn count, not the shared MatchState.TurnNumber (which advances once for EITHER
    // player's turn, so "your second turn from now" is +4 raw turns in this two-player alternating
    // game, not +2 -- TurnsTaken already counts only this player's own turns, avoiding that trap).
    // StartTurn discards the card once the owner's own TurnsTaken reaches it. Null for an Event with
    // no bounded duration (Suspicions, Formal Apology) or any non-Event card.
    public int? ExpiresAtTurnsTaken { get; set; }
    // Emperor Edition remaining-cards pass (issue #492), group F: "until your Events Phase N turns from now begins" -- the Event leaves play as its owner's Events Phase begins with this many turns taken.
    public int? ExpiresAtEventsPhaseTurnsTaken { get; set; }
    // A created COPY of another card (issue #398, Mendon): this instance's DefinitionId is the copied card's own, so it behaves exactly as that card, and this flag is the only thing
    // that says it is a created card: it has no physical card to return to (it is removed from the game when it leaves play) and the client marks it so. Every other created card is a
    // CardDefinition.CreatedOnly scan and needs no marker.
    public bool CreatedCopy { get; set; }
    // Issue #501 (Mendon's item 32, EmperorMechBCopies.cs): what the client's "duplicate" frame shows for a copy -- the instance id and definition id of the card it copies (its scan
    // is the original's), and the conditions on the copy (timed removal, ...), each a human-readable sentence. Null / empty for every card that is not a copy.
    public string? CopyOfCardId { get; set; }
    public string? CopyOfDefinitionId { get; set; }
    public List<string> CopyConditions { get; set; } = [];
    // Seeds of Decay pass (issue #453), batch 1. Defensive Blockade: the battle (MatchState.BattleNumber) in which this card contributes no Force to its army's total.
    public int ForceContributionNegatedBattle { get; set; } = -1;
    // Embracing Battle / Military Promotion: the turn in which this Personality may be assigned to a battlefield even if bowed.
    public int AssignBowedTurn { get; set; } = -1;
    // Megumi's Guidance: the turn through which this Personality's destruction from other players' cards' effects is negated (SeedsOfDecayBatch1.cs).
    public int DestructionShieldTurn { get; set; } = -1;
    // Emperor Edition Personality pass (issue #477), batch 4. Asahina Michiru's barrier of air: the turn through which the NEXT destruction of this Personality by another player's action is
    // negated and moves him home instead (spent by that destruction). Bayushi Rentatsu: true while the Limited action's "while Rentatsu remains bowed" effect stands
    // (EmperorPersonalityBatch4.cs; ends when he straightens or leaves play).
    public int BarrierTurn { get; set; } = -1;
    public bool RentatsuHeld { get; set; }
    // Seeds of Decay pass (issue #453), batch 2. Sentinel Spirit ("if it was opposed this turn"): the last turn this card stood in a battle whose two armies opposed each other
    // (MarkOpposed, at the battle's resolution); the AssignedToAttackTurn convention. Dragon Tattoo - exp ("who has not performed an action from a Dragon Tattoo this game"):
    // the titles of the cards whose actions this Personality performed, kept for the game (never reset with the card: it is the same card).
    public int OpposedTurn { get; set; } = -1;
    public List<string> PerformedActionFromTitles { get; set; } = [];
}

public sealed class ProvinceState
{
    public int Index { get; set; }
    public int Strength { get; set; }
    // Times of Strife (issue #259): "all Provinces have a maximum strength equal to their base
    // strength" -- a clamp CEILING, distinct from Strength itself (which Ring of Earth/a Dueling win
    // bonus/etc. already mutate directly, in place). Set once at match setup, identically to Strength
    // there, and never touched again by anything -- the one stable value Times of Strife's own clamp
    // (EffectiveProvinceStrength) has to cap back down to.
    public int PrintedStrength { get; set; }
    public bool Destroyed { get; set; }
    public string? CardId { get; set; }
    // Issue #503 (EmperorP6.cs): Illusory Defense's created province "permanently does not hold Dynasty cards" (never refilled, nothing put into it); Dark Experiments' permanent mark ("until the
    // game ends, after a Region attaches to it, destroy the Region, and after a Holding enters play from it, give the Holding a -1GP token").
    public bool HoldsNoDynastyCards { get; set; }
    // P21 (issue #697, Kokure's Lands): this province holds no Dynasty cards WHILE the named card is in play (MatchEngine.HoldsNoDynasty reads it with the permanent flag above).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldsNoDynastyCardsWhile { get; set; }
    public bool DarkExperimentsMarked { get; set; }
    // Reconnaissance (core rules Glossary): a per-player boolean at this specific province, indexed
    // by player id, defaulted to false. No ability removes it directly; it resets to false for both
    // players only once a battle at this province resolves (ResolveBattle), regardless of outcome.
    public bool[] Reconnaissance { get; set; } = new bool[2];
    // The Region currently attached to this province, if any (at most one -- ResolveRegion discards a
    // second Region revealed here rather than replacing the first). Mirrors CardId's own role for the
    // face-down/face-up Dynasty card, but for the Zone.Region card instead.
    public string? RegionId { get; set; }
    // Plains of Otosan Uchi (oracle-l5r-5979, OtosanUchiPlains.cs): the province's second Dynasty-card slot, mirroring
    // CardId's own role. Only ever populated while GrantsSecondDynastySlot(this) holds (RegionId is Otosan Uchi's own
    // Region) -- except the catalogue's own errata note: once that Region is destroyed independent of the province
    // (Brawl), whatever already sits here stays until it next leaves normally, and simply is not refilled again.
    public string? CardId2 { get; set; }
    // Strength bonuses from cards that stay in play (Hasty Fortifications, issue #418): counted while their source is.
    public List<ProvinceBonus> SourceBonuses { get; set; } = [];
}

public sealed class PlayerState
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Clan { get; set; } = "";
    public int Honor { get; set; }
    public int TurnsTaken { get; set; }
    // P21 (issue #697): the ids of this player's Personalities that were in play at the end of this player's last turn ("in play during your last turn"); null until a turn has ended.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? InPlayDuringLastTurn { get; set; }
    public int ProclaimUsedTurn { get; set; } = -1;
    /// <summary>The Personality whose entry Proclaim was taken this turn: a Personality whose Proclaim ignores the once-per-turn limit (Kakita Yusugi) may still be Proclaimed only ONCE per entry (issue #642: the bot soak found it offered forever).</summary>
    public string? ProclaimedCardId { get; set; }
    /// <summary>The turn this player last bought a Personality from a province (issue #639): the next purchase that turn is no longer the one that defaults to Proclaim.</summary>
    public int PersonalityPurchaseTurn { get; set; } = -1;
    /// <summary>True once this player has bought, overlaid or discarded a province card since the current Dynasty Phase began; reset when a Dynasty Phase begins. Lets a client confirm an End Dynasty that would do neither. Derived from commands, so replay recomputes it.</summary>
    public bool DynastyActionTaken { get; set; }
    public bool DishonorLossPending { get; set; }
    // Utaku Chikako's own Limited ability (issue #186): "You may not declare an attack this turn" --
    // a broad restriction (not just her own printed card's interaction), so it blocks declaring an
    // attack (and, defensively, any attacker assignment) outright regardless of what grants the opportunity. Reset every StartTurn.
    public bool MayNotAttackThisTurn { get; set; }
    // Ring of Air's own recruitment condition (issue #194): "after you resolve two or more Favor
    // actions in one turn." Incremented at each Favor-consuming ability's own execution case
    // (shunya-honor, favor-cycle, favor-battle); reset every StartTurn.
    public int FavorActionsThisTurn { get; set; }
    // Moshi Madoka - exp (Gempukku pass, issue #461): "If you have attached a card from your hand this turn" -- set wherever a card leaves the hand to become an attachment, reset every StartTurn.
    public bool AttachedFromHandThisTurn { get; set; }
    // Emperor Edition remaining-cards pass (issue #492), group F. Alter History: "until the game ends, he may ignore Honor Requirements when bringing Personalities into play". Benefices of the Emperor:
    // "permanently increase his maximum hand size by three". Rebuilding the Empire: "he may not declare an attack until your next turn begins" -- refused while the match's turn number is at or below this.
    public bool IgnoresHonorRequirements { get; set; }
    public int MaxHandBonus { get; set; }
    public int AttackLockedThroughTurn { get; set; } = -1;
    // Ring of Fire's own recruitment condition (issue #194, the "Ring of Flame" printing): "after you
    // resolve your second or later action in one turn which destroyed a Personality you did not
    // control." Incremented once per qualifying ResolveBattle destruction (a simplification: only
    // battle-driven destructions are counted, not every possible destroy path -- documented in
    // ROADMAP.md). Reset every StartTurn.
    public int DestroyedEnemyPersonalityActionsThisTurn { get; set; }
    // Ring of Water's own recruitment condition (issue #194): "a battle... in which you played a
    // Terrain, destroyed a Terrain, and destroyed any cards or provinces during resolution." Both
    // flags are battle-scoped (like NoNewAdditionalActionsThisBattle): set when this player's own
    // Terrain-play/Terrain-destroy actually happens, cleared at FinishBattleResolution.
    public bool TerrainPlayedThisBattle { get; set; }
    public bool TerrainDestroyedThisBattle { get; set; }
    public Dictionary<string, int> AbilityUses { get; set; } = [];
    public string StrongholdId { get; set; } = "";
    public List<string> DynastyDeck { get; set; } = [];
    public List<string> FateDeck { get; set; } = [];
    public List<string> Hand { get; set; } = [];
    public List<ProvinceState> Provinces { get; set; } = [];
    // Brotherhood Schism (issue #453, batch 2): the last turn this player put a Ring into play (its own recruit action), the AssignedToAttackTurn convention.
    public int RingPutIntoPlayTurn { get; set; } = -1;
    // Plans Within Plans (Emperor Edition remaining-cards pass, issue #492, group B): "At your next opportunity to take an Open action, you may instead take a Limited action" -- armed when the card
    // resolves, spent by the player's next command at an opportunity of the Action Phase (whatever he does with it); Sets counts the resolutions, so a copy played at that very opportunity re-arms it.
    public bool PlansWithinPlansArmed { get; set; }
    public int PlansWithinPlansSets { get; set; }
}

public sealed record PendingChoice(string Type, int PlayerId, string? CardId, string Prompt, int? ActingPlayerId = null);

/// <summary>The Honor loss Ten Thousand Temples may negate, waiting for its controller's answer. <paramref name="Command"/> is the command that was being executed (the whole of it is replayed once every
/// decision is made); Decisions are the answers so far (true: the Temple was destroyed to negate that loss), one per earlier loss of that command that the Temple could have negated; the Prior* fields are the
/// pending choice, priority and pass count the state had before this decision took them over.</summary>
/// <summary>Emperor Edition mechanics group C (issue #501, EmperorMechC.cs): a command whose execution reached an additional cost of entering play (En'you) with more than one way to pay, held whole
/// while the entering card's controller chooses (MatchEngine.Execute replays it with the answer, kept on MatchState.EntryCostAnswers).</summary>
public sealed class PendingEntryCostDecision
{
    public GameCommand Command { get; set; } = new("pass", 0, 0);
    public int PlayerId { get; set; }
    public string CardId { get; set; } = "";
    public List<EntryCostOption> Options { get; set; } = [];
    public PendingChoice? PriorChoice { get; set; }
    public int PriorPriority { get; set; }
    public int PriorPasses { get; set; }
    public List<bool> HonorDecisions { get; set; } = [];
    public int HonorWindowsDone { get; set; }
}

/// <summary>One way to pay an additional cost of entering play: Key is "remove:&lt;hand card id&gt;" or "sake:&lt;token id&gt;".</summary>
public sealed record EntryCostOption(string Key, string Label);

public sealed class PendingHonorLossDecision
{
    public GameCommand Command { get; set; } = new("pass", 0, 0);
    public List<bool> Decisions { get; set; } = [];
    public int PlayerId { get; set; }
    public string TempleId { get; set; } = "";
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
    public PendingChoice? PriorChoice { get; set; }
    public int PriorPriority { get; set; }
    public int PriorPasses { get; set; }
    /// <summary>Honor-change windows (PendingHonorChange) already given to this command: its replay does not open them again (issue #492, R4).</summary>
    public int HonorWindowsDone { get; set; }
}

/// <summary>An Honor gain or loss from an action's effects that is about to be applied and that a Reaction may be played on (Rhetoric, Solid Defense, Shiro Chugo: Mendon's R4 ruling, "may be played
/// on any honor gain or loss"). <paramref name="Command"/> is the command that was being executed when the change was reached; nothing it did is kept, the window is open on the state it started
/// from, and the command is replayed when both players have passed (MatchEngine.Execute, EmperorRulingR4.cs). The Prior* fields are the pending choice, priority and pass count the state had.</summary>
public sealed class PendingHonorChange
{
    public GameCommand Command { get; set; } = new("pass", 0, 0);
    /// <summary>The action whose effects change Honor (EmperorStrongholdBatch2.ActionKey, or the duel's), who took it, and which of its eligible Honor changes (in order) this is.</summary>
    public string ActionKey { get; set; } = "";
    public int ActionPlayerId { get; set; }
    public int Occurrence { get; set; }
    /// <summary>The Honor change itself: whose, how much (negative for a loss) and why.</summary>
    public int PlayerId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>The Honor-change windows given so far, this one included; the Ten Thousand Temples answers the replayed command already has.</summary>
    public int WindowsDone { get; set; }
    public List<bool> TempleDecisions { get; set; } = [];
    public PendingChoice? PriorChoice { get; set; }
    public int PriorPriority { get; set; }
    public int PriorPasses { get; set; }
}
public sealed record GameEvent(long Sequence, string Type, string Message, int? PlayerId = null, string? CardId = null, int? Amount = null);

/// <summary>A Ranged/Melee Attack awaiting resolution, open to reaction (core rules: "players must
/// have an opportunity to react"). Strength is deliberately mutable state, not a one-shot value --
/// either player's Reaction may raise, lower, or (by driving it to 0 or below) negate it before
/// Kind/Strength are compared against the target's current Force at actual resolution time.</summary>
public sealed class PendingAttack
{
    // Hida Defenders (issue #503, EmperorP3Windows.cs): the Personalities bowed before the action that declared this Attack began.
    public List<string>? BowedBefore { get; set; }
    public string AbilityId { get; set; } = "";
    public int PlayerId { get; set; }
    // The Personality that made the attack (an attached Item/Weapon's own wielder, when the attack
    // came from one of those rather than a Personality directly) -- populated for every Attack, not
    // just the two cards that originally needed it for a card-specific post-attack effect (Tsuruchi
    // Isas' "Move Isas home"; False Ring of Water's own discard-from-hand case). Also the performer
    // Protection (issue #212) checks before letting a Reaction negate this Attack's own strength.
    public string? SourceId { get; set; }
    public string? TargetId { get; set; }
    public int Strength { get; set; }
    public string Kind { get; set; } = "";
    // Was this specific Attack itself taken as a consumed Additional Action grant (issue #181)? If so,
    // ResolveAttack's own NextAction call must know to keep priority with the attacker (grants
    // permitting), the same as any other Battle ability resolved as an additional action -- the
    // immediate (non-deferred) resolution path already gets this for free via ExecuteAbilityEffects'
    // own tail, so only the deferred/reaction-window path needs this carried across.
    public bool Additional { get; set; }
    // The declaring command, so targeted-by-action reactions can re-run the Attack's target validation; kept in step with TargetId on a redirect. Its CardId is the
    // card the ability is printed on (not always the performer: an Item, Spell, Follower or hand Strategy performs through a Personality), which is where the
    // inherited keywords of "was that a [keyword] action?" are read.
    public GameCommand? Command { get; set; }
    // Mendon's R4 ruling on Deception Revealed (issue #492): the Rings bowed before the action that declared this Attack began.
    public List<string>? RingsBowedBefore { get; set; }
    // The whole Attack was negated by a reaction (Yasuki Jinn-Kuen): it resolves without effect.
    public bool Negated { get; set; }
}

/// <summary>Conqueror (core rules Glossary): open only when the attacker won and more than one of
/// their attacking Personalities carries the Conqueror keyword -- a real choice for the attacker.
/// (Exactly one candidate resolves immediately, with no pending state, since there is nothing to
/// choose.) ProvinceIndex is enough to recompute the untouched attacking army once the choice lands;
/// nothing about it can change while this is the only legal next command (see ResolveConqueror).</summary>
public sealed class PendingConqueror
{
    public int PlayerId { get; set; }
    public int ProvinceIndex { get; set; }
    public string[] CandidateIds { get; set; } = [];
}

/// <summary>Before This Battle Resolves (issue #272, Encircled Terrain's own printed timing): "occur
/// after all players have passed but before resolution is determined... If there are multiple
/// 'before this battle resolves' effects in play at a battlefield, the player whose turn it is
/// chooses the order." Opened by ResolveBattle itself, before Army()/ArmyForces are ever computed --
/// deliberately BEFORE the battle's own participants are read, since Encircled Terrain's own effect
/// (sending every other Personality home) changes who actually fights. ONE set, ordered by the active
/// player (rules lead: there is no alternating reaction round): a card's trait (its own card id), a
/// delayed effect a Battle action registered ("delayed:N", DelayedBattleEffect) and a printed
/// "Reaction: Before a battle resolves" ("reaction:CARD:ABILITY", optional, its owner decides). RemainingSourceIds
/// holds every not-yet-resolved item's id, still to be ordered by the active player (via the
/// "beforeBattleOrder" PendingChoice) whenever more than one remains; ActiveSourceId is whichever one
/// is currently resolving; ActiveItemSpent is set once its owner has used or declined a Reaction item. The Encircled* fields are that one source's own sub-stage -- a future
/// second "before this battle resolves" card with a different shape gets its own such fields, never
/// reusing these by coincidence of type.</summary>
public sealed class PendingBeforeBattleResolve
{
    public int ProvinceIndex { get; set; }
    public int Attacker { get; set; }
    public int Defender { get; set; }
    public List<string> RemainingSourceIds { get; set; } = [];
    public string? ActiveSourceId { get; set; }
    public bool ActiveItemSpent { get; set; }
    // Encircled Terrain's own sub-stage while ActiveSourceId is its id: "ChooseOrder" (its own
    // controller picks who selects first) -> "FirstSelect" -> "SecondSelect" (each side chooses one
    // Personality of their own at this battlefield to keep) -> resolved (every other Personality here
    // sent home).
    public string? EncircledStage { get; set; }
    public string? EncircledFirstSelectedId { get; set; }
}

/// <summary>An effect a Battle action registered for the moment its battle is about to resolve ("Before this battle resolves, bow
/// him"): Kind "bow", "straighten", "modifier" (Stat, Amount: "give him -3C") or "destroy". It joins the one ordered set of
/// before-resolution effects (PendingBeforeBattleResolve) and lapses with its battle, applied or not (FinishBattleResolution).
/// PlayerId is who registered it, SourceId the card whose action did.</summary>
public sealed record DelayedBattleEffect(int Id, string Kind, int PlayerId, string? SourceId, string TargetId, string? Stat = null, int Amount = 0, string? ThenMoveId = null);

/// <summary>One claim on "the first opportunity to take a Battle action or pass" in a battle (Battle Phase
/// spec 4.3/4.4; issue #310). PlayerId is the BENEFICIARY -- who the card says gets the opportunity, not
/// necessarily who played it (Sneak Attack names "the Attacker" even if the Defender were to play it).
/// Kind is "Naval" (the Naval Invasion Reaction) or "Engage" (any other grant, e.g. Daidoji Yuki);
/// SourceId is the card that made the claim, when there is one.</summary>
public sealed record FirstOpportunityClaim(int PlayerId, string Kind, string? SourceId);

/// <summary>The Engage window (Battle Phase spec 4.2; issue #310): open from the moment the Attacker selects
/// a battlefield until both players pass in succession, while "Engage:" actions and "After engaging"
/// Reactions are played. Claims accumulate here in the order made and are resolved ONCE, when the window
/// closes (MatchEngine.ResolveFirstOpportunity) -- the Defender's claim always wins whatever the order,
/// otherwise the Attacker's, and any number of claims by one player are the same single opportunity.
/// NavalInvasionUsed enforces the printed "once per battle"; NavalPerformerId is the Personality that
/// Reaction's "which he must perform" binds the Attacker's first action to.</summary>
public sealed class PendingEngage
{
    public int ProvinceIndex { get; set; }
    public int Attacker { get; set; }
    public int Defender { get; set; }
    public List<FirstOpportunityClaim> Claims { get; set; } = [];
    public bool NavalInvasionUsed { get; set; }
    public string? NavalPerformerId { get; set; }
    // Jade's Roar - exp (Gempukku pass, issue #461): "You have the first opportunity to take a Battle action in this battle, which must be a Terrain action." The keyword the
    // claimant's first action must carry, and who claimed it (the restriction applies only if that player actually ends up holding the first opportunity).
    public string? FirstActionKeyword { get; set; }
    public int? FirstActionKeywordPlayerId { get; set; }
    // Shosuro Toson - exp2 (issue #503, EmperorP3.cs): "you have the first opportunity to take a Battle action in this battle, which must be performed by Toson" -- the performer and who claimed it.
    public string? FirstActionPerformerId { get; set; }
    public int? FirstActionPerformerPlayerId { get; set; }
}

/// <summary>A limit on the FIRST Battle action a player takes in a battle, set when the Engage window closes
/// and consumed by that player's next accepted command: Naval Invasion's "which he must perform"
/// (PerformerId), or Rebuilding Kyuden Hida's "the Attacker may not announce a Battle action before the
/// Defender's first normal opportunity to act or pass" (MustPass).</summary>
public sealed record FirstActionRestriction(int PlayerId, string? PerformerId, bool MustPass, string? ActionKeyword = null, string? Reason = null); // Reason: the card naming the performer (issue #503; Naval Invasion when null)

/// <summary>An Additional Action grant (core rules Glossary; issue #181): one Battle action the
/// named player may take out of the normal back-and-forth alternation, without ceding priority to
/// their opponent in between. ScopeCardIds null means any of the player's own cards (Again!'s
/// shape); a specific set restricts it to those cards only (Two Hands' "from cards in his unit").
/// Consumed one at a time by NextAction/ConsumeAdditionalActionGrant; battle-scoped -- cleared at
/// FinishBattleResolution, never carried into a later battle or turn. FromDiscard: the one action is the play of the
/// single scoped card FROM A DISCARD PILE (Daigotsu Bofana - exp: "take an additional action to play it ... After it
/// resolves, remove the card from the game"); it makes that card's Battle actions available, and consumed, like any
/// grant.</summary>
///  <param name="Battle">The battle the grant belongs to (MatchState.BattleNumber), or null for one that needs no battle. A Ring's recruit grant
/// (Mendon's ruling, issue #379: "A recruit grant at one battle is limited to that battle") is usable only at its own battle and lapses when that
/// battle is over: made during a battle it belongs to that battle, made between battles (the Action phase) it belongs to the NEXT one.</param>
///  <param name="AnyDiscardTiming">FromDiscard only. False (Daigotsu Bofana - exp) makes only the scoped card's Battle-timed abilities playable, the
/// default and only shape until issue #449. True (Akodo Tezuka: "play it for one of its Battle, Limited, or Open actions, as appropriate to the
/// current phase") also admits Limited and Open -- each still gated by its own ordinary phase/active-player legality, so "as appropriate to the
/// current phase" needs no extra check of its own.</param>
///  <param name="RangedTargetId">Emperor Edition Stronghold pass (issue #486), batch 1 (Koshin Keep: "an additional Ranged Attack Battle action that may only target the same card"): when set, the
/// grant is used only by a Ranged Attack that targets that card, and while the player holds it nothing else may be taken (EmperorStrongholdBatch1.cs).</param>
// PerformerId (Emperor Edition remaining-cards pass, group C, issue #492: Spinning Heel Kick): the additional action "which he must perform" -- spent only by an action that Personality performs.
// BaseAbilityOnly (Emperor Edition three-scans pass, issue #496: Honor Never Falls -- "an additional Battle action to use one of his base abilities"): with a PerformerId, the grant is spent only by one of the abilities PRINTED on that Personality
// (his own definition's abilities, not one an attachment provides or one granted to him).
// Issue #501 (EmperorMechA.cs): RequiredActionKeyword -- the action spending the grant must carry that keyword (Mirumoto Inokuchi: "which must be Iaijutsu"; Beyond the Line: a Recon ability);
// Tag -- a grant with a rule of its own (Beyond the Line: a Limited or Open Recon ability used as a Battle action that targets the current battlefield's province); Id -- a unique name, so a grant can be
// followed after it leaves the list (Inokuchi's "If you don't, bow him") and is never equal to another grant; Mandatory -- the text does not say "may" (Beyond the Line: "Take an additional action"),
// so the grant may be taken as a pass but not declined (Mendon's item 2).
public sealed record AdditionalActionGrant(int PlayerId, string[]? ScopeCardIds, bool FromDiscard = false, int? Battle = null, bool AnyDiscardTiming = false, string? RangedTargetId = null, string? PerformerId = null, bool BaseAbilityOnly = false,
    string? RequiredActionKeyword = null, string? Tag = null, string? Id = null, bool Mandatory = false,
    // Primitive P17 (issue #721, AdditionalActionScope.cs) -- appended LAST, passed by name: the grant is spent only by a command that uses one of these abilities (Mirumoto Kalen - exp's second printed ability).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? ScopeAbilityIds = null);

/// <summary>A discard a player owes: they choose the card from their hand (Times of Strife, Daigotsu Bofana - exp's entry).
/// Reason names the card or rule in the prompt and the event.</summary>
public sealed record ForcedDiscard(int PlayerId, string Reason);

/// <summary>Force redirection (issue #216, Togashi Noboru): TargetId's own Force (not its unit's
/// Followers) contributes to ToPlayerId's own army total instead of the target's own controller's,
/// while ToPlayerId still has at least one unit in their own current army -- re-checked live at
/// resolution, not a one-time snapshot.</summary>
public sealed record ForceRedirect(string TargetId, int ToPlayerId);

/// <summary>Tamori Tamoko's own Open action (issue #207 follow-up): "gain 1 Honor if [TargetId]
/// was not assigned to attack your provinces this turn" -- BeneficiaryPlayerId is Tamoko's own
/// controller (the one who gains the Honor), not the targeted Personality's own controller.</summary>
public sealed record TamokoHonorCheck(string TargetId, int BeneficiaryPlayerId);

/// <summary>Lady Matsu's Rage (the first Celestial): "Bow the target. After the next time (this
/// turn) it straightens, destroy it." Checked reactively wherever Straighten() actually flips
/// Bowed to false, not polled -- consumed (removed) the instant it fires, or discarded unconsumed at
/// EndTurn if the target never straightens this same turn. ActingPlayerId is whoever performed the
/// original Battle action (Lady Matsu's Rage's own controller at the time), carried through to the
/// delayed destroy so her own destroy-redirect trait still attributes correctly even though she has
/// already been discarded as this ability's own cost by the time it fires.</summary>
public sealed record LadyMatsusRageWatch(string TargetId, int ActingPlayerId);

/// <summary>Yasuki Tsujiken (oracle-l5r-9469): one mark placed by his own "After Tsujiken enters
/// play, target a Holding" Reaction. See MatchState.TsujikenWatches for the full shape/lifecycle.</summary>
public sealed record TsujikenWatch(string TsujikenId, string HoldingId);

/// <summary>Fields of Mercy (oracle-l5r-2530): the per-player destroyed-Personality counter for one
/// specific in-play copy of the card. See MatchState.FieldsOfMercyWatches for the full lifecycle.</summary>
public sealed class FieldsOfMercyWatch
{
    public string CardId { get; set; } = "";
    public int[] DestroyedCount { get; set; } = new int[2];
}

/// <summary>Dueling (core rules; issue #204): a mini-game that pauses whatever the outer game was
/// doing (a duel-issuing ability's own execution sets this up and returns without ceding priority,
/// same shape as PendingAttack) until it resolves, then hands priority back exactly where the
/// pausing ability's own NextAction call would have. Stage drives which commands are legal next:
/// "AwaitingAccept" (Refusable only) -> "Reaction" (a real PendingChoice("duelReaction",...) window,
/// same machinery as attackReaction) -> "Focusing" (challenged goes first; either side may Strike,
/// ending it for both; a player who has focused four times must Strike on their turn) ->
/// "DuelistRefocus" (each Duelist-controlling side, in DuelistRefocusOrder, may discard one focused
/// card and focus once more) -> "OrderingEffects" (the OUTER game's ActivePlayerId orders any
/// Focus-Effect-bearing focused cards) -> resolved (PendingDuel set back to null).</summary>
public sealed class PendingDuel
{
    // Hida Defenders (issue #503, EmperorP3Windows.cs): the Personalities bowed before the action that issued this duel began.
    public List<string>? BowedBefore { get; set; }
    // Mendon's R4 ruling on Deception Revealed (issue #492): the Rings that were bowed before the action that issued this duel began; a Ring bowed since is one the action bowed, a Focus Effect's bows excepted.
    public List<string>? RingsBowedBefore { get; set; }
    // Kakita Ichigiku (The Shadow's Embrace pass, issue #476 batch 1): he challenged a dishonorable Personality during a Combat Segment, so the loser is destroyed too.
    public bool IchigikuDestroysLoser { get; set; }
    // The shared delay mechanism (issue #492, EmperorRulingR2.cs): the Focus Effects "you may choose (now) to delay the effects of losing this duel until ..." are answered before the duel's outcome
    // applies -- DelayDecisionIndex is how far along the ordered Focus Effects those answers are, and DelayedLossPlayerIds the players whose Personalities' losing effects were delayed (with the trigger).
    public int DelayDecisionIndex { get; set; }
    public List<DelayedLossChoice> DelayedLosses { get; set; } = [];
    // Negations a Reaction set on the issuing action (To First Blood's "negate his destruction from the action's effects"): the duel's own outcome is one of that action's effects.
    public List<NegatedEffect> NegatedEffects { get; set; } = [];
    public string AbilityId { get; set; } = "";
    public string DuelId { get; set; } = "";
    // The card the challenge-issuing ability is printed on (an Attack keeps it in PendingAttack.Command.CardId); null for a player ability.
    public string? SourceCardId { get; set; }
    // Duel presentation: the retained result assembled by ResolveDuel and completed by FinishDuelResolution.
    public DuelResult? ResultDraft { get; set; }
    public int ResultEventStart { get; set; }
    // The player who issued the challenge -- NextAction is called with this once the duel fully
    // resolves, so priority returns exactly where it would have without the interjecting duel.
    public int ChallengerPlayerId { get; set; }
    public int ChallengedPlayerId { get; set; }
    public string ChallengerId { get; set; } = "";
    public string ChallengedId { get; set; } = "";
    // "Chi" (default), "Force", or "PersonalHonor" -- the ability's own printed duel stat, or Chi if
    // none is named.
    public string Stat { get; set; } = "Chi";
    public bool Refusable { get; set; }
    // P18 (DuelRules.cs, issue #698): the rules this duel carries (focus counts, first to focus, item bonuses, automatic losses, the cost of refusing); null -- and not saved -- for an ordinary duel.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DuelRules? Rules { get; set; }
    public string Stage { get; set; } = "AwaitingAccept";
    // Whose turn to Focus or Strike, alternating, starting with ChallengedPlayerId.
    public int TurnPlayerId { get; set; }
    public bool Struck { get; set; }
    // Bend like a Reed (Emperor Edition remaining-cards pass, group A, issue #492): the player who struck, whether a copy was played this duel, and the "+N to his duel stat until the duel ends" per Personality.
    public int? StrikerPlayerId { get; set; }
    public bool BendUsed { get; set; }
    public Dictionary<string, int> StatBonuses { get; set; } = [];
    public List<string> ChallengerFocused { get; set; } = [];
    public List<string> ChallengedFocused { get; set; } = [];
    // Player ids still owed a Duelist discard-and-refocus decision, in the order they decide (the
    // OUTER game's ActivePlayerId first if both sides have Duelist) -- popped one at a time.
    public List<int> PendingDuelistDecisions { get; set; } = [];
    // The active player's own chosen resolution order for every focused card carrying a
    // Timing=="FocusEffect" ability, set once (by ResolveDuelFocusEffectsInOrder) just before
    // ResolveDuel actually applies them.
    public List<string> OrderedFocusEffects { get; set; } = [];
    // The duel's result, kept while its Focus Effects resolve one by one (a Focus Effect may pause for a decision, FocusEffectChoice.cs).
    public string? WinnerId { get; set; }
    public List<string> LoserIds { get; set; } = [];
    // Mirumoto Hikaro's own second trait: "once per duel, you may focus from your discard pile
    // instead" -- per player (either side could independently control their own Hikaro), and which
    // already-focused card ids came from a discard pile this way (ResolveDuel removes those from
    // the game instead of the ordinary end-of-duel FateDiscard sweep every other focused card gets).
    public bool[] HikaroDiscardFocusUsed { get; set; } = new bool[2];
    public List<string> DiscardFocusedIds { get; set; } = [];
}

public sealed partial class MatchState
{
    public long Version { get; set; }
    public int TokenSequence { get; set; }
    // Kitsu Akai's own Limited action (issue #257): a persistent sequence for minting fresh
    // CardInstance.Id values at runtime, the same replay-determinism reasoning as TokenSequence
    // above -- match setup's own local `nextId` closure only ever runs once, at Create(), and does
    // not survive into gameplay.
    public int CreatedCardSequence { get; set; }
    /// <summary>Last UnitNumber handed out (see CardInstance.UnitNumber).</summary>
    public int UnitSequence { get; set; }
    public int? FavorPlayerId { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int ActivePlayerId { get; set; }
    public int PriorityPlayerId { get; set; }
    public Phase Phase { get; set; }
    public int ConsecutivePasses { get; set; }
    public PendingChoice? PendingChoice { get; set; }
    public PendingAttack? PendingAttack { get; set; }
    public PendingConqueror? PendingConqueror { get; set; }
    public PendingDuel? PendingDuel { get; set; }
    public DuelRecord? LastDuel { get; set; }
    // Deck search (issue #312): see DeckSearch.cs. Non-null exactly while one of the "search*" PendingChoice
    // types is open; it holds the engine-private context (which deck, which card was picked) that the
    // player's own MatchView.PendingSearch is derived from, and is never sent as-is to anyone.
    public PendingSearch? PendingSearch { get; set; }
    // Look at the top N cards (issue #322): see DeckLook.cs. Non-null exactly while a "look*" PendingChoice is open; the
    // player's own MatchView.PendingLook is derived from it and nobody else sees anything of it.
    public PendingLook? PendingLook { get; set; }
    // Conditional effect choice (issue #337): see EffectChoice.cs. Non-null exactly while a "effectChoice" PendingChoice is open; it holds the
    // resolving action (source, target, performer, the negations a reaction set) the chosen option's effects are then applied to.
    public PendingEffectChoice? PendingEffectChoice { get; set; }
    // Deck search (issue #312): the append-only, PUBLIC record of every card a player has shown the other
    // player (a search shows what it found). Exposed to both players as MatchView.ShownCards.
    public List<ShownCard> ShownCards { get; set; } = [];
    // Deck search (issue #312): the persisted shuffle generator (SplitMix64, see MatchEngine.StableRandom).
    // Setup continues the very sequence the seed started (so every existing setup is byte-for-byte
    // unchanged), and every mid-game Shuffle reads and advances this one value. It is engine-private: it
    // is part of MatchState only so State/Copy()/Execute rollback carry it, and it appears in no MatchView.
    public ulong RngState { get; set; }
    // Events Phase reveal cursor: the index of the next province ContinueEventsReveal should reveal. A
    // reveal that defers to a player decision (Harsh Choices, Imperial Gift's search) resumes from here,
    // so the card that province refilled with is not revealed in the same phase it was placed.
    public int EventsRevealNext { get; set; }
    // Plains of Otosan Uchi (oracle-l5r-5979, issue #554, OtosanUchiPlains.cs): a two-slot province's own reveal
    // sequence in progress -- the province index (of the ACTIVE Events-Phase player's own row), the remaining slots
    // still to reveal+resolve in the controller's chosen order (0 = CardId, 1 = CardId2), and whether CardId/CardId2
    // are currently swapped so the ordinary single-slot ResolveEvent/ResolveRegion machinery can run unmodified
    // against slot 2. All three reset once the sequence finishes; null/empty/false otherwise.
    public int? OtosanUchiProvinceIndex { get; set; }
    public List<int> OtosanUchiRevealQueue { get; set; } = [];
    public bool OtosanUchiSwapped { get; set; }
    // A province-card-targeting effect (Shosuro Toson's Reaction) paused on a two-candidate province (issue #554); null otherwise.
    public PendingProvinceCardChoice? PendingProvinceCardChoice { get; set; }
    public int? CurrentBattleProvince { get; set; }
    public List<int> UnresolvedBattlefields { get; set; } = [];
    // Before This Battle Resolves (issue #272): see PendingBeforeBattleResolve's own doc comment.
    // Non-null only while ResolveBattle's own before-resolution sequence is still being sorted out
    // (ordering choices, Encircled Terrain's own selection sub-stages); cleared before the battle's
    // actual winner/casualties are ever computed.
    public PendingBeforeBattleResolve? PendingBeforeBattleResolve { get; set; }
    // Effects registered by Battle actions for THIS battle's before-resolution set (DelayedBattleEffect); cleared when the battle ends.
    public List<DelayedBattleEffect> DelayedBattleEffects { get; set; } = [];
    public int NextDelayedEffectId { get; set; }
    // "That battle's resolution will not bow cards in your army" (Akodo Dairuko): the players whose army the resolution does not bow;
    // cleared when the battle ends.
    public List<int> ResolutionWillNotBowPlayerIds { get; set; } = [];
    // Cards whose unit will not bow from this battle's resolution (Redoubled Attack, issue #417); lapses with the battle like the player-wide list above.
    public List<string> ResolutionWillNotBowCardIds { get; set; } = [];
    // What the battle's resolution did, held while the after-resolution window is open (AfterResolution.cs).
    public PendingAfterResolution? PendingAfterResolution { get; set; }
    // Emperor Edition remaining-cards pass (issue #492), group G: Bandit Gang's after-resolution trait in progress (EmperorRemainingG.cs), and the Personalities the assignment command just placed (Chrysanthemum Blossom).
    public PendingBanditGangs? PendingBanditGangs { get; set; }
    public List<string> AssignedThisCommand { get; set; } = [];
    // Battle Phase timing (issue #310). PendingEngage is non-null exactly while the Engage window is open
    // (PendingChoice "engageReaction"). FirstActionPlayerId/FirstActionReason record WHO holds the first
    // opportunity in the current battle and WHY ("Default", "Naval", "EngageGrant" or "DefenderOverride"),
    // set the instant the battle starts (Default) and again when the Engage window resolves its claims;
    // cleared when the battle resolves. FirstActionRestriction is the one-shot limit on the holder's first
    // action (see FirstActionRestriction). All three are plain data so a replay reproduces them exactly.
    public PendingEngage? PendingEngage { get; set; }
    public int? FirstActionPlayerId { get; set; }
    public string? FirstActionReason { get; set; }
    public FirstActionRestriction? FirstActionRestriction { get; set; }
    // After This Battle Resolves (issue #272, Looting the Ruins' own printed timing): "after the
    // resolution of the current battle but before the next battlefield is chosen." FinishBattleResolution
    // sets this to the province that just resolved (its own provinceIndex parameter, otherwise lost --
    // CurrentBattleProvince is already cleared by the time this hook fires, and the Overconfident
    // draw window may defer the whole tail across a command boundary first, same reasoning
    // PendingConqueror.ProvinceIndex already documents). FinishBattleReturnHomeAndTransition reads and
    // clears it once its own after-resolve effects have run.
    public int? ResolvedBattleProvinceIndex { get; set; }
    // Your Clan Needs You! (oracle-l5r-9687, issue #295): "After the battle ends, if he is in play,
    // discard all his attachments and put him into one of your provinces, face-up, discarding any
    // card there" -- the recruited Personality's own id, watched from the moment he enters play via
    // this ability until this same battle's own resolution checks it
    // (MatchEngine.CheckYourClanNeedsYouReturn), then removed from this list regardless of outcome.
    // A List (not a single nullable id), since more than one copy could resolve within one battle.
    public List<string> YourClanNeedsYouWatchIds { get; set; } = [];
    /// <summary>Seeking the Truth (Forgotten Legacy pass, issue #467 batch 4): the players who owe a random discard after the battle they played it in ends, unless they destroyed its province or an army.</summary>
    public List<SeekingTheTruthWatch> SeekingTheTruthWatches { get; set; } = [];
    // The Shadow's Embrace pass (issue #476), batch 3 (ShadowsEmbraceBatch3.cs): Cowed and Defeated's watches, Methodical Questioning's end-of-turn return to hand and
    // Forthrightness's watches.
    public List<CowedAndDefeatedWatch> CowedAndDefeatedWatches { get; set; } = [];
    public List<ReturnToHandBeforeTurnEnds> ReturnsToHandBeforeTurnEnds { get; set; } = [];
    public List<ForthrightnessWatch> ForthrightnessWatches { get; set; } = [];
    // Cards an action created "until the battle ends" (Kyuden Hida - exp's Scout Followers): removed from the game when their battle is over, wherever they are.
    public List<string> CreatedUntilBattleEndsIds { get; set; } = [];
    // Ashigaru Elite's shape: a per-player counter of the current battle's Force boost to each of the
    // performer's own Ashigaru-keyword Personalities/Followers at this battlefield, incremented once
    // per use (stacks, tireless). Reset to zero whenever a battle resolves (FinishBattleResolution) --
    // "for the remainder of the battle," not the turn -- since a turn can contain more than one battle.
    public int[] AshigaruBoost { get; set; } = new int[2];
    // Village Guardian (oracle-l5r-9162, issue #285): "Draw one card for each player who has had a
    // province destroyed this game" -- a game-wide, per-player, match-lifetime flag, set once and
    // never cleared (unlike AshigaruBoost above, which resets every battle). Indexed by player id,
    // the same shape as AshigaruBoost/DestroyedCount, set at DestroyProvince's own single choke
    // point. Deliberately NOT FieldsOfMercyWatches' shape (a List of per-Terrain-instance, per-player
    // Personality-destruction counters) -- this is a plain per-player boolean, scoped to the whole
    // match rather than any one card instance, and to Province destruction rather than Personality
    // destruction.
    public bool[] ProvinceEverDestroyed { get; set; } = new bool[2];
    // Ohsuki's Sparrow (oracle-l5r-10023, issue #299): "If none of your provinces were destroyed since
    // your last turn ended" -- unlike ProvinceEverDestroyed above (match-lifetime, never cleared), this
    // one is per-player and WINDOWED: set at the same single DestroyProvince choke point, but cleared
    // for a player at the end of THAT player's own turn (FinishTurn), so it reads true exactly when one
    // of their provinces was destroyed at any point since their previous turn ended -- most commonly
    // during the opponent's turn, but also by anything that destroys it during their own. Reset at
    // turn END, not StartTurn: clearing at the start of a turn would erase a destruction that happened
    // on the opponent's turn just before it, which is precisely the case the printed text asks about.
    // Never true before any province has been destroyed, so a fresh game starts with it usable.
    public bool[] ProvinceDestroyedSinceTurnEnd { get; set; } = new bool[2];
    // Additional Action (issue #181): every currently-available grant across both players, and a
    // command snapshot awaiting the "additionalActionReaction" window (Effortless Counterattack's
    // shape) before it actually resolves. Two Hands' own restriction ("other actions will not give
    // you NEW additional Battle actions this battle") is a separate flag -- it blocks future GRANTS,
    // it does not touch grants already sitting in AdditionalActions. All battle-scoped: cleared at
    // FinishBattleResolution, never carried into a later battle or turn.
    public List<AdditionalActionGrant> AdditionalActions { get; set; } = [];
    // Which battle of the game is being fought (issue #379): incremented when a battle is selected, so it names the battle in progress from its Engage
    // window through its aftermath, and stays that battle's number until the next one is selected. What a battle-scoped grant is compared against.
    public int BattleNumber { get; set; }
    public GameCommand? PendingAdditionalAction { get; set; }
    public bool NoNewAdditionalActionsThisBattle { get; set; }
    /// <summary>The name of the card that set NoNewAdditionalActionsThisBattle (Two Hands), for the "barred" notice.</summary>
    public string? NoNewAdditionalActionsBy { get; set; }
    // Attack-vs-Followers exceptions granted by another card (AttackFollowerException shape (e), Yoritomo
    // Ebumi): standing, one-shot permissions for a player's NEXT Attack action of the covered kind. Battle-
    // scoped like AdditionalActions -- consumed when that Attack executes, cleared at FinishBattleResolution.
    public List<AttackTargetGrant> AttackTargetGrants { get; set; } = [];
    // "Before an action resolves" reactions (Mirumoto Rokai's destruction-negation, Tamori Tamoko's
    // movement-or-straightening-negation, Champion of Thunder, Brothers of Goemon, Akodo Kakihara's Force
    // bonus): the whole triggering command is stashed here (never partially applied) while an alternating
    // "beforeActionReaction" window lets EITHER player use any number of applicable reactions -- the active
    // player first, then the other, alternating until both pass consecutively (issue #318) -- before
    // re-invoking ExecuteAbilityEffects on the SAME command. NegatedEffects is the negation signal those
    // reactions set: one entry per negation played, so several of different kinds compose on one command
    // (Champion's "all" on one card, Rokai's "destroy" on another, Brothers' "bow"). An entry is consumed --
    // read then removed -- by whichever of MoveHome/Straighten/DestroyUnits/StalwartNegatesBow actually runs
    // first and matches it; "all" is never consumed (see NegatesEffect), and the whole list is wiped right
    // after the re-invoked command returns.
    public GameCommand? PendingNegatableAbility { get; set; }
    // A multi-target action opens one before-action window per target that has a possible reactor, in slot order (issue #336, TargetWindows.cs):
    // the target the open window is for, and the targets still to come. Both empty for a single-target action and once the action resolves.
    public string? ReactionWindowTargetId { get; set; }
    public List<string> ReactionWindowRemaining { get; set; } = [];
    public List<NegatedEffect> NegatedEffects { get; set; } = [];
    // Windows set aside behind a nested one, most recent last (SuspendedWindow; empty outside a Reaction's own follow-on windows).
    public List<SuspendedWindow> SuspendedWindows { get; set; } = [];
    // Force redirection (issue #216, Togashi Noboru): "the target's Force contributes to [Noboru's
    // controller's] army instead of his own controller's" -- battle-scoped ("for the duration of the
    // battle," core rules' own default for an in-battle condition), so cleared at
    // FinishBattleResolution like AdditionalActions above, never carried into a later battle. Live
    // re-checked at ACTUAL resolution time (ResolveBattle), not baked in when granted -- "while you
    // have at least one unit in your current army" is a continuously-checked condition, not a
    // snapshot: only the target Personality's own Force moves (never its Followers', which always
    // stay with their own controller, since a Follower's owner is always its host's own owner).
    public List<ForceRedirect> ForceRedirects { get; set; } = [];
    // Brash (core rules keyword, issue #216): "the Defender may draw a card after a Brash card is
    // assigned to attack" -- once independently per Brash card in ONE attack declaration, so this is
    // a plain countdown (every offer is identical: draw or don't), not per-card state. Non-zero only
    // while PendingChoice.Type=="brashDraw" is open; the deferred phase transition (which of
    // DefenseAssignment/CavalryDefense AssignAttackers would otherwise have moved to) resumes once it
    // reaches zero -- read off _state.Phase itself (still InfantryDeclaration/CavalryDeclaration until
    // then), so no separate field is needed to remember it.
    public int PendingBrashDraws { get; set; }
    // Bow reaction (issue #216, Togashi Hizumi): while PendingChoice.Type=="bowReaction" is open on
    // Hizumi's own behalf, the specific newly-bowed personality/personalities at his own battlefield
    // this action just bowed -- his own Target category checks membership here rather than
    // re-deriving "was this bowed by that specific action" after the fact (nothing else changes a
    // card's own Bowed status in between). Unused, and safe to leave stale, for Tamori Ruya's own
    // window (a different, unrelated Target shape -- see PersonalityAtSourceLocation).
    public List<string> BowReactionCandidateIds { get; set; } = [];
    // Overconfident (core rules keyword, issue #223): "each other player may draw a card after an
    // Overconfident card enters or leaves play" -- both triggers just queue the ONE beneficiary
    // player id here (QueueOverconfidentDraw), drained one at a time (OpenOverconfidentDrawWindow)
    // at every point the engine is about to hand priority back normally, whether that is because no
    // other reaction window ever opened, or because one that did just closed (see PassReaction).
    public List<int> PendingOverconfidentDraws { get; set; } = [];
    // Times of Strife (issue #259): "another player who controls no units at the current battlefield
    // announces a Battle action" -- a queue of discards owed, the same drain-one-at-a-time shape
    // PendingOverconfidentDraws above already uses (see OpenOverconfidentDrawWindow, which drains this queue
    // first), so it composes for free with every existing deferred-window call site instead of needing its own.
    // Generalized (issue #333) from a list of player ids to (player, reason): Daigotsu Bofana - exp's entry queues
    // the same forced discard for each other player.
    public List<ForcedDiscard> PendingForcedDiscards { get; set; } = [];
    // Daigotsu Bofana (issue #333): a forced discard opened at the moment an action is announced (a trait resolves
    // before any Reaction to the same trigger) holds the announced command here until the discard is answered, when
    // UseAbility carries on with it exactly where it stopped.
    public GameCommand? ForcedDiscardContinuation { get; set; }
    // Where the held action resumes once the discard is answered: null = announcement (UseAbility carries on), "action" = a deferred action whose window
    // has closed, "attack" = a pending Attack whose window has closed (issue #335).
    public string? ForcedDiscardResume { get; set; }
    // The Bofanas the action being announced or resolved has already been judged for (the announce-time trait fires once per action per Bofana).
    public List<string> AnnounceTraitEvaluated { get; set; } = [];
    // The card whose mandatory "after this enters play" effects wait for its open Proclaim window (see ApplyPendingEntryEffects).
    public string? PendingEntryEffectsCardId { get; set; }
    // War Weariness (issue #259): "the first action each phase that one or more Personalities
    // performed" -- cleared only when Action/AttackDeclaration/Dynasty is entered (see SetPhase),
    // never by every individual Phase.Battle re-entry (once per battlefield) -- the user's own
    // clarification is that the whole Attack-Declaration-through-battle-resolution sequence counts as
    // one "battle phase" for this purpose, not each battlefield separately.
    public bool WarWearinessBowedThisPhase { get; set; }
    // Stormless Fury (issue #276 follow-up): the general "after a Battle action resolves" primitive.
    // The just-resolved Battle action's own performer, valid only while a "battleActionReaction"
    // window is open -- stormless-fury-bow's own implicit Target (no separate selection needed, since
    // there is only ever one performer to name).
    public string? BattleActionReactionPerformerId { get; set; }
    // Which Stormless-Fury-shaped card ids already used their own out-of-play "attach as Reaction"
    // half (stormless-fury-attach) against the CURRENTLY open battleActionReaction window's own
    // action -- the same card's own in-play "bow" half (stormless-fury-bow) may not also fire against
    // that same action (the card owner's own errata ruling). Overwritten (cleared) every time a fresh
    // battleActionReaction window opens for a new action, so it never leaks into a later, unrelated
    // one -- the small transient piece of state the primitive needs, nothing more.
    public List<string> BattleActionReaction1UsedCardIds { get; set; } = [];
    // Tsuruchi Yakusho (issue #276 follow-up): the general "after you announce an action" primitive.
    // The whole triggering command, stashed here (never partially applied) while an "announceReaction"
    // window offers the one eligible watching card's own controller a chance to react before
    // ExecuteAbilityEffects actually runs -- the same defer-then-reexecute shape PendingNegatableAbility
    // already uses for beforeActionReaction.
    public GameCommand? PendingAnnouncedAction { get; set; }
    // Issue #917 (ResolutionArea.cs): the cards in the resolution area, oldest first (a Reaction played in response sits above the action it answers).
    public List<ResolutionEntry> ResolutionArea { get; set; } = [];
    // Entrenched Position (Forgotten Legacy pass, issue #467 batch 3): set by its Reaction inside an "announceReaction" window; the announced action then resolves negated
    // (ResolveNegatedAction: taken, its costs paid, no effects) when the window closes. Cleared as it is read.
    public bool AnnouncedActionNegated { get; set; }
    // Emperor Edition Stronghold pass (issue #486), batch 2: the action (a "player|ability|card" key, EmperorStrongholdBatch2.ActionKey) whose effects will not be negated or delayed
    // (Suitengu's Torch), and the action whose Honor gain and loss is reduced by 2 (Shiro Chugo). Each is read while that action resolves and replaced by the next action announced.
    public string? UnnegatableActionKey { get; set; }
    public string? HonorReducedActionKey { get; set; }
    // Mendon's R4 ruling (issue #492): Unimpeachable Name played in the window before an Honor loss -- the action whose losses of this player's are reduced by 3 (read while it resolves).
    public string? UnimpeachableActionKey { get; set; }
    public int? UnimpeachablePlayerId { get; set; }
    // Emperor Edition remaining-cards pass, group C (issue #492): the state of Rumors Travel, Rhetoric and Solid Defense (EmperorRemainingC.cs).
    public EmperorRemainingCState RemC { get; set; } = new();
    // Emperor Edition rulings pass R1 (issue #492): battlefields without a province, and a battle fought inside the Action Phase (EmperorRulingR1.cs).
    public RulingR1State R1 { get; set; } = new();
    // Issue #501, mechanics group B (EmperorMechB.cs): control returns owed, the infiltrator straighten trait grants and its open choice.
    public EmperorMechBState MechB { get; set; } = new();
    // Issue #501, mechanics group A (EmperorMechA.cs): additional actions (pass and decline, conditional grants, the per-battle records) and Aramasu's Legacy's Attack Phase inside the Action Phase.
    public MechAState MechA { get; set; } = new();
    // Entrenched Position: the battle (BattleNumber) in which the Attacker holds the first opportunity to act and the Defender has not yet had his first normal
    // opportunity to act or pass -- set when the Engage window closes with the Attacker holding it, cleared the moment the Attacker cedes priority (NextAction, Pass).
    public int AttackerLeadBattle { get; set; } = -1;
    // True for the remainder of the currently-resolving Ranged Attack's own resolution once Tsuruchi
    // Yakusho's Reaction fires -- checked by every existing Attack-strength-reducing Reaction (Fallow
    // Lands, Training's own negate) before it applies, and cleared unconditionally once that specific
    // Attack actually resolves (ResolveAttackNow). A single flag, not a per-attack set, is correct and
    // sufficient here: only one PendingAttack is ever open at a time in this engine.
    public bool RangedAttackStrengthReductionImmune { get; set; }
    // Tamori Tamoko's own Open action (issue #207 follow-up): "target an unbowed Personality
    // controlled by the active player: before the turn ends, gain 1 Honor if he was not assigned to
    // attack your provinces this turn" -- a delayed, mandatory (no further choice) check, queued
    // here and drained unconditionally in EndTurn, the same turn it was queued (Tamoko's own Open
    // Timing is only ever usable "if it is not your turn," so this always resolves before the SAME
    // active player's own turn actually ends).
    public List<TamokoHonorCheck> PendingTamokoHonorChecks { get; set; } = [];
    // Lady Matsu's Rage (the first Celestial): reactive, checked inside Straighten() itself -- see
    // LadyMatsusRageWatch's own doc comment. Cleared unconditionally in EndTurn (same call site as
    // PendingTamokoHonorChecks above) for whichever entries never fired this turn.
    public List<LadyMatsusRageWatch> PendingLadyMatsusRageWatches { get; set; } = [];
    // Spirit of the Berserker (Second City): "Delay his destruction from the action's effects until before this turn ends" -- the Personalities whose destruction a
    // reaction has put off, destroyed for real at the top of EndTurn if they are still in play (DelayedDestruction.cs).
    // Now a read-only view of the shared delay store (EmperorRulingR2.cs, issue #492): the destructions owed until the end of the turn.
    [JsonIgnore]
    public List<string> PendingDelayedDestructions => DelayedEffects.Where(e => e.Kind == DelayedKinds.Destroy && e.Until == DelayUntil.EndOfTurn).Select(e => e.CardId).ToList();
    // The shared delay mechanism (Emperor Edition ruling pass R2, issue #492; EmperorRulingR2.cs): every effect an action (or a duel's outcome) applied to a card that a card delayed, kept in the
    // order it was applied until its trigger point comes (end of battle, end of phase, end of turn, the next Dynasty Phase ...). DelayedEffectSequence numbers them.
    public List<DelayedEffect> DelayedEffects { get; set; } = [];
    public int DelayedEffectSequence { get; set; }
    // He's Mine! (Emperor Edition remaining-cards pass, issue #492, group B): the Samurai who took a Personality's place in a duel and bow once that duel has ended (cleared as the turn ends).
    public List<string> HesMineSubstitutes { get; set; } = [];
    // Keywords and Clan alignments taken off cards until an action or the turn ends (Red Crane Dojo, TemporaryRemovals.cs).
    public List<TemporaryRemoval> TemporaryRemovals { get; set; } = [];
    // Kyuden Kitsune: the battles ("turn:province") whose one negated movement has been used.
    public List<string> KitsuneNegatedBattles { get; set; } = [];
    // Legion of the Bat (issue #279): "after the resolution of an action that moved a target enemy
    // Infantry Personality to this card's battlefield" -- MoveToBattlefield itself (the sole "an
    // action moved a card to a battlefield" primitive; ordinary Attack/Defense assignment goes
    // through Assign() instead, and RecruitReserve is explicitly excluded per the card's own rule)
    // queues the card that just arrived here; OpenMoveReactionWindow (the same "after this action"
    // timing OpenBowReactionWindow already uses) drains it once the whole ability finishes
    // resolving. MoveReactionTargetId is then the one card actually offered by the open window,
    // read by legion-of-bat-bow's own execution (Target "none": the affected card is auto-
    // determined, not separately chosen, the same shape PendingNegatableAbility's own resolution
    // already uses).
    public string? PendingMoveReactionCardId { get; set; }
    public string? MoveReactionTargetId { get; set; }
    // Nikutai (issue #279): "Even if this card is bowed, after a Follower in this unit bows:
    // Straighten it" -- the one sibling Follower actually offered by an open "nikutaiReaction"
    // window, read by nikutai-straighten's own execution (Target "none", same auto-determined shape
    // as MoveReactionTargetId above).
    public string? NikutaiReactionTargetId { get; set; }
    // Unimpeachable Name (oracle-l5r-8989): an out-of-play (hand/discard) Reaction offered before
    // ANY action resolves -- broader than PendingNegatableAbility above (which only ever watches a
    // curated destroy/move/straighten Target on an IN-PLAY card). The whole triggering command is
    // stashed here, same defer-then-execute shape, while a "unimpeachableNameReaction" window offers
    // the opponent a chance to play it (or pass) before the command actually resolves. See
    // CardAbilities.ResolveOrDeferUnimpeachableName / AbilityMayCauseHonorLoss.
    public GameCommand? PendingOutOfPlayActionReaction { get; set; }
    // Mendon's R4 ruling (issue #492): the out-of-play Reaction window (Unimpeachable Name, Bad Kharma, Footsteps of Madness) that FOLLOWS the before-action window of the same action carries that
    // window's negations of effects, and the fact that the action is an additional one (its grant already consumed), to the action's resolution.
    public List<NegatedEffect>? PendingOutOfPlayNegations { get; set; }
    public bool PendingOutOfPlayConsumedGrant { get; set; }
    // Set to the reacting player's own id for the duration of resolving PendingOutOfPlayActionReaction
    // only when Unimpeachable Name was actually played (not merely offered) -- consumed (amount
    // floored toward 0, never past it) by every negative GainHonor call for that same player while
    // active, satisfying "reduce EACH Honor loss from its effects by 3" for a command that causes more
    // than one. Cleared unconditionally once that one command finishes resolving.
    public int? HonorLossReductionPlayerId { get; set; }
    // Yasuki Tsujiken (oracle-l5r-9469): each entry is one "mark" placed by his own entering-play
    // Reaction on a specific Holding -- a list, not a dictionary, since the SAME Holding may be marked
    // more than once (by separate copies of Tsujiken; nothing printed forbids it), and each mark is
    // independently worth 1 Honor lost per future bow. A mark stays here even after its own Tsujiken
    // leaves play (removed instead at that point -- see MatchEngine's own destroy/discard paths) rather
    // than being filtered live by InPlay(), so a client can enumerate exactly which stacked marks are
    // still active without re-deriving it. TsujikenId names the specific CardInstance that placed the
    // mark (Alfredo's own future signifier can key its rendering off this -- see the decision doc).
    public List<TsujikenWatch> TsujikenWatches { get; set; } = [];
    // Fields of Mercy (oracle-l5r-2530): one entry per currently-in-play copy, created when it enters
    // play (never earlier -- "the count of destroyed personalities does not start until Fields of
    // Mercy are played") and removed when it leaves (DiscardTerrain), so a later copy entering play
    // always starts its own count fresh. DestroyedCount is indexed by player id: how many of that
    // player's own Personalities have already been fully destroyed while this specific copy has been
    // in play. See CardAbilities.FieldsOfMercyNegatesPersonalityDestruction.
    public List<FieldsOfMercyWatch> FieldsOfMercyWatches { get; set; } = [];
    // Copying keywords and abilities (issue #338, Fukuzo): non-null exactly while a "copyKeyword"/"copyAbility" PendingChoice is
    // open. Holds the snapshot of what may be copied and the steps still to answer; see CardCopy.cs. The chooser's own
    // MatchView.PendingCopy is derived from it.
    public PendingCopy? PendingCopy { get; set; }
    // The opposing player's Either/Or choice (issue #338, Yoritomo Sakuma, Unfortunate Hesitation): non-null exactly while an
    // "eitherOr" PendingChoice is open. See EitherOr.cs; MatchView.PendingEitherOr is derived from it for BOTH players.
    public PendingEitherOr? PendingEitherOr { get; set; }
    // The opposing player's variable payment (issue #373, Virtuous Charity): non-null exactly while an "opponentPayment" PendingChoice is open. See OpponentPayment.cs.
    public PendingOpponentPayment? PendingOpponentPayment { get; set; }
    // The decision another player makes inside an action (issue #419, DeciderChoice.cs): non-null exactly while a "deciderChoice" PendingChoice is open.
    public PendingDeciderChoice? PendingDeciderChoice { get; set; }
    // A Focus Effect paused for a decision (issue #419, FocusEffectChoice.cs): non-null exactly while a "focusEffectChoice" PendingChoice is open.
    public PendingFocusChoice? PendingFocusChoice { get; set; }
    // The second target of a sequenced action (issue #419, SequencedTarget.cs): non-null exactly while a "sequencedTarget" PendingChoice is open.
    public PendingSequencedTarget? PendingSequencedTarget { get; set; }
    // Whole-action negation (issue #338, Yoritomo Sakuma): set on the deferred command's window while a reaction has negated the
    // action's effects; the window's close then resolves it with everything but its costs skipped (ActionNegation.cs).
    public bool DeferredActionNegated { get; set; }
    // Unfortunate Hesitation: players who "must pass at [their] next opportunity this phase to take a Battle, Limited, or Open
    // action". Each entry is consumed by that player's next pass at such an opportunity, and lapses when its phase ends.
    public List<ForcedPass> ForcedPasses { get; set; } = [];
    public int? WinnerId { get; set; }
    public string? VictoryReason { get; set; }
    public PlayerState[] Players { get; set; } = [];
    public Dictionary<string, CardInstance> Cards { get; set; } = [];
    public List<GameEvent> Events { get; set; } = [];
    // Core rules (Abilities): a single copy of a Reaction -- one source -- may not be taken more than once to the same trigger, even when something lets it be used more than once per turn (issue #1131). Each entry is "card|ability|window event|window type"; an entry of an earlier window is stale and dropped.
    public List<string> ReactionsTakenThisTrigger { get; set; } = [];
    // Action-effect tracking (issue #437, ActionEffectTracking.cs): what the currently-resolving action has affected so far -- appended to by
    // DestroyUnits/DestroyAttachment/MoveHome/MoveByRule as they run, snapshotted into LastResolvedAction and cleared once the action (and any
    // reaction windows of its own) finishes resolving. Never populated by a mass sweep outside a single action (battle resolution's own casualties).
    public List<AffectedCard> CurrentActionAffected { get; set; } = [];
    // Mendon's R4 ruling on Deception Revealed (issue #492): the Rings bowed when the action now resolving began (before it paid anything), copied to the Attack or duel it declares.
    public List<string> CurrentActionRingsBefore { get; set; } = [];
    // The Shadow's Embrace pass (issue #476), batch 6 (ShadowsEmbraceBatch6.cs). On: each player's Honor when the phase in progress began (null until a phase boundary
    // has been crossed), and the Honor each gained from battle resolution since. Star-Crossed Lovers: the pairs that may not share a battlefield, for the rest of the game.
    public int[]? PhaseStartHonor { get; set; }
    public int[] PhaseBattleHonor { get; set; } = new int[2];
    public List<string[]> StarCrossedPairs { get; set; } = [];
    // Emperor Edition Stronghold pass (issue #486), batch 3 (The Otoro Estate: "Look at all face-down cards in one player's provinces"): what a player was shown, private to that viewer.
    public List<ProvinceSight> ProvinceSights { get; set; } = [];
    public ResolvedActionFacts? LastResolvedAction { get; set; }
    // Revenge (oracle-l5r-6302, issue #437): "another player's action has destroyed one of your Personalities since the end of your last Battle
    // action this battle" -- per player, set true whenever another player's action destroys one of theirs (FinishActionEffectTracking), cleared
    // when their own Battle action finishes resolving (the same point) and when a new battle begins (SelectBattle).
    public bool[] EnemyDestroyedOwnPersonalitySinceLastBattleAction { get; set; } = new bool[2];
    // Akodo Kobi (oracle-l5r-232, issue #440 follow-up): "Recruit him... before the current turn ends" -- queued when her Reaction triggers, resolved the next time
    // ANY player's turn ends (MatchEngine.cs's ResolvePendingEndPhaseRecruits, called from EndTurn -- exactly "the next End Phase," whichever player's it is). Via
    // names the source for the entry log, the same role DestroyUnits'/BringDeadPersonalityIntoPlay's own "via" string already plays elsewhere.
    public List<PendingEndPhaseRecruit> PendingEndPhaseRecruits { get; set; } = [];
    // Seeds of Decay pass (issue #453), batch 1. Standing redirects (StandingRedirects.cs): "the next time (this turn / this battle) another player's action would
    // target one of your Personalities/cards, it targets X instead, if legal" (Ceaseless Vigil, Ruthless Onslaught) and Metsubushi's "must target him"; each is spent
    // by the next action that matches it, when that action is announced.
    public List<StandingRedirect> StandingRedirects { get; set; } = [];
    // Grappling the Snake: the units to bow once the action the Monk's Reaction answered has resolved (SeedsOfDecayBatch1.cs).
    public List<PendingGrapplingBow> PendingGrapplingBows { get; set; } = [];
    // Advanced Intelligence: per player, how many actions from a Recon Strategy that player has taken in the current battle (reset when a battle is selected).
    public int[] ReconStrategyActionsThisBattle { get; set; } = new int[2];
    // Seeds of Decay pass (issue #453), batch 2 (SeedsOfDecayBatch2.cs). Each record expires by comparison (a turn number, a phase instance, a player's turn count), never by a sweep.
    // Province strength for the turn (Incriminating Testimony, Bayushi Shibata - exp, Imperial Protection's standing rule) -- read by EffectiveProvinceStrength beside the permanent Strength.
    public List<ProvinceStrengthEffect> ProvinceStrengthEffects { get; set; } = [];
    // Forgotten Legacy pass (issue #467), batch 1 (ForgottenLegacyBatch1.cs); each record expires by comparison with the turn number.
    // "Choose one of his abilities; it may not be used" (Doji Hakuseki, Doji Rengetsu): one printed ability (by usage key) of one card, locked for the turn.
    public List<DisabledAbility> DisabledAbilities { get; set; } = [];
    // "Until this battle ends, abilities on cards now in that unit may not be used" (Yoritomo Naoto, Emperor Edition Personality pass, issue #477): cleared when the battle ends.
    public List<BattleAbilityLock> BattleAbilityLocks { get; set; } = [];
    // "If it is not your turn, before it ends, if none of your provinces were destroyed this turn, gain 1 Honor" (Asahina Shigemitsu): drained in EndTurn.
    public List<ProvincesHeldHonorCheck> ProvincesHeldHonorChecks { get; set; } = [];
    // "Unless Murota has performed a Dark Virtue action this turn": the action keywords each Personality has performed an action with, per turn.
    public List<PerformedKeywordAction> PerformedKeywordActions { get; set; } = [];
    // "After the next time this turn he is assigned to attack [or is destroyed]" (Imperial Protection, Fair Warning): fired from Assign() and MarkDead.
    public List<AssignmentWatch> AssignmentWatches { get; set; } = [];
    // The Shadow's Embrace pass (issue #476), batch 4 (ShadowsEmbraceBatch4.cs): Preserving Beauty's "before this turn ends, gain 3 Honor if that battlefield's province
    // has not been destroyed", one record per use, drained in EndTurn.
    public List<PreservingBeautyCheck> PreservingBeautyChecks { get; set; } = [];
    // Emperor Edition remaining-cards pass, group A (issue #492; EmperorRemainingA.cs): Followers Deadly Orders destroys once the battle's resolution is over; the held action a hand Reaction
    // (Bad Kharma, Footsteps of Madness) negated as a whole (and whether its costs are ignored); the number of cards Face of Ninube shuffled in.
    public bool RemANegatesHeldAction { get; set; }
    public bool RemAIgnoresHeldCosts { get; set; }
    public int RemAShuffledCount { get; set; }
    // Emperor Edition remaining-cards pass (issue #492), group D (EmperorRemainingD.cs): Torch's Flame Flickers' "negate the next card effect that bows ...", The Shadow Court's "after the next time
    // this turn ... destroys your Courtier" and The Sincerity of the Dragon's named number -- one record per use; and the battle resolution paused for a Shadow Court decision.
    public List<TorchWatch> TorchWatches { get; set; } = [];
    public List<ShadowCourtWatch> ShadowCourtWatches { get; set; } = [];
    public List<SincerityWatch> SincerityWatches { get; set; } = [];
    public PendingAfterResolution? PendingShadowCourtResolution { get; set; }
    // Twist of Fate (issue #492, group D): "Before your Dynasty Phase ends: Take an additional Limited or Open action" -- while the additional action is being taken the match sits in the Action Phase
    // (TwistOfFateActive) and returns to TwistOfFateReturnPhase once it is done; a player who passed the window is not asked again this turn (TwistOfFateDeclinedTurn).
    public bool TwistOfFateActive { get; set; }
    public Phase TwistOfFateReturnPhase { get; set; }
    public int TwistOfFateDeclinedTurn { get; set; } = -1;
    // Paralyzing Touch's "negate the effects of the next action performed by its winner" (for the turn), spent by that action.
    public List<ParalyzingTouchWatch> ParalyzingTouchWatches { get; set; } = [];
    // Spiritual Manipulation's "must declare a normal attack this turn and assign the target (if legal)".
    public List<ForcedAttack> ForcedAttacks { get; set; } = [];
    // The Game Has Changed: the attachments its resolving action has already transferred (each at most once), cleared as the action begins.
    public List<string> GameHasChangedTransferred { get; set; } = [];
    // Sentinel Spirit: judged when the next Dynasty Phase begins (SetPhase).
    public List<SentinelWatch> SentinelWatches { get; set; } = [];
    // Urgency: "each player may only take two more non-Reaction actions this phase" -- ForcedPass's own shape, counted down by Apply().
    public List<ActionAllowance> ActionAllowances { get; set; } = [];
    // Yogo Ugimori: "he may not take the Proclaim Reaction until your next turn begins".
    public List<ProclaimLock> ProclaimLocks { get; set; } = [];
    // Turning the Story: non-null exactly while a "proclaimNegateReaction" PendingChoice is open (the Proclaim it holds resolves when the window closes).
    public HeldProclaim? HeldProclaim { get; set; }
    // Ingenuity: "draw an additional card during your next End Phase", per player, drawn and reset by EndTurn.
    public int[] ExtraEndPhaseDraws { get; set; } = new int[2];
    // Yogo Ugimori ("after Ugimori enters play from a province"): the card a province recruit (Purchase, RecruitReserve) last brought into play.
    public string? LastProvinceEntryCardId { get; set; }
    // An Insult Answered ("an action ... which caused you an Honor loss"): the players the currently-resolving action cost Honor, snapshotted into LastResolvedAction.HonorLosers.
    public List<int> CurrentActionHonorLosers { get; set; } = [];
    // Emperor Edition Stronghold pass (issue #486), batch 1 (EmperorStrongholdBatch1.cs). Eternal Victory Dojo: "Draw a card after the next End Phase begins" -- one entry per draw owed, the player who
    // draws it. Kyuden Otomo - exp: the players who took the Imperial Favor during the action now resolving (snapshotted into LastResolvedAction.FavorTakers). Hidden Falls Dojo: the provinces a
    // player targeted this turn, and whether the additional Infantry Maneuvers Segment it grants is the segment in progress.
    public List<int> PendingEndPhaseDraws { get; set; } = [];
    public List<int> CurrentActionFavorTakers { get; set; } = [];
    public List<HiddenFallsGrant> HiddenFallsGrants { get; set; } = [];
    // Emperor Edition remaining-cards pass (issue #492), group E (EmperorRemainingE.cs): Consecration's "after the first time this turn a Personality assigns or moves to an attacking army at one of those
    // provinces' battlefields, gain 2 Honor" -- one record per use, each spent by the event or lapsing with the turn.
    public List<ConsecrationWatch> ConsecrationWatches { get; set; } = [];
    public bool ExtraInfantrySegment { get; set; }
    // Brotherhood Schism: the turn in which "Rings will not enter play if their owner has already put a Ring into play this turn" stands.
    public int RingSchismTurn { get; set; } = -1;
    // Moksha Patam (oracle-l5r-10121, issue #453 batch 5): "After an action resolves, if a player gained Honor from its effects" -- set by GainHonor on any
    // gain, reset when an action begins (RunAbilityEffects) and read once when it is truly done (FinishActionEffectTracking, ApplyMokshaPatamTokens).
    public bool CurrentActionHonorGained { get; set; }
    // The Shadow's Embrace pass (issue #476), batch 1 (ShadowsEmbraceBatch1.cs); each record expires by comparison with the turn (or battle) number.
    // Bayushi Misaki: "Before this phase ends, straighten him. Before this turn ends, if the Personality was not assigned to attack this turn, dishonor him ..."
    public List<MisakiWatch> MisakiWatches { get; set; } = [];
    // Bayushi Waru: "negate the effects of the next action the target performs (this turn)" -- spent by that action.
    public List<NextPerformedActionNegation> NextPerformedActionNegations { get; set; } = [];
    // Hiruma Nikaru (Shadow's Embrace): "Negate the effects of the next Battle action this battle another player takes which targets Nikaru" -- spent by that action.
    public List<BattleTargetShield> BattleTargetShields { get; set; } = [];
    // Agasha Kurou: "Remove it from the game before the turn ends" -- the Spells he attached, drained in EndTurn.
    public List<TurnEndRemoval> TurnEndRemovals { get; set; } = [];
    // Emperor Edition Personality pass (issue #477), batch 5 (EmperorPersonalityBatch5.cs).
    // Asako Ryo ("refill it face-up"): the province index LastProvinceEntryCardId last left (set beside it).
    public int? LastProvinceEntryProvinceIndex { get; set; }
    // Isawa Norimichi ("your army has +5 total Force" until the battle ends): read by ArmyForces, expiring by comparison with BattleNumber.
    public List<ArmyForceBonus> ArmyForceBonuses { get; set; } = [];
    // Kitsu Tamasine ("if you have lost Honor from another player's action during an Action Phase since your last turn began"): per player, set as such an action resolves, cleared as that player's turn begins.
    public bool[] HonorLostToOtherActionSinceTurnBegan { get; set; } = new bool[2];
    // Rugashi Bazaar (Emperor Edition remaining-cards pass, issue #492 group F): "lost Honor from another player's action during the Action Phase since your last turn ENDED" -- the same
    // record, restarted when the loser's own turn ends rather than when it begins.
    public bool[] HonorLostToOtherActionSinceTurnEnded { get; set; } = new bool[2];
    // Emperor Edition remaining-cards pass (issue #492), group F (EmperorRemainingFEvents.cs): the decision of an Event in progress, the ones queued behind it (an optional draw owed when a battle's
    // resolution or an attach finishes), and the Personality an Event was told to name (Delayed Arrival), by the Event's card id.
    public PendingEventChoice? PendingEventChoice { get; set; }
    // Ten Thousand Temples (issue #492, group F): "Before you lose Honor, destroy this card: Negate the Honor loss." -- the command whose execution reached an Honor loss the Temple could negate, held whole
    // while its controller decides (MatchEngine.Execute replays it with the decisions made).
    public PendingHonorLossDecision? PendingHonorLossDecision { get; set; }
    // Mendon's R4 ruling (issue #492): an Honor gain or loss from an action's effects that Reactions may be played on before it is applied (see PendingHonorChange).
    public PendingHonorChange? PendingHonorChange { get; set; }
    public List<PendingEventChoice> PendingEventChoices { get; set; } = [];
    public Dictionary<string, string> EventTargets { get; set; } = [];
    // Isawa Kimi - exp 2 ("an action that put any cards into another player's hand"): the players whose hand the currently-resolving action put a card into, snapshotted into LastResolvedAction.HandRecipients.
    public List<int> CurrentActionHandRecipients { get; set; } = [];
    // Emperor Edition follow-ups (issue #498, EmperorFollowups*.cs). An "After this card enters play: search ..." trait queued behind the entry windows (Enslaved Djinn, Celestial Sword of the Dragon,
    // Asako Chukage); the Attack target selection step after an announce window (No Hiding Place); the changes an announce Reaction made to the Attacks of the announced action; Rend the Soul's marks.
    public List<PendingEntrySearch> PendingEntrySearches { get; set; } = [];
    public PendingAttackTargetSelection? PendingAttackTargetSelection { get; set; }
    public List<AnnouncedAttackModifier> AnnouncedAttackModifiers { get; set; } = [];
    public List<RendTheSoulMark> RendTheSoulMarks { get; set; } = [];
    // Emperor Edition mechanics group C (issue #501, EmperorMechC.cs): an additional cost of entering play awaiting its payer's choice, and the answers given so far to the command being replayed.
    public PendingEntryCostDecision? PendingEntryCostDecision { get; set; }
    public List<string> EntryCostAnswers { get; set; } = [];

}

/// <summary>Isawa Norimichi: PlayerId's army has +Amount total Force in the battle numbered Battle (while it has any units).</summary>
public sealed record ArmyForceBonus(int PlayerId, int Amount, int Battle);

/// <summary>A standing redirect (Seeds of Decay, issue #453): Kind is a StandingRedirectKinds value, OwnerId the player whose cards it protects, ToCardId the card the action
/// targets instead, Turn the turn it lasts (Battle: the battle it lasts, or null for the whole turn), SourceName the card that set it up.</summary>
public sealed record StandingRedirect(string Kind, int OwnerId, string ToCardId, int Turn, int? Battle, string SourceName);

/// <summary>Grappling the Snake: after the action that targeted MonkId resolves, bow the unit of PerformerId (a Personality that performed it).</summary>
public sealed record PendingGrapplingBow(string MonkId, string PerformerId, string SourceName);

/// <summary>A dead Personality to recruit automatically -- ignoring costs and Honor Requirements -- the next time any player's turn ends, if he is still dead then
/// (issue #440 follow-up). See MatchState.PendingEndPhaseRecruits.</summary>
public sealed record PendingEndPhaseRecruit(int PlayerId, string CardId, string Via);

/// <summary>One card the currently-resolving action affected, and how (issue #437: ActionEffectTracking.cs). Kind is "destroyed" or "moved" today;
/// a bow is not tracked here yet (no single choke point narrates every one the way DestroyUnits/MoveHome already do -- see the file comment).</summary>
// HostId (The Shadow's Embrace pass, issue #476 batch 6: Shieldman's "destroyed another card in this unit"): the Personality a destroyed attachment was attached to.
/// FromBattlefield (The Shadow's Embrace pass, issue #476 batch 3): for a move home, the battlefield (province index) the card was moved home FROM; null when it was not at one
/// (I Cast Two Shadows: "move him to a different battlefield than the one from which he was moved home").
/// ToBattlefield / Side / Focus (P8, ActionEffectWatch.cs): where a "moved" card went ("attacking" or "defending" army there), and the Focus Value a "discarded" card had; omitted when unset.
public sealed record AffectedCard(string CardId, string Kind, string? HostId = null, int? FromBattlefield = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ToBattlefield = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Side = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Focus = null);

/// <summary>What the LAST fully-resolved action did (issue #437): who performed it, which ability, and which cards it affected. Replaced, not
/// merged, each time a new top-level action -- an ability's own synchronous resolution, a deferred Attack, or a duel -- finishes, including its
/// own reaction windows. A Reaction whose trigger names "after an action ... " reads this instead of re-deriving it from the raw event log.</summary>
public sealed class ResolvedActionFacts
{
    public string AbilityId { get; set; } = "";
    public int PlayerId { get; set; }
    public string? SourceId { get; set; }
    public List<AffectedCard> Affected { get; set; } = [];
    // Seeds of Decay pass (issue #453), batch 2: the Personality who performed the action (the named performer, or the source resolved to its wielder), and the players it cost Honor.
    public string? PerformerId { get; set; }
    // The cards the action targeted (issue #648, Without Mercy: "if the action did not target an enemy card"): the command's named targets, a duel's challenged Personality, an Attack's target card.
    public List<string> TargetIds { get; set; } = [];
    public List<int> HonorLosers { get; set; } = [];
    // Emperor Edition Personality pass (issue #477), batch 5: the players whose hand the action put a card into (Isawa Kimi - exp 2).
    public List<int> HandRecipients { get; set; } = [];
    // Emperor Edition Stronghold pass (issue #486), batch 1 (Kyuden Otomo - exp): the players who took the Imperial Favor during the action.
    public List<int> FavorTakers { get; set; } = [];
    // Issue #667 (FavorMempo.cs): the players who discarded the Imperial Favor as a cost of the action ("After you discard the Imperial Favor").
    public List<int> FavorDiscarders { get; set; } = [];
    // Emperor Edition remaining-cards pass (issue #492), group D (Words Have Strength): "You may only play one copy ... in reaction to any one resolution" -- set once a copy is played.
    public bool WordsHaveStrengthPlayed { get; set; }
    // Issue #898: Reactions printed "You may only play one copy of X in reaction to any one resolution" (Grief), recorded when a copy is played so another cannot answer the same resolution.
    public List<string> OneCopyPlayed { get; set; } = [];
    // P6 (issue #705, AfterFacts.cs): the fact this record stands for when AbilityId is a fact sentinel (FactTriggers.Sentinel); null for an action's record.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FactRecord? Fact { get; set; }
}

// DeckPosition ("Top"/"Bottom") is the acting player's own upfront choice of where a card goes in
// its owner's deck (e.g. Goju House Guard's own cost, issue #192) -- unlike Choice, which resolves a
// separate, later decision command, this is supplied on the initiating command itself, the same way
// TargetId or SourceIds are.
public sealed record GameCommand(string Type, int PlayerId, long ExpectedVersion, string? CardId = null,
    string[]? SourceIds = null, int? Province = null, bool ClanDiscount = false, bool ClanAllegiance = false,
    Dictionary<string, int>? Assignments = null, string[]? CardIds = null, string? TargetId = null, string? AbilityId = null, string? PerformerId = null, string? Choice = null, string? DeckPosition = null,
    // "Choose a player" (issue #210): a player, not a card, is the target -- Kitsuki Horume's own
    // shape ("Choose a player. He gains or loses 1 Honor."). Distinct from PlayerId (the ACTING
    // player submitting this command).
    int? TargetPlayerId = null,
    // Generalized Gold payment (issue #303): the amount of Gold the acting player chooses to pay for an
    // ability whose cost is a player-chosen variable ("pay zero or more Gold"). Null for every fixed or
    // derived cost (the engine's own GoldQuote decides those) and read only for a variable-cost ability,
    // where it must lie inside that quote's MinAmount..MaxAmount; null there means the minimum.
    int? GoldAmount = null,
    // Alternative cost (issue #337): which of the ability's CostOptions the acting player pays ("bow" or "destroy" the
    // performing Ancestor). Chosen up front, like the performer, because a cost is paid before anything resolves; null is
    // only accepted when the ability has a single option, and never for an ability with none.
    string? CostOption = null,
    // Multi-target actions (issue #336): one entry per target slot of an ability that declares AbilityDefinition.Slots --
    // the cards (or the player) chosen for that slot. Null for every ability with a single Target.
    SlotSelection[]? Selections = null,
    // "Create a card" with a choice of cards (issue #394, "an Armor or Weapon Item"): the id of the scan the acting player chose among the ability's
    // CreatedCardSpec.Or alternatives; null for an ability that creates a single card.
    string? CreatedCard = null,
    // Emperor Edition Personality pass (issue #477), batch 4 (Asahina Keigo): for each Gold source that produces "Gold equal to the Focus Value of a card discarded", the id of the
    // hand card the payer discards for it (source id -> hand card id). Null for every other payment.
    Dictionary<string, string>? PaymentDiscards = null,
    // Emperor Edition Stronghold pass (issue #486), batch 1 (Hidden Falls Dojo: "Target one or two provinces"): the indices of the Defender's provinces the action targets; null for every other action
    // (a single province rides in Province, as it always has).
    int[]? Provinces = null);
public sealed record CommandResult(bool Accepted, string? Error, long Version, GameEvent[] Events);
/// <summary>The consequence of destroying the province in a Harsh Choices decision, so no client re-derives it: the decider's unbroken provinces before and after, whether that ends the match in defeat, and the cards drawn if it does not.</summary>
public sealed record HarshChoicesView(int ProvincesBefore, int ProvincesAfter, bool EndsMatch, int CardsDrawn);
public sealed record PaymentOption(string[] SourceIds, int Produced, int Cost, int Excess);
// Basis (issue #303): the human-readable derivation of Cost, e.g. "Training samurai cost 5 - 2 (Clan
// discount)". Null only for quotes serialized by an older engine.
public sealed record PurchaseQuote(string CardId, int Cost, bool ClanDiscount, bool ClanAllegiance,
    bool ProclaimEligible, string? Error, PaymentOption[] Payments, string? Basis = null,
    // Authoritative methods for this payment, including a card's own production and Reaction's Gold used beside or instead of bowing (Seaside Bazaar, Luxurious Silk, Library of
    // Rebirth), Zlkyt's "destroy your Mine or Farm" (one per Holding, highest Gold Cost first, always last) -- each a source id Payments and GameCommand.SourceIds may name. Null only for older payloads.
    PaymentSource[]? Sources = null);
public sealed record OverlayQuote(string CardId, string TargetId, int HigherLevel, int LowerLevel, string? Error);
/// <summary>The one authoritative Gold quote (issue #303) for any action that costs Gold: recruiting,
/// equipping, playing a Strategy and every costed ability. The client renders this and never derives a
/// cost itself. Kind is "fixed" (printed on the card or fixed in an ability), "derived" (computed from a
/// chosen target: another card's cost, a unit's grouped total, a recruit quote, plus a modifier and floor)
/// or "variable" (the player chooses the amount, MinAmount..MaxAmount, sent as GameCommand.GoldAmount).
/// Cost is what the action costs now (for "variable", the default amount, i.e. MinAmount). Payments are
/// the engine's suggested and alternative source sets for Cost, best first; for "variable" quotes
/// PaymentsByAmount holds them for every legal amount (keyed by amount) so the client can change the
/// amount without a round trip. Error, when set, means this quote cannot be paid or used as it stands.</summary>
public sealed record GoldQuote(int Cost, string Kind, string Basis, string? Error, PaymentOption[] Payments,
    int? MinAmount = null, int? MaxAmount = null, Dictionary<int, PaymentOption[]>? PaymentsByAmount = null,
    // Authoritative payment methods and source amounts (see PurchaseQuote.Sources); null only for older payloads.
    PaymentSource[]? Sources = null);
// Presentation metadata survives a disabled option's null Command. No executable command is invented for it.
public sealed record AlternativeCostView(string AbilityId, string? PerformerId, string Id, string Label, string ActionLabel);
/// <param name="Gold">The quote for this option's own command (its first legal target when the cost is
/// target-derived). Null when the action costs no Gold.</param>
/// <param name="TargetGold">Per-target quotes (target id to quote) for a target-derived cost: the cost
/// changes when the player picks a different target, so every legal target ships its own quote and the
/// client swaps between them without a round trip. Null for fixed and variable costs.</param>
public sealed record ActionOption(string Type, string Label, bool Enabled, string? Reason,
    GameCommand? Command = null, string? CardId = null, string[]? TargetIds = null,
    int[]? Provinces = null, PurchaseQuote? Quote = null, GoldQuote? Gold = null,
    Dictionary<string, GoldQuote>? TargetGold = null,
    // Multi-target actions (issue #336): the slots of a Slots ability with the legal candidates of each, so a viewer can
    // collect the whole selection. Null for every ability with a single Target.
    TargetSlotOption[]? Slots = null,
    bool NoTarget = false, AlternativeCostView? AlternativeCost = null,
    MoveDestinationView? Movement = null, AttackCueView? Attack = null,
    // Engine-selected preview cards for a complete pending answer; private candidates are only offered to the decider.
    string[]? DecisionCandidateIds = null, string? DecisionLabel = null,
    // The destination rule of an ability that moves cards (MoveSpec.Home / CurrentBattlefield, or a rule that needs a chosen battlefield); null for every other action. Lets a client offer a drag without reading card text.
    string? MoveTo = null)
{
    /// <summary>
    /// Issue #1028: true when this option's own fields say the player must supply more than a confirm (a target, a province choice list, a slot, a movement or attack choice, an
    /// alternative cost, or a Gold payment), so a client must not submit <see cref="Command"/> as it stands. A client adds only what it alone knows (which of several variants
    /// to play); it does not keep its own list of Core's fields. <c>ActionOptionInputTests</c> fails when a field is added to this record without being classified there.
    /// </summary>
    public bool RequiresInput() => TargetIds is { Length: > 0 } || Slots is not null || Movement is not null || Attack is not null || AlternativeCost is not null
        || Gold is { Cost: > 0 } || Gold is { Kind: GoldCostKind.Variable } || TargetGold is not null || Command?.CardIds is not null;
}
/// <summary>Read-only attack presentation. SourceId is the explicitly selected performer or the ability source;
/// it identifies the card to mark, not a new legality or damage calculation.</summary>
public sealed record AttackCueView(string Kind, string SourceId);
/// <summary>Proposed movement only: reactions may prevent each arrival independently. FixedMoverIds names the source/performer units;
/// TargetMoves adds the command's selected target. Destinations remain in ActionOption.Provinces. No card-rule inference belongs in the UI.</summary>
public sealed record MoveDestinationView(int DefenderId, string[] FixedMoverIds, bool TargetMoves);
/// <param name="UnitForce">Authoritative effective unit contribution, including qualifying Followers.
/// Null for hidden/non-unit/out-of-play cards and older payloads; never substitute a client calculation.</param>
public sealed record CardView(string Id, CardDefinition? Definition, int OwnerId, Zone Zone, bool FaceUp,
    bool Bowed, int? ProvinceIndex, string? AttachedTo, int Force, int Chi = 0, bool StraightenBlocked = false,
    AppliedModifier[]? Modifiers = null, GrantedKeyword[]? GrantedKeywords = null, bool Dishonorable = false, int? UnitForce = null,
    CardToken[]? Tokens = null,
    // Abilities this card has by another card's effect (a copied ability, a permanent grant), with their labels; null when none.
    GrantedAbilityView[]? GrantedAbilities = null,
    // Authoritative Gold this card can produce for a payment right now (a printed producer, or a card that can bow to produce Gold
    // while a cost is being paid: Yoritomo Sakuma); null when it cannot. The client shows this and never derives it.
    int? GoldProduced = null,
    // "May not perform actions" (issue #336): true while the card, or the Personality it is attached to, is action-locked.
    bool ActionLocked = false,
    // A created copy of another card (issue #398): its Definition is the copied card's own; this says it is a created card, for the client's marker.
    bool CreatedCopy = false,
    // Read-only explanations of already enforced granted effects (duel presentation); null when none.
    GrantedEffectView[]? GrantedEffects = null,
    // Issue #501 (Mendon's item 32; the client's "duplicate" frame is issue #502): true for a copy of another card; the copied card's instance id (null when it is no longer known)
    // and definition id (the scan the client shows); a human-readable sentence of every condition on the copy (timed removal, ...) for the frame's tooltip.
    bool IsCopy = false, string? CopyOfCardId = null, string? CopyOfDefinitionId = null, string? CopyConditions = null,
    // Issue #501 (control transfer): the card's OWNER when another player controls it (OwnerId is always the controller); null when its owner controls it.
    int? RealOwnerId = null,
    // Issue #611: the Personality's match-unique, public "Unit N" number, assigned when it first enters play; 0 for a card that has not (and for hidden cards).
    int UnitNumber = 0,
    // P21 (issue #697): the card a Zone.Underneath card sits under (null for the cards under the Stronghold and for any other card). A face-down under-card is redacted for the other seat.
    string? UnderHostId = null);
/// <summary>What Dark Experiments does to the province it marks (permanent) and to each Holding that enters play from it; the board marker shows these values.</summary>
public static class DarkExperimentsMark
{
    public const int Strength = -2, Production = -1;
}

/// <summary>One granted ability as the client shows it: the id, the engine's label for it, and where it came from.</summary>
public sealed record GrantedAbilityView(string AbilityId, string Label, string Timing, string Source, string Duration, string? CopiedFrom, string? CopiedFromName,
    string? SourceName = null, int? UsesPerTurn = null, int? UsesRemaining = null);
// Strength is the EFFECTIVE strength (printed + any attached Region's static bonus, e.g. Fallow
// Lands) -- what a player actually needs to beat to destroy the province, not the raw mutable field.
/// <param name="Strength">What anything reads the province's strength as now: the current strength, or the maximum while a limit holds it down.</param>
/// <param name="MaxStrength">The limit in force (Times of Strife, Ancient Feud), or null. While it holds, the current strength may be higher and returns when the limit ends.</param>
public sealed record ProvinceView(int Index, int Strength, bool Destroyed, CardView? Card, CardView? Region, int? MaxStrength = null,
    // Issue #503 (EmperorP6.cs): Illusory Defense's province that never holds a Dynasty card, and Dark Experiments' permanent mark (both public).
    bool HoldsNoDynastyCards = false, bool DarkExperimentsMarked = false)
{
    /// <summary>Optional slot view for multi-card provinces. Null retains legacy Card as slot 0.
    /// When supplied this is authoritative, including empty slots; Card must not be appended again.
    /// Slot indices are stable across refills. Face-down cards must already be redacted by the producer.</summary>
    public ProvinceCardSlotView[]? DynastySlots { get; init; }
    /// <summary>P15 (issue #719): the ids of the Fortification Holdings that guard this Province (public: a Holding in play is face up). Null when none.</summary>
    public string[]? FortificationIds { get; init; }
    /// <summary>What every change to this province's strength currently adds to its printed strength (permanent and timed effects, Regions, Terrain, static bonuses, minus caps), as Strength - printed.
    /// Zero when unchanged or when the printed strength is not recorded (prepared fixtures). The board shows it beside the province whenever it is not zero.</summary>
    public int StrengthModifier { get; init; }
    /// <summary>The named timed effects inside StrengthModifier ("Ring of Earth +3"); the remainder is permanent or ongoing. Null when there are none.</summary>
    public string[]? StrengthNotes { get; init; }
}
public sealed record ProvinceCardSlotView(int Index, CardView? Card);
/// <summary>Public decision identity/prompt; Options are sent only to PlayerId. Commands are engine-provided,
/// not inferred by the client. Slot indices refer to OwnerId's Province, not card identities.</summary>
public sealed record ProvinceOrderView(string Id, int PlayerId, int OwnerId, int Province, string Prompt,
    ProvinceOrderOption[] Options);
public sealed record ProvinceOrderOption(string Id, string Label, int[] Slots, GameCommand? Command,
    bool Enabled = true, string? Reason = null);
// DeadCount (issue #221): the authoritative count of this player's own Zone.Dead Personalities --
// already present, individually, within Discard (each CardView's own Zone tells a dead card apart
// from a merely-discarded one), but computed here as a single number the same way UnitForce is, so
// a UI can show it directly and a future card can read it without re-deriving its own filter.
public sealed record PlayerView(int Id, string Name, string Clan, int Honor, int TurnsTaken,
    CardView Stronghold, CardView[] Home, CardView[] Army, ProvinceView[] Provinces,
    CardView[] Hand, int HandCount, CardView[] Discard,
    bool ProclaimUsedThisTurn, bool DishonorLossPending, CardView[] RemovedFromGame,
    int DeadCount, CardView[]? Focused = null,
    // Zone.EventInPlay (Suspicions): public, like RemovedFromGame -- no visibility gate needed.
    CardView[]? EventsInPlay = null,
    // Zone.Celestial (Lady Matsu's Rage): also public -- no visibility gate needed.
    CardView[]? Celestials = null, int FocusCount = 0,
    // Zone.Underneath (issue #503, The New Foundries): the cards under the player's Stronghold (public) and, since P21, under any host card (a face-down one is redacted for the other seat).
    CardView[]? Underneath = null)
{
    public HandLimitView? HandLimit { get; init; }
    /// <summary>True once this player bought, overlaid or discarded a province card since the current Dynasty Phase began (public; absent/false in older payloads).</summary>
    public bool DynastyActionTaken { get; init; }
    /// <summary>True only in the owning seat's own view, while Plans Within Plans lets this player take a Limited action in place of an Open one right now. Lets a client treat that out-of-phase Limited action as the one being played.</summary>
    public bool PlansWithinPlansArmed { get; init; }
}
public sealed record HandLimitModifier(string CardId, string CardName, int Amount);
public sealed record HandLimitView(int Base, int Maximum, HandLimitModifier[] Modifiers);
/// <summary>Nothing here is hidden information -- both players already know who challenged whom, on
/// what stat, and whose turn it is; a focused card's own identity is still gated the normal way,
/// through PlayerView.Focused's own per-viewer visibility.</summary>
public sealed record DuelView(int ChallengerPlayerId, int ChallengedPlayerId, string ChallengerId,
    string ChallengedId, string Stat, string Stage, int TurnPlayerId, bool Struck, string DuelId = "");
/// <summary>The names <see cref="BattleStepView"/> speaks in, one per step of the rules lead's formal Battle
/// Phase (issue #310). A client keys its sidebar off these constants and never infers timing itself.</summary>
public static class BattleSteps
{
    // Segments (spec steps 1-5). "None" is any moment outside the Attack Phase. Declaration is one explicit decision by
    // the Attacker (DeclareOrDecline): declining ends the Attack Phase at once (the view then reads None), declaring
    // creates a battlefield at every one of the Defender's provinces and moves on to Infantry Maneuvers.
    public const string None = "None", Declaration = "Declaration", InfantryManeuvers = "InfantryManeuvers", CavalryManeuvers = "CavalryManeuvers",
        FightBattles = "FightBattles", Resolution = "Resolution";
    // Steps. DeclareOrDecline is the Declaration decision; AttackerAssigns/DefenderAssigns are the Maneuvers decisions; ChooseBattle is 4.1; Engage is 4.2;
    // BattleActions is 4.5 (the first-action holder of 4.3/4.4 is in FirstActionPlayerId); BeforeResolution and
    // Aftermath are the two observable halves of Resolution (5) -- the Force comparison itself is instantaneous.
    public const string DeclareOrDecline = "DeclareOrDecline", AttackerAssigns = "AttackerAssigns", DefenderAssigns = "DefenderAssigns", ChooseBattle = "ChooseBattle",
        Engage = "Engage", BattleActions = "BattleActions", BeforeResolution = "BeforeResolution", Aftermath = "Aftermath";
    // FirstActionReason values.
    public const string ReasonDefault = "Default", ReasonNaval = "Naval", ReasonEngageGrant = "EngageGrant", ReasonDefenderOverride = "DefenderOverride";
    // FirstActionLimit values.
    public const string LimitMustPass = "MustPass", LimitPerformerOnly = "PerformerOnly", LimitActionKeyword = "ActionKeyword";
    // BeforeResolutionEffectView.Kind: the three kinds of item in the one ordered set of before-resolution effects.
    public const string EffectTrait = "Trait", EffectDelayed = "Delayed", EffectReaction = "Reaction";
}

/// <summary>Public, read-only Battle Phase timing (issue #310): everything a battle sidebar needs, derived from
/// authoritative state on every GetView and identical for both viewers (nothing here is hidden information).
/// Read: <c>Segment</c>/<c>Step</c> (what the phase is doing), <c>DecisionPlayerId</c> (whose decision it is),
/// <c>BattlefieldProvince</c> (the selected battle), <c>FirstActionPlayerId</c>/<c>FirstActionReason</c> (who acts
/// first and why; while the Engage window is open they are provisional, <c>FirstActionResolved</c> false, and
/// <c>EngageClaims</c> lists the claims so far), <c>FirstActionLimit</c>/<c>FirstActionPerformerId</c> (a limit on
/// that first action), <c>ConsecutivePasses</c> (0 or 1; two ends the segment), <c>BeforeResolutionSourceIds</c>
/// (cards whose "before this battle resolves" trait is still to apply) and <c>Window</c> (the open PendingChoice
/// type, if any).</summary>
public sealed record BattleStepView(string Segment, string Step, int? DecisionPlayerId, int? AttackerPlayerId,
    int? BattlefieldProvince, int? FirstActionPlayerId, string? FirstActionReason, bool FirstActionResolved,
    FirstOpportunityClaim[] EngageClaims, string? FirstActionLimit, string? FirstActionPerformerId,
    int ConsecutivePasses, string[] BeforeResolutionSourceIds, string? ActiveBeforeResolutionSourceId, string? Window,
    BeforeResolutionEffectView[]? BeforeResolutionEffects = null);

/// <summary>One item of the ordered set of before-resolution effects still to apply (BattleStepView.BeforeResolutionEffects; its Id is the
/// one in BeforeResolutionSourceIds and the one a "beforeBattleOrder" names). Kind is EffectTrait (a card's trait), EffectDelayed (registered
/// by a Battle action) or EffectReaction (a printed "Before a battle resolves" Reaction, optional); PlayerId is whose it is (the active player
/// orders the whole set), CardId the card behind it when there is one, Label a name for it.</summary>
public sealed record BeforeResolutionEffectView(string Id, string Kind, int PlayerId, string? CardId, string Label);
/// <summary>Public presentation of the current response opportunity. It describes existing timing only: no commands,
/// hidden candidates or prediction of the other player's available Reactions. A nested choice may have no decline label.
/// Status describes the triggering moment, never a receipt claiming that the player's latest Reaction fully resolved.</summary>
public sealed record ReactionPresentationView(string Title, string Context, string Status, string DeclineLabel,
    string DeclineConsequence, string? ParentContext = null, bool IsNested = false);
/// <summary>Issue #917: one card in the resolution area -- the card, who played it, and the ability being resolved.</summary>
public sealed record ResolutionEntry(string CardId, int OwnerId, string AbilityId);
/// <summary>Issue #917: PUBLIC to both seats. Depth 0 is the oldest (the action the later ones answer).</summary>
public sealed record ResolutionEntryView(CardView Card, int OwnerId, string AbilityId, string AbilityLabel, int Depth);
public sealed record MatchView(string ContentVersion, string Scenario, long Version, int TurnNumber,
    int ViewerId, int ActivePlayerId, int PriorityPlayerId, Phase Phase, PendingChoice? PendingChoice,
    int? CurrentBattleProvince, int[] UnresolvedBattlefields, int? WinnerId, string? VictoryReason,
    PlayerView[] Players, GameEvent[] Events, string[] Limitations, int? FavorPlayerId = null,
    DuelView? PendingDuel = null, BattleStepView? BattleStep = null,
    // Deck search (issue #312), see DeckSearch.cs. PendingSearch is PRIVATE to the searching player (the deck
    // viewer: remaining cards grouped with counts, eligibility per entry) and null in everyone else's view.
    // ShownCards is PUBLIC: the append-only record of every card either player has shown, identical in both views.
    SearchView? PendingSearch = null, ShownCardView[]? ShownCards = null,
    // Look at the top N cards (issue #322), see DeckLook.cs: PRIVATE to the viewing player (the top cards in order and what each
    // may become) and null in everyone else's view, including the deck owner's when the viewer looks at the OPPONENT's deck.
    LookView? PendingLook = null,
    // Copying keywords and abilities (issue #338, see CardCopy.cs): PRIVATE to the choosing player, null in everyone else's view.
    CopyView? PendingCopy = null,
    // The opposing player's Either/Or choice (issue #338, see EitherOr.cs): PUBLIC -- both players see what is being decided and
    // by whom; only the deciding player's view carries the payment quotes.
    EitherOrView? PendingEitherOr = null,
    // The opposing player's variable payment (issue #373, see OpponentPayment.cs): PUBLIC -- both players see the first player's amount, the card and the
    // Honor at stake; only the paying player's view carries the payment quote and what each amount would do.
    OpponentPaymentView? PendingOpponentPayment = null, DuelRecord? LastDuel = null,
    // The approved attack cue (#355): public, derived from authoritative state.
    AttackCueView? PendingAttack = null,
    // Emperor Edition Stronghold pass (issue #486), batch 3 (see EmperorStrongholdBatch3.cs): the face-down province cards this viewer looked at this turn -- PRIVATE to the looker, null for
    // everyone else. Never a card id: a face-down card is named by whose province and which.
    ProvinceSightView[]? ProvinceSights = null,
    // Emperor Edition rulings pass R1 (issue #492): every battlefield of the Attack Phase in progress and the province (an index into the Defender's row) it is at, null when it has
    // none. An index below 100 is the battlefield of the Defender's province of that index; a higher one is an additional battlefield (EmperorRulingR1.cs).
    BattlefieldView[]? Battlefields = null,
    // Issue #503 (EmperorP5Hidden.cs): the cards of another player's hand this viewer looked at this turn (Scouting the Pass, The Price of Honor) -- PRIVATE to the looker, never an instance id.
    HandSightView[]? HandSights = null)
{
    // #722: Core-owned response context. Older snapshots omit this member; no response is inferred by the client.
    public ReactionPresentationView? ReactionPresentation { get; init; }
    // Issue #803 (ReactionOrdering.cs): the announce / order / resolve procedure of a Reaction window both players hold a Reaction for; PUBLIC, null otherwise.
    public ReactionOrderView? ReactionOrder { get; init; }
    public ProvinceOrderView? PendingProvinceOrder { get; init; }
    // Harsh Choices (issue #581): what destroying the province would do, computed by the engine and PUBLIC -- both players see the same consequence. Null when no Harsh Choices decision is open.
    public HarshChoicesView? PendingHarshChoices { get; init; }
    // Public display context for a battle fought inside the Action Phase. ActivePlayerId
    // remains the battle's attacker; this is the original turn owner, never the paused action.
    public int? ActionPhaseTurnPlayerId { get; init; }
    // Issue #667 (PhaseOrder.cs): the six printed phases by name in the order this turn is running in, PUBLIC and identical for both seats; null while the usual order (Straighten, Events, Action, Attack, Dynasty, End) runs.
    public string[]? PhaseOrder { get; init; }
    // Issue #917 (ResolutionArea.cs): the resolution area, oldest first, PUBLIC and identical for both seats; null when empty so a payload outside an open action is unchanged. Declared last.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResolutionEntryView[]? ResolutionArea { get; init; }
}
/// <summary>Engine-private record of one face-down province card a player looked at (MatchState.ProvinceSights). Turn is the turn number it was looked at.</summary>
public sealed record ProvinceSight(int ViewerId, int OwnerId, int Province, string CardId, string DefinitionId, int Turn);
/// <summary>PRIVATE to the looking player: what the face-down card in a province is (its owner's province Province).</summary>
public sealed record ProvinceSightView(int OwnerId, int Province, string DefinitionId, CardDefinition Definition);
public sealed record ReplayRecord(string ContentVersion, string Scenario, ulong Seed, GameCommand[] Commands, Deck[]? CustomDecks = null, bool IdleBattlePasses = false);
