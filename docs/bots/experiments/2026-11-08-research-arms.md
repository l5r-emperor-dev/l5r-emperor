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

- Stage 1 table (net against the Contender and the champion, per arm), the switches selected, stage 2 numbers, wall clock:
- Outcome: promote-candidate | negative | inconclusive.
