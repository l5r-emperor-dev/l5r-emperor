"""content/printed-text.json is current with the Oracle catalogues and the published cards (issue #1358)."""
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class PrintedTextIsCurrent(unittest.TestCase):
    def test_generated_file_is_current(self):
        result = subprocess.run([sys.executable, str(ROOT / "tools/content/printed_text.py"), "--check"], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
