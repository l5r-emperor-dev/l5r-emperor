# 2026-11-27 Cavalry reserve (#1360)

- Issue / branch / arms: #1360 / `claude/1360-cavalry-reserve` / `hard` vs `conan` with TunedRules + HardRules + `cavhold,cavres,lkCav`.
- **Hypothesis:** Conan commits Cavalry in the Infantry steps (37 of 44 Cavalry at the Infantry declaration and 27 of 30 in the Infantry defence across 33 human replays) and never searches the Cavalry declaration, so it gives the opponent information and loses optionality.
- **Change (all opt-in):**
  - `cavhold`: the attacker sends only Infantry at the Infantry declaration and plans as though its Cavalry joins later. Plans and playouts hold Cavalry back.
  - `cavres`: the defender keeps Cavalry for the Cavalry defence, sizes the Infantry defence against the possible Cavalry swing, and counts units already defending in the Cavalry defence.
  - `lkCav`: the lookahead searches the Cavalry declaration with "home", "all", "enough", "reinforce" and "split" shapes.
  - Incredible Tragedy (no Cavalry segment) disables the holding.
- **Test:** independent pairing against T2, `L5R_TOURN_BUDGET=10`, both arms run together under equal load. Seeds 12100 and 12101.
- **Decision rule:** promote into `HardRules` only if Conan's paired win rate rises.

## Result

| chunk | games | Hard | with rules | gained | lost | net |
|---|---|---|---|---|---|---|
| 12100 | 604 | 68.2% | 67.9% | 13 | 15 | −2 |
| 12101 | 607 | 69.1% | 67.7% | 11 | 19 | −8 |
| total | 1,211 | | | 24 | 34 | −10 |

- The sign test on 24 against 34 is two-sided p ≈ 0.24.
- **Outcome: not positive.** The rules stay opt-in and are not promoted. The run stopped at two chunks because the trend was negative and a reversal large enough to show a gain was unlikely.
- About a quarter of games play differently, mostly by defending in the Cavalry step instead of the Infantry step. Few of them change the result.
- **Next:** screen each rule alone, since one may help while another hurts. Then read the lost games, starting with Crane preset, Phoenix Talons and Pinned Lion.

## Second pass: Cavalry takes Provinces (`cavplan`, `cavpick`)

- **Diagnosis (Unicorn decks, seeds 12100–12101):** the Cavalry dodged defenders (unopposed attacks 398 → 436) and took Provinces earlier, but contested battles fell (57 → 32) and attacks on the last Province failed more often (70% against 86%).
- **Change (opt-in):**
  - `cavplan`: carries the plan's Cavalry into its segment, in play and in the playouts, and re-targets it to take as many Provinces as it can with a margin for the defender's hand. The knobs are `cavM`, `cavMH` and `lkCN`.
  - `cavpick`: holds Cavalry only when the opponent has two or more Provinces left and the bot has `cavN` (3) or more Cavalry units ready.
- **Result, all five rules (seeds 12102–12103):** 20 gained, 32 lost, **net −12**. Unicorn wins fell from 114 to 106.

## Ablation (seeds 12104–12105)

| arm | gained | lost | net | Unicorn wins (of 136) | Unicorn Maidens |
|---|---|---|---|---|---|
| attack only (`cavhold,lkCav,cavplan,cavpick`) | 23 | 23 | 0 | 98 (Hard 105) | +1 / −7 |
| defence only (`cavres`) | 19 | 18 | +1 | 105 | +3 / −1 |

- Unicorn Maidens is an Honor deck. With the attack rules it sent 332 riders in the Cavalry step against Hard's 224.
- Its Military wins stayed at 11, but Military defeats rose from 8 to 13 and Honor wins fell from 48 to 42.
- The follow-up kept nobody home. Six of its seven flipped losses were the deck's own Provinces destroyed.

## Third pass: `cavkeep` (seeds 12106–12107)

- **Change (opt-in, with `cavplan`):**
  - Only the Force needed rides.
  - Leftover riders stay home.
  - Guard's units stay home.
  - Planned groups keep their Province against the defence as placed.
  - When nothing can be taken, the Cavalry stays home.
- **Arm:** attack rules plus `cavkeep`.

| chunk | games | Hard | with rules | gained | lost | net |
|---|---|---|---|---|---|---|
| 12106 | 602 | 67.7% | 67.6% | 8 | 8 | 0 |
| 12107 | 608 | 67.0% | 66.6% | 9 | 11 | −2 |
| total | 1,210 | | | 17 | 19 | **−2** |

- Unicorn Maidens is repaired: 0 gained and 1 lost. It had 6 Military defeats against Hard's 5, and its Honor wins were 49 against 50.
- Allan Unicorn, a Military deck, gained 3 and lost 5. Its Military wins fell from 42 to 38.
- Provinces taken did not rise. Unicorn decks destroyed 282 non-final Provinces against Hard's 285. Four-Province games fell from 53 to 48.
- **Outcome: not positive.** None of `cavhold`, `cavres`, `lkCav`, `cavplan`, `cavpick` or `cavkeep` is promoted. They stay opt-in.
- Across four arena runs, holding Cavalry for its segment has not taken more Provinces than Hard's practice of sending it with the plan.
