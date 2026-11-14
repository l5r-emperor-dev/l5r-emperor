# Bot development roadmap

This is the long-running plan for the practice bots. It is the place to return to after a break: what each bot is for, where it stands, and what comes next. Experiment details live in `docs/bots/experiments/`; the method for the Conan campaign lives in `docs/bots/CONAN_STRATEGY_BRIEF.md` (since 2026-11-12; it replaces the search-first strategy of `CONAN_CAMPAIGN_BRIEF.md`, whose test method it keeps).

## The direction since 2026-11-12: a read of the game, then contextual rules

Mendon, 2026-11-12: more thinking time is not producing better decisions; the bots need rules that are "more complex and contextual but not heavily computational", driven by "a plan that it executes on the fly that is constantly being updated by the changing information". The goal is unchanged (Conan at 80% against the frozen Contender) and extended: "I want the bar to be genuinely challenging" for skilled players, with licence "to actually rethink the Conan strategy from the ground up."

Where Conan stands: 72.7 to 74.5% against the frozen Contender on three fresh blocks (7800, 7900, 8100), flat across five blocks of search work.

What was prepared for the next stage (all under `docs/bots/`):

| File | What it is |
| --- | --- |
| `CONAN_STRATEGY_BRIEF.md` | The operating brief for the model that does the work: the Read (effect descriptors, ledger, belief, exchange, posture), six steps with checkable completion criteria, the calibration gate, the test loop |
| `strategy/PLAYER_MODEL.md` | How Mendon decides, mined from his eight replays, as nested rules with the turn behind each |
| `strategy/RULE_FAMILIES.md` | Nine families of seed rules (attack, Cavalry, counter-attack, defence, battle, Dynasty, posture, reading the opponent, Favor and hand) |
| `strategy/ARENA_BEHAVIOUR.md` | What the bots measurably do well and badly over 4,000 logged games; the behaviour profile |
| `strategy/RULES_DIGEST.md`, `strategy/CODE_MAP.md` | The comprehensive rules as a bot needs them; the decision code as it stands |

The order of work: instruments (a rule registry with fire counters, battle records, the decision test set), then the Read in shadow until its predictions beat the existing estimators on real battles, then one rule family at a time, then the question of where the lookahead belongs, then confirmation, then the human bar (agreement with Mendon's decisions, the behaviour profile, exploiter decks, his own playtests).

Open for Mendon: a screenshot interview on Cavalry forcing and on the counter-attack at two Provinces or fewer (`strategy/PLAYER_MODEL.md` section 5); his games contain one Cavalry attack and no position where an empty home was punished.

## Improving the Contender too (Mendon, 2026-11-11)

"It's okay to improve the contender and lose ground to it as Conan because the contender isn't particularly difficult to beat for a human at the moment." Contender gains are welcome even if they cost Conan its margin. They also make Conan stronger, because Conan's playouts are played by the Contender.

How this is measured:
- Contender changes are screened Contender against Contender, from an A/A baseline.
- Conan's 80% goal is measured against a frozen snapshot of the Contender's rules (`T2`, the tuned set as of 2026-11-10), so the figure stays comparable while the live Contender improves.
- An improved Contender becomes Moderate in practice only with Mendon's approval.

## The ladder

| Bot | What it is | Goal | Status |
| --- | --- | --- | --- |
| Champion | The Easy practice level (`new BotContext()`) | The baseline every bot is measured from | Shipped |
| Contender | Champion + turn planner, triage, tuned rules (`docs/bots/CONTENDER.md`) | About 67% against the Champion | Done. The Moderate practice level since 2026-11-11. Tuning paused (Mendon, 2026-11-10) |
| Conan | Contender + lookahead search over sampled worlds (`docs/bots/CONAN.md`) | **80% against the Contender** | In progress, see below. The Hard practice level since 2026-11-11 (`BotContext.HardRules`) |
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

The campaign brief's method (fresh blocks, SPRT, a frozen pool, an oracle for mining losses) still governs testing. Its hypothesis backlog below is kept as a record; items 1, 2, 9, 12 and 13 were tried and rejected or not repeatable, and the work now follows `CONAN_STRATEGY_BRIEF.md`:
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

## The strategy campaign's instruments (2026-11-13)

Step 1 of `CONAN_STRATEGY_BRIEF.md` was split across two sessions (issue #1206): the Windows session claimed step 0 and the rule registry on 2026-11-13 but pushed nothing, so the Mac took them over on 2026-11-14 (branch `claude/1206-conan-registry`: `RuleRegistry.cs`, `BotContext.T2Rules` and arm `t2`, `L5R_TOURN_FIRES`; evidence in `docs/bots/experiments/2026-11-14-conan-step0-registry.md`); the Mac branch `claude/1206-conan-instruments` delivered the battle records (`L5R_TOURN_BATTLES`, `tools/bots/battles.py`), the behaviour profile (`tools/bots/behaviour/profile.py`), the decision test set (`--filter conan-positions`, 52 of Mendon's positions, replays in `docs/bots/replays/`) and step 2's effect descriptors (`EffectDescriptors.Of`, 59 of 60 right on a blind sample, 0 unknown in the audited decks). Evidence: `docs/bots/experiments/2026-11-13-conan-instruments.md`. The ledger, the belief and the exchange are built in shadow (rule `read`, branch `claude/1206-conan-read-shadow`) and pass the calibration gate on a held-out block: Brier 0.152 against 0.213 for the paper margin and 0.223 for `EstimateAttack`, with every large cell within 10 points, and zero games changed with the shadow on. Next on the Mac: the threat answer, the three questions and posture (section 4.5), then family D with the Read.

## The strategy campaign's third family, E (2026-11-14)

Family E (inside a battle) is done and shelved: the Fight (`readE`, `src/L5R.AI/Read/Fight.cs`) plays the player model's battle from the Read's exchange and agrees with Mendon's positions on 13 of 18 (T2: 6), but screens −49 and (with `reOpen`) −27 games in 1,456 against T2 on block 10100, losing the battles it fights (44-45% won against 50%). The note (`docs/bots/experiments/2026-11-14-conan-family-e.md`) reads the cause as the exchange's one-line projection, calibrated to predict a fall and not to choose inside the battle, and carries forward: judge the next families by playing the game on from the position, and use the exchange in battle as a playout's candidate generator (step 4). Next: families C, B, F, G, I; then refit the calibration; then step 4.

## The strategy campaign's Read and its first two rule families (2026-11-13 to 2026-11-14)

Step 2 is complete and merged (PRs #1223, #1234, #1235): the Read (ledger, belief, exchange) passed its calibration gate on held-out blocks (Brier 0.149 to 0.152 against 0.213 for the paper margin); `threat(home)` passed as the pair (three-hand cell, Force gap) at 0.110 against 0.130 and 0.125; the three questions and the posture are computed in shadow and match Mendon's shape on every split and lone attack position of the decision set. Step 3's first two families, D (`readD`, PR #1236) and A (`readA`, `claude/1206-conan-family-a`), each judged right on 7 or 8 of 10 drawn decisions and each ahead of T2 on the decision set, **screened null against the Contender twice each** (net −2, −1, −5, −16 on blocks of 1,456 games) and are shelved opt-in. `docs/bots/experiments/2026-11-14-conan-family-a.md` ends with three readings of that for Mendon (the Contender as the yardstick; the belief untested on its own; the calibration tables stale after the exchange fix of 2026-11-14) and waits for his choice before more families are built.

## Still open

- **Conan is in local Practice only.** It is the Hard level (`docs/decisions/2026-11-11-practice-difficulty.md`): the client samples a private copy of the match and decides off the UI thread. Rooms do not supply a sampler. When a better Conan is measured, update `BotContext.HardRules`.
- **The sampler knows the opponent's decklist composition** (brief item 15). This was fine for bot-vs-bot measurement. Hard now plays people with it, by Mendon's decision of 2026-11-11, so a person's opponent knows which cards are in their decks (not where they are). `lkBlind` was never built or measured.
- **The zero-cost `Payable` bug** (#1211): a card costing nothing counts as unpayable. The fix changed 41 of 728 games, net −1. Awaiting Mendon's call.
- **The Broken Alliance issue** draft is unfiled.

## Toth-Amon (after Conan reaches 80%)

Toth-Amon starts from Conan's final settings and is measured against Conan the same way: fresh blocks, paired games and the frozen pool. The pool keeps the Contender and the Champion as guards. Likely directions, carried over from the brief's later items:
- a learned value function;
- information-set search instead of independent worlds;
- search over Action-phase and purchase decisions;
- an opponent model fitted to Conan's own habits, from public information only.

The same rules apply: the comprehensive rules, no hidden information, card-agnostic logic.
