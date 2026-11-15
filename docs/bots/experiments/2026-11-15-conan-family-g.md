# Conan strategy campaign, step 3: family G (posture and route) reading the Read (issue #1206, from 2026-11-15)

Branch `claude/1206-conan-family-g`, from `main` after family F's PR (#1245). Blocks: Mac range from 11000 (shadow on seed 11000; the screen and its A/A on 11100).

## Rule card (above the line)

Rule `readG`, opt-in, off by default: **the route gate**. Fires at the seat's own attack declaration (declare or pass), after the Read's posture (`Questions.Of`: race, lock, drain, punish, wait, press, build, hold) is read; it never touches a lethal attack. It rests on PM 3.1 (a non-military route attacks only when it costs nothing; stop attacking once the Honor win is certain; wait and punish an army bowed from attacking), PM 7 rows 9-10 (G5: eleven attacks recommended in the drain game Mendon won without attacking; G4 T5: hold at 6F v 7), AB section 7 (the bots attack on 56-77% of turns at every stage of every game; Mendon's attack rate falls to nothing after turn 8 in the games he won by Honor or by drain; the mirror loses 21% of its lost games to Honor, 2% to Dishonor; after the opponent passes 30 Honor it still attacks on 68% of its decisions). CODE_MAP section 7: no bot code prices the clocks against each other.

```
posture, clocks = Questions.Of(read, trend)         (the Read's answers at this declaration)
lethal          = the Contender's LethalPlan (every Province they have left falls)              -> declare, whatever the posture
Lock, Drain     = my Honor (or their dishonor) wins before their fastest clock while home holds
                  -> declare only when the opportunity's best attack is risk-free (exposure within .1 Province of the hold) and takes something, and (drain) uses no unit that runs the drain; else pass
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

**The decision set** (`L5R_POS_ARMS="t2;rg:+read,readG" L5R_POS_FAMILY=G`, `L5R_POS_READ=1` printing `route:` at attack declarations): T2 declares at all four of Mendon's G5 hold positions (0 of 5 in family G). With the gate as first written (the posture as it stood), only T20 passes (Drain within the horizon); at T12 and T22 the posture read **Race** ("my military clock 6 is a turn shorter than their fastest 12"; "4 shorter than 38") because the Read's route is its shortest clock, and 27 Force against one unit is a short military clock. Mendon's route was the drain he was running: his attackers were the Courtiers that ran it (PM 7 row 10). Hence the posture change above: a running table engine that wins before their fastest clock while home holds is the route whatever the army could do. T24 read Drain but the gate declared: the Read's best attack (a split) was "risk-free" at +0.01 exposure over the hold; Mendon held (the Courtiers that attack do not drain); hence the gate's second condition, a drain's risk-free attack may use no unit that runs it. POSITIONS_AFTER
