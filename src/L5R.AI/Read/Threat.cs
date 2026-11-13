using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.5, question 2): threat(home), their best attack on their next turn against what will be unbowed at my home. Their whole army
// straightens before it attacks (every unit, bowed or not, with its abilities fresh and next turn's Gold); mine stays as it is (the units that attacked this turn stay bowed until my own
// Straighten Phase, a body bought this Dynasty arrives unbowed). They choose the attack that is worst for me (all-in at any Province, or a split across two) and I choose the defence that
// is best for me (nobody, everybody at one battlefield, or a balanced split), each judged by the exchange under the three opponent hands. The outcome is read in Mendon's tiers: lose a
// Province; hold; hold and lose no defender; hold and destroy attackers (RULE_FAMILIES.md family C).
public sealed record ThreatCase(int ProvincesLost, int DefendersLost, int AttackersLost, string Attack, string Defence, int Margin = 0)
{
    /// <summary>Mendon's tiers, cumulative: 0 a Province falls; 1 held; 2 held and no defender lost; 3 held, nobody lost and an attacker destroyed.</summary>
    public int Tier => ProvincesLost > 0 ? 0 : DefendersLost > 0 ? 1 : AttackersLost > 0 ? 3 : 2;
    /// <summary>Orders outcomes from the defender's view: fewer Provinces lost, then fewer defenders lost, then more attackers destroyed is better (positive when <paramref name="x"/> is better for the defender).</summary>
    public static int Compare(ThreatCase x, ThreatCase y) => x.ProvincesLost != y.ProvincesLost ? y.ProvincesLost.CompareTo(x.ProvincesLost) : x.DefendersLost != y.DefendersLost ? y.DefendersLost.CompareTo(x.DefendersLost) : x.AttackersLost.CompareTo(y.AttackersLost);
    public override string ToString() => $"lose {ProvincesLost}, def lost {DefendersLost}, atk lost {AttackersLost}, margin {Margin:+0;-0;0} [{Attack} v {Defence}]";
}

public sealed record ThreatRead(int AttackForce, int HomeForce, int WeakestStrength, int Candidates, ThreatCase Open, ThreatCase Likely, ThreatCase Worst)
{
    private static readonly ThreatCase Quiet = new(0, 0, 0, "none", "none");
    public static readonly ThreatRead None = new(0, 0, 0, 0, Quiet, Quiet, Quiet);
    public override string ToString() => $"threat {AttackForce} v home {HomeForce} + s{WeakestStrength} ({Candidates} attacks): open {Open.Tier}/{Open.ProvincesLost} likely {Likely.Tier}/{Likely.ProvincesLost} worst {Worst.Tier}/{Worst.ProvincesLost}; likely {Likely}";
}

public static class Threat
{
    /// <summary>Their army as it will be on their turn: every unit at home and unbowed with its abilities fresh. <paramref name="except"/> names the units my attack is expected to destroy.</summary>
    public static LedgerUnit[] Straightened(IEnumerable<LedgerUnit> theirs, IEnumerable<string>? except = null)
    {
        var gone = except?.ToHashSet(StringComparer.Ordinal) ?? [];
        return theirs.Where(u => !gone.Contains(u.Name) && !gone.Contains(u.Id)).Select(u => u with { Bowed = false, Spent = [], Province = null, ActionLocked = false }).ToArray();
    }

    /// <summary>What will defend: the home units that are unbowed now (a unit that attacked stays bowed through their turn), each with its abilities fresh for their turn.</summary>
    public static LedgerUnit[] Defenders(IEnumerable<LedgerUnit> mine) => mine.Where(u => u.Province is null && !u.Bowed).Select(u => u with { Spent = [] }).ToArray();

    /// <summary>Their best attack against <paramref name="home"/> with <paramref name="attackers"/>, under my best defence, under each of the three hands. Their Gold is next turn's full production.</summary>
    public static ThreatRead Of(Read read, LedgerUnit[] home, LedgerUnit[] attackers)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them with { GoldNow = read.Ledger.Them.GoldCapacity };
        var provinces = me.Provinces.OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        var army = attackers.Where(u => u.UnitForce > 0 || u.Abilities.Any(a => a.IsBattle)).ToArray();
        var attackForce = army.Sum(u => u.UnitForce); var homeForce = home.Sum(u => u.Contribution);
        if (army.Length == 0 || provinces.Length == 0) return ThreatRead.None with { HomeForce = homeForce, WeakestStrength = provinces.Length == 0 ? 0 : provinces[0].Strength };
        var candidates = Candidates(army, provinces).ToArray();
        ThreatCase Under(IEnumerable<Answer> theirHand)
        {
            ThreatCase? worstForMe = null;
            foreach (var attack in candidates)
            {
                ThreatCase? best = null;
                foreach (var defence in Defences(home, attack))
                {
                    int lost = 0, defLost = 0, atkLost = 0, margin = int.MinValue;
                    foreach (var (province, strength, group) in attack)
                    {
                        var r = Exchange.Run(them, group, me, defence.GetValueOrDefault(province, []), strength, theirHand, me.HandAnswers);
                        if (r.Falls) lost++;
                        defLost += r.DefendersDestroyed.Length; atkLost += r.AttackersDestroyed.Length;
                        margin = Math.Max(margin, r.AttackForce - r.DefenceForce - strength); // after the answers: how far past my defenders and Strength their worst battlefield reaches
                    }
                    var c = new ThreatCase(lost, defLost, atkLost, Describe(attack), string.Join(";", defence.Where(d => d.Value.Length > 0).Select(d => $"P{d.Key + 1}<-" + string.Join(",", d.Value.Select(u => u.Name)))) is { Length: > 0 } s ? s : "nobody", margin);
                    if (best is null || ThreatCase.Compare(c, best) > 0) best = c;   // my best defence against this attack
                }
                if (worstForMe is null || ThreatCase.Compare(best!, worstForMe) < 0) worstForMe = best;   // their attack that leaves me worst off
            }
            return worstForMe!;
        }
        return new(attackForce, homeForce, provinces[0].Strength, candidates.Length, Under([]), Under(read.Belief.Answers(false)), Under(read.Belief.Answers(true)));
    }

    private static string Describe(List<(int Province, int Strength, LedgerUnit[] Group)> attack) => string.Join(";", attack.Select(a => $"P{a.Province + 1}<-" + string.Join(",", a.Group.Select(u => u.Name))));

    /// <summary>All-in at each Province, and a balanced split across each pair where both groups exceed their Province's Strength on Force alone.</summary>
    private static IEnumerable<List<(int Province, int Strength, LedgerUnit[] Group)>> Candidates(LedgerUnit[] army, (int Index, int Strength)[] provinces)
    {
        foreach (var p in provinces) yield return [(p.Index, p.Strength, army)];
        if (army.Length < 2) yield break;
        var ordered = army.OrderByDescending(u => u.UnitForce).ToArray();
        for (var i = 0; i < provinces.Length; i++)
            for (var j = i + 1; j < provinces.Length; j++)
            {
                var g1 = new List<LedgerUnit>(); var g2 = new List<LedgerUnit>(); int f1 = 0, f2 = 0;
                foreach (var u in ordered)
                {
                    if (f1 - provinces[i].Strength <= f2 - provinces[j].Strength) { g1.Add(u); f1 += u.UnitForce; } else { g2.Add(u); f2 += u.UnitForce; }
                }
                if (f1 > provinces[i].Strength && f2 > provinces[j].Strength) yield return [(provinces[i].Index, provinces[i].Strength, g1.ToArray()), (provinces[j].Index, provinces[j].Strength, g2.ToArray())];
            }
    }

    /// <summary>My defences against one attack: nobody; everybody at each attacked Province; for a split, my units dealt to the battlefield where the shortfall is larger.</summary>
    private static IEnumerable<Dictionary<int, LedgerUnit[]>> Defences(LedgerUnit[] home, List<(int Province, int Strength, LedgerUnit[] Group)> attack)
    {
        yield return [];
        if (home.Length == 0) yield break;
        foreach (var (province, _, _) in attack) yield return new() { [province] = home };
        if (attack.Count < 2) yield break;
        var deal = attack.ToDictionary(a => a.Province, _ => new List<LedgerUnit>());
        var shortfall = attack.ToDictionary(a => a.Province, a => a.Group.Sum(u => u.UnitForce) - a.Strength);
        foreach (var u in home.OrderByDescending(u => u.Contribution))
        {
            var target = shortfall.MaxBy(s => s.Value).Key;
            deal[target].Add(u); shortfall[target] -= u.Contribution;
        }
        yield return deal.ToDictionary(d => d.Key, d => d.Value.ToArray());
    }
}
