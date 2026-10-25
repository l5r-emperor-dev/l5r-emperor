namespace L5R.Core;

// Forgotten Legacy pass (issue #467), batch 5: the primitives of spell-wave-21, holding-wave-11, item-wave-13, event-wave-6, celestial-wave-6 and
// region-wave-8. The shared engine files carry short, commented hooks into this file (each names its card); the catalog entries, Target categories and
// Attack kinds live in CardAbilities.cs beside every other ability's, as they must (one dictionary literal, one switch each). Two shared cost verbs
// (CostVerbs.BowUnit, CostVerbs.ChiPenalty) are in AbilityCosts.cs, and Black Pearl's any-pile recruit terms in GoldQuotes.cs/QuotePurchase.
public sealed partial class MatchEngine
{
    // ---- Effect keys --------------------------------------------------------------------------------------------------------------------------------
    internal const string AssaultInTheJungleEffect = "assault-in-the-jungle";
    internal const string BurningDreamsEffect = "burning-dreams";
    internal const string WillingSpiritsEffect = "willing-spirits";
    internal const string ColonialHarborEffect = "colonial-harbor-tireless";
    internal const string BambooHarvestersExpEffect = "bamboo-harvesters-exp";
    internal const string RyoshunsGuidanceEffect = "ryoshuns-guidance";
    internal const string SecondCityEffect = "second-city-force";
    internal const string PearlOfRageEffect = "pearl-of-rage";
    internal const string MountainsPowerEffect = "mountains-power";
    internal const string WindOfTheMoonEffect = "wind-of-the-moon";
    // Burning Dreams: the owner's TurnsTaken at which its bounded clause ends, kept on the card's own stamp dictionary (the Castle of Order's per-battle stamp shape).
    private const string BurningDreamsUntilKey = "burning-dreams:until";

    // ---- Events (ResolveEvent / StartTurn hooks) ----------------------------------------------------------------------------------------------------
    // Returns true when the Event is one of this batch's and has been placed (the caller then refills the province, as for every Event).
    private bool ResolveForgottenLegacyBatch5Event(PlayerState player, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            // Assault in the Jungle (oracle-l5r-628): "until your turn two turns from now begins, all players have the ability ..." -- the bounded
            // War Weariness shape (ExpiresAtTurnsTaken = TurnsTaken + 2); the granted ability is the player ability "assault-jungle-destroy", live
            // only while a copy sits in Zone.EventInPlay (AssaultInTheJungleActive).
            case AssaultInTheJungleEffect:
                card.Zone = Zone.EventInPlay;
                card.ExpiresAtTurnsTaken = player.TurnsTaken + 2;
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play until {player.Name}'s second turn from now begins: every player has its Battle action.", player.Id, card.Id);
                return true;
            // Burning Dreams (oracle-l5r-1185): three independent clauses. The Favor is discarded now; the negation of Favor-discarding actions that target a
            // Nonhuman Personality lasts until the owner's second turn from now begins (a stamp, since the card itself must stay for the third clause); the
            // Political ban on Nonhumans lasts until the game ends (the card stays in Zone.EventInPlay for good, Suspicions' shape).
            case BurningDreamsEffect:
                card.Zone = Zone.EventInPlay;
                card.AbilityUses[BurningDreamsUntilKey] = player.TurnsTaken + 2;
                if (_state.FavorPlayerId is int favorHolder)
                {
                    ReleaseImperialFavor(favorHolder, asCost: false); // issue #667 (FavorMempo.cs): the Event's effect, not a cost, so no "after you discard" window follows it
                    Emit("favorDiscarded", $"{Definition(card).Name}: {Player(favorHolder).Name} discards the Imperial Favor.", favorHolder, card.Id);
                }
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play: Nonhumans may not perform Political actions until the game ends.", player.Id, card.Id);
                return true;
            // Willing Spirits (oracle-l5r-9337): "After your next turn begins, permanently give each of your provinces +2 strength." A delayed effect: the
            // card waits in Zone.EventInPlay until its owner's next turn begins (ExpiresAtTurnsTaken = TurnsTaken + 1), when StartTurn's expiry hands it to
            // OnForgottenLegacyBatch5EventExpired.
            case WillingSpiritsEffect:
                card.Zone = Zone.EventInPlay;
                card.ExpiresAtTurnsTaken = player.TurnsTaken + 1;
                Emit("eventEnteredPlay", $"{Definition(card).Name}: after {player.Name}'s next turn begins, each of their provinces permanently gets +2 strength.", player.Id, card.Id);
                return true;
            default:
                return false;
        }
    }

    // StartTurn's expiry loop, just before the Event is discarded: the one delayed effect of this batch.
    private void OnForgottenLegacyBatch5EventExpired(CardInstance card)
    {
        if (Definition(card).Effect != WillingSpiritsEffect) return;
        var owner = Player(card.OwnerId);
        // "Permanently give ... +2 strength": the in-place mutation of the province's current strength (Ring of Earth's raise), so it outlives the Event.
        foreach (var province in owner.Provinces.Where(p => !p.Destroyed)) province.Strength += 2;
        Emit("provinceStrengthRaised", $"{Definition(card).Name}: each of {owner.Name}'s provinces permanently gets +2 strength.", owner.Id, card.Id);
    }

    private bool AssaultInTheJungleActive() => _state.Cards.Values.Any(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == AssaultInTheJungleEffect);

    // A player ability that exists only while a card grants it is not listed at all outside that time (Equip (Battle)'s shape: no disabled entry for nothing).
    private bool PlayerAbilityDormant(string abilityId) => abilityId == "assault-jungle-destroy" && !AssaultInTheJungleActive();

    // Burning Dreams, clause 3 ("until the game ends"): any copy in Zone.EventInPlay.
    private bool BurningDreamsInPlay() => _state.Cards.Values.Any(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == BurningDreamsEffect);

    // Burning Dreams, clause 2: a copy whose "until your turn two turns from now begins" has not yet come.
    private bool BurningDreamsNegationActive() => _state.Cards.Values.Any(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == BurningDreamsEffect
        && c.AbilityUses.TryGetValue(BurningDreamsUntilKey, out var until) && Player(c.OwnerId).TurnsTaken < until);

    // The actions that discard the Imperial Favor as their cost ("as a condition upon further effects"): the one list RunAbilityEffects and
    // ResolveNegatedAction discard the Favor for.
    // Issue #501: the list became data -- each of these abilities declares CostVerbs.DiscardFavor among its Costs (EmperorMechE.cs, FavorDiscardMigrated), which is what this reads.
    private static bool DiscardsImperialFavorAsCost(string abilityId) => DeclaresFavorDiscard(abilityId);

    // Burning Dreams, clause 2 (ExecuteAbilityEffects' hook, asked as the action resolves -- after any redirect): the action's effects are negated
    // when it discards the Favor as its cost and targets a Nonhuman Personality. The whole action is still taken and every cost (the Favor included) paid.
    private bool BurningDreamsNegates(GameCommand command)
    {
        if (command.AbilityId is not { } id || !(DiscardsImperialFavorAsCost(id) || ChoseFavorDiscardOption(command)) || MechEUnnegatable(id) || !BurningDreamsNegationActive()) return false; // issue #501: The Heir's Wrath's effects will not be negated // issue #501: or the Favor discard chosen as an alternative cost
        var nonhuman = ActionTargetIds(command).Where(t => _state.Cards.ContainsKey(t)).Select(Card)
            .FirstOrDefault(t => Definition(t).Type == CardType.Personality && Keyword(t, "Nonhuman"));
        if (nonhuman is null) return false;
        Emit("actionNegated", $"Burning Dreams: the action targets {Definition(nonhuman).Name}, a Nonhuman Personality, so its effects are negated.", command.PlayerId, nonhuman.Id);
        return true;
    }

    // Burning Dreams, clause 3 (RequireCanPerform's hook): "Nonhumans may not perform Political actions." The performer is the one RequireCanPerform names
    // (the chosen Personality/Follower performer, else the source card), an Item's or Spell's action being performed by its wielder; the rulebook Lobby is a
    // "Political Limited" action performed by the Personality it bows (its target), Doji Dainagon's by Dainagon himself.
    private void RequireBurningDreamsAllows(GameCommand command, AbilityDefinition ability, CardInstance? source)
    {
        if (!IsAction(ability) || !BurningDreamsInPlay() || command.AbilityId is not { } id) return;
        var political = id is "lobby" or "dainagon-lobby" || ActionCarriesKeyword(ability, source, "Political");
        if (!political) return;
        var performers = new List<CardInstance>();
        if (id == "lobby") { if (command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var lobbying)) performers.Add(lobbying); }
        else
        {
            var named = ability.Performer is not null && command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var chosen) ? chosen : null;
            var actor = named is not null && Definition(named).Type is CardType.Personality or CardType.Follower ? named : source;
            if (actor is { Zone: Zone.Attached, AttachedTo: { } hostId } && Definition(actor).Type is CardType.Item or CardType.Spell) actor = Card(hostId);
            if (actor is not null) performers.Add(actor);
            if (ability.Slots is not null) performers.AddRange(PerformingSlotCards(command, ability));
        }
        foreach (var performer in performers)
            if (Definition(performer).Type is CardType.Personality or CardType.Follower && Keyword(performer, "Nonhuman"))
                RequireNotBarred(false, $"{Definition(performer).Name} performing Political actions", "Burning Dreams");
    }

    // ---- Colonial Harbor: "Your Stronghold's abilities have Tireless" ---------------------------------------------------------------------------------
    // Tireless (core rules, Reading the Cards): the action may be taken even while the card is bowed. Read wherever a bowed source bars an ability.
    // A "Bow this card" cost still cannot be paid by a bowed Stronghold (core rules, Pay Costs: a cost must fully happen).
    private bool TirelessStronghold(CardInstance card) =>
        Definition(card).Type == CardType.Stronghold && InPlay(card)
        && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Effect == ColonialHarborEffect);

    // The one "does being bowed bar this card's abilities?" question for the reaction windows that ask it themselves.
    private bool BowedBarsAbilities(CardInstance card) => card.Bowed && !TirelessStronghold(card);

    // ---- Static Force and Chi --------------------------------------------------------------------------------------------------------------------------
    // PersonalityForce's and FollowerForce's hook.
    private int ForgottenLegacyBatch5Force(CardInstance card)
    {
        var bonus = 0;
        // The Second City (oracle-l5r-8303, a Region): "Each of your cards that is Courtier or Magistrate has +1F." Live while the Region is attached.
        if (InPlay(card) && (Keyword(card, "Courtier") || Keyword(card, "Magistrate"))
            && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && c.Zone == Zone.Region && Definition(c).Effect == SecondCityEffect))
            bonus += 1;
        // Pearl of Rage (oracle-l5r-5903, a Spell): "This Shugenja has +1F for each Pearl card you control and in your discard piles."
        if (Definition(card).Type == CardType.Personality)
        {
            // Issue #888: the Pearl count scans every card, so it is taken only when a Pearl of Rage is attached (the product is 0 otherwise).
            var pearlsOfRage = StaticSpells(card).Count(a => Definition(a).Effect == PearlOfRageEffect);
            if (pearlsOfRage > 0) bonus += pearlsOfRage * PearlCount(card.OwnerId);
        }
        return bonus;
    }

    // "Each Pearl card you control and in your discard piles": in play under the player's control, or in any of the player's own discard piles (a dead
    // Personality's pile included).
    private int PearlCount(int playerId) => _state.Cards.Values.Count(c => c.OwnerId == playerId && Keyword(c, "Pearl")
        && (InPlay(c) || c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead));

    // Ryoshun's Guidance (oracle-l5r-6425, a Celestial): "Your Personalities have +1C while they are not involved in a duel." PersonalityChi's hook.
    // Involved in a duel: either duelist of the pending duel, from the challenge until it resolves.
    private int RyoshunChiBonus(CardInstance card) =>
        InPlay(card) && Definition(card).Type == CardType.Personality && ControlsCelestialEffect(card.OwnerId, RyoshunsGuidanceEffect)
        && !(_state.PendingDuel is { } duel && (duel.ChallengerId == card.Id || duel.ChallengedId == card.Id)) ? 1 : 0;

    // When the Celestial leaves play the +1C goes with it; a Personality left at 0 Chi is destroyed (core rules, Chi: "if a Personality's Chi is ever zero").
    private void CheckChiAfterRyoshunLeaves(int ownerId)
    {
        var dying = _state.Cards.Values.Where(c => c.OwnerId == ownerId && InPlay(c) && Definition(c).Type == CardType.Personality && PersonalityChi(c) <= 0).ToArray();
        if (dying.Length > 0) DestroyUnits(dying, true);
    }

    // ---- The Mountain's Power: "While this Shugenja is Earth and defending, negate his destruction from other players' cards' effects." --------------
    // DestructionShielded's hook (an action's effect or an Attack from another player; a battle's resolution is not a card's effect and never asks).
    private bool MountainsPowerNegates(CardInstance card, int actingPlayerId)
    {
        if (actingPlayerId == card.OwnerId || Definition(card).Type != CardType.Personality || !Keyword(card, "Earth")) return false;
        if (!StaticSpells(card).Any(a => Definition(a).Effect == MountainsPowerEffect)) return false;
        if (!AtBattle(card) || card.OwnerId == _state.ActivePlayerId) return false; // defending: at the current battle, on the non-active player's side
        Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated (The Mountain's Power).", card.OwnerId, card.Id);
        return true;
    }

    // ---- Wind of the Moon: "While this Shugenja is Air, he straightens during each player's Straighten Phase." ---------------------------------------
    // FinishStraightening's hook: the Straighten Phase of the OTHER player (his own already straightened him).
    private void StraightenWindOfTheMoonShugenja(PlayerState activePlayer)
    {
        foreach (var shugenja in _state.Cards.Values.Where(c => c.OwnerId != activePlayer.Id && InPlay(c) && c.Bowed && Definition(c).Type == CardType.Personality && Keyword(c, "Air")
                     && StaticSpells(c).Any(a => Definition(a).Effect == WindOfTheMoonEffect)).ToArray())
            Straighten(shugenja);
    }

    // ---- Ryoshun's Guidance's redirect ---------------------------------------------------------------------------------------------------------------
    // "At the same location": a battlefield or a player's Home, an attachment standing where its host does (a Region, Celestial or Stronghold stands at neither).
    private string? LocationOf(CardInstance card)
    {
        var at = card.Zone == Zone.Attached && card.AttachedTo is string hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : card;
        return at.Zone switch
        {
            Zone.Battlefield => $"battlefield:{at.ProvinceIndex}",
            Zone.Home => $"home:{at.OwnerId}",
            _ => null
        };
    }

    private IEnumerable<CardInstance> RyoshunRedirectCandidates(CardInstance celestial, ReactionSubject subject, CardInstance affected) =>
        LocationOf(affected) is not { } location ? []
        : _state.Cards.Values.Where(c => c.OwnerId == celestial.OwnerId && c.Id != affected.Id && InPlay(c) && Definition(c).Type != CardType.Stronghold
                && LocationOf(c) == location && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- Validation hook (ValidateAbility, after the per-id Requires) --------------------------------------------------------------------------------
    private void ValidateForgottenLegacyBatch5(string id, AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        switch (id)
        {
            case "assault-jungle-destroy":
                Require(AssaultInTheJungleActive(), "No Assault in the Jungle is in effect.");
                break;
            // Ageless Shrine: "Straighten one of his attachments if he is Nonhuman" -- one attachment must be named exactly when he is Nonhuman and has one.
            case "ageless-shrine-straighten" when ability.Slots is not null:
                {
                    var him = SlotCards(command, "target").FirstOrDefault();
                    var named = SlotCards(command, "attachment").Count();
                    var owed = him is not null && Keyword(him, "Nonhuman") && Attachments(him.Id).Any() ? 1 : 0;
                    Require(named == owed, owed == 1 ? "Choose one of his attachments to straighten." : "Only a Nonhuman's attachment is straightened.");
                    break;
                }
            // The Second City: the named province must hold a face-down card.
            case "second-city-reveal" or "second-city-reveal-raise" when command.TargetPlayerId is int cityOwner && command.Province is int cityIndex:
                {
                    Require(cityIndex >= 0 && cityIndex < Player(cityOwner).Provinces.Count, "Choose a province holding a face-down card.");
                    var province = Player(cityOwner).Provinces[cityIndex];
                    Require(province.CardId is { } hiddenId && !Card(hiddenId).FaceUp, "Choose a province holding a face-down card.");
                    break;
                }
            // Black Pearl: "a Personality in any player's discard pile" brought into play under the acting player's control, paying all costs.
            case "black-pearl-recruit" when command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var pearlTarget):
                {
                    var quote = QuotePurchase(command.PlayerId, pearlTarget.Id, fromDeadPile: true, anyOwnerPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                    break;
                }
        }
    }

    // ---- Execution hook (RunAbilityEffects' default) ---------------------------------------------------------------------------------------------------
    // null: not one of this batch's abilities. false: its effects are done, the action finishes as usual. true: a window opened, the caller returns.
    private bool? RunForgottenLegacyBatch5(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Thunder's Wrath: "Give it -4F."
            case "thunders-wrath-bow":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                return false;
            // Thunder's Wrath: the Spell was destroyed as the cost; the wielder (the named performer) is "this Shugenja".
            case "thunders-wrath-attack":
                {
                    var shugenja = Card(command.PerformerId!);
                    _thundersWrathFollowerDestroyed = false;
                    if (ResolveOrDeferAttack(id, player, target?.Id, shugenja.Id, consumedGrant)) return true;
                    if (ThundersWrathFollowUpDue(shugenja))
                        return BeginSequencedAttackTarget("thunders-wrath-followup-attack", command, shugenja, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                    return false;
                }
            // Assault in the Jungle's granted ability: the performer's unit was bowed as the cost (CostVerbs.BowUnit).
            case "assault-jungle-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            // Burning Dreams has no ability of its own; Willing Spirits neither.
            // Ageless Shrine: "Straighten him. Straighten one of his attachments if he is Nonhuman."
            case "ageless-shrine-straighten":
                foreach (var him in SlotCards(command, "target").ToArray()) Straighten(him);
                foreach (var attachment in SlotCards(command, "attachment").ToArray()) Straighten(attachment);
                return false;
            // Bamboo Harvesters - experienced: "Draw a card. If you now have 6 or more cards in your hand, discard a card." The discard is the player's own
            // choice, queued as a forced discard (Times of Strife's shape) that opens as the action completes.
            case "bamboo-exp-draw":
                Draw(player);
                if (player.Hand.Count >= 6) _state.PendingForcedDiscards.Add(new(player.Id, Definition(source!).Name));
                return false;
            // Border Keep - experienced: "put one to three cards in your hand at the bottom of your deck. Draw cards equal to the number you put at the bottom."
            // The cards are hidden: the log says how many, never which.
            case "border-keep-exp-cycle":
                {
                    var cycled = SlotCards(command, "cards").ToArray();
                    foreach (var card in cycled)
                    {
                        player.Hand.Remove(card.Id);
                        card.Zone = Zone.FateDeck; card.FaceUp = false; ResetCardState(card);
                        player.FateDeck.Add(card.Id);
                    }
                    Emit("toDeck", $"{Definition(source!).Name}: {player.Name} put {cycled.Length} card{(cycled.Length == 1 ? "" : "s")} from their hand on the bottom of their Fate deck.", player.Id, source!.Id);
                    for (var drawn = 0; drawn < cycled.Length; drawn++) Draw(player);
                    return false;
                }
            // Black Pearl: the Pearl's destruction was the cost and the Gold (4 less for a Nonhuman) was paid; he enters at home, under the acting player's control.
            case "black-pearl-recruit":
                {
                    var quote = QuotePurchase(player.Id, target!.Id, fromDeadPile: true, anyOwnerPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                    TakeControl(target, player.Id);
                    return BringDeadPersonalityIntoPlay(player, target, "Black Pearl", null);
                }
            // Cursed Relic: the -1C was paid as the cost (CostVerbs.ChiPenalty).
            case "cursed-relic-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            case "cursed-relic-remove":
                RemoveFromGame(target!);
                return false;
            // The Second City: "Turn the face-down card face-up. You may increase the Province's strength by 2" -- the "may" is the choice between the two abilities (one use).
            case "second-city-reveal":
            case "second-city-reveal-raise":
                {
                    var owner = Player(command.TargetPlayerId!.Value);
                    var province = owner.Provinces[command.Province!.Value];
                    var revealed = Card(province.CardId);
                    revealed.FaceUp = true;
                    Emit("provinceRevealed", $"{Definition(source!).Name}: {Definition(revealed).Name} in {owner.Name}'s province {province.Index + 1} is turned face-up.", player.Id, revealed.Id);
                    if (id == "second-city-reveal-raise")
                    {
                        GiveProvinceStrengthThisTurn(owner.Id, province.Index, 2, Definition(source!).Name); // until end of turn (issue #492 ruling R3)
                    }
                    return false;
                }
            // Pearl of Rage: "Ranged Attack with strength equal to this Shugenja's Chi, plus the number of Pearl cards you control and in your discard piles."
            case "pearl-of-rage-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // The Mountain's Power: "Bow it. Gain 1 Honor."
            case "mountains-power-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            // Wind of the Moon: "Give him +1PH or +1 Gold Cost" (this turn: no duration is printed).
            case "wind-moon-honor":
                ApplyModifier(target!, source!.Id, id, "PersonalHonor", 1);
                return false;
            case "wind-moon-gold":
                ApplyModifier(target!, source!.Id, id, "GoldCost", 1);
                return false;
            // Ryoshun's Guidance: the Celestial was discarded as the cost; the held action now targets the chosen card, and the +1C it gave is gone.
            case "ryoshun-redirect":
                RedirectAction(HeldAction(), target!.Id, source!);
                CheckChiAfterRyoshunLeaves(source!.OwnerId);
                return false;
            default:
                return null;
        }
    }

    // ---- Thunder's Wrath's follow-up Attack -------------------------------------------------------------------------------------------------------------
    // "If this Shugenja is Thunder and the Ranged Attack destroyed a Follower, Ranged 4 Attack." Whether THIS Attack destroyed a Follower is noted where every Attack resolves
    // (ResolveAttackNow: its target was in play before and is not after -- a negated or shielded destruction leaves it in play, Sticks and Stones' reading), and read
    // straight after, by the execution case when the Attack resolved at once or by ResolveAttack when it waited for a reaction window. Transient: never carried past the
    // command that resolves the Attack.
    private bool _thundersWrathFollowerDestroyed;

    private void NoteThundersWrathAttack(string abilityId, string? targetId, bool targetInPlayBefore)
    {
        if (abilityId != "thunders-wrath-attack") return;
        _thundersWrathFollowerDestroyed = targetId is not null && targetInPlayBefore && _state.Cards.TryGetValue(targetId, out var target)
            && Definition(target).Type == CardType.Follower && !InPlay(target);
    }

    private bool ThundersWrathFollowUpDue(CardInstance shugenja)
    {
        var due = _thundersWrathFollowerDestroyed && InPlay(shugenja) && Keyword(shugenja, "Thunder");
        _thundersWrathFollowerDestroyed = false;
        return due;
    }

    // The deferred first Attack's own completion (ResolveAttack): the second target is handed over exactly as the immediate path does, and the action then finishes through the
    // ordinary tail once the second Attack has resolved (ResolveSequencedTargetCommand). The deferred path has no snapshot of what was bowed before the action: every card bowed
    // now counts as bowed before (an Attack bows nothing), and no Honor snapshot (an Attack costs the opponent none).
    private bool BeginThundersWrathFollowUpAfterDeferredAttack(PendingAttack attack, List<AdditionalActionGrant> grantsBeforeAttack)
    {
        if (attack.AbilityId != "thunders-wrath-attack" || attack.Command is null || attack.SourceId is null || !_state.Cards.TryGetValue(attack.SourceId, out var shugenja)) return false;
        if (attack.Negated) { _thundersWrathFollowerDestroyed = false; return false; }
        if (!ThundersWrathFollowUpDue(shugenja)) return false;
        var bowedNow = _state.Cards.Values.Where(c => c.Bowed).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return BeginSequencedAttackTarget("thunders-wrath-followup-attack", attack.Command, shugenja, attack.Additional, false, null, bowedNow, grantsBeforeAttack);
    }

    // ---- Attack strengths (AttackStrength's hook) -----------------------------------------------------------------------------------------------------
    private int? ForgottenLegacyBatch5AttackStrength(string abilityId, string? sourceId, CardInstance? target) => abilityId switch
    {
        // Thunder's Wrath: "Ranged 4 Attack with +2 strength if it targets a Follower", and the follow-up "Ranged 4 Attack".
        "thunders-wrath-attack" => 4 + (target is not null && Definition(target).Type == CardType.Follower ? 2 : 0),
        "thunders-wrath-followup-attack" => 4,
        "cursed-relic-attack" => 3,
        "pearl-of-rage-attack" => sourceId is not null && _state.Cards.TryGetValue(sourceId, out var pearl) && pearl.AttachedTo is string host
            ? PersonalityChi(Card(host)) + PearlCount(pearl.OwnerId) : 0,
        _ => null
    };
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterForgottenLegacyBatch5Hooks()
    {
        EngineHooks.AttackStrengthBefore.Register("ForgottenLegacyBatch5AttackStrength", 500, static (engine, context) => engine.ForgottenLegacyBatch5AttackStrength(context.abilityId, context.sourceId, context.target));
        EngineHooks.PersonalityForce.Register("ForgottenLegacyBatch5Force", 800, static (engine, context) => engine.ForgottenLegacyBatch5Force(context.card));
        EngineHooks.ValidateEarly.Register("ValidateForgottenLegacyBatch5", 100, static (engine, context) => engine.ValidateForgottenLegacyBatch5(context.id, context.ability, context.command, context.source));
        EngineHooks.RunEffects.Register("RunForgottenLegacyBatch5", 900, static (engine, context) => engine.RunForgottenLegacyBatch5(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
