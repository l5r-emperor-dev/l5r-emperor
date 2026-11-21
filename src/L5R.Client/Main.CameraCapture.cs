using Godot;
using L5R.Core;

namespace L5R.Client;

// Native renderer/input acceptance probes. These manipulate only the local camera;
// the match is produced by the real rules engine and training opponent.
public partial class Main
{
    private int _cameraProbeStep;
    private bool _cameraProbeDone;
    private Vector2 _probePoint, _probeCenter, _probeAnchor, _probeHud;
    private float _probeZoom;
    private long _probeVersion;
    private string? _probeSelection;
    private CardTile? _probeTile;
    private double _nextCameraProbe = .25;

    private const string CaptureMouseTag = "native_acceptance_mouse";
    private bool IsMouseInputCapture => _capturePath is not null &&
        (_captureMode.StartsWith("published-stabilization", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-top-cards", StringComparison.Ordinal) && !_captureMode.EndsWith("-review", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-created-copy", StringComparison.Ordinal) && !_captureMode.EndsWith("-review", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-shown", StringComparison.Ordinal) && !_captureMode.Contains("review", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-action-lock", StringComparison.Ordinal) && !_captureMode.Contains("review", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-direct-play", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-variant-steps", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-table-click", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-drag", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-paused-animation", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-province-target", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-search", StringComparison.Ordinal) && !_captureMode.Contains("review", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-supports", StringComparison.Ordinal) && !_captureMode.Contains("review", StringComparison.Ordinal) ||
         _captureMode.StartsWith("published-board", StringComparison.Ordinal) &&
         (_captureMode.Contains("camera", StringComparison.Ordinal) || _captureMode.Contains("deselect", StringComparison.Ordinal)));
    private void PushCaptureMouse(InputEventMouse mouse, bool buffered = false)
    {
        mouse.SetMeta(CaptureMouseTag, true);
        if (buffered) Input.ParseInputEvent(mouse);
        else GetViewport().PushInput(mouse, true);
    }
    // Issue #905: L5R_PROBE_NO_OS_MOUSE=1 drives the checks through Godot's input pipeline alone (PushInput), without moving the operating system's cursor, so they need neither window focus nor a free desktop.
    private static readonly bool ProbeWithoutOsMouse = OS.GetEnvironment("L5R_PROBE_NO_OS_MOUSE") == "1";
    private void MoveProbeMouse(Vector2 point, Vector2 relative = default)
    {
        if (!ProbeWithoutOsMouse) GetViewport().WarpMouse(point);
        PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative });
    }
    private Vector2 FindProbeCloth()
    {
        var rect = _tableViewport.GetGlobalRect().Grow(-16);
        var fabric = TableArtStage.FabricBounds(new Vector2(WorldWidth, _worldHeight)).Grow(-16);
        // HUD overlays now cover the old viewport-origin target. Find a clear
        // gesture path using current routing and real projected GUI hit tests.
        for (var y = rect.Position.Y; y < rect.End.Y; y += 24)
            for (var x = rect.Position.X; x < rect.End.X - 60; x += 24)
            {
                var point = new Vector2(x, y);
                if (Enumerable.Range(0, 4).Any(i => !PointerOnTable(point + new Vector2(i * 20, 0))
                    || OverStatToken(point + new Vector2(i * 20, 0))
                    || !fabric.HasPoint(ScreenToWorld(point + new Vector2(i * 20, 0))))) continue;
                MoveProbeMouse(point);
                var hit = TableHovered;
                if (hit is null || hit == _world || hit == _board || hit == _pileHud)
                {
                    GD.Print($"CAMERA_CLOTH_TARGET: {point}; focus={GetWindow().HasFocus()}; hit={hit?.Name}");
                    return point;
                }
            }
        throw new InvalidOperationException("Camera probe could not find uncovered empty cloth.");
    }
    private void CameraMouse(MouseButton button, bool pressed, Vector2 point) => PushCaptureMouse(
        new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = point, GlobalPosition = point, Factor = 1 });
    private void CameraKey(Key key, bool pressed) => Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
    private void RequireCamera(bool value, string message) { if (!value) throw new InvalidOperationException("Camera acceptance: " + message); }

    private void ProcessCameraCapture()
    {
        if (_cameraProbeDone || _captureClock < _nextCameraProbe) return;
        // Native input acceptance requires the test window to be foreground.
        if (_cameraProbeStep == 0 && !GetWindow().HasFocus())
        {
            GetWindow().GrabFocus();
            if (_captureClock > 20) { GD.PushError("Camera acceptance could not focus the native window."); GetTree().Quit(1); _capturePath = null; }
            return;
        }
        _nextCameraProbe = _captureClock + .12;
        if (_captureMode.Contains("coverage", StringComparison.Ordinal))
        {
            if (_cameraProbeStep++ == 0) _ = CaptureTableCoverage();
            return;
        }
        try
        {
            switch (_cameraProbeStep++)
            {
                case 0:
                    ResetTableCamera();
                    _probeVersion = View.Version; _probeSelection = _selected?.Id;
                    _probeHud = _inspectorPanel.GetGlobalRect().Position;
                    _probeTile = _tiles.First(t => t.Card.Zone == Zone.Stronghold && t.Card.OwnerId == Self.Id);
                    _probePoint = ScreenCenter(_probeTile);
                    MoveProbeMouse(_probePoint); break;
                case 1:
                    _probeZoom = _cameraZoom;
                    _probeAnchor = ScreenToWorld(_probePoint);
                    CameraMouse(MouseButton.WheelUp, true, _probePoint); CameraMouse(MouseButton.WheelUp, false, _probePoint);
                    break;
                case 2:
                    RequireCamera(_cameraZoom > _probeZoom, $"wheel over a card did not zoom (focus={GetWindow().HasFocus()}, blocked={CameraBlocked}, pointer={_probePoint}, viewport={_tableViewport.GetGlobalRect()}, hovered={GetViewport().GuiGetHoveredControl()?.Name})");
                    RequireCamera((ScreenToWorld(_probePoint)).DistanceTo(_probeAnchor) < .1f, "cursor-anchored zoom drifted");
                    RequireCamera(_selected?.Id == _probeSelection && View.Version == _probeVersion, "wheel selected or played a card");
                    _probeCenter = _cameraCenter;
                    MoveProbeMouse(_probePoint);
                    CameraMouse(MouseButton.Middle, true, _probePoint); break;
                case 3:
                    MoveProbeMouse(_probePoint + new Vector2(70, 40), new Vector2(70, 40));
                    CameraMouse(MouseButton.Middle, false, _probePoint + new Vector2(70, 40)); break;
                case 4:
                    RequireCamera(_cameraCenter.DistanceTo(_probeCenter) > 1 && !_cameraDragging, "middle drag/release did not pan cleanly");
                    RequireCamera(_selected?.Id == _probeSelection && View.Version == _probeVersion, "drag activated a card");
                    RequireCamera(_inspectorPanel.GetGlobalRect().Position == _probeHud, "HUD moved with the table");
                    ResetTableCamera();
                    _probePoint = FindProbeCloth();
                    MoveProbeMouse(_probePoint); break;
                case 5:
                    RequireCamera(GetWindow().HasFocus() && PointerOnTable(_probePoint), "cloth target lost focus or became covered before the drag");
                    _probeCenter = _cameraCenter;
                    CameraMouse(MouseButton.Left, true, _probePoint); break;
                case 6:
                    MoveProbeMouse(_probePoint + new Vector2(60, 0), new Vector2(60, 0));
                    CameraMouse(MouseButton.Left, false, _probePoint + new Vector2(60, 0)); break;
                case 7:
                    RequireCamera(_cameraCenter.DistanceTo(_probeCenter) > 1 && !_cameraDragging, "empty-cloth left drag failed");
                    _probeCenter = _cameraCenter;
                    ReleaseTableFocus(); CameraKey(Key.Right, true); break;
                case 8:
                    CameraKey(Key.Right, false);
                    RequireCamera(_cameraCenter.X > _probeCenter.X, "arrow-key pan failed");
                    CameraKey(Key.Home, true); CameraKey(Key.Home, false); break;
                case 9:
                    RequireCamera(_cameraFitted && Mathf.IsEqualApprox(_cameraZoom, FitZoom), $"Home did not restore overview (fitted={_cameraFitted}, zoom={_cameraZoom}, fit={FitZoom}, focus={GetWindow().HasFocus()}, blocked={CameraBlocked}, right={Input.IsPhysicalKeyPressed(Key.Right)})");
                    _probeZoom = _cameraZoom;
                    _probePoint = _inspectorPanel.GetGlobalRect().GetCenter(); MoveProbeMouse(_probePoint); break;
                case 10:
                    CameraMouse(MouseButton.WheelUp, true, _probePoint); CameraMouse(MouseButton.WheelUp, false, _probePoint);
                    break;
                case 11:
                    RequireCamera(_cameraZoom == _probeZoom, "wheel over the inspector zoomed the board");
                    _probePoint = _hand.GetGlobalRect().GetCenter(); MoveProbeMouse(_probePoint); break;
                case 12:
                    CameraMouse(MouseButton.WheelDown, true, _probePoint); CameraMouse(MouseButton.WheelDown, false, _probePoint);
                    break;
                case 13:
                    RequireCamera(_cameraZoom == _probeZoom, "wheel over the fixed hand zoomed the board");
                    _probePoint = ScreenCenter(_probeTile!); MoveProbeMouse(_probePoint);
                    CameraKey(Key.Space, true); break;
                case 14:
                    _probeCenter = _cameraCenter;
                    CameraMouse(MouseButton.Left, true, _probePoint); break;
                case 15:
                    MoveProbeMouse(_probePoint + new Vector2(-45, 20), new Vector2(-45, 20));
                    CameraMouse(MouseButton.Left, false, _probePoint + new Vector2(-45, 20)); CameraKey(Key.Space, false); break;
                case 16:
                    RequireCamera(_cameraCenter.DistanceTo(_probeCenter) > 1 && !_cameraDragging, "Space + drag over card failed");
                    RequireCamera(View.Version == _probeVersion && _selected?.Id == _probeSelection, "camera gestures changed selection or game state");
                    ResetTableCamera();
                    _probePoint = ScreenCenter(_probeTile!); MoveProbeMouse(_probePoint); break;
                case 17:
                    // Ordinary clicks must still reach the real card controls.
                    CameraMouse(MouseButton.Left, true, _probePoint); CameraMouse(MouseButton.Left, false, _probePoint); break;
                case 18:
                    RequireCamera(_selected?.Id == _probeTile!.Card.Id, "ordinary card click was swallowed by navigation");
                    RequireCamera(View.Version == _probeVersion, "card inspection changed match state");
                    ValidatePerspectiveClicks();
                    // Options now replaces the table with the main-menu page.
                    // Use the real Rules dialog to exercise modal camera blocking.
                    ShowRules(); _probeZoom = _cameraZoom; break;
                case 19:
                    RequireCamera(CameraBlocked, "modal did not block camera");
                    CameraKey(Key.Right, true); break;
                case 20:
                    CameraKey(Key.Right, false);
                    RequireCamera(_cameraZoom == _probeZoom && _cameraFitted, "modal keyboard input moved camera");
                    foreach (var dialog in GetChildren().OfType<AcceptDialog>().Where(d => d.Visible)) { dialog.Hide(); dialog.QueueFree(); }
                    CancelChoice(); ReleaseTableFocus(); ResetTableCamera();
                    if (_captureMode.Contains("zoom", StringComparison.Ordinal))
                    {
                        FocusBattle();
                    }
                    if (_captureMode.Contains("edges", StringComparison.Ordinal))
                    {
                        _cameraFitted = false; _cameraZoom = FitZoom * .72f;
                        ApplyTableCamera();
                    }
                    if (_captureMode.Contains("mat-max", StringComparison.Ordinal))
                    {
                        // Review the real table at its existing maximum zoom, with
                        // both near-side artwork and cards visible for scale.
                        _cameraFitted = false; _cameraZoom = 2f;
                        _cameraCenter = new Vector2(WorldWidth * .2f, _selfProvinceY);
                        if (_captureMode.Contains("gold", StringComparison.Ordinal))
                        {
                            var fabric = TableArtStage.FabricBounds(new Vector2(WorldWidth, _worldHeight));
                            _cameraCenter.X = fabric.Position.X + fabric.Size.X * .09f;
                        }
                        if (_captureMode.Contains("frame", StringComparison.Ordinal))
                        {
                            var fabric = TableArtStage.FabricBounds(new Vector2(WorldWidth, _worldHeight));
                            _cameraCenter = new Vector2(fabric.Position.X, fabric.End.Y);
                        }
                        ApplyTableCamera();
                        RequireCamera(Mathf.IsEqualApprox(_cameraZoom, 2f), "mat review did not reach the maximum zoom");
                        GD.Print($"MAT_MAX_ZOOM: zoom={_cameraZoom}, density={_root.Scale.X}, center={_cameraCenter}");
                    }
                    MoveProbeMouse(_menu.GetGlobalRect().Position - new Vector2(10, 10));
                    break;
                case 21:
                    RequireCamera(_inspector.GetChildren().OfType<Control>().Any(c => c.Size.Y > 0), "inspector has not completed layout");
                    _cameraProbeDone = true;
                    GD.Print("UI_SMOKE_PASS: native cursor-anchored wheel zoom, middle/empty/Space drags, arrow pan, Home reset, fixed HUD/hand, inspector scroll, modal isolation and normal card click; no match changes.");
                    break;
            }
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); _capturePath = null; _cameraProbeDone = true; }
    }

    private void ValidatePerspectiveClicks()
    {
        var version = View.Version;
        var probes = new[] { _probeTile!, _tiles.First(t => t.Card.Bowed && t.Card.AttachedTo is null && _board.IsAncestorOf(t)) };
        foreach (var tile in probes)
        {
            var corners = tile.FaceCornersOnViewport();
            var center = corners.Aggregate(Vector2.Zero, (a, b) => a + b) / 4;
            foreach (var edge in Enumerable.Range(0, 4))
            {
                var midpoint = (corners[edge] + corners[(edge + 1) % 4]) / 2;
                var point = midpoint + (center - midpoint).Normalized() * 6;
                RequireCamera(_tableSurface.ProjectCanvas(_tableSurface.ScreenToCanvas(point)).DistanceTo(point) < .1f, "perspective round trip drifted");
                if (_selected?.Id == tile.Card.Id) DeselectCard();
                MoveProbeMouse(point); CameraMouse(MouseButton.Left, true, point); CameraMouse(MouseButton.Left, false, point);
                RequireCamera(_selected?.Id == tile.Card.Id, "projected edge click selected the wrong card");
            }
        }
        RequireCamera(View.Version == version, "edge inspection changed game state");
        var restore = ScreenCenter(_probeTile!); MoveProbeMouse(restore);
        CameraMouse(MouseButton.Left, true, restore); CameraMouse(MouseButton.Left, false, restore);
        GD.Print("UI_SMOKE_PASS: perspective inverse agrees with rendering; native edge clicks select correct upright and bowed cards without changing game state.");
    }

    private void ValidatePhysicalLayout()
    {
        foreach (var player in View.Players)
        {
            var rowY = ProvinceRowY(player);
            var piles = _pileHud.GetChildren().OfType<TablePile>().Where(p => p.OwnerId == player.Id).ToDictionary(p => p.PileZone);
            var expectedOrder = new[] { Zone.DynastyDiscard, Zone.DynastyDeck, Zone.FateDeck, Zone.FateDiscard };
            var positions = expectedOrder.Select(z => piles[z].Position.X).ToArray();
            RequireCamera(player.Id == Self.Id ? positions.SequenceEqual(positions.Order()) : positions.SequenceEqual(positions.OrderDescending()), "deck/discard row not mirrored correctly");
            foreach (var province in player.Provinces.Where(p => !p.Destroyed))
            {
                var button = _provinceButtons[(player.Id, province.Index)];
                var area = (Control)button.GetParent();
                RequireCamera(Mathf.IsEqualApprox(area.Position.Y, rowY) && Mathf.IsEqualApprox(area.Position.X, SlotX(player, 2 + province.Index)), "province moved from its stable slot");
            }
        }
        foreach (var tile in _tiles.Where(t => t.Card.AttachedTo is not null && !_expandedUnits.Contains(t.Card.AttachedTo)))
        {
            var parent = _tiles.Single(t => t.Card.Id == tile.Card.AttachedTo);
            RequireCamera(tile.GetParent() == parent.GetParent() && Mathf.IsEqualApprox(tile.Position.X, parent.Position.X) && tile.Position.Y < parent.Position.Y, "attachment title strip is not behind its Personality");
        }
        RequireCamera(!HudDescendants(_board).OfType<CardShelf>().Any(), "table still paginates physical cards");
        foreach (var tile in _tiles.Where(t => _board.IsAncestorOf(t)))
        {
            var bounds = new Rect2(Vector2.Zero, _board.Size).Grow(6);
            RequireCamera(tile.FaceCornersOnViewport().All(p => bounds.HasPoint(_board.GetGlobalTransform().AffineInverse() * _tableSurface.ScreenToCanvas(p))), "card lies outside expandable table bounds");
        }
        GD.Print("UI_SMOKE_PASS: separate home/province rows, mirrored deck/discard order, stable province slots, vertical attachment strips, expandable rows without paging.");
    }
}
