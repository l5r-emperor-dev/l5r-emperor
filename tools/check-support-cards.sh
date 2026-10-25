#!/usr/bin/env bash
# Native Regions/Celestials input, lifecycle and visual evidence on prepared real-engine tables.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/support-cards
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo > artifacts/checks/support-cards/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/support-cards/import.log 2>&1
for spec in '1920x1080 default' '1280x800 compact-opponent' '1920x1080 crowded' '1440x900 crowded-opponent' '1920x1080 motion' '1920x1080 dynasty-flow' '1920x1080 doji'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=regions-celestials-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/support-cards/$mode.png" --capture-mode="published-supports-$mode" > "artifacts/checks/support-cards/$mode.log" 2>&1
  rg 'REGIONS_CELESTIALS_PASS' "artifacts/checks/support-cards/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/support-cards/$mode.log"; then cat "artifacts/checks/support-cards/$mode.log"; exit 1; fi
done
