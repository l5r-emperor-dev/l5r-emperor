#!/usr/bin/env python3
"""digest.py - condensed turn-by-turn view of one or more game logs.

Usage: python3 digest.py <log-file-or-dir>... [from-to]

Prints, per turn, what the parser understood: both seats' Honor / Provinces /
Personalities (unbowed marked, bowed with *), who attacked with what and
where, defence assignments by segment, every Battle action with its class
(home/bow/destroy/reduce/add/negate/strength/other), the engine's resolution
Forces, the parser's pre-battle Force estimate, Province and unit losses and
the Dynasty purchases.  It exists to hand-check the parser against the raw log
and to read games quickly; it computes no statistics.
"""
import re
import sys

import l5rlog as L


def fmt_status(g, t, s):
    st = t.status.get(s)
    if st is None:
        return '  s%d (no status)' % s
    pers = ' '.join(repr(c) for c in st.home if c.type in ('Personality', 'Follower'))
    row = ' '.join('%d:%s' % (a, b) for a, b in st.row)
    return ('  s%d %-18s H%-3d prov %d hand %d gold %d hold %d | %s\n       hand: %s | row: %s'
            % (s, g.ptype[s], st.honor, st.provinces, st.hand_n, st.gold, st.holdings, pers,
               ', '.join(st.hand), row))


def digest(g, lo=1, hi=999, out=sys.stdout):
    w = out.write
    w('=== %s/%s\n    s0 %s (%s)  vs  s1 %s (%s); winner s%s by %s on turn %s\n'
      % (g.arm, g.name, g.decks[0], g.ptype[0], g.decks[1], g.ptype[1], g.winner, g.victory, g.end_turn))
    for t in g.turns:
        if not lo <= t.n <= hi:
            continue
        w('--- TURN %d (own turn %d) active s%d %s; Favor: %s\n'
          % (t.n, L.own_turn(t.n), t.active, g.ptype[t.active], 's%d' % t.favor if t.favor is not None else '-'))
        for s in (0, 1):
            w(fmt_status(g, t, s) + '\n')
        pre = [c for c in t.cmds if c.phase == 'Action' and c.verb not in ('pass',)]
        if pre:
            w('  Action phase: ' + '; '.join(
                's%d %s%s%s' % (c.seat, c.verb, '/' + c.ability if c.ability else '',
                                ' ' + ' '.join('%s=%s' % kv for kv in c.args.items()) if c.args else '')
                for c in pre) + '\n')
        if not t.attacks:
            w('  no attack (%s)\n' % ('declared, nothing assigned' if t.declared else 'passed'))
        for a in t.attacks:
            w('  ATTACK%s by s%d: inf %s | cav %s (cav segment offered: %s)\n'
              % (' (additional phase)' if a.extra else '', a.attacker,
                 ', '.join('%s>p%d' % x for x in a.inf) or '-', ', '.join('%s>p%d' % x for x in a.cav) or '-',
                 a.cav_option))
            w('    defence: inf %s | cav %s\n'
              % (', '.join('%s>p%d' % x for x in a.dfn) or '-', ', '.join('%s>p%d' % x for x in a.cavdef) or '-'))
            for p in sorted(a.battles):
                b = a.battles[p]
                if not b.real():
                    continue
                w('    p%d (strength %s%+d): est %s v %s%s' % (p, b.strength, b.strength_mod, b.est_A, b.est_D,
                                                           ' (uncertain)' if b.est_uncertain else ''))
                w(' -> engine %s v %s; %s; attackers lost %s; defenders lost %s\n'
                  % (b.A, b.D, 'PROVINCE DESTROYED' if b.destroyed else 'held',
                     ', '.join(b.att_lost) or '-', ', '.join(b.def_lost) or '-'))
                for c, k in L.battle_actions(b):
                    role = 'att' if c.seat == a.attacker else 'def'
                    w('       %s s%d [%s] %s card=%s target=%s by=%s\n'
                      % (role, c.seat, k, c.ability, c.args.get('card'), c.args.get('target'), c.args.get('by')))
        for s in (0, 1):
            if t.unit_losses[s]:
                w('  s%d lost outside battles: %s\n' % (s, ', '.join(t.unit_losses[s])))
        if t.purchases:
            w('  bought: ' + ', '.join('s%d %s [%s] paid %d (excess %d)' % (s, c, ty, pd, ex)
                                       for s, c, ty, pd, pr, ex in t.purchases) + '\n')
        for s in (0, 1):
            gs = t.gold[s]
            if gs[0]:
                w('  s%d gold: paid %d, produced %d, excess lost %d\n' % (s, gs[0], gs[1], gs[2]))
    w('\n')


if __name__ == '__main__':
    args = sys.argv[1:]
    lo, hi = 1, 999
    if args and re.match(r'^\d+-\d+$', args[-1]):
        lo, hi = (int(x) for x in args.pop().split('-'))
    for g in L.load(args):
        digest(g, lo, hi)
