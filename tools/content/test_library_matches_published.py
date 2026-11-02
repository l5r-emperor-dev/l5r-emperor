"""The card library and the wired card definitions describe the same cards (full audit, 2026-11).

Every wired card that is a real printed card is in the client library, every library card is wired except the ones Mendon deliberately refused, the cost/Force/Chi/Honor
numbers agree, and no wired card repeats its text twice. Created-only proxies and scan-only cards are wired but stay out of the library on purpose."""
import glob
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# Not wired by decision: multiplayer-only (#742) or rejected by Mendon (2026-10-05); see docs/CARD_INVENTORY.xlsx blockers.
UNWIRED_BY_DECISION = {"oracle-l5r-946", "oracle-l5r-2904", "oracle-l5r-10906", "oracle-l5r-10237", "oracle-l5r-6065", "oracle-l5r-9993"}
# Pinned Lion/Crane deck printing and a Strategy whose cost is paid in the ability text: kept as wired, reviewed by hand.
STAT_EXCEPTIONS = {"oracle-l5r-3636", "oracle-l5r-25"}
STATS = {"cost": "GoldCost", "force": "Force", "chi": "Chi", "focus": "Focus", "ph": "PersonalHonor", "honor": "HonorRequirement", "production": "GoldProduction",
         "startinghonor": "StartingHonor", "strength": "ProvinceStrength"}


def _load():
    library = {c["id"]: c for c in json.loads((ROOT / "src/L5R.Client/Content/library.json").read_text(encoding="utf-8"))["cards"]}
    published = {}
    for path in glob.glob(str(ROOT / "content/published/*.json")):
        if path.endswith("-sources.json"):
            continue
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        if isinstance(data, dict) and isinstance(data.get("Cards"), list):
            for card in data["Cards"]:
                if isinstance(card, dict) and "Id" in card:
                    published[card["Id"]] = card
    return library, published


def _proxy_ids():
    ids = set()
    for path in glob.glob(str(ROOT / "content/oracle/**/catalogue.json"), recursive=True):
        for card in json.loads(Path(path).read_text(encoding="utf-8"))["cards"]:
            if "Proxy" in (card.get("types") or []):
                ids.add(card["id"])
    return ids


class LibraryMatchesPublished(unittest.TestCase):
    def test_every_printed_wired_card_is_in_the_library(self):
        library, published = _load()
        proxies = _proxy_ids()
        missing = sorted(f"{c['Id']} {c['Name']}" for c in published.values()
                         if c["Id"] not in library and c["Id"] not in proxies and not c.get("CreatedOnly") and not c["Id"].startswith("user-scan-"))
        self.assertEqual([], missing, "wired printed cards missing from the card library")

    def test_every_library_card_is_wired_unless_refused(self):
        library, published = _load()
        self.assertEqual(sorted(UNWIRED_BY_DECISION), sorted(i for i in library if i not in published))

    def test_numbers_agree(self):
        library, published = _load()
        bad = []
        for card_id, card in library.items():
            wired = published.get(card_id)
            if wired is None or card_id in STAT_EXCEPTIONS:
                continue
            for key, field in STATS.items():
                values = card.get("stats", {}).get(key)
                if values and re.fullmatch(r"\d+", values[0]) and int(values[0]) != (wired.get(field) or 0):
                    bad.append(f"{wired['Name']} {key}: library {values[0]}, wired {wired.get(field)}")
        self.assertEqual([], bad)

    def test_no_wired_text_is_doubled(self):
        _, published = _load()
        doubled = []
        for card in published.values():
            lines = (card.get("Text") or "").split("\n")
            if len(lines) >= 2 and len(lines) % 2 == 0 and lines[:len(lines) // 2] == lines[len(lines) // 2:]:
                doubled.append(card["Name"])
        self.assertEqual([], doubled)


if __name__ == "__main__":
    unittest.main()
