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
        // shinden: 1 Honor is cheap next to a bowed Personality (Mendon: the bot bowed its main unit and lost the next battle); only an opponent near an Honor victory changes that.
        if (Rule(x, "shinden") && action.Type == "chooseShinden" && x.Opponent.Honor < 34) return command.Choice == "honor" ? 120 : 60;
        if (action.Type is "useAbility" or "playStrategy" or "equip" && Contender2Bar(x, action, command)) return Math.Min(score, BarredScore);
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

    // ---- waste ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex StraightenText = new(@"\bstraighten\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>The waste rules: an action they rule out is scored below passing.</summary>
    private static bool Contender2Bar(Ctx x, ActionOption action, GameCommand command)
    {
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        var target = x.Card(command.TargetId);
        var ownTurn = x.View.ActivePlayerId == x.Self.Id;
        var openPhase = x.View.Phase == Phase.Action && x.View.PendingChoice is null;
        // selfharm: the bot's own unit is sent home or bowed only to save it from a battle it is losing.
        if (Rule(x, "selfharm") && target is not null && target.OwnerId == x.Self.Id && target.Definition?.Type is CardType.Personality or CardType.Follower
            && (BattlePolicy.SendsHome(command.AbilityId) || intent.Rank is EffectRank.Bow or EffectRank.Penalize && intent.Side != TargetSide.Own))
            return !(x.Odds is { Winning: false } && target.ProvinceIndex is not null);
        // slate: Gold spent in the Action phase is Gold the Dynasty phase that follows cannot spend on the plan's purchases.
        if (Rule(x, "slate") && openPhase && ownTurn && (action.Gold?.Cost ?? 0) is var cost and > 0 && !intent.IsReaction && x.Plan is { Slate.Length: > 0 } plan)
        {
            var slateCost = plan.Slate.Select(id => x.Card(id)).OfType<CardView>().Sum(c => EffectiveCost(x, c));
            if (Ready(x) - cost < slateCost) return true;
        }
        // purpose: on its own turn, a straighten or a bow is worth something only for an attack it will make.
        if (Rule(x, "purpose") && openPhase && ownTurn && !intent.IsReaction && command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability)
            && (StraightenText.IsMatch(AbilityIntent.EffectText(ability.Label ?? "")) || intent.Rank == EffectRank.Bow && intent.Side != TargetSide.Own) && DeclareScore(x) <= PassScore)
            return true;
        // lobby: the Favor is not worth bowing one of the last two Personalities who could defend.
        if (Rule(x, "lobby") && intent.TakesFavorBowing && x.Self.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed) <= 2) return true;
        // cycle: the Favor's discard-and-draw throws away a card the bot chose to keep.
        if (Rule(x, "cycle") && command.AbilityId == "favor-cycle") return true;
        return false;
    }

    // ---- lean attack ------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// lean: the champion defends only where it can win or save the Province, so an army with more Force than the Province's Strength plus every unbowed enemy defender razes it unopposed (and an
    /// undefended battlefield gives the defender no presence for Battle actions). The bot sends the smallest such armies at as many Provinces as it can, and keeps every other unit home, where it is a
    /// defender the opponent's next attack must beat. Null when no army can raze: the bot does not attack.
    /// </summary>
    private static Dictionary<string, int>? LeanPlan(Ctx x, CardView[] units, int[] provinces)
    {
        var defence = ArmyForce(x.Opponent.Home);
        var pool = units.Select(u => (Unit: u, Force: UnitForce(x, u))).Where(u => u.Force > 0).OrderByDescending(u => u.Force).ThenBy(u => u.Unit.Id, StringComparer.Ordinal).Take(9).ToArray();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        for (var k = Math.Min(3, Math.Min(targets.Length, pool.Length)); k >= 1; k--)
        {
            var chosen = targets.Take(k).ToArray();
            var need = chosen.Select(t => t.Strength + defence + 1).ToArray();
            int[]? best = null; var bestUsed = int.MaxValue; var slot = new int[pool.Length];
            void Search(int i, int[] force, int used)
            {
                if (used >= bestUsed) return;
                if (i == pool.Length)
                {
                    for (var j = 0; j < k; j++) if (force[j] < need[j]) return;
                    bestUsed = used; best = (int[])slot.Clone(); return;
                }
                for (var choice = 0; choice <= k; choice++) // k means home
                {
                    slot[i] = choice;
                    if (choice < k) { force[choice] += pool[i].Force; Search(i + 1, force, used + pool[i].Force); force[choice] -= pool[i].Force; }
                    else Search(i + 1, force, used);
                }
            }
            Search(0, new int[k], 0);
            if (best is not null)
                return Enumerable.Range(0, pool.Length).Where(i => best[i] < k).ToDictionary(i => pool[i].Unit.Id, i => chosen[best[i]].Index, StringComparer.Ordinal);
        }
        return null;
    }
}
