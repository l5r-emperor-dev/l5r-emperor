# My Collection: the per-printing file

Accepted by Mendon (issue #807). Supersedes the CSV paragraph and the "set printings are not separate inventory buckets" sentence of [2026-10-01-my-collection.md](2026-10-01-my-collection.md); everything else there stands.

## File

`Set,Card Name,Card Type,Rarity,Edition Code,Count` — one row per printing, exactly the columns of Mendon's `Collection Template.csv`. Import reads it (UTF-8, with or without a BOM, or a Windows-1252 file saved by a spreadsheet); Export writes it as UTF-8 with a BOM, and the CSV template button writes every printing with `0`.

The catalogue carries no printing data, so the printings the file is read against ship as `src/L5R.Client/Content/collection-printings.txt` (the template's rows, counts reset to `0`, `-` kept; a `.txt` so Godot does not import it as a translation). Two rows that differed only by a curly apostrophe were collapsed to one. `EOW` is read as `EoW`; edition codes match case-insensitively.

## Counts

* A number, `0`–`99999`, is the copies owned of that printing. `0` clears it.
* `-` (Tokens) and a blank are not collectible: the row is not read, and is written back as `-` on export.
* A printing is the card plus Edition Code plus Rarity (one card can be printed twice in a set at two rarities).

## Storage

The collection is stored per ownership bucket in `my-collection.json` (schema 2; schema 1 files still load): the card's ID alone is copies of no recorded printing, `ID@EDITION` or `ID@EDITION@Rarity` a printing. A card's total — what the filters, the Owned/Unowned views and deck shortages read — is the sum of its buckets, so nothing outside the collection changes.

* Editing a card's total in the editor adds copies to the unassigned bucket; removing copies takes the unassigned ones first, then printings from the last bucket name backwards.
* Importing a card by printing replaces its unassigned copies with the printings the file lists; printings of that card the file does not list stay. The older `card_id,card_name,owned_count` file is still read and sets a card's total, replacing all of its buckets.
* Export writes every printing of the list in order, then one extra row (blank Set, Type, Rarity and Edition) for copies recorded against no printing, so an export always imports back to exactly the same counts.

## Names

A row's name is matched to the catalogue after folding curly quotes and reading `(Exp)`, `(Exp.)`, `(Exp2)`, `(Exp. 3)`, `(Inexp)`, `Experienced`, `Experienced N`, `Experienced CoM` and `Inexperienced` as the catalogue's ` - exp`, ` - expN`, ` - inexperienced`. A title that matches only other versions is never guessed; it goes to the existing review screen. A row listed as `0` that cannot be matched is skipped, because every unowned row of the template would otherwise need review. Against today's catalogue about 96% of the template resolves without review; the rest are names the catalogue spells differently or lacks, and are reviewed, excluded or corrected like any other row.

## Closest-name guess

A name no card has (a typo, a different spelling) is scored against the whole catalogue with the deck troubleshooter's scoring (`DeckTroubleshooter.Rank`, the one Sun and Moon deck lists use). One clear winner (score of at least 0.9, ahead of the runner-up by 0.06, and of the Experience level the row names) is applied and shown in the review as "Matched ... by closest name". Anything less sure is offered as the closest cards to choose from, and a name nothing resembles is an error. A guessed row whose printing the file also lists under the right spelling is left out. "Exclude unresolved" leaves out every row still without a card so the rest can be applied. The review draws only the rows needing a decision plus a short sample, so a 2,000-row file opens in under a second.

## Update, issue #1141: one detailed file

The collection file is now `Set,Card Name,Card Type,Rarity,Clan,Limit,Count`. Limit is `U` (Unique), `S` (Singular), `X` (the card allows more than the usual number of copies) or `N` (normal). Export and the template list every card of `Content/collection-printings.txt` (2,374 rows, including Promo printings, Tokens and every Honor & Treachery card as Fixed) and report a card with none owned on a line with `0` in Count. Tokens are counted like any card; the `-` convention is gone (a `-` or blank Count is still skipped on import). Edition codes are no longer in the file: a printing is the set plus rarity, and copies recorded under an old code (`ID@EoW@Rare`) are renamed to the set (`ID@Embers of War@Rare`) when a collection is read. Promotional sets read "Promotional Emperor" and "Promotional Celestial".
