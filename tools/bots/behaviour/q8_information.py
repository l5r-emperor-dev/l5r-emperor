#!/usr/bin/env python3
"""q8_information.py - does behaviour change after the opponent reveals something? (question 8)

Usage: python3 q8_information.py <logs-dir>...

Each table compares the same player type in the same kind of situation with
and without a piece of public information.

  8a  Removal seen.  Pairs of consecutive attack decisions by one seat: on
      own turn A it attacked and the defender did / did not use a
      neutralising Battle action (send home, bow, destroy, reduce Force) in a
      battle; on its next own turn B (>=2 unbowed Personalities at the
      header) - does it attack, what share of its unbowed Personalities does
      it commit, does it keep a guard?  Split by whether attack A destroyed a
      Province, because failure also changes the position.
  8b  The Imperial Favor.  The Favor is public and its holder can discard it
      in battle to send one attacker home, so an attack by a single
      Personality into a Favor holder with any defence is likely wasted.
      Attack decisions with >=1 unbowed Personality: attack rate, share of
      attacks made with one Personality, and what the defender did with the
      Favor, by whether the defender held it.
  8c  Attacked last turn.  Own turns 2-6 with an affordable Personality and a
      Holding both in the row: purchase mix by what happened on the
      opponent's last turn (not attacked / attacked, no Province lost /
      Province lost), and the next attack decision.
  8d  Defender's hand size (public).  Own turns 3-8, the two sides within 6
      Force of each other: attack rate and all-in share by the number of
      cards in the defender's hand.

Limits: the Favor holder is tracked from "took / discarded the Imperial
Favor" events attributed to the seat of the command they follow.  8a's
"neutralising" uses the ability classification in l5rlog.py.  Human rows are
counts (7 games).
"""
import collections
import sys

import l5rlog as L




def main(argv):
    games = L.load(argv)
    P = L.ptypes(games)
    A = collections.defaultdict(collections.Counter)
    B = collections.defaultdict(collections.Counter)
    C = collections.defaultdict(collections.Counter)
    D = collections.defaultdict(collections.Counter)
    for g in games:
        last_attack = {0: None, 1: None}       # seat -> (saw removal?, destroyed?) for its most recent own turn
        attacked_prev = {0: None, 1: None}     # seat -> what the opponent's last turn did to it
        for i, t in enumerate(g.turns):
            s = t.active
            p = g.ptype[s]
            st, so = t.status.get(s), t.status.get(1 - s)
            if st is None or so is None:
                continue
            ot = L.own_turn(t.n)
            ch = L.choice(t)
            nub = len(st.unbowed_pers)
            # ---- 8a
            la = last_attack[s]
            if la is not None and nub >= 2:
                x = A[(p, 'a: last attack took a Province' if la[1] else 'b: last attack took nothing',
                       'defender used removal' if la[0] else 'no removal used')]
                x['n'] += 1
                x['attack'] += ch != 'none'
                x['allin'] += ch == 'all-in'
                if ch != 'none':
                    x['att_n'] += 1
                    x['commit'] += min(1.0, len(t.attack.assigned) / nub)
                    x['one'] += len(t.attack.assigned) == 1
                x['unb'] += nub
            if L.has_attack(t):
                a = t.attack
                rem = False
                for b in a.battles.values():
                    for c, k in L.battle_actions(b):
                        if c.seat == a.defender and k in L.NEUTRALISING:
                            rem = True
                last_attack[s] = (rem, bool(a.destroyed))
            else:
                last_attack[s] = None
            # ---- 8b
            if ch is not None:
                held = t.favor == 1 - s
                x = B[(p, 'defender holds the Favor' if held else 'defender does not')]
                x['n'] += 1
                x['attack'] += ch != 'none'
                if ch != 'none':
                    a = t.attack
                    x['att_n'] += 1
                    one = len(a.assigned) == 1
                    x['one'] += one
                    guard = len(so.unbowed_pers) > 0
                    x['one_guard'] += one and guard
                    fav = any(c.ability == 'favor-battle' and c.seat == a.defender
                              for b in a.battles.values() for c in b.cmds)
                    x['fav_used'] += fav
                    x['one_fav'] += one and fav
                    x['nothing'] += (not a.destroyed) and a.def_lost == 0
                    x['one_nothing'] += one and (not a.destroyed) and a.def_lost == 0
            # ---- 8c
            ap = attacked_prev[s]
            if ap is not None and 2 <= ot <= 6 and i < len(g.turns) - 1:
                row = [n for _, n in st.row if n != 'down']
                hp = [n for n in row if L.CAT.type(n) == 'Personality' and L.CAT.stat(n, 'cost', 99) <= st.gold]
                hh = [n for n in row if L.CAT.type(n) == 'Holding' and L.CAT.stat(n, 'cost', 99) <= st.gold]
                if hp and hh:
                    buys = [b for b in t.purchases if b[0] == s]
                    x = C[(p, ap)]
                    x['n'] += 1
                    x['pers'] += sum(1 for b in buys if b[2] == 'Personality')
                    x['hold'] += sum(1 for b in buys if b[2] == 'Holding')
                    x['anyP'] += any(b[2] == 'Personality' for b in buys)
                    x['onlyH'] += bool(buys) and all(b[2] == 'Holding' for b in buys)
                    x['cap'] += st.gold
                    if ch is not None:
                        x['dec'] += 1
                        x['attack'] += ch != 'none'
                        x['allin'] += ch == 'all-in'
            if L.has_attack(t):
                attacked_prev[1 - s] = 'c: attacked, lost a Province' if t.attack.destroyed else \
                    'b: attacked, no Province lost'
            else:
                attacked_prev[1 - s] = 'a: not attacked'
            # ---- 8d
            if ch is not None and 3 <= ot <= 8 and abs(st.unbowed_force - so.unbowed_force) <= 6 and nub >= 2:
                h = so.hand_n
                x = D[(p, 'a: 0-2 cards' if h <= 2 else 'b: 3-4' if h <= 4 else 'c: 5-6' if h <= 6 else 'd: 7+')]
                x['n'] += 1
                x['attack'] += ch != 'none'
                x['allin'] += ch == 'all-in'
                if ch != 'none':
                    x['att_n'] += 1
                    x['dest'] += bool(t.attack.destroyed)
                    x['lost'] += t.attack.att_lost > 0

    print('## Q8a  The next attack decision after the defender did / did not show a removal action\n')
    rows = []
    for (p, res, rem), x in sorted(A.items(), key=lambda z: (P.index(z[0][0]), z[0][1], z[0][2])):
        n = x['n']
        rows.append([p, res[3:], rem, n, '%.1f' % (x['unb'] / n), L.rate(x['attack'], n, p), L.rate(x['allin'], n, p),
                     L.rate(x['one'], x['att_n'], p),
                     ('%.0f%%' % (100 * x['commit'] / x['att_n'])) if x['att_n'] else '-'])
    L.table(['attacker', 'previous attack', 'what the defender showed', 'next decisions (>=2 unbowed Pers.)',
             'unbowed Pers. (mean)', 'attacks again', 'all-in', 'attacks with one Personality',
             'share of unbowed Pers. committed when attacking'], rows)
    print('## Q8b  Attacking into the Imperial Favor\n')
    rows = []
    for (p, held), x in sorted(B.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        n = x['n']
        rows.append([p, held, n, L.rate(x['attack'], n, p), x['att_n'], L.rate(x['one'], x['att_n'], p),
                     L.rate(x['fav_used'], x['att_n'], p), L.rate(x['one_fav'], x['one'], p),
                     L.rate(x['one_nothing'], x['one'], p)])
    L.table(['attacker', 'Favor', 'decisions', 'attacks', 'attacks (n)', 'attacks with one Personality',
             'defender discarded the Favor to send an attacker home', '... in one-Personality attacks',
             'one-Personality attacks that achieved nothing'], rows)
    print('## Q8c  After being attacked: purchases and the next attack decision (own turns 2-6, a Personality and a '
          'Holding both affordable in the row)\n')
    rows = []
    for (p, ap), x in sorted(C.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        n = x['n']
        rows.append([p, ap[3:], n, '%.1f' % (x['cap'] / n), L.rate(x['anyP'], n, p), L.rate(x['onlyH'], n, p),
                     '%.2f' % (x['pers'] / n), '%.2f' % (x['hold'] / n), L.rate(x['attack'], x['dec'], p),
                     L.rate(x['allin'], x['dec'], p)])
    L.table(['player', 'opponent\'s last turn', 'turns', 'Gold capacity (mean)', 'bought >=1 Personality',
             'bought only Holdings', 'Personalities bought (mean)', 'Holdings bought (mean)',
             'attacks this turn (given unbowed Pers.)', 'all-in'], rows)
    print('## Q8d  Attack decision by the defender\'s hand size (own turns 3-8, unbowed Force within 6 of each other, '
          '>=2 unbowed Personalities)\n')
    rows = []
    for (p, hb), x in sorted(D.items(), key=lambda z: (P.index(z[0][0]), z[0][1])):
        n = x['n']
        rows.append([p, hb[3:], n, L.rate(x['attack'], n, p), L.rate(x['allin'], n, p),
                     L.rate(x['dest'], x['att_n'], p), L.rate(x['lost'], x['att_n'], p)])
    L.table(['attacker', 'cards in defender\'s hand', 'decisions', 'attacks', 'all-in',
             'attack destroys >=1 Province', 'attacker loses >=1 Personality'], rows)


if __name__ == '__main__':
    main(sys.argv[1:])
