using System.Text.RegularExpressions;
using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md step 0 and step 1 item 1): the frozen T2 yardstick, the rule registry that fails a run on an unknown name, and the fire counters.
internal static class RuleRegistryTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    /// <summary>The repository root: the directory holding src/L5R.AI, from the working directory or above the test binary.</summary>
    private static string Root()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "L5R.AI"))) return dir.FullName;
        throw new Exception("src/L5R.AI not found from the working directory or the test binary");
    }

    private static Exception? Throws(Action act) { try { act(); return null; } catch (Exception e) { return e; } }

    public static readonly (string, Action)[] Tests =
    [
        ("T2Rules is the yardstick frozen on 2026-11-13: these 22 tokens in this order, and the Contender's TunedRules still equals it today", () =>
        {
            const string frozen = "econ,presence,lobby,shinden,openbow,ownprov,selflock,surereact,selfboost,nostraighten,provaim,spreadwide,conf,selfplayer,presdepth,pdh=100,keepup,nofoeboost,raid,pdstrong,defwin,nobowgift";
            Check(string.Join(",", BotContext.T2Rules) == frozen, $"T2Rules has changed; it is the campaign's yardstick and must not: {string.Join(",", BotContext.T2Rules)}");
            Check(BotContext.T2Rules.SequenceEqual(BotContext.TunedRules), "TunedRules has moved away from T2; the practice Contender and the yardstick now differ (allowed, but say so in docs/bots/CONTENDER.md)");
            var t2 = BotContext.Contender(rules: BotContext.T2Rules);
            Check(t2.Rules.SetEquals(BotContext.T2Rules) && t2.UseSureReactions && t2.UsePlanner, "the t2 arm carries the 22 rules, with surereact as the reaction flag and the planner on");
        }),
        ("a misspelt rule fails the run: Contender(rules: t2 + 'ecno') throws naming it, an unknown knob and a knob without a number too; 't2' in a list expands to the yardstick", () =>
        {
            var e = Throws(() => BotContext.Contender(rules: [.. BotContext.T2Rules, "ecno"]));
            Check(e is ArgumentException && e.Message.Contains("'ecno'"), $"an unknown rule must fail: {e?.Message ?? "no exception"}");
            e = Throws(() => BotContext.Contender(rules: ["econ", "pdq=100"]));
            Check(e is ArgumentException && e.Message.Contains("'pdq'"), $"an unknown knob must fail: {e?.Message ?? "no exception"}");
            e = Throws(() => BotContext.Contender(rules: ["econ", "pdh=high"]));
            Check(e is ArgumentException && e.Message.Contains("'pdh=high'"), $"a knob without a number must fail: {e?.Message ?? "no exception"}");
            e = Throws(() => BotContext.Contender(rules: ["econ", "ecno", "lkT=x"]));
            Check(e is ArgumentException && e.Message.Contains("'ecno'") && e.Message.Contains("'lkT=x'"), $"every offender is named: {e?.Message ?? "no exception"}");
            Check(Throws(() => BotContext.Contender(rules: ["pdh=100", "lkT=2.5", "look"])) is null, "a valid list with knobs passes");
            Check(Throws(() => BotContext.Conan()) is null && Throws(() => BotContext.Hard()) is null && Throws(() => new BotContext()) is null, "the named bots pass");
            var arm = BotContext.Contender(rules: ["t2", "read", "readA"]);
            Check(arm.Rules.SetEquals([.. BotContext.T2Rules, "read", "readA"]) && !arm.Rules.Contains("t2"), $"'t2' expands to the yardstick: {string.Join(",", arm.Rules)}");
            Check(BotContext.Contender(rules: ["pdh=100"]).Knob("pdh", 50) == 100 && BotContext.Contender(rules: ["econ"]).Knob("pdh", 50) == 50, "a knob reads its value, else its default");
        }),
        ("every name the bots read (Rule, Has, Knob, the factory's flags) is declared in RuleRegistry, and every declared name is read somewhere in src/L5R.AI", () =>
        {
            var dir = Path.Combine(Root(), "src", "L5R.AI");
            var read = new HashSet<string>(StringComparer.Ordinal); var knobs = new HashSet<string>(StringComparer.Ordinal);
            var ruleRx = new Regex("(?:Rule\\(x, |\\bHas\\(|Rules\\.Contains\\()\"([A-Za-z0-9_]+)\""); var knobRx = new Regex("Knob\\(x?,? ?[^\"\\n]*?\"([A-Za-z0-9_]+)\"(?:\\s*:\\s*\"([A-Za-z0-9_]+)\")?");
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                if (Path.GetFileName(file) == "RuleRegistry.cs") continue;
                foreach (Match m in ruleRx.Matches(text)) read.Add(m.Groups[1].Value);
                foreach (Match m in knobRx.Matches(text)) { knobs.Add(m.Groups[1].Value); if (m.Groups[2].Success) knobs.Add(m.Groups[2].Value); }
            }
            Check(read.Count > 80 && knobs.Count > 30, $"the scan found {read.Count} rules and {knobs.Count} knobs; the patterns no longer match the source");
            var declared = RuleRegistry.ByName;
            var unknownRules = read.Where(r => !declared.TryGetValue(r, out var e) || !e.Switch).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            var unknownKnobs = knobs.Where(k => !declared.TryGetValue(k, out var e) || !e.Knob).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            Check(unknownRules.Length == 0, $"rules read but not declared in RuleRegistry.cs: {string.Join(", ", unknownRules)}");
            Check(unknownKnobs.Length == 0, $"knobs read but not declared in RuleRegistry.cs: {string.Join(", ", unknownKnobs)}");
            var unread = RuleRegistry.Entries.Where(e => (e.Switch && !read.Contains(e.Name) || e.Knob && !knobs.Contains(e.Name)) && !e.Flag).Select(e => e.Name).ToArray();
            Check(unread.Length == 0, $"declared but read nowhere (remove them from RuleRegistry.cs or read them): {string.Join(", ", unread)}");
            Check(RuleRegistry.Entries.Count == RuleRegistry.Entries.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count(), "a name is declared twice");
            // every T2 token must be known, and the flags the factory consumes must be declared as flags
            foreach (var flag in new[] { "surereact", "trades", "battlepolicy", "noplanner", "notriage", "notargets", "noprinted", "pattack", "pdiscard", "reserve" })
                Check(declared.TryGetValue(flag, out var f) && f.Flag, $"{flag} is a factory flag and must be declared as one");
        }),
        ("fire counters: a T2 game counted (every decision re-decided per consulted rule) plays move for move as the uncounted game, and T2's rules show on and fired decisions", () =>
        {
            var lion = CustomPractice.Preset("Lion"); var crane = CustomPractice.Preset("Crane"); const ulong seed = 1_000_000UL + 9_900UL * 10_000UL + 7UL; var budget = TimeSpan.FromMinutes(5);
            Func<BotContext> plain = () => BotContext.Contender(rules: BotContext.T2Rules);
            Func<BotContext> counting = () => { var b = BotContext.Contender(rules: BotContext.T2Rules); b.CountFires = true; return b; };
            var a = BotTournament.Play("Lion preset", lion, "Crane preset", crane, [plain, plain], 0, seed, budget);
            var b = BotTournament.Play("Lion preset", lion, "Crane preset", crane, [counting, plain], 0, seed, budget);
            Check(a.Error is null && b.Error is null && a.Winner is not null, $"the game must finish: {a.Reason} {a.Error} / {b.Reason} {b.Error}");
            Check(a.Winner == b.Winner && a.Turn == b.Turn && a.Reason == b.Reason && a.Honor.SequenceEqual(b.Honor) && a.ProvincesLeft.SequenceEqual(b.ProvincesLeft), $"counting changed the game: {a.Winner}/{a.Turn}/{a.Reason} vs {b.Winner}/{b.Turn}/{b.Reason}");
            for (var seat = 0; seat < 2; seat++) Check(a.Actions[seat].Count == b.Actions[seat].Count && a.Actions[seat].All(kv => b.Actions[seat].GetValueOrDefault(kv.Key) == kv.Value), $"counting changed seat {seat}'s actions");
            Check(a.Fires is null && b.Fires is not null && b.Active is not null && b.Counted > 50, $"only the counted game carries fire counts ({b.Counted} decisions)");
            Check(b.Active!.Count >= 10 && b.Active.ContainsKey("econ") && b.Active.ContainsKey("presence"), $"T2's rules were consulted: {string.Join(",", b.Active.Keys)}");
            Check(b.Fires!.Values.Sum() > 0 && b.Fires.Keys.All(k => BotContext.T2Rules.Contains(k)), $"some rule fired, and only T2 tokens can: {string.Join(",", b.Fires.Select(kv => kv.Key + "=" + kv.Value))}");
            Check(b.Fires.All(kv => kv.Value <= b.Active.GetValueOrDefault(kv.Key)), "a rule fires only in decisions it was on in");
        }),
    ];
}
