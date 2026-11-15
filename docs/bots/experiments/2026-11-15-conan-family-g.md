# Conan strategy campaign, step 3: family G (posture and route) reading the Read (issue #1206, from 2026-11-15)

Branch `claude/1206-conan-family-g`, from `main` after family F's PR (#1245). Blocks: Mac range from 11000 (shadow on seed 11000; the screen and its A/A on 11100).

## Rule card (above the line)

Rule `readG`, opt-in, off by default: **the route gate**. Fires at the seat's own attack declaration (declare or pass), after the Read's posture (`Questions.Of`: race, lock, drain, punish, wait, press, build, hold) is read; it never touches a lethal attack. It rests on PM 3.1 (a non-military route attacks only when it costs nothing; stop attacking once the Honor win is certain; wait and punish an army bowed from attacking), PM 7 rows 9-10 (G5: eleven attacks recommended in the drain game Mendon won without attacking; G4 T5: hold at 6F v 7), AB section 7 (the bots attack on 56-77% of turns at every stage of every game; Mendon's attack rate falls to nothing after turn 8 in the games he won by Honor or by drain; the mirror loses 21% of its lost games to Honor, 2% to Dishonor; after the opponent passes 30 Honor it still attacks on 68% of its decisions). CODE_MAP section 7: no bot code prices the clocks against each other.

```
posture, clocks = Questions.Of(read, trend)         (the Read's answers at this declaration)
lethal          = the Contender's LethalPlan (every Province they have left falls)              -> declare, whatever the posture
Lock, Drain     = my Honor (or their dishonor) wins before their fastest clock while home holds
                  -> declare only when the opportunity's best attack is risk-free (exposure within .1 Province of the hold), loses no unit, spends no card, takes something, and (drain) uses no unit that runs the drain; else pass
Wait            = their last Province, they cannot win before my next turn, my best attack does not take it    -> pass (preserve units and cards; finish next turn)
Race            = my military clock is a turn shorter than their fastest, within six turns      -> declare (the Guard already lets a race stand)
Punish          = their Personalities are bowed from attacking                                   -> declare
Press, Build, Hold                                                                               -> silent: the Contender's own declare-or-pass stands
clocks: the drain clock also reads the table (my units' repeatable Open Honor-loss abilities, Honor a turn) when the trend has not yet shown a loss
posture: a running engine is the route: Drain when the table drains, the drain wins before their fastest clock and home holds, even when the army's clock is shorter (its attackers are the engine)
```

The gate decides only declare or pass; the shape of a declared attack is the Contender's (family A's shape was shelved). The Action-phase side of the drain (every repeatable Honor-loss effect each turn; units that run it never attack) is left to the Contender's Honor rules in this formulation; the "units that run it never attack" part follows from the gate (a drain seat does not declare).

**Should move** (RULE_FAMILIES.md G): Honor and Dishonor wins by decks with that route (Q9a: the mirror loses 21% of its lost games to Honor); attacks declared while on a non-military route (Q1a: Mendon none after turn 8; the bots 56-77%); agreement on PM 7 rows 9 and 10 (the four G5 hold positions, where T2 declares). **Predicted fire rate**: the posture is read at every declaration (about one a turn); it is Lock, Drain, Wait, Race or Punish in a minority of turns, and the gate differs from the Contender where it says pass on a lock or a drain (the Contender attacks) or declare on a punish the Contender holds: perhaps 0.3-1 fires a game, concentrated in Honor-route games.

## Decision tests

`RouteTests.cs` (`--filter route`, 8 cases): a lock passes without a risk-free attack and declares with one; a drain passes; a wait passes; a race and a punish declare; press, build and hold are silent; a lethal attack declares under a drain; the table drain (two Courtiers at 2 Honor each: 4 a turn, a 7-turn clock, posture Drain before any trend); the engine is the route (a 9-turn drain over a 4-turn military clock while home holds).

**The decision set** (`L5R_POS_ARMS="t2;rg:+read,readG" L5R_POS_FAMILY=G`, `L5R_POS_READ=1` printing `route:` at attack declarations): T2 declares at all four of Mendon's G5 hold positions (0 of 5 in family G). With the gate as first written (the posture as it stood), only T20 passes (Drain within the horizon); at T12 and T22 the posture read **Race** ("my military clock 6 is a turn shorter than their fastest 12"; "4 shorter than 38") because the Read's route is its shortest clock, and 27 Force against one unit is a short military clock. Mendon's route was the drain he was running: his attackers were the Courtiers that ran it (PM 7 row 10). Hence the posture change above: a running table engine that wins before their fastest clock while home holds is the route whatever the army could do. T24 read Drain but the gate declared: the Read's best attack (a split) was "risk-free" at +0.01 exposure over the hold; Mendon held (the Courtiers that attack do not drain); hence the gate's second condition, a drain's risk-free attack may use no unit that runs it. With both changes: 2 of 5 (T20 and T22 pass as Mendon did); with the cost condition of round 2 (a lock's or drain's attack must lose no unit and spend no card) **3 of 5**: T24's split spent a card, and the gate passes as Mendon did. T12 still reads Race: the table drain reads 1 Honor a turn where the engine's printed magnitude varies (the descriptor gives 1), so the drain clock (12) is not shorter than their fastest (12); T24 read Drain and the first gate declared a split at +0.01 exposure whose units carry no drain ability; Mendon held both. G1T10 (an own Province destroyed for Honor at 36) is the Contender's `ownprov` rule, not the gate's.

## Shadow (T2 with the Read against T2, seed 11000, 364 games)

**Round 1** (`shG_110`, the gate as first written; 3:25 wall): the posture at 2,556 attack declarations: Build 925, Hold 528, Race 411, Punish 244, Press 227, Lock 124, Drain and Wait a few dozen. The gate differed from the Contender at 252 (0.69 a game): declare on a race 98, declare on a punish 53, pass on a lock 49, declare on a lock 20, pass on a wait 19, pass on a drain 9, declare on a drain 4. Ten drawn by seed 110:

| # | Position | Contender | Read | Verdict |
|---|---|---|---|---|
| 1 | Lion Legacy v Phoenix T11, P3v2, race (my clock 3, theirs 6) | pass | declare: a split expecting 1.78 Provinces, home exposed (0.95) | right: the race accepts the loss, the Province count ends it |
| 2 | Lion v Dragon T19, P1v1, they win next turn | pass | declare | **gap**: no army ("hold: no army or no Province"): nothing to declare |
| 3 | Phoenix v Lion Legacy T4, punish | pass | declare | **gap**: no army |
| 4 | Crab v Dragon T14, they win next turn | pass | declare | **gap**: no army |
| 5 | Crab v Classic Crane T8, punish | pass | declare | **gap**: no group exceeds Strength even undefended |
| 6 | Crab v Classic Lion T8, punish | pass | declare | **gap**: no army |
| 7 | Scottish Lion v Spider T5, punish | pass | declare | **gap**: no army |
| 8 | Lion Legacy v Dragon T7, race (5 v 10) | pass | declare: a split expecting 0.89, home holds (0.04) | right |
| 9 | Scottish Lion v Classic Lion T9, race (4 v 7) | pass | declare: a split expecting 1.29 with five kills | right |
| 10 | Pinned Lion v Phoenix T19, P1v1, wait (their last Province, no win for them before my next turn, my best attack does not take it) | declare | pass | right by the Read: attacking bows the defenders of my own last Province for nothing |

**4 right, 0 wrong, 6 gaps of one kind**: a race or a punish declared with no possible attack (no army, or no group exceeding Strength even undefended); the Contender's pass was the only move. The gate now stays silent on a race or a punish when the opportunity has no attack option, so those six read "same". Round 2 is the rerun, with the posture change (a running engine is the route) and the drain condition (no engine unit in a risk-free attack) in.

**Round 2** (`shG2_110`): the gate differed at 142 declarations (0.39 a game): pass on a lock 49, declare on a race 40, declare on a lock 20, pass on a wait 19, pass on a drain 10, declare on a drain 4; the six "no army" gaps of round 1 are gone. Ten drawn by seed 210:

| # | Position | Contender | Read | Verdict |
|---|---|---|---|---|
| 1 | Classic Crane v Phoenix T13, H35/10, my Honor clock 1 | declare | pass (lock) | right: the Honor win is next turn |
| 2 | Crab v Unicorn T8, a 33-turn drain as the shortest route, no group exceeds Strength | declare | pass (drain) | right (nothing to take) |
| 3 | Scottish Lion v Phoenix T9, H25/9, Honor clock 2 | declare | pass (lock) | right (PM 3.1: three attacks in two Honor games) |
| 4 | Lion Legacy v Dragon T13, H31/13, Honor clock 2 | pass | declare (lock: "everything" at −0.20 exposure, 0.10 Provinces, 2 kills for 2 losses) | **wrong**: two units are not nothing with the win two turns away |
| 5 | Unicorn v Crane T10, H28/15, Honor clock 2 | declare | pass (lock) | right |
| 6 | Scottish Lion v Crane T5, race (5 against 32) | pass | declare: a split expecting 0.89 | right by the Read (a "race" only in name: their clock is 32) |
| 7 | Crane v Dragon T11, P4v1, wait | declare | pass: the best attack takes their last Province at 0.40 | right by the model (finish next turn) |
| 8 | Classic Lion v Lion T15, H35/23, Honor clock 2 | declare | pass (lock) | right |
| 9 | Lion Legacy v Phoenix T13, H26/17, Honor clock 3 against 8 | declare | pass (lock) | right |
| 10 | Scottish Lion v Spider T9, race (6 against 7) | pass | declare: everything, 0.79 Provinces, home loses one at likely | right by the seed (a race accepts the loss) |

**9 of 10 right**, one wrong: a lock's "risk-free" attack now also has to lose no unit and spend no card (#4). Round 3 is the rerun.

**Round 3** (`shG3_110`): 137 differences (0.38 a game): pass on a lock 61, declare on a race 40, pass on a wait 19, pass on a drain 11, declare on a lock 6. Ten drawn by seed 310:

| # | Position | Contender | Read | Verdict |
|---|---|---|---|---|
| 1 | Dragon v Scottish Lion T14, P2v4, H13/38, they win by Honor next turn | pass | declare ("nothing to lose"): 0.10 Provinces | equivalent: nothing stops the Honor win; the attack costs nothing that matters |
| 2 | Unicorn v Dragon T8, H17/9, Honor clock 4 against 16 | declare | pass (lock) | right |
| 3 | Scottish Lion v Crane Dominance T9, H19/13, clock 4 against 8 | declare | pass (lock) | right |
| 4 | Scorpion v Classic Lion T14, P1v3, H24/26, Honor clock 2 against 4 | declare | pass (lock): my last Province's defenders stay unbowed | right |
| 5 | Lion Legacy v Dragon T7, race (6 against 17) | pass | declare: everything, 0.79 Provinces, home holds at likely | right |
| 6 | Scottish Lion v Crane Dominance T17, P4v2, H36/31, Honor clock 1 | declare | pass (lock): the split expecting 1.78 Provinces loses a unit | equivalent: the Honor win is next turn either way; the split might end it now |
| 7 | Crane Dominance v Spider T11, P3v4, H29/0, Honor clock 2 against 5 | declare | pass (lock) | right |
| 8 | Lion Legacy v Phoenix T17, H40/26 (the Honor win is checked at my next turn start) | declare | pass (lock) | right |
| 9 | Crane v Dragon T11, P4v1, wait | declare | pass | right by the model |
| 10 | Scottish Lion v Pinned Lion T9, P4v1, wait, no attack above the hold | declare | pass | right by the model |

**9 right, 1 equivalent, 0 wrong.** The gate passes the shadow gate (4 of 10 with six gaps, then 9, then 10 of 10) and fires 0.38 times a game, inside the card's 0.3-1. The screen runs on block 11100.
