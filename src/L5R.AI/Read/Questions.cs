using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.5): the three questions the Read answers at every rebuild, and the posture they set. Opportunity: my best attack and its gain,
// among the shapes of RULE_FAMILIES.md family A (the sufficient group, a split, the lone immune unit, everything, hold), each against the defender's best reply, scored as
// gain - weight(posture) x (exposure(choice) - exposure(hold)) where exposure is threat(home) after the choice. Race: each side's turns to each win route at the current rates. Posture: family G's
// seed rule over the three. Shadow mode: computed, logged, driving nothing.

/// <summary>The rates behind the race, from what the bot has seen of the game so far (BotContext.History): Honor per own turn for each side, Provinces taken per own turn.</summary>
public sealed record Trend(double MyHonorPerTurn, double TheirHonorPerTurn, double MyProvincesPerTurn, double TheirProvincesPerTurn)
{
    public static readonly Trend None = new(0, 0, 0, 0);
}

/// <summary>The fitted rates the Read is read through (experiment note of 2026-11-13: the battle Read fitted on block 9400, the threat on 9400, both scored on 9600). A cell is the three-hand verdict.</summary>
public static class Calibration
{
    /// <summary>P(the Province falls) by the exchange's cell at defence (open/likely/worst falls), "Read at defence, or undefended when nobody defended".</summary>
    public static double PFall(string cell) => cell switch { "000" => .10, "001" => .24, "011" => .45, "111" => .79, "010" => .30, "100" => .50, "101" => .60, "110" => .70, _ => .5 };

    // the eight gap buckets of threats.py: <= -10, -9..-5, -4..-1, 0..2, 3..5, 6..9, 10..14, > 14
    private static readonly Dictionary<string, double[]> Lose = new()
    {
        ["000"] = [.03, .04, .23, .30, .37, .44, .50, .57],
        ["001"] = [.05, .24, .42, .47, .58, .66, .67, .76],
        ["011"] = [.05, .24, .52, .71, .80, .88, .88, .88],
        ["111"] = [.05, .20, .49, .71, .82, .87, .91, .95],
    };
    /// <summary>P(I lose a Province on their next turn) by the threat's cell and its Force gap (their straightened army against my unbowed home and weakest Strength).</summary>
    public static double PLose(string cell, int gap)
    {
        var row = Lose.GetValueOrDefault(cell) ?? Lose["001"];
        var i = gap <= -10 ? 0 : gap <= -5 ? 1 : gap < 0 ? 2 : gap <= 2 ? 3 : gap <= 5 ? 4 : gap <= 9 ? 5 : gap <= 14 ? 6 : 7;
        return row[i];
    }
}

/// <summary>One attack the Read considered: its groups, the defender's best reply, the cell and fall probability per battlefield, what it is expected to cost and destroy, the threat it leaves.</summary>
public sealed record AttackOption(string Label, Dictionary<string, int> Assignment, List<(int Province, int Strength, LedgerUnit[] Group)> Groups, string Reply, string Cells, double ExpectedProvinces,
    int ExpectedKills, int ExpectedLosses, int CardsSpent, double Gain, ThreatRead Exposure, double ExposureCost)
{
    public double Score { get; set; }
    public bool IsHold => Groups.Count == 0;
    public override string ToString() => IsHold ? $"hold: exposure {ExposureCost:0.00} ({Exposure.Cell} gap {Exposure.Gap:+0;-0;0})"
        : $"{Label} [{Threat.Describe(Groups)}] v {Reply}: cells {Cells}, provinces {ExpectedProvinces:0.00}, kills {ExpectedKills}, losses {ExpectedLosses}, cards {CardsSpent}, gain {Gain:0.00}, exposure {ExposureCost:0.00} ({Exposure.Cell} gap {Exposure.Gap:+0;-0;0}), score {Score:0.00}";
}

public sealed record Opportunity(AttackOption Best, AttackOption Hold, AttackOption[] Options, string Gate)
{
    public bool Attacks => !Best.IsHold;
    public override string ToString() => $"{(Gate.Length > 0 ? Gate + "; " : "")}best {Best}";
}

/// <summary>Turns until each side wins by each route at the current rates; 99 when the route is closed.</summary>
public sealed record Clocks(double MyMilitary, double MyHonor, double MyDrain, double TheirMilitary, double TheirHonor, double TheirDrain)
{
    public const double Never = 99;
    public double MyFastest => Math.Min(MyMilitary, Math.Min(MyHonor, MyDrain));
    public double TheirFastest => Math.Min(TheirMilitary, Math.Min(TheirHonor, TheirDrain));
    public string MyRoute => MyMilitary <= MyHonor && MyMilitary <= MyDrain ? "military" : MyHonor <= MyDrain ? "honor" : "drain";
    public override string ToString() => $"me {MyMilitary:0.#}/{MyHonor:0.#}/{MyDrain:0.#} ({MyRoute}) them {TheirMilitary:0.#}/{TheirHonor:0.#}/{TheirDrain:0.#}";
}

public enum Posture { Hold, Press, Build, Race, Lock, Drain, Punish, Wait }

public sealed record Answers(Opportunity Opportunity, ThreatRead Threat, Clocks Clocks, Posture Posture, string Why)
{
    /// <summary>Family A's weight on the counter-attack's price by posture: a race or a press pays little for exposure, a lock or a drain pays most.</summary>
    public static double Weight(Posture p) => p switch { Posture.Race => .25, Posture.Press or Posture.Punish => .5, Posture.Lock or Posture.Drain or Posture.Wait => 1.5, _ => 1.0 };
    public override string ToString() => $"posture {Posture} ({Why}); clocks {Clocks}; threat {Threat.Cell} gap {Threat.Gap:+0;-0;0} likely {Threat.Likely}; opportunity {Opportunity}";
}

public static class Questions
{
    private const double UnitWorth = .3, CardWorth = .1; // in Provinces
    private const int RaceHorizon = 6;                   // a race is a race only when the end is in sight
    private const double RiskNothing = .1;               // in Provinces: what a lock or a drain may expose for an attack

    /// <summary>The value of taking one of <paramref name="theirs"/> Provinces when they have that many left: the last ends the game.</summary>
    public static double ProvinceWorth(int left) => left <= 1 ? 4 : left == 2 ? 2 : 1;

    public static Answers Of(Read read, Trend? trend = null)
    {
        trend ??= Trend.None;
        var threat = read.ThreatNow();
        var options = Options(read).ToList();
        var hold = options.Single(o => o.IsHold);
        var clocks = ClocksOf(read, trend, options, threat);
        var (posture, why) = PostureOf(read, options, hold, threat, clocks);
        var w = Answers.Weight(posture);
        foreach (var o in options) o.Score = o.Gain - w * (o.ExposureCost - hold.ExposureCost);
        // lock and drain (PLAYER_MODEL.md 3.1: "attack only when it costs nothing I need"): an attack that raises the exposure is not taken whatever its gain
        var eligible = posture is Posture.Lock or Posture.Drain ? options.Where(o => o.IsHold || o.ExposureCost - hold.ExposureCost <= RiskNothing).ToList() : options;
        var best = eligible.OrderByDescending(o => o.Score).ThenBy(o => o.IsHold ? 0 : 1).ThenBy(o => o.Assignment.Count).First();
        // their last Province falls: the last turn of the game sends everything (PLAYER_MODEL.md 3.2); the exchange's cell cannot see the extra safety the rest of the army buys
        if (!best.IsHold && read.Ledger.Them.Provinces.Length == 1 && options.FirstOrDefault(o => o.Label == "everything") is { } all && all.Cells == best.Cells) best = all;
        var gate = Gate(read, options);
        if (gate is { Length: > 0 } && gate.StartsWith("hold")) best = hold;
        return new(new(best, hold, options.ToArray(), gate ?? ""), threat, clocks, posture, why);
    }

    /// <summary>PLAYER_MODEL.md 3.1's gates: nothing exceeds Strength undefended even with late Force; nobody unbowed to defend; have below need everywhere.</summary>
    private static string? Gate(Read read, List<AttackOption> options)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var army = Army(read); if (army.Length == 0 || them.Provinces.Length == 0) return "hold: no army or no Province";
        var late = me.HandCount > 0 ? army.Count(u => u.Tactician) * me.BestFocus : 0;
        if (army.Sum(u => u.UnitForce) + late <= them.Provinces.Min(p => p.Strength)) return "hold: no group exceeds Strength even undefended";
        if (!them.Home.Any(u => !u.Bowed)) return "attack: they have no unbowed Personality";
        if (options.Where(o => !o.IsHold).All(o => o.ExpectedProvinces < .2)) return "hold: have below need at every Province";
        return null;
    }

    /// <summary>The units an attack may use: unbowed, free to act, with 2 Force or more. A 0 to 1 Force utility Personality is a target, not security (PLAYER_MODEL.md 3.2); it stays home.</summary>
    private static LedgerUnit[] Army(Read read) => read.Ledger.Me.Home.Where(u => !u.Bowed && !u.ActionLocked && u.UnitForce >= 2).OrderByDescending(u => u.UnitForce).ThenBy(u => u.Id, StringComparer.Ordinal).ToArray();

    /// <summary>Family A's candidates, each played against the defender's best reply.</summary>
    private static IEnumerable<AttackOption> Options(Read read)
    {
        var army = Army(read); var provinces = read.Ledger.Them.Provinces.OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        yield return Evaluate(read, "hold", [], army);
        if (army.Length == 0 || provinces.Length == 0) yield break;
        var weakest = provinces[0];
        IEnumerable<AttackOption> One(string label, List<(int, int, LedgerUnit[])> groups)
        {
            var key = Threat.Describe(groups);
            if (seen.Add(key)) yield return Evaluate(read, label, groups, army);
        }
        foreach (var o in One("everything", [(weakest.Index, weakest.Strength, army)])) yield return o;
        // sufficient: at each Province, the smallest group (strongest first) whose exchange under the likely hand razes against the defender's best reply
        foreach (var p in provinces)
        {
            for (var n = 1; n < army.Length; n++)
            {
                var group = army.Take(n).ToArray();
                var trial = Evaluate(read, "sufficient", [(p.Index, p.Strength, group)], army);
                if (trial.Cells.Split(',').All(c => c[1] == '1')) { foreach (var o in One("sufficient", [(p.Index, p.Strength, group)])) yield return o; break; }
            }
        }
        // split: a balanced pair of groups across two Provinces (PLAYER_MODEL.md 3.2); the exchange judges each with its late Force, so a group below Strength on paper is still offered
        foreach (var split in Threat.Candidates(army, provinces, requireExceed: false).Where(c => c.Count == 2)) foreach (var o in One("split", split)) yield return o;
        // lone: every answer they have shown (Fate cards seen, printed Battle neutralisers on their units) is conditional and one unit fails all of them: that unit alone at the strongest Province it exceeds
        var printed = read.Ledger.Them.Units.SelectMany(u => u.UsableBattle.Where(a => a.Neutralises)).ToArray();
        var shown = read.Belief.AnswersSeen + printed.Length;
        if (shown > 0 && read.Belief.ConditionsSeen.Length + printed.Count(Conditional) == shown)
        {
            var performer = read.Ledger.Them.Units.OrderByDescending(u => u.UnitForce).Select(u => new ExchangeUnit(u)).FirstOrDefault();
            var bestChi = read.Ledger.Them.Units.Select(u => u.Chi).DefaultIfEmpty(0).Max();
            foreach (var u in army)
            {
                var me = new ExchangeUnit(u);
                var immune = read.Belief.ConditionsSeen.All(conds => !Exchange.Legal(new EffectDescriptor("shown", "Battle", null, EffectClass.SendHome, null, false, TargetSide.Enemy, TargetKind.UnitCard, conds, [], null, false, false, false, false, false, 0, 0, false, false, 1, false, null), me, performer))
                    && printed.All(a => a.Class == EffectClass.Duel ? u.Chi > bestChi : !Exchange.Legal(a, me, performer));
                if (!immune) continue;
                var target = provinces.Where(p => u.UnitForce > p.Strength).OrderByDescending(p => p.Strength).FirstOrDefault();
                if (target.Strength > 0 || provinces.Any(p => u.UnitForce > p.Strength)) foreach (var o in One("lone", [(target.Index, target.Strength, [u])])) yield return o;
                break;
            }
        }
    }

    /// <summary>A printed answer that can miss: it carries a condition, or it is a duel (won on Chi).</summary>
    private static bool Conditional(EffectDescriptor a) => a.Conditions.Length > 0 || a.Class == EffectClass.Duel;

    /// <summary>One attack against the defender's best reply among nobody, everybody at one battlefield and a balanced split; the cells and fall probabilities per battlefield under the chosen reply; the threat the attack leaves.</summary>
    private static AttackOption Evaluate(Read read, string label, List<(int Province, int Strength, LedgerUnit[] Group)> groups, LedgerUnit[] army)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var attacking = groups.SelectMany(g => g.Group).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var assignment = groups.SelectMany(g => g.Group.Select(u => (u.Id, g.Province))).ToDictionary(t => t.Id, t => t.Province, StringComparer.Ordinal);
        var homeLeft = me.Home.Where(u => !attacking.Contains(u.Id)).ToArray();
        if (groups.Count == 0)
        {
            var quiet = Threat.Of(read, Threat.Defenders(homeLeft), Threat.Straightened(them.Units));
            return new(label, assignment, groups, "-", "", 0, 0, 0, 0, 0, quiet, Calibration.PLose(quiet.Cell, quiet.Gap) * ProvinceWorth(me.Provinces.Length));
        }
        var defenders = them.Home.Where(u => !u.Bowed).ToArray();
        // the defender's best reply, judged under the likely hand from its side (fewer Provinces lost, fewer defenders lost, more attackers destroyed)
        Dictionary<int, LedgerUnit[]>? reply = null; ThreatCase? replyCase = null; ExchangeResult[]? replyRuns = null;
        foreach (var defence in Threat.Defences(defenders, groups))
        {
            var runs = groups.Select(g => Exchange.Run(me, g.Group, them, defence.GetValueOrDefault(g.Province, []), g.Strength, me.HandAnswers, read.Belief.Answers(false))).ToArray();
            var c = new ThreatCase(runs.Count(r => r.Falls), runs.Sum(r => r.DefendersDestroyed.Length), runs.Sum(r => r.AttackersDestroyed.Length), "", "");
            if (replyCase is null || ThreatCase.Compare(c, replyCase) > 0) { reply = defence; replyCase = c; replyRuns = runs; }
        }
        var cells = new List<string>(); double provinces = 0;
        foreach (var g in groups)
        {
            var d = reply!.GetValueOrDefault(g.Province, []);
            var open = Exchange.Run(me, g.Group, them, d, g.Strength, me.HandAnswers, []); var likely = Exchange.Run(me, g.Group, them, d, g.Strength, me.HandAnswers, read.Belief.Answers(false)); var worst = Exchange.Run(me, g.Group, them, d, g.Strength, me.HandAnswers, read.Belief.Answers(true));
            var cell = $"{(open.Falls ? 1 : 0)}{(likely.Falls ? 1 : 0)}{(worst.Falls ? 1 : 0)}"; cells.Add(cell);
            provinces += Calibration.PFall(cell) * ProvinceWorth(them.Provinces.Length);
        }
        var runsChosen = replyRuns ?? [];
        var kills = runsChosen.Sum(r => r.DefendersDestroyed.Length); var losses = runsChosen.Sum(r => r.AttackersDestroyed.Length);
        var cards = runsChosen.Sum(r => r.AttackerPlayed.Count(p => me.HandAnswers.Any(h => p.StartsWith(h.ToString(), StringComparison.Ordinal)))); // the own hand cards the exchange spent
        var gain = provinces + UnitWorth * (kills - losses) - CardWorth * cards;
        var expectedDead = runsChosen.SelectMany(r => r.DefendersDestroyed).ToArray();
        var exposure = Threat.Of(read, Threat.Defenders(homeLeft), Threat.Straightened(them.Units, expectedDead));
        var replyText = reply!.Count == 0 || reply.All(r => r.Value.Length == 0) ? "nobody" : string.Join(";", reply.Where(r => r.Value.Length > 0).Select(r => $"P{r.Key + 1}<-{r.Value.Length}"));
        return new(label, assignment, groups, replyText, string.Join(",", cells), provinces, kills, losses, cards, gain, exposure, Calibration.PLose(exposure.Cell, exposure.Gap) * ProvinceWorth(me.Provinces.Length));
    }

    /// <summary>Turns to each win: Provinces at the rate my best attack and their threat take them, Honor at the rate it has climbed, the other's Honor at the rate it has fallen.</summary>
    private static Clocks ClocksOf(Read read, Trend trend, List<AttackOption> options, ThreatRead threat)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var myRate = Math.Max(trend.MyProvincesPerTurn, options.Max(o => o.ExpectedProvinces / Math.Max(1, ProvinceWorth(them.Provinces.Length))));
        var theirRate = Math.Max(trend.TheirProvincesPerTurn, Calibration.PLose(threat.Cell, threat.Gap));
        double Turns(double left, double rate) => rate < .05 ? Clocks.Never : Math.Min(Clocks.Never, Math.Ceiling(left / rate));
        return new(
            them.Provinces.Length == 0 ? 0 : Turns(them.Provinces.Length, myRate),
            me.Honor >= 40 ? 0 : Turns(40 - me.Honor, trend.MyHonorPerTurn),
            them.Honor <= -20 ? 0 : Turns(them.Honor + 20, -trend.TheirHonorPerTurn),
            me.Provinces.Length == 0 ? 0 : Turns(me.Provinces.Length, theirRate),
            them.Honor >= 40 ? 0 : Turns(40 - them.Honor, trend.TheirHonorPerTurn),
            me.Honor <= -20 ? 0 : Turns(me.Honor + 20, -trend.MyHonorPerTurn));
    }

    /// <summary>RULE_FAMILIES.md family G's seed rule, in its order.</summary>
    private static (Posture, string) PostureOf(Read read, List<AttackOption> options, AttackOption hold, ThreatRead threat, Clocks clocks)
    {
        var them = read.Ledger.Them; var me = read.Ledger.Me;
        var bestGain = options.Where(o => !o.IsHold).DefaultIfEmpty(hold).Max(o => o.Gain);
        var bestProvinces = options.Max(o => o.ExpectedProvinces);
        var holds = threat.Likely.ProvincesLost == 0;
        // they win before my next turn: nothing to lose, unless it is my last Province that falls and holding still saves it more often than not (then the exposure keeps its full weight)
        if (clocks.TheirFastest <= 1 && clocks.MyFastest > 1 && !(me.Provinces.Length == 1 && hold.ExposureCost < .85 * ProvinceWorth(1))) return (Posture.Race, "they win before my next turn unless this turn changes it: nothing to lose");
        if (them.Units.Length > 0 && !them.Home.Any(u => !u.Bowed)) return (Posture.Punish, "their Personalities are bowed");
        if (them.Provinces.Length == 1 && bestProvinces >= .5 * ProvinceWorth(1)) return (Posture.Press, "their last Province falls to my best attack");
        if (them.Provinces.Length == 1 && clocks.TheirFastest > 1) return (Posture.Wait, "their last Province; they cannot win before my next turn");
        // a non-military route that is certain before their clock and near enough to plan on (PLAYER_MODEL.md 3.1: "attack only when it costs nothing I need"); the posture's weight then prices every attack's exposure
        if (clocks.MyHonor <= RaceHorizon && clocks.MyHonor < clocks.TheirFastest && holds) return (Posture.Lock, $"my Honor clock {clocks.MyHonor:0} beats their fastest {clocks.TheirFastest:0} while home holds");
        if (clocks.MyDrain < Clocks.Never && (clocks.MyRoute == "drain" || clocks.MyDrain <= RaceHorizon && clocks.MyDrain < clocks.TheirFastest)) return (Posture.Drain, $"the drain wins in {clocks.MyDrain:0} turns{(clocks.MyRoute == "drain" ? ", my shortest route" : "")}");
        if (clocks.TheirFastest < Clocks.Never && clocks.MyMilitary + 1 <= clocks.TheirFastest && clocks.MyMilitary <= RaceHorizon) return (Posture.Race, $"my military clock {clocks.MyMilitary:0} is a turn shorter than their fastest {clocks.TheirFastest:0}");
        if (bestGain >= .8 && bestProvinces >= .5) return (Posture.Press, $"my best attack expects {bestProvinces:0.0} Provinces");
        if (bestProvinces < .3 && holds) return (Posture.Build, "their answers beat my units and home holds");
        return (Posture.Hold, "nothing decides");
    }
}
