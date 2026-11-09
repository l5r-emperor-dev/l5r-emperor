# Contender rules from Mendon's battle grill (issue #1185), 2026-11-09

Round 1 of the screenshot grill (five real Contender-vs-Champion positions) and Mendon's follow-up notes produced these card-general rules. Every rule is opt-in by name in the c2 rule list; the default `BotContext.TunedRules` is unchanged.

Dev arena: block 7000 (2 seeds, 727 decided games), paired against `v7000-T` (the tuned set, 60.9%). Confirmation: block 7100 against `v7100-T` (59.1%). Holdout: seeds 9100–9103, 720 games, every game includes a held-out deck. "Net" is games gained minus lost on the same seeds; p is the one-sided exact sign test.

| Rule | Source | Dev 7000 net (p) | Dev 7100 net (p) | Holdout net |
|---|---|---|---|---|
| `spreadwide`: once one unit beats the strongest Province, attachments go to the weakest unbowed Personality | "spread those out so that you can be more versatile" | +13 (.007) | +14 (.003) | +1 |
| `spreadvwide`: a Personality with no attachment gets one before any gets a second; a Kensai may carry two Weapons | Mendon's rule | +12 (.03) | with conf +21 (.006) | — |
| `conf`: low confidence (the bot's removals that reach the deciding unit, minus the opponent's expected cut-offs, ≤ 1) hits that unit now with the biggest removal; high confidence spends the cheapest removal and keeps hand cards while an in-play ability reaches the same unit | Miki follow-up | +12 (.03) | +9 (.09) | −3 |
| `spreadwide,conf` | | +25 (.0008); +24 with the cost fix | +19 (.007) | 0 |
| `cutoff`: the low-confidence half alone | | +6 | identical to conf when stacked | — |
| `lethal`: opponent down to its last two Provinces and an all-in attack razes all: attack with everyone | Position 1 | +1 | | |
| `overkill`: no Force bonus at a decided battle | "playing actions when it doesn't need to" | −2 | | |
| `presence2` (with or without duels): count every bow/send-home/duel on an enemy as a defensive removal | Position 3 | −11 | | |
| `nogift,nogiftH=0`: no losing defense with Provinces to spare | Position 5 | **−18 (.0005, worse)** | | |
| `threatbody`, `tb2`, `firstbody` | early razes by Spider | +1, +1, −23 | | |
| `clock` at 37/34/30/28 Honor | Unicorn Honor wins | +2, 0, −2, 0 | | |
| `handkeep`, `svwgate`, `cutk=2`, `cuth=25` on top of `spreadwide,conf` | | +3, ≈−3, +1, +4 | | |

Findings:

- Both bots put 81% of a game's attachments on one Personality; `spreadwide` cuts it to 70%, `spreadvwide` to 63%.
- Refusing outmatched defenses loses games: a defender present unlocks the hand's battle actions (Positions 1–3 of round 2 show three such defenses that won).
- The gains hold on two dev seed blocks but not on the holdout decks, where the rules change few games (≈20 flips in 720). The tuned rules may be fitted to the dev deck pool's attachment and removal density.
- `conf` first counted "Bow Kouki · enemy −3 Force" as a removal (the bow is the cost); fixed by reading the effect after "·" when the ability's cost bows.
