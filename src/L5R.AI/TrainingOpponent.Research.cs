using L5R.Core;

namespace L5R.AI;

// Issue #1185, research arms (docs/bots/experiments/2026-11-08-research-arms.md): eight changes proposed from CCG strategy writing (docs/bots/research/2026-11-08-ccg-strategy-recommendations.md), each behind its
// own BotContext switch, off by default, and tested one at a time against the champion and the Contender on the same games. All of them need the planner (BotContext.UsePlanner), which supplies x.Plan.
public static partial class TrainingOpponent
{
    // ---- R1 discard v2: keep what can be bought TOGETHER next turn, flush the rest ---------------------------------------------------------------------------------------------

    /// <summary>The Gold each Stronghold and Holding makes in a turn, bowed or not: what next turn's purchases will have.</summary>
    private static int[] CapacitySources(Ctx x) =>
        new[] { CapacityOf(x.Self.Stronghold, x.Self.Clan) }.Concat(x.Self.Home.Select(c => CapacityOf(c, x.Self.Clan))).Where(g => g > 0).OrderByDescending(g => g).ToArray();

    /// <summary>Strange Assembly, "When to Flush": flush every unbought card you cannot afford, keep what you can buy next turn; each card kept is one fewer new card seen. The first discard rule kept every card that
    /// was affordable ALONE, so a row of four 5-cost cards stayed whole against Gold for one. This keeps the best-valued set the Gold can pay for together (the purchase slate against next turn's Gold) and flushes the rest.</summary>
    private static double PlanDiscardScore2(Ctx x, GameCommand command)
    {
        const double Discard = 20, Keep = -5;
        if (ProvinceCards(x).All(c => c.Id != command.CardId)) return Keep; // face-down: unknown, kept
        var intent = x.Plan?.Intent ?? PlanIntent.Develop;
        return Slate(x, intent, CapacitySources(x)).Contains(command.CardId) ? Keep : Discard;
    }

    // ---- R2a / R2b / R3 declaring the attack --------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The attack declaration under the planner. Full (the Contender with attack on): the swing estimate decides alone. AttackAddOnly: the estimate may add an attack the champion would not make, never cancel one.
    /// AttackVetoOnly: it may cancel the champion's attack, never add one. HonorGuard (Strange Assembly, "Offense"): once the opponent is at 30 Honor or more (below the 40 the champion already treats as a race), a failed
    /// attack can hand them the game, so attack only when the estimate wins by a margin.</summary>
    private static double DeclareScore(Ctx x, TurnPlan plan, double score)
    {
        var m = x.Memory; var estimate = plan.Attack;
        var result = score;
        if (m.PlannerAttack)
        {
            var estimated = estimate is null ? -50 : 150 + Math.Clamp(estimate.Estimate.Net, 0, 50);
            if (m.AttackAddOnly) { if (estimate is not null) result = Math.Max(score, estimated); }
            else if (m.AttackVetoOnly) { if (estimate is null) result = -50; }
            else result = estimated;
        }
        if (m.HonorGuard && x.Opponent.Honor is >= 30 and < 40 && (estimate is null || estimate.Estimate.Net < 3)) result = Math.Min(result, -50);
        return result;
    }

    // ---- R4 presence denial -----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Strange Assembly, "Tempo": without presence the opponent can never take tempo from you; and (the interview) a Personality that is the only possible defender, bowed in the Action phase, means no defence and
    /// no Imperial Favor use. In the Action phase, when the target of a bow or destroy is the opponent's only unbowed Personality at home and the bot's army beats a Province once it is gone, play it.</summary>
    private static double? PresenceDenialScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (!x.Memory.PresenceDenial || x.View.Phase != Phase.Action || action.Type is not ("useAbility" or "playStrategy")) return null;
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        if (intent.Rank < EffectRank.Bow || intent.Side != TargetSide.Enemy || x.Card(command.TargetId) is not { } target || target.OwnerId != x.Opponent.Id) return null;
        var defenders = x.Opponent.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        if (defenders.Length != 1 || defenders[0].Id != target.Id) return null;
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var own = units.Sum(u => UnitForce(x, u));
        var enemyLeft = ArmyForce(x.Opponent.Home.Where(h => h.Id != target.Id && h.AttachedTo != target.Id));
        return x.Opponent.Provinces.Any(p => !p.Destroyed && own > p.Strength + enemyLeft) ? Math.Max(score, 220) : null;
    }

    // ---- R5 do not spend on a lost fight -----------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Strange Assembly, "Defense" and "Tempo": when you cannot save the battle, let it go and keep your resources; and (the interview) never pass a battle a card could change. So Gold and cards are held back only
    /// when even the whole hand, as far as the Gold pays, cannot turn the battle; a free printed ability is still used, and the bot's last Province is always defended with everything.</summary>
    private static double HopelessScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (!x.Memory.NoSpendLost || x.Odds is not { } odds || action.Type is not ("useAbility" or "playStrategy" or "equip")) return score;
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        if (intent.Negates) return score;
        var source = x.Card(command.CardId);
        var inPlay = source is not null && source.OwnerId == x.Self.Id && source.Zone != Zone.Resolving && !x.Self.Hand.Any(h => h.Id == source.Id);
        if ((action.Gold?.Cost ?? 0) <= 0 && inPlay) return score;
        if (!odds.IsAttacker && x.Self.Provinces.Count(p => !p.Destroyed) <= 1) return score;
        var enemy = EnemyArmyAt(x).ToArray();
        var units = enemy.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).Select(u => ArmyForce(enemy.Where(h => h.Id == u.Id || h.AttachedTo == u.Id))).ToArray();
        var best = odds.Own + ChooseTricks(HandTricks(x), Ready(x), units).Gains.Sum();
        return (odds.IsAttacker ? best <= odds.Enemy : best < odds.Enemy) ? Math.Min(score, BarredScore) : score;
    }
}
