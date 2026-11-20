#!/bin/bash
set -euo pipefail
l5r_action_checkout="$(cd "$(dirname "$0")" && pwd)"
source "$l5r_action_checkout/tools/env.sh"
l5r_require_godot
cd "$l5r_action_checkout"
mkdir -p artifacts/checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
exec "$GODOT" --path "$l5r_action_checkout/src/L5R.Client" "$@" -- --profile=printed-action-review --capture="$l5r_action_checkout/artifacts/checks/printed-action-review.png" --capture-mode=published-action-lock-restriction-spent-crowded-review
