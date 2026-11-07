#!/usr/bin/env python3
"""Pool several bot-tournament JSON reports (issue #1185): the same challenger arm run on different machines or seed blocks.

    python3 tools/bots/merge_tournament.py artifacts/bots/mac.json artifacts/bots/pc.json [--out artifacts/bots/pooled.md]

Each report is the <out>.json that `bot-tournament` writes (docs/bots/README.md). The runs must be the same challenger, pairing and mode on DISJOINT seed blocks (give each
machine its own L5R_TOURN_SEED_START); a repeated game (same decks, seed and seat) is an error, not something to average away. The pooled result is the sum of the games, so
splitting the registered sample over two machines gives the same answer as one run, as long as the total is the pre-registered number of seeds and nobody looks halfway.
Reports the pooled win rate with a Wilson interval, the exact one- and two-sided sign test, the per-deck table and the soundness counts. Python standard library only.
"""
import argparse
import json
import math
import sys
from collections import defaultdict


def wilson(wins, n, z=1.959964):
    if n == 0:
        return 0.0, 0.0
    p = wins / n
    denom = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / denom
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denom
    return centre - half, centre + half


def sign_test(wins, n):
    """Exact binomial against 0.5: returns (one-sided p that the challenger is better, two-sided p)."""
    if n == 0:
        return 1.0, 1.0
    upper = sum(math.comb(n, k) for k in range(wins, n + 1)) / 2 ** n
    lower = sum(math.comb(n, k) for k in range(0, wins + 1)) / 2 ** n
    return upper, min(1.0, 2 * min(upper, lower))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("reports", nargs="+", help="tournament .json reports to pool")
    ap.add_argument("--out", help="also write the pooled report to this markdown file")
    args = ap.parse_args()

    games, seen, heads = [], {}, []
    for path in args.reports:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        summary = data["Summary"]
        heads.append((path, summary["challenger"], summary["pairing"], summary["mode"], len(data["games"])))
        for g in data["games"]:
            key = (g["Deck0"], g["Deck1"], g["Seed"], g["ChallengerSeat"])
            if key in seen:
                sys.exit(f"error: game {key} is in both {seen[key]} and {path}: the seed blocks overlap")
            seen[key] = path
            games.append(g)
    if len({h[1:4] for h in heads}) != 1:
        sys.exit("error: the reports are not the same challenger, pairing and mode:\n" + "\n".join(map(str, heads)))

    challenger, pairing, mode = heads[0][1:4]
    decided = [g for g in games if g["Winner"] is not None]
    wins = sum(1 for g in decided if g["Winner"] == g["ChallengerSeat"])
    lo, hi = wilson(wins, len(decided))
    one, two = sign_test(wins, len(decided))
    unsound = [g for g in games if g.get("Error")]

    per_deck = defaultdict(lambda: [0, 0])
    for g in decided:
        deck = g["Deck0"] if g["ChallengerSeat"] == 0 else g["Deck1"]
        per_deck[deck][0] += 1
        per_deck[deck][1] += g["Winner"] == g["ChallengerSeat"]

    lines = [f"# Pooled tournament: {challenger} (challenger) vs champion", "",
             f"- {len(args.reports)} reports, pairing **{pairing}**, mode **{mode}**: " + "; ".join(f"{p} ({n} games)" for p, _, _, _, n in heads),
             f"- {len(games)} games, {len(decided)} decided, {len(games) - len(decided)} with no winner, {len(unsound)} with an error", "",
             f"**Challenger won {wins}, champion won {len(decided) - wins}** = {100 * wins / max(1, len(decided)):.1f}% (95% Wilson CI {100 * lo:.1f}-{100 * hi:.1f}%).",
             f"Exact sign test: two-sided p = {two:.4f}; one-sided (challenger better) p = {one:.4f}.", "",
             "| Deck (challenger piloting) | Games decided | Challenger wins |", "| --- | --- | --- |"]
    for deck in sorted(per_deck):
        n, w = per_deck[deck]
        lines.append(f"| {deck} | {n} | {w} ({100 * w / n:.0f}%) |")
    text = "\n".join(lines) + "\n"
    print(text)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(text)


if __name__ == "__main__":
    main()
