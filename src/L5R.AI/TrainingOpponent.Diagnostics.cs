using L5R.Core;

namespace L5R.AI;

/// <summary>Issue #1206 (battle records, docs/bots/CONAN_STRATEGY_BRIEF.md step 1): what the planner's attack estimate says about one attack, as numbers a harness can write beside the
/// battle's real result. Own and Enemy are Force on the table, the swings what each hand is estimated to change, Net their sum; Wins and Razes are the estimate's verdicts.</summary>
public sealed record AttackDiagnosis(int Own, int OwnSwing, int Enemy, int EnemySwing, int Strength, int Net, bool Wins, bool Razes);

public static partial class TrainingOpponent
{
    /// <summary>A harness diagnostic, never a decision: the estimate (<c>EstimateAttack</c>) for the attack of <paramref name="unitIds"/> on <paramref name="province"/>, from this seat's own
    /// view (its hand, its Gold, the opponent's public table). The same number <c>pattack</c> and <c>guard</c> would act on. Null when the Province is unknown.</summary>
    public static AttackDiagnosis? DiagnoseAttack(MatchView view, BotContext memory, IEnumerable<string> unitIds, int province)
    {
        var self = view.Players.Single(p => p.Id == view.ViewerId);
        var opponent = view.Players.Single(p => p.Id != view.ViewerId);
        if (opponent.Provinces.FirstOrDefault(p => p.Index == province && !p.Destroyed) is not { } target) return null;
        var x = new Ctx(view, self, opponent, BotProfile.Assess(self, memory.DeckAveragePersonalHonor), memory, AllCards(view));
        var units = unitIds.Select(x.Card).OfType<CardView>().ToArray();
        var e = EstimateAttack(x, units, target);
        return new(e.Own, e.OwnSwing, e.Enemy, e.EnemySwing, e.Strength, e.Net, e.Wins, e.Razes);
    }
}
