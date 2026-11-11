# Conan tuning (issue #1206), 2026-11-10

Conan is the Contender with battle lookahead (`docs/bots/CONAN.md`). It was merged in #1207 at 77.7%, 76.6% and 79.3% against the Champion on blocks 7000, 7100 and the holdout. The Contender scored 66.9%, 64.0% and 67.1% on the same games.

This round tested the opt-in search settings. Each arm runs Conan plus one setting, bot against Champion, with `L5R_TOURN_BUDGET=20`. Each arm is compared with Conan on the same games: the net is the games the arm won that Conan lost, minus the reverse.

## Round 1: block 7000 ×1 (364 games), against Conan (`k-look`)

| Arm | Setting | Win rate | Net vs Conan | p (two-sided) |
| --- | --- | --- | --- | --- |
| `i-def` | `lkDef`: also search the defenders' assignment (assign or pass) | 78.6% | +6 | .24 |
| `i-opp` | `lkOpp`: the other seat is played by the Champion in playouts | 77.9% | +4 | .45 |
| `i-atk` | `lkAtk`: also search whether to attack | 76.2% | −2 | .83 |

None of these differences is significant on 364 games. Attack search was dropped.

The arms ran three at a time on 10 cores, which took about two hours. Lookahead games average about 90 s against 19 s for the Contender, so each round can test only a few settings.

## Round 2: block 7100 ×1 (unseen), against Conan (`v-conan`)

| Arm | Setting | Win rate | Net vs Conan | p (two-sided) |
| --- | --- | --- | --- | --- |
| `j-def` | `lkDef` | 78.2% | +6 (12–6) | .24 |
| `j-defopp` | `lkDef,lkOpp` | 78.6% | +7 (19–12) | .28 |

`lkDef` gained 6 net games on both blocks, 24–12 in total (two-sided p about .07). That makes it the best candidate to join Conan's defaults. Adding `lkOpp` contributed +1 and was not kept.

## Holdout check: 9100 ×1 (180 games), against Conan (`h-conan`)

`lkDef` scored 80.0% against the Champion on held-out decks: 3–2 against Conan, net +1. It did not lose anything there, but the gain is not confirmed either. Over all three blocks the total is 27–14, net +13.

## Next

- **Make `lkDef` default (proposed, not applied).** It shows small, consistent gains and no losses. A larger holdout run would settle it.
- **Make lookahead faster.** About 60% of a playout is the engine building legal action lists. Cheaper playouts would allow more samples and more tuning per hour.
- **Score defence options beyond assign-or-pass.** For example: the Contender's assignment, everything on the threatened Province, or the cheapest blocker.
- **Add purchase planning with Personality roles** (attack, defend, keep pace, keyword optionality). This is the second part of the plan in #1206.


## Round 3: faster playouts, defence shapes, Personality roles

### Faster playouts (engine, no behaviour change)

A CPU trace of one Conan game showed where lookahead spends its time:

| Work | Share of lookahead time |
| --- | --- |
| The engine building the legal-action list | about 52% |
| Building player views | about 32% |
| Executing commands | about 18% |

Within one action-list query, 93% of the Gold quotes repeat an earlier quote exactly.

`QueryScope.cs` now remembers a fixed quote's payment options and sources for the length of one `QueryActions` call. This is the same mechanism already used for payment tokens (#888), and `L5R_VERIFY_QUERY_SCOPE=1` checks it too. Playouts also read the acting seat and the phase from engine scalars instead of building a view.

- The bot-profile listing hash is equal before and after over six seeded matches, so the action lists are unchanged.
- The core and AI suites pass under `L5R_VERIFY_QUERY_SCOPE=1`.
- The Conan game on seed 71010108 plays the same moves in 17 s instead of 23 s.

`lkDrop` (successive halving: stop playing out a clearly trailing candidate after half the samples) saved only about 8%. It stays off.

### Personality roles (Mendon, 2026-11-10), on the Contender, block 7000 ×2 (727 games), against the Contender

All three role rules feed the planner's Personality valuation (`RoleValue` in `TrainingOpponent.Planner.cs`):

- **`rkw` (keyword optionality):** a keyword that hand actions need from their performer and that no Personality in play has.
- **`roff` (offence):** Force towards taking the opponent's weakest Province.
- **`rdef` (defence):** Force towards holding off a larger enemy army.

| Arm | Net | Games changed |
| --- | --- | --- |
| `rkw` (25) / `rkw=80` | −1 / −2 | 13 / 14 |
| `roff` (6) / `roff=20` | +3 / 0 | 7 / 16 |
| `rdef` (6) / `rdef=20` | +2 / +2 | 6 / 6 |
| all three | 0 | 12 |

Even at three times the weight, these rules change fewer than 2% of games, and none changes the result. They stay opt-in. In this arena, the Contender's purchases are decided by Gold shape and its existing push for bodies. A choice between two affordable Personalities that differ in role or keyword is rare.

## Round 4: defence shapes and the plan-driven settings

### `lkVar` (defence shapes) on top of `lkDef`, against `lkDef` alone

| Block | Win rate | Net |
| --- | --- | --- |
| 7000 ×1 | 79.7% | +4 (9–5) |
| 7100 ×1 | 79.1% | +3 (12–9) |

`CV` below is the Contender defaults plus `look,lkDef,lkVar`.

### Plan-driven settings (Mendon, 2026-11-10), block 7000 ×1, against `CV` (`x-var`, 79.7%)

| Arm | Setting | Win rate | Net |
| --- | --- | --- | --- |
| `p-plan` | `lkPlan`: the attack chosen by search and then followed | 80.4% | +3 (23–20) |
| `p-intent` | `lkIntent`: playouts scored for the plan's intent | 78.8% | −3 (1–4) |
| `p-buy` | `lkBuy`: purchases played out through the next attack | 77.4% | −10 (21–31) |
| `p-all` | all three (355 of 363 games; stopped, see below) | 80.8% | +4 (30–26) |

None of these is significant. The attack plan holds its own but does not yet beat the Contender's attack rules plus battle search. Intent weighting changes almost nothing. Purchase search loses games: a one-turn playout scored with the material weights pushes it towards bad purchases.

The four arms ran at once, so the per-search times measured in this batch (median about 3 s) mostly measure load. Single games on a quiet machine are in the next section.

## Hangs: diagnosis and fixes

The `p-all` batch stopped finishing games at 15:08. A CPU trace of the stuck process, and of three replays of other long games, showed the same place: `MaxProvincesCavalryDestroys`, under the Contender's cavalry last stand (`CavalryLastStandPlan`).

- That search tried every subset of the attacker's Cavalry at every Province, with no memory. The last stand calls it once for each assignment of defenders (up to 4^8).
- With many Cavalry, one decision ran for hours. The Contender uses the same code without lookahead, so a practice game against a Cavalry-heavy deck could stall too.
- `Parallel.For` gave each worker a block of games in advance, so the one stuck game also held back the 7 games queued behind it.

A trace of a whole long game (Crane Dominance vs Crab Control, turn 22) showed a second cost: the purchase planner's `Payable` took about 90% of the game. It tried every group of Gold sources, which is exponential in the number of Holdings.

Fixes:

- **Cavalry reach.** The search tries only the minimal Cavalry groups for each Province, and remembers its results by (Province, Cavalry used) and by Province needs. It has a fixed step limit; past it, the last stand keeps the best defence found so far.
  - It matched the old search on 3,000 random boards.
  - `cavalry-defense/LastStandIsBounded` (12 Cavalry against 8 defenders) runs in 0.4 s. The old code had not finished it after 5 minutes.
- **Payable.** Sources of equal value are interchangeable, so the search runs over how many of each value are left.
  - It matched the old search on 4,000 random cases.
  - `planner/ManySources` (20 Holdings) runs in under 0.1 s. The old code had not finished it after 120 s.
- **Search time limit, `lkT` (10 s).** A search past its limit is abandoned, and the Contender's own choice stands. A player never waits longer than that on one decision.
- **Arena cutoff.** `L5R_TOURN_BUDGET` now also sets `BotContext.Deadline`, which the bot checks inside its decisions. A runaway game is stopped mid-decision, recorded as `killed` and logged with its stack. A game that ignores the cutoff is named on stderr every minute. Workers take one game at a time.

Replays of the five longest games, each alone:

| Game | Setting | Before | After |
| --- | --- | --- | --- |
| Crane Dominance vs Crab Control 71500041 | `CV` | 560 s | 34 s |
| Crab Control vs Unicorn Maidens 71000034 | `lkBuy` | over 25 min, unfinished | 253 s |
| Lion Legacy vs Unicorn Maidens 71000139 | `lkBuy` | over 25 min, unfinished | 61 s |
| Classic Lion vs Crane Dominance 71000159 | `lkPlan` | 257 s | 142 s |
| Unicorn Maidens vs Crab Control 71500119 | all three | over 25 min, unfinished | 302 s |

Only the `lkBuy` games still reach the 10 s search limit (8 and 11 times). Their playouts run through the whole next turn, and the time goes to the engine building legal actions, not to a bot loop.

**Cutoff for tuning runs:** 20 minutes per game. That is above every Conan game in these batches that was not stuck (the longest was 965 s with four arms running at once), and the search limit now bounds the rest.
