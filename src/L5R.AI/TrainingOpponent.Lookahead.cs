using L5R.Core;

namespace L5R.AI;

// Issue #1206 (battle lookahead): instead of a rule guessing whether a battle is won, the bot plays its best few battle actions out to the end of the attack on
// copies of the match whose hidden cards are redealt (MatchEngine.Sample), with the Contender playing both seats, and keeps the action whose outcomes score best.
// Card-agnostic: the engine resolves every card; the score reads only Provinces, units, Honor and cards. Opt-in rule "look", and only when the caller supplies
// BotContext.Sampler (the bot-vs-bot arena does). Knobs:
//   lkK, lkN, lkM  candidates searched (3), samples per candidate (4), margin a searched action needs over the Contender's own first choice (0);
//   lkAtk, lkDef   also search whether to attack, and the defenders' assignment; lkVar: with lkDef, also compare the defence shapes of DefenceShapes;
//   lkDrop         after half the samples, stop playing out a candidate trailing the best by more than this (0, off);
//   lkOpp          the other seat is played by the Champion in playouts (an opponent model);
//   lkP, lkU, lkH, lkC  the score's weights for a Province (12), a unit's Gold Cost plus Force (1), Honor (1.5) and a card in hand (1.5).
// The plan-driven parts (Mendon, 2026-11-10: "start with the plan and then make updated decisions based on new information"):
//   lkPlan    the attack is chosen by search: at the attack declaration, not attacking and each of AttackShapes are played out to the end of the attack; the best
//             becomes the turn plan's attack (BotContext.PlannedAttack), the infantry declaration carries it out, and searches it again if the table has changed;
//   lkIntent  playouts are scored for the plan's intent: under Develop the bot's own units count lkIw (1.5) times, under Attack the opponent's Provinces, under Defend its own;
//   lkBuy     a Dynasty purchase is chosen by playing the best lkBK (3) purchases (or none) out on lkBN (3) samples through the bot's next attack; the score then also
//             counts each Holding's Gold production lkG (3) times.
public static partial class TrainingOpponent
{
    private const int RolloutDecisions = 400, LongRolloutDecisions = 2000;

    /// <summary>One move a search compares: the command, and for an attack plan the infantry assignment the playout then makes.</summary>
    private sealed record Option(ActionOption Action, GameCommand Command, string Label, Dictionary<string, int>? Infantry = null);

    private static (GameCommand Command, ActionOption Action)? LookAhead(Ctx x, (ActionOption Action, GameCommand Command, double Score)[] candidates)
    {
        if (x.Memory.Sampler is not { } sampler || !Rule(x, "look") || !Searched(x) || candidates.Length == 0) return null;
        if (x.View.Phase == Phase.InfantryDeclaration) return FollowAttackPlan(x, sampler, candidates);
        if (x.View.Phase == Phase.AttackDeclaration && Rule(x, "lkPlan")) return PlanAttack(x, sampler, candidates);
        var dynasty = x.View.Phase == Phase.Dynasty;
        var pool = dynasty ? candidates.Where(c => c.Command.Type is "purchase" or "endDynasty").ToArray() : candidates;
        // Passing and passing every remaining battle are the same move in this battle; searching both spends the budget on a tie.
        var searched = pool.Where((c, i) => !IsPass(c.Command) || !pool.Take(i).Any(o => IsPass(o.Command))).Take((int)Knob(x, dynasty ? "lkBK" : "lkK", 3))
            .Select(c => new Option(c.Action, c.Command, Label(c.Command))).ToList();
        if (dynasty && pool.FirstOrDefault(c => c.Command.Type == "endDynasty") is { Action: not null } end && !searched.Any(o => o.Command.Type == "endDynasty"))
            searched.Add(new(end.Action, end.Command, "endDynasty")); // buying nothing more is always one of the options
        // lkVar: the defenders' assignment is searched over a few card-agnostic shapes besides the one the Contender's rules built (DefenceShapes).
        if (Rule(x, "lkVar") && candidates.FirstOrDefault(c => c.Command.Type == "assignDefense" && c.Command.Assignments is not null) is { Action: not null } defend)
            searched.AddRange(DefenceShapes(x, defend.Action).Where(a => !searched.Any(o => SameAssignment(o.Command.Assignments, a)))
                .Select(a => new Option(defend.Action, defend.Command with { Assignments = a }, "defend " + a.Count)));
        if (searched.Count < 2 || candidates[0].Command != searched[0].Command) return null;
        var values = Search(x, sampler, searched, (int)Knob(x, dynasty ? "lkBN" : "lkN", dynasty ? 3 : 4), dynasty);
        var best = Enumerable.Range(0, searched.Count).MaxBy(i => values[i]);
        return best > 0 && values[best] > values[0] + Knob(x, "lkM", 0) ? (searched[best].Command, searched[best].Action) : null;
    }

    /// <summary>The mean value of each option over the same redealt samples (a paired comparison), logged with the time it took.</summary>
    private static double[] Search(Ctx x, Func<ulong, MatchEngine> sampler, IReadOnlyList<Option> options, int samples, bool throughNextAttack = false)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var sums = new double[options.Count]; var counts = new int[options.Count];
        var live = Enumerable.Range(0, options.Count).ToList();
        var drop = Knob(x, "lkDrop", 0);
        for (var s = 0; s < samples; s++)
        {
            // lkDrop: after half the samples, a candidate trailing the best by more than this is not played out further (successive halving).
            if (drop > 0 && s == samples / 2 && live.Count > 1) { var lead = live.Max(i => sums[i] / counts[i]); live.RemoveAll(i => sums[i] / counts[i] < lead - drop); }
            var seed = (ulong)x.View.Version * 1_000_003UL + (ulong)s * 7919UL + (ulong)x.View.ViewerId;
            foreach (var i in live) { sums[i] += SafePlayout(x, sampler, seed, options[i], throughNextAttack); counts[i]++; }
        }
        var values = Enumerable.Range(0, options.Count).Select(i => live.Contains(i) ? sums[i] / counts[i] : double.MinValue / 8).ToArray();
        x.Memory.ThinkMs.Add(clock.Elapsed.TotalMilliseconds);
        x.Memory.LookLog.Add($"T{x.View.TurnNumber} {x.View.Phase} v{x.View.Version} {clock.Elapsed.TotalMilliseconds:0}ms: {string.Join(" | ", options.Select((o, i) => $"{o.Label} {values[i]:0.0}"))} -> {Array.IndexOf(values, values.Max())}");
        return values;
    }

    private static string Label(GameCommand command) => command.Type + (command.AbilityId ?? command.CardId) switch { null => "", var id => "/" + id };

    // Battle actions always; with lkAtk or lkPlan the attack, with lkDef the choice of defenders, with lkBuy the Dynasty purchases.
    private static bool Searched(Ctx x) => x.View.Phase switch
    {
        Phase.Battle => true,
        Phase.AttackDeclaration => Rule(x, "lkAtk") || Rule(x, "lkPlan"),
        Phase.InfantryDeclaration => Rule(x, "lkPlan"),
        Phase.DefenseAssignment or Phase.CavalryDefense => Rule(x, "lkDef"),
        Phase.Dynasty => Rule(x, "lkBuy"),
        _ => false
    };

    private static bool InAttack(Phase phase) => phase is Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle;

    // ---- the attack plan (lkPlan) ---------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>At the attack declaration: not attacking, and attacking with each shape, played out to the end of the attack; the best becomes the plan.</summary>
    private static (GameCommand, ActionOption)? PlanAttack(Ctx x, Func<ulong, MatchEngine> sampler, (ActionOption Action, GameCommand Command, double Score)[] candidates)
    {
        var declare = candidates.FirstOrDefault(c => c.Command.Type == "declareAttack");
        var pass = candidates.FirstOrDefault(c => IsPass(c.Command));
        if (declare.Action is null) return null;
        var options = new List<Option>();
        if (pass.Action is not null) options.Add(new(pass.Action, pass.Command, "no attack"));
        options.AddRange(AttackShapes(x).Select(s => new Option(declare.Action, declare.Command, s.Label, s.Assignment)));
        if (options.Count < 2) return null;
        var values = Search(x, sampler, options, (int)Knob(x, "lkN", 4));
        var best = options[Enumerable.Range(0, options.Count).MaxBy(i => values[i])];
        x.Memory.PlannedAttack = best.Infantry is null ? null : (x.View.TurnNumber, best.Infantry, best.Label);
        x.Memory.PlanLog.Add($"T{x.View.TurnNumber} attack plan by search: {best.Label} ({string.Join(", ", options.Select((o, i) => $"{o.Label} {values[i]:0.0}"))})");
        return (best.Command, best.Action);
    }

    /// <summary>At the infantry declaration: the planned shape, as far as its units and Provinces are still legal; searched again when nothing of it is.</summary>
    private static (GameCommand, ActionOption)? FollowAttackPlan(Ctx x, Func<ulong, MatchEngine> sampler, (ActionOption Action, GameCommand Command, double Score)[] candidates)
    {
        var attack = candidates.FirstOrDefault(c => c.Command.Type == "attack");
        if (attack.Action is null) return null;
        if (x.Memory.PlannedAttack is { } plan && plan.Turn == x.View.TurnNumber && Legal(attack.Action, plan.Assignment) is { Count: > 0 } kept)
        {
            x.Memory.PlanLog.Add($"T{x.View.TurnNumber} infantry: the planned {plan.Label}{(kept.Count < plan.Assignment.Count ? $" ({plan.Assignment.Count - kept.Count} unit(s) no longer able)" : "")}");
            return (attack.Command with { Assignments = kept }, attack.Action);
        }
        // The table changed since the plan (or there was none): the shapes are searched again from here.
        var options = new List<Option> { new(attack.Action, attack.Command, "contender") };
        options.AddRange(AttackShapes(x).Select(s => Legal(attack.Action, s.Assignment)).Where(a => a.Count > 0 && !options.Any(o => SameAssignment(o.Command.Assignments, a)))
            .Select(a => new Option(attack.Action, attack.Command with { Assignments = a }, "shape " + a.Count)));
        if (options.Count < 2) return null;
        var values = Search(x, sampler, options, (int)Knob(x, "lkN", 4));
        var best = options[Enumerable.Range(0, options.Count).MaxBy(i => values[i])];
        x.Memory.PlanLog.Add($"T{x.View.TurnNumber} infantry re-planned by search: {best.Label}");
        return (best.Command, best.Action);
    }

    private static Dictionary<string, int> Legal(ActionOption action, Dictionary<string, int> assignment) =>
        assignment.Where(p => (action.TargetIds is null || action.TargetIds.Contains(p.Key)) && (action.Provinces is null || action.Provinces.Contains(p.Value)))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>Attacks worth comparing, whatever the cards: everyone at the weakest Province; the strongest units until they beat the weakest Province and every
    /// unbowed enemy defender (the rest kept home); the strongest units spread so each Province they reach is beaten on Force; and the single strongest unit (a probe).</summary>
    private static IEnumerable<(string Label, Dictionary<string, int> Assignment)> AttackShapes(Ctx x)
    {
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && UnitForce(x, c) > 0)
            .OrderByDescending(c => UnitForce(x, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        if (units.Count == 0 || targets.Length == 0) yield break;
        var weakest = targets[0]; var defence = ArmyForce(x.Opponent.Home);
        yield return ("all at the weakest", units.ToDictionary(u => u.Id, _ => weakest.Index, StringComparer.Ordinal));
        var raze = new Dictionary<string, int>(StringComparer.Ordinal); var force = 0;
        foreach (var unit in units) { if (force > weakest.Strength + defence) break; raze[unit.Id] = weakest.Index; force += UnitForce(x, unit); }
        if (raze.Count < units.Count) yield return ("enough to raze", raze);
        var spread = new Dictionary<string, int>(StringComparer.Ordinal); var free = new Queue<CardView>(units);
        foreach (var province in targets)
        {
            var sent = 0;
            while (free.Count > 0 && sent <= province.Strength) { var unit = free.Dequeue(); spread[unit.Id] = province.Index; sent += UnitForce(x, unit); }
            if (free.Count == 0) break;
        }
        if (spread.Values.Distinct().Count() > 1) yield return ("spread", spread);
        if (units.Count > 1) yield return ("probe", new(StringComparer.Ordinal) { [units[0].Id] = weakest.Index });
    }

    // ---- defence shapes (lkVar) ---------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Defences worth comparing, whatever the cards: every defender at the most threatened Province; at each threatened Province in turn, the strongest
    /// units until it holds on Force (the rest kept home); and the single strongest unit there (presence for the bot's removals, nothing more risked).</summary>
    private static IEnumerable<Dictionary<string, int>> DefenceShapes(Ctx x, ActionOption action)
    {
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id)))
            .OrderByDescending(c => UnitForce(x, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var threatened = x.Self.Provinces.Where(p => !p.Destroyed && (action.Provinces is null || action.Provinces.Contains(p.Index)) && AttackersAt(x, p.Index) > 0)
            .OrderByDescending(p => AttackersAt(x, p.Index) - p.Strength).ToArray();
        if (units.Count == 0 || threatened.Length == 0) yield break;
        yield return units.ToDictionary(u => u.Id, _ => threatened[0].Index, StringComparer.Ordinal);
        var holding = new Dictionary<string, int>(StringComparer.Ordinal); var free = new Queue<CardView>(units);
        foreach (var province in threatened)
        {
            var force = ArmyForce(x.Self.Army.Where(c => c.ProvinceIndex == province.Index));
            while (free.Count > 0 && province.Strength + force <= AttackersAt(x, province.Index)) { var unit = free.Dequeue(); holding[unit.Id] = province.Index; force += UnitForce(x, unit); }
        }
        if (holding.Count > 0) yield return holding;
        yield return new(StringComparer.Ordinal) { [units[0].Id] = threatened[0].Index };
    }

    private static bool SameAssignment(Dictionary<string, int>? a, Dictionary<string, int> b) => a is not null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

    private static bool IsPass(GameCommand command) => command.Type is "pass" or "passAllBattles";

    // ---- playouts -----------------------------------------------------------------------------------------------------------------------------------------------------

    // A redealt copy can hold a combination the real match never reaches (a card where its owner's deck could not have put it); a playout the engine cannot finish is no evidence either way.
    private static double SafePlayout(Ctx x, Func<ulong, MatchEngine> sampler, ulong seed, Option option, bool throughNextAttack)
    {
        try { return Playout(x, sampler(seed), option, throughNextAttack); }
        catch (Exception e) { x.Memory.LookLog.Add($"playout failed: {e.GetType().Name}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("L5R."))?.Trim()}"); return 0; }
    }

    /// <summary>The value, to the deciding seat, of playing the option on one redealt copy and letting the Contender play both seats until the attack is over
    /// (or, for a purchase, until the bot's own next attack is over). An attack plan's infantry assignment is made as planned.</summary>
    private static double Playout(Ctx x, MatchEngine game, Option option, bool throughNextAttack)
    {
        var me = x.View.ViewerId;
        var before = Material(x, x.View); // a sample shows its viewer exactly this view (HiddenSampleTests)
        if (!game.Execute(option.Command).Accepted) return double.MinValue / 4;
        var bots = new BotContext[2];
        for (var seat = 0; seat < 2; seat++) bots[seat] = seat != me && Rule(x, "lkOpp") ? new BotContext() : BotContext.Contender(rules: x.Memory.Rules.Where(r => r != "look"));
        bots[me].Archetype = x.Memory.Archetype;
        var turn = x.View.TurnNumber; var infantry = option.Infantry;
        for (var n = 0; n < (throughNextAttack ? LongRolloutDecisions : RolloutDecisions); n++)
        {
            if (game.WinnerId is not null) break;
            if (throughNextAttack ? game.TurnNumber > turn + 2 || game.TurnNumber == turn + 2 && game.Phase >= Phase.Dynasty : game.TurnNumber != turn || !InAttack(game.Phase)) break;
            var seat = game.PendingChoicePlayerId ?? game.PriorityPlayerId;
            var actions = game.QueryActions(seat);
            if (!actions.Any(a => a.Enabled && a.Type is not ("concede" or "wait"))) { seat = 1 - seat; actions = game.QueryActions(seat); }
            if (infantry is not null && seat == me && game.Phase == Phase.InfantryDeclaration && actions.FirstOrDefault(a => a.Enabled && a.Type == "attack" && a.Command is not null) is { } attack)
            {
                var planned = Legal(attack, infantry); infantry = null;
                if (planned.Count > 0 && game.Execute(attack.Command! with { Assignments = planned }).Accepted) continue;
            }
            var decision = Choose(game.GetView(seat), actions, bots[seat]);
            if (decision.Command is null) break;
            if (!game.Execute(decision.Command).Accepted) bots[seat].NoteRejection(game.Version, decision.Command, null);
        }
        var after = game.GetView(me);
        if (after.WinnerId is { } winner) return winner == me ? 1000 : -1000;
        return Material(x, after) - before;
    }

    /// <summary>The deciding seat's standing, card-agnostically: Provinces, the Gold and Force of what is in play, Honor and cards in hand, each side's minus the other's.
    /// With lkIntent the plan's intent weights it: Develop the bot's own units, Attack the opponent's Provinces, Defend the bot's own Provinces.</summary>
    private static double Material(Ctx x, MatchView view)
    {
        double province = Knob(x, "lkP", 12), unit = Knob(x, "lkU", 1), honor = Knob(x, "lkH", 1.5), card = Knob(x, "lkC", 1.5);
        var intent = Rule(x, "lkIntent") ? x.Plan?.Intent : null; var lean = Knob(x, "lkIw", 1.5);
        var gold = Knob(x, "lkG", Rule(x, "lkBuy") ? 3 : 0); // a purchase's worth includes the economy it buys; a battle rarely changes Holdings
        double Side(PlayerView p, bool mine) =>
            province * (mine && intent == PlanIntent.Defend || !mine && intent == PlanIntent.Attack ? lean : 1) * p.Provinces.Count(q => !q.Destroyed)
            + unit * (mine && intent == PlanIntent.Develop ? lean : 1) * p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower or CardType.Item).Sum(c => c.Definition!.GoldCost + Math.Max(0, c.Definition.Force))
            + honor * p.Honor + card * p.HandCount
            + gold * p.Home.Where(c => c.Definition?.Type == CardType.Holding).Sum(c => c.Definition!.GoldProduction);
        return Side(view.Players.Single(p => p.Id == view.ViewerId), true) - Side(view.Players.Single(p => p.Id != view.ViewerId), false);
    }
}
