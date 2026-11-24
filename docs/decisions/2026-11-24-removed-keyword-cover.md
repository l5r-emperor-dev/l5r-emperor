# A removed printed keyword is covered on the card art

- **Status:** accepted (Mendon, 2026-11-24, issue #1329)

## Decision

When a Personality loses a printed keyword (a Clan alignment, Ninja or any other), the keyword is covered on the card art for as long as it is
removed. The cover is a box in the colour of the paper right around the keyword, feathered at its edges and shaded by the card's own paper
grading, so the keyword appears not to be there. Mendon first asked for a black box, then for the text-background colour instead.

## Consequences

- Core: each face-up `CardView` carries `RemovedPrintedKeywords`, the printed keywords in `CardInstance.RemovedKeywords` (permanent removals,
  Clan alignments and timed removals alike), in printed order. Public: the removal was announced.
- `tools/content/ability_regions` places every printed keyword of every playable Personality on its scan with the Windows OCR engine
  (3,929 of 3,936 on 2026-11-24) and samples the paper colour around each; the box covers the separating bullet too.
- Client: `CardTile` draws the cover wherever the card face is drawn (table, hand, inspector, previews). A keyword without a box keeps showing.
- The line is not re-centred: the remaining keywords stay where they are printed.
