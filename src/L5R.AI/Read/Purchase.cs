using L5R.Core;

namespace L5R.AI;

// Issue #1206, strategy brief step 3, family F (docs/bots/strategy/RULE_FAMILIES.md F): Dynasty, economy against recruiting. At each of the seat's own Dynasty purchases the affordable row is
// read against two questions: does a body turn the threat to home (Threat.Of with the candidate as an unbowed defender), and does a body turn my best attack next turn from fails to falls
// (Questions.Of on the Read with the candidate in the straightened army)? Then the player model's conditions (PM 3.6): press with bodies, build while they have nobody or home holds anyway,
// bodies otherwise; a free Holding always; a Gold-producing Personality before others. The rule names the class and the card to buy next; the Contender's own values order the rest.

/// <summary>A face-up card in the row the seat can pay for now. <see cref="Unit"/> is the Personality as it would stand at home, unbowed, for the projections (null for a Holding).</summary>
public sealed record PurchaseCandidate(string Id, string Name, CardType Type, int Cost, int Force, int Production, LedgerUnit? Unit)
{
    public bool Body => Type == CardType.Personality && Unit is not null;
    public bool Holding => Type == CardType.Holding;
    public override string ToString() => $"{Name}({(Body ? $"{Force}F" : $"+{Production}G")} c{Cost})";
}

/// <summary>What the Read would buy next: the card (null: nothing it insists on, the Contender decides), the line of the rule card that chose it, and why.</summary>
public sealed record PurchasePlan(PurchaseCandidate? Choice, int Line, string Why, ThreatRead Before, string[] Log)
{
    public bool Silent => Choice is null;
}

public static class Purchase
{
    /// <param name="row">the affordable face-up Dynasty cards (Personalities and Holdings); others are ignored.</param>
    /// <param name="posture">the Read's posture this turn (null: unknown).</param>
    /// <param name="turnsLeft">the turns the game is expected to run, for a Holding's pay-back (the shorter clock, 2 to 8).</param>
    public static PurchasePlan Of(Read read, PurchaseCandidate[] row, Posture? posture, int turnsLeft)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var log = new List<string>();
        var bodies = row.Where(c => c.Body && c.Cost <= me.GoldNow).OrderByDescending(c => c.Force).ThenBy(c => c.Cost).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var holdings = row.Where(c => c.Holding && c.Cost <= me.GoldNow).OrderByDescending(c => c.Production - c.Cost).ThenBy(c => c.Cost).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var theirArmy = Threat.Straightened(them.Units);
        var before = Threat.Of(read, Threat.Defenders(me.Units), theirArmy);
        log.Add($"row: {string.Join(", ", row)}; gold {me.GoldNow}; home {before}");
        if (bodies.Length == 0 && holdings.Length == 0) return new(null, 0, "nothing affordable in the row", before, log.ToArray());

        // line 0: a free Holding costs nothing and changes nothing else
        if (holdings.FirstOrDefault(h => h.Cost == 0) is { } free) return new(free, 0, "a free Holding", before, log.ToArray());

        // line 1: home loses a Province at likely and an affordable body makes it hold
        if (before.Likely.ProvincesLost > 0)
        {
            PurchaseCandidate? best = null; ThreatRead? bestThreat = null;
            foreach (var body in bodies.OrderBy(b => b.Cost).ThenByDescending(b => b.Force))
            {
                var with = Threat.Of(read, [.. Threat.Defenders(me.Units), body.Unit!], theirArmy);
                log.Add($"with {body}: home {with}");
                if (with.Likely.ProvincesLost == 0) { best = body; bestThreat = with; break; }
            }
            if (best is not null) return new(best, 1, $"home loses {before.Likely.ProvincesLost} at likely; {best.Name} makes it hold ({bestThreat!.Cell})", before, log.ToArray());
        }

        // line 2: they have no Personality: economy that pays back (PM 3.6: Holdings while they have nobody), unless the Gold buys a body and a paying Holding both (then the body first, the Holding follows)
        var paying = holdings.Where(h => h.Production * turnsLeft > h.Cost).ToArray();
        if (them.Units.Length == 0)
        {
            var both = paying.Length > 0 && bodies.Length > 0 && bodies.Min(b => b.Cost) + paying.Min(h => h.Cost) <= me.GoldNow;
            if (paying.Length > 0 && !both) return new(paying[0], 2, $"they have no Personality: {paying[0].Name} pays back in {turnsLeft} turns", before, log.ToArray());
            if (both && Body(bodies) is { } first) return new(first, 2, "they have no Personality and the Gold buys a body and a Holding: the body first, the Holding follows (PM 3.6 turn 1)", before, log.ToArray());
        }

        // line 3: a body turns my best attack next turn from fails to falls
        if (bodies.Length > 0 && them.Provinces.Length > 0 && them.Units.Length > 0)
        {
            var next = NextTurn(read, null);
            var without = Questions.Of(next).Opportunity.Best;
            var bar = .5 * Questions.ProvinceWorth(them.Provinces.Length);
            log.Add($"next turn without a purchase: {(without.IsHold ? "hold" : $"{without.Label} {without.ExpectedProvinces:0.00}")}");
            if (without.IsHold || without.ExpectedProvinces < bar)
            {
                foreach (var body in bodies.OrderBy(b => b.Cost).ThenByDescending(b => b.Force))
                {
                    var with = Questions.Of(NextTurn(read, body.Unit)).Opportunity.Best;
                    log.Add($"next turn with {body}: {(with.IsHold ? "hold" : $"{with.Label} {with.ExpectedProvinces:0.00}")}");
                    if (!with.IsHold && with.ExpectedProvinces >= bar) return new(body, 3, $"{body.Name} turns next turn's attack from fails to falls ({with.ExpectedProvinces:0.00} of {bar:0.00})", before, log.ToArray());
                }
            }
        }

        // line 4: pressing, bodies; a cheap Holding only with nothing else to buy
        if (posture == Posture.Press)
        {
            if (Body(bodies) is { } pressBody) return new(pressBody, 4, "pressing: a body", before, log.ToArray());
            if (holdings.FirstOrDefault(h => h.Cost <= 2) is { } cheap) return new(cheap, 4, "pressing, no body affordable: a Holding costing 2 or less", before, log.ToArray());
            return new(null, 4, "pressing, nothing to buy", before, log.ToArray());
        }

        // line 5: building (their shown answers beat my units: there must be units) while home holds: economy that pays back (PM 3.6: "build until I can field the unit their answers do not touch")
        if (posture == Posture.Build && me.Units.Length > 0 && before.Likely.ProvincesLost == 0 && paying.Length > 0) return new(paying[0], 5, $"building while home holds at likely: {paying[0].Name} pays back in {turnsLeft} turns", before, log.ToArray());

        // line 6: bodies, a Gold producer first
        if (Body(bodies) is { } body5) return new(body5, 6, before.Likely.ProvincesLost > 0 ? $"home loses {before.Likely.ProvincesLost} at likely and no body alone holds it: a body" : "a body", before, log.ToArray());
        return new(null, 6, "no body affordable", before, log.ToArray());
    }

    /// <summary>The body to buy when the class is bodies: one that produces Gold first (PM 3.6), then the most Force, then the cheaper.</summary>
    private static PurchaseCandidate? Body(PurchaseCandidate[] bodies) => bodies.OrderByDescending(b => b.Production > 0 ? 1 : 0).ThenByDescending(b => b.Force).ThenBy(b => b.Cost).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The board at my next attack: my units straightened (with the candidate at home), theirs straightened; the opponent's hand as believed now.</summary>
    public static Read NextTurn(Read read, LedgerUnit? bought)
    {
        var mine = Threat.Straightened(read.Ledger.Me.Units).ToList();
        if (bought is not null) mine.Add(bought with { Bowed = false, Spent = [], Province = null });
        var me = read.Ledger.Me with { Units = mine.ToArray(), GoldNow = read.Ledger.Me.GoldCapacity };
        var them = read.Ledger.Them with { Units = Threat.Straightened(read.Ledger.Them.Units) };
        return read with { Ledger = read.Ledger with { Phase = Phase.InfantryDeclaration, Turn = read.Ledger.Turn + 1, Me = me, Them = them }, AttackerId = me.Id };
    }
}
