#!/usr/bin/env python3
"""profile.py - the behaviour profile of each player type, in one table.

Usage: python3 profile.py <logs-dir-or-file>... [dir=ChallengerLabel,ChampionLabel] [--json]

Prints the table that docs/bots/strategy/ARENA_BEHAVIOUR.md shows under
"The behaviour profile": one row per measure, one column per player label
found in the logs (Human, Conan, Contender(vsConan), Contender(mirror),
PracticeBot, or the labels given with `dir=...`), the direction a stronger
player moves it and the arena table (Q1c, Q2a, ...) that defines it.  The
games per label are printed first.

Every measure is computed by the q*.py script named in its "Table" column:
this script only imports their collect() accumulators and takes one cell
out of each, so a profile cell reproduces the headline number of that table
when both are run on the same logs.  Q4e's set of "neutralising" Strategy
cards is derived from the games loaded, so run the human logs together with
the arena logs (as the arena tables were) if that row is to match.

Labels with 30 or more games are shown as percentages, smaller samples as
"a of b" counts.  Two rows are ratios of sums rather than of events and keep
their form at any sample size: "per 100 attacks" (small samples add the
counts in brackets) and the share of Gold capacity left unproduced.

`--json` emits the same numbers as JSON: games per label and, per measure,
numerator, denominator and value (100 * numerator / denominator) per label,
so two profiles can be diffed by another script.  Game logs are passed
exactly as to the q scripts: a directory (every *.log in it; the human
`*.compare.log` files count) or a single file; see l5rlog.py for the labels.
"""
import collections
import json
import sys

import l5rlog as L
import q1_attack_timing as q1
import q2_counterattack as q2
import q3_defence as q3
import q4_battle_actions as q4
import q5_cavalry as q5
import q6_splitting as q6
import q7_economy as q7
import q8_information as q8

SMALL_GAMES = 30            # fewer games than this: show counts, not percentages
PERCENT, PER100, SHARE = 'percent', 'per 100 attacks', 'percent of Gold capacity'

# Q4e battlefield class and Q7c situation, spelled as the q scripts key them.
Q4E_IDLE = 'b: undefended, >=1 unbowed Personality left unassigned at home'
Q7C_BEHIND = ('Personalities in play', 'a: behind on Personalities')
Q8B_HELD = 'defender holds the Favor'


def games_per_label(games):
    """Seats per label: a mirror game counts once for each seat."""
    n = collections.Counter()
    for g in games:
        for s in (0, 1):
            n[g.ptype[s]] += 1
    return n


def compute(games):
    """[(key, measure, good, table, unit, {label: (numerator, denominator)})]."""
    out = q1.collect(games)[2]                     # (ptype, own-turn bucket) -> [attacks, destroyed, lost, nothing]
    A2 = q2.collect(games)[0]                      # (ptype, choice) -> metric -> values
    a3, cls = q3.collect(games)[:2]                # ptype -> [faced, defended, assigned, unbowed]; ptype -> class -> n
    hand_fall = q4.collect(games, q4.neutral_strategy_cards(games))[5]   # (ptype, class) -> [falls, with card, cards]
    a5 = q5.collect(games)[0]                      # ptype -> [attacks, offered, used, cav in inf seg, cav in cav seg]
    d6 = q6.collect(games)[3]                      # ptype -> Counter(attacks, attacks2, ...)
    A7, _, C7 = q7.collect(games)[:3]              # (ptype, own turn) -> metric -> values; (ptype, dim, sit) -> Counter
    B8 = q8.collect(games)[1]                      # (ptype, Favor held?) -> Counter

    q1_tot = collections.defaultdict(lambda: [0, 0, 0, 0])
    for (p, _), o in out.items():
        for i in range(4):
            q1_tot[p][i] += o[i]
    gold = collections.defaultdict(lambda: [0, 0])
    for (p, ot), d in A7.items():
        if ot <= 8:
            gold[p][0] += sum(d['unused'])
            gold[p][1] += sum(d['cap'])
    defended = {p: sum(cls[p][c] for c in q3.CLASSES[2:]) for p in cls}

    def by_label(f, keys):
        return {p: f(p) for p in keys}

    return [
        ('attack_destroys', 'Attacks that destroy a Province', 'higher', 'Q1c', PERCENT,
         by_label(lambda p: (q1_tot[p][1], q1_tot[p][0]), q1_tot)),
        ('attack_loses_personality', 'Attacks that lose a Personality', 'lower', 'Q1c', PERCENT,
         by_label(lambda p: (q1_tot[p][2], q1_tot[p][0]), q1_tot)),
        ('attack_nothing', 'Attacks that achieve nothing', 'lower', 'Q1c', PERCENT,
         by_label(lambda p: (q1_tot[p][3], q1_tot[p][0]), q1_tot)),
        ('two_provinces_per_100_attacks', 'Turns with two or more Provinces destroyed, per 100 attacks', 'higher',
         'Q6d', PER100, by_label(lambda p: (d6[p]['attacks2'], d6[p]['attacks']), d6)),
        ('defended_held_attacker_killed', 'Defended battlefields held with an attacker destroyed', 'higher', 'Q3b',
         PERCENT, by_label(lambda p: (cls[p][q3.CLASSES[2]], defended[p]), cls)),
        ('gift_defences', 'Gift defences: Province lost and defenders destroyed', 'lower', 'Q3b', PERCENT,
         by_label(lambda p: (cls[p][q3.CLASSES[5]], defended[p]), cls)),
        ('unbowed_assigned_when_attacked', 'Unbowed Personalities assigned when attacked', 'fewer, better placed',
         'Q3a', PERCENT, by_label(lambda p: (a3[p][2], a3[p][3]), a3)),
        ('province_lost_after_all_in', "A Province lost on the opponent's turn after an all-in attack", 'lower',
         'Q2a', PERCENT, {p: (sum(d['lostany']), len(d['back'])) for (p, ch), d in A2.items() if ch == 'all-in'}),
        ('neutraliser_in_hand_idle_guard', 'A neutralising card in hand when an undefended Province fell with an '
         'unbowed Personality at home', 'lower', 'Q4e', PERCENT,
         {p: (x[1], x[0]) for (p, k), x in hand_fall.items() if k == Q4E_IDLE}),
        ('cavalry_held_for_segment', 'Cavalry Personalities held for the Cavalry segment', 'higher', 'Q5a', PERCENT,
         by_label(lambda p: (a5[p][4], a5[p][3] + a5[p][4]), a5)),
        ('gold_capacity_unproduced', 'Gold capacity left unproduced, turns 1 to 8', 'lower', 'Q7a', SHARE,
         by_label(lambda p: (gold[p][0], gold[p][1]), gold)),
        ('first_purchase_personality_when_behind', 'First purchase is a Personality when behind on Personalities',
         'higher', 'Q7c', PERCENT,
         {p: (x['first:Personality'], x['n']) for (p, dim, sit), x in C7.items() if (dim, sit) == Q7C_BEHIND}),
        ('one_personality_into_favor_nothing', 'One-Personality attacks into a held Favor that achieve nothing',
         'fewer such attacks', 'Q8b', PERCENT,
         {p: (x['one_nothing'], x['one']) for (p, held), x in B8.items() if held == Q8B_HELD}),
    ]


def cell(num, den, unit, small):
    if not den:
        return '-'
    v = 100.0 * num / den
    if unit == PER100:
        return '%.1f (%d of %d)' % (v, num, den) if small else '%.1f' % v
    if unit == PERCENT and small:
        return '%d of %d' % (num, den)
    return '%.0f%%' % v


def main(argv):
    as_json = '--json' in argv
    argv = [a for a in argv if a != '--json']
    if not argv:
        sys.exit(__doc__)
    games = L.load(argv)
    if not games:
        sys.exit('no games parsed from: ' + ' '.join(argv))
    labels = L.ptypes(games)
    n_games = games_per_label(games)
    rows = compute(games)
    if as_json:
        doc = {'games': {p: n_games[p] for p in labels}, 'measures': []}
        for key, name, good, table, unit, vals in rows:
            doc['measures'].append({
                'key': key, 'measure': name, 'good': good, 'table': table, 'unit': unit,
                'values': {p: {'num': vals[p][0], 'den': vals[p][1],
                               'value': round(100.0 * vals[p][0] / vals[p][1], 2) if vals[p][1] else None}
                           for p in labels if p in vals}})
        json.dump(doc, sys.stdout, indent=1)
        sys.stdout.write('\n')
        return
    print('## Games per label (a mirror game counts once per seat)\n')
    L.table(['label', 'games'], [[p, n_games[p]] for p in labels])
    print('## The behaviour profile\n')
    L.table(['Measure'] + labels + ['Good', 'Table'],
            [[name] + [cell(*vals.get(p, (0, 0)), unit, n_games[p] < SMALL_GAMES) for p in labels] + [good, table]
             for _, name, good, table, unit, vals in rows])


if __name__ == '__main__':
    main(sys.argv[1:])
