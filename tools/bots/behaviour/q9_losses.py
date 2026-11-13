#!/usr/bin/env python3
"""q9_losses.py - how the challenger's losses happen (question 9).

Usage: python3 q9_losses.py <logs-dir>... [--player NAME] [--files]

Default player: Conan (the challenger in logs-r<NN> directories).  `--files`
appends one line per lost game (class, turns of each Province fall, file) so
games can be picked for reading.

For every Province of the player destroyed in a battle:
  defended and lost   the player had a unit at that battlefield
  declined            no unit there although >=1 unbowed Personality at the
                      TURN header was never assigned to any battlefield
  no guard            no unit there and no unbowed Personality left to assign
                      (after attack: the player attacked on its previous own
                       turn, so its attackers were still bowed;
                       no attack: it did not)
"Unbowed" = unbowed at the TURN header and not bowed / destroyed by a targeted
action of the attacker in the Action phase before the attack
(l5rlog.free_defenders).  Mass or untargeted bowing is not seen, so a few
'declined' falls were not real choices (approximation; see the report).

Tables
  9a  how the losses end: victory type, length, Provinces the player took
  9b  Province falls by class, in lost games and (for contrast) in won games
  9c  the turn of the 1st..4th fall and how many fell in one turn
  9d  what the player did on its own turn before each fall
  9e  Honor losses: the race (the log ends on the loser's last own turn; the
      Honor victory is declared at the start of the winner's next turn)
  9f  taxonomy of the lost games (mutually exclusive, first match wins)
"""
import collections
import sys

import l5rlog as L


def falls(g, s):
    """[(turn, class, prev choice, guard, n_attackers, paperhold)] for each Province of seat s destroyed in battle."""
    out = []
    prev_choice = None
    for t in g.turns:
        if t.active == s:
            prev_choice = L.choice(t) if s in t.status else None
            if prev_choice is None:
                prev_choice = 'no unbowed Personality'
            continue
        st = t.status.get(s)
        for a in t.attacks:
            if a.defender != s:
                continue
            idle = (len(L.free_defenders(t, a)) - len(a.defenders)) if st else 0
            for p_ in a.order or sorted(a.battles):
                b = a.battles.get(p_)
                if b is None or not b.destroyed:
                    continue
                if b.def_assigned:
                    k = 'defended and lost'
                elif idle > 0:
                    k = 'declined (unbowed Personality left at home)'
                elif prev_choice in ('partial', 'all-in'):
                    k = 'no guard after own attack'
                else:
                    k = 'no guard, did not attack'
                out.append((t.n, k, prev_choice, len(st.unbowed_pers) if st else 0, len(st.pers) if st else 0))
    return out


def classify_game(g, s, fl):
    o = 1 - s
    last = g.turns[-1]
    took = 4 - (last.status[o].provinces if o in last.status else 4)
    took += L.provinces_lost_on(last, o)
    if g.vclass == 'Dishonor':
        return 'H0 lost by own Dishonor'
    if g.vclass == 'Honor':
        return 'H1 Honor race lost: took >=2 Provinces first' if took >= 2 else 'H2 Honor race lost: took <=1 Province'
    k = collections.Counter(f[1] for f in fl)
    n = sum(k.values()) or 1
    ng = k['no guard after own attack']
    if took >= 3:
        return 'M1 lost the Province race by one (took 3)'
    if (k['defended and lost'] + k['declined (unbowed Personality left at home)']) * 2 > n and took <= 1:
        return 'M4 overrun: defences beaten or declined, took <=1'
    if ng * 2 >= n:
        return 'M2 traded Provinces: most fell with the army bowed from its own attack'
    if k['no guard, did not attack'] * 2 >= n:
        return 'M5 no army at home and not attacking (outbuilt or wiped)'
    if (k['defended and lost'] + k['declined (unbowed Personality left at home)']) * 2 > n:
        return 'M3 defences beaten or declined, took 2'
    return 'M6 mixed'


def main(argv):
    files = '--files' in argv
    argv = [a for a in argv if a != '--files']
    player = 'Conan'
    if '--player' in argv:
        i = argv.index('--player')
        player = argv[i + 1]
        del argv[i:i + 2]
    games = [g for g in L.load(argv) if player in list(g.ptype)]
    lost = [g for g in games if g.ptype[1 - g.winner] == player]
    won = [g for g in games if g.ptype[g.winner] == player]
    print('Player: %s. Games %d, lost %d, won %d.\n' % (player, len(games), len(lost), len(won)))

    print('## Q9a  How the losses end\n')
    v = collections.defaultdict(lambda: collections.defaultdict(list))
    for g in lost:
        s = 1 - g.winner
        last = g.turns[-1]
        took = 4 - last.status[1 - s].provinces + L.provinces_lost_on(last, 1 - s) if (1 - s) in last.status else 0
        d = v[g.vclass]
        d['turn'].append(g.end_turn or last.n)
        d['took'].append(took)
        d['honor'].append(last.status[s].honor if s in last.status else 0)
        d['ohonor'].append(last.status[1 - s].honor if (1 - s) in last.status else 0)
        d['first'].append(g.turns[0].active == s)
    L.table(['lost by', 'games', 'share', 'last turn (median)', 'Provinces the player had taken (mean)',
             'had taken >=2', 'had taken 3', 'player went first'],
            [[k, len(d['turn']), L.pct(len(d['turn']), len(lost)), L.median(d['turn']), L.mean(d['took'], 2),
              L.rate(sum(1 for x in d['took'] if x >= 2), len(d['took'])),
              L.rate(sum(1 for x in d['took'] if x >= 3), len(d['took'])),
              L.rate(sum(d['first']), len(d['first']))] for k, d in sorted(v.items())])

    print('## Q9b  How the player\'s Provinces fell (each Province destroyed in a battle)\n')
    rows = []
    cls = ['defended and lost', 'declined (unbowed Personality left at home)', 'no guard after own attack',
           'no guard, did not attack']
    allf = {}
    for lab, gs in (('lost games', lost), ('won games', won)):
        k = collections.Counter()
        for g in gs:
            s = g.seat_of(player)[0]
            fl = falls(g, s)
            allf[g.path] = fl
            for f in fl:
                k[f[1]] += 1
        n = sum(k.values())
        rows.append([lab, len(gs), n] + [('%d (%s)' % (k[c], L.pct(k[c], n))) for c in cls])
    L.table(['games', 'n games', 'Provinces lost in battle'] + cls, rows)

    print('## Q9c  When the Provinces fell (lost games, Military)\n')
    order = collections.defaultdict(list)
    same = collections.Counter()
    for g in lost:
        if g.vclass != 'Military':
            continue
        fl = allf[g.path]
        for i, f in enumerate(fl[:4]):
            order[i].append(f[0])
        pert = collections.Counter(f[0] for f in fl)
        same[max(pert.values()) if pert else 0] += 1
    L.table(['fall', 'n', 'global turn: median', 'by turn 6', 'by turn 8', 'by turn 10', 'turn 13 or later'],
            [['#%d' % (i + 1), len(xs), L.median(xs), L.pct(sum(1 for x in xs if x <= 6), len(xs)),
              L.pct(sum(1 for x in xs if x <= 8), len(xs)), L.pct(sum(1 for x in xs if x <= 10), len(xs)),
              L.pct(sum(1 for x in xs if x >= 13), len(xs))] for i, xs in sorted(order.items())])
    L.table(['most Provinces lost in a single turn', 'Military losses'],
            [[k, '%d (%s)' % (n, L.pct(n, sum(same.values())))] for k, n in sorted(same.items())])

    print('## Q9d  The player\'s own turn before each fall\n')
    rows = []
    for lab, gs in (('lost games', lost), ('won games', won)):
        k = collections.Counter()
        guard = collections.defaultdict(list)
        for g in gs:
            for f in allf[g.path]:
                k[f[2]] += 1
                guard[f[2]].append(f[3])
        n = sum(k.values())
        for c in ('all-in', 'partial', 'none', 'no unbowed Personality'):
            if k[c]:
                rows.append([lab, c, k[c], L.pct(k[c], n), L.mean(guard[c], 2)])
    L.table(['games', 'own previous turn', 'Provinces then lost', 'share', 'unbowed Pers. at home when attacked (mean)'],
            rows)

    print('## Q9e  Honor losses\n')
    rows = []
    for g in lost:
        if g.vclass == 'Military':
            continue
        s = 1 - g.winner
        hs = [(t.n, t.status[s].honor, t.status[1 - s].honor, t.status[1 - s].provinces) for t in g.turns
              if s in t.status and (1 - s) in t.status]
        t30 = next((n for n, _, oh, _ in hs if oh >= 30), None)
        prov30 = next((pv for n, _, oh, pv in hs if oh >= 30), None)
        att_after = sum(1 for t in g.turns if t30 and t.n >= t30 and t.active == s and L.has_attack(t))
        dec_after = sum(1 for t in g.turns if t30 and t.n >= t30 and t.active == s and L.choice(t) is not None)
        allin_after = sum(1 for t in g.turns if t30 and t.n >= t30 and t.active == s and L.choice(t) == 'all-in')
        rows.append((g.vclass, hs[-1][0], hs[-1][1], hs[-1][2],
                     4 - hs[-1][3] + L.provinces_lost_on(g.turns[-1], 1 - s), t30, prov30, att_after, dec_after,
                     g.decks[1 - s], allin_after))
    k = collections.Counter(r[9] for r in rows if r[0] == 'Honor')
    print('Opponent decks in the Honor losses: ' + ', '.join('%s %d' % x for x in k.most_common()) + '\n')
    hon = [r for r in rows if r[0] == 'Honor']
    if hon:
        L.table(['Honor losses', 'last turn (median)', 'player\'s final Honor (median)',
                 'Provinces taken by the end (mean)', 'turn the opponent passed 30 Honor (median)',
                 'opponent Provinces left at that point (mean)', 'own decisions after that point',
                 'attacked on those', 'all-in on those'],
                [[len(hon), L.median([r[1] for r in hon]), L.median([r[2] for r in hon]),
                  L.mean([r[4] for r in hon], 2), L.median([r[5] for r in hon if r[5]]),
                  L.mean([r[6] for r in hon if r[6] is not None], 2), sum(r[8] for r in hon),
                  L.rate(sum(r[7] for r in hon), sum(r[8] for r in hon)),
                  L.rate(sum(r[10] for r in hon), sum(r[8] for r in hon))]])

    print('## Q9f  Taxonomy of the lost games\n')
    tax = collections.defaultdict(list)
    for g in lost:
        s = 1 - g.winner
        tax[classify_game(g, s, allf[g.path])].append(g)
    L.table(['class', 'games', 'share of losses'],
            [[k, len(v_), L.pct(len(v_), len(lost))] for k, v_ in sorted(tax.items())])
    if files:
        print('## Lost games by class\n')
        for k, v_ in sorted(tax.items()):
            print('### %s (%d)\n' % (k, len(v_)))
            for g in sorted(v_, key=lambda x: x.name):
                fl = allf[g.path]
                short = {'defended and lost': 'D', 'declined (unbowed Personality left at home)': 'X',
                         'no guard after own attack': 'A', 'no guard, did not attack': 'N'}
                print('- %s/%s  end T%s  falls %s' % (g.arm, g.name, g.end_turn,
                                                       ' '.join('T%d%s' % (f[0], short[f[1]]) for f in fl)))
            print()


if __name__ == '__main__':
    main(sys.argv[1:])
