namespace L5R.AI;

// Issue #1206, strategy brief step 3, family C (docs/bots/strategy/RULE_FAMILIES.md C): home and the counter-attack. The attack as the Contender prepared it is read for what it leaves at
// home: threat(home without the attackers, their survivors straightened) under their likely hand (Threat.Of). If a Province is lost at likely, the cheapest remedy that turns the threat is
// applied: a unit kept back whose group still razes without it; then a whole group kept back; then nobody attacks, when only the whole army at home holds. The loss is accepted when the
// posture is a race, or when after it the seat stays ahead or level on Provinces with two or more left and every remedy breaks the attack (PM 3.3: a Province is a price he pays while ahead).

/// <summary>What the Guard would send: the thinned assignment (empty: nobody attacks), the units kept back, and why.</summary>
public sealed record GuardPlan(IReadOnlyDictionary<string, int> Assignments, string[] Kept, string Why, ThreatRead Before, ThreatRead After, string[] Log)
{
    public bool Nobody => Assignments.Count == 0;
    public bool Stands => Kept.Length == 0 && !Nobody;
}

public static class Guard
{
    /// <param name="assignment">the attackers about to be declared (unit id to Province index); units already at a battlefield in the ledger are fixed and count as attackers too.</param>
    /// <param name="posture">the Read's posture at this declaration (null: unknown).</param>
    /// <param name="lethal">the attack ends the game: it is never thinned.</param>
    public static GuardPlan Of(Read read, IReadOnlyDictionary<string, int> assignment, Posture? posture, bool lethal)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var log = new List<string>();
        var home = me.Units.Where(u => u.Province is null && !u.Bowed).ToArray();
        var fixedUnits = me.Units.Where(u => u.Province is not null).ToArray(); // the infantry already committed (the Cavalry declaration)
        var sending = assignment.Where(a => home.Any(u => u.Id == a.Key)).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
        var defenders = them.Home.Where(u => !u.Bowed).ToArray();

        (ThreatRead Threat, int Razed, bool[] GroupRazes, List<(int Province, int Strength, LedgerUnit[] Group)> Groups) Eval(IReadOnlyDictionary<string, int> send)
        {
            var groups = send.Select(a => (Id: a.Key, Province: a.Value)).Concat(fixedUnits.Select(u => (Id: u.Id, Province: u.Province!.Value)))
                .GroupBy(a => a.Province).Select(g => (Province: g.Key, Strength: them.Provinces.FirstOrDefault(p => p.Index == g.Key).Strength, Group: g.Select(a => me.Units.First(u => u.Id == a.Id)).ToArray())).ToList();
            var homeLeft = home.Where(u => !send.ContainsKey(u.Id)).ToArray();
            if (groups.Count == 0) return (Threat.Of(read, Threat.Defenders(homeLeft), Threat.Straightened(them.Units)), 0, [], groups);
            // their best reply (fewest Provinces lost, fewest defenders lost, most attackers destroyed), then each group's exchange under their likely hand
            Dictionary<int, LedgerUnit[]>? reply = null; ThreatCase? replyCase = null; ExchangeResult[]? runs = null;
            foreach (var defence in Threat.Defences(defenders, groups))
            {
                var r = groups.Select(g => Exchange.Run(me, g.Group, them, defence.GetValueOrDefault(g.Province, []), g.Strength, me.HandAnswers, read.Belief.Answers(false))).ToArray();
                var c = new ThreatCase(r.Count(x => x.Falls), r.Sum(x => x.DefendersDestroyed.Length), r.Sum(x => x.AttackersDestroyed.Length), "", "");
                if (replyCase is null || ThreatCase.Compare(c, replyCase) > 0) { reply = defence; replyCase = c; runs = r; }
            }
            var dead = runs!.SelectMany(r => r.DefendersDestroyed).ToArray();
            return (Threat.Of(read, Threat.Defenders(homeLeft), Threat.Straightened(them.Units, dead)), runs!.Count(r => r.Falls), runs!.Select(r => r.Falls).ToArray(), groups);
        }

        var start = Eval(sending);
        log.Add($"as prepared: {start.Razed} of {start.Groups.Count} groups raze; home {start.Threat}");
        if (start.Threat.Likely.ProvincesLost == 0) return new(sending, [], "home holds at likely: the attack stands", start.Threat, start.Threat, log.ToArray());
        if (lethal || start.Razed >= them.Provinces.Length) return new(sending, [], "the attack ends the game: it stands", start.Threat, start.Threat, log.ToArray()); // every Province they have left razes at likely
        if (posture == Posture.Race) return new(sending, [], $"race: the loss of {start.Threat.Likely.ProvincesLost} is accepted, the attack stands", start.Threat, start.Threat, log.ToArray());

        // remedy 1: keep back the weakest unit whose group still razes without it, as often as needed
        var send = new Dictionary<string, int>(sending, StringComparer.Ordinal); var kept = new List<string>(); var current = start;
        while (current.Threat.Likely.ProvincesLost > 0)
        {
            (string Id, int Force, (ThreatRead, int, bool[], List<(int, int, LedgerUnit[])>) Eval)? best = null;
            foreach (var (id, province) in send.OrderBy(a => me.Units.First(u => u.Id == a.Key).UnitForce))
            {
                var gi = current.Groups.FindIndex(g => g.Province == province);
                if (gi < 0 || !current.GroupRazes[gi]) continue; // a group that does not raze is handled by remedy 2
                var trial = new Dictionary<string, int>(send, StringComparer.Ordinal); trial.Remove(id);
                var e = Eval(trial);
                var still = e.Groups.FindIndex(g => g.Province == province) is var ti && ti >= 0 && e.GroupRazes[ti];
                if (!still) continue;
                var unit = me.Units.First(u => u.Id == id);
                if (best is null || e.Threat.Likely.ProvincesLost < best.Value.Eval.Item1.Likely.ProvincesLost || e.Threat.Likely.ProvincesLost == best.Value.Eval.Item1.Likely.ProvincesLost && unit.UnitForce < best.Value.Force) best = (id, unit.UnitForce, e);
                if (e.Threat.Likely.ProvincesLost == 0 && unit.UnitForce <= best.Value.Force) break;
            }
            if (best is null) break;
            send.Remove(best.Value.Id); kept.Add(best.Value.Id); current = best.Value.Eval;
            log.Add($"kept back {me.Units.First(u => u.Id == best.Value.Id).Name} ({best.Value.Force}F): home {current.Threat}");
        }
        if (current.Threat.Likely.ProvincesLost == 0) return new(send, kept.ToArray(), $"{kept.Count} kept back, the groups still raze: home holds at likely", start.Threat, current.Threat, log.ToArray());

        // remedy 2: keep back whole groups, the least valuable first (a group that does not raze, then the smallest), while a razing group remains
        var afterUnits = new Dictionary<string, int>(send, StringComparer.Ordinal); var keptUnits = kept.ToList(); var afterUnitsEval = current;
        while (current.Threat.Likely.ProvincesLost > 0 && current.Groups.Count > 1)
        {
            // only a group with a unit still to be declared can be kept back: the infantry already at a battlefield (the Cavalry declaration) is fixed, and dropping it would change nothing
            var order = current.Groups.Select((g, i) => (g, i)).Where(t => send.Values.Contains(t.g.Province)).OrderBy(t => current.GroupRazes[t.i] ? 1 : 0).ThenBy(t => t.g.Group.Sum(u => u.UnitForce)).ToList();
            if (order.Count == 0) break;
            var drop = order[0].g;
            var trial = send.Where(a => a.Value != drop.Province).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
            if (trial.Count == 0 || trial.Count == send.Count) break;
            var e = Eval(trial);
            if (e.Razed == 0) break; // no razing group would remain: that is remedy 3
            kept.AddRange(send.Where(a => a.Value == drop.Province).Select(a => a.Key)); send = trial; current = e;
            log.Add($"kept back the group at P{drop.Province + 1}: home {current.Threat}");
        }
        if (current.Threat.Likely.ProvincesLost == 0) return new(send, kept.ToArray(), $"{kept.Count} kept back (whole groups): home holds at likely", start.Threat, current.Threat, log.ToArray());

        // remedy 3: nobody attacks, when the whole army at home holds; else the attack is not what loses the Province
        var hold = Eval(new Dictionary<string, int>(StringComparer.Ordinal));
        log.Add($"nobody: home {hold.Threat}");
        var lostAfter = me.Provinces.Length - start.Threat.Likely.ProvincesLost; var theirsAfter = them.Provinces.Length - start.Razed;
        var accept = lostAfter >= theirsAfter && lostAfter >= 2;
        if (hold.Threat.Likely.ProvincesLost == 0)
            return accept
                ? new(sending, [], $"the loss of {start.Threat.Likely.ProvincesLost} is accepted: ahead or level ({lostAfter} to {theirsAfter}) with two or more left, and only the hold turns it", start.Threat, start.Threat, log.ToArray())
                : new(new Dictionary<string, int>(StringComparer.Ordinal), sending.Keys.ToArray(), "nobody attacks: only the whole army at home holds", start.Threat, hold.Threat, log.ToArray());
        return new(sending, [], $"even the whole army at home loses {hold.Threat.Likely.ProvincesLost}: the attack is not what loses it, it stands", start.Threat, start.Threat, log.ToArray());
    }
}
