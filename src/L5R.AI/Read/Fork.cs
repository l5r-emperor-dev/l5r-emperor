namespace L5R.AI;

// Issue #1206, strategy brief step 3, family B (docs/bots/strategy/RULE_FAMILIES.md B): Cavalry forcing. The order of a turn's declarations (RULES_DIGEST.md section 2) is attacker Infantry,
// defender Infantry, attacker Cavalry, defender Cavalry: the defender places its non-Cavalry units before it sees where the Cavalry goes, and a battlefield with no defending unit gives it
// no presence. So the Cavalry is a fork. At the Infantry declaration the Read weighs each infantry option (nothing, the Contender's own plan, a probe, all the infantry) by its forced
// value: the worst, over their infantry replies, of the best Cavalry follow-up (all the Cavalry at the weakest uncovered Province, the sufficient part of it, a split across uncovered
// Provinces each exceeding Strength alone, the Cavalry reinforcing a battlefield, the Cavalry home), each follow-up itself judged under their Cavalry reply (their unbowed home Cavalry
// to whichever battlefield hurts most). At the Cavalry declaration the follow-up is chosen against the defence actually placed. Every group sent to an uncovered Province is sized to
// exceed Strength by itself; Cavalry the fork does not need stays home, where it defends in the Cavalry step after seeing their attack.

/// <summary>What the fork would declare: the infantry (empty: nothing in the Infantry segment), the Cavalry follow-up (empty: home), the option's forced value against the hold, and why.</summary>
public sealed record ForkPlan(string Option, IReadOnlyDictionary<string, int> Infantry, string FollowUp, IReadOnlyDictionary<string, int> Cavalry, double Forced, string Why, string[] Log)
{
    /// <summary>Nothing is declared at all: the infantry stays and the Cavalry stays.</summary>
    public bool Holds => Infantry.Count == 0 && Cavalry.Count == 0;
}

public static class Fork
{
    private const double UnitWorth = .3;      // in Provinces, as the opportunity's (Questions)
    private const double AttackMargin = .05;  // the forced value an attack must clear over the hold

    /// <summary>The Infantry declaration: the infantry option with the highest forced value, when the seat has Cavalry to follow with. Null when it has none (the fork does not apply).</summary>
    /// <param name="prepared">the Contender's own infantry assignment (unit id to Province), offered as one option; null when it has none.</param>
    public static ForkPlan? AtInfantry(Read read, IReadOnlyDictionary<string, int>? prepared, Posture? posture)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var army = Army(read); var cavalry = army.Where(u => u.Cavalry).ToArray(); var infantry = army.Where(u => !u.Cavalry).ToArray();
        if (cavalry.Length == 0 || them.Provinces.Length == 0) return null;
        var provinces = them.Provinces.OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        var log = new List<string>(); var w = Answers.Weight(posture ?? Posture.Hold);
        var ctx = new Valuer(read, w);
        var options = new List<(string Label, List<(int Province, int Strength, LedgerUnit[] Group)> Groups)> { ("nothing", []) };
        if (prepared is { Count: > 0 })
        {
            var groups = prepared.Where(a => infantry.Any(u => u.Id == a.Key) && provinces.Any(p => p.Index == a.Value)).GroupBy(a => a.Value)
                .Select(g => (Province: g.Key, Strength: provinces.First(p => p.Index == g.Key).Strength, Group: g.Select(a => infantry.First(u => u.Id == a.Key)).ToArray())).ToList();
            if (groups.Count > 0) options.Add(("prepared", groups));
        }
        if (infantry.Length > 0)
        {
            var weakest = provinces[0];
            // the probe: the smallest infantry group that exceeds the weakest Province's Strength undefended (it has to be answered, or the Province falls)
            var probe = new List<LedgerUnit>(); foreach (var u in infantry) { probe.Add(u); if (probe.Sum(x => x.UnitForce) > weakest.Strength) break; }
            if (probe.Sum(x => x.UnitForce) > weakest.Strength) options.Add(("probe", [(weakest.Index, weakest.Strength, probe.ToArray())]));
            options.Add(("all infantry", [(weakest.Index, weakest.Strength, infantry)]));
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        (string Label, List<(int, int, LedgerUnit[])> Groups, double Forced, string FollowUp, Dictionary<string, int> Cavalry)? best = null;
        var theirInfantry = them.Home.Where(u => !u.Bowed && !u.Cavalry).ToArray(); var theirCavalry = them.Home.Where(u => !u.Bowed && u.Cavalry).ToArray();
        foreach (var (label, groups) in options)
        {
            if (!seen.Add(Threat.Describe(groups))) continue;
            // their infantry replies: nobody, everybody at each attacked Province, the dealt split (Threat.Defences); with nothing declared there is no battlefield to defend
            var replies = groups.Count == 0 ? new List<Dictionary<int, LedgerUnit[]>> { new() } : Threat.Defences(theirInfantry, groups).ToList();
            double forced = double.MaxValue; string forcedFollow = "home"; Dictionary<string, int> forcedCavalry = [];
            foreach (var reply in replies)
            {
                var (value, follow, assignment) = BestFollowUp(ctx, groups, reply, cavalry, theirCavalry, provinces, null);
                log.Add($"{label} v {Valuer.Describe(reply)}: best follow-up {follow} {value:+0.00;-0.00;0.00}");
                if (value < forced) { forced = value; forcedFollow = follow; forcedCavalry = assignment; }
            }
            log.Add($"{label}: forced {forced:+0.00;-0.00;0.00} ({forcedFollow})");
            if (best is null || forced > best.Value.Forced + 1e-9) best = (label, groups, forced, forcedFollow, forcedCavalry);
        }
        var b = best!.Value;
        var infantryAssignment = b.Groups.SelectMany(g => g.Item3.Select(u => (u.Id, g.Item1))).ToDictionary(t => t.Id, t => t.Item2, StringComparer.Ordinal);
        if (b.Forced <= AttackMargin)
            return new("nothing", new Dictionary<string, int>(StringComparer.Ordinal), "home", new Dictionary<string, int>(StringComparer.Ordinal), b.Forced, $"no option's forced value clears the hold (best {b.Label} {b.Forced:+0.00;-0.00;0.00})", log.ToArray());
        return new(b.Label, infantryAssignment, b.FollowUp, b.Cavalry, b.Forced, $"{b.Label}: forced {b.Forced:+0.00;-0.00;0.00}, its worst reply still leaves {b.FollowUp}", log.ToArray());
    }

    /// <summary>The Cavalry declaration: the follow-up with the best value against the defence as placed (the infantry already at battlefields is fixed), under their Cavalry reply.
    /// Null when the seat has no Cavalry at home to declare.</summary>
    public static ForkPlan? AtCavalry(Read read, Posture? posture)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var cavalry = Army(read).Where(u => u.Cavalry).ToArray();
        if (cavalry.Length == 0 || them.Provinces.Length == 0) return null;
        var provinces = them.Provinces.OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        var log = new List<string>(); var w = Answers.Weight(posture ?? Posture.Hold);
        var ctx = new Valuer(read, w);
        var fixedGroups = me.Units.Where(u => u.Province is not null).GroupBy(u => u.Province!.Value)
            .Select(g => (Province: g.Key, Strength: provinces.FirstOrDefault(p => p.Index == g.Key).Strength, Group: g.ToArray())).ToList();
        var placed = them.Units.Where(u => u.Province is not null).GroupBy(u => u.Province!.Value).ToDictionary(g => g.Key, g => g.ToArray());
        var theirCavalry = them.Home.Where(u => !u.Bowed && u.Cavalry).ToArray();
        var (value, follow, assignment) = BestFollowUp(ctx, fixedGroups, placed, cavalry, theirCavalry, provinces, log);
        var fixedAssignment = fixedGroups.SelectMany(g => g.Group.Select(u => (u.Id, g.Province))).ToDictionary(t => t.Id, t => t.Province, StringComparer.Ordinal);
        return new("cavalry", fixedAssignment, follow, assignment, value, $"{follow}: value {value:+0.00;-0.00;0.00} against the defence as placed", log.ToArray());
    }

    /// <summary>The units an attack may use, as the opportunity's: unbowed, free to act, with 2 Force or more, at home.</summary>
    private static LedgerUnit[] Army(Read read) => read.Ledger.Me.Home.Where(u => !u.Bowed && !u.ActionLocked && u.UnitForce >= 2).OrderByDescending(u => u.UnitForce).ThenBy(u => u.Id, StringComparer.Ordinal).ToArray();

    /// <summary>The Cavalry follow-ups to an infantry position and a placed defence, each judged under their Cavalry reply; the best by value.</summary>
    private static (double Value, string Label, Dictionary<string, int> Assignment) BestFollowUp(Valuer ctx, List<(int Province, int Strength, LedgerUnit[] Group)> groups,
        Dictionary<int, LedgerUnit[]> reply, LedgerUnit[] cavalry, LedgerUnit[] theirCavalry, (int Index, int Strength)[] provinces, List<string>? log)
    {
        var follows = new List<(string Label, List<(int Province, int Strength, LedgerUnit[] Group)> Add)> { ("home", []) };
        var attacked = groups.Select(g => g.Province).ToHashSet();
        var uncovered = provinces.Where(p => !attacked.Contains(p.Index) && !(reply.TryGetValue(p.Index, out var d) && d.Length > 0)).ToArray();
        if (uncovered.Length > 0)
        {
            var weakest = uncovered[0];
            follows.Add(("all Cavalry at the weakest uncovered", [(weakest.Index, weakest.Strength, cavalry)]));
            var part = new List<LedgerUnit>(); foreach (var u in cavalry) { part.Add(u); if (part.Sum(x => x.UnitForce) > weakest.Strength) break; }
            if (part.Count < cavalry.Length && part.Sum(x => x.UnitForce) > weakest.Strength) follows.Add(("the sufficient Cavalry at the weakest uncovered", [(weakest.Index, weakest.Strength, part.ToArray())]));
            // a split across uncovered Provinces, each group exceeding Strength alone, strongest units first; a Province whose group falls short is not attacked
            var split = new List<(int, int, LedgerUnit[])>(); var pool = new Queue<LedgerUnit>(cavalry);
            foreach (var p in uncovered)
            {
                var g = new List<LedgerUnit>(); while (pool.Count > 0 && g.Sum(x => x.UnitForce) <= p.Strength) g.Add(pool.Dequeue());
                if (g.Sum(x => x.UnitForce) > p.Strength) split.Add((p.Index, p.Strength, g.ToArray())); else break;
            }
            if (split.Count >= 2) follows.Add(("a split across uncovered Provinces", split));
        }
        foreach (var g in groups) follows.Add(($"all Cavalry reinforcing P{g.Province + 1}", [(g.Province, g.Strength, cavalry)]));
        (double Value, string Label, Dictionary<string, int> Assignment)? best = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (label, add) in follows)
        {
            var all = Merge(groups, add);
            if (!seen.Add(Threat.Describe(all) + "|" + label.StartsWith("home"))) continue;
            // their Cavalry reply: all of it to the one battlefield that hurts most, or none of it
            var value = double.MaxValue; string worstReply = "none";
            foreach (var where in new int?[] { null }.Concat(all.Select(g => (int?)g.Province)))
            {
                if (where is { } p && theirCavalry.Length == 0) continue;
                var defence = new Dictionary<int, LedgerUnit[]>(reply);
                if (where is { } at) defence[at] = defence.GetValueOrDefault(at, []).Concat(theirCavalry).ToArray();
                var v = ctx.Value(all, defence);
                if (v < value) { value = v; worstReply = where is { } w ? $"their Cavalry to P{w + 1}" : "their Cavalry home"; }
            }
            log?.Add($"{label} [{Threat.Describe(all)}] v {Valuer.Describe(reply)}: {value:+0.00;-0.00;0.00} ({worstReply})");
            if (best is null || value > best.Value.Value + 1e-9) best = (value, label, add.SelectMany(g => g.Group.Select(u => (u.Id, g.Province))).ToDictionary(t => t.Id, t => t.Province, StringComparer.Ordinal));
        }
        return best!.Value;
    }

    private static List<(int Province, int Strength, LedgerUnit[] Group)> Merge(List<(int Province, int Strength, LedgerUnit[] Group)> groups, List<(int Province, int Strength, LedgerUnit[] Group)> add)
    {
        var all = groups.ToList();
        foreach (var a in add)
        {
            var i = all.FindIndex(g => g.Province == a.Province);
            if (i >= 0) all[i] = (a.Province, a.Strength, all[i].Group.Concat(a.Group).ToArray()); else all.Add(a);
        }
        return all;
    }

    /// <summary>Values one attack against one placed defence: expected Provinces by the calibrated cell, kills and losses, less the weighted cost of the counter-attack it leaves home open to
    /// (the exposure, cached by what stays home and what the attack is expected to destroy) over the hold's own exposure.</summary>
    private sealed class Valuer(Read read, double weight)
    {
        private readonly Dictionary<string, double> exposure = new(StringComparer.Ordinal);
        private double? hold;
        private double Hold => hold ??= Exposure(read.Ledger.Me.Home.ToArray(), []);

        public double Value(List<(int Province, int Strength, LedgerUnit[] Group)> groups, Dictionary<int, LedgerUnit[]> defence)
        {
            var me = read.Ledger.Me; var them = read.Ledger.Them;
            var attacking = groups.SelectMany(g => g.Group).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
            var homeLeft = me.Home.Where(u => !attacking.Contains(u.Id)).ToArray();
            if (groups.Count == 0) return -weight * (Exposure(homeLeft, []) - Hold);
            double provinces = 0; int kills = 0, losses = 0; var dead = new List<string>();
            foreach (var g in groups)
            {
                var d = defence.GetValueOrDefault(g.Province, []);
                var open = Exchange.Run(me, g.Group, them, d, g.Strength, me.HandAnswers, []); var likely = Exchange.Run(me, g.Group, them, d, g.Strength, me.HandAnswers, read.Belief.Answers(false)); var worst = Exchange.Run(me, g.Group, them, d, g.Strength, me.HandAnswers, read.Belief.Answers(true));
                var cell = $"{(open.Falls ? 1 : 0)}{(likely.Falls ? 1 : 0)}{(worst.Falls ? 1 : 0)}";
                provinces += Calibration.PFall(cell) * Questions.ProvinceWorth(them.Provinces.Length);
                kills += likely.DefendersDestroyed.Length; losses += likely.AttackersDestroyed.Length; dead.AddRange(likely.DefendersDestroyed);
            }
            return provinces + UnitWorth * (kills - losses) - weight * (Exposure(homeLeft, dead) - Hold);
        }

        private double Exposure(LedgerUnit[] homeLeft, List<string> dead)
        {
            var key = string.Join(",", homeLeft.Select(u => u.Id).OrderBy(x => x, StringComparer.Ordinal)) + "|" + string.Join(",", dead.OrderBy(x => x, StringComparer.Ordinal));
            if (exposure.TryGetValue(key, out var cost)) return cost;
            var t = Threat.Of(read, Threat.Defenders(homeLeft), Threat.Straightened(read.Ledger.Them.Units, dead));
            cost = Calibration.PLose(t.Cell, t.Gap) * Questions.ProvinceWorth(read.Ledger.Me.Provinces.Length);
            exposure[key] = cost;
            return cost;
        }

        public static string Describe(Dictionary<int, LedgerUnit[]> defence) => defence.Count == 0 || defence.All(d => d.Value.Length == 0) ? "nobody" : string.Join(";", defence.Where(d => d.Value.Length > 0).Select(d => $"P{d.Key + 1}<-{d.Value.Length}"));
    }
}
