using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.Core;

// Issue #1301: plays by deck across replays/index.json, and the games each deck of the bot's pool still needs for bot training.
//
// The practice client seats the human at 0 and the bot at 1. A deck is named by its list's fingerprint: the bot's pool first (the two presets and
// src/L5R.Client/Content/preloaded-decks.json, the decks BotTournament plays), then the deck names an export recorded in `practice`, then the stronghold.
//
// Targets (per pool deck), from what the bot work uses Mendon's games for (docs/bots/CONAN_STRATEGY_BRIEF.md, strategy/PLAYER_MODEL.md, README's decision test set):
// - Played by him: 5. His decisions with a deck are where its rules are mined; rules taken from his decisions at real positions are the only ones that gained.
//   A deck-specific habit is written as a rule only once it shows in at least 3 separate games, and a deck's characteristic situations (its Cavalry segment,
//   its win route) arise in most games but not all, so 5 games give 3 sightings with margin. They also give the decision test set positions from every deck.
// - Played by the bot against him, at Hard: 10. These measure whether the bot is a challenging opponent with the deck. If the bot loses all 10, the 95%
//   upper bound on its win rate against him is 26% (exact binomial), so 10 is the fewest games that can show a deck is not yet challenging; 3 or more wins
//   show it can be. Easy and Normal games show his play but not the bot's challenge.
internal static class DeckTable
{
    private const int HumanTarget = 5, BotTarget = 10;

    public static string Fingerprint(Deck deck)
    {
        var lines = deck.Entries.OrderBy(e => e.Name, StringComparer.Ordinal).Select(e => $"{e.Count} {e.Name}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))))[..12];
    }

    private sealed class Row { public int Human, HumanWon, BotHard, BotHardWon, BotOther; }

    public static int Print(string root)
    {
        var pool = new List<(string Name, Deck Deck)> { ("Lion preset", CustomPractice.Preset("Lion")), ("Crane preset", CustomPractice.Preset("Crane")) };
        foreach (var e in JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(Path.Combine(root, "src", "L5R.Client", "Content", "preloaded-decks.json")))!)
            pool.Add((e["name"], DeckFormat.Parse(e["text"])));
        var poolNames = pool.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, deck) in pool) names.TryAdd(Fingerprint(deck), name);

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "replays", "index.json")));
        var games = index.RootElement.GetProperty("games").EnumerateArray().ToArray();
        string? Label(JsonElement g, int seat) => g.TryGetProperty("practice", out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty(seat == 0 ? "yourDeck" : "opponentDeck", out var n) ? n.GetString() : null;
        foreach (var g in games) // the names exports recorded, for lists outside the pool (an older export of the same list gets the same name)
            for (var seat = 0; seat < 2; seat++)
                if (Label(g, seat) is { Length: > 0 } label) names.TryAdd(g.GetProperty("decks")[seat].GetProperty("fingerprint").GetString()!, label);

        var rows = new SortedDictionary<string, Row>(StringComparer.Ordinal);
        foreach (var name in poolNames) rows[name] = new Row();
        foreach (var g in games)
        {
            var winner = g.TryGetProperty("winner", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : (int?)null;
            var level = g.TryGetProperty("practice", out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("botLevel", out var l) ? l.GetString() : null;
            for (var seat = 0; seat < 2; seat++)
            {
                var d = g.GetProperty("decks")[seat];
                var fp = d.GetProperty("fingerprint").GetString()!;
                var name = names.TryGetValue(fp, out var n) ? n : $"{d.GetProperty("stronghold").GetString()} list {fp}";
                if (!rows.TryGetValue(name, out var row)) rows[name] = row = new Row();
                if (seat == 0) { row.Human++; if (winner == 0) row.HumanWon++; }
                else if (level == "Hard") { row.BotHard++; if (winner == 1) row.BotHardWon++; }
                else row.BotOther++;
            }
        }

        Console.WriteLine($"Plays by deck: {games.Length} games in replays/index.json. Targets per bot-pool deck: {HumanTarget} played by you, {BotTarget} played by the bot (Hard) against you.");
        Console.WriteLine();
        Console.WriteLine("| Deck | In bot pool | You played (won) | Bot played at Hard (bot won) | Bot played, other level | Needed for bot training |");
        Console.WriteLine("|---|---|---|---|---|---|");
        int needHuman = 0, needBot = 0;
        foreach (var (name, row) in rows.OrderByDescending(r => poolNames.Contains(r.Key)).ThenBy(r => r.Key, StringComparer.Ordinal))
        {
            var inPool = poolNames.Contains(name);
            string needed;
            if (!inPool) needed = "none (not in the bot's pool)";
            else
            {
                int h = Math.Max(0, HumanTarget - row.Human), b = Math.Max(0, BotTarget - row.BotHard);
                needHuman += h; needBot += b;
                needed = h + b == 0 ? "none" : string.Join(", ", new[] { h > 0 ? $"{h} played by you" : null, b > 0 ? $"{b} played by the bot" : null }.Where(x => x is not null));
            }
            Console.WriteLine($"| {name} | {(inPool ? "yes" : "no")} | {row.Human} ({row.HumanWon}) | {row.BotHard} ({row.BotHardWon}) | {row.BotOther} | {needed} |");
        }
        Console.WriteLine();
        Console.WriteLine($"Still needed: {needHuman} games played by you and {needBot} played by the bot at Hard across {poolNames.Count} pool decks (one game can count for both: your deck and the bot's).");
        return 0;
    }
}
