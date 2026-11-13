namespace L5R.Core;

public sealed partial class MatchEngine
{
    // _state.Events only ever grows via Emit(); GetView is called several times per submitted command (once per
    // player, sometimes twice per player -- see RoomManager.CreateState/SubmitAsync), and was re-walking and
    // re-masking the whole history array every single call, an O(total events so far) cost repeated on every
    // call for the life of a match (issue #546). Cache the masked-per-player projection and only extend it with
    // newly appended events. The cache key is the exact List<GameEvent> object plus its length, not just the
    // length: _state is reassigned wholesale (never mutated in place) on the engine's internal rollback paths
    // (e.g. MatchEngine.cs:197,247,270), so a stale reference can never be mistaken for the current one, and a
    // miss just falls back to full recomputation -- this can only skip redundant work, never return wrong data.
    private readonly GameEvent[]?[] _viewedEventsCache = new GameEvent[]?[2];
    private readonly List<GameEvent>?[] _viewedEventsSource = new List<GameEvent>?[2];
    private GameEvent[] ViewedEvents(int playerId)
    {
        var events = _state.Events;
        if (ReferenceEquals(_viewedEventsSource[playerId], events) && _viewedEventsCache[playerId] is { } cached && cached.Length == events.Count) return cached;
        var start = ReferenceEquals(_viewedEventsSource[playerId], events) ? _viewedEventsCache[playerId]!.Length : 0;
        var result = new GameEvent[events.Count];
        if (start > 0) Array.Copy(_viewedEventsCache[playerId]!, result, start);
        for (var i = start; i < events.Count; i++)
        {
            var e = events[i];
            result[i] = e.Type == "duelFocused" && e.PlayerId != playerId ? e with { CardId = null } : e;
        }
        _viewedEventsSource[playerId] = events;
        _viewedEventsCache[playerId] = result;
        return result;
    }

    /// <summary>Only this detached view may cross the trust boundary to a player.</summary>
    public MatchView GetView(int playerId)
    {
        if (playerId is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(playerId));
        var players = _state.Players.Select(player =>
        {
            var owned = _state.Cards.Values.Where(c => c.OwnerId == player.Id).ToArray();
            // A card attached to a Personality stands in that Personality's unit, whoever owns it (issue #337: a Follower Moto Jun-Ni attached from the
            // opponent's discard pile; Cultist Infiltrator on an enemy Personality): it is listed with the host's controller so a client that groups a unit
            // from its own player's cards finds it. Its CardView.OwnerId is still its owner's, and it returns to that player's pile when it leaves play.
            var units = _state.Cards.Values.Where(c => (c.Zone == Zone.Attached && c.AttachedTo is string hostId ? Card(hostId).OwnerId : c.OwnerId) == player.Id).ToArray();
            return new PlayerView(player.Id, player.Name, player.Clan, player.Honor, player.TurnsTaken,
                ViewCard(Card(player.StrongholdId)),
                units.Where(c => c.Zone == Zone.Home || c.Zone == Zone.Attached && Card(c.AttachedTo).Zone == Zone.Home).Select(c => ViewCard(c)).ToArray(),
                units.Where(c => c.Zone == Zone.Battlefield || c.Zone == Zone.Attached && Card(c.AttachedTo).Zone == Zone.Battlefield).Select(c => ViewCard(c)).ToArray(),
                player.Provinces.Select(p => new ProvinceView(p.Index, EffectiveProvinceStrength(p), p.Destroyed,
                    p.CardId is null ? null : ViewCard(Card(p.CardId), Card(p.CardId).FaceUp),
                    p.RegionId is null ? null : ViewCard(Card(p.RegionId)), ProvinceStrengthMax(p), HoldsNoDynasty(p), p.DarkExperimentsMarked)
                // Plains of Otosan Uchi (issue #554): DynastySlots is authoritative only once a second slot has ever existed; every
                // other province leaves it null, keeping the legacy Card field as the whole story (the PR #544 view contract).
                { FortificationIds = FortificationsOf(player.Id, p.Index), StrengthModifier = StrengthModifierOf(p), StrengthNotes = StrengthNotesOf(p), DynastySlots = p.CardId2 is null ? null : [new(0, p.CardId is null ? null : ViewCard(Card(p.CardId), Card(p.CardId).FaceUp)), new(1, ViewCard(Card(p.CardId2), Card(p.CardId2).FaceUp))] }).ToArray(), // issue #503
                player.Id == playerId ? player.Hand.Select(id => ViewCard(Card(id))).ToArray() : [], player.Hand.Count,
                // Current Dynasty/Fate deck sizes are hidden information (EE-STATE-014): no seat's projection carries them, not even the deck owner's.
                owned.Where(c => c.Zone is Zone.DynastyDiscard or Zone.FateDiscard or Zone.Dead).Select(c => ViewCard(c)).ToArray(),
                player.ProclaimUsedTurn == _state.TurnNumber, player.DishonorLossPending,
                owned.Where(c => c.Zone == Zone.RemovedFromGame).Select((c, i) => c.FaceUp || c.OwnerId == playerId ? ViewCard(c) : HiddenRemovedView(c, i)).ToArray(), // issue #821: a face-down card removed from the game stays hidden from the other player
                DeadCount(player.Id),
                // Counts are public; identities are private unless already publicly focused from
                // discard or revealed by Strike. Anonymous backs need no stable hidden card IDs.
                owned.Where(c => c.Zone == Zone.Focused && (player.Id == playerId || c.FaceUp)).Select(c => ViewCard(c)).ToArray(),
                EventsInPlay: owned.Where(c => c.Zone == Zone.EventInPlay).Select(c => ViewCard(c)).ToArray(),
                Celestials: owned.Where(c => c.Zone == Zone.Celestial).Select(c => ViewCard(c)).ToArray(),
                FocusCount: owned.Count(c => c.Zone == Zone.Focused),
                Underneath: owned.Where(c => c.Zone == Zone.Underneath).Select(c => ViewUnder(c, playerId)).ToArray())
            { HandLimit = HandLimitFor(player.Id), DynastyActionTaken = player.DynastyActionTaken, PlansWithinPlansArmed = player.Id == playerId && LimitedInsteadOfOpen(player.Id) }; // issue #503: The New Foundries
        }).ToArray();
        var duel = _state.PendingDuel;
        return new(ContentVersion, _scenario, _state.Version, _state.TurnNumber, playerId, _state.ActivePlayerId, _state.PriorityPlayerId,
            _state.Phase, _state.PendingChoice, _state.CurrentBattleProvince, _state.UnresolvedBattlefields.ToArray(),
            _state.WinnerId, _state.VictoryReason, players, ViewedEvents(playerId), _scenario == CustomPractice.Scenario ? ["Custom Practice: validated deck construction and implemented cards. Not historical tournament certification."] : _scenario.StartsWith("duel-demo/", StringComparison.Ordinal) ? ["Prepared Duel Demo position; not a playable deck profile.", DuelDemoScenarios.Get(_scenario[10..]).Description] : _scenario is PublishedContent.Scenario or PublishedContent.TestScenario ? ["Published Lion/Crane playtest: all 40 definitions supported under the pinned Emperor profile.", "Other imported decks remain reference-only; this profile is not tournament certification."] : [.. Limits], _state.FavorPlayerId,
            duel is null ? null : new DuelView(duel.ChallengerPlayerId, duel.ChallengedPlayerId, duel.ChallengerId, duel.ChallengedId, duel.Stat, duel.Stage, _state.PriorityPlayerId, duel.Struck, duel.DuelId),
            BuildBattleStepView(),
            // Deck search (issue #312): the deck viewer is private to the searcher; the shown-cards record is public.
            BuildSearchView(playerId), BuildShownCards(), BuildLookView(playerId), BuildCopyView(playerId), BuildEitherOrView(playerId), BuildOpponentPaymentView(playerId),
            _state.LastDuel is null ? null : Copy(_state.LastDuel), BuildPendingAttackCue(), BuildProvinceSights(playerId), BuildBattlefieldViews(), BuildHandSights(playerId)) // issue #503: hand sights
        { ReactionPresentation = BuildReactionPresentation(), ReactionOrder = BuildReactionOrderView(), PhaseOrder = _state.PhaseOrder.IsDefault ? null : [.. _state.PhaseOrder.Order], PendingProvinceOrder = BuildProvinceOrderView(playerId), PendingHarshChoices = BuildHarshChoicesView(), ActionPhaseTurnPlayerId = _state.WinnerId is null ? R1.Aside?.TurnPlayerId : null, ResolutionArea = BuildResolutionAreaView(), BattleActions = BuildBattleActionViews(playerId) }; // Plains of Otosan Uchi (issue #554); public context only, the paused effect stays engine-private.
    }

    // The reveal-order decision (PR #544's own contract): PUBLIC identity/prompt, options PRIVATE to the deciding
    // seat (the other seat gets an empty array and shows waiting UI instead).
    // Harsh Choices (issue #581): the same counts the "Destroy last province · Defeat" action is labelled from.
    private HarshChoicesView? BuildHarshChoicesView()
    {
        if (_state.PendingChoice is not { Type: "harshChoices" } harsh) return null;
        var before = Player(harsh.PlayerId).Provinces.Count(p => !p.Destroyed);
        return new(before, Math.Max(0, before - 1), before == 1, before == 1 ? 0 : HarshChoicesDraw);
    }

    private ProvinceOrderView? BuildProvinceOrderView(int viewerId)
    {
        if (_state.PendingChoice is not { Type: "provinceOrder" } pending || _state.OtosanUchiProvinceIndex is not int index) return null;
        var owner = Player(pending.PlayerId);
        var province = owner.Provinces[index];
        var options = viewerId == pending.PlayerId
            ? new[]
            {
                new ProvinceOrderOption("left","Left",[0,1],new GameCommand("provinceOrder",pending.PlayerId,_state.Version,Choice:"left")),
                new ProvinceOrderOption("right","Right",[1,0],new GameCommand("provinceOrder",pending.PlayerId,_state.Version,Choice:"right")),
            }
            : [];
        return new($"provinceOrder-{province.Index}", pending.PlayerId, owner.Id, province.Index, pending.Prompt, options);
    }

    // A card removed from the game face-down (the Governor's Estate): its owner sees it, the other player sees only that it is there.
    private CardView HiddenRemovedView(CardInstance card, int index) =>
        new($"hidden-p{card.OwnerId}-removed{index}", null, card.OwnerId, card.Zone, false, false, null, null, 0);

    private CardView ViewCard(CardInstance card, bool visible = true)
    {
        if (!visible)
            return new($"hidden-p{card.OwnerId}-province{card.ProvinceIndex}", null, card.OwnerId, card.Zone, false, false, card.ProvinceIndex, null, 0);
        var def = Definition(card);
        var force = def.Type switch { CardType.Personality => PersonalityForce(card), CardType.Follower => FollowerForce(card), _ => Math.Max(0, def.Force + StatModifier(card, "Force")) };
        return new(card.Id, Copy(def), card.OwnerId, card.Zone, card.FaceUp, card.Bowed, card.ProvinceIndex, card.AttachedTo, force, def.Type == CardType.Personality ? PersonalityChi(card) : def.Chi, card.StraightenBlockedTurn == _state.TurnNumber || StraightenRefusedInView(card), card.Modifiers.ToArray(), card.GrantedKeywords.ToArray(), card.Dishonorable,
            def.Type == CardType.Personality && card.Zone is Zone.Home or Zone.Battlefield ? BattleForce(card) : null, card.Tokens.ToArray(),
            GrantedAbilityViews(card),
            GoldSourceProduction(card), ActionLocked(card), card.CreatedCopy,
            card.Protections.Select(p => new GrantedEffectView("Action protection", "Effects of actions this card performs will not be negated.", p.Source, p.Duration)).Concat(MechBGrantedEffects(card)).Concat(PromoGrantedEffects(card)).ToArray(), // issue #501: Only a Shadow's trait
            IsCopyCard(card), card.CopyOfCardId, CopyOfDefinition(card), CopyConditionsText(card), // issue #501 item 32 (EmperorMechBCopies.cs)
            card.OriginalOwnerId is int realOwner && realOwner != card.OwnerId ? realOwner : null, // issue #501: the owner of a card another player controls
            card.UnitNumber, card.Zone == Zone.Underneath ? card.HostCardId : null, SpentPrintedAbilities(card, def));
    }

    /// <summary>The printed abilities of a face-up card in play whose uses this turn are used up (never an Unlimited one); null when there are none.</summary>
    private string[]? SpentPrintedAbilities(CardInstance card, CardDefinition def)
    {
        if (!card.FaceUp || card.Zone is not (Zone.Home or Zone.Battlefield) || def.Abilities is not { Length: > 0 } ids) return null;
        var spent = ids.Where(id => AbilityCatalog.All.TryGetValue(id, out var ability) && ability.UsesPerTurn != AbilityUseLimit.Unlimited && AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? id)).ToArray();
        return spent.Length == 0 ? null : spent;
    }

    /// <summary>Returns exact proposed commands plus customization targets. Command validation remains authoritative.</summary>
    public ActionOption[] QueryActions(int playerId) => QueryActionsScoped(playerId); // QueryScope.cs

    private ActionOption[] QueryActionsUnscoped(int playerId)
    {
        if (playerId is < 0 or > 1) return [];
        if (_state.WinnerId is not null) return [];
        if (_state.PendingReactionOrder is { } reactionOrder && Equals(_state.PendingChoice, reactionOrder.Window)) return ReactionOrderActions(playerId, reactionOrder); // issue #803 (ReactionOrdering.cs)
        // Duel stages and duel reaction windows list their own actions; any other decision opened mid-duel
        // (a paused Focus Effect's choice, a search it starts) falls through to its ordinary handler below.
        if (_state.PendingChoice?.Type is "duelReaction" or "duelWonReaction" or "overconfidentDraw"
            || _state.PendingDuel is not null && _state.PendingChoice is null)
            return QueryDuelActions(playerId);
        var result = new List<ActionOption>();
        GameCommand Command(string type, string? id = null, string[]? sources = null, int? province = null, bool discount = false, bool allegiance = false,
            Dictionary<string, int>? assignments = null, string[]? cardIds = null, string? target = null)
            => new(type, playerId, _state.Version, id, sources, province, discount, allegiance, assignments, cardIds, target);
        void Add(string type, string label, GameCommand? command = null, string? reason = null, string? cardId = null, string[]? targets = null, int[]? provinces = null, PurchaseQuote? quote = null,
            GoldQuote? gold = null, Dictionary<string, GoldQuote>? targetGold = null)
            => result.Add(new(type, label, reason is null, reason, command, cardId, targets, provinces, quote, gold, targetGold));
        // A Reaction offered inside a reaction window (an Invest, Singh Remnants' attach, ...): same one Gold
        // quote as every other costed action (issue #303), so the client shows the payment panel and the
        // command carries the engine's suggested sources. An unpayable cost is offered disabled with its
        // reason instead of as an action that could only be rejected.
        // validate (Akodo Kakihara, issue #305): a reaction with a printed condition of its own (only when HE is
        // the target) is listed disabled, with ValidateAbility's own reason, while that condition does not
        // hold -- checked against the command AS QUOTED, so a payment the engine just suggested is never itself
        // the reason. targetId/label serve a reaction whose own Target is a choice (Champion of Thunder's
        // "own-unit-member": one option per unit member); both default to the plain single-option shape.
        void AddReaction(CardInstance card, string abilityId, string? reason = null, string? targetId = null, string? label = null, bool validate = false, string? performerId = null)
        {
            // "Choose a player" (issue #389): the player is a command parameter (TargetPlayerId), not a card, so a Reaction whose Target is a player is offered
            // once per legal player -- either player (you, or your opponent), each carrying the id the command needs -- and validated as quoted.
            var chosenPlayers = AbilityCatalog.All[abilityId].Target == "player" ? new int?[] { playerId, 1 - playerId } : new int?[] { null };
            foreach (var chosenPlayer in chosenPlayers)
            {
                var command = new GameCommand("useAbility", playerId, _state.Version, CardId: card.Id, TargetId: targetId, TargetPlayerId: chosenPlayer, AbilityId: abilityId, PerformerId: performerId);
                // A Reaction whose move names a battlefield the player chooses (Shosuro Hawado - exp, issue #453): the command carries the first listed destination
                // and the option lists them all (ActionOption.Provinces), as an ordinary move ability's offer does.
                var destinations = AbilityCatalog.All[abilityId].Move is { ChoosesBattlefield: true } reactionMove ? MoveDestinations(reactionMove) : null;
                if (destinations is { Length: > 0 }) command = command with { Province = destinations[0] };
                var gold = QuoteAbilityGold(command);
                if (gold is not null) command = command with { SourceIds = gold.Payments.FirstOrDefault()?.SourceIds };
                var why = reason ?? (gold is null ? null : gold.Error ?? (gold.Payments.Length == 0 ? "Insufficient unbowed Gold production." : null));
                if (why is null && (validate || chosenPlayer is not null)) why = AbilityRejection(command);
                // "May not perform actions" (issue #336): a Reaction is an action, so a locked performer is listed disabled with the reason.
                why ??= ActionLockRejection(command);
                var text = label ?? AbilityOptionLabel(abilityId, AbilityCatalog.All[abilityId], null, card);
                if (chosenPlayer is int who) text += who == playerId ? " · you" : " · your opponent";
                Add("useAbility", text, command, why, card.Id, provinces: destinations, gold: gold);
            }
        }
        // A Reaction that targets a card in play (Mastering the Elements' entry, Tamori Ruya's bow): one option carrying every legal target, each with its own Gold quote when the
        // cost comes from the target, the way an ordinary ability is listed; disabled, with the first reason, when no target is legal.
        void AddTargetedReaction(CardInstance card, string abilityId, string? reason)
        {
            var ability = AbilityCatalog.All[abilityId];
            var legal = new List<(GameCommand Command, GoldQuote? Gold)>(); string? firstError = null;
            // Shinjo Itao (issue #477, batch 3): an entering-play Reaction that attaches a card from a hand or discard pile (AttachSpec) names it among its targets too.
            var entryTargets = (OutOfPlayEntryReactionTargets(ability, playerId) ?? _state.Cards.Values.Where(InPlay).Select(c => c.Id)).Concat(ability.Attach is { } attachSpec ? AttachCandidatesInDiscard(attachSpec, playerId).Select(c => c.Id) : []).Distinct(StringComparer.Ordinal);
            foreach (var candidate in entryTargets.OrderBy(x => x, StringComparer.Ordinal))
            {
                var command = new GameCommand("useAbility", playerId, _state.Version, CardId: card.Id, TargetId: candidate, AbilityId: abilityId);
                var gold = QuoteAbilityGold(command);
                if (gold is not null) command = command with { SourceIds = gold.Payments.FirstOrDefault()?.SourceIds };
                var why = gold is null ? null : gold.Error ?? (gold.Payments.Length == 0 ? "Insufficient unbowed Gold production." : null);
                why ??= AbilityRejection(command);
                why ??= ActionLockRejection(command);
                if (why is null) legal.Add((command, gold)); else firstError ??= why;
            }
            var first = legal.FirstOrDefault();
            var targetGold = first.Gold?.Kind == GoldCostKind.Derived ? legal.Where(l => l.Gold is not null).ToDictionary(l => l.Command.TargetId!, l => l.Gold!) : null;
            Add("useAbility", AbilityOptionLabel(abilityId, ability, null, card), legal.Count > 0 ? first.Command : null, reason ?? (legal.Count > 0 ? null : firstError ?? "No legal target."), card.Id,
                legal.Select(l => l.Command.TargetId!).ToArray(), gold: first.Gold, targetGold: targetGold);
        }
        // Targeted-by-action reactions (issue #335): one option per legal new target for Blade of Champions, else one; unusable ones are listed disabled.
        void AddTargetedActionReactions()
        {
            if (OpenReactionSubject() is not { } held) return;
            foreach (var (card, reactionId) in TargetedActionReactionCandidates().Where(x => x.Card.OwnerId == playerId && TargetedActionReactionApplies(x.Card, x.AbilityId, held)).ToArray())
            {
                foreach (var (candidate, label) in TargetedReactionChoices(card, reactionId, held))
                {
                    // Ancestral Protection (Emperor Edition remaining-cards pass, group A, issue #492): a Reaction with a performer to choose is offered once per performer who could take it.
                    if (AbilityCatalog.All[reactionId].Performer is not null)
                    {
                        foreach (var performer in RemAReactionPerformers(playerId, AbilityCatalog.All[reactionId]))
                            AddReaction(card, reactionId, targetId: candidate, label: $"{AbilityCatalog.All[reactionId].Label} · {Definition(performer).Name} ({performer.Id})", validate: true, performerId: performer.Id);
                        continue;
                    }
                    AddReaction(card, reactionId, targetId: candidate, label: label, validate: true);
                }
            }
        }
        // Order Seppuku is an Open action (issue #368): offered for any Dishonorable Samurai/Courtier/Shugenja the querying player controls, but
        // ONLY at their own turn to act in the Action Phase with nothing pending -- the same single predicate the command handler uses
        // (SeppukuTiming.cs), so an offered Seppuku is never one the engine would refuse for its timing.
        if (SeppukuTimingReason(playerId) is null)
            foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && c.Dishonorable && Definition(c).Type == CardType.Personality && !SeppukuBarredByPriceOfHonor(c) /* issue #503 */ && !SeppukuBarredByControl(c) /* issue #827 */ &&
                (Keyword(c, "Samurai") || Keyword(c, "Courtier") || Keyword(c, "Shugenja"))))
                Add("orderSeppuku", $"Order Seppuku · {Definition(card).Name}", OwesForcedPass(playerId) ? null : Command("orderSeppuku", card.Id),
                    OwesForcedPass(playerId) ? ForcedPassReason : ActionLocked(card) ? $"{Definition(card).Name} may not perform actions this turn." : null, card.Id);
        if (playerId != _state.PriorityPlayerId)
        {
            Add("wait", $"Waiting for {Player(_state.PriorityPlayerId).Name}", reason: "The other player has the action opportunity.");
            Add("concede", "Concede", Command("concede"));
            return result.ToArray();
        }
        var player = Player(playerId);
        var registeredPending = RegisteredPendingActions(playerId); // P25 (DecisionInfra.cs): a hook-registered pending type lists its own actions
        if (registeredPending is not null) result.AddRange(registeredPending);
        else if (_state.PendingChoice is { Type: "harshChoices" } harsh)
        {
            Add("harshChoices", "Keep province", Command("harshChoices") with { Choice = "decline" }, cardId: harsh.CardId);
            var last = player.Provinces.Count(p => !p.Destroyed) == 1;
            Add("harshChoices", last ? "Destroy last province · Defeat" : "Destroy province · Draw 3",
                Command("harshChoices") with { Choice = "destroy" }, cardId: harsh.CardId,
                provinces: harsh.CardId is not null && _state.Cards.TryGetValue(harsh.CardId, out var eventSource) && eventSource.ProvinceIndex is int province ? [province] : []);
        }
        else if (_state.PendingChoice is { Type: "overconfidentDraw" or "brashDraw" } draw)
        {
            Add(draw.Type, "Draw 1 card", Command(draw.Type) with { Choice = "draw" });
            Add(draw.Type, "Decline draw", Command(draw.Type) with { Choice = "decline" });
        }
        // "bowReaction" is handled further below (issue #216/#417's candidate-specific version), not here.
        else if (_state.PendingChoice is { Type: "proclaim" or "entryReaction" } pending)
        {
            if (pending.PlayerId == playerId)
            {
                if (pending.Type == "proclaim")
                {
                    var honor = Definition(Card(pending.CardId)).PersonalHonor;
                    // Kakita Yusugi / Yogo Ugimori (issue #453, batch 2): the limit he ignores, and the lock (ProclaimRejection).
                    Add("proclaim", $"Proclaim · gain {honor} Honor", Command("proclaim"), ProclaimRejection(player, Definition(Card(pending.CardId)), pending.CardId), pending.CardId);
                }
                var enteringCard = Card(pending.CardId);
                // Every "own enters play" Reaction the card prints (issue #389: Horume's gain AND lose; before, only the first was listed). One that shares a
                // once-per-window usage key with another (Horume's "gain or lose") is used once for the pair.
                foreach (var reactionId in (Definition(enteringCard).Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-enters-play"))
                {
                    var used = enteringCard.AbilityUses.ContainsKey(AbilityCatalog.All[reactionId].UsageKey ?? reactionId);
                    // A multi-target entering-play Reaction (Bayushi Makubesu, issue #453: "one or two of your Personalities"): the slot offer an ordinary ability gets.
                    if (AbilityCatalog.All[reactionId].Slots is not null)
                    {
                        var slotReaction = AbilityCatalog.All[reactionId];
                        var slotOption = SlotAbilityOption(new GameCommand("useAbility", playerId, _state.Version, CardId: enteringCard.Id, AbilityId: reactionId), slotReaction, AbilityOptionLabel(reactionId, slotReaction, null, enteringCard), enteringCard.Id);
                        result.Add(used ? slotOption with { Enabled = false, Reason = "This reaction has already been used." } : slotOption);
                        continue;
                    }
                    if (AbilityCatalog.All[reactionId].Target is "none" or "player") AddReaction(enteringCard, reactionId, used ? "This reaction has already been used." : null, validate: true); // validated like a submitted command (issue #833: Suikihime listed enabled while The Forgotten Temple refused it)
                    else AddTargetedReaction(enteringCard, reactionId, used ? "This reaction has already been used." : null);
                }
                // Singh Remnants' shape: a DIFFERENT card (hand or discard pile) watching this entry,
                // only relevant once a Personality is what entered.
                if (Definition(enteringCard).Type == CardType.Personality)
                    foreach (var (card, watchId) in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.Hand or Zone.FateDiscard)
                        .SelectMany(c => (Definition(c).Abilities ?? []).Select(a => (Card: c, Id: a)))
                        .Where(x => AbilityCatalog.All.TryGetValue(x.Id, out var def) && def.Timing == "Reaction" && def.Trigger == "any-personality-enters-play"))
                        AddReaction(card, watchId);
                // The Spider's Web (issue #486, batch 3): the controller's own Stronghold watching the entry, one option per legal Weapon in the hand.
                foreach (var watchId in StrongholdEntryWatchers(playerId, enteringCard))
                    AddTargetedReaction(Card(player.StrongholdId), watchId, null);
                // Shinjo Ki-Chang - exp (issue #492, proxy group Y): an in-play Personality watching the entry, listed disabled with the reason while it cannot be used (spent, bowed).
                foreach (var (watcher, watchId) in InPlayEntryWatchers(playerId, enteringCard))
                    AddReaction(watcher, watchId, validate: true);
            }
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "attackReaction" })
        {
            // Open to either side -- list this player's own eligible before-attack-resolves Reactions.
            foreach (var (card, reactionId) in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && !c.Bowed)
                .SelectMany(c => AbilityIdsOf(c).Select(a => (Card: c, Id: a)))
                .Where(x => AbilityCatalog.All.TryGetValue(x.Id, out var def) && def.Timing == "Reaction" && def.Trigger == "before-attack-resolves"))
            {
                var reaction = AbilityCatalog.All[reactionId];
                var used = AbilityUseSpent(card.AbilityUses, reaction, reaction.UsageKey ?? reactionId);
                // Akodo Kakihara (issue #305): an in-play Attack reactor may carry a printed condition of its own
                // (only when HE is the target), so a listed reaction is only enabled while ValidateAbility
                // actually accepts it -- otherwise every unrelated Attack would offer a button that then errors.
                AddReaction(card, reactionId, used ? "This reaction has already been used." : null, validate: true);
            }
            // Emperor Edition remaining-cards pass, group C (issue #492): a Strategy Reaction played from the hand ("After a Ranged Attack is targeted", Sniping; A Close Call and the like) is listed too.
            foreach (var (card, reactionId) in OutOfPlayReactions(playerId, "before-attack-resolves").ToArray())
                AddReaction(card, reactionId, validate: true);
            AddTargetedActionReactions();
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "beforeActionReaction" } beforeActionPending)
        {
            // Rokai / Tamoko / Champion of Thunder / Brothers of Goemon / Akodo Kakihara, and any future watcher:
            // the general in-play "before an action resolves" window is an alternating window open to EITHER
            // player (issue #318), so whoever holds priority is offered every reaction of THEIR OWN that applies
            // to the deferred action right now, from any number of cards, plus Pass. A reaction whose own
            // Target is a choice among the watcher's unit (Champion's "own-unit-member") gets one option per
            // member; every other one is offered once. Each is validated exactly like a submitted command, so a
            // reaction that cannot legally be used right now (spent, bowed) is listed disabled with its reason
            // rather than hidden or offered blind. Recomputed on every query, so what a played reaction changed
            // (a bowed or destroyed source, a spent use) is already reflected at the next opportunity.
            if (_state.PendingNegatableAbility is { } deferredAction)
                foreach (var (card, reactionId) in BeforeActionReactionCandidates().Where(x => x.Card.OwnerId == playerId && BeforeActionReactionApplies(x.Card, x.AbilityId, deferredAction)).ToArray())
                {
                    var reaction = AbilityCatalog.All[reactionId];
                    // Shiro Chugo (Emperor Edition Stronghold pass, issue #486 batch 2): a Reaction with a performer to choose is offered once per performer who could take it.
                    if (reaction.Performer is not null)
                    {
                        foreach (var performer in BeforeActionReactionPerformers(playerId, reaction))
                            AddReaction(card, reactionId, label: $"{reaction.Label} · {Definition(performer).Name} ({performer.Id})", validate: true, performerId: performer.Id);
                        continue;
                    }
                    string?[] candidates = reaction.Target == "own-unit-member" ? UnitMembers(card).Select(c => (string?)c.Id).ToArray()
                        // Issue #336 fixture: one option per Personality the deferred action would move.
                        : reaction.Target == "deferred-action-mover" ? ActionMovers(deferredAction).Select(c => (string?)c).ToArray()
                        // The Earth's Strength (issue #453 batch 5): one option per defending Personality the deferred action would move.
                        : reaction.Target == "deferred-action-defending-mover" ? DeferredActionDefendingMovers(deferredAction).Select(c => (string?)c).ToArray() : P3BeforeActionTargets(reaction.Target); // issue #503: Request Authorization targets any Personality
                    foreach (var candidate in candidates)
                        AddReaction(card, reactionId, targetId: candidate, label: candidate is null ? null : $"{reaction.Label} · {Definition(Card(candidate)).Name} ({candidate})", validate: true);
                }
            AddTargetedActionReactions();
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "additionalActionReaction" })
        {
            // Additional Action (issue #181): open to either side, same shape as attackReaction above
            // -- list this player's own eligible before-additional-action-resolves Reactions, played
            // from where they can be (Effortless Counterattack's shape: hand, not in play; a Discipline one
            // also from a discard pile), the same list whose existence opened this window.
            foreach (var (card, reactionId) in OutOfPlayReactions(playerId, "before-additional-action-resolves"))
                AddReaction(card, reactionId);
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "battleActionReaction" } battleActionPending)
        {
            // Stormless Fury (issue #276 follow-up): scoped to the one specific offered card by id,
            // same single-watcher shape as entryReaction/bowReaction above.
            if (battleActionPending.PlayerId == playerId)
            {
                var card = Card(battleActionPending.CardId!);
                foreach (var reactionId in AbilityIdsOf(card).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "after-battle-action-resolves"))
                    AddReaction(card, reactionId, validate: card.Zone != Zone.Hand); // issue #830: an in-play watcher with a printed condition (Shosuro Hawado - exp: "if Hawado is at the current battlefield") is listed disabled, with ValidateAbility's reason, instead of as a button that is refused
            }
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "announceReaction" } announcePending)
        {
            // Tsuruchi Yakusho (issue #276 follow-up): scoped to the one specific offered card by id.
            if (announcePending.PlayerId == playerId)
            {
                var card = Card(announcePending.CardId!);
                foreach (var reactionId in AbilityIdsOf(card).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger is "own-ranged-action-announced" or EntrenchedTrigger or EmperorStrongholdBatch2Triggers.TorchAnnounce or EmperorFollowupsAbilities.NoHidingPlaceTrigger))
                    AddReaction(card, reactionId);
                // P6 (AfterFacts.cs): a registered watcher's Reaction, one option per performer it may name.
                foreach (var reactionId in AbilityIdsOf(card).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == AnnounceTriggers.WithPerformer))
                {
                    var announceReaction = AbilityCatalog.All[reactionId];
                    if (announceReaction.Performer is null) { AddReaction(card, reactionId, validate: true); continue; }
                    foreach (var performer in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && HasKeywords(c, announceReaction.RequiredKeywords)).OrderBy(c => c.Id, StringComparer.Ordinal))
                        AddReaction(card, reactionId, label: $"{announceReaction.Label} · {Definition(performer).Name}", validate: true, performerId: performer.Id);
                }
            }
            // Kalani's Landing (issue #816): the Reaction of the window's card, one option carrying every card in play it may target.
            if (announcePending.PlayerId == playerId && announcePending.CardId is { } kalaniWatcherId)
                foreach (var kalaniReaction in AbilityIdsOf(Card(kalaniWatcherId)).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == Wave816Abilities.AnnounceAnyAction))
                    AddTargetedReaction(Card(kalaniWatcherId), kalaniReaction, null);
            // No Hiding Place (issue #498): the announcing player's copies in hand, whichever card the window was opened for.
            foreach (var (noHidingPlace, noHidingId) in NoHidingPlaceOptions(playerId)) AddReaction(noHidingPlace, noHidingId);
            // Ikoma Ayumu, Utaku Lishan (issue #503): the watcher's own P3 Reactions, one option per target.
            if (announcePending.PlayerId == playerId && announcePending.CardId is { } announceWatcherId)
                foreach (var (p3Id, p3Target, p3Label) in P3AnnounceOptions(Card(announceWatcherId))) AddReaction(Card(announceWatcherId), p3Id, targetId: p3Target, label: p3Label, validate: true);
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "engageReaction" })
        {
            // Battle Phase timing (issue #310, spec 4.2): every legal "Engage:" action and "After engaging" Reaction this
            // player holds (in play, or a Strategy in hand, or Naval Invasion), then the pass that ends their opportunity.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "bowReaction" } bowPending)
        {
            // After an action bowed a Personality (issue #216 Hizumi and Ruya, issue #417 Quagmire): the one watching card's own Reaction, one option per Personality it may target
            // when its target is one of those the action bowed, else the plain option; then the pass.
            if (bowPending.PlayerId == playerId && bowPending.CardId is not null)
            {
                var watcher = Card(bowPending.CardId);
                foreach (var reactionId in AbilityIdsOf(watcher).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger is "bowed-personality-at-own-battlefield" or "bowed-personality-near-region" or "own-action-bowed-personality"))
                {
                    var used = AbilityUseSpent(watcher.AbilityUses, AbilityCatalog.All[reactionId], AbilityCatalog.All[reactionId].UsageKey ?? reactionId);
                    if (AbilityCatalog.All[reactionId].Target == "bow-reaction-candidate")
                        foreach (var candidateId in _state.BowReactionCandidateIds)
                            AddReaction(watcher, reactionId, used ? "This reaction has already been used." : null, targetId: candidateId, label: $"{AbilityCatalog.All[reactionId].Label} · {Definition(Card(candidateId)).Name} ({candidateId})", validate: true);
                    else if (AbilityCatalog.All[reactionId].Target is not ("none" or "player"))
                        AddTargetedReaction(watcher, reactionId, used ? "This reaction has already been used." : null);
                    else AddReaction(watcher, reactionId, used ? "This reaction has already been used." : null);
                }
            }
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "moveReaction" } movePending)
        {
            // After an action moved an enemy Infantry Personality to the battlefield of Legion of the Bat (issue #279): the one watching card's own Reaction, then the pass. Before issue #833 the window
            // listed neither, so a client offered only the engine's options could never play (or pass) the Reaction.
            if (movePending.PlayerId == playerId && movePending.CardId is not null)
            {
                var watcher = Card(movePending.CardId);
                foreach (var reactionId in AbilityIdsOf(watcher).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "moved-enemy-infantry-to-own-battlefield"))
                {
                    var used = AbilityUseSpent(watcher.AbilityUses, AbilityCatalog.All[reactionId], AbilityCatalog.All[reactionId].UsageKey ?? reactionId);
                    AddReaction(watcher, reactionId, used ? "This reaction has already been used." : null);
                }
            }
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "nikutaiReaction" } nikutaiPending)
        {
            // After a Follower in Nikutai's unit bowed (issue #279): the one watching card's own Reaction, then the pass; like moveReaction above, the window used to list neither (issue #833).
            if (nikutaiPending.PlayerId == playerId && nikutaiPending.CardId is not null)
            {
                var watcher = Card(nikutaiPending.CardId);
                foreach (var reactionId in AbilityIdsOf(watcher).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "sibling-follower-bowed"))
                {
                    var used = AbilityUseSpent(watcher.AbilityUses, AbilityCatalog.All[reactionId], AbilityCatalog.All[reactionId].UsageKey ?? reactionId);
                    AddReaction(watcher, reactionId, used ? "This reaction has already been used." : null);
                }
            }
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "afterResolutionReaction" })
        {
            // After a battle's resolution (issue #417): this player's Reactions that name it, from a hand, a discard pile or play, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "attackPhaseEndReaction" })
        {
            // Retribution (Emperor Edition remaining-cards pass, group C, issue #492): the Defender's Reactions that name the end of the Attack Phase, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "afterActionResolvedReaction" })
        {
            // After an action's resolution (issue #437): this player's Reactions that name it, then the pass -- same shape as afterResolutionReaction above.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "proclaimNegateReaction" })
        {
            // Turning the Story (issue #453, batch 2): the one reacting player's "before a Proclaim resolves" Reactions from hand, then the pass.
            result.AddRange(BeforeProclaimReactionOptions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "unimpeachableNameReaction" })
        {
            // Before a held action resolves (Unimpeachable Name's window; Bad Kharma and Footsteps of Madness, Emperor Edition remaining-cards pass group A, issue #492): this player's Reactions from a hand, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "honorChangeReaction" })
        {
            // Mendon's R4 ruling (issue #492): before an Honor gain or loss from an action's effects -- this player's Reactions for it (a Reaction with a performer to choose is offered once per
            // performer who could take it), then the pass.
            foreach (var (card, reactionId) in HonorChangeWindowOffers(playerId))
            {
                var reaction = AbilityCatalog.All[reactionId];
                // Unimpeachable Name: played alone, or with a dishonorable Personality to rehonor (its optional target).
                if (reactionId == HonorChangeReactions.UnimpeachableName)
                {
                    AddReaction(card, reactionId, validate: true);
                    foreach (var dishonored in _state.Cards.Values.Where(c => InPlay(c) && c.Dishonorable && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal))
                        AddReaction(card, reactionId, targetId: dishonored.Id, label: $"{reaction.Label} · rehonor {Definition(dishonored).Name} ({dishonored.Id})", validate: true);
                    continue;
                }
                if (reaction.Performer is not null)
                {
                    foreach (var performer in BeforeActionReactionPerformers(playerId, reaction))
                        AddReaction(card, reactionId, label: $"{reaction.Label} · {Definition(performer).Name} ({performer.Id})", validate: true, performerId: performer.Id);
                    continue;
                }
                AddReaction(card, reactionId, validate: true);
            }
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "battleRecruit" })
        {
            // After a battle resolved (issue #379): the recruit grants of this battle this player still holds, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOptional" } heartPending && _state.PendingBeforeBattleResolve is { } heartResolve && IsHeartOfDarknessChoice(heartResolve))
        {
            // Heart of Darkness (Forgotten Legacy pass, issue #467 batch 4): bow no card, one card, or two cards (ForgottenLegacyBatch4.cs).
            foreach (var option in HeartOfDarknessOptions(playerId, heartPending.CardId)) result.Add(option);
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOptional" } extendedPending && _state.PendingBeforeBattleResolve is { } extendedResolve && IsExtendedManeuversChoice(extendedResolve))
        {
            // Extended Maneuvers (Emperor Edition remaining-cards pass, group A, issue #492): straighten a Samurai, bow a non-Samurai, both, or neither (EmperorRemainingA.cs).
            foreach (var option in ExtendedManeuversOptions(playerId, extendedPending.CardId)) result.Add(option);
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOptional" } hardPending && _state.PendingBeforeBattleResolve is { } hardResolve && IsHardPressedChoice(hardResolve))
        {
            // Hard Pressed (Emperor Edition remaining-cards pass, issue #492, group B): the unit to move home, with or without the bow (EmperorRemainingB.cs).
            foreach (var option in HardPressedOptions(playerId, hardPending.CardId)) result.Add(option);
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOptional" } lakesidePending && _state.PendingBeforeBattleResolve is { } lakesideResolve && IsLakesideRetreatChoice(lakesideResolve))
        {
            // Lakeside Retreat (issue #622, TfStrategiesB.cs): the bowed Personality or Follower to straighten.
            foreach (var option in LakesideRetreatOptions(playerId, lakesidePending.CardId)) result.Add(option);
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOptional" } movingPending && _state.PendingBeforeBattleResolve is { } movingResolve && IsWave741MovingBattleChoice(movingResolve))
        {
            // A Moving Battle (issue #741, Wave741.cs): decline, or the Personality and the adjacent unresolved battlefield to move him to.
            foreach (var option in Wave741MovingBattleOptions(playerId, movingPending.CardId)) result.Add(option);
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOptional" } optionalPending)
        {
            // Before This Battle Resolves, an optional ("you may") effect: decline it, or name its one target.
            Add("beforeBattleOptional", "Decline", Command("beforeBattleOptional") with { Choice = "decline" }, cardId: optionalPending.CardId);
            foreach (var target in Army(1 - playerId, _state.CurrentBattleProvince!.Value))
                Add("beforeBattleOptional", $"Bow {Definition(target).Name}", Command("beforeBattleOptional", target: target.Id) with { Choice = "use" }, cardId: target.Id);
        }
        else if (_state.PendingChoice is { Type: "beforeBattleReaction" })
        {
            // A printed "Reaction: Before a battle resolves" (Steady Resolve, Akodo Dairuko): its owner's plays of it, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "beforeBattleOrder" })
        {
            // The active player orders the simultaneous "before this battle resolves" effects (issue #272/#310).
            foreach (var id in _state.PendingBeforeBattleResolve!.RemainingSourceIds)
                Add("beforeBattleOrder", $"Resolve {BeforeResolutionItemLabel(id)} next", Command("beforeBattleOrder", target: id), cardId: _state.Cards.ContainsKey(id) ? id : null);
        }
        else if (_state.PendingChoice is { Type: "encircledOrder" })
        {
            Add("encircledOrder", "The Attacker selects a Personality first", Command("encircledOrder") with { Choice = "attacker" });
            Add("encircledOrder", "The Defender selects a Personality first", Command("encircledOrder") with { Choice = "defender" });
        }
        else if (_state.PendingChoice is { Type: "encircledSelect" })
        {
            foreach (var card in Army(playerId, _state.PendingBeforeBattleResolve!.ProvinceIndex))
                Add("encircledSelect", $"Keep {Definition(card).Name} at this battlefield", Command("encircledSelect", target: card.Id), cardId: card.Id);
        }
        else if (_state.PendingChoice is { Type: "twistOfFateReaction" })
        {
            // Twist of Fate (Emperor Edition remaining-cards pass, issue #492, group D): before its player's Dynasty Phase ends, the Reactions that name it, then the pass.
            result.AddRange(AbilityActions(playerId));
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "killingGroundsSelect" })
        {
            // The Killing Grounds (Emperor Edition remaining-cards pass, issue #492, group D): the passing player names one of his own Personalities at the battlefield to destroy.
            result.AddRange(KillingGroundsActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "shinden" })
        {
            Add("chooseShinden", "Bow and prevent straightening this turn", Command("chooseShinden") with { Choice = "bow" });
            Add("chooseShinden", "Allow opponent to gain 1 Honor", Command("chooseShinden") with { Choice = "honor" });
        }
        else if (_state.PendingChoice is { Type: "preservingHonor" })
        {
            Add("choosePreservingHonor", "Bow him", Command("choosePreservingHonor") with { Choice = "bow" });
            Add("choosePreservingHonor", "Move him home", Command("choosePreservingHonor") with { Choice = "move" });
        }
        else if (_state.PendingChoice is { Type: "rikateDetach" } rikatePending)
        {
            // Asako Rikate (issue #301): any subset of the unit's attachments may go to hand. Offered:
            // keep them all, return them all, or return exactly one (every subset is legal to submit
            // directly through CardIds; these are the choices a client can enumerate without a combinatorial
            // explosion, the same convention declineStraighten's own single-card options follow).
            var attachments = Attachments(rikatePending.CardId!).Select(a => a.Id).ToArray();
            Add("chooseRikateDetach", "Keep every attachment", Command("chooseRikateDetach", cardIds: []), targets: attachments);
            Add("chooseRikateDetach", "Put every attachment into your hand", Command("chooseRikateDetach", cardIds: attachments), targets: attachments);
            if (attachments.Length > 1)
                foreach (var id in attachments)
                    Add("chooseRikateDetach", $"Put only {Definition(Card(id)).Name} into your hand", Command("chooseRikateDetach", cardIds: [id]), cardId: id);
        }
        else if (_state.PendingChoice is { Type: "rinShame" })
        {
            Add("chooseRinShame", "Dishonor him", Command("chooseRinShame") with { Choice = "dishonor" });
            Add("chooseRinShame", "Refuse: he is bowed and cannot straighten this turn", Command("chooseRinShame") with { Choice = "decline" });
        }
        // Plains of Otosan Uchi (issue #554): both slots are always still face down while this is open (it only ever
        // opens when neither has been revealed yet this Events Phase), so Left/Right never has a title to show instead.
        else if (_state.PendingChoice is { Type: "provinceOrder" } orderChoice && orderChoice.PlayerId == playerId)
        {
            Add("provinceOrder", "Left", Command("provinceOrder") with { Choice = "left" });
            Add("provinceOrder", "Right", Command("provinceOrder") with { Choice = "right" });
        }
        // Plains of Otosan Uchi (issue #554): a province-card-targeting effect (Shosuro Toson) with two candidates.
        // Mendon's ruling: a face-up candidate's own title replaces its plain "Left"/"Right" label.
        else if (_state.PendingChoice is { Type: "provinceCardChoice" })
        {
            var pcProvince = Player(_state.PendingProvinceCardChoice!.ProvinceOwnerId).Provinces[_state.PendingProvinceCardChoice!.ProvinceIndex];
            var pcLeft = pcProvince.CardId is { } pcLeftId && Card(pcLeftId).FaceUp ? Definition(Card(pcLeftId)).Name : "Left";
            var pcRight = pcProvince.CardId2 is { } pcRightId && Card(pcRightId).FaceUp ? Definition(Card(pcRightId)).Name : "Right";
            Add("provinceCardChoice", pcLeft, Command("provinceCardChoice") with { Choice = "left" });
            Add("provinceCardChoice", pcRight, Command("provinceCardChoice") with { Choice = "right" });
        }
        else if (_state.PendingChoice is { Type: "yourClanNeedsYouProvince" })
            foreach (var p in Player(playerId).Provinces.Where(p => !p.Destroyed && !HoldsNoDynasty(p))) // issue #503
                // Hidden information: only a face-up card's name may be shown; a face-down card is never revealed by the prompt.
                Add("chooseYourClanNeedsYouProvince", $"Place into province {p.Index + 1}" + (p.CardId is { } existingId ? Card(existingId).FaceUp ? $" (discarding {Definition(Card(existingId)).Name})" : " (discarding the face-down card)" : ""),
                    Command("chooseYourClanNeedsYouProvince", province: p.Index));
        else if (_state.PendingChoice is { Type: "conqueror" })
        {
            foreach (var id in _state.PendingConqueror!.CandidateIds)
                Add("chooseConqueror", $"Straighten {Definition(Card(id)).Name} instead of returning home bowed", Command("chooseConqueror", target: id), cardId: id);
        }
        else if (_state.PendingChoice is { } lookPending && IsLookChoice(lookPending.Type))
        {
            // Look at the top N cards (issue #322): the viewer always has a legal answer (see LookActions).
            result.AddRange(LookActions(playerId));
        }
        else if (_state.PendingChoice is { } copyPending && IsCopyChoice(copyPending.Type))
        {
            // Copying a keyword or an ability (issue #338): one option per candidate; the player always has a legal answer.
            result.AddRange(CopyActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "eitherOr" })
        {
            // The opposing player's Either/Or choice (issue #338): one option per option the engine offers, the unavailable ones
            // listed disabled with the reason.
            result.AddRange(EitherOrActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "deciderChoice" })
        {
            result.AddRange(DeciderChoiceActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "focusEffectChoice" })
        {
            result.AddRange(FocusChoiceActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "sequencedTarget" })
        {
            result.AddRange(SequencedTargetActions(playerId));
        }
        else if (_state.PendingChoice is { Type: DuelSleepAbilities.PendingType })
        {
            result.AddRange(BudgetSelectActions(playerId)); // Sleep (issue #667, DuelSleepBudget.cs): the budgeted multi-select
        }
        else if (_state.PendingChoice is { Type: "attackTargetSelect" })
        {
            result.AddRange(AttackTargetSelectionActions(playerId)); // the Attack target selection step after an announce window (issue #498)
        }
        else if (_state.PendingChoice is { Type: "opponentPayment" })
        {
            // The opposing player's variable payment (issue #373): one action carrying the variable quote; the paying player always has the legal answer of paying nothing.
            result.AddRange(OpponentPaymentActions(playerId));
        }
        else if (_state.PendingChoice is { } searchPending && IsSearchChoice(searchPending.Type))
        {
            // Deck search (issue #312): the searcher always has a legal answer (see SearchActions), so no bot or
            // legal-actions-driven client can stall in a search.
            result.AddRange(SearchActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "infiltratorStraighten" })
        {
            result.AddRange(InfiltratorStraightenActions(playerId)); // Goju Asuka / Only a Shadow (issue #501, EmperorMechB.cs).
        }
        else if (_state.PendingChoice is { Type: "p3BeforeEventReaction" })
        {
            result.AddRange(AbilityActions(playerId)); // Forging Destiny (issue #503, EmperorP3Windows.cs): the Reactions before an Event resolves, then the pass.
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "p3EntryReaction" })
        {
            result.AddRange(AbilityActions(playerId)); // Easy Persuasion (issue #503, EmperorP3Entries.cs): the Reactions after a Personality entered play, then the pass.
            Add("declineReaction", "Pass reaction", Command("declineReaction"));
        }
        else if (_state.PendingChoice is { Type: "p3AroruMove" })
        {
            result.AddRange(P3AroruMoveActions(playerId)); // Shosuro Aroru - exp2 (issue #503, EmperorP3Priority.cs).
        }
        else if (_state.PendingChoice is { Type: "p3MikiDarkVirtue" })
        {
            result.AddRange(P3MikiActions(playerId)); // Daigotsu Miki (issue #503, EmperorP3Windows.cs).
        }
        else if (_state.PendingChoice is { Type: "p3DarkVirtueDiscard" })
        {
            result.AddRange(P3DarkVirtueActions(playerId)); // Daigotsu Tenbatsu (issue #503, EmperorP3.cs).
        }
        else if (_state.PendingChoice is { Type: NitoshiInexperienced.HoldChoice })
        {
            result.AddRange(NitoshiHoldActions(playerId)); // Bayushi Nitoshi - inexperienced (issue #617, NitoshiInexperienced.cs).
        }
        else if (_state.PendingChoice is { Type: "rentatsuLoss" })
        {
            result.AddRange(RentatsuLossActions(playerId)); // Bayushi Rentatsu (Emperor Edition Personality pass, issue #477, batch 4).
        }
        else if (_state.PendingChoice is { Type: TurnBoundary.ChoiceType })
        {
            result.AddRange(TurnBeginChoiceActions(playerId)); // P4 (TurnBoundary.cs): the generic choice of an "after a turn begins" trait.
        }
        else if (_state.PendingChoice is { Type: "p5BlessingsTop" })
        {
            result.AddRange(BlessingsTopActions(playerId)); // issue #503: Blessings of the Shi-Tien Yen-Wang (EmperorP5Zones.cs)
        }
        else if (_state.PendingChoice is { Type: "declineStraighten" })
        {
            var optional = EligibleForOptionalStraighten(playerId);
            Add("declineStraighten", "Straighten all eligible cards", Command("declineStraighten", cardIds: []), targets: optional);
            foreach (var id in optional)
                Add("declineStraighten", $"Leave {Definition(Card(id)).Name} bowed", Command("declineStraighten", cardIds: [id]), cardId: id);
        }
        else if (_state.PendingChoice is { Type: "forcedDiscard" })
        {
            // A discard owed (Times of Strife, Daigotsu Bofana): one option per card of the discarding player's own hand -- only
            // they hold priority here, so the other player is told to wait and never sees these -- except a card the announced
            // action itself uses. Nothing to pick means no choice was ever opened (see OpenOverconfidentDrawWindow).
            foreach (var id in player.Hand.Where(id => _state.ForcedDiscardContinuation is not { } held || !CommandNamesCard(held, id)))
                Add("forcedDiscard", $"Discard {Definition(Card(id)).Name}", Command("forcedDiscard", target: id), cardId: id);
        }
        else if (_state.PendingChoice is { Type: "effectChoice" })
        {
            // Conditional effect choice (issue #337): one option per effect the rules let the acting player pick (see EffectChoice.cs).
            // Only they hold priority here, so the other player is told to wait and is offered nothing.
            result.AddRange(EffectChoiceActions(playerId));
        }
        else if (_state.PendingChoice is { Type: "discardHand" })
        {
            var count = player.Hand.Count - MaxHandSize(playerId);
            Add("discardHand", $"Discard {count} card(s)", Command("discardHand", cardIds: player.Hand.Take(count).ToArray()), targets: player.Hand.ToArray());
        }
        // Seeds of Decay pass (issue #453), batch 4: the two Events Phase decisions (SeedsOfDecayBatch4.cs), for the revealing player only.
        else if (_state.PendingChoice is { Type: "offeringReverence" } reverence)
        {
            if (playerId == reverence.PlayerId)
            {
                Add("offeringReverence", "Offering Reverence: shuffle it into your Dynasty deck", new GameCommand("offeringReverence", playerId, _state.Version, Choice: "shuffle"), cardId: reverence.CardId);
                Add("offeringReverence", "Offering Reverence: discard it", new GameCommand("offeringReverence", playerId, _state.Version, Choice: "discard"), cardId: reverence.CardId);
            }
        }
        // The Shadow's Embrace pass (issue #476), batch 6: Star-Crossed Lovers' pair, for the revealing player only (ShadowsEmbraceBatch6.cs).
        else if (_state.PendingChoice is { Type: "starCrossedLovers" })
            result.AddRange(StarCrossedLoversActions(playerId));
        else if (_state.PendingChoice is { Type: "eventChoice" }) // Emperor Edition remaining-cards pass (issue #492), group F.
            result.AddRange(EventChoiceActions(playerId));
        else if (_state.PendingChoice is { Type: "honorLossDecision" })
            result.AddRange(HonorLossDecisionActions(playerId));
        else if (_state.PendingChoice is { Type: "entryCostDecision" }) // En'you (issue #501, EmperorMechC.cs)
            result.AddRange(EntryCostDecisionActions(playerId));
        else if (_state.PendingChoice is { Type: "testedLandProvince" } testedLand)
        {
            if (playerId == testedLand.PlayerId)
                foreach (var province in TestedLandCandidates(playerId))
                    Add("testedLandProvince", $"Tested Land: attach it to the enemy province {province.Index + 1}", new GameCommand("testedLandProvince", playerId, _state.Version, Province: province.Index), cardId: testedLand.CardId, provinces: [province.Index]);
        }
        else switch (_state.Phase)
            {
                case Phase.Action:
                    // Equip (Limited) on the active player's own turn, and -- with a player-level timing grant (Foothills Keep, TimingGrants.cs) -- an
                    // Open equip of a Weapon in ANY player's action segment. The per-attachment timing is decided in EquipActions.
                    result.AddRange(EquipActions(playerId));
                    Add("pass", "Pass action", Command("pass"));
                    if (MayDeclineAdditionalAction(playerId)) Add("declineAdditionalAction", "Decline the additional action", Command("declineAdditionalAction")); // Mendon's item 2 (issue #501)
                    break;
                case Phase.AttackDeclaration:
                    Add("declareAttack", "Declare an attack", Command("declareAttack"), player.MayNotAttackThisTurn || AttackLockedByEvent(playerId) ? "You may not declare an attack this turn." : null);
                    Add("pass", "Decline to attack", Command("pass"), ForcedAttackDeclineRefusal(playerId)); // Spiritual Manipulation (issue #476 batch 4): an owed attack may not be declined
                    break;
                case Phase.InfantryDeclaration:
                case Phase.DefenseAssignment:
                case Phase.CavalryDeclaration:
                case Phase.CavalryDefense:
                    var cavalrySegment = _state.Phase is Phase.CavalryDeclaration or Phase.CavalryDefense;
                    var units = _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Home && (!c.Bowed || MayAssignBowed(c)) && Definition(c).Type == CardType.Personality && (!cavalrySegment || IsCavalryUnit(c))
                        && !RestrictionBarsAssignment(c, _state.Phase is Phase.DefenseAssignment or Phase.CavalryDefense) // P11 (CardRestrictions.cs): may not attack / defend
                        && !DelayedArrivalBarsAttacker(c, _state.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration)
                        && !(_state.Phase is Phase.DefenseAssignment or Phase.CavalryDefense && IsGojuAsuka(c))
                        && !(cavalrySegment && CavalryAssignmentBarredP6()) && !SuikihimeMovementLocked(c, MovementKinds.Assign) && !PromoMovementLocked(c, MovementKinds.Assign)).Select(c => c.Id).ToArray(); // issue #503: Incredible Tragedy, Otomo Suikihime; Fudo's Charging tokens (issue #653) // Delayed Arrival (issue #492, group F); Goju Asuka never defends (issue #501)
                    var attack = _state.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration;
                    // Defenders are offered only the battlefields AssignDefense accepts.
                    var provinces = Player(1 - _state.ActivePlayerId).Provinces
                        .Where(p => !p.Destroyed && (attack || _state.UnresolvedBattlefields.Contains(p.Index))).Select(p => p.Index)
                        .Concat(_state.UnresolvedBattlefields.Where(b => b >= FirstAdditionalBattlefield)).ToArray(); // a province's battlefield each, and Two Fronts' additional ones (issue #492, rulings pass R1)
                                                                                                                      // Hidden Falls Dojo (Stronghold pass, issue #486 batch 1): in the additional Infantry Maneuvers Segment only the acting player's Scouts, only to the provinces he targeted.
                    if (_state.ExtraInfantrySegment) (units, provinces) = HiddenFallsAssignmentChoices(playerId, attack);
                    var actionType = attack ? "attack" : "assignDefense";
                    // Retribution (issue #492, group C): an additional Attack Phase assigns one Personality at most, so each (unit, province) is its own option.
                    if (attack && RetributionRestricts())
                    {
                        foreach (var province in provinces)
                            foreach (var unit in units)
                                Add(actionType, $"Attack {BattlefieldLabel(province)} with {Definition(Card(unit)).Name}", Command(actionType, assignments: new Dictionary<string, int> { [unit] = province }),
                                    _state.RemC.RetributionAssigned >= 1 ? "You may only assign one Personality in an additional Attack Phase." : null, targets: [unit], provinces: [province]);
                    }
                    // Issue #627: a player with no Cavalry unit to assign is not offered the (empty) Cavalry assignment at all.
                    else if (!(cavalrySegment && units.Length == 0))
                        foreach (var province in provinces)
                        {
                            var armyUnits = ArmyAssignmentTemplate(units, playerId, province); // issue #821 (ArmyRestrictions.cs): the "assign everyone" command never names units that will not share an army
                            Add(actionType, $"{(attack ? "Attack" : "Defend")} {BattlefieldLabel(province)} · {armyUnits.Length} {(cavalrySegment ? "Cavalry" : "Infantry")} units",
                                Command(actionType, assignments: armyUnits.ToDictionary(id => id, _ => province)), units.Length == 0 ? $"No unbowed {(cavalrySegment ? "Cavalry" : "Infantry")} Personalities at home." : null,
                                targets: armyUnits, provinces: provinces);
                            foreach (var solo in ArmyAssignmentSoloUnits(units, armyUnits, playerId, province))
                                Add(actionType, $"{(attack ? "Attack" : "Defend")} {BattlefieldLabel(province)} with {Definition(Card(solo)).Name} alone", Command(actionType, assignments: new Dictionary<string, int> { [solo] = province }),
                                    null, targets: [solo], provinces: [province]);
                        }
                    // Spiritual Manipulation (issue #476 batch 4): assigning no attackers is refused while a forced target can be assigned in this segment.
                    Add("pass", attack ? (cavalrySegment ? "Assign no Cavalry attackers" : "Assign no Infantry attackers") : (cavalrySegment ? "Assign no Cavalry defenders" : "Assign no Infantry defenders"), Command("pass"),
                        attack ? ForcedAssignmentRefusal(playerId, null, cavalrySegment) : null);
                    break;
                case Phase.BattleSelection:
                    foreach (var province in _state.UnresolvedBattlefields) Add("selectBattle", $"Fight at {BattlefieldLabel(province)}", Command("selectBattle", province: province));
                    if (!_state.PassAllBattles[playerId] && _state.UnresolvedBattlefields.Count > 0) Add("passAllBattles", "Pass all battles", Command("passAllBattles")); // issue #628
                    break;
                case Phase.Battle:
                    // A published Terrain is played through this same command (the generic Terrain rule -- it prints no Battle ability of its own), so it is listed
                    // here too; before issue #467 only scenario Strategies were, and every published Terrain was playable by command but never offered.
                    // A Terrain whose own Battle ability puts it into play (Unstable Ground, issue #476 batch 4) is offered through that ability instead.
                    foreach (var card in player.Hand.Select(Card).Where(c => Definition(c).Type == CardType.Strategy && (Definition(c).ScenarioOnly || Keyword(c, "Terrain")) && !TerrainPlaysThroughOwnAbility(c)))
                    {
                        var def = Definition(card);
                        // A Strategy played with playStrategy pays its printed cost (issue #303): one quote, with
                        // the engine's suggested sources pre-filled, and an unpayable cost is a disabled option.
                        var strategyGold = QuoteStrategyGold(playerId, card);
                        var strategyPay = strategyGold?.Payments.FirstOrDefault()?.SourceIds;
                        var strategyReason = strategyGold is not null && strategyGold.Payments.Length == 0 ? "Insufficient unbowed Gold production." : null;
                        if (Keyword(card, "Terrain"))
                        {
                            var blocked = _state.Cards.Values.Any(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == _state.CurrentBattleProvince && Keyword(c, "Terrain"))
                                && !MarshallingFieldsWaiverAvailable(playerId); // The Marshalling Fields (issue #501)
                            var terrainReason = JahanTerrainReason(playerId) ?? RuleFlagTerrainReason(playerId) ?? (!Army(playerId, _state.CurrentBattleProvince!.Value).Any() && !TerrainPlaysWithoutPresence(def) ? "You have no unit at the current battlefield."
                                : blocked ? "This battlefield already has a Terrain in play." : strategyReason);
                            Add("playStrategy", $"Play {def.Name}", terrainReason is null ? Command("playStrategy", card.Id, strategyPay) : null, terrainReason, card.Id, [], gold: strategyGold);
                            continue;
                        }
                        var targets = _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == _state.CurrentBattleProvince && Definition(c).Type == CardType.Personality &&
                            (def.Effect == "force-three" ? c.OwnerId == playerId : c.OwnerId != playerId && PersonalityForce(c) <= 3)).Select(c => c.Id).ToArray();
                        var reason = !Army(playerId, _state.CurrentBattleProvince!.Value).Any() ? "You have no unit at the current battlefield." : targets.Length == 0 ? "No legal target at this battlefield." : strategyReason;
                        Add("playStrategy", $"Play {def.Name}", reason is null ? Command("playStrategy", card.Id, strategyPay, target: targets[0]) : null, reason, card.Id, targets, gold: strategyGold);
                    }
                    // Reserve (issue #184): an absent battle action -- no presence of the recruiting
                    // player's own required, only a battlefield at the card's own province with enemy
                    // presence there. Offered across every one of the player's own provinces, not just the
                    // currently-resolving battlefield.
                    foreach (var card in player.Provinces.Where(p => p.CardId is not null).Select(p => Card(p.CardId)).Where(c => c.FaceUp && Definition(c).Type == CardType.Personality && IsReserveRecruitable(c)))
                    {
                        var def = Definition(card);
                        var provinceIndex = card.ProvinceIndex!.Value;
                        var eligible = _state.UnresolvedBattlefields.Contains(provinceIndex) && Army(1 - playerId, provinceIndex).Any();
                        var modes = HasClanAffiliation(card, player.Clan) ? new[] { (true, false), (false, false), (true, true), (false, true) } : [(false, false)];
                        foreach (var (discount, allegiance) in modes)
                        {
                            var quote = QuotePurchase(playerId, card.Id, discount, allegiance, viaReserve: true);
                            var payment = quote.Payments.FirstOrDefault();
                            var reason = !eligible ? "There is no battlefield here with enemy presence." : quote.Error ?? (payment is null ? "Insufficient unbowed Gold production." : null);
                            var mode = discount ? allegiance ? "discount + allegiance" : "Clan discount" : allegiance ? "Clan allegiance" : "full cost";
                            Add("recruitReserve", $"Recruit {def.Name} from Reserve · {quote.Cost} Gold · {mode}",
                                reason is null ? Command("recruitReserve", card.Id, payment!.SourceIds, discount: discount, allegiance: allegiance) : null, reason, card.Id, quote: quote, gold: ToGoldQuote(quote));
                        }
                    }
                    // A Battle-timing equip granted by a card in play (Foothills Keep, TimingGrants.cs): a Weapon from hand to a Personality at the battlefield.
                    result.AddRange(EquipActions(playerId));
                    Add("pass", "Pass battle action", Command("pass"));
                    if (!_state.PassAllBattles[playerId]) Add("passAllBattles", "Pass all battles", Command("passAllBattles")); // issue #628
                    if (MayDeclineAdditionalAction(playerId)) Add("declineAdditionalAction", "Decline the additional action", Command("declineAdditionalAction")); // Mendon's item 2 (issue #501)
                    break;
                case Phase.Dynasty:
                case Phase.DynastyDiscard:
                    foreach (var card in player.Provinces.Where(p => p.CardId is not null).Select(p => Card(p.CardId)).Where(c => c.FaceUp))
                    {
                        var def = Definition(card);
                        if (_state.Phase == Phase.Dynasty)
                        {
                            var modes = def.Type == CardType.Personality && HasClanAffiliation(card, player.Clan) ? PurchaseModes(player, card) : [(false, false)];
                            foreach (var (discount, allegiance) in modes)
                            {
                                var quote = QuotePurchase(playerId, card.Id, discount, allegiance);
                                var payment = quote.Payments.FirstOrDefault();
                                var reason = quote.Error ?? (payment is null ? "Insufficient unbowed Gold production." : null);
                                var mode = discount ? allegiance ? "discount + allegiance" : "Clan discount" : allegiance ? "Clan allegiance" : "full cost";
                                Add("purchase", $"Buy {def.Name} · {quote.Cost} Gold{(def.Type == CardType.Personality ? $" · {mode}" : "")}",
                                    reason is null ? Command("purchase", card.Id, payment!.SourceIds, discount: discount, allegiance: allegiance) : null, reason, card.Id, quote: quote, gold: ToGoldQuote(quote));
                            }
                            if (def.Type == CardType.Personality)
                            {
                                foreach (var target in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality
                                    && string.Equals(Experienced.BaseTitle(Definition(c).Name), Experienced.BaseTitle(def.Name), StringComparison.OrdinalIgnoreCase)))
                                {
                                    var overlay = QuoteOverlay(playerId, card.Id, target.Id);
                                    Add("overlay", $"Overlay {def.Name} · Experience {overlay.HigherLevel} over {overlay.LowerLevel} · no Gold",
                                        overlay.Error is null ? Command("overlay", card.Id, target: target.Id) : null, overlay.Error, card.Id, [target.Id]);
                                }
                            }
                        }
                        Add("discardProvince", $"Discard {def.Name}", Command("discardProvince", card.Id), cardId: card.Id);
                    }
                    Add("endDynasty", "End Dynasty Phase", Command("endDynasty"));
                    break;
            }
        if (_state.PendingChoice is null && _state.Phase is Phase.Action or Phase.Battle) result.AddRange(AbilityActions(playerId));
        Add("concede", "Concede", Command("concede"));
        // Unfortunate Hesitation (issue #338): what a player who owes a pass cannot do at this opportunity is listed disabled.
        // Urgency (issue #453, batch 2): past the allowance, every non-Reaction action is listed disabled too.
        return ApplyActionAllowanceToOptions(playerId, ApplySearchPlayToOptions(playerId, ApplyForcedPassToOptions(playerId, result))); // issue #667 (SearchPlayGozu.cs): while the Terrain Gozu found is owed, only its play is offered enabled
    }
}
