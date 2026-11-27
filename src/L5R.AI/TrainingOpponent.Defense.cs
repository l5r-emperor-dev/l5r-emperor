using L5R.Core;

namespace L5R.AI;

// Issue #946 (Mendon's defense guidance, 2026-10-14): the Infantry Maneuvers defense when the attacker assigned nobody. See DefensePlanner for the arithmetic.
public static partial class TrainingOpponent
{
    /// <summary>A unit is Cavalry only when the Personality and every attached Follower carry the keyword (core rules, Attack Phase).</summary>
    private static bool IsCavalryUnit(PlayerView player, CardView personality) =>
        personality.Definition?.Keywords?.Contains("Cavalry") == true
        && player.Home.Where(c => c.AttachedTo == personality.Id && c.Definition?.Type == CardType.Follower).All(c => c.Definition?.Keywords?.Contains("Cavalry") == true);

    /// <summary>The Force of everything the attacker could send in the Cavalry Maneuvers Segment: its unbowed Cavalry units with their attachments.</summary>
    private static int PossibleCavalryForce(PlayerView attacker) =>
        attacker.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && IsCavalryUnit(attacker, c))
            .Sum(c => ArmyForce(attacker.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id)));

    /// <summary>cavopen (issue #1336, replay 115409): the enemy Force that can still defend against what this declaration assigns. In the Cavalry Maneuvers Segment the infantry defense
    /// has closed, so only the defender's unbowed Cavalry at home can still be assigned; counting its whole home army made every Cavalry attack on an undefended board look hopeless.</summary>
    private static bool CavalryOpen(Ctx x) => Rule(x, "cavopen") && x.View.Phase == Phase.CavalryDeclaration;
    private static int OpenDefence(Ctx x) => CavalryOpen(x) ? PossibleCavalryForce(x.Opponent) : ArmyForce(x.Opponent.Home);
    /// <summary>cavopen: a Province's Strength plus, in the Cavalry Maneuvers Segment, the defenders already assigned to it.</summary>
    private static int HeldStrength(Ctx x, ProvinceView p) => p.Strength + (CavalryOpen(x) ? ArmyForce(x.Opponent.Army.Where(c => c.ProvinceIndex == p.Index)) : 0);

    // ---- issue #1360: the Cavalry waits for its own segment (Mendon, 2026-11-27) -------------------------------------------------------------------------------------------
    // A Cavalry unit may assign in either Maneuvers Segment, but waiting loses nothing: the attacker's Cavalry assigns after the defender's Infantry, the defender's Cavalry last, with
    // every other assignment visible. So Cavalry is assigned in the Infantry segment only when the Cavalry segment is barred (Incredible Tragedy in play).

    /// <summary>The Cavalry Maneuvers Segment is barred this turn (Incredible Tragedy in play: nobody assigns in it), so Cavalry assigns with the Infantry or not at all.</summary>
    private static bool CavalryBarred(Ctx x) => x.View.Players.Any(p => p.EventsInPlay?.Any(c => c.Definition?.Effect == "incredible-tragedy") == true);

    /// <summary>cavres: in the Infantry defence the bot's Cavalry is held for the Cavalry segment.</summary>
    private static bool ReserveCavalry(Ctx x) => Rule(x, "cavres") && x.View.Phase == Phase.DefenseAssignment && !CavalryBarred(x);

    /// <summary>cavhold: in the Infantry declaration the bot's Cavalry is held for the Cavalry segment.</summary>
    private static bool HoldsCavalry(Ctx x) => Rule(x, "cavhold") && x.View.Phase == Phase.InfantryDeclaration && !CavalryBarred(x);

    /// <summary>The offered units the Infantry defence may commit now: under cavres, the non-Cavalry ones.</summary>
    private static CardView[] InfantryDefenders(Ctx x, CardView[] units) => ReserveCavalry(x) ? units.Where(u => !IsCavalryUnit(x.Self, u)).ToArray() : units;

    /// <summary>cavres: what the Cavalry still to come can change at a battlefield the infantry defends: the attacker's possible Cavalry less the bot's held Cavalry, both taken to
    /// arrive there (the worst case for the infantry, who must then fight it; the bot's Cavalry replies last, seeing where theirs went). 0 outside the Infantry defence.</summary>
    private static int CavalrySwing(Ctx x) => ReserveCavalry(x) ? PossibleCavalryForce(x.Opponent) - PossibleCavalryForce(x.Self) : 0;

    /// <summary>The attack the infantry defence is judged against at a Province: the attackers there plus the Cavalry swing.</summary>
    private static int ThreatAt(Ctx x, int province) => AttackersAt(x, province) + CavalrySwing(x);

    /// <summary>cavres, in the Cavalry defence: the bot's defenders already at a battlefield, who fight beside the Cavalry it sends there.</summary>
    private static int DefendingAt(Ctx x, int province) => Rule(x, "cavres") && x.View.Phase == Phase.CavalryDefense ? ArmyForce(x.Self.Army.Where(c => c.ProvinceIndex == province)) : 0;

    /// <summary>Province order for a defense: face-up Personalities by Gold cost, most expensive first, then provinces whose card is face down, then the rest.</summary>
    private static int[] ProvincesByValue(PlayerView self, IEnumerable<int> offered)
    {
        int Cost(ProvinceView p) => (p.DynastySlots?.Select(s => s.Card) ?? [p.Card]).OfType<CardView>().Where(c => c.FaceUp && c.Definition?.Type == CardType.Personality).Select(c => c.Definition!.GoldCost).DefaultIfEmpty(-1).Max();
        bool FaceDown(ProvinceView p) => (p.DynastySlots?.Select(s => s.Card) ?? [p.Card]).OfType<CardView>().Any(c => !c.FaceUp || c.Definition is null);
        return self.Provinces.Where(p => !p.Destroyed && offered.Contains(p.Index))
            .OrderBy(p => Cost(p) >= 0 ? 0 : FaceDown(p) ? 1 : 2).ThenByDescending(Cost).ThenBy(p => p.Index).Select(p => p.Index).ToArray();
    }

    /// <summary>The assignment that commits the infantry to the provinces it can hold against the attacker's possible Cavalry, or null: nobody is attacking yet and there is no Cavalry at home, or the defense cannot reach parity.</summary>
    private static Dictionary<string, int>? CavalryDefensePlan(Ctx x, ActionOption action, CardView[]? offeredUnits)
    {
        if (!x.Memory.UseCavalryDefense || x.View.Phase != Phase.DefenseAssignment || x.Opponent.Army.Length > 0) return null;
        var threat = PossibleCavalryForce(x.Opponent);
        if (threat == 0) return null;
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var units = (offeredUnits ?? x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id))).ToArray())
            .Select(c =>
            {
                var cards = x.Self.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id).ToArray();
                return new DefensePlanner.Unit(c.Id, ArmyForce(cards), !IsCavalryUnit(x.Self, c), cards.Any(HasBattleAction));
            }).ToArray();
        var provinces = ProvincesByValue(x.Self, action.Provinces ?? x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index));
        // cavblind (block 8300 review: blind assignments before the Cavalry declares lost 92 of 234 battles for the loser, 12 of 77 for the winner): the attacker's hand removes the strongest
        // defenders it can reach, so those are planned as absent and stay home, and a group commits only where it holds without them.
        if (Rule(x, "cavblind"))
        {
            var riders = x.Opponent.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed && IsCavalryUnit(x.Opponent, c));
            var removed = units.OrderByDescending(u => u.Force).Take(HandRemovals(x, riders)).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
            units = units.Where(u => !removed.Contains(u.Id)).ToArray();
            if (units.Length == 0) return null;
        }
        return DefensePlanner.Plan(units, provinces, threat, x.Self.HandCount);
    }

    /// <summary>
    /// The most Provinces the attacker's unbowed Cavalry (alone or joining armies already assigned) can destroy, given each Province's Strength plus the Force defending it and the Force already
    /// attacking it. Issue #1211: the plain search tried every Cavalry subset at every Province with no memory, and the last stand asks it once per defence assignment, so one decision could run
    /// for hours (a Unicorn or Crab board with many Cavalry). Now each Province tries only its minimal Cavalry groups (a larger group only wastes Cavalry), results are remembered per
    /// (Province, Cavalry used) and per Province needs, and the work is capped: past <see cref="StepBudget"/> the answer assumes the worst, that the Cavalry takes the rest.
    /// </summary>
    private sealed class CavalryReach
    {
        internal const int StepBudget = 2_000_000;
        private readonly int _units;
        private readonly int[] _sum;
        private readonly Dictionary<int, int[]> _groups = [];
        private readonly Dictionary<string, int> _known = new(StringComparer.Ordinal);
        private int _steps;

        public CavalryReach(int[] cavalry)
        {
            _units = cavalry.Length; _sum = new int[1 << _units];
            for (var m = 1; m < _sum.Length; m++) { var low = m & -m; _sum[m] = _sum[m ^ low] + cavalry[System.Numerics.BitOperations.TrailingZeroCount(low)]; }
        }

        public bool OverBudget => _steps > StepBudget;

        /// <summary>need[p]: Force still missing at Province p beyond what is already attacking it (below 0: it falls without any Cavalry). Each Cavalry unit joins at most one Province.</summary>
        public int MaxDestroyed(int[] need)
        {
            _steps += need.Length;
            var key = string.Join(",", need);
            if (_known.TryGetValue(key, out var known)) return known;
            var memo = new Dictionary<int, int>();
            int Best(int province, int used)
            {
                if (province == need.Length) return 0;
                if (++_steps > StepBudget) return need.Length - province;
                var k = province << _units | used;
                if (memo.TryGetValue(k, out var value)) return value;
                if (need[province] < 0) value = 1 + Best(province + 1, used);
                else
                {
                    value = Best(province + 1, used);
                    foreach (var group in Groups(need[province]))
                    {
                        if ((group & used) != 0) continue;
                        value = Math.Max(value, 1 + Best(province + 1, used | group));
                        if (value == need.Length - province) break;
                    }
                }
                return memo[k] = value;
            }
            return _known[key] = Best(0, 0);
        }

        private int[] Groups(int need)
        {
            if (_groups.TryGetValue(need, out var groups)) return groups;
            _steps += _sum.Length;
            return _groups[need] = Enumerable.Range(1, _sum.Length - 1).Where(m => _sum[m] > need && Minimal(m, need)).ToArray();
        }

        private bool Minimal(int group, int need)
        {
            for (var rest = group; rest != 0; rest &= rest - 1) if (_sum[group ^ (rest & -rest)] > need) return false;
            return true;
        }
    }

    /// <summary>
    /// Mendon's rule (playtest 2026-10-17, generalised 2026-10-18): when the attacker's Cavalry at home, on top of any armies it has already assigned, could form armies that destroy EVERY Province the bot has left,
    /// the bot defends in the Infantry Maneuvers Segment with every unit it is offered, even when the parity arithmetic of CavalryDefensePlan says it cannot win. It sends them where the Cavalry can then no longer
    /// destroy every Province; if no assignment manages that, to the Province with the greatest Strength. Null when the rule does not apply.
    /// </summary>
    private static Dictionary<string, int>? CavalryLastStandPlan(Ctx x, ActionOption action, CardView[]? offeredUnits)
    {
        if (!x.Memory.UseCavalryDefense || !x.Memory.UseCavalryLastStand || x.View.Phase != Phase.DefenseAssignment) return null;
        var remaining = x.Self.Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Index).ToArray();
        if (remaining.Length == 0) return null;
        var cavalry = x.Opponent.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && IsCavalryUnit(x.Opponent, c))
            .Select(c => ArmyForce(x.Opponent.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id))).Where(f => f > 0).ToArray();
        if (cavalry.Length == 0 || cavalry.Length > 12) return null;
        int Need(ProvinceView p, int defenders) => p.Strength + defenders - AttackersAt(x, p.Index);
        var reach = new CavalryReach(cavalry);
        if (reach.MaxDestroyed(remaining.Select(p => Need(p, 0)).ToArray()) < remaining.Length) return null; // the Cavalry cannot take every Province: nothing to be done here
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var all = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id))).ToArray();
        var offered = remaining.Where(p => action.Provinces is null || action.Provinces.Contains(p.Index)).ToArray();
        if (all.Length == 0 || offered.Length == 0) return null;
        // cavres (issue #1360): the infantry alone first; the Cavalry is sent now only when no infantry assignment keeps a Province
        if (ReserveCavalry(x) && InfantryDefenders(x, all) is { Length: > 0 } infantry && infantry.Length < all.Length)
        {
            if (Stand(infantry) is { } held) return held;
            if (Stand(all) is { } everyone) return everyone;
            return infantry.ToDictionary(c => c.Id, _ => Strongest(), StringComparer.Ordinal); // cannot be stopped: the infantry holds the strongest Province, the Cavalry still replies
        }
        return Stand(offeredUnits ?? all) ?? (offeredUnits ?? all).ToDictionary(c => c.Id, _ => Strongest(), StringComparer.Ordinal); // cannot be stopped: hold the strongest Province
        int Strongest() => offered.OrderByDescending(p => p.Strength - AttackersAt(x, p.Index)).ThenBy(p => p.Index).First().Index;
        Dictionary<string, int>? Stand(CardView[] units)
        {
        if (units.Length == 0) return null;
        var forces = units.Select(u => ArmyForce(x.Self.Home.Where(h => h.Id == u.Id || h.AttachedTo == u.Id))).ToArray();
        if (units.Length <= 8)
        {
            var choice = new int[units.Length]; int[]? best = null; var bestDestroyed = remaining.Length; var bestSlack = int.MinValue;
            for (var total = (int)Math.Pow(offered.Length, units.Length); total-- > 0;)
            {
                var c = total; var defenders = new int[remaining.Length];
                for (var i = 0; i < units.Length; i++) { choice[i] = c % offered.Length; c /= offered.Length; defenders[Array.FindIndex(remaining, p => p.Index == offered[choice[i]].Index)] += forces[i]; }
                if (reach.OverBudget) break; // issue #1211: keep the best assignment found so far rather than stall the decision
                if ((total & 1023) == 0) CheckDeadline(x.Memory);
                var destroyed = reach.MaxDestroyed(remaining.Select((p, k) => Need(p, defenders[k])).ToArray());
                if (destroyed >= remaining.Length) continue;
                var slack = remaining.Select((p, k) => p.Strength + defenders[k]).Sum();
                if (destroyed < bestDestroyed || destroyed == bestDestroyed && slack > bestSlack) { best = (int[])choice.Clone(); bestDestroyed = destroyed; bestSlack = slack; }
            }
            if (best is not null) return units.Select((u, i) => (u.Id, Province: offered[best[i]].Index)).ToDictionary(t => t.Id, t => t.Province, StringComparer.Ordinal);
        }
        return null;
        }
    }

    private static bool HasBattleAction(CardView card) => !card.Bowed && !card.ActionLocked && BattlePolicy.Printed(card).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen");

    // ---- Attacking: Cavalry waits for its own segment (issue #946) --------------------------------------------------------------------------------------------------------

    /// <summary>The Personalities the bot already sent in this Attack Phase's Infantry Maneuvers Segment; the Cavalry joins them.</summary>
    private static CardView[] AlreadyAssigned(Ctx x) =>
        x.Memory.UseCavalryAttack && x.View.Phase == Phase.CavalryDeclaration ? x.Self.Army.Where(c => c.Definition?.Type == CardType.Personality && c.ProvinceIndex is not null).ToArray() : [];

    /// <summary>The part of a whole-army plan that this segment may assign: the units it offers.</summary>
    private static Dictionary<string, int> OfferedOnly(Dictionary<string, int> plan, CardView[] offered) =>
        plan.Where(p => offered.Any(c => c.Id == p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>In the Infantry Maneuvers Segment the bot assigns only units that are not Cavalry; the Cavalry is assigned in the Cavalry Maneuvers Segment, when the defense is visible.</summary>
    private static Dictionary<string, int> HoldCavalry(Ctx x, Dictionary<string, int> assignments) =>
        !x.Memory.UseCavalryAttack || x.View.Phase != Phase.InfantryDeclaration || Rule(x, "cavhold") && CavalryBarred(x) ? assignments : WithoutCavalry(x.Self, assignments);

    /// <summary>The assignment without the player's Cavalry units.</summary>
    internal static Dictionary<string, int> WithoutCavalry(PlayerView self, Dictionary<string, int> assignments) =>
        assignments.Where(p => self.Home.FirstOrDefault(c => c.Id == p.Key) is not { } unit || !IsCavalryUnit(self, unit)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>cavhold (issue #1360): a plan's Infantry declaration, its Cavalry held for the Cavalry segment; unchanged without the rule.</summary>
    private static Dictionary<string, int> InfantryPart(Ctx x, Dictionary<string, int> assignments) => HoldsCavalry(x) ? HoldCavalry(x, assignments) : assignments;

    /// <summary>The attack as the scores read it: the whole army, not only this segment's part. In the Infantry segment, the plan the infantry assignment is the part of; in the Cavalry segment, the infantry already sent plus these Cavalry.</summary>
    private static Dictionary<string, int> WholeAttack(Ctx x, ActionOption action, Dictionary<string, int>? assignments)
    {
        assignments ??= [];
        if (!x.Memory.UseCavalryAttack) return assignments;
        if (x.View.Phase == Phase.CavalryDeclaration)
        {
            var merged = new Dictionary<string, int>(assignments, StringComparer.Ordinal);
            foreach (var unit in AlreadyAssigned(x)) if (unit.ProvinceIndex is int province) merged.TryAdd(unit.Id, province);
            return merged;
        }
        if (x.View.Phase != Phase.InfantryDeclaration) return assignments;
        var (units, provinces) = AttackInputs(x, action);
        return PlanAttack(x, units, provinces) is { } whole && HoldCavalry(x, whole) is var part && part.Count == assignments.Count && part.All(p => assignments.TryGetValue(p.Key, out var sent) && sent == p.Value) ? whole : assignments;
    }

    // ---- Defending: where the defense changes the result (issue #963, bot loop round 2) ----------------------------------------------------------------------------------------
    // Rules (core rules, battle resolution, as the engine applies them): the army with more Force wins and the loser's whole army is destroyed; a Province falls only when the attackers' Force
    // exceeds the defenders' Force plus the Province's Strength. So a defense is worth sending only where it wins (the attackers are destroyed and nothing is lost) or where it is the difference
    // between a Province standing and falling. Everywhere else the defenders are risked or exposed for nothing. Only the public view is read; nothing names a card or a deck.

    private static int AttackersAt(Ctx x, int province) => ArmyForce(x.Opponent.Army.Where(c => c.ProvinceIndex == province));

    private static int DefendersForce(Ctx x, IEnumerable<string> unitIds)
    {
        var ids = unitIds.ToHashSet(StringComparer.Ordinal);
        return ArmyForce(x.Self.Home.Where(c => ids.Contains(c.Id) || c.AttachedTo is not null && ids.Contains(c.AttachedTo)));
    }

    /// <summary>The battlefield to send every offered defender to: where they win outright (attackers destroyed), else where they save a Province that would fall, else null (the old rule decides).</summary>
    private static int? InformedDefenseProvince(Ctx x, CardView[] units, int[] offered)
    {
        var defenders = DefendersForce(x, units.Select(u => u.Id));
        var contested = x.Self.Provinces.Where(p => !p.Destroyed && offered.Contains(p.Index) && AttackersAt(x, p.Index) > 0).ToArray();
        if (contested.Length == 0 || defenders == 0) return null;
        // cavres (issue #1360): against the attackers plus the Cavalry swing (ThreatAt), with the defenders already there in the Cavalry defence (DefendingAt)
        var wins = contested.Where(p => defenders + DefendingAt(x, p.Index) > ThreatAt(x, p.Index)).ToArray();
        // Winning kills the biggest army available, and a Province that would otherwise fall counts for more than one that would stand.
        if (wins.Length > 0) return wins.OrderByDescending(p => ThreatAt(x, p.Index) > p.Strength ? 1 : 0).ThenByDescending(p => AttackersAt(x, p.Index)).ThenBy(p => p.Index).First().Index;
        var saves = contested.Where(p => ThreatAt(x, p.Index) > p.Strength + DefendingAt(x, p.Index) && ThreatAt(x, p.Index) <= p.Strength + DefendingAt(x, p.Index) + defenders).ToArray();
        return saves.Length > 0 ? saves.OrderByDescending(p => p.Strength).ThenBy(p => p.Index).First().Index : null;
    }

    /// <summary>
    /// Hypothesis A: when attackers sit at several Provinces, the assignment of defenders to battlefields (or home) with the best outcome: an attacking army destroyed counts, a Province saved counts more,
    /// and defenders sent to a battle they lose or that did not need them cost something. Null unless the best assignment uses two or more battlefields.
    /// </summary>
    private static Dictionary<string, int>? SplitDefense(Ctx x, CardView[] units, int[] offered)
    {
        var contested = x.Self.Provinces.Where(p => !p.Destroyed && offered.Contains(p.Index) && AttackersAt(x, p.Index) > 0).ToArray();
        if (contested.Length < 2 || units.Length < 2 || units.Length > 7) return null;
        var forces = units.Select(u => ArmyForce(x.Self.Home.Where(h => h.Id == u.Id || h.AttachedTo == u.Id))).ToArray();
        var options = contested.Length + 1; // each unit goes to one contested battlefield or stays home
        var total = (int)Math.Pow(options, units.Length);
        double bestValue = double.MinValue; int[]? best = null;
        var choice = new int[units.Length];
        for (var code = 0; code < total; code++)
        {
            var c = code; for (var i = 0; i < units.Length; i++) { choice[i] = c % options; c /= options; }
            double value = 0; var used = 0;
            for (var p = 0; p < contested.Length; p++)
            {
                int defenders = 0, count = 0;
                for (var i = 0; i < units.Length; i++) if (choice[i] == p + 1) { defenders += forces[i]; count++; }
                if (count == 0) continue;
                used++;
                var attackers = ThreatAt(x, contested[p].Index); var strength = contested[p].Strength + DefendingAt(x, contested[p].Index); defenders += DefendingAt(x, contested[p].Index); // cavres (issue #1360)
                if (defenders > attackers) value += attackers > strength ? 3 : 1;
                else if (attackers > strength && attackers <= strength + defenders) value += 2 - .25 * count;
                else value -= .5 * count;
            }
            value -= .01 * choice.Count(v => v != 0);
            if (value > bestValue) { bestValue = value; best = (int[])choice.Clone(); }
        }
        if (best is null || bestValue <= 0 || best.Where(v => v != 0).Distinct().Count() < 2) return null;
        var plan = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < units.Length; i++) if (best[i] != 0) plan[units[i].Id] = contested[best[i] - 1].Index;
        return plan;
    }

    /// <summary>The score of a split defense: 95 when any group wins its battle, 70 when it only saves Provinces, 0 otherwise.</summary>
    private static double SplitDefenseScore(Ctx x, GameCommand command)
    {
        double best = 0;
        foreach (var group in command.Assignments!.GroupBy(p => p.Value))
        {
            var target = x.Self.Provinces.FirstOrDefault(p => p.Index == group.Key); if (target is null) continue;
            if (AttackersAt(x, group.Key) == 0) continue;
            var attackers = ThreatAt(x, group.Key); var defenders = DefendersForce(x, group.Select(p => p.Key)) + DefendingAt(x, group.Key); var strength = target.Strength; // cavres (issue #1360)
            if (defenders > attackers) best = 95;
            else if (attackers > strength + DefendingAt(x, group.Key) && attackers <= strength + defenders) best = Math.Max(best, 70);
        }
        return best;
    }

    /// <summary>The score of a defense assignment under the informed rule, or null to leave it to the old scores (nothing assigned, or the Cavalry and last-stand rules apply).</summary>
    private static double? InformedDefenseScore(Ctx x, GameCommand command)
    {
        if (command.Assignments is not { Count: > 0 } || x.Opponent.Army.Length == 0) return null;
        if (LastStandProvince(x, x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray()) is not null) return null; // the last Provinces will fall: the old rule defends and fights
        if (x.Memory.UseSplitDefense && command.Assignments.Values.Distinct().Count() > 1) return SplitDefenseScore(x, command);
        var province = command.Assignments.Values.First();
        var target = x.Self.Provinces.FirstOrDefault(p => p.Index == province);
        if (target is null) return null;
        if (AttackersAt(x, province) == 0) return 0; // nobody is there: sending the defense is pointless
        var attackers = ThreatAt(x, province); // cavres (issue #1360): with the Cavalry swing, and the defenders already there in the Cavalry defence
        var defenders = DefendersForce(x, command.Assignments.Keys) + DefendingAt(x, province);
        if (defenders > attackers) return 95; // wins: the attackers are destroyed
        if (attackers > target.Strength + DefendingAt(x, province) && attackers <= target.Strength + defenders) return 70; // loses the battle but the Province stands
        return 0; // would lose the defenders without saving the Province, or the Province stands anyway: keep them home
    }
}
