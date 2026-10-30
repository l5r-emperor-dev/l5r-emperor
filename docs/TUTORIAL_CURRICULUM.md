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
| Keywords 1 (`keywords`) | Functional vs Non-functional Keywords: Clan alignment (Clan discount, Proclaim, Clan allegiance), Samurai, Courtier and Shugenja (Seppuku, Spells), Kensai, Cavalry, Naval, Tactician, Elite, Conqueror and Stalwart, on three tables: Crane against Scorpion, Spider against Unicorn, Spider against a lone Shiba Jikaro (built, issue #1078). |
| Keywords 2 (`keywords-2`) | Unique, Singular, Loyal, Experienced, Duelist, Brash, Overconfident, Destined, Kharmic, Reserve and Terrain (listed, not built; draft script in `docs/drafts/KEYWORDS_2_SCRIPT.md`). |
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

## The Keywords 1 lesson (issue #1078)

`TutorialLessonKeywords.cs`, lesson id `keywords`. Keywords need a different table for almost every topic, so the lesson sets the table up three times with `TutorialContext.Reset(setup, opening)` (the runner swaps in a new engine; `MatchSession.RefreshLocal` follows it and the table redraws as for a new match; Back replays the lesson, resets included). Table 1: Crane (you, Shinden Asahina, 8 Honor) against Scorpion, with Doji Dainagon, Asahina Konomi, Daidoji Kenshi and Kakita Maratai in play; `TutorialContext.Dishonor` makes all four dishonorable and `PutInPlay` brings Konomi back with Scent of Dishonor. Table 2: Spider (you, The Spider's Web, 10 Honor: Keep of the Dead cannot be used because its "You do not lose Honor from Spider Clan and Fate cards you own" negates A Paragon's Strength's Honor loss) against Unicorn (Plains of the Maiden), one attack on Province 1; `FoeTacticalAdvantage` has the Unicorn discard a Focus to give Moto Taha +5F. Table 3: Spider against a lone Shiba Jikaro, with two Paid Offs.

Corrections to the original script, checked against the comprehensive rules and the cards: Infantry is not a keyword (a unit that is not Cavalry is Infantry); the assignment order is Infantry Maneuvers (attacker, then defender: Cavalry may assign too) and then Cavalry Maneuvers (attacking, then defending Cavalry); Naval counts Naval cards, Followers included, not only Personalities; A Paragon's Strength needs a Paragon (Hotako is one), and its Naval first action must be his; Unorthodox Attack (Elite) is played before the pass, because the battle is over once both pass; Conqueror straightens the unit so it does not bow from the resolution, once per turn; Stalwart negates the first bowing each turn from effects of other players' cards; Scent of Dishonor does not bow an Air Shugenja; Imperial is a conditional Clan alignment when a player uses Journey's End Keep (Experienced), and a descriptive keyword otherwise. The Stalwart table gives the Spider one Shinomen Marsh and one Small Farm (the catalogue has no Shinomen Forest) so the Holdings are legal; a Conqueror-result step redraws the table.
