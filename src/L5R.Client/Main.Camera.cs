using Godot;

namespace L5R.Client;

public partial class Main
{
    private Control _tableViewport = null!, _world = null!;
    private TableSurface _tableSurface = null!;
    private Control? _tableTooltip, _tableHover;
    private double _tableHoverTime;
    private Vector2 _lastTableMouse;
    private bool _syncTablePointer;
    private Control? TableFocus => _tableSurface.Canvas.GuiGetFocusOwner();
    private Control? TableHovered => _tableSurface.Canvas.GuiGetHoveredControl();
    private void ReleaseTableFocus() { GetViewport().GuiReleaseFocus(); _tableSurface.Canvas.GuiReleaseFocus(); }
    private Vector2 ScreenCenter(Control c) => _world.IsAncestorOf(c) ? _tableSurface.ProjectCanvas(c.GetGlobalRect().GetCenter()) : c.GetGlobalRect().GetCenter();
    private Vector2 ScreenToWorld(Vector2 p) => _world.GetGlobalTransform().AffineInverse() * _tableSurface.ScreenToCanvas(p);
    private Vector2 WorldToScreen(Vector2 p) => _tableSurface.ProjectCanvas(_world.GetGlobalTransform() * p);
    private Button _fitTableButton = null!, _battleCameraButton = null!;
    private bool _tableLayoutSeen;
    private Vector2 _cameraCenter = new(680, 410);
    private float _cameraZoom = 1;
    private bool _cameraFitted = true, _cameraDragging;
    private MouseButton _cameraDragButton;
    private float FitZoom => FitRegion(new Rect2(-20, -12, WorldWidth + 40, _worldHeight + 24));
    private float FitRegion(Rect2 region)
    {
        var low = .05f; var high = 2f;
        var center = region.GetCenter();
        Vector2[] corners = [region.Position, new(region.End.X, region.Position.Y), region.End, new(region.Position.X, region.End.Y)];
        var available = new Rect2(Vector2.Zero, _tableViewport.Size).Grow(-12);
        for (var i = 0; i < 18; i++)
        {
            var zoom = (low + high) / 2;
            if (corners.All(p => (p.Y - center.Y) * zoom * TableSurface.Depth < .8f && available.HasPoint(_tableViewport.Size / 2 + TableSurface.ProjectOffset((p - center) * zoom)))) low = zoom;
            else high = zoom;
        }
        return low;
    }

    private void BuildTableViewport()
    {
        // Keep overview fitting separate from full-window table drawing.
        _tableViewport = Place(_root, new Control { ClipContents = false, ZIndex = -1, MouseFilter = Control.MouseFilterEnum.Ignore }, 24, 148, 1500, 702);
        _tableSurface = Place(_tableViewport, new TableSurface(), 0, 0, 1500, 702);
        _tableSurface.ResizePlane(_tableViewport.Size, 1);
        _world = Place(_tableSurface.Canvas, new Control { Theme = Palette.MakeTheme(), MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, WorldWidth, 1100);
    }

    private void BuildCameraControls()
    {
        _fitTableButton = new TableIconButton(TableGlyph.Fit, () => { ReleaseTableFocus(); ResetTableCamera(); },
            "Fit table · Home\nArrow keys or drag empty cloth to pan. Middle-drag or Space + drag also pans. Mouse wheel zooms toward the pointer.");
        _menu.AddChild(_fitTableButton); _menu.MoveChild(_fitTableButton, 0);
        _battleCameraButton = new TableIconButton(TableGlyph.Battle, () => { ReleaseTableFocus(); FocusBattle(); }, "Frame current battlefields");
        _menu.AddChild(_battleCameraButton); _menu.MoveChild(_battleCameraButton, 1);
    }

    private void FitTableViewport()
    {
        var left = 144 + TutorialReserve;
        _tableViewport.Position = new Vector2(left, 148);
        // Leave the fitted mat clear of the resting hand, including the small
        // upward reach of its fan. Hover can still deliberately lift a hand card.
        _tableViewport.Size = new Vector2(_layoutWidth - SidebarWidth - BattleStripReserve - left - 44,
            Math.Min(666, 1060 - HandCardSide - _tableViewport.Position.Y - 30));
        _tableSurface.ResizePlane(_tableViewport.Size, _root.Scale.X,
            new Rect2(-_tableViewport.Position, GetViewport().GetVisibleRect().Size / _root.Scale.X));
        ApplyTableCamera();
    }

    private void FocusMySide()
    {
        _cameraFitted = false;
        _cameraZoom = FitRegion(new Rect2(-20, Math.Min(_selfHomeY, _selfProvinceY - MatHeader) - 20, WorldWidth + 40, _worldHeight - Math.Min(_selfHomeY, _selfProvinceY - MatHeader) + 40));
        _cameraCenter = new Vector2(WorldWidth / 2, (Math.Min(_selfHomeY, _selfProvinceY - MatHeader) + _worldHeight) / 2);
        ApplyTableCamera();
    }

    private void FocusBattle()
    {
        if (!BattleActive) return;
        var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
        var lanes = View.CurrentBattleProvince is int active ? new[] { active }
            : View.Players.SelectMany(p => p.Army).Where(c => c.ProvinceIndex is not null).Select(c => c.ProvinceIndex!.Value).Distinct().ToArray();
        if (lanes.Length == 0) return;
        // A battlefield without a province has no province mat to frame with its court.
        var regions = lanes.Where(_battleCourts.ContainsKey).Select(i => _matTargets.FirstOrDefault(s => s.Id == $"province:{defender.Id}:{i}") is { } bay
            ? _battleCourts[i].Merge(bay.Bounds) : _battleCourts[i]);
        if (!regions.Any()) return;
        var region = regions.Aggregate((a, b) => a.Merge(b)).Grow(24);
        _cameraFitted = false; _cameraGlide = false;
        _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter();
        ApplyTableCamera();
    }

    private bool _cameraGlide, _glideEndsFitted;
    private Vector2 _cameraFrom, _cameraTo;
    private float _zoomFrom, _zoomTo;
    private double _cameraGlideTime;
    private (string Match, int Viewer, int Turn, int Owner, int Province)? _framedBattle;
    private void FrameNewBattle()
    {
        if (_capturePath is not null && !_captureMode.StartsWith("published-battle-sequence", StringComparison.Ordinal) || PresentationBusy) return;
        if (View.CurrentBattleProvince is not int province || !BattleActive) { _framedBattle = null; return; }
        var key = (_session.MatchKey, View.ViewerId, View.TurnNumber, View.ActivePlayerId, province);
        if (_framedBattle == key) return;
        _framedBattle = key;
        var from = _cameraCenter; var zoom = _cameraZoom;
        FocusBattle();
        if (_reducedMotion) return;
        _cameraTo = _cameraCenter; _zoomTo = _cameraZoom;
        _cameraFrom = from; _zoomFrom = zoom; _cameraGlideTime = 0; _cameraGlide = true; _glideEndsFitted = false;
        _cameraCenter = from; _cameraZoom = zoom; ApplyTableCamera();
    }

    // Entering the Dynasty Phase (after Battle, every turn): snap the table back to the fitted view once, before the player is shown End Dynasty.
    // Keyed per match/turn/seat so it never fights the player's own pan/zoom later in the phase; it waits out a battle presentation (which hides the
    // footer and re-frames afterwards) and never disturbs a capture.
    private (string Match, int Viewer, int Turn, int Owner)? _framedDynasty;
    private void FrameDynasty()
    {
        if (_capturePath is not null && !_captureMode.Contains("dynasty-flow", StringComparison.Ordinal) || _session.Snapshot is null) return;
        if (View.Phase != L5R.Core.Phase.Dynasty) { _framedDynasty = null; return; }
        if (PresentationBusy || View.ActivePlayerId != View.ViewerId) return;
        if (_capturePath is null && !TableMotionsSettled()) return; // the battle's province cleanup and refills finish first, so the camera makes one clean move (retried every frame until then)
        var key = (_session.MatchKey, View.ViewerId, View.TurnNumber, View.ActivePlayerId);
        if (_framedDynasty == key) return;
        _framedDynasty = key;
        if (_capturePath is not null || _reducedMotion || _cameraFitted) { ResetTableCamera(); return; } // an instant snap
        // One short glide to the fitted view; it ends fitted so later layout changes keep following the table.
        _cameraFrom = _cameraCenter; _zoomFrom = _cameraZoom;
        _cameraTo = new Vector2(WorldWidth / 2, _worldHeight / 2); _zoomTo = FitZoom;
        _cameraGlideTime = 0; _cameraGlide = true; _glideEndsFitted = true; _cameraDragging = false;
    }

    private void ResetTableCamera()
    {
        _cameraGlide = false; _glideEndsFitted = false; _cameraFitted = true; _cameraDragging = false;
        ApplyTableCamera();
    }

    private void ApplyTableCamera()
    {
        if (_world is null) return;
        if (_battleCameraButton is not null) _battleCameraButton.Visible = _session.Snapshot is not null && BattleActive && View.Players.Any(p => p.Army.Length > 0);
        if (_cameraFitted)
        {
            _cameraZoom = FitZoom;
            _cameraCenter = TutorialContentCenter() ?? new Vector2(WorldWidth / 2, _worldHeight / 2);
        }
        else
        {
            _cameraZoom = Math.Clamp(_cameraZoom, FitZoom * .72f, 2f);
            // Keep the table recoverable while allowing breathing room around every edge.
            _cameraCenter = _cameraCenter.Clamp(new Vector2(-100, -100), new Vector2(WorldWidth + 100, _worldHeight + 100));
        }
        _world.Scale = Vector2.One * _cameraZoom;
        _world.Position = _tableSurface.CanvasSize / 2 - _cameraCenter * _cameraZoom;
        _table.SetPlaneCamera(_tableViewport.GetGlobalRect().GetCenter(), _root.Scale.X, _cameraCenter, _cameraZoom, new Vector2(WorldWidth, _worldHeight), FitZoom);
        CardPaper.SetTableTransform(_world.Position * _tableSurface.RenderScale, _cameraZoom * _tableSurface.RenderScale, _tableSurface.Canvas.Size);
        HideTableTooltip(); _syncTablePointer = true;
        _intentOverlay?.QueueRedraw();
    }

    private void ZoomTable(float factor, Vector2 point)
    {
        _cameraGlide = false;
        var offset = TableSurface.UnprojectOffset(point - _tableViewport.Size / 2);
        var before = _cameraCenter + offset / _cameraZoom;
        _cameraFitted = false;
        _cameraZoom = Math.Clamp(_cameraZoom * factor, FitZoom * .72f, 2f);
        _cameraCenter = before - offset / _cameraZoom;
        ApplyTableCamera();
    }

    private bool CameraBlocked => MatchDuelOpen || LookPanelOpen || SearchPanelOpen || CopyPanelOpen || PracticeVisible || MenuVisible || !GetWindow().HasFocus() || (_fullscreenGate?.Visible ?? false)
        || GetChildren().OfType<Window>().Any(w => w.Visible)
        || _expandedUnits.Count > 0;

    private bool PointerOnTable(Vector2 viewportPoint)
    {
        if (CameraBlocked || !_tableSurface.GetGlobalRect().HasPoint(viewportPoint) || FixedHudCovers(viewportPoint)) return false;
        // Prepared review scenarios (attack, payment, copy, sequence, multi, look, lock, either/or, movement, search, shown...) put a row of navigation buttons over
        // the table. Let native GUI picking receive its clicks instead of forwarding them into the mat. Matched by name so a new review row cannot be forgotten.
        foreach (var child in _root.GetChildren())
            if (child is Control review && review.Name.ToString().EndsWith("ReviewControls", StringComparison.Ordinal) && review.IsVisibleInTree() && review.GetGlobalRect().HasPoint(viewportPoint)) return false;
        if (_eventHandBar is { Visible: true } && _eventHandBar.GetGlobalRect().HasPoint(viewportPoint)) return false;
        if (_coachPanel is { Visible: true } coach && coach.GetGlobalRect().HasPoint(viewportPoint)) return false; // the tutorial's note card floats over the table, so its button and scroll must receive their own clicks (issue #935)
        if (_proceedButton is { } proceed && IsInstanceValid(proceed) && proceed.Visible && proceed.GetGlobalRect().HasPoint(viewportPoint)) return false; // Issue #660: Proceed floats over the table
        if (_shownPreview is { } preview && IsInstanceValid(preview) && preview.GetGlobalRect().HasPoint(viewportPoint)) return false;
        // The Fallen strips can overlap the projection. Check only the visible
        // placards, not their full-screen parent, before forwarding root input.
        return !_clanHud.GetChildren().OfType<Control>().Any(c =>
            c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(viewportPoint));
    }

    private bool FixedHudCovers(Vector2 point)
    {
        static bool Covers(Control? control, Vector2 p) => control is not null &&
            control.IsVisibleInTree() && control.GetGlobalRect().HasPoint(p);
        return Covers(_battlefieldReviewBar, point) || Covers(_provinceOrderTray, point) || Covers(_provinceReviewBar, point) || Covers(_inspectorPanel, point) || Covers(_notice, point) || Covers(_shownPanel, point) || Covers(_shownToggle, point) || Covers(_eventSidebar, point) || Covers(_eventHandBar, point) || Covers(_hand, point) || Covers(_candidateTray, point) || Covers(_timeline, point) || Covers(_menu, point) || Covers(_battleProgressStrip, point)
            || _battlefieldCaptions.Values.Any(c => Covers(c, point))
            || _clanHud.GetChildren().OfType<Control>().Any(c => Covers(c, point))
            // A hovered hand face can rise above the hand container.
            || _hand.GetChildren().OfType<CardTile>().Any(c => Covers(c, point));
    }

    public override void _Input(InputEvent @event)
    {
        if (GuardRepeatedSubmissionInput(@event)) return;
        // Native camera probes share a desktop with the reviewer. Only their
        // tagged pointer events belong to the scripted acceptance sequence.
        if (IsMouseInputCapture && @event is InputEventMouse && !@event.HasMeta(CaptureMouseTag))
        { GetViewport().SetInputAsHandled(); return; }
        // A recorded demonstration consumes live input so a wheel/key/click
        // cannot alter the evidence mid-take. Normal play and input probes do not.
        if (_capturePath is not null && _captureMode == "published-ink-film")
        { GetViewport().SetInputAsHandled(); return; }
        // The acceptance probes supply tagged viewport mouse events. Ignore
        // unrelated live desktop gestures only in those explicit capture modes;
        // focus checks and ordinary game input routing remain in force.
        if (IsMouseInputCapture && @event is InputEventMouse && !@event.HasMeta(CaptureMouseTag))
        { GetViewport().SetInputAsHandled(); return; }
        if (_tableSurface is null || MenuVisible || _transitionReview || MatchDuelOpen) return;
        if (CardShowSwallows(@event)) { GetViewport().SetInputAsHandled(); return; } // issue #1138
        if (_dragging && @event is InputEventKey { Pressed: true, Keycode: Key.Escape }) { CancelDrag(); GetViewport().SetInputAsHandled(); return; } // issue #993
        if (@event is InputEventMouse location) _lastTableMouse = location.Position;
        // The token buttons live above the projected viewport; let native GUI input
        // reach them instead of forwarding their clicks into the underlying table.
        if (!_cameraDragging && @event is InputEventMouse tokenMouse && OverStatToken(tokenMouse.Position)
            && (@event is InputEventMouseMotion || @event is InputEventMouseButton { ButtonIndex: MouseButton.Left }))
        { _tableSurface.ExitMouse(); HideTableTooltip(); return; }
        if (@event is InputEventKey { Pressed: false } released && released.Keycode is Key.Enter or Key.KpEnter or Key.Tab && !CameraBlocked && GetViewport().GuiGetFocusOwner() is null && TableFocus is not null)
            _tableSurface.Canvas.PushInput(released, true);
        if (@event is InputEventMouseButton mouse)
        {
            if (!mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && FinishDrag(mouse)) { GetViewport().SetInputAsHandled(); return; } // issue #993: a drag's release is a drop, never also a click
            if (!mouse.Pressed && _cameraDragging && mouse.ButtonIndex == _cameraDragButton)
            {
                _cameraDragging = false; GetViewport().SetInputAsHandled(); return;
            }
            if (!PointerOnTable(mouse.Position))
            {
                // A root-UI drag owns its release. Forwarding it into the table's
                // SubViewport can end the shared Godot drag before the tray receives it.
                if (!mouse.Pressed && !GetViewport().GuiIsDragging()) _tableSurface.ReleaseMouseOutside(mouse);
                return;
            }
            if (!mouse.Pressed) { _tableSurface.ForwardMouse(mouse); GetViewport().SetInputAsHandled(); return; }
            if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var point = _tableViewport.GetGlobalTransform().AffineInverse() * mouse.Position;
                ZoomTable(Mathf.Pow(1.12f, (mouse.ButtonIndex == MouseButton.WheelUp ? 1 : -1) * Math.Max(.1f, mouse.Factor)), point);
                GetViewport().SetInputAsHandled(); return;
            }
            _tableSurface.ForwardMouse(new InputEventMouseMotion { Position = mouse.Position });
            var hovered = TableHovered;
            var emptyCloth = hovered is null || hovered == _root || hovered == _tableViewport || hovered == _world || hovered == _board || hovered == _pileHud || hovered?.HasMeta("fallen_marker") == true;
            if (mouse.ButtonIndex == MouseButton.Middle || mouse.ButtonIndex == MouseButton.Left && (Input.IsPhysicalKeyPressed(Key.Space) || emptyCloth))
            {
                _cameraGlide = false; _cameraDragging = true; _cameraDragButton = mouse.ButtonIndex;
                ReleaseTableFocus(); HideTableTooltip(); _tableSurface.ExitMouse(); GetViewport().SetInputAsHandled();
            }
            else { BeginDragCandidate(hovered, mouse); _tableSurface.ForwardMouse(mouse); GetViewport().SetInputAsHandled(); }
        }
        else if (@event is InputEventMouseMotion motion && _cameraDragging)
        {
            if (!CameraBlocked)
            {
                _cameraFitted = false;
                var local = _tableViewport.GetGlobalTransform().AffineInverse() * motion.Position - _tableViewport.Size / 2;
                var previous = local - motion.Relative / _root.Scale.X;
                _cameraCenter -= (TableSurface.UnprojectOffset(local) - TableSurface.UnprojectOffset(previous)) / _cameraZoom;
                ApplyTableCamera();
            }
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseMotion pointer)
        {
            _lastTableMouse = pointer.Position;
            if (TableDragMotion(pointer)) { GetViewport().SetInputAsHandled(); return; } // issue #993
            if (PointerOnTable(pointer.Position)) { _tableSurface.ForwardMouse(pointer); GetViewport().SetInputAsHandled(); }
            else { _tableSurface.ExitMouse(); HideTableTooltip(); }
        }
        else if (@event is InputEventKey { Pressed: true } key && !CameraBlocked)
        {
            if (key.Keycode == Key.Space && PointerOnTable(GetViewport().GetMousePosition()))
            {
                GetViewport().SetInputAsHandled(); return;
            }
            var focus = GetViewport().GuiGetFocusOwner() ?? TableFocus;
            if (focus is LineEdit or TextEdit) return;
            if (focus is not null && _world.IsAncestorOf(focus) && key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down) ReleaseTableFocus();
            if (key.Keycode == Key.Home)
            {
                ResetTableCamera(); GetViewport().SetInputAsHandled();
            }
            // With focus on an inspector button, arrows retain native focus navigation.
            else if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down && GetViewport().GuiGetFocusOwner() is null)
                GetViewport().SetInputAsHandled();
            else if (key.Keycode is Key.Enter or Key.KpEnter or Key.Tab && GetViewport().GuiGetFocusOwner() is null && TableFocus is not null) { _tableSurface.Canvas.PushInput(key, true); GetViewport().SetInputAsHandled(); }
        }
    }

    private void ProcessTableCamera(double delta)
    {
        if (_capturePath is not null && _captureMode == "published-ink-film") return;
        if (_cameraGlide && !CameraBlocked)
        {
            _cameraGlideTime += delta;
            var t = _reducedMotion ? 1 : Mathf.SmoothStep(0, 1, (float)(_cameraGlideTime / (_glideEndsFitted ? .45 : .7)));
            _cameraCenter = _cameraFrom.Lerp(_cameraTo, t); _cameraZoom = Mathf.Lerp(_zoomFrom, _zoomTo, t);
            ApplyTableCamera();
            if (t >= 1) { _cameraGlide = false; if (_glideEndsFitted) { _glideEndsFitted = false; _cameraFitted = true; ApplyTableCamera(); } }
        }
        ProcessTableTooltip(delta);
        if (_world is null || CameraBlocked)
        {
            _cameraDragging = false; return;
        }
        if (GetViewport().GuiGetFocusOwner() is not null || TableFocus is not null) return;
        var direction = new Vector2((Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0),
            (Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0));
        if (direction == Vector2.Zero) return;
        _cameraFitted = false;
        _cameraGlide = false;
        _cameraCenter += direction.Normalized() * new Vector2(1, 1 / TableSurface.Pitch) * (float)delta * 650 / _cameraZoom;
        ApplyTableCamera();
    }
    private void HideTableTooltip()
    {
        if (IsInstanceValid(_tableTooltip)) { _tableTooltip!.QueueFree(); _tableTooltip = null; }
        _tableHover = null; _tableHoverTime = 0;
    }
    private bool _tooltipsDirty = true; private double _tooltipPrepareClock;
    private void PrepareTableTooltips(Node node)
    {
        if (node is Control control && !string.IsNullOrEmpty(control.TooltipText))
        {
            control.SetMeta("flat_tooltip", control.TooltipText); control.TooltipText = "";
        }
        foreach (var child in node.GetChildren()) PrepareTableTooltips(child);
    }
    private void ProcessTableTooltip(double delta)
    {
        if (_tableSurface is null) return;
        var token = GetViewport().GuiGetHoveredControl() as StatTokens;
        if (token is not null && !token.IsVisibleInTree()) token = null;
        // Token explanations use the same bounded overlay as card previews, so
        // native tooltips and the projected viewport cannot compete for the hover.
        if (token is not null) { _syncTablePointer = false; _tableSurface.ExitMouse(); }
        // Issue #875: the walk moves every Control's native tooltip text into metadata, which only matters once a node is added or its text is set; run it when the tree changes and a few times a second, not every frame.
        _tooltipPrepareClock -= delta;
        if (_tooltipsDirty || _tooltipPrepareClock <= 0) { PrepareTableTooltips(_world); _tooltipsDirty = false; _tooltipPrepareClock = .2; }
        if (token is null && _syncTablePointer && !_cameraDragging && PointerOnTable(_lastTableMouse))
        {
            _syncTablePointer = false;
            _tableSurface.ForwardMouse(new InputEventMouseMotion { Position = _lastTableMouse });
        }
        if (_cameraDragging || (CameraBlocked && token?.Inline != true) || (token is null && !PointerOnTable(_lastTableMouse)))
        { _matSections.Emphasize(null, hover: true); HideTableTooltip(); return; }
        Control? hovered = token ?? _matSections.IconAt(_lastTableMouse) ?? TableHovered;
        if (token is null)
            while (hovered is not null && !hovered.HasMeta("flat_tooltip") && hovered is not (CardTile or PermanentGrantSeal or CardTokenRail)) hovered = hovered.GetParent() as Control;
        var provinceHover = hovered?.HasMeta("province_marker") == true ? hovered.GetMeta("province_marker").AsString()
            : hovered is CardTile provinceCard && provinceCard.Card.Zone is L5R.Core.Zone.Province or L5R.Core.Zone.Region
                ? View.Players.SelectMany(p => p.Provinces.Where(q => ProvincePresentation.Cards(q).Any(c => c.Id == provinceCard.Card.Id)).Select(q => $"province:{p.Id}:{q.Index}")).FirstOrDefault() : null;
        _matSections.Emphasize(provinceHover, hover: true);
        if (hovered != _tableHover) { HideTableTooltip(); _tableHover = hovered; }
        if (hovered is null || _tableTooltip is not null || (_tableHoverTime += delta) < .5) return;
        _tableTooltip = token is not null ? token._MakeCustomTooltip(token.Explanation) :
            hovered is PermanentGrantSeal grant ? grant.CreateReadingPreview() :
            hovered is CardTokenRail rail ? rail.CreateReadingPreview() :
            hovered is HomeReminder reminder ? reminder.CreateReadingPreview() :
            hovered is CardTile card ? card.CreateReadingPreview() : ReadingTooltip.Create(hovered, hovered.GetMeta("flat_tooltip").AsString());
        _root.AddChild(_tableTooltip);
        _tableTooltip.Scale = Vector2.One / _root.Scale.X;
        _tableTooltip.Size = _tableTooltip.CustomMinimumSize;
        var size = _tableTooltip.Size / _root.Scale.X;
        _tableTooltip.Position = (_lastTableMouse / _root.Scale + new Vector2(18, 22)).Clamp(new Vector2(8, 8), DesignSize - size - new Vector2(8, 8));
        _tableTooltip.ZIndex = 90;
        void Ignore(Node n) { if (n is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore; foreach (var child in n.GetChildren()) Ignore(child); }
        Ignore(_tableTooltip);
    }

}
