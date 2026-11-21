using Godot;
using L5R.Core;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private bool _matchReview;
    private string _matchProbeScenario = "steel";
    private int _matchProbeSeat;
    private int _matchProbeStep, _matchProbeSeatStep;
    private double _matchProbeClock, _matchProbeTotal;
    private string? _matchProbeReplay;
    private long _matchProbeVersion;
    private int _matchProbeFilm;
    private readonly HashSet<string> _matchProbeShots = [];
    private readonly List<string> _matchProbeChecks = [];
    private bool _matchProbeResized, _matchProbeSkip, _matchProbeTableClick;
    private void StartMatchDuelCapture()
    {
        _matchReview = true; _autoOpponent = _capturePath is null;
        _preferences.ReducedMotion = _reducedMotion = _preferences.ReducedMotion || OS.GetCmdlineUserArgs().Contains("--duel-match-reduced");
        _matchProbeScenario = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--duel-match-scenario="))?[22..] ?? "steel";
        _matchProbeSeat = OS.GetCmdlineUserArgs().Contains("--duel-match-lion") ? 1 : 0;
        // Portable review launchers use existing capture-mode plumbing, but stay interactive.
        var packagedFollowup = _captureMode.StartsWith("duel-followup-", StringComparison.Ordinal);
        if (packagedFollowup)
        {
            var scenario = _captureMode[14..];
            if (scenario.EndsWith("-opponent", StringComparison.Ordinal)) { _matchProbeSeat = 1; scenario = scenario[..^9]; }
            if (scenario.EndsWith("-reduced", StringComparison.Ordinal)) { _preferences.ReducedMotion = _reducedMotion = true; scenario = scenario[..^8]; }
            _matchProbeScenario = scenario == "concede" ? "concede" : "choice-" + scenario;
            _capturePath = null;
        }
        _table.ReducedMotion = _timeline.ReducedMotion = _reducedMotion;
        _session.StartDuelDemo(_matchProbeScenario == "concede" ? "steel" : _matchProbeScenario, 205, _matchProbeSeat); _matchDuelFlow.Observe(_session.Snapshot);
        EnterTable(); _turnBloom.Cancel();
        if (packagedFollowup || OS.GetCmdlineUserArgs().Contains("--duel-followup-review"))
        {
            _autoOpponent = false; _followupPreparing = true; _ = _session.BeginDuelDemo(); return;
        }
        if (_capturePath is null)
        {
            var reviewLayer = new CanvasLayer { Layer = 90 }; AddChild(reviewLayer);
            var button = SidebarSkin.Button("Begin prepared duel", () => { reviewLayer.QueueFree(); _ = _session.BeginDuelDemo(); }, true);
            button.Position = new Vector2(30, 110); button.Size = new Vector2(285, 55); reviewLayer.AddChild(button);
        }
    }
    private bool ProcessMatchDuelCapture(double delta)
    {
        try { return RunMatchDuelCapture(delta); }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); return true; }
    }
    private bool RunMatchDuelCapture(double delta)
    {
        if (ProcessDuelFollowupReview(delta)) return true;
        if (!_matchReview || _capturePath is null) return false;
        _matchProbeTotal += delta; _matchProbeClock += delta;
        if (_matchProbeTotal > 80) throw new InvalidOperationException($"Round-trip probe timed out: step {_matchProbeStep}, {_matchDuel?.Screen.CinematicPhase}");
        RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
        var film = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--duel-match-film="))?[18..];
        if (film is not null) { System.IO.Directory.CreateDirectory(film); GetViewport().GetTexture().GetImage().SaveJpg(System.IO.Path.Combine(film, $"{_matchProbeFilm++:D5}.jpg"), .96f); }
        var screen = _matchDuel?.Screen;
        if (screen is not null && !_matchDuelPreparing)
        {
            var phase = screen.CinematicPhase;
            if (phase == "opening-challenger" && screen.CinematicTime > 4.45 || phase == "opening-answer" && screen.CinematicTime > 5.35) Shot(phase);
        }
        if (_matchDuelTransition is { Running: true } transition)
        {
            if (transition.Elapsed > .9) Shot(_matchDuelReturning ? "return" : "entrance");
            if (OS.GetCmdlineUserArgs().Contains("--duel-match-resize") && !_matchProbeResized && transition.Elapsed > .7)
            { _matchProbeResized = true; GetWindow().Size = new Vector2I(1440, 900); }
            if (OS.GetCmdlineUserArgs().Contains("--duel-match-skip") && !_matchProbeSkip && transition.Elapsed > .8)
            { _matchProbeSkip = true; NativeKey(Key.Space); }
        }
        if (_matchDuelPreparing || _matchDuelReturning || screen?.PresentationBusy == true) { _matchProbeClock = 0; return true; }
        if (_matchProbeClock < .6) return true;
        _matchProbeClock = 0;
        if (_matchProbeScenario != "steel" || _matchProbeSeat == 1) return RunMatchDuelScenario();
        switch (_matchProbeStep)
        {
            case 0:
                Shot("table-before"); _ = _session.BeginDuelDemo();
                _matchProbeVersion = _session.Snapshot!.View.Version; _matchProbeReplay = _session.ExportPracticeReplay(); break;
            case 1:
                Require(screen is not null, "challenge opens the shared live-match screen");
                Require(_session.Snapshot!.View.Version == _matchProbeVersion && _session.ExportPracticeReplay() == _matchProbeReplay, "entrance and both portraits submit no game commands");
                Require(_reducedMotion || screen!.OpeningRuns == 1 && _matchProbeShots.Contains("opening-challenger") && _matchProbeShots.Contains("opening-answer"), "both champion introductions play once after table entry");
                Shot("ready");
                _session.StepPracticeOpponent((v, a) => a.Single(x => x.Command?.Choice == "deck").Command); break;
            case 2:
                var courtesy = _session.Snapshot!.View.Players[0].Hand.Single(c => c.Definition!.Id == "oracle-l5r-7996");
                Native(screen!.CardClickPoint(courtesy.Id)); break;
            case 3: Require(screen!.SelectedInspectionId is not null, $"real hand selection after the introductions (locked {screen.InputLocked}, busy {screen.PresentationBusy}, priority {_session.Snapshot!.View.PriorityPlayerId}, hover {GetViewport().GuiGetHoveredControl()?.GetPath()})"); Native(screen.LandingClickPoint); break;
            case 4:
                if (_matchProbeSeatStep == 0) { Require(_session.Snapshot!.View.Version == 3, "focus placement submits one command"); Shot("focus"); Press("Menu"); _matchProbeSeatStep++; return true; }
                if (_matchProbeSeatStep == 1) { Press("Switch seat"); _matchProbeSeatStep++; return true; }
                if (_matchProbeSeatStep == 2) { Require(!screen!.IsVisibleInTree(), "manual handoff covers all private cards before revealing the other seat"); Shot("handoff"); Press("Reveal this seat"); _matchProbeSeatStep++; return true; }
                if (_matchProbeSeatStep == 3) { Native(screen!.FocusedClickPoint(1)); _matchProbeSeatStep++; return true; }
                if (_matchProbeSeatStep == 4) { Require(screen!.InspectedCard?.Definition is not null && screen.InspectedCard.OwnerId == 1, "new seat can inspect its own blind focus through the main-game inspector"); Shot("private-focus"); Press("Close inspection"); _matchProbeSeatStep++; return true; }
                Press("Strike"); break;
            case 5:
                Require(screen!.CanContinue, "authoritative verdict held until Continue");
                Require(_session.Snapshot!.View.LastDuel is { Result.ChallengerTotal: 8, Result.ChallengedTotal: 7 }, "canonical 8–7 result preserved");
                Require(screen.EndingRuns == 1, "resolution plays exactly once"); Shot("verdict");
                _matchProbeVersion = _session.Snapshot.View.Version; _matchProbeReplay = _session.ExportPracticeReplay();
                var button = Descendants(screen).OfType<Button>().Single(b => b.Text == "Continue" && b.IsVisibleInTree());
                Native(button.GetGlobalRect().GetCenter()); break;
            case 6:
                Require(!MatchDuelOpen && _root.GetParent() == _rootCanvas && _table.GetParent() == this, "returns to original table without reloading or replacing session");
                Require(_session.Snapshot!.View.Version == _matchProbeVersion && _session.ExportPracticeReplay() == _matchProbeReplay, "return changes no commands or rules state");
                Require(View.PriorityPlayerId == 1 && View.Phase == Phase.Battle && View.PendingDuel is null, "engine priority restored to Lion in the battle");
                Require(View.Players[1].Discard.Any(c => c.Definition?.Name == "Matsu Hachiro" && c.Zone == Zone.Dead), "loser remains destroyed");
                Require(_battlePresentationStage == 0, "casualties are not presented twice"); Shot("table-returned");
                var tile = _tiles.First(t => t.Card.Id == View.LastDuel!.Challenger.Id);
                GetWindow().GrabFocus();
                break;
            case 7:
                if (!GetWindow().HasFocus()) { GetWindow().GrabFocus(); return true; }
                if (!_matchProbeTableClick)
                {
                    GD.Print($"MATCH_INPUT: blocked={CameraBlocked}, focus={GetWindow().HasFocus()}, expanded={_expandedUnits.Count}, menu={MenuVisible}, practice={PracticeVisible}");
                    Native(ScreenCenter(_tiles.First(t => t.Card.Id == View.LastDuel!.Challenger.Id))); _matchProbeTableClick = true; return true;
                }
                Require(_selected?.Id == View.LastDuel!.Challenger.Id, "normal table native card input restored");
                System.IO.File.WriteAllText(_capturePath + ".json", JsonSerializer.Serialize(new { Passed = true, Checks = _matchProbeChecks, ReducedMotion = _reducedMotion, Resized = _matchProbeResized, Skipped = _matchProbeSkip, Resolution = GetViewportRectSize() }, new JsonSerializerOptions { WriteIndented = true }));
                GD.Print("MATCH_DUEL_CHECK: completed"); GetTree().Quit(); return true;
        }
        _matchProbeStep++; return true;
        void Press(string text)
        {
            var b = Descendants(_matchDuel!).OfType<Button>().First(b => b.Text == text && b.IsVisibleInTree() && !b.Disabled);
            Native(b.GetGlobalRect().GetCenter());
        }
        void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); _matchProbeChecks.Add(message); GD.Print("MATCH_DUEL_CHECK: " + message); }
        void Shot(string name) { if (!_matchProbeShots.Add(name)) return; System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_capturePath)!); GetViewport().GetTexture().GetImage().SavePng(_capturePath + "-" + name + ".png"); }
        void Native(Vector2 point)
        {
            GetViewport().WarpMouse(point); Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); Input.FlushBufferedEvents();
        }
        void NativeKey(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false }); Input.FlushBufferedEvents(); }
    }
    private bool RunMatchDuelScenario()
    {
        var snapshot = _session.Snapshot!;
        if (_matchProbeStep == 0) { _ = _session.BeginDuelDemo(); _matchProbeStep = 1; return true; }
        if (_matchProbeStep == 2)
        {
            if (MatchDuelOpen) return true;
            if (_session.Snapshot!.View.Version != _matchProbeVersion || _session.ExportPracticeReplay() != _matchProbeReplay || View.PendingDuel is not null && View.WinnerId is null)
                throw new InvalidOperationException("Scenario return changed authoritative state.");
            GetViewport().GetTexture().GetImage().SavePng(_capturePath + "-table.png");
            System.IO.File.WriteAllText(_capturePath + ".json", JsonSerializer.Serialize(new { Passed = true, Scenario = _matchProbeScenario, Seat = _matchProbeSeat, Version = View.Version, Phase = View.Phase.ToString(), Priority = View.PriorityPlayerId, UnchangedReturnReplay = true }));
            GD.Print("MATCH_DUEL_CHECK: completed scenario " + _matchProbeScenario); GetTree().Quit(); return true;
        }
        if (_matchDuel?.CanReturn == true)
        {
            _matchProbeVersion = snapshot.View.Version; _matchProbeReplay = _session.ExportPracticeReplay();
            if (_matchProbeScenario == "refusal" && snapshot.View.LastDuel!.Result!.Kind != "Refused") throw new InvalidOperationException("Refusal scenario was not refused.");
            GetViewport().GetTexture().GetImage().SavePng(_capturePath + "-result.png");
            Press(_matchProbeScenario == "concede" ? "Return to table" : "Continue"); _matchProbeStep = 2; return true;
        }
        GameCommand? Choose(MatchView view, ActionOption[] actions) => _matchProbeScenario == "concede" ? new GameCommand("concede", view.ViewerId, view.Version) : actions.FirstOrDefault(a => a.Enabled && a.Type == (_matchProbeScenario == "refusal" ? "declineDuel" : "strike"))?.Command
            ?? actions.FirstOrDefault(a => a.Enabled && a.Type == "duelistRefocus" && a.Command?.CardId is null)?.Command
            ?? L5R.AI.DuelOpponent.Choose(view, actions);
        if (snapshot.View.PriorityPlayerId != snapshot.View.ViewerId) { _session.StepPracticeOpponent(Choose); return true; }
        var command = Choose(snapshot.View, snapshot.Actions) ?? throw new InvalidOperationException("No scenario action");
        var option = snapshot.Actions.First(a => a.Command == command);
        var label = command.Type == "duelistRefocus" ? "Keep focused cards" : command.Type == "strike" ? "Strike" : option.Label;
        Press(label); return true;
        void Press(string text)
        {
            Node parent = _matchDuel is not null ? _matchDuel : _root;
            var b = Descendants(parent).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && !b.Disabled && b.Text == text) ?? throw new InvalidOperationException("Scenario control unavailable: " + text);
            var point = b.GetGlobalRect().GetCenter(); GetViewport().WarpMouse(point);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            foreach (var pressed in new[] { true, false }) Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed }); Input.FlushBufferedEvents();
        }
    }
    private string GetViewportRectSize() => GetViewport().GetVisibleRect().Size.ToString();
}
