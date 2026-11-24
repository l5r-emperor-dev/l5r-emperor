# Ability regions (issue #1309)

`build.py` writes `src/L5R.Client/Content/ability-regions.json`: for each card whose printed abilities the client greys when used, the pixel box of each ability's paragraph on its scan, plus the printed "Tactician" keyword.

```
python tools/content/ability_regions/build.py            # rebuild the JSON (fails on any card it cannot place)
python tools/content/ability_regions/build.py --sheet 100  # also write 12-card review sheets to artifacts/ability-regions
```

- The card list comes from `dotnet run --project tools/AbilityRegions` (`AbilityRegionCards` in L5R.Core, the same selection the engine view uses).
- Text positions come from the Windows OCR engine (`ocr.ps1`, no install), cached in `artifacts/ability-regions/ocr.jsonl`. On a Mac, add boxes to `overrides.json` by hand.
- `overrides.json` holds hand-checked boxes (scan pixels `x0,y0,x1,y1`) where the printed text differs from the stored text; it always wins.
- `ability-region-coverage` in the Core tests fails when a newly wired card has no box. Review new boxes on the sheets before committing.

## Printed keywords (issue #1329)

Every playable Personality also gets `kw` (each printed keyword, Clan alignments such as "Lion Clan" included, mapped to its box on the keyword line) and `kc` (the paper colour right around each keyword). The box runs over the bullet that separates the keyword from its neighbour, so nothing is left behind when the client covers a keyword the Personality has lost. A keyword the OCR cannot read is listed in `artifacts/ability-regions/keywords-unread.txt` and does not fail the run: that card simply keeps showing the keyword. Add its box under `"keywords"` in an `overrides.json` entry to fix it. On 2026-11-24, 3,929 of 3,936 printed keywords were placed.
