#!/usr/bin/env bash
# Native check of the variant steps (issue #1202): Governor's Court asks performer, then how to pay; Stolen Merchandise asks Holding, then which ability.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
mkdir -p artifacts/checks/variant-steps
"$GODOT" --headless --editor --path src/L5R.Client --import --quit > artifacts/checks/variant-steps/import.log 2>&1
"$GODOT" --path src/L5R.Client --resolution 1920x1080 -- --profile=direct-play-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/variant-steps/variant-steps.png" --capture-mode="published-variant-steps" > artifacts/checks/variant-steps/native.log 2>&1
rg 'VARIANT_STEPS_PASS' artifacts/checks/variant-steps/native.log
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/variant-steps/native.log; then cat artifacts/checks/variant-steps/native.log; exit 1; fi
