#!/usr/bin/env python3
"""Package the offline reference library for Godot exports; no network access."""

from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path, PurePosixPath


def package(root: Path) -> dict[str, int]:
    def read(relative: str) -> dict:
        return json.loads((root / relative).read_text(encoding="utf-8"))

    catalogue = read("content/oracle/catalogue-index.json")
    library = read("content/deck-library.json")
    manifest = read("content/source-manifest.json")
    target = root / "src/L5R.Client/Content"
    target.mkdir(parents=True, exist_ok=True)
    cards = catalogue["cards"]
    supplemental_file = root / "content/oracle/emperor-supplemental-catalogue.json"  # Emperor Edition cards outside the legality query (issue #812)
    if supplemental_file.is_file():
        fields = catalogue["cards"][0].keys()
        cards = [*cards, *({field: card[field] for field in fields if field in card} for card in read("content/oracle/emperor-supplemental-catalogue.json")["cards"])]
    from stat_corrections import apply_to_library_cards  # printed stats Oracle gets wrong (Ikoma Shika's cost)
    apply_to_library_cards(cards, root)
    card_ids = {card["id"] for card in cards}
    if len(card_ids) != len(cards):
        raise ValueError("Catalogue contains duplicate canonical IDs")
    references_file = root / "content/legacy/canonical-card-references.json"
    if references_file.is_file():
        source_references = read("content/legacy/canonical-card-references.json")
        if source_references.get("schema_version") != 1:
            raise ValueError("Unsupported legacy canonical-reference schema")
        reference_version = str(source_references.get("source", {}).get("database_version", "unknown"))
        by_card = {card["id"]: card for card in cards}
        seen_references = set()
        for reference in source_references.get("references", []):
            canonical_id = reference["canonical_card_id"]
            if canonical_id not in by_card:
                raise ValueError(f"Legacy reference points outside the Emperor catalogue: {canonical_id}")
            key = (canonical_id, str(reference["legacy_card_id"]))
            if key in seen_references:
                raise ValueError(f"Duplicate legacy canonical reference: {key}")
            seen_references.add(key)
            comparison = reference["text_comparison"]
            if comparison not in {"same_plain_text", "different_text_needs_review"}:
                raise ValueError(f"Unknown legacy text comparison: {comparison}")
            by_card[canonical_id].setdefault("legacy_references", []).append({
                "legacy_card_id": str(reference["legacy_card_id"]),
                "source_card_id": str(reference["source_card_id"]),
                "name": reference["name"], "legacy_text": reference["legacy_text"],
                "declared_legalities": reference.get("declared_legalities", []),
                "image_editions": reference.get("image_editions", []),
                "text_comparison": comparison, "database_version": reference_version,
                "match_evidence": reference.get("match_evidence", []),
            })
    decks = []
    for deck in library["decks"]:
        entries = []
        for entry in deck["entries"]:
            resolution = entry["resolution"]
            card_id = resolution.get("card_id")
            if card_id is not None and card_id not in card_ids:
                raise ValueError(f"Resolved card missing from catalogue: {card_id}")
            entries.append({
                "name": entry["name"], "quantity": entry["quantity"],
                "card_id": card_id, "resolution_status": resolution["status"],
                "resolution_reason": resolution["reason"],
                "types": resolution.get("types", []),
                "deck_types": resolution.get("deck_types", []),
            })
        decks.append({
            "id": deck["id"], "name": deck["name"],
            "declared_era": deck["declared_era"],
            "card_count": deck["card_count"], "entry_count": deck["entry_count"],
            "legality_status": "unverified", "playable": False,
            "partition_counts": deck["reference_partition_counts"],
            "resolution_counts": deck["resolution_counts"], "entries": entries,
        })
    mapping_file = root / "content/legacy/artwork-mappings.json"
    mapping_source = {}
    mappings = {}
    known_artwork = {art["id"] for art in manifest["artwork"]}
    if len(known_artwork) != len(manifest["artwork"]):
        raise ValueError("Source manifest contains duplicate artwork IDs")
    if mapping_file.is_file():
        source_mappings = read("content/legacy/artwork-mappings.json")
        if source_mappings.get("schema_version") != 1:
            raise ValueError("Unsupported legacy artwork mapping schema")
        mapping_source = source_mappings.get("source", {})
        for mapping in source_mappings.get("mappings", []):
            art_id = mapping["artwork_id"]
            if art_id not in known_artwork:
                raise ValueError(f"Mapping refers to artwork outside the imported collection: {art_id}")
            if art_id in mappings:
                raise ValueError(f"Duplicate artwork mapping: {art_id}")
            if mapping.get("status") not in {"matched", "ambiguous", "unmatched"}:
                raise ValueError(f"Unsupported artwork mapping status: {art_id}")
            mappings[art_id] = mapping

    artwork = []
    for art in manifest["artwork"]:
        source = (root / art["path"]).resolve()
        source.relative_to((root / "assets/reference").resolve())
        if source.suffix.lower() not in {".jpg", ".jpeg", ".png"}:
            raise ValueError(f"Not an image: {source}")
        if hashlib.sha256(source.read_bytes()).hexdigest() != art["sha256"]:
            raise ValueError(f"Artwork differs from imported source: {source}")
        relative = Path("Art") / art["source_set_folder"] / source.name
        destination = target / relative
        destination.resolve().relative_to(target.resolve())
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        packaged_art = {
            "id": art["id"], "filename": source.name,
            "resource_path": "res://Content/" + relative.as_posix(),
            "source_folder": art["source_set_folder"],
            "width": art["width"], "height": art["height"],
            "sha256": art["sha256"], "mapping_status": art["mapping_status"],
            "legality_status": "unverified",
        }
        mapping = mappings.get(art["id"])
        if mapping is not None and mapping["status"] == "matched":
            declared_path = str(mapping.get("source_image_path", "")).replace("\\", "/")
            parsed_path = PurePosixPath(declared_path)
            if parsed_path.is_absolute() or ".." in parsed_path.parts or ":" in declared_path:
                raise ValueError(f"Legacy image path is not a portable source reference: {art['id']}")
            expected_path = source.relative_to((root / "assets/reference/sun-and-moon").resolve()).as_posix()
            if declared_path != expected_path:
                raise ValueError(f"Legacy image path does not identify this scan: {art['id']}")
            canonical_id = mapping.get("canonical_card_id")
            if canonical_id is not None and canonical_id not in card_ids:
                raise ValueError(f"Artwork mapping references a missing canonical card: {canonical_id}")
            packaged_art.update({
                "card_name": mapping.get("name", ""),
                "legacy_card_id": str(mapping.get("legacy_card_id", "")),
                "source_card_id": str(mapping.get("source_card_id", "")),
                "card_type": mapping.get("type", ""),
                "image_edition": mapping.get("image_edition", ""),
                "source_image_path": declared_path,
                "declared_legalities": mapping.get("declared_legalities", []),
                "legacy_text": mapping.get("text", ""),
                "database_version": str(mapping_source.get("database_version", "unknown")),
                "canonical_card_id": canonical_id,
                "mapping_status": "source_path_match",
                "mapping_reason": mapping.get("reason", "The database image path identifies this uploaded file."),
            })
        elif mapping is not None:
            packaged_art["mapping_status"] = "ambiguous" if mapping["status"] == "ambiguous" else "unmapped"
            packaged_art["mapping_reason"] = mapping.get("reason", "No unique source path match.")
        artwork.append(packaged_art)
    result = {
        "schema_version": 1,
        "notice": "Reference only. Imported decks are not playable. Source era labels and current Oracle catalogue membership do not establish historical legality.",
        "catalogue_source": catalogue["source"],
        "catalogue_notice": catalogue["notice"],
        "cards": cards,
        "decks": sorted(decks, key=lambda d: (d["declared_era"] != "Emperor", d["name"].casefold())),
        "artwork": artwork,
    }
    output = target / "library.json"
    output.write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":"), sort_keys=True) + "\n", encoding="utf-8")
    if (root / "content/oracle/emperor-catalogue.json").is_file():
        from package_catalogue import package as package_printings
        package_printings(root)
    return {"cards": len(cards), "decks": len(decks), "artwork": len(artwork),
            "source_linked_artwork": sum(art["mapping_status"] == "source_path_match" for art in artwork),
            "legacy_references": sum(len(card.get("legacy_references", [])) for card in cards),
            "json_bytes": output.stat().st_size}


if __name__ == "__main__":
    print(json.dumps(package(Path(__file__).resolve().parents[2]), indent=2))
