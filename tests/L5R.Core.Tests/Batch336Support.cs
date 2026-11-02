using static RegressionAssertions;
using L5R.Core;

// Shared by the issue #336 suites (ActionLockTests, MultiTargetTests, MoveDestinationTests, StatPenaltyTests): the same helpers the
// Bofana suite uses -- an explicit position built on the economy scenario (player 0 the Crane, player 1 the Lion), driven only through
// QueryActions and the commands the engine offers.
internal static class Batch336Support
{
    public static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    public static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    public static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"fx-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // MatchEngine.State is a detached copy, so a mid-test change is made on a copy and the engine rebuilt from it.
    /// <summary>A province's strength as read once the turn has ended (issue #492 ruling R3: an effect lasts until end of turn unless it is permanent): read from a copy of the state with the turn number stepped forward.</summary>
    public static int StrengthNextTurn(MatchEngine e, int owner, int index)
    {
        var later = e.State; // a copy: stepping its turn forward leaves the engine alone
        later.TurnNumber++;
        return MatchEngine.FromTestState(later).ProvinceStrengthForTest(owner, index);
    }
    public static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) { var st = e.State; edit(st); return MatchEngine.FromTestState(st); }

    // Ring of the Void's recruit is a granted Battle action (issue #1157: a fourth non-Kiho Strategy action in a phase grants it). For a test that only needs a Ring to enter play:
    // the table moves to a battle and the grant is made, on a rebuilt engine (the state is a copy).
    public static MatchEngine GrantVoid(MatchEngine e, int player, string ringId) => Edit(e, st =>
    {
        st.Phase = Phase.Battle; st.PriorityPlayerId = player;
        st.AdditionalActions.Add(new AdditionalActionGrant(player, [ringId], Battle: st.BattleNumber));
    });

    public static void Force(CardInstance card, int amount) => card.Modifiers.Add(new("test-source", "test", "Force", amount));

    // The economy scenario with both hands emptied, so every hand card in a test is one the test put there. Battle: province 0 is the
    // current battlefield and the only unresolved one; player 1 is the Attacker, player 0 the Defender.
    public static MatchState Table(Phase phase = Phase.Battle, int actor = 1)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }

    // Every offered option for an ability, whether or not it is enabled (an action a Personality may not take is listed disabled, with the
    // reason, not silently absent).
    // A disabled option carries no command (its label is all that names the ability), and the label is the ability's own, then " · performer" and
    // " · Discipline ..." suffixes; a performer, when asked for, is found in that suffix.
    public static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null)
    {
        var label = AbilityCatalog.All[ability].Label;
        return e.QueryActions(player).Where(a => a.Type == "useAbility" && (a.Label == label || a.Label.StartsWith(label + " · "))
            && (cardId is null || a.CardId == cardId) && (performerId is null || a.Label.Contains($"({performerId})"))).ToArray();
    }

    // The one enabled option for an ability on a card, or a failure that lists what WAS offered.
    public static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null)
    {
        var options = Options(e, player, ability, cardId, performerId);
        var found = options.FirstOrDefault(a => a.Enabled);
        Check(found is not null, $"{ability} on {cardId} was not offered enabled to player {player}: " + string.Join("; ", options.Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return found!;
    }

    // True when the ability is offered enabled to the player on that card (and performer).
    public static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) =>
        Options(e, player, ability, cardId, performerId).Any(a => a.Enabled);

    // The offered command with a chosen single target, exactly the way the client retargets an option.
    public static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        var option = Options(e, player, ability, cardId, performerId).FirstOrDefault(a => a.Enabled && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", Options(e, player, ability, cardId, performerId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return option!.Command! with { TargetId = targetId };
    }

    // Issue #492 (ruling R5): the offered command of an ability with a "may compare" choice, for the chosen comparison ("force", "chi", "gold", "base").
    public static GameCommand OfferChoice(MatchEngine e, int player, string ability, string cardId, string targetId, string choice, string? performerId = null)
    {
        var option = Options(e, player, ability, cardId, performerId).FirstOrDefault(a => a.Enabled && a.Command?.Choice == choice && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} comparing {choice} was not offered to player {player}: "
            + string.Join("; ", Options(e, player, ability, cardId, performerId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return option!.Command! with { TargetId = targetId };
    }

    public static string Rejection(MatchEngine e, GameCommand command)
    {
        var result = e.Execute(command);
        Check(!result.Accepted, $"Expected rejection: {command.AbilityId}");
        return result.Error ?? "";
    }

    public static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        new("useAbility", player, e.Version, cardId, AbilityId: ability, PerformerId: performerId, TargetId: targetId, Selections: selections.Length == 0 ? null : selections);

    public static SlotSelection Pick(string slot, params string[] cards) => new(slot, cards);
    public static SlotSelection PickPlayer(string slot, int player) => new(slot, PlayerId: player);

    public static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;

    // Drives the game with offered commands only until `stop` holds, choosing for whoever must decide: attack province 0 with everything, defend
    // province 0 with everything, otherwise the first pass / end-of-phase / battle choice on offer. `each` sees every position on the way.
    public static void RunUntil(MatchEngine e, Func<bool> stop, Action? each = null)
    {
        for (var guard = 0; guard < 300 && !stop(); guard++)
        {
            var actor = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var actions = e.QueryActions(actor).Where(a => a.Enabled && a.Command is not null).ToArray();
            var option = actions.FirstOrDefault(a => a.Type is "attack" or "assignDefense" && a.Command!.Assignments!.Values.All(p => p == 0) && a.Command.Assignments.Count > 0 && (a.Type == "attack" || e.State.UnresolvedBattlefields.Contains(0)))
                ?? actions.FirstOrDefault(a => a.Type == "declareAttack") // the Declaration Segment (#359): declare, never decline
                ?? actions.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "selectBattle" or "declineReaction" or "chooseConqueror" or "discardHand");
            Check(option is not null, $"nothing to choose for player {actor} in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            var command = option!.Command!;
            if (command.Type == "discardHand") command = command with { CardIds = e.State.Players[actor].Hand.Take(e.State.Players[actor].Hand.Count - 8).ToArray() };
            Accept(e, command);
            each?.Invoke();
        }
        Check(stop(), $"did not reach the stop in {e.State.Phase}");
    }
}
