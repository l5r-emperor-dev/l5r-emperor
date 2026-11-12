#!/usr/bin/env python3
"""l5rlog.py - shared parser for L5R Emperor arena / practice logs.

Not a report by itself; every q*.py script imports it.  Running it directly
(`python3 l5rlog.py <logs-dir>...`) prints parser self-checks: games parsed,
how often the Personality count derived from the `home:` list equals the
`pers N` field, how often the units destroyed after a battle resolution match
the side the Force comparison says lost, and how well the pre-battle Force
estimate reproduces the engine's resolution line in battles with no actions.

What is parsed
  header line   decks, roles (C challenger / c champion / H human), winner,
                victory type, final turn
  TURN blocks   per-seat status (Honor, provinces, hand, gold capacity,
                holdings, pers, force, home list with bowed flags, hand list,
                Province row with strengths)
  commands      `T<n> <Phase> s<seat><role> verb[/abilityId] key=value...`
  events        the `·` lines, attached to the command they follow
  attacks       one Attack per turn in which the active seat declared one:
                Infantry-segment and Cavalry-segment assignments (the log
                names the phase, so the segment is exact), defence
                assignments (both segments), one Battle per battlefield that
                ever held a unit, with Battle-phase commands, the engine's
                resolution Forces, province destroyed, units destroyed.

Player types.  `<dir>` names decide who the seats are:
  logs-r<NN>   C = Conan,  c = Contender(vsConan)
  logs-ca<NN>  C and c = Contender(mirror)
  human logs   H = Human,  c = PracticeBot
Override with `dir=ChallengerLabel,ChampionLabel` on any command line.

Known approximations (also listed in the report)
  * Events name a clan ("Lion paid ..."), not a seat.  Seats are taken from
    the command the event follows, never from the clan word, so mirror
    matches are handled; a destroyed/moved unit is attributed by the names
    assigned to each side of the battle, and when both sides hold a card of
    that name at the battlefield it goes to the opponent of the acting seat
    (counted in Game.ambiguous).
  * Bowed state is known only at each TURN header (start of the Action
    phase).  Bowing during the Action phase before the attack is not seen.
  * Force of a unit before battle is estimated: header Force of the
    Personality plus Followers attached to it (tracked from "attached to"
    events; ambiguous when a side has two Personalities of one name) plus
    printed Force of Followers/Items equipped earlier in the same turn.
    Action-phase Force bonuses are included only when the ability text
    carries an explicit number ("+2 Force", "+4F", "−3 Force") or a token is
    gained (+1 assumed).  See selfcheck().
  * The Imperial Favor holder is tracked from "took/discarded the Imperial
    Favor" events and credited to the seat of the command they follow.
  * A card can grant an additional Attack Phase inside a turn (the defender
    attacks back at once).  Those are kept in Turn.attacks with extra=True;
    Turn.attack is always the active seat's own declared attack.
"""
import collections
import glob
import json
import os
import re
import sys

CATALOGUE = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'content', 'oracle', 'emperor-catalogue.json')
TOKEN_TYPES = {'Infantry Follower': 'Follower', 'Aviary Falcon': 'Follower',
               'Lion Ancestor': 'Personality', 'Ninja': 'Personality'}


class Catalogue:
    def __init__(self, path=CATALOGUE):
        self.by = {}
        try:
            cards = json.load(open(path))['cards']
        except OSError:
            cards = []
        for c in cards:
            self.by.setdefault(c['name'], c)
        for c in cards:  # titles ("Bayushi Nomen" for "Bayushi Nomen - exp2")
            for t in c.get('titles') or []:
                self.by.setdefault(t, c)

    def card(self, name):
        return self.by.get(name)

    def type(self, name):
        if name in TOKEN_TYPES:
            return TOKEN_TYPES[name]
        c = self.by.get(name)
        if not c or not c.get('types'):
            return None
        return c['types'][0]

    def kw(self, name, kw):
        c = self.by.get(name)
        return bool(c) and kw in (c.get('keywords') or [])

    def stat(self, name, key, default=0):
        c = self.by.get(name)
        if not c:
            return default
        v = (c.get('stats') or {}).get(key)
        if not v:
            return default
        try:
            return int(str(v[0]).replace('+', ''))
        except ValueError:
            return default

    def text(self, name):
        c = self.by.get(name)
        return (c or {}).get('current_oracle_text') or ''


CAT = Catalogue()

RE_HDR_ARENA = re.compile(
    r'^# (.*?) \(seat 0, (\w+)\) vs (.*?) \(seat 1, (\w+)\); seed (\d+); '
    r'winner (\S*) \((\w+)\) by (.*?)(?: on turn (\d+))?;')
RE_HDR_HUMAN = re.compile(
    r'^# human \(seat 0, (.*?)\) vs champion \(seat 1, (.*?)\); replay (\S+); .*?'
    r'winner (\d) by (.*?) on turn (\d+);')
RE_TURN = re.compile(r'^===== TURN (\d+) active s(\d)(\w) =====')
RE_STATUS = re.compile(
    r'^  s(\d)(\w) Honor (-?\d+) provinces (\d+) hand (\d+) gold (-?\d+) '
    r'holdings (\d+) pers (\d+) force (-?\d+) \| home: ?(.*)$')
RE_HAND = re.compile(r'^     hand: ?(.*?) ?\| row: ?(.*)$')
RE_CMD = re.compile(r'^T(\d+) (\w+) s(\d)(\w) (\S+)(?: (.*))?$')
RE_ARG = re.compile(r'(?:^| )(\w+)=')
RE_HOME = re.compile(r'^(.*?)(?:\((-?\d+)F\))?(\*)?$')
RE_PAID = re.compile(r'^(\w+) paid (\d+) Gold for (.*), producing (\d+); (\d+) excess was lost\.$')
RE_RESOLVE = re.compile(r'^Province (\d+): attacking Force (-?\d+), defending Force (-?\d+)\.$')
RE_BATTLE = re.compile(r'^Battle at province (\d+): ')
RE_PROVDEST = re.compile(r"^(\w+)'s province (\d+) was destroyed\.$")
RE_UNITDEST = re.compile(r'^(.*?) (?:and its attachments were destroyed|was destroyed and removed from the game)\.$')
RE_CARDDEST = re.compile(r'^(.*?) was destroyed\.$')
RE_HOME_MOVE = re.compile(r"^(.*?)'s unit moved home(?: \(a cost\))?\.$")
RE_MOVE_TO = re.compile(r"^(.*?)'s unit moved to the battlefield at province (\d+)\.$")
RE_ATTACH = re.compile(r'^(.*) attached to (.*)\.$')
RE_CREATE_ATTACH = re.compile(r'^\w+ created (.*) and attached it to (.*)\.$')
RE_STRENGTH = re.compile(r"province (\d+) has ([+-]\d+) strength this turn")
RE_EXTRA_ATTACK = re.compile(r'declared an additional Attack Phase')
RE_PLUS = re.compile(r'\+(\d+) ?F(?:orce)?\b')
RE_MINUS = re.compile(r'[−-](\d+) ?F(?:orce)?\b')
RE_TOKEN = re.compile(r'^(.*) gained a (?:Force|Fire) token\.$')


class Card:
    __slots__ = ('name', 'force', 'bowed', 'type')

    def __init__(self, name, force, bowed):
        self.name = name
        self.force = force
        self.bowed = bowed
        t = CAT.type(name)
        if t is None and force is not None:
            t = 'Follower'
        self.type = t

    def __repr__(self):
        return '%s%s%s' % (self.name, '' if self.force is None else '(%dF)' % self.force,
                           '*' if self.bowed else '')


class Status:
    """One seat's line at a TURN header."""

    def __init__(self):
        self.honor = self.provinces = self.hand_n = self.gold = 0
        self.holdings = self.pers_n = self.force = 0
        self.home = []
        self.hand = []
        self.row = []      # [(strength, name-or-'down')]

    @property
    def pers(self):
        return [c for c in self.home if c.type == 'Personality']

    @property
    def unbowed_pers(self):
        return [c for c in self.home if c.type == 'Personality' and not c.bowed]

    @property
    def unbowed_force(self):
        """Force of unbowed Personalities and unbowed Followers at home.
        (A Follower on a bowed Personality cannot defend; that case is counted
        anyway - approximation.)"""
        return sum(c.force or 0 for c in self.home
                   if c.force is not None and not c.bowed and c.type in ('Personality', 'Follower'))

    @property
    def unbowed_pers_force(self):
        return sum(c.force or 0 for c in self.unbowed_pers)


class Cmd:
    __slots__ = ('turn', 'phase', 'seat', 'role', 'verb', 'ability', 'args', 'events', 'alt', 'raw')

    def __init__(self, turn, phase, seat, role, verbfull, argstr, raw):
        self.turn = turn
        self.phase = phase
        self.seat = seat
        self.role = role
        if '/' in verbfull:
            self.verb, self.ability = verbfull.split('/', 1)
        else:
            self.verb, self.ability = verbfull, None
        self.args = parse_args(argstr or '')
        self.events = []
        self.alt = None
        self.raw = raw

    def text(self):
        """The engine's description of the ability: 'Clan: Card — text'."""
        for e in self.events:
            if ' — ' in e and re.match(r'^\w+: ', e):
                return e.split(' — ', 1)[1]
        return ''


def parse_args(s):
    out = {}
    ms = list(RE_ARG.finditer(s))
    for i, m in enumerate(ms):
        end = ms[i + 1].start() if i + 1 < len(ms) else len(s)
        out[m.group(1)] = s[m.end():end].strip()
    return out


def parse_assign(s):
    """'Ikoma Sido>1,Matsu Hachiro>2' -> [('Ikoma Sido', 2), ...] (1-based province number)."""
    out = []
    if not s:
        return out
    for part in re.split(r',(?=[^,]*>\d+(?:,|$))', s):
        m = re.match(r'^(.*)>(\d+)$', part.strip())
        if m:
            out.append((m.group(1), int(m.group(2)) + 1))
    return out


def split_names(s):
    if not s.strip():
        return []
    parts = [p.strip() for p in s.split(', ')]
    out = []
    i = 0
    while i < len(parts):
        if i + 1 < len(parts) and CAT.card(parts[i]) is None and CAT.card(parts[i] + ', ' + parts[i + 1]):
            out.append(parts[i] + ', ' + parts[i + 1])
            i += 2
        else:
            out.append(parts[i])
            i += 1
    return out


class Battle:
    def __init__(self, prov):
        self.prov = prov
        self.cmds = []            # Battle-phase commands while this battlefield was current
        self.att_assigned = []    # Personality names assigned (both segments)
        self.def_assigned = []
        self.att_cav = []         # subset assigned in the Cavalry segment
        self.def_cav = []
        self.att_now = collections.Counter()   # membership, updated by move/destroy events
        self.def_now = collections.Counter()
        self.A = self.D = None    # resolution Forces
        self.destroyed = False    # province destroyed
        self.att_lost = []        # attacker Personalities destroyed (mid-battle + resolution)
        self.def_lost = []
        self.att_lost_res = 0
        self.def_lost_res = 0
        self.res_destroyed_names = []
        self.strength = None      # Province Strength printed in the row at the header
        self.strength_mod = 0     # sum of "+N strength this turn" events seen before resolution
        self.est_A = self.est_D = None   # pre-battle Force estimate
        self.est_uncertain = False
        self.removed = []         # (actor_seat, victim_side 'att'/'def', name, how, ability)
        self.resolved = False

    def real(self):
        return bool(self.att_assigned) or bool(self.def_assigned)


class Attack:
    def __init__(self, turn, attacker, extra=False):
        self.turn = turn
        self.attacker = attacker
        self.defender = 1 - attacker
        self.extra = extra  # an additional Attack Phase granted by a card (rare)
        self.inf = []       # [(name, prov)]
        self.cav = []
        self.dfn = []
        self.cavdef = []
        self.cav_option = False       # attacker was offered the Cavalry segment
        self.cavdef_option = False
        self.battles = {}             # prov -> Battle
        self.order = []
        self.favor = None             # seat holding the Imperial Favor when the attack was declared

    @property
    def assigned(self):
        return self.inf + self.cav

    @property
    def defenders(self):
        return self.dfn + self.cavdef

    @property
    def targets(self):
        return sorted(set(p for _, p in self.assigned))

    @property
    def destroyed(self):
        return [p for p, b in self.battles.items() if b.destroyed]

    @property
    def att_lost(self):
        return sum(len(b.att_lost) for b in self.battles.values())

    @property
    def def_lost(self):
        return sum(len(b.def_lost) for b in self.battles.values())


class Turn:
    def __init__(self, n, active):
        self.n = n
        self.active = active
        self.status = {}
        self.cmds = []
        self.stream = []       # ('c', Cmd) / ('e', text, Cmd-or-None)
        self.attack = None     # the active seat's own declared attack
        self.attacks = []      # that attack plus any additional Attack Phases this turn
        self.declared = False
        self.purchases = []    # (seat, card, type, paid, produced, excess)
        self.gold = {0: [0, 0, 0], 1: [0, 0, 0]}   # seat -> [paid, produced, excess]
        self.unit_losses = {0: [], 1: []}          # Personalities destroyed outside battles
        self.prov_alive = {}   # seat -> list of province numbers alive at header
        self.favor = None      # seat holding the Imperial Favor at the header (tracked from events)


class Game:
    def __init__(self, path):
        self.path = path
        self.name = os.path.basename(path)
        self.arm = os.path.basename(os.path.dirname(path))
        self.decks = [None, None]
        self.roles = [None, None]
        self.ptype = [None, None]
        self.winner = None
        self.victory = None
        self.end_turn = None
        self.turns = []
        self.clan = [None, None]
        self.ambiguous = 0
        self.human = False

    def turn(self, n):
        i = n - self.turns[0].n if self.turns else -1
        if 0 <= i < len(self.turns) and self.turns[i].n == n:
            return self.turns[i]
        for t in self.turns:
            if t.n == n:
                return t
        return None

    def seat_of(self, ptype):
        return [s for s in (0, 1) if self.ptype[s] == ptype]

    @property
    def vclass(self):
        v = self.victory or ''
        if v.startswith('Military'):
            return 'Military'
        if v.startswith('Honor'):
            return 'Honor'
        if v.startswith('Dishonor'):
            return 'Dishonor'
        return v


def default_labels(dirpath, human):
    base = os.path.basename(os.path.normpath(dirpath))
    if human:
        return {'H': 'Human', 'c': 'PracticeBot', 'C': 'PracticeBot'}
    if re.match(r'^logs-ca\d', base):
        return {'C': 'Contender(mirror)', 'c': 'Contender(mirror)'}
    return {'C': 'Conan', 'c': 'Contender(vsConan)'}


def parse_game(path, labels=None):
    g = Game(path)
    try:
        lines = open(path, encoding='utf-8').read().split('\n')
    except OSError:
        return None
    if not lines or not lines[0].startswith('#'):
        return None
    h = lines[0]
    m = RE_HDR_HUMAN.match(h)
    if m:
        g.human = True
        g.decks = [m.group(1), m.group(2)]
        g.roles = ['H', 'c']
        g.winner = int(m.group(4))
        g.victory = m.group(5)
        g.end_turn = int(m.group(6))
    else:
        m = RE_HDR_ARENA.match(h)
        if not m:
            return None
        g.decks = [m.group(1), m.group(3)]
        g.roles = ['C' if m.group(2) == 'challenger' else 'c',
                   'C' if m.group(4) == 'challenger' else 'c']
        if m.group(6) not in ('0', '1'):
            return None            # killed / unfinished game
        g.winner = int(m.group(6))
        g.victory = m.group(8)
        g.end_turn = int(m.group(9)) if m.group(9) else None
    lab = labels or default_labels(os.path.dirname(path), g.human)
    g.ptype = [lab.get(g.roles[0], g.roles[0]), lab.get(g.roles[1], g.roles[1])]

    cur = None
    last = None
    pre = []       # events seen before any command
    stat_seat = None
    for ln in lines[1:]:
        if ln.startswith('## '):
            break
        if ln.startswith('    · '):
            ev = ln[6:]
            if last is not None:
                last.events.append(ev)
                # events after a command of turn N stay in turn N's stream
                tt = g.turns[-1] if g.turns else None
                if tt is not None:
                    owner = tt if last.turn == tt.n else g.turn(last.turn) or tt
                    owner.stream.append(('e', ev, last))
            else:
                pre.append(ev)
            continue
        if ln.startswith('    >> '):
            if last is not None:
                last.alt = ln[7:]
            continue
        m = RE_CMD.match(ln)
        if m:
            c = Cmd(int(m.group(1)), m.group(2), int(m.group(3)), m.group(4), m.group(5), m.group(6), ln)
            if cur is None or cur.n != c.turn:
                t2 = g.turn(c.turn)
                if t2 is None:
                    t2 = Turn(c.turn, c.seat)
                    g.turns.append(t2)
                cur = t2
            cur.cmds.append(c)
            cur.stream.append(('c', c))
            last = c
            continue
        m = RE_TURN.match(ln)
        if m:
            cur = Turn(int(m.group(1)), int(m.group(2)))
            g.turns.append(cur)
            continue
        m = RE_STATUS.match(ln)
        if m and cur is not None:
            st = Status()
            stat_seat = int(m.group(1))
            st.honor, st.provinces, st.hand_n, st.gold = (int(m.group(i)) for i in (3, 4, 5, 6))
            st.holdings, st.pers_n, st.force = (int(m.group(i)) for i in (7, 8, 9))
            for e in split_names(m.group(10)):
                mm = RE_HOME.match(e)
                st.home.append(Card(mm.group(1), int(mm.group(2)) if mm.group(2) is not None else None,
                                    bool(mm.group(3))))
            cur.status[stat_seat] = st
            continue
        m = RE_HAND.match(ln)
        if m and cur is not None and stat_seat is not None:
            st = cur.status[stat_seat]
            st.hand = split_names(m.group(1))
            for e in split_names(m.group(2)):
                mm = re.match(r'^(-?\d+):(.*)$', e)
                if mm:
                    st.row.append((int(mm.group(1)), mm.group(2)))
            continue
    if not g.turns:
        return None
    _derive(g)
    return g


def _derive(g):
    # clan word per seat (first "<Clan> passed." after a pass command)
    for t in g.turns:
        for c in t.cmds:
            if g.clan[c.seat] is None and c.verb == 'pass' and c.events:
                m = re.match(r'^(\w+) passed\.$', c.events[0])
                if m:
                    g.clan[c.seat] = m.group(1)
        if g.clan[0] and g.clan[1]:
            break
    alive = {0: [1, 2, 3, 4], 1: [1, 2, 3, 4]}
    attach = {0: [], 1: []}     # (follower name, personality name)
    favor = [None]
    for t in g.turns:
        t.prov_alive = {0: list(alive[0]), 1: list(alive[1])}
        t.favor = favor[0]
        # re-sync tracked attachments with the header's Follower list
        for s in (0, 1):
            st = t.status.get(s)
            if st is None:
                continue
            have = collections.Counter(c.name for c in st.home if c.type == 'Follower')
            pers = set(c.name for c in st.home if c.type == 'Personality')
            keep = []
            for f, p in attach[s]:
                if have[f] > 0 and p in pers:
                    have[f] -= 1
                    keep.append((f, p))
            attach[s] = keep
        _derive_turn(g, t, alive, attach, favor)


def _owner_guess(name, att, bat, actor, cost=False):
    """Which side of the battle ('att'/'def') holds a Personality called name."""
    a = bat.att_now[name] > 0 if bat else False
    d = bat.def_now[name] > 0 if bat else False
    if a and not d:
        return 'att', False
    if d and not a:
        return 'def', False
    if a and d:
        # same name on both sides: the victim is the opponent of the actor (or the actor, for a cost)
        actor_side = 'att' if actor == att.attacker else 'def'
        other = 'def' if actor_side == 'att' else 'att'
        return (actor_side if cost else other), True
    return None, False


def _derive_turn(g, t, alive, attach, favor):
    A = t.active
    D = 1 - A
    att = None
    bat = None
    phase = None
    post = False
    equipped_now = {0: [], 1: []}   # (card, target) equipped this turn before battle
    pumps = {0: collections.Counter(), 1: collections.Counter()}   # explicit +N/-N Force seen before battle
    smod = collections.Counter()    # province number -> strength modifier "this turn"
    pers_names = {s: collections.Counter(c.name for c in t.status[s].pers) if s in t.status else collections.Counter()
                  for s in (0, 1)}
    hdr_attach = {0: list(attach[0]), 1: list(attach[1])}

    def battle_for(p):
        b = att.battles.get(p)
        if b is None:
            b = Battle(p)
            b.strength_mod = smod[p]
            att.battles[p] = b
            st = t.status.get(att.defender)
            if st is not None:
                al = t.prov_alive[att.defender]
                if p in al and al.index(p) < len(st.row):
                    b.strength = st.row[al.index(p)][0]
        return b

    for ent in t.stream:
        if ent[0] == 'c':
            c = ent[1]
            phase = c.phase
            if c.phase == 'AttackDeclaration' and c.verb == 'declareAttack':
                t.declared = True
                att = Attack(t.n, c.seat)
                att.favor = favor[0]
                t.attack = att
                t.attacks.append(att)
                A, D = att.attacker, att.defender
            elif att is not None and c.phase == 'InfantryDeclaration' and c.verb == 'attack':
                att.inf += parse_assign(c.args.get('assign', ''))
            elif att is not None and c.phase == 'CavalryDeclaration' and c.seat == A and c.verb in ('attack', 'pass'):
                att.cav_option = True
                if c.verb == 'attack':
                    att.cav += parse_assign(c.args.get('assign', ''))
            elif att is not None and c.phase == 'DefenseAssignment' and c.verb == 'assignDefense':
                att.dfn += parse_assign(c.args.get('assign', ''))
            elif att is not None and c.phase == 'CavalryDefense' and c.seat == D and c.verb in ('assignDefense', 'pass'):
                att.cavdef_option = True
                if c.verb == 'assignDefense':
                    att.cavdef += parse_assign(c.args.get('assign', ''))
            elif att is not None and c.phase == 'Battle' and bat is not None and not bat.resolved:
                bat.cmds.append(c)
            if c.verb == 'equip' and c.phase == 'Action':
                equipped_now[c.seat].append((c.args.get('card'), c.args.get('target')))
            continue
        ev, c = ent[1], ent[2]
        if 'the Imperial Favor' in ev and c is not None:
            if re.match(r'^\w+ took the Imperial Favor', ev):
                favor[0] = c.seat
                continue
            if re.match(r'^\w+ discarded the Imperial Favor', ev):
                favor[0] = None
                continue
        if c is not None and c.verb == 'useAbility' and c.phase != 'Battle' and ' — ' in ev and bat is None:
            txt = ev.split(' — ', 1)[1]
            tgt = c.args.get('target') or c.args.get('by') or c.args.get('card')
            mp = RE_PLUS.search(txt)
            mm_ = RE_MINUS.search(txt)
            if tgt and mp and not mm_:
                pumps[c.seat][tgt] += int(mp.group(1))
            elif tgt and mm_ and not mp:
                pumps[1 - c.seat][tgt] -= int(mm_.group(1))
        mt = RE_TOKEN.match(ev)
        if mt and c is not None and bat is None:
            pumps[c.seat][mt.group(1)] += 1
        if RE_EXTRA_ATTACK.search(ev) and c is not None:
            att = Attack(t.n, c.seat, extra=True)
            t.attacks.append(att)
            A, D = att.attacker, att.defender
            bat = None
            post = False
            continue
        if ev.startswith('Phase: '):
            ph = ev[7:].rstrip('.')
            if ph == 'BattleSelection' and att is not None and not att.battles:
                _init_battles(g, t, att, battle_for, pers_names, hdr_attach, equipped_now, pumps, smod)
            if bat is not None and bat.resolved:
                bat = None
            post = False
            phase = ph
            continue
        m = RE_PAID.match(ev)
        if m and c is not None:
            paid, prod, exc = int(m.group(2)), int(m.group(4)), int(m.group(5))
            gs = t.gold[c.seat]
            gs[0] += paid
            gs[1] += prod
            gs[2] += exc
            if c.verb == 'purchase':
                t.purchases.append((c.seat, m.group(3), CAT.type(m.group(3)), paid, prod, exc))
            continue
        m = RE_ATTACH.match(ev)
        if m and c is not None:
            mc = RE_CREATE_ATTACH.match(ev)
            if mc:
                attach[c.seat].append((mc.group(1), mc.group(2)))
            else:
                attach[c.seat].append((m.group(1), m.group(2)))
            continue
        m = RE_BATTLE.match(ev)
        if m and att is not None:
            if not att.battles:
                _init_battles(g, t, att, battle_for, pers_names, hdr_attach, equipped_now, pumps, smod)
            bat = battle_for(int(m.group(1)))
            att.order.append(bat.prov)
            post = False
            continue
        m = RE_RESOLVE.match(ev)
        if m and att is not None:
            b = battle_for(int(m.group(1)))
            bat = b
            b.A, b.D = int(m.group(2)), int(m.group(3))
            b.resolved = True
            post = True
            continue
        m = RE_PROVDEST.match(ev)
        if m:
            p = int(m.group(2))
            # the province belongs to the defender of the current attack (or the non-active seat)
            owner = att.defender if att is not None else 1 - t.active
            if p in alive[owner]:
                alive[owner].remove(p)
            if att is not None:
                battle_for(p).destroyed = True
            continue
        m = RE_STRENGTH.search(ev)
        if m:
            p = int(m.group(1))
            b = att.battles.get(p) if att is not None else None
            if b is not None:
                if not b.resolved:
                    b.strength_mod += int(m.group(2))
            else:
                smod[p] += int(m.group(2))
            continue
        m = RE_UNITDEST.match(ev)
        if m:
            name = m.group(1)
            name = re.sub(r' \(.*\)$', '', name)
            if att is not None and bat is not None and post:
                bat.res_destroyed_names.append(name)
                if bat.A > bat.D:
                    side = 'def'
                elif bat.D > bat.A:
                    side = 'att'
                else:
                    side, amb = _owner_guess(name, att, bat, A)
                    if side is None:
                        side = 'def'
                (bat.att_lost if side == 'att' else bat.def_lost).append(name)
                if side == 'att':
                    bat.att_lost_res += 1
                    bat.att_now[name] -= 1
                else:
                    bat.def_lost_res += 1
                    bat.def_now[name] -= 1
            elif att is not None and bat is not None and not bat.resolved:
                actor = c.seat if c is not None else A
                side, amb = _owner_guess(name, att, bat, actor)
                if amb:
                    g.ambiguous += 1
                if side is None:
                    # not at this battlefield: a unit elsewhere (home or another battlefield)
                    own = _seat_by_name(name, pers_names, actor)
                    t.unit_losses[own].append(name)
                    for ob in att.battles.values():
                        cnt = ob.att_now if own == A else ob.def_now
                        if cnt[name] > 0:
                            cnt[name] -= 1
                            (ob.att_lost if own == A else ob.def_lost).append(name)
                            t.unit_losses[own].pop()
                            break
                else:
                    (bat.att_lost if side == 'att' else bat.def_lost).append(name)
                    (bat.att_now if side == 'att' else bat.def_now)[name] -= 1
                    bat.removed.append((actor, side, name, 'destroy', c.ability if c else None))
            else:
                actor = c.seat if c is not None else t.active
                own = _seat_by_name(name, pers_names, actor)
                t.unit_losses[own].append(name)
            continue
        m = RE_HOME_MOVE.match(ev)
        if m and att is not None and bat is not None and not bat.resolved:
            name = m.group(1)
            actor = c.seat if c is not None else A
            cost = ev.endswith('(a cost).')
            side, amb = _owner_guess(name, att, bat, actor, cost)
            if amb:
                g.ambiguous += 1
            if side is not None:
                (bat.att_now if side == 'att' else bat.def_now)[name] -= 1
                actor_side = 'att' if actor == A else 'def'
                if side != actor_side:
                    bat.removed.append((actor, side, name, 'home', c.ability if c else None))
            continue
        m = RE_MOVE_TO.match(ev)
        if m and att is not None:
            name, p = m.group(1), int(m.group(2))
            actor = c.seat if c is not None else A
            own = _seat_by_name(name, pers_names, actor)
            b = battle_for(p)
            for ob in att.battles.values():
                cnt = ob.att_now if own == A else ob.def_now
                if ob is not b and cnt[name] > 0:
                    cnt[name] -= 1
                    break
            (b.att_now if own == A else b.def_now)[name] += 1
            continue
        mb = re.match(r'^(\w+) brought (.*?) into play', ev)
        if mb and c is not None:
            if CAT.type(mb.group(2)) == 'Personality':
                pers_names[c.seat][mb.group(2)] += 1
            continue
    if att is not None and not att.battles:
        _init_battles(g, t, att, battle_for, pers_names, hdr_attach, equipped_now, pumps, smod)


def _seat_by_name(name, pers_names, actor):
    a = pers_names[0][name] > 0
    b = pers_names[1][name] > 0
    if a and not b:
        return 0
    if b and not a:
        return 1
    return 1 - actor      # unknown or on both sides: assume an enemy effect


def _init_battles(g, t, att, battle_for, pers_names, hdr_attach, equipped_now, pumps=None, smod=None):
    """Called once assignments are complete: fill battle membership and Force estimates."""
    for (name, p) in att.inf:
        b = battle_for(p)
        b.att_assigned.append(name)
        b.att_now[name] += 1
    for (name, p) in att.cav:
        b = battle_for(p)
        b.att_assigned.append(name)
        b.att_cav.append(name)
        b.att_now[name] += 1
    for (name, p) in att.dfn:
        b = battle_for(p)
        b.def_assigned.append(name)
        b.def_now[name] += 1
    for (name, p) in att.cavdef:
        b = battle_for(p)
        b.def_assigned.append(name)
        b.def_cav.append(name)
        b.def_now[name] += 1
    for seat, key in ((att.attacker, 'att'), (att.defender, 'def')):
        st = t.status.get(seat)
        if st is None:
            continue
        allassigned = collections.Counter(n for n, _ in (att.assigned if key == 'att' else att.defenders))
        for b in att.battles.values():
            names = b.att_assigned if key == 'att' else b.def_assigned
            est, unc = estimate_force(st, names, allassigned, hdr_attach[seat], equipped_now[seat],
                                      pumps[seat] if pumps else None)
            if key == 'att':
                b.est_A = est
            else:
                b.est_D = est
            b.est_uncertain = b.est_uncertain or unc


def estimate_force(st, names, allassigned, attach, equipped, pumps=None):
    """Pre-battle Force of the Personalities `names` (one battlefield) for one seat.

    header Force of each Personality (unbowed copies first) + header Force of
    unbowed Followers tracked as attached to that name + printed Force of
    Followers / Force bonus of Items equipped this turn.  Returns (force,
    uncertain); uncertain when a name has more copies at home than were
    assigned in total (cannot tell which copy went) or a name is missing."""
    total = 0
    unc = False
    want = collections.Counter(names)
    fol_force = collections.defaultdict(list)
    for c in st.home:
        if c.type == 'Follower' and not c.bowed and c.force is not None:
            fol_force[c.name].append(c.force)
    for name, k in want.items():
        copies = sorted([c for c in st.home if c.type == 'Personality' and c.name == name],
                        key=lambda c: (c.bowed, -(c.force or 0)))
        if len(copies) < k:
            unc = True
        if len(copies) > allassigned[name] and len(set(c.force for c in copies)) > 1:
            unc = True
        for c in copies[:k]:
            total += c.force or 0
        # followers attached to this name
        fl = [f for f, p in attach if p == name]
        if fl and len(copies) > allassigned[name]:
            unc = True
        if fl and allassigned[name] > k:
            unc = True       # same name split over two battlefields
        for f in fl:
            if fol_force[f]:
                total += fol_force[f].pop()
        if pumps and pumps.get(name):
            total += pumps[name]
            if len(copies) > 1 or allassigned[name] > k:
                unc = True
        for card, target in equipped:
            if target == name and card:
                ty = CAT.type(card)
                if ty in ('Follower', 'Item'):
                    total += CAT.stat(card, 'force', 0)
    return total, unc


# --------------------------------------------------------------------------
# Battle-action classification
#
# Most Battle actions leave no result event in the log (a bow, a Force penalty
# and a ranged attack print only the ability line), so actions are classified
# from the ability id and the engine's description, not from what happened.
# Classes:
#   home     moves an enemy unit home (out of the battle)
#   bow      bows an enemy card (its Force stops counting)
#   destroy  destroys an enemy card: targeted destruction, Ranged/Melee
#            attacks, duels that destroy the loser, taking control
#   reduce   lowers an enemy card's Force
#   add      raises own Force: bonus, straighten, move own unit in, create or
#            recruit a unit at the battlefield
#   negate   negates / redirects / taxes an enemy action (mostly Reactions)
#   strength changes Province Strength
#   other    card draw, extra actions, first-action claims, Honor, equip
# home+bow+destroy+reduce are the "neutralising" actions of question 4.
# The automatic rules were reviewed against all 192 ability ids seen in the
# five log directories; ACTION_OVERRIDE holds the ones the rules got wrong.
ACTION_CLASSES = ['home', 'bow', 'destroy', 'reduce', 'add', 'negate', 'strength', 'other']
NEUTRALISING = ('home', 'bow', 'destroy', 'reduce')
ACTION_OVERRIDE = {
    'favor-battle': 'home', 'incapacitated': 'home', 'block-supply': 'home',
    'yasuki-palaces-move': 'home', 'tactical-trap-move': 'home', 'shamate-move': 'home',
    'fear-me-move': 'home', 'exploited-advantage-move': 'home', 'obscured-pathways-move': 'home',
    'effortless-counterattack-move': 'home', 'sheathing-sword-move': 'home',
    'dismissing-the-cur-move': 'home', 'request-authorization-move': 'home',
    'moving-unmoving-retreat': 'home', 'unnatural-flood-move': 'home', 'power-word-move': 'home',
    'paragon-strength-move': 'home', 'tsunami-move-home': 'home', 'makoto-lock': 'home',
    'my-enemys-mercy-choose': 'home', 'shigeyuki-duel': 'home', 'spirit-duelist-duel': 'home',
    'reckless-duel': 'home', 'yumaru-move-home': 'home',
    'aerie-duel': 'bow', 'mitsuru-duel': 'bow', 'surety-of-purpose': 'bow', 'bow-before-my-will': 'bow',
    'unorthodox-attack': 'bow', 'eun-ju-bow': 'bow', 'unfortunate-hesitation': 'bow',
    'megu-bow-straighten': 'bow', 'lady-matsus-rage-bow-destroy': 'bow', 'hidden-defenses-bow': 'bow',
    'never-afraid': 'destroy', 'black-marketeering-take': 'destroy', 'remember-ancestors-choose': 'destroy',
    'steel-on-steel-duel': 'destroy', 'makoto-duel': 'destroy', 'shikei-duel': 'destroy',
    'unstoppable-strike-destroy': 'destroy', 'akodo-guidance-melee': 'destroy', 'dairuko-melee': 'destroy',
    'costly-feud': 'reduce', 'kouki-force': 'reduce', 'strength-in-terror': 'reduce',
    'overcome-adversity': 'reduce', 'strength-of-the-spider': 'reduce', 'swell-of-strength-attack': 'reduce',
    'superior-reach-zero': 'reduce', 'mai-move-force-penalty': 'reduce', 'hearts-enemy-weaken': 'reduce',
    'tactical-advantage': 'add', 'defensive-stance': 'add', 'crushing-force': 'add', 'pure-intent': 'add',
    'michi-move': 'add', 'natsumi-move': 'add', 'back-to-the-front-move': 'add',
    'moving-unmoving-approach': 'add', 'hidden-defenses-move': 'add', 'broken-alliance-move': 'add',
    'sound-of-thunder-move': 'add', 'dislodge-the-foe': 'add', 'again-boost': 'add',
    'perfect-sacrifice-battle': 'add', 'norimichi-army': 'add', 'p5-two-fold-virtue': 'add',
    'p5-yutaka-return': 'add', 'p5-yutaka-return-dark': 'add', 'at-any-cost-recruit': 'add',
    'celestial-sword-lion-create': 'add', 'kobi-recruit': 'add', 'feign-death-action': 'add',
    'honor-lion-boost': 'add', 'celestial-sword-phoenix-return': 'add',
    'power-word-reaction': 'negate', 'tanimura-political-pay': 'negate', 'red-crane-dojo-ninja': 'negate',
    'blade-of-champions-redirect': 'negate', 'hundred-fold-cut-bow': 'negate',
    'hundred-fold-cut-destroy': 'negate', 'incense-mill-cleanse': 'negate',
    'wall-of-honor-lower': 'strength', 'wall-of-honor-raise': 'strength', 'ring-of-earth-lower': 'strength',
    'unstoppable-strike-grant': 'other', 'kano-destroy-token': 'other', 'brawl-destroy': 'other',
    'control-the-board-terrain': 'other', 'fubatsu-plate-move': 'other', 'tsunami-straighten': 'add',
    'equip-battle': 'other', 'naval-invasion': 'other', 'sneak-attack-claim': 'other',
    'tenbatsu-engage-look': 'other', 'hundred-fold-cut-draw': 'other', 'preserving-beauty': 'other',
    'armor-heavens-gain': 'other', 'eternal-victory-draw': 'other', 'two-hands-grant': 'other',
    'without-mercy-action': 'other', 'dairuko-rally': 'other', 'kanpeki-additional-action': 'other',
    'blade-perfection-attach': 'other', 'discovering-conspiracy-expose': 'other', 'tadanobu-draw': 'other',
    'kurou-attach-spell': 'other', 'ring-of-void-recruit': 'other', 'aya-entry-dark-virtue': 'other',
    'broken-alliance-destroy': 'destroy', 'home-guard-bow': 'bow',
}


def classify(ability, text=''):
    """Class of a Battle action from its ability id (and description as a fallback)."""
    a = ability or ''
    if a in ACTION_OVERRIDE:
        return ACTION_OVERRIDE[a]
    toks = set(a.split('-'))
    t = (text or '').lower()
    if 'negate' in toks or 'reaction' in toks:
        return 'negate'
    if toks & {'lower', 'raise'} or 'strength of the current battlefield' in t:
        return 'strength'
    if 'straighten' in toks or toks & {'boost', 'recruit', 'return', 'create'}:
        return 'add'
    if toks & {'destroy', 'attack', 'melee'} or re.search(r'(ranged|melee) (\d+ )?attack', t):
        return 'destroy'
    if 'home' in toks or re.search(r'move (him|them|it|the personality) home', t):
        return 'home'
    if 'bow' in toks:
        return 'bow'
    if toks & {'penalty', 'weaken', 'zero'} or re.search(r'force penalty|reduce (his|its) force|-\d+f\b', t):
        return 'reduce'
    if 'duel' in toks:
        if 'destroy the' in t:
            return 'destroy'
        if 'home' in t:
            return 'home'
        return 'bow'
    if re.search(r'\bbow (him|it|the target|a target enemy)', t):
        return 'bow'
    if re.search(r'\+\d+ ?f\b|force bonus|straighten', t):
        return 'add'
    return 'other'


def battle_actions(b):
    """[(cmd, class)] for the useAbility commands of a Battle (Battle phase only)."""
    return [(c, classify(c.ability, c.text())) for c in b.cmds if c.verb == 'useAbility']


def card_classes(games):
    """card name -> Counter(class), from every Battle-phase `useAbility card=` seen in `games`."""
    out = collections.defaultdict(collections.Counter)
    for g in games:
        for t in g.turns:
            for a in t.attacks:
                for b in a.battles.values():
                    for c, k in battle_actions(b):
                        if c.args.get('card'):
                            out[c.args['card']][k] += 1
    return out


def expand_dirs(argv):
    """Command-line helper: returns [(dir, labels-or-None)]."""
    out = []
    for a in argv:
        if '=' in a and not os.path.exists(a):
            d, lab = a.split('=', 1)
            ch, cp = (lab.split(',') + [None])[:2]
            out.append((d, {'C': ch, 'c': cp or ch, 'H': ch}))
        else:
            out.append((a, None))
    return out


def load(argv):
    """Parse every *.log under the given directories.  Returns list of Game."""
    games = []
    for d, lab in expand_dirs(argv):
        files = sorted(glob.glob(os.path.join(d, '*.log'))) if os.path.isdir(d) else [d]
        for f in files:
            g = parse_game(f, lab)
            if g is not None:
                games.append(g)
    return games


PTYPE_ORDER = ['Human', 'Conan', 'Contender(vsConan)', 'Contender(mirror)', 'PracticeBot']


def ptypes(games):
    seen = set(p for g in games for p in g.ptype)
    return [p for p in PTYPE_ORDER if p in seen] + sorted(seen - set(PTYPE_ORDER))


def pct(a, b, digits=0):
    if not b:
        return '-'
    return ('%.' + str(digits) + 'f%%') % (100.0 * a / b)


def mean(xs, digits=1):
    xs = list(xs)
    if not xs:
        return '-'
    return ('%.' + str(digits) + 'f') % (sum(xs) / len(xs))


def median(xs):
    xs = sorted(xs)
    if not xs:
        return '-'
    n = len(xs)
    return xs[n // 2] if n % 2 else (xs[n // 2 - 1] + xs[n // 2]) / 2


def table(headers, rows, out=sys.stdout):
    """Print a GitHub-markdown table."""
    out.write('| ' + ' | '.join(str(h) for h in headers) + ' |\n')
    out.write('|' + '|'.join('---' for _ in headers) + '|\n')
    for r in rows:
        out.write('| ' + ' | '.join(str(x) for x in r) + ' |\n')
    out.write('\n')


def own_turn(n):
    """Global turn number -> the active player's own turn number (1, 1, 2, 2, ...)."""
    return (n + 1) // 2


# --------------------------------------------------------------------------
# Shared derived facts used by the q*.py scripts

SMALL = ('Human', 'PracticeBot')


def rate(a, b, ptype=None):
    """'a/b' for the 7-game human sample (and its opponent) or tiny denominators, else a percentage."""
    if not b:
        return '-'
    if ptype in SMALL or b < 30:
        return '%d/%d' % (a, b)
    return '%.0f%%' % (100.0 * a / b)


def tb(k, last=9):
    """Own-turn bucket label."""
    return str(k) if k < last else '%d+' % last


def has_attack(t):
    """The active seat declared an attack and assigned at least one unit."""
    return t.attack is not None and len(t.attack.assigned) > 0


def committed_force(a):
    """Estimated Force assigned by the attacker (all battlefields)."""
    return sum(b.est_A or 0 for b in a.battles.values() if b.att_assigned)


def attack_uncertain(a):
    return any(b.est_uncertain for b in a.battles.values() if b.real())


def target_strength(a):
    xs = [b.strength for b in a.battles.values() if b.att_assigned and b.strength is not None]
    return sum(xs) / len(xs) if xs else None


def provinces_lost_on(t, seat):
    """Provinces of `seat` destroyed during turn t (any Attack Phase of the turn)."""
    return sum(len(a.destroyed) for a in t.attacks if a.defender == seat)


def units_lost_on(t, seat):
    """Personalities of `seat` destroyed during turn t (battles + outside battles)."""
    n = len(t.unit_losses[seat])
    for a in t.attacks:
        n += a.att_lost if a.attacker == seat else a.def_lost
    return n


def free_defenders(t, a):
    """Personalities the defender of attack `a` could still assign: unbowed at the TURN header,
    minus those the attacker bowed, destroyed or sent away in the Action phase before the attack
    (ability class bow/destroy/home aimed at them, or a Shinden offer answered with 'bow').
    Returns a list of Card.  Bowing by untargeted or mass effects is not seen (approximation)."""
    st = t.status.get(a.defender)
    if st is None:
        return []
    free = list(st.unbowed_pers)
    cmds = t.cmds
    for i, c in enumerate(cmds):
        if c.phase != 'Action' or c.seat != a.attacker or c.verb != 'useAbility':
            continue
        tgt = c.args.get('target')
        if not tgt:
            continue
        hit = classify(c.ability, c.text()) in ('bow', 'destroy', 'home')
        if c.ability == 'shinden-choice':
            nxt = next((x for x in cmds[i + 1:i + 4] if x.verb == 'chooseShinden'), None)
            hit = nxt is not None and nxt.args.get('choice') == 'bow'
        if hit:
            for x in free:
                if x.name == tgt:
                    free.remove(x)
                    break
    return free


def choice(t):
    """What the active seat did with its army this turn: 'none', 'partial', 'all-in', or None
    when it had no unbowed Personality at the header (no decision to make)."""
    st = t.status.get(t.active)
    if st is None or not st.unbowed_pers:
        return None
    if not has_attack(t):
        return 'none'
    return 'all-in' if len(t.attack.assigned) >= len(st.unbowed_pers) else 'partial'


def balance_bucket(d):
    if d <= -8:
        return 'a: <=-8'
    if d <= -1:
        return 'b: -7..-1'
    if d <= 6:
        return 'c: 0..6'
    if d <= 14:
        return 'd: 7..14'
    return 'e: >=15'


def selfcheck(games):
    n_stat = ok_stat = 0
    n_res = ok_res = 0
    n_est = ok_est = close_est = 0
    n_unc = 0
    amb = 0
    battles = 0
    for g in games:
        amb += g.ambiguous
        for t in g.turns:
            for s, st in t.status.items():
                n_stat += 1
                ok_stat += (len(st.pers) == st.pers_n)
            a = t.attack
            if not a:
                continue
            for b in a.battles.values():
                if not b.real() or b.A is None:
                    continue
                battles += 1
                # resolution-loss rule vs membership
                if b.A != b.D:
                    n_res += 1
                    loser = b.def_now if b.A > b.D else b.att_now
                    lost = b.def_lost_res if b.A > b.D else b.att_lost_res
                    # membership counter already decremented for resolution losses
                    remaining = sum(v for v in loser.values() if v > 0)
                    ok_res += (remaining == 0)
                acts = [c for c in b.cmds if c.verb not in ('pass', 'declineReaction', 'passAllBattles')]
                if not acts and not b.est_uncertain:
                    n_est += 1
                    if b.est_A == b.A and b.est_D == b.D:
                        ok_est += 1
                    if abs(b.est_A - b.A) <= 2 and abs(b.est_D - b.D) <= 2:
                        close_est += 1
                if b.est_uncertain:
                    n_unc += 1
    print('games parsed: %d' % len(games))
    print('status lines: %d; Personality count from home list == `pers N`: %s' % (n_stat, pct(ok_stat, n_stat, 1)))
    print('battles with units and a resolution line: %d' % battles)
    print('decided battles where every unit of the losing side still present was destroyed '
          '(membership tracking agrees with engine): %s of %d' % (pct(ok_res, n_res, 1), n_res))
    print('Force estimate vs engine in battles with no Battle action: exact %s, within 2 %s (n=%d); '
          'battles flagged uncertain (duplicate names): %d' % (pct(ok_est, n_est, 1), pct(close_est, n_est, 1), n_est, n_unc))
    print('destroy/move events where both sides held the name (attributed to the non-acting side): %d' % amb)


if __name__ == '__main__':
    gs = load(sys.argv[1:])
    selfcheck(gs)
