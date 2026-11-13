# Rules digest for bot decision rules (L5R Emperor Edition)

Sources, all under `docs/rules/`, all read in full on 2026-11-12:
- **CR** = `COMPREHENSIVE_RULES.md`. Citations give the section heading and line, e.g. (CR Resolution Segment, L2197).
- **R-nnn** = a confirmed project ruling in `RULINGS.md`.
- **BPG** = `BOT_PHASE_GUIDANCE.md`.

Markers: **[SILENT]** = the documents do not state it. **[INFERENCE]** = follows from the cited lines but is not stated in one place. CR Part 4 (card rulings, L4022 onward) is Celestial Edition material that "must be checked" before applying to an EE card (CR header, L9); it is used here only for examples.

**A common misreading:** a tie does not "destroy nothing". A tie destroys both armies and no Province (section 3).

## 1. Turn structure and what each phase permits

- A turn is Straighten, Events, Action, Attack (optional), Dynasty, End (CR The turn sequence, L1970-2088; Start of game, L1966).
- **Straighten:** "The active player simultaneously straightens all his or her cards in play" (CR Straighten Phase, L1974). Only the active player's cards straighten. A card that bows on the opponent's turn stays bowed until its controller's own Straighten Phase [INFERENCE from L1974].
- **Events:** the active player turns each card in his Provinces face-up, leftmost Province first. A Holding or Personality just stays there; an Event resolves; a Region attaches; a Celestial enters play (CR Events Phase, L1978-1993).
- **Action:** players alternate opportunities, active player first, until all pass consecutively. The active player may take Limited and Open actions; every other player may take only Open actions (CR Action Phase, L1997-1999).
- **Attack:** optional. Segments in order: Declaration, Infantry Maneuvers, Cavalry Maneuvers, Battles (CR Attack Phase, L2005-2009).
- **Dynasty:** the active player may bring face-up Holdings and Personalities from his Provinces into play, one at a time; then he may discard face-up Province cards; after any such discard he cannot bring more cards into play from Provinces that phase (CR Dynasty Phase, L2069, L2073).
- **End:** the active player draws one Fate card (not optional), then discards down to maximum hand size, normally 8, and may not discard more than necessary (CR End Phase, L2081, L2086; Maximum hand size, L91-95). No other rulebook draw exists in the turn [INFERENCE: none is listed].

Timing of action types:
- Limited: an opportunity in the player's own Action Phase. Open: an opportunity in any player's Action Phase. Battle: an opportunity in the Combat Segment. Reaction: at its stated trigger. Interrupt: "Before an action's effects resolve". Engage: "After engaging" (CR Designators, L1780-1785).
- An Open action is not usable in a battle unless an effect grants it (R-030). A card-created battle in the Action Phase also forbids Limited actions (CR Battles, L2110-2111).
- A "Battle/Open" ability may be used at either time (CR Abilities, L718). The engine models this as `BattleOrOpen`; there is no "Battle/Limited" timing (BPG L8).
- An ability is usable once per turn unless it states otherwise; each copy on a separate card is a separate ability (CR Abilities, L700-705; Designators, L1796). Repeatable abilities have no per-turn limit (CR Repeatable, L3602). A once-per-turn Battle/Open use spent in the Action Phase is gone for the battle (BPG L8).
- Abilities on a bowed card cannot be used, but an unbowed attachment on a bowed Personality can still use its abilities (CR Bowed and unbowed, L1066; R-080). A Tireless ability works while its card is bowed (CR Tireless, L3836).

Holdings and Gold timing:
- "Holdings enter play bowed by default no matter how they enter play" (CR Holdings, L251). A Holding bought in the Dynasty Phase therefore first produces Gold after its controller's next Straighten Phase, unless an effect straightens it [INFERENCE from L251 and L1974].
- Start of game: each player has an unbowed Border Keep; each player who is not going first also has Bamboo Harvesters in play, bowed (CR Start of game, L1954, L1958). The CE-era card text says Bamboo Harvesters "will not straighten before your second turn" and produces 2 Gold (CR Part 4, Bamboo Harvesters, L4135).

Provinces:
- A player starts with four Provinces, each filled face-down (CR Provinces, L880). When a card leaves a Province, triggered effects resolve, then the Province is refilled with a face-down card from the top of the Dynasty deck (CR Provinces, L884).
- A face-down card is turned face-up in its owner's next Events Phase (CR Events Phase, L1982). So a Province emptied by a purchase offers nothing more that turn unless an effect refills it face-up [INFERENCE].

Victory and defeat checks:
- **Honor:** checked "at the point when a player's turn begins": 40 or more Family Honor then wins (CR Victory, L139). Reaching 40 during a turn does not win at once.
- **Military:** a player loses immediately when his last Province is destroyed, by battle resolution or by any other means (CR Victory, L143-145).
- **Dishonor:** if Family Honor "ever becomes -20 or below", the player loses at the beginning of his next End Phase "even if he or she returns to -19 or higher" (CR Victory, L149).
- Enlightenment and card-defined victories also exist (CR Victory, L155-159).
- **Deck-out:** there is none. "Nothing special happens if either deck contains no cards" (CR Fate and Dynasty decks, L874). An empty Dynasty deck leaves Provinces empty (CR Provinces, L890).

## 2. Declaring an attack

- The active player declares the attack and chooses the Defender. A battlefield is created at each of the Defender's Provinces (CR Declaration Segment, L2017, L2025).
- **Infantry Maneuvers:** step A, the Attacker chooses "any number of his units whose Personality is unbowed" from his home and assigns them "to any battlefields in any arrangement desired". Step B, the Defender does the same with his units (CR Infantry Maneuvers, L2035, L2039). Cavalry units may also assign in this segment (L2029).
- **Cavalry Maneuvers:** step A, the Attacker assigns Cavalry units; step B, the Defender assigns Cavalry units (CR Cavalry Maneuvers, L2043-2051). Card effects that allow assignment in this segment are not limited to Cavalry (L2043).
- The full order is therefore: attacker's units, defender's units, attacker's Cavalry, defender's Cavalry. The Defender's Cavalry assigns last, with every other assignment visible.
- **Knowledge:** how a player assigns units is public, as are all face-up cards, bowed states and stat totals (CR Game knowledge, L1009-1011). Hands are hidden; hand sizes are public (L1012, L1018).
- The Maneuvers segments list assignments only. They list no opportunity for Limited, Open or Battle actions [SILENT on any action window; Reactions fire at their own triggers].
- **Who can be assigned:** only a unit at home whose Personality is unbowed. A bowed Personality cannot assign, either by the normal procedure or through effects that assign him (CR Bowed and unbowed, L1066; Assign, L2611). The test is on the Personality; bowed Followers do not block assignment [INFERENCE from L2035].
- **Several Provinces:** different units may go to different battlefields ("any arrangement"). One unit has one location (CR Locations, L1098), so one unit goes to one battlefield.
- **Cavalry:** "A unit is Cavalry if its Personality and all its Followers are Cavalry. Otherwise, it is Infantry" (CR Cavalry, L2679). A Cavalry Personality with one non-Cavalry Follower is an Infantry unit and must assign in Infantry Maneuvers or not at all. Items are not part of the test (L2679 names only the Personality and Followers). Cavalry forbids nothing; it only adds the later assignment window (L2681).
- **Battle order:** the Attacker chooses the first battlefield and, after each battle, the next (CR Battles, L2055-2059; Choose Battlefield, L2119).
- **Second-Battle Rule:** a unit that was in an attacking army during a battle's resolution cannot move or assign to another battlefield that Attack Phase (CR Second-Battle Rule, L2165). Defenders are not covered by this rule.
- **One battlefield affecting another:** a card in a unit must be at the current battlefield to be targeted by, or to perform, an Engage, Battle or Interrupt action; an ability printed on a card in a unit needs that unit at the current battlefield (CR Rule of Location, L2150-2153). Exceptions: a Home ability, and an action that moves that unit to the current battlefield (L2155-2157). Units at home or at another battlefield are therefore out of reach of Battle actions unless card text says otherwise. Reactions are exempt (CR Combat Segment, L2133; R-092). Card effects can move units between battlefields (CR Locations, L1110).
- **Keywords at assignment:**
  - Brash: after Brash cards are assigned to attack, the Defender may draw one card per Brash card (CR Brash, L2667).
  - Naval: gives no assignment effect. After engaging, once per battle, an Attacker whose current army has more Naval cards than the opposing side may take the first Battle action, which the chosen Naval Personality must perform (CR Player abilities, L126).
  - Conqueror: matters only after resolution (section 3).
  - Scout: a keyword only. No rule gives it an effect. Reconnaissance "grants no benefits by itself" (CR Reconnaissance, L3559).

## 3. Fighting a battle

- **Engage round:** only Engage actions and "After Engaging" Reactions; the active player acts first (CR Combat Segment, L2125). An Engage must come from a card at that battlefield (R-064).
- **Battle action round:** only Battle actions, plus Reactions and Interrupts they trigger. "The Defender gets the first opportunity either to take a Battle action or pass." The segment ends "after all players pass in sequence" (CR Combat Segment, L2127). The text does not say a pass forfeits later opportunities; only consecutive passes end the round.
- **Rule of Presence:** "Engage, Battle and Interrupt actions can only be taken by a player who controls a unit at the current battlefield" (CR Rule of Presence, L2137). This binds the player, so it covers Strategies from hand, Holdings, the Stronghold and player abilities. Only a Personality gives presence; a Region does not (R-065).
- **Presence exceptions:** Absent actions (L2139); a Battle action whose costs or effects say they can move, create or bring into play one of his units there (L2141), legal even if the movement is known to fail (L2143-2144); text reading "Even if you control no units at the current battlefield" (L2146).
- **Reactions** need no presence unless their own text requires it; Interrupts do need it (CR L2133; R-092, which reverses R-090).
- **Unopposed:** a card or army is opposed if the opposing side has one or more units (CR Opposed, L3416). "Unopposed" is simply not opposed (CR Un-, L3869). The rules attach no restriction to being unopposed. The practical effect comes from Presence: the side with no unit there cannot take Battle actions. Rulebook Battle Equip needs an "opposed" Shugenja (CR Player abilities, L106). BPG L36 names an engine flag `UseUnopposedGate` but does not describe it.
- **Army total Force:** "the sum of the modified Force of all unbowed Personalities and all unbowed Followers in the army" (CR Total Force, L3846).
  - A bowed Personality contributes nothing. An unbowed Follower attached to a bowed Personality still counts; a bowed Follower on an unbowed Personality does not [INFERENCE: L3846 counts each card on its own state].
  - Items add to their Personality's Force and are not counted separately (CR Units, L431; Items, L450). So Items on a bowed Personality contribute nothing [INFERENCE]. Whether a bowed Item stops modifying its Personality is [SILENT].
  - An Elite card contributes Force even if bowed (CR Elite, L2887).
  - A *unit's* total Force is different: it counts the Personality and all Followers, bowed or not (CR Total Force, L3844; R-084). Card text that compares "unit total Force" uses this number, not the battle contribution.
- **Outcome:** the army with the highest total Force wins. A side with no units counts as zero. The comparison is made once; later changes do not alter the outcome (CR Resolution Segment, L2177-2181).
- **Attacker wins:** he destroys all cards in the defending army. He destroys the Province only "if the attacking army's total Force ... was greater than the defending army's total Force plus the Province Strength". He gains Honor equal to twice the number of defending cards destroyed by resolution (CR Resolution Segment, L2195-2199). Province Strength starts from the Stronghold's value plus modifiers (CR Base Province Strength, L221).
- **Defender wins:** he destroys all cards in the attacking army and gains Honor equal to twice the number destroyed (L2204-2206).
- "All cards in the army" includes bowed cards and every attachment; each destroyed card counts for Honor [INFERENCE from L2195, L2199].
- **Tie** (same total Force and both armies contain a unit): each side rehonors its own dishonorable Personalities, then each side destroys all cards in the enemy army. No Province is destroyed. Each side gains Honor equal to the number (not twice) of cards it destroyed, but only if none of its own Personalities was dishonorable (L2187, L2210-2216). No project ruling changes this.
- **No defined outcome:** an army with total Force zero against a side with no units. Nothing is destroyed (L2189).
- **Dishonorable Personality in a winning army:** the battle Honor gain is replaced by rehonoring him (CR Honorable and dishonorable, L1088; Resolution, L2200).
- **After resolution:** "all attacking and allied units at its battlefield bow simultaneously, then return home", whoever won (CR Final Steps, L2230). Surviving defenders stay at the battlefield, and go home if the Province was destroyed (L2232). No rule bows defenders. When battlefields cease to exist, only attacking and allied units bow (CR Battlefields, general, L2244).
- **Conquest:** after resolution ends, an attacking Conqueror's unit may be straightened and will not bow from that resolution (CR Player abilities, L122). Once per turn across all battlefields, and offered only when the attacker wins; the latter is engine behaviour kept by ruling, not CR text (R-091).

## 4. Ways to neutralise a unit, and exact consequences

Common rules first:
- A Battle action needs Presence and a target at the current battlefield (section 3). The same effect on an Open or Limited ability, used in the Action Phase, needs neither and can reach units at home.
- Effects last until end of turn unless printed as permanent (R-001).
- If an effect fails (for example, bowing an already bowed card), the other effects still happen (CR Action resolution, L1896). If required targeting fails or a cost is interfered with, the ability still counts as used and a Strategy is still discarded (CR L1871, L1883).

**Send home (move home).**
- The unit leaves the army. Targeting or moving a unit affects the Personality; his attachments go with him (CR Units, L434; Unit, L3897).
- It is not in the army at resolution, so it adds no Force and is not destroyed with a losing or tied army [INFERENCE from L2195, L2212].
- An attacker moved home is not "at its battlefield" in Final Steps, so the attack bow (L2230) does not reach it [INFERENCE]. It keeps its current bowed or unbowed state. If unbowed, it is still unbowed on the opponent's turn.
- It can return only by a card effect; the Second-Battle Rule blocks that only for a unit that was in an attacking army during a resolution (L2165).
- If it was the player's only unit there, that player loses Presence and the side counts as zero Force (L2137, L2179).

**Bowing.**
- "Bow a unit" bows each card in it separately (CR Unit, L3895; R-017). Bowing only the Personality leaves his unbowed Followers counting (L3846).
- A bowed card adds no Force to the army (L3846) and its own abilities cannot be used (L1066). Unbowed attachments on a bowed Personality still work (R-080). Card text naming a performer without "unbowed" may be performed by a bowed Personality (R-035).
- The unit stays in the army. It still gives Presence, since L2137 requires only "a unit" [INFERENCE]. It is still destroyed if its army loses or ties (L2195, L2204, L2212).
- A bowed Personality cannot assign (L1066, L2611). Bowing a Personality in the Action Phase, before Maneuvers, removes him from that attack or defence.
- It straightens only in its controller's next Straighten Phase (L1974).
- Stalwart negates the first bowing each turn from other players' card effects (CR Stalwart, L3716).

**Destroying.**
- The card goes to its discard pile; a destroyed Personality's attachments are destroyed too (CR Destroy, L2803; Playing attachments, L421).
- If the Personality was dishonorable, his controller loses Honor equal to his base Personal Honor; this is a rulebook effect that "you do not lose Honor from cards you own" does not stop (CR L1080; R-083).
- Destruction by an action gives no Honor by rule; only cards "destroyed by the effects of resolution" pay battle Honor (L2199).
- Chi zero destroys a Personality immediately (CR Chi Death Rule, L291). Force zero does not.

**Reducing Force.**
- A stat cannot go below 0 (CR Calculating stats, L532). A penalty on a bowed card changes nothing in the army total (L3846).
- The unit keeps all abilities, still gives Presence and still dies with a losing or tied army. A Force change "matters only in a battle" (BPG L17).

**Dishonoring.**
- Only Personalities can be dishonorable (CR L1072). Maximum Personal Honor becomes 0 (L1078).
- The documents give dishonor no effect on Force, bowing, assignment or abilities. He fights at full value.
- Consequences: Honor loss equal to base Personal Honor when he is destroyed (L1080); Honor his controller would gain from an action or trait that he performed, that targeted him or came from him is replaced by rehonoring him (L1084); his presence in a winning army replaces the battle Honor (L1088); he cannot Lobby, which needs Personal Honor 1 or higher (CR Player abilities, L110); a new Follower needs his Personal Honor to meet its Honor Requirement (CR Followers, L444), though attachments already on him stay (L419).
- His controller may Order Seppuku on a dishonorable Courtier, Samurai or Shugenja (Repeatable Open, so Action Phase only): he is rehonored, then destroyed, and this cannot be negated (CR Player abilities, L119; Seppuku, L3686).

**Negating an action.**
- Prevention is applied per effect; preventing one effect does not prevent the others, and it is never retroactive (CR Prevention, L1696, L1729).
- The documents contain no rule refunding costs of a negated action. For failed actions the ability is "considered used" (L1871, L1883). Whether a fully negated action differs is [SILENT]; treat the ability as used.
- The unit is otherwise untouched and may use other abilities.

**Ranged Attack.**
- "Ranged X Attack" means: "You may target a Follower or a Personality without Followers in the current enemy army. If its Force is equal to or less than X, destroy it" (CR Ranged Attack, L3533).
- A Personality who has any Follower cannot be targeted. The Force compared is that one card's Force. Bowed state is not part of the test.
- The target must be in the enemy army at the current battlefield. The action may be announced with no legal target (L3541).
- Several simultaneous Ranged Attacks cannot kill a Follower and then its Personality; consecutive ones can (L3545-3547).

**Melee Attack.** The only general statement is "Melee Attacks work like Ranged Attacks but under a different name" (CR Part 3, L3999). There is no glossary entry. R-045 and R-052 concern single cards. Anything beyond the Ranged Attack definition is [SILENT].

**Duel.**
- Each side's score is its duel stat (Chi by default) plus the total Focus Value of its focused cards (CR Duels, L2312-2313).
- The challenged player focuses or strikes first; players alternate; each may focus at most four cards, face-down, from hand or from the top of his deck; there is no pass (L2290-2296).
- Higher score wins. A tie means both lose, except that a Duelist beats a non-Duelist on a tie (L2315; CR Duelist, L2865).
- "There is no default consequence of winning or losing a duel" (L2330). The loser suffers only what the card creating the duel says.
- Challenges cannot be refused unless the effect allows it (L2274). Focused cards are discarded afterwards (L2338). The duel ends without resolution if either Personality leaves play (L2268).

**Fear.** [SILENT] as a rule: there is no rule or glossary entry. "Fear" appears only as a keyword on individual abilities whose effects differ, e.g. "Bow all Followers with 4 or lower Force in a target enemy unit" (CR Part 4, Omoni, L5156) and "Move a target enemy unit with lower total Force ... home" (Matsu Fumiyo, L5044). Read each card.

## 5. Tacticians, Focus Values and other hand-to-Force conversions

- Tactical Advantage: "Repeatable Tactical Battle: Discard a card and choose your performing Tactician Personality: Give him a Force bonus equal to the Focus Value of the discarded card" (CR Player abilities, L116).
- "Each Tactician can only perform one Tactical action per turn" (CR Tactical, L3773). So each Tactician gets at most one such bonus per turn (L117), and any other Tactical action he performs uses that same allowance. Several Tacticians can each take one.
- It is a Battle action: Combat Segment only, the player needs Presence, and the Tactician must be at the current battlefield (L1782, L2137, L2150).
- All Fate cards have a Focus Value (CR Fate card types, L370). The bonus lasts until end of turn (R-001).
- A bonus on a bowed Tactician adds nothing to the army total (L3846). Whether a bowed Tactician may perform the action is [SILENT]: the text has no "unbowed", and R-035 states the matching principle for cards, not for this player ability.
- Focus Values also decide duels (section 4). A Focus Effect is not an action (R-013).
- Battle Equip: an unbowed opposed Shugenja may attach a Spell from hand, paying its costs, and the player may take an additional action to use a Battle ability on that Spell (CR Player abilities, L106; any element, R-036).
- No other general hand-to-Force rule exists in the documents.

## 6. The Imperial Favor

- One Favor exists; it starts uncontrolled; only one player controls it (CR The Imperial Favor, L827-829).
- **Gain (Lobby):** "Political Limited: If you have higher Family Honor than each other player, bow your performing Personality with 1 Personal Honor or higher: Take the Imperial Favor" (CR Player abilities, L110). Own Action Phase only; Honor must be strictly higher; it may be taken from the player who holds it (L831). Not Repeatable.
- **Spend, Limited:** "Discard the Imperial Favor and a card: Draw a card" (L112). Own Action Phase.
- **Spend, Battle:** "Discard the Imperial Favor and target an attacking enemy Personality: Move him home" (L114). It only targets attackers, so it is a defensive tool. It is a Battle action with no presence exemption, so the Defender needs a unit at that battlefield, and the target must be there [INFERENCE from L2137, L2150].
- The documents give no Honor or other benefit for merely holding the Favor.

## 7. Honor

- Family Honor is public, may be negative, and changes instantly (CR Family Honor, L70-76; Game knowledge, L1012).
- **Gains:** battle resolution (twice the cards destroyed for a win; once for a tie; section 3). Proclaim: after bringing a Personality of your Clan from your Province into play without Clan discount, on your turn, gain Honor equal to his base Personal Honor (CR Player abilities, L124). Proclaim is a Reaction not marked Repeatable, so the once-per-turn limit applies [INFERENCE from L700]. Card effects.
- **Losses:** a dishonorable Personality's destruction (L1080); card costs and effects.
- A gain of 0 is not a gain (L82). Rehonoring replaces gains as described in section 4 (L1084-1090).
- **Timing:** 40 Honor wins only when that player's turn begins (L139), so the opponent always gets a turn to respond to a 40 reached on one's own turn. Reaching -20 at any moment fixes a loss at that player's next End Phase even if Honor recovers (L149); that player still plays up to that End Phase.
- Higher Honor than every other player is the condition for Lobby (L110), and the higher-Honor player goes first (L1956).

## 8. Economy

- Gold is produced while paying a cost, from any number of sources (CR Gold production, L1395). "Any excess Gold produced is 'taxed by the Emperor' and lost" (L1397). "Gold from a single source may not be split to pay multiple costs" (L1399). There is no change.
- The Stronghold bows to produce its Gold Production (CR Stronghold, L223). Holdings bow to produce (CR Holdings, L253).
- **Clan discount:** a player buying a Personality of his Clan may pay 2 less (discount), or 2 more to ignore the Honor Requirement (allegiance), or both (CR Clan alignment and entering play, L319-325). Proclaim is available only when the discount was not taken (L124), so the choice is 2 Gold against his base Personal Honor in Honor.
- Honor Requirement is checked only on entering play (CR Playing Personalities, L307).
- Cards other than Holdings enter play unbowed, at home (CR L1062, L1100). A Personality bought in the Dynasty Phase missed that turn's Attack Phase but can defend on the opponent's turn [INFERENCE].
- A Strategy's Gold cost is paid when its action is taken (CR Strategy cards, L376). An attachment's cost is paid on attaching (L409).
- **Dynasty discard:** discarding any face-up Province card ends buying for that phase (L2073). The Province refills face-down (L884).

## 9. Public versus hidden information

Public (CR Game knowledge, L1009-1012):
- The title, stats and text of every face-up card, including discard piles (which are face-up, L906) and face-up Province cards.
- The game state of all cards, current effects, and stat totals used by procedures.
- How another player assigns units or pays a Gold cost.
- Each player's Family Honor and number of cards in hand.

Hidden (L1016-1018):
- "The number of cards currently in a deck, even their own."
- The order or identity of face-down cards in decks or Provinces, even one's own.
- Cards in other players' hands and focusing areas, until duel focuses are revealed (L2303).

Other points:
- Attachments enter play with the title showing (CR Playing attachments, L394). The documents describe no face-down attachments.
- Whether an ability has been used this turn is not named in the list. It may fall under "the game state of all cards" (L1010), but that is not explicit: [SILENT].
- Looking at face-down cards requires a card effect (CR Game knowledge, L1014-1017; examples in Part 4, L4737, L5667).

## 10. Counter-intuitive points and traps

1. A tie destroys **both** armies, destroys no Province, and pays 1 Honor per card destroyed (L2210-2216). "Nothing happens" applies only to zero Force against no units (L2189).
2. Bowed cards add no Force but still die with a losing or tied army and still count for the winner's Honor (L3846, L2195).
3. Taking a Province needs attacker Force strictly greater than defender Force plus Province Strength (L2197). Winning the battle by less kills the defenders and leaves the Province.
4. Attackers bow and go home after their battle even when they win (L2230). Only the active player straightens (L1974), and bowed Personalities cannot assign (L1066), so they cannot defend on the opponent's turn. Exceptions: Conquest (L122, R-091) and units moved home before resolution.
5. Surviving defenders are not bowed by the battle (L2232).
6. A player with no unit at the current battlefield cannot take Battle actions there, including Strategies from hand, Tactical Advantage and the Favor's Battle ability (L2137). One defender unlocks all of them; it is also destroyed if the defence loses.
7. Reactions ignore Presence unless their text says otherwise; Interrupts do not (R-092).
8. Battle actions cannot touch units at home or at other battlefields (L2150).
9. Open actions cannot be used in battle (R-030); Limited actions are for the active player only (L1999).
10. A once-per-turn Battle/Open ability used in the Action Phase is unavailable in the battle (BPG L8). A held use is lost if no battle happens (BPG L33).
11. A unit is Cavalry only if the Personality and every Follower are Cavalry (L2679).
12. The Defender assigns after seeing the Attacker's ordinary assignment, and the Defender's Cavalry assigns last (L2035-2051). The Attacker picks the battle order (L2055).
13. Ranged Attacks cannot target a Personality who still has a Follower (L3533).
14. Losing a duel has no default penalty (L2330); a tied duel is lost by both (L2315).
15. Moving a unit home rescues it from destruction and, for an attacker, from the bow (section 4).
16. Honor 40 wins only at the start of one's own turn (L139). A fall to -20 cannot be undone (L149).
17. Excess Gold is lost and one source cannot be split across two purchases (L1397-1399).
18. A new Holding arrives bowed (L251); a refilled Province is face-down until the next Events Phase (L884, L1982).
19. Deck sizes are not public, and an empty deck causes no loss (L1016, L874).
20. Dishonoring a Personality does not reduce his Force or stop him fighting (section 4).
