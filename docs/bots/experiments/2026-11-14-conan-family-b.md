# Conan strategy campaign, step 3: family B (Cavalry forcing) reading the Read (issue #1206, from 2026-11-14)

Branch `claude/1206-conan-family-b`, from `main` after family E's PR (64fc249b). Blocks: Mac range from 10500 (shadow on seed 10500; the screen and its A/A on 10600). Family C's sequential test uses 10400-10403.

## Rule card (above the line)

Rule `readB`, opt-in, off by default. Fires at the seat's own Infantry and Cavalry declarations when it has at least one Cavalry unit at home that can attack (a unit is Cavalry when the Personality and every Follower on it carry the keyword, as the ledger reads it); a seat without Cavalry is left to the Contender. It rests on RD section 2 (attacker Infantry, defender Infantry, attacker Cavalry, defender Cavalry: the defender places its non-Cavalry units before it sees where the Cavalry goes, and at a battlefield with no defending unit it has no presence) and on AB Q5 (the Contender already holds 80% of its Cavalry for the Cavalry segment; Cavalry-segment attackers reach an undefended battlefield 57% of the time, and 32% of those arrivals do not take the Province).

```
C = my Cavalry units able to attack, I = my infantry able to attack (unbowed, free, 2 Force or more)
Infantry declaration:
  options  = nothing | the Contender's own infantry plan | a probe (the smallest infantry group exceeding the weakest Strength undefended) | all infantry at the weakest Province
  replies  = their infantry's placements against the option (nobody, everybody at each attacked Province, the dealt split: Threat.Defences); with nothing declared there is no battlefield to defend
  follow-ups(option, reply) = all C at the weakest uncovered Province | the sufficient part of C there (the rest home) | C split across uncovered Provinces, each group exceeding Strength alone | all C reinforcing each attacked battlefield | C home
  value(attack, defence)  = expected Provinces (the calibrated cell per battlefield) + 0.3 (kills - losses) - weight(posture) (exposure(home left, their survivors) - exposure(hold))
  each follow-up is valued under their Cavalry reply (their unbowed home Cavalry to whichever battlefield hurts most, or home): the minimum
  forced(option) = min over replies of max over follow-ups
  declare the option with the highest forced value; when none clears the hold by 0.05, declare nothing (the Cavalry may still go alone in its segment)
Cavalry declaration (the infantry already at battlefields is fixed, their placed defence is visible):
  the follow-up with the highest value against the defence as placed, under their Cavalry reply; groups sent to an uncovered Province exceed its Strength alone; the rest stays home
```

**Should move** (RULE_FAMILIES.md B): Province destroyed when Cavalry-segment attackers arrive undefended (AB Q5b, mirror 68%); Provinces per attack for a side with two or more Cavalry units (Q5c); Cavalry held for the Cavalry segment (Q5a, mirror 80%); and family C's Q2a (a Province lost after an all-in), since Cavalry the fork does not need stays home. **Predicted fire rate**: the fork is read at every declaration of a seat with Cavalry; six of the fourteen decks carry 11-14% Cavalry Personalities and one (Unicorn Maidens) is all Cavalry, so it fires in a minority of games and several times in those.

## Decision tests

`tests/L5R.AI.Tests/ForkTests.cs` (`--filter fork`, 6 cases): no Cavalry, no fork; the probe is declared when the Cavalry can punish the defence that answers it (my 6 probes, their 6 defends and 15 beats 10 and kills it, or the Province falls), and nothing is declared when the probe would die (their 12), the Cavalry going alone to the Province no infantry can defend; the sufficient Cavalry (the 5 alone exceeds 4; the 4 stays home against their straightened 9); the Cavalry reinforces a battlefield its infantry is losing when that razes; the Cavalry does not take an uncovered Province their own Cavalry can still reach. The fork lives in `src/L5R.AI/Read/Fork.cs` (`Fork.AtInfantry(read, prepared, posture)` and `Fork.AtCavalry(read, posture)` → `ForkPlan`), hooked in `Prepare`'s attack path after `readA` (nothing → pass when offered), with a shadow log (`BotContext.ForkLog`, arena section "challenger fork").

The decision set has one family B position (`B-G1T6-cavalry`, Mendon's Utaku Ryoko alone to a Strength 7 Province against two unbowed defenders, which the Read sees never razing); T2 sends two units elsewhere, the fork sends nobody; neither agrees with Mendon, whose reason is not in the game (RULE_FAMILIES.md B lists his Cavalry habits as open). On the whole set (`L5R_POS_ARMS="t2;readB:+read,readB"`) `readB` agrees on 9 of 50, as T2 does, changing only that position. `L5R_POS_READ=1` now prints the fork's reading at declarations.
