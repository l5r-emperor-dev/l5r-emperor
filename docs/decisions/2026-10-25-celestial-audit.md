# Celestial audit (issue #1114)

Date: 2026-10-25. Rules lead: Mendon (`mdornbrook`).

All 38 published Celestials were checked: printed text against the engine and tests (three read-only reviews), and a soak that put each Celestial into play in real custom matches (the shipped clan decks) and forced its actions with seeded random-legal play (5 matches each, 30 turns). Every action that came up was accepted by the engine. No engine exception, stall or resolution-area violation occurred.

## The report: Doji's Guidance's Open "isn't working"

No engine or client defect blocks it. In a real Crane Dominance match the Open was offered, used and accepted whenever its condition held, and the native client used it end to end (click the Celestial, confirm, the Strategy reaches the hand). In that deck only The Courtesy of the Crane and Discovering a Conspiracy are non-Unique Strategies with a Political ability, and the Open is an Open (Action Phase, with priority). The rest of the time it was disabled with the generic reason "No legal target or performer.", shown only in a tooltip. It now says "Your Fate discard pile has no non-Unique Strategy with a Political ability.", in the sidebar above its button. The other Guidance discard-pile and hand targets got the same treatment.

## Rulings

- **Discard after the colon is an effect.** Jizo's Mercy and Chagatai's Wrath print "Discard this Celestial." after the colon. It is part of the effect, as for The Inevitable Grasp of Conquest and The Ways of Sensei Wu: a negated action leaves the Celestial in play. (R-087)
- **Lady Matsu's Rage after Stalwart.** If Stalwart negates the bow, no delayed destruction is set: the "after the next time it straightens" half is tied to the bow it made. (R-088)

## Fixed

- Ebisu's Honesty: "destroy your rightmost province" is a cost (it was applied in the effect), so it is refused with no province left; its "gain will not be negated" is honoured (Destructive Priorities is not offered on it). The same no-province refusal now covers Akuma's Power's leftmost province.
- Megumi's Guidance and Lady Matsu's Rage: "discard this card" before the colon is a declared cost (it was done in the effect).
- Benten's Blessing: entering play again forgets its earlier choice of Personalities.
- Suitengu's Blessing: entering play again forgets its earlier Naval grant.
- Daigotsu's Guidance discarding the opponent's Ryoshun's Guidance now destroys his Personalities left at 0 Chi.
- Hantei's Guidance: the rulebook Lobby and the Favor's actions are Political actions ("Political Limited", "Favor Political Limited/Battle"), so its Interrupt can negate them; every other Political check sees them the same way.

## Not changed

- An audit claim that Tales of Dishonor and Solid Defense lack the Political tag was wrong: both are tagged.
- Untested pieces the reviews listed (Hotei's movement negation, Blessings of the Shi-Tien Yen-Wang's destroy/move shields, Hachiman's assign-while-bowed through a real assignment, Grasp's immunity ending in a real battle) still work by code reading and are left for a later pass.
- Suitengu's Naval grant is keyed on the target's card id: a target that leaves play and returns keeps it. Not changed.
