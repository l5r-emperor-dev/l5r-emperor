namespace L5R.AI;

// Issue #1206, strategy brief step 3, family G (docs/bots/strategy/RULE_FAMILIES.md G): posture and route. The Read's posture (Questions.Of: race, lock, drain, punish, wait, press, build, hold)
// gates the seat's own attack declaration. A lock or a drain (a non-military route that wins first while home holds) attacks only when the opportunity's best attack is risk-free (PM 3.1:
// "attack only when it costs nothing I need"; G5: no attacks in fourteen turns of a drain the Contender would have interrupted eleven times); a wait (their last Province, no win for them
// before my next turn, my best attack does not take it) preserves units and cards; a race or a punish declares. Press, build and hold leave the Contender's own declare-or-pass alone.
// A lethal attack is never gated.

/// <summary>What the route gate says at an attack declaration: declare, pass, or nothing (null: the Contender decides), and why.</summary>
public sealed record RoutePlan(bool? Declare, Posture Posture, string Why)
{
    public bool Silent => Declare is null;
}

public static class Route
{
    /// <param name="answers">the Read's answers at this declaration.</param>
    /// <param name="lethal">the Contender has a lethal attack: it is declared whatever the posture.</param>
    public static RoutePlan Gate(Answers answers, bool lethal)
    {
        var p = answers.Posture;
        if (lethal) return new(true, p, "a lethal attack is declared whatever the route");
        return p switch
        {
            Posture.Lock or Posture.Drain => !answers.Opportunity.Attacks
                ? new(false, p, $"{Name(p)}: no attack that costs nothing ({answers.Why})")
                : p == Posture.Drain && answers.Opportunity.Best.Groups.Any(g => g.Group.Any(u => u.Abilities.Any(a => a.IsOpen && a.Has(EffectClass.HonorTax) && a.Side != TargetSide.Own)))
                    ? new(false, p, $"drain: the risk-free attack ({answers.Opportunity.Best.Label}) would bow a unit that runs the drain")
                    : new(true, p, $"{Name(p)}: the best attack risks nothing ({answers.Opportunity.Best.Label}, exposure {answers.Opportunity.Best.ExposureCost - answers.Opportunity.Hold.ExposureCost:+0.00;-0.00} over the hold)"),
            Posture.Wait => new(false, p, $"wait: {answers.Why}; preserve units and cards, finish next turn"),
            Posture.Race => new(true, p, $"race: {answers.Why}"),
            Posture.Punish => new(true, p, $"punish: {answers.Why}"),
            _ => new(null, p, $"{Name(p)}: the Contender decides"),
        };
    }

    private static string Name(Posture p) => p.ToString().ToLowerInvariant();

    /// <summary>Honor a turn a side's table can take from the other without the trend having shown it yet: its units' repeatable Open Honor-loss abilities (their stated magnitude, 1 when it varies),
    /// each once a turn unless it says more. Card-agnostic: read from the descriptors, never from a name.</summary>
    public static double TableDrain(IEnumerable<LedgerUnit> units) =>
        units.SelectMany(u => u.Abilities).Where(a => a.IsOpen && a.Has(EffectClass.HonorTax) && a.Side != TargetSide.Own)
            .Sum(a => (a.Magnitude ?? 1) * Math.Max(1, a.UsesPerTurn > 0 && a.UsesPerTurn < 10 ? a.UsesPerTurn : 1));
}
