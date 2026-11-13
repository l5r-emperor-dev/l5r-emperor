#!/usr/bin/env python3
"""threats.py - read the threat records a tournament writes with L5R_TOURN_THREATS (tests/L5R.AI.Tests/ThreatRecords.cs).

    python3 tools/bots/threats.py <threats-dir>... [--fit <threats-dir>...] [--json]

One record per player turn: threat(home) as the Read projected it for that seat at three moments (at its attack declaration, with the units its
exchange expects to destroy left out; after its last Dynasty decision, their army straightened in projection; at the first decision of their
next turn, their army as it really straightened), two paper baselines taken at that last moment (the paper margin: their total Force minus
my unbowed home Force minus my weakest Strength; the guard ratio of ARENA_BEHAVIOUR.md Q2c: my unbowed home Force over their Force), and what
their turn then did: whether they attacked, the Provinces this seat lost, the units each side lost.

Prints, over the turns whose opponent's next turn was played:
  1. the calibration table per estimator: its value in cells against the observed rate at which the seat lost a Province on the opponent's turn
     and the mean Provinces lost; with --fit, the cell rates are fitted on the --fit records and scored on the others (Brier score, lower is
     better, beside the constant-rate Brier), which is how the threat answer is judged against the baselines (CONAN_STRATEGY_BRIEF.md step 2);
  2. the same for the Read's projection split by whether the opponent had shown a Fate card yet (the belief has something to update from).
No bot reads these files.
"""
import collections
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from battles import MARGIN_BUCKETS, bucket, pct, table  # noqa: E402

RATIO_BUCKETS = [('< 25%', lambda r: r < 0.25), ('25-49%', lambda r: 0.25 <= r < 0.5), ('50-99%', lambda r: 0.5 <= r < 1.0), ('>= 100%', lambda r: r >= 1.0)]


def ratio_bucket(r):
    for name, f in RATIO_BUCKETS:
        if f(r):
            return name
    return '?'


def cell(p):
    """The three-hand cell of a projection: whether a Province falls under the open, likely and worst hand."""
    if not p:
        return None
    return ''.join('1' if p[k]['provincesLost'] > 0 else '0' for k in ('open', 'likely', 'worst'))


def cell_count(p):
    """The likely hand's Provinces lost, capped at 2, with the worst hand's beside it."""
    if not p:
        return None
    return f"{min(2, p['likely']['provincesLost'])}/{min(2, p['worst']['provincesLost'])}"


def tiers(p):
    """The tier under each hand (0 a Province falls, 1 held, 2 held and nobody lost, 3 held and an attacker destroyed), open/likely/worst."""
    if not p:
        return None
    if p['candidates'] == 0:
        return 'no army'
    return ''.join(str(p[k]['tier']) for k in ('open', 'likely', 'worst'))


def margin(p):
    if not p:
        return None
    return 'no army' if p['candidates'] == 0 else bucket(p['likely']['margin'])


FINE_BUCKETS = [('<= -10', lambda m: m <= -10), ('-9..-5', lambda m: -10 < m <= -5), ('-4..-1', lambda m: -5 < m < 0), ('0..+2', lambda m: 0 <= m <= 2), ('+3..+5', lambda m: 2 < m <= 5), ('+6..+9', lambda m: 5 < m <= 9), ('+10..+14', lambda m: 9 < m <= 14), ('> +14', lambda m: m > 14)]


def fine(m):
    for name, f in FINE_BUCKETS:
        if f(m):
            return name
    return '?'


def read_margin(p):
    """The projection's own Force gap: their straightened army against my unbowed home and weakest Strength (the paper margin as the ledger counts it)."""
    return p['attackForce'] - p['homeForce'] - p['weakestStrength']


ESTIMATORS = {
    'paper margin (their Force - my unbowed home - weakest Strength)': lambda t: bucket(t['paperMargin']),
    'paper margin, eight buckets': lambda t: fine(t['paperMargin']),
    'guard ratio (my unbowed home Force / their Force)': lambda t: ratio_bucket(t['guardRatio']),
    'threat at their turn: the three-hand cell crossed with the projection\'s own Force gap, eight buckets (the headline estimator)': lambda t: (cell(t.get('atStart')) or '?') + ' ' + fine(read_margin(t['atStart'])) if t.get('atStart') else None,
    'threat projected after my last Dynasty decision: cell crossed with the Force gap, eight buckets': lambda t: (cell(t.get('atEnd')) or '?') + ' ' + fine(read_margin(t['atEnd'])) if t.get('atEnd') else None,
    'threat at the first decision of their turn (open/likely/worst)': lambda t: cell(t.get('atStart')),
    'threat projected after my last Dynasty decision (open/likely/worst)': lambda t: cell(t.get('atEnd')),
    'threat projected at my attack declaration, attacking turns only (open/likely/worst)': lambda t: cell(t.get('atDeclaration')),
    'threat at their turn, Provinces lost likely/worst (capped at 2)': lambda t: cell_count(t.get('atStart')),
    'threat tiers at their turn (open/likely/worst; 0 falls, 1 held, 2 held and nobody lost, 3 held and an attacker destroyed)': lambda t: tiers(t.get('atStart')),
    'threat at their turn, the likely hand\'s margin after answers (their Force - my defenders - Strength at the worst battlefield)': lambda t: margin(t.get('atStart')),
    'threat at their turn, cell crossed with the likely margin': lambda t: (cell(t.get('atStart')) or '?') + ' ' + (margin(t.get('atStart')) or '?'),
    'threat at their turn crossed with the paper margin': lambda t: (cell(t.get('atStart')) or '?') + ' ' + bucket(t['paperMargin']),
    'threat likely tier at their turn crossed with the paper margin': lambda t: (str(t['atStart']['likely']['tier']) if t.get('atStart') else '?') + ' ' + bucket(t['paperMargin']),
}
ORDER = ['no army'] + [n for n, _ in MARGIN_BUCKETS] + [n for n, _ in FINE_BUCKETS if n != '<= -10'] + [n for n, _ in RATIO_BUCKETS] + ['000', '001', '011', '111', '0/0', '0/1', '0/2', '1/1', '1/2', '2/2', '0', '1', '2', '3']


def order_key(k):
    """Cells in the order of ORDER; a crossed cell ("111 > +12") by its two parts."""
    if k in ORDER:
        return (ORDER.index(k), 0, k)
    head, _, tail = k.partition(' ')
    return (ORDER.index(head) if head in ORDER else 99, ORDER.index(tail) if tail in ORDER else 99, k)


def load(paths):
    for path in paths:
        files = glob.glob(os.path.join(path, '*.jsonl')) if os.path.isdir(path) else [path]
        for f in sorted(files):
            with open(f) as fh:
                for line in fh:
                    if line.strip():
                        t = json.loads(line)
                        if t['nextTurnPlayed']:
                            yield t


def lost(t):
    return 1 if t['provincesLost'] > 0 else 0


def calibration(recs, fit_recs, estimators, title):
    print(f'## {title} ({len(recs)} turns{"; rates fitted on " + str(len(fit_recs)) + " others" if fit_recs is not None else ""})\n')
    base_rate = sum(map(lost, fit_recs if fit_recs is not None else recs)) / max(1, len(fit_recs if fit_recs is not None else recs))
    print(f'Lost a Province on the opponent\'s next turn: {pct(sum(map(lost, recs)), len(recs))} of the scored turns; the opponent attacked in {pct(sum(t["attacked"] for t in recs), len(recs))}.\n')
    if fit_recs is not None:
        print(f'Constant-rate Brier (predict {base_rate:.2f} everywhere): {sum((base_rate - lost(t)) ** 2 for t in recs) / max(1, len(recs)):.4f}\n')
    summary = {}
    for name, f in estimators.items():
        counts = collections.defaultdict(lambda: [0, 0, 0])
        for t in (fit_recs if fit_recs is not None else recs):
            k = f(t)
            if k is not None:
                counts[k][0] += 1; counts[k][1] += lost(t); counts[k][2] += t['provincesLost']
        observed = collections.defaultdict(lambda: [0, 0, 0])
        if fit_recs is not None:
            for t in recs:
                k = f(t)
                if k is not None:
                    observed[k][0] += 1; observed[k][1] += lost(t); observed[k][2] += t['provincesLost']
        rows = []; misses = 0
        for k in sorted(counts, key=order_key):
            n, d, pl = counts[k]
            if fit_recs is None:
                rows.append([k, n, pct(d, n), f'{pl / n:.2f}'])
            else:
                on, od, opl = observed.get(k, [0, 0, 0])
                fitted = d / n if n else base_rate; obs = od / on if on else None
                flag = ''
                if on >= 200 and obs is not None and abs(obs - fitted) > 0.10:
                    flag = ' MISS'; misses += 1
                rows.append([k, n, f'{100 * fitted:.0f}%', f'{pl / n:.2f}', on, pct(od, on) + flag, f'{opl / on:.2f}' if on else '-'])
        brier = ''
        if fit_recs is not None:
            scored = [(counts[k][1] / counts[k][0] if k in counts and counts[k][0] else base_rate, lost(t)) for t in recs for k in [f(t)] if k is not None]
            if scored:
                b = sum((p - y) ** 2 for p, y in scored) / len(scored)
                brier = f' Brier on the scored turns: {b:.4f} ({len(scored)} turns)'
                summary[name] = {'brier': round(b, 4), 'turns': len(scored), 'misses': misses}
        print(f'### {name}{brier}{(" ; cells of 200+ turns off by more than 10 points: " + str(misses)) if fit_recs is not None else ""}\n')
        table(['cell', 'fit turns', 'fitted loss rate', 'mean lost', 'scored turns', 'observed', 'mean lost'] if fit_recs is not None else ['cell', 'turns', 'lost a Province', 'mean lost'], rows)
    return summary


def main(argv):
    fit = []; paths = []; as_json = False; it = iter(argv)
    for a in it:
        if a == '--fit':
            fit = [x for x in it]
            break
        elif a == '--json':
            as_json = True
        else:
            paths.append(a)
    recs = list(load(paths)); fit_recs = list(load(fit)) if fit else None
    if as_json:
        print(json.dumps(calibration(recs, fit_recs, ESTIMATORS, 'Calibration per turn')))
        return
    print(f'{len(recs)} turns with an opponent turn after them from {len(paths)} source(s)\n')
    calibration(recs, fit_recs, ESTIMATORS, 'Calibration per turn')
    key = 'threat at the first decision of their turn (open/likely/worst)'
    for name, keep in (('before the opponent showed a Fate card', lambda t: t['beliefSeen'] == 0), ('after the opponent showed a Fate card', lambda t: t['beliefSeen'] > 0)):
        calibration([t for t in recs if keep(t)], [t for t in fit_recs if keep(t)] if fit_recs is not None else None, {key: ESTIMATORS[key]}, f'Calibration {name}')


if __name__ == '__main__':
    main(sys.argv[1:])
