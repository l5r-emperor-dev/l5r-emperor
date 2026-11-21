namespace L5R.Core;

// Action-effect tracking (issue #437): what a resolved action did to specific cards, kept only long enough for a Reaction whose own trigger
// asks about it to check. Two pieces:
//   - Recording: DestroyUnits, DestroyAttachment, MoveHome and MoveByRule's own move-to-a-battlefield path append to MatchState.CurrentActionAffected
//     as they run (RecordActionEffect). Never for the mass casualty sweep a battle's own resolution performs directly (MatchEngine.cs's own
//     DestroyUnits(d)/DestroyUnits(a) calls, with no acting player) -- that is "the battle's own resolution," not "a Battle action," the same
//     distinction the printed cards below draw. DestroyUnits itself is only ever called with an acting player id from within an action's own
//     effects (an ability, a deferred Attack's hit, a duel's outcome), so gating recording on that parameter being present is exact, not a guess.
//   - Reading: FinishActionEffectTracking, called from the three points an action -- including its own reaction windows -- is truly done
//     (CompleteAbilityResolution, ResolveAttack, Dueling.cs's FinishDuelResolution), snapshots the accumulator into MatchState.LastResolvedAction
//     (replacing whatever was there before) and opens the after-action-resolved Reaction window below if anyone holds one that applies.
//
// What is NOT tracked: a bow. Unlike destroy/move, nothing bows a card through one shared choke point -- over a hundred call sites set
// CardInstance.Bowed directly today, none narrated individually. Reading every one of them (or building a shared Bow() helper and migrating all
// of them) is its own, much larger task; Hida Defenders (oracle-l5r-3083, "after your action bowed, destroyed, or moved an opposing Personality")
// stays blocked on that piece alone, now that the destroyed/moved half of its own condition is otherwise expressible.
public static class AfterActionReactionTriggers
{
    /// <summary>"After the resolution of [a/your] action that affected a card" (issue #437): the specific condition -- which cards, whose action, what
    /// happened to them -- is read from MatchState.LastResolvedAction by AfterActionResolvedApplies, keyed per ability id like AfterResolution.cs's
    /// own AfterResolutionApplies keys "after a battle's resolution ends."</summary>
    public const string AfterActionResolved = "after-action-resolved";
}

public sealed partial class MatchEngine
{
    public const string DestroyedKind = "destroyed";
    public const string MovedKind = "moved";

    public const string DiscardedKind = "discarded";

    private void RecordActionEffect(string cardId, string kind, string? hostId = null, int? fromBattlefield = null, int? toBattlefield = null, int? focus = null)
    {
        var fact = new AffectedCard(cardId, kind, hostId, fromBattlefield, toBattlefield, toBattlefield is null ? null : MovedSide(cardId), focus);
        _state.CurrentActionAffected.Add(fact);
        NoteWatchedFact(fact); // P8 (ActionEffectWatch.cs): a watch armed for this action hears the fact as it happens
    }

    // Snapshots what the just-finished action affected into LastResolvedAction, opens the Reaction window naming it if anyone holds one that
    // applies, and clears the accumulator either way. Called from the three points an action (its own reaction windows included) is truly done,
    // in the same chain as CompleteAbilityResolution's own OpenBowReactionWindow/OpenMoveReactionWindow/OpenOverconfidentDrawWindow (closesTo is
    // the same "who priority returns to once every window this action opens has closed" every one of those already threads through).
    // performerId (issue #453, batch 2): the Personality who performed the action, for a Reaction that targets "the action's performer" (An Insult Answered); the Honor
    // the action cost (RecordHonorLoss, from GainHonor) rides along the same way.
    // "You may only play one copy of X in reaction to any one resolution": the Reactions the after-action window offers once per resolution (a second copy has nothing to react to).
    private static readonly HashSet<string> OneCopyPerResolution = ["grief-dishonor"];

    private bool FinishActionEffectTracking(string abilityId, int playerId, string? sourceId, int closesTo, string? performerId = null, IEnumerable<string>? targetIds = null)
    {
        // Issue #898: a Reaction played in an after-action window is an action too and records its own resolution, but the window's other Reactions still answer the action the window
        // opened for (two Chrysanthemum Blossoms on one Personality both react to his assignment). The window's record is kept: given back at once when the Reaction opens no window of its
        // own, and carried on the window it sets aside when it does.
        var outer = _state.PendingChoice is { Type: "afterActionResolvedReaction" } && AbilityCatalog.All.TryGetValue(abilityId, out var played) && played.Timing == "Reaction" ? _state.LastResolvedAction : null;
        var suspended = _state.SuspendedWindows.Count;
        var opened = FinishActionEffectTrackingCore(abilityId, playerId, sourceId, closesTo, performerId, targetIds);
        if (outer is null) return opened;
        if (OneCopyPerResolution.Contains(abilityId) && !outer.OneCopyPlayed.Contains(abilityId)) outer.OneCopyPlayed.Add(abilityId);
        if (_state.SuspendedWindows.Count > suspended) _state.SuspendedWindows[suspended].LastResolvedAction = outer;
        else _state.LastResolvedAction = outer;
        return opened;
    }

    private bool FinishActionEffectTrackingCore(string abilityId, int playerId, string? sourceId, int closesTo, string? performerId = null, IEnumerable<string>? targetIds = null)
    {
        ClearActionScopedModifiers(ActionKeyPrefix(playerId, sourceId, abilityId)); // P6 (AfterFacts.cs): "until the action ends" modifiers end with the action
        WlAfterActionResolved(abilityId, playerId, sourceId); // issue #674 (WlSpellsRings.cs): The Thunderer's Protection bows the action's performers after it resolves
        WlPersTornActionDone(abilityId, playerId, sourceId); // Moshi Ira (issue #674, WlPersTorn.cs): the -3F owed once the action targeting his unit has resolved
        AfterMechAActionResolved(abilityId, playerId, sourceId); // issue #501 (EmperorMechA.cs): Beyond the Line's draw "after the action's resolution"; Momoko's waiver is spent
        ApplyAgonyOfHerGaze(abilityId, playerId); // issue #503 (EmperorP2.cs): The Agony of Her Gaze's trait, before any Reaction to the same moment
        var affected = _state.CurrentActionAffected;
        FireEndWatches(affected, playerId, abilityId); // P8 (ActionEffectWatch.cs): the watches that want the whole record, and the end of every watch of this action
        P4AfterActionResolved(affected, abilityId, playerId, sourceId); // Scars of War, The Menacing Sky (issue #503, EmperorP4Watches.cs)
        _state.LastResolvedAction = new ResolvedActionFacts
        {
            AbilityId = abilityId,
            PlayerId = playerId,
            SourceId = sourceId,
            Affected = affected,
            PerformerId = performerId,
            TargetIds = targetIds?.Distinct().ToList() ?? [],
            HonorLosers = _state.CurrentActionHonorLosers,
            HandRecipients = _state.CurrentActionHandRecipients,
            FavorTakers = _state.CurrentActionFavorTakers,
            FavorDiscarders = TakeFavorDiscarders() // issue #667 (FavorMempo.cs)
        };
        foreach (var hook in EngineHooks.AfterActionTraits.Items) hook(this, _state.LastResolvedAction); // #689: mandatory printed traits, before Reactions.
        QueueP1AfterActionTraits(_state.LastResolvedAction); // Issue #503 (EmperorP1.cs): Osote's owed discard is due, Bakishi's shield drops, Tamisu's and Shichiro's traits are queued.
        NoteHonorLostToOtherAction(playerId, _state.CurrentActionHonorLosers); // Kitsu Tamasine (issue #477, batch 5)
        CountHandActionResolved(playerId, _state.CurrentActionHandRecipients); // Indecision (issue #501, EmperorMechD.cs): "actions which put cards into his hand this phase".
        _state.CurrentActionAffected = []; _state.CurrentActionHonorLosers = []; _state.CurrentActionHandRecipients = []; _state.CurrentActionFavorTakers = [];
        // Revenge (issue #437): this action destroyed a Personality of some OTHER player's -- set that owner's flag.
        foreach (var a in affected)
        {
            var affectedCard = Card(a.CardId);
            if (a.Kind == DestroyedKind && Definition(affectedCard).Type == CardType.Personality && affectedCard.OwnerId != playerId)
                _state.EnemyDestroyedOwnPersonalitySinceLastBattleAction[affectedCard.OwnerId] = true;
        }
        // Revenge: the window resets once the acting player's OWN Battle action finishes resolving.
        if (AbilityCatalog.All[abilityId].Timing == "Battle") _state.EnemyDestroyedOwnPersonalitySinceLastBattleAction[playerId] = false;
        CountReconStrategyAction(abilityId, playerId, sourceId); // Advanced Intelligence (Seeds of Decay pass, issue #453, batch 1).
        // Moksha Patam (issue #453 batch 5): "After an action resolves, if a player gained Honor from its effects" -- a trait, so applied here before any Reaction to the same point.
        ApplyMokshaPatamTokens();
        ApplyWlPersCoilsAfterAction(sourceId); // Daidoji Adachi (issue #674, WlPersCoils.cs): a trait, before any Reaction to the same point.
        ApplyP3AfterActionTraits(affected); // Deep Snows (issue #503, EmperorP3.cs): its trait, before any Reaction to the same point.
        // The Shadow's Embrace pass (issue #476), batch 3: Crowded Streets' trait -- a trait, so before any Reaction to the same point (ShadowsEmbraceBatch3.cs).
        if (OpenCrowdedStreetsTrait(closesTo)) return true;
        // Emperor Edition remaining-cards pass (issue #492), group D: The Shadow Court's and The Sincerity of the Dragon's traits (EmperorRemainingD.cs) -- also before any Reaction to the same moment.
        if (OpenShadowCourtTrait(closesTo) || OpenSincerityTrait(closesTo)) return true;
        if (OpenP1AfterActionTraits(closesTo, playerId)) return true; // Issue #503 (EmperorP1.cs): traits resolve before the Reactions to the same moment.
        if (OpenAfterActionResolvedWindow(closesTo)) return true;
        // Moshi Ino's storm (issue #453, batch 3; MoshiInoStorm.cs): the same "this action is truly done" moment, asked once no Reaction window opened over it.
        // Adachi's owed discard (issue #674) is asked here when no Reaction window has opened to carry it.
        return OpenInoStorm(closesTo) || WlPersCoilsDrainDiscards(closesTo);
    }

    // The Personality who performs an ability: the named performer, else the source resolved to its wielder; null for a player ability or a non-Personality source.
    private string? ActionPerformerIdOrNull(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        var performer = ability.Performer is not null ? (command.PerformerId is null ? null : ActingPersonality(Card(command.PerformerId))) : source is null ? null : ActingPersonality(source);
        return performer is not null && Definition(performer).Type == CardType.Personality ? performer.Id : null;
    }

    // A Battle action: a Battle-timed ability, or a Battle/Open one resolved in a Combat Segment.
    private bool IsBattleAction(string abilityId) => AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Timing is "Battle" or "BattleOrOpen" && _state.Phase == Phase.Battle; // P19: a rulebook timing override can take a Battle action in the Action phase

    private static bool IsAfterActionResolvedReaction(AbilityDefinition ability) => ability.Timing == "Reaction" && (ability.Trigger is AfterActionReactionTriggers.AfterActionResolved or ReactionTriggers.RingEntersPlay || FactTriggers.IsFact(ability.Trigger)); // P6: a fact's Reaction is offered in the same window

    // Whether the last resolved action's own effects destroyed a Personality of `playerId`'s.
    private bool LastActionDestroyedOwnPersonality(int playerId) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == DestroyedKind && Card(a.CardId).OwnerId == playerId && Definition(Card(a.CardId)).Type == CardType.Personality);

    // As above, narrowed to a Personality carrying `keyword` -- the shape Measure of Devotion ("one of your Deathseekers") and a future card of
    // this same family would read.
    private bool LastActionDestroyedOwnKeyword(int playerId, string keyword) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == DestroyedKind && Card(a.CardId).OwnerId == playerId && Keyword(Card(a.CardId), keyword));

    // Each Reaction's own condition once the window is open (the trigger check in ValidateAbility calls this).
    private bool AfterActionResolvedApplies(CardInstance source, string abilityId, int playerId)
    {
        if (_state.LastResolvedAction is not { } facts || source.OwnerId != playerId) return false;
        if (FactApplies(source, abilityId, playerId, facts) is { } factApplies) return factApplies; // P6 (AfterFacts.cs): a fact's window offers only the Reactions that name it, and an action's only the others
        if (WlRingsAfterActionApplies(abilityId, playerId, facts) is { } wlRings) return wlRings; // issue #699 (WlRings.cs): "After your Ring enters play"
        if (FavorMempoAfterActionApplies(source, abilityId, playerId, facts) is { } favorAfter) return favorAfter; // issue #667 (FavorMempo.cs): after you discard the Imperial Favor, and a negated Favor action's record
        // Hida Takeuchi (Emperor Edition Personality pass, issue #477 batch 2): "After the resolution of an action that attached a Follower from your hand to Takeuchi" -- any action,
        // Equip (a universal player action with no catalog entry) included, so it is read before the per-ability-id switch that indexes the catalog by the action's id.
        if (abilityId == "takeuchi-draw") return ActionAttachedFollowerFromHandTo(source, playerId);
        // Chrysanthemum Blossom (Emperor Edition remaining-cards pass, issue #492 group G): "After this Personality is assigned" -- the assignment command's own record, not a catalogued action.
        if (abilityId == "chrysanthemum-blossom-boost") return ChrysanthemumBlossomApplies(source, playerId, facts);
        if (PromoAfterActionApplies(source, abilityId, playerId, facts) is { } promoAfter) return promoAfter; // Closing the Gap (issue #653, PromoEmperorFateReactions.cs)
        if (WlAfterActionApplies(abilityId, playerId, facts) is { } wlHoldings) return wlHoldings; // Hero's Memorial (issue #674, WlHoldings.cs)
        if (PerformersAmbushApplies(source, abilityId, playerId, facts) is { } performersAmbush) return performersAmbush; // Lying in Ambush (issue #667, PerformersAmbush.cs)
        if (WithoutMercyApplies(source, abilityId, playerId, facts) is { } withoutMercy) return withoutMercy; // issue #648 (WithoutMercy.cs): a Battle action by your Samurai that targeted no enemy card
        if (PromoItemsAfterActionApplies(source, abilityId, playerId, facts) is { } promoItemsAfter) return promoItemsAfter; // The Ivory Sword (PromoEmperorItems.cs): an action by a dishonorable Personality who opposed it
        if (WlPersTornAfterActionApplies(source, abilityId, playerId, facts) is { } wlPersTorn) return wlPersTorn; // Shinjo Sihung (issue #674, WlPersTorn.cs): "after your Tactical action resolves"
        if (CreatedProxies677AfterActionApplies(source, abilityId, facts) is { } proxy677) return proxy677; // Goju Kaxt (issue #677, CreatedProxies677.cs): "an action ... during which this card was destroyed"
        if (Wave739AfterActionApplies(source, abilityId, playerId, facts) is { } wave739) return wave739; // The Tachikaze (issue #739, Wave739.cs): "after an action resolves whose effects destroyed your Follower"
        if (P3AssignmentOrActionApplies(source, abilityId, facts) is { } p3Assigned) return p3Assigned; // Utaku Tairu - exp (issue #503): "after a Personality assigns or moves"
        if (facts.AbilityId == AssignedSentinel) return false;
        if (!AbilityCatalog.All.ContainsKey(facts.AbilityId)) return false;
        if (FollowupsAfterActionApplies(source, abilityId, playerId) is { } followups) return followups;
        if (EmperorP1AfterActionApplies(source, abilityId, playerId) is { } p1) return p1; // Issue #503 (EmperorP1.cs): Itoku, Hida Bakishi
        if (P2AfterActionApplies(abilityId, playerId, facts) is { } p2After) return p2After; // issue #503: A Champion in Court (EmperorP2.cs)
        if (P3AfterActionApplies(source, abilityId, playerId, facts) is { } p3) return p3; // issue #503 (EmperorP3.cs)
        if (MechDAfterActionApplies(source, abilityId, playerId, facts) is { } mechD) return mechD; // Indecision (issue #501, EmperorMechD.cs) // Asako Chukage (issue #498) // Equip's facts name no catalogued ability: only the Reaction above reads them.
        return abilityId switch
        {
            // Measure of Devotion (oracle-l5r-5013): "After the resolution of a Battle action that destroyed one of your Deathseekers."
            "measure-of-devotion-create" => AbilityCatalog.All[facts.AbilityId].Timing == "Battle" && LastActionDestroyedOwnKeyword(playerId, "Deathseeker"),
            // Akodo Kobi - exp (oracle-l5r-232): "After another player's Battle action resolves which destroyed one of your Lion Clan Personalities."
            "kobi-recruit" => facts.PlayerId != playerId && AbilityCatalog.All[facts.AbilityId].Timing == "Battle" && LastActionDestroyedOwnKeyword(playerId, "Lion Clan"),
            // Matsu Rika (oracle-l5r-9827): "After your Bushido Virtue action resolves" -- her own action (facts.PlayerId == playerId, already implied by
            // source.OwnerId == playerId above only gating who HOLDS the Reaction, not whose action it was), classified through ActionCarriesKeyword the
            // same way Ebizo/Kobi's Recon/Tactical Traits already read a card's own keyword when the acting ability prints no explicit designator.
            "rika-bushido-virtue-bonus" => facts.PlayerId == playerId
                && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Bushido Virtue"),
            // Synthetic fixture (issue #437): "after the resolution of an action that destroyed one of your Personalities, gain 3 Honor" -- no
            // Timing/keyword narrowing, exercising AfterActionResolvedApplies/the window itself directly rather than through a real card's own
            // extra conditions.
            "training-after-action-resolved-gain" => LastActionDestroyedOwnPersonality(playerId),
            "crippled-sensei-create-action" => LastActionDestroyedOwnPersonality(playerId),
            // Feign Death (issue #622, TfStrategiesA.cs): "After the resolution of an action ... that destroyed a performing Personality you own" -- the target is checked against the record.
            TfStrategiesAAbilities.FeignAction => LastActionDestroyedOwnPersonality(playerId),
            // Rata (oracle-l5r-6165, Embers of War pass, issue #460 batch 2): "After an action resolves that Rata
            // performed and that destroyed a Personality" -- scoped to Rata himself as the action's own performer
            // (facts.PerformerId), not merely his controller's action, and to either player's destroyed Personality.
            "rata-draw" => RataDestroyedAPersonality(source),
            // Shinjo Byung - exp (Gempukku pass, issue #461): "After a Tactical Battle action resolves that Byung performed" -- his own performed action (facts.PerformerId), a Battle-timed
            // ability classified Tactical through ActionCarriesKeyword (the ability's own tag, or the source card's keyword).
            // Shinjo Jong-Ho (The Shadow's Embrace pass, issue #476, batch 2): "After a Tactical action Jong-Ho performed resolves" -- Byung's shape, any Timing.
            "jongho-bow" => facts.PerformerId == source.Id && facts.PlayerId == playerId
                && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Tactical"),
            "byung-additional-action" => facts.PerformerId == source.Id && facts.PlayerId == playerId && AbilityCatalog.All[facts.AbilityId].Timing == "Battle"
                && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Tactical"),
            // Bayushi Mitsuo (Emperor Edition Personality pass, issue #477, batch 4): "After the resolution of a Battle action that moved Mitsuo" -- either player's Battle action (a
            // Battle/Open one taken in a Combat Segment is a Battle action), his own move on the action's record.
            "mitsuo-additional-action" => IsBattleAction(facts.AbilityId) && facts.Affected.Any(a => a.Kind == MovedKind && a.CardId == source.Id),
            // Hida Yamadera - exp: "After another player's Battle action resolves, if its effects destroyed Yamadera."
            // Blade of Perfection (Emperor Edition remaining-cards pass, issue #492 group G): "After the resolution of another player's action that destroyed this card or its Personality".
            "blade-perfection-attach" => BladeOfPerfectionApplies(source, playerId, facts),
            // Shosuro Rokuta (proxy group Z, issue #492): "After the resolution of an action ... from a card that another player owns, if the card destroyed Rokuta."
            "rokuta-action-create" => RokutaActionApplies(source, playerId, facts),
            "yamadera-attack" => facts.PlayerId != playerId && AbilityCatalog.All[facts.AbilityId].Timing == "Battle" && facts.Affected.Any(a => a.Kind == DestroyedKind && a.CardId == source.Id),
            // Advanced Intelligence (Seeds of Decay pass, issue #453, batch 1): "After a Recon Strategy's action resolves" -- either player's, classified through ActionCarriesKeyword.
            "advanced-intelligence-draw" => facts.SourceId is { } reconSourceId && _state.Cards.TryGetValue(reconSourceId, out var reconSource) && Definition(reconSource).Type == CardType.Strategy
                && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], reconSource, "Recon"),
            // An Insult Answered (oracle-l5r-10136, issue #453 batch 2): "After another player's performed Limited or Open action resolves which caused you an Honor loss" -- a
            // Limited or Open ability (its Open half when Battle/Open was taken in the Action Phase) with a Personality performer still in play, that cost this player Honor.
            "insult-answered-duel" => facts.PlayerId != playerId && AbilityCatalog.All[facts.AbilityId].Timing is "Limited" or "Open" or "BattleOrOpen" && _state.Phase == Phase.Action
                && LastActionCostHonor(playerId) && LastActionPerformer() is not null,
            // Asahina Akikusa (oracle-l5r-10046, issue #453 batch 3): "After an action on your Air Spell resolves" -- the resolved action's source card is a Spell with the
            // Air keyword attached to a Personality her controller controls (the Spell's action is her controller's own, so facts.PlayerId matches too).
            // Shieldman (The Shadow's Embrace pass, issue #476 batch 6): "After another player's action resolves which destroyed another card in this unit."
            "shieldman-honor" => ShieldmanReactionApplies(source, playerId),
            "akikusa-honor" => facts.SourceId is not null && _state.Cards.TryGetValue(facts.SourceId, out var akikusaSpell) && Definition(akikusaSpell).Type == CardType.Spell
                && Keyword(akikusaSpell, "Air") && akikusaSpell.OwnerId == playerId && facts.PlayerId == playerId,
            // The Shadow's Embrace pass (issue #476), batch 3. Grief: "After an action resolves whose effects destroyed a player's Personality" -- either player's action, either player's
            // Personality. I Cast Two Shadows: "After an action resolves which moved your Personality in a Ninja unit home" -- from a battlefield (ShadowsEmbraceBatch3.cs).
            // Emperor Edition Stronghold pass (issue #486), batch 1. Eternal Victory Dojo: "After your Tactical action resolves" -- Jong-Ho's Tactical classification, any performer.
            // Kyuden Otomo - exp: "After you take the Imperial Favor" (the resolved action took it for him, and he still holds it).
            "eternal-victory-draw" => facts.PlayerId == playerId && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Tactical"),
            "otomo-favor-token" => OtomoFavorTraitApplies(playerId),
            "grief-dishonor" => !facts.OneCopyPlayed.Contains(abilityId) && facts.Affected.Any(a => a.Kind == DestroyedKind && Definition(Card(a.CardId)).Type == CardType.Personality),
            "cast-two-shadows-move" => facts.Affected.Any(a => a.Kind == MovedKind && a.FromBattlefield is not null && Card(a.CardId) is { Zone: Zone.Home } moved
                && moved.OwnerId == playerId && Definition(moved).Type == CardType.Personality && InNinjaUnit(moved)),
            // Emperor Edition Personality pass (issue #477), batch 5 (EmperorPersonalityBatch5.cs): Isawa Kimi - exp 2 and Shiba Tsukimi - exp 4.
            "kimi-exp2-discard" or "tsukimi-home" or "tsukimi-battlefield" => EmperorBatch5AfterActionApplies(source, abilityId, playerId, facts),
            // Emperor Edition Personality pass (issue #477), batch 6 (EmperorPersonalityBatch6.cs). Yung: "After one of your Rings enters play". Daigotsu Kanpeki - exp: "After the resolution of a Battle
            // action from a Strategy, if Kanpeki performed it".
            "yung-search-ring" => YungRingEnteredPlay(facts, playerId),
            // Emperor Edition remaining-cards pass (issue #492), group B: Introspection ("After one of your Rings enters play", Yung's trigger) and Meeting the Keepers ("After the resolution of an action
            // from one of your Rings") -- a Strategy's Reaction played from a hand (EmperorRemainingB.cs).
            "introspection-search" => YungRingEnteredPlay(facts, playerId),
            "keepers-search" => RingActionResolved(facts, playerId),
            "kanpeki-additional-action" => KanpekiPerformedStrategyBattleAction(source, facts, playerId),
            // Pillars of Virtue (Emperor Edition Stronghold pass, issue #486 batch 2): "After the resolution of a Kiho Battle action you took."
            "pillars-virtue-additional-action" => PillarsKihoBattleActionTaken(facts, playerId),
            // Deception Revealed (Emperor Edition remaining-cards pass, group A, issue #492): "After the resolution of an action that bowed a Ring" -- either player's action, a Ring still bowed.
            "deception-revealed-straighten" => facts.Affected.Any(a => a.Kind == BowedRingKind && Card(a.CardId).Bowed && InPlay(Card(a.CardId))),
            // Words Have Strength (Emperor Edition remaining-cards pass, issue #492, group D): "the resolution of another player's Battle action, if ... the action destroyed any of your Courtiers or Magistrates".
            "words-strength-action" => facts.PlayerId != playerId && IsBattleAction(facts.AbilityId) && ActionDestroyedCourtierOrMagistrate(facts, playerId),
            _ => EmperorRemainingCAfterActionApplies(abilityId, facts, playerId) // Emperor Edition remaining-cards pass, group C (issue #492): The Empress' Address.
        };
    }

    // The Reaction plays this player could make right now, asked with the window open exactly as they will be (AfterResolutionOptions' own shape).
    private ActionOption[] AfterActionResolvedOptions(int playerId)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("afterActionResolvedReaction", playerId, null, "After that action's resolution: use a Reaction, or pass.");
        try { return AbilityActions(playerId).Where(o => o.Enabled).ToArray(); }
        finally { _state.PendingChoice = saved; }
    }

    // Opens the window when a player has something to play. False (and nothing changes) when neither does.
    private bool OpenAfterActionResolvedWindow(int closesTo)
    {
        bool Holds(int player) => AfterActionResolvedOptions(player).Length > 0;
        if (!Holds(0) && !Holds(1)) return false;
        var first = FirstReactionOpportunity(Holds);
        // An action taken inside another settled window (a Ring put into play in the after-battle battleRecruit window, then Meeting the Keepers' "after an action from one of
        // your Rings"): that window is set aside and given back when this one closes (ReactionsAreActions.cs). Overwriting it left a Battle-phase priority with no battle once
        // both players passed here, and the game could not go on.
        if (_state.PendingChoice is { } open && open.Type != "afterActionResolvedReaction" && SettledReactionWindows.Contains(open.Type)) SuspendOpenWindow();
        OpenAlternatingReactionWindow(new("afterActionResolvedReaction", first, null, "After that action's resolution: you may use a Reaction, or pass.", closesTo), first, "After-action-resolved window opened.");
        return true;
    }
}
