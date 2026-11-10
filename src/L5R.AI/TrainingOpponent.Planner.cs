using L5R.Core;

namespace L5R.AI;

// Issue #1185: the planning bot. Behind BotContext.UsePlanner (off: the champion, unchanged). Mendon's scenario interview (memory: planner-bot-interview) describes how a player
// plays a turn: weigh the opponent's table and the Gold at hand into a plan after the Events phase, then revisit it when something changes. The plan here is the first slice:
//   intent (Attack / Defend / Develop), the Gold the bot can pay with and how it breaks into holdings, and the purchase slate: the best set of cards in the province row that the
//   Gold's SHAPE can pay for (a 5 and a 3 pay for a 5 and a 3, not for an 8).
// It is rebuilt whenever its inputs change, which covers the moments a player re-thinks: the start of the turn, a Border Keep refresh, the Action phase ending, each battle (units and
// Provinces change) and the attack phase ending. Planning is list arithmetic over the permitted view, so it costs microseconds, never an engine search.
public enum PlanIntent { Develop, Defend, Attack }

/// <summary>What the bot means to do this turn, rebuilt when the picture changes. Slate: the province-row card ids it intends to buy, best first.</summary>
internal sealed record TurnPlan(int Turn, string Trigger, PlanIntent Intent, int Gold, int[] Sources, string[] Slate, string Why, AttackChoice? Attack = null, int Hold = 0)
{
    /// <summary>The Gold to leave untapped for Battle tricks: this turn's attack (slice 2), or at the end of the turn what the opponent's attack on the bot is likely to need (Hold). 0 when none.</summary>
    public int Reserve => Attack?.Estimate.Reserve ?? Hold;
}

public static partial class TrainingOpponent
{
    private const int SlateBonus = 500; // above any ordinary purchase score, so the plan's own buys come first
    private const int OffPlanBuy = 40;  // a card outside the slate is still worth buying with Gold the slate leaves over (ending the Dynasty phase scores 0)

    private static TurnPlan? PlanFor(Ctx x)
    {
        var memory = x.Memory; var view = x.View;
        var key = PlanKey(x);
        if (memory.Plan is { } current && memory.PlanKey == key) return current;
        var trigger = memory.Plan is null ? "first plan"
            : memory.Plan.Turn != view.TurnNumber ? "turn start"
            : PhaseGroup(view.Phase) != memory.PlanGroup ? "phase " + PhaseGroup(view.Phase)
            : view.Phase == Phase.Dynasty ? "purchase" : "table changed";
        var attack = PhaseGroup(view.Phase) == "dynasty" || !memory.PlannerAttack ? null : ChooseAttack(x); // slice 2: the swing estimate decides whether, and where, to attack
        var (intent, why) = IntentFor(x, attack);
        var sources = Sources(x);
        var hold = DefenseReserve(x);
        var slate = Slate(x, intent, ReserveSources(sources, hold));
        var plan = new TurnPlan(view.TurnNumber, trigger, intent, sources.Sum(), sources, slate, why, attack, hold);
        memory.NotePlan(plan, key, PhaseGroup(view.Phase));
        return plan;
    }

    private static string PhaseGroup(Phase phase) => phase switch
    {
        Phase.Straighten or Phase.Events => "events",
        Phase.Action => "action",
        Phase.Dynasty or Phase.DynastyDiscard => "dynasty",
        _ => "attack"
    };

    // Everything the plan reads: when any of it changes the plan is made again.
    private static string PlanKey(Ctx x)
    {
        var self = x.Self; var opponent = x.Opponent;
        var row = string.Join(",", ProvinceCards(x).Select(c => c.Id));
        return $"{x.View.TurnNumber}|{PhaseGroup(x.View.Phase)}|{self.Provinces.Count(p => !p.Destroyed)}|{opponent.Provinces.Count(p => !p.Destroyed)}|{Personalities(self)}|{Personalities(opponent)}|{ArmyForce(self.Home)}|{ArmyForce(opponent.Home)}|{string.Join("/", Sources(x))}|{row}";
    }

    /// <summary>The Gold each unbowed source (Stronghold, Holdings) can still produce this turn, largest first. Gold is produced per payment, so the split matters.</summary>
    private static int[] Sources(Ctx x) =>
        new[] { Production(x.Self.Stronghold, x.Self.Clan) }.Concat(x.Self.Home.Select(c => Production(c, x.Self.Clan))).Where(g => g > 0).OrderByDescending(g => g).ToArray();

    // ---- intent ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static (PlanIntent, string) IntentFor(Ctx x, AttackChoice? attack)
    {
        var self = x.Self; var opponent = x.Opponent;
        var mine = TotalForce(self); var theirs = TotalForce(opponent);
        var dynasty = PhaseGroup(x.View.Phase) == "dynasty";
        if (attack is not null) return (PlanIntent.Attack, attack.Why);
        // The Dynasty phase buys for NEXT turn: behind in material, buy bodies even if the race looks lost; far ahead, build the economy; otherwise buy toward the next attack.
        if (mine < theirs) return (PlanIntent.Defend, $"behind in Force ({mine} to {theirs}): bodies deter a counterattack");
        // pressahead (Mendon's games, 2026-11-09: well ahead, he kept buying Personalities and attacked with them): a lead is pressed with bodies, not banked in Holdings.
        if (mine >= theirs + 6 && Personalities(self) >= Personalities(opponent)) return x.Memory.Has("pressahead") ? (PlanIntent.Attack, $"well ahead ({mine} to {theirs}): press with bodies") : (PlanIntent.Develop, $"well ahead ({mine} to {theirs}): build the economy");
        return (dynasty ? PlanIntent.Attack : PlanIntent.Develop, $"no attack this turn (my Force {mine}, theirs {theirs})");
    }

    /// <summary>All the Force a player owns at home, bowed or not: what the army will be once it straightens.</summary>
    private static int TotalForce(PlayerView p) => p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c => Math.Max(0, c.Force));

    // ---- the purchase slate -----------------------------------------------------------------------------------------------------------------------------------------

    private static double CardValue(Ctx x, CardView card, PlanIntent intent)
    {
        var def = card.Definition!; var cost = EffectiveCost(x, card);
        var holdings = x.Self.Home.Count(c => c.Definition?.Type == CardType.Holding);
        var bodies = Personalities(x.Self);
        switch (def.Type)
        {
            case CardType.Holding:
                // Mendon: about one Gold more than it produces is a fair price; cost 2 producing 3 is always attractive and a 5-cost producing 3 is low priority.
                // The first smoke runs (docs/bots/experiments/2026-11-07-planner-slice1.md) showed 0.66 fewer Personalities in play than the champion, so Holdings are worth less than they were,
                // and less again as their number grows (the champion stops at ten; economy past the sixth buys little).
                var efficiency = Math.Clamp(40 + 25 * (def.GoldProduction + 1 - cost), 0, 100);
                var crowd = holdings >= 10 ? 0 : holdings >= Knob(x, "crowd", 6) ? .5 : 1.0;
                var early = x.Self.TurnsTaken <= 2 ? 1.15 : 1.0;
                return efficiency * early * crowd * (intent switch { PlanIntent.Develop => Knob(x, "hdev", 1.2), PlanIntent.Attack => Knob(x, "hatk", 0.7), _ => Knob(x, "hdef", 0.6) });
            case CardType.Personality:
                var threat = BattlePolicy.Printed(card).Any(p => BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent)) ? 20 : 0;
                var usable = BattlePolicy.Printed(card).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen" or "Reaction") ? 8 : 0;
                // Behind the opponent in bodies (the champion's catch-up recruit rule), or short of an army at all, a Personality is worth much more.
                var behind = bodies < Personalities(x.Opponent) ? 40 : 0;
                var shortHanded = bodies < 3 ? 1.4 : 1.0;
                return (Knob(x, "pbase", 60) + Knob(x, "pf", 16) * def.Force + 10 * def.PersonalHonor + threat + usable + behind) * shortHanded * (intent switch { PlanIntent.Defend => 1.5, PlanIntent.Attack => 1.2, _ => 1.0 }) + ProclaimValue(x, card, cost)
                    + RoleValue(x, card);
            case CardType.Follower or CardType.Item:
                return x.Self.Home.Any(c => c.Definition?.Type == CardType.Personality) ? 10 * Math.Max(0, def.Force) : 0;
            default: return 0;
        }
    }

    // Issue #1206 (Mendon, 2026-11-10: "I buy personalities offensively when I think I can use them to attack and tease a province, defensively when my opponent is making early
    // game moves and I need to hold them off while I build my engine ... their keywords matter, so making sure I have a Samurai if I have several actions in my hand that are Samurai
    // keyword dependent"). Each role is its own opt-in rule, read from the table and the hand only, never from a card's name:
    //   rkw   a keyword that actions in the hand need from their performer, which no Personality in play has, is worth rkw (25) per such action, up to two;
    //   roff  when the army is short of taking the opponent's weakest Province, the Personality's Force towards closing that gap is worth roff (6) a point;
    //   rdef  when the opponent's army outweighs the bot's own and its weakest Province, the Personality's Force towards closing that gap is worth rdef (6) a point.
    private static double RoleValue(Ctx x, CardView card)
    {
        var def = card.Definition!; double value = 0;
        if (Rule(x, "rkw"))
        {
            var mine = x.Self.Home.Concat(x.Self.Army).Where(c => c.Definition?.Type == CardType.Personality).ToArray();
            var needs = x.Self.Hand.SelectMany(h => BattlePolicy.Printed(h).Where(p => p.Ability.Performer is not null).SelectMany(p => p.Ability.RequiredKeywords ?? []).Distinct(StringComparer.Ordinal)).ToArray();
            foreach (var keyword in needs.Distinct(StringComparer.Ordinal))
                if (HasKeyword(card, keyword) && !mine.Any(c => HasKeyword(c, keyword))) value += Knob(x, "rkw", 25) * Math.Min(2, needs.Count(k => k == keyword));
        }
        var force = Math.Max(0, def.Force);
        var weakest = x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(0).Min();
        if (Rule(x, "roff"))
        {
            var gap = weakest + ArmyForce(x.Opponent.Home) / 2 - TotalForce(x.Self);
            if (gap > 0) value += Knob(x, "roff", 6) * Math.Min(force, gap);
        }
        if (Rule(x, "rdef"))
        {
            var mineWeakest = x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(0).Min();
            var gap = TotalForce(x.Opponent) - TotalForce(x.Self) - mineWeakest;
            if (gap > 0) value += Knob(x, "rdef", 6) * Math.Min(force, gap);
        }
        return value;
    }

    private static bool HasKeyword(CardView card, string keyword) =>
        (card.Definition?.Keywords ?? []).Contains(keyword, StringComparer.OrdinalIgnoreCase) || (card.GrantedKeywords ?? []).Any(g => string.Equals(g.Keyword, keyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>What a Proclaim is worth on this Personality (Mendon: an Honor lead can keep the opponent from taking the Imperial Favor). The same weights as the champion's: by the deck's Honor stance,
    /// the offered purchase quote saying whether the card is eligible; before the Dynasty phase there is no quote, so a Personality of the bot's own Clan is taken to be.</summary>
    private static double ProclaimValue(Ctx x, CardView card, int cost)
    {
        var quoted = x.Actions.Where(a => a.Type == "purchase" && a.CardId == card.Id && a.Quote is not null).Select(a => a.Quote!).ToArray();
        var eligible = quoted.Length > 0 ? quoted.Any(q => q.ProclaimEligible) : ClanAligned(card.Definition!, x.Self.Clan);
        if (!eligible) return 0;
        return x.Profile.StanceFor(x.Self.Honor, x.Opponent.Honor, Ready(x) - cost >= 2) switch { ProclaimStance.Prioritize => 40, ProclaimStance.Neutral => 20, _ => -10 };
    }

    /// <summary>The best set of row cards, by value, that the Gold sources can pay for with each card paid from its own group of sources.</summary>
    private static string[] Slate(Ctx x, PlanIntent intent, int[] sources)
    {
        if (sources.Length == 0) return [];
        // Mendon: "I always prefer to recruit over discarding", and when the Gold can clear the whole row, the whole row is bought.
        var whole = ProvinceCards(x);
        if (whole.Length > 0 && Payable(whole.Select(c => EffectiveCost(x, c)).OrderByDescending(c => c).ToArray(), sources))
            return whole.OrderByDescending(c => CardValue(x, c, intent)).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToArray();
        var row = ProvinceCards(x).Select(c => (Card: c, Cost: EffectiveCost(x, c), Value: CardValue(x, c, intent))).Where(c => c.Value > 0 && c.Cost <= sources.Sum()).ToArray();
        if (row.Length == 0) return [];
        double bestValue = 0; string[] best = [];
        for (var mask = 1; mask < 1 << row.Length; mask++)
        {
            var chosen = Enumerable.Range(0, row.Length).Where(i => (mask & 1 << i) != 0).Select(i => row[i]).OrderByDescending(c => c.Cost).ToArray();
            var value = chosen.Sum(c => c.Value);
            if (value <= bestValue || !Payable(chosen.Select(c => c.Cost).ToArray(), sources)) continue;
            bestValue = value; best = chosen.OrderByDescending(c => c.Value).ThenBy(c => c.Card.Id, StringComparer.Ordinal).Select(c => c.Card.Id).ToArray();
        }
        return best;
    }

    /// <summary>At the end of the turn: the Gold to leave untapped for the Battle tricks in hand, when the opponent's attack looks likely (they own enough Force to take the bot's weakest Province, or to
    /// outweigh its army). Mendon: hold Gold for equipping or Strategy costs "only if I think it is likely I'll need some or all of them". Untapped Holdings stay untapped through the opponent's turn.</summary>
    private static int DefenseReserve(Ctx x)
    {
        if (!x.Memory.PlannerReserve || PhaseGroup(x.View.Phase) != "dynasty") return 0;
        var tricks = HandTricks(x);
        if (tricks.Length == 0) return 0;
        var theirs = TotalForce(x.Opponent);
        var weakest = x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(int.MaxValue).Min();
        if (theirs <= 0 || !(theirs > weakest || theirs >= TotalForce(x.Self))) return 0;
        var enemyUnits = x.Opponent.Home.Where(c => c.Definition?.Type == CardType.Personality).Select(d => ArmyForce(x.Opponent.Home.Where(h => h.Id == d.Id || h.AttachedTo == d.Id))).ToArray();
        return Math.Min(ChooseTricks(tricks, Ready(x), enemyUnits).Costs.Sum(), Ready(x));
    }

    /// <summary>The sources left for buying once <paramref name="hold"/> Gold is kept back: the smallest sources go to the reserve first.</summary>
    private static int[] ReserveSources(int[] sources, int hold)
    {
        if (hold <= 0) return sources;
        var kept = new List<int>(sources.OrderByDescending(s => s)); var held = 0;
        for (var i = kept.Count - 1; i >= 0 && held < hold; i--) { held += kept[i]; kept.RemoveAt(i); }
        return kept.ToArray();
    }

    /// <summary>Whether every cost (largest first) can be paid from its own disjoint group of sources whose total reaches it. Issue #1211: sources of equal value are interchangeable, so the
    /// search runs over how many of each value are left rather than which sources; the subset search it replaces was exponential in Holdings and took most of a late game's time.</summary>
    private static bool Payable(int[] costs, int[] sources)
    {
        var values = sources.Distinct().OrderByDescending(v => v).ToArray();
        var left = values.Select(v => sources.Count(s => s == v)).ToArray();
        var failed = new HashSet<string>(StringComparer.Ordinal);
        bool Fit(int card)
        {
            if (card == costs.Length) return true;
            var key = card + ":" + string.Join(",", left);
            if (failed.Contains(key)) return false;
            var take = new int[values.Length];
            bool Take(int value, int total)
            {
                if (value == values.Length)
                {
                    if (total < costs[card]) return false;
                    // minimal groups only: dropping any one source must break the payment, otherwise a smaller group serves and leaves more for the rest
                    for (var i = 0; i < values.Length; i++) if (take[i] > 0 && total - values[i] >= costs[card]) return false;
                    for (var i = 0; i < values.Length; i++) left[i] -= take[i];
                    var fits = Fit(card + 1);
                    for (var i = 0; i < values.Length; i++) left[i] += take[i];
                    return fits;
                }
                for (var n = 0; n <= left[value]; n++)
                {
                    if (n > 0 && total + (n - 1) * values[value] >= costs[card]) break;
                    take[value] = n;
                    if (Take(value + 1, total + n * values[value])) return true;
                }
                take[value] = 0;
                return false;
            }
            if (Take(0, 0)) return true;
            failed.Add(key);
            return false;
        }
        return Fit(0);
    }

    // ---- scoring ----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Discarding a province card at the end of the Dynasty phase (it comes after every purchase: recruiting is always preferred). Mendon (issue #1185 interview): the champion over-discards,
    /// and a discard hides what is in the provinces, which plans the next turn. Discard only: (1) a card too dear to pay for with all the bot's production; (2) a Holding costing 4 or more that would
    /// force it to forgo a Personality it can otherwise afford, early in the game, or later when it is digging (behind in Personalities); (3) a non-Personality when behind in Personalities and no
    /// Personality on offer is affordable (it needs specific cards for a superior army and they are not there). Anything else stays: two affordable Personalities, a Personality and a Holding, and
    /// Holdings besides when the bot is not behind. Face-down cards stay (unknown). Discarding scores above ending the phase (0); keeping scores below it.</summary>
    private static double PlanDiscardScore(Ctx x, GameCommand command)
    {
        const double Discard = 20, Keep = -5;
        var row = ProvinceCards(x);
        if (row.FirstOrDefault(c => c.Id == command.CardId) is not { Definition: { } def } card) return Keep;
        var capacity = Capacity(x); var cost = EffectiveCost(x, card);
        if (cost > capacity) return Discard;
        var affordable = row.Where(c => c.Definition!.Type == CardType.Personality && EffectiveCost(x, c) <= capacity).ToArray();
        var digging = Personalities(x.Self) < Personalities(x.Opponent);
        if (def.Type == CardType.Holding && cost >= 4 && affordable.Any(p => EffectiveCost(x, p) + cost > capacity) && (x.Self.TurnsTaken <= 3 || digging)) return Discard;
        if (def.Type != CardType.Personality && digging && affordable.Length == 0) return Discard;
        return Keep;
    }

    /// <summary>The plan's say over the ordinary score. Slice one: the Dynasty phase's purchases follow the slate.</summary>
    private static double PlanScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (x.Plan is not { } plan) return score;
        // Slice 2: attack when the swing estimate says the battle is won, whatever the printed Force says; otherwise pass. An opponent about to win on Honor is the champion's own rule.
        if (x.Memory.PlannerAttack && action.Type == "declareAttack" && !HonorRace(x)) return plan.Attack is null ? -50 : 150 + Math.Clamp(plan.Attack.Estimate.Net, 0, 50);
        if (x.Memory.PlannerAttack && action.Type == "attack" && plan.Attack is not null && !HonorRace(x)) return 300;
        // Gold the battle's own tricks need is not spent before the battle (Mendon: prefer having the Gold to win the fight over recruiting; the Dynasty phase comes after).
        if (x.Memory.PlannerReserve && plan.Reserve > 0 && x.View.Phase == Phase.Action && action.Type is "equip" or "useAbility" or "playStrategy" && (action.Gold?.Cost ?? 0) is var cost and > 0 && Ready(x) - cost < plan.Reserve)
            return Math.Min(score, PassScore - 1);
        if (action.Type == "discardProvince") return x.Memory.PlannerDiscard ? PlanDiscardScore(x, command) : score;
        if (action.Type != "purchase") return score;
        // Gold the opponent's likely attack will need for Battle tricks stays untapped through their turn (DefenseReserve).
        if (x.Memory.PlannerReserve && plan.Reserve > 0 && PhaseGroup(x.View.Phase) == "dynasty" && Ready(x) - (action.Quote?.Cost ?? 0) < plan.Reserve) return -5;
        var rank = Array.IndexOf(plan.Slate, command.CardId);
        if (rank >= 0) return SlateBonus - rank * 5 + score * 0.001; // the card's own variants (Clan discount, allegiance) keep their relative order
        // Off-plan: capped, but the champion's own order among a card's variants (Proclaim, Clan discount, allegiance) must survive the cap, or ties break by name and Proclaims are lost.
        return plan.Slate.Length > 0 || x.View.Phase == Phase.Dynasty ? Math.Min(score, OffPlanBuy) + Math.Clamp(score, -1000, 1000) * 0.0001 : score;
    }
}
