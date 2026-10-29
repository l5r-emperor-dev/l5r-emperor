# Tutorial (issue #935)

Play → Tutorial teaches the game by playing it, offline: you are the **pinned Lion deck** against the **pinned Crane deck** (`content/published/lion-crane-v1.json`), for the first turn and a half of a game (59 steps, ending on your destroyed Province). Illegal clicks are locked out.

## Lessons

Every lesson is a `TutorialLesson` (`TutorialLesson.cs`): a `TutorialSetup` (each side's clan, Stronghold, deck tops, Honor and cards at home, plus who goes first), its steps, and a `TutorialFoeScript` saying what the opponent does when a step lets it play (which Personalities it recruits with the Clan Discount, what else it recruits, which abilities it uses on which target). `TutorialLessons.All` lists them; nothing is locked. The Play → Tutorial menu lists every entry of `TutorialCurriculum` by track (the readable copy and the task checklist, #1035, are in the curriculum doc); an entry whose lesson is not built is a dimmed placeholder showing its brief. To add one, write its setup and steps and list it there. The lessons still to write are in [TUTORIAL_CURRICULUM.md](TUTORIAL_CURRICULUM.md).

## How it works

- **The real engine plays the lesson.** `MatchEngine.CreateTutorial(setup)` builds a prepared table (each side's pinned deck, with the cards the lesson meets put on top in the order it needs them) and stops before the first turn. Nothing in the rules changed.
  Lion has the higher starting Honor (7 against 6), so you go first; Crane, second, starts with the bowed Bamboo Harvesters.
- **The engine can be asked to stop.** `TutorialHoldAt(phase, player)` makes the engine stop when it has just reached the Straighten phase (after its cards straightened), the Events phase (after the Provinces were revealed) or the End phase (before the draw). `TutorialResume()` carries on. Tutorial-only; no rules change.
- **The words are in the script.** `src/L5R.Core/Tutorial/TutorialLessonOne.cs` is an ordered list of steps. A *note* has a button; a *move* names the one move it waits for (`Allow`), optionally the parameters it must have (`Accept`: the Gold sources, the unit equipped) and the provinces the table may offer (`OnlyProvinces`).
  What a step does to the table when it is done (`Then`) says when a hold is lifted and when Crane plays; highlights (`Highlight`: cards), `Glow` (the phase bar, the battle bar, your hand), `ShowDefinitionId` (a card held large in the middle of the table) and `Slow` (the presentations the step starts run four times as slow) are all per step.
- **`TutorialRunner` narrows and plays the script.** It switches off every option the step does not ask for (the table then shows them unavailable), refuses anything else before the engine sees it, and plays Crane's side. Crane only answers on its own: passes, defends the Province you attacked. Everything else it does (declining its attack, recruiting Daidoji Kenshi and the farms, Never Afraid) is something a step asks for in `Then`, so the lesson can stop between its plays.
- **Back.** The runner keeps every button and move you made. `Back()` starts the lesson again and plays all but the last of them: the engine is deterministic, so that is the scene as it was. The coach panel's *Back* button does this.
- **The client only presents it.** `MatchSession.StartTutorial` hands the table the runner's options; `TutorialCoachPanel` shows the note in a column at the left of the table; `CardTile.Pointed` pulses on the cards the note names; `TutorialGlowFrame` (`Main.TutorialStage.cs`) pulses around a part of the screen; the Show flow, the slow Province layout and the four-times-slower reveals are in `Main.TutorialStage.cs` and `Main.CardPlays.cs`. The Title menu entry is `TitleMenu.Tutorial`.

## The Events, Regions and Celestials lesson (issue #1074)

`TutorialLessonDynasty.cs`, lesson id `dynasty-deck`: you play the Unicorn Maidens against Mantis Manipulation (stronghold, Honor, cards at home and deck tops come from its `TutorialSetup`; the rest of each deck is filler).
It uses two more tutorial-only stops in the engine: `TutorialHoldReveal(player, province, after)` stops the Events phase before a province's card is turned over (`after: false`) or with it face up and not yet resolved (`after: true`), and a revealed Celestial that
enters play outside the Events phase (Border Keep's refresh) honours the same stop. The Mantis player's Border Keep refresh is played by the script (`TutorialContext.FoeBorderKeep`). Dark Experiments may name any Mantis Province but the leftmost, where a
Mantis Region attaches later in the lesson (a Region attaching to a Dark Experiments Province is destroyed).

## The Spells and Shugenja lesson (issue #1076)

`TutorialLessonSpells.cs`, lesson id `spells`: Phoenix (Library of Rebirth, 10 Honor) against Scorpion Lethargy (The Towers of the Yogo, 3 Honor), from your Action phase into the Scorpion's attack on your Province 1. The lesson's `Opening` draws your six Spells and starts the turn before the first note, and `TutorialKeepFaceDown` keeps Agasha Kodo and Asako Megu face down through that Events phase. The Scorpion's side is scripted with `TutorialFoeScript.AttackProvince` (it attacks that Province with everyone and fights there) and `Performers` (Bayushi Hurunayi, not another Scorpion, uses the bow The Towers of the Yogo grants). On the opponent's turn the runner no longer passes a Reaction of yours that the current step asks for (Shiba Sansesuke's). While a lesson's coach column is showing, a battle's placards sit at the foot of that column and the note ends above them, instead of covering it.

Corrections to the original script, checked against the comprehensive rules and the cards: the two Equip abilities are player abilities, not printed on Spells; targeting is not a cost (CR, Costs); "As a Focus Effect" works only when the card is focused in a duel; Library of Rebirth's Reaction produces 4 Gold, it does not reduce the cost, and is once per turn, so the Dynasty phase is guided (Whispering Archive, then a Silver Mine, the Stronghold kept straightened); The Earth's Hunger bows Isawa Tomohiro, who is not Earth (Mendon chose to keep that and teach it); Spells are described with the rules' blue background (Mendon's choice).

## The Attachments lesson (issue #1077)

`TutorialLessonAttachments.cs`, lesson id `attachments`: Scorpion Lethargy (you, The Towers of the Yogo, 3 Honor) against Phoenix (Library of Rebirth, 10 Honor), from your Action phase into your attack on Province 1. The `Opening` draws your five attachments and the Phoenix's six Spells, keeps every Province face down and starts the turn. The Phoenix plays only when a note asks it to: `TutorialContext.FoeUse` takes one named move (an Equip (Battle) paid by exactly the sources the note names, a Spell's ability, Library of Rebirth's straighten), `FoeDeclineAdditionalAction` declines the optional additional action Library gives, and `FoeDecide` answers "You may move him home". The Phoenix pays Strength of the Tsunami with Library's Reaction plus its Border Keep and Large Farm, and The Swell of Strength with the Silver Mine, so its Stronghold stays straightened and can use its own ability (a bowed card's abilities can't be used).

Corrections to the original script, checked against the comprehensive rules and the cards: Clan Standard was not in the hand and the five attachments cost 16 Gold against the 15 the table made, so a Large Farm was added to the Scorpion table (Mendon's choice) and every attachment is paid by exactly one named source; Spells have no Force or Chi bonus (Strength of the Tsunami's +6F is its text); there is no "Battle Phase" (a Spell attaches during a battle with Equip (Battle), which needs an unbowed, opposed Shugenja); the Follower is attached before Spells are introduced; the second ability of Strength of the Tsunami, not the first, is the one that works while it is bowed; Bayushi Hurunayi's Force before The Swell of Strength is 9 (Clan Standard's +1F); a Personality's unbowed attachments' abilities are usable while he is bowed (RULINGS, 2026-10-12).

## Writing or changing the lesson

1. Pick cards from `TutorialCards.cs` (add constants there; the test checks every number the text quotes against the catalogue and the engine).
2. Add steps in order in `TutorialLessonOne.cs`. Quote only what the Emperor Edition core rules (`docs/rules/EMPEROR_EDITION_CORE_RULES.md`) say. Say which controls to click: the table's own buttons and cards are what a player uses.
3. Extend `tests/L5R.Core.Tests/TutorialTests.cs` and run `dotnet run --project tests/L5R.Core.Tests -- --filter tutorial` (headless, seconds).
4. Add the new move steps' clicks to `TutorialUiScripts` in `Main.Tutorial.cs` and run `bash tools/check-tutorial.sh`, which plays the whole lesson through the real controls.

## The lab

```
godot --path src/L5R.Client -- --tutorial-review                                       # open the lesson directly
godot --path src/L5R.Client -- --tutorial-review --tutorial-step=incapacitated --capture=/absolute/out.png   # play to a step, save the view
godot --path src/L5R.Client -- --tutorial-review --tutorial-step=recruit-hachiro --tutorial-script="select:Matsu Hachiro;click:Recruit"  # try clicks and print the controls
godot --path src/L5R.Client -- --tutorial-review --tutorial-ui-check                    # the full UI walk (tools/check-tutorial.sh)
```

## Not done yet

Only the first turn and a half. The Rings/keyword notes of the earlier lesson are gone. A final lesson that unlocks the table and plays out a full game against the bot is not built.

## Where a finished lesson returns to

A lesson that ends (its last button) or is left from the table menu returns to the menu list it was started from: lessons of Level Up Your Play (or Building a Deck) return to that track's list, and The Basics, a track of one lesson, returns to the Tutorial track list. `TutorialCurriculum.TrackOf` decides it; every new lesson inherits the behaviour by being listed in the curriculum. A step's `animateOnly` limits which cards' reveal animations play while it is showing (the Events lesson's Celestial step animates only Suitengu's Blessing).
