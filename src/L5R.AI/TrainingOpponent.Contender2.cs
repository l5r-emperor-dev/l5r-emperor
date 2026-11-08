using L5R.Core;

namespace L5R.AI;

// Issue #1185 tuning: further Contender rules, each named in BotContext.Rules and switched on separately so a tournament can measure it (the c2 arm reads them from L5R_C2).
// Every rule reads only the seat's own view and printed card data.
//   econ      Holdings that are fair value (cost at most one Gold above production) are bought first in the early turns, and a province card the bot can pay for with its
//             production next turn is not discarded (Mendon: the bot over-discards; a kept card plans next turn).
//   border    the once-per-game Border Keep refresh is kept for a turn when nothing in the row can be paid for.
//   presence  defending, when an attack would raze a Province and the bot's removals (send home, bow, destroy, -Force) would stop it, the cheapest unit that is needed is sent there:
//             without a unit at the battlefield the bot may take no Battle action at all (the Rule of Presence).
public static partial class TrainingOpponent
{
    private static bool Rule(Ctx x, string name) => x.Memory.Has(name);

    private static bool FairHolding(CardDefinition def) => def.Type == CardType.Holding && def.GoldCost <= def.GoldProduction + 1;

    private static int Holdings(PlayerView p) => p.Home.Count(c => c.Definition?.Type == CardType.Holding);

    /// <summary>The Contender's further rules over an action's final score.</summary>
    private static double Contender2Score(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (x.Memory.Rules.Count == 0) return score;
        if (Rule(x, "econ") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { } bought && FairHolding(bought) && Holdings(x.Self) < 8 && x.Self.TurnsTaken <= 4)
            return SlateBonus + 50 + 5 * (bought.GoldProduction - bought.GoldCost) + score * 0.0001;
        if (Rule(x, "econ") && action.Type == "discardProvince" && x.Card(command.CardId)?.Definition is { } kept)
        {
            if (FairHolding(kept) && Holdings(x.Self) < 8) return -5;
            if (kept.Type == CardType.Personality && EffectiveCost(x, x.Card(command.CardId)!) <= Capacity(x)) return -5;
        }
        if (Rule(x, "border") && action.Type == "useAbility" && command.AbilityId == "border-game"
            && ProvinceCards(x).Any(c => EffectiveCost(x, c) <= Ready(x) && (c.Definition!.Type == CardType.Personality || FairHolding(c.Definition))))
            return Math.Min(score, BarredScore);
        if (Rule(x, "presence") && action.Type == "assignDefense" && PresencePlan(x, action) is { } plan && SameAssignments(plan, command.Assignments)) return Math.Max(score, 130);
        return score;
    }

    private static bool SameAssignments(Dictionary<string, int> a, Dictionary<string, int>? b) => b is not null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

    // ---- presence defense -------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A removal the bot could take as a defender once present: how much attacking Force it takes away (Full: the largest attacking unit), and its Gold.</summary>
    private sealed record Removal(bool Full, int Minus, int Cost);

    private static readonly HashSet<string> AttackerTargets = new(StringComparer.Ordinal) { "attacking", "enemy-attacking", "enemy-attacking-card", "enemy-personality", "enemy-unit", "enemy-force", "enemy-card" };

    private static IEnumerable<Removal> DefenderRemovals(Ctx x)
    {
        var sources = x.Self.Hand.Select(c => (Card: c, FromHand: true))
            .Concat(x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Select(c => (Card: c, FromHand: false)));
        foreach (var (card, fromHand) in sources)
        {
            if (card.Definition is null || !fromHand && (card.Bowed || card.ActionLocked)) continue;
            foreach (var (id, ability, intent) in BattlePolicy.Printed(card))
            {
                if (ability.Timing is not ("Battle" or "BattleOrOpen") || intent.IsReaction || !AttackerTargets.Contains(ability.Target ?? "")) continue;
                var minus = System.Text.RegularExpressions.Regex.Match(AbilityIntent.EffectText(ability.Label ?? ""), @"[-−]\s?(\d+)\s?F");
                var full = BattlePolicy.SendsHome(id) || intent.Rank is EffectRank.Destroy or EffectRank.Bow && !minus.Success;
                if (!full && !minus.Success) continue;
                yield return new(full, minus.Success ? int.Parse(minus.Groups[1].Value) : 0, fromHand ? card.Definition.GoldCost : 0);
                break; // one use per card
            }
        }
        if (x.View.FavorPlayerId == x.Self.Id && AbilityCatalog.All.ContainsKey("favor-battle")) yield return new(true, 0, 0);
    }

    /// <summary>The attacking Force left at a battlefield after the removals the Gold pays for, each taking the largest unit (or its Force bonus off the largest).</summary>
    private static int ForceAfterRemovals(int[] attackingUnits, Removal[] removals, int gold)
    {
        var units = attackingUnits.OrderByDescending(f => f).ToList();
        foreach (var r in removals.OrderByDescending(r => r.Full).ThenBy(r => r.Cost))
        {
            if (r.Cost > gold || units.Count == 0) continue;
            gold -= r.Cost;
            if (r.Full) units.RemoveAt(0);
            else { units[0] = Math.Max(0, units[0] - r.Minus); units = units.OrderByDescending(f => f).ToList(); }
        }
        return units.Sum();
    }

    /// <summary>
    /// The presence defense: at the battlefield where the attack would raze the Province and the removals would stop it, the cheapest of the offered units whose presence (and Force) keeps the
    /// Province standing. Null when no attack threatens a Province, when the removals would not save it, or when nothing is offered.
    /// </summary>
    private static Dictionary<string, int>? PresencePlan(Ctx x, ActionOption action)
    {
        if (x.View.Phase is not (Phase.DefenseAssignment or Phase.CavalryDefense)) return null;
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var offered = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id)))
            .OrderBy(c => UnitValue(x.Self, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (offered.Length == 0) return null;
        var removals = DefenderRemovals(x).ToArray();
        if (removals.Length == 0) return null;
        var provinces = action.Provinces ?? x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        foreach (var province in x.Self.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderByDescending(p => AttackersAt(x, p.Index)))
        {
            var attackers = x.Opponent.Army.Where(c => c.ProvinceIndex == province.Index && c.Definition?.Type == CardType.Personality)
                .Select(c => ArmyForce(x.Opponent.Army.Where(h => h.Id == c.Id || h.AttachedTo == c.Id))).Where(f => f > 0).ToArray();
            var already = ArmyForce(x.Self.Army.Where(c => c.ProvinceIndex == province.Index));
            if (attackers.Length == 0 || attackers.Sum() <= province.Strength + already) continue; // it does not fall
            var left = ForceAfterRemovals(attackers, removals, Ready(x));
            var sent = new List<CardView>(); var force = already;
            foreach (var unit in offered)
            {
                if (sent.Count > 0 && left <= province.Strength + force) break;
                sent.Add(unit); force += UnitForce(x, unit);
            }
            if (left > province.Strength + force) continue; // even with removals and everyone, it falls: the ordinary defense decides
            return sent.ToDictionary(u => u.Id, _ => province.Index, StringComparer.Ordinal);
        }
        return null;
    }
}
