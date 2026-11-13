# At Any Cost offers Clan discount and Clan allegiance

Date: 2026-11-13. Issue #1230. Requested by Mendon (`mdornbrook`). Accepted for Clan discount and Clan allegiance; the Proclaim part is open (below).

## Request

"At Any Cost states to recruit my samurai as a battle action from the dead pile. I was not offered the clan discount or the proclaim bonus. Do the comprehensive rules restrict these options to the dynasty phase? If not, correct at any cost to apply all three of the recruit modifiers (honor requirement, proclaim, and clan discount)."

## The Comprehensive Rules

- Nothing limits these options to the Dynasty Phase. The Emperor changes, item 9: Proclaim "and the other rules about cost reduction and Honor Requirement for in-Clan personalities, apply at any time, not just in the Dynasty Phase."
- "Clan alignment and entering play": each time a player brings a Personality aligned to their Clan into play (unless it is created), they may pay 2 more Gold to ignore the Honor Requirement (Clan allegiance), pay 2 less Gold (Clan discount), both, or neither.
- Proclaim: "After you bring a Personality **in your province** with your Clan alignment into play without Clan discount, if it is your turn: Gain Honor equal to his base Personal Honor." At Any Cost's Samurai is in the dead pile, not a province.

## Decision

- At Any Cost offers four ways to pay for a Samurai of the player's Clan: full cost, Clan discount, Clan allegiance (ignoring the Honor Requirement), and both. Another Clan's Samurai is offered at full cost only. The player chooses the way to pay after choosing the Samurai.
- Proclaim is not offered. Its own text needs a Personality in a province, and the 2026-09-20 ruling on At Any Cost ([gold payment rulings](2026-09-20-gold-payment-rulings.md), ruling 4) says "This Personality may not be proclaimed". **Open for Mendon:** if At Any Cost's recruit should be proclaimable anyway, it is one change (`BringDeadPersonalityIntoPlay` opens the recruit windows with Proclaim eligibility).
- This supersedes the agent's reading in the 2026-09-20 decision that At Any Cost has "no Clan allegiance bypass"; that was not part of Mendon's ruling.
- Only At Any Cost offers the options (`CardAbilities.RecruitOffersClanModes`). The other abilities that recruit a Personality (Your Clan Needs You!, Shinden Shorai, Goju Plot, ...) are unchanged; by the same rules they could offer them too.

## Engine and client

- `CardAbilities`: one offered option per way to pay (`ClanDiscount`/`ClanAllegiance` on the command, labelled by `ChoiceLabel`); a command carrying either flag for another ability is refused. `GoldQuotes.TargetRecruit` and the At Any Cost resolution pass the flags to `QuotePurchase`.
- Client (`Main.VariantSteps.cs`): the ways to pay are one play, asked after the target ("Choose how to pay for ..."), each with its price.
- Tests: `GoldRulingsTests.AtAnyCostClanModes`; `AtAnyCostRestrictions` now expects Clan allegiance to waive the Honor Requirement. Client: `tools/check-variant-steps-ui.sh` (At Any Cost scenario).
