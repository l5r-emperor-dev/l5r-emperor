# 2026-11-07 contender (pre-registration)

- Issue / branch / arm: #1185, `claude/1185-contender`, arm `contender` (`BotContext.Contender()`; flags default off elsewhere).
- Champion: `main` (flags off). This note supersedes `2026-11-07-planner-slice1.md`, whose decisive run was never made: slice 2 was built first, then the paired analysis below made that plan obsolete.
- **Evidence behind this note (exploratory, seed block 1700 and earlier blocks; not a decisive run).** The champion played against itself scores 46.4% on block 1700, so a win rate against 50% is not
  the test (docs/bots/CONTENDER.md). Paired on the same 364 games: purchase slate -2, triage target ranking +1, printed-first +2, won-battle cap -3 (nothing significant); the attack estimate -14
  and the interview's discard rule -11 lose games and are off in the Contender; the reserve does nothing.
- **Hypothesis.** The purchase slate (Gold shape, bodies, Proclaim, clear-the-row) plus triage's target ranking and printed-first ordering is at least not worse than the champion and slightly better, because
  it spends Gold on cards that fit together and bows or destroys the unit that can still do something. Honest expectation: a net of +0 to +10 games per 364, because triage changes only 7-11 outcomes in 364
  and the slate changes more but cancels. **This is a baseline measurement, not a promotion bid.**
- **Change.** `BotContext.Contender()` with `attack`, `discard`, `reserve` and `wonCap` all false. Reads only the seat's own view.
- **Decisive test.** Paired against the champion A/A on the same fresh block: `L5R_TOURN_PAIRING=independent L5R_TOURN_MODE=dev L5R_TOURN_SEED_START=2000 L5R_TOURN_SEEDS=6` for BOTH `contender` and
  `champion` (6 seeds x 182 pairs x 2 = 2,184 games each; split between machines as 2000-2002 and 2003-2005 if wanted). Primary metric: games gained minus games lost against the baseline, exact McNemar
  sign test, one-sided p < 0.025 with the net positive; soundness findings = 0 beyond the baseline's; no deck (>= 40 decided games piloted) where the arm's net is below -5. About 0.3 games/s per
  machine on this hardware, so about 2 hours per arm on one machine. With ~3% of games differing, 2,184 games detect a net of about +30 (1.4 points); a smaller true effect will read as inconclusive.
- **Confirmation (only if it passes):** mode `holdout`, `L5R_TOURN_SEED_START=2000 L5R_TOURN_SEEDS=3`, same rule with one-sided p < 0.10.
- **Decision rule:** promote-candidate only if both pass; otherwise recorded as negative or inconclusive and the flags stay off. Promotion is never automatic.

## Result (fill in after the single run; the plan above is not edited)

- Command, output files, wall clock, tokens:
- Outcome: promote-candidate | negative | inconclusive. Numbers:
