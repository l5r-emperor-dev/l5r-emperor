#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/reaction-flow
dotnet build src/L5R.Client/L5R.Client.csproj --nologo --disable-build-servers -p:UseSharedCompilation=false -m:1 > artifacts/checks/reaction-flow/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/reaction-flow/import.log 2>&1
# Each seat performs the same published-card scenarios. The compact case checks
# the supported 1280x800 viewport and the independent reduced-motion rendering.
specs=('1920x1080 normal-seat0' '1920x1080 normal-seat1' '1280x800 reduced-seat0' '1280x800 reduced-seat1' '1280x800 armor-seat1')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=reaction-flow-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/reaction-flow/$mode.png" --capture-mode="published-reaction-flow-$mode" > "artifacts/checks/reaction-flow/$mode.log" 2>&1
  rg -q 'REACTION_FLOW_PASS' "artifacts/checks/reaction-flow/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/reaction-flow/$mode.log"; then cat "artifacts/checks/reaction-flow/$mode.log"; exit 1; fi
  echo "Reaction flow native: $mode passed"
done
