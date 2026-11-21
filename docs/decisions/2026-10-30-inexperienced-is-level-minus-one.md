# Inexperienced is Experience level -1

Issue: #1150. Lane: rules (mdornbrook).

## Decision

The Comprehensive Rules (Glossary, Experienced/Inexperienced) say "The keyword Inexperienced refers to an experience level of -1", and a card with no Experienced keyword is level 0. The engine now follows that: `Experienced.Level` returns -1 for a card whose only version keyword is Inexperienced.

Before, P23 (#672) treated Inexperienced as level 0, so a version with no keyword could not overlay it and the two could not be told apart by level. Now a Personality with no experience keyword (level 0) may overlay the Inexperienced one (-1), an Experienced one may overlay either, and the Inexperienced one overlays nothing. The overlay decision of 2026-09-16 is otherwise unchanged.

## Also

An additional Battle action granted inside an entering-play window (Bayushi Wateru's Interrupt) goes back to the player who recruited, and a Terrain played as that action spends the grant, like an Equip does. Found while building the Keywords 2 tutorial.
