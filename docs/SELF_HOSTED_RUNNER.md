# Local macOS Actions runner

Issue: https://github.com/l5r-emperor-dev/l5r-emperor/issues/100

Two repository-scoped Mac runners exist: `alfredo-mac-studio` (labels `self-hosted`, `macOS`, `ARM64`,
`l5r-mac`, `l5r-mac-studio`) and Mendon's backup `mendon-mac-m5` (`self-hosted`, `macOS`, `ARM64`, `l5r-mac`,
`l5r-mac-m5`; see "Backup runner"). The host notes below describe the Studio.

**Routing (issue #1303, 2026-11-20):** the Studio is primary. Before each run, the `route-mac` job in
`check.yml` reads the Studio's last two `verify (macos-local)` jobs. If both hit the 25-minute limit (a
cancelled job that ran at least 24 minutes; a shorter cancel, from a newer push, is skipped) and the newer one
ended in the last 24 hours, the row runs on `mendon-mac-m5`, with 60 minutes and a full-history checkout.
After 24 hours without a new Studio timeout (or once the Studio's timeouts drop out of the last 30 runs), it
tries the Studio again. To force a Mac, set the repository variable `MAC_RUNNER` to `studio` or `m5`
(Settings → Secrets and variables → Actions → Variables); delete it to return to automatic routing.
Each run's summary says which Mac it chose.
The Windows row and Linux collaboration workflows remain GitHub-hosted.
One runner executes one job at a time. Newer desktop runs supersede older runs
on the same PR/ref. Fork PRs do not execute the desktop workflow on this Mac.

## Host and operation

- Host: Alfredo’s M1 Max Mac Studio, 32 GB RAM, macOS 26.5.2 at installation.
- Runner: `/Users/alfredo/.local/share/l5r-actions-runner`.
- Jobs use its separate `_work` checkout; they never build in the shared development worktree.
- The runner runs as Alfredo, without sudo. This separates checkouts, not OS permissions:
  trusted repository workflow code has the account’s file access. Do not enable this
  runner for public/untrusted contributions. Checkout does not retain its GitHub token.
  **The repository is now public (and moved to `l5r-emperor-dev/l5r-emperor`).** The fork guard in
  `check.yml` keeps fork PRs off this Mac, but any user with write access can still push a branch
  whose workflow runs here as Alfredo. Keep write access to people trusted with that account, keep
  fork-PR approval on "all outside contributors", never run `pull_request_target` PR code here, and
  prefer switching the macOS row to a hosted runner (see Recovery) if that trust cannot be kept.
- A per-user launchd service starts after Alfredo logs in and restarts the runner if it exits.
  After reboot/FileVault unlock, log in before expecting jobs. Logging out, shutting down,
  or manually sleeping the Mac makes jobs wait until it returns.
- While the service runs, `caffeinate -i` prevents idle system sleep but permits display sleep.
  No global power settings are changed. Stop the service to restore normal idle sleep.
- The service uses reduced CPU scheduling priority (nice 10). This is not a CPU/RAM cap.
- macOS uses the installed Homebrew Python 3.14 (requires 3.12+) in a temporary job
  virtual environment; Windows retains setup-python 3.12. Python packages and tool
  caches reside under the runner directory. Each job installs/validates
  the pinned project tools and runs the same full check script as hosted macOS.
- Runner updates remain automatic. Credentials and binaries are outside Git.

## Backup runner: `mendon-mac-m5`

- Host: Mendon's MacBook (Apple M5, 32 GB RAM, macOS 26.5). Runner ID 22. Installed 2026-11-20 by
  `tools/ci/install-mac-runner.sh`; the `l5r-mac-m5` label was added afterwards (issue #1304).
- It runs as the hidden standard user `l5r-runner` (home `/Users/l5r-runner`, mode 700), not as Mendon:
  workflow code cannot read his home folder (also mode 700), credentials or game data. Runner
  directory `/Users/l5r-runner/actions-runner`, jobs in its `_work`.
- Checkout: full history (`fetch-depth: 0`). A shallow fetch re-sends the whole ~3.8 GB tree on every
  run, about 27 minutes on this Mac's connection; with full history on disk, later runs fetch only new commits.
- Service: the LaunchDaemon `/Library/LaunchDaemons/dev.l5r-emperor.actions-runner.plist` (starts at
  boot, no login needed; KeepAlive; nice 10). It wraps the runner in `caffeinate -s`, which keeps the
  system awake **on AC power only**: on battery or with the lid closed the laptop sleeps and the
  runner is simply offline. Python is Homebrew's 3.13 (the runner's `.path`).
- Arena runs on this Mac: Conan's lookahead is bounded by wall-clock time, so a CI job beside an
  arena run distorts the arms. Stop the runner first, start it after:
  `sudo launchctl bootout system/dev.l5r-emperor.actions-runner` /
  `sudo launchctl bootstrap system /Library/LaunchDaemons/dev.l5r-emperor.actions-runner.plist`.
- Remove: boot it out as above, delete the plist, remove the runner in Settings → Actions → Runners,
  then `sudo sysadminctl -deleteUser l5r-runner`.

## Controls

From Terminal:

```sh
cd /Users/alfredo/.local/share/l5r-actions-runner
./svc.sh status
./svc.sh stop
./svc.sh start
```

Check GitHub Settings → Actions → Runners for Online/Idle/Active status.
Logs are in `_diag` and the service log paths in the launchd plist.
Do not share raw diagnostics without checking for sensitive information.

## After the repository move

The existing registration works after the move to `l5r-emperor-dev/l5r-emperor`.
On 2026-10-17, runner **21**, `alfredo-mac-studio`, appeared Online/Idle under the
new repository with all four expected labels. Its [successful main-branch Mac
job](https://github.com/l5r-emperor-dev/l5r-emperor/actions/runs/39035241626/job/117403616962)
ran after the transfer. The launchd service remained active with `KeepAlive`,
`RunAtLoad`, `caffeinate -i` and nice 10. **Leave a healthy registration and service
alone.** A repository transfer by itself is not evidence that re-registration
is necessary.

The local `.runner` file still records the old repository URL, and the existing
service label also contains the old owner name. These retained identifiers do
not mean jobs are running against a different repository. Do not manually edit
registration or credential files, rename the service for appearance, or print
credentials, tokens, environment secrets or raw diagnostic logs during checks.
For `.runner`, report only the repository URL, runner name and ID.

GitHub's current [runner configuration implementation](https://github.com/actions/runner/blob/main/src/Runner.Listener/Configuration/ConfigurationManager.cs)
rejects configuring an already configured runner; it requires removal before
reconfiguration. There is no supported in-place URL change in that command.
Consider re-registration only when an actual registration fault remains after
checking service status and network access. If it is needed:

1. Wait for the runner to be idle and confirm that no job will be interrupted.
2. Record its labels, work directory and launchd configuration, including the
   `caffeinate -i` wrapper, nice 10, `KeepAlive` and `RunAtLoad`. Preserve the runner
   directory, workspaces and caches.
3. Follow the current removal and registration instructions in the **new**
   repository's Settings → Actions → Runners. Keep short-lived tokens local;
   never paste them into shared files, issues, shell history or logs.
4. Keep the name `alfredo-mac-studio` and custom label `l5r-mac-studio`. Restore
   the recorded custom service behavior before starting it: a plain service
   reinstall must not silently discard the sleep-prevention or priority settings.
5. Verify Online/Idle and all four labels, then run the trusted desktop workflow
   on `main` and verify that its Mac job succeeds on this runner. Record any new
   runner ID. Do not use fork code as a test.

## Recovery

If offline, start the service and check network access. Jobs queue rather than
silently falling back to paid macOS. A temporary hosted fallback requires a reviewed
workflow change: replace the macOS row’s runner labels with `[macos-14]`.
To remove: stop/uninstall the service, then remove this runner through repository
Settings → Actions → Runners before deleting its local directory.

CI is compilation and headless import/smoke validation; it does not substitute for
native graphical Mac/Windows playtesting. Running on macOS 26 also changes OS coverage
from the former hosted macOS 14 environment.
