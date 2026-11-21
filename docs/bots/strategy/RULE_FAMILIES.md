# Rule families: seed hypotheses for the Read

Each family below is one area of decisions, with the arena symptom that says it leaks games, what Mendon does there, a seed rule written against the Read, and the measure that should move if the rule is right. The seed rules are **hypotheses to test, in the order of the brief's step 3**, not specifications. Change them when the evidence says so and record why.

Vocabulary (defined in `../CONAN_STRATEGY_BRIEF.md` section 4):

- `exchange(A, D, P)`: the projected battle of attackers `A` against defenders `D` at Province `P`, under three opponent hands: `open` (public abilities only), `likely` (the belief), `worst` (the belief's upper bound). Each gives: does the Province fall, which units die on each side, what each side spent.
- `p_fall`, `p_hold`: the calibrated probability read from those three results.
- `need(A, D, P)`: the fewest defenders that must be neutralised for `A` to exceed the rest plus Province Strength.
- `have(side, P)`: that side's neutralisers usable at `P` now, by class, with a legal performer, payable, unspent.
- `threat(home)`: the opponent's best attack on its next turn against the units that will be unbowed at home, as Provinces at risk, units at risk and attackers I would destroy.
- `clock(side, route)`: turns until that side wins by that route at current rates.
- `posture`: press, build, hold, race, lock or drain.

Sources cited as PM (`PLAYER_MODEL.md`), AB (`ARENA_BEHAVIOUR.md`), RD (`RULES_DIGEST.md`), CM (`CODE_MAP.md`).

## A. Attack or hold, and the shape of the attack

**Symptom.** Conan's attacks take a Province 72% of the time and the Contender's 51 to 59% (AB Q1c). Force on paper explains little of that: with a paper margin of 0 to +5 the Contender's attacks still fail 40 to 51% of the time, and with a margin of -9 to -1 they succeed 31 to 34% (AB Q1e). The bots attack on Force balance (96 to 99% of turns when 7 or more ahead, AB Q1d); Mendon made 9 of 16 attacks at a negative paper margin, took a Province with 4 of them and lost a Personality in 1 (AB Q1e), because he counts neutralisers and late Force (PM 3.1, 3.5).

**Seed rule.**

```
candidates:
  sufficient  the smallest group G for which exchange(G, best defence, P).likely falls
  split       IF their unbowed defenders < Provinces I can exceed: one group per Province,
              each reaching Strength with a finisher available, margins balanced
  lone        IF every answer they have shown is conditional AND one unit fails every condition: that unit alone
  everything  only when this attack ends the game, or posture is race
  hold
gates (PM 3.1):
  IF no group exceeds Strength undefended, counting late Force        -> hold
  IF they have no unbowed Personality                                -> attack with what I have
  IF have(me, P) < need(G, D, P) for every G, P                      -> hold, unless their army will be bowed next turn (punish then)
for each remaining candidate c, against the defender's best reply among {none, all at one Province, one per Province}:
  gain(c)     = sum over battles of p_fall x Province value
                + expected enemy units destroyed - expected own units lost - cards spent
  exposure(c) = threat(home left by c, plus the bodies I will buy this Dynasty)
  score(c)    = gain(c) - weight(posture) x (exposure(c) - exposure(hold))
choose the best score; a failed attack that loses no unit costs only the cards it spent
```

Late Force (a Tactician's discard, an army bonus, a unit created in battle) counts in `gain` at declaration and is spent by family E.

**Should move.** Provinces per attack; attacks with "nothing achieved" (Conan 13%); attacks at paper margin -10 or worse that achieve nothing (Conan 40% of 325); agreement on PM section 7 rows 1 to 3 and 9.

**Traps.** The enemy units an attack destroys do not attack next turn: `exposure` must be computed after the expected kills, or the rule repeats the passivity of the 2026-11-07 encoding (brief section 2). A veto alone lost 64 games; this family must also *add* attacks the bots do not make today.

## B. Cavalry forcing

**Symptom.** Conan assigns 85% of its Cavalry Personalities in the Infantry segment, where the Contender holds 80% of its own for the Cavalry segment (AB Q5a); check the lookahead's `AttackShapes` first. Cavalry-segment attackers reach a battlefield with no defender about 60% of the time, the defender answers in its own Cavalry step in 0 to 2% of cases, and yet an undefended Province survives 28 to 39% of those arrivals (AB Q5b): the group sent did not exceed Province Strength.

**What the rules give** (RD section 2). The order is attacker Infantry, defender Infantry, attacker Cavalry, defender Cavalry. A defender must place its non-Cavalry units before seeing where Cavalry goes. At a battlefield with no defending unit the defender has no presence and can take no Battle action; only Reactions and the Province's Strength stand between a Cavalry group and the Province.

**Seed rule** (from Mendon's description and the rules; one game instance, PM section 5 item 1).

```
IF I have Cavalry units C (a unit is Cavalry only if the Personality and all its Followers are):
  infantry options:  nothing | a probe (a group that threatens Province X) | all infantry
  for each option, for each defender Infantry reply in
        {none, everything at X, one body per threatened Province, a stack on its most valuable Province}:
     follow-up = the best of {all C at the weakest uncovered Province,
                              C split across uncovered Provinces, each group exceeding Strength alone,
                              C reinforcing X, C stays home}
                 valued by exchange after their Cavalry reply (their unbowed Cavalry go where it hurts me most)
  forced(option) = the worst follow-up value over their replies
  attack with the option whose forced value is highest, IF it beats hold (family A scoring, exposure included)
  size each uncovered group to exceed Strength by itself, plus the Reactions they have shown
  Cavalry not needed for the fork stays home: it defends in the Cavalry step, after seeing their attack
defending with Cavalry:    place infantry where presence unlocks my answers; hold Cavalry for the Cavalry step
defending against Cavalry: treat every unassigned enemy Cavalry unit as able to arrive at any Province;
                           put one surviving body on each Province their Cavalry could take alone, when I hold answers
```

**Should move.** Share of Cavalry held for the Cavalry segment in decks with two or more Cavalry units; Provinces per attack for those decks (Conan 0.89 when all assigned as infantry, 1.03 when the Cavalry segment is used, AB Q5c); undefended Cavalry arrivals that take the Province.

**Open.** Whether Mendon probes with infantry, how much he keeps home, and how he defends against a Cavalry deck are not in the games. Prepare the screenshot interview (brief step 6) before treating any sizing as his.

## C. Home and the counter-attack

**Symptom.** Guard Force relative to the opponent's unbowed Force predicts the next turn better than anything else measured: under 25%, Conan is attacked 93% of the time and loses a Province 66%; at 100% or more, 26% and 5% (AB Q2c). On the turn after its own, the Contender destroys an attacker 7 to 14% of the time and Conan 11 to 21%; Mendon did on 6 of the 8 turns he was attacked after holding (AB Q2a). But holding units back is not free and is not what wins: matched on turn, Province counts and Force balance, an all-in attack wins as often as a partial one and more often than no attack (Conan 80% v 84% v 71%, AB Q2d).

**What Mendon does** (PM 3.3). A Province is a price he pays while ahead; he concedes rather than lose units or cards; when units stay home he makes them count. His stated standard for Conan is stricter: keep the Province, better keep the defenders too, best destroy the counter-attackers, by neutralising attackers and not by Force alone.

**Seed rule.**

```
planned home = unbowed units not attacking + bodies bought this Dynasty (they arrive unbowed)
their attackers next turn = their units that survive my attack (all straighten)
T = threat(planned home)
outcome tiers, best first:  hold and destroy attackers > hold, lose nobody > hold > lose a Province
IF T.likely loses a Province:
   remedies, cheapest first:
     a body bought this Dynasty that turns T to hold
     one surviving body plus a send-home, bow or destroy answer kept in hand (presence unlocks it)
     a unit kept back from the attack (costs gain in family A)
   accept the loss instead WHEN
     posture is race AND my clock is shorter than theirs after the loss, OR
     I stay ahead or level on Provinces AND keep two or more AND the remedy costs more than a Province is worth
IF T.likely holds: improve the tier only with resources the attack does not need
```

**Should move.** Provinces lost on the turn after an attack (Conan 0.30 partial, 0.39 all-in); attackers destroyed while defending; net Province swing per attack turn. Win rate decides; AB Q2d says the gain here is small unless the attack itself is kept.

**Open.** PM section 5 item 2: no evidence at two Provinces or fewer.

## D. Defence assignment

**Symptom.** Of the battlefields the Contender defends, 20 to 32% end with the Province lost **and** the defenders destroyed (Conan 14%, Mendon 0 of 12, AB Q3b). When an undefended Province falls, the defender still holds a neutralising Strategy in about 70% of cases, and in 458 of Conan's cases an unbowed Personality had stayed home that could have unlocked it (68% held one, 1.6 cards on average; AB Q4e). The two leaks pull in opposite directions (defend less, defend more); only a projection of the battle tells them apart. Whoever plays more neutralising actions wins the battle: the Province falls 45 to 58% of the time when the attacker plays more and 11 to 21% when the defender does (AB Q4d).

**Seed rule** (PM 3.4).

```
for each attacked Province P with attackers A, options:
   none | presence (the fewest bodies that survive their shown effects and unlock my answers)
        | kill (every unit with 2 Force or more) | everything
IF this is my last Province                      -> the option with the highest p_hold; unit losses do not matter
ELSE IF no option holds at likely                 -> none; keep the units and the cards
ELSE IF an option holds and destroys A at likely  -> kill
ELSE IF holding depends on my answers             -> presence
IF they are on their last Province AND their attackers will be bowed on my turn AND my attack then wins -> none
across Provinces: place defenders to maximise the sum of p_hold x Province value minus expected losses;
                  utility Personalities with 0 to 1 Force stay home unless they are the presence body
```

**Should move.** Gift defences per defended battlefield, by Provinces left (the last Province is the exception: 51 to 66% there is forced); "held and attacker destroyed" (Conan 25% of defended battlefields, Mendon 7 of 12); undefended losses with an answer in hand and a body at home.

## E. Inside a battle

**Symptom.** The Contender chooses a send-home in battles already won, which lets two or three Personalities survive (PM section 7 row 7), and passes below the threshold with Force still to add (row 1, a Province each time). About half of the losers of military games end the game holding a neutralising card (AB Q4e-end).

**Seed rule** (PM 3.5).

```
at each priority:
  IF the battle as it stands gives the result I want  -> pass
  ELSE the cheapest action that restores it: printed repeatable ability > Stronghold > hand card > Favor
kill, do not repel:  IF I win on Force: pass, or destroy, bow, cut Force; a unit sent home survives
                     send home only to cross a threshold or to save my Province
target:              the unit whose removal changes the result in the fewest actions;
                     then the one they just boosted; then the one with unspent abilities
order:               first the unit that carries their unspent abilities (a bowed unit's abilities are off);
                     Honor and Gold taxes while the target is present; the send-home last
late Force:          add Force only after they have passed or have no unbowed performer; add the minimum;
                     discard for Focus the card with the fewest live uses whose value suffices
lost battle:         IF the result cannot change: spend nothing, unless an action destroys a unit at no cost I need
deciding battle:     IF the game turns on it: spend whatever raises the probability
duels:               IF my stat is higher AND I act first: strike, focus nothing
```

**Should move.** Enemy Personalities destroyed per battle won; battles lost by 0 to 3 Force with late Force unused; cards in hand at a military loss; agreement on PM section 7 rows 1, 7 and 8.

## F. Dynasty: economy against recruiting

**Symptom.** Forcing Personalities first lost 41 games (`bodyfirst`); the `econ` rule ignores the threat (CM section 7 gap 6). Mendon's mix depends on what the opponent has in play, on whether he is pressing, and on the attack he plans (PM 3.6).

**Seed rule.**

```
T = threat(home with the candidate purchase);  O = my best attack next turn with it (family A)
1  IF T loses a Province AND an affordable body makes T hold           -> the cheapest such body
2  ELSE IF a purchase turns O from fails to falls                      -> that purchase
3  ELSE IF posture is press (I attack each turn and destroy what they buy) -> bodies; leftover Gold on Holdings costing 2 or less
4  ELSE IF they have no Personality, or T holds without a purchase:
        IF Holding Gold x expected turns left > its cost               -> Holding
5  ELSE bodies
always: free and 1-Gold Holdings; a Gold-producing Personality before others;
        Proclaim when it keeps my Honor strictly above theirs (the Favor) or my route is Honor
reserve: IF T shows an attack coming AND my hand's answers cost Gold    -> leave that Gold unbowed
Action Phase attachments: buy the one that makes this turn's attack cross its threshold, then stop;
        after they have shown attachment destruction, attach only free cards, at the moment of need
```

**Should move.** Turn of first Personality against an opponent with units in play; Provinces lost on turns 3 to 5; Gold production at turn 6 when not pressing; win rate by deck (no deck collapses).

## G. Posture and route

**Symptom.** The Contender recommended eleven attacks in the game Mendon won by Honor drain without attacking (PM 3.1, G5); it bows the units that run its own win route. No bot code prices the game's clocks against each other (CM section 7).

**Seed rule.**

```
route(side) = the shortest of clock(side, military), clock(side, Honor), clock(side, drain of the other)
lock:   IF my Honor clock is shorter than their fastest clock while my home holds  -> stop attacking unless it risks nothing
drain:  IF my route is drain  -> every repeatable Honor-loss effect each turn; units that run it never attack
punish: IF their Personalities are bowed from attacking  -> attack now with everything the exchange supports
wait:   IF they are on their last Province AND cannot win before my next turn  -> preserve units and cards, finish next turn
race:   IF my military clock is at least one turn shorter than their fastest  -> everything, family C accepts losses
press:  IF my attacks destroy more than they rebuild each turn  -> keep attacking, weak economy accepted (family F)
build:  IF their shown answers beat my units AND T holds  -> economy until I field the unit their answers do not touch
hold:   otherwise
```

**Should move.** Honor wins by decks with an Honor route; games lost with a shorter clock than the opponent's at the turn of the last attack; agreement on PM section 7 rows 9 and 10.

## H. Reading the opponent

**Symptom.** Nothing in the bot changes when the opponent reveals a card: `MatchView.Events` and `ShownCards` are never read, and the opponent's hand is a count times a fixed share (CM section 7 gaps 1 and 5). PM section 4 lists ten reveals that changed Mendon's next decision.

**Seed rule.**

```
on every effect the opponent plays, uses or shows:
   record its class, magnitude, cost and condition; mark each of my units exposed or immune to that condition
   update the share of each class in their hand:
        (prior share x k + copies of the class seen) / (k + cards of theirs seen)
on a pass while losing a battle, with cards in hand and Gold unbowed:  lower the share of answers usable there
on Gold left unbowed through their own Dynasty Phase:                   raise the share of Gold-cost answers
on their Stronghold, clan and the keywords of their Personalities:      set the prior shares
                                                                        (Tactician, Duelist, Cavalry, Courtier, Shugenja)
consequences, through the Read:
   immunity   -> family A lone, and which of my units attack or stay home
   conditions -> family F attachments (fit what removes the condition; avoid what they destroy)
   a new class shown -> replan now (brief section 4.6)
```

**Should move.** Calibration of `exchange` on turns after the first reveal of a class (step 2 gate, split by "class seen before" or not); own Personalities lost to an effect class already shown twice.

## I. The Favor, the opponent's turn and the hand

**Seed rules** (PM 3.7; the Contender chose the opposite in each cited instance).

```
lobby:   IF my Honor is strictly higher: with a unit that neither attacks nor guards, after boosts are done
Favor:   keep it for a defence that needs it (it moves an attacking Personality home, with presence); never cycle a card with it
         spend it in a deciding battle (family E)
their turn: repeatable free disruption every turn (bow a Gold Holding before they buy);
            one-shot cards wait until they have invested in the target
hand:    IF I hold three or more removal or movement cards: decline effects that replace the hand
discard: the card with the fewest live uses against this opponent's shown cards
```

**Should move.** Favor spent on a send-home in a battle already won (should reach zero); Gold the opponent spends per turn against decks with repeatable Holding-bow effects.
