# Conan strategy campaign, step 3: family B (Cavalry forcing) reading the Read (issue #1206, from 2026-11-14)

Branch `claude/1206-conan-family-b`, from `main` after family E's PR (64fc249b). Blocks: Mac range from 10500 (shadow on seed 10500; the screen and its A/A on 10600). Family C's sequential test uses 10400-10403.

## Rule card (above the line)

Rule `readB`, opt-in, off by default. Fires at the seat's own Infantry and Cavalry declarations when it has at least one Cavalry unit at home that can attack (a unit is Cavalry when the Personality and every Follower on it carry the keyword, as the ledger reads it); a seat without Cavalry is left to the Contender. It rests on RD section 2 (attacker Infantry, defender Infantry, attacker Cavalry, defender Cavalry: the defender places its non-Cavalry units before it sees where the Cavalry goes, and at a battlefield with no defending unit it has no presence) and on AB Q5 (the Contender already holds 80% of its Cavalry for the Cavalry segment; Cavalry-segment attackers reach an undefended battlefield 57% of the time, and 32% of those arrivals do not take the Province).

```
C = my Cavalry units able to attack, I = my infantry able to attack (unbowed, free, 2 Force or more)
Infantry declaration:
  options  = nothing | the Contender's own infantry plan | a probe (the smallest infantry group exceeding the weakest Strength undefended) | all infantry at the weakest Province
  replies  = their infantry's placements against the option (nobody, everybody at each attacked Province, the dealt split: Threat.Defences); with nothing declared there is no battlefield to defend
  follow-ups(option, reply) = all C at the weakest uncovered Province | the sufficient part of C there (the rest home) | C split across uncovered Provinces, each group exceeding Strength alone | all C reinforcing each attacked battlefield | C home
  value(attack, defence)  = expected Provinces (the calibrated cell per battlefield) + 0.3 (kills - losses) - weight(posture) (exposure(home left, their survivors) - exposure(hold))
  each follow-up is valued under their Cavalry reply (their unbowed home Cavalry to whichever battlefield hurts most, or home): the minimum
  forced(option) = min over replies of max over follow-ups
  declare the option with the highest forced value; when none clears the hold by 0.05, declare nothing (the Cavalry may still go alone in its segment)
Cavalry declaration (the infantry already at battlefields is fixed, their placed defence is visible):
  the follow-up with the highest value against the defence as placed, under their Cavalry reply; groups sent to an uncovered Province exceed its Strength alone; the rest stays home
```

**Should move** (RULE_FAMILIES.md B): Province destroyed when Cavalry-segment attackers arrive undefended (AB Q5b, mirror 68%); Provinces per attack for a side with two or more Cavalry units (Q5c); Cavalry held for the Cavalry segment (Q5a, mirror 80%); and family C's Q2a (a Province lost after an all-in), since Cavalry the fork does not need stays home. **Predicted fire rate**: the fork is read at every declaration of a seat with Cavalry; six of the fourteen decks carry 11-14% Cavalry Personalities and one (Unicorn Maidens) is all Cavalry, so it fires in a minority of games and several times in those.

## Decision tests

`tests/L5R.AI.Tests/ForkTests.cs` (`--filter fork`, 6 cases): no Cavalry, no fork; the probe is declared when the Cavalry can punish the defence that answers it (my 6 probes, their 6 defends and 15 beats 10 and kills it, or the Province falls), and nothing is declared when the probe would die (their 12), the Cavalry going alone to the Province no infantry can defend; the sufficient Cavalry (the 5 alone exceeds 4; the 4 stays home against their straightened 9); the Cavalry reinforces a battlefield its infantry is losing when that razes; the Cavalry does not take an uncovered Province their own Cavalry can still reach. The fork lives in `src/L5R.AI/Read/Fork.cs` (`Fork.AtInfantry(read, prepared, posture)` and `Fork.AtCavalry(read, posture)` → `ForkPlan`), hooked in `Prepare`'s attack path after `readA` (nothing → pass when offered), with a shadow log (`BotContext.ForkLog`, arena section "challenger fork").

The decision set has one family B position (`B-G1T6-cavalry`, Mendon's Utaku Ryoko alone to a Strength 7 Province against two unbowed defenders, which the Read sees never razing); T2 sends two units elsewhere, the fork sends nobody; neither agrees with Mendon, whose reason is not in the game (RULE_FAMILIES.md B lists his Cavalry habits as open). On the whole set (`L5R_POS_ARMS="t2;readB:+read,readB"`) `readB` agrees on 9 of 50, as T2 does, changing only that position. `L5R_POS_READ=1` now prints the fork's reading at declarations.

## Shadow (T2 with the Read against T2, seed 10500, 364 games)

**Round 1** (`shB_105`, 364 games, no game stuck; the block ran beside three Conan arms, so its 12.6 s a game says nothing about the fork's cost): the fork was read at 360 declarations of seats with Cavalry; 169 agree with the Contender (116 infantry, 53 Cavalry) and 191 differ (64 infantry, 127 Cavalry: 53%). Of the differences, 44 keep the Cavalry home at the Cavalry declaration where the Contender adds it, 31 send all the Cavalry to the weakest uncovered Province, 26 only the sufficient part of it, 12 a split, 13 reinforce a battlefield; at the Infantry declaration 33 declare nothing where the Contender attacks (the Cavalry to follow alone), 11 a probe, 17 the Contender's own plan with a different follow-up. Ten drawn by seed 105 (`sample_fork.py`), judged on the position as read:

| # | Game, turn | Contender | Fork | Judged |
|---|---|---|---|---|
| 1 | Lion preset v Scorpion 106000005 T13 infantry | 4 to P3 | nothing (+0.14): the prepared attack reads −2.02 against their two defenders | right on the read: the attack dies if answered |
| 2 | Unicorn v Pinned Lion 106000127 T10 Cavalry | both Mai to P2 | one Mai (+0.16 against −0.44): the other stays home | right: the second buys nothing and the home needs her |
| 3 | Classic Lion v Scottish Lion 106000163 T16 Cavalry | Natsu joins P2 | Natsu alone to the uncovered P3 (−0.40 against −0.50) | wrong: Natsu does not exceed P3's Strength; the all-Cavalry follow-up was not sized |
| 4 | Lion preset v Lion Legacy 106000009 T12 Cavalry | Natsu joins three at P4 | home (+1.31 against +1.17): the group razes without her | right |
| 5 | Unicorn v Crane preset 106000118 T6 Cavalry | Lishan alone to P1 | home (0 against −0.30) | right: a lone Cavalry unit that does not raze |
| 6 | Phoenix v Classic Crane 106500077 T15 infantry | 1 + 2 + 1 across three Provinces | nothing (+0.10): every option reads negative under their best reply | right on the read; the Contender's spread won the game |
| 7 | Unicorn v Crane preset 106000118 T10 Cavalry | two to P2 | home: their Cavalry reply kills (−0.53) | right |
| 8 | Unicorn v Classic Crane 106000129 T8 Cavalry | Tsukiko to P3, Ryoko to P4 | Tsukiko alone to P3 (+1.49); Ryoko does not exceed P4's Strength, so no split | right by the sizing rule |
| 9 | Lion preset v Crane preset 106000000 T13 Cavalry | Natsu joins three at undefended P4 | home (equal value): the group razes already | right |
| 10 | Unicorn v Pinned Lion 106000127 T6 Cavalry | Mai to the defended P1 | Mai to the uncovered P2 (+0.32) | right |

9 of 10. The fault: the follow-up "all the Cavalry at the weakest uncovered Province" is now offered only when the Cavalry's Force exceeds that Strength (the rule card's sizing); the sufficient part and the split were already sized. Round 2 is the rerun after that fix.

**Round 2** (`shB2_105`, after the sizing fix): 173 agree, 187 differ (62 keep the Cavalry home where the Contender adds it, 26 the sufficient part, 12 a split, 10 all the Cavalry at the weakest uncovered, 13 reinforce; 32 declare nothing at the Infantry declaration, 9 a probe). Ten drawn by seed 205: Unicorn v Classic Lion T10 (one Tsukiko to the uncovered P2, two home: right), Scorpion v Crane preset T15 (**their last Province**: the fork probes with one unit at +3.16 where the Contender sends seven: wrong, the last turn sends everything, PM 3.2), Lion preset v Dragon T13 (**their last Province**, two razing it undefended; Natsu home at +3.16 against +3.15 reinforcing: wrong, the same), Classic Crane v Scottish Lion T12 (nothing, every option negative under their best reply: right on the read), Crab v Unicorn T11 (a split P3 and P4 keeping two home where the Contender spreads five over three Provinces: right on the read), Phoenix v Unicorn T6 (a lone Cavalry unit below Strength stays home: right), Unicorn v Classic Crane T8 (Tsukiko alone, Ryoko below P4's Strength stays: right), Unicorn v Pinned Lion T10 (one Mai: right), Unicorn v Scorpion T7 (Lishan alone to the uncovered P3 rather than one unit into a defended P2 and one to P3: right), Lion preset v Lion Legacy T10 (Natsu home, both groups raze: right). **8 of 10**; the fault: at their last Province the fork paid the counter-attack's exposure and let it tie against sending more. Now, when their last Province falls at likely, no exposure is paid, otherwise it is weighted by the chance the Province survives, and 0.01 a point of Force sent breaks ties toward sending everything (the seventh fork test: the 4 joins the 7 at their last Province). Round 3 is the rerun.

**Round 3** (`shB3_105`, after the last-Province fix): 196 agree, 164 differ (46%; 47 keep the Cavalry home, 24 the sufficient part, 12 a split, 10 all the Cavalry at the weakest uncovered, 13 reinforce; 24 declare nothing at the Infantry declaration, 8 a probe, 21 the Contender's plan with the Cavalry held for its segment). Ten drawn by seed 305: Unicorn v Crane Dominance T12 (Chikako alone to the uncovered P2, the lone unit into the defended P4 dropped: right), Lion Legacy v Classic Lion T17 (Natsu reinforces the contested P1 at +1.49 rather than the won P2: right), Unicorn v Crane preset T8 (home: their Cavalry reply kills at P2: right), Unicorn v Scorpion T7 (Lishan alone: right), Classic Crane v Lion preset T10 (Atsumichi reinforces Tsunehiko's battle at +1.26 rather than a Province alone at +0.81: right), Lion preset v Scorpion T13 (nothing where four would die to two defenders: right on the read), Classic Lion v Crab T15 at their last Province (the same attack, the Cavalry held for its segment: equivalent), Classic Crane v Unicorn T11 (a one-unit probe at +0.37 where the Contender sends eight against an all-Cavalry home at +0.08: arguable, the exposure of eight bodies away against Cavalry is the reason), Unicorn v Classic Lion T10 (one Tsukiko: right), Lion Legacy v Pinned Lion T16 (the same attack with the Cavalry held: equivalent). **9 of 10** with one arguable. The fork passes the shadow gate (9, 8, 9 of 10 across the three rounds) and goes to the screen on block 10600.

## Screen (block 10600, T2 + read + readB against T2, 1,456 games, battles and fires on)

A/A `ca106` (T2 v T2): **49.3%**. Arm `rB106`: **50.3%** (733 of 1,456, Wilson 47.8-52.9); against the A/A on the same games **+38 gained, −23 lost, net +15, two-sided p .072** (one-sided .036): a weak positive, a third of family C's +28. `readB` is consulted 4.0 times a game and fires 0.59 times a game (14.8 fires per 100 consultations) in 265 of 1,456 games: the seats with Cavalry. The block ran beside three Conan arms, so its seconds a game (14.3 against the A/A's 13.6) are only a rough cost.

Per deck (gained / lost): Unicorn Maidens **+17 / −5**, Pinned Lion +6 / −4, Crane preset +5 / −0, Phoenix Talons +4 / −1, Classic Crane +1 / −1, Classic Lion +3 / −6, Lion preset +2 / −6; the seven decks without Cavalry 0 / 0 (the rule never fires there). The all-Cavalry deck gains; the two Lion decks whose one Cavalry unit (Ikoma Natsu) the fork keeps home lose.

Measures (`q5_cavalry.py` and the profile on the arm's and the A/A's logs; Conan = T2 + readB, mirror = the A/A):

| Measure | T2 + readB | T2 against it | T2 mirror | Good | Table |
|---|---|---|---|---|---|
| Province destroyed when Cavalry-segment attackers arrive undefended | **77%** | 69% | 68% | higher | Q5b |
| Cavalry-segment arrivals at a battlefield with no defender | 71% | 55% | 57% | | Q5b |
| Unicorn Maidens: attack rate (turns with unbowed Personalities) | 51% | 81% | 81% | | Q5d |
| Unicorn Maidens: Provinces per attack | **0.89** | 0.73 | 0.71 | higher | Q5d |
| Unicorn Maidens: games won | 73% | 66% | 63% | | Q5d |
| Cavalry Personalities held for the Cavalry segment | 67% | 80% | 78% | higher | Q5a |
| Attacks that lose a Personality | 20% | 23% | 21% | lower | Q1c |
| Attacks that destroy a Province | 61% | 59% | 60% | higher | Q1c |
| A Province lost on the opponent's turn after an all-in attack | 37% | 37% | 38% | lower | Q2a |

The fork's own measure moves: Cavalry arriving undefended takes the Province nine points more often, and the all-Cavalry deck attacks much less and takes more each time. Q5a falls because the fork declares fewer Cavalry attacks altogether (the share is of Cavalry Personalities sent), and because a Cavalry Personality carrying a non-Cavalry Follower is infantry to the ledger (the rules) but Cavalry to the table (the printed keyword).

**Formulation 2**, `rbCav` (the fork decides only the Cavalry declaration; the Contender's own infantry plan stands), screens next on the same block: the per-deck table says the Infantry-declaration "nothing" costs the Lion decks, and the Cavalry-declaration sizing is where the measure moves.

## Screen, formulation 2 (block 10600, T2 + read + readB + rbCav against T2, 1,456 games)

Arm `rB2_106`: **50.3%** (732 of 1,456); against the A/A **+28 gained, −14 lost, net +14, two-sided p .044** (one-sided .022). Fewer games change (42 against 61) for the same net: the Infantry-declaration "nothing" was where formulation 1 both gained and lost. `readB` is consulted 3.9 times a game and fires 0.35 times (in 225 games); `rbCav` fires 0.15 (it masks the fork at 127 games' Infantry declarations). Per deck: Unicorn Maidens **+17 / −5** again, Crane preset +4 / −2, Classic Lion +2 / −1, Pinned Lion +1 / −1, Lion preset +1 / −3, Phoenix Talons +1 / −2, the seven decks without Cavalry 0 / 0. Measures: Cavalry arriving undefended takes the Province 77% (A/A 68%); Unicorn Maidens attacks 51% of its turns against 81%, 0.89 Provinces an attack against 0.71, wins 73% against 63%; Cavalry Personalities held for the Cavalry segment 67%.

**Family B screens positive in both formulations** (+15 and +14 in 1,456), the second cleaner. Its gain is one deck's: the all-Cavalry Unicorn Maidens, which the fork plays as a fork (attack half as often, take more). Per the brief it goes to the sequential test as Hard + `read,readB,rbCav` against Hard on block 10700, run as fair pairs (both arms together under the same load; see family C's note on `lkT`). Family C's test under Hard found no gain where the Contender-speed screen found +28, and the same may hold here: the lookahead already chooses among shapes by playing on.

## Sequential test (Hard + read,readB,rbCav against Hard, both v T2, block 10700 upward)

Hard is Conan-best (`BotContext.Hard`: T2 + lkDef, lkVar, lkPlan + look); the arm is the `conan` arm with `L5R_C2="t2,lkDef,lkVar,lkPlan,read,readB,rbCav"`. Chunks of one seed (364 games), both arms of a chunk run together under the same load (`lkT` is a wall-clock limit; see family C's note), 10-minute budget, no fire counting; analysed at chunk boundaries with `tools/bots/sprt.py` (p1 0.6, bounds ±2.94), hard cap 1,456 games (CONAN_CAMPAIGN_BRIEF.md section 4, Tier 2).

| Chunk | Seed | Hard v T2 | Hard + readB, rbCav v T2 | Paired | Gained | Lost | LLR | Verdict |
|---|---|---|---|---|---|---|---|---|
| 1 | 10700 | 67.0% (244/364) | 67.8% (246/363) | 363 | 4 | 1 | +0.51 | continue |
| 2 | 10701 | 69.5% (253/364) | 69.8% (254/364) | 364 | 3 | 2 | +0.10 (chunk alone) | continue |
| 3 | 10702 | 71.2% (259/364) | 70.8% (257/363) | 363 | 3 | 4 | −0.35 (chunk alone) | continue |
| 1 + 2 + 3 | | | | 1,090 | 10 | 7 | +0.26 | continue |
