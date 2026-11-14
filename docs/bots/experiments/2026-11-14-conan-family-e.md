# Conan strategy campaign, step 3: family E (inside a battle) reading the Read (issue #1206, from 2026-11-14)

Branch `claude/1206-conan-family-e`, from `main` at 7c5dfd4a (after PR #1238). Blocks: Mac range from 10000 (shadow on seed 10000; the screen and its A/A on 10100).

## Rule card (above the line)

Rule `readE`, opt-in, off by default. Fires at every Battle priority of the seat's own (`Phase.Battle`, no pending choice, the battle step's decision player is the seat) where at least one offered action can be read: a `useAbility` or `playStrategy` whose effect descriptor (`EffectDescriptors.Of`) is Battle-timed and either neutralises an enemy Personality present at this battlefield (send home, bow, destroy, Force cut, lock, duel) or adds Force. Reactions, Honor plays, moves, attachments and anything the descriptors do not class are **not read** and keep the Contender's score (formulation 1).

It rests on PM 3.5 (pass while ahead; kill, do not repel; order; target; printed before hand before the Favor; late Force) and PM section 7 rows 1, 7 and 8 (late Force after the defender passed, a Province each time; pass or kill in a won battle where the Contender sent home; strike at once), and on AB section 5 (actions decide battles more than Force; half the losers of military games end holding a neutralising card).

```
read the battle as it stands: Exchange.Run from the Read with ME PLAYING NOTHING MORE and them playing their likely hand (the belief) and table
tier (attacking): 0 my army loses, 1 tie, 2 wins without the Province, 3 the Province falls          want 3
tier (defending): 0 the Province falls, 1 holds but my defenders die, 2 holds and nobody dies, 3 an attacker dies   want 2
IF standing tier >= want                      -> pass; EXCEPT a free destroy (printed or table, no Gold), or a free bow that lifts the tier, on a unit here: take it; a send-home never (the unit would survive)
ELSE one action after which the standing tier >= want                 -> the cheapest: printed > table (Stronghold, Holding) > hand > Favor;
                                                                          destroy > bow > Force cut > lock > duel > Force add > send home; the target carrying unspent abilities first;
                                                                          the minimum Force add that crosses (Tactical Advantage: the discard with the smallest sufficient Focus)
ELSE IF everything I have (hand included) reaches want                 -> the first step in the same order, the send-home last
ELSE (short of the want)                                                -> the highest tier one action still reaches, IF it is free (a printed or table ability at no Gold),
                                                                          OR the standing tier loses the Province (defending) or the army (attacking: it dies or ties): then a card is spent for it;
                                                                          otherwise spend nothing
deciding battle (a last Province on either side): read against their OPEN hand; a costly action that lifts the tier is played
late Force: a Force add waits while they have an unbowed unit here and have not passed (I am given priority again after their pass)
```

The Fight chooses the target itself: one option per target the engine lists (which enemy to hit, which card to discard for Force); `Prepare` takes the Fight's target. Duels (focus/strike) are left to the champion's `DuelReply` (it already strikes at once with the higher stat).

**Should move** (RULE_FAMILIES.md E): enemy Personalities destroyed per battle won; battles lost by 0 to 3 Force with late Force unused; cards in hand at a military loss (Q4e); agreement on PM section 7 rows 1, 7 and 8. **Predicted fire rate**: the first shadow block says T2 and the Fight differ at most Battle priorities (3,009 of 3,672 read priorities on seed 10000: T2 acts where the Fight passes in 970, the Fight acts where T2 passed or did otherwise in about 240, the rest T2 playing an action the Fight does not read); the rule will fire several times a game.

## Decision tests

`tests/L5R.AI.Tests/FightTests.cs` (`--filter fight`, 7 cases): won as it stands passes; a free destroy is taken in a won battle and a send-home never; the printed bow before the hand send-home; late Force waits, then is added after their pass, and at once when nobody unbowed defends; a lost battle spends nothing and a deciding one plays the cut; several actions needed: the bow first, the send-home last; the ability carrier is bowed first.

Decision set (`L5R_POS_FAMILY=E L5R_POS_ARMS="t2;readE:+read,readE"`): T2 agrees with Mendon on 6 of 18 family E positions; `readE` on **12 of 18**. Fixed: the five late-Force positions (G3 T3, G3 T9, G7 T3, GT T5 a and b: Tactical Advantage after the defender passed, with the smallest sufficient Focus; at G3 T3 he discarded a 3-Focus card at 4 against Strength 7, the Fight a 4), G4 T11 (the army bonus) and G6 T11 (the printed destroy; before the Favor was matched as an action the arm played the Favor, which the Fight reads as not legal on its "attacking" condition). Still differing, each a judgment the shadow decides: G7 T7 a and b (he passed in a won battle, the Fight spends a hand card to turn the won battle into a fallen Province, tier 2 to 3); GT T7 (he destroyed the defender with a hand card, the Fight adds Force with two Tacticians and takes the Province); G6 T6 order and melee (the Fight waits for late Force or passes; the Contender then plays Unstoppable Strike, a grant the Fight does not read); G7 T5 target (the Fight and T2 destroy the other of two equal attackers). Broke none that T2 had right.

## Shadow (seed 10000, 364 games, `t2,read` against `t2`)

The shadow logs every Battle priority the Fight reads (`## challenger fight (family E in shadow)` in the game logs) as `same` or `DIFFERS`, with the position, the standing and reach projections and every option's two tiers. Ten `DIFFERS` lines are drawn at random (`random.seed(1)` over the sorted logs) and judged against the digest and PM 3.5.

**Round 1** (artifacts `shE2_100`; the first run `shE_100` was superseded by the target choice and the Focus sizing before any decision was drawn): 3,000 DIFFERS, 771 same. Judged 5 right, 4 wrong, 1 unclear.
- Right: a duel aimed at the attacker after which everything reaches a hold (T2 duelled the other); pass with the Province already falling to 30 Force; pass in a battle won on Force where T2 spent a destroy from hand; pass at 3 against 0 where the defenders die anyway and T2 used a table ability that changed nothing; a hand bow before the Favor as the first of the several actions a 20-Force attack needed.
- Wrong, two causes: (1) a Holding's free bow that kept the Province (tier 0 to 1) was refused because "free" meant a unit's printed ability only; (2) a card that saved the Province (defending, Costly Feud at 0 against 8) or the army (attacking at 12 against 14, a hand bow that turned a lost battle into a won one; attacking at 7 against 2 under two likely destroys, a send-home of the only defender) was refused because the lost branch spent nothing short of the full want.
- Unclear: a Holding's move-in at 0 against 10 with no hand, which the Fight does not read.
- Revision: free = a printed or table ability at no Gold, Honor or discard; short of the want, the highest tier one action reaches is bought with a card when the standing tier loses the Province (defending) or the army (attacking: it dies or ties), free actions lift any tier; otherwise nothing is spent. `FightTests` gained the four cases.

**Round 2** (artifacts `shE3_100`, draw seed 2): 2,902 DIFFERS, 869 same (the Fight differs from T2 at 77% of the priorities it reads, about 8 a game; T2 acts where the Fight passes in 1,816 of them). Judged **8 right, 0 wrong, 2 unclear**.
- Right: pass at 4 against 2 where the defenders die anyway and T2 spent a hand bow; in a deciding battle at 5 against 7 the printed send-home that saves the army (tier 0 to 2) where T2's table bow gave a tie; the printed bow that saves the defenders (the same ability and target as T2, another performer); pass at 5 against 0 where T2 sent the dying attacker home with a hand card (PM row 7 exactly); the table bow that takes the Province at 26 against 24 where T2's bow left it standing; pass at 2 against 7 where a Force cut would be wasted; pass at 7 against 4 with the attackers dying where T2 straightened; the hand bow that takes the Province at 11 against 4 where T2's printed bow left it standing.
- Unclear: two lost attacks at 0 Force where T2 played a card the Fight does not read (Defensive Stance) or one the Fight had marked "not legal on its conditions" (a destroy the engine offered). The second is a flaw: the Fight's legality reading of the printed conditions was stricter than the engine's, so an offered action could be barred. Revision: an option the engine offers is legal as offered; the descriptor's conditions serve only the projection of the other answers.
- The shadow passes (8 of 10). The legality revision is applied before the screen; a third draw on the screen's own logs is recorded below.

**Round 3** (artifacts `shE4_100`, the Fight as screened, draw seed 3): 2,812 DIFFERS, 959 same. Judged **9 right, 1 wrong**.
- Right: pass where the defenders die anyway and T2 spent a printed cut, a hand bow or a draw; a free printed destroy in a battle already won; the hand bow that saves two defenders and kills the attacker (T2 played an unread card); the same bow and target as T2 with another performer; late Force held while they have an unbowed unit and have not passed, at 16 against 21 (PM 3.5 as written, with the risk that their duel bows the Tactician first); pass with every attacker bowed; pass at 4 against Strength 6 where a +1 changes nothing.
- Wrong: attacking at 2 against 6 with the army dying, a hand destroy on the 6 and then Tactical Advantage would have won the battle (tier 2); the Fight looked for sequences that reach the Province only (tier 3) and spent nothing. T2 played the destroy. Revision written as **formulation 1b**, built after the screen of formulation 1: when losing or deciding, the goal is the best tier everything reaches, so a sequence that saves the army is started as one that takes the Province would be (`FightTests` gained the case).

## Screen (block 10100, `t2,read,readE` against `t2`, 1,456 games, with battle records and fire counts)

A/A on the block (`ca101`, `t2` against `t2`): **49.2%** (717 of 1,456).

**Formulation 1** (`rE101`): **45.9%** (668 of 1,456; CI 43.3-48.4), against the A/A **+132 / −181, net −49**, p = .007. Soundness clean. `readE` fired **8.39 times a game** (on in every decision, 131 a game; a fire in 1,362 of 1,456 games): the most active rule in the set, four times econ's 5.2.

| Measure (challenger's battles, battle records) | A/A challenger | A/A opponent | readE | its opponent |
| --- | --- | --- | --- | --- |
| Battles fought | 5,817 | 5,817 | 5,856 | 5,856 |
| Battles won | 2,882 (50%) | 2,935 | **2,561 (44%)** | 3,295 |
| Enemy Personalities destroyed per battle won | 1.24 | 1.20 | 1.29 | 1.21 |
| Narrow losses (0-3 Force) with cards in hand, of battles fought | 11% | 10% | 9% | 12% |
| Cards in hand at a military loss (last declaration) | 3.16 | 3.29 | **3.83** | 3.21 |

Profile (`profile.py`): attacks that destroy a Province 59% against the mirror's 61%; attacks that lose a Personality 24% against 21%; attacks that achieve nothing 30% against 27%; defended battlefields held with an attacker destroyed 17% against 19%; a neutralising card in hand when an undefended Province fell 75% against 68%. Per deck: Lion Legacy +15/−8 and Crab Control +16/−9 gain; Crane Dominance +11/−24, Scottish Lion +10/−23, Phoenix Talons +8/−19, Unicorn Maidens +6/−15 lose.

**Reading.** The Fight wins fewer of the battles it fights and dies with more cards in hand: it gives battles up that T2 fights for. The branch that does it is "cannot reach ... spend nothing" (about 2.6 decisions a game where T2 acted), and it is judged against the opponent's *likely* hand: the belief's synthetic answers assume the unseen cards will answer, so the Fight sees no way through and keeps its cards. The decks that lose are the action decks (Crane Dominance, Scottish Lion, Phoenix) whose battles turn on cards. The drawn decisions were judged right because the judge saw the same likely hand; the games say the belief is too strong for the spend decision, or that spending into an uncertain battle is right more often than the model's "spend nothing" suggests. **Shelved.**

**Formulation 2** (`readE,reOpen`, with 1b): the pass test keeps their likely hand (do not pass into a card they likely hold); the spend-or-abandon projections read their open hand (the answers they have shown), so unseen cards never make the bot give a battle up; and, when losing, the sequence that saves the army is started (1b). Screened on the same block, below.

**Formulation 2** (`rE2_101`): **47.4%** (690 of 1,456; CI 44.8-50.0), against the A/A **+131 / −158, net −27**, p = .13. `readE` fired 7.77 times a game; `reOpen` changed 0.88 decisions a game (a fire in 537 games): the open-hand reading changed fewer than one decision in eight of the Fight's. Battles won 2,654 (45%) against the A/A's 50%; enemy Personalities destroyed per battle won 1.32 (A/A 1.24); narrow losses with cards in hand 10%; cards in hand at a military loss 3.63 (A/A 3.16). Profile: Provinces destroyed per attack 59% (mirror 61%), Personalities lost 24% (21%), nothing achieved 28% (27%), attackers destroyed while defending 17% (19%), a neutralising card in hand when an undefended Province fell 71% (68%). Per deck: Crab Control +11/−7, Lion Legacy +11/−8, Classic Crane +8/−7 gain; Crane Dominance +15/−22, Scottish Lion +8/−18, Unicorn Maidens +8/−17 lose. Decision set 13 of 18, as formulation 1.

## Verdict (below the line)

**Family E is done as the brief defines it: two formulations screened and shelved**, `readE` and `readE,reOpen`, both opt-in and off by default; nothing becomes a default.

What the evidence says:
- The Fight plays the player model's battle (pass while ahead, kill rather than repel, the cheapest sufficient action, late Force last, spend nothing in a lost battle) and the model's own positions agree with it twice as often as with T2 (13 against 6 of 18). Against T2 it loses: −49 and −27 games in 1,456. The loss is in the battles themselves: the Fight wins 44-45% of the battles it fights where T2 wins 50%, and it dies holding more cards. The decks that lose are the action decks (Crane Dominance, Scottish Lion, Phoenix Talons, Unicorn Maidens), whose battles turn on cards; the Force decks (Crab, Lion Legacy) gain a little.
- Reading their open hand instead of their likely one moved 0.9 decisions a game and recovered 22 of the 49 games. The remaining gap is not the belief: it is the projection itself. The exchange is a greedy, one-line reading of the battle (each side plays the answer that takes most Force off the other, in a fixed order); it was built and calibrated (PR #1223, #1234) to predict whether a Province falls, which it does better than the paper margin, not to choose the action inside the battle, where T2's triage (what a target can still do, printed before hand, no spending on a comfortably won battle) and its `conf`/`cutoff` rules read the opponent's remaining options more finely than one projected line. Spending a card into an uncertain battle is right more often than the model's "spend nothing" allows, and passing on a projected win costs the battle when their reply is not the one projected.
- The three shadow rounds (5, 8, 9 of 10 right) judged the decisions against the same projection the Fight used; the draws could not see what the games then showed. For the next families the judge must read the game on from the position (the brief's fork test), not the projection.

What to carry forward: the fire counters say the Fight is the most active rule of all (8 a game), so a battle rule has leverage; the shelved families now have a measure of their own (`tools/bots/fight.py`); and the Read's exchange should be used in battle only as a candidate generator for a playout (step 4: "its exchange as the playout's battle policy"), where the game itself judges the line.

Mac blocks used: 9100 to 10100; next fresh block 10200.
