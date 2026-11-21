# Contender rules from Mendon's battle grill (issue #1185), 2026-11-09

Round 1 of the screenshot grill (five real Contender-vs-Champion positions) and Mendon's follow-up notes produced these card-general rules. Every rule is opt-in by name in the c2 rule list. At Mendon's direction (2026-11-09) `spreadwide` and `conf` joined the default `BotContext.TunedRules`; the rest stay opt-in.

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

## Rules from Mendon's practice games (2026-11-09)

Mendon played three practice games against the Champion (Unicorn Maidens vs Lion preset, Classic Crane vs Spider Conquer, Allan Lion MCA vs Spider Conquer) and the human-compare tool (`tests/L5R.AI.Tests/HumanReplayCompare.cs`) set his choices beside the Contender's. Rules drawn from the differences, net against the default with `spreadwide,conf` (`l-swc` 64.2% on 7000, `v7100-S` 61.5% on 7100, `ho-S2` 63.9% holdout):

| Rule | Source | 7000 net | 7100 net | Holdout net (p) |
|---|---|---|---|---|
| `selfplayer`: an Honor ability offered per player is never aimed the wrong way; of gain-self and lose-opponent, the one the Honor race wants | Armor of the Heavens, Unicorn game | +2 | | |
| `ownprov2`: the own-province refusal also lifts when no province would remain safe | Ebisu's Honesty, Unicorn game | +1 | | |
| `presdepth,pdh=100`: a presence defense sends one more unit than the attackers' printed removals plus the hand's expected removals | "two Yumarus need three Personalities", Crane game | +10 (.09) | +2 | +18 (.0015) |
| `keepup`: a bowing ability is performed by a unit that cannot straighten itself when another can | Shinden + Nanae loop, Crane game | 0 | | |
| `nofoeboost`: a helping ability is never given to an opponent's card | Kamina +4F on Ishibashi, Lion game | +1 | | |
| `aggro2`, `aggro2,raidboost` | turn-3 raze, Lion game | −9, −9 | | |
| `raid`: attack an undefended home's weakest province when Force plus Tactical Advantage from the hand beats it | turn-3 raze, Lion game | 0 (2 games changed) | | |
| `selfplayer,presdepth,pdh=100,keepup` | | +9 | +13 (.06) | +15 (.014) |
| `selfplayer,presdepth,pdh=100,keepup,nofoeboost,raid` | | +13 (.065), 66.0% | | |

At Mendon's direction (2026-11-09) the last set joined the defaults. Broken Alliance's move went three times to a performer whose movement was negated (Lion game, turn 7); those plays are counted as an engine/UI failure, not a decision.

## Later practice games and the cross-game review (2026-11-09)

Games 4–6: Phoenix Talons vs Crab Control (Military win, turn 11), Crab Control vs Lion Legacy (a deliberate Dishonor win, turn 29), Scottish Lion Deck vs Crane Dominance (Military win, turn 13; the Champion challenged Akodo Dairuko three times with weaker duelists). Net against the new defaults (`l-all2` 66.0% on 7000, `v7100-D` 64.0% on 7100, `ho-D` 67.1% holdout):

| Rule | Pattern in Mendon's play | 7000 | 7100 | Holdout |
|---|---|---|---|---|
| `nobowgift`: let the opponent gain Honor rather than bow an own Personality while it is short of 34 | Governor's Court, 7 of 7 | +3 | | |
| `favorshame`, `dhdrive`, both | Favor spent on Honor loss, not draws (Crab) | 0, −2, +1 | | |
| `aggro2`, `raid` | turn-3 raze (Lion) | −9, 0 | | |
| `duelodds`: challenge only a lower duel stat (a Duelist may challenge a tied non-Duelist) | Dairuko | −1 (1 game) | | |
| `duelplay`: strike while ahead; v2 only when clearly ahead | struck at once when ahead | −19, +1 | −1 | |
| `bodyfirst` at bfH 2/3/5: a Holding waits while a Personality is affordable | Personality where the bot bought a Holding, 21 times in 6 games | −41, −10, −8 | | |
| `pressahead`, `hdev=0.7` | pressing a lead with bodies | 0, 0 (purchase scoring does not read the plan intent) | | |
| `lobbyatk`: no lobbying with an attacker before attacking | lobbied after attacks | −4 | | |
| `defwin`: a presence defense big enough to win before removals | defended with 3–9 units | +4 (dwM=2 +4) | +5 | −3 |
| `pdstrong`: presence defenders strongest first | strongest defenders | +2 | | |
| `pdstrong,defwin,nobowgift` | | +6 | +7 (.06) | +3 |
| … `,favorhold` / `,favorshame` | held the Favor in won battles | +5 / +5 (`favorhold` alone changes 2 games) | | |

The Personality-first purchases are Mendon's most consistent difference, but forcing them loses: the buying pays off through what he does with the bodies, which no purchase rule captures. In arena games duels are answered by TrainingOpponent's generic scoring, while the client's Champion uses DuelOpponent: the two bots duel differently in practice and in the arena.
