#!/usr/bin/env bash
# The table menu's "Return to game" closes the menu when it is really clicked (it once sat under the dialog's hidden Close-button row, which took the click).
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/table-menu
dotnet build src/L5R.Client/L5R.Client.csproj --nologo --disable-build-servers -p:UseSharedCompilation=false -m:1 > artifacts/checks/table-menu/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/table-menu/import.log 2>&1
for resolution in 1920x1080 1280x800; do
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=table-menu-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/table-menu/$resolution.png" --capture-mode="published-table-menu" > "artifacts/checks/table-menu/$resolution.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/table-menu/$resolution.log"; then cat "artifacts/checks/table-menu/$resolution.log"; exit 1; fi
  rg -q 'TABLE_MENU_PASS' "artifacts/checks/table-menu/$resolution.log"
  echo "Table menu native: $resolution passed"
done
