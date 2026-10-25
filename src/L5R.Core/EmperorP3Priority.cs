namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P3 (continued): priority overrides, announcement windows, Henshin entries and a Kata's Honor-gain Reaction.
//   * A REACTION LOCKOUT FOR ONE ACTION (Thunder Dragon - exp2, Shosuro Aroru - exp2): "Other players may not take Reactions until it ends" -- P3.ReactionLockouts names the locked player and
//     the action; while it has not ended, that player's Reactions are refused (ValidateAbility) and are not counted as held by the windows' availability checks, the way Cherry Blossom
//     Festival's bar is. Thunder Dragon's is a trait triggered as he performs an action (its announcement); Aroru's is an effect of his Battle action (it applies as the action resolves).
//     The lock ends where the action's tail begins (P3AfterHeldAction: its resolution, its negated taking, a deferred Attack's end, a duel's end, a search or look's end), so the Reactions
//     "after it resolves" are not locked.
//   * STATIC GRANTS OF THE THUNDER DRAGON: +1F and "Battle: Target an enemy card without unbowed attachments: Bow it." for its controller's Mantis Clan Personalities (itself included).
//   * A HOST THAT REFUSES A CLASS OF ATTACHMENTS (Aroru: "Will not attach non-Ninja Followers"), read by HostRefusesAttachment.
//   * A REDIRECT WITH A DEFERRED TAIL (Aroru): "Choose another of your cards. The action targets it instead, if legal. After the action's resolution, you may move Aroru to a different
//     location." -- the move is owed when the held action has finished and asked (a "p3AroruMove" choice, answered by "p3AroruMove" with Choice "home", a Province, or neither to decline) once
//     the command leaves nothing open.
//   * MORE ANNOUNCE WATCHERS (Ikoma Ayumu, Utaku Lishan): "after you announce a Tactical / Battle action". The announce window (Tsuruchi Yakusho's) asks one watcher at a time; each P3 watcher
//     not yet asked for the announcement is asked in turn (P3.Priority.Announcements). Battle actions that are not abilities (a Terrain played, a Reserve recruit, a Battle equip) and additional Battle
//     actions get the same window: the command is judged legal by running it on a copy of the state first, then set aside while the window is open and run when it closes.
//   * HENSHIN ENTRIES (Asako Kikugoro): his own entry (own-enters-play), another of his player's Henshin entering play or overlaying (the entry window every overlay also opens), and one of
//     his player's Rings entering play (the after-action record Yung reads) -- three catalog entries sharing the printed action's one use.
//   * AN HONOR GAIN NOT FROM AN ACTION (Destructive Priorities): "Before you gain Honor during your turn" -- the R4 window before an Honor change (EmperorRulingR4Honor.cs) also opens on a
//     gain that no action caused (a battle's resolution, a trait) for a player whose Kata grants this Reaction; only that Reaction is offered there. "Negate the gain" marks that change
//     (its action key and occurrence), read by GainHonor as the command is run again.
// ======================================================================================================================================================================================
public static class EmperorP3PriorityAbilities
{
    public const string ThunderDragonEffect = "thunder-dragon";
    public const string ThunderDragonBow = "thunder-dragon-granted-bow";
    public const string AroruEffect = "aroru-refuses-non-ninja-followers";
    public const string AroruRedirect = "aroru-exp2-redirect";
    public const string AroruAttack = "aroru-exp2-attack";
    public const string AroruRedirectTarget = "p3-aroru-redirect";
    public const string AyumuStraighten = "ayumu-straighten";
    public const string LishanEffect = "lishan-attacking-bonus";
    public const string LishanPenalty = "lishan-penalty";
    public const string LishanTarget = "p3-opposing-lishan";
    public const string OwnTacticalAnnounced = "p3-own-tactical-action-announced";
    public const string OwnBattleAnnounced = "p3-own-battle-action-announced";
    public const string KikugoroSelf = "kikugoro-self-entry";
    public const string KikugoroHenshin = "kikugoro-henshin-entry";
    public const string KikugoroRing = "kikugoro-ring-entry";
    public const string DestructiveKata = "destructive-priorities-kata";
    public const string DestructiveReaction = "destructive-priorities-reaction";

    public static LookSpec KikugoroLook { get; } = new("kikugoro-look", "Look at the top two cards of your Dynasty deck", [new("Dynasty")], 2, [new(LookDisposition.Bottom, 0, 1)]);

    public static Dictionary<string, AbilityDefinition> AddEmperorP3Priority(this Dictionary<string, AbilityDefinition> all)
    {
        // Thunder Dragon - exp2 (oracle-l5r-10067, Seeds of Decay): "After Thunder Dragon performs an action: Other players may not take Reactions until it ends." (a trait) and "The Thunder Dragon
        // inspires. Your Mantis Clan Personalities have +1F and the ability, "Battle: Target an enemy card without unbowed attachments: Bow it."" -- both its Effect; the granted ability:
        all[ThunderDragonBow] = new("Battle", "Target an enemy card without unbowed attachments: Bow it", "enemy-card-without-unbowed-attachments");
        // Shosuro Aroru - exp2 (oracle-l5r-7089, Forgotten Legacy): "Will not attach non-Ninja Followers." (its Effect); "Reaction: When an action would target a Ninja at Aroru's location: Choose
        // another of your cards. The action targets it instead, if legal. After the action's resolution, you may move Aroru to a different location."; "Battle: Other players may not take
        // Reactions until this action ends. Ranged 5 Attack."
        all[AroruRedirect] = new("Reaction", "When an action would target a Ninja at Aroru's location: Choose another of your cards. The action targets it instead, if legal. After the action's resolution, you may move Aroru to a different location",
            AroruRedirectTarget, Trigger: ReactionTriggers.TargetedByAction);
        all[AroruAttack] = new("Battle", "Other players may not take Reactions until this action ends. Ranged 5 Attack", "attack");
        // Ikoma Ayumu (oracle-l5r-3595, Before the Dawn): "Reaction: Even if Ayumu is bowed, after you announce a Tactical action, if he is at the current battlefield: Straighten him."
        all[AyumuStraighten] = new("Reaction", "Even if Ayumu is bowed, after you announce a Tactical action, if he is at the current battlefield: Straighten him", "none", Trigger: OwnTacticalAnnounced);
        // Utaku Lishan (oracle-l5r-9083, Embers of War): "While Lishan is attacking, she has +1F and +1PH." (her Effect) and "Reaction: After you announce a Battle action, if Lishan is at the
        // current battlefield, target a Personality or Follower opposing her: Give it a Force penalty equal to Lishan's Personal Honor."
        all[LishanPenalty] = new("Reaction", "After you announce a Battle action, if Lishan is at the current battlefield, target a Personality or Follower opposing her: Give it a Force penalty equal to Lishan's Personal Honor",
            LishanTarget, Trigger: OwnBattleAnnounced);
        // Asako Kikugoro (oracle-l5r-556, Embers of War): "Reaction: After your Ring or Henshin (including Kikugoro) enters play or overlays: Look at the top two cards of your Dynasty deck. You
        // may put one of them at the bottom of the deck." -- one printed action, three catalog entries sharing its use (himself, another Henshin, a Ring).
        all[KikugoroSelf] = new("Reaction", "After your Ring or Henshin (including Kikugoro) enters play or overlays: Look at the top two cards of your Dynasty deck. You may put one of them at the bottom of the deck",
            "none", Trigger: "own-enters-play", UsageKey: "kikugoro-look", Look: KikugoroLook);
        all[KikugoroHenshin] = new("Reaction", "After your Ring or Henshin (including Kikugoro) enters play or overlays: Look at the top two cards of your Dynasty deck. You may put one of them at the bottom of the deck",
            "none", Trigger: "own-personality-enters-play", UsageKey: "kikugoro-look", Look: KikugoroLook);
        all[KikugoroRing] = new("Reaction", "After your Ring or Henshin (including Kikugoro) enters play or overlays: Look at the top two cards of your Dynasty deck. You may put one of them at the bottom of the deck",
            "none", Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "kikugoro-look", Look: KikugoroLook);
        // Destructive Priorities (oracle-l5r-1983, Before the Dawn, Kata Political): "You have the ability, "Political Reaction: Before you gain Honor during your turn, bow your performing Courtier:
        // Negate the gain. Choose a player. He loses 2 Honor."" and "Limited: Put this card into play. Discard all your other Katas."
        all[DestructiveKata] = new("Limited", "Put this card into play. Discard all your other Katas", "none");
        all[DestructiveReaction] = new("Reaction", "Political Reaction: Before you gain Honor during your turn, bow your performing Courtier: Negate the gain. Choose a player. He loses 2 Honor", "player",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Trigger: HonorChangeReactions.Trigger, Costs: ActionCost.BowPerformer, ActionKeywords: ["Political"]);
        return all;
    }
}

/// <summary>The P3 announce watchers asked for one announcement, and what kind of command it is.</summary>
public sealed class P3AnnounceRecord
{
    public List<string> Asked { get; set; } = [];
    public string? Kind { get; set; }
}

/// <summary>A player who may not take Reactions until an action ends (this turn).</summary>
public sealed record ReactionLockout(int LockedPlayerId, HeldActionKey Action, int Turn, string SourceName);

/// <summary>Aroru's "After the action's resolution, you may move Aroru to a different location": owed once the held action (Action) has finished (Ready).</summary>
public sealed record AroruMoveOwed(string AroruId, HeldActionKey Action, bool Ready);

public sealed class EmperorP3PriorityState
{
    public List<ReactionLockout> ReactionLockouts { get; set; } = [];
    public List<AroruMoveOwed> AroruMoves { get; set; } = [];
    public string? AroruAsking { get; set; }
    /// <summary>Per announcement (its key): the P3 watchers asked, and its kind -- "additional": an additional action; "command": a Battle action that is not an ability (its command is run
    /// when the window closes). Cleared as a new action is announced at the top level (a Reaction played in a window keeps the records of the announcement it answers).</summary>
    public Dictionary<string, P3AnnounceRecord> Announcements { get; set; } = [];
    /// <summary>Destructive Priorities: the Honor changes negated ("action key#occurrence").</summary>
    public List<string> NegatedHonorGains { get; set; } = [];
}

public sealed partial class MatchEngine
{
    private EmperorP3PriorityState P3P => _state.P3.Priority;
    private const string P3NonActionHonorPrefix = "p3-no-action|";
    private (string Key, int Occurrence)? _p3LastHonorChange;

    // ---- the Reaction lockout ------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>ContinueUseAbility's hook, as an action is announced: the asked-watcher record starts afresh, and Thunder Dragon's trait locks the other player out if he performs it.</summary>
    private void P3NoteAnnouncement(GameCommand command)
    {
        if (_state.PendingChoice is null) P3P.Announcements.Clear();
        if (ActionPerformerPerRules(command) is { } performer && Definition(performer).Effect == EmperorP3PriorityAbilities.ThunderDragonEffect && InPlay(performer) && KeyOf(command) is { } key)
            LockReactions(1 - command.PlayerId, key, Definition(performer).Name);
    }

    private void LockReactions(int lockedPlayerId, HeldActionKey action, string sourceName)
    {
        P3P.ReactionLockouts.RemoveAll(l => l.Turn != _state.TurnNumber);
        P3P.ReactionLockouts.Add(new(lockedPlayerId, action, _state.TurnNumber, sourceName));
        Emit("reactionsLocked", $"{sourceName}: {Player(lockedPlayerId).Name} may not take Reactions until this action ends.", 1 - lockedPlayerId);
    }

    /// <summary>Whether <paramref name="playerId"/> may not take the Reaction <paramref name="abilityId"/> now (a trait written as a Reaction is not a Reaction).</summary>
    private bool P3ReactionLocked(int playerId, string abilityId) =>
        P3P.ReactionLockouts.Any(l => l.LockedPlayerId == playerId && l.Turn == _state.TurnNumber) && AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Timing == "Reaction"
        && !TraitReactions.Is(abilityId);

    private void P3EndLockouts(HeldActionKey key)
    {
        if (P3P.ReactionLockouts.RemoveAll(l => l.Action == key) > 0) Emit("reactionsUnlocked", "The action has ended: Reactions may be taken again.");
        for (var i = 0; i < P3P.AroruMoves.Count; i++) if (P3P.AroruMoves[i].Action == key) P3P.AroruMoves[i] = P3P.AroruMoves[i] with { Ready = true };
    }

    /// <summary>FinishDeferredAction's hook: an action that ended in a search or a look (it has no other tail).</summary>
    private void P3AfterDeferredAction(int playerId, string sourceCardId)
    {
        foreach (var key in P3P.ReactionLockouts.Where(l => l.Action.PlayerId == playerId && l.Action.CardId == sourceCardId).Select(l => l.Action).Distinct().ToArray())
            P3EndLockouts(key);
    }

    // ---- the Thunder Dragon's statics ----------------------------------------------------------------------------------------------------------------------------------------------------

    private bool ControlsThunderDragon(int playerId) => _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == EmperorP3PriorityAbilities.ThunderDragonEffect);

    /// <summary>PersonalityForce's hook: the Thunder Dragon's +1F to its controller's Mantis Clan Personalities; Utaku Lishan's +1F while she is attacking.</summary>
    private int P3Force(CardInstance card) =>
        (Definition(card).Type == CardType.Personality && InPlay(card) && HasClanAffiliation(card, "Mantis") && ControlsThunderDragon(card.OwnerId) ? 1 : 0)
        + (Definition(card).Effect == EmperorP3PriorityAbilities.LishanEffect && IsAttacking(card) ? 1 : 0);

    /// <summary>StaticPersonalHonorBonus's hook: Utaku Lishan's +1PH while she is attacking.</summary>
    private int P3PersonalHonorBonus(CardInstance card) => Definition(card).Effect == EmperorP3PriorityAbilities.LishanEffect && IsAttacking(card) ? 1 : 0;

    /// <summary>StaticGrantedAbilityIds' hook: the ability the Thunder Dragon gives its controller's Mantis Clan Personalities.</summary>
    private IEnumerable<string> P3StaticGrantedAbilityIds(CardInstance card)
    {
        if (Definition(card).Type == CardType.Personality && InPlay(card) && HasClanAffiliation(card, "Mantis") && ControlsThunderDragon(card.OwnerId))
            yield return EmperorP3PriorityAbilities.ThunderDragonBow;
    }

    // ---- Shosuro Aroru - exp2 -------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>HostRefusesAttachment's hook: "Will not attach non-Ninja Followers."</summary>
    private bool P3HostRefuses(CardInstance attachment, CardInstance host) =>
        Definition(host).Effect == EmperorP3PriorityAbilities.AroruEffect && Definition(attachment).Type == CardType.Follower && !Keyword(attachment, "Ninja");

    /// <summary>"When an action would target a Ninja at Aroru's location": any player's action, a Ninja standing where he stands.</summary>
    private bool AroruRedirectApplies(CardInstance aroru, ReactionSubject subject, CardInstance affected) =>
        InPlay(aroru) && Keyword(affected, "Ninja") && InPlay(affected) && SameLocation(aroru, UnitHostId(affected) == affected.Id ? affected : Card(UnitHostId(affected))) && AroruCandidates(aroru, affected).Any();

    /// <summary>"Choose another of your cards": any card of his player's in play other than the targeted Ninja (the action targets it instead only if it is legal).</summary>
    private IEnumerable<CardInstance> AroruCandidates(CardInstance aroru, CardInstance affected) =>
        _state.Cards.Values.Where(c => c.OwnerId == aroru.OwnerId && c.Id != affected.Id && InPlay(c)).OrderBy(c => c.Id, StringComparer.Ordinal);

    private void AroruRedirects(CardInstance aroru, CardInstance chosen)
    {
        var subject = HeldAction();
        if (ActionAcceptsTarget(subject, chosen.Id)) RedirectAction(subject, chosen.Id, aroru);
        else Emit("redirectFailed", $"{Definition(aroru).Name}: the action cannot legally target {Definition(chosen).Name}; it keeps its target.", aroru.OwnerId, chosen.Id);
        if (KeyOf(subject.Command) is { } key) P3P.AroruMoves.Add(new(aroru.Id, key, false));
    }

    /// <summary>Apply's hook once a command leaves nothing open: an owed "you may move Aroru to a different location" is asked.</summary>
    private void OpenP3OwedChoices()
    {
        if (_state.PendingChoice is not null || _state.PendingDuel is not null || _state.PendingAttack is not null || _state.WinnerId is not null) return;
        P3P.AroruMoves.RemoveAll(m => m.Ready && !(_state.Cards.TryGetValue(m.AroruId, out var gone) && InPlay(gone)));
        if (P3P.AroruMoves.FirstOrDefault(m => m.Ready) is not { } owed) return;
        P3P.AroruMoves.Remove(owed);
        var aroru = Card(owed.AroruId);
        P3P.AroruAsking = aroru.Id;
        _state.PendingChoice = new("p3AroruMove", aroru.OwnerId, aroru.Id, $"{Definition(aroru).Name}: you may move him to a different location.", _state.PriorityPlayerId);
        _state.PriorityPlayerId = aroru.OwnerId; _state.ConsecutivePasses = 0;
    }

    private int[] AroruBattlefields(CardInstance aroru) =>
        _state.CurrentBattleProvince is null && _state.UnresolvedBattlefields.Count == 0 ? []
            : _state.UnresolvedBattlefields.Concat(_state.CurrentBattleProvince is int current ? [current] : []).Distinct().Where(b => !(aroru.Zone == Zone.Battlefield && aroru.ProvinceIndex == b)).Order().ToArray();

    private void ResolveP3AroruMove(GameCommand command)
    {
        var aroru = Card(P3P.AroruAsking ?? throw new RuleViolation("No decision is open."));
        var pending = _state.PendingChoice!;
        Require(command.Type == "p3AroruMove" && command.PlayerId == aroru.OwnerId, "Move Aroru, or decline.");
        if (command.Choice == "home")
        {
            Require(aroru.Zone != Zone.Home, "Aroru is already at home.");
            MoveHome(aroru, false);
        }
        else if (command.Province is int battlefield)
        {
            Require(AroruBattlefields(aroru).Contains(battlefield), "Choose a battlefield Aroru is not at.");
            MoveToBattlefield(aroru, battlefield);
            Emit("unitMovedToBattlefield", $"{Definition(aroru).Name}'s unit moved to {BattlefieldLabel(battlefield)}.", aroru.OwnerId, aroru.Id);
        }
        else Emit("moveDeclined", $"{Definition(aroru).Name} stays where he is.", aroru.OwnerId, aroru.Id);
        P3P.AroruAsking = null;
        _state.PendingChoice = null;
        _state.PriorityPlayerId = pending.ActingPlayerId ?? _state.PriorityPlayerId;
    }

    private IEnumerable<ActionOption> P3AroruMoveActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "p3AroruMove" } pending || pending.PlayerId != playerId || P3P.AroruAsking is not { } id) yield break;
        var aroru = Card(id);
        if (aroru.Zone != Zone.Home)
            yield return new("p3AroruMove", $"Move {Definition(aroru).Name} home", true, null, new GameCommand("p3AroruMove", playerId, _state.Version, Choice: "home"), aroru.Id, []);
        foreach (var battlefield in AroruBattlefields(aroru))
            yield return new("p3AroruMove", $"Move {Definition(aroru).Name} to {BattlefieldLabel(battlefield)}", true, null, new GameCommand("p3AroruMove", playerId, _state.Version, Province: battlefield), aroru.Id, [], Provinces: [battlefield]);
        yield return new("p3AroruMove", $"Leave {Definition(aroru).Name} where he is", true, null, new GameCommand("p3AroruMove", playerId, _state.Version), aroru.Id, []);
    }

    private static int? P3AttackStrength(string abilityId) => abilityId == EmperorP3PriorityAbilities.AroruAttack ? 5 : null;

    // ---- Ikoma Ayumu, Utaku Lishan: "after you announce a [Tactical / Battle] action" -----------------------------------------------------------------------------------------------------

    // Whether a command announces a Battle action, a Tactical one, and who takes it.
    private bool AnnouncesBattleAction(GameCommand command) => command.Type == "useAbility" ? IsBattleAction(command) : _state.Phase == Phase.Battle && command.Type is "playStrategy" or "recruitReserve" or "equip";

    private bool AnnouncesTacticalAction(GameCommand command) =>
        command.Type == "useAbility" ? command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ActionCarriesKeyword(ability, command.CardId is null ? null : Card(command.CardId), "Tactical")
            : command.Type == "playStrategy" && command.CardId is not null && Keyword(Card(command.CardId), "Tactical");

    private static string AnnounceKeyOf(GameCommand command) => $"{command.PlayerId}|{command.CardId}|{command.AbilityId ?? command.Type}|{command.TargetId}";

    private P3AnnounceRecord AnnounceRecord(GameCommand command)
    {
        var key = AnnounceKeyOf(command);
        if (!P3P.Announcements.TryGetValue(key, out var record)) P3P.Announcements[key] = record = new();
        return record;
    }

    /// <summary>The next P3 watcher of the announcement not yet asked (FindAnnounceWatcher's fallback, the window's reopening), marked asked.</summary>
    // P6 (AfterFacts.cs): any registered watcher; Kalani's Landing (issue #816): a card of either player that reacts to any player's action.
    private CardInstance? P3AnnounceWatcher(GameCommand command) => P3BuiltInAnnounceWatcher(command) ?? (EngineHooks.AnnounceRegistrants.Items.Length == 0 ? null : RegisteredAnnounceWatcher(command)) ?? Wave816AnnounceWatcher(command);

    private CardInstance? P3BuiltInAnnounceWatcher(GameCommand command)
    {
        if (_state.Phase != Phase.Battle || _state.CurrentBattleProvince is not int battle) return null;
        var battleAction = AnnouncesBattleAction(command); var tactical = AnnouncesTacticalAction(command);
        if (!battleAction && !tactical) return null;
        var record = AnnounceRecord(command);
        var watcher = _state.Cards.Values.Where(c => c.OwnerId == command.PlayerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == battle && !record.Asked.Contains(c.Id) && !ActionLocked(c))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault(c => AbilityIdsOf(c).Any(a => a == EmperorP3PriorityAbilities.AyumuStraighten && tactical && Unspent(c, a) && !BarredByCherryBlossom(a) && !P3ReactionLocked(c.OwnerId, a)
                || a == EmperorP3PriorityAbilities.LishanPenalty && battleAction && !c.Bowed && Unspent(c, a) && !BarredByCherryBlossom(a) && !P3ReactionLocked(c.OwnerId, a) && LishanTargets(c).Any()));
        if (watcher is not null) record.Asked.Add(watcher.Id);
        return watcher;
    }

    private IEnumerable<CardInstance> LishanTargets(CardInstance lishan) =>
        lishan.Zone != Zone.Battlefield ? []
            : _state.Cards.Values.Where(c => c.OwnerId != lishan.OwnerId && InPlay(c) && Definition(c).Type is CardType.Personality or CardType.Follower
                && Card(UnitHostId(c)) is { Zone: Zone.Battlefield } host && host.ProvinceIndex == lishan.ProvinceIndex).OrderBy(c => c.Id, StringComparer.Ordinal);

    private void OpenAnnounceWindowFor(GameCommand command, CardInstance watcher)
    {
        _state.PendingAnnouncedAction = command;
        _state.PendingChoice = new("announceReaction", watcher.OwnerId, watcher.Id, "After you announce this action: you may use this card's own reaction, or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = watcher.OwnerId;
        Emit("reactionWindow", "Announce reaction window opened.", watcher.OwnerId, watcher.Id);
    }

    /// <summary>ResolveOrDeferAdditionalAction's hook: an additional Battle action is announced too. True when the announce window opened (the action waits behind it).</summary>
    private bool P3AnnounceAdditionalAction(GameCommand command)
    {
        if (P3AnnounceWatcher(command) is not { } watcher) return false;
        AnnounceRecord(command).Kind = "additional";
        OpenAnnounceWindowFor(command, watcher);
        return true;
    }

    /// <summary>The Apply dispatch's hook for a Battle action that is not an ability (a Terrain played, a Reserve recruit, a Battle equip): judged legal by running it on a copy of the state,
    /// then set aside behind the announce window. True when the window opened.</summary>
    private bool P3AnnounceCommand(GameCommand command, Action run)
    {
        if (command.Type == "useAbility" || !AnnouncesBattleAction(command) && !AnnouncesTacticalAction(command) && !Wave816Announces(command)) return false;
        P3P.Announcements.Remove(AnnounceKeyOf(command));
        var probe = P3AnnounceWatcher(command);
        if (probe is null) return false;
        var saved = Copy(_state); var savedDiscards = new Dictionary<string, string>(_paymentDiscards); var savedOccurrence = _honorChangeOccurrence;
        try { run(); }
        catch (HonorChangeWindowNeeded) { }
        finally { _state = saved; _paymentDiscards = savedDiscards; _honorChangeOccurrence = savedOccurrence; }
        AnnounceRecord(command).Kind = "command";
        OpenAnnounceWindowFor(command, probe);
        return true;
    }

    /// <summary>The announce window's close (MatchEngine's "announceReaction" case): another P3 watcher not yet asked is asked; an additional action or a set-aside command goes on. True when
    /// this handled the close.</summary>
    private bool P3ContinueAnnounce(GameCommand pending, Action<GameCommand> runCommand)
    {
        if (_state.AnnouncedActionNegated) return false;
        var kind = AnnounceRecord(pending).Kind;
        var nextWatcher = pending.AbilityId is not null && AbilityCatalog.All.TryGetValue(pending.AbilityId, out var announcedAbility) ? InPlayAnnounceWatcher(pending, announcedAbility) : null; // issue #898: the announcement's other in-play watchers
        if ((nextWatcher ?? NextEntrenchedWatcher(pending) ?? P3AnnounceWatcher(pending)) is { } watcher) { OpenAnnounceWindowFor(pending, watcher); return true; }
        if (kind == "command") { _state.PriorityPlayerId = pending.PlayerId; runCommand(pending); return true; }
        if (kind == "additional") { ResolveOrDeferAdditionalAction(pending); return true; }
        return false;
    }

    /// <summary>The trigger check (ValidateAbility): the announce window of this card, opened for its player's announcement of the right kind.</summary>
    private bool P3AnnounceWindowOpen(string trigger, GameCommand command) =>
        _state.PendingChoice?.Type == "announceReaction" && _state.PendingChoice.CardId == command.CardId && command.PlayerId == _state.PendingChoice.PlayerId
        && _state.PendingAnnouncedAction is { } announced && announced.PlayerId == command.PlayerId
        && (trigger == EmperorP3PriorityAbilities.OwnTacticalAnnounced ? AnnouncesTacticalAction(announced) : AnnouncesBattleAction(announced));

    /// <summary>The announce window's listing (MatchQueries): the P3 Reactions of the card the window is for, one option per target.</summary>
    private IEnumerable<(string AbilityId, string? TargetId, string? Label)> P3AnnounceOptions(CardInstance card)
    {
        foreach (var id in AbilityIdsOf(card))
        {
            if (id == EmperorP3PriorityAbilities.AyumuStraighten) yield return (id, null, null);
            if (id == EmperorP3PriorityAbilities.LishanPenalty)
                foreach (var target in LishanTargets(card)) yield return (id, target.Id, $"{AbilityCatalog.All[id].Label} · {Definition(target).Name} ({target.Id})");
        }
    }

    // ---- Asako Kikugoro ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>EntryWatchTriggerApplies' hook: Kikugoro watches only a Henshin's entry or overlay.</summary>
    private bool P3EntryWatchAllows(AbilityDefinition ability, CardInstance entered) =>
        !ReferenceEquals(ability, AbilityCatalog.All[EmperorP3PriorityAbilities.KikugoroHenshin]) || Keyword(entered, "Henshin");

    // ---- Destructive Priorities --------------------------------------------------------------------------------------------------------------------------------------------------------

    private CardInstance? DestructivePrioritiesInPlay(int playerId) =>
        _state.Cards.Values.FirstOrDefault(c => c.OwnerId == playerId && c.Zone == Zone.Home && (Definition(c).Abilities ?? []).Contains(EmperorP3PriorityAbilities.DestructiveReaction));

    /// <summary>OfferHonorChangeWindow's hook for a change no action made: a gain of the player whose turn it is, while his Kata grants him Destructive Priorities' Reaction.</summary>
    private bool P3NonActionHonorChange(int playerId, int amount, out string key, out int actorId)
    {
        key = ""; actorId = -1;
        if (amount <= 0 || playerId != _state.ActivePlayerId || DestructivePrioritiesInPlay(playerId) is null) return false;
        key = P3NonActionHonorPrefix + _state.TurnNumber;
        return true;
    }

    /// <summary>HonorChangeReactionApplies' hook. Null: not this group's (and not barred by it). A change no action made offers Destructive Priorities' Reaction only.</summary>
    private bool? P3HonorChangeApplies(CardInstance card, string abilityId, PendingHonorChange change)
    {
        if (abilityId == EmperorP3PriorityAbilities.DestructiveReaction)
            return card.Zone == Zone.Home && change.Amount > 0 && !UnnegatableHonorGain(change.ActionKey, change.Amount) && change.PlayerId == card.OwnerId && card.OwnerId == _state.ActivePlayerId
                && BeforeActionReactionPerformers(card.OwnerId, AbilityCatalog.All[abilityId]).Any() && !P3ReactionLocked(card.OwnerId, abilityId);
        return change.ActionKey.StartsWith(P3NonActionHonorPrefix, StringComparison.Ordinal) ? false : null;
    }

    // Ebisu's Honesty (issue #1114): "Gain 4 Honor; this gain will not be negated." Nothing that negates a gain is offered on it, or applies to it.
    private static bool UnnegatableHonorGain(string actionKey, int amount) => amount > 0 && actionKey.Split('|') is [_, "ebisu-destroy-province", _];

    /// <summary>GainHonor's hook: the change just offered was negated by Destructive Priorities.</summary>
    private bool P3HonorGainNegated(int playerId, int amount, string reason)
    {
        if (amount <= 0 || _p3LastHonorChange is not { } change || UnnegatableHonorGain(change.Key, amount) || !P3P.NegatedHonorGains.Remove($"{change.Key}#{change.Occurrence}")) return false;
        Emit("honorGainNegated", $"{Player(playerId).Name}'s Honor gain from {reason} was negated (Destructive Priorities).", playerId);
        return true;
    }

    private void DestructivePrioritiesNegates(GameCommand command, PlayerState player, CardInstance source)
    {
        if (_state.PendingHonorChange is { } change) P3P.NegatedHonorGains.Add($"{change.ActionKey}#{change.Occurrence}");
        if (command.TargetPlayerId is int loser) GainHonor(loser, -2, Definition(source).Name, source);
    }

    // ---- the hooks' shared entry points -------------------------------------------------------------------------------------------------------------------------------------------------

    private bool P3PriorityTargetAllows(string category, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        EmperorP3PriorityAbilities.AroruRedirectTarget => target.OwnerId == player.Id && InPlay(target) && OpenReactionSubject() is { TargetId: { } affected } && affected != target.Id,
        EmperorP3PriorityAbilities.LishanTarget => source is not null && LishanTargets(source).Any(c => c.Id == target.Id),
        _ => false
    };

    private void ValidateEmperorP3Priority(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (P3ReactionLocked(player.Id, id)) Require(false, $"{P3P.ReactionLockouts.First(l => l.LockedPlayerId == player.Id).SourceName}: you may not take Reactions until the action ends.");
        switch (id)
        {
            case EmperorP3PriorityAbilities.AyumuStraighten:
            case EmperorP3PriorityAbilities.LishanPenalty:
                Require(source is { Zone: Zone.Battlefield } && source.ProvinceIndex == _state.CurrentBattleProvince, "He must be at the current battlefield.");
                break;
            case EmperorP3PriorityAbilities.KikugoroHenshin:
                Require(_state.PendingChoice?.CardId is { } entered && Keyword(Card(entered), "Henshin"), "Only a Henshin's entry.");
                break;
            case EmperorP3PriorityAbilities.DestructiveKata:
                break;
            case EmperorP3PriorityAbilities.DestructiveReaction:
                Require(command.TargetPlayerId is int, "Choose a player.");
                break;
        }
    }

    private bool? RunEmperorP3Priority(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, HashSet<string> bowedBefore)
    {
        switch (id)
        {
            case EmperorP3PriorityAbilities.ThunderDragonBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case EmperorP3PriorityAbilities.AroruRedirect:
                AroruRedirects(source!, target!);
                return false;
            case EmperorP3PriorityAbilities.AroruAttack:
                if (KeyOf(command) is { } key) LockReactions(1 - player.Id, key, Definition(source!).Name);
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            case EmperorP3PriorityAbilities.AyumuStraighten:
                Straighten(source!);
                return false;
            case EmperorP3PriorityAbilities.LishanPenalty:
                ApplyModifier(target!, source!.Id, id, "Force", -CardPersonalHonor(source));
                return false;
            case EmperorP3PriorityAbilities.KikugoroSelf:
            case EmperorP3PriorityAbilities.KikugoroHenshin:
            case EmperorP3PriorityAbilities.KikugoroRing:
                SuspendOpenWindow(); // a Reaction's look sets the window it was played in aside (ReactionsAreActions.cs)
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBefore);
                return true;
            case EmperorP3PriorityAbilities.DestructiveKata:
                EnterPlayAsKata(player, source!);
                return false;
            case EmperorP3PriorityAbilities.DestructiveReaction:
                DestructivePrioritiesNegates(command, player, source!);
                return false;
            default:
                return RunEmperorP3Entries(id, command, player, source, target); // Easy Persuasion (EmperorP3Entries.cs)
        }
    }

    private bool P3PriorityAfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts, out bool applies)
    {
        applies = false;
        if (abilityId != EmperorP3PriorityAbilities.KikugoroRing) return false;
        applies = InPlay(source) && YungRingEnteredPlay(facts, playerId);
        return true;
    }

    private bool P3PriorityTargetedApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) =>
        abilityId == EmperorP3PriorityAbilities.AroruRedirect && AroruRedirectApplies(card, subject, affected);

    private IEnumerable<(string? TargetId, string? Label)>? P3TargetedReactionChoices(CardInstance card, string abilityId, ReactionSubject subject) =>
        abilityId == EmperorP3PriorityAbilities.AroruRedirect && subject.TargetId is { } affected
            ? AroruCandidates(card, Card(affected)).Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})")).ToArray()
            : null;
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredP3AroruMove(string type, GameCommand command)
    {
        if (type != "p3AroruMove") return false;
        ResolveP3AroruMove(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP3PriorityHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorP3Priority", 3300, static abilities => abilities.AddEmperorP3Priority());
        EngineHooks.AttackStrengthBefore.Register("P3AttackStrength", 100, static (engine, context) => P3AttackStrength(context.abilityId));
        EngineHooks.PersonalityForce.Register("P3Force", 500, static (engine, context) => engine.P3Force(context.card));
        EngineHooks.PendingChoice.Register("HandleRegisteredP3AroruMove", 2200, static (engine, type, command) => engine.HandleRegisteredP3AroruMove(type, command));
    }
}
