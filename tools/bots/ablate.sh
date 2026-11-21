#!/usr/bin/env bash
# ablate.sh <seed-start> <seeds> <baseline.json> <full,rule,list> [extra name=rules ...]   drops each kept rule in turn (battlepolicy+trades together) and runs the extras
cd "$(dirname "$0")/../.."; start=$1; seeds=$2; base=$3; full=$4; shift 4
args=("$@")
IFS=',' read -ra rules <<< "$full"
for r in "${rules[@]}"; do
  [ "$r" = trades ] && continue
  drop="$r"; [ "$r" = battlepolicy ] && drop="battlepolicy,trades"
  rest=$(python3 -c "import sys;f=sys.argv[1].split(',');d=sys.argv[2].split(',');print(','.join(x for x in f if x not in d))" "$full" "$drop")
  args+=("a-no-${drop//,/+}=$rest")
done
SEEDS=$seeds exec tools/bots/batch2.sh "$start" "$base" "${args[@]}"
