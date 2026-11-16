# Issue #1200: approved shared restriction rail

Historical local-build record. Current publication and refreshed validation: [16 November review](2026-11-16-1200-publication.md).

Base: `307a181d460784101ab94cbcd2b781b9f1c24af8`. Working branch: `codex/1200-restriction-rail`. GitHub main and open PRs were read through the connector before final validation; main was unchanged and open PRs #1146/#1122/#1111 did not overlap this task. CLI credentials are invalid; connector mutations returned HTTP 403. No PR, publication, notification or merge is claimed.

## Result and contract

The optional `CardView.Restrictions` contains engine-produced `CardRestrictionView` rows (`Kind`, `Label`, `Duration`, nullable `SourceName`, `SourceCardId`, `ByPlayer`, and `Protective`). Both seats receive only public in-play subjects and publicly visible source identities. Unknown historic source information stays null. Older JSON without the field retains null. New client views show the rail, bounded hover and inspector sentences; old views retain the previous action-lock seal. No new command, choice, state-persistence field or replay input. The ledger misclassifies record members under State as well as View; the record is presentation data only. No existing id or content semantics changed, so no content-version bump.

Projection covers active movement and assignment locks, straighten holds, attachment/host action locks, registered card and targeting restrictions, pending negation, Force contribution, Combat Segment shielding, stored immunities and supported resolution/Force protections. This is initial coverage of the requested high-priority systems, not exhaustive catalogue-wide coverage. Some historic records do not store provenance; some static immunity/global/player rules still require a subsequent projection pass. Missing metadata is not fabricated. Legality is unchanged.

## Review reconciliation

Four independent governance-lite reviewers read the working implementation: rules versus core rules, hidden information, negation/edge cases and client contract. Confirmed findings were fixed: hidden Hand sources even when FaceUp; view-time event emission; subject-specific attachment/host immunity; unseeded printed straighten holds; assignment versus movement-negation immunity; temporary battle-protection scope; static whole-unit protection on attachments; pending movement immunity; misleading fallback glyph; missing full-card explanations and obsolete probe assertions. Six focused regressions pass, including JSON preservation. These agent reviews are evidence, not human rules acceptance.

Coverage: rules reviewer read the governance profile, routed concerns, R-001/R-017/R-018, core duration/prevention/scope, P11/P12/performer decisions and relevant Core projection/effect excerpts; did not inspect every scan/effect. Privacy reviewer read projection, source state, server room/protocol, client session, AI context and transport excerpts; did not run reconnect/replay or all catalogue producers. Contract reviewer read view/client consumers, layout, protocol and transport excerpts; did not execute native tests or inspect the full fixture corpus. Negation reviewer checked the relevant movement/straighten/action/target/protection implementations and subsequent focused fixes. Reviews were of a changing tree, not an immutable final commit; later fixes were verified by the implementation agent. No clean exhaustive audit is implied.

## Native evidence

`tools/check-restriction-rails.sh` builds/imports, checks native flat rails at 1920×1080 and 1280×720, checks perspective, then runs the actual full-table Hotako action-lock scenario at 1920×1080 and 1280×800 with reduced motion. It exercises six token types, copy/grant overlays, Force standards, attack tabs, selection, dishonor, folded attachments and upright/bowed units. Geometry checks reject rail/rail, rail/exposed-face, attack-tab and window collisions. Full-table checks cover native restriction clicks, hover, attachment inspection, both seats and expiry, with crowded upright/bowed captures and polygon checks.

Final native suite passed in all five modes; build reported zero warnings/errors; six focused regressions passed. Supplementary SDK 10.0.401 full checks passed (4,686 Core and 204 AI cases plus Python/server/transport/collection checks). Native captures and logs are under `artifacts/checks/restrictions/`; launcher build/run reached `ACTION_LOCK_REVIEW_READY` and exited 0, log `artifacts/checks/restriction-launcher.log`. Exact limits are recorded in the task handoff. Automated fixtures use synthetic layout stresses; Hotako's action refusal is real engine behavior. Windows native graphical acceptance and private preview publication remain outstanding. The pinned SDK 10.0.400 stalled on this machine; supplementary repository checks use 10.0.401 via a temporary local script with only its version guard changed. That is not a pinned-SDK pass.

## Launch

`Review Card Restrictions.command` and the generated `L5R Card Restrictions - Review.app` open the crowded review fixture using checked-in fixture scans. The normal `Play Latest.command` still hits the existing authenticated bulk-reference-art download prerequisite on this machine; it was not presented as a working full-game launcher. No old downloaded release contains this change. See the task handoff for publication limits.
