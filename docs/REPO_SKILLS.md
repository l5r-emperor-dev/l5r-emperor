# Repository skills

Rung 5 of [the PR ladder](PR_LADDER.md), tracked in [#509](https://github.com/l5r-emperor-dev/l5r-emperor/issues/509), provides four project recipes; Rung 9 adds governance-lite. Open a session in this repository; invoke by name or ask for the described workflow. Repository skills add procedure, not permissions or rulings, and AGENTS.md remains the shared policy.

| Skill | Claude | Codex | Purpose |
|---|---|---|---|
| wire-card-wave | `/wire-card-wave` | `$wire-card-wave` | Verified printing/packager, feature-owned mechanics, manifest, tests, artwork and inventory |
| art-pack-release | `/art-pack-release` | `$art-pack-release` | Complete verified WebP pack and, when requested, versioned release (public on this repo; needs an owner decision) |
| card-pass-report | `/card-pass-report` | `$card-pass-report` | Reconciled coverage/blockers, confirmed readings, client contracts and evidence |
| ruling-interview | `/ruling-interview` | `$ruling-interview` | Focused questions and counterexamples leading to a recorded owner ruling |
| governance-lite | `/governance-lite` | `$governance-lite` | Four focused review roles and a diff-based client-contract ledger; see [governance guide](GOVERNANCE_LITE.md) |
| upload-replays | `/upload-replays` | `$upload-replays` | Mendon's practice replay exports into `replays/games`: indexed, replayed by the engine, refused games retired, merged after the replay checks ([replays/README.md](../replays/README.md), #1264) |
| friends-pack | `/friends-pack` | `$friends-pack` | The downloadable friends pack: Windows ZIP built locally and macOS ZIP built by the `friends-pack-macos.yml` workflow on the Mac runners, from the same `main` commit, published as one pre-release (#1317) |

Edit canonical files in `.claude/skills/<name>/SKILL.md` and their linked resources. Run `python tools/skills/sync_skills.py` to refresh byte-identical copies in `.agents/skills`; Windows checkouts need no symlink setup. Run `python tools/skills/sync_skills.py --check` for a read-only check. The normal tools/hooks test discovery validates names/references and rejects drift, so existing CI checks cover both discovery paths without a new job or dependency.

Codex repository discovery follows the [official OpenAI documentation](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills). The session must be opened inside the repository (or a repository subdirectory), not its unrelated parent folder. Reload/restart if an existing session does not notice newly installed skills. CLAUDE.md imports AGENTS.md; Claude's recipes live in its standard `.claude/skills` directory. No per-user installation or global configuration is changed by this PR.

The ruling interview is original project guidance using the one-question-at-a-time decision-tree pattern requested in the plan. The plan did not identify a specific Yuzu source revision; no third-party skill implementation was copied or represented as verified.

## Acceptance exercise

The acceptance target is a fresh session executing a two-card wave using wire-card-wave as its only recipe. Use an isolated scratch repository with reviewed synthetic catalogue/receipt/scan inputs; do not publish fixture cards, pretend they are historical printings, or upload an art release. Give the evaluator the skill, input IDs/printing and artifact destination, without the intended implementation. It may inspect the maintained files linked by the skill. Record its commands, resulting packager/provenance/manifest/tests/inventory, any recipe gaps, and successful actual Core discovery/execution. An unattended script alone does not establish that an agent could follow the recipe.

The acceptance record belongs in docs/decisions with exact results and limitations. After recipe fixes, synchronize both copies and rerun relevant checks. Production card-wave runs still need the full validation and publisher work described by the skill.

Reproduce the raw exercise environment with `python tools/skills/prepare_wave_exercise.py --output artifacts/skills-wave-exercise`. The preparer only copies the maintained dependencies and generates clearly synthetic catalogue/receipt/scan inputs; it supplies no packager, wave, rules implementation or expected test answer. It refuses existing output directories. Give a fresh session the skill and FIXTURE_INPUTS.md in that output, request local-only wiring and evidence, and prohibit production/GitHub mutations. The scratch copy supports offline .NET restoration; point PATH/DOTNET_ROOT at the pinned SDK. It omits the historical scan library, so its acceptance tests should focus on the new wave and do not claim a full production content/client run.
