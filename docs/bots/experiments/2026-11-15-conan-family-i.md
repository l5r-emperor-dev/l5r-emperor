# Conan strategy campaign, step 3: family I (the Favor, the opponent's turn and the hand) reading the Read (issue #1206, from 2026-11-15)

Branch `claude/1206-conan-family-i`, from `main` after family G's PR. Blocks: Mac range from 11200 (shadow on seed 11200; the screen and its A/A on 11300).

## Rule card (above the line)

Rule `readI`, opt-in, off by default: **the hand and the Favor**. Three decisions, each read from the Read's descriptors and exchange, never from a card's name. It rests on PM 3.7 (hold the Favor: spent on defence when level or behind, never to cycle a card; four declined cycles; keep the hand when it holds three or more removal or movement cards: G3 T5, G7 T7, G4 T3; at the hand limit discard the card with the fewest live uses in this matchup, keeping one-shot cards that scale with their investment: G5 T12 to T24, G6 T5) and PM 7 rows 7 and 11 (the Favor spent on a send-home in a battle already won, G7 T7, G6 T11, G1 T9, GT T7: two or three Personalities survive and the Favor is gone; "discard hand, draw four" taken where the cards would have won the following battles).

```
hand answers = the hand's cards with a Battle descriptor that neutralises (send home, bow, destroy, Force cut, lock, duel) or moves a unit (the Read's HandAnswers)
1  the Favor in a battle (favor-battle offered):
     IF the exchange at this battlefield without the Favor already wins for me at likely (the Province holds, or falls, as I want it)   -> keep it
     IF defending AND the exchange without it loses the Province at likely AND with it (one attacker home) holds                      -> spend it
     IF attacking                                                                                                                       -> keep it (it moves an attacker home, with presence, on defence)
     never on the Favor's card cycle
2  an Event that replaces the hand (a yes/no choice whose text discards the hand and draws):
     IF the hand holds three or more answers                                                                                            -> no
     ELSE                                                                                                                               -> the Contender decides
3  the hand-limit discard: for each card, live uses = how many of their units in play (straightened) its conditions admit as a target now, or 1 for a card without a target;
     discard the fewest live uses first; among equals the Contender's own order; a card whose condition is their investment (attachments, Followers, cost) is kept while they can still invest
```

**Should move** (RULE_FAMILIES.md I): the Favor spent on a send-home in a battle already won (should reach zero); Gold the opponent spends per turn against decks with repeatable Holding-bow effects (not addressed in this formulation: their-turn disruption is the Contender's `openbow`); agreement on PM 7 rows 7 and 11 (E-G1T9-favor, I-G3T5 and I-G7T7 keep-hand, the four G5 discards). **Predicted fire rate**: the Favor decision is read whenever favor-battle is offered (a few times a game for the Favor holder); the hand Event is rare (one card in the pool among the fourteen decks); the hand-limit discard about once a game. Perhaps 0.5-1.5 fires a game.

## Decision tests

`HandTests.cs` (`--filter hand`, 7 cases): the Favor kept while the Province holds without it, spent when the attacker home turns the fall into a hold, kept when it changes nothing and spent at my last Province, kept when attacking; three answers keep the hand, two leave it to the Contender, the text test; the hand-limit discard by live uses (a destroy for Force 2 or less with no target goes first); a condition against the performer judged with my best unit.

**The decision set** (`L5R_POS_ARMS="t2;ri:+read,readI" L5R_POS_FAMILY=I`, with `L5R_POS_READ=1` printing `hand:` at Event and hand-limit positions): T2 agrees at none of the seven (0 of 7). With `readI`: the two "keep the hand" positions (A Dangerous Game, four answers in hand each) agree once the hand-replacing choice is recognised from the pending prompt (the Event card is in no zone the bot's card map lists while it resolves); E-G1T9-favor keeps the Favor as Mendon did but then plays My Enemy's Mercy where he passed (a family E decision; the Favor part is right); the four G5 hand-limit discards still differ: Mendon discarded Remember Your Ancestors (destroys an attacker; the Lion rarely attacked him), Incriminating Testimony and Sheathing the Sword as situational and kept Dismissing the Cur to T24 and Rocky Terrain throughout; the Read counts live targets now (their units straightened) and cannot see that a card needing an attacking target is dead against an opponent who does not attack, so it ranks Rocky Terrain (one use by default) and the Cur (its performer condition, now judged with my best unit) lowest. **2 of 7**, with the Favor right at a third. A first version counted a card with a performer-relative condition (Honor at most the performer's) as having no live use because the matcher got no performer; it now takes the best of my units.

## Shadow (T2 with the Read against T2, seed 11200, 364 games)

**Round 1** (`shI_112`, before the two fixes above; 5:46 wall, 8.5 s a game: the shadow runs the exchange at every Favor offer): the Favor was offered 204 times to the challenger; the Read differed at 161 (0.44 a game), all of them "the Contender spends, the Read keeps" (156 defending: the Province holds at likely without it; 5 attacking). The hand-limit discard was read 182 times and differed at 96 (0.26 a game), every difference a card with no live target against their units in play where the Contender discarded by its fixed card values. No hand-replacing Event is in the fourteen decks (0 decisions). Round 2 is the rerun with the fixes; ten of each kind are judged there.
