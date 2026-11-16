#!/bin/bash
set -euo pipefail
l5r_restriction_checkout="$(cd "$(dirname "$0")" && pwd)"
source "$l5r_restriction_checkout/tools/env.sh"
l5r_require_godot
cd "$l5r_restriction_checkout"
mkdir -p artifacts/checks
# This prepared review uses checked-in fixture scans; it needs no bulk reference-art download.
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
exec "$GODOT" --path "$l5r_restriction_checkout/src/L5R.Client" "$@" -- --profile=restriction-review --capture="$l5r_restriction_checkout/artifacts/checks/restriction-review.png" --capture-mode=published-action-lock-restriction-crowded-review
