#!/usr/bin/env python3
"""battles.py - read the battle records a tournament writes with L5R_TOURN_BATTLES (tests/L5R.AI.Tests/BattleRecords.cs).

    python3 tools/bots/battles.py <battles-dir>... [dir=AttackerLabelC,AttackerLabelc] [--fit <battles-dir>...] [--json]

One record per declared attack: the public board when the first unit was assigned, every attacker and defender, each estimator's number at
the time (the paper margin at declaration and at defence, the planner's EstimateAttack, BattlePolicy.Odds at the attacker's first Battle
decision) and the real result (resolution Forces, the Province, the units destroyed on each side).

Prints:
  1. the paper-margin table of docs/bots/strategy/arena-tables/q1.md (Q1e), computed from the records instead of the parsed logs: per attack,
     committed Force minus the defender's unbowed home Force minus the mean Strength of the targets; the share with a Province destroyed, with
     an attacker Personality destroyed, with nothing achieved (no Province and no defender destroyed);
  2. calibration tables per battlefield: for each estimator, its value in buckets against the observed rate at which the Province fell, with the
     battlefields counted; with --fit, the bucket rates are fitted on the --fit records and scored on the others (Brier score, lower is better,
     beside the constant-rate Brier), which is how the Read is judged against these baselines (CONAN_STRATEGY_BRIEF.md step 2).

Labels: who attacked is the challenger (C) or the champion (c) of the game, named from the directory as l5rlog.py names the seats
(battles-r<NN>: Conan / Contender(vsConan); battles-ca<NN>: both Contender(mirror)); `dir=C-label,c-label` overrides. No bot reads these files.
"""
import collections
import glob
import json
import os
import sys

MARGIN_BUCKETS = [('<= -10', lambda m: m <= -10), ('-9..-1', lambda m: -10 < m < 0), ('0..+5', lambda m: 0 <= m <= 5), ('+6..+12', lambda m: 5 < m <= 12), ('> +12', lambda m: m > 12)]


def bucket(m):
    for name, f in MARGIN_BUCKETS:
        if f(m):
            return name
    return '?'


def labels_for(path, override):
    if override:
        return override
    base = os.path.basename(path.rstrip('/'))
    if base.startswith('battles-ca') or base.startswith('ca'):
        return ('Contender(mirror)', 'Contender(mirror)')
    if base.startswith('battles-r') or base.startswith('r'):
        return ('Conan', 'Contender(vsConan)')
    return ('Challenger', 'Champion')


def load(paths, override=None):
    """Yields (label of the attacker, record)."""
    for path in paths:
        files = glob.glob(os.path.join(path, '*.jsonl')) if os.path.isdir(path) else [path]
        c_label, ch_label = labels_for(path, override)
        for f in sorted(files):
            with open(f) as fh:
                for line in fh:
                    if not line.strip():
                        continue
                    a = json.loads(line)
                    yield (c_label if a['attacker'] == a['challengerSeat'] else ch_label), a


def pct(n, d):
    return '-' if d == 0 else f'{100.0 * n / d:.0f}%' if d >= 30 else f'{n}/{d}'


def table(header, rows):
    print('| ' + ' | '.join(header) + ' |')
    print('|' + '---|' * len(header))
    for r in rows:
        print('| ' + ' | '.join(str(x) for x in r) + ' |')
    print()


def attack_margin(a):
    fields = [b for b in a['battlefields'] if b['attackers']]
    if not fields:
        return None
    committed = sum(u['unitForce'] for b in fields for u in b['attackers'])
    strength = sum(b['strength'] for b in fields) / len(fields)
    return committed - a['defenderHomeUnbowedForce'] - strength


def q1e(records):
    out = collections.defaultdict(lambda: [0, 0, 0, 0])  # (label, bucket) -> attacks, destroyed, lost, nothing
    for label, a in records:
        m = attack_margin(a)
        if m is None:
            continue
        dest = any(b['provinceDestroyed'] for b in a['battlefields'])
        lost = any(b['attackersDestroyed'] for b in a['battlefields'])
        nothing = not dest and not any(b['defendersDestroyed'] for b in a['battlefields'])
        o = out[(label, bucket(m))]
        o[0] += 1; o[1] += dest; o[2] += lost; o[3] += nothing
    print('## Q1e from battle records: outcome by paper margin of the attack (committed F - defender unbowed home F - mean target Strength)\n')
    rows = []
    for name, _ in MARGIN_BUCKETS:
        for label in sorted({l for l, _ in out}):
            o = out.get((label, name))
            if o:
                rows.append([name, label, o[0], pct(o[1], o[0]), pct(o[2], o[0]), pct(o[3], o[0])])
    table(['paper margin', 'attacker', 'attacks', '>=1 Province destroyed', 'attacker lost >=1 Personality', 'nothing achieved'], rows)
    return out


# ---- calibration: each estimator maps a battlefield to a bucket; the bucket's observed fall rate is its probability ----------------------------
ESTIMATORS = {
    'paper at declaration': lambda b: bucket(b['paperAtDeclaration']) if b.get('paperAtDeclaration') is not None else None,
    'paper at defence': lambda b: bucket(b['paperAtDefence']) if b.get('paperAtDefence') is not None else (bucket(b['paperAtDeclaration'] + 0) if b.get('paperAtDeclaration') is not None and not b['defenders'] else None),
    'EstimateAttack.net': lambda b: bucket(b['estimate']['net'] - b['estimate']['strength']) if b.get('estimate') else None,
    'EstimateAttack.razes': lambda b: ('razes' if b['estimate']['razes'] else 'wins' if b['estimate']['wins'] else 'loses') if b.get('estimate') else None,
    'Odds at first Battle decision': lambda b: bucket(b['odds'][0] - b['odds'][1] - max(0, b['odds'][2])) if b.get('odds') else None,
    'Read at declaration, against every unbowed home defender (open/likely/worst)': lambda b: read_bucket(b.get('readAtDeclaration')),
    'Read at defence (open/likely/worst)': lambda b: read_bucket(b.get('readAtDefence')),
    'Read at defence, or undefended when nobody defended': lambda b: read_bucket(b.get('readAtDefence') or b.get('readUndefended')),
    'Read (defence or undefended) with the paper margin at defence': lambda b: (read_bucket(b.get('readAtDefence') or b.get('readUndefended')) or '?') + ' ' + (bucket(b['paperAtDefence']) if b.get('paperAtDefence') is not None else bucket(b['paperAtDeclaration']) if b.get('paperAtDeclaration') is not None else '?'),
}


def read_bucket(r):
    if not r:
        return None
    return ''.join('1' if r[k] else '0' for k in ('open', 'likely', 'worst'))


def fields(records):
    for label, a in records:
        for b in a['battlefields']:
            if b['attackers'] and b['fought']:
                yield label, b


def calibration(records, fit=None):
    recs = list(fields(records))
    fit_recs = list(fields(fit)) if fit is not None else None
    print(f'## Calibration per battlefield ({len(recs)} fought battlefields with attackers{"; rates fitted on " + str(len(fit_recs)) + " others" if fit_recs is not None else ""})\n')
    base_rate = sum(b['provinceDestroyed'] for _, b in recs) / max(1, len(recs))
    if fit_recs is not None:
        fit_rate = sum(b['provinceDestroyed'] for _, b in fit_recs) / max(1, len(fit_recs))
        print(f'Constant-rate Brier (predict {fit_rate:.2f} everywhere): {sum((fit_rate - b["provinceDestroyed"]) ** 2 for _, b in recs) / max(1, len(recs)):.4f}\n')
    for name, f in ESTIMATORS.items():
        counts = collections.defaultdict(lambda: [0, 0])
        for _, b in (fit_recs if fit_recs is not None else recs):
            k = f(b)
            if k is not None:
                counts[k][0] += 1; counts[k][1] += b['provinceDestroyed']
        rows = []
        order = [n for n, _ in MARGIN_BUCKETS] + ['loses', 'wins', 'razes']
        observed = collections.defaultdict(lambda: [0, 0])
        if fit_recs is not None:
            for _, b in recs:
                k = f(b)
                if k is not None:
                    observed[k][0] += 1; observed[k][1] += b['provinceDestroyed']
        misses = 0
        for k in sorted(counts, key=lambda k: order.index(k) if k in order else 99):
            n, d = counts[k]
            if fit_recs is None:
                rows.append([k, n, pct(d, n)])
            else:
                on, od = observed.get(k, [0, 0])
                fitted = d / n if n else base_rate; obs = od / on if on else None
                flag = ''
                if on >= 200 and obs is not None and abs(obs - fitted) > 0.10:
                    flag = ' MISS'; misses += 1
                rows.append([k, n, f'{100 * fitted:.0f}%', on, pct(od, on) + flag])
        brier = ''
        if fit_recs is not None:
            scored = [(counts[k][1] / counts[k][0] if k in counts and counts[k][0] else base_rate, b['provinceDestroyed']) for _, b in recs for k in [f(b)] if k is not None]
            if scored:
                brier = f' Brier on the scored records: {sum((p - y) ** 2 for p, y in scored) / len(scored):.4f} ({len(scored)} battlefields)'
        print(f'### {name}{brier}{(" ; cells of 200+ off by more than 10 points: " + str(misses)) if fit_recs is not None else ""}\n')
        table(['bucket', 'fit battlefields', 'fitted fall rate', 'scored battlefields', 'observed'] if fit_recs is not None else ['bucket', 'battlefields', 'Province fell'], rows)


def main(argv):
    override = None; fit = []; paths = []; as_json = False; it = iter(argv)
    for a in it:
        if a.startswith('dir='):
            override = tuple(a[4:].split(',', 1))
        elif a == '--fit':
            fit = [x for x in it]  # everything after --fit
            break
        elif a == '--json':
            as_json = True
        else:
            paths.append(a)
    records = list(load(paths, override))
    if as_json:
        print(json.dumps({'attacks': len(records), 'q1e': {f'{l}|{b}': v for (l, b), v in q1e(records).items()}}))
        return
    print(f'{len(records)} attacks from {len(paths)} source(s)\n')
    q1e(records)
    calibration(records, list(load(fit, override)) if fit else None)


if __name__ == '__main__':
    main(sys.argv[1:])
