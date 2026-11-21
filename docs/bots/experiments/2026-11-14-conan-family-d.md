# Conan strategy campaign, step 3: family D (defence assignment) reading the Read (issue #1206, from 2026-11-14)

Branch `claude/1206-conan-family-d`, from main after PR #1235. Blocks: Mac range from 9700.

## Rule card (above the line)

Rule `readD`, opt-in, off by default. Fires at every `assignDefense` decision of the infantry segment where at least one Province is attacked (the Cavalry segment keeps the existing `CavalryDefensePlan`; the last stand keeps `LastStandProvince` unless `readD` finds an option with a higher hold probability). Rests on `PLAYER_MODEL.md` 3.3 and 3.4 (defend to kill, five instances; minimum bodies, three; concede, four), `RULE_FAMILIES.md` family D, and `ARENA_BEHAVIOUR.md` Q3b, Q3c, Q4d, Q4e.

```
for each attacked Province P with attackers A, from the Read (my hand exact, their hand from the belief):
   options:
     none        nobody
     presence    the fewest bodies that unlock my answers and survive being targeted: 1 + their usable printed neutralisers at P
                 + a share of their hand's (the belief's likely count), strongest first, capped by what I have
     kill        every unbowed unit with 2 Force or more (0 to 1 Force utility Personalities stay home)
     everything  every unbowed unit
   each option is run through the exchange under the open, likely and worst hand -> cell -> p_hold = 1 - PFall(cell);
   losses and attackers destroyed are taken from the likely run
   IF this is my last Province                           -> the option with the highest p_hold (ties: fewer losses)
   ELSE IF no option holds at likely                      -> none (keep the units and the cards)
   ELSE IF an option holds and destroys an attacker at likely -> the smallest such option (kill before everything)
   ELSE IF the hold depends on my answers (open holds only with my hand or abilities played) -> presence
   ELSE                                                   -> the smallest option that holds at likely
   IF they are on their last Province AND their attackers at P will be bowed on my turn
      AND the opportunity's best attack then razes it (p >= .5)   -> none
across Provinces: the Province whose loss costs most first (the second-last is worth 2, the last 4, the rest 1;
   ties by the attackers' Force); assign its option; the units used are gone for the next Province
```

Fire rate predicted: every attacked `assignDefense` decision in the infantry segment; about half of the opponent's turns (ARENA_BEHAVIOUR: attacked turns), so roughly 8 to 10 decisions a game.

Should move (profile measures, `tools/bots/behaviour/profile.py`):

- gift defences (Province lost and defenders destroyed): Contender 32% of defended battlefields, Conan 14%, Mendon 0 of 12 -> down;
- defended battlefields held with an attacker destroyed: Contender 7%, Conan 25%, Mendon 7 of 12 -> up;
- undefended Provinces lost with a neutralising card in hand and an unbowed Personality at home: 68 to 74% -> down;
- unbowed Personalities assigned when attacked: 77 to 78% -> fewer, better placed.

Decision tests (a case per branch): the last Province takes the highest p_hold even at a loss of units; nobody when nothing holds; kill when a big defence holds and destroys; presence when only a card in hand holds it (one body unlocks it, nobody loses it); none when their last Province falls to my attack next turn and their attackers will be bowed; the cross-Province order; utility 0 to 1 Force units stay home unless they are the presence body. Decision set: family D positions (6) must not get worse than T2's 1 of 6.

Shadow: 182 games with `readD` logging what it would change against what the Contender did; draw 10 decisions at random and judge each against the digest and the player model.

Screen: `T2 + read + readD` against T2 on a fresh block of 1,456 games (9700), compared with the A/A of the same block: net flipped games, the measures above, per-deck table.

## Results (below the line)

### 2026-11-14: decision tests and the decision set

Five `--filter defence` cases pass (nobody when nothing holds; kill with everyone; the smallest hold with the 1 Force unit home; presence by a card in hand, and the same hold conceded when ahead; the last Province's highest hold and their last Province conceded; the cross-Province order). Decision set, family D, T2 against `T2 + read + readD`: **T2 1 of 6, readD 3 of 6** after the first revision (G1 T9 kill agreed; G3 T10 and G7 T10 concede agreed), then **2 of 6** after the second (G3 T10: the Read now holds P2 with one unit on Force alone, losing nobody under the likely hand, where Mendon conceded; his choice took no risk on their hand, the Read's hold is free under the belief; recorded as a disagreement with its cause). G5 T13 and T25 (minimum bodies): the Read sends 3 and 2 bodies against his 2 and 2, the strongest first where he sent two copies of one utility Personality; the count agrees once, the units do not.

### 2026-11-14: shadow round 1 (block 9700, seed 9700, 364 games, `T2 + read`, readD off)

A/A on the seed: 51.1% (186 to 178). The Read's defence differed from the Contender's at 642 of 933 infantry defence decisions (69%): 247 times the Read defends with nobody where the Contender assigned (70 "holding would spend cards for no kill", 59 "nothing holds", 48 at the last Province, 41 with no infantry attacker yet, 20 conceding their last Province), 55 times the Read defends where the Contender passed (presence), the rest differ in size or place. Ten drawn at random (`random.seed(1206)`) and judged against the digest and the player model: **5 right, 2 unclear, 3 wrong**. The wrong three and their causes:

- a one-body defence (hold 0.76) preferred to two bodies (hold 0.90) because the rule ordered the holds by size first: now the surest hold first (within five points), then the fewest bodies;
- a hold on Force alone read as "by answers" because the exchange plays answers to win the battle, not only to keep the Province: now a bare run without my hand decides whether the hold needs answers, and a free hold (no answers, no losses) is taken before any concession;
- a Province conceded for free on "their last Province, their attackers bowed next turn" where one body held it without loss: the concession now applies only when the hold would cost units or cards.

Also from the sample: two Provinces conceded in one turn "while a Province is a price I can pay" (the concession now needs me still level or ahead with two or more after the loss, and happens once a decision); and 41 "nobody attacks" decisions where only the Cavalry was still to come (the Cavalry plans keep those). The unclear two: six units sent to kill where one body already killed three by answers (Mendon's "every unit with 2 Force or more"), and the double concession.

### 2026-11-14: shadow round 2 (same seed, revised rule)

559 of 864 infantry defence decisions differ (65%); the largest groups: the fewest bodies that hold surest and destroy (presence 113, one fighter 65, two 22), nothing holds so nobody (90), defend to kill with every fighter (57), my last Province (48). Ten drawn (`random.seed(2026)`): **5 right, 1 unclear, 4 wrong**. Causes of the wrong four:

- at the last Province, when no option holds under the likely hand, the rule picked nobody (same cell, fewer losses) three times where the Contender sent a body; Mendon's "unit losses do not matter" at the last Province means the body goes: ties in the hold now break on the attacker's margin after the exchange, which a body always narrows and whose answers it unlocks;
- a hold that lost six units to destroy three was taken (the only option with a kill); a hold that loses more units than it destroys is now conceded while a Province is a price I can pay, and taken only with no Province to spare.

The unclear one: two Provinces each held by one body with a card for one kill (the Contender put two bodies at one); recorded, not changed.

### 2026-11-14: shadow round 3 (same seed, revised rule)

517 of 864 decisions differ (60%). Ten drawn (`random.seed(3)`): **8 right, 1 unclear, 1 wrong**; the bar of seven is met. The wrong one: a Province already safe on Strength (5 Force against 6) was given a body that could only die, because the "nobody" option was never counted as a hold; now a safe Province gets a body only when that body destroys the attacker without a loss (PLAYER_MODEL.md 3.4's last line), and nobody otherwise. The unclear one: four fighters sent against a lone 21 Force attacker that two of them already kill; Mendon's "every unit with 2 Force or more" taken literally. Among the eight right: three defences where the Contender had passed and the Read holds (one body on Force alone twice, four bodies by answers once), two concessions of hopeless defences the Contender made (23 Force into Strength 0 with six bodies; 16 into Strength 6 with one), two "fewest bodies" defences against three the Contender sent, and a two-Province defence (three bodies killing three at one, one body holding the other on Force) where the Contender sent four to a third Province.

### 2026-11-14: the decision set after round 3

Whole set, T2 against `T2 + read + readD`: **9 of 50 against 10 of 50**; family D 1 of 6 against 2 of 6; no position that T2 had right is lost (the rule only touches infantry defence assignments). Seven `--filter defence` cases pass.

Block 9700 A/A (Contender against Contender, seeds 9700 to 9703, 1,456 games): **49.3%** (718 to 738). Its behaviour profile (`profile.py` on the logs, both seats): attacks that destroy a Province 60%, gift defences 20%, defended battlefields held with an attacker destroyed 21%, unbowed Personalities assigned when attacked 76%, a neutralising card in hand when an undefended Province fell with a body at home 70%.

### 2026-11-14: screen, formulation 1 (block 9700)

`T2 + read + readD` against the Contender on the A/A's seeds: **49.2%** (716 to 740) against the A/A's 49.3%; per game, **99 gained, 101 lost, net −2** (sign test p = 0.94). Per deck piloted: Lion Legacy +20/−6 and Unicorn Maidens +8/−3 are the gains, Scottish Lion +11/−16, Crane preset +3/−10 and Phoenix Talons +4/−10 the losses. The profile (the arm's seat against the A/A's mirror):

| Measure | readD | A/A | Wanted |
| --- | --- | --- | --- |
| Unbowed Personalities assigned when attacked | 59% | 76% | fewer, better placed |
| A neutralising card in hand when an undefended Province fell with a body at home | 59% | 70% | lower |
| Gift defences (Province lost and defenders destroyed) | 22% | 20% | lower |
| Defended battlefields held with an attacker destroyed | 18% | 21% | higher |
| A Province lost on the turn after an all-in attack | 39% | 38% | lower |

The rule moves what it says it moves about placement (fewer bodies, fewer answers left unplayable), and does not move what wins: gift defences and kills are no better, and the win rate is the A/A's. **Formulation 1 screens null** and is shelved with these numbers. Formulation 2 (`rdDefend`): the same reading without the two concessions "while a Province is a price I can pay" (the card-only hold and the costly hold), screened next on the same block.

### 2026-11-14: screen, formulation 2 (block 9700) and the family's verdict

`T2 + read + readD + rdDefend` against the Contender on the same seeds: **49.2%** (717 to 739); per game **94 gained, 95 lost, net −1**. The deck table is formulation 1's within a game or two (Lion Legacy +20/−5, Scottish Lion +11/−15, Crane preset +2/−10, Phoenix Talons +4/−10). Profile: bodies assigned 61% (A/A 76%), undefended losses with an answer in hand and a body at home 57% (70%), gift defences 22% (20%), held with an attacker destroyed 18% (21%). The two concessions fire too rarely to tell the formulations apart; both read the same games the same way nearly everywhere.

**Family D is done as the brief defines it: two formulations tested and shelved with their evidence.** What the evidence says, exactly:

- The Read's defence assignment is judged right on 8 of 10 drawn decisions against the digest and the player model, agrees with Mendon on 2 of 6 family D positions where T2 agrees on 1, and changes 60% of the Contender's infantry defence decisions, mostly by sending fewer bodies, defending Provinces the Contender gave up, and conceding hopeless defences the Contender made.
- Against the Contender it does not change who wins: two blocks of 1,456 games, net −2 and −1. The two measures the family named as its targets, gift defences and defences that destroy an attacker, do not improve: the Read defends less, so it holds and kills less, and when it does defend it is wrong often enough to keep the gift rate where it was.
- The deck table says the rule helps the Lion decks (Lion Legacy +20/−5 twice) and hurts Scottish Lion, Crane preset and Phoenix Talons: a defence read that is right for a Force deck and wrong for decks that win battles with actions, which is where the belief's prior is weakest (the experiment note of 2026-11-13: likely rarely differs from open).

What this means for the campaign: the brief's premise is that the rule families, each reading the Read, add up against the Contender. The first family does not add by itself. The defence decision may simply matter less than attack and Dynasty against a bot that attacks on Force balance; and the belief, which the defence leans on through "likely", has not yet earned its keep against these opponents. Family A (the attack, where the Read's shape already matches Mendon's on every split and lone position) is the next test of the premise, and should be read against the deck table here: the decks that lost under readD are the ones whose battles are decided by actions.

`readD` and `rdDefend` stay in the code as opt-in rules, off by default, with their tests; the shadow log under `read` stays as an instrument.

