namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #501, MECHANICS GROUP B: CONTROL TRANSFER (Mendon's items 6, 7, 8, 9 and 10; item 32's engine side is EmperorMechBCopies.cs).
//
// Mendon's rulings (verbatim in the brief, summarised):
//   6.  Giving control (Goju Asuka) works the same way as taking control; giving originates from the controller, taking from the other player. Asuka, after being recruited, is transferred to the
//       receiving player's home: optional for her controller, not for the receiver.
//   7.  Gaining control of a Personality: in the Action Phase the entire unit goes to the taker's home in whatever state it is; during battle the unit leaves its controller's side of the battlefield for
//       the other side there. The taker uses him and his attachments as though he had recruited them, until an effect returns him. It is not a recruit, not an entering play, and triggers no Proclaim.
//   8.  Control given to a card's OWNER: the Personality moves from its controller's home / battlefield side to its owner's home / side, in its current state (dishonored stays dishonored, new
//       attachments are kept). Yoritomo Sachina's return triggers the next time she leaves play, whenever that is, immediately, and cannot be negated.
//   9.  Black Marketeering is a time-bound take (item 7), and may be paid in part by Luxurious Silk (it is not a recruit).
//   10. Only a Shadow grants Goju Asuka's non-straightening trait (against non-Ninja) and gives control of the Personality who gains it to the opponent.
//
// The engine reads CardInstance.OwnerId as the CONTROLLER of an in-play card everywhere ("you control", sides, homes, armies, Gold sources, Unique/Singular), and CardInstance.OriginalOwnerId as the
// real OWNER while another player controls it; every card returns to its OWNER's piles the moment it leaves play (AttachAction.cs, ReturnCardsOutOfPlayToTheirOwners; ReturnToHand / ToOwnerDeck restore
// the owner first). So a control change is: OwnerId := new controller for the Personality and every card attached to him. Where he is follows from that: a card at home is in its controller's home,
// a card at a battlefield is on its controller's side. Nothing enters play, so no entry trait, Reaction or Proclaim fires, and nothing about the card is reset.
//
// New primitives:
//   * TransferUnitControl: the one whole-unit control change (take, give, return). Loyal and Unique/Singular refuse a take or give (the 2026-09-12 control-transfer decision); an attachment of the unit
//     the new controller already has a Unique/Singular copy of is DISCARDED (core rules, Singular/Unique). "controlNegated": an action's control change is negated by a negation of all its effects on the
//     Personality (NegatesEffect kind "control"); a return to the owner never is.
//   * CONTROL RETURNS (MatchState.MechB.ControlReturns): "give control of him to his owner" owed later, on one of three triggers -- the turn ending (Black Marketeering), a card leaving play (Sachina)
//     or an ability being announced again (Shadow Dragon). A return is never negated. A return blocked by Unique/Singular (the owner now controls another copy) waits, and is retried after every
//     command, until that copy leaves play or his control (the 2026-09-12 decision).
//   * THE INFILTRATOR STRAIGHTEN TRAIT ("After a Straighten Phase begins: Choose your bowed non-X Personality. Negate his straightening this phase."): printed on Goju Asuka (non-Spider Clan) and granted
//     permanently by Only a Shadow (non-Ninja, MatchState.MechB.InfiltratorGrants). At the start of its controller's Straighten Phase the controller chooses (a "infiltratorStraighten" pending choice
//     answered by "chooseInfiltratorStraighten" with a TargetId when there is more than one candidate) and the chosen Personality gets a StraightenPhaseEnds hold (StraightenHolds.cs).
//   * ASUKA'S DEFENDING-ARMY BAR: "may not assign, and will not move, into a defending army" -- Assign (defence), the created-battlefield assignment and MoveToBattlefield refuse it; the defence offer
//     leaves her out.
// ======================================================================================================================================================================================

/// <summary>What the control-transfer group keeps between commands (issue #501).</summary>
public sealed class EmperorMechBState
{
    /// <summary>Returns of control owed later, in the order they were registered.</summary>
    public List<ControlReturn> ControlReturns { get; set; } = [];
    public int ControlReturnSequence { get; set; }
    /// <summary>Personalities permanently given the infiltrator straighten trait (Only a Shadow).</summary>
    public List<InfiltratorGrant> InfiltratorGrants { get; set; } = [];
    /// <summary>The infiltrator trait sources already asked in the Straighten Phase under way ("source key|turn").</summary>
    public List<string> InfiltratorHandled { get; set; } = [];
    /// <summary>The open infiltrator choice: which Personalities are excluded (a keyword), and the source's name for the log.</summary>
    public string? OpenInfiltratorExcluded { get; set; }
    public string? OpenInfiltratorSource { get; set; }
}

/// <summary>"Give control of him to his owner", owed on a trigger (ControlReturnTriggers). Blocked while his owner controls another copy of a Unique/Singular card with his title.</summary>
public sealed class ControlReturn
{
    public int Seq { get; set; }
    public string CardId { get; set; } = "";
    public string Trigger { get; set; } = ControlReturnTriggers.TurnEnds;
    /// <summary>The card whose leaving play (SourceLeavesPlay) or whose ability's announcement (AbilityAnnounced) triggers the return.</summary>
    public string? SourceId { get; set; }
    public string? AbilityId { get; set; }
    public string Reason { get; set; } = "";
    public bool Blocked { get; set; }
    /// <summary>SourceStraightens only (issue #690): the source has been seen bowed since the return was registered, so the next time it is unbowed it has straightened.</summary>
    public bool SourceSeenBowed { get; set; }
    /// <summary>SourceStraightens only (issue #827, P'an Ku): while this return is owed the source may remain bowed -- every straightening of it is its controller's choice.</summary>
    public bool RemainBowed { get; set; }
    /// <summary>SourceStraightens only (issue #827, P'an Ku): the taken Personality's controller may not order Seppuku for him while this return is owed.</summary>
    public bool NoSeppuku { get; set; }
}

/// <summary>A permanent grant of the infiltrator straighten trait: "Choose your bowed non-<see cref="ExcludedKeyword"/> Personality. Negate his straightening this phase."</summary>
public sealed class InfiltratorGrant
{
    public string CardId { get; set; } = "";
    public string ExcludedKeyword { get; set; } = "Ninja";
    public string Source { get; set; } = "";
}

public static class ControlReturnTriggers
{
    /// <summary>"Give control of him to his owner before the turn ends" (Black Marketeering): at the top of EndTurn.</summary>
    public const string TurnEnds = "turn-ends";
    /// <summary>"After the next time this game Sachina leaves play, give control of him to his owner."</summary>
    public const string SourceLeavesPlay = "source-leaves-play";
    /// <summary>"Return control of him to his owner after the next time this game this ability is announced" (Shadow Dragon).</summary>
    public const string AbilityAnnounced = "ability-announced";
    /// <summary>"After P'an Ku [straightens or leaves play], give control of the target to his owner" (issue #690, P24, EffectPrimitivesTokens.cs): the source straightening, or leaving play.</summary>
    public const string SourceStraightens = "source-straightens";
}

public static class EmperorMechBAbilities
{
    public const string GojuAsukaEffect = "goju-asuka";
    public const string GojuAsukaGive = "goju-asuka-give";
    public const string ShadowDragonTake = "shadow-dragon-exp2-take";
    public const string ShadowDragonReturn = "shadow-dragon-exp2-return";
    public const string DarkNagaForest = "dark-naga-forest-negate";
    public const string DarkNagaBattle = "dark-naga-take";
    public const string SachinaTake = "sachina-exp-take";
    public const string BlackMarketeering = "black-marketeering-take";
    public const string OnlyAShadow = "only-a-shadow-infiltrate";
    /// <summary>"An enemy Personality whose unit's total Gold cost is 10 or lower" (Black Marketeering).</summary>
    public const string EnemyPersonalityUnitGold10 = "mechb-enemy-personality-unit-gold-10";

    public static Dictionary<string, AbilityDefinition> AddEmperorMechB(this Dictionary<string, AbilityDefinition> all)
    {
        // Goju Asuka (oracle-l5r-10087): "After Asuka enters play: You may give control of her to another player." A trait (no "Reaction:" designator) modelled as an optional entering-play
        // Reaction of her own (TraitReactions); her other two traits are her Effect (GojuAsukaEffect).
        all[GojuAsukaGive] = new("Reaction", "After Asuka enters play: You may give control of her to another player", "none", Trigger: "own-enters-play");
        // Shadow Dragon - exp2 (oracle-l5r-6653): "Shadow Dragon's abilities will not be copied." (NotCopyable on both.) "Battle: Target an enemy Personality: Permanently give him Ninja and
        // Shadowlands. Take control of him. Return control of him to his owner after the next time this game this ability is announced."
        all[ShadowDragonTake] = new("Battle", "Target an enemy Personality: Permanently give him Ninja and Shadowlands. Take control of him. Return control of him to his owner after the next time this game this ability is announced",
            "enemy-personality", NotCopyable: true);
        // "Open: If Shadow Dragon is dead: Bring it into play, paying all costs." (Kunji - exp's paid half: an ability taken by its own card from the dead pile.)
        all[ShadowDragonReturn] = new("Open", "If Shadow Dragon is dead: Bring it into play, paying all costs", "source-if-dead", FromDead: true, Cost: GoldCostRule.TargetRecruit(), NotCopyable: true);
        // The Dark Naga (oracle-l5r-8012): "Reaction: After another player targets The Dark Naga with an action: You may bow one of your Forests. If you did, negate The Dark Naga's destruction
        // from the action's effects." The Forest is the Reaction's target, bowed as its cost (Kyuden Kitsune's "your target Forest").
        all[DarkNagaForest] = new("Reaction", "After another player targets The Dark Naga with an action: You may bow one of your Forests. If you did, negate The Dark Naga's destruction from the action's effects",
            "own-unbowed-card-with-keyword", RequiredKeywords: ["Forest"], Trigger: ReactionTriggers.TargetedByAction, Costs: ActionCost.BowTarget);
        // "Battle: Target an enemy Personality: If he is Nonhuman, take control of him (he joins your army). If he is human, bow his unit. Destroy him before this turn ends."
        all[DarkNagaBattle] = new("Battle", "Target an enemy Personality: If he is Nonhuman, take control of him (he joins your army). If he is human, bow his unit. Destroy him before this turn ends", "enemy-personality");
        // Yoritomo Sachina - exp (oracle-l5r-9623): "Sachina's base ability will not be copied. Political Limited: Once per game, target a Personality, pay Gold equal to his unit's total Gold Cost
        // plus four, and bow Sachina: Take control of him. After the next time this game Sachina leaves play, give control of him to his owner."
        all[SachinaTake] = new("Limited", "Political Limited: Once per game, target a Personality, pay Gold equal to his unit's total Gold Cost plus four, and bow Sachina: Take control of him. After the next time this game Sachina leaves play, give control of him to his owner",
            "personality", ActionKeywords: ["Political"], OncePerGame: true, NotCopyable: true, Cost: GoldCostRule.TargetUnitTotal(4), Costs: ActionCost.BowSource);
        // Black Marketeering (oracle-l5r-10137, a Strategy, Gold Cost 10): "Battle: Target an enemy Personality whose unit's total Gold cost is 10 or lower: He defects. Take control of him (he joins
        // your army). Dishonor him. Destroy any of his Followers whose Honor Requirement he now fails to meet. Give control of him to his owner before the turn ends."
        all[BlackMarketeering] = new("Battle", "Target an enemy Personality whose unit's total Gold cost is 10 or lower: He defects. Take control of him (he joins your army). Dishonor him. Destroy any of his Followers whose Honor Requirement he now fails to meet. Give control of him to his owner before the turn ends",
            EnemyPersonalityUnitGold10);
        // Only a Shadow (oracle-l5r-10161, a Ninja Strategy): "Limited: Choose your performing unbowed Ninja Personality: He infiltrates the enemy ranks. Permanently give him the trait, "After a
        // Straighten Phase begins: Choose your bowed non-Ninja Personality. Negate his straightening this phase." Give control of him to another player." (The scan names a "Ninja Personality",
        // not the Oracle's "base Ninja keyword".)
        all[OnlyAShadow] = new("Limited", "Choose your performing unbowed Ninja Personality: He infiltrates the enemy ranks. Permanently give him the trait, \"After a Straighten Phase begins: Choose your bowed non-Ninja Personality. Negate his straightening this phase.\" Give control of him to another player",
            "none", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private EmperorMechBState MechB => _state.MechB;

    // Test seam: the ability ids a card has now (printed, granted, static grants; a copy without its "will not be copied" abilities). Never used outside tests.
    internal string[] AbilityIdsForTest(string cardId) => AbilityIdsOf(Card(cardId)).ToArray();
    // Test seam: an effect's move of a card to a battlefield (every "move to the battlefield" ability funnels through MoveToBattlefield). Never used outside tests.
    internal bool MoveToBattlefieldForTest(string cardId, int province) => MoveToBattlefield(Card(cardId), province);

    // ---- target categories ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool MechBTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        // Black Marketeering: the unit's total Gold Cost is the Personality's and every attachment's current Gold Cost (A Stain Cleansed's reading, UnitTotalGoldCost).
        EmperorMechBAbilities.EnemyPersonalityUnitGold10 => Definition(target).Type == CardType.Personality && target.OwnerId != player.Id && UnitTotalGoldCost(target, out _) <= 10,
        _ => false
    };

    // ---- the per-id conditions -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateEmperorMechB(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Shadow Dragon's Open half is Kunji - exp's "paying all costs": his ordinary entry price, and every restriction on bringing him into play (Unique, the Honor Requirement).
        if (id == EmperorMechBAbilities.ShadowDragonReturn && source is not null)
        {
            var quote = QuotePurchase(player.Id, source.Id, fromDeadPile: true);
            Require(quote.Error is null, quote.Error ?? "");
        }
    }

    // ---- execution: null when the id is not this group's; false when its effects are done; true when a window opened and the action waits ----------------------------------------------------------

    private bool? RunEmperorMechB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case EmperorMechBAbilities.GojuAsukaGive:
                TransferUnitControl(source!, 1 - player.Id, Definition(source!).Name, checkLoyal: true, fromEffect: false);
                return false;
            case EmperorMechBAbilities.ShadowDragonTake:
                {
                    var name = Definition(source!).Name;
                    GiveKeywordPermanently(target!, "Ninja", source!.Id);
                    GiveKeywordPermanently(target!, "Shadowlands", source.Id);
                    if (TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true))
                        RegisterControlReturn(target!, ControlReturnTriggers.AbilityAnnounced, source.Id, id, $"{name}: the next time this ability is announced");
                    return false;
                }
            case EmperorMechBAbilities.ShadowDragonReturn:
                return BringDeadPersonalityIntoPlay(player, source!, Definition(source!).Name, null);
            case EmperorMechBAbilities.DarkNagaForest:
                NegateEffects(source!, "destroy");
                return false;
            case EmperorMechBAbilities.DarkNagaBattle:
                {
                    var name = Definition(source!).Name;
                    if (Keyword(target!, "Nonhuman")) TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true);
                    else BowUnitByEffect(target!, player.Id);
                    // "Destroy him before this turn ends": the shared store's instruction to destroy (Deadly Orders' shape), carried out at the top of EndTurn if he is still in play.
                    ScheduleDelayedEffect(target!, DelayedKinds.Destroy, DelayUntil.EndOfTurn, null, name, source!.Id);
                    Emit("destructionScheduled", $"{Definition(target!).Name} will be destroyed before this turn ends ({name}).", player.Id, target!.Id);
                    return false;
                }
            case EmperorMechBAbilities.SachinaTake:
                {
                    var name = Definition(source!).Name;
                    if (TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true))
                        RegisterControlReturn(target!, ControlReturnTriggers.SourceLeavesPlay, source!.Id, null, $"{name}: the next time she leaves play");
                    return false;
                }
            case EmperorMechBAbilities.BlackMarketeering:
                {
                    var name = Definition(source!).Name;
                    if (TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true))
                        RegisterControlReturn(target!, ControlReturnTriggers.TurnEnds, source!.Id, null, $"{name}: before the turn ends");
                    Dishonor(target!, player.Id);
                    // "Any of his Followers whose Honor Requirement he now fails to meet": his current Personal Honor (0 once dishonorable) against each Follower's printed requirement.
                    foreach (var follower in Attachments(target!.Id).Where(a => Definition(a).Type == CardType.Follower && FollowerRequirementFailed(target, a)).ToArray())
                        if (InPlay(follower)) DestroyAttachment(follower);
                    return false;
                }
            case EmperorMechBAbilities.OnlyAShadow:
                {
                    var ninja = Card(command.PerformerId!);
                    MechB.InfiltratorGrants.Add(new InfiltratorGrant { CardId = ninja.Id, ExcludedKeyword = "Ninja", Source = Definition(source!).Name });
                    Emit("traitGranted", $"{Definition(ninja).Name} permanently has the trait \"After a Straighten Phase begins: Choose your bowed non-Ninja Personality. Negate his straightening this phase.\" ({Definition(source!).Name})", player.Id, ninja.Id);
                    TransferUnitControl(ninja, 1 - player.Id, Definition(source!).Name, checkLoyal: true, fromEffect: false);
                    return false;
                }
            default:
                return null;
        }
    }

    private bool FollowerRequirementFailed(CardInstance personality, CardInstance follower) =>
        Definition(follower).HonorRequirement is int requirement && !IgnoresFollowerHonorRequirement(personality) && !RoninIgnoresHonorRequirement(follower, personality.OwnerId)
        && CardPersonalHonor(personality) < requirement;

    // The Dark Naga's Reaction (TargetedActionReactionApplies' fallback): another player's action targets him, and he has a Forest to bow.
    private bool MechBTargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) => abilityId switch
    {
        EmperorMechBAbilities.DarkNagaForest => subject.Command.PlayerId != card.OwnerId && affected.Id == card.Id && NagaForests(card).Any(),
        _ => false
    };

    // "One of your Forests": an unbowed card in play with the Forest keyword that the Naga's controller controls (the Reaction's target, bowed as its cost).
    private IEnumerable<CardInstance> NagaForests(CardInstance naga) =>
        _state.Cards.Values.Where(c => c.OwnerId == naga.OwnerId && InPlay(c) && !c.Bowed && Keyword(c, "Forest")).OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- the whole-unit control change -------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Gives <paramref name="controllerId"/> control of the Personality's whole unit (items 6, 7, 8). Returns whether control changed. <paramref name="checkLoyal"/>: a take or a give is refused by
    /// Loyal; a return to the owner is not. <paramref name="fromEffect"/>: the change is an effect of an action on the Personality, so a negation of all that action's effects on him stops it (a give by
    /// the controller of his own card, and a return, are never stopped).</summary>
    private bool TransferUnitControl(CardInstance personality, int controllerId, string via, bool checkLoyal, bool fromEffect)
    {
        if (personality.OwnerId == controllerId || !InPlay(personality) || Definition(personality).Type != CardType.Personality) return false;
        var name = Definition(personality).Name;
        if (fromEffect && NegatesEffect(personality, "control"))
        {
            Emit("controlNegated", $"{via}: the change of control of {name} was negated.", controllerId, personality.Id);
            return false;
        }
        if (checkLoyal && !LoyaltyAllowsControl(personality, Player(controllerId).Clan, controllerId))
        {
            var gateReason = RecruitGateFor(personality, controllerId, Player(controllerId).Clan, RecruitGateKind.ControlChange).RefuseReason; // P3 (issue #695): a RecruitGate refusal says why (the legacy Loyal-like one keeps its words)
            Emit("controlRefused", $"{via}: {(gateReason is null or LoyalRecruitRefusal ? $"{name} is Loyal and cannot be controlled by {Player(controllerId).Name}." : gateReason)}", controllerId, personality.Id);
            return false;
        }
        if ((Keyword(personality, "Unique") || Keyword(personality, "Singular")) && ControlsCopyOf(controllerId, name, ExperienceLevel(personality)))
        {
            Emit("controlRefused", $"{via}: {Player(controllerId).Name} already controls a {name}.", controllerId, personality.Id);
            return false;
        }
        var from = personality.OwnerId;
        TakeControl(personality, controllerId);
        foreach (var attachment in Attachments(personality.Id).ToArray())
        {
            // Core rules (Singular / Unique): "Taking control of a unit carrying a duplicate Singular/Unique attachment discards the new copy."
            if ((Keyword(attachment, "Unique") || Keyword(attachment, "Singular")) && _state.Cards.Values.Any(c => c.OwnerId == controllerId && InPlay(c) && c.Id != attachment.Id && Definition(c).Name == Definition(attachment).Name))
            {
                DiscardAttachmentFromPlay(attachment, via);
                continue;
            }
            TakeControl(attachment, controllerId);
        }
        var where = personality.Zone == Zone.Battlefield ? $"joins {Player(controllerId).Name}'s army at the battlefield" : $"is now in {Player(controllerId).Name}'s home";
        Emit("unitControlChanged", $"{via}: {Player(controllerId).Name} now controls {name} (from {Player(from).Name}); his unit {where}.", controllerId, personality.Id);
        return true;
    }

    // An attachment leaves play for its owner's discard pile without being destroyed (no Expendable draw, no "after a card is destroyed").
    private void DiscardAttachmentFromPlay(CardInstance attachment, string via)
    {
        var host = attachment.AttachedTo is { } hostId ? Card(hostId) : null;
        if (host is not null) RemoveAttachmentGrants(host, attachment.Id);
        attachment.LastHostId = host?.Id;
        attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.FaceUp = true; attachment.Tokens.Clear();
        RestoreOwner(attachment);
        Emit("cardDiscarded", $"{Definition(attachment).Name} was discarded: its new controller already controls one ({via}).", attachment.OwnerId, attachment.Id);
        if (host is not null && InPlay(host) && PersonalityChi(host) <= 0) DestroyUnits([host], true);
    }

    // ---- returns of control ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void RegisterControlReturn(CardInstance personality, string trigger, string? sourceId, string? abilityId, string reason)
    {
        MechB.ControlReturns.Add(new ControlReturn { Seq = ++MechB.ControlReturnSequence, CardId = personality.Id, Trigger = trigger, SourceId = sourceId, AbilityId = abilityId, Reason = reason });
    }

    /// <summary>Carries out every return accepted by <paramref name="due"/>, in the order registered. A return is never negated; one blocked by Unique/Singular stays owed (and is retried after each command).</summary>
    private void ApplyControlReturns(Func<ControlReturn, bool> due)
    {
        if (MechB.ControlReturns.Count == 0) return;
        foreach (var entry in MechB.ControlReturns.Where(due).OrderBy(r => r.Seq).ToArray())
        {
            MechB.ControlReturns.Remove(entry);
            if (!ApplyControlReturn(entry)) MechB.ControlReturns.Add(entry);
        }
    }

    // False when the return is blocked and must wait; true when it is done (or has nothing left to do).
    private bool ApplyControlReturn(ControlReturn entry)
    {
        if (!_state.Cards.TryGetValue(entry.CardId, out var card) || !InPlay(card)) return true; // he left play: he is back in his owner's piles already
        var owner = card.OriginalOwnerId ?? card.OwnerId;
        if (card.OwnerId == owner) return true;
        var name = Definition(card).Name;
        if ((Keyword(card, "Unique") || Keyword(card, "Singular")) && _state.Cards.Values.Any(c => c.Id != card.Id && c.OwnerId == owner && InPlay(c) && Definition(c).Name == name))
        {
            if (!entry.Blocked) Emit("controlReturnBlocked", $"{entry.Reason}: {name} cannot return to {Player(owner).Name} while {Player(owner).Name} controls another {name}; he will return once it leaves play or {Player(owner).Name}'s control.", owner, card.Id);
            entry.Blocked = true;
            return false;
        }
        Emit("controlReturned", $"{entry.Reason}: control of {name} is given to his owner, {Player(owner).Name}.", owner, card.Id);
        TransferUnitControl(card, owner, entry.Reason, checkLoyal: false, fromEffect: false);
        return true;
    }

    // After every command (the out-of-play funnel's hook): a return owed to a card that left play is carried out, and a blocked one is tried again. Returns of cards no longer in play are dropped.
    private void ApplyControlReturnsAfterCommand()
    {
        if (MechB.ControlReturns.Count == 0) return;
        MechB.ControlReturns.RemoveAll(r => !_state.Cards.TryGetValue(r.CardId, out var c) || !InPlay(c) || (c.OriginalOwnerId ?? c.OwnerId) == c.OwnerId);
        ApplyControlReturns(r => r.Blocked || r.Trigger == ControlReturnTriggers.SourceLeavesPlay && (r.SourceId is null || !_state.Cards.TryGetValue(r.SourceId, out var s) || !InPlay(s))
            || SourceStraightenedReturnDue(r)); // issue #690 (P24): ControlReturnTriggers.SourceStraightens
    }

    // AfterCardDestroyed's hook: Sachina's "after the next time this game Sachina leaves play" happens at once when she is destroyed (other ways out of play are caught after the command).
    private void ControlReturnsOnDestruction(CardInstance destroyed) =>
        ApplyControlReturns(r => r.Trigger == ControlReturnTriggers.SourceLeavesPlay && r.SourceId == destroyed.Id && !InPlay(destroyed));

    // UseAbility's hook: "after the next time this game this ability is announced" (Shadow Dragon) -- the announcement of the same ability of the same card.
    private void ControlReturnsOnAnnouncement(GameCommand command)
    {
        if (MechB.ControlReturns.Count == 0 || command.AbilityId is null) return;
        ApplyControlReturns(r => r.Trigger == ControlReturnTriggers.AbilityAnnounced && r.AbilityId == command.AbilityId && r.SourceId == command.CardId);
    }

    // EndTurn's hook: "give control of him to his owner before the turn ends".
    private void ControlReturnsAtTurnEnd() => ApplyControlReturns(r => r.Trigger == ControlReturnTriggers.TurnEnds);

    // ---- Goju Asuka: "Asuka may not assign, and will not move, into a defending army." --------------------------------------------------------------------------------------------------------

    private static bool IsGojuAsuka(CardInstance card) => Definition(card).Effect == EmperorMechBAbilities.GojuAsukaEffect && Definition(card).Type == CardType.Personality;

    // Assign's hook (a Defender's assignment) and the created-battlefield assignment's.
    private void RequireAsukaMayAssign(CardInstance card, bool isDefender) =>
        RequireNotBarred(!isDefender || !IsGojuAsuka(card), "Assigning into a defending army", Definition(card).Name);

    // MoveToBattlefield's hook: a move to a battlefield puts the unit on its controller's side, which is the defending army when its controller is not the Attacker.
    private bool AsukaNegatesMove(CardInstance card)
    {
        if (!IsGojuAsuka(card) || card.OwnerId == _state.ActivePlayerId || _state.CurrentBattleProvince is null && _state.UnresolvedBattlefields.Count == 0) return false;
        Emit("moveNegated", $"{Definition(card).Name} will not move into a defending army.", card.OwnerId, card.Id);
        return true;
    }

    // ---- the infiltrator straighten trait ------------------------------------------------------------------------------------------------------------------------------------------------------

    // Every copy of the trait its controller has in play: Goju Asuka's printed one (non-Spider Clan) and each Only a Shadow grant (non-Ninja), in a stable order.
    private IEnumerable<(string Key, CardInstance Source, string Excluded, string SourceName)> InfiltratorSources(int playerId)
    {
        foreach (var asuka in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && IsGojuAsuka(c)).OrderBy(c => c.Id, StringComparer.Ordinal))
            yield return ($"{asuka.Id}|printed", asuka, "Spider Clan", Definition(asuka).Name);
        for (var i = 0; i < MechB.InfiltratorGrants.Count; i++)
        {
            var grant = MechB.InfiltratorGrants[i];
            if (_state.Cards.TryGetValue(grant.CardId, out var card) && card.OwnerId == playerId && InPlay(card) && Definition(card).Type == CardType.Personality)
                yield return ($"{card.Id}|grant{i}", card, grant.ExcludedKeyword, $"{Definition(card).Name} ({grant.Source})");
        }
    }

    private bool InfiltratorExcludes(CardInstance personality, string excluded) =>
        excluded == "Spider Clan" ? Keyword(personality, "Spider Clan") || HasClanAffiliation(personality, "Spider") : Keyword(personality, excluded);

    private CardInstance[] InfiltratorCandidates(int playerId, string excluded) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && c.Bowed && !InfiltratorExcludes(c, excluded))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();

    private void HoldForInfiltrator(CardInstance personality, int playerId, string sourceName)
    {
        AddStraightenHold(personality, sourceName, playerId, StraightenPoint.StraightenPhaseEnds);
        Emit("straighteningNegated", $"{sourceName}: {Definition(personality).Name}'s straightening is negated this phase.", playerId, personality.Id);
    }

    /// <summary>ContinueStraightenPhase's hook ("After a Straighten Phase begins"): each copy of the trait the turn's player controls is applied in turn; with several candidates the player chooses and the
    /// phase waits (true). Only the Straighten Phase of the trait's controller is one in which his cards straighten, so only that one asks.</summary>
    private bool BeginInfiltratorStraighten(PlayerState player)
    {
        MechB.InfiltratorHandled.RemoveAll(k => !k.EndsWith($"|{_state.TurnNumber}", StringComparison.Ordinal));
        foreach (var (key, source, excluded, sourceName) in InfiltratorSources(player.Id).ToArray())
        {
            var handledKey = $"{key}|{_state.TurnNumber}";
            if (MechB.InfiltratorHandled.Contains(handledKey)) continue;
            MechB.InfiltratorHandled.Add(handledKey);
            var candidates = InfiltratorCandidates(player.Id, excluded);
            if (candidates.Length == 0) continue;
            if (candidates.Length == 1) { HoldForInfiltrator(candidates[0], player.Id, sourceName); continue; }
            MechB.OpenInfiltratorExcluded = excluded; MechB.OpenInfiltratorSource = sourceName;
            _state.PendingChoice = new("infiltratorStraighten", player.Id, source.Id, $"{sourceName}: choose your bowed non-{excluded} Personality. His straightening is negated this phase.");
            _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
            return true;
        }
        return false;
    }

    private void ResolveInfiltratorStraighten(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        var excluded = MechB.OpenInfiltratorExcluded ?? "Ninja";
        Require(command.Type == "chooseInfiltratorStraighten" && command.PlayerId == pending.PlayerId, "Choose the bowed Personality whose straightening is negated.");
        var chosen = InfiltratorCandidates(pending.PlayerId, excluded).FirstOrDefault(c => c.Id == command.TargetId);
        Require(chosen is not null, $"Choose one of your bowed non-{excluded} Personalities.");
        _state.PendingChoice = null;
        HoldForInfiltrator(chosen!, pending.PlayerId, MechB.OpenInfiltratorSource ?? Definition(Card(pending.CardId!)).Name);
        MechB.OpenInfiltratorExcluded = null; MechB.OpenInfiltratorSource = null;
        ContinueStraightenPhase(Player(pending.PlayerId));
    }

    private IEnumerable<ActionOption> InfiltratorStraightenActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "infiltratorStraighten" } pending || pending.PlayerId != playerId) yield break;
        foreach (var candidate in InfiltratorCandidates(playerId, MechB.OpenInfiltratorExcluded ?? "Ninja"))
            yield return new("chooseInfiltratorStraighten", $"Negate {Definition(candidate).Name}'s straightening this phase", true, null,
                new GameCommand("chooseInfiltratorStraighten", playerId, _state.Version, TargetId: candidate.Id), pending.CardId, [candidate.Id]);
    }

    // The view: the granted trait is an explanation of an effect the engine enforces (GrantedEffectView), shown on the card that has it.
    private IEnumerable<GrantedEffectView> MechBGrantedEffects(CardInstance card) =>
        MechB.InfiltratorGrants.Where(g => g.CardId == card.Id).Select(g => new GrantedEffectView("Infiltrator trait",
            $"After a Straighten Phase begins: Choose your bowed non-{g.ExcludedKeyword} Personality. Negate his straightening this phase.", g.Source, GrantDuration.Permanent, "Trait"));
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredInfiltratorStraighten(string type, GameCommand command)
    {
        if (type != "infiltratorStraighten") return false;
        ResolveInfiltratorStraighten(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorMechBHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorMechB", 1900, static abilities => abilities.AddEmperorMechB());
        EngineHooks.TargetAllows.Register("MechBTargetAllows", 1000, static (engine, context) => engine.MechBTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.TargetedReaction.Register("MechBTargetedReactionApplies", 400, static (engine, context) => engine.MechBTargetedReactionApplies(context.card, context.abilityId, context.subject, context.affected));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorMechB", 800, static (engine, context) => engine.ValidateEmperorMechB(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorMechB", 1800, static (engine, context) => engine.RunEmperorMechB(context.id, context.ability, context.command, context.player, context.source, context.target));
        EngineHooks.PendingChoice.Register("HandleRegisteredInfiltratorStraighten", 1800, static (engine, type, command) => engine.HandleRegisteredInfiltratorStraighten(type, command));
    }
}
