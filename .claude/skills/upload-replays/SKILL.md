---
name: upload-replays
description: Upload Mendon's practice replay exports into the replay corpus (replays/games) and merge them. Use when asked to upload, add or commit replays, replay sessions or practice games.
---

# Upload practice replays

The corpus, its index and what its checks prove are described in `replays/README.md`; read it first. Follow AGENTS.md for the issue, branch, handoff and merge. The goal of a run: every new export is either in the corpus, replaying legally under the current rules, or reported to Mendon with the reason it is not.

## Steps

1. **Find the new exports.** The client writes `practice-<UTC>.json` to its user data folder: on Windows `%APPDATA%\Godot\app_userdata\L5R · Emperor\`, on macOS `~/Library/Application Support/Godot/app_userdata/L5R · Emperor/`, or wherever Mendon says he put them. New means named in neither `games` nor `retired` of `replays/index.json`. Done when you can list each new file with its date; if there are none, say so and stop.
2. **Open the task.** One issue per upload ("Replays: N practice games from <first>–<last date>"), branch `claude/<issue>-practice-replays` (Codex: `codex/...`) from current `origin/main`.
3. **Copy, never edit.** Copy each new export into `replays/games/` byte for byte under its exported name. A replay is evidence of a game: its content is never changed, trimmed or reformatted.
4. **Prove the files plain.** `python3 tools/replays/check_replays.py`. Before indexing, "not in replays/index.json" is expected for each new file; every other finding is a file that stays out: remove it from `replays/games/` and tell Mendon which file and what the finding was (a path, an address or a link in a string means personal data, so the file is never committed).
5. **Index and replay.** From the repository root: `bash -c 'source tools/env.sh && dotnet run -c Release --project tools/ReplayCheck -- --write-index'`, then the same without `--write-index`. Done when it ends "every card and every move legal".
6. **Retire what the rules refuse.** A refused game names its command and the engine's reason. Remove the file from `replays/games/`, delete its entry from `games`, and add `{"file", "sha256", "reason"}` to `retired`: the command number, the reason, and, when you can find it, the rules change that made it illegal (`git log --merges --since=<game date>` on `src/L5R.Core`, with merge times against the export's UTC timestamp). Run step 5 again until it is clean.
7. **Notes.** Ask Mendon once, listing the new games (date, his stronghold, the bot's stronghold, result), for a note on any of them: the bot level, a deck experiment, a known misplay. Write what he gives into each entry's `note`; leave the rest empty and carry on.
8. **Share.** Both checks again, then handoff, audit, commit `replays/games/` and `replays/index.json` together, push and open the PR: games added, games retired with their reasons, stage 1 and stage 2 output. A replay-only PR runs the two replay checks and the policy checks, not the desktop matrix.
9. **Merge.** When every check on the PR has passed (read the list; a failure or a pending check means wait or fix), and no `nightly-broken` issue is open, merge under Mendon's standing authority. Done when the PR is merged and the report to Mendon names the games added, the games retired and why, and the corpus total.
