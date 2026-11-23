---
name: friends-pack
description: Build and publish the L5R Emperor friends pack, a downloadable Windows ZIP and a parallel macOS ZIP from the same main commit, as one GitHub pre-release. Use when asked to build, rebuild, refresh or publish the friends pack, the share build or a download for friends.
---

# Friends pack (Windows and macOS)

A friends pack is one GitHub pre-release holding two downloads built from the same commit of `main`, so each player picks their system:

- `L5R-Emperor-Windows-<sha8>.zip`, built on this Windows machine;
- `L5R-Emperor-macOS-<sha8>.zip`, built by the **Friends pack (macOS)** workflow on the repository's Mac runners. Signing a Mac app needs Apple's `codesign`, so a Windows session cannot build it locally.

Always build both. Publish with only one of them only when Mendon says so after you tell him why the other failed. Packaging changes no game source, so it needs no issue, branch or PR. A change to the packaging tools does, under AGENTS.md. Publishing is outward-facing: when Mendon asks for the friends pack, that request is the go-ahead to publish it.

## Steps

1. **Source.** Build the current `main` only. Fetch, then check that the checkout is on `main`, at `origin/main`, with no tracked changes (`git status`; untracked deck lists such as `Crane deck.txt` are fine and stay untouched). On Mendon's Windows box, update with `.l5r-local/Refresh-L5R.ps1`. Note the full SHA and its first 8 characters. Check that CI passed on that commit (`gh run list --branch main --limit 5`) and that no `nightly-broken` issue is open. If either fails, tell Mendon before building. Done when you have a full SHA that equals `origin/main`.
2. **Windows build.** `bash tools/export.sh windows` exports and validates the game. `python tools/package-share-build.py` wraps it as `artifacts/share/L5R-Emperor-Play-Windows.zip`. Copy that file to `artifacts/share/L5R-Emperor-Windows-<sha8>.zip`. Then check what a friend gets: the ZIP passes `testzip()`, it has `L5R Emperor/Play L5R Emperor.cmd`, `L5R Emperor/READ ME.txt` and `L5R Emperor/game/L5R-Emperor.exe`, and once extracted to a temporary folder `game/L5R-Emperor.exe --headless --quit-after 3` exits 0 with no `ERROR:` in its output. Done when all three checks pass. Record the size and SHA-256.
3. **Draft release.** Run `gh release create friends-main-<YYYYMMDD>-<sha8>-r1 artifacts/share/L5R-Emperor-Windows-<sha8>.zip --draft --prerelease --target <full SHA> --title "Friends pack <D Month YYYY> (<sha8>): Windows and Mac" --notes-file <notes>`. `--target` must be the full SHA, because a short one fails with HTTP 422. Raise `r1` if the tag exists. Drafts are not visible to friends, so the release stays a draft until both downloads are in it.
4. **Mac build.** Run `gh workflow run friends-pack-macos.yml --ref main -f tag=<tag>`. Then find the run (`gh run list --workflow friends-pack-macos.yml --limit 1`) and wait for it with `gh run watch <id> --exit-status`. Run it in the background: it takes 20 to 60 minutes, longer when the backup Mac has to fetch. The workflow refuses a commit that is not on `main` and builds the release's own target commit. It exports and signs the app, starts it, packages it with ditto, then unzips the package, re-verifies the signature and starts it again. It uploads `L5R-Emperor-macOS-<sha8>.zip` and checks that GitHub's digest matches. If it fails, read its log (`gh run view <id> --log-failed`) and the `friends-pack-macos-logs` artifact. Fix the cause on a task branch (issue, PR, CI, merge), then build again from step 1 on the new `main`. If the Mac runners are offline (`gh api repos/{owner}/{repo}/actions/runners`), say so. Done when the run is green and the release lists both assets.
5. **Publish.** Check that the release lists both ZIPs and that the Windows digest equals your local SHA-256 (`gh api 'repos/{owner}/{repo}/releases?per_page=20' --jq '.[] | select(.tag_name == "<tag>") | .assets[] | [.name, .size, .digest]'`; the by-tag endpoint cannot see a draft). Then run `gh release edit <tag> --draft=false`. Done when the release page is public as a pre-release.
6. **Report.** Give Mendon the release link, each download's name, size and SHA-256, and what was and wasn't checked. Neither package was hand-played. No online multiplayer was tested. The Mac app is not notarised, so a friend has to choose Open Anyway the first time, as its READ ME explains.

## Release notes

Write the notes to a temporary file. They are public, because the repository is (docs/PUBLIC_REPO.md): no local paths, names or e-mail addresses. Include:

- **Which to download:** Windows, `L5R-Emperor-Windows-<sha8>.zip`. Mac (Apple silicon or Intel), `L5R-Emperor-macOS-<sha8>.zip`.
- **Install:** Windows: extract, then double-click `Play L5R Emperor.cmd`; on the unsigned warning, click More info, then Run anyway. Mac: unzip, open the app, then System Settings > Privacy & Security > Open Anyway the first time. Each ZIP's `READ ME.txt` has the full steps, where decks are saved and where the log is.
- **What changed** since the previous friends pack: the merged PRs since its target commit (`git log --merges --first-parent <previous target>..<sha>`), in plain player terms.
- **Source and validation:** the commit, the checks from steps 2 and 4, and the limits from step 6.

Earlier packs used `windows-main-...` tags and only a Windows download. Leave them as they are.
