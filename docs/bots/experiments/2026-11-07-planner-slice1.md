# 2026-11-07 planner, slice 1 (pre-registration)

- Issue / branch / arm: #1185, `claude/1185-planning-bot`, arm `planner` (`BotContext.UsePlanner`, default off).
- Champion: `main` (flag off), commit recorded below at run time.
- **Hypothesis.** The champion scores each action alone: it has no notion of what the turn is for, so it buys by fixed per-type scores that ignore the SHAPE of its Gold (a 5 and a 3 cannot pay for two
  4-cost cards), ignore whether it is ahead or behind in material, and never re-thinks. Mendon's interview (round 1-3, see the issue) describes a player who builds a plan after the Events phase and
  revisits it after a Border Keep refresh, the Action phase, each battle and the attack phase.
- **Change (slice 1 only; reads only the seat's own PlayerView).** A `TurnPlan` (intent Attack / Defend / Develop; Gold and its shape; purchase slate) rebuilt whenever its inputs change. Only the
  Dynasty-phase purchase decision uses it: a card in the slate scores above every other purchase, in value order (Holdings by efficiency: about one Gold above production is fair; Personalities by Force,
  Personal Honor and printed threatening or Battle abilities, weighted up when behind in Force or Personalities and down when well ahead); a card outside the slate is bought only with Gold the slate leaves.
  The slate is the best-valued subset of the row that the Gold's shape can pay for. Nothing else in the champion changes; the Action and Attack phases are untouched in this slice.
- **Expected effect size:** 0 to +3 points against the champion. A purchasing-only change; the lesson of rounds 1-4 is that single rules rarely move the win rate. This run exists to show that the
  planner is sound and what the purchase slice alone is worth, before the Action-phase, battle and re-plan slices are built on it.
- **Decks / situations it should help:** decks with uneven Gold (a Stronghold plus one or two Holdings) and rows with several affordable cards; neutral where the champion already buys everything.
- **Decisive test:** pairing `same`, mode `dev`, `L5R_TOURN_SEED_START=60 L5R_TOURN_SEEDS=12` (= 864 pairs, 1,728 games), primary metric pairs won both vs lost both (exact one-sided sign test,
  p < 0.025), win rate >= 51%, zero bot stalls and no more of any other soundness finding than the champion, no deck (>= 40 decided games piloted) below 35% where the champion is at 50% or more,
  provinces-left and Personalities-in-play margins (challenger minus champion, decided games) not negative. Fixed sample, no extension. The 144-game smoke run (`L5R_TOURN_SEED_START=500`,
  `L5R_TOURN_SEEDS=1`, independent pairing) is a soundness check only and is not used to tune.
- **Confirmation (only if dev passes):** mode `holdout`, pairing `same`, `L5R_TOURN_SEED_START=40 L5R_TOURN_SEEDS=10` (600 games): one-sided p < 0.10, win rate >= 50%, zero bot stalls.
- **Wall clock estimate / token budget:** about 35 minutes for the dev run; tokens only for reading the report.
- **Decision rule:** promote-candidate only if dev and confirmation both pass; otherwise recorded as negative or inconclusive and the flag stays off. Promotion is never automatic.

## Result (fill in after the single run; the plan above is not edited)

- Command, output files, wall clock, tokens:
- Outcome: promote-candidate | negative | inconclusive. Numbers:
