#!/usr/bin/env python3
"""Stage 1 of the replay checks (issue #1262): prove every file under replays/games is a plain practice-replay export before anything executes it.

No build and no engine: strict UTF-8 JSON (no duplicate keys, no NaN, bounded size and depth), exactly the export's fields and types (ReplayRecord, GameCommand,
SlotSelection and Deck in src/L5R.Core), strings from a narrow character set (no paths, addresses or links), and replays/index.json listing every file with its
SHA-256. Stage 2 (tools/ReplayCheck) then rebuilds each game in the engine and executes every command.

Usage: python3 tools/replays/check_replays.py [--root DIR]   (exit 1 on any finding, one line per finding)
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

MAX_BYTES = 2 * 1024 * 1024
MAX_DEPTH = 8
MAX_COMMANDS = 20000
NAME = re.compile(r'[a-z0-9][a-z0-9-]{0,80}\.json')
# letters (any script), digits, space and the punctuation card names use (catalogue: ! & ' ( ) , -), plus . : _ for ids and choices; never / \ @
TEXT = re.compile(r"[\w !&'(),.:\-]*")
IDENT = re.compile(r'[A-Za-z0-9][A-Za-z0-9_.:#\-]{0,63}')
COMMAND_TYPE = re.compile(r'[a-z][A-Za-z0-9]{0,47}')
LINK = re.compile(r'https?|www\.|\.com\b|\.exe\b', re.IGNORECASE)

TOP = {'contentVersion': str, 'scenario': str, 'seed': int, 'commands': list, 'customDecks': (list, type(None)), 'idleBattlePasses': bool}
REQUIRED_TOP = {'contentVersion', 'scenario', 'seed', 'commands'}
# GameCommand (src/L5R.Core/Models.cs): field -> kind; tools/ReplayCheck checks this list against the record itself
COMMAND = {
    'type': 'type', 'playerId': 'seat', 'expectedVersion': 'count', 'cardId': 'id?', 'sourceIds': 'ids?', 'province': 'small?', 'clanDiscount': 'bool',
    'clanAllegiance': 'bool', 'assignments': 'id-int-map?', 'cardIds': 'ids?', 'targetId': 'id?', 'abilityId': 'id?', 'performerId': 'id?', 'choice': 'text?',
    'deckPosition': 'text?', 'targetPlayerId': 'seat?', 'goldAmount': 'small?', 'costOption': 'text?', 'selections': 'selections?', 'createdCard': 'id?',
    'paymentDiscards': 'id-id-map?', 'provinces': 'smalls?',
}
REQUIRED_COMMAND = {'type', 'playerId', 'expectedVersion'}
SELECTION = {'slot': 'text', 'cardIds': 'ids?', 'playerId': 'seat?', 'provinceOwner': 'seat?', 'province': 'small?'}


class Finding(Exception):
    pass


def no_duplicates(pairs):
    seen = {}
    for key, value in pairs:
        if key in seen:
            raise Finding(f'duplicate key "{key}"')
        seen[key] = value
    return seen


def no_constants(name):
    raise Finding(f'non-standard number {name}')


def depth(value, level=1):
    if level > MAX_DEPTH:
        raise Finding(f'nested deeper than {MAX_DEPTH}')
    children = value.values() if isinstance(value, dict) else value if isinstance(value, list) else ()
    for child in children:
        depth(child, level + 1)


def text(value, where, pattern=TEXT, limit=80):
    if not isinstance(value, str):
        raise Finding(f'{where}: expected a string')
    if len(value) > limit or not pattern.fullmatch(value) or LINK.search(value):
        raise Finding(f'{where}: string {value[:40]!r} is outside the allowed form')


def integer(value, where, low, high):
    if isinstance(value, bool) or not isinstance(value, int) or not low <= value <= high:
        raise Finding(f'{where}: expected an integer in {low}..{high}')


def field(kind, value, where):
    optional = kind.endswith('?')
    kind = kind.rstrip('?')
    if value is None:
        if optional:
            return
        raise Finding(f'{where}: missing value')
    if kind == 'type':
        text(value, where, COMMAND_TYPE, 48)
    elif kind == 'seat':
        integer(value, where, 0, 1)
    elif kind == 'count':
        integer(value, where, 0, 10_000_000)
    elif kind == 'small':
        integer(value, where, 0, 999)
    elif kind == 'bool':
        if not isinstance(value, bool):
            raise Finding(f'{where}: expected true or false')
    elif kind == 'id':
        text(value, where, IDENT, 64)
    elif kind == 'text':
        text(value, where)
    elif kind in ('ids', 'smalls'):
        if not isinstance(value, list) or len(value) > 200:
            raise Finding(f'{where}: expected a list of at most 200')
        for i, item in enumerate(value):
            field('id' if kind == 'ids' else 'small', item, f'{where}[{i}]')
    elif kind in ('id-int-map', 'id-id-map'):
        if not isinstance(value, dict) or len(value) > 200:
            raise Finding(f'{where}: expected an object of at most 200 entries')
        for key, item in value.items():
            text(key, f'{where} key', IDENT, 64)
            field('small' if kind == 'id-int-map' else 'id', item, f'{where}.{key}')
    elif kind == 'selections':
        if not isinstance(value, list) or len(value) > 20:
            raise Finding(f'{where}: expected a list of at most 20 slots')
        for i, item in enumerate(value):
            record(item, SELECTION, {'slot'}, f'{where}[{i}]')
    else:
        raise AssertionError(kind)


def record(value, schema, required, where):
    if not isinstance(value, dict):
        raise Finding(f'{where}: expected an object')
    unknown = sorted(set(value) - set(schema))
    if unknown:
        raise Finding(f'{where}: unexpected field(s) {", ".join(unknown)}')
    missing = sorted(required - set(value))
    if missing:
        raise Finding(f'{where}: missing field(s) {", ".join(missing)}')
    for key, item in value.items():
        field(schema[key], item, f'{where}.{key}')


def check_replay(raw):
    """The findings for one file's bytes (empty: a plain export)."""
    if len(raw) > MAX_BYTES:
        return [f'{len(raw)} bytes, over the {MAX_BYTES} limit']
    try:
        content = raw.decode('utf-8', errors='strict')
    except UnicodeDecodeError as error:
        return [f'not UTF-8: {error.reason} at byte {error.start}']
    if content.startswith('﻿'):
        return ['starts with a byte-order mark']
    if any(ord(c) < 32 and c not in '\n\r\t' for c in content):
        return ['contains control characters']
    try:
        data = json.loads(content, object_pairs_hook=no_duplicates, parse_constant=no_constants)
        depth(data)
        if not isinstance(data, dict):
            raise Finding('not a JSON object')
        unknown = sorted(set(data) - set(TOP))
        if unknown:
            raise Finding(f'unexpected top-level field(s) {", ".join(unknown)}')
        missing = sorted(REQUIRED_TOP - set(data))
        if missing:
            raise Finding(f'missing top-level field(s) {", ".join(missing)}')
        for key, kind in TOP.items():
            if key in data and (not isinstance(data[key], kind) or isinstance(data[key], bool) and kind is int):
                raise Finding(f'{key}: wrong type')
        text(data['contentVersion'], 'contentVersion', IDENT, 64)
        text(data['scenario'], 'scenario', IDENT, 64)
        integer(data['seed'], 'seed', 0, 2**64 - 1)
        commands = data['commands']
        if not 1 <= len(commands) <= MAX_COMMANDS:
            raise Finding(f'commands: expected 1..{MAX_COMMANDS}')
        last = -1
        for i, command in enumerate(commands):
            record(command, COMMAND, REQUIRED_COMMAND, f'commands[{i}]')
            if command['expectedVersion'] < last:
                raise Finding(f'commands[{i}]: expectedVersion goes backwards')
            last = command['expectedVersion']
        decks = data.get('customDecks')
        if decks is not None:
            if len(decks) != 2:
                raise Finding('customDecks: expected two decks')
            for d, deck in enumerate(decks):
                if not isinstance(deck, dict) or set(deck) != {'legality', 'entries'}:
                    raise Finding(f'customDecks[{d}]: expected exactly legality and entries')
                text(deck['legality'], f'customDecks[{d}].legality', IDENT, 32)
                entries = deck['entries']
                if not isinstance(entries, list) or not 1 <= len(entries) <= 200:
                    raise Finding(f'customDecks[{d}].entries: expected 1..200 entries')
                for e, entry in enumerate(entries):
                    if not isinstance(entry, dict) or set(entry) != {'name', 'count'}:
                        raise Finding(f'customDecks[{d}].entries[{e}]: expected exactly name and count')
                    text(entry['name'], f'customDecks[{d}].entries[{e}].name')
                    integer(entry['count'], f'customDecks[{d}].entries[{e}].count', 1, 60)
    except Finding as finding:
        return [str(finding)]
    except json.JSONDecodeError as error:
        return [f'not JSON: {error.msg} at line {error.lineno}']
    except RecursionError:
        return ['nested too deeply']
    return []


def git_modes(root, folder):
    """Git's recorded mode for each tracked file under folder (None outside a Git checkout)."""
    try:
        out = subprocess.run(['git', '-C', str(root), 'ls-files', '-s', '--', folder], capture_output=True, text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return None
    return {line.split('\t', 1)[1]: line.split()[0] for line in out.splitlines() if '\t' in line}


def check(root):
    games = root / 'replays' / 'games'
    findings = []
    if not games.is_dir():
        return ['replays/games is missing']
    files = {}
    for path in sorted(games.iterdir()):
        rel = f'replays/games/{path.name}'
        if path.is_symlink() or not path.is_file():
            findings.append(f'{rel}: not a regular file')
            continue
        if not NAME.fullmatch(path.name):
            findings.append(f'{rel}: name must be lower-case letters, digits and hyphens, ending .json')
            continue
        raw = path.read_bytes()
        findings.extend(f'{rel}: {f}' for f in check_replay(raw))
        files[path.name] = hashlib.sha256(raw).hexdigest()
    modes = git_modes(root, 'replays')
    if modes is not None:
        findings.extend(f'{path}: Git mode {mode}, expected 100644 (a plain, non-executable file)' for path, mode in sorted(modes.items()) if mode != '100644')
    index_path = root / 'replays' / 'index.json'
    try:
        index = json.loads(index_path.read_text(encoding='utf-8'), object_pairs_hook=no_duplicates)
        listed = {game['file']: game for game in index['games']}
    except (OSError, ValueError, KeyError, TypeError, Finding) as error:
        return findings + [f'replays/index.json: unreadable ({error})']
    for name, digest in files.items():
        if name not in listed:
            findings.append(f'replays/games/{name}: not in replays/index.json (run tools/ReplayCheck with --write-index)')
        elif listed[name].get('sha256') != digest:
            findings.append(f'replays/games/{name}: SHA-256 differs from replays/index.json (the file changed after it was indexed)')
    findings.extend(f'replays/index.json: lists {name}, which is not in replays/games' for name in sorted(set(listed) - set(files)))
    return findings


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    findings = check(args.root)
    for finding in findings:
        print(finding)
    count = len(list((args.root / 'replays' / 'games').glob('*.json'))) if (args.root / 'replays' / 'games').is_dir() else 0
    print(f'{count} replay file(s): {"all plain exports" if not findings else f"{len(findings)} finding(s)"}')
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main())
