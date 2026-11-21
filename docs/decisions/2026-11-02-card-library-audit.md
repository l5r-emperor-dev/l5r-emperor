# Card library and wired-card audit (2026-11-02)

Mendon asked for every wired card and every library card to be checked against each other, the missing ones added, and the unwired ones listed.

**Missing from the library: Quelsa.** The library is the Oracle "Age of Conquest (Emperor)" legality query plus `emperor-supplemental-catalogue.json` (#812). Quelsa (`oracle-l5r-9857`, The Shadow's Embrace #69) is wired in `personality-wave-75` but, like the seven Emperor Edition cards, Oracle records him as banned from Emperor Edition tournaments (2013), so the query dropped him. `tools/content/import_supplemental_emperor.py` now takes him from the Shadow's Embrace catalogue (`OTHER_SET_CARDS`); R-040 applies (a ban is data, never shown in game). He keeps the pinned game face he already had; his Oracle scan is **not** added to `catalogue-printings.json` because that needs an art-pack release (`validate_coverage`).

**Wired but not in the library, on purpose (68):** created-only proxies (Dak-ho's Men, Witness, Zombie ...) and scan-only `user-scan-*` cards. They are not collectible; the deck pool lists them, the library does not.

**In the library but not wired (6), all by decision:** Begone, Fool!, Prideful Allies, Ningen's Treatise on the Nezumi and Peace Treaty (multiplayer-only, #742), Green Nightshade Blend and Wildfire (rejected by Mendon, 2026-10-05). Not wired.

**Wired data fixed.**
- Ten Personalities in waves 14-16 (Moshi Ikako, Mirumoto Hikaro, Rokai and Yozo, Tamori Tamoko and Shosei, Togashi Mitsu and Sakata, Xijkt) had their Gold cost, Force and Chi shifted into the wrong fields and a bogus Gold production. They now match Oracle. `Wave835Tests` used Rokai as a "3 Gold" Yojimbo; it uses Bayushi Wakui (4 Gold) and one Gold of income.
- 49 wired cards repeated their text twice (Quelsa among them); the repeat is removed.

**Rulings on the two open items (Mendon, 2026-11-02).**
- **Ikoma Shika costs 4.** The wired card and the pinned Lion/Crane deck were right; Oracle's 2 is wrong. `content/oracle/stat-corrections.json` records it and `tools/content/stat_corrections.py` applies it to the card library and deck pool (the raw Oracle snapshots keep Oracle's value).
- **A Game of Dice: the cost printed inside the Limited action is its Gold Cost.** It is wired with Gold Cost 3 (it was 0, so it was free) and is priced exactly as a card with a central cost, as A Small Favor already is. `ForgottenLegacyBatch3Tests.GameOfDiceCostTerms` checks it against Mysterious Deaths: with Yoritomo Kanaye - exp ("Actions on your Strategies cost 1 less Gold") in play A Game of Dice goes 3 to 2 and Mysterious Deaths 6 to 5.
- **Kaiu Iemasa:** the report is withdrawn; no change.

`tools/content/test_library_matches_published.py` now has no exceptions: the library, the wired cards, their numbers and their text agree.
