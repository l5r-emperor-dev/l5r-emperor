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

## Reading the opponent: rush or out-develop (Mendon, 2026-11-11)

Mendon's example, from a game against Allan. Allan's Lion brought out Akodo Suoh on turn 1, equipped Dual Warfans on turn 2 and attacked. Mendon's Scorpion held Heavily Engaged: he waited for Allan's Tactical action, then bowed Suoh. He had judged that he could absorb the rush, so he kept investing in his economy, and he out-developed Allan with Personalities. Without the answer in hand he would have bought a Personality to match. With a very strong economic opening he might have conceded the first Province and still expected to win the race.

What a bot needs for this, card-agnostically and without hidden information:
1. **Purchases decided from the position, every time.** A first try, `lkPosture`, searched two blanket postures, economy first or bodies first, over six turns. Mendon judged it too inflexible: "Decisions should be much more contextually based on opportunities and threats." It was removed before any arena test. What replaces it:
   - For Conan, `lkMix` with `lkBuy`: each purchase searches the Contender's pick, the best Holding on offer, the best Personality on offer, and buying nothing. Each is played `lkBH` turns forward from this table, with every later decision made normally, and the choice is made again at the next purchase.
   - For the Contender, `econctx`: the tuned economy-first opening rule (`econ`) gives way when the table calls for a body. That is when the opponent's army beats the bot's own on its weakest Province, or when one affordable Personality closes the gap to razing the opponent's weakest Province.
   
   Both use only the bot's view: its own hand and the public board, with hidden cards from the sampler.
2. **Reading intentions from what the opponent did** (not built). The sampler redeals the opponent's hidden cards uniformly. A player infers from behaviour: attacking a lone Personality into an open board suggests a held Battle action. A belief-weighted sampler would keep or weight each redealt world by how likely the opponent's observed choices were in it, judged by the Contender's policy. The oracle diagnostic (84% against 74%) bounds what better beliefs can give.
3. **The race estimate behind the posture:** each side's Gold per turn, Personalities in play and Force threatened against Province strength, over the next few turns. The playouts above compute this implicitly; an explicit version would be cheaper and could be shown to a player.

A regression for this belongs with item 1. In a fixed position (an early rush against a hand holding an answer), the purchase should change depending on whether the answer is in hand.

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
