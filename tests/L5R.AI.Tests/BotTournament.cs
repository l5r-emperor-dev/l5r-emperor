using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.AI;
using L5R.Core;

// Issue #963: the champion-vs-challenger tournament that every bot change must pass (see docs/bots/README.md). Bot against bot only: the harness plays through the real engine with
// JSON-round-tripped views and actions, exactly as BotSoak and BotArena do, and nothing in it reads more than the seat's own PlayerView.
//
//   L5R_TOURN_CHAL=<arm>            the challenger arm (default "champion"; "champion" against "champion" is the A/A calibration). Arms are registered in Challengers below.
//   L5R_TOURN_PAIRING=same|independent   same: both games of a pair use one seed, so only the bot differs (most sensitive; an A/A is then identical games, which checks determinism);
//                                   independent: the second game gets a fresh shuffle (an A/A then measures the noise a plain win-rate test faces, and the seat-0 advantage).
//   L5R_TOURN_MODE=dev|holdout      dev: ordered pairs among the 9 development decks, development seed range. holdout: ordered pairs with at least one held-out deck, held-out seed range
//                                   (final confirmation only; never used to tune).
//   L5R_TOURN_SEEDS=<n>             seeds per ordered pair (the fixed sample from the power note), L5R_TOURN_OUT=<path without extension> writes <path>.json and <path>.md.
//   L5R_TOURN_SEED_START=<k>        first seed index (default 0): a follow-up uses a fresh block, never a re-run of the same seeds.
//   L5R_TOURN_PILOT=<a;b;c>         only games in which the challenger pilots one of these decks (for a hypothesis about specific decks; pre-register the list, and report the full pool too).
//   L5R_TOURN_ONLY=<deckA|deckB|seed|challengerSeat> replays one game; L5R_TOURN_TRACE=1 prints what a seat was offered when the bot stalls.
//   L5R_TOURN_DECKS=<preloaded-decks.json> (default src/L5R.Client/Content/preloaded-decks.json).
internal static class BotTournament
{
    /// <summary>The bot arms. "champion" is the bot on main. A candidate branch registers its challenger here, with the BotContext flag that turns its change on.</summary>
    public static readonly Dictionary<string, Func<BotContext>> Arms = new()
    {
        ["champion"] = () => new(), // the bot on main: since the 2026-10-15 promotion this includes the Seppuku policy and informed defense
        ["pre-promotion"] = () => new() { UseSeppukuPolicy = false, UseInformedDefense = false }, // the champion before them (rounds 1-2 baseline)
        ["pre-gates"] = () => new() { UseUnopposedGate = false, UsePhaseChoice = false }, // the champion before #997/#1003 (unopposed gate) and #998 (Battle/Open phase choice); run as the challenger against the champion that has them
        ["pre-laststand"] = () => new() { UseCavalryLastStand = false }, // the champion before #1008/#1015 (Cavalry last-stand defence); run as the challenger against the champion that has it
        ["seppuku"] = () => new() { UseSeppukuPolicy = true }, // round 1 (docs/bots/experiments/2026-10-15-seppuku.md)
        ["defense"] = () => new() { UseInformedDefense = true }, // round 2 (docs/bots/experiments/2026-10-15-defense.md)
        ["bodies"] = () => new() { UseBodiesFirst = true }, // round 3 (docs/bots/experiments/2026-10-15-bodies.md)
        ["reactions"] = () => new() { UseSureReactions = true }, // round 4 (docs/bots/experiments/2026-10-15-reactions.md)
        ["arch-attack"] = () => new() { UseArchetypeAttack = true }, // archetype S1 (docs/bots/experiments/2026-10-15-archetype.md)
        ["arch-abilities"] = () => new() { UseArchetypeAbilities = true }, // S2
        ["arch-military"] = () => new() { UseArchetypeMilitary = true }, // S3
        ["arch-switch"] = () => new() { UseArchetypeSwitch = true }, // S4
        ["split-defense"] = () => new() { UseSplitDefense = true }, // hypothesis A
        ["military-trades"] = () => new() { UseMilitaryTrades = true }, // hypothesis B
        ["planner"] = () => new() { UsePlanner = true },
        ["planner-no-reserve"] = () => new() { UsePlanner = true, PlannerReserve = false }, // ablation: no Gold held back for Battle tricks
        ["planner-no-discard"] = () => new() { UsePlanner = true, PlannerDiscard = false }, // ablation: the champion's discard rule
        ["triage"] = () => new() { UsePlannerTriage = true }, // issue #1185 slice 3 alone, on the champion
        ["planner-triage"] = () => new() { UsePlanner = true, UsePlannerTriage = true }, // the planner with triage
        ["planner-purchase"] = () => new() { UsePlanner = true, PlannerAttack = false, PlannerReserve = false, PlannerDiscard = false }, // ablation: only the purchase slate
        ["triage-targets"] = () => new() { UsePlannerTriage = true, TriagePrintedFirst = false, TriageWonCap = false }, // triage on the champion: only the target ranking
        ["triage-printed"] = () => new() { UsePlannerTriage = true, TriageTargets = false, TriageWonCap = false }, // only printed abilities before hand cards
        ["triage-cap"] = () => new() { UsePlannerTriage = true, TriageTargets = false, TriagePrintedFirst = false }, // only the no-spend-on-a-won-battle cap
        ["triage-cap-bows"] = () => new() { UsePlannerTriage = true, TriageTargets = false, TriagePrintedFirst = false, TriageSpareKills = true }, // the cap, but destroys still allowed
        ["triage-null"] = () => new() { UsePlannerTriage = true, TriageTargets = false, TriagePrintedFirst = false, TriageWonCap = false }, // calibration: the triage flag with every rule off must replay the champion game for game
        ["contender"] = () => BotContext.Contender(), // issue #1185: the planning bot (docs/bots/CONTENDER.md); compare it with the champion's own baseline on the same games (tools/bots/compare_tournament.py)
        ["c2"] = () => BotContext.Contender(rules: (Environment.GetEnvironmentVariable("L5R_C2") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)), // issue #1185 tuning: the Contender plus the rules named in L5R_C2
        ["conan"] = () => BotContext.Conan(rules: (Environment.GetEnvironmentVariable("L5R_C2") is { Length: > 0 } c ? c.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : null)), // issue #1206: the Contender with battle lookahead (rules from L5R_C2 when set)
        ["contender+attack"] = () => BotContext.Contender(attack: true),
        ["contender+discard"] = () => BotContext.Contender(discard: true),
        ["contender+reserve"] = () => BotContext.Contender(reserve: true),
        ["contender+woncap"] = () => BotContext.Contender(wonCap: true),
        ["contender+all"] = () => BotContext.Contender(attack: true, discard: true, reserve: true, wonCap: true),
        ["planner-plain"] = () => new() { UsePlanner = true, PlannerReserve = false, PlannerDiscard = false }, // ablation: neither // issue #1185: the turn-planning bot (docs/bots/experiments/2026-11-07-planner-slice1.md)
        ["arch-all"] = () => new() { UseArchetypeAttack = true, UseArchetypeAbilities = true, UseArchetypeMilitary = true, UseArchetypeSwitch = true }
    };

    public const ulong DevSeedBase = 1_000_000UL, HoldoutSeedBase = 9_000_000UL, IndependentOffset = 500_000UL;

    public sealed record GameRecord(string Deck0, string Deck1, ulong Seed, int ChallengerSeat, int? Winner, string Reason, int Turn, double Seconds, int Refused, string? Error,
        int[] Honor, int[] ProvincesLeft, int[] Personalities, Dictionary<string, int>[] Actions);

    /// <summary>The held-out decks are fixed by a hash of the name, not chosen by hand: the three lowest SHA-256 values of the 12 pool names.</summary>
    public static string[] HoldoutNames(IEnumerable<string> names) => names.OrderBy(n => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("l5r-bot-holdout:" + n)))).Take(3).ToArray();

    private static ActionOption[] Clone(ActionOption[] a) => JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(a))!;

    public static GameRecord Play(string name0, Deck deck0, string name1, Deck deck1, Func<BotContext>[] makers, int challengerSeat, ulong seed, TimeSpan budget)
    {
        var clock = Stopwatch.StartNew(); var refused = 0; var counts = new[] { new Dictionary<string, int>(), new Dictionary<string, int>() };
        MatchEngine? game = null;
        GameRecord End(int? winner, string reason, int turn, string? error = null)
        {
            int[] honor = [0, 0], provinces = [0, 0], people = [0, 0];
            if (game is not null) { var v = game.GetView(0); for (var i = 0; i < 2; i++) { var p = v.Players[i]; honor[i] = p.Honor; provinces[i] = p.Provinces.Count(x => !x.Destroyed); people[i] = p.Home.Concat(p.Army).Count(c => c.Definition?.Type == CardType.Personality); } }
            return new(name0, name1, seed, challengerSeat, winner, reason, turn, clock.Elapsed.TotalSeconds, refused, error, honor, provinces, people, counts);
        }
        try { game = MatchEngine.CreateCustomMatch(seed, deck0, deck1); }
        catch (Exception e) { return End(null, "setup", 0, e.Message.Split('\n')[0]); }
        var memory = new[] { makers[0](), makers[1]() }; for (var seat = 0; seat < 2; seat++) { var own = seat; if (memory[seat].Has("look")) memory[seat].Sampler = sample => game!.Sample(own, sample); } memory[0].Archetype = DeckArchetypeTests.Of(deck0); memory[1].Archetype = DeckArchetypeTests.Of(deck1); var refusedAt = new HashSet<(long, int)>(); var sameAnswer = 0; string? lastRefusal = null;
        // L5R_TOURN_LOG=<dir>: one plain-text log per game (every accepted decision, by whom, and the engine's event messages), for reviewing why a game was won or lost.
        var logDir = Environment.GetEnvironmentVariable("L5R_TOURN_LOG"); var log = logDir is { Length: > 0 } ? new StringBuilder() : null; long lastEvent = -1; var lastTurn = -1;
        string Name(string? id) => id is not null && game.State.Cards.TryGetValue(id, out var ci) && CardCatalog.Cards.TryGetValue(ci.DefinitionId, out var cd) ? cd.Name : id ?? "";
        GameRecord Logged(GameRecord r)
        {
            if (log is null) return r;
            Directory.CreateDirectory(logDir!);
            var who = r.Winner is null ? "none" : r.Winner == challengerSeat ? "CHAL" : "CHAMP";
            File.WriteAllText(Path.Combine(logDir!, $"{who}-{name0}-vs-{name1}-{seed}-c{challengerSeat}.log".Replace(' ', '_').Replace('/', '_')),
                $"# {name0} (seat 0, {(challengerSeat == 0 ? "challenger" : "champion")}) vs {name1} (seat 1, {(challengerSeat == 1 ? "challenger" : "champion")}); seed {seed}; winner {r.Winner} ({who}) by {r.Reason} on turn {r.Turn}; Honor {string.Join("/", r.Honor)} provinces {string.Join("/", r.ProvincesLeft)} personalities {string.Join("/", r.Personalities)}\n" + log + "\n## challenger plans\n" + string.Join("\n", memory[challengerSeat].PlanLog) + (memory[challengerSeat].LookLog is { Count: > 0 } looks ? "\n## challenger lookahead\n" + string.Join("\n", looks) : ""));
            return r;
        }
        for (var n = 0; n < 6000; n++)
        {
            try
            {
                var v0 = game.GetView(0);
                if (v0.WinnerId is not null || v0.Phase == Phase.Finished) return Logged(End(v0.WinnerId, v0.VictoryReason ?? "finished", v0.TurnNumber));
                if (v0.TurnNumber > BotSoak.TurnLimit) return Logged(End(null, "turn limit", v0.TurnNumber));
                if (clock.Elapsed > budget) return End(null, "out of time", v0.TurnNumber, "wall budget");
                if (log is not null && v0.TurnNumber != lastTurn)
                {
                    lastTurn = v0.TurnNumber;
                    log.Append($"===== TURN {v0.TurnNumber} active s{v0.ActivePlayerId}{(v0.ActivePlayerId == challengerSeat ? "C" : "c")} =====\n");
                    foreach (var p in v0.Players)
                    {
                        var hand = game.GetView(p.Id).Players[p.Id].Hand;
                        var cap = (p.Stronghold.Definition?.GoldProduction ?? 0) + p.Home.Where(h => h.Definition?.Type == CardType.Holding).Sum(h => h.Definition!.GoldProduction); var hold = p.Home.Count(h => h.Definition?.Type == CardType.Holding); var pers = p.Home.Concat(p.Army).Count(h => h.Definition?.Type == CardType.Personality); var tf = p.Home.Concat(p.Army).Where(h => h.Definition?.Type is CardType.Personality or CardType.Follower).Sum(h => Math.Max(0, h.Force));
                        log.Append($"  s{p.Id}{(p.Id == challengerSeat ? "C" : "c")} Honor {p.Honor} provinces {p.Provinces.Count(q => !q.Destroyed)} hand {p.HandCount} gold {cap} holdings {hold} pers {pers} force {tf} | home: {string.Join(", ", p.Home.Select(h => (h.Definition?.Name ?? "?") + (h.Definition?.Type is CardType.Personality or CardType.Follower ? $"({h.Force}F)" : "") + (h.Bowed ? "*" : "")))}\n");
                        log.Append($"     hand: {string.Join(", ", hand.Select(h => h.Definition?.Name ?? "?"))} | row: {string.Join(", ", p.Provinces.Where(q => !q.Destroyed).Select(q => $"{q.Strength}:" + (q.Card?.Definition?.Name ?? "down")))}\n");
                    }
                }
                var seat = v0.PendingChoice?.PlayerId ?? v0.PriorityPlayerId;
                var actions = Clone(game.QueryActions(seat));
                if (!actions.Any(x => x.Enabled && x.Type != "concede"))
                {
                    var other = 1 - seat; var alt = Clone(game.QueryActions(other));
                    if (alt.Any(x => x.Enabled && x.Type != "concede")) { seat = other; actions = alt; }
                }
                var view = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(game.GetView(seat)))!;
                var decision = TrainingOpponent.Choose(view, actions, memory[seat]);
                if (decision.Command is null && Environment.GetEnvironmentVariable("L5R_TOURN_TRACE") == "1")
                {
                    Console.WriteLine($"STALL seed {seed} seat {seat} turn {v0.TurnNumber} phase {v0.Phase} pending {v0.PendingChoice?.Type} priority {v0.PriorityPlayerId}: {decision.Reason}");
                    foreach (var a in actions.Where(x => x.Enabled)) Console.WriteLine($"  offered {a.Type} card={a.CardId} ability={a.Command?.AbilityId} choice={a.Command?.Choice} target={a.Command?.TargetId} label={a.Label}");
                }
                // "engine stall": every action the seat was offered at this version has been refused by the engine (a legal-action-list gap in Core), so no bot could continue; "stall" is the bot's own failure.
                if (decision.Command is null) return End(null, refusedAt.Contains((view.Version, seat)) ? "engine stall" : "stall", v0.TurnNumber, $"seat {seat} had no command: {decision.Reason}");
                var outcome = game.Execute(decision.Command);
                if (outcome.Accepted)
                {
                    if (log is not null)
                    {
                        var c = decision.Command;
                        log.Append($"T{v0.TurnNumber} {v0.Phase} s{seat}{(seat == challengerSeat ? "C" : "c")} {c.Type}{(c.AbilityId is null ? "" : "/" + c.AbilityId)}{(c.CardId is null ? "" : " card=" + Name(c.CardId))}{(c.TargetId is null ? "" : " target=" + Name(c.TargetId))}{(c.PerformerId is null ? "" : " by=" + Name(c.PerformerId))}{(c.Choice is null ? "" : " choice=" + c.Choice)}{(c.Province is null ? "" : " prov=" + c.Province)}{(c.CardIds is { Length: > 0 } ids ? " cards=" + string.Join(",", ids.Select(Name)) : "")}{(c.Assignments is { Count: > 0 } asg ? " assign=" + string.Join(",", asg.Select(a => Name(a.Key) + ">" + a.Value)) : "")}\n");
                        foreach (var e in game.State.Events.Where(e => e.Sequence > lastEvent)) { log.Append("    · ").Append(e.Message).Append('\n'); lastEvent = e.Sequence; }
                    }
                    if (Environment.GetEnvironmentVariable("L5R_TOURN_TRACE") == "1" && v0.TurnNumber >= 10) Console.WriteLine($"T{v0.TurnNumber} {v0.Phase} seat {seat} {decision.Command.Type}/{decision.Command.AbilityId} card={decision.Command.CardId} target={decision.Command.TargetId} | {string.Join(" || ", game.State.Events.TakeLast(2).Select(e => e.Message))}");
                    sameAnswer = 0; lastRefusal = null;
                    var key = decision.Command.Type == "useAbility" ? "ability" : decision.Command.Type; counts[seat][key] = counts[seat].GetValueOrDefault(key) + 1;
                    continue;
                }
                if (Environment.GetEnvironmentVariable("L5R_TOURN_TRACE") == "1") Console.WriteLine($"REFUSED seed {seed} seat {seat} turn {v0.TurnNumber} {decision.Command.Type}/{decision.Command.AbilityId} card={decision.Command.CardId} target={decision.Command.TargetId}: {outcome.Error}");
                refusedAt.Add((view.Version, seat)); refused++; var signature = JsonSerializer.Serialize(decision.Command);
                memory[seat].NoteRejection(view.Version, decision.Command, outcome.Error);
                sameAnswer = signature == lastRefusal ? sameAnswer + 1 : 0; lastRefusal = signature;
                if (sameAnswer >= 24 || refused > 400) return End(null, "illegal", v0.TurnNumber, $"refused repeatedly: {outcome.Error}");
                n--;
            }
            catch (Exception e) { return End(null, "exception", 0, $"{e.GetType().Name}: {e.Message}"); }
        }
        return End(null, "decision cap", 0, "6000 decisions");
    }

    // ---- statistics -------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static double[] LogFactorial(int n) { var f = new double[n + 1]; for (var i = 1; i <= n; i++) f[i] = f[i - 1] + Math.Log(i); return f; }
    /// <summary>P(X >= k) for X ~ Binomial(n, .5), exact.</summary>
    public static double UpperTail(int n, int k)
    {
        if (n == 0) return 1; var lf = LogFactorial(n); double p = 0;
        for (var i = Math.Max(k, 0); i <= n; i++) p += Math.Exp(lf[n] - lf[i] - lf[n - i] - n * Math.Log(2));
        return Math.Min(1, p);
    }
    public static double TwoSided(int a, int b) => Math.Min(1, 2 * UpperTail(a + b, Math.Max(a, b)));
    public static (double Low, double High) Wilson(int wins, int n)
    {
        if (n == 0) return (0, 1); const double z = 1.96; var p = (double)wins / n; var d = 1 + z * z / n;
        var c = (p + z * z / (2 * n)) / d; var h = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n)) / d; return (c - h, c + h);
    }
    /// <summary>Decided games needed to detect a win rate of <paramref name="rate"/> against an even split (one-sided alpha .05 is z 1.645; the note uses the usual two-sided .05, z 1.96) with 80% power.</summary>
    public static int GamesNeeded(double rate) { var q = 1 - rate; var x = (1.96 * .5 + .8416 * Math.Sqrt(rate * q)) / (rate - .5); return (int)Math.Ceiling(x * x); }

    // ---- the tournament -----------------------------------------------------------------------------------------------------------------------------------------------------------
    public static void Run(string decksPath, string challenger, string pairing, string mode, int seeds, string? outPath, int seedStart = 0)
    {
        if (!Arms.TryGetValue(challenger, out var challengerMaker)) throw new Exception($"unknown arm '{challenger}'; known: {string.Join(", ", Arms.Keys)}");
        var entries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(decksPath))!;
        var pool = new List<(string Name, Deck Deck)> { ("Lion preset", CustomPractice.Preset("Lion")), ("Crane preset", CustomPractice.Preset("Crane")) };
        var unplayable = new List<string>();
        foreach (var e in entries)
        {
            try { var d = DeckFormat.Parse(e["text"]); if (CustomPractice.Review(d).Ready) pool.Add((e["name"], d)); else unplayable.Add(e["name"]); }
            catch (Exception ex) { unplayable.Add($"{e["name"]}: {ex.Message}"); }
        }
        if (Environment.GetEnvironmentVariable("L5R_TOURN_CARDS") is { Length: > 0 } cardsOut) // a card table for every deck in the pool, for reviewing games
        {
            var t = new StringBuilder();
            foreach (var (name, deck) in pool)
            {
                t.AppendLine($"## {name}");
                foreach (var e in deck.Entries)
                {
                    var d = CardCatalog.Cards.Values.Where(c => c.Name == e.Name).OrderBy(c => c.ScenarioOnly).FirstOrDefault();
                    if (d is null) { t.AppendLine($"- {e.Count}x {e.Name} (unknown)"); continue; }
                    var abilities = string.Join(" | ", (d.Abilities ?? []).Select(a => AbilityCatalog.All.TryGetValue(a, out var ab) ? $"{ab.Timing}: {ab.Label}" : a));
                    t.AppendLine($"- {e.Count}x {d.Name} [{d.Type}{(d.Clan is "Unaligned" ? "" : ", " + d.Clan)}] cost {d.GoldCost}{(d.GoldProduction > 0 ? $" prod {d.GoldProduction}" : "")}{(d.Type is CardType.Personality or CardType.Follower or CardType.Item ? $" {d.Force}F/{d.Chi}C" : "")}{(d.Type == CardType.Personality ? $" PH {d.PersonalHonor} HR {d.HonorRequirement}" : "")}{(d.Focus > 0 ? $" FV {d.Focus}" : "")}{(d.Keywords is { Length: > 0 } k ? " {" + string.Join(", ", k) + "}" : "")}{(d.Type == CardType.Stronghold ? $" PS {d.ProvinceStrength} SH {d.StartingHonor}" : "")}{(abilities.Length > 0 ? " — " + abilities : "")}");
                }
                t.AppendLine();
            }
            File.WriteAllText(cardsOut, t.ToString());
            return;
        }
        var holdout = HoldoutNames(pool.Select(p => p.Name)).ToHashSet();
        var holdMode = mode == "holdout"; var seedBase = holdMode ? HoldoutSeedBase : DevSeedBase;
        var pairs = pool.SelectMany(a => pool.Where(b => b.Name != a.Name).Select(b => (A: a, B: b)))
            .Where(p => holdMode ? holdout.Contains(p.A.Name) || holdout.Contains(p.B.Name) : !holdout.Contains(p.A.Name) && !holdout.Contains(p.B.Name)).ToArray();
        Func<BotContext>[][] seatMakers = [[challengerMaker, Arms["champion"]], [Arms["champion"], challengerMaker]]; // index = the challenger's seat
        var jobs = new List<(int Pair, ulong Seed, int ChalSeat)>();
        var pilots = Environment.GetEnvironmentVariable("L5R_TOURN_PILOT")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        for (var s = seedStart; s < seedStart + seeds; s++)
            for (var i = 0; i < pairs.Length; i++)
            {
                var seed = seedBase + (ulong)s * 10_000UL + (ulong)i;
                if (pilots is null || pilots.Contains(pairs[i].A.Name)) jobs.Add((i, seed, 0)); // the challenger pilots deck A in seat 0
                if (pilots is null || pilots.Contains(pairs[i].B.Name)) jobs.Add((i, pairing == "independent" ? seed + IndependentOffset : seed, 1)); // and deck B in seat 1
            }
        if (Environment.GetEnvironmentVariable("L5R_TOURN_ONLY") is { Length: > 0 } only) // "deckA|deckB|seed|challengerSeat": replay one game (the seed is the full seed of a report row)
        {
            var f = only.Split('|'); jobs = [(Array.FindIndex(pairs, p => p.A.Name == f[0] && p.B.Name == f[1]), ulong.Parse(f[2]), int.Parse(f[3]))];
        }
        var wall = Stopwatch.StartNew(); var results = new GameRecord[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            var j = jobs[i]; var p = pairs[j.Pair];
            results[i] = Play(p.A.Name, p.A.Deck, p.B.Name, p.B.Deck, seatMakers[j.ChalSeat], j.ChalSeat, j.Seed, TimeSpan.FromMinutes(double.TryParse(Environment.GetEnvironmentVariable("L5R_TOURN_BUDGET"), out var minutes) ? minutes : 5)); // L5R_TOURN_BUDGET=<minutes>: a slower bot (lookahead, issue #1206) needs a longer wall budget
        });
        wall.Stop();
        var report = Summarise(results, jobs.Select(j => j.Pair).ToArray(), challenger, pairing, mode, seeds, pairs.Length, holdout, unplayable, wall.Elapsed, seedStart);
        Console.WriteLine(report.Markdown);
        if (outPath is not null)
        {
            File.WriteAllText(outPath + ".md", report.Markdown);
            File.WriteAllText(outPath + ".json", JsonSerializer.Serialize(new { report.Summary, games = results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static (string Markdown, object Summary) Summarise(GameRecord[] results, int[] pairIndex, string challenger, string pairing, string mode, int seeds, int pairCount, HashSet<string> holdout,
        List<string> unplayable, TimeSpan wall, int seedStart)
    {
        bool ChalWon(GameRecord g) => g.Winner == g.ChallengerSeat;
        var decided = results.Where(g => g.Winner is not null).ToArray();
        int won = decided.Count(ChalWon), lost = decided.Length - won;
        var (lo, hi) = Wilson(won, decided.Length);
        var seat0 = decided.Count(g => g.Winner == 0); var (s0lo, s0hi) = Wilson(seat0, decided.Length);
        // Pair-level (paired seeds): in "same" pairing both games share deck, seat order and shuffle and only the bot differs.
        var both = 0; var neither = 0; var split = 0;
        foreach (var pair in results.Select((g, i) => (g, key: (pairIndex[i], g.Seed % 500_000UL))).GroupBy(x => x.key).Where(x => x.Count() == 2 && x.All(y => y.g.Winner is not null)))
        {
            var c = pair.Count(y => ChalWon(y.g));
            if (c == 2) both++; else if (c == 0) neither++; else split++;
        }
        var bad = results.Where(g => g.Error is not null || g.Reason is "stall" or "engine stall" or "illegal" or "exception" or "decision cap" or "setup").ToArray();
        double Mean(IEnumerable<double> v) { var a = v.ToArray(); return a.Length == 0 ? 0 : a.Average(); }
        double ProvMargin(GameRecord g) => g.ProvincesLeft[g.ChallengerSeat] - g.ProvincesLeft[1 - g.ChallengerSeat];
        double HonorMargin(GameRecord g) => g.Honor[g.ChallengerSeat] - g.Honor[1 - g.ChallengerSeat];
        var reasons = decided.GroupBy(g => g.Reason).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}: {g.Count()}").ToArray();
        var perSeconds = results.Sum(g => g.Seconds) / Math.Max(1, results.Length); var gamesPerSecond = results.Length / Math.Max(.001, wall.TotalSeconds);
        var decidedShare = results.Length == 0 ? 0 : (double)decided.Length / results.Length;
        string Eta(double decidedGames) => TimeSpan.FromSeconds(decidedGames / Math.Max(.001, decidedShare) / Math.Max(.001, gamesPerSecond)).ToString(@"h\:mm\:ss");
        var sb = new StringBuilder();
        sb.AppendLine($"# Bot tournament: {challenger} (challenger) vs champion").AppendLine();
        sb.AppendLine($"- Mode **{mode}** ({(mode == "holdout" ? "ordered pairs with at least one held-out deck; held-out seed range" : "ordered pairs among the development decks; development seed range")}), pairing **{pairing}**, {seeds} seed(s) (from index {seedStart}) x {pairCount} ordered deck pairs x 2 seats{(Environment.GetEnvironmentVariable("L5R_TOURN_PILOT") is { Length: > 0 } pl ? $", only games where the challenger pilots {pl}" : "")} = {results.Length} games.");
        sb.AppendLine($"- Held-out decks (SHA-256 order, fixed): {string.Join(", ", holdout.OrderBy(x => x))}.");
        foreach (var u in unplayable) sb.AppendLine($"- NOT PLAYED: {u}");
        sb.AppendLine($"- Decided {decided.Length} of {results.Length}; no winner {results.Length - decided.Length}. Wall clock {wall:h\\:mm\\:ss} on {Environment.ProcessorCount} cores ({gamesPerSecond:F2} games/s, {perSeconds:F1} s per game on one core).").AppendLine();
        sb.AppendLine("## Win rate").AppendLine();
        sb.AppendLine($"**Challenger won {won}, champion won {lost}** = {(decided.Length == 0 ? 0 : 100.0 * won / decided.Length):F1}% (95% Wilson CI {100 * lo:F1}-{100 * hi:F1}%). Exact two-sided sign test p = {TwoSided(won, lost):F4}; one-sided (challenger better) p = {UpperTail(decided.Length, won):F4}.");
        sb.AppendLine($"- Paired by seed: challenger won both games of a pair {both}, lost both {neither}, split {split}. Exact McNemar (both vs neither) two-sided p = {TwoSided(both, neither):F4}.");
        sb.AppendLine($"- **Seat-0 advantage:** seat 0 won {seat0} of {decided.Length} decided games = {(decided.Length == 0 ? 0 : 100.0 * seat0 / decided.Length):F1}% (95% CI {100 * s0lo:F1}-{100 * s0hi:F1}%); subtract {(decided.Length == 0 ? 0 : 100.0 * seat0 / decided.Length - 50):+0.0;-0.0}% from a seat-0 win rate to correct it. Challenger as seat 0: {decided.Count(g => g.ChallengerSeat == 0 && ChalWon(g))}/{decided.Count(g => g.ChallengerSeat == 0)}; as seat 1: {decided.Count(g => g.ChallengerSeat == 1 && ChalWon(g))}/{decided.Count(g => g.ChallengerSeat == 1)}.");
        sb.AppendLine().AppendLine("## Beyond win rate (challenger minus champion, decided games)").AppendLine();
        sb.AppendLine($"- Provinces left margin {Mean(decided.Select(ProvMargin)):+0.00;-0.00}; Honor margin {Mean(decided.Select(HonorMargin)):+0.0;-0.0}; Personalities in play margin {Mean(decided.Select(g => (double)(g.Personalities[g.ChallengerSeat] - g.Personalities[1 - g.ChallengerSeat]))):+0.00;-0.00}.");
        sb.AppendLine($"- Game length: mean turn {Mean(decided.Select(g => (double)g.Turn)):F1} (challenger wins {Mean(decided.Where(ChalWon).Select(g => (double)g.Turn)):F1}, losses {Mean(decided.Where(g => !ChalWon(g)).Select(g => (double)g.Turn)):F1}); turn-limit games {results.Count(g => g.Reason == "turn limit")}.");
        sb.AppendLine($"- Victory reasons: {string.Join("; ", reasons)}.");
        sb.AppendLine($"- **Soundness:** bot stalls {results.Count(g => g.Reason == "stall")} (engine stalls, where the engine refused the only offered action: {results.Count(g => g.Reason == "engine stall")}), illegal {results.Count(g => g.Reason == "illegal")}, exceptions {results.Count(g => g.Reason == "exception")}, decision cap {results.Count(g => g.Reason == "decision cap")}, setup failures {results.Count(g => g.Reason == "setup")}, out of time {results.Count(g => g.Reason == "out of time")}; games with a refused-then-retried action {results.Count(g => g.Refused > 0)} ({results.Sum(g => g.Refused)} refusals, informational). The bar for promotion is zero bot stalls and none of the others; engine stalls are reported to the rules lane.");
        sb.AppendLine().AppendLine("## Per deck (challenger piloting the deck)").AppendLine().AppendLine("| Deck | Games decided | Challenger wins |").AppendLine("| --- | --- | --- |");
        foreach (var name in results.SelectMany(g => new[] { g.Deck0, g.Deck1 }).Distinct().OrderBy(x => x))
        {
            var mine = decided.Where(g => (g.ChallengerSeat == 0 ? g.Deck0 : g.Deck1) == name).ToArray();
            sb.AppendLine($"| {name}{(holdout.Contains(name) ? " (held out)" : "")} | {mine.Length} | {mine.Count(ChalWon)} ({(mine.Length == 0 ? 0 : 100.0 * mine.Count(ChalWon) / mine.Length):F0}%) |");
        }
        sb.AppendLine().AppendLine("## Power note").AppendLine();
        sb.AppendLine($"Independent games, exact binomial, two-sided alpha .05, 80% power: detect 55% needs about **{GamesNeeded(.55)}** decided games; 60% needs about **{GamesNeeded(.60)}**; 52% needs about {GamesNeeded(.52)}. At this run's throughput ({gamesPerSecond:F2} games/s, {decidedShare:P0} decided) that is {Eta(GamesNeeded(.55))} and {Eta(GamesNeeded(.60))} of wall clock. `same` pairing removes deck, seat and shuffle noise, so it needs fewer games when the bots differ; the A/A with `independent` pairing is the honest noise floor.");
        if (unplayable.Count > 0) sb.AppendLine().AppendLine($"{unplayable.Count} deck(s) were not playable.");
        foreach (var g in bad.Take(20)) sb.AppendLine($"- FINDING {g.Deck0} vs {g.Deck1}, seed {g.Seed}, challenger seat {g.ChallengerSeat}: {g.Reason} turn {g.Turn} {g.Error}");
        var summary = new
        {
            challenger,
            pairing,
            mode,
            seeds,
            pairs = pairCount,
            games = results.Length,
            decided = decided.Length,
            challengerWins = won,
            championWins = lost,
            seat0Wins = seat0,
            both,
            neither,
            split,
            soundnessFindings = bad.Length,
            wallSeconds = wall.TotalSeconds,
            gamesPerSecond
        };
        return (sb.ToString(), summary);
    }
}
