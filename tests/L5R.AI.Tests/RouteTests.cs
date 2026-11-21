using L5R.AI;
using L5R.Core;

// Issue #1206 (strategy brief step 3, family G; RULE_FAMILIES.md G; PLAYER_MODEL.md 3.1): the route gate. A lock or a drain passes unless the Read's best attack risks nothing; a wait
// passes; a race or a punish declares; press, build and hold are silent; a lethal attack always declares. The drain clock reads the table's repeatable Honor-loss abilities before the trend
// has shown a loss.
internal static class RouteTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static readonly EffectDescriptor Tax2 = new("test-tax", "Open", null, EffectClass.HonorTax, 2, false, TargetSide.Enemy, TargetKind.Player, [], [], null, true, false, true, false, false, 0, 0, false, false, 1, false, null);
    private static LedgerUnit U(string id, int owner, int force, bool bowed = false, EffectDescriptor[]? abilities = null) => new(id, id, owner, force, force, bowed, false, null, abilities ?? [], [], 2, 0, false, false, 0, 0, 2, null);
    private static LedgerSide Side(int id, LedgerUnit[] units, (int, int)[] provinces, int honor = 20) => new(id, units, provinces, 4, 4, honor, false, 0, [], [], 0);
    private static Read ReadOf(LedgerSide me, LedgerSide them) => new(new Ledger(me.Id, 8, Phase.AttackDeclaration, me, them), new Belief(them.HandCount, Belief.Prior, 0, []), "test", me.Id);
    private static AttackOption Hold() => new("hold", [], [], "-", "", 0, 0, 0, 0, 0, ThreatRead.None, 0);
    private static AttackOption Attack(double provinces, double exposure, int losses = 0, int cards = 0) => new("lone", [], [(0, 5, Array.Empty<LedgerUnit>())], "nobody", "111", provinces, 0, losses, cards, provinces, ThreatRead.None, exposure);
    private static Answers With(Posture posture, bool attacks, bool viable = true) => new(new(attacks ? Attack(.8, .05) : Hold(), Hold(), viable ? [Hold(), Attack(.3, .9)] : [Hold()], viable ? "" : "hold: no army or no Province"), ThreatRead.None, new(6, 99, 99, 9, 99, 99), posture, "test");

    public static readonly (string, Action)[] Tests =
    [
        ("a lock passes when the best attack is the hold, and declares when a risk-free attack is the best", () =>
        {
            Check(Route.Gate(With(Posture.Lock, false), false).Declare == false, "lock, no risk-free attack: pass");
            Check(Route.Gate(With(Posture.Lock, true), false).Declare == true, "lock, a risk-free attack: declare");
        }),
        ("a lock passes when the risk-free attack would lose a unit or spend a card: it is not nothing", () =>
        {
            var losing = new Answers(new(Attack(.1, .05, losses: 2), Hold(), [Hold(), Attack(.1, .05, losses: 2)], ""), ThreatRead.None, new(20, 2, 99, 17, 14, 99), Posture.Lock, "test");
            Check(Route.Gate(losing, false).Declare == false, $"two losses under a lock: pass ({Route.Gate(losing, false).Why})");
        }),
        ("a drain passes when the best attack is the hold", () => Check(Route.Gate(With(Posture.Drain, false), false).Declare == false, "drain: pass")),
        ("a wait passes", () => Check(Route.Gate(With(Posture.Wait, false), false).Declare == false, "wait: pass")),
        ("a race and a punish declare", () =>
        {
            Check(Route.Gate(With(Posture.Race, false), false).Declare == true, "race: declare");
            Check(Route.Gate(With(Posture.Punish, false), false).Declare == true, "punish: declare");
        }),
        ("a race or a punish with no possible attack (no army, or no group exceeds Strength even undefended) is silent", () =>
        {
            Check(Route.Gate(With(Posture.Race, false, viable: false), false).Silent, "race, no army: silent");
            Check(Route.Gate(With(Posture.Punish, false, viable: false), false).Silent, "punish, no army: silent");
        }),
        ("press, build and hold are silent", () =>
        {
            foreach (var p in new[] { Posture.Press, Posture.Build, Posture.Hold }) Check(Route.Gate(With(p, false), false).Silent, $"{p}: silent");
        }),
        ("a lethal attack declares whatever the posture", () => Check(Route.Gate(With(Posture.Drain, false), true).Declare == true, "lethal under a drain: declare")),
        ("the table drain: two Courtiers each taking 2 Honor a turn drain 4 a turn, so with the opponent at 8 Honor the drain clock reads 7 turns and the posture is Drain before any trend", () =>
        {
            var me = Side(0, [U("c1", 0, 1, abilities: [Tax2]), U("c2", 0, 1, abilities: [Tax2]), U("i6", 0, 6)], [(0, 7), (1, 7), (2, 7)], honor: 20);
            var them = Side(1, [U("t5", 1, 5)], [(0, 7), (1, 7), (2, 7), (3, 7)], honor: 8);
            Check(Math.Abs(Route.TableDrain(me.Units) - 4) < 1e-9, $"table drain 4: {Route.TableDrain(me.Units)}");
            var answers = Questions.Of(ReadOf(me, them));
            Check(answers.Clocks.MyDrain == 7, $"drain clock 7: {answers.Clocks}");
            Check(answers.Posture == Posture.Drain, $"posture Drain: {answers}");
            Check(Route.Gate(answers, false).Declare == false, $"the gate passes: {Route.Gate(answers, false).Why}");
        }),
        ("the engine is the route: two Courtiers draining 2 each against an opponent at 16 Honor (9 turns, past the race horizon) with my 12-Force army able to raze a Strength 7 Province each turn (a 4-turn military clock): the posture is Drain, not Race, while home holds (the attackers would be the engine)", () =>
        {
            var me = Side(0, [U("c1", 0, 6, abilities: [Tax2]), U("c2", 0, 6, abilities: [Tax2])], [(0, 7), (1, 7), (2, 7)], honor: 20);
            var them = Side(1, [U("t2", 1, 2)], [(0, 7), (1, 7), (2, 7), (3, 7)], honor: 16);
            var answers = Questions.Of(ReadOf(me, them));
            Check(answers.Clocks.MyDrain == 9 && answers.Clocks.MyMilitary < 9, $"drain 9, military shorter: {answers.Clocks}");
            Check(answers.Posture == Posture.Drain, $"posture Drain over the military race: {answers}");
        }),
        ("formulation 2 (rgRace): with their Honor win two turns away and my fastest clock six, a hold posture declares; without the flag it is silent; under a lock it still passes", () =>
        {
            var behind = new Answers(new(Hold(), Hold(), [Hold(), Attack(.3, .9)], ""), ThreatRead.None, new(6, 99, 99, 9, 2, 99), Posture.Hold, "test");
            Check(Route.Gate(behind, false, raceBehind: true).Declare == true, $"race behind: declare ({Route.Gate(behind, false, raceBehind: true).Why})");
            Check(Route.Gate(behind, false).Silent, "without the flag: silent");
            Check(Route.Gate(behind with { Posture = Posture.Lock }, false, raceBehind: true).Declare == false, "under a lock: pass");
        }),
    ];
}
