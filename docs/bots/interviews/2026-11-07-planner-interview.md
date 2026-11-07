# Planner bot interview, 2026-11-07 (issue #1185)

The scenario interview that the Contender (`docs/bots/CONTENDER.md`) was built from, kept in full so another machine or session can read what was actually said. **Mendon's words are verbatim** (typos included),
quoted under "Mendon:". The questions are the ones asked ("Scenario" blocks, with the option labels that were offered); "Read as" lines are the assistant's interpretation at the time, not Mendon's words, and are
labelled as such. Where Mendon later corrected or contradicted something, the correction is shown after it. Nothing here is a confirmed ruling; it is design input for a practice bot.

How it was used: each round's answers were turned into rules in `src/L5R.AI` (`TrainingOpponent.Planner.cs`, `.Estimator.cs`, `.Triage.cs`), then measured bot-against-bot. The measured result and the paired
evidence are in `docs/bots/CONTENDER.md`; several of the rules below **lost games** when encoded (the attack estimate, the discard rule) and are off in the Contender. The interview describes how a human
player thinks; it does not follow that the first encoding of it is right.

## 0. The brief

Assistant's earlier finding that opened it: the champion bot has no turn plan; it scores each legal action on its own, with a real planner only for attack assignment and defence.

Mendon:

> Yes. The bot should make a plan for the turn after the events phase. If it uses its border keep ability to refresh the cards in its provinces, it should rebuild the plan. Then, it should revisit the plan after each battle is resolved and after the attack phase.
>
> I will walk you through how a human player thinks and then we will discuss options for the bot.
>
> 1. When a player reveals cards during the events phase, after resolving Events, regions, and Celestials that come into play, they evaluate the gold available to them and the shape of the gold (how it's broken into components) to understand what in their fate hand and provinces they can afford in a turn. If they can afford every single card, then this is trivial and doesn't enter into consideration. Much more often, though, they cannot afford everything and are constrained by their gold. They also evaluate abilities on holdings that either bow the holding or require the holding to be unbowed to be performed.
>
> The evaluation is not linear and does not flow identically each turn, however, the intent is to evaluate their options against their opponent's tableau. For a destruction deck, the goal is to destroy the opponent's provinces, so that requires the player to evaluate how to attack the opponent. For an honor deck, it will ask how to maximize honor gain for the turn. For a dishonor deck, it will look to produce honor losses for an opponent. and for an enlightenment deck, how to create the conditions necessary to play any rings in its hand.
>
> A destruction deck is easiest to imagine, so we'll start there. The player will evaluate its opponent's force and the abilities the opponent has to counteract the forces it can bring to bear. If the bot has enough force to destroy a province, or is able to create enough force to destroy a province through cards in its hand, it should consider an attack and make a plan to attack either by planning attachments to its personalities, playing abilities that augment their force, or creating a rough battle plan for how to produce enough force to destroy a province.
>
> It should then evaluate its opponent's tableau, including battle and open actions on the table that can be played against it and evaluate them for harmfulness, then develop a mitigation strategy, if possible. In the case of a destruction deck, attacking and destroying enemy cards in a victory should be considered only slightly less valuable than destroying a province because the more of an enemey's estate is destroyed, the less material they have to defend with. So, cards that have a high force and powerfully harmful abilities should be mitigated first by destroying, bowing, or sending home. The bot should give some credence to the number of cards in the opponent's hand, however, the bot should value cards in its hand, especially battle actions and engage reactions and plan to use them as mitigation effects. If the bot has fewer cards in its hand than its hand limit and has a mechanism for drawing cards up to the hand limit, it should evaluate whether the cost of using those cards is worth the additional value of drawing cards. It should do this by assigning some probability to drawing a battle action based on its knowledge of the deck. It should be cautious because some cards (like traveling peddler) may have a high cost associated with drawing a card.
>
> 2. Players then make battle plans. They think about whether they can destroy a province or just force an opponent to lose defenders trying to hold off the province. They also think about defending against a counter attack on the subsequent turn. Sometimes, trading provinces is worth it if the bot feels confident that it can destroy more provinces attacking than the opponent can destroy in a counter attack. So, it should plan some defense, at least, from its provinces. Or, if there is a personality likely to die from an action the opponent can produce, such as an attack, then it should consider whether or not the opposing ability can be dealth with before the opponent uses it to destroy the personality. Sometimes this is desirable as a more powerful action will disable that unit, making the death of the personality suitable bait. However, often, it is better to spread out attachments on to multiple personalities to reduce the likelihood of a chokepoint being catastrophic, like a duel or a ranged attack allowed to bypass followers.
>
> 3. Once this evaluation is made, the player decides, before taking any actions in the Action Phase, whether they will attack or not. THey will revisit this after the Actions Phase because the opponent may take open actions. If the opponent takes no open actions, then they will not revise and will attack or not based on their earlier decision unless they decided that the attack would be contingent on some open action the opponent may play. For example, a player with confusion at court in their hand may not feel confident they can defeat Doji Dainagon in battle but may be willing to attack if she bows to lobby, so if the player lobbies and has the imperial favor, they'll attack on a contingent basis because she is now bowed and the player feels confident it can attack. On the otherside, it's possible the imperial favor is too powerful and the player might not have confusion at court and would only plan to attack if Doji Dainagon did NOT lobby. Context matters.
>
> 4. Once this determination is made, it is a rough guide to the turn. Usually it looks like, "play cards from hand during action phase to achieve some goal". If the player is not planning to attack, they are unlikely to use cards to bow their opponent's cards unless they have no cost (i.e., an ability printed on card in play that doesn't require bowing) or if they achieve an effect desired (example: You may want to bow a personality that can bow a holding before they are able to bow said holding because you plan to use the holding you think they're going to bow). Then, you carryout the action phase plan and proceed to the attack phase. If there is no attack, it's simple, the bot moves on and spends its remaining gold on cards it planned to purchase.
>
> 5. If, however, there is an attack phase and the outcome is not as expected by the player, they will re-evaluate the cards in their provinces. Perhaps they planned to proclaim a personality but they lost more personalities in battle than anticipated, so now they are prioritizing recruiting more personalities, they might opt to purchase a different slate of personalities. OR if the battle went better than expected and they have substantially more matieral than their opponent, they might choose to purchase several holdings to bolster their economy. At every step, the goal is to set up the player to be in a stronger position than the opponent. It may be "stronger" to have all of your personalities bowed and be subject to a counter attack. Let's consider: if a player attacks and destroys one or mre provinces, leaving their opponent with two provinces, a counter attack that reduces the attacker to 1 province may still be a stronger position relative to the opponent because the opponent will have bowed, unassaignable units and the player may now be able to easily destroy the remaining two provinces. Winning with one province left is as much of a win as winning with four provinces remaining.
>
> 6. On the other hand, overall, if the player does not think they are able to attack and destroy a province, they will evaluate how to best position themselves to defend against aggression from their opponent then counterattack on their next turn. Or, if they have a card like Retribution, they may bait the opponent into an attack then use retribution to attack and get a leg up on them.
>
> 7. On a player's first turn, they will be thinking about bringing out personalities to acheive their objective for future turns. Personalities aren't always prioritized, on a first turn, building a robust economy can be powerful. However, if a player sees a path to destroying a province on the second turn (allan's lion deck does this very well by using force bonuses with tactical advantage) it may prioritize a cheap personality  that it can give a force granting attachement to and then attacking on the next turn. So, the bot should be planning heavily for the current turn and then lightly for the next turn.
>
> I suspect that this isn't enough to build bot logic on. Instead, I want you to grill me on how to build logic by asking me "what would a player do in [this scenario]" and then attempt to turn that into logic.
>
> From this, we will build a separate bot from the current bot and we'll test the outcomes against the current bot until the new bot is a fair bit better than the old bot.

Read as (assistant): a separate planning bot, benchmarked against the current one; a plan built after Events and rebuilt after a Border Keep refresh, the Action phase, each battle and the attack phase; heavy planning for this turn,
light for next; destruction decks first.

## 1. Round 1: buying, attacking, trading, contingencies

**Scenario 1, Gold budget.** Turn 2, destruction deck. 8 Gold (a 5 and a 3 from two Holdings). Row: a 5-Gold Personality (Force 3) and a 6-Gold Personality (Force 4). Hand: a 3-Gold attachment giving +2 Force. The opponent has weak defenders. What do you buy? Options: cheap Personality + attachment; biggest Personality, hold the attachment; depends on what the attachment is; depends on the opponent's tableau.

Mendon:

> It's a mix of 3 and 4. proclaiming a 5 gold personality might keep me ahead of the opponent in honor so they can't take the imperial favor. additionally, what abilities do my personalities have vs my opponent? A card like Bayushi Waru in the Towers of the Yogo is very powerful if I can give him Elite because his ability + the Towers of the Yogo ability mean he can bow two cards AND have his force contribute to resolution. Whereas a cheaper personality with lower force only bows one person and contributes less force. However, maybe I know they're very good at taking out one strong personality, in which case two cheap personalities would be better because it keeps me present at the battle longer to achieve my  desired outcome. Super dependent on the opponent's tableau and the abilities on my cards, not just their force.

**Scenario 2, Attack margin.** Attacker, Force 9 against a Province of Strength 6; two unbowed enemy Personalities at home (total Force 5), 3 cards in hand, 4 Gold unspent. When do you attack? Options: only if Force beats Strength + defenders; attack because defenders and Strength are not stacked; attack with a margin for tricks; attack even if it likely fails.

Mendon:

> If I think I can win the battle, I attack. I want to destroy my enemy's units if possible as well as their province. So, I'd thinka bout how many actions I have in hand, whether I can bow, destroy, send home, or reduce the force of their army. If I don't destroy the province but I kill their personalities, my army can attack next turn and will be bigger for what i buy in the Dynasty phase. Having enough to destroy the province likely forces my opponent to defend. And, if they have lower force than I do, there's a good chance that they haven't been able to fully execute their strategy. Generally, destroying personalities early and often is a good strategy for destruction decks, even if provinces aren't getting destroyed.

**Scenario 3, Counterattack.** You can destroy 1 of the opponent's 4 provinces but your army ends bowed; the opponent could then destroy 1 of your 4. Attack? Options: yes a straight trade is fine; only if the trade favours me; no unless I keep defenders home; only when it wins or leaves them on 2 or fewer.

Mendon:

> Yes, a trade is fine if its 1:1. I'd look at hand size and deck content to guess whether they could expand it to 2 provinces and whether the dynasty cards I'm purchasing will be sufficient to deflect it from being 2 provinces. If yes, it's a worthy trade because if we trade 1:1 and I always take the first province, I'll win in the end.

**Scenario 4, Contingent plan.** Doji Dainagon (the Imperial Favor lobbyist) is unbowed and would stop your attack; you hold Confusion at Court and the Favor is available. How should the bot represent a contingent attack? Options: two plans chosen after Open actions; one plan with a named condition; replan from scratch after the Action phase; let me describe it.

Mendon:

> I'd probably go with 2 and 3. First, you look to see if the opponent does the thing. They may also do other things, such as straighten her. So, it's a good idea to build a plan based around contingencies then revise your plan if they do something else unexpected. So, a player who uses paid off to straighten Dainagon (or bow on of my guys) would have so changed the math that I'd have to re-evaluate. Even still, the plan to attack was valuable because it informed attachments and other action phase actions. For cards in my hand that get discarded, I might wait to play them until after Dainagon bows to lobby and would prefer actions on printed cards in play or attaching cards to playing strategies as a wait-and-see mechanism.

Read as (assistant): buying weighs Gold shape, ability synergy, the opponent's removal profile and Proclaim value (an Honor lead denies the Favor), not Force per Gold; attack when the battle can be won counting the hand, and killing units has value
even if no province falls; a 1:1 trade is fine, especially taking the first province, subject to whether the opponent can make it 2; contingency plans that also steer earlier choices (free printed abilities first, hand cards that cost resources held).

## 2. Round 2: triage, drawing, the first turn, rebuilding

**Scenario 1, Threat triage.** You can neutralize only one before attacking: (A) Force 6 Personality with no ability, (B) Force 2 Personality whose Battle action destroys one of your units, (C) Force 3 Personality holding an unbowed Holding that gives the opponent +3 Force on defence, (D) the Imperial Favor lobbyist, Force 3. Mendon chose **B: the dangerous ability** (over highest Force, battle-math swing, or the lobbyist).

**Scenario 2, Draw vs cost.** 3 cards in hand (limit 8), an ability that draws up to your hand limit but costs 2 Honor, 4 Gold you will not otherwise spend; 12 of the 40 cards left are Battle actions or Engage reactions; you attack next turn. Draw?

Mendon:

> That's an insane deal for 5 cards. Think about the traveling peddler, it costs 5 gold opportunity cost to draw one card. I'd give that honor up and gold for that many cards. I'd probably give that much up for 2 cards and, in battle, if I needed a card to have a chance to win, i'd pay that every time.

**Scenario 3, First turn** (assistant's scenario was flawed; Mendon said the row would not occur). 5 Gold, going second; row: a 4-Gold Personality (Force 3, Personal Honor 2), a 3-Gold Holding producing 2, a 5-Gold Holding producing 3, a 2-Gold follower.

Mendon:

> I don't think this exists as a real option. However, I would likely refresh the cards and hope to get a 2 gold holding that produces 3. However, assuming I have a 3 gold producing holding and a border keep, I would use the 2 gold to recruit the personality without proclaim and 3 gold to get the 2 gold producing holding. This enables further economic buildup and reveals the greatest number of new cards. I'd likely discard the 5 gold cost, 3 gold holding in hopes of getting a more efficient card.

**Scenario 4, Replan after a bad battle.** Planned to Proclaim and buy two more bodies; lost 2 of 3 Personalities to the opponent's 1; Dynasty phase, 9 Gold, row of two Personalities (cost 4 and 5) and two Holdings (cost 4 each).

Mendon:

> It depends on the army they are able to rally against me. Saying it went badly, I assume I lost more than they did, in which case, more personalities matters more. It might be a losing proposition at that point, though, but you'd still want to bring out the personalities in order to either defend or deter them for fear of a more robust counterattack when your first army straightens.

Read as (assistant): a unit whose ability can hurt is removed before a bigger plain body; a card is worth about 5 Gold, so paying a little Honor and Gold to draw several cards is a good deal, and mid-battle the bot should pay almost anything for a card
it needs to win; the first turn is about efficiency and revealing cards; after a bad battle, buy bodies.

## 3. Round 3: Honor, Holdings, reserving a Holding, defending

**Honor.** "When does Honor become too precious to spend or give away?"

Mendon:

> Contextual. I'd think harder about it if it would take me below my opponent's honor. If my honor is still above most of my personality's honor requirements in the deck, I'd likely not be fussed about going down to it but when it adds cost structure to recruiting, it is undesirable. If there's a card in play that changes honor requirements, I'd adjust the evaluation around that. If I were at risk of losing or putting myself out of reach of an honor victory, I would think harder. But, if there was no chance of winning the battle without drawing the card, unless I think I can recover from losing a battle, I'd probably always draw the card no matter what.

**Holding value** (Holding A cost 5 produces 3; B cost 2 produces 3; C cost 4 produces 2 with a useful ability).

Mendon:

> Efficiency is highly desirable, so the 2 gold cost, 3 producing holding is always attractive. The useful ability is contextual. If it is something like a temple fortress and I have an honor deck, I'm going to weigh that special ability pretty highly. If it is something that depends on something else occuring or is low probability, I would tend to discount it. In general, 1 gold more than a card produces is a good cost. So, a 5 gold holding that only produces 3 gold and has no other ability would be low priority compared to a 4, 3, or 2 cost holding that produce 3 gold (each of which becomes more attractive than the last as the gold cost declines).

**A Holding that either produces 3 Gold or bows to destroy a bowed enemy Personality; the plan needs 6 Gold.**

Mendon:

> Reserve the holding, if possible, unless what it would pay for prevents a more painful loss on my part than it can produce on the enemy or pays for a card with a similar ability but permanent. If a holding straight up destroys a card where a strategy would destroy the same card with a similar cost, I would prefer to use the card in play to the strategy, preserving optionality of cards in hand for a future scenario.

**Defending when you cannot attack** (opponent Force 8 across 3 unbowed Personalities; you have 2 Personalities of Force 3 and 7 Gold; row: Force 4 for 5, Force 2 for 3, a 4-cost Holding producing 3).

Mendon:

> The player considers their hand and actions printed on personalities and their attachments. They make an estimate of how well they can defend against the opponent and what their needs will be for the next turn. Perhaps feinting at a defense and then countering is better, so I might lean towards the 4 force personality. But, if I thouhgt I could deal with the 8 force army, I would go for the economy, especially if my opponent looks like they might be stalling trying to claim provinces and I can get a lead by having more gold to hand. If, however, they have a bunch of dangerous personalities, despite their relatively low force, I would probably try to recruit as much force as I could, unless the low force personality had an ability that advanced my engine substantially or was powerful in its own right.

## 4. Round 4: spending in a won battle, the Imperial Favor, defending, the Honor deck

**Overkill.** Winning comfortably (Force 10 against 5, Strength 6) holding a bow, a destroy and a +2 Force action; two enemy defenders.

Mendon:

> I need to evaluate the potential of my opponent's personalities and units. A 5 Force swing is very achievable in a battle. I'd look at printed abilities in the units I'm facing and assess their ability to modify the outcome of the battle. If they have no printed abilities or keywords that represent a threat, then I might ignore them. In general, if I can bow a personality or a follower without additional cost, I would do so, even if I felt it wouldn't change the outcome of the battle. However, if either unit represented a material threat to the 5 point lead, I would destroy, then bow, then send home their units in order to ensure the victory. If they are bowed or otherwise unable to act, then I would likely ignore them even if they contribute force to the army unless the force changes and they represent a material threat.

**The Imperial Favor** (higher Family Honor, 3 Personalities to their 4, attack planned).

Mendon:

> Lobbying is a Limited action and cannot be performed after battle. However, I would not lobby for the imperial favor when I'm down a personality on a turn I plan to attack. If I had 4 personalities to 3 of my opponent, I'd definitely weigh lobbying for the imperial favor befofre attacking.

(The scenario's option "lobby after combat" was wrong, as Mendon notes.)

**Defend or let a province fall on the opponent's turn** (3 Gold untapped, two Battle actions, an Engage reaction).

Mendon:

> Evaluate if I have enough actions on the board to win the battle. Then, evaluate whether I have cards in hand that can produce the win. If I cannot produce a win under any circumstance, I let the province fall unless it's my last battle and I have to gamble.

**Honor deck** (Proclaim, hold Gold for an Honor-gaining Battle action, or lobby; two affordable). Mendon chose **prefer permanent Honor engines**.

**Guessing the opponent's hand** (planning an attack, 4 cards in their hand, 6 Gold untapped).

Mendon:

> I would make an informed guess about what is likely in their hand based on what I've seen the deck do. If they've played many strategy battle actions, I would lean towards them having up to 4 good battle actions on strategies. If I've seen attachments and action phase cards, I would guess a lower amount, maybe 2-3. I would assign a risk level to the unknown that is my opponent's hand and make a decision around that. If I hold 4 battle actions, I'm likely not worried. If I hold fewer and don't have many printed battle actions in play, I'd be more concerned. If I have no printed battle actions in play and have fewer than 4 battle actions in my hand, I'm much more worried and would want a large margin of victory before attacking.

**What to play in the Action phase** (an attachment giving +2 Force, a +3 Force Battle Strategy, a bow Strategy; an attack planned).

Mendon:

> In general, I would hold the strategies and play the attachments. However, your question is interesting: Let's consider a Battle/Open action that can be played in either phase. When do you use it's ability? If it bows someone in the action phase, it blocks their entire unit from assigning. This is tempting as it allows you to bypass that personality altoghether. On the other hand, bowing a personality in battle may lead to destroying them. The math has to be, is the personality dangerous enough that I don't want them assigned to oppose me at all? The answer might be yes if the opponent holds the imperial favor and that personality is the only possible defender. No presence means no Imperial Favor use and I can destroy the province. Similarly, if that personality has attachments that destroy cards, like ranged or melee attacks, I might not want them to be at the battle because it means I can't attack with vulnerable personalities, so bowing during the action phase is reasonable. However, if I think I can bait them into defending and then bow them, I would probably play for that so that I could remove them from the board. Even if I don't destroy a province, if I destroy several personalities or a large unit, I'm usually pretty happy because that turns into a future advantage. On the flip side, there are some cards that allow you to equip attachments during the attack phase. I would be inclined to use those if I have several personalities who can carry that item and the opponent will have to spend resources targeting one of them knowing that I may equip on a different personality. So, with spells, I'd be inclined to wait until the attack phase for some of them in order to leverage the element of surprise (the surprise being that I have the spell at all and that it is on a specific shugenja and it is being cast before the opponent can react to it). Other times, like Strength of the Tsunami, I might be tempted to attach it if I knew it would eliminate the ability of an opponent to destroy a key personality with a card that destroys personalities without attachments or melee or ranged attacks. For example, If I am holding On the Hunt and an attachment, I might wait to attach the item until the battle phase, especially after I've seen the opponent use their likely more powerful abilities to attempt to disrupt me. The ability to exhaust their resources before placing an attachment and then using its ability on subsequent turn is powerful optionality and the discount makes it even more interesting.

**A broken plan** (planned Force 9 into a defence of 7; their Open action bowed your best unit, Force 4). Mendon chose **re-count with the hand; attack only if it still wins**.

**A free ability when not attacking** (a free printed bow ability and a Holding that could bow to do the same; the target is a Force 3 Personality with a harmless ability). Mendon chose **use the free printed ability only**.

## 5. Round 5: no fixed margins, the Gold split, which province, the counterattack

**Margin numbers.** Asked for a margin by risk level; offered +1/+3/+6, +2/+5/+9, cards-not-Force, or his own.

Mendon:

> This is not perfectly formulaic. I would evaluate how much I can change force on both sides of the battle and evaluate how much I expect my opponent to do the same. So, if they have one large 7 force personality but I have multiple actions that will either destroy, bow, or send home/move away, I will attack with confidence. If they have 4 low force personalities, each with a good printed ability and attachments, I'd be much more concerned about attacking because of the probability that I will end up bowed, destroyed, and then defeated in the resolution. Cards don't have a fixed numerical value because a good card might remove a very powerful unit, making it have an outsized impact on the battle. On the flip side, I might only be able to make minor adjustments even with "good" battle actions available to me because the opponent is well kitted to negate or mitigate those actions. It would be an assessment each time based on what I know about my deck, the cards in play, and what I might guess the opponent is holding.

**Reserving Gold** (8 Gold of production, two Battle actions costing 2 and 3 in hand, a 5-cost Personality in the row; Gold spent in battle comes from Holdings that then cannot pay in the Dynasty phase).

Mendon:

> Make an assessment about the probability of needing to play both actions to win. If it is moderate to high, hold the gold to play the actions. Then, if I don't need to use both of them, purchase what I can with what's left. But, in general, I'd prefer to have the gold available to win the battle than to recruit the personality. More complicated is deciding whether to pay for an attachment or reserve gold for a different battle cost. attachments are often protective and keep personalities from being bowed or destroyed, so I'd need to evaluate the likelihood of needing to protect a personality with the attachment vs reserving the gold for the strategy that has a cost. There isn't a clear answer that determines which to choose. It would have to be an estimate of how much each card will move the battle in my favor. If my objective is to bait the opponent into defending against me, I might pass on the attachment to appear weaker. If I just want to win, I would evaluate which card makes the probability of winning greater.

**Which province first** (strengths 4, 6, 7, 9; each holds a card).

Mendon:

> Generally, I'd look to face up personalities with good abilities that they can afford. If it's a face up personality that costs enough that it precludes purchasing other cards, I would be inclined to attack cheaper provinces in an effort to encourage them to pour all of their resources into a single card, especially if I had a strategy to overcome its abilities. Otherwise, if I didn't have a strategy to manage the card, I'd likely attack that card. In general, I'd evaluate whichever face up personality in the provinces is most likely to be difficult to manage and attack that. Then, after that, I'd probably attack face down provinces over holdings in the hopes of depriving my opponent of a personality. If the holding is very good (allows them to draw cards or gain honor) then I'd attack that to block them from building their engine. With regard to variable province strengths, I'd attack the strongest province that I thought I could destroy. It usually only gets harder to destroy provinces, so all things being equal, the toughest province you can destroy means you have less work to do later in the game.

**Judging the counterattack** (your army will be bowed).

Mendon:

> Evaluate the army that they have and make some estimate of attachments that might come out from their hand. A larger hand is more dangerous than a smaller hand because it's more likely to contain things that augment their force. I would then plan around the cards I could bring out between then or during battle. So, recruiting personalities during the dynasty phase and any attachments that I am allowed to attach to them as an open or battle action to evaluate my own defense. Then, add in any battle actions that I Think might change the outcome of the counter attack.

## 6. Process decisions between rounds

- After round 5, asked whether to build slice 2 on a swing model (each side's Force can change by a range) or interview more: **build slice 2 now**.
- After the design sketch, asked how to proceed: **build the first slice and keep interviewing as it needs data** (over interviewing more first, or writing the design down first).

## 7. Follow-up commentary on discarding (after the first smoke results)

The planner discarded more province cards than the champion. Mendon first sent a message with typos and then a corrected one; both are kept. The assistant's own diagnosis was that the champion's rule discards any Holding it did not buy.

Mendon (first message):

> The bot already massively over discards. I didn't mention discarding much. If anything, having information regarding what is in the provinces helps plan for the next turn where discards hide that information. If two cats can be purchased from provinces on the next turn, it's generally worth keeping them off their personalities or a personality and a holding. If they're holdings, they might be worth keeping if there is a lead in personality counts against the opponent. Generally, I only discard cards that are prohibitively expensive, I'm blocked because the cards are generally too expensive, or when I need specific cards to build a superior army. 
>
> Troubleshoot the discard business, integrate my advice logic, and then return the smoke test.

Mendon (corrected):

> Slight correct to my message above: The bot already massively over-discards. I didn't mention discarding much. If anything, having information regarding what is in the provinces helps plan for the next turn where discards hide that information. If two cards can be purchased from provinces on the next turn, it's generally worth keeping them if they're personalities or a personality and a holding. If they're holdings, they might be worth keeping if there is a lead in personality counts against the opponent. Generally, I only discard cards that are prohibitively expensive, I'm blocked because the cards are generally too expensive, or when I need specific cards to build a superior army and they're not available.
>
> Troubleshoot the discard business, integrate my advice logic, and then return the smoke test.

A further message, again with a typo-laden first version, then a corrected one:

> The rule on prohibitive should not be that steep. If I'm forced to forgo buying a personality because of the cost of a holding (usually a holding costing 4 or more) I'm more inclined to discard it, especially early in the game. Mud game, I'm more likely to discard a holding if I'm digging. I always prefer to recruit over discarding, so I would vote all of my folks to recruit the highest priority cards then pay for anything else I can afford. If I can "clear" my provinces by recruiting all of them I generally will unless I must hold money in reserve for actions like equipping spells or items in battle or strategy cards costs and only if I think it is likely I'll need some or all of them.

> Slight correction: The rule on prohibitive should not be that steep. If I'm forced to forgo buying a personality because of the cost of a holding (usually a holding costing 4 or more) I'm more inclined to discard it, especially early in the game. Mid game, I'm more likely to discard a holding if I'm digging. I always prefer to recruit over discarding, so I would bow all of my holdings to recruit the highest priority cards then pay for anything else I can afford. If I can "clear" my provinces by recruiting all of them I generally will unless I must hold money in reserve for actions like equipping spells or items in battle or strategy cards costs and only if I think it is likely I'll need some or all of them.

Read as (assistant): discard only a card too dear for all production, a 4+ Holding that forces forgoing an affordable Personality (early in the game, or later when digging), or a non-Personality when behind in Personalities with none on
offer; otherwise keep it (the information plans next turn); recruit first, buy the whole row when the Gold's shape can pay for it, and hold Gold for Battle tricks only when an attack on the bot is likely.

## 8. Round 6: how a human spends abilities (volunteered, after the planner underperformed)

Mendon, after being told the planner was doing worse than the champion:

> This is interesting because a human player is going to look to use their abilities to the fullest extent that does not waste those abilities. So if you take a look at the Scorpion deck, every single personality has the bow ability from the towers of the yogo. Those are permanent abilities that can be reused if the personalities survive battles. A player will lean towards using printed abilities first that are in play. Then, they will utilise their hand cards. They will never pass and lose a battle. If they have cards or actions to play that could change the outcome of the battle. So, I would expect the strategy that I described to use its battle abilities better than the champion. The reason for this is that the champion often uses multiple actions where one would suffice, especially in that mantis deck where it overuses it's engage reactions at every battle.
>
> If it performed better, I would not be surprised if it played fewer actions because it's playing better targeted actions. For example, the champion bot is indiscriminate about how it uses bayushi waru. Instead of owing a personality with useful abilities that have not been used, bayushi waru's printed ability often bowels low value targets like followers without a printed ability.
>
> When planning, however, I would expect the planner to identify a personality that is particularly high value based on both force and or dangerous printed abilities. If the printed ability is exhausted, I would deprioritize bowing that person and instead focus on a person with a powerful ability or a very high force. I would treat personalities like tacticians as having a potential for to four more force as long as the opponent has cards in their hand and they have not used that personalities tactical ability. A smart player will focus on bowing or destroying or sending home personalities that have keywords or printed abilities that can be used in battle that have not been used that turn. For example, facing Allan's lion deck it is common for him to have multiple tacticians. Once one tactician has used its tactical ability, it can no longer use it. So unless it has a strong printed ability, in addition to tactician, it should be deprioritized and other tacticians should be prioritised over it until they have been incapacitated with a bowel destruction send home or have used their tactician abilities. This is also true in other decks that have keyword based abilities such as tattooed or magistrate or duelist or paragon or scout? Or in some cases even just samurai or courtier. Descriptive keywords are generally lower priority than printed battle abilities. But tacticians are always high priority as long as they have not used their tactical ability that turn.

Process decisions that followed: to learn whether an enemy card has spent a once-per-turn ability, **observe only, no engine change** (the bot reads the public battle action list); and to run the purchase/discard line and the triage line **in parallel on separate branches**.

## 9. Where each rule went, and what happened

| Interview rule | Implemented in | Paired result on 364 identical games (block 1700) |
| --- | --- | --- |
| Gold shape, bodies, Proclaim value, Holding efficiency, clear the row | `TrainingOpponent.Planner.cs` (purchase slate) | -2, neutral |
| Attack when the battle can be won counting the hand; which province; Gold set aside for tricks | `TrainingOpponent.Estimator.cs` (`PlannerAttack`) | about -14, off in the Contender |
| Keep what can be bought next turn; discard only what is too dear or a blocking Holding | `PlanDiscardScore` (`PlannerDiscard`) | about -11, off in the Contender |
| Hold Gold for Battle tricks when an attack on me is likely | `DefenseReserve` (`PlannerReserve`) | 0 |
| Target by what a unit can still do; unspent Tacticians worth up to +4; battle keywords over descriptive ones | `TrainingOpponent.Triage.cs` (`TriageTargets`) | +1 |
| Printed abilities in play before hand cards | `TriagePrintedFirst` | +2 |
| No spending on a comfortably won battle against units with nothing to spring | `TriageWonCap` | -3, off in the Contender |

Not built yet from the interview: contingency branches that wait on the opponent's Open action (Dainagon), mitigation order across all of the opponent's units, the light next-turn plan, bait plays (Retribution), spreading
attachments to avoid chokepoints, Honor, dishonor and enlightenment decks, and re-planning from the Border Keep refresh and after each battle beyond what the plan's inputs already trigger. Interview topics still open:
how to value one ability's swing (destroy against bow against send home against +Force), and how to read what an opponent unit's printed abilities can do to my units.
