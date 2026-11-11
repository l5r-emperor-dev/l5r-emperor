# Practice difficulty: Easy, Moderate, Hard

Status: accepted (Mendon, 2026-11-11). Issue #1214.

## Decision

Practice against the bot has a **Difficulty** choice on the Practice setup screen, remembered with the deck choices:

| Level | Bot | How it is built |
| --- | --- | --- |
| Easy (default) | The Champion | `new BotContext()`, the bot practice played against before this change |
| Moderate | The Contender | `BotContext.Contender()` (`docs/bots/CONTENDER.md`) |
| Hard | Conan | `BotContext.Hard()`: the Contender's tuned rules plus `look`, `lkDef`, `lkVar`, `lkPlan` |

Mendon asked for Conan to ship "in its current winning-most state". That is `look,lkDef,lkVar,lkPlan` on the Contender's tuned rules: 71.2%, 73.4% and 72.8% against the Contender on blocks 7000, 7200 and 7300 (`docs/bots/experiments/2026-11-10-conan-vs-contender.md`). Settings that measured worse or not at all better stay off: `lkAtk` (replaced by `lkPlan`), `lkVal` (−20), `lkBuy` (−10), `lkIntent` (−3), `lkFar` (−7), `lkM`, `lkHS`, `lkPS`, `clock`, more worlds (`lkN=8`, +9 over 728 games, not reliable). Making these rules the Hard level's setting satisfies the roadmap's "no setting becomes a default on main without Mendon's approval" for this set; the experimental rules still need it.

The arena's `conan` arm and `BotContext.ConanRules` are unchanged, so the tuning campaign's arms keep their meaning. The `hard` arm is exactly the shipped Hard level.

## How Hard runs in the client

- Hard searches, so it needs `BotContext.Sampler`. The client builds it from a **private copy** of the match made for each decision (`MatchEngine.Clone()`), then `Sample(seat, seed)` redeals every card hidden from the bot's seat on that copy. The bot never reads the real match's hidden cards; `HiddenSampleTests` still guards the sampler.
- A decision takes about 1.3 s at the median, 4 s at p90 and at most 10 s (`lkT`; past it the Contender's own choice stands). It runs on a worker thread (`MatchSession.StepPracticeOpponentAsync`), so the table stays responsive. The answer is played only if the match is unchanged; a refusal is reported to the bot's memory and it chooses again, as for the other levels.
- If a Hard decision throws, the opponent plays the Champion's move for that decision, so it always acts.
- Easy and Moderate decide on the UI thread as before.

## Limits

- Private rooms and online play are unchanged. Difficulty applies to local Practice against the bot only.
- Hard is not stronger by a fixed amount for every deck. Conan pilots some decks worse (Crane preset, Crab Control, Phoenix Talons in the tuning blocks), and the 80% goal against the Contender is not met yet. When the campaign merges a better Conan, change `BotContext.HardRules`, not the level's definition.
- Hard Conan is a bot-vs-bot result. Nobody has measured it against people.
- **Hard knows the person's decklist.** The sampler redeals the real multiset of the person's hidden cards, so Conan knows which cards are in their hand and decks (never where). The roadmap asked for a blind sampler to be measured before Conan played people; this ships without it, at Mendon's request, and the gap is recorded in `docs/bots/CONAN.md`.
