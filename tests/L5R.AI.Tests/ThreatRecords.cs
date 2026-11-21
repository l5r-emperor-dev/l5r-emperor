using System.Text.Json;
using System.Text.RegularExpressions;
using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md step 2, the threat answer's calibration): with L5R_TOURN_THREATS=<dir>, the tournament writes one JSON line per player turn: threat(home)
// as the Read projects it for that seat at three moments (when it declared its attack, with the units its exchange expects to destroy left out; at its last Dynasty decision, their army
// straightened in projection; at the first decision of their next turn, their army as it really straightened), two paper baselines (their total Force against my unbowed home Force and
// weakest Strength; the guard ratio of ARENA_BEHAVIOUR.md Q2c), and what then happened on their turn: whether they attacked, the Provinces this seat lost, the units each side lost.
// The harness writes it from the real match; no bot reads it. tools/bots/threats.py aggregates a directory of them.
internal sealed class ThreatRecorder(string dir, int challengerSeat)
{
    private static readonly Regex ProvinceGone = new(@"province (\d+) was destroyed\.$");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { IncludeFields = true };

    public sealed class Case { public int ProvincesLost; public int DefendersLost; public int AttackersLost; public int Tier; public int Margin; public string Attack = ""; public string Defence = ""; }
    public sealed class Projection
    {
        public int AttackForce; public int HomeForce; public int WeakestStrength; public int Candidates; public Case Open = new(); public Case Likely = new(); public Case Worst = new();
        public static Projection Of(ThreatRead t) => new() { AttackForce = t.AttackForce, HomeForce = t.HomeForce, WeakestStrength = t.WeakestStrength, Candidates = t.Candidates, Open = C(t.Open), Likely = C(t.Likely), Worst = C(t.Worst) };
        private static Case C(ThreatCase c) => new() { ProvincesLost = c.ProvincesLost, DefendersLost = c.DefendersLost, AttackersLost = c.AttackersLost, Tier = c.Tier, Margin = c.Margin, Attack = c.Attack, Defence = c.Defence };
    }
    public sealed class Turn
    {
        public ulong Seed; public int ChallengerSeat; public int Seat; public int TurnNumber; public string[] Decks = [];
        public Projection? AtDeclaration; public Projection? AtEnd; public Projection? AtStart;
        public int PaperMargin; public double GuardRatio; public int HomeUnbowed; public int HomeUnbowedForce; public int TheirUnits; public int TheirForce; public int BeliefSeen; public int TheirHand; public int MyHand; public int MyGold; public int TheirGold; public int ProvincesBefore; public int TheirProvinces;
        public bool NextTurnPlayed; public bool Attacked; public int ProvincesLost; public int DefendersLost; public int AttackersLost; public int? Winner; public string? Reason;
    }

    private readonly List<Turn> _turns = [];
    private Turn? _playing;   // the turn being played
    private Turn? _watching;  // the previous turn, whose opponent's turn is now being watched for what it did
    private MatchView? _lastDynastyView; private int _lastTurn = -1; private long _lastEvent = -1;

    private static Projection Baselines(MatchView view, Turn t, bool projectStraighten)
    {
        var read = Read.Of(view);
        var home = Threat.Defenders(read.Ledger.Me.Units); var theirs = projectStraighten ? Threat.Straightened(read.Ledger.Them.Units) : read.Ledger.Them.Home.Where(u => !u.Bowed).ToArray();
        t.HomeUnbowed = home.Length; t.HomeUnbowedForce = home.Sum(u => u.Contribution); t.TheirUnits = theirs.Length; t.TheirForce = theirs.Sum(u => u.UnitForce);
        t.PaperMargin = t.TheirForce - t.HomeUnbowedForce - (read.Ledger.Me.Provinces.Length == 0 ? 0 : read.Ledger.Me.Provinces.Min(p => p.Strength));
        t.GuardRatio = t.TheirForce == 0 ? 9.99 : Math.Round((double)t.HomeUnbowedForce / t.TheirForce, 3);
        t.BeliefSeen = read.Belief.Seen; t.TheirHand = read.Ledger.Them.HandCount; t.MyHand = read.Ledger.Me.HandCount; t.MyGold = read.Ledger.Me.GoldNow; t.TheirGold = read.Ledger.Them.GoldCapacity;
        t.ProvincesBefore = read.Ledger.Me.Provinces.Length; t.TheirProvinces = read.Ledger.Them.Provinces.Length;
        return Projection.Of(Threat.Of(read, home, theirs));
    }

    /// <summary>At the top of every decision: a new turn closes the watched turn, projects the played turn's threat from its board after its last Dynasty decision and starts watching it, and opens
    /// the new turn; the first decision of the watched turn past Straighten takes the threat from the board as it really is.</summary>
    public void Tick(MatchEngine game, MatchView v0)
    {
        if (v0.TurnNumber != _lastTurn)
        {
            _lastTurn = v0.TurnNumber; _watching = null;
            if (_playing is not null && _playing.TurnNumber == v0.TurnNumber - 1 && _playing.Seat != v0.ActivePlayerId)
            {
                if (_lastDynastyView is not null) _playing.AtEnd = Baselines(_lastDynastyView, _playing, projectStraighten: true);
                _playing.NextTurnPlayed = true; _watching = _playing;
            }
            _lastDynastyView = null;
            _playing = new() { ChallengerSeat = challengerSeat, Seat = v0.ActivePlayerId, TurnNumber = v0.TurnNumber };
            _turns.Add(_playing);
        }
        if (_watching is not null && _watching.AtStart is null && v0.Phase != Phase.Straighten) _watching.AtStart = Baselines(game.GetView(_watching.Seat), _watching, projectStraighten: false); // their army as it really straightened; the baselines are taken here too
        if (_playing is not null && v0.ActivePlayerId == _playing.Seat && v0.Phase is Phase.Dynasty or Phase.DynastyDiscard or Phase.End) _lastDynastyView = game.GetView(_playing.Seat); // before each Dynasty decision
    }

    /// <summary>After every accepted command: the attacker's declaration projects the threat with its expected kills left out; the watched turn's events supply what the opponent did.</summary>
    public void Accepted(MatchEngine game, int seat, GameCommand command, MatchView before, ulong seed, string[] decks)
    {
        foreach (var t in new[] { _playing, _watching }) if (t is not null && t.Seed == 0) { t.Seed = seed; t.Decks = decks; }
        // the seat's board after each of its Dynasty decisions while the turn is still its own: the last such view holds what it bought (a buy that ends the phase is the one view this misses)
        if (_playing is not null && seat == _playing.Seat && before.Phase is Phase.Dynasty or Phase.DynastyDiscard or Phase.End && game.GetView(seat) is { } after && after.TurnNumber == before.TurnNumber) _lastDynastyView = after;
        if (command.Type == "attack" && command.Assignments is { Count: > 0 } && before.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration)
        {
            if (_watching is not null && seat != _watching.Seat) _watching.Attacked = true;
            if (_playing is not null && seat == _playing.Seat && _playing.AtDeclaration is null)
            {
                var view = game.GetView(seat); var read = Read.Of(view);
                var expected = read.Battles().SelectMany(b => b.Likely.DefendersDestroyed).ToArray();
                _playing.AtDeclaration = Projection.Of(Threat.Of(read, Threat.Defenders(read.Ledger.Me.Units), Threat.Straightened(read.Ledger.Them.Units, expected)));
            }
        }
        if (_watching is null) { _lastEvent = game.State.Events.Count == 0 ? -1 : game.State.Events[^1].Sequence; return; }
        foreach (var e in game.State.Events.Where(e => e.Sequence > _lastEvent))
        {
            _lastEvent = e.Sequence;
            switch (e.Type)
            {
                case "provinceDestroyed" when e.PlayerId == _watching.Seat && ProvinceGone.IsMatch(e.Message): _watching.ProvincesLost++; break;
                case "unitDestroyed" when e.CardId is not null: if (e.PlayerId == _watching.Seat) _watching.DefendersLost++; else _watching.AttackersLost++; break;
            }
        }
    }

    private void Close() { _playing = null; _watching = null; }

    /// <summary>Writes the game's turns, one JSON line each, with the game's result on every line. A turn whose opponent's next turn never came is written with NextTurnPlayed false.</summary>
    public void Finish(string fileStem, int? winner, string reason)
    {
        Close();
        var turns = _turns.Where(t => t.AtEnd is not null || t.AtStart is not null || t.AtDeclaration is not null).ToList();
        if (turns.Count == 0) return;
        Directory.CreateDirectory(dir);
        foreach (var t in turns) { t.Winner = winner; t.Reason = reason; }
        File.WriteAllLines(Path.Combine(dir, fileStem + ".jsonl"), turns.Select(t => JsonSerializer.Serialize(t, Json)));
    }
}
