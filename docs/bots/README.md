# Bot improvement loop (issue #963)

The practice bot (`src/L5R.AI`, `TrainingOpponent`) on `main` is the **champion**. A change reaches the champion only as a *challenger*: a variation behind a new `BotContext` flag that is
**off by default**, pre-registered, and then beaten against the champion in a seeded, paired, bot-against-bot tournament. No agent plays; no tokens are spent per game. Core, the wire
protocol and the UI are never touched, and the bot reads only its own `PlayerView` and printed card data.

Past lesson (docs/parked/2026-10-09-what-makes-a-good-l5r-bot.md on `claude/788-bot-battle-policy`): six rules went 137-117 (p = 0.23, inconclusive) and trade attacks 116-144 (p = 0.09).
More rules were not clearly better, so measurement comes before ideas.

## The instrument

`tests/L5R.AI.Tests/BotTournament.cs`, run through the opt-in `bot-tournament` case:

    L5R_TOURN_CHAL=<arm> L5R_TOURN_PAIRING=same|independent L5R_TOURN_MODE=dev|holdout L5R_TOURN_SEEDS=<n> L5R_TOURN_OUT=artifacts/bots/<name> \
      bash -c 'source tools/env.sh && dotnet run --no-build --project tests/L5R.AI.Tests -- --filter bot-tournament'

- **Pool:** the 10 preloaded decks the protocol was registered on (`src/L5R.Client/Content/preloaded-decks.json`) plus the Lion and Crane presets = 12 decks. Lion Legacy was added to that file later (#1021): the tournament now reads 13 decks, and the held-out three are unchanged (the hash order does not put it among them), but the dev pool and pair counts are larger than the earlier reports'. **Held out:** the three decks with the lowest SHA-256 of
  `"l5r-bot-holdout:<name>"` (a fixed rule, not a choice). They are never in `dev` mode, which plays the other 9 decks (72 ordered pairs) on seeds `1,000,000+`. `holdout` mode plays ordered
  pairs with at least one held-out deck on a separate seed range (`9,000,000+`) and is for final confirmation only.
- **Games:** for every ordered deck pair and seed, two games: the challenger in seat 0, then in seat 1. `same` pairing uses one seed for both, so deck, seat order and shuffle are identical and
  only the bot differs (most sensitive; an A/A is then two identical games, which is also a determinism check). `independent` gives the second game a fresh shuffle, so the result behaves like
  independent coin flips: this is the pairing the power note and the A/A noise floor refer to.
- **Output:** `<out>.json` (summary plus every game: seed, seats, winner, reason, turn, final Honor/provinces/Personalities, action counts) and `<out>.md`.
- **Arms:** `BotTournament.Arms`. A candidate branch registers `["<name>"] = () => new() { UseThing = true }`; `champion` is `new()`.
- **Battle records (issue #1206):** `L5R_TOURN_BATTLES=<dir>` writes one JSON line per declared attack (board, assignments, each estimator's number at the time, the real result); `tools/bots/battles.py` prints the paper-margin table and estimator calibration from them. No bot reads them.
- **Decision test set (issue #1206):** `--filter conan-positions` re-decides Mendon's positions from `docs/bots/replays/` with any rule set (`L5R_POS_ARMS`, `tests/L5R.AI.Tests/ConanPositions.cs`) and prints agreement by family: a metric for where to look, never a proof.

## What "good" means

A challenger is judged on all of these, from the report, never win rate alone:

1. Win rate against the champion over the pool (Wilson interval and exact sign test), and the **paired** McNemar result.
2. Margin: provinces left, Honor, Personalities in play (challenger minus champion).
3. Game length and turn-limit games.
4. **Soundness: zero** stalls, illegal commands, exceptions, decision-cap and setup failures (BotSoak categories). Refused-then-retried actions are informational.
5. No deck where it collapses: per-deck table, and the Adversary's decks (below).

## Pre-registration and stopping rule

Before any run, write `docs/bots/experiments/<date>-<name>.md` from `docs/bots/experiments/TEMPLATE.md` and commit it. It fixes the hypothesis, expected effect size, decks/situations,
the metric, the **sample (a number of seeds, from the power note)**, the significance threshold, and the decision rule. Then run **once** at that sample.

- **Fixed sample, no peeking.** Never extend a run that has not passed. If the registered sample is inconclusive the result is recorded as inconclusive. A larger follow-up is a *new*
  pre-registration that cites the first and uses a fresh seed block (`L5R_TOURN_SEEDS` runs seeds 0..n-1 of the range, so a second block needs a documented offset).
- Default decision rule (dev stage): independent pairing, one-sided exact sign test p < 0.025 (the same as two-sided .05) **and** a win rate at or above the registered effect, **and** zero
  soundness findings, **and** no per-deck collapse (below 35% on a deck where the champion is at 50% or more, with at least 20 games).
- Then confirm once on `holdout` mode with its own pre-registered sample. Only a pass on both is a promotion proposal.
- Promotion is **never automatic**: report the evidence and ask. After a promotion the champion is re-baselined (new `main`) and the A/A and held-out checks are rerun.

## Roles (agents never play games)

- **Challenger:** proposes one hypothesis at a time about a weakness in the champion; implements it behind a `BotContext` flag defaulting off, with a unit test of the decision itself;
  pre-registers; runs the tournament; records the result (negative results are kept in `docs/bots/experiments/`).
- **Adversary:** reads only public data (the view a seat has; printed card text). Builds decks and scenarios that exploit a specific champion or challenger behaviour (for example an attacker
  that never commits Cavalry, a rush that ignores province value) and reports each exploit with a reproducible seed, deck lists and the tournament command. A challenger that wins on average
  but loses badly to an adversary deck is not promoted. Adversary decks are added to a separate `adversary` list and never to the development pool.
- Branches: `claude/<issue>-bot-<name>`, one per candidate, started from current `main`.

## Cost accounting

Games are free of model tokens: the loop is `dotnet run` unattended. Tokens are spent only writing and reading code and reports. Each experiment's note records the wall-clock and the model
tokens used (the session's token counter before and after), so the cost of evidence is visible next to the evidence.
