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
        ("cavplan: at replay 185919's turn 10 Cavalry declaration (the defence placed blind) every army the Contender sends takes its Province with a margin, and it takes more Provinces than without cavplan", CavalryTakesProvinces),
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

    // The Provinces an assignment takes on Force: every battlefield whose attackers exceed its Strength, the defenders already there, the defender's Cavalry still at home
    // and the margin (one per card in the defender's hand, at most four).
    private static (int Taken, int Battlefields, string Detail) Takes(MatchEngine game, GameCommand? answer)
    {
        var view = game.GetView(0); var self = view.Players[0]; var them = view.Players[1];
        int Force(IEnumerable<CardView> cards) => cards.Where(c => !c.Bowed && c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c => Math.Max(0, c.Force));
        bool Cavalry(PlayerView p, CardView c) => c.Definition?.Keywords?.Contains("Cavalry") == true && p.Home.Where(h => h.AttachedTo == c.Id && h.Definition?.Type == CardType.Follower).All(h => h.Definition?.Keywords?.Contains("Cavalry") == true);
        var reply = them.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && Cavalry(them, c)).Sum(c => Force(them.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id)));
        var margin = Math.Min(them.HandCount, 4);
        var armies = (answer?.Assignments ?? []).GroupBy(a => a.Value).Select(g => (Province: g.Key, Force: Force(self.Home.Where(h => g.Any(a => a.Key == h.Id || a.Key == h.AttachedTo))))).ToArray();
        var taken = armies.Count(a => a.Force > them.Provinces.Single(p => p.Index == a.Province).Strength + Force(them.Army.Where(c => c.ProvinceIndex == a.Province)) + reply + margin);
        return (taken, armies.Length, string.Join(", ", armies.Select(a => $"P{a.Province + 1} {a.Force}F vs {them.Provinces.Single(p => p.Index == a.Province).Strength}+{Force(them.Army.Where(c => c.ProvinceIndex == a.Province))}")));
    }

    private static void CavalryTakesProvinces()
    {
        var game = Before(130);
        Check(game.Phase == Phase.AttackDeclaration, $"the position moved: {game.Phase}");
        Check(game.Execute(new GameCommand("declareAttack", 0, game.Version)).Accepted, "the declaration is refused");
        var rules = (string[])[.. BotContext.TunedRules, "cavhold", "cavres"];
        var infantry = HumanReplayCompare.Ask(game, 0, BotContext.Contender(rules: [.. rules, "cavplan"]), roundTrip: false);
        Check(game.Execute(infantry ?? new GameCommand("pass", 0, game.Version)).Accepted, "the Infantry declaration is refused");
        var defence = HumanReplayCompare.Ask(game, 1, BotContext.Contender(rules: rules), roundTrip: false);
        Check(game.Execute(defence ?? new GameCommand("pass", 1, game.Version)).Accepted, "the Infantry defence is refused");
        Check(game.Phase == Phase.CavalryDeclaration, $"expected the Cavalry declaration, got {game.Phase}");
        var with = Takes(game, HumanReplayCompare.Ask(game.Clone(), 0, BotContext.Contender(rules: [.. rules, "cavplan"]), roundTrip: false));
        var without = Takes(game, HumanReplayCompare.Ask(game.Clone(), 0, BotContext.Contender(rules: rules), roundTrip: false));
        Check(with.Battlefields > 0 && with.Taken == with.Battlefields, $"with cavplan every army should take its Province with the margin: {with.Detail}");
        Check(with.Taken > without.Taken, $"cavplan should take more Provinces here than without it: with {with.Detail}; without {without.Detail}");
    }
}
