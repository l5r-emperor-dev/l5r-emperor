# Conan strategy campaign, step 3: family A (attack or hold, and the shape) reading the Read (issue #1206, from 2026-11-14)

Branch `claude/1206-conan-family-a`, from `claude/1206-conan-family-d` (PR #1236). Blocks: Mac range from 9800.

## Rule card (above the line)

Rule `readA`, opt-in, off by default. Fires at every `declareAttack` decision (attack or hold) and at the infantry assignment of the seat's own turn; the Cavalry segment keeps the existing plans. It reads `Questions.Of(read, trend).Opportunity` (PR #1235): the candidates of `RULE_FAMILIES.md` family A (hold; everything at the weakest Province; the smallest group that razes under the likely hand at each Province; a balanced split across two Provinces; the lone immune unit), each against the defender's best reply, scored `gain − weight(posture) × (exposure − exposure(hold))` through the calibration tables. Rests on `PLAYER_MODEL.md` 3.1 and 3.2 (the gates; the smallest group that suffices; one group per Province when their defenders are fewer than the Provinces I can threaten; the immune unit alone), `ARENA_BEHAVIOUR.md` Q1c to Q1e and Q6d, and the family D screen's deck table (the Read's reading helps Lion Force decks and hurts the action decks: Scottish Lion, Crane preset, Phoenix Talons).

```
at declareAttack:
   IF lethal (the existing rule: the attack ends the game)        -> declare (unchanged)
   ELSE IF the opportunity's best is hold                           -> pass
   ELSE                                                             -> declare
at the infantry assignment:
   the opportunity's best assignment, restricted to the units and Provinces the engine offers,
   Cavalry held back for its own segment as today (HoldCavalry)
```

Fire rate predicted: every own turn with an unbowed Personality at home (about 95% of turns); the Contender declares on 96 to 99% of turns when 7 or more Force ahead, so the visible change is the hold on turns the Contender attacks and the shape on turns it stacks one Province.

Should move (profile measures): attacks that destroy a Province (Contender 60%, Conan 72%) up; attacks that achieve nothing (27%) down; turns with two or more Provinces destroyed per 100 attacks (7.7) up; attacks that lose a Personality (22%) down; and family C's "a Province lost on the turn after an all-in attack" (38%) down through the exposure term. Agreement on the decision set's family A positions (T2 2 of 9; the Read's shape already matched Mendon's on all seven split and lone positions in shadow).

Decision tests: the opportunity's own six cases (PR #1235) plus the hook: with `readA` on, the bot passes at `declareAttack` where the opportunity holds and declares where it attacks, and its infantry assignment is the opportunity's shape.

Shadow: with `read` on and `readA` off, the opportunity's best beside the Contender's attack (`BotContext.AttackLog`, in the game log), one seed of block 9800; ten drawn at random and judged.

Screen: `T2 + read + readA` against T2 on block 9800 (1,456 games), against the A/A of the same block: net flipped games, the measures above, per-deck table read against family D's.

## Results (below the line)

### 2026-11-14: decision tests and the decision set

Two `--filter read-a` cases pass (declare and assign where the opportunity attacks, pass where it holds; the shadow changes nothing and logs the opportunity). The first run of the rule passed at four of Mendon's split positions although the opportunity attacked: the ordinary attack score judged the Read's split with the old estimator and ranked it below passing; the opportunity's own shape is now scored as the attack. Decision set, whole, T2 against `T2 + read + readA`: **9 of 50 against 11 of 50**; family A **2 of 9 against 4 of 9** (G6 T11 the immune unit alone and G4 T5 the hold now agree; the split positions attack with two groups but place them on other Provinces than his); no position T2 had right is lost.

### 2026-11-14: shadow round 1 (block 9800, seed 9800, 364 games, `T2 + read`, readA off)

A/A on the seed: 51.1% (186 to 178). At the declaration the opportunity differed from the Contender 450 times in 2,575 (17%): 314 holds where the Contender declared, 136 declarations where it held. At the infantry assignment it differed 347 times in 694 where both attacked (the Contender's one Province against the Read's split, or the Read's hold after the Contender's declaration). Ten drawn (`random.seed(1206)`): **3 right, 1 wrong, 6 unclear**; the log held only the option chosen, not the position, so six could not be judged. Right: a split declared where home is lost whether I attack or not; two holds from the gates (have below need everywhere; no group exceeds Strength). Wrong: an attack under the lock posture that raised the exposure by half a Province for one expected Province and two kills; Mendon's lock attacks only when it costs nothing. Fixed: under lock and drain an attack may raise the exposure by at most 0.1 Provinces; and the log now carries the position (each side's home Forces, Provinces, Honor, hand, the clocks) and the four best options with their scores, so the next round can be judged.

### 2026-11-14: shadow round 2 (same seed, with the position in the log)

Block 9800 A/A (seeds 9800 to 9803, 1,456 games): **47.9%** (698 to 758). Shadow on seed 9800: 1,277 decisions differ (declaration or assignment). Ten drawn (`random.seed(2026)`): **3 right, 3 wrong, 4 unclear**. Right: a declaration where the opponent's Honor wins in three turns and holding loses; a hold from the gate "have below need" under the drain posture where the Contender attacked 11 Force into 9 plus 7; a desperate attack with the last unit where home is lost whatever I do. Wrong, and fixed: (1) two units sent to take their last Province where everything should go (PLAYER_MODEL.md 3.2: the last turn sends everything; the exchange's cell cannot see the safety the rest buys), now everything when their last Province falls; (2) a hold where their Honor stood at 45, winning at the start of their turn, because the exposure term outweighed the gain; now "they win before my next turn" is a race whatever my clock. (3) A hold at their last Province with 42 Force against two defenders (6 Force) and Strength 6, read as held under every hand: the game shows the attack razing with 25 Force to spare; the Phoenix Talons table (Holdings and Spells with Battle bow, send-home and destroy effects, two of them needing the one Shugenja) was counted as more answers than it could play. Diagnosed below. Unclear: an assignment of six units where the Contender sent five into Strength 10 with home lost either way; a split spending four cards under the race posture; a hold by the gate against a lone 19 Force unit the exchange has neutralised by a printed ability; a hold where the opponent's Honor wins in two turns and no attack changes it.

**The Phoenix table, diagnosed.** Rebuilt in a test (`ReadTests`, "the Phoenix table"): the exchange let the defender play six answers against nine attackers: a destroy by the Shugenja's Spell (bowing him), then a second Spell by the same bowed Shugenja, two Holding bows, the Personality's own bow, and Scouring Flood, whose text targets a *defending* Personality (a rescue of their own unit) and which the descriptor had as an enemy send-home with the condition "defending". Three faults in `Exchange`, all fixed: an ability whose cost bows its performer now bows that unit in the projection (its Force leaves the count and its other abilities leave the list); an answer naming a performer keyword needs an unbowed unit with that keyword present; an effect conditioned on an attacking or a defending target is legal only against that role. After the fix the same table plays a destroy, a bow and two Holding bows, and 19 Force against 6 plus 6 razes, as the game did. The battle Read's and the threat's calibration tables (`Calibration`, fitted on blocks 9400 and 9600 with the old exchange) are now slightly off for defences by action decks; they should be refitted on a fresh block before the tables carry weight in a promoted rule (recorded as a follow-up, not done here).

### 2026-11-14: shadow round 3 (same seed, after the exchange fix)

1,186 decisions differ. Ten drawn (`random.seed(3)`): **7 right, 1 unclear, 2 wrong**; the bar of seven is met. Right: four holds from the gates where the Contender attacked into Force it could not beat (8 into 5 plus 7; 6 into 7; 2 plus 2 into 14; 15 into 7 plus 10), a hold by the exchange against a lone 20 Force unit that one printed answer voids, a lock hold with my Honor five turns from the win and their fastest clock eight, and a race declaration two turns from the military win. Wrong, and fixed: (1) two 0 Force utility Personalities sent into a contested battle as part of "everything" (PLAYER_MODEL.md 3.2: a weak Personality is a target, not security); the attack's army is now the units with 2 Force or more; (2) a "nothing to lose" race declared with my last Province still held two times in three by staying home; that race now needs the hold to be lost anyway (exposure at least 0.85 of the Province's worth). Unclear: a split of a 10 and a 4 against an 11 and a 3 defending Strength 10 Provinces, trading units for no Province, where the Contender stacked both at one Province.

Decision set after the fixes: whole set T2 9 of 50, `readA` 12 of 50 (A 4 of 9, G 1 of 5: the G5 T20 hold now agrees); nothing T2 had right is lost.

### 2026-11-14: screen, formulation 1 (block 9800)

`T2 + read + readA` against the Contender on the A/A's seeds: **47.6%** (693 to 763) against the A/A's 47.9%; per game **144 gained, 149 lost, net −5** (sign test p = 0.82). Per deck piloted: Crane Dominance +16/−5, Dragon Destruction +11/−3 and Unicorn Maidens +17/−9 gain; Lion preset +8/−18, Scottish Lion +6/−17 and Spider Conquer +12/−15 lose. The profile (the arm's seat against the A/A's mirror):

| Measure | readA | A/A | Wanted |
| --- | --- | --- | --- |
| Attacks that destroy a Province | 62% | 59% | higher |
| Attacks that achieve nothing | 24% | 27% | lower |
| Turns with two or more Provinces destroyed, per 100 attacks | 8.3 | 7.6 | higher |
| Attacks that lose a Personality | 25% | 22% | lower |
| A Province lost on the turn after an all-in attack | 39% | 37% | lower |

The rule moves the attack's own measures the right way by three points each (more Provinces, fewer empty attacks, more double turns) and pays for it in Personalities lost and in the counter-attack; the win rate is the A/A's. **Formulation 1 screens null.** Formulation 2 (`raShape`): the Contender keeps the attack-or-hold decision, the Read gives only the shape of the infantry assignment, screened next on the same block.

### 2026-11-14: screen, formulation 2 (block 9800) and the family's verdict

`T2 + read + readA + raShape` (the Contender decides attack or hold; the Read gives the shape): **46.8%** (682 to 774) against the A/A's 47.9%; per game **54 gained, 70 lost, net −16** (sign test p = 0.18). The attack's measures move the wrong way: attacks that destroy a Province 56% (A/A 59%), attacks that achieve nothing 30% (27%), double turns 6.8 per 100 (7.6). So the shape alone costs, and the gains formulation 1 showed in those measures came from its holds (the gates and the exposure term), which cost the same amount elsewhere (Personalities lost, the counter-attack).

**Family A is done as the brief defines it: two formulations tested and shelved with their evidence.** `readA` and `raShape` stay opt-in and off by default; the shadow log (`AttackLog`, with the position and the four best options) stays as an instrument. Three exchange faults found through this family are fixed and tested (the bowed performer, the performer keyword, the attacking or defending target).

### What two shelved families say about the campaign

Both families read the Read, were judged right on 7 or 8 of 10 drawn decisions against the digest and Mendon's model, agreed with Mendon on more of his positions than T2 does (A 4 of 9 against 2, D 2 of 6 against 1, the whole set 12 of 50 against 9), moved the behaviour measures they named, and left the win rate against the Contender where it was (net −2, −1, −5, −16 on blocks of 1,456 games). Three readings of that, for Mendon to weigh:

1. **The Contender is the wrong yardstick for these rules.** It attacks on Force balance and defends with everything; the Read's rules trade with it even where they are right by the model, because the Contender's errors are symmetric (it attacks badly and defends badly) and a bot that attacks and defends better by the model gains and gives back the same games. The decision set and the behaviour profile show the rules doing what the brief asked; the win rate does not. The brief's stage 1 goal (80% against T2) may not be reachable by making the bot more like Mendon one family at a time; it may need the families together (step 3's last check, "the promoted set as one arm") or the lookahead with the Read's shapes (step 4), which the brief placed after the families.
2. **The belief has not earned its keep.** The "likely" hand rarely differs from the open one against these bots (experiment note of 2026-11-13); the exchange's cells separate battles well at defence time but the attack and defence rules lean on "likely" at declaration, where it is weakest. A belief that learns per deck archetype from what the bots show (the step 2 report "before and after the first reveal" was inconclusive by base rate) is the one Read component not yet tested on its own.
3. **The calibration tables are stale.** `Calibration` was fitted before the exchange fix of 2026-11-14; refitting on a fresh block (threat and battle records on, blocks 9900 and 10000) is cheap and should come before any rule leans on them again.

Nothing here becomes a default. The next step on the Mac waits for Mendon's reading of this note: continue the families (E, C, B, F, G, I) as the brief orders, or take reading 1 and test the families together, or step 4 first.

