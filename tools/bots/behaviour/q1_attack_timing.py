#!/usr/bin/env python3
"""q1_attack_timing.py - attack timing and selectivity (question 1).

Usage: python3 q1_attack_timing.py <logs-dir>...

Unit of analysis: one own turn of one seat in which it had at least one
unbowed Personality at the TURN header (a turn with a decision to make).
"Attack" = the seat declared an attack and assigned at least one unit.

Tables
  1a  attack rate by own-turn number and player type
  1b  per attack: units committed, estimated Force committed, defender's
      unbowed Force at home, Strength of the targeted Province(s), and the
      "paper margin" = committed - defender's unbowed home Force - Strength
  1c  attack outcomes by own-turn number: >=1 Province destroyed, attacker
      lost >=1 Personality, nothing achieved (no Province, no defender killed)
  1d  attack rate in comparable situations: own turns 3-8, same Province
      count on both sides, by Force balance at the header
  1e  outcomes by paper margin (decision-level: same odds, different pilots)

Approximations: committed Force is the parser's pre-battle estimate (exact in
~72% of battles without actions, within 2 in ~85%; attacks with duplicate-name
ambiguity are left out of the Force columns, kept elsewhere).  The defender's
unbowed home Force counts unbowed Personalities and unbowed Followers at the
header.  Human rows are counts (7 games).
"""
import collections
import sys

import l5rlog as L


def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    opp = collections.Counter()      # (ptype, bucket) turns with a decision
    att = collections.Counter()
    out = collections.defaultdict(lambda: [0, 0, 0, 0])   # attacks, destroyed>=1, lost unit, nothing
    force = collections.defaultdict(list)                 # (ptype, bucket) -> (units, unbowed, committed, defF, strength, margin)
    cond = collections.defaultdict(lambda: [0, 0])        # (ptype, balance) -> [turns, attacks]
    marg = collections.defaultdict(lambda: [0, 0, 0, 0])  # (ptype, margin bucket) -> attacks, destroyed, lost, nothing
    for g in games:
        for t in g.turns:
            s = t.active
            st, so = t.status.get(s), t.status.get(1 - s)
            if st is None or so is None or not st.unbowed_pers:
                continue
            p = g.ptype[s]
            k = L.tb(L.own_turn(t.n))
            opp[(p, k)] += 1
            a = t.attack if L.has_attack(t) else None
            if a:
                att[(p, k)] += 1
            if 3 <= L.own_turn(t.n) <= 8 and st.provinces == so.provinces:
                bb = L.balance_bucket(st.unbowed_force - so.unbowed_force)
                cond[(p, bb)][0] += 1
                cond[(p, bb)][1] += bool(a)
            if not a:
                continue
            dest = len(a.destroyed) > 0
            lost = a.att_lost > 0
            nothing = (not dest) and a.def_lost == 0
            o = out[(p, k)]
            o[0] += 1
            o[1] += dest
            o[2] += lost
            o[3] += nothing
            if not L.attack_uncertain(a) and L.target_strength(a) is not None:
                cf = L.committed_force(a)
                m = cf - so.unbowed_force - L.target_strength(a)
                force[(p, k)].append((len(a.assigned), len(st.unbowed_pers), cf, so.unbowed_force,
                                      L.target_strength(a), m))
                mb = ('a: <= -10' if m <= -10 else 'b: -9..-1' if m < 0 else 'c: 0..+5' if m <= 5
                      else 'd: +6..+12' if m <= 12 else 'e: > +12')
                mm = marg[(p, mb)]
                mm[0] += 1
                mm[1] += dest
                mm[2] += lost
                mm[3] += nothing
    buckets = [L.tb(i) for i in range(1, 10)]
    print('## Q1a  Attack rate by own turn (turns with >=1 unbowed Personality at the header)\n')
    L.table(['own turn'] + ['%s' % p for p in P],
            [[k] + ['%s (n=%d)' % (L.rate(att[(p, k)], opp[(p, k)], p), opp[(p, k)]) for p in P] for k in buckets])
    print('## Q1b  Size of the attack (attacks without name ambiguity)\n')
    rows = []
    for p in P:
        for k in buckets:
            xs = force[(p, k)]
            if not xs:
                continue
            rows.append([p, k, len(xs), L.mean(x[0] for x in xs), L.mean(x[1] for x in xs),
                         L.median([x[2] for x in xs]), L.median([x[3] for x in xs]),
                         L.median([x[4] for x in xs]), L.median([x[5] for x in xs]),
                         L.rate(sum(1 for x in xs if x[5] > 0), len(xs), p)])
    L.table(['player', 'own turn', 'attacks', 'units sent (mean)', 'unbowed Pers. (mean)', 'committed F (median)',
             'defender unbowed home F (median)', 'target Strength (median)', 'paper margin (median)',
             'attacks with margin > 0'], rows)
    print('## Q1c  Attack outcomes by own turn\n')
    rows = []
    for p in P:
        for k in buckets:
            o = out[(p, k)]
            if not o[0]:
                continue
            rows.append([p, k, o[0], L.rate(o[1], o[0], p), L.rate(o[2], o[0], p), L.rate(o[3], o[0], p)])
    L.table(['player', 'own turn', 'attacks', '>=1 Province destroyed', 'attacker lost >=1 Personality',
             'nothing achieved'], rows)
    print('## Q1c-total  Attack outcomes, all turns\n')
    rows = []
    for p in P:
        o = [sum(out[(p, k)][i] for k in buckets) for i in range(4)]
        if o[0]:
            rows.append([p, o[0], L.rate(o[1], o[0], p), L.rate(o[2], o[0], p), L.rate(o[3], o[0], p)])
    L.table(['player', 'attacks', '>=1 Province destroyed', 'attacker lost >=1 Personality', 'nothing achieved'], rows)
    print('## Q1d  Attack rate in comparable situations (own turns 3-8, equal Province counts), '
          'by own unbowed Force minus opponent unbowed Force at the header\n')
    bbs = sorted(set(b for _, b in cond))
    L.table(['Force balance'] + P,
            [[bb[3:]] + ['%s (n=%d)' % (L.rate(cond[(p, bb)][1], cond[(p, bb)][0], p), cond[(p, bb)][0]) for p in P]
             for bb in bbs])
    print('## Q1e  Outcome by paper margin of the attack (committed F - defender unbowed home F - Strength)\n')
    rows = []
    for mb in sorted(set(b for _, b in marg)):
        for p in P:
            mm = marg[(p, mb)]
            if mm[0]:
                rows.append([mb[3:], p, mm[0], L.rate(mm[1], mm[0], p), L.rate(mm[2], mm[0], p), L.rate(mm[3], mm[0], p)])
    L.table(['paper margin', 'player', 'attacks', '>=1 Province destroyed', 'attacker lost >=1 Personality',
             'nothing achieved'], rows)
    print('## Q1e-share  How each player type distributes its attacks over paper margins\n')
    rows = []
    for p in P:
        tot = sum(marg[(p, mb)][0] for mb in set(b for _, b in marg))
        if tot:
            rows.append([p, tot] + [L.rate(marg[(p, mb)][0], tot, p) for mb in sorted(set(b for _, b in marg))])
    L.table(['player', 'attacks'] + [mb[3:] for mb in sorted(set(b for _, b in marg))], rows)


if __name__ == '__main__':
    main(sys.argv[1:])
