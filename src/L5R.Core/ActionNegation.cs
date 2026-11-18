namespace L5R.Core;

// Whole-action negation (issue #338, Yoritomo Sakuma: "negate the action's (remaining) effects"). This is the minimal, isolated form of
// the general "negate an action's effects" a reaction to a before-action window can ask for; the narrower, per-card negations
// (Champion of Thunder's "all" on one card, Rokai's destroy, Tamoko's move) live in MatchState.NegatedEffects and are unchanged.
//
// The deferred action (MatchState.PendingNegatableAbility) has announced but not resolved. Negating it does not stop it from having been
// TAKEN: what it costs is still paid (Mendon's ruling, issue #351: a negated action pays ALL its costs unless the card says otherwise), its use is
// still spent (once per turn), a Strategy still leaves the hand, an Additional Action grant is still consumed or forfeited, and priority still
// passes -- only its effects do not happen, whichever they were and whoever they reached (bow, move, destroy, Force, Honor, a draw, a choice for
// another player...). Every cost is paid here, in ONE place: the Gold cost (PayForAbility), a chosen CostOption, the Imperial Favor discard, and
// the ability's declared Costs (AbilityCosts.cs: bow, discard, remove from the game, sacrifice, destroy a token, return to a deck, lose Honor) --
// the same list RunAbilityEffects pays for an action that is not negated. A cost an ability still pays inside its own effects (the few the
// decision document lists by id, whose cost is conditional or feeds its effect) is not paid when negated. Payment is ATOMIC: see below.
public sealed partial class MatchEngine
{
    // Marks the deferred action as negated; it is resolved that way when its reaction window closes (the "beforeActionReaction" handler in
    // Apply). Protection (issue #212): an action performed by a Protected card cannot be negated, the same "still legal to play, its own
    // effect just does nothing" shape StalwartNegatesBow and effortless-counterattack-negate already use.
    private void NegateDeferredAction(CardInstance negatingCard)
    {
        if (_state.PendingNegatableAbility is not { } deferred) return;
        if (ActionProtected(deferred)) // Takeshi's Protection, or Ki-Uso's for a Political action (issue #453 batch 5)
        {
            Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", negatingCard.OwnerId, negatingCard.Id);
            return;
        }
        _state.DeferredActionNegated = true; _reactionNegatedHeldAction = true;
        Emit("actionNegated", $"{Definition(negatingCard).Name} negates {Player(deferred.PlayerId).Name}'s action ({AbilityCatalog.All[deferred.AbilityId!].Label}).", negatingCard.OwnerId, negatingCard.Id);
    }

    // The deferred action, resolved negated: the taking of the action without its effects (see the file comment). Costs are paid in full and ATOMICALLY
    // (Mendon, AbilityCosts.cs): when paying any cost is blocked by a negation (CostPaymentBlockedByNegation, the one predicate) the ENTIRE action is negated --
    // nothing is paid, not even an Honor-loss cost (AbortActionForBlockedCost) -- and otherwise every cost is paid, an Honor-loss cost included, before the
    // (skipped) effects. `consumedGrant`: the action was taken as one of a burst of Additional Actions, and being negated loses only itself.
    private void ResolveNegatedAction(GameCommand command, bool consumedGrant = false, bool ignoreCosts = false)
    {
        ClearActionScopedModifiers(ActionKeyPrefix(command.PlayerId, command.CardId, command.AbilityId)); // P6 (AfterFacts.cs): a negated action ends too
        DropWatchesOf(command); // P8 (ActionEffectWatch.cs): a wholly negated action does nothing, so nothing is heard
        var id = command.AbilityId!; var ability = AbilityCatalog.All[id];
        // Bad Kharma (Emperor Edition remaining-cards pass, group A, issue #492): "He ignores the action's costs" -- the action is taken, nothing of it is paid and nothing happens.
        if (ignoreCosts)
        {
            var ignoring = Player(command.PlayerId); var ignoringSource = command.CardId is null ? null : Card(command.CardId);
            Emit("abilityUsed", $"{ignoring.Name}: {(ignoringSource is null ? "Player ability" : Definition(ignoringSource).Name)} — {ability.Label} (negated; its costs are ignored).", ignoring.Id, ignoringSource?.Id);
            CompleteNegatedAction(command, ability, ignoring, ignoringSource, PlayedFromDiscard(ability, ignoringSource), consumedGrant);
            return;
        }
        if (CostPaymentBlockedByNegation(command)) { AbortActionForBlockedCost(command, consumedGrant); return; }
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        var playedFromDiscard = PlayedFromDiscard(ability, source);
        PayForAbility(command, player);
        PayCostOption(ability, command, player, source);
        // The same list RunAbilityEffects discards the Favor for (Insulting Gesture's own discard was missing here: a negated one kept the Favor); a Favor token in its place (Kyuden Otomo - exp).
        if (IsFavorDiscardAbility(id)) DiscardImperialFavorCost(command, player);
        // A negated action was still performed (Forgotten Legacy pass, issue #467, batch 1: Daigotsu Murota's Dark Virtue record).
        RecordPerformedKeywordActions(ability, command, source);
        NoteSteelSoulNegated(ability, command.PlayerId, source); // Steel Soul Dojo (Emperor Edition Stronghold pass, issue #486 batch 2): a negated Dark Virtue action was still the first of its phase.
        PayAbilityCosts(ability, command, player, source);
        Emit("abilityUsed", $"{player.Name}: {(source is null ? "Player ability" : Definition(source).Name)} — {ability.Label} (negated).", player.Id, source?.Id);
        CompleteNegatedAction(command, ability, player, source, playedFromDiscard, consumedGrant);
    }

    // What a negated action still is, blocked or not: TAKEN. Its use is spent, a Strategy played from a hand (or a card played from a discard pile) is spent,
    // priority passes -- and, as for any action, an ordinary one ends the burst of Additional Actions the player held (a Reaction never does, and an action taken
    // as one of the burst loses only itself: the burst goes on, Mendon's ruling on #354). Nothing else of it happens.
    private void CompleteNegatedAction(GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, bool playedFromDiscard, bool consumedGrant)
    {
        SpendAbilityUse(source?.AbilityUses ?? player.AbilityUses, ability, ability.UsageKey ?? command.AbilityId!);
        // The Sincerity of the Dragon (issue #492, Mendon): "If Sincerity's base action is negated it is still removed from the game" -- the removal is not one of the negatable effects.
        // The Honesty of the Phoenix (corrected scan, issue #494) is removed the same way.
        if (command.AbilityId is "sincerity-dragon-name" or "honesty-phoenix-name" && source is not null && PlayedFromHand(source)) { player.Hand.Remove(source.Id); RemoveFromGame(source); }
        else if (!ability.FromDiscard) SpendHandPlayedCard(player, source);
        if (playedFromDiscard) RemoveFromGame(source!);
        if (!consumedGrant && ForfeitsAdditionalActions(ability)) _state.AdditionalActions.RemoveAll(g => g.PlayerId == player.Id && Forfeitable(g));
        P1ActionEnded(player.Id, command.AbilityId!); // Issue #503 (EmperorP1.cs): a wholly negated action ended too.
        P3AfterHeldAction(command); // Iron Will (issue #503): the additional Battle action it owes this player
        AfterMechAActionResolved(command.AbilityId!, player.Id, source?.Id); // issue #501: a negated action was still taken (Beyond the Line's "If you do, draw a card after the action's resolution")
        var burstContinues = ForfeitsAdditionalActions(ability) && _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g));
        if (_state.PendingChoice is null && OpenFavorDiscardAfterNegation(command.AbilityId!, player.Id, burstContinues ? player.Id : 1 - player.Id)) return; // issue #667 (FavorMempo.cs): the Favor was discarded as a cost all the same
        TakeFavorDiscarders();
        if (OpenOverconfidentDrawWindow(1 - player.Id)) return;
        NextAction(player.Id, burstContinues);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredAdditionalActionReaction(string type, GameCommand command)
    {
        if (type != "additionalActionReaction") return false;
        // Additional Action (issue #181): open to either side, same shape as attackReaction
        // above -- the opponent may play Effortless Counterattack's Reaction (negating the
        // pending effects) or pass; the granting player may also pass here to decline this
        // specific opportunity outright (forfeiting it -- see ConsumeAdditionalActionGrant,
        // already removed the grant regardless of what happens next).
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the pending additional action first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        if (_state.PendingChoice is null && _state.PendingAdditionalAction is not null)
        {
            var pending = _state.PendingAdditionalAction; _state.PendingAdditionalAction = null;
            // Negated (Effortless Counterattack): it is taken and pays its costs, only its effects are lost, and the burst it was part of goes on --
            // the next additional action is the holder's to take (ResolveNegatedAction, consumedGrant).
            var negated = _state.DeferredActionNegated; _state.DeferredActionNegated = false;
            if (negated) ResolveNegatedAction(pending, consumedGrant: true);
            else if (!ChainOutOfPlayReactionWindow(pending, consumedGrant: true)) ExecuteAbilityEffects(pending, consumedGrant: true); // R4 (issue #492): Bad Kharma and the like may still be played on top.
        }
        return true;
    }
    private bool HandleRegisteredUnimpeachableNameReaction(string type, GameCommand command)
    {
        if (type != "unimpeachableNameReaction") return false;
        // Unimpeachable Name (oracle-l5r-8989): open only to the one reacting player (unlike
        // attackReaction/additionalActionReaction, which are open to either side) -- the
        // top-level priority check already enforces that, so this window closes the instant
        // they answer either way, same single-pass shape as entryReaction/bowReaction above.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the pending reaction first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        if (_state.PendingChoice is null && _state.PendingOutOfPlayActionReaction is not null)
        {
            var pending = _state.PendingOutOfPlayActionReaction; _state.PendingOutOfPlayActionReaction = null;
            var reduction = _state.HonorLossReductionPlayerId;
            // Mendon's R4 ruling (issue #492): this window can follow the before-action window and the additional-action window of the same action, and what they set (the negations of
            // effects, the additional action's grant) is carried through it.
            var carried = _state.PendingOutOfPlayNegations; var carriedGrant = _state.PendingOutOfPlayConsumedGrant;
            _state.PendingOutOfPlayNegations = null; _state.PendingOutOfPlayConsumedGrant = false;
            var outer = ContinueSuspendedReaction(pending);
            if (carried is not null) _state.NegatedEffects = carried;
            _state.HonorLossReductionPlayerId = reduction; var suspended = _state.SuspendedWindows.Count;
            // Bad Kharma / Footsteps of Madness (Emperor Edition remaining-cards pass, group A, issue #492): the held action was negated as a whole in this window.
            if (RemATakeHeldNegation(out var remAIgnoreCosts)) ResolveNegatedAction(pending, consumedGrant: carriedGrant, ignoreCosts: remAIgnoreCosts); else ExecuteAbilityEffects(pending, consumedGrant: carriedGrant);
            if (carried is not null) { _state.NegatedEffects.Clear(); RestoreTemporaryRemovals(TemporaryRemovals.Action); if (_state.SuspendedWindows.Count <= suspended) _state.NegatedEffects = outer?.NegatedEffects ?? []; }
            if (_state.SuspendedWindows.Count > suspended) _state.SuspendedWindows[^1].HonorLossReductionPlayerId = outer?.HonorLossReductionPlayerId;
            else _state.HonorLossReductionPlayerId = outer?.HonorLossReductionPlayerId;
        }
        return true;
    }
    private bool HandleRegisteredAnnounceReaction(string type, GameCommand command)
    {
        if (type != "announceReaction") return false;
        // Tsuruchi Yakusho (issue #276 follow-up): the general "after you announce an action"
        // primitive -- once the window closes, the originally-announced command finally runs,
        // the same defer-then-reexecute shape beforeActionReaction/PendingNegatableAbility uses.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the announce reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        if (_state.PendingChoice is null && _state.PendingAnnouncedAction is not null)
        {
            var pending = _state.PendingAnnouncedAction; _state.PendingAnnouncedAction = null;
            if (P3ContinueAnnounce(pending, RunSetAsideBattleCommand)) return true; // Ikoma Ayumu, Utaku Lishan (issue #503): the next watcher, or the set-aside additional action or command goes on
            // Entrenched Position (Forgotten Legacy pass, issue #467 batch 3): a Reaction in this window negated the announced action.
            if (_state.AnnouncedActionNegated) { _state.AnnouncedActionNegated = false; ResolveNegatedAction(pending); }
            else if (!BeginAttackTargetSelection(pending)) ContinueAnnouncedAction(pending); // No Hiding Place (issue #498): the Attack's target is selected after the announce window.
        }
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterActionNegationHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredAdditionalActionReaction", 4900, static (engine, type, command) => engine.HandleRegisteredAdditionalActionReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredUnimpeachableNameReaction", 5200, static (engine, type, command) => engine.HandleRegisteredUnimpeachableNameReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredAnnounceReaction", 6000, static (engine, type, command) => engine.HandleRegisteredAnnounceReaction(type, command));
    }
}
