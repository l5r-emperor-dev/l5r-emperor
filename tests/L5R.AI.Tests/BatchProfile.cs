using System.Diagnostics;
using System.Text.Json;
using L5R.AI;
using L5R.Core;

/// <summary>
/// Issue #888: times seeded headless bot-vs-bot matches and splits the time into the engine's view and action queries, the harness's
/// own JSON copies, the bot's choice and the engine's Execute. Opt-in:
///   L5R_PROFILE_GAMES=40 [L5R_PROFILE_SEED=1] dotnet run -c Release --project tests/L5R.AI.Tests -- --filter "bot-profile"
/// Single-threaded on purpose, so a trace attached to the process shows one match's hot paths.
/// </summary>
/// <summary>Samples allocations by type (the runtime's GCAllocationTick, about every 100 KB) when L5R_PROFILE_ALLOC is set.</summary>
internal sealed class AllocationListener : System.Diagnostics.Tracing.EventListener
{
    public readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> ByType = new();
    protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
    }
    protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
    {
        if (e.EventName?.StartsWith("GCAllocationTick") != true || e.Payload is null) return;
        var names = e.PayloadNames!; var type = e.Payload[names.IndexOf("TypeName")] as string ?? "?"; var amount = Convert.ToInt64(e.Payload[names.IndexOf("AllocationAmount64")]);
        ByType.AddOrUpdate(type, amount, (_, c) => c + amount);
    }
}

internal static class BatchProfile
{
    private static readonly bool Hashing = Environment.GetEnvironmentVariable("L5R_PROFILE_HASH") is not null;
    private static readonly System.Security.Cryptography.IncrementalHash Digest = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);

    private sealed class Totals
    {
        public long Views, Actions, Copies, Bot, Execute, Create, ViewsAlloc, ActionsAlloc, CopiesAlloc, BotAlloc, ExecuteAlloc; public int Decisions, Games, Finished, Turns, Refused;
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static void Play(SoakPool pool, ulong seed, Totals t)
    {
        var a = pool.Build(seed, 0, out var ra); var b = pool.Build(seed, 1, out var rb);
        if (!ra.Ready || !rb.Ready) return;
        var created = Stopwatch.GetTimestamp();
        var game = MatchEngine.CreateCustomMatch(seed, a, b); game.AllowIdleBattlePasses();
        t.Create += Stopwatch.GetTimestamp() - created; t.Games++;
        var contexts = new[] { new BotContext(), new BotContext() };
        var retries = 0;
        for (var n = 0; n < BotSoak.DecisionCap; n++)
        {
            var mark = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread();
            var v0 = game.GetView(0);
            t.Views += Stopwatch.GetTimestamp() - mark; t.ViewsAlloc += GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (v0.WinnerId is not null || v0.Phase == Phase.Finished || v0.TurnNumber > BotSoak.TurnLimit) { t.Finished++; t.Turns += v0.TurnNumber; return; }
            var seat = v0.PendingChoice?.PlayerId ?? v0.PriorityPlayerId;
            mark = Stopwatch.GetTimestamp(); bytes = GC.GetAllocatedBytesForCurrentThread();
            var actions = game.QueryActions(seat);
            if (!actions.Any(x => x.Enabled && x.Type != "concede"))
            {
                var other = 1 - seat; var alt = game.QueryActions(other);
                if (alt.Any(x => x.Enabled && x.Type != "concede")) { seat = other; actions = alt; }
            }
            t.Actions += Stopwatch.GetTimestamp() - mark; t.ActionsAlloc += GC.GetAllocatedBytesForCurrentThread() - bytes;
            mark = Stopwatch.GetTimestamp(); bytes = GC.GetAllocatedBytesForCurrentThread();
            var actionsJson = JsonSerializer.Serialize(actions);
            if (Hashing) Digest.AppendData(System.Text.Encoding.UTF8.GetBytes(actionsJson)); // every option, reason and quote the engine listed, in order
            actions = JsonSerializer.Deserialize<ActionOption[]>(actionsJson)!;
            var seatView = game.GetView(seat);
            t.Views += Stopwatch.GetTimestamp() - mark; t.ViewsAlloc += GC.GetAllocatedBytesForCurrentThread() - bytes;
            mark = Stopwatch.GetTimestamp(); bytes = GC.GetAllocatedBytesForCurrentThread();
            var view = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(seatView))!;
            t.Copies += Stopwatch.GetTimestamp() - mark; t.CopiesAlloc += GC.GetAllocatedBytesForCurrentThread() - bytes;
            mark = Stopwatch.GetTimestamp(); bytes = GC.GetAllocatedBytesForCurrentThread();
            var decision = TrainingOpponent.Choose(view, actions, contexts[seat]);
            t.Bot += Stopwatch.GetTimestamp() - mark; t.BotAlloc += GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (decision.Command is null) { t.Turns += v0.TurnNumber; return; }
            mark = Stopwatch.GetTimestamp(); bytes = GC.GetAllocatedBytesForCurrentThread();
            var outcome = game.Execute(decision.Command);
            t.Execute += Stopwatch.GetTimestamp() - mark; t.ExecuteAlloc += GC.GetAllocatedBytesForCurrentThread() - bytes; t.Decisions++;
            if (!outcome.Accepted) { t.Refused++; contexts[seat].NoteRejection(view.Version, decision.Command, outcome.Error); if (++retries > 24) return; n--; continue; }
            retries = 0;
        }
    }

    public static readonly (string, Action)[] Tests =
    [
        ("query scope (issues #888, #1206): over seeded bot matches the Gold payment tokens, payment options and sources QueryActions remembers always equal a fresh computation", () =>
        {
            MatchEngine.VerifyQueryScopeForTests = true;
            try
            {
                var decisions = 0;
                for (ulong seed = 101; seed < 105; seed++)
                {
                    var result = BotSoak.PlayMatch(seed, TimeSpan.FromSeconds(90));
                    var broken = result.Findings.Where(f => f.Category is "exception" or "illegal").ToArray();
                    if (broken.Length > 0) throw new Exception($"seed {seed}: " + string.Join(" | ", broken.Take(3).Select(f => f.Reason)));
                    decisions += result.Decisions;
                }
                if (decisions < 200) throw new Exception($"only {decisions} decisions were played; the check did not exercise the query scope");
                Console.WriteLine($"  query scope verified over {decisions} decisions");
            }
            finally { MatchEngine.VerifyQueryScopeForTests = false; }
        }),
        ("bot-profile: N seeded headless bot-vs-bot matches, timed by phase (opt-in, see BatchProfile.cs)", () =>
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("L5R_PROFILE_GAMES"), out var games) || games <= 0)
            { Console.WriteLine("  SKIP: set L5R_PROFILE_GAMES to the number of matches to time"); return; }
            var first = ulong.TryParse(Environment.GetEnvironmentVariable("L5R_PROFILE_SEED"), out var s) ? s : 1UL;
            var pool = new SoakPool(); var t = new Totals();
            Play(pool, first, new Totals()); // warm-up: JIT and catalogue loading stay out of the timing
            var sites = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(); long thrown = 0; var counts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            if (Environment.GetEnvironmentVariable("L5R_PROFILE_THROWS") is not null) AppDomain.CurrentDomain.FirstChanceException += (_, e) => { Interlocked.Increment(ref thrown); counts.AddOrUpdate(e.Exception.GetType().Name, 1, (_, c) => c + 1); if (Environment.GetEnvironmentVariable("L5R_PROFILE_THROWS") == "1") { var st = new StackTrace(1, false); var key = string.Join(" < ", st.GetFrames().Take(7).Select(f => f.GetMethod()?.Name ?? "?")); sites.AddOrUpdate(key + " :: " + e.Exception.Message[..Math.Min(60, e.Exception.Message.Length)], 1, (_, c) => c + 1); } };
            using var allocations = Environment.GetEnvironmentVariable("L5R_PROFILE_ALLOC") is not null ? new AllocationListener() : null;
            var clock = Stopwatch.StartNew(); var gc0 = GC.CollectionCount(0); var alloc = GC.GetTotalAllocatedBytes(true);
            for (var i = 0; i < games; i++) Play(pool, first + (ulong)i, t);
            var seconds = clock.Elapsed.TotalSeconds; var total = t.Views + t.Actions + t.Copies + t.Bot + t.Execute + t.Create;
            double Share(long x) => total == 0 ? 0 : 100.0 * x / total;
            Console.WriteLine($"  PROFILE games={t.Games} finished={t.Finished} decisions={t.Decisions} refused={t.Refused} wall={seconds:0.00}s games/s={t.Games / seconds:0.00} decisions/s={t.Decisions / seconds:0} avg_turn={(t.Finished == 0 ? 0 : (double)t.Turns / t.Finished):0.0}");
            Console.WriteLine($"  PROFILE phases engine_views={Ms(t.Views):0}ms({Share(t.Views):0}%) engine_actions={Ms(t.Actions):0}ms({Share(t.Actions):0}%) engine_execute={Ms(t.Execute):0}ms({Share(t.Execute):0}%) engine_create={Ms(t.Create):0}ms({Share(t.Create):0}%) harness_json={Ms(t.Copies):0}ms({Share(t.Copies):0}%) bot={Ms(t.Bot):0}ms({Share(t.Bot):0}%)");
            Console.WriteLine($"  PROFILE exceptions thrown={thrown} per_decision={(double)thrown / Math.Max(1, t.Decisions):0.0} by_type={string.Join(", ", counts.OrderByDescending(c => c.Value).Take(4).Select(c => c.Key + ":" + c.Value))}");
            foreach (var site in sites.OrderByDescending(x => x.Value).Take(14)) Console.WriteLine($"  THROW {site.Value} {site.Key}");
            Console.WriteLine($"  PROFILE alloc_kb_per_decision views={t.ViewsAlloc / 1024 / Math.Max(1, t.Decisions)} actions={t.ActionsAlloc / 1024 / Math.Max(1, t.Decisions)} execute={t.ExecuteAlloc / 1024 / Math.Max(1, t.Decisions)} harness_json={t.CopiesAlloc / 1024 / Math.Max(1, t.Decisions)} bot={t.BotAlloc / 1024 / Math.Max(1, t.Decisions)}");
            if (allocations is not null) { var sum = allocations.ByType.Values.Sum(); foreach (var kv in allocations.ByType.OrderByDescending(x => x.Value).Take(25)) Console.WriteLine($"  ALLOC {100.0 * kv.Value / sum,5:0.0}% {kv.Key}"); }
            if (Hashing) Console.WriteLine($"  PROFILE listing_hash={Convert.ToHexString(Digest.GetHashAndReset())[..16]} (over every action list of {t.Decisions} decisions)");
            Console.WriteLine($"  PROFILE memory alloc={(GC.GetTotalAllocatedBytes(true) - alloc) / 1048576}MB gc0={GC.CollectionCount(0) - gc0} per_decision_us={Ms(total) * 1000 / Math.Max(1, t.Decisions):0}");
        })
    ];
}
