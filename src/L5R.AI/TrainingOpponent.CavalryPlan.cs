using L5R.Core;

namespace L5R.AI;

// Issue #1360, second pass (Mendon, 2026-11-27): the Cavalry waits for its segment and then takes Provinces. Arena logs of the first pass showed the Unicorn decks dodging
// defenders and taking Provinces earlier, but failing to finish: the plan was valued with its Cavalry removed, the Cavalry follow-up was sized with no margin for the
// defender's hand, and small Cavalry contingents were held back where showing the whole army would have deterred the defence.
//   cavplan  the plan's Cavalry is carried into the Cavalry segment (in play and in the lookahead's playouts) and re-targeted against the defence as placed: the planned
//            groups that still take their Province ride there, the rest take as many other Provinces as they can, each with a margin for the cards in the defender's hand
//            (cavM Force per card, at most cavMH cards); the Cavalry search plays lkCN (2) samples so it finishes inside its time limit.
//   cavpick  Cavalry is held for its segment only when holding pays: the opponent has two or more Provinces left to sidestep to, and the bot has at least cavN (3) Cavalry
//            units ready; otherwise the whole army assigns in the Infantry segment, as without cavhold.
// Third pass (ablation, seeds 12104-12105): the attack rules alone lost 7 of 8 flipped Unicorn Maidens games, six of them to Military defeat of an Honor deck that sent 50% more riders
// than Hard and kept nobody home. cavkeep answers that:
//   cavkeep  only the Force needed to take each Province rides; riders left over stay home rather than reinforce; the units Guard computes (enough to stop the counter-attack
//            razing a last Province, guardN) stay home whatever the guard rule says; a planned group keeps its planned Province when it beats the defence as placed there
//            (its Strength and defenders; the margin is for re-targeted riders), so the plan search's shapes stay distinct in its playouts; and when nothing can be taken the
//            Cavalry stays home rather than falling back to the planner's whole-army battlefield.
public static partial class TrainingOpponent
{
    /// <summary>cavplan: the Force a Cavalry follow-up adds for the battle actions the defender may still hold (its hand, capped).</summary>
    private static int CavalryMargin(Ctx x) => Rule(x, "cavplan") ? (int)(Knob(x, "cavM", 1) * Math.Min(x.Opponent.HandCount, Knob(x, "cavMH", 4))) : 0;

    /// <summary>cavpick: holding the Cavalry for its segment pays when there are Provinces left to sidestep to and enough Cavalry to make the choice worth keeping.</summary>
    private static bool HoldPays(Ctx x) =>
        !Rule(x, "cavpick") || x.Opponent.Provinces.Count(p => !p.Destroyed) >= 2
        && x.Self.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed && IsCavalryUnit(x.Self, c)) >= Knob(x, "cavN", 3);

    /// <summary>cavplan: the Cavalry declaration that follows a plan against the defence as placed. The plan's groups that still take their Province (with the infantry already
    /// there, against its Strength, its defenders, the defender's Cavalry still to come and the margin) ride there; every other rider takes the cheapest Province left that it can,
    /// alone if one unit suffices, else the strongest together; units left over reinforce the battlefield with the thinnest surplus. Null when nothing can be taken.
    /// Without a plan, every offered rider is free.</summary>
    private static Dictionary<string, int>? CavalryFollowUp(Ctx x, ActionOption action, Dictionary<string, int>? planned)
    {
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var keep = Rule(x, "cavkeep");
        var home = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var guard = keep ? Guard(x, home, x.Self.Army.Where(c => c.Definition?.Type == CardType.Personality).ToArray(), always: true).Select(c => c.Id).ToHashSet(StringComparer.Ordinal) : [];
        var riders = home.Where(c => UnitForce(x, c) > 0 && !guard.Contains(c.Id) && (legal is null || legal.Contains(c.Id)) && (planned is null || planned.ContainsKey(c.Id)))
            .OrderByDescending(c => UnitForce(x, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && (action.Provinces is null || action.Provinces.Contains(p.Index))).ToList();
        if (riders.Count == 0 || targets.Count == 0) return null;
        var reply = PossibleCavalryForce(x.Opponent); var margin = CavalryMargin(x);
        var against = targets.ToDictionary(p => p.Index, p => p.Strength + ArmyForce(x.Opponent.Army.Where(c => c.ProvinceIndex == p.Index)) + reply + margin);
        var sent = targets.ToDictionary(p => p.Index, p => ArmyForce(x.Self.Army.Where(c => c.ProvinceIndex == p.Index)));
        int Need(ProvinceView p) => against[p.Index] - sent[p.Index];
        int Placed(ProvinceView p) => p.Strength + ArmyForce(x.Opponent.Army.Where(c => c.ProvinceIndex == p.Index)) - sent[p.Index]; // cavkeep: the defence as placed, no reply or margin
        var result = new Dictionary<string, int>(StringComparer.Ordinal); var free = new List<CardView>(riders); var taken = new List<ProvinceView>();
        void Ride(ProvinceView p, IEnumerable<CardView> group)
        {
            foreach (var u in group.ToList()) { result[u.Id] = p.Index; free.Remove(u); sent[p.Index] += UnitForce(x, u); }
            if (!taken.Contains(p)) taken.Add(p);
        }
        if (planned is not null)
            foreach (var group in riders.Where(r => planned.ContainsKey(r.Id)).GroupBy(r => planned[r.Id]).OrderBy(g => g.Key))
                if (targets.FirstOrDefault(p => p.Index == group.Key) is { } province && group.Sum(u => UnitForce(x, u)) > (keep ? Placed(province) : Need(province))) Ride(province, group);
        foreach (var province in targets.Except(taken).OrderBy(Need).ThenBy(p => p.Index).ToList())
        {
            var need = Need(province);
            if (need < 0 || free.Sum(u => UnitForce(x, u)) <= need) continue; // already falling to the infantry there, or out of reach
            if (free.Where(u => UnitForce(x, u) > need).OrderBy(u => UnitForce(x, u)).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault() is { } alone) { Ride(province, [alone]); continue; }
            var group = new List<CardView>(); var force = 0;
            foreach (var u in free) { if (force > need) break; group.Add(u); force += UnitForce(x, u); }
            Ride(province, group);
        }
        if (taken.Count == 0) return null;
        if (!keep) foreach (var u in free.ToList()) Ride(taken.OrderBy(p => sent[p.Index] - against[p.Index]).ThenBy(p => p.Index).First(), [u]);
        return result;
    }

    /// <summary>A decision context for the deciding seat in a playout's position, reading the same rules and knobs as the bot that searches.</summary>
    private static Ctx PlayoutCtx(Ctx x, MatchView view, ActionOption[] actions)
    {
        var self = view.Players.Single(p => p.Id == view.ViewerId); var opponent = view.Players.Single(p => p.Id != view.ViewerId);
        return new Ctx(view, self, opponent, x.Profile, x.Memory, AllCards(view)) { Actions = actions };
    }
}
