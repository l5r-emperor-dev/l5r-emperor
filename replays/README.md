# Practice replays

Every practice game kept for study (the Conan bot work, issue #1206, and its decision test set) lives here, in one place (issue #1262).

- `games/` holds the raw exports, exactly as the client's **Menu → Export practice replay** writes them. Each export carries everything the game needs: the content version, the seed, both players' full deck lists (`customDecks`) and every command in order. Editing or deleting a deck file never changes a replay, because the engine rebuilds the game from the lists inside the replay.
- `index.json` lists every game with its SHA-256 and the facts the engine reaches when it replays it (commands, turns, winner, each deck's stronghold, size and a fingerprint of its list). `note` is free text for the player ("experimenting with X", "misplayed turn 8", the bot level); notes are kept when the facts are refreshed. `retired` lists games taken out because the current rules refuse them, with the reason.

## Adding games

Agents follow the repository skill `upload-replays` (`.claude/skills/upload-replays/SKILL.md`). By hand:

1. Copy the exports into `games/`. Names are lower-case letters, digits and hyphens (`practice-20261113-183409.json` as exported).
2. From the repository root: `bash -c 'source tools/env.sh && dotnet run -c Release --project tools/ReplayCheck -- --write-index'` adds them to the index and replays them.
3. Optionally add a `note` to each new entry, then commit `games/` and `index.json` together.

## What the checks prove (`.github/workflows/replays.yml`)

1. **Plain files** (`tools/replays/check_replays.py`, Python, no build, seconds): strict UTF-8 JSON with no duplicate keys or non-standard numbers, bounded size and nesting, exactly the export's fields and types, strings in a narrow character set (no paths, addresses or links), plain non-executable Git files, and an index entry with a matching SHA-256 for every file.
2. **Legal games** (`tools/ReplayCheck`, the engine itself): each game is set up from its seed and both decks, with deck legality checked as practice checks it, and every command is executed; a refused command fails the check with its number and the engine's reason. The facts must match the index.

Only games the current rules accept are kept. The checks also run when the engine (`src/L5R.Core`) changes: if a rules fix makes a recorded game illegal, the same change retires it (remove the file from `games/`, add it to `retired` with the reason; its last commit keeps it). A change to the replays alone does not run the desktop test matrix.

The repository is public: replays hold card names, choices and seeds only. Stage 1 refuses any string that looks like a path, an e-mail address or a link.
