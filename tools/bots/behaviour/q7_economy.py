#!/usr/bin/env python3
"""q7_economy.py - economy versus bodies (question 7).

Usage: python3 q7_economy.py <logs-dir>...

Sources: the TURN header gives each seat's Gold capacity ("gold N" = what its
Holdings in play could produce), Holdings and Personalities in play.  Every
payment is logged as "X paid P Gold for C, producing Q; E excess was lost".
Dynasty purchases are the `purchase` commands; their card type comes from the
catalogue.

Tables
  7a  per own turn 1-8: Gold capacity at the header, Gold produced over the
      turn (all payments by the active seat), Gold paid, excess lost, capacity
      left unproduced (max(0, capacity - produced)), Holdings and
      Personalities bought, Gold paid for non-Dynasty cards (attachments,
      actions)
  7b  Personalities, unbowed Force, Holdings and capacity at the start of own
      turns 3, 5 and 7 - all games, and split by seat order
  7c  the Dynasty choice when both were on offer: own turns 2-6 whose row at
      the header shows >=1 Holding and >=1 Personality whose printed cost is
      within the Gold capacity.  First purchase, Personalities and Holdings
      bought, by situation (behind / level / ahead on Personalities in play;
      attacked on the opponent's last turn or not)

  7d  own turns 3-8: a Personality face-up in the row at the header that is
      still there at the next header, whose printed cost fits in the Gold
      capacity the seat left unproduced and whose Honor Requirement the seat
      met - a purchase that was available and not made
  7e  own turns 4-8: Holdings bought, by how much Gold capacity the seat had
      already left unproduced on its previous own turn (buying more capacity
      while existing capacity goes unused)
  7f  own turns 2-6: the Action phase comes before the Dynasty phase, so Gold
      paid for attachments and actions is no longer there for Personalities.
      Turns on which a Personality whose printed cost was within the Gold
      capacity stayed in the row, no Personality was bought, and >=4 Gold
      went to Fate cards instead

Approximations: printed cost is what the logs show paid in 52% of Personality
purchases; 43% were paid 2 below it and 5% above it, so 7c/7d slightly
under-count what was affordable; the header row is what the active player can see at the start of
its turn (cards that refill during the turn are not seen).  Human rows are
counts or plain sums (7 games).
"""
import collections
import sys

import l5rlog as L


def collect(games):
    """Accumulators of every table in this script, from parsed games (profile.py reuses them)."""
    A = collections.defaultdict(lambda: collections.defaultdict(list))
    B = collections.defaultdict(lambda: collections.defaultdict(list))
    C = collections.defaultdict(collections.Counter)
    D = collections.defaultdict(collections.Counter)
    E = collections.defaultdict(collections.Counter)
    F = collections.defaultdict(collections.Counter)
    for g in games:
        prev_attacked = {0: False, 1: False}
        prev_left = {0: None, 1: None}
        for i, t in enumerate(g.turns):
            s = t.active
            p = g.ptype[s]
            st, so = t.status.get(s), t.status.get(1 - s)
            ot = L.own_turn(t.n)
            complete = i < len(g.turns) - 1          # the last turn of a game is usually cut short
            if st is not None and ot <= 8 and complete:
                paid, prod, exc = t.gold[s]
                buys = [x for x in t.purchases if x[0] == s]
                d = A[(p, ot)]
                d['cap'].append(st.gold)
                d['prod'].append(prod)
                d['paid'].append(paid)
                d['exc'].append(exc)
                d['unused'].append(max(0, st.gold - prod))
                d['hold'].append(sum(1 for x in buys if x[2] == 'Holding'))
                d['pers'].append(sum(1 for x in buys if x[2] == 'Personality'))
                d['fate'].append(paid - sum(x[3] for x in buys))
            if st is not None and ot in (3, 5, 7):
                first = 'first player' if g.turns[0].active == s else 'second player'
                for key in ((p, ot, 'all'), (p, ot, first)):
                    d = B[key]
                    d['pers'].append(len(st.pers))
                    d['foll'].append(sum(1 for c_ in st.home if c_.type == 'Follower'))
                    d['top'].append(max([c_.force or 0 for c_ in st.pers] or [0]))
                    d['force'].append(st.force)
                    d['hold'].append(st.holdings)
                    d['cap'].append(st.gold)
            if st is not None and so is not None and 2 <= ot <= 6 and complete:
                row = [n for _, n in st.row if n != 'down']
                hp = [n for n in row if L.CAT.type(n) == 'Personality' and L.CAT.stat(n, 'cost', 99) <= st.gold]
                hh = [n for n in row if L.CAT.type(n) == 'Holding' and L.CAT.stat(n, 'cost', 99) <= st.gold]
                if hp and hh:
                    buys = [x for x in t.purchases if x[0] == s]
                    np_, nh = len(st.pers), len(so.pers)
                    sit = 'a: behind on Personalities' if np_ < nh else 'b: level' if np_ == nh else 'c: ahead'
                    for key in ((p, 'Personalities in play', sit),
                                (p, 'opponent attacked last turn', 'a: yes' if prev_attacked[s] else 'b: no')):
                        x = C[key]
                        x['n'] += 1
                        ft = buys[0][2] if buys else 'nothing'
                        x['first:' + (ft if ft in ('Personality', 'Holding', 'nothing') else 'other')] += 1
                        x['pers'] += sum(1 for b in buys if b[2] == 'Personality')
                        x['hold'] += sum(1 for b in buys if b[2] == 'Holding')
                        x['anyP'] += any(b[2] == 'Personality' for b in buys)
                        x['onlyH'] += bool(buys) and all(b[2] == 'Holding' for b in buys)
                        x['cap'] += st.gold
            if st is not None and 3 <= ot <= 8 and complete and s in g.turns[i + 1].status:
                # 7d: a Personality that was face-up in the row at this header, is still there at the next
                # header, and whose printed cost fits in the capacity the seat did not produce this turn
                left = max(0, st.gold - t.gold[s][1])
                before = collections.Counter(n for _, n in st.row if n != 'down')
                after = collections.Counter(n for _, n in g.turns[i + 1].status[s].row if n != 'down')
                still = [n for n in (before & after) if L.CAT.type(n) == 'Personality']
                ok = [n for n in still if L.CAT.stat(n, 'cost', 99) <= left and L.CAT.stat(n, 'honor', 0) <= st.honor]
                for key in ((p, 'all'), (p, 'behind on Personalities' if so is not None and len(st.pers) < len(so.pers)
                                         else 'level or ahead')):
                    x = D[key]
                    x['n'] += 1
                    x['still'] += bool(still)
                    x['ok'] += bool(ok)
                    x['left'] += left
                    x['left_ok'] += left if ok else 0
                    x['force'] += max([L.CAT.stat(n, 'force', 0) for n in ok] or [0])
            if st is not None and 2 <= ot <= 6 and complete and s in g.turns[i + 1].status:
                # 7f: an affordable Personality stayed in the row, none was bought, Gold went to Fate cards
                before = collections.Counter(n for _, n in st.row if n != 'down')
                after = collections.Counter(n for _, n in g.turns[i + 1].status[s].row if n != 'down')
                aff = [n for n in (before & after) if L.CAT.type(n) == 'Personality'
                       and L.CAT.stat(n, 'cost', 99) <= st.gold and L.CAT.stat(n, 'honor', 0) <= st.honor]
                buys = [b for b in t.purchases if b[0] == s]
                fate = t.gold[s][0] - sum(b[3] for b in buys)
                nop = not any(b[2] == 'Personality' for b in buys)
                x = F[(p, 'a: 0-1 Personalities in play' if len(st.pers) <= 1 else 'b: 2+ Personalities in play')]
                x['n'] += 1
                x['aff'] += bool(aff)
                x['aff_nop'] += bool(aff) and nop
                x['starved'] += bool(aff) and nop and fate >= 4
                x['fate'] += fate
            if st is not None and 4 <= ot <= 8 and complete:
                # 7e: Holdings bought by a seat that already left Gold unproduced on its previous own turn
                pl = prev_left[s]
                if pl is not None:
                    x = E[(p, 'a: 0-2' if pl <= 2 else 'b: 3-5' if pl <= 5 else 'c: 6-9' if pl <= 9 else 'd: 10+')]
                    nh = sum(1 for b in t.purchases if b[0] == s and b[2] == 'Holding')
                    x['n'] += 1
                    x['hold'] += nh
                    x['any'] += nh > 0
                    x['gold'] += sum(b[3] for b in t.purchases if b[0] == s and b[2] == 'Holding')
            if st is not None and complete:
                prev_left[s] = max(0, st.gold - t.gold[s][1])
            # prev_attacked[x] = "x was attacked on the opponent's most recent turn"
            prev_attacked[1 - s] = L.has_attack(t)

    return A, B, C, D, E, F


def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    A, B, C, D, E, F = collect(games)
    print('## Q7a  Economy per own turn (means per turn; games\' final turns excluded)\n')
    rows = []
    for p in P:
        for ot in range(1, 9):
            d = A[(p, ot)]
            n = len(d['cap'])
            if not n:
                continue
            f = (lambda k: '%d' % sum(d[k])) if p in L.SMALL else (lambda k: L.mean(d[k], 2 if k in ('hold', 'pers') else 1))
            rows.append([p, ot, n, f('cap'), f('prod'), f('paid'), f('exc'), f('unused'), f('hold'), f('pers'), f('fate')])
    L.table(['player', 'own turn', 'turns', 'Gold capacity at header', 'Gold produced', 'Gold paid', 'excess lost',
             'capacity not produced', 'Holdings bought', 'Personalities bought', 'Gold paid outside Dynasty'], rows)
    print('(Human and PracticeBot rows are sums over the listed turns, not means.)\n')
    print('## Q7a-sum  Totals over own turns 1-8\n')
    rows = []
    for p in P:
        tot = collections.Counter()
        n = 0
        for ot in range(1, 9):
            d = A[(p, ot)]
            n += len(d['cap'])
            for k in d:
                tot[k] += sum(d[k])
        if n:
            rows.append([p, n, tot['prod'], L.pct(tot['exc'], tot['prod'], 1), L.pct(tot['unused'], tot['cap'], 1),
                         '%.2f' % (tot['hold'] / n), '%.2f' % (tot['pers'] / n),
                         L.pct(tot['fate'], tot['paid'])])
    L.table(['player', 'turns', 'Gold produced', 'excess lost / produced', 'capacity not produced / capacity',
             'Holdings bought per turn', 'Personalities bought per turn', 'share of Gold paid outside Dynasty'], rows)
    print('## Q7b  Board at the start of own turns 3, 5 and 7\n')
    rows = []
    for p in P:
        for ot in (3, 5, 7):
            for w in ('all', 'first player', 'second player'):
                d = B[(p, ot, w)]
                n = len(d['pers'])
                if n and not (p in L.SMALL and w != 'all'):
                    rows.append([p, ot, w, n, L.mean(d['pers'], 2), L.mean(d['foll'], 2), L.mean(d['force']),
                                 L.mean(d['hold'], 2), L.mean(d['cap'])])
    L.table(['player', 'own turn', 'seat order', 'turns', 'Personalities in play (mean)', 'Followers in play (mean)',
             'Force in play (mean)', 'Holdings (mean)', 'Gold capacity (mean)'], rows)
    print('## Q7c  Dynasty choice when an affordable Personality and a Holding were both in the row (own turns 2-6)\n')
    rows = []
    for (p, dim, sit), x in sorted(C.items(), key=lambda z: (P.index(z[0][0]), z[0][1], z[0][2])):
        n = x['n']
        rows.append([p, dim, sit[3:], n, '%.1f' % (x['cap'] / n), L.rate(x['first:Personality'], n, p),
                     L.rate(x['first:Holding'], n, p), L.rate(x['anyP'], n, p), L.rate(x['onlyH'], n, p),
                     '%.2f' % (x['pers'] / n), '%.2f' % (x['hold'] / n)])
    L.table(['player', 'split by', 'situation', 'turns', 'Gold capacity (mean)', 'first purchase a Personality',
             'first purchase a Holding', 'bought >=1 Personality', 'bought only Holdings',
             'Personalities bought (mean)', 'Holdings bought (mean)'], rows)
    print('## Q7d  A Personality left in the row that the unproduced Gold would have paid for (own turns 3-8)\n')
    rows = []
    for (p, sit), x in sorted(D.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        n = x['n']
        rows.append([p, sit, n, '%.1f' % (x['left'] / n), L.rate(x['still'], n, p), L.rate(x['ok'], n, p),
                     '%.1f' % (x['left_ok'] / x['ok']) if x['ok'] else '-',
                     '%.1f' % (x['force'] / x['ok']) if x['ok'] else '-'])
    L.table(['player', 'situation', 'turns', 'capacity not produced (mean)', 'a face-up Personality stayed unbought',
             '... and its printed cost fitted in the unproduced Gold', 'unproduced Gold on those turns (mean)',
             'printed Force of the best such Personality (mean)'], rows)
    print('## Q7e  Holdings bought on own turns 4-8, by the Gold capacity left unproduced on the previous own turn\n')
    rows = []
    for (p, bk), x in sorted(E.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        n = x['n']
        rows.append([p, bk[3:], n, L.rate(x['any'], n, p), '%.2f' % (x['hold'] / n), '%.1f' % (x['gold'] / n)])
    L.table(['player', 'Gold unproduced on the previous own turn', 'turns', 'bought >=1 Holding',
             'Holdings bought (mean)', 'Gold paid for Holdings (mean)'], rows)
    print('## Q7f  Gold spent on Fate cards while an affordable Personality stayed in the row (own turns 2-6)\n')
    rows = []
    for (p, bk), x in sorted(F.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        n = x['n']
        rows.append([p, bk[3:], n, '%.1f' % (x['fate'] / n), L.rate(x['aff'], n, p), L.rate(x['aff_nop'], n, p),
                     L.rate(x['starved'], n, p)])
    L.table(['player', 'board at the header', 'turns', 'Gold paid outside Dynasty (mean)',
             'a Personality within Gold capacity stayed unbought', '... and no Personality was bought that turn',
             '... and >=4 Gold went to attachments / actions instead'], rows)


if __name__ == '__main__':
    main(sys.argv[1:])
