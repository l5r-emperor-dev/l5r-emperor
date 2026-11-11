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
| `vp-hs` | plan + `lkHS` | 70.1% | −4 vs plan | .29 | 2.1 s / 6.3 s | 79 s |
| `vp-ps` | plan + `lkPS` | 70.6% | −2 vs plan | .80 | 2.1 s / 6.4 s | 79 s |
| `vp-m3` | plan + `lkM=3,lkDedup` | 69.8% | −5 vs plan | .54 | 1.7 s / 5.7 s | 65 s |

Two arms ran at a time, so their thinking times run somewhat high. No game was killed.

Notes:
- `vp-far` searched its attack plan through the opponent's next turn. At p90 its searches hit the 10 s limit, so many of them were abandoned and the Contender's own choice was played. This result does not fairly test the longer horizon.
- `clock`, `lkHS` and `lkPS` changed very few games (5, 8 and 16). None of the scoring tweaks helps. The hand-set material score seems to be the limit, which is the case for the learned value (`lkVal`).
- `vp-m3`, requiring a margin of 3 points before overriding the Contender, lost 5 net. The thin-margin overrides were helping on balance.

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

### Block 7200 results (2026-11-11)

The Contender A/A on this block scored 52.7% (192–172). The value-model export (blocks 8000–8003, Contender against Contender) scored 49.7% over 1,456 games, with no stalls. Three arms ran at a time, so thinking times run high.

| Arm | Settings | Win rate vs Contender | Net vs A/A | Net vs `p72` | Search median / p90 / p99 | Thinking per game (mean / max) |
| --- | --- | --- | --- | --- | --- | --- |
| `p72` | `vc-plan` (4 worlds, no anytime) | 73.4% | +75 | — | 1.3 / 4.0 / 10.0 s | 53 / 302 s |
| `s72-n1` | + `lkAny,lkN=1` | 72.0% | +70 | −5 | 0.7 / 2.1 / 6.4 s | 29 / 258 s |
| `s72-n2` | + `lkAny,lkN=2` | 74.7% | +80 | +5 | 1.4 / 4.0 / 10.0 s | 53 / 373 s |
| `s72-n4` | + `lkAny` (4 worlds) | 74.2% | +78 | +3 | 2.7 / 8.3 / 10.1 s | 100 / 418 s |
| `s72-n8` | + `lkAny,lkN=8` | **76.9%** | +88 | **+13 (p = .024)** | 4.6 / 10.0 / 10.1 s | 148 / 583 s |
| `s72-n16` | + `lkAny,lkN=16` | 76.9% | +88 | +13 (p = .024) | 7.3 / 10.0 / 10.1 s | 188 / 801 s |
| `o72` | oracle: the real match, `lkN=1,lkK=5` (a diagnostic, not a player) | **84.1%** | +114 | — | 0.4 / 1.4 / 5.4 s | 19 / 371 s |

Reading:
- **The number of worlds scales strength, but only a little.** One world is already 72% against the Contender, and 8 worlds reach 77%. Above 8 nothing more is gained: `s72-n16` hit the 10 s limit on 36% of its searches (3,692 of 10,124), so it really decided on about 8–10 worlds. The 8- and 16-world arms differ on 20 games, with the same net.
- **As a difficulty slider, worlds alone span about 5 points against the Contender** (72% to 77%), at 6× the thinking time. That is too narrow to be a slider on its own. The larger steps come from the bot rungs and from which decisions are searched (`lkPlan` was +30). A slider would combine these. `docs/bots/ROADMAP.md` tracks that.
- **The oracle bounds what better sampling can gain.** With perfect knowledge of hidden cards, Conan wins 84%, about 10 points above the sampled bot. Even a perfect information model leaves 16% of games lost. The rest of the gap to 80% has to come from the candidates and the evaluation, not from the sampler.
- `lkAny` alone (`s72-n4` against `p72`) changed only 9 games (+3).

## Block 7300 ×1: `lkVal` screen. Pre-registered

The value model was fitted on the Dynasty-phase snapshots of blocks 8000–8003 (`ValueModel.Weights`). On held-out games it predicts the winner in 65.8% of positions (log-loss 0.576, against 0.693 for a constant guess).

Arms on this block, compared with `p73` (`vc-plan`'s settings):
- `v73`: + `lkVal`
- `v73-n8`: + `lkVal,lkAny,lkN=8`
- `n73-n8`: + `lkAny,lkN=8`, which repeats the 7200 result on a second block

An arm goes forward when it is net +8 or better, with no soundness finding and no deck collapse.
