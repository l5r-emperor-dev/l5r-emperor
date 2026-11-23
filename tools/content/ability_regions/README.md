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
