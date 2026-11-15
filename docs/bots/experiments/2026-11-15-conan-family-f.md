# Conan strategy campaign, step 3: family F (Dynasty: economy against recruiting) reading the Read (issue #1206, from 2026-11-15)

Branch `claude/1206-conan-family-f`, from `main` after family B's PR (#1244). Blocks: Mac range from 10800 (shadow on seed 10800; the screen and its A/A on 10900).

## Rule card (above the line)

Rule `readF`, opt-in, off by default. Fires at the seat's own Dynasty purchases: each purchase decision asks the Read which card to buy next; within a class the Contender's own values order the cards. It rests on PM 3.6 (Holdings while they have no Personality; Personalities first once they have one or once the Gold buys a body and a cheap Holding; a weak economy is acceptable while pressing; build until the unit their answers do not touch when they beat my units; "a Personality that produces Gold first") and AB section 7 (Q7c: when behind on Personalities with both affordable, the bots buy the Holding first 54-58% of the time, Mendon 9 of 9; Q7e: the late Holdings are bought whatever Gold went unused; Q7a: 24-29% of capacity unproduced against Mendon's 12%). RULE_FAMILIES.md F gives the seed; CODE_MAP section 7 gap 6 (purchases ignore the threat).

```
row      = the affordable face-up Dynasty cards (Personalities, Holdings), each with its printed Force, cost, production and descriptors
T(c)     = threat(home with c as an unbowed defender, their army straightened)          (Threat.Of)       T0 = the threat without a purchase
O(c)     = my best attack next turn with c in the straightened army (Questions.Of)                        O0 = without
paying   = the Holdings whose production x turns left (the shorter clock, 2 to 8) exceeds their cost, the most efficient first
0  IF a free Holding is in the row                                                            -> it
1  IF T0.likely loses a Province AND some affordable body makes T(c).likely lose none        -> the cheapest such body
2  ELSE IF they have no Personality AND paying is not empty AND the Gold does not buy a body plus a paying Holding -> the most efficient paying Holding
3  ELSE IF some body turns O from fails to falls (O(c).Best takes at least half a Province's worth at likely where O0 does not) -> the cheapest such body
4  ELSE IF posture is Press                                                                  -> the best body; a Holding costing 2 or less only when no body is affordable
5  ELSE IF T0.likely loses none AND paying is not empty                                      -> the most efficient paying Holding
6  ELSE                                                                                       -> the best body: a Gold producer first, then Force, then the cheaper
silent when nothing applies (the Contender decides); within a class the Contender's own values order the cards
```

Lines 1 and 3 are the Read's; lines 2, 4, 5 and 6 are the player model's conditions (PM 3.6) on the Contender's own ordering. Against the seed in RULE_FAMILIES.md F the "they have no Personality" condition moved ahead of "fails to falls": with nobody across the table every body makes an undefended Province fall, and Mendon builds Holdings there (G6 T1-T3, G4 T1, G1 T2) until the Gold buys both. The rule names the class and the card to buy next at each purchase decision (the board is re-read after each purchase); the Contender's slate values order everything the rule is silent on. Attachments in the Action Phase, Proclaim and the Gold reserve are left to the Contender's rules in this formulation.

**Should move** (RULE_FAMILIES.md F): Q7c, first purchase a Personality when behind on Personalities (T2 mirror 41%; up); Provinces lost on own turns 3 to 5 (down); Gold capacity at turn 6 when not pressing (held); win rate by deck (no deck collapses: `bodyfirst` lost 41 games forcing bodies unconditionally). **Predicted fire rate**: the Read is consulted at every first purchase of a turn (about 1 a turn, 15-20 a game); it differs from the Contender where line 1 or 2 names a body the slate did not put first, or line 4 names a Holding where the slate put a body: on the mirror's Q7c table a quarter to a third of the turns with both on offer, so about 2-4 fires a game.
