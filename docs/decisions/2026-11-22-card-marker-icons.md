# Card markers are separate icons outside the card

- **Status:** accepted (Mendon, 2026-11-22, issue #1314)
- **Supersedes:** the rail hardware of [token rails](2026-09-09-token-rails.md) and the status track of #1279; the icon art is unchanged.

## Decision

After reviewing #1311 in game, Mendon chose the permanent-change seal's style for every public card marker:

- The status/restriction rail and the token rail are removed. No rail hardware is drawn.
- Each marker is its own icon on the card's exterior: the permanent-change seal first, then one icon per restriction kind
  (Alfredo's move, straighten and protection sprites; the action-lock seal for every other restriction), then one per token group
  (the approved token medallions, with "×N" when a group holds more than one token).
- Icons stack along the card's left edge, five to the card's height, and start another column outward when full.
- They follow the card when it bows, lying along its near edge with extra rows below.
- Hovering an icon explains that marker (the status text, duration and source supplied by Core, or the token count and sources); clicking selects the card.

## Consequences

- `CardTokenRail` is now only the layout holder (`Tracks`/`WidthFor` count columns of five) and takes no input itself; `CardIcon` is one marker.
- `RestrictionRail` keeps the shared status helpers and icon drawing.
- Board spacing still reserves the stack's width through `CardTokenRail.WidthFor`.
- Native checks: `tools/check-status-rails.sh`, `check-token-rails.sh`, `check-restriction-rails.sh`, `check-permanent-grants.sh`, `check-fallen.sh`.
