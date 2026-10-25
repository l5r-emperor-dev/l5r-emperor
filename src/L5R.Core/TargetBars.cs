namespace L5R.Core;

/// <summary>"May not be targeted by [x]" (issue #420, Mendon's ruling on Superior Reach): the card that carries it cannot be the target of any action the named card performs, until the end of
/// the turn with number <see cref="UntilTurn"/>, and only for as long as the condition holds (a keyword an attachment of the protected card has: "while your Kensai has a Weapon").</summary>
/// <param name="ActorCardId">The card whose actions may not target the protected card.</param>
/// <param name="UntilTurn">The number of the last turn it holds in.</param>
/// <param name="WhileAttachmentKeyword">When set, the bar holds only while the protected card has an attachment with this keyword.</param>
public sealed record TargetBar(string ActorCardId, int UntilTurn, string? WhileAttachmentKeyword = null);

// Superior Reach: "Reduce his Force to 0. While your Kensai has a Weapon, actions the Personality performs may not target your performer." Two readings settled with Mendon: it lasts until the
// end of the turn, and it depends on the Kensai's Weapon live -- the bar is checked every time an action is announced, so a Weapon lost mid-turn ends it and one gained brings it back.
// "An action the Personality performs" is the card that performs it in the engine's own sense (the same rule as PerformingCardForce): the performer the ability names, else the card it is on,
// and an Item or Spell's action is performed by the Personality it is attached to. Every card the action targets is judged (its target, an attack's card, each slot's cards); the check is
// made where an action is validated, so an action that the bar forbids is neither offered nor accepted, and costs nothing.
public sealed partial class MatchEngine
{
    private void AddTargetBar(CardInstance protectedCard, string actorCardId, string? whileAttachmentKeyword = null)
    {
        protectedCard.TargetBars.RemoveAll(b => b.ActorCardId == actorCardId && b.WhileAttachmentKeyword == whileAttachmentKeyword);
        protectedCard.TargetBars.Add(new(actorCardId, _state.TurnNumber, whileAttachmentKeyword));
        Emit("targetBarred", $"{Definition(Card(actorCardId)).Name}'s actions may not target {Definition(protectedCard).Name} this turn.", protectedCard.OwnerId, protectedCard.Id);
    }

    private bool TargetBarHolds(CardInstance protectedCard, TargetBar bar) =>
        bar.UntilTurn >= _state.TurnNumber && InPlay(protectedCard)
        && (bar.WhileAttachmentKeyword is null || Attachments(protectedCard.Id).Any(a => Keyword(a, bar.WhileAttachmentKeyword)));

    // The Personality (or Follower) that performs the action: the card the ability names as its performer, else the card it is on; an Item or Spell acts through its wielder.
    private CardInstance? PerformingCard(GameCommand command, AbilityDefinition ability)
    {
        var performerId = ability.Performer is not null && command.PerformerId is not null ? command.PerformerId : command.CardId;
        if (performerId is null || !_state.Cards.TryGetValue(performerId, out var performer)) return null;
        if (Definition(performer).Type is CardType.Item or CardType.Spell)
            return performer.Zone == Zone.Attached && performer.AttachedTo is string wielderId ? Card(wielderId) : null;
        return performer;
    }

    private void RequireNotBarredFromTargeting(GameCommand command, AbilityDefinition ability)
    {
        RequireNotBarredByLegionOfPain(command, ability); // Emperor Edition remaining-cards pass (issue #492), group G: Legion of Pain's static bar.
        RequireSuikihimeNotPoliticallyTargeted(command, ability); // Otomo Suikihime (issue #503, EmperorP6.cs).
        RequireRestrictionRulesAllow(command, ability); // P11 (CardRestrictions.cs): generalised bars, forced targets, spell limits.
        if (!_state.Cards.Values.Any(c => c.TargetBars.Count > 0)) return;
        var actor = PerformingCard(command, ability);
        if (actor is null) return;
        foreach (var targetId in BarredTargetChannelIds(command))
            if (_state.Cards.TryGetValue(targetId, out var target)) RequireTargetNotBarredForAction(actor, target, command, ability);
    }

    // Every channel a target can arrive on, not just TargetId: ActionTargetIds' own set (TargetId, Kahoku's
    // Attack target, each slot's cards) plus any other CardIds an ability carries its targets on (Visage of
    // the Orochi's bowed card, Scorn the Weak's and Strength in Terror's optional second selection). Ids that
    // name no card (Knowledge and Power's tokens, Mitsu's Fire tokens) are simply not found and skipped.
    private static IEnumerable<string> BarredTargetChannelIds(GameCommand command) =>
        ActionTargetIds(command).Concat(command.CardIds ?? []).Distinct(StringComparer.Ordinal);

    /// <summary>The one bar check: made wherever a target is chosen -- as the action is announced, later
    /// (a sequenced Attack target), or by redirection (AttackTargeting.RequireLegalAttackTarget).</summary>
    private void RequireTargetNotBarredForAction(CardInstance? actor, CardInstance target, GameCommand command, AbilityDefinition ability)
    {
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        foreach (var rule in BarRulesOn(target))
            if (!rule.Forced && ActorMatches(rule.Actor, actor, command.PlayerId, ability, source, target))
                RequireNotBarred(false, $"Targeting {Definition(target).Name} with this action", rule.Label);
        RequireTargetNotBarred(actor, target);
    }

    private void RequireTargetNotBarred(CardInstance? actor, CardInstance target)
    {
        if (actor is null) return;
        RequireTargetNotBarredByRules(actor, target); // P11 (CardRestrictions.cs)
        Require(!target.TargetBars.Any(b => b.ActorCardId == actor.Id && TargetBarHolds(target, b)),
            $"{Definition(target).Name} may not be targeted by {Definition(actor).Name}'s actions this turn.");
    }
}
