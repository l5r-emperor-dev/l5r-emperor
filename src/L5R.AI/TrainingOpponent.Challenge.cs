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
        return DuelStat(challenged, ability.DuelStat) > DuelStat(challenger, ability.DuelStat);
    }

    private static int DuelStat(CardView card, string stat) => stat switch
    {
        "Force" => card.Force,
        "PersonalHonor" => card.Dishonorable ? 0 : (card.Definition?.PersonalHonor ?? 0) + (card.Modifiers?.Where(m => m.Stat == "PersonalHonor").Sum(m => m.Amount) ?? 0),
        _ => card.Chi,
    };
}
