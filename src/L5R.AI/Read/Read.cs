using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4): the Read, one object rebuilt when what it depends on changes. Shadow mode (rule "read"): built and logged, driving nothing.
// Section 4.5's questions, posture and 4.6's replanning come after the calibration gate; this first part is the ledger, the belief and the exchange with need and have.
public sealed record BattleRead(int Province, int Strength, int Attackers, int Defenders, int? Need, int HaveAttacker, int HaveDefender, ExchangeResult Open, ExchangeResult Likely, ExchangeResult Worst)
{
    public override string ToString() => $"P{Province + 1} s{Strength} {Attackers}v{Defenders} need {(Need is { } n ? n : "-")} have {HaveAttacker}/{HaveDefender}: open {(Open.Falls ? 1 : 0)} likely {(Likely.Falls ? 1 : 0)} worst {(Worst.Falls ? 1 : 0)}";
}

public sealed record Read(Ledger Ledger, Belief Belief, string Key, int AttackerId)
{
    public static Read Of(MatchView view)
    {
        var them = view.Players.Single(p => p.Id != view.ViewerId);
        return new(Ledger.Of(view), Belief.Of(view, them), KeyOf(view), view.BattleStep?.AttackerPlayerId ?? view.ActivePlayerId);
    }

    /// <summary>Everything the Read depends on; a change rebuilds it.</summary>
    public static string KeyOf(MatchView view)
    {
        var parts = view.Players.Select(p => $"{p.Honor}:{p.HandCount}:{p.Discard.Length}:{p.Provinces.Count(q => !q.Destroyed)}:{string.Join(",", p.Home.Concat(p.Army).Select(c => c.Id + (c.Bowed ? "*" : "") + c.Force + "@" + (c.ProvinceIndex?.ToString() ?? "h") + (c.SpentAbilityIds?.Length ?? 0)))}");
        return $"{view.TurnNumber}|{view.Phase}|{view.FavorPlayerId}|{view.Events.Length}|{string.Join("|", parts)}";
    }

    /// <summary>The exchange at one Province, from the viewer's side: its own hand exactly, the opponent's under three assumptions (nothing, the belief, the belief's upper bound).</summary>
    public BattleRead Battle(int province, LedgerUnit[] attackers, LedgerUnit[] defenders, bool viewerAttacks)
    {
        var attackerSide = viewerAttacks ? Ledger.Me : Ledger.Them; var defenderSide = viewerAttacks ? Ledger.Them : Ledger.Me;
        var strength = defenderSide.Provinces.FirstOrDefault(p => p.Index == province).Strength;
        var myHand = Ledger.Me.HandAnswers;
        ExchangeResult Run(IEnumerable<Answer> theirs) => viewerAttacks ? Exchange.Run(attackerSide, attackers, defenderSide, defenders, strength, myHand, theirs) : Exchange.Run(attackerSide, attackers, defenderSide, defenders, strength, theirs, myHand);
        var need = Exchange.Need(attackers, defenders, strength);
        var haveA = Exchange.Have(attackers, attackerSide, viewerAttacks ? myHand : Belief.Answers(false)).Length;
        var haveD = Exchange.Have(defenders, defenderSide, viewerAttacks ? Belief.Answers(false) : myHand).Length;
        return new(province, strength, attackers.Length, defenders.Length, need, haveA, haveD, Run([]), Run(Belief.Answers(false)), Run(Belief.Answers(true)));
    }

    /// <summary>The exchange for every battlefield the view shows, from the viewer's side.</summary>
    public IEnumerable<BattleRead> Battles()
    {
        var viewerAttacks = AttackerId == Ledger.ViewerId;
        foreach (var province in Ledger.Me.Units.Concat(Ledger.Them.Units).Where(u => u.Province is not null).Select(u => u.Province!.Value).Distinct().Order())
        {
            var mine = Ledger.Me.At(province).ToArray(); var theirs = Ledger.Them.At(province).ToArray();
            yield return viewerAttacks ? Battle(province, mine, theirs, true) : Battle(province, theirs, mine, false);
        }
    }

    /// <summary>threat(home) from the board as it stands: their whole army straightened against my unbowed home units (section 4.5, question 2). <paramref name="projectStraighten"/> off reads
    /// their units as they are, for a view taken on their own turn after they straightened.</summary>
    public ThreatRead ThreatNow(bool projectStraighten = true, IEnumerable<string>? theirUnitsExpectedDestroyed = null) =>
        Threat.Of(this, Threat.Defenders(Ledger.Me.Units), projectStraighten ? Threat.Straightened(Ledger.Them.Units, theirUnitsExpectedDestroyed) : Ledger.Them.Home.Where(u => !u.Bowed).ToArray());

    public override string ToString() => $"T{Ledger.Turn} {Ledger.Phase} {(AttackerId == Ledger.ViewerId ? "attacking" : "defending")}; belief {Belief}; " + string.Join("; ", Battles());
}
