# Name the card that must take a limited first or additional action

- **Status:** accepted (Mendon, 2026-11-24, issue #1333; wording approved before implementation)

## Decision

When a player holds an action that a named or chosen card must take, a gold clickable box above the battle controls says so, with a
smaller line naming the card that caused it. Clicking it selects that card so its usual action buttons appear; the card is outlined on the
table. The opponent sees a matching line ("Your opponent's first action must be taken using "{Name}"."). Approved wording:

| Source | Headline | Smaller line |
|---|---|---|
| Naval Invasion, Scouting the Pass | Take the First Action Using "{Name}" | {Source}: {Name} must perform your first Battle action. |
| Shosuro Toson | Take the First Action Using "Shosuro Toson" | Shosuro Toson: he must perform your first Battle action. |
| (any of the three, no action available) | (as above) | {Name} has no Battle action available: you may only pass. |
| Spinning Heel Kick, Skipping the Puddle | Take an Additional Action Using "{Name}" | {Card}: {Name} must perform it. You may decline. |
| Takasho, Sugihara | Take an Additional Action Using "{Name}" | {Name} must perform it. You may decline. |
| Mirumoto Inokuchi | Take an Additional Iaijutsu Action Using "Mirumoto Inokuchi" | It must be Iaijutsu. If you decline, Inokuchi bows. |
| Fubuko | Play a Kiho from Your Discard Pile Using "Fubuko" | Fubuko must perform it; the Strategy is then removed from the game. |
| Honor Never Falls | Use One of "{Name}"'s Printed Abilities | Honor Never Falls: one additional Battle action from his own printed abilities. You may decline. |
| Mirumoto Kalen - exp | Use "Mirumoto Kalen"'s Second Ability | An additional action from his first ability. You may decline. |
| Two Hands | Take Up to Two Actions from "{Name}"'s Unit | Two Hands: any cards in his unit may act (2 left, then 1 left). Other actions give no new additional actions this battle. |
| Equip (Battle), Agasha Kurou | Use an Ability on "{Attachment}" | {Source}: an additional action from the card just attached to {Name}. |
| Unsafe Passage | Use a Base Ability of "{Follower}" | Unsafe Passage: even if it is bowed or has used the ability this turn. |
| Jade's Roar | Take the First Action: a {Keyword} Action | Jade's Roar: your first Battle action must be a {Keyword} action. |
| Rebuilding Kyuden Hida | Pass Your First Opportunity | Rebuilding Kyuden Hida: you may not announce a Battle action before the Defender's first opportunity. |
| Card-only grants (Learning, Daigotsu Bofana, Akodo Tezuka, Control the Field, Plain of Gold, Nitoshi, the Gozu search, Ivory Magistrate Outpost) | Play "{Card}" as an Additional Action | {Source}: an additional action to play it. |

With one Two Hands action left the headline reads "Take One More Action from "{Name}"'s Unit".

## Consequences

- Core: `MatchView.HeldActions` (public, identical for both seats) lists the first-action limit and the held additional actions grouped
  with a count, with performer, scope cards, keyword, base-abilities-only, named abilities, from-discard and may-decline. Each
  `AdditionalActionGrant` records its `SourceCardId` (the card of the ability this player used last). No rules change.
- Client (`Main.HeldActions.cs`): the box and line; the battle controls area grows while a box is shown so Pass stays in view.
- Mendon's Scouting the Pass game (replay `practice-20261124-111309`, commands 85-89): the engine did give him the first action; the box now
  says whose it is, and that the Scout had none to take.
