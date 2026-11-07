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
internal sealed record TurnPlan(int Turn, string Trigger, PlanIntent Intent, int Gold, int[] Sources, string[] Slate, string Why, AttackChoice? Attack = null)
{
    /// <summary>The Gold to leave untapped for the battle's own tricks (slice 2), 0 without an attack.</summary>
    public int Reserve => Attack?.Estimate.Reserve ?? 0;
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
        var attack = PhaseGroup(view.Phase) == "dynasty" ? null : ChooseAttack(x); // slice 2: the swing estimate decides whether, and where, to attack
        var (intent, why) = IntentFor(x, attack);
        var sources = Sources(x);
        var slate = Slate(x, intent, sources);
        var plan = new TurnPlan(view.TurnNumber, trigger, intent, sources.Sum(), sources, slate, why, attack);
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
        if (mine >= theirs + 6 && Personalities(self) >= Personalities(opponent)) return (PlanIntent.Develop, $"well ahead ({mine} to {theirs}): build the economy");
        return (dynasty ? PlanIntent.Attack : PlanIntent.Develop, $"no attack this turn (my Force {mine}, theirs {theirs})");
    }

    /// <summary>All the Force a player owns at home, bowed or not: what the army will be once it straightens.</summary>
    private static int TotalForce(PlayerView p) => p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c => Math.Max(0, c.Force));

    // ---- the purchase slate -----------------------------------------------------------------------------------------------------------------------------------------

    private static double CardValue(Ctx x, CardView card, PlanIntent intent)
    {
        var def = card.Definition!; var cost = EffectiveCost(x, card);
        var early = x.Self.TurnsTaken <= 2 ? 1.3 : 1.0;
        switch (def.Type)
        {
            case CardType.Holding:
                // Mendon: about one Gold more than it produces is a fair price; cost 2 producing 3 is always attractive and a 5-cost producing 3 is low priority.
                var efficiency = Math.Clamp(40 + 25 * (def.GoldProduction + 1 - cost), 0, 120);
                return efficiency * early * (intent switch { PlanIntent.Develop => 1.4, PlanIntent.Attack => 0.8, _ => 0.7 });
            case CardType.Personality:
                var threat = BattlePolicy.Printed(card).Any(p => BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent)) ? 20 : 0;
                var usable = BattlePolicy.Printed(card).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen" or "Reaction") ? 8 : 0;
                var behind = Personalities(x.Self) < Personalities(x.Opponent) ? 25 : 0;
                return (30 + 14 * def.Force + 10 * def.PersonalHonor + threat + usable + behind) * (intent switch { PlanIntent.Defend => 1.5, PlanIntent.Attack => 1.2, _ => 0.8 });
            case CardType.Follower or CardType.Item:
                return x.Self.Home.Any(c => c.Definition?.Type == CardType.Personality) ? 10 * Math.Max(0, def.Force) : 0;
            default: return 0;
        }
    }

    /// <summary>The best set of row cards, by value, that the Gold sources can pay for with each card paid from its own group of sources.</summary>
    private static string[] Slate(Ctx x, PlanIntent intent, int[] sources)
    {
        if (sources.Length == 0) return [];
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

    /// <summary>Whether every cost (largest first) can be paid from its own disjoint group of sources whose total reaches it.</summary>
    private static bool Payable(int[] costs, int[] sources)
    {
        var seen = new HashSet<(int, int)>();
        bool Fit(int card, int used)
        {
            if (card == costs.Length) return true;
            if (!seen.Add((card, used))) return false;
            for (var group = 1; group < 1 << sources.Length; group++)
            {
                if ((group & used) != 0) continue;
                var total = 0; for (var i = 0; i < sources.Length; i++) if ((group & 1 << i) != 0) total += sources[i];
                if (total < costs[card]) continue;
                // minimal groups only: dropping any one source must break the payment, otherwise a smaller group serves and leaves more for the rest
                var minimal = true; for (var i = 0; i < sources.Length && minimal; i++) if ((group & 1 << i) != 0 && total - sources[i] >= costs[card]) minimal = false;
                if (minimal && Fit(card + 1, used | group)) return true;
            }
            return false;
        }
        return Fit(0, 0);
    }

    // ---- scoring ----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The plan's say over the ordinary score. Slice one: the Dynasty phase's purchases follow the slate.</summary>
    private static double PlanScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (x.Plan is not { } plan) return score;
        // Slice 2: attack when the swing estimate says the battle is won, whatever the printed Force says; otherwise pass. An opponent about to win on Honor is the champion's own rule.
        if (action.Type == "declareAttack" && !HonorRace(x)) return plan.Attack is null ? -50 : 150 + Math.Clamp(plan.Attack.Estimate.Net, 0, 50);
        if (action.Type == "attack" && plan.Attack is not null && !HonorRace(x)) return 300;
        // Gold the battle's own tricks need is not spent before the battle (Mendon: prefer having the Gold to win the fight over recruiting; the Dynasty phase comes after).
        if (plan.Reserve > 0 && x.View.Phase == Phase.Action && action.Type is "equip" or "useAbility" or "playStrategy" && (action.Gold?.Cost ?? 0) is var cost and > 0 && Ready(x) - cost < plan.Reserve)
            return Math.Min(score, PassScore - 1);
        if (action.Type != "purchase") return score;
        var rank = Array.IndexOf(plan.Slate, command.CardId);
        if (rank >= 0) return SlateBonus - rank * 5 + score * 0.001; // the card's own variants (Clan discount, allegiance) keep their relative order
        return plan.Slate.Length > 0 || x.View.Phase == Phase.Dynasty ? Math.Min(score, OffPlanBuy) : score;
    }
}
