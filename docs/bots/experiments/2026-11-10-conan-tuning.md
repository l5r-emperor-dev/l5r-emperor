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

