#!/usr/bin/env python3
"""q5_cavalry.py - use of the Cavalry manoeuvre segment (question 5).

Usage: python3 q5_cavalry.py <logs-dir>...

The log names the phase of every assignment, so "assigned in the Cavalry
segment" (after the defender has committed its Infantry-segment defenders) is
exact.  Whether a Personality is Cavalry comes from the catalogue keyword on
the Personality card; a unit that gains or loses Cavalry through attachments
is not seen (approximation), except that anything actually assigned in the
Cavalry segment is by definition a Cavalry unit.

Tables
  5a  attacks, attacks in which the attacker was offered the Cavalry segment
      (it still had an unassigned Cavalry unit), attacks that used it; and of
      the Cavalry-keyword Personalities committed to attacks, how many went
      in the Infantry segment (before seeing the defence) vs the Cavalry
      segment (after)
  5b  where Cavalry-segment attackers went: a battlefield with no defender,
      one with defenders, a fresh battlefield with no Infantry attackers; did
      the defender answer in the Cavalry-defence segment; Province destroyed
  5c  Province destruction per attack with and without Cavalry-segment
      assignments, among attacks by a side that had >=2 unbowed Cavalry
      Personalities at the header (same resource, different use)
  5d  decks by Cavalry share (appearances of Cavalry-keyword Personalities at
      TURN headers) and how each player type pilots the mostly-Cavalry decks

Human rows are counts (7 games).
"""
import collections
import sys

import l5rlog as L


def is_cav(name):
    return L.CAT.kw(name, 'Cavalry')


def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    a5 = collections.defaultdict(lambda: [0, 0, 0, 0, 0, 0])   # attacks, offered, used, cavP inf-seg, cavP cav-seg, attacks w/ cav pers
    b5 = collections.defaultdict(collections.Counter)
    c5 = collections.defaultdict(lambda: [0, 0, 0.0])           # (ptype, used?) -> attacks, with destruction, provinces
    deck_c = collections.defaultdict(lambda: [0, 0])
    for g in games:
        for t in g.turns:
            for s in (0, 1):
                st = t.status.get(s)
                if st is None:
                    continue
                for c in st.pers:
                    deck_c[g.decks[s]][0] += 1
                    deck_c[g.decks[s]][1] += is_cav(c.name)
    cav_decks = set(d for d, (n, k) in deck_c.items() if n and k / n >= 0.5)
    d5 = collections.defaultdict(lambda: collections.Counter())   # (ptype, deckclass) -> counters
    for g in games:
        for s in (0, 1):
            dc = 'mostly-Cavalry deck' if g.decks[s] in cav_decks else 'other deck'
            d5[(g.ptype[s], dc)]['games'] += 1
            d5[(g.ptype[s], dc)]['wins'] += (g.winner == s)
        for t in g.turns:
            s = t.active
            st = t.status.get(s)
            if st is None:
                continue
            p = g.ptype[s]
            dc = 'mostly-Cavalry deck' if g.decks[s] in cav_decks else 'other deck'
            if st.unbowed_pers:
                d5[(p, dc)]['turns'] += 1
                d5[(p, dc)]['attacks'] += L.has_attack(t)
            if not L.has_attack(t):
                continue
            a = t.attack
            x = a5[p]
            x[0] += 1
            x[1] += a.cav_option
            x[2] += bool(a.cav)
            ci = sum(1 for n, _ in a.inf if is_cav(n))
            cv = len(a.cav)
            x[3] += ci
            x[4] += cv
            dd = d5[(p, dc)]
            dd['att'] += 1
            dd['att_dest'] += bool(a.destroyed)
            dd['prov'] += len(a.destroyed)
            dd['cav_used'] += bool(a.cav)
            dd['cav_inf'] += ci
            dd['cav_cav'] += cv
            ncav = sum(1 for c in st.unbowed_pers if is_cav(c.name))
            if ncav >= 2:
                k = (p, 'used the Cavalry segment' if a.cav else 'all assigned in the Infantry segment')
                c5[k][0] += 1
                c5[k][1] += bool(a.destroyed)
                c5[k][2] += len(a.destroyed)
            inf_att = collections.Counter(pp for _, pp in a.inf)
            inf_def = collections.Counter(pp for _, pp in a.dfn)
            cav_def = collections.Counter(pp for _, pp in a.cavdef)
            for pp in set(pp for _, pp in a.cav):
                bb = b5[p]
                bb['battlefields'] += 1
                bb['undefended after Infantry defence'] += (inf_def[pp] == 0)
                bb['fresh battlefield (no Infantry attackers)'] += (inf_att[pp] == 0)
                bb['fresh and undefended'] += (inf_att[pp] == 0 and inf_def[pp] == 0)
                bb['defender answered with Cavalry defenders'] += (cav_def[pp] > 0)
                b = a.battles.get(pp)
                bb['destroyed'] += bool(b and b.destroyed)
                bb['destroyed|undef'] += bool(b and b.destroyed and inf_def[pp] == 0)
                bb['destroyed|def'] += bool(b and b.destroyed and inf_def[pp] > 0)
                bb['def'] += (inf_def[pp] > 0)
    print('## Q5a  Use of the Cavalry segment\n')
    L.table(['attacker', 'attacks', 'Cavalry segment offered', 'Cavalry segment used', 'used / offered',
             'Cavalry Personalities sent in the Infantry segment', '... in the Cavalry segment',
             'share held for the Cavalry segment'],
            [[p, a5[p][0], L.rate(a5[p][1], a5[p][0], p), L.rate(a5[p][2], a5[p][0], p),
              L.rate(a5[p][2], a5[p][1], p), a5[p][3], a5[p][4], L.rate(a5[p][4], a5[p][3] + a5[p][4], p)]
             for p in P if a5[p][0]])
    print('## Q5b  Where Cavalry-segment attackers went\n')
    rows = []
    for p in P:
        bb = b5[p]
        n = bb['battlefields']
        if not n:
            continue
        rows.append([p, n, L.rate(bb['undefended after Infantry defence'], n, p),
                     L.rate(bb['fresh battlefield (no Infantry attackers)'], n, p),
                     L.rate(bb['fresh and undefended'], n, p),
                     L.rate(bb['defender answered with Cavalry defenders'], n, p),
                     L.rate(bb['destroyed|undef'], bb['undefended after Infantry defence'], p),
                     L.rate(bb['destroyed|def'], bb['def'], p)])
    L.table(['attacker', 'battlefields receiving Cavalry-segment attackers', 'no defender there after Infantry defence',
             'no Infantry attackers there (fresh)', 'fresh and undefended', 'defender answered in Cavalry defence',
             'Province destroyed when undefended', 'Province destroyed when defended'], rows)
    print('## Q5c  Same resource, different use: attacks by a side with >=2 unbowed Cavalry Personalities\n')
    rows = []
    for (p, k), x in sorted(c5.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        rows.append([p, k, x[0], L.rate(x[1], x[0], p), '%.2f' % (x[2] / x[0])])
    L.table(['attacker', 'how the Cavalry was assigned', 'attacks', '>=1 Province destroyed',
             'Provinces destroyed per attack'], rows)
    print('## Q5d  Decks by Cavalry share\n')
    L.table(['deck', 'Personality appearances at headers', 'Cavalry share'],
            [[d, n, L.pct(k, n)] for d, (n, k) in sorted(deck_c.items(), key=lambda z: -z[1][1] / max(1, z[1][0]))])
    print('## Q5d-pilot  Piloting of mostly-Cavalry decks (Cavalry share >= 50%) vs other decks\n')
    rows = []
    for p in P:
        for dc in ('mostly-Cavalry deck', 'other deck'):
            dd = d5[(p, dc)]
            if not dd['games']:
                continue
            rows.append([p, dc, dd['games'], L.rate(dd['wins'], dd['games'], p), L.rate(dd['attacks'], dd['turns'], p),
                         dd['att'], L.rate(dd['cav_used'], dd['att'], p),
                         L.rate(dd['cav_cav'], dd['cav_cav'] + dd['cav_inf'], p),
                         L.rate(dd['att_dest'], dd['att'], p),
                         '%.2f' % (dd['prov'] / dd['att']) if dd['att'] else '-'])
    L.table(['pilot', 'deck', 'games', 'wins', 'attack rate (turns with unbowed Pers.)', 'attacks',
             'attacks using the Cavalry segment', 'Cavalry Personalities held for the Cavalry segment',
             'attacks destroying >=1 Province', 'Provinces per attack'], rows)


if __name__ == '__main__':
    main(sys.argv[1:])
