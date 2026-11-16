#!/usr/bin/env bash
# Native engine-backed chooser verification; requires a graphical desktop.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/decisions
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/decisions/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/decisions/import.log 2>&1
specs=('1920x1080 yumaru' '1920x1080 yumaru-self-only' '1280x800 yumaru-target-only' '1920x1080 fukui' '1280x800 fukui-pay-bow' '1280x800 fukui-bowed' '1280x800 waiting' '1280x800 generic' '1280x800 review-check' '1280x800 kamina' '1280x800 p25' '1280x800 otomo' '1280x800 otomo-favor')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=decision-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/decisions/$mode.png" --capture-mode="published-decisions-$mode" > "artifacts/checks/decisions/$mode.log" 2>&1
  rg -q 'DECISION_CHOOSER_PASS' "artifacts/checks/decisions/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/decisions/$mode.log"; then cat "artifacts/checks/decisions/$mode.log";exit 1;fi
  echo "Decision native: $mode passed"
done
