using L5R.Core;

namespace L5R.AI;

// Issue #1206 (battle lookahead): instead of a rule guessing whether a battle is won, the bot plays its best few battle actions out to the end of the attack on
// copies of the match whose hidden cards are redealt (MatchEngine.Sample), with the Contender playing both seats, and keeps the action whose outcomes score best.
// Card-agnostic: the engine resolves every card; the score reads only Provinces, units, Honor and cards. Opt-in rule "look", and only when the caller supplies
// BotContext.Sampler (the bot-vs-bot arena does); knobs lkK (candidates searched, 3), lkN (samples per candidate, 4), lkM (margin a searched action needs over the
// Contender's own first choice, 0).
public static partial class TrainingOpponent
{
    private const int RolloutDecisions = 400;

    private static (GameCommand Command, ActionOption Action)? LookAhead(Ctx x, (ActionOption Action, GameCommand Command, double Score)[] candidates)
    {
        if (x.Memory.Sampler is not { } sampler || !Rule(x, "look") || x.View.Phase != Phase.Battle || candidates.Length < 2) return null;
        var searched = candidates.Take((int)Knob(x, "lkK", 3)).ToArray();
        var samples = (int)Knob(x, "lkN", 4);
        var values = new double[searched.Length];
        for (var s = 0; s < samples; s++)
        {
            var seed = (ulong)x.View.Version * 1_000_003UL + (ulong)s * 7919UL + (ulong)x.View.ViewerId;
            for (var i = 0; i < searched.Length; i++) values[i] += Playout(x, sampler(seed), searched[i].Command) / samples; // the same redeal for every candidate: a paired comparison
        }
        var best = Enumerable.Range(0, searched.Length).MaxBy(i => values[i]);
        x.Memory.LookLog.Add($"T{x.View.TurnNumber} v{x.View.Version}: {string.Join(" | ", searched.Select((c, i) => $"{c.Command.Type}/{c.Command.AbilityId ?? c.Command.CardId} {values[i]:0.0}"))} -> {best}");
        return best > 0 && values[best] > values[0] + Knob(x, "lkM", 0) ? (searched[best].Command, searched[best].Action) : null;
    }

    /// <summary>The value, to the deciding seat, of playing <paramref name="command"/> on one redealt copy and letting the Contender play both seats until the attack is over.</summary>
    private static double Playout(Ctx x, MatchEngine game, GameCommand command)
    {
        var me = x.View.ViewerId;
        var before = Material(game.GetView(me));
        if (!game.Execute(command).Accepted) return double.MinValue / 4;
        var bots = new BotContext[2];
        for (var seat = 0; seat < 2; seat++) bots[seat] = BotContext.Contender(rules: x.Memory.Rules.Where(r => r != "look"));
        bots[me].Archetype = x.Memory.Archetype;
        var turn = x.View.TurnNumber;
        for (var n = 0; n < RolloutDecisions; n++)
        {
            var v = game.GetView(me);
            if (v.WinnerId is not null || v.TurnNumber != turn || v.Phase is not (Phase.Battle or Phase.BattleSelection)) break;
            var seat = v.PendingChoice?.PlayerId ?? v.PriorityPlayerId;
            var actions = game.QueryActions(seat);
            if (!actions.Any(a => a.Enabled && a.Type != "concede")) { seat = 1 - seat; actions = game.QueryActions(seat); }
            var decision = Choose(game.GetView(seat), actions, bots[seat]);
            if (decision.Command is null) break;
            if (!game.Execute(decision.Command).Accepted) bots[seat].NoteRejection(v.Version, decision.Command, null);
        }
        var after = game.GetView(me);
        if (after.WinnerId is { } winner) return winner == me ? 1000 : -1000;
        return Material(after) - before;
    }

    /// <summary>The deciding seat's standing, card-agnostically: Provinces, the Gold and Force of what is in play, Honor and cards in hand, each side's minus the other's.</summary>
    private static double Material(MatchView view)
    {
        double Side(PlayerView p) => 12 * p.Provinces.Count(q => !q.Destroyed)
            + p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower or CardType.Item).Sum(c => c.Definition!.GoldCost + Math.Max(0, c.Definition.Force))
            + 1.5 * p.Honor + 1.5 * p.HandCount;
        var me = view.Players.Single(p => p.Id == view.ViewerId);
        return Side(me) - Side(view.Players.Single(p => p.Id != view.ViewerId));
    }
}
