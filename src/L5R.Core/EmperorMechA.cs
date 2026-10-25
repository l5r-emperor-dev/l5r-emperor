namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #501, MECHANICS GROUP A: ADDITIONAL ACTIONS AND PHASES (Mendon's items 1-5, verbatim in the issue's specification). Cards: Isawa Momoko (personality), Mirumoto Inokuchi (personality),
// Beyond the Line, One Action, Two Strikes, String of Victories (strategies) and Aramasu's Legacy (stronghold). Every card was read letter by letter against its scan (the scan is authoritative).
//
// New primitives (each reads the core rules and Mendon's rulings; none conflicts with them):
//   * PASS AND DECLINE (item 2). An additional action granted with "may" is optional: DECLINING it (command "declineAdditionalAction", offered while the player holds an optional grant) is not passing --
//     his optional grants are gone and the priority goes to the other player as after his last action, the count of consecutive passes untouched. TAKING it AS A PASS is passing: the pass spends
//     ONE grant (PassSpendsAdditionalAction; a mandatory one first, then an unrestricted one) and he keeps the priority while he holds another. A phase (or a battle) ends only when BOTH players
//     have passed in succession (CountPass): one player passing twice in a row -- two additional actions taken as passes -- counts once. A grant whose text does not say "may" (Beyond the Line:
//     "Take an additional action") is Mandatory: it may be taken as a pass, never declined.
//   * GRANTS SCOPED BY WHAT SPENDS THEM (item 3). AdditionalActionGrant.RequiredActionKeyword (the action must carry that keyword, read as every "[keyword] action" classification is:
//     ActionCarriesKeyword) and Tag (a grant with a rule of its own). Both are RESTRICTED grants (EmperorStrongholdBatch1.RestrictedGrant): while a player holds only restricted grants he may take
//     only an action one of them allows, pass, or decline. Beyond the Line's grant (Tag BeyondTheLineTag) is spent only by a Limited or Open Recon ability used AS A BATTLE ACTION (MechAActsAsBattle:
//     the timing check, the listing and the Battle-action presence rules all read it) that targets the current battlefield's province, and then the player draws a card after that action's
//     resolution (AfterMechAActionResolved, from the two points an action is truly done: FinishActionEffectTracking and CompleteNegatedAction).
//   * THE PER-BATTLE RECORD OF BATTLE ACTIONS (item 4): MechAState.TookBattleActionBattle -- the battle (BattleNumber) in which each player last took a Battle action at an ordinary opportunity
//     (a non-Reaction ability, a Strategy, an Equip or a Reserve recruit in the Combat Segment). A PER-PLAYER, PER-BATTLE LOCK ON NEW GRANTS (GrantLockBattle, read by GrantAdditionalAction):
//     One Action, Two Strikes' "You will not take any other additional Battle actions this battle".
//   * GRANTS IN THE ACTION PHASE (item 5) already existed (issue #453); a Battle/Open action taken in a battle now spends a grant like a Battle-timed one (it used to end the burst).
//   * A GRANT FOLLOWED AFTER IT IS GONE: AdditionalActionGrant.Id plus MechAState.InokuchiWatches / SpentGrantIds -- after every command (Apply's tail, CheckInokuchiWatches) a watched grant that is
//     no longer held and was not spent by an action (declined, taken as a pass, lapsed with the battle, never created) triggers its consequence ("If you don't, bow him").
//   * A COST WAIVER FOR ONE ANNOUNCED ACTION (Isawa Momoko): MechAState.MomokoWatches ("the next time this turn you announce an action on one of Momoko's Spells") and WaivedAction; every
//     non-destruction cost of that action (a declared cost, a cost option, its Gold) is ignored (MomokoWaivesCost, read by AbilityCosts.cs, AlternativeCosts.cs and GoldQuotes.cs).
//   * A WHOLE ATTACK PHASE INSIDE THE ACTION PHASE (item 1, Aramasu's Legacy; the AsideBattle of the rulings pass R1 generalised from one battlefield to a phase): the Action Phase pauses (no phase
//     ends or begins: SetPhase is bypassed on the way in and out), the controller is the Attacker and declarer, a battlefield exists at each of the Defender's provinces, and Infantry Maneuvers,
//     Cavalry Maneuvers, the battle order, engagement, Battle actions and resolution run as normal. PER-PHASE EXEMPTIONS read by the resolution (AramasuResolutionActive): the defending army is
//     not destroyed (a tie destroys only the attacking one), no province is destroyed, and the resolution does not bow the controller's Mantis Clan Magistrates or his attachments. A PER-PHASE
//     WIN RECORD (AramasuPhaseState.WonAny: more Force at resolution, even against no units). When the phase ends (EmperorRemainingC.FinishAttackPhaseEnd, after the after-Attack-Phase window) the
//     Action Phase resumes and, if any battle was won, the controller may target a Personality or Holding: it is bowed and its straightening is negated until after his next Straighten Phase ends
//     (a StraightenHold). The action then completes with its ordinary tail.
// ======================================================================================================================================================================================
public static class EmperorMechAAbilities
{
    public const string MomokoWaiver = "momoko-spell-waiver";
    public const string InokuchiStraighten = "inokuchi-straighten";
    public const string BeyondTheLine = "beyond-the-line-recon";
    public const string OneActionTwoStrikes = "one-action-two-strikes";
    public const string StringOfVictories = "string-of-victories";
    public const string AramasusLegacy = "aramasus-legacy-attack-phase";

    /// <summary>The Tag of Beyond the Line's grant.</summary>
    public const string BeyondTheLineTag = "beyond-the-line";

    public static Dictionary<string, AbilityDefinition> AddEmperorMechA(this Dictionary<string, AbilityDefinition> all)
    {
        // Isawa Momoko (oracle-l5r-10076, Seeds of Decay; the scan prints "non-destruction costs", the catalogue carries a later erratum "bowing costs"): performed by Momoko.
        all[MomokoWaiver] = new("BattleOrOpen", "After the next time this turn you announce an action on one of Momoko's Spells, ignore the action's non-destruction costs. You may take an additional action", "none");
        // Mirumoto Inokuchi (oracle-l5r-5106, Before the Dawn): performed by Inokuchi, even if he is bowed (MechAEvenIfBowed).
        all[InokuchiStraighten] = new("Battle", "Even if Inokuchi is bowed: Straighten him. You may take an additional Battle action, which must be Iaijutsu and which Inokuchi must perform. If you don't, bow him", "none");
        // Beyond the Line (oracle-l5r-967, Before the Dawn, Strategy, Focus 2).
        all[BeyondTheLine] = new("Battle", "Take an additional action to use a Limited or Open Recon ability as a Battle action, which must target the current battlefield's province. If you do, draw a card after the action's resolution", "none");
        // One Action, Two Strikes (oracle-l5r-5706, Before the Dawn, Strategy, Focus 1). "The enemy leader" is the other player (Mendon's item 4).
        all[OneActionTwoStrikes] = new("Battle", "If the enemy leader has taken any Battle actions during this battle: You may take two additional Battle actions after this one. You will not take any other additional Battle actions this battle", "none");
        // String of Victories (oracle-l5r-7588, Before the Dawn, Strategy, Focus 4).
        all[StringOfVictories] = new("BattleOrOpen", "Straighten all of your Rings. You may put a Ring in your Fate discard pile into your hand. You may take an additional action appropriate to this phase", "none");
        // Aramasu's Legacy (oracle-l5r-10184, Seeds of Decay Mantis Stronghold). The scan (Mendon's, 2026-09-25) prints "until after your next Straighten Phase ends".
        all[AramasusLegacy] = new("Limited", "Create an additional Attack Phase, during which battle resolution does not destroy defending armies or provinces and does not bow your Mantis Clan Magistrates or your attachments. After it ends, if you won any of its battles (even against no units at resolution), you may target a Personality or Holding, bow it, and negate its straightening until after your next Straighten Phase ends", "none");
        return all;
    }
}

/// <summary>Momoko's watch: "After the next time this turn you announce an action on one of Momoko's Spells".</summary>
public sealed record MomokoWatch(int PlayerId, string MomokoId, int Turn);

/// <summary>The one announced action whose non-destruction costs are ignored (Momoko), until it is done.</summary>
public sealed record WaivedAction(int PlayerId, string SourceId, string AbilityId, int Turn);

/// <summary>Inokuchi's "If you don't, bow him": the grant (by id) and the Personality bowed when it is gone without being taken.</summary>
public sealed record InokuchiWatch(string GrantId, string InokuchiId, int PlayerId);

/// <summary>Beyond the Line's "If you do, draw a card after the action's resolution": the action (its ability and source) and the drawing player.</summary>
public sealed record PendingActionDraw(int PlayerId, string AbilityId, string? SourceId);

/// <summary>Aramasu's Legacy's Attack Phase in progress inside the Action Phase.</summary>
public sealed class AramasuPhaseState
{
    /// <summary>The controller: the declarer and Attacker of the phase.</summary>
    public int PlayerId { get; set; }
    /// <summary>Whose turn it is (ActivePlayerId is restored to him when the phase ends).</summary>
    public int TurnPlayerId { get; set; }
    public string SourceId { get; set; } = "";
    /// <summary>"If you won any of its battles (even against no units at resolution)".</summary>
    public bool WonAny { get; set; }
    /// <summary>The Limited action that created the phase, kept whole so it completes exactly as it would have.</summary>
    public PendingEffectChoice Paused { get; set; } = new();
}

public sealed class MechAState
{
    /// <summary>The player whose pass was the last one counted (CountPass: a player's second pass in a row does not count again).</summary>
    public int? LastPasserId { get; set; }
    /// <summary>The battle (BattleNumber) in which each player last took a Battle action (-1: none).</summary>
    public int[] TookBattleActionBattle { get; set; } = [-1, -1];
    /// <summary>The battle in which each player "will not take any other additional Battle actions" (One Action, Two Strikes; -1: none).</summary>
    public int[] GrantLockBattle { get; set; } = [-1, -1];
    public int NextGrantId { get; set; }
    public List<InokuchiWatch> InokuchiWatches { get; set; } = [];
    /// <summary>Watched grants spent by an action (so their "if you don't" never fires).</summary>
    public List<string> SpentGrantIds { get; set; } = [];
    public List<PendingActionDraw> PendingDraws { get; set; } = [];
    public List<MomokoWatch> MomokoWatches { get; set; } = [];
    public WaivedAction? Waived { get; set; }
    public AramasuPhaseState? Aramasu { get; set; }
}

public sealed partial class MatchEngine
{
    private MechAState MechA => _state.MechA;

    // The verb name the Gold cost is asked under (MomokoWaivesCost).
    private const string MechAGoldVerb = "gold";

    private string NewMechAGrantId() => $"mecha-grant-{++MechA.NextGrantId}";

    // ---- item 2: taking an additional action as a pass, and declining it ------------------------------------------------------------------------------------------------------------------

    // The grants a player holds that a pass or a decline is about (the battle-free, "burst" grants; a Ring's recruit grant is a standing permission, issue #379).
    private List<AdditionalActionGrant> HeldBurstGrants(int playerId) => _state.AdditionalActions.Where(g => g.PlayerId == playerId && Forfeitable(g)).ToList();

    /// <summary>A pass by a player holding additional actions takes one of them as a pass (Mendon's item 2): one grant is spent -- a mandatory one first, then an unrestricted one, then the oldest --
    /// and true is returned when he still holds another (he keeps the priority).</summary>
    private bool PassSpendsAdditionalAction(int passer)
    {
        var held = HeldBurstGrants(passer);
        if (held.Count == 0) return false;
        var spent = held.OrderByDescending(g => g.Mandatory).ThenBy(g => RestrictedGrant(g)).First();
        _state.AdditionalActions.Remove(spent);
        var remaining = HeldBurstGrants(passer).Count;
        Emit("additionalActionPassed", $"{Player(passer).Name} took an additional action as a pass{(remaining > 0 ? $" ({remaining} still to take)" : "")}.", passer);
        return remaining > 0;
    }

    /// <summary>Counts a pass; true when both players have now passed in succession. A player's pass right after his own counted pass is not a second pass of "both players".</summary>
    private bool CountPass(int passer)
    {
        if (!(_state.ConsecutivePasses > 0 && MechA.LastPasserId == passer)) _state.ConsecutivePasses++;
        MechA.LastPasserId = passer;
        return _state.ConsecutivePasses >= 2;
    }

    private bool MayDeclineAdditionalAction(int playerId) =>
        _state.PendingChoice is null && _state.PriorityPlayerId == playerId && HeldBurstGrants(playerId).Any(g => !g.Mandatory);

    /// <summary>"declineAdditionalAction": the player declines the optional additional actions he holds. Not a pass: the count of consecutive passes is untouched, and the priority goes to the other
    /// player as after his last action (unless a mandatory grant is still held, which he must take, as an action or a pass).</summary>
    private void DeclineAdditionalAction(GameCommand command)
    {
        Require(_state.Phase is Phase.Action or Phase.Battle, "There is no additional action to decline now.");
        var optional = HeldBurstGrants(command.PlayerId).Where(g => !g.Mandatory).ToList();
        Require(optional.Count > 0, "You hold no additional action you may decline.");
        foreach (var grant in optional) _state.AdditionalActions.Remove(grant);
        var keeps = HeldBurstGrants(command.PlayerId).Count > 0;
        Emit("additionalActionDeclined", $"{Player(command.PlayerId).Name} declined {(optional.Count == 1 ? "the additional action" : $"{optional.Count} additional actions")}.", command.PlayerId);
        if (keeps) return;
        EndAttackerLeadIfCeded(command.PlayerId, false);
        _state.PriorityPlayerId = 1 - command.PlayerId;
    }

    // ---- restricted grants: what spends them ------------------------------------------------------------------------------------------------------------------------------------------

    // PerformerGrantMatches' last condition (EmperorRemainingC.cs). A grant with a RequiredActionKeyword is spent only by an action carrying it; Beyond the Line's only by a Limited or Open Recon ability
    // used as a Battle action that targets the current battlefield's province; and such an action spends no other grant. With no command (an Equip) only a grant with neither can be spent.
    private bool MechAGrantMatches(AdditionalActionGrant grant, GameCommand? command)
    {
        AbilityDefinition? ability = null; CardInstance? source = null;
        if (command?.AbilityId is { } abilityId) AbilityCatalog.All.TryGetValue(abilityId, out ability);
        if (command?.CardId is { } cardId) _state.Cards.TryGetValue(cardId, out source);
        if (grant.RequiredActionKeyword is { } keyword && (ability is null || !ActionCarriesKeyword(ability, source, keyword))) return false;
        var asBattle = ability is not null && ability.Timing is "Limited" or "Open" && _state.Phase == Phase.Battle;
        if (grant.Tag == EmperorMechAAbilities.BeyondTheLineTag)
            return ability is not null && ability.Timing is "Limited" or "Open" or "BattleOrOpen" && _state.Phase == Phase.Battle && ActionCarriesKeyword(ability, source, "Recon") && TargetsCurrentProvinceOnly(command!);
        return !asBattle;
    }

    // "Which must target the current battlefield's province": the province (or provinces) the command names is exactly that one.
    private bool TargetsCurrentProvinceOnly(GameCommand command)
    {
        if (CurrentProvinceIndex() is not int current) return false;
        var named = command.Provinces ?? (command.Province is int single ? [single] : []);
        return named.Length == 1 && named[0] == current;
    }

    private bool HoldsBeyondTheLine(int playerId) => _state.AdditionalActions.Any(g => g.PlayerId == playerId && g.Tag == EmperorMechAAbilities.BeyondTheLineTag && GrantUsableNow(g));

    /// <summary>A Limited or Open Recon ability used as a Battle action: only in a battle, only by a player holding Beyond the Line's grant. Read by the timing check, the listing, the Battle-action
    /// presence rules (actingAsBattle) and the grant consumption.</summary>
    private bool MechAActsAsBattle(int playerId, AbilityDefinition ability, CardInstance? source) =>
        _state.Phase == Phase.Battle && ability.Timing is "Limited" or "Open" && HoldsBeyondTheLine(playerId) && ActionCarriesKeyword(ability, source, "Recon");

    private bool MechAGrantLocked(int playerId, int? battle) =>
        battle is null && _state.Phase == Phase.Battle && playerId is 0 or 1 && MechA.GrantLockBattle[playerId] == _state.BattleNumber;

    // ConsumeAdditionalActionGrant's hook: a watched grant was taken by an action; Beyond the Line's owes a draw after the action's resolution.
    private void NoteMechAGrantSpent(AdditionalActionGrant grant, GameCommand? command)
    {
        if (grant.Id is { } id && MechA.InokuchiWatches.Any(w => w.GrantId == id)) MechA.SpentGrantIds.Add(id);
        if (grant.Tag == EmperorMechAAbilities.BeyondTheLineTag && command?.AbilityId is { } abilityId)
            MechA.PendingDraws.Add(new(grant.PlayerId, abilityId, command.CardId));
    }

    // The two points an action is truly done (FinishActionEffectTracking; CompleteNegatedAction for a negated one): Beyond the Line's draw, and the end of Momoko's waiver for that action.
    private void AfterMechAActionResolved(string abilityId, int playerId, string? sourceId)
    {
        if (MechA.Waived is { } waived && waived.AbilityId == abilityId && waived.SourceId == sourceId) MechA.Waived = null;
        var draw = MechA.PendingDraws.FirstOrDefault(d => d.PlayerId == playerId && d.AbilityId == abilityId && d.SourceId == sourceId);
        if (draw is null) return;
        MechA.PendingDraws.Remove(draw);
        Emit("beyondTheLineDraw", $"{Player(playerId).Name} draws a card after the additional action's resolution (Beyond the Line).", playerId);
        Draw(Player(playerId));
    }

    // ---- item 4: the record of Battle actions --------------------------------------------------------------------------------------------------------------------------------------

    // ApplyCore's hook, before the command is dispatched (a refused command reverts it with everything else): a Battle action at an ordinary opportunity of the Combat Segment.
    private void NoteBattleActionTaken(GameCommand command)
    {
        if (_state.Phase != Phase.Battle || !AtOrdinaryActionOpportunity() || !IsNonReactionActionCommand(command) || command.PlayerId is not (0 or 1)) return;
        MechA.TookBattleActionBattle[command.PlayerId] = _state.BattleNumber;
    }

    // ---- Mirumoto Inokuchi --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool MechAEvenIfBowed(string abilityId) => abilityId == EmperorMechAAbilities.InokuchiStraighten;

    /// <summary>Apply's tail: a watched grant that is gone without having been taken bows its Personality ("If you don't, bow him").</summary>
    private void CheckInokuchiWatches()
    {
        foreach (var watch in MechA.InokuchiWatches.ToArray())
        {
            if (_state.AdditionalActions.Any(g => g.Id == watch.GrantId)) continue;
            MechA.InokuchiWatches.Remove(watch);
            if (MechA.SpentGrantIds.Remove(watch.GrantId)) continue;
            if (!_state.Cards.TryGetValue(watch.InokuchiId, out var inokuchi) || !InPlay(inokuchi)) continue;
            if (!StalwartNegatesBow(inokuchi, watch.PlayerId)) inokuchi.Bowed = true;
            Emit("inokuchiBowed", $"{Definition(inokuchi).Name} did not take the additional Iaijutsu Battle action, so he bows.", watch.PlayerId, inokuchi.Id);
        }
    }

    // ---- Isawa Momoko ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsDestructionCostVerb(string verb) =>
        verb is CostVerb.Destroy or CostVerbs.Sacrifice or CostVerbs.DestroyRegion or CostVerbs.DestroyToken or CostVerbs.DestroyLeftmostProvince or CostVerbs.DestroyRightmostProvince;

    // The Spell the command's action is on, when it is attached to the watched Momoko of a watch of the command's player this turn.
    private MomokoWatch? MomokoWatchFor(GameCommand command)
    {
        if (command.CardId is null || !_state.Cards.TryGetValue(command.CardId, out var source) || Definition(source).Type != CardType.Spell || source.Zone != Zone.Attached) return null;
        return MechA.MomokoWatches.FirstOrDefault(w => w.PlayerId == command.PlayerId && w.Turn == _state.TurnNumber && w.MomokoId == source.AttachedTo);
    }

    /// <summary>Whether a cost of this verb of the command's action is ignored: the action is the one Momoko's watch was spent on, or the one that would spend it (for the validation and the quote,
    /// which come before the announcement). Destruction costs are paid.</summary>
    private bool MomokoWaivesCost(GameCommand command, string verb)
    {
        if (P3CostsIgnored(command)) return true; // Iron Will (issue #503): every cost of the held action is ignored
        if (IsDestructionCostVerb(verb) || command.AbilityId is null) return false;
        if (MechA.Waived is { } waived && waived.Turn == _state.TurnNumber && waived.PlayerId == command.PlayerId && waived.SourceId == command.CardId && waived.AbilityId == command.AbilityId) return true;
        return MomokoWatchFor(command) is not null;
    }

    private void NoteMomokoWaivedCost(GameCommand command, string verb) =>
        Emit("costIgnored", $"{Player(command.PlayerId).Name} ignores the action's {(verb == MechAGoldVerb ? "Gold" : verb)} cost (Isawa Momoko).", command.PlayerId, command.CardId);

    // ContinueUseAbility's hook: the announcement of an action on one of Momoko's Spells spends the watch; its non-destruction costs are ignored until it is done.
    private void NoteMomokoAnnouncement(GameCommand command)
    {
        if (MomokoWatchFor(command) is not { } watch) return;
        MechA.MomokoWatches.Remove(watch);
        MechA.Waived = new(command.PlayerId, command.CardId!, command.AbilityId!, _state.TurnNumber);
        Emit("momokoWaiver", $"{Player(command.PlayerId).Name} announced an action on one of {Definition(Card(watch.MomokoId)).Name}'s Spells: its non-destruction costs are ignored.", command.PlayerId, command.CardId);
    }

    // ---- validation ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateEmperorMechA(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Beyond the Line: the Recon ability used as a Battle action "must target the current battlefield's province".
        if (MechAActsAsBattle(player.Id, ability, source))
            Require(TargetsCurrentProvinceOnly(command), "Beyond the Line: the Recon ability must target the current battlefield's province (and only it).");
        switch (id)
        {
            case EmperorMechAAbilities.OneActionTwoStrikes:
                Require(MechA.TookBattleActionBattle[1 - player.Id] == _state.BattleNumber, "The enemy leader has taken no Battle action during this battle.");
                break;
            case EmperorMechAAbilities.AramasusLegacy:
                Require(MechA.Aramasu is null && R1.Aside is null, "An additional Attack Phase is already in progress.");
                Require(Player(1 - player.Id).Provinces.Any(p => !p.Destroyed), "The other player has no province to create a battlefield at.");
                break;
        }
    }

    // ---- execution: null when the id is not this group's; false when its effects are done; true when the action waits ----

    private bool? RunEmperorMechA(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorMechAAbilities.MomokoWaiver:
                MechA.MomokoWatches.RemoveAll(w => w.MomokoId == source!.Id);
                MechA.MomokoWatches.Add(new(player.Id, source!.Id, _state.TurnNumber));
                Emit("momokoWatch", $"{Definition(source).Name}: the next action {player.Name} announces this turn on one of her Spells ignores its non-destruction costs.", player.Id, source.Id);
                GrantAdditionalAction(player.Id, null, anyDiscardTiming: _state.Phase != Phase.Battle, id: NewMechAGrantId());
                return false;
            case EmperorMechAAbilities.InokuchiStraighten:
                {
                    Straighten(source!);
                    var grantId = NewMechAGrantId();
                    MechA.InokuchiWatches.Add(new(grantId, source!.Id, player.Id));
                    GrantAdditionalAction(player.Id, null, performerId: source.Id, requiredActionKeyword: "Iaijutsu", id: grantId);
                    return false;
                }
            case EmperorMechAAbilities.BeyondTheLine:
                GrantAdditionalAction(player.Id, null, requiredActionKeyword: "Recon", tag: EmperorMechAAbilities.BeyondTheLineTag, id: NewMechAGrantId(), mandatory: true);
                return false;
            case EmperorMechAAbilities.OneActionTwoStrikes:
                // "You will not take any other additional Battle actions this battle": the player's other burst grants go, and no new one is created for him this battle -- after these two.
                _state.AdditionalActions.RemoveAll(g => g.PlayerId == player.Id && Forfeitable(g));
                MechA.GrantLockBattle[player.Id] = -1;
                GrantAdditionalAction(player.Id, null, id: NewMechAGrantId());
                GrantAdditionalAction(player.Id, null, id: NewMechAGrantId());
                MechA.GrantLockBattle[player.Id] = _state.BattleNumber;
                return false;
            case EmperorMechAAbilities.StringOfVictories:
                {
                    foreach (var ring in _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Ring && c.Bowed).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
                        Straighten(ring);
                    GrantAdditionalAction(player.Id, null, anyDiscardTiming: _state.Phase != Phase.Battle, id: NewMechAGrantId());
                    var rings = _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.FateDiscard && Definition(c).Type == CardType.Ring).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
                    var options = rings.Select(r => new EffectOption(r.Id, $"Put {Definition(r).Name} into your hand", [RingToHandEffect + r.Id])).Append(new EffectOption("none", "Put no Ring into your hand", [])).ToArray();
                    return BeginListedEffectChoice(command, options, $"{Definition(source!).Name}: you may put a Ring from your Fate discard pile into your hand.", consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case EmperorMechAAbilities.AramasusLegacy:
                BeginAramasuAttackPhase(command, player, source!, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                return true;
            default:
                return null;
        }
    }

    private const string RingToHandEffect = "mecha-ring-to-hand:";
    private const string AramasuBowEffect = "mecha-aramasu-bow:";

    // ApplyBranchEffect's arm for this group's listed options; false for any other effect.
    private bool ApplyEmperorMechABranchEffect(string effect, GameCommand command)
    {
        if (effect.StartsWith(RingToHandEffect, StringComparison.Ordinal))
        {
            var ring = Card(effect[RingToHandEffect.Length..]);
            var player = Player(command.PlayerId);
            if (ring.Zone != Zone.FateDiscard || ring.OwnerId != player.Id || P2CardIntoHandBlocked(player.Id)) return true; // issue #503: The Scorpion Wall is Finished
            player.Hand.Add(ring.Id); ring.Zone = Zone.Hand; ring.FaceUp = true; NoteCardIntoHand(player.Id);
            Emit("returnedToHand", $"{Definition(ring).Name} was put into {player.Name}'s hand from the Fate discard pile.", player.Id, ring.Id);
            return true;
        }
        if (effect.StartsWith(AramasuBowEffect, StringComparison.Ordinal))
        {
            var target = Card(effect[AramasuBowEffect.Length..]);
            if (!InPlay(target)) return true;
            if (!StalwartNegatesBow(target, command.PlayerId)) target.Bowed = true;
            Emit("aramasuBow", $"Aramasu's Legacy: {Definition(target).Name} is bowed.", command.PlayerId, target.Id);
            AddStraightenHold(target, "Aramasu's Legacy", command.PlayerId, StraightenPoint.StraightenPhaseEnds);
            return true;
        }
        return false;
    }

    // ---- Aramasu's Legacy: an Attack Phase inside the Action Phase -------------------------------------------------------------------------------------------------------------------

    private void BeginAramasuAttackPhase(GameCommand command, PlayerState player, CardInstance source, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore,
        List<AdditionalActionGrant> preExistingGrants)
    {
        MechA.Aramasu = new AramasuPhaseState
        {
            PlayerId = player.Id,
            TurnPlayerId = _state.ActivePlayerId,
            SourceId = source.Id,
            Paused = new()
            {
                Command = command,
                PlayerId = command.PlayerId,
                Negated = _state.NegatedEffects.ToList(),
                ConsumedGrant = consumedGrant,
                PlayedFromDiscard = playedFromDiscard,
                OpponentHonorBefore = opponentHonorBefore,
                BowedBefore = bowedBefore.ToList(),
                PreExistingGrants = preExistingGrants.ToList()
            }
        };
        _state.NegatedEffects.Clear();
        // The action's own burst ends here (an ordinary action), before the phase: its grants belong to the Action Phase.
        _state.AdditionalActions.RemoveAll(g => g.PlayerId == player.Id && Forfeitable(g));
        _state.ActivePlayerId = player.Id; // "the player who declared the attack phase is the attacker" (Mendon)
        _state.UnresolvedBattlefields = Player(1 - player.Id).Provinces.Where(p => !p.Destroyed).Select(p => p.Index).Order().ToList();
        Emit("attackDeclared", $"{player.Name} created an additional Attack Phase ({Definition(source).Name}): a battlefield exists at each of {Player(1 - player.Id).Name}'s provinces. The Action Phase waits until it ends.", player.Id, source.Id);
        Wave835AttackDeclared(); // issue #835: Imperial Deployment
        // The Action Phase pauses, it does not end: nothing that ends or begins a phase fires (SetPhase is bypassed), as for a battle fought inside the Action Phase (AsideBattle).
        _state.Phase = Phase.InfantryDeclaration; _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0; MechA.LastPasserId = null;
        Emit("phaseChanged", $"Phase: {Phase.InfantryDeclaration}.");
    }

    /// <summary>The resolution of a battle of Aramasu's Legacy's Attack Phase (its controller the Attacker; not an Attack Phase another card declared inside it): the per-phase exemptions apply.</summary>
    private bool AramasuResolutionActive(int attacker) => MechA.Aramasu is { } aramasu && aramasu.PlayerId == attacker && RemC.RetributionRoot < 0 && R1.Aside is null;

    private void NoteAramasuBattleWon()
    {
        if (MechA.Aramasu is not { } aramasu || aramasu.WonAny) return;
        aramasu.WonAny = true;
        Emit("aramasuBattleWon", $"{Player(aramasu.PlayerId).Name} won a battle of the Attack Phase of Aramasu's Legacy.", aramasu.PlayerId, aramasu.SourceId);
    }

    private bool IsMantisClanMagistrate(CardInstance card) =>
        Definition(card).Type == CardType.Personality && (HasClanAffiliation(card, "Mantis") || Keyword(card, "Mantis Clan")) && Keyword(card, "Magistrate");

    /// <summary>"Does not bow your Mantis Clan Magistrates or your attachments": the bowed state, before the resolution returns them home, of each card of the controller's units it must not bow.</summary>
    private Dictionary<string, bool>? AramasuUnbowedByResolution(IEnumerable<CardInstance> returning)
    {
        if (MechA.Aramasu is not { } aramasu) return null;
        var kept = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var card in returning.Where(c => c.OwnerId == aramasu.PlayerId))
        {
            if (IsMantisClanMagistrate(card)) kept[card.Id] = card.Bowed;
            foreach (var attached in Attachments(card.Id).Where(a => a.OwnerId == aramasu.PlayerId)) kept[attached.Id] = attached.Bowed;
        }
        return kept;
    }

    private void RestoreAramasuUnbowed(Dictionary<string, bool>? kept)
    {
        if (kept is null) return;
        foreach (var (id, bowed) in kept) if (_state.Cards.TryGetValue(id, out var card)) card.Bowed = bowed;
    }

    /// <summary>EmperorRemainingC.FinishAttackPhaseEnd's hook: the Attack Phase of Aramasu's Legacy (and any additional one declared after it) is over. The Action Phase resumes where it paused; if the
    /// controller won any of its battles he may target a Personality or Holding (bowed, its straightening negated until after his next Straighten Phase ends); then the action completes.</summary>
    private bool FinishAramasuAttackPhase()
    {
        if (MechA.Aramasu is not { } aramasu) return false;
        RemC.RetributionRoot = -1; RemC.RetributionAssigned = 0; R1.PerfectMoment = null;
        ApplyDelayedEffectsAtPhaseEnd(_state.Phase); // what waited for the end of this Attack Phase
        foreach (var waveMan in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == "wave-man-attack-phase-straighten").ToArray()) StraightenBowedUnit(waveMan); // "After an Attack Phase ends"
        MechA.Aramasu = null;
        _state.UnresolvedBattlefields = []; _state.CurrentBattleProvince = null;
        _state.ActivePlayerId = aramasu.TurnPlayerId;
        _state.Phase = Phase.Action; _state.ConsecutivePasses = 0; MechA.LastPasserId = null; _state.PriorityPlayerId = aramasu.PlayerId;
        Emit("phaseChanged", $"Phase: {Phase.Action}.");
        var paused = aramasu.Paused;
        var ability = AbilityCatalog.All[paused.Command.AbilityId!];
        var bowedBefore = paused.BowedBefore.ToHashSet(StringComparer.Ordinal);
        if (aramasu.WonAny)
        {
            var candidates = _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type is CardType.Personality or CardType.Holding)
                .OrderBy(c => c.OwnerId == aramasu.PlayerId).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
            var options = candidates.Select(c => new EffectOption(c.Id, $"Bow {Definition(c).Name} ({Player(c.OwnerId).Name}'s) and negate its straightening until after your next Straighten Phase ends",
                [AramasuBowEffect + c.Id])).Append(new EffectOption("none", "Target nothing", [])).ToArray();
            if (BeginListedEffectChoice(paused.Command, options, $"{Definition(Card(aramasu.SourceId)).Name}: you won a battle of its Attack Phase -- you may target a Personality or Holding, bow it and negate its straightening.",
                    paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore, bowedBefore, paused.PreExistingGrants))
                return true;
        }
        CompleteAbilityResolution(paused.Command, ability, paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore, bowedBefore, paused.PreExistingGrants);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorMechAHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorMechA", 2000, static abilities => abilities.AddEmperorMechA());
        EngineHooks.ValidateMiddle.Register("ValidateEmperorMechA", 900, static (engine, context) => engine.ValidateEmperorMechA(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorMechA", 1600, static (engine, context) => engine.RunEmperorMechA(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
