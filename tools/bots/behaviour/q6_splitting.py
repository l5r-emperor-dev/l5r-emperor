#!/usr/bin/env python3
"""q6_splitting.py - attacking more than one Province in a turn (question 6).

Usage: python3 q6_splitting.py <logs-dir>...

Unit: the active seat's attack with >=2 Personalities assigned against a
defender with >=2 Provinces (so a split was possible).  "Split" = attackers
assigned to 2 or more Provinces.

Tables
  6a  how often each player type splits
  6b  split vs unsplit: >=1 Province destroyed, >=2 destroyed, Provinces per
      attack, destruction rate per targeted Province, attacker loses a unit
  6c  the same inside comparable situations: the defender's unbowed
      Personalities at the header (0 / 1 / 2 / 3+) and the attacker's paper
      surplus = estimated committed Force - defender's unbowed home Force -
      mean Strength of the targeted Provinces (how much the attack has to
      spare if everything met at one Province)
  6d  turns in which a defender lost 2 or more Provinces, per game and per
      attack, and whether the side that did it won

Approximation: committed Force is the parser's pre-battle estimate (exact in
~72% of checkable battles, within 2 in ~85%); attacks with an uncertain
estimate are left out of 6c only.  Human rows are counts (7 games).
"""
import collections
import sys

import l5rlog as L


def surplus_bucket(x):
    return 'a: < 0' if x < 0 else 'b: 0..9' if x < 10 else 'c: 10..19' if x < 20 else 'd: >= 20'


def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    a6 = collections.defaultdict(collections.Counter)
    b6 = collections.defaultdict(collections.Counter)
    c6 = collections.defaultdict(collections.Counter)
    d6 = collections.defaultdict(collections.Counter)
    for g in games:
        seen2 = {0: 0, 1: 0}
        for t in g.turns:
            for s in (0, 1):
                k = L.provinces_lost_on(t, 1 - s)
                if k >= 2:
                    seen2[s] += 1
            if not L.has_attack(t):
                continue
            a = t.attack
            s = a.attacker
            p = g.ptype[s]
            so = t.status.get(a.defender)
            d6[p]['attacks'] += 1
            d6[p]['attacks2'] += len(a.destroyed) >= 2
            if so is None or so.provinces < 2 or len(a.assigned) < 2:
                continue
            nt = len(a.targets)
            a6[p]['n'] += 1
            a6[p]['2'] += nt == 2
            a6[p]['3+'] += nt >= 3
            kind = 'split' if nt >= 2 else 'one Province'
            for key in ((p, kind), ):
                x = b6[key]
                x['n'] += 1
                x['any'] += bool(a.destroyed)
                x['two'] += len(a.destroyed) >= 2
                x['prov'] += len(a.destroyed)
                x['targets'] += nt
                x['lost'] += a.att_lost > 0
                x['units'] += len(a.assigned)
            gp = len(so.unbowed_pers)
            gb = '%d%s unbowed defenders' % (min(gp, 3), '+' if gp >= 3 else '')
            x = c6[(p, 'by defender guard', gb, kind)]
            x['n'] += 1
            x['any'] += bool(a.destroyed)
            x['prov'] += len(a.destroyed)
            x['lost'] += a.att_lost > 0
            ts = L.target_strength(a)
            if ts is not None and not L.attack_uncertain(a):
                sp = L.committed_force(a) - so.unbowed_force - ts
                x = c6[(p, 'by paper surplus', surplus_bucket(sp), kind)]
                x['n'] += 1
                x['any'] += bool(a.destroyed)
                x['prov'] += len(a.destroyed)
                x['lost'] += a.att_lost > 0
        for s in (0, 1):
            p = g.ptype[s]
            d6[p]['games'] += 1
            d6[p]['games2'] += seen2[s] > 0
            d6[p]['turns2'] += seen2[s]
            d6[p]['wins2'] += seen2[s] > 0 and g.winner == s
            d6[p]['wins'] += g.winner == s

    print('## Q6a  How often an attack is split (attacks with >=2 Personalities assigned, defender has >=2 Provinces)\n')
    L.table(['attacker', 'attacks', 'at 2 Provinces', 'at 3+ Provinces', 'split (any)'],
            [[p, a6[p]['n'], L.rate(a6[p]['2'], a6[p]['n'], p), L.rate(a6[p]['3+'], a6[p]['n'], p),
              L.rate(a6[p]['2'] + a6[p]['3+'], a6[p]['n'], p)] for p in P if a6[p]['n']])
    print('## Q6b  Split vs one Province\n')
    rows = []
    for p in P:
        for kind in ('one Province', 'split'):
            x = b6[(p, kind)]
            if x['n']:
                rows.append([p, kind, x['n'], '%.1f' % (x['units'] / x['n']), L.rate(x['any'], x['n'], p),
                             L.rate(x['two'], x['n'], p), '%.2f' % (x['prov'] / x['n']),
                             L.rate(x['prov'], x['targets'], p), L.rate(x['lost'], x['n'], p)])
    L.table(['attacker', 'attack', 'attacks', 'Personalities assigned (mean)', '>=1 Province destroyed',
             '>=2 destroyed', 'Provinces per attack', 'destroyed per targeted Province', 'attacker lost >=1 Personality'],
            rows)
    print('## Q6c  Split vs one Province inside comparable situations\n')
    rows = []
    for p in P:
        if p in L.SMALL:
            continue
        for dim in ('by defender guard', 'by paper surplus'):
            for bk in sorted(set(k[2] for k in c6 if k[0] == p and k[1] == dim)):
                u, sp = c6[(p, dim, bk, 'one Province')], c6[(p, dim, bk, 'split')]
                if not u['n'] or not sp['n']:
                    continue
                lab = bk[3:] if dim == 'by paper surplus' else bk
                rows.append([p, dim[3:], lab, u['n'], sp['n'], L.rate(sp['n'], sp['n'] + u['n']),
                             '%.2f vs %.2f' % (u['prov'] / u['n'], sp['prov'] / sp['n']),
                             '%s vs %s' % (L.rate(u['any'], u['n']), L.rate(sp['any'], sp['n'])),
                             '%s vs %s' % (L.rate(u['lost'], u['n']), L.rate(sp['lost'], sp['n']))])
    L.table(['attacker', 'situation', 'bucket', 'one-Province attacks', 'split attacks', 'split share',
             'Provinces per attack (one vs split)', '>=1 destroyed (one vs split)',
             'attacker lost a Personality (one vs split)'], rows)
    print('## Q6d  Two or more Provinces falling in one turn\n')
    L.table(['attacker', 'games', 'games with such a turn', 'such turns per 100 attacks',
             'won the game when it had such a turn', 'won the game overall'],
            [[p, d6[p]['games'], L.rate(d6[p]['games2'], d6[p]['games'], p),
              '%.1f' % (100.0 * d6[p]['attacks2'] / d6[p]['attacks']) if d6[p]['attacks'] else '-',
              L.rate(d6[p]['wins2'], d6[p]['games2'], p), L.rate(d6[p]['wins'], d6[p]['games'], p)] for p in P])


if __name__ == '__main__':
    main(sys.argv[1:])
