# Human decision model: games 3, 4 and 7

Sources: `human-compare` logs of the exported practice replays (untracked, under `artifacts/bots/human/`):

| Label | Log | Match | Result |
|---|---|---|---|
| G3 | `practice-20261109-172814.compare.log` | Lion (Tactician deck) v Spider bot | Military win T11, Provinces 2 v 0, 47 of 88 choices differ from bot |
| G4 | `practice-20261109-184403.compare.log` | Phoenix (Shugenja deck) v Crab honour/political bot | Military win T11, Provinces 4 v 0, 28 of 72 differ |
| G7 | `practice-20261110-173816.compare.log` | Lion (same deck) v Dragon bot | Military win T11, Provinces 3 v 0, 32 of 66 differ |

Province Strength is 7 everywhere except his Phoenix Stronghold (6). A Province falls only if attacking Force is strictly greater than defending Force plus Strength. "Tactician discard" means the Tactical Advantage player ability: discard a card, the Tactician gains Force equal to its Focus Value (it does not need the Imperial Favor, despite the log label). Everything below uses only what he could see: his hand, cards in play, discards, hand sizes and what the opponent had already played. Where I mention a card the opponent still held, I say so.

Three things looked for never occurred: he assigned no Cavalry in any game (his one Cavalry Personality was destroyed before it attacked), he made no end-of-turn discard, and he never spent the Imperial Favor.

## 1. Decision ledger

### G3: Lion v Spider (Spider Honor stays 0 all game)

- **T1 Dynasty.** Gold 5, no Personalities either side. He used the first-turn Province refill on one duplicate Personality only (bot: replace all three). Bought a 3-Gold 1F Tactician and a 1-Gold Holding (bot: same cards, Holding first). He never used the once-per-game refill in this or either other game (bot wanted it).
- **T3 Action and attack.** His one Personality 1F; Spider no Personalities, hand 7; Honor 7 v 0; Gold 7. Hand: a +2F Item (2 Gold, draws a card), a 1-Gold Item (+1F and +1 Honor when assigned), three movement cards, a permanent-Elite card, a blank Focus-5 card. He equipped both Items (bot: other actions first), attacked alone at the Province carrying Spider's Region (bot: a different Province). Force 4 v 0. After the defender passed he discarded the Focus-4 card he had just drawn: 8 v 7, Province destroyed. Bot: pass, which fails at 4 v 7. Dynasty: 3 Gold left, bought a Holding (agree); three 5-Gold Personalities unaffordable.
- **T5 Event, action, attack.** His one Personality 4F; Spider 4F and 5F unbowed, hand 8; Gold about 10. Hand: two send-home cards usable by a Tactician from anywhere, one attack-only send-home card, one return-to-battle card, two Elite cards, the Focus-5 blank. He declined an Event offering "discard hand, draw four" (bot: accept). Made the attacker Elite, looked at his top card and bottomed it, attacked alone (bot: no attack). Both defenders assigned (9F + 7). Spider cut him to 1F and boosted one defender; he sent that defender home (agree). Spider doubled the other defender with a self-destroying effect; he sent it home too (bot aimed at the Personality already home). He then passed at 1 v 0, keeping the second send-home card and the Focus-5 card (1 + 5 would not beat 7). No Province, no losses; Spider lost a 5F Personality to its own effect and three cards. Dynasty: two 3F Personalities at 5 Gold each.
- **T7 Action and attack.** His 6F Elite, 3F, 3F; Spider three unbowed (13F), hand 7. Hand: one send-home card, three return-to-battle cards, Focus-5 blank, Elite card. He bowed a Personality to give +4F to an attacker (bot: lobby with it) and attacked one Province with two Personalities, 13F (bot: no attack). All three defended (13F + 7 = 20). He bowed the 5F defender with a Personality ability (bot: bow a 4F one); Spider cut his attacker by 4F; he discarded the Focus-5 card (14 v 8 + 7 = 15) (bot: send-home card); he then passed one point short (bot: send-home card, giving 14 v 11). Spider sent one attacker home and negated its movement; he played a return-to-battle card on that same Personality three times, each negated, while Spider sent the second attacker home. The send-home card was never played. Result 0 v 8, no units lost on either side. Dynasty: a 2F attachment-destroyer with Personal Honor 3 (4 Gold) and a 3F Follower-creating Personality (6 Gold) (bot: two more 3F Personalities).
- **T8 defence.** Spider attacked one Province with 13F. His unbowed units: the two just bought (2F, 3F). Hand: one send-home card, attack-only cards. He did not defend (agree). Province lost, 4 to 3.
- **T9 Action and attack.** Spider's three attackers still bowed, one new 5F unbowed, 3 Provinces, hand 5. He put a 1-Gold Item and the +4F boost on one attacker, lobbied with the Personal-Honor-3 Personality that was not attacking (Favor taken; bot wanted the lobby earlier), created a Follower. Split attack: 7F at one Province, 8F at another (bot: three Personalities at one Province). The lone defender opposed the 8F attacker and doubled itself; he sent it home (8 v 0). At the other Province the defender passed and he discarded a Focus-3 card: 10 v 7 (bot: pass, fails at 7 v 7). Two Provinces destroyed; Spider's defender died to its own effect. Left home unbowed: 3F Personality with 2F Follower; one send-home card kept. Dynasty: a 5-Gold Personality, a Holding at 0 Gold, a 2-Gold Holding (whole row).
- **T10 defence.** Spider, on its last Province, attacked one Province with 9F (two of three Personalities). His unbowed: 3F and 5F; Favor and two send-home cards in hand. He assigned both defenders to Provinces that were not attacked (bot: both to the attacked Province) and passed every battle. Province lost, 3 to 2.
- **T11.** All Spider Personalities bowed, one Province. He created a Follower, looked at his top card (bot: discard the Favor to cycle a card), attacked with all six: 28 v 0. Win.

### G4: Phoenix v Crab

- **T1 Dynasty.** Gold 6. Refilled all three face-up Personalities (agree). Bought two Holdings (4 and 2 Gold), no Personality.
- **T3.** Gold 10; Crab no Personalities. Did not pay 3 Gold for a Holding's card draw (bot would). Bought a 6-Gold 2F Shugenja and a 3-Gold Holding (bot: Holding first). A 9-Gold Personality left.
- **T5 Action, no attack.** His one Shugenja; Crab 0F and 2F Courtiers unbowed, hand 8; Gold 13. He attached a free "bow an enemy card" Spell and a 2-Gold +2F Spell (6F). Declined to bow for Crab's Holding (Crab +2 Honor). Did not attack (bot: attack). Dynasty: second 6-Gold Shugenja and a 2-Gold Holding; 3 Gold unspent, nothing else affordable.
- **T6 defence.** Crab destroyed his 2-Gold Spell with a Favor-discarding card, dishonoured a Shugenja, then attacked one Province with a lone 2F Courtier. He defended with both Shugenja (agree), bowed the attacker with the free Spell (+2 Honor), and when Crab bowed his other Shugenja he attached a free straightening Spell in battle and straightened her (bot: a different Spell). 0 v 6, attacker destroyed, no losses.
- **T7 Action and attack.** Two Shugenja 2F + 4F; Crab one unbowed 2F, hand 6. He attached a free Spell to the weaker Shugenja (her text gives +2F while she has a Spell): 8F, one more than Strength (bot: pay for a draw, attach a different Spell). Crab bowed its last unbowed Personality for its Holding. He attacked one Province with both (agree); no defence; 8 v 0, Province destroyed. Nothing left home. Dynasty: 9-Gold and 5-Gold Personalities and a 2-Gold Holding.
- **T9 Action and attack.** His four Personalities (4F, 4F, 4F, 3F); Honor 16 v 11; Crab two unbowed, one bowed, 3 Provinces, hand 7. He attached a free send-home Spell to the 3F Shugenja whose trait makes it un-negatable (bot: lobby with her), then passed four times while Crab bowed both remaining Personalities for political effects. Never lobbied. Split: 4F alone at one Province, 8F at another; the 3F Shugenja stayed home unbowed (bot: all four at one Province). No defence. He chose "pass all battles" before the battles: 8 v 0 took one Province; 4 v 0 did not take the other, and the lone attacker's "+5 Force to my army" ability went unused. Dynasty: 10-Gold Cavalry Personality, 5-Gold Personality, two Holdings (0 and 1 Gold).
- **T10.** Crab paid 10 Gold and the Favor to destroy the Cavalry Personality; no attack. He only passed.
- **T11.** Five Personalities; Crab one unbowed, 2 Provinces, hand 8. He attached a 1-Gold Spell (bot: lobby), waited until Crab bowed its last Personality, then attacked both Provinces: 7F and 11F. He selected battles manually this time; at the 7F battle the defender passed and he used the +5 ability (12 v 0) (bot: pass, fails at 7 v 7). Win.

### G7: Lion v Dragon

- **T1.** Gold 5. No Province refill (bot: replace three). Bought a 3-Gold 2F Tactician and a 2-Gold Holding.
- **T3.** Dragon no Personalities; Gold 8. All 8 Gold on a Weapon (+Force scaling with his Personality count, +2 Honor, Battle: create a 2F Personality at the battlefield) (agree). Attacked alone (bot: another Province). Defender passed; he created the 2F Personality (7F), then discarded an attack-only send-home card, Focus 2: 9 v 7 (bot: pass, fails). Province destroyed. No Gold for Dynasty.
- **T5.** His 5F Tactician and 2F token; Dragon 4F and 3F unbowed, hand 8. He put a 3-Gold 2F Follower on the token, not on the Tactician who already held the Weapon (bot: on the Tactician). Attacked one Province with both, 9F (agree on attacking). Both defended (7F + 7). Dragon made a Ranged 4 attack; he destroyed the 4F defender with a "destroy a card without attachments" card (bot: the 3F one); Dragon repeated the Ranged attack; he created a second 2F token (12 v 3 + 7) (bot: −4F card); Dragon sent the bare token home (10 v 10). He passed all battles holding a −4F card, a send-home card and a return-to-battle card (bot: −4F card on the defender). Result 10 v 3: defender destroyed, Province not destroyed. Dynasty: one 5-Gold Personality.
- **T7.** His 7F, 5F, two tokens; Dragon 4F and 5F, 3 Provinces, hand 6. Declined the "discard hand, draw four" Event (bot: accept). Attached a 2-Gold Item, lobbied with a token Personality (Favor; agree). Split: 7F alone at one Province, 9F at another (bot: all at a third Province). Dragon put both defenders against the 7F attacker; one then moved to the 9F battle, where the log counts it at 0 Force (Dragon had bowed it to draw cards). He passed three times there holding two send-home cards (bot: send the defender home): 9 v 0, defender and Province destroyed. At the other battle Dragon destroyed his Weapon (2F left) and sent him home; he cut the defender to 1F (agree), returned with a return-to-battle card, and passed (bot: send the defender home): 2 v 1, defender destroyed, Province stands. Dynasty: one 5-Gold Personality.
- **T9.** Five Personalities; Honor 15 v 12; Dragon two 3F unbowed, 2 Provinces, hand 6. He paid 3 Gold to put a Follower on his newest Personality (bot: discard that Follower to cycle with the Favor). Attacked one Province with exactly the three units that had attachments, 14F; the two bare Personalities (4F) stayed home (bot: all five, split 2 and 3). No defence. 14 v 0, Province destroyed. Dynasty: a 4-Gold Personality (bot: a 5-Gold one).
- **T10 defence.** Dragon, on its last Province, put 12 Gold of Weapons on its two Personalities and attacked one Province with 14F. His unbowed: three 2F Personalities; hand included a send-home card and a return-to-battle card. He assigned all three to a Province that was not attacked (bot: to the attacked one, then move a bowed 5F unit in) and passed. Province lost, 4 to 3.
- **T11.** Dragon's Personalities bowed. No actions (bot: cycle with the Favor). All six attacked: 20 v 0. Win.

## 2. Threat and opportunity reads

**Attacks.**

| Turn | Neutralisation route visible to him | Left home | Risk accepted |
|---|---|---|---|
| G3 T3, G7 T3 | None needed (no enemy Personalities); Force from hand to cross Strength | Nothing | None: no counter-attack possible |
| G3 T5 | Three send-home cards against two defenders, then a Focus-5 discard (5 + 5 = 10) | Nothing | 9F of enemy could counter-attack an empty home; a failed attack costs no units. A Force cut he had not seen broke the plan |
| G3 T7 | Bow one defender, send one home, +5 from hand, three return-to-battle cards | One bowed Personality | Attacking 13F into 20; again nothing unbowed at home |
| G3 T9 | Enemy army bowed; one send-home card for the one defender; one discard for the other Province | 5F unbowed, one send-home card | Small: he could not lose the game next turn |
| G4 T7, T9, T11 | Opponent bowed its own Personalities first; free Spells in reserve | T9 only: a 3F Shugenja with a send-home Spell | Crab's Stronghold can send an attacker home for 3 Gold (public). At T7 one use would have stopped the attack. It was never used |
| G7 T5 | One destroy effect, a token-creating Weapon, −4F card, send-home card | Nothing | Counter-attack by survivors; he killed both instead |
| G7 T7 | Split to force a choice; −4F, two send-home, one return card | One bowed token | Same |
| G7 T9 | None needed: 14F beats both defenders plus Strength (13) with no cards | 4F unbowed | None |
| G3 T11, G7 T11 | Enemy army bowed | Nothing | None |

Home defence never held a Province in these games: on the two occasions he kept unbowed units home and was then attacked (G3 T10, G7 T10) he chose not to use them. In the early and middle turns (G3 T5, T7; G7 T5, T7) he left nothing unbowed at home against opponents with 9F or more available. The hypothesis that he checks whether he can defend the counter-attack is therefore not supported here; what the logs show is that he prices a Province as expendable while he is ahead in Provinces.

**Verdict on the player's stated method.** Supported: he counts his removal effects against the defenders before attacking, accepts failed attacks that lose no units, and revises after each reveal (section 3). Not supported in these games: checking that he can defend the counter-attack. Not testable: Cavalry manoeuvres.

**Defences.** G4 T6: aim was to kill a lone 2F attacker with 8F of defenders and a free bow effect; achieved. G3 T8: concede. A hold was arithmetically possible (send the 5F attacker home, then two Tactician discards: 12 v 8) but needed three cards and exposed 2F and 3F Personalities to Force cuts and Ranged attacks Spider had shown. G3 T10 and G7 T10: concede with units and cards in hand that could have won the fight (one send-home card made it 8 v 4 in G3; a send-home plus a discard made it 10 v 6 in G7). In both, the opponent was on its last Province and its attackers would be bowed on his next turn. In both he assigned his defenders to an unattacked Province, which has the same effect as not defending; the log does not show why he did this instead of passing, and I found no rules reason.

## 3. Information updates

- **G3, Force cuts and movement negation.** On T5 and T7 he attacked an unbowed Spider army and was beaten by effects he saw for the first time (Force reduction on T5; send-home with negated movement on T7). On T9 and T11 he attacked only when that army was bowed, and on T9 he split so that Spider's single defender could cover one Province.
- **G4, attachment destruction.** After Crab destroyed his 2-Gold Spell on T6, every later Spell he attached cost 0 or 1 Gold and was attached on the turn it was needed (T6 in battle, T7, T9, T11). He held five other Spells all game.
- **G4, pass-all error.** On T9 "pass all battles" cost him a Province; on T11 he selected battles one at a time and used the ability he had skipped.
- **G7, "without attachments" effects.** Dragon's send-home card hit a bare token on T5 and a just-stripped Personality on T7, and a Dragon Personality's destroy ability also needs a target without attachments. On T9 he paid 3 Gold for a Follower on his newest Personality and attacked only with the three units that carried attachments. His T5 choice to put the Follower on the bare token pre-dates any such reveal, so that one may be habit rather than a reaction.
- **Waiting for the opponent.** G4 T7, T9, T11: he passed in the Action phase with legal actions available until Crab had bowed its Personalities for political effects, then declared. G7 T7 (9F battle): three passes while Dragon spent two cards and bowed its own defender. G3 T7: he passed at 14 v 15; had Spider also passed he would have won the battle and destroyed three Personalities, so the pass forced Spider to spend cards, although it also gave up a winning send-home.
- **Never changed.** He declined Crab's "bow one of your Personalities or I gain 2 Honor" offer all seven times.

## 4. Economy versus recruiting

Counts are before the purchase: Personalities / Holdings (Holdings exclude the Stronghold).

**G3 (Lion v Spider)**

| Turn | Gold | His P/H | Opp P/H | Bought | Visible reason |
|---|---|---|---|---|---|
| 1 | 5 | 0/1 | 0/2 | Personality 3, Holding 1 | Cheapest of each |
| 3 | 7 | 1/2 | 0/3 | Two Items (3) before attacking; Holding 3 | Leftover could not buy a 5-Gold Personality |
| 5 | ~10 | 1/3 | 2/3 | Two Personalities (5, 5) | Opponent now has an army |
| 7 | ~10 | 3/3 | 3/3 | Two Personalities (4, 6) | Failed attack; one buy has Personal Honor 3 (he lobbied with it on T9) |
| 9 | ~9 | 5/3 | 4/3 | Item 1; Personality 5; Holdings 0 and 2 | One Holding was free because its cost falls per Personality |

**G4 (Phoenix v Crab)**

| Turn | Gold | His P/H | Opp P/H | Bought | Visible reason |
|---|---|---|---|---|---|
| 1 | 6 | 0/1 | 0/2 | Holdings 4 and 2 | No cheap Personality; one affordable at 6 was passed over, reason not shown |
| 3 | 10 | 0/3 | 0/5 | Personality 6, Holding 3 | Opponent has no army |
| 5 | 13 | 1/4 | 2/5 | Spell 2; Personality 6, Holding 2 | 9-Gold Personality unaffordable after the Spell |
| 7 | 16 | 2/5 | 2/6 | Personalities 9 and 5, Holding 2 | Spent to zero |
| 9 | 20 | 4/6 | 3/8 | Personalities 10 and 5, Holdings 0 and 1 | Whole row |

**G7 (Lion v Dragon)**

| Turn | Gold | His P/H | Opp P/H | Bought | Visible reason |
|---|---|---|---|---|---|
| 1 | 5 | 0/1 | 0/2 | Personality 3, Holding 2 | Cheapest of each |
| 3 | 8 | 1/2 | 0/5 | Weapon 8, nothing else | Enabled a Province on T3 |
| 5 | 8 | 2/2 | 2/5 | Follower 3, Personality 5 | |
| 7 | 8 | 4/2 | 2/6 | Item 2, Personality 5 | |
| 9 | 8 | 5/2 | 2/6 | Follower 3, Personality 4 | Why this Personality over two 5-Gold ones is not shown |

In G7 a 1-Gold Holding sat in his row from T3 to the end and he never bought it; his production stayed at 8 while Dragon's reached 17. Every turn the Gold went first to an attachment used in that turn's attack, then to one Personality. He attacked on every one of his turns from T3 and destroyed all four Personalities Dragon bought before T8, so Dragon's Gold lead bought nothing lasting until T10.

## 5. Candidate rules

Evidence: strong = 3 or more instances, moderate = 2, single = 1.

1. **Attack feasibility.** IF no group of my unbowed units can exceed Province Strength even undefended, counting Force I can add from hand, THEN do not attack. ELSE IF the opponent has no unbowed Personalities THEN attack, with a single Personality if necessary and regardless of what stays home. Support: G4 T5 (no attack at 6F v 7); G3 T3, G7 T3, G4 T7, G3 T11, G7 T11, G4 T11. Contradicting: none. Strong.
2. **Late Force.** IF I hold Force I can add after assignment (Tactician discard, army bonus, unit creation) THEN count it when deciding to attack, but add it only after the defender has passed or committed, AND add the minimum that exceeds the threshold. Support: G3 T3, G3 T9, G7 T3, G4 T11. Partial contradiction: G3 T7 (added mid-battle while one short). Strong.
3. **Which card to discard for Force.** Discard the card with the least use in the current matchup whose Focus is just sufficient; keep send-home cards unless they are dead (attack-only against an opponent with no Personalities). Support: G3 T3, G3 T7, G3 T9, G7 T3. Strong.
4. **Attacking into defenders.** IF the opponent's unbowed defenders plus Strength exceed my Force AND my send-home, bow, destroy and Force-cut effects number at least the defenders I must remove THEN attack; a failed attack that loses no units is acceptable. ELSE do not. Support: G3 T5 (3 effects v 2 defenders), G7 T5, G7 T7. G3 T7 fits the condition loosely (2 removers plus recovery cards v 3 defenders) and failed. Strong, with one costly failure.
5. **Split.** IF the opponent's unbowed defenders are fewer than its Provinces AND I can form more groups that each reach Strength (with a finisher in hand) than it has defenders THEN send one group per Province. Support: G3 T9, G4 T9, G4 T11, G7 T7 (two groups v two defenders, so the "more groups than defenders" clause was not met there). Contradicting: none; the bot stacked every time. Strong.
6. **Size, do not stack.** IF one group can exceed all unbowed defenders' Force plus Strength by 1 without cards THEN send that group and keep the rest home unbowed. Support: G7 T9 (14 v 13), G3 T9 and G4 T9 (one unit kept home). Contradicting: the two final-turn attacks sent everything. Strong for non-final turns.
7. **Do not rescue a doomed defender.** IF my Force already exceeds defenders plus Strength, OR I am winning the battle and cannot reach the Province, THEN pass and keep send-home cards, because a defender sent home is not destroyed. Support: G7 T7 (both battles), G3 T5. Strong within two turns. Caution: on G7 T5 he passed at 10 v 3 + 7 when any of three cards in hand would have taken the Province; I read that as an error, not as this rule.
8. **Target the largest.** WHEN removing or bowing one defender, choose the highest-Force eligible one; WHEN the opponent boosts a defender, send the boosted one home. Support: G7 T5, G3 T7, G3 T5 (twice), G3 T9. Strong.
9. **Let the opponent bow itself.** IF the opponent has abilities that bow its own Personalities and unbowed Personalities remain THEN pass in the Action phase until it stops, then declare. Never bow my own Personality in exchange for denying the opponent 2 Honor. Support: G4 T7, T9, T11 and seven declined offers. Strong, one opponent.
10. **Punish an attack.** IF the opponent's Personalities are bowed from attacking THEN attack this turn with the split or all-in that takes the most Provinces. Support: G3 T9, G3 T11, G7 T11. Strong.
11. **Concede a Province.** IF holding needs cards from hand or puts Personalities at risk of dying AND I will still have at least 2 Provinces AND I am ahead or level on Provinces THEN do not defend. Sharper form: IF the opponent is on its last Province and its attackers will be bowed on my turn THEN never defend. Support: G3 T8, G3 T10, G7 T10. Contradicting: none. Strong. Untested at 2 Provinces or fewer.
12. **Defend to kill.** IF my unbowed defenders' Force exceeds the attackers' without cards AND I hold a free neutraliser THEN defend with everything. Support: G4 T6. Single.
13. **Lobby with a spare.** IF I have higher Honor AND a Personality that will not attack and has no better use this turn THEN lobby after boosts are done; otherwise do not. Hold the Favor; do not discard it to cycle. Support: G3 T9, G7 T7; four declined cycles. G4: never lobbied, even on T9 with an idle Personality. Moderate.
14. **Spend order.** Gold goes first to an attachment that makes this turn's attack reach its threshold, then to Personalities, then to Holdings costing 2 or less or bought with Gold that cannot buy a Personality. Support: every Dynasty phase in G3 and G7, G4 T3 to T9. Exception: G4 T1 (two Holdings). Strong.
15. **Opening.** IF a Personality costing 3 or less and a Holding are both affordable on T1 THEN buy both, ELSE buy two Holdings. Support: G3, G7; G4. Strong for the first branch, single for the second.
16. **Play around "without attachments".** IF the opponent has shown effects that need a target without attachments THEN attack only with Personalities that carry one, buying a cheap attachment if needed. Support: G7 T9. Single.
17. **Cheap attachments after attachment removal.** IF the opponent has destroyed one of my paid attachments THEN attach only free or 1-Gold attachments, at the moment of need. Support: G4 T7, T9, T11. Strong, one game.
18. **Keep the hand.** IF my hand holds three or more removal or movement cards THEN decline effects that replace the hand, and do not pay Gold or the Favor to cycle. Support: G3 T5, G7 T7, G4 T3. Strong.

Apparent mistakes or luck: G3 T7 (three return cards wasted on a Personality whose movement was negated; a winning send-home not played); G4 T9 (pass-all forfeited a Province); G7 T5 (Province forfeited at 10 v 10); G4 T7 (Crab never used its public 3-Gold send-home).

## 6. Where the bot differed and it mattered

- **Passing instead of adding Force after the defender passed** (G3 T3, G3 T9, G7 T3, G4 T11): the bot's pass loses four Provinces, each by 0 to 3 Force.
- **Stacking instead of splitting** (G3 T9): one Province instead of two. G4 T9: the bot's stack takes one Province, which is what he actually got after his own error, but his split was worth two.
- **G4 T5, bot attacks with 6F into Strength 7:** cannot succeed and bows the only Personality holding his defensive Spell before Crab's turn.
- **G7 T7 and T5, bot sends defenders home in won battles:** saves two or three Dragon Personalities that he destroyed by passing.
- **G7 T5, bot destroys the 3F defender, not the 4F one:** leaves 1F more defence.
- **G7 T9, bot discards the Follower to cycle:** removes the 2F that made 14 v 13 and leaves an attacker open to the send-home effect.
- **G7 T10, bot defends the attacked Province with 6F against 14F and moves a bowed 5F unit in:** the Province holds (11 + 7 v 14) but the battle is lost 11 v 14, so four Personalities and a Follower die unless a send-home card follows; Dragon also had public Ranged attacks (one per Fire token) able to kill 2F defenders.
- **G3 T10, bot defends 8F against 9F:** holds the Province but loses both defenders unless it follows with a send-home card.
- **G3 T5 and G7 T7, bot accepts "discard hand, draw four":** throws away the hand that produced the send-home plays of G3 T5 and T9 and the Force-cut and return plays of G7 T7.
- **G3 T5 and T7, bot does not attack:** T5 cost Spider a Personality and three cards for nothing; T7 cost him four cards for nothing. Mixed.
- **Bot was right:** G7 T5 (−4F card takes the Province), G3 T7 (send-home at 14 v 15 gives 14 v 11), G4 T9 (select the battle and use the +5).
