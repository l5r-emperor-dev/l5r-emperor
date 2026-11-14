namespace L5R.AI;

// Issue #1206, strategy brief step 3, family E (docs/bots/strategy/RULE_FAMILIES.md): inside a battle. The battle is read as it stands from the Read's exchange: the viewer plays nothing more,
// the opponent plays its likely hand (the belief) and its table. If that already gives the result the viewer wants, pass (or take a free kill: a unit sent home survives, PM 3.5 "kill, do not
// repel"). Otherwise the cheapest single action after which the battle as it stands gives that result: printed abilities before the table before hand cards before the Favor; destroy before bow
// before a Force cut before a lock or duel, Force adds and send-homes last; the target carrying unspent abilities first. When no single action suffices but the viewer's whole hand does, the
// first step of that sequence in the same order. When nothing reaches the result, nothing is spent unless a free action still improves the tier (a lost battle), except in a deciding battle
// (a last Province on either side), which is read against the open hand and fought on. Late Force (a Force add) waits while the opponent has an unbowed unit at the battle and has not passed:
// the viewer is given priority again after their pass, so waiting costs nothing and the minimum can be added last (PM 3.5 "late Force").

/// <summary>One action the viewer may take now, as the Fight sees it: the answer it plays and the enemy unit it targets (null for a Force add or an untargeted effect). Key is the caller's handle.</summary>
public sealed record FightOption(string Key, Answer Answer, string? TargetId);

/// <summary>What the Fight would do now: pass, or the option with this key. Standing and Reach describe the projections it decided on; Log holds every option's projection, for the shadow.</summary>
public sealed record FightPlan(string? Pick, string Why, string Standing, string Reach, string[] Log)
{
    public bool Pass => Pick is null;
}

public static class Fight
{
    /// <summary>The outcome ladder for the viewer. Attacking: 0 the army loses, 1 tie (both armies die), 2 wins without the Province, 3 the Province falls.
    /// Defending: 0 the Province falls, 1 holds but my defenders die, 2 holds and nobody of mine dies, 3 holds and an attacker dies.</summary>
    public static int Tier(ExchangeResult r, bool viewerAttacks, int preDestroyedEnemies = 0)
    {
        if (viewerAttacks) return r.Falls ? 3 : r.AttackerWins ? 2 : r.Tie ? 1 : 0;
        if (r.Falls) return 0;
        if (r.AttackerWins || r.Tie) return 1;
        return r.AttackersDestroyed.Length + preDestroyedEnemies > 0 ? 3 : 2;
    }

    /// <summary>The tier the viewer plays for: the Province (attacking), or the Province and the defenders (defending).</summary>
    public static int WantTier(bool viewerAttacks) => viewerAttacks ? 3 : 2;

    private static readonly EffectClass[] ClassOrder = [EffectClass.Destroy, EffectClass.Bow, EffectClass.ForceCut, EffectClass.Lock, EffectClass.Duel, EffectClass.ForceAdd, EffectClass.SendHome];
    private static int ClassRank(EffectClass c) { var i = Array.IndexOf(ClassOrder, c); return i < 0 ? ClassOrder.Length : i; }

    /// <summary>The position after one option is played: the viewer's units, the enemy's, the viewer's hand answers and Gold, and the enemies the option itself destroyed.</summary>
    private sealed record After(LedgerUnit[] Mine, LedgerUnit[] Theirs, Answer[] MyHand, int Gold, int Destroyed, bool Legal);

    public static FightPlan Of(Read read, int province, bool viewerAttacks, bool theyPassed, bool deciding, IReadOnlyList<FightOption> options) => Of(read, province, viewerAttacks, theyPassed, deciding, options, false);

    /// <summary>With <paramref name="openForSpending"/> (rule reOpen, formulation 2): whether the battle is won as it stands is still read against their likely hand, but what an action
    /// reaches, and whether anything can, is read against their open hand (the answers they have shown), so unseen cards never make the bot give a battle up.</summary>
    public static FightPlan Of(Read read, int province, bool viewerAttacks, bool theyPassed, bool deciding, IReadOnlyList<FightOption> options, bool openForSpending)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var mine = me.At(province).ToArray(); var theirs = them.At(province).ToArray();
        var strength = (viewerAttacks ? them : me).Provinces.FirstOrDefault(p => p.Index == province).Strength;
        var likelyHand = deciding ? [] : read.Belief.Answers(false).ToArray(); var openHand = Array.Empty<Answer>();
        var theirHand = likelyHand;
        var want = WantTier(viewerAttacks);
        var log = new List<string>();
        // where an answer comes from, in the order the player prefers: 0 a printed ability of a unit here, 1 the table (Stronghold, Holding), 2 a card from hand, 3 the Favor
        int Rank(FightOption o) => o.Answer.Source == "Favor" ? 3 : o.Answer.FromHand ? 2 : mine.Any(u => u.Name == o.Answer.Source) ? 0 : 1;

        ExchangeResult Project(LedgerUnit[] my, LedgerUnit[] their, Answer[] myHand, int gold, bool iAct)
        {
            var side = me with { GoldNow = gold, HandAnswers = myHand };
            return viewerAttacks
                ? Exchange.Run(side, my, them, their, strength, myHand, theirHand, iAct, true)
                : Exchange.Run(them, their, side, my, strength, theirHand, myHand, true, iAct);
        }
        var standing = Project(mine, theirs, [], me.GoldNow, false); var standingTier = Tier(standing, viewerAttacks);
        if (openForSpending) { theirHand = openHand; var open = Project(mine, theirs, [], me.GoldNow, false); if (Tier(open, viewerAttacks) < standingTier) { standing = open; standingTier = Tier(open, viewerAttacks); } } // the spend decisions read the open hand; a standing that is worse against the open hand (none) cannot be
        var full = Project(mine, theirs, me.HandAnswers, me.GoldNow, true); var fullTier = Tier(full, viewerAttacks);
        var standingText = $"standing {standing} tier {standingTier}"; var reachText = $"with everything {full} tier {fullTier}";
        // the options, each projected: the battle as it stands after it (I play nothing more), and with everything I have left after it
        var projected = options.Select(o =>
        {
            var after = Apply(o, mine, theirs, me.HandAnswers, me.GoldNow, viewerAttacks);
            if (!after.Legal) { log.Add($"{o.Key}: not read (no such enemy unit here, or no performer)"); return (Option: o, After: after, Standing: -1, Full: -1); }
            var s = Tier(Project(after.Mine, after.Theirs, [], after.Gold, false), viewerAttacks, after.Destroyed);
            var f = Tier(Project(after.Mine, after.Theirs, after.MyHand, after.Gold, true), viewerAttacks, after.Destroyed);
            log.Add($"{o.Key} ({o.Answer.Effect.Class}{(o.Answer.Effect.Magnitude is { } m ? " " + m : "")} from {Source(o, Rank(o))}{(o.TargetId is { } t ? " > " + (theirs.FirstOrDefault(u => u.Id == t)?.Name ?? t) : "")}): standing tier {s}, with everything {f}");
            return (Option: o, After: after, Standing: s, Full: f);
        }).Where(p => p.Standing >= 0).ToArray();
        var reach = Math.Max(fullTier, projected.Select(p => p.Full).DefaultIfEmpty(-1).Max()); // what everything I have reaches, the options included
        bool Free(FightOption o) => Rank(o) <= 1 && o.Answer.Gold == 0 && o.Answer.Effect.HonorCost == 0 && !o.Answer.Effect.DiscardsCard; // a printed or table ability that costs nothing
        bool Kills(FightOption o) => o.Answer.Effect.Class is EffectClass.Destroy or EffectClass.Bow && o.TargetId is not null;
        // late Force: a Force add waits while they still have an unbowed unit here and have not passed (I am given priority again after their pass)
        bool Waits(FightOption o) => o.Answer.Effect.Class == EffectClass.ForceAdd && !theyPassed && theirs.Any(u => u.Province == province && !u.Bowed && !u.ActionLocked);
        IOrderedEnumerable<(FightOption Option, After After, int Standing, int Full)> Order(IEnumerable<(FightOption Option, After After, int Standing, int Full)> set) => set
            .OrderBy(p => Rank(p.Option)).ThenBy(p => ClassRank(p.Option.Answer.Effect.Class))
            .ThenByDescending(p => p.Option.TargetId is { } t && theirs.FirstOrDefault(u => u.Id == t) is { } u && u.UsableBattle.Any() ? 1 : 0) // the unit carrying their unspent abilities first
            .ThenByDescending(p => p.Standing).ThenBy(p => p.Option.Answer.Effect.Class == EffectClass.ForceAdd ? p.Option.Answer.Effect.Magnitude ?? 3 : 0) // the minimum Force that crosses
            .ThenBy(p => p.Option.Answer.Gold).ThenBy(p => p.Option.Key, StringComparer.Ordinal);

        if (standingTier >= want)
        {
            // kill, do not repel: a free printed destroy or bow that keeps the result is taken; nothing else is spent, and a send-home never (the unit would survive)
            var kill = Order(projected.Where(p => Free(p.Option) && Kills(p.Option) && p.Standing >= want && !Waits(p.Option))).FirstOrDefault();
            return kill.Option is { } k && (k.Answer.Effect.Class == EffectClass.Destroy || kill.Standing > standingTier)
                ? new(k.Key, $"won as it stands (tier {standingTier}); a free {k.Answer.Effect.Class} keeps it", standingText, reachText, log.ToArray())
                : new(null, $"won as it stands (tier {standingTier}): pass, cards unused", standingText, reachText, log.ToArray());
        }
        // the goal: the want; when it is out of reach and the standing tier loses the army (attacking: it dies or ties) or the Province (defending), or the battle decides the game,
        // the best tier everything reaches (a sequence that saves the army is started, as one that takes the Province would be)
        var losing = viewerAttacks ? standingTier <= 1 : standingTier == 0;
        var goal = reach >= want ? want : (losing || deciding) && reach > standingTier ? reach : want;
        // one action that restores the result, cheapest first
        var sufficient = Order(projected.Where(p => p.Standing >= goal && !Waits(p.Option))).FirstOrDefault();
        if (sufficient.Option is { } one) return new(one.Key, $"restores tier {goal} alone (standing {standingTier})", standingText, reachText, log.ToArray());
        if (reach >= goal)
        {
            // several actions are needed: the first of them, in the player's order (disable the unit that carries their abilities first; the send-home and the late Force last)
            var step = Order(projected.Where(p => p.Full >= goal && !Waits(p.Option) && p.Option.Answer.Effect.Class != EffectClass.SendHome)).FirstOrDefault();
            if (step.Option is null) step = Order(projected.Where(p => p.Full >= goal && !Waits(p.Option))).FirstOrDefault();
            if (step.Option is { } first) return new(first.Key, $"first of several: everything reaches tier {goal}, standing {standingTier}", standingText, reachText, log.ToArray());
            if (projected.Any(p => p.Full >= goal && Waits(p.Option))) return new(null, $"late Force waits: they have an unbowed unit here and have not passed (standing {standingTier})", standingText, reachText, log.ToArray());
        }
        // short of the goal: the highest tier one action still reaches. A card is spent for it when losing or deciding; otherwise only a free action lifts the tier
        // (the defenders' lives behind a held Province are not bought with a card they may need).
        var lift = Order(projected.Where(p => p.Standing > standingTier && (Free(p.Option) || deciding || losing) && !Waits(p.Option))).OrderByDescending(p => p.Standing).FirstOrDefault();
        if (lift.Option is { } l) return new(l.Key, $"{(deciding ? "deciding battle" : losing ? "losing" : "won short")}: {l.Answer.Effect.Class} lifts the tier from {standingTier} to {lift.Standing}", standingText, reachText, log.ToArray());
        if (deciding && Order(projected.Where(p => p.Full > standingTier)).FirstOrDefault().Option is { } fight) return new(fight.Key, $"deciding battle: {fight.Answer.Effect.Class} raises what everything reaches", standingText, reachText, log.ToArray());
        return new(null, $"cannot reach tier {want} (everything gives {reach}): spend nothing", standingText, reachText, log.ToArray());
    }

    private static Answer[] Without(Answer[] hand, Answer played)
    {
        var i = Array.FindIndex(hand, a => a.Effect.Id == played.Effect.Id && a.Source == played.Source);
        if (i < 0) i = Array.FindIndex(hand, a => a.Effect.Id == played.Effect.Id);
        return i < 0 ? hand : hand.Where((_, j) => j != i).ToArray();
    }

    private static string Source(FightOption o, int rank) => rank switch { 0 => "a printed ability", 1 => "the table", 2 => "hand", _ => "the Favor" };

    /// <summary>The position after the option: its effect applied to the target on the public numbers (the exchange's own reading), the performer bowed when the answer bows it, the answer spent.</summary>
    private static After Apply(FightOption o, LedgerUnit[] mine, LedgerUnit[] theirs, Answer[] myHand, int gold, bool viewerAttacks)
    {
        var e = o.Answer.Effect; var my = mine.ToList(); var their = theirs.ToList(); var destroyed = 0;
        var performer = my.Where(u => !u.Bowed).OrderByDescending(u => u.UnitForce).FirstOrDefault();
        var hand = o.Answer.FromHand ? Without(myHand, o.Answer) : myHand;
        // the option is legal as offered: the engine lists it with this target and performer, and its reading of the printed conditions outranks the descriptor's (which serves the projection of the other answers)
        if (o.TargetId is { } id)
        {
            var i = their.FindIndex(u => u.Id == id);
            if (i < 0) return new(mine, theirs, hand, gold, 0, false);
            var t = their[i];
            switch (e.Class)
            {
                case EffectClass.Destroy: their.RemoveAt(i); destroyed = 1; break;
                case EffectClass.SendHome: their.RemoveAt(i); break;
                case EffectClass.Bow: their[i] = t with { Bowed = true }; break;
                case EffectClass.Lock: their[i] = t with { ActionLocked = true }; break;
                case EffectClass.ForceCut: var cut = e.Magnitude ?? 3; their[i] = t with { UnitForce = Math.Max(0, t.UnitForce - cut), Force = Math.Max(0, t.Force - cut) }; break;
                case EffectClass.Duel:
                    var challenger = my.Where(u => !u.Bowed).OrderByDescending(u => u.Chi).FirstOrDefault();
                    if (challenger is null) return new(mine, theirs, hand, gold, 0, false);
                    if (challenger.Chi > t.Chi) { if (e.Has(EffectClass.Destroy)) { their.RemoveAt(i); destroyed = 1; } else if (e.Has(EffectClass.Bow)) their[i] = t with { Bowed = true }; else their.RemoveAt(i); }
                    else if (challenger.Chi < t.Chi) { var c = my.IndexOf(challenger); if (e.Has(EffectClass.Destroy)) my.RemoveAt(c); else if (e.Has(EffectClass.Bow)) my[c] = challenger with { Bowed = true }; else my.RemoveAt(c); }
                    break;
                default: break;
            }
        }
        else if (e.Class == EffectClass.ForceAdd)
        {
            if (performer is null) return new(mine, theirs, hand, gold, 0, false);
            var p = my.IndexOf(performer); var add = e.Magnitude ?? 3; my[p] = performer with { UnitForce = performer.UnitForce + add, Force = performer.Force + add };
        }
        else return new(mine, theirs, hand, gold, 0, false);
        // the performer: bowed when the answer bows it (its other abilities go with it); its ability spent either way
        if (!o.Answer.FromHand && o.Answer.Source != "Favor" && my.FindIndex(u => u.Name == o.Answer.Source) is var s && s >= 0)
        {
            var u = my[s];
            my[s] = u with { Spent = [.. u.Spent, e.Id], Bowed = u.Bowed || e.BowsSource };
        }
        return new(my.ToArray(), their.ToArray(), hand, gold - o.Answer.Gold, destroyed, true);
    }
}
