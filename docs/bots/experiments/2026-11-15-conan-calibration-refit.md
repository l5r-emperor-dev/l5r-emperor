# Conan strategy campaign: refitting the Read's Calibration tables (issue #1206, 2026-11-15)

Branch `claude/1206-conan-calibration`, from `main` after families G and I. Mendon's order of work (2026-11-14): "continue on the lettered families then refit the calibration tables. Then move on to step 4."

## Why

`Calibration` (`src/L5R.AI/Read/Questions.cs`) holds two tables the Read's questions lean on: `PFall(cell)`, the chance a Province falls by the exchange's cell at defence (open/likely/worst), fitted on block 9200 and gated on 9300 (`docs/bots/experiments/2026-11-13-conan-instruments.md`), and `PLose(cell, gap)`, the chance of losing a Province on the opponent's next turn by the threat's cell and Force gap, fitted on 9400 and gated on 9600. Both were fitted before the exchange fix of 2026-11-14 (family A's note, item 3: a bowed performer, the performer keyword, the attacking or defending target), so the cells the exchange now produces are not the cells the tables were fitted on. Families C, B, F, G and I were screened on the stale tables; nothing was promoted, so nothing on main leans on them yet, but step 4 (the Read's shapes and exchange as the search's candidates and battle policy) will.

## Pre-registration

- **Fit set**: block 11400, the T2 mirror (`ca114`), 1,456 games, battle and threat records on (`L5R_TOURN_BATTLES`, `L5R_TOURN_THREATS`).
- **Held-out set**: block 11500, the same arms (`ca115`), records on.
- **Procedure**: `tools/bots/battles.py artifacts/bots/battles-ca115 --fit artifacts/bots/battles-ca114` gives the fall rate per (open, likely, worst) cell fitted on 11400 and scored on 11500 (Brier beside the paper margin and `EstimateAttack`); `tools/bots/threats.py artifacts/bots/threats-ca115 --fit artifacts/bots/threats-ca114` the same for the threat's (cell, gap bucket). The fitted rates replace `PFall` and `Lose`.
- **Gate** (brief step 2, unchanged): the Read's Brier below both baselines on the held-out block; every cell holding 200 or more battlefields (or turns) observed within 10 points of the fitted rate.
- **Identity**: `t2` plays the same games (the tables are read only under `read`); the shadow A/A on a seed is unchanged in result.

## Blocks

Block 11400 (`ca114`, T2 against T2, 1,456 games, all decided, 49.5%) is the fit set; block 11500 (`ca115`, 48.8%) the held-out set; battle and threat records on both (`artifacts/bots/battles-ca114`, `battles-ca115`, `threats-ca114`, `threats-ca115`; copies in `~/L5R-conan/artifacts/bots/`). Both ran on the Mac on 2026-11-15 alongside family I's screen (12.3 s a game).

## The battle table (`PFall`)

`python3 tools/bots/battles.py artifacts/bots/battles-ca115 --fit artifacts/bots/battles-ca114`: 12,245 fought battlefields scored, rates fitted on 12,094; the Province fell in 53%.

| Estimator (bucketed) | Brier on 11500 | Cells of 200+ off by more than 10 points |
|---|---|---|
| Constant rate | 0.2497 | — |
| Paper margin at declaration | 0.2154 | 0 |
| Paper margin at defence | 0.2116 | 0 |
| `EstimateAttack` net | 0.2219 | 0 |
| `BattlePolicy.Odds` at the first Battle decision | 0.1800 | 0 |
| Read at declaration | 0.2094 | 0 |
| Read at defence (defended only, 5,744) | 0.1502 | 0 |
| **Read at defence, or undefended when nobody defended** (the table) | **0.1471** | 0 |
| Read crossed with the paper margin at defence | 0.1394 | 0 |

| Cell | Old `PFall` | Fitted on 11400 (battlefields) | Observed on 11500 | New `PFall` |
|---|---|---|---|---|
| 000 | .10 | 9% (2,825) | 10% | .09 |
| 001 | .24 | 23% (2,188) | 23% | .23 |
| 011 | .45 | 56% (105) | 48% (122) | .56 |
| 111 | .79 | 80% (6,972) | 80% | .80 |
| 010, 100, 101, 110 | .30, .50, .60, .70 | under ten each | — | unchanged |

The exchange fix moved the table by a point in the three big cells and eleven points in the small 011 cell (a Province that falls only under the belief's likely and worst hands); the Read's Brier is 0.147 against 0.152 before the fix (blocks 9200/9300), still well below both baselines.

## The threat table (`PLose`)

`python3 tools/bots/threats.py artifacts/bots/threats-ca115 --fit artifacts/bots/threats-ca114`: 18,915 turns scored (a Province lost on the opponent's next turn in 29%; the opponent attacked in 50%), rates fitted on 18,584.

| Estimator | Brier on 11500 | Cells of 200+ off by more than 10 points |
|---|---|---|
| Constant rate | 0.2078 | — |
| Paper margin, five buckets | 0.1295 | 0 |
| Paper margin, eight buckets | 0.1212 | 0 |
| Guard ratio | 0.1259 | 0 |
| **Threat at their turn: cell x Force gap, eight buckets** (the table) | **0.1091** | 0 |
| Threat projected after my last Dynasty decision | 0.1124 | 0 |

New `Lose` rows (gap buckets <= -10, -9..-5, -4..-1, 0..2, 3..5, 6..9, 10..14, > 14), fitted on 11400; the old row in brackets:

| Cell | New row | Old row |
|---|---|---|
| 000 | .02 .03 .19 .30 .35 .39 .38 .57 | .03 .04 .23 .30 .37 .44 .50 .57 |
| 001 | .05 .25 .44 .47 .57 .68 .66 .74 | .05 .24 .42 .47 .58 .66 .67 .76 |
| 011 | .08 .25 .59 .72 .76 .76 .88 .95 (the last three buckets hold under 20 turns; capped at .95) | .05 .24 .52 .71 .80 .88 .88 .88 |
| 111 | .05 .20 .47 .75 .83 .86 .91 .95 | .05 .20 .49 .71 .82 .87 .91 .95 |

Within four points of the old row in every cell of 200 or more turns: the exchange fix did not move the threat's calibration in aggregate. The gate (Brier below both baselines; every cell of 200+ within 10 points) holds for both tables on the held-out block.

## Identity

The tables are read only under `read` (the shadow Read and the opt-in families); `T2Rules`, `Contender` and `Hard` do not read them, so the arena's defaults play the same games. The refit is a two-way door: the previous values are in this note.
