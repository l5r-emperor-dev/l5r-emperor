# The Contender (issue #1185)

A planning bot, registered as the tournament arm `contender`, built from Mendon's scenario interviews. It lives next to the champion in `src/L5R.AI` behind `BotContext` flags (all **off by default**; the
practice opponent and the champion on `main` are unchanged). It is measured the way every bot change is (`docs/bots/README.md`): bot against bot, seeded, never promoted without a pre-registered run.

The scenario interview it was built from (Mendon's words verbatim, the questions asked, and where each rule went) is `docs/bots/interviews/2026-11-07-planner-interview.md`.

## What it is

`BotContext.Contender()` turns on two things, both in `src/L5R.AI`:

- **The turn plan** (`TrainingOpponent.Planner.cs`): an intent (Attack / Defend / Develop), the Gold and the shape it comes in (a 5 and a 3 cannot pay for two 4-cost cards), and a purchase slate: the
  best-valued set of province cards the Gold can pay for (efficient Holdings, bodies, Proclaim value, the whole row when it can all be paid for). The plan is made again whenever its inputs change
  (turn start, phase change, Border Keep refresh, units or Provinces changing in a battle, each purchase); `BotContext.PlanLog` says when and why.
- **Triage** (`TrainingOpponent.Triage.cs`): enemy targets are ranked by what they can still do (Force, printed abilities not yet used this turn, an unspent Tactician worth up to +4 while its owner holds
  cards, battle keywords over descriptive ones), and printed abilities already in play go before hand cards. What a card has spent this turn is not in the view; the bot remembers it from the public
  battle action list (`MatchView.BattleActions`), so it sees Tacticians and printed Battle abilities, not Open-phase uses, and matches by performer name.

Four more parts exist but are **off** in `Contender()` because paired testing found they lose games (or do nothing). Each has its own switch and its own arm so they can be fixed one at a time:

| Switch (`Contender(...)` argument) | What it does | Paired result on the same 364 games |
| --- | --- | --- |
| `attack` (`PlannerAttack`) | a swing-based battle estimate decides whether and where to attack and the Gold set aside for tricks | about **-14** games |
| `discard` (`PlannerDiscard`) | the interview's discard rule (keep what can be bought next turn; discard only what is too dear, or a blocking Holding early or when digging) | about **-11** games |
| `reserve` (`PlannerReserve`) | Gold held untapped for Battle tricks, before an attack and at the end of the turn | **0** (never changes an outcome) |
| `wonCap` (`TriageWonCap`) | no Gold or card spent on a battle already comfortably won | **-3**, not significant |

## Read results against the champion's own baseline, never against 50%

With `independent` pairing the champion playing itself does **not** score 50% on a given seed block: on block 1700 it scored **46.4%** (169-195; the challenger label sits more often on the weaker side of
a deck pairing, and the seats are not symmetric). Every game is deterministic, so an arm and the champion A/A on the same block play **the same games**. `tools/bots/compare_tournament.py` counts only
the games whose outcome changed, which removes that skew. A rule that changes 10 of 364 games shows up as +3 or -3, which a win-rate comparison would bury.

Results on block 1700 (`L5R_TOURN_SEED_START=1700 L5R_TOURN_SEEDS=1`, independent pairing, 364 games, champion A/A baseline 169-195):

| Arm | Gained | Lost | Net | p (two-sided) |
| --- | --- | --- | --- | --- |
| planner, all rules | 33 | 60 | -27 | 0.007 |
| planner, no discard rule | 37 | 53 | -16 | 0.11 |
| purchase slate only | 37 | 39 | -2 | 0.91 |
| triage, all three rules | 11 | 16 | -5 | 0.44 |
| triage: target ranking only | 4 | 3 | +1 | 1.0 |
| triage: printed abilities first only | 7 | 5 | +2 | 0.77 |
| triage: won-battle cap only | 4 | 7 | -3 | 0.55 |
| `contender` (slate + triage targets + printed-first) | 37 | 41 | -4 | 0.73 |

The harmless parts are also nearly invisible: triage changes the outcome of only 7-11 games in 364. The purchase slate matters more but is a wash. **Nothing here beats the champion yet.** The first
useful work is to find out why the attack estimate and the discard rule lose, then to build the parts that have not been tried: Action-phase sequencing and contingency branches, mitigation of the most
dangerous unit first, and re-planning after a battle (see `docs/bots/experiments/2026-11-07-contender.md`).

## Running it on two machines (a Mac and a PC)

The AI tests need only the .NET SDK (no Godot): `source tools/env.sh` as in `docs/bots/README.md`. Give each machine its **own seed block**; the reports pool because every game is identified by
its decks, seed and seat.

```
# machine A                                                             # machine B
L5R_TOURN_CHAL=contender L5R_TOURN_PAIRING=independent L5R_TOURN_MODE=dev \
  L5R_TOURN_SEED_START=2000 L5R_TOURN_SEEDS=3 L5R_TOURN_OUT=artifacts/bots/contender-A \     # ... SEED_START=2003 ... contender-B
  bash -c 'source tools/env.sh && dotnet run -c Release --project tests/L5R.AI.Tests -- --filter bot-tournament'
```

Run the same two blocks for the baseline (`L5R_TOURN_CHAL=champion`, out `champion-A` / `champion-B`), then:

```
python3 tools/bots/compare_tournament.py --baseline artifacts/bots/champion-A.json artifacts/bots/champion-B.json \
                                          --arm artifacts/bots/contender-A.json artifacts/bots/contender-B.json
python3 tools/bots/merge_tournament.py artifacts/bots/contender-A.json artifacts/bots/contender-B.json   # the pooled win rate on its own
```

Arms to run beside `contender`: `contender+attack`, `contender+discard`, `contender+reserve`, `contender+woncap`, `contender+all` (and the champion for the baseline). Splitting one pre-registered sample
over two machines is fine; looking at half of it and then deciding to stop or extend is not (the rule in `docs/bots/README.md`). Running 5 arms at once on one machine can push a slow game past its wall-time
budget (the report lists those as "out of time"); a few are harmless but they are not wins, so prefer fewer simultaneous arms than cores allow.
