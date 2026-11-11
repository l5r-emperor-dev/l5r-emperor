# Shared decisions

Record durable decisions here when they affect future work: an agreed period printing, a rules interpretation, an approved UI interaction, or a shared engine/client contract. The agent writes the record so users do not repeat the same explanation in new sessions.

Use one Markdown file per decision, named `YYYY-MM-DD-topic.md`. Include: decision; reason and evidence; relevant lane/lead; affected files; issue/PR; and whether proposed, accepted, or superseded. A proposal or old chat assertion is not automatically accepted. Link superseding decisions instead of silently replacing historical evidence.

Current standing decisions are in `AI_HANDOFF.md` and the pinned rules coverage report. Current work belongs in issues/PRs and `docs/handoffs/tasks/`, not this index. Private auto-memory can be a convenience but never overrides the checked-in team record or the user's latest instruction.

Mendon's confirmed rulings, once he has stated them, are also indexed by number in
[`docs/rules/RULINGS.md`](../rules/RULINGS.md) — check there before re-deriving a rules
interpretation this register's decision docs already settled.

- [Duel verdict with retained evidence](2026-09-17-duel-verdict-evidence.md) — centered result, authoritative reason and continuous card transitions.

- [Inspect every own focused card](2026-09-17-own-focus-inspection.md) — direct private inspection after commitment, from every supported source.

- [Shared live-match duel and sandbox](2026-09-17-live-duel-integration.md) — one approved screen, separate sessions, full table-to-duel round trip.

- [Suspicions on the owner's Events Phase](2026-09-22-suspicions-owner-events-phase.md) — fires only on its owner's turns; an Event already face up resolves in the Events Phase.

- [Personality-destruction Reaction triggers](2026-09-29-personality-destruction-reactions.md) — confirmed play-to-dead trigger, with narrower action-source wording and no later discard-to-dead trigger.

- [Rename the "Fallen" UI label to "Dead"](2026-09-30-dead-not-fallen.md) — proposed, for Alfredo: "Fallen" is a printed keyword later in the Emperor Arc and collides with the client's own word for the dead-personality pile.

- [Regret and Embracing Virtue](2026-10-03-regret-embracing-virtue.md) — accepted rulings R-026 to R-030; multi-attach from a discard pile, each card its own equip, the end-of-turn return, and the client's two-slot contract.

- [Pass all battles](2026-10-04-pass-all-battles.md) — accepted: a button that passes a player's actions across every remaining battle; the engine passes and selects battles for them until the opponent acts or a decision needs them.

- [Catalogue check-box filters](2026-10-04-catalogue-checkbox-filters.md) — accepted: every Card Library and Deck Builder filter is an include/exclude check-box group with Select all and Deselect all; the default shape for new filters.

- [Proclaim flow](2026-10-04-proclaim-flow.md) — accepted: the Proclaim window closes when nothing is left to react with; the first Personality bought each turn defaults to Proclaim, later ones to the Clan discount, a missed Honor Requirement to allegiance.

- [Playtest fixes (#644)](2026-10-05-playtest-fixes-644.md) — accepted: entry-reaction windows close when nothing is left to take, a face-up Celestial enters play at once, Steel on Steel needs a performer at the battlefield, the bot always uses its own entering-play Reactions.

- [Promotional-Emperor pass (#653)](2026-10-05-promotional-emperor-pass.md) — accepted as readings: 106 of 134 unwired Promotional-Emperor cards wired, 28 blocked with notes; the rules readings and open questions for Mendon are listed.

- [Rejected cards (#667)](2026-10-05-rejected-cards.md) — accepted as readings: Peace Treaty, Wildfire and Green Nightshade Blend are deliberately not wired (Mendon).

- [Fusako, Gaijin Sorcerer, Sleep (#667)](2026-10-05-667-duel-sleep.md) — accepted as readings: bow-to-refuse duel, weighted Shugenja count, budgeted multi-select.

- [Mempo and the Favor window (#667)](2026-10-05-667-favor.md) — accepted as readings: after-Favor-discard reaction window, +1 Favor use, any-unit Favor Battle.

- [Coin Pouch, Near Miss, Profits (#667)](2026-10-05-667-payments.md) — accepted as readings: extra payment sources in the Gold flow.

- [Blade, Yumi, Winter Sled, Wyrmbone, Lying in Ambush (#667)](2026-10-05-667-performers.md) — accepted as readings: hand-played Items, unnegatable movement, created Ambusher.

- [The Deciding Moment and phase order (#667)](2026-10-05-667-phase-order.md) — accepted as readings: per-turn phase-order marker, allowed swaps, header animation.

- [Jade Embrace, Sea & Sky, Through the Breach, Colonial Farm (#667)](2026-10-05-667-removal.md) — accepted as readings: per-card ability/trait removal that resets on leaving play.

- [Gozu, Hyobuko, Masaya, Rosary, Topaz Armor (#667)](2026-10-05-667-search-play.md) — accepted as readings: owed play after a search, Celestial into play, keyword pair.
- [Remaining unwired cards and the minimal primitive set (#668)](2026-10-05-remaining-unwired-primitives.md) — proposed: 491 unwired cards triaged against the end-of-arc supplement and the engine; 206 wirable today, 25 ranked primitives, 18 rulings, with a [per-card appendix](2026-10-05-remaining-unwired-primitives-cards.md) and [one implementation prompt per primitive](2026-10-05-remaining-unwired-primitives-prompts.md). No card wired.
- [First three primitives (#672)](2026-10-06-672-p9-stat-terms.md) — accepted as readings: the stat-term registry with army totals and contribution rules (P9, partial), [Tireless and Home as ability flags](2026-10-06-672-p16-tireless-home.md) (P16, partial) and [ExperiencedCoM and Inexperienced parsing](2026-10-06-672-p23-experienced-variants.md) (P23); 16 cards wired; each doc lists readings for Mendon to confirm.

- [Holdings wave 53: nineteen Holdings of Torn Asunder, Coils of Madness, Gates of Chaos and Aftermath (#674)](2026-10-06-674-holdings.md) — proposed readings built in for the #674 WIRE-list wave (holdings); Mendon to confirm.

- [Issue #674, cluster "items-misc": 21 Items, Followers, Events and Celestials wired, one blocked](2026-10-06-674-items-misc.md) — proposed readings built in for the #674 WIRE-list wave (items-misc); Mendon to confirm.

- [Wire-list cluster "pers-aftermath" (#674, personality-wave-132)](2026-10-06-674-pers-aftermath.md) — proposed readings built in for the #674 WIRE-list wave (pers-aftermath); Mendon to confirm.

- [pers-coils cluster of the #668 WIRE cards (issue #674)](2026-10-06-674-pers-coils.md) — proposed readings built in for the #674 WIRE-list wave (pers-coils); Mendon to confirm.

- [674 pers-gates-1 (personality-wave-133): fourteen Gates of Chaos Personalities](2026-10-06-674-pers-gates-1.md) — proposed readings built in for the #674 WIRE-list wave (pers-gates-1); Mendon to confirm.

- [Issue #674, cluster "pers-gates-2": thirteen Gates of Chaos Personalities (personality-wave-134)](2026-10-06-674-pers-gates-2.md) — proposed readings built in for the #674 WIRE-list wave (pers-gates-2); Mendon to confirm.

- [Issue #674, cluster "pers-torn": twenty-one Torn Asunder Personalities (personality-wave-131)](2026-10-06-674-pers-torn.md) — proposed readings built in for the #674 WIRE-list wave (pers-torn); Mendon to confirm.

- [674, the "spells-rings" cluster (spell-wave-55, ring-wave-3)](2026-10-06-674-spells-rings.md) — proposed readings built in for the #674 WIRE-list wave (spells-rings); Mendon to confirm.

- [Issue #674, cluster strat-aftermath-coils: Aftermath and Coils of Madness Strategies (strategy-wave-117)](2026-10-06-674-strat-aftermath-coils.md) — proposed readings built in for the #674 WIRE-list wave (strat-aftermath-coils); Mendon to confirm.

- [674 strat-gates: eighteen Gates of Chaos Strategies (strategy-wave-119)](2026-10-06-674-strat-gates.md) — proposed readings built in for the #674 WIRE-list wave (strat-gates); Mendon to confirm.

- [Cluster strat-torn (issue #674): nineteen Torn Asunder Strategies, strategy-wave-118](2026-10-06-674-strat-torn.md) — proposed readings built in for the #674 WIRE-list wave (strat-torn); Mendon to confirm.

- [Created-card proxies in the Ivory frame, and the creators they unblock (issue #677)](2026-10-06-677-created-card-proxies.md) — Mendon's ruling of 2026-10-06 (the Ivory frame is accepted; an exact Oracle proxy is preferred to a manufactured scan), seventeen proxies published, eighteen creators wired, five left with a precise gap; readings for Mendon to confirm.
- [Engine for P24, P4 and P22 (#690)](2026-10-06-690-p24-effect-primitives.md) — accepted as readings, engine only: [small effect primitives](2026-10-06-690-p24-effect-primitives.md) (switch, home-targeted and printed-Force Attacks, cost substitution, three-stat tokens), the [turn-begins hook, owner-turn durations and player-level grants](2026-10-06-690-p4-turn-boundary.md), and [created-card removal and current values, exchange, overlay onto a target, control-changing transfer, filter grants and traits](2026-10-06-690-p22-created-copies-grants.md); each doc gives the API for card wiring and the readings for Mendon.
- [Engine for P1, P3 and P10 (#695)](2026-10-07-695-p1-payment-hooks.md) — accepted as readings, engine only: [payment hooks](2026-10-07-695-p1-payment-hooks.md) (tokens that bow or depend on other sources, production conditions and adjustments, lent producers, an after-payment hook and ledger), [recruitment gates](2026-10-07-695-p3-recruit-gates.md) (refuse, waive Honor Requirement, extra-Clan control and Proclaim) and the [keyword-rule registry](2026-10-07-695-p10-keyword-rules.md) (conditional keywords by scope, gain blocks, element replacement, permanent Clan alignment); each doc gives the API for card wiring and the readings for Mendon.
- [Issue #699: Benediction (strategy-wave-120)](2026-10-06-699-benediction.md) — proposed readings for the #699 follow-up (benediction); Mendon's rulings applied, remaining readings to confirm.
- [699, the "essence" follow-up: Read the Essence (spell-wave-56)](2026-10-06-699-essence.md) — proposed readings for the #699 follow-up (essence); Mendon's rulings applied, remaining readings to confirm.
- [699, the "rings" cluster: how a Ring enters play (spell-wave-57, celestial-wave-29)](2026-10-06-699-rings.md) — proposed readings for the #699 follow-up (rings); Mendon's rulings applied, remaining readings to confirm.
- [Battle placards](2026-10-07-battle-placards.md) — replaces #710's court rails: province placards, left-edge stack, katana when the province falls
- [Direct play from the hand](2026-10-07-direct-play-from-hand.md) — select a playable card, confirm first, assume the single legal action, clickable action text when several
- [P13: phase and battle rule flags (#717)](2026-10-07-p13-rule-flags.md) — built, engine only, no card wired; the flags a later card wave sets and the choke points that read them.
- [P19: rulebook-action modifiers (#717)](2026-10-07-p19-rulebook-modifiers.md) — built, engine only, no card wired; the RulebookModifier hook and card-free timing grants.

- [P25: decision infrastructure (#723)](2026-10-07-p25-decision-infrastructure.md) — built, engine only, no card wired; multi-pick Either/Or, the PendingActions hook with generic client rendering of unknown pending types, slot cross-constraints and a shown-card choice.

- [P20: search and look extensions (#728)](2026-10-07-p20-search-look-extensions.md) — built, engine only, no card wired; province and any-player search sources, multi-select, new criteria and dispositions, other-player-chooses and reveal-until looks.

- [Wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) — proposed: the 182 wirable unwired cards in ten batches (issues #732 to #741, umbrella #742); 13 cards wait on rulings.

- [Wave batch 1: payment and cost cards (#732)](2026-10-08-wave-batch-1-payment-and-cost.md) — proposed readings: 15 cards wired on P1/P2/P3/P12 (Chiyurei's Axe and Duty Over All Things deferred); the art-pack candidate is unpublished.
- [Wave batch 3: stat-term and Experienced cards (#734)](2026-10-08-wave-batch-3-stat-terms.md) — proposed readings: 19 cards wired on P9/P23 with P3/P16/P18/P25 (Kachiko, Kokujin and Warded Premises deferred); art pack `catalogue-art-issue734-20261007-r1`.
- [Wave batch 2: recruit gates, Proclaim and Honor (#733)](2026-10-08-wave-batch-2-recruit-gates-and-honor.md) — proposed readings: 12 cards wired on P3/P14/P18/P10/P2; Zansho, Merchant Patronage, The Heavens' Blessing and Marriage of the Emerald Champion deferred.

- [Wave batch 5: card restrictions and immunities (#736)](2026-10-08-wave-batch-5-card-restrictions-and-immunities.md) — proposed readings: 14 cards wired on P11/P12; seven deferred (A Cleansing Breath needs a ruling).

- [Wave batch 6: after-event windows and period watches (#737)](2026-10-08-wave-batch-6-after-event-windows-and-watches.md) — proposed readings: 15 cards wired on P6/P7/P8 (with P4's draw rule); Koan's Robes and Imperial Deployment deferred.

- [Wave batch 7: turn boundaries and rule flags (#738)](2026-10-08-wave-batch-7-turn-boundaries-and-rule-flags.md) — proposed readings: 16 cards wired on P4/P13/P6 (the scans print no P5 clause, so the Events are ordinary); Revelations and The Onyx Championship deferred.

- [Wave batch 8: zones, search and decisions (#739)](2026-10-08-wave-batch-8-zones-search-and-decisions.md) — proposed readings: all 17 cards wired on P20/P21/P22/P25 (one new search disposition, RefillProvince); nothing deferred.
- [Foothold of the Mad (#766)](2026-10-08-foothold-of-the-mad.md) — Mendon's ruling: counts as 39 Honor for going first, then 0, silently; Madness tokens are -1F tokens while it is in play; wired with its Honor-loss immunity, Fallen discount and two Limited abilities
- [Force cap primitive and Asako Kaitoko - exp (#768)](2026-10-08-force-cap-and-kaitoko.md) — a live maximum-Force rule (lowers, never raises, lapses with its source, negated by Force-penalty negations and immunities) and the card that uses it, with its keyword-conditioned target bar
- [Bayushi Kachiko (#778)](2026-10-09-bayushi-kachiko.md) — Mendon's broad reading of "attacks": the first assignment to, or move into, an attacking army costs the target's controller 2 Honor; wired with her -1F term

- [Practice bot battle policy](2026-10-09-bot-battle-policy.md) — accepted (owner spec): pass when the battle is decided, attack/bow target priorities, send-home rule, first-action reactions and the Border Keep refresh.
- [Kalani's Landing (#816)](2026-10-10-kalanis-landing.md) — Mendon's ruling (R-041): a card in play with a non-zero Gold Cost is targeted after any announced action and gets -2 Gold Cost until end of turn, once per turn; wired with its attachment-production rule and the new "after each phase begins" channel.
- [Six cards Mendon's rulings unblocked (#821)](2026-10-10-six-ruled-cards.md) — rulings R-042 to R-048: the Governor's Estate creates an Imperial Estate Holding, Hida Kaiji and Isawa Ryuzo (same-side army assignment rules), Chuda Kaito's +3 Gold Cost, Ikoma Satoru and the shared gift step (also Sadahako's Artistry), Isawa Tsumaro's Home Engage and Illusion grant.
- [Kokujin, P'an Ku, Zansho, Chiyurei's Axe and the Koan cards (#827)](2026-10-11-koan-panku-kokujin.md) — rulings R-049 to R-053: Kokujin's sacrifice question after a won duel, P'an Ku's remain-bowed control link and seppuku bar, Zansho's own Follower, the Axe's automatic lower-Force comparison, the Koan Spell limit/bow waiver/Robes unit protection

- [Farm, Wedding, Koku and eight more (#829)](2026-10-11-farm-wedding-koku-batch.md) — accepted: eleven cards wired on rulings R-054 to R-062; ten readings for Mendon are listed.
- [Twelve cards on Mendon's rulings (#835)](2026-10-11-twelve-ruled-cards.md) — accepted: rulings R-067 to R-079: the Egg of P'an Ku, Blood of Isawa's Tribe, A Cleansing Breath, Duty Over All Things, Imperial Deployment, Merchant Patronage, Pierce the Tapestry, The Heavens' Blessing, Unbearable Weight, Warded Premises, Revelations and The Empire's Foe Exposed.

- [A bowed Personality may use his unbowed attachments' abilities](2026-10-12-bowed-host-attachment-abilities.md) — accepted ruling R-080, from the Comprehensive Rules clarification of 2013-06-05.

- [No deck counts; generic hidden-card refusals](2026-10-13-no-deck-counts.md) — current Dynasty/Fate deck sizes are hidden from both seats (protocol 4); a request naming a hidden card gets one fixed error.

- [Action resolution area (#917)](2026-10-13-action-resolution-area.md) — accepted and built (Phase 1, Strategies and Rings): a played card leaves the hand for Zone.Resolving at announcement, public to both seats, shown in MatchView.ResolutionArea; the Attack/Duel stopgap stays.

- [Practice difficulty](2026-11-11-practice-difficulty.md) — Easy is the Champion, Moderate the Contender, Hard Conan in its best-measured state; Hard decides off the UI thread on a copy of the match.
