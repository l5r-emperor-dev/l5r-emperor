using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.Core;

// Issue #1301, by Stronghold since #1335: plays across replays/index.json, and the games each Stronghold the bot's pool plays still needs for bot training.
//
// The practice client seats the human at 0 and the bot at 1. A game counts for each seat's Stronghold as the index records it. The bot's pool is the two
// presets and src/L5R.Client/Content/preloaded-decks.json, the decks BotTournament plays. Fingerprint names a list in the index.
//
// Targets (per Stronghold the pool plays), from what the bot work uses Mendon's games for (docs/bots/CONAN_STRATEGY_BRIEF.md, strategy/PLAYER_MODEL.md, README's decision test set):
// - Played by him: 5. His decisions with a Stronghold are where its rules are mined; rules taken from his decisions at real positions are the only ones that gained.
//   A Stronghold-specific habit is written as a rule only once it shows in at least 3 separate games, and its characteristic situations (its Cavalry segment,
//   its win route) arise in most games but not all, so 5 games give 3 sightings with margin. They also give the decision test set positions from every Stronghold.
// - Played by the bot against him, at Hard: 10. These measure whether the bot is a challenging opponent with the Stronghold. If the bot loses all 10, the 95%
//   upper bound on its win rate against him is 26% (exact binomial), so 10 is the fewest games that can show a Stronghold is not yet challenging; 3 or more wins
//   show it can be. Easy and Normal games show his play but not the bot's challenge.
internal static class DeckTable
{
    private const int HumanTarget = 5, BotTarget = 10;

    public static string Fingerprint(Deck deck)
    {
        var lines = deck.Entries.OrderBy(e => e.Name, StringComparer.Ordinal).Select(e => $"{e.Count} {e.Name}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))))[..12];
    }

    private sealed class Row { public int Human, HumanWon, BotHard, BotHardWon, BotOther; public readonly SortedSet<string> PoolDecks = new(StringComparer.Ordinal); }

    // Issue #1335: one row per Stronghold in the game (every printing a deck may name, experienced printings under their Stronghold's name, as the index records
    // them), grouped by clan, so the bot can be trained per Stronghold and, once a clan's Strongholds are covered, per clan. The targets apply to every
    // Stronghold the bot's pool plays; a Stronghold without a pool deck needs a deck before it can be trained.
    public static int Print(string root)
    {
        var pool = new List<(string Name, Deck Deck)> { ("Lion preset", CustomPractice.Preset("Lion")), ("Crane preset", CustomPractice.Preset("Crane")) };
        foreach (var e in JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(Path.Combine(root, "src", "L5R.Client", "Content", "preloaded-decks.json")))!)
            pool.Add((e["name"], DeckFormat.Parse(e["text"])));

        var rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        var clans = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in CardCatalog.PlayableFromDecks.Where(c => c.Type == CardType.Stronghold).GroupBy(c => c.Name))
        {
            rows[g.Key] = new Row();
            clans[g.Key] = string.Join("/", g.Select(c => c.Clan).Distinct().Order(StringComparer.Ordinal));
        }
        Row RowOf(string stronghold)
        {
            if (!rows.TryGetValue(stronghold, out var row)) { rows[stronghold] = row = new Row(); clans[stronghold] = "?"; }
            return row;
        }
        foreach (var (name, deck) in pool)
        {
            var id = CustomPractice.Review(deck).StrongholdId;
            RowOf(id is null ? $"{name} (no Stronghold)" : CardCatalog.Get(id).Name).PoolDecks.Add(name);
        }

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "replays", "index.json")));
        var games = index.RootElement.GetProperty("games").EnumerateArray().ToArray();
        foreach (var g in games)
        {
            var winner = g.TryGetProperty("winner", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : (int?)null;
            var level = g.TryGetProperty("practice", out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("botLevel", out var l) ? l.GetString() : null;
            for (var seat = 0; seat < 2; seat++)
            {
                var row = RowOf(g.GetProperty("decks")[seat].GetProperty("stronghold").GetString()!);
                if (seat == 0) { row.Human++; if (winner == 0) row.HumanWon++; }
                else if (level == "Hard") { row.BotHard++; if (winner == 1) row.BotHardWon++; }
                else row.BotOther++;
            }
        }

        (int Human, int Bot) Needed(Row row) => row.PoolDecks.Count == 0 ? (0, 0) : (Math.Max(0, HumanTarget - row.Human), Math.Max(0, BotTarget - row.BotHard));
        Console.WriteLine($"Plays by Stronghold: {games.Length} games in replays/index.json, {rows.Count} Strongholds. Targets per Stronghold the bot's pool plays: {HumanTarget} played by you, {BotTarget} played by the bot (Hard) against you.");
        Console.WriteLine();
        Console.WriteLine("| Clan | Stronghold | Bot pool decks | You played (won) | Bot played at Hard (bot won) | Bot played, other level | Needed for bot training |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        int needHuman = 0, needBot = 0;
        foreach (var (name, row) in rows.OrderBy(r => clans[r.Key], StringComparer.Ordinal).ThenBy(r => r.Key, StringComparer.Ordinal))
        {
            var (h, b) = Needed(row);
            needHuman += h; needBot += b;
            var needed = row.PoolDecks.Count == 0 ? "no pool deck yet"
                : h + b == 0 ? "none" : string.Join(", ", new[] { h > 0 ? $"{h} played by you" : null, b > 0 ? $"{b} played by the bot" : null }.Where(x => x is not null));
            var decks = row.PoolDecks.Count == 0 ? "-" : string.Join(", ", row.PoolDecks);
            Console.WriteLine($"| {clans[name]} | {name} | {decks} | {row.Human} ({row.HumanWon}) | {row.BotHard} ({row.BotHardWon}) | {row.BotOther} | {needed} |");
        }
        Console.WriteLine();
        Console.WriteLine("| Clan | Strongholds | With a pool deck | At both targets |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var clan in rows.GroupBy(r => clans[r.Key]).OrderBy(c => c.Key, StringComparer.Ordinal))
            Console.WriteLine($"| {clan.Key} | {clan.Count()} | {clan.Count(r => r.Value.PoolDecks.Count > 0)} | {clan.Count(r => r.Value.PoolDecks.Count > 0 && Needed(r.Value) == (0, 0))} |");
        var trained = rows.Values.Count(r => r.PoolDecks.Count > 0);
        Console.WriteLine();
        Console.WriteLine($"Still needed: {needHuman} games played by you and {needBot} played by the bot at Hard across the {trained} Strongholds the pool plays (one game can count for both: your Stronghold and the bot's). " +
            $"{rows.Count - trained} Strongholds have no pool deck yet. A clan is ready for clan-wide heuristics when every one of its Strongholds has a pool deck at both targets.");
        return 0;
    }
}
