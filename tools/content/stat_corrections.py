"""Printed stats that Oracle of the Void gets wrong, corrected in the derived card library and deck pool (content/oracle/stat-corrections.json).

Like name-corrections.json, the raw Oracle snapshots keep Oracle's value as evidence; only what the game shows and reads is corrected."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def stat_corrections(root: Path = ROOT) -> dict[tuple[str, str], tuple[str, str]]:
    """(card id, stat) -> (Oracle's value, the correct value)."""
    path = root / "content/oracle/stat-corrections.json"
    if not path.is_file():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schema_version") != 1:
        raise ValueError("Unsupported stat-corrections schema")
    return {(c["card_id"], c["stat"]): (c["oracle_value"], c["value"]) for c in data["corrections"]}


def apply_to_library_cards(cards: list[dict], root: Path = ROOT) -> None:
    """Rewrite library-shaped cards (stats: {stat: [value, ...]}) in place; refuse a correction whose Oracle value is no longer what the catalogue holds."""
    for (card_id, stat), (oracle_value, value) in stat_corrections(root).items():
        card = next((c for c in cards if c["id"] == card_id), None)
        if card is None:
            continue
        held = card.get("stats", {}).get(stat)
        if held and held[0] == oracle_value:
            card["stats"][stat] = [value, *held[1:]]
        elif held != [value]:
            raise ValueError(f"Stat correction for {card_id} {stat} expected Oracle's {oracle_value}, found {held}")
