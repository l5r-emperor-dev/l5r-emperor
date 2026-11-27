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
