#!/usr/bin/env python3
"""q2_counterattack.py - what happens on the opponent's next turn (question 2).

Usage: python3 q2_counterattack.py <logs-dir>...

Unit: one own turn N of seat S with >=1 unbowed Personality at the header,
followed by the opponent's turn N+1 (the game did not end on N).
S's choice on N: 'none' (no attack), 'partial' (assigned fewer Personalities
than it had unbowed at the header) or 'all-in' (assigned all of them).
The guard is read from the TURN header of N+1: S's unbowed Personalities and
unbowed Force at home at the moment the opponent starts its turn.  That is
exact (it includes Personalities S bought in its Dynasty phase).

Tables
  2a  by choice: guard left, opponent attacks back, S loses >=1 Province,
      S loses units, S kills attackers, Provinces S took on N, net swing
  2b  by guard size (unbowed Personalities at N+1)
  2c  by guard Force relative to the opponent's unbowed Force at N+1
  2d  all-in vs partial vs none inside matched situations: strata of own-turn
      bucket x S Provinces x opponent Provinces x Force-balance bucket at the
      header of N; each stratum weighted by its all-in count.  Outcomes: the
      Provinces taken on N, lost on N+1, net, and whether S won the game.

Approximation: 'all-in' compares assigned Personalities with unbowed
Personalities at the header, so a Personality bowed in the Action phase
(e.g. to lobby) makes an all-out attack look 'partial'.  Table 2b does not
depend on that.  Human rows are counts (7 games).
"""
import collections
import sys

import l5rlog as L

CH = ['none', 'partial', 'all-in']


def collect(games):
    """Accumulators of every table in this script, from parsed games (profile.py reuses them)."""
    A = collections.defaultdict(lambda: collections.defaultdict(list))   # (ptype, choice) -> metric -> values
    G = collections.defaultdict(lambda: collections.defaultdict(list))   # (ptype, guard bucket)
    R = collections.defaultdict(lambda: collections.defaultdict(list))   # (ptype, ratio bucket)
    S = collections.defaultdict(lambda: collections.defaultdict(list))   # (ptype, stratum, choice) -> metric
    for g in games:
        for i, t in enumerate(g.turns[:-1]):
            n = g.turns[i + 1]
            if n.n != t.n + 1 or n.active == t.active:
                continue
            s = t.active
            ch = L.choice(t)
            st, so = t.status.get(s), t.status.get(1 - s)
            gs, go = n.status.get(s), n.status.get(1 - s)
            if ch is None or gs is None or go is None:
                continue
            p = g.ptype[s]
            took = L.provinces_lost_on(t, 1 - s)
            lostp = L.provinces_lost_on(n, s)
            lostu = L.units_lost_on(n, s)
            back = L.has_attack(n)
            killed = n.attack.att_lost if back else 0
            rec = dict(guard=len(gs.unbowed_pers), guardF=gs.unbowed_force, back=back, lostp=lostp,
                       lostany=lostp > 0, lostu=lostu > 0, killed=killed > 0, took=took, net=took - lostp,
                       win=(g.winner == s))
            for k, v in rec.items():
                A[(p, ch)][k].append(v)
            gb = str(min(3, len(gs.unbowed_pers))) + ('+' if len(gs.unbowed_pers) >= 3 else '')
            for k, v in rec.items():
                G[(p, gb)][k].append(v)
            of = go.unbowed_force
            ratio = gs.unbowed_force / of if of > 0 else 9
            rb = ('a: guard F < 25% of opp.' if ratio < 0.25 else 'b: 25-60%' if ratio < 0.6
                  else 'c: 60-100%' if ratio < 1.0 else 'd: >= 100%')
            if of > 0:
                for k, v in rec.items():
                    R[(p, rb)][k].append(v)
            ot = L.own_turn(t.n)
            stratum = ('2-3' if ot <= 3 else '4-5' if ot <= 5 else '6-7' if ot <= 7 else '8+',
                       st.provinces, so.provinces, L.balance_bucket(st.unbowed_force - so.unbowed_force))
            for k, v in rec.items():
                S[(p, stratum, ch)][k].append(v)

    return A, G, R, S


def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    A, G, R, S = collect(games)
    def row(lab, p, d):
        n = len(d['back'])
        return lab + [n, L.mean(d['guard']), L.mean(d['guardF']), L.rate(sum(d['back']), n, p),
                      L.rate(sum(d['lostany']), n, p), L.mean(d['lostp'], 2), L.rate(sum(d['lostu']), n, p),
                      L.rate(sum(d['killed']), n, p), L.mean(d['took'], 2), L.mean(d['net'], 2)]
    hdr = ['turns', 'guard: unbowed Pers. (mean)', 'guard Force (mean)', 'opp. attacks next turn',
           'loses >=1 Province next turn', 'Provinces lost next turn (mean)', 'loses >=1 unit next turn',
           'kills >=1 attacker next turn', 'Provinces taken this turn (mean)', 'net Province swing (mean)']
    print('## Q2a  The opponent\'s next turn, by what the side did this turn\n')
    L.table(['player', 'this turn'] + hdr, [row([p, ch], p, A[(p, ch)]) for p in P for ch in CH if A[(p, ch)]])
    print('## Q2b  The opponent\'s next turn, by guard left at home (unbowed Personalities at the next header)\n')
    L.table(['player', 'guard'] + hdr,
            [row([p, gb], p, G[(p, gb)]) for p in P for gb in ['0', '1', '2', '3+'] if G[(p, gb)]])
    print('## Q2c  The opponent\'s next turn, by guard Force relative to the opponent\'s unbowed Force\n')
    L.table(['player', 'guard Force / opp. Force'] + hdr,
            [row([p, rb[3:]], p, R[(p, rb)]) for p in P for rb in sorted(set(b for _, b in R)) if R[(p, rb)]])
    print('## Q2d  Matched comparison: all-in vs partial, all-in vs none (same own-turn bucket, same Province '
          'counts for both sides, same Force-balance bucket; strata weighted by the all-in count)\n')
    rows = []
    for p in P:
        if p in L.SMALL:
            continue
        strata = set(k[1] for k in S if k[0] == p)
        for other in ('partial', 'none'):
            w = 0
            acc = collections.defaultdict(lambda: [0.0, 0.0])
            used = 0
            for sm in strata:
                a, b = S.get((p, sm, 'all-in')), S.get((p, sm, other))
                if not a or not b or len(a['back']) < 5 or len(b['back']) < 5:
                    continue
                na = len(a['back'])
                w += na
                used += 1
                for k in ('took', 'lostp', 'net', 'win', 'lostu'):
                    acc[k][0] += na * sum(a[k]) / len(a[k])
                    acc[k][1] += na * sum(b[k]) / len(b[k])
            if not w:
                continue
            rows.append([p, 'all-in vs ' + other, used, w] + [
                '%.2f vs %.2f' % (acc[k][0] / w, acc[k][1] / w) for k in ('took', 'lostp', 'net')] + [
                '%.0f%% vs %.0f%%' % (100 * acc['lostu'][0] / w, 100 * acc['lostu'][1] / w),
                '%.0f%% vs %.0f%%' % (100 * acc['win'][0] / w, 100 * acc['win'][1] / w)])
    L.table(['player', 'comparison', 'strata used', 'all-in turns matched', 'Provinces taken this turn',
             'Provinces lost next turn', 'net swing', 'loses a unit next turn', 'wins the game'], rows)


if __name__ == '__main__':
    main(sys.argv[1:])
