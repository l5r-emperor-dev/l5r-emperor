# Card library and wired-card audit (2026-11-02)

Mendon asked for every wired card and every library card to be checked against each other, the missing ones added, and the unwired ones listed.

**Missing from the library: Quelsa.** The library is the Oracle "Age of Conquest (Emperor)" legality query plus `emperor-supplemental-catalogue.json` (#812). Quelsa (`oracle-l5r-9857`, The Shadow's Embrace #69) is wired in `personality-wave-75` but, like the seven Emperor Edition cards, Oracle records him as banned from Emperor Edition tournaments (2013), so the query dropped him. `tools/content/import_supplemental_emperor.py` now takes him from the Shadow's Embrace catalogue (`OTHER_SET_CARDS`); R-040 applies (a ban is data, never shown in game). He keeps the pinned game face he already had; his Oracle scan is **not** added to `catalogue-printings.json` because that needs an art-pack release (`validate_coverage`).

**Wired but not in the library, on purpose (68):** created-only proxies (Dak-ho's Men, Witness, Zombie ...) and scan-only `user-scan-*` cards. They are not collectible; the deck pool lists them, the library does not.

**In the library but not wired (6), all by decision:** Begone, Fool!, Prideful Allies, Ningen's Treatise on the Nezumi and Peace Treaty (multiplayer-only, #742), Green Nightshade Blend and Wildfire (rejected by Mendon, 2026-10-05). Not wired.

**Wired data fixed.**
- Ten Personalities in waves 14-16 (Moshi Ikako, Mirumoto Hikaro, Rokai and Yozo, Tamori Tamoko and Shosei, Togashi Mitsu and Sakata, Xijkt) had their Gold cost, Force and Chi shifted into the wrong fields and a bogus Gold production. They now match Oracle. `Wave835Tests` used Rokai as a "3 Gold" Yojimbo; it uses Bayushi Wakui (4 Gold) and one Gold of income.
- 49 wired cards repeated their text twice (Quelsa among them); the repeat is removed.

**Left for review, not changed.** Ikoma Shika (pinned Lion/Crane deck): wired cost 4, Oracle cost 2. A Game of Dice: wired cost 0 with the cost in the ability, Oracle card cost 3. `tools/content/test_library_matches_published.py` records both as exceptions and keeps the library, the wired cards, their numbers and their text in agreement.
