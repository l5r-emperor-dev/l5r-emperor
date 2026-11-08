#!/usr/bin/env bash
# run_arm.sh <out-name> <arm> <seed-start> [seeds] [rules for c2]   (issue #1185 tuning loop; writes artifacts/bots/<out-name>.{json,md,out} and logs-<out-name>/)
set -euo pipefail
name=$1; arm=$2; start=$3; seeds=${4:-1}; rules=${5:-}
cd "$(dirname "$0")/../.."
L5R_C2="$rules" L5R_TOURN_CHAL=$arm L5R_TOURN_PAIRING=independent L5R_TOURN_MODE=dev L5R_TOURN_SEED_START=$start L5R_TOURN_SEEDS=$seeds \
  L5R_TOURN_OUT=artifacts/bots/$name L5R_TOURN_LOG=artifacts/bots/logs-$name \
  bash -c 'source tools/env.sh && dotnet run -c Release --no-build --project tests/L5R.AI.Tests -- --filter bot-tournament' > artifacts/bots/$name.out 2>&1
grep -E "Challenger won" artifacts/bots/$name.out | cut -c1-120
