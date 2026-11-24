using System.Text.Json;
using L5R.AI;
using L5R.Core;

// Issue #1336 (replay 115409, Unicorn human vs Scorpion Hard, turn 15): the bot had two Provinces left and 28 Force at home, the attacker only Cavalry. Its last stand chose to defend,
// but Hard's search passed, because in its playouts the attacking Contender counted the whole home army as defenders after the defense had closed and never attacked.
internal static class CavalryOpenDefenceTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("cavopen: at replay 115409's Cavalry declaration (the defender passed, 28 Force at home, no Cavalry) the Contender attacks; the T2 yardstick, counting the closed defence, passes", CavalryAttacksOpenBoard),
        ("Hard at replay 115409's last stand (Cavalry can take both Provinces left) defends instead of passing, with cavopen alone and with lkStand alone", HardDefendsLastStand),
    ];

    private const int DefenseIndex = 215; // the bot's DefenseAssignment pass on turn 15

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static (MatchEngine Game, ReplayRecord Replay) Before(int index)
    {
        var replay = JsonSerializer.Deserialize<ReplayRecord>(File.ReadAllText("replays/games/practice-20261124-115409.json"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var game = MatchEngine.Setup(replay with { ContentVersion = MatchEngine.ContentVersion });
        foreach (var command in replay.Commands.Take(index)) Check(game.Execute(command).Accepted, $"the replay is refused before command {index}: {command.Type}");
        return (game, replay);
    }

    private static void CavalryAttacksOpenBoard()
    {
        var (game, replay) = Before(DefenseIndex);
        Check(game.Phase == Phase.DefenseAssignment && replay.Commands[DefenseIndex].Type == "pass", $"the position moved: {game.Phase} / {replay.Commands[DefenseIndex].Type}");
        Check(game.Execute(replay.Commands[DefenseIndex]).Accepted && game.Phase == Phase.CavalryDeclaration, $"expected the Cavalry declaration after the pass, got {game.Phase}");
        string? Choice(BotContext bot) => HumanReplayCompare.Ask(game.Clone(), 0, bot, roundTrip: false)?.Type;
        Check(Choice(BotContext.Contender()) == "attack", $"the practice Contender should attack the undefended Provinces, chose {Choice(BotContext.Contender())}");
        Check(Choice(BotContext.Contender(rules: BotContext.T2Rules)) != "attack", "without cavopen the yardstick passes here (the bug); if it now attacks, this test no longer proves the fix");
    }

    private static void HardDefendsLastStand()
    {
        var (game, _) = Before(DefenseIndex);
        // Either fix alone saves the last stand: cavopen makes the playouts see the Cavalry take both Provinces, lkStand keeps the pass out of the search. Without both, Hard passed.
        foreach (var (arm, rules) in new[] { ("cavopen alone", BotContext.TunedRules.Concat(["lkDef", "lkVar", "lkPlan"])), ("lkStand alone", BotContext.T2Rules.Concat(BotContext.HardRules)) })
        {
            var bot = BotContext.Conan(rules: rules.ToArray()); bot.Sampler = sample => game.Sample(1, sample);
            var answer = HumanReplayCompare.Ask(game, 1, bot, roundTrip: false);
            Check(answer?.Type == "assignDefense", $"{arm}: Hard should make its last stand, chose {answer?.Type ?? "nothing"} ({string.Join(" / ", bot.LookLog)})");
        }
    }
}
