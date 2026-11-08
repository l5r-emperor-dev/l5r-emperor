using L5R.Core;

namespace L5R.AI;

// Issue #1185, slice 2: the planner's battle estimate. Mendon (interview rounds 4-5): there is no fixed Force margin. A player asks how much each side can CHANGE the Force: my
// destroy / bow / send-home actions and +Force cards against their units and their guessed hand. One big unit against my removal is an easy attack; several small units, each with an
// ability and attachments, is a dangerous one. The estimate is therefore a swing model, not a margin rule:
//   Net = my Force + what my hand can add (within my Gold)  -  their defending Force  -  what their units and hand could take back.
// A battle is WON when Net > 0 and the Province RAZED when Net exceeds its Strength. It reads only the seat's own view, its own hand, and what the opponent has made public.
internal sealed record Trick(int Gain, int Cost, bool Removal);

/// <summary>The estimate for one attack. Own and Enemy are printed Force; OwnSwing the Force my hand adds; EnemySwing the Force their table and hand could change; Reserve the Gold to keep for my own tricks.</summary>
internal sealed record BattleEstimate(int Own, int OwnSwing, int Enemy, int EnemySwing, int Strength, int Reserve)
{
    public int Net => Own + OwnSwing - Enemy - EnemySwing;
    public bool Wins => Net > 0;
    public bool Razes => Net > Strength;
}

/// <summary>The attack the plan intends: one Province, whether it should fall, and why.</summary>
internal sealed record AttackChoice(int Province, bool Raze, BattleEstimate Estimate, string Why);

public static partial class TrainingOpponent
{
    private const double OwnSwingConfidence = 0.8; // my own tricks can be negated or answered, so the Gold reserved is sized on four fifths of what they add
    private const int TrickSwing = 3;              // the Force an unseen enemy trick is assumed to change (a bow or a +3 Force card)
    private const double UnseenStrategyShare = .4; // the share of an opponent's hand assumed to be Battle-action Strategies before their deck has shown anything

    private static int UnitForce(Ctx x, CardView unit) => ArmyForce(x.Self.Home.Concat(x.Self.Army).Where(h => h.Id == unit.Id || h.AttachedTo == unit.Id));

    /// <summary>Tricks the hand holds: a +Force card adds its Force; a destroy, bow or send-home Strategy removes an enemy unit's Force (assigned to the largest unit left when it is counted).</summary>
    private static Trick[] HandTricks(Ctx x) =>
        x.Self.Hand.Where(c => c.Definition is not null).Select(c =>
        {
            var def = c.Definition!; var cost = def.GoldCost;
            if (def.Effect == "force-three") return new Trick(3, cost, false);
            var gain = 0; var removal = false;
            foreach (var (id, ability, intent) in BattlePolicy.Printed(c))
            {
                if (ability.Timing is not ("Battle" or "BattleOrOpen")) continue;
                if (intent.Side == TargetSide.Enemy && (intent.Rank >= EffectRank.Bow || BattlePolicy.SendsHome(id))) removal = true;
                else if (intent.Side != TargetSide.Enemy) { var created = CreatedForce.Match(AbilityIntent.EffectText(ability.Label ?? "")); gain = Math.Max(gain, Math.Max(ForceBonus(ability), created.Success ? int.Parse(created.Groups[1].Value) : 0)); }
            }
            if (def.Effect == "bow-small") removal = true;
            return removal ? new Trick(0, cost, true) : new Trick(gain, cost, false);
        }).Where(t => t.Removal || t.Gain > 0).ToArray();

    /// <summary>The tricks the Gold pays for, taking the biggest swing first. A removal trick is worth the Force of the largest enemy unit still standing.</summary>
    private static (int[] Gains, int[] Costs) ChooseTricks(Trick[] tricks, int gold, int[] enemyUnits)
    {
        var units = enemyUnits.OrderByDescending(f => f).ToList(); var left = tricks.ToList(); var gains = new List<int>(); var costs = new List<int>();
        while (left.Count > 0 && gains.Count < 4)
        {
            var best = left.Where(t => t.Cost <= gold).Select(t => (Trick: t, Gain: t.Removal ? units.FirstOrDefault() : t.Gain)).Where(t => t.Gain > 0)
                .OrderByDescending(t => t.Gain).ThenBy(t => t.Trick.Cost).FirstOrDefault();
            if (best.Trick is null) break;
            left.Remove(best.Trick); gold -= best.Trick.Cost; gains.Add(best.Gain); costs.Add(best.Trick.Cost);
            if (best.Trick.Removal && units.Count > 0) units.RemoveAt(0);
        }
        return (gains.ToArray(), costs.ToArray());
    }

    /// <summary>How many Battle-action tricks the opponent may hold: their hand sized by the share of Strategies their deck has shown (a spent Strategy is public), capped by the Gold they can pay with.</summary>
    private static int EnemyTricks(Ctx x)
    {
        var seen = x.Opponent.Discard.Concat(x.Opponent.RemovedFromGame).Where(c => c.Definition is { } d && d.Type is CardType.Strategy or CardType.Follower or CardType.Item or CardType.Spell).ToArray();
        var share = seen.Length >= 3 ? seen.Count(c => c.Definition!.Type == CardType.Strategy) / (double)seen.Length : UnseenStrategyShare;
        var gold = Production(x.Opponent.Stronghold, x.Opponent.Clan) + x.Opponent.Home.Sum(c => Production(c, x.Opponent.Clan));
        return (int)Math.Min(Math.Min(4, Math.Ceiling(x.Opponent.HandCount * share)), gold / 2);
    }

    /// <summary>The estimate for sending <paramref name="units"/> (plus any already sent) at <paramref name="province"/>, against everything the opponent has at home.</summary>
    private static BattleEstimate EstimateAttack(Ctx x, CardView[] units, ProvinceView province)
    {
        var own = units.Sum(u => UnitForce(x, u));
        var defenders = x.Opponent.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var enemy = ArmyForce(x.Opponent.Home);
        var unitForces = defenders.Select(d => ArmyForce(x.Opponent.Home.Where(h => h.Id == d.Id || h.AttachedTo == d.Id))).ToArray();
        var threats = Math.Min(3, defenders.Count(BattlePolicy.IsThreat));
        var enemySwing = (int)Math.Round((EnemyTricks(x) + threats) * Knob(x, "swing", TrickSwing));
        if (Rule(x, "nopresence") && defenders.Length == 0) enemySwing = 0; // issue #1185 tuning: with nobody to defend, the opponent has no presence at the battle and takes no Battle action there
        var (gains, costs) = ChooseTricks(HandTricks(x), Ready(x), unitForces);
        var swing = gains.Sum();
        // The Gold to keep for the fight: the smallest set of tricks that still wins when my tricks work four times in five and theirs all work.
        var reserve = 0; double pessimistic = own - enemy - enemySwing;
        for (var i = 0; i < gains.Length && pessimistic <= 0; i++) { pessimistic += gains[i] * OwnSwingConfidence; reserve += costs[i]; }
        return new BattleEstimate(own, swing, enemy, enemySwing, province.Strength, pessimistic > 0 ? reserve : costs.Sum());
    }

    // ---- which Province ---------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>What destroying a Province takes from the opponent. Mendon: a face-up Personality with good abilities that they can afford first; then a face-down card (it may be a Personality) before a Holding;
    /// a Holding that draws cards or gains Honor beats an ordinary one.</summary>
    private static double ProvinceValue(Ctx x, ProvinceView province)
    {
        if (province.Card is not { } card) return 10;
        if (card.Definition is not { } def) return 50;
        switch (def.Type)
        {
            case CardType.Personality:
                var threat = BattlePolicy.Printed(card).Any(p => BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent)) ? 20 : 0;
                var gold = Production(x.Opponent.Stronghold, x.Opponent.Clan) + x.Opponent.Home.Sum(c => Production(c, x.Opponent.Clan));
                return 80 + 6 * def.Force + threat + (def.GoldCost <= gold ? 20 : 0);
            case CardType.Holding:
                return BattlePolicy.Printed(card).Any(p => p.Intent.Draws || p.Intent.HonorGain) ? 70 : 25 + 6 * def.GoldProduction;
            default: return 40;
        }
    }

    /// <summary>The attack the plan makes: the most valuable Province it can raze (the strongest on a tie: it only gets harder later); failing that, the Province whose defenders it can beat and kill, the weakest first.
    /// Null when no attack wins. Units already sent in an earlier segment fix the Province.</summary>
    private static AttackChoice? ChooseAttack(Ctx x)
    {
        if (x.View.ActivePlayerId != x.Self.Id) return null;
        var sent = x.Self.Army.Where(c => c.Definition?.Type == CardType.Personality && c.ProvinceIndex is not null).ToArray();
        var ready = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var provinces = x.Opponent.Provinces.Where(p => !p.Destroyed).ToArray();
        if (provinces.Length == 0 || ready.Length + sent.Length == 0) return null;
        var units = ready.Concat(sent).ToArray();
        var estimates = provinces.Select(p => (Province: p, Estimate: EstimateAttack(x, units, p))).ToArray();
        var razing = estimates.Where(e => e.Estimate.Razes).OrderByDescending(e => ProvinceValue(x, e.Province)).ThenByDescending(e => e.Province.Strength).ThenBy(e => e.Province.Index).ToArray();
        if (razing.Length > 0) return new(razing[0].Province.Index, true, razing[0].Estimate, $"can raze Province {razing[0].Province.Index + 1} (net Force {razing[0].Estimate.Net} against Strength {razing[0].Province.Strength})");
        var winning = estimates.Where(e => e.Estimate.Wins).OrderBy(e => e.Province.Strength).ThenBy(e => e.Province.Index).ToArray();
        if (winning.Length > 0) return new(winning[0].Province.Index, false, winning[0].Estimate, $"can beat the defenders at Province {winning[0].Province.Index + 1} (net Force {winning[0].Estimate.Net}) though not raze it");
        return null;
    }

    /// <summary>The Province an assignment goes to once the plan has one: what was already sent fixes it, otherwise the plan's own. Whole army to one battle, unless the champion's own split can raze several.</summary>
    private static Dictionary<string, int>? PlannerAssignment(Ctx x, CardView[] units, int[] provinces)
    {
        if (x.Plan?.Attack is not { } attack) return null;
        var sentAt = x.Self.Army.Where(c => c.Definition?.Type == CardType.Personality && c.ProvinceIndex is not null).GroupBy(c => c.ProvinceIndex!.Value).OrderByDescending(g => g.Count()).Select(g => (int?)g.Key).FirstOrDefault();
        var target = sentAt ?? attack.Province;
        if (!provinces.Contains(target)) return null;
        if (sentAt is null && PlanAttack(x, units, provinces) is { } split && split.Values.Distinct().Count() >= 2) return HoldCavalry(x, OfferedOnly(split, units));
        return HoldCavalry(x, units.ToDictionary(c => c.Id, _ => target, StringComparer.Ordinal));
    }
}
