using L5R.Core;

namespace L5R.AI;

// Issue #1206 (battle lookahead): instead of a rule guessing whether a battle is won, the bot plays its best few battle actions out to the end of the attack on
// copies of the match whose hidden cards are redealt (MatchEngine.Sample), with the Contender playing both seats, and keeps the action whose outcomes score best.
// Card-agnostic: the engine resolves every card; the score reads only Provinces, units, Honor and cards. Opt-in rule "look", and only when the caller supplies
// BotContext.Sampler (the bot-vs-bot arena does); knobs lkK (candidates searched, 3), lkN (samples per candidate, 4), lkM (margin a searched action needs over the
// Contender's own first choice, 0); lkAtk, lkDef: also search whether to attack and the defenders' assignment; lkOpp: the other seat is played by the Champion in playouts (an opponent model); lkP, lkU, lkH, lkC: the score's weights for a Province (12), a unit's
// Gold Cost plus Force (1), Honor (1.5) and a card in hand (1.5).
public static partial class TrainingOpponent
{
    private const int RolloutDecisions = 400;

    private static (GameCommand Command, ActionOption Action)? LookAhead(Ctx x, (ActionOption Action, GameCommand Command, double Score)[] candidates)
    {
        if (x.Memory.Sampler is not { } sampler || !Rule(x, "look") || !Searched(x) || candidates.Length < 2) return null;
        // Passing and passing every remaining battle are the same move in this battle; searching both spends the budget on a tie.
        var searched = candidates.Where((c, i) => !IsPass(c.Command) || !candidates.Take(i).Any(o => IsPass(o.Command))).Take((int)Knob(x, "lkK", 3)).ToArray();
        if (searched.Length < 2) return null;
        var samples = (int)Knob(x, "lkN", 4);
        var values = new double[searched.Length];
        for (var s = 0; s < samples; s++)
        {
            var seed = (ulong)x.View.Version * 1_000_003UL + (ulong)s * 7919UL + (ulong)x.View.ViewerId;
            for (var i = 0; i < searched.Length; i++) values[i] += SafePlayout(x, sampler, seed, searched[i].Command) / samples; // the same redeal for every candidate: a paired comparison
        }
        var best = Enumerable.Range(0, searched.Length).MaxBy(i => values[i]);
        x.Memory.LookLog.Add($"T{x.View.TurnNumber} v{x.View.Version}: {string.Join(" | ", searched.Select((c, i) => $"{c.Command.Type}/{c.Command.AbilityId ?? c.Command.CardId} {values[i]:0.0}"))} -> {best}");
        return best > 0 && values[best] > values[0] + Knob(x, "lkM", 0) ? (searched[best].Command, searched[best].Action) : null;
    }

    // Battle actions always; with lkAtk the choice to attack at all, with lkDef the choice of defenders (each played out to the end of the attack the same way).
    private static bool Searched(Ctx x) => x.View.Phase switch
    {
        Phase.Battle => true,
        Phase.AttackDeclaration => Rule(x, "lkAtk"),
        Phase.DefenseAssignment or Phase.CavalryDefense => Rule(x, "lkDef"),
        _ => false
    };

    private static bool InAttack(Phase phase) => phase is Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle;

    private static bool IsPass(GameCommand command) => command.Type is "pass" or "passAllBattles";

    // A redealt copy can hold a combination the real match never reaches (a card where its owner's deck could not have put it); a playout the engine cannot finish is no evidence either way.
    private static double SafePlayout(Ctx x, Func<ulong, MatchEngine> sampler, ulong seed, GameCommand command)
    {
        try { return Playout(x, sampler(seed), command); }
        catch (Exception e) { x.Memory.LookLog.Add($"playout failed: {e.GetType().Name}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("L5R."))?.Trim()}"); return 0; }
    }

    /// <summary>The value, to the deciding seat, of playing <paramref name="command"/> on one redealt copy and letting the Contender play both seats until the attack is over.</summary>
    private static double Playout(Ctx x, MatchEngine game, GameCommand command)
    {
        var me = x.View.ViewerId;
        var before = Material(x, x.View); // a sample shows its viewer exactly this view (HiddenSampleTests)
        if (!game.Execute(command).Accepted) return double.MinValue / 4;
        var bots = new BotContext[2];
        for (var seat = 0; seat < 2; seat++) bots[seat] = seat != me && Rule(x, "lkOpp") ? new BotContext() : BotContext.Contender(rules: x.Memory.Rules.Where(r => r != "look"));
        bots[me].Archetype = x.Memory.Archetype;
        var turn = x.View.TurnNumber;
        for (var n = 0; n < RolloutDecisions; n++)
        {
            var v = game.GetView(me);
            if (v.WinnerId is not null || v.TurnNumber != turn || !InAttack(v.Phase)) break;
            var seat = v.PendingChoice?.PlayerId ?? v.PriorityPlayerId;
            var actions = game.QueryActions(seat);
            if (!actions.Any(a => a.Enabled && a.Type != "concede")) { seat = 1 - seat; actions = game.QueryActions(seat); }
            var decision = Choose(seat == me ? v : game.GetView(seat), actions, bots[seat]);
            if (decision.Command is null) break;
            if (!game.Execute(decision.Command).Accepted) bots[seat].NoteRejection(v.Version, decision.Command, null);
        }
        var after = game.GetView(me);
        if (after.WinnerId is { } winner) return winner == me ? 1000 : -1000;
        return Material(x, after) - before;
    }

    /// <summary>The deciding seat's standing, card-agnostically: Provinces, the Gold and Force of what is in play, Honor and cards in hand, each side's minus the other's.</summary>
    private static double Material(Ctx x, MatchView view)
    {
        double province = Knob(x, "lkP", 12), unit = Knob(x, "lkU", 1), honor = Knob(x, "lkH", 1.5), card = Knob(x, "lkC", 1.5);
        double Side(PlayerView p) => province * p.Provinces.Count(q => !q.Destroyed)
            + unit * p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower or CardType.Item).Sum(c => c.Definition!.GoldCost + Math.Max(0, c.Definition.Force))
            + honor * p.Honor + card * p.HandCount;
        var me = view.Players.Single(p => p.Id == view.ViewerId);
        return Side(me) - Side(view.Players.Single(p => p.Id != view.ViewerId));
    }
}
