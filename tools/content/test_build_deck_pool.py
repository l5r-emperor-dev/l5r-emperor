import json
from pathlib import Path
import tempfile
import unittest

from build_deck_pool import ARC, PUBLISHED, build


class BuildDeckPoolTests(unittest.TestCase):
    def catalogue(self, **cards):
        return {"cards": list(cards.values())}

    def card(self, id, name, types, source_membership=ARC, stats=None, **overrides):
        return {
            "id": id, "name": name, "types": types, "clans": [], "keywords": [],
            "deck_types": ["Fate"], "stats": stats or {}, "current_oracle_text": "",
            "source_membership": source_membership, **overrides,
        }

    def write(self, directory, name, value):
        path = Path(directory) / name
        path.write_text(json.dumps(value), encoding="utf-8")
        return path

    def test_filters_to_arc_and_excludes_proxy(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = self.write(tmp, "catalogue.json", self.catalogue(
                a=self.card("a", "In Arc", ["Strategy"]),
                b=self.card("b", "Wrong Arc", ["Strategy"], source_membership="Legacy"),
                c=self.card("c", "Junk", ["Proxy"]),
            ))
            published = self.write(tmp, "published.json", {"Cards": []})
            pool = build(source, published)
            self.assertEqual(pool["count"], 1)
            self.assertEqual(pool["cards"][0]["id"], "a")

    def test_playable_flag_cross_references_published_pool(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = self.write(tmp, "catalogue.json", self.catalogue(
                a=self.card("a", "Playable Card", ["Personality"]),
                b=self.card("b", "Reference Only", ["Personality"]),
            ))
            published = self.write(tmp, "published.json", {"Cards": [{"Id": "a"}]})
            pool = build(source, published)
            by_id = {c["id"]: c for c in pool["cards"]}
            self.assertTrue(by_id["a"]["playable"])
            self.assertFalse(by_id["b"]["playable"])

    def test_stats_flattened_from_single_element_lists(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = self.write(tmp, "catalogue.json", self.catalogue(
                a=self.card("a", "Sneak Attack", ["Strategy"], stats={"cost": ["0"], "focus": ["3"]}),
            ))
            published = self.write(tmp, "published.json", {"Cards": []})
            pool = build(source, published)
            self.assertEqual(pool["cards"][0]["stats"], {"cost": "0", "focus": "3"})

    def test_missing_published_pool_treats_nothing_as_playable(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = self.write(tmp, "catalogue.json", self.catalogue(
                a=self.card("a", "Solo Card", ["Item"]),
            ))
            pool = build(source, Path(tmp) / "does-not-exist.json")
            self.assertFalse(pool["cards"][0]["playable"])

    def test_real_catalogue_matches_the_verified_count(self):
        pool = build()
        self.assertEqual(pool["count"], 2151)  # 2149 total minus 6 Proxy entries, plus 8 cards the legality query omits: 7 Emperor Edition (issue #812) and Quelsa

    def test_lion_crane_pool_is_a_clean_subset(self):
        # Every currently-playable card must resolve inside the Age of Conquest pool --
        # confirms the frozen lion-crane-v1 profile isn't drawing from outside this arc.
        published = json.loads(PUBLISHED.read_text(encoding="utf-8"))
        published_ids = {card["Id"] for card in published["Cards"]}
        pool_ids = {card["id"] for card in build()["cards"]}
        missing = published_ids - pool_ids
        self.assertEqual(missing, set(), f"lion-crane-v1 cards missing from the Age of Conquest pool: {missing}")


if __name__ == "__main__":
    unittest.main()
