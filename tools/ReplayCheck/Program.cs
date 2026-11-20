using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using L5R.Core;

// Issue #1262, stage 2 of the replay checks: every replay under replays/games is rebuilt by the engine from its seed and its two decks (deck legality exactly as practice
// checks it) and every logged command is executed; a refused command is a finding with its number and the engine's reason. The facts the engine reaches (commands, turns,
// winner, the decks' strongholds and fingerprints) must match replays/index.json. Only games the current rules accept are kept: there is no exemption list. Stage 1 (tools/replays/check_replays.py) has already proved each file a plain export.
//
//   dotnet run -c Release --project tools/ReplayCheck                 check every replay against the engine and the index
//   dotnet run -c Release --project tools/ReplayCheck -- --write-index    add new replays to the index and refresh its facts (notes and status are kept)
//   dotnet run -c Release --project tools/ReplayCheck -- --decks          print the plays-by-deck table and the games still needed for bot training (reads the index only)
//   (run from the repository root, or pass --root DIR)

var root = Directory.GetCurrentDirectory(); var write = false; var deckTable = false;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--root" && i + 1 < args.Length) root = args[++i];
    else if (args[i] == "--write-index") write = true;
    else if (args[i] == "--decks") deckTable = true;
    else { Console.Error.WriteLine($"unknown argument {args[i]}"); return 2; }
}
var games = Path.Combine(root, "replays", "games"); var indexPath = Path.Combine(root, "replays", "index.json");
var findings = new List<string>();
if (deckTable) return DeckTable.Print(root);

// The stage 1 schema must name exactly the engine's fields, so neither stage can drift from the export format.
var stage1 = File.ReadAllText(Path.Combine(root, "tools", "replays", "check_replays.py"));
SchemaMatches("COMMAND", typeof(GameCommand)); SchemaMatches("SELECTION", typeof(SlotSelection)); SchemaMatches("TOP", typeof(ReplayRecord)); SchemaMatches("PRACTICE", typeof(PracticeLabels));

var strict = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
var index = File.Exists(indexPath) ? JsonSerializer.Deserialize<ReplayIndex>(File.ReadAllText(indexPath), strict) ?? new ReplayIndex(1, [], []) : new ReplayIndex(1, [], []);
var listed = index.Games.ToDictionary(g => g.File, StringComparer.Ordinal);
var rows = new List<IndexEntry>();
foreach (var path in Directory.GetFiles(games, "*.json").OrderBy(p => p, StringComparer.Ordinal))
{
    var name = Path.GetFileName(path); var bytes = File.ReadAllBytes(path);
    listed.TryGetValue(name, out var entry);
    var facts = Check(name, bytes, out var problem);
    if (problem is not null) findings.Add($"{name}: {problem} (a game the current rules refuse is retired: move it out of replays/games and add it to the index's retired list with the reason)");
    var row = new IndexEntry(name, Convert.ToHexStringLower(SHA256.HashData(bytes)), facts?.Commands ?? entry?.Commands ?? 0, facts?.Turns ?? entry?.Turns ?? 0, facts is null ? entry?.Winner : facts.Winner,
        facts?.Decks ?? entry?.Decks ?? [], entry?.Note ?? "", facts?.Practice ?? entry?.Practice, facts?.Digest ?? entry?.Digest ?? false);
    if (entry is null && !write) findings.Add($"{name}: not in replays/index.json (run with --write-index)");
    else if (entry is not null && !write && facts is not null && !Same(entry, row)) findings.Add($"{name}: the index's facts differ from the engine's ({Facts(entry)} listed, {Facts(row)} replayed); run with --write-index");
    rows.Add(row);
    Console.WriteLine($"  {(problem is null ? "ok  " : "FAIL")} {name}: {Facts(row)}");
}
foreach (var gone in listed.Keys.Except(rows.Select(r => r.File), StringComparer.Ordinal)) findings.Add($"replays/index.json lists {gone}, which is not in replays/games (a retired game belongs in the retired list)");
foreach (var back in index.Retired.Where(r => File.Exists(Path.Combine(games, r.File)))) findings.Add($"{back.File} is in replays/games and in the retired list");
if (write)
{
    var json = JsonSerializer.Serialize(new ReplayIndex(1, rows.ToArray(), index.Retired), new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    File.WriteAllText(indexPath, json.Replace("\r\n", "\n") + "\n");
    Console.WriteLine($"wrote replays/index.json ({rows.Count} games)");
}
foreach (var f in findings) Console.WriteLine(f);
Console.WriteLine($"{rows.Count} replay(s) rebuilt by engine content {MatchEngine.ContentVersion}: {(findings.Count == 0 ? "every card and every move legal" : $"{findings.Count} finding(s)")}");
return findings.Count == 0 ? 0 : 1;

ReplayFacts? Check(string name, byte[] bytes, out string? problem)
{
    problem = null;
    ReplayRecord replay;
    try { replay = JsonSerializer.Deserialize<ReplayRecord>(bytes, strict) ?? throw new JsonException("empty"); }
    catch (JsonException e) { problem = $"not a replay record: {e.Message}"; return null; }
    if (replay.ContentVersion != MatchEngine.ContentVersion) { problem = $"recorded with content {replay.ContentVersion}; this engine is {MatchEngine.ContentVersion}"; return null; }
    MatchEngine game;
    try { game = MatchEngine.Setup(replay); } // the match exactly as recorded: seed, both decks reviewed as practice reviews them, idle battle passes
    catch (ArgumentException e) { problem = $"the match cannot be set up: {e.Message}"; return null; }
    for (var i = 0; i < replay.Commands.Length; i++)
    {
        var c = replay.Commands[i];
        var result = game.Execute(c);
        if (!result.Accepted) { problem = $"command {i} ({c.Type}, seat {c.PlayerId}, version {c.ExpectedVersion}) refused: {result.Error}"; break; } // the facts then describe the game as far as it replays
    }
    // issue #1266: an export that carries its final state's digest must reach it, so a game that diverges under today's rules while every command is still accepted is caught
    if (problem is null && replay.FinalDigest is { } digest && digest != game.Digest()) problem = $"every command is accepted but the game ends in a different state than recorded ({digest} recorded, {game.Digest()} replayed)";
    var decks = (replay.CustomDecks ?? []).Select((d, seat) => Deck(d, game, seat)).ToArray();
    return new ReplayFacts(replay.Commands.Length, game.TurnNumber, problem is null ? game.WinnerId : null, decks, replay.Practice, replay.FinalDigest is not null);
}

// a deck's stronghold as the engine set it up (the printing the deck names, experienced or not), its size and a fingerprint of its list
DeckFacts Deck(Deck deck, MatchEngine game, int seat)
{
    var stronghold = game.State.Cards.Values.FirstOrDefault(c => c.OwnerId == seat && c.Zone == Zone.Stronghold) is { } card ? CardCatalog.Get(card.DefinitionId).Name : "?";
    return new DeckFacts(stronghold, deck.Entries.Sum(e => e.Count), DeckTable.Fingerprint(deck));
}

bool Same(IndexEntry a, IndexEntry b) => a.Sha256 == b.Sha256 && a.Commands == b.Commands && a.Turns == b.Turns && a.Winner == b.Winner && a.Decks.SequenceEqual(b.Decks) && a.Practice == b.Practice && a.Digest == b.Digest;
string Facts(IndexEntry e) => $"{(e.Practice is { } p ? $"{p.YourDeck} v {p.OpponentDeck} ({p.BotLevel}), " : "")}{(e.Digest ? "final state verified, " : "")}{e.Commands} commands, turn {e.Turns}, {(e.Winner is { } w ? $"seat {w} won" : "unfinished")}, {string.Join(" v ", e.Decks.Select(d => $"{d.Stronghold} ({d.Cards}, {d.Fingerprint})"))}";

void SchemaMatches(string table, Type type)
{
    var block = Regex.Match(stage1, $@"^{table} = \{{(.*?)\}}", RegexOptions.Multiline | RegexOptions.Singleline);
    if (!block.Success) { findings.Add($"tools/replays/check_replays.py: no {table} table"); return; }
    var python = Regex.Matches(block.Groups[1].Value, @"'([A-Za-z]+)':").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    var engine = type.GetProperties().Where(p => p.DeclaringType == type && p.Name != "EqualityContract").Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).ToHashSet(StringComparer.Ordinal);
    if (!python.SetEquals(engine)) findings.Add($"tools/replays/check_replays.py {table} names {string.Join(", ", python.Except(engine))} beyond {type.Name} and misses {string.Join(", ", engine.Except(python))}");
}

record DeckFacts(string Stronghold, int Cards, string Fingerprint);
record ReplayFacts(int Commands, int Turns, int? Winner, DeckFacts[] Decks, PracticeLabels? Practice, bool Digest);
record IndexEntry(string File, string Sha256, int Commands, int Turns, int? Winner, DeckFacts[] Decks, string Note, PracticeLabels? Practice = null, bool Digest = false);
/// <summary>A game taken out of replays/games because the current rules refuse it: the file's last commit keeps it; the reason says which change.</summary>
record RetiredEntry(string File, string Sha256, string Reason);
record ReplayIndex(int Version, IndexEntry[] Games, RetiredEntry[] Retired);
