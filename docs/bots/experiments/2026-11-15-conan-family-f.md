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
2  ELSE IF they have no Personality AND paying is not empty:
        IF the Gold buys a body plus a paying Holding                                              -> the best body (the Holding follows with the leftover)
        ELSE                                                                                        -> the most efficient paying Holding
3  ELSE IF some body turns O from fails to falls (O(c).Best takes at least half a Province's worth at likely where O0 does not) -> the cheapest such body
4  ELSE IF posture is Press                                                                  -> the best body; a Holding costing 2 or less only when no body is affordable
5  ELSE IF posture is Build (their shown answers beat my units) AND I have a unit AND T0.likely loses none AND paying is not empty -> the most efficient paying Holding
5b ELSE IF T0.likely loses none AND I am not behind on Personalities AND a Holding pays back within two turns (proposed) -> that Holding
6  ELSE                                                                                       -> the best body: a Gold producer first when within 2 Force of the best, then Force, then the cheaper
silent when nothing applies (the Contender decides); within a class the Contender's own values order the cards
```

Lines 1 and 3 are the Read's; lines 2, 4, 5 and 6 are the player model's conditions (PM 3.6) on the Contender's own ordering. Against the seed in RULE_FAMILIES.md F two conditions changed. The "they have no Personality" condition moved ahead of "fails to falls": with nobody across the table every body makes an undefended Province fall, and Mendon builds Holdings there (G6 T1-T3, G4 T1, G1 T2) until the Gold buys both. And the seed's "T holds without a purchase -> Holding" became "building while T holds": at G1 T4 and T6 home held and Mendon bought the Personality (PM 3.6: once they have a Personality, Personalities first; Holdings only while building against answers that beat his units); with the seed's line the rule bought the Holding at both positions, as the Contender does. The rule names the class and the card to buy next at each purchase decision (the board is re-read after each purchase); the Contender's slate values order everything the rule is silent on. Attachments in the Action Phase, Proclaim and the Gold reserve are left to the Contender's rules in this formulation.

**Should move** (RULE_FAMILIES.md F): Q7c, first purchase a Personality when behind on Personalities (T2 mirror 41%; up); Provinces lost on own turns 3 to 5 (down); Gold capacity at turn 6 when not pressing (held); win rate by deck (no deck collapses: `bodyfirst` lost 41 games forcing bodies unconditionally). **Predicted fire rate**: the Read is consulted at every first purchase of a turn (about 1 a turn, 15-20 a game); it differs from the Contender where line 1 or 2 names a body the slate did not put first, or line 4 names a Holding where the slate put a body: on the mirror's Q7c table a quarter to a third of the turns with both on offer, so about 2-4 fires a game.

## Decision tests

`PurchaseTests.cs` (`--filter purchase`, 11 cases, one or two per line of the card): a body that makes home hold, the cheapest such body, the turn-1 both-affordable case, the paying Holding while they have nobody, fails to falls, pressing, the Build line with and without a paying Holding, a body once they have a Personality, the Gold producer first, the free Holding, silence when nothing is affordable.

**The decision set** (`L5R_POS_ARMS="t2;rf:+read,readF" L5R_POS_FAMILY=F`, with `L5R_POS_READ=1` printing `purchase:` at Dynasty positions): T2 buys the Holding at all three of Mendon's body positions (0 of 4 in family F). With `readF`: F-G2T1-body buys Daidoji Kenshi as Mendon did (judged DIFFER only because the replayed command carries the Clan discount flag and the bot's variant does not; the card is the same); F-G1T4-body and F-G1T6-body buy a Personality where T2 bought Prosperous Village (Utaku Chikako both times, where Mendon bought Tsukiko and Ryoko: the class is right, the card is the Contender's own order within the class). Two faults the positions found before this reading: with the seed's "T holds -> Holding" line the rule bought the Village at T4 and T6 (home held; Mendon bought bodies: once they have a Personality, Personalities first), and on turn 1 with the Gold for both the rule fell through to the Holding (PM 3.6 turn 1: buy both, the body first). Whole set: T2 9/50, `rf` 9/50 (the three F positions change from the Holding to a body; no other position moves).

## Shadow (T2 with the Read against T2, seed 10800, 364 games)

**Round 1** (`shF_108`, 3:21 wall): the Read was consulted at 4,961 purchase decisions (13.6 a game), differing from the Contender's next purchase at 1,958 (5.4 a game), far above the card's 2-4 prediction. Two kinds of difference are order only: a free Holding the Contender buys later in the turn (386), and the turn-1 "both affordable" case where the Contender buys the Holding first and the body after (384). The substantive 1,188 split: a body where the Contender buys a Holding (line 6: 412; after a threat no body alone holds: 154), a body that makes home hold (line 1: 322, mostly utility Personalities whose ability turns the exchange), a different body when pressing (46), a different Holding while building (81), and fails-to-falls (84). Ten substantive differences drawn by seed 108 (`sample_purchase.py`), judged on the position as read:

| # | Position | Contender | Read | Verdict |
|---|---|---|---|---|
| 1 | Crane v Crab T23, pressing, their last Province, 31 Gold | Tsunehiko 6F | Soken 0F (a Gold producer first) | **wrong** as a preference (everything is bought anyway): 6 Force while pressing beats a 0-Force producer |
| 2 | Scorpion v Spider T13, pressing | Utoro 4F c6 | Kasumi 4F c5 | equivalent (same Force, cheaper) |
| 3 | Lion v Dragon T7, nobody home, their 9 against Strength 7 | Copper Mine | Ikoma Natsu 3F | right: once they have Personalities and home is threatened, a body |
| 4 | Lion Legacy v Crane T7, home holds (11 v 11 + 9), 6 Gold | Merchant Atoll +5G c6 | Akodo Michitsu 2F c4 | unclear, leaning the Holding: it pays back in two turns and the Lion is not behind on Personalities |
| 5 | Crane v Lion Legacy T4, nobody home, 12 Gold | Marketplace +2 c2 | Daidoji Tsunehiko 6F | right (PM 3.6: Personalities first; leftover on 2-Gold Holdings) |
| 6 | Scorpion v Unicorn T10 | Waru 4F c6 | Tsuji 5F c6 | equivalent (more Force, same cost) |
| 7 | Crane v Lion T4, nobody home, 13 Gold buys all three | Copper Mine | Doji Shunya 1F (makes home hold by his ability) | equivalent (everything is bought); the cheapest holding body over the 6-Force body is the seed's own preference |
| 8 | Crab v Lion T6, 15 Gold buys all three | Yasuki Jekku 2F | Yasuki Baiko 0F (holds by ability) | equivalent |
| 9 | Crane v Crab T15, 23 Gold buys all three | Doji Shunya | Daidoji Soken | equivalent |
| 10 | Crab v Crane T12, three Holdings, 21 Gold buys all | Prosperous Village +5 c6 | Iron Mine +2 c2 | equivalent |

**8 of 10 right or equivalent**, one wrong, one unclear; six of the ten were moot (the Gold paid for the whole affordable row, so only the order differed). Three changes: the Gold-producer preference applies only within 2 Force of the best body (#1); a new line 5b, proposed, buys a Holding that pays back within two turns when home holds and the seat is not behind on Personalities (#4; AB Q7c keeps the body first when behind); and the plan marks a decision **moot** when the Gold pays for every affordable card, so the shadow log says MOOT rather than DIFFERS and the sample draws only decisions that matter. Round 2 is the rerun.
