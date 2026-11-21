# Conan strategy campaign, step 0 and step 1 item 1: the T2 yardstick, the rule registry and the fire counters (issue #1206, 2026-11-14)

Branch `claude/1206-conan-registry`, from `main` at 70bb3be5 (after PR #1237). Blocks: Mac range from 9900. The Windows session claimed these two items on 2026-11-13 (issue #1206, 14:13 UTC) and had pushed nothing by 2026-11-14; the Mac took them over, on a differently named branch so a late Windows push cannot collide.

## What was built

- **`BotContext.T2Rules`**: the Contender's 22 rules frozen as they stood on 2026-11-13 (`econ, presence, lobby, shinden, openbow, ownprov, selflock, surereact, selfboost, nostraighten, provaim, spreadwide, conf, selfplayer, presdepth, pdh=100, keepup, nofoeboost, raid, pdstrong, defwin, nobowgift`). `RuleRegistryTests` fails if the list changes. Arm `t2` in `BotTournament.Arms`; a rule list may write `t2` for the set, so arms are `L5R_C2="t2,read,readA"` instead of spelling the yardstick out. `ConanPositions`, `ReadTests` and `ReadATests` pin to it; `BotLevelTests` keeps `TunedRules` (the practice Contender may move).
- **`RuleRegistry.cs`**: every rule (111 switches, three of them also knobs), factory flag (10) and knob (53) declared once with a line on what it does and where it acts. `BotContext.Contender` checks every rule set; an unknown or misspelt token throws, naming every offender. Before this, `L5R_C2="t2,raed"` played the baseline in silence. A source-scan test keeps the registry and the code equal in both directions.
- **Fire counters** (`L5R_TOURN_FIRES=1`): the challenger decides each decision again once per rule it consulted, with that rule alone masked; the rule fires when the choice differs. Knob tokens count the same way (masked, the default is used). The ten factory flags (`surereact` among them) read through the same gate, so they count. The re-decisions' memory (plan, planned attack, the Read's caches, the logs) is put back; a counted game replays move for move (test). The report's "Rule fires" table gives per rule the decisions it was on in and the decisions it changed, per game, and names the rules that never fired.

## Pre-registration (written before the runs)

On block 9900 (dev, independent pairing):

1. **A/A** Contender against Contender, 4 seeds, 1,456 games: the noise floor for the block. Expected 47-51%.
2. **Identity** arm `t2` against Contender on the same block: every game must end as the A/A's (`compare_tournament.py` zero flipped games). Done when zero games differ.
3. **Conan-best** (`hard`: T2 + `look, lkDef, lkVar, lkPlan`) against `t2`, 1 seed, 364 games, 20-minute budget, run alone. The brief's step 0 expects 70-77%. Outside that range the cause is found before anything else.
4. **Fire counts**: `t2` against Contender with `L5R_TOURN_FIRES=1`, 1 seed, 364 games. Done when every one of the 22 T2 tokens appears in the table and the rules that never fired are listed here.

## Results (block 9900, run 2026-11-14 09:30 to 10:29 UTC, Release build of this branch)

| Run | Arm (challenger) | Opponent | Games | Challenger won | Wall clock | Artifact |
| --- | --- | --- | --- | --- | --- | --- |
| 1. A/A | contender | contender | 1,456 | 742 = 51.0% (CI 48.4-53.5) | 19:57, three runs sharing 10 cores | `ca99` |
| 2. Identity | t2 | contender | 1,456 | 742 = 51.0%; **0 gained, 0 lost against run 1** | 19:56 | `t2_99` |
| 3. Conan-best | hard (T2 + look, lkDef, lkVar, lkPlan) | t2 | 364 | **267 = 73.4% (CI 68.6-77.6)**, p < .0001 | 38:45 alone, 57 s per game on one core | `hard_t2_99` |
| 4. Fire count | t2, `L5R_TOURN_FIRES=1` | contender | 364 (seed 9900) | 199 = 54.7%; **0 gained, 0 lost against run 1's games** | 8:05 | `fires99` |

- **Step 0 is done as the brief defines it**: the identity run flips no game, and Conan-best lands at 73.4%, inside 70-77% (blocks 7000, 7200 and 7300 gave 71-73% against the same Contender). Soundness in run 3: 0 stalls, 0 illegal, 0 exceptions, 0 out of time, 12 games with a refused-then-retried action (28 refusals, informational). Victory reasons were military 291, Honor 64, Dishonor 9 (the A/A: 1,091 / 323 / 42). Per deck Conan-best won from 46% (Classic Crane, 12 of 26) to 96% (Scottish Lion, 25 of 26); Crab Control 50%, Crane preset 54%.
- **The fire count works at full scale and costs little**: 48,020 decisions in 364 games (132 a game), none cut by the deadline, and the counted games are the A/A's games move for move (0 flipped). The run took 8:05 against roughly 5:00 for an uncounted 364-game run sharing the machine, so counting is about 1.6 times the cost, not the 10 times feared: most decisions consult few rules that are on.
- **Every one of the 22 T2 tokens fired at least once.** Fires per game, from the table in `fires99.md`:

| Rule | On, per game | Fires, per game | Games with a fire (of 364) |
| --- | --- | --- | --- |
| econ | 131.9 | 5.10 | 364 |
| lobby | 38.3 | 2.15 | 186 |
| selfplayer | 131.9 | 1.51 | 37 |
| ownprov | 37.4 | 1.13 | 33 |
| nostraighten | 36.1 | 1.01 | 118 |
| selflock | 35.5 | 0.97 | 49 |
| selfboost | 36.6 | 0.86 | 37 |
| surereact | 2.9 | 0.85 | 124 |
| presence | 131.3 | 0.84 | 181 |
| openbow | 131.3 | 0.77 | 85 |
| pdstrong | 2.6 | 0.61 | 134 |
| spreadwide | 5.5 | 0.60 | 110 |
| conf | 131.3 | 0.59 | 126 |
| nofoeboost | 131.9 | 0.51 | 36 |
| shinden | 131.9 | 0.48 | 49 |
| provaim | 131.4 | 0.30 | 25 |
| keepup | 131.9 | 0.18 | 28 |
| presdepth | 1.4 | 0.17 | 49 |
| defwin | 1.4 | 0.14 | 34 |
| raid | 131.3 | 0.06 | 14 |
| pdh=100 | 1.4 | 0.06 | 20 |
| nobowgift | 131.9 | 0.01 | 5 |

Reading the table. "On" counts the decisions in which the rule was consulted and present, not those in which its condition held: a rule tested at the top of every candidate's score (econ, selfplayer, presence, conf, shinden, nofoeboost, keepup, raid, nobowgift, provaim, openbow) is on in all 132 decisions a game, so its fires per 100 on is not comparable with a rule gated by the phase or the action (surereact 29 per 100, pdstrong 23, presdepth 12, spreadwide 11, defwin 10). Fires per game is the comparable column. The rules that rarely change a choice are **nobowgift** (once in 73 games), **pdh=100** (the knob: its default 50 would have changed 6 decisions in 100 games) and **raid** (6 in 100 games); **selfplayer, ownprov, selflock, selfboost and nofoeboost** fire in few games but several times in those games (a deck with the abilities they govern). Nothing here says a rule is good or bad: a rule that fires once and wins a Province is worth more than one that fires five times a game on Dynasty purchases. The counts say where a rule family's screen can show an effect at all (family F, the Dynasty: econ fires every game) and where it cannot (a rule that fires in 5 games of 364 cannot move a 1,456-game win rate by more than a few games, whatever it does).

**What this changes for the campaign.** The two null families of PR #1236 and #1237 can now be counted: a family A or D arm run with `L5R_TOURN_FIRES=1` says in how many decisions `readA` or `readD` changed the choice, which the behaviour profile could only infer. The Mac blocks used: 9100 to 9900; the next fresh block is 10000.
