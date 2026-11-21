using L5R.AI;
using L5R.Core;

// Issue #1214: the practice difficulty. Easy is the Champion, Moderate the Contender, Hard Conan. The client gives Hard a sampler made from a private copy of the match, because it decides off the UI
// thread while the real match goes on; these cases pin the mapping and prove that a Hard opponent on such a copy really searches, plays only legal moves and never touches the real match.
internal static class BotLevelTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Difficulty levels (issue #1214): Easy is the Champion, Moderate the Contender, Hard Conan with the best-measured searches; only Hard needs a sampler", Mapping),
        ("Hard (issue #1214): decides on a private copy of the match, searches its Battle decisions on redealt worlds, plays legal moves, and leaves the real match as it was", HardOnACopy),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static void Mapping()
    {
        var easy = BotLevel.Easy.NewMemory(); var moderate = BotLevel.Moderate.NewMemory(); var hard = BotLevel.Hard.NewMemory();
        Check(!easy.UsePlanner && easy.Rules.Count == 0, "Easy is the Champion: no planner, no tuned rules");
        Check(moderate.UsePlanner && moderate.Rules.SetEquals(BotContext.TunedRules) && !moderate.Has("look"), "Moderate is the Contender: the planner and the tuned rules, no search");
        Check(hard.UsePlanner && BotContext.TunedRules.All(hard.Has) && hard.Has("look") && BotContext.HardRules.All(hard.Has), "Hard is Conan: the Contender's rules, the search, and the defence, shape and plan searches");
        Check(!hard.Has("lkAtk") && !hard.Has("lkVal") && !hard.Has("lkFar") && !hard.Has("lkBuy"), "the searches that measured worse stay off");
        Check(new[] { BotLevel.Easy, BotLevel.Moderate, BotLevel.Hard }.Select(l => l.NeedsSampler()).SequenceEqual([false, false, true]), "only Hard needs a sampler");
        Check(BotLevels.Parse("Hard") == BotLevel.Hard && BotLevels.Parse("moderate") == BotLevel.Moderate && BotLevels.Parse("") == BotLevel.Easy && BotLevels.Parse("9") == BotLevel.Easy, "a saved level is read back; anything else is Easy");
    }

    private static void HardOnACopy()
    {
        var game = MatchEngine.CreateCustomMatch(20261111, CustomPractice.Preset("Lion"), CustomPractice.Preset("Crane")); game.AllowIdleBattlePasses();
        BotContext[] bots = [BotLevel.Easy.NewMemory(), BotLevel.Hard.NewMemory()];
        var searches = 0; var steps = 0;
        while (game.WinnerId is null && steps++ < 4000 && searches < 3)
        {
            var seat = game.PriorityPlayerId; var bot = bots[seat];
            for (var attempt = 0; ; attempt++)
            {
                if (attempt == 12) throw new Exception($"seat {seat} was refused twelve times at version {game.Version}");
                var copy = game.Clone(); var view = copy.GetView(seat); var actions = copy.QueryActions(seat); var before = game.Version;
                if (seat == 1) bot.Sampler = s => copy.Sample(seat, s);
                var logged = bot.LookLog.Count;
                var command = view.PendingDuel is not null ? DuelOpponent.Choose(view, actions) : TrainingOpponent.Choose(view, actions, bot).Command;
                Check(game.Version == before, "choosing on a copy never changes the real match");
                Check(command is not null, $"seat {seat} had no move at version {before}");
                searches += bot.LookLog.Count - logged;
                var result = game.Execute(command!);
                if (result.Accepted) break;
                bot.NoteRejection(view.Version, command!, result.Error);
            }
        }
        Check(searches >= 3 || game.WinnerId is not null, $"Hard never searched in {steps} decisions");
        Check(bots[1].LookLog.Count >= 1 && bots[0].LookLog.Count == 0, "only the Hard seat logs lookahead searches");
    }
}
