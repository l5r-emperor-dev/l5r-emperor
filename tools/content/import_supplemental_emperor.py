#!/usr/bin/env python3
"""Import the Emperor Edition cards that the Age of Conquest (Emperor) legality query leaves out.

Oracle's Emperor legality list omits cards it records as banned, so a handful of cards with an explicit Emperor Edition printing (issue #812) never reached
content/oracle/emperor-catalogue.json, the card library or the deck pool. content/oracle/emperor-edition/coverage-review.json names them; their raw Oracle records are
kept in content/oracle/emperor-edition/catalogue.json. This writes them, in the same schema as emperor-catalogue.json, to content/oracle/emperor-supplemental-catalogue.json.

Mendon ruled (R-040, 2026-10-10) that all seven are playable once wired: each keeps its source legality labels and any ban note as DATA ONLY, never shown in game.
Offline; standard library only.
"""
from __future__ import annotations

import json
from pathlib import Path

from import_sources import write_json
from normalize_oracle import normalize_card, plain

ROOT = Path(__file__).resolve().parents[2]
EDITION = ROOT / "content/oracle/emperor-edition"
OUTPUT = ROOT / "content/oracle/emperor-supplemental-catalogue.json"
MEMBERSHIP = "Emperor Edition printing outside the Age of Conquest (Emperor) legality query"
# Wired cards that no Emperor Edition printing or legality query brings in: (set folder, card id). Quelsa (The Shadow's Embrace #69) is wired in
# personality-wave-75 and, like the six above, is recorded by Oracle as banned from Emperor Edition tournaments (2013), so the legality query drops him. R-040 applies.
OTHER_MEMBERSHIP = "Wired card outside the Age of Conquest (Emperor) legality query"
OTHER_SET_CARDS = [("the-shadow-s-embrace", "oracle-l5r-9857")]
GAME_TYPES = {"Stronghold", "Personality", "Follower", "Item", "Spell", "Strategy", "Holding", "Event", "Celestial", "Region", "Ring"}


def build(root: Path = ROOT) -> dict:
    edition = root / "content/oracle/emperor-edition"
    review = json.loads((edition / "coverage-review.json").read_text(encoding="utf-8"))["metadata_review"]
    records = {card["id"]: card for card in json.loads((edition / "catalogue.json").read_text(encoding="utf-8"))["cards"]}
    cards = []
    for entry in review:
        record = records[entry["card_id"]]
        card = normalize_card(record["source_record"], require_membership=False)
        if not GAME_TYPES.issuperset(card["types"]):
            raise ValueError(f"{card['name']} is not a game card: {card['types']}")
        card.update(source_membership=MEMBERSHIP, legality_status="supplemental_review", historical_legality_status="playable_by_ruling_r040",
                    source_legality_labels=[plain(label) for label in record["source_legality_labels"]],
                    source_ban_notes=[plain(note) for note in record["source_record"].get("banned", [])],
                    emperor_edition_numbers=entry["printing_number"])
        cards.append(card)
    for folder, card_id in OTHER_SET_CARDS:
        record = next(c for c in json.loads((root / "content/oracle" / folder / "catalogue.json").read_text(encoding="utf-8"))["cards"] if c["id"] == card_id)
        card = normalize_card(record["source_record"], require_membership=False)
        if not GAME_TYPES.issuperset(card["types"]):
            raise ValueError(f"{card['name']} is not a game card: {card['types']}")
        card.update(source_membership=OTHER_MEMBERSHIP, legality_status="supplemental_review", historical_legality_status="playable_by_ruling_r040",
                    source_legality_labels=[plain(label) for label in record.get("source_legality_labels", [])],
                    source_ban_notes=[plain(note) for note in record["source_record"].get("banned", [])], emperor_edition_numbers=[])
        cards.append(card)
    cards.sort(key=lambda card: (card["name"].casefold(), card["id"]))
    return {"schema_version": 1, "notice": "Reference-only. These cards have explicit Emperor Edition printings but are absent from the Age of Conquest (Emperor) legality query "
            "(Oracle omits banned cards from it). No historical legality is inferred; see docs/decisions/2026-10-10-supplemental-emperor-cards.md.",
            "source": {"name": "Oracle of the Void", "derived_from": "content/oracle/emperor-edition/catalogue.json", "selected_by": "content/oracle/emperor-edition/coverage-review.json metadata_review",
                       "record_count": len(cards)}, "cards": cards}


if __name__ == "__main__":
    result = build()
    write_json(OUTPUT, result)
    print(f"Wrote {len(result['cards'])} supplemental Emperor Edition cards to {OUTPUT.relative_to(ROOT)}")
