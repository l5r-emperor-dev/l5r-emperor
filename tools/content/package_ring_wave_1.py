#!/usr/bin/env python3
"""Build Ring wave 1 (issue #194): the first CardType.Ring cards, and the first real cards
exercising the Rings concept (recruitment-as-granted-Additional-Battle-Action, Enlightenment
victory). All seven wired Rings so far: Air, Water, Void, False Air, False Water (unblocked once
their scans became available locally, issue #196, via the WebP working-copy pipeline, issue #200),
plus Earth and Fire (revalidated once their own scans became available -- their earlier blocker
notes were themselves based on mismatched/incomplete scraped text, the same class of problem issue
#195 flagged for Ring of Fire specifically; resolved by reading the actual scans directly).

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "ring-wave-1"

ABILITIES: dict[int, list[str]] = {
    6333: ["ring-of-air-recruit", "ring-of-air-straighten"],
    6339: ["ring-of-water-recruit", "ring-of-water-move-home", "ring-of-water-move-battlefield"],
    6338: ["ring-of-void-recruit", "ring-of-void-draw"],
    10531: ["false-ring-of-air-recruit", "false-ring-of-air-force"],
    10535: ["false-ring-of-water-recruit", "false-ring-of-water-attack"],
    6334: ["ring-of-earth-recruit", "ring-of-earth-raise", "ring-of-earth-lower"],
    6335: ["ring-of-fire-recruit", "ring-of-fire-destroy"],
}
EFFECTS: dict[int, str] = {}

# Ring of Air, Water, Earth, Void and Fire's own local catalogue "text" field does not match their actual
# scanned printing at all -- it carries a DIFFERENT printing's text entirely (an Interrupt-timing
# Earth ability, a duel-based Fire ability), the same class of scan/scrape mismatch issue #195
# already flagged for Ring of Fire specifically. Hand-transcribed directly from each card's own scan
# (assets/reference/oracle/emperor-edition/webp/oracle-l5r-{6333,6334,6335,6338,6339}-printing-1-image-1.webp),
# omitting the printed italic flavor line to match every other Ring's own Text field convention.
TEXT_OVERRIDE: dict[int, str] = {
    6333: "After you resolve your third or later Spell or Kiho action in one turn from cards with different titles: You may put this card into play from your hand.\nBattle/Open: Bow this card or discard it from your hand, and choose your performing Personality: Straighten his unit.",
    6339: "After you resolve your fourth or later Battle action in one battle from cards with different titles: You may put this card into play from your hand.\nBattle: Bow this card or discard it from your hand, and choose your performing Personality: Move him home or to a battlefield with one or more enemy units. If he moved, straighten his unit.",
    6338: "After you resolve your fourth or later non-Kiho action from Strategies in one phase: You may put this card into play from your hand.\nOpen: Bow this card or discard it from your hand: If any player has more cards in his hand than you do, or if this card is in play, draw a card.",
    6334: "After the resolution of a battle that you won, if there were ever any enemy units at that battlefield and none of your Personalities were destroyed during the battle: You may put this card into play from your hand.\nBattle: Even if you control no units at the current battlefield, bow this card or discard it from your hand: Give a province +3 or -3 strength.",
    6335: "After the resolution of a battle at a province, if you destroyed one or more provinces or enemy cards in that resolution: You may put this card into play from your hand.\nBattle: Bow this card or discard it from your hand, destroy your performing Personality, and target an enemy Personality with equal or lower Chi: Destroy him.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in TEXT_OVERRIDE:
            fields["Text"] = TEXT_OVERRIDE[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_ring_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
