using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md step 3, RULE_FAMILIES.md family D): the defence assignment read from the Read. For each attacked Province the options are nobody, presence
// (the fewest bodies that unlock my answers and survive being targeted), kill (every unit with 2 Force or more) and everything; each is run through the exchange under the three hands;
// the choice follows PLAYER_MODEL.md 3.4: the last Province takes the highest hold probability; nothing that holds means nobody; a hold that destroys an attacker is taken with the
// smallest option that does; a hold that depends on my answers is held by presence; otherwise the smallest option that holds. Their last Province, with their attackers bowed on my
// turn and my attack then razing it, is conceded (3.3). Provinces are taken in the order of what their loss costs; the units used are gone for the next.
public sealed record DefenceOption(string Label, int Province, LedgerUnit[] Units, string Cell, double PHold, int Losses, int Kills, bool NeedsAnswers, bool HoldsLikely, int Margin)
{
    /// <summary>A hold that loses more units than it destroys.</summary>
    public bool Costly => Losses > Kills;
    public override string ToString() => $"{Label}({Units.Length}) {Cell} hold {PHold:0.00} lose {Losses} kill {Kills} margin {Margin:+0;-0;0}{(NeedsAnswers ? " by answers" : "")}";
}

public sealed record DefencePlan(Dictionary<string, int> Assignments, string Why, string[] Log)
{
    public bool Nobody => Assignments.Count == 0;
    public override string ToString() => $"{(Nobody ? "nobody" : string.Join(",", Assignments.GroupBy(a => a.Value).Select(g => $"P{g.Key + 1}<-{g.Count()}")))}: {Why}";
}

public static class Defence
{
    /// <summary>The plan for the infantry defence segment: <paramref name="legalIds"/> are the units the engine offers (null: every unbowed Personality at home), <paramref name="provinces"/> the
    /// Provinces that may be defended. <paramref name="concedeLast"/> says whether my attack next turn razes their last Province (the opportunity's reading), for 3.3's exception;
    /// <paramref name="concedeSpare"/> off (rule rdDefend, formulation 2) never concedes a hold that would cost cards or units while a Province is a price.</summary>
    public static DefencePlan Of(Read read, IEnumerable<string>? legalIds, IEnumerable<int> provinces, bool concedeLast = false, bool concedeSpare = true)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var legal = legalIds?.ToHashSet(StringComparer.Ordinal);
        var available = me.Home.Where(u => !u.Bowed && (legal is null || legal.Contains(u.Id))).OrderByDescending(u => u.Contribution).ThenBy(u => u.Id, StringComparer.Ordinal).ToList();
        var attacked = provinces.Where(p => them.At(p).Any()).Select(p => (Index: p, Strength: me.Provinces.FirstOrDefault(q => q.Index == p).Strength, Attackers: them.At(p).ToArray()))
            .OrderByDescending(p => p.Attackers.Sum(a => a.Contribution)).ThenBy(p => p.Index).ToArray();
        var assignments = new Dictionary<string, int>(StringComparer.Ordinal); var log = new List<string>(); var why = new List<string>();
        if (attacked.Length == 0) return new(assignments, "nobody attacks", []);
        var last = me.Provinces.Length == 1; var conceded = 0;
        foreach (var (province, strength, attackers) in attacked)
        {
            if (available.Count == 0) { log.Add($"P{province + 1}: nobody left"); continue; }
            var theirAnswers = Exchange.Have(attackers, them, read.Belief.Answers(false)).Length;
            var presenceCount = Math.Min(available.Count, 1 + theirAnswers);
            var fighters = available.Where(u => u.Contribution >= 2).ToArray(); // 0 to 1 Force utility Personalities stay home unless they are the presence body
            var options = new List<DefenceOption>
            {
                Evaluate(read, "nobody", province, strength, attackers, []),
                Evaluate(read, "presence", province, strength, attackers, available.Take(presenceCount).ToArray()),
            };
            for (var k = 1; k <= fighters.Length; k++) options.Add(Evaluate(read, k == fighters.Length ? "kill" : $"kill{k}", province, strength, attackers, fighters.Take(k).ToArray())); // the strongest k fighters
            options.Add(Evaluate(read, "everything", province, strength, attackers, available.ToArray()));
            var distinct = options.GroupBy(o => string.Join(",", o.Units.Select(u => u.Id))).Select(g => g.First()).ToList();
            DefenceOption chosen; string reason;
            // the holds, the surest first (hold probabilities within five points count as equal), then the fewest bodies, then the fewest losses
            var holding = distinct.Where(o => o.HoldsLikely && o.Units.Length > 0).OrderByDescending(o => Math.Round(o.PHold * 20)).ThenBy(o => o.Units.Length).ThenBy(o => o.Losses).ToList();
            var free = holding.FirstOrDefault(o => !o.NeedsAnswers && o.Losses == 0); // a hold on Force alone that loses nobody costs nothing
            // they hold destroy or duel effects (shown in their Fate cards, or printed on the attackers): every body is a target, so the fewest bodies go (PLAYER_MODEL.md 3.4, G5 T13 and T25)
            var targeted = attackers.Any(a => a.UsableBattle.Any(x => x.Class is EffectClass.Destroy or EffectClass.Duel)) || read.Belief.SeenOf(EffectClass.Destroy) + read.Belief.SeenOf(EffectClass.Duel) > 0;
            var afterLoss = me.Provinces.Length - conceded - 1;
            var spare = concedeSpare && afterLoss >= 2 && afterLoss >= them.Provinces.Length; // still level or ahead with two or more after the loss: a Province is a price (3.3); one concession a turn; off in formulation 2 (rdDefend)
            if (concedeLast && them.Provinces.Length == 1 && free is null) { chosen = distinct[0]; reason = "their last Province falls to my attack next turn while these attackers are bowed; holding here would cost units or cards"; }
            // my last Province: the highest hold; when the cells tie, the option that leaves the attacker the smallest margin (a body there unlocks the hand's answers; unit losses do not matter)
            else if (last) { chosen = distinct.OrderByDescending(o => Math.Round(o.PHold * 20)).ThenBy(o => o.Margin).ThenBy(o => o.Losses).First(); reason = "my last Province: the highest hold"; }
            else if (holding.Count == 0) { chosen = distinct[0]; reason = "nothing holds at likely: keep the units and the cards"; }
            // already safe on Strength: a body goes only to destroy a lone attacker it can beat without a loss (PLAYER_MODEL.md 3.4, last line)
            else if (distinct[0].HoldsLikely && holding.FirstOrDefault(o => o.Kills > 0 && o.Losses == 0) is { } sweep) { chosen = sweep; reason = "safe on Strength; one body destroys the attacker"; }
            else if (distinct[0].HoldsLikely) { chosen = distinct[0]; reason = "safe on Strength: nobody is needed"; }
            else if (!targeted && distinct.FirstOrDefault(o => o.Label == "kill") is { HoldsLikely: true, Kills: > 0, Costly: false } killAll && Math.Round(killAll.PHold * 20) >= Math.Round(holding[0].PHold * 20)) { chosen = killAll; reason = "defend to kill: every unit with 2 Force or more"; }
            else if (holding.FirstOrDefault(o => o.Kills > 0 && !o.Costly) is { } kills) { chosen = kills; reason = targeted ? "the fewest bodies that hold surest and destroy; their effects make every body a target" : "the fewest bodies that hold surest and destroy"; }
            else if (free is null && spare && holding.All(o => o.Costly)) { chosen = distinct[0]; reason = "every hold loses more units than it destroys while a Province is a price I can pay: concede"; conceded++; }
            else if (holding.FirstOrDefault(o => o.Kills > 0) is { } trade) { chosen = trade; reason = "holds and destroys at a cost in units, with no Province to spare"; }
            else if (free is not null) { chosen = free; reason = "holds on Force alone and loses nobody"; }
            else if (holding[0].NeedsAnswers && spare) { chosen = distinct[0]; reason = "holding would spend cards for no kill while a Province is a price I can pay: concede"; conceded++; }
            else if (holding[0].NeedsAnswers && distinct.FirstOrDefault(o => o.Label == "presence" && o.HoldsLikely) is { } presence) { chosen = presence; reason = "the hold depends on my answers: presence"; }
            else { chosen = holding[0]; reason = targeted ? "the fewest bodies that hold; their effects make every body a target" : "the smallest option that holds"; }
            log.Add($"P{province + 1} ({attackers.Length} attackers, {attackers.Sum(a => a.Contribution)}F v s{strength}): {string.Join(" | ", distinct)} -> {chosen.Label}: {reason}");
            why.Add($"P{province + 1} {chosen.Label}: {reason}");
            foreach (var u in chosen.Units) { assignments[u.Id] = province; available.Remove(u); }
        }
        return new(assignments, string.Join("; ", why), log.ToArray());
    }

    private static DefenceOption Evaluate(Read read, string label, int province, int strength, LedgerUnit[] attackers, LedgerUnit[] units)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var open = Exchange.Run(them, attackers, me, units, strength, [], me.HandAnswers);
        var likely = Exchange.Run(them, attackers, me, units, strength, read.Belief.Answers(false), me.HandAnswers);
        var worst = Exchange.Run(them, attackers, me, units, strength, read.Belief.Answers(true), me.HandAnswers);
        var bare = units.Length == 0 ? likely : Exchange.Run(them, attackers, me, units, strength, read.Belief.Answers(false), []); // without my hand: does Force and the table hold it?
        var cell = $"{(open.Falls ? 1 : 0)}{(likely.Falls ? 1 : 0)}{(worst.Falls ? 1 : 0)}";
        return new(label, province, units, cell, 1 - Calibration.PFall(cell), likely.DefendersDestroyed.Length, likely.AttackersDestroyed.Length, !likely.Falls && bare.Falls, !likely.Falls, likely.AttackForce - likely.DefenceForce - strength);
    }
}
