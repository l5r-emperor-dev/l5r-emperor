"""Stage 1 replay checks (issue #1262): what a plain practice-replay export is, and what is refused before anything executes it."""
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import check_replays  # noqa: E402

COMMAND = {'type': 'useAbility', 'playerId': 1, 'expectedVersion': 3, 'cardId': 'card-1077', 'sourceIds': ['card-12#reaction'], 'province': None, 'clanDiscount': False,
           'clanAllegiance': False, 'assignments': {'card-5': 1}, 'cardIds': ['card-1762'], 'targetId': None, 'abilityId': 'border-first', 'performerId': None,
           'choice': None, 'deckPosition': None, 'targetPlayerId': None, 'goldAmount': None, 'costOption': None,
           'selections': [{'slot': 'target', 'cardIds': ['card-9'], 'playerId': None, 'provinceOwner': None, 'province': None}],
           'createdCard': None, 'paymentDiscards': None, 'provinces': None}


def replay(**changes):
    data = {'contentVersion': 'emperor-lion-crane-0.5.0', 'scenario': 'custom-practice-v1', 'seed': 2**63 + 5, 'commands': [dict(COMMAND)],
            'customDecks': [{'legality': 'Emperor', 'entries': [{'name': "Ebisu's Honesty", 'count': 3}]}] * 2, 'idleBattlePasses': True}
    data.update(changes)
    return json.dumps(data, indent=2).encode('utf-8')


class ReplayShape(unittest.TestCase):
    def test_an_export_is_accepted(self):
        self.assertEqual(check_replays.check_replay(replay()), [])

    def test_a_command_may_omit_its_null_fields(self):
        self.assertEqual(check_replays.check_replay(replay(commands=[{'type': 'pass', 'playerId': 0, 'expectedVersion': 0}])), [])

    def test_not_a_simple_file(self):
        for raw, expected in [(b'\xff\xfe{}', 'not UTF-8'), ('\ufeff{}'.encode(), 'byte-order mark'), (b'{"a": 1, "a": 2}', 'duplicate key'),
                              (b'{"seed": NaN}', 'non-standard number'), (b'[1, 2]', 'not a JSON object'), (b'{"x": 1}', 'unexpected top-level'),
                              (b'{"a": "\x01"}', 'control characters'), (b'{"a":' + b'[' * 20 + b']' * 20 + b'}', 'nested deeper'),
                              (b' ' * (check_replays.MAX_BYTES + 1), 'over the')]:
            with self.subTest(expected=expected):
                findings = check_replays.check_replay(raw)
                self.assertTrue(findings and expected in findings[0], findings)

    def test_fields_and_types_are_the_export_s(self):
        bad = [({'commands': [dict(COMMAND, script='x')]}, 'unexpected field(s) script'), ({'commands': [dict(COMMAND, playerId=2)]}, 'playerId'),
               ({'commands': [dict(COMMAND, clanDiscount='no')]}, 'clanDiscount'), ({'commands': [dict(COMMAND, expectedVersion=True)]}, 'expectedVersion'),
               ({'commands': [dict(COMMAND, expectedVersion=5), dict(COMMAND, expectedVersion=4)]}, 'backwards'), ({'seed': -1}, 'seed'), ({'commands': []}, 'commands'),
               ({'customDecks': [{'legality': 'Emperor', 'entries': []}] * 2}, 'entries'), ({'customDecks': [{'legality': 'Emperor', 'entries': [{'name': 'x', 'count': 0}]}] * 2}, 'count')]
        for change, expected in bad:
            with self.subTest(expected=expected):
                findings = check_replays.check_replay(replay(**change))
                self.assertTrue(findings and expected in findings[0], findings)

    def test_practice_labels_and_final_digest(self):
        labelled = replay(practice={'yourDeck': "Scottish Lion", 'opponentDeck': "Mendon's Crane", 'botLevel': 'Hard'}, finalDigest='d1:' + 'a' * 32)
        self.assertEqual(check_replays.check_replay(labelled), [])
        for change, expected in [({'practice': {'yourDeck': 'C:\\decks\\lion.txt'}}, 'outside the allowed form'), ({'practice': {'deckPath': 'x'}}, 'unexpected field'),
                                 ({'finalDigest': 'abc'}, 'finalDigest')]:
            with self.subTest(expected=expected):
                findings = check_replays.check_replay(replay(**change))
                self.assertTrue(findings and expected in findings[0], findings)

    def test_no_paths_addresses_or_links_in_strings(self):
        for value in ['C:\\Users\\me\\deck.txt', '/Users/me/x', 'me@example.org', 'https://example.org', 'www.example.org', 'run.exe']:
            with self.subTest(value=value):
                findings = check_replays.check_replay(replay(commands=[dict(COMMAND, choice=value)]))
                self.assertTrue(findings and 'outside the allowed form' in findings[0], findings)


class ReplayFolder(unittest.TestCase):
    def folder(self, files, index):
        root = Path(tempfile.mkdtemp())
        (root / 'replays' / 'games').mkdir(parents=True)
        for name, raw in files.items():
            (root / 'replays' / 'games' / name).write_bytes(raw)
        (root / 'replays' / 'index.json').write_text(json.dumps({'version': 1, 'games': index}), encoding='utf-8')
        return root

    def test_every_file_is_indexed_with_its_hash(self):
        raw = replay()
        good = self.folder({'practice-20261113-183409.json': raw}, [{'file': 'practice-20261113-183409.json', 'sha256': hashlib.sha256(raw).hexdigest()}])
        self.assertEqual(check_replays.check(good), [])
        changed = self.folder({'practice-20261113-183409.json': raw}, [{'file': 'practice-20261113-183409.json', 'sha256': '0' * 64}])
        self.assertIn('SHA-256 differs', check_replays.check(changed)[0])
        missing = self.folder({'practice-20261113-183409.json': raw}, [])
        self.assertIn('not in replays/index.json', check_replays.check(missing)[0])
        orphan = self.folder({}, [{'file': 'gone.json', 'sha256': '0' * 64}])
        self.assertIn('not in replays/games', check_replays.check(orphan)[0])

    def test_names_are_plain(self):
        root = self.folder({'Practice 1.json': replay()}, [])
        self.assertIn('name must be', check_replays.check(root)[0])


if __name__ == '__main__':
    unittest.main()
