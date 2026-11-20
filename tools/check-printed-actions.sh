#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/printed-actions
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/printed-actions/import.log 2>&1
for spec in '1920x1080 normal' '1280x800 reduced'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=printed-action-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/printed-actions/$mode.png" --capture-mode="published-action-lock-restriction-spent-$mode" > "artifacts/checks/printed-actions/$mode.log" 2>&1
  rg -q ACTION_LOCK_PASS "artifacts/checks/printed-actions/$mode.log"
done
if rg -n 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/printed-actions/*.log; then exit 1; fi
echo 'Printed-action native crowding, upright/bowed attachments, six tokens, restrictions, input, both seats, preview bounds and reset checks passed.'
