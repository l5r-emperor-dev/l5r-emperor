using System.Text.Json;
using L5R.AI;
using L5R.Core;

// Issue #1360 (Mendon, 2026-11-27): the Cavalry waits for the Cavalry Maneuvers Segment. Replay 185919 (Unicorn human, seat 0): on turn 9 the attacker sent four infantry units and
// has no Cavalry at home, and the human defended with two Cavalry in the Infantry segment; on turn 10 the human attacked with four Cavalry. The Contender and Conan, asked there,
// committed every Cavalry unit in the Infantry segment (the replay probe in the issue).
internal static class CavalryReserveTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("cavres: at replay 185919's Infantry defence (four attackers, no enemy Cavalry at home) the Contender defends without its two Cavalry; without cavres it sends them", InfantryDefenceHoldsCavalry),
        ("cavhold: at replay 185919's turn 10 Infantry declaration (four Cavalry at home) Hard assigns no Cavalry", ConanHoldsCavalryAttacking),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static MatchEngine Before(int index)
    {
        var replay = JsonSerializer.Deserialize<ReplayRecord>(File.ReadAllText("replays/games/practice-20261113-185919.json"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var game = MatchEngine.Setup(replay with { ContentVersion = MatchEngine.ContentVersion });
        foreach (var command in replay.Commands.Take(index)) Check(game.Execute(command).Accepted, $"the replay is refused before command {index}: {command.Type}");
        return game;
    }

    private static string[] CavalrySent(MatchEngine game, GameCommand? answer)
    {
        var self = game.GetView(0).Players[0];
        bool Cavalry(CardView c) => c.Definition?.Keywords?.Contains("Cavalry") == true && self.Home.Where(h => h.AttachedTo == c.Id && h.Definition?.Type == CardType.Follower).All(h => h.Definition?.Keywords?.Contains("Cavalry") == true);
        return answer?.Assignments?.Keys.Where(id => self.Home.FirstOrDefault(c => c.Id == id) is { } unit && Cavalry(unit)).ToArray() ?? [];
    }

    private static void InfantryDefenceHoldsCavalry()
    {
        var game = Before(96);
        Check(game.Phase == Phase.DefenseAssignment && game.GetView(0).Players[1].Army.Length > 0, $"the position moved: {game.Phase}");
        var held = HumanReplayCompare.Ask(game.Clone(), 0, BotContext.Contender(rules: [.. BotContext.TunedRules, "cavres"]), roundTrip: false);
        Check(CavalrySent(game, held).Length == 0, $"with cavres the Infantry defence should hold the Cavalry, sent {string.Join(", ", CavalrySent(game, held))}");
        var before = HumanReplayCompare.Ask(game.Clone(), 0, BotContext.Contender(), roundTrip: false);
        Check(CavalrySent(game, before).Length > 0, "without cavres the Contender defends with its Cavalry here (the habit); if it no longer does, this test no longer proves the rule");
    }

    private static void ConanHoldsCavalryAttacking()
    {
        var game = Before(130);
        Check(game.Phase == Phase.AttackDeclaration, $"the position moved: {game.Phase}");
        var bot = BotContext.Conan(rules: [.. BotContext.TunedRules, .. BotContext.HardRules, "cavhold"]); bot.Sampler = sample => game.Sample(0, sample);
        var declare = HumanReplayCompare.Ask(game, 0, bot, roundTrip: false); // the turn's attack plan is made here
        if (declare?.Type != "declareAttack") Check(game.Execute(new GameCommand("declareAttack", 0, game.Version)).Accepted, "the declaration is refused");
        else Check(game.Execute(declare).Accepted, "the bot's declaration is refused");
        Check(game.Phase == Phase.InfantryDeclaration, $"expected the Infantry declaration, got {game.Phase}");
        var answer = HumanReplayCompare.Ask(game, 0, bot, roundTrip: false);
        Check(CavalrySent(game, answer).Length == 0, $"Hard with cavhold should keep its Cavalry for the Cavalry segment, sent {string.Join(", ", CavalrySent(game, answer))} ({string.Join(" / ", bot.PlanLog)})");
    }
}
