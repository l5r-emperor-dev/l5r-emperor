#!/usr/bin/env python3
"""q3_defence.py - how each player type defends (question 3).

Usage: python3 q3_defence.py <logs-dir>...

Unit for 3a: one attack (declared, >=1 unit assigned) seen from the defender.
Unit for 3b-3e: one attacked battlefield = a Province with >=1 attacker
assigned.  "Defended" = the defender assigned >=1 Personality there in either
defence segment.

Tables
  3a  per attack: does the defender assign anyone (given >=1 unbowed
      Personality at the header), and what share of them
  3b  outcome classes of attacked battlefields
        undefended, lost / undefended, held (attackers removed or too weak)
        defended: held + attacker killed / held, no kill /
                  held but defenders lost (Province saved by dying) /
                  lost + defenders killed ("gift") / lost, defenders survived
  3c  "gift" defences (defended, Province destroyed, >=1 defender destroyed)
      by player type and Provinces the defender had left at the header
  3d  decision-level: the paper position at the moment defenders were
      assigned.  paper margin = est. attacking Force - est. defending Force -
      Strength.  A defence assigned into margin > 0 loses on paper and needs
      Battle actions to hold; margin <= 0 holds on paper.
  3e  undefended battlefields that fall on paper: could the unbowed units the
      defender kept at home have held it on paper?

Province-destroyed rule verified from the logs: destroyed iff attacking Force
- defending Force > Strength (3,366 of 3,372 resolutions in logs-r81; the
rest are Strength modifiers the parser did not see).
Approximations: Force columns use the parser's pre-battle estimate and skip
battlefields with duplicate-name ambiguity.  Human rows are counts (7 games).
"""
import collections
import sys

import l5rlog as L

CLASSES = ['undefended, lost', 'undefended, held', 'held + attacker killed', 'held, no kill',
           'held, defenders lost', 'lost + defenders killed (gift)', 'lost, defenders survived']


def classify(b):
    if not b.def_assigned:
        return CLASSES[0] if b.destroyed else CLASSES[1]
    if b.destroyed:
        return CLASSES[5] if b.def_lost else CLASSES[6]
    if b.def_lost:
        return CLASSES[4]
    return CLASSES[2] if b.att_lost else CLASSES[3]


def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    a3 = collections.defaultdict(lambda: [0, 0, 0, 0])   # attacks faced w/ unbowed pers, defended, sum assigned, sum unbowed
    cls = collections.defaultdict(collections.Counter)
    gift = collections.defaultdict(lambda: [0, 0, 0])   # (ptype, provinces) -> battlefields attacked, defended, gifts
    paper = collections.defaultdict(lambda: [0, 0, 0, 0])   # (ptype, kind) -> n, held, defenders lost, def used neutraliser
    undef = collections.defaultdict(lambda: [0, 0, 0])   # ptype -> undefended paper-falls, could hold with home units, lost
    for g in games:
        for t in g.turns:
            for a in t.attacks:
                if not a.assigned:
                    continue
                d = a.defender
                p = g.ptype[d]
                st = t.status.get(d)
                if st is None:
                    continue
                free = L.free_defenders(t, a)
                if not a.extra and free:
                    x = a3[p]
                    x[0] += 1
                    x[1] += bool(a.defenders)
                    x[2] += len(a.defenders)
                    x[3] += max(len(free), len(a.defenders))
                est_def_total = sum(b.est_D or 0 for b in a.battles.values())
                for b in a.battles.values():
                    if not b.att_assigned:
                        continue
                    c = classify(b)
                    cls[p][c] += 1
                    gg = gift[(p, st.provinces)]
                    gg[0] += 1
                    gg[1] += bool(b.def_assigned)
                    gg[2] += (c == CLASSES[5])
                    if a.extra or b.est_uncertain or b.strength is None or b.est_A is None:
                        continue
                    if b.def_assigned:
                        m = b.est_A - (b.est_D or 0) - b.strength
                        kind = 'defended into a paper loss (margin > 0)' if m > 0 else 'defended, holds on paper (margin <= 0)'
                        x = paper[(p, kind)]
                        x[0] += 1
                        x[1] += (not b.destroyed)
                        x[2] += bool(b.def_lost)
                        x[3] += any(k in L.NEUTRALISING and c2.seat == d for c2, k in L.battle_actions(b))
                    else:
                        m0 = b.est_A - b.strength
                        if m0 > 0:
                            spare = st.unbowed_force - est_def_total - sum(
                                c_.force or 0 for c_ in st.unbowed_pers if c_ not in free)
                            u = undef[p]
                            u[0] += 1
                            u[1] += (spare >= m0)
                            u[2] += b.destroyed
    print('## Q3a  Does the defender assign anyone? (attacks faced with >=1 unbowed Personality at home)\n')
    L.table(['defender', 'attacks faced', 'assigned >=1 defender', 'defenders assigned / unbowed Personalities'],
            [[p, a3[p][0], L.rate(a3[p][1], a3[p][0], p), L.rate(a3[p][2], a3[p][3], p)] for p in P if a3[p][0]])
    print('## Q3b  Outcome of attacked battlefields, by defender\n')
    rows = []
    for p in P:
        n = sum(cls[p].values())
        if n:
            rows.append([p, n] + [L.rate(cls[p][c], n, p) for c in CLASSES])
    L.table(['defender', 'battlefields attacked'] + CLASSES, rows)
    print('## Q3b-defended  The same, defended battlefields only\n')
    rows = []
    for p in P:
        n = sum(cls[p][c] for c in CLASSES[2:])
        if n:
            rows.append([p, n] + [L.rate(cls[p][c], n, p) for c in CLASSES[2:]])
    L.table(['defender', 'defended battlefields'] + CLASSES[2:], rows)
    print('## Q3c  "Gift" defences by Provinces the defender had left\n')
    rows = []
    for p in P:
        for pr in (4, 3, 2, 1):
            gg = gift[(p, pr)]
            if gg[0]:
                rows.append([p, pr, gg[0], L.rate(gg[1], gg[0], p), gg[2], L.rate(gg[2], gg[1], p)])
    L.table(['defender', 'Provinces left', 'battlefields attacked', 'defended', 'gift defences',
             'gifts / defended battlefields'], rows)
    print('## Q3d  The paper position when the defenders were assigned\n')
    rows = []
    for p in P:
        for kind in sorted(set(k for _, k in paper)):
            x = paper[(p, kind)]
            if x[0]:
                rows.append([p, kind, x[0], L.rate(x[1], x[0], p), L.rate(x[2], x[0], p), L.rate(x[3], x[0], p)])
    L.table(['defender', 'assignment', 'battlefields', 'Province held', 'lost >=1 defender',
             'defender used >=1 neutralising action'], rows)
    print('## Q3e  Undefended battlefields that fall on paper\n')
    L.table(['defender', 'undefended, attackers exceed Strength on paper', 'Province destroyed',
             'unbowed units kept at home could have held it on paper'],
            [[p, undef[p][0], L.rate(undef[p][2], undef[p][0], p), L.rate(undef[p][1], undef[p][0], p)]
             for p in P if undef[p][0]])


if __name__ == '__main__':
    main(sys.argv[1:])
