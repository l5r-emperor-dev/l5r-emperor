#!/usr/bin/env python3
"""Summarise tournament game logs (L5R_TOURN_LOG) by seat: what each bot spent and how its battles went.

Usage: analyze_logs.py <log dir> [--by-deck]
Prints, for the challenger and the champion, averages over won and lost games: Strategies played, abilities used, Reactions taken,
equips, purchases, province discards, attacks declared, battles fought as attacker, provinces destroyed, Personalities lost.
"""
import os
import re
import sys
from collections import defaultdict

HEAD = re.compile(r"^# (.+?) \(seat 0, (challenger|champion)\) vs (.+?) \(seat 1, (challenger|champion)\); seed (\d+); winner (\S+) \((\w+)\) by (.+?) on turn (\d+)")
ACT = re.compile(r"^T(\d+) (\w+) s(\d)([Cc]) (\S+)")


def parse(path):
    lines = open(path, encoding="utf-8").read().split("\n")
    m = HEAD.match(lines[0])
    if not m:
        return None
    deck0, role0, deck1, _, seed, winner, who, reason, turn = m.groups()
    chal = 0 if role0 == "challenger" else 1
    g = {"deck": [deck0, deck1], "chal": chal, "who": who, "reason": reason, "turn": int(turn), "seed": seed, "path": path,
         "stats": [defaultdict(int), defaultdict(int)]}
    names = {0: None, 1: None}
    for line in lines:
        a = ACT.match(line)
        if a:
            t, phase, seat, _, cmd = a.groups()
            seat = int(seat)
            st = g["stats"][seat]
            kind = cmd.split("/")[0]
            st["cmd:" + kind] += 1
            if kind == "useAbility":
                ab = cmd.split("/", 1)[1] if "/" in cmd else ""
                st["ability:" + ab] += 1
                if phase == "Battle":
                    st["battleAbility"] += 1
                if phase == "Action":
                    st["actionAbility"] += 1
            if kind == "playStrategy":
                st["strategy@" + phase] += 1
            if kind == "declareAttack":
                st["declares"] += 1
            if kind == "discardProvince":
                st["discards"] += 1
            if kind == "purchase":
                st["purchases"] += 1
            last = (seat, kind)
            continue
        s = line.strip()
        if s.startswith("· "):
            msg = s[2:]
            for seat in (0, 1):
                pass
            if "province" in msg and "destroyed" in msg.lower():
                g.setdefault("provmsgs", []).append(msg)
    return g


def main():
    d = sys.argv[1]
    games = [g for g in (parse(os.path.join(d, f)) for f in sorted(os.listdir(d)) if f.endswith(".log")) if g]
    keys = ["cmd:playStrategy", "cmd:useAbility", "battleAbility", "actionAbility", "cmd:equip", "purchases", "discards", "declares", "cmd:attack", "cmd:assignDefense", "cmd:declineReaction"]
    for label, sel in (("challenger WON", lambda g: g["who"] == "CHAL"), ("challenger LOST", lambda g: g["who"] == "CHAMP")):
        gs = [g for g in games if sel(g)]
        if not gs:
            continue
        print(f"== {label}: {len(gs)} games, mean turn {sum(g['turn'] for g in gs)/len(gs):.1f}")
        for k in keys:
            c = sum(g["stats"][g["chal"]][k] for g in gs) / len(gs)
            p = sum(g["stats"][1 - g["chal"]][k] for g in gs) / len(gs)
            print(f"   {k:22s} challenger {c:6.1f}  champion {p:6.1f}")
    reasons = defaultdict(lambda: [0, 0])
    for g in games:
        reasons[g["reason"]][0 if g["who"] == "CHAL" else 1] += 1
    print("reasons (chal wins, champ wins):", dict(reasons))
    if "--by-deck" in sys.argv:
        per = defaultdict(lambda: [0, 0])
        for g in games:
            per[g["deck"][g["chal"]]][0 if g["who"] == "CHAL" else 1] += 1
        for k, v in sorted(per.items()):
            print(f"   {k:24s} {v[0]}-{v[1]}")
    abil = defaultdict(lambda: [0, 0])
    for g in games:
        for seat in (0, 1):
            for k, v in g["stats"][seat].items():
                if k.startswith("ability:"):
                    abil[k][0 if seat == g["chal"] else 1] += v
    print("top abilities (challenger uses, champion uses):")
    for k, v in sorted(abil.items(), key=lambda kv: -sum(kv[1]))[:30]:
        print(f"   {k:40s} {v[0]:5d} {v[1]:5d}")


if __name__ == "__main__":
    main()
