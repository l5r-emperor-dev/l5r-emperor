# Conan strategy campaign, Mac side: the instruments (issue #1206, from 2026-11-13)

The Mac half of `docs/bots/CONAN_STRATEGY_BRIEF.md` step 1, split with the Windows session on issue #1206 (comment of 2026-11-13 16:09 UTC): Windows holds step 0 (the frozen `T2Rules`, the A/A and the Conan-best baseline) and step 1 item 1 (the rule registry with fire counters) on blocks 8500 to 8999; the Mac holds step 1 items 2 to 4 (battle records, the behaviour profile, the decision test set) and then step 2's effect descriptors, on blocks 9100 to 9499 and 9600 upward. Branch `claude/1206-conan-instruments`.

## Plan (above the line)

Nothing here drives a decision. Each instrument has a criterion from the brief, checked below.

1. **Battle records** (`L5R_TOURN_BATTLES=<dir>`, `tests/L5R.AI.Tests/BattleRecords.cs`, read by `tools/bots/battles.py`). One JSON line per declared attack: board, assignments, the paper margin at declaration and at defence, `EstimateAttack` (through `TrainingOpponent.DiagnoseAttack`, a harness-only API), `BattlePolicy.Odds` at the attacker's first Battle decision, the engine's battle action log, the resolution and the units destroyed. Criterion: the records of one block reproduce the paper-margin table (`ARENA_BEHAVIOUR.md` Q1e) to within two points of the same block's parsed logs.
   - Block **9100** (seeds 9100 to 9103, 1,456 games, Contender against Contender, `independent`, `dev`, budget 20) is run with both `L5R_TOURN_LOG` and `L5R_TOURN_BATTLES`. It is an instrument check, not a decisive run; it is also this machine's first A/A on its own block range.
2. **The behaviour profile** (`tools/bots/behaviour/profile.py`): one command over a log directory, the thirteen measures of `ARENA_BEHAVIOUR.md`'s headline table, each taken from the q script that defines it (the q scripts were split into `collect()` and `main()` so their definitions are imported, with outputs byte-identical before and after). Criterion: reproduces the headline table from fresh logs.
3. **The decision test set** (`tests/L5R.AI.Tests/ConanPositions.cs`, case `conan-positions`; replays in `docs/bots/replays/`). 52 positions: `PLAYER_MODEL.md` section 7, the old brief's section 6.4, and four of his mistakes (section 6) stored with the correct move. Each is (replay, turn, phase, nth real choice); the expected move is his logged command, or a fragment list for positions where only part of the command matters (the immune unit alone, no defender at the attacked Province, the Tactical Advantage whichever card is discarded). Any rule set runs through `L5R_POS_ARMS`. Criterion: runs under 60 seconds; the agreement of T2 and of Conan-best is the baseline.

4. **The Read in shadow and the calibration gate** (step 2, second part; branch `claude/1206-conan-read-shadow`, stacked on the instruments branch). `Read.Of(view)` builds the ledger (units with their descriptors and spent abilities, Provinces, Gold, Honor, the Favor, the own hand exactly), the belief (the opponent's hand by effect class: a prior of about four Battle answers in ten cards, updated by every Fate card of theirs that is public) and the exchange (one battle played out defender first, each side using its best neutraliser on its best target while it is not getting the result it wants, resolved by the rules). Rule `read` builds and logs it at every change of its key and drives nothing. Battle records now carry the Read's three verdicts (open, likely, worst) from the attacker's view at declaration and from the defender's at assignment.
   - Pre-registered: block **9200** (Contender against Contender, 1,456 games, records on) is the fit set: `battles.py` learns the fall rate of each (open, likely, worst) cell and of each baseline bucket there. Block **9300** (same arms, records on) is the held-out set on which the Brier score of each estimator is reported. Block 9200 is also run with `T2 + read` against the Contender on the same seeds: the gate requires zero games whose result differs from the A/A (the shadow Read changes nothing).
   - Gate (brief step 2): the Read's Brier below both baselines on 9300, and in each cell holding 200 or more battlefields the observed rate within 10 points of the fitted one; the same for the threat answer (not yet built: recorded as open); need and have reproduced on the attack positions of the decision set, or each miss with a cause.

5. **The threat answer and its calibration** (step 2, last part; branch `claude/1206-conan-threat`, from main after the Read merged). `Threat.Of(read, home, army)` (`src/L5R.AI/Read/Threat.cs`, `Read.ThreatNow()`) is section 4.5's `threat(home)`: their whole army straightened (every unit, bowed or not, with its full Force, its abilities fresh and next turn's Gold) against what is unbowed at my home; they choose the attack worst for me among all-in at any Province and a balanced split across any two, I choose the defence best for me among nobody, everybody at one battlefield and a balanced split, each judged by the exchange under the open, likely and worst hand; the outcome is read in Mendon's cumulative tiers (a Province falls; held; held and no defender lost; held, nobody lost and an attacker destroyed) with the exchange's margin after answers at the worst battlefield. Rule `read` logs it once a turn at the first Dynasty decision and drives nothing.
   - Threat records (`L5R_TOURN_THREATS=<dir>`, `tests/L5R.AI.Tests/ThreatRecords.cs`, read by `tools/bots/threats.py`): one JSON line per player turn with the projection at three moments (at the seat's attack declaration with the units its exchange expects to destroy left out; after its last Dynasty decision; at the first decision of the opponent's turn, their army as it really straightened), two paper baselines (their Force minus my unbowed home Force minus my weakest Strength; the guard ratio of `ARENA_BEHAVIOUR.md` Q2c) and what the opponent's turn did (attacked or not, Provinces lost, units lost on each side).
   - Pre-registered: block **9400** (Contender against Contender, 1,456 games, threat and battle records on) is the fit set; block **9600** (same arms) the held-out set. Block 9400 is also run as `T2 + read` against the Contender on the same seeds for the identity check (the threat log is new code under the shadow rule). Gate: the threat's Brier on 9600 below both paper baselines, every cell of 200 or more turns within 10 points of its fitted rate; reported before and after the opponent's first Fate reveal.

Blocks used by this note: 9100, 9200, 9300, 9400, 9600.

## Results (below the line)

### 2026-11-13: the decision test set

52 positions, 50 reachable. Two are unreachable: `practice-20261109-141352` (G2) stops at its command 46 (T5 Battle), where the engine now refuses Spider's `ring-of-fire-destroy` ("That card does not meet this ability's performer requirement"), a rules change since the export; its T10 and T12 defence positions wait for a decision on that reading. Replays are stepped as `human-compare` steps them; the bot is asked before every seat-0 decision and judged at the listed ones.

| Arm | Agreed | A | B | D | E | F | G | I | Time |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `t2` (`BotContext.Contender()`) | 9 of 50 | 2/9 | 0/1 | 1/6 | 6/18 | 0/4 | 0/5 | 0/7 | 16 s |
| `conan` (T2 + `look,lkDef,lkVar,lkPlan,lkB=600,lkT=60`, sampler only at the positions) | 10 of 50 | 2/9 | 0/1 | 1/6 | 7/18 | 0/4 | 0/5 | 0/7 | 45 s |

(Measured before the late-Force positions were loosened to judge the Tactical Advantage itself; with that change `conan` also agrees on `E-G7T3` and `E-GT5-lateforce-a`, where it discards a different card.) Where the two agree with Mendon it is mostly where the Contender already matched him in the compare logs (the duel strikes, G1 T9's defence, GT T7's split) and the four mistakes stored with the correct move, all four of which both bots get right. Neither bot holds in the drain game, splits an attack, keeps the hand, buys the Personality first, defends with the minimum or concedes a Province; Conan adds the late Force in two of six positions where the Contender passes in all six.

### 2026-11-13: the behaviour profile

`profile.py` on block 8400's logs (`logs-r84`, `logs-ca84`, in `~/L5R-conan`) beside the seven human compare logs reproduces the headline table: Mendon's column exactly (all 13 cells), Conan's and the Contender's within four points except one cell on 35 attacks (one-Personality attacks into a held Favor, Contender vs Conan: 74% against 90%, two standard errors on that denominator). Parser self-check on the same logs: 735 games, Personality counts 100%, membership 97.6% of 5,434, Force estimate exact 74.9% and within 2 in 86.3%.

### 2026-11-13: block 9100, the battle records and the paper-margin check

Block 9100 (seeds 9100 to 9103), Contender against Contender, 1,456 games, all decided, 14 minutes: **48.9%** (712 to 744), the A/A of this machine's block range. Logs in `artifacts/bots/logs-ca91`, battle records in `artifacts/bots/battles-ca91` (1,456 files, 9,383 attacks, 12,390 fought battlefields; not committed).

`profile.py` on these logs beside the headline table (Contender mirror column, old blocks): 59/59, 23/23, 28/28, 8.2/8.0, 20/20, 20/20, 78/77, 38/—, 70/—, 80/81, 27/27, 41/41, 87/84.

**The Q1e check did not land within two points, and the cause is the parser, not the records.** Per attack, `battles.py` (records) against `q1_attack_timing.py` (parsed logs), same block:

| paper margin | attacks: records / parsed | Province destroyed | attacker lost a Personality | nothing achieved |
| --- | --- | --- | --- | --- |
| <= -10 | 331 / 405 | 3% / 10% | 41% / 33% | 84% / 79% |
| -9..-1 | 1,924 / 2,142 | 24% / 33% | 30% / 27% | 50% / 46% |
| 0..+5 | 2,623 / 2,175 | 56% / 59% | 21% / 17% | 30% / 27% |
| +6..+12 | 2,323 / 1,753 | 73% / 76% | 20% / 17% | 20% / 17% |
| > +12 | 2,182 / 1,283 | 87% / 90% | 23% / 18% | 8% / 6% |

Matching the 7,758 attacks both see by (seed, turn): the parser drops 1,625 of the 9,383 attacks as "uncertain" (its Force estimate), and on the rest its committed Force equals the engine's in 5,001 cases and is lower by 2 to 8 in about 1,900 (attachment Force it does not see); its defender Force is taken from the turn header and so is higher than the engine's at assignment time in about 1,300 cases (units bowed or penalised during the Action Phase). The records carry the engine's own numbers at the moment of assignment, so where the two disagree the records are right. Consequence for `ARENA_BEHAVIOUR.md`: Q1e's parsed rates are shifted towards the low-margin buckets; the shape of the finding (the Contender fails 30 to 50% of the time at a margin of 0 to +5) stands. The calibration gate of step 2 will use the records, never the parser.

### 2026-11-13: the effect descriptors (step 2, first part)

`EffectDescriptors.Of(abilityId)` reads an ability's label, target category and structured fields into class, magnitude, side, target kind, conditions, secondary effects, costs, performer and timing (`src/L5R.AI/EffectDescriptor.cs`, 11 regression tests in `EffectDescriptorTests.cs`). Nothing in the decision code reads it yet.

Coverage (`docs/bots/strategy/descriptors/coverage.md`): 299 distinct abilities over the 14 pool decks, the two presets and the 16 replay decks; **0 Unknown**. Over the whole catalogue, 48 of 1,139 Battle abilities are Unknown; 33 of those are the `training-*` scenario abilities and the rest are grants of additional actions and movement primitives (`predator-move`, `goju-house-guard-return`, `two-hands-grant`).

Hand check against the card text, three samples of 60 (seeds 1206, 2026, 3):

| Sample | Use | Right (class) | Wrong | What was wrong |
| --- | --- | --- | --- | --- |
| 1 (seed 1206, in the audit file) | read first, then used to fix | 49 of 60 | 11 | `without-attachments` read as an Attachment target (3); "destroyed" in an if-clause read as Destroy (2); a negation of locks read as Lock; "reduce his Force by" missed; an own unit moved home read as SendHome; a Personality in a province read as a Province target (2); a Region target read as a unit |
| 2 (seed 2026), blind after those fixes | read, then used to fix | 55 of 60 | 5 | an "A or B" category read as the second kind; "do not destroy" and "destroy his unit" read as Destroy; "each time it bows" read as Bow; one unclear |
| 3 (seed 3), blind after those fixes | the gate | **59 of 60** | 1 | a Reconnaissance reaction whose "Ranged Attacks that do not destroy" fires the Attack reading |

Known imprecisions that do not change the class: a spurious Negate secondary from "will not be negated" (2 of 60); side Any where the text says enemy in a Slots ability (2); a magnitude of 1 where two +1F tokens are given. Secondary effects and conditions are read but not yet tested beyond the regression cases.

### Criteria of step 1 and step 2 as they stand

| Criterion | Status |
| --- | --- |
| Rule registry: a misspelt rule fails; fire counts for all 22 T2 rules | Windows side |
| Battle records reproduce Q1e within two points | Not met as stated; the parser is the one that differs (above). The records are the exact table |
| Profile reproduces the headline table | Met (block 8400 and block 9100) |
| Decision set under 60 s, T2 and Conan-best baselines recorded | Met: 16 s and 45 s; 9/50 and 10/50 (12/50 for Conan-best with the late-Force positions judged on the Tactical Advantage alone) |
| Descriptors: 57 of 60 right; unknown share per deck | Met: 59 of 60 on the blind sample; 0% unknown in every audited deck |
| Shadow Read A/A identical; Brier below both baselines; need/have on the decision set | Not started (next: the ledger, the belief, the exchange) |

### 2026-11-13: the Read in shadow, block 9200

Block 9200 (seeds 9200 to 9203), Contender against Contender, 1,456 games, all decided, 14 minutes: **50.6%** (737 to 719).

The first records of the Read showed a flaw in how the record took it at declaration, not in the Read: the attacker's exchange was projected against the defenders already assigned, which at declaration is nobody, so 10,797 of 12,410 battlefields read "falls" under all three hands while 57% fell. The defence-time Read, projected against the defenders actually assigned, separated the battlefields: 11% fell where all three hands said "held" (2,675), 27% where only the upper bound said "falls" (2,200), 74% where all three said "falls" (879). The record now takes the declaration Read against every unbowed defender at home (the paper margin's convention) and once against nobody, so an undefended battlefield has a Read of its own; block 9200's records are regenerated on the same seeds before the fit (identical games; the A/A result does not change).

Two things the Read lacked at Mendon's attack positions, found with `L5R_POS_READ=1` on family A, and the first is fixed:

- **Tactician late Force.** At GT T5 both of his groups were below Strength 7 at declaration and he razed both with Tactical Advantage; the exchange now gives each unbowed Tactician at the battle one discard worth the hand's best Focus Value, and reads GT T5 as two razes under every hand, and G3 T9's Province 1 as a raze with his own hand. The Contender assigns the same positions as "nothing achieved".
- **The belief is pessimistic where the opponent had shown nothing.** At G3 T9 the likely and worst hands stop the raze that happened; the prior gives an eight-card hand one send-home, one bow and one destroy. Whether that prior is too heavy is what the calibration on blocks 9200 and 9300 measures.

`need` and `have` at the attack positions: G6 T9 and T11 (the immune Dairuko alone) read need 1, have 8 and 10 against 3; GT T7 need 1, have 4 against 1; G7 T7 Province 2 need 2, have 5 against 4; G3 T9 and GT T5 need "-" on Force alone (razed with late Force), have 3 to 6 against 1 to 4. These match his own counts in `PLAYER_MODEL.md` 3.1 ("he usually held 1.5 to 4 times need") on every position where he attacked.

### 2026-11-13: the calibration gate (blocks 9200 and 9300)

- Block 9200 run again with `T2 + read` against the Contender on the same seeds as its A/A: **0 games gained, 0 lost** (1,456 games, 50.6% both). The shadow Read changes nothing.
- Block 9300 (seeds 9300 to 9303), Contender against Contender, 1,456 games, all decided: **51.8%** (754 to 702), records on; the held-out set.
- Block 9200's records regenerated with the current recorder on the same seeds (identical games, 50.6%); the fit set.

Each estimator's bucket rates fitted on 9200 and scored on 9300 (`battles.py artifacts/bots/battles-ca93 --fit artifacts/bots/battles-ca92`; 12,338 fought battlefields with attackers; the Province fell in 52%):

| Estimator (bucketed) | Brier on 9300 | Cells of 200+ battlefields off by more than 10 points |
| --- | --- | --- |
| Constant rate | 0.2496 | — |
| Paper margin at declaration | 0.2179 | 0 |
| Paper margin at defence | 0.2134 | 0 |
| `EstimateAttack` net (the planner's, `pattack`'s and `guard`'s number) | 0.2229 | 0 |
| `EstimateAttack` verdict (loses / wins / razes) | 0.2258 | 0 |
| `BattlePolicy.Odds` at the attacker's first Battle decision | 0.1809 | 0 |
| Read at declaration, against every unbowed home defender | 0.2120 | 0 |
| Read at defence (defended battlefields only, 5,728) | 0.1540 | 0 |
| **Read at defence, or against nobody when undefended** | **0.1516** | 0 |
| Read (as above) crossed with the paper margin at defence | 0.1417 | 0 |

Cells, fitted on 9200 / observed on 9300: the Read says "held" under every hand 10% / 10% (3,219 battlefields); "falls" only under the belief's upper bound 26% / 26% (2,103); "falls" under every hand 79% / 79% (7,001). Crossed with the paper margin, the extremes are 3% / 2% ("held", margin −10 or worse) and 91% / 93% ("falls", margin above +12); the Read tells apart battlefields the paper margin lumps together: at a margin of 0 to +5 the Province fell 82% of the time when the Read said "falls" and 12% when it said "held" (2,697 and 859 battlefields).

**Gate (brief step 2), as it stands:**

| Criterion | Status |
| --- | --- |
| 57 of 60 abilities classified correctly; unknown share by deck | Met: 59 of 60 blind; 0% in every audited deck |
| A/A with the shadow Read identical to one without | Met: 0 flipped on block 9200 |
| The Read's Brier below both baselines on a held-out block; every cell of 200+ within 10 points | Met: 0.152 against 0.213 (paper) and 0.223 (`EstimateAttack`); 0 cells off |
| The same two tests for the threat answer | See the threat section below (blocks 9400 and 9600) |
| `need` and `have` reproduce Mendon's counts on the attack positions, or each miss has a cause | Met on every position where he attacked (above); the two he razed below Strength on Force alone are read as razes once Tactician late Force counts |

Limits worth stating: the Read's exchange is a projection with fixed habits (each side plays its best neutraliser while not getting its result; a duel is counted as half a removal; a Negate cancels the other side's decisive answer); its "likely" verdict rarely differs from "open" against these bots because the belief's prior is weak, which is also why the upper bound carries the signal. The threat answer and the questions of section 4.5 come next; nothing decides on the Read yet.

### 2026-11-13: the threat answer (blocks 9400 and 9600)

- Block 9400 (seeds 9400 to 9403), Contender against Contender, 1,456 games, all decided, threat and battle records on: **50.1%** (729 to 727); the fit set.
- Block 9600 (seeds 9600 to 9603), same arms: **49.6%** (722 to 734); the held-out set.
- Block 9400 run again as `T2 + read` against the Contender on the same seeds (the threat log is new code under the shadow rule): **0 games gained, 0 lost**; all 1,456 games identical in winner, turn, reason, final Honor and Provinces (50.1% both).

The smoke block of one seed found two faults in the first recorder before any number was taken: the ledger took the engine's `UnitForce`, which is 0 for a bowed Personality and leaves bowed Followers out, so their straightened army was under-counted by 2 to 10 Force in a third of the turns; and the "after Dynasty" view missed the seat's own purchases when the buy was the last command of the phase. Both fixed (a bowed unit's `UnitForce` is now its full Force and `Contribution` zeroes it; the view is taken before and after each Dynasty decision while the turn is still the seat's).

**Calibration of threat(home)**: fitted on the 19,034 turns of block 9400 that had an opponent turn after them, scored on the 18,786 of block 9600. The target is "lost a Province on the opponent's next turn" (30% of turns; the opponent attacked in 49%).

| Estimator | Brier on 9600 | Cells of 200+ turns off by more than 10 points |
| --- | --- | --- |
| Constant rate | 0.2100 | — |
| Paper margin (their Force − my unbowed home Force − my weakest Strength), five buckets | 0.1296 | 0 |
| Paper margin, eight buckets | 0.1202 | 0 |
| Guard ratio (`ARENA_BEHAVIOUR.md` Q2c), four buckets | 0.1247 | 0 |
| Threat three-hand cell alone (open / likely / worst says a Province falls) | 0.1424 | 0 |
| Threat tiers alone (the tier under each hand) | 0.1348 | 0 |
| Threat likely-hand margin after answers, alone | 0.1398 | 0 (not monotone: see below) |
| **Threat: the three-hand cell crossed with the projection's own Force gap, eight buckets** | **0.1096** | 0 |
| The same from the "after my last Dynasty decision" projection | 0.1130 | 1 |

The cells of the headline estimator: "000" runs from 2% (gap −10 or less) to 58% (gap above +14); "001" from 5% to 80%; "111" from 7% to 95%; every cell of 200 or more turns within 5 points of its fitted rate.

**What this says, exactly.** The exchange's verdict on its own is coarser than the Force gap: the cell alone scores worse than both paper baselines. The verdict adds to the gap what the gap cannot see (my answers, their answers under the belief, the split, the best defence): crossed with it, the threat scores 0.110 against 0.130 and 0.125, and 0.120 for the gap at the same resolution. The gate's letter ("the Read's Brier lower than both baselines, every cell within 10 points") is met by the crossed estimator and not by the verdict alone; the Force gap in it is the ledger's own number (their straightened army, Followers counted, against my unbowed home and weakest Province), which is the paper margin. The threat answer the rules will read is therefore the pair (cell, gap) with this table, not the verdict alone.

Two findings for the rules:

- **Held at the cost of the army.** The projection's best defence puts Provinces first (Mendon's tiers), so it reads "held" where everybody dies to keep the Province; the tier table shows those turns ("111": held with defenders lost under all three hands) lose a Province 28% of the time and the Contender in play does not make that defence. Family C's price of a defence must weigh the units, which is what `exposure(choice)` is for.
- **Open says held, the gap says +14 or more: 58% lost.** The exchange's optimism there comes from my own hand answers and printed abilities, which it assumes I will play and the Contender often does not. The belief's two other hands raise the cell, as they should.

Before the opponent's first Fate reveal the cell alone scores 0.0675 against a constant 0.0983 (6,090 turns, 11% lost); after it 0.1746 against 0.2382 (12,696 turns, 39% lost). The two populations differ too much in base rate to compare the belief's effect from this; the split is recorded as the brief asks.

The threat projected at the seat's own attack declaration (with the units its exchange expects to destroy left out, before its purchases) scores 0.2040 on attacking turns alone: it is the answer the attack rules will see, and it is weaker than the end-of-turn one because it does not yet know what will be bought; the "after Dynasty" projection is the right moment for family F and the declaration one for family A, each with its own table.

The battle Read's calibration repeats on these blocks after the ledger change: Read at defence or undefended 0.1491 against paper at defence 0.2132 and `EstimateAttack` 0.2243 (fitted on 9400, scored on 9600, 12,212 battlefields, 0 cells off).

### Criteria of step 2 as they stand after the threat answer

| Criterion | Status |
| --- | --- |
| The same two tests for the threat answer | Met by the threat as (cell, Force gap): 0.110 against 0.130 and 0.125 on held-out block 9600, 0 cells off. Not met by the verdict alone (0.142). Stated as such |
| A/A with the shadow Read identical to one without | Met again on block 9400 with the threat log on: 1,456 of 1,456 games identical |

Nothing decides on the Read yet. Next: the opportunity and race answers, the posture, and replanning (section 4.5 and 4.6), then family D.

