# How Mendon plays: a decision model from eight games

This is the behaviour model the Conan strategy rules are mined from. It is a synthesis of three per-game ledgers (`replays/games-1-2.md`, `replays/games-3-4-7.md`, `replays/games-5-6-tomaru.md`), the planner interview (`../interviews/2026-11-07-planner-interview.md`) and Mendon's own description of his method (2026-11-12, quoted in section 1). Every rule cites the turns that support it; go to the ledgers for the board, the hand and the bot's alternative at each turn.

## Sources and limits

| Tag | Replay | His deck | Opponent (bot) | Result |
| --- | --- | --- | --- | --- |
| G1 | `practice-20261109-133956` | Unicorn Battle Maidens (Cavalry) | Lion | Honor, T12 |
| G2 | `practice-20261109-141352` | Crane | Spider | Honor, T17 |
| G3 | `practice-20261109-172814` | Lion Tacticians | Spider | Military, T11 |
| G4 | `practice-20261109-184403` | Phoenix Shugenja | Crab | Military, T11 |
| G5 | `practice-20261109-195056` | Crab Courtier control | Lion | Opponent dishonored, T29 |
| G6 | `practice-20261109-201243` | Lion (Akodo), Shamate Keep | Crane duelists | Military, T13 |
| G7 | `practice-20261110-173816` | Lion Tacticians | Dragon | Military, T11 |
| GT | `tomaru` | Lion | Mantis | Military, T7 |

Read the model with these limits in mind:

- **All eight are wins against the Champion or the Contender.** The model shows what beats those bots. Risks he accepted and was not punished for (an empty home on six attack turns) are unpunished against a weak counter-attacker, not proven sound against a strong one.
- **The logs record actions, not reasons.** A reason is stated here only where a public fact explains the choice; "not shown" means exactly that.
- **Only public information explains a choice.** The logs are omniscient; the model never uses the opponent's hidden hand.
- **Cavalry is almost absent**: one Cavalry-segment assignment in eight games (G1 T6). Section 4 lists what must come from the rules and from a screenshot interview instead.
- **He makes mistakes.** Section 6 lists them. Rules are mined from his repeated choices, never from a single turn that the ledger marks as an error.

Evidence grades: **strong** = three or more instances, **moderate** = two, **single** = one. "One game" marks a rule whose instances all come from one matchup.

## 1. The loop he runs

His words, 2026-11-12: "These are all tasks that I'm performing while I play the game based on how information changes. Because a player has played cards from their hands or has brought cards from the provinces out or has attacked me, anything that the opponent does feeds my internal model of what I can do next." And from the interview: a plan after the Events Phase, revisited after the Action Phase, after each battle and after the Attack Phase; "If I think I can win the battle, I attack"; "If I cannot produce a win under any circumstance, I let the province fall"; "If I hold 4 battle actions, I'm likely not worried".

The plan answers four questions, and every reveal reopens them:

1. **Can I neutralise their defenders?** By sending them home, destroying them, bowing them or cutting their Force, so that what remains cannot stop a Province falling.
2. **How well can they counter that?** From their unbowed units, unspent printed abilities, Gold, hand size and everything they have already shown.
3. **What happens on their turn?** Can I keep my Provinces; better, lose no defenders; best, destroy the counter-attackers. This is a matter of sending attackers home or otherwise neutralising them, not only of Force.
4. **So what do I do with this turn's Gold and units?** Attack or hold, how many units, and the balance of economy against Personalities.

## 2. What he reads

| Input | Where it shows in his play |
| --- | --- |
| Their unbowed Personalities, and which are bowed from attacking | Attack timing (G3 T9, T11; G7 T11); waits for the opponent to bow its own (G4 T7, T9, T11) |
| The **kind** of answer they have shown, and its condition | Duels on Chi, so one high-Chi unit attacks alone (G6 T9, T11); "target without attachments", so only units with attachments attack (G7 T9); attachment destruction, so only free Spells (G4 T7 on) |
| Their unspent printed abilities and Stronghold ability | Bows the unit that carries the abilities first (G6 T6) |
| Their hand size and Gold | Sizes a defence; "If I hold 4 battle actions, I'm likely not worried" (interview) |
| Who holds the Favor | Attacked into a public Favor with one self-sufficient unit only (G1 T6) |
| Province count each side, and whose attackers will be bowed next turn | Concedes a Province while ahead (G3 T8, T10; G7 T10; GT T6) |
| Honor each side and the route each deck is on | No attacks at all in the drain game (G5); stops attacking once the Honor win is certain (G1, G2) |
| His own neutralisers, counted against the defenders he must remove | Every attack into unbowed defenders (section 3.1) |
| Force he can add after assignment | Attacks that are below Province Strength at declaration (G3 T3, T9; G7 T3; G4 T11; GT T5) |

## 3. The rules

### 3.1 Attack or hold

```
IF my win route is not military (Honor gain, Honor drain)
    attack only when it costs nothing I need            # G5: 0 attacks in 14 turns, 27F against one unit;
                                                        # G1+G2: 3 attacks in two games
    IF my Honor win is certain before their clock runs out: stop attacking
ELSE IF no group of my unbowed units can exceed Province Strength even undefended,
        counting Force I can add after assignment
    hold                                                # G4 T5 (6F v 7), G5 T8
ELSE IF they have no unbowed Personality
    attack, with one unit if that is all I have,
    whatever stays home                                 # G3 T3, T11; G7 T3, T11; G4 T7, T11; G6 T13
ELSE                                                    # unbowed defenders exist
    need = defenders I must neutralise so that my Force > the rest + Province Strength
    have = my neutralisers usable in that battle: unspent printed abilities, Stronghold,
           hand cards I can pay for and have a legal performer for
    IF have >= need                                     # he usually held 1.5 to 4 times need
        attack with the smallest group that suffices    # G3 T5, G7 T5, G7 T7, G6 T9, G6 T11
        a failed attack that loses no unit is acceptable    # G3 T5
    ELSE IF their army will be bowed after attacking me
        wait and punish next turn                       # G3 T9, T11; G7 T11
    ELSE
        hold                                            # G1 T8 (all defenders unbowed, no route)
```

Grade: strong across five games; one costly failure (G3 T7, two removers plus recovery cards against three defenders).

Refinements:

- **Immunity beats numbers** (moderate, G6 T9 and T11; single, G7 T9). IF every answer they have shown is conditional (a duel on a stat, a Force comparison, "a target without attachments") AND one of my units fails the condition for all of them, THEN that unit attacks alone and the rest stay home. In G6 he attacked 11F into 13 and 13F into 13 and spent no hand card: Crane's duels bowed its own defenders.
- **After a new kind of answer beats me, stop attacking that army while it is unbowed** (single game, G3: beaten by Force cuts on T5 and negated movement on T7, then attacked only the bowed army on T9 and T11).
- **Let the opponent bow itself first** (strong, one opponent: G4 T7, T9, T11). IF they have abilities that bow their own Personalities, pass in the Action Phase until they stop, then declare.

### 3.2 The shape of the attack

```
IF their unbowed defenders are fewer than the Provinces I can threaten
    one group per Province, each reaching Province Strength with a finisher in hand,
    margins balanced across groups                      # G3 T9; G4 T9, T11; G7 T7; G6 T13; GT T5, T7
ELSE IF one group exceeds all their unbowed defenders plus Province Strength without cards
    send that group only; the rest stay home unbowed    # G7 T9 (14 v 13); G3 T9, G4 T9 kept one home
ELSE
    the smallest group my neutralisers can carry        # 3.1
never add a weak or dishonored Personality to a contested battle:
    it is a target, not security                        # G6 T11; G5 T13, T25
the last turn of the game is the exception: send everything    # G3 T11, G7 T11
```

Grade: strong. The Contender stacked one Province on nearly every one of these turns.

Which Province: the one whose face-up card or marker makes it worth most (the Province an opposing Event had marked to grow, GT T5; a Province holding a dangerous face-up Personality, interview and G1 T6). Otherwise not shown.

### 3.3 Home and the counter-attack

What the games show is a price, not a gate. A Province is a resource he spends while he is ahead.

```
IF I am ahead or level on Provinces AND I will still hold 2 or more
    an empty home is acceptable when the attack is worth it     # G3 T5, T7; G7 T5, T7; GT T5; G6 T9
    IF they then attack and holding would cost units or cards I need
        concede the Province, keep the units and the hand       # G3 T8, T10; G7 T10; GT T6
    IF they are on their last Province and their attackers will be bowed on my turn
        never defend; kill next turn with everything            # G3 T10, G7 T10
WHEN units do stay home
    make them count: a bow effect on one of them, the lobby done by a unit that stays,
    a send-home card kept in hand                               # G6 T11, T13; G3 T9
```

Grade: strong for conceding (four instances, three games). **Untested at two Provinces or fewer and against an opponent that punishes an empty home.** His stated intent for Conan (section 1, question 3) is stricter than his play against these bots; build the stricter evaluation and let the Province count and the clock decide how much it weighs.

### 3.4 Defence

```
IF no line holds the Province, OR holding costs more units and cards than the Province is worth (3.3)
    assign nobody                                       # G3 T8, T10; G7 T10; GT T6
ELSE IF my Force, after my neutralisers, beats theirs
    defend to kill: every unit with 2F or more goes to that battlefield,
    0 to 1F utility Personalities stay home             # G1 T5, T9; G2 T10 (3 bodies), T12 (6 bodies); G4 T6
ELSE IF the defence is won by actions, not Force, AND they hold destroy or duel effects
    the fewest bodies that give presence and survive being targeted;
    perform from home what can be performed from home   # G5 T13, T25 (2 bodies; the bot wanted 7 and 15)
IF the Province is already safe on Strength AND a lone attacker can be bowed or shrunk
    defend with one body to destroy it                  # G6 T6, G5 T25
```

Grade: strong (defend to kill, five instances; minimum bodies, three). The Contender's single weak "presence" defender lost a Province (G2 T12) and forfeited 12 Honor (G1 T9); its seven and fifteen defenders in G5 only added targets.

### 3.5 Inside a battle

- **Pass while ahead; answer each of their actions with the smallest effect that restores the result I want; finish with cards unused** (strong: G1 T5, G2 throughout, G6 T9, G7 T7).
- **Late Force.** Force I can add after assignment (a Tactician's discard, an army bonus, a created unit) counts when I decide to attack, is added only after the defender has passed or committed, and is the minimum that crosses the threshold (strong: G3 T3, T9; G7 T3; G4 T11). The Contender passed at 4 v 7 and 7 v 7 on those turns. The card discarded for Force is the one with least use in this matchup whose Focus Value just suffices.
- **Kill, do not repel.** IF the battle is won on Force THEN pass: a unit sent home survives, a unit left in a losing army dies and pays 2 Honor per card. Send-home is for crossing a threshold or saving a Province when Force cannot (strong: G1 T9, G7 T7 both battles, G3 T5, G6 T11, GT T7). The Contender chose the send-home each time.
- **Order.** First the effect that disables the unit carrying their abilities (bowing a unit turns its abilities off), or the effect that needs my own performer unbowed; Honor and Gold taxes while the target is still present; the send-home last (strong: G6 T6, GT T7, G5 T13).
- **Target** the largest eligible unit; when they boost one, that one (strong: G7 T5; G3 T5, T7, T9). From the interview: prefer units with unspent abilities, and Tacticians.
- **Printed and repeatable abilities before hand cards; the Favor last** (strong: G6 T9, T11; G5 T25).
- **Duels.** IF my stat is higher AND I choose first THEN strike at once, focusing nothing (strong, one game: G6 three of three; it also denied a "+4 instead of focusing" option).

### 3.6 Economy against recruiting

```
turn 1:  IF a Personality costing 3 or less AND a Holding are both affordable: buy both   # G3, G7
         ELSE Holdings                                                                   # G1, G4, G6
WHILE they have no Personality AND my Gold cannot buy a Personality plus a Holding
    Holdings                                            # G6 T1 to T3, G4 T1, G1 T2
ONCE they have a Personality, OR my Gold buys a Personality and a cheap Holding
    Personalities first; leftover Gold on free and 2-Gold-or-less Holdings     # every Dynasty in G3, G7; G4 T3 on
ON a turn I attack
    Gold goes first, in the Action Phase, to the attachment that makes this attack reach its threshold,
    stops when the attack is sufficient,
    then to Personalities in Dynasty: they are the unbowed defenders of the next turn   # G6 T9, T11; GT T5; G7 T3 to T9
IF I am pressing (attacking every turn and destroying what they buy)
    a weak economy is acceptable                        # G7: production 8 against 17, no Holding after T1
IF I cannot press because their answers beat my units
    build until I can field the unit their answers do not touch     # G6: 9 Holdings and 1 Personality to T8
ONCE production covers the row (about 18 Gold) or I have taken a Province
    Personalities only; discard Holdings from the row   # G6 T9, T11 (military decks)
which Personality: one that produces Gold first; Proclaim the highest Personal Honor at full price,
    take the discount on the others; buy the whole row when Gold covers it    # G1, G2
reserve: IF my hand holds Gold-cost actions for their turn or for this battle,
    leave that much production unbowed                  # G5 T8, T11, T13; G1 T6 (kept 11 Gold)
```

Grade: strong. From the interview: "As long as I was able to press the attack and kept coming home with an advantage in personalities and force ... I was happy for a weak economy ... if I hadn't had the early attack advantage, I'd have dug for holdings." Forcing Personalities first unconditionally lost 41 games in the arena (`bodyfirst`); the conditions are the rule.

### 3.7 The Favor, the opponent's turn and the hand

- **Lobby** when my Honor is higher, with a unit that is not attacking and has no better use (a token, the cheapest body), after boosts are done; never with a unit the attack or the home defence needs (strong: G3 T9, G7 T7, G6 T11; G1, G2 every turn). Counter-example: G4, never lobbied.
- **Hold the Favor.** It is spent on defence when level or behind and never to cycle a card (strong: four declined cycles; unspent in G3, G6, G7).
- **Their turn.** Use free repeatable disruption every turn: bow one of their Gold Holdings (G5, eleven turns), the Stronghold's bow-or-Honor ability (G2). Hold one-shot cards until they have invested in a unit worth the card (G1 T5).
- **Honor drain route** (strong, one game: G5). Every repeatable Honor-loss effect every turn, on targets where it will resolve; my own Honor is spendable while far from -20.
- **Hand.** IF I hold three or more removal or movement cards THEN decline effects that replace the hand and do not pay to cycle (strong: G3 T5, G7 T7, G4 T3). At the hand limit discard the card with the fewest live uses in this matchup, keeping one-shot cards that scale with their investment (G5 T12 to T24; G6 T5).

## 4. Reveals that changed his play

| He saw | He then |
| --- | --- |
| Crane load one unit with duel abilities (G6 T6) | Bowed that unit first, which turned all of them off |
| A duel Stronghold and Chi 4 duelists (G6 T8) | Fitted the +1 Chi Item first, attacked alone with Chi 6, struck at once in three duels |
| Crane dishonor one of his Personalities (G6 T10) | Kept it home until Crane had no Personalities |
| Dragon's effects need "a target without attachments" (G7 T5, T7) | Paid 3 Gold for a Follower and attacked only with units carrying attachments (T9) |
| Crab destroy a paid Spell (G4 T6) | Attached only 0 to 1 Gold Spells, at the moment of need |
| Spider's Force cuts and negated movement (G3 T5, T7) | Attacked only when Spider's army was bowed, and split (T9, T11) |
| Lion reduce a Province to Strength 0 (G5 T7) | Began denying Lion's Gold each turn and bought two defensive Personalities |
| An opposing Event mark a Province to grow (GT T5) | Attacked that Province that turn |
| A negation shield on a target (G5 T10) | Moved the effect to a fresh target (and failed to on T12, section 6) |
| The first enemy Personality enter play (G6 T4, G3 T4) | Bought his first Personality the next turn (confounded with affordability in G6) |

## 5. Open questions: what the games cannot answer

Each needs a screenshot interview (real positions, multiple choice plus free text) before a rule is treated as his.

1. **Cavalry forcing.** One instance (G1 T6): he passed the infantry step, watched Lion assign no infantry defender, then sent one self-sufficient unit into a publicly held Favor and kept the rest home. His description (2026-11-12): a deck with all or mostly Cavalry "should consider whether it can force its opponent to defend in such a way that it can destroy one or more provinces easily". The mechanics are in `RULES_DIGEST.md` section 2; the sizing is not in the games.
2. **The counter-attack at two Provinces or fewer**, and against an opponent that attacks an empty home.
3. **Which Province** to attack when nothing marks one.
4. **Defenders assigned to an unattacked Province** (G3 T10, G7 T10): the same effect as not defending; the reason is not shown.
5. **How many spare neutralisers** he wants per defender when their hand is large and their Gold is unspent.

## 6. His mistakes: do not copy

- G6 T6: a 0 v 0 tie traded a 7-Gold Personality when an in-play Melee 4 would have saved her.
- G6 T9: forgot the reaction that keeps the attacker unbowed.
- G7 T5: passed at 10 v 3 + 7 holding three cards that each took the Province.
- G4 T9: "pass all battles" forfeited a Province with a +5 ability unused.
- G3 T7: three return-to-battle cards on a Personality whose movement was negated; a winning send-home not played at 14 v 15.
- G5 T12: aimed a dishonor into a public negation shield. G5 T6: copied an ability he could not use.
- G1 T8: overpaid 4 Gold. G2 T6: Stronghold ability unused. G2 T16: first pass at 9 v 3.
- G2 T5: stacked his only unit and lost it to a destroy effect he had not yet seen (the lesson is in section 4, not a rule against attacking).
- G6 T5, T6: declined optional draws at eight cards; unexplained.

## 7. Where the Contender's choice cost most

These are the first decision positions for the test set (`../CONAN_STRATEGY_BRIEF.md`, step 1). His move first, the Contender's second.

| Position | His move | The Contender's move | Cost of the bot's move |
| --- | --- | --- | --- |
| G3 T3, G3 T9, G7 T3, G4 T11 battle | Add late Force after the defender passed | Pass at 4 v 7 or 7 v 7 | A Province each time |
| G3 T9, G4 T9, G6 T13, GT T5 assignment | One group per Province | Everyone at one Province | A Province, or a turn |
| G6 T11 assignment | The immune unit alone | Three attackers at two Provinces, a dishonored 3F unit alone against Strength 6 | Units, and only bowed units at home |
| G2 T10, T12; G1 T9 defence | Every unit with 2F or more | One weak body | A Province (G2 T12); 12 Honor (G1 T9) |
| G5 T13, T25 defence | Two bodies that punish targeting | Seven and fifteen bodies | Targets for destroy effects |
| G3 T10, G7 T10 defence | Concede, kill next turn | Defend 8F against 9F, 6F against 14F | Defenders die unless a send-home follows |
| G7 T7, G6 T11, G1 T9, GT T7 battle | Pass or kill in a won battle | Send the unit home (often with the Favor) | Two or three Personalities survive; the Favor is gone |
| G6 T9, T11 duels | Strike at once | Focus a card | A card, and an opening for their focus |
| G4 T5 declaration | Hold at 6F v 7 | Attack | Bows the unit holding the defensive Spell |
| G5, eleven declarations | Hold | Attack | Bows the Courtiers that run the drain and the defence |
| G3 T5, G7 T7 Event | Keep the hand | "Discard hand, draw four" | The cards that won the following battles |
| G1 T4, T6; G2 T1 Dynasty | Personality, then Holdings | Holding first; refresh the cheap Personality away | A body a turn |

Where the Contender was right and he was not: section 6.
