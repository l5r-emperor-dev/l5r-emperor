#!/usr/bin/env python3
"""Build the Emperor-legal (Age of Conquest arc) deck-building card pool.

Filters the full Oracle catalogue (content/oracle/emperor-catalogue.json) to
cards belonging to the Age of Conquest (Emperor) legality arc and excludes
non-card catalogue noise (Proxy entries). This is deliberately separate from
the playable content pool (content/published/lion-crane-v1.json): none of
these cards carry rules automation, so this artifact is reference data for
browsing and deck construction, never a source of executable card behavior.
Only Python's standard library is required.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "content/oracle/emperor-catalogue.json"
PUBLISHED = ROOT / "content/published/lion-crane-v1.json"
PUBLISHED_WAVES = (ROOT / "content/published/vanilla-wave-1.json",)
SUPPLEMENTAL = ROOT / "content/oracle/emperor-supplemental-catalogue.json"  # Emperor Edition cards the legality query omits (issue #812)
OUTPUT = ROOT / "content/deck-pool.json"
ARC = "Age of Conquest (Emperor)"
EXCLUDED_TYPES = {"Proxy"}


def playable_ids(*published_paths: Path) -> set[str]:
    ids: set[str] = set()
    for path in published_paths:
        if not path.exists():
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        ids |= {card["Id"] for card in data.get("Cards", [])}
    return ids


def build(source: Path = SOURCE, published_path: Path = PUBLISHED, extra_published_paths: tuple[Path, ...] = PUBLISHED_WAVES, supplemental: Path | None = None) -> dict:
    data = json.loads(source.read_text(encoding="utf-8"))
    if supplemental is None and source == SOURCE:
        supplemental = SUPPLEMENTAL  # a fixture catalogue never picks up the real supplemental cards
    extra = json.loads(supplemental.read_text(encoding="utf-8"))["cards"] if supplemental is not None and supplemental.exists() else []
    extra_ids = {card["id"] for card in extra}
    if source == SOURCE:  # a fixture catalogue never picks up the real corrections
        from stat_corrections import apply_to_library_cards
        apply_to_library_cards([*data["cards"], *extra])
    playable = playable_ids(published_path, *extra_published_paths)
    entries = []
    for card in [*data["cards"], *extra]:
        supplemental_card = card["id"] in extra_ids
        if card.get("source_membership") != ARC and not supplemental_card:
            continue
        types = card.get("types") or []
        if any(t in EXCLUDED_TYPES for t in types):
            continue
        stats = {name: (values[0] if values else None) for name, values in (card.get("stats") or {}).items()}
        entries.append({
            "id": card["id"],
            "name": card["name"],
            "types": types,
            "clans": card.get("clans") or [],
            "keywords": card.get("keywords") or [],
            "deckTypes": card.get("deck_types") or [],
            "stats": stats,
            "text": card.get("current_oracle_text") or "",
            "playable": card["id"] in playable,
            **({"supplemental": True} if supplemental_card else {}),
        })
    entries.sort(key=lambda entry: (entry["name"], entry["id"]))
    return {"schemaVersion": 1, "sourceArc": ARC, "count": len(entries), "cards": entries}


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    pool = build()
    write_json(OUTPUT, pool)
    print(f"Wrote {pool['count']} Age of Conquest (Emperor) cards to {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
