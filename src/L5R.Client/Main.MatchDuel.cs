using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private long _matchDuelGeneration = -1;
    private readonly MatchDuelFlow _matchDuelFlow = new();
    private MatchDuelHost? _matchDuel;
    private CanvasLayer? _matchDuelLayer;
    private SubViewport? _matchDuelRoom;
    private TableDuelTransition? _matchDuelTransition;
    private TextureRect? _matchDuelCover;
    private CanvasItem[] _matchDuelHidden = [];
    private bool _matchDuelPreparing, _matchDuelReturning, _matchDuelRenderTable;
    private string? _matchDuelId;
    private Vector2 _matchTablePosition;
    private double _matchBotClock;
    private int _matchDuelEpoch;
    private bool MatchDuelOpen => _matchDuelPreparing || _matchDuel is not null;

    private bool RouteMatchDuel()
    {
        if (_transitionReview || _matchDuelRenderTable) return false;
        if (_matchDuelGeneration != _session.Generation)
        { _matchDuelFlow.Reset(); _matchDuelGeneration = _session.Generation; _matchDuelFlow.Observe(_session.Snapshot); }
        if (MatchDuelOpen)
        {
            if (!_matchDuelReturning && _session.Snapshot is { } actual && actual.View.LastDuel?.Id == _matchDuelId)
                _matchDuel?.Present(actual);
            else if (!_matchDuelReturning && _matchDuelFlow.Current is { } current && current.View.LastDuel?.Id == _matchDuelId)
                _matchDuel?.Present(current);
            SyncDuelCoach();
            return true;
        }
        if (_matchDuelFlow.Current is { } next)
        {
            // Acceptance and pre-duel reactions remain at the table; only an accepted duel enters the arena.
            if (next.View.PendingDuel?.Stage is "AwaitingAccept" or "Reaction") return false;
            _ = EnterMatchDuel(next); return true;
        }
        return false;
    }
    private async Task EnterMatchDuel(SessionSnapshot snapshot)
    {
        _matchDuelPreparing = true; _matchDuelId = snapshot.View.LastDuel!.Id; var epoch = ++_matchDuelEpoch;
        try
        {
            _turnBloom.Cancel(); _cameraDragging = false; _cameraGlide = false; ReleaseTableFocus(); HideTableTooltip();
            foreach (var flight in _flights.Where(IsInstanceValid)) flight.Finish(); _flights.Clear();
            _presentationSnapshot = null; _battlePresentationStage = 0;
            var record = snapshot.View.LastDuel!;
            var publicCards = record.Attachments.Concat(new[] { record.Challenger, record.Challenged }).ToArray();
            var ids = publicCards.Select(c => c.Id).ToHashSet();
            var origins = _tiles.Where(t => IsInstanceValid(t) && ids.Contains(t.Card.Id) && t.Visible).GroupBy(t => t.Card.Id).ToDictionary(g => g.Key, g => g.First().FaceCornersOnViewport());
            // Reconnection, a zoomed/panned-away unit or an already resolved challenge uses a calm dissolve.
            var fresh = record.Result is null && snapshot.View.Players.All(p => p.FocusCount == 0);
            var lift = !_reducedMotion && fresh && publicCards.All(c => origins.TryGetValue(c.Id, out var quad) && quad.All(p => GetViewport().GetVisibleRect().Grow(20).HasPoint(p)));
            _matchDuelLayer = new CanvasLayer { Layer = 100 }; AddChild(_matchDuelLayer);
            await Drawn(); if (epoch != _matchDuelEpoch) return;
            var size = GetViewport().GetVisibleRect().Size;
            var full = GetViewport().GetTexture().GetImage();
            _matchDuelCover = new TextureRect { Texture = ImageTexture.CreateFromImage(full), Size = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Stop }; _matchDuelLayer.AddChild(_matchDuelCover);
            _matchDuelHidden = _root.GetChildren().OfType<CanvasItem>().Where(n => n != _tableViewport && n.Visible).ToArray();
            foreach (var item in _matchDuelHidden) item.Hide();
            foreach (var tile in _tiles.Where(t => ids.Contains(t.Card.Id))) tile.Hide();
            foreach (var rail in _statTokenLayer?.GetChildren().OfType<CanvasItem>() ?? []) rail.Hide();
            _matchTablePosition = _tableViewport.Position;
            _matchDuelRoom = new SubViewport { Size = (Vector2I)size, OwnWorld3D = true, HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; AddChild(_matchDuelRoom);
            _table.Reparent(_matchDuelRoom, false); _root.Reparent(_matchDuelRoom, false); _table.SetProcess(false);
            _matchDuel = new MatchDuelHost { Session = _session, Preferences = _preferences };
            var warmup = new SubViewport { Size = (Vector2I)size, HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; AddChild(warmup); warmup.AddChild(_matchDuel); if (TutorialActive) _matchDuel.Screen.SetGuidanceEnabled(false); // a lesson explains the duel itself
            _matchDuel.Present(snapshot); _matchDuel.Screen.SetEntranceCovered(lift);
            _matchDuel.Finished += () => _ = ReturnFromMatchDuel();
            _matchDuel.Reconnect += () => _ = ReconnectMatchDuel();
            _matchDuel.Leave += ConfirmLeaveMatchDuel;
            _matchDuel.SwitchSeat += () => { _autoOpponent = false; _session.SwitchPracticeSeat(); };
            await Drawn(); await Drawn(); if (epoch != _matchDuelEpoch) { warmup.QueueFree(); return; }
            var destination = warmup.GetTexture().GetImage();
            _matchDuel.Reparent(_matchDuelLayer, false); _matchDuelLayer.MoveChild(_matchDuel, 0); warmup.QueueFree();
            var cards = lift ? publicCards.Select(c => new TableDuelTransition.Card(c, origins[c.Id], _matchDuel.Screen.EntranceQuad(c.Id))).ToArray() : [];
            _matchDuelTransition = new TableDuelTransition { ReducedMotion = !lift }; _matchDuelLayer.AddChild(_matchDuelTransition);
            _matchDuelTransition.Build(_matchDuelRoom.GetTexture(), full, destination, cards, size, record.Challenger.Definition!.Name, record.Challenged.Definition!.Name);
            if (lift) _matchDuelTransition.CameraProgress += amount => MoveMatchRoomCamera(amount);
            _matchDuelTransition.Finished += () =>
            {
                _matchDuelTransition = null; _matchDuelPreparing = false;
                _matchDuelRoom.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled; _tableSurface.Canvas.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                // Use the latest permitted snapshot if the other player acted during the entrance.
                if (_matchDuelFlow.Current is { } latest) _matchDuel.Present(latest);
                var live = _matchDuel.Screen.Snapshot!;
                _matchDuel.Enter(fresh && live.View.WinnerId is null && live.View.LastDuel?.Result is null);
                _matchBotClock = 0; SyncDuelCoach();
            };
            await Drawn(); if (epoch != _matchDuelEpoch) return;
            _matchDuelCover.QueueFree(); _matchDuelCover = null; _matchDuelTransition.Start();
        }
        catch (Exception e)
        {
            GD.PushError($"Duel entrance: {e}");
            // Keep the running match playable even if an optional visual resource failed to prepare.
            RestoreMatchTable(); DisposeMatchDuel(); SetNotice("The duel transition could not be prepared. Continue using the table's legal actions.");
            _matchDuelFlow.Complete(snapshot.View.LastDuel!.Id); Render();
        }
    }
    private void MoveMatchRoomCamera(float amount)
    {
        var size = _tableViewport.Size;
        _tableViewport.Position = _matchTablePosition.Lerp(new Vector2((_layoutWidth - size.X) / 2, 330), amount);
        _tableSurface.PlanePitch = Mathf.Lerp(TableSurface.Pitch, .40f, amount);
        _tableSurface.ResizePlane(size, _root.Scale.X);
        _world.Position = _tableSurface.CanvasSize / 2 - _cameraCenter * _cameraZoom;
        _table.SetPlaneCamera(_tableViewport.GetGlobalRect().GetCenter(), _root.Scale.X, _cameraCenter, _cameraZoom, new Vector2(WorldWidth, _worldHeight), FitZoom, _tableSurface.PlanePitch);
    }
    private void RestoreMatchTable()
    {
        foreach (var item in _matchDuelHidden.Where(IsInstanceValid)) item.Show(); _matchDuelHidden = [];
        if (_root.GetParent() != this) _root.Reparent(this, false);
        if (_table.GetParent() != this) _table.Reparent(this, false);
        _tableSurface.PlanePitch = TableSurface.Pitch; _tableViewport.Position = _matchTablePosition;
        _tableSurface.Canvas.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _root.Show(); _table.Show(); _table.SetProcess(true);
        // Resolution has already been shown in the duel. Do not replay the same casualty as a battle action.
        _motionView = null; _presentationSnapshot = null; _battlePresentationStage = 0; _staticOutcomeUntil = 0;
        _selected = null; _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; _expandedUnits.Clear();
        _matchDuelRenderTable = true; try { Render(); } finally { _matchDuelRenderTable = false; }
        foreach (var flight in _flights.Where(IsInstanceValid)) flight.Finish(); _flights.Clear();
    }
    private async Task ReturnFromMatchDuel()
    {
        if (_matchDuelReturning || _matchDuel is null || !_matchDuel.CanReturn) return;
        _matchDuelReturning = true; _matchDuel.Covered = true; _matchDuel.Screen.InputLocked = true;
        var epoch = _matchDuelEpoch; var id = _matchDuelId!;
        try
        {
            var size = GetViewport().GetVisibleRect().Size; var screen = _matchDuel.Screen;
            var record = screen.Snapshot!.View.LastDuel!;
            var publicCards = record.Attachments.Concat(new[] { record.Challenger, record.Challenged }).ToArray();
            var origins = publicCards.ToDictionary(c => c.Id, c => screen.EntranceQuad(c.Id));
            await Drawn(); if (epoch != _matchDuelEpoch) return;
            var full = GetViewport().GetTexture().GetImage();
            _matchDuelCover = new TextureRect { Texture = ImageTexture.CreateFromImage(full), Size = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize }; _matchDuelLayer!.AddChild(_matchDuelCover);
            var cleanViewport = new SubViewport { Size = (Vector2I)size, HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; AddChild(cleanViewport);
            _matchDuel.Reparent(cleanViewport, false); screen.PrepareReturnBackground();
            await Drawn(); await Drawn(); if (epoch != _matchDuelEpoch) { cleanViewport.QueueFree(); return; }
            var cleanDuel = cleanViewport.GetTexture().GetImage();
            _matchDuel.Reparent(_matchDuelLayer, false); cleanViewport.QueueFree();
            RestoreMatchTable();
            // Only actual survivors are carried back. Dead, discarded and removed cards stay in their real piles.
            var ids = publicCards.Select(c => c.Id).ToHashSet();
            var survivors = _tiles.Where(t => ids.Contains(t.Card.Id) && t.Card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached).GroupBy(t => t.Card.Id).Select(g => g.First()).ToArray();
            var cards = survivors.Select(t => new TableDuelTransition.Card(t.Card, origins[t.Card.Id], t.FaceCornersOnViewport())).ToArray();
            var lift = !_reducedMotion && cards.All(c => c.Destination.All(p => GetViewport().GetVisibleRect().Grow(20).HasPoint(p)));
            _matchDuelRoom!.Size = (Vector2I)size; _matchDuelRoom.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _root.Reparent(_matchDuelRoom, false); _table.Reparent(_matchDuelRoom, false); _table.SetProcess(false);
            if (lift) foreach (var tile in survivors) tile.Hide();
            await Drawn(); await Drawn(); if (epoch != _matchDuelEpoch) return;
            var table = _matchDuelRoom.GetTexture().GetImage();
            _matchDuel.Hide();
            _matchDuelTransition = new TableDuelTransition { ReducedMotion = !lift }; _matchDuelLayer.AddChild(_matchDuelTransition);
            _matchDuelTransition.BuildReturn(full, cleanDuel, table, _matchDuelRoom.GetTexture(), lift ? cards : [], size);
            if (lift) _matchDuelTransition.CameraProgress += MoveMatchRoomCamera;
            _matchDuelTransition.ControlsRevealed += () => { foreach (var tile in survivors.Where(IsInstanceValid)) tile.Show(); };
            _matchDuelTransition.Finished += () =>
            {
                _matchDuelFlow.Complete(id); _matchDuelTransition = null;
                RestoreMatchTable(); DisposeMatchDuel(); _opponentClock = 0; Render();
                GD.Print("MATCH_DUEL: returned to authoritative table");
            };
            await Drawn(); if (epoch != _matchDuelEpoch) return;
            _matchDuelCover.QueueFree(); _matchDuelCover = null; _matchDuelTransition.Start();
        }
        catch (Exception e)
        { GD.PushError($"Duel return: {e}"); _matchDuelFlow.Complete(id); RestoreMatchTable(); DisposeMatchDuel(); Render(); }
    }
    private void DisposeMatchDuel()
    {
        ++_matchDuelEpoch;
        if (_matchDuelLayer is not null) _matchDuelLayer.Visible = false;
        DisposeDuelCoach(); _matchDuelLayer?.QueueFree(); _matchDuelRoom?.QueueFree();
        _matchDuel = null; _matchDuelLayer = null; _matchDuelRoom = null; _matchDuelCover = null; _matchDuelTransition = null;
        _matchDuelPreparing = _matchDuelReturning = false; _matchDuelId = null; _matchBotClock = 0;
    }
    private void ProcessMatchDuel(double delta)
    {
        _turnBloom.Cancel(); if (_duelCoach is not null && _matchDuel is not null) LayoutDuelCoach();
        if (_matchDuel is null || _matchDuel.Paused || _matchDuelReturning || !_autoOpponent || _session.Online || _matchDuel.Screen.Snapshot?.View.LastDuel is not { Complete: false }) return;
        _matchBotClock += delta; if (_matchBotClock < (_reducedMotion ? .15 : .8)) return; _matchBotClock = 0;
        _session.StepPracticeOpponent(L5R.AI.DuelOpponent.Choose);
    }
    private async Task ReconnectMatchDuel()
    {
        if (_session.Seat is not { } seat) return;
        try { await _session.Resume(_savedServer, seat); }
        catch (Exception e) { _matchDuel?.Notify("Could not reconnect: " + e.Message); }
    }
    private void ConfirmLeaveMatchDuel()
    {
        Confirm("Leave this duel?", _session.Online ? "Disconnect from this private table and return to the menu? Your seat can be resumed." : "Return to the menu? Your practice duel will remain available through Resume.", () =>
        {
            // Preserve unfinished presentation in the flow; no command or concession is synthesized.
            RestoreMatchTable(); DisposeMatchDuel(); if (_session.Online) _session.LeavePrivateTable(); ShowMainMenu();
        });
    }
}
