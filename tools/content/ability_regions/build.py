#!/usr/bin/env python3
"""Issue #1309: locate each marked printed ability (and printed Tactician keyword) on its card scan.

The client greys a used ability by drawing a box over its printed paragraph. This script writes those boxes to
src/L5R.Client/Content/ability-regions.json:

1. `dotnet run --project tools/AbilityRegions` lists the cards and ability groups (shared with the engine view).
2. ocr.ps1 reads each scan's text lines and word boxes with the Windows OCR engine (cached in artifacts/ability-regions).
3. Each OCR line is matched to a paragraph of the card's text; each ability group to a paragraph by its label (or by
   order when every action paragraph is accounted for). The word "Tactician" is found in the keyword line.
4. overrides.json supplies hand-checked boxes where the printed text differs from the stored text; it always wins.
5. Issue #1329: every printed keyword of a Personality is found in its keyword line ("kw": keyword -> box, the box running over
   the bullet that separates it from its neighbour so nothing is left behind when it is covered), with the paper colour
   right around each one ("kc"), so the client can cover a keyword while the Personality has lost it. A keyword the OCR
   cannot read is reported, not fatal: that card simply shows the keyword.

`--sheet N` also renders contact sheets (12 cards each) under artifacts/ability-regions for a visual review.
Cards still unresolved are listed and fail the run, so a newly wired card cannot ship without a box.
"""
from __future__ import annotations

import argparse
import difflib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
CLIENT = ROOT / "src" / "L5R.Client"
OUT = CLIENT / "Content" / "ability-regions.json"
WORK = ROOT / "artifacts" / "ability-regions"
STOP = set("a an the of to your you this card his her him it its or and target 1 2 3 per turn".split())


def tokens(text: str) -> list[str]:
    return re.findall(r"[a-z0-9]+", text.lower())


def label_score(label: str, paragraph: str) -> float:
    wanted = [t for t in tokens(label) if t not in STOP]
    have = set(tokens(paragraph))
    return sum(t in have for t in wanted) / max(1, len(wanted))


def scan_path(art: str) -> Path:
    return CLIENT / art.replace("res://", "")


def list_cards() -> list[dict]:
    WORK.mkdir(parents=True, exist_ok=True)
    out = WORK / "cards.json"
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools" / "AbilityRegions"), "-c", "Release", "--", str(out)], check=True, cwd=ROOT)
    return json.loads(out.read_text(encoding="utf-8"))


def read_ocr(cards: list[dict]) -> dict[str, dict]:
    cache = WORK / "ocr.jsonl"
    known: dict[str, dict] = {}
    if cache.exists():
        for line in cache.read_text(encoding="utf-8-sig").splitlines():
            if line.strip():
                row = json.loads(line)
                known[Path(row["path"]).name] = row
    missing = sorted({str(scan_path(c["art"])).replace("\\", "/") for c in cards if Path(c["art"]).name not in known})
    if missing:
        if sys.platform != "win32":
            sys.exit(f"{len(missing)} scans have no cached OCR, and ocr.ps1 needs Windows. Add their boxes to overrides.json instead.")
        listing = WORK / "ocr-paths.txt"
        listing.write_text("\n".join(missing), encoding="utf-8")
        fresh = WORK / "ocr-new.jsonl"
        subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(HERE / "ocr.ps1"), "-List", str(listing), "-Out", str(fresh)], check=True)
        with cache.open("a", encoding="utf-8") as sink:
            for line in fresh.read_text(encoding="utf-8-sig").splitlines():
                if line.strip():
                    row = json.loads(line)
                    known[Path(row["path"]).name] = row
                    sink.write(line + "\n")
    return known


def union(boxes: list[list[int]]) -> list[int]:
    return [min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes)]


GAP = 9  # pixels of vertical space that separate two printed paragraphs (lines within one are 0-8 apart)
TERMINAL = ('.', '!', '"', '”', ')', "'")


def rows_of(ocr: dict) -> list[dict]:
    """OCR lines merged into visual rows (a split line overlaps its neighbour vertically), top to bottom."""
    lines = []
    for line in ocr["lines"]:
        if not line["w"]:
            continue
        x0 = min(w[1] for w in line["w"]); y0 = min(w[2] for w in line["w"])
        x1 = max(w[1] + w[3] for w in line["w"]); y1 = max(w[2] + w[4] for w in line["w"])
        lines.append({"box": [x0, y0, x1, y1], "t": line["t"], "w": line["w"]})
    lines.sort(key=lambda l: (l["box"][1], l["box"][0]))
    rows: list[dict] = []
    for line in lines:
        if rows:
            last = rows[-1]["box"]
            overlap = min(last[3], line["box"][3]) - max(last[1], line["box"][1])
            if overlap > .5 * min(last[3] - last[1], line["box"][3] - line["box"][1]):
                rows[-1]["box"] = union([last, line["box"]])
                rows[-1]["t"] += " " + line["t"]
                rows[-1]["w"] += line["w"]
                continue
        rows.append(dict(line))
    return rows


def is_keyword_row(text: str) -> bool:
    letters = [c for c in text if c.isalpha()]
    # Keyword lines are printed in small capitals; a bullet alone is not enough ("a Lion Clan • Samurai Personality" sits inside ability text).
    return len(letters) >= 3 and sum(c.isupper() for c in letters) / len(letters) >= .75


def locate(card: dict, ocr: dict) -> tuple[dict, list[str]]:
    height = ocr["h"]
    paragraphs = [p for p in card["text"].split("\n") if p.strip()]
    rows = [r for r in rows_of(ocr) if height * .52 < r["box"][1] < height * .955]
    block = 0
    for i, row in enumerate(rows):
        row["kw"] = is_keyword_row(row["t"])
        if i and (row["kw"] != rows[i - 1]["kw"] or row["box"][1] - rows[i - 1]["box"][3] > GAP):
            block += 1
        row["block"] = block
        row["para"] = None
    current = 0
    for row in rows:
        if row["kw"] or sum(c.isalpha() for c in row["t"]) < 4:
            continue  # keyword lines, and fragments (a cost numeral) too short to match anything reliably
        best = None
        for i in range(current, len(paragraphs)):
            text, para = row["t"].lower(), paragraphs[i].lower()
            match = difflib.SequenceMatcher(None, text, para, autojunk=False).find_longest_match(0, len(text), 0, len(para))
            score = match.size / max(1, len(text))
            if best is None or score > best[0] + .15:
                best = (score, i)
        if best and best[0] >= .45:
            current = best[1]
            row["para"] = best[1]
    # Fill unmatched rows that belong to a paragraph: between two of its rows, or continuing it in the same block.
    changed = True
    while changed:
        changed = False
        for i, row in enumerate(rows):
            if row["para"] is not None or row["kw"] or sum(c.isalpha() for c in row["t"]) < 4:
                continue
            before = rows[i - 1] if i else None
            after = rows[i + 1] if i + 1 < len(rows) else None
            same = lambda other: other is not None and other["block"] == row["block"] and other["para"] is not None
            if same(before) and same(after) and before["para"] == after["para"]:
                row["para"] = before["para"]; changed = True
            elif same(before) and not before["t"].rstrip().endswith(TERMINAL) and not row["t"].lstrip().startswith(('"', '“', "'")):
                row["para"] = before["para"]; changed = True
            elif same(after) and not row["t"].rstrip().endswith(TERMINAL) and not row["t"].lstrip().startswith(('"', '“', "'")):
                row["para"] = after["para"]; changed = True
    found: dict[int, list[int]] = {}
    for i in {r["para"] for r in rows if r["para"] is not None}:
        mine = [r for r in rows if r["para"] == i]
        blocks = {}
        for r in mine:
            blocks.setdefault(r["block"], []).append(r)
        # A stored paragraph that spans printed paragraphs (gold production merged with an action): keep the action blocks.
        if len(blocks) > 1:
            kept = {b: rs for b, rs in blocks.items() if not re.search(r"produce \d+ gold", " ".join(r["t"] for r in rs).lower()) or len(rs) > 2}
            blocks = kept or blocks
        found[i] = union([r["box"] for rs in blocks.values() for r in rs])
    problems: list[str] = []
    regions = []
    used: set[int] = set()
    groups = card["groups"]
    for group in groups:
        scores = sorted(((max(label_score(l, paragraphs[i]) for l in group["labels"]), i) for i in found), reverse=True)
        pick = None
        if scores and scores[0][0] >= .5 and (len(scores) == 1 or scores[0][0] - scores[1][0] >= .15):
            pick = scores[0][1]
        if pick is None:
            actions = [i for i in sorted(found) if i not in used and re.match(r"^[^:]{0,40}:", paragraphs[i]) and not paragraphs[i].lower().startswith(("bow this card: produce", ":bow:"))]
            if len(actions) == len(groups) - len(used):
                pick = actions[0]
        if pick is None:
            problems.append(f"{group['key']}: paragraph not found")
            continue
        used.add(pick)
        regions.append({"k": group["key"], "ids": group["ids"], "r": found[pick]})
    result = {"w": ocr["w"], "h": height, "a": regions}
    if card["tactician"]:
        # The keyword itself, not "Master Tactician" in a title line: prefer a keyword row, a word not preceded by "MASTER".
        candidates = []
        for row in rows_of(ocr):
            words = row["w"]
            for j, w in enumerate(words):
                if re.sub(r"[^A-Z]", "", w[0].upper()) == "TACTICIAN":
                    master = j > 0 and words[j - 1][0].upper().startswith("MASTER")
                    candidates.append((not is_keyword_row(row["t"]), master, w))
        if candidates:
            w = sorted(candidates, key=lambda c: (c[0], c[1]))[0][2]
            result["t"] = [w[1], w[2], w[1] + w[3], w[2] + w[4]]
        else:
            problems.append("Tactician keyword not found")
    return result, problems


def letters(text: str) -> str:
    return re.sub(r"[^A-Z]", "", text.upper())


def locate_keywords(card: dict, ocr: dict) -> tuple[dict, list[str]]:
    """Issue #1329: each printed keyword's box on the keyword line, widened over its separating bullet."""
    wanted = card.get("keywords") or []
    if not wanted:
        return {}, []
    height = ocr["h"]
    rows = [r for r in rows_of(ocr) if height * .45 < r["box"][1] < height * .97 and is_keyword_row(r["t"])]
    found: dict[str, list[int]] = {}
    for keyword in wanted:
        parts = [letters(p) for p in keyword.split() if letters(p)]
        best = None
        for row in rows:
            words = [w for w in row["w"] if letters(w[0])]
            for i in range(len(words) - len(parts) + 1):
                span = words[i:i + len(parts)]
                score = difflib.SequenceMatcher(None, "".join(parts), "".join(letters(w[0]) for w in span)).ratio()
                if score >= .8 and (best is None or score > best[0]):
                    best = (score, words, i, i + len(parts))
        if best is None:
            continue
        _, words, start, end = best
        box = union([[w[1], w[2], w[1] + w[3], w[2] + w[4]] for w in words[start:end]])
        # Cover the bullet too: up to the next keyword's first letter, or back from the previous keyword's last letter when this one ends the line.
        if end < len(words):
            box[2] = words[end][1] - 2
        elif start > 0:
            box[0] = words[start - 1][1] + words[start - 1][3] + 2
        found[keyword] = box
    missing = [k for k in wanted if k not in found]
    return found, missing


def paper_colour(image, box: list[int]) -> str:
    """The paper colour right around one keyword, its watermark tint included: the average of thin strips above, below and at its ends."""
    x0, y0, x1, y1 = box
    strips = [(x0, y0 - 7, x1, y0 - 1), (x0, y1 + 1, x1, y1 + 7), (x0 - 8, y0, x0 - 2, y1), (x1 + 2, y0, x1 + 8, y1)]
    pixels = []
    for a, b, c, d in strips:
        a, b, c, d = max(0, a), max(0, b), min(image.width, c), min(image.height, d)
        if c > a and d > b:
            pixels += list(image.crop((a, b, c, d)).get_flattened_data())
    pixels.sort(key=lambda p: sum(p))
    # Leave out the darkest quarter (ink of the neighbouring letters and rules) and the brightest specks; average the paper between.
    paper = pixels[len(pixels) // 4: len(pixels) * 9 // 10] or [(239, 228, 200)]
    channel = lambda c: round(sum(p[c] for p in paper) / len(paper))
    return "#%02x%02x%02x" % (channel(0), channel(1), channel(2))


def keyword_colours(name: str, boxes: dict[str, list[int]]) -> dict[str, str]:
    from PIL import Image
    image = Image.open(CLIENT / "Content" / "Published" / name).convert("RGB")
    return {keyword: paper_colour(image, box) for keyword, box in boxes.items()}


def apply_override(card: dict, result: dict, override: dict) -> None:
    by_key = {r["k"]: r for r in result["a"]}
    for key, box in override.get("abilities", {}).items():
        group = next(g for g in card["groups"] if g["key"] == key)
        by_key[key] = {"k": key, "ids": group["ids"], "r": box}
    order = [g["key"] for g in card["groups"]]
    result["a"] = [by_key[k] for k in order if k in by_key]
    if "tactician" in override:
        result["t"] = override["tactician"]
    for keyword, box in override.get("keywords", {}).items():
        result.setdefault("kw", {})[keyword] = box


def sheets(cards: list[dict], regions: dict, count: int) -> None:
    from PIL import Image, ImageDraw
    names = [Path(c["art"]).name for c in cards]
    by_name = {Path(c["art"]).name: c["name"] for c in cards}
    for n in range(count):
        chunk = names[n * 12:(n + 1) * 12]
        if not chunk:
            break
        sheet = Image.new("RGB", (1200, 1260), "white")
        for i, name in enumerate(chunk):
            image = Image.open(CLIENT / "Content" / "Published" / name).convert("RGBA")
            overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
            draw = ImageDraw.Draw(overlay)
            for region in regions[name]["a"]:
                x0, y0, x1, y1 = region["r"]
                draw.rectangle([x0 - 5, y0 - 5, x1 + 5, y1 + 5], fill=(128, 128, 128, 64), outline=(255, 0, 0, 255), width=3)
            if "t" in regions[name]:
                x0, y0, x1, y1 = regions[name]["t"]
                draw.rectangle([x0 - 3, y0 - 3, x1 + 3, y1 + 3], outline=(0, 0, 255, 255), width=3)
            tile = Image.alpha_composite(image, overlay).convert("RGB").resize((300, 420))
            label = ImageDraw.Draw(tile)
            label.rectangle([0, 0, 300, 14], fill=(0, 0, 0))
            label.text((3, 1), name.removeprefix("oracle-l5r-").removesuffix(".jpg") + " " + by_name[name], fill=(255, 255, 0))
            sheet.paste(tile, ((i % 4) * 300, (i // 4) * 420))
        sheet.save(WORK / f"sheet-{n + 1:03}.png")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--sheet", type=int, default=0, help="render this many 12-card review sheets")
    args = parser.parse_args()
    cards = list_cards()
    ocr = read_ocr(cards)
    overrides = json.loads((HERE / "overrides.json").read_text(encoding="utf-8"))["cards"]
    regions: dict[str, dict] = {}
    unresolved: list[str] = []
    keywords_unread: list[str] = []
    for card in cards:
        name = Path(card["art"]).name
        result, problems = locate(card, ocr[name])
        keyword_boxes, unread = locate_keywords(card, ocr[name])
        if keyword_boxes:
            result["kw"] = keyword_boxes
        keywords_unread.extend(f"{name} {card['name']}: {k}" for k in unread)
        if name in overrides:
            apply_override(card, result, overrides[name])
            covered = {r["k"] for r in result["a"]}
            problems = [p for p in problems if not (p.split(":")[0] in covered or (p.startswith("Tactician") and "t" in result))]
        if problems:
            unresolved.append(f"{name} {card['name']}: " + "; ".join(problems))
        if result.get("kw"):
            result["kc"] = keyword_colours(name, result["kw"])
        regions[name] = result
    OUT.write_text(json.dumps({"version": 1, "source": "tools/content/ability_regions/build.py", "cards": dict(sorted(regions.items()))}, separators=(",", ":")) + "\n", encoding="utf-8")
    if args.sheet:
        sheets(cards, regions, args.sheet)
    wanted = sum(len(c.get("keywords") or []) for c in cards)
    print(f"{len(regions)} cards, {sum(len(r['a']) for r in regions.values())} ability boxes, {sum('t' in r for r in regions.values())} Tactician boxes, "
          f"{sum(len(r.get('kw', {})) for r in regions.values())} of {wanted} printed keywords -> {OUT.relative_to(ROOT)}")
    (WORK / "keywords-unread.txt").write_text("".join(k + chr(10) for k in keywords_unread), encoding="utf-8")
    for line in unresolved:
        print("UNRESOLVED " + line)
    return 1 if unresolved else 0


if __name__ == "__main__":
    sys.exit(main())
