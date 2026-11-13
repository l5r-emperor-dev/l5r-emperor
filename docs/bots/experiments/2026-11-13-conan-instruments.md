# Conan strategy campaign, Mac side: the instruments (issue #1206, from 2026-11-13)

The Mac half of `docs/bots/CONAN_STRATEGY_BRIEF.md` step 1, split with the Windows session on issue #1206 (comment of 2026-11-13 16:09 UTC): Windows holds step 0 (the frozen `T2Rules`, the A/A and the Conan-best baseline) and step 1 item 1 (the rule registry with fire counters) on blocks 8500 to 8999; the Mac holds step 1 items 2 to 4 (battle records, the behaviour profile, the decision test set) and then step 2's effect descriptors, on blocks 9100 to 9499 and 9600 upward. Branch `claude/1206-conan-instruments`.

## Plan (above the line)

Nothing here drives a decision. Each instrument has a criterion from the brief, checked below.

1. **Battle records** (`L5R_TOURN_BATTLES=<dir>`, `tests/L5R.AI.Tests/BattleRecords.cs`, read by `tools/bots/battles.py`). One JSON line per declared attack: board, assignments, the paper margin at declaration and at defence, `EstimateAttack` (through `TrainingOpponent.DiagnoseAttack`, a harness-only API), `BattlePolicy.Odds` at the attacker's first Battle decision, the engine's battle action log, the resolution and the units destroyed. Criterion: the records of one block reproduce the paper-margin table (`ARENA_BEHAVIOUR.md` Q1e) to within two points of the same block's parsed logs.
   - Block **9100** (seeds 9100 to 9103, 1,456 games, Contender against Contender, `independent`, `dev`, budget 20) is run with both `L5R_TOURN_LOG` and `L5R_TOURN_BATTLES`. It is an instrument check, not a decisive run; it is also this machine's first A/A on its own block range.
2. **The behaviour profile** (`tools/bots/behaviour/profile.py`): one command over a log directory, the thirteen measures of `ARENA_BEHAVIOUR.md`'s headline table, each taken from the q script that defines it (the q scripts were split into `collect()` and `main()` so their definitions are imported, with outputs byte-identical before and after). Criterion: reproduces the headline table from fresh logs.
3. **The decision test set** (`tests/L5R.AI.Tests/ConanPositions.cs`, case `conan-positions`; replays in `docs/bots/replays/`). 52 positions: `PLAYER_MODEL.md` section 7, the old brief's section 6.4, and four of his mistakes (section 6) stored with the correct move. Each is (replay, turn, phase, nth real choice); the expected move is his logged command, or a fragment list for positions where only part of the command matters (the immune unit alone, no defender at the attacked Province, the Tactical Advantage whichever card is discarded). Any rule set runs through `L5R_POS_ARMS`. Criterion: runs under 60 seconds; the agreement of T2 and of Conan-best is the baseline.

Blocks used by this note: 9100.

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
