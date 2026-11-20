#!/bin/bash
# A macOS CI runner for l5r-emperor-dev/l5r-emperor (labels l5r-mac, l5r-mac-m5), run as a dedicated hidden standard user from a boot-time LaunchDaemon (issue #1303;
# docs/SELF_HOSTED_RUNNER.md). Usage: sudo bash tools/ci/install-mac-runner.sh <token-file>, where <token-file> (mode 600, outside Git) holds a registration token from
# `gh api -X POST repos/l5r-emperor-dev/l5r-emperor/actions/runners/registration-token --jq .token`; the script deletes it. Rename the runner (--name) for another Mac.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "run with sudo"; exit 1; }
TOKEN_FILE=$1
RUSER=l5r-runner; RHOME=/Users/$RUSER; DIR=$RHOME/actions-runner
VER=2.338.2; SHA=25a065af409ffa317ab4d5a3b614dd15fc9c40f2172bc1b35a1b1258e919a388
LABEL=dev.l5r-emperor.actions-runner; PLIST=/Library/LaunchDaemons/$LABEL.plist

if ! id "$RUSER" >/dev/null 2>&1; then
  echo "== creating hidden standard user $RUSER"
  sysadminctl -addUser "$RUSER" -fullName "L5R CI runner" -shell /bin/zsh -home "$RHOME" -password "$(openssl rand -base64 32)" 2>&1 | grep -v -i password || true
  dscl . create "/Users/$RUSER" IsHidden 1
  createhomedir -c -u "$RUSER" >/dev/null
fi
chmod 700 "$RHOME"

echo "== downloading runner $VER and checking its SHA-256"
sudo -u "$RUSER" mkdir -p "$DIR"
cd "$DIR"
if [ ! -f config.sh ]; then
  sudo -u "$RUSER" curl -fsSL -o runner.tar.gz "https://github.com/actions/runner/releases/download/v$VER/actions-runner-osx-arm64-$VER.tar.gz"
  echo "$SHA  runner.tar.gz" | shasum -a 256 -c -
  sudo -u "$RUSER" tar xzf runner.tar.gz && rm runner.tar.gz
fi

if [ ! -f .runner ]; then
  echo "== registering mendon-mac-m5 (labels self-hosted, macOS, ARM64, l5r-mac, l5r-mac-m5)"
  TOKEN=$(cat "$TOKEN_FILE")
  sudo -u "$RUSER" ./config.sh --unattended --url https://github.com/l5r-emperor-dev/l5r-emperor --token "$TOKEN" \
    --name mendon-mac-m5 --labels l5r-mac,l5r-mac-m5 --work _work
fi
rm -f "$TOKEN_FILE"

# Python 3.12+ and Homebrew tools for the job (the workflow makes its own venv); the runner reads .path.
echo "/opt/homebrew/opt/python@3.13/libexec/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin" | sudo -u "$RUSER" tee .path >/dev/null
sudo -u "$RUSER" mkdir -p _diag

echo "== installing the boot-time service ($PLIST): caffeinate -s (awake on AC power only), nice 10, KeepAlive"
cat > "$PLIST" <<PL
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>Label</key><string>$LABEL</string>
  <key>UserName</key><string>$RUSER</string>
  <key>WorkingDirectory</key><string>$DIR</string>
  <key>ProgramArguments</key><array><string>/usr/bin/caffeinate</string><string>-s</string><string>/usr/bin/nice</string><string>-n</string><string>10</string><string>$DIR/run.sh</string></array>
  <key>EnvironmentVariables</key><dict><key>HOME</key><string>$RHOME</string><key>PATH</key><string>/opt/homebrew/opt/python@3.13/libexec/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin</string><key>LANG</key><string>en_US.UTF-8</string></dict>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>ProcessType</key><string>Background</string>
  <key>StandardOutPath</key><string>$DIR/_diag/launchd.out.log</string>
  <key>StandardErrorPath</key><string>$DIR/_diag/launchd.err.log</string>
</dict></plist>
PL
chown root:wheel "$PLIST"; chmod 644 "$PLIST"
launchctl bootout "system/$LABEL" 2>/dev/null || true
launchctl bootstrap system "$PLIST"
sleep 5
launchctl print "system/$LABEL" | grep -E "state =|pid =" | head -2
echo "== done"
