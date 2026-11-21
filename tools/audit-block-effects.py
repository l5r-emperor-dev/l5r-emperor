#!/usr/bin/env python3
"""Audit every catalogue card's text for effects that BLOCK a card (or player) from doing something -- will not move, will not straighten, may not perform actions, negate ... --
and write docs/rules/BLOCK_EFFECTS_CATALOGUE.md. The text scan is a regex pass over content/oracle/emperor-catalogue.json (the printed oracle text), so it lists candidates for
review; the engine-mechanism column is a hand-kept map (MECHANISM below). Run: PYTHONUTF8=1 python tools/audit-block-effects.py"""
import json, re, collections, pathlib, datetime

ROOT = pathlib.Path(__file__).resolve().parent.parent
data = json.load(open(ROOT / 'content/oracle/emperor-catalogue.json', encoding='utf-8'))
cards = data if isinstance(data, list) else data.get('cards', data)
cards = list(cards.values()) if isinstance(cards, dict) else cards

NOT = r"(?:will not|won't|may not|can ?not|can't|does not|do not|is not able to|are not able to|unable to)"
# (id, title, regex over one sentence)
CATS = [
    ("move", "Cannot move or be moved",
     NOT + r"[^.]{0,30}\b(?:be )?(?:move|moved|moving)\b|negate[^.]{0,40}\b(?:movement|moving)\b|negate[^.]{0,25}\bmove\b"),
    ("assign", "Cannot be assigned / cannot attack or defend",
     NOT + r"[^.]{0,30}\b(?:assign|assigned|attack|defend|engage)\b|negate[^.]{0,30}\bassign"),
    ("straighten", "Will not straighten",
     NOT + r"[^.]{0,40}\bstraighten|negate[^.]{0,50}\bstraighten"),
    ("act", "Cannot perform actions / use abilities",
     NOT + r"[^.]{0,30}\b(?:perform|use|take|play|activate|trigger)\b[^.]{0,30}\b(?:actions?|abilit|reactions?|interrupts?|strateg)|abilities[^.]{0,30}(?:may not|can ?not|can't|will not) be (?:used|activated)|negate[^.]{0,30}abilit"),
    ("target", "Cannot be targeted / chosen",
     NOT + r"[^.]{0,20}\b(?:be )?(?:target|targeted|chosen)\b|negate[^.]{0,30}\btarget"),
    ("contribute", "Does not contribute Force / count in the army",
     NOT + r"[^.]{0,30}\bcontribut|prevent[^.]{0,30}\bcontribut"),
    ("protect", "Will not bow / be destroyed / dishonored / discarded (protection)",
     NOT + r"[^.]{0,30}\b(?:bow|bowed|destroyed|dishonored|discarded|removed from|sent home)\b|negate[^.]{0,40}\b(?:destruction|bowing|dishonor|discard)"),
    ("honor", "Honor or Gold cannot change",
     NOT + r"[^.]{0,30}\b(?:gain|lose|pay|produce)\b[^.]{0,20}\b(?:honor|gold)\b|negate[^.]{0,30}\b(?:honor|gold)\b"),
    ("enter", "Cannot enter play / be recruited / attached / proclaimed",
     NOT + r"[^.]{0,30}\b(?:enter play|recruit|recruited|attach|attached|proclaim|be played|be put into play|be revealed)\b"),
    ("stats", "Force / keyword / stat changes blocked",
     NOT + r"[^.]{0,30}\b(?:gain|lose|have|receive|get|be given)\b[^.]{0,25}\b(?:keyword|force|chi|bonus|penalt|token)|negate[^.]{0,40}\b(?:force|penalt|bonus|token|keyword)\b"),
    ("react", "Reactions / windows closed",
     NOT + r"[^.]{0,30}\b(?:react|reaction|interrupt)\b|negate[^.]{0,30}\breaction"),
    ("duel", "Duel / challenge restrictions",
     NOT + r"[^.]{0,30}\b(?:duel|challenge|challenged|refuse)\b"),
    ("cards", "Draw / search / hand / discard-pile restrictions",
     NOT + r"[^.]{0,25}\b(?:draw|search|look at|discard|hand|deck)\b"),
    ("negate", "Other \"negate\" effects (effects of an action, general negation)", r"\bnegate\b"),
]

# How an effect of that kind is held by the engine, and what the client is told about it today (CardView). Hand-kept: update with the engine.
MECHANISM = {
    "move": ("`CardInstance.MovementLocks` (`MovementLock`: kinds Home / ToBattlefield / Assign / AssignAttack, one-shot `NextOnly`, battlefield-conditional); `R1.MovementNegated` (per battlefield); `P4Watch` snow shields; `KitsuneNegatedBattles`",
             "Not in `CardView`. Only a `movementLocked` / `moveNegated` log line when it happens."),
    "assign": ("`MovementLock` kinds Assign / AssignAttack; `AssignmentWatches`; per-card traits", "Not in `CardView`; the move is simply not offered."),
    "straighten": ("`CardInstance.StraightenBlockedTurn`; `NextStraightenNegated`; `NegatedEffects`", "`CardView.StraightenBlocked` (inspector line \"Straightening is prevented for this turn\"; no mark on the card)."),
    "act": ("`CardInstance.ActionLocks` (`ActionLock`); `BattleAbilityLocks`; `ReactionLockouts`", "`CardView.ActionLocked` (a seal on the Personality tile, `ActionLockStatus`); the other two are not exposed."),
    "target": ("`BattleTargetShields`, `FollowerShields`, `TargetBars` / `TargetBarRule`, `TargetShieldTurn`", "Not in `CardView`."),
    "contribute": ("`ContributionNegatedBy`, `ContributionShields`, Deep Roots (`ContributionNegatedTurn`)", "Not in `CardView` (the unit Force just changes)."),
    "protect": ("`CombatShields`, `BakishiShields`, `RestraintShields`, `ResolutionWillNotBow*`, `NegatedEffects`, `Protections`", "`GrantedEffects` for a few (\"Action protection\"); otherwise not in `CardView`."),
    "honor": ("`NegatedHonorGains`, `NegatedEffects`", "Not in `CardView`."),
    "enter": ("`ProclaimLocks`, `SeppukuBars`, `PromoItemsYariBars`, `HoteiLocks`, `KeywordGainBlock` (`Blocks`)", "Not in `CardView`."),
    "stats": ("`ForcePenaltiesNegatedTurn`, `ForceModifiersNegatedTurn`, `KeywordGainBlock`", "Not in `CardView` (modifiers are listed, the block on them is not)."),
    "react": ("`ReactionLockouts`, `NegationWatches`, `NextPerformedActionNegations`", "Not in `CardView`."),
    "duel": ("per-card duel traits", "Not in `CardView`."),
    "cards": ("per-card / per-player traits", "Not in `CardView`."),
    "negate": ("`NegatedEffects` (`NegatedEffect(Kind, CardId, ByPlayer)`), `NegationWatches`, `NextPerformedActionNegations`, `PendingOutOfPlayNegations`", "Only an `effectsNegated` log line when it happens."),
}

# When does it stop? Comprehensive Rules, Duration of effects: an ONGOING effect ("may not assign", a bonus, a keyword) lasts until the end of the turn it was applied unless the text says
# otherwise ("permanent", "while ..."); a CONTINUOUS trait (printed, no colon) is always on while its card is in play; a Terrain's ongoing effects last while the Terrain stays in play.
DURATIONS = [
    ("Next time (one-shot)", r"next time|the next|first time|once"),
    ("This battle", r"this battle|until the battle|during the battle|until the end of the battle|this combat"),
    ("While (a condition)", r"while|as long as"),
    ("Permanent", r"permanent"),
    ("Until end of turn (stated)", r"this turn|until the turn ends|until the end of the turn|for the rest of the turn|until end of turn|rest of this turn|each turn|until the end of your"),
    ("Until (an event)", r"until"),
]
TEMPORARY = ("Until end of turn (stated)", "Until end of turn (default rule)", "This battle", "Next time (one-shot)", "Until (an event)")


def abilities(text):
    """(ability line, [its sentences]): each printed ability is one line of the oracle text; a sentence is split at a full stop, never at the colon that ends the action's cost."""
    out = []
    for line in text.splitlines():
        if line.strip():
            out.append((line.strip(), [s.strip() for s in re.split(r'(?<=[.!?])\s+(?=[A-Z])', line.strip()) if s.strip()]))
    return out


def duration(line, terrain):
    for name, rx in DURATIONS:
        if re.search(rx, line, re.I):
            return name
    if terrain:
        return "While the Terrain is in play"
    if ':' in line:
        return "Until end of turn (default rule)"
    return "Persistent (continuous trait)"


def is_instant(sentence):
    """A Reaction that makes one action's effects not happen leaves no state on a card."""
    if re.search(r"effects? (?:do|does|will) not happen|negate (?:its|their|that|the) (?:effects|resolution)", sentence, re.I):
        return True
    return bool(re.match(r"^[^:]{0,40}(?:Reaction|Interrupt)", sentence)) and 'negate' in sentence.lower() and bool(re.search(r"from (?:that|the|an|another player's|the other player's)? ?action|action's effects|before .{0,40}resolves", sentence, re.I)) and not re.search(r"this turn|until|while|future|next", sentence, re.I)


rows = collections.defaultdict(list)
for c in cards:
    text = c.get('current_oracle_text') or ''
    if not text:
        continue
    typ = (c.get('types') or ['?'])[0]
    clan = '/'.join(c.get('clans') or []) or '-'
    terrain = 'Terrain' in (c.get('keywords') or [])
    seen = set()
    for line, sents in abilities(text):
        for s in sents:
            for cid, title, rx in CATS:
                if cid == 'negate' and seen:
                    continue
                if cid in seen:
                    continue
                if re.search(rx, s, re.I):
                    seen.add(cid)
                    # Duration and "instant" are read from the whole printed ability (its cost, its other sentences), not just the matching sentence.
                    shown = s if line.startswith(s) or ':' not in line.split(s)[0] else line
                    rows[cid].append((c['name'], typ, clan, line if len(line) <= 300 else s, duration(line, terrain), is_instant(line), c['id']))


def esc(s):
    return s.replace('|', '\\|').replace('\n', ' ')


out = []
out.append("# Catalogue of block effects\n")
out.append(f"Generated by `tools/audit-block-effects.py` on {datetime.date.today().isoformat()} from the printed oracle text of {len(cards)} catalogue cards (`content/oracle/emperor-catalogue.json`). "
           "It is a text scan, so every row is a **candidate** to review, not a verified engine behavior; a card with more than one kind of block is listed under each. "
           "Engine state referred to below: `src/L5R.Core/Models.cs`, `MovementLocks.cs`, `EmperorRulingR1.cs`.\n")
out.append("A **block** is an effect that stops a card or player from doing something: not moving, not straightening, not performing actions, not being targeted, not contributing Force, not bowing or being destroyed, and so on. "
           "The **Duration** column is read from the same sentence. *Until end of turn*, *This battle*, *Next time* and *Until* are the temporary blocks a player cannot see on the board today; "
           "*While* and *Persistent* are tied to a card or rule that is itself on the table. *Instant* marks a Reaction that cancels one action as it resolves and leaves no state on any card.\n")
out.append("## Summary\n")
out.append("| Kind | Cards | Temporary (turn / battle / next / until) | Persistent or conditional | Instant cancel |\n|---|---:|---:|---:|---:|")
for cid, title, _ in CATS:
    r = rows[cid]
    temp = sum(1 for x in r if x[4] in TEMPORARY and not x[5])
    pers = sum(1 for x in r if x[4] not in TEMPORARY and not x[5])
    inst = sum(1 for x in r if x[5])
    out.append(f"| [{title}](#{cid}) | {len(r)} | {temp} | {pers} | {inst} |")
out.append("\n## What the engine holds, and what the client shows\n")
out.append("| Kind | Engine state | Visible to the player today |\n|---|---|---|")
for cid, title, _ in CATS:
    m, v = MECHANISM[cid]
    out.append(f"| {title} | {m} | {v} |")
out.append("\nOnly two of these reach `CardView` (`StraightenBlocked` and `ActionLocked`). Every movement lock, target shield, contribution shield, protection, Force-change block and reaction lockout lives in `MatchState` and is invisible to a client that reads only `CardView`; "
           "the player learns of it from a log line after the fact, if at all. An overlay therefore needs the engine to publish, per card, the list of active blocks (kind, source card, duration, who set it).\n")
for cid, title, _ in CATS:
    r = sorted(rows[cid], key=lambda x: (x[4], x[0]))
    out.append(f"<a id=\"{cid}\"></a>\n## {title}\n")
    out.append(f"{len(r)} cards. Engine: {MECHANISM[cid][0]}.\n")
    out.append("| Card | Type | Clan | Duration | Printed text (the matching sentence) |\n|---|---|---|---|---|")
    for name, typ, clan, s, dur, inst, _id in r:
        out.append(f"| {esc(name)} | {typ} | {esc(clan)} | {dur}{' · instant' if inst else ''} | {esc(s[:260])} |")
    out.append("")
dest = ROOT / 'docs/rules/BLOCK_EFFECTS_CATALOGUE.md'
dest.write_text('\n'.join(out), encoding='utf-8', newline='\n')
print(dest, {cid: len(rows[cid]) for cid, _, _ in CATS})
