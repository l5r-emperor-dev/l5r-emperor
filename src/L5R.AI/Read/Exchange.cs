using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.4): the exchange, a projection of one battle. The two sides alternate, defender first, each playing its best neutraliser on its best target
// while it is not getting the result it wants and passing when it is; the battle then resolves by the rules (RULES_DIGEST.md section 3): the attacker needs strictly more Force than the defenders plus
// Strength to destroy the Province, a tie destroys both armies and no Province, a bowed unit stays, counts nothing and dies with a losing army, a unit sent home survives. Arithmetic over a dozen
// steps on the ledger; never a match clone.
public sealed record ExchangeUnit(LedgerUnit Unit)
{
    public int Force { get; set; } = Unit.Contribution; public bool Present { get; set; } = true; public bool Dead { get; set; }
    public bool Bowed { get; set; } = Unit.Bowed; public bool Locked { get; set; } = Unit.ActionLocked; public bool Followers { get; set; } = Unit.Followers > 0;
    public int Contribution => Present && !Bowed ? Force : 0;
}

public sealed record ExchangeResult(bool Fought, bool Falls, bool AttackerWins, bool Tie, int AttackForce, int DefenceForce, string[] AttackersDestroyed, string[] DefendersDestroyed, string[] AttackerPlayed, string[] DefenderPlayed)
{
    public override string ToString() => $"{(Falls ? "FALLS" : AttackerWins ? "won, holds" : Tie ? "tie" : "held")} {AttackForce} v {DefenceForce}; atk lost {AttackersDestroyed.Length}, def lost {DefendersDestroyed.Length}; played {string.Join(",", AttackerPlayed)} | {string.Join(",", DefenderPlayed)}";
}

public static class Exchange
{
    private const int MaxSteps = 14;

    /// <summary>The fewest defenders that must be neutralised for the attackers to exceed the rest plus Strength; null when even all of them is not enough.</summary>
    public static int? Need(IEnumerable<LedgerUnit> attackers, IEnumerable<LedgerUnit> defenders, int strength)
    {
        var attack = attackers.Sum(u => u.Contribution); var ordered = defenders.Select(u => u.Contribution).OrderByDescending(f => f).ToList(); var rest = ordered.Sum();
        for (var k = 0; k <= ordered.Count; k++)
        {
            if (attack > rest + strength) return k;
            if (k < ordered.Count) rest -= ordered[k];
        }
        return null;
    }

    /// <summary>The neutralisers a side can use at a battle now: its units' unspent Battle abilities (a unit there and unbowed), the table's, and the hand's.</summary>
    public static Answer[] Have(IEnumerable<LedgerUnit> unitsThere, LedgerSide side, IEnumerable<Answer> hand)
    {
        var answers = new List<Answer>();
        foreach (var u in unitsThere) foreach (var d in u.UsableBattle.Where(d => d.Neutralises)) answers.Add(new(d, u.Name, false, d.GoldCost));
        answers.AddRange(side.TableAnswers.Where(a => a.Effect.Neutralises));
        answers.AddRange(hand.Where(a => a.Effect.Neutralises));
        return answers.ToArray();
    }

    /// <summary>Plays the battle out. <paramref name="attackHand"/> and <paramref name="defenceHand"/> are the hand answers each side is assumed to hold (its own exactly, the opponent's from the belief or nothing).</summary>
    public static ExchangeResult Run(LedgerSide attacker, LedgerUnit[] attackers, LedgerSide defender, LedgerUnit[] defenders, int strength, IEnumerable<Answer> attackHand, IEnumerable<Answer> defenceHand)
    {
        var a = attackers.Select(u => new ExchangeUnit(u)).ToList(); var d = defenders.Select(u => new ExchangeUnit(u)).ToList();
        if (a.Count == 0) return new(false, false, false, false, 0, d.Sum(u => u.Contribution), [], [], [], []);
        var aAnswers = Available(a, attacker, attackHand); var dAnswers = Available(d, defender, defenceHand);
        var aGold = attacker.GoldNow; var dGold = defender.GoldNow; var aPlayed = new List<string>(); var dPlayed = new List<string>();
        var passes = 0; var defenderTurn = true;
        for (var step = 0; step < MaxSteps && passes < 2; step++, defenderTurn = !defenderTurn)
        {
            var (mine, theirs, answers, gold, played, want) = defenderTurn
                ? (d, a, dAnswers, dGold, dPlayed, (Func<bool>)(() => Force(a) <= Force(d) || d.All(u => !u.Present)))   // the defender wants its army kept, which keeps the Province (Mendon's tiers: keep the Province; better, lose no defender)
                : (a, d, aAnswers, aGold, aPlayed, () => Force(a) > Force(d) + strength);                                            // the attacker wants it destroyed
            var hasPresence = mine.Any(u => u.Present);
            if (want() || !hasPresence) { passes++; continue; }
            var best = Best(mine, theirs, answers, gold, defenderTurn ? strength : -1);
            if (best is null) { passes++; continue; }
            passes = 0;
            var (answer, target) = best.Value;
            answers.Remove(answer); gold -= answer.Gold; if (defenderTurn) dGold = gold; else aGold = gold;
            played.Add(answer.ToString() + (target is null ? "" : ">" + target.Unit.Name));
            Apply(answer.Effect, mine, theirs, target);
            // the other side may hold a negation: it cancels this answer when the answer was decisive
            var negate = (defenderTurn ? aAnswers : dAnswers).FirstOrDefault(x => x.Effect.Class == EffectClass.Negate);
            if (negate is not null && target is not null && Decisive(defenderTurn, a, d, strength)) { (defenderTurn ? aAnswers : dAnswers).Remove(negate); Undo(answer.Effect, mine, theirs, target); (defenderTurn ? aPlayed : dPlayed).Add("Negate>" + answer); }
        }
        var attackForce = Force(a); var defenceForce = Force(d);
        var attackersPresent = a.Where(u => u.Present && !u.Dead).ToList(); var defendersPresent = d.Where(u => u.Present && !u.Dead).ToList();
        bool falls = false, attackerWins = false, tie = false;
        if (attackersPresent.Count == 0) { }
        else if (defendersPresent.Count == 0) { attackerWins = attackForce > 0; falls = attackForce > strength; }
        else if (attackForce > defenceForce) { attackerWins = true; falls = attackForce > defenceForce + strength; foreach (var u in defendersPresent) u.Dead = true; }
        else if (attackForce < defenceForce) { foreach (var u in attackersPresent) u.Dead = true; }
        else { tie = true; foreach (var u in attackersPresent.Concat(defendersPresent)) u.Dead = true; }
        return new(true, falls, attackerWins, tie, attackForce, defenceForce, a.Where(u => u.Dead).Select(u => u.Unit.Name).ToArray(), d.Where(u => u.Dead).Select(u => u.Unit.Name).ToArray(), aPlayed.ToArray(), dPlayed.ToArray());
    }

    private static int Force(List<ExchangeUnit> side) => side.Sum(u => u.Contribution);

    private static List<Answer> Available(List<ExchangeUnit> units, LedgerSide side, IEnumerable<Answer> hand)
    {
        var list = new List<Answer>();
        foreach (var u in units) foreach (var e in u.Unit.UsableBattle.Where(e => e.Neutralises || e.Class is EffectClass.ForceAdd or EffectClass.Negate)) list.Add(new(e, u.Unit.Name, false, e.GoldCost));
        list.AddRange(side.TableAnswers); list.AddRange(hand);
        return list;
    }

    private static bool Decisive(bool defenderPlayed, List<ExchangeUnit> a, List<ExchangeUnit> d, int strength) => defenderPlayed ? Force(a) <= Force(d) + strength : Force(a) > Force(d) + strength;

    /// <summary>The (answer, target) that takes most Force off the other side, or adds most to mine, affordable and legal on its conditions; printed abilities before hand cards, cheaper first.</summary>
    private static (Answer, ExchangeUnit?)? Best(List<ExchangeUnit> mine, List<ExchangeUnit> theirs, List<Answer> answers, int gold, int strengthIfDefending)
    {
        (Answer, ExchangeUnit?)? best = null; var bestGain = 0.0;
        var performer = mine.Where(u => u.Present && !u.Bowed).OrderByDescending(u => u.Force).FirstOrDefault();
        foreach (var answer in answers.Where(x => x.Gold <= gold && x.Effect.Class != EffectClass.Negate))
        {
            if (answer.Effect.Class == EffectClass.ForceAdd)
            {
                var gain = (answer.Effect.Magnitude ?? 3) * 0.9 - (answer.FromHand ? 0.2 : 0);
                if (gain > bestGain && performer is not null) { bestGain = gain; best = (answer, null); }
                continue;
            }
            foreach (var target in theirs.Where(t => t.Present && !t.Dead && Legal(answer.Effect, t, performer)))
            {
                var gain = Removes(answer.Effect, target) - (answer.FromHand ? 0.2 : 0) - answer.Gold * 0.05;
                if (gain > bestGain) { bestGain = gain; best = (answer, target); }
            }
        }
        return bestGain > 0 ? best : null;
    }

    /// <summary>Whether the effect may touch this unit on its own conditions, judged on the public numbers.</summary>
    public static bool Legal(EffectDescriptor e, ExchangeUnit t, ExchangeUnit? performer)
    {
        foreach (var c in e.Conditions)
            switch (c.Kind)
            {
                case "force-at-most": if (t.Unit.Force > c.Value) return false; break;
                case "without-followers": if (t.Followers) return false; break;
                case "without-attachments": if (t.Unit.Attachments > 0) return false; break;
                case "force-less-than-performer": if (performer is null || t.Unit.Force >= performer.Unit.Force) return false; break;
                case "force-less-than-source-unit": if (performer is null || t.Unit.UnitForce >= performer.Unit.UnitForce) return false; break;
                case "chi-at-most-performer": if (performer is null || t.Unit.Chi > performer.Unit.Chi) return false; break;
                case "honor-at-most-performer": if (performer is null || t.Unit.PersonalHonor > performer.Unit.PersonalHonor) return false; break;
                case "gold-cost-at-most-performer": if (performer is null || t.Unit.GoldCost > performer.Unit.GoldCost) return false; break;
                case "bowed": if (!t.Bowed) return false; break;
                case "unbowed": if (t.Bowed) return false; break;
                case "dishonorable": if (!t.Unit.Dishonorable) return false; break;
                case "honorable": if (t.Unit.Dishonorable) return false; break;
                case "at-home": return false;
            }
        return true;
    }

    private static double Removes(EffectDescriptor e, ExchangeUnit t) => e.Class switch
    {
        EffectClass.SendHome or EffectClass.Destroy => t.Contribution + (e.Class == EffectClass.Destroy ? 0.5 : 0),
        EffectClass.Bow => t.Contribution + 0.3,
        EffectClass.Lock => t.Bowed ? 0 : 0.4, // keeps the Force, takes the abilities
        EffectClass.ForceCut => Math.Min(t.Contribution, e.Magnitude ?? 3),
        EffectClass.Duel => t.Contribution * 0.5, // won or lost on Chi; counted as half
        _ => 0
    };

    private static void Apply(EffectDescriptor e, List<ExchangeUnit> mine, List<ExchangeUnit> theirs, ExchangeUnit? target)
    {
        switch (e.Class)
        {
            case EffectClass.ForceAdd: var p = mine.Where(u => u.Present && !u.Bowed).OrderByDescending(u => u.Force).FirstOrDefault(); if (p is not null) p.Force += e.Magnitude ?? 3; break;
            case EffectClass.SendHome: if (target is not null) target.Present = false; break;
            case EffectClass.Destroy: if (target is not null) { target.Dead = true; target.Present = false; } break;
            case EffectClass.Bow: if (target is not null) target.Bowed = true; break;
            case EffectClass.Lock: if (target is not null) target.Locked = true; break;
            case EffectClass.ForceCut: if (target is not null) target.Force = Math.Max(0, target.Force - (e.Magnitude ?? 3)); break;
            case EffectClass.Duel:
                var challenger = mine.Where(u => u.Present && !u.Bowed).OrderByDescending(u => u.Unit.Chi).FirstOrDefault();
                if (target is not null && challenger is not null)
                {
                    var loser = challenger.Unit.Chi > target.Unit.Chi ? target : challenger.Unit.Chi < target.Unit.Chi ? challenger : null;
                    if (loser is not null) { if (e.Secondary.Contains(EffectClass.Destroy)) { loser.Dead = true; loser.Present = false; } else if (e.Secondary.Contains(EffectClass.Bow)) loser.Bowed = true; else if (e.Secondary.Contains(EffectClass.SendHome)) loser.Present = false; }
                }
                break;
        }
    }

    private static void Undo(EffectDescriptor e, List<ExchangeUnit> mine, List<ExchangeUnit> theirs, ExchangeUnit target)
    {
        switch (e.Class)
        {
            case EffectClass.SendHome: target.Present = true; break;
            case EffectClass.Destroy: target.Dead = false; target.Present = true; break;
            case EffectClass.Bow: target.Bowed = target.Unit.Bowed; break;
            case EffectClass.Lock: target.Locked = target.Unit.ActionLocked; break;
            case EffectClass.ForceCut: target.Force = Math.Min(target.Unit.Contribution, target.Force + (e.Magnitude ?? 3)); break;
        }
    }
}
