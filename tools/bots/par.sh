#!/usr/bin/env bash
# par.sh <seed-start> <seeds> <baseline-name> <jobs> name=rules ...   runs the baseline (c2 with $TUNED unless it exists) then the variants, <jobs> at a time; appends paired comparisons to summary.txt
cd "$(dirname "$0")/../.."; start=$1; seeds=$2; base=$3; jobs=$4; shift 4
[ -f artifacts/bots/$base.json ] || tools/bots/run_arm.sh "$base" c2 "$start" "$seeds" "$TUNED" >/dev/null
one() {
  local name=${1%%=*} rules=${1#*=}
  tools/bots/run_arm.sh "$name" c2 "$start" "$seeds" "$rules" >/dev/null
  local line; line=$(python3 tools/bots/compare_tournament.py --baseline artifacts/bots/$base.json --arm artifacts/bots/$name.json | grep "artifacts/bots/$name.json")
  echo "$(date +%H:%M) [$rules] $line" | tee -a artifacts/bots/summary.txt
}
for spec in "$@"; do
  while [ "$(jobs -rp | wc -l)" -ge "$jobs" ]; do sleep 5; done
  one "$spec" &
done
wait
