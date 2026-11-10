using System.Text.Json;
using L5R.Core;

// Issue #1206: MatchEngine.Sample redeals only what its viewer cannot see. Along seeded random games, each seat's view of every sample is exactly its view of the real
// match (so a bot searching over samples learns nothing hidden), the cards themselves are only moved between the hidden places of one owner, and the samples do differ
// from the match where the other seat's hand is concerned.
internal static class HiddenSampleTests
{
    public static void Run(TestRunner runner) => runner.Run("hidden-sample", [
        ("A sample shows its viewer exactly the real match's view, keeps each owner's cards, and redeals the other hand (seeds 5, 29)", () => { foreach (var s in new ulong[] { 5, 29 }) Walk(s); }),
    ]);

    private static void Walk(ulong seed)
    {
        var game = MatchEngine.CreatePublishedMatch(seed);
        var rng = new Random((int)seed);
        var redealtHands = 0;
        for (var n = 0; n < 300 && game.WinnerId is null; n++)
        {
            if (n % 10 == 0)
                for (var viewer = 0; viewer < 2; viewer++)
                {
                    var sample = game.Sample(viewer, (ulong)(n * 2 + viewer));
                    if (Json(sample.GetView(viewer)) != Json(game.GetView(viewer))) throw new Exception($"Seat {viewer}'s view of a sample differs from its view of the match (seed {seed}, step {n}).");
                    if (Owned(sample.State) != Owned(game.State)) throw new Exception($"A sample moved a card to another owner or invented one (seed {seed}, step {n}).");
                    if (Json(sample.GetView(1 - viewer).Players[1 - viewer].Hand) != Json(game.GetView(1 - viewer).Players[1 - viewer].Hand)) redealtHands++;
                }
            var state = game.State;
            var waiting = state.PendingChoice is { } pending ? pending.ActingPlayerId ?? pending.PlayerId : state.PriorityPlayerId;
            var options = game.QueryActions(waiting).Where(a => a.Enabled && a.Command is not null && a.Type != "concede").ToList();
            if (options.Count == 0) break;
            var active = options.Where(o => o.Type != "pass").ToList();
            var pool = active.Count > 0 && rng.Next(4) > 0 ? active : options;
            var choice = pool[rng.Next(pool.Count)];
            if (!game.Execute(choice.Command!).Accepted) break;
        }
        if (redealtHands == 0) throw new Exception($"No sample ever redealt the other seat's hand (seed {seed}): the sampler hides nothing.");
    }

    private static string Owned(MatchState state) => string.Join(";", state.Cards.Values.GroupBy(c => c.OwnerId).OrderBy(g => g.Key).Select(g => g.Key + ":" + string.Join(",", g.Select(c => c.DefinitionId).Order(StringComparer.Ordinal))));

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
}
