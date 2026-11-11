using System.Text;
using System.Text.Json;
using L5R.AI;
using L5R.Core;

// Issue #1185: compare a human's moves with a bot's at every human decision of an exported practice replay (the HUD menu's "Export practice replay").
// The practice client puts the human in seat 0 and the Champion (a fresh `new BotContext()`, no Archetype) in seat 1 (Main.cs, MatchSession.StepPracticeOpponent).
// The match is rebuilt from the replay and stepped command by command. Before each seat-0 command the comparison bot is asked exactly as BotTournament asks a bot
// (JSON-round-tripped seat-0 view and actions, one persistent memory with the deck's Archetype, an engine refusal noted and the bot asked again) and its answer is
// written beside the human's; then the human's logged command is executed. Opt-in:
//   L5R_HUMAN_REPLAY=<practice-....json>   the exported replay (required)
//   L5R_HUMAN_OUT=<path>                   the log (default artifacts/bots/human/<replay name>.compare.log)
//   L5R_HUMAN_BOT=contender|<arm>          the comparison bot for seat 0 (default contender; any BotTournament arm, e.g. champion)
//   L5R_HUMAN_CHECKCHAMP=1                 also check that a fresh Champion in seat 1 picks each logged seat-1 command (a stepping sanity check)
//   L5R_HUMAN_PROBE=<i,j,...>              before each of these (0-based) commands, whichever seat acts: list its offered options and the Force of each unit in play, and ask a
//                                          fresh L5R_HUMAN_PROBE_BOT (default conan, rules from L5R_C2) with a Sampler for that seat, logging its lookahead (issue #1216)
//   L5R_HUMAN_SYNTH=<path.json>            instead write a synthetic replay: the Champion at both seats, driven as the practice client drives the opponent, for testing the tool
//                                          (L5R_HUMAN_SYNTH_DECKS="Classic Crane|Spider Conquer", L5R_HUMAN_SYNTH_SEED=<n>)
//   dotnet run -c Release --project tests/L5R.AI.Tests -- --filter human-compare
internal static class HumanReplayCompare
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true }; // the client's own replay serialiser (MatchSession.ExportPracticeReplay)
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    public static readonly (string, Action)[] Tests =
    [
        ("human-compare: a human's moves in an exported practice replay beside the bot's at each decision (opt-in, set L5R_HUMAN_REPLAY; see HumanReplayCompare.cs)", () =>
        {
            if (Env("L5R_HUMAN_SYNTH") is { } synth) { Synthesise(synth, Env("L5R_HUMAN_SYNTH_DECKS") ?? "Classic Crane|Spider Conquer", ulong.TryParse(Env("L5R_HUMAN_SYNTH_SEED"), out var s) ? s : 1_000_123UL); return; }
            if (Env("L5R_HUMAN_REPLAY") is not { } path) { Console.WriteLine("  SKIP: set L5R_HUMAN_REPLAY to an exported practice replay"); return; }
            var output = Env("L5R_HUMAN_OUT") ?? Path.Combine("artifacts", "bots", "human", Path.GetFileNameWithoutExtension(path) + ".compare.log");
            Console.WriteLine(Compare(path, output, Env("L5R_HUMAN_BOT") ?? "contender", Env("L5R_HUMAN_CHECKCHAMP") == "1"));
        })
    ];

    /// <summary>The identity of a command for comparison: everything but the version and the Gold sources paid with, with list fields in a fixed order.</summary>
    public static string Key(GameCommand c) => JsonSerializer.Serialize(c with
    {
        ExpectedVersion = 0,
        SourceIds = null,
        PaymentDiscards = null,
        CardIds = c.CardIds?.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        Provinces = c.Provinces?.Order().ToArray(),
        Assignments = c.Assignments?.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value)
    });

    /// <summary>The bot's answer at this position, as the tournament and the practice client get it: a refusal is noted in the bot's memory and the bot asked again (tested on a copy of the match).</summary>
    private static GameCommand? Ask(MatchEngine game, int seat, BotContext memory, bool roundTrip)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var view = roundTrip ? RoundTrip(game.GetView(seat)) : game.GetView(seat);
            var actions = roundTrip ? RoundTrip(game.QueryActions(seat)) : game.QueryActions(seat);
            var command = view.PendingDuel is not null ? DuelOpponent.Choose(view, actions) : TrainingOpponent.Choose(view, actions, memory).Command; // Main.cs: the duel screen has its own chooser
            if (command is null) return null;
            var trial = game.Clone().Execute(command);
            if (trial.Accepted) return command;
            memory.NoteRejection(view.Version, command, trial.Error);
        }
        return null;
    }

    public static string Compare(string replayPath, string outPath, string botArm, bool checkChampion)
    {
        if (!BotTournament.Arms.TryGetValue(botArm, out var maker)) throw new Exception($"unknown bot '{botArm}'; known: {string.Join(", ", BotTournament.Arms.Keys)}");
        var replay = JsonSerializer.Deserialize<ReplayRecord>(File.ReadAllText(replayPath), Web) ?? throw new Exception("not a replay");
        var notes = new List<string>();
        if (replay.ContentVersion != MatchEngine.ContentVersion) notes.Add($"replay content {replay.ContentVersion}, engine {MatchEngine.ContentVersion}: rebuilt anyway, stops at the first refused command");
        var game = MatchEngine.Replay(replay with { ContentVersion = MatchEngine.ContentVersion, Commands = [] }); // the match exactly as MatchEngine.Replay sets it up (custom decks, idle battle passes)
        var bot = maker(); var champion = new BotContext();
        if (replay.CustomDecks is { Length: 2 } decks) bot.Archetype = DeckArchetypeTests.Of(decks[0]);

        var log = new StringBuilder(); long lastEvent = -1; var lastTurn = -1;
        int decisions = 0, choices = 0, diffs = 0, botNone = 0, champChecked = 0, champMismatch = 0, executed = 0;
        var byPhase = new SortedDictionary<string, int[]>(StringComparer.Ordinal); // phase -> [decisions, choices, diffs]
        var byType = new SortedDictionary<string, int[]>(StringComparer.Ordinal); // the human's command type -> [choices, diffs]
        var champLines = new List<string>();
        string Name(string? id) => id is not null && game.State.Cards.TryGetValue(id, out var ci) && CardCatalog.Cards.TryGetValue(ci.DefinitionId, out var cd) ? cd.Name : id ?? "";
        string Format(GameCommand c) => $"{c.Type}{(c.AbilityId is null ? "" : "/" + c.AbilityId)}{(c.CardId is null ? "" : " card=" + Name(c.CardId))}{(c.TargetId is null ? "" : " target=" + Name(c.TargetId))}{(c.PerformerId is null ? "" : " by=" + Name(c.PerformerId))}{(c.Choice is null ? "" : " choice=" + c.Choice)}{(c.Province is null ? "" : " prov=" + c.Province)}{(c.CardIds is { Length: > 0 } ids ? " cards=" + string.Join(",", ids.Select(Name)) : "")}{(c.Assignments is { Count: > 0 } asg ? " assign=" + string.Join(",", asg.Select(a => Name(a.Key) + ">" + a.Value)) : "")}{(c.GoldAmount is null ? "" : " gold=" + c.GoldAmount)}{(c.TargetPlayerId is null ? "" : " player=" + c.TargetPlayerId)}";
        string Tag(int seat) => seat == 0 ? "s0H" : "s1c";

        var probes = (Env("L5R_HUMAN_PROBE") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet(); var index = -1;
        foreach (var command in replay.Commands)
        {
            if (probes.Contains(++index))
            {
                var at = command.PlayerId; var probe = BotTournament.Arms[Env("L5R_HUMAN_PROBE_BOT") ?? "conan"](); var live = game;
                probe.Sampler = sample => live.Sample(at, sample);
                log.Append($"  ## probe before command {index} (seat {at}, {game.Phase})\n");
                foreach (var p in game.GetView(at).Players)
                    log.Append($"     seat {p.Id}: {string.Join(", ", p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower).Select(c => $"{c.Definition!.Name} {c.Force}F{(c.Bowed ? " bowed" : "")}"))}\n");
                foreach (var a in game.QueryActions(at).Where(a => a.Enabled)) log.Append($"     offered {a.Type}{(a.Command is { } c ? ": " + Format(c) : "")}{(a.TargetIds is { Length: > 0 } t ? " targets=" + string.Join(",", t.Select(Name)) : "")}\n");
                var answer = Ask(game, at, probe, roundTrip: false);
                log.Append($"     probe bot: {(answer is null ? "(no command)" : Format(answer))}\n");
                foreach (var l in probe.LookLog) log.Append($"     look: {l}\n");
                // L5R_HUMAN_PROBE_PLAY=<n>: then play the real match (every card known: a diagnostic, never a bot's input) forward with the Contender at both seats for n
                // decisions after each offered option, as a playout would, and log the moves and the end state.
                if (int.TryParse(Env("L5R_HUMAN_PROBE_PLAY"), out var steps))
                    foreach (var option in game.QueryActions(at).Where(a => a.Enabled && a.Command is not null && a.Type != "concede"))
                    {
                        var copy = game.Clone(); var bots = new[] { BotContext.Contender(), BotContext.Contender() }; var turn = copy.TurnNumber;
                        log.Append($"     -- play {Format(option.Command!)}: {(copy.Execute(option.Command!).Accepted ? "" : "refused")}\n");
                        for (var n = 0; n < steps && copy.WinnerId is null && copy.TurnNumber == turn && copy.Phase is Phase.Battle or Phase.BattleSelection; n++)
                        {
                            var who = copy.PendingChoicePlayerId ?? copy.PriorityPlayerId; var acts = copy.QueryActions(who);
                            if (!acts.Any(a => a.Enabled && a.Type is not ("concede" or "wait"))) { who = 1 - who; acts = copy.QueryActions(who); }
                            var d = TrainingOpponent.Choose(copy.GetView(who), acts, bots[who]).Command; if (d is null) break;
                            var events = copy.State.Events.Count; var name = Format(d); copy.Execute(d);
                            log.Append($"        s{who} {name}{string.Concat(copy.State.Events.Skip(events).Where(e => e.Message.Contains("destroyed") || e.Message.Contains("Force")).Select(e => " | " + e.Message))}\n");
                        }
                        foreach (var p in copy.GetView(at).Players) log.Append($"        end seat {p.Id}: provinces {p.Provinces.Count(q => !q.Destroyed)} Honor {p.Honor} units {string.Join(", ", p.Home.Concat(p.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower).Select(c => c.Definition!.Name))}\n");
                    }
            }
            var v0 = game.GetView(0);
            if (v0.TurnNumber != lastTurn) // the same turn header as the tournament's per-game logs (BotTournament.Play, L5R_TOURN_LOG)
            {
                lastTurn = v0.TurnNumber;
                log.Append($"===== TURN {v0.TurnNumber} active {Tag(v0.ActivePlayerId)} =====\n");
                foreach (var p in v0.Players)
                {
                    var hand = game.GetView(p.Id).Players[p.Id].Hand;
                    var cap = (p.Stronghold.Definition?.GoldProduction ?? 0) + p.Home.Where(h => h.Definition?.Type == CardType.Holding).Sum(h => h.Definition!.GoldProduction); var hold = p.Home.Count(h => h.Definition?.Type == CardType.Holding); var pers = p.Home.Concat(p.Army).Count(h => h.Definition?.Type == CardType.Personality); var tf = p.Home.Concat(p.Army).Where(h => h.Definition?.Type is CardType.Personality or CardType.Follower).Sum(h => Math.Max(0, h.Force));
                    log.Append($"  {Tag(p.Id)} Honor {p.Honor} provinces {p.Provinces.Count(q => !q.Destroyed)} hand {p.HandCount} gold {cap} holdings {hold} pers {pers} force {tf} | home: {string.Join(", ", p.Home.Select(h => (h.Definition?.Name ?? "?") + (h.Definition?.Type is CardType.Personality or CardType.Follower ? $"({h.Force}F)" : "") + (h.Bowed ? "*" : "")))}\n");
                    log.Append($"     hand: {string.Join(", ", hand.Select(h => h.Definition?.Name ?? "?"))} | row: {string.Join(", ", p.Provinces.Where(q => !q.Destroyed).Select(q => $"{q.Strength}:" + (q.Card?.Definition?.Name ?? "down")))}\n");
                }
            }
            var seat = command.PlayerId; var phase = v0.Phase.ToString();
            string? botLine = null;
            if (seat == 0)
            {
                decisions++; var counts = byPhase.TryGetValue(phase, out var pc) ? pc : byPhase[phase] = new int[3]; counts[0]++;
                var offered = game.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").ToArray();
                var forced = offered is [{ Command: { } only }] && Key(only) == Key(command); // the human had a single, complete legal option
                var answer = Ask(game, 0, bot, roundTrip: true); // asked at every seat-0 decision, as in the tournament, so its memory sees the same positions
                if (!forced)
                {
                    choices++; counts[1]++; var types = byType.TryGetValue(command.Type, out var tc) ? tc : byType[command.Type] = new int[2]; types[0]++;
                    var differs = answer is null || Key(answer) != Key(command);
                    if (answer is null) botNone++;
                    if (differs) { diffs++; counts[2]++; types[1]++; }
                    botLine = $"    >> {botArm}: {(answer is null ? "(no command)" : Format(answer))}{(differs ? "   [DIFF]" : "")}\n";
                    // L5R_HUMAN_DUMP=<substring>: list every offered option at the decisions whose logged line contains it (debugging a rule against a real position).
                    if (Environment.GetEnvironmentVariable("L5R_HUMAN_DUMP") is { Length: > 0 } dump && Format(command).Contains(dump, StringComparison.Ordinal))
                        botLine += string.Concat(offered.Select(a => $"       offered {a.Type}{(a.Command is { } c ? ": " + Format(c) : "")}{(a.TargetIds is { Length: > 0 } t ? " targets=" + t.Length : "")}\n"));
                }
            }
            else if (checkChampion)
            {
                var answer = Ask(game, seat, champion, roundTrip: false); champChecked++; // the client feeds its bot the live view and actions (MatchSession.StepPracticeOpponent)
                if (answer is null || Key(answer) != Key(command)) { champMismatch++; if (champLines.Count < 20) champLines.Add($"T{v0.TurnNumber} {phase}: logged {Format(command)} | champion {(answer is null ? "(no command)" : Format(answer))}"); }
            }
            var line = $"T{v0.TurnNumber} {phase} {Tag(seat)} {Format(command)}\n"; // named before Execute, while the cards are where the player saw them
            var result = game.Execute(command);
            if (!result.Accepted) { notes.Add($"command {executed + 1} ({line.Trim()}) was refused: {result.Error}; the comparison stops there"); break; }
            executed++;
            log.Append(line);
            if (botLine is not null) log.Append(botLine);
            foreach (var e in game.State.Events.Where(e => e.Sequence > lastEvent)) { log.Append("    · ").Append(e.Message).Append('\n'); lastEvent = e.Sequence; }
        }

        var end = game.GetView(0);
        var header = $"# human (seat 0, {end.Players[0].Stronghold.Definition?.Name}) vs champion (seat 1, {end.Players[1].Stronghold.Definition?.Name}); replay {Path.GetFileName(replayPath)}; seed {replay.Seed}; " +
                     $"comparison bot {botArm}; {executed}/{replay.Commands.Length} commands; winner {end.WinnerId?.ToString() ?? "none"}{(end.VictoryReason is null ? "" : " by " + end.VictoryReason)} on turn {end.TurnNumber}; " +
                     $"Honor {string.Join("/", end.Players.Select(p => p.Honor))} provinces {string.Join("/", end.Players.Select(p => p.Provinces.Count(q => !q.Destroyed)))}\n";
        var summary = new StringBuilder();
        summary.AppendLine().AppendLine("## summary");
        summary.AppendLine($"human decisions {decisions}; with a real choice {choices}; differences from {botArm} {diffs}{(choices == 0 ? "" : $" ({100.0 * diffs / choices:F0}%)")}{(botNone > 0 ? $"; {botArm} had no command at {botNone}" : "")}");
        foreach (var (phase, c) in byPhase) summary.AppendLine($"  {phase}: decisions {c[0]}, choices {c[1]}, DIFF {c[2]}");
        summary.AppendLine("by the human's command:");
        foreach (var (type, c) in byType) summary.AppendLine($"  {type}: choices {c[0]}, DIFF {c[1]}");
        if (checkChampion) { summary.AppendLine($"seat-1 check: the champion re-chose {champChecked} logged seat-1 commands, mismatches {champMismatch}"); foreach (var l in champLines) summary.AppendLine("  " + l); }
        foreach (var n in notes) summary.AppendLine("NOTE " + n);
        var plans = bot.PlanLog.Count == 0 ? "" : $"\n## {botArm} plans\n" + string.Join("\n", bot.PlanLog) + "\n";
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath)); if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(outPath, header + log + summary + plans);
        return $"  wrote {outPath}\n" + summary.ToString().TrimEnd();
    }

    /// <summary>A stand-in for a human replay: the Champion at both seats, seat 1 driven exactly as the practice client drives its opponent and seat 0 asked as the comparison asks its bot,
    /// so comparing seat 0 with the Champion must show no differences and the seat-1 check no mismatches. Saved with the client's serialiser.</summary>
    private static void Synthesise(string outPath, string deckPair, ulong seed)
    {
        var names = deckPair.Split('|'); var pool = DeckArchetypeTests.PoolDecks(Env("L5R_TOURN_DECKS") ?? "src/L5R.Client/Content/preloaded-decks.json").ToDictionary(p => p.Name, p => p.Deck);
        var game = MatchEngine.CreateCustomMatch(seed, pool[names[0]], pool[names[1]]);
        var human = new BotContext { Archetype = DeckArchetypeTests.Of(pool[names[0]]) }; var opponent = new BotContext();
        for (var n = 0; n < 6000 && game.WinnerId is null && game.GetView(0).TurnNumber <= BotSoak.TurnLimit; n++)
        {
            var seat = game.PriorityPlayerId; // the client asks its bot whenever seat 1 has priority; the human acts otherwise
            if (!game.QueryActions(seat).Any(a => a.Enabled && a.Type != "concede")) seat = 1 - seat;
            var command = seat == 0 ? Ask(game, 0, human, roundTrip: true) : Ask(game, 1, opponent, roundTrip: false);
            if (command is null || !game.Execute(command).Accepted) throw new Exception($"synthetic match stalled at seat {seat}, turn {game.GetView(0).TurnNumber}");
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath)); if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(outPath, JsonSerializer.Serialize(game.ExportReplay(), Web));
        Console.WriteLine($"  wrote {outPath}: {names[0]} vs {names[1]}, seed {seed}, {game.ExportReplay().Commands.Length} commands, winner {game.WinnerId?.ToString() ?? "none"}");
    }
}
