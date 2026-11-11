# Conan (issue #1206)

Conan is the Contender (`docs/bots/CONTENDER.md`) plus **battle lookahead**. The Contender's hand-written rules levelled off at about 66% against the Champion. Mendon's practice games showed why. The decisions that separate a good player hinge on how the battle will actually play out:

- spend in-play actions first;
- keep the Imperial Favor to finish a battle that is already being won;
- destroy units rather than send them home.

A single "confidence" number cannot capture those decisions. Conan does not try. It plays its options out on copies of the match and keeps the one that works.

## How it works

- **`MatchEngine.Sample(viewerId, seed)`** (`src/L5R.Core/HiddenSample.cs`) is a detached copy of the match in which every card hidden from the viewer is redealt. The copy is redealt in three ways:
  - the other player's hand is shuffled together with their Fate deck;
  - each Dynasty deck is shuffled together with its owner's face-down Province cards;
  - each Fate deck is reordered.

  The shuffle generator is reseeded, and only card identities move. `HiddenSampleTests` checks along seeded games that the viewer's view of every sample is exactly its view of the real match, so the search learns nothing hidden.
- **The `look` rule** (`src/L5R.AI/TrainingOpponent.Lookahead.cs`) runs at each Battle-action decision:
  - it takes the Contender's best `lkK` (3) distinct candidates;
  - it plays each one out on `lkN` (4) samples to the end of the turn's attack, with the Contender playing both seats;
  - every candidate is played out on the same samples, so the comparison is paired;
  - it keeps the candidate with the best mean score.

  The score reads only Provinces, the Gold Cost and Force of units and Items in play, Honor and cards in hand. A win or a loss scores ±1000. The logic is card-agnostic, because the engine resolves every card.
- **The bot only searches when the caller supplies `BotContext.Sampler`.** The arena gives it a sampler of the real match for the bot's own seat. `BotContext.Conan()` is the configuration, the Contender's defaults plus `look`. `BotContext.LookLog` records each search, and the game logs print it under "challenger lookahead".
- **Opt-in knobs for further tuning:**
  - `lkM`: the margin a searched action needs over the Contender's own first choice;
  - `lkAtk` and `lkDef`: also search the decision to attack and the choice of defenders;
  - `lkOpp`: model the other seat as the Champion in playouts;
  - `lkP`, `lkU`, `lkH`, `lkC`: the score's weights.
  - `lkT`: a search's time limit in seconds (10). Past it, the search is abandoned and the Contender's own choice stands, so a player never waits longer than that on one decision.
  - `lkVar`: with `lkDef`, also compare a few defence shapes.
  - `lkAny`: anytime search. At the time limit, decide on the samples every candidate has completed instead of abandoning the search.
  - `lkVal`: score the end of a playout by the learned win chance of `ValueModel` (fitted by `tools/bots/fit_value.py` from `L5R_TOURN_SNAP` exports) instead of the material score.
  - `lkFar`: with `lkPlan`, play the attack's options through the opponent's next attack.
  - `lkPS`, `lkHS`: Provinces worth more as fewer remain; Honor worth more near the win.
  - `lkDedup`: a move offered twice is searched once.
  - `lkPlan`, `lkIntent`, `lkBuy`: plan-driven search (attack chosen by search and then followed; intent-weighted scoring; purchases played out through the next attack). See `docs/bots/experiments/2026-11-10-conan-tuning.md`.

## Results against the Champion

Each result is compared with the Contender defaults on the same games.

| Block | Games | Contender | Conan | Net |
| --- | --- | --- | --- | --- |
| dev 7000 ×2 | 669 decided | 66.9% | **77.7%** | +62 (p < .0001) |
| dev 7100 ×1 (unseen) | 363 | 64.0% | **76.6%** | +36 (p < .0001) |
| holdout 9100 ×2 (unseen decks) | 358 | 67.1% | **79.3%** | +39 (p < .0001) |

The 7000 run used the arena's default 5-minute wall budget, under which 58 games timed out. The 7100 and holdout runs used `L5R_TOURN_BUDGET=20` and timed out 1 and 2 games.

The **cost** is that a Conan game takes about 5× as long: about 90 s against 19 s in the arena.
- A playout averages about 14 decisions, or about 45 ms. Most of that is the engine building the legal action list.
- A Battle decision with three candidates and four samples takes about half a second.

## Running it

```bash
L5R_TOURN_BUDGET=20 tools/bots/par.sh 7100 1 <baseline> 1 "conan=$(tr -d '\n' < tuned-rules.txt),look"
```

You can also use the `conan` arm, which takes extra rules from `L5R_C2` when set.

## Speed and safety

- The engine remembers repeated Gold quotes within one action query, which made playouts about 25% faster with identical action lists.
- Two of the Contender's own searches were exponential and could stall a decision for minutes or hours: the cavalry last stand and the purchase planner's payment check. Both now finish in moments (regression tests `cavalry-defense/LastStandIsBounded` and `planner/ManySources`).
- In the arena, `L5R_TOURN_BUDGET` is also a hard per-game cutoff, checked inside decisions. A killed game is logged with where it was.

## Limits

- Conan runs only bot-vs-bot. The practice opponent and rooms do not supply a sampler, so their bots are unchanged.
- A sampled copy can occasionally reach an engine state the real match would not. `ValidateAbilityResult` then throws. That playout counts as neutral and the real game is unaffected.
- Playouts use fresh Contender memories, so they forget the cards seen and the abilities spent earlier in the match.
