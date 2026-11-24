# Every card shown to a player is held until they click

- **Status:** accepted (Mendon, 2026-11-24, issue #1319)
- **Changes:** how [PR #434](https://github.com/l5r-emperor-dev/l5r-emperor/pull/434)'s incoming shows arrive (no longer by forcing the Shown cards history open).

## Decision

After playing Scouting the Pass and never seeing the two cards of the Defender's hand, Mendon asked that each card shown to a player
use the Paused Animation presentation and wait for a click.

- Every card shown to this player is presented on its own, centred over the table, with a caption saying what showed it and where it
  was seen ("1 of 2" when there are several), and stays until they press Proceed (or Enter/Esc). It is held whether or not the
  Paused Animation preference is on, and the preference's Settings reminder is not shown for it.
- This covers the three engine records of a shown card, each read from the player's own view: a card the opponent shows
  (`ShownCards`, public), a card of another player's hand this player looked at (`HandSights`: Scouting the Pass, The Price of Honor)
  and a face-down province card this player looked at (`ProvinceSights`: The Otoro Estate and similar).
- Shown cards queue with played cards and are held one at a time; the local player's input waits, and so does the practice opponent.
- The Shown cards history keeps every opponent show. A show arriving while seated is marked read once presented; arriving at a seat
  through the local hand-off still opens the history, as before.

## Consequences

- Client only (`Main.ShownBeats.cs`, the `AlwaysHold` beat in `Main.CardPlays.cs`); Core views are unchanged. Only what newly appears
  is shown: a new match, a reconnect or a seat change starts from what is already there.
- `tools/check-shown-cards.sh` now also plays Scouting the Pass and The Otoro Estate through the engine and checks that each card is
  held, is not dismissed by the clock and is released by a click.
- Interactive looks (searching a deck, looking at the top cards to arrange them) keep their own panels: they are decisions, not shows.
