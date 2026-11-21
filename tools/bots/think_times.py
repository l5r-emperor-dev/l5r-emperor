#!/usr/bin/env python3
"""think_times.py <tournament.json>...: the challenger's lookahead thinking time per decision (issue #1206), from each game's ThinkMs."""
import json, statistics, sys

for path in sys.argv[1:]:
    games = json.load(open(path))
    games = games["games"] if isinstance(games, dict) else games
    each = [ms for g in games for ms in (g.get("ThinkMs") or [])]
    per_game = [sum(g.get("ThinkMs") or []) / 1000 for g in games if g.get("ThinkMs")]
    if not each:
        print(f"{path}: no lookahead decisions recorded")
        continue
    each.sort()
    pct = lambda q: each[min(len(each) - 1, int(q * len(each)))]
    print(f"{path}: {len(each)} searches in {len(per_game)} games; per search median {pct(.5):.0f} ms, p90 {pct(.9):.0f} ms, p99 {pct(.99):.0f} ms, max {each[-1]:.0f} ms; "
          f"per game {statistics.mean(per_game):.1f} s mean thinking ({max(per_game):.1f} s max)")
