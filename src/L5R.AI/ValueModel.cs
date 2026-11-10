using L5R.Core;

namespace L5R.AI;

// Issue #1206 (lkVal, docs/bots/CONAN_CAMPAIGN_BRIEF.md item 7): a learned value for the end of a playout. A logistic model predicts, from the deciding seat's view,
// the chance that this seat goes on to win. Its features are card-agnostic public facts (and the seat's own hand size), so it reads nothing the seat cannot see.
// The weights are fitted by tools/bots/fit_value.py from arena games exported with L5R_TOURN_SNAP, and kept here as a table, not a runtime dependency.
public static class ValueModel
{
    public static readonly string[] Names =
    [
        "bias", "turn", "active", "favor",
        "prov", "last", "units", "unbowed", "pers", "honor", "honorHigh", "dishonor", "hand", "gold",
        "oProv", "oLast", "oUnits", "oUnbowed", "oPers", "oHonor", "oHonorHigh", "oDishonor", "oHand", "oGold",
    ];

    /// <summary>The features of <paramref name="view"/> for its viewer, in the order of <see cref="Names"/>.</summary>
    public static double[] Features(MatchView view)
    {
        var me = view.Players.Single(p => p.Id == view.ViewerId); var them = view.Players.Single(p => p.Id != view.ViewerId);
        var f = new List<double> { 1, view.TurnNumber / 10.0, view.ActivePlayerId == view.ViewerId ? 1 : 0, view.FavorPlayerId is not { } favor ? 0 : favor == view.ViewerId ? 1 : -1 };
        foreach (var p in new[] { me, them })
        {
            var inPlay = p.Home.Concat(p.Army).ToArray(); var provinces = p.Provinces.Count(q => !q.Destroyed);
            f.Add(provinces); f.Add(provinces == 1 ? 1 : 0);
            f.Add(inPlay.Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower or CardType.Item).Sum(c => c.Definition!.GoldCost + Math.Max(0, c.Definition.Force)) / 10.0);
            f.Add(inPlay.Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower && !c.Bowed).Sum(c => Math.Max(0, c.Force)) / 10.0);
            f.Add(inPlay.Count(c => c.Definition?.Type == CardType.Personality));
            f.Add(p.Honor / 10.0); f.Add(Math.Max(0, p.Honor - 30) / 10.0); f.Add(Math.Max(0, -p.Honor) / 10.0);
            f.Add(p.HandCount);
            f.Add((p.Stronghold.Definition?.GoldProduction ?? 0) + p.Home.Where(c => c.Definition?.Type == CardType.Holding).Sum(c => c.Definition!.GoldProduction));
        }
        return [.. f];
    }

    /// <summary>Fitted weights, one per feature (tools/bots/fit_value.py). All zero until a fit is pasted in.</summary>
    public static double[] Weights { get; set; } = new double[Names.Length];

    /// <summary>The log-odds that the viewer wins.</summary>
    public static double Logit(MatchView view)
    {
        var f = Features(view); double z = 0;
        for (var i = 0; i < f.Length; i++) z += Weights[i] * f[i];
        return z;
    }
}
