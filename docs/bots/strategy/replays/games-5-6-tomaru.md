# Human decision model: games 5, 6 and the "tomaru" game

## Scope, sources and three warnings

Tags used in this file: **A** is game 6 (G6) of `../PLAYER_MODEL.md`, **B** is game 5 (G5), **C** is the Tomaru game (GT).

| Tag | Log | Human deck | Opponent | Result |
|---|---|---|---|---|
| A | `practice-20261109-201243.compare.log` (read in full) | Lion, Shamate Keep, Akodo Tacticians | Crane bot, The Aerie (duels) | Military win, turn 13 |
| B | `practice-20261109-195056.compare.log` (T1-15 in full, T16-29 filtered) | Crab, Yasuki Palaces, Courtier/Merchant control | Lion bot, Eternal Victory Dojo | Opponent lost by Dishonor, turn 29 |
| C | `tomaru.compare.log` (attack and defence turns) | Lion, Eternal Victory Dojo | Mantis bot, Koshin Keep (Scouts, Ranged) | Military win, turn 7 |

1. **Game A is not a Cavalry game.** It is a Lion deck; no unit in any of the three logs ever had Cavalry, and every Cavalry segment reads "no Cavalry units to assign". The Cavalry part of the hypothesis cannot be tested from this material. The only Cavalry-related fact is that in C the human owned an unbowed Holding that can grant Cavalry to one unit on his final attack turn (C-T7) and did not use it; the log does not show whether the engine offered that ability.
2. The logs are omniscient. Below, "public" means what he could see: both boards, Honor, Gold, hand sizes, cards already played or discarded. I never use the opponent's hidden hand to explain his choice.
3. The log records actions, not reasons. Where nothing supports a reason I say "not shown".

Abbreviations: F = Force, PS = Province Strength, H = Honor (his v opponent's), G = Gold, [=] bot agreed, [DIFF] bot disagreed. PS: his 7 in all games; Crane 6, Lion 7 (9 late in B), Mantis 7.

Headline numbers. In the two Lion games he attacked on every turn on which he controlled a Personality at the Attack Phase (A: T9, T11, T13; C: T5, T7): five attacks, eight Provinces targeted, eight destroyed, no attacking Personality lost. In the Crab game he never attacked in 14 turns although the bot recommended it 11 times.

## 1. Decision ledger

### Game A (complete)

- **T1, his turn.** No Personalities either side; G 5. Used the free first-turn refresh of all four row cards [=]; declined the once-per-game refresh to dig past a 7-cost Personality [DIFF]. No attack possible. Bought an Honor-producing Holding (4) and a free farm; the Personality (7) and a 3-cost Holding were unaffordable.
- **T3.** G 8; still no Personalities either side. Bought two 3-cost Gold Holdings [=] and left the 7-cost Personality in the row. No attack possible.
- **T5.** H 8 v 9; G 12; Crane had bought its first Personality (3F Duelist) on T4; Crane hand 8. Declined an opponent-offered optional draw while holding 8 cards [DIFF]. No attack possible. Bought Akodo Kamina (7, Proclaim +3), Copper Mine (2), free farm; left Akodo Dairuko (10) in the row. End discard: Incapacitated (moves home a *defending* Personality, so usable only when attacking) [DIFF; bot would discard a bow/straighten card].
- **T6, Crane attacks.** Public: Crane spent 8 G on a Sword for its Duelist (+2 H), used the Sword's Open to give Kamina -4F (Kamina now 0F), gave the Duelist three extra "duel, loser goes home" abilities, then attacked Province 1 with him alone, about 5F against PS 7. The Province was safe undefended. He assigned Kamina [DIFF; bot: no defence]. As defender he had the first action and used Control the Board (-6F and bow) on the attacker [DIFF; bot: a bow-or-go-home card]. Bowing also switched off every duel ability on that unit. He then passed [DIFF; bot: the in-play Celestial's Melee 4, which would have destroyed the 0F attacker and left Kamina alive]. Result 0 v 0, a tie: both units destroyed; he lost a 7-G Personality, Crane lost an 8-G Personality and an 8-G Item; H +2 him, +1 Crane. Held back: My Enemy's Mercy, Dangerous Indulgence, the Melee 4. Whether the trade was intended or he overlooked the Melee 4 is not shown; on the evidence it was avoidable.
- **T7.** G 15; no Personalities either side. Passed the once-per-game refresh [DIFF]. No attack possible. Bought Dairuko (10, Proclaim +5; 6F, Chi 5, Personal Honor 5, Melee 8, and a reaction that stops battle resolution bowing her army), Large Farm, free farm.
- **T8, Crane.** Crane bought two Chi 4 Duelists (4F, 3F); no attack.
- **T9, Action.** H 20 v 15; G 18 (+2). His only Personality: Dairuko. Crane: two unbowed Duelists (7F total), 4 Provinces, hand 8, a Stronghold whose Battle ability is a duel (loser bowed and dishonored). His hand: bow-or-go-home card, bow card, "destroy target Personality" card, Sword (8 G: +1 Chi, +1F per Lion Personality, creates a 2F token in battle), Follower (4 G: 3F, Melee), second Follower (4 G: 3F, bows a card), two situational cards. Equipped the Sword then the first Follower on Dairuko [order DIFF]; did not equip the second Follower [DIFF], keeping about 10 G.
- **T9, attack.** Declared [=]. Dairuko alone at Province 3 [DIFF on which Province; no reason for the choice is shown]. At declaration: unit 11F v PS 6 plus up to 7F of defenders = 13. Crane defended with both. Crane's Stronghold duel: he struck at once without focusing [DIFF; bot would focus a card]; Chi 6 beat Chi 4; the 4F Duelist was bowed and dishonored. He created the token [DIFF; bot: Melee]; Crane sent it home. He passed [DIFF]. Crane's second duel card: struck at once [DIFF], won; second Duelist bowed and sent home. He passed all battles [DIFF; bot wanted a Melee kill]. Result 12 v 0: Province destroyed, the bowed dishonored Duelist destroyed (Crane -3 H), he +2 H. Held back: all three hand neutralisers, Melee 8, the Follower's Melee, the Stronghold send-home. His blanket pass also skipped Dairuko's no-bow reaction, so she was bowed through Crane's next turn; this looks like an oversight (he used it on T11).
- **T9, after the battle.** Crane's extra-attack card came with no attackers. Dynasty: Akodo Furu (7, +2 H) and Akodo Uehara (3); discarded two Holdings from the row [all =]. These two and the token were his only unbowed defenders on T10.
- **T10, Crane.** Accepted the optional draw at 7 cards [=]. Passed three times holding a permanent-buff Open [DIFF x3]. Crane dishonored Furu and removed his clan, bought a Chi 5 Duelist, did not attack.
- **T11, Action.** H 28 v 14; Provinces 4 v 3; G 18 (+2). His units: Dairuko 13F with Follower, Furu 3F (dishonored), Uehara 1F, token 2F. Crane: Duelists 2F (Chi 5) and 5F (Chi 5 with a fan), hand 7. Equipped the bow-Follower on Uehara [DIFF; bot: lobby with Uehara]. Lobbied for the Imperial Favor by bowing the token [DIFF]. A Celestial in play let his Favor move home a *defender*.
- **T11, attack.** Declared [=]. Dairuko alone at Province 2 [DIFF; bot: Dairuko and Uehara at one Province, Furu alone at another]. At declaration 13F v PS 6 + 7F = 13, not enough without neutralising someone. Crane defended with both. He passed first [DIFF]; Crane's Stronghold duel with the Chi 5 Duelist: struck at once [DIFF], Chi 6 won (striking first also denied that Duelist's "+4 instead of focusing"). He used Melee 8 to destroy the unbowed 5F defender [DIFF; bot: spend the Favor to send him home], passed [DIFF], used the no-bow reaction [=]. Result 13 v 0: Province destroyed, and with it the bowed, dishonored Duelist (Crane -3 H). Favor unspent; hand neutralisers unspent. Home: Furu, Uehara with bow-Follower (unbowed), token (bowed), and Dairuko returned unbowed.
- **T11, Dynasty.** Three Personalities for 18 G (9, 4, 5; Proclaim +3). The row held only Personalities.
- **T12, Crane.** No Personalities; bought Holdings. He passed [DIFF].
- **T13.** H 35 v 11; Crane: 2 Provinces, no Personalities, hand 8, 18 G. Equipped a second bow-Follower on Uehara and played the buff Open on a 2F attacker [DIFF x3; bot: cycle a card with the Favor]. Attack [=]: Dairuko alone at one Province (16F v 6), five others at the other (14F v 6); Uehara with two Followers (9F, two bow effects) stayed home [DIFF; bot: six at one Province, Uehara alone at the other, 9F v 6]. No defence. Military victory.

### Game C (attack and defence only)

- **T5.** H 10 v 2; three 2F Tacticians bought on T3; Mantis: one unbowed 4F Scout with two Ranged 3 attacks, 4 Provinces, hand 8, 15 G. A Mantis Event publicly marked one Province: it would gain +3 PS next turn, and destroying it now gave each attacker +1F and a card. He spent all 11 G in the Action Phase on a fan and the Sword and played a permanent buff; did not lobby [DIFF x4]. Attack [=]: two Tacticians (6F) at the marked Province, the Sword-bearer (6F) at another [DIFF; bot: all three at one Province]. Both armies were *below* PS 7 at declaration; he closed the gap with the Tactician player ability "discard a card: +Force equal to its Focus Value" (used once in each battle) plus the Sword token. 9 v 0 at each: two Provinces destroyed. Nothing stayed home.
- **T6, Mantis counter-attacks** with 10F at one Province. All his units were bowed. He passed in battle [DIFF; bot: a card that moves a bowed Personality in and straightens him]. That card would have saved the Province (5F + PS 7 against 10F) but the defender would have died. He conceded the Province and kept the unit and the card.
- **T7.** Mantis: one unbowed 2F defender, attacker still bowed, 2 Provinces, hand 4. He spent 2 G on two 1-cost Items, did not lobby with the token [DIFF x3], attacked [=] with two armies (9F and 10F v PS 7) [=]. Mantis bowed one attacker and forced a discard; he discarded the card he had declined to use on T6 [DIFF], destroyed the defender with a card needing an unbowed Tactician [DIFF; bot: send home], then straightened the bowed attacker with +3F. 12 v 0 and 10 v 0: win.

### Game B (selective)

- **T5, Lion's turn.** His only Personality had a "bow: dishonor, controller loses 1 H" ability; he passed four times [DIFF x4]. Reason not shown; it kept his only Courtier unbowed, which his defensive cards require.
- **T6 onward, own turns.** Every turn he used every dishonor-with-Honor-loss ability he had. T6 he copied an opposing Holding ability he could not use (apparent error).
- **T7 onward, Lion's turns.** After Lion had bowed a Holding and a Tactician to lower one of his Provinces to PS 0, he bowed an unbowed Lion Gold Holding [DIFF; bot: a different Holding]. Repeated on eleven Lion turns.
- **T8.** Three Personalities 7F against one unbowed 2F and PS 7. No attack [DIFF]; his Force could not have taken a Province. Bought two defensive Personalities (one taxes anyone targeting him; one moves a Personality home from anywhere).
- **T12.** Used the once-per-game row refresh at 22 G. Aimed a dishonor at a Personality protected by a publicly played "negate the next action" effect; it was negated (error). No attack [DIFF].
- **T13, Lion attacks** the PS 0 Province with a 12F unit (Sword, Follower) and a 2F unit; Lion hand 3. He assigned only two 2F "tax if targeted" Personalities [DIFF; bot: seven]. Sequence: paid 10 G to take control of the 2F attacker [=]; made Lion pay 4 G when it targeted his defender, then paid 2 G to negate that action; used the stolen unit's ability to destroy the Sword [DIFF]; forced -3 H on Lion's second destroy action (one defender died); from home, a tax effect (Lion paid 4 G, -1 H); a bow with -2 H on the big attacker [DIFF; bot: Stronghold send-home]; last, a send-home on him [DIFF]. Result 0 v 2: Province held. Lion H 17 to 10, hand 3 to 1, about 8 G and its Sword gone. Held back four cards and two printed send-homes.
- **T16.** Took the Favor with an Item and discarded it to destroy Lion's largest Personality for 11 G (self -2 H, Lion -2).
- **T18-T24.** A loop: a 2-G Open retakes the Favor, a Favor ability costs Lion 1 H per dishonored Personality targeted, repeated three times a turn, plus Gold-for-dishonor abilities. Lion H 6 to -10.
- **T25, Lion attacks** with one dishonored unit; he again assigned two defenders [DIFF; bot: fifteen], destroyed the attacker with one card, passed.
- **T26, T28.** 27F against 0-3 Lion Personalities and three PS 9 Provinces; still no attack [DIFF]. T28: with Lion at -19 he made Lion's most-used Holding cost Lion 1 H per use; Lion used it on T29, reached -20 and lost.

## 2. Threat and opportunity reads

| Decision | (a) Neutralisation route he held | (b) Left home; did it hold | (c) Risk accepted |
|---|---|---|---|
| A-T9 attack, 1 unit v 2 defenders | Public: his Chi 6 v their Chi 4, so their duels would bow their own units; Stronghold send-home, Melee 8, Follower Melee. Hand: three more. Eight routes for two defenders; he needed one and spent none from hand. | Nothing existed to leave. Dairuko came home bowed (missed reaction). Two purchases plus the token defended; Crane did not attack. | Crane hand 8 unseen. A single non-duel bow or kill would have stopped the attack and exposed him with no defender. Not punished. |
| A-T11 attack, 1 v 2 | Same, plus the Favor. Used one printed kill. | Two unbowed Personalities, one with a bow-Follower fitted that turn; Dairuko unbowed. Crane had no Personalities left. | A Chi 5 Duelist with a +4 option; removed by striking first. |
| A-T13 split | None needed: no enemy Personalities. | One unit with 9F and two bow effects. | Crane hand 8, 18 G; each army had 8+ spare Force. |
| C-T5 split, below PS | Force from discards (five discardable cards), token. | Nothing. Lost one Province on T6. | Two Ranged 3 attacks against 2F bodies; a failed battle with nothing home. |
| C-T7 split | Destroy card, straighten card, send-home card for one 2F defender. | Nothing; game ended. | Low. |
| B, 14 non-attacks | Not applicable: his Force never exceeded PS plus defenders until T26, and his Personalities were the engine. | Everything; both Lion attacks failed. | Lion's Honor gains outpaced his drain until T13 (Lion 7 to 17). |

Defences: A-T6 aim was to kill the attacker (Province already safe); cost a Personality. B-T13 aim was to hold and to tax (Honor, Gold, cards, an Item); preserve units by committing only two. B-T25: kill the attacker. C-T6: concede.

## 3. Information updates

- A: he bought Holdings only while Crane had no Personality (T1, T3) and bought his first Personality on the turn after Crane's first. Confounded: T5 was also the first turn he could afford a Personality and a Holding together.
- A-T6: Crane loaded one unit with duel abilities; his first action was the one effect (bow) that disables all of them.
- A-T9: after Crane showed a duel Stronghold and two Chi 4 Duelists, he fitted the +1 Chi Item first and answered each duel by striking at once. Three duels, three immediate strikes.
- A-T10 to T11: Crane dishonored one of his Personalities; he kept that unit home on T11 (a destroyed dishonored Personality costs Honor), and attacked with it only on T13 when Crane had no Personalities.
- B-T7 to T8: one turn after Lion revealed it could set a Province to PS 0, he started bowing Lion's Gold each turn and bought his two defensive Personalities. Sequence only; causation not shown.
- B-T10: with Lion's main unit shielded, he switched the dishonor to a fresh Personality; on T12 he failed to do so.
- C-T5: he attacked the Province the opponent's Event had marked, before it grew.
- Waiting for the opponent: A-T9 and A-T11 he passed while ahead and let Crane spend its duels into him. In B the opponent acts first by turn structure, so "after Lion committed" is not evidence of choice.

## 4. Economy versus recruiting

Game A (H/P = Holdings/Personalities in play before buying, his v Crane's).

| Turn | Gold | His H/P | Crane H/P | Bought | Left | Visible reason |
|---|---|---|---|---|---|---|
| 1 | 5 | 1/0 | 2/0 | 2 Holdings (4, 0) | Personality 7 | Unaffordable |
| 3 | 8 | 3/0 | 4/0 | 2 Holdings (3, 3) | Personality 7 | Two Holdings over one body; no enemy Personality |
| 5 | 12 | 5/0 | 4/1 | Personality 7, Holdings 2, 0 | Personality 10 | Cheaper body plus Gold; enemy now has one |
| 7 | 15 | 7/0 | 5/0 | Personality 10, Holdings 1, 0 | none seen | Best body affordable with two cheap Holdings |
| 9 | 20 | 9/1 | 5/2 | Action: Item 8, Follower 4. Dynasty: Personalities 7, 3; discarded two Holdings | second Follower in hand | Attack kit first, then defenders; Holdings no longer wanted |
| 11 | 20 | 9/4 | 5/2 | Action: Follower 4. Dynasty: Personalities 9, 4, 5 | one Personality | All Gold to bodies |
| 13 | 18 | 9/7 | 7/0 | Action: Follower 4 | game ended | |

Game B, turns 2-14 (his v Lion's).

| Turn | Gold | His H/P | Lion H/P | Bought | Visible reason |
|---|---|---|---|---|---|
| 2 | 9 | 2/0 | 3/0 | Dishonor Holding 6, farm 0 | Row held only Holdings |
| 4 | 15 | 4/0 | 4/1 | Courtier 8, two Holdings (2, 2) | Left a second dishonor Holding (6) |
| 6 | 17 | 6/1 | 4/1 | Personalities 5, 9 | Repeatable abilities and card draw over Holdings (bot wanted Holdings) |
| 8 | 17 | 6/3 | 5/2 | Personalities 6, 7 | Defensive pieces, after Lion's PS-0 reveal |
| 10 | 17 | 6/5 | 5/3 | Personalities 6, 5; dishonor Holding 5 | |
| 12 | 22 | 7/7 | 6/4 | Personality 7, two cheap Holdings; 5 G card draw | About 8 G unspent |
| 14 | 26 | 9/7 | 6/4 | Personality 5, two cheap Holdings; 5 G card draw | About 15 G unspent |

C: T1 three Holdings (5 G); T3 three Personalities (12 G) with no enemy Personality in play; T5 all Gold on attachments.

## 5. The control game

There is no pivot in the log. The route was Honor drain from his first purchase (a Holding that dishonors) and he never took a military action. What changed was the rate. T6-T12: 1-2 Honor a turn, while Lion gained more than that. T13: a defence converted into -7 Honor and most of Lion's hand. T16 onward: 4-8 a turn through the Favor loop. What marks the control game: (1) he kept production unbowed for the opponent's turn and straightened his highest-yield Holding during it (T8, T11, T13: 12 G spent in Lion's turn); (2) he denied Lion's Gold every turn; (3) he treated his own Honor as spendable (3 to -4 through his own symmetric Event and his removal card); (4) after T13 he bought drain pieces, not defenders, and kept defending with two bodies; (5) he declined to attack even at 27F against one Lion Personality, when Dishonor was one or two points away and three PS 9 Provinces were not takeable in one turn.

## 6. Candidate rules

1. **Attack gate.** IF I control an unbowed Personality at my Attack Phase AND (my bow, send-home, destroy and Force-cut effects, counting Stronghold and printed abilities, outnumber the opponent's unbowed Personalities) AND my unit's Force beats Province Strength, alone or with Force I can add from hand, THEN attack. ELSE IF my Force cannot beat Province Strength plus their defenders and my win route is not military THEN never attack. Support: A-T9, T11, T13; C-T5, T7; B all turns. Contradicting: none. **Strong.**
2. **Lone champion.** IF one unit beats Province Strength alone AND its duel stat and Personal Honor exceed every enemy Personality's AND the enemy's visible defence depends on such comparisons THEN send only that unit, at one Province. Support: A-T9, T11. **Moderate.**
3. **Split only against few defenders.** IF the enemy has no unbowed Personalities (or one small one) THEN make one army per Province needed, each beating Province Strength by a margin, and balance the margins rather than stacking one army. Support: A-T13, C-T5, C-T7. Contradicting: C-T5 had no margin before hand effects. **Strong.**
4. **Do not add bodies to a contested battle.** IF the enemy holds kill or duel effects THEN each extra weak unit is a target, not security; keep it home. Keep dishonored Personalities out of contested battles. Support: A-T11, B-T13, B-T25. Contradicting: C-T5 and C-T7 (all-in, but against at most one defender). **Strong.**
5. **Attack-turn spending order.** On an attack turn, spend Gold in the Action Phase on attachments the attacker needs, stop when the attack is sufficient, and spend the rest on Personalities afterwards as next turn's defenders. Support: A-T9, A-T11, C-T5. **Strong.**
6. **Prepare home before leaving.** IF units stay home THEN fit a bow effect on one of them, lobby with a body that is not attacking, and use any effect that keeps attackers unbowed. Support: A-T11, A-T13. Contradicting: A-T9 (reaction missed), C-T5. **Moderate.**
7. **Race exception.** IF this attack leaves me one attack from winning AND the counter-attack can take at most one Province THEN commit everything, and next turn concede that Province rather than lose a unit or card needed for the kill. Support: C-T5 to T7. **Single.**
8. **Duels.** IF challenged AND my duel stat is higher AND I choose first THEN strike immediately, focusing nothing. Support: A-T9 twice, A-T11 (one game). **Strong within one game.**
9. **Stop when won; kill over send-home.** IF the battle is already won on Force THEN pass and keep the rest of the hand. IF one more action is needed THEN prefer a printed or repeatable ability to a card, prefer destroying an unbowed defender to sending him home, and keep the Favor. Support: A-T9, A-T11, C-T7, B-T25. **Strong.**
10. **Action order.** Play first the effect that disables the enemy unit carrying the abilities, or the effect that needs my own performer unbowed; play Honor and Gold taxes while the target is still present; play the send-home last. Support: A-T6, C-T7, B-T13. **Strong.**
11. **Minimum defenders.** Assign the fewest defenders that let my actions work, choosing units that punish being targeted, and perform defensive actions from home. Support: A-T6, B-T13, B-T25. **Strong.**
12. **Defend to kill.** IF the Province is safe on Strength AND I can bow or shrink a lone attacker THEN defend with one body to destroy it. Support: A-T6, B-T25. Caveat: A-T6 cost a Personality unnecessarily. **Moderate.**
13. **Opening economy.** IF the opponent has no Personality AND my Gold cannot buy a Personality plus a Holding THEN buy Holdings. Always add free and 1-cost Holdings. IF Gold buys two or more Personalities at once, or the opponent has a Personality, THEN start buying Personalities, taking the cheaper one if that still allows a Holding. Support: A-T1, T3, T5, T7; C-T1, T3; B-T2, T4. **Strong.**
14. **Switch to bodies.** Once production is about 18 Gold or I have taken a Province THEN buy only Personalities and discard Holdings from the row. Support: A-T9, A-T11. In B he kept adding cheap Holdings all game. **Moderate, military decks only.**
15. **Drain route.** IF my win route is Honor loss THEN each turn use every repeatable Honor-loss effect, on targets where it will resolve (not shielded, not already dishonored where that matters); bow one of the opponent's unbowed Gold Holdings on each of its turns; treat my own Honor as spendable while far from -20. Support: B-T6 to T29. Contradicting: B-T12 (aimed into a shield). **Strong within one game.**
16. **Reserve for the opponent's turn.** IF my hand holds Gold-cost defensive actions THEN leave that much production unbowed and straighten my highest-yield Holding during the opponent's turn. Support: B-T8, T11, T13. **Strong within one game.**
17. **Hand.** At maximum hand size discard the card usable in the fewest situations (attack-only with no attacker). He also declined optional draws at maximum and accepted below (A-T5, T6, T10); that is unexplained and probably a small loss, so do not copy it. **Single for the discard.**

## 7. Where the bot differed and it mattered

- **A-T11 and C-T5 assignment.** A: the bot's three attackers at two Provinces would have put a 3F dishonored unit alone against PS 6 and left only bowed units home. C: the bot's single stack takes one Province instead of two and delays the win.
- **A-T9, T11 duels.** Focusing a card gives the opponent a chance to focus as well (on T11 a +4 option); striking first won all three duels for no cards.
- **A-T11 battle.** The bot spends the Favor to send home a defender the human destroyed with a printed ability.
- **A-T6.** Mixed: the bot's "no defence" loses nothing and kills nothing; the bot's later Melee 4 would have improved the human's line by saving his Personality.
- **B attack declarations (11).** Attacking would have bowed the Courtiers that run the drain and that perform his defensive cards, for no Province.
- **B-T13, T25 defence.** Seven or fifteen defenders add targets for destroy effects and nothing else; every winning action was performed by two defenders or from home.
- **C-T6.** The bot saves the Province but loses an attacker; the human kept the attacker and won next turn.
- **Lobbying (A-T11, C-T5, C-T7).** The bot lobbies with a unit the human needed as attacker or home defender.
- **Bot probably right:** the optional draws (A-T5, T6). Not measurable either way: the earlier permanent buff (A-T10 to T12) and the row refreshes (A-T1, T7).

## Verdict on the hypothesis

Supported: the neutralise-then-attack assessment (rule 1), defence by sending home and bowing rather than by Force (rules 10-12), and one assessment driving spending (rules 5, 13, 14). Partly contradicted: "can I still defend after attacking" is not a hard constraint; he attacked with his only Personality (A-T9) and all-in (C-T5), losing a Province once. Untestable here: anything about Cavalry.
