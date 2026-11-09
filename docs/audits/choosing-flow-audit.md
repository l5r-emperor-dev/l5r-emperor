# Choosing flow audit (issue #1004)

The choosing flow asks one question at a time (performer, province, player, target), advances on the first click, offers Back, and shows a bullet summary before Confirm. The client draws it in a handful of panels; `tests/L5R.Core.Tests/ChoosingFlowAuditTests.cs` routes every catalogue ability (2407 on 2026-10-17) to exactly one of them, so a new ability shape that no panel handles fails the test.

This is an audit by shape, not a playthrough of every card. Each panel is proven by a native check that drives real engine options through it; the per-card engine behaviour is covered by the Core scenario tests.

| Panel (client) | Abilities | What it asks | Native proof |
|---|---|---|---|
| Draft (`RenderTargeted`) | 1415 | performer, then province / player / target, then summary and Back | `check-direct-play-ui.sh` (Sound of Thunder performer, target, summary, Back; Spirit of the Berserker performer click and Back), `check-table-click-ui.sh` |
| Draft with a player (`RenderPlayerPicker`) | 9 | the player, after the performer | `check-direct-play-ui.sh` (player by plaque click and by card text) |
| Attack target (`RenderAttackTargets`) | 193 | performer, then the card / province / no target, summary at the top, Back | `check-attack-targets-ui.sh` (Tensin picks its performer first), `check-direct-play-ui.sh` (A Paragon's Strength: action, performer, target, summary) |
| Multi-target (`RenderMultiTargets`) | 184 | one group at a time, hands over when full, summary and Back | `check-multi-targets-ui.sh` (single group, mixed groups with summary and Back) |
| Province picker | 24 | the province, in the draft | `check-province-target-ui.sh` |
| Movement | 65 | the destination (already a single question) | `check-movement-ui.sh` |
| Draft with a choice of cost (`RenderDecisionChooser`, issue #1202) | the abilities with `CostOptions` (Governor's Court, Ancestor Fukui, Akodo Kamina, ...) | performer on its own screen with nothing pre-selected, then the target, then "how do you pay?" with each way listed once, Back at each | `check-variant-steps-ui.sh` (Governor's Court), `check-decision-chooser.sh` (Fukui, Kamina) |
| Draft with a choice after the target (`RenderChoicePicker`, issue #1202) | Stolen Merchandise (an option carries `ChoiceLabel`) | the Holding, then which of its abilities to copy, then summary and Back | `check-variant-steps-ui.sh` (Stolen Merchandise) |
| Support-card draft (`RenderSupportDraft`) | part of the draft count | performer first when several, then target, summary and Back | `check-direct-play-ui.sh` (shared performer step) |
| Reaction windows | 447 | unchanged: the reaction flow keeps its own panel | `check-reaction-flow-ui.sh` |
| Dedicated wizards (deck search, look, copy, either/or, opponent payment) | 70 | unchanged: each already asks one thing at a time | their own `check-*-ui.sh` scripts |

## Known limits

- Choices the engine asks after a card resolves (Captured Convict's gain or lose 2 Honor, which depends on the target) keep appearing after Confirm; moving them earlier needs an engine change.
- The reaction-window panel was not converted.
- Nothing here replaces a human playtest of look and feel; the native checks prove the order, the options offered, Back and the summary text.
