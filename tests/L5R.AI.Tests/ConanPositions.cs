using System.Text;
using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md, step 1 item 4): the decision test set. Each position is one of Mendon's decisions in an exported practice replay
// (docs/bots/replays/), chosen because the bot's move there cost most (docs/bots/strategy/PLAYER_MODEL.md section 7, CONAN_CAMPAIGN_BRIEF.md section 6.4) or because his
// own move was a mistake (PLAYER_MODEL.md section 6: those positions carry the correct move, never his). Any rule set is replayed over all of them in seconds, as
// human-compare replays a whole game: the match is rebuilt from the replay and stepped command by command; before each seat-0 decision the bot is asked (same
// JSON-round-tripped view and actions, one memory per replay), and at a listed position its answer is judged against the expected move. A search bot gets its Sampler only
// at the listed positions, so the rest of the replay costs nothing. Agreement is a metric, never a proof: a bot that wins more and agrees less is still a win.
//   L5R_POS_ARMS="t2;conan;name:rule,rule;name:+rule"   the arms to run (default t2). t2 = the Contender's tuned rules; conan = Conan-best (t2 + look,lkDef,lkVar,lkPlan,lkB=600,lkT=60);
//                                                      hard = the practice Hard level; name:rules = the Contender with exactly those rules; name:+rules = t2 plus those rules.
//   L5R_POS_LOG=1                                       also print the search's own log at each searched position.
//   L5R_POS_FAMILY=D                                    only the positions of one family.
//   L5R_POS_READ=1                                      at every attack and defence position, also print the Read's exchange (need, have, open/likely/worst) from the human's seat (brief step 2 gate).
//   dotnet run -c Release --project tests/L5R.AI.Tests -- --filter conan-positions
internal static class ConanPositions
{
    /// <summary>One decision. Nth counts the seat-0 decisions with a real choice (more than one legal option) in that turn and phase, from 1, as the compare log's ">>" lines do.
    /// Expect null: the human's logged command (HumanReplayCompare.Key equality). Otherwise " &amp; "-separated fragments the bot's described command must contain ("!" = must not).</summary>
    public sealed record Position(string Id, string Family, string Replay, int Turn, Phase Phase, int Nth, string? Expect, string Source, string Note);

    public static readonly string[] Conan = ["look", "lkDef", "lkVar", "lkPlan", "lkB=600", "lkT=60"]; // Conan-best, the brief's section 3

    private const string G1 = "practice-20261109-133956", G2 = "practice-20261109-141352", G3 = "practice-20261109-172814", G4 = "practice-20261109-184403",
        G5 = "practice-20261109-195056", G6 = "practice-20261109-201243", G7 = "practice-20261110-173816", GT = "2026-11-11-lion-vs-mantis-tomaru-missed-ranged-kill";

    // Families as RULE_FAMILIES.md: A attack and shape, B Cavalry, C home, D defence, E inside a battle, F Dynasty, G posture, I the Favor and the hand.
    public static readonly Position[] All =
    [
        // E: late Force added after the defender passed (PM 7 row 1; brief 6.4 item 1). Judged on the Tactical Advantage itself; which card is discarded is a separate, smaller choice
        new("E-G3T3-lateforce", "E", G3, 3, Phase.Battle, 1, "useAbility/tactical-advantage", "PM7r1", "Tactical Advantage discarding Defensive Stance (8 v 7, raze); the Contender passed at 4 v 7"),
        new("E-G3T9-lateforce", "E", G3, 9, Phase.Battle, 3, "useAbility/tactical-advantage", "PM7r1", "Second battle, Province 1: Tactical Advantage (7 v 7 fails without it)"),
        new("E-G7T3-lateforce", "E", G7, 3, Phase.Battle, 2, "useAbility/tactical-advantage", "PM7r1", "Tactical Advantage discarding Incapacitated (9 v 7)"),
        new("E-G4T11-army", "E", G4, 11, Phase.Battle, 2, null, "PM7r1", "The army +5 ability (12 v 7) instead of passing at 7 v 7"),
        new("E-GT5-lateforce-a", "E", GT, 5, Phase.Battle, 1, "useAbility/tactical-advantage", "PM7r1", "Tactical Advantage at the marked Province, below Strength at declaration"),
        new("E-GT5-lateforce-b", "E", GT, 5, Phase.Battle, 5, "useAbility/tactical-advantage", "PM7r1", "Tactical Advantage in the second battle"),
        // A: one group per Province (PM 7 row 2; brief 6.4 item 3)
        new("A-G3T9-split", "A", G3, 9, Phase.InfantryDeclaration, 1, null, "PM7r2", "Uehara to Province 0, Furu to Province 3: two razes; the Contender stacked three at one"),
        new("A-G4T9-split", "A", G4, 9, Phase.InfantryDeclaration, 1, null, "PM7r2", "One unit to Province 3, two to Province 2"),
        new("A-G6T13-split", "A", G6, 13, Phase.InfantryDeclaration, 1, null, "PM7r2", "Dairuko alone at Province 0, five at Province 3"),
        new("A-GT5-split", "A", GT, 5, Phase.InfantryDeclaration, 1, null, "PM7r2", "Two Tacticians at the marked Province, the Sword-bearer at another"),
        new("A-G7T7-split", "A", G7, 7, Phase.InfantryDeclaration, 1, null, "PM3.2", "Suoh at Province 3, Kamina and the Ancestor at Province 1"),
        new("A-GT7-split", "A", GT, 7, Phase.InfantryDeclaration, 1, null, "PM3.2", "Two armies, 9F and 10F, against Strength 7 each"),
        // A: the immune unit alone (PM 7 row 3)
        new("A-G6T11-alone", "A", G6, 11, Phase.InfantryDeclaration, 1, "Akodo Dairuko> & !Akodo Uehara & !Akodo Furu & !Lion Ancestor", "PM7r3", "Chi 6 Dairuko alone into Crane's duels; the Contender sent three at two Provinces"),
        new("A-G6T9-alone", "A", G6, 9, Phase.InfantryDeclaration, 1, "Akodo Dairuko> & !Akodo Kamina", "PM3.1", "11F into 13 with the unit their duels cannot beat"),
        // A/G: hold (PM 7 rows 9 and 10)
        new("A-G4T5-hold", "A", G4, 5, Phase.AttackDeclaration, 1, null, "PM7r9", "Hold at 6F v Strength 7; attacking bows the unit holding the defensive Spell"),
        new("G-G5T12-hold", "G", G5, 12, Phase.AttackDeclaration, 1, null, "PM7r10", "Honor-drain route: no attack; the Contender recommended eleven"),
        new("G-G5T20-hold", "G", G5, 20, Phase.AttackDeclaration, 1, null, "PM7r10", "Drain route, no attack"),
        new("G-G5T22-hold", "G", G5, 22, Phase.AttackDeclaration, 1, null, "PM7r10", "Drain route, no attack"),
        new("G-G5T24-hold", "G", G5, 24, Phase.AttackDeclaration, 1, null, "PM7r10", "Drain route, no attack"),
        // D: defend to kill with every unit of 2F or more (PM 7 row 4; brief 6.4 item 5)
        new("D-G2T10-kill", "D", G2, 10, Phase.DefenseAssignment, 1, null, "PM7r4", "Three bodies against two attackers; destroys a Personality. The Contender sent one 0F body"),
        new("D-G2T12-kill", "D", G2, 12, Phase.DefenseAssignment, 1, null, "PM7r4", "Six bodies against four attackers; destroys three"),
        new("D-G1T9-kill", "D", G1, 9, Phase.DefenseAssignment, 1, null, "PM7r4", "Ryoko and Chikako; the Contender's lone weak body forfeited 12 Honor"),
        // D: the fewest bodies that punish targeting (PM 7 row 5; brief 6.4 item 8)
        new("D-G5T13-minimum", "D", G5, 13, Phase.DefenseAssignment, 1, null, "PM7r5", "Two reaction bodies at a 0-Strength Province; the Contender wanted seven"),
        new("D-G5T25-minimum", "D", G5, 25, Phase.DefenseAssignment, 1, null, "PM7r5", "Two bodies; the Contender wanted fifteen"),
        // D: concede, kill next turn (PM 7 row 6; brief 6.4 item 10): nothing at the attacked Province
        new("D-G3T10-concede", "D", G3, 10, Phase.DefenseAssignment, 1, "!>1", "PM7r6", "Defend 8F against 9F? No: the units stay out of Province 1 and kill next turn"),
        new("D-G7T10-concede", "D", G7, 10, Phase.DefenseAssignment, 1, "!>0", "PM7r6", "Defend 6F against 14F? No"),
        new("D-GT6-concede", "D", GT, 6, Phase.Battle, 1, null, "PM3.3", "All units bowed; pass rather than move a Personality in to save the Province and lose him"),
        // E: pass or kill in a won battle, never the send-home (PM 7 row 7; brief 6.4 items 4 and 6)
        new("E-G7T7-pass-a", "E", G7, 7, Phase.Battle, 1, null, "PM7r7", "Won on Force: pass (the Contender moved a defender home)"),
        new("E-G7T7-pass-b", "E", G7, 7, Phase.Battle, 3, null, "PM7r7", "Second battle, won: pass after Deception Revealed and Broken Alliance"),
        new("E-G6T11-kill", "E", G6, 11, Phase.Battle, 2, null, "PM7r7", "Dairuko's Melee kills the attacker; the Contender spent the Favor to send him home"),
        new("E-G1T9-favor", "I", G1, 9, Phase.Battle, 3, null, "PM7r7", "Pass holding the Favor after the -3 penalty; both attackers die with it unspent"),
        new("E-GT7-kill", "E", GT, 7, Phase.Battle, 2, null, "PM7r7", "Tactical Assault destroys the lone defender; the Contender's Incapacitated sends him home"),
        // E: order (PM 3.5): first the effect that disables the unit carrying their abilities
        new("E-G6T6-order", "E", G6, 6, Phase.Battle, 1, null, "PM3.5", "Control the Board bows the duelist first, which turns off every duel ability on him"),
        // E: duels, strike at once (PM 7 row 8)
        new("E-G6T9-strike", "E", G6, 9, Phase.Battle, 2, null, "PM7r8", "Higher Chi, first to act: strike, focus nothing"),
        new("E-G6T11-strike", "E", G6, 11, Phase.Battle, 1, null, "PM7r8", "Strike at once"),
        // E: target the larger body (brief 6.4 item 7)
        new("E-G7T5-target", "E", G7, 5, Phase.Battle, 1, null, "6.4i7", "Tactical Assault on Katagi (the bigger body), not Meiyu"),
        // F: attachments and purchases (brief 6.4 item 7; PM 7 row 12)
        new("F-G7T5-equip", "F", G7, 5, Phase.Action, 1, null, "6.4i7", "The Follower on the body without a Sword"),
        new("F-G1T4-body", "F", G1, 4, Phase.Dynasty, 1, null, "PM7r12", "Personality first, then Holdings; the Contender bought the Holding"),
        new("F-G1T6-body", "F", G1, 6, Phase.Dynasty, 1, null, "PM7r12", "Personality first"),
        new("F-G2T1-body", "F", G2, 1, Phase.Dynasty, 1, null, "PM7r12", "Kenshi before the Large Farm on turn 1"),
        // I: keep the hand (PM 7 row 11), the discard (brief 6.4 item 8)
        new("I-G3T5-keephand", "I", G3, 5, Phase.Events, 1, null, "PM7r11", "A Dangerous Game: keep the hand (the Contender discards and draws four)"),
        new("I-G7T7-keephand", "I", G7, 7, Phase.Events, 1, null, "PM7r11", "A Dangerous Game: keep the hand"),
        new("I-G5T12-discard", "I", G5, 12, Phase.End, 1, null, "6.4i8", "Keep the steal and the kill; discard the situational cards"),
        new("I-G5T20-discard", "I", G5, 20, Phase.End, 1, null, "6.4i8", "Discard the situational card"),
        new("I-G5T22-discard", "I", G5, 22, Phase.End, 1, null, "6.4i8", "Discard the situational card"),
        new("I-G5T24-discard", "I", G5, 24, Phase.End, 1, null, "6.4i8", "Discard the situational card"),
        // G: +2 Honor plays at 36 Honor with four Provinces, not destroying an own Province for 4 (brief 6.4 item 9)
        new("G-G1T10-honor", "G", G1, 10, Phase.Action, 1, "!ebisu-destroy-province", "6.4i9", "Anything but destroying an own Province for Honor at 36 with four Provinces"),
        // B: the Cavalry segment, one self-sufficient unit into the held Favor (PM 5 item 1): a single instance, proposed
        new("B-G1T6-cavalry", "B", G1, 6, Phase.CavalryDeclaration, 1, "Utaku Ryoko> & !Utaku Tsukiko", "PM5i1", "Ryoko alone in the Cavalry segment after no infantry defender appeared; Tsukiko stays home"),
        // His mistakes (PM 6): the correct move, not his
        new("E-G6T6-melee", "E", G6, 6, Phase.Battle, 2, "akodo-guidance-melee", "PM6", "The Melee 4 kills the 0F attacker and keeps Kamina; he passed into a 0 v 0 tie"),
        new("E-G7T5-nopass", "E", G7, 5, Phase.Battle, 4, "!pass", "PM6", "10 v 3 + 7 with three cards that each take the Province: play one, do not pass"),
        new("E-G4T9-select", "E", G4, 9, Phase.BattleSelection, 1, "selectBattle", "PM6", "Fight the battle: passing all battles forfeited a Province with the +5 unused"),
        new("E-G3T7-sendhome", "E", G3, 7, Phase.Battle, 4, "tactical-trap-move", "PM6", "The winning send-home at 14 v 15, instead of a pass"),
    ];

    public static readonly (string, Action)[] Tests =
    [
        ("conan-positions: Mendon's decisions from his replays, re-decided by any rule set (L5R_POS_ARMS; see ConanPositions.cs)", () => Console.WriteLine(Run(Arms(), Environment.GetEnvironmentVariable("L5R_POS_FAMILY"))))
    ];

    /// <summary>The arms named by L5R_POS_ARMS (default t2).</summary>
    public static (string Name, Func<BotContext> Make)[] Arms()
    {
        var spec = Environment.GetEnvironmentVariable("L5R_POS_ARMS") is { Length: > 0 } s ? s : "t2";
        return spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(a =>
        {
            var colon = a.IndexOf(':');
            var name = colon < 0 ? a : a[..colon]; var rules = colon < 0 ? null : a[(colon + 1)..];
            Func<BotContext> make = name switch
            {
                "t2" when rules is null => () => BotContext.Contender(),
                "conan" when rules is null => () => BotContext.Contender(rules: [.. BotContext.TunedRules, .. Conan]),
                "hard" when rules is null => () => BotContext.Hard(),
                _ when rules is not null && rules.StartsWith('+') => () => BotContext.Contender(rules: [.. BotContext.TunedRules, .. rules[1..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]),
                _ when rules is not null => () => BotContext.Contender(rules: rules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                _ => throw new Exception($"unknown arm '{a}': use t2, conan, hard, name:rules or name:+rules")
            };
            return (name, make);
        }).ToArray();
    }

    public sealed record Verdict(Position Position, string Human, string? Bot, bool? Agree, string[] Look); // Agree null: the position was not reached

    public static string Run((string Name, Func<BotContext> Make)[] arms, string? family = null)
    {
        var positions = All.Where(p => family is null || p.Family == family).ToArray();
        var log = Environment.GetEnvironmentVariable("L5R_POS_LOG") == "1";
        var sb = new StringBuilder();
        var results = new Dictionary<string, List<Verdict>>();
        foreach (var (name, make) in arms)
        {
            var verdicts = new List<Verdict>();
            foreach (var replay in positions.Select(p => p.Replay).Distinct())
                verdicts.AddRange(Decide(replay, positions.Where(p => p.Replay == replay).ToArray(), make, log, sb));
            results[name] = verdicts;
        }
        sb.AppendLine();
        sb.AppendLine($"| id | family | Mendon | {string.Join(" | ", arms.Select(a => a.Name))} |");
        sb.AppendLine($"|---|---|---|{string.Concat(arms.Select(_ => "---|"))}");
        foreach (var p in positions)
        {
            var cells = arms.Select(a => results[a.Name].First(v => v.Position.Id == p.Id)).ToArray();
            var human = cells[0].Human;
            sb.AppendLine($"| {p.Id} | {p.Family} | {Short(human)} | {string.Join(" | ", cells.Select(c => c.Agree is null ? "unreached" : (c.Agree.Value ? "agree" : "DIFFER: " + Short(c.Bot ?? "(no command)"))))} |");
        }
        sb.AppendLine();
        foreach (var (name, _) in arms)
        {
            var v = results[name]; var reached = v.Where(x => x.Agree is not null).ToArray();
            sb.AppendLine($"{name}: agreed {reached.Count(x => x.Agree == true)} of {reached.Length} reached positions ({positions.Length - reached.Length} unreached); by family: " +
                string.Join(", ", reached.GroupBy(x => x.Position.Family).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count(x => x.Agree == true)}/{g.Count()}")));
        }
        return sb.ToString();
    }

    private static string Short(string s) => s.Length > 70 ? s[..70] + "…" : s;

    /// <summary>Replays one game, asking the bot before every seat-0 decision, and judges it at the listed positions.</summary>
    private static IEnumerable<Verdict> Decide(string replayName, Position[] positions, Func<BotContext> make, bool log, StringBuilder notes)
    {
        var path = Path.Combine("docs", "bots", "replays", replayName + ".json");
        var replay = System.Text.Json.JsonSerializer.Deserialize<ReplayRecord>(File.ReadAllText(path), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? throw new Exception("not a replay: " + path);
        var game = MatchEngine.Replay(replay with { ContentVersion = MatchEngine.ContentVersion, Commands = [] });
        var bot = make();
        if (replay.CustomDecks is { Length: 2 } decks) bot.Archetype = DeckArchetypeTests.Of(decks[0]);
        var searches = bot.Has("look");
        var found = new Dictionary<string, Verdict>();
        var counts = new Dictionary<(int, Phase), int>();
        foreach (var command in replay.Commands)
        {
            var v0 = game.GetView(0);
            if (command.PlayerId == 0)
            {
                var offered = game.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").ToArray();
                var forced = offered is [{ Command: { } only }] && HumanReplayCompare.Key(only) == HumanReplayCompare.Key(command);
                Position? at = null;
                if (!forced)
                {
                    var key = (v0.TurnNumber, v0.Phase); counts[key] = counts.GetValueOrDefault(key) + 1;
                    at = positions.FirstOrDefault(p => p.Turn == v0.TurnNumber && p.Phase == v0.Phase && p.Nth == counts[key]);
                }
                var live = game; var before = bot.LookLog.Count;
                bot.Sampler = at is not null && searches ? sample => live.Sample(0, sample) : null;
                var answer = HumanReplayCompare.Ask(game, 0, bot, roundTrip: true);
                if (at is not null)
                {
                    if (Environment.GetEnvironmentVariable("L5R_POS_READ") == "1" && v0.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration or Phase.DefenseAssignment or Phase.CavalryDefense or Phase.Battle or Phase.AttackDeclaration)
                        notes.AppendLine($"  {at.Id} read: {ReadAt(game, command)}");
                    var human = HumanReplayCompare.Describe(game, command); var described = answer is null ? null : HumanReplayCompare.Describe(game, answer);
                    var agree = answer is not null && Matches(at, command, answer, described!);
                    var look = bot.LookLog.Skip(before).ToArray();
                    found[at.Id] = new(at, human, described, agree, look);
                    if (log && look.Length > 0) foreach (var l in look) notes.AppendLine($"  {at.Id} look: {l}");
                }
            }
            var result = game.Execute(command);
            if (!result.Accepted) { notes.AppendLine($"NOTE {replayName}: command refused at T{v0.TurnNumber} {v0.Phase} ({HumanReplayCompare.Describe(game, command)}): {result.Error}; later positions unreached"); break; }
        }
        foreach (var p in positions) yield return found.TryGetValue(p.Id, out var v) ? v : new(p, "?", null, null, []);
    }

    /// <summary>The Read's exchange at the position, from seat 0: for an assignment command, the battle the human's own assignment makes (his units against the defenders at home, or his defenders against the attackers there);
    /// for a battle in progress, the current battlefield.</summary>
    private static string ReadAt(MatchEngine game, GameCommand command)
    {
        var view = game.GetView(0); var read = Read.Of(view);
        if (command.Type == "attack" && command.Assignments is { Count: > 0 } asg)
            return string.Join("; ", asg.GroupBy(a => a.Value).Select(g =>
            {
                var attackers = read.Ledger.Me.Units.Where(u => g.Any(a => a.Key == u.Id)).ToArray(); var defenders = read.Ledger.Them.Home.Where(u => !u.Bowed).ToArray();
                return $"to P{g.Key + 1} vs all unbowed defenders: {read.Battle(g.Key, attackers, defenders, true)}";
            }));
        if (command.Type == "assignDefense" && command.Assignments is { Count: > 0 } def)
            return string.Join("; ", def.GroupBy(a => a.Value).Select(g =>
            {
                var defenders = read.Ledger.Me.Units.Where(u => g.Any(a => a.Key == u.Id)).ToArray(); var attackers = read.Ledger.Them.At(g.Key).ToArray();
                return $"at P{g.Key + 1}: {read.Battle(g.Key, attackers, defenders, false)}";
            }));
        if (command.Type is "pass" or "passAllBattles" && view.Phase is Phase.DefenseAssignment or Phase.AttackDeclaration)
            return string.Join("; ", read.Ledger.Them.Units.Where(u => u.Province is not null).GroupBy(u => u.Province!.Value).Select(g => $"undefended P{g.Key + 1}: {read.Battle(g.Key, g.ToArray(), [], false)}")
                .DefaultIfEmpty($"home: mine {read.Ledger.Me.UnbowedHomeForce}F unbowed, theirs {read.Ledger.Them.UnbowedHomeForce}F; belief {read.Belief}"));
        return string.Join("; ", read.Battles().Select(b => b.ToString()).DefaultIfEmpty("no battlefield"));
    }

    private static bool Matches(Position p, GameCommand human, GameCommand bot, string described)
    {
        if (p.Expect is null) return HumanReplayCompare.Key(human) == HumanReplayCompare.Key(bot);
        foreach (var raw in p.Expect.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var negate = raw.StartsWith('!'); var fragment = negate ? raw[1..] : raw;
            if (described.Contains(fragment, StringComparison.Ordinal) == negate) return false;
        }
        return true;
    }
}
