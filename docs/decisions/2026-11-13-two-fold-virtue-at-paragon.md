# Two-Fold Virtue creates its Personality where the Paragon is

Date: 2026-11-13. Issue #1224. Ruling by Mendon (`mdornbrook`). Accepted.

## Ruling

"Two fold virtue should create the personality at the battlefield where the targeted paragon is located."

Two-Fold Virtue (oracle-l5r-9939) prints no location for the Personality it creates. The engine had created it at home. It is now created at the targeted Paragon's battlefield. As a Battle action, its target is always at the current battlefield, so the Personality joins that battle on its controller's side.

## Engine

`CreateTwoFoldVirtue` (src/L5R.Core/EmperorP5Zones.cs) passes the Paragon's battlefield to `CreatePersonality`. Regression: `TwoFoldVirtueCard` in tests/L5R.Core.Tests/EmperorP5Tests.Zones.cs.
