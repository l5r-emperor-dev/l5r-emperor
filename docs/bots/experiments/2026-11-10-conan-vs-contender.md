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

### Block 7300 results (2026-11-11)

`p73` (`vc-plan`'s settings) won 72.8% against the Contender. Three arms ran at a time.

| Arm | Win rate vs Contender | Net vs `p73` | p | Search median / p90 | Thinking per game (mean) |
| --- | --- | --- | --- | --- | --- |
| `p73` | 72.8% | — | — | 1.3 / 4.3 s | 55 s |
| `v73` (+ `lkVal`) | 67.3% | **−20** | .009 | 3.0 / 10.0 s | 115 s |
| `v73-n8` (+ `lkVal,lkAny,lkN=8`) | 72.0% | −3 | .78 | 5.2 / 10.0 s | 165 s |
| `n73-n8` (+ `lkAny,lkN=8`) | 71.7% | −4 | .61 | 5.1 / 10.0 s | 156 s |

Reading:
- **`lkVal` as fitted is worse than the hand-set score** (−20, p = .009). It was fitted on start-of-turn (Dynasty) positions but scores positions at the end of an attack, and as a probability it compresses the differences between candidates that the material score keeps apart. Its searches also ran longer and, without `lkAny`, more of them hit the 10 s limit and fell back to the Contender's choice. Negative result; `lkVal` stays off.
- **8 worlds did not repeat.** +13 on 7200, −4 here: +9 over 728 games, not a reliable gain at twice the thinking time. Worlds stay at 4.

## Block 7400 ×1: more attack shapes (`lkShapes`). Pre-registered

From Mendon's Lion vs Dragon games: split attacks, where each unit goes alone to a Province it beats on its own Force, and a pair of the strongest two at the weakest Province, keeping the rest home to defend. `lkShapes` adds both as `lkPlan` candidates. Arms, compared with `p74` (`vc-plan`'s settings):
- `sh74`: + `lkShapes`
- `sh74-k5`: + `lkShapes,lkK=5` (more candidates kept in Battle searches, so the new shapes are not crowded out)
- `k74`: + `lkK=5` alone, to separate the two

An arm goes forward at net +8 or better.

### Block 7400 results (2026-11-11), and a flaw in the screens since block 7200

`p74` won 70.1% against the Contender.

| Arm | Win rate | Net vs `p74` | p | Search median / p90 | Searches over the time limit |
| --- | --- | --- | --- | --- | --- |
| `p74` | 70.1% | — | — | 1.4 / 4.6 s | 219 |
| `sh74` (+ `lkShapes`) | 66.8% | −12 | .03 | 3.0 / 10.0 s | 1,193 |
| `sh74-k5` (+ `lkShapes,lkK=5`) | 67.0% | −11 | .14 | 3.9 / 10.0 s | — |
| `k74` (+ `lkK=5`) | 68.4% | −6 | .46 | 3.9 / 10.0 s | 1,723 |

**These numbers are not a fair test.** `par.sh` runs the reference first, alone, then the arms three at a time. Each arm uses 9 of the 10 cores, so with three arms each search had about a third of the CPU. The 10 s limit is wall-clock time, so the arms' searches hit it 5–8 times as often as the reference's, and each such search fell back to the Contender's choice. The arm searches' doubled median time in 7300 and 7400 comes from this contention, not from the settings.

The same bias applies to every arm compared with `p72`, `p73` and `p74`, so those negative results are not evidence against the settings. Block 7000 ran two arms at a time against the CV reference, so it is affected too, less. `vc-plan` won by +30 despite this. The nets among arms that ran together (for example `s72-n8` against `s72-n4`) are fair.

Fix: `lkB`, a search budget counted in playout decisions instead of seconds. It does not depend on the machine or its load. From now on, arms and their reference run with the same `lkB` and a generous wall-clock limit (`lkT=60`) as a safety net only. `cal90` (block 9000, run alone) calibrates `lkB` to the work an uncontended search does in 10 s.

## Block 7500 ×1: the 7300/7400 ideas again, on a work budget. Pre-registered

Calibration (`cal90`, block 9000, alone: 73.4%): a search makes a median of 156 playout decisions, 334 at p90 and 503 at p98, at about 91 decisions a second. The new reference `r75` is `vc-plan` + `lkB=600,lkT=60`. Arms:
- `sh75`: + `lkShapes`
- `v75`: + `lkVal`
- `n75`: + `lkN=8,lkB=1200` (twice the worlds and twice the budget)

An arm goes forward at net +8 or better.

### Block 7500 results (2026-11-11): fair now, and still nothing

`r75` won 75.0% against the Contender. Searches that hit a limit: `r75` 96, the arms 136–198, out of about 10,000 each, so the budget evens the field.

| Arm | Win rate | Net vs `r75` | p | Search median / p90 | Thinking per game (mean) |
| --- | --- | --- | --- | --- | --- |
| `r75` | 75.0% | — | — | 1.3 / 3.8 s | 52 s |
| `sh75` (+ `lkShapes`) | 75.3% | +1 (5–4) | 1.0 | 2.7 / 8.2 s | 112 s |
| `v75` (+ `lkVal`) | 70.1% | **−18** | .02 | 2.7 / 9.0 s | 122 s |
| `n75` (8 worlds, `lkB=1200`) | 74.2% | −3 | .73 | 3.8 / 13.1 s | 166 s |

Reading: on equal terms, `lkShapes` changes almost nothing (9 games), `lkVal` is really worse, and more worlds with more budget do not help. Searching the attack, defence and battle decisions harder has stopped paying. The losses come from decisions the search does not make: development and the Action phase.

Also #1216 (Mendon's Lion vs Mantis game against Hard): Tomaru moving home instead of staying for a ranged kill was correct, since Lion held Tactical Assault. See the issue.

## Block 7600 ×1: searching other decisions. Pre-registered

Reference `r76` = `r75`'s settings. Arms:
- `act76`: + `lkAct` (Action-phase moves searched, played out through the end of this turn's attack)
- `buy76`: + `lkBuy,lkB=2000,lkAny` (purchases searched through the next attack, with the budget that horizon needs)
- `wide76`: + `lkK=5,lkB=1500` (more candidates in each searched decision, with the budget for them)

An arm goes forward at net +8 or better.

### Block 7600 results (2026-11-11): searching more decisions pays

`r76` won 70.6% against the Contender.

| Arm | Win rate | Net vs `r76` | p (two-sided) | Search median / p90 | Thinking per game (mean / max) |
| --- | --- | --- | --- | --- | --- |
| `r76` | 70.6% | — | — | 1.3 / 3.9 s | 55 / 449 s |
| `act76` (+ `lkAct`) | 74.1% | **+12** (33–21) | .13 | 2.4 / 8.2 s | 193 / 2,362 s |
| `buy76` (+ `lkBuy,lkB=2000,lkAny`) | 75.0% | **+16** (45–29) | .08 | 2.8 / 9.6 s | 188 / 1,075 s |
| `wide76` (+ `lkK=5,lkB=1500`) | 74.7% | **+15** (25–10) | .017 | 3.6 / 10.7 s | 145 / 1,241 s |

All three pass the +8 screen. `act76` had one game stopped at the 40-minute per-game cutoff.

**Correction:** a knob takes the first `name=value` rule it finds, and the reference settings already include `lkB=600`. So the `lkB=2000` and `lkB=1500` added to `buy76` and `wide76` had no effect: every arm here ran on the same 600-decision budget, and the gains come from the extra decisions searched, not from more budget. (`n75`'s `lkB=1200` did apply, because its settings were built without `lkB=600`.)

## Block 7700 ×1: the combinations. Pre-registered

Reference `r77` = `r75`'s settings. Arms (each + `lkAny`; the `lkB=2000` also passed has no effect, as above, so the budget stays 600):
- `all77`: + `lkAct,lkBuy,lkK=5`
- `ab77`: + `lkAct,lkBuy`
- `kb77`: + `lkBuy,lkK=5`

An arm goes forward at net +8 or better. The best goes to an SPRT and the Tier 3 confirmation.

### Block 7700 results (2026-11-11): `lkBuy` with `lkK=5` is the new best

`r77` won 67.9% against the Contender.

| Arm | Win rate | Net vs `r77` | p (two-sided) | Search median / p90 | Thinking per game (mean / max) |
| --- | --- | --- | --- | --- | --- |
| `r77` | 67.9% | — | — | 1.3 / 3.8 s | 52 / 408 s |
| `all77` (+ `lkAct,lkBuy,lkK=5`) | 73.4% | **+20** (52–32) | .038 | 3.1 / 11.0 s | 315 / 3,013 s |
| `ab77` (+ `lkAct,lkBuy`) | 71.4% | +13 (49–36) | .19 | 2.8 / 9.5 s | 283 / 2,558 s |
| `kb77` (+ `lkBuy,lkK=5`) | 74.7% | **+25** (54–29) | .008 | 3.8 / 11.8 s | 234 / 2,431 s |

All three pass the +8 screen, and `kb77` is the best. Adding `lkAct` to it (`all77`) costs 81 s a game and gains nothing. Across 7600 and 7700, searching purchases and searching more candidates each add about +15 net, and together about +25. `lkAct` is dropped.

**Change to the plan.** At about 75%, `kb77` would fail the 80% Tier 3 claim, and Tier 3 costs about 2,200 Conan games at 4 minutes of thinking each. So Tier 3 waits for a candidate that screens at 80% or better. The SPRT continues on fresh blocks: block 7800's `k78` against `r78` is its first chunk.

## Block 7800 ×1: economy or bodies, by the table. Pre-registered

Reference `r78` = `r77`'s settings (`PB`). `K` = `PB` + `lkAny,lkBuy,lkK=5` (`kb77`'s settings without the ineffective `lkB=2000`). Arms:
- `k78`: `K`. Compared with `r78`, this is the first SPRT chunk for `K` (p1 0.6, α = β = 0.05).
- `mix78`: `K` + `lkMix,lkDedup,lkBH=4` (the best Holding and the best Personality are always among the purchases searched, and purchase playouts run 4 turns ahead).
- `ec78`: `K` + `econctx` (in Conan's own rules and its playouts, the Contender's economy-first purchase gives way when the table is contested).

`mix78` and `ec78` are compared with `k78` on the same games, and go forward at net +8 or better.

Contender screen (Contender against the default Contender, the same block): `c78` = `T2`, `cx78` = `T2,econctx`. `econctx` goes forward for the Contender at net +8 or better. It becomes a Moderate default only with Mendon's approval.

### Block 7800 results (2026-11-12): `K` holds a little on fresh games; `lkMix` hurts; `econctx` does nothing

`r78` won 72.8% against the Contender.

| Arm | Win rate | Net | p (two-sided) | Thinking per game (mean / max) |
| --- | --- | --- | --- | --- |
| `r78` | 72.8% | — | — | 55 / 400 s |
| `k78` (`K`) vs `r78` | 73.8% | +5 (32–27) | .60 | 235 / 1,181 s |
| `ec78` (`K` + `econctx`) vs `k78` | 74.0% | 0 (3–3) | 1.0 | 236 / 1,180 s |
| `mix78` (`K` + `lkMix,lkDedup,lkBH=4`) vs `k78` | 68.6% | **−18** (23–41) | .033 | 299 / 1,176 s |

Five of `k78`'s games and twelve of `mix78`'s ended undecided at the 20-minute cutoff.

- **SPRT for `K`**, fresh blocks only (block 7700 chose `K`, so it does not count): after chunk 1, LLR −0.19 with bounds ±2.94, so the test continues.
- **`lkMix` is rejected.** Purchase playouts 4 turns long are slower and noisier, and they cost wins.
- **`econctx` changed 6 of Conan's games, and 12 of the Contender's (Contender screen: `cx78` 51.4% against `c78` 51.9%, net −2).** It only removes the economy bonus. Without that bonus, the Contender's base purchase score usually still picks the Holding, so the choice hardly changes. An earlier fixed rule that bought bodies first (`firstbody`, `bodyfirst`; 2026-11-09 grill) lost up to −41. The next step is a judgment comparing the two futures (roadmap: an explicit race estimate), not a stronger fixed rule.

## Block 7900 ×1: SPRT chunk 2, more candidates, more budget. Pre-registered

Reference `r79` = `PB`. Arms:
- `k79`: `K` (SPRT chunk 2 against `r79`)
- `k879`: `PB` + `lkAny,lkBuy,lkK=8`
- `b979`: `PB` with `lkB=900` + `lkAny,lkBuy,lkK=5`

`k879` and `b979` are compared with `k79` and go forward at net +8 or better.

### A review of `k78`'s 94 losses (2026-11-12, scripted over all games, 16 logs read)

How the games were lost: 83 on Provinces, 8 on Honor, 3 on Dishonor. `r78` also lost 67 of them. In 30 of the 32 games `K` lost that `r78` won, the first move that differed was a purchase on turns 1–4.

| Pattern | Losses | `r78` won the same game |
| --- | --- | --- |
| Lost the Province race: Conan ahead or level, and its last Provinces fell while its army was bowed from attacking | 32 | 10 |
| Ahead at some point, then lost defending with units at home | 24 | 9 |
| Out-developed: behind on Personalities and Force from turn 7 | 16 | 2 |
| Lost to an Honor or Dishonor clock while ahead on Force | 11 | 4 |
| Behind throughout | 11 | 0 |

Two other measures:
- **Undefended against an attack:** turns on which the opponent attacked with no Conan Personality left unbowed came to 205 for `K` and 158 for `r78`.
- **Cards held in hand:** won and lost games held about the same number of battle tricks for four turns or more, so holding cards does not explain the losses.

Changes that follow:
- `lkFar` again, now with a work budget: it lost under the old time limit (block 7000).
- A new `lkClock`: the Honor win is checked at the start of a turn, so a playout that ends the turn on 40 Honor is scored as that win; and when the opponent is projected to reach 40 within a round, only the Provinces and Honor taken from them count.
- A replay of the Honor loss with `lkClock` (`Scottish Lion Deck` against `Crane preset`, seed 79500092): `lkClock` fires but does not change the game. Attacking gave the defender battle-won Honor and reached 40 sooner, so not attacking stayed best there. That game was lost earlier, through passivity on turns 10–14.

### Block 7900 results (2026-11-12): the SPRT rejects `K`

`r79` won 72.7% against the Contender.

| Arm | Win rate | Net | Thinking per game (mean) |
| --- | --- | --- | --- |
| `k79` (`K`) vs `r79` | 70.3% | −8 (32–40) | 232 s |
| `k879` (`lkK=8`) vs `k79` | 70.4% | +1 (10–9) | 244 s |
| `b979` (`lkB=900`) vs `k79` | 70.2% | 0 (2–2) | 235 s |

- **The SPRT for `K`**, on the fresh blocks 7800 and 7900 (716 games): gained 64, lost 67, net −3. LLR −3.28 is below −2.94, so H0 is accepted: `K` is no better than `PB`.
- **Why the screens misled:** the +15 to +25 in blocks 7600 and 7700 came from picking the best of three arms in each block on 364 games, where a net of ±10 is common.
- **The lesson:** a screen's best arm needs the fresh-block SPRT before it is built on. `K` costs four times `PB`'s thinking for nothing, so later arms build on `PB` again.
- **Budget and candidates:** `lkB=900` changed only 4 games, so the 600-decision budget is rarely what limits a search. `lkK=8` adds nothing over 5.

## Block 8100 ×1: `lkFar` on a budget, and `lkClock`. Pre-registered (revised before any game was played: built on `PB`, not `K`)

Reference `r81` = `PB`. Arms:
- `far81`: `PB` + `lkAny,lkFar`
- `clk81`: `PB` + `lkClock`
- `fc81`: `PB` + `lkAny,lkFar,lkClock`

Each arm is compared with `r81` and goes forward at net +8 or better, then to an SPRT on fresh blocks before anything builds on it.

### Block 8100 results (2026-11-12): `lkFar` gains on decided games but stalls; `lkClock` is inert

`r81` won 74.5% against the Contender.

| Arm | Decided | Win rate | Net vs `r81` | p | Thinking per game (mean / max) |
| --- | --- | --- | --- | --- | --- |
| `far81` (`lkAny,lkFar`) | 334 | 76.0% | **+12** (29–17) | .10 | 294 / 1,170 s |
| `clk81` (`lkClock`) | 364 | 74.5% | 0 (2–2) | 1.0 | 112 / 915 s |
| `fc81` (both) | 335 | 74.6% | +8 (30–22) | .33 | 290 / 1,177 s |

- **`lkFar` makes Conan cautious.** 30 of its games reached the 20-minute cutoff, at turns 25 to 65. Of its decided games, 18 lasted 30 turns or more, against 3 for `r81`, and its median game ran 16 turns against 14. `r81` won 29 of the 30 undecided games, so `far81`'s +12 is not yet a result.
- **`lkClock` changed 4 games and is dropped.**

## Block 8200 ×1: `lkFar`, with the time to finish. Pre-registered

First, `far81`'s 30 undecided games are replayed with a 60-minute cutoff, and the `far81` result is restated with them.

Then block 8200 runs with `L5R_TOURN_BUDGET=60`: reference `r82` = `PB`, arm `far82` = `PB` + `lkAny,lkFar`. This is SPRT chunk 1 for `lkFar` (p1 0.6, α = β = 0.05), and games that still end undecided are reported.

## The direction changes: rules, not search (Mendon, 2026-11-12)

The block 8200 run was stopped partway, at Mendon's direction: "It doesn't seem like adding more thinking time is helping the bot make better decisions. We probably need to look at iterating on the contender and Conan's core rules and adding more nested if statements that account for a wider variety of scenarios."

**`far81`'s undecided games, finished.** Before the stop, the 30 `far81` games left undecided were replayed with a 60-minute cutoff. `lkFar` won 25, lost none, and 5 were still undecided. With those games, `far81` stands at about +12 against `r81`, at five times the thinking. Its effect is to keep defenders home against the counter-attack, which a rule can do without searching.

**How rules are screened now.** Each scenario rule is tested Contender against the default Contender, on block 8300 with 4 seeds (1,456 games, about 15 minutes an arm). The A/A reference `ca83` won 49.6%. The rules come from two loss reviews: `K`'s losses (above) and the losing seat's decisions in `ca83`.

| Rule | What it does | Games changed | Net |
| --- | --- | --- | --- |
| `guard` | with 2 or fewer Provinces left, the cheapest set of units that stops the opponent's whole army razing the weakest stays home (not when the attack takes the opponent's last Province, or when no guard could stop it); the rest attack only where they still beat Province and defenders | 46 | +6 (p .46) |
| `guard,guardN=3` | the same from 3 Provinces | 112 | −8 |
| `hgift` | with the attacker within 10 Honor of its win and Provinces to spare, a defence that loses after the attacker's likely removals is not made (not the last stand) | 14 | −2 |
| `cavblind` | a blind defence before the Cavalry declares plans without the defenders the attacker's hand can remove | 12 | −2 |
| `lobbycycle` | the Favor lobbied for this turn is not discarded to draw, while the opponent has an army | 5 | +3 |
| all four | | 76 | −2 |

The first `guard` screen changed no game. The Contender does not use the planner's attack (`PlannerAttack` is off), so `guard` now runs where the attack assignment is finalised, beside `deter`.

**Why the reviews' counts did not become wins.** The review found 104 games in which a losing defence fed an Honor win, but `hgift` changed only 14. A review that compares the loser's decisions with the winner's mostly measures the position, not the decision: a side that is losing makes more losing defences. None of these rules is adopted. Historically, the rules that did gain came from Mendon's own decisions at real positions: `spreadwide` and `conf` from the battle grill (+24 and +19), and `selfplayer` through `raid` from his practice games (+13).

## Block 8400 ×1: the baseline after merging main (2026-11-12)

Run after merging `origin/main` (through #1220) into the branch, as the starting point for `docs/bots/CONAN_STRATEGY_BRIEF.md`. 364 games each, `L5R_TOURN_BUDGET=20`, opponent the frozen Contender (`T2`).

| Arm | Rules | Won | Lost | Win rate |
| --- | --- | --- | --- | --- |
| `ca84` | `T2` (Contender A/A) | 192 | 172 | 52.7% |
| `r84` | `PB` = `T2` + `look,lkDef,lkVar,lkPlan,lkB=600,lkT=60` | 271 | 93 | 74.5% |

Compared on the same games, `r84` is net +79 against `ca84` (93 games gained, 14 lost). Conan is where it was on blocks 7800, 7900 and 8100 (72.7 to 74.5%): the merge changed nothing measurable.
