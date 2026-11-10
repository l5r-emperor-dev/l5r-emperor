# Conan against the Contender (issue #1206), from 2026-11-10

Goal (Mendon, 2026-11-10): Conan wins 80% of its games against the Contender. The method follows `docs/bots/CONAN_CAMPAIGN_BRIEF.md`. The plan for the bots is in `docs/bots/ROADMAP.md`.

Every arm here uses `L5R_TOURN_OPP=contender` and `L5R_TOURN_BUDGET=20`, with `independent` pairing in `dev` mode. Each arm is compared on the same games: the net is the number of games the arm won that the reference lost, minus the reverse.

## Block 7000 ×1 (364 games): screens

The reference for the first two rows is the Contender A/A (Contender against Contender: 48.6%). After that, each arm is compared with the previous best.

| Arm | Settings (on top of the Contender's tuned rules) | Win rate | Net | p | Search median / p90 | Thinking per game (mean) |
| --- | --- | --- | --- | --- | --- | --- |
| `vc-look` | `look` | 62.1% | +49 vs A/A | <.0001 | 1.3 s / 3.8 s | 38 s |
| `vc-cv` | `look,lkDef,lkVar` | 62.9% | +52 vs A/A | <.0001 | 1.4 s / 4.1 s | 46 s |
| `vc-big` | CV + `lkN=8,lkK=5` | 64.8% | +7 vs CV | .43 | 6.0 s / 10 s | 141 s |
| `vc-atk` | CV + `lkAtk` | 66.8% | +14 vs CV | .05 | 1.7 s / 6.5 s | 81 s |
| `vc-plan` | CV + `lkPlan` | **71.2%** | **+30 vs CV** | .0003 | 1.8 s / 5.7 s | 70 s |
| `vp-clock` | plan + `clock` | 70.9% | −1 vs plan | 1.0 | 2.0 s / 6.1 s | 75 s |
| `vp-far` | plan + `lkFar` | 69.2% | −7 vs plan | .44 | 2.8 s / 10 s | 127 s |
| `vp-hs` | plan + `lkHS` | 70.1% | −4 vs plan | .29 | | |

Two arms ran at a time, so their thinking times run somewhat high. No game was killed.

Notes:
- `vp-far` searched its attack plan through the opponent's next turn. At p90 its searches hit the 10 s limit, so many of them were abandoned and the Contender's own choice was played. This result does not fairly test the longer horizon.
- `clock` and `lkHS` changed very few games (5 and 8).

### Why Conan lost (`vc-cv`, 135 losses; a review of the game logs)

On the same seeds, the plain Contender also lost 115 of these 135 games, so most of the losses come from decisions the search does not make.

| Pattern | Losses affected (about) | Searched? |
| --- | --- | --- |
| Lost its own attack: ahead on Force, it attacked and lost Personalities or gave the defender Honor | 28 | Only with `lkPlan` (end of the attack only) |
| Did not race an opponent nearing an Honor win (seen in none of the 229 wins) | 23 | No |
| The defence search gave up a late Province to keep units | 10–15 | Yes (`lkDef`) |
| Two or more Personalities behind by turn 9 | 50 | No (purchases) |
| Overrides on thin margins (under 3 points) | most of the 20 games the Contender won | Yes |

## Block 7200 ×1 (364 games): the number of worlds (`lkN`) and the oracle. Pre-registered

This block is fresh: no arm has used it.

**Question 1 (Mendon):** does Conan's strength scale predictably with the number of sampled worlds? If it does, a player-facing difficulty slider could use it.
- Every arm runs `vc-plan`'s settings plus `lkAny` (anytime search: at the 10 s limit, decide on the samples every candidate has completed).
- Values tested: `lkN` = 1, 2, 4, 8 and 16.
- Each arm is reported against the Contender A/A on this block (`a72`), with win rate, Wilson interval and thinking time.
- `p72` (`vc-plan` without `lkAny`) is the reference that shows what `lkAny` alone does.
- Prediction: win rate rises from `lkN` = 1 to 4 and flattens after that. At 16, the 10 s limit truncates many searches.

**Question 2:** how much would perfect sampling be worth? `o72` is the oracle: `vc-plan` with `lkN=1,lkK=5`, whose search plays out the real match (`L5R_TOURN_ORACLE=1`).
- It is a diagnostic, never a player.
- If the oracle wins far more than `p72`, sampling is the bottleneck. If not, the bottleneck is the evaluation and the candidates.

The value-model training data comes from 1,456 Contender games on blocks 8000–8003 (`L5R_TOURN_SNAP`). Those blocks are not used for evaluation.

## Results below this line
