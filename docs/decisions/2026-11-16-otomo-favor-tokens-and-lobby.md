# Lobby is barred only by holding the Imperial Favor; Kyuden Otomo's Favor tokens show on its rail

Date: 2026-11-16. Issue #1272. Ruling by Mendon (`mdornbrook`). Accepted.

## Ruling

"Once there are favor tokens on Kyuden Otomo, the player should be able to continue lobbying for the imperial favor. The only time they will be barred from lobbying for the favor is if they are currently holding the imperial favor "card" instead of the imperial favor tokens on Kyuden Otomo."

This agrees with the Comprehensive Rules, The Imperial Favor: a player takes control of the Favor "whether it is controlled by another player or by no player" through lobbying. A player who already controls it has nothing to take.

## Engine

- Lobby (`lobby`, and Dainagon's `dainagon-lobby`) is refused while the acting player controls the Imperial Favor: "You already hold the Imperial Favor." Before this change the engine let a player lobby for a Favor they already held.
- Favor tokens on Kyuden Otomo - exp (oracle-l5r-4646) are not the Favor. They never bar Lobby. The full loop already worked and is now covered by a test: Lobby, discard the Favor for a token with Otomo's Reaction, and Lobby again on the next turn.
- Regression: `KyudenOtomoLobby` in tests/L5R.Core.Tests/EmperorStrongholdBatch1Tests.cs.

## Client

The token rail (`CardTokenRail`) now also shows for a card in the Stronghold zone, so a Stronghold that gains tokens, such as Kyuden Otomo's Favor tokens, shows them on the table. Holdings at home already had the rail. The Favor token uses the existing fan icon in `Art/card-token-source.png`, so no new image was needed.
