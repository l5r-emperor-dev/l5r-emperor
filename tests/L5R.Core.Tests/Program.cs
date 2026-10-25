using static RegressionAssertions;
using static AboveStrengthAssertions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using L5R.Core;
using L5R.Client;

var tests = new (string Name, Action Run)[]
{
    ("Gameplay-control quotes preserve distinct methods and private entry candidates", GameplayControlsChecks.SourcesAndPrivacy),
    ("Province slots retain empty/hidden positions and legacy wire compatibility", ProvincePresentationChecks.SlotsAndSerialization),
    ("Province order retains exact engine command and rejects stale/other-seat choices", ProvincePresentationChecks.DecisionFreshness),
    ("Battle receipts include both explicit Dynasty slots", ProvincePresentationChecks.BattleReceipts),
    ("Granted-effect view is public, detached, duration-accurate and absent from hidden cards/older payloads", GrantedEffectPresentationView),
    ("Public hand limit matches End Phase enforcement, follows visible modifiers and preserves hand privacy", PublicHandLimitView),
    ("Duel Demo canonical commands, privacy and retained result", DuelDemoChecks.Canonical),
    ("Own focused cards are inspectable from every source without opponent disclosure", DuelDemoChecks.OwnFocusViews),
    ("Duel Demo scenarios and legal decisions", DuelDemoChecks.Decisions),
    ("Duel stakes and source-linked public consequences", DuelDemoChecks.Story),
    ("Either player may concede while a duel is pending", DuelDemoChecks.ConcedeDuringDuel),
    ("Duel results record effective province strength (Times of Strife's ceiling), matching GetView", DuelDemoChecks.EffectiveProvinceStrength),
    ("Battle journal groups public effects and actual destinations without duplicating snapshots", BattleJournalProjection),
    ("Accepted battle and reaction passes publish their actor; rejected passes remain atomic", PublicPassEvents),
    ("BowedPersonalityResolution/attacker: two Native Guides win and destroy the province", () => BowedPersonalityResolution(0,true,10,1)),
    ("BowedPersonalityResolution/defender: two Native Guides win without destroying the province", () => BowedPersonalityResolution(1,false,1,10)),
    ("Unit view clamps Personality reductions separately from Followers", UnitViewClamp),
    ("UnitViewBows/unbowed Follower contributes with bowed Personality", () => UnitViewBows(false,false,false,5)),
    ("UnitViewBows/both bowed without Elite contribute zero", () => UnitViewBows(true,false,false,0)),
    ("UnitViewBows/bowed Elite Follower contributes independently", () => UnitViewBows(true,false,true,5)),
    ("UnitViewBows/both bowed Elite cards contribute", () => UnitViewBows(true,true,true,7)),
    ("Unit view refreshes after attachment removal and moving home", UnitViewRemoval),
    ("Unit view counts Items once and matches battle resolution", UnitViewResolution),
    ("Unit view is absent outside visible in-play Personalities and old payloads", UnitViewPrivacy),
    ("Starter setup uses four provinces, six-card hands and second-player bowed holding", Setup),
    ("Player snapshots hide enemy hand, all deck order and face-down own provinces", Privacy),
    ("Invalid, duplicate, stale and wrong-priority commands are atomic", AtomicReject),
    ("Exact Gold payment bows only selected sources", ExactPayment),
    ("Excess Gold is discarded and cannot fund another purchase", Overproduction),
    ("Marketplace production is conditional on Crane clan", ConditionalProduction),
    ("Clan discount does not gain Honor or offer Proclaim", Discount),
    ("Clan allegiance ignores requirement and combines with discount", Allegiance),
    ("Proclaim happens after entry and is limited to once per turn", Proclaim),
    ("Declining Proclaim keeps its once-per-turn use available", Decline),
    ("New Holding enters bowed and province refills face down immediately", HoldingAndRefill),
    ("Province discarding prevents returning to purchases", DynastyDiscard),
    ("DynastyActionTaken (issue #659): false when a Dynasty Phase begins, true after a purchase or a province discard, visible in both seats' views, and reset when the next Dynasty Phase begins", DynastyActionFlag),
    ("Action phase needs consecutive passes and defender starts battle", Priority),
    ("Bamboo fixture waits until owner's second turn to straighten", Bamboo),
    ("Turn start passes through Straighten then Events before Action", StraightenAndEventsPhaseOrder),
    ("Choose-straighten fixture lets its controller leave it bowed", ChooseStraightenDecline),
    ("Choose-straighten fixture straightens when not declined", ChooseStraightenDefault),
    ("Battle destroys province only on strictly greater margin", ProvinceThreshold),
    ("Tied armies destroy each other and gain one Honor per opposing card", BattleTie),
    ("Unbowed Follower wins a battle while its bowed Personality contributes zero", FollowerForce),
    ("Bowed Elite Followers still count in unit Force; plain bowed Followers do not", EliteFollowerForce),
    ("A bowed Elite Personality's own Force still counts toward its unit", ElitePersonalityForce),
    ("Turn-duration granted keywords clear at turn end; Permanent grants do not", GrantedKeywordDuration),
    ("A Personality with the enters-dishonored effect enters play Dishonorable", EntersPlayDishonored),
    ("A Personality without an enters-bowed effect always enters play straightened", EntersPlayStraightenedByDefault),
    ("A Personality with the enters-bowed effect enters play bowed directly, not straightened then bowed", EntersPlayBowed),
    ("Seppuku requires Samurai, Courtier, or Shugenja", OrderSeppukuRequiresKeyword),
    ("Seppuku requires a Dishonorable Personality", OrderSeppukuRequiresDishonorable),
    ("Seppuku destroys honorably with no Honor change", OrderSeppukuHonorableDeath),
    ("Seppuku is an Open action taken at the player's own turn to act: refused while the other player holds the opportunity (issue #368), and once taken the opportunity passes", OrderSeppukuIsOpenAction),
    ("Seppuku cannot be ordered for an opponent's Personality", CannotOrderSeppukuForOpponentsPersonality),
    ("A Dishonorable Personality's death (other than Seppuku) costs its controller Honor", DishonorableDeathLosesHonor),
    ("Battle winner gains two Honor per destroyed card including attachments", BattleHonor),
    ("Successful attacking units and attachments return bowed; defenders stay", BattleReturn),
    ("Last province destruction immediately wins military victory", MilitaryVictory),
    ("Honor victory waits for beginning of owner's turn", HonorVictory),
    ("Latched dishonor defeat resolves before end-phase draw", DishonorTiming),
    ("End phase requires exact hand-limit discard", HandLimit),
    ("Battle Strategies require presence and valid local targets", StrategyLegality),
    ("Terrain plays as a Battle action, caps at one per battlefield, is targetable, and leaves at resolution", TerrainPrimitive),
    ("Equip validates Personal Honor, payment and action priority", Equip),
    ("Temporary Force expires at turn end", ExpiringForce),
    ("Same seed and accepted command replay reproduce complete state", Replay),
    ("Different seeds change hidden deck order", DifferentSeeds),
    ("Unsupported definitions and replay versions fail explicitly", Unsupported),
    ("Public snapshots and diagnostic state cannot mutate engine", Detached),
    ("Empty decks do not remove provinces or create cards", EmptyDecks),
    ("ActionQueries/economy: every enabled exact command validates on a replay", () => ActionQueries(MatchEngine.CreateEconomyScenario())),
    ("ActionQueries/starter: every enabled exact command validates on a replay", () => ActionQueries(MatchEngine.CreateStarterMatch())),
    ("Invalid later assignment rolls back earlier valid assignment", AssignmentRollback),
    ("Zero-Force battle with an empty side has no outcome", EmptyBattle),
    ("Proclaim use becomes available on a subsequent turn", ProclaimReset),
    ("Scripted training match completes with replay and invariant checks", CompleteMatch),
    ("Journey's End Keep moves home a Personality with no more Followers than the performer, not one with more", JourneysEndMove),
    ("Shamate Keep moves home a Personality with equal or lower Personal Honor; gains Honor only if uncontrolled", ShamateMove),
    ("Plains of the Maiden gains Honor per surviving Battle Maiden after the Attacker destroys an army", PlainsOfMaiden),
    ("Law of Darkness Dojo weakens and bows a dishonorable enemy; punishes further Honor loss from a Battle action", LawOfDarknessDojo),
    ("Attack targeting (core rule): a target above the Attack's strength is a legal target that survives; no target is still fine; Honor/bow effects of the action are unchanged", AttackMayTargetAboveStrength),
    ("Carpenter Castle's Ranged Attack is legal with no target in sight; destroys a legal target and gains Honor if it targeted a card (Mendon, issue #335)", CarpenterCastleAttack),
    ("Shiro Shiba's Melee Attack strength rises with a controlled Shugenja", ShiroShibaAttack),
    ("Tokens create/move/destroy, persist across turns unlike Modifiers, and are removed when their host leaves play", TokenPrimitives),
    ("A discarded dishonorable Personality can become honorably dead: rehonored, no Honor-loss penalty", MakeDeadFromDiscard),
    ("A Stronghold's Honor-loss immunity negates a matching Clan or Fate source the losing player owns, and only that", HonorLossImmunity),
    ("Meiyu's entering-play reaction composes with Proclaim, each usable once, before the window closes", MeiyuEntersPlayReaction),
    ("Monastery of New Thought grants a Fire token to a Tattooed Personality and destroys one to fuel its Ranged 5 Attack", MonasteryFireTokenAndAttack),
    ("Togashi Torazu's entering-play reaction and self-performed Fire-token Ranged Attack need no Performer", TorazuFireTokensAndAttack),
    ("An Attack's strength is mutable state: a Reaction may negate or boost it before resolution, when one is available", AttackReactionWindow),
    ("A hand-played Strategy or Ring Attack is discarded once as its reaction window opens, and the immediate path is unchanged (issue #320)", HandPlayedAttackSpentWhenWindowOpens),
    ("Import wave 1: four unconditional Ranged/Melee Attack cards need only catalog wiring", UnconditionalAttackImportWave),
    ("Tsuruchi Isas bows himself, Ranged 3 Attacks, and moves home after regardless of the hit", TsuruchiIsasAttackAndMoveHome),
    ("Import wave 2: Bayushi Kahoku's Attack strength and own Force both key off an enemy's dishonorable status", BayushiKahokuAttack),
    ("Attack-vs-Followers exception, default rule (P1): a Personality with an attached Follower is untargetable by a Ranged AND a Melee Attack (with the rejection naming the Follower), an Item or Spell attachment never blocks, the lone Follower and a bare Personality stay legal targets, and an Item beside a Follower does not change the answer", AttackFollowersDefaultRuleBothKinds),
    ("Attack-vs-Followers exception, shapes (P1): Tsuruchi Kosoko - exp's printed unconditional exception (whole unit destroyed with the Personality; the exception lifts only the Follower immunity, never the strength check; the Personality's OWN Force is compared -- the AttackComparedForce decision point); the exception evaluator over every kind (Ranged/Melee/Both) x condition (Reconnaissance at the CURRENT battlefield, the acting player's own) x target status (dishonorable) combination, granted exceptions belonging to their own player, an unknown condition failing loudly; and Ranged/Melee end to end through real commands with the grant spent by the Attack it covered", AttackFollowersExceptionShapes),
    ("Attack-vs-Followers exception, printed conditional shapes (P1), end to end through a training fixture: a Ranged Attack printed with the Reconnaissance condition (only at the CURRENT battlefield) and a Melee Attack printed with the dishonorable-target condition, each scoped to its own ability and kind", AttackFollowersPrintedConditionalShapes),
    ("Bayushi Kahoku - exp (oracle-l5r-807): the source-scoped, target-status-gated Follower exception (dishonorable followed Personalities only, honorable ones stay immune, status read live) and his two-selection Battle action (-4F on TargetId, the Ranged 3 Attack on CardIds: separate targets or the same one, the strength check seeing the same action's -4F, an optional Attack half, malformed selections rejected), no leak to Attacks performed by any other card (Asako Ume, the non-experienced Kahoku; the evaluator compares the PERFORMER; Ranged only), and the shared reaction window with the declared target fixed", BayushiKahokuExpTrait),
    ("Yoritomo Ebumi (oracle-l5r-9832): the granted-exception hook (P1 shape (e)) -- a standing Ranged grant made by an action and spent by the player's NEXT Ranged Attack action (needed or not; a Melee action leaves it alone; a later Ranged Attack is back to the ordinary rule), an additional Battle action only with Reconnaissance at the current battlefield (not another province's, not the enemy's), an unused grant cleared when the battle resolves", YoritomoEbumiGrant),
    ("Target-status predicate (P2): honorable/dishonorable (Personalities only, one representation), keyword-only, clan-only vs clan-or-keyword, any-of combinations counted once, none matching, an empty specification; as an EFFECT condition through Dispensing Justice, Winds of Dismissal (each old outcome kept, plus the intended fix for a Shadowlands-KEYWORD-only card, Asako Rikate) and the non-experienced Kahoku's +2 strength; and as a legality layer", TargetStatusPredicate),
    ("Personality wave 24: Kakita Kazan's TargetMustBe legality layer (enemy dishonorable only) and rehonor/Stalwart interplay; Hiruma Nitani's Reconnaissance-conditional bow and Shadowlands +2 strength (clan and keyword); Kuni Shinoda - exp's bow-then-destroy-if-Shadowlands (Personality unit or lone attachment)", PersonalityWave24Cards),
    ("Import wave 2: Asako Ume's Force bonus keys off carrying a Spell; her Attack is unconditional", AsakoUmeAttack),
    ("Reconnaissance defaults to false per player per province, is granted independently of other players/provinces, and gates only the dependent portion of an effect", Reconnaissance),
    ("Reconnaissance persists through a province refill and clears only when its battle resolves, destroyed or not", ReconnaissanceOnlyClearsOnBattleResolution),
    ("Tactical Advantage requires a performing Tactician, is usable any number of times per turn, and gives Force equal to the discarded card's Focus Value", TacticalAdvantage),
    ("Conqueror straightens the sole eligible attacker automatically; with two eligible, the attacker must choose", ConquerorStraightensAfterVictory),
    ("Kensai raises the Weapon cap to two; Kama independently raises it to two but does not stack with Kensai", KensaiAndKamaWeaponCap),
    ("Cavalry Maneuvers follows Infantry Maneuvers; a unit is Cavalry only if the Personality and every attached Follower carry the keyword", CavalryManeuversSegment),
    ("Infantry and Cavalry defender queries only offer existing battlefields, and every offered assignment executes", DefenderAssignmentQueries),
    ("A Two-Handed Weapon overrides Kensai and Kama entirely: at most one Weapon whenever one is involved, either way round", TwoHandedWeaponOverridesKensaiAndKama),
    ("Daigotsu Kendo's Conqueror keeps him unbowed after a win; Ikoma Natsu's Cavalry assigns during Cavalry Maneuvers", DaigotsuKendoConquerorAndIkomaNatsuCavalry),
    ("Mirumoto Reiyu gives an enemy Force/Follower a Force penalty equal to her own current Force", MirumotoReiyuForcePenalty),
    ("Shinjo Horibe may bow or straighten any Personality, but only while opposed", ShinjoHoribeBowOrStraighten),
    ("Doji Atsumichi may bow or straighten a Personality with lower Personal Honor than himself", DojiAtsumichiBowOrStraighten),
    ("Tetsuo destroys an attachment, and separately an enemy Personality with fewer Weapons than himself", TetsuoDestroyAttachmentAndWeakerWeaponed),
    ("Moto Shunsuke's Force bonus tracks the largest single dead-Personality pile (issue #221: the shared DeadCount helper, and PlayerView.DeadCount exposing the same authoritative per-player count for a future UI/card to read); his Limited ability only targets another player's discarded Personality", MotoShunsukeForceBonusAndMakeDead),
    ("Equip (Battle) attaches a Spell to an opposed unbowed Shugenja, opens a bounded immediate follow-up window for its Battle ability, and is usable any number of times per turn", SpellsAndShugenja),
    ("Unique/Singular (issue #202): both restrict simultaneous in-play copies of the same-named card to one per controller (Unique's own in-play restriction had no test before this), checked at all three entry-into-play paths -- Purchase, ordinary Equip (a real pre-existing gap for both keywords, fixed here), and Equip (Battle)", UniqueAndSingularInPlayRestriction),
    ("Loyal follows effective Clan affiliations; a Personality with none remaining is Unaligned and controllable by any Clan", LoyalKeyword),
    ("Experienced levels and Overlay recruitment: any higher level replaces the same-titled Personality for free, ignores Honor Requirement, transfers its complete state, and removes the lower level", ExperiencedKeywordAndOverlay),
    ("A keyword with no mechanical ability of its own still satisfies a generic 'choose your performing/target [Keyword] Personality' condition, on both Target and Performer selection", KeywordSatisfaction),
    ("Strategy wave 1: Immovable Object (Battle/Open dual timing, both abilities), Blind Rage, Chokehold, and Unorthodox Attack", StrategyWave1KeywordCards),
    ("Follower wave 2: 11 cards found by auditing every not-yet-imported Follower for ones needing nothing beyond already-built engine pieces", FollowerWave2Cards),
    ("Follower wave 3: 28 cards, including the three real cards behind this session's aggregate/group Force effects (Senior Infantry Legion, Ashigaru Elite, Utaku Elite Guard) and province adjacency's first consumer (Moto Gunso); AttachGrantsKeyword's first published use; several new small primitives (Force-swap, destruction-triggered Honor, wielder-keyed Follower Force, Melee-only immunity, compound OR/controller-scoped targets)", FollowerWave3Cards),
    ("The 'after entering play' Reaction window now opens from Equip and a played Terrain too, not just Purchase; it is repeatable per entry, and correctly cedes priority to the opponent for Action-phase entries", GeneralizedEntryReactionWindow),
    ("A Follower's own static conditional Force stays with the Follower (visible for targeting even bowed, joins the unit total only unbowed/Elite); an Item's printed and conditional Force/Chi always convey to its wielder regardless of the Item's own bow state", FollowerAndItemStaticForceHooks),
    ("'Boost other Followers in the unit' primitive (Senior Infantry Legion's shape): a static +1F to every OTHER Follower attached to the same Personality, never to itself, and never leaking into a different unit; two boosters in one unit grant each other but not themselves", UnitForceBoostPrimitive),
    ("Ashigaru Elite's shape: an activated, tireless, stacking Battle ability -- bow an unbowed Farm to give +1F to every Ashigaru-keyword card the performer controls at the current battlefield, army-scoped (not just one unit); unaffected: a non-Ashigaru card, and the enemy's own Ashigaru card; the per-battle counter resets once the battle resolves", AshigaruEliteBoostPrimitive),
    ("Expendable/Destined/Kharmic keyword mechanisms: a mandatory Fate-deck draw on destruction (not a plain discard) for Expendable; a mandatory Fate-deck draw on entering play (Purchase or Equip alike) for Destined; a repeatable pay-2-Gold-and-discard action for Kharmic, refilling a Dynasty province face up or drawing an extra Fate card", KeywordMechanics),
    ("Passive Attack-strength defense (Destrier/Singh Remnants' shape, Scope Self/Unit/Wielder, kind-specific) and a new 'any Personality enters play' Reaction letting a card in hand or a discard pile attach itself (Singh Remnants' shape), following Equip's own restrictions including Personal Honor", PassiveAttackDefenseAndEntryWatch),
    ("Follower wave 5 / Item wave 1: Scourge of the Sea, Unparalleled Sword, and Justice of the Crane -- three more AttachGrantsKeyword carriers (Naval/Kensai/Magistrate), GrantKeyword's idempotency guard, a new Battle Dishonor action, and (once Additional Actions exist) Justice of the Crane's own printed-Magistrate bonus", GrantsKeywordWave),
    ("The Additional Action primitive (issue #181): Again!'s unscoped grant, Two Hands' unit-scoped grant and its 'no new grants this battle' restriction, Effortless Counterattack's negating Reaction and its own unrelated move-home Battle action, and Equip (Battle)'s bespoke follow-up window replaced by a genuine scoped grant", AdditionalActionPrimitive),
    ("Stalwart (issue #184): the first enemy-caused bow each turn is negated (and only the bow), a second succeeds, the card's own controller bowing it is never affected, and a stale marker from an earlier turn does not block this turn's negation", StalwartKeyword),
    ("Reserve (issue #184): a Dynasty Personality recruited as an absent battle action straight to the battlefield (no presence of the recruiter's own required, only enemy presence, face up only), and a non-Spell Fate card attached via Equip (Battle) the same way a Spell is, but without the bonus Additional Action", ReserveKeyword),
    ("Discipline (issue #184): a card played from a discard pile, paying its own printed Gold cost, resolving its effect, then removed from the game (the new Zone.RemovedFromGame/PlayerView.RemovedFromGame) rather than left in the discard pile; unusable from hand; unaffected by an earlier, unrelated discard the same turn", DisciplineKeyword),
    ("Dishonor/Rehonor (issue #186): the Personal-Honor override (Utaku Chikako's shape), GainHonorFromPerformer negating a dishonorable performer's own Honor-granting ability and rehonoring them instead, and battle resolution rehonoring a dishonorable winner/tier and negating the full Honor gain (a tie's rehonoring happens before its own mutual destruction, so no double penalty)", DishonorRehonorMechanics),
    ("Utaku Chikako (issue #186): the real card's own Personal-Honor override, and her Limited ability (gain 2 Honor; may not declare an attack this turn, a broad restriction on the assignment itself that resets next turn)", UtakuChikako),
    ("Kiho keyword and discard-to-hand primitive (issue #190): Volcano Tattoo's own Fire Kiho Battle ability (Togashi Meiyu as the Tattooed Monk performer), and Hunter of Harmony returning it from the Fate discard pile 'freshly drawn' so it may be used again the same turn", KihoDiscardToHand),
    ("Multi-keyword Target/Performer requirement (issue #190): a card may name any number of keywords, all independently required regardless of order (e.g. 'a Tattooed Monk') -- a card with only one of the two required keywords does not qualify", MultiKeywordRequirement),
    ("Goju House Guard and Aberrations (issue #192): the new deck-return primitive (top or bottom, controller's choice for Goju itself; unattached unit cards always to the bottom of their owner's appropriate deck, fully reset) and top-of-Fate-deck discard primitive (Force bonus equal to the discarded card's Focus Value)", GojuHouseGuardAndAberrations),
    ("Focus-1 Battle-action wave (issue #192): Sankaku-Yari (target-keyword-conditional Attack strength, zero new primitives), Pearl of Embers (BattleOrOpen, zero new primitives), Ritual Preparation (the new own-unit-card Target category -- a Personality OR Follower, own, in play)", FocusOneBattleActionsWave),
    ("Enlightenment victory (issue #194): all five real elemental Rings in play at the start of a turn wins; a False Ring's own matching elemental keyword never substitutes, even for a missing real Ring -- Fudo is the actual exclusion", EnlightenmentVictory),
    ("Ring of Air (issue #194): two Favor actions in one turn grants an Additional Battle Action scoped to the Ring rather than a forced interrupt; recruiting it, then its own Repeatable straighten action", RingOfAir),
    ("Ring of Water (issue #194): a battle resolving with a Terrain played, a Terrain destroyed, and something destroyed grants an Additional Battle Action scoped to the Ring, surviving battle-end cleanup", RingOfWater),
    ("Ring of the Void (issue #194): a Limited ability gated on two or fewer Rings in play, discarding the rest of the player's hand as its own cost", RingOfTheVoid),
    ("False Ring of Air (issue #194): winning a battle with an opposing unit present grants an Additional Battle Action scoped to the Ring; recruiting it costs 2 Honor, unlike a real Ring", FalseRingOfAir),
    ("False Ring of Water (issue #194): a standing Province-Strength-11-or-greater condition, not a Reaction/grant -- usable any time it holds, like an ordinary Battle ability", FalseRingOfWater),
    ("Review of previously Ring-blocked cards (issue #194): Moshi Ikako's 'Fire Battle' is a plain classification, not Ring-gated (confirmed, zero new primitives); Scholarly Hermit needed only a Ring-type check in a discard pile (new own-ring-in-discard Target)", RingBlockedCardsReview),
    ("Ring of Earth (issue #194, revalidated once its own scan became available): a won battle with enemy units present grants an Additional Battle Action scoped to the Ring; its own in-play action waives the ordinary Battle-timing presence check entirely and may raise or lower either side's own province strength by 3 (a new 'any-province' Target, generalizing the existing enemy-only category)", RingOfEarth),
    ("Ring of Fire (issue #194, revalidated once its own scan became available -- resolving issue #195's own text uncertainty by reading the scan directly rather than the mismatched scrape): destroying a province or enemy card in a battle's own resolution (no win required, like Ring of Water) grants an Additional Battle Action scoped to the Ring; its own in-play action destroys its own performing Personality as part of the cost (DestroyUnits, already fully generic) and a target enemy Personality with equal or lower Chi than the performer (enemy-personality-chi-lte-performer, Chokehold's own existing Target)", RingOfFire),
    ("Sensei keyword (issue #207 follow-up): 'after this card enters play, discard all your other Sensei Personalities' -- a Home card discards directly, a face-up province card discards and refills, a face-down copy is untouched (hidden information), and the entering card is never its own 'other'", SenseiKeyword),
    ("Mirumoto Yozo, Experienced 2 (issue #207 follow-up): 'after your Iaijutsu action resolves, give any Personality who performed it +2F' -- a new per-ability-id IsIaijutsuAction classification (every currently self-performer duel-issuing ability), applied via ApplyModifier regardless of the duel's own outcome", YozoIaijutsuBonus),
    ("Mirumoto Hikaro (issue #207 follow-up): 'before you focus in a duel involving your Dragon Clan Personality, once per duel, you may focus from your discard pile instead' -- revealed to the opponent immediately (unlike an ordinary hidden focus) and removed from the game after the duel ends, not the ordinary FateDiscard", HikaroDiscardPileFocus),
    ("Mirumoto Rokai (issue #207 follow-up): 'before an action resolves, negate its own destruction' -- a new defer-then-reexecute 'beforeActionReaction' primitive (the same shape PendingAdditionalAction already uses) so MoveHome/Straighten/DestroyUnits are never reached until the watcher's own controller answers, scoped to another player's action, Rokai's own controller's Personality, and Rokai's own location", RokaiNegatesDestruction),
    ("Tamori Tamoko's own Reaction (issue #207 follow-up): 'before an action resolves, negate its own movement or straightening' -- the same beforeActionReaction primitive as Rokai, but with no 'another player's,' location, or cost restriction at all", TamokoNegatesMovementOrStraightening),
    ("Tamori Tamoko's own Open action (issue #207 follow-up): 'if it is not your turn, target an unbowed Personality controlled by the active player: before the turn ends, gain 1 Honor if he was not assigned to attack your provinces this turn' -- a new turn-stamped CardInstance.AssignedToAttackTurn flag, checked unconditionally in EndTurn", TamokoOpenHonorGain),
    ("Togashi Mitsu (issue #207 follow-up): 'Fire Battle/Open, give Mitsu +1F Fire tokens until he has exactly five' and 'Fire Battle: destroy zero or more Fire tokens on Mitsu, Ranged Attack with strength equal to the number destroyed' -- a new strengthOverride parameter on ResolveOrDeferAttack, and a CardIds carve-out in the 'attack' Target's own validation naming which Fire tokens to destroy", TogashiMitsuVariableAttack),
    ("Togashi Sakata (issue #207 follow-up): 'Limited, give Sakata a +1F Fire or Sake token' (a UsageKey-shared choice) and 'Ranged 4 Attack, +1 strength for each Sake token on Sakata' -- only Sake tokens count toward Attack strength, via the pre-existing but previously-unused TokenCount helper threaded through AttackStrength/EffectiveAttackStrength's new sourceId parameter", TogashiSakataTokenAttack),
    ("Xijkt (issue #207 follow-up): 'may assign during Cavalry Maneuvers if you are the Defender' (an additive-only Cavalry-eligibility override via Assign's new isDefender parameter) and 'bow Xijkt or one of your Mines' (the general own-source-or-unbowed-holding-with-keyword cost-choice Performer category, with the forced-pathway case emerging for free from ordinary eligibility validation)", XijktCavalryDefenseAndBowChoice),
    ("Tamori Shosei (issue #207 follow-up): 'if you control a Temple, bow Shosei or one of your Temple Holdings: gain 1 Honor' -- the same cost-choice shape as Xijkt, plus a separate control-gate (a bowed Temple still counts as controlled for the gate, but is itself ineligible to pay the cost)", TamoriShoseiBowChoice),
    ("Utaku Elite Guard's shape: a static +1F, keyword-based (Battle Maiden) and army-scoped (every OTHER Battle-Maiden card the owner controls at the same battlefield, not just one unit) -- unlike Senior Infantry Legion's type-based, unit-scoped boost; unaffected: a non-Battle-Maiden card, a Battle Maiden not at this battlefield, and the enemy's own Battle Maiden; two guards in one army grant each other but not themselves", ArmyBattleMaidenBoostPrimitive),
    ("An attachment's 'Will only/not attach to X' restriction (keyword, the Human-is-not-Nonhuman special case, or a named Personality) gates Equip, both at Accept and in the offered legal-targets list; the Follower Honor Requirement now reads current, not printed, Personal Honor -- fixed at 0 while Dishonorable, with a Personal-Honor-boosting token still applying once honorable", AttachmentRestrictions),
    ("'Without attachments' is a boolean, not a count: any Item/Follower/Spell, or a Follower/Weapon/Armor token, blocks a Ranged/Melee Attack target and any ability flagged RequireNoAttachments (layered onto an existing category); an ordinary stat token does not, and an attachment itself is always a legal 'without attachments' target", NoAttachmentsFilter),
    ("Province adjacency: a destroyed province is skipped, its former neighbors become directly adjacent, and this compounds across multiple destructions -- matching every worked example given, including both ends destroyed", ProvinceAdjacency),
    ("Lion Clan Personality wave 8 (issue #198): 17 cards from the full Lion Clan Personality review, wired using only already-existing primitives -- new Target categories (enemy-personality-lower-honor-than-self, personality-chi-lte-self, own-lion-or-fallen-personality, enemy-card-without-attachments, enemy-unit-card-terrain-conditional), a reused enemy-attachment category, clan-conditional Ranged Attack strength, a token-gated Additional Action grant, entry Honor loss via the real Dynasty-purchase path, and two passive Force modifiers (attacking-vs-dragon-bonus, force-with-own-terrain-at-battlefield)", LionClanPersonalityReview),
    ("Dueling (issue #204): the challenge/accept-or-refuse/reaction-window/focus-or-strike/Duelist-refocus/Focus-Effect-ordering/resolution state machine, wired against Steel on Steel and The Courtesy of the Crane -- reproducing the user's own worked example exactly (Doji Tatsuki 8 vs. Matsu Hachiro 7), plus a refusable-training-fixture decline, the four-focus cap, and both Duelist tie-break outcomes (wins alone; both lose when both/neither side has it)", Dueling),
    ("Dragon Clan Personality review (issue #207): 34 cards from a full review of all 79 catalogue Dragon Clan Personalities (69 previously unwired) -- three real self-performer Iaijutsu duels (the marquee use case for Dueling, just landed), StraightenUnit/MoveToBattlefield primitives, and several new Target categories (dishonorable-personality, enemy-card-force-lte-self, personality-force-lt-self, enemy-force-non-cavalry, personality-with-weapon, bare enemy-card, enemy-card-without-unbowed-attachments)", DragonClanPersonalityReview),
    ("'After a duel resolves' Reaction trigger (issue #209): own-won-duel, scoped to the one specific card that won -- a duel is a single, complete action from challenge to consequences, the same shape own-enters-play already uses for entering-play Reactions, closing on two consecutive passes. Kitsuki Daisuke, Experienced (draw a card, used); Kitsuki Horume (gain 1 Honor, declined); Kitsuki Jakuei (take the Imperial Favor, used); plus Jakuei's own separate Political Battle ability, a dual-choice pair (bow only / bow-and-discard-the-Favor-to-also-move-home-bowed) gated on actually controlling the Favor", DuelWonReaction),
    ("Mandatory vs. optional 'after X enters play' text (issue #210): CardDefinition.EntryHonorLoss applies bare, un-prefixed 'lose X Honor' text automatically with no choice offered (the generic fixture); the new 'player' Target category and GameCommand.TargetPlayerId let an actual own-enters-play Reaction choose a player to gain or lose Honor (the generic dual-UsageKey fixture, and Kitsuki Horume's real first ability), with ApplyPlayerHonorChoice substituting Rehonor for a Dishonorable performer's own gain exactly as GainHonorFromPerformer already does, and the shared UsageKey correctly deduping the gain/lose pair to one use per entry", PlayerTargetingPrimitive),
    ("Protection (issue #212): a general action-effect negation-immunity status (Tamori Takeshi's 'effects of actions the target performs will not be negated'), checked at the point a negation would otherwise apply -- the same 'still legal to play, its own effect just becomes a no-op' shape StalwartNegatesBow already uses -- against both existing negation points in the engine (Effortless Counterattack's Additional-Action negation; the Attack-strength-negation Reaction), generalizing PendingAttack's own performer-tracking (an attached Weapon resolves to its wielder) along the way so every Attack, not just the two cards that needed it before, records who actually performed it. Cleared at FinishTurn, the same as a Turn-duration GrantedKeyword, and Tamori Takeshi's own real Open ability grants it", ProtectionPrimitive),
    ("Issue #216: Brash (the Defender may draw a card, once per Brash card assigned to attack); Force redirection (Togashi Noboru -- a targeted enemy Personality's own Force, never its Followers', counts toward Noboru's controller's army instead, live-rechecked at resolution against 'while you have at least one unit in your current army'); the new 'after a bow resolves' Reaction trigger family (Togashi Hizumi -- location-scoped on the trigger, negating that same Personality's own next straightening, which Straighten() now correctly clears once consumed rather than blocking every attempt the rest of the turn; Tamori Ruya -- actor-scoped on the trigger instead, bowing an unrelated Personality at her own location, Home or a battlefield); Tamori Yayu (a bow-or-destroy dual-cost Ranged Attack restricted to his own attached Spells); Mirumoto Katagi and Tamori Shinji (single-card Gold discounts -- a Non-Unique Weapon attaching to Katagi, and Shinji's own Reserve-while-Defender recruit)", BrashForceRedirectAndBowReactions),
    ("Overconfident (issue #223): each other player may optionally draw a card after an Overconfident card enters OR leaves play -- a queue (PendingOverconfidentDraws) populated from the single MarkDead choke point (leaving play; 'leaves' means dying while actually InPlay, excluding both a province/deck discard via DestroyProvince and MakeDead applied to a card already sitting in a discard pile) and from Purchase/RecruitReserve (entering play), drained at every point the engine would otherwise hand back priority (PassReaction's own window-close, Purchase, RecruitReserve, ExecuteAbilityEffects, FinishBattleResolution via the new FinishBattleReturnHomeAndTransition split, ResolveDuel, OrderSeppuku), including a tie battle queuing separate sequential offers for both sides. Kitsuki Minori's live Keyword() suppression (no Overconfident at all, in or out of play, while the other player is Lion Clan); Asahina Tsugio/Asako Kijo's MaxHandSize (raises only an opponent's own hand-size threshold, never the controller's own); Bayushi Mugoshi's own entering-play Reaction chaining into the Overconfident offer once its own window closes", OverconfidentPrimitive),
    ("Event wave 1: Suspicions, the first Event resolved through the Events-Phase dispatch (ResolveEvent) -- revealed, removed from its province and refilled immediately, both players lose 1 Honor (revealing player first) the same turn it is revealed; the ongoing effect fires before each of its owner's later Action Phases (never on the opponent's turn), with the card never leaving the new public Zone.EventInPlay/PlayerView.EventsInPlay for the discard pile", SuspicionsEventInPlay),
    ("An Event already face up in a province (refilled face up) resolves at its owner's next Events Phase instead of staying stuck", FaceUpEventResolvesInEventsPhase),
    ("Region primitive: a Region attaches to a PROVINCE (the new Zone.Region/ProvinceState.RegionId), not to a card; a second Region revealed at an already-regioned province is discarded instead of replacing the first; destroying a province discards whichever Region was attached to it and clears the pointer", RegionAttachmentPrimitive),
    ("Region wave 1: Fallow Lands -- a static province-strength bonus live-recomputed from the attached Region (EffectiveProvinceStrength), applied immediately without respect to presence; reveal/attach/refill mechanics; its Reaction weakens a Melee/Ranged Attack by 2, usable the same turn it enters play, gated on the Attack actually being at Fallow Lands' own province with its controller's own presence there (a Region never satisfies presence itself)", FallowLandsRegion),
    ("Region wave 1: Low Market -- an Open ability (not the ordinary Limited-only Equip() command, not a PlayerAbility like Equip (Battle)) that bows itself as its entire cost/limiter, Equips a targeted Item/Follower/Spell from hand to a chosen Personality at a genuine 2-Gold discount floored at a minimum of 1", LowMarketEquip),
    ("Celestial primitive: enters play immediately on reveal (Zone.Celestial), province refills; a new Celestial entering play discards every OTHER Celestial the SAME player controls, never the opponent's", CelestialAttachmentPrimitive),
    ("Celestial wave 1: Lady Matsu's Rage's own destroy-redirect trait, wired into DestroyUnits' single shared choke point -- redirects a Ranged/Melee Attack's own destroy to RemovedFromGame when the attacker controls her, but never battle resolution's own Force-comparison casualty sweep, even in a battle she controller declared", LadyMatsusRageDestroyRedirect),
    ("Celestial wave 1: Lady Matsu's Rage's Battle action -- discards herself as its own cost, bows a target enemy card, and (tracked via the new MatchState.PendingLadyMatsusRageWatches, checked reactively from Straighten() itself) destroys it if it straightens later the same turn; that delayed destroy is an ordinary death, not redirected, since she has already left play by the time it fires", LadyMatsusRageBattleAction),
    ("Spell wave 1: Suitengu's Path -- a pure AttachGrantsKeyword carrier (Naval), the first published Spell to use that mechanism, attached via the real Equip (Battle) path", SuitengusPathNavalGrant),
    ("Spell wave 1: Scouring Flood -- a text-described static +2F trait (PersonalityForce's own new Effect-keyed Spell term, since unlike an Item a Spell never prints its own Force stat) applying whether bowed or not, plus a Battle ability that bows itself as its entire cost and moves a targeted defending Personality home", ScouringFloodForceAndMoveHome),
    ("Spell wave 1: The Earth's Hunger -- a cost-choice Battle ability (own-wielder-or-source-with-wielder-keyword, Xijkt's own shape mirrored the other way round): normally bows the wielding Shugenja, but may bow the Spell itself instead when the wielder carries the Earth keyword", TheEarthsHungerCostChoice),
    ("Personality wave 17: Kitsu Akai's Limited action -- the engine's first 'create a card' effect (MatchEngine.CreatePersonality), producing a Lion Ancestor straight into Zone.Home with the correct stats/keywords, no Proclaim window, once-per-turn Limited usage, and no restriction on coexisting with an already-in-play copy", KitsuAkaiCreatesLionAncestor),
    ("Personality wave 17: Kitsu Akai's Battle ability -- 'bow your performing Ancestor Personality' (own-unbowed-personality-at-battle-with-keyword) rejects Kitsu Akai himself (no Ancestor keyword) but accepts the created Lion Ancestor as the cost-payer, destroying an enemy card without attachments and gaining 1 Honor", KitsuAkaiBattleAncestorCost),
    ("Personality wave 17: a created card (Lion Ancestor) follows ordinary Personality destruction rules -- including the Dishonorable-death penalty -- through battle resolution's own casualty sweep, then is removed from the game rather than left in a discard pile, since it has no physical printed card to return to", LionAncestorDestructionRemovedFromGame),
    ("Event wave 2: Formal Apology -- a one-shot 'Honor below starting value' watch, checked both immediately at entry and continuously afterward through GainHonor's own single shared choke point, so it fires the first time it genuinely happens, not only at a snapshot moment", FormalApologyWatch),
    ("Event wave 2: Harsh Choices -- the first Event needing a genuine player decision at reveal time (destroy this province for 3 cards, or resolve as an ordinary discard-and-refill Event), deferred through a new PendingChoice rather than resolving unconditionally", HarshChoicesProvinceDestroy),
    ("Event wave 2: Rebuilding Kyuden Hida -- exercises only the new bounded 'until your second turn from now begins' duration (counted in the controller's own TurnsTaken, not raw shared TurnNumber); its own restriction needs no enforcement code, verified already the engine's unconditional default", RebuildingKyudenHidaDuration),
    ("Event wave 2: Times of Strife -- a global province-strength CEILING (EffectiveProvinceStrength's own new clamp, live-visible through GetView) plus a discard trigger for 'another player who controls no units at the current battlefield announces a Battle action', queued through the existing Overconfident-draw drain", TimesOfStrifeProvinceClampAndDiscard),
    ("Event wave 2: War Weariness -- bows the performer of the first Personality-performed action each phase even when that action's own printed text never bows its performer, but not a second one the same phase", WarWearinessFirstActionPerPhaseBows),
    ("Second City unwired-card audit, Strategy wave 6: five Battle-timed Strategy cards (Dispensing Justice, Bow Before My Will, Surety of Purpose, Unexpected Support, Wrathful Ancestor) found fully buildable with pre-existing primitives -- the generic RequiredKeywords Performer shape, exemption-by-omission from Tamoko's own curated negation list, and one new enemy-scoped Follower-count Target category", SecondCityStrategyWave6),
    ("Second City unwired-card audit, Personality wave 18: ten Personalities found fully buildable with pre-existing primitives -- Keppo's own Reconnaissance-gated fizzle, Kuronada's own attachment-restricted destroy, Matsu Kasei reusing 'incapacitated' verbatim, Moto Ming-Gwok's own dead-Personality-gated Attack, Shiba Kudome's own Bow-the-loser self-performer duel, Shosuro Ritoru's own compound bow-then-conditional-destroy, Takasho's own reinforce-and-grant plus separate Attack, and Yogo Adi's own dishonorable-or-bulk-province-boost choice", SecondCityPersonalityWave18),
    ("Second City unwired-card audit, the final rest batch (Holding wave 1, Item wave 3, Region wave 2, Spell wave 2, Strategy wave 7): thirteen cards found fully buildable with pre-existing primitives -- Archery Range/Flesh Cutter Arrows' own before-attack-resolves Reaction; Slanderer's Courtier-or-Magistrate-gated dishonor; Temple of Hotei's plain 'shugenja' Performer; Tranquil Garden's new ring-in-play-or-own-discard Target; Fubatsu Plate's own paired even-if-bowed abilities; Nightingale Blade's conditional attachment-destroy; Satoshi's Dual Warfans' new lion-clan-discount Equip term plus its own entering-play draw; Overgrown Grove's own province-index Require (never AtBattle-eligible); Intervening Spirit's reinforce-from-elsewhere plus conditional additional action; The Earth Flows' Defender-only reinforce and its own new CheckEarthFlowsBonus battle-victory Honor bonus; Winds of Dismissal's own wielder-bow cost; Strategic Sacrifice's own new force-lte-province-strength Target plus its own bow-or-straighten toggle", SecondCityRestBatch),
    ("Battle/Open presence primitive (issue #267): a printed Battle/Open ability's Open half carries no battlefield-presence requirement at all (performed from Home); the SAME ability, used during the Battle phase, now correctly behaves like an ordinary Battle ability -- actingAsBattle generalizes every existing Timing==\"Battle\" presence Require (blanket army presence, Personality-source AtBattle, Target-must-be-at-battle, and the keyword-Performer categories' own AtBattle checks) to also fire for BattleOrOpen during Phase.Battle, with zero behavior change for any existing Battle-only card. Proven end to end on The Weakness of Man (Strategy wave 8, a new force-lte-... sibling Target comparing against the performer's own BASE Force) plus closes a real coverage gap the generalization itself surfaced on Immovable Object (Strategy wave 1) and Mirumoto Houken, Experienced (a genuine regression this pass caught and fixed: his own 'move to the current battlefield if he would be opposed there' needed the same reinforce-from-elsewhere exemption Michi/Rikyou/Intervening Spirit already use). Also builds the sibling \"at any location\" presence exemption (AtAnyLocation: Home, the current battlefield, or another battlefield that has not yet resolved this Battle phase -- broadened, not removed) and proves it on Improved Defenses (Strategy wave 9), an ordinary Battle-only card", BattleOpenPresencePrimitive),
    ("Front Rank Troops / Fudo's Fiends (issue #271): both cards' own BLOCKERS notes were stale -- MatchEngine.ExpendableDraw already exists and is already generically correct for any card carrying the Expendable keyword, checked at every real destroy path. Zero AbilityCatalog wiring needed; this proves the existing, previously-untested helper actually fires for a real published card", ExpendableFollowers),
    ("General in-play unit-targeting Reaction primitive: Champion of Thunder negates an enemy action's effects on itself, on its host Personality, or on a sibling Follower, choosing which card in its own unit to negate on each time -- reuses the existing beforeActionReaction window (BeforeActionReactionApplies's own Champion/Brothers case), with NegatedEffectKind \"all\" reaching every effect sink (StalwartNegatesBow, ApplyModifier, DestroyUnits, MoveHome, Straighten)", ChampionOfThunderNegatesOnAnyUnitMember),
    ("General in-play unit-targeting Reaction primitive: Champion of Thunder's own multi-affected-card case -- one action (a Force penalty) hits every card in the unit at once; Champion's controller chooses exactly one to negate on, leaving the rest penalized", ChampionOfThunderChoosesAmongMultipleAffectedCards),
    ("General in-play unit-targeting Reaction primitive: Brothers of Goemon requires controlling a Ring before the reaction is even offered, never triggers when the resolving effect could never bow the target (BowsTarget's own curated classification), and negates ONLY the bow when an effect both applies a Force penalty and conditionally bows off the post-penalty Force (costly-feud) -- the Force penalty still lands in full", BrothersOfGoemonRingGateAndPartialBowNegation),
    ("A Close Call reuses the existing before-attack-resolves Reaction window from hand (AnyAttackReactionAvailable's own new out-of-play check) rather than the new unit-targeting watcher, gated on an Armor-wearing Personality targeted by a Ranged or Melee Attack -- no Armor or a non-Attack ability never offers it, and negation zeroes the Attack's strength before it resolves, same shape as training-attack-negate", ACloseCallArmorAttackNegation),
    ("Personality wave 23 (issue #305): Akodo Kakihara is published with the scan's own colon-form text, two catalog entries sharing ONE UsageKey (one printed ability, two windows), and the stale 'broad targets-this-card trigger' blocker is gone", AkodoKakiharaPublishedShape),
    ("Akodo Kakihara (issue #305), non-Attack half: another player's Battle or Open action targeting HIM opens the general beforeActionReaction window for his controller; the bonus is his CURRENT Personal Honor (a PersonalHonor modifier counts), lands before the action resolves and does not negate it; never offered for his own controller's action, an action targeting a different card, a Dishonorable Kakihara (+0F: no window), a bowed one, one not in play, or once his single per-turn use is spent", AkodoKakiharaReactsToNonAttackActions),
    ("Akodo Kakihara (issue #305), Attack half: a Ranged or Melee Attack targeting HIM opens the existing attackReaction window and the Force bonus decides the outcome -- the same Attack destroys him when nobody reacts and fails against his raised Force when he does; no window opens for an Attack at another card, a Dishonorable/bowed/spent Kakihara, and when another reactor opens one anyway his reaction is disabled and rejected unless he is the target", AkodoKakiharaReactsToAttacks),
    ("Multi-reaction before-action window (issue #318, Mendon's ruling): every applicable reaction of BOTH players is takeable -- the player whose turn it is has the first opportunity (only a player holding a reaction holds one), then the other, alternating; a reaction played resets the pass count and leaves the window open; it closes only when both pass consecutively and the deferred command then resolves; one player passing, the other reacting and the first reacting after all works; both passing at once resolves the command untouched", BeforeActionWindowAlternatesTurnPlayerFirst),
    ("Multi-reaction before-action window (issue #318): several reactions of ONE player are all takeable in either order (Champion of Thunder + Kakihara in one unit; Rokai + Kakihara), each spent after use and never usable twice, and Kakihara's bonus survives a Champion played before it because it is not an effect of the action", BeforeActionWindowSamePlayerReactionsInEitherOrder),
    ("Multi-reaction before-action window (issue #318): eligibility is re-evaluated at every opportunity -- a reaction that bows the other sources removes them from the window (listed disabled, rejected if submitted) while the window stays open", BeforeActionWindowReactionCanRemoveAnothersEligibility),
    ("Multi-reaction before-action window (issue #318): negations compose (MatchState.NegatedEffects) -- Brothers of Goemon's bow negation on the host survives a Champion of Thunder negation on another card played before or after it, two Champions each negate a different card, all wiped when the command has resolved", BeforeActionWindowNegationsCompose),
    ("Multi-reaction before-action window (issue #318): QueryActions offers each player only their own reactions at their own opportunity (Champion of Thunder one option per unit member; Rokai; the waiting player told to wait), every step is drivable by passing through the listing alone (no deadlock), and the same commands give byte-identical state", BeforeActionWindowListingAndDeterminism),
    ("Multi-reaction before-action window (issue #318): only reactions that apply AND are usable count -- a spent, bowed or other-unit Champion of Thunder never opens a window, its own controller's action never does, and inside a window someone else's reaction opened an inapplicable reaction (Rokai elsewhere, a Champion in another unit) is neither offered nor accepted", BeforeActionWindowOnlyOffersUsableAndApplicableReactions),
    ("Multi-reaction before-action window (issue #318) x Bayushi Kahoku - exp (#313, merged since): Kahoku is Attack-classified but its TargetId is a separate -4F target, so that targeting is an action targeting the card and the before-action reactions apply to it (Champion of Thunder negating the -4F, Kakihara's bonus), while the Attack's own target in CardIds still reaches the attack window -- an AttackKind-only exclusion would have skipped them", BeforeActionWindowKahokuMinusForceTargetIsAnActionTarget),
    ("Attack reaction window aligned with the same rule (issue #318): the player whose turn it is has the first opportunity when they hold a reaction (previously the attacked side always went first), then the other player, alternating -- an attacker's later boost after the defender's Kakihara bonus decides the Attack", AttackWindowTurnPlayerFirstThenAlternates),
    ("Stormless Fury (issue #276 follow-up): the general 'after a Battle action resolves' Reaction primitive (OpenBattleActionReactionWindow) -- its own out-of-play half reuses Equip (Limited)'s own attach mechanism as a Reaction, deliberately granting no Additional Action (unlike Equip (Battle)); its own in-play half bows the just-resolved action's own performer, excluded (BattleActionReaction1UsedCardIds) from firing against the SAME action that triggered the first half, but firing normally against a later, different action", StormlessFuryBattleActionReaction),
    ("Tsuruchi Yakusho (issue #276 follow-up): the general 'after you announce an action' Reaction primitive (ResolveOrDeferAnnounceReaction), offered only when the about-to-execute ability is classified Ranged by the existing AttackKind() lookup (never Melee); her own effect sets a transient immunity (RangedAttackStrengthReductionImmune) blocking every existing Attack-strength-reducing Reaction (proven against Training's own negate) for the remainder of that specific Attack's own resolution, never carrying over into a later, separate Attack", TsuruchiYakushoAnnounceReactionAndImmunity),
    ("Follower wave 12 (issue #279): four small, mostly-independent primitives -- Apprentice Shinobi's universal 'after any Ninja action resolves, +1F' trait (Keyword(source,\"Ninja\") alone, since a printed 'Ninja Battle' prefix is never retained in ability.Timing; reset for free by FinishTurn's own Modifiers.Clear()); Legion of the Bat's new 'after an action moves a card to this battlefield' Reaction (MoveToBattlefield itself queues the candidate; RecruitReserve is explicitly excluded, proven negative) plus its own Crane Clan Equip discount; Nikutai's new 'after a sibling Follower in this unit bows' Reaction (Even if bowed; straightens the OTHER Follower, not itself; proven scoped to its own unit only); and Wave Man's new automatic 'after an Attack Phase ends, straighten this unit' trait, proven on both the 'last battlefield resolved' and 'declined to attack' paths, and proven NOT to spuriously consume an unrelated StraightenBlockedTurn on an already-unbowed card", FollowerWave11Primitives),
    ("Cultist Infiltrator (oracle-l5r-1595): may attach to a Personality its controller does not own (a new Effect-keyed exception on Equip's own ownership Require); after each Straighten Phase gives its host a persistent -1C token via the existing shared CreateToken Chi<=0 death check; is excluded from Overlay's own attachment-transfer loop (discarded instead); and, attached to an enemy Personality, is returned to its OWN original owner's discard pile when that Personality dies, since CardInstance.OwnerId is never reassigned", CultistInfiltratorPrimitive),
    ("Unimpeachable Name (oracle-l5r-8989): a broad out-of-play \"before any action resolves\" Reaction -- reduces the reacting player's own Honor loss from that action by 3 (minimum 0) and/or rehonors a target Dishonorable Personality; NOT offered at all when neither condition could possibly apply; her own separate Battle ability bows a performing Shugenja and an enemy card for 2 Honor", UnimpeachableNamePrimitive),
    ("Fields of Mercy (oracle-l5r-2530): a per-Terrain-instance, per-player destroyed-Personality counter that starts at 0 only once the card itself enters play (never retroactively) -- the first Personality destruction after entry still happens normally (and starts the count), every one after that has only the Personality-destruction component negated (attachments on that unit are still destroyed)", FieldsOfMercyPrimitive),
    ("Yasuki Tsujiken (oracle-l5r-9469): his own entering-play Reaction marks a target Holding, independently stacking per copy of Tsujiken that marks it; every future bow of a marked Holding (by any cause) costs its controller 1 Honor per active mark, for as long as the marking Tsujiken remains in play; his separate Open half straightens a target Holding, only bowing himself as a cost when that Holding's base Gold Cost is above 4", YasukiTsujikenPrimitive),
    ("Follower wave 14 (issue #285): the first batch of six \"Not blocked; deferred for scope\" cards from build_card_inventory.py's own BLOCKERS audit -- Asako House Guard's dual-choice Set-Force (asako-house-guard-force-chi/-honor, sharing one UsageKey, plus its own Phoenix Clan attach restriction); Legulus's Legion's bow-cost Attack reading its own LIVE Force via sourceId (proven against a token modifier, not just its printed Force); Village Guardian's new per-player MatchState.ProvinceEverDestroyed tracker, set at MatchEngine.DestroyProvince's own single choke point (proven to flag only the province's own owner, never the attacker) and read by its own entry Reaction's draw count (0, 1 or 2); Village Raiders' AttachGrantsKeyword \"Conqueror\" (its own scan literally reads \"This Personality has Conqueror\" despite being a Follower), proven end-to-end through an actual battle resolution, plus its own separate flat Melee 4 Attack; Water Spider Adept's pure attach restriction with no ability text at all; and Yodotai Legionnaire's entry Honor loss carrying the real Legionnaire keyword, previously exercised only by a synthetic fixture, through Aulus's own existing unit-scoped boost", FollowerWave14Primitives),
    ("Strategy wave 13 (issue #287): Batch B of the \"Not blocked; deferred for scope\" cards from build_card_inventory.py's own BLOCKERS audit -- Scorn the Weak's flat -5F with a Force-now-0-gated Honor loss and optional second-target dishonor (CardIds); Strength in Terror's dynamic Force reduction off a chosen Paragon Performer's own live Force plus an optional attachment-destroy (CardIds); Strength of Purity's new numeric Personal-Honor-threshold Performer category; Strength of the Bear's own-unbowed-personality Performer paired with enemy-force-lower-than-performer, plus its own printed-Force-gated straighten-back; Strength of the Divine's dual Force-vs-Personal-Honor choice sharing one UsageKey, each branch buffing its own chosen, opposed Paragon Performer then attacking with strength read live off that same Performer; The Strength of the Spider's new clan-checked Performer and its own destroy-if-bowed-else-cripple two-branch effect; and Unexpected's optional self-bow-for-a-draw (a third CardIds reuse)", StrategyWave13Primitives),
    ("Strategy wave 14 (issue #289): Batch C of the \"Not blocked; deferred for scope\" cards from build_card_inventory.py's own BLOCKERS audit -- Banzai!'s Discipline card narrowed back to a Battle-only presence Require (its own printed verb, unlike the shared Discipline switch's default BattleOrOpen-like permissiveness) plus crushing-force's own own-opposed Target reused verbatim; Pure Intent's new own-personality-opposed Performer category; Marshal Your Strength's own Force-and-Chi dual modifier (ApplyModifier's own stat-agnostic Chi branch, exercised by a real card for the first time) minus 1 when unopposed; Lost in Transit's own Battle/Open Discipline needing no presence narrowing, reusing the standalone \"attachment\" Target category verbatim; Strong Defenses' new own-defending-personality-at-battle Performer plus its own Khol-Regulars-shaped -3 Reaction; Trusting Instinct's own -2 Reaction (both added to Tsuruchi Yakusho's shared strength-reduction-immunity id list) plus a move-to-the-opposed-current-battlefield Battle half; and Visage of the Orochi's own floored Set-Force \"raise to 6\" plus an optional CardIds-carried bow target compared against the Performer's own post-raise Force", StrategyWave14Primitives),
    ("Strategy wave 15 / spell-wave-4 / ring-wave-2 (issue #295): Batch D of the \"Not blocked; deferred for scope\" cards from build_card_inventory.py's own BLOCKERS audit -- Charge Into Danger's own plain own-personality Performer, its move conditional (not a hard gate) on assigned-to-or-would-be-opposed, its straighten independent of whether it moved; Consuming Weakness's new own-shadowlands-personality-at-battle Performer and dead-personality-any-discard Target (Zone.Dead, any owner); Fires of Turmoil's new unattached-unit-card Target (the non-enemy-restricted counterpart to Ikoma Masumi's own); Gripped by Terror's new own-personality-at-battle Performer plus a one-shot ApplyModifier loop over Army(); Outnumbered's own Performer/Target-less \"give all qualifying cards X\" shape, the same one-shot loop; Preserving Honor's own new \"preservingHonor\" PendingChoice, proven through all three outcomes (bow, move, and Stalwart negating the bow into the remove-from-game fallback); Your Clan Needs You!'s fourth caller into the shared Purchase/RecruitReserve/CreatePersonality entry sequence, its new own-personality-in-province Target, and the new post-battle province-placement primitive, proven end to end through an actual battle resolution; Sanctuary of the Waves' new own-wielder-or-personality-with-wielder-keyword Target, proven on both the default (wielder) and Water-unlocked (another own Personality) branches, plus the not-opposed no-op; and False Ring of Earth's new Dueling.SharesNoKeywords comparison feeding Ring recruitment's existing GrantRingRecruit shape, proven through an actual duel resolution on both a qualifying and a disqualifying pairing, plus the first negative-magnitude MaxHandSize use", BatchDPrimitives),
    ("Personality wave 21 (issue #299): Batch E of the \"Not blocked; deferred for scope\" cards from build_card_inventory.py's own BLOCKERS audit -- the ONE shared controller-clan-conditional Honor Requirement waiver (CardDefinition.HonorRequirementWaiverClan) proven for Akodo Chiyo's Crab and Kitsu Ririko's Unicorn branches, each positive, crossed-negative (the other clan does not qualify) and wrong-clan-negative, through both QuotePurchase and an actual Purchase (Ririko's Destined draw firing off the waived entry); Chiyo's Melee 4 Attack; Matsu Kaido's attacking-one Effect (+1F only while attacking) and 'Reduce its Force to 0' (a delta off the target's live Force, with a Follower target); Matsu Miura's/Utaku Mai's Personal-Honor Force penalty (Mai's move-then-penalty from Home, from already-at-battlefield, bowed-rejected, and the reinforcesFromElsewhere exemption proven Mai-specific by a Miura-at-Home control); Shinjo Kinto's conditional un-negatable movement (Tamoko/Champion of Thunder windows suppressed only with Reconnaissance AT the current battlefield -- not another province's) plus Wrathful Ancestor now routed through the same shared check; Kakita Maratai's broadened own-personality-won-duel Reaction (Maratai need not be the duelist; enemy win, bowed Maratai, and co-existence with an existing own-won-duel Reaction like Daisuke's -- no regression); Kitsune Ohsuki/Tokoru's created tokens (real Oracle proxy cards) created from inside their own entry window WITHOUT clobbering it (Proclaim still available afterward, even with a Singh Remnants-style watcher in hand); Ohsuki's Sparrow's new windowed MatchState.ProvinceDestroyedSinceTurnEnd flag (set only for the province owner, cleared only at THAT player's own turn end, proven end to end through a real province destruction across turns); and Tokoru's Bear's new enemy-attacking-card Target (Personality, attached Follower/Item destroyed after bowing, own/defending cards rejected)", BatchEPrimitives),
    ("Personality wave 22 / region-wave-3 (issue #301): Batch F -- the final batch -- of the \"Not blocked; deferred for scope\" cards from build_card_inventory.py's own BLOCKERS audit -- the published wave shape (twelve Personalities, Shinden Shorai as the Region; Hawado's Clan corrected to Scorpion, Kodo's text following the scan, Mitsuko as the plain-titled Experienced printing; the two halves of Nori's/Hawado's printed choices sharing one UsageKey)", BatchFPublishedShape),
    ("Personality wave 22 (issue #301): the Open straighteners and their new Holding-type Targets -- Agasha Kodo's any-holding (either side's, Open so usable by the non-active player, not a Battle action, once per turn), Asahina Munefusa's own-holding-with-keyword Temple (own only, keyword required, not a Personality), Isawa Mitsuko's air-shugenja-or-holding union (Air AND Shugenja both required, either side's, Holding half, and a Personality target reaching Tamori Tamoko's straighten-negation window while a Holding never does)", BatchFStraightenAndHoldingTargets),
    ("Personality wave 22 (issue #301): the Battle bow/destroy family -- Bayushi Nori's dual-choice shape (bow the Personality OR every attachment, Honor loss only when dishonorable, one shared use, enemy only, presence required); Hida Mimori's new enemy-bowed-card Target (a Personality with his attachments, or a lone Follower/Item, with every illegal shape rejected, plus Rokai's destruction-negation window); Isawa Mitsuko's bow-then-conditional-destroy (Shadowlands by Clan AND by the printed keyword Rikate carries, Ninja, Kolat, and a Stalwart target still destroyed); Sutigu's new own-source-or-unbowed-attached-with-keyword Performer (Sutigu or his own unbowed Weapon, straightening negated only on an attachment)", BatchFBattleBowAndDestroy),
    ("Personality wave 22 (issue #301): movement and opponent-choice cards -- Asahina Munefusa's Battle half (exempt from presence for a controller with no unit there, attacking Personalities only, Honor only if he really moved, Tamoko able to negate it); Shosuro Hawado (remove any Dynasty card in any discard pile as the cost, usable while bowed, home or a chosen battlefield with enemy units, straightening only if she really moved, one shared use); Asako Rikate's new rikateDetach PendingChoice (any subset of the unit's attachments returned to hand through the generalized ReturnToHand, THEN -2C, with the ordering proven lethal or not); Shosuro Rin's new rinShame PendingChoice (dishonor, or bow plus a negated straightening, Stalwart included)", BatchFMovementAndChoices),
    ("Personality wave 22 / region-wave-3 (issue #301): passives, equip and recruit -- Isawa Kojiro's live conditional Naval (appears and disappears with an attached Spell, never a durable grant); Yoritomo Minori's self-received per-other-Magistrate Force bonus (army-scoped, printed or granted, bowed counting, attacking or defending); Soshi Neiru's Open-timed Air-Spell equip (fixed wielder, no once-per-turn limit, usable by the non-active player, never in Battle); Shinden Shorai as the fourth caller of the shared province-Personality entry sequence (the player's OWN turn count, the Lion-Clan-Personality Honor Requirement waiver keyed on the recruit rather than the player, same province, full cost, Proclaim resuming to the opponent)", BatchFPassivesEquipAndRecruit),
    ("Strategy wave 16 (issue #304): Brawl -- its \"Needs Regions (not built)\" blocker was stale. The destroy half's new region-or-terrain Target (a Region at ANY province, either owner's, destroyed to its owner's Dynasty discard with its static strength bonus going with it; a Terrain through the shared DiscardTerrain removal, meeting Ring of Water's destroyed-a-Terrain condition; never a Personality; offered targets exactly the Region and the Terrain; disabled with nothing to destroy), usable at Open timing outside a Battle while the Battle-only bow half is not, and the bow half's reuse of enemy-card-without-attachments (an unattached enemy bows; an attached one and your own card do not)", StrategyWave16Primitives),
    ("Pile-targeted abilities are offered by QueryActions (issue #317): Hunter of Harmony's own-kiho-strategy-in-discard, Scholarly Hermit's own-ring-in-discard and Tranquil Garden's ring-in-play-or-own-discard each had a ValidateAbility branch but no candidates arm, so a target in a discard pile was never offered -- each is now offered enabled with exactly its legal targets (wrong owner, wrong zone and wrong type excluded), every offered target validates, and with no target the ability is offered disabled", PileTargetsAreOffered)

};
var runner = new TestRunner(args);
if (!runner.Valid) return 2;
runner.Run("core", tests);
foreach (var suite in typeof(PublishedTests).Assembly.GetTypes().Where(t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("Tests", StringComparison.Ordinal)).OrderBy(t => t.Name, StringComparer.Ordinal))
{
    var run = suite.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, [typeof(TestRunner)]) ?? throw new InvalidOperationException($"{suite.Name} has no public static Run(TestRunner), so its tests would never run");
    try { run.Invoke(null, [runner]); } catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException!).Throw(); }
}
return runner.Finish();

static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
static string Json<T>(T value) => JsonSerializer.Serialize(value);
static void Equal<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
static GameCommand Cmd(MatchEngine e, string type, int? player = null, string? id = null, string[]? sources = null, int? province = null, bool discount = false, bool allegiance = false, Dictionary<string, int>? assignments = null, string[]? cards = null, string? target = null)
    => new(type, player ?? e.State.PriorityPlayerId, e.Version, id, sources, province, discount, allegiance, assignments, cards, target);
static CardInstance Find(MatchEngine e, string definition, int owner = 0, Zone? zone = null) => e.State.Cards.Values.First(c => c.DefinitionId == definition && c.OwnerId == owner && (zone is null || c.Zone == zone));
static string[] Sources(MatchEngine e, int owner = 0) => e.State.Cards.Values.Where(c => c.OwnerId == owner && c.Zone is Zone.Home or Zone.Stronghold && !c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray();
static string ProvinceCard(MatchEngine e, int index = 0, int owner = 0) => e.State.Players[owner].Provinces[index].CardId!;
// The before-action window (issue #318) is an alternating window open to either player, not scoped to one watching
// card: what a single-reactor test can still assert is that the window is open, that the reaction's OWNER holds
// priority (the only player with anything to play, so the active player's vacuous first opportunity is skipped),
// and that the legal-action listing offers that card's reaction, enabled.
static void ExpectBeforeActionOffer(MatchEngine e, string cardId, string message = "The card's own reaction is offered")
{
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
    var owner = e.State.Cards[cardId].OwnerId;
    Equal(e.State.PriorityPlayerId, owner, "Priority sits with the reaction's owner");
    Check(e.QueryActions(owner).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.CardId == cardId), message);
}
static void SkipToDynasty(MatchEngine e) { while (e.State.Phase != Phase.Dynasty) Accept(e, Cmd(e, "pass")); }
// Ends the current turn all the way through, including a hand-limit discard should one come up (a
// many-turns-in-a-row test otherwise stalls on it, since nothing else in a bare pass-only flow ever
// spends a card) -- picks arbitrary cards, since which ones is never the point of a test using this.
static void EndTurnFully(MatchEngine e)
{
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    if (e.State.PendingChoice?.Type == "discardHand")
    {
        var pending = e.State.PendingChoice; var hand = e.State.Players[pending.PlayerId].Hand;
        Accept(e, Cmd(e, "discardHand", player: pending.PlayerId, cards: hand.Take(hand.Count - 8).ToArray()));
    }
}
static void CloseReaction(MatchEngine e) { while (e.State.PendingChoice?.Type == "proclaim") Accept(e, Cmd(e, "declineReaction")); }
static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? attachedTo = null)
{
    var c = new CardInstance { Id = $"test-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = attachedTo };
    s.Cards.Add(c.Id, c);
    if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
    return c;
}
static MatchEngine Battle(int attackForce = 5, int defenseForce = 2, int provinceStrength = 6, bool attackBowed = false)
{
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
    s.Players[1].Provinces[0].Strength = provinceStrength;
    var a = Add(s, "training-crane-veteran", 0, Zone.Battlefield, attackBowed, 0); a.Modifiers = [new("test-fixture", "test-fixture", "Force", attackForce - 5)];
    var d = Add(s, "training-lion-veteran", 1, Zone.Battlefield, false, 0); d.Modifiers = [new("test-fixture", "test-fixture", "Force", defenseForce - 6)];
    return MatchEngine.FromTestState(s);
}
static (MatchState State, CardInstance Attacker, CardInstance Defender) ForceBattleFixture(int attackForce, int defenseForce, int provinceStrength, bool bowed = false)
{
    var s = Battle(attackForce, defenseForce, provinceStrength, bowed).State;
    return (s, s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield),
        s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield));
}
static void AssertBattleForces(MatchEngine e, int attack, int defense)
{
    var result = e.GetView(0).Events.Single(ev => ev.Type == "battleResolved").Message;
    Check(result.Contains($"attacking Force {attack}, defending Force {defense}"), result);
}
static void Resolve(MatchEngine e) { Accept(e, Cmd(e, "pass")); Accept(e, Cmd(e, "pass")); }

static void PublicPassEvents()
{
    var e = Battle();
    var before = e.State.Events.Count;
    Reject(e, new("pass", 0, e.Version));
    Equal(e.State.Events.Count, before);
    Accept(e, Cmd(e, "pass"));
    var first = e.GetView(0).Events.Last(); Equal(first.Type, "passed"); Equal(first.PlayerId, 1);
    Accept(e, Cmd(e, "pass"));
    var events = e.GetView(1).Events.Skip(before).ToArray();
    Equal(events.Count(x => x.Type == "passed"), 2);
    Check(events.Single(x => x.Type == "passed" && x.PlayerId == 0).Sequence < events.Single(x => x.Type == "battleResolved").Sequence);
    var reaction = MatchEngine.CreateEconomyScenario();
    var id = ProvinceCard(reaction); var quote = reaction.QuotePurchase(0, id);
    Accept(reaction, Cmd(reaction, "purchase", id: id, sources: quote.Payments[0].SourceIds));
    Check(reaction.State.PendingChoice is not null);
    var actor = reaction.State.PriorityPlayerId;
    Accept(reaction, Cmd(reaction, "declineReaction"));
    var pass = reaction.GetView(1).Events.Last(x => x.Type == "reactionPassed"); Equal(pass.PlayerId, actor);
    Check(pass.CardId is null, "Passing must not reveal a hidden card.");
}

static void Setup()
{
    var e = MatchEngine.CreateStarterMatch(); var s = e.State;
    Equal(s.Phase, Phase.Action); Equal(s.ActivePlayerId, 0); Equal(s.Version, 0L);
    OpeningCounts(s);
    Check(Find(e, "training-bamboo", 1).Bowed); Check(!s.Cards.Values.Any(c => c.OwnerId == 0 && c.DefinitionId == "training-bamboo"));
    Check(s.Players[0].Provinces.All(p => s.Cards[p.CardId!].FaceUp));
    Check(s.Players[1].Provinces.All(p => !s.Cards[p.CardId!].FaceUp));
}
static void Privacy()
{
    var e = MatchEngine.CreateStarterMatch(); var view = e.GetView(1); var s = e.State;
    Equal(view.Players[0].Hand.Length, 0); Equal(view.Players[0].HandCount, 6); Equal(view.Players[1].Hand.Length, 6);
    foreach (var p in view.Players[1].Provinces) { Check(p.Card!.Definition is null); Check(p.Card.Id.StartsWith("hidden-")); }
    var json = Json(view);
    HiddenIdsAbsent(view, s.Players[0].Hand.Concat(s.Players[0].FateDeck).Concat(s.Players[1].DynastyDeck));
    var quote = e.QuotePurchase(1, ProvinceCard(e, owner: 1)); Equal(quote.Cost, 0); Check(quote.Error is not null);
    Check(!json.Contains("Seed"));
}
static void AtomicReject()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e); var sources = Sources(e);
    Reject(e, Cmd(e, "purchase", player: 1, id: id, sources: sources));
    Reject(e, Cmd(e, "purchase", id: id, sources: [sources[0], sources[0]]));
    Reject(e, Cmd(e, "purchase", id: id, sources: []));
    Reject(e, new("pass", 0, 99)); Reject(e, new("pass", 44, 0)); Reject(e, Cmd(e, "unimplemented"));
    var c = Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds);
    Accept(e, c); Reject(e, c);
}
static void ExactPayment()
{
    var e = MatchEngine.CreateEconomyScenario(); var p = e.QuotePurchase(0, ProvinceCard(e)).Payments.First(p => p.Excess == 0);
    var unused = Sources(e).Except(p.SourceIds).ToArray();
    Accept(e, Cmd(e, "purchase", id: ProvinceCard(e), sources: p.SourceIds));
    Check(p.SourceIds.All(id => e.State.Cards[id].Bowed)); Check(unused.All(id => !e.State.Cards[id].Bowed));
    Equal(e.State.Players[0].Honor, 8);
}
static void Overproduction()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e, 1);
    var p = e.QuotePurchase(0, id).Payments.First(); Equal(p.Cost, 8); Equal(p.Produced, 9); Equal(p.Excess, 1);
    Accept(e, Cmd(e, "purchase", id: id, sources: p.SourceIds)); CloseReaction(e);
    var holding = ProvinceCard(e, 2); Equal(e.QuotePurchase(0, holding).Payments.Length, 0);
    Reject(e, Cmd(e, "purchase", id: holding, sources: []));
}
static void ConditionalProduction()
{
    var e = MatchEngine.CreateEconomyScenario(); var s = e.State; var crane = Find(e, "training-marketplace", zone: Zone.Home);
    var lion = Add(s, "training-marketplace", 1, Zone.Home); var x = MatchEngine.FromTestState(s);
    Equal(x.GoldProduced(crane.Id, 0), 3); Equal(x.GoldProduced(lion.Id, 1), 2);
}
static void Discount()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e); var q = e.QuotePurchase(0, id, true);
    Equal(q.Cost, 4); Check(!q.ProclaimEligible);
    Accept(e, Cmd(e, "purchase", id: id, sources: q.Payments[0].SourceIds, discount: true));
    Equal(e.State.Players[0].Honor, 8); Check(e.State.PendingChoice is null); Reject(e, Cmd(e, "proclaim"));
}
static void Allegiance()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e, 3);
    Check(e.QuotePurchase(0, id).Error is not null);
    var q = e.QuotePurchase(0, id, true, true); Equal(q.Cost, 6); Check(q.Error is null); Check(!q.ProclaimEligible);
    Accept(e, Cmd(e, "purchase", id: id, sources: q.Payments[0].SourceIds, discount: true, allegiance: true));
    var other = MatchEngine.CreateEconomyScenario(); var q2 = other.QuotePurchase(0, ProvinceCard(other, 3), false, true);
    Equal(q2.Cost, 8); Check(q2.ProclaimEligible);
}
static void Proclaim()
{
    var s = MatchEngine.CreateEconomyScenario().State;
    for (var i = 0; i < 6; i++) Add(s, "training-farm", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s); var id = ProvinceCard(e);
    Accept(e, Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds));
    Equal(e.State.Cards[id].Zone, Zone.Home); Equal(e.State.Players[0].Honor, 8); Check(e.State.PendingChoice is not null);
    Reject(e, Cmd(e, "endDynasty")); Accept(e, Cmd(e, "proclaim")); Equal(e.State.Players[0].Honor, 11);
    Check(e.State.PendingChoice is null, "issue #639: nothing is left to react to after the Proclaim, so the window is closed"); Reject(e, Cmd(e, "proclaim"));
    var next = ProvinceCard(e, 1); Check(!e.QuotePurchase(0, next).ProclaimEligible);
    Accept(e, Cmd(e, "purchase", id: next, sources: e.QuotePurchase(0, next).Payments[0].SourceIds));
    Check(e.State.PendingChoice is null); Equal(e.State.Players[0].Honor, 11);
}
static void Decline()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e);
    Accept(e, Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds)); CloseReaction(e);
    Equal(e.State.Players[0].ProclaimUsedTurn, -1); Equal(e.State.Players[0].Honor, 8);
}
static void HoldingAndRefill()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e, 2); var before = e.State.Players[0].DynastyDeck.Count;
    Accept(e, Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds));
    Check(e.State.Cards[id].Bowed); Equal(e.State.Cards[id].Zone, Zone.Home); Equal(e.State.Players[0].DynastyDeck.Count, before - 1);
    var replacement = ProvinceCard(e, 2); Check(replacement != id); Check(!e.State.Cards[replacement].FaceUp);
    Check(e.GetView(0).Players[0].Provinces[2].Card!.Definition is null);
}
static void DynastyDiscard()
{
    var e = MatchEngine.CreateEconomyScenario(); Accept(e, Cmd(e, "discardProvince", id: ProvinceCard(e, 2)));
    Equal(e.State.Phase, Phase.DynastyDiscard); Reject(e, Cmd(e, "purchase", id: ProvinceCard(e), sources: Sources(e)));
    Reject(e, Cmd(e, "discardProvince", id: ProvinceCard(e, 2)));
}
static void DynastyActionFlag()
{
    var e = MatchEngine.CreateEconomyScenario();
    Check(!e.State.Players[0].DynastyActionTaken && !e.GetView(0).Players[0].DynastyActionTaken, "nothing bought or discarded at the start of the Dynasty Phase");
    var id = ProvinceCard(e, 2);
    Accept(e, Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds));
    Check(e.GetView(0).Players[0].DynastyActionTaken && e.GetView(1).Players[0].DynastyActionTaken, "a purchase sets it, public to both seats");
    Check(!e.GetView(0).Players[1].DynastyActionTaken, "the opponent's flag is their own");
    var d = MatchEngine.CreateEconomyScenario();
    Accept(d, Cmd(d, "discardProvince", id: ProvinceCard(d, 2)));
    Check(d.GetView(0).Players[0].DynastyActionTaken, "a province discard sets it");
    var n = MatchEngine.CreateEconomyScenario();
    Accept(n, Cmd(n, "endDynasty")); Check(!n.State.Players[0].DynastyActionTaken, "ending with neither leaves it false");
    EndTurnFully(e); SkipToDynasty(e);
    Check(e.State.Players.All(p => !p.DynastyActionTaken), "the next Dynasty Phase starts clean");
}
static void Priority()
{
    var e = MatchEngine.CreateStarterMatch(); Accept(e, Cmd(e, "pass")); Equal(e.State.Phase, Phase.Action); Equal(e.State.PriorityPlayerId, 1);
    Accept(e, Cmd(e, "pass")); Equal(e.State.Phase, Phase.AttackDeclaration);
    // The Declaration Segment is one explicit decision (declareAttack, or pass to decline), then the attacker's Infantry Maneuvers.
    Accept(e, Cmd(e, "declareAttack")); Equal(e.State.PriorityPlayerId, 0); Equal(e.State.Phase, Phase.InfantryDeclaration);
    Accept(e, Cmd(e, "attack", assignments: [])); Equal(e.State.PriorityPlayerId, 1); Equal(e.State.Phase, Phase.DefenseAssignment);
    // Neither player has a Cavalry unit, so Cavalry Maneuvers is skipped for both (issue #627) and the phase goes straight on.
    // Nobody ever placed a unit anywhere, but the attack WAS declared: a battlefield exists at every province
    // regardless of assignments, so all four are still there to be fought.
    Accept(e, Cmd(e, "assignDefense", assignments: [])); Equal(e.State.Phase, Phase.BattleSelection);
    Equal(string.Join(",", e.State.UnresolvedBattlefields), "0,1,2,3");
}
static void Bamboo()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = Find(e, "training-bamboo", 1).Id;
    Accept(e, Cmd(e, "endDynasty")); Check(e.State.Cards[id].Bowed);
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty")); SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Check(!e.State.Cards[id].Bowed); Equal(e.State.Players[1].TurnsTaken, 2);
}
static void StraightenAndEventsPhaseOrder()
{
    var e = MatchEngine.CreateEconomyScenario();
    SkipToDynasty(e);
    var result = e.Execute(Cmd(e, "endDynasty"));
    Check(result.Accepted, result.Error ?? "Rejected");
    var order = result.Events.Where(ev => ev.Type == "phaseChanged").Select(ev => ev.Message).ToArray();
    var straighten = Array.IndexOf(order, "Phase: Straighten.");
    var events = Array.IndexOf(order, "Phase: Events.");
    var action = Array.IndexOf(order, "Phase: Action.");
    Check(straighten >= 0 && events > straighten && action > events, "Straighten must precede Events, which must precede Action");
    Equal(e.State.Phase, Phase.Action);
}
static void SuspicionsEventInPlay()
{
    var s = MatchEngine.CreateEconomyScenario().State;
    var card = Add(s, "oracle-l5r-7675", 1, Zone.Province, province: 0);
    card.FaceUp = false;
    s.Players[1].Provinces[0].CardId = card.Id;
    var honor0 = s.Players[0].Honor; var honor1 = s.Players[1].Honor;
    var e = MatchEngine.FromTestState(s);
    // Player 0's turn ends; player 1's Straighten -> Events reveals Suspicions in their own province,
    // removes it, resolves it into play (not the discard pile) and immediately applies its ongoing
    // effect -- "including the turn it is revealed" -- before reaching their own Action phase.
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.Phase, Phase.Action);
    Equal(e.State.Cards[card.Id].Zone, Zone.EventInPlay);
    Check(e.State.Cards[card.Id].FaceUp, "Revealed face-up");
    Check(e.State.Cards[card.Id].ProvinceIndex is null, "Removed from its province, not left sitting there");
    Check(e.State.Players[1].Provinces[0].CardId != card.Id, "Province refilled face down immediately");
    Check(!e.State.Cards[e.State.Players[1].Provinces[0].CardId!].FaceUp, "Refilled card is face down");
    Equal(e.State.Players[0].Honor, honor0 - 1);
    Equal(e.State.Players[1].Honor, honor1 - 1);
    Check(e.GetView(0).Players[1].EventsInPlay?.Any(c => c.Id == card.Id) ?? false, "Public zone: visible to the opponent");
    Check(e.GetView(1).Players[1].EventsInPlay?.Any(c => c.Id == card.Id) ?? false, "Public zone: visible to its own controller");
    Check(!e.GetView(0).Players[1].Discard.Any(c => c.Id == card.Id), "Never discarded -- the effect is still active");
    // "Before each of YOUR Action Phases": the opponent's turn triggers nothing...
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.Phase, Phase.Action); Equal(e.State.ActivePlayerId, 0);
    Equal(e.State.Players[0].Honor, honor0 - 1);
    Equal(e.State.Players[1].Honor, honor1 - 1);
    // ...and its owner's next turn triggers it again -- it is not a one-turn effect, and the card
    // never leaves Zone.EventInPlay for the discard pile on its own.
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.Phase, Phase.Action); Equal(e.State.ActivePlayerId, 1);
    Equal(e.State.Players[0].Honor, honor0 - 2);
    Equal(e.State.Players[1].Honor, honor1 - 2);
    Equal(e.State.Cards[card.Id].Zone, Zone.EventInPlay);
}
static void FaceUpEventResolvesInEventsPhase()
{
    // Refilled face up outside the Events Phase (Border Keep, the Kharmic replacement): never revealed,
    // but still resolved at its owner's next Events Phase rather than left sitting in the province.
    var s = MatchEngine.CreateEconomyScenario().State;
    var card = Add(s, "oracle-l5r-7675", 1, Zone.Province, province: 0);
    card.FaceUp = true;
    s.Players[1].Provinces[0].CardId = card.Id;
    var honor0 = s.Players[0].Honor; var honor1 = s.Players[1].Honor;
    var e = MatchEngine.FromTestState(s);
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.Phase, Phase.Action); Equal(e.State.ActivePlayerId, 1);
    Equal(e.State.Cards[card.Id].Zone, Zone.EventInPlay);
    Check(e.State.Players[1].Provinces[0].CardId != card.Id, "Removed from its province and refilled");
    Equal(e.State.Players[0].Honor, honor0 - 1);
    Equal(e.State.Players[1].Honor, honor1 - 1);
}
static void RegionAttachmentPrimitive()
{
    // A second Region revealed at an already-regioned province is discarded instead of replacing it;
    // the existing Region stays untouched and the province still refills immediately.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var first = Add(s, "oracle-l5r-4777", 1, Zone.Region, province: 0);
        s.Players[1].Provinces[0].RegionId = first.Id;
        var second = Add(s, "oracle-l5r-9865", 1, Zone.Province, province: 0);
        second.FaceUp = false;
        s.Players[1].Provinces[0].CardId = second.Id;
        var e = MatchEngine.FromTestState(s);
        SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Cards[second.Id].Zone, Zone.DynastyDiscard, "A second Region revealed at an already-regioned province is discarded, not attached");
        Equal(e.State.Players[1].Provinces[0].RegionId, first.Id, "The existing Region stays attached, untouched");
        Check(e.State.Players[1].Provinces[0].CardId != second.Id, "Province still refilled face down immediately");
        Check(!e.State.Cards[e.State.Players[1].Provinces[0].CardId!].FaceUp);
    }
    // A Region attaches to the province itself, so it cannot outlive it: destroying the province
    // discards whichever Region was attached, and clears the province's own pointer to it.
    {
        var bs = Battle(attackForce: 10, defenseForce: 0, provinceStrength: 1).State;
        var region = Add(bs, "oracle-l5r-4777", 1, Zone.Region, province: 0);
        bs.Players[1].Provinces[0].RegionId = region.Id;
        var be = MatchEngine.FromTestState(bs);
        Resolve(be);
        Check(be.State.Players[1].Provinces[0].Destroyed, "Province destroyed as usual (10 Force vs. 1 strength)");
        Equal(be.State.Cards[region.Id].Zone, Zone.DynastyDiscard, "Region discarded along with its destroyed province");
        Check(be.State.Players[1].Provinces[0].RegionId is null, "Province's own pointer to it is cleared");
    }
}
static void FallowLandsRegion()
{
    // Static +2 province strength (core rules: applied immediately, without respect to presence) is
    // live-recomputed from the attached Region, not a one-shot mutation -- an un-boosted 6-strength
    // province falls to a Force-8 attack; the same setup with Fallow Lands attached does not.
    {
        var baseline = Battle(attackForce: 8, defenseForce: 0, provinceStrength: 6);
        Resolve(baseline);
        Check(baseline.State.Players[1].Provinces[0].Destroyed, "Baseline: Force 8 destroys an un-boosted 6-strength province");
    }
    {
        var s = Battle(attackForce: 8, defenseForce: 0, provinceStrength: 6).State;
        var fallowLands = Add(s, "oracle-l5r-9865", 1, Zone.Region, province: 0);
        s.Players[1].Provinces[0].RegionId = fallowLands.Id;
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[1].Provinces[0].Strength, 8, "The view exposes EFFECTIVE strength (6 printed + 2 from Fallow Lands), not the raw field");
        Resolve(e);
        Check(!e.State.Players[1].Provinces[0].Destroyed, "Fallow Lands' +2 raises the province to 8, so a Force-8 attack no longer exceeds it");
    }
    // Reveal/attach mechanics: removed from its province, attached, and the province refills face
    // down immediately -- same shape as an Event, but it never resolves-and-discards afterward.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var region = Add(s, "oracle-l5r-9865", 1, Zone.Province, province: 0);
        region.FaceUp = false;
        s.Players[1].Provinces[0].CardId = region.Id;
        var e = MatchEngine.FromTestState(s);
        SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Cards[region.Id].Zone, Zone.Region);
        Equal(e.State.Players[1].Provinces[0].RegionId, region.Id);
        Equal(e.State.Cards[region.Id].ProvinceIndex, 0);
        Check(e.State.Players[1].Provinces[0].CardId != region.Id, "Province refilled face down immediately");
        Check(!e.State.Cards[e.State.Players[1].Provinces[0].CardId!].FaceUp);
    }
    // The Reaction is usable the very turn Fallow Lands enters play, weakening an Attack at its own
    // province while its controller has presence there.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Attacker's own presence (Carpenter Castle's requirement)
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); enemy.Modifiers = [new("test-fixture", "test-fixture", "Force", 1)]; // Force 4: defender's own presence too
        var fallowLands = Add(s, "oracle-l5r-9865", 1, Zone.Region, province: 0);
        s.Players[1].Provinces[0].RegionId = fallowLands.Id;
        Add(s, "training-attack-reactor", 1, Zone.Home); // the defender still holds a Reaction, so the window stays open to read the strength (issue #644)
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Equal(e.State.PendingAttack?.Strength, 5);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: fallowLands.Id, AbilityId: "fallow-lands-reaction"));
        Equal(e.State.PendingAttack?.Strength, 3);
        Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds nothing, so this one pass closes the window
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "Force 4 vs. reduced strength 3 no longer destroys -- worked the very turn Fallow Lands entered play");
    }
    // Rejected when its own province is not the one under attack, even though the window still opens
    // (a Reaction-eligible card merely existing in play is enough for that, regardless of whether its
    // own Require would actually pass).
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var fallowLands = Add(s, "oracle-l5r-9865", 1, Zone.Region, province: 1); // attached to a DIFFERENT province
        s.Players[1].Provinces[1].RegionId = fallowLands.Id;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: fallowLands.Id, AbilityId: "fallow-lands-reaction"));
        Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds nothing, so this one pass closes the window (issue #644)
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "Attack resolved at full strength -- Fallow Lands never reacted");
    }
}
static void LowMarketEquip()
{
    // Query-to-command contract: each offered price covers every target in that option.
    // The client changes only the chosen target and payment; it never computes discounts.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action;
        var region = Add(s, "oracle-l5r-4777", 0, Zone.Region, province: 0); s.Players[0].Provinces[0].RegionId = region.Id;
        var hero = Add(s, "training-crane-clan-samurai", 0, Zone.Home);
        var cheap = Add(s, "training-item", 0, Zone.Hand);
        var costly = Add(s, "training-follower-crane-guard", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var options = e.QueryActions(0).Where(a => a.CardId == region.Id && a.Enabled && a.Command?.PerformerId == hero.Id).ToArray();
        Check(options.Any(a => a.TargetGold?[cheap.Id].Cost == 1 && a.TargetIds!.Contains(cheap.Id)), "Minimum-one discount must be quoted");
        Check(options.Any(a => a.TargetGold?[costly.Id].Cost == 2 && a.TargetIds!.Contains(costly.Id)), "Four-Gold attachment must be quoted as two");
        foreach (var option in options)
            foreach (var target in option.TargetIds!)
            {
                var expected = Math.Max(1, CardCatalog.Get(e.State.Cards[target].DefinitionId).GoldCost - 2);
                var quote = option.TargetGold![target];
                Equal(quote.Cost, expected, "Target has its own authoritative quote");
                var copy = MatchEngine.FromTestState(s);
                Accept(copy, option.Command! with { TargetId = target, SourceIds = quote.Payments.First().SourceIds });
                Check(copy.State.Cards[region.Id].Bowed, "Offered command pays the Region's bow cost");
                Equal(copy.State.Cards[target].AttachedTo, hero.Id);
            }
    }
    // "Attach it to him, paying 2 less Gold" -- a genuine reduction (4 -> 2), not a no-op; bowing
    // itself is the entire cost/limiter (no separate UsageKey), so a second use this turn is rejected.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; // CreateEconomyScenario defaults to Phase.Dynasty; Open needs Action.
        var lowMarket = Add(s, "oracle-l5r-4777", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = lowMarket.Id;
        var performer = Add(s, "training-crane-clan-samurai", 0, Zone.Home); // Crane Clan keyword, Personal Honor 3
        var attachment = Add(s, "training-follower-crane-guard", 0, Zone.Hand); // GoldCost 4, requires Crane Clan + Honor >= 2
        var second = Add(s, "training-follower-crane-guard", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 2).FirstOrDefault();
        Check(payment is not null, "Enough Gold to cover the discounted cost of 2 (4 - 2)");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: lowMarket.Id, SourceIds: payment!.SourceIds, TargetId: attachment.Id, AbilityId: "low-market-equip", PerformerId: performer.Id));
        Check(e.State.Cards[lowMarket.Id].Bowed, "Bowing itself is the cost");
        Equal(e.State.Cards[attachment.Id].Zone, Zone.Attached);
        Equal(e.State.Cards[attachment.Id].AttachedTo, performer.Id);
        Check(!e.State.Players[0].Hand.Contains(attachment.Id));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: lowMarket.Id, TargetId: second.Id, AbilityId: "low-market-equip", PerformerId: performer.Id));
    }
    // "... to a minimum of 1": a cheap attachment's discount floors at 1, never 0 or negative.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action;
        var lowMarket = Add(s, "oracle-l5r-4777", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = lowMarket.Id;
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Hand); // GoldCost 2 -- 2-2=0, floored to 1
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 1).FirstOrDefault();
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: lowMarket.Id, SourceIds: payment!.SourceIds, TargetId: item.Id, AbilityId: "low-market-equip", PerformerId: performer.Id));
        Equal(e.State.Cards[item.Id].Zone, Zone.Attached);
    }
}
static void CelestialAttachmentPrimitive()
{
    // Reveal enters play immediately (Zone.Celestial), province refills face down; a new Celestial
    // entering play discards every OTHER Celestial the SAME player controls -- but not the opponent's.
    var s = MatchEngine.CreateEconomyScenario().State;
    var mine1 = Add(s, "oracle-l5r-4658", 1, Zone.Celestial);
    var mine2 = Add(s, "oracle-l5r-4658", 1, Zone.Celestial);
    var theirs = Add(s, "oracle-l5r-4658", 0, Zone.Celestial);
    var incoming = Add(s, "oracle-l5r-4658", 1, Zone.Province, province: 0);
    incoming.FaceUp = false;
    s.Players[1].Provinces[0].CardId = incoming.Id;
    var e = MatchEngine.FromTestState(s);
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.Cards[incoming.Id].Zone, Zone.Celestial, "The newly revealed Celestial enters play");
    Equal(e.State.Cards[mine1.Id].Zone, Zone.DynastyDiscard, "Player 1's other Celestial is discarded");
    Equal(e.State.Cards[mine2.Id].Zone, Zone.DynastyDiscard, "Player 1's other Celestial is discarded");
    Equal(e.State.Cards[theirs.Id].Zone, Zone.Celestial, "Player 0's own Celestial is untouched -- discard is per-player");
    Check(e.State.Players[1].Provinces[0].CardId != incoming.Id, "Province refilled face down immediately");
    Check(e.GetView(0).Players[1].Celestials?.Any(c => c.Id == incoming.Id) ?? false, "Public zone: visible to the opponent");
}
static void LadyMatsusRageDestroyRedirect()
{
    // "After your action resolves that destroyed any Personalities: Remove them from the game" --
    // Ranged/Melee Attack resolution is a real, non-battle-resolution action-triggered destroy.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); // Carpenter Castle
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Attacker's own presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        Add(s, "oracle-l5r-4658", 0, Zone.Celestial);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, dies to strength 5
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.Cards[enemy.Id].Zone, Zone.RemovedFromGame, "Redirected: the attacker controls Lady Matsu's Rage");
    }
    // Baseline: no redirect at all without her in play.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "No redirect without her in play");
    }
    // Battle resolution's own Force-comparison casualty sweep is NEVER redirected -- explicitly not
    // "an action," even in a battle her controller declared, and even though she is in play. Ring of
    // Fire's own destroy and a duel's own loss consequence share the identical
    // DestroyUnits(..., actingPlayerId: player.Id) shape already exercised above; not independently
    // re-tested here.
    {
        var s = Battle(attackForce: 8, defenseForce: 0, provinceStrength: 1).State; // attacker wins outright
        Add(s, "oracle-l5r-4658", 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        var defender = e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        Resolve(e);
        Equal(e.State.Cards[defender.Id].Zone, Zone.Dead, "Battle resolution's own sweep is excluded, regardless of who controls her");
    }
}
static void LadyMatsusRageBattleAction()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var matsu = Add(s, "oracle-l5r-4658", 0, Zone.Celestial);
    var performer = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // attacking, unbowed
    var horibe = Add(s, "oracle-l5r-6885", 0, Zone.Battlefield, province: 0); // Shinjo Horibe, unbowed
    var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // gives Horibe "opposed" too
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: matsu.Id, PerformerId: performer.Id, TargetId: target.Id, AbilityId: "lady-matsus-rage-bow-destroy"));
    Equal(e.State.Cards[matsu.Id].Zone, Zone.DynastyDiscard, "Discarded as this ability's own cost");
    Check(e.State.Cards[target.Id].Bowed, "Target bowed");
    Check(e.State.PendingLadyMatsusRageWatches.Any(w => w.TargetId == target.Id), "Watching for the next time it straightens, this turn");
    Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "Not destroyed yet -- only bowed so far");
    // Player 1 passes priority back; player 0 then straightens the SAME target via Shinjo Horibe --
    // the delayed destroy fires. Lady Matsu's Rage has already left play by now (discarded as this
    // very ability's own cost), so this is an ordinary death (Dead), NOT redirected to
    // RemovedFromGame -- her own trait only applies for as long as she is actually still in play.
    Accept(e, Cmd(e, "pass"));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: horibe.Id, TargetId: target.Id, AbilityId: "horibe-straighten"));
    Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Destroyed after straightening the same turn -- not redirected, since she already left play");
    Check(!e.State.PendingLadyMatsusRageWatches.Any(w => w.TargetId == target.Id), "Watch consumed");
}
static void SuitengusPathNavalGrant()
{
    // Attached via the real Equip (Battle) path -- a Spell's own AttachGrantsKeyword, new territory
    // (the mechanism itself is unchanged/generic; Suitengu's Path is the first published Spell to use it).
    var s = StrongholdBattle("oracle-l5r-1218");
    var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
    Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // opposed
    var spell = Add(s, "oracle-l5r-10133", 0, Zone.Hand); // GoldCost 0
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, SourceIds: [], TargetId: spell.Id, AbilityId: "equip-battle", PerformerId: shugenja.Id));
    Equal(e.State.Cards[spell.Id].Zone, Zone.Attached);
    Check(e.GetView(0).Players[0].Army.Single(c => c.Id == shugenja.Id).GrantedKeywords!.Any(g => g.Keyword == "Naval"), "The wielder gains Naval");
}
static void ScouringFloodForceAndMoveHome()
{
    // "This Personality has +2F" applies whether bowed or not, every phase -- unlike an Item, a
    // Spell never prints its own Force stat, so this is Effect-keyed (PersonalityForce's own new term).
    {
        var s = MatchEngine.CreateStarterMatch().State;
        var wielder = Add(s, "training-shugenja", 0, Zone.Home, bowed: true);
        Add(s, "oracle-l5r-6502", 0, Zone.Attached, attachedTo: wielder.Id);
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == wielder.Id).Force, 3, "Base Force 1 + 2 from Scouring Flood, applies even bowed");
    }
    // Battle ability: bowing itself is the cost; moves a targeted defending Personality home,
    // without bowing it further.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var wielder = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, "oracle-l5r-6502", 0, Zone.Attached, attachedTo: wielder.Id, province: 0);
        var defender = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: defender.Id, AbilityId: "scouring-flood-move-home"));
        Check(e.State.Cards[spell.Id].Bowed, "Bowing itself is the cost");
        Equal(e.State.Cards[defender.Id].Zone, Zone.Home, "Moved home");
        Check(!e.State.Cards[defender.Id].Bowed, "Move home does not itself bow the target");
    }
}
static void TheEarthsHungerCostChoice()
{
    // Without Earth: must bow the wielder -- the Spell itself is not a legal cost-payer.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var wielder = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); // Phoenix, no Earth keyword
        var spell = Add(s, "oracle-l5r-9889", 0, Zone.Attached, attachedTo: wielder.Id, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: enemy.Id, AbilityId: "earths-hunger-destroy", PerformerId: spell.Id));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: enemy.Id, AbilityId: "earths-hunger-destroy", PerformerId: wielder.Id));
        Check(e.State.Cards[wielder.Id].Bowed, "Wielder bowed -- the only legal cost-payer without Earth");
        Check(!e.State.Cards[spell.Id].Bowed, "Spell itself untouched");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
    }
    // With Earth: the player may choose to bow the Spell itself instead, leaving the wielder unbowed.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var wielder = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        wielder.GrantedKeywords = [new("Earth", "test-fixture", "Permanent")];
        var spell = Add(s, "oracle-l5r-9889", 0, Zone.Attached, attachedTo: wielder.Id, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: enemy.Id, AbilityId: "earths-hunger-destroy", PerformerId: spell.Id));
        Check(e.State.Cards[spell.Id].Bowed, "Chose to bow the Spell itself instead, since the wielder is Earth");
        Check(!e.State.Cards[wielder.Id].Bowed, "Wielder left unbowed");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
    }
}
static void KitsuAkaiCreatesLionAncestor()
{
    // The engine's first "create a card" effect (MatchEngine.CreatePersonality). No restriction on
    // created-card copies (issue #257's own spec): a Lion Ancestor already in play does not block
    // creating a second one.
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
    var akai = Add(s, "oracle-l5r-4371", 0, Zone.Home);
    Add(s, "oracle-l5r-9785", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    var before = e.State.Cards.Keys.ToHashSet();
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: akai.Id, AbilityId: "kitsu-akai-create"));
    var createdId = e.State.Cards.Keys.Except(before).Single();
    var created = e.State.Cards[createdId];
    Equal(created.DefinitionId, "oracle-l5r-9785", "Created directly from the definition, not drawn from any deck/Province");
    Equal(created.OwnerId, 0);
    Equal(created.Zone, Zone.Home, "Enters play directly in Home, like any other Personality (Zone.Home already IS in play here)");
    Check(created.FaceUp, "Face up on entry");
    Check(!created.Bowed, "Straightened by default -- the Lion Ancestor carries no enters-bowed effect");
    Check(e.State.PendingChoice is null, "No Proclaim window -- created, not recruited; and no entry-reaction/Overconfident window either, since the Lion Ancestor has neither");
    Equal(e.State.Cards.Values.Count(c => c.DefinitionId == "oracle-l5r-9785" && c.OwnerId == 0), 2, "Coexists with the pre-existing copy -- no restriction on created-card copies");
    var def = CardCatalog.Get(created.DefinitionId);
    Equal(def.Force, 2); Equal(def.Chi, 2); Equal(def.PersonalHonor, 3);
    Check(def.CreatedOnly, "Flagged as created-only -- cannot be included in a deck");
    foreach (var keyword in new[] { "Lion Clan", "Samurai", "Ancestor", "Spirit" })
        Check(def.Keywords!.Contains(keyword), $"Carries the {keyword} keyword named in Kitsu Akai's own Limited text");
    // Limited, once per turn, like any other.
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: akai.Id, AbilityId: "kitsu-akai-create"));
}
static void KitsuAkaiBattleAncestorCost()
{
    // "Bow your performing Ancestor Personality" -- own-unbowed-personality-at-battle-with-keyword,
    // Volcano Tattoo's own generic Performer category, with a single RequiredKeywords entry. Kitsu
    // Akai himself does not qualify (no Ancestor keyword of his own); the created Lion Ancestor
    // (or any other own Ancestor Personality at the battle) does.
    var s = StrongholdBattle("oracle-l5r-1218");
    var akai = Add(s, "oracle-l5r-4371", 0, Zone.Battlefield, province: 0);
    var ancestor = Add(s, "oracle-l5r-9785", 0, Zone.Battlefield, province: 0);
    var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    var honorBefore = e.State.Players[0].Honor;
    // Kitsu Akai himself lacks the Ancestor keyword.
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: akai.Id, TargetId: enemy.Id, AbilityId: "kitsu-akai-battle", PerformerId: akai.Id));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: akai.Id, TargetId: enemy.Id, AbilityId: "kitsu-akai-battle", PerformerId: ancestor.Id));
    Check(e.State.Cards[ancestor.Id].Bowed, "The performing Ancestor Personality is bowed as the cost");
    Check(!e.State.Cards[akai.Id].Bowed, "Kitsu Akai himself is untouched -- he is not the performer here");
    Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
    Equal(e.State.Players[0].Honor, honorBefore + 1, "Gain 1 Honor");
}
static void LionAncestorDestructionRemovedFromGame()
{
    // A created card has no physical printed card to return to any discard pile: once ordinary
    // destruction rules finish with it, it goes to RemovedFromGame instead -- exercised here through
    // battle resolution's own Force-comparison casualty sweep (no actingPlayerId at all), the most
    // common way one actually dies, not a specific ability's own actingPlayerId-carrying destroy.
    // Dishonorable=true also exercises the ordinary Dishonorable-death penalty first, confirming
    // "it follows the rules of destruction" -- normal Personality death consequences are untouched,
    // only where the card ends UP afterward changes.
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
    s.Players[1].Provinces[0].Strength = 1;
    var attacker = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
    attacker.Modifiers = [new("test-fixture", "test-fixture", "Force", 8 - 5)]; // Force 8, outright win
    var ancestor = Add(s, "oracle-l5r-9785", 1, Zone.Battlefield, province: 0); // Force 2, defending
    ancestor.Dishonorable = true;
    var e = MatchEngine.FromTestState(s);
    var honorBefore = e.State.Players[1].Honor;
    Resolve(e);
    Equal(e.State.Cards[ancestor.Id].Zone, Zone.RemovedFromGame, "Created card: removed from the game once destruction finishes, not left in an ordinary discard pile");
    Equal(e.State.Players[1].Honor, honorBefore - CardCatalog.Get(ancestor.DefinitionId).PersonalHonor, "Ordinary destruction rules still applied first -- the Dishonorable-death penalty fires exactly as it would for any other Personality");
}
static void FormalApologyWatch()
{
    // Entry-time trigger: Honor is ALREADY below starting the moment the event resolves -- "the next
    // time" is right now, not merely from this point forward.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var startingHonor = CardCatalog.Get(s.Cards[s.Players[1].StrongholdId].DefinitionId).StartingHonor;
        s.Players[1].Honor = startingHonor - 1;
        var card = Add(s, "oracle-l5r-2660", 1, Zone.Province, province: 0);
        card.FaceUp = false;
        s.Players[1].Provinces[0].CardId = card.Id;
        var e = MatchEngine.FromTestState(s);
        SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Cards[card.Id].Zone, Zone.DynastyDiscard, "Triggered immediately: Honor was already below starting at entry");
        Equal(e.State.Players[1].Honor, startingHonor - 1 + 5);
    }
    // Continuous watch: sits inert while Honor stays at/above starting, then fires the first time it
    // LATER drops below, via the ordinary GainHonor path (here: a Dishonorable death's own existing
    // penalty) -- not merely a one-time snapshot check at entry.
    {
        var s = Battle(9, 2, 6).State;
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        defender.Dishonorable = true; // training-lion-veteran: PersonalHonor 3
        var startingHonor = CardCatalog.Get(s.Cards[s.Players[1].StrongholdId].DefinitionId).StartingHonor;
        s.Players[1].Honor = startingHonor + 2;
        var watch = Add(s, "oracle-l5r-2660", 1, Zone.EventInPlay);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Players[1].Honor, startingHonor + 2 - 3 + 5, "Dishonorable-death penalty (-3) drops Honor below starting, then the tea ceremony (+5) fires -- via the same shared GainHonor path, so any future honor-gain hook would apply equally");
        Equal(e.State.Cards[watch.Id].Zone, Zone.DynastyDiscard, "One-shot: discarded once triggered");
    }
}
static void HarshChoicesProvinceDestroy()
{
    // Destroy: draws 3, the province itself is destroyed (not merely discarded/refilled).
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var card = Add(s, "oracle-l5r-2978", 1, Zone.Province, province: 0);
        card.FaceUp = false;
        s.Players[1].Provinces[0].CardId = card.Id;
        var handBefore = s.Players[1].Hand.Count;
        var e = MatchEngine.FromTestState(s);
        SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Check(e.State.PendingChoice?.Type == "harshChoices", "Opens a pending choice instead of auto-resolving like every other Event so far");
        Accept(e, new GameCommand("harshChoices", 1, e.Version, Choice: "destroy"));
        Check(e.State.Players[1].Provinces[0].Destroyed, "Province destroyed");
        Equal(e.State.Cards[card.Id].Zone, Zone.DynastyDiscard);
        Equal(e.State.Players[1].Hand.Count, handBefore + 3);
        Equal(e.State.Phase, Phase.Action, "Events Phase resumes and completes normally afterward");
    }
    // Decline: resolves as an ordinary Event -- discarded, province refills, no draw.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var card = Add(s, "oracle-l5r-2978", 1, Zone.Province, province: 0);
        card.FaceUp = false;
        s.Players[1].Provinces[0].CardId = card.Id;
        var handBefore = s.Players[1].Hand.Count;
        var e = MatchEngine.FromTestState(s);
        SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Accept(e, new GameCommand("harshChoices", 1, e.Version, Choice: "decline"));
        Check(!e.State.Players[1].Provinces[0].Destroyed, "Province not destroyed");
        Equal(e.State.Cards[card.Id].Zone, Zone.DynastyDiscard);
        Check(e.State.Players[1].Provinces[0].CardId != card.Id, "Refilled face down");
        Equal(e.State.Players[1].Hand.Count, handBefore, "No draw when declined");
    }
}
static void RebuildingKyudenHidaDuration()
{
    // Its own restriction needs no enforcement code: verified already the engine's unconditional
    // default (SelectBattle's one and only SetPhase(Phase.Battle,...) call always seeds the
    // Defender's own priority first, so the Attacker can never act before the Defender's first
    // opportunity regardless of this card). This test exercises only the bounded duration itself --
    // "your second turn from now begins" -- counted in the CONTROLLER's own turns (TurnsTaken), not
    // raw shared TurnNumber (four raw turns pass here for two of the controller's own).
    var s = MatchEngine.CreateEconomyScenario().State;
    var card = Add(s, "oracle-l5r-6200", 1, Zone.Province, province: 0);
    card.FaceUp = false;
    s.Players[1].Provinces[0].CardId = card.Id;
    var e = MatchEngine.FromTestState(s);
    EndTurnFully(e); // player 0's turn ends -> player 1's turn (reveal)
    Equal(e.State.Cards[card.Id].Zone, Zone.EventInPlay);
    EndTurnFully(e); // player 1's turn ends -> player 0's turn
    EndTurnFully(e); // player 0's turn ends -> player 1's own NEXT turn
    Equal(e.State.Cards[card.Id].Zone, Zone.EventInPlay, "Still active through the controller's own next turn (\"your first turn from now\")");
    EndTurnFully(e); // player 1's turn ends -> player 0's turn
    EndTurnFully(e); // player 0's turn ends -> player 1's SECOND-next turn
    Equal(e.State.Cards[card.Id].Zone, Zone.DynastyDiscard, "Effect ended: the controller's second turn from now has begun");
}
static void TimesOfStrifeProvinceClampAndDiscard()
{
    // Province-strength clamp: a global CEILING (either side's provinces, not scoped to "your own"),
    // never a floor -- Ring of Earth's own +3 raise is capped back down to the province's own printed
    // strength while Times of Strife is active.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        Add(s, "oracle-l5r-8494", 0, Zone.EventInPlay); // Times of Strife
        var printed = s.Players[1].Provinces[0].PrintedStrength;
        var ring = Add(s, "training-ring-of-earth-stub", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-raise", TargetPlayerId: 1, Province: 0));
        Equal(e.State.ProvinceStrengthEffects.Where(x => x.PlayerId == 1 && x.ProvinceIndex == 0).Sum(x => x.Amount), 3, "The raise (until end of turn, issue #492 ruling R3) still applies underneath");
        Equal(e.GetView(0).Players[1].Provinces[0].Strength, printed, "But the EFFECTIVE (client-visible) strength is clamped back down to printed");
    }
    // Discard trigger: "another player who controls no units at the current battlefield announces a
    // Battle action" -- Ring of Earth's own "even with no units present" exemption from the ordinary
    // blanket Battle-timing presence Require is the only currently reachable path to this (every
    // other Battle-timed ability requires the acting player's own presence already). Queued and
    // drained through the existing Overconfident-draw window rather than a bespoke one.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1; s.ActivePlayerId = 0;
        Add(s, "oracle-l5r-8494", 0, Zone.EventInPlay); // player 0 controls Times of Strife
        var ring = Add(s, "training-ring-of-earth-stub", 1, Zone.Home); // player 1: no units anywhere at this battlefield
        var handCard = Add(s, "training-farm", 1, Zone.Hand).Id;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-lower", TargetPlayerId: 0, Province: 0));
        Check(e.State.PendingChoice?.Type == "forcedDiscard", "Queued, then opened as a deferred window (the general forced discard, issue #333)");
        Equal(e.State.PendingChoice!.PlayerId, 1);
        Check(e.QueryActions(1).Any(a => a.Type == "forcedDiscard" && a.Enabled && a.CardId == handCard && a.Command?.TargetId == handCard), "The discard is offered through QueryActions, one option per hand card");
        Accept(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: handCard));
        Check(!e.State.Players[1].Hand.Contains(handCard), "Discarded");
        Equal(e.State.Cards[handCard].Zone, Zone.FateDiscard);
    }
}
static void WarWearinessFirstActionPerPhaseBows()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    Add(s, "oracle-l5r-9222", 0, Zone.EventInPlay); // War Weariness
    var first = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
    first.GrantedKeywords = [new("Tactician", "test-fixture", "Permanent")];
    var second = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
    second.GrantedKeywords = [new("Tactician", "test-fixture", "Permanent")];
    var hand1 = Add(s, "training-farm", 0, Zone.Hand).Id;
    var hand2 = Add(s, "training-marketplace", 0, Zone.Hand).Id;
    var e = MatchEngine.FromTestState(s);
    // Tactical Advantage never bows its own performer (no "unbowed" qualifier printed) -- any bow
    // here is War Weariness's own doing.
    Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: hand1, AbilityId: "tactical-advantage", PerformerId: first.Id));
    Check(e.State.Cards[first.Id].Bowed, "War Weariness bows the first Personality-performed action's performer this phase");
    // Priority passed to player 1 after player 0's own action, same as any ordinary action -- player
    // 1 passes it straight back so player 0 can act again this same Battle phase.
    Accept(e, Cmd(e, "pass"));
    Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: hand2, AbilityId: "tactical-advantage", PerformerId: second.Id));
    Check(!e.State.Cards[second.Id].Bowed, "Only the FIRST action each phase bows its performer -- the once-per-phase trigger is already spent");
}
static void SecondCityStrategyWave6()
{
    // Dispensing Justice: destroys an eligible target (no attachments, and Dishonorable/Kolat/
    // Ninja/Shadowlands), otherwise moves the target home instead.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-2029", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Magistrate", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        target.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "dispensing-justice-destroy", PerformerId: performer.Id));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "No attachments and Dishonorable: destroyed");
        Equal(e.State.Cards[strat.Id].Zone, Zone.FateDiscard, "The Strategy card itself is discarded after use, played from hand");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-2029", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Magistrate", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "dispensing-justice-destroy", PerformerId: performer.Id));
        Equal(e.State.Cards[target.Id].Zone, Zone.Home, "Not eligible for destruction (Honorable, no qualifying keyword): moved home instead");
    }
    // Bow Before My Will: baseline without the Shadowlands keyword. StabilizationTests
    // also covers its conditional straightening block with a Shadowlands performer.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-1121", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "bow-before-my-will", PerformerId: performer.Id));
        Check(e.State.Cards[target.Id].Bowed, "Target bowed");
        Equal(e.State.Players[0].Honor, honorBefore - 2, "Costs 2 Honor");
        Check(e.State.Cards[target.Id].StraightenBlockedTurn != e.State.TurnNumber, "No negated straightening -- the performer is not Shadowlands");
    }
    // Surety of Purpose: bows an enemy Personality with equal-or-lower Personal Honor than the
    // performer (lower-or-equal-honor-than-performer, Shamate Keep's own existing Target category)
    // and gains 2 Honor.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-7664", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "surety-of-purpose", PerformerId: performer.Id));
        Check(e.State.Cards[target.Id].Bowed, "Bowed");
        Equal(e.State.Players[0].Honor, honorBefore + 2, "Gains 2 Honor");
    }
    // Unexpected Support: bows an enemy card with as-many-or-fewer Followers than the performer
    // (the new enemy-fewer-or-equal-followers-than-performer Target category) and negates its next
    // straightening.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8977", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Commander", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "unexpected-support", PerformerId: performer.Id));
        Check(e.State.Cards[target.Id].Bowed, "Bowed");
        Equal(e.State.Cards[target.Id].StraightenBlockedTurn, e.State.TurnNumber, "Negated its next straightening");
    }
    // Wrathful Ancestor: moves an enemy Personality home. "This movement will not be negated" is
    // exemption by omission -- wrathful-ancestor-move is deliberately never registered in
    // MoveOrStraightenKind, so there is no separate negation path to test against; a plain MoveHome
    // is already exhaustively tested elsewhere (journeys-end-move/shamate-move/mihato-move).
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-9390", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Spirit", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "wrathful-ancestor-move", PerformerId: performer.Id));
        Equal(e.State.Cards[target.Id].Zone, Zone.Home, "Moved home");
    }
}
static void SecondCityPersonalityWave18()
{
    // Keppo: "If you have Reconnaissance" fizzles the effect (like training-reconnaissance-strike's
    // own precedent) rather than blocking the action outright -- separate MatchStates since the
    // ability is limited to once per turn by default.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var keppo = Add(s, "oracle-l5r-4328", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 1, Zone.Attached, attachedTo: enemy.Id, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: keppo.Id, TargetId: item.Id, AbilityId: "keppo-destroy"));
        Equal(e.State.Cards[item.Id].Zone, Zone.Attached, "No Reconnaissance: fizzles, nothing destroyed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var keppo = Add(s, "oracle-l5r-4328", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 1, Zone.Attached, attachedTo: enemy.Id, province: 0);
        s.Players[1].Provinces[0].Reconnaissance[0] = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: keppo.Id, TargetId: item.Id, AbilityId: "keppo-destroy"));
        Check(e.State.Cards[item.Id].Zone != Zone.Attached, "With Reconnaissance: destroyed");
    }
    // Kuronada: target an enemy card without attachments, destroy it (RequireNoAttachments at the
    // ability level, the same shape as Kitsu Akai's own Battle ability).
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var kuronada = Add(s, "oracle-l5r-4611", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kuronada.Id, TargetId: enemy.Id, AbilityId: "kuronada-destroy"));
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
    }
    // Matsu Kasei: +2F while attacking (Effect="attacking-two"), and her own Battle ability reuses
    // "incapacitated" verbatim (Target a defending Personality: Move him home).
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var kasei = Add(s, "oracle-l5r-4937", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // opposed
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == kasei.Id).Force, CardCatalog.Get("oracle-l5r-4937").Force + 2, "+2F while attacking");
        var defender = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: kasei.Id, TargetId: defender.Id, AbilityId: "incapacitated"));
        Equal(e2.State.Cards[defender.Id].Zone, Zone.Home, "Moved the defending Personality home");
    }
    // Moto Ming-Gwok: the Attack itself is gated on a dead Personality existing anywhere, not merely
    // conditionally boosted -- rejected without one, accepted with one (the same Require shape
    // Torazu's own Fire-token gate already uses).
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var mingGwok = Add(s, "oracle-l5r-5351", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mingGwok.Id, AbilityId: "moto-ming-gwok-attack"));
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var mingGwok = Add(s, "oracle-l5r-5351", 0, Zone.Battlefield, province: 0);
        var dead = Add(s, "training-crane-samurai", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mingGwok.Id, AbilityId: "moto-ming-gwok-attack"));
    }
    // Shiba Kudome: self-performer Iaijutsu duel -- bows the loser; grants herself a +1F Fire token
    // only if she won AND her controller controls a Shugenja. Neither side focuses (daisuke-duel's
    // own minimal test pattern): the challenged player strikes immediately, then -- since Kudome
    // herself carries Duelist -- she declines her own extra refocus opportunity.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var kudome = Add(s, "oracle-l5r-6774", 0, Zone.Battlefield, province: 0); // Chi 4
        Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3 -- Kudome wins
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kudome.Id, TargetId: target.Id, AbilityId: "shiba-kudome-duel"));
        Accept(e, new GameCommand("strike", 1, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 0, e.Version));
        Check(e.State.Cards[target.Id].Bowed, "Loser bowed");
        Check(e.State.Cards[kudome.Id].Tokens.Count(t => t.Type == "Fire") == 1, "Won, and controls a Shugenja: +1F Fire token");
    }
    // Shosuro Ritoru: bows any enemy card; destroys it only when it also has no attachments AND
    // Ritoru's own controller controls a (separate) Courtier.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var ritoru = Add(s, "oracle-l5r-7153", 0, Zone.Battlefield, province: 0);
        var courtier = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        courtier.GrantedKeywords = [new("Courtier", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ritoru.Id, TargetId: target.Id, AbilityId: "shosuro-ritoru-bow"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "No attachments, and Ritoru's own controller controls a Courtier");
    }
    // Takasho: reinforces the current battlefield from elsewhere (rikyou-move's own exact shape) and
    // grants himself a scoped Additional Battle action; separately, a plain Ranged 5 Attack.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var takasho = Add(s, "oracle-l5r-7741", 0, Zone.Home);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // enemy unit present
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: takasho.Id, AbilityId: "takasho-move"));
        Equal(e.State.Cards[takasho.Id].Zone, Zone.Battlefield, "Reinforced the current battlefield");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds != null && g.ScopeCardIds.Contains(takasho.Id)), "Granted an Additional Battle action scoped to Takasho himself");
    }
    // Yogo Adi: Political Open -- a dishonorable target's controller loses 1 Honor; otherwise every
    // one of Adi's own provinces gains +2 strength.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var adi = Add(s, "oracle-l5r-9485", 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 1, Zone.Home);
        target.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: adi.Id, TargetId: target.Id, AbilityId: "yogo-adi-choice"));
        Equal(e.State.Players[1].Honor, honorBefore - 1, "Dishonorable target: controller loses 1 Honor");
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var adi = Add(s, "oracle-l5r-9485", 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 1, Zone.Home);
        var strengthBefore = s.Players[0].Provinces[0].Strength;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: adi.Id, TargetId: target.Id, AbilityId: "yogo-adi-choice"));
        Check(e.State.Players[0].Provinces.All(p => e.ProvinceStrengthForTest(0, p.Index) == strengthBefore + 2), "Not dishonorable: all of Adi's own provinces gain +2 strength instead");
        Check(e.State.Players[0].Provinces.All(p => Batch336Support.StrengthNextTurn(e, 0, p.Index) == strengthBefore), "Yogo Adi: the +2 lasts until end of turn (issue #492 ruling R3)");
    }
}
static void SecondCityRestBatch()
{
    // Archery Range: a Holding (Zone.Province) boosting any Ranged Attack, no presence restriction
    // of its own (unlike Khol Regulars' own AtBattle-gated precedent).
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var shikaro = Add(s, "oracle-l5r-6850", 0, Zone.Battlefield, province: 0); // Shikaro: Ranged 4 Attack
        var range = Add(s, "oracle-l5r-430", 0, Zone.Home);
        Add(s, "training-attack-reactor", 1, Zone.Home); // the opponent still holds a Reaction, so the window stays open to read the strength (issue #644)
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shikaro.Id, TargetId: target.Id, AbilityId: "shikaro-attack"));
        Equal(e.State.PendingAttack?.Strength, 4, "Base Ranged 4");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: range.Id, AbilityId: "archery-range-reaction"));
        Equal(e.State.PendingAttack?.Strength, 5, "4 base + 1 from Archery Range");
    }
    // Slanderer: bows itself as its own cost; dishonors the target only while its controller controls
    // a Courtier or Magistrate.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var slanderer = Add(s, "oracle-l5r-7291", 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: slanderer.Id, TargetId: target.Id, AbilityId: "slanderer-dishonor"));
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var slanderer = Add(s, "oracle-l5r-7291", 0, Zone.Home);
        var courtier = Add(s, "training-crane-samurai", 0, Zone.Home);
        courtier.GrantedKeywords = [new("Courtier", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: slanderer.Id, TargetId: target.Id, AbilityId: "slanderer-dishonor"));
        Check(e.State.Cards[slanderer.Id].Bowed, "Bowed as cost");
        Check(e.State.Cards[target.Id].Dishonorable, "Dishonored");
    }
    // Temple of Hotei: bows a performing Shugenja (the generic "shugenja" Performer category) to gain
    // 1 Honor.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var temple = Add(s, "oracle-l5r-7875", 0, Zone.Home);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: temple.Id, AbilityId: "temple-of-hotei-honor", PerformerId: shugenja.Id));
        Check(e.State.Cards[shugenja.Id].Bowed, "Performer bowed");
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Gained 1 Honor");
    }
    // Tranquil Garden: straightens a Ring already in play, or -- targeting one already in this
    // player's own Fate discard pile instead -- returns it to hand and destroys itself.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var garden = Add(s, "oracle-l5r-8721", 0, Zone.Home);
        var ring = Add(s, "oracle-l5r-6333", 0, Zone.Home, bowed: true); // Ring of Air, in play
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: garden.Id, TargetId: ring.Id, AbilityId: "tranquil-garden-ring"));
        Check(e.State.Cards[garden.Id].Bowed, "Bowed as cost");
        Check(!e.State.Cards[ring.Id].Bowed, "In-play Ring straightened");
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var garden = Add(s, "oracle-l5r-8721", 0, Zone.Home);
        var ring = Add(s, "oracle-l5r-6333", 0, Zone.FateDiscard); // Ring of Air, in its own controller's Fate discard pile
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: garden.Id, TargetId: ring.Id, AbilityId: "tranquil-garden-ring"));
        Check(e.State.Players[0].Hand.Contains(ring.Id), "Ring in own Fate discard: returned to hand instead");
        Equal(e.State.Cards[garden.Id].Zone, Zone.DynastyDiscard, "Tranquil Garden destroyed itself, into its own Dynasty discard pile (a Holding, not a Personality)");
    }
    // Flesh Cutter Arrows: only reacts to a Ranged Attack performed by its OWN wielder, targeting a
    // card with no attached Armor.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "oracle-l5r-6850", 0, Zone.Battlefield, province: 0); // Shikaro: Ranged 4 Attack
        var arrows = Add(s, "oracle-l5r-2608", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-attack-reactor", 1, Zone.Home); // the opponent still holds a Reaction, so the window stays open to read the strength (issue #644)
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: host.Id, TargetId: target.Id, AbilityId: "shikaro-attack"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: arrows.Id, AbilityId: "flesh-cutter-arrows-reaction"));
        Check(e.State.Cards[arrows.Id].Bowed, "Bowed as cost");
        Equal(e.State.PendingAttack?.Strength, 6, "4 base + 2 from Flesh Cutter Arrows");
    }
    // Fubatsu Plate: two mutually-exclusive-per-battle (shared UsageKey) "even if bowed" Battle
    // abilities on its own wielder.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var plate = Add(s, "oracle-l5r-2719", 0, Zone.Attached, bowed: true, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: plate.Id, AbilityId: "fubatsu-plate-straighten"));
        Check(!e.State.Cards[host.Id].Bowed, "Straightened its own bowed wielder, even though Fubatsu Plate itself is bowed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var plate = Add(s, "oracle-l5r-2719", 0, Zone.Attached, bowed: true, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: plate.Id, AbilityId: "fubatsu-plate-move"));
        Equal(e.State.Cards[host.Id].Zone, Zone.Home, "Moved its own wielder home, even though Fubatsu Plate itself is bowed");
    }
    // Nightingale Blade: bows itself and target; destroys the target only when it is an attachment AND
    // its own wielder is Dragon Clan or a Kensai.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var blade = Add(s, "oracle-l5r-5556", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: blade.Id, TargetId: enemy.Id, AbilityId: "nightingale-blade-bow"));
        Check(e.State.Cards[blade.Id].Bowed && e.State.Cards[enemy.Id].Bowed, "Both bowed");
        Check(e.State.Cards[enemy.Id].Zone != Zone.Dead, "Not an attachment: not destroyed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); host.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
        var blade = Add(s, "oracle-l5r-5556", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyItem = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: blade.Id, TargetId: enemyItem.Id, AbilityId: "nightingale-blade-bow"));
        Equal(e.State.Cards[enemyItem.Id].Zone, Zone.FateDiscard, "An attachment, and the wielder is a Kensai: destroyed, into its own Fate discard pile (an Item, not a Personality)");
    }
    // Satoshi's Dual Warfans: entering play draws a card; the equipping player's own Clan discounts
    // its Gold cost by 1 only when Lion.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0; s.Players[0].Clan = "Lion";
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var warfans = Add(s, "oracle-l5r-6481", 0, Zone.Hand); // GoldCost 3
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 2).First(); // 3 - 1 (Lion Clan discount)
        Accept(e, Cmd(e, "equip", id: warfans.Id, sources: payment.SourceIds, target: host.Id));
        Equal(e.State.Cards[warfans.Id].Zone, Zone.Attached, "Equipped at the discounted cost of 2");
        Equal(e.State.PendingChoice?.Type, "entryReaction", "Entering play opens its own Reaction window");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: warfans.Id, AbilityId: "satoshi-warfans-draw"));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1, "Drew a card");
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0; s.Players[0].Clan = "Crane";
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var warfans = Add(s, "oracle-l5r-6481", 0, Zone.Hand); // GoldCost 3
        var e = MatchEngine.FromTestState(s);
        // Not Lion Clan: the discounted (2 Gold) payment is short of the real (3 Gold) cost by 1.
        Reject(e, Cmd(e, "equip", id: warfans.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: host.Id));
    }
    // Overgrown Grove: a Region (never AtBattle-eligible) restricted instead to its own province;
    // discards the top 3 cards of its controller's own Fate deck and moves an attacking Personality
    // home.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; // player 1 is the Attacker
        var grove = Add(s, "oracle-l5r-5841", 0, Zone.Region, province: 0);
        var fateDeck = s.Players[0].FateDeck; for (var i = 0; i < 5; i++) fateDeck.Add(Add(s, "training-item", 0, Zone.FateDeck).Id);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Grove's own controller needs presence to take a Battle action
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var deckBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: grove.Id, TargetId: attacker.Id, AbilityId: "overgrown-grove-move"));
        Equal(e.State.Players[0].FateDeck.Count, deckBefore - 3, "Discarded the top 3 cards of its own controller's Fate deck");
        Equal(e.State.Cards[attacker.Id].Zone, Zone.Home, "Moved the attacking Personality home");
    }
    // Intervening Spirit: reinforces an own Personality into the current battlefield from elsewhere,
    // even when its own wielder is not there, gated on enemy presence; grants an additional Battle
    // action only for an Ancestor moved by a Water-keyword Shugenja.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915) // Intervening Spirit's own wielder, not at the battlefield
        var spirit = Add(s, "oracle-l5r-3788", 0, Zone.Attached, attachedTo: host.Id);
        var mover = Add(s, "training-lion-samurai", 0, Zone.Home);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // enemy presence
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spirit.Id, TargetId: mover.Id, AbilityId: "intervening-spirit-move"));
        Equal(e.State.Cards[mover.Id].Zone, Zone.Battlefield, "Moved into the current battlefield");
        Check(!e.State.Cards[mover.Id].Bowed, "Straightened since it was not already at the battlefield");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Not an Ancestor: no additional Battle action granted");
    }
    // The Earth Flows: Defender-only; reinforces a Personality into the current battlefield; its own
    // passive Honor bonus (a new Effect="earth-flows-honor" checked from CheckEarthFlowsBonus) fires
    // only for the winning side, keyed off an Earth-keyword wielder.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 1; // player 0 is the Attacker; player 1 is the Defender
        // Unlike Intervening Spirit's own reinforce-from-elsewhere exemption, Earth Flows' own wielder
        // must be at the current battlefield already (no "even if" clause of its own, so it falls back
        // to the ordinary attached-Spell-source AtBattle Require -- Scouring Flood/Earth's Hunger's
        // own precedent), which also doubles as the Defender's own presence for this Battle action.
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var flows = Add(s, "oracle-l5r-8038", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var mover = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: flows.Id, TargetId: mover.Id, AbilityId: "earth-flows-move"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: flows.Id, TargetId: mover.Id, AbilityId: "earth-flows-move"));
        Equal(e.State.Cards[mover.Id].Zone, Zone.Battlefield, "Moved into the current battlefield");
    }
    {
        // Battle resolution's own +2 Honor bonus: the wielder needs the Earth keyword, and must end up
        // on the winning side. Defender's Force (6) beats the Attacker's (2), so player 1 wins;
        // isolate the bonus itself by diffing against an identical battle with no Earth Flows attached.
        var baseline = Battle(2, 6); Resolve(baseline);
        var baselineHonor = baseline.State.Players[1].Honor;
        var s = Battle(2, 6).State;
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        defender.GrantedKeywords = [new("Earth", "test-fixture", "Permanent")];
        Add(s, "oracle-l5r-8038", 1, Zone.Attached, province: 0, attachedTo: defender.Id);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Players[1].Honor, baselineHonor + 2, "The Earth Flows: +2 Honor on top of ordinary battle-victory Honor, for the winning Defender's own Earth-keyword wielder");
    }
    // Winds of Dismissal: bows its own wielder as its cost; an eligible (Dishonorable/Kolat/Ninja/
    // Shadowlands) target's controller loses Honor equal to the target's own base Personal Honor; a
    // second, independent +1 Honor for the caster when the wielder is Air.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var host = Add(s, "training-shugenja", 0, Zone.Home); host.GrantedKeywords = [new("Air", "test-fixture", "Permanent")];
        var winds = Add(s, "oracle-l5r-9344", 0, Zone.Attached, attachedTo: host.Id);
        var target = Add(s, "training-crane-samurai", 1, Zone.Home);
        target.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var casterHonorBefore = e.State.Players[0].Honor;
        var targetHonorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: winds.Id, TargetId: target.Id, AbilityId: "winds-of-dismissal-choice"));
        Check(e.State.Cards[host.Id].Bowed, "Bowed its own wielder as cost");
        Equal(e.State.Players[1].Honor, targetHonorBefore - CardCatalog.Get("training-crane-samurai").PersonalHonor, "Dishonorable target: controller loses Honor equal to its base Personal Honor");
        Equal(e.State.Players[0].Honor, casterHonorBefore + 1, "Wielder is Air: +1 Honor too");
    }
    // Strategic Sacrifice: the one false negative the BLOCKERS documentation pass itself caught. A
    // plain Siege Performer, a new Force-vs-province-strength Target, and "Bow or straighten" as an
    // unconditional toggle (straighten if already bowed, bow otherwise).
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.Players[1].Provinces[0].Strength = 4;
        var strat = Add(s, "oracle-l5r-7524", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Siege", "test-fixture", "Permanent")];
        var tooStrong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, above the province's 4
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: tooStrong.Id, AbilityId: "strategic-sacrifice-toggle", PerformerId: performer.Id));
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.Players[1].Provinces[0].Strength = 4;
        var strat = Add(s, "oracle-l5r-7524", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Siege", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 3, at or below the province's 4
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strategic-sacrifice-toggle", PerformerId: performer.Id));
        Check(e.State.Cards[target.Id].Bowed, "Was unbowed: toggled to bowed");
        Equal(e.State.Players[0].Honor, honorBefore + 2, "Gained 2 Honor");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.Players[1].Provinces[0].Strength = 4;
        var strat = Add(s, "oracle-l5r-7524", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Siege", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strategic-sacrifice-toggle", PerformerId: performer.Id));
        Check(!e.State.Cards[target.Id].Bowed, "Was bowed: toggled to straightened");
    }
}
static void BattleOpenPresencePrimitive()
{
    // The Weakness of Man (issue #267): the proof case for the general Battle/Open presence
    // primitive. Open half: performed from Home, no presence check at all (the Monk performer is
    // nowhere near a battlefield, and there isn't even one in progress).
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-8425", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home); // base Force 3
        performer.GrantedKeywords = [new("Monk", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Home); // Force 4, different from the performer's base Force
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "weakness-of-man-toggle", PerformerId: performer.Id));
        Check(e.State.Cards[target.Id].Bowed, "Was unbowed: toggled to bowed, via the Open route with no battle in progress at all");
    }
    // Battle half: now a genuine Battle action -- the performing Monk must actually be at the
    // current battlefield, exactly like an ordinary Battle ability.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8425", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // base Force 3
        performer.GrantedKeywords = [new("Monk", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "weakness-of-man-toggle", PerformerId: performer.Id));
        Check(!e.State.Cards[target.Id].Bowed, "Was bowed: toggled to straightened, via the Battle route with the performer present");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8425", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home); // NOT at the current battlefield
        performer.GrantedKeywords = [new("Monk", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "weakness-of-man-toggle", PerformerId: performer.Id));
    }
    // The target's current Force must differ from that same target's printed Force, and the
    // target itself must also be at the current battlefield during the Battle half.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8425", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // base Force 3
        performer.GrantedKeywords = [new("Monk", "test-fixture", "Permanent")];
        var sameForce = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // also base Force 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: sameForce.Id, AbilityId: "weakness-of-man-toggle", PerformerId: performer.Id));
    }
    // Improved Defenses (issue #267): the proof case for "at any location" -- a broadened, not
    // removed, presence requirement. The performing Artisan may be at Home...
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-3720", 0, Zone.Hand);
        var artisan = Add(s, "training-crane-samurai", 0, Zone.Home);
        artisan.GrantedKeywords = [new("Artisan", "test-fixture", "Permanent")];
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // the acting player's own presence at the battlefield, via a different unit
        var fateDeck = s.Players[0].FateDeck; fateDeck.Insert(0, Add(s, "training-item", 0, Zone.FateDeck).Id); // top card, Focus 0 by default
        var e = MatchEngine.FromTestState(s);
        var strengthBefore = e.ProvinceStrengthForTest(1, 0);
        var deckBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "improved-defenses-strength", PerformerId: artisan.Id));
        Check(e.State.Cards[artisan.Id].Bowed, "Bowed as cost");
        Equal(e.State.Players[0].FateDeck.Count, deckBefore - 1, "Discarded the top card of the Fate deck");
        Equal(e.ProvinceStrengthForTest(1, 0), strengthBefore + 1, "Focus Value 0 + 1");
        Equal(Batch336Support.StrengthNextTurn(e, 1, 0), strengthBefore, "Improved Defenses: until end of turn (issue #492 ruling R3)");
    }
    // ...or at another battlefield that has not yet resolved this Battle phase...
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.UnresolvedBattlefields = [0, 1];
        var strat = Add(s, "oracle-l5r-3720", 0, Zone.Hand);
        var artisan = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1); // a different, unresolved battlefield
        artisan.GrantedKeywords = [new("Artisan", "test-fixture", "Permanent")];
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "improved-defenses-strength", PerformerId: artisan.Id));
        Check(e.State.Cards[artisan.Id].Bowed, "Bowed as cost, performer at another unresolved battlefield");
    }
    // ...but not at an already-resolved battlefield, or anywhere else that isn't Home/current/
    // unresolved.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.UnresolvedBattlefields = [0]; // province 1 already resolved
        var strat = Add(s, "oracle-l5r-3720", 0, Zone.Hand);
        var artisan = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1);
        artisan.GrantedKeywords = [new("Artisan", "test-fixture", "Permanent")];
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "improved-defenses-strength", PerformerId: artisan.Id));
    }
}
static void ExpendableFollowers()
{
    // Front Rank Troops / Fudo's Fiends: both carry only the Expendable keyword, no ability of their
    // own -- destroying one draws a card for its own controller, via the existing, previously-
    // untested ExpendableDraw helper.
    var s = StrongholdBattle("oracle-l5r-1218");
    var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
    var sentry = Add(s, "oracle-l5r-2304", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Elite Sentry
    var troops = Add(s, "oracle-l5r-10897", 1, Zone.Battlefield, province: 0); // Front Rank Troops
    var e = MatchEngine.FromTestState(s);
    var handBefore = e.State.Players[1].Hand.Count;
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sentry.Id, TargetId: troops.Id, AbilityId: "elite-sentry-destroy"));
    Equal(e.State.Cards[troops.Id].Zone, Zone.FateDiscard, "Destroyed, into its own Fate discard pile (a Follower, not a Personality)");
    Equal(e.State.Players[1].Hand.Count, handBefore + 1, "Expendable: its own controller drew a card");
}
static void FollowerWave11Primitives()
{
    {
        // Apprentice Shinobi: unconditional and universal -- a Ninja action resolving anywhere,
        // performed by the OPPONENT, unrelated to Shinobi's own controller or location, still gives
        // +1F. training-ninja-source carries the Ninja keyword plus the plain training-bow-any
        // ability; the classification is Keyword(source,"Ninja") alone (Goju House Guard's own
        // "Ninja Battle" precedent: the prefix itself is never retained in ability.Timing).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var shinobi = Add(s, "oracle-l5r-419", 0, Zone.Attached, attachedTo: host.Id);
        var ninjaSource = Add(s, "training-ninja-source", 1, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: ninjaSource.Id, TargetId: target.Id, AbilityId: "training-bow-any"));
        Check(e.State.Cards[shinobi.Id].Modifiers.Any(m => m.Effect == "apprentice-shinobi-ninja-bonus" && m.Stat == "Force" && m.Amount == 1),
            "Every in-play Apprentice Shinobi gets +1F after any Ninja action resolves, regardless of controller or location");
        EndTurnFully(e);
        Check(!e.State.Cards[shinobi.Id].Modifiers.Any(m => m.Effect == "apprentice-shinobi-ninja-bonus"),
            "Retained only until the end of the turn -- FinishTurn's own unconditional Modifiers.Clear() resets it");
    }
    {
        // Negative: a non-Ninja action, the exact same ability/target shape (training-bow-any-source
        // carries the identical training-bow-any ability but no Ninja keyword), gives no bonus at all.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var shinobi = Add(s, "oracle-l5r-419", 0, Zone.Attached, attachedTo: host.Id);
        var nonNinjaSource = Add(s, "training-bow-any-source", 1, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: nonNinjaSource.Id, TargetId: target.Id, AbilityId: "training-bow-any"));
        Check(!e.State.Cards[shinobi.Id].Modifiers.Any(m => m.Effect == "apprentice-shinobi-ninja-bonus"),
            "A non-Ninja action never gives Apprentice Shinobi its bonus");
    }
    {
        // Legion of the Bat: a real "action moved a card to a battlefield" event -- Mirumoto Michi
        // (Infantry, no Cavalry keyword) reinforcing an opposed battlefield -- opens Legion's own
        // reaction, which bows the enemy Personality that just arrived at Legion's own battlefield.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var michi = Add(s, "oracle-l5r-10054", 0, Zone.Home, bowed: true);
        var legionHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var legion = Add(s, "oracle-l5r-4700", 1, Zone.Attached, province: 0, attachedTo: legionHost.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: michi.Id, AbilityId: "michi-move"));
        Equal(e.State.Cards[michi.Id].Zone, Zone.Battlefield, "Michi moved to the battlefield");
        Equal(e.State.PendingChoice?.Type, "moveReaction");
        Equal(e.State.PendingChoice?.CardId, legion.Id);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: legion.Id, AbilityId: "legion-of-bat-bow"));
        Check(e.State.Cards[michi.Id].Bowed, "Legion of the Bat bows the enemy Infantry Personality that just moved to its battlefield");
        // The window now lists Legion's Reaction and its Pass like every other alternating window (issue #833), so it settles (issue #644): nothing is left to react with, it closes at once.
        Check(e.State.PendingChoice is null, "Nobody holds another Reaction: the reaction window closed without a Pass being asked");
    }
    {
        // Negative: RecruitReserve is explicitly excluded per the card's own rule ("Recruiting a
        // personality with the Reserve Keyword is not considered moving the personality") -- it
        // assigns Zone.Battlefield directly and never calls MoveToBattlefield, so Legion's own
        // reaction never opens even though an enemy Infantry Personality lands at its battlefield.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0;
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "training-reserve-personality"; s.Cards[reserveId].FaceUp = true;
        var legionHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var legion = Add(s, "oracle-l5r-4700", 1, Zone.Attached, province: 0, attachedTo: legionHost.Id);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: payment.SourceIds));
        Equal(e.State.Cards[reserveId].Zone, Zone.Battlefield, "Recruited directly to the battlefield");
        Check(e.State.PendingChoice is null, "RecruitReserve never opens Legion of the Bat's own move-reaction window");
    }
    {
        // Nikutai: another Follower on the same host bows (here, via Nubane's plain nubane-bow, "Target
        // an attachment: bow it") -- Nikutai, even though it is itself already bowed ("Even if this
        // card is bowed"), may straighten the sibling Follower that just bowed. Confirms "it" is the
        // Follower that bowed, not Nikutai itself.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var nikutai = Add(s, "oracle-l5r-9872", 0, Zone.Attached, attachedTo: host.Id, bowed: true);
        var sibling = Add(s, "training-follower", 0, Zone.Attached, attachedTo: host.Id);
        var nubane = Add(s, "oracle-l5r-4429", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nubane.Id, TargetId: sibling.Id, AbilityId: "nubane-bow"));
        Check(e.State.Cards[sibling.Id].Bowed, "nubane-bow bowed the sibling Follower");
        Equal(e.State.PendingChoice?.Type, "nikutaiReaction");
        Equal(e.State.PendingChoice?.CardId, nikutai.Id);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nikutai.Id, AbilityId: "nikutai-straighten"));
        Check(!e.State.Cards[sibling.Id].Bowed, "Nikutai straightens the sibling Follower that just bowed");
        Check(e.State.Cards[nikutai.Id].Bowed, "Nikutai itself stays bowed -- it straightens the OTHER Follower, not itself");
    }
    {
        // Negative: a Follower outside Nikutai's own unit (a different host) bowing does not open its
        // reaction at all.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "oracle-l5r-9872", 0, Zone.Attached, attachedTo: host.Id);
        var otherHost = Add(s, "training-lion-samurai", 0, Zone.Home);
        var otherFollower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: otherHost.Id);
        var nubane = Add(s, "oracle-l5r-4429", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nubane.Id, TargetId: otherFollower.Id, AbilityId: "nubane-bow"));
        Check(e.State.Cards[otherFollower.Id].Bowed);
        Check(e.State.PendingChoice is null, "A Follower outside Nikutai's own unit bowing does not open its reaction");
    }
    {
        // Nikutai's own Battle ability (issue #289): "Melee 5 Attack" -- previously missing from both
        // AttackStrength and AttackKind, so it silently resolved at strength 0 and could never
        // legally target any card with positive Force. Force 5 is exactly lethal.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var nikutai = Add(s, "oracle-l5r-9872", 0, Zone.Attached, attachedTo: host.Id, province: 0);
        var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nikutai.Id, TargetId: target.Id, AbilityId: "nikutai-attack"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Force-5 target destroyed by Melee 5 Attack (no reactor in play, so it resolves immediately)");
    }
    {
        // Negative: a Force-6 target is above Nikutai's Melee 5 and survives.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var nikutai = Add(s, "oracle-l5r-9872", 0, Zone.Attached, attachedTo: host.Id, province: 0);
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nikutai.Id, TargetId: target.Id, AbilityId: "nikutai-attack"));
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "Force-6 target is above the Attack's strength: a legal target that survives");
    }
    {
        // AttackKind must classify as "Melee", not the missing-case default "": Seasoned Ashigaru is
        // immune to Melee Attacks specifically, checked via AttackKind(id) != "Melee".
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var nikutai = Add(s, "oracle-l5r-9872", 0, Zone.Attached, attachedTo: host.Id, province: 0);
        var seasoned = Add(s, "oracle-l5r-10901", 1, Zone.Battlefield, province: 0); // Force 1, melee-immune
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: nikutai.Id, TargetId: seasoned.Id, AbilityId: "nikutai-attack"));
    }
    {
        // Wave Man: the Attack Phase ends because the last battlefield resolved -- straightens
        // automatically (a trait, not the ordinary Straighten Phase). Host is the winning ATTACKER
        // (Force 4 vs. the defender's 3), so a decisive win destroys the losing army outright
        // (ResolveBattle's own DestroyUnits) and FinishBattleReturnHomeAndTransition's own
        // ReturnHome(card,true) unconditionally BOWS every returning attacker first -- Wave Man's own
        // trait is what then straightens his unit right back, proven distinct from that mandatory bow.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var waveMan = Add(s, "oracle-l5r-9255", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[host.Id].Zone, Zone.Home, "Returned home (the winning side) after the last battlefield resolves");
        Equal(e.State.Phase, Phase.Dynasty);
        Check(!e.State.Cards[waveMan.Id].Bowed, "Wave Man straightens once the last battlefield resolves, even though attackers always return home bowed");
        Check(!e.State.Cards[host.Id].Bowed, "His whole unit straightens, including the host");
    }
    {
        // Wave Man: the Attack Phase also ends when a player declines to attack outright (the Declaration
        // Segment's pass) -- no battlefield is ever created, yet Wave Man still straightens.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.AttackDeclaration; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var waveMan = Add(s, "oracle-l5r-9255", 0, Zone.Attached, attachedTo: host.Id, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "pass", player: 0));
        Equal(e.State.Phase, Phase.Dynasty, "Declining ends the Attack Phase at once: no battlefields, no Maneuvers, no defender prompts");
        Check(!e.State.Cards[waveMan.Id].Bowed, "Wave Man straightens even though no battle ever happened");
    }
    {
        // Negative: already unbowed, and a live StraightenBlockedTurn on the host (simulating Hizumi/
        // Ruya's own "negate the next straightening") is left untouched -- proving the trait only ever
        // calls Straighten() on a card that IS bowed, never spuriously consuming an unrelated block.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        host.StraightenBlockedTurn = s.TurnNumber;
        Add(s, "oracle-l5r-9255", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Check(!e.State.Cards[host.Id].Bowed, "Already unbowed, stays unbowed");
        Equal(e.State.Cards[host.Id].StraightenBlockedTurn, e.State.TurnNumber, "Untouched -- Wave Man's trait never calls Straighten() on an already-unbowed card");
    }
}
static void ChooseStraightenDecline()
{
    var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
    var card = Add(s, "training-choose-straighten-samurai", 1, Zone.Home, bowed: true);
    var e2 = MatchEngine.FromTestState(s);
    SkipToDynasty(e2); Accept(e2, Cmd(e2, "endDynasty"));
    Equal(e2.State.Phase, Phase.Straighten); Check(e2.State.PendingChoice?.Type == "declineStraighten");
    Accept(e2, Cmd(e2, "declineStraighten", cards: [card.Id]));
    Check(e2.State.Cards[card.Id].Bowed, "Declined card should stay bowed"); Equal(e2.State.Phase, Phase.Action);
}
static void ChooseStraightenDefault()
{
    var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
    var card = Add(s, "training-choose-straighten-samurai", 1, Zone.Home, bowed: true);
    var e2 = MatchEngine.FromTestState(s);
    SkipToDynasty(e2); Accept(e2, Cmd(e2, "endDynasty"));
    Accept(e2, Cmd(e2, "declineStraighten", cards: []));
    Check(!e2.State.Cards[card.Id].Bowed, "Choosing to straighten everything should include the optional card");
}
static void ProvinceThreshold()
{
    var exact = Battle(8, 2, 6); Resolve(exact); Check(!exact.State.Players[1].Provinces[0].Destroyed);
    var over = Battle(9, 2, 6); Resolve(over); Check(over.State.Players[1].Provinces[0].Destroyed);
}
static void BattleTie()
{
    var e = Battle(3, 3); var a = e.State.Players[0].Honor; var d = e.State.Players[1].Honor; Resolve(e);
    Equal(e.State.Cards.Values.Count(c => c.Zone == Zone.Dead), 2); Equal(e.State.Players[0].Honor, a + 1); Equal(e.State.Players[1].Honor, d + 1);
}
static void FollowerForce()
{
    // Bowed Personality's OWN Force is suppressed, but its unbowed Follower still contributes
    // (issue #216 correctness fix): the attacker's own printed 5F is suppressed to 0, but the
    // Follower's unbowed 2F still counts, beating the defender's 1F.
    var (s, personality, enemy) = ForceBattleFixture(5, 1, 6, true);
    Add(s, "training-follower", 0, Zone.Attached, attachedTo: personality.Id, province: 0);
    var e = MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == personality.Id).UnitForce, 2, "Personality's own 5F suppressed while bowed; Follower's unbowed 2F still counts");
    Resolve(e);
    Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "The attacker's own side won, 2 vs 1");
    Equal(e.State.Cards[personality.Id].Zone, Zone.Home, "The attacker returns home after the battle, not destroyed");
}
static void EliteFollowerForce()
{
    // With an unbowed zero-Force Personality, only the bowed Elite Follower counts.
    var (s, personality, _) = ForceBattleFixture(0, 0, 1);
    Add(s, "training-follower", 0, Zone.Attached, bowed: true, attachedTo: personality.Id, province: 0);
    var elite = Add(s, "training-follower", 0, Zone.Attached, bowed: true, attachedTo: personality.Id, province: 0);
    elite.GrantedKeywords = [new("Elite", "test-fixture", "Permanent")];
    var e = MatchEngine.FromTestState(s); Resolve(e); AssertBattleForces(e, 2, 0);
    Check(e.State.Players[1].Provinces[0].Destroyed, "Bowed Elite Follower's Force should still count toward the attacking unit");
}
static void ElitePersonalityForce()
{
    // A bowed Personality normally contributes nothing to its unit's Force; Elite is the exception,
    // whether printed or granted, same as for a Follower.
    var (s, attacker, _) = ForceBattleFixture(9, 0, 1, true);
    attacker.GrantedKeywords = [new("Elite", "test-fixture", "Permanent")];
    var e = MatchEngine.FromTestState(s); Resolve(e); AssertBattleForces(e, 9, 0);
    Check(e.State.Players[1].Provinces[0].Destroyed, "A bowed Elite Personality's own Force should still count toward its unit");
}
static void BattleHonor()
{
    var s = Battle(7, 2).State; var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
    Add(s, "training-item", 1, Zone.Attached, attachedTo: defender.Id, province: 0);
    var e = MatchEngine.FromTestState(s); Resolve(e); Equal(e.State.Players[0].Honor, 12);
}
static void BattleReturn()
{
    var s = Battle(8, 2).State; s.UnresolvedBattlefields = [0, 1]; var attacker = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
    var follower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: attacker.Id, province: 0);
    var e = MatchEngine.FromTestState(s); Resolve(e);
    Equal(e.State.Cards[attacker.Id].Zone, Zone.Home); Check(e.State.Cards[attacker.Id].Bowed); Check(e.State.Cards[follower.Id].Bowed);
    Equal(e.State.Phase, Phase.BattleSelection);
    var ds = Battle(2, 8).State; ds.UnresolvedBattlefields = [0, 1]; var def = ds.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
    var de = MatchEngine.FromTestState(ds); Resolve(de); Equal(de.State.Cards[def.Id].Zone, Zone.Battlefield); Check(!de.State.Cards[def.Id].Bowed);
}
static void MilitaryVictory()
{
    var s = Battle(10, 2).State; foreach (var p in s.Players[1].Provinces.Skip(1)) { p.Destroyed = true; p.CardId = null; }
    var e = MatchEngine.FromTestState(s); Resolve(e); Equal(e.State.WinnerId, 0); Equal(e.State.Phase, Phase.Finished);
    Reject(e, Cmd(e, "pass"));
}
static void HonorVictory()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Players[0].Honor = 40;
    var e = MatchEngine.FromTestState(s); Accept(e, Cmd(e, "endDynasty")); Check(e.State.WinnerId is null);
    SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty")); Equal(e.State.WinnerId, 0);
}
static void DishonorTiming()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Players[0].DishonorLossPending = true; s.Players[0].Honor = 5;
    var count = s.Players[0].Hand.Count; var e = MatchEngine.FromTestState(s); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.WinnerId, 1); Equal(e.State.Players[0].Hand.Count, count);
}
static void HandLimit()
{
    var s = MatchEngine.CreateEconomyScenario().State;
    Add(s, "training-rally", 0, Zone.Hand); Add(s, "training-rally", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s); Accept(e, Cmd(e, "endDynasty")); Equal(e.State.Players[0].Hand.Count, 9);
    Equal(e.State.PendingChoice!.Type, "discardHand"); Reject(e, Cmd(e, "discardHand", cards: []));
    Accept(e, Cmd(e, "discardHand", cards: [e.State.Players[0].Hand[0]])); Equal(e.State.Players[0].Hand.Count, 8); Equal(e.State.ActivePlayerId, 1);
}
static void StrategyLegality()
{
    var s = Battle(5, 2).State; var target = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
    var card = Add(s, "training-pressure", 1, Zone.Hand); var e = MatchEngine.FromTestState(s);
    Reject(e, Cmd(e, "playStrategy", id: card.Id, target: target.Id));
    var outside = Add(s, "training-crane-samurai", 0, Zone.Home); var rally = Add(s, "training-rally", 1, Zone.Hand);
    e = MatchEngine.FromTestState(s); Reject(e, Cmd(e, "playStrategy", id: rally.Id, target: outside.Id));
    var own = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
    Accept(e, Cmd(e, "playStrategy", id: rally.Id, target: own.Id)); Equal(e.State.PriorityPlayerId, 0);
    Equal(e.State.Cards[rally.Id].Zone, Zone.FateDiscard);
}
static void TerrainPrimitive()
{
    // MatchEngine.State returns a fresh Copy() on every access -- every fixture card this scenario
    // needs must go into the state passed to FromTestState up front, not added onto e.State later.
    var s = Battle(5, 2).State;
    var attacker = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
    var terrain = Add(s, "training-terrain", 1, Zone.Hand);
    var second = Add(s, "training-terrain", 0, Zone.Hand);
    var destroyer = Add(s, "training-terrain-destroyer", 0, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    Reject(e, Cmd(e, "playStrategy", id: terrain.Id, target: attacker.Id)); // Terrain takes no target.
    Accept(e, Cmd(e, "playStrategy", id: terrain.Id));
    Equal(e.State.Cards[terrain.Id].Zone, Zone.Battlefield); Equal(e.State.Cards[terrain.Id].ProvinceIndex, 0); Equal(e.State.PriorityPlayerId, 0);
    // At most one Terrain per battlefield until the one in play is destroyed.
    Reject(e, Cmd(e, "playStrategy", id: second.Id));
    // The Terrain in play may be targeted by a Battle ability built for it.
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: terrain.Id, AbilityId: "training-destroy-terrain"));
    Equal(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard);
    // Once destroyed, a fresh Terrain may be played at the same battlefield again (after the
    // opponent's action opportunity, which using the ability handed to them, passes back).
    Accept(e, Cmd(e, "pass")); Equal(e.State.PriorityPlayerId, 0);
    Accept(e, Cmd(e, "playStrategy", id: second.Id));
    Equal(e.State.Cards[second.Id].Zone, Zone.Battlefield);
    // Terrain leaves automatically at battle resolution, its printed effects already applied --
    // never a separate destroy step, and never carried into a later battle at this province.
    Resolve(e);
    Equal(e.State.Cards[second.Id].Zone, Zone.FateDiscard); Check(e.State.Cards[second.Id].ProvinceIndex is null);
}
static void Equip()
{
    var s = MatchEngine.CreateStarterMatch().State; var p = Add(s, "training-crane-courtier", 0, Zone.Home); var item = Add(s, "training-item", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s); var payment = e.PaymentOptions(0, 2)[0];
    Accept(e, Cmd(e, "equip", id: item.Id, sources: payment.SourceIds, target: p.Id)); Equal(e.State.Cards[item.Id].Zone, Zone.Attached);
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == p.Id).Force, 4); Equal(e.State.PriorityPlayerId, 1);
    Reject(e, Cmd(e, "equip", id: item.Id, target: p.Id));
}
static void ExpiringForce()
{
    var s = MatchEngine.CreateEconomyScenario().State; var p = Add(s, "training-crane-samurai", 0, Zone.Home); p.Modifiers = [new("test-fixture", "test-fixture", "Force", 3)];
    var e = MatchEngine.FromTestState(s); Accept(e, Cmd(e, "endDynasty")); Equal(e.State.Cards[p.Id].Modifiers.Count, 0);
}
static void GrantedKeywordDuration()
{
    // Turn-duration grants clear at turn end; Permanent grants do not.
    var s = MatchEngine.CreateEconomyScenario().State; var p = Add(s, "training-crane-samurai", 0, Zone.Home);
    p.GrantedKeywords = [new("Elite", "test-fixture", "Turn"), new("Elite", "test-fixture", "Permanent")];
    var e = MatchEngine.FromTestState(s); Accept(e, Cmd(e, "endDynasty"));
    Equal(e.State.Cards[p.Id].GrantedKeywords.Count, 1); Equal(e.State.Cards[p.Id].GrantedKeywords[0].Duration, "Permanent");
}
static void EntersPlayDishonored()
{
    var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
    var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-dishonored-samurai";
    e = MatchEngine.FromTestState(s); var quote = e.QuotePurchase(0, cardId);
    Accept(e, Cmd(e, "purchase", id: cardId, sources: quote.Payments[0].SourceIds));
    Check(e.State.Cards[cardId].Dishonorable, "A Personality with Effect 'enters-dishonored' should enter play Dishonorable");
}
static void EntersPlayStraightenedByDefault()
{
    var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
    var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-crane-samurai";
    e = MatchEngine.FromTestState(s); var quote = e.QuotePurchase(0, cardId);
    Accept(e, Cmd(e, "purchase", id: cardId, sources: quote.Payments[0].SourceIds));
    Check(!e.State.Cards[cardId].Bowed, "A Personality without an enters-bowed effect should enter play straightened");
}
static void EntersPlayBowed()
{
    var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
    var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-enters-bowed-samurai";
    e = MatchEngine.FromTestState(s); var quote = e.QuotePurchase(0, cardId);
    var result = e.Execute(Cmd(e, "purchase", id: cardId, sources: quote.Payments[0].SourceIds));
    Check(result.Accepted, result.Error ?? "Rejected");
    Check(e.State.Cards[cardId].Bowed, "A Personality with Effect 'enters-bowed' should enter play bowed");
    var entered = result.Events.Single(ev => ev.Type == "cardEnteredPlay" && ev.CardId == cardId);
    Check(entered.Message.Contains("bowed"), "Entry should report bowed as part of the same transition");
    Check(!result.Events.Any(ev => ev.CardId == cardId && ev.Type is "straightened" or "unbowed"), "No separate straighten-then-bow event should exist for this card");
}
static void OrderSeppukuRequiresKeyword()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action;
    var veteran = Add(s, "training-crane-veteran", 0, Zone.Home); veteran.Dishonorable = true;
    var e = MatchEngine.FromTestState(s);
    Reject(e, Cmd(e, "orderSeppuku", id: veteran.Id));
}
static void OrderSeppukuRequiresDishonorable()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action;
    var samurai = Add(s, "training-dishonored-samurai", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    Reject(e, Cmd(e, "orderSeppuku", id: samurai.Id));
}
static void OrderSeppukuHonorableDeath()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action;
    var samurai = Add(s, "training-dishonored-samurai", 0, Zone.Home); samurai.Dishonorable = true;
    var e = MatchEngine.FromTestState(s); var honorBefore = e.State.Players[0].Honor;
    Accept(e, Cmd(e, "orderSeppuku", id: samurai.Id));
    Equal(e.State.Cards[samurai.Id].Zone, Zone.Dead);
    Check(!e.State.Cards[samurai.Id].Dishonorable, "Seppuku should leave the Personality honorable in death");
    Equal(e.State.Players[0].Honor, honorBefore);
}
static void OrderSeppukuIsOpenAction()
{
    // Issue #368 (Mendon): an Open action is taken as an ACTION at the player's own turn to act. It was available regardless of who held
    // priority; it may no longer interrupt the other player's opportunity.
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1;
    var samurai = Add(s, "training-dishonored-samurai", 0, Zone.Home); samurai.Dishonorable = true;
    var e = MatchEngine.FromTestState(s);
    Check(!e.QueryActions(0).Any(a => a.Type == "orderSeppuku"), "Not offered while the other player holds the action opportunity");
    Reject(e, Cmd(e, "orderSeppuku", player: 0, id: samurai.Id));
    Equal(e.State.Cards[samurai.Id].Zone, Zone.Home);
    // At their own turn to act it is accepted, and as an action it hands the opportunity to the other player.
    var t = MatchEngine.CreateEconomyScenario().State; t.Phase = Phase.Action; t.PriorityPlayerId = 0;
    var mine = Add(t, "training-dishonored-samurai", 0, Zone.Home); mine.Dishonorable = true;
    var f = MatchEngine.FromTestState(t);
    Accept(f, Cmd(f, "orderSeppuku", player: 0, id: mine.Id));
    Equal(f.State.Cards[mine.Id].Zone, Zone.Dead);
    Equal(f.State.PriorityPlayerId, 1);
}
static void CannotOrderSeppukuForOpponentsPersonality()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action;
    var samurai = Add(s, "training-dishonored-samurai", 0, Zone.Home); samurai.Dishonorable = true;
    var e = MatchEngine.FromTestState(s);
    Reject(e, Cmd(e, "orderSeppuku", player: 1, id: samurai.Id));
}
static void DishonorableDeathLosesHonor()
{
    var s = Battle(9, 2, 6).State; var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
    defender.Dishonorable = true; // training-lion-veteran: PersonalHonor 3
    var e = MatchEngine.FromTestState(s); Resolve(e);
    Equal(e.State.Players[1].Honor, 3);
}
static void Replay()
{
    var e = MatchEngine.CreateEconomyScenario(724); var q = e.QuotePurchase(0, ProvinceCard(e));
    Accept(e, Cmd(e, "purchase", id: q.CardId, sources: q.Payments[0].SourceIds)); Accept(e, Cmd(e, "proclaim")); CloseReaction(e);
    Accept(e, Cmd(e, "endDynasty")); SkipToDynasty(e);
    var replay = JsonSerializer.Deserialize<ReplayRecord>(Json(e.ExportReplay()))!;
    Equal(Json(MatchEngine.Replay(replay).State), Json(e.State));
    Equal(Json(e.State), Json(MatchEngine.Replay(e.ExportReplay()).State));
}
static void DifferentSeeds() => Check(Json(MatchEngine.CreateStarterMatch(1).State.Players[0].DynastyDeck) != Json(MatchEngine.CreateStarterMatch(2).State.Players[0].DynastyDeck));
static void Unsupported()
{
    var failed = false; try { CardCatalog.Get("Bayushi Nitoshi"); } catch (ArgumentException) { failed = true; }
    Check(failed);
    failed = false; try { MatchEngine.Replay(new("future", "starter", 1, [])); } catch (ArgumentException) { failed = true; }
    Check(failed);
    // Issue #611: card ids became a seeded permutation in 0.3.0, so a replay saved before that (card-NNNN in deck-list order) must be refused, not replayed onto different cards.
    failed = false; try { MatchEngine.Replay(MatchEngine.CreateStarterMatch(1).ExportReplay() with { ContentVersion = "emperor-lion-crane-0.2.0" }); } catch (ArgumentException) { failed = true; }
    Check(failed);
    Check(CardCatalog.Cards.Values.Where(c => c.Id.StartsWith("training-")).All(c => c.ScenarioOnly));
}
static void Detached()
{
    var e = MatchEngine.CreateStarterMatch(); var state = e.State; state.Players[0].Honor = 200; state.Cards.Clear();
    Equal(e.State.Players[0].Honor, 8); Check(e.State.Cards.Count > 0);
    var view = e.GetView(0); view.Players[0].Provinces[0] = new(0, 0, true, null, null); Check(!e.GetView(0).Players[0].Provinces[0].Destroyed);
}
static void EmptyDecks()
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Players[0].DynastyDeck.Clear(); s.Players[0].FateDeck.Clear();
    var e = MatchEngine.FromTestState(s); Accept(e, Cmd(e, "discardProvince", id: ProvinceCard(e))); Check(e.State.Players[0].Provinces[0].CardId is null);
    Check(!e.State.Players[0].Provinces[0].Destroyed); var hand = e.State.Players[0].Hand.Count; Accept(e, Cmd(e, "endDynasty")); Equal(e.State.Players[0].Hand.Count, hand);
}
static void ActionQueries(MatchEngine source) => OfferedCommandsValidate(source, () => MatchEngine.Replay(source.ExportReplay()));
static void AssignmentRollback()
{
    var s = MatchEngine.CreateStarterMatch().State; s.Phase = Phase.AttackDeclaration;
    var p = Add(s, "training-crane-samurai", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    Reject(e, Cmd(e, "attack", assignments: new() { [p.Id] = 0, ["nonexistent"] = 1 }));
    Equal(e.State.Cards[p.Id].Zone, Zone.Home);
}
static void EmptyBattle()
{
    var s = Battle(0, 0).State;
    var attacker = s.Cards.Values.Single(c => c.Zone == Zone.Battlefield && c.OwnerId == 0);
    var defender = s.Cards.Values.Single(c => c.Zone == Zone.Battlefield && c.OwnerId == 1);
    defender.Zone = Zone.Home; defender.ProvinceIndex = null;
    var e = MatchEngine.FromTestState(s); Resolve(e);
    Equal(e.State.Cards[attacker.Id].Zone, Zone.Home); Equal(e.State.Players[0].Honor, 8); Equal(e.State.Players[1].Honor, 6);
}
static void ProclaimReset()
{
    var e = MatchEngine.CreateEconomyScenario(); var id = ProvinceCard(e);
    Accept(e, Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds)); Accept(e, Cmd(e, "proclaim")); CloseReaction(e);
    Check(e.GetView(0).Players[0].ProclaimUsedThisTurn); Accept(e, Cmd(e, "endDynasty")); SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
    Check(!e.GetView(0).Players[0].ProclaimUsedThisTurn); SkipToDynasty(e); Check(e.QuotePurchase(0, ProvinceCard(e, 1)).ProclaimEligible);
}
static void CompleteMatch()
{
    var e = MatchEngine.CreateStarterMatch(831); var count = 0;
    while (e.State.WinnerId is null && count++ < 2000)
    {
        var s = e.State; var actions = e.QueryActions(s.PriorityPlayerId).Where(a => a.Enabled && a.Command is not null && a.Type != "concede").ToArray();
        ActionOption? chosen = null;
        if (s.PendingChoice?.Type == "proclaim") chosen = actions.FirstOrDefault(a => a.Type == "proclaim") ?? actions.First(a => a.Type == "declineReaction");
        else if (s.PendingChoice is not null) chosen = actions.First(a => a.Type == "discardHand");
        else if (s.Phase == Phase.Dynasty)
        {
            chosen = actions.FirstOrDefault(a => a.Type == "purchase" && CardCatalog.Get(s.Cards[a.CardId!].DefinitionId).Type == CardType.Holding && s.TurnNumber < 9)
                ?? actions.FirstOrDefault(a => a.Type == "purchase" && !a.Command!.ClanDiscount)
                ?? actions.FirstOrDefault(a => a.Type == "purchase")
                ?? actions.FirstOrDefault(a => a.Type == "discardProvince")
                ?? actions.First(a => a.Type == "endDynasty");
        }
        else if (s.Phase == Phase.DynastyDiscard) chosen = actions.FirstOrDefault(a => a.Type == "discardProvince") ?? actions.First(a => a.Type == "endDynasty");
        else if (s.Phase == Phase.AttackDeclaration && s.PriorityPlayerId == 0) chosen = actions.First(a => a.Type == "declareAttack");
        else if (s.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration && s.PriorityPlayerId == 0) chosen = actions.FirstOrDefault(a => a.Type == "attack") ?? actions.First(a => a.Type == "pass");
        else if (s.Phase == Phase.BattleSelection) chosen = actions.First(a => a.Type == "selectBattle");
        else chosen = actions.FirstOrDefault(a => a.Type == "pass") ?? actions.First();
        Accept(e, chosen.Command!);
        Check(e.State.Players.All(p => p.Honor >= 0));
        Check(e.State.Cards.Values.GroupBy(c => c.Id).All(g => g.Count() == 1));
    }
    Check(e.State.WinnerId is not null, $"Match did not finish in {count} commands.");
    Equal(Json(MatchEngine.Replay(e.ExportReplay()).State), Json(e.State));
}

static MatchState StrongholdBattle(string strongholdDefinitionId)
{
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Cards[s.Players[0].StrongholdId].DefinitionId = strongholdDefinitionId;
    s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
    return s;
}
static void JourneysEndMove()
{
    var s = StrongholdBattle("oracle-l5r-4059");
    var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
    var heavy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
    Add(s, "training-follower", 1, Zone.Attached, attachedTo: heavy.Id, province: 0);
    var light = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    var ability = new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: heavy.Id, AbilityId: "journeys-end-move", PerformerId: performer.Id);
    Reject(e, ability); // heavy has 1 Follower attached; the performer has 0 -- too many
    Accept(e, ability with { TargetId = light.Id });
    Equal(e.State.Cards[light.Id].Zone, Zone.Home, "A target with no more Followers than the performer should move home");
}
static void ShamateMove()
{
    {
        var s = StrongholdBattle("oracle-l5r-6691");
        var performer = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Personal Honor 3
        var proud = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0); // Personal Honor 4
        var honorable = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Personal Honor 3
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        var ability = new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: proud.Id, AbilityId: "shamate-move", PerformerId: performer.Id);
        Reject(e, ability); // proud has higher Personal Honor than the performer
        Accept(e, ability with { TargetId = honorable.Id });
        Equal(e.State.Cards[honorable.Id].Zone, Zone.Home);
        Equal(e.State.Players[0].Honor, before + 2, "Should gain 2 Honor for moving home a Personality this player does not control");
    }
    {
        var s = StrongholdBattle("oracle-l5r-6691");
        var performer = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Personal Honor 3
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Personal Honor 2, same side
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: own.Id, AbilityId: "shamate-move", PerformerId: performer.Id));
        Equal(e.State.Cards[own.Id].Zone, Zone.Home);
        Equal(e.State.Players[0].Honor, before, "No Honor gain when the performer's controller already controls the target");
    }
}
static void PlainsOfMaiden()
{
    var s = StrongholdBattle("oracle-l5r-5982");
    s.PriorityPlayerId = 1; s.Players[1].Provinces[0].Strength = 1;
    Add(s, "training-battle-maiden", 0, Zone.Battlefield, province: 0);
    Add(s, "training-battle-maiden", 0, Zone.Battlefield, province: 0);
    Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    var before = e.State.Players[0].Honor;
    Resolve(e);
    Equal(e.State.Players[0].Honor, before + 4, "2 Honor for the destroyed defender plus 2 for two surviving Battle Maidens");
}
static void LawOfDarknessDojo()
{
    {
        var s = StrongholdBattle("oracle-l5r-4676");
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var honorable = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: honorable.Id, AbilityId: "law-of-darkness-weaken"));
        Check(e.State.Cards[honorable.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -4), "Should apply -4 Force");
        Check(!e.State.Cards[honorable.Id].Bowed, "An honorable target should not be bowed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-4676");
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var dishonorable = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); dishonorable.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: dishonorable.Id, AbilityId: "law-of-darkness-weaken"));
        Check(e.State.Cards[dishonorable.Id].Bowed, "A dishonorable target should be bowed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-4676");
        var samurai = Add(s, "training-honor-strike-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: samurai.Id, TargetId: enemy.Id, AbilityId: "training-honor-strike"));
        Equal(e.State.Players[1].Honor, before - 3, "2 Honor from the training ability plus 1 more from Law of Darkness Dojo's passive");
    }
}
static void MakeDeadFromDiscard()
{
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var master = Add(s, "training-token-master", 0, Zone.Home);
        var discarded = Add(s, "training-dishonored-samurai", 0, Zone.DynastyDiscard); discarded.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: discarded.Id, AbilityId: "training-make-dead"));
        Equal(e.State.Cards[discarded.Id].Zone, Zone.Dead);
        Check(!e.State.Cards[discarded.Id].Dishonorable, "Becomes honorably dead: rehonored first");
        Equal(e.State.Players[0].Honor, honorBefore, "No Honor loss -- he was rehonored before becoming dead");
    }
    {
        // A Personality still in play (not yet discarded) is not a legal target.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var master = Add(s, "training-token-master", 0, Zone.Home);
        var inPlay = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: inPlay.Id, AbilityId: "training-make-dead"));
    }
}
static void HonorLossImmunity()
{
    {
        // Fate-card source (Clan is irrelevant here -- the Strategy is Unaligned): a Battle ability
        // that costs its own controller Honor to use is negated by the acting player's Stronghold.
        var s = StrongholdBattle("training-honor-immune-stronghold");
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-honor-cost-strategy", 0, Zone.Hand);
        var honorBefore = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strategy.Id, AbilityId: "training-honor-cost-attack"));
        Equal(e.State.Players[0].Honor, honorBefore, "Fate-card Honor cost negated by the immune Stronghold");
        Check(e.State.Events.Any(x => x.Type == "honorLossNegated"));
    }
    {
        // Without the immune Stronghold, the identical cost applies normally.
        var s = StrongholdBattle("oracle-l5r-4059");
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-honor-cost-strategy", 0, Zone.Hand);
        var honorBefore = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strategy.Id, AbilityId: "training-honor-cost-attack"));
        Equal(e.State.Players[0].Honor, honorBefore - 2);
    }
    {
        // Clan-matching source: a Spider Clan Personality's own "loses Honor after entering play"
        // Reaction is negated by the same Stronghold.
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "training-honor-immune-stronghold";
        Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-spider-honor-loss"; s.Cards[cardId].FaceUp = true;
        var honorBefore = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "training-honor-loss-entry"));
        Equal(e.State.Players[0].Honor, honorBefore, "Spider Clan Honor loss negated by the immune Stronghold");
    }
    {
        // A Dishonorable death from a card matching neither the immune Clan nor Fate is not negated,
        // even under the same Stronghold -- the check is scoped, not blanket.
        var s = Battle(9, 2).State;
        s.Cards[s.Players[1].StrongholdId].DefinitionId = "training-honor-immune-stronghold";
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        defender.Dishonorable = true;
        var honorBefore = s.Players[1].Honor;
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[defender.Id].Zone, Zone.Dead);
        Equal(e.State.Players[1].Honor, honorBefore - CardCatalog.Get("training-lion-veteran").PersonalHonor, "Lion Personality Honor loss is not negated by a Spider/Fate-scoped Stronghold");
    }
    {
        // 2026-10-13 ruling (official rulings on Fields of the Dead, The Fingers of Bone, The Shadowed Dojo): "You do not lose Honor from cards you own" does not prevent the Honor loss from the
        // death of a dishonorable Personality -- the loss is the rulebook's -- even when the dying Personality matches the stronghold's Clan.
        var s = Battle(9, 2).State;
        s.Cards[s.Players[1].StrongholdId].DefinitionId = "training-honor-immune-stronghold";
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        defender.DefinitionId = "training-spider-honor-loss";
        defender.Dishonorable = true;
        var honorBefore = s.Players[1].Honor;
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[defender.Id].Zone, Zone.Dead);
        Equal(e.State.Players[1].Honor, honorBefore - CardCatalog.Get("training-spider-honor-loss").PersonalHonor, "a dishonorable Spider Personality's death still costs Honor under a Spider/Fate-immune Stronghold");
    }
}
static void TokenPrimitives()
{
    {
        // Create: a token's stat bonus applies immediately and, unlike a Modifier, survives FinishTurn.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var master = Add(s, "training-token-master", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: master.Id, AbilityId: "training-create-token"));
        Equal(e.State.Cards[master.Id].Tokens.Count, 1); Equal(e.State.Cards[master.Id].Tokens[0].Type, "Training");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == master.Id).Force, 3, "Base Force 2 plus the token's +1F");
        Accept(e, Cmd(e, "pass")); SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Cards[master.Id].Tokens.Count, 1, "Tokens are not cleared at turn end, unlike Modifiers");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == master.Id).Force, 3);
    }
    {
        // Move: the token's stat bonus transfers with it, off the source and onto the destination.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var master = Add(s, "training-token-master", 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: master.Id, AbilityId: "training-create-token"));
        var tokenId = e.State.Cards[master.Id].Tokens[0].Id;
        Accept(e, Cmd(e, "pass"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: master.Id, PerformerId: other.Id, AbilityId: "training-move-token"));
        Equal(e.State.Cards[master.Id].Tokens.Count, 0);
        Equal(e.State.Cards[other.Id].Tokens.Count, 1); Equal(e.State.Cards[other.Id].Tokens[0].Id, tokenId);
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == master.Id).Force, 2, "Force returns to base once its token moves away");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == other.Id).Force, 4, "Base Force 3 plus the moved token's +1F");
    }
    {
        // Destroy: the token and its stat bonus are gone.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var master = Add(s, "training-token-master", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: master.Id, AbilityId: "training-create-token"));
        Accept(e, Cmd(e, "pass"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: master.Id, TargetId: master.Id, AbilityId: "training-destroy-token"));
        Equal(e.State.Cards[master.Id].Tokens.Count, 0);
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == master.Id).Force, 2);
    }
    {
        // Leaves play: a destroyed unit's own tokens, and its destroyed attachment's tokens, are
        // removed from the game entirely (core rules Glossary — Token).
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
        var attacker = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); attacker.Modifiers = [new("fixture", "fixture", "Force", 5)]; // Force 10
        var defender = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        defender.Tokens = [new("test-token", "Training", "test-fixture", "Force", 1)];
        var follower = Add(s, "training-follower", 1, Zone.Attached, attachedTo: defender.Id, province: 0); // +2F unbowed; total defense 8 < attack 10
        follower.Tokens = [new("test-token-2", "Training", "test-fixture", "Force", 1)];
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[defender.Id].Zone, Zone.Dead);
        Equal(e.State.Cards[defender.Id].Tokens.Count, 0, "A destroyed unit's tokens are removed from the game");
        Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard);
        Equal(e.State.Cards[follower.Id].Tokens.Count, 0, "A destroyed attachment's tokens are removed from the game too");
    }
}
static void MeiyuEntersPlayReaction()
{
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Players[0].Clan = "Dragon"; // in-clan with Meiyu, so Proclaim is also eligible this entry
    Add(s, "training-marketplace", 0, Zone.Home); Add(s, "training-marketplace", 0, Zone.Home); // enough Gold for her 5 cost
    var meiyuId = PublishedContent.PersonalityWave1.Cards.Single(c => c.Name == "Togashi Meiyu").Id;
    var cardId = s.Players[0].Provinces[0].CardId!;
    s.Cards[cardId].DefinitionId = meiyuId; s.Cards[cardId].FaceUp = true;
    var e = MatchEngine.FromTestState(s);
    var honorBefore = e.State.Players[0].Honor;
    var payment = e.PaymentOptions(0, 5).First();
    Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
    Equal(e.State.PendingChoice?.Type, "proclaim", "Both Proclaim and Meiyu's own reaction are eligible on this entry");
    // Meiyu's own reaction first; the window stays open afterward for Proclaim.
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "meiyu-fire-tokens"));
    Equal(e.State.Cards[cardId].Tokens.Count, 3);
    Check(e.State.Cards[cardId].Tokens.All(t => t.Type == "Fire" && t.Stat == "Force" && t.Amount == 1));
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == cardId).Force, 3, "Base Force 0 plus three +1F Fire tokens");
    Check(e.State.PendingChoice is not null, "The window stays open for Proclaim afterward");
    // Issue #644: using the reaction passed priority to the opponent, who holds nothing to react with and is not asked to pass: the opportunity is back with Meiyu's controller for Proclaim.
    Equal(e.State.PriorityPlayerId, 0, "the opponent has no reaction here, so priority returns to Proclaim's owner without a pass");
    Reject(e, new GameCommand("declineReaction", 1, e.Version));
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "meiyu-fire-tokens")); // once only
    // Proclaim still works afterward, in either order.
    Accept(e, new GameCommand("proclaim", 0, e.Version));
    Equal(e.State.Players[0].Honor, honorBefore + 2, "Meiyu's printed Personal Honor is 2");
    Check(e.State.PendingChoice is null, "Window closes at once: Meiyu's reaction and Proclaim are both spent (issue #639)");
}
static void MonasteryFireTokenAndAttack()
{
    {
        // Limited: only a Tattooed Personality is a legal target.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-10183";
        var tattooed = Add(s, "training-tattooed", 0, Zone.Home);
        var notTattooed = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: notTattooed.Id, AbilityId: "monastery-fire-token"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, TargetId: tattooed.Id, AbilityId: "monastery-fire-token"));
        Equal(e.State.Cards[tattooed.Id].Tokens.Count, 1); Equal(e.State.Cards[tattooed.Id].Tokens[0].Type, "Fire");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == tattooed.Id).Force, 3, "Base Force 2 plus the token's +1F");
    }
    {
        // Battle: destroying a Fire token on the performer is a required cost -- no Fire token, no legal action.
        var s = StrongholdBattle("oracle-l5r-10183");
        var performer = Add(s, "training-tattooed", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, AbilityId: "monastery-fire-attack"));
    }
    {
        // The performer may be bowed -- no "unbowed" qualifier is printed. Deferred targeting: legal
        // with no target in sight, but the Fire token is destroyed as a cost regardless.
        var s = StrongholdBattle("oracle-l5r-10183");
        var performer = Add(s, "training-tattooed", 0, Zone.Battlefield, province: 0, bowed: true);
        performer.Tokens.Add(new("fixture-token", "Fire", "test-fixture", "Force", 1));
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, above Ranged 5
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, AbilityId: "monastery-fire-attack"));
        Equal(e.State.Cards[performer.Id].Tokens.Count, 0, "The Fire token is destroyed as a cost regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "An out-of-range enemy is untouched");
    }
    {
        // A legal target (Force <= 5, no Followers) is destroyed.
        var s = StrongholdBattle("oracle-l5r-10183");
        var performer = Add(s, "training-tattooed", 0, Zone.Battlefield, province: 0);
        performer.Tokens.Add(new("fixture-token", "Fire", "test-fixture", "Force", 1));
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, no Followers
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, TargetId: enemy.Id, AbilityId: "monastery-fire-attack"));
        Equal(e.State.Cards[performer.Id].Tokens.Count, 0);
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
    }
}
static void TorazuFireTokensAndAttack()
{
    var torazuId = PublishedContent.PersonalityWave2.Cards.Single(c => c.Name == "Togashi Torazu").Id;
    {
        // Reaction: entering play grants three +1F Fire tokens, same mechanism as Meiyu's. The
        // buying Crane player is off-clan, so only Torazu's own reaction is offered, not Proclaim.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-marketplace", 0, Zone.Home); Add(s, "training-marketplace", 0, Zone.Home); // enough Gold for cost 6
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = torazuId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 6).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "torazu-fire-tokens"));
        Equal(e.State.Cards[cardId].Tokens.Count, 3);
        Check(e.State.Cards[cardId].Tokens.All(t => t.Type == "Fire" && t.Stat == "Force" && t.Amount == 1));
    }
    {
        // Battle: no separate Performer -- the shared "source Personality must be at this
        // battlefield" check already stands in for presence/location. No Fire token, no legal action.
        var s = StrongholdBattle("oracle-l5r-4059");
        var torazu = Add(s, torazuId, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: torazu.Id, AbilityId: "torazu-fire-attack"));
    }
    {
        // Deferred targeting: legal with no target in sight, but the Fire token is destroyed as a
        // cost regardless of whether the Attack hits.
        var s = StrongholdBattle("oracle-l5r-4059");
        var torazu = Add(s, torazuId, 0, Zone.Battlefield, province: 0);
        torazu.Tokens.Add(new("fixture-token", "Fire", "test-fixture", "Force", 1));
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, above Ranged 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: torazu.Id, AbilityId: "torazu-fire-attack"));
        Equal(e.State.Cards[torazu.Id].Tokens.Count, 0, "The Fire token is destroyed as a cost regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "An out-of-range enemy is untouched");
    }
    {
        // A legal target (Force <= 3, no Followers) is destroyed.
        var s = StrongholdBattle("oracle-l5r-4059");
        var torazu = Add(s, torazuId, 0, Zone.Battlefield, province: 0);
        torazu.Tokens.Add(new("fixture-token", "Fire", "test-fixture", "Force", 1));
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, no Followers
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: torazu.Id, TargetId: enemy.Id, AbilityId: "torazu-fire-attack"));
        Equal(e.State.Cards[torazu.Id].Tokens.Count, 0);
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
    }
}
static void AttackReactionWindow()
{
    {
        // A reaction is available (the fixture reactor, anywhere in play -- Reaction timing has no
        // Battle-presence/location requirement of its own): the window opens instead of resolving
        // immediately. Negating drives strength to 0; the cost already paid stays paid regardless.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var reactor = Add(s, "training-attack-reactor", 1, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, legal target
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.PendingChoice?.Type, "attackReaction", "A reaction is available, so the window opens instead of resolving immediately");
        Equal(e.State.PendingAttack?.Strength, 5);
        Check(e.State.Cards[castle.Id].Bowed, "The cost is paid immediately, before the window even opens");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: reactor.Id, AbilityId: "training-attack-negate"));
        Equal(e.State.PendingAttack?.Strength, 0);
        // Issue #644: the attacker holds no reaction and is not asked to pass; the reactor's other reaction is still his, so he alone passes and the window closes.
        Equal(e.State.PriorityPlayerId, 1);
        Reject(e, new GameCommand("declineReaction", 0, e.Version));
        Accept(e, new GameCommand("declineReaction", 1, e.Version));
        Check(e.State.PendingChoice is null && e.State.PendingAttack is null, "Window closed and the attack resolved");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "Negated: the target survives");
    }
    {
        // Boosting raises strength; a target already legal at announce time is still destroyed, and
        // the per-ability hit bonus (Carpenter Castle's +2 Honor) still fires on the deferred hit.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var reactor = Add(s, "training-attack-reactor", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: reactor.Id, AbilityId: "training-attack-boost"));
        Equal(e.State.PendingAttack?.Strength, 7, "5 base + 2 from the boost");
        Accept(e, new GameCommand("declineReaction", 0, e.Version)); // the defender holds nothing, so the attacker's one pass closes the window (issue #644)
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
        Equal(e.State.Players[0].Honor, honorBefore + 2, "Carpenter Castle's hit bonus still applies to a deferred resolution");
    }
}
// Issue #320: ExecuteAbilityEffects' tail (which discards a Strategy/Ring played from hand) is skipped whenever an
// Attack case returns early because ResolveOrDeferAttack opened the attackReaction window, and ResolveAttack never
// discards it either -- so the card stayed in Hand for good. The played card is now spent as the window opens.
static void HandPlayedAttackSpentWhenWindowOpens()
{
    // Strength of the Divine (Strategy): the published hand-played Attack card. (False Ring of Water used to be a second one; since issue #370 a False Ring has no
    // hand play -- its Ranged 3 Attack bows the Ring, which must be in play -- so no published Ring Attack is played from a hand any more.)
    // Each helper builds the same table with or without a player-1 Attack reactor in play, so the only variable is
    // whether the window opens.
    MatchEngine Divine(bool reactor, out CardInstance card, out CardInstance enemy)
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        card = Add(s, "oracle-l5r-10555", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, +2 from the card
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        enemy = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2, opposes the performer
        if (reactor) Add(s, "training-attack-reactor", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: card.Id, PerformerId: performer.Id, TargetId: enemy.Id, AbilityId: "strength-of-the-divine-force"));
        return e;
    }
    int Discards(MatchEngine e, CardInstance card) => e.State.Events.Count(x => x.Type == "fateDiscarded" && x.CardId == card.Id);
    void Spent(MatchEngine e, CardInstance card, string when)
    {
        Equal(e.State.Cards[card.Id].Zone, Zone.FateDiscard, $"The played card is in the discard pile {when}");
        Check(!e.State.Players[0].Hand.Contains(card.Id), $"The played card has left the hand {when}");
        Equal(Discards(e, card), 1, $"Discarded exactly once {when}");
    }
    foreach (var (name, play) in new (string, Func<bool, (MatchEngine, CardInstance, CardInstance)>)[]
    {
        ("Strength of the Divine",r=>{var e=Divine(r,out var c,out var en); return (e,c,en);}),
    })
    {
        {
            // Window opens: the card is spent at once (not left in hand while the opponent reacts), stays spent once the
            // window closes, and the Attack itself still resolves as before.
            var (e, card, enemy) = play(true);
            Equal(e.State.PendingChoice?.Type, "attackReaction", $"{name}: a reactor is available, so the window opens");
            Spent(e, card, $"{name}, while the window is open");
            Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds no reaction, so this one pass closes the window (issue #644)
            Check(e.State.PendingChoice is null && e.State.PendingAttack is null, $"{name}: window closed");
            Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, $"{name}: the Attack itself still resolves");
            Spent(e, card, $"{name}, after the window closes");
        }
        {
            // A negating Reaction only zeroes the Attack's strength; the played card is spent regardless.
            var (e, card, enemy) = play(true);
            var reactor = e.State.Cards.Values.Single(c => c.DefinitionId == "training-attack-reactor");
            Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: reactor.Id, AbilityId: "training-attack-negate"));
            Equal(e.State.PendingAttack?.Strength, 0, $"{name}: negated");
            Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds no reaction, so the reactor's owner alone passes (issue #644)
            Equal(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, $"{name}: the negated Attack does not destroy its target");
            Spent(e, card, $"{name}, after a negated Attack");
        }
        {
            // No window (nothing can react): the unchanged immediate path -- resolved and discarded inline.
            var (e, card, enemy) = play(false);
            Check(e.State.PendingChoice is null && e.State.PendingAttack is null, $"{name}: no reactor, so no window");
            Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, $"{name}: resolved immediately");
            Spent(e, card, $"{name}, on the immediate path");
        }
    }
    // Additional Action bookkeeping must be identical whether or not the window opens: an Attack taken as a consumed
    // grant keeps priority while other grants remain, and an ordinary Attack forfeits the grants it did not consume.
    foreach (var (label, grants, expectedPriority, expectedGrants) in new (string, AdditionalActionGrant[], int, int)[]
    {
        ("consumes one of two grants",[new(0,null),new(0,null)],0,1),
        ("ordinary action with an unrelated pre-existing grant",[new(0,["not-this-card"])],1,0),
    })
    {
        foreach (var reactor in new[] { false, true })
        {
            var s = StrongholdBattle("oracle-l5r-1218");
            var card = Add(s, "oracle-l5r-10555", 0, Zone.Hand);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
            var enemy = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
            if (reactor) Add(s, "training-attack-reactor", 1, Zone.Home);
            s.AdditionalActions.AddRange(grants);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: card.Id, PerformerId: performer.Id, TargetId: enemy.Id, AbilityId: "strength-of-the-divine-force"));
            Equal(e.State.PendingChoice?.Type == "attackReaction", reactor, $"{label}: window state (reactor={reactor})");
            if (reactor) Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds no reaction: this one pass closes the window (issue #644)
            Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, $"{label}: resolved (reactor={reactor})");
            Equal(e.State.PriorityPlayerId, expectedPriority, $"{label}: priority (reactor={reactor})");
            Equal(e.State.AdditionalActions.Count(g => g.PlayerId == 0), expectedGrants, $"{label}: remaining grants (reactor={reactor})");
            Spent(e, card, $"{label} (reactor={reactor})");
        }
    }
}
static void UnconditionalAttackImportWave()
{
    // Shinjo Eun-Sahng (Ranged 4), Shosuro Konishi (Ranged 5), Tsuruchi Kosoko (Ranged 5) and Stalking
    // Tiger (Melee 6): each card is its own performer with no cost and no condition -- proves the
    // shared Attack primitive needs nothing beyond a catalog entry plus a UseAbility case per card.
    foreach (var (cardId, abilityId, strength) in new[]
    {
        ("oracle-l5r-6872","shinjo-eun-sahng-attack",4),
        ("oracle-l5r-7121","shosuro-konishi-attack",5),
        ("oracle-l5r-8852","tsuruchi-kosoko-attack",5),
        ("oracle-l5r-7450","stalking-tiger-attack",6),
    })
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        // Stalking Tiger is a Follower: it needs a friendly Personality unit to attach to before it
        // (or anything else) counts as "a unit at the current battlefield" (Army() is Personality-only).
        var isFollower = CardCatalog.Get(cardId).Type == CardType.Follower;
        var host = isFollower ? Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0) : null;
        var performer = Add(s, cardId, 0, isFollower ? Zone.Attached : Zone.Battlefield, province: 0, attachedTo: host?.Id);
        var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        if (strength < 5) AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: performer.Id, TargetId: target.Id, AbilityId: abilityId), target.Id);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: performer.Id, TargetId: strength >= 5 ? target.Id : null, AbilityId: abilityId));
        Equal(e.State.Cards[target.Id].Zone, strength >= 5 ? Zone.Dead : Zone.Battlefield, $"{cardId}: hit expectation at strength {strength} vs Force 5");
    }
}
static void TsuruchiIsasAttackAndMoveHome()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var isas = Add(s, "oracle-l5r-8840", 0, Zone.Battlefield, province: 0);
    var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: isas.Id, TargetId: target.Id, AbilityId: "tsuruchi-isas-attack"));
    Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Ranged 3 hits a Force-3 target");
    Check(e.State.Cards[isas.Id].Bowed, "Bowing Isas is the cost, paid up front");
    Equal(e.State.Cards[isas.Id].Zone, Zone.Home, "Isas moves home after the Attack resolves");
    Equal(e.State.Cards[isas.Id].ProvinceIndex, null, "Home is not associated with a province");
}
static void BayushiKahokuAttack()
{
    // No enemy Dishonorable Personality yet: no passive Force bonus, base strength 3 can't reach a
    // Force-5 target.
    var s = StrongholdBattle("oracle-l5r-1218");
    var kahoku = Add(s, "oracle-l5r-806", 0, Zone.Battlefield, province: 0);
    var honorable = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5, not dishonorable
    var e = MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == kahoku.Id).Force, 3, "No enemy is dishonorable: base Force 3");
    AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: kahoku.Id, TargetId: honorable.Id, AbilityId: "bayushi-kahoku-attack"), honorable.Id);

    // A dishonorable enemy Personality both raises Kahoku's own Force (+2, always-on) and the Attack's
    // strength against that specific target (+2, target-dependent) -- two separate conditionals that
    // happen to share the same Dishonorable flag.
    var s2 = StrongholdBattle("oracle-l5r-1218");
    var kahoku2 = Add(s2, "oracle-l5r-806", 0, Zone.Battlefield, province: 0);
    var dishonorable = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0); dishonorable.Dishonorable = true; // Force 5
    var e2 = MatchEngine.FromTestState(s2);
    Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == kahoku2.Id).Force, 5, "An enemy dishonorable Personality raises Kahoku's own Force by 2");
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: kahoku2.Id, TargetId: dishonorable.Id, AbilityId: "bayushi-kahoku-attack"));
    Equal(e2.State.Cards[dishonorable.Id].Zone, Zone.Dead, "Strength 3+2=5 destroys the dishonorable Force-5 target");
}
// =====================================================================================================
// Bayushi Kahoku - exp (oracle-l5r-807) and the two generalized primitives built for him:
//   P1 -- the Attack-vs-Followers exception (AttackFollowerException, AttackTargeting.cs), and
//   P2 -- the generalized target-status predicate (TargetStatus, TargetStatus.cs).
// =====================================================================================================
static MatchState FollowerAttackTable()
{
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    return s;
}
static CardInstance WithFollower(MatchState s, CardInstance host) => Add(s, "training-follower", host.OwnerId, Zone.Attached, province: host.ProvinceIndex, attachedTo: host.Id); // Force 2
static GameCommand UseAtk(MatchEngine e, string ability, string card, string? target = null, string[]? cardIds = null, string? performer = null, int player = 0)
    => new("useAbility", player, e.Version, CardId: card, TargetId: target, AbilityId: ability, CardIds: cardIds, PerformerId: performer);
// A followed enemy Personality: the Personality (its own Force is Force(def)+extra) plus one Force-2 Follower.
static (CardInstance Host, CardInstance Follower) Followed(MatchState s, string def = "training-crane-samurai", bool dishonorable = false)
{
    var host = Add(s, def, 1, Zone.Battlefield, province: 0); host.Dishonorable = dishonorable;
    return (host, WithFollower(s, host));
}

static void AttackFollowersDefaultRuleBothKinds()
{
    // The base rule, unchanged: a Personality with an attached FOLLOWER is untargetable by a Ranged AND by a Melee Attack;
    // an Item or Spell attachment never blocks it; the Follower itself and a bare Personality are legal targets.
    foreach (var (attackerDef, ability, kind) in new[] { ("oracle-l5r-592", "asako-ume-attack", "Ranged"), ("oracle-l5r-10290", "chiyo-attack", "Melee") })
    {
        var s = FollowerAttackTable();
        var attacker = Add(s, attackerDef, 0, Zone.Battlefield, province: 0);
        var (host, follower) = Followed(s); // Force 3 -- within the strength-4 Attack, so ONLY the Follower blocks it
        var plain = Add(s, "training-crane-courtier", 1, Zone.Battlefield, province: 0); // Force 2
        var armored = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: armored.Id); // Force 2 + Item 2 = 4
        var spelled = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-spell", 1, Zone.Attached, province: 0, attachedTo: spelled.Id); // Force 3
        var e = MatchEngine.FromTestState(s);
        var refused = e.Execute(UseAtk(e, ability, attacker.Id, host.Id));
        Check(!refused.Accepted && refused.Error!.Contains("without an attached Follower"), $"{kind}: a followed Personality is immune ({refused.Error})");
        Reject(e, UseAtk(e, ability, attacker.Id, host.Id));
        foreach (var legal in new[] { follower.Id, plain.Id, armored.Id, spelled.Id })
        {
            var fresh = MatchEngine.FromTestState(s);
            Accept(fresh, UseAtk(fresh, ability, attacker.Id, legal));
        }
        var hit = MatchEngine.FromTestState(s);
        Accept(hit, UseAtk(hit, ability, attacker.Id, follower.Id));
        Equal(hit.State.Cards[follower.Id].Zone, Zone.FateDiscard, $"{kind}: the lone Follower is destroyed");
        Equal(hit.State.Cards[host.Id].Zone, Zone.Battlefield, $"{kind}: its Personality is untouched");
        // Item + Follower together: the Follower alone is what blocks.
        var s2 = FollowerAttackTable();
        var attacker2 = Add(s2, attackerDef, 0, Zone.Battlefield, province: 0);
        var (both, _) = Followed(s2, "training-crane-courtier"); Add(s2, "training-item", 1, Zone.Attached, province: 0, attachedTo: both.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, UseAtk(e2, ability, attacker2.Id, both.Id));
    }
}

static void AttackFollowersExceptionShapes()
{
    // ---- (a) printed on the ability, unconditional: Tsuruchi Kosoko - exp, "Ranged 6 Attack that may target a Personality with Followers".
    {
        var s = FollowerAttackTable();
        var kosoko = Add(s, "oracle-l5r-9831", 0, Zone.Battlefield, province: 0);
        var (host, follower) = Followed(s); // Force 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "tsuruchi-kosoko-exp-attack", kosoko.Id, host.Id));
        Equal(e.State.Cards[host.Id].Zone, Zone.Dead, "The followed Personality is destroyed");
        // Rules: a Personality leaving play takes his whole unit with him -- the attached Follower is destroyed too.
        Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "His attached Follower is destroyed with the unit");
    }
    {
        // The non-experienced Kosoko has no such exception -- the exception is the printed text of THAT ability, nobody else's.
        var s = FollowerAttackTable();
        var plainKosoko = Add(s, "oracle-l5r-8852", 0, Zone.Battlefield, province: 0);
        var (host, _) = Followed(s);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseAtk(e, "tsuruchi-kosoko-attack", plainKosoko.Id, host.Id));
    }
    {
        // DECISION POINT (AttackComparedForce; open rules question -- see the decision note): the followed Personality's OWN Force is
        // compared to the strength, never the unit's total. Force 5 + a Force-2 Follower = unit 7; strength 6 destroys him. If a ruling makes
        // the unit total the comparison, THIS assertion flips (and AttackComparedForce is the one line to change).
        var s = FollowerAttackTable();
        var kosoko = Add(s, "oracle-l5r-9831", 0, Zone.Battlefield, province: 0);
        var (host, _) = Followed(s, "training-crane-veteran"); // Force 5, unit total 7
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "tsuruchi-kosoko-exp-attack", kosoko.Id, host.Id));
        Equal(e.State.Cards[host.Id].Zone, Zone.Dead, "Personality Force 5 <= strength 6, whatever the unit total");
        // The exception lifts the Follower immunity only -- never the strength check: an Item takes his own Force to 7 > 6.
        var s2 = FollowerAttackTable();
        var kosoko2 = Add(s2, "oracle-l5r-9831", 0, Zone.Battlefield, province: 0);
        var (strong, _) = Followed(s2, "training-crane-veteran"); Add(s2, "training-item", 1, Zone.Attached, province: 0, attachedTo: strong.Id);
        var e2 = MatchEngine.FromTestState(s2);
        AcceptSurvive(e2, UseAtk(e2, "tsuruchi-kosoko-exp-attack", kosoko2.Id, strong.Id), strong.Id); // legal target, but the strength check leaves it alive
    }

    // ---- The exception evaluator over every kind/condition/target-status combination (shapes (b), (c) and (e)), through granted
    // exceptions: printed-on-ability, static-trait and granted exceptions all funnel through the same AttackFollowerExceptionApplies.
    MatchEngine Table(AttackFollowerException? grant, bool reconAtBattlefield = false, bool reconAtOtherProvince = false, bool reconEnemy = false)
    {
        var s = FollowerAttackTable();
        Add(s, "oracle-l5r-592", 0, Zone.Battlefield, province: 0); // Ranged performer
        Followed(s, "training-crane-samurai", dishonorable: false); // test-1.. honorable followed
        Followed(s, "training-lion-samurai", dishonorable: true);   // dishonorable followed
        if (grant is not null) s.AttackTargetGrants.Add(new(0, grant));
        if (reconAtBattlefield) s.Players[1].Provinces[0].Reconnaissance[0] = true;
        if (reconAtOtherProvince) s.Players[1].Provinces[1].Reconnaissance[0] = true;
        if (reconEnemy) s.Players[1].Provinces[0].Reconnaissance[1] = true;
        return MatchEngine.FromTestState(s);
    }
    bool May(MatchEngine e, string ability, bool dishonorableTarget, int player = 0)
    {
        var target = e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && c.Dishonorable == dishonorableTarget);
        return e.AttackMayTargetFollowedPersonality(ability, player, e.State.Cards.Values.First(c => c.DefinitionId == "oracle-l5r-592"), target);
    }
    Check(!May(Table(null), "asako-ume-attack", false) && !May(Table(null), "asako-ume-attack", true) && !May(Table(null), "chiyo-attack", true), "No exception anywhere: nothing may target a followed Personality");
    foreach (var (kind, ranged, melee) in new[] { ("Ranged", true, false), ("Melee", false, true), ("Both", true, true) })
    {
        var e = Table(new(kind));
        Equal(May(e, "asako-ume-attack", false), ranged, $"{kind} exception, a Ranged Attack");
        Equal(May(e, "chiyo-attack", false), melee, $"{kind} exception, a Melee Attack");
        Check(!May(e, "asako-ume-attack", false, player: 1) && !May(e, "chiyo-attack", false, player: 1), $"{kind}: a grant belongs to its own player only");
        Check(!May(e, "training-item-not-an-attack", false), "An ability that is no Attack is never covered");
    }
    // (b) Reconnaissance at the CURRENT battlefield, held by the acting player.
    {
        var recon = new AttackFollowerException("Ranged", Condition: AttackFollowerException.Reconnaissance);
        Check(!May(Table(recon), "asako-ume-attack", false), "No Reconnaissance: no exception");
        Check(May(Table(recon, reconAtBattlefield: true), "asako-ume-attack", false), "Reconnaissance at the current battlefield: the exception applies");
        Check(!May(Table(recon, reconAtOtherProvince: true), "asako-ume-attack", false), "Reconnaissance at a DIFFERENT province does not count");
        Check(!May(Table(recon, reconEnemy: true), "asako-ume-attack", false), "The OTHER player's Reconnaissance does not count");
        Check(!May(Table(recon, reconAtBattlefield: true), "chiyo-attack", false), "Kind still scopes a conditional exception");
    }
    // (c) conditional on the TARGET's status (P2): dishonorable only.
    {
        var dishonorableOnly = new AttackFollowerException("Ranged", TargetIs: TargetStatus.IsDishonorable);
        Check(!May(Table(dishonorableOnly), "asako-ume-attack", false), "An honorable followed Personality stays untargetable");
        Check(May(Table(dishonorableOnly), "asako-ume-attack", true), "A dishonorable followed Personality may be targeted");
        // Combined conditions must ALL hold.
        var both = new AttackFollowerException("Ranged", Condition: AttackFollowerException.Reconnaissance, TargetIs: TargetStatus.IsDishonorable);
        Check(!May(Table(both), "asako-ume-attack", true) && !May(Table(both, reconAtBattlefield: true), "asako-ume-attack", false) && May(Table(both, reconAtBattlefield: true), "asako-ume-attack", true));
    }
    // A misconfigured condition fails loudly rather than silently reading as "never".
    {
        var threw = false;
        try { May(Table(new("Ranged", Condition: "no-such-condition")), "asako-ume-attack", false); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "An unknown exception condition throws");
    }

    // ---- End to end through real commands, BOTH kinds: a granted Melee exception makes a Melee Attack (and only it) legal...
    foreach (var (kind, ranged, melee) in new[] { ("Ranged", true, false), ("Melee", false, true) })
    {
        var s = FollowerAttackTable();
        var rangedAttacker = Add(s, "oracle-l5r-592", 0, Zone.Battlefield, province: 0);  // Ranged 4
        var meleeAttacker = Add(s, "oracle-l5r-10290", 0, Zone.Battlefield, province: 0); // Melee 4
        var (host, follower) = Followed(s);
        s.AttackTargetGrants.Add(new(0, new AttackFollowerException(kind)));
        var e = MatchEngine.FromTestState(s);
        var e2 = MatchEngine.FromTestState(s);
        if (ranged) Accept(e, UseAtk(e, "asako-ume-attack", rangedAttacker.Id, host.Id)); else Reject(e, UseAtk(e, "asako-ume-attack", rangedAttacker.Id, host.Id));
        if (melee) Accept(e2, UseAtk(e2, "chiyo-attack", meleeAttacker.Id, host.Id)); else Reject(e2, UseAtk(e2, "chiyo-attack", meleeAttacker.Id, host.Id));
        var hit = ranged ? e : e2;
        Equal(hit.State.Cards[host.Id].Zone, Zone.Dead, $"{kind} exception: the followed Personality is destroyed");
        Equal(hit.State.Cards[follower.Id].Zone, Zone.FateDiscard, $"{kind} exception: his Follower goes with him");
        Check(hit.State.AttackTargetGrants.Count == 0, $"{kind} exception: the grant is spent by the Attack it covered");
    }
}


static void AttackFollowersPrintedConditionalShapes()
{
    // The two printed shapes no published card exercises yet, end to end through real commands, via the training fixture that carries one of
    // each: (b) a RANGED Attack printed with "if you have Reconnaissance", (c) a MELEE Attack printed with "dishonorable Personalities only".
    foreach (var (recon, reconElsewhere, legal) in new[] { (false, false, false), (true, false, true), (false, true, false) })
    {
        var s = FollowerAttackTable();
        var attacker = Add(s, "training-followed-attacker", 0, Zone.Battlefield, province: 0);
        var (host, follower) = Followed(s); // Force 3 <= strength 3, so only the Follower could ever block it
        if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
        if (reconElsewhere) s.Players[1].Provinces[1].Reconnaissance[0] = true;
        var e = MatchEngine.FromTestState(s);
        var command = UseAtk(e, "training-followed-recon-attack", attacker.Id, host.Id);
        if (legal)
        {
            Accept(e, command);
            Equal(e.State.Cards[host.Id].Zone, Zone.Dead, "Reconnaissance at the current battlefield: the followed Personality may be Ranged-attacked");
            Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard);
        }
        else Reject(e, command);
    }
    foreach (var (dishonorable, legal) in new[] { (false, false), (true, true) })
    {
        var s = FollowerAttackTable();
        var attacker = Add(s, "training-followed-attacker", 0, Zone.Battlefield, province: 0);
        var (host, follower) = Followed(s, "training-crane-samurai", dishonorable);
        var e = MatchEngine.FromTestState(s);
        var command = UseAtk(e, "training-followed-dishonorable-melee-attack", attacker.Id, host.Id);
        if (legal)
        {
            Accept(e, command);
            Equal(e.State.Cards[host.Id].Zone, Zone.Dead, "A dishonorable followed Personality may be Melee-attacked");
            Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard);
        }
        else Reject(e, command);
    }
    {
        // Each printed exception belongs to its own ability and kind: Reconnaissance does not unlock the Melee Attack against an honorable
        // followed Personality, and a dishonorable target does not unlock the Ranged one without Reconnaissance.
        var s = FollowerAttackTable();
        var attacker = Add(s, "training-followed-attacker", 0, Zone.Battlefield, province: 0);
        var (honorable, _) = Followed(s); s.Players[1].Provinces[0].Reconnaissance[0] = true;
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseAtk(e, "training-followed-dishonorable-melee-attack", attacker.Id, honorable.Id));
        var s2 = FollowerAttackTable();
        var attacker2 = Add(s2, "training-followed-attacker", 0, Zone.Battlefield, province: 0);
        var (dishonorable, _) = Followed(s2, "training-crane-samurai", dishonorable: true);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, UseAtk(e2, "training-followed-recon-attack", attacker2.Id, dishonorable.Id));
    }
}

static void BayushiKahokuExpTrait()
{
    // (d) + (c): "Ranged Attacks from actions performed by Kahoku may target dishonorable Personalities with attached Followers."
    // Kahoku's own action: "Target an enemy Personality: Give him -4F. Ranged 3 Attack." -- TargetId is the -4F target, CardIds the Attack's own.
    var definition = CardCatalog.Get("oracle-l5r-807");
    Equal(definition.Effect, "ranged-attacks-may-target-dishonorable-with-followers"); Equal(definition.Force, 5); Equal(definition.Chi, 4); Equal(definition.GoldCost, 9);
    Check(definition.Abilities!.SequenceEqual(new[] { "kahoku-exp-attack" }));
    // Kahoku (player 0) against: H = honorable followed (Force 3), D4 = dishonorable followed (Force 4), D3 = dishonorable followed (Force 3).
    (MatchState S, CardInstance Kahoku, CardInstance H, CardInstance HFollower, CardInstance D4, CardInstance D3) Table()
    {
        var s = FollowerAttackTable();
        var kahoku = Add(s, "oracle-l5r-807", 0, Zone.Battlefield, province: 0);
        var (h, hf) = Followed(s, "training-crane-samurai", dishonorable: false);
        var (d4, _) = Followed(s, "training-lion-samurai", dishonorable: true);
        var (d3, _) = Followed(s, "training-crane-samurai", dishonorable: true);
        return (s, kahoku, h, hf, d4, d3);
    }
    {
        // A dishonorable followed Personality: the -4F (Force 4 -> 0) then the Ranged 3 Attack destroys him, Follower and all.
        var (s, kahoku, _, _, d4, _) = Table();
        var honorBefore = s.Players[1].Honor;
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, d4.Id, [d4.Id]));
        Equal(e.State.Cards[d4.Id].Zone, Zone.Dead, "The dishonorable followed Personality is destroyed by the weakened-then-attacked shot");
        Equal(e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Zone == Zone.FateDiscard && c.DefinitionId == "training-follower"), 1, "...and exactly his own Follower went with him");
        Equal(e.State.Players[1].Honor, honorBefore - CardCatalog.Get("training-lion-samurai").PersonalHonor, "Dishonorable death: his controller loses his base Personal Honor");
    }
    {
        // An HONORABLE followed Personality is still immune -- the exception is target-status-gated (P2).
        var (s, kahoku, h, _, _, _) = Table();
        var e = MatchEngine.FromTestState(s);
        var refused = e.Execute(UseAtk(e, "kahoku-exp-attack", kahoku.Id, h.Id, [h.Id]));
        Check(!refused.Accepted && refused.Error!.Contains("without an attached Follower"), $"An honorable followed Personality is immune ({refused.Error})");
        Reject(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, h.Id, [h.Id]));
        // Status is read live: the very same Personality, rehonored, is immune again.
        var (s2, kahoku2, _, _, _, d3b) = Table(); d3b.Dishonorable = false;
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, UseAtk(e2, "kahoku-exp-attack", kahoku2.Id, d3b.Id, [d3b.Id]));
    }
    {
        // Two SEPARATE selections: -4F on one Personality (H), the Attack on another (D3, Force 3 <= 3).
        var (s, kahoku, h, _, _, d3) = Table();
        var e = MatchEngine.FromTestState(s);
        var kahokuForce = e.GetView(0).Players[0].Army.Single(c => c.Id == kahoku.Id).Force;
        Accept(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, h.Id, [d3.Id]));
        Check(e.State.Cards[h.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -4), "The -4F went to the TargetId Personality");
        // Mendon's ruling: "Bayushi Kahoku's force is not modified by this action."
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == kahoku.Id).Force, kahokuForce, "Kahoku's own Force is unchanged by his action");
        Check(!e.State.Cards[kahoku.Id].Modifiers.Any(m => m.Stat == "Force"), "No Force modifier lands on Kahoku himself");
        Equal(e.State.Cards[h.Id].Zone, Zone.Battlefield, "...who was not attacked");
        Equal(e.State.Cards[d3.Id].Zone, Zone.Dead, "The Attack (CardIds) destroyed the other, dishonorable, Personality");
        // The exception lifts the Follower immunity only, never the strength check: D4 (Force 4 > 3) is not made a legal target by the -4F on H.
        var (s2, kahoku2, h2, _, d4, _) = Table();
        var e2 = MatchEngine.FromTestState(s2);
        AcceptSurvive(e2, UseAtk(e2, "kahoku-exp-attack", kahoku2.Id, h2.Id, [d4.Id]), d4.Id); // still a legal target, but survives
        // ...but weakening D4 itself does bring him within reach (the strength check sees the -4F the same action applies).
        var e3 = MatchEngine.FromTestState(s2);
        Accept(e3, UseAtk(e3, "kahoku-exp-attack", kahoku2.Id, d4.Id, [d4.Id]));
    }
    {
        // Strength boundary with an Item AND a Follower attached: Force 5 + Item 2 = 7 -> 3 after the -4F (destroyed); a second Item -> 5 (rejected).
        foreach (var (items, legal) in new[] { (1, true), (2, false) })
        {
            var s = FollowerAttackTable();
            var kahoku = Add(s, "oracle-l5r-807", 0, Zone.Battlefield, province: 0);
            var (host, _) = Followed(s, "training-crane-veteran", dishonorable: true);
            for (var i = 0; i < items; i++) Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: host.Id);
            var e = MatchEngine.FromTestState(s);
            var command = UseAtk(e, "kahoku-exp-attack", kahoku.Id, host.Id, [host.Id]);
            if (legal) { Accept(e, command); Equal(e.State.Cards[host.Id].Zone, Zone.Dead); } else AcceptSurvive(e, command, host.Id);
        }
    }
    {
        // The Attack half is optional (targeting is its own step, "not required up front"): -4F alone is a legal action, destroying nothing.
        var (s, kahoku, h, _, _, d3) = Table();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, h.Id));
        Check(e.State.Cards[h.Id].Modifiers.Any(m => m.Amount == -4));
        Equal(e.State.Cards[d3.Id].Zone, Zone.Battlefield, "No Attack target chosen: nothing is destroyed");
        // A lone Follower is an ordinary Attack target, honorable host or not.
        var (s2, kahoku2, h2, hf2, _, _) = Table();
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, UseAtk(e2, "kahoku-exp-attack", kahoku2.Id, h2.Id, [hf2.Id]));
        Equal(e2.State.Cards[hf2.Id].Zone, Zone.FateDiscard); Equal(e2.State.Cards[h2.Id].Zone, Zone.Battlefield);
    }
    {
        // Malformed selections: an own card as the Attack target, two Attack targets, a missing/own/Follower -4F target.
        var (s, kahoku, h, hf, _, d3) = Table();
        var own = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, h.Id, [own.Id]));
        Reject(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, h.Id, [hf.Id, d3.Id]));
        Reject(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, null, [d3.Id]));
        Reject(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, own.Id, [d3.Id]));
        Reject(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, hf.Id, [d3.Id]));
    }
    {
        // SOURCE-SCOPED: the exception does not leak to Attacks performed by any other card, even with Kahoku in play at the same battlefield.
        var (s, _, _, hf, _, d3) = Table();
        var ume = Add(s, "oracle-l5r-592", 0, Zone.Battlefield, province: 0);                // Ranged 4, another card's own Attack
        var plainKahoku = Add(s, "oracle-l5r-806", 0, Zone.Battlefield, province: 0);        // the non-experienced Kahoku: strength 3 (+2 vs dishonorable)
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseAtk(e, "asako-ume-attack", ume.Id, d3.Id));
        Reject(e, UseAtk(e, "bayushi-kahoku-attack", plainKahoku.Id, d3.Id));
        var control = MatchEngine.FromTestState(s);
        Accept(control, UseAtk(control, "asako-ume-attack", ume.Id, hf.Id)); // a lone Follower is fine: the rejection above is the Follower immunity, nothing else
        // The evaluator itself: it is the PERFORMER that is compared. Any Ranged ability performed by Kahoku qualifies; the same ability
        // performed by someone else, or a Melee Attack by Kahoku, does not; an honorable target never does.
        var ev = MatchEngine.FromTestState(s); var st = ev.State;
        var kahokuCard = st.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-807"); var umeCard = st.Cards[ume.Id];
        Check(ev.AttackMayTargetFollowedPersonality("asako-ume-attack", 0, kahokuCard, st.Cards[d3.Id]), "Ranged ability performed by Kahoku, dishonorable followed target");
        Check(!ev.AttackMayTargetFollowedPersonality("asako-ume-attack", 0, umeCard, st.Cards[d3.Id]), "The same ability performed by another card: no exception");
        Check(!ev.AttackMayTargetFollowedPersonality("chiyo-attack", 0, kahokuCard, st.Cards[d3.Id]), "A Melee Attack performed by Kahoku: the trait names Ranged Attacks");
        Check(!ev.AttackMayTargetFollowedPersonality("asako-ume-attack", 0, kahokuCard, st.Cards.Values.Single(c => c.OwnerId == 1 && c.DefinitionId == "training-crane-samurai" && !c.Dishonorable)), "An honorable target: no exception");
    }
    {
        // The Attack pipeline still runs through the shared reaction window: the declared target is fixed at declaration (PendingAttack),
        // a Reaction can change strength but neither legalize nor re-target, and the followed Personality is destroyed on resolution.
        MatchEngine Declared(out CardInstance d3, out CardInstance reactor)
        {
            var (s, kahoku, _, _, _, target) = Table(); d3 = target;
            reactor = Add(s, "training-attack-reactor", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, UseAtk(e, "kahoku-exp-attack", kahoku.Id, target.Id, [target.Id]));
            return e;
        }
        var e1 = Declared(out var t1, out var reactor1);
        Equal(e1.State.PendingChoice?.Type, "attackReaction"); Equal(e1.State.PendingAttack?.TargetId, t1.Id); Equal(e1.State.PendingAttack?.Strength, 3); Equal(e1.State.PendingAttack?.Kind, "Ranged");
        Accept(e1, new GameCommand("useAbility", 1, e1.Version, CardId: reactor1.Id, AbilityId: "training-attack-negate")); // nobody holds another reaction: the window closes (issue #644)
        Equal(e1.State.Cards[t1.Id].Zone, Zone.Battlefield, "Negated in the window: the (legal) target survives");
        var e2 = Declared(out var t2, out _);
        Accept(e2, new GameCommand("declineReaction", 1, e2.Version)); // the attacker holds no reaction: this one pass closes the window (issue #644)
        Equal(e2.State.Cards[t2.Id].Zone, Zone.Dead, "Not negated: destroyed on resolution, its Follower with it");
    }
    {
        // Offered through QueryActions, and the offered command validates.
        var (s, _, _, _, _, _) = Table();
        var e = MatchEngine.FromTestState(s);
        Check(OfferedFor(e, 0, "kahoku-exp-attack").Length >= 1, "Offered as an enabled action with a command that validates");
    }
}

static void YoritomoEbumiGrant()
{
    // (e): "One Ranged Attack from your next Ranged Attack action this battle may target a Personality with attached Followers.
    // If you have Reconnaissance, you may take an additional Battle action."
    MatchState Table(bool recon, out CardInstance ebumi, out CardInstance ume, out CardInstance chiyo, out CardInstance isas, out CardInstance host, out CardInstance follower, out CardInstance host2)
    {
        var s = FollowerAttackTable();
        ebumi = Add(s, "oracle-l5r-9832", 0, Zone.Battlefield, province: 0);
        ume = Add(s, "oracle-l5r-592", 0, Zone.Battlefield, province: 0);   // Ranged 4
        chiyo = Add(s, "oracle-l5r-10290", 0, Zone.Battlefield, province: 0); // Melee 4
        isas = Add(s, "oracle-l5r-8840", 0, Zone.Battlefield, province: 0);  // Ranged 3
        (host, follower) = Followed(s, "training-crane-samurai");         // Force 3
        host2 = Add(s, "training-crane-courtier", 1, Zone.Battlefield, province: 0); // Force 2, no Follower
        if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
        return s;
    }
    var s0 = Table(false, out var ebumi, out var ume, out var chiyo, out var isas, out var host, out var follower, out var host2);
    Check(CardCatalog.Get("oracle-l5r-9832").Abilities!.SequenceEqual(new[] { "yoritomo-ebumi-grant" }));
    {
        var e = MatchEngine.FromTestState(s0);
        Reject(e, UseAtk(e, "asako-ume-attack", ume.Id, host.Id)); // control: immune before the grant
        Accept(e, UseAtk(e, "yoritomo-ebumi-grant", ebumi.Id));
        Equal(e.State.AttackTargetGrants.Count, 1, "The action leaves a standing grant"); Equal(e.State.AttackTargetGrants[0].PlayerId, 0); Equal(e.State.AttackTargetGrants[0].Exception.Kind, "Ranged");
        Equal(e.State.AdditionalActions.Count, 0, "No Reconnaissance: no additional Battle action"); Equal(e.State.PriorityPlayerId, 1, "...so priority passes to the opponent");
        Accept(e, Cmd(e, "pass"));
        // A MELEE Attack action neither uses the exception nor spends the grant ("your next RANGED Attack action").
        Reject(e, UseAtk(e, "chiyo-attack", chiyo.Id, host.Id));
        Accept(e, UseAtk(e, "chiyo-attack", chiyo.Id, host2.Id));
        Equal(e.State.AttackTargetGrants.Count, 1, "A Melee action leaves the Ranged grant alone");
        Accept(e, Cmd(e, "pass"));
        // The next Ranged Attack action: the followed Personality is a legal target...
        Accept(e, UseAtk(e, "asako-ume-attack", ume.Id, host.Id));
        Equal(e.State.Cards[host.Id].Zone, Zone.Dead, "The Ranged Attack destroyed the followed Personality"); Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "...with his Follower");
        Equal(e.State.AttackTargetGrants.Count, 0, "...and used the grant up");
        Accept(e, Cmd(e, "pass"));
        // ...and only that one action: a later Ranged Attack is back to the ordinary rule.
    }
    {
        var s = Table(false, out var eb, out var um, out _, out var isasCard, out var followed, out _, out _);
        var (_, otherFollower) = Followed(s, "training-crane-courtier"); // a second, unrelated followed Personality: his Follower is a plain legal target
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "yoritomo-ebumi-grant", eb.Id)); Accept(e, Cmd(e, "pass"));
        // "Your next Ranged Attack action" is spent by that action even when it did not need the exception (here: a lone Follower).
        Accept(e, UseAtk(e, "asako-ume-attack", um.Id, otherFollower.Id));
        Equal(e.State.AttackTargetGrants.Count, 0, "Spent by the next Ranged Attack action, needed or not");
        Accept(e, Cmd(e, "pass"));
        Reject(e, UseAtk(e, "tsuruchi-isas-attack", isasCard.Id, followed.Id));
    }
    {
        // With Reconnaissance at the current battlefield: the additional Battle action, priority stays with Ebumi's player.
        var s = Table(true, out var eb, out var um, out _, out _, out var followed, out _, out _);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "yoritomo-ebumi-grant", eb.Id));
        Equal(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 1, "Reconnaissance: an additional Battle action is granted"); Equal(e.State.PriorityPlayerId, 0, "...so the same player keeps priority");
        Accept(e, UseAtk(e, "asako-ume-attack", um.Id, followed.Id)); // taken as that additional action, still covered by the grant
        Equal(e.State.Cards[followed.Id].Zone, Zone.Dead);
        // Reconnaissance at a different province, or held by the other player, is not "you have Reconnaissance" here.
        var s2 = Table(false, out var eb2, out _, out _, out _, out _, out _, out _);
        s2.Players[1].Provinces[1].Reconnaissance[0] = true; s2.Players[1].Provinces[0].Reconnaissance[1] = true;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, UseAtk(e2, "yoritomo-ebumi-grant", eb2.Id));
        Equal(e2.State.AdditionalActions.Count, 0); Equal(e2.State.PriorityPlayerId, 1);
    }
    {
        // The grant is battle-scoped: an unused one ends with the battle's resolution.
        var s = Table(false, out var eb, out _, out _, out _, out _, out _, out _);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "yoritomo-ebumi-grant", eb.Id));
        Equal(e.State.AttackTargetGrants.Count, 1);
        Accept(e, Cmd(e, "pass")); Accept(e, Cmd(e, "pass")); // both players pass: the battle resolves
        Equal(e.State.AttackTargetGrants.Count, 0, "An unused grant is cleared when the battle resolves");
    }
    {
        // Offered through QueryActions, and the offered command validates.
        var s = Table(false, out _, out _, out _, out _, out _, out _, out _);
        Equal(OfferedFor(MatchEngine.FromTestState(s), 0, "yoritomo-ebumi-grant").Length, 1);
    }
}

static void TargetStatusPredicate()
{
    // The generalized target-status predicate (P2): any-of, one representation of honor, keyword-only / clan-only / clan-or-keyword terms.
    var s = FollowerAttackTable();
    CardInstance P(string def, Action<CardInstance>? tweak = null) { var c = Add(s, def, 1, Zone.Battlefield, province: 0); tweak?.Invoke(c); return c; }
    var honorable = P("training-lion-samurai");
    var crane = P("training-crane-samurai");
    var dishonorable = P("training-lion-samurai", c => c.Dishonorable = true);
    var ninja = P("training-ninja-source");                                                     // printed Ninja keyword, Unaligned
    var kolat = P("training-lion-samurai", c => c.GrantedKeywords = [new("Kolat", "test-fixture", "Permanent")]);
    var shadowClan = P("training-shadowlands-personality");                                     // Shadowlands CLAN, no keyword
    var rikate = P("oracle-l5r-576");                                                           // Phoenix Clan + the printed Shadowlands KEYWORD, no Shadowlands Clan
    var dishonorableNinjaShadow = P("training-shadowlands-personality", c => { c.Dishonorable = true; c.GrantedKeywords = [new("Ninja", "test-fixture", "Permanent")]; }); // matches EVERY term
    var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: honorable.Id); follower.Dishonorable = true; // a Follower is never (dis)honorable, whatever the flag says
    var e = MatchEngine.FromTestState(s);
    bool M(CardInstance c, TargetStatus t) => e.MatchesTargetStatus(e.State.Cards[c.Id], t);
    // honorable / dishonorable
    Check(M(honorable, TargetStatus.IsHonorable) && !M(dishonorable, TargetStatus.IsHonorable), "honorable: a Personality not in the dishonorable state");
    Check(M(dishonorable, TargetStatus.IsDishonorable) && !M(honorable, TargetStatus.IsDishonorable), "dishonorable");
    Check(!M(follower, TargetStatus.IsHonorable) && !M(follower, TargetStatus.IsDishonorable), "Only a Personality is honorable or dishonorable");
    // keyword-only
    var ninjaOnly = new TargetStatus(Keywords: ["Ninja"]);
    Check(M(ninja, ninjaOnly) && M(dishonorableNinjaShadow, ninjaOnly) && !M(shadowClan, ninjaOnly) && !M(honorable, ninjaOnly), "keyword term: printed and granted keywords, nothing else");
    Check(M(kolat, new TargetStatus(Keywords: ["Kolat"])) && !M(ninja, new TargetStatus(Keywords: ["Kolat"])), "a GRANTED keyword counts");
    // clan-only vs clan-or-keyword
    var clanOnly = new TargetStatus(Clans: ["Shadowlands"]);
    Check(M(shadowClan, clanOnly) && !M(rikate, clanOnly), "Clans: Clan alignment only -- Rikate's Shadowlands is a keyword");
    Check(M(shadowClan, TargetStatus.IsShadowlands) && M(rikate, TargetStatus.IsShadowlands) && !M(honorable, TargetStatus.IsShadowlands) && !M(ninja, TargetStatus.IsShadowlands), "ClanOrKeyword: Clan alignment OR the printed keyword");
    Check(M(honorable, new TargetStatus(Clans: ["Lion"])) && !M(crane, new TargetStatus(Clans: ["Lion"])), "a real Clan term");
    // combinations: any one term is sufficient; none matching is false
    var dj = TargetStatus.DishonorableKolatNinjaOrShadowlands;
    foreach (var (card, expected, why) in new[] { (dishonorable, true, "dishonorable"), (ninja, true, "Ninja"), (kolat, true, "Kolat"), (shadowClan, true, "Shadowlands clan"), (rikate, true, "Shadowlands keyword"), (honorable, false, "none of them") })
        Equal(M(card, dj), expected, $"dishonorable/Kolat/Ninja/Shadowlands: {why}");
    Check(M(dishonorableNinjaShadow, dj), "A card satisfying several terms matches (once -- the answer is a boolean)");
    Check(!M(honorable, TargetStatus.KolatNinjaOrShadowlands) && M(rikate, TargetStatus.KolatNinjaOrShadowlands) && !M(dishonorable, TargetStatus.KolatNinjaOrShadowlands), "KolatNinjaOrShadowlands has no honor term");
    Check(!M(honorable, new TargetStatus()), "An empty specification matches nothing");
    Equal(dj.Describe(), "dishonorable, Kolat, Ninja, or Shadowlands"); Equal(TargetStatus.IsDishonorable.Describe(), "dishonorable"); Equal(new TargetStatus(Honorable: true, Dishonorable: true).Describe(), "honorable or dishonorable");

    // ---- As an EFFECT condition: Dispensing Justice ("destroy him if dishonorable, Kolat, Ninja, or Shadowlands", else move him home) and
    // Winds of Dismissal (Honor loss equal to his base Personal Honor), each parameterized by the ability's own IfTarget. The migration
    // keeps every old outcome EXCEPT the intended fix: a Shadowlands-KEYWORD-only card (Asako Rikate) now counts, as Mitsuko's already did.
    Equal(AbilityCatalog.All["dispensing-justice-destroy"].IfTarget, TargetStatus.DishonorableKolatNinjaOrShadowlands);
    Equal(AbilityCatalog.All["winds-of-dismissal-choice"].IfTarget, TargetStatus.DishonorableKolatNinjaOrShadowlands);
    Equal(AbilityCatalog.All["mitsuko-bow-destroy"].IfTarget, TargetStatus.KolatNinjaOrShadowlands);
    foreach (var (def, tweak, destroyed, why) in new (string, Action<CardInstance>?, bool, string)[]
    {
        ("training-lion-samurai",null,false,"ordinary honorable: moved home"),
        ("training-lion-samurai",c=>c.Dishonorable=true,true,"dishonorable"),
        ("training-ninja-source",null,true,"printed Ninja"),
        ("training-lion-samurai",c=>c.GrantedKeywords=[new("Kolat","t","Permanent")],true,"granted Kolat"),
        ("training-shadowlands-personality",null,true,"Shadowlands clan"),
        ("training-shadowlands-personality",c=>{c.Dishonorable=true;c.GrantedKeywords=[new("Ninja","t","Permanent")];},true,"every term at once: destroyed (once)"),
        ("oracle-l5r-576",null,true,"THE FIX: Shadowlands printed as a keyword only (Asako Rikate, Phoenix Clan)"),
    })
    {
        var t = FollowerAttackTable();
        var strat = Add(t, "oracle-l5r-2029", 0, Zone.Hand);
        var performer = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); performer.GrantedKeywords = [new("Magistrate", "test-fixture", "Permanent")];
        var target = Add(t, def, 1, Zone.Battlefield, province: 0); tweak?.Invoke(target);
        var ee = MatchEngine.FromTestState(t);
        Accept(ee, new GameCommand("useAbility", 0, ee.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "dispensing-justice-destroy", PerformerId: performer.Id));
        Equal(ee.State.Cards[target.Id].Zone, destroyed ? Zone.Dead : Zone.Home, $"Dispensing Justice: {why}");
    }
    {
        // Dispensing Justice's own "no attachments" clause is unchanged: a followed Shadowlands-keyword Personality is only moved home.
        var t = FollowerAttackTable();
        var strat = Add(t, "oracle-l5r-2029", 0, Zone.Hand);
        var performer = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); performer.GrantedKeywords = [new("Magistrate", "test-fixture", "Permanent")];
        var target = Add(t, "oracle-l5r-576", 1, Zone.Battlefield, province: 0); WithFollower(t, target);
        var ee = MatchEngine.FromTestState(t);
        Accept(ee, new GameCommand("useAbility", 0, ee.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "dispensing-justice-destroy", PerformerId: performer.Id));
        Equal(ee.State.Cards[target.Id].Zone, Zone.Home, "An attachment keeps him from being destroyed, exactly as before");
    }
    foreach (var (def, tweak, loses, why) in new (string, Action<CardInstance>?, bool, string)[]
    {
        ("training-crane-samurai",null,false,"ordinary honorable: no loss"),
        ("training-crane-samurai",c=>c.Dishonorable=true,true,"dishonorable"),
        ("training-shadowlands-personality",null,true,"Shadowlands clan"),
        ("training-shadowlands-personality",c=>{c.Dishonorable=true;c.GrantedKeywords=[new("Ninja","t","Permanent")];},true,"every term at once: the loss applies ONCE"),
        ("oracle-l5r-576",null,true,"THE FIX: Shadowlands keyword only"),
    })
    {
        var t = MatchEngine.CreateEconomyScenario().State; t.Phase = Phase.Action; t.ActivePlayerId = 0; t.PriorityPlayerId = 0;
        var host = Add(t, "training-shugenja", 0, Zone.Home); host.GrantedKeywords = [new("Air", "test-fixture", "Permanent")];
        var winds = Add(t, "oracle-l5r-9344", 0, Zone.Attached, attachedTo: host.Id);
        var target = Add(t, def, 1, Zone.Home); tweak?.Invoke(target);
        var ee = MatchEngine.FromTestState(t);
        var before = ee.State.Players[1].Honor;
        Accept(ee, new GameCommand("useAbility", 0, ee.Version, CardId: winds.Id, TargetId: target.Id, AbilityId: "winds-of-dismissal-choice"));
        Equal(ee.State.Players[1].Honor, before - (loses ? CardCatalog.Get(def).PersonalHonor : 0), $"Winds of Dismissal: {why}");
    }

    // ---- As an effect condition on an Attack's strength: the non-experienced Kahoku's "+2 strength if the target is dishonorable".
    Equal(AbilityCatalog.All["bayushi-kahoku-attack"].IfTarget, TargetStatus.IsDishonorable);
    foreach (var (dishonorableTarget, expectedZone) in new[] { (false, Zone.Battlefield), (true, Zone.Dead) })
    {
        var t = FollowerAttackTable();
        var kahoku = Add(t, "oracle-l5r-806", 0, Zone.Battlefield, province: 0);
        var target = Add(t, "training-crane-veteran", 1, Zone.Battlefield, province: 0); target.Dishonorable = dishonorableTarget; // Force 5: needs the +2
        var ee = MatchEngine.FromTestState(t);
        var command = UseAtk(ee, "bayushi-kahoku-attack", kahoku.Id, target.Id);
        if (dishonorableTarget) { Accept(ee, command); Equal(ee.State.Cards[target.Id].Zone, expectedZone); } else AcceptSurvive(ee, command, target.Id);
    }

    // ---- As a target-LEGALITY layer: the existing honorable-personality / dishonorable-personality categories now ask the predicate, and
    // TargetMustBe layers it on any category (Kakita Kazan: an enemy DISHONORABLE Personality).
    Equal(AbilityCatalog.All["kazan-shame"].TargetMustBe, TargetStatus.IsDishonorable);
}

static void PersonalityWave24Cards()
{
    Equal(PublishedContent.PersonalityWave24.Cards.Length, 6, "Personality wave 24 publishes six Personalities");
    foreach (var (id, ability) in new[]{("oracle-l5r-807","kahoku-exp-attack"),("oracle-l5r-9831","tsuruchi-kosoko-exp-attack"),("oracle-l5r-9832","yoritomo-ebumi-grant"),("oracle-l5r-4185","kazan-shame"),
        ("oracle-l5r-3311","hiruma-nitani-attack"),("oracle-l5r-10276","shinoda-exp-bow-destroy")})
        Check(CardCatalog.Get(id).Abilities!.SequenceEqual(new[] { ability }) && CardCatalog.Get(id).Type == CardType.Personality, $"{id} wires {ability}");
    Equal(CardCatalog.Get("oracle-l5r-9831").Text, "Battle: Ranged 6 Attack that may target a Personality with Followers.");

    // ---- Kakita Kazan: "Target an enemy dishonorable Personality: Bow him. His controller loses 2 Honor. Gain 2 Honor."
    MatchState KazanTable(out CardInstance kazan, out CardInstance dishonorable, out CardInstance honorable, out CardInstance ownDishonorable, out CardInstance enemyFollower)
    {
        var s = FollowerAttackTable();
        kazan = Add(s, "oracle-l5r-4185", 0, Zone.Battlefield, province: 0);
        dishonorable = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); dishonorable.Dishonorable = true;
        honorable = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        ownDishonorable = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); ownDishonorable.Dishonorable = true;
        enemyFollower = WithFollower(s, dishonorable);
        return s;
    }
    {
        var s = KazanTable(out var kazan, out var dishonorable, out var honorable, out var ownDishonorable, out var enemyFollower);
        var e = MatchEngine.FromTestState(s);
        var refused = e.Execute(UseAtk(e, "kazan-shame", kazan.Id, honorable.Id));
        Check(!refused.Accepted && refused.Error!.Contains("dishonorable"), $"An honorable target is not a legal target ({refused.Error})");
        Reject(e, UseAtk(e, "kazan-shame", kazan.Id, honorable.Id));
        Reject(e, UseAtk(e, "kazan-shame", kazan.Id, ownDishonorable.Id)); // dishonorable but not an ENEMY Personality
        Reject(e, UseAtk(e, "kazan-shame", kazan.Id, enemyFollower.Id));   // a Follower is not a Personality
        var (mine, theirs) = (e.State.Players[0].Honor, e.State.Players[1].Honor);
        Accept(e, UseAtk(e, "kazan-shame", kazan.Id, dishonorable.Id));
        Check(e.State.Cards[dishonorable.Id].Bowed, "Bowed"); Equal(e.State.Players[1].Honor, theirs - 2, "His controller loses 2 Honor"); Equal(e.State.Players[0].Honor, mine + 2, "Gain 2 Honor");
        Check(OfferedFor(MatchEngine.FromTestState(s), 0, "kazan-shame").Length >= 1, "Offered only against a legal (enemy dishonorable) target, and it validates");
    }
    {
        // A dishonorable Kazan performing it is rehonored instead of gaining the Honor; a Stalwart target's bow is negated, the Honor loss is not.
        var s = KazanTable(out var kazan, out var dishonorable, out _, out _, out _);
        kazan.Dishonorable = true; dishonorable.GrantedKeywords = [new("Stalwart", "test-fixture", "Permanent")];
        var e = MatchEngine.FromTestState(s);
        var (mine, theirs) = (e.State.Players[0].Honor, e.State.Players[1].Honor);
        Accept(e, UseAtk(e, "kazan-shame", kazan.Id, dishonorable.Id));
        Check(!e.State.Cards[kazan.Id].Dishonorable && e.State.Players[0].Honor == mine, "A dishonorable performer is rehonored instead of gaining Honor");
        Check(!e.State.Cards[dishonorable.Id].Bowed, "Stalwart negates the bow"); Equal(e.State.Players[1].Honor, theirs - 2, "...but not the Honor loss");
        // With only an honorable enemy Personality on the table, no enabled Kazan action is offered at all.
        var s2 = FollowerAttackTable(); Add(s2, "oracle-l5r-4185", 0, Zone.Battlefield, province: 0); Add(s2, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        Equal(OfferedFor(MatchEngine.FromTestState(s2), 0, "kazan-shame").Length, 0);
    }

    // ---- Hiruma Nitani: "Bow Nitani unless you have Reconnaissance: Ranged 4 Attack, with +2 strength if the target is Shadowlands."
    foreach (var (recon, bowsNitani) in new[] { (false, true), (true, false) })
    {
        var s = FollowerAttackTable();
        var nitani = Add(s, "oracle-l5r-3311", 0, Zone.Battlefield, province: 0);
        var lone = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "hiruma-nitani-attack", nitani.Id, lone.Id));
        Equal(e.State.Cards[lone.Id].Zone, Zone.Dead, "Ranged 4 destroys a Force-3 target");
        Equal(e.State.Cards[nitani.Id].Bowed, bowsNitani, recon ? "Reconnaissance: Nitani is not bowed" : "No Reconnaissance: Nitani bows");
    }
    foreach (var (def, tweak, expectHit, why) in new (string, Action<CardInstance>?, bool, string)[]
    {
        ("training-lion-veteran",null,false,"an ordinary Force-6 target: 4 < 6"),
        ("training-lion-veteran",c=>c.GrantedKeywords=[new("Shadowlands","t","Permanent")],true,"a Shadowlands (keyword) Force-6 target: 4+2 = 6"),
        ("training-shadowlands-personality",c=>c.Modifiers=[new("test-fixture","test-fixture","Force",4)],true,"a Shadowlands CLAN Force-6 target"),
        ("oracle-l5r-576",c=>c.Modifiers=[new("test-fixture","test-fixture","Force",2)],true,"Asako Rikate (Shadowlands as a printed keyword only), Force 6"),
        ("training-lion-veteran",c=>c.Dishonorable=true,false,"dishonorable is NOT Shadowlands: still 4"),
    })
    {
        var s = FollowerAttackTable();
        var nitani = Add(s, "oracle-l5r-3311", 0, Zone.Battlefield, province: 0);
        var target = Add(s, def, 1, Zone.Battlefield, province: 0); tweak?.Invoke(target);
        var e = MatchEngine.FromTestState(s);
        var command = UseAtk(e, "hiruma-nitani-attack", nitani.Id, target.Id);
        if (expectHit) { Accept(e, command); Equal(e.State.Cards[target.Id].Zone, Zone.Dead, why); } else AcceptSurvive(e, command, target.Id);
    }
    {
        var s = FollowerAttackTable(); var nitani = Add(s, "oracle-l5r-3311", 0, Zone.Battlefield, bowed: true, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseAtk(e, "hiruma-nitani-attack", nitani.Id)); // a bowed Nitani cannot act, Reconnaissance or not
    }

    // ---- Kuni Shinoda - exp: "Target an enemy card: Bow it. If it is Shadowlands, destroy it."
    foreach (var (def, tweak, destroyed, why) in new (string, Action<CardInstance>?, bool, string)[]
    {
        ("training-lion-samurai",null,false,"an ordinary Personality is only bowed"),
        ("training-shadowlands-personality",null,true,"Shadowlands clan: bowed then destroyed"),
        ("oracle-l5r-576",null,true,"Shadowlands keyword only: bowed then destroyed"),
        ("training-stalwart-personality",c=>c.GrantedKeywords=[new("Shadowlands","t","Permanent")],true,"Stalwart negates only the bow: the destroy is a separate clause"),
    })
    {
        var s = FollowerAttackTable();
        var shinoda = Add(s, "oracle-l5r-10276", 0, Zone.Battlefield, province: 0);
        var target = Add(s, def, 1, Zone.Battlefield, province: 0); tweak?.Invoke(target);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseAtk(e, "shinoda-exp-bow-destroy", shinoda.Id, target.Id));
        if (destroyed) Equal(e.State.Cards[target.Id].Zone, Zone.Dead, why); else { Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, why); Check(e.State.Cards[target.Id].Bowed, why + " (bowed)"); }
    }
    {
        // A lone enemy Follower is "an enemy card" too: destroyed as an attachment when Shadowlands (its host survives), only bowed otherwise; own cards are out.
        var s = FollowerAttackTable();
        var shinoda = Add(s, "oracle-l5r-10276", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var dark = WithFollower(s, host); dark.GrantedKeywords = [new("Shadowlands", "t", "Permanent")];
        var plain = WithFollower(s, host);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseAtk(e, "shinoda-exp-bow-destroy", shinoda.Id, own.Id));
        var e1 = MatchEngine.FromTestState(s); Accept(e1, UseAtk(e1, "shinoda-exp-bow-destroy", shinoda.Id, dark.Id));
        Equal(e1.State.Cards[dark.Id].Zone, Zone.FateDiscard, "A Shadowlands Follower is destroyed as an attachment"); Equal(e1.State.Cards[host.Id].Zone, Zone.Battlefield, "...its host survives");
        var e2 = MatchEngine.FromTestState(s); Accept(e2, UseAtk(e2, "shinoda-exp-bow-destroy", shinoda.Id, plain.Id));
        Equal(e2.State.Cards[plain.Id].Zone, Zone.Attached, "An ordinary Follower is only bowed"); Check(e2.State.Cards[plain.Id].Bowed);
    }

}

static void AsakoUmeAttack()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var ume = Add(s, "oracle-l5r-592", 0, Zone.Battlefield, province: 0);
    var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 2
    var e = MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == ume.Id).Force, 3, "No Spell attached: base Force 3");
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ume.Id, TargetId: target.Id, AbilityId: "asako-ume-attack"));
    Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Ranged 4 Attack is unconditional regardless of the Spell check");

    var s2 = StrongholdBattle("oracle-l5r-1218");
    var ume2 = Add(s2, "oracle-l5r-592", 0, Zone.Battlefield, province: 0);
    Add(s2, "training-spell", 0, Zone.Attached, attachedTo: ume2.Id, province: 0);
    var e2 = MatchEngine.FromTestState(s2);
    Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == ume2.Id).Force, 4, "A Spell attached raises her own Force by 1; the Attack's strength is unaffected");
}
static void Reconnaissance()
{
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var scout = Add(s, "training-scout", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    Check(!e.State.Players[1].Provinces[0].Reconnaissance[0], "Defaults to false");
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: scout.Id, AbilityId: "training-gain-reconnaissance", Province: 4)); // out of range
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: scout.Id, AbilityId: "training-gain-reconnaissance", Province: 0));
    Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Granted for player 0 in player 1's province 0");
    Check(!e.State.Players[1].Provinces[1].Reconnaissance[0], "Not granted at a different province");
    Check(!e.State.Players[1].Provinces[0].Reconnaissance[1], "Not granted to the other player");
    Check(!e.State.Players[0].Provinces[0].Reconnaissance[0], "Not granted in the granting player's own province");

    // Move the same, now-Reconnaissance-carrying state into a battle and exercise the gated effect:
    // with Reconnaissance in the target's province, the strike's Honor-loss portion proceeds.
    var battle = e.State; battle.Phase = Phase.Battle; battle.CurrentBattleProvince = 0; battle.UnresolvedBattlefields = [0]; battle.PriorityPlayerId = 0;
    Add(battle, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
    var scoutAtBattle = Add(battle, "training-scout", 0, Zone.Battlefield, province: 0);
    var withRecon = Add(battle, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
    var eb = MatchEngine.FromTestState(battle);
    var honorBefore = eb.State.Players[1].Honor;
    Accept(eb, new GameCommand("useAbility", 0, eb.Version, CardId: scoutAtBattle.Id, TargetId: withRecon.Id, AbilityId: "training-reconnaissance-strike"));
    Equal(eb.State.Players[1].Honor, honorBefore - 2, "Reconnaissance is present in this province: the Honor-loss portion proceeds");

    // Without Reconnaissance, the same ability still succeeds -- its gated portion just does nothing.
    var s2 = MatchEngine.CreateEconomyScenario().State;
    s2.Phase = Phase.Battle; s2.CurrentBattleProvince = 0; s2.UnresolvedBattlefields = [0]; s2.PriorityPlayerId = 0;
    Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
    var scoutNoRecon = Add(s2, "training-scout", 0, Zone.Battlefield, province: 0);
    var withoutRecon = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
    var en = MatchEngine.FromTestState(s2);
    var honorBefore2 = en.State.Players[1].Honor;
    Accept(en, new GameCommand("useAbility", 0, en.Version, CardId: scoutNoRecon.Id, TargetId: withoutRecon.Id, AbilityId: "training-reconnaissance-strike"));
    Equal(en.State.Players[1].Honor, honorBefore2, "No Reconnaissance in this province: the action succeeds but its gated portion does nothing");
}
static void ReconnaissanceOnlyClearsOnBattleResolution()
{
    // Refilling a province with a new, unseen card does NOT clear Reconnaissance on its own --
    // resolving a battle there is the only thing that does.
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
    var scout = Add(s, "training-scout", 1, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    var id = ProvinceCard(e, 2);
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: scout.Id, AbilityId: "training-gain-reconnaissance", Province: 2));
    Check(e.State.Players[0].Provinces[2].Reconnaissance[1], "Granted before the province refills");
    var dynasty = e.State; dynasty.Phase = Phase.Dynasty; dynasty.PriorityPlayerId = 0; dynasty.ActivePlayerId = 0;
    e = MatchEngine.FromTestState(dynasty);
    Accept(e, Cmd(e, "purchase", id: id, sources: e.QuotePurchase(0, id).Payments[0].SourceIds));
    Check(e.State.Players[0].Provinces[2].Reconnaissance[1], "Still granted after the province refills -- only a battle resolving there clears it");

    // A tied battle resolves without destroying the province -- Reconnaissance still resets to No.
    var e2 = Battle(3, 3);
    e2.State.Players[1].Provinces[0].Reconnaissance[0] = true;
    Resolve(e2);
    Check(!e2.State.Players[1].Provinces[0].Destroyed, "Sanity: a tied battle does not destroy the province");
    Check(!e2.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance resets to No once the battle resolves, whether or not the province survives");

    // Destroying a province via battle also clears it (same precedent values as ProvinceThreshold).
    var e3 = Battle(9, 2, 6);
    e3.State.Players[1].Provinces[0].Reconnaissance[0] = true;
    Resolve(e3);
    Check(e3.State.Players[1].Provinces[0].Destroyed, "Sanity: the province was actually destroyed");
    Check(!e3.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance clears when its province's battle resolves, including when destroyed");
}
static void TacticalAdvantage()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var tactician = Add(s, "training-tactician", 0, Zone.Battlefield, province: 0);
    var notTactician = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
    var focus1 = Add(s, "training-focus-strategy", 0, Zone.Hand);
    var focus2 = Add(s, "training-focus-strategy", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s);
    Reject(e, new GameCommand("useAbility", 0, e.Version, PerformerId: notTactician.Id, TargetId: focus1.Id, AbilityId: "tactical-advantage"));
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == tactician.Id).Force, 2, "Base Force 2, no bonus yet");
    Accept(e, new GameCommand("useAbility", 0, e.Version, PerformerId: tactician.Id, TargetId: focus1.Id, AbilityId: "tactical-advantage"));
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == tactician.Id).Force, 5, "Base 2 + discarded card's Focus Value 3");
    Equal(e.State.Cards[focus1.Id].Zone, Zone.FateDiscard, "The discarded card moves to the Fate discard pile");
    // Any number of times per turn: a second use in the same turn is not blocked by the normal
    // once-per-turn dedupe -- only its own cost (discarding another card) limits it -- but each individual
    // Tactician may perform at most one Tactical action per turn (core rules Glossary, Tactical / Tactician;
    // enforced since issue #503, Moto Taigo), so the second use needs a second Tactician. Priority passed
    // to the opponent after the first use (like any action), so they must act (or pass) before it
    // returns to player 0 for the second use.
    Accept(e, new GameCommand("pass", 1, e.Version));
    Reject(e, new GameCommand("useAbility", 0, e.Version, PerformerId: tactician.Id, TargetId: focus2.Id, AbilityId: "tactical-advantage"));
    var s2 = e.State; var second = Add(s2, "training-tactician", 0, Zone.Battlefield, province: 0); var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, PerformerId: second.Id, TargetId: focus2.Id, AbilityId: "tactical-advantage"));
    Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == second.Id).Force, 5, "The second Tactician's own use: base 2 + 3");
}
static void ConquerorStraightensAfterVictory()
{
    // A single eligible Conqueror resolves immediately: no choice to make.
    var s = StrongholdBattle("oracle-l5r-1218");
    var conqueror = Add(s, "training-conqueror", 0, Zone.Battlefield, province: 0); // Force 2
    var other = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Force 4, no Conqueror
    var weak = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, attacker wins 6-3
    var e = MatchEngine.FromTestState(s);
    Resolve(e);
    Check(e.State.PendingChoice is null, "No choice needed with only one Conqueror candidate");
    Equal(e.State.Cards[conqueror.Id].Zone, Zone.Home); Check(!e.State.Cards[conqueror.Id].Bowed, "The sole Conqueror candidate returns home unbowed");
    Equal(e.State.Cards[other.Id].Zone, Zone.Home); Check(e.State.Cards[other.Id].Bowed, "A non-Conqueror attacking unit still returns home bowed");

    // Two eligible Conquerors: the attacker must choose which one straightens.
    var s2 = StrongholdBattle("oracle-l5r-1218");
    var c1 = Add(s2, "training-conqueror", 0, Zone.Battlefield, province: 0);
    var c2 = Add(s2, "training-conqueror", 0, Zone.Battlefield, province: 0);
    var weak2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, attacker wins 4-3
    var e2 = MatchEngine.FromTestState(s2);
    Resolve(e2);
    Equal(e2.State.PendingChoice?.Type, "conqueror", "Two eligible Conquerors: a real choice is required");
    Check(e2.State.Cards[c1.Id].Zone == Zone.Battlefield && e2.State.Cards[c2.Id].Zone == Zone.Battlefield, "Resolution defers until the choice is made");
    Reject(e2, new GameCommand("chooseConqueror", 0, e2.Version, TargetId: weak2.Id)); // not an eligible candidate
    Accept(e2, new GameCommand("chooseConqueror", 0, e2.Version, TargetId: c1.Id));
    Check(e2.State.PendingChoice is null && e2.State.Cards[c1.Id].Zone == Zone.Home && !e2.State.Cards[c1.Id].Bowed, "Chosen Conqueror returns home unbowed");
    Check(e2.State.Cards[c2.Id].Zone == Zone.Home && e2.State.Cards[c2.Id].Bowed, "The other eligible Conqueror still returns home bowed");
}
static void KensaiAndKamaWeaponCap()
{
    // A Kensai may attach two Weapons but not a third.
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var kensai = Add(s, "training-kensai", 0, Zone.Home);
    var w1 = Add(s, "training-weapon", 0, Zone.Hand);
    var w2 = Add(s, "training-weapon", 0, Zone.Hand);
    var w3 = Add(s, "training-weapon", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s);
    // Equip is a Limited action: it passes priority to the opponent (like any action), so they must
    // act (or pass) before it returns to player 0 for a second equip.
    Accept(e, Cmd(e, "equip", player: 0, id: w1.Id, sources: e.PaymentOptions(0, 1).First().SourceIds, target: kensai.Id));
    Accept(e, Cmd(e, "pass", player: 1));
    Accept(e, Cmd(e, "equip", player: 0, id: w2.Id, sources: e.PaymentOptions(0, 1).First().SourceIds, target: kensai.Id));
    Equal(e.State.Cards.Values.Count(c => c.AttachedTo == kensai.Id), 2, "Kensai carries two Weapons");
    Accept(e, Cmd(e, "pass", player: 1));
    Reject(e, Cmd(e, "equip", player: 0, id: w3.Id, target: kensai.Id));

    // A non-Kensai personality with a Kama may attach a second Weapon, but still not a third.
    var s2 = MatchEngine.CreateEconomyScenario().State;
    s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
    var plain = Add(s2, "training-crane-samurai", 0, Zone.Home);
    var kama = Add(s2, "training-kama", 0, Zone.Hand);
    var pw1 = Add(s2, "training-weapon", 0, Zone.Hand);
    var pw2 = Add(s2, "training-weapon", 0, Zone.Hand);
    var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, Cmd(e2, "equip", player: 0, id: pw1.Id, sources: e2.PaymentOptions(0, 1).First().SourceIds, target: plain.Id));
    Accept(e2, Cmd(e2, "pass", player: 1));
    Accept(e2, Cmd(e2, "equip", player: 0, id: kama.Id, sources: e2.PaymentOptions(0, 1).First().SourceIds, target: plain.Id));
    Equal(e2.State.Cards.Values.Count(c => c.AttachedTo == plain.Id), 2, "A plain Weapon plus a Kama: two Weapons total");
    Accept(e2, Cmd(e2, "pass", player: 1));
    Reject(e2, Cmd(e2, "equip", player: 0, id: pw2.Id, target: plain.Id));

    // A Kensai wielding a Kama is still capped at two Weapons -- the two allowances do not stack.
    var s3 = MatchEngine.CreateEconomyScenario().State;
    s3.Phase = Phase.Action; s3.PriorityPlayerId = 0; s3.ActivePlayerId = 0;
    var kensai2 = Add(s3, "training-kensai", 0, Zone.Home);
    var kama2 = Add(s3, "training-kama", 0, Zone.Hand);
    var kw1 = Add(s3, "training-weapon", 0, Zone.Hand);
    var kw2 = Add(s3, "training-weapon", 0, Zone.Hand);
    var e3 = MatchEngine.FromTestState(s3);
    Accept(e3, Cmd(e3, "equip", player: 0, id: kama2.Id, sources: e3.PaymentOptions(0, 1).First().SourceIds, target: kensai2.Id));
    Accept(e3, Cmd(e3, "pass", player: 1));
    Accept(e3, Cmd(e3, "equip", player: 0, id: kw1.Id, sources: e3.PaymentOptions(0, 1).First().SourceIds, target: kensai2.Id));
    Equal(e3.State.Cards.Values.Count(c => c.AttachedTo == kensai2.Id), 2, "Kensai + Kama: still two Weapons total");
    Accept(e3, Cmd(e3, "pass", player: 1));
    Reject(e3, Cmd(e3, "equip", player: 0, id: kw2.Id, target: kensai2.Id));
}
static void CavalryManeuversSegment()
{
    var s = MatchEngine.CreateStarterMatch().State; s.Phase = Phase.AttackDeclaration; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var infantry = Add(s, "training-crane-samurai", 0, Zone.Home); // no Cavalry keyword: Infantry
    var soloCavalry = Add(s, "training-cavalry", 0, Zone.Home); // Cavalry keyword, no Followers: Cavalry
    var mixedUnit = Add(s, "training-cavalry", 0, Zone.Home); // Cavalry Personality...
    Add(s, "training-follower", 0, Zone.Attached, attachedTo: mixedUnit.Id); // ...+ a non-Cavalry Follower: still Infantry
    var pureCavalryUnit = Add(s, "training-cavalry", 0, Zone.Home); // Cavalry Personality...
    Add(s, "training-cavalry-follower", 0, Zone.Attached, attachedTo: pureCavalryUnit.Id); // ...+ a Cavalry Follower: Cavalry
    Add(s, "training-cavalry", 1, Zone.Home); // the defender needs a Cavalry unit too, or his Cavalry segment is skipped (#627)
    var e = MatchEngine.FromTestState(s);
    Accept(e, Cmd(e, "declareAttack", player: 0));

    // Infantry Maneuvers: every unit may assign, Cavalry units included (they may also wait for Cavalry Maneuvers).
    var infantryOffer = e.QueryActions(0).First(a => a.Type == "attack" && a.Enabled).TargetIds!;
    Check(infantryOffer.Contains(soloCavalry.Id) && infantryOffer.Contains(pureCavalryUnit.Id) && infantryOffer.Contains(infantry.Id) && infantryOffer.Contains(mixedUnit.Id), "Cavalry units are offered in Infantry Maneuvers too");
    Accept(e, Cmd(e, "attack", player: 0, assignments: new() { [infantry.Id] = 0, [mixedUnit.Id] = 1 }));
    Equal(e.State.Cards[infantry.Id].Zone, Zone.Battlefield);
    Equal(e.State.Cards[mixedUnit.Id].Zone, Zone.Battlefield, "A Cavalry Personality with a non-Cavalry Follower assigns as Infantry");
    Equal(e.State.Phase, Phase.DefenseAssignment);

    Accept(e, Cmd(e, "assignDefense", player: 1, assignments: []));
    Equal(e.State.Phase, Phase.CavalryDeclaration, "Cavalry Maneuvers follows Infantry Maneuvers");

    // Cavalry Maneuvers: only the two genuine Cavalry units (both members of the unit carry Cavalry).
    Accept(e, Cmd(e, "attack", player: 0, assignments: new() { [soloCavalry.Id] = 0, [pureCavalryUnit.Id] = 1 }));
    Equal(e.State.Cards[soloCavalry.Id].Zone, Zone.Battlefield);
    Equal(e.State.Cards[pureCavalryUnit.Id].Zone, Zone.Battlefield, "A Cavalry Personality with a Cavalry Follower assigns as Cavalry");
    Equal(e.State.Phase, Phase.CavalryDefense);

    Accept(e, Cmd(e, "assignDefense", player: 1, assignments: []));
    Equal(e.State.Phase, Phase.BattleSelection, "Both Maneuvers segments complete before Battle Selection");
}
static void DefenderAssignmentQueries()
{
    foreach (var cavalry in new[] { false, true })
    {
        var s = MatchEngine.CreateStarterMatch().State;
        // After the Declaration Segment (#359) a battlefield exists at each declared province; a subset
        // here proves the defender is offered exactly the battlefields AssignDefense accepts.
        s.Phase = cavalry ? Phase.CavalryDeclaration : Phase.InfantryDeclaration;
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [2];
        var attacker = Add(s, cavalry ? "training-cavalry" : "training-crane-samurai", 0, Zone.Home);
        var defender = Add(s, cavalry ? "training-cavalry" : "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Where(a => a.Type == "attack" && a.Enabled)
            .SelectMany(a => a.Provinces!).Distinct().Order().SequenceEqual(new[] { 0, 1, 2, 3 }), "An attacker may create a battlefield at any surviving province");
        Accept(e, Cmd(e, "attack", assignments: new() { [attacker.Id] = 2 }));
        Equal(e.State.Phase, cavalry ? Phase.CavalryDefense : Phase.DefenseAssignment);
        var options = e.QueryActions(1).Where(a => a.Type == "assignDefense" && a.Enabled).ToArray();
        Check(options.Length > 0, "The available defender must be offered an assignment");
        foreach (var option in options)
        {
            Check(option.Provinces!.SequenceEqual(new[] { 2 }), "Defenders cannot be offered an unattacked province");
            var replay = MatchEngine.FromTestState(e.State);
            Accept(replay, option.Command!);
            Equal(replay.State.Cards[defender.Id].Zone, Zone.Battlefield);
            Equal(replay.State.Cards[defender.Id].ProvinceIndex, 2);
        }
        var empty = s;
        empty.Phase = cavalry ? Phase.CavalryDefense : Phase.DefenseAssignment; empty.PriorityPlayerId = 1;
        empty.UnresolvedBattlefields = [];
        var noBattle = MatchEngine.FromTestState(empty);
        Check(!noBattle.QueryActions(1).Any(a => a.Type == "assignDefense"), "No battlefield means no destination to defend");
        Accept(noBattle, noBattle.QueryActions(1).Single(a => a.Type == "pass" && a.Enabled).Command!);
    }
}
static void TwoHandedWeaponOverridesKensaiAndKama()
{
    // A Kensai may attach a Two-Handed Weapon as their only Weapon...
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var kensai = Add(s, "training-kensai", 0, Zone.Home);
    var twoHanded = Add(s, "training-two-handed-weapon", 0, Zone.Hand);
    var plain = Add(s, "training-weapon", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s);
    Accept(e, Cmd(e, "equip", player: 0, id: twoHanded.Id, sources: e.PaymentOptions(0, 1).First().SourceIds, target: kensai.Id));
    Equal(e.State.Cards.Values.Count(c => c.AttachedTo == kensai.Id), 1);
    // ...but it then blocks any further Weapon, even for a Kensai -- Two-Handed overrides the cap.
    Accept(e, Cmd(e, "pass", player: 1));
    Reject(e, Cmd(e, "equip", player: 0, id: plain.Id, target: kensai.Id));

    // A Kensai with a plain Weapon already attached cannot then attach a Two-Handed one either.
    var s2 = MatchEngine.CreateEconomyScenario().State;
    s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
    var kensai2 = Add(s2, "training-kensai", 0, Zone.Home);
    var plain2 = Add(s2, "training-weapon", 0, Zone.Hand);
    var twoHanded2 = Add(s2, "training-two-handed-weapon", 0, Zone.Hand);
    var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, Cmd(e2, "equip", player: 0, id: plain2.Id, sources: e2.PaymentOptions(0, 1).First().SourceIds, target: kensai2.Id));
    Accept(e2, Cmd(e2, "pass", player: 1));
    Reject(e2, Cmd(e2, "equip", player: 0, id: twoHanded2.Id, target: kensai2.Id));
}
static void DaigotsuKendoConquerorAndIkomaNatsuCavalry()
{
    // Daigotsu Kendo carries Conqueror: winning a battle straightens him instead of bowing him.
    var s = StrongholdBattle("oracle-l5r-1218");
    var kendo = Add(s, "oracle-l5r-1736", 0, Zone.Battlefield, province: 0); // Force 3, no defenders present
    var e = MatchEngine.FromTestState(s);
    Resolve(e);
    Check(!e.State.Cards[kendo.Id].Bowed, "Conqueror: Kendo returns home unbowed after winning");
    Equal(e.State.Cards[kendo.Id].Zone, Zone.Home);

    // Ikoma Natsu carries Cavalry: assigns during Cavalry Maneuvers, not Infantry Maneuvers.
    var s2 = MatchEngine.CreateStarterMatch().State; s2.Phase = Phase.AttackDeclaration; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
    var natsu = Add(s2, "oracle-l5r-3623", 0, Zone.Home);
    var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, Cmd(e2, "declareAttack", player: 0));
    Check(e2.QueryActions(0).First(a => a.Type == "attack" && a.Enabled).TargetIds!.Contains(natsu.Id), "a Cavalry Personality is also offered in Infantry Maneuvers");
    Accept(e2, Cmd(e2, "attack", player: 0, assignments: []));
    Accept(e2, Cmd(e2, "assignDefense", player: 1, assignments: []));
    Equal(e2.State.Phase, Phase.CavalryDeclaration);
    Accept(e2, Cmd(e2, "attack", player: 0, assignments: new() { [natsu.Id] = 0 }));
    Equal(e2.State.Cards[natsu.Id].Zone, Zone.Battlefield, "Cavalry-keyword Natsu assigns during Cavalry Maneuvers");
}
static void MirumotoReiyuForcePenalty()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var reiyu = Add(s, "oracle-l5r-5138", 0, Zone.Battlefield, province: 0); // Force 4
    var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: reiyu.Id, TargetId: target.Id, AbilityId: "reiyu-force-penalty"));
    Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 1, "Force 5 - 4 (Reiyu's current Force) = 1");
}
static void ShinjoHoribeBowOrStraighten()
{
    // Not opposed: rejected.
    var s = StrongholdBattle("oracle-l5r-1218");
    var horibe = Add(s, "oracle-l5r-6885", 0, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: horibe.Id, TargetId: horibe.Id, AbilityId: "horibe-bow"));

    // Opposed: may bow or straighten any Personality, including an enemy's.
    var s2 = StrongholdBattle("oracle-l5r-1218");
    var horibe2 = Add(s2, "oracle-l5r-6885", 0, Zone.Battlefield, province: 0);
    var enemy = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
    var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: horibe2.Id, TargetId: enemy.Id, AbilityId: "horibe-bow"));
    Check(e2.State.Cards[enemy.Id].Bowed, "Bows the target enemy Personality");
    // Bow and straighten are the same single printed ability's two effects, not two independent
    // once-per-turn abilities: having just bowed, straightening (even a different target) is spent too.
    Reject(e2, new GameCommand("useAbility", 0, e2.Version, CardId: horibe2.Id, TargetId: horibe2.Id, AbilityId: "horibe-straighten"));
}
static void DojiAtsumichiBowOrStraighten()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var atsumichi = Add(s, "oracle-l5r-2060", 0, Zone.Battlefield, province: 0); // Personal Honor 4
    var lowerHonor = Add(s, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0); // PH 2
    var equalHonor = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0); // PH 4: not lower
    var another = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // a second eligible lower-Honor target
    var e = MatchEngine.FromTestState(s);
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: atsumichi.Id, TargetId: equalHonor.Id, AbilityId: "atsumichi-bow"));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: atsumichi.Id, TargetId: lowerHonor.Id, AbilityId: "atsumichi-straighten"));
    Check(!e.State.Cards[lowerHonor.Id].Bowed, "Straightens the lower-Honor target");
    // Bow and straighten are the same single printed ability's two effects, not two independent
    // once-per-turn abilities: having just straightened, bowing (even a different target) is spent too.
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: atsumichi.Id, TargetId: another.Id, AbilityId: "atsumichi-bow"));
}
static void TetsuoDestroyAttachmentAndWeakerWeaponed()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var tetsuo = Add(s, "oracle-l5r-7943", 0, Zone.Battlefield, province: 0);
    Add(s, "training-weapon", 0, Zone.Attached, attachedTo: tetsuo.Id, province: 0); // Tetsuo carries 1 Weapon
    var fewerWeapons = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // 0 Weapons
    var notFewerWeapons = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
    Add(s, "training-weapon", 1, Zone.Attached, attachedTo: notFewerWeapons.Id, province: 0); // 1 Weapon: not fewer
    var attachment = Add(s, "training-item", 1, Zone.Attached, attachedTo: notFewerWeapons.Id, province: 0);
    var e = MatchEngine.FromTestState(s);
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: tetsuo.Id, TargetId: notFewerWeapons.Id, AbilityId: "tetsuo-destroy-weaker-weaponed"));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tetsuo.Id, TargetId: fewerWeapons.Id, AbilityId: "tetsuo-destroy-weaker-weaponed"));
    Equal(e.State.Cards[fewerWeapons.Id].Zone, Zone.Dead);
    Accept(e, new GameCommand("pass", 1, e.Version));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tetsuo.Id, TargetId: attachment.Id, AbilityId: "tetsuo-destroy-attachment"));
    Equal(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard);
}
static void MotoShunsukeForceBonusAndMakeDead()
{
    var s = StrongholdBattle("oracle-l5r-1218");
    var shunsuke = Add(s, "oracle-l5r-5371", 0, Zone.Battlefield, province: 0); // base Force 3
    Add(s, "training-crane-samurai", 1, Zone.Dead); // player 1's dead pile: 2
    Add(s, "training-crane-samurai", 1, Zone.Dead);
    Add(s, "training-lion-samurai", 0, Zone.Dead); // player 0's (Shunsuke's own) dead pile: 1
    var e = MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == shunsuke.Id).Force, 5, "Base 3 + the largest single dead pile (2)");
    // DeadCount (issue #221): the same authoritative per-player count PlayerView now exposes
    // directly, computed through the exact same shared helper Shunsuke's own Force bonus reads.
    Equal(e.GetView(0).Players[0].DeadCount, 1);
    Equal(e.GetView(0).Players[1].DeadCount, 2);
    Check(e.GetView(0).Players[1].Discard.Count(c => c.Zone == Zone.Dead) == 2, "Individually distinguishable within Discard too, by each CardView's own Zone");

    var s2 = MatchEngine.CreateEconomyScenario().State;
    s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
    var shunsuke2 = Add(s2, "oracle-l5r-5371", 0, Zone.Home);
    var discardedEnemy = Add(s2, "training-crane-samurai", 1, Zone.DynastyDiscard);
    var ownDiscarded = Add(s2, "training-lion-samurai", 0, Zone.DynastyDiscard);
    var e2 = MatchEngine.FromTestState(s2);
    Reject(e2, new GameCommand("useAbility", 0, e2.Version, CardId: shunsuke2.Id, TargetId: ownDiscarded.Id, AbilityId: "shunsuke-make-dead"));
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: shunsuke2.Id, TargetId: discardedEnemy.Id, AbilityId: "shunsuke-make-dead"));
    Equal(e2.State.Cards[discardedEnemy.Id].Zone, Zone.Dead, "Becomes honorably dead");
}
static void OverconfidentPrimitive()
{
    {
        // Enters play via Purchase: opponent offered a draw, and may accept.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-overconfident-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "overconfidentDraw");
        Equal(e.State.PendingChoice?.PlayerId, 1, "The enemy/opponent is offered the draw, never the controller");
        Equal(e.State.PriorityPlayerId, 1);
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("overconfidentDraw", 1, e.Version, Choice: "draw"));
        Equal(e.State.Players[1].Hand.Count, handBefore + 1);
        Check(e.State.PendingChoice is null);
        Equal(e.State.PriorityPlayerId, 0, "Resumes to the purchasing player -- Dynasty-phase Purchase never cedes priority on its own");
    }
    {
        // Same entry, but declined -- always optional.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-overconfident-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("overconfidentDraw", 1, e.Version, Choice: "decline"));
        Equal(e.State.Players[1].Hand.Count, handBefore, "Declining is legal");
        Check(e.State.PendingChoice is null);
    }
    {
        // Leaves play: dying while actually InPlay (here, a Ranged Attack destroying it outright)
        // queues the offer for the OTHER player -- not the performer's own controller specifically.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var target = Add(s, "training-overconfident-personality", 1, Zone.Battlefield, province: 0); // Force 2, no Followers
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: target.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead);
        Equal(e.State.PendingChoice?.Type, "overconfidentDraw");
        Equal(e.State.PendingChoice?.PlayerId, 0, "Target's own owner is player 1, so player 0 is offered");
        Accept(e, new GameCommand("overconfidentDraw", 0, e.Version, Choice: "draw"));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1);
        Check(e.State.PendingChoice is null);
    }
    {
        // A province/deck discard (DestroyProvince) never calls MarkDead -- the card was never in
        // play at all, so no offer, even though it carries the keyword.
        var s = Battle(5, 0, 0).State;
        var provinceCardId = s.Players[1].Provinces[0].CardId!;
        s.Cards[provinceCardId].DefinitionId = "training-overconfident-personality";
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[provinceCardId].Zone, Zone.DynastyDiscard, "Discarded, never entered play");
        Check(e.State.PendingOverconfidentDraws.Count == 0, "DestroyProvince never routes through MarkDead");
        Check(e.State.PendingChoice is null);
    }
    {
        // MakeDead applied to a card ALREADY sitting in a discard pile is not "leaving play" --
        // InPlay is false the whole time, so no offer, matching the user's own carve-out.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var tokenMaster = Add(s, "training-token-master", 0, Zone.Home);
        var discarded = Add(s, "training-overconfident-personality", 1, Zone.DynastyDiscard);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tokenMaster.Id, TargetId: discarded.Id, AbilityId: "training-make-dead"));
        Equal(e.State.Cards[discarded.Id].Zone, Zone.Dead);
        Check(e.State.PendingOverconfidentDraws.Count == 0, "Already discarded, not in play, when it became dead");
        Check(e.State.PendingChoice is null);
    }
    {
        // Bayushi Mugoshi: a real card with BOTH Overconfident and his own entering-play Reaction --
        // his own entryReaction window closes first (used, then both decline), and only THEN, via
        // PassReaction's own close hook, does the Overconfident offer open for the opponent.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-marketplace", 0, Zone.Home);
        var mugoshiId = PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Bayushi Mugoshi").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = mugoshiId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        var opponentHonorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "mugoshi-slander", TargetPlayerId: 1));
        Equal(e.State.Players[1].Honor, opponentHonorBefore - 1, "Slandered player loses 1 Honor");
        Equal(e.State.PendingChoice?.Type, "overconfidentDraw", "Chains into the Overconfident offer once the entry-reaction window itself closes");
        Equal(e.State.PendingChoice?.PlayerId, 1);
        Accept(e, new GameCommand("overconfidentDraw", 1, e.Version, Choice: "decline"));
        Check(e.State.PendingChoice is null);
    }
    {
        // A tie battle destroys both armies fully -- Overconfident casualties on BOTH sides queue
        // separate, sequential offers, each to the other side's own opponent.
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
        s.Players[1].Provinces[0].Strength = 6;
        var attackerUnit = Add(s, "training-overconfident-personality", 0, Zone.Battlefield, province: 0); // Force 2
        var defenderUnit = Add(s, "training-overconfident-personality", 1, Zone.Battlefield, province: 0); // Force 2, tie
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[attackerUnit.Id].Zone, Zone.Dead);
        Equal(e.State.Cards[defenderUnit.Id].Zone, Zone.Dead);
        Equal(e.State.PendingOverconfidentDraws.Count, 1, "One offer already opened; one still queued");
        Equal(e.State.PendingChoice?.Type, "overconfidentDraw");
        var firstOffered = e.State.PendingChoice!.PlayerId;
        Accept(e, new GameCommand("overconfidentDraw", firstOffered, e.Version, Choice: "decline"));
        Equal(e.State.PendingChoice?.Type, "overconfidentDraw", "A second, separate offer opens for the other player");
        Check(e.State.PendingChoice!.PlayerId != firstOffered, "Each player is offered their own opponent's casualty, not the same one twice");
        Accept(e, new GameCommand("overconfidentDraw", e.State.PendingChoice!.PlayerId, e.Version, Choice: "decline"));
        Check(e.State.PendingChoice is null);
        Equal(e.State.Phase, Phase.Dynasty, "Both offers drained; the deferred battle-resolution tail (FinishBattleReturnHomeAndTransition) still completes the phase transition");
    }
    {
        // Kitsuki Minori: suppressed entirely (in and out of play) while the OTHER player is Lion
        // Clan -- the engine's own default economy-scenario matchup (player 0 Crane, player 1 Lion).
        var s = MatchEngine.CreateEconomyScenario().State;
        Equal(s.Players[1].Clan, "Lion", "Sanity: default opponent is Lion Clan");
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var minoriId = PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Kitsuki Minori").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = minoriId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 5).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Check(e.State.PendingOverconfidentDraws.Count == 0, "Overconfident suppressed vs. a Lion opponent");
        Check(e.State.PendingChoice is null, "No own entry-reaction either, so Purchase's tail resolves with no window at all");
    }
    {
        // Same purchase, but against a non-Lion opponent -- Overconfident stands.
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[1].Clan = "Crane";
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var minoriId = PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Kitsuki Minori").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = minoriId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 5).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "overconfidentDraw", "Overconfident stands vs. a non-Lion opponent");
        Equal(e.State.PendingChoice?.PlayerId, 1);
    }
    {
        // Asahina Tsugio / Asako Kijo: raises OTHER players' own max hand size by 1 -- never the
        // controller's own. Tsugio, controlled by player 1, raises player 0's own threshold to 9.
        var s = MatchEngine.CreateEconomyScenario().State;
        var tsugioId = PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Asahina Tsugio").Id;
        Add(s, tsugioId, 1, Zone.Home);
        while (s.Players[0].Hand.Count < 9) Add(s, "training-rally", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Players[0].Hand.Count, 10, "One over the raised threshold of 9 (8 printed +1 from Tsugio's opponent) after the End Phase draw");
        Equal(e.State.PendingChoice!.Type, "discardHand");
        Equal(e.State.PendingChoice!.Prompt, "Discard 1 card(s) to return to 9.");
        Accept(e, Cmd(e, "discardHand", cards: [e.State.Players[0].Hand[0]]));
        Equal(e.State.Players[0].Hand.Count, 9);
    }
}
static void PublicHandLimitView()
{
    var state = MatchEngine.CreateEconomyScenario().State;
    var tsugio = PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Asahina Tsugio").Id;
    var baseline = MatchEngine.FromTestState(state).GetView(0);
    Check(baseline.Players.All(p => p.HandLimit is { Base: 8, Maximum: 8, Modifiers.Length: 0 }));
    var first = Add(state, tsugio, 1, Zone.Home);
    var second = Add(state, tsugio, 1, Zone.Battlefield, province: 0);
    Add(state, tsugio, 1, Zone.Hand); // Concealed copies neither modify the limit nor leak into sources.
    var engine = MatchEngine.FromTestState(state);
    foreach (var viewer in new[] { 0, 1 })
    {
        var view = engine.GetView(viewer);
        Equal(view.Players[0].HandLimit!.Maximum, 10);
        Equal(view.Players[1].HandLimit!.Maximum, 8, "The controller does not gain their own modifier");
        Equal(view.Players[0].HandLimit!.Modifiers.Length, 2);
        Check(view.Players[0].HandLimit!.Modifiers.All(m => m.CardId == first.Id || m.CardId == second.Id));
        Equal(view.Players[1 - viewer].Hand.Length, 0, "Public counts must not reveal concealed cards");
        var roundTrip = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(view))!;
        Equal(roundTrip.Players[0].HandLimit!.Maximum, 10);
        Equal(roundTrip.Players[0].HandLimit!.Modifiers[0].CardName, "Asahina Tsugio");
    }
    // Leaving play updates both public presentation and the very same End Phase threshold.
    state.Cards[first.Id].Zone = Zone.Dead;
    state.Cards[second.Id].Zone = Zone.DynastyDiscard;
    engine = MatchEngine.FromTestState(state);
    Equal(engine.GetView(0).Players[0].HandLimit!.Maximum, 8);
    while (state.Players[0].Hand.Count < 9) Add(state, "training-rally", 0, Zone.Hand);
    engine = MatchEngine.FromTestState(state);
    Accept(engine, Cmd(engine, "endDynasty"));
    var maximum = engine.GetView(0).Players[0].HandLimit!.Maximum;
    Equal(engine.State.PendingChoice!.Prompt, $"Discard {engine.State.Players[0].Hand.Count - maximum} card(s) to return to {maximum}.");
    var legacy = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(baseline.Players[0]))!.AsObject();
    legacy.Remove("HandLimit");
    Check(JsonSerializer.Deserialize<PlayerView>(legacy.ToJsonString())!.HandLimit is null, "Old snapshots report an unknown limit");
}

static void AttackMayTargetAboveStrength()
{
    {
        // Ranged 3 (Dark Naga Scouts) targeting a Force-5 Personality: accepted, leaves him alive and in place, nothing else moves.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var scouts = Add(s, "oracle-l5r-1839", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Ranged 3, unconditional
        var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5 > 3
        var e = MatchEngine.FromTestState(s);
        var honor = (e.State.Players[0].Honor, e.State.Players[1].Honor);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: scouts.Id, TargetId: strong.Id, AbilityId: "dark-naga-scouts-attack"));
        Equal(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "The Force-5 target is legal but not destroyed by Ranged 3");
        Check(!e.State.Cards[strong.Id].Bowed && e.State.Cards[strong.Id].Modifiers.Count == 0, "...and is otherwise untouched");
        Equal(e.State.Cards[scouts.Id].Zone, Zone.Attached, "The attacking Follower stays attached");
        Equal((e.State.Players[0].Honor, e.State.Players[1].Honor), honor, "Honor unchanged: this Attack has no Honor effect");
    }
    {
        // The same Attack at Force <= strength still destroys, and with no target at all it is still a legal action.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var scouts = Add(s, "oracle-l5r-1839", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var weak = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: scouts.Id, AbilityId: "dark-naga-scouts-attack"), weak.Id, strong.Id);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: scouts.Id, TargetId: weak.Id, AbilityId: "dark-naga-scouts-attack"));
        Equal(e.State.Cards[weak.Id].Zone, Zone.Dead, "Force 3 <= Ranged 3 is destroyed");
        Equal(e.State.Cards[strong.Id].Zone, Zone.Battlefield);
    }
    {
        // An above-strength target of an Attack whose action pays a cost and grants Honor: cost and Honor still apply (Carpenter Castle, Ranged 5 vs Force 6).
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var strong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: strong.Id, AbilityId: "carpenter-castle-siege"));
        Check(e.State.Cards[castle.Id].Bowed, "The bow cost is paid");
        Equal(e.State.Cards[strong.Id].Zone, Zone.Battlefield);
        Equal(e.State.Players[0].Honor, before + 2, "The Honor gain for having targeted a card still applies");
    }
}

static void CarpenterCastleAttack()
{
    {
        // Ranged/Melee Attack targeting is deferred, not required up front: the action is legal even
        // though the only enemy in play is too strong to be a legal target.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, above Ranged 5
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        // Core rule: a target whose Force is above the strength is still legal; the Attack just fails to destroy it.
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Check(e.State.Cards[castle.Id].Bowed, "Bowing the Castle/Siege card is a cost paid regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "An out-of-range enemy is a legal target but untouched");
        Equal(e.State.Players[0].Honor, before + 2, "Gains 2 Honor because the Attack targeted a card, even one above the strength");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, above Ranged 5
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, AbilityId: "carpenter-castle-siege"));
        Check(e.State.Cards[castle.Id].Bowed, "Bowing the Castle/Siege card is a cost paid regardless of whether the Attack hits");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "Untargeted Attack touches nothing");
        Equal(e.State.Players[0].Honor, before, "No Honor gain when it targeted no card");
    }
    {
        // A Personality with a Follower attached is not itself targetable; the Follower is.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, would be in range alone
        var follower = Add(s, "training-follower", 1, Zone.Attached, attachedTo: enemy.Id, province: 0); // Force 2
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        var before = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: follower.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "The targeted Follower is destroyed as an attachment, not marked Dead");
        Equal(e.State.Players[0].Honor, before + 2, "Gains 2 Honor because the Attack targeted a card");
    }
    {
        // A Personality with no Followers, at or under strength, is destroyed as a full unit (Dead).
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Presence
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, no Followers
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
        Equal(e.State.Players[0].Honor, before + 2);
    }
    {
        // The Castle/Siege card must be unbowed and carry the matching keyword.
        var s = StrongholdBattle("oracle-l5r-1218");
        var bowedCastle = Add(s, "training-castle", 0, Zone.Home, bowed: true);
        var notCastle = Add(s, "training-border-keep", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: bowedCastle.Id, AbilityId: "carpenter-castle-siege"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: notCastle.Id, AbilityId: "carpenter-castle-siege"));
    }
}
static void ShiroShibaAttack()
{
    {
        // Base Melee 4 Attack: a Force-4 enemy without Followers is destroyed; a Force-5 one is still a legal target but survives.
        var s = StrongholdBattle("oracle-l5r-7017");
        var performer = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);
        var tooStrong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, TargetId: tooStrong.Id, AbilityId: "shiro-shiba-melee")); // legal target, simply not destroyed
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, AbilityId: "shiro-shiba-melee"));
        Check(e.State.Cards[tooStrong.Id].Zone == Zone.Battlefield, "Above-strength enemy is untouched; performer does not bow as a cost");
        Check(!e.State.Cards[performer.Id].Bowed, "The performing Samurai is not itself bowed by this ability");
    }
    {
        // Controlling a Shugenja (anywhere in play, not just at this battlefield) raises strength to 6.
        var s = StrongholdBattle("oracle-l5r-7017");
        var performer = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var stillTooStrong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        Add(s, "training-item", 1, Zone.Attached, attachedTo: stillTooStrong.Id, province: 0); // +2 Force = 8, above even the boosted strength of 6
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, TargetId: stillTooStrong.Id, AbilityId: "shiro-shiba-melee"), stillTooStrong.Id); // legal target, survives
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: performer.Id, TargetId: target.Id, AbilityId: "shiro-shiba-melee"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead);
        Check(shugenja.Zone == Zone.Home, "The Shugenja need not be at this battlefield to grant the bonus");
    }
    {
        // The performer must be an unbowed Samurai at this battlefield.
        var s = StrongholdBattle("oracle-l5r-7017");
        var bowed = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var notSamurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: bowed.Id, AbilityId: "shiro-shiba-melee"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: notSamurai.Id, AbilityId: "shiro-shiba-melee"));
    }
}
// Presentation consumes the same authoritative contribution used by ResolveBattle.
static (MatchState State, CardInstance Parent, CardInstance Follower) UnitViewFixture()
{
    var (s, p, _) = ForceBattleFixture(2, 0, 20);
    var f = Add(s, "training-follower", 0, Zone.Attached, attachedTo: p.Id, province: 0);
    f.Modifiers = [new("fixture", "fixture", "Force", 3)]; // Printed 2F plus 3F gives the fixture 5F.
    return (s, p, f);
}
static CardView UnitView(MatchState s, string id) => MatchEngine.FromTestState(s).GetView(0).Players[0].Army.Single(c => c.Id == id);
static void UnitViewClamp()
{
    var (s, p, f) = UnitViewFixture(); Equal(UnitView(s, p.Id).UnitForce, 7);
    p.Modifiers.Add(new("fixture", "force-reduction", "Force", -3));
    Equal(UnitView(s, p.Id).Force, 0); Equal(UnitView(s, p.Id).UnitForce, 5);
    Equal(UnitView(s, f.Id).Force, 5); Equal(UnitView(s, f.Id).UnitForce, null);
}
static void UnitViewBows(bool followerBowed, bool personalityElite, bool followerElite, int expected)
{
    var (s, p, f) = UnitViewFixture(); p.Bowed = true; f.Bowed = followerBowed;
    if (personalityElite) p.GrantedKeywords = [new("Elite", "fixture", "Permanent")];
    if (followerElite) f.GrantedKeywords = [new("Elite", "fixture", "Permanent")];
    Equal(UnitView(s, p.Id).UnitForce, expected);
}
static void UnitViewRemoval()
{
    var (s, p, f) = UnitViewFixture(); p.Bowed = true; f.Bowed = true;
    p.GrantedKeywords = [new("Elite", "fixture", "Permanent")];
    f.GrantedKeywords = [new("Elite", "fixture", "Permanent")];
    Equal(UnitView(s, p.Id).UnitForce, 7);
    f.Zone = Zone.FateDiscard; f.AttachedTo = null; Equal(UnitView(s, p.Id).UnitForce, 2);
    p.Zone = Zone.Home; p.ProvinceIndex = null;
    Equal(MatchEngine.FromTestState(s).GetView(0).Players[0].Home.Single(c => c.Id == p.Id).UnitForce, 2);
}
static void UnitViewResolution()
{
    var (s, p, f) = UnitViewFixture(); Add(s, "training-item", 0, Zone.Attached, bowed: true, attachedTo: p.Id, province: 0);
    Equal(UnitView(s, p.Id).Force, 4); Equal(UnitView(s, p.Id).UnitForce, 9);
    var e = MatchEngine.FromTestState(s);
    Resolve(e); AssertBattleForces(e, 9, 0);
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == p.Id).UnitForce, 0, "Returning bowed units must refresh the projection");
}
static void UnitViewPrivacy()
{
    var e = MatchEngine.CreateStarterMatch(); var v = e.GetView(1);
    Check(v.Players.SelectMany(p => p.Provinces).All(p => p.Card?.UnitForce is null));
    Check(v.Players.SelectMany(p => p.Hand).All(c => c.UnitForce is null));
    Equal(v.Players[0].Hand.Length, 0); Equal(v.Players[0].Stronghold.UnitForce, null);
    var (s, p, f) = UnitViewFixture(); p.Zone = Zone.Dead; f.Zone = Zone.FateDiscard; f.AttachedTo = null;
    Check(MatchEngine.FromTestState(s).GetView(0).Players[0].Discard.All(c => c.UnitForce is null));
    var payload = System.Text.Json.Nodes.JsonNode.Parse(Json(UnitView(UnitViewFixture().State, UnitViewFixture().Parent.Id)))!.AsObject();
    payload.Remove("UnitForce"); Equal(JsonSerializer.Deserialize<CardView>(payload.ToJsonString())!.UnitForce, null);
}

static void BowedPersonalityResolution(int owner, bool provinceDestroyed, int attackingForce, int defendingForce)
{
    var s = Battle(1, 1, 6).State;
    var p = s.Cards.Values.Single(c => c.OwnerId == owner && c.Zone == Zone.Battlefield);
    var opposing = s.Cards.Values.Single(c => c.OwnerId == 1 - owner && c.Zone == Zone.Battlefield); // untouched, Force 1
    p.DefinitionId = PublishedContent.TestSnapshot.Cards.Single(d => d.Name == "Akodo Kisho").Id; // Force 3, Chi 3
    p.Modifiers.Clear(); p.Bowed = true;
    var guide = PublishedContent.TestSnapshot.Cards.Single(d => d.Name == "Native Guide").Id;
    Add(s, guide, owner, Zone.Attached, attachedTo: p.Id, province: 0);
    Add(s, guide, owner, Zone.Attached, attachedTo: p.Id, province: 0);
    var e = MatchEngine.FromTestState(s);
    var bowedCards = e.GetView(owner).Players[owner].Army;
    var card = bowedCards.Single(c => c.Id == p.Id);
    // Bowed Personality's OWN Force is suppressed (0), but its two unbowed Native Guides (5F
    // each) still contribute -- issue #216 correctness fix, so this unit's own UnitForce is 10,
    // not 0, and now beats rather than loses to the opposing 1F unit.
    Equal(card.Force, 3); Equal(card.Chi, 3); Equal(card.UnitForce, 10);
    Resolve(e);
    Check(e.State.Cards[p.Id].Zone != Zone.Dead, "The bowed Personality's own side wins (10 vs 1) thanks to its unbowed Followers -- it is not destroyed");
    Equal(e.State.Cards[opposing.Id].Zone, Zone.Dead, "The 1F opposing unit loses instead");
    // Only an ATTACKER's win can destroy the DEFENDER's own province -- owner 0 is the attacker
    // here (Battle()'s own convention), so only that iteration's own win (10 vs 1, with a
    // Province Strength of 6) destroys it (10 > 1+6).
    Equal(e.State.Players[1].Provinces[0].Destroyed, provinceDestroyed);
    AssertBattleForces(e, attackingForce, defendingForce);
    p.Bowed = false;
    var ready = MatchEngine.FromTestState(s).GetView(owner).Players[owner].Army.Single(c => c.Id == p.Id);
    Equal(ready.Force, 3); Equal(ready.Chi, 3); Equal(ready.UnitForce, 13);
    if (owner == 0 && Environment.GetEnvironmentVariable("L5R_UNIT_VIEW_FIXTURE") is { Length: > 0 } output)
        File.WriteAllText(output, Json(new[] { bowedCards, MatchEngine.FromTestState(s).GetView(owner).Players[owner].Army }));
}

static void BattleJournalProjection()
{
    var state = MatchEngine.CreateEconomyScenario().State;
    state.Phase = Phase.Battle; state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.PriorityPlayerId = 1;
    state.Cards.Add("public-attacker", new CardInstance { Id = "public-attacker", DefinitionId = "training-crane-veteran", OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true });
    state.Cards.Add("public-defender", new CardInstance { Id = "public-defender", DefinitionId = "training-lion-veteran", OwnerId = 1, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true, Modifiers = [new("fixture", "fixture", "Force", -5)] });
    var engine = MatchEngine.FromTestState(state); var journal = new BattleJournal(); var before = engine.GetView(0);
    journal.Observe(before, null); var revision = journal.Revision;
    journal.Observe(before, before); Check(journal.Revision == revision, "Rerender reinserted events.");
    Check(engine.Execute(new("pass", 1, engine.Version)).Accepted, "First pass rejected.");
    var intermediate = engine.GetView(0); journal.Observe(intermediate, before);
    Check(engine.Execute(new("pass", 0, engine.Version)).Accepted, "Second pass rejected.");
    var after = engine.GetView(0); journal.Observe(after, intermediate);
    Check(journal.Exchanges.Count(e => e.Events[0].Type == "passed") == 2, "Lost either player's pass.");
    var resolution = journal.Exchanges.Single(e => e.Events[0].Type == "battleResolved");
    Check(resolution.Events.Any(e => e.Type == "unitDestroyed"), "Destruction separated from its resolution cause.");
    Check(journal.Aftermath.Any(r => r.Destination == "Dead") && journal.Aftermath.Any(r => r.Destination == "Home · bowed"), "Aftermath lost actual destinations.");
    var count = journal.Exchanges.Count; journal.Observe(after, after); Check(count == journal.Exchanges.Count, "Duplicate outcome on rerender.");
    var restored = new BattleJournal(); restored.Observe(after, null);
    Check(restored.Aftermath.Count == 0, "Reconnect invented historical movement destinations.");
    Check(restored.Exchanges.SelectMany(x => x.Events).All(e => after.Events.Contains(e)), "Journal invented a public event.");
    restored.Reset(); Check(restored.Exchanges.Count == 0 && restored.LastSequence == -1, "New match retained previous history.");
}
static void SpellsAndShugenja()
{
    {
        // Not offered at all (not merely disabled) without a Spell in hand, even with an eligible Shugenja.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "equip-battle"), "No Spell in hand: Equip (Battle) should not be offered");
    }
    {
        // Not offered without an opposed unbowed Shugenja, even with a Spell in hand.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "equip-battle"), "No opposed unbowed Shugenja: Equip (Battle) should not be offered");
    }
    {
        // A successful equip attaches the Spell (paying its cost) and grants an Additional Action
        // (issue #181) scoped to it, keeping priority with the equipping player. No Reaction is
        // available in this scenario, so it resolves immediately without opening a window.
        var s = StrongholdBattle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a => a.Command?.AbilityId == "equip-battle"), "Eligible Shugenja and Spell: Equip (Battle) should be offered");
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell.Id, SourceIds: payment.SourceIds));
        Equal(e.State.Cards[spell.Id].Zone, Zone.Attached); Equal(e.State.Cards[spell.Id].AttachedTo, shugenja.Id);
        Equal(e.State.Cards[spell.Id].ProvinceIndex, 0);
        Check(!e.State.Players[0].Hand.Contains(spell.Id), "Spell removed from hand");
        Check(e.State.PendingChoice is null, "No Reaction available to disrupt it -- resolves immediately");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds!.Contains(spell.Id)), "Grants an Additional Action scoped to the just-attached Spell");
        Equal(e.State.PriorityPlayerId, 0, "Priority stays with the equipping player -- the granted action is still available");
    }
    {
        // Using the Spell's own Battle ability consumes the granted Additional Action and hands
        // priority onward once it resolves.
        var s = StrongholdBattle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell.Id, SourceIds: payment.SourceIds));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, AbilityId: "training-spell-force-boost", TargetId: shugenja.Id));
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Grant consumed");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == shugenja.Id).Force, 3, "+2 Force applied on top of base Force 1");
        Equal(e.State.PriorityPlayerId, 1, "Priority hands onward once the granted action is used up");
    }
    {
        // Additional Actions may be passed (issue #181): passing forfeits the grant rather than using
        // it, same as an ordinary Battle-phase pass.
        var s = StrongholdBattle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell.Id, SourceIds: payment.SourceIds));
        Accept(e, new GameCommand("pass", 0, e.Version));
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Passing forfeits the granted Additional Action");
        Equal(e.State.PriorityPlayerId, 1);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == shugenja.Id).Force, 1, "Declined: no Force bonus applied");
    }
    {
        // Tireless (any number of times per turn) and scoped to exactly the Spell it was granted for
        // (issue #181): a second, independent Equip (Battle) succeeds immediately -- still holding
        // priority, since it creates its own fresh grant -- but forfeits the first Spell's still-
        // unused one as an unrelated action, rather than letting it linger.
        var s = StrongholdBattle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var spell1 = Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var spell2 = Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var payment1 = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell1.Id, SourceIds: payment1.SourceIds));
        var payment2 = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell2.Id, SourceIds: payment2.SourceIds));
        Equal(e.State.Cards[spell2.Id].Zone, Zone.Attached, "Tireless: usable again the same turn, still holding priority");
        Equal(e.State.PriorityPlayerId, 0);
        Check(!e.State.AdditionalActions.Any(g => g.ScopeCardIds!.Contains(spell1.Id)), "Spell1's grant is forfeited, not carried over");
        Check(e.State.AdditionalActions.Any(g => g.ScopeCardIds!.Contains(spell2.Id)), "Spell2's own fresh grant is still there");
    }
}
static void UniqueAndSingularInPlayRestriction()
{
    {
        // Unique (issue #202): recruiting a second copy of an already-controlled Unique Personality
        // is rejected -- Unique's own in-play restriction had no training fixture or test at all
        // before this. A DIFFERENT card is unaffected.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-unique-personality", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-unique-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, cardId);
        Check(!quote.ProclaimEligible, "Not eligible: already controls this Unique card");
        Equal(quote.Error, "You already control this Unique card.");
    }
    {
        // Singular (issue #202): the same in-play restriction, but -- unlike Unique -- with no
        // deck-construction limit (DeckFormat.cs is unaffected by Singular; not exercised here).
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-singular-personality", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-singular-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, cardId);
        Check(!quote.ProclaimEligible, "Not eligible: already controls this Singular card");
        Equal(quote.Error, "You already control this Singular card.");
    }
    {
        // Once the in-play copy leaves play (here, simply never having entered it), recruiting is
        // legal again -- the restriction is about a copy currently in play, not about having owned
        // one ever.
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-singular-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, cardId);
        Check(quote.ProclaimEligible, "No copy in play yet: eligible");
        Equal(quote.Error, null);
    }
    {
        // Unique/Singular via the ordinary Equip() path (issue #202): a Follower already controlled
        // in play cannot be attached a second time -- Equip had no such check at all before this fix.
        var s = MatchEngine.CreateStarterMatch().State;
        var host1 = Add(s, "training-crane-samurai", 0, Zone.Home);
        var host2 = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "training-unique-follower", 0, Zone.Attached, attachedTo: host1.Id);
        var second = Add(s, "training-unique-follower", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: second.Id, sources: e.PaymentOptions(0, 1).First().SourceIds, target: host2.Id));
    }
    {
        var s = MatchEngine.CreateStarterMatch().State;
        var host1 = Add(s, "training-crane-samurai", 0, Zone.Home);
        var host2 = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "training-singular-follower", 0, Zone.Attached, attachedTo: host1.Id);
        var second = Add(s, "training-singular-follower", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: second.Id, sources: e.PaymentOptions(0, 1).First().SourceIds, target: host2.Id));
    }
    {
        // Unique/Singular via Equip (Battle) (issue #202): a Spell already controlled in play cannot
        // be attached a second time either -- equip-battle had no such check at all before this fix.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var shugenja1 = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var shugenja2 = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-singular-spell", 0, Zone.Attached, province: 0, attachedTo: shugenja1.Id);
        var second = Add(s, "training-singular-spell", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja2.Id, TargetId: second.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds));
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var shugenja1 = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var shugenja2 = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-unique-spell", 0, Zone.Attached, province: 0, attachedTo: shugenja1.Id);
        var second = Add(s, "training-unique-spell", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja2.Id, TargetId: second.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds));
    }
}
static void LoyalKeyword()
{
    {
        // A Loyal Personality recruits normally for a matching Clan.
        var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-loyal-crane";
        e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, cardId).Error is null, "A Loyal Crane Personality recruits normally for a Crane player");
    }
    {
        // A Loyal Personality is not recruitable by a Clan it is not aligned with.
        var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
        s.Players[0].Clan = "Lion"; var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-loyal-crane";
        e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, cardId).Error is not null, "A Crane-Loyal Personality should not be recruitable by a Lion player");
        Reject(e, Cmd(e, "purchase", id: cardId, sources: []));
    }
    {
        // A Personality aligned with more than one Clan (Clans overrides the single Clan field) may be
        // recruited by either -- Loyal restricts to "a Clan it is aligned with," not only the first.
        var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
        s.Players[0].Clan = "Lion"; var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-loyal-multi-clan";
        e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, cardId).Error is null, "A Crane/Lion dual-aligned Loyal Personality recruits normally for a Lion player");
    }
    {
        var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-loyal-multi-clan";
        e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, cardId).Error is null, "A Crane/Lion dual-aligned Loyal Personality recruits normally for a Crane player");
    }
    {
        // Removing the last Clan affiliation makes a Loyal Personality Unaligned. Unaligned is not a
        // tenth Clan: any player, including one with an Unaligned Stronghold, may take control.
        foreach (var playerClan in new[] { "Lion", "Unaligned" })
        {
            var e = MatchEngine.CreateEconomyScenario(); var s = e.State; s.Players[0].Clan = playerClan;
            var cardId = s.Players[0].Provinces[0].CardId!; var card = s.Cards[cardId];
            card.DefinitionId = "training-loyal-crane"; card.RemovedClanAffiliations.Add("Crane");
            e = MatchEngine.FromTestState(s);
            Check(e.QuotePurchase(0, cardId).Error is null, $"An Unaligned Loyal Personality may be controlled by a {playerClan} player");
        }
    }
    {
        // Removing only one of several affiliations leaves the others in force.
        var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
        var cardId = s.Players[0].Provinces[0].CardId!; var card = s.Cards[cardId];
        card.DefinitionId = "training-loyal-multi-clan"; card.RemovedClanAffiliations.Add("Crane");
        s.Players[0].Clan = "Lion"; e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, cardId).Error is null, "A Loyal Personality remains aligned with its unremoved Clan");
        s = e.State; s.Players[0].Clan = "Crane"; e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, cardId).Error is not null, "A removed Clan affiliation no longer satisfies Loyal");
    }
    {
        // The derived Unaligned keyword is visible to every ordinary keyword-based rule check.
        var s = StrongholdBattle("oracle-l5r-1218");
        var dairuko = Add(s, "oracle-l5r-10635", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-loyal-crane", 1, Zone.Battlefield, province: 0);
        target.RemovedClanAffiliations.Add("Crane");
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: dairuko.Id, TargetId: target.Id, AbilityId: "dairuko-exp-bow"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "A Personality with no Clan affiliations has the Unaligned keyword");
    }
}
static void ExperiencedKeywordAndOverlay()
{
    Equal(Experienced.Level([]), 0, "No Experienced keyword means level 0");
    Equal(Experienced.Level(["Experienced"]), 1, "Experienced without a number means level 1");
    Equal(Experienced.Level(["Experienced 5"]), 5, "Experienced N supplies level N");
    Equal(Experienced.Level(["Inexperienced"]), 0, "Inexperienced is not the Experienced keyword");
    Equal(Experienced.BaseTitle("Mirumoto Yozo - exp2"), "Mirumoto Yozo", "Imported Experience suffixes are not part of the in-game title");

    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].Clan = "Scorpion"; s.Players[0].Honor = 0;
    var higherId = s.Players[0].Provinces[0].CardId!; var higher = s.Cards[higherId];
    higher.DefinitionId = "training-paneki-exp3"; higher.FaceUp = true;
    var lower = Add(s, "training-paneki-base", 0, Zone.Home, bowed: true);
    lower.Dishonorable = true; lower.StraightenBlockedTurn = s.TurnNumber; lower.AssignedToAttackTurn = s.TurnNumber;
    lower.Modifiers = [new("old-effect", "ongoing", "Force", 2)];
    lower.GrantedKeywords = [new("Cavalry", "old-effect", "Permanent")];
    lower.RemovedKeywords = ["Samurai"];
    lower.RemovedClanAffiliations = ["Scorpion"];
    lower.Protections = [new("old-effect", "Turn")];
    lower.Tokens = [new("token-overlay", "Fire", "old-effect", "Force", 1)];
    lower.AbilityUses["old-once-per-turn"] = s.TurnNumber;
    var follower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: lower.Id);
    var item = Add(s, "training-item", 0, Zone.Attached, attachedTo: lower.Id);
    var initiallyUnbowed = s.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).Order().ToArray();

    var e = MatchEngine.FromTestState(s);
    var quote = e.QuoteOverlay(0, higher.Id, lower.Id);
    Check(quote.Error is null, quote.Error ?? "Overlay should be legal");
    Equal(quote.HigherLevel, 3); Equal(quote.LowerLevel, 0);
    Check(e.QueryActions(0).Any(a => a.Type == "overlay" && a.Enabled && a.Command?.CardId == higher.Id && a.Command.TargetId == lower.Id), "Overlay is offered as an executable Dynasty action");
    Accept(e, new GameCommand("overlay", 0, e.Version, CardId: higher.Id, TargetId: lower.Id, SourceIds: []));

    var state = e.State; var replacement = state.Cards[higher.Id]; var removed = state.Cards[lower.Id];
    Equal(replacement.Zone, Zone.Home); Check(replacement.Bowed, "Bowed state transfers"); Check(replacement.Dishonorable, "Dishonorable state transfers");
    Equal(replacement.Modifiers.Single().Amount, 2); Equal(replacement.Tokens.Single().Id, "token-overlay");
    Check(replacement.GrantedKeywords.Any(k => k.Keyword == "Cavalry" && k.Duration == "Permanent"), "Granted keyword transfers");
    Check(replacement.RemovedKeywords.Contains("Samurai"), "Removed keyword transfers");
    Check(replacement.RemovedClanAffiliations.Contains("Scorpion"), "Removed Clan affiliation transfers");
    Check(replacement.Protections.Any(p => p.Source == "old-effect"), "Protection transfers");
    Equal(replacement.StraightenBlockedTurn, state.TurnNumber); Equal(replacement.AssignedToAttackTurn, state.TurnNumber);
    Equal(replacement.AbilityUses["old-once-per-turn"], state.TurnNumber);
    Check(state.Cards[follower.Id].AttachedTo == higher.Id && state.Cards[item.Id].AttachedTo == higher.Id, "Every attachment moves to the overlaying Personality");
    Equal(removed.Zone, Zone.RemovedFromGame); Check(removed.Tokens.Count == 0 && removed.Modifiers.Count == 0 && removed.GrantedKeywords.Count == 0, "Transferred state is not duplicated on the removed card");
    Check(initiallyUnbowed.SequenceEqual(state.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).Order()), "No Gold source was bowed");

    // Levels need only be higher, not sequential: Experience 5 may replace Experience 3 directly.
    var exp5Id = state.Players[0].Provinces[1].CardId!; state.Cards[exp5Id].DefinitionId = "training-paneki-exp5"; state.Cards[exp5Id].FaceUp = true;
    e = MatchEngine.FromTestState(state); quote = e.QuoteOverlay(0, exp5Id, higher.Id);
    Check(quote.Error is null, quote.Error ?? "Experience 5 should overlay Experience 3"); Equal(quote.LowerLevel, 3); Equal(quote.HigherLevel, 5);
    Accept(e, new GameCommand("overlay", 0, e.Version, CardId: exp5Id, TargetId: higher.Id, SourceIds: []));
    state = e.State; Equal(state.Cards[exp5Id].Zone, Zone.Home); Equal(state.Cards[higher.Id].Zone, Zone.RemovedFromGame);
    Check(state.Cards[follower.Id].AttachedTo == exp5Id && state.Cards[item.Id].AttachedTo == exp5Id, "Attachments follow repeated non-sequential overlays");

    // Real imported catalogue names keep their -exp notation for deck construction, while Overlay
    // still recognizes them as versions of the same titled Personality.
    var legacyHigherId = state.Players[0].Provinces[2].CardId!; state.Cards[legacyHigherId].DefinitionId = "oracle-l5r-10287"; state.Cards[legacyHigherId].FaceUp = true;
    var legacyLower = Add(state, "oracle-l5r-5178", 0, Zone.Home);
    e = MatchEngine.FromTestState(state); quote = e.QuoteOverlay(0, legacyHigherId, legacyLower.Id);
    Check(quote.Error is null, quote.Error ?? "Imported Experienced titles should overlay"); Equal(quote.LowerLevel, 1); Equal(quote.HigherLevel, 2);
}
static void KeywordSatisfaction()
{
    {
        // Target-side: "Choose your performing Berserker" -- a Personality carrying the Berserker
        // keyword (with no mechanical ability of its own) can be chosen and straightened; one without
        // it cannot, even though nothing else about it differs.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0;
        var satisfier = Add(s, "training-keyword-satisfier", 0, Zone.Home);
        var berserker = Add(s, "training-berserker", 0, Zone.Home, bowed: true);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: satisfier.Id, AbilityId: "training-keyword-conditional-straighten", TargetId: other.Id));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: satisfier.Id, AbilityId: "training-keyword-conditional-straighten", TargetId: berserker.Id));
        Check(!e.State.Cards[berserker.Id].Bowed, "The chosen Berserker is straightened");
    }
    {
        // Performer-side: "Choose your performing Berserker at the current battlefield" -- the same
        // gate, mirrored onto Performer selection instead of Target selection.
        var s = StrongholdBattle("oracle-l5r-1218");
        var satisfier = Add(s, "training-keyword-satisfier", 0, Zone.Battlefield, province: 0);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: satisfier.Id, AbilityId: "training-keyword-conditional-force-boost", PerformerId: other.Id));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: satisfier.Id, AbilityId: "training-keyword-conditional-force-boost", PerformerId: berserker.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == berserker.Id).Force, 4, "Base Force 2 + 2 from the boost");
    }
}
static void StrategyWave1KeywordCards()
{
    {
        // Immovable Object's first ability, the keyword-satisfaction primitive's own worked example:
        // usable via the Open route (dual Battle/Open timing) with no battle in progress at all.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0;
        var io = Add(s, "oracle-l5r-3671", 0, Zone.Hand);
        var berserker = Add(s, "training-berserker", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: io.Id, TargetId: berserker.Id, AbilityId: "immovable-object-straighten"));
        Check(!e.State.Cards[berserker.Id].Bowed, "Straightened via the Open route, with no battle in progress");
    }
    {
        // Battle/Open presence primitive (issue #267): the SAME first ability, used during the
        // Battle phase instead, now behaves exactly like an ordinary Battle ability -- the performing
        // Berserker must actually be at the current battlefield.
        var s = StrongholdBattle("oracle-l5r-1218");
        var io = Add(s, "oracle-l5r-3671", 0, Zone.Hand);
        var berserker = Add(s, "training-berserker", 0, Zone.Home, bowed: true); // not at the battlefield
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: io.Id, TargetId: berserker.Id, AbilityId: "immovable-object-straighten"));
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var io = Add(s, "oracle-l5r-3671", 0, Zone.Hand);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, bowed: true, province: 0); // at the battlefield
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: io.Id, TargetId: berserker.Id, AbilityId: "immovable-object-straighten"));
        Check(!e.State.Cards[berserker.Id].Bowed, "Straightened via the Battle route, performer present");
    }
    {
        // Immovable Object's second ability: Battle-timing, bows an enemy card with lower Force.
        var s = StrongholdBattle("oracle-l5r-1218");
        var io = Add(s, "oracle-l5r-3671", 0, Zone.Hand);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0); // Force 2
        var weak = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); weak.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)]; // Force 1
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: io.Id, PerformerId: berserker.Id, TargetId: weak.Id, AbilityId: "immovable-object-bow"));
        Check(e.State.Cards[weak.Id].Bowed, "Lower-Force enemy is bowed");
    }
    {
        // Blind Rage: +5F to the performing unbowed Berserker, Lose 2 Honor.
        var s = StrongholdBattle("oracle-l5r-1218");
        var blindRage = Add(s, "oracle-l5r-1044", 0, Zone.Hand);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0); // Force 2
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: blindRage.Id, PerformerId: berserker.Id, AbilityId: "blind-rage"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == berserker.Id).Force, 7, "Base Force 2 + 5");
        Equal(e.State.Players[0].Honor, honorBefore - 2);
    }
    {
        // Chokehold: performing unbowed Monk bows an enemy Personality with equal or lower Chi.
        var s = StrongholdBattle("oracle-l5r-1218");
        var chokehold = Add(s, "oracle-l5r-1336", 0, Zone.Hand);
        var monk = Add(s, "training-monk", 0, Zone.Battlefield, province: 0); // Chi 2
        var target = Add(s, "training-dishonored-samurai", 1, Zone.Battlefield, province: 0); // Chi 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: chokehold.Id, PerformerId: monk.Id, TargetId: target.Id, AbilityId: "chokehold"));
        Check(e.State.Cards[target.Id].Bowed, "Equal Chi satisfies 'equal or lower'");
    }
    {
        // Unorthodox Attack: reuses the pre-existing Samurai Performer and enemy-force Target
        // categories entirely -- no new engine pieces at all.
        var s = StrongholdBattle("oracle-l5r-1218");
        var unorthodox = Add(s, "oracle-l5r-8999", 0, Zone.Hand);
        var samurai = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: unorthodox.Id, PerformerId: samurai.Id, TargetId: enemy.Id, AbilityId: "unorthodox-attack"));
        Check(e.State.Cards[enemy.Id].Bowed);
    }
}
static void FollowerWave2Cards()
{
    {
        // Plain Ranged/Melee Attack Followers: no cost, no condition, matching UnconditionalAttackImportWave.
        foreach (var (cardId, abilityId, strength) in new[] { ("oracle-l5r-1839", "dark-naga-scouts-attack", 3), ("oracle-l5r-3456", "horse-archers-attack", 4) })
        {
            var s = StrongholdBattle("oracle-l5r-1218");
            var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var performer = Add(s, cardId, 0, Zone.Attached, province: 0, attachedTo: host.Id);
            var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
            var e = MatchEngine.FromTestState(s);
            AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: performer.Id, TargetId: target.Id, AbilityId: abilityId), target.Id);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: performer.Id, AbilityId: abilityId));
            Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, $"{cardId}: strength {strength} misses Force 5");
        }
    }
    {
        // Cavalry Flankers: only the Attacker may use it, and it bows itself as part of the attack.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var flankers = Add(s, "oracle-l5r-10333", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); target.Modifiers = [new("test-fixture", "test-fixture", "Force", -1)]; // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: flankers.Id, TargetId: target.Id, AbilityId: "cavalry-flankers-attack"));
        Check(e.State.Cards[flankers.Id].Bowed, "Bows itself as part of the attack");
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Melee 2 destroys Force 2");
        var s2 = StrongholdBattle("oracle-l5r-1218"); s2.ActivePlayerId = 1; s2.PriorityPlayerId = 0;
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var flankers2 = Add(s2, "oracle-l5r-10333", 0, Zone.Attached, province: 0, attachedTo: host2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, CardId: flankers2.Id, AbilityId: "cavalry-flankers-attack"));
    }
    {
        // Murderous Thieves: bows itself, no other condition.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var thieves = Add(s, "oracle-l5r-5445", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: thieves.Id, AbilityId: "murderous-thieves-attack"));
        Check(e.State.Cards[thieves.Id].Bowed);
    }
    {
        // Veteran Advisor: draws a card, no cost.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var advisor = Add(s, "oracle-l5r-9144", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: advisor.Id, AbilityId: "veteran-advisor-draw"));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1);
    }
    {
        // Hired Legion: bows itself, draws a card.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var legion = Add(s, "oracle-l5r-3277", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: legion.Id, AbilityId: "hired-legion-draw"));
        Check(e.State.Cards[legion.Id].Bowed); Equal(e.State.Players[0].Hand.Count, handBefore + 1);
    }
    {
        // Naga Remnants: bows a target enemy attachment.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var remnants = Add(s, "oracle-l5r-10513", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemyHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var enemyItem = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: remnants.Id, TargetId: enemyItem.Id, AbilityId: "naga-remnants-bow"));
        Check(e.State.Cards[enemyItem.Id].Bowed);
    }
    {
        // Hiruma Sniper: only usable with Reconnaissance at the current battlefield.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sniper = Add(s, "oracle-l5r-3325", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: sniper.Id, TargetId: target.Id, AbilityId: "hiruma-sniper-attack"));

        var s2 = StrongholdBattle("oracle-l5r-1218");
        s2.Players[1].Provinces[0].Reconnaissance[0] = true;
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sniper2 = Add(s2, "oracle-l5r-3325", 0, Zone.Attached, province: 0, attachedTo: host2.Id);
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); target2.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)]; // Force 2
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: sniper2.Id, TargetId: target2.Id, AbilityId: "hiruma-sniper-attack"));
        Equal(e2.State.Cards[target2.Id].Zone, Zone.Dead, "Ranged 4 destroys Force 2 with Reconnaissance");
    }
    {
        // Heavy Chargers: only usable while opposed.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var chargers = Add(s, "oracle-l5r-9869", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var hand = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: chargers.Id, TargetId: hand.Id, AbilityId: "heavy-chargers-boost"));
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var chargers = Add(s, "oracle-l5r-9869", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var hand = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus 3
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: chargers.Id, TargetId: hand.Id, AbilityId: "heavy-chargers-boost"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == chargers.Id).Force, 6, "Base Force 3 + 3");
    }
    {
        // Spitting Llama: -3F to a target enemy Personality or Follower.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var llama = Add(s, "oracle-l5r-10686", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: llama.Id, TargetId: target.Id, AbilityId: "spitting-llama-force"));
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 3, "Base Force 6 - 3");
    }
    {
        // Akodo Regulars: discards a card, boosts its own Force by the discard's Focus Value.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var regulars = Add(s, "oracle-l5r-262", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var hand = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: regulars.Id, TargetId: hand.Id, AbilityId: "akodo-regulars-boost"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == regulars.Id).Force, 5, "Base Force 2 + 3");
    }
}
static void FollowerWave3Cards()
{
    {
        // Commander's Steed: AttachGrantsKeyword's first published use -- Conqueror. Cleanup on detach
        // is already covered generically by "Destroying an attachment clears the Attachment-duration
        // keyword it granted", so this only needs to prove the grant itself.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var steed = Add(s, "oracle-l5r-10893", 0, Zone.Hand);
        var human = Add(s, "training-crane-samurai", 0, Zone.Home);
        var nonhuman = Add(s, "training-nonhuman-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: steed.Id, sources: e.PaymentOptions(0, 4).First().SourceIds, target: nonhuman.Id));
        Accept(e, Cmd(e, "equip", id: steed.Id, sources: e.PaymentOptions(0, 4).First().SourceIds, target: human.Id));
        Check(e.State.Cards[human.Id].GrantedKeywords.Any(g => g.Keyword == "Conqueror"), "Attaching grants Conqueror to the wielder");
    }
    {
        // Seppun Heavy Elite (issue #179 correction): grants its wielder Elite's EFFECT (Force still
        // counts while bowed) WITHOUT granting the Elite keyword itself -- unlike Commander's Steed's
        // genuine keyword grant above, EliteEffect() reads AttachGrantsEliteEffect live off the
        // attachment, so no GrantedKeywords entry is needed or expected here.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); // Force 3, bowed
        Add(s, "oracle-l5r-6596", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == host.Id).UnitForce, 5, "Bowed, but Seppun's granted Elite EFFECT still contributes to BattleForce: host's 3 + Seppun's own unbowed 2");
        Check(!e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Elite"), "The wielder gains Elite's effect, not the Elite keyword itself");
    }
    {
        // Family Sensei / Merchant Guard: self-referential, ControlsKeyword reuse.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var sensei = Add(s, "oracle-l5r-10592", 0, Zone.Attached, attachedTo: host.Id); // Force 2
        var guard = Add(s, "oracle-l5r-10589", 0, Zone.Attached, attachedTo: host.Id); // Force 2
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == sensei.Id).Force, 2, "No Dojo controlled -- base Force only");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == guard.Id).Force, 2, "No Market controlled -- base Force only");
        Add(s, "training-dojo", 0, Zone.Home); Add(s, "training-market-holding", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Equal(e2.GetView(0).Players[0].Home.Single(c => c.Id == sensei.Id).Force, 3, "Controls a Dojo -- +1F");
        Equal(e2.GetView(0).Players[0].Home.Single(c => c.Id == guard.Id).Force, 3, "Controls a Market -- +1F");
    }
    {
        // Ashigaru Guards: +1F for every two Farms controlled.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var guards = Add(s, "oracle-l5r-615", 0, Zone.Home); // Force 0
        var e0 = MatchEngine.FromTestState(s);
        Equal(e0.GetView(0).Players[0].Home.Single(c => c.Id == guards.Id).Force, 0, "No Farms -- no bonus");
        Add(s, "training-farm", 0, Zone.Home); Add(s, "training-farm", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Equal(e2.GetView(0).Players[0].Home.Single(c => c.Id == guards.Id).Force, 1, "Two Farms -- +1F");
        Add(s, "training-farm", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s);
        Equal(e3.GetView(0).Players[0].Home.Single(c => c.Id == guards.Id).Force, 1, "Three Farms -- still +1F (integer division)");
    }
    {
        // Brothers of the Great Lake: self-count of same-named cards in the SAME ARMY, plus its own bow ability.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var lake1 = Add(s, "oracle-l5r-10332", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Force 3
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == lake1.Id).Force, 3, "Alone -- no bonus");
        var lake2 = Add(s, "oracle-l5r-10332", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e2 = MatchEngine.FromTestState(s);
        Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == lake1.Id).Force, 4, "One other same-named card in the army -- +1F");
        var enemyTarget = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, new GameCommand("useAbility", 0, e3.Version, CardId: lake1.Id, TargetId: enemyTarget.Id, AbilityId: "brothers-great-lake-bow"));
        Check(e3.State.Cards[enemyTarget.Id].Bowed, "Bows the target enemy card");
    }
    {
        // Ninja Guards: Force bonus equal to its wielder's own PRINTED Force.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-lion-veteran", 0, Zone.Home); // printed Force 6
        var guards = Add(s, "oracle-l5r-5565", 0, Zone.Attached, attachedTo: host.Id); // base Force 0
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == guards.Id).Force, 6, "Base 0 + wielder's printed Force 6");
    }
    {
        // Disciples of Ganesh: "+2F while defending" (self, existing shape) plus a destruction-triggered
        // Honor gain (new shape) -- one card, two engine hooks off the same Effect string.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; // player 0 defends
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ganesh = Add(s, "oracle-l5r-10895", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Force 1
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == ganesh.Id).Force, 3, "Base 1 + 2 while defending");
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        s.PriorityPlayerId = 1;
        var e2 = MatchEngine.FromTestState(s);
        var honorBefore = e2.State.Players[0].Honor;
        Accept(e2, new GameCommand("useAbility", 1, e2.Version, CardId: destroyer.Id, TargetId: ganesh.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e2.State.Cards[ganesh.Id].Zone, Zone.FateDiscard, "Destroyed during battle -- a Follower, so its own FateDiscard, not Dead (issue #223's own review: only a Personality actually becomes dead)");
        Equal(e2.State.Players[0].Honor, honorBefore + 1, "Its own controller gains 1 Honor from the destruction");
    }
    {
        // Daifuke: swaps its own Force with the target's, symmetrically.
        var s = StrongholdBattle("oracle-l5r-1218"); s.Phase = Phase.Action; s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var daifuke = Add(s, "oracle-l5r-10235", 0, Zone.Attached, attachedTo: host.Id); // Force 3
        var enemyHost = Add(s, "training-lion-veteran", 1, Zone.Home); // Force 6
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: daifuke.Id, TargetId: enemyHost.Id, AbilityId: "daifuke-swap-force"));
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == daifuke.Id).Force, 6, "Daifuke now has the enemy's old Force");
        Equal(e.GetView(1).Players[1].Home.Single(c => c.Id == enemyHost.Id).Force, 3, "The enemy now has Daifuke's old Force");
    }
    {
        // Moto Gunso: +1F to every OTHER Follower at the current battlefield AND each adjacent one.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var gunso = Add(s, "oracle-l5r-5320", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Force 2
        var sameBattlefield = Add(s, "training-follower", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Force 2
        var adjacentHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1);
        var adjacent = Add(s, "training-follower", 0, Zone.Attached, province: 1, attachedTo: adjacentHost.Id); // Force 2, province 1 (adjacent to 0)
        var farHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 3);
        var far = Add(s, "training-follower", 0, Zone.Attached, province: 3, attachedTo: farHost.Id); // Force 2, province 3 (NOT adjacent to 0)
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: gunso.Id, AbilityId: "moto-gunso-boost"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == gunso.Id).Force, 2, "Not boosted -- 'other' Followers only");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == sameBattlefield.Id).Force, 3, "Same battlefield -- +1F");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == adjacent.Id).Force, 3, "Adjacent battlefield -- +1F");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == far.Id).Force, 2, "Not adjacent -- unaffected");
    }
    {
        // Elite Sentry: "destroy without attachments" (existing #158 shape) and a Tireless straighten of
        // its own unit (wielder plus every attachment).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var sentry = Add(s, "oracle-l5r-2304", 0, Zone.Attached, province: 0, attachedTo: host.Id); // unbowed -- its own ability needs to use it
        var otherFollower = Add(s, "training-follower", 0, Zone.Attached, province: 0, attachedTo: host.Id, bowed: true);
        var enemyNaked = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sentry.Id, TargetId: enemyNaked.Id, AbilityId: "elite-sentry-destroy"));
        Equal(e.State.Cards[enemyNaked.Id].Zone, Zone.Dead, "Destroys the unattached enemy card");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sentry.Id, AbilityId: "elite-sentry-straighten"));
        Check(!e.State.Cards[host.Id].Bowed && !e.State.Cards[otherFollower.Id].Bowed, "Straightens the whole unit: wielder and every attachment");
    }
    {
        // Furugaro: destroys itself and gives the target Fudo two Charging tokens.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var furugaro = Add(s, "oracle-l5r-10236", 0, Zone.Attached, attachedTo: host.Id);
        var fudo = Add(s, "training-fudo-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: furugaro.Id, TargetId: fudo.Id, AbilityId: "furugaro-charge"));
        Equal(e.State.Cards[furugaro.Id].Zone, Zone.FateDiscard, "Destroys itself -- a Follower, so its own FateDiscard, not Dead");
        Equal(e.State.Cards[fudo.Id].Tokens.Count(t => t.Type == "Charging"), 2, "Gives the target Fudo two Charging tokens");
    }
    {
        // P'an Ku Cultist: MANDATORY entry Honor loss (issue #210 -- no printed "Reaction:" prefix,
        // so it applies automatically on entry, unlike an actual optional Reaction) and a separate
        // Limited ability giving a Madness token.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var hand = Add(s, "oracle-l5r-10590", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Cmd(e, "equip", id: hand.Id, sources: e.PaymentOptions(0, 3).First().SourceIds, target: host.Id));
        Equal(e.State.Players[0].Honor, honorBefore - 1, "Entering play costs 1 Honor, automatically -- no choice offered");
        Check(e.State.PendingChoice is null, "No entering-play Reaction of its own left to offer");
        // panku-madness is a separate Limited ability, tested independently of the entry-reaction window.
        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var cultist = Add(s2, "oracle-l5r-10590", 0, Zone.Home);
        var target = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: cultist.Id, TargetId: target.Id, AbilityId: "panku-madness"));
        Check(e2.State.Cards[target.Id].Tokens.Any(t => t.Type == "Madness"), "Gives the target a Madness token");
    }
    {
        // Soshi House Guard: conditional bow (only during a Combat Segment) plus a dishonorable-conditional
        // Honor loss.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var guard = Add(s, "oracle-l5r-7347", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var dishonorable = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); dishonorable.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: guard.Id, TargetId: dishonorable.Id, AbilityId: "soshi-conditional-bow"));
        Check(e.State.Cards[dishonorable.Id].Bowed, "Bows the target during the Battle phase");
        Equal(e.State.Players[1].Honor, honorBefore - 1, "Dishonorable target's controller loses 1 Honor");
    }
    {
        // Unseen Assassins: bows itself, destroys a target Follower, then destroys itself and loses 2 Honor.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var assassin = Add(s, "oracle-l5r-10410", 0, Zone.Attached, attachedTo: host.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Home);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, attachedTo: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: assassin.Id, TargetId: enemyFollower.Id, AbilityId: "unseen-assassin-strike"));
        Equal(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "Destroys the target Follower -- FateDiscard, not Dead");
        Equal(e.State.Cards[assassin.Id].Zone, Zone.FateDiscard, "Destroys itself -- FateDiscard, not Dead");
        Equal(e.State.Players[0].Honor, honorBefore - 2, "Loses 2 Honor");
    }
    {
        // Asahina House Guard: straighten a target bowed Personality, gain 1 Honor.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-clan-samurai", 0, Zone.Battlefield, province: 0); // Crane Clan keyword
        var guard = Add(s, "oracle-l5r-494", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var bowedAlly = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: guard.Id, TargetId: bowedAlly.Id, AbilityId: "asahina-straighten"));
        Check(!e.State.Cards[bowedAlly.Id].Bowed, "Straightens the target");
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Gains 1 Honor");
    }
    {
        // Bounty Hunter: bows a target attachment OR a target dishonorable Personality.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hunter = Add(s, "oracle-l5r-1119", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyAttachment = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: hunter.Id, TargetId: enemyHost.Id, AbilityId: "bounty-hunter-bow")); // honorable Personality, not an attachment
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: hunter.Id, TargetId: enemyAttachment.Id, AbilityId: "bounty-hunter-bow"));
        Check(e.State.Cards[enemyAttachment.Id].Bowed, "Bows the target attachment");
    }
    {
        // Rumormonger: bows itself, targets a Personality whose controller controls a dishonorable
        // Personality (not necessarily the target itself), Force penalty equal to the target's own base PH.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
        var rumormonger = Add(s, "oracle-l5r-6404", 1, Zone.Home);
        var dishonorableAlly = Add(s, "training-crane-clan-samurai", 0, Zone.Home); dishonorableAlly.Dishonorable = true; // PH 3, current 0
        var otherAlly = Add(s, "training-crane-clan-low-honor", 0, Zone.Home); // Force 2, PH 1
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: rumormonger.Id, TargetId: otherAlly.Id, AbilityId: "rumormonger-penalty"));
        Check(e.State.Cards[rumormonger.Id].Bowed, "Bows itself as part of the cost");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == otherAlly.Id).Force, 1, "Force penalty equal to the target's own base Personal Honor (1): base 2 - 1");
    }
    {
        // Seasoned Ashigaru: immune to Melee Attacks specifically (not Ranged).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "oracle-l5r-10106", 0, Zone.Attached, province: 0, attachedTo: host.Id); // Horseback Warriors, Melee
        var seasoned = Add(s, "oracle-l5r-10901", 1, Zone.Battlefield, province: 0); // Force 1, melee-immune
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: seasoned.Id, AbilityId: "horseback-warriors-attack"));
    }
    {
        // Horseback Warriors: Melee 4, or 5 against a target without the Cavalry keyword ("Infantry").
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var warriors = Add(s, "oracle-l5r-10106", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var infantry = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); infantry.Modifiers = [new("test", "test", "Force", 1)]; // Force 5
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: warriors.Id, TargetId: infantry.Id, AbilityId: "horseback-warriors-attack"));
        Equal(e.State.Cards[infantry.Id].Zone, Zone.Dead, "Melee 5 (target is Infantry -- no Cavalry keyword) destroys Force 5");
    }
    {
        // Khol Regulars: Ranged 4 Attack, and its own Reaction raises a pending attack's strength by 2
        // while it is at the current battlefield.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var regulars = Add(s, "oracle-l5r-4338", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-attack-reactor", 1, Zone.Home); // the opponent still holds a Reaction, so the window stays open to read the strength (issue #644)
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, within base Ranged 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: regulars.Id, TargetId: target.Id, AbilityId: "khol-regulars-attack"));
        Equal(e.State.PendingAttack?.Strength, 4, "Base Ranged 4");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: regulars.Id, AbilityId: "khol-regulars-reaction"));
        Equal(e.State.PendingAttack?.Strength, 6, "4 base + 2 from its own Reaction");
        Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds nothing more, so one pass closes the window
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead);
    }
    {
        // Aulus: entry Honor loss (own amount, -2) and a keyword-based unit-scoped boost ("Legionnaires
        // in this unit have +1F") -- reuses UnitFollowerBoost's existing switch with a new case.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var aulus = Add(s, "oracle-l5r-10687", 0, Zone.Attached, attachedTo: host.Id);
        var legionnaire = Add(s, "training-legionnaire-follower", 0, Zone.Attached, attachedTo: host.Id); // Force 1
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == legionnaire.Id).Force, 2, "Base 1 + 1 from Aulus's unit-scoped boost");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == aulus.Id).Force, 3, "Aulus itself is not a Legionnaire -- unaffected");
    }
}
static void KeywordMechanics()
{
    {
        // Expendable: a mandatory Fate-deck draw when destroyed by any effect -- not merely discarded.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var expendable = Add(s, "training-expendable-follower", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: expendable.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e.State.Cards[expendable.Id].Zone, Zone.FateDiscard, "Destroyed via DestroyUnits' own top-level path -- a Follower, so FateDiscard, not Dead");
        Equal(e.State.Players[1].Hand.Count, handBefore + 1, "Mandatory Fate-deck draw on destruction");
    }
    {
        // Expendable does NOT trigger on a plain discard (the discardHand path never calls
        // DestroyUnits/DestroyAttachment) -- proving the Destroyed-vs-Discarded distinction. Needs a
        // hand of exactly 9 (one over the limit) so discardHand's own count check accepts discarding
        // just the one Expendable card.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        while (s.Players[0].Hand.Count < 8) Add(s, "training-item", 0, Zone.Hand);
        var expendable = Add(s, "training-expendable-follower", 0, Zone.Hand);
        Equal(s.Players[0].Hand.Count, 9, "Test setup: exactly one over the eight-card hand limit");
        s.PendingChoice = new("discardHand", 0, null, "Discard 1 card(s) to return to eight.");
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("discardHand", 0, e.Version, CardIds: [expendable.Id]));
        Equal(e.State.Cards[expendable.Id].Zone, Zone.FateDiscard, "Discarded");
        Equal(e.State.Players[0].Hand.Count, handBefore - 1, "No extra draw -- a plain discard, not a destroy");
    }
    {
        // Destined: a mandatory Fate-deck draw every time it enters play -- exercised via Purchase
        // (from a province) and separately via Equip (a Follower attaching).
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-destined-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1, "Purchase (from a province): mandatory draw");

        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var destinedFollower = Add(s2, "training-destined-follower", 0, Zone.Hand);
        var wielder = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var handBefore2 = e2.State.Players[0].Hand.Count;
        Accept(e2, Cmd(e2, "equip", id: destinedFollower.Id, sources: e2.PaymentOptions(0, 2).First().SourceIds, target: wielder.Id));
        Equal(e2.State.Players[0].Hand.Count, handBefore2, "Hand lost the Follower itself, then gained the Destined draw -- net unchanged");
    }
    {
        // Kharmic: pay 2 Gold, discard a Kharmic Dynasty card from a province -- immediately refilled
        // face up (unlike a normal end-of-turn refill, which stays face down). Player abilities need a
        // published (non-ScenarioOnly) Stronghold, same requirement Tactical Advantage's own test meets.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-kharmic-dynasty"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var deckBefore = e.State.Players[0].DynastyDeck.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: cardId, AbilityId: "kharmic-discard", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Equal(e.State.Cards[cardId].Zone, Zone.DynastyDiscard, "The Kharmic card is discarded");
        var newId = e.State.Players[0].Provinces[0].CardId;
        Check(newId is not null && newId != cardId, "The province is refilled with a different card");
        Check(e.State.Cards[newId!].FaceUp, "Immediately face up, unlike a normal end-of-turn refill");
        Equal(e.State.Players[0].DynastyDeck.Count, deckBefore - 1, "One card left the Dynasty deck to refill the province");
    }
    {
        // Kharmic: pay 2 Gold, discard a Kharmic Fate card from hand -- an extra draw follows.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        var kharmicFate = Add(s, "training-kharmic-fate", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: kharmicFate.Id, AbilityId: "kharmic-discard", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Equal(e.State.Cards[kharmicFate.Id].Zone, Zone.FateDiscard, "Discarded from hand");
        Equal(e.State.Players[0].Hand.Count, handBefore, "Hand lost the discarded card, then gained an extra draw -- net unchanged");
    }
    {
        // Invest (Clay Kshatriya's shape): a separate, optional, standalone payment as the card
        // enters play, reusing the same own-enters-play Reaction window every entry effect shares --
        // genuinely optional, so declining it is legal too. Clay Kshatriya is a Follower, entering via
        // Equip (not Purchase, which is Personality/Holding-only).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var invest = Add(s, "training-invest-follower", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "equip", id: invest.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: host.Id));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: invest.Id, AbilityId: "training-invest", SourceIds: [])); // 0 Gold offered, needs 3
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: invest.Id, AbilityId: "training-invest", SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
        Equal(e.State.Cards[invest.Id].Tokens.Count(t => t.Type == "Invest"), 1, "Invest paid: one +1F token");

        // Declining is equally legal: no tokens, no Gold spent.
        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var invest2 = Add(s2, "training-invest-follower", 0, Zone.Hand);
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Cmd(e2, "equip", id: invest2.Id, sources: e2.PaymentOptions(0, 2).First().SourceIds, target: host2.Id));
        Accept(e2, new GameCommand("declineReaction", 0, e2.Version));
        Equal(e2.State.Cards[invest2.Id].Tokens.Count, 0, "Declined -- no Invest tokens");
    }
}
static void PassiveAttackDefenseAndEntryWatch()
{
    {
        // Destrier's shape: Scope "Self" -- protects only the card itself, both Attack kinds.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var attackerHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "oracle-l5r-3456", 0, Zone.Attached, province: 0, attachedTo: attackerHost.Id); // Horse Archers: Ranged 4, unconditional
        var defenderHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var defended = Add(s, "training-attack-defense-self", 1, Zone.Attached, province: 0, attachedTo: defenderHost.Id); // Force 2
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: defended.Id, AbilityId: "horse-archers-attack"), defended.Id); // effective 4-3=1 < Force 2: a legal target, but it survives
    }
    {
        // Singh Remnants' shape: Scope "Unit", Ranged only -- protects every card in the unit, but a
        // Melee Attack against the same target is unaffected.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var attackerHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var rangedAttacker = Add(s, "oracle-l5r-3456", 0, Zone.Attached, province: 0, attachedTo: attackerHost.Id); // Ranged 4
        var meleeAttackerHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var meleeAttacker = Add(s, "oracle-l5r-10333", 0, Zone.Attached, province: 0, attachedTo: meleeAttackerHost.Id); // Cavalry Flankers: Melee 2, Attacker-only, bows self
        var defenderHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var guard = Add(s, "training-attack-defense-unit", 1, Zone.Attached, province: 0, attachedTo: defenderHost.Id);
        var otherFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: defenderHost.Id); // Force 2, different card, same unit
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: rangedAttacker.Id, TargetId: otherFollower.Id, AbilityId: "horse-archers-attack"), otherFollower.Id); // Ranged: effective 4-3=1 < Force 2, protects the WHOLE unit (legal target, survives)
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: meleeAttacker.Id, TargetId: otherFollower.Id, AbilityId: "cavalry-flankers-attack")); // Melee: unaffected, strength 2 = Force 2
        Equal(e.State.Cards[otherFollower.Id].Zone, Zone.FateDiscard, "Melee Attacks are not reduced -- Singh Remnants' shape only names Ranged (a destroyed Follower attachment lands in the Fate discard pile)");
    }
    {
        // An Item's own default scope (no "unit" language) conveys to its wielder, not the Item itself.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var attackerHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "oracle-l5r-10333", 0, Zone.Attached, province: 0, attachedTo: attackerHost.Id); // Cavalry Flankers: Melee 2
        var wielder = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        wielder.Modifiers = [new("test", "test", "Force", -1)]; // Force 2, matching the Melee 2 strength exactly
        Add(s, "training-attack-defense-wielder", 1, Zone.Attached, province: 0, attachedTo: wielder.Id); // -2 Melee, Scope Wielder
        var otherFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: wielder.Id); // Force 2, same unit but not the wielder
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: wielder.Id, AbilityId: "cavalry-flankers-attack")); // effective 2-2=0 < Force 2 -- protects the wielder
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: otherFollower.Id, AbilityId: "cavalry-flankers-attack")); // the OTHER Follower in the same unit is unaffected
        Equal(e.State.Cards[otherFollower.Id].Zone, Zone.FateDiscard, "Wielder scope does not protect the rest of the unit");
    }
    {
        // Singh Remnants' shape: from hand, attach to a Personality as it enters play (via Purchase),
        // following Equip's own restrictions (Personal Honor Requirement included).
        var s = MatchEngine.CreateEconomyScenario().State;
        var lowHonorId = s.Players[0].Provinces[0].CardId!;
        s.Cards[lowHonorId].DefinitionId = "training-crane-clan-low-honor"; s.Cards[lowHonorId].FaceUp = true; // PH 1 -- fails the Requirement
        var watcher = Add(s, "training-personality-entry-watcher", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: lowHonorId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "proclaim", "In-Clan recruit: the Proclaim-eligible window doubles as this entry's reaction window");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: watcher.Id, AbilityId: "training-personality-entry-watch", SourceIds: e.PaymentOptions(0, 2).First().SourceIds)); // PH 1 < Requirement 3

        var s2 = MatchEngine.CreateEconomyScenario().State;
        var highHonorId = s2.Players[0].Provinces[0].CardId!;
        s2.Cards[highHonorId].DefinitionId = "training-crane-clan-samurai"; s2.Cards[highHonorId].FaceUp = true; // PH 3 -- meets the Requirement
        var watcher2 = Add(s2, "training-personality-entry-watcher", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var honorBefore = e2.State.Players[0].Honor;
        var payment2 = e2.PaymentOptions(0, 5).First(); // training-crane-clan-samurai costs 5
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: highHonorId, SourceIds: payment2.SourceIds));
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: watcher2.Id, AbilityId: "training-personality-entry-watch", SourceIds: e2.PaymentOptions(0, 2).First().SourceIds));
        Equal(e2.State.Cards[watcher2.Id].Zone, Zone.Attached, "Attached to the just-entered Personality");
        Equal(e2.State.Cards[watcher2.Id].AttachedTo, highHonorId);
        Equal(e2.State.Players[0].Honor, honorBefore + 1, "Gains 1 Honor");
    }
    {
        // The same reaction also fires from a discard pile, not only from hand.
        var s = MatchEngine.CreateEconomyScenario().State;
        var highHonorId = s.Players[0].Provinces[0].CardId!;
        s.Cards[highHonorId].DefinitionId = "training-crane-clan-samurai"; s.Cards[highHonorId].FaceUp = true;
        var watcher = Add(s, "training-personality-entry-watcher", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: highHonorId, SourceIds: e.PaymentOptions(0, 5).First().SourceIds)); // training-crane-clan-samurai costs 5
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: watcher.Id, AbilityId: "training-personality-entry-watch", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Equal(e.State.Cards[watcher.Id].Zone, Zone.Attached, "Attaches from a discard pile too, not only from hand");
    }
}
static void GrantsKeywordWave()
{
    {
        // Scourge of the Sea (issue #179): a pure "This Personality has X" grant with no ability of
        // its own -- AttachGrantsKeyword's second published carrier after Commander's Steed.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var scourge = Add(s, "oracle-l5r-6501", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "equip", id: scourge.Id, sources: e.PaymentOptions(0, 9).First().SourceIds, target: host.Id)); // costs 9 -- every producer in the economy scenario, combined
        Check(e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Naval"), "Attaching grants Naval to the wielder");
    }
    {
        // Unparalleled Sword (issue #179): grants Kensai (unlocking the second Weapon slot) plus a
        // printed Battle Melee 4 attack -- both reuse already-existing primitives. Added directly to a
        // raw MatchState (not via the real Equip() command, already proven for Scourge of the Sea
        // above), so the grant is applied by hand exactly as Equip() would.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sword = Add(s, "oracle-l5r-10124", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        host.GrantedKeywords.Add(new GrantedKeyword("Kensai", sword.Id, "Attachment"));
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sword.Id, TargetId: enemy.Id, AbilityId: "unparalleled-sword-attack")); // Melee 4 >= Force 3
    }
    {
        // GrantKeyword idempotency (issue #179): a Personality that already carries a keyword (printed
        // here) does not gain a redundant second grant of the same keyword from an attachment.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var sword = Add(s, "oracle-l5r-10124", 0, Zone.Hand);
        var host = Add(s, "training-kensai", 0, Zone.Home); // already has printed Kensai
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "equip", id: sword.Id, sources: e.PaymentOptions(0, 7).First().SourceIds, target: host.Id)); // costs 7
        Check(!e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Kensai"), "Already has Kensai printed -- no redundant grant");
    }
    {
        // Justice of the Crane (issue #179): grants Magistrate plus a Battle Dishonor action. The
        // "additional action if the printed Magistrate keyword" bonus is deferred (no general
        // "additional action" primitive yet, same gap as Effortless Counterattack in #177).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sword = Add(s, "oracle-l5r-10528", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        host.GrantedKeywords.Add(new GrantedKeyword("Magistrate", sword.Id, "Attachment"));
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!enemy.Dishonorable);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sword.Id, TargetId: enemy.Id, AbilityId: "justice-of-crane-dishonor"));
        Check(e.State.Cards[enemy.Id].Dishonorable, "Dishonors the target");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Only granted Magistrate (not printed): no additional-action bonus (issue #181)");
    }
    {
        // Justice of the Crane's own deferred bonus (#179/#180), now wired (#181): "if this Personality
        // has the PRINTED Magistrate keyword" -- checked against training-magistrate-personality's own
        // printed Keywords, distinct from the grant Justice of the Crane itself also applies.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-magistrate-personality", 0, Zone.Battlefield, province: 0);
        var sword = Add(s, "oracle-l5r-10528", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sword.Id, TargetId: enemy.Id, AbilityId: "justice-of-crane-dishonor"));
        Check(e.State.Cards[enemy.Id].Dishonorable, "Dishonors the target");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is null), "Printed Magistrate: grants an unscoped additional action");
        Equal(e.State.PriorityPlayerId, 0, "Priority stays with the wielder's controller");
    }
}
static void AdditionalActionPrimitive()
{
    {
        // Again! (issue #181): straightens a bowed Kensai and his Weapons, resets the Weapons' own
        // Battle abilities so they may be used again this turn, and grants an unscoped Additional
        // Action -- keeping priority with the same player.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0, bowed: true);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id, bowed: true);
        weapon.AbilityUses["training-weapon-attack"] = s.TurnNumber;
        var enemy = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0).Id); // Force 2, matching the Weapon's Melee 2
        var again = Add(s, "oracle-l5r-127", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: again.Id, PerformerId: kensai.Id, AbilityId: "again-boost"));
        Check(!e.State.Cards[kensai.Id].Bowed, "Straightens the Kensai"); Check(!e.State.Cards[weapon.Id].Bowed, "Straightens his Weapons");
        Check(!e.State.Cards[weapon.Id].AbilityUses.ContainsKey("training-weapon-attack"), "The Weapon's Battle ability may be used again this turn");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is null), "Grants an unscoped additional Battle action");
        Equal(e.State.PriorityPlayerId, 0, "Priority stays with the same player");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon.Id, TargetId: enemy.Id, AbilityId: "training-weapon-attack")); // Melee 2 = Force 2, consumes the grant
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Grant consumed");
        Equal(e.State.PriorityPlayerId, 1, "Priority hands onward once the granted action is used up");
    }
    {
        // Two Hands' scoping (issue #181): the two granted actions are restricted to cards in the
        // Kensai's own unit -- a card outside it is an ordinary action (and forfeits both grants).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id);
        var outsider = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var outsiderWeapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: outsider.Id);
        var enemyHost = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id); // Force 2, matching the Weapon's Melee 2
        var twoHands = Add(s, "oracle-l5r-10175", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: twoHands.Id, PerformerId: kensai.Id, AbilityId: "two-hands-grant"));
        Equal(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 2, "Grants two additional Battle actions");
        Check(e.State.AdditionalActions.All(g => g.ScopeCardIds!.Contains(kensai.Id) && g.ScopeCardIds.Contains(weapon.Id)), "Scoped to the Kensai and his Weapon");
        // A card outside the unit is an ordinary action: succeeds, but is not scoped by the grant, so
        // both of it forfeit rather than lingering.
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: outsiderWeapon.Id, TargetId: enemy.Id, AbilityId: "training-weapon-attack"));
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Outside the unit: forfeits both grants rather than consuming one");
        Equal(e.State.PriorityPlayerId, 1, "An ordinary action correctly cedes priority");
    }
    {
        // Two Hands' own two grants ARE usable, one after another, by different cards in the unit.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0);
        var weapon1 = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id);
        var weapon2 = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id);
        var enemyHost = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var enemy1 = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id); // Force 2, matching the Weapon's Melee 2
        var enemy2 = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id); // a second, separate target -- the first is destroyed by the first attack
        var twoHands = Add(s, "oracle-l5r-10175", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: twoHands.Id, PerformerId: kensai.Id, AbilityId: "two-hands-grant"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon1.Id, TargetId: enemy1.Id, AbilityId: "training-weapon-attack"));
        Equal(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 1, "One of the two grants remains");
        Equal(e.State.PriorityPlayerId, 0, "Still holding priority for the second one");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon2.Id, TargetId: enemy2.Id, AbilityId: "training-weapon-attack"));
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0));
        Equal(e.State.PriorityPlayerId, 1, "Both used up: priority cedes normally");
    }
    {
        // Two Hands' restriction: "other actions will not give you new additional Battle actions this
        // battle" -- Again!'s OTHER effects (straighten, reset Weapon usage) still fully apply, only
        // its own grant is suppressed.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0); // Two Hands needs an unbowed performer
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id, bowed: true);
        weapon.AbilityUses["training-weapon-attack"] = s.TurnNumber;
        var twoHands = Add(s, "oracle-l5r-10175", 0, Zone.Hand);
        var again = Add(s, "oracle-l5r-127", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: twoHands.Id, PerformerId: kensai.Id, AbilityId: "two-hands-grant"));
        Check(e.State.NoNewAdditionalActionsThisBattle);
        // Again! is a different card, outside Two Hands' own scope -- an unrelated action, so it also
        // forfeits Two Hands' still-unused grants (same rule proven above), on top of never adding one
        // of its own. Its OTHER effects (the Weapon-ability reset here; the Kensai's own straighten is
        // already proven above) still fully apply -- only the grant itself is suppressed.
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: again.Id, PerformerId: kensai.Id, AbilityId: "again-boost"));
        Check(!e.State.Cards[weapon.Id].AbilityUses.ContainsKey("training-weapon-attack"), "Again!'s Weapon-ability reset still applies");
        // Issue #1115: the grant that never arrives is not silent -- the player is told which card withheld it.
        Check(e.State.Events.Any(ev => ev.Type == BarNotice.EventType && ev.PlayerId == 0 && ev.Message == "Taking a new additional Battle action is barred by Two Hands."), "Two Hands tells the player it withheld Again!'s grant");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Again!'s own grant is suppressed by Two Hands' restriction");
    }
    {
        // Effortless Counterattack's Reaction (issue #181): negates a Battle action taken as an
        // Additional Action before it resolves, opening the same before-additional-action-resolves
        // window an out-of-play Reaction needs.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0, bowed: true);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id, bowed: true);
        var enemyHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id); // Force 2 -- would be destroyed by the Weapon's Melee 2
        var again = Add(s, "oracle-l5r-127", 0, Zone.Hand);
        var counter = Add(s, "oracle-l5r-2283", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: again.Id, PerformerId: kensai.Id, AbilityId: "again-boost"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon.Id, TargetId: enemy.Id, AbilityId: "training-weapon-attack"));
        Equal(e.State.PendingChoice?.Type, "additionalActionReaction", "A Reaction is available to disrupt it -- deferred rather than resolving immediately");
        Equal(e.State.PriorityPlayerId, 1, "The opponent gets the opportunity to react");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: counter.Id, AbilityId: "effortless-counterattack-negate"));
        Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Accept(e, new GameCommand("declineReaction", 1, e.Version));
        Check(e.State.PendingChoice is null, "Window closed");
        Check(e.State.PendingAdditionalAction is null);
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Attached, "Negated: the attack never actually resolved");
        Check(!e.State.Players[1].Hand.Contains(counter.Id), "Effortless Counterattack discarded after use");
    }
    {
        // Effortless Counterattack's own plain Battle ability: unrelated to the Reaction above.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var performer = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var counter = Add(s, "oracle-l5r-2283", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: counter.Id, PerformerId: performer.Id, TargetId: target.Id, AbilityId: "effortless-counterattack-move"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Home, "Moved home");
    }
    {
        // Battle-scoped: Two Hands' restriction survives a mere pass (unlike its own grants, forfeited
        // immediately -- see the scoping test above) but not into a later battle, matching AshigaruBoost's
        // own "for the remainder of the battle" precedent -- cleared at FinishBattleResolution.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0);
        var twoHands = Add(s, "oracle-l5r-10175", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: twoHands.Id, PerformerId: kensai.Id, AbilityId: "two-hands-grant"));
        Check(e.State.NoNewAdditionalActionsThisBattle);
        // Mendon's item 2 (issue #501): a pass by the holder takes ONE of his two grants as a pass (he keeps the priority), a second pass the other; one player's two passes count as one.
        Accept(e, new GameCommand("pass", 0, e.Version));
        Check(e.State.AdditionalActions.Count == 1 && e.State.PriorityPlayerId == 0, "The first pass takes one grant as a pass; the holder keeps the priority");
        Accept(e, new GameCommand("pass", 0, e.Version));
        Check(!e.State.AdditionalActions.Any() && e.State.PriorityPlayerId == 1 && e.State.Phase == Phase.Battle, "The second pass takes the other; the battle is not over (only one player has passed)");
        Check(e.State.NoNewAdditionalActionsThisBattle, "But the restriction itself survives a pass");
        Accept(e, new GameCommand("pass", 1, e.Version)); // second consecutive pass: resolves the battle
        Check(!e.State.NoNewAdditionalActionsThisBattle, "Cleared once the battle actually resolves");
    }
}
static void StalwartKeyword()
{
    {
        // Stalwart (issue #184): the first time an enemy's effect would bow it each turn, the bow is
        // negated (and only the bow -- nothing else about the effect is touched); a second, independent
        // enemy-bow effect the same turn succeeds normally. Two separate source instances of the same
        // per-instance (not per-player) ability isolate this from the ordinary once-per-turn dedupe.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var stalwart = Add(s, "training-stalwart-personality", 1, Zone.Home);
        var source1 = Add(s, "training-bow-enemy-source", 0, Zone.Home);
        var source2 = Add(s, "training-bow-enemy-source", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source1.Id, TargetId: stalwart.Id, AbilityId: "training-bow-enemy"));
        Check(!e.State.Cards[stalwart.Id].Bowed, "First enemy bow this turn is negated");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source2.Id, TargetId: stalwart.Id, AbilityId: "training-bow-enemy"));
        Check(e.State.Cards[stalwart.Id].Bowed, "Second enemy bow this turn succeeds");
    }
    {
        // Never negates the card's own controller bowing it -- only "an enemy's effect" ever qualifies.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var stalwart = Add(s, "training-stalwart-personality", 0, Zone.Home);
        var source = Add(s, "training-bow-any-source", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, TargetId: stalwart.Id, AbilityId: "training-bow-any"));
        Check(e.State.Cards[stalwart.Id].Bowed, "Own controller bowing it is never negated");
    }
    {
        // Resets at end phase (each player's own, regardless of whose turn) -- modeled here as a stale
        // marker from an earlier TurnNumber, matching AbilityUses' own per-turn convention this reuses;
        // TurnNumber already increments at the end of every player's own turn (FinishTurn).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var stalwart = Add(s, "training-stalwart-personality", 1, Zone.Home);
        stalwart.AbilityUses["stalwart-negate"] = s.TurnNumber - 1;
        var source = Add(s, "training-bow-enemy-source", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, TargetId: stalwart.Id, AbilityId: "training-bow-enemy"));
        Check(!e.State.Cards[stalwart.Id].Bowed, "A stale marker from an earlier turn does not block this turn's negation");
    }
    {
        // Real card (issue #184): Shiba Jikaro, pure Stalwart reminder text.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var jikaro = Add(s, "oracle-l5r-10244", 1, Zone.Home);
        var source = Add(s, "training-bow-enemy-source", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, TargetId: jikaro.Id, AbilityId: "training-bow-enemy"));
        Check(!e.State.Cards[jikaro.Id].Bowed, "Shiba Jikaro negates the first enemy bow this turn");
    }
}
static void ReserveKeyword()
{
    {
        // Reserve (issue #184), Dynasty side: an absent battle action -- the recruiting player needs
        // no presence of their own at this battlefield (only the reserve card, still in its province,
        // and the enemy's own presence there), and it goes straight to the battlefield. Also proves the
        // action is available even when it is NOT the recruiting player's own active turn (ActivePlayerId
        // is the opponent's here), matching "an absent battle action" -- just no Proclaim in that case.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0;
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "training-reserve-personality"; s.Cards[reserveId].FaceUp = true;
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: payment.SourceIds));
        Equal(e.State.Cards[reserveId].Zone, Zone.Battlefield, "Recruited directly to the battlefield");
        Equal(e.State.Cards[reserveId].ProvinceIndex, 0);
        Check(e.State.Players[0].Provinces[0].CardId is not null, "Province refilled");
        Check(!e.State.Cards[e.State.Players[0].Provinces[0].CardId!].FaceUp, "Refilled face down, as normal");
        Check(e.State.PendingChoice is null, "Not the recruiting player's own active turn: no Proclaim window");
        Equal(e.State.PriorityPlayerId, 1, "Priority cedes to the opponent, like any Battle action");
    }
    {
        // Face down: cannot be activated.
        var s = StrongholdBattle("oracle-l5r-1218");
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "training-reserve-personality"; s.Cards[reserveId].FaceUp = false;
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
    }
    {
        // No battlefield at this province at all: rejected.
        var s = StrongholdBattle("oracle-l5r-1218"); s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [1];
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "training-reserve-personality"; s.Cards[reserveId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
    }
    {
        // A battlefield exists at the province, but the enemy has no presence there: rejected.
        var s = StrongholdBattle("oracle-l5r-1218");
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "training-reserve-personality"; s.Cards[reserveId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
    }
    {
        // Reserve, Fate side: a non-Spell Item attaches via Equip (Battle), following the exact same
        // rules as a Spell -- but does NOT grant the bonus Additional Action for its own Battle ability.
        var s = StrongholdBattle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var reserveItem = Add(s, "training-reserve-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a => a.Command?.AbilityId == "equip-battle"), "A Reserve Item in hand: Equip (Battle) should be offered");
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: reserveItem.Id, SourceIds: payment.SourceIds));
        Equal(e.State.Cards[reserveItem.Id].Zone, Zone.Attached);
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "Non-Spell Reserve attachment: no bonus Additional Action grant, despite having a Battle ability");
    }
    {
        // Real cards (issue #184): Hida Kurabi (Dynasty Destined/Reserve) recruited as an absent battle
        // action, and Floating Reserve (Fate Reserve Follower) attached via Equip (Battle).
        var s = StrongholdBattle("oracle-l5r-1218");
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "oracle-l5r-10832"; s.Cards[reserveId].FaceUp = true; // Hida Kurabi
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: e.PaymentOptions(0, 6).First().SourceIds));
        Equal(e.State.Cards[reserveId].Zone, Zone.Battlefield, "Hida Kurabi recruited from Reserve");
        Equal(e.State.Players[0].Hand.Count, handBefore + 1, "Destined's own mandatory draw fires on entering play, Reserve recruitment included");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var floatingReserve = Add(s, "oracle-l5r-10896", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 2).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: floatingReserve.Id, SourceIds: payment.SourceIds));
        Equal(e.State.Cards[floatingReserve.Id].Zone, Zone.Attached, "Floating Reserve attached via Equip (Battle)");
    }
}
static void DisciplineKeyword()
{
    {
        // Discipline (issue #184): playable from a discard pile, paying its own printed Gold cost,
        // resolving its effect, then removed from the game -- never left in the discard pile again.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var card = Add(s, "training-discipline-item", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        var payment = e.PaymentOptions(0, 2).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: card.Id, AbilityId: "training-discipline-ability", SourceIds: payment.SourceIds));
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Resolves its printed effect");
        Equal(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "Removed from the game afterward, not left in the discard pile");
        Check(e.GetView(0).Players[0].RemovedFromGame.Any(c => c.Id == card.Id), "Visible to its own controller in the new Removed-from-Game pile");
        Check(e.GetView(1).Players[0].RemovedFromGame.Any(c => c.Id == card.Id), "Visible to the opponent too, like any discard pile");
    }
    {
        // Cannot be activated from Hand -- only from a discard pile. (The fixture is an Item: the Discipline
        // flag adds the discard-pile mode, and a hand play only exists for a Strategy, whose printed action is
        // its ordinary play -- see DisciplineAndAbsentTests for the Strategy in hand.)
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var card = Add(s, "training-discipline-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: card.Id, AbilityId: "training-discipline-ability", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
    }
    {
        // May be discarded for another purpose (here, Tactical Advantage's own Focus-Value discard)
        // and still have its own Discipline ability used from the discard pile in the same turn.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var tactician = Add(s, "training-tactician", 0, Zone.Battlefield, province: 0);
        var card = Add(s, "training-discipline-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, PerformerId: tactician.Id, TargetId: card.Id, AbilityId: "tactical-advantage"));
        Equal(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "Discarded for its Focus Value by an unrelated ability");
        Accept(e, new GameCommand("pass", 1, e.Version));
        var payment = e.PaymentOptions(0, 2).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: card.Id, AbilityId: "training-discipline-ability", SourceIds: payment.SourceIds));
        Equal(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "Discipline still usable the same turn, unaffected by the earlier, unrelated discard");
    }
}
static void DishonorRehonorMechanics()
{
    {
        // Dishonor's own Personal-Honor override (issue #186, Utaku Chikako's shape): without the
        // override, a dishonorable Battle Maiden's current Personal Honor reads 0 (blocking the
        // Follower Honor Requirement, the same observable probe already used elsewhere); with it (an
        // in-play Personality the same owner controls carrying the override Effect), it reads the
        // printed/base value instead.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var follower = Add(s, "training-follower", 0, Zone.Hand); // HonorRequirement 1
        var maiden = Add(s, "training-battle-maiden", 0, Zone.Home); maiden.Dishonorable = true; // printed PH 1
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: maiden.Id));

        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var follower2 = Add(s2, "training-follower", 0, Zone.Hand);
        var maiden2 = Add(s2, "training-battle-maiden", 0, Zone.Home); maiden2.Dishonorable = true; // printed PH 1
        Add(s2, "training-battle-maiden-honor-override", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Cmd(e2, "equip", id: follower2.Id, sources: e2.PaymentOptions(0, 2).First().SourceIds, target: maiden2.Id));
    }
    {
        // GainHonorFromPerformer (issue #186): an honorable performer's own ability grants Honor
        // normally; a dishonorable performer's gain is negated and the performer is rehonored instead.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        s.FavorPlayerId = 0;
        var honorable = Add(s, "training-honor-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: honorable.Id, AbilityId: "shunya-honor"));
        Equal(e.State.Players[0].Honor, honorBefore + 2, "Honorable performer: gains Honor normally");

        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        s2.FavorPlayerId = 0;
        var dishonorable = Add(s2, "training-honor-personality", 0, Zone.Home); dishonorable.Dishonorable = true;
        var e2 = MatchEngine.FromTestState(s2);
        var honorBefore2 = e2.State.Players[0].Honor;
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: dishonorable.Id, AbilityId: "shunya-honor"));
        Equal(e2.State.Players[0].Honor, honorBefore2, "Dishonorable performer: gain negated");
        Check(!e2.State.Cards[dishonorable.Id].Dishonorable, "...and rehonored instead");
    }
    {
        // Battle resolution (issue #186): a dishonorable personality in the WINNING army negates the
        // full Honor gain from that victory and is rehonored.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var attacker = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); attacker.Dishonorable = true; // Force 6
        var defender = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Resolve(e);
        Check(!e.State.Cards[attacker.Id].Dishonorable, "Rehonored");
        Equal(e.State.Players[0].Honor, honorBefore, "Honor gain from the victory negated entirely");
        Equal(e.State.Cards[defender.Id].Zone, Zone.Dead, "The defeated defender is still destroyed normally");
    }
    {
        // Tie (issue #186): rehonored BEFORE the tie's mutual destruction, so the dishonorable side
        // takes neither the battle's own gain nor its own dishonorable-death Honor penalty; the other
        // (honorable) side still gains Honor normally from what it destroyed.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); attacker.Dishonorable = true; // Force 3, printed PH 2
        var defender = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, PH 2
        var e = MatchEngine.FromTestState(s);
        var attackerHonorBefore = e.State.Players[0].Honor; var defenderHonorBefore = e.State.Players[1].Honor;
        Resolve(e);
        Equal(e.State.Cards[attacker.Id].Zone, Zone.Dead, "Destroyed in the mutual tie");
        Equal(e.State.Players[0].Honor, attackerHonorBefore, "Dishonorable side: no gain, and no dishonorable-death penalty either (rehonored first)");
        Equal(e.State.Players[1].Honor, defenderHonorBefore + 1, "Honorable side still gains Honor normally (1 destroyed, a tie does not double it)");
    }
}
static void UtakuChikako()
{
    {
        // Passive: the real card grants the override too, not just the training fixture.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var follower = Add(s, "training-follower", 0, Zone.Hand);
        var maiden = Add(s, "training-battle-maiden", 0, Zone.Home); maiden.Dishonorable = true; // printed PH 1
        Add(s, "oracle-l5r-10102", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: maiden.Id));
    }
    {
        // Limited: gain 2 Honor, then may not declare an attack this turn -- a broad restriction on
        // declaring the attack itself. Declining (the Declaration Segment's pass) is always still legal.
        // Resets at the start of the next turn.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var chikako = Add(s, "oracle-l5r-10102", 0, Zone.Home);
        var unit = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: chikako.Id, AbilityId: "utaku-chikako-decline"));
        Equal(e.State.Players[0].Honor, honorBefore + 2, "Gains 2 Honor");
        Check(e.State.Players[0].MayNotAttackThisTurn, "Restricted for the rest of this turn");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("pass", 0, e.Version)); // Action Phase: two consecutive passes -> Attack Declaration
        Reject(e, new GameCommand("declareAttack", 0, e.Version)); // declaring an attack is rejected
        Reject(e, new GameCommand("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [unit.Id] = 0 })); // and so is assigning an attacker, wherever it is offered
        Check(e.QueryActions(0).Any(a => a.Type == "declareAttack" && !a.Enabled), "declaring is listed, disabled");
        Accept(e, new GameCommand("pass", 0, e.Version)); // declining is still legal: the Attack Phase simply ends
        Equal(e.State.Phase, Phase.Dynasty);
    }
}
static void KihoDiscardToHand()
{
    {
        // Volcano Tattoo (issue #190): a real "Fire Kiho Battle" card. Bows the performing Tattooed
        // Monk (Togashi Meiyu: Fire, Monk, Tattooed, Chi 3) to destroy an unattached card in an enemy
        // unit whose Personality's Chi is at or below hers, then straightens her again since the
        // destroyed card (a Follower) counts as "an attachment" and she is Fire.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var meiyu = Add(s, "oracle-l5r-8550", 0, Zone.Battlefield, province: 0); // Fire/Monk/Tattooed, Chi 3
        var enemyHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3 <= 3
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id);
        var volcano = Add(s, "oracle-l5r-9186", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: volcano.Id, PerformerId: meiyu.Id, TargetId: enemyFollower.Id, AbilityId: "volcano-tattoo-destroy"));
        Check(e.State.Cards[meiyu.Id].Zone == Zone.Battlefield && !e.State.Cards[meiyu.Id].Bowed, "Fire performer is straightened again: the destroyed Follower counted as an attachment");
        Equal(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "The unattached Follower is destroyed -- FateDiscard, not Dead");
        Equal(e.State.Cards[volcano.Id].Zone, Zone.FateDiscard, "Volcano Tattoo discards after resolving, like any other Strategy");
        Check(e.State.Cards[volcano.Id].AbilityUses.ContainsKey("volcano-tattoo-destroy"), "Stamped used this turn");
    }
    {
        // Hunter of Harmony (issue #190): "Remove this card from the game: choose a Kiho Strategy in
        // your discard pile. Put it into your hand." Volcano Tattoo, already used once this turn
        // (simulating an earlier play-then-discard), returns to hand "freshly drawn": its once-per-
        // turn stamp is cleared, so it may be used again this same turn.
        // Hunter of Harmony's own Limited ability requires Phase.Action (like every other Limited
        // ability); Volcano Tattoo's own Battle ability requires Phase.Battle. A real match reaches
        // both within the same turn via Action -> AttackDeclaration -> Battle. MatchEngine.State
        // always returns a fresh copy (writes through it are no-ops), so the transition is modeled
        // the same way every other test builds a starting MatchState: capture the post-return state,
        // edit its Phase/PriorityPlayerId, and wrap a second engine around it -- same TurnNumber
        // throughout, which is all the once-per-turn stamp comparison actually depends on.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.Phase = Phase.Action;
        var meiyu = Add(s, "oracle-l5r-8550", 0, Zone.Battlefield, province: 0);
        var enemyHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hunter = Add(s, "oracle-l5r-3522", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var volcano = Add(s, "oracle-l5r-9186", 0, Zone.FateDiscard);
        volcano.AbilityUses["volcano-tattoo-destroy"] = s.TurnNumber;
        var plainStrategy = Add(s, "training-rally", 0, Zone.FateDiscard); // a Strategy, but not Kiho
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: hunter.Id, TargetId: plainStrategy.Id, AbilityId: "hunter-of-harmony-return")); // not Kiho
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: hunter.Id, TargetId: volcano.Id, AbilityId: "hunter-of-harmony-return"));
        Equal(e.State.Cards[hunter.Id].Zone, Zone.RemovedFromGame, "Hunter of Harmony pays its own cost: removed from the game");
        Check(e.State.Cards[volcano.Id].Zone == Zone.Hand && e.State.Players[0].Hand.Contains(volcano.Id), "Volcano Tattoo returns to hand");
        Check(e.State.Cards[volcano.Id].AbilityUses.Count == 0, "Treated as freshly drawn: its earlier stamp is cleared");
        var s2 = e.State; s2.Phase = Phase.Battle; s2.PriorityPlayerId = 0; s2.CurrentBattleProvince = 0; s2.UnresolvedBattlefields = [0];
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: volcano.Id, PerformerId: meiyu.Id, TargetId: enemyFollower.Id, AbilityId: "volcano-tattoo-destroy"));
        Equal(e2.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "May be used again this same turn, unlike a card that stayed in the discard pile -- FateDiscard, not Dead");
    }
}
static void MultiKeywordRequirement()
{
    // "A Dragon Clan Kensai", "a Crane Clan Magistrate" (issue #190): any number of keywords, all
    // independently required, order irrelevant. Volcano Tattoo's own "Tattooed Monk" Performer
    // requirement is exactly this generic primitive (RequiredKeywords: ["Monk","Tattooed"]), not a
    // hardcoded category -- a card with only one of the two keywords does not qualify.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var monkOnly = Add(s, "training-monk", 0, Zone.Battlefield, province: 0); // Monk, but not Tattooed
    var meiyu = Add(s, "oracle-l5r-8550", 0, Zone.Battlefield, province: 0); // Monk and Tattooed (and Fire)
    var enemyHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
    var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id);
    var volcano = Add(s, "oracle-l5r-9186", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s);
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: volcano.Id, PerformerId: monkOnly.Id, TargetId: enemyFollower.Id, AbilityId: "volcano-tattoo-destroy")); // only one of the two required keywords
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: volcano.Id, PerformerId: meiyu.Id, TargetId: enemyFollower.Id, AbilityId: "volcano-tattoo-destroy")); // has both
}
static void GojuHouseGuardAndAberrations()
{
    {
        // Goju House Guard (issue #192): target with two attachments -- both individually "without
        // attachments" (neither carries a further attachment of its own), so both return to the
        // bottom of their owner's Fate deck; the target Personality herself HAS attachments at the
        // snapshot moment, so she stays in play untouched. Goju's own cost requires an explicit
        // DeckPosition and applies it to itself (Top here), separate from the target's own cards
        // (which always go to the Bottom regardless of Goju's own choice).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var goju = Add(s, "oracle-l5r-2851", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var followerA = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemy.Id);
        var followerB = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: goju.Id, TargetId: enemy.Id, AbilityId: "goju-house-guard-return")); // no DeckPosition chosen
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: goju.Id, TargetId: enemy.Id, AbilityId: "goju-house-guard-return", DeckPosition: "Top"));
        Equal(e.State.Cards[goju.Id].Zone, Zone.FateDeck, "Goju itself returns to its own owner's Fate deck");
        Check(e.State.Players[0].FateDeck[0] == goju.Id, "Placed on top -- the controller's choice");
        Equal(e.State.Cards[followerA.Id].Zone, Zone.FateDeck, "An unattached card in the unit returns to the bottom of its owner's Fate deck");
        Equal(e.State.Cards[followerB.Id].Zone, Zone.FateDeck, "Likewise for the second one");
        Check(new[] { followerA.Id, followerB.Id }.Contains(e.State.Players[1].FateDeck[^1]) && new[] { followerA.Id, followerB.Id }.Contains(e.State.Players[1].FateDeck[^2]), "Both placed at the bottom");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "The target Personality herself had attachments at the snapshot moment -- she stays in play");
    }
    {
        // A bare target (no attachments at all): the Personality herself is "a card without
        // attachments in his unit" -- she returns to the bottom of her own owner's Dynasty deck,
        // fully reset (no longer bowed, no stale Modifiers).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var goju = Add(s, "oracle-l5r-2851", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: goju.Id, TargetId: enemy.Id, AbilityId: "goju-house-guard-return", DeckPosition: "Bottom"));
        Check(e.State.Players[0].FateDeck[^1] == goju.Id, "Placed on bottom -- the controller's choice");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.DynastyDeck, "A bare Personality returns to the bottom of her own owner's Dynasty deck");
        Check(!e.State.Cards[enemy.Id].Bowed, "Fully reset: no longer bowed");
        Check(e.State.Players[1].DynastyDeck[^1] == enemy.Id);
    }
    {
        // Aberrations: discards the forced top card of its OWN controller's Fate deck (no choice),
        // gaining a Force bonus equal to that card's own printed Focus Value.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var aberrations = Add(s, "oracle-l5r-10516", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        var topId = e.State.Players[0].FateDeck[0];
        var topFocus = CardCatalog.Get(e.State.Cards[topId].DefinitionId).Focus;
        var deckCountBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: aberrations.Id, AbilityId: "aberrations-boost"));
        Equal(e.State.Cards[topId].Zone, Zone.FateDiscard, "The top card of the Fate deck is discarded");
        Equal(e.State.Players[0].FateDeck.Count, deckCountBefore - 1);
        Check(e.State.Cards[aberrations.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == topFocus), "Force bonus equal to the discarded card's Focus Value");
    }
}
static void FocusOneBattleActionsWave()
{
    {
        // Sankaku-Yari (issue #192): zero new primitives -- Horseback Warriors already established a
        // target-keyword-conditional Attack strength; this is the mirror image (bonus FOR Cavalry).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var yari = Add(s, "oracle-l5r-6469", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var nonCavalry = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5, no attachments, not Cavalry
        var cavalry = Add(s, "training-cavalry", 1, Zone.Battlefield, province: 0); // Force 2, well within either strength
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: yari.Id, TargetId: nonCavalry.Id, AbilityId: "sankaku-yari-attack"), nonCavalry.Id); // Melee 4 < Force 5, no Cavalry bonus: legal target, survives
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: yari.Id, TargetId: cavalry.Id, AbilityId: "sankaku-yari-attack"));
        Equal(e.State.Cards[cavalry.Id].Zone, Zone.Dead, "Destroys the Cavalry target");
    }
    {
        // Pearl of Embers (issue #192): zero new primitives -- bows itself as a cost, straightens its
        // own wielder even though he was already bowed.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var pearl = Add(s, "oracle-l5r-5902", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: pearl.Id, AbilityId: "pearl-of-embers-straighten"));
        Check(e.State.Cards[pearl.Id].Bowed, "Bows itself as the cost");
        Check(!e.State.Cards[host.Id].Bowed, "Straightens its wielder");
    }
    {
        // Ritual Preparation (issue #192): "your target card in a unit" -- either a Personality or a
        // Follower, own, in play; needed the one new own-unit-card Target category.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, attachedTo: host.Id, bowed: true);
        var ritual = Add(s, "oracle-l5r-10714", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ritual.Id, TargetId: follower.Id, AbilityId: "ritual-preparation-straighten"));
        Check(!e.State.Cards[follower.Id].Bowed, "Straightens the targeted Follower");
        Check(e.State.Cards[host.Id].Bowed, "The Personality herself is untouched -- a different card was targeted");
    }
}
static void EnlightenmentVictory()
{
    {
        // All five REAL elemental Rings in play at the start of a turn (issue #194) -- checked via
        // CreateEconomyScenario's own Dynasty-phase start, matching HonorVictory's own harness.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-ring-of-air", 0, Zone.Home); Add(s, "training-ring-of-water", 0, Zone.Home);
        Add(s, "training-ring-of-void", 0, Zone.Home); Add(s, "training-ring-of-fire-stub", 0, Zone.Home);
        Add(s, "training-ring-of-earth-stub", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "endDynasty")); Check(e.State.WinnerId is null, "Not this player's own turn yet");
        SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.WinnerId, 0); Equal(e.State.VictoryReason, "Enlightenment victory");
    }
    {
        // False Rings never count, even sharing the exact same elemental keyword as a missing real
        // Ring (issue #194) -- Fudo is the actual exclusion, not the elemental keyword itself.
        var s = MatchEngine.CreateEconomyScenario().State;
        Add(s, "training-ring-of-air", 0, Zone.Home); Add(s, "training-ring-of-water", 0, Zone.Home);
        Add(s, "training-ring-of-void", 0, Zone.Home); Add(s, "training-ring-of-fire-stub", 0, Zone.Home);
        Add(s, "training-false-ring-of-earth", 0, Zone.Home); // carries "Earth", but also "Fudo"
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "endDynasty")); SkipToDynasty(e); Accept(e, Cmd(e, "endDynasty"));
        Check(e.State.WinnerId is null, "A False Ring's own elemental keyword does not substitute for the real Ring");
    }
}
static void RingOfAir()
{
    // Ring of Air (issue #194): two Favor actions in one turn grants an Additional Battle Action
    // scoped to the Ring (rather than a forced mid-resolution interrupt, per the user's own
    // simplification); consuming it recruits the Ring; its own in-play action (straighten a
    // Personality or attachment, Repeatable) is then usable, bowing itself as the cost.
    // Ring of Air's own condition is checked mid-Action-phase (Favor actions are usable there), but
    // its recruit ability is Timing "Battle" -- a real match reaches both within the same turn via
    // Action -> AttackDeclaration -> Battle. MatchEngine.State always returns a fresh copy (writes
    // through it are no-ops), so the transition is modeled by capturing the post-Favor-action state,
    // editing its Phase/PriorityPlayerId, and wrapping a second engine around it (same pattern used
    // for Volcano Tattoo's own Phase.Action -> Phase.Battle test, issue #190).
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.Phase = Phase.Action; s.FavorPlayerId = 0; s.Players[0].FavorActionsThisTurn = 1;
    var ring = Add(s, "training-ring-of-air", 0, Zone.Hand);
    var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
    var shunya = Add(s, "training-honor-personality", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shunya.Id, AbilityId: "shunya-honor")); // this turn's 2nd Favor action
    Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds != null && g.ScopeCardIds.Contains(ring.Id)), "Grants an Additional Battle Action scoped to the Ring");
    var s2 = e.State; s2.Phase = Phase.Battle; s2.PriorityPlayerId = 0; s2.BattleNumber = 1; // the grant made in the Action phase belongs to the next battle (#379): the one SelectBattle numbers 1
    var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: ring.Id, AbilityId: "ring-of-air-recruit"));
    Equal(e2.State.Cards[ring.Id].Zone, Zone.Home, "Recruited into play"); Check(!e2.State.Players[0].Hand.Contains(ring.Id));
    Accept(e2, Cmd(e2, "pass")); // priority ceded to the opponent once the grant is spent; they pass back
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: ring.Id, TargetId: host.Id, AbilityId: "ring-of-air-straighten"));
    Check(!e2.State.Cards[host.Id].Bowed, "Straightens the target"); Check(e2.State.Cards[ring.Id].Bowed, "Bows itself as the in-play cost");
}
static void RingOfWater()
{
    // Ring of Water (issue #194): a battle resolves in which this player played a Terrain, destroyed
    // a Terrain, and destroyed a card during resolution -- grants an Additional Battle Action scoped
    // to the Ring, surviving FinishBattleResolution's own Clear() (only ordinary grants are cleared).
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var ring = Add(s, "training-ring-of-water", 0, Zone.Hand);
    var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
    var destroyer = Add(s, "training-terrain-destroyer", 0, Zone.Battlefield, province: 0); // Force 2 -- af=5
    var terrain = Add(s, "training-terrain", 0, Zone.Hand);
    var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3 -- df=3, attacker wins
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: terrain.Id));
    Check(e.State.Players[0].TerrainPlayedThisBattle, "Playing a Terrain sets the flag");
    Accept(e, Cmd(e, "pass"));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: terrain.Id, AbilityId: "training-destroy-terrain"));
    Check(e.State.Players[0].TerrainDestroyedThisBattle, "Destroying it sets the flag");
    Resolve(e); // two passes in Phase.Battle -> ResolveBattle; af=5 > df=3, the enemy Personality is destroyed
    Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds != null && g.ScopeCardIds.Contains(ring.Id)), "Grants an Additional Battle Action scoped to the Ring, surviving battle-end cleanup");
}
static void RingOfTheVoid()
{
    // Ring of the Void (issue #194): a Limited ability (not a granted Additional Action) -- gated
    // only by controlling two or fewer Rings; its own cost discards the rest of the player's hand.
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var ring = Add(s, "training-ring-of-void", 0, Zone.Hand);
    var otherCard = Add(s, "training-follower", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-void-recruit"));
    Equal(e.State.Cards[ring.Id].Zone, Zone.Home, "Recruited into play");
    Equal(e.State.Cards[otherCard.Id].Zone, Zone.FateDiscard, "The rest of the hand is discarded");
    Check(!e.State.Players[0].Hand.Contains(otherCard.Id) && !e.State.Players[0].Hand.Contains(ring.Id));
}
static void FalseRingOfAir()
{
    // False Ring of Air (issue #194): after winning a battle with an opposing unit present (approximated
    // as "present at the moment of resolution"), grants an Additional Battle Action scoped to the Ring.
    // Recruiting it costs 2 Honor, unlike any of the three real Rings.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var ring = Add(s, "training-false-ring-of-air", 0, Zone.Hand);
    Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // Force 6
    Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3 -- attacker wins, opposing unit was present
    var e = MatchEngine.FromTestState(s);
    Resolve(e); // the battle win itself also grants Honor (destroyed the enemy Personality) -- captured below, after that settles
    Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds != null && g.ScopeCardIds.Contains(ring.Id)), "Grants an Additional Battle Action scoped to the Ring, surviving battle-end cleanup");
    var honorBefore = e.State.Players[0].Honor;
    // The grant is only consumable during Phase.Battle, which this battle's own resolution has just
    // left (FinishBattleResolution moves on to Dynasty/BattleSelection) -- reachable again at this
    // player's NEXT battle, modeled the same way as Ring of Air's own Phase transition above.
    var s2 = e.State; s2.Phase = Phase.Battle; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0; s2.CurrentBattleProvince = 0; s2.UnresolvedBattlefields = [0];
    Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // presence at a new battle, to satisfy the generic Battle-timing Require
    var e2 = MatchEngine.FromTestState(s2);
    Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: ring.Id, AbilityId: "false-ring-of-air-recruit"));
    Equal(e2.State.Cards[ring.Id].Zone, Zone.Home, "Recruited into play");
    Equal(e2.State.Players[0].Honor, honorBefore - 2, "Costs 2 Honor, unlike a real Ring");
}
static void FalseRingOfWater()
{
    // False Ring of Water (issue #194): a standing condition (not a Reaction/grant) -- usable any
    // time the current battlefield's own Province Strength is 11 or greater, like an ordinary
    // Battle ability. Its own in-play action is a plain Ranged 3 Attack.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 1;
    s.Players[1].Provinces.Single(p => p.Index == 0).Strength = 10; // "the current battlefield's own province" is the Defender's
    var ring = Add(s, "training-false-ring-of-water", 1, Zone.Hand);
    Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: ring.Id, AbilityId: "false-ring-of-water-recruit")); // Strength 10, not enough
    var s2 = e.State; s2.PriorityPlayerId = 1; s2.Players[1].Provinces.Single(p => p.Index == 0).Strength = 11;
    var e2 = MatchEngine.FromTestState(s2);
    var honorBefore = e2.State.Players[1].Honor;
    Accept(e2, new GameCommand("useAbility", 1, e2.Version, CardId: ring.Id, AbilityId: "false-ring-of-water-recruit"));
    Equal(e2.State.Cards[ring.Id].Zone, Zone.Home, "Recruited into play"); Equal(e2.State.Players[1].Honor, honorBefore - 2, "Costs 2 Honor");
}
static void RingOfEarth()
{
    {
        // Ring of Earth (issue #194, revalidated): winning a battle with enemy units present grants
        // an Additional Battle Action scoped to the Ring -- the same "you won" requirement as False
        // Ring of Air, unlike Ring of Water/Fire, which need no win at all.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var ring = Add(s, "training-ring-of-earth-stub", 0, Zone.Hand);
        Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // Force 6
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3 -- attacker wins, enemy unit was present
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds != null && g.ScopeCardIds.Contains(ring.Id)), "Grants an Additional Battle Action scoped to the Ring, surviving battle-end cleanup");
    }
    {
        // Its own in-play action: "Even if you control no units at the current battlefield" waives
        // the ordinary Battle-timing presence check entirely -- may target either side's own
        // province (the new "any-province" category), raising or lowering its strength by 3, the
        // player's own choice (a dual-UsageKey pair sharing one once-per-turn dedupe).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var ring = Add(s, "training-ring-of-earth-stub", 0, Zone.Home); // in play, no units of player 0's own anywhere at this battlefield
        var e = MatchEngine.FromTestState(s);
        var enemyStrengthBefore = e.ProvinceStrengthForTest(1, 0);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-raise", TargetPlayerId: 1, Province: 0));
        Equal(e.ProvinceStrengthForTest(1, 0), enemyStrengthBefore + 3, "Raises the enemy's own province by 3, even with no units of its own present");
        Equal(Batch336Support.StrengthNextTurn(e, 1, 0), enemyStrengthBefore, "Ring of Earth: until end of turn (issue #492 ruling R3, Mendon)");
        Check(e.State.Cards[ring.Id].Bowed, "Bows itself as the in-play cost");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-lower", TargetPlayerId: 0, Province: 0)); // already used this turn (shared UsageKey) and now bowed
    }
    {
        // Used from hand instead: discarded afterward (the same generic Ring-from-hand shape every
        // other Ring already has), never bowed.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var ring = Add(s, "training-ring-of-earth-stub", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var ownStrengthBefore = e.ProvinceStrengthForTest(0, 0);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-lower", TargetPlayerId: 0, Province: 0));
        Equal(e.ProvinceStrengthForTest(0, 0), ownStrengthBefore - 3, "May target its own controller's own province too");
        Equal(Batch336Support.StrengthNextTurn(e, 0, 0), ownStrengthBefore, "Ring of Earth (lower): until end of turn");
        Equal(e.State.Cards[ring.Id].Zone, Zone.FateDiscard, "Discarded from hand instead of bowed");
    }
}
static void RingOfFire()
{
    {
        // Ring of Fire (issue #194, revalidated -- issue #195's own text uncertainty resolved by
        // reading the scan directly): no "win" requirement, same as Ring of Water -- destroying one
        // or more provinces or enemy cards in a battle's own resolution is enough on its own.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var ring = Add(s, "training-ring-of-fire-stub", 0, Zone.Hand);
        Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // Force 6
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3 -- destroyed
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds != null && g.ScopeCardIds.Contains(ring.Id)), "Grants an Additional Battle Action scoped to the Ring, surviving battle-end cleanup");
    }
    {
        // Its own in-play action: destroys its own performing Personality as part of the cost (no
        // new destroy primitive -- DestroyUnits is already fully generic), and a target enemy
        // Personality with equal or lower Chi than the performer (enemy-personality-chi-lte-performer,
        // Chokehold's own Target -- not personality-chi-lte-self, which would wrongly compare against
        // the Ring's own nonexistent Chi instead of the performer's).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var ring = Add(s, "training-ring-of-fire-stub", 0, Zone.Home);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
        var tooHighChi = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Chi 4 > 3
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3 <= 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, PerformerId: performer.Id, TargetId: tooHighChi.Id, AbilityId: "ring-of-fire-destroy"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, PerformerId: performer.Id, TargetId: target.Id, AbilityId: "ring-of-fire-destroy"));
        Equal(e.State.Cards[performer.Id].Zone, Zone.Dead, "Destroys its own performing Personality as part of the cost");
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Destroys the target");
        Check(e.State.Cards[ring.Id].Bowed, "Bows itself as the in-play cost");
    }
}
static void SenseiKeyword()
{
    // Sensei (Mirumoto Hikaro / Mirumoto Yozo, Experienced 2, issue #207 follow-up): "After [this
    // card] enters play: Discard all your other Sensei Personalities" -- a Home card discards
    // directly; a face-up province card discards AND refills; a face-down province card and an
    // unrelated (non-Sensei) card are both unaffected; the entering card itself is never its own
    // "other".
    var s = MatchEngine.CreateEconomyScenario().State;
    for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
    var homeSensei = Add(s, "training-sensei-personality", 0, Zone.Home);
    var provinceSenseiId = s.Players[0].Provinces[0].CardId!;
    s.Cards[provinceSenseiId].DefinitionId = "training-sensei-personality"; s.Cards[provinceSenseiId].FaceUp = true;
    var faceDownSenseiId = s.Players[0].Provinces[1].CardId!;
    // Forced back to hidden: CreateEconomyScenario's own StartTurn already ran EventsPhase, which
    // reveals every one of the ACTIVE player's own provinces before Purchase is ever reachable --
    // this specific combination (a face-down OWN province, still Purchase-reachable) does not occur
    // naturally, but the same carve-out is real and reachable via RecruitReserve (a non-active
    // player's own OTHER, unrelated provinces are never revealed by an opponent's own EventsPhase).
    s.Cards[faceDownSenseiId].DefinitionId = "training-sensei-personality"; s.Cards[faceDownSenseiId].FaceUp = false;
    var nonSensei = Add(s, "training-crane-samurai", 0, Zone.Home);
    var enteringCardId = s.Players[0].Provinces[2].CardId!;
    s.Cards[enteringCardId].DefinitionId = "training-sensei-entry"; s.Cards[enteringCardId].FaceUp = true;
    var e = MatchEngine.FromTestState(s);
    var payment = e.PaymentOptions(0, 3).First();
    Accept(e, new GameCommand("purchase", 0, e.Version, CardId: enteringCardId, SourceIds: payment.SourceIds));
    Equal(e.State.Cards[homeSensei.Id].Zone, Zone.DynastyDiscard, "Discarded from Home");
    Equal(e.State.Cards[provinceSenseiId].Zone, Zone.DynastyDiscard, "Discarded from a province, face-up");
    Check(e.State.Cards[provinceSenseiId].ProvinceIndex is null);
    Check(e.State.Players[0].Provinces[0].CardId != provinceSenseiId, "The province refills with a different card");
    Equal(e.State.Cards[faceDownSenseiId].Zone, Zone.Province, "A face-down copy is hidden information, never affected");
    Equal(e.State.Cards[nonSensei.Id].Zone, Zone.Home, "A non-Sensei card is unaffected");
    Equal(e.State.Cards[enteringCardId].Zone, Zone.Home, "The entering card itself is never discarded as its own 'other'");
}
static void YozoIaijutsuBonus()
{
    // Mirumoto Yozo, Experienced 2's own second trait (issue #207 follow-up): "after your Iaijutsu
    // action resolves, give any Personality who performed it +2F" -- exercised against Kitsuki
    // Daisuke's own real self-performer Iaijutsu duel (daisuke-duel), regardless of win or loss.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var daisuke = Add(s, "oracle-l5r-4406", 0, Zone.Battlefield, province: 0); // Chi 4
    var weakEnemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3 -- Daisuke wins
    Add(s, "training-yozo-exp2", 0, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    var forceBefore = e.GetView(0).Players[0].Army.Single(c => c.Id == daisuke.Id).Force;
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: daisuke.Id, TargetId: weakEnemy.Id, AbilityId: "daisuke-duel"));
    Accept(e, new GameCommand("strike", 1, e.Version));
    Accept(e, new GameCommand("duelistRefocus", 0, e.Version)); // Daisuke carries Duelist: decline the extra refocus opportunity
    Equal(e.State.Cards[weakEnemy.Id].Zone, Zone.Home, "Daisuke wins (Chi 4 > 3); the loser moves home, per daisuke-duel's own outcome");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == daisuke.Id).Force, forceBefore + 2, "Yozo's own trait: +2F for performing an Iaijutsu action");
}
static void HikaroDiscardPileFocus()
{
    // Mirumoto Hikaro's own second trait (issue #207 follow-up): "before you focus in a duel
    // involving your Dragon Clan Personality, once per duel, you may focus from your discard pile
    // instead" -- revealed immediately (unlike an ordinary hidden focus), removed from the game
    // after the duel ends (not the ordinary FateDiscard every other focused card gets), once per duel.
    var s = MatchEngine.CreateEconomyScenario().State;
    s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
    var dragon = Add(s, "oracle-l5r-4406", 0, Zone.Battlefield, province: 0); // Kitsuki Daisuke, Dragon Clan, Chi 4
    var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
    Add(s, "training-hikaro", 0, Zone.Home);
    var discardedCard = Add(s, "training-rally", 0, Zone.FateDiscard);
    var otherDiscarded = Add(s, "training-rally", 0, Zone.FateDiscard);
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: dragon.Id, TargetId: enemy.Id, AbilityId: "daisuke-duel"));
    Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck")); // the challenged player's own first Focus-or-Strike option
    Accept(e, new GameCommand("focus", 0, e.Version, Choice: "discard", CardId: discardedCard.Id));
    Equal(e.State.Cards[discardedCard.Id].Zone, Zone.Focused);
    Check(e.State.Cards[discardedCard.Id].FaceUp, "Revealed to the opponent immediately, unlike an ordinary hidden focus");
    Check(e.State.PendingDuel!.HikaroDiscardFocusUsed[0], "Marked used for this player this duel");
    Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
    Reject(e, new GameCommand("focus", 0, e.Version, Choice: "discard", CardId: otherDiscarded.Id));
    Accept(e, new GameCommand("strike", 0, e.Version));
    Accept(e, new GameCommand("duelistRefocus", 0, e.Version)); // Daisuke carries Duelist: decline the extra refocus opportunity
    Equal(e.State.Cards[discardedCard.Id].Zone, Zone.RemovedFromGame, "Removed from the game after the duel ends, not the ordinary FateDiscard");
}
static void RokaiNegatesDestruction()
{
    // Mirumoto Rokai (issue #207 follow-up): "after another player's action targets your
    // Personality at Rokai's location, destroy Rokai: negate your Personality's destruction from
    // the action's effects" -- deferred BEFORE the destroy ever applies (DestroyUnits is never
    // reached until the choice is answered), scoped to another player's action, own controller, and
    // Rokai's own location.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
    var rokai = Add(s, "training-rokai", 1, Zone.Battlefield, province: 0);
    var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // no attachments; Rokai's own controller's Personality, same location
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: target.Id, AbilityId: "training-destroy-no-attachments"));
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
    ExpectBeforeActionOffer(e, rokai.Id);
    Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Not yet destroyed -- deferred until the choice is answered");
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: rokai.Id, AbilityId: "rokai-negate-destruction"));
    Equal(e.State.Cards[rokai.Id].Zone, Zone.Dead, "Rokai's own cost: destroying himself");
    Accept(e, Cmd(e, "declineReaction"));
    Accept(e, Cmd(e, "declineReaction"));
    Check(e.State.PendingChoice is null);
    Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Negated: the original destroy never applied");
}
static void TamokoNegatesMovementOrStraightening()
{
    {
        // "Negate his movement" -- Togashi Mihato's own real ability (mihato-move), no restriction
        // to another player's action or any location -- Tamoko may negate her own controller's own
        // action just as easily as an opponent's.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mihato = Add(s, "oracle-l5r-10289", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var tamoko = Add(s, "training-tamoko", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mihato.Id, TargetId: target.Id, AbilityId: "mihato-move"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        ExpectBeforeActionOffer(e, tamoko.Id);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tamoko.Id, AbilityId: "tamoko-negate"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Negated: the move never applied, and Tamoko herself is untouched (no cost)");
    }
    {
        // "Negate his straightening" -- a plain training fixture, declined this time.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var straightener = Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        Add(s, "training-tamoko", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: straightener.Id, TargetId: target.Id, AbilityId: "training-straighten-at-battle"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Check(!e.State.Cards[target.Id].Bowed, "Declined: the straighten applies normally");
    }
}
static void ChampionOfThunderNegatesOnAnyUnitMember()
{
    // General in-play unit-targeting Reaction primitive: Champion of Thunder reuses the same
    // beforeActionReaction window Rokai/Tamoko already opened (BeforeActionReactionApplies's own
    // Champion/Brothers branch), but the affected card is a genuine player CHOICE ("own-unit-member"),
    // not auto-determined -- any card in Champion's own unit, not just the literal TargetId.
    {
        // On itself (a Follower): Naga Remnants' own naga-remnants-bow (Target "enemy-attachment")
        // bows a target Follower directly -- Champion picks itself, the only card the window's own
        // CardId could plausibly have meant here.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var attackerHost = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var nagaRemnants = Add(s, "oracle-l5r-10513", 0, Zone.Attached, province: 0, attachedTo: attackerHost.Id);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nagaRemnants.Id, TargetId: champion.Id, AbilityId: "naga-remnants-bow"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        ExpectBeforeActionOffer(e, champion.Id);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: champion.Id, AbilityId: "champion-of-thunder-negate"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Check(!e.State.Cards[champion.Id].Bowed, "Negated: Champion never bows from the enemy's own action");
    }
    {
        // On its host Personality: training-bow-at-battle (Target "enemy-personality") bows the host
        // directly -- Champion's own unit still matches (same host), even though Champion itself was
        // never the literal TargetId, and its controller may choose to negate on the host instead.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: host.Id, AbilityId: "training-bow-at-battle"));
        ExpectBeforeActionOffer(e, champion.Id);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: host.Id, AbilityId: "champion-of-thunder-negate"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Check(!e.State.Cards[host.Id].Bowed, "Negated: the host never bows either");
    }
    {
        // On a sibling Follower: naga-remnants-bow again, this time targeting a DIFFERENT Follower in
        // Champion's own unit -- proving eligibility is unit membership, not "must be Champion's own
        // literal target."
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var attackerHost = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var nagaRemnants = Add(s, "oracle-l5r-10513", 0, Zone.Attached, province: 0, attachedTo: attackerHost.Id);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var sibling = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nagaRemnants.Id, TargetId: sibling.Id, AbilityId: "naga-remnants-bow"));
        ExpectBeforeActionOffer(e, champion.Id);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: sibling.Id, AbilityId: "champion-of-thunder-negate"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Check(!e.State.Cards[sibling.Id].Bowed, "Negated: the sibling Follower never bows");
    }
}
static void ChampionOfThunderChoosesAmongMultipleAffectedCards()
{
    // Champion of Thunder's own worked example (the card owner's own ruling): "if all Followers in
    // its unit suffer a Force penalty, the champion of thunder must choose which card to negate the
    // effects on." training-unit-force-penalty applies -1F to every card in the target's own unit at
    // once from a single command; Champion's controller chooses exactly ONE to negate on, leaving
    // the rest penalized -- NegatedEffectKind "all" is deliberately not consumed by the first match
    // (see NegatesEffect's own doc comment), so it stays live for every remaining ApplyModifier call
    // in the same re-invoked command, but only ever matches the one chosen card's own id.
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var penalizer = Add(s, "training-unit-penalty-personality", 0, Zone.Home);
    var host = Add(s, "training-crane-samurai", 1, Zone.Home);
    var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, attachedTo: host.Id);
    var sibling = Add(s, "training-follower", 1, Zone.Attached, attachedTo: host.Id);
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: penalizer.Id, TargetId: host.Id, AbilityId: "training-unit-force-penalty"));
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
    ExpectBeforeActionOffer(e, champion.Id);
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: sibling.Id, AbilityId: "champion-of-thunder-negate"));
    Accept(e, Cmd(e, "declineReaction"));
    Accept(e, Cmd(e, "declineReaction"));
    Equal(e.State.Cards[sibling.Id].Modifiers.Count, 0, "Chosen: the sibling's own Force penalty never applied");
    Equal(e.State.Cards[host.Id].Modifiers.Count, 1, "Not chosen: the host still suffers the Force penalty");
    Equal(e.State.Cards[champion.Id].Modifiers.Count, 1, "Not chosen: Champion itself still suffers the Force penalty");
}
static void BrothersOfGoemonRingGateAndPartialBowNegation()
{
    {
        // No Ring in play: the reaction is never even offered -- Costly Feud's own conditional bow
        // (Force penalty first, then bow if Force is now 0) resolves immediately and unopposed.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // army presence
        var feud = Add(s, "oracle-l5r-1521", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        Add(s, "oracle-l5r-1167", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: feud.Id, TargetId: host.Id, AbilityId: "costly-feud"));
        Check(e.State.PendingChoice is null, "No Ring: never offered");
        Check(e.State.Cards[host.Id].Bowed, "Costly Feud's own bow applies unopposed");
    }
    {
        // A Ring in play, but the resolving effect could never bow the target at all (Reiyu's own
        // plain Force penalty): BowsTarget's own curated classification correctly withholds the
        // reaction rather than offering a choice that could never matter.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var reiyu = Add(s, "oracle-l5r-5138", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-1167", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-ring-of-air", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: reiyu.Id, TargetId: host.Id, AbilityId: "reiyu-force-penalty"));
        Check(e.State.PendingChoice is null, "A plain Force penalty never offers this reaction");
        Check(e.State.Cards[host.Id].Modifiers.Count > 0, "The Force penalty itself still applies");
    }
    {
        // A Ring in play, and the resolving effect both applies a Force penalty AND conditionally
        // bows off the POST-penalty Force (Costly Feud, base Force 3 -> 0 after -3): the Force
        // penalty still lands in full; only the resulting bow is negated -- the retroactive/partial
        // negation the card needs, achieved by setting the negation signal BEFORE the whole command
        // is re-invoked, so the unconditional Force penalty and the now-negated conditional bow check
        // both run within that same single re-invocation.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // army presence
        var feud = Add(s, "oracle-l5r-1521", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var brothers = Add(s, "oracle-l5r-1167", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-ring-of-air", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: feud.Id, TargetId: host.Id, AbilityId: "costly-feud"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        ExpectBeforeActionOffer(e, brothers.Id);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: brothers.Id, AbilityId: "brothers-goemon-negate"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Check(!e.State.Cards[host.Id].Bowed, "Negated: only the bow");
        Check(e.State.Cards[host.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3), "The Force penalty still landed in full");
    }
}
static void ACloseCallArmorAttackNegation()
{
    {
        // Armor-wearing Personality targeted by a Ranged Attack: A Close Call, sitting in hand, may
        // negate the whole Attack -- zeroing PendingAttack.Strength before it resolves, the same
        // shape training-attack-negate already established. AnyAttackReactionAvailable's own new
        // out-of-play check is what makes a hand-only reactor discoverable at all.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var closeCall = Add(s, "oracle-l5r-10359", 1, Zone.Hand);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-armor", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: target.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: closeCall.Id, AbilityId: "a-close-call-negate"));
        // Issue #644: the negation was the only reaction; nobody is asked to pass, the window closes and the negated Attack resolves.
        Check(e.State.PendingChoice is null && e.State.PendingAttack is null, "the window closed");
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "Negated: the Armor-wearing target survives");
    }
    {
        // No Armor attached: the same Reaction is rejected outright -- the Armor/Kind Require gates
        // actually USING it, even though the window itself may still open regardless of the eventual
        // target's own Armor (A Close Call sits in hand either way).
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var closeCall = Add(s, "oracle-l5r-10359", 1, Zone.Hand);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // no Armor
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: target.Id, AbilityId: "carpenter-castle-siege"));
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: closeCall.Id, AbilityId: "a-close-call-negate"));
    }
    {
        // A Melee Attack against an Armor-wearing Personality is negated too, not Ranged-only.
        var s = StrongholdBattle("oracle-l5r-1218");
        var wielderHost = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: wielderHost.Id);
        var closeCall = Add(s, "oracle-l5r-10359", 1, Zone.Hand);
        var target = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); // Force 2, at or below the Melee 2 Attack's strength
        Add(s, "training-armor", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon.Id, TargetId: target.Id, AbilityId: "training-weapon-attack"));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: closeCall.Id, AbilityId: "a-close-call-negate"));
        Check(e.State.PendingChoice is null && e.State.PendingAttack is null, "the negation was the only reaction, so the window closed (issue #644)");
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "Negated: Melee Attacks too, not just Ranged");
    }
}
static void AkodoKakiharaPublishedShape()
{
    Equal(PublishedContent.PersonalityWave23.Cards.Length, 1, "Akodo Kakihara only");
    var k = CardCatalog.Get("oracle-l5r-223");
    Check(!k.ScenarioOnly && k.Type == CardType.Personality && k.Name == "Akodo Kakihara" && k.Clan == "Lion");
    // Verified against the scan (Force 4 / Chi 3 / Cost 7 / Honor Requirement 7 / Personal Honor 4).
    Equal(k.Force, 4); Equal(k.Chi, 3); Equal(k.GoldCost, 7); Equal(k.HonorRequirement, 7); Equal(k.PersonalHonor, 4);
    Check(k.Keywords!.Contains("Lion Clan") && k.Keywords.Contains("Samurai") && k.Keywords.Contains("Paragon"));
    // The published text follows the scan (a colon after "targets Kakihara"), not the catalogue's comma.
    Equal(k.Text, "Reaction: After another player's action targets Kakihara: Give him a Force bonus equal to his Personal Honor.");
    Equal(k.Abilities!.Length, 2);
    // One printed ability in two windows: exactly one per-turn use, shared through one UsageKey.
    Equal(AbilityCatalog.All["kakihara-force-bonus"].UsageKey, "kakihara-force-bonus");
    Equal(AbilityCatalog.All["kakihara-attack-force-bonus"].UsageKey, "kakihara-force-bonus");
    Equal(AbilityCatalog.All["kakihara-force-bonus"].Trigger, "before-action-resolves");
    Equal(AbilityCatalog.All["kakihara-attack-force-bonus"].Trigger, "before-attack-resolves");
}
static int KakiharaForce(MatchEngine e, string cardId) => e.GetView(0).Players.SelectMany(p => p.Home.Concat(p.Army)).Single(c => c.Id == cardId).Force;
static void AkodoKakiharaReactsToNonAttackActions()
{
    {
        // Another player's Battle action targets Kakihara: the window opens for HIS controller, on HIM.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Equal(KakiharaForce(e, k.Id), 4, "Printed Force before any reaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        ExpectBeforeActionOffer(e, k.Id);
        Equal(e.State.PendingChoice?.PlayerId, 0, "The window records the acting player (whose action is deferred)");
        Check(!e.State.Cards[k.Id].Bowed, "Deferred: the bow has not applied yet");
        var options = e.QueryActions(1);
        Check(options.Any(a => a.Command?.AbilityId == "kakihara-force-bonus" && a.Enabled), "The reaction is offered to his controller");
        Check(options.Any(a => a.Type == "declineReaction"), "and so is passing on it");
        Check(!options.Any(a => a.Command?.AbilityId == "kakihara-attack-force-bonus"), "The Attack half belongs to the Attack window, never this one");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus")); // wrong window
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
        Equal(KakiharaForce(e, k.Id), 8, "+4F: a bonus equal to his Personal Honor, applied before the action resolves");
        var modifier = e.State.Cards[k.Id].Modifiers.Single(m => m.Stat == "Force");
        Equal(modifier.Amount, 4); Equal(modifier.Source, k.Id); Equal(modifier.Effect, "kakihara-force-bonus");
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        Check(e.State.PendingChoice is null);
        Check(e.State.Cards[k.Id].Bowed, "The bonus is not a negation: the deferred action still resolves");
        Equal(KakiharaForce(e, k.Id), 8, "and the bonus stays");
        Equal(e.State.Cards[k.Id].AbilityUses["kakihara-force-bonus"], e.State.TurnNumber, "his one printed use this turn is spent");
    }
    {
        // Passing on it leaves him untouched; the action resolves as if he were not a watcher at all.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        Check(e.State.Cards[k.Id].Bowed && e.State.Cards[k.Id].Modifiers.Count == 0, "Declined: no bonus");
    }
    {
        // The amount is his CURRENT Personal Honor (printed 4, plus a +2 PersonalHonor modifier = 6), not the printed one.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        k.Modifiers.Add(new("test-source", "test", "PersonalHonor", 2));
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
        Equal(e.State.Cards[k.Id].Modifiers.Single(m => m.Stat == "Force").Amount, 6, "Current Personal Honor, not printed");
    }
    {
        // A dishonored Kakihara has 0 Personal Honor: +0F, so no window is opened for nothing at all.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0); k.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
        Check(e.State.PendingChoice is null, "No window for a +0F bonus");
        Check(e.State.Cards[k.Id].Bowed && e.State.Cards[k.Id].Modifiers.Count == 0, "The action resolved immediately, no bonus");
    }
    {
        // A different card is targeted: Kakihara sits in play but nothing about HIM triggers.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: other.Id, AbilityId: "training-bow-at-battle"));
        Check(e.State.PendingChoice is null, "An action targeting another card never opens his window");
        Check(e.State.Cards[other.Id].Bowed);
    }
    {
        // Bowed: ValidateAbility forbids any ability from a bowed card, so the window is never offered.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var striker = Add(s, "training-honor-strike-samurai", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: striker.Id, TargetId: k.Id, AbilityId: "training-honor-strike"));
        Check(e.State.PendingChoice is null, "A bowed Kakihara cannot react");
        Equal(e.State.Players[1].Honor, honor - 2, "The action resolved at once");
    }
    {
        // Not in play (in his controller's hand): nothing to monitor, an unrelated action resolves normally.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-223", 1, Zone.Hand);
        var other = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: other.Id, AbilityId: "training-bow-at-battle"));
        Check(e.State.PendingChoice is null, "A Kakihara in hand watches nothing");
    }
    {
        // Once per turn, shared with the Attack half: a spent use opens no window; a use stamped on an EARLIER turn does.
        foreach (var stamp in new[] { 0, -1 })
        {
            var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
            var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
            k.AbilityUses["kakihara-force-bonus"] = s.TurnNumber + stamp;
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
            Equal(e.State.PendingChoice?.Type, stamp == 0 ? null : "beforeActionReaction", stamp == 0 ? "Spent this turn: no window" : "Used on an earlier turn: available again");
        }
    }
    {
        // A Reaction IS an action (rules lead: "Reactions count as actions"): Tamori Ruya's own Reaction (ruya-bow) names
        // Kakihara as its target, from another player, inside an already-open bowReaction window. It triggers his window
        // like any other action targeting him; the bowReaction window it was played in is set aside (not overwritten) and
        // comes back, as it was, once the Reaction has resolved.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var ruya = Add(s, "oracle-l5r-7789", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var bowedByAction = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: bowedByAction.Id, AbilityId: "training-bow-at-battle"));
        Equal(e.State.PendingChoice?.Type, "bowReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ruya.Id, TargetId: k.Id, AbilityId: "ruya-bow"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "A Reaction that targets Kakihara opens his window");
        ExpectBeforeActionOffer(e, k.Id);
        Equal(e.State.SuspendedWindows.Count, 1); Equal(e.State.SuspendedWindows[0].Choice.Type, "bowReaction", "the window it was played in is set aside");
        Check(!e.State.Cards[k.Id].Bowed, "Deferred: Ruya's bow has not applied yet");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
        Equal(KakiharaForce(e, k.Id), 8, "+4F before the Reaction resolves");
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        // Issue #644: the window Ruya's Reaction was played in is given back, and with nothing left to take in it nobody is asked to pass it: it closes.
        Check(e.State.PendingChoice is null && e.State.SuspendedWindows.Count == 0, "The window Ruya's Reaction was played in has nothing left to offer, so it is closed");
        Check(e.State.Cards[k.Id].Bowed, "The Reaction resolved after Kakihara reacted");
    }
    {
        // Open actions too (not just Battle actions), and the acting player must be ANOTHER player: the
        // opponent's Open action targeting Kakihara opens the window; his own controller's never does.
        foreach (var actor in new[] { 0, 1 })
        {
            var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = actor; s.ActivePlayerId = actor;
            var source = Add(s, "training-bow-any-source", actor, Zone.Home);
            var k = Add(s, "oracle-l5r-223", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", actor, e.Version, CardId: source.Id, TargetId: k.Id, AbilityId: "training-bow-any"));
            if (actor == 0)
            {
                Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "Another player's Open action");
                ExpectBeforeActionOffer(e, k.Id);
                Check(!e.State.Cards[k.Id].Bowed);
            }
            else
            {
                Check(e.State.PendingChoice is null, "His own controller's action never triggers it");
                Check(e.State.Cards[k.Id].Bowed);
            }
        }
    }
}
static void AkodoKakiharaReactsToAttacks()
{
    // Ranged 5 (Carpenter Castle Siege) vs Kakihara's Force 4: a legal target that dies if nothing changes,
    // and survives once his Personal Honor lands on top of his Force before the strength comparison.
    (MatchEngine, MatchState, CardInstance, CardInstance, CardInstance) Siege(Action<CardInstance, MatchState>? tweak = null, bool otherReactor = false)
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, also a legal target for Ranged 5
        if (otherReactor) Add(s, "training-attack-reactor", 1, Zone.Battlefield, province: 0);
        tweak?.Invoke(k, s);
        return (MatchEngine.FromTestState(s), s, castle, k, other);
    }
    GameCommand Attack(MatchEngine e, MatchState s, CardInstance castle, string targetId) =>
        new("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: targetId, AbilityId: "carpenter-castle-siege");
    {
        // Control: nobody reacts, the Attack destroys him (the window opened only because HE could react).
        var (e, s, castle, k, _) = Siege();
        Accept(e, Attack(e, s, castle, k.Id));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Accept(e, Cmd(e, "declineReaction")); // the attacker holds no reaction, so his one pass closes the window (issue #644)
        Check(e.State.Cards[k.Id].Zone != Zone.Battlefield, "Force 4 <= strength 5: destroyed when he does not react");
    }
    {
        // Reacting decides it: +4F makes Force 8 > strength 5 by the time the Attack resolves.
        var (e, s, castle, k, _) = Siege(otherReactor: true); // a second reactor keeps the window open after his reaction, so the strength can be read (issue #644)
        Accept(e, Attack(e, s, castle, k.Id));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        var options = e.QueryActions(1);
        Check(options.Any(a => a.Command?.AbilityId == "kakihara-attack-force-bonus" && a.Enabled), "Offered (enabled) in the Attack window");
        Check(!options.Any(a => a.Command?.AbilityId == "kakihara-force-bonus"), "The non-Attack half is never listed here");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus")); // wrong window
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus"));
        Equal(KakiharaForce(e, k.Id), 8);
        Equal(e.State.PendingAttack?.Strength, 5, "The Attack's own strength is untouched: the bonus is on Kakihara, not a negation");
        Accept(e, Cmd(e, "declineReaction")); // the attacker holds no reaction: this one pass closes the window
        Check(e.State.PendingChoice is null && e.State.PendingAttack is null);
        Equal(e.State.Cards[k.Id].Zone, Zone.Battlefield, "Force 8 > strength 5: the Attack fails");
        Equal(e.State.Cards[k.Id].AbilityUses["kakihara-force-bonus"], e.State.TurnNumber, "The one printed use is spent (shared UsageKey)");
        Check(!e.QueryActions(1).Any(a => a.Command?.AbilityId == "kakihara-attack-force-bonus" && a.Enabled), "and no longer offered");
    }
    {
        // Melee too: Sankaku-Yari's Melee 4 Attack vs the same Force 4.
        foreach (var reacts in new[] { false, true })
        {
            var s = StrongholdBattle("oracle-l5r-1218");
            var wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            var yari = Add(s, "oracle-l5r-6469", 0, Zone.Attached, province: 0, attachedTo: wielder.Id);
            var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: yari.Id, TargetId: k.Id, AbilityId: "sankaku-yari-attack"));
            Equal(e.State.PendingChoice?.Type, "attackReaction");
            if (reacts) Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus")); // nothing is left to react with: the window closes (issue #644)
            else Accept(e, Cmd(e, "declineReaction"));
            Check((e.State.Cards[k.Id].Zone == Zone.Battlefield) == reacts, reacts ? "Melee 4 < Force 8: survives" : "Melee 4 >= Force 4: destroyed");
        }
    }
    {
        // Current Personal Honor again: a PersonalHonor modifier of +1 makes the bonus 5.
        var (e, s, castle, k, _) = Siege((c, _) => c.Modifiers.Add(new("test-source", "test", "PersonalHonor", 1)));
        Accept(e, Attack(e, s, castle, k.Id));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus"));
        Equal(e.State.Cards[k.Id].Modifiers.Single(m => m.Stat == "Force").Amount, 5);
    }
    {
        // The Attack targets a DIFFERENT card: no reactor could matter, so no window even though Kakihara is in play.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Attack(e, s, castle, other.Id));
        Check(e.State.PendingChoice is null, "An Attack at someone else never opens a window for Kakihara");
        Check(e.State.Cards[other.Id].Zone != Zone.Battlefield, "and simply resolves");
    }
    {
        // Another reactor DOES open a window on an Attack at another card: Kakihara's reaction is listed
        // disabled and rejected there, while the same reactor changes nothing when HE is the target.
        var (e, s, castle, k, other) = Siege(otherReactor: true);
        Accept(e, Attack(e, s, castle, other.Id));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Check(e.QueryActions(1).Any(a => a.Command?.AbilityId == "kakihara-attack-force-bonus" && !a.Enabled), "Listed, but disabled: he is not the target");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus"));
    }
    {
        // Dishonorable (+0F), bowed, or spent this turn: no window on an Attack at him, and it destroys him outright.
        foreach (var scenario in new[] { "dishonored", "bowed", "spent" })
        {
            var (e, s, castle, k, _) = Siege((c, state) =>
            {
                if (scenario == "dishonored") c.Dishonorable = true;
                if (scenario == "bowed") c.Bowed = true;
                if (scenario == "spent") c.AbilityUses["kakihara-force-bonus"] = state.TurnNumber;
            });
            Accept(e, Attack(e, s, castle, k.Id));
            Check(e.State.PendingChoice is null, $"{scenario}: no window");
            Check(e.State.Cards[k.Id].Zone != Zone.Battlefield, $"{scenario}: the Attack resolved and destroyed him");
        }
    }
}
static string[] BeforeActionOffers(MatchEngine e, int playerId) => e.QueryActions(playerId).Where(a => a.Type == "useAbility" && a.Enabled).Select(a => a.Command!.AbilityId!).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
// Both players pass through the legal-action listing alone (never a hand-built command), the way a client or the AI
// would: every step must offer the player holding priority a way to pass, and two consecutive passes close the window.
static void PassBothThroughListing(MatchEngine e)
{
    for (var step = 0; step < 6 && e.State.PendingChoice is not null; step++)
    {
        var pass = e.QueryActions(e.State.PriorityPlayerId).SingleOrDefault(a => a.Type == "declineReaction" && a.Enabled);
        Check(pass?.Command is not null, "The player holding priority is always offered a pass -- no deadlock");
        Accept(e, pass!.Command!);
    }
    Check(e.State.PendingChoice is null, "Both players passed: the window closed");
}
// Player 0 (the active player) straightens a bowed Personality of player 1's; player 0 holds Tamoko (negates a
// straighten), player 1 holds a Champion of Thunder in that Personality's unit -- one applicable reaction each.
static (MatchEngine Engine, CardInstance Tamoko, CardInstance Champion, CardInstance Target) StraightenWindow()
{
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var straightener = Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
    var tamoko = Add(s, "training-tamoko", 0, Zone.Battlefield, province: 0);
    var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
    var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: target.Id);
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: straightener.Id, TargetId: target.Id, AbilityId: "training-straighten-at-battle"));
    return (e, tamoko, champion, target);
}
static string BeforeActionTurnPlayerFirstScenario()
{
    var (e, tamoko, champion, target) = StraightenWindow();
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
    Equal(e.State.PendingChoice?.PlayerId, 0, "The window records the acting player");
    Equal(e.State.PriorityPlayerId, 0, "Both players hold a reaction: the player whose turn it is has the first opportunity");
    Check(BeforeActionOffers(e, 0).SequenceEqual(["tamoko-negate"]), "The turn player is offered exactly their own reaction");
    Check(!e.QueryActions(1).Any(a => a.Type == "useAbility"), "The other player is not offered anything before their opportunity");
    Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: target.Id, AbilityId: "champion-of-thunder-negate")); // not their opportunity
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tamoko.Id, AbilityId: "tamoko-negate"));
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "Playing a reaction leaves the window open");
    Equal(e.State.PriorityPlayerId, 1, "...and passes the opportunity to the other player");
    Equal(e.State.ConsecutivePasses, 0);
    Check(BeforeActionOffers(e, 1).SequenceEqual(["champion-of-thunder-negate"]), "Now the other player is offered theirs");
    Check(!e.QueryActions(0).Any(a => a.Type == "useAbility"), "and the first player waits");
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: target.Id, AbilityId: "champion-of-thunder-negate"));
    Equal(e.State.PriorityPlayerId, 0, "Alternating: back to the turn player");
    Check(BeforeActionOffers(e, 0).Length == 0, "Their reaction is spent (once per turn): nothing left to play, only to pass");
    Accept(e, Cmd(e, "declineReaction"));
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "One pass does not close the window");
    Equal(e.State.PriorityPlayerId, 1);
    Accept(e, Cmd(e, "declineReaction"));
    Check(e.State.PendingChoice is null, "Both passed consecutively: it closed");
    Check(e.State.Cards[target.Id].Bowed, "and the deferred straighten resolved -- negated");
    return Json(e.State);
}
static void BeforeActionWindowAlternatesTurnPlayerFirst()
{
    BeforeActionTurnPlayerFirstScenario();
    {
        // One player passes, the other reacts, then the first reacts after all: a reaction resets the pass count, so
        // an opportunity already passed on comes round again.
        var (e, tamoko, champion, target) = StraightenWindow();
        Accept(e, Cmd(e, "declineReaction"));
        Equal(e.State.PriorityPlayerId, 1); Equal(e.State.ConsecutivePasses, 1);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: target.Id, AbilityId: "champion-of-thunder-negate"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        Equal(e.State.PriorityPlayerId, 0, "The first player's earlier pass is forgotten");
        Equal(e.State.ConsecutivePasses, 0);
        Check(BeforeActionOffers(e, 0).SequenceEqual(["tamoko-negate"]));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tamoko.Id, AbilityId: "tamoko-negate"));
        PassBothThroughListing(e);
        Check(e.State.Cards[target.Id].Bowed, "Both negations were played; the straighten never applied");
    }
    {
        // Both players pass immediately: the window closes and the deferred command resolves as if it had never opened.
        var (e, _, _, target) = StraightenWindow();
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, Cmd(e, "declineReaction"));
        Check(e.State.PendingChoice is null && e.State.PendingNegatableAbility is null);
        Check(!e.State.Cards[target.Id].Bowed, "The straighten applied");
    }
    {
        // When only the NON-turn player holds a reaction the turn player has no vacuous first opportunity: the
        // window opens for the only player who can act (every single-reactor window's long-standing behavior).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var straightener = Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: straightener.Id, TargetId: target.Id, AbilityId: "training-straighten-at-battle"));
        Equal(e.State.PriorityPlayerId, 1);
    }
    {
        // The non-turn player is the one ACTING (an Open action): the window still goes to whoever holds a reaction,
        // here the turn player (Kakihara's controller).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 0;
        var source = Add(s, "training-bow-any-source", 1, Zone.Home);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var kakihara = Add(s, "oracle-l5r-223", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: source.Id, TargetId: kakihara.Id, AbilityId: "training-bow-any"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        Equal(e.State.PriorityPlayerId, 0, "Player 0 holds the only reaction and is the turn player");
    }
}
static void BeforeActionWindowSamePlayerReactionsInEitherOrder()
{
    // Champion of Thunder + Kakihara on ONE unit, both belonging to player 1 (the case the old one-window limit refused).
    foreach (var kakiharaFirst in new[] { true, false })
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: k.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
        Equal(e.State.PriorityPlayerId, 1);
        Check(BeforeActionOffers(e, 1).SequenceEqual(["champion-of-thunder-negate", "kakihara-force-bonus"]), "Both are offered at once");
        GameCommand Kakihara() => new("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus");
        GameCommand Champion() => new("useAbility", 1, e.Version, CardId: champion.Id, TargetId: k.Id, AbilityId: "champion-of-thunder-negate");
        Accept(e, kakiharaFirst ? Kakihara() : Champion());
        Accept(e, Cmd(e, "declineReaction")); // player 0 has nothing to play but still holds the opportunity
        Equal(e.State.PriorityPlayerId, 1);
        Check(BeforeActionOffers(e, 1).SequenceEqual([kakiharaFirst ? "champion-of-thunder-negate" : "kakihara-force-bonus"]), "Only the other one is left");
        Reject(e, kakiharaFirst ? Kakihara() : Champion()); // a source is never usable twice beyond its own limit
        Accept(e, kakiharaFirst ? Champion() : Kakihara());
        Check(BeforeActionOffers(e, 1).Length == 0);
        PassBothThroughListing(e);
        Check(!e.State.Cards[k.Id].Bowed, "Champion's negation removed the bow");
        Equal(KakiharaForce(e, k.Id), 8, "and Kakihara's +4 survived a Champion played " + (kakiharaFirst ? "after" : "BEFORE") + " it: his bonus is not an effect of the action");
    }
    // Rokai + Kakihara: both belong to player 1, both applicable to a destroy targeting Kakihara.
    foreach (var kakiharaFirst in new[] { true, false })
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var rokai = Add(s, "training-rokai", 1, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: k.Id, AbilityId: "training-destroy-no-attachments"));
        Check(BeforeActionOffers(e, 1).SequenceEqual(["kakihara-force-bonus", "rokai-negate-destruction"]));
        var kakihara = new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus");
        var rokaiUse = new GameCommand("useAbility", 1, e.Version, CardId: rokai.Id, AbilityId: "rokai-negate-destruction");
        Accept(e, kakiharaFirst ? kakihara : rokaiUse);
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: (kakiharaFirst ? rokai : k).Id, AbilityId: kakiharaFirst ? "rokai-negate-destruction" : "kakihara-force-bonus"));
        PassBothThroughListing(e);
        Equal(e.State.Cards[k.Id].Zone, Zone.Battlefield, "Rokai negated the destruction");
        Equal(e.State.Cards[rokai.Id].Zone, Zone.Dead, "at the cost of Rokai");
        Equal(KakiharaForce(e, k.Id), 8, "and Kakihara has his bonus too");
    }
}
static void BeforeActionWindowReactionCanRemoveAnothersEligibility()
{
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
    var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
    var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: k.Id);
    var suppressor = Add(s, "training-before-action-suppressor", 1, Zone.Battlefield, province: 0);
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
    Check(BeforeActionOffers(e, 1).SequenceEqual(["champion-of-thunder-negate", "kakihara-force-bonus", "training-before-action-suppress"]));
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: suppressor.Id, AbilityId: "training-before-action-suppress"));
    Check(e.State.Cards[k.Id].Bowed && e.State.Cards[champion.Id].Bowed && e.State.Cards[suppressor.Id].Bowed, "Its resolution bowed every source");
    Accept(e, Cmd(e, "declineReaction")); // player 0
    Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "The window stays open");
    Equal(e.State.PriorityPlayerId, 1);
    Check(BeforeActionOffers(e, 1).Length == 0, "What the played reaction changed is reflected at the very next opportunity: nothing is offered as usable");
    Check(e.QueryActions(1).Any(a => a.Command?.AbilityId == "kakihara-force-bonus" && !a.Enabled), "(the bowed reactions are listed, disabled)");
    Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
    Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: k.Id, AbilityId: "champion-of-thunder-negate"));
    PassBothThroughListing(e);
    Equal(e.State.Cards[k.Id].Modifiers.Count, 0, "Kakihara never got his bonus");
}
static void BeforeActionWindowNegationsCompose()
{
    // Brothers of Goemon's "bow" negation on the host and Champion of Thunder's "all" on ANOTHER card, played in
    // either order against one Costly Feud (a Force penalty plus a conditional bow): the second no longer
    // overwrites the first, so the host takes the penalty but never bows.
    foreach (var brothersFirst in new[] { true, false })
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var feud = Add(s, "oracle-l5r-1521", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3: the -3 penalty reaches 0 and would bow
        var brothers = Add(s, "oracle-l5r-1167", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        Add(s, "training-ring-of-air", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: feud.Id, TargetId: host.Id, AbilityId: "costly-feud"));
        Check(BeforeActionOffers(e, 1).SequenceEqual(["brothers-goemon-negate", "champion-of-thunder-negate"]));
        var brothersUse = new GameCommand("useAbility", 1, e.Version, CardId: brothers.Id, AbilityId: "brothers-goemon-negate");
        var championUse = new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: champion.Id, AbilityId: "champion-of-thunder-negate");
        Accept(e, brothersFirst ? brothersUse : championUse);
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: (brothersFirst ? champion : brothers).Id, TargetId: brothersFirst ? champion.Id : null, AbilityId: brothersFirst ? "champion-of-thunder-negate" : "brothers-goemon-negate"));
        Equal(e.State.NegatedEffects.Count, 2, "Both negations are held at once");
        PassBothThroughListing(e);
        Check(e.State.NegatedEffects.Count == 0, "and wiped once the command has resolved");
        Check(!e.State.Cards[host.Id].Bowed, "Brothers' bow negation survived Champion's negation of a different card (" + (brothersFirst ? "played after" : "played before") + " it)");
        Check(e.State.Cards[host.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3), "and the Force penalty still landed in full");
    }
    {
        // Two Champions of Thunder in one unit, each negating on a DIFFERENT card of an action that hits every card in it.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var penalizer = Add(s, "training-unit-penalty-personality", 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 1, Zone.Home);
        var champion1 = Add(s, "oracle-l5r-1292", 1, Zone.Attached, attachedTo: host.Id);
        var champion2 = Add(s, "oracle-l5r-1292", 1, Zone.Attached, attachedTo: host.Id);
        var sibling = Add(s, "training-follower", 1, Zone.Attached, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: penalizer.Id, TargetId: host.Id, AbilityId: "training-unit-force-penalty"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion1.Id, TargetId: sibling.Id, AbilityId: "champion-of-thunder-negate"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion2.Id, TargetId: host.Id, AbilityId: "champion-of-thunder-negate"));
        PassBothThroughListing(e);
        Equal(e.State.Cards[sibling.Id].Modifiers.Count, 0, "The first Champion's choice held");
        Equal(e.State.Cards[host.Id].Modifiers.Count, 0, "and so did the second's");
        Equal(e.State.Cards[champion1.Id].Modifiers.Count, 1, "the cards nobody chose are still penalized");
        Equal(e.State.Cards[champion2.Id].Modifiers.Count, 1);
    }
}
static void BeforeActionWindowListingAndDeterminism()
{
    {
        // Champion of Thunder lists one option per unit member (host + itself), each enabled, to its own controller only.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: host.Id, AbilityId: "training-bow-at-battle"));
        var options = e.QueryActions(1).Where(a => a.Command?.AbilityId == "champion-of-thunder-negate").ToArray();
        Equal(options.Length, 2, "One option per card in the unit");
        Check(options.All(a => a.Enabled) && options.Select(a => a.Command!.TargetId).OrderBy(x => x).SequenceEqual(new[] { host.Id, champion.Id }.OrderBy(x => x)));
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility"), "The acting player is offered nothing to use, only to pass");
        Check(e.QueryActions(0).Any(a => a.Type == "wait"), "(they are told to wait: it is not their opportunity)");
        Accept(e, options[0].Command!);
    }
    {
        // Rokai: a single option, offered only to its own controller.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var rokai = Add(s, "training-rokai", 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: target.Id, AbilityId: "training-destroy-no-attachments"));
        Check(e.QueryActions(1).Any(a => a.Command?.AbilityId == "rokai-negate-destruction" && a.Command.CardId == rokai.Id && a.Enabled));
    }
    {
        // Every step of the two-player window is drivable from the listing alone: never a state where the player
        // holding priority has no legal move.
        var (e, _, _, _) = StraightenWindow();
        PassBothThroughListing(e);
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: k.Id);
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: bower.Id, TargetId: k.Id, AbilityId: "training-bow-at-battle"));
        PassBothThroughListing(e2);
        Check(e2.State.Cards[k.Id].Bowed, "Passing on every reaction lets the action resolve normally");
    }
    // Determinism: the same commands from the same state give byte-identical state (the negation set, the pass count and
    // the alternating priority all live in MatchState, which every Execute round-trips through JSON).
    Equal(BeforeActionTurnPlayerFirstScenario(), BeforeActionTurnPlayerFirstScenario(), "Deterministic");
}
static void BeforeActionWindowOnlyOffersUsableAndApplicableReactions()
{
    // A spent Champion of Thunder, a bowed one, and one in ANOTHER unit: none of them can open a window on its own.
    foreach (var scenario in new[] { "spent", "bowed", "other-unit" })
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var otherHost = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: scenario == "other-unit" ? otherHost.Id : host.Id);
        if (scenario == "spent") champion.AbilityUses["champion-of-thunder-negate"] = s.TurnNumber;
        if (scenario == "bowed") champion.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: host.Id, AbilityId: "training-bow-at-battle"));
        Check(e.State.PendingChoice is null, $"{scenario}: no window for a reaction nobody can use");
        Check(e.State.Cards[host.Id].Bowed, $"{scenario}: the action simply resolved");
    }
    {
        // Inside a window some OTHER reaction opened (Kakihara's), a reaction that does not apply to this action is neither
        // offered nor accepted: Rokai at Home (not at Kakihara's location) and a Champion in another unit.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var rokai = Add(s, "training-rokai", 1, Zone.Home);
        var otherHost = Add(s, "training-crane-samurai", 1, Zone.Home);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, attachedTo: otherHost.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: k.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        Check(BeforeActionOffers(e, 1).SequenceEqual(["kakihara-force-bonus"]), "Only the reaction that applies is offered");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: rokai.Id, AbilityId: "rokai-negate-destruction"));
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: champion.Id, AbilityId: "champion-of-thunder-negate"));
    }
    {
        // The Champion's own controller acting on their own unit is not "another player's action".
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
        var source = Add(s, "training-bow-any-source", 1, Zone.Home);
        var host = Add(s, "training-crane-samurai", 1, Zone.Home);
        Add(s, "oracle-l5r-1292", 1, Zone.Attached, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: source.Id, TargetId: host.Id, AbilityId: "training-bow-any"));
        Check(e.State.PendingChoice is null && e.State.Cards[host.Id].Bowed, "Its own controller's action never opens a Champion window");
    }
}
static void BeforeActionWindowKahokuMinusForceTargetIsAnActionTarget()
{
    // Bayushi Kahoku - exp (issue #313) is Attack-classified, but its TargetId is the separate "-4F" target (an ordinary
    // targeted effect); its Attack's own target rides in CardIds. The -4F target IS "an action targets" its card, so the
    // before-action reactions apply to it (an AttackKind-only exclusion would have silently skipped them), while the
    // Attack's own target still reaches the attack window.
    {
        // Champion of Thunder negates the -4F, Kakihara adds his bonus: Force 4 -> 8 (no attack target chosen).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kahoku = Add(s, "oracle-l5r-807", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: k.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kahoku.Id, TargetId: k.Id, AbilityId: "kahoku-exp-attack"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "The -4F targeting opens the window");
        Check(BeforeActionOffers(e, 1).SequenceEqual(["champion-of-thunder-negate", "kakihara-force-bonus"]));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
        Accept(e, Cmd(e, "declineReaction"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: k.Id, AbilityId: "champion-of-thunder-negate"));
        PassBothThroughListing(e);
        Equal(KakiharaForce(e, k.Id), 8, "-4F negated by Champion, +4 from Kakihara");
    }
    {
        // Without Champion the -4F lands, and Kakihara's +4 offsets it: Force 4 (an action targeting him, not an Attack at him).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kahoku = Add(s, "oracle-l5r-807", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kahoku.Id, TargetId: k.Id, AbilityId: "kahoku-exp-attack"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
        PassBothThroughListing(e);
        Equal(KakiharaForce(e, k.Id), 4, "4 - 4 + 4");
    }
    foreach (var reacts in new[] { false, true })
    {
        // The -4F target IS also the Attack's target (Ranged 3): the before-action reaction (his one use this turn) decides
        // whether the weakened Kakihara -- Force 0 -- or the restored one -- Force 4 -- meets the Attack.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kahoku = Add(s, "oracle-l5r-807", 0, Zone.Battlefield, province: 0);
        var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kahoku.Id, TargetId: k.Id, AbilityId: "kahoku-exp-attack", CardIds: [k.Id]));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        if (reacts) Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-force-bonus"));
        PassBothThroughListing(e);
        Check((e.State.Cards[k.Id].Zone == Zone.Battlefield) == reacts, reacts ? "Force 4 > strength 3: survives" : "Force 0 <= strength 3: destroyed");
    }
}
static void AttackWindowTurnPlayerFirstThenAlternates()
{
    // Ranged 5 (Carpenter Castle Siege) at Kakihara; player 0 (the active player, the attacker) holds a strength boost of
    // their own, player 1 holds Kakihara's reaction. His Personal Honor is 3 (a -1 modifier): Force 4 -> 7 with his bonus.
    var s = StrongholdBattle("oracle-l5r-1218");
    Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
    var castle = Add(s, "training-castle", 0, Zone.Home);
    var reactor = Add(s, "training-attack-reactor", 0, Zone.Home);
    var k = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
    k.Modifiers.Add(new("test-source", "test", "PersonalHonor", -1));
    var e = MatchEngine.FromTestState(s);
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: k.Id, AbilityId: "carpenter-castle-siege"));
    Equal(e.State.PendingChoice?.Type, "attackReaction");
    Equal(e.State.PriorityPlayerId, 0, "The turn player holds a reaction, so has the first opportunity (previously the attacked side always went first)");
    Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus"));
    Accept(e, Cmd(e, "declineReaction"));
    Equal(e.State.PriorityPlayerId, 1);
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: k.Id, AbilityId: "kakihara-attack-force-bonus"));
    Equal(KakiharaForce(e, k.Id), 7);
    Equal(e.State.PriorityPlayerId, 0, "Alternating: the attacker, who had passed, reacts after all");
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: reactor.Id, AbilityId: "training-attack-boost"));
    Equal(e.State.PendingAttack?.Strength, 7);
    Equal(e.State.PriorityPlayerId, 0, "Issue #644: Kakihara's controller holds nothing more, so he is not asked to pass: the attacker, who still holds the negate, is");
    Accept(e, Cmd(e, "declineReaction"));
    Check(e.State.PendingChoice is null);
    Check(e.State.Cards[k.Id].Zone != Zone.Battlefield, "Strength 7 >= Force 7: the last reaction decided it");
}
static void TamokoOpenHonorGain()
{
    // Tamori Tamoko's own Open action (issue #207 follow-up): "if it is not your turn, target an
    // unbowed Personality controlled by the active player: before the turn ends, gain 1 Honor if he
    // was not assigned to attack your provinces this turn."
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 0;
        var tamoko = Add(s, "training-tamoko", 1, Zone.Home);
        var target = Add(s, "training-lion-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: tamoko.Id, TargetId: target.Id, AbilityId: "tamoko-open")); // it IS player 0's own turn
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: tamoko.Id, TargetId: target.Id, AbilityId: "tamoko-open"));
        var honorBefore = e.State.Players[1].Honor;
        while (e.State.Phase != Phase.End) Accept(e, Cmd(e, "pass"));
        Equal(e.State.Players[1].Honor, honorBefore + 1, "Not assigned to attack this turn: gains the Honor");
    }
    {
        // "Open" is only ever usable during Phase.Action in this engine (every Open ability shares
        // this convention) -- the "assigned to attack" flag itself is set directly here instead of
        // walking through a real AttackDeclaration phase, to isolate EndTurn's own check.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 1; s.ActivePlayerId = 0;
        var tamoko = Add(s, "training-tamoko", 1, Zone.Home);
        var target = Add(s, "training-lion-samurai", 0, Zone.Home);
        target.AssignedToAttackTurn = s.TurnNumber;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: tamoko.Id, TargetId: target.Id, AbilityId: "tamoko-open"));
        var honorBefore = e.State.Players[1].Honor;
        while (e.State.Phase != Phase.End) Accept(e, Cmd(e, "pass"));
        Equal(e.State.Players[1].Honor, honorBefore, "Assigned to attack this turn: no Honor gained");
    }
}
static void TogashiMitsuVariableAttack()
{
    {
        // "Fire Battle/Open: Give Mitsu +1F Fire tokens until he has exactly five" -- fills UP TO a
        // target count (BattleOrOpen, either phase), not a flat number like Meiyu/Torazu's own three.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var mitsu = Add(s, "training-mitsu", 0, Zone.Home);
        mitsu.Tokens.Add(new("fixture-token-1", "Fire", "test-fixture", "Force", 1));
        mitsu.Tokens.Add(new("fixture-token-2", "Fire", "test-fixture", "Force", 1));
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mitsu.Id, AbilityId: "mitsu-fire-tokens"));
        Equal(e.State.Cards[mitsu.Id].Tokens.Count(t => t.Type == "Fire"), 5, "Fills up to exactly five");
    }
    {
        // "Fire Battle: destroy zero or more Fire tokens on Mitsu. Ranged Attack with strength equal
        // to the number of tokens destroyed" -- a per-COMMAND variable strength (strengthOverride),
        // chosen via CardIds naming which of Mitsu's own Fire tokens to destroy.
        var s = StrongholdBattle("oracle-l5r-1218");
        var mitsu = Add(s, "training-mitsu", 0, Zone.Battlefield, province: 0);
        var t1 = new CardToken("fixture-token-1", "Fire", "test-fixture", "Force", 1);
        var t2 = new CardToken("fixture-token-2", "Fire", "test-fixture", "Force", 1);
        var t3 = new CardToken("fixture-token-3", "Fire", "test-fixture", "Force", 1);
        mitsu.Tokens.AddRange([t1, t2, t3]);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mitsu.Id, TargetId: enemy.Id, AbilityId: "mitsu-fire-attack", CardIds: [t1.Id, t1.Id])); // duplicated id
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: mitsu.Id, TargetId: enemy.Id, AbilityId: "mitsu-fire-attack", CardIds: [t1.Id, t2.Id]), enemy.Id); // strength 2 < Force 3: legal target, survives
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mitsu.Id, TargetId: enemy.Id, AbilityId: "mitsu-fire-attack", CardIds: [t1.Id, t2.Id, t3.Id]));
        Equal(e.State.Cards[mitsu.Id].Tokens.Count, 0, "Every destroyed token is removed");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Strength 3 (all 3 tokens destroyed) matches the Force-3 target");
    }
    {
        // Destroying zero tokens (no CardIds at all) is legal: a Ranged 0 Attack, no target needed.
        var s = StrongholdBattle("oracle-l5r-1218");
        var mitsu = Add(s, "training-mitsu", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mitsu.Id, AbilityId: "mitsu-fire-attack"));
    }
}
static void TogashiSakataTokenAttack()
{
    {
        // "Limited: give Sakata a +1F Fire or Sake token" -- a UsageKey-shared choice of two
        // effects (the same shape as yayu-bow-attack/yayu-destroy-attack), not two independent uses.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var sakata = Add(s, "training-sakata", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sakata.Id, AbilityId: "sakata-sake-token"));
        Equal(e.State.Cards[sakata.Id].Tokens.Count(t => t.Type == "Sake"), 1);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: sakata.Id, AbilityId: "sakata-fire-token")); // shares its Limited use with sakata-sake-token this turn
    }
    {
        // "Ranged 4 Attack, with +1 strength for each Sake token on Sakata" -- only Sake tokens
        // count, even though his own Limited grants either Fire or Sake (both +1F alike).
        var s = StrongholdBattle("oracle-l5r-1218");
        var sakata = Add(s, "training-sakata", 0, Zone.Battlefield, province: 0);
        sakata.Tokens.Add(new("fixture-sake-1", "Sake", "test-fixture", "Force", 1));
        sakata.Tokens.Add(new("fixture-sake-2", "Sake", "test-fixture", "Force", 1));
        sakata.Tokens.Add(new("fixture-fire-1", "Fire", "test-fixture", "Force", 1)); // doesn't count toward Attack strength
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, no attachments
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sakata.Id, TargetId: enemy.Id, AbilityId: "sakata-attack"));
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "4 base + 2 Sake tokens = strength 6, matching the Force-6 target");
    }
}
static void XijktCavalryDefenseAndBowChoice()
{
    {
        // "Xijkt may assign during Cavalry Maneuvers if you are the Defender" -- he carries no
        // Cavalry keyword of his own, so never eligible while ATTACKING.
        var s = MatchEngine.CreateStarterMatch().State;
        s.Phase = Phase.CavalryDeclaration; s.PriorityPlayerId = 0; s.ActivePlayerId = 0; s.UnresolvedBattlefields = [0];
        var xijkt = Add(s, "training-xijkt", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "attack", player: 0, assignments: new() { [xijkt.Id] = 0 }));
    }
    {
        // Additive-only override: eligible for Cavalry Maneuvers while DEFENDING.
        var s = MatchEngine.CreateStarterMatch().State;
        s.Phase = Phase.CavalryDefense; s.PriorityPlayerId = 1; s.ActivePlayerId = 0; s.UnresolvedBattlefields = [0];
        var xijkt = Add(s, "training-xijkt", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "assignDefense", player: 1, assignments: new() { [xijkt.Id] = 0 }));
        Equal(e.State.Cards[xijkt.Id].Zone, Zone.Battlefield);
    }
    {
        // "Battle: bow Xijkt or one of your Mines, and target an enemy card without attachments:
        // bow it" -- the full cost-choice shape: an eligible unbowed Mine pays the cost instead.
        var s = StrongholdBattle("oracle-l5r-1218");
        var xijkt = Add(s, "training-xijkt", 0, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-mine-holding", 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: xijkt.Id, PerformerId: mine.Id, TargetId: enemy.Id, AbilityId: "xijkt-bow"));
        Check(e.State.Cards[mine.Id].Bowed, "Paid by bowing the Mine, not Xijkt himself");
        Check(!e.State.Cards[xijkt.Id].Bowed);
        Check(e.State.Cards[enemy.Id].Bowed, "The targeted enemy card is bowed");
    }
    {
        // Forced pathway: no eligible Mine exists, so Xijkt himself is the only valid Performer left.
        var s = StrongholdBattle("oracle-l5r-1218");
        var xijkt = Add(s, "training-xijkt", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: xijkt.Id, PerformerId: xijkt.Id, TargetId: enemy.Id, AbilityId: "xijkt-bow"));
        Check(e.State.Cards[xijkt.Id].Bowed, "No eligible Holding: forced to bow Xijkt himself");
    }
}
static void TamoriShoseiBowChoice()
{
    {
        // Gated on controlling a Temple AT ALL before the bow-cost choice is even offered.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var shosei = Add(s, "training-shosei", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: shosei.Id, PerformerId: shosei.Id, AbilityId: "shosei-honor"));
    }
    {
        // A bowed Temple still counts as "controlled" for the gate, but is ineligible as the
        // Performer itself (own-source-or-unbowed-holding-with-keyword requires unbowed) -- forcing
        // Shosei himself to pay the cost.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var shosei = Add(s, "training-shosei", 0, Zone.Home);
        Add(s, "training-temple-holding", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shosei.Id, PerformerId: shosei.Id, AbilityId: "shosei-honor"));
        Equal(e.State.Players[0].Honor, honorBefore + 1);
        Check(e.State.Cards[shosei.Id].Bowed, "The bowed Temple is ineligible as Performer: forced to bow Shosei himself");
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var shosei = Add(s, "training-shosei", 0, Zone.Home);
        var temple = Add(s, "training-temple-holding", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shosei.Id, PerformerId: temple.Id, AbilityId: "shosei-honor"));
        Check(e.State.Cards[temple.Id].Bowed, "Paid by bowing the Temple, not Shosei himself");
        Check(!e.State.Cards[shosei.Id].Bowed);
    }
}
static void RingBlockedCardsReview()
{
    {
        // Moshi Ikako (issue #194's own review): "Fire Battle" was speculatively noted as a possibly
        // Ring-gated Timing -- confirmed, now that Rings are actually built, to be a plain
        // printed-keyword classification instead. Zero new primitives.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var ikako = Add(s, "training-moshi-ikako", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0).Id); // Force 2
        target.Modifiers.Add(new("test-setup", "test-setup", "Force", -1)); // down to Force 1, within Ranged 1
        var tooStrong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, no attachments
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: ikako.Id, TargetId: tooStrong.Id, AbilityId: "moshi-ikako-attack"), tooStrong.Id); // Force 6 > Ranged 1: legal target, survives
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ikako.Id, TargetId: target.Id, AbilityId: "moshi-ikako-attack"));
        Equal(e.State.Cards[target.Id].Zone, Zone.FateDiscard, "Destroys the Force-1 (after modifier) Follower target");
    }
    {
        // Scholarly Hermit (issue #194's own review): needed only a Ring-type check in a discard
        // pile, now trivial with CardType.Ring in place. New Target "own-ring-in-discard".
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var hermit = Add(s, "training-scholarly-hermit", 0, Zone.Attached, attachedTo: host.Id);
        var ring = Add(s, "training-ring-of-water", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: hermit.Id, TargetId: ring.Id, AbilityId: "scholarly-hermit-search"));
        Check(e.State.Players[0].Hand.Contains(ring.Id), "The Ring returns to hand"); Equal(e.State.Cards[ring.Id].Zone, Zone.Hand);
        Equal(e.State.Cards[hermit.Id].Zone, Zone.FateDiscard, "Destroys itself -- a Follower, so its own FateDiscard, not Dead");
    }
}
static void LionClanPersonalityReview()
{
    {
        // Akodo Dairuko, Experienced (issue #198): bow a target enemy Personality with lower Personal
        // Honor; destroy him too if he is Unaligned.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var dairuko = Add(s, "oracle-l5r-10635", 0, Zone.Battlefield, province: 0);
        var unaligned = Add(s, "training-honor-personality", 1, Zone.Battlefield, province: 0); // Clan Unaligned, PH 1 < Dairuko's 5
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: dairuko.Id, TargetId: unaligned.Id, AbilityId: "dairuko-exp-bow"));
        Equal(e.State.Cards[unaligned.Id].Zone, Zone.Dead, "Bowed, then destroyed for being Unaligned");
    }
    {
        // Akodo Furu (issue #198): a bare Chi comparison against the source itself, dual bow/straighten choice.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var furu = Add(s, "oracle-l5r-9823", 0, Zone.Battlefield, province: 0); // Chi 3
        var lowChi = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true); // Chi 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: furu.Id, TargetId: lowChi.Id, AbilityId: "furu-straighten"));
        Check(!e.State.Cards[lowChi.Id].Bowed, "Straightens a Personality with equal Chi");
    }
    {
        // Akodo Kamina, Experienced (issue #198): Ranged 3, or 4 if the target is Lion Clan or Unaligned.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kamina = Add(s, "oracle-l5r-10636", 0, Zone.Battlefield, province: 0);
        var craneTarget = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5 > Ranged 3
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: kamina.Id, TargetId: craneTarget.Id, AbilityId: "kamina-exp-attack"), craneTarget.Id); // no clan bonus: 3 < 5, legal target, survives
        var s2 = StrongholdBattle("oracle-l5r-1218"); s2.ActivePlayerId = 0;
        var kamina2 = Add(s2, "oracle-l5r-10636", 0, Zone.Battlefield, province: 0);
        var lionTarget = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4 <= boosted Ranged 4
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: kamina2.Id, TargetId: lionTarget.Id, AbilityId: "kamina-exp-attack"));
        Equal(e2.State.Cards[lionTarget.Id].Zone, Zone.Dead, "Lion Clan target destroyed with the +1 bonus (Ranged 4)");
    }
    {
        // Akodo Kano (issue #198): Limited grants a Master token; destroying it grants two Additional
        // Battle Actions.
        var s0 = MatchEngine.CreateEconomyScenario().State; s0.Phase = Phase.Action; s0.PriorityPlayerId = 0; s0.ActivePlayerId = 0;
        var kano0 = Add(s0, "oracle-l5r-226", 0, Zone.Home);
        var e0 = MatchEngine.FromTestState(s0);
        Accept(e0, new GameCommand("useAbility", 0, e0.Version, CardId: kano0.Id, AbilityId: "kano-token"));
        Check(e0.State.Cards[kano0.Id].Tokens.Any(t => t.Type == "Master"), "Gains a Master token");

        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kano = Add(s, "oracle-l5r-226", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: kano.Id, AbilityId: "kano-destroy-token")); // no token yet
        kano.Tokens = [new("t1", "Master", "test-fixture")];
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: kano.Id, AbilityId: "kano-destroy-token"));
        Check(!e2.State.Cards[kano.Id].Tokens.Any(t => t.Type == "Master"), "Master token consumed");
        Equal(e2.State.AdditionalActions.Count(g => g.PlayerId == 0), 2, "Two Additional Battle Actions granted");
    }
    {
        // Akodo Michitsu (issue #198): destroy a target enemy attachment.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var michitsu = Add(s, "oracle-l5r-10060", 0, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0).Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: michitsu.Id, TargetId: enemyFollower.Id, AbilityId: "michitsu-destroy"));
        Equal(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "The attachment is destroyed");
    }
    {
        // Akodo Kamina (issue #198): "Tactical Open" maps onto the existing Open Timing; bow for +4F.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var kamina = Add(s, "oracle-l5r-224", 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kamina.Id, TargetId: target.Id, AbilityId: "kamina-boost", CostOption: "bow"));
        Check(e.State.Cards[kamina.Id].Bowed, "Bows itself as the cost");
        Check(e.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == 4), "+4F applied");
    }
    {
        // Akodo Tsudoken, Experienced (issue #198): usable even while bowed; straightens himself
        // unconditionally, plus a dual bow/straighten choice on the target.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var tsudoken = Add(s, "oracle-l5r-302", 0, Zone.Battlefield, province: 0, bowed: true);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tsudoken.Id, TargetId: target.Id, AbilityId: "tsudoken-exp-straighten"));
        Check(!e.State.Cards[tsudoken.Id].Bowed, "Straightens himself even though he was bowed");
        Check(!e.State.Cards[target.Id].Bowed, "Straightens the target too");
    }
    {
        // Akodo Tsudoken, Experienced 2 (issue #198): entry Honor loss, via the real Dynasty-purchase
        // entry path; Tireless straighten of a Lion Clan or Fallen Personality.
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "oracle-l5r-10458"; s.Cards[cardId].FaceUp = true;
        var honorBefore = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 7).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "tsudoken-exp2-entry"));
        Equal(e.State.Players[0].Honor, honorBefore - 3, "Loses 3 Honor");

        var s2 = StrongholdBattle("oracle-l5r-1218"); s2.ActivePlayerId = 0;
        var tsudoken2 = Add(s2, "oracle-l5r-10458", 0, Zone.Battlefield, province: 0, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: tsudoken2.Id, TargetId: tsudoken2.Id, AbilityId: "tsudoken-exp2-straighten"));
        Check(!e2.State.Cards[tsudoken2.Id].Bowed, "Straightens even though bowed (Tireless), targeting himself (Lion Clan)");
    }
    {
        // Akodo Nojima (issue #198): entry Honor loss, via the real Dynasty-purchase entry path.
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "oracle-l5r-10637"; s.Cards[cardId].FaceUp = true;
        var honorBefore = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 4).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "nojima-entry"));
        Equal(e.State.Players[0].Honor, honorBefore - 1, "Loses 1 Honor");
    }
    {
        // Ikoma Aimi, Experienced (issue #198): "Political Limited" maps onto the existing Limited
        // Timing; discard a card, gain Honor equal to its Focus Value.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var aimi = Add(s, "oracle-l5r-10459", 0, Zone.Home);
        var discard = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus 3
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: aimi.Id, TargetId: discard.Id, AbilityId: "aimi-exp-discard"));
        Check(e.State.Cards[aimi.Id].Bowed, "Bows itself as the cost");
        Equal(e.State.Cards[discard.Id].Zone, Zone.FateDiscard, "Discards the chosen card");
        Equal(e.State.Players[0].Honor, honorBefore + 3, "Gains Honor equal to its Focus Value");
    }
    {
        // Ikoma Ayumi (issue #198): entry Honor loss, via the real Dynasty-purchase entry path.
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "oracle-l5r-10462"; s.Cards[cardId].FaceUp = true;
        var honorBefore = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 7).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "ayumi-entry"));
        Equal(e.State.Players[0].Honor, honorBefore - 2, "Loses 2 Honor");
    }
    {
        // Ikoma Masumi (issue #198): target an enemy card without attachments: bow it.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var masumi = Add(s, "oracle-l5r-10062", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: masumi.Id, TargetId: enemy.Id, AbilityId: "masumi-bow"));
        Check(e.State.Cards[enemy.Id].Bowed, "Bows the unattached enemy");
    }
    {
        // Ikoma Shuji (issue #198): Ranged 4, +1 strength against a Spider Clan Personality.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var shuji = Add(s, "oracle-l5r-9824", 0, Zone.Battlefield, province: 0);
        var spider = Add(s, "training-spider-honor-loss", 1, Zone.Battlefield, province: 0); // Force 2, Clan Spider
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shuji.Id, TargetId: spider.Id, AbilityId: "shuji-attack"));
        Equal(e.State.Cards[spider.Id].Zone, Zone.Dead, "Destroys the Force-2 Spider Clan target (Ranged 4, no bonus even needed)");
    }
    {
        // Ikoma Tsukasa (issue #198): the "without attachments" restriction is waived while
        // controlling a Terrain.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var tsukasa = Add(s, "oracle-l5r-3655", 0, Zone.Battlefield, province: 0);
        var terrain = Add(s, "training-terrain", 0, Zone.Battlefield, province: 0);
        var enemyUnitHead = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyUnitHead.Id); // gives the target an attachment
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tsukasa.Id, TargetId: enemyUnitHead.Id, AbilityId: "tsukasa-bow"));
        Check(e.State.Cards[enemyUnitHead.Id].Bowed, "Bows a card WITH attachments, waived because Tsukasa's controller controls a Terrain");
    }
    {
        // Matsu Arata (issue #198): +2F while attacking (attacking-two); Force penalty on a target
        // equal to Arata's Personal Honor.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var arata = Add(s, "oracle-l5r-4892", 0, Zone.Battlefield, province: 0); // Force 3, PH 4
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        Equal(UnitView(e.State, arata.Id).Force, 5, "3 printed + 2 while attacking");
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: arata.Id, TargetId: enemy.Id, AbilityId: "arata-force-penalty"));
        Check(e.State.Cards[enemy.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -4), "Force penalty equal to Arata's own Personal Honor (4)");
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Gains 1 Honor");
    }
    {
        // Matsu Yoshito (issue #198): +1F while attacking, +2F instead if the Defender is Dragon Clan.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var yoshito = Add(s, "oracle-l5r-10295", 0, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Equal(UnitView(e.State, yoshito.Id).Force, 4, "+1F attacking a non-Dragon defender");

        var s2 = StrongholdBattle("oracle-l5r-1218"); s2.ActivePlayerId = 0; s2.Players[1].Clan = "Dragon";
        var yoshito2 = Add(s2, "oracle-l5r-10295", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Equal(UnitView(e2.State, yoshito2.Id).Force, 5, "+2F instead when the Defender is Dragon Clan");
    }
    {
        // Pride of the Hand (issue #198): +1F while controlling a Terrain at its own battlefield.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var pride = Add(s, "oracle-l5r-6064", 0, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Equal(UnitView(e.State, pride.Id).Force, 3, "No Terrain yet");
        var s2 = e.State; Add(s2, "training-terrain", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Equal(UnitView(e2.State, pride.Id).Force, 4, "+1F once its own controller has a Terrain at this battlefield");
    }
}
static void GeneralizedEntryReactionWindow()
{
    {
        // Equip now opens the same "own-enters-play" Reaction window Purchase always has -- and,
        // unlike Purchase (which never cedes priority away from the active player), the window
        // correctly hands priority to the *opponent* once closed, matching Equip's normal
        // any-action-cedes-priority rule (ActingPlayerId set to the opponent when the window opens).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item-entry-reaction", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, Cmd(e, "equip", id: item.Id, sources: payment.SourceIds, target: host.Id));
        Equal(e.State.Cards[item.Id].Zone, Zone.Attached);
        Equal(e.State.PendingChoice?.Type, "entryReaction", "Equip opens the entering card's own Reaction window");
        Equal(e.State.PriorityPlayerId, 0, "Priority has not yet ceded -- the window is still open");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: item.Id, AbilityId: "training-item-entry-reaction"));
        Equal(e.State.Players[0].Honor, honorBefore + 1);
        Check(e.State.PendingChoice is null, "issue #644: the used Reaction left nothing to pass, so the window closed");
        Equal(e.State.PriorityPlayerId, 1, "Once closed, priority correctly cedes to the opponent, unlike Purchase's own-turn windows");
    }
    {
        // Repeatable each time the card enters play: a stale usage record from an earlier entry
        // (simulating "left play, then entered again") does not block this fresh window.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item-entry-reaction", 0, Zone.Hand); item.AbilityUses["training-item-entry-reaction"] = 0;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, Cmd(e, "equip", id: item.Id, sources: payment.SourceIds, target: host.Id));
        Check(e.QueryActions(0).Any(a => a.Command?.AbilityId == "training-item-entry-reaction" && a.Enabled), "Offered and enabled despite the stale earlier-entry usage record");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: item.Id, AbilityId: "training-item-entry-reaction"));
        Equal(e.State.Players[0].Honor, honorBefore + 1, "The reaction actually fires again, not just offered");
    }
    {
        // PlayStrategy's Terrain path opens the same window too, with the same opponent-cede fix.
        var s = Battle(5, 2).State;
        var terrain = Add(s, "training-terrain-entry-reaction", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, Cmd(e, "playStrategy", player: 1, id: terrain.Id));
        Equal(e.State.Cards[terrain.Id].Zone, Zone.Battlefield);
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: terrain.Id, AbilityId: "training-terrain-entry-reaction"));
        Equal(e.State.Players[1].Honor, honorBefore + 1);
        Check(e.State.PendingChoice is null, "issue #644: nothing is left to react with, so the window closed");
        Equal(e.State.PriorityPlayerId, 0, "Priority cedes to the opponent (player 0) once the window closes");
    }
}
static void FollowerAndItemStaticForceHooks()
{
    {
        // A Follower's own static conditional Force ("+2F while defending") stays with the Follower
        // itself and shows up in its own CardForce/view regardless of bow state...
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var defender = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var defFollower = Add(s, "training-follower-force-effect", 1, Zone.Attached, bowed: true, province: 0, attachedTo: defender.Id); // Force 2+2=4
        var atkFollower = Add(s, "training-follower-force-effect", 0, Zone.Attached, province: 0, attachedTo: attacker.Id); // attacking, no bonus: Force 2
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(1).Players[1].Army.Single(c => c.Id == defFollower.Id).Force, 4, "Bowed defending Follower still shows its own +2F for targeting");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == atkFollower.Id).Force, 2, "Attacking (not defending): the conditional bonus does not apply");
        // ...but only joins the unit's BattleForce total when unbowed (or Elite) -- matching every
        // other Follower's existing bowed-suppression rule.
        Equal(e.GetView(1).Players[1].Army.Single(c => c.Id == defender.Id).UnitForce, 3, "Bowed: defender's own Force 3, the bowed Follower's +2F+2F does not join the unit total");
        defFollower.Bowed = false;
        var e2 = MatchEngine.FromTestState(s);
        Equal(e2.GetView(1).Players[1].Army.Single(c => c.Id == defender.Id).UnitForce, 7, "Unbowed: 3 (defender) + 4 (Follower's own Force 2 + its +2F while defending)");
    }
    {
        // An Item's printed Force/Chi and its own conditional bonus to the wielder (Kensai's Blade's
        // shape) always convey to the Personality, whether the Item itself is bowed or not.
        var s = StrongholdBattle("oracle-l5r-1218");
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0); // Force 2, Chi 2
        var blade = Add(s, "training-item-force-effect", 0, Zone.Attached, bowed: true, province: 0, attachedTo: kensai.Id); // +2F/+1C printed, +1F conditional (Kensai)
        var nonKensai = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var blade2 = Add(s, "training-item-force-effect", 1, Zone.Attached, province: 0, attachedTo: nonKensai.Id);
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == kensai.Id).Force, 5, "Base 2 + printed 2 + conditional 1 (Kensai wielder), even though the Item is bowed");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == kensai.Id).Chi, 3, "Base Chi 2 + printed Chi 1, regardless of the Item's own bow state");
        Equal(e.GetView(1).Players[1].Army.Single(c => c.Id == nonKensai.Id).Force, 5, "Base 3 + printed 2, no conditional bonus for a non-Kensai wielder");
    }
}
static void UnitForceBoostPrimitive()
{
    // Senior Infantry Legion's shape: a card that grants a static +1F to every OTHER Follower in its
    // own unit (a Personality and all of its attachments), never to itself, and never leaking to a
    // different unit.
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
    var unitA = Add(s, "training-crane-samurai", 0, Zone.Home);
    var plainFollower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: unitA.Id); // Force 2
    var booster = Add(s, "training-follower-booster", 0, Zone.Attached, attachedTo: unitA.Id); // Force 1, boosts other unit Followers
    var unitB = Add(s, "training-crane-samurai", 0, Zone.Home);
    var otherUnitFollower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: unitB.Id); // different unit -- no boost
    var e = MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == plainFollower.Id).Force, 3, "Plain Follower gets +1F from the booster in the same unit");
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == booster.Id).Force, 1, "The booster itself is not boosted (no other booster present in the unit)");
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == otherUnitFollower.Id).Force, 2, "A Follower in a different unit is unaffected");

    var booster2 = Add(s, "training-follower-booster", 0, Zone.Attached, attachedTo: unitA.Id); // second booster, same unit
    var e2 = MatchEngine.FromTestState(s);
    Equal(e2.GetView(0).Players[0].Home.Single(c => c.Id == booster.Id).Force, 2, "Two boosters in one unit grant each other +1F, not themselves: printed 1 + 1 from the other booster");
    Equal(e2.GetView(0).Players[0].Home.Single(c => c.Id == booster2.Id).Force, 2, "Symmetric: the second booster also gets +1F from the first, not itself");
    Equal(e2.GetView(0).Players[0].Home.Single(c => c.Id == plainFollower.Id).Force, 4, "Plain Follower now gets +1F from each of the two boosters: 2 + 1 + 1");
}
static void AshigaruEliteBoostPrimitive()
{
    // Ashigaru Elite's shape: an activated, tireless, stacking Battle ability -- bow an unbowed Farm
    // to give +1F, for the remainder of the battle, to every Ashigaru-keyword Personality/Follower the
    // performer controls at the current battlefield. Distinct from Senior Infantry Legion's static
    // unit-scoped boost: this is army-scoped (any of the performer's own units at this battlefield,
    // not just one unit) and keyed off a keyword rather than a card type.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var booster = Add(s, "training-ashigaru-boost-source", 0, Zone.Battlefield, province: 0);
    var ashigaruUnit = Add(s, "training-ashigaru-personality", 0, Zone.Battlefield, province: 0); // Force 2
    var ashigaruFollower = Add(s, "training-ashigaru-follower", 0, Zone.Attached, attachedTo: ashigaruUnit.Id, province: 0); // Force 1
    var plainUnit = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, no Ashigaru keyword
    var enemyAshigaru = Add(s, "training-ashigaru-personality", 1, Zone.Battlefield, province: 0); // enemy's own Ashigaru
    var farm1 = Add(s, "training-farm", 0, Zone.Home);
    var farm2 = Add(s, "training-farm", 0, Zone.Home);
    var e = MatchEngine.FromTestState(s);
    var use = new GameCommand("useAbility", 0, e.Version, CardId: booster.Id, AbilityId: "training-ashigaru-boost", TargetId: farm1.Id);
    Reject(e, use with { TargetId = plainUnit.Id }); // not an unbowed Farm
    Accept(e, use);
    Equal(e.State.Cards[farm1.Id].Bowed, true, "Bowing the Farm is the cost");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == ashigaruUnit.Id).Force, 3, "Base 2 + 1 from one use");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == ashigaruFollower.Id).Force, 2, "Base 1 + 1 from one use -- the boost also reaches an Ashigaru Follower");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == plainUnit.Id).Force, 3, "No Ashigaru keyword -- unaffected");
    Equal(e.GetView(1).Players[1].Army.Single(c => c.Id == enemyAshigaru.Id).Force, 2, "The enemy's own Ashigaru unit is unaffected -- scoped to the performer's own cards");
    // Tireless and stacking: usable again in the same battle, bowing a second Farm. Priority passed to
    // the opponent after the first use (like any action), so they must act (or pass) first.
    Accept(e, new GameCommand("pass", 1, e.Version));
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: booster.Id, AbilityId: "training-ashigaru-boost", TargetId: farm2.Id));
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == ashigaruUnit.Id).Force, 4, "Base 2 + 2 from two stacking uses");
    Accept(e, new GameCommand("pass", 1, e.Version));
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: booster.Id, AbilityId: "training-ashigaru-boost", TargetId: farm1.Id)); // already bowed
    // "For the remainder of the battle": once it resolves, the boost does not carry into the next one.
    Resolve(e);
    Equal(e.State.AshigaruBoost[0], 0, "The per-battle counter resets once the battle resolves");
}
static void ArmyBattleMaidenBoostPrimitive()
{
    // Utaku Elite Guard's shape: like Senior Infantry Legion, a static "boost others" bonus -- but
    // keyword-based (Battle Maiden) rather than type-based (Follower), and army-scoped (every OTHER
    // Battle-Maiden card the owner controls at the same battlefield) rather than unit-scoped.
    var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
    var guard = Add(s, "training-utaku-elite-guard", 0, Zone.Battlefield, province: 0); // Force 2, itself Battle Maiden
    var maidenUnit = Add(s, "training-battle-maiden", 0, Zone.Battlefield, province: 0); // Force 2, a different unit
    var otherUnit = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, no keyword
    var maidenFollower = Add(s, "training-battle-maiden-follower", 0, Zone.Attached, attachedTo: otherUnit.Id, province: 0); // Force 1
    var plainFollower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: otherUnit.Id, province: 0); // Force 2, no keyword
    var homeMaiden = Add(s, "training-battle-maiden", 0, Zone.Home); // not at this battlefield
    var enemyMaiden = Add(s, "training-battle-maiden", 1, Zone.Battlefield, province: 0); // enemy's own -- unaffected
    var e = MatchEngine.FromTestState(s);
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == guard.Id).Force, 2, "The guard does not boost itself (no other booster present in the army)");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == maidenUnit.Id).Force, 3, "Base 2 + 1 -- a Battle Maiden Personality in a DIFFERENT unit, same army");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == otherUnit.Id).Force, 3, "No Battle Maiden keyword -- unaffected");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == maidenFollower.Id).Force, 2, "Base 1 + 1 -- the boost also reaches a Battle Maiden Follower");
    Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == plainFollower.Id).Force, 2, "No Battle Maiden keyword -- unaffected");
    Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == homeMaiden.Id).Force, 2, "Not at this battlefield -- army-scoped, so unaffected");
    Equal(e.GetView(1).Players[1].Army.Single(c => c.Id == enemyMaiden.Id).Force, 2, "The enemy's own Battle Maiden is unaffected -- scoped to the performer's own army");

    var guard2 = Add(s, "training-utaku-elite-guard", 0, Zone.Battlefield, province: 0); // second guard, same army
    var e2 = MatchEngine.FromTestState(s);
    Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == guard.Id).Force, 3, "Two guards in one army grant each other +1F, not themselves: base 2 + 1 from the other guard");
    Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == guard2.Id).Force, 3, "Symmetric: the second guard also gets +1F from the first, not itself");
    Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == maidenUnit.Id).Force, 4, "maidenUnit now gets +1F from each of the two guards: 2 + 1 + 1");
}
static void AttachmentRestrictions()
{
    {
        // Asahina House Guard's own shape: will only attach to a Crane Clan Personality with Personal
        // Honor 2+. A Crane Clan target with sufficient Honor succeeds; one without the keyword, or
        // with insufficient Honor, is rejected -- and rejected outright even by the offered-action
        // query (not just at Accept), since AttachmentLegalTarget gates both the same way.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var guard = Add(s, "training-follower-crane-guard", 0, Zone.Hand);
        var craneTarget = Add(s, "training-crane-clan-samurai", 0, Zone.Home); // Crane Clan, PH 3
        var lionTarget = Add(s, "training-lion-samurai", 0, Zone.Home); // no Crane Clan keyword
        var e = MatchEngine.FromTestState(s);
        var equipOption = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == guard.Id);
        Check(equipOption.TargetIds!.SequenceEqual([craneTarget.Id]), "Only the Crane Clan target is offered as legal");
        Reject(e, Cmd(e, "equip", id: guard.Id, sources: e.PaymentOptions(0, 4).First().SourceIds, target: lionTarget.Id));
        Accept(e, Cmd(e, "equip", id: guard.Id, sources: e.PaymentOptions(0, 4).First().SourceIds, target: craneTarget.Id));
        Equal(e.State.Cards[guard.Id].Zone, Zone.Attached);
    }
    {
        // Current Personal Honor, not printed: Dishonorable fixes it at 0 (blocking the Honor
        // Requirement despite printed Personal Honor 3); a Personal-Honor-boosting token still applies
        // once honorable, even against a low printed value, and keeps applying rather than being
        // consumed -- the same token is present, unconsumed, in both scenarios below.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var guard = Add(s, "training-follower-crane-guard", 0, Zone.Hand);
        var dishonored = Add(s, "training-crane-clan-samurai", 0, Zone.Home); dishonored.Dishonorable = true; // printed PH 3, current 0
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: guard.Id, sources: e.PaymentOptions(0, 4).First().SourceIds, target: dishonored.Id));

        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Phase = Phase.Action; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var guard2 = Add(s2, "training-follower-crane-guard", 0, Zone.Hand);
        var lowHonor = Add(s2, "training-crane-clan-low-honor", 0, Zone.Home); // printed PH 1, below the requirement of 2
        lowHonor.Modifiers = [new("test-fixture", "test-fixture", "PersonalHonor", 2)]; // current 1+2=3
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Cmd(e2, "equip", id: guard2.Id, sources: e2.PaymentOptions(0, 4).First().SourceIds, target: lowHonor.Id));
    }
    {
        // "Will not attach to a Water Personality" (Spawn of Vritra's shape).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var follower = Add(s, "training-follower-not-water", 0, Zone.Hand);
        var water = Add(s, "training-water-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: water.Id));
    }
    {
        // "Will only attach to a Human" -- no keyword is ever printed for it; it means "does not have
        // Nonhuman" (core rules Glossary).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var follower = Add(s, "training-follower-human-only", 0, Zone.Hand);
        var nonhuman = Add(s, "training-nonhuman-personality", 0, Zone.Home);
        var human = Add(s, "training-crane-samurai", 0, Zone.Home); // no Nonhuman keyword: Human by default
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: nonhuman.Id));
        Accept(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: human.Id));
    }
    {
        // A named-Personality restriction (e.g. "Attaches to Yotsu Ueda").
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var follower = Add(s, "training-follower-named", 0, Zone.Hand);
        var named = Add(s, "training-crane-veteran", 0, Zone.Home); // Name: "Crane training veteran"
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: other.Id));
        Accept(e, Cmd(e, "equip", id: follower.Id, sources: e.PaymentOptions(0, 2).First().SourceIds, target: named.Id));
    }
}
static void NoAttachmentsFilter()
{
    {
        // Core rule: a Ranged/Melee Attack may target a Personality "without Followers"; an attached Item does NOT block it
        // (only the RequireNoAttachments abilities below need a bare target). The old Reject here was really the Force-4 > Ranged 3
        // strength check, so Force is now held within strength to prove the Item itself changes nothing.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "oracle-l5r-1839", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var withItem = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: withItem.Id);
        withItem.Modifiers = [new("test-fixture", "test-fixture", "Force", -3)]; // keep Force within Ranged 3 so only the attachment could matter
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: withItem.Id, AbilityId: "dark-naga-scouts-attack"));
        Equal(e.State.Cards[withItem.Id].Zone, Zone.Dead, "An attached Item does not make a Personality an illegal Attack target");
    }
    {
        // Mendon (2026-09-24): a Follower TOKEN shields its Personality from Ranged and Melee Attacks exactly like a Follower card (Force is held within strength so only the token could matter)...
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "oracle-l5r-1839", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var withToken = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); withToken.Tokens = [new("t1", "Follower", "test-fixture")];
        withToken.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)]; // Force 2, within Ranged 3: only the token could matter
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: withToken.Id, AbilityId: "dark-naga-scouts-attack"));
        Equal(e.State.Cards[withToken.Id].Zone, Zone.Battlefield, "A Follower token shields its Personality from the Attack");
    }
    {
        // ...while Item, Spell, Weapon and Armor attachments/tokens do not block an Attack (they only count for "without attachments" effects, below).
        foreach (var (kind, attach) in new (string, Action<MatchState, CardInstance>)[]{
            ("Spell",(st,host)=>Add(st,"training-spell",1,Zone.Attached,province:0,attachedTo:host.Id)),
            ("Weapon token",(st,host)=>host.Tokens=[new("t1","Weapon","test-fixture")]),
            ("Armor token",(st,host)=>host.Tokens=[new("t1","Armor","test-fixture")])})
        {
            var s = StrongholdBattle("oracle-l5r-1218");
            var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var attacker = Add(s, "oracle-l5r-1839", 0, Zone.Attached, province: 0, attachedTo: host.Id);
            var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); attach(s, target);
            target.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)];
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: target.Id, AbilityId: "dark-naga-scouts-attack"));
            Equal(e.State.Cards[target.Id].Zone, Zone.Dead, $"A {kind} does not shield a Personality from an Attack");
        }
    }
    {
        // ...and an ordinary stat/status token (Fire) does not either -- still a legal target.
        var s = StrongholdBattle("oracle-l5r-1218");
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "oracle-l5r-1839", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var withFireToken = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); withFireToken.Tokens = [new("t1", "Fire", "test-fixture")];
        withFireToken.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)]; // Force 4-2=2, within Ranged 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: attacker.Id, TargetId: withFireToken.Id, AbilityId: "dark-naga-scouts-attack"));
        Equal(e.State.Cards[withFireToken.Id].Zone, Zone.Dead);
    }
    {
        // The generic RequireNoAttachments flag, layered onto an existing category ("enemy-force"):
        // matches "Target an enemy card without attachments" verbatim. A Personality with an
        // attachment is rejected; a clean one is destroyed; an enemy Follower (an attachment itself)
        // is always a legal target, since an attachment can never itself have attachments.
        var s = StrongholdBattle("oracle-l5r-1218");
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var withItem = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: withItem.Id);
        var clean = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-cavalry-follower", 1, Zone.Attached, province: 0, attachedTo: clean.Id);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: withItem.Id, AbilityId: "training-destroy-no-attachments"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: enemyFollower.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "An attachment is always a legal 'without attachments' target -- FateDiscard, not Dead");
    }
}
static void ProvinceAdjacency()
{
    // Provinces sit in a line, 0-1-2-3 (the user's "Province 1..4"). A destroyed province is skipped
    // -- its former neighbors become directly adjacent instead, compounding across destructions --
    // derived on demand from Destroyed, not tracked as separate routed state ("provinces slide
    // together"). IsLegal(currentProvince, destroyed, targetProvince) checks whether a Personality at
    // targetProvince is a legal target for an ability sourced at currentProvince.
    bool IsLegal(int currentProvince, int[] destroyed, int targetProvince)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = currentProvince; s.UnresolvedBattlefields = [.. new[] { currentProvince, targetProvince }.Distinct()]; s.PriorityPlayerId = 0; s.ActivePlayerId = 1; // player 1 attacks, so player 0's province line is the one the battlefields follow (issue #336)
        foreach (var d in destroyed) s.Players[0].Provinces[d].Destroyed = true;
        var source = Add(s, "training-adjacent-booster", 0, Zone.Battlefield, province: currentProvince);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: targetProvince);
        var e = MatchEngine.FromTestState(s);
        return e.Execute(new GameCommand("useAbility", 0, e.Version, CardId: source.Id, TargetId: target.Id, AbilityId: "training-adjacent-boost")).Accepted;
    }
    // Baseline: 0-1, 1-2, 2-3 only. Not transitively adjacent (0 and 2 are not adjacent).
    Check(IsLegal(0, [], 1), "0 adjacent to 1"); Check(IsLegal(1, [], 0), "1 adjacent to 0 (symmetric)");
    Check(IsLegal(1, [], 2), "1 adjacent to 2"); Check(IsLegal(2, [], 3), "2 adjacent to 3");
    Check(!IsLegal(0, [], 2), "0 not adjacent to 2 with nothing destroyed");
    // Province 2 (index 1) destroyed: 0 and 2 become adjacent; 2-3 is untouched.
    Check(IsLegal(0, [1], 2), "0 adjacent to 2 once province 1 (index 1) is destroyed");
    Check(IsLegal(2, [1], 0), "...symmetric");
    Check(IsLegal(2, [1], 3), "2 still adjacent to 3 (unaffected by index 1's destruction)");
    Check(!IsLegal(0, [1], 3), "0 still not adjacent to 3");
    // Province 3 (index 2) destroyed: 1 and 3 become adjacent.
    Check(IsLegal(1, [2], 3), "1 adjacent to 3 once province 2 (index 2) is destroyed");
    Check(IsLegal(3, [2], 1), "...symmetric");
    // Both middle provinces (index 1 and 2) destroyed: the two ends become adjacent.
    Check(IsLegal(0, [1, 2], 3), "0 adjacent to 3 once both middle provinces are destroyed");
    Check(IsLegal(3, [1, 2], 0), "...symmetric");
    // The user's own closing example: index 1 and index 3 destroyed -- only 0 and 2 remain adjacent.
    Check(IsLegal(0, [1, 3], 2), "0 adjacent to 2 when index 1 and index 3 are both destroyed");
    Check(IsLegal(2, [1, 3], 0), "...symmetric");

    // Not hardcoded to 4 provinces: AdjacentProvinces scans however many entries Provinces actually
    // has, so a player with more than the usual 4 (some Stronghold effect, say) is handled exactly
    // the same way, with no changes needed. Proven directly here with 6.
    bool IsLegalWithProvinces(int provinceCount, int currentProvince, int[] destroyed, int targetProvince)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        while (s.Players[0].Provinces.Count < provinceCount) s.Players[0].Provinces.Add(new() { Index = s.Players[0].Provinces.Count, Strength = 6 });
        s.Phase = Phase.Battle; s.CurrentBattleProvince = currentProvince; s.UnresolvedBattlefields = [.. new[] { currentProvince, targetProvince }.Distinct()]; s.PriorityPlayerId = 0; s.ActivePlayerId = 1; // player 1 attacks, so player 0's province line is the one the battlefields follow (issue #336)
        foreach (var d in destroyed) s.Players[0].Provinces[d].Destroyed = true;
        var source = Add(s, "training-adjacent-booster", 0, Zone.Battlefield, province: currentProvince);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: targetProvince);
        var e = MatchEngine.FromTestState(s);
        return e.Execute(new GameCommand("useAbility", 0, e.Version, CardId: source.Id, TargetId: target.Id, AbilityId: "training-adjacent-boost")).Accepted;
    }
    Check(IsLegalWithProvinces(6, 4, [], 5), "index 4 adjacent to index 5 with 6 provinces");
    Check(!IsLegalWithProvinces(6, 0, [], 5), "index 0 still not adjacent to index 5 (not transitive)");
    Check(IsLegalWithProvinces(6, 3, [4], 5), "index 3 adjacent to index 5 once index 4 (a middle province) is destroyed, with 6 provinces");
}
static void Dueling()
{
    // Steel on Steel / The Courtesy of the Crane: reproduces the user's own worked example end to
    // end -- Doji Tatsuki (Chi 4) focuses Courtesy of the Crane (FV 4) for a total of 8; Matsu Hachiro
    // (Chi 3) focuses Paid Off (FV 4) for a total of 7 -- including hidden-until-Struck focused-card
    // visibility, Courtesy's own Focus Effect, and priority returning to exactly where the challenge
    // was issued (the Lion player's Battle action) once the duel resolves.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = 0; s.PriorityPlayerId = 1;
        var matsu = Add(s, "oracle-l5r-4915", 0, Zone.Battlefield, province: 0); // Matsu Hachiro: Chi 3
        var tatsuki = Add(s, "oracle-l5r-2166", 1, Zone.Battlefield, province: 0); // Doji Tatsuki: Chi 4
        var steelOnSteel = Add(s, "oracle-l5r-7483", 1, Zone.Hand);
        var courtesy = Add(s, "oracle-l5r-7996", 1, Zone.Hand);
        var paidOff = Add(s, "oracle-l5r-5861", 0, Zone.FateDeck); // Focus Value 4
        s.Players[0].FateDeck.Insert(0, paidOff.Id);
        var craneProvincesBefore = s.Players[1].Provinces.Select(p => p.Strength).ToArray();
        var e = MatchEngine.FromTestState(s);

        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: steelOnSteel.Id, PerformerId: tatsuki.Id, TargetId: matsu.Id, AbilityId: "steel-on-steel-duel"));
        Equal(e.State.Cards[steelOnSteel.Id].Zone, Zone.FateDiscard, "Steel on Steel discards immediately, like any Strategy played from hand");
        Check(e.State.PendingDuel is { Stage: "Focusing", ChallengerPlayerId: 1, ChallengedPlayerId: 0, TurnPlayerId: 0 }, "The challenged player (Matsu Hachiro's controller) gets the first Focus-or-Strike option");
        Equal(e.State.PriorityPlayerId, 0);

        // Focusing from the top of the deck: the card cannot be inspected first (Choice:"deck" alone).
        Accept(e, new GameCommand("focus", 0, e.Version, Choice: "deck"));
        Equal(e.State.Cards[paidOff.Id].Zone, Zone.Focused);
        Check(e.GetView(0).Players[0].Focused!.Any(c => c.Id == paidOff.Id), "A player can always inspect their own focused cards");
        Check(!e.GetView(1).Players[0].Focused!.Any(c => c.Id == paidOff.Id), "An opponent cannot see a focused card before a Strike");

        Accept(e, new GameCommand("focus", 1, e.Version, CardId: courtesy.Id));
        Equal(e.State.Cards[courtesy.Id].Zone, Zone.Focused);
        Equal(e.State.PendingDuel!.TurnPlayerId, 0);

        Accept(e, new GameCommand("strike", 0, e.Version));

        Equal(e.State.Cards[matsu.Id].Zone, Zone.Dead, "Matsu Hachiro loses the duel (7) and is destroyed, per Steel on Steel");
        Equal(e.State.Cards[tatsuki.Id].Zone, Zone.Battlefield, "Doji Tatsuki wins the duel (8) and is untouched");
        Equal(e.State.Cards[courtesy.Id].Zone, Zone.FateDiscard, "Focused cards move to the Fate discard pile once the duel resolves");
        Equal(e.State.Cards[paidOff.Id].Zone, Zone.FateDiscard);
        // "As a Focus Effect: After this duel ends, if you won it, give all your provinces +2
        // strength" -- Doji Tatsuki's controller (Crane, player 1) won, so only their provinces gain.
        var craneProvincesAfter = e.State.Players[1].Provinces.Select(p => e.ProvinceStrengthForTest(1, p.Index)).ToArray();
        for (var i = 0; i < craneProvincesBefore.Length; i++) Equal(Batch336Support.StrengthNextTurn(e, 1, i), craneProvincesBefore[i], $"Courtesy of the Crane: province {i} is back to its strength when the turn ends (issue #492 ruling R3)");
        for (var i = 0; i < craneProvincesBefore.Length; i++) Equal(craneProvincesAfter[i], craneProvincesBefore[i] + 2, $"Province {i} should gain +2 strength");
        Check(e.State.PendingDuel is null, "The duel is fully resolved");
        // The game returns to exactly the state it was in when the challenge was issued (the Crane
        // player's Battle action), so priority now returns to the Lion player, to act or pass.
        Equal(e.State.PriorityPlayerId, 0);
    }
    // A refusable challenge (the training-duel-challenge fixture) may simply be declined: the duel
    // never begins, and priority cedes to the challenger's opponent, exactly like any other action.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
        var target = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var performer = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: performer.Id, TargetId: target.Id, AbilityId: "training-duel-challenge"));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept", Refusable: true });
        Accept(e, new GameCommand("declineDuel", 0, e.Version));
        Check(e.State.PendingDuel is null, "A declined duel simply ends");
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "A refused duel has no other effect on either personality");
        Equal(e.State.PriorityPlayerId, 0, "Priority cedes to the challenger's opponent, same as any other action");
    }
    // The four-focus cap: a player who has already focused four times must Strike -- a fifth attempt
    // is rejected atomically.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
        var target = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var performer = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: performer.Id, TargetId: target.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Check(e.State.PendingDuel is { Stage: "Focusing", TurnPlayerId: 0 });
        for (var i = 0; i < 4; i++)
        {
            Accept(e, new GameCommand("focus", 0, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        }
        Equal(e.State.PendingDuel!.TurnPlayerId, 0);
        Reject(e, new GameCommand("focus", 0, e.Version, Choice: "deck"));
    }
    // Duelist tie-break, first case: a lone Duelist wins a tied duel outright (both Chi 3, nothing
    // focused on either side -- an immediate double Strike).
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
        var challenged = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        var challenger = Add(s, "training-duelist-personality", 1, Zone.Battlefield, province: 0); // Chi 3, Duelist
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        // The lone Duelist gets an optional discard-and-refocus before resolution; nothing to
        // discard here, so they decline (CardId:null).
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        Check(e.State.Cards[challenged.Id].Modifiers.Any(m => m.Effect == "training-duel-challenge" && m.Stat == "Force" && m.Amount == -1), "The tie's loser (the non-Duelist) takes the fixture's loss penalty");
        Check(!e.State.Cards[challenger.Id].Modifiers.Any(m => m.Amount == -1), "The lone Duelist wins the tie outright and is untouched");
        Check(e.State.PendingDuel is null);
    }
    // Duelist tie-break, second case: neither side has Duelist -- a tie means both personalities lose.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1;
        var challenged = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        var challenger = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Check(e.State.Cards[challenged.Id].Modifiers.Any(m => m.Amount == -1), "Both sides lose a Duelist-less tie");
        Check(e.State.Cards[challenger.Id].Modifiers.Any(m => m.Amount == -1), "Both sides lose a Duelist-less tie");
        Check(e.State.PendingDuel is null);
    }
}
static void DragonClanPersonalityReview()
{
    {
        // Kitsuki Daisuke (issue #207): the marquee self-performer duel -- no separate Performer,
        // Daisuke himself is the challenger. Also exercises his own printed Duelist keyword's
        // optional re-focus (declined here) before resolution.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var daisuke = Add(s, "oracle-l5r-4406", 1, Zone.Battlefield, province: 0); // Chi 4, Duelist
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: daisuke.Id, TargetId: enemy.Id, AbilityId: "daisuke-duel"));
        Check(e.State.PendingDuel is { Stage: "Focusing", ChallengerPlayerId: 1, ChallengedPlayerId: 0 }, "Daisuke himself is the challenger, no Performer selection");
        Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version)); // Daisuke's own Duelist re-focus, declined
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Home, "Daisuke wins (4 vs 3 Chi) -- the loser moves home");
        Equal(e.State.Cards[daisuke.Id].Zone, Zone.Battlefield, "The winner is untouched");
        Check(e.State.PendingDuel is null);
    }
    {
        // Mirumoto Ichizo, Experienced: the same self-performer duel shape, but destroys the loser
        // instead of moving him home.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var ichizo = Add(s, "oracle-l5r-5104", 1, Zone.Battlefield, province: 0); // Chi 4, Duelist
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: ichizo.Id, TargetId: enemy.Id, AbilityId: "ichizo-exp-duel"));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Ichizo wins and destroys the loser");
    }
    {
        // Mirumoto Shikei: Limited Draw, plus the same self-performer duel/destroy shape as Ichizo.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var shikei = Add(s, "oracle-l5r-5148", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shikei.Id, AbilityId: "shikei-draw"));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1, "Shikei meditates and draws a card");
    }
    {
        // Mirumoto Houken: "Battle/Open: Target a Personality with a Weapon: straighten his unit."
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var houken = Add(s, "oracle-l5r-5101", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var weapon = Add(s, "training-weapon", 1, Zone.Attached, province: 0, attachedTo: host.Id, bowed: true);
        var noWeapon = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: houken.Id, TargetId: noWeapon.Id, AbilityId: "houken-straighten")); // no Weapon
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: houken.Id, TargetId: host.Id, AbilityId: "houken-straighten"));
        Check(!e.State.Cards[host.Id].Bowed && !e.State.Cards[weapon.Id].Bowed, "StraightenUnit straightens both the Personality and his Weapon");
    }
    {
        // Mirumoto Houken, Experienced: even if bowed, straightens his own unit; moves to the
        // current battlefield if opposed there, otherwise home.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var houken = Add(s, "oracle-l5r-10053", 0, Zone.Home, bowed: true);
        var follower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: houken.Id, bowed: true);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // opposed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: houken.Id, AbilityId: "houken-exp-straighten"));
        Check(!e.State.Cards[houken.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "Straightens even though bowed");
        Equal(e.State.Cards[houken.Id].Zone, Zone.Battlefield, "Moves to the current battlefield since he would be opposed there");
    }
    {
        // Mirumoto Michi: even if bowed, reinforces the current battlefield only if opposed there --
        // he starts at Home, nowhere near the battle, and moves in via the new MoveToBattlefield
        // primitive.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var michi = Add(s, "oracle-l5r-10054", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: michi.Id, AbilityId: "michi-move")); // nobody opposing yet
        var s2 = e.State; Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: michi.Id, AbilityId: "michi-move"));
        Equal(e2.State.Cards[michi.Id].Zone, Zone.Battlefield, "Moves to the current battlefield");
        Equal(e2.State.Cards[michi.Id].ProvinceIndex, 0);
        Check(!e2.State.Cards[michi.Id].Bowed, "Straightens his unit since he moved");
    }
    {
        // Togashi Rikyou: the same MoveToBattlefield shape, gated on any enemy units present rather
        // than specifically "opposed".
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var rikyou = Add(s, "oracle-l5r-8568", 0, Zone.Home, bowed: true);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: rikyou.Id, AbilityId: "rikyou-move"));
        Equal(e.State.Cards[rikyou.Id].Zone, Zone.Battlefield);
        Check(!e.State.Cards[rikyou.Id].Bowed);
    }
    {
        // Kitsuki Fujimura: bow self, target a dishonorable Personality (either side), -2 Honor to
        // its controller.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var fujimura = Add(s, "oracle-l5r-4409", 0, Zone.Home);
        var honorable = Add(s, "training-crane-samurai", 1, Zone.Home);
        var dishonorable = Add(s, "training-lion-samurai", 1, Zone.Home); dishonorable.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: fujimura.Id, TargetId: honorable.Id, AbilityId: "fujimura-expose")); // not dishonorable
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: fujimura.Id, TargetId: dishonorable.Id, AbilityId: "fujimura-expose"));
        Check(e.State.Cards[fujimura.Id].Bowed, "Bows itself as the cost");
        Equal(e.State.Players[1].Honor, honorBefore - 2);
    }
    {
        // Mirumoto Kouzei: target an enemy card with equal or lower Force than Kouzei's own current
        // Force (5) -- bow it.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kouzei = Add(s, "oracle-l5r-5125", 0, Zone.Battlefield, province: 0); // Force 5
        var tooStrong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5 -- equal, legal
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kouzei.Id, TargetId: tooStrong.Id, AbilityId: "kouzei-bow"));
        Check(e.State.Cards[tooStrong.Id].Bowed);
    }
    {
        // Mirumoto Mitoshi: target a Personality (either side) with strictly lower Force than
        // Mitoshi's own (4) -- bow or straighten him (a dual choice).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mitoshi = Add(s, "oracle-l5r-9818", 0, Zone.Battlefield, province: 0); // Force 4
        var tooStrong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4 -- not strictly lower
        var lower = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0, bowed: true); // Force 5 -- weaken below 4
        lower.Modifiers = [new("test-setup", "test-setup", "Force", -2)]; // down to Force 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mitoshi.Id, TargetId: tooStrong.Id, AbilityId: "mitoshi-straighten"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mitoshi.Id, TargetId: lower.Id, AbilityId: "mitoshi-straighten"));
        Check(!e.State.Cards[lower.Id].Bowed);
    }
    {
        // Mirumoto Hatsuto: target an enemy Personality or Follower without the Cavalry keyword --
        // "Infantry" is not a printed keyword, it just means not Cavalry.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var hatsuto = Add(s, "oracle-l5r-10844", 0, Zone.Battlefield, province: 0);
        var cavalry = Add(s, "training-cavalry", 1, Zone.Battlefield, province: 0);
        var infantry = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: hatsuto.Id, TargetId: cavalry.Id, AbilityId: "hatsuto-weaken")); // has Cavalry
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: hatsuto.Id, TargetId: infantry.Id, AbilityId: "hatsuto-weaken"));
        Check(e.State.Cards[infantry.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3));
    }
    {
        // Mirumoto Kojinrue: bow self (simplified from "bow Kojinrue or one of his Weapons"),
        // target an enemy card without attachments, destroy it.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kojinrue = Add(s, "oracle-l5r-5123", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kojinrue.Id, TargetId: target.Id, AbilityId: "kojinrue-destroy"));
        Check(e.State.Cards[kojinrue.Id].Bowed); Equal(e.State.Cards[target.Id].Zone, Zone.Dead);
    }
    {
        // Mirumoto Yozo, Experienced: the exact existing enemy-card-without-attachments category;
        // removed from the game instead of merely destroyed when the target is Lion Clan.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var yozo = Add(s, "oracle-l5r-5178", 0, Zone.Battlefield, province: 0);
        var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: yozo.Id, TargetId: lion.Id, AbilityId: "yozo-exp-destroy"));
        Equal(e.State.Cards[lion.Id].Zone, Zone.RemovedFromGame, "A Lion Clan target is removed from the game, not merely destroyed");
    }
    {
        // Kitsuki Nubane: target an attachment, bow it.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var nubane = Add(s, "oracle-l5r-4429", 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Attached, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nubane.Id, TargetId: item.Id, AbilityId: "nubane-bow"));
        Check(e.State.Cards[item.Id].Bowed);
    }
    {
        // Kitsuki Nakai, Experienced: -2F, or -3F if the target is dishonorable.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var nakai = Add(s, "oracle-l5r-10629", 0, Zone.Home);
        var dishonorable = Add(s, "training-crane-samurai", 1, Zone.Home); dishonorable.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: nakai.Id, TargetId: dishonorable.Id, AbilityId: "nakai-exp-weaken"));
        Check(e.State.Cards[dishonorable.Id].Modifiers.Any(m => m.Amount == -3));
    }
    {
        // Kitsuki Kinaro, Experienced: bow self, target a Personality with lower Personal Honor;
        // +1 Honor conditional on controlling a specific named card (not present here).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var kinaro = Add(s, "oracle-l5r-10453", 0, Zone.Home); // PH 3
        var lowerHonor = Add(s, "training-lion-samurai", 1, Zone.Home); // PH 2
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinaro.Id, TargetId: lowerHonor.Id, AbilityId: "kinaro-exp-bow"));
        Check(e.State.Cards[kinaro.Id].Bowed); Check(e.State.Cards[lowerHonor.Id].Bowed);
        Equal(e.State.Players[0].Honor, honorBefore, "No Kakita Ichigiku in play -- no bonus Honor");
    }
    {
        // Mirumoto Bojan / Mirumoto Kazuya / Togashi Noboru, the Shattered Star: MANDATORY entry
        // Honor loss (issue #210 -- no printed "Reaction:" prefix, so CardDefinition.EntryHonorLoss
        // applies it automatically on entry, the same way Destined's own draw already is, never an
        // optional choice) -- exercised through a real Purchase, the same way
        // MeiyuEntersPlayReaction does.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var bojanId = PublishedContent.PersonalityWave9.Cards.Single(c => c.Name == "Mirumoto Bojan").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = bojanId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        var payment = e.PaymentOptions(0, 5).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.Players[0].Honor, honorBefore - 1, "Automatic, no choice offered");
        Check(e.State.PendingChoice is null, "Bojan has no own-enters-play Reaction of his own to offer");
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var kazuyaId = PublishedContent.PersonalityWave9.Cards.Single(c => c.Name == "Mirumoto Kazuya").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = kazuyaId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        var payment = e.PaymentOptions(0, 5).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.Players[0].Honor, honorBefore - 2, "Automatic, no choice offered");
    }
    {
        // Togashi Noboru, the Shattered Star, Experienced: the same mandatory entry Honor loss,
        // then Fire Battle: bow a target enemy Personality and take a Force bonus equal to its Force.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var noboruId = PublishedContent.PersonalityWave9.Cards.Single(c => c.Name == "Togashi Noboru, the Shattered Star").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = noboruId; s.Cards[cardId].FaceUp = true;
        var e0 = MatchEngine.FromTestState(s);
        var honorBefore = e0.State.Players[0].Honor;
        var payment = e0.PaymentOptions(0, 9).First();
        Accept(e0, new GameCommand("purchase", 0, e0.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e0.State.Players[0].Honor, honorBefore - 3, "Automatic, no choice offered");
        // Now move on to the Battle ability, at the current battlefield.
        var s2 = e0.State; s2.Phase = Phase.Battle; s2.CurrentBattleProvince = 0; s2.UnresolvedBattlefields = [0]; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var noboru = s2.Cards[cardId]; noboru.Zone = Zone.Battlefield; noboru.ProvinceIndex = 0; // Force 3
        var target = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s2);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: noboru.Id, TargetId: target.Id, AbilityId: "noboru-shattered-star-weaken"));
        Check(e.State.Cards[target.Id].Bowed);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == noboru.Id).Force, 8, "Base Force 3 plus the target's own Force 5");
    }
    {
        // Togashi Mihato: target an enemy Personality, move him home.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mihato = Add(s, "oracle-l5r-10289", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mihato.Id, TargetId: target.Id, AbilityId: "mihato-move"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Home);
    }
    {
        // Togashi Noboru, Experienced 2: Political Limited -- take the Imperial Favor outright.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var noboru = Add(s, "oracle-l5r-10847", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: noboru.Id, AbilityId: "noboru-exp2-favor"));
        Equal(e.State.FavorPlayerId, 0);
    }
    {
        // Togashi Noritada: Invest 3 Gold for two +1F Fire tokens, the exact existing Invest/Fire-
        // token patterns composed together -- exercised through a real Purchase, same as
        // Bojan/Kazuya above, since Invest reuses the same own-enters-play Reaction window.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var noritadaId = PublishedContent.PersonalityWave9.Cards.Single(c => c.Name == "Togashi Noritada").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = noritadaId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 4).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "noritada-invest", SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
        Equal(e.State.Cards[cardId].Tokens.Count(t => t.Type == "Fire"), 2);
    }
    {
        // Togashi Obote: Void Limited -- discard a card, Force bonus equal to its Focus Value.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var obote = Add(s, "oracle-l5r-10848", 0, Zone.Home);
        var card = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus Value 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: obote.Id, TargetId: card.Id, AbilityId: "obote-boost"));
        Check(e.State.Cards[obote.Id].Modifiers.Any(m => m.Amount == 3));
    }
    {
        // Togashi Korimi, Experienced 2: Limited, draw a card.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var korimi = Add(s, "oracle-l5r-10222", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: korimi.Id, AbilityId: "korimi-exp2-draw"));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1);
    }
    {
        // Togashi Kasuru: bow self; if attacking, draw a card.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var kasuru = Add(s, "oracle-l5r-9821", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kasuru.Id, AbilityId: "kasuru-draw"));
        Check(e.State.Cards[kasuru.Id].Bowed);
        Equal(e.State.Players[0].Hand.Count, handBefore + 1, "Kasuru is attacking (owner is the active player)");
    }
    {
        // Kitsuki Miroken: +2F while opposing a dishonorable or Gaijin card.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var miroken = Add(s, "oracle-l5r-10843", 0, Zone.Battlefield, province: 0); // base Force 2
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == miroken.Id).Force, 2, "No opposing dishonorable/Gaijin card yet");
        var s2 = e.State; Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Dishonorable = true;
        var e2 = MatchEngine.FromTestState(s2);
        Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == miroken.Id).Force, 4, "+2F opposing a dishonorable card");
    }
    {
        // Tamori Kazushige / Togashi Korimi: +2F while defending.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1; // owner 0 is defending
        var kazushige = Add(s, "oracle-l5r-7777", 0, Zone.Battlefield, province: 0); // base Force 3
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == kazushige.Id).Force, 5, "+2F while defending");
    }
    {
        // Tamori Muzu: your OTHER Earth Personalities have +1F while defending (army-scoped, not
        // unit-scoped); Muzu himself does not grant it to himself.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0; // owner 0 is defending
        var muzu = Add(s, "oracle-l5r-7783", 0, Zone.Battlefield, province: 0); // base Force 3, Earth
        var other = Add(s, "oracle-l5r-7777", 0, Zone.Battlefield, province: 0); // Kazushige, base Force 3, Earth
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == other.Id).Force, 6, "Kazushige's own +2F defending, plus Muzu's own +1F");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == muzu.Id).Force, 3, "Muzu does not boost himself");
        // Muzu's own Battle ability: bow a target enemy card if it is attacking or has no
        // attachments; always gain 1 Honor. Owner 1 is the active player, so host is attacking.
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: muzu.Id, TargetId: host.Id, AbilityId: "muzu-bow"));
        Check(e.State.Cards[host.Id].Bowed, "Attacking (owner 1 is the active player) -- bowed regardless of its attachment");
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Honor gained regardless");
    }
    {
        // Togashi Hiroto: Reaction entry Fire tokens (four, unlike Meiyu/Torazu's three) --
        // exercised through a real Purchase, same as Bojan/Kazuya above -- then Battle: target an
        // enemy card without attachments (the bare enemy-card category plus RequireNoAttachments)
        // -- bow it, then also destroy it if it is itself an attachment (#748).
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var hirotoId = PublishedContent.PersonalityWave9.Cards.Single(c => c.Name == "Togashi Hiroto").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = hirotoId; s.Cards[cardId].FaceUp = true;
        var e0 = MatchEngine.FromTestState(s);
        var payment = e0.PaymentOptions(0, 8).First();
        Accept(e0, new GameCommand("purchase", 0, e0.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Accept(e0, new GameCommand("useAbility", 0, e0.Version, CardId: cardId, AbilityId: "hiroto-fire-tokens"));
        Equal(e0.State.Cards[cardId].Tokens.Count(t => t.Type == "Fire"), 4);
        var s2 = e0.State; s2.Phase = Phase.Battle; s2.CurrentBattleProvince = 0; s2.UnresolvedBattlefields = [0]; s2.PriorityPlayerId = 0; s2.ActivePlayerId = 0;
        var hiroto = s2.Cards[cardId]; hiroto.Zone = Zone.Battlefield; hiroto.ProvinceIndex = 0;
        var host = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var attachedItem = Add(s2, "training-item", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, CardId: hiroto.Id, TargetId: host.Id, AbilityId: "hiroto-bow")); // host has an attachment
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: hiroto.Id, TargetId: attachedItem.Id, AbilityId: "hiroto-bow"));
        Equal(e2.State.Cards[attachedItem.Id].Zone, Zone.FateDiscard, "Bowed, then destroyed, since the target was itself an attachment");
    }
    {
        // Togashi Korimi, Experienced: while defending, always negates her own bowing (an unlimited
        // extension of Stalwart's own shape) -- tested against a real bow-inflicting ability
        // (Tamori Jinai's own Battle bow, from the other side).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1; // owner 0 defending
        var korimi = Add(s, "oracle-l5r-9822", 0, Zone.Battlefield, province: 0);
        var jinai = Add(s, "oracle-l5r-10633", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: jinai.Id, TargetId: korimi.Id, AbilityId: "jinai-bow"));
        Check(!e.State.Cards[korimi.Id].Bowed, "While defending, Korimi always negates the bow");
    }
    {
        // Togashi Korimi, Experienced's own Battle ability: target an enemy card without unbowed
        // attachments (an already-bowed attachment still qualifies, unlike ordinary "without
        // attachments") -- bow it.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var korimi = Add(s, "oracle-l5r-9822", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var bowedItem = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: host.Id, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: korimi.Id, TargetId: host.Id, AbilityId: "korimi-exp-bow"));
        Check(e.State.Cards[host.Id].Bowed, "Only bowed attachments -- still a legal target");
    }
    {
        // Tamori Jinai: Earth Battle, the exact existing enemy-card-without-attachments category.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var jinai = Add(s, "oracle-l5r-10633", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: jinai.Id, TargetId: target.Id, AbilityId: "jinai-bow"));
        Check(e.State.Cards[target.Id].Bowed);
    }
    {
        // Mirumoto Yonekura / Tamori Seiken: zero new primitives -- pure keyword reminder text, no
        // Abilities of their own at all.
        Check(CardCatalog.Get("oracle-l5r-10286").Abilities is { Length: 0 });
        Check(CardCatalog.Get("oracle-l5r-7790").Abilities is { Length: 0 });
    }
}
static void DuelWonReaction()
{
    {
        // Kitsuki Daisuke, Experienced: no duel-issuing ability of his own (unlike base Daisuke) --
        // he wins a duel issued by something else entirely (the training-duel-challenge fixture,
        // choosing him as performer), then his own "after this duel resolved" Reaction is offered.
        // Used here; the window stays open (matching MeiyuEntersPlayReaction's own precedent) until
        // two FRESH consecutive passes close it, since using the ability itself resets the streak.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var daisuke = Add(s, "oracle-l5r-4407", 1, Zone.Battlefield, province: 0); // Chi 4, Duelist
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: daisuke.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version)); // Daisuke's own Duelist re-focus, declined
        Check(e.State.PendingDuel is null, "The duel itself is fully resolved");
        Equal(e.State.PendingChoice?.Type, "duelWonReaction");
        Equal(e.State.PendingChoice?.CardId, daisuke.Id, "Scoped to the specific card that won");
        Equal(e.State.PriorityPlayerId, 1, "Daisuke's own controller (the winner) goes first");
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: daisuke.Id, AbilityId: "daisuke-exp-draw"));
        Equal(e.State.Players[1].Hand.Count, handBefore + 1);
        Check(e.State.PendingChoice is null, "Issue #644: nothing is left to react with, so the window closes without a pass");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: daisuke.Id, AbilityId: "daisuke-exp-draw")); // once only
        Equal(e.State.PriorityPlayerId, 0, "Resumes exactly where the challenge was issued -- the Lion-side convention, here the challenger's opponent");
    }
    {
        // Kitsuki Horume: the same shape, declined outright this time -- no Honor gained.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var horume = Add(s, "oracle-l5r-4412", 1, Zone.Battlefield, province: 0); // Chi 4, Duelist
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: horume.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the opponent holds no reaction: this one pass closes the window (issue #644)
        Check(e.State.PendingChoice is null);
        Equal(e.State.Players[1].Honor, honorBefore, "Declined -- no Honor gained");
    }
    {
        // Kitsuki Jakuei: the Reaction half, used to take the Imperial Favor.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var jakuei = Add(s, "oracle-l5r-4416", 1, Zone.Battlefield, province: 0); // Chi 4, Duelist
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: jakuei.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: jakuei.Id, AbilityId: "jakuei-favor"));
        Equal(e.State.FavorPlayerId, 1);
        Check(e.State.PendingChoice is null, "nobody holds another reaction, so the window closes without a pass (issue #644)");
    }
    {
        // Kitsuki Jakuei's own separate Political Battle ability: bow only, no Favor involved.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var jakuei = Add(s, "oracle-l5r-4416", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: jakuei.Id, TargetId: target.Id, AbilityId: "jakuei-bow"));
        Check(e.State.Cards[target.Id].Bowed); Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Not moved -- the plain half");
    }
    {
        // Discarding the Favor for the bonus is rejected without controlling it...
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var jakuei = Add(s, "oracle-l5r-4416", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: jakuei.Id, TargetId: target.Id, AbilityId: "jakuei-bow-favor"));
        // ...but with it, bows the target AND moves him home bowed -- the whole reason it's worth
        // discarding the Favor here, unlike the plain rulebook favor-battle Player ability (which
        // moves an enemy attacker home WITHOUT bowing him).
        var s2 = e.State; s2.FavorPlayerId = 0;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: jakuei.Id, TargetId: target.Id, AbilityId: "jakuei-bow-favor"));
        Check(e2.State.Cards[target.Id].Bowed, "Moved home bowed, not straightened");
        Equal(e2.State.Cards[target.Id].Zone, Zone.Home);
        Check(e2.State.FavorPlayerId is null, "The Favor is discarded");
    }
}
static void PlayerTargetingPrimitive()
{
    {
        // Generic fixture: mandatory entry Honor loss (no printed "Reaction:" prefix) -- automatic,
        // exercised through a real Purchase, the same way Bojan/Kazuya/Noboru-Shattered-Star already are.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-entry-honor-loss-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.Players[0].Honor, honorBefore - 3, "Automatic, no choice offered");
        Check(e.State.PendingChoice is null, "No entering-play Reaction of its own to offer");
    }
    {
        // Generic fixture: the "choose a player" Reaction pair -- choosing the OPPONENT to lose Honor.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-choose-player-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        var opponentHonorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "training-choose-player-lose", TargetPlayerId: 1));
        Equal(e.State.Players[1].Honor, opponentHonorBefore - 1, "The chosen (opponent) player loses 1 Honor");
        Check(e.State.PendingChoice is null, "issue #644: the window closed: the paired half is spent too"); // formerly two passes
        // Shared UsageKey: the paired "gain" half is now blocked, once per turn -- same stamp,
        // not a separate one per catalog entry.
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "training-choose-player-gain", TargetPlayerId: 1));
        Check(e.State.PendingChoice is null);
    }
    {
        // Generic fixture: choosing OWN controller to gain Honor -- ordinary GainHonor, honorable performer.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-choose-player-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "training-choose-player-gain", TargetPlayerId: 0));
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Honorable performer: own controller gains Honor normally");
        Check(!e.State.Cards[cardId].Dishonorable, "Never dishonorable to begin with");
    }
    {
        // Generic fixture: choosing OWN controller to "gain" while the performer is itself Dishonorable
        // -- ApplyPlayerHonorChoice substitutes Rehonor instead of granting Honor (Doji Shunya's own
        // GainHonorFromPerformer shape), the user's own worked example ("if Horume were dishonorable...
        // the player would rehonor Horume").
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-choose-player-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        // MatchEngine.State returns a fresh copy on every access -- mutating it directly is a no-op
        // against the live engine. Snapshot it, mutate the snapshot, then rebuild the engine from it.
        var dishonoredState = e.State; dishonoredState.Cards[cardId].Dishonorable = true;
        var e2 = MatchEngine.FromTestState(dishonoredState);
        var honorBefore = e2.State.Players[0].Honor;
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: cardId, AbilityId: "training-choose-player-gain", TargetPlayerId: 0));
        Equal(e2.State.Players[0].Honor, honorBefore, "Dishonorable performer: gain negated");
        Check(!e2.State.Cards[cardId].Dishonorable, "...and rehonored instead");
    }
    {
        // Generic fixture: choosing a LOSS against the performer's OWN controller is a plain Honor
        // change regardless of the performer's own Dishonorable status -- Rehonor only substitutes for
        // a gain targeting the performer's own controller, never for a loss.
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-choose-player-personality"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 3).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        var dishonoredState = e.State; dishonoredState.Cards[cardId].Dishonorable = true;
        var e2 = MatchEngine.FromTestState(dishonoredState);
        var honorBefore = e2.State.Players[0].Honor;
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: cardId, AbilityId: "training-choose-player-lose", TargetPlayerId: 0));
        Equal(e2.State.Players[0].Honor, honorBefore - 1, "Own controller still loses 1 Honor, plainly");
        Check(e2.State.Cards[cardId].Dishonorable, "Still dishonorable -- Rehonor never applies to a loss");
    }
    {
        // Kitsuki Horume's own real first ability: the same "choose a player, gain or lose 1 Honor"
        // shape, exercised via a real Purchase, then his Dishonor/Rehonor interaction.
        var s = MatchEngine.CreateEconomyScenario().State; s.Players[0].Honor = 10; // meets his printed Honor Requirement
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home); // enough Gold for his 7 cost
        var horumeId = PublishedContent.PersonalityWave10.Cards.Single(c => c.Name == "Kitsuki Horume").Id;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = horumeId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 7).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        var opponentHonorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "horume-lose", TargetPlayerId: 1));
        Equal(e.State.Players[1].Honor, opponentHonorBefore - 1, "Horume's controller chooses the opponent to lose 1 Honor");
        Check(e.State.PendingChoice is null, "issue #644: Horume's reaction is spent (gain or lose), so the window closed");

        var s2 = MatchEngine.CreateEconomyScenario().State; s2.Players[0].Honor = 10;
        for (var i = 0; i < 8; i++) Add(s2, "training-marketplace", 0, Zone.Home);
        var cardId2 = s2.Players[0].Provinces[0].CardId!;
        s2.Cards[cardId2].DefinitionId = horumeId; s2.Cards[cardId2].FaceUp = true;
        var e2 = MatchEngine.FromTestState(s2);
        var payment2 = e2.PaymentOptions(0, 7).First();
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: cardId2, SourceIds: payment2.SourceIds));
        var dishonoredState = e2.State; dishonoredState.Cards[cardId2].Dishonorable = true;
        var e3 = MatchEngine.FromTestState(dishonoredState);
        var honorBefore = e3.State.Players[0].Honor;
        Accept(e3, new GameCommand("useAbility", 0, e3.Version, CardId: cardId2, AbilityId: "horume-gain", TargetPlayerId: 0));
        Equal(e3.State.Players[0].Honor, honorBefore, "Dishonorable Horume: gain negated");
        Check(!e3.State.Cards[cardId2].Dishonorable, "...and rehonored instead -- 'if Horume were dishonorable dead, brought back into play, instead of gaining honor, the player would rehonor Horume'");
    }
}
static void ProtectionPrimitive()
{
    {
        // Protection blocks the Attack-strength-negation Reaction: the exact AttackReactionWindow
        // scenario, except the attacking Castle is protected beforehand -- training-attack-negate is
        // still a legal Reaction to play (StalwartNegatesBow's own precedent: negating IS legal, it
        // just becomes a no-op), so the window still opens and closes normally, but the attack still
        // hits at full, unnegated strength.
        var s = StrongholdBattle("oracle-l5r-1218");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        s.Cards[s.Players[0].StrongholdId!].Protections = [new("test-fixture", "Turn")]; // Carpenter Castle itself performs its action; the chosen Castle/Siege card is only bowed
        var reactor = Add(s, "training-attack-reactor", 1, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: s.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        Equal(e.State.PendingAttack?.SourceId, s.Players[0].StrongholdId, "PendingAttack now records the actual performer generically: the Stronghold that prints Carpenter Castle's action");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: reactor.Id, AbilityId: "training-attack-negate"));
        Equal(e.State.PendingAttack?.Strength, 5, "Protection negates the negation -- strength is untouched");
        Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds no reaction, so the reactor's owner alone passes (issue #644)
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "Unnegated: the attack still hits");
    }
    {
        // Protection blocks Effortless Counterattack's own Additional-Action negation: the exact
        // AdditionalActionPrimitive scenario, except Kensai (the Weapon's own wielder, not the Weapon
        // itself) is protected beforehand.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0, bowed: true);
        kensai.Protections = [new("test-fixture", "Turn")];
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: kensai.Id, bowed: true);
        var enemyHost = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyHost.Id); // Force 2
        var again = Add(s, "oracle-l5r-127", 0, Zone.Hand);
        var counter = Add(s, "oracle-l5r-2283", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: again.Id, PerformerId: kensai.Id, AbilityId: "again-boost"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon.Id, TargetId: enemy.Id, AbilityId: "training-weapon-attack"));
        Equal(e.State.PendingChoice?.Type, "additionalActionReaction");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: counter.Id, AbilityId: "effortless-counterattack-negate"));
        Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Accept(e, new GameCommand("declineReaction", 1, e.Version));
        Check(e.State.PendingAdditionalAction is null, "Resolved normally once the window closed");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.FateDiscard, "Unnegated: the attack still resolves and hits (a Follower's own destruction lands in the Fate discard pile), because the Weapon's own wielder is protected");
        Check(!e.State.Players[1].Hand.Contains(counter.Id), "Still discarded after use -- playing it was legal, only its own effect was negated");
    }
    {
        // Turn-duration Protection clears at FinishTurn, the same as a Turn-duration GrantedKeyword.
        var s = MatchEngine.CreateEconomyScenario().State;
        var p = Add(s, "training-crane-samurai", 0, Zone.Home);
        p.Protections = [new("test-fixture", "Turn"), new("test-fixture", "Permanent")];
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Cards[p.Id].Protections.Count, 1);
        Equal(e.State.Cards[p.Id].Protections[0].Duration, "Permanent");
    }
    {
        // Tamori Takeshi's own real Open ability: grants Protection to a target Personality.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var takeshi = Add(s, "oracle-l5r-10055", 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.Cards[target.Id].Protections.Any(), "Not protected yet");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: takeshi.Id, TargetId: target.Id, AbilityId: "takeshi-protect"));
        Check(e.State.Cards[target.Id].Protections.Any(p => p.Source == takeshi.Id && p.Duration == "Turn"), "Protected for the rest of the turn");
    }
}
static void BrashForceRedirectAndBowReactions()
{
    {
        // Brash: the Defender may draw a card, independently, once per Brash card assigned to
        // attack -- regardless of how many different provinces they were sent to.
        var s = MatchEngine.CreateStarterMatch().State; s.Phase = Phase.AttackDeclaration; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var brash1 = Add(s, "training-brash-personality", 0, Zone.Home);
        var brash2 = Add(s, "training-brash-personality", 0, Zone.Home);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, Cmd(e, "declareAttack", player: 0));
        Accept(e, Cmd(e, "attack", player: 0, assignments: new() { [brash1.Id] = 0, [brash2.Id] = 1, [plain.Id] = 0 }));
        Equal(e.State.PendingChoice?.Type, "brashDraw");
        Equal(e.State.PendingBrashDraws, 2);
        Equal(e.State.PriorityPlayerId, 1);
        Accept(e, new GameCommand("brashDraw", 1, e.Version, Choice: "decline"));
        Equal(e.State.PendingBrashDraws, 1);
        Check(e.State.PendingChoice is not null, "Still one more offer");
        Accept(e, new GameCommand("brashDraw", 1, e.Version, Choice: "draw"));
        Equal(e.State.Players[1].Hand.Count, handBefore + 1);
        Check(e.State.PendingChoice is null);
        Equal(e.State.Phase, Phase.DefenseAssignment);
    }
    {
        // Togashi Noboru: redirects a lower-Chi enemy Personality's own Force to Noboru's own
        // controller's army for the duration of the battle -- only his own Force, never his unbowed
        // Follower's, which stays with its own controller.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var noboru = Add(s, "oracle-l5r-10058", 0, Zone.Battlefield, province: 0); // Chi 5, Force 4
        var lowChi = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3, Force 3
        Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: lowChi.Id); // Force 2, unbowed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: noboru.Id, TargetId: lowChi.Id, AbilityId: "noboru-redirect"));
        Check(e.State.ForceRedirects.Any(r => r.TargetId == lowChi.Id && r.ToPlayerId == 0));
        Resolve(e);
        var msg = e.GetView(0).Events.Last(ev => ev.Type == "battleResolved").Message;
        Check(msg.Contains("attacking Force 7, defending Force 2"), msg);
    }
    {
        // Live re-check: the beneficiary must still have a unit in their OWN current army at actual
        // resolution -- not just when the redirect was granted.
        var s = StrongholdBattle("oracle-l5r-1218");
        var lowChi = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        s.ForceRedirects = [new(lowChi.Id, 0)]; // granted to player 0, who has no unit here at all
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        var msg = e.GetView(0).Events.Last(ev => ev.Type == "battleResolved").Message;
        Check(msg.Contains("attacking Force 0, defending Force 3"), msg);
    }
    {
        // Togashi Hizumi: after ANY action's resolution bows a Personality at Hizumi's own
        // battlefield, his own controller may negate that Personality's own next straightening.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var hizumi = Add(s, "oracle-l5r-8521", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var straightener = Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        var straightener2 = Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: target.Id, AbilityId: "training-bow-at-battle"));
        Check(e.State.Cards[target.Id].Bowed);
        Equal(e.State.PendingChoice?.Type, "bowReaction");
        Equal(e.State.PendingChoice?.CardId, hizumi.Id);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: hizumi.Id, TargetId: target.Id, AbilityId: "hizumi-negate-straighten"));
        Equal(e.State.Cards[target.Id].StraightenBlockedTurn, e.State.TurnNumber);
        Check(e.State.PendingChoice is null, "nobody holds another reaction, so the window closes without a pass (issue #644)");
        // The window closed back to priority 1 (resuming exactly where the original bowing
        // action's own NextAction would have left it) -- an ordinary pass hands it back to 0.
        Accept(e, new GameCommand("pass", 1, e.Version));
        // Straighten() clears the block the moment it negates one attempt (issue #216 fix) --
        // "negate his NEXT straightening" is a one-shot, not "cannot straighten for the rest of the
        // turn"; a later attempt (a different source, its own dedupe scope) succeeds normally.
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: straightener.Id, TargetId: target.Id, AbilityId: "training-straighten-at-battle"));
        Check(e.State.Cards[target.Id].Bowed, "Still bowed -- the first attempt was negated");
        Equal(e.State.Cards[target.Id].StraightenBlockedTurn, -1, "Consumed, not blocked for the rest of the turn");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: straightener2.Id, TargetId: target.Id, AbilityId: "training-straighten-at-battle"));
        Check(!e.State.Cards[target.Id].Bowed, "A second attempt succeeds normally");
    }
    {
        // Tamori Ruya, at a battlefield: targets a DIFFERENT Personality at that same battlefield --
        // unrelated to which Personality the triggering action actually bowed.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var ruya = Add(s, "oracle-l5r-7789", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var bowedByAction = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var alsoAtBattlefield = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: bowedByAction.Id, AbilityId: "training-bow-at-battle"));
        Equal(e.State.PendingChoice?.Type, "bowReaction");
        Equal(e.State.PendingChoice?.CardId, ruya.Id);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ruya.Id, TargetId: alsoAtBattlefield.Id, AbilityId: "ruya-bow"));
        Check(e.State.Cards[alsoAtBattlefield.Id].Bowed);
        Check(e.State.PendingChoice is null, "nobody holds another reaction, so the window closes without a pass (issue #644)");
    }
    {
        // Tamori Ruya, at Home: "the same location" is Home WITH HER (same owner) -- the enemy's
        // own Home, even though both are "Zone.Home", is a different location entirely.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var ruya = Add(s, "oracle-l5r-7789", 0, Zone.Home);
        var bower = Add(s, "training-bow-enemy-source", 0, Zone.Home);
        var enemyBowed = Add(s, "training-lion-samurai", 1, Zone.Home);
        var otherAtRuyasHome = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bower.Id, TargetId: enemyBowed.Id, AbilityId: "training-bow-enemy"));
        Equal(e.State.PendingChoice?.Type, "bowReaction");
        Equal(e.State.PendingChoice?.CardId, ruya.Id);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ruya.Id, TargetId: enemyBowed.Id, AbilityId: "ruya-bow"));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ruya.Id, TargetId: otherAtRuyasHome.Id, AbilityId: "ruya-bow"));
        Check(e.State.Cards[otherAtRuyasHome.Id].Bowed);
        Check(e.State.PendingChoice is null, "nobody holds another reaction, so the window closes without a pass (issue #644)");
    }
    {
        // Tamori Yayu: "+2F and Cavalry while it is not your turn" -- a live conditional, not a
        // durable grant, re-evaluated as ActivePlayerId itself changes.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; // not Yayu's own controller's turn
        var yayu = Add(s, "oracle-l5r-7806", 0, Zone.Battlefield, province: 0); // printed Force 3
        var e = MatchEngine.FromTestState(s);
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == yayu.Id).Force, 5, "+2F while it is not his controller's turn");
        Check(e.State.Cards[yayu.Id].DefinitionId == "oracle-l5r-7806");
        var s2 = e.State; s2.ActivePlayerId = 0; // now his own controller's turn
        var e2 = MatchEngine.FromTestState(s2);
        Equal(e2.GetView(0).Players[0].Army.Single(c => c.Id == yayu.Id).Force, 3, "No bonus on his own controller's own turn");
    }
    {
        // Tamori Yayu: bowing one of his own Spells pays the cost for a Ranged 5 Attack.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var yayu = Add(s, "oracle-l5r-7806", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Attached, province: 0, attachedTo: yayu.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: yayu.Id, PerformerId: spell.Id, TargetId: enemy.Id, AbilityId: "yayu-bow-attack"));
        Check(e.State.Cards[spell.Id].Bowed, "Paid by bowing the Spell");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Ranged 5 hits Force 3");
    }
    {
        // The destroy-cost variant, and the shared UsageKey correctly deduping the pair once per turn.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var yayu = Add(s, "oracle-l5r-7806", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Attached, province: 0, attachedTo: yayu.Id);
        var spell2 = Add(s, "training-spell", 0, Zone.Attached, province: 0, attachedTo: yayu.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: yayu.Id, PerformerId: spell.Id, TargetId: enemy.Id, AbilityId: "yayu-destroy-attack"));
        Equal(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "Destroyed as the cost");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: yayu.Id, PerformerId: spell2.Id, TargetId: enemy.Id, AbilityId: "yayu-bow-attack"));
    }
    {
        // Restricted to Spells only -- an Item attached to Yayu is not a legal cost card.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var yayu = Add(s, "oracle-l5r-7806", 0, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 0, Zone.Attached, province: 0, attachedTo: yayu.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: yayu.Id, PerformerId: item.Id, TargetId: enemy.Id, AbilityId: "yayu-bow-attack"));
    }
    {
        // Mirumoto Katagi: a Non-Unique Weapon attaching to him from hand pays 4 less Gold -- here,
        // clamped all the way to 0 (the fixture's own printed cost is only 1).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var katagi = Add(s, "oracle-l5r-5112", 0, Zone.Home);
        var weapon = Add(s, "training-weapon", 0, Zone.Hand); // GoldCost 1, Weapon, non-Unique
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "equip", id: weapon.Id, target: katagi.Id));
        Equal(e.State.Cards[weapon.Id].Zone, Zone.Attached, "Attached for free -- the discount clamps to 0");
    }
    {
        // A Unique Weapon gets no discount -- "Non-Unique" is a real restriction, not decorative.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var katagi = Add(s, "oracle-l5r-5112", 0, Zone.Home);
        var weapon = Add(s, "training-unique-weapon", 0, Zone.Hand); // GoldCost 1, Weapon, Unique
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: weapon.Id, target: katagi.Id)); // no sources offered -- still costs 1
    }
    {
        // A non-Weapon attachment gets no discount either.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var katagi = Add(s, "oracle-l5r-5112", 0, Zone.Home);
        var spell = Add(s, "training-spell", 0, Zone.Hand); // GoldCost 1, not a Weapon
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: spell.Id, target: katagi.Id));
    }
    {
        // Tamori Shinji: enters play for 4 less Gold specifically via the Reserve-while-Defender
        // path (ReserveKeyword's own generic absent-battle-action shape) -- printed cost 8, less 4.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0;
        var shinjiId = s.Players[0].Provinces[0].CardId!;
        s.Cards[shinjiId].DefinitionId = "oracle-l5r-10846"; s.Cards[shinjiId].FaceUp = true;
        s.Players[0].Honor = 5; // meets the printed Honor Requirement outright
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        // Issue #443: the offered recruitReserve option itself must show the discounted price too,
        // not just what RecruitReserve actually charges below.
        Check(e.QueryActions(0).Any(a => a.Type == "recruitReserve" && a.CardId == shinjiId && a.Gold?.Cost == 4), "Offered recruitReserve quote reflects the Reserve-while-Defender discount (4, not 8)");
        var payment = e.PaymentOptions(0, 4).First();
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: shinjiId, SourceIds: payment.SourceIds));
        Equal(e.State.Cards[shinjiId].Zone, Zone.Battlefield);
    }
    {
        // Below the Honor Requirement, Clan Allegiance is still needed, and stacks with the
        // discount as usual: 8 - 4 (Reserve-while-Defender) + 2 (Allegiance) = 6.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0; s.Players[0].Clan = "Dragon";
        var shinjiId = s.Players[0].Provinces[0].CardId!;
        s.Cards[shinjiId].DefinitionId = "oracle-l5r-10846"; s.Cards[shinjiId].FaceUp = true;
        s.Players[0].Honor = 4;
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a => a.Type == "recruitReserve" && a.CardId == shinjiId && a.Command?.ClanAllegiance == true && a.Command?.ClanDiscount != true && a.Gold?.Cost == 6), "Offered recruitReserve quote (Allegiance mode) reflects the Reserve discount stacked with Allegiance (6, not 8)");
        Reject(e, new GameCommand("recruitReserve", 0, e.Version, CardId: shinjiId, SourceIds: e.PaymentOptions(0, 4).First().SourceIds, ClanAllegiance: false));
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: shinjiId, SourceIds: e.PaymentOptions(0, 6).First().SourceIds, ClanAllegiance: true));
        Equal(e.State.Cards[shinjiId].Zone, Zone.Battlefield);
    }
    {
        // An ordinary Dynasty-phase Purchase of the same card never qualifies for the discount.
        var e = MatchEngine.CreateEconomyScenario(); var s = e.State;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "oracle-l5r-10846";
        s.Players[0].Honor = 5;
        e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, cardId);
        Equal(quote.Cost, 8, "No Reserve-while-Defender discount on an ordinary Purchase");
    }
}
static void CultistInfiltratorPrimitive()
{
    // "You may attach this card to a Personality you do not control" -- an Effect-keyed exception on
    // Equip's own ordinary ownership Require.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var infiltrator = Add(s, "oracle-l5r-1595", 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "equip", id: infiltrator.Id, target: enemy.Id, sources: Sources(e, 0)));
        Equal(e.State.Cards[infiltrator.Id].Zone, Zone.Attached, "Attached to a Personality he does not control");
        Equal(e.State.Cards[infiltrator.Id].AttachedTo, enemy.Id);
        Equal(e.State.Cards[infiltrator.Id].OwnerId, 0, "OwnerId stays with whoever equipped it (never reassigned)");
    }
    // An ordinary Follower is not granted this exception -- still restricted to your own Personality.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var follower = Add(s, "training-follower", 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: follower.Id, target: enemy.Id, sources: Sources(e, 0)));
    }
    // "After each Straighten Phase: Give this Personality a -1C token" -- fires every Straighten
    // Phase regardless of whose turn it is or who controls the host, routed through the existing
    // shared CreateToken Chi<=0 death check.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-lion-samurai", 1, Zone.Home); // enemy of the infiltrator's own owner
        var infiltrator = Add(s, "oracle-l5r-1595", 0, Zone.Attached, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        EndTurnFully(e);
        Equal(e.State.Cards[host.Id].Tokens.Count(t => t.Source == infiltrator.Id && t.Stat == "Chi" && t.Amount == -1), 1, "One -1C token after one Straighten Phase");
        Check(e.State.Cards[host.Id].Zone != Zone.Dead, "Chi 3 - 1 is still alive");
    }
    // Chi<=0 death still fires immediately via the existing shared check, and -- attached to an
    // ENEMY Personality -- the Follower is returned to its own original owner's discard pile when
    // its host dies, not its host's owner's, since OwnerId is never reassigned.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-lion-samurai", 1, Zone.Home); // Chi 3
        host.Tokens = [new("pretest-token", "TestChi", "test-fixture", "Chi", -2)]; // down to Chi 1 before the tick
        var infiltrator = Add(s, "oracle-l5r-1595", 0, Zone.Attached, attachedTo: host.Id); // owned by player 0
        var e = MatchEngine.FromTestState(s);
        EndTurnFully(e);
        Equal(e.State.Cards[host.Id].Zone, Zone.Dead, "Chi 1 - 1 = 0: destroyed immediately");
        Equal(e.State.Cards[infiltrator.Id].Zone, Zone.FateDiscard, "Follower discarded along with its host");
        Equal(e.State.Cards[infiltrator.Id].OwnerId, 0, "Returned to its OWN original owner's discard pile, not the host's owner's (player 1)");
    }
    // "This card will not be transferred" -- excluded from Overlay's own attachment-transfer loop,
    // discarded instead of left dangling on the removed lower-level card.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var higherId = s.Players[0].Provinces[0].CardId!; var higher = s.Cards[higherId];
        higher.DefinitionId = "training-paneki-exp3"; higher.FaceUp = true;
        var lower = Add(s, "training-paneki-base", 0, Zone.Home);
        var infiltrator = Add(s, "oracle-l5r-1595", 1, Zone.Attached, attachedTo: lower.Id); // owned by the OTHER player
        var ordinaryFollower = Add(s, "training-follower", 0, Zone.Attached, attachedTo: lower.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("overlay", 0, e.Version, CardId: higher.Id, TargetId: lower.Id, SourceIds: []));
        Equal(e.State.Cards[infiltrator.Id].Zone, Zone.FateDiscard, "Not transferred -- discarded instead");
        Equal(e.State.Cards[infiltrator.Id].OwnerId, 1, "Still its own original owner");
        Check(e.State.Cards[infiltrator.Id].AttachedTo is null);
        Equal(e.State.Cards[ordinaryFollower.Id].AttachedTo, higher.Id, "An ordinary Follower still transfers normally");
    }
}
static void UnimpeachableNamePrimitive()
{
    // Reduces the reacting player's own Honor loss from another player's action by 3 (minimum 0) --
    // offered because the triggering ability is in the curated Honor-loss list.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var striker = Add(s, "training-honor-strike-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var unimpeachable = Add(s, "oracle-l5r-8989", 1, Zone.Hand);
        s.Players[1].Honor = 10;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: striker.Id, TargetId: target.Id, AbilityId: "training-honor-strike"));
        Equal(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "Offered before the action resolves");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: unimpeachable.Id, AbilityId: "unimpeachable-name-reaction"));
        Equal(e.State.Cards[unimpeachable.Id].Zone, Zone.FateDiscard, "Consumed");
        Accept(e, Cmd(e, "declineReaction", player: 0));
        Accept(e, Cmd(e, "declineReaction", player: 1));
        Check(e.State.PendingChoice is null);
        Equal(e.State.Players[1].Honor, 10, "2-Honor loss reduced by 3, floored at 0 -- fully negated");
    }
    // Offered purely because the reacting player controls a Dishonorable Personality, even though
    // this specific action causes no Honor loss at all -- and may rehonor instead.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var actor = Add(s, "training-harmless-battle-action", 0, Zone.Battlefield, province: 0);
        var actorTarget = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var dishonorable = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); dishonorable.Dishonorable = true;
        var unimpeachable = Add(s, "oracle-l5r-8989", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: actor.Id, TargetId: actorTarget.Id, AbilityId: "kouki-force"));
        Equal(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "Offered: reactor controls a Dishonorable Personality");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: unimpeachable.Id, TargetId: dishonorable.Id, AbilityId: "unimpeachable-name-reaction"));
        Check(!e.State.Cards[dishonorable.Id].Dishonorable, "Rehonored");
        Accept(e, Cmd(e, "declineReaction", player: 0));
        Accept(e, Cmd(e, "declineReaction", player: 1));
        Check(e.State.PendingChoice is null);
    }
    // NOT offered when neither condition holds: a harmless action, and no Dishonorable Personality.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var actor = Add(s, "training-harmless-battle-action", 0, Zone.Battlefield, province: 0);
        var actorTarget = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-8989", 1, Zone.Hand); // held, but never offered
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: actor.Id, TargetId: actorTarget.Id, AbilityId: "kouki-force"));
        Check(e.State.PendingChoice is null, "Not offered: kouki-force causes no Honor loss and player 1 controls no Dishonorable Personality");
    }
    // Her own separate Battle ability: bow a performing Shugenja and an enemy card, gain 2 Honor.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var un = Add(s, "oracle-l5r-8989", 0, Zone.Hand);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var enemyCard = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        s.Players[0].Honor = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: un.Id, TargetId: enemyCard.Id, AbilityId: "unimpeachable-name-bow", PerformerId: shugenja.Id));
        Check(e.State.Cards[enemyCard.Id].Bowed, "Bowed");
        Equal(e.State.Players[0].Honor, 2, "Gained 2 Honor");
    }
}
static void FieldsOfMercyPrimitive()
{
    // The counter starts at 0 only once Fields of Mercy actually enters play -- a Personality
    // destroyed BEFORE it entered is never counted retroactively.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var destroyer2 = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var destroyer3 = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var fomHand = Add(s, "oracle-l5r-2530", 0, Zone.Hand);
        var victim1 = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: victim1.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e.State.Cards[victim1.Id].Zone, Zone.Dead, "Destroyed normally -- Fields of Mercy is not even in play yet");
        Accept(e, Cmd(e, "pass", player: 1));
        Accept(e, Cmd(e, "playStrategy", player: 0, id: fomHand.Id));
        var state = e.State;
        Check(state.FieldsOfMercyWatches.Any(w => w.CardId == fomHand.Id && w.DestroyedCount[1] == 0), "Counter starts at 0 on entry -- not retroactive to victim1's earlier destruction");
        // The FIRST Personality destruction after entry still happens normally (and starts the count)...
        var victim2 = Add(state, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(state);
        Accept(e2, Cmd(e2, "pass", player: 1));
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: destroyer2.Id, TargetId: victim2.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e2.State.Cards[victim2.Id].Zone, Zone.Dead, "First destruction after entry: not negated");
        // ...but the SECOND is negated.
        var state2 = e2.State;
        var victim3 = Add(state2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(state2);
        Accept(e3, Cmd(e3, "pass", player: 1));
        Accept(e3, new GameCommand("useAbility", 0, e3.Version, CardId: destroyer3.Id, TargetId: victim3.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e3.State.Cards[victim3.Id].Zone, Zone.Battlefield, "Second destruction after entry: negated -- still alive");
    }
    // A destroy effect that would take out a whole unit still destroys every attached card even when
    // the Personality herself is spared -- proven via an outright battle-resolution loss (which
    // destroys the whole losing army in one DestroyUnits call, regardless of individual attachments).
    {
        var s = Battle(attackForce: 8, defenseForce: 0, provinceStrength: 1).State; // attacker wins outright
        var fom = Add(s, "oracle-l5r-2530", 1, Zone.Battlefield, province: 0);
        s.FieldsOfMercyWatches.Add(new() { CardId = fom.Id }); // already in play, count starts at [0,0]
        var firstDefender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && c.Id != fom.Id);
        var secondDefender = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var followerOnSecond = Add(s, "training-follower", 1, Zone.Attached, attachedTo: secondDefender.Id, province: 0);
        var e = MatchEngine.FromTestState(s);
        Resolve(e);
        Equal(e.State.Cards[firstDefender.Id].Zone, Zone.Dead, "First Personality processed: destroyed normally, starts the count");
        Check(e.State.Cards[secondDefender.Id].Zone != Zone.Dead, "Second Personality processed: negated -- destruction did not apply (battle resolution's own post-battle move-home still applies normally, same as any other surviving unit)");
        Equal(e.State.Cards[followerOnSecond.Id].Zone, Zone.FateDiscard, "Its attachment is still destroyed even though the Personality herself was spared");
        Check(e.State.Cards[secondDefender.Id].AttachedTo is null, "The now-negated Personality has no dangling attachment reference left behind");
    }
}
static void YasukiTsujikenPrimitive()
{
    // His own Reaction, exercised end to end via a real Purchase: marks a target Holding.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var provinceCardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[provinceCardId].DefinitionId = "oracle-l5r-9469"; s.Cards[provinceCardId].FaceUp = true;
        var holding = Add(s, "training-farm", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 10).First();
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: provinceCardId, SourceIds: payment.SourceIds));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: provinceCardId, TargetId: holding.Id, AbilityId: "tsujiken-watch-holding"));
        Check(e.State.TsujikenWatches.Any(w => w.TsujikenId == provinceCardId && w.HoldingId == holding.Id), "Marked the target Holding");
    }
    // Bowing a marked Holding (any cause -- here, Ashigaru Elite's own Farm-bow cost, the one
    // currently-real Holding-bowing path) costs its controller 1 Honor per active mark; two separate
    // marks (from two copies of Tsujiken, owned by either player) on the SAME Holding stack.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var booster = Add(s, "training-ashigaru-boost-source", 0, Zone.Battlefield, province: 0);
        var farm = Add(s, "training-farm", 0, Zone.Home);
        var tsujiken1 = Add(s, "oracle-l5r-9469", 1, Zone.Home);
        var tsujiken2 = Add(s, "oracle-l5r-9469", 1, Zone.Home);
        s.TsujikenWatches.Add(new(tsujiken1.Id, farm.Id));
        s.TsujikenWatches.Add(new(tsujiken2.Id, farm.Id));
        s.Players[0].Honor = 10;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: booster.Id, TargetId: farm.Id, AbilityId: "training-ashigaru-boost"));
        Check(e.State.Cards[farm.Id].Bowed, "Bowed as the ability's own cost");
        Equal(e.State.Players[0].Honor, 8, "Two active marks: 2 Honor lost on this one bow");
    }
    // The watch ends the moment Tsujiken himself leaves play -- destroy him, then bow the same
    // Holding again: no further Honor loss from his own mark.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var booster = Add(s, "training-ashigaru-boost-source", 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var farm = Add(s, "training-farm", 0, Zone.Home);
        var tsujiken = Add(s, "oracle-l5r-9469", 1, Zone.Battlefield, province: 0);
        s.TsujikenWatches.Add(new(tsujiken.Id, farm.Id));
        s.Players[0].Honor = 10;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: tsujiken.Id, AbilityId: "training-destroy-no-attachments"));
        Equal(e.State.Cards[tsujiken.Id].Zone, Zone.Dead, "Tsujiken destroyed");
        Check(!e.State.TsujikenWatches.Any(w => w.TsujikenId == tsujiken.Id), "His own watch was cleared when he left play");
        Accept(e, Cmd(e, "pass", player: 1));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: booster.Id, TargetId: farm.Id, AbilityId: "training-ashigaru-boost"));
        Equal(e.State.Players[0].Honor, 10, "No mark left active: no Honor lost bowing the same Holding again");
    }
    // Open half: bows Tsujiken (his own ability's usual cost) and straightens a target Holding.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var tsujiken = Add(s, "oracle-l5r-9469", 0, Zone.Home);
        var holding = Add(s, "training-expensive-holding", 0, Zone.Home, bowed: true); // base Gold Cost 6, above 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tsujiken.Id, TargetId: holding.Id, AbilityId: "tsujiken-straighten-holding"));
        Check(e.State.Cards[tsujiken.Id].Bowed, "Bowed as the mandatory cost -- the target's base Gold Cost (6) is above 4");
        Check(!e.State.Cards[holding.Id].Bowed, "Straightened");
    }
    // The bow-Tsujiken cost is waived for a cheap target (base Gold Cost 4 or lower).
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var tsujiken = Add(s, "oracle-l5r-9469", 0, Zone.Home);
        var holding = Add(s, "training-farm", 0, Zone.Home, bowed: true); // base Gold Cost 2, 4 or lower
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tsujiken.Id, TargetId: holding.Id, AbilityId: "tsujiken-straighten-holding"));
        Check(!e.State.Cards[tsujiken.Id].Bowed, "Cost waived -- the target's base Gold Cost (2) is 4 or lower");
        Check(!e.State.Cards[holding.Id].Bowed, "Still straightened");
    }
}
static void StormlessFuryBattleActionReaction()
{
    // Both halves share the general "after a Battle action resolves" primitive: the out-of-play
    // attach (no Additional Action granted), then the same card's own second Reaction excluded from
    // firing against the SAME action that just triggered its own attachment, but firing normally
    // against a later, different action.
    var s = StrongholdBattle("oracle-l5r-1218");
    var thunderShugenja = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
    var secondPerformer = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
    var enemy = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5, survives a Melee 2 either way
    var stormlessFury = Add(s, "oracle-l5r-7512", 0, Zone.Hand);
    var e = MatchEngine.FromTestState(s);
    var grantsBefore = e.State.AdditionalActions.Count;
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: thunderShugenja.Id, AbilityId: "training-weapon-attack")); // no target in sight; deferred Ranged/Melee targeting allows a null target
    Equal(e.State.PendingChoice?.Type, "battleActionReaction", "Stormless Fury in hand, its own controller's performing Thunder Shugenja just resolved a Battle action: the window opens");
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: stormlessFury.Id, AbilityId: "stormless-fury-attach"));
    Equal(e.State.Cards[stormlessFury.Id].Zone, Zone.Attached, "Equip (Limited)'s own attach mechanism fires as a Reaction");
    Equal(e.State.Cards[stormlessFury.Id].AttachedTo, thunderShugenja.Id);
    Equal(e.State.AdditionalActions.Count, grantsBefore, "Unlike Equip (Battle), attaching as this Reaction grants no Additional Action");
    // Issue #644: the card's own errata bars the second Reaction against the SAME action that just triggered the first, and the window is scoped to Stormless Fury's own controller,
    // so nobody holds a Reaction any more and nobody is asked to pass: the window has closed.
    Check(e.State.PendingChoice is null, "Window closed");
    Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: stormlessFury.Id, AbilityId: "stormless-fury-bow"));
    // A later, different action: the second Reaction fires normally, bowing whoever performed it.
    Accept(e, new GameCommand("pass", 1, e.Version)); // hand priority back to player 0 for the next action
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: secondPerformer.Id, AbilityId: "training-weapon-attack"));
    Equal(e.State.PendingChoice?.Type, "battleActionReaction", "The now-in-play Stormless Fury is offered again for a fresh action");
    Check(!e.State.Cards[secondPerformer.Id].Bowed, "Not yet bowed");
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: stormlessFury.Id, AbilityId: "stormless-fury-bow"));
    Check(e.State.Cards[secondPerformer.Id].Bowed, "Stormless Fury's second Reaction bows the just-resolved action's own performer, against a later action");
}
static void TsuruchiYakushoAnnounceReactionAndImmunity()
{
    // Offered only for a Ranged-Attack-producing ability (AttackKind), never Melee or a non-Attack
    // action; her own effect immunizes a Ranged Attack against every existing strength-reducing
    // Reaction for the remainder of THAT Attack's own resolution, never a later, separate one.
    var s = StrongholdBattle("oracle-l5r-1218");
    var yakusho = Add(s, "oracle-l5r-8895", 0, Zone.Battlefield, province: 0);
    var meleeAttacker = Add(s, "training-dual-attacker", 0, Zone.Battlefield, province: 0);
    var rangedAttacker1 = Add(s, "training-dual-attacker", 0, Zone.Battlefield, province: 0);
    var rangedAttacker2 = Add(s, "training-dual-attacker", 0, Zone.Battlefield, province: 0);
    var reactor = Add(s, "training-attack-reactor", 1, Zone.Home);
    var meleeTarget = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5, survives Melee 2 regardless
    var rangedTarget1 = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
    var rangedTarget2 = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
    var e = MatchEngine.FromTestState(s);

    // Melee: never offered, regardless of Yakusho's own presence.
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: meleeAttacker.Id, AbilityId: "training-weapon-attack"));
    Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "announceReaction", "A Melee action never offers Tsuruchi Yakusho's own announce Reaction");
    Accept(e, new GameCommand("pass", 1, e.Version)); // hand priority back to player 0 for the next action

    // Ranged, first Attack: Yakusho reacts to the announce, then her immunity blocks Training's own
    // negate Reaction from reducing THIS Attack's strength.
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: rangedAttacker1.Id, TargetId: rangedTarget1.Id, AbilityId: "training-honor-cost-attack"));
    Equal(e.State.PendingChoice?.Type, "announceReaction", "A Ranged-Attack-producing action offers Tsuruchi Yakusho's own announce Reaction");
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: yakusho.Id, AbilityId: "tsuruchi-yakusho-immunity"));
    Accept(e, new GameCommand("declineReaction", 1, e.Version));
    Accept(e, new GameCommand("declineReaction", 0, e.Version));
    Equal(e.State.PendingChoice?.Type, "attackReaction", "The originally-announced action now resolves, deferred by Training's own available reactor");
    // Immune to strength reduction for the remainder of this Attack's own resolution.
    Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: reactor.Id, AbilityId: "training-attack-negate"));
    Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds no reaction: this one pass closes the window (issue #644)
    Equal(e.State.Cards[rangedTarget1.Id].Zone, Zone.Dead, "Undiminished Ranged 3 destroys the Force-3 target: the negate Reaction never landed");

    // Ranged, second Attack: the immunity does not carry over -- Training's own negate now succeeds.
    Accept(e, new GameCommand("pass", 1, e.Version)); // hand priority back to player 0 for the next action
    Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: rangedAttacker2.Id, TargetId: rangedTarget2.Id, AbilityId: "training-honor-cost-attack"));
    if (e.State.PendingChoice?.Type == "announceReaction")
    {
        Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Accept(e, new GameCommand("declineReaction", 1, e.Version));
    }
    Equal(e.State.PendingChoice?.Type, "attackReaction");
    Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: reactor.Id, AbilityId: "training-attack-negate"));
    Equal(e.State.PendingAttack?.Strength, 0, "No lingering immunity from the earlier, separate Attack: the negate Reaction succeeds this time");
    Accept(e, new GameCommand("declineReaction", 1, e.Version)); // the attacker holds no reaction, so the reactor's owner alone passes (issue #644)
    Equal(e.State.Cards[rangedTarget2.Id].Zone, Zone.Battlefield, "Negated: the second target survives");
}
static void FollowerWave14Primitives()
{
    // Asako House Guard (oracle-l5r-543): "Will only attach to a Phoenix Clan Personality" plus a
    // dual-choice Battle/Open ability, "Set his Force equal to his Chi or Personal Honor. Gain 1
    // Honor" -- two catalog entries sharing one UsageKey.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var nonPhoenix = Add(s, "training-crane-samurai", 0, Zone.Home);
        var badGuard = Add(s, "oracle-l5r-543", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: badGuard.Id, target: nonPhoenix.Id, sources: Sources(e, 0))); // Will only attach to a Phoenix Clan Personality
    }
    {
        // Set Force equal to Chi: training-lion-samurai's own Force (4), Chi (3) and Personal Honor
        // (2) are all distinct, so each choice below is an observable, DIFFERENT change.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-phoenix-clan-samurai", 0, Zone.Home);
        var guard = Add(s, "oracle-l5r-543", 0, Zone.Attached, attachedTo: host.Id);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home); // Force 4, Chi 3, Personal Honor 2
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: guard.Id, TargetId: target.Id, AbilityId: "asako-house-guard-force-chi"));
        Equal(e.GetView(0).Players[1].Home.Single(c => c.Id == target.Id).Force, 3, "Force set to the target's own Chi (3), not its Personal Honor (2)");
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Gains 1 Honor");
        // Shared UsageKey: the OTHER half of the same printed opportunity is now exhausted too.
        var target2 = Add(e.State, "training-lion-samurai", 1, Zone.Home);
        // Already used this turn's Battle/Open opportunity, whichever half was used first.
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: guard.Id, TargetId: target2.Id, AbilityId: "asako-house-guard-force-honor"));
    }
    {
        // Set Force equal to Personal Honor: the independent half of the same dual choice.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-phoenix-clan-samurai", 0, Zone.Home);
        var guard = Add(s, "oracle-l5r-543", 0, Zone.Attached, attachedTo: host.Id);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home); // Force 4, Chi 3, Personal Honor 2
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: guard.Id, TargetId: target.Id, AbilityId: "asako-house-guard-force-honor"));
        Equal(e.GetView(0).Players[1].Home.Single(c => c.Id == target.Id).Force, 2, "Force set to the target's own Personal Honor (2), not its Chi (3)");
        Equal(e.State.Players[0].Honor, honorBefore + 1, "Gains 1 Honor");
    }
    // Legulus's Legion (oracle-l5r-10899): entry Honor loss (-3), and "Battle, bow: Melee Attack with
    // strength equal to this Follower's Force" -- reading its own LIVE Force (including a modifier
    // applied after it entered play), not just its printed Force, resolving the BLOCKERS note's own
    // open verification question.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var legion = Add(s, "oracle-l5r-10899", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Cmd(e, "equip", id: legion.Id, target: host.Id, sources: Sources(e, 0)));
        Equal(e.State.Players[0].Honor, honorBefore - 3, "Entry Honor loss: -3");
    }
    {
        // A Force-4 target survives printed Force 3 alone but dies to live Force 5 (3 printed + 2
        // from a token applied after Legulus's Legion entered play) -- a behavioral proof that
        // AttackStrength reads its own source's LIVE Force via sourceId, not a cached printed value.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var legion = Add(s, "oracle-l5r-10899", 0, Zone.Attached, province: 0, attachedTo: host.Id); // printed Force 3
        legion.Tokens = [new("pretest-token", "TestForce", "test-fixture", "Force", 2)]; // live Force 5, not printed 3
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: legion.Id, TargetId: target.Id, AbilityId: "legulus-legion-attack"));
        Check(e.State.Cards[legion.Id].Bowed, "Bow is a printed cost, paid immediately");
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Live Force 5 destroys a Force-4 target that printed Force 3 alone could not");
        // Already bowed by its own cost -- cannot act again.
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: legion.Id, AbilityId: "legulus-legion-attack"));
    }
    // Village Guardian (oracle-l5r-9162): "Reaction: After this card enters play from your hand:
    // Draw one card for each player who has had a province destroyed this game."
    {
        // The choke point itself: DestroyProvince sets MatchState.ProvinceEverDestroyed for the
        // PROVINCE OWNER only, never the attacker.
        var s = StrongholdBattle("oracle-l5r-1218");
        s.Players[1].Provinces[0].Strength = 1; s.Players[1].Provinces[0].PrintedStrength = 1;
        Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); // Force 5, unopposed
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.ProvinceEverDestroyed[0] && !e.State.ProvinceEverDestroyed[1], "Neither player has had a province destroyed yet");
        Resolve(e);
        Check(e.State.Players[1].Provinces[0].Destroyed, "Test setup: the province was actually destroyed");
        Check(e.State.ProvinceEverDestroyed[1], "Province owner (player 1) is now flagged");
        Check(!e.State.ProvinceEverDestroyed[0], "The attacker (player 0) is not -- nobody destroyed THEIR province");
    }
    {
        // Draws once per player already flagged -- zero, one, or two cards.
        foreach (var (flags, expectedDraws) in new[] { ((false, false), 0), ((true, false), 1), ((true, true), 2) })
        {
            var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            s.ProvinceEverDestroyed = [flags.Item1, flags.Item2];
            var host = Add(s, "training-crane-samurai", 0, Zone.Home);
            var guardian = Add(s, "oracle-l5r-9162", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Cmd(e, "equip", id: guardian.Id, target: host.Id, sources: Sources(e, 0)));
            Equal(e.State.PendingChoice?.Type, "entryReaction", "A genuine printed Reaction -- offered, not automatic");
            var handBefore = e.State.Players[0].Hand.Count;
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: guardian.Id, AbilityId: "village-guardian-draw"));
            Equal(e.State.Players[0].Hand.Count, handBefore + expectedDraws, $"ProvinceEverDestroyed={flags}: draws {expectedDraws}");
        }
    }
    // Village Raiders (oracle-l5r-9163): entry Honor loss (-1), AttachGrantsKeyword "Conqueror" (its
    // own scan literally reads "This Personality has Conqueror" despite being a Follower), and a
    // plain "Battle: Melee 4 Attack" with no bow cost of its own.
    {
        // Entry Honor loss AND AttachGrantsKeyword "Conqueror" both fire off the same real Equip --
        // GrantKeyword only ever runs from an actual attach command, never from a test fixture that
        // places a card directly into Zone.Attached (Commander's Steed's own established pattern).
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var raiders = Add(s, "oracle-l5r-9163", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Cmd(e, "equip", id: raiders.Id, target: host.Id, sources: Sources(e, 0)));
        Equal(e.State.Players[0].Honor, honorBefore - 1, "Entry Honor loss: -1");
        Check(e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Conqueror"), "Attaching grants Conqueror to the wielder -- the same proof Commander's Steed's own established AttachGrantsKeyword test uses; the grant's own effect during battle resolution is already covered generically by ConquerorStraightensAfterVictory");
    }
    {
        // Flat Melee 4: destroys a Force-4 target.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var raiders = Add(s, "oracle-l5r-9163", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: raiders.Id, TargetId: weak.Id, AbilityId: "village-raiders-attack"));
        Equal(e.State.Cards[weak.Id].Zone, Zone.Dead, "Melee 4 destroys a Force-4 target");
        Check(!e.State.Cards[raiders.Id].Bowed, "No printed bow cost, unlike Legulus's Legion above");
    }
    {
        // Flat Melee 4 may target a Force-5 card (core rule: legal, targeting is not gated by Force) but does not destroy it.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var raiders = Add(s, "oracle-l5r-9163", 0, Zone.Attached, province: 0, attachedTo: host.Id);
        var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: raiders.Id, TargetId: strong.Id, AbilityId: "village-raiders-attack"), strong.Id);
    }
    // Water Spider Adept (oracle-l5r-10109): a pure attach restriction with no ability text at all.
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var nonNinja = Add(s, "training-crane-samurai", 0, Zone.Home);
        var ninja = Add(s, "training-ninja-source", 0, Zone.Home);
        var badAdept = Add(s, "oracle-l5r-10109", 0, Zone.Hand);
        var goodAdept = Add(s, "oracle-l5r-10109", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Cmd(e, "equip", id: badAdept.Id, target: nonNinja.Id, sources: Sources(e, 0))); // Will only attach to a Ninja
        Accept(e, Cmd(e, "equip", id: goodAdept.Id, target: ninja.Id, sources: Sources(e, 0)));
        Equal(e.State.Cards[goodAdept.Id].Zone, Zone.Attached);
        Check(CardCatalog.Get(goodAdept.DefinitionId).Abilities?.Length is 0 or null, "No ability text at all -- a pure restriction");
    }
    // Yodotai Legionnaire (oracle-l5r-10688): entry Honor loss (-2), and the real Legionnaire
    // keyword Aulus's own boost condition already reads (MatchEngine's boost-legionnaires-in-unit),
    // previously exercised only by a synthetic fixture (training-legionnaire-follower).
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var aulus = Add(s, "oracle-l5r-10687", 0, Zone.Attached, attachedTo: host.Id);
        var legionnaire = Add(s, "oracle-l5r-10688", 0, Zone.Hand); // printed Force 1
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Cmd(e, "equip", id: legionnaire.Id, target: host.Id, sources: [])); // Cost 0: no Gold sources needed
        Equal(e.State.Players[0].Honor, honorBefore - 2, "Entry Honor loss: -2");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == legionnaire.Id).Force, 2, "Base 1 + 1 from Aulus's own unit-scoped Legionnaire boost -- the real card, not just the synthetic fixture");
        Equal(e.GetView(0).Players[0].Home.Single(c => c.Id == aulus.Id).Force, 3, "Aulus itself is not a Legionnaire -- unaffected");
    }
}
static void StrategyWave13Primitives()
{
    // Scorn the Weak (oracle-l5r-9925): "Target an enemy Personality: Give him -5F. If his Force is
    // now 0, his controller loses 1 Honor and you may target and dishonor a Personality."
    {
        // Force 6 - 5 = 1: does NOT reach 0. No Honor loss, and the optional second target is
        // rejected outright (nothing to gate it on).
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-9925", 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // blanket army-presence requirement -- no Performer of its own
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var third = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[1].Honor;
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "scorn-the-weak", CardIds: [third.Id]));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "scorn-the-weak"));
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 1, "6 - 5 = 1");
        Equal(e.State.Players[1].Honor, honorBefore, "Force did not reach 0: no Honor loss");
        Check(!e.State.Cards[third.Id].Dishonorable, "No dishonor offered when Force never reached 0");
    }
    {
        // Force 4 - 5 floors to 0: the controller loses 1 Honor, and the optional second target
        // (any Personality, either side, no restriction printed) may be dishonored.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-9925", 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // blanket army-presence requirement -- no Performer of its own
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var third = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "scorn-the-weak", CardIds: [third.Id]));
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 0, "CardForce floors at 0, never negative");
        Equal(e.State.Players[1].Honor, honorBefore - 1, "Force reached 0: controller loses 1 Honor");
        Check(e.State.Cards[third.Id].Dishonorable, "The optional second target was dishonored");
    }
    // Strength in Terror (oracle-l5r-7538, Fear): "Choose your performing Paragon and target an
    // enemy Personality: Reduce his Force by an amount equal to your Paragon's Force. You may
    // target and destroy one of the Personality's attachments. Lose 3 Honor."
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-7538", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var attachment = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var filler = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // an unrelated card -- not the primary target
        var offTarget = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: filler.Id);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        e.State.Cards[performer.Id].Bowed = true; // no "unbowed" printed: a bowed Paragon may perform it
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strength-in-terror", PerformerId: performer.Id, CardIds: [offTarget.Id])); // not one of the TARGET's own attachments
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strength-in-terror", PerformerId: performer.Id, CardIds: [attachment.Id]));
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 3, "6 - 3 (the Paragon's own live Force) = 3");
        Equal(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard, "The optional attachment was destroyed");
        Equal(e.State.Players[0].Honor, honorBefore - 3, "Loses 3 Honor");
    }
    // Strength in Terror: the "may destroy an attachment" is a real choice the acting player is offered (and may decline); a target with no attachments is asked nothing.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-7538", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var attachment = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strength-in-terror", PerformerId: performer.Id));
        Equal(e.GetView(0).PendingChoice?.Type, "effectChoice", "The optional attachment destroy is offered");
        var offered = e.QueryActions(0).Where(a => a.Type == "chooseEffect").ToArray();
        Equal(offered.Length, 2, "One option per attachment plus decline");
        Accept(e, offered.First(a => a.Command!.Choice!.StartsWith("sit-destroy:", StringComparison.Ordinal)).Command!);
        Equal(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard, "The chosen attachment was destroyed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-7538", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strength-in-terror", PerformerId: performer.Id));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "sit-decline").Command!);
        Check(e.State.Cards.Values.Any(c => c.Zone == Zone.Attached && c.AttachedTo == target.Id), "Declining keeps the attachment");
    }
    // Strength of Purity (oracle-l5r-7546): "Choose your performing Personality with 3 or higher
    // Personal Honor: Give him +3F."
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-7546", 0, Zone.Hand);
        var tooLow = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Personal Honor 2
        var qualifies = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Personal Honor 3
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "strength-of-purity", PerformerId: tooLow.Id)); // Personal Honor 2 < 3
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "strength-of-purity", PerformerId: qualifies.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == qualifies.Id).Force, 5, "Printed Force 2 + 3");
    }
    // Strength of the Bear (oracle-l5r-10748, no printed Cost): "Bow your target unbowed
    // Personality. Bow a target enemy card with lower Force than his. If your Personality has 5 or
    // more printed Force, straighten him."
    {
        // Printed Force 4 (below 5): stays bowed.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-10748", 0, Zone.Hand);
        var performer = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // printed Force 4
        var weaker = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: weaker.Id, AbilityId: "strength-of-the-bear", PerformerId: performer.Id));
        Check(e.State.Cards[performer.Id].Bowed, "Printed Force 4 < 5: stays bowed");
        Check(e.State.Cards[weaker.Id].Bowed, "Enemy target with lower Force is bowed too");
    }
    {
        // Printed Force 6 (5 or more): straightened back immediately.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-10748", 0, Zone.Hand);
        var performer = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // printed Force 6
        var weaker = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: weaker.Id, AbilityId: "strength-of-the-bear", PerformerId: performer.Id));
        Check(!e.State.Cards[performer.Id].Bowed, "Printed Force 6 >= 5: straightened back");
        Check(e.State.Cards[weaker.Id].Bowed, "Enemy target with lower Force is still bowed");
    }
    // Strength of the Divine (oracle-l5r-10555, no printed Cost): "Target your opposed unbowed
    // Paragon. Give him +2F or +2PH. Melee Attack with strength equal to his Force or Personal
    // Honor." A dual choice sharing one UsageKey; each branch buffs the Performer then attacks with
    // strength read live off that same, now-buffed Performer.
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-10555", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, PersonalHonor 2
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var enemy = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2, opposes the performer
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, PerformerId: performer.Id, TargetId: enemy.Id, AbilityId: "strength-of-the-divine-force"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 5, "3 + 2");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Attack strength 5 (the buffed Force) destroys a Force-2 target");
        // Shared UsageKey: the OTHER half of this same printed opportunity is now exhausted too.
        var enemy2 = Add(e.State, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, PerformerId: performer.Id, TargetId: enemy2.Id, AbilityId: "strength-of-the-divine-honor"));
    }
    {
        // The Personal Honor branch, independent of the Force branch above.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-10555", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, PersonalHonor 2
        performer.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var enemy = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, PerformerId: performer.Id, TargetId: enemy.Id, AbilityId: "strength-of-the-divine-honor"));
        Equal(e.State.Cards[performer.Id].Modifiers.Where(m => m.Stat == "PersonalHonor").Sum(m => m.Amount), 2, "Personal Honor buffed by +2");
        Equal(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Attack strength 4 (the buffed Personal Honor: printed 2 + 2) destroys a Force-2 target");
    }
    // The Strength of the Spider (oracle-l5r-8362): "Choose your performing unbowed Spider Clan
    // Personality and target an enemy Personality or Follower: Destroy it if it is bowed. Reduce
    // its Force by an amount equal to your Personality's Force. Bow it if its Force is now 0."
    {
        // Already-bowed branch: destroyed outright, no Force reduction needed.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8362", 0, Zone.Hand);
        var performer = Add(s, "training-spider-honor-loss", 0, Zone.Battlefield, province: 0); // Spider Clan, Force 2
        var bowedTarget = Add(s, "training-lion-veteran", 1, Zone.Battlefield, true, province: 0); // Force 6, already bowed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: bowedTarget.Id, AbilityId: "strength-of-the-spider", PerformerId: performer.Id));
        Equal(e.State.Cards[bowedTarget.Id].Zone, Zone.Dead, "Already bowed: destroyed outright, even at Force 6");
    }
    {
        // Unbowed branch, reduced to exactly 0: cripple, then bow (still alive).
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8362", 0, Zone.Hand);
        var performer = Add(s, "training-spider-honor-loss", 0, Zone.Battlefield, province: 0); // Spider Clan, Force 2
        var target = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2, unbowed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strength-of-the-spider", PerformerId: performer.Id));
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Still alive -- the unbowed branch never destroys");
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 0, "2 - 2 = 0");
        Check(e.State.Cards[target.Id].Bowed, "Force reached 0: bowed");
    }
    {
        // Unbowed branch, NOT reduced to 0: crippled but not bowed.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-8362", 0, Zone.Hand);
        var performer = Add(s, "training-spider-honor-loss", 0, Zone.Battlefield, province: 0); // Spider Clan, Force 2
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, unbowed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "strength-of-the-spider", PerformerId: performer.Id));
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 4, "6 - 2 = 4");
        Check(!e.State.Cards[target.Id].Bowed, "Force did not reach 0: not bowed");
    }
    // Unexpected (oracle-l5r-9940): "Choose your performing unbowed Personality and target an enemy
    // card without attachments: Bow it. You may bow your Personality; if you did, draw a card."
    {
        // Declines the optional self-bow: the target is still bowed, but no draw and the performer
        // stays unbowed.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-9940", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "unexpected", PerformerId: performer.Id, CardIds: [target.Id])); // must be the PERFORMER's own id, not the target's
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "unexpected", PerformerId: performer.Id));
        Check(e.State.Cards[target.Id].Bowed, "Target bowed");
        Check(!e.State.Cards[performer.Id].Bowed, "Declined the optional self-bow: performer stays unbowed");
        Equal(e.State.Players[0].Hand.Count, handBefore - 1, "No draw when the optional self-bow was declined -- only the Strategy card itself leaves hand, played from it");
    }
    {
        // Takes the optional self-bow: draws a card.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-9940", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "unexpected", PerformerId: performer.Id, CardIds: [performer.Id]));
        Check(e.State.Cards[target.Id].Bowed, "Target bowed");
        Check(e.State.Cards[performer.Id].Bowed, "Took the optional self-bow");
        Equal(e.State.Players[0].Hand.Count, handBefore, "The Strategy card itself leaves hand (-1) but the draw (+1) nets back to even");
    }
}

static void GrantedEffectPresentationView()
{
    var state = MatchEngine.CreateEconomyScenario().State;
    var card = Add(state, "training-crane-samurai", 0, Zone.Home);
    card.GrantedKeywords = [new("Berserker", "source", "Permanent")];
    card.Protections = [new("source", "Permanent"), new("temporary-source", "Turn")];
    var hidden = state.Cards.Values.First(c => c.Zone == Zone.Province && !c.FaceUp);
    hidden.Protections = [new("hidden-source", "Permanent")];
    var engine = MatchEngine.FromTestState(state);
    foreach (var viewer in new[] { 0, 1 })
    {
        var view = engine.GetView(viewer);
        var visible = view.Players[0].Home.Single(c => c.Id == card.Id);
        Equal(visible.GrantedEffects!.Length, 2);
        Check(visible.GrantedEffects.Any(e => e.Duration == "Permanent" && e.Source == "source"));
        Check(visible.GrantedEffects.All(e => e.Text.Contains("will not be negated")));
        Check(view.Players.SelectMany(p => p.Provinces).Where(p => p.Card?.Definition is null).All(p => p.Card?.GrantedEffects is null), "Hidden provinces never carry metadata");
        visible.GrantedEffects[0] = new("Changed", "Not engine text", "x", "Turn");
        Check(engine.GetView(viewer).Players[0].Home.Single(c => c.Id == card.Id).GrantedEffects![0].Name == "Action protection", "Detached view does not mutate the engine");
    }
    var snapshot = engine.GetView(0).Players[0].Home.Single(c => c.Id == card.Id);
    var encoded = JsonSerializer.Serialize(snapshot);
    var decoded = JsonSerializer.Deserialize<CardView>(encoded)!;
    Equal(decoded.GrantedEffects!.Length, 2);
    var old = System.Text.Json.Nodes.JsonNode.Parse(encoded)!.AsObject(); old.Remove("GrantedEffects");
    Check(JsonSerializer.Deserialize<CardView>(old.ToJsonString())!.GrantedEffects is null, "Older snapshots remain readable");
    SkipToDynasty(engine); Accept(engine, Cmd(engine, "endDynasty"));
    var after = engine.GetView(0).Players[0].Home.Single(c => c.Id == card.Id);
    Equal(after.GrantedEffects!.Length, 1); Equal(after.GrantedEffects[0].Duration, "Permanent");
    Equal(after.GrantedKeywords!.Single().Duration, "Permanent");
}

static void StrategyWave14Primitives()
{
    // Banzai! (oracle-l5r-10929): "Discipline (2). Battle: Give your target opposed Personality +2F."
    {
        // own-opposed's own shape (Crushing Force's own precedent): the TARGET is your own opposed
        // Personality, not the enemy's -- opposition comes from the enemy army merely being present.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10929", 0, Zone.FateDiscard);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, own, opposed
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // enemy presence -> opposed
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 2).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "banzai", SourceIds: payment.SourceIds));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == target.Id).Force, 5, "3 + 2");
        Equal(e.State.Cards[strat.Id].Zone, Zone.RemovedFromGame, "Discipline removes itself from the game after resolving");
    }
    {
        // Rejected during the Action phase: Banzai!'s printed "Battle:" verb (its ability's Timing, since
        // issue #331 made Discipline a flag rather than a timing) is Battle-phase only.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Action;
        var strat = Add(s, "oracle-l5r-10929", 0, Zone.FateDiscard);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "banzai", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
    }
    // Pure Intent (oracle-l5r-9922): "Battle: Choose your performing opposed Personality: Give him a
    // Force bonus equal to his Personal Honor. Gain 2 Honor." Its own scan reads this Performer shape
    // (not deck-pool.json's own misleading plain-Target paraphrase) -- a new own-personality-opposed
    // Performer category.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9922", 0, Zone.Hand);
        var performer = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Force 2, Personal Honor 3
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // opposing presence
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "pure-intent", PerformerId: performer.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 5, "2 + 3 (Personal Honor)");
        Equal(e.State.Players[0].Honor, honorBefore + 2, "Gain 2 Honor");
    }
    {
        // Rejected when not opposed -- own-personality-opposed's own gate, no "unbowed" restriction
        // printed either (unlike own-unbowed-personality-with-keyword-opposed).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9922", 0, Zone.Hand);
        var performer = Add(s, "training-crane-courtier", 0, Zone.Battlefield, true, province: 0); // bowed: still legal, no unbowed gate
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "pure-intent", PerformerId: performer.Id));
    }
    // Marshal Your Strength (oracle-l5r-10946): "Battle: Give your target Personality Force and Chi
    // bonuses equal to his Personal Honor, minus 1 if he is unopposed." own-personality Target (not
    // opposed-gated -- works unopposed too, just for less) plus ApplyModifier's own stat-agnostic Chi
    // branch, exercised by a real card for the first time.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10946", 0, Zone.Hand);
        var target = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Force 2, Chi 3, Personal Honor 3
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // opposed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "marshal-your-strength"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == target.Id).Force, 5, "2 + 3 (opposed: the full Personal Honor bonus)");
        Equal(e.State.Cards[target.Id].Modifiers.Where(m => m.Stat == "Chi").Sum(m => m.Amount), 3, "Chi bonus too, opposed: the full Personal Honor");
    }
    {
        // Unopposed: minus 1 on both stats.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10946", 0, Zone.Hand);
        var target = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "marshal-your-strength"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == target.Id).Force, 4, "2 + (3 - 1) unopposed");
        Equal(e.State.Cards[target.Id].Modifiers.Where(m => m.Stat == "Chi").Sum(m => m.Amount), 2, "Chi bonus too: 3 - 1 unopposed");
    }
    // Lost in Transit (oracle-l5r-10945): "Discipline (1). Battle/Open: Bow a target attachment." The
    // standalone "attachment" Target category already existed (nubane-bow's own shape) -- no new
    // primitive, just wiring. Issue #331 (Discipline is a flag, not a timing): its timing is its printed
    // Battle/Open verb, so used DURING a battle it is a real Battle action and needs the acting player's
    // presence at the battlefield like every Battle/Open card (the old Discipline timing skipped that
    // check); this fixture therefore gives player 0 a unit there (it had none before).
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10945", 0, Zone.FateDiscard);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attachment = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: attachment.Id, AbilityId: "lost-in-transit-bow", SourceIds: payment.SourceIds));
        Check(e.State.Cards[attachment.Id].Bowed, "Target attachment bowed");
        Equal(e.State.Cards[strat.Id].Zone, Zone.RemovedFromGame, "Discipline removes itself from the game after resolving");
    }
    {
        // Usable during the Action phase too -- Battle/Open, unlike Banzai!'s own Battle-only shape.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10945", 0, Zone.FateDiscard);
        var host = Add(s, "training-lion-samurai", 1, Zone.Home);
        var attachment = Add(s, "training-item", 1, Zone.Attached, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        var payment = e.PaymentOptions(0, 1).First();
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: attachment.Id, AbilityId: "lost-in-transit-bow", SourceIds: payment.SourceIds));
        Check(e.State.Cards[attachment.Id].Bowed, "Usable in the Action phase too (Battle/Open)");
    }
    // Strong Defenses (oracle-l5r-9931, Siege): "Reaction: After a Ranged or Melee Attack is
    // targeted: Give it -3 strength.\nBattle: Choose your performing defending Personality: Give him
    // +5F. Straighten him before this battle resolves."
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var wielderHost = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: wielderHost.Id);
        var defenses = Add(s, "oracle-l5r-9931", 1, Zone.Hand);
        Add(s, "training-attack-reactor", 1, Zone.Home); // the defender still holds a Reaction, so the window stays open to read the strength (issue #644)
        var target = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2, within the Melee 2 Attack's strength
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon.Id, TargetId: target.Id, AbilityId: "training-weapon-attack"));
        Equal(e.State.PendingChoice?.Type, "attackReaction");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: defenses.Id, AbilityId: "strong-defenses-reaction"));
        Equal(e.State.PendingAttack?.Strength, -1, "training-weapon-attack's own Melee 2 - 3 = -1");
    }
    {
        // Battle half: no "unbowed" qualifier printed, so a bowed defender may be chosen -- straighten
        // is the whole point.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0; // player 0 is the Defender
        var strat = Add(s, "oracle-l5r-9931", 0, Zone.Hand);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, true, province: 0); // Force 3, bowed
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "strong-defenses-boost", PerformerId: defender.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == defender.Id).Force, 8, "3 + 5");
        Check(!e.State.Cards[defender.Id].Bowed, "Straightened before this battle resolves");
    }
    {
        // Rejected against an attacking Personality -- must be defending.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0; // player 0 is the Attacker
        var strat = Add(s, "oracle-l5r-9931", 0, Zone.Hand);
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "strong-defenses-boost", PerformerId: attacker.Id));
    }
    // Trusting Instinct (oracle-l5r-10402): "Reaction: After a Ranged or Melee attack is targeted:
    // Give it -2 strength.\nBattle: Choose your performing Personality: Move him to a battlefield
    // where he would be opposed."
    {
        var s = StrongholdBattle("oracle-l5r-1218");
        var wielderHost = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, attachedTo: wielderHost.Id);
        var instinct = Add(s, "oracle-l5r-10402", 1, Zone.Hand);
        Add(s, "training-attack-reactor", 1, Zone.Home); // the defender still holds a Reaction, so the window stays open to read the strength (issue #644)
        var target = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2, within the Melee 2 Attack's strength
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: weapon.Id, TargetId: target.Id, AbilityId: "training-weapon-attack"));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: instinct.Id, AbilityId: "trusting-instinct-reaction"));
        Equal(e.State.PendingAttack?.Strength, 0, "training-weapon-attack's own Melee 2 - 2 = 0");
    }
    {
        // Battle half: moved to the current battlefield (this engine resolves one battle at a time),
        // gated on the destination actually having enemy presence.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10402", 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // blanket army-presence requirement
        var mover = Add(s, "training-lion-samurai", 0, Zone.Home);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // opposing presence at the destination
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "trusting-instinct-move", PerformerId: mover.Id));
        Equal(e.State.Cards[mover.Id].Zone, Zone.Battlefield, "Moved to the current battlefield");
        Equal(e.State.Cards[mover.Id].ProvinceIndex, 0);
    }
    {
        // Rejected when the destination would NOT be opposed.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10402", 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var mover = Add(s, "training-lion-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "trusting-instinct-move", PerformerId: mover.Id));
    }
    // Visage of the Orochi (oracle-l5r-9168, Thunder/Fear): "Battle: Choose your performing opposed
    // unbowed Thunder Personality: Raise his Force to 6. You may target and bow an enemy card with
    // lower Force and no attachments."
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9168", 0, Zone.Hand);
        var performer = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0); // Force 2, raised to 6
        var weak = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2, no attachments
        var e = MatchEngine.FromTestState(s);
        // A target bar (issue #420) covers this channel too: the bowed card is named on CardIds, not TargetId.
        var barredState = e.State;
        barredState.Cards[weak.Id].TargetBars.Add(new TargetBar(performer.Id, barredState.TurnNumber));
        var barred = MatchEngine.FromTestState(barredState);
        var barRefusal = barred.Execute(new GameCommand("useAbility", 0, barred.Version, CardId: strat.Id, AbilityId: "visage-of-the-orochi", PerformerId: performer.Id, CardIds: [weak.Id]));
        Check(!barRefusal.Accepted && (barRefusal.Error ?? "").Contains("may not be targeted"), "A barred card named on CardIds is refused: " + barRefusal.Error);
        Check(!barred.State.Cards[weak.Id].Bowed, "and it was not bowed");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "visage-of-the-orochi", PerformerId: performer.Id, CardIds: [weak.Id]));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 6, "Raised to 6");
        Check(e.State.Cards[weak.Id].Bowed, "Optional bow target bowed");
    }
    {
        // Already at or above 6: never reduced -- "Raise," not "Set."
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9168", 0, Zone.Hand);
        var performer = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // Force 6
        performer.GrantedKeywords = [new("Thunder", "test-fixture", "Permanent")];
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // opposing presence
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "visage-of-the-orochi", PerformerId: performer.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 6, "Already 6 or higher: never reduced");
    }
    {
        // The optional bow's own "lower Force" comparison reads the Performer's POST-raise Force (6),
        // not his live pre-effect Force (2) -- an enemy at Force 3 (above 2, below 6) is only a legal
        // target under the raised comparison.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9168", 0, Zone.Hand);
        var performer = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0); // Force 2, raised to 6
        var midForce = Add(s, "training-crane-dignitary", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "visage-of-the-orochi", PerformerId: performer.Id, CardIds: [midForce.Id]));
        Check(e.State.Cards[midForce.Id].Bowed, "Legal against the Performer's post-raise Force (6), not his live pre-effect Force (2)");
    }
    {
        // Rejected: the enemy card has an attachment.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9168", 0, Zone.Hand);
        var performer = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        var withAttachment = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: withAttachment.Id);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "visage-of-the-orochi", PerformerId: performer.Id, CardIds: [withAttachment.Id]));
    }

}
static void BatchDPrimitives()
{
    // Charge Into Danger (oracle-l5r-10366): "Battle: Choose your performing Personality: If he
    // assigned to, or would be opposed at, the current battlefield, move him there. If his base
    // Force is 7 or higher, straighten him." Plain own-personality Performer -- no new category
    // needed. The move is conditional, not a hard legality gate.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10366", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home, true); // bowed, Force 3
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // enemy present -- would be opposed
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // satisfies the blanket presence Require
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "charge-into-danger-move", PerformerId: performer.Id));
        Equal(e.State.Cards[performer.Id].Zone, Zone.Battlefield, "Would be opposed there: moved in");
        Equal(e.State.Cards[performer.Id].ProvinceIndex, 0);
        Check(e.State.Cards[performer.Id].Bowed, "Base Force 3 is below 7: not straightened");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10366", 0, Zone.Hand);
        var performer = Add(s, "training-high-force-personality", 0, Zone.Home, true); // Force 7
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "charge-into-danger-move", PerformerId: performer.Id));
        Equal(e.State.Cards[performer.Id].Zone, Zone.Battlefield);
        Check(!e.State.Cards[performer.Id].Bowed, "Base Force 7 or higher: straightened");
    }
    {
        // Not assigned to the current battlefield, and no enemy presence there -- legal to submit
        // (own-personality carries no presence gate of its own), but nothing moves. The Force-7
        // straighten is still unconditional, independent of whether he moved.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10366", 0, Zone.Hand);
        var performer = Add(s, "training-high-force-personality", 0, Zone.Home, true); // Force 7
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "charge-into-danger-move", PerformerId: performer.Id));
        Equal(e.State.Cards[performer.Id].Zone, Zone.Home, "Neither assigned nor opposed: no move");
        Check(!e.State.Cards[performer.Id].Bowed, "Straightened regardless -- the Force check does not depend on moving");
    }
    // Consuming Weakness (oracle-l5r-1467, Fear): "Battle: Choose your performing Shadowlands
    // Personality and remove a target dead Personality in any discard pile from the game: Give your
    // Personality +3F, or +4F if he is Undead." New own-shadowlands-personality-at-battle Performer;
    // new dead-personality-any-discard Target (Zone.Dead, any owner -- contrary to its own BLOCKERS
    // note, no such Target category existed anywhere in the engine before this).
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-1467", 0, Zone.Hand);
        var performer = Add(s, "training-shadowlands-personality", 0, Zone.Battlefield, province: 0); // Force 2, not Undead
        var deadEnemy = Add(s, "training-lion-samurai", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: deadEnemy.Id, AbilityId: "consuming-weakness", PerformerId: performer.Id));
        Equal(e.State.Cards[deadEnemy.Id].Zone, Zone.RemovedFromGame, "Removed from the game");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 5, "2 + 3 (not Undead)");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-1467", 0, Zone.Hand);
        var performer = Add(s, "training-undead-personality", 0, Zone.Battlefield, province: 0); // Force 2, Undead
        var deadOwn = Add(s, "training-crane-samurai", 0, Zone.Dead); // "any discard pile" -- the performer's own works too
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: deadOwn.Id, AbilityId: "consuming-weakness", PerformerId: performer.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 6, "2 + 4 (Undead)");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-1467", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // not Shadowlands
        var deadEnemy = Add(s, "training-lion-samurai", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: deadEnemy.Id, AbilityId: "consuming-weakness", PerformerId: performer.Id));
    }
    // Fires of Turmoil (oracle-l5r-10552): "Battle: Discard the top card of a Fate deck. Target a
    // card without attachments. If the discarded card's Focus Value is odd, bow the target (0 is
    // even). Otherwise, straighten it. Move its unit home if the target has a Madness token." New
    // unattached-unit-card Target (no ownership restriction, unlike Ikoma Masumi's own); "a Fate
    // deck" resolves to the performer's own (DiscardTopOfFateDeck, Aberrations' own precedent).
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10552", 0, Zone.Hand);
        var oddFocusCard = Add(s, "oracle-l5r-1121", 0, Zone.FateDeck); // Bow Before My Will, Focus 1 (odd)
        s.Players[0].FateDeck.Insert(0, oddFocusCard.Id);
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // satisfies the blanket presence Require
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        target.Tokens.Add(new CardToken("test-madness", "Madness", "test"));
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "fires-of-turmoil"));
        Equal(e.State.Cards[oddFocusCard.Id].Zone, Zone.FateDiscard, "Discarded the top card of the performer's own Fate deck");
        Check(e.State.Cards[target.Id].Bowed, "Odd Focus Value: bow the target");
        Equal(e.State.Cards[target.Id].Zone, Zone.Home, "Has a Madness token: its unit moves home");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10552", 0, Zone.Hand);
        var evenFocusCard = Add(s, "training-item", 0, Zone.FateDeck); // Focus 0 (even)
        s.Players[0].FateDeck.Insert(0, evenFocusCard.Id);
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // satisfies the blanket presence Require
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, true, province: 0); // bowed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "fires-of-turmoil"));
        Check(!e.State.Cards[target.Id].Bowed, "Even Focus Value (0 counts as even): straighten instead");
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "No Madness token: stays put");
    }
    // Gripped by Terror (oracle-l5r-2908, Fear): "Battle: Choose your performing Personality and
    // target an enemy Personality or Follower: Give it -4F. If your Personality is Nonhuman, give
    // each of your Personalities at the current battlefield +1F." New own-personality-at-battle
    // Performer (no unbowed qualifier, no keyword gate). Applied once via a plain ApplyModifier loop
    // over Army() at resolution, not a live recomputed passive (Utaku Elite Guard's own shape is
    // mirrored only conceptually here, since this is a one-shot Strategy effect).
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-2908", 0, Zone.Hand);
        var performer = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0); // Force 2, Nonhuman
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, another own Personality here
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "gripped-by-terror", PerformerId: performer.Id));
        Equal(e.GetView(1).Players[1].Army.Single(c => c.Id == target.Id).Force, 0, "4 - 4, clamped at 0");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 3, "Nonhuman performer: +1F too (2 + 1)");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == ally.Id).Force, 4, "Every own Personality at the battlefield: +1F (3 + 1)");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-2908", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, not Nonhuman
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "gripped-by-terror", PerformerId: performer.Id));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == performer.Id).Force, 3, "Not Nonhuman: no army-wide boost");
    }
    // Outnumbered (oracle-l5r-10746): "Battle: Give all your opposed Personalities with Followers at
    // the current battlefield +1F. Give all enemy Personalities there without Followers -1F." No
    // Performer or Target -- a pure battlefield-scoped, one-shot foreach-over-Army effect.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-10746", 0, Zone.Hand);
        var withFollower = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        Add(s, "training-follower", 0, Zone.Attached, province: 0, attachedTo: withFollower.Id);
        var withoutFollower = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Force 2
        var enemyWithout = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var enemyWithFollower = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: enemyWithFollower.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, AbilityId: "outnumbered"));
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == withFollower.Id).Force, 4, "3 + 1: has a Follower");
        Equal(e.GetView(0).Players[0].Army.Single(c => c.Id == withoutFollower.Id).Force, 2, "No Follower: unaffected");
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == enemyWithout.Id).Force, 3, "4 - 1: no Follower");
        Equal(e.GetView(0).Players[1].Army.Single(c => c.Id == enemyWithFollower.Id).Force, 6, "Has a Follower: unaffected");
    }
    // Preserving Honor (oracle-l5r-6060): "Battle: Target an enemy Personality at the current
    // battlefield: His controller chooses either to bow him, or to choose another location and move
    // him there. If the bowing or movement is negated, remove the Personality from the game." New
    // "preservingHonor" PendingChoice; StalwartNegatesBow's own already-returned bool (every other
    // call site discards it) feeds the remove-from-game fallback.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-6060", 0, Zone.Hand);
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // satisfies the blanket presence Require
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "preserving-honor"));
        Equal(e.State.PendingChoice?.Type, "preservingHonor");
        Equal(e.State.PendingChoice?.PlayerId, 1, "The targeted Personality's own controller answers");
        Accept(e, Cmd(e, "choosePreservingHonor", player: 1) with { Choice = "bow" });
        Check(e.State.Cards[target.Id].Bowed, "Chose to bow");
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Nothing negated the bow: not removed");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-6060", 0, Zone.Hand);
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // satisfies the blanket presence Require
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "preserving-honor"));
        Accept(e, Cmd(e, "choosePreservingHonor", player: 1) with { Choice = "move" });
        Equal(e.State.Cards[target.Id].Zone, Zone.Home, "Chose to move: moved home, not removed");
    }
    {
        // Stalwart negates the bow itself -- the detection hook fires the fallback.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-6060", 0, Zone.Hand);
        Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // satisfies the blanket presence Require
        var target = Add(s, "training-stalwart-personality", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: target.Id, AbilityId: "preserving-honor"));
        Accept(e, Cmd(e, "choosePreservingHonor", player: 1) with { Choice = "bow" });
        Check(!e.State.Cards[target.Id].Bowed, "Stalwart negated the bow itself");
        Equal(e.State.Cards[target.Id].Zone, Zone.RemovedFromGame, "The bowing was negated: removed from the game");
    }
    // Your Clan Needs You! (oracle-l5r-9687): a fourth caller into the shared Purchase/
    // RecruitReserve/CreatePersonality entry sequence, for ANY own Personality mid-Battle; new
    // own-personality-in-province Target (a province card is never InPlay, so ability.Performer's
    // own InPlay requirement could never apply); a genuinely new post-battle province-placement
    // primitive (MatchEngine.CheckYourClanNeedsYouReturn/ResolveYourClanNeedsYouProvince).
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9687", 0, Zone.Hand);
        var recruit = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: 1); // GoldCost 3 - 6 clamps to 0
        s.Players[0].Provinces[1].CardId = recruit.Id;
        s.Players[1].Provinces[0].Strength = 0;
        var enemy = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        enemy.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)]; // Force 2 -> 0, so the recruit (2) wins outright as Attacker
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: recruit.Id, AbilityId: "your-clan-needs-you"));
        Equal(e.State.Cards[recruit.Id].Zone, Zone.Battlefield, "Brought into play at the current battlefield");
        Equal(e.State.Cards[recruit.Id].ProvinceIndex, 0);
        Check(!e.State.Cards[recruit.Id].Bowed, "Enters play straightened by default");
        Resolve(e); // the recruit's own side (the Attacker) wins outright, so he survives the battle
        Equal(e.State.PendingChoice?.Type, "yourClanNeedsYouProvince", "After the battle ends, if he is in play: choose a province");
        Equal(e.State.PendingChoice?.PlayerId, 0);
        Accept(e, Cmd(e, "chooseYourClanNeedsYouProvince", player: 0, province: 2));
        Equal(e.State.Cards[recruit.Id].Zone, Zone.Province, "Put into one of your provinces, face-up");
        Check(e.State.Cards[recruit.Id].FaceUp);
        Equal(e.State.Players[0].Provinces[2].CardId, recruit.Id);
        Check(!e.State.Cards[recruit.Id].Bowed, "A fresh start, the same reset a card returning to a deck gets");
    }
    {
        // The province prompt must not reveal a face-down card's name; a face-up card is still named.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9687", 0, Zone.Hand);
        var recruit = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: 1);
        s.Players[0].Provinces[1].CardId = recruit.Id;
        var hidden = Add(s, "oracle-l5r-9687", 0, Zone.Province, province: 2); hidden.FaceUp = false;
        s.Players[0].Provinces[2].CardId = hidden.Id;
        var shown = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: 3); shown.FaceUp = true;
        s.Players[0].Provinces[3].CardId = shown.Id;
        s.Players[1].Provinces[0].Strength = 0;
        var enemy = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        enemy.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)];
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: recruit.Id, AbilityId: "your-clan-needs-you"));
        Resolve(e);
        var labels = e.QueryActions(0).Where(a => a.Type == "chooseYourClanNeedsYouProvince").ToDictionary(a => a.Command!.Province!.Value, a => a.Label);
        Check(!labels[2].Contains("Your Clan Needs You!"), "A face-down card's name is not revealed by the province prompt");
        Check(labels[3].Contains("(discarding ") && !labels[3].Contains("face-down"), "A face-up card is still named");
    }
    {
        // Card ids must not follow deck-list order: a face-down card's id (offered in target lists and commands) would identify it.
        foreach (var (name, create) in new (string, Func<ulong, MatchEngine>)[] { ("published", sd => MatchEngine.CreatePublishedMatch(sd)), ("starter", sd => MatchEngine.CreateStarterMatch(sd)),
            ("custom", sd => MatchEngine.CreateCustomMatch(sd, CustomPractice.Preset("Crane"), CustomPractice.Preset("Lion"))), ("published test", sd => MatchEngine.CreatePublishedTestMatch(sd)) })
        {
            var a = create(11); var b = create(11); var c = create(12);
            var keys = a.State.Cards.Keys.ToArray();
            // Issue #611: the dictionary is enumerated in id order, so no "for each card" list (targets, default commands, first-match lookups) follows deck-list position.
            // Player 0's Stronghold is created first, so if enumeration still followed setup order it would be first for every seed.
            Check(keys.Zip(keys.Skip(1)).All(x => string.CompareOrdinal(x.First, x.Second) < 0), $"{name}: cards are enumerated in opaque-id order");
            Check(Enumerable.Range(1, 12).Any(sd => { var m = create((ulong)sd); return m.State.Cards.Keys.First() != m.State.Players[0].StrongholdId; }), $"{name}: setup order does not decide enumeration order");
            Check(keys.SequenceEqual(b.State.Cards.Keys), $"{name}: the same seed still yields the same ids");
            Check(keys.Select(k => a.State.Cards[k].DefinitionId).SequenceEqual(b.State.Cards.Keys.Select(k => b.State.Cards[k].DefinitionId)), $"{name}: and the same cards");
            Check(!keys.SequenceEqual(c.State.Cards.Keys), $"{name}: a different seed yields a different id order");
            Check(keys.Distinct().Count() == keys.Length, $"{name}: ids are unique");
        }
    }
    {
        // Issue #611: a Personality in play carries a match-unique, stable "Unit N" number from the engine (the card id is opaque and means nothing to the client).
        foreach (var (name, create) in new (string, Func<ulong, MatchEngine>)[] { ("published", sd => MatchEngine.CreatePublishedMatch(sd)), ("starter", sd => MatchEngine.CreateStarterMatch(sd)) })
        {
            var m = create(31); var seen = new Dictionary<string, int>();
            for (var step = 0; step < 400 && m.State.WinnerId is null; step++)
            {
                var actions = m.QueryActions(m.State.PriorityPlayerId).Where(a => a.Enabled && a.Command is not null && a.Type != "concede").ToArray();
                if (actions.Length == 0) break;
                for (var k = 0; k < actions.Length; k++) if (m.Execute(actions[(step * 7 + k) % actions.Length].Command!).Accepted) break;
                var st = m.State;
                foreach (var c in st.Cards.Values.Where(c => CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && (c.Zone is Zone.Home or Zone.Battlefield or Zone.Attached)))
                    Check(c.UnitNumber > 0, $"{name}: a Personality in play is numbered");
                var numbered = st.Cards.Values.Where(c => c.UnitNumber > 0).ToArray();
                Check(numbered.Select(c => c.UnitNumber).Distinct().Count() == numbered.Length, $"{name}: unit numbers are unique");
                foreach (var c in numbered) { if (seen.TryGetValue(c.Id, out var before)) Check(before == c.UnitNumber, $"{name}: a unit's number never changes"); seen[c.Id] = c.UnitNumber; }
            }
            Check(seen.Count >= 3, $"{name}: the walk brought several Personalities into play ({seen.Count})");
            var shown = m.GetView(0).Players.SelectMany(p => p.Home.Concat(p.Army)).Where(c => c.Definition?.Type == CardType.Personality).ToArray();
            Check(shown.All(c => c.UnitNumber > 0), $"{name}: the view carries the number");
        }
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var strat = Add(s, "oracle-l5r-9687", 0, Zone.Hand);
        var recruit = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: 1);
        s.Players[0].Provinces[1].CardId = recruit.Id;
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: recruit.Id, AbilityId: "your-clan-needs-you"));
    }
    // Sanctuary of the Waves (oracle-l5r-10238, spell-wave-4, Water): "Battle: Bow this card and
    // target this Shugenja, or, if this Shugenja is Water, you may target another of your
    // Personalities: Move the target to the current battlefield if he would be opposed there.
    // Straighten him if he moved." New own-wielder-or-personality-with-wielder-keyword Target -- the
    // Target-side mirror of own-wielder-or-source-with-wielder-keyword.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-shugenja", 0, Zone.Home, true); // bowed, at Home, not Water
        var spell = Add(s, "oracle-l5r-10238", 0, Zone.Attached, attachedTo: wielder.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // opposed
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: wielder.Id, AbilityId: "sanctuary-of-the-waves-move"));
        Check(e.State.Cards[spell.Id].Bowed, "Bow this card, as its own cost");
        Equal(e.State.Cards[wielder.Id].Zone, Zone.Battlefield, "Moved to the current battlefield: would be opposed there");
        Check(!e.State.Cards[wielder.Id].Bowed, "Straightened: he moved");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-shugenja", 0, Zone.Home); // not Water
        var spell = Add(s, "oracle-l5r-10238", 0, Zone.Attached, attachedTo: wielder.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: other.Id, AbilityId: "sanctuary-of-the-waves-move"));
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0); // Water; already at the battlefield
        var spell = Add(s, "oracle-l5r-10238", 0, Zone.Attached, province: 0, attachedTo: wielder.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home, true); // bowed, at Home
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: other.Id, AbilityId: "sanctuary-of-the-waves-move"));
        Equal(e.State.Cards[other.Id].Zone, Zone.Battlefield, "Water wielder: a different own Personality may be targeted, and moves in");
        Check(!e.State.Cards[other.Id].Bowed, "Straightened: he moved");
        Equal(e.State.Cards[wielder.Id].Zone, Zone.Battlefield, "The wielder himself stays where he was");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-shugenja", 0, Zone.Home, true);
        var spell = Add(s, "oracle-l5r-10238", 0, Zone.Attached, attachedTo: wielder.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, TargetId: wielder.Id, AbilityId: "sanctuary-of-the-waves-move"));
        Equal(e.State.Cards[wielder.Id].Zone, Zone.Home, "Not opposed: no move");
        Check(e.State.Cards[wielder.Id].Bowed, "Never moved, so never straightened");
    }
    // False Ring of Earth (oracle-l5r-10532, ring-wave-2): Ring recruitment's own existing
    // GrantRingRecruit shape (issue #194), triggered from Dueling.ResolveDuel via a new
    // Dueling.SharesNoKeywords comparison, plus the first negative-magnitude MaxHandSize use.
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var ringId = PublishedContent.RingWave2.Cards.Single(c => c.Name == "False Ring of Earth").Id;
        var ring = Add(s, ringId, 1, Zone.Hand);
        var challenger = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Chi 4, no keywords
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); // Chi 3, no keywords
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Check(e.State.PendingDuel is null, "The duel is fully resolved (neither side has Duelist)");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 1 && g.ScopeCardIds is { Length: 1 } ids && ids[0] == ring.Id),
            "False Ring of Earth becomes recruitable: winner and loser shared no keywords");
        var honorBefore = e.State.Players[1].Honor;
        Accept(e, Cmd(e, "pass")); // priority passed to player 0 after the duel resolved -- hand it back
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: ring.Id, AbilityId: "false-ring-of-earth-recruit"));
        Equal(e.State.Cards[ring.Id].Zone, Zone.Home, "Put into play");
        Equal(e.State.Players[1].Honor, honorBefore - 2, "Lose 2 Honor");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var ringId = PublishedContent.RingWave2.Cards.Single(c => c.Name == "False Ring of Earth").Id;
        var ring = Add(s, ringId, 1, Zone.Hand);
        var challenger = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0); // Chi 3, Shugenja
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0); // Chi 2, Shugenja too
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Check(!e.State.AdditionalActions.Any(g => g.ScopeCardIds is { Length: 1 } ids && ids[0] == ring.Id),
            "Shared the Shugenja keyword: not recruitable");
    }
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var ringId = PublishedContent.RingWave2.Cards.Single(c => c.Name == "False Ring of Earth").Id;
        Add(s, ringId, 1, Zone.Home);
        while (s.Players[0].Hand.Count < 7) Add(s, "training-rally", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, "endDynasty"));
        Equal(e.State.Players[0].Hand.Count, 8, "One over the lowered threshold of 7 (8 printed - 1 from the opponent's False Ring of Earth) after the End Phase draw");
        Equal(e.State.PendingChoice!.Type, "discardHand");
        Equal(e.State.PendingChoice!.Prompt, "Discard 1 card(s) to return to 7.");
    }
}
// Brawl (issue #304): its "Needs Regions (not built)" blocker was stale -- Regions and Terrains both
// already existed. Two paired abilities of one Strategy (one shared UsageKey): the bow half reuses
// enemy-card-without-attachments verbatim; the Battle/Open destroy half is the new region-or-terrain
// Target (exempt from the shared "Battle-timing target must be at the current battlefield" Require) plus
// DestroyRegion, and reuses DiscardTerrain and Ring of Water's own destroyed-a-Terrain flag.
static void StrategyWave16Primitives()
{
    var definition = CardCatalog.Get("oracle-l5r-1127");
    Equal(PublishedContent.StrategyWave16.Cards.Length, 1, "Strategy wave 16 publishes exactly Brawl");
    Equal(definition.Type, CardType.Strategy); Equal(definition.GoldCost, 0); Equal(definition.Focus, 1);
    Check(definition.Abilities!.SequenceEqual(new[] { "brawl-bow", "brawl-destroy" }), "Both printed halves are wired");
    Check(AbilityCatalog.All["brawl-bow"].UsageKey is not null && AbilityCatalog.All["brawl-bow"].UsageKey == AbilityCatalog.All["brawl-destroy"].UsageKey, "The two halves share one UsageKey");
    Equal(AbilityCatalog.All["brawl-bow"].Timing, "Battle"); Equal(AbilityCatalog.All["brawl-destroy"].Timing, "BattleOrOpen");

    GameCommand Use(MatchEngine e, string ability, CardInstance brawl, CardInstance target) => new("useAbility", 0, e.Version, CardId: brawl.Id, TargetId: target.Id, AbilityId: ability);
    MatchState Fixture(out CardInstance brawl)
    {
        var s = Battle(5, 2).State; s.PriorityPlayerId = 0;
        brawl = Add(s, "oracle-l5r-1127", 0, Zone.Hand);
        return s;
    }

    // A Region at ANY province -- here the defender's province 1, not the battlefield (province 0) --
    // is destroyed to its owner's Dynasty discard, and its static effect goes with it.
    {
        var s = Fixture(out var brawl);
        var region = Add(s, "oracle-l5r-9865", 1, Zone.Region, province: 1);
        s.Players[1].Provinces[1].RegionId = region.Id;
        var e = MatchEngine.FromTestState(s);
        var before = e.GetView(0).Players[1].Provinces[1].Strength;
        Accept(e, Use(e, "brawl-destroy", brawl, region));
        Equal(e.State.Cards[region.Id].Zone, Zone.DynastyDiscard, "The Region is destroyed to its owner's Dynasty discard");
        Check(e.State.Cards[region.Id].ProvinceIndex is null, "A destroyed Region no longer names a province");
        Check(e.State.Players[1].Provinces[1].RegionId is null, "The province's own pointer to the Region is cleared");
        Equal(e.GetView(0).Players[1].Provinces[1].Strength, before - 2, "Fallow Lands' +2 strength leaves with it");
        Equal(e.State.Cards[brawl.Id].Zone, Zone.FateDiscard, "Brawl itself is spent");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "regionDestroyed"), "A regionDestroyed event is emitted");
    }
    // Ownership is unrestricted: the acting player's own Region may be destroyed too.
    {
        var s = Fixture(out var brawl);
        var region = Add(s, "oracle-l5r-9865", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = region.Id;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, "brawl-destroy", brawl, region));
        Equal(e.State.Cards[region.Id].Zone, Zone.DynastyDiscard); Check(e.State.Players[0].Provinces[0].RegionId is null);
    }
    // A Terrain in play is destroyed through the shared DiscardTerrain removal, and counts as Ring of
    // Water's own "destroyed a Terrain" condition (issue #194) for the acting player.
    {
        var s = Fixture(out var brawl);
        var terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.Players[0].TerrainDestroyedThisBattle, "Baseline: no Terrain destroyed yet");
        Accept(e, Use(e, "brawl-destroy", brawl, terrain));
        Equal(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "The Terrain leaves play");
        Check(e.State.Cards[terrain.Id].ProvinceIndex is null);
        Check(e.State.Players[0].TerrainDestroyedThisBattle, "Ring of Water's destroyed-a-Terrain condition is met");
    }
    // Only a Region or a Terrain is a legal destroy target -- never a Personality -- and the offered
    // action lists exactly the Region and the Terrain (the candidates the client target picker shows).
    {
        var s = Fixture(out var brawl);
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        var region = Add(s, "oracle-l5r-9865", 1, Zone.Region, province: 1);
        s.Players[1].Provinces[1].RegionId = region.Id;
        var terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, "brawl-destroy", brawl, defender));
        var offered = e.QueryActions(0).Single(a => a.Command?.AbilityId == "brawl-destroy");
        Check(offered.Enabled, "Brawl's destroy half is offered when a Region and a Terrain are in play");
        Check(offered.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { region.Id, terrain.Id }.OrderBy(x => x)), "Offered targets are exactly the Region and the Terrain");
    }
    // With no Region or Terrain in play the destroy half is offered but disabled.
    {
        var s = Fixture(out var brawl);
        var e = MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Single(a => a.Command?.AbilityId == "brawl-destroy" || a.Label.StartsWith("Target a Region")).Enabled, "Nothing to destroy: the destroy half is disabled");
    }
    // Battle/Open: the destroy half also works outside a Battle (a Region at any province), while the
    // Battle-only bow half does not -- proven against the same enemy Personality that the bow half
    // legally bows once a Battle is under way (the control just below).
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.PriorityPlayerId = 0; // Open timing is the Action phase's own.
        var brawl = Add(s, "oracle-l5r-1127", 0, Zone.Hand);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Home);
        var region = Add(s, "oracle-l5r-9865", 1, Zone.Region, province: 0);
        s.Players[1].Provinces[0].RegionId = region.Id;
        var e = MatchEngine.FromTestState(s);
        Check(e.State.Phase != Phase.Battle, "Not a Battle: the Open half is being exercised");
        Reject(e, new GameCommand("useAbility", e.State.PriorityPlayerId, e.Version, CardId: brawl.Id, TargetId: enemy.Id, AbilityId: "brawl-bow"));
        Accept(e, new GameCommand("useAbility", e.State.PriorityPlayerId, e.Version, CardId: brawl.Id, TargetId: region.Id, AbilityId: "brawl-destroy"));
        Equal(e.State.Cards[region.Id].Zone, Zone.DynastyDiscard, "Destroyed at Open timing, outside any Battle");
    }
    // The bow half: an enemy card without attachments bows; one wearing an attachment does not qualify;
    // and your own card is never a target.
    {
        var s = Fixture(out var brawl);
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        var attacker = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, "brawl-bow", brawl, attacker));
        Accept(e, Use(e, "brawl-bow", brawl, defender));
        Check(e.State.Cards[defender.Id].Bowed, "The unattached enemy Personality is bowed");
    }
    {
        var s = Fixture(out var brawl);
        var defender = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        Add(s, "training-follower-crane-guard", 1, Zone.Attached, attachedTo: defender.Id);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, "brawl-bow", brawl, defender));
        Check(!e.State.Cards[defender.Id].Bowed, "A Personality with an attachment is not a legal bow target");
    }
}
// Issue #317: three target categories ValidateAbility accepts but AbilityActions' candidates switch never
// listed. A target in a discard pile is not in the in-play default, so the ability was never offered by
// QueryActions and could only be taken by submitting the command directly (the same gap Kharmic had, #303).
static void PileTargetsAreOffered()
{
    // The ability is offered enabled, lists exactly its legal targets, and every offered target validates.
    void Offered(MatchEngine e, string ability, string[] expected)
    {
        var option = e.QueryActions(0).SingleOrDefault(a => a.Command?.AbilityId == ability);
        Check(option is not null && option.Enabled, $"{ability} is offered enabled");
        Check(option!.TargetIds!.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), $"{ability} offers exactly its legal targets");
        foreach (var target in expected) Accept(MatchEngine.FromTestState(e.State), option.Command! with { TargetId = target });
    }
    MatchState Fixture()
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        return s;
    }
    // Hunter of Harmony: a Kiho Strategy in the player's OWN Fate discard pile.
    {
        var s = Fixture();
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "oracle-l5r-3522", 0, Zone.Attached, attachedTo: host.Id);
        var volcano = Add(s, "oracle-l5r-9186", 0, Zone.FateDiscard);
        var weakness = Add(s, "oracle-l5r-8425", 0, Zone.FateDiscard);
        Add(s, "training-rally", 0, Zone.FateDiscard);   // a Strategy, but not Kiho
        Add(s, "oracle-l5r-9186", 1, Zone.FateDiscard);  // Kiho, but the opponent's discard pile
        Add(s, "oracle-l5r-9186", 0, Zone.Hand);         // Kiho, but in hand
        Offered(MatchEngine.FromTestState(s), "hunter-of-harmony-return", [volcano.Id, weakness.Id]);
    }
    // Scholarly Hermit: a Ring in the player's OWN Fate discard pile.
    {
        var s = Fixture();
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "oracle-l5r-10684", 0, Zone.Attached, attachedTo: host.Id);
        var air = Add(s, "oracle-l5r-6333", 0, Zone.FateDiscard);
        var water = Add(s, "oracle-l5r-6339", 0, Zone.FateDiscard);
        Add(s, "oracle-l5r-6335", 1, Zone.FateDiscard);  // a Ring, but the opponent's discard pile
        Add(s, "oracle-l5r-6338", 0, Zone.Home);         // a Ring, but in play rather than in a discard pile
        Add(s, "oracle-l5r-6334", 0, Zone.Hand);         // a Ring, but in hand
        Add(s, "oracle-l5r-9186", 0, Zone.FateDiscard);  // in the right pile, but not a Ring
        Offered(MatchEngine.FromTestState(s), "scholarly-hermit-search", [air.Id, water.Id]);
    }
    // Tranquil Garden: any Ring in play (either side's), or a Ring in the player's OWN Fate discard pile.
    {
        var s = Fixture();
        Add(s, "oracle-l5r-8721", 0, Zone.Home);
        var ownInPlay = Add(s, "oracle-l5r-6333", 0, Zone.Home, bowed: true);
        var enemyInPlay = Add(s, "oracle-l5r-6335", 1, Zone.Home, bowed: true);
        var ownDiscard = Add(s, "oracle-l5r-6339", 0, Zone.FateDiscard);
        Add(s, "oracle-l5r-6334", 1, Zone.FateDiscard);  // a Ring, but the opponent's discard pile
        Add(s, "oracle-l5r-6338", 0, Zone.Hand);         // a Ring, but in hand
        Add(s, "oracle-l5r-9186", 0, Zone.FateDiscard);  // in the right pile, but not a Ring
        Offered(MatchEngine.FromTestState(s), "tranquil-garden-ring", [ownInPlay.Id, enemyInPlay.Id, ownDiscard.Id]);
    }
    // With nothing to target the ability is offered disabled (not silently absent, not enabled).
    {
        var s = Fixture();
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "oracle-l5r-3522", 0, Zone.Attached, attachedTo: host.Id);
        Add(s, "oracle-l5r-10684", 0, Zone.Attached, attachedTo: host.Id);
        var e = MatchEngine.FromTestState(s);
        foreach (var label in new[] { "Remove this card from the game", "Bow: Put a Ring" })
            Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Label.StartsWith(label) && !a.Enabled), $"'{label}...' is offered disabled with no legal target");
    }
}
static void BatchEPrimitives()
{
    // The pinned wave itself: nine Personalities plus the two created-only tokens.
    Equal(PublishedContent.PersonalityWave21.Cards.Length, 11, "Nine Personalities + Ohsuki's Sparrow + Tokoru's Bear");
    foreach (var tokenId in new[] { "oracle-l5r-10023", "oracle-l5r-10025" })
        Check(CardCatalog.Get(tokenId).CreatedOnly, "Both tokens are created-only, exactly like personality-wave-17's own Lion Ancestor");
    Check(CardCatalog.Get("oracle-l5r-6906").HonorRequirement is null, "Shinjo Kinto's scan prints '-': no Honor Requirement at all");

    // ------------------------------------------------------------------------------------------
    // The ONE shared Honor Requirement waiver (CardDefinition.HonorRequirementWaiverClan): Akodo
    // Chiyo (Crab) and Kitsu Ririko (Unicorn), both Lion Personalities with Honor Requirement 7.
    // ------------------------------------------------------------------------------------------
    static MatchEngine DynastyWith(string definitionId, string clan, int honor)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = clan; s.Players[0].Honor = honor;
        var province = s.Cards[s.Players[0].Provinces[0].CardId!]; province.DefinitionId = definitionId; province.FaceUp = true;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        return MatchEngine.FromTestState(s);
    }
    Equal(CardCatalog.Get("oracle-l5r-10290").HonorRequirementWaiverClan, "Crab", "Chiyo's own printed waiver");
    Equal(CardCatalog.Get("oracle-l5r-10640").HonorRequirementWaiverClan, "Unicorn", "Ririko's own printed waiver");
    foreach (var (card, waivedClan) in new[] { ("oracle-l5r-10290", "Crab"), ("oracle-l5r-10640", "Unicorn") })
        foreach (var clan in new[] { "Crab", "Unicorn", "Crane", "Lion" })
        {
            var e = DynastyWith(card, clan, 3); // Family Honor 3, below the printed Honor Requirement of 7
            var quote = e.QuotePurchase(0, ProvinceCard(e));
            if (clan == waivedClan) Check(quote.Error is null, $"{card}: a {clan} player ignores the Honor Requirement (got: {quote.Error})");
            else Check(quote.Error is not null && quote.Error.Contains("Honor Requirement"), $"{card}: a {clan} player is still bound by the Honor Requirement (crossed/wrong-clan negative)");
            // Honor high enough: never blocked for ANY clan, waiver or not (the control).
            var rich = DynastyWith(card, clan, 7);
            Check(rich.QuotePurchase(0, ProvinceCard(rich)).Error is null, $"{card}: a {clan} player with Honor 7 meets the requirement outright");
        }
    {
        // An actual Purchase, end to end: a Crab player with Honor 3 recruits Chiyo without any
        // Clan-allegiance surcharge (a different, separate waiver route) -- and pays exactly her printed 7.
        var e = DynastyWith("oracle-l5r-10290", "Crab", 3);
        var id = ProvinceCard(e); var quote = e.QuotePurchase(0, id);
        Equal(quote.Cost, 7, "No allegiance surcharge -- this is the printed waiver, not the +2 Gold allegiance route");
        Accept(e, Cmd(e, "purchase", id: id, sources: quote.Payments[0].SourceIds));
        Equal(e.State.Cards[id].Zone, Zone.Home, "Entered play");
        Equal(e.State.Players[0].Honor, 3, "Honor untouched by the waiver");
    }
    {
        // Ririko: the same waiver for Unicorn, plus Destined's own draw firing off the waived entry
        // (the keyword itself was already built; this proves the two compose).
        var e = DynastyWith("oracle-l5r-10640", "Unicorn", 3);
        var id = ProvinceCard(e); var quote = e.QuotePurchase(0, id);
        Equal(quote.Cost, 5);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Cmd(e, "purchase", id: id, sources: quote.Payments[0].SourceIds));
        Equal(e.State.Cards[id].Zone, Zone.Home, "Entered play ignoring her Honor Requirement");
        Equal(e.State.Players[0].Hand.Count, handBefore + 1, "Destined: draw a card after your Destined card enters play");
        // The wrong clan cannot buy her at all (control for the Reject path through a real command).
        var lion = DynastyWith("oracle-l5r-10640", "Lion", 3);
        Reject(lion, Cmd(lion, "purchase", id: ProvinceCard(lion), sources: Sources(lion, 0)));
    }

    // ------------------------------------------------------------------------------------------
    // Akodo Chiyo: Battle: Melee 4 Attack.
    // ------------------------------------------------------------------------------------------
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var chiyo = Add(s, "oracle-l5r-10290", 0, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: chiyo.Id, TargetId: weak.Id, AbilityId: "chiyo-attack"));
        Equal(e.State.Cards[weak.Id].Zone, Zone.Dead, "Melee 4 destroys a Force-4 target (an unwired attack id would silently resolve as a 0-strength Attack)");
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var chiyo = Add(s, "oracle-l5r-10290", 0, Zone.Battlefield, province: 0);
        var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        AcceptSurvive(e, new GameCommand("useAbility", 0, e.Version, CardId: chiyo.Id, TargetId: strong.Id, AbilityId: "chiyo-attack"), strong.Id); // Force 5 > Melee 4: legal target, survives
    }

    // ------------------------------------------------------------------------------------------
    // Matsu Kaido: +1F while attacking; Fear Battle: reduce an enemy Personality/Follower's Force to 0.
    // ------------------------------------------------------------------------------------------
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        Add(s, "oracle-l5r-4934", 0, Zone.Battlefield, province: 0); // printed Force 4
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        Resolve(e); AssertBattleForces(e, 5, 6); // 4 + 1 while attacking
    }
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1;
        Add(s, "oracle-l5r-4934", 0, Zone.Battlefield, province: 0); // Kaido DEFENDING: no bonus
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var e = MatchEngine.FromTestState(s);
        Resolve(e); AssertBattleForces(e, 5, 4);
    }
    {
        // "Reduce its Force to 0": a delta off the target's LIVE Force (printed 6 + a +2 modifier = 8).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kaido = Add(s, "oracle-l5r-4934", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        target.Modifiers = [new("test-fixture", "test-fixture", "Force", 2)];
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: kaido.Id, TargetId: own.Id, AbilityId: "kaido-force-zero")); // enemy only
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kaido.Id, TargetId: target.Id, AbilityId: "kaido-force-zero"));
        Resolve(e); AssertBattleForces(e, 5 + 3, 0); // Kaido 5 (attacking) + the own Crane samurai's 3; the enemy is at 0
    }
    {
        // A Follower target: its own Force goes to 0, its host's does not.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kaido = Add(s, "oracle-l5r-4934", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: host.Id); // +2 unit Force
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kaido.Id, TargetId: follower.Id, AbilityId: "kaido-force-zero"));
        Resolve(e); AssertBattleForces(e, 5, 4);
    }

    // ------------------------------------------------------------------------------------------
    // Matsu Miura: Force penalty equal to Miura's (current) Personal Honor.
    // ------------------------------------------------------------------------------------------
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var miura = Add(s, "oracle-l5r-10853", 0, Zone.Battlefield, province: 0); // Personal Honor 3
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: miura.Id, TargetId: target.Id, AbilityId: "miura-force-penalty"));
        Equal(e.State.Cards[target.Id].Modifiers.Single(m => m.Effect == "miura-force-penalty").Amount, -3);
        Resolve(e); AssertBattleForces(e, 3, 3); // Miura's own 3 vs. the target's 6 - 3
    }
    {
        // "Current" Personal Honor: a Dishonorable Miura's is 0, so there is nothing to subtract.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var miura = Add(s, "oracle-l5r-10853", 0, Zone.Battlefield, province: 0); miura.Dishonorable = true;
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: miura.Id, TargetId: target.Id, AbilityId: "miura-force-penalty"));
        Equal(e.State.Cards[target.Id].Modifiers.Where(m => m.Effect == "miura-force-penalty").Sum(m => m.Amount), 0);
    }

    // ------------------------------------------------------------------------------------------
    // Utaku Mai: move to the current battlefield (from anywhere), then a Personal-Honor Force penalty.
    // ------------------------------------------------------------------------------------------
    {
        // From Home, with NO other presence of her own at the battlefield at all.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mai = Add(s, "oracle-l5r-9086", 0, Zone.Home); // Personal Honor 4
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mai.Id, TargetId: target.Id, AbilityId: "mai-move-force-penalty"));
        Equal(e.State.Cards[mai.Id].Zone, Zone.Battlefield, "Moved to the current battlefield");
        Equal(e.State.Cards[mai.Id].ProvinceIndex, 0);
        Equal(e.State.Cards[target.Id].Modifiers.Single(m => m.Effect == "mai-move-force-penalty").Amount, -4, "Force penalty equal to Mai's Personal Honor (4)");
    }
    {
        // Already at the battlefield: the move is a no-op and the penalty still applies.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mai = Add(s, "oracle-l5r-9086", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mai.Id, TargetId: target.Id, AbilityId: "mai-move-force-penalty"));
        Equal(e.State.Cards[mai.Id].ProvinceIndex, 0);
        Equal(e.State.Cards[target.Id].Modifiers.Single(m => m.Effect == "mai-move-force-penalty").Amount, -4);
    }
    {
        // A bowed Mai cannot act at all -- the exemption is from PRESENCE, never from being unbowed.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mai = Add(s, "oracle-l5r-9086", 0, Zone.Home, true);
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mai.Id, TargetId: target.Id, AbilityId: "mai-move-force-penalty"));
    }
    {
        // The TARGET must still be at the current battlefield (only Mai herself is exempt).
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mai = Add(s, "oracle-l5r-9086", 0, Zone.Home);
        var elsewhere = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mai.Id, TargetId: elsewhere.Id, AbilityId: "mai-move-force-penalty"));
    }
    {
        // Control: the reinforcesFromElsewhere exemption is Mai-specific -- Miura, identical but for that
        // exemption, cannot use the same shape from Home with no presence at the battlefield.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var miura = Add(s, "oracle-l5r-10853", 0, Zone.Home);
        var target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: miura.Id, TargetId: target.Id, AbilityId: "miura-force-penalty"));
    }

    // ------------------------------------------------------------------------------------------
    // Shinjo Kinto: move an enemy Personality home; un-negatable movement iff Reconnaissance.
    // ------------------------------------------------------------------------------------------
    {
        // No negating interrupt in play: an ordinary move home.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kinto = Add(s, "oracle-l5r-6906", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinto.Id, TargetId: target.Id, AbilityId: "kinto-move"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Home);
    }
    {
        // WITHOUT Reconnaissance a Tamori Tamoko interrupt IS offered, and negating really stops the move.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kinto = Add(s, "oracle-l5r-6906", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var tamoko = Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinto.Id, TargetId: target.Id, AbilityId: "kinto-move"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "No Reconnaissance: the movement is offered for negation");
        ExpectBeforeActionOffer(e, tamoko.Id);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: tamoko.Id, AbilityId: "tamoko-negate"));
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Negated: Kinto's movement never applied");
    }
    {
        // WITH Reconnaissance at the current battlefield the window is never even opened.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kinto = Add(s, "oracle-l5r-6906", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0);
        s.Players[1].Provinces[0].Reconnaissance[0] = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinto.Id, TargetId: target.Id, AbilityId: "kinto-move"));
        Check(e.State.PendingChoice is null, "Reconnaissance: no negation window at all");
        Equal(e.State.Cards[target.Id].Zone, Zone.Home, "Moved home, un-negated");
    }
    {
        // Reconnaissance at a DIFFERENT province than the current battlefield does not count.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kinto = Add(s, "oracle-l5r-6906", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0);
        s.Players[1].Provinces[1].Reconnaissance[0] = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinto.Id, TargetId: target.Id, AbilityId: "kinto-move"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "Reconnaissance elsewhere does not apply here");
    }
    {
        // The OTHER player having Reconnaissance there does not help Kinto's controller either.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var kinto = Add(s, "oracle-l5r-6906", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0);
        s.Players[1].Provinces[0].Reconnaissance[1] = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinto.Id, TargetId: target.Id, AbilityId: "kinto-move"));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
    }
    {
        // Champion of Thunder's own "negate all effects" is likewise offered against Kinto without
        // Reconnaissance, and suppressed with it -- the shared MovementCannotBeNegated check, not just
        // Tamoko's own move-negation list.
        foreach (var recon in new[] { false, true })
        {
            var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
            var kinto = Add(s, "oracle-l5r-6906", 0, Zone.Battlefield, province: 0);
            var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id);
            if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kinto.Id, TargetId: host.Id, AbilityId: "kinto-move"));
            if (recon)
            {
                Check(e.State.PendingChoice is null, "Reconnaissance: Champion of Thunder is never offered a window");
                Equal(e.State.Cards[host.Id].Zone, Zone.Home);
            }
            else ExpectBeforeActionOffer(e, champion.Id, "No Reconnaissance: Champion of Thunder may negate");
        }
    }
    {
        // Wrathful Ancestor ("This movement will not be negated", unconditionally) now goes through the
        // same shared check: Champion of Thunder used to be able to catch it despite the printed text.
        var s = StrongholdBattle("oracle-l5r-1218");
        var strat = Add(s, "oracle-l5r-9390", 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        performer.GrantedKeywords = [new("Spirit", "test-fixture", "Permanent")];
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-1292", 1, Zone.Attached, province: 0, attachedTo: host.Id); // Champion of Thunder
        Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strat.Id, TargetId: host.Id, AbilityId: "wrathful-ancestor-move", PerformerId: performer.Id));
        Check(e.State.PendingChoice is null, "Neither Tamoko nor Champion of Thunder is offered a window");
        Equal(e.State.Cards[host.Id].Zone, Zone.Home);
    }

    // ------------------------------------------------------------------------------------------
    // Kakita Maratai: the duelWonReaction window broadened to ANY of its controller's Personalities.
    // ------------------------------------------------------------------------------------------
    static (MatchEngine Engine, CardInstance Maratai) DuelWon(bool ownWins, bool maratBowed = false)
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var maratai = Add(s, "oracle-l5r-4197", 1, Zone.Home, maratBowed); // NOT the duelist
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var challenger = Add(s, ownWins ? "training-crane-veteran" : "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 4 vs. 3
        var enemy = Add(s, ownWins ? "training-crane-samurai" : "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Check(e.State.PendingDuel is null, "The duel is fully resolved");
        return (e, maratai);
    }
    {
        var (e, maratai) = DuelWon(ownWins: true);
        Equal(e.State.PendingChoice?.Type, "duelWonReaction", "One of Maratai's controller's Personalities won: her window opens even though she was not the duelist");
        Equal(e.State.PendingChoice?.PlayerId, 1);
        var honorBefore = e.State.Players[1].Honor;
        Check(e.State.FavorPlayerId is null, "Test setup: nobody holds the Favor yet");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: maratai.Id, AbilityId: "maratai-statue"));
        Check(e.State.Cards[maratai.Id].Bowed, "Bow Maratai: the printed cost");
        Equal(e.State.Players[1].Honor, honorBefore + 2, "Gain 2 Honor");
        Equal(e.State.FavorPlayerId, 1, "Take the Imperial Favor");
        Check(e.State.PendingChoice is null, "Issue #644: nothing is left to react with, so the window closes without a pass, like every other duelWonReaction");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: maratai.Id, AbilityId: "maratai-statue")); // bowed now, and once only
    }
    {
        // "your Personality won": the ENEMY's Personality winning never offers Maratai's controller a window.
        var (e, _) = DuelWon(ownWins: false);
        Check(e.State.PendingChoice is null, "Player 0's Personality won; player 1 (Maratai's controller) has nothing to react to");
    }
    {
        // A bowed Maratai could never pay her own bow cost: no window is opened just for her.
        var (e, _) = DuelWon(ownWins: true, maratBowed: true);
        Check(e.State.PendingChoice is null);
    }
    {
        // A window may also be used to take the Favor away from the opponent who held it.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.FavorPlayerId = 0;
        var maratai = Add(s, "oracle-l5r-4197", 1, Zone.Home);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var challenger = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version)); Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: maratai.Id, AbilityId: "maratai-statue"));
        Equal(e.State.FavorPlayerId, 1, "Taken from player 0");
    }
    {
        // No regression to the existing own-won-duel Reaction: Kitsuki Daisuke, Experienced (Duelist) wins
        // AND a Maratai is in play -- BOTH reactions are usable in the one window, independently.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var daisuke = Add(s, "oracle-l5r-4407", 1, Zone.Battlefield, province: 0); // Chi 4, Duelist
        var maratai = Add(s, "oracle-l5r-4197", 1, Zone.Home);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: daisuke.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version)); Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        Equal(e.State.PendingChoice?.Type, "duelWonReaction");
        Equal(e.State.PendingChoice?.CardId, daisuke.Id, "Still scoped to the winning card, exactly as before");
        var handBefore = e.State.Players[1].Hand.Count; var honorBefore = e.State.Players[1].Honor;
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: daisuke.Id, AbilityId: "daisuke-exp-draw"));
        Equal(e.State.Players[1].Hand.Count, handBefore + 1, "Daisuke's own reaction unaffected");
        Equal(e.State.PriorityPlayerId, 1, "the opponent holds nothing to react with and is not asked to pass: Maratai's controller still holds a reaction (issue #644)");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: maratai.Id, AbilityId: "maratai-statue"));
        Equal(e.State.Players[1].Honor, honorBefore + 2, "Maratai's reaction in the very same window");
    }
    {
        // ...and a Daisuke-only window (no Maratai anywhere) is completely unchanged.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var daisuke = Add(s, "oracle-l5r-4407", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: daisuke.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version)); Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        Equal(e.State.PendingChoice?.Prompt, "After the duel resolved, you may use this card's own reaction, or pass.", "The original own-won-duel prompt, verbatim");
    }

    // ------------------------------------------------------------------------------------------
    // Kitsune Ohsuki / Kitsune Tokoru: create a token from inside their own entry Reaction window.
    // ------------------------------------------------------------------------------------------
    static MatchEngine MantisDynasty(string definitionId, bool withWatcher = false)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = "Mantis";
        var province = s.Cards[s.Players[0].Provinces[0].CardId!]; province.DefinitionId = definitionId; province.FaceUp = true;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        // A Singh Remnants-style watcher in hand: it would open ITS OWN entry window for the created
        // token, were CreatePersonality allowed to -- overwriting the creator's still-open window.
        if (withWatcher) Add(s, "training-personality-entry-watcher", 0, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }
    {
        // Ohsuki (Loyal Mantis, in-clan): bought normally -> a Proclaim window carrying her own Reaction.
        var e = MantisDynasty("oracle-l5r-4469", withWatcher: true);
        var id = ProvinceCard(e); var quote = e.QuotePurchase(0, id);
        Check(quote.Error is null, quote.Error ?? ""); Check(quote.ProclaimEligible, "In-clan: Proclaim-eligible, so the window is a 'proclaim' one");
        Accept(e, Cmd(e, "purchase", id: id, sources: quote.Payments[0].SourceIds));
        Equal(e.State.PendingChoice?.Type, "proclaim");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "ohsuki-create"));
        var sparrow = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10023");
        Equal(sparrow.OwnerId, 0); Equal(sparrow.Zone, Zone.Home); Check(sparrow.FaceUp);
        Check(sparrow.Id.StartsWith("created-"), "A minted runtime instance, exactly like Kitsu Akai's Lion Ancestor");
        Equal(e.State.PendingChoice?.Type, "proclaim", "The creator's own window was NOT clobbered by any window for the created token");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "ohsuki-create")); // once per entry
        // ...so Proclaim, still owed to the player, remains available afterward; the opponent holds nothing to react with and is not asked to pass (issue #644).
        Equal(e.State.PriorityPlayerId, 0);
        Reject(e, Cmd(e, "declineReaction", player: 1));
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Cmd(e, "proclaim"));
        Equal(e.State.Players[0].Honor, honorBefore + CardCatalog.Get("oracle-l5r-4469").PersonalHonor, "Proclaim still works after the token was created");
    }
    {
        // Not Proclaim-eligible (bought with the Clan discount): a plain entryReaction window instead.
        var e = MantisDynasty("oracle-l5r-4469", withWatcher: true);
        var id = ProvinceCard(e); var quote = e.QuotePurchase(0, id, clanDiscount: true);
        Check(quote.Error is null, quote.Error ?? "");
        Accept(e, Cmd(e, "purchase", id: id, sources: quote.Payments[0].SourceIds, discount: true));
        Equal(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "ohsuki-create"));
        Check(e.State.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-10023" && c.OwnerId == 0));
        Equal(e.State.PendingChoice?.Type, "entryReaction", "Window still open, same as any other entry Reaction");
    }
    {
        // Tokoru: the same shape, creating Tokoru's Bear (a distinct published token).
        var e = MantisDynasty("oracle-l5r-4474");
        var id = ProvinceCard(e); var quote = e.QuotePurchase(0, id);
        Check(quote.Error is null, quote.Error ?? "");
        Accept(e, Cmd(e, "purchase", id: id, sources: quote.Payments[0].SourceIds));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "tokoru-create"));
        var bear = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10025");
        Equal(bear.OwnerId, 0); Equal(bear.Zone, Zone.Home);
        Check(!e.State.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-10023"), "Tokoru creates only her own Bear, never Ohsuki's Sparrow");
        var def = CardCatalog.Get("oracle-l5r-10025");
        Equal(def.Force, 4); Equal(def.Chi, 2); Equal(def.PersonalHonor, 3);
        Check(def.Keywords!.Contains("Bear") && def.Keywords.Contains("Mantis Clan") && def.Keywords.Contains("Nonhuman") && def.Keywords.Contains("Spirit"));
        var sparrowDef = CardCatalog.Get("oracle-l5r-10023");
        Equal(sparrowDef.Force, 0); Equal(sparrowDef.Chi, 2); Equal(sparrowDef.PersonalHonor, 3);
        Check(sparrowDef.Keywords!.Contains("Sparrow") && sparrowDef.Keywords.Contains("Cavalry") && sparrowDef.Keywords.Contains("Spirit"));
    }
    {
        // A created token that dies is removed from the game, never left in a discard pile -- the
        // existing CreatedOnly redirect (issue #257) applies to these two tokens unchanged.
        var e = MantisDynasty("oracle-l5r-4474");
        var id = ProvinceCard(e); var quote = e.QuotePurchase(0, id);
        Accept(e, Cmd(e, "purchase", id: id, sources: quote.Payments[0].SourceIds));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "tokoru-create"));
        var bear = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10025");
        var s = e.State; s.Cards[bear.Id].Zone = Zone.Battlefield; s.Cards[bear.Id].ProvinceIndex = 0; s.PendingChoice = null; s.Phase = Phase.Battle;
        s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = 1; s.PriorityPlayerId = 1;
        var chiyo = Add(s, "oracle-l5r-10290", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, new GameCommand("useAbility", 1, e2.Version, CardId: chiyo.Id, TargetId: bear.Id, AbilityId: "chiyo-attack")); // Force 4 <= Melee 4
        Equal(e2.State.Cards[bear.Id].Zone, Zone.RemovedFromGame, "Removed from the game rather than discarded");
    }

    // ------------------------------------------------------------------------------------------
    // Ohsuki's Sparrow: Limited -- "If none of your provinces were destroyed since your last turn ended".
    // ------------------------------------------------------------------------------------------
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var sparrow = Add(s, "oracle-l5r-10023", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sparrow.Id, AbilityId: "ohsuki-sparrow-honor"));
        Equal(e.State.Players[0].Honor, honorBefore + 3, "Gain 3 Honor");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: sparrow.Id, AbilityId: "ohsuki-sparrow-honor")); // Limited: once per turn
    }
    {
        // A destroyed province since the last turn ended: the action cannot even be used.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var sparrow = Add(s, "oracle-l5r-10023", 0, Zone.Home);
        s.ProvinceDestroyedSinceTurnEnd = [true, false];
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: sparrow.Id, AbilityId: "ohsuki-sparrow-honor"));
        // ...but only the CONTROLLER's own flag matters, never the opponent's.
        s.ProvinceDestroyedSinceTurnEnd = [false, true];
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: sparrow.Id, AbilityId: "ohsuki-sparrow-honor"));
    }
    {
        // The flag's own windowing, in isolation: each player's flag clears at the end of THAT player's turn only.
        var s = MatchEngine.CreateEconomyScenario().State; s.ProvinceDestroyedSinceTurnEnd = [true, true];
        var e = MatchEngine.FromTestState(s);
        EndTurnFully(e);
        Equal(e.State.ActivePlayerId, 1);
        Check(!e.State.ProvinceDestroyedSinceTurnEnd[0] && e.State.ProvinceDestroyedSinceTurnEnd[1], "Player 0's turn ended: only player 0's flag cleared");
        EndTurnFully(e);
        Equal(e.State.ActivePlayerId, 0);
        Check(!e.State.ProvinceDestroyedSinceTurnEnd[0] && !e.State.ProvinceDestroyedSinceTurnEnd[1], "Player 1's turn ended: now theirs cleared too");
    }
    {
        // End to end through a REAL province destruction. Player 1 attacks and destroys player 0's
        // province on player 1's own turn; the flag reads true for player 0 (the province owner only),
        // survives the end of player 1's turn, still blocks the Sparrow on player 0's very next turn,
        // and clears only once player 0's own turn ends -- so the turn after that it is usable again.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1;
        s.Players[0].Provinces[0].Strength = 1; s.Players[0].Provinces[0].PrintedStrength = 1;
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6, unopposed
        var sparrow = Add(s, "oracle-l5r-10023", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.ProvinceDestroyedSinceTurnEnd[0] && !e.State.ProvinceDestroyedSinceTurnEnd[1]);
        Resolve(e);
        Check(e.State.Players[0].Provinces[0].Destroyed, "Test setup: the province was actually destroyed");
        Check(e.State.ProvinceDestroyedSinceTurnEnd[0], "The province's owner (player 0) is flagged");
        Check(!e.State.ProvinceDestroyedSinceTurnEnd[1], "The attacker is not");
        EndTurnFully(e); // player 1's turn ends
        Equal(e.State.ActivePlayerId, 0);
        Check(e.State.ProvinceDestroyedSinceTurnEnd[0], "Player 1's turn ending does NOT clear player 0's flag");
        // Player 0's Action phase: the Sparrow may not be used.
        while (e.State.Phase != Phase.Action) Accept(e, Cmd(e, "pass"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: sparrow.Id, AbilityId: "ohsuki-sparrow-honor"));
        EndTurnFully(e); // player 0's turn ends: their window restarts
        Check(!e.State.ProvinceDestroyedSinceTurnEnd[0], "Player 0's own turn ending clears it");
        EndTurnFully(e); // player 1's next turn ends
        while (e.State.Phase != Phase.Action) Accept(e, Cmd(e, "pass"));
        Equal(e.State.ActivePlayerId, 0);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: sparrow.Id, AbilityId: "ohsuki-sparrow-honor"));
        Equal(e.State.Players[0].Honor, honorBefore + 3, "Usable again: nothing destroyed since player 0's last turn ended");
    }

    // ------------------------------------------------------------------------------------------
    // Tokoru's Bear: "Target an attacking enemy card: Bow it. Destroy it if it is an attachment."
    // ------------------------------------------------------------------------------------------
    static (MatchState State, CardInstance Bear, CardInstance AttackerHost) BearBattle()
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 1; s.PriorityPlayerId = 0; // player 0 DEFENDS
        var bear = Add(s, "oracle-l5r-10025", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return (s, bear, attacker);
    }
    {
        var (s, bear, attacker) = BearBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bear.Id, TargetId: attacker.Id, AbilityId: "tokoru-bear-bow"));
        Check(e.State.Cards[attacker.Id].Bowed, "An attacking Personality is bowed");
        Equal(e.State.Cards[attacker.Id].Zone, Zone.Battlefield, "...and not destroyed: only an attachment is");
    }
    {
        var (s, bear, attacker) = BearBattle();
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: attacker.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bear.Id, TargetId: item.Id, AbilityId: "tokoru-bear-bow"));
        Check(e.State.Cards[item.Id].Zone != Zone.Attached, "An attacking unit's Item is destroyed");
        Check(!e.State.Cards[attacker.Id].Bowed, "Its host Personality is untouched");
    }
    {
        var (s, bear, attacker) = BearBattle();
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: attacker.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: bear.Id, TargetId: follower.Id, AbilityId: "tokoru-bear-bow"));
        Check(e.State.Cards[follower.Id].Zone != Zone.Attached, "An attacking unit's Follower is destroyed too -- Followers are attachments here");
    }
    {
        // Own cards are never "enemy"; a DEFENDING enemy card is never "attacking".
        var (s, bear, attacker) = BearBattle();
        var ownFriend = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: bear.Id, TargetId: ownFriend.Id, AbilityId: "tokoru-bear-bow"));
        var s2 = StrongholdBattle("oracle-l5r-1218"); s2.ActivePlayerId = 0; // the Bear's controller now ATTACKS
        var bear2 = Add(s2, "oracle-l5r-10025", 0, Zone.Battlefield, province: 0);
        var defender = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, CardId: bear2.Id, TargetId: defender.Id, AbilityId: "tokoru-bear-bow"));
    }
    {
        // A card that is not at the current battlefield at all is out of reach too, even if its
        // controller is the active player.
        var (s, bear, _) = BearBattle();
        var homeAttacker = Add(s, "training-lion-courtier", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: bear.Id, TargetId: homeAttacker.Id, AbilityId: "tokoru-bear-bow"));
    }
}

// =====================================================================================================
// Personality wave 22 / region-wave-3 (issue #301): Batch F -- the final batch -- of the "Not blocked;
// deferred for scope" cards from build_card_inventory.py's own BLOCKERS audit.
// =====================================================================================================
static GameCommand UseF(MatchEngine e, int player, string ability, string? card = null, string? target = null, string? performer = null, string[]? sources = null, int? province = null)
    => new("useAbility", player, e.Version, CardId: card, SourceIds: sources, Province: province, TargetId: target, AbilityId: ability, PerformerId: performer);
// An Action-phase table (the economy scenario, which defaults to the Dynasty phase): player 0 active and holding priority.
static MatchState ActionTable(int priority = 0)
{
    var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = priority; s.ActivePlayerId = 0;
    return s;
}
// Every enabled command an action list offers for one ability id really validates against a fresh copy of
// the same position -- the AI/client contract QueryActions makes (RegressionAssertions.OfferedCommandsValidate's
// own shape, but scoped to one ability and returning the offered commands for further assertions).
static GameCommand[] OfferedFor(MatchEngine e, int player, string abilityId)
{
    var offered = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == abilityId).Select(a => a.Command!).ToArray();
    foreach (var command in offered) Accept(MatchEngine.FromTestState(e.State), command);
    return offered;
}

static void BatchFPublishedShape()
{
    Equal(PublishedContent.PersonalityWave22.Cards.Length, 12, "Twelve Personalities");
    Equal(PublishedContent.RegionWave3.Cards.Length, 1, "Shinden Shorai");
    Check(!CardCatalog.Get("oracle-l5r-6857").ScenarioOnly && CardCatalog.Get("oracle-l5r-6857").Type == CardType.Region);
    // Hawado: the scan prints "Scorpion Clan * Ninja"; the catalogue's first-listed "clan" is Ninja, which is a
    // keyword here -- Clan drives Proclaim/Clan discount/Loyal, so a Scorpion player must see her as in-clan.
    Equal(CardCatalog.Get("oracle-l5r-7103").Clan, "Scorpion");
    Check(CardCatalog.Get("oracle-l5r-7103").Keywords!.Contains("Ninja") && CardCatalog.Get("oracle-l5r-7103").Keywords!.Contains("Scorpion Clan"));
    // Kodo's published text follows the scan, not the stale catalogue paraphrase.
    Equal(CardCatalog.Get("oracle-l5r-142").Text, "Political Open: Target a Holding: Straighten it.");
    // Mitsuko is the Experienced printing under the plain catalogue title, like every other published Experienced card.
    Equal(CardCatalog.Get("oracle-l5r-3866").Name, "Isawa Mitsuko");
    Check(Experienced.Level(CardCatalog.Get("oracle-l5r-3866").Keywords) == 1);
    Check(CardCatalog.Get("oracle-l5r-576").HonorRequirement is null && CardCatalog.Get("oracle-l5r-857").HonorRequirement == 1, "Rikate prints '-', Nori prints 1");
    // Two-halves cards share one once-per-turn use; every other id has its own.
    Equal(AbilityCatalog.All["nori-bow-personality"].UsageKey, AbilityCatalog.All["nori-bow-attachments"].UsageKey);
    Equal(AbilityCatalog.All["hawado-move-home"].UsageKey, AbilityCatalog.All["hawado-move-battlefield"].UsageKey);
    Check(AbilityCatalog.All["munefusa-straighten"].UsageKey is null && AbilityCatalog.All["munefusa-move"].UsageKey is null);
}

static void BatchFStraightenAndHoldingTargets()
{
    // ---- Agasha Kodo: "Political Open: Target a Holding: Straighten it." -- either side's Holding, Open timing.
    {
        var s = ActionTable();
        var kodo = Add(s, "oracle-l5r-142", 0, Zone.Home);
        var own = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var enemy = Add(s, "training-farm", 1, Zone.Home, bowed: true);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "kodo-straighten", kodo.Id, samurai.Id)); // a Personality is not a Holding
        Accept(e, UseF(e, 0, "kodo-straighten", kodo.Id, enemy.Id));
        Check(!e.State.Cards[enemy.Id].Bowed, "An ENEMY Holding is a legal target (no ownership qualifier is printed)");
        Check(e.State.Cards[own.Id].Bowed && e.State.Cards[samurai.Id].Bowed, "Nothing else straightened");
        Accept(e, Cmd(e, "pass", player: 1));
        Reject(e, UseF(e, 0, "kodo-straighten", kodo.Id, own.Id)); // once per turn
    }
    {
        // An Open action: the NON-active player uses it too, on their own opportunity.
        var s = ActionTable(priority: 1);
        var kodo = Add(s, "oracle-l5r-142", 1, Zone.Home);
        var holding = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 1, "kodo-straighten", kodo.Id, holding.Id));
        Check(!e.State.Cards[holding.Id].Bowed);
        Check(OfferedFor(MatchEngine.FromTestState(KodoOfferTable()), 0, "kodo-straighten").Length == 1, "Offered through QueryActions, and the offered command validates");
    }
    {
        // Not a Battle action: the Battle phase rejects it.
        var s = StrongholdBattle("oracle-l5r-1218");
        var kodo = Add(s, "oracle-l5r-142", 0, Zone.Battlefield, province: 0);
        var holding = Add(s, "training-farm", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "kodo-straighten", kodo.Id, holding.Id));
    }

    // ---- Asahina Munefusa's Open half: "Target your Temple Holding" -- own-holding-with-keyword.
    {
        var s = ActionTable();
        var mune = Add(s, "oracle-l5r-505", 0, Zone.Home);
        var temple = Add(s, "training-temple-holding", 0, Zone.Home, bowed: true);
        var enemyTemple = Add(s, "training-temple-holding", 1, Zone.Home, bowed: true);
        var farm = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "munefusa-straighten", mune.Id, enemyTemple.Id)); // "your" Temple only
        Reject(e, UseF(e, 0, "munefusa-straighten", mune.Id, farm.Id)); // own, but not a Temple
        Reject(e, UseF(e, 0, "munefusa-straighten", mune.Id, samurai.Id)); // not a Holding at all
        Accept(e, UseF(e, 0, "munefusa-straighten", mune.Id, temple.Id));
        Check(!e.State.Cards[temple.Id].Bowed && e.State.Cards[farm.Id].Bowed && e.State.Cards[enemyTemple.Id].Bowed);
    }

    // ---- Isawa Mitsuko's Open half: "Target an Air Shugenja or a Holding: Straighten it."
    {
        var s = ActionTable();
        var mitsuko = Add(s, "oracle-l5r-3866", 0, Zone.Home);
        var airShugenja = Add(s, "oracle-l5r-505", 1, Zone.Home, bowed: true); // Munefusa: Air + Shugenja, an ENEMY -- no ownership qualifier
        var waterShugenja = Add(s, "oracle-l5r-3850", 0, Zone.Home, bowed: true); // Kojiro: Shugenja but Water
        var airNonShugenja = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        airNonShugenja.GrantedKeywords = [new("Air", "test-fixture", "Permanent")];
        var plain = Add(s, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var holding = Add(s, "training-farm", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "mitsuko-straighten", mitsuko.Id, waterShugenja.Id)); // Shugenja alone is not enough
        Reject(e, UseF(e, 0, "mitsuko-straighten", mitsuko.Id, airNonShugenja.Id)); // Air alone is not enough (the existing "air" Target's own half)
        Reject(e, UseF(e, 0, "mitsuko-straighten", mitsuko.Id, plain.Id));
        Accept(e, UseF(e, 0, "mitsuko-straighten", mitsuko.Id, airShugenja.Id));
        Check(!e.State.Cards[airShugenja.Id].Bowed);
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, UseF(e2, 0, "mitsuko-straighten", mitsuko.Id, holding.Id));
        Check(!e2.State.Cards[holding.Id].Bowed, "The Holding half of the union");
    }
    {
        // A straightened Personality is a real target for Tamori Tamoko's straighten-negation window (the
        // curated MoveOrStraightenKind list now names mitsuko-straighten); a Holding never reaches it.
        var s = ActionTable();
        var mitsuko = Add(s, "oracle-l5r-3866", 0, Zone.Home);
        var airShugenja = Add(s, "oracle-l5r-505", 1, Zone.Home, bowed: true);
        var tamoko = Add(s, "training-tamoko", 1, Zone.Home);
        var holding = Add(s, "training-farm", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "mitsuko-straighten", mitsuko.Id, airShugenja.Id));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        ExpectBeforeActionOffer(e, tamoko.Id);
        Accept(e, UseF(e, 1, "tamoko-negate", tamoko.Id));
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        Check(e.State.Cards[airShugenja.Id].Bowed, "Negated: the straightening never applied");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, UseF(e2, 0, "mitsuko-straighten", mitsuko.Id, holding.Id));
        Check(e2.State.PendingChoice is null, "A Holding target opens no negation window");
        Check(!e2.State.Cards[holding.Id].Bowed);
    }
}
// A one-off Action table for OfferedFor (its own Kodo, an enemy bowed Holding to target).
static MatchState KodoOfferTable()
{
    var s = ActionTable();
    Add(s, "oracle-l5r-142", 0, Zone.Home);
    Add(s, "training-farm", 1, Zone.Home, bowed: true);
    return s;
}

static void BatchFBattleBowAndDestroy()
{
    // ---- Bayushi Nori: "Bow the Personality or all of his attachments. If he is dishonorable, his controller loses 2 Honor."
    static (MatchState S, CardInstance Nori, CardInstance Target) NoriBattle(bool dishonorable = false)
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var nori = Add(s, "oracle-l5r-857", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); target.Dishonorable = dishonorable;
        return (s, nori, target);
    }
    foreach (var dishonorable in new[] { false, true })
    {
        var (s, nori, target) = NoriBattle(dishonorable);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Accept(e, UseF(e, 0, "nori-bow-personality", nori.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "The Personality is bowed");
        Equal(e.State.Players[1].Honor, honor - (dishonorable ? 2 : 0), dishonorable ? "Dishonorable: his controller loses 2 Honor" : "Honorable: no Honor loss");
    }
    foreach (var dishonorable in new[] { false, true })
    {
        var (s, nori, target) = NoriBattle(dishonorable);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Accept(e, UseF(e, 0, "nori-bow-attachments", nori.Id, target.Id));
        Check(e.State.Cards[follower.Id].Bowed && e.State.Cards[item.Id].Bowed, "EVERY attachment in his unit is bowed");
        Check(!e.State.Cards[target.Id].Bowed, "...but not the Personality himself (that is the OTHER half of the choice)");
        Equal(e.State.Players[1].Honor, honor - (dishonorable ? 2 : 0));
    }
    {
        // "All of his attachments" with none: legal, does nothing to the Personality, and the Honor clause still applies.
        var (s, nori, target) = NoriBattle(dishonorable: true);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Accept(e, UseF(e, 0, "nori-bow-attachments", nori.Id, target.Id));
        Check(!e.State.Cards[target.Id].Bowed);
        Equal(e.State.Players[1].Honor, honor - 2);
    }
    {
        // One shared once-per-turn use: the other half is rejected after either is used (Horibe's shape).
        var (s, nori, target) = NoriBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "nori-bow-personality", nori.Id, target.Id));
        Accept(e, Cmd(e, "pass", player: 1));
        Reject(e, UseF(e, 0, "nori-bow-attachments", nori.Id, target.Id));
        Reject(e, UseF(e, 0, "nori-bow-personality", nori.Id, target.Id));
    }
    {
        // Enemy Personalities only; and Nori must be at the battlefield (no exemption here).
        var (s, nori, target) = NoriBattle();
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "oracle-l5r-857", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "nori-bow-personality", nori.Id, own.Id));
        Reject(e, UseF(e, 0, "nori-bow-personality", home.Id, target.Id));
    }

    // ---- Hida Mimori: "Target a bowed enemy card: Destroy it."
    static (MatchState S, CardInstance Mimori) MimoriBattle()
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        return (s, Add(s, "oracle-l5r-3150", 0, Zone.Battlefield, province: 0));
    }
    {
        // A bowed enemy Personality: destroyed, and his attachments with him.
        var (s, mimori) = MimoriBattle();
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, true, 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "mimori-destroy", mimori.Id, target.Id));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead);
        Equal(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "His attachments are destroyed with him");
    }
    {
        // A bowed enemy ATTACHMENT (Follower or Item): destroyed alone; its unbowed host is untouched.
        foreach (var def in new[] { "training-follower", "training-item" })
        {
            var (s, mimori) = MimoriBattle();
            var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, false, 0);
            var attachment = Add(s, def, 1, Zone.Attached, true, 0, host.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, UseF(e, 0, "mimori-destroy", mimori.Id, attachment.Id));
            Equal(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard, $"{def}: destroyed as an attachment");
            Equal(e.State.Cards[host.Id].Zone, Zone.Battlefield, "The host is unharmed");
        }
    }
    {
        // Every illegal shape: an unbowed enemy, a bowed OWN card, a bowed enemy away from the battlefield,
        // and a bowed enemy attachment whose host is not at the battlefield.
        var (s, mimori) = MimoriBattle();
        var unbowed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, false, 0);
        var unbowedFollower = Add(s, "training-follower", 1, Zone.Attached, false, 0, unbowed.Id);
        var ownBowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, true, 0);
        var homeBowed = Add(s, "training-lion-courtier", 1, Zone.Home, true);
        var homeFollower = Add(s, "training-follower", 1, Zone.Attached, true, null, homeBowed.Id);
        var e = MatchEngine.FromTestState(s);
        foreach (var id in new[] { unbowed.Id, unbowedFollower.Id, ownBowed.Id, homeBowed.Id, homeFollower.Id })
            Reject(e, UseF(e, 0, "mimori-destroy", mimori.Id, id));
    }
    {
        // Rokai's destruction-negation window applies to a Personality target of Mimori (curated DestroysTarget list).
        var (s, mimori) = MimoriBattle();
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, true, 0);
        var rokai = Add(s, "training-rokai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "mimori-destroy", mimori.Id, target.Id));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction");
        Accept(e, UseF(e, 1, "rokai-negate-destruction", rokai.Id));
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Negated");
        Equal(e.State.Cards[rokai.Id].Zone, Zone.Dead, "Rokai paid his own cost");
    }

    // ---- Isawa Mitsuko's Battle half: "Bow him. Destroy him if he is Kolat, Ninja, or Shadowlands."
    static void MitsukoBattle(string targetDef, Action<CardInstance>? tweak, bool expectDestroyed, string label)
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mitsuko = Add(s, "oracle-l5r-3866", 0, Zone.Battlefield, province: 0);
        var target = Add(s, targetDef, 1, Zone.Battlefield, province: 0);
        tweak?.Invoke(target);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "mitsuko-bow-destroy", mitsuko.Id, target.Id));
        if (expectDestroyed) Equal(e.State.Cards[target.Id].Zone, Zone.Dead, $"{label}: destroyed");
        else { Equal(e.State.Cards[target.Id].Zone, Zone.Battlefield, $"{label}: survives"); Check(e.State.Cards[target.Id].Bowed, $"{label}: bowed"); }
    }
    MitsukoBattle("training-lion-samurai", null, false, "An ordinary Personality is only bowed");
    MitsukoBattle("training-shadowlands-personality", null, true, "Shadowlands by Clan alignment");
    MitsukoBattle("training-ninja-source", null, true, "Ninja keyword");
    MitsukoBattle("oracle-l5r-576", null, true, "Asako Rikate: 'Phoenix Clan' + the printed Shadowlands KEYWORD, no Shadowlands Clan alignment");
    MitsukoBattle("training-lion-samurai", t => t.GrantedKeywords = [new("Kolat", "test-fixture", "Permanent")], true, "Kolat keyword");
    // Stalwart negates only the bowing -- a separate clause still destroys him.
    MitsukoBattle("training-stalwart-personality", t => t.GrantedKeywords = [new("Kolat", "test-fixture", "Permanent")], true, "Stalwart Kolat");
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var mitsuko = Add(s, "oracle-l5r-3866", 0, Zone.Battlefield, province: 0);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "mitsuko-bow-destroy", mitsuko.Id, own.Id)); // enemy only
    }

    // ---- Sutigu: "Bow Sutigu or one of his Weapons, and target an enemy card: Negate its straightening (until the turn ends) if it is an attachment. Bow it."
    static (MatchState S, CardInstance Sutigu, CardInstance Weapon, CardInstance Host, CardInstance Follower) SutiguBattle()
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var sutigu = Add(s, "oracle-l5r-7676", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon", 0, Zone.Attached, province: 0, attachedTo: sutigu.Id);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: host.Id);
        return (s, sutigu, weapon, host, follower);
    }
    {
        // Sutigu himself pays: an enemy attachment is bowed AND cannot straighten this turn.
        var (s, sutigu, weapon, host, follower) = SutiguBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "sutigu-bow", sutigu.Id, follower.Id, performer: sutigu.Id));
        Check(e.State.Cards[sutigu.Id].Bowed && !e.State.Cards[weapon.Id].Bowed, "Sutigu bowed as the cost; his Weapon untouched");
        Check(e.State.Cards[follower.Id].Bowed, "Bow it");
        Equal(e.State.Cards[follower.Id].StraightenBlockedTurn, e.State.TurnNumber, "Negate its straightening -- it is an attachment");
    }
    {
        // His Weapon pays instead: Sutigu stays unbowed; an enemy PERSONALITY is bowed but its straightening is NOT negated.
        var (s, sutigu, weapon, host, follower) = SutiguBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "sutigu-bow", sutigu.Id, host.Id, performer: weapon.Id));
        Check(e.State.Cards[weapon.Id].Bowed && !e.State.Cards[sutigu.Id].Bowed, "The Weapon bowed as the cost instead");
        Check(e.State.Cards[host.Id].Bowed, "Bow it");
        Equal(e.State.Cards[host.Id].StraightenBlockedTurn, -1, "Only an ATTACHMENT has its straightening negated");
    }
    {
        // A Stalwart Personality target: its first enemy bow is negated (Personality targets are not attachments,
        // so there is no straightening negation to keep).
        var (s, sutigu, weapon, host, follower) = SutiguBattle();
        var stalwart = Add(s, "training-stalwart-personality", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "sutigu-bow", sutigu.Id, stalwart.Id, performer: sutigu.Id));
        Check(!e.State.Cards[stalwart.Id].Bowed, "Stalwart negated the bow");
    }
    {
        // The performer must be Sutigu or an UNBOWED WEAPON attached to Sutigu himself.
        var (s, sutigu, weapon, host, follower) = SutiguBattle();
        var nonWeapon = Add(s, "training-item", 0, Zone.Attached, province: 0, attachedTo: sutigu.Id);
        var bowedWeapon = Add(s, "training-weapon", 0, Zone.Attached, true, 0, sutigu.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var othersWeapon = Add(s, "training-weapon", 0, Zone.Attached, province: 0, attachedTo: other.Id);
        var e = MatchEngine.FromTestState(s);
        foreach (var bad in new[] { nonWeapon.Id, bowedWeapon.Id, other.Id, othersWeapon.Id })
            Reject(e, UseF(e, 0, "sutigu-bow", sutigu.Id, follower.Id, performer: bad));
        Reject(e, UseF(e, 0, "sutigu-bow", sutigu.Id, follower.Id)); // a performer is required
        // Targets: enemy cards at the battlefield only.
        var own = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var away = Add(s, "training-lion-courtier", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Reject(e2, UseF(e2, 0, "sutigu-bow", sutigu.Id, own.Id, performer: sutigu.Id));
        Reject(e2, UseF(e2, 0, "sutigu-bow", sutigu.Id, away.Id, performer: sutigu.Id));
    }
    {
        // QueryActions offers BOTH cost choices, each a command that validates.
        var (s, sutigu, weapon, host, follower) = SutiguBattle();
        var e = MatchEngine.FromTestState(s);
        var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "sutigu-bow").Select(a => a.Command!.PerformerId).ToArray();
        Check(offered.Contains(sutigu.Id) && offered.Contains(weapon.Id), "Sutigu and his Weapon are both offered as the performer");
        OfferedFor(e, 0, "sutigu-bow");
    }
}

static void BatchFMovementAndChoices()
{
    // ---- Asahina Munefusa's Battle half: "Even if Munefusa is not at the current battlefield, target an
    // attacking Personality: Move him home. If he moved, gain 1 Honor."
    static (MatchState S, CardInstance Mune, CardInstance Attacker) MuneBattle(bool muneAtHome = true)
    {
        // Player 0 ATTACKS the (player 1) province; player 1 -- Munefusa's controller -- has NO unit at the
        // battlefield at all, and holds priority.
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0; s.PriorityPlayerId = 1;
        var mune = Add(s, "oracle-l5r-505", 1, muneAtHome ? Zone.Home : Zone.Battlefield, province: muneAtHome ? null : 0);
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        return (s, mune, attacker);
    }
    {
        var (s, mune, attacker) = MuneBattle();
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Accept(e, UseF(e, 1, "munefusa-move", mune.Id, attacker.Id));
        Equal(e.State.Cards[attacker.Id].Zone, Zone.Home, "Moved home from a Munefusa who was not even at the battlefield, with no presence of her controller's there");
        Equal(e.State.Players[1].Honor, honor + 1, "If he moved, gain 1 Honor");
        Equal(e.State.Cards[mune.Id].Zone, Zone.Home, "Munefusa herself stays where she was");
    }
    {
        // A negated move (Tamori Tamoko's window) is not "moved": no Honor.
        var (s, mune, attacker) = MuneBattle();
        var tamoko = Add(s, "training-tamoko", 0, Zone.Home); // the attacker's controller's own Tamoko
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Accept(e, UseF(e, 1, "munefusa-move", mune.Id, attacker.Id));
        Equal(e.State.PendingChoice?.Type, "beforeActionReaction", "The move is offered for negation (curated MoveOrStraightenKind)");
        Accept(e, UseF(e, 0, "tamoko-negate", tamoko.Id));
        Accept(e, Cmd(e, "declineReaction")); Accept(e, Cmd(e, "declineReaction"));
        Equal(e.State.Cards[attacker.Id].Zone, Zone.Battlefield, "Negated");
        Equal(e.State.Players[1].Honor, honor, "He did not move: no Honor");
    }
    {
        // Only an ATTACKING Personality, at the battlefield; the exemption is from PRESENCE, never from being unbowed.
        var (s, mune, attacker) = MuneBattle();
        var defender = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Munefusa's own controller's defender
        var attackerAtHome = Add(s, "training-crane-veteran", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 1, "munefusa-move", mune.Id, defender.Id));
        Reject(e, UseF(e, 1, "munefusa-move", mune.Id, attackerAtHome.Id));
        var (s2, mune2, attacker2) = MuneBattle();
        s2.Cards[mune2.Id].Bowed = true;
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, UseF(e2, 1, "munefusa-move", mune2.Id, attacker2.Id));
    }
    {
        // She may also be the one at the battlefield (a defending Munefusa); and once per turn.
        var (s, mune, attacker) = MuneBattle(muneAtHome: false);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 1, "munefusa-move", mune.Id, attacker.Id));
        Equal(e.State.Cards[attacker.Id].Zone, Zone.Home);
        Accept(e, Cmd(e, "pass", player: 0));
        Reject(e, UseF(e, 1, "munefusa-move", mune.Id, attacker.Id));
    }

    // ---- Shosuro Hawado.
    static (MatchState S, CardInstance Hawado, CardInstance Discarded) HawadoBattle(bool bowed = true)
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        s.UnresolvedBattlefields = [0, 1];
        var hawado = Add(s, "oracle-l5r-7103", 0, Zone.Battlefield, bowed, 0);
        var discarded = Add(s, "training-lion-samurai", 1, Zone.DynastyDiscard);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // an enemy unit at the current battlefield
        return (s, hawado, discarded);
    }
    {
        // Home: the cost is removed from the game, she moves home, and -- because she moved -- she straightens (she was bowed).
        var (s, hawado, discarded) = HawadoBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "hawado-move-home", hawado.Id, discarded.Id));
        Equal(e.State.Cards[discarded.Id].Zone, Zone.RemovedFromGame, "The Dynasty card is removed from the game");
        Equal(e.State.Cards[hawado.Id].Zone, Zone.Home);
        Check(!e.State.Cards[hawado.Id].Bowed, "Straightened because she moved (usable even though she was bowed)");
    }
    {
        // Any Dynasty card in ANY discard pile: a dead Personality, a Holding, either player's -- not a Fate card, not one in play.
        foreach (var (def, zone, owner, legal) in new[]{("training-lion-samurai",Zone.Dead,0,true),("training-lion-samurai",Zone.Dead,1,true),("training-marketplace",Zone.DynastyDiscard,0,true),
            ("training-item",Zone.FateDiscard,1,false),("training-marketplace",Zone.Home,1,false),("training-lion-samurai",Zone.Hand,1,false),("training-lion-courtier",Zone.RemovedFromGame,1,false)})
        {
            var (s, hawado, _) = HawadoBattle();
            var card = Add(s, def, owner, zone);
            var e = MatchEngine.FromTestState(s);
            var command = UseF(e, 0, "hawado-move-home", hawado.Id, card.Id);
            if (legal) { Accept(e, command); Equal(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, $"{def} in {zone}"); }
            else Reject(e, command);
        }
    }
    {
        // Battlefield half: names another unresolved battlefield with enemy units in command.Province.
        var (s, hawado, discarded) = HawadoBattle();
        Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, discarded.Id)); // a destination is required
        Reject(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, discarded.Id, province: 2)); // not a battlefield this Battle phase
        Reject(e, UseF(e, 0, "hawado-move-home", hawado.Id, discarded.Id, province: 1)); // "home" names no battlefield
        Accept(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, discarded.Id, province: 1));
        Equal(e.State.Cards[hawado.Id].Zone, Zone.Battlefield);
        Equal(e.State.Cards[hawado.Id].ProvinceIndex, 1);
        Check(!e.State.Cards[hawado.Id].Bowed, "She moved: straightened");
        Equal(e.State.Cards[discarded.Id].Zone, Zone.RemovedFromGame);
    }
    {
        // A battlefield WITHOUT enemy units is not a legal destination.
        var (s, hawado, discarded) = HawadoBattle();
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, discarded.Id, province: 1));
    }
    {
        // Already at the destination (the current battlefield has enemy units): no move, so NOT straightened -- "if she moved".
        var (s, hawado, discarded) = HawadoBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, discarded.Id, province: 0));
        Equal(e.State.Cards[hawado.Id].ProvinceIndex, 0);
        Check(e.State.Cards[hawado.Id].Bowed, "She did not move: still bowed");
        Equal(e.State.Cards[discarded.Id].Zone, Zone.RemovedFromGame, "The cost was still paid");
    }
    {
        // She must be at the battlefield to act at all (no presence exemption, only the "even if bowed" one), and the two
        // halves share one once-per-turn use (she stays put and bowed after a no-move use, so only the shared use blocks this).
        var (s, hawado, discarded) = HawadoBattle();
        var second = Add(s, "training-marketplace", 0, Zone.DynastyDiscard);
        var awayHawado = Add(s, "oracle-l5r-7103", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "hawado-move-home", awayHawado.Id, discarded.Id));
        Accept(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, discarded.Id, province: 0));
        Accept(e, Cmd(e, "pass", player: 1));
        Reject(e, UseF(e, 0, "hawado-move-home", hawado.Id, second.Id));
        Reject(e, UseF(e, 0, "hawado-move-battlefield", hawado.Id, second.Id, province: 0));
    }
    {
        // Offered through QueryActions -- one enabled command per (target, legal destination); every one validates.
        var (s, hawado, discarded) = HawadoBattle();
        Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        var homeOffers = OfferedFor(e, 0, "hawado-move-home");
        Check(homeOffers.Length >= 1 && homeOffers.All(c => c.Province is null));
        // QueryActions lists ONE action per ability with its first legal command (targets/destinations are variations on it): the
        // enabled action must already name a destination -- a Province-less command could never validate -- and every legal
        // variation validates too.
        var battlefieldOffers = OfferedFor(e, 0, "hawado-move-battlefield");
        Check(battlefieldOffers.Length == 1 && battlefieldOffers[0].Province is not null, "The offered battlefield command names its destination");
        foreach (var destination in new[] { 0, 1 }) Accept(MatchEngine.FromTestState(e.State), battlefieldOffers[0] with { Province = destination });
    }

    // ---- Asako Rikate.
    static (MatchState S, CardInstance Rikate, CardInstance Target) RikateBattle(string targetDef = "training-lion-samurai")
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = 0;
        var rikate = Add(s, "oracle-l5r-576", 0, Zone.Battlefield, province: 0);
        var target = Add(s, targetDef, 1, Zone.Battlefield, province: 0);
        return (s, rikate, target);
    }
    {
        // The controller may return ANY subset: here only the Item -- then the Personality gets -2C.
        var (s, rikate, target) = RikateBattle();
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: target.Id);
        var item = Add(s, "training-item", 1, Zone.Attached, true, 0, target.Id);
        target.GrantedKeywords = [new("Cavalry", item.Id, "Attachment")];
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, target.Id));
        Equal(e.State.PendingChoice?.Type, "rikateDetach");
        Equal(e.State.PendingChoice?.PlayerId, 1, "His controller chooses");
        Equal(e.State.PriorityPlayerId, 1);
        Check(e.State.Cards[target.Id].Modifiers.All(m => m.Stat != "Chi"), "The -2C waits for the controller's choice");
        // Every offered answer is a real command; and the wrong player / a foreign card are rejected outright.
        var offered = e.QueryActions(1).Where(a => a.Type == "chooseRikateDetach").ToArray();
        Check(offered.Length >= 4, "Keep all, return all, and one option per attachment");
        foreach (var option in offered) Accept(MatchEngine.FromTestState(e.State), option.Command!);
        Reject(e, new GameCommand("chooseRikateDetach", 0, e.Version, CardIds: []));
        Reject(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: ["nonexistent"]));
        Reject(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: [item.Id, item.Id]));
        Reject(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: [rikate.Id]));
        Accept(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: [item.Id]));
        Equal(e.State.Cards[item.Id].Zone, Zone.Hand, "Into his hand");
        Check(e.State.Players[1].Hand.Contains(item.Id));
        Check(e.State.Cards[item.Id].AttachedTo is null && !e.State.Cards[item.Id].Bowed, "A fresh card again: detached, unbowed");
        Check(!e.State.Cards[target.Id].GrantedKeywords.Any(g => g.Source == item.Id), "Its Attachment-duration keyword grant ended with it");
        Equal(e.State.Cards[follower.Id].AttachedTo, target.Id, "The other attachment stays");
        Equal(e.State.Cards[target.Id].Modifiers.Single(m => m.Stat == "Chi").Amount, -2, "Give the Personality -2C");
        Check(e.State.PendingChoice is null);
    }
    {
        // Declining (keep everything) still gives -2C; "all" returns every attachment.
        foreach (var takeAll in new[] { false, true })
        {
            var (s, rikate, target) = RikateBattle();
            var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, attachedTo: target.Id);
            var item = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: target.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, target.Id));
            Accept(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: takeAll ? new[] { follower.Id, item.Id } : Array.Empty<string>()));
            Equal(e.State.Cards[follower.Id].Zone, takeAll ? Zone.Hand : Zone.Attached);
            Equal(e.State.Cards[item.Id].Zone, takeAll ? Zone.Hand : Zone.Attached);
            Equal(e.State.Cards[target.Id].Modifiers.Single(m => m.Stat == "Chi").Amount, -2);
            Equal(e.State.PriorityPlayerId, 1, "Priority passes on as after any other action");
        }
    }
    {
        // No attachments: nothing to choose, so no window at all -- the -2C applies immediately.
        var (s, rikate, target) = RikateBattle();
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, target.Id));
        Check(e.State.PendingChoice is null, "No attachments, no choice");
        Equal(e.State.Cards[target.Id].Modifiers.Single(m => m.Stat == "Chi").Amount, -2);
    }
    {
        // The final Chi is the same whichever order the two halves run in: printed Chi 3 (lion samurai) with -1 = 2, plus a +1C Item
        // = 3. Keeping the Item: 3 - 2 = 1, he survives. Returning it: 2 - 2 = 0 -- destroyed.
        foreach (var returnItem in new[] { false, true })
        {
            var (s, rikate, target) = RikateBattle();
            target.Modifiers = [new("test-fixture", "test-fixture", "Chi", -1)];
            var item = Add(s, "training-item-force-effect", 1, Zone.Attached, province: 0, attachedTo: target.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, target.Id));
            Accept(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: returnItem ? new[] { item.Id } : Array.Empty<string>()));
            Equal(e.State.Cards[target.Id].Zone, returnItem ? Zone.Dead : Zone.Battlefield, returnItem ? "Chi reached 0 once the Item left" : "Chi 3 - 2 = 1: alive");
        }
    }
    {
        // ORDER matters where it is observable: a Personality who the -2C will destroy (Chi 2) loses his attachments with him -- unless the
        // controller returned them to hand FIRST, which is the whole point of offering the choice before the penalty applies.
        foreach (var returnItem in new[] { false, true })
        {
            var (s, rikate, target) = RikateBattle();
            target.Modifiers = [new("test-fixture", "test-fixture", "Chi", -1)]; // Chi 2: -2C is lethal
            var item = Add(s, "training-item", 1, Zone.Attached, province: 0, attachedTo: target.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, target.Id));
            Accept(e, new GameCommand("chooseRikateDetach", 1, e.Version, CardIds: returnItem ? new[] { item.Id } : Array.Empty<string>()));
            Equal(e.State.Cards[target.Id].Zone, Zone.Dead, "Either way the -2C destroys him");
            Equal(e.State.Cards[item.Id].Zone, returnItem ? Zone.Hand : Zone.FateDiscard, returnItem ? "Returned first: safe in his controller's hand" : "Kept: destroyed along with its host");
        }
    }
    {
        // A Personality already at Chi 2 is simply destroyed when there is no attachment to shuffle; enemy Personalities only.
        var (s, rikate, target) = RikateBattle();
        target.Modifiers = [new("test-fixture", "test-fixture", "Chi", -1)]; // Chi 2
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, own.Id));
        Accept(e, UseF(e, 0, "rikate-detach-chi", rikate.Id, target.Id));
        Equal(e.State.Cards[target.Id].Zone, Zone.Dead);
    }

    // ---- Shosuro Rin.
    static (MatchEngine E, CardInstance Rin, CardInstance Target) RinTable(string targetDef = "training-lion-samurai", bool dishonorable = false)
    {
        var s = ActionTable();
        var rin = Add(s, "oracle-l5r-7151", 0, Zone.Home);
        var target = Add(s, targetDef, 1, Zone.Home); target.Dishonorable = dishonorable;
        return (MatchEngine.FromTestState(s), rin, target);
    }
    {
        var (e, rin, target) = RinTable();
        Accept(e, UseF(e, 0, "rin-shame", rin.Id, target.Id));
        Check(e.State.Cards[rin.Id].Bowed, "Bow Rin: the printed cost");
        Equal(e.State.PendingChoice?.Type, "rinShame");
        Equal(e.State.PendingChoice?.PlayerId, 1, "The target's own controller chooses");
        Equal(e.State.PriorityPlayerId, 1);
        Check(!e.State.Cards[target.Id].Dishonorable && !e.State.Cards[target.Id].Bowed, "Nothing yet: it waits for the answer");
        Check(e.QueryActions(1).Count(a => a.Type == "chooseRinShame") == 2);
        foreach (var option in e.QueryActions(1).Where(a => a.Type == "chooseRinShame")) Accept(MatchEngine.FromTestState(e.State), option.Command!);
        Reject(e, new GameCommand("chooseRinShame", 0, e.Version, Choice: "dishonor")); // not Rin's controller's decision
        Reject(e, new GameCommand("chooseRinShame", 1, e.Version, Choice: "bow"));
        Accept(e, new GameCommand("chooseRinShame", 1, e.Version, Choice: "dishonor"));
        Check(e.State.Cards[target.Id].Dishonorable, "He was dishonored");
        Check(!e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].StraightenBlockedTurn == -1, "...and nothing else happens to him");
        Check(e.State.PendingChoice is null);
    }
    {
        // "If he did not choose this, bow him and negate his straightening (this turn)."
        var (e, rin, target) = RinTable();
        Accept(e, UseF(e, 0, "rin-shame", rin.Id, target.Id));
        Accept(e, new GameCommand("chooseRinShame", 1, e.Version, Choice: "decline"));
        Check(e.State.Cards[target.Id].Bowed && !e.State.Cards[target.Id].Dishonorable);
        Equal(e.State.Cards[target.Id].StraightenBlockedTurn, e.State.TurnNumber, "His straightening is negated this turn");
    }
    {
        // Stalwart negates the bowing (and only the bowing): the straightening negation still applies.
        var (e, rin, target) = RinTable("training-stalwart-personality");
        Accept(e, UseF(e, 0, "rin-shame", rin.Id, target.Id));
        Accept(e, new GameCommand("chooseRinShame", 1, e.Version, Choice: "decline"));
        Check(!e.State.Cards[target.Id].Bowed, "Stalwart negated the bow");
        Equal(e.State.Cards[target.Id].StraightenBlockedTurn, e.State.TurnNumber);
    }
    {
        // Honorable Personalities only -- either side's; a Dishonorable one, a Holding, and a bowed Rin are all rejected.
        var (e, rin, dishonorable) = RinTable(dishonorable: true);
        Reject(e, UseF(e, 0, "rin-shame", rin.Id, dishonorable.Id));
        var s = ActionTable();
        var rin2 = Add(s, "oracle-l5r-7151", 0, Zone.Home);
        var holding = Add(s, "training-farm", 1, Zone.Home);
        var ownHonorable = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Reject(e2, UseF(e2, 0, "rin-shame", rin2.Id, holding.Id));
        Accept(e2, UseF(e2, 0, "rin-shame", rin2.Id, ownHonorable.Id)); // her own controller may shame his own Personality; then HE answers
        Equal(e2.State.PendingChoice?.PlayerId, 0);
        var s3 = ActionTable();
        var bowedRin = Add(s3, "oracle-l5r-7151", 0, Zone.Home, bowed: true);
        var target3 = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Reject(e3, UseF(e3, 0, "rin-shame", bowedRin.Id, target3.Id));
    }
}

static void BatchFPassivesEquipAndRecruit()
{
    // ---- Isawa Kojiro: "Kojiro has Naval while he has a Spell." -- a LIVE conditional keyword.
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var kojiro = Add(s, "oracle-l5r-3850", 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(kojiro.Id, "Naval"), "No Spell: no Naval");
        Check(e.HasKeyword(kojiro.Id, "Cavalry"), "His own printed Cavalry is untouched");
        // A Spell attached to Kojiro: Naval.
        var s2 = e.State;
        var spell = Add(s2, "oracle-l5r-9344", 0, Zone.Attached, attachedTo: kojiro.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.HasKeyword(kojiro.Id, "Naval"), "A Spell attached: Naval");
        Check(!e2.HasKeyword(other.Id, "Naval"), "Only Kojiro");
        Check(!e2.GetView(0).Players[0].Home.Single(c => c.Id == kojiro.Id).GrantedKeywords!.Any(g => g.Keyword == "Naval"), "A live check, not a durable GrantedKeyword");
        // The Spell leaves: the Naval leaves with it, no cleanup needed.
        var s3 = e2.State; s3.Cards[spell.Id].Zone = Zone.FateDiscard; s3.Cards[spell.Id].AttachedTo = null;
        Check(!MatchEngine.FromTestState(s3).HasKeyword(kojiro.Id, "Naval"), "The Spell gone: Naval gone");
        // A Follower or Item is not a Spell.
        var s4 = e.State; Add(s4, "training-follower", 0, Zone.Attached, attachedTo: kojiro.Id); Add(s4, "training-item", 0, Zone.Attached, attachedTo: kojiro.Id);
        Check(!MatchEngine.FromTestState(s4).HasKeyword(kojiro.Id, "Naval"), "Only a SPELL counts");
        // A Spell on somebody else does not give Kojiro Naval; nor does Kojiro's Effect leak onto another card.
        var s5 = e.State; Add(s5, "oracle-l5r-9344", 0, Zone.Attached, attachedTo: other.Id);
        var e5 = MatchEngine.FromTestState(s5);
        Check(!e5.HasKeyword(kojiro.Id, "Naval") && !e5.HasKeyword(other.Id, "Naval"));
    }

    // ---- Yoritomo Minori: "+1F for each other Magistrate in her army."
    static (MatchState S, CardInstance Minori) MinoriBattle(int attacker = 0)
    {
        var s = StrongholdBattle("oracle-l5r-1218"); s.ActivePlayerId = attacker;
        return (s, Add(s, "oracle-l5r-9605", 0, Zone.Battlefield, province: 0)); // printed Force 4
    }
    static int MinoriForce(MatchState s, string id) => MatchEngine.FromTestState(s).GetView(0).Players[0].Army.Single(c => c.Id == id).Force;
    {
        var (s, minori) = MinoriBattle();
        Equal(MinoriForce(s, minori.Id), 4, "No other Magistrate: printed Force");
        Add(s, "oracle-l5r-857", 0, Zone.Battlefield, province: 0);   // Bayushi Nori: Magistrate
        Equal(MinoriForce(s, minori.Id), 5, "One other Magistrate: +1F");
        Add(s, "oracle-l5r-3866", 0, Zone.Battlefield, true, 0);     // Isawa Mitsuko: Magistrate, BOWED (no unbowed qualifier printed)
        Equal(MinoriForce(s, minori.Id), 6, "Two: +2F, a bowed one counting too");
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // not a Magistrate
        Equal(MinoriForce(s, minori.Id), 6);
        var granted = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        granted.GrantedKeywords = [new("Magistrate", "test-fixture", "Permanent")];
        Equal(MinoriForce(s, minori.Id), 7, "A GRANTED Magistrate keyword counts too (Keyword(), not just printed)");
    }
    {
        // Only Personalities in HER army: an enemy Magistrate at the same battlefield, one at Home, and one at ANOTHER battlefield
        // never count; a second Minori (who prints Magistrate herself) does.
        var (s, minori) = MinoriBattle();
        Add(s, "oracle-l5r-857", 1, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-857", 0, Zone.Home);
        Add(s, "oracle-l5r-857", 0, Zone.Battlefield, province: 1);
        Equal(MinoriForce(s, minori.Id), 4, "None of those is another Magistrate in her army");
        var minori2 = Add(s, "oracle-l5r-9605", 0, Zone.Battlefield, province: 0); // Minori herself prints Magistrate
        Equal(MinoriForce(s, minori.Id), 5, "A second Minori IS another Magistrate in the first one's army");
        Equal(MinoriForce(s, minori2.Id), 5, "...and vice versa: each excludes only herself");
    }
    {
        // Real resolution: the bonus counts toward the army's actual total, attacking or defending alike (no attacking gate).
        var (s, minori) = MinoriBattle(attacker: 0);
        Add(s, "oracle-l5r-857", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        Resolve(e); AssertBattleForces(e, (4 + 1) + 4, 6);
        var (s2, minori2) = MinoriBattle(attacker: 1); // Minori's controller DEFENDS
        Add(s2, "training-magistrate-personality", 0, Zone.Battlefield, province: 0); // Force 2, Magistrate
        Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Resolve(e2); AssertBattleForces(e2, 6, (4 + 1) + 2);
    }

    // ---- Soshi Neiru: "You may equip Air Spells to Neiru as an Open action."
    {
        var s = ActionTable();
        var neiru = Add(s, "oracle-l5r-7360", 0, Zone.Home);
        var air1 = Add(s, "oracle-l5r-9344", 0, Zone.Hand); // Winds of Dismissal: Air Spell, 2 Gold
        var air2 = Add(s, "oracle-l5r-9344", 0, Zone.Hand);
        var water = Add(s, "oracle-l5r-6502", 0, Zone.Hand); // Scouring Flood: a WATER Spell
        var item = Add(s, "training-item", 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var pay = e.PaymentOptions(0, 2).First().SourceIds;
        Reject(e, UseF(e, 0, "neiru-equip", neiru.Id, water.Id, sources: pay)); // not Air
        Reject(e, UseF(e, 0, "neiru-equip", neiru.Id, item.Id, sources: pay)); // not a Spell
        Reject(e, UseF(e, 0, "neiru-equip", neiru.Id, air1.Id)); // unpaid
        Accept(e, UseF(e, 0, "neiru-equip", neiru.Id, air1.Id, sources: pay));
        Equal(e.State.Cards[air1.Id].Zone, Zone.Attached);
        Equal(e.State.Cards[air1.Id].AttachedTo, neiru.Id, "Neiru is the fixed wielder");
        Check(!e.State.Players[0].Hand.Contains(air1.Id));
        Check(pay.All(id => e.State.Cards[id].Bowed), "The Spell's full Gold cost was paid");
        // No once-per-turn limit (equip-battle's own convention): a second Air Spell the same turn.
        Accept(e, Cmd(e, "pass", player: 1));
        var pay2 = e.PaymentOptions(0, 2).First().SourceIds;
        Accept(e, UseF(e, 0, "neiru-equip", neiru.Id, air2.Id, sources: pay2));
        Equal(e.State.Cards[air2.Id].AttachedTo, neiru.Id);
        // Only Neiru herself: another Personality's copy of the ability does not exist; only cards carrying it may use it.
        Check(OfferedFor(MatchEngine.FromTestState(NeiruOfferTable()), 0, "neiru-equip").Length == 1, "Offered through QueryActions with a payment that validates (and only for the Air Spell)");
    }
    {
        // An OPEN action: the non-active player equips on their own opportunity.
        var s = ActionTable(priority: 1);
        var neiru = Add(s, "oracle-l5r-7360", 1, Zone.Home);
        var air = Add(s, "oracle-l5r-9344", 1, Zone.Hand);
        Add(s, "training-marketplace", 1, Zone.Home); Add(s, "training-marketplace", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, UseF(e, 1, "neiru-equip", neiru.Id, air.Id, sources: e.PaymentOptions(1, 2).First().SourceIds));
        Equal(e.State.Cards[air.Id].AttachedTo, neiru.Id);
        // ...but never a Battle action.
        var bs = StrongholdBattle("oracle-l5r-1218");
        var neiru2 = Add(bs, "oracle-l5r-7360", 0, Zone.Battlefield, province: 0);
        var air2 = Add(bs, "oracle-l5r-9344", 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(bs);
        Reject(eb, UseF(eb, 0, "neiru-equip", neiru2.Id, air2.Id, sources: eb.PaymentOptions(0, 2).FirstOrDefault()?.SourceIds));
        // And a Spell that is not in the player's OWN hand is out of reach.
        var s3 = ActionTable();
        var neiru3 = Add(s3, "oracle-l5r-7360", 0, Zone.Home);
        var enemyHand = Add(s3, "oracle-l5r-9344", 1, Zone.Hand);
        var discarded = Add(s3, "oracle-l5r-9344", 0, Zone.FateDiscard);
        Add(s3, "training-marketplace", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        var pay3 = e3.PaymentOptions(0, 2).First().SourceIds;
        Reject(e3, UseF(e3, 0, "neiru-equip", neiru3.Id, enemyHand.Id, sources: pay3));
        Reject(e3, UseF(e3, 0, "neiru-equip", neiru3.Id, discarded.Id, sources: pay3));
    }

    // ---- Shinden Shorai.
    static (MatchEngine E, CardInstance Region, string ProvinceCardId) ShoraiTable(string personalityDef, string clan = "Crane", int honor = 3, int turnsTaken = 3, int provinceIndex = 0, int turnNumber = 1)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0; s.TurnNumber = turnNumber;
        s.Players[0].Clan = clan; s.Players[0].Honor = honor; s.Players[0].TurnsTaken = turnsTaken;
        var region = Add(s, "oracle-l5r-6857", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = region.Id;
        var provinceCard = s.Cards[s.Players[0].Provinces[provinceIndex].CardId!]; provinceCard.DefinitionId = personalityDef; provinceCard.FaceUp = true;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        return (MatchEngine.FromTestState(s), region, provinceCard.Id);
    }
    static GameCommand Recruit(MatchEngine e, CardInstance region, string cardId)
    {
        var cost = CardCatalog.Get(e.State.Cards[cardId].DefinitionId).GoldCost;
        return UseF(e, 0, "shinden-shorai-recruit", region.Id, cardId, sources: e.PaymentOptions(0, cost).First().SourceIds);
    }
    const string LionWithHonorRequirement = "oracle-l5r-10290"; // Akodo Chiyo: Lion, Honor Requirement 7, cost 7
    {
        // A Lion Personality whose Honor Requirement (7) exceeds the player's Honor (3): waived, paying all costs.
        var (e, region, id) = ShoraiTable(LionWithHonorRequirement);
        Check(e.QuotePurchase(0, id).Error is not null, "Control: an ordinary Purchase quote is still bound by the Honor Requirement");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Recruit(e, region, id));
        Equal(e.State.Cards[id].Zone, Zone.Home, "Brought into play at Home");
        Check(e.State.Cards[id].FaceUp && !e.State.Cards[id].Bowed);
        Equal(e.State.Players[0].Honor, 3, "No Honor change (Proclaim needs an in-Clan Personality)");
        Check(e.State.Players[0].Provinces[0].CardId != id, "The province card left it (and was refilled face down, like any recruit)");
        Equal(e.State.PriorityPlayerId, 1, "An Action-phase action cedes priority like any other");
        Check(e.State.Events.Any(ev => ev.Type == "payment" && ev.Message.Contains("paid 7 Gold")), "All costs: the Personality's full printed cost (7)");
    }
    {
        // The waiver is keyed on the PERSONALITY's Clan: a non-Lion Personality with an Honor Requirement is still bound...
        var (e, region, id) = ShoraiTable("oracle-l5r-142"); // Agasha Kodo: Phoenix, Honor Requirement 6
        Reject(e, Recruit(e, region, id));
        // ...but recruits fine with sufficient Honor (the control), and a Lion Personality needs no Honor either way.
        var (e2, region2, id2) = ShoraiTable("oracle-l5r-142", honor: 6);
        Accept(e2, Recruit(e2, region2, id2));
        Equal(e2.State.Cards[id2].Zone, Zone.Home);
        // And it is NOT the player's own Clan that matters: a Lion Clan PLAYER recruiting a non-Lion Personality gets no waiver.
        var (e3, region3, id3) = ShoraiTable("oracle-l5r-142", clan: "Lion");
        Reject(e3, Recruit(e3, region3, id3));
    }
    {
        // "Your first or second turn" is the player's OWN turn count: 1 and 2 are refused, 3 is fine -- whatever the global turn number says.
        foreach (var (turnsTaken, turnNumber, legal) in new[] { (1, 1, false), (2, 3, false), (2, 99, false), (3, 1, true), (4, 2, true) })
        {
            var (e, region, id) = ShoraiTable(LionWithHonorRequirement, turnsTaken: turnsTaken, turnNumber: turnNumber);
            var command = Recruit(e, region, id);
            if (legal) Accept(e, command); else Reject(e, command);
            if (legal) Equal(e.State.Cards[id].Zone, Zone.Home);
        }
    }
    {
        // "A face-up Personality in THIS province": another province's card, a face-down one, an opponent's, a Holding, and no gold.
        var (e, region, id) = ShoraiTable(LionWithHonorRequirement, provinceIndex: 1); // the card is in province 1; the Region is on province 0
        Reject(e, Recruit(e, region, id));
        var (e2, region2, id2) = ShoraiTable(LionWithHonorRequirement);
        var faceDown = e2.State; faceDown.Cards[id2].FaceUp = false;
        var eFaceDown = MatchEngine.FromTestState(faceDown);
        Reject(eFaceDown, Recruit(eFaceDown, region2, id2));
        var (e3, region3, id3) = ShoraiTable("training-marketplace"); // a Holding is not a Personality
        Reject(e3, Recruit(e3, region3, id3));
        var (e4, region4, id4) = ShoraiTable(LionWithHonorRequirement);
        var broke = e4.State; foreach (var c in broke.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var eBroke = MatchEngine.FromTestState(broke);
        Reject(eBroke, UseF(eBroke, 0, "shinden-shorai-recruit", region4.Id, id4)); // no Gold to pay "all costs"
        // The opponent's own province card is never yours to bring into play.
        var (e5, region5, _) = ShoraiTable(LionWithHonorRequirement);
        var theirs = e5.State.Players[1].Provinces[0].CardId!; var theirState = e5.State; theirState.Cards[theirs].FaceUp = true; theirState.Cards[theirs].DefinitionId = LionWithHonorRequirement;
        var eTheirs = MatchEngine.FromTestState(theirState);
        Reject(eTheirs, Recruit(eTheirs, region5, theirs));
    }
    {
        // A Lion Clan player recruiting an in-Clan Personality may still Proclaim afterward (the shared entry sequence's own Proclaim
        // window, resuming to the opponent once both pass, like any Action-phase action).
        var (e, region, id) = ShoraiTable(LionWithHonorRequirement, clan: "Lion");
        Accept(e, Recruit(e, region, id));
        Equal(e.State.PendingChoice?.Type, "proclaim");
        Equal(e.State.PendingChoice?.ActingPlayerId, 1);
        Accept(e, Cmd(e, "proclaim"));
        Equal(e.State.Players[0].Honor, 3 + CardCatalog.Get(e.State.Cards[id].DefinitionId).PersonalHonor, "Proclaim: gain the recruit's Personal Honor");
        Check(e.State.PendingChoice is null, "nothing is left to react to after the Proclaim: the window closed (issue #639)");
        Equal(e.State.PriorityPlayerId, 1);
    }
    {
        // QueryActions offers it (Action phase, own Region), and the offered command -- Gold sources included -- validates.
        var (e, region, id) = ShoraiTable(LionWithHonorRequirement);
        var offered = OfferedFor(e, 0, "shinden-shorai-recruit");
        Check(offered.Length == 1 && offered[0].TargetId == id && offered[0].SourceIds is { Length: > 0 });
        var (e2, _, _) = ShoraiTable(LionWithHonorRequirement, turnsTaken: 2);
        Check(!e2.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "shinden-shorai-recruit"), "Turn 2: not offered as an enabled action");
    }
}
// One Neiru with one Air Spell in hand and the Gold to cast it (OfferedFor's own table).
static MatchState NeiruOfferTable()
{
    var s = ActionTable();
    Add(s, "oracle-l5r-7360", 0, Zone.Home);
    Add(s, "oracle-l5r-9344", 0, Zone.Hand);
    Add(s, "oracle-l5r-6502", 0, Zone.Hand);
    Add(s, "training-marketplace", 0, Zone.Home); Add(s, "training-marketplace", 0, Zone.Home);
    return s;
}
