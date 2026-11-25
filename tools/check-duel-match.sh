#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
output="$L5R_PROJECT_ROOT/artifacts/duel-match"
mkdir -p "$output"
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > "$output/build.log" 2>&1
# Real-time native rendering allows macOS focus/resize notifications to settle.
for variant in ${L5R_DUEL_MATCH_VARIANTS:-normal reduced skip compact wide resize lion refusal reaction empty open aftermath concede tie effects}; do
  resolution=1920x1080
  args=()
  case "$variant" in
    reduced) args+=(--duel-match-reduced) ;;
    skip) args+=(--duel-match-skip) ;;
    resize) args+=(--duel-match-resize) ;;
    compact) resolution=1440x900 ;;
    wide) resolution=2560x1080 ;;
    lion) args+=(--duel-match-lion) ;;
    refusal|reaction|empty|open|aftermath|concede|tie) args+=(--duel-match-scenario="$variant") ;;
    effects) args+=(--duel-match-scenario=effects --duel-match-lion) ;; # the active seat orders the Focus Effects
  esac
  "$GODOT" --path src/L5R.Client --always-on-top --position 10,50 --resolution "$resolution" -- --duel-match-review --capture="$output/$variant" --capture-mode=match ${args[@]+"${args[@]}"} > "$output/$variant.log" 2>&1
  if rg -n 'ERROR:|SCRIPT ERROR:|SHADER ERROR:|Unhandled exception' "$output/$variant.log"; then exit 1; fi
  rg -q 'MATCH_DUEL_CHECK: completed' "$output/$variant.log"
  echo "PASS: $variant"
done
