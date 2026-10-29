# Tutorial curriculum (issue #935)

**Where to pick this up:** the tracking issue is [#1035](https://github.com/l5r-emperor-dev/l5r-emperor/issues/1035), a checklist with one task issue per lesson. Play → Tutorial lists every lesson below; a built one starts, the rest are dimmed placeholders. Hovering or focusing an entry shows its subtitle (the part after the colon below). A long list (Level Up Your Play) scrolls with the mouse wheel or by dragging the bar at its right. The menu's own list is `src/L5R.Core/Tutorial/TutorialCurriculum.cs`: when you add a lesson, give it the same id there and in `TutorialLessons.All`.

The list was reset on 2026-10-21 by Mendon: the earlier 38-entry draft was dropped and only the first lesson kept; this is the new framework. **Everything is unlocked from the start.** Each lesson stands alone, so each brings its own table (`TutorialSetup`: decks, Stronghold, Honor, cards at home, deck tops, who goes first; see [TUTORIAL.md](TUTORIAL.md)). Budget: 15-30 steps, 3-5 minutes of play, headless play-through of 2 seconds or less; stage the position instead of playing up to it.

## Playing the Game

A track with a single built lesson has no list: choosing it starts the lesson.

| Lesson (id) | Subtitle | Status |
|---|---|---|
| The Basics (`first-turn`) | Take a first turn as Lion against Crane: the phases, recruiting, Proclaim, equipping, attacking and destroying a province. | built (PR #1025) |

## Level Up Your Play

| Lesson (id) | Subtitle |
|---|---|
| Events, Regions, and Celestials (`dynasty-deck`) | The rest of the Dynasty Deck: Unicorn Maidens against Mantis Manipulation, one turn each. Dark Experiments, Natural Aviary, Ebisu's Honesty and the Mantis Border Keep refresh (built, issue #1074). |
| Recruiting Personalities (`recruiting`) | Clan Alignment, Honor Requirement, Recruitment Effects, Overlays: Crab Control against Classic Crane at the Dynasty phase. Gold Cost and Honor Requirement (each swells on the cards, over and over, while its note shows), a Loyal card that will not join, Proclaim, the Clan Discount, an Overlay, then the Crane showing Clan Allegiance against an Honor Requirement (built, issue #1075). |
| Spells and Shugenja (`spells`) | Attaching and using spells in the Action and Battle Phases: Phoenix (Library of Rebirth) against Scorpion Lethargy. Only a Shugenja takes a Spell, Equip (Limited) in your Action phase, element keywords, cost and effect; then you defend, and Equip (Battle) with its additional action destroys Bayushi Waru (The Earth's Hunger bows Isawa Tomohiro, who is not Earth), Shiba Sansesuke catches the Scorpion's bow, and Library of Rebirth pays for Strength of the Tsunami (built, issue #1076). |
| Attachments (`attachments`) | Followers, Items and Spells: Equip (Limited) and Equip (Battle), Honor Requirement, Weapon and Armor, and the abilities on unbowed attachments: Scorpion Lethargy against Phoenix. A Follower, a Weapon, an Armor and a General Item in the Action phase, then a battle where the Phoenix attaches Spells and a bowed Personality still uses his unbowed Nightingale Blade (built, issue #1077). |
| Keywords (`keywords`) | Functional vs Non-functional Keywords |
| Traits and Actions (`traits-actions`) | Timing and limitations of Traits and Actions |
| Cost and Effect (`cost-effect`) | The difference between paying costs and applying effects |
| Special Actions - Attacks (`special-attacks`) | Melee and Ranged Attacks |
| Special Actions - Duels (`special-duels`) | Refusability, the Duel flow, and Focus Effects |
| Special Actions - Dishonor (`special-dishonor`) | Dishonorable status, Dying Dishonorable, Dishonorable Dead, Rehonor, Seppuku |
| Rings and Enlightenment (`rings`) | How to play a ring, How to win an Enlightenment Victory |
| Reactions (`reactions`) | Timing, Triggers, Effects |
| Imperial Favor (`imperial-favor`) | Lobbying and Using the Imperial Favor |

## Building a Deck

| Lesson (id) | Subtitle |
|---|---|
| Card Library (`card-library`) | |
| Deck Builder Overview (`deck-builder`) | How to use the Deck Builder |
| Anatomy of a Deck (`deck-anatomy`) | What is required and what is good practice |
| Using Your Designed Deck (`using-your-deck`) | |
| Using Settings (`using-settings`) | |

The Building a Deck lessons that teach a screen (Card Library, Deck Builder Overview) run in the app rather than in a match; how they are presented (coach column or guided tour) is not decided.

Every built lesson ends on a note with a **Tutorial Menu** button and a **Next Tutorial** button (the entry the list above shows next, across tracks), and says "Select the Next Tutorial to continue learning." Next Tutorial on a lesson whose successor is not built yet opens that track's list with the placeholder selected.
