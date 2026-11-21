#!/usr/bin/env python3
"""Fit Conan's learned value (issue #1206, lkVal; src/L5R.AI/ValueModel.cs) from arena snapshots.

    python3 tools/bots/fit_value.py <snap-dir> [<snap-dir> ...] [--l2 1.0] [--phases Dynasty,End] [--holdout 0.2]

Each snapshot row (written by the arena with L5R_TOURN_SNAP=<dir>) is: won, seat, turn, phase, features... in the order of ValueModel.Names. A logistic
regression is fitted by Newton's method with an L2 penalty (the bias is not penalised). Games, not rows, are split into training and validation sets, so a
game's correlated rows never sit on both sides. Prints the validation log-loss and accuracy against a constant model, and the weights as a C# array to paste into
ValueModel.Weights. Python standard library only.
"""
import argparse
import glob
import math
import os
import random


def solve(a, b):
    n = len(b); m = [row[:] + [b[i]] for i, row in enumerate(a)]
    for c in range(n):
        p = max(range(c, n), key=lambda r: abs(m[r][c])); m[c], m[p] = m[p], m[c]
        for r in range(n):
            if r != c and m[r][c] != 0:
                f = m[r][c] / m[c][c]
                for k in range(c, n + 1): m[r][k] -= f * m[c][k]
    return [m[i][n] / m[i][i] for i in range(n)]


def sigmoid(z):
    return 1 / (1 + math.exp(-max(-30, min(30, z))))


def loss(rows, w):
    total = correct = 0
    for y, x in rows:
        p = sigmoid(sum(wi * xi for wi, xi in zip(w, x)))
        total -= math.log(p if y else 1 - p); correct += (p > .5) == bool(y)
    return total / len(rows), correct / len(rows)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("dirs", nargs="+"); ap.add_argument("--l2", type=float, default=1.0); ap.add_argument("--phases", default="")
    ap.add_argument("--holdout", type=float, default=0.2); ap.add_argument("--iterations", type=int, default=10)
    a = ap.parse_args()
    phases = set(a.phases.split(",")) if a.phases else None
    games = sorted(f for d in a.dirs for f in glob.glob(os.path.join(d, "*.csv")))
    random.Random(1206).shuffle(games)
    cut = int(len(games) * (1 - a.holdout)); split = {"train": games[:cut], "valid": games[cut:]}
    data = {}
    for name, files in split.items():
        rows = []
        for f in files:
            for line in open(f, encoding="utf-8"):
                p = line.strip().split(",")
                if len(p) < 5 or phases and p[3] not in phases: continue
                rows.append((int(p[0]), [float(v) for v in p[4:]]))
        data[name] = rows
    train, valid = data["train"], data["valid"]
    n = len(train[0][1]); w = [0.0] * n
    print(f"games {len(games)} (train {cut}), rows train {len(train)} valid {len(valid)}, features {n}")
    for it in range(a.iterations):
        g = [0.0] * n; h = [[0.0] * n for _ in range(n)]
        for y, x in train:
            p = sigmoid(sum(wi * xi for wi, xi in zip(w, x))); d = p - y; s = p * (1 - p)
            for i in range(n):
                if x[i] == 0: continue
                g[i] += d * x[i]; si = s * x[i]; hi = h[i]
                for j in range(n): hi[j] += si * x[j]
        for i in range(1, n): g[i] += a.l2 * w[i]; h[i][i] += a.l2
        step = solve(h, g); w = [wi - si for wi, si in zip(w, step)]
        if max(abs(s) for s in step) < 1e-6: break
    base = sum(y for y, _ in train) / len(train); const = [math.log(base / (1 - base))] + [0.0] * (n - 1)
    for name, rows in (("train", train), ("valid", valid)):
        (ll, acc), (cll, cacc) = loss(rows, w), loss(rows, const)
        print(f"{name}: log-loss {ll:.4f} (constant {cll:.4f})  accuracy {100 * acc:.1f}% (constant {100 * cacc:.1f}%)")
    print("weights: [" + ", ".join(f"{v:.4f}" for v in w) + "]")


if __name__ == "__main__":
    main()
