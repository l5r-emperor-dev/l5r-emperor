# Every recruit offers the Clan options; Proclaim in any phase on your own turn

Date: 2026-11-13. Issue #1232. Ruled by Mendon (`mdornbrook`). Follows [At Any Cost: Clan discount and Clan allegiance](2026-11-13-at-any-cost-clan-options.md) (issue #1230).

## Ruling

"Recruit should always behave the same unless otherwise specified. In which case, clan affiliated modifiers should be allowed for. If a personality is recruited out of the dynasty phase and a player proclaims them, then that would consume their once per turn use of Proclaim. Check the comprehensive rules to see if proclaim is restricted to recruits on your own turn. If not, then a player may proclaim a personality as long as they are enabled to recruit during that player's turn and have not already proclaimed a personality on that turn."

## The Comprehensive Rules

- "Clan alignment and entering play": each time a player brings a Personality aligned to their Clan into play, unless it is created, they may pay 2 more Gold to ignore the Honor Requirement (Clan allegiance), 2 less Gold (Clan discount), both or neither. The Emperor changes, item 9: these apply "at any time, not just in the Dynasty Phase".
- Proclaim **is** restricted to your own turn: "After you bring a Personality **in your province** with your Clan alignment into play without Clan discount, **if it is your turn**: Gain Honor equal to his base Personal Honor."

## Decision

- Every ability that brings a Personality into play at his recruit price (`GoldCostRule.TargetRecruit`) offers the four ways to pay: Your Clan Needs You!, Shinden Shorai, Requesting Reinforcements, Rise of the Dark Naga, Masaya, Essence of Undeath, Goju Plot, A Yojimbo's Duty, Kunji, Black Pearl, At Any Cost and the other dead-pile recruits. A Personality of another Clan is offered at full cost only; a Clan way to pay that no target allows is not listed.
- Proclaim, in any phase, needs all of: a Personality from your province, aligned to your Clan, not bought with the Clan discount (Clan allegiance keeps it), on your own turn, and no Proclaim yet this turn. Taking it spends the turn's Proclaim. A recruit from a discard pile or a deck is not "in your province" and is never proclaimed (At Any Cost's "may not be proclaimed" stands).
- Not covered (documented limits): the Holding recruits (the Clan options are for Personalities); recruits whose cards are picked in slots (Resurrection, Renewal); and the deck-search recruits paid in a later step (Sacrifice of Pawns, Searching for Answers).

## Engine and client

- `CardAbilities.RecruitOffersClanModes`: every `TargetRecruit` ability except the Holding recruits and the slot ones. Each recruit's own checks (`QuotePurchase` at validation and resolution, `RecruitFromProvince`, `RiseOfTheDarkNagaRecruit`) use the command's `ClanDiscount`/`ClanAllegiance`, so Honor Requirement and Proclaim eligibility follow the way to pay.
- The client already asks the way to pay after the target for any `TargetRecruit` ability (issue #1230).
- Test: `GoldRulingsTests.RecruitAbilityClanModesAndProclaim` (Your Clan Needs You! in the Battle Phase: four prices; full cost offers Proclaim and spends it; Clan discount, a second Proclaim in the turn, and the opponent's turn do not; Clan allegiance below the Honor Requirement keeps it).
