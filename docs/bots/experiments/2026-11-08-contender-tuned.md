# 2026-11-08 contender, tuned (pre-registration)

- Issue / branch / arm: #1185, `claude/1185-contender-tuning`, arm `contender` (`BotContext.Contender()`, whose default rules are now `BotContext.TunedRules`). Champion: `main`.
- **Evidence (exploratory, dev mode).** Tuning blocks 3000-3001; the 16-rule set (S13) then scored 58.5% on the unseen dev block 5000-5001 (728 games). Ablation on that block: dropping slate +17,
  selfharm +9, econT2 +20, battlepolicy+trades +28 games; dropping econ -50, shinden -12, ownprov -7, presence -5, selflock -5, selfboost -4, nostraighten/provaim -3, surereact -2.
  The combined set without the four losers scored 60.5% there (+15 against S13); also dropping lobby and openbow scored 59.0%, so both stay.
- **Hypothesis.** The tuned Contender beats the champion on decks and seeds it was never tuned on. Expected win rate about 57-60% (lower than dev, since three decks are new to it).
- **Decisive test (run once).** `L5R_TOURN_CHAL=contender L5R_TOURN_PAIRING=independent L5R_TOURN_MODE=holdout L5R_TOURN_SEED_START=9000 L5R_TOURN_SEEDS=2` = 2 seeds x 90 ordered pairs x 2
  seats = 360 games, a fresh held-out block. Sample choice: the shortest arena with at least 80% power (exact sign test, one-sided alpha .05: 197+ wins of 360 needed; power .82 at a true 57%,
  .91 at 58%). One seed (180 games) has only .57-.67 power.
- **Decision rule.** Superior with 95% confidence if the one-sided exact sign test p < 0.05 (decided games) **and** zero soundness findings (stalls, illegal commands, exceptions). Otherwise
  inconclusive or negative; no extension of this run. Promotion is never automatic.

## Result (fill in after the single run; the plan above is not edited)

- Command, output files, wall clock: the command above, `artifacts/bots/h9000-contender.{md,json,out}` (not committed), 3 min 0 s.
- Outcome: **superior** (rule met). Contender 237, champion 123 of 360 = **65.8%** (95% Wilson CI 60.8-70.5%); exact sign test one-sided p < 0.0001. Paired by seed: won both 75, lost both
  18, split 87 (McNemar p < 0.0001). Soundness: 0 bot stalls, 0 illegal, 0 exceptions, 0 decision cap, 0 setup failures (45 games had a refused-then-retried action, informational).
  Held-out decks piloted: Allan Lion MCA 34/64 (53%), Mantis Manipulation 44/64 (69%), Pinned Crane 47/64 (73%). No deck has the 20+ games the collapse rule needs below 35%; the lowest
  small samples are Crab Control 3/12 and Crane Dominance and Phoenix Talons 4/12 each.
