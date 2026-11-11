# Bot development roadmap

This is the long-running plan for the practice bots. It is the place to return to after a break: what each bot is for, where it stands, and what comes next. Experiment details live in `docs/bots/experiments/`; the method for the Conan campaign lives in `docs/bots/CONAN_CAMPAIGN_BRIEF.md`.

## The ladder

| Bot | What it is | Goal | Status |
| --- | --- | --- | --- |
| Champion | The practice bot on `main` (`new BotContext()`) | The baseline every bot is measured from | Shipped |
| Contender | Champion + turn planner, triage, tuned rules (`docs/bots/CONTENDER.md`) | About 67% against the Champion | Done. Tuning paused (Mendon, 2026-11-10) |
| Conan | Contender + lookahead search over sampled worlds (`docs/bots/CONAN.md`) | **80% against the Contender** | In progress, see below |
| Toth-Amon | The next bot, built on Conan | **80% against Conan** | Not started. Begins when Conan reaches 80% |

Rules for every rung (Mendon, 2026-11-10):
- A bot must follow the comprehensive rules and must never inspect hidden information. The sampler (`MatchEngine.Sample`) is its only window onto hidden cards, and `HiddenSampleTests` must keep passing.
- Every win must come from quality of play.
- Bot logic is card-agnostic.
- No setting becomes a default on `main` without Mendon's approval.

## Conan: where it stands

Results against the Contender on block 7000 (364 games, compared on the same games):

| Settings | Win rate | Net vs previous best |
| --- | --- | --- |
| `look` | 62.1% | +49 vs Contender A/A (48.6%) |
| `look,lkDef,lkVar` | 62.9% | +52 vs A/A |
| + `lkPlan` | **71.2%** | +30 (p = .0003) |
| + `lkPlan,clock` | 70.9% | −1 |
| + `lkPlan,lkFar` | 69.2% | −7 (searches hit the 10 s limit) |

The campaign brief's method (fresh blocks, SPRT, a frozen pool, an oracle for mining losses) governs the next steps. Its hypothesis backlog, in order:
1. Anytime search.
2. More worlds.
3. A cheaper forward model.
4. Shared samples across one attack.
5. A weaker rollout policy.
6. Strategy-fusion guards.
7. A learned end-state value.
8. Weight tuning.
9. Horizon.
10. Better attack shapes.
11. Better defence shapes.
12. Action-phase search.
13. Purchase search with the learned value.
14. Favor, reactions and duels.
15. A blind sampler.
16. An adversary pass.

## Difficulty scaling for players

Question (Mendon, 2026-11-10): does Conan's strength scale predictably with the number of sampled worlds (`lkN`, default 4)? If it does, a player-facing difficulty slider could be built on it.

Variables that could scale difficulty without new rules, to be measured against the Contender and the Champion:

| Variable | What it changes | Cost | Expected effect |
| --- | --- | --- | --- |
| `lkN`: worlds per candidate | Noise in the search's estimate | Linear in thinking time | The main candidate for a slider; measured first |
| `lkK`: candidates searched | How many of the Contender's options are considered | Linear | Smaller than `lkN` so far (`lkN=8,lkK=5`: +7, n.s.) |
| `lkT`: time limit per search | How often a hard decision falls back to the Contender | Caps the wait | Only matters with anytime search |
| Which decisions are searched (`lkPlan`, `lkDef`, `lkVar`) | How much of the game is planned rather than rule-driven | Small | Large steps: +30 for `lkPlan` |
| The bot rung (Champion, Contender, Conan) | Everything | — | Coarse steps that already exist |

The measurement:
- `lkN` = 1, 2, 4, 8 and 16 on a fresh block.
- Win rate against the Contender and thinking time for each value.
- 16 needs anytime search first, or its searches hit the 10 s limit and fall back.

A slider is worth building only if win rate rises monotonically with a usable spread. The UI would need a decision (Mendon's lane, with Alfredo for the client).

## Still open

- **Conan is not in the game yet.** It is not wired into practice or rooms: the client supplies no sampler. Shipping it needs a server-side sampler for the bot's seat and a decision on its thinking time.
- **The sampler knows the opponent's decklist composition** (brief item 15). This is fine for bot-vs-bot measurement. Before Conan plays humans, measure a blind sampler (`lkBlind`).
- **The zero-cost `Payable` bug** (#1211): a card costing nothing counts as unpayable. The fix changed 41 of 728 games, net −1. Awaiting Mendon's call.
- **The Broken Alliance issue** draft is unfiled.

## Toth-Amon (after Conan reaches 80%)

Toth-Amon starts from Conan's final settings and is measured against Conan the same way: fresh blocks, paired games and the frozen pool. The pool keeps the Contender and the Champion as guards. Likely directions, carried over from the brief's later items:
- a learned value function;
- information-set search instead of independent worlds;
- search over Action-phase and purchase decisions;
- an opponent model fitted to Conan's own habits, from public information only.

The same rules apply: the comprehensive rules, no hidden information, card-agnostic logic.
