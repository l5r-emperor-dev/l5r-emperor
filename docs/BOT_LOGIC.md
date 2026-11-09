# How the practice bot decides (plain-English review copy)

This describes what `src/L5R.AI` does today on `main`. It is a reading aid for review, written from the code (issue #813); if the two ever disagree, the code is right, so fix this page. Numbers are the bot's internal scores (higher wins) and are included so you can see *how strongly* it prefers something, not just what.

Status tags: **ACTIVE** (shipped and on), **PARKED** (in the code, off by default, see [the decision record](decisions/2026-10-09-bot-battle-policy.md) and [the parked note](parked/2026-10-09-what-makes-a-good-l5r-bot.md)).

## 1. The basic mechanism

- The bot sees only what its seat is allowed to see (its own hand, the public table) and the list of actions the engine offers. It cannot cheat or look at hidden cards.
- At each decision it takes **every enabled action** the engine offers (except concede and wait), fills in the choices that action needs (targets, payment, province, etc.), gives each a **score**, and does the highest. Ties break by action type name, then card id, then ability id, so a replay always makes the same choices.
- **Passing is worth 10.** Playing a card or using an ability is never scored below a pass, except the "never do this" cases below (harming itself, giving the opponent Honor), which are negative so it passes instead.
- If it is not the bot's turn to act and nothing is offered to it, it waits.
- If the engine refuses an answer the bot chose, the bot is told and picks a different answer at the same decision (for attacks it tries one unit at one province at a time).
- Memory across the match: every face-up card it has seen (assuming 3 copies of a non-Unique card, 1 of a Unique), whether it lobbied this turn, how many times it has used each ability this turn, and what the engine refused.
- Each repeat use of the same ability from the same card in one turn lowers that use's score by 20 (never below passing). This stops two bots trading a free action back and forth forever.

## 2. The bot's Honor profile (ACTIVE)

The bot classes its own deck as **High**, **Intermediate** or **Low** Honor:

- Stronghold starting Honor 5 or more counts as a High signal; under 3 counts as a Low signal.
- Average Personal Honor of its Personalities over 2.5 is High; under 1.5 is Low. It uses its decklist if the game supplies it, otherwise the average of its own Personalities it has seen (needs at least three); until then only the Stronghold speaks.
- More signals wins; a tie is Intermediate.

What the profile changes:

| Choice | High | Low | Intermediate |
| --- | --- | --- | --- |
| Proclaim | Always a priority | Only when it has spare Gold (2+ left after a purchase) | Priority if within 4 Honor of the opponent or ahead; deprioritised if behind by more than 5; neutral between |
| "Gain Honor" vs "opponent loses Honor" | Gain | Make the opponent lose | Gain if ahead or below its own starting Honor, otherwise make the opponent lose |

## 3. Dynasty phase: buying and recruiting (ACTIVE)

- **Holdings:** the first holdings score 160, minus 10 per holding it already owns (so the fifth is 120). With 5 to 9 holdings it scores `70 - number owned`; with 10 or more, 5 (so it stops). If it can afford **every** face-up card in its provinces, it buys them all whatever its holdings count and discards nothing (issue #818).
- **Personalities / Followers:** `110 + 2 x Force + 2 x Personal Honor - Gold cost`, with adjustments:
  - Proclaim-eligible: +40 (Prioritise), +20 (Neutral), -10 (Deprioritise) from the Honor profile.
  - Paying the Clan allegiance cost when it has the Honor: -20.
  - Using the Clan discount (if Proclaim is not already used this turn): -25 (Prioritise), -15 (Neutral), +15 (Deprioritise). So a deck that wants to Proclaim avoids spending the discount that turn.
  - +30 for a turn after it used the Border Keep refresh (it wants the fresh cards). Only the parked refresh sets this, so it is inactive while that rule is off.
- **Experienced Personality overlaid for free:** 180 + 2 x Force, but only when it has more Personalities in play than its opponent, or the card would otherwise be discarded (issue #818); otherwise -5, so it stays in the province.
- **Proclaim:** 1000 (always), or 40 when the profile deprioritises it.
- **End dynasty:** 0. **Discard a province card** at the end: 20, or 40 for a Holding when it already has 10 or more. Face-up Personalities are different (issue #818): if its total Gold production (bowed Holdings counted) pays for all of them, none is discarded; otherwise it discards the cheapest, one at a time, until the rest fit, counting a Personality of its own Clan 2 Gold cheaper. With Gold for the whole row nothing is discarded at all.
- **Catch-up recruit (issue #818):** after its second turn, when it has fewer Personalities than its opponent, buying a Personality scores +400 (above everything but Proclaim), once per turn; it then goes back to the usual scores.
- **Border Keep first-turn ability (ACTIVE):** score 140. It cycles province cards it cannot afford; in its first two turns with fewer than 3 holdings, Personalities too; with 7+ holdings, Holdings.
- **Border Keep once-per-game refresh (PARKED):** the 35-point default stands when parked. The parked rule would use it with 22+ producible Gold and three or more provinces holding Holdings or face-down cards.

## 4. Action phase, outside battle

### Abilities it already understood (ACTIVE)
Fixed scores, applied to the target it picks:
Defensive Stance (own bowed Personality) 95 + Force; Never Afraid / Crushing Destroy (enemy) 90 + Force; Incapacitated / Block Supply / Favor (Battle) 100 + Force; Kouki Force / Costly Feud 75 + Force; air/paid straighten (own bowed) 75 + Force; Crushing Force 70 + Force; Shinden 70 + Force; paid bow 65 + Force; Prepared Death 35 + Force. Anything aimed at the wrong side scores -20 (effectively never).

### Everything else: read from the ability's text (ACTIVE)
The bot never keys on a card's name. It reads the printed ability label and target category and classifies the effect, so a card wired tomorrow is scored the moment it exists.

- **Effect rank, in your order:** destroy (bonus 50) > bow (40) > penalize (25) > help (10) > neutral (0). An Attack counts as destroy.
- **Generic score = 30 + rank bonus.**
- **Draw abilities:** +12 for each card the hand is below its maximum. A pure draw at or over the maximum is worth only a pass.
- **Honor:** a deck that wants Honor for itself gets +25 for abilities that give it Honor; one that wants to hurt the opponent gets +25 for abilities that take it.
- **Never hurt itself:** a harmful ability (destroy, bow, penalize, Honor loss) aimed at its own card scores -50, so it passes instead of using it.
- **Targets:** for a harmful ability, the opponent's unbowed Personality with the most Force; for a helpful one, its own. When straightening, bowed ones first. A bow never goes to a card that is already bowed (Prudence, below).
- **Imperial Favor:**
  - If it has the higher Family Honor **and** more Personalities, it **always** takes the Favor (900). So does a bot with the higher *current* Honor whose opponent's units average more Force than its own weakest unit (issue #818), whatever the Personality counts. Lobbying bows its lowest-Force Personality, then the one with the fewest attachments, then the fewest printed abilities.
  - Defending, it always spends the Favor (Favor Battle) to send home the largest attacking unit (300 + that unit's Force).
  - With equal or fewer Personalities it takes the Favor only if that costs no bow (150), otherwise it is barely above a pass.
  - In between: 60.
  - Spending the Favor as a cost: unlimited if it can retake it for free; otherwise once, and only when it will certainly Lobby again.
- **Equip outside a battle:** 60 if it produces 6+ Gold, otherwise barely above a pass.
- **Bow Strategy:** 90 + target's Force against an unbowed enemy. **+3 Force Strategy:** 70. Against a bowed target, a pass.

## 5. Declaring and assigning attacks (ACTIVE)

- The **attack planner** splits its unbowed army into as many sub-armies as it can, each sent at a province, so that **each army's Force is greater than that province's Strength plus the Force of every unbowed enemy unit at home** (it assumes any home unit can defend any province). It tries the largest number of the weakest provinces first; leftovers join the army at the strongest chosen province. If not even one army clears its threshold, it does not attack.
- **Issue #818:** an army attacks when its Force beats the Province's Strength **and** beats the defenders' Force (not their sum); the Force each +N Force card in its hand could add (or a card it creates with Force) is added to its armies. Holding Honor-gaining Battle cards, it also sends one unit that can perform one at a Province that unit cannot destroy, while the rest attack a different Province (or alone, when nothing else can).
- **Cavalry waits (`UseCavalryAttack`, issue #946):** the plan is made for the whole army, but in the Infantry Maneuvers Segment only units that are not Cavalry are assigned (a Cavalry unit is one whose Personality and every Follower are Cavalry); with only Cavalry planned it assigns nobody. In the Cavalry Maneuvers Segment it re-plans with the infantry already sent plus its Cavalry, against the defense then visible, and assigns the Cavalry. Scores read the whole army, not the segment's part.
- **Declare attack:** 0 if no plan exists (and -100 if it has no units). If a plan exists, `90 + planned Force - enemy home Force`, far above passing.
- **Honor race:** if the opponent already has 40+ Honor (it wins at the start of its next turn), it attacks anything it can break, ignoring enemy defenders (declare 200, assign 150+).
- **Attack assignment:** margin over every army > 0: `90 + Force - enemy home Force`. A single army that beats the province with no enemy at home: 80. Otherwise 0.

## 6. Defending (ACTIVE)

- **Choosing battles:** it picks the unresolved battlefield with the most of its own army (100 + that Force).
- **Assigning defenders:** all its unbowed home Personalities go to one province.
  - If the attackers can destroy **every** remaining province, it defends (200) at the battlefield where its defence is likeliest to save one (largest Strength + its Force - their Force).
  - **Cavalry in reserve (`UseCavalryDefense`, issue #946, Mendon's guidance):** in the Infantry Maneuvers defense, when the attacker assigned nobody but has unbowed Cavalry units at home, the bot's infantry must be committed now (Cavalry can wait for the Cavalry Maneuvers Segment). It takes the attacker's **total possible Cavalry army** (each unbowed Cavalry unit with its attachments; a unit is Cavalry only if the Personality and every Follower are) as the threat, and the ratio **its unbowed Force / that threat**. Below 1, each card in its hand and each defending card with a printed Battle action adds 0.1; if the ratio still cannot reach 1 it does not defend (95 when it can, else 0). The first army, infantry first and strongest first, defends the most valuable province: face-up Personalities by Gold cost (most expensive first), then provinces holding a face-down card, then the rest. Each further sub-army that reaches 1 on Force alone (the hand backs the first army only) defends the next most valuable province. Units that cannot form another army stay home, unless they print a Battle action, in which case they join the first army. **Cavalry defenders are always held back** for the Cavalry Maneuvers Segment, when the real attack is visible (their Force still counts toward the ratio); if the infantry alone cannot make an army, it still holds the most valuable province.
  - With attackers already assigned, the rules below apply unchanged.
  - **Informed defence (`UseInformedDefense`, ACTIVE since 2026-10-15, issue #963 round 2, Mendon):** the defenders go where they **win** (their Force beats the attackers' there: the attackers are destroyed; 95), preferring the battlefield that destroys the biggest army and one where the Province would otherwise fall; else where they **save a Province** (attackers' Force exceeds its Strength but not Strength plus the defenders' Force; 70); else they stay home (0). Measured: 51.4% on the development decks (884-836; paired 39 vs 15) and 51.4% on the held-out decks (617-583; paired 32 vs 15).
  - With the flag off, or when nothing above applies: attackers' Force 0 means no need (0); its Force >= theirs 95; its Force plus the threatened province's Strength >= theirs 60; only one province left 50; else 0. (The no-attackers check comes first.)

## 7. Reactions (ACTIVE)

- A Reaction that **negates** an effect: always played (1500). A Reaction that **destroys or bows** a card of the opponent's: always played (1400, issue #818).
- A Reaction a card prints for its own entry (e.g. draws or tokens when it enters play): always played, 160 + rank bonus, unless it would only cost the bot Honor.
- **Any other Reaction is a weighted gamble**, rolled deterministically from the match state: the chance it plays is `0.3 + 0.15 x rank` (neutral 30%, help 45%, penalize 60%, bow 75%, destroy 90%). If it plays it scores 150 + rank bonus; otherwise 50. Declining a Reaction scores 100, so a failed roll declines.
- **Kokujin's question (issue #827):** after Kokujin wins a duel it gives up one of its own Personalities (never Kokujin unless he is the weakest) only when the loser's Force plus Personal Honor exceeds the one given up by at least 1 (score 100 + the difference - 1; declining scores 100); P'an Ku's choice to straighten or stay bowed: stays bowed (105 over 100); a Koan Spell discard: the first listed.
- **Cross-Clan Wedding, Minor Blackmail and Benten's Blessing (issue #829):** the Wedding gives away its weakest Personality (Force plus Personal Honor), both as the first gift and as the one given back (score 100 minus the worth); Minor Blackmail names the other player's strongest Personality (100 plus the worth); Benten's Blessing ticks its own Personalities (120), never an enemy's or one it already ticked (-50), then confirms (100). An Event choice with no rule takes the first offered answer.
- **Reaction windows with several Reactions:** it announces what it would play anyway, then says it is done; it orders its own Reactions first and the opponent's as announced. Once its order is set, it plays them (+1000).

## 8. Battle actions

- **Ordinary scoring (ACTIVE):** the same ability, Strategy and equip scores as above; targets follow the destroy > bow > penalize > help order.

### Equip rules (ACTIVE, `UseEquipPolicy`, issue #788)
1. **Attachments are valued like Strategies:** an Equip (Battle) of a Spell, or an Equip of an Item in a battle, is scored 30 + the bonus of the best Battle ability the attachment prints (destroy > bow > penalize > help). An attachment with no Battle ability keeps its ordinary equip score.
2. **The granted action is always used:** when attaching a Spell grants the additional action to use its Battle ability, the bot takes it (300, above everything) and never declines, even in a battle it has already won. Its target comes from the normal battle priorities.
3. **Won battle, full hand:** once the battle is decided (a defender with Force at least the attacker's; an attacker whose Force exceeds the defenders plus the province's Strength) and the bot would otherwise do nothing, it equips one card at a time while its hand is **at or above its maximum**, then passes.
4. **Undecided battle:** if equipping **at most three** attachments it can afford would move the outcome **up the ladder**, those attachments score 140. The ladder is, for a defender: loses the battle and the province, loses, ties, wins. For an attacker: loses, ties, wins, wins and destroys the province. A tie is its own rung. The Force an attachment adds is its printed Force plus the largest "+N Force" its Battle abilities give.
   - Force removed from the enemy counts (issue #818): an attachment whose Battle ability bows or destroys an enemy Personality or Follower is credited with that card's Force, the largest first, so two attachments are not credited with the same card.

### Attacker rules (ACTIVE, issue #818)
An attacker nobody opposes, whose Force already destroys the Province, passes. The only actions it still plays are those that gain it Honor or cost the opponent Honor *and will actually do so*: an Honor gain printed as "if the province has not been destroyed" (Preserving Beauty) is skipped once the Province falls. Short of the Province it plays actions as usual. A bot holding as many cards as its hand limit also plays Honor gains that change nothing in a decided battle.

### Honor cost and fruitless Attacks (ACTIVE, `UseHonorAware`, issue #870)

Mendon's playtest note: a Unicorn (high-Honor) bot played A Paragon's Strength as a Ranged Attack that could not destroy its target and lost 4 Honor for nothing.

- **Whose Honor.** An ability's label saying "Lose N Honor" (an imperative, with no player named in the sentence) is the user's own loss; "his controller loses N Honor" or "all players lose 3 Honor" is not (`AbilityIntent.SelfHonorLoss`). Before, the bot read any Honor loss as taking the opponent's Honor and even gave a low-Honor deck +25 for its own loss.
- **Price by tier.** Each Honor the user loses takes 25 points off the ability for a high-Honor deck, 12 for an in-between deck, 4 for a low-Honor deck. A 4-Honor loss therefore puts a high-Honor deck's Attack (80) below passing. Honor gained is worth 35 more to a high-Honor deck (on top of the existing +25). With no Honor involved the order stays destroy, bow, penalize, help.
- **An Attack that cannot destroy.** When no legal target has Force at or below the Attack's strength (the printed strength, "strength equal to his Force" read off the performer, the bonuses its text promises, and any augment the bot can still use), the Attack and the Honor it costs are taken off the ability and what is left is scored alone: Lion's Charge still gains Honor, a pure Attack is passed. The bot picks the highest-Force target the Attack can destroy.
- **Exception.** A killing Attack that takes the bot from not winning the battle to winning it is played whatever Honor it costs.

### Prudence (ACTIVE, `UsePrudence`, issue #902)

Mendon's playtest notes on a Scorpion Lethargy deck: it played Mysterious Deaths on its first turn with nothing to destroy, bowed Personalities that were already bowed, and Sneak Attack was wasted. None of the rules below names a card; each reads the printed text.

- **An effect on both sides** ("Each player targets a unit he controls ... Destroy all targets") is played only when the opponent loses clearly more. The bot's cheapest unit (its Personality's Gold Cost plus everything attached) must be worth at least 3 less than the opponent's cheapest, or the bot must have no Personality in play while the opponent has one. Otherwise the ability scores just under passing. A Personality-creating ability scores 200 while the bot holds such an effect that the new unit (worth 0) would make worth playing: the created Personality becomes its cheapest unit, so it is made first.
- **Bows hit unbowed cards.** A bow (a Personality, Follower, attachment, Holding or Ring alike) is aimed at a card that is not already bowed. With no unbowed legal target the action is not played at all, which keeps it in hand in case something straightens. Before, the bot kept the engine's default target (the first legal card, by id) and never looked at its bow state; with a choice to make it now picks by what the ability does (an opponent's unbowed Personality with the most Force when it hurts, its own when it helps).
- **Claiming the first action of a battle** ("The Attacker has the first opportunity ...") is done only by the Attacker, at a battlefield where both sides have units. A Defender never claims it, and an Attacker without units of its own there, or against none, does not.
- **Economy first.** The Action phase comes before the Dynasty phase of the same turn, so Gold spent on a card from the hand in the Action phase is Gold the Dynasty phase cannot spend on Holdings and Personalities. In its first two turns, with fewer than 3 Holdings and a card in a province to buy, the bot does not pay Gold for a card from the hand in the Action phase (Reactions excepted).
- The Honor an effect costs is weighed separately (the section above), so a high-Honor deck still declines Mysterious Deaths for the 4 Honor even when the units favor it.
- **Measured** (`L5R_ARENA_SPEC=prudence`, the bot-arena battle-policy filter, 12 decks, every ordered pair twice with the bots swapped, one seed): the bot with the rules won 174 of 264 games against the same bot without them (65.9%, sign test p < 0.0001). Most of the gain is the target choice: with a choice to make the bot used to keep the engine's default target, the first legal card by id.

### Six battle rules (PARKED, off by default, `UseBattlePolicy`)
When switched on:
1. **Pass when the battle is decided**, except to gain Honor / take the opponent's, to move a unit that is not needed into an opposed battlefield, or to remove an enemy Personality that could undo the result. In-play abilities before hand cards. Never use an ability whose cost bows a unit the win depends on.
2. **Attack target:** the highest-Force target the Attack can destroy (counting every augment the bot still has); ties go to the highest total printed Gold of the unit.
3. **Bow target:** the Personality with the most attachments (then Gold); otherwise a Personality that destroys, an attachment that destroys, an attachment that bows, a Personality that bows, one with another Battle ability, then others.
4. **Losing defender:** sends home a bowed or Force-less unit to save it, or a fighting unit only if the province still holds without it.
5. **First-action reactions** (Naval, Sneak Attack, etc.) only when the battle is not already decided.
6. **Border Keep refresh** with 22+ Gold, as in section 3.

### Trade attacks (PARKED, off by default, `UseTradeAttacks`)
When it cannot raze a province, it still attacks if it would beat every possible defender (destroying the whole defending army at no cost) or tie favourably on Gold. A unit's worth is its Gold plus attachments, +2 for a Personality. The arena found this lost slightly more than it won.

### Challenges (ACTIVE, `ChallengeGuard`, issue #1203, Mendon's instruction)
- The bot never issues a challenge (an ability that starts a duel) when the challenged Personality's duel stat is **higher** than the challenger's. A **tie** is still allowed. The stat is the one the ability prints (Chi, Force or Personal Honor), read as the current value on the table (a dishonorable Personality counts as 0 Personal Honor). Other duel modifiers (Items, "until the duel ends" bonuses) are not counted, so the rule is a floor, not a guarantee of winning.
- Not measured against the previous champion yet; `ChallengeGuard = false` restores the old behaviour for a comparison.

### Order Seppuku (ACTIVE, `UseSeppukuPolicy`, issue #963 round 1, Mendon's spec)
- Before 2026-10-15 the bot had no rule for it and ordered it whenever it was offered (a "no rule" action scores just above a pass): 2,290 orders in 1,728 games, throwing its own Personalities away.
- Now: only for a **Dishonored** Personality, only when family Honor is **-5 or lower**, only when its dishonorable death would leave Honor at **-12 or lower** (within reach of the -20 loss), and **not** when it is part of the attack the bot plans this turn (winning a battle rehonors every Dishonorable Personality in the winning army). Then weighed: `10 + 8 x Personal Honor + 3 x (-12 - Honor after its death) - 6 x Force` must beat a pass. Seppuku only saves the Personal Honor lost when a Dishonored Personality dies, and costs the Personality.
- Measured: 58.5% on the development decks (1002-711; paired 151 vs 6; Seppuku orders 2,290 -> 193) and 53.8% on the held-out decks (645-555; paired 48 vs 3).

## 9. Discarding and small choices (ACTIVE)

- **Discard down to the hand limit:** it discards the lowest-valued cards first: +3 Force Strategy 8, Bow Strategy 6, Follower 5, anything else 4.
- **Honor choices:** never lets the opponent gain Honor or loses its own by choice. Shinden (issue #818): it bows the Personality when that unit is under 15% of its army's Force or the opponent has more than 25 Honor; otherwise it bows only if that cannot change whether it would attack, and lets the opponent gain 1 Honor if it could. The same test governs an Either/Or option that bows.
- **Either/Or:** pays Gold if it can (110), else bows (90), else other options (60), never "lose Honor".
- **Attachment destruction** offered by an effect: always destroys an opponent's attachment (125), never its own.
- **Search and look-at-the-top:** takes the first legal option the engine lists. Puts a found Ring into play.
- **Read the Essence:** flips an enemy face-down card (110), never its own.
- **Any decision with no rule:** takes the first complete option, scored just above a pass (11), so it can never stall a match.

## 10. Known readings worth reviewing

- A tied defender counts as "winning" in the parked pass-when-won rule, but as a separate rung (tie) in the active equip ladder. The engine destroys both armies on a tie.
- With the six rules off, the equip rule's "would otherwise do nothing" is not enforced: in a won battle the bot still makes its ordinary plays, and the hand-limit equip (100) outranks most of them.
- The Honor race triggers at exactly 40 opponent Honor.
- The weighted Reaction roll means the bot sometimes declines a Reaction that destroys something; this is deliberate variety, not a bug.
- The arena results do not show that more rules make the bot stronger (see the parked note), so "what makes a good bot" is still open.
