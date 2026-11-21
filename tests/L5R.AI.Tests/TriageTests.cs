using L5R.AI;
using L5R.Core;

// Issue #1185, slice 3: ability triage. See TrainingOpponent.Triage.cs.
internal static class TriageTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Triage (issue #1185): a Tactician that has not used its tactical ability outranks a plain unit and a spent Tactician as a bow target; the champion picks the biggest body", TargetOrder),
        ("Triage (issue #1185): no Gold is spent on a battle that is comfortably won against units with nothing left to spring, a free printed ability still is, and an unspent Tactician keeps the spending on", NoWaste),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static CardDefinition Plain(int force) => CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force == force && (d.Abilities?.Length ?? 0) == 0 && !(d.Keywords?.Contains("Tactician") ?? false));
    private static CardView Tactician(string id, string name, int force) =>
        new(id, Plain(force) with { Name = name, Keywords = ["Tactician"] }, 1, Zone.Battlefield, true, false, 0, null, force);

    private static MatchView Fight(int own, int enemyMain, int strength, params CardView[] enemyExtra)
    {
        var v = BattlePolicyTests.Battle(own, enemyMain, strength, true, null, enemyExtra, enemyHand: 5);
        var plain = new CardView("enemy-main", Plain(enemyMain), 1, Zone.Battlefield, true, false, 0, null, enemyMain);
        return v with { Players = [v.Players[0], v.Players[1] with { Army = [plain, .. enemyExtra] }] };
    }

    private static string BowTarget(MatchView v, BotContext memory)
    {
        var bow = BattlePolicyTests.Use(v, "training-bow-at-battle", "own-main", null, ["enemy-main", "t1", "t2"]);
        return TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), bow], memory).Command?.TargetId ?? "none";
    }

    private static void TargetOrder()
    {
        var v = Fight(6, 4, 10, Tactician("t1", "Tactician One", 2), Tactician("t2", "Tactician Two", 2));
        // The enemy has already used Tactician One's tactical ability this battle: the public action list says so.
        var spent = new BattleActionView(1, "Action", 1, "Enemy", null, "tactical-advantage", "Tactical Advantage", "", "Tactician One", [], []);
        var seen = v with { BattleActions = [spent] };
        Check(BowTarget(seen, new BotContext()) == "enemy-main", "the champion bows the biggest body");
        var triage = new BotContext { UsePlannerTriage = true };
        var picked = BowTarget(seen, triage);
        Check(picked == "t2", "an unspent Tactician (Force 2 + up to 4 while they hold cards) is worth more than a plain Force 4; the spent one is not: " + picked);
        Check(triage.SpentThisTurn(seen.TurnNumber, "Tactician One", "tactical-advantage") && !triage.SpentThisTurn(seen.TurnNumber, "Tactician Two", "tactical-advantage"), "the bot remembers who has used the ability this turn");
        // A new turn forgets it.
        triage.NoteBattleActions(seen with { TurnNumber = seen.TurnNumber + 1, BattleActions = [new BattleActionView(1, "Pass", 1, "Enemy", null, "pass", "Pass", "", null, [], [])] });
        Check(!triage.SpentThisTurn(seen.TurnNumber + 1, "Tactician One", "tactical-advantage"), "what was spent last turn is spent no more");
    }

    private static void NoWaste()
    {
        var costly = new GoldQuote(2, "Printed", "", null, []);
        string Chose(MatchView v, bool free, BotContext memory)
        {
            var bow = BattlePolicyTests.Use(v, "training-bow-at-battle", "own-main", null, ["enemy-main", "t1"]);
            if (!free) bow = bow with { Gold = costly };
            return TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), bow], memory).Command?.Type ?? "none";
        }
        var won = Fight(20, 4, 5); // 20 against 4 with Strength 5: won by a wide margin
        var triage = () => new BotContext { UsePlannerTriage = true };
        Check(Chose(won, free: false, triage()) == "pass", "a costly bow on a won battle against units with nothing to spring is not played");
        Check(Chose(won, free: true, triage()) == "useAbility", "the same bow for no Gold is played: it costs nothing");
        var springing = Fight(20, 4, 5, Tactician("t1", "Tactician One", 2));
        Check(Chose(springing, free: false, triage()) == "useAbility", "an unspent Tactician with cards in their hand can still spring something: the bow is played");
    }
}
