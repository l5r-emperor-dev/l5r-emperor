# Printed-action strip review — #1201

Alfredo approved concept A: numbered per-printed-action use status below the complete card/token/restriction assembly. Implementation branch `codex/1201-action-strip`, PR #1278, base `a5eb303c7568d5c1cb698cdf1859a99b5720b6d7` (includes merged #1200). No merge requested.

## Contract and authority

Core adds optional `CardView.PrintedActions`: ordered execution `AbilityIds[]`, label, timing and `Spent`. Only face-up in-play cards receive it, for both seats. Old payloads omit the field and show no strip. The engine uses the same once-per-game, period/count, shared-key, additional-use and Unsafe Passage rules already used for command validation. The client only presents status; UNUSED means a use remains, not legal now.

Numbering excludes audited trait reactions and non-action Focus Effects/before-resolution effects. One printed action can have several execution IDs. An explicit 92-key presentation alias list was checked against the corresponding catalogue text; unverified future keys remain separate. A shared usage key alone is not a printed-action identity (Brawl/Folklore are counterexamples). Timing/labels combine alternatives; legacy SpentAbilityIds retains all spent branch IDs. Selected Sakata, Kakihara and Otomo evidence was reviewed; the entire scan corpus was not independently re-audited.

The optional view field does not change commands, choices, state persistence, content IDs, seeded state, journal/replay input or digest meaning. No content/protocol version bump is needed for this additive projection. Existing server seat authorization and client tolerant view deserialization remain in place.

## Independent governance lenses

- Rules: found trait entries inventing extra numbered actions (Otomo), and one printed Reaction with two engine branches (Kakihara). Both fixed and rechecked; Sakata/Otomo regressions pass.
- Privacy: no concrete leak found. Found a vacuous hidden-hand regression; corrected by inserting the actual subject into the owner hand and checking owner status/opponent identity separately.
- Negation/edges: confirmed the projection follows current limit/waiver logic. Found branch IDs numbered as actions (Sakata) and documented the explicit alias inventory; blanket UsageKey grouping was rejected because distinct actions can share a limit.
- Client contract: found preview centering overflow and Region/host strip overlap. Fixed with a reserved preview wrapper, shared province footprint/footer offset and province/persistent-row height reservation. Focused recheck found no new concrete defect.

All roles read the skill/profile, ledger and relevant Core/view/version/fixture evidence. Privacy also inspected server authorization/reconnect, transport and AI readers. Rules inspected core rule sections, relevant rulings, Otomo/Kakihara decisions and selected scans. Client inspected the changed UI, persistent/province/preview layouts and transport handling. Edges inspected period/waiver/reset producers and catalogue alias text. None read every giant engine file, all catalogue scans or every replay; reviewers did not independently run full/native suites. Their findings are advisory evidence, not Mendon's acceptance.

## Validation

Pinned SDK 10.0.400 full required `tools/check.sh --skip-client` passes: 4,702 Core cases, 316 AI cases plus Python/server/transport/collection stages. Four focused usage-view cases pass, covering both seats, JSON roundtrip, hidden/out-of-play suppression, public-zone coverage, once-per-game, Repeatable, same-turn extra uses, Unsafe Passage, traits and aliases. Client compiles with zero warnings/errors; changed-file C# formatting passes.

The final native check passes at 1920×1080 normal motion and 1280×800 reduced motion: crowded upright/bowed/dishonored units, mixed-bow attachments, six token types, restriction seals, Region/host clearance, strip/face/rail/Unit Force non-overlap, native strip/rail input, both seats, enlarged-preview enclosure, real turn expiry and same-turn use restoration. Expanded-unit checks compare the opaque inspection assembly separately from the background table/hand. Native captures were visually inspected and are linked below. Versioned Windows package delivery remains in progress at this source snapshot. Prepared positions include six token types, mixed-bow attachments, restriction seals, grants, copies, Unit Force and crowded neighboring units. Layout/usage stresses are labelled fixtures; Hotako's restriction/use and command-driven turn expiry are real engine behavior. Restore action use applies the existing extra-use mark as a labelled review fixture while retaining the action restriction.

The native probe now waits for the actual hover preview rather than assuming a fixed frame count is long enough. Explicit `L5R_PROBE_NO_OS_MOUSE=1` tagged non-review capture modes can receive viewport input without desktop focus; normal play/review retains focus gating. This avoids concurrent Mac CI windows invalidating native input evidence.

[Native evidence](../screenshots/printed-actions/README.md).

Human Windows graphical/rules acceptance remains pending. Publication and automated checks never accept or merge the change.
