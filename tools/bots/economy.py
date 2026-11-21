#!/usr/bin/env python3
"""Economy and tempo from tournament logs: Holdings bought/discarded by turn, Personalities, first razed province. Usage: economy.py <log dir> <decks.md>"""
import os, re, sys
from collections import defaultdict
types = {}
for line in open(sys.argv[2], encoding="utf-8"):
    m = re.match(r"- \d+x (.+?) \[(\w+)", line)
    if m: types[m.group(1)] = m.group(2)
HEAD = re.compile(r"^# .+?\(seat 0, (challenger|champion)\).+?winner (\S+) \((\w+)\)")
ACT = re.compile(r"^T(\d+) (\w+) s(\d)([Cc]) (\w+)(?:/\S+)? card=(.+?)(?: target=| by=| choice=| prov=| cards=| assign=|$)")
rows = []
for f in sorted(os.listdir(sys.argv[1])):
    lines = open(os.path.join(sys.argv[1], f), encoding="utf-8").read().split("\n")
    h = HEAD.match(lines[0]);
    if not h: continue
    chal = 0 if h.group(1) == "challenger" else 1
    won = h.group(3) == "CHAL"
    st = [defaultdict(int), defaultdict(int)]
    first_raze = [99, 99]; turn = 0; active = 0
    for line in lines:
        m = re.match(r"===== TURN (\d+) active s(\d)", line)
        if m: turn = int(m.group(1)); active = int(m.group(2)); continue
        a = ACT.match(line)
        if a:
            t, ph, seat, _, cmd, card = a.groups(); seat = int(seat); t = int(t); ty = types.get(card.strip(), "?")
            if cmd == "purchase":
                st[seat][f"buy{ty}"] += 1
                if t <= 4: st[seat][f"buy{ty}<=4"] += 1
            if cmd == "discardProvince":
                st[seat][f"disc{ty}"] += 1
                if t <= 4: st[seat][f"disc{ty}<=4"] += 1
            continue
        m = re.search(r"·\s+(\w+)'s province \d was destroyed", line)
        if m:
            # destroyed province belongs to the non-active player; the active player razed it
            first_raze[active] = min(first_raze[active], turn)
    rows.append((chal, won, st, first_raze))
def avg(sel, key, side):
    xs = [r[2][r[0] if side == "C" else 1 - r[0]][key] for r in rows if sel(r)]
    return sum(xs) / max(1, len(xs))
for label, sel in (("Contender won", lambda r: r[1]), ("Contender lost", lambda r: not r[1])):
    print(f"== {label} ({sum(1 for r in rows if sel(r))})")
    for k in ["buyHolding<=4", "discHolding<=4", "buyPersonality<=4", "discPersonality<=4", "buyHolding", "discHolding", "buyPersonality", "discPersonality"]:
        print(f"  {k:20s} C {avg(sel,k,'C'):5.2f}  c {avg(sel,k,'c'):5.2f}")
    fr = [(r[3][r[0]], r[3][1 - r[0]]) for r in rows if sel(r)]
    print(f"  first raze turn      C {sum(min(a,30) for a,_ in fr)/len(fr):5.1f}  c {sum(min(b,30) for _,b in fr)/len(fr):5.1f}; Contender razed first in {sum(1 for a,b in fr if a<b)}/{len(fr)}")
both = [(r[3][r[0]] < r[3][1 - r[0]], r[1]) for r in rows]
print("P(win | razed first) =", sum(1 for f, w in both if f and w) / max(1, sum(1 for f, _ in both if f)), " n", sum(1 for f, _ in both if f))
