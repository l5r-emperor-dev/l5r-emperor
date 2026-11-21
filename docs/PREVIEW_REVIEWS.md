# Playable preview reviews

Alfredo requested this delivery flow on 2026-09-18. Mendon uses Windows and primarily
Claude Code. Deliver a prepared, portable game and a targeted review notification;
do not ask either person to operate branches or build tools merely to playtest.
Alfredo delegates human preview/playability review to Mendon. This does not imply
Mendon's acceptance or replace the existing checks/review requirements for merging.

Current delivery candidate: [Approved Top Cards and updated UI review for Windows](https://github.com/l5r-emperor-dev/l5r-emperor/releases/tag/preview-pr434-659e015f0e-r2-windows).
This pins combined PR #434 at `659e015f`, including Alfredo's approved Top Cards
correction (`58456f72`), ghost-copy markers, revised Multiple Targets/Battle Progress,
Table Visibility and Opponent Payment. [Current delivery evidence](handoffs/2026-09-23-approved-ui-windows-review.md).
The older [9f8ef9f5 package](https://github.com/l5r-emperor-dev/l5r-emperor/releases/tag/preview-pr434-9f8ef9f5a5-r1-windows)
predates the final Top Cards correction; retain it only as historical evidence.
Packages remain immutable; publication does not merge the candidate.

## Mendon's path

1. Open the GitHub notification **“Alfredo has built … and would like your review.”**
2. Follow **Download the Windows preview** while signed into GitHub.
3. Right-click the ZIP → **Extract All**, then open **Start Review.cmd**. If the
   notification lists multiple ZIP parts, download every part and extract each into
   the **same destination folder**. Start Review verifies and assembles the complete
   game on its first launch; later launches reuse that prepared game.
4. Try the prepared scene. Close/reopen to reset it. Other views are in **Other previews**;
   **Open Full Game.cmd** opens the same build's normal menus.
5. Open **Send Feedback.url** or reply to the GitHub notification email. Describe
   what happened, the expected behavior and the scenario. Formal PR approval is a
   separate review action; neither a comment nor launching the game accepts it.

The game, .NET runtime and artwork are included. No development tools or runtime
download are required. Keep the extracted folder together. The private executable
is unsigned, so Windows may show a first-run warning. Do not disable Windows
security or run as administrator to make a preview work; investigate a blocked or
failed launch. The package's SHA-256 and exact source commit are in the notification.

Preview settings/decks are isolated under `%APPDATA%\L5R\Previews\<package-id>`.
Logs and initial table captures are under `%LOCALAPPDATA%\L5R\Previews\<package-id>`.
The package never fetches, switches or edits a development checkout. Once downloaded,
it works offline. A newer preview gets a new package/version; old evidence stays tied
to the old build. The separate Duel Demo must be packaged as its own preview.

GitHub must have **Email** / **On GitHub** enabled for participating notifications
on Mendon's own account. We can publish a mention and review request; we cannot
confirm email delivery or silently configure his account. There is no background
Claude listener or automatic remote launch. Claude can read the review record and
download/open its exact build when Mendon asks.

## Claude Code / other agent

Read the review notification on the source PR. It supplies the versioned release,
asset name and expected SHA-256. With authenticated GitHub CLI access on Mendon's
Windows computer, download into a **new** version-specific folder outside his source
checkout. For example (substitute values from the notification, not arbitrary text):

```powershell
gh release download RELEASE_TAG --repo l5r-emperor-dev/l5r-emperor --pattern ASSET_NAME --dir DOWNLOAD_FOLDER
Get-FileHash -Algorithm SHA256 -LiteralPath ZIP_PATH
# Verify that the full hash equals the notification's SHA-256 before extracting.
Expand-Archive -LiteralPath ZIP_PATH -DestinationPath NEW_REVIEW_FOLDER
Start-Process -FilePath 'FULL_PATH_TO_EXTRACTED_FOLDER\Start Review.cmd'
```

For a multipart release, repeat the download, hash verification and extraction for
every named ZIP asset, using the same new destination folder. Each asset has its
own hash in the notification; never launch before all parts have been extracted.

Do not invent a different branch, substitute `main`, fetch artwork separately, alter
Mendon's checkout, or merge anything to launch a review. A normal local filesystem
link to the extracted launcher is then available for future clicks. If GitHub CLI
isn't configured, use the authenticated browser download; no token belongs in a
message, command pasted into a PR, or committed file.

## Public repository publication

The repository and its Releases are public; a prerelease is not private storage.
Follow `docs/PUBLIC_REPO.md`. For an owner-authorized game preview, pass
`--allow-public-repository` to the stage command only after checking the package
and its required artwork for public distribution. The default still refuses public
publication without that explicit opt-in. This does not relax hashes, immutable
versions, Windows validation or notification gates. Do not publish additional bulk
artwork, research scans, credentials or local room data as part of a preview.

For #1200, Alfredo explicitly requested publication and Mendon's review on
16 November 2026; the issue records the bounded game/required-art preview scope.

## Publisher workflow (Codex and Claude)

The user has authorized targeted review notifications to Mendon for Alfredo's
finished previews. Send one per verified package, after its download and launch
checks pass. Keep the first comment useful: direct download, three launch steps,
what to inspect, source commit, limitations and feedback route. Do not notify on
every build retry or claim that a GitHub comment was delivered/read by email.

1. Use the task's existing issue/PR and a clean exact source checkout. Update or add
   `previews/<name>.json` following the Regions/Celestials example. Modes must be
   existing interactive review modes in that source; do not use automated probes
   that click through actions or exit while a human is reviewing. Increment
   `revision` for any repackaging, even if the game source commit is unchanged.
2. Package from the checkout that actually contains the scenario:

   ```sh
   python3 tools/previews/package.py --spec previews/regions-celestials.json --source /absolute/path/to/source
   ```

   This runs the existing strict Windows exporter, checks source SHA and tracked
   cleanliness, adds isolated settings/launchers and emits a ZIP plus receipt under
   ignored `artifacts/previews/`. Never put game binaries into Git. Archives above
   GitHub's 2 GiB per-asset limit are delivered as numbered ZIP parts without changing
   the game or its artwork. The receipt records each part and the original archive's
   hash; the bundled Windows helper verifies and assembles those exact bytes.
3. Stage a validation candidate (GitHub CLI authentication required):

   ```sh
   python3 tools/previews/publish.py stage --spec previews/regions-celestials.json --receipt artifacts/previews/regions-celestials.receipt.json --archive artifacts/previews/L5R-Review-pr292-7360ff1c17-r1-Windows.zip
   ```

   The publisher uploads as a draft, verifies GitHub's size/SHA-256, then makes it
   a prerelease titled **Validation pending** so read-only CI can download
   it. No targeted review notification is sent yet. It never replaces an existing
   asset. Copy that receipt into `previews/published/<name>.json`, commit
   it with the tooling/specification and handoff, then push the delivery PR.
4. Wait for **Windows review package** CI and the relevant existing checks. The
   Windows job downloads the same release archive, verifies its hash, starts its
   real exported executable headlessly and executes every .cmd launcher against an
   argument-capturing executable, including paths with spaces, `&` and `!`. Multipart
   receipts also require every companion asset, byte-for-byte archive assembly, and
   the outer Start Review bootstrap through its real PowerShell helper.
   All committed receipts run in four parallel Windows groups; the named **Windows
   review package** check succeeds only when every group succeeds. This keeps the
   growing archive collection within each job's time/disk limits without skipping
   any package.
   This proves packaging/runtime/argument handling, not native graphical gameplay.
5. Once checks pass, publish and notify using the exact validated delivery commit:

   ```sh
   python3 tools/previews/publish.py deliver --spec previews/regions-celestials.json --receipt previews/published/regions-celestials.json --validation-commit DELIVERY_COMMIT_SHA
   ```

   The command refuses a mismatched receipt or missing Windows check. It marks
   the prerelease ready for review (never as the latest stable release), adds the complete instructions
   and posts one `@mdornbrook` review comment on the source PR. Retries detect the
   version marker and do not send another notification. Review routing remains in
   place; publishing a preview never merges the game or changes human acceptance.

The toolkit can run from its own tooling checkout against an older/unmerged game
checkout. Preserve that distinction in evidence: the receipt identifies game source;
the CI commit identifies delivery tooling. Source PR findings remain visible and
must be investigated before merging, even if the preview package passes.
