#!/usr/bin/env bash
# #1279: a card with a status but no tokens shows no token track, on every in-play card type and status combination.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/status-rails
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/status-rails/import.log 2>&1
for spec in '1920x1080 normal' '1280x800 small'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://StatusRailProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/checks/status-rails/$mode" > "artifacts/checks/status-rails/$mode.log" 2>&1 || true
  if ! grep -q STATUS_RAIL_PASS "artifacts/checks/status-rails/$mode.log" || grep -qE 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/status-rails/$mode.log"; then cat "artifacts/checks/status-rails/$mode.log"; exit 1; fi
  grep STATUS_RAIL_PASS "artifacts/checks/status-rails/$mode.log"
done
echo 'Status-only rail checks passed. Review artifacts/checks/status-rails/*/ screenshots separately.'
