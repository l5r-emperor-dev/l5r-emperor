namespace L5R.Core;

// "He must pass at his next opportunity this phase to take a Battle, Limited, or Open action" (issue #338, Unfortunate Hesitation): a
// per-player restriction with a lifetime of one printed phase. It is a plain record of WHO owes the pass and in WHICH phase
// (PrintedPhases.Instance -- the whole Attack Phase is one phase, as for every "each phase" effect), so it lapses by comparison when the
// phase moves on and needs no clearing, and it is exactly what a replay reproduces.
//
// While it stands, at an ordinary opportunity of that player in the Action or Battle phase (they hold priority, nothing is pending) the only
// legal thing to do is pass: no Battle, Limited or Open action -- a useAbility, a played Strategy, a Reserve recruit or an equip -- is
// legal, and QueryActions lists them disabled with the reason. The pass IS the payment of the debt: it is spent by that player's next pass
// at such an opportunity. Reactions, purchasing in the Dynasty Phase, the Attack Phase's own assignment decisions and every pending decision
// are not "a Battle, Limited, or Open action" and are untouched.

/// <summary>A pass owed by <paramref name="PlayerId"/> during the phase numbered <paramref name="PhaseInstance"/>, because of
/// <paramref name="SourceId"/>.</summary>
public sealed record ForcedPass(int PlayerId, int PhaseInstance, string SourceId);

public sealed partial class MatchEngine
{
    private static readonly string ForcedPassReason = new BarNotice("Taking any action but passing", "Unfortunate Hesitation").Message;

    private void AddForcedPass(int playerId, CardInstance source) => _state.ForcedPasses.Add(new(playerId, PhaseInstance(), source.Id));

    private bool OwesForcedPass(int playerId) => _state.ForcedPasses.Any(f => f.PlayerId == playerId && f.PhaseInstance == PhaseInstance());

    // Whether an ordinary action opportunity is what the player holds now: an Action or Battle phase with nothing pending.
    private bool AtOrdinaryActionOpportunity() =>
        _state.Phase is Phase.Action or Phase.Battle && _state.PendingChoice is null && _state.PendingDuel is null;

    // Apply()'s check, on the command of a player who owes a pass at an ordinary opportunity: everything but a pass is refused, and the pass
    // is what settles the debt (one entry per pass, so two forced passes take two opportunities).
    private void EnforceForcedPass(GameCommand command)
    {
        if (!AtOrdinaryActionOpportunity() || !OwesForcedPass(command.PlayerId)) return;
        if (command.Type is "useAbility" or "playStrategy" or "recruitReserve" or "equip") throw new RuleViolation(ForcedPassReason);
        if (command.Type != "pass") return;
        var owed = _state.ForcedPasses.First(f => f.PlayerId == command.PlayerId && f.PhaseInstance == PhaseInstance());
        _state.ForcedPasses.Remove(owed);
        Emit("forcedPassTaken", $"{Player(command.PlayerId).Name} passes as {(_state.Cards.TryGetValue(owed.SourceId, out var source) ? Definition(source).Name : "an effect")} requires.", command.PlayerId, _state.Cards.ContainsKey(owed.SourceId) ? owed.SourceId : null);
    }

    // The same rule as the player's offered actions: an action they could not take is listed, disabled, with the reason.
    private ActionOption[] ApplyForcedPassToOptions(int playerId, List<ActionOption> options)
    {
        if (!AtOrdinaryActionOpportunity() || !OwesForcedPass(playerId)) return options.ToArray();
        return options.Select(o => o.Type is "useAbility" or "playStrategy" or "recruitReserve" or "equip" ? o with { Enabled = false, Reason = ForcedPassReason } : o).ToArray();
    }

    // Entries of a phase that is over are dropped when the next one begins (they could never match again).
    private void PruneForcedPasses(Phase entering) =>
        _state.ForcedPasses.RemoveAll(f => f.PhaseInstance != PrintedPhases.Instance(_state.TurnNumber, entering));
}
