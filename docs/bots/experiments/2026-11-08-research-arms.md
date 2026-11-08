> **Status (2026-11-08): dead end, stopped.** The arms named here (`rec1-discard2` ... `contender2`) were never merged; their code is preserved on the remote branch `claude/1185-research-arms` (PR #1188, closed unmerged). Only this record is on main.

# 2026-11-08 research arms (pre-registration)

- Issue / branch: #1185, `claude/1185-research-arms`. Arms: `rec1-discard2`, `rec2a-attack-add`, `rec2b-attack-veto`, `rec3-honor30`, `rec4-presence`, `rec5-nolost`, `rec7-open2`, `rec8-proclaim` (each the Contender plus one switch;
  `BotContext.Contender(...)`), then one integrated arm `contender2`. Source of the ideas: `docs/bots/research/2026-11-08-ccg-strategy-recommendations.md`. Recommendation 6 (defender count) is not built: the champion's informed defence
  already sends defenders only where they win or save a Province (docs/BOT_LOGIC.md, section 6).
- Champion: `main` at d257a868 (flags off). Baselines on the same games: the champion against itself (`champion`) and the Contender (`contender`, the parts of the planner that paired testing found harmless).
- **Method (as learned in docs/bots/CONTENDER.md).** Every arm is judged on the games whose outcome changed against a baseline on the SAME seed block (`tools/bots/compare_tournament.py`), never on a win rate against 50%.
- **Stage 1, screening (one change at a time).** Mode `dev`, pairing `independent`, `L5R_TOURN_SEED_START=3000 L5R_TOURN_SEEDS=1` (182 pairs x 2 = 364 games per arm), fresh block. Primary: net games
  (gained minus lost) against the `contender` baseline on the common decided games; also reported against the champion, with the exact McNemar two-sided p. The screen is underpowered by design (a switch that changes a few games
  cannot reach significance in 364): it chooses what to integrate, it does not prove anything.
  - **Selection rule, fixed now:** a switch is integrated if its net against the Contender is **+3 or more** and it has no soundness finding the Contender lacks (bot stall, illegal command, exception, decision cap).
    R2a and R2b are the two halves of one decision: only the one with the larger net can be integrated, and only if it is itself +3 or more. A switch at +2 or below is left out, however plausible. Expect one or two false includes
    among eight arms by chance; stage 2 is what guards against that.
- **Stage 2, final arena.** Arm `contender2` = the Contender plus the selected switches, against the champion A/A and the Contender on a fresh block: `L5R_TOURN_SEED_START=5000 L5R_TOURN_SEEDS=4` (1,456 games each), pairing
  `independent`, mode `dev`. Primary: `contender2` against the champion's baseline, net games, exact McNemar one-sided (arm better) **p < 0.025**; secondary: against the Contender; soundness findings = 0 beyond the champion's;
  no deck (>= 40 decided games piloted) where the arm's net against the champion is below -5. Fixed sample, no extension, run once.
- **Decision rule:** promote-candidate only if the primary passes and the soundness and deck conditions hold, and then only a proposal (a holdout confirmation follows, and promotion is never automatic); otherwise negative or
  inconclusive, flags stay off. If no switch is selected in stage 1, stage 2 is not run and the result is recorded as negative.
- **Expected effect:** small. The champion-versus-Contender difference so far is -4 games in 364; most switches change under 15 outcomes in 364. Honest expectation: stage 1 finds zero to two switches with a net of +3 or more.
- **Wall clock:** about 3 hours of machine time in total (stage 1 about 1 hour with four arms at a time on 16 cores, stage 2 about 2 hours).

## Result (fill in after the runs; the plan above is not edited)

- Commands: stage 1, ten runs of `L5R_TOURN_SEED_START=3000 L5R_TOURN_SEEDS=1` (champion, contender and the eight arms; four at a time on 16 cores, about 50 minutes). Stage 2, three runs of
  `L5R_TOURN_SEED_START=5000 L5R_TOURN_SEEDS=4` (contender2, champion, contender; three at a time, about 45 minutes). Output files `artifacts/bots/scr-*` and `fin-*` (not committed).
- **Stage 1** (364 games each; net = games gained minus lost on the common decided games; the champion A/A scored 48.8% on this block, and the Contender against the champion was +44/-41):

| Arm | vs Contender (gained / lost, net, p two-sided) | vs champion (net, p) |
| --- | --- | --- |
| rec1-discard2 | 29 / 54, **-25**, 0.008 | -22, 0.033 |
| rec2a-attack-add | 6 / 14, -8, 0.115 | -5, 0.67 |
| rec2b-attack-veto | 11 / 6, **+5**, 0.33 | +8, 0.46 |
| rec3-honor30 | 1 / 3, -2, 0.63 | +1, 1.0 |
| rec4-presence | 0 / 0, 0 (changed no game: the trigger almost never occurs), 1.0 | +3, 0.83 |
| rec5-nolost | 7 / 17, -10, 0.064 | -7, 0.53 |
| rec7-open2 | 10 / 11, -1, 1.0 | +2, 0.91 |
| rec8-proclaim | 0 / 2, -2, 0.50 | +1, 1.0 |

  No soundness findings in any arm (0 stalls, illegal commands, exceptions, decision-cap or setup failures; 0 to 2 games out of time).
- **Selection (the rule fixed above):** only `rec2b-attack-veto` (+5) reached +3, so `contender2` = the Contender plus the veto-only half of the attack estimate. The others were left out.
- **Stage 2, the final arena** (1,456 games per arm, fresh block 5000, champion A/A 721-728 = 49.8%):

| Comparison | Gained | Lost | Net | p (two-sided) |
| --- | --- | --- | --- | --- |
| contender2 against the champion (primary) | 125 | 189 | **-64** | **0.0004** |
| contender against the champion | 122 | 180 | -58 | 0.0010 |
| contender2 against the contender | 26 | 32 | -6 | 0.51 |

  contender2 won 45.3% of decided games (656-794), the Contender 45.8% (663-787), the champion A/A 49.8%. Soundness: 0 stalls, illegal commands, exceptions, decision-cap or setup failures in all three;
  6 to 7 games out of time per arm (three arms sharing the machine; the same in all). By deck, contender2 gained more than it lost against the champion with only Crab Control (+12/-9), Scottish Lion (+12/-8) and
  Pinned Lion (+8/-8, even); the rest were net negative, the worst Spider Conquer (+8/-21), Lion preset (+5/-18) and Classic Lion (+5/-15).
- **Outcome: negative.** The primary criterion fails in the wrong direction (p one-sided for the arm being better = 0.9999), so `contender2` is not a promotion candidate and the flags stay off.
- **What the larger sample changed.** The earlier 364-game screens put the Contender at net -4 (block 1700) and +3 (block 3000) and read it as neutral; at 1,456 games it is -58 (p = 0.001), about four points of win rate. The
  small screens could not see it: the purchase slate, triage and the rest each lose a few games, and together they lose a clear number. The veto-only attack estimate added nothing measurable on top (-6, p = 0.51).
  Four of eight research switches were harmless or inert, three lost games (discard v2 clearly), and the one that screened positive did not survive a fair sample. **Nothing proposed from the CCG strategy writing beat the
  champion.**
