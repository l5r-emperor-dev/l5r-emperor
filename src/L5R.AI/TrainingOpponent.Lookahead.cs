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
//   lkT            a search's time limit in seconds (10): past it the search is abandoned and the Contender's own choice stands, so a player is never kept waiting;
//   lkB            a search's work budget in playout decisions (0, none): past it the search ends as at lkT. Unlike the time limit it does not depend on the machine
//                  or its load, so arena arms run side by side compare fairly (block 7400 found the time limit cut arms run three at a time 5-8 times as often);
//   lkAny          anytime search: at the time limit, decide on the samples every candidate has completed instead of abandoning the search (none completed: as lkT);
//   lkP, lkU, lkH, lkC  the score's weights for a Province (12), a unit's Gold Cost plus Force (1), Honor (1.5) and a card in hand (1.5).
//   lkVal          the end state is scored by the learned win chance of ValueModel (in percent) instead of the material score;
//   lkPS, lkHS     Provinces worth more as fewer remain; Honor past lkHT (30) worth lkHx (3) times more. lkDedup: a move offered twice is searched once.
// The plan-driven parts (Mendon, 2026-11-10: "start with the plan and then make updated decisions based on new information"):
//   lkPlan    the attack is chosen by search: at the attack declaration, not attacking and each of AttackShapes are played out to the end of the attack; the best
//             becomes the turn plan's attack (BotContext.PlannedAttack), the infantry declaration carries it out, and searches it again if the table has changed;
//   lkShapes  with lkPlan, two more attack shapes: each unit alone at a Province it beats on its own Force, and the strongest pair at the weakest Province;
//   lkFar     with lkPlan, the attack's options are played out through the opponent's next attack, so units lost, Honor given away and the counter-attack count;
//   lkIntent  playouts are scored for the plan's intent: under Develop the bot's own units count lkIw (1.5) times, under Attack the opponent's Provinces, under Defend its own;
//   lkAct     an Action-phase move (outside battle: equipping, Holdings, other actions, passing) is searched too, played out through the end of this turn's attack;
//   lkMix     with lkBuy, the best Holding and the best Personality on offer are always searched; lkBH: purchase playouts run lkBH (2) turns ahead (also lkFar's);
//             lkBB: the budget of a search played through later turns (lkB times twice lkBH);
//   lkClock   a playout that ends with the next player at 40 Honor is scored as that player's Honor win (it is checked at the start of their turn); and one that ends with the
//             opponent within lkCl (1) rounds of 40 at the rate their Honor has climbed lately scores -lkCw (500) plus only the Provinces and Honor it took from them;
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
        x.Memory.SeenHonor.TryAdd(x.View.TurnNumber, x.Opponent.Honor);
        if (x.View.Phase == Phase.InfantryDeclaration) return FollowAttackPlan(x, sampler, candidates);
        if (x.View.Phase == Phase.AttackDeclaration && Rule(x, "lkPlan")) return PlanAttack(x, sampler, candidates);
        var dynasty = x.View.Phase == Phase.Dynasty;
        var pool = dynasty ? candidates.Where(c => c.Command.Type is "purchase" or "endDynasty").ToArray() : candidates;
        // Passing and passing every remaining battle are the same move in this battle; searching both spends the budget on a tie.
        // lkDedup: the same move offered twice (differing only in how it is paid for) is searched once, so the budget goes to a different move.
        if (Rule(x, "lkDedup")) pool = pool.DistinctBy(c => System.Text.Json.JsonSerializer.Serialize(c.Command with { SourceIds = null })).ToArray();
        var searched = pool.Where((c, i) => !IsPass(c.Command) || !pool.Take(i).Any(o => IsPass(o.Command))).Take((int)Knob(x, dynasty ? "lkBK" : "lkK", 3))
            .Select(c => new Option(c.Action, c.Command, Label(c.Command))).ToList();
        // lkMix: the best Holding and the best Personality on offer are always among the purchases searched, so the choice between economy and bodies is made by
        // playing both forward from this position (its threats and openings), not by the Contender's ranking (Mendon, 2026-11-11: decisions by opportunities and threats).
        if (dynasty && Rule(x, "lkMix"))
            foreach (var type in new[] { CardType.Holding, CardType.Personality })
                if (pool.FirstOrDefault(c => c.Command.Type == "purchase" && x.Card(c.Command.CardId)?.Definition?.Type == type) is { Action: not null } pick && !searched.Any(o => o.Command.CardId == pick.Command.CardId))
                    searched.Add(new(pick.Action, pick.Command, Label(pick.Command)));
        if (dynasty && pool.FirstOrDefault(c => c.Command.Type == "endDynasty") is { Action: not null } end && !searched.Any(o => o.Command.Type == "endDynasty"))
            searched.Add(new(end.Action, end.Command, "endDynasty")); // buying nothing more is always one of the options
        // lkVar: the defenders' assignment is searched over a few card-agnostic shapes besides the one the Contender's rules built (DefenceShapes).
        if (Rule(x, "lkVar") && candidates.FirstOrDefault(c => c.Command.Type == "assignDefense" && c.Command.Assignments is not null) is { Action: not null } defend)
            searched.AddRange(DefenceShapes(x, defend.Action).Where(a => !searched.Any(o => SameAssignment(o.Command.Assignments, a)))
                .Select(a => new Option(defend.Action, defend.Command with { Assignments = a }, "defend " + a.Count)));
        if (searched.Count < 2 || candidates[0].Command != searched[0].Command) return null;
        if (Search(x, sampler, searched, (int)Knob(x, dynasty ? "lkBN" : "lkN", dynasty ? 3 : 4), dynasty) is not { } values) return null;
        var best = Enumerable.Range(0, searched.Count).MaxBy(i => values[i]);
        return best > 0 && values[best] > values[0] + Knob(x, "lkM", 0) ? (searched[best].Command, searched[best].Action) : null;
    }

    /// <summary>The mean value of each option over the same redealt samples (a paired comparison), logged with the time it took; null when it ran past its time limit (lkT).</summary>
    private static double[]? Search(Ctx x, Func<ulong, MatchEngine> sampler, IReadOnlyList<Option> options, int samples, bool throughNextAttack = false)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var sums = new double[options.Count]; var counts = new int[options.Count];
        var live = Enumerable.Range(0, options.Count).ToList();
        var drop = Knob(x, "lkDrop", 0);
        var limit = DateTime.UtcNow.AddSeconds(Knob(x, "lkT", 10));
        var deadline = x.Memory.Deadline is { } cutoff && cutoff < limit ? cutoff : limit;
        var rounds = 0; x.Memory.LookWork = 0;
        try
        {
            for (var s = 0; s < samples; s++)
            {
                // lkDrop: after half the samples, a candidate trailing the best by more than this is not played out further (successive halving).
                if (drop > 0 && s == samples / 2 && live.Count > 1) { var lead = live.Max(i => sums[i] / counts[i]); live.RemoveAll(i => sums[i] / counts[i] < lead - drop); }
                var seed = (ulong)x.View.Version * 1_000_003UL + (ulong)s * 7919UL + (ulong)x.View.ViewerId;
                // A round is added only once every live candidate has played this sample, so a search cut short (lkAny) still compares like with like.
                var round = live.ToDictionary(i => i, i => SafePlayout(x, sampler, seed, options[i], throughNextAttack, deadline));
                foreach (var (i, value) in round) { sums[i] += value; counts[i]++; }
                rounds++;
            }
        }
        catch (BotDeadlineException) when ((x.Memory.Deadline is not { } game || DateTime.UtcNow < game) && Rule(x, "lkAny") && rounds > 0)
        {
            x.Memory.LookLog.Add($"T{x.View.TurnNumber} {x.View.Phase} v{x.View.Version}: limit after {rounds} of {samples} samples ({x.Memory.LookWork} decisions), deciding on those");
        }
        catch (BotDeadlineException) when (x.Memory.Deadline is not { } game || DateTime.UtcNow < game)
        {
            x.Memory.ThinkMs.Add(clock.Elapsed.TotalMilliseconds);
            x.Memory.LookLog.Add($"T{x.View.TurnNumber} {x.View.Phase} v{x.View.Version} {clock.Elapsed.TotalMilliseconds:0}ms {x.Memory.LookWork}w: over the limit, the Contender's choice stands");
            return null;
        }
        var values = Enumerable.Range(0, options.Count).Select(i => live.Contains(i) ? sums[i] / counts[i] : double.MinValue / 8).ToArray();
        x.Memory.ThinkMs.Add(clock.Elapsed.TotalMilliseconds);
        x.Memory.LookLog.Add($"T{x.View.TurnNumber} {x.View.Phase} v{x.View.Version} {clock.Elapsed.TotalMilliseconds:0}ms {x.Memory.LookWork}w: {string.Join(" | ", options.Select((o, i) => $"{o.Label} {values[i]:0.0}"))} -> {Array.IndexOf(values, values.Max())}");
        return values;
    }

    /// <summary>Issue #1206: stops a decision that has run past the seat's deadline (BotContext.Deadline).</summary>
    internal static void CheckDeadline(BotContext memory)
    {
        if (memory.Deadline is { } deadline && DateTime.UtcNow > deadline) throw new BotDeadlineException("a decision ran past the bot's deadline");
    }

    private static string Label(GameCommand command) => command.Type + (command.AbilityId ?? command.CardId) switch { null => "", var id => "/" + id };

    // Battle actions always; with lkAtk or lkPlan the attack, with lkDef the choice of defenders, with lkBuy the Dynasty purchases, with lkAct Action-phase moves.
    private static bool Searched(Ctx x) => x.View.Phase switch
    {
        Phase.Battle => true,
        Phase.AttackDeclaration => Rule(x, "lkAtk") || Rule(x, "lkPlan"),
        Phase.InfantryDeclaration => Rule(x, "lkPlan"),
        Phase.DefenseAssignment or Phase.CavalryDefense => Rule(x, "lkDef"),
        Phase.Dynasty => Rule(x, "lkBuy"),
        Phase.Action => Rule(x, "lkAct"),
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
        if (Search(x, sampler, options, (int)Knob(x, "lkN", 4), Rule(x, "lkFar")) is not { } values) { x.Memory.PlannedAttack = null; return null; }
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
        if (Search(x, sampler, options, (int)Knob(x, "lkN", 4), Rule(x, "lkFar")) is not { } values) return null;
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
        if (!Rule(x, "lkShapes")) yield break;
        // lkShapes: each unit alone at a Province it beats on its own Force (strongest unit at the strongest Province it beats), the rest home; Mendon's split attacks.
        var split = new Dictionary<string, int>(StringComparer.Ordinal); var open = targets.ToList();
        foreach (var unit in units)
            if (open.Where(p => UnitForce(x, unit) > p.Strength).OrderByDescending(p => p.Strength).FirstOrDefault() is { } beaten) { split[unit.Id] = beaten.Index; open.Remove(beaten); }
        if (split.Values.Distinct().Count() > 1) yield return ("split", split);
        // and the strongest two units together at the weakest Province, the rest home (a raze that keeps defenders back).
        if (units.Count > 2) yield return ("pair", new(StringComparer.Ordinal) { [units[0].Id] = weakest.Index, [units[1].Id] = weakest.Index });
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
    private static double SafePlayout(Ctx x, Func<ulong, MatchEngine> sampler, ulong seed, Option option, bool throughNextAttack, DateTime deadline)
    {
        try { return Playout(x, sampler(seed), option, throughNextAttack, deadline); }
        catch (Exception e) when (e is not BotDeadlineException) { x.Memory.LookLog.Add($"playout failed: {e.GetType().Name}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("L5R."))?.Trim()}"); return 0; }
    }

    /// <summary>The value, to the deciding seat, of playing the option on one redealt copy and letting the Contender play both seats until the attack is over
    /// (or, for a purchase, until the bot's own next attack is over). An attack plan's infantry assignment is made as planned.</summary>
    private static double Playout(Ctx x, MatchEngine game, Option option, bool throughNextAttack, DateTime deadline)
    {
        var me = x.View.ViewerId;
        var before = Material(x, x.View); // a sample shows its viewer exactly this view (HiddenSampleTests)
        if (!game.Execute(option.Command).Accepted) return double.MinValue / 4;
        var bots = new BotContext[2];
        for (var seat = 0; seat < 2; seat++) bots[seat] = seat != me && Rule(x, "lkOpp") ? new BotContext() : BotContext.Contender(rules: x.Memory.Rules.Where(r => r != "look"));
        bots[me].Archetype = x.Memory.Archetype;
        bots[0].Deadline = bots[1].Deadline = deadline; // a playout bot stuck in one decision is stopped too
        var fromAction = x.View.Phase == Phase.Action; // lkAct: an Action-phase move is played out through the end of this turn's attack
        var turn = x.View.TurnNumber; var infantry = option.Infantry; var horizon = (int)Knob(x, "lkBH", 2);
        // a playout through later turns does several times a battle's work: its search's budget is lkBB, by default lkB times twice the horizon
        var budget = (int)(throughNextAttack ? Knob(x, "lkBB", Knob(x, "lkB", 0) * horizon * 2) : Knob(x, "lkB", 0));
        for (var n = 0; n < (throughNextAttack ? LongRolloutDecisions * horizon / 2 : RolloutDecisions); n++)
        {
            if (game.WinnerId is not null) break;
            if (DateTime.UtcNow > deadline) throw new BotDeadlineException("a lookahead playout ran past its deadline");
            if (++x.Memory.LookWork > budget && budget > 0) throw new BotDeadlineException("a lookahead search used up its work budget");
            if (throughNextAttack ? game.TurnNumber > turn + horizon || game.TurnNumber == turn + horizon && game.Phase >= Phase.Dynasty : game.TurnNumber != turn || !InAttack(game.Phase) && !(fromAction && game.Phase == Phase.Action)) break;
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
        // lkVal: the end state is scored as the learned chance of winning, in percent (ValueModel); a decided game is 100 or 0.
        if (Rule(x, "lkVal")) return after.WinnerId is { } decided ? decided == me ? 100 : 0 : 100 / (1 + Math.Exp(-ValueModel.Logit(after)));
        if (after.WinnerId is { } winner) return winner == me ? 1000 : -1000;
        if (Rule(x, "lkClock") && Clock(x, after) is { } clock) return clock;
        return Material(x, after) - before;
    }

    /// <summary>lkClock: the Honor win a playout's end leads to before the bot can act again, or null. The player whose turn comes next wins at 40 Honor at its start;
    /// beyond that, the opponent's Honor is projected at the rate it climbed over the last round or more the bot saw.</summary>
    private static double? Clock(Ctx x, MatchView after)
    {
        var me = after.Players.Single(p => p.Id == after.ViewerId); var them = after.Players.Single(p => p.Id != after.ViewerId);
        var next = after.ActivePlayerId == me.Id ? them : me;
        if (next.Honor >= 40) return next == me ? 1000 : -1000;
        var turn = x.View.TurnNumber; var seen = x.Memory.SeenHonor.Where(s => s.Key <= turn - 2).OrderByDescending(s => s.Key).Select(s => (int?)s.Key).FirstOrDefault();
        if (seen is not { } from) return null;
        var perRound = 2.0 * (x.Opponent.Honor - x.Memory.SeenHonor[from]) / (turn - from);
        if (perRound <= 0 || them.Honor + perRound * Knob(x, "lkCl", 1) < 40) return null;
        // the units a race costs do not matter; only what slows the opponent does: Provinces taken from them and Honor they lost
        var was = x.Opponent;
        return -Knob(x, "lkCw", 500) + Knob(x, "lkP", 12) * (was.Provinces.Count(p => !p.Destroyed) - them.Provinces.Count(p => !p.Destroyed)) + Knob(x, "lkH", 1.5) * (was.Honor - them.Honor);
    }

    /// <summary>The deciding seat's standing, card-agnostically: Provinces, the Gold and Force of what is in play, Honor and cards in hand, each side's minus the other's.
    /// With lkIntent the plan's intent weights it: Develop the bot's own units, Attack the opponent's Provinces, Defend the bot's own Provinces.</summary>
    private static double Material(Ctx x, MatchView view)
    {
        double province = Knob(x, "lkP", 12), unit = Knob(x, "lkU", 1), honor = Knob(x, "lkH", 1.5), card = Knob(x, "lkC", 1.5);
        var intent = Rule(x, "lkIntent") ? x.Plan?.Intent : null; var lean = Knob(x, "lkIw", 1.5);
        var gold = Knob(x, "lkG", Rule(x, "lkBuy") ? 3 : 0); // a purchase's worth includes the economy it buys; a battle rarely changes Holdings
        // lkPS: a Province is worth more the fewer remain (losing the last of four costs four times the first); lkHS: each point of Honor past lkHT (30) counts lkHx (3) more, as the Honor win nears.
        var scarce = Rule(x, "lkPS"); var steep = Rule(x, "lkHS"); double threshold = Knob(x, "lkHT", 30), extra = Knob(x, "lkHx", 3);
        double Provinces(int n) => scarce ? Enumerable.Range(1, n).Sum(k => 4.0 / k) : n;
        double Side(PlayerView p, bool mine) =>
            province * (mine && intent == PlanIntent.Defend || !mine && intent == PlanIntent.Attack ? lean : 1) * Provinces(p.Provinces.Count(q => !q.Destroyed))
            + (steep ? honor * extra * Math.Max(0, p.Honor - threshold) : 0)
            + unit * (mine && intent == PlanIntent.Develop ? lean : 1) * p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower or CardType.Item).Sum(c => c.Definition!.GoldCost + Math.Max(0, c.Definition.Force))
            + honor * p.Honor + card * p.HandCount
            + gold * p.Home.Where(c => c.Definition?.Type == CardType.Holding).Sum(c => c.Definition!.GoldProduction);
        return Side(view.Players.Single(p => p.Id == view.ViewerId), true) - Side(view.Players.Single(p => p.Id != view.ViewerId), false);
    }
}
