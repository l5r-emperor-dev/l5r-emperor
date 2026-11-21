#!/usr/bin/env python3
"""q4_battle_actions.py - neutralising actions in battle (question 4).

Usage: python3 q4_battle_actions.py <logs-dir>... [--table]

Battle actions are classified from the ability id and the engine's
description (see l5rlog.classify): home / bow / destroy / reduce (the four
"neutralising" classes), add, negate, strength, other.  `--table` prints the
whole id -> class table with counts so the classification can be reviewed.

Tables
  4a  actions per contested battle (both sides have units) by class, player
      type and role
  4b  actions per game by class and player type
  4c  battles decided against the paper result.  Paper result = pre-battle
      estimate (est. attacking F - est. defending F > Strength => falls).
      Only battles whose paper margin is at least 3 away from the threshold
      are used, because the estimate is exact in ~72% and within 2 in ~85%
      of battles without actions.
        paper-fall held   : the defender's actions (or the attacker's
                            failure to answer) saved the Province
        paper-hold fell   : the attacker's actions broke it
  4d  who acts last / who runs out: neutralising actions by each side in
      contested battles, and the Province result by who used more
  4e  neutralising Strategy cards left in the defender's hand at the moment a
      Province fell, and in the loser's hand at the last TURN header.  Hand =
      hand at the header minus cards played since.  A card counts when the
      logs show it used as a home/bow/destroy/reduce Battle action anywhere.
      Whether it was playable (legal target, a performing Personality present,
      Gold) is NOT known - an upper bound on missed plays, split by whether
      the defender had a unit at that battlefield and, if not, whether an
      unbowed Personality was left unassigned at home (most of these cards
      need a performing Personality at the battlefield).

Human rows are counts (7 games).
"""
import collections
import sys

import l5rlog as L


def neutral_strategy_cards(games):
    """Strategy cards that the logs show used as a home/bow/destroy/reduce Battle action more often than not.
    Derived from the games given, so the set depends on which logs are loaded together."""
    cc = L.card_classes(games)
    return set(c for c, k in cc.items()
               if L.CAT.type(c) == 'Strategy' and sum(k[x] for x in L.NEUTRALISING) * 2 > sum(k.values()))


def collect(games, neutral_cards):
    """Accumulators of every table in this script, from parsed games (profile.py reuses them)."""
    per = collections.defaultdict(collections.Counter)    # (ptype, role) -> class -> n
    nb = collections.Counter()                            # (ptype, role) -> contested battles
    pg = collections.defaultdict(collections.Counter)     # ptype -> class -> n ; '_games'
    flip = collections.defaultdict(lambda: [0, 0, 0])     # (kind, ptype) -> n, flipped, flipper used neutraliser
    more = collections.defaultdict(lambda: [0, 0])        # (attacker ptype, who used more) -> n, destroyed
    hand_fall = collections.defaultdict(lambda: [0, 0, 0])   # (ptype, defended?) -> falls, falls with >=1, cards
    hand_end = collections.defaultdict(lambda: [0, 0, 0])
    for g in games:
        for s in (0, 1):
            pg[g.ptype[s]]['_games'] += 1
        for t in g.turns:
            played = {0: collections.Counter(), 1: collections.Counter()}
            for a in t.attacks:
                pa, pd = g.ptype[a.attacker], g.ptype[a.defender]
                for p_ in a.order or sorted(a.battles):
                    b = a.battles.get(p_)
                    if b is None:
                        continue
                    acts = L.battle_actions(b)
                    for c, k in acts:
                        pg[g.ptype[c.seat]][k] += 1
                        if c.args.get('card'):
                            played[c.seat][c.args['card']] += 1
                    if b.destroyed and a.defender in t.status:
                        hand = collections.Counter(t.status[a.defender].hand) - played[a.defender]
                        k = sum(v for c, v in hand.items() if c in neutral_cards)
                        idle = len(L.free_defenders(t, a)) - len(a.defenders)
                        x = hand_fall[(pd, 'a: had a defender there' if b.def_assigned else
                                       'b: undefended, >=1 unbowed Personality left unassigned at home' if idle > 0
                                       else 'c: undefended, no unbowed Personality left')]
                        x[0] += 1
                        x[1] += k > 0
                        x[2] += k
                    if not (b.att_assigned and b.def_assigned):
                        continue
                    nb[(pa, 'attacker')] += 1
                    nb[(pd, 'defender')] += 1
                    na = nd = 0
                    for c, k in acts:
                        role = 'attacker' if c.seat == a.attacker else 'defender'
                        per[(g.ptype[c.seat], role)][k] += 1
                        if k in L.NEUTRALISING:
                            if role == 'attacker':
                                na += 1
                            else:
                                nd += 1
                    who = 'attacker used more' if na > nd else 'defender used more' if nd > na else (
                        'equal (>=1 each)' if na else 'neither used any')
                    more[(pa, pd, who)][0] += 1
                    more[(pa, pd, who)][1] += b.destroyed
                    if b.est_uncertain or b.strength is None or a.extra:
                        continue
                    m = b.est_A - b.est_D - b.strength
                    if m >= 3:
                        x = flip[('paper-fall (margin >= +3): Province held', pd)]
                        x[0] += 1
                        x[1] += (not b.destroyed)
                        x[2] += (not b.destroyed) and nd > 0
                    elif m <= -3:
                        x = flip[('paper-hold (margin <= -3): Province fell', pa)]
                        x[0] += 1
                        x[1] += b.destroyed
                        x[2] += b.destroyed and na > 0
        # loser's hand at the last header
        loser = 1 - g.winner
        last = g.turns[-1]
        if loser in last.status:
            played = collections.Counter(c.args['card'] for c in last.cmds
                                         if c.seat == loser and c.verb == 'useAbility' and c.args.get('card'))
            hand = collections.Counter(last.status[loser].hand) - played
            k = sum(v for c, v in hand.items() if c in neutral_cards)
            x = hand_end[(g.ptype[loser], g.vclass)]
            x[0] += 1
            x[1] += k > 0
            x[2] += k

    return per, nb, pg, flip, more, hand_fall, hand_end


def main(argv):
    show = '--table' in argv
    argv = [a for a in argv if a != '--table']
    games = L.load(argv)
    P = L.ptypes(games)
    neutral_cards = neutral_strategy_cards(games)
    if show:
        tab = collections.Counter()
        txt = {}
        for g in games:
            for t in g.turns:
                for a in t.attacks:
                    for b in a.battles.values():
                        for c, k in L.battle_actions(b):
                            tab[(k, c.ability)] += 1
                            txt[c.ability] = (c.args.get('card') or '', c.text())
        print('## Battle-action classification table\n')
        L.table(['class', 'ability id', 'card', 'uses', 'engine description'],
                [[k, a, txt[a][0], v, txt[a][1][:110].replace('|', '/')]
                 for (k, a), v in sorted(tab.items(), key=lambda x: (L.ACTION_CLASSES.index(x[0][0]), -x[1]))])
        print('Strategy cards counted as neutralisers in 4e: ' + ', '.join(sorted(neutral_cards)) + '\n')
    per, nb, pg, flip, more, hand_fall, hand_end = collect(games, neutral_cards)
    cl = L.ACTION_CLASSES
    print('## Q4a  Battle actions per contested battle (both sides assigned units), by class\n')
    rows = []
    for p in P:
        for role in ('attacker', 'defender'):
            n = nb[(p, role)]
            if n:
                d = per[(p, role)]
                neu = sum(d[k] for k in L.NEUTRALISING)
                if p in L.SMALL:
                    rows.append([p, role, n] + ['%d' % d[k] for k in cl] + ['%d' % neu])
                else:
                    rows.append([p, role, n] + ['%.2f' % (d[k] / n) for k in cl] + ['%.2f' % (neu / n)])
    L.table(['player', 'role', 'contested battles'] + cl + ['neutralising (home+bow+destroy+reduce)'], rows)
    print('(Human and PracticeBot rows are total counts, not per-battle means.)\n')
    print('## Q4b  Battle actions per game, by class (all battles)\n')
    rows = []
    for p in P:
        n = pg[p]['_games']
        d = pg[p]
        neu = sum(d[k] for k in L.NEUTRALISING)
        if p in L.SMALL:
            rows.append([p, n] + ['%d' % d[k] for k in cl] + ['%d' % neu])
        else:
            rows.append([p, n] + ['%.1f' % (d[k] / n) for k in cl] + ['%.1f' % (neu / n)])
    L.table(['player', 'games'] + cl + ['neutralising'], rows)
    print('(Human and PracticeBot rows are total counts over their 7 games.)\n')
    print('## Q4c  Battles decided against the paper result\n')
    rows = []
    for kind in sorted(set(k for k, _ in flip)):
        for p in P:
            x = flip[(kind, p)]
            if x[0]:
                rows.append([kind, ('defender ' if 'fall' in kind else 'attacker ') + p, x[0], L.rate(x[1], x[0], p),
                             L.rate(x[2], x[1], p)])
    L.table(['paper result and what happened instead', 'side that overturned it', 'battles with that paper result',
             'overturned', 'of those, that side used >=1 neutralising action'], rows)
    print('## Q4d  Province result by who used more neutralising actions (contested battles)\n')
    rows = []
    for (pa, pd, who), x in sorted(more.items()):
        if pa in L.SMALL or pd in L.SMALL:
            continue
        rows.append([pa, pd, who, x[0], L.rate(x[1], x[0])])
    L.table(['attacker', 'defender', 'neutralising actions', 'battles', 'Province destroyed'], rows)
    print('## Q4e  Neutralising Strategy cards still in the defender\'s hand when a Province fell\n')
    L.table(['defender', 'battlefield', 'Provinces lost', 'with >=1 such card in hand', 'cards per loss (mean)'],
            [[p, d[3:], x[0], L.rate(x[1], x[0], p), '%.2f' % (x[2] / x[0])]
             for (p, d), x in sorted(hand_fall.items(), key=lambda z: (P.index(z[0][0]), z[0][1])) if x[0]])
    print('## Q4e-end  ... and in the loser\'s hand at the last TURN header of the game\n')
    L.table(['loser', 'lost by', 'games', 'with >=1 such card in hand', 'cards per game (mean)'],
            [[p, v, x[0], L.rate(x[1], x[0], p), '%.2f' % (x[2] / x[0])]
             for (p, v), x in sorted(hand_end.items(), key=lambda z: (P.index(z[0][0]), z[0][1])) if x[0]])


if __name__ == '__main__':
    main(sys.argv[1:])
