# Doji Dainagon's Open Lobby shares Lobby's once-per-turn use

Date: 2026-11-18. Issue #1086. Ruling by Mendon (`mdornbrook`). Accepted.

## Ruling

Asked while validating the Imperial Favor tutorial script: does "Dainagon may Lobby as an Open action" share the once-per-turn limit of the player's Lobby? Mendon chose the shared limit: it is the same Lobby ability taken at a different time, so one Lobby per turn in total.

This follows the Comprehensive Rules: Lobby is a player ability (Player abilities), an ability can be used once per turn unless it says otherwise, and Dainagon's trait changes when Lobby may be taken, not how many Lobby abilities the player has.

## Engine

- Before: `dainagon-lobby` was tracked on Dainagon's card and `lobby` on the player, so Dainagon could Lobby right after another Personality had, and the other way round.
- Now: `dainagon-lobby` is refused ("You have already Lobbied this turn.") once the player's Lobby is spent this turn, and using it spends the player's Lobby.
- Regression: `DainagonLobbies` in tests/L5R.Core.Tests/SecondCityFavorTests.cs (both orders, and a new turn resets it), and `RulesTheNotesState` in tests/L5R.Core.Tests/TutorialFavorTests.cs.
