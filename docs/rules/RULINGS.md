# Rulings register

This is the durable, greppable record of Mendon's (`mdornbrook`) rules rulings. Its purpose is
so agents implementing or reviewing cards do not have to re-derive an interpretation from a
decision doc's prose, and do not contradict a ruling that was already given.

**A ruling only becomes an entry here once Mendon has actually stated it.** An agent's own
"reading" of a card, offered for Mendon to confirm or correct, is not a ruling — it stays in the
originating decision doc under a "Readings" (or "Readings for Mendon to confirm or correct")
heading until he answers it. Do not add an entry from a reading alone, even a plausible one.

## Adding an entry

1. Confirm the text you want to add is attributed to Mendon as a ruling (not a reading awaiting
   confirmation) in a `docs/decisions/*.md` file.
2. Add it under the closest existing heading, or a new one if none fits, as the next `R-NNN`.
3. Keep the ruling text to one to three sentences, faithful to what the decision doc records —
   do not paraphrase it into something stronger or broader than what was actually ruled.
4. List the cards or engine primitives it touches (names, and oracle ids when the decision doc
   gives them), the date, and a link to the decision doc.
5. If a later ruling narrows, extends or overrides an earlier entry, add the new entry and note
   the relationship in both; do not silently edit the old one's meaning.

## Durations and modifiers

### R-001 — Durations default to until end of turn, not permanent
An effect is permanent only if its printed text says so; otherwise a duration (including a
province-strength change) lasts until end of turn, and a Region's effect on a province is passive
"while attached" rather than a one-time permanent change.

- **Cards/primitives:** Only Willing Spirits, Boastful Proclamation and Debt to the Kaiu are
  permanent. Changed to until end of turn: Ring of Earth, Tamori Kusugi, Burn the Towers,
  Besieged, Yoritomo's Guidance, Improved Defenses, Expensive Achievement, Siege Volley, Genji's
  Students, Indomitable Home, Stone-Hewed Shrine, Scavenging Party, Yogo Adi, The Second City,
  Courtesy of the Crane.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R3)

## Battles and provinces

### R-002 — A battle can be fought at a battlefield with no province
A battlefield may have no province, either because it was declared without one or because its
province was destroyed before engagement (e.g. two fronts). Such a battle resolves normally,
destroys no province, triggers no "destroys a province" effect, and has no adjacent battlefields.
The declarer of the Attack Phase is the Attacker unless a card says otherwise.

- **Cards/primitives:** Additional battlefields numbered from 100
  (`MatchEngine.FirstAdditionalBattlefield`), `MatchView.Battlefields`. Wired for Surprise Attack,
  Chikushudo's Trickery, Shinobi Assault, Two Fronts, The Perfect Moment.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R1)

### R-023 — Plains of Otosan Uchi: reveal order, Honor timing and province-card targeting
The 10-Honor loss fires whenever the province is destroyed (the printed "before" ordering is
immaterial). Provinces still reveal left to right; reaching Otosan Uchi with both slots face down
asks the controller which to reveal first (Left/Right), then each resolves in that order. The
second slot is created empty, refilled face down, and never revealed the turn the Region enters
play. Both cards share the underlying province's one battlefield and its adjacency. An effect
that targets "a card in a province" (Shosuro Toson's Reaction) may hit only one of the two: a
Left/Right choice, replaced by the card's own title once it is face up.

- **Cards/primitives:** Plains of Otosan Uchi (oracle-l5r-5979); `ProvinceState.CardId2`,
  `HandleOtosanUchiProvinceReveal`, `DestroyProvince`, `BeginProvinceCardChoice` (Toson wired as
  the worked example).
- **Date:** 2026-09-30
- **Source:** [Plains of Otosan Uchi province rules](../decisions/2026-09-30-otosan-uchi-province-rules.md)

## Delays and negation

### R-003 — The shared delay mechanism
An effect a delaying card holds back is recorded per card, in application order, and applied at
its trigger point (end of battle, end of phase, end of turn, the next Dynasty Phase, or the End
Phase). Each delayed effect is re-validated when its turn comes: one that can no longer be applied
(e.g. move home from home) is negated; destroy, dishonor and bow apply from any location; an
effect on a card that has left play is negated.

- **Cards/primitives:** `MatchState.DelayedEffects`. Spirit of the Berserker, In the Heart of
  Battle, Deadly Orders, Halls of the Forgotten, The Honor of the Lion, The Loyalty of the
  Scorpion, The Compassion of the Unicorn, The Courage of the Mantis, The Honesty of the Phoenix,
  The Strength of the Spider's Focus Effect. Journey's End Keep's printed "will not be negated or
  delayed" is honoured against this store.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R2)

### R-004 — Wholly vs. partially negated actions, for records
A partially negated action counts towards a record of what an action did; a wholly negated action
does not.

- **Cards/primitives:** Records built in the mechanics batch (group D), e.g. the per-player Recon
  counter and the attach log.
- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

## Additional actions and passing

### R-005 — Using a Reaction is always optional; a player may always pass
No Reaction is ever mandatory to use, and a player retains the right to pass at any decision point
where passing is otherwise available.

- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

### R-006 — Aramasu's Legacy follows its scan
The additional Attack Phase bows the controller's army until after the next Straighten Phase ends;
Mantis Clan Magistrates and attachments are not bowed by it.

- **Cards/primitives:** Aramasu's Legacy (stronghold-wave-23).
- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

### R-007 — Beyond the Line's performer
Beyond the Line may be played by either player, but only its player takes the granted action.
Hidden Falls Dojo is the one Recon Limited/Open ability that targets a province.

- **Cards/primitives:** Beyond the Line (strategy-wave-69), Hidden Falls Dojo.
- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

## Costs and payment

### R-008 — Equip (Limited) attaches Spells, Followers and Items
The Equip (Limited) designator is not restricted to Followers; it attaches any of Spells,
Followers or Items.

- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

### R-009 — Luxurious Silk's Gold Cost reduction is scoped to a Personality-Cost recruit
Luxurious Silk does not reduce the Dynasty recruit, or a recruit that pays all its costs
normally, but does reduce an action whose cost is a Personality's Gold Cost and whose effect
recruits that Personality.

- **Cards/primitives:** Luxurious Silk.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-010 — Deception Revealed only counts action-caused Ring bows
Only Rings bowed by an action (Limited, Open, Battle or Reaction), as a declared cost or as an
effect, qualify for Deception Revealed. A Ring bowed as the cost of a Focus Effect does not count.

- **Cards/primitives:** Deception Revealed.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-087 — "Discard this Celestial." after the colon is an effect, not a cost
Jizo's Mercy and Chagatai's Wrath print "Discard this Celestial." after the colon, so it is part of the effect, as for The Inevitable Grasp of Conquest and The Ways of Sensei Wu: if the action is negated, the Celestial stays in play.

- **Cards/primitives:** Jizo's Mercy (oracle-l5r-10563), Chagatai's Wrath (oracle-l5r-10816); `w829-jizo-mercy`, `w738-chagatai-end-phase`.
- **Date:** 2026-10-25
- **Source:** Mendon — [decision](../decisions/2026-10-25-celestial-audit.md). Issue #1114.

### R-088 — Lady Matsu's Rage sets no delayed destruction when its bow is negated
If Stalwart (or another negation) stops the bow, the "After the next time (this turn) it straightens, destroy it" half does not apply.

- **Cards/primitives:** Lady Matsu's Rage (oracle-l5r-4658); `lady-matsus-rage-bow-destroy`.
- **Date:** 2026-10-25
- **Source:** Mendon — [decision](../decisions/2026-10-25-celestial-audit.md). Issue #1114.

## Search and look

### R-011 — Ordered searches take from the first named location only
A search naming an ordered list of locations (e.g. "discard pile, then Fate deck") requires the
searcher to take a qualifying card from the first named location if one exists there; the searcher
chooses among that location's qualifying cards and never sees later locations. Only if the first
location has none may the searcher search the next. A searched deck is always reshuffled
afterward.

- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-012 — Ring search is an ordinary ordered search keyed on card type
Searching for a Ring uses the same ordered-search primitive as any other named-location search,
keyed on the Ring card type; an "after this card enters play: search" trait waits until the entry
windows close.

- **Cards/primitives:** Enslaved Djinn (oracle-l5r-10680), Celestial Sword of the Dragon - exp
  (oracle-l5r-10112), Asako Chukage (oracle-l5r-533); `PendingEntrySearches`.
- **Date:** 2026-09-25
- **Source:** [Rend the Soul, No Hiding Place, the Ring searches](../decisions/2026-09-25-rend-no-hiding-place-ring-search.md)

## Duels and focus

### R-013 — A Focus Effect is not an action
Bad Kharma and Footsteps of Madness may be played at any time, on top of other modifiers. A Focus
Effect is not an action.

- **Cards/primitives:** Bad Kharma, Footsteps of Madness.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-014 — He's Mine! and Bayushi Irezu act only in an accepted or mandatory duel
He's Mine! and Bayushi Irezu (same shape) act only in an accepted or mandatory duel.

- **Cards/primitives:** He's Mine!, Bayushi Irezu.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

## Clans and keywords

### R-015 — There is no Ninja Clan; Ninja is only a keyword
Sixteen published cards that had incorrectly carried a "Ninja" clan alignment were corrected to
their actual clan.

- **Cards/primitives:** Goju Oyoto, Ninube Onchi (all versions), Ninube Shiho and four other Goju
  cards corrected to Spider; Shosuro Konishi, Tsuji, Chikata, Hawado - exp2, Bayushi Hurunayi,
  Mitsuo, Nitoshi, Amorie and Shosuro Nobu corrected to Scorpion.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

### R-016 — "Ninja Personality" means a Personality with the Ninja keyword
Where a card's text refers to a "Ninja Personality", that means any Personality carrying the
Ninja keyword, not a member of a Ninja clan (there is none — see R-015).

- **Date:** 2026-09-25
- **Source:** [Mechanics batch](../decisions/2026-09-25-mechanics-batch.md)

## Units and Terrain

### R-017 — Whole-unit bow/straighten affects every card of the unit individually
An effect that bows or straightens a whole unit bows or straightens every card of that unit
individually, honouring any per-card prevention effect that would otherwise stop one of those
cards from being bowed or straightened.

- **Cards/primitives:** Fortitude, Barred Passage, Hard Pressed, Block Supply Lines, The
  Conqueror.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

### R-018 — A Terrain can never be bowed or straightened
Terrain cards are exempt from every bow and straighten effect, regardless of source.

- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R5)

## Honor

### R-019 — Rhetoric and Solid Defense apply to any Honor gain or loss from an action's effects
Rhetoric and Solid Defense are offered whenever an action's effects cause an Honor gain or loss
(`GainHonor` opens an Honor-change Reaction window), not only on a loss. No published card prints
Honor as a cost before the colon (audited).

- **Cards/primitives:** Rhetoric, Solid Defense, `honorChangeReaction`.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

## Reaction scope and triggers

### R-020 — Cherry Blossom Festival bars Reactions, Proclaim included, not every trait-modelled ability
Cherry Blossom Festival bars Reactions, including Proclaim, but not Overconfident and not the 24
abilities the engine models as traits rather than Reactions.

- **Cards/primitives:** Cherry Blossom Festival, Proclaim, Overconfident; `TraitReactions`
  (audited against all 248 Reaction-timed abilities).
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-021 — The Killing Grounds fires once per player
The Killing Grounds fires once per player.

- **Cards/primitives:** The Killing Grounds.
- **Date:** 2026-09-25
- **Source:** [Emperor Edition rulings pass](../decisions/2026-09-25-emperor-edition-rulings-pass.md) (R4)

### R-022 — Personality-destruction Reactions fire on a qualifying play-to-dead transition
A Reaction to "your Personality is destroyed" is eligible when that Personality leaves play dead,
including from any action type or battle resolution; marking an already-discarded Personality dead
does not trigger it. Wording that requires destruction by or as an effect of an action excludes
battle-resolution deaths, and wording such as "another player's action destroyed" also requires
that source player.

- **Cards/primitives:** Crippled Sensei (oracle-l5r-9899); play-to-dead destruction Reaction window and action-source filters.
- **Date:** 2026-09-29
- **Source:** [Personality-destruction Reaction triggers](../decisions/2026-09-29-personality-destruction-reactions.md) (Mendon's clarifications in this task)

### R-024 — Inexorable negates the whole announced Reaction, not only a negation
Inexorable's printed text ("Its effects will not negate other effects") reads narrower than its
actual ruling: it negates the entire announced Reaction, whatever that Reaction would have done,
not only one that would itself have negated or modified something. It applies to any Reaction an
opponent announces during the Combat Segment (Phase.Battle), whether or not that Reaction targets a
card, reusing the existing whole-action negation primitive (`NegateDeferredAction`, ActionNegation.cs,
Yoritomo Sakuma's original card).

- **Cards/primitives:** Inexorable (oracle-l5r-3739); `NegateDeferredAction`; `CardWave566BeforeActionApplies`.
- **Date:** 2026-09-30
- **Source:** Mendon's ruling in issue #566's card-wave pass, correcting the fork's initial narrower reading of the printed text.

### R-025 — Wanted for Questioning's "custody" is flavor text, not a mechanic
"Your Magistrate takes him into custody" on Wanted for Questioning is flavor text with no mechanical
effect; the card is built from its remaining printed clauses alone (destroy the target's attachments
if he is dishonorable, then move him home).

- **Cards/primitives:** Wanted for Questioning (oracle-l5r-9208).
- **Date:** 2026-09-30
- **Source:** Mendon's ruling in issue #566's card-wave pass, resolving the rules-lane question tools/content/ee_p6_blockers.json had recorded for it.

## Regret, Embracing Virtue and attaching several cards from a discard pile

### R-026 — Embracing Virtue is checked before the End Phase's draw and hand-size discard
Embracing Virtue's "before an End Phase begins, if this card is in your discard pile: you may put it in your hand" is checked in the
End Phase, before the hand-size check. A player who takes it back on their own turn may therefore draw above their hand size and be forced to discard.

- **Cards/primitives:** Embracing Virtue (oracle-l5r-2313); `BeginEmbracingVirtue` (RegretEmbracingVirtue.cs).
- **Date:** 2026-10-03
- **Source:** Mendon's instruction when wiring Regret and Embracing Virtue (issue #619). Confirmed by Mendon on 2026-10-04: "an End Phase" is any End Phase, so each player's copy is asked at every End Phase (the active player first, once per copy per turn).

### R-027 — Regret counts the cards the winner's side focused; a stat bonus instead of focusing is not a focus
Regret's Focus Effect counts the cards focused by each player and makes the duel winner's controller lose Honor equal to the number of cards his side focused.
Abilities that add a bonus to a stat instead of focusing (Kakita Gonji) are not a focus and are not counted. Confirmed 2026-10-04: this holds for any "in place of focusing" ability, not only Gonji's.

- **Cards/primitives:** Regret (oracle-l5r-9923); Kakita Gonji (oracle-l5r-4167); `ApplyRegretFocusEffect`.
- **Date:** 2026-10-03
- **Source:** Mendon's instruction when wiring Regret (issue #619).

### R-028 — Regret's Open action attaches many cards at once, and is not Equip (Battle)
Regret's Open action behaves like the Dragon stronghold's equipping from a discard pile, except that the player may select several targets, pay for each of them, and then attach them to
Personalities; the legality of each attachment still applies. It is not the Equip (Battle) action (nor is Bayushi Nitoshi's), so a Personality attaching a Spell this way does not get Equip (Battle)'s
second, subsequent action to cast the Spell. Actions printed on cards attached this way are available at the player's next subsequent action in the phase (Action Phase or battle): a player who
attaches Blades of the Black Dragon can use it on their next turn to make an attack.

- **Cards/primitives:** Regret (oracle-l5r-9923); Bayushi Nitoshi - inexperienced (the "not Equip (Battle)" half); `GoldCostBasis.AttachSlotsCost`; `RunRegret`; `RegretEntryQueue`.
- **Date:** 2026-10-03
- **Source:** Mendon's instruction when wiring Regret (issue #619). The first implementation (PR #621) priced all the cards as one payment; Mendon's ruling of 2026-10-04 (R-029) replaces that.

### R-029 — Each card Regret attaches is its own Equip (Limited)-like action, resolved in full before the next
Regret's Open action treats each attachment as a standalone equip (Limited) action taken as part of the Open action. Each card is paid for independently, as an attachment (or its sub-type: Weapon, Armor,
Follower), so a Gold source that pays more for an attachment does so every time; each can be reacted to on its own; and each attaches and resolves its "after this enters play" effects before the next begins.
This refines R-028 (which left the payment as the implementation's reading).

- **Cards/primitives:** Regret (oracle-l5r-9923); Platinum Mine (oracle-l5r-9798); `AdvanceRegret`, `RegretStepSpec`, `EitherOrPayingFor` (RegretEmbracingVirtue.cs).
- **Date:** 2026-10-04
- **Source:** Mendon's ruling on the PR #621 readings (issue #619). The reaction opportunity of each card is the window of its entering play; a separate before-action window per card is not modelled.

### R-030 — An Open action is an Action Phase action unless an effect grants it in a battle
Regret has no Battle designator, so it is usable only during the Action Phase. Open actions are not normally usable in the battle segment and stay that way; if an effect grants the ability to play an
Open action during the Battle Phase, it may be played then.

- **Cards/primitives:** Regret (oracle-l5r-9923); the engine's Open timing (`AbilityActions`, `ValidateAbility`).
- **Date:** 2026-10-04
- **Source:** Mendon's ruling on the PR #621 readings (issue #619).

### R-031 — "Bow it ... Destroy it if it is an attachment": bow first, then destroy, as separate effects
Togashi Hiroto and Moshi Rukia - exp bow a target card without attachments and destroy it if it is an attachment. The card is bowed first, then destroyed. The two are separate effects: a Stalwart-style negation of the bow does not save the attachment from destruction, and a negation of the destruction does not undo the bow.

- **Cards/primitives:** Togashi Hiroto, Moshi Rukia - exp; `hiroto-bow` (CardAbilities.cs).
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (GC-2), issue #748.

### R-032 — A printed "If" before an effect is a conditional cost: unmet, the action cannot be taken
A condition written as "If ..." before an effect (Daidoji Ichigiku's Family Honor test, Moto Ming-Gwok's "If there is a dead Personality in any discard pile") must be met to take the action at all. Costs must be paid to take an action; an unmet condition bars it, so the action is not offered.

- **Cards/primitives:** Ichigiku, Moto Ming-Gwok - exp (personality-wave-135); `WlPersCoils.cs` requirement checks.
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (CM-1), issue #757.

### R-033 — "Then discard a card" after a search or look: the card just found may be discarded (Daisuke, Tselu, Shizuka)
For Kitsuki Daisuke - exp2, Shinjo Tselu - exp and Ikoma Shizuka (Terrain search) the card just taken into the hand may be the card discarded, and when it is the only card in hand it is discarded. This reverses the issue #368 answer for Shizuka (the found Terrain could not be discarded); Asahina Kitiaru - exp (#679) already followed this rule.

- **Cards/primitives:** Daisuke (oracle-l5r-10452), Tselu (oracle-l5r-10511), Ikoma Shizuka (oracle-l5r-10292); `SearchSpec.DiscardMayBeFound`.
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (CM-8, CM-9) and his follow-up on Shizuka (2026-10-08), issue #757.

### R-034 — A card without a unit that needs a performer is performed by the Personality in that unit (Daidoji Adachi)
Adachi's "opposing" counts a card with no unit of its own that requires a performer (a Strategy, a Holding): the performing Personality's unit stands for it, and the Personality's Madness tokens are compared to his printed Personal Honor. Pillowbook bars the discard.

- **Cards/primitives:** Daidoji Adachi (oracle-l5r-10449); `ApplyWlPersCoilsAfterAction`.
- **Date:** 2026-10-07
- **Source:** Mendon's ruling on the PR #676 readings (CM-13), issue #757.

### R-035 — A performer may be bowed unless the card says "unbowed"
A card that names a performing Personality without the word "unbowed" may be performed by a bowed Personality (Strength in Terror, Surety of Purpose). Only a printed "unbowed" requires it to be straightened.

- **Cards/primitives:** Strength in Terror (oracle-l5r-7538); Performer categories without "unbowed".
- **Date:** 2026-10-08
- **Source:** Mendon, during playtest (QoL batch).

### R-036 — A Spell may be equipped by a Shugenja that does not share its element; a Focus Effect's wielder limit applies to that Focus Effect only
Equip (Battle), and any other ordinary equip of a Spell, is not bound by matching element keywords: a Fire Shugenja may equip a Water Spell. A restriction printed inside a Focus Effect (The Swell of Strength: "As a Focus Effect: ...attach this card to one of your Water Shugenja, ignoring Gold cost") limits that Focus Effect's free attach only and does not affect the card's ordinary equip in any other phase or segment. Library of Rebirth's Reaction is likewise not element-bound.

- **Cards/primitives:** The Swell of Strength (oracle-l5r-8366), which no longer carries a whole-card `AttachRequiresKeyword`; `FocusEffectChoice.AttachWielderCandidates`; Library of Rebirth (oracle-l5r-4724).
- **Date:** 2026-10-09
- **Source:** Mendon, during playtest (issue #794).

### R-037 — Library of Rebirth pays only in the Combat Segment (as printed), not in the Action phase
"When paying for a Spell during the Combat Segment" means the combat part of a battle. Equipping a Spell in the Action phase is not helped by the Library's 4 Gold, bowed or not.

- **Cards/primitives:** Library of Rebirth (oracle-l5r-4724); `ReactivePaymentConditionMet` (`spell-in-combat`).
- **Date:** 2026-10-09
- **Source:** Mendon, during playtest (issue #794).

### R-038 — A less experienced version in play does not stop a more experienced one being recruited
An Experienced Personality may be bought for Gold (or brought in by any other recruit) while a lower-Experience version of the same title is in play; the two are independent cards. Overlaying is optional, never required. Only a copy of the same Experience Level is a duplicate for the Unique/Singular rule.

- **Cards/primitives:** Tsuruchi Tomaru - experienced (oracle-l5r-8894) over Tsuruchi Tomaru (oracle-l5r-8893); `ControlsCopyOf(..., experienceLevel)`.
- **Date:** 2026-10-09
- **Source:** Mendon, during playtest (issue #803).

### R-039 — Reaction windows both players can react in: announce, order, resolve
When both players hold a Reaction in an alternating Reaction window (Engage, entry, duel result, after-resolution, after-action, after-Battle-action), the turn player and then the other player announce every Reaction they intend to play, then say they are done. If the announced Reactions are not all the turn player's and there is more than one, the turn player chooses the order they resolve in; otherwise they resolve in the order announced. Each is then played by its owner (targets and Gold chosen at that time; one no longer legal fizzles) and the window closes after the last. A window only one player can react in is unchanged: Reactions are played one after another in the order played. A player who took Pass all battles is treated as holding no Reaction.

- **Cards/primitives:** `PendingReactionOrder` (ReactionOrdering.cs); the bot orders its own Reactions first, then the other player's in the order announced.
- **Date:** 2026-10-09
- **Source:** Mendon's gameplay QoL list, items 3 and 4 (issue #803).

### R-040 — The seven Emperor Edition cards Oracle lists as banned are playable
Kalani's Landing, One Koku, Caught Unawares, Cross-Clan Wedding, The Duty of the Crab and Well-Tended Farm (which Oracle records as banned from Emperor Edition tournaments, December 2012 or September 2013) and The Governor's Estate (Legacy-only) are playable like any other Emperor Edition card once wired. The tournament ban is kept only as a data note on each card (`source_ban_notes` in `content/oracle/emperor-supplemental-catalogue.json`, and the inventory's blocker notes); the game does not display it, restrict deck building because of it, or treat it as a rule.

- **Cards/primitives:** content/oracle/emperor-supplemental-catalogue.json; the library carries no ban field.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #812).

### R-041 — Kalani's Landing: targeting a card in play and reducing its Gold Cost
Kalani's Landing's Reaction ("After an action is announced, target a card (in play): Give it -2 Gold Cost") targets a card in play with a non-zero Gold Cost. A card is in play when it is in a player's home (holdings, Personalities, Rings and other permanent-effect cards) and not explicitly out of play, or at a battlefield; a card with no printed Gold Cost has a Gold Cost of 0 and cannot be targeted. The Reaction can happen after any action; once it has been used, its window is not opened for that player again until the next turn. The target's current Gold Cost is reduced by 2 until the end of the turn.

- **Cards/primitives:** Kalani's Landing (oracle-l5r-4252); the Gold Cost modifier (`ApplyModifier` with "GoldCost", read by `CardGoldCost`), the announce window for either player's action (`Wave816AnnounceWatcher`) and `EngineHooks.PhaseBegins`; docs/decisions/2026-10-10-kalanis-landing.md.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #816).

### R-042 — The Governor's Estate creates a Holding instead of putting a face-down card into play
Where the Governor's Estate says to turn a card in your province face-down and put it into play bowed, read it as *creating* a card that is a Holding: the card in the province is removed from the game (shown in the removed-from-the-game pile only if it had been revealed before; a face-down card stays hidden) and a bowed Holding is created in the player's home. The Holding is the supplied "Imperial Estate" scan: Gold Cost 0, "Bow this card: Produce 2 Gold." The Reaction is used after the owner's Dynasty Phase begins, twice a turn, for 2 Gold each.

- **Cards/primitives:** The Governor's Estate (oracle-l5r-8106), the Imperial Estate (user-scan-imperial-estate); `Wave821GovernorCreate`, the `dynastyBeginReaction` window; docs/decisions/2026-10-10-six-ruled-cards.md.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-043 — Hida Kaiji is never assigned into an army that holds another Personality of his side
He cannot be assigned so that he joins a battlefield with other units on his side, and other units cannot be assigned into his army; the opposing army is never considered. Other units may still be moved there during the engage or battle action segment. The rule is a count of the units of his side at a battlefield (`SideUnitCount`).

- **Cards/primitives:** Hida Kaiji - exp (oracle-l5r-10831); `RestrictionKind.SoleUnit`, `SideUnitsAt` (ArmyRestrictions.cs).
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-044 — Chuda Kaito's +3GC while the effects of his Spell action resolve
Kaito's +3GC adds 3 to his Gold Cost while the effects of a Spell action he performed are resolving. A Spell or Reaction that checks a Shugenja's Gold Cost reads it: for Damning Ceremony he is considered to have a Gold Cost of 5.

- **Cards/primitives:** Chuda Kaito (oracle-l5r-10320), Damning Ceremony; `Wave821BeginKaito`, the +3 term of `CardGoldCost`.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-045 — The gift step: Ikoma Satoru and Naoharu's Gift use one mechanism
Showing a hand card to another player, who may put it in his hand, is one mechanism; only the outcome differs. Naoharu's Gift: Honor equal to the card's Focus Value, or discard it and draw cards. Ikoma Satoru: draw two cards, or discard it and Melee Attack with strength equal to its Focus Value targeting a Follower or a Personality without Followers in his home. (Sadahako's Artistry is wired on the same step: reading 9 of the decision, to be confirmed.)

- **Cards/primitives:** Ikoma Satoru (oracle-l5r-10291), Naoharu's Gift, Sadahako's Artistry (oracle-l5r-10186); `BeginGift`, `TransferGift` (GiftOffer.cs), `AttackTargetScope.EnemyHome`.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-046 — Isawa Ryuzo's pairs will not attack or defend in the same army, and stack
The two targeted Personalities (each with more Madness tokens than printed Personal Honor) are never assigned into one army. Ryuzo is not Unique, so there can be three pairs; a Personality can be a member of up to three pairs and so be unable to assign with three others, and a pair touches no other pair.

- **Cards/primitives:** Isawa Ryuzo (oracle-l5r-10476); `RestrictionKind.NoSameArmy`, `AddSameArmyPair`.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-047 — Home Reactions and Home Engage behave like Home Battle
A Reaction with Home, and an Engage action with Home, behave as Home does on a Battle action: its unit may be at home as well as at the current battlefield. They are taken at their own timing: the Reaction's trigger, or the Engage window.

- **Cards/primitives:** Isawa Tsumaro (oracle-l5r-10863); `Wave821Validate` (the Home flag on a Reaction), `HomeWaivesSourceLocation`.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-048 — Tsumaro's Illusion lends Home to the target's Spell actions only while it stands at that battlefield
While the Illusion is at the battlefield, the original Shugenja's Spell actions have Home. Destroyed, sent home or moved to a different battle, the Spells lose it; returned to the battlefield they regain it. When the battle resolves they lose it, so moving the Illusion to a new battlefield does not carry Home there.

- **Cards/primitives:** Isawa Tsumaro (oracle-l5r-10863), Tsumaro's Illusion (oracle-l5r-11346); `IllusionLink`, `Wave821GrantsHome`.
- **Date:** 2026-10-10
- **Source:** Mendon (issue #821).

### R-049 — Kokujin: when he wins his duel, his controller is asked whether to destroy a Personality to take control of the loser
Kokujin's Battle duel destroys the loser. When Kokujin wins, a question opens before the outcome applies: "Destroy a personality to take control of the loser?" It lists every Personality his controller controls (bowed or unbowed, at any location, Kokujin himself included). Choosing one destroys it and takes control of the loser, who joins Kokujin's army; declining destroys the loser. The question is the engine's own step, not a Reaction window: it cannot be negated or reacted to. It is presented as a list of the legal cards, each selectable by clicking the card on the table or the entry in the list, and hovering one highlights the other.

- **Cards/primitives:** Kokujin - inexperienced (oracle-l5r-10486); `w827KokujinChoice` pending decision (Wave827.cs), asked from `ResolveDuel` once per duel like Awed Witness's question.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #827).

### R-050 — P'an Ku may remain bowed whenever he would straighten, and the Personality he takes stays controlled until he does
"P'an Ku may remain bowed" makes every straightening of him, the Straighten Phase's or another card's effect, the controller's choice (straighten him or leave him bowed), unless that straightening may not be negated. The Personality he takes control of stays under his controller's control until P'an Ku straightens (or leaves play) or that Personality is destroyed, and its controller cannot give it the Order Seppuku action meanwhile.

- **Cards/primitives:** P'an Ku - exp (oracle-l5r-10497); `ControlReturn.RemainBowed` and `ControlReturn.NoSeppuku` (EmperorMechB.cs), the `declineStraighten` choice, `w827PanKuStraighten`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #827).

### R-051 — Zansho - exp bows his own attached Follower
Zansho's Battle action bows an unbowed Follower attached to Zansho himself, as its cost; the Ranged Attack's strength is that Follower's Force, plus 2 if the Attack targets Daigotsu Arakan.

- **Cards/primitives:** Zansho - exp (oracle-l5r-10505); `w827-zansho-ranged`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #827).

### R-052 — Chiyurei's Axe: Melee Attacks compare the lower of the target's current and printed Force, automatically
There is no choice. A Melee Attack Hida Chiyurei performs (printed on him, or from an attached Item, Spell or action that names him as performer) is compared against the lower of the target's current Force and its printed (base) Force.

- **Cards/primitives:** Chiyurei's Axe (oracle-l5r-10340); `AttackComparedValue` (Wave827AxeComparedValue).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #827).

### R-053 — Koan cards: the Spell limit, the bow cost and the Robes
A Shugenja may normally have any number of Spells attached; Koan's Jingasa limits him to as many as the number of attached cards with the Koan keyword. When he has more, his controller discards Spells down to that count. A Spell attached to him that requires him to bow is paid by the Jingasa: he does not bow to perform it. Koan's Robes add a Force bonus equal to the number of Koan cards in his unit, counting the Robes themselves and every other Koan-keyword card of the unit, and the first destruction each turn of any card in his unit by another player's card is negated: the Personality and everything attached, Spells, Followers and Items, created cards included, tokens not.

- **Cards/primitives:** Koan's Jingasa (oracle-l5r-10524), Koan's Robes (oracle-l5r-10525); `WatchScope.Unit`, `Wave827KoanExcess`, the P24 `SpellFocusParityWaives` cost waiver.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #827). Ningen's 'Treatise on the Nezumi' is not wired: allies are out of scope.

### R-054 — Cross-Clan Wedding is two separate resolvable effects
The Event's first effect: the Event's owner targets a Personality he controls and gives it to the opponent. Its second effect: if the opponent now controls that Personality, the opponent targets a Personality he controls that is not the one he just received and gives control of it to the Event's owner.

- **Cards/primitives:** Cross-Clan Wedding (oracle-l5r-1576); a P5 province-reveal Event, `w829WeddingGive` (Wave829.cs).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-055 — Caught Unawares: five primitives
Open: a "you control no Personalities" condition; one or two target Personalities; a straightening negation that lasts THIS TURN (not the "next straightening" of Bow Before My Will and Quagmire); a draw when it is not your turn. Battle: a target filter "an enemy card with no unbowed attachments" (the existing filter is "no attachments at all").

- **Cards/primitives:** Caught Unawares (oracle-l5r-1239); `LockStraightening` (the per-turn lock), a 1-to-2 slot, `Wave829TargetAllows`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-056 — Stone of Remembrance counts as zero cards, reacts to any action that destroys one of its controller's cards, and is torn in half
It counts as zero cards when counted toward the minimum deck size. It may react to any action that would destroy a card of its controller. When it is played, an animation shows it torn in half before it is removed from the game.

- **Cards/primitives:** Stone of Remembrance (oracle-l5r-7501); `DeckWorkshop.CountsTowardMinimumOf`, the `cardTorn` event, the client's `CardTear`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-057 — Shrine to the Heavens: each Shrine protects one Celestial per Celestial revealed, and the check is made only at the reveal
When a Celestial is revealed in a player's Province, the trait lets the player select a Celestial in play and stop it from being discarded. It is a trait, so it repeats: each Shrine a player has in play protects one Celestial for each Celestial revealed in a Province (three Shrines protect up to three). Celestials not so protected are discarded when another Celestial enters play. The check is made only when a Celestial is revealed, so a Shrine that is destroyed does not cause a Celestial to be discarded.

- **Cards/primitives:** Shrine to the Heavens (oracle-l5r-7229); the P6 `CelestialEntryRegistry` hooks `Wave829Shrine1..3`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-058 — Minor Blackmail: an untimed watch on performing and on assignment
The watch is not timed. It triggers when the chosen Personality is the performer of an action (an ability on his own card, a Spell, an Item, or named as the performer by another card); it does not trigger when he is a target. It also triggers when he is assigned to a battlefield, on either side.

- **Cards/primitives:** Minor Blackmail (oracle-l5r-5061); `BlackmailWatch`, fired from the after-action traits and the two assignment sites.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-059 — The Onyx Championship: the granted ability is a usual recruit action, from the discard pile
The ability the Event grants lets its controller perform a recruit action on an Oni from the discard pile, dead Oni included. It is an ordinary recruit action; the only difference is that the Personality comes from the discard pile.

- **Cards/primitives:** The Onyx Championship (oracle-l5r-8242); `w829-onyx-maho` (recruit cost through `GoldCostRule.TargetRecruit`, an Oni in any of the player's discard piles).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-060 — Benten's Blessing: a multi-pick over Personalities in or out of play, and the +1 PH persists and counts in a Proclaim
Create a multi-pick choice. "Out of play" Personalities include those in the discard pile (the dead included) and those revealed in Provinces, but not those in a deck or face down in a Province. As usual, cards are selected by clicking them or from a pick list, with highlighting both ways. The +1 PH persists as long as Benten's Blessing is in play; if the Personality is proclaimed, the player gains the +1 PH as well.

- **Cards/primitives:** Benten's Blessing (oracle-l5r-959); `w829BentenChoice` (Sleep's multi-select actions), `Wave829PersonalHonor`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-061 — Jizo's Mercy: the second Battle is the attacker's, and 4 Honor replace the Province's destruction
After the second Battle action is used, only an attacking player may play it. Instead of destroying the Province in the resolution, the player gains 4 Honor. Every other effect of the resolution still happens as normal (losers destroyed, Honor gained or lost, resolution effects, the attacker's bow); only the Province is not destroyed.

- **Cards/primitives:** Jizo's Mercy (oracle-l5r-10563); `JizoMark`, `Wave829JizoReplacesDestruction` (called from `ContinueResolveBattle`).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829).

### R-062 — The Duty of the Crab: recruit at a battlefield from a province or the discard pile
The Battle half is a recruit at a battlefield: its source is a province or the discard pile. Whether one action can offer both sources was to be checked; it can, with the Crab Clan Personality as the action's target. Nothing remains unwired.

- **Cards/primitives:** The Duty of the Crab (oracle-l5r-8036); `w829-duty-battle` (`BringProvincePersonalityIntoPlay` / `BringDeadPersonalityIntoPlay`), `w829-duty-focus` (a delay Focus Effect).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #829). Well-Tended Farm and One Koku needed no ruling.

### R-063 — Mastering the Elements loses what it copied when it leaves play; it may then copy a different Spell
Mastering the Elements' permanent copy lasts only while the card stays in play. If it is discarded, destroyed, or returned to a hand or deck it loses the copied keywords, trait and abilities; entering play again it may copy a different Spell (the copies do not accumulate). "Once per game" means once per time in play. (Supersedes the 2026-09-21 reading that the copy persisted and added to itself.)

- **Cards/primitives:** Mastering the Elements (oracle-l5r-4874); `CardInstance.CopiedSpellRules`, `SweepSpellCopies` (PermanentCopy.cs).
- **Date:** 2026-10-11
- **Source:** Mendon, answering the #833 audit's rules questions (issue #838).

### R-064 — Engage is a battlefield-specific reaction, for every card
An Engage is not a home action. A Personality, Follower or Item taking an Engage must itself be at the battlefield being engaged, and an Engage reaches only cards present at that battlefield. Exempt: a Home Engage, an Absent Engage, and an Engage Reaction whose text names another location it may be performed from (listed by ability id in `EngageRules.cs`: Isawa Tsumaro, Recovering What Was Lost, Shinjo Hayan, The Wave Persists, Rokku, Winter Sled, False Ring of the Void). A Strategy or Item played from a hand, and a Stronghold, Holding or Region, are not unit cards in play: their own text names the battlefield they act on. First applied to The Bestial Ones (#838), then to all cards (#840).

- **Cards/primitives:** every `after-engaging` / `after-engaging-at-own-battlefield` Reaction; `EngageSourceMustBeAtBattlefield` / `EngageTargetMustBeAtBattlefield` (EngageRules.cs). Changed for Khalimpeh-jiak, Iuchi Takaki, Shosuro Takazaki, The Yari of the Shabura, The Bestial Ones and the Engage fixtures; Takaki's and New Cavalry Tactics' targets.
- **Date:** 2026-10-11
- **Source:** Mendon, issues #838 and #840.

### R-065 — A Region is present at a battlefield when its province has one; that never gives its controller presence
Regions with Battle actions, or Reactions that respond to Battle actions, may only be used while the Region is present at the battlefield. A Region is present when the province it is attached to has a battlefield created for it. A Region's presence does not satisfy the presence requirement of its controller: only a Personality can. Fallow Lands (own province under attack, its controller the Defender with presence there) is confirmed as implemented.

- **Cards/primitives:** Fallow Lands (oracle-l5r-9865); `fallow-lands-reaction`.
- **Date:** 2026-10-11
- **Source:** Mendon, answering the #833 audit's rules questions (issue #838).

### R-066 — Soul's Sacrifice gives Shadowlands and Undead to the performing Samurai; Rata's corpse line is flavor text
"Permanently give your Samurai Shadowlands and Undead" means the performer. Rata's "sacrifices his victim's corpse" is flavor text with no rules effect: only "Draw a card" is wired.

- **Cards/primitives:** Soul's Sacrifice (oracle-l5r-7385), Rata (oracle-l5r-6165).
- **Date:** 2026-10-11
- **Source:** Mendon, answering the #833 audit's rules questions (issue #838).

### R-067 — The Egg of P'an Ku: the Egg's controller controls the copy, and the copied Personality is held back whoever's it is
The player who controls the Egg of P'an Ku controls the copy. The copy enters play in the Egg's controller's home and may perform its printed abilities, attack, be assigned, and so on. The target that was copied (the second target) is subject to the bowing, attacking and defending constraints regardless of who it belongs to. Costs and restrictions such as Unique are ignored. A copied Personality with the Sensei trait still discards the controller's other Sensei Personalities: that is an effect of entering play, not an entrance restriction.

- **Cards/primitives:** The Egg of P'an Ku - exp (oracle-l5r-10522); `w835-egg`, `CreatedCardSpec.CopyOfTarget` with `CreatedRemoval.NextTurnBegins`, a straighten hold (`StraightenPhaseEnds`, as Aramasu's Legacy), `RestrictionUntil.OwnersNextTurnBegins`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-068 — Blood of Isawa's Tribe: two choices, a confirmation for the same keyword, no Reaction window and no pass
The Spell first queries the element keywords on the Shugenja it attached to and the player chooses one of them. The player then chooses which of the five elements replaces it. Choosing the same element asks whether they are sure they want to replace the keyword with the same keyword; they may cancel and go back to the second choice. The selection opens no Reaction window and offers no "pass reaction": the player always travels through the confirm path, even if replacing a keyword with itself changes nothing.

- **Cards/primitives:** Blood of Isawa's Tribe (oracle-l5r-10347); `w835-blood` (a mandatory entry trait), `w835Blood` (the pending decision), `ReplaceElementKeyword`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-069 — A Cleansing Breath: "He centers himself." is flavor text
The sentence has no rules effect and is ignored. The rest of the card is as printed: other players' cards' effects that prevent him assigning, moving, or performing actions are negated, and a Kensai's unit straightens.

- **Cards/primitives:** A Cleansing Breath (oracle-l5r-10358); `w835-cleansing-breath` (a Prevention immunity for the turn, which also frees a stored "may not attack/defend" another player's action made).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-070 — Duty Over All Things: the Yojimbo is discarded (not dead), enters at the battlefield, and an enemy Personality must be there
The performer must be discarded (in the discard pile, not dead). The performer enters play at the battlefield where Duty Over All Things is played, and the card may not be played unless an opposing (enemy) Personality is present at the battle when it is played.

- **Cards/primitives:** Duty Over All Things (oracle-l5r-10370); `w835-duty-discount` / `w835-duty-full` (the printed "paying 3 less Gold if you control a Courtier or Shugenja" as two prices of one action), `BringDeadPersonalityIntoPlay`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-071 — Imperial Deployment: assigning only, and a mandatory straightening when an attack is declared
It negates only the effects that block the Personality from assigning; he may still be barred from moving (assigning is not moving). It also carries a mandatory trigger that straightens his unit if an attack is declared in the turn it is played. That straightening can be stopped only by an effect that prevents the next straightening or says the Personality will not straighten.

- **Cards/primitives:** Imperial Deployment (oracle-l5r-10378); `ImmunityKind.AssignPrevention` (movement locks of kind Assign and a stored NoAttack/NoDefend), `DeploymentWatch` fired by the attack declaration.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-072 — Too Close to Home bars movement, not assignment; The Perfect Moment does not touch Imperial Deployment's target
Too Close to Home does not bar assignment to battlefields. It bars movement between battlefields, from a player's home to a battle, and from a battle to a player's home. It does not affect Imperial Deployment's negation. The Perfect Moment restricts only the Personalities of the player who played it, so it is not "another player's cards' effect" on the target of Imperial Deployment.

- **Cards/primitives:** Too Close to Home (the global movement lock `P4GlobalMovementLock`: Home and ToBattlefield kinds only), The Perfect Moment (`PerfectMomentLocks`: its own player's Personalities only). The existing behaviour already followed this reading; Wave835Tests proves it.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-073 — Merchant Patronage: a Reaction in the Honor-change window
After a player performs an action of a Holding that causes an Honor gain or loss, Merchant Patronage may be played to redirect the gain or loss to another player. If the player who played it controls one or more Courtiers, they may bow one to increase the gain or loss by 1 in addition to the redirect. The window may sit anywhere in the existing flow as long as it occurs before the gain or loss is realized.

- **Cards/primitives:** Merchant Patronage (oracle-l5r-10720); `w835-patronage` / `w835-patronage-bow` in the existing before-Honor-change window (`HonorChangeReactions.Trigger`, EmperorRulingR4Honor.cs), `PatronageMark` applied by an `EngineHooks.HonorChange` registrant.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-074 — Pierce the Tapestry: the name is Tapestry, up to seven copies, a per-battle count and an additional action
Oracle of the Void misspells the card "Pierce the Tapestery". Spell it correctly locally and add a note reconciling it with Oracle's spelling. A deck may hold up to seven copies. A counter of the Pierce the Tapestry played at a battlefield is kept so the destruction applies. Its additional actions may be negated by cards that negate additional actions.

- **Cards/primitives:** Pierce the Tapestry (oracle-l5r-10388); `w835-pierce`, `DeckCopyLimit` 7, `AddPeriodCount` (Period.Battle, per player), `GrantAdditionalAction` scoped to the ability; `content/oracle/name-aliases.json` maps Oracle's spelling to this card.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-075 — The Heavens' Blessing: each card an own Strategy draws costs 2 Gold on its own
It looks only at card draw arising from the effects of the player's own Strategy during an Action Phase and adds a 2 Gold cost to it. Draws by other cards (Traveling Peddler) are not affected. The Courtesy of the Crane costs 2 Gold to draw each card it grants to the player who plays it. Each card is its own instance, so several cards are several payments, each satisfied individually: a Gold source that produces 4 cannot be split to pay two costs.

- **Cards/primitives:** The Heavens' Blessing (oracle-l5r-10558); `w835-heavens-draw`, `w835-heavens-honor` (P14 no-gain modifier, Action Phases, this turn), the `w835HeavensPay` window and `w835-heavens-pay` (a player ability).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-076 — Unbearable Weight: a card drawn into a hand is controlled by the player who holds it
A Strategy that goes into a player's hand is controlled by them and may be played as though they owned it; when it leaves play and is discarded it goes to its owner's discard pile as usual. Discarding the top card of a Fate deck reuses the existing top-card discard (`DiscardTopOfFateDeck`) rather than a new primitive.

- **Cards/primitives:** Unbearable Weight (oracle-l5r-10550); `w835-unbearable` (a Fate-deck slot and an optional target), `WeightReturn`, `OriginalOwnerId`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-077 — Warded Premises negates only the bonuses actions give
Permanent bonuses, bonuses from attachments, and bonuses from tokens are not negated. Only bonuses from actions are negated. Bonuses from cards or tokens attached by actions are not negated.

- **Cards/primitives:** Warded Premises (oracle-l5r-10404); `w835-warded-negate` (a before-resolution trait removing positive, non-permanent Force Modifiers of action abilities from cards at its battlefield), `w835-warded-play`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-078 — Revelations: the owner shows, the other player discards, the owner arranges, the Province refills face up
Revelations shows the top five cards of the owner's Dynasty deck. After a discard of the opponent's choice, the owner decides the order of the remaining four, and then the Event's final step replaces its empty Province face up. A viewing pane lets the opponent select the card to discard; the owner then reorders the rest.

- **Cards/primitives:** Revelations (oracle-l5r-10262); a P5 reveal Event, `w835-revelations-shown` (the shown-choice panel), `w835-revelations-order` (the look panel), `FaceUpRefill`.
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

### R-079 — The Empire's Foe Exposed: a duel opened while the Events Phase resolves
The Event opens a duel during the event resolution stage of the Events Phase. It does not resolve if both players do not have a Unique Personality in play; it is discarded without effect. A player with exactly one Unique Personality is not asked. If both have exactly one, they are placed into the duel at once. Otherwise each player who has a choice gets a selection panel listing only the Unique Personalities in play they control, selectable by clicking a card or from the list, with the matching cards highlighted.

- **Cards/primitives:** The Empire's Foe Exposed (oracle-l5r-10423); `w835-foe` (an Event), `w835Foe` (the selection), `w835-foe-duel` (`IssueChallenge`; the loser is destroyed).
- **Date:** 2026-10-11
- **Source:** Mendon, card wiring list (issue #835).

## Bowing and performers

### R-080 — A bowed Personality may use the abilities of his unbowed attachments
"Abilities on a bowed card can not be used" refers to the card carrying the ability. An Item or Spell's ability is blocked only when that attachment is bowed; the host Personality, who performs it, may be bowed (unless the text says "unbowed", R-035). "Even if this card is bowed" lets a bowed attachment use the ability.

- **Cards/primitives:** every Item and Spell action; Nightingale Blade (oracle-l5r-5556) with a bowed Bayushi Utoro (oracle-l5r-10249) is the case that raised it; Celestial Sword of the Crab - exp, Fubatsu Plate, Mantle of the Jade Champion - exp2 and Strength of the Tsunami print the attachment-bowed permission. The engine behaved this way in the Utoro case; no change.
- **Date:** 2026-10-12
- **Source:** Mendon, from the Comprehensive Rules clarification (Bowed and unbowed, 2013-06-05) — [decision](../decisions/2026-10-12-bowed-host-attachment-abilities.md); text in [COMPREHENSIVE_RULES.md](COMPREHENSIVE_RULES.md).

### R-081 — Battlefield adjacency: battlefields sharing a province form a line between the flanking provinces
Battlefields at adjacent provinces are adjacent (destroyed provinces slide together; "left" and "right" follow the Defender's province line). A battlefield with no province is adjacent to nothing. When several battlefields are associated with one province (Two Fronts and the like) they are arranged in a line between the flanking provinces: the leftmost is adjacent to the province on the left, the rightmost to the province on the right, and each is adjacent to its neighbour(s) within that province, so siblings are adjacent to each other and a flanking province touches only the nearer end of the line. Order within a province: the province's primary battlefield is leftmost, and additional battlefields follow in creation order (the last created is rightmost).

- **Cards/primitives:** Sea Troll Predator (oracle-l5r-6522), Moto Gunso (oracle-l5r-5320), `personality-at-adjacent-battlefield`, every "adjacent unresolved battlefield" move; `AdjacentBattlefields` (EmperorRulingR1.cs); supersedes the "every battlefield of each neighbouring province" reading of [R1 and the predator pass](../decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md).
- **Date:** 2026-10-13
- **Source:** Mendon, validation report EE-Areas-of-the-game-047 — [decision](../decisions/2026-10-13-validation-report-reading.md); Comprehensive Rules, Battlefields (text in [COMPREHENSIVE_RULES.md](COMPREHENSIVE_RULES.md)). Issue #920.

### R-082 — A face-up dishonorable Personality stays dishonorable outside play and on re-entry
A Personality who is dishonorable and face up keeps the Dishonorable status when he leaves play (the dead pile, a discard pile, removed from the game, a face-up province) and when he enters play again, unless an effect rehonors him. A hand or a deck is hidden and a face-down card has no public status, so a Personality sent there loses it. Dishonorable death's Honor loss, "dishonorably dead" checks (`IsDishonorablyDead`, Ivory Political) and Seppuku read the same flag and are unchanged. Supersedes the At Any Cost line of the 2026-09-20 gold-payment rulings (he used to return honorable).

- **Cards/primitives:** `MatchEngine.ResetCardState` / `KeepsDishonorOutsidePlay`; At Any Cost (oracle-l5r-639), Akodo Kobi, Essence of Undeath, I Give You My Name, Shinjo's Guidance and other dead-pile recruits; Your Clan Needs You!; Step Through the Void; Sensei discarded from play.
- **Date:** 2026-10-13
- **Source:** Mendon, validation report EE-STATE-008 (item 13) — [decision](../decisions/2026-10-13-validation-report-reading.md); supersedes [2026-09-20](../decisions/2026-09-20-gold-payment-rulings.md).

### R-083 — Stronghold Honor-loss immunity does not prevent a dishonorable death's Honor loss
"You do not lose Honor from ... cards you own" (Foothold of the Mad, Keep of the Dead, Steel Soul Dojo, The Plain of Glass, The Castle of Order, The Otoro Estate, The Shadow's Lair, The False Path) does not prevent the Honor loss from the death of a dishonorable Personality. That loss is a rulebook effect and comes from the rulebook, not from the dying Personality or any card (Comprehensive Rules, "From"; the Honor loss after a dishonorable death). The Forgotten Temple already treats it this way (reduced to 1, not negated).

- **Cards/primitives:** the eight strongholds above; `MatchEngine.GainHonorCore` (the immunity check is skipped while `_rulebookHonorChange`); `HonorLossImmunity`, `FootholdNegatesLoss`, `HonorLossNegatedByKeyword`.
- **Date:** 2026-10-13
- **Source:** Mendon, validation work follow-up — [decision](../decisions/2026-10-13-validation-report-reading.md); the official rulings on Fields of the Dead, The Fingers of Bone and The Shadowed Dojo in [COMPREHENSIVE_RULES.md](COMPREHENSIVE_RULES.md) ("Does not prevent honor loss from death of dishonored Personalities").

### R-084 — Great Sacrifice compares the enemy unit's total Force
Great Sacrifice's target is an enemy unit whose total Force (the Personality and all his Followers, bowed or not) is equal to or lower than the performer's Force, plus Personal Honor, plus one if he is defending. It is the same unit total Fortitude uses, not the unit's current battle contribution. Supersedes item 11 of the [2026-09-21 second-city pass](../decisions/2026-09-21-second-city-pass.md) for the unit total.

- **Cards/primitives:** Great Sacrifice (oracle-l5r-2900); `enemy-unit-within-performer-sacrifice`, `UnitTotalForce`, `SacrificeReach`.
- **Date:** 2026-10-13
- **Source:** Mendon, following the validation report — [decision](../decisions/2026-10-13-spell-static-subject.md). Issue #936.

### R-085 — A Spell's static effect follows its printed subject
A Spell attached to a Personality who is not currently a Shugenja provides no actions (#928) and, when its printed static text names the Shugenja host as the subject ("This Shugenja has +3F", "While this Shugenja is Water, he ...", his destruction or bowing), no static effect either. When the text says "This Personality" the effect works on any host. The Spell stays attached and the effect returns when the host is a Shugenja again, by printing or a granted keyword. A static whose subject is not the host is unchanged and listed for a later ruling (Incite the Phantoms, Channel the Moon, Erosion, Drawing on the Mountain, The Tapestry Perceived, Conflagration, the unit and battle-order sentences of Thunder's Stamina, Hitomi's Devotion and Warded Paths).

- **Cards/primitives:** the 26 Spells in `ShugenjaSubjectStaticSpells` (CardCopy.cs); Scouring Flood, Tempest of Flame and The Thunderer's Protection are "This Personality" and unchanged; full audit table in the [decision](../decisions/2026-10-13-spell-static-subject.md).
- **Date:** 2026-10-13
- **Source:** Mendon, following the validation report. Issue #936.

### R-087 — Fallow Lands reacts to an Attack that targets its province
Fallow Lands' Reaction ("after a Melee or Ranged Attack is targeted: -2 strength") applies to an Attack that targets the province as well as to one that targets a card. An Attack on a province opens the Attack reaction window when Fallow Lands can react (its controller the Defender with presence at the attacked province); the other Attack reactions are covered by R-088. Without any reactor such an Attack opens no window.

- **Cards/primitives:** Fallow Lands (oracle-l5r-9865); `fallow-lands-reaction`; `ResolveOrDeferAttack`, `ProvinceAttackReactionAvailableTo`.
- **Date:** 2026-10-27
- **Source:** Mendon, issue #1124.

### R-088 — Attack reactions: a province is a target, and presence at the attacked battlefield is required
An Attack on a province is targeted (as is any card the Attack affects), so every "after an Attack is targeted" Reaction triggers on it. Unless its text lets it be used from another location, a Reaction to an Attack needs presence at the battlefield where the province is attacked: an in-play card must be at that battlefield (a Follower, Item or Spell through its wielder), a Holding never is, and a Strategy played from a hand needs its player to have a Personality there. A Reaction that needs a card to be targeted (A Close Call, Akodo Kakihara, Flesh Cutter Arrows) cannot react to an Attack that targets no card. An Attack that names neither a card nor a province cannot be reacted to.

- **Cards/primitives:** `before-attack-resolves` Reactions (Khol Regulars, Archery Range, Isawa Kaname, Moshi Umiko, Kaiu Fumiko, Strong Defenses, Trusting Instinct, Precision, Sniping, Unholy Strike, Steal the Candle's Flame, Fallow Lands); `ProvinceAttackReactionOffered`.
- **Date:** 2026-10-27
- **Source:** Mendon, issue #1129. Applied to Attacks on a province only; whether the presence requirement also applies to Attacks on a card is open.

## Printings and card text

### R-086 — Ring of Fire reads as the Promotional–Emperor text on the Emperor Edition scan
Oracle of the Void's current text for Ring of Fire is the Twenty Festivals reading, which does not match the Emperor Edition scan. The card uses printing 18's text ("After the resolution of a battle at a province, if you destroyed one or more provinces or enemy cards in that resolution: You may put this card into play from your hand. Battle: Bow this card or discard it from your hand, destroy your performing Personality, and target an enemy Personality with equal or lower Chi: Destroy him.") with printing 1's scan, everywhere.

- **Cards/primitives:** Ring of Fire (oracle-l5r-6335); `ring-of-fire-recruit`, `ring-of-fire-destroy`; `content/oracle/printing-readings.json`, `tools/content/apply_printing_readings.py`.
- **Date:** 2026-10-18
- **Source:** Mendon — [decision](../decisions/2026-10-18-tutorial-lion-crane-rulings.md). Issue #935.
