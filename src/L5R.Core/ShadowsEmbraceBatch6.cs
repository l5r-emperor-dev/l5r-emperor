namespace L5R.Core;

// The Shadow's Embrace pass (issue #476), batch 6: the primitives of holding-wave-12, follower-wave-30, event-wave-7, stronghold-wave-16 and celestial-wave-7.
// The shared engine files carry short, commented hooks into this file (each names its card); the catalog entries, Target and Performer categories live in
// CardAbilities.cs beside every other ability's, as they must (one dictionary literal, one switch each). The deck-search extensions (a Fate discard pile as a
// searched place, a recruit-unbowed search effect, excluded titles) are in DeckSearch.cs; their payment stage is here.
public sealed partial class MatchEngine
{
    // ---- Effect keys --------------------------------------------------------------------------------------------------------------------------------
    internal const string PlatinumMineEffect = "platinum-mine-gold";
    internal const string ShelteredPortEffect = "sheltered-port-gold";
    internal const string WhisperingArchiveEffect = "whispering-archive-spell-discount";
    internal const string ClearingCrewEffect = "clearing-crew-draw";
    internal const string ShieldmanEffect = "shieldman";
    internal const string DiscoveringTheTempleEffect = "discovering-the-temple";
    internal const string OnEffect = "on-honor-limit";
    internal const string WartimePreparationsEffect = "wartime-preparations";
    internal const string StarCrossedLoversEffect = "star-crossed-lovers";
    internal const string TwinForksCityEffect = "twin-forks-city";
    internal const string FortressOfLidlessEyesTitle = "Fortress of Lidless Eyes";

    // ---- Gold production (GoldProduced's hook) ------------------------------------------------------------------------------------------------------
    // The Moon's Imperative / The Sun's Aspiration: "It produces 1 less (more) Gold when it bows to produce Gold" -- a GoldProduction modifier on the target Holding for the turn
    // (no duration is printed: the rulebook's default), counted on whatever it produces. Sheltered Port: "Produce 2 Gold, plus 1 Gold for each other Holding you control that is a
    // Market or Port" -- read live when it is asked what it produces.
    private int ShadowsEmbraceBatch6Production(CardInstance card, int playerId)
    {
        var extra = StatModifier(card, "GoldProduction");
        if (Definition(card).Effect == ShelteredPortEffect)
            extra += _state.Cards.Values.Count(c => c.Id != card.Id && c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Holding && (Keyword(c, "Market") || Keyword(c, "Port")));
        return extra;
    }

    // ---- Whispering Archive: "Your Spells enter play paying 1 less Gold." (EquipGoldCost's term; every Spell entry that pays is priced there) ------------------
    private int WhisperingArchiveDiscount(CardInstance entering, int playerId) =>
        Definition(entering).Type == CardType.Spell ? _state.Cards.Values.Count(c => ProducerControlledBy(c, playerId) && InPlay(c) && Definition(c).Effect == WhisperingArchiveEffect) /* issue #732: Black Riders lends the traits */ : 0;

    // ---- Clearing Crew: "After this card enters play from your hand: Draw an additional card during your next End Phase." ---------------------------------
    // A trait (no designator), so mandatory: ApplyMandatoryEntryEffects' hook. An attach from a discard pile (ExecuteAttach) is not an entry from the hand; it marks itself here.
    private string? _attachingFromPileId;

    private void ApplyShadowsEmbraceBatch6EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect == ClearingCrewEffect && card.Id != _attachingFromPileId && Definition(card).Type == CardType.Follower)
            QueueExtraEndPhaseDraws(Player(playerId), 1, Definition(card).Name);
    }

    // ---- Shieldman --------------------------------------------------------------------------------------------------------------------------------------
    // "This Personality may not be assigned to, and will not move into, an attacking army." Printed on a Follower, "this Personality" is the one it is attached to: his unit.
    private const string ShieldmanBars = "Assigning to attack or moving into an attacking army";
    private string ShieldmanName(CardInstance personality) =>
        Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Follower && Definition(a).Effect == ShieldmanEffect).Select(a => Definition(a).Name).FirstOrDefault() ?? "Shieldman";

    private bool UnitHasShieldman(CardInstance personality) =>
        Attachments(personality.Id).Any(a => Definition(a).Type == CardType.Follower && Definition(a).Effect == ShieldmanEffect);

    // Assign's hook: an attacker's assignment only (a Defender's army is never an attacking army).
    private void RequireShieldmanMayAssign(CardInstance personality, bool isDefender) =>
        RequireNotBarred(isDefender || !UnitHasShieldman(personality), ShieldmanBars, ShieldmanName(personality));

    // MoveToBattlefield's hook: a move to a battlefield puts the unit on its controller's side there, which is the attacking army when its controller is the Attacker.
    private bool ShieldmanNegatesMove(CardInstance personality)
    {
        if (!UnitHasShieldman(personality) || personality.OwnerId != _state.ActivePlayerId || _state.CurrentBattleProvince is null && _state.UnresolvedBattlefields.Count == 0) return false;
        Emit("moveNegated", $"{Definition(personality).Name} will not move into an attacking army (Shieldman).", personality.OwnerId, personality.Id);
        EmitBarred(personality.OwnerId, ShieldmanBars, ShieldmanName(personality), personality.Id);
        return true;
    }

    // "Reaction: After another player's action resolves which destroyed another card in this unit: Gain 2 Honor." (AfterActionResolvedApplies' arm.) The Personality leading
    // the unit taking the whole unit with him, the cards that can be destroyed while Shieldman stays are the unit's other attachments: their host is recorded as they go.
    private bool ShieldmanReactionApplies(CardInstance shieldman, int playerId) =>
        _state.LastResolvedAction is { } facts && facts.PlayerId != playerId && shieldman.Zone == Zone.Attached && shieldman.AttachedTo is { } host
        && facts.Affected.Any(a => a.Kind == DestroyedKind && a.CardId != shieldman.Id && a.HostId == host);

    // ---- Fortress of Lidless Eyes ------------------------------------------------------------------------------------------------------------------------
    // "All players have the ability, 'Open: Pay 6 Gold: Destroy a card titled Fortress of Lidless Eyes.'" -- a player ability that exists only while such a card is in play
    // (PlayerAbilityDormant's hook, Assault in the Jungle's shape).
    private bool FortressOfLidlessEyesInPlay() => _state.Cards.Values.Any(c => InPlay(c) && Definition(c).Name == FortressOfLidlessEyesTitle);

    private bool ShadowsEmbraceBatch6PlayerAbilityDormant(string abilityId) => abilityId == "fortress-lidless-destroy" && !FortressOfLidlessEyesInPlay();

    // ---- Wartime Preparations ------------------------------------------------------------------------------------------------------------------------------
    // "Until the game ends, before the second or later time each turn another player's card's effect bows any of your Personalities during the Action Phase, negate that
    // effect, and before the second or later time each turn another player's card's effect prevents any of your Personalities from assigning, negate that effect." The Event
    // stays in Zone.EventInPlay for good; its two per-turn counts are kept on the card (turn-stamped, so a new turn starts them again).
    private const string WartimeBowsKey = "wartime-preparations:bows", WartimeAssignKey = "wartime-preparations:assign", WartimeTurnKey = "wartime-preparations:turn";

    private CardInstance? WartimePreparationsOf(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.EventInPlay && Definition(c).Effect == WartimePreparationsEffect).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

    // Counts one more time and says whether it is the second or later this turn.
    private static bool WartimeSecondOrLater(CardInstance wartime, string key, int turn)
    {
        if (!wartime.AbilityUses.TryGetValue(WartimeTurnKey, out var stamped) || stamped != turn)
        {
            wartime.AbilityUses[WartimeTurnKey] = turn;
            wartime.AbilityUses[WartimeBowsKey] = 0;
            wartime.AbilityUses[WartimeAssignKey] = 0;
        }
        wartime.AbilityUses[key] = wartime.AbilityUses.GetValueOrDefault(key) + 1;
        return wartime.AbilityUses[key] >= 2;
    }

    // StalwartNegatesBow's hook (the one place an effect of another player's card bows a card): a Personality of the Event's owner, during the Action Phase.
    private bool WartimePreparationsNegatesBow(CardInstance card, int actingPlayerId)
    {
        if (_state.Phase != Phase.Action || actingPlayerId == card.OwnerId || Definition(card).Type != CardType.Personality || !InPlay(card)) return false;
        if (WartimePreparationsOf(card.OwnerId) is not { } wartime || !WartimeSecondOrLater(wartime, WartimeBowsKey, _state.TurnNumber)) return false;
        Emit("bowNegated", $"{Definition(card).Name}'s bowing was negated (Wartime Preparations: another player's second bowing effect this turn).", card.OwnerId, card.Id);
        return true;
    }

    // LockMovement's hook: a lock that prevents assigning, from another player's card (the lock's source card's controller, else the player whose action is resolving).
    private bool WartimePreparationsNegatesAssignLock(CardInstance card, string source, MovementKinds kinds)
    {
        if ((kinds & (MovementKinds.Assign | MovementKinds.AssignAttack)) == 0 || Definition(card).Type != CardType.Personality || !InPlay(card)) return false;
        int? actor = _state.Cards.TryGetValue(source, out var sourceCard) ? sourceCard.OwnerId : _runningCommand?.PlayerId;
        if (actor is not int acting || acting == card.OwnerId) return false;
        if (WartimePreparationsOf(card.OwnerId) is not { } wartime || !WartimeSecondOrLater(wartime, WartimeAssignKey, _state.TurnNumber)) return false;
        Emit("effectNegated", $"The effect preventing {Definition(card).Name} from assigning was negated (Wartime Preparations: another player's second such effect this turn).", card.OwnerId, card.Id);
        return true;
    }

    // ---- On ------------------------------------------------------------------------------------------------------------------------------------------------
    // "Until the game ends, at the end of each phase, lower each player's Honor to what it was at the start of that phase plus 3 plus any Honor he gained from battle
    // resolution that phase; this Honor loss will not be negated, substituted or reduced." The engine's phases are the rulebook's, with the Attack Phase split into its
    // segments: the whole Attack-declaration-through-battle-resolution stretch is one phase (the War Weariness convention), the Dynasty Phase's discard step is part of it.
    // The Honor each player had at the start of the phase in progress is recorded at every phase boundary (SetPhase's hook) -- always, so that an On revealed during a
    // phase already knows it -- and the Honor each gained from a battle's resolution is added up as it is gained (GainHonor's hook).
    private static int RulesPhase(Phase phase) => phase switch
    {
        Phase.Straighten => 0,
        Phase.Events => 1,
        Phase.Action => 2,
        Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle => 3,
        Phase.Dynasty or Phase.DynastyDiscard => 4,
        Phase.End => 5,
        _ => 6
    };

    private void OnPhaseBoundary(Phase from, Phase to)
    {
        if (RulesPhase(from) == RulesPhase(to)) return;
        if (_state.PhaseStartHonor is { Length: 2 } start && _state.Cards.Values.Any(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == OnEffect))
            foreach (var player in _state.Players)
            {
                var ceiling = start[player.Id] + 3 + _state.PhaseBattleHonor[player.Id];
                if (player.Honor <= ceiling) continue;
                var lost = player.Honor - ceiling;
                // "Will not be negated, substituted or reduced": the Honor is set, not routed through GainHonor's negations and reductions.
                player.Honor = ceiling;
                RecordHonorLoss(player.Id);
                if (player.Honor <= -20) player.DishonorLossPending = true;
                Emit("honorChanged", $"{player.Name}: -{lost} Honor (On: no more than 3 Honor gained this phase outside battle resolution).", player.Id, Amount: -lost);
                CheckFormalApology(player.Id);
            }
        _state.PhaseStartHonor = _state.Players.Select(p => p.Honor).ToArray();
        _state.PhaseBattleHonor = new int[2];
    }

    // GainHonor's hook: the Honor a battle's own resolution gave a player ("Battle victory", "Tied battle").
    private void NoteBattleResolutionHonor(int playerId, int amount, string reason)
    {
        if (amount > 0 && reason is "Battle victory" or "Tied battle") _state.PhaseBattleHonor[playerId] += amount;
    }

    // ---- Star-Crossed Lovers -----------------------------------------------------------------------------------------------------------------------------
    // "Choose two Personalities controlled by the same player. Until the game ends, they may not be assigned, and will not move, to each other's battlefields." The choice is
    // the revealing player's, made in the reveal (Offering Reverence's shape); with no player controlling two Personalities there is nothing to choose and the Event is
    // simply discarded. The pair is kept on the match (MatchState.StarCrossedPairs) for the rest of the game; the Event is discarded once chosen.
    private IEnumerable<(CardInstance First, CardInstance Second)> StarCrossedCandidatePairs()
    {
        foreach (var owner in _state.Players)
        {
            var personalities = _state.Cards.Values.Where(c => c.OwnerId == owner.Id && InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < personalities.Length; i++)
                for (var j = i + 1; j < personalities.Length; j++) yield return (personalities[i], personalities[j]);
        }
    }

    private bool ResolveStarCrossedLovers(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (!StarCrossedCandidatePairs().Any())
        {
            Emit("eventResolved", $"{Definition(card).Name}: no player controls two Personalities, so nothing is chosen.", player.Id, card.Id);
            province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
            Refill(player, province);
            return false;
        }
        _state.PendingChoice = new("starCrossedLovers", player.Id, card.Id, "Star-Crossed Lovers: choose two Personalities controlled by the same player.");
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        Emit("eventChoice", $"{Definition(card).Name}: {player.Name} chooses two Personalities controlled by the same player.", player.Id, card.Id);
        return true;
    }

    private void ResolveStarCrossedLoversChoice(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "starCrossedLovers" && command.PlayerId == pending.PlayerId && command.CardIds is { Length: 2 }, "Choose two Personalities controlled by the same player.");
        var first = Card(command.CardIds![0]); var second = Card(command.CardIds[1]);
        Require(first.Id != second.Id && StarCrossedCandidatePairs().Any(p => p.First.Id == first.Id && p.Second.Id == second.Id || p.First.Id == second.Id && p.Second.Id == first.Id),
            "Choose two different Personalities in play controlled by the same player.");
        var card = Card(pending.CardId!);
        var player = Player(pending.PlayerId);
        var province = player.Provinces[card.ProvinceIndex!.Value];
        _state.PendingChoice = null;
        _state.StarCrossedPairs.Add([first.Id, second.Id]);
        Emit("eventResolved", $"{Definition(card).Name}: {Definition(first).Name} and {Definition(second).Name} may not be assigned, and will not move, to each other's battlefields until the game ends.", player.Id, card.Id);
        province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
        Refill(player, province);
        ContinueEventsReveal(player);
    }

    private IEnumerable<ActionOption> StarCrossedLoversActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "starCrossedLovers" } pending || pending.PlayerId != playerId) yield break;
        foreach (var (first, second) in StarCrossedCandidatePairs())
            yield return new("starCrossedLovers", $"Star-Crossed Lovers: {Definition(first).Name} and {Definition(second).Name}", true, null,
                new GameCommand("starCrossedLovers", playerId, _state.Version, CardIds: [first.Id, second.Id]), pending.CardId, [first.Id, second.Id]);
    }

    // The other of the pair(s) a Personality belongs to, while they are both in play under one controller.
    private IEnumerable<CardInstance> StarCrossedPartners(CardInstance personality) =>
        _state.StarCrossedPairs.Where(p => p.Contains(personality.Id)).Select(p => p[0] == personality.Id ? p[1] : p[0])
            .Where(id => _state.Cards.TryGetValue(id, out var other) && InPlay(other) && other.OwnerId == personality.OwnerId).Select(Card);

    // Assign's hook: not to the battlefield where his partner already is, nor to the one the same command sends his partner to.
    private void RequireStarCrossedMayAssign(CardInstance personality, int battlefield, Dictionary<string, int> assignments)
    {
        foreach (var partner in StarCrossedPartners(personality))
            Require(!(partner.Zone == Zone.Battlefield && partner.ProvinceIndex == battlefield) && !(assignments.TryGetValue(partner.Id, out var partnerTo) && partnerTo == battlefield),
                $"Star-Crossed Lovers: {Definition(personality).Name} may not be assigned to {Definition(partner).Name}'s battlefield.");
    }

    // MoveToBattlefield's hook.
    private bool StarCrossedNegatesMove(CardInstance personality, int battlefield)
    {
        var partner = StarCrossedPartners(personality).FirstOrDefault(p => p.Zone == Zone.Battlefield && p.ProvinceIndex == battlefield);
        if (partner is null) return false;
        Emit("moveNegated", $"{Definition(personality).Name} will not move to {Definition(partner).Name}'s battlefield (Star-Crossed Lovers).", personality.OwnerId, personality.Id);
        return true;
    }

    // ---- Twin Forks City -----------------------------------------------------------------------------------------------------------------------------
    // "After an action resolves that created a duel your Personality won during a Combat Segment, once per Personality per battle: Negate his next bowing, movement, or
    // destruction from other players' cards' effects." A trait of the Stronghold, so mandatory: FinishDuelResolution's hook, while a battle is being fought. The shield is
    // one-shot (the next of the three, whichever comes first) and lapses at the end of the turn, like every "negate his next ..." (Shinjo Junpei's).
    private const string TwinForksShieldKey = "twin-forks-city:shield", TwinForksBattleKey = "twin-forks-city:battle";

    private void ApplyTwinForksCity(string? winnerId)
    {
        if (winnerId is null || _state.Phase != Phase.Battle || !_state.Cards.TryGetValue(winnerId, out var winner) || !InPlay(winner) || Definition(winner).Type != CardType.Personality) return;
        var stronghold = Card(Player(winner.OwnerId).StrongholdId);
        if (Definition(stronghold).Effect != TwinForksCityEffect) return;
        if (winner.AbilityUses.TryGetValue(TwinForksBattleKey, out var battle) && battle == _state.BattleNumber) return;
        winner.AbilityUses[TwinForksBattleKey] = _state.BattleNumber;
        winner.AbilityUses[TwinForksShieldKey] = _state.TurnNumber;
        Emit("shieldRaised", $"{Definition(stronghold).Name}: {Definition(winner).Name}'s next bowing, movement or destruction from other players' cards' effects will be negated.", winner.OwnerId, winner.Id);
    }

    // Spends the shield when one of the three would happen to him from another player's card's effect.
    private bool TwinForksNegates(CardInstance card, int? actingPlayerId, string what)
    {
        if (actingPlayerId is not int acting || acting == card.OwnerId || !card.AbilityUses.TryGetValue(TwinForksShieldKey, out var turn) || turn != _state.TurnNumber) return false;
        card.AbilityUses.Remove(TwinForksShieldKey);
        Emit($"{what}Negated", $"{Definition(card).Name}'s {what} was negated (Twin Forks City).", card.OwnerId, card.Id);
        return true;
    }

    // ---- The Moon's Imperative / The Sun's Aspiration's second Open -----------------------------------------------------------------------------------
    // "Shuffle them into their appropriate decks": a Dynasty card to its owner's Dynasty deck, a Fate card to the Fate deck, and every deck touched is shuffled.
    private void ShuffleDiscardsIntoDecks(IEnumerable<CardInstance> cards, CardInstance source)
    {
        var touched = new HashSet<(int Owner, string Deck)>();
        foreach (var card in cards.ToArray())
        {
            var owner = Player(card.OwnerId);
            var dynasty = IsDynastyCardType(Definition(card).Type);
            card.Zone = dynasty ? Zone.DynastyDeck : Zone.FateDeck; card.FaceUp = false; card.ProvinceIndex = null; ResetCardState(card);
            (dynasty ? owner.DynastyDeck : owner.FateDeck).Add(card.Id);
            touched.Add((owner.Id, dynasty ? SearchDeck.Dynasty : SearchDeck.Fate));
            Emit("toDeck", $"{Definition(card).Name} was shuffled into {owner.Name}'s {(dynasty ? "Dynasty" : "Fate")} deck ({Definition(source).Name}).", owner.Id, card.Id);
        }
        foreach (var (owner, deck) in touched) ShuffleDeck(Player(owner), deck);
    }

    // ---- Versatile Farm's recruit (DeckSearch.cs: SearchEffect.RecruitUnbowed) --------------------------------------------------------------------------
    // "Put it into play, unbowed, paying 2 less Gold": the Holding's entry price (its Gold Cost and every "enters play for less" term a Holding gets, Yasuki Makoto's)
    // less the search's RecruitDiscount, paid for it as a recruit (PaymentKind.Recruit, so a Holding that produces more for a Holding does). Its costs must be paid for it to
    // enter play: with no payment the searcher can make it stays in the deck (shuffled as always); with one, the searcher pays (or declines, and it stays).
    private int SearchRecruitCost(PendingSearch search, CardInstance card) =>
        Math.Max(0, CardGoldCost(card) - SearchSpecOf(search).RecruitDiscount - HoldingEntryDiscount(card, search.PlayerId) - KeywordEntryDiscount(card, search.PlayerId));

    private void BeginSearchRecruit(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        var cost = SearchRecruitCost(search, card);
        if (cost == 0) { EnterSearchedHolding(search, card); FinishSearch(search); return; }
        if (PaymentOptions(player.Id, cost, new PaymentSubject(card, PaymentKind.Recruit)).Length == 0)
        {
            Emit("searchRecruitFailed", $"{player.Name} cannot pay {cost} Gold for {Definition(card).Name}, so it does not enter play.", player.Id);
            FinishSearch(search);
            return;
        }
        search.Stage = SearchStage.ChoosePayment;
        _state.PendingChoice = new("searchRecruitPay", search.PlayerId, search.SourceCardId, $"{Definition(Card(search.SourceCardId)).Name}: pay {cost} Gold to put {Definition(card).Name} into play {(SearchEntersBowed(search) ? "bowed" : "unbowed")}, or decline.");
    }

    private void ChooseSearchRecruitPayment(PendingSearch search, GameCommand command)
    {
        Require(command.Type == "chooseSearchPayment", "Pay for the card you found, or decline.");
        var card = Card(search.SelectedCardId);
        if (Definition(card).Type == CardType.Personality) { ChoosePersonalityRecruitPayment(search, command); return; } // Sacrifice of Pawns (issue #492, group C)
        if (command.Choice == "decline")
        {
            Emit("searchRecruitDeclined", $"{Player(search.PlayerId).Name} did not pay for {Definition(card).Name}; it stays in the deck.", search.PlayerId);
            FinishSearch(search);
            return;
        }
        Pay(search.PlayerId, card, SearchRecruitCost(search, card), command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Recruit));
        EnterSearchedHolding(search, card);
        FinishSearch(search);
    }

    private IEnumerable<ActionOption> SearchRecruitPaymentActions(PendingSearch search, int playerId)
    {
        var card = Card(search.SelectedCardId);
        if (Definition(card).Type == CardType.Personality) { foreach (var personalityOption in PersonalityRecruitPaymentActions(search, playerId)) yield return personalityOption; yield break; } // Sacrifice of Pawns (issue #492, group C)
        var cost = SearchRecruitCost(search, card);
        var paying = new PaymentSubject(card, PaymentKind.Recruit);
        var quote = new GoldQuote(cost, GoldCostKind.Fixed, $"{Definition(card).Name} cost {CardGoldCost(card)} − {SearchSpecOf(search).RecruitDiscount} ({Definition(Card(search.SourceCardId)).Name})",
            null, PaymentOptions(playerId, cost, paying), Sources: PaymentSourcesFor(playerId, paying));
        yield return new("chooseSearchPayment", $"Pay {cost} Gold: put {Definition(card).Name} into play {(SearchEntersBowed(search) ? "bowed" : "unbowed")}", quote.Payments.Length > 0, quote.Payments.Length > 0 ? null : "You cannot pay for it.",
            new GameCommand("chooseSearchPayment", playerId, _state.Version, SourceIds: quote.Payments.FirstOrDefault()?.SourceIds), card.Id, Gold: quote);
        yield return new("chooseSearchPayment", $"Decline: {Definition(card).Name} stays in the deck", true, null, new GameCommand("chooseSearchPayment", playerId, _state.Version, Choice: "decline"), card.Id);
    }

    private void EnterSearchedHolding(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        card.Zone = Zone.Home; card.ProvinceIndex = null; card.FaceUp = true; card.Bowed = SearchEntersBowed(search); // Peace (issue #653) enters bowed
        Emit("cardEnteredPlay", $"{player.Name} put {Definition(card).Name} into play {(card.Bowed ? "bowed" : "unbowed")} ({Definition(Card(search.SourceCardId)).Name}).", player.Id, card.Id);
        // The mandatory half of entering play (a trait of the Holding's own); an entering-play Reaction of its own is not opened inside the search (no Farm Holding prints one).
        ApplyMandatoryEntryEffects(player.Id, card);
    }

    // ---- Validation hook (ValidateAbility, beside the other batches') ----------------------------------------------------------------------------------
    private void ValidateShadowsEmbraceBatch6(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case "fortress-lidless-destroy":
                Require(FortressOfLidlessEyesInPlay(), "No Fortress of Lidless Eyes is in play.");
                break;
            // Small-Time Bully: "After this card enters play, if you control a Courtier or Magistrate: You may dishonor a Personality (in play)."
            case "small-time-bully-dishonor":
                Require(_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality && (Keyword(c, "Courtier") || Keyword(c, "Magistrate"))),
                    "You control no Courtier or Magistrate.");
                break;
        }
    }

    // ---- Execution hook (RunAbilityEffects' default) -------------------------------------------------------------------------------------------------------
    // null: not one of this batch's abilities. false: its effects are done, the action finishes as usual. true: a window opened, the caller returns.
    private bool? RunShadowsEmbraceBatch6(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target,
        IEnumerable<string>? bowedBeforeAction)
    {
        switch (id)
        {
            // The Moon's Imperative / The Sun's Aspiration: the Holding's production for the turn.
            case "moons-imperative-holding":
                ApplyModifier(target!, source!.Id, id, "GoldProduction", -1);
                Emit("productionChanged", $"{Definition(target!).Name} produces 1 less Gold this turn ({Definition(source).Name}).", player.Id, target!.Id);
                return false;
            case "suns-aspiration-holding":
                ApplyModifier(target!, source!.Id, id, "GoldProduction", 1);
                Emit("productionChanged", $"{Definition(target!).Name} produces 1 more Gold this turn ({Definition(source).Name}).", player.Id, target!.Id);
                return false;
            // The Moon's Imperative: the Celestial was removed from the game as the cost; each face-up Personality is discarded and its province refilled face-up.
            case "moons-imperative-discard":
                foreach (var personality in SlotCards(command, "personalities").ToArray())
                    if (personality.Zone == Zone.Province) DiscardProvinceCardAndRefillFaceUp(personality, source!);
                return false;
            // The Sun's Aspiration: the Celestial was removed from the game as the cost.
            case "suns-aspiration-shuffle":
                ShuffleDiscardsIntoDecks(SlotCards(command, "cards").Where(c => c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead), source!);
                return false;
            // Fortress of Lidless Eyes: "Bow him. Negate his straightening while this card remains in play." A hold tied to the Fortress (StraightenHolds.cs' WhileInPlay), which no
            // point of any turn releases.
            case "fortress-lidless-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                AddStraightenHold(target!, Definition(source!).Name, null, StraightenPoint.TurnEnds, skip: int.MaxValue, whileInPlay: source!.Id);
                return false;
            // The ability all players have: destroy the Fortress (a Holding, destroyed as any card in play is).
            case "fortress-lidless-destroy":
                DestroyUnits([target!], false, player.Id);
                return false;
            // Small-Time Bully: "You may dishonor a Personality."
            case "small-time-bully-dishonor":
                Dishonor(target!, player.Id);
                return false;
            // Versatile Farm: the Holding was destroyed as the cost; the search opens (the action finishes when it does).
            case "versatile-farm-search":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            // Elephant Caravan: "If it is bowed or an attachment, destroy it. Bow it." A card whose destruction was negated is still bowed.
            case "elephant-caravan-fear":
                if (target!.Bowed || target.Zone == Zone.Attached) DestroyTargetCard(target, player.Id);
                if (InPlay(target) && !StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                return false;
            // Wasteland Scout: "You have Reconnaissance there. Give your other Followers there +1F."
            case "wasteland-scout-recon":
                GrantReconnaissanceAtCurrentBattlefield(player.Id);
                foreach (var follower in _state.Cards.Values.Where(c => c.Id != source!.Id && c.OwnerId == player.Id && c.Zone == Zone.Attached && Definition(c).Type == CardType.Follower && AtBattle(c))
                             .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                    ApplyModifier(follower, source!.Id, id, "Force", 1);
                return false;
            // Shieldman: "Gain 2 Honor."
            case "shieldman-honor":
                GainHonor(player.Id, 2, Definition(source!).Name, source);
                return false;
            // Bishamon's Tower: "Destroy it."
            case "bishamon-tower-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            default:
                return null;
        }
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredStarCrossedLovers(string type, GameCommand command)
    {
        if (type != "starCrossedLovers") return false;
        ResolveStarCrossedLoversChoice(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterShadowsEmbraceBatch6Hooks()
    {
        EngineHooks.ValidateEarly.Register("ValidateShadowsEmbraceBatch6", 300, static (engine, context) => engine.ValidateShadowsEmbraceBatch6(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunShadowsEmbraceBatch6", 600, static (engine, context) => engine.RunShadowsEmbraceBatch6(context.id, context.ability, context.command, context.player, context.source, context.target, context.bowedBeforeAction));
        EngineHooks.PendingChoice.Register("HandleRegisteredStarCrossedLovers", 3700, static (engine, type, command) => engine.HandleRegisteredStarCrossedLovers(type, command));
    }
}
