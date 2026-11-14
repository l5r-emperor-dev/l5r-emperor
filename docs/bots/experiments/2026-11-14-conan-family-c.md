# Conan strategy campaign, step 3: family C (home and the counter-attack) reading the Read (issue #1206, from 2026-11-14)

Branch `claude/1206-conan-family-c`, from `main` after family E's PR. Blocks: Mac range from 10200 (shadow on seed 10200; the screen and its A/A on 10300).

## Rule card (above the line)

Rule `readC`, opt-in, off by default. Fires at the seat's own infantry and Cavalry declarations, after the Contender's other rules have shaped the attack (it reads the prepared assignment and may thin it). It rests on PM 3.3 (a Province is a price he pays while ahead; he concedes rather than lose units or cards; units that stay home must count; his standard for Conan: keep the Province, better the defenders too, best destroy the counter-attackers) and AB section 3 (guard Force against the opponent's unbowed Force predicts the next turn better than anything else measured: under 25% the bot is attacked 93% of the time and loses a Province 66%; at 100% or more, 26% and 5%; but an all-in attack wins as often as a partial one, so the gain is small unless the attack itself is kept).

```
A        = the attack as the Contender prepared it (groups per Province)
survive  = their units the Read's exchange expects to survive A (per group, under their likely hand, against their best reply)
T        = threat(home without A's units, their survivors straightened)          (Threat.Of)
IF T.likely loses no Province                                   -> A stands
ELSE accept the loss WHEN
     posture is Race (their fastest clock is within a turn and mine is shorter)                       -> A stands
  OR after the loss I stay ahead or level on Provinces AND keep two or more, AND every remedy below breaks A -> A stands
ELSE remedy, cheapest first, each checked with T recomputed:
  1. keep back the weakest unit of the group that still razes without it (the group's exchange at likely still Falls)   (repeat)
  2. keep back the whole group that contributes least to the attack (its expected Provinces), the others stand   (repeat)
  3. nobody attacks (the hold), when only the whole army at home holds and the hold itself holds
```

The Cavalry declaration applies the same reading to the Cavalry about to be added, with the infantry already committed fixed.

**Should move** (RULE_FAMILIES.md C): Provinces lost on the turn after an attack (profile Q2a: A/A about 39% after an all-in); attackers destroyed while defending (Q3b); net Province swing per attack turn. **Predicted fire rate**: the threat is read at every declaration; it changes the assignment where the Contender's attack leaves home short, which AB Q2c says is common (guard under 25% in a large share of attack turns): several times a game, fewer than readE's eight.

## Decision tests

`tests/L5R.AI.Tests/GuardTests.cs`: the attack stands when home holds; a unit is kept back when it alone turns the threat; the whole army stays home when nothing less holds and the hold holds; the loss is accepted in a race, and when ahead with two Provinces to spare; a lethal attack is never thinned.

`tests/L5R.AI.Tests/GuardTests.cs` (`--filter guard`, 6 cases) is written and passes; the Guard lives in `src/L5R.AI/Read/Guard.cs` (`Guard.Of(read, assignment, posture, lethal)` → `GuardPlan`), hooked at the end of `Prepare` after the paper `guard` rule, with a shadow log (`BotContext.GuardLog`).

The decision set has no family C positions (Mendon's games in `PLAYER_MODEL.md` section 7 yield none). On the whole set (`L5R_POS_ARMS="t2;readC:+read,readC"`) `readC` agrees with Mendon on 9 of 50, as T2 does, and flips none that T2 had right; it changes one position, A G6 T11 (Mendon sent Dairuko alone; T2 sent three units; the Guard holds everything, reading that the attack leaves home open to the counter-attack), which neither had right.

## Shadow (T2 with the Read against T2, seed 10200, 364 games)

The first run of the block hung in four games (Crane preset v Phoenix Talons 103500017, Crane Dominance v Phoenix Talons 103500043, Scottish Lion v Pinned Lion 103500101, Classic Crane v Classic Lion 103500181: the arena's watchdog named them past two hours). The cause was the Guard's second remedy at a Cavalry declaration: the group kept back was chosen among all groups, including the infantry already at a battlefield, which the declaration cannot recall; dropping such a group removed nothing from the Cavalry assignment, and the loop never ended. The remedy now chooses only among groups with a unit still to be declared, and stops when a drop would change nothing; the sixth guard test is that position (the fixed 8 at their Strength 9, the 6 to go; it must answer within 20 s, and keeps the 6 home). Replayed alone (`L5R_TOURN_ONLY`), game 103500017 finishes in 6 s. The block was rerun from scratch.

**Round 1** (the rerun, 364 games, 3:20 wall clock, no game stuck): the Guard was read at 1,340 declarations (919 infantry and 138 Cavalry agree with the Contender; 238 and 45 differ: 283 DIFFERS, 21%). Of the disagreements, 159 hold the whole army (only the army at home holds), 97 keep units back with the groups still razing, 27 keep whole groups back. Ten drawn by seed 102 (`sample_guard.py`), judged on the position as read, not on the game's end:

| # | Game, turn | Contender | Guard | Judged |
|---|---|---|---|---|
| 1 | Dragon v Pinned Lion 103500062 T9, 3v3 | 2 units to P2, the group does not raze; home empty against 23 | nobody: home 9 + Strength 7 holds | right: an attack that razes nothing and loses a Province |
| 2 | Spider v Unicorn 103500112 T11 Cavalry, 4v1 | 2 Cavalry to their last Province, razing | 1 kept back for home | wrong: the attack ends the game; the Guard only knew lethal from the Contender's own lethal plan |
| 3 | Spider v Classic Crane 103000116 T16, 4v1 | 14 units to their last Province | 3 kept back | wrong, the same fault |
| 4 | Unicorn v Crane Dominance 103500120 T9, 4v4 | 3 + 1 to two Provinces, neither group razes; home empty against Lishan | nobody: home 16 holds and kills an attacker | right |
| 5 | Crane preset v Phoenix 103500017 T11 Cavalry, 2v4 | Kojiro (5F) joins a razing group | Kojiro stays: home 5 + 6 holds against 9 | right: behind 2 to 4 the Province matters more than the margin |
| 6 | Lion Legacy v Pinned Lion 103000140 T9, 2v3 | 3 to P2 (razes); home empty, two Provinces lost at likely (the game) | 2 kept back, the group still razes, home holds | right |
| 7 | Unicorn v Crane preset 103500118 T7, 3v4 | 1 unit razes P1; home empty against Chikako 8 | nobody: home 8 + 6 holds | right by PM 3.3 (no Province trade while behind), arguable |
| 8 | Classic Crane v Lion Legacy 103500179 T11, 1v3 | 1 + 2 to two Provinces; my last Province open against 30 | nobody: 20 + 7 holds, five attackers die at likely | right |
| 9 | Phoenix v Lion Legacy 103000074 T18, 1v2 | 3 to P3 (razes); my last Province open against 30 | nobody: 18 + 6 holds | right (the game was won anyway: they did not take it) |
| 10 | Scottish Lion v Crane Dominance 103000094 T11, 4v4 | 4 to P1 (razes) | Uehara (1F) kept back: his presence lets the hand's answers kill the attackers | right, if the hand is as the Read sees it |

8 of 10. The fault behind the two: the Guard now treats an attack that razes every Province they have left at likely as lethal itself (`start.Razed >= them.Provinces.Length`), and the fourth guard test covers it (their last Province at Strength 2 falls to 9 + 3 through their 7; nothing is kept back although the Contender's lethal plan is silent). Round 2 is the rerun after that fix.

**Round 2** (`shC2_102`, after the lethal fix; 364 games, no game stuck): 1,117 declarations agree, 223 differ (17%; 153 hold the whole army, 50 keep units back, 20 whole groups). Ten drawn by seed 202: Lion preset v Spider T5 (a non-razing unit sent while home 14 holds: nobody), Crane preset v Phoenix T9 (two non-razing units, home 12 + 6 holds and kills two: nobody), Spider v Classic Crane T12 (six to P2 razing, two lost at home: Tenbatsu and Miki kept, the group still razes, home holds), Crane Dominance v Classic Crane T12 at 2 v 3 (Yoshitada kept, home holds in every cell), Lion Legacy v Phoenix T13 at 2 v 4 (two razing units sent while both Provinces at home fall: nobody), Unicorn v Crane Dominance T8 Cavalry (two non-razing Cavalry: nobody), Crane Dominance v Phoenix T14 (Yoshitada kept, both groups still raze, home holds in every cell), Scorpion v Classic Crane T12 at 1 v 2 (the last Province open to 24: nobody, 22 + 7 holds), Crane Dominance v Classic Crane T6 (a non-razing unit: nobody), Classic Lion v Dragon T10 at 2 v 3 (two razing units, the Province behind them lost: nobody, 15 + 7 holds). **10 of 10** right on the position as read; the recurring shapes are an attack that razes nothing and leaves a Province to fall, and a razing attack from a seat at one or two Provinces that leaves one of them open.

## Screen (block 10300, T2 + read + readC against T2, 1,456 games, battles and fires on)

A/A `ca103` (T2 v T2): **50.5%**. Arm `rC103`: **52.4%** (763 of 1,456, Wilson 49.8-55.0; sign test p .07); against the A/A on the same games **+83 gained, −55 lost, net +28, p .021** (`compare_tournament.py`). Cost 6.9 s a game against 6.8 (fires on in the arm only). `readC` is consulted 3.8 times a game and fires 0.68 times a game (17.8 fires per 100 consultations), in 598 of 1,456 games; every other T2 rule fires as before.

Per deck (gained / lost): Crane Dominance +19 / −5, Unicorn Maidens +11 / −2, Phoenix Talons +10 / −3, Dragon Destruction +7 / −2, Scorpion Lethargy +6 / −4, Classic Crane +5 / −3, Scottish Lion +4 / −2, Classic Lion +4 / −5, Crab Control +3 / −2, Crane preset +3 / −4, Pinned Lion +3 / −5, Lion preset +3 / −6, Lion Legacy +3 / −7, Spider Conquer +2 / −5. The Guard helps the decks that win by Honor or by holding (Crane Dominance, Unicorn, Phoenix) and costs the Lion Force decks a little, the mirror image of family D's table.

The measures (profile on the arm's and the A/A's logs; Conan = T2 + readC, Contender(vsConan) = its opponent, mirror = the A/A):

| Measure | T2 + readC | T2 against it | T2 mirror | Good | Table |
|---|---|---|---|---|---|
| A Province lost on the opponent's turn after an all-in attack | **34%** | 29% | 38% | lower | Q2a |
| Defended battlefields held with an attacker destroyed | **25%** | 22% | 21% | higher | Q3b |
| The opponent's attacks that destroy a Province | | 55% | 59% | | Q1c |
| The opponent's attacks that lose a Personality | | 27% | 23% | | Q1c |
| Attacks that destroy a Province | 59% | 55% | 59% | higher | Q1c |
| Attacks that achieve nothing | 29% | 31% | 29% | lower | Q1c |
| Gift defences | 19% | 20% | 19% | lower | Q3b |
| Cavalry held for the Cavalry segment | 76% | 83% | 80% | higher | Q5a |

The family's measures move the way the rule card said: fewer Provinces lost after an all-in, more counter-attackers destroyed, and the opponent's attacks achieve less; the seat's own attacks raze as often as before (declared attacks 3.38 a game against 3.24). The Cavalry held for its segment falls four points: the Guard keeps Cavalry home at the Cavalry declaration, which the profile does not count as held.

**Family C screens positive**, the first family to do so. Per the brief's step 3 item 5 it goes to the sequential test as Conan-best (Hard: T2 + lkDef, lkVar, lkPlan + look) plus `read,readC` against Hard, both playing T2 on the same games, in 364-game chunks from block 10400 (`tools/bots/sprt.py`, bounds ±2.94).

## Sequential test (Hard + read,readC against Hard, both v T2, block 10400 upward)

Hard is Conan-best (`BotContext.Hard`: T2 + lkDef, lkVar, lkPlan + look); the arm is the `conan` arm with `L5R_C2="t2,lkDef,lkVar,lkPlan,read,readC"` (the same plus the Read and the Guard). Chunks of one seed (364 games), analysed at chunk boundaries with `tools/bots/sprt.py` (p1 0.6, bounds ±2.94), hard cap 1,456 games (CONAN_CAMPAIGN_BRIEF.md section 4, Tier 2). The two arms of a chunk run in parallel on the Mac (about 80-90 s a game on one core; a 5-minute budget left 12 and 14 games undecided in chunk 1, so chunk 2 runs with a 10-minute budget and fire counting on the arm).

| Chunk | Seed | Hard v T2 | Hard + readC v T2 | Paired | Gained | Lost | LLR | Verdict |
|---|---|---|---|---|---|---|---|---|
| 1 | 10400 | 68.2% (240/352) | 68.0% (238/350) | 348 | 12 | 11 | −0.27 | continue |
| 2 (unfair, see below) | 10401 | 71.7% (261/364) | 64.1% (229/357) | 357 | 16 | 43 | −6.94 | H0 |
| 3 | 10402 | 66.5% (240/361) | 66.9% (239/357) | 356 | 19 | 18 | −0.55 (chunk alone) | continue |
| 1 + 2 + 3 | | | | 1,061 | 47 | 72 | −7.50 | H0 accepted |

Chunk 2 is not a fair pair. Its baseline ran in the first batch beside one other arm (57 minutes, 84 s a game); its arm, rerun after the fire-counting mistake, ran in a batch of three arms (1:53, 168 s a game, 7 games undecided). Hard's lookahead is bounded by wall-clock time (`lkT`, 10 s a search: past it the search is abandoned and the Contender's own choice stands), so under twice the load the arm searched less than its baseline, and the chunk measures the load as much as the Guard. Chunks 1 and 3, whose two arms ran side by side under the same load, are fair and sum to 31 gained, 29 lost over 704 paired games: no evidence either way. Chunk 2 is being rerun as a fair pair (both arms together, seed 10401); the test then has three fair chunks and the cap allows one more.

The reading so far, whatever the fair rerun says: at Contender speed the Guard gains +28 in 1,456; under Hard it changes 6-10% of games and gains nothing measurable, because `lkPlan` (2.2 fires a game) shapes the declaration before the Guard reads it and the lookahead then judges the thinned candidate against the others by playing on. For step 4 this is the expected pattern: a rule that reads the board well adds to the rule-based bot and is absorbed by the search. Arena discipline learned: Conan arms are compared only when run together under identical load, or with the work budget `lkB` instead of the time limit.

Chunk 2's first arm run (seed 10401, fire counting on, 10-minute budget) is discarded: fire counting re-decides each consulted rule, and under the lookahead each re-decision is a search, so the arm took 5:28 for 139 decided games of 364 against the baseline's 0:57 for 364; the decided games had less search than a fire-less run and cannot be paired (`cC104b-fires-invalid.*`). The one number kept from it, with that caveat: in the 364 games counted `readC` was consulted 2.5 times and fired 0.38 times a game (99 games with a fire), against 3.8 and 0.68 at Contender speed: under Hard the planned attack (`lkPlan`, 2.23 fires a game) shapes the declaration before the Guard reads it, and the lookahead then chooses among candidates the Guard has only thinned. Fire counts belong to Contender-speed arms; the arm was rerun without them.
