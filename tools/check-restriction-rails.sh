#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/restrictions
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/restrictions/import.log 2>&1
for spec in '1920x1080 normal' '1280x720 small' '1920x1080 perspective'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://TokenRailProbe.tscn -- --restrictions "--$mode" --output="$L5R_PROJECT_ROOT/artifacts/checks/restrictions/$mode.png" > "artifacts/checks/restrictions/$mode.log" 2>&1
  rg -q TOKEN_RAIL_PASS "artifacts/checks/restrictions/$mode.log"
done
for spec in '1920x1080 normal' '1280x800 reduced'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=restriction-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/restrictions/table-$mode.png" --capture-mode="published-action-lock-restriction-$mode" > "artifacts/checks/restrictions/table-$mode.log" 2>&1
  rg -q ACTION_LOCK_PASS "artifacts/checks/restrictions/table-$mode.log"
done
if rg -n 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/restrictions/*.log; then exit 1; fi
echo 'Restriction rail native layout, input, inspector, both-seat and expiry checks passed.'
