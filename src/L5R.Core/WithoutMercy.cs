namespace L5R.Core;

// Without Mercy (Torn Asunder, oracle-l5r-10405, issue #648): "Reaction: After a Battle action performed by your Samurai Personality resolves, if the action did not target an enemy card:
// Take an additional Battle action."
//
// It rides the after-action-resolved Reaction window (ActionEffectTracking.cs), which already offers a Strategy in hand to its owner after any action. What the window did not know is
// what the action TARGETED, so ResolvedActionFacts.TargetIds records it (the command's named targets, a duel's challenged Personality, an Attack's target card; an Attack at a province
// names no card). "An enemy card" is a card another player owns. The window and its eligibility are decided here; the additional Battle action is the ordinary grant every other card makes.
//
// Reading, for confirmation: an action that targets nothing (a Battle Open ability with no target, a province Attack, a Strategy that affects "all") did not target an enemy card, so the
// Reaction applies; an action whose only targets are the performer's own cards applies too.
public static class WithoutMercyAbilities
{
    public const string TakeAction = "without-mercy-action";

    public static Dictionary<string, AbilityDefinition> AddWithoutMercy(this Dictionary<string, AbilityDefinition> all)
    {
        all[TakeAction] = new("Reaction", "After a Battle action performed by your Samurai Personality resolves, if the action did not target an enemy card: Take an additional Battle action", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // The window's own condition, read by AfterActionResolvedApplies. Null: not this card's Reaction.
    private bool? WithoutMercyApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (abilityId != WithoutMercyAbilities.TakeAction) return null;
        if (facts.PlayerId != playerId || !IsBattleAction(facts.AbilityId) || facts.PerformerId is not { } performerId || !_state.Cards.TryGetValue(performerId, out var performer)) return false;
        if (performer.OwnerId != playerId || Definition(performer).Type != CardType.Personality || !Keyword(performer, "Samurai")) return false;
        return !facts.TargetIds.Any(id => _state.Cards.TryGetValue(id, out var targeted) && targeted.OwnerId != playerId);
    }

    // The effect: the ordinary additional Battle action grant. Returns false so the Strategy finishes the way every other Reaction does.
    private bool? RunWithoutMercy(string id, PlayerState player)
    {
        if (id != WithoutMercyAbilities.TakeAction) return null;
        GrantAdditionalAction(player.Id, null);
        return false;
    }

    // The after-action window closes back to whoever the action had handed priority to, which was fixed before any Reaction was played. An additional Battle action granted INSIDE the
    // window belongs to the player whose action it was, so priority goes back to them to take it (otherwise the opponent would act first and the grant would lapse).
    private int AfterActionWindowResumeTo(PendingChoice pending, int resumeTo)
    {
        // The same goes for an entering-play window in a battle (Bayushi Wateru's Interrupt, issue #1150): the additional Battle action it grants is the recruiter's to take next.
        if (pending.Type == "entryReaction" && _state.Phase == Phase.Battle && pending.CardId is { } entered && _state.Cards.TryGetValue(entered, out var enteredCard)
            && _state.AdditionalActions.Any(g => g.PlayerId == enteredCard.OwnerId && Forfeitable(g) && GrantUsableNow(g))) return enteredCard.OwnerId;
        if (pending.Type != "afterActionResolvedReaction" || _state.LastResolvedAction is not { } facts) return resumeTo;
        return _state.AdditionalActions.Any(g => g.PlayerId == facts.PlayerId && Forfeitable(g) && GrantUsableNow(g)) ? facts.PlayerId : resumeTo;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWithoutMercyHooks()
    {
        EngineHooks.Abilities.Register("AddWithoutMercy", 11900, static abilities => abilities.AddWithoutMercy());
        EngineHooks.RunEffects.Register("RunWithoutMercy", 11900, static (engine, context) => engine.RunWithoutMercy(context.id, context.player));
    }
}
