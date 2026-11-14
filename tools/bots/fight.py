#!/usr/bin/env python3
"""Family E's profile measures from battle records (L5R_TOURN_BATTLES): what a side does inside its battles.

  fight.py <dir-or-file> [<dir-or-file> ...]

For each record directory, two rows: the challenger seat and the opponent seat, each judged on the battles it fought (as attacker or defender):
  - enemy Personalities destroyed per battle won (attacker: attacking Force > defending; defender: the Province held);
  - battles lost narrowly: as attacker, fought and the Province held by 0 to 3 Force, with cards still in hand at the declaration (late Force unused);
    as defender, fought and the Province fell by 1 to 3 Force with cards in hand;
  - cards in hand at a military loss: the loser's hand at its last recorded attack declaration of a game it lost by military victory (an approximation: the record has no end-of-game hand).
No bot reads these. RULE_FAMILIES.md family E names the first three as what the Fight should move."""
import collections, glob, json, os, sys


def load(path):
    files = glob.glob(os.path.join(path, '*.jsonl')) if os.path.isdir(path) else [path]
    for f in sorted(files):
        with open(f) as fh:
            for line in fh:
                if line.strip():
                    yield json.loads(line)


def pct(n, d):
    return f"{100.0 * n / d:.0f}% ({n}/{d})" if d else "-"


def measure(path):
    m = {s: collections.Counter() for s in ('challenger', 'opponent')}
    last_by_game = {}
    for a in load(path):
        who = lambda seat: 'challenger' if seat == a['challengerSeat'] else 'opponent'
        att, dfd = who(a['attacker']), who(1 - a['attacker'])
        hand_att = a['sides'][a['attacker']]['hand']; hand_dfd = a['sides'][1 - a['attacker']]['hand']
        for b in a['battlefields']:
            if not b['fought'] or b['attackingForce'] is None or b['defendingForce'] is None or not b['defenders']:
                continue
            af, df, s = b['attackingForce'], b['defendingForce'], b['strength']
            c, d = m[att], m[dfd]
            c['fought'] += 1; d['fought'] += 1
            if af > df:
                c['won'] += 1; c['killed_in_won'] += len(b['defendersDestroyed'])
            else:
                d['won'] += 1; d['killed_in_won'] += len(b['attackersDestroyed'])
            if not b['provinceDestroyed'] and 0 <= df + s - af <= 3:
                c['narrow_lost'] += 1; c['narrow_lost_hand'] += hand_att > 0
            if b['provinceDestroyed'] and 1 <= af - df - s <= 3:
                d['narrow_lost'] += 1; d['narrow_lost_hand'] += hand_dfd > 0
        key = (a['seed'], a['challengerSeat'])
        last_by_game[key] = a
    for a in last_by_game.values():
        if a.get('winner') is None or not str(a.get('reason', '')).startswith('Military'):
            continue
        loser = 1 - a['winner']; lab = 'challenger' if loser == a['challengerSeat'] else 'opponent'
        m[lab]['mil_losses'] += 1; m[lab]['mil_loss_hand'] += a['sides'][loser]['hand']
    return m


def main(argv):
    if not argv:
        print(__doc__); return 1
    print('| records | side | battles fought | won | enemy Personalities destroyed per battle won | narrow losses (0-3 F) with cards in hand | cards in hand at a military loss (last declaration) |')
    print('| --- | --- | --- | --- | --- | --- | --- |')
    for path in argv:
        m = measure(path)
        for side in ('challenger', 'opponent'):
            c = m[side]
            print(f"| {os.path.basename(path.rstrip('/'))} | {side} | {c['fought']} | {c['won']} | {c['killed_in_won'] / c['won'] if c['won'] else 0:.2f} | "
                  f"{pct(c['narrow_lost_hand'], c['fought'])} (of {c['narrow_lost']} narrow) | {c['mil_loss_hand'] / c['mil_losses'] if c['mil_losses'] else 0:.2f} over {c['mil_losses']} losses |")
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
