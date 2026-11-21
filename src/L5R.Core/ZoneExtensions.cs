namespace L5R.Core;

// Primitive P21 (issue #697, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): zone extensions.
//
//   CARDS UNDER A HOST     Zone.Underneath was one public pile under the owner's Stronghold (EmperorP5Zones.cs). A card may now sit under any card: CardInstance.HostCardId names the host, FaceUp says whether
//                          it is public. PutUnder / UnderCards are the helpers. A FACE-DOWN under-card is private: the other seat's view carries a redacted card (no definition, id "hidden-under-<host>-<n>"), the
//                          other seat's slot candidates never list it, and no event names it. Cards under a host that leaves play are discarded (the engine's one sweep, below). Equip from under: an
//                          AttachSpec with Pile = AttachPile.UnderHost attaches a card that sits under the ability's own source, paying the usual Equip price.
//   FROM A PROVINCE        AbilityDefinition.FromProvince: the ability is taken by its own card while that card is face up in its owner's province (AbilitySourceZoneAllows and the source list of AbilityActions
//                          honour it). RecruitFromProvince is the entry sequence such an action runs (Masaya, Kitsu Miwa, Yoritomo Mikaru): the card leaves its province, enters play (optionally bowed), the province is
//                          refilled and the entering windows open. The price is the ability's own Gold rule (GoldCostRule.TargetRecruit), never a term of this helper.
//   REFILL HOOK            EngineHooks.RefillFaceUp: Func<RefillContext, bool?> consulted by Refill; true makes that one refill face up. Oracle of the Void's "the next two refills this turn may be face up" is a
//                          card-owned count plus this hook; nothing else about refilling changes.
//   HISTORY                CardInstance.EnteredPlayTurn (stamped by the one sweep after every accepted command, from the turn the command began in; cleared when the card is not in play) and
//                          PlayerState.InPlayDuringLastTurn (the owner's Personalities in play when that owner's turn ended). HoldsNoDynasty(province) reads ProvinceState.HoldsNoDynastyCardsWhile (Kokure's Lands).
//   TO-DECK DELAY          DelayedKinds.ToDeck (Arg "Top" / "Bottom"): put in the delay store, applied by ToOwnerDeck; Masaya's "before the turn ends, put him on top of your Dynasty deck" is its first user.

/// <summary>What a refill is about to put in a province: the player, the province, and the command being resolved (null outside a command). Return true from a EngineHooks.RefillFaceUp callback to make it face up.</summary>
internal sealed record RefillContext(MatchEngine Engine, PlayerState Player, ProvinceState Province, GameCommand? Command);

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<RefillContext, bool?>> RefillFaceUp = new();
}

public sealed partial class MatchEngine
{
    // ---- cards under a host -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The cards that sit under <paramref name="hostId"/>, in a stable order.</summary>
    internal IEnumerable<CardInstance> UnderCards(string hostId) =>
        _state.Cards.Values.Where(c => c.Zone == Zone.Underneath && c.HostCardId == hostId).OrderBy(c => c.Id, StringComparer.Ordinal);

    /// <summary>Puts a card under a host, leaving whatever pile or slot it was in. A face-down card stays private to its owner. The log never names a face-down card.</summary>
    internal void PutUnder(CardInstance card, CardInstance host, bool faceUp)
    {
        var owner = Player(card.OwnerId);
        owner.Hand.Remove(card.Id); owner.FateDeck.Remove(card.Id); owner.DynastyDeck.Remove(card.Id);
        foreach (var province in owner.Provinces.Where(p => p.CardId == card.Id || p.CardId2 == card.Id))
            ClearProvinceSlot(province, province.CardId == card.Id ? OtosanSlotPrimary : OtosanSlotSecond);
        card.Zone = Zone.Underneath; card.HostCardId = host.Id; card.FaceUp = faceUp; card.ProvinceIndex = null; card.AttachedTo = null;
        Emit("putUnderneath", faceUp ? $"{owner.Name} put {Definition(card).Name} face-up underneath {Definition(host).Name}." : $"{owner.Name} put a card face-down underneath {Definition(host).Name}.", owner.Id, faceUp ? card.Id : null);
    }

    internal void PutUnder(string cardId, string hostId, bool faceUp) => PutUnder(Card(cardId), Card(hostId), faceUp);

    /// <summary>The under-card as a viewer may see it: its owner sees every one of theirs, anyone sees a face-up one, and a face-down card of the other seat is a redacted marker numbered under its host.</summary>
    private CardView ViewUnder(CardInstance card, int viewerId)
    {
        if (card.FaceUp || card.OwnerId == viewerId) return ViewCard(card);
        var number = card.HostCardId is null ? 0 : UnderCards(card.HostCardId).TakeWhile(c => c.Id != card.Id).Count();
        return new($"hidden-under-{card.HostCardId ?? "stronghold"}-{number}", null, card.OwnerId, Zone.Underneath, false, false, null, null, 0, UnderHostId: card.HostCardId);
    }

    // ---- an ability taken from a province -----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The source zone rule of a FromProvince ability: its own card, face up in its owner's province. Null for an ability that is not FromProvince.</summary>
    private static bool? ProvinceSourceAllows(AbilityDefinition ability, CardInstance source, PlayerState player) =>
        ability.FromProvince ? source.Zone == Zone.Province && source.FaceUp && source.OwnerId == player.Id : null;

    /// <summary>A face-up province card carrying at least one FromProvince action (an extra source of AbilityActions).</summary>
    private static bool HasProvinceAction(CardInstance card) =>
        card.Zone == Zone.Province && card.FaceUp && (Definition(card).Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var ability) && ability.FromProvince);

    /// <summary>"Recruit a card in your province": the entry sequence of an action taken from a province. The card leaves its province, enters play at home (or the given battlefield), bowed if asked, the
    /// province is refilled and the entering windows open (returns true when one did). The Gold is the ability's own rule (GoldCostRule.TargetRecruit(modifier, floor)), already paid.</summary>
    internal bool RecruitFromProvince(PlayerState player, CardInstance card, bool bowed = false, int? battlefield = null, int? proclaimResumesTo = null, bool clanDiscount = false, bool clanAllegiance = false)
    {
        var quote = QuotePurchase(player.Id, card.Id, clanDiscount, clanAllegiance); // issue #1232: the Clan discount / allegiance the action's command chose (its Proclaim eligibility too)
        Require(quote.Error is null, quote.Error ?? "");
        var opened = Definition(card).Type == CardType.Personality
            ? BringProvincePersonalityIntoPlay(player, card, quote, battlefield, Definition(card).Name, proclaimResumesTo)
            : BringProvinceCardIntoPlay(player, card, quote, bowed);
        if (bowed && InPlay(card)) card.Bowed = true;
        return opened;
    }

    internal bool RecruitFromProvince(int playerId, string cardId, bool bowed = false, int? battlefield = null) => RecruitFromProvince(Player(playerId), Card(cardId), bowed, battlefield);

    // A province Holding (or other non-Personality) recruited by an action: the same sequence without a battlefield or a Proclaim.
    private bool BringProvinceCardIntoPlay(PlayerState player, CardInstance card, PurchaseQuote quote, bool bowed)
    {
        var (province, slot) = FindProvinceSlot(player, card.Id); // either slot of a two-slot province (bot soak, issue #768)
        ClearProvinceSlot(province, slot);
        _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = province.Index;
        EnterPlayFromRecruit(player, card, null, Definition(card).Name);
        RefillProvinceSlot(player, province, slot);
        return OpenRecruitEntryWindows(player, card, false, null);
    }

    // ---- the refill hook -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool RefillWantsFaceUp(PlayerState player, ProvinceState province)
    {
        var context = new RefillContext(this, player, province, _runningCommand);
        foreach (var hook in EngineHooks.RefillFaceUp.Items)
            if (hook(context) is bool faceUp) return faceUp;
        return false;
    }

    // ---- history and province locks -----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"This province holds no Dynasty cards": the permanent flag (Illusory Defense) or the while-a-card-is-in-play one (Kokure's Lands).</summary>
    // Kokure's Lands (issue #739, Wave739.cs) is read from the Region the province carries.
    internal bool HoldsNoDynasty(ProvinceState province) =>
        province.HoldsNoDynastyCards || province.HoldsNoDynastyCardsWhile is { } whileId && _state.Cards.TryGetValue(whileId, out var holder) && InPlay(holder) || Wave739KokureHolds(province);

    /// <summary>The one sweep after every accepted command: a card now in play that has no entry turn gets <paramref name="turnAtStart"/>; a card not in play loses it; a card whose host left play is discarded.</summary>
    private void SweepZoneHistory(int turnAtStart)
    {
        foreach (var card in _state.Cards.Values)
        {
            if (InPlay(card)) { card.EnteredPlayTurn ??= turnAtStart; }
            else if (card.EnteredPlayTurn is not null) { card.EnteredPlayTurn = null; NoteLeftPlay(card); } // P6 (AfterFacts.cs): "after X leaves play"
            if (card.Zone == Zone.Underneath && card.HostCardId is { } hostId && !(_state.Cards.TryGetValue(hostId, out var host) && InPlay(host)))
            {
                card.Zone = Zone.FateDiscard; card.HostCardId = null; card.FaceUp = true;
                Emit("underDiscarded", $"{Definition(card).Name} was discarded: the card it was under left play.", card.OwnerId, card.Id);
            }
        }
    }

    /// <summary>At the end of a player's turn: which of their Personalities are in play ("in play during your last turn").</summary>
    private void CaptureInPlayDuringTurn()
    {
        var player = Player(_state.ActivePlayerId);
        player.InPlayDuringLastTurn = _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>True when the card was in play at the end of its owner's last turn.</summary>
    internal bool WasInPlayDuringLastTurn(CardInstance card) => Player(card.OwnerId).InPlayDuringLastTurn?.Contains(card.Id) == true;

    // ---- the to-deck delay ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Applied from ApplyDelayedEffect: the card (a Personality with its unit: its attachments go on top of their owners' Fate decks) is put on its owner's deck at the position the effect names.
    private void ApplyDelayedToDeck(DelayedEffect effect, CardInstance card)
    {
        var position = effect.Arg == "Bottom" ? "Bottom" : "Top";
        Emit("delayedEffectApplied", $"{Definition(card).Name} is put on {position.ToLowerInvariant()} of {Player(card.OwnerId).Name}'s deck, as owed.", card.OwnerId, card.Id);
        foreach (var attachment in Attachments(card.Id).ToArray()) ToOwnerDeck(attachment, position);
        ToOwnerDeck(card, position);
    }

    /// <summary>Schedules "put this card on top / the bottom of its owner's deck" through the delay store (DelayedKinds.ToDeck); <paramref name="until"/> is a DelayUntil point (end of turn for Masaya).</summary>
    internal void ScheduleToDeck(CardInstance card, string position, string until, int? playerId, string reason) =>
        ScheduleDelayedEffect(card, DelayedKinds.ToDeck, until, playerId, reason, sourceId: card.Id, arg: position == "Bottom" ? "Bottom" : "Top");

    internal void SweepZoneHistoryForTest() => SweepZoneHistory(_state.TurnNumber);
    internal void CaptureInPlayDuringTurnForTest() => CaptureInPlayDuringTurn();
    internal void RefillForTest(int playerId, int provinceIndex) => Refill(Player(playerId), Player(playerId).Provinces[provinceIndex]);
}
