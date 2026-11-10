using L5R.Core;
namespace L5R.AI;

/// <summary>Small practice policy. Its only inputs are this seat's permitted snapshot and legal commands.</summary>
public static class DuelOpponent
{
    public static GameCommand? Choose(MatchView view, ActionOption[] actions)
    {
        var legal = actions.Where(a => a.Enabled && a.Command is not null).ToArray();
        GameCommand? First(string type) => legal.FirstOrDefault(a => a.Type == type)?.Command;
        if (view.PendingChoice?.Type == "focusEffectChoice")
            return legal.FirstOrDefault(a => a.Type == "chooseFocusEffect" && a.Command!.Choice == "decline")?.Command ?? First("chooseFocusEffect");
        var fromBelow = TrainingOpponent.ChallengedFromBelow(view); // challenged by a lower duel stat: accept, and Strike before anyone focuses
        if (fromBelow && view.PendingDuel is { Stage: "AwaitingAccept" } && First("acceptDuel") is { } accept) return accept;
        if (view.PendingDuel is { Stage: "Focusing" })
        {
            if (fromBelow && view.PendingDuel.TurnPlayerId == view.ViewerId && view.Players[view.ViewerId].FocusCount == 0 && First("strike") is { } now) return now;
            if (view.Players[view.ViewerId].FocusCount >= 2) return First("strike");
            var known = view.Players[view.ViewerId].Hand.Concat(view.Players[view.ViewerId].Discard).ToDictionary(c => c.Id);
            return legal.Where(a => a.Type == "focus" && a.Command!.CardId is not null)
                .OrderByDescending(a => known.GetValueOrDefault(a.Command!.CardId!)?.Definition?.Focus ?? -1)
                .FirstOrDefault()?.Command ?? First("focus") ?? First("strike");
        }
        if (view.PendingDuel is { Stage: "DuelistRefocus" })
        {
            var own = view.Players[view.ViewerId];
            var known = own.Hand.Concat(own.Focused ?? []).ToDictionary(c => c.Id, c => c.Definition?.Focus ?? 0);
            var improvement = legal.Where(a => a.Type == "duelistRefocus" && a.Command!.TargetId is not null)
                .Select(a => (Command: a.Command!, Gain: known.GetValueOrDefault(a.Command!.TargetId!) - known.GetValueOrDefault(a.Command.CardId!)))
                .OrderByDescending(x => x.Gain).FirstOrDefault();
            return improvement.Gain > 0 ? improvement.Command : legal.FirstOrDefault(a => a.Type == "duelistRefocus" && a.Command!.CardId is null)?.Command;
        }
        return First("acceptDuel") ?? First("declineReaction") ?? legal.FirstOrDefault(a => a.Type == "overconfidentDraw" && a.Command!.Choice == "decline")?.Command
            ?? First("orderDuelFocusEffects");
    }
}
