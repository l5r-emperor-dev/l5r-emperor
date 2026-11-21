import unittest

from build_deck_pool import build as build_pool
from import_supplemental_emperor import MEMBERSHIP, OTHER_MEMBERSHIP, build

REVIEWED = {"Caught Unawares", "Cross-Clan Wedding", "Kalani's Landing", "One Koku", "The Duty of the Crab", "The Governor's Estate", "Well-Tended Farm", "Quelsa"}  # Quelsa (The Shadow's Embrace) joins the seven: wired, and dropped by the same legality query


class SupplementalEmperorTests(unittest.TestCase):
    def test_imports_exactly_the_reviewed_cards(self):
        cards = build()["cards"]
        self.assertEqual({card["name"] for card in cards}, REVIEWED)
        self.assertTrue(all(card["source_membership"] in (MEMBERSHIP, OTHER_MEMBERSHIP) and not card["playable"] for card in cards))

    def test_keeps_ban_notes_as_data_and_marks_them_playable_by_ruling(self):
        by_name = {card["name"]: card for card in build()["cards"]}
        self.assertIn("September 21st, 2013", by_name["Kalani's Landing"]["source_ban_notes"][0])
        self.assertEqual(by_name["The Governor's Estate"]["source_ban_notes"], [])
        self.assertTrue(all(card["historical_legality_status"] == "playable_by_ruling_r040" for card in by_name.values()))

    def test_stronghold_keeps_its_stats_and_clan(self):
        kalani = next(card for card in build()["cards"] if card["name"] == "Kalani's Landing")
        self.assertEqual((kalani["clans"], kalani["stats"]["production"], kalani["stats"]["strength"]), (["Mantis"], ["4"], ["7"]))

    def test_deck_pool_lists_them_flagged_supplemental(self):
        flagged = {card["name"] for card in build_pool()["cards"] if card.get("supplemental")}
        self.assertEqual(flagged, REVIEWED)


if __name__ == "__main__":
    unittest.main()
