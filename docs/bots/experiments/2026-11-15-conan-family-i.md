# Conan strategy campaign, step 3: family I (the Favor, the opponent's turn and the hand) reading the Read (issue #1206, from 2026-11-15)

Branch `claude/1206-conan-family-i`, from `main` after family G's PR. Blocks: Mac range from 11200 (shadow on seed 11200; the screen and its A/A on 11300).

## Rule card (above the line)

Rule `readI`, opt-in, off by default: **the hand and the Favor**. Three decisions, each read from the Read's descriptors and exchange, never from a card's name. It rests on PM 3.7 (hold the Favor: spent on defence when level or behind, never to cycle a card; four declined cycles; keep the hand when it holds three or more removal or movement cards: G3 T5, G7 T7, G4 T3; at the hand limit discard the card with the fewest live uses in this matchup, keeping one-shot cards that scale with their investment: G5 T12 to T24, G6 T5) and PM 7 rows 7 and 11 (the Favor spent on a send-home in a battle already won, G7 T7, G6 T11, G1 T9, GT T7: two or three Personalities survive and the Favor is gone; "discard hand, draw four" taken where the cards would have won the following battles).

```
hand answers = the hand's cards with a Battle descriptor that neutralises (send home, bow, destroy, Force cut, lock, duel) or moves a unit (the Read's HandAnswers)
1  the Favor in a battle (favor-battle offered):
     IF the exchange at this battlefield without the Favor already wins for me at likely (the Province holds, or falls, as I want it)   -> keep it
     IF defending AND the exchange without it loses the Province at likely AND with it (one attacker home) holds                      -> spend it
     IF defending my last Province AND it holds at likely but falls under their worst hand AND with it holds under that hand too       -> spend it
     IF attacking                                                                                                                       -> keep it (it moves an attacker home, with presence, on defence)
     never on the Favor's card cycle
2  an Event that replaces the hand (a yes/no choice whose text discards the hand and draws):
     IF the hand holds three or more answers                                                                                            -> no
     ELSE                                                                                                                               -> the Contender decides
3  the hand-limit discard: for each card, live uses = how many of their units in play (straightened) its conditions admit as a target now, or 1 for a card without a target;
     discard the fewest live uses first; among equals the Contender's own order; a card whose condition is their investment (attachments, Followers, cost) is kept while they can still invest
```

**Should move** (RULE_FAMILIES.md I): the Favor spent on a send-home in a battle already won (should reach zero); Gold the opponent spends per turn against decks with repeatable Holding-bow effects (not addressed in this formulation: their-turn disruption is the Contender's `openbow`); agreement on PM 7 rows 7 and 11 (E-G1T9-favor, I-G3T5 and I-G7T7 keep-hand, the four G5 discards). **Predicted fire rate**: the Favor decision is read whenever favor-battle is offered (a few times a game for the Favor holder); the hand Event is rare (one card in the pool among the fourteen decks); the hand-limit discard about once a game. Perhaps 0.5-1.5 fires a game.

## Decision tests

`HandTests.cs` (`--filter hand`, 7 cases): the Favor kept while the Province holds without it, spent when the attacker home turns the fall into a hold, kept when it changes nothing and spent at my last Province, kept when attacking; three answers keep the hand, two leave it to the Contender, the text test; the hand-limit discard by live uses (a destroy for Force 2 or less with no target goes first); a condition against the performer judged with my best unit.

**The decision set** (`L5R_POS_ARMS="t2;ri:+read,readI" L5R_POS_FAMILY=I`, with `L5R_POS_READ=1` printing `hand:` at Event and hand-limit positions): T2 agrees at none of the seven (0 of 7). With `readI`: the two "keep the hand" positions (A Dangerous Game, four answers in hand each) agree once the hand-replacing choice is recognised from the pending prompt (the Event card is in no zone the bot's card map lists while it resolves); E-G1T9-favor keeps the Favor as Mendon did but then plays My Enemy's Mercy where he passed (a family E decision; the Favor part is right); the four G5 hand-limit discards still differ: Mendon discarded Remember Your Ancestors (destroys an attacker; the Lion rarely attacked him), Incriminating Testimony and Sheathing the Sword as situational and kept Dismissing the Cur to T24 and Rocky Terrain throughout; the Read counts live targets now (their units straightened) and cannot see that a card needing an attacking target is dead against an opponent who does not attack, so it ranks Rocky Terrain (one use by default) and the Cur (its performer condition, now judged with my best unit) lowest. **2 of 7**, with the Favor right at a third. A first version counted a card with a performer-relative condition (Honor at most the performer's) as having no live use because the matcher got no performer; it now takes the best of my units.

## Shadow (T2 with the Read against T2, seed 11200, 364 games)

**Round 1** (`shI_112`, before the two fixes above; 5:46 wall, 8.5 s a game: the shadow runs the exchange at every Favor offer): the Favor was offered 204 times to the challenger; the Read differed at 161 (0.44 a game), all of them "the Contender spends, the Read keeps" (156 defending: the Province holds at likely without it; 5 attacking). The hand-limit discard was read 182 times and differed at 96 (0.26 a game), every difference a card with no live target against their units in play where the Contender discarded by its fixed card values. No hand-replacing Event is in the fourteen decks (0 decisions). Round 2 is the rerun with the fixes; ten of each kind are judged there.

**Round 2** (`shI2_112`, with the two fixes): the Favor differs at 161 of 204 offers (0.44 a game), the discard at 84 of 182 (0.23 a game). Five of each drawn by seed 212:

| # | Position | Contender | Read | Verdict |
|---|---|---|---|---|
| F1 | Scottish Lion v Scorpion T12, level, their 5 against an empty defence at Strength 5 | spend | keep: holds at likely (5 does not exceed 5) | right by the model, on a razor's edge |
| F2 | Scorpion v Spider T10, P4v1, every attacker neutralised in the projection | spend | keep | right |
| F3 | Phoenix v Crane T16, my Honor 49 | spend | keep | equivalent: the Honor win is at my next turn start |
| F4 | Phoenix v Classic Lion T15, 15 against 12 | spend | keep | right |
| F5 | Crane Dominance v Dragon T18, **my last Province**, holds at likely | spend | keep | unclear: PM 3.7 spends in the battle it decides; the Favor now goes at my last Province when their worst hand would take it |
| D1 | Dragon v Classic Crane T4 | Superior Reach | Moving and Unmoving (no live target) | right by the policy |
| D2 | Crab Control v Scottish Lion T8 | Sheathing the Sword | Dismissing the Cur (no target: every Lion outranks my performers in Honor) | right |
| D3 | Crab Control v Scottish Lion T6 | Black Marketeering (a destroy with six targets) | Dismissing the Cur | right |
| D4 | Classic Crane v Crab T5 | Pure Strike Blade | Block Supply Lines (no live target) | right |
| D5 | Unicorn v Crane Dominance T6, two cards | My Enemy's Mercy, A Paragon's Strength | Pure Intent, Preserving Beauty (one use each) | unclear |

**8 of 10 right or equivalent, 0 wrong, 2 unclear.** The gate is met; the Favor's deciding-battle line is added for F5 and round 3 is the rerun.

**Round 3** (`shI3_112`, with the deciding-battle line): the Favor differs at 157 of 204 offers (0.43 a game), the discard at 84 of 182 (0.23 a game); the deciding-battle line fired at no offer in the block (the last-Province offers it saw already held under their worst hand). Five of each drawn by seed 312:

| # | Position | Contender | Read | Verdict |
|---|---|---|---|---|
| F1 | Crab Control v Crane Dominance T13, my last Province, every attacker neutralised in the projection (4 v 0) | spend | keep | right by the Read (holds under their worst hand too) |
| F2 | Crab Control v Phoenix T17, 6 v 0 | spend | keep | right |
| F3 | Crab Control v Dragon T11, 0 v 0 | spend | keep | right |
| F4 | Crab Control v Scorpion T12, 2 v 0 | spend | keep | right |
| F5 | Crab Control v Crane Dominance T11, P2v4, 4 v 0 | spend | keep | right |
| D1 | Phoenix v Crab T5 | The Mountain's Power | The Earth's Strength (one use) | equivalent |
| D2 | Dragon v Classic Crane T8 | Blade of Perfection | Engulfing Flames (no live target) | right by the policy |
| D3 | Lion Legacy v Crane T3 | Focus | Creating Order (no live target) | right |
| D4 | Crab Control v Scottish Lion T26, the opponent at −21 Honor | Rocky Terrain | Disgraceful Conduct (no target) | equivalent (the game is decided) |
| D5 | Crane Dominance v Scottish Lion T8 | Reckless Duel | Of One Instant (one use) | unclear |

**7 right, 2 equivalent, 1 unclear, 0 wrong.** The rule passes the shadow gate (8, then 9 of 10 counting equivalents) and fires 0.66 times a game, inside the card's prediction. One caveat the screen must judge: every Favor keep rests on the exchange's projection that my hand's answers neutralise the attackers ("0 v 0"); the Crab Control deck, whose hand is all answers, supplies most of them. The screen runs on block 11300.

## Screen (block 11300, T2 + read + readI against T2, 1,456 games, fire counts on)

A/A `ca113`: 48.7% (709 of 1,456). Arm `rI113`: **48.6%** (707 of 1,456); against the A/A **+48 gained, −50 lost, net −2, two-sided p .92**: null. `readI` is consulted at every decision and fires 3.41 times a game in 620 games (the Favor's cycle held, the Favor's Battle action held, the hand-limit discard's order). Per deck: Crab Control **+11 / −7** (the deck whose hand is all answers, where the Favor keeps concentrate), Phoenix Talons +5 / −0, Dragon Destruction +5 / −3, Spider Conquer +4 / −2; Scottish Lion **+4 / −9**, Classic Lion +1 / −5, Lion Legacy +4 / −6, Unicorn Maidens +1 / −3; the rest within one or two games.

**Formulation 2**, `riHand`: the hand decides and the Favor stays the Contender's (its Battle action spent as the Contender spends it, 300 plus the target's Force when defending). The three parts of formulation 1 pull in different directions by deck (the Favor keeps help Crab Control and hurt the Lion decks whose attackers the Contender's Favor would have sent home), so the second formulation isolates the hand-limit discard by live uses and the hand-replacing Event, the two parts the shadow judged right at every draw. Screens next on the same block.

## Screen, formulation 2 (block 11300, T2 + read + readI + riHand against T2, 1,456 games)

Arm `rI2_113`: **49.5%** (720 of 1,456); against the A/A **+23 gained, −12 lost, net +11, two-sided p .09** (one-sided .045). The hand-limit discard by live uses fires 0.25 times a game in 266 games; `riHand` is consulted at every decision and silences the Favor 0.91 times a game. Per deck: Crab Control +5 / −2, Dragon Destruction +5 / −2, Spider Conquer +4 / −2, Phoenix Talons +3 / −0, Unicorn Maidens +2 / −0, Crane Dominance +3 / −3, Lion Legacy +0 / −2; six decks unchanged (0 / 0). Against formulation 1's −2, the Favor's keeps cost about 13 games net: the exchange's "every attacker neutralised" projection is optimistic where the Contender's Favor would have sent the biggest attacker home for nothing.

**Family I screens positive in its second formulation** (the hand only). Per the brief it goes to the sequential test as Hard + `read,readI,riHand` against Hard on block 11600, run as fair pairs (both arms together, 10-minute budget). Unlike families C and B, nothing in the lookahead touches the hand-limit discard (`lkBuy` is for purchases, the shapes for attacks), so the Contender-speed gain should carry over if it is real.

## Sequential test (Hard + read,readI,riHand against Hard, both v T2, block 11600 upward)

Hard is Conan-best (T2 + lkDef, lkVar, lkPlan + look); the arm is the `conan` arm with `L5R_C2="t2,lkDef,lkVar,lkPlan,read,readI,riHand"`. Chunks of one seed (364 games), both arms together under the same load, 10-minute budget, no fire counting, one build for every chunk; `tools/bots/sprt.py` (p1 0.6, bounds ±2.94), cap 1,456.

| Chunk | Seed | Hard v T2 | Hard + hand v T2 | Paired | Gained | Lost | LLR | Verdict |
|---|---|---|---|---|---|---|---|---|
| 1 | 11600 | 73.6% (268/364) | 73.9% (269/364) | 364 | 8 | 7 | −0.10 | continue |
| 2 | 11601 | 68.1% (248/364) | 67.9% (247/364) | 364 | 5 | 6 | −0.43 (chunk alone) | continue |
| 1 + 2 | | | | 728 | 13 | 13 | −0.53 | continue |
| 3 | 11602 | 70.0% (254/363) | 70.0% (254/363) | 363 | 8 | 8 | −0.33 (chunk alone) | continue |
| 1 + 2 + 3 | | | | 1,091 | 21 | 21 | −0.86 | continue |
| 4 | 11603 | 70.1% (255/364) | 68.7% (250/364) | 364 | 9 | 14 | −1.48 (chunk alone) | continue |
| 1 to 4 | | | | 1,455 | 30 | 35 | −2.34 | **cap: no decision** |

**Verdict: null under Hard.** The test reached the 1,456-game cap with 30 games gained and 35 lost (net −5, LLR −2.34, short of the −2.94 bound but leaning to no gain). The +11 at Contender speed does not carry over to Hard: whatever the live-use discard fixes, the Hard bot's own play already covers it or the discard is too rare to matter. `readI`/`riHand` stay opt-in and off by default; nothing changes on main's defaults.

What follows: formulation 3 (branch `claude/1206-conan-family-i3`) replaces the live-use count with a value read from the board (defence and attack deltas from the Read, development in quiet turns, Honor for opponent-choice cards read as their cheaper branch, PLAYER_MODEL.md 3.7). It is screened on a fresh block before any sequential test.

## Formulation 3, the discard as a read of the board (`riBoard`; Mendon, 2026-11-15)

Mendon on the discard: "I read the board for likely board states and make discards based on utility ... the value of cards in hand shifts based on the board state ... an assessed probability that that card will shift the outcome of a pivotal encounter" (PLAYER_MODEL.md 3.7). `Hand.DiscardByRead`: a card's worth in Provinces is the rise in the chance of losing a Province next turn without it (the threat read again with the hand less the card, `Calibration.PLose` x `ProvinceWorth`), plus the fall in next turn's best attack without it, plus, for a Follower or Item, its Force at .05 Province a point times the chance home holds; the hand's own Force cuts apply before a card's targets are counted; the least worth goes first. Tests: under threat the bow is kept and the Follower goes; quiet, the destroy goes and the Follower stays; a Force cut in hand makes a Force-2-or-less destroy live.

**Rocky Terrain** (Mendon: "it bows before resolution but after all other actions are played ... often devastating for an attacker that has attacked with only a few personalities"): the Read did not count it at all. Three descriptor fixes, card-agnostic: before-resolution abilities are battle answers (`IsBattle`); an untargeted ability that names "attacking (or defending) Personalities" targets the enemy's Personalities with that condition; "one or two" is two targets (`EffectDescriptor.Targets`), and the ledger adds one hand answer per target. The exchange now holds a Strength 5 Province with 3 Force against 6 + 5 when the terrain bows both attackers (a test). Effect-descriptor 11/11 and descriptor-audit 1/1 still pass. **This changes the Read** for every rule under `read` where a hand holds such a card; the shadow and the calibration tables see it from this branch on.

**The decision set after both changes**: Rocky Terrain is kept at all four G5 discards (the live-use discard had thrown it away at three). Agreement is still 2 of 7: at T12 both formulations discard Dismissing the Cur and The Heir's Wrath where Mendon discarded Remember Your Ancestors and Incriminating Testimony; at T20 to T24 the next card out is Inexplicable Challenge, whose target the descriptor reads as an attachment (no live target): a descriptor fault to fix before the screen.

**Inexplicable Challenge** ("target an enemy card without attachments: Bow it") was read as a bow of an attachment: the slot reader took the word "attachments" in the condition for the target. A "with" or "without attachments" now names a condition only, and "card" or "unit" makes the target a unit card (a test). With it, the G5 discards: both formulations now drop Dismissing the Cur at T12 to T22 (its target must have Personal Honor at most the performer's, and every Lion outranks the Crab Courtiers) and Inexplicable Challenge at T24; Mendon dropped Remember Your Ancestors, Incriminating Testimony, Sheathing the Sword and, at T24, the Cur. Still 2 of 7. In the drain game the threat to home was low throughout, so the board read gives most cards no worth either way and the live count decides; Mendon's "situational" reading (a destroy that needs an attacking target is dead against an opponent who does not attack) is the next thing the board read lacks: it should weigh a defensive answer by how likely the opponent is to attack at all, which the threat's cell already says.

**The situational weighting.** A residual worth now orders the cards the threat and the attack leave tied: .02 Province a live target (up to three), times the number of attackers the card stops (Rocky Terrain two), and, for a card that can touch only attackers, times the chance they attack at all (1.7 times the chance they take a Province, from the refit's threat records: attacked in 50% of turns, a Province lost in 29%). A test: against an opponent unlikely to attack, a destroy that needs an attacking target goes before a send-home with the same targets. The G5 discards: **T20 agrees** (Remember Your Ancestors), T12 half (Remember Your Ancestors with the Cur, where Mendon paired it with Incriminating Testimony), T22 and T24 still drop Rocky Terrain (Mendon: Sheathing the Sword, then the Cur). **3 of 7.** Rocky Terrain loses on the same weighting that drops Remember Your Ancestors: in Mendon's reading it is worth keeping against a rare attack because it is devastating when one comes; the residual counts its two targets but not that it resolves after every answer.
