#!/usr/bin/env bash
# batch.sh <seed-start> <baseline.json> name=rules ...   runs each c2 variant on the block and appends a paired comparison to artifacts/bots/summary.txt
cd "$(dirname "$0")/../.."; start=$1; base=$2; shift 2
for spec in "$@"; do
  name=${spec%%=*}; rules=${spec#*=}
  tools/bots/run_arm.sh "$name" c2 "$start" "${SEEDS:-1}" "$rules" >/dev/null
  line=$(python3 tools/bots/compare_tournament.py --baseline "$base" --arm artifacts/bots/$name.json | grep "artifacts/bots/$name.json")
  echo "$(date +%H:%M) [$rules] $line" | tee -a artifacts/bots/summary.txt
done
