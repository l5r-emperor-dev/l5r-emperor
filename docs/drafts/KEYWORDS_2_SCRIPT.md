# Keywords 2: draft script (not built)

## PROMPT FOR CLAUDE: read this first

Build the **Keywords 2** tutorial (curriculum id `keywords-2`, listed after Keywords 1 and not built yet) from the script below. Use the same method as Keywords 1 (`TutorialLessonKeywords.cs`, `TutorialContext.Reset` for each new table, `TutorialKeywordsTests.cs` for the play-through and the per-step rule checks).

1. Validate every sentence against `docs/rules/COMPREHENSIVE_RULES.md` and the cards' own text. Correct errors, keep the flow, and ask me about anything you cannot resolve.
2. Check `docs/decisions/` and `docs/rules/RULINGS.md` for rulings that contradict the Comprehensive Rules, and ask me to resolve each before building around it.
3. Validate that the engine does what the Comprehensive Rules say for each keyword, with a test assertion at the step that shows it.
4. **Destined** and **Reserve** are not in the repository's Comprehensive Rules. Their rules are the reminder text on the cards. Check `docs/decisions/` for the engine's rulings on them and ask me before relying on them.
5. Keep it short and punchy: show, don't tell. One or two sentences per step, then make the player do the thing and watch the result. Each table is set up with one `Reset`.
6. Run the project's start and finish workflow (`AGENTS.md`), and give me a launcher link.

Cards named below were picked from the catalogue and are unchecked. Swap any that is not wired, or does not behave as the step says, and tell me what you swapped.

---

## Table 1: what may enter play (you: Crane; opponent: Scorpion, passive)

1. **Keywords 2.** "Last time you learned what keywords do in battle. These keywords decide what may enter play, what draws cards, and what changes a fight." *Continue*

### Unique
2. Setup: a Unique Crane Personality (your choice of the catalogue) is in play. A second copy sits in your Province 1. Gold is plentiful.
   "This Personality is **Unique**. Try to recruit the other one." *Player tries to recruit the second copy; it is refused, and the reason shows.*
3. "A Unique card will not enter play for a player who already controls one with the same title. In deck building, it also limits you to one copy of the card." *Continue*

### Singular
4. Setup: Falling Rain Dojo (Singular) in play, a second in Province 2.
   "**Singular** looks the same, but the limit is one in play, not one in the deck." *Player tries to buy the second Dojo; refused.*
5. "You may put several in a deck. Only one can be in play at a time." *Continue*

### Loyal
6. Setup: Akodo Dairuko (Loyal, Lion Clan) in Province 3. You are Crane.
   "Dairuko is **Loyal**. Try to recruit her." *Refused: she will not join a player with another Clan alignment.*
7. *Reset the player to Lion (same Province, same Gold).* "A Lion player may recruit her. Loyal also stops her changing control to anyone who is not Lion." *Player recruits her.*

### Experienced
8. Setup: Yasuki Tsujiken (base) in play, his Experienced version in Province 4, an Experienced Unique personality as a second pair if the catalogue has one.
   "**Experienced** cards are better versions of the same Personality. Recruit one onto the base card." *Player overlays; the base card is replaced and keeps its attachments.*
9. "An Experienced version of a Unique Personality shares the title with the base, so you cannot have both in play as separate cards. Overlaying is the way." *Continue*

---

## Table 2: a duel (you: Crane Duelist; opponent: Lion, passive)

10. Setup: Doji Genshin (Duelist) in play unbowed, and an equal-skill non-Duelist Lion Personality. Duel already challenged; both sides have focused a card of the same value.
    "Doji Genshin is a **Duelist**. The two scores are tied." *Player clicks Continue to resolve the duel; Genshin wins the tie.*
11. *Reset to the start of the focus step.* "A Duelist's controller may discard a focused card and focus another before Focus Effects resolve." *Player swaps a focused card for a higher one; the score changes.*

---

## Table 3: battle (you: Spider; opponent: Crab, attacks you, one Province each)

12. Setup: Daigotsu Yamato (Brash), a Spider personality that is Overconfident, and Daidoji Tobei (Destined, a Crane Personality, in your hand to recruit). Opponent has one Personality at home and one card in hand.
    "Three keywords draw cards. Start with **Brash**: assign Yamato to attack." *Player assigns Yamato; the Defender draws a card and the hand count shows it.*
13. "The Defender may draw a card each time a Brash card is assigned to attack." *Continue*

### Overconfident
14. "**Overconfident** pays the other players when it enters or leaves play. Recruit the Overconfident Personality." *Player recruits; the opponent draws a card.* *Then it is destroyed: the opponent draws again.*
15. "Both times, every other player drew." *Continue*

### Destined
16. "**Destined** pays you. Recruit Daidoji Tobei." *Player recruits; the player draws a card.*
17. "A Destined card draws a card for you after it enters play, a small reward for a costly Personality." *Continue* (Check the engine's ruling and ask me.)

### Reserve
18. Setup: Bayushi Wateru (Reserve, Ninja) in your Province 1. A battle is on, with enemy units present.
    "It is the middle of a battle. **Reserve** lets you recruit a Reserve Personality right now, from your Province, if he would be opposed." *Player recruits Wateru as a Battle action.*
19. "Wateru also gives you an additional Battle action." *Player takes the additional action.* (Check the engine's ruling and ask me.)

---

## Table 4: Kharmic and Terrain (you: Crane; opponent: Lion, passive)

### Kharmic
20. Setup: two Kharmic cards in hand (Absolution is one), Gold available.
    "Every player can turn **Kharmic** cards into new ones. Pay 2 Gold and discard a Kharmic card." *Player uses the Kharmic ability; draws a card.*
21. "There is a second version for a Kharmic card in your Province: discard it and refill the Province face up." *Player does so.*

### Terrain
22. Setup: Crowded Streets in your hand, a battle at one of your Provinces, a Terrain in the opponent's hand if Deep Snows is wired.
    "**Terrain** decides where a fight happens. Put Crowded Streets into play at the battlefield." *Player plays it.*
23. "A Terrain belongs to neither army, so it is never an enemy or opposing card. It cannot be bowed, and Control the Board can destroy it." *Player plays Control the Board (Battle: Destroy a target Terrain); the Terrain leaves.*

---

## End

24. "That is every keyword in the Comprehensive Rules with a rule of its own. Select the Next Tutorial." *Tutorial Menu*

---

## Open questions for you

- **Destined and Reserve** are in the catalogue and engine but not in the Comprehensive Rules in the repository. Do you want me to teach the card reminder text as written, or should the lesson wait for the rules text?
- **Experienced** has several sub-rules (overlaying, alternate titles, Inexperienced). The draft teaches only overlay; say if you want more.
- **Duelist** needs a prepared duel. If a mid-duel table is too heavy to set up, I will use a short prepared position and say so.
