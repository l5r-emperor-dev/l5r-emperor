using L5R.Core;

namespace L5R.AI;

// Issue #1185 tuning: further Contender rules, each named in BotContext.Rules and switched on separately so a tournament can measure it (the c2 arm reads them from L5R_C2).
// Every rule reads only the seat's own view and printed card data.
//   econ      Holdings that are fair value (cost at most one Gold above production) are bought first in the early turns, and a province card the bot can pay for with its
//             production next turn is not discarded (Mendon: the bot over-discards; a kept card plans next turn).
//   border    the once-per-game Border Keep refresh is kept for a turn when nothing in the row can be paid for.
//   presence  defending, when an attack would raze a Province and the bot's removals (send home, bow, destroy, -Force) would stop it, the cheapest unit that is needed is sent there:
//             without a unit at the battlefield the bot may take no Battle action at all (the Rule of Presence).
public static partial class TrainingOpponent
{
    private static bool Rule(Ctx x, string name) => x.Memory.Has(name);

    private static bool FairHolding(CardDefinition def) => def.Type == CardType.Holding && def.GoldCost <= def.GoldProduction + 1;

    private static int Holdings(PlayerView p) => p.Home.Count(c => c.Definition?.Type == CardType.Holding);

    /// <summary>The Contender's further rules over an action's final score.</summary>
    private static double Contender2Score(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (x.Memory.Rules.Count == 0) return score;
        // firstbody: short of bodies (none, or fewer than the opponent) from its second turn on, the best affordable Personality is bought before any Holding: provinces with nobody to
        // defend them are what the opponent's first attack takes.
        if (Rule(x, "firstbody") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { Type: CardType.Personality } body && x.Self.TurnsTaken >= 2
            && Personalities(x.Self) < Math.Max(1, Personalities(x.Opponent)) && !x.Memory.RecruitedThisTurn(x.View.TurnNumber))
            return SlateBonus + 100 + body.Force * 3 + score * 0.0001;
        if (Rule(x, "econ") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { } bought && FairHolding(bought) && (Holdings(x.Self) < 8 && x.Self.TurnsTaken <= (Rule(x, "econT2") ? 2 : 4) || Rule(x, "econ2") && Holdings(x.Self) < 10)
            && !(Rule(x, "body1") && Personalities(x.Self) == 0 && x.Self.TurnsTaken >= 2 && ProvinceCards(x).Any(c => c.Definition!.Type == CardType.Personality && EffectiveCost(x, c) <= Ready(x))))
            return SlateBonus + 50 + 5 * (bought.GoldProduction - bought.GoldCost) + score * 0.0001;
        if (Rule(x, "econ") && action.Type == "discardProvince" && x.Card(command.CardId)?.Definition is { } kept)
        {
            if (FairHolding(kept) && Holdings(x.Self) < 8) return -5;
            if (kept.Type == CardType.Personality && EffectiveCost(x, x.Card(command.CardId)!) <= Capacity(x)) return -5;
        }
        if (Rule(x, "border") && action.Type == "useAbility" && command.AbilityId == "border-game"
            && ProvinceCards(x).Any(c => EffectiveCost(x, c) <= Ready(x) && (c.Definition!.Type == CardType.Personality || FairHolding(c.Definition))))
            return Math.Min(score, BarredScore);
        // honorclock: "gain Honor or make the opponent lose it" goes to whichever matters more: the side nearer an Honor victory.
        if (Rule(x, "honorclock") && action.Type == "chooseEffect" && command.Choice is "self-gain" or "opponent-lose" && 40 - x.Opponent.Honor < 40 - x.Self.Honor)
            return command.Choice == "opponent-lose" ? 140 : 70;
        // holdcap: with production past what the deck can spend, a Holding buys nothing more; it is a province slot that could hold a Personality.
        if (Rule(x, "holdcap") && Capacity(x) >= 20 && x.Card(command.CardId)?.Definition is { Type: CardType.Holding })
        {
            if (action.Type == "purchase") return -10;
            if (action.Type == "discardProvince") return 20;
        }
        // shinden: 1 Honor is cheap next to a bowed Personality (Mendon: the bot bowed its main unit and lost the next battle); only an opponent near an Honor victory changes that.
        if (Rule(x, "shinden") && action.Type == "chooseShinden" && x.Opponent.Honor < 34) return command.Choice == "honor" ? 120 : 60;
        // openbow: an Open-phase bow of an enemy Personality is worth most on the opponent's turn, when it stops that unit attacking (the bot's own cost, a bowed performer or spent Gold,
        // matters least then); on the bot's own turn only an attack it is about to make justifies it.
        if (Rule(x, "openbow") && action.Type == "useAbility" && x.View.Phase == Phase.Action && x.View.PendingChoice is null && command.AbilityId is { } bowId
            && AbilityCatalog.All.TryGetValue(bowId, out var bowAbility) && bowAbility.Timing is "Open" or "BattleOrOpen" && AbilityIntent.Of(bowAbility) is { IsReaction: false } bowIntent
            && (bowIntent.Rank == EffectRank.Bow && bowIntent.Side != TargetSide.Own || bowId == "shinden-choice") && x.Card(command.TargetId) is { Bowed: false, Definition.Type: CardType.Personality } bowed && bowed.OwnerId != x.Self.Id)
            return x.View.ActivePlayerId == x.Self.Id ? (DeclareScore(x) > PassScore ? score : Math.Min(score, BarredScore)) : Math.Max(score, 200 + UnitForceOf(x, bowed));
        if (action.Type is "useAbility" or "playStrategy" or "equip" && Contender2Bar(x, action, command)) return Math.Min(score, BarredScore);
        // nogift: a defense that loses the battle hands an Honor deck Honor for every card destroyed; with Provinces to spare it is not made (a presence defense is judged by its own rule).
        if (Rule(x, "nogift") && action.Type == "assignDefense" && command.Assignments is { Count: > 0 } given && x.Opponent.Honor >= 20 && x.Self.Provinces.Count(p => !p.Destroyed) > 2
            && !(Rule(x, "presence") && PresencePlan(x, action) is { } presenceKept && SameAssignments(presenceKept, given))
            && given.GroupBy(p => p.Value).Any(g => DefendersForce(x, g.Select(p => p.Key)) <= AttackersAt(x, g.Key)))
            return Math.Min(score, BarredScore);
        if (Rule(x, "presence") && action.Type == "assignDefense" && PresencePlan(x, action) is { } plan && SameAssignments(plan, command.Assignments)) return Math.Max(score, 130);
        return score;
    }

    private static bool SameAssignments(Dictionary<string, int> a, Dictionary<string, int>? b) => b is not null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

    // ---- presence defense -------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A removal the bot could take as a defender once present: how much attacking Force it takes away (Full: the largest attacking unit), and its Gold.</summary>
    private sealed record Removal(bool Full, int Minus, int Cost);

    private static readonly HashSet<string> AttackerTargets = new(StringComparer.Ordinal) { "attacking", "enemy-attacking", "enemy-attacking-card", "enemy-personality", "enemy-unit", "enemy-force", "enemy-card" };

    private static IEnumerable<Removal> DefenderRemovals(Ctx x)
    {
        var sources = x.Self.Hand.Select(c => (Card: c, FromHand: true))
            .Concat(x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Select(c => (Card: c, FromHand: false)));
        foreach (var (card, fromHand) in sources)
        {
            if (card.Definition is null || !fromHand && (card.Bowed || card.ActionLocked)) continue;
            foreach (var (id, ability, intent) in BattlePolicy.Printed(card))
            {
                if (ability.Timing is not ("Battle" or "BattleOrOpen") || intent.IsReaction || !AttackerTargets.Contains(ability.Target ?? "")) continue;
                var minus = System.Text.RegularExpressions.Regex.Match(AbilityIntent.EffectText(ability.Label ?? ""), @"[-−]\s?(\d+)\s?F");
                var full = BattlePolicy.SendsHome(id) || intent.Rank is EffectRank.Destroy or EffectRank.Bow && !minus.Success;
                if (!full && !minus.Success) continue;
                yield return new(full, minus.Success ? int.Parse(minus.Groups[1].Value) : 0, fromHand ? card.Definition.GoldCost : 0);
                break; // one use per card
            }
        }
        if (x.View.FavorPlayerId == x.Self.Id && AbilityCatalog.All.ContainsKey("favor-battle")) yield return new(true, 0, 0);
    }

    /// <summary>The attacking Force left at a battlefield after the removals the Gold pays for, each taking the largest unit (or its Force bonus off the largest).</summary>
    private static int ForceAfterRemovals(int[] attackingUnits, Removal[] removals, int gold)
    {
        var units = attackingUnits.OrderByDescending(f => f).ToList();
        foreach (var r in removals.OrderByDescending(r => r.Full).ThenBy(r => r.Cost))
        {
            if (r.Cost > gold || units.Count == 0) continue;
            gold -= r.Cost;
            if (r.Full) units.RemoveAt(0);
            else { units[0] = Math.Max(0, units[0] - r.Minus); units = units.OrderByDescending(f => f).ToList(); }
        }
        return units.Sum();
    }

    /// <summary>
    /// The presence defense: at the battlefield where the attack would raze the Province and the removals would stop it, the cheapest of the offered units whose presence (and Force) keeps the
    /// Province standing. Null when no attack threatens a Province, when the removals would not save it, or when nothing is offered.
    /// </summary>
    private static Dictionary<string, int>? PresencePlan(Ctx x, ActionOption action)
    {
        if (x.View.Phase is not (Phase.DefenseAssignment or Phase.CavalryDefense)) return null;
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var offered = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id)))
            .OrderBy(c => UnitValue(x.Self, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (offered.Length == 0) return null;
        var removals = DefenderRemovals(x).ToArray();
        if (removals.Length == 0) return null;
        var provinces = action.Provinces ?? x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        foreach (var province in x.Self.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderByDescending(p => AttackersAt(x, p.Index)))
        {
            var attackers = x.Opponent.Army.Where(c => c.ProvinceIndex == province.Index && c.Definition?.Type == CardType.Personality)
                .Select(c => ArmyForce(x.Opponent.Army.Where(h => h.Id == c.Id || h.AttachedTo == c.Id))).Where(f => f > 0).ToArray();
            var already = ArmyForce(x.Self.Army.Where(c => c.ProvinceIndex == province.Index));
            if (attackers.Length == 0 || attackers.Sum() <= province.Strength + already) continue; // it does not fall
            var left = ForceAfterRemovals(attackers, removals, Ready(x));
            var sent = new List<CardView>(); var force = already;
            foreach (var unit in offered)
            {
                if (sent.Count > 0 && left <= province.Strength + force) break;
                sent.Add(unit); force += UnitForce(x, unit);
            }
            if (left > province.Strength + force) continue; // even with removals and everyone, it falls: the ordinary defense decides
            return sent.ToDictionary(u => u.Id, _ => province.Index, StringComparer.Ordinal);
        }
        return null;
    }

    // ---- waste ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex BattlefieldClause = new(@"\bbattlefield\b|\battacking\b|\bopposed\b|\bdefending\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex MayNot = new(@"\bmay not\b|\bcannot\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex MinusForce = new(@"[-−]\s?\d+\s?F\b|\bForce\b[^.]*\b(penalty|to 0)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex StraightenText = new(@"\bstraighten\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>The waste rules: an action they rule out is scored below passing.</summary>
    private static bool Contender2Bar(Ctx x, ActionOption action, GameCommand command)
    {
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        var target = x.Card(command.TargetId);
        var ownTurn = x.View.ActivePlayerId == x.Self.Id;
        var openPhase = x.View.Phase == Phase.Action && x.View.PendingChoice is null;
        // selfharm: the bot's own unit is sent home or bowed only to save it from a battle it is losing.
        if (Rule(x, "selfharm") && target is not null && target.OwnerId == x.Self.Id && target.Definition?.Type is CardType.Personality or CardType.Follower
            && (BattlePolicy.SendsHome(command.AbilityId) || intent.Rank is EffectRank.Bow or EffectRank.Penalize && intent.Side != TargetSide.Own))
            return !(x.Odds is { Winning: false } && target.ProvinceIndex is not null
                && !(Rule(x, "lastprov") && !x.Odds.IsAttacker && x.Self.Provinces.Count(p => !p.Destroyed) <= 1)); // lastprov: the last Province is never given up
        // slate: Gold spent in the Action phase is Gold the Dynasty phase that follows cannot spend on the plan's purchases.
        if (Rule(x, "slate") && openPhase && ownTurn && (action.Gold?.Cost ?? 0) is var cost and > 0 && !intent.IsReaction && x.Plan is { Slate.Length: > 0 } plan)
        {
            var slateCost = plan.Slate.Select(id => x.Card(id)).OfType<CardView>().Sum(c => EffectiveCost(x, c));
            if (Ready(x) - cost < slateCost) return true;
        }
        // purpose: on its own turn, a straighten or a bow is worth something only for an attack it will make.
        if (Rule(x, "purpose") && openPhase && ownTurn && !intent.IsReaction && command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability)
            && (StraightenText.IsMatch(AbilityIntent.EffectText(ability.Label ?? "")) || intent.Rank == EffectRank.Bow && intent.Side != TargetSide.Own) && DeclareScore(x) <= PassScore)
            return true;
        // lobby: the Favor is not worth bowing one of the last two Personalities who could defend.
        if (Rule(x, "lobby") && intent.TakesFavorBowing && x.Self.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed) <= 2) return true;
        // provaim: a cut to a Province's strength outside a battle (Shrine to Nimuro) is made only on a turn the bot attacks; Complete aims it at the opponent's weakest Province.
        if (Rule(x, "provaim") && action.Type == "useAbility" && x.View.Phase == Phase.Action && command.AbilityId is { } aimId && AbilityCatalog.All.TryGetValue(aimId, out var aim)
            && LowersStrength.IsMatch(AbilityIntent.EffectText(aim.Label ?? "")) && (!ownTurn || DeclareScore(x) <= PassScore && !ArmyRazesAfterCut(x)))
            return true;
        // provtarget: a cut to a Province's strength is only worth anything at a battle the bot is attacking (there the existing rule aims it at the attacked Province); anywhere else it
        // lands on the first Province listed, often the bot's own.
        if (Rule(x, "provtarget") && action.Type == "useAbility" && command.AbilityId is { } lowerId && AbilityCatalog.All.TryGetValue(lowerId, out var lower)
            && LowersStrength.IsMatch(AbilityIntent.EffectText(lower.Label ?? "")) && !(x.Odds is { IsAttacker: true } && AttackedProvince(x) is not null))
            return true;
        // ownprov: a Province is a quarter of the game; giving one away for Honor is only right when that Honor wins it.
        if (Rule(x, "ownprov") && command.AbilityId is { } costId && AbilityCatalog.All.TryGetValue(costId, out var costly)
            && (costly.Costs ?? []).Any(c => c.Verb is CostVerbs.DestroyLeftmostProvince or CostVerbs.DestroyRightmostProvince) && x.Self.Honor < 36)
            return true;
        if (command.AbilityId is { } wasteId && AbilityCatalog.All.TryGetValue(wasteId, out var waste))
        {
            var effect = AbilityIntent.EffectText(waste.Label ?? "");
            // holdbo: a Battle-or-Open effect that depends on a battlefield ("if he is at a battlefield, bow him", attacking, opposed) loses that half in the Open phase.
            if (Rule(x, "holdbo") && openPhase && waste.Timing == "BattleOrOpen" && BattlefieldClause.IsMatch(effect)) return true;
            // selflock: "may not perform actions / assign" on one of its own cards, or on itself.
            var lockTarget = target ?? (intent.Side == TargetSide.None ? x.Card(command.CardId) : null);
            if (Rule(x, "selflock") && MayNot.IsMatch(effect) && lockTarget is not null && lockTarget.OwnerId == x.Self.Id) return true;
            // noop0: a Force penalty on a card that has no Force left.
            if (Rule(x, "noop0") && target is { Force: <= 0 } && target.OwnerId != x.Self.Id && MinusForce.IsMatch(effect) && intent.Rank < EffectRank.Bow) return true;
        }
        // selfboost: a Force bonus a card buys by bowing itself, given to itself, does nothing (a bowed unit adds no Force).
        if (Rule(x, "selfboost") && command.CardId is { } boostSource && (command.TargetId == boostSource || command.PerformerId == boostSource && command.TargetId is null)
            && command.AbilityId is { } boostId && AbilityCatalog.All.TryGetValue(boostId, out var boost)
            && ((boost.Costs ?? []).Any(c => c.Verb == CostVerb.Bow) || (boost.CostOptions ?? []).Any(c => c.Verb == CostVerb.Bow))
            && ForceBonus(boost) > 0) return true;
        // selfboost: on its own turn an Open-phase Force bonus matters only for an attack it is about to make.
        if (Rule(x, "selfboost") && openPhase && ownTurn && action.Type == "useAbility" && command.AbilityId is { } pumpId && AbilityCatalog.All.TryGetValue(pumpId, out var pump)
            && pump.Timing == "Open" && ForceBonus(pump) > 0 && intent.Side != TargetSide.Enemy && DeclareScore(x) <= PassScore) return true;
        // nostraighten: a straighten on a unit that is already straight does nothing.
        if (Rule(x, "nostraighten") && command.AbilityId is { } upId && AbilityCatalog.All.TryGetValue(upId, out var up) && StraightenText.IsMatch(AbilityIntent.EffectText(up.Label ?? ""))
            && intent.Side != TargetSide.Enemy && (target ?? x.Card(command.PerformerId)) is { Bowed: false }) return true;
        // tiecheck: a cost that bows (or an effect that removes) one of the bot's own units at a battle is not paid when its Force there would fall to the enemy's or below: a tie destroys both armies.
        if (Rule(x, "tiecheck") && x.Odds is { } tieOdds && x.View.Phase == Phase.Battle && command.AbilityId is { } tieId && AbilityCatalog.All.TryGetValue(tieId, out var tieAbility))
        {
            var lost = new List<CardView>();
            if (((tieAbility.Costs ?? []).Any(c => c.Verb == CostVerb.Bow) || command.CostOption == "bow") && (x.Card(command.PerformerId) ?? x.Card(command.CardId)) is { Bowed: false, ProvinceIndex: not null } payer && payer.OwnerId == x.Self.Id) lost.Add(payer);
            var lostForce = lost.Sum(c => ArmyForce(x.Self.Army.Where(h => h.Id == c.Id || h.AttachedTo == c.Id)));
            if (lostForce > 0 && tieOdds.Own - lostForce <= tieOdds.Enemy && tieOdds.Own > tieOdds.Enemy) return true;
        }
        // cycle: the Favor's discard-and-draw throws away a card the bot chose to keep.
        if (Rule(x, "cycle") && command.AbilityId == "favor-cycle") return true;
        return false;
    }

    // ---- lean attack ------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// lean: the champion defends only where it can win or save the Province, so an army with more Force than the Province's Strength plus every unbowed enemy defender razes it unopposed (and an
    /// undefended battlefield gives the defender no presence for Battle actions). The bot sends the smallest such armies at as many Provinces as it can, and keeps every other unit home, where it is a
    /// defender the opponent's next attack must beat. Null when no army can raze: the bot does not attack.
    /// </summary>
    private static Dictionary<string, int>? LeanPlan(Ctx x, CardView[] units, int[] provinces, bool maxRaze = false)
    {
        var defence = ArmyForce(x.Opponent.Home);
        var pool = units.Select(u => (Unit: u, Force: UnitForce(x, u))).Where(u => u.Force > 0).OrderByDescending(u => u.Force).ThenBy(u => u.Unit.Id, StringComparer.Ordinal).Take(9).ToArray();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        for (var k = Math.Min(3, Math.Min(targets.Length, pool.Length)); k >= 1; k--)
        {
            var chosen = targets.Take(k).ToArray();
            var need = chosen.Select(t => (Rule(x, "lean2") || maxRaze ? Math.Max(t.Strength, defence) : t.Strength + defence) + 1).ToArray(); // lean2: beating each of them separately razes it or destroys the defense
            int[]? best = null; var bestUsed = int.MaxValue; var slot = new int[pool.Length];
            void Search(int i, int[] force, int used)
            {
                if (used >= bestUsed) return;
                if (i == pool.Length)
                {
                    for (var j = 0; j < k; j++) if (force[j] < need[j]) return;
                    bestUsed = used; best = (int[])slot.Clone(); return;
                }
                for (var choice = 0; choice <= k; choice++) // k means home
                {
                    slot[i] = choice;
                    if (choice < k) { force[choice] += pool[i].Force; Search(i + 1, force, used + pool[i].Force); force[choice] -= pool[i].Force; }
                    else Search(i + 1, force, used);
                }
            }
            Search(0, new int[k], 0);
            if (best is not null)
                return Enumerable.Range(0, pool.Length).Where(i => best[i] < k).ToDictionary(i => pool[i].Unit.Id, i => chosen[best[i]].Index, StringComparer.Ordinal);
        }
        return null;
    }

    /// <summary>lean2, Cavalry segment: every army already sent beats both its Province's Strength and the enemy's unbowed defenders, so no Cavalry need join.</summary>
    private static bool LeanArmiesSuffice(Ctx x)
    {
        var defence = ArmyForce(x.Opponent.Home);
        return x.Self.Army.Where(c => c.ProvinceIndex is not null).GroupBy(c => c.ProvinceIndex!.Value)
            .All(g => ArmyForce(g) > Math.Max(defence, x.Opponent.Provinces.FirstOrDefault(p => p.Index == g.Key)?.Strength ?? 0));
    }

    /// <summary>favoraware: the opponent holds the Imperial Favor, whose Battle action sends one attacking unit home. A plan of a single army that no longer beats its Province once its largest unit
    /// is gone razes nothing: the bot keeps its army home instead.</summary>
    private static bool FavorStopsAttack(Ctx x, Dictionary<string, int> plan)
    {
        if (x.View.FavorPlayerId != x.Opponent.Id || plan.Values.Distinct().Count() != 1) return false;
        var province = x.Opponent.Provinces.FirstOrDefault(p => p.Index == plan.Values.First());
        if (province is null) return false;
        var units = x.Self.Home.Concat(x.Self.Army).Where(c => plan.ContainsKey(c.Id)).Select(c => UnitForce(x, c)).OrderByDescending(f => f).ToArray();
        return units.Skip(1).Sum() <= Math.Max(province.Strength, ArmyForce(x.Opponent.Home));
    }

    /// <summary>maxraze: the most Provinces the army can raze this turn, found exactly (each army beats its Province's Strength and the unbowed defenders, as lean2), with every unit the armies do not
    /// need added to the army with the thinnest margin: nobody stays home (keeping units home lost games).</summary>
    private static Dictionary<string, int>? MaxRazePlan(Ctx x, CardView[] units, int[] provinces)
    {
        if (LeanPlan(x, units, provinces, maxRaze: true) is not { } lean) return null;
        var plan = new Dictionary<string, int>(lean, StringComparer.Ordinal);
        var defence = ArmyForce(x.Opponent.Home);
        int Margin(int province) => units.Where(u => plan.TryGetValue(u.Id, out var p) && p == province).Sum(u => UnitForce(x, u))
            - Math.Max(defence, x.Opponent.Provinces.First(p => p.Index == province).Strength);
        foreach (var unit in units.Where(u => !plan.ContainsKey(u.Id)).OrderByDescending(u => UnitForce(x, u)))
            plan[unit.Id] = plan.Values.Distinct().OrderBy(Margin).ThenBy(p => p).First();
        return plan;
    }

    /// <summary>lean3, Cavalry segment: every army already sent beats its Province's Strength plus every unbowed defender, so it razes whatever the defense does.</summary>
    private static bool LeanArmiesRaze(Ctx x)
    {
        var defence = ArmyForce(x.Opponent.Home);
        return x.Self.Army.Where(c => c.ProvinceIndex is not null).GroupBy(c => c.ProvinceIndex!.Value)
            .All(g => ArmyForce(g) > defence + (x.Opponent.Provinces.FirstOrDefault(p => p.Index == g.Key)?.Strength ?? 0));
    }

    /// <summary>provaim: the whole unbowed home army beats every unbowed defender, so with one Province's strength cut to nothing it razes it.</summary>
    private static bool ArmyRazesAfterCut(Ctx x) => ArmyForce(x.Self.Home) > ArmyForce(x.Opponent.Home);
}
