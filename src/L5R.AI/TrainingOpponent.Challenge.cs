using L5R.Core;

namespace L5R.AI;

public static partial class TrainingOpponent
{
    /// <summary>True when the action would challenge an enemy Personality whose duel stat is higher than the challenger's. A tie is still allowed. The stat is the one the ability prints (Chi, Force or Personal Honor); what is
    /// read is the current value on the table, so Items and other modifiers are not counted here beyond what the view already shows.</summary>
    private static bool LosingChallenge(Ctx x, ActionOption action, GameCommand command)
    {
        if (!x.Memory.ChallengeGuard || action.Type != "useAbility" || command.TargetId is null || command.AbilityId is null
            || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || !ability.IssuesDuel) return false;
        var challenger = x.Card(command.PerformerId ?? command.CardId);
        var challenged = x.Card(command.TargetId);
        if (challenger is null || challenged is null || challenger.OwnerId == challenged.OwnerId) return false;
        var (mine, theirs) = (DuelStat(challenger, ability.DuelStat), DuelStat(challenged, ability.DuelStat));
        // A Duelist wins a tie against a non-Duelist (Mendon), so that is the one tie worth challenging on.
        return theirs > mine || theirs == mine && !(IsDuelist(challenger) && !IsDuelist(challenged));
    }

    /// <summary>True when this seat is the challenged one in a pending duel and the challenger's duel stat is strictly lower than its own Personality's.</summary>
    internal static bool ChallengedFromBelow(MatchView view)
    {
        if (view.PendingDuel is not { } duel || duel.ChallengedPlayerId != view.ViewerId || view.WinnerId is not null) return false;
        var cards = AllCards(view);
        return cards.TryGetValue(duel.ChallengerId, out var challenger) && cards.TryGetValue(duel.ChallengedId, out var challenged)
            && DuelStat(challenger, duel.Stat) < DuelStat(challenged, duel.Stat);
    }

    /// <summary>Challenged by a Personality with a lower duel stat: accept, never refuse, and Strike at the first opportunity (before either side focuses), so the challenger gets no focus to close the gap. Null when this is not that case.</summary>
    private static double? DuelReply(Ctx x, ActionOption action)
    {
        if (!ChallengedFromBelow(x.View)) return null;
        var duel = x.View.PendingDuel!;
        if (action.Type == "acceptDuel") return StrikeNowScore;
        if (action.Type is "declineDuel" or "bowToRefuseDuel") return BarredScore;
        if (action.Type == "strike" && duel.Stage == "Focusing" && duel.TurnPlayerId == x.Self.Id && x.Self.FocusCount == 0) return StrikeNowScore;
        return null;
    }
    private const double StrikeNowScore = 320;

    private static int DuelStat(CardView card, string stat) => stat switch
    {
        "Force" => card.Force,
        "PersonalHonor" => card.Dishonorable ? 0 : (card.Definition?.PersonalHonor ?? 0) + (card.Modifiers?.Where(m => m.Stat == "PersonalHonor").Sum(m => m.Amount) ?? 0),
        _ => card.Chi,
    };
}
