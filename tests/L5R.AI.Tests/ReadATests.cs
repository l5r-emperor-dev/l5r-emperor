using L5R.AI;
using L5R.Core;

// Issue #1206 (RULE_FAMILIES.md family A, rule readA): the attack declaration and the infantry assignment follow the Read's opportunity when the rule is on; with read on and readA off the
// opportunity is logged beside the Contender's choice and changes nothing.
internal static class ReadATests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static CardView Home(string id, int owner, int force) => new(id, BattlePolicyTests.Person(force), owner, Zone.Home, true, false, null, null, force);
    private static MatchView View(int[] mine, int[] theirs, int strength)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        ProvinceView[] Provinces(int owner) => view.Players[owner].Provinces.Select(p => p with { Strength = strength, Destroyed = false }).ToArray();
        return view with
        {
            Phase = Phase.AttackDeclaration,
            ActivePlayerId = 0,
            PriorityPlayerId = 0,
            Players = [view.Players[0] with { Home = mine.Select((f, i) => Home($"m{i}", 0, f)).ToArray(), Army = [], Provinces = Provinces(0) },
                view.Players[1] with { Home = theirs.Select((f, i) => Home($"t{i}", 1, f)).ToArray(), Army = [], Provinces = Provinces(1) }]
        };
    }
    private static ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
    private static ActionOption Declare(MatchView v) => new("declareAttack", "Declare", true, null, new("declareAttack", 0, v.Version));
    private static ActionOption Attack(MatchView v) => new("attack", "Attack", true, null, new("attack", 0, v.Version, Assignments: []));
    private static BotContext With(params string[] rules) => BotContext.Contender(rules: [.. BotContext.TunedRules, .. rules]);

    public static readonly (string, Action)[] Tests =
    [
        ("readA declares where the opportunity attacks and assigns its shape; it passes where the opportunity holds", () =>
        {
            var strong = View([12, 3], [4], 3); // 12 alone razes 4 + 3: the sufficient group is the 12; the 3 stays home
            var declared = TrainingOpponent.Choose(strong, [Pass(strong), Declare(strong)], With("read", "readA")).Command;
            Check(declared?.Type == "declareAttack", $"the opportunity attacks: declared, not {declared?.Type}");
            var infantry = strong with { Phase = Phase.InfantryDeclaration };
            var assigned = TrainingOpponent.Choose(infantry, [Pass(infantry), Attack(infantry)], With("read", "readA")).Command;
            Check(assigned is { Type: "attack", Assignments: { Count: >= 1 } } && assigned.Assignments.ContainsKey("m0"), $"the 12 goes: {assigned?.Type} {string.Join(",", assigned?.Assignments?.Keys.ToArray() ?? [])}");
            var weak = View([3], [4], 9); // 3 cannot exceed 9 even undefended
            var held = TrainingOpponent.Choose(weak, [Pass(weak), Declare(weak)], With("read", "readA")).Command;
            Check(held?.Type == "pass", $"the opportunity holds: pass, not {held?.Type}");
        }),
        ("in shadow (read on, readA off) the Contender's choice is unchanged and the opportunity is logged beside it", () =>
        {
            var weak = View([3], [4], 9);
            var off = TrainingOpponent.Choose(weak, [Pass(weak), Declare(weak)], BotContext.Contender()).Command; var shadow = With("read");
            var with = TrainingOpponent.Choose(weak, [Pass(weak), Declare(weak)], shadow).Command;
            Check(off?.Type == with?.Type, "the shadow changed the choice");
            Check(shadow.AttackLog.Count == 1 && shadow.AttackLog[0].Contains("read hold"), $"the opportunity was logged: {string.Join(" / ", shadow.AttackLog)}");
        }),
    ];
}
