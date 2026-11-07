#!/usr/bin/env python3
"""Compare a bot-tournament arm with the champion's own baseline on the SAME games (issue #1185).

    python3 tools/bots/compare_tournament.py --baseline artifacts/bots/cal-champion.json --arm artifacts/bots/contender.json [--arm more.json ...]

Why this exists: with `independent` pairing, the champion played against itself (the A/A, `L5R_TOURN_CHAL=champion`) does NOT score 50% on a given seed block: on block 1700 it scored 46.4%
(169-195), because the challenger label sits more often on the weaker side of a deck pairing and the seats are not symmetric. A single arm at 46% against 50% therefore looks like a loss
when it is none. Every game is deterministic, so an arm and the A/A baseline on the same seed block play the SAME games; counting only the games whose outcome changed (the arm won where
the baseline lost, and the reverse) removes that skew and is far more sensitive: a rule that changes 10 games out of 364 shows up as +3 or -3, not as noise in a 5-point win rate.

Reports, per arm: games gained, games lost, the net, and the exact McNemar sign test over the changed games (two-sided and one-sided, arm better). Several --baseline files (disjoint seed
blocks, e.g. from two machines) and several files per arm are pooled the way merge_tournament.py pools them, but pass the same blocks for the baseline and the arm.
Python standard library only.
"""
import argparse
import json
import sys
from collections import defaultdict

from merge_tournament import sign_test


def load(paths):
    games = {}
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for g in json.load(f)["games"]:
                key = (g["Deck0"], g["Deck1"], g["Seed"], g["ChallengerSeat"])
                if key in games:
                    sys.exit(f"error: game {key} appears twice (in {path}): the seed blocks overlap")
                games[key] = g
    return games


def won(g):
    return g["Winner"] == g["ChallengerSeat"]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--baseline", nargs="+", required=True, help="the champion-versus-champion report(s) (.json), same seed blocks as the arm")
    ap.add_argument("--arm", action="append", nargs="+", required=True, help="an arm's report(s); repeat --arm for several arms")
    args = ap.parse_args()

    base = load(args.baseline)
    decided = [k for k, g in base.items() if g["Winner"] is not None]
    base_wins = sum(won(base[k]) for k in decided)
    print(f"Baseline: {len(base)} games, challenger label won {base_wins} of {len(decided)} decided = {100 * base_wins / max(1, len(decided)):.1f}% (an A/A should be near 50%; this is the skew)\n")
    print(f"{'arm':44s} {'games':>6s} {'win%':>6s} {'gained':>7s} {'lost':>5s} {'net':>5s} {'p two':>8s} {'p arm better':>13s}")
    for paths in args.arm:
        arm = load(paths)
        common = [k for k in decided if k in arm and arm[k]["Winner"] is not None]
        if not common:
            print(f"{paths[0]}: no games in common with the baseline: use the same seed block"); continue
        gained = sum(1 for k in common if won(arm[k]) and not won(base[k]))
        lost = sum(1 for k in common if won(base[k]) and not won(arm[k]))
        one, two = sign_test(gained, gained + lost) if gained + lost else (1.0, 1.0)
        wins = sum(won(arm[k]) for k in common)
        label = paths[0] if len(paths) == 1 else f"{paths[0]} (+{len(paths) - 1})"
        print(f"{label[-44:]:44s} {len(common):6d} {100 * wins / len(common):5.1f}% {gained:7d} {lost:5d} {gained - lost:+5d} {two:8.4f} {one:13.4f}")
        if len(args.arm) == 1:
            per = defaultdict(lambda: [0, 0])
            for k in common:
                g = arm[k]; deck = g["Deck0"] if g["ChallengerSeat"] == 0 else g["Deck1"]
                per[deck][0] += won(arm[k]) and not won(base[k]); per[deck][1] += won(base[k]) and not won(arm[k])
            print("\nper deck the arm piloted (gained / lost against the baseline):")
            for deck in sorted(per):
                print(f"  {deck:24s} +{per[deck][0]} / -{per[deck][1]}")


if __name__ == "__main__":
    main()
