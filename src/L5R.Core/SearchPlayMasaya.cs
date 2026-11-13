namespace L5R.Core;

// ======================================================================================================================================================================================
// MASAYA (oracle-l5r-4853): "Open: If Masaya is face-up in one of your provinces: Bring him into play, paying all costs and paying 2 less Gold. Before the turn ends, put him on top of your
// Dynasty deck."
//
// Mendon: "Masaya has a Reserve-like ability that allows the player to recruit him during the Action Phase as an Open action." It is Rise of the Dark Naga's province recruit (the target is the
// face-up province card; the shared recruit entry sequence: his entry price, the entry windows, a Proclaim eligible quote, the province refilled) taken by the card itself:
//   * The action's own Gold cost is the ordinary recruit price less 2, never below 0 (GoldCostRule.TargetRecruit(-2, 0)); the other costs of entering play (an Honor Requirement, Unique, an additional
//     entry cost) are the ones QuotePurchase applies to every recruit. It is an Open action: the Action Phase only (R-030) -- not the Battle Phase, where the Reserve recruit is taken.
//   * "Before the turn ends, put him on top of your Dynasty deck" is a debt paid at the top of EndTurn (SearchPlayReturns, beside the other turn-end checks): he is put on top of
//     his owner's Dynasty deck, face down, and his unit leaves play with him (his attachments go on top of their owners' Fate decks, as Ninube Shiho's does). Like the shared delay store's effects it is
//     re-validated when its turn comes: a Masaya who has left play meanwhile (destroyed, discarded, sent to a deck) has nothing to put on top, and the effect is negated (most conservative reading).
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    // The action's effect: he enters play at home, the province refills, and his return is scheduled. True when an entry window opened.
    private bool SearchPlayMasayaRecruit(PlayerState player, CardInstance masaya, GameCommand command)
    {
        var opened = RecruitFromProvince(player, masaya, proclaimResumesTo: 1 - player.Id, clanDiscount: command.ClanDiscount, clanAllegiance: command.ClanAllegiance); // P21 (ZoneExtensions.cs): the province recruit
        ScheduleToDeck(masaya, "Top", DelayUntil.EndOfTurn, player.Id, "Masaya returns to the top of the Dynasty deck"); // P21: the to-deck delay
        return opened;
    }

    // The top of EndTurn: each Personality owed to the top of his owner's Dynasty deck goes there if he is still in play, with his unit (his attachments go on top of their owners' Fate decks, as
    // Ninube Shiho's do). One that left play meanwhile has nothing to put on top, and the debt is dropped (most conservative reading).
    private void ResolveSearchPlayTurnEnd()
    {
        if (_state.SearchPlayReturns.Count == 0) return;
        var due = _state.SearchPlayReturns.ToArray();
        _state.SearchPlayReturns.Clear();
        foreach (var owed in due)
        {
            if (!_state.Cards.TryGetValue(owed.CardId, out var card)) continue;
            if (!InPlay(card) || Definition(card).Type != CardType.Personality)
            {
                Emit("delayedEffectNegated", $"{Definition(card).Name} is no longer in play, so he is not put on top of the Dynasty deck.", card.OwnerId, card.Id);
                continue;
            }
            Emit("delayedEffectApplied", $"{Definition(card).Name} is put on top of {Player(card.OwnerId).Name}'s Dynasty deck, as owed.", card.OwnerId, card.Id);
            foreach (var attachment in Attachments(card.Id).ToArray()) ToOwnerDeck(attachment, "Top");
            ToOwnerDeck(card, "Top");
        }
    }

    internal void SearchPlayTurnEndForTest() => ResolveSearchPlayTurnEnd();

    // Masaya's province-source rule is the generic AbilityDefinition.FromProvince (ZoneExtensions.cs); the debt is the generic DelayedKinds.ToDeck. The legacy SearchPlayReturns above is still drained
    // so a match saved before P21 resolves exactly as before.
}
