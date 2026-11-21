# Reactions are not tied to presence unless their text says so

Date: 2026-10-31. Issue #1152. Ruling by Mendon (`mdornbrook`).

## Ruling

- A Reaction may be taken from anywhere. It needs a card at the battlefield only if its own text requires it (Utaku Ryoko: "After engaging at Ryoko's battlefield"; Khol Regulars: "If this card is at the current battlefield").
- An Interrupt is a Reaction, taken in the Reaction window its text names (usually "before an action's effects resolve"). The Comprehensive Rules tie Interrupts to the Rules of Presence and Location (Combat Segment, Presence and Location); that counts as text on the card, so a card printed with an Interrupt designator needs a unit of its player at the battlefield and, if it sits on a Personality or attachment, that card there.
- Engage abilities are Reactions taken at engagement. Printed on a Personality or attachment, the card must be at the engaged battlefield; Regions, Holdings and Strongholds name their own battlefield. R-064 stands unchanged, with its Home, Absent and named exemptions.

## Effect on earlier rulings

- R-090 (every Reaction in a battle follows presence) is reversed by R-092.
- R-088 (Attack reactions need presence) keeps its Attack-targeting parts; its presence requirement now applies only to Interrupts (Unholy Strike, Steal the Candle's Flame) and to Khol Regulars' own text. Fallow Lands still reacts only at its own province.
- R-089 (one use per source per trigger) is unaffected.

## Engine

`BattleReactionPresenceRefusal` (CardAbilities.cs) returns no refusal unless the source card's text has an Interrupt designator (`PrintedInterrupt`). Tests that #1133 changed for presence were updated: Tamoko, the Attack presence matrix, Strategy Reactions, Quagmire (adjacent province) and The Tower of Vigilance (its "if you have any units" is a condition of its effect).
