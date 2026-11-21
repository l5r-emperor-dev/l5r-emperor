# CCG strategy advice and recommendations to test on the Contender (2026-11-08, issue #1185)

Compiled for the bot-vs-bot tests in `docs/bots/experiments/2026-11-08-research-arms.md`. **Not from Reddit**: reddit.com is blocked for the search and fetch tools and the browser extension could not connect, so r/l5r
was not read. Nine CCG pages were read instead, all about the collectible card game (the Emperor and Ivory Editions), none about the 2018 LCG. The RPG was not mined: it has no card-timing or economy advice a bot can be
tested against. Claims are paraphrased, with the edition the author wrote about; the articles are community blogs (2012-2014), so some advice is edition-specific, and the engine implements the Emperor Edition.

## What the sources say

- **Flushing** (Emperor, with an Ivory note): discard every unbought face-up card you cannot afford, keep what you can buy next turn; every card kept is one fewer new card seen; flushing early rarely loses a game, digging too
  slowly does.
- **Offense** (Ivory): always be attacking; attack when your Force beats the Province plus what the opponent could add from hand (play-everything-versus-they-play-nothing test); conservation loses games for the attacker;
  stop attacking once the opponent reaches 30 Honor, since a failed attack can let them cross their win threshold; read the opponent's hand size.
- **Defense** (Emperor): provinces are "the clock"; if you cannot save one, let it go and make them pay; plan about one defender per expected enemy action; absent actions add actions; running a Personality home denies
  opposed battle actions; against Cavalry send minimal defenders.
- **Tempo** (Ivory): without presence the opponent can never take tempo from you, so removing units is a priority; when losing, save resources for the next fight.
- **Economy** (Ivory, Emperor): Holdings enter bowed, so early economy is slow; an efficient scheme is about X cost for X+ production, 15 to 18 Holdings in a deck; a "boxable" Personality can be bought on turn one with the
  Stronghold's Gold alone; a single secondhand summary says the usual first turn is two Holdings (unverified).
- **Honor decks** (Ivory): Proclaim for 3 to 4 Honor every turn; the Imperial Favor is a key tool against military decks.
- **Emperor Edition meta** (early 2012): military dominant, attachments essential, attachment removal heavily played.

## Recommendations, each a switch on `BotContext.Contender(...)` and an arm

| # | Arm | Change | Source support |
| --- | --- | --- | --- |
| 1 | `rec1-discard2` | Keep only the best set of cards the Gold can buy together next turn; flush the rest | Flushing |
| 2a | `rec2a-attack-add` | The attack estimate may add attacks (win without razing) but never cancel the champion's | Offense (always attack) |
| 2b | `rec2b-attack-veto` | The attack estimate may cancel the champion's attack but never add one | Offense (the other half of the -14) |
| 3 | `rec3-honor30` | With the opponent at 30 to 39 Honor, attack only when the estimate wins by a margin of 3 | Offense |
| 4 | `rec4-presence` | In the Action phase, bow or destroy the opponent's only possible defender when the attack then wins | Tempo; interview round 4 |
| 5 | `rec5-nolost` | Spend no Gold or cards on a battle the whole hand cannot turn; always defend the last Province with everything | Defense, Tempo; interview round 4 |
| 6 | (not built) | Defender count | Defense; already covered by the champion's informed defence |
| 7 | `rec7-open2` | On the first turn, buy Holdings before a Personality the Stronghold's Gold alone cannot pay for | Economy (boxable; two-holding claim unverified) |
| 8 | `rec8-proclaim` | Double the Proclaim bonus for a High-Honor deck | Honor decks; interview |

Where the sources and the interview disagree: the sources flush more aggressively than the interview's discard rule, and they err toward attacking and assume the opponent plays nothing from hand, where the interview
weighs the enemy hand. Arms 1, 2a and 2b test which is right in this engine.

## Sources

- [Three Things To Expect in Early Emperor Edition](https://www.strangeassembly.com/?p=1429)
- [L5R Newbie Corner: Attack!! Pt 1. Offense](https://www.strangeassembly.com/2013/l5r-newbie-corner-attack-pt-1-offense)
- [L5R Newbie Corner: Attack!! Pt 2. Defense](https://www.strangeassembly.com/2013/l5r-newbie-corner-attack-pt-2-defense)
- [L5R Newbie Corner: Attack!! Pt 3. Tempo](https://www.strangeassembly.com/2014/l5r-newbie-corner-attack-pt-3-tempo)
- [L5R Newbie Corner: When to Flush](https://www.strangeassembly.com/2013/l5r-newbie-corner-when-to-flush)
- [L5R Newbie Corner: On the Subject of Gold Schemes](https://www.strangeassembly.com/2014/l5r-newbie-corner-on-the-subject-of-gold-schemes)
- [L5R Newbie Corner: Q and/or A](https://www.strangeassembly.com/2014/l5r-newbie-corner-q-andor-a)
- [Review: Emperor Edition (L5R CCG)](https://www.strangeassembly.com/2012/review-emperor-edition-l5r-ccg)
- [L5R CCG - Break it down for me please (FFG forum archive)](https://ffg-forum-archive.entropicdreams.com/topic/201699-l5r-ccg-break-it-down-for-me-please)
