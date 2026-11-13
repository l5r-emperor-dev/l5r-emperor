using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.2): the ledger. Everything public or the seat's own, read once from the view into the shape the exchange reasons with: each unit with
// its Force, its abilities as descriptors and which are spent; Provinces and their Strength; Gold now; Honor; the Favor; the seat's own hand exactly, the opponent's as a count.

/// <summary>One unit (a Personality with its attachments) as the Read sees it. UnitForce adds the Followers and is the unit's Force when straightened; Contribution is what counts now (nothing while bowed).</summary>
public sealed record LedgerUnit(string Id, string Name, int OwnerId, int Force, int UnitForce, bool Bowed, bool Cavalry, int? Province, EffectDescriptor[] Abilities, string[] Spent,
    int Chi, int PersonalHonor, bool Dishonorable, bool ActionLocked, int Attachments, int Followers, int GoldCost, string[]? Keywords = null)
{
    /// <summary>A Tactician may take one Tactical action a turn: Tactical Advantage discards a card for its Focus Value in Force (RULES_DIGEST.md section 5), the late Force Mendon counts at declaration.</summary>
    public bool Tactician => Keywords?.Contains("Tactician") ?? false;
    /// <summary>What this unit contributes to its army's total now.</summary>
    public int Contribution => Bowed ? 0 : UnitForce;
    /// <summary>Battle-timed abilities the unit can still use: not spent, the unit unbowed and free to act.</summary>
    public IEnumerable<EffectDescriptor> UsableBattle => Bowed || ActionLocked ? [] : Abilities.Where(a => a.IsBattle && !Spent.Contains(a.Id));
}

/// <summary>An answer a side can play in a battle: a descriptor with where it comes from and what it costs. Synthetic answers stand for the belief's expected hand cards.</summary>
public sealed record Answer(EffectDescriptor Effect, string Source, bool FromHand, int Gold, bool Synthetic = false)
{
    public override string ToString() => $"{Effect.Class}{(Effect.Magnitude is { } m ? m : "")}{(Synthetic ? "?" : "")}@{Source}";
}

public sealed record LedgerSide(int Id, LedgerUnit[] Units, (int Index, int Strength)[] Provinces, int GoldNow, int GoldCapacity, int Honor, bool Favor, int HandCount, Answer[] HandAnswers, Answer[] TableAnswers, int BestFocus = 0)
{
    public IEnumerable<LedgerUnit> Home => Units.Where(u => u.Province is null);
    public IEnumerable<LedgerUnit> At(int province) => Units.Where(u => u.Province == province);
    public int UnbowedHomeForce => Home.Sum(u => u.Contribution);
}

public sealed record Ledger(int ViewerId, int Turn, Phase Phase, LedgerSide Me, LedgerSide Them)
{
    public static Ledger Of(MatchView view)
    {
        var me = view.Players.Single(p => p.Id == view.ViewerId); var them = view.Players.Single(p => p.Id != view.ViewerId);
        return new(view.ViewerId, view.TurnNumber, view.Phase, Side(view, me, own: true), Side(view, them, own: false));
    }

    private static LedgerSide Side(MatchView view, PlayerView p, bool own)
    {
        var all = p.Home.Concat(p.Army).ToArray();
        var units = all.Where(c => c.Definition?.Type == CardType.Personality).Select(c =>
        {
            var attached = all.Where(a => a.AttachedTo == c.Id).ToArray();
            var followers = attached.Where(a => a.Definition?.Type == CardType.Follower).ToArray();
            // the engine's UnitForce is the contribution now (0 while bowed, bowed Followers left out); a bowed unit keeps its full Force here, for the projection of it straightened, and Contribution zeroes it
            var unitForce = c.Bowed ? Math.Max(0, c.Force) + followers.Sum(f => Math.Max(0, f.Force)) : c.UnitForce ?? Math.Max(0, c.Force) + followers.Where(f => !f.Bowed).Sum(f => Math.Max(0, f.Force));
            var abilities = EffectDescriptors.Of(c).Concat(attached.SelectMany(EffectDescriptors.Of)).ToArray();
            var spent = (c.SpentAbilityIds ?? []).Concat(attached.SelectMany(a => a.SpentAbilityIds ?? [])).ToArray();
            var cavalry = (c.Definition?.Keywords?.Contains("Cavalry") ?? false) && followers.All(f => f.Definition?.Keywords?.Contains("Cavalry") ?? false);
            return new LedgerUnit(c.Id, c.Definition?.Name ?? c.Id, p.Id, Math.Max(0, c.Force), unitForce, c.Bowed, cavalry, c.Zone == Zone.Battlefield ? c.ProvinceIndex : null, abilities, spent,
                c.Chi, c.Definition?.PersonalHonor ?? 0, c.Dishonorable, c.ActionLocked, attached.Length, followers.Length, c.Definition?.GoldCost ?? 0, c.Definition?.Keywords);
        }).ToArray();
        var provinces = p.Provinces.Where(q => !q.Destroyed).Select(q => (q.Index, q.Strength)).ToArray();
        int Production(CardView c) => c.Bowed ? 0 : c.Definition?.GoldProduction ?? 0;
        var goldNow = Production(p.Stronghold) + p.Home.Where(h => h.Definition?.Type == CardType.Holding).Sum(Production);
        var capacity = (p.Stronghold.Definition?.GoldProduction ?? 0) + p.Home.Where(h => h.Definition?.Type == CardType.Holding).Sum(h => h.Definition!.GoldProduction);
        // Answers on the table that need no unit to carry them: the Stronghold's and Holdings' Battle abilities, and the Favor's send-home.
        var table = new List<Answer>();
        foreach (var card in p.Home.Where(h => h.Definition?.Type == CardType.Holding).Append(p.Stronghold).Where(c => !c.Bowed))
            foreach (var d in EffectDescriptors.Of(card).Where(d => d.IsBattle && d.Neutralises && !(card.SpentAbilityIds?.Contains(d.Id) ?? false))) table.Add(new(d, card.Definition?.Name ?? card.Id, false, d.GoldCost));
        if (view.FavorPlayerId == p.Id && AbilityCatalog.All.ContainsKey("favor-battle")) table.Add(new(EffectDescriptors.Of("favor-battle"), "Favor", false, 0));
        var hand = own ? p.Hand.Where(c => c.Definition is not null).SelectMany(c => EffectDescriptors.Of(c).Where(d => d.IsBattle && (d.Neutralises || d.Class is EffectClass.ForceAdd or EffectClass.Negate)).Take(1).Select(d => new Answer(d, c.Definition!.Name, true, c.Definition.GoldCost))).ToArray() : [];
        var bestFocus = own ? p.Hand.Where(c => c.Definition is not null).Select(c => c.Definition!.Focus).DefaultIfEmpty(0).Max() : p.HandCount > 0 ? 3 : 0; // the opponent's best Focus Value is unseen: a typical card's
        return new(p.Id, units, provinces, goldNow, capacity, p.Honor, view.FavorPlayerId == p.Id, p.HandCount, hand, table.ToArray(), bestFocus);
    }
}
