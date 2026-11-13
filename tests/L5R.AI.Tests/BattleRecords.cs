using System.Text.Json;
using System.Text.RegularExpressions;
using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md, step 1 item 2): battle records. With L5R_TOURN_BATTLES=<dir>, the tournament writes one JSON line per declared attack: the public board
// when the first unit was assigned, every attacker and defender assignment, each estimator's number at that moment (the paper margin, the planner's EstimateAttack, BattlePolicy.Odds at the
// first Battle decision), the battle's actions, and the real result (resolution Forces, the Province, the units destroyed on each side). The harness writes it from the real match; no bot
// reads it. tools/bots/battles.py aggregates a directory of them (the paper-margin table, estimator calibration).
internal sealed class BattleRecorder(string dir, int challengerSeat)
{
    private static readonly Regex Resolved = new(@"^Province (\d+): attacking Force (-?\d+), defending Force (-?\d+)\.$");
    private static readonly Regex ProvinceGone = new(@"province (\d+) was destroyed\.$");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { IncludeFields = true };

    public sealed class Unit { public string Id = ""; public string Name = ""; public int Force; public int UnitForce; public bool Cavalry; public int Attachments; public string[] Spent = []; public string Segment = ""; }
    public sealed class Side { public int Honor; public int Hand; public int Provinces; public int Gold; public int Personalities; public int UnbowedForce; public int UnbowedPersonalities; public bool Favor; public string[] Home = []; }
    public sealed class Battlefield
    {
        public int Province; public int Strength; public List<Unit> Attackers = []; public List<Unit> Defenders = [];
        public int? PaperAtDeclaration; public int? PaperAtDefence; public AttackDiagnosis? Estimate; public int[]? Odds; // [own, enemy, strength] from the attacker's view at its first Battle decision
        public ReadSummary? ReadAtDeclaration; public ReadSummary? ReadAtDefence; // the Read's exchange from the attacker's view when it assigned, and from the defender's view when it assigned
        public List<string> Actions = []; // "seat:kind:abilityId" in order, the engine's own battle action log
        public bool Fought; public int? AttackingForce; public int? DefendingForce; public bool ProvinceDestroyed; public List<string> AttackersDestroyed = []; public List<string> DefendersDestroyed = [];
    }
    public sealed class ReadSummary { public bool Open; public bool Likely; public bool Worst; public int? Need; public int HaveAttacker; public int HaveDefender; public string Text = ""; }
    private static ReadSummary Summarise(MatchView view, int province, bool viewerAttacks)
    {
        var read = Read.Of(view); var me = read.Ledger.Me.At(province).ToArray(); var them = read.Ledger.Them.At(province).ToArray();
        var b = viewerAttacks ? read.Battle(province, me, them, true) : read.Battle(province, them, me, false);
        return new() { Open = b.Open.Falls, Likely = b.Likely.Falls, Worst = b.Worst.Falls, Need = b.Need, HaveAttacker = b.HaveAttacker, HaveDefender = b.HaveDefender, Text = b.ToString() };
    }

    public sealed class Attack
    {
        public ulong Seed; public int ChallengerSeat; public int Attacker; public int Turn; public string[] Decks = []; public Side[] Sides = [];
        public List<Battlefield> Battlefields = []; public int DefenderHomeUnbowedForce; public int? Winner; public string? Reason;
    }

    private readonly List<Attack> _attacks = [];
    private Attack? _current; private long _lastEvent = -1;

    private static bool InAttack(Phase phase) => phase is Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle;

    private static Unit Describe(MatchView view, CardView c, string segment)
    {
        var attached = view.Players.SelectMany(p => p.Home.Concat(p.Army)).Where(a => a.AttachedTo == c.Id).ToArray();
        return new()
        {
            Id = c.Id,
            Name = c.Definition?.Name ?? c.Id,
            Force = Math.Max(0, c.Force),
            UnitForce = c.UnitForce ?? Math.Max(0, c.Force) + attached.Where(a => a.Definition?.Type == CardType.Follower).Sum(a => Math.Max(0, a.Force)),
            Cavalry = (c.Definition?.Keywords?.Contains("Cavalry") ?? false) && attached.Where(a => a.Definition?.Type == CardType.Follower).All(a => a.Definition?.Keywords?.Contains("Cavalry") ?? false),
            Attachments = attached.Length,
            Spent = c.SpentAbilityIds ?? [],
            Segment = segment
        };
    }

    private static Side Snapshot(MatchView view, PlayerView p) => new()
    {
        Honor = p.Honor,
        Hand = p.HandCount,
        Provinces = p.Provinces.Count(q => !q.Destroyed),
        Gold = (p.Stronghold.Definition?.GoldProduction ?? 0) + p.Home.Where(h => h.Definition?.Type == CardType.Holding).Sum(h => h.Definition!.GoldProduction),
        Personalities = p.Home.Concat(p.Army).Count(c => c.Definition?.Type == CardType.Personality),
        UnbowedForce = BattlePolicy.Force(p.Home),
        UnbowedPersonalities = p.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed),
        Favor = view.FavorPlayerId == p.Id,
        Home = p.Home.Where(c => c.Definition?.Type is CardType.Personality).Select(c => $"{c.Definition!.Name}:{Math.Max(0, c.Force)}{(c.Bowed ? "*" : "")}").ToArray()
    };

    /// <summary>At the top of every decision: closes the attack record when the Attack Phase is over, and takes the attacker's Odds at its first Battle decision of each battlefield.</summary>
    public void Tick(MatchEngine game, MatchView v0)
    {
        if (_current is null) return;
        if (v0.TurnNumber != _current.Turn || !InAttack(v0.Phase)) { Close(); return; }
        if (v0.Phase != Phase.Battle) return;
        var province = v0.BattleStep?.BattlefieldProvince ?? v0.CurrentBattleProvince;
        if (province is not int p || _current.Battlefields.FirstOrDefault(b => b.Province == p) is not { } field) return;
        field.Fought = true;
        if (field.Odds is null && BattlePolicy.Assess(game.GetView(_current.Attacker)) is { } odds) field.Odds = [odds.Own, odds.Enemy, odds.Strength ?? -1];
        if (v0.BattleActions is { Length: > 0 } actions) field.Actions = actions.Select(a => $"{a.PlayerId}:{a.Kind}:{a.AbilityId}").ToList();
    }

    /// <summary>After every accepted command: assignments open and extend the record; the engine's events supply the results.</summary>
    public void Accepted(MatchEngine game, int seat, GameCommand command, MatchView before, BotContext[] memory, ulong seed, string[] decks)
    {
        if (command.Type == "attack" && command.Assignments is { Count: > 0 } assigned && before.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration)
        {
            var after = game.GetView(seat); var opponent = after.Players.Single(p => p.Id != seat);
            if (_current is null || _current.Turn != before.TurnNumber)
            {
                _current = new()
                {
                    Seed = seed,
                    ChallengerSeat = challengerSeat,
                    Attacker = seat,
                    Turn = before.TurnNumber,
                    Decks = decks,
                    Sides = before.Players.Select(p => Snapshot(before, p)).ToArray(),
                    DefenderHomeUnbowedForce = BattlePolicy.Force(before.Players.Single(p => p.Id != seat).Home)
                };
                _attacks.Add(_current);
            }
            var segment = before.Phase == Phase.CavalryDeclaration ? "cavalry" : "infantry";
            foreach (var group in assigned.GroupBy(a => a.Value))
            {
                var field = _current.Battlefields.FirstOrDefault(b => b.Province == group.Key);
                if (field is null) { field = new() { Province = group.Key, Strength = opponent.Provinces.FirstOrDefault(p => p.Index == group.Key)?.Strength ?? 0 }; _current.Battlefields.Add(field); }
                foreach (var id in group.Select(a => a.Key))
                    if (after.Players[seat].Army.FirstOrDefault(c => c.Id == id) is { } card && field.Attackers.All(u => u.Id != id)) field.Attackers.Add(Describe(after, card, segment));
                var committed = field.Attackers.Sum(u => u.UnitForce);
                field.PaperAtDeclaration = committed - BattlePolicy.Force(opponent.Home) - field.Strength; // the defender's unbowed home Force, before it assigns
                field.Estimate = TrainingOpponent.DiagnoseAttack(after, memory[seat], field.Attackers.Select(u => u.Id), group.Key);
                field.ReadAtDeclaration = Summarise(after, group.Key, viewerAttacks: true);
            }
        }
        else if (command.Type == "assignDefense" && command.Assignments is { Count: > 0 } defended && _current is not null && _current.Turn == before.TurnNumber)
        {
            var after = game.GetView(seat); var segment = before.Phase == Phase.CavalryDefense ? "cavalry" : "infantry";
            foreach (var group in defended.GroupBy(a => a.Value))
            {
                var field = _current.Battlefields.FirstOrDefault(b => b.Province == group.Key);
                if (field is null) { field = new() { Province = group.Key, Strength = after.Players[seat].Provinces.FirstOrDefault(p => p.Index == group.Key)?.Strength ?? 0 }; _current.Battlefields.Add(field); }
                foreach (var id in group.Select(a => a.Key))
                    if (after.Players[seat].Army.FirstOrDefault(c => c.Id == id) is { } card && field.Defenders.All(u => u.Id != id)) field.Defenders.Add(Describe(after, card, segment));
                field.PaperAtDefence = field.Attackers.Sum(u => u.UnitForce) - field.Defenders.Sum(u => u.UnitForce) - field.Strength;
                field.ReadAtDefence = Summarise(after, group.Key, viewerAttacks: false);
            }
        }
        if (_current is null) { _lastEvent = game.State.Events.Count == 0 ? -1 : game.State.Events[^1].Sequence; return; }
        var v = game.GetView(0); var current = v.BattleStep?.BattlefieldProvince ?? v.CurrentBattleProvince;
        foreach (var e in game.State.Events.Where(e => e.Sequence > _lastEvent))
        {
            _lastEvent = e.Sequence;
            switch (e.Type)
            {
                case "battleResolved" when Resolved.Match(e.Message) is { Success: true } m:
                    if (Field(int.Parse(m.Groups[1].Value) - 1) is { } f) { f.Fought = true; f.AttackingForce = int.Parse(m.Groups[2].Value); f.DefendingForce = int.Parse(m.Groups[3].Value); }
                    break;
                case "provinceDestroyed" when e.PlayerId != _current.Attacker && ProvinceGone.Match(e.Message) is { Success: true } m:
                    if (Field(int.Parse(m.Groups[1].Value) - 1) is { } g) g.ProvinceDestroyed = true;
                    break;
                case "unitDestroyed" when e.CardId is { } id:
                    var at = _current.Battlefields.FirstOrDefault(b => b.Attackers.Any(u => u.Id == id) || b.Defenders.Any(u => u.Id == id)) ?? (current is int c ? Field(c) : null);
                    if (at is null) break;
                    var name = at.Attackers.Concat(at.Defenders).FirstOrDefault(u => u.Id == id)?.Name ?? HumanReplayCompare.CardName(game, id);
                    if (e.PlayerId == _current.Attacker) at.AttackersDestroyed.Add(name); else at.DefendersDestroyed.Add(name);
                    break;
            }
        }
        Battlefield? Field(int province) => _current.Battlefields.FirstOrDefault(b => b.Province == province);
    }

    private void Close() { _current = null; }

    /// <summary>Writes the game's attacks, one JSON line each, with the game's result on every line.</summary>
    public void Finish(string fileStem, int? winner, string reason)
    {
        Close();
        if (_attacks.Count == 0) return;
        Directory.CreateDirectory(dir);
        foreach (var a in _attacks) { a.Winner = winner; a.Reason = reason; }
        File.WriteAllLines(Path.Combine(dir, fileStem + ".jsonl"), _attacks.Select(a => JsonSerializer.Serialize(a, Json)));
    }
}
