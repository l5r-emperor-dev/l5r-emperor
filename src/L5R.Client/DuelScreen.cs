using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Reusable player-view-only presentation. Host owns session, setup, bot and completion.</summary>
public partial class DuelScreen : Control
{
    public event Action<GameCommand>? CommandRequested;
    public event Action? PresentationFinished;
    public event Action<bool>? ReadingChanged;
    private bool _inputLocked;
    public bool InputLocked { get => _inputLocked; set { if (_inputLocked == value) return; _inputLocked = value; HideCardHover(); RenderGuidance(); RenderLanding(); } }
    public bool ReducedMotion { get; set; }
    public bool PresentationBusy => CinematicBusy || _revealQueue.Count > 0 || _revealClock > 0 || _cleanupClock > 0 || _focusClock > 0;
    public SessionSnapshot? Snapshot { get; private set; }
    public bool CanContinue => Snapshot?.View.LastDuel is { Complete: true } && !PresentationBusy;
    private DuelCinematicStage _stage = null!;
    private Control _scene = null!, _uprights = null!, _flights = null!;
    private TableSurface _table = null!;
    internal Vector2I TableRasterSize => _table.Canvas.Size;
    private Control _plane = null!, _hud = null!;
    private VBoxContainer _actions = null!;
    private Label _status = null!;
    private Control? _reading;
    private string? _pinnedInspectionId;
    private CardTile? _inspectionFace;
    private readonly Dictionary<string, CardTile> _tiles = [];
    private readonly Queue<DuelReveal> _revealQueue = [];
    private readonly HashSet<string> _pendingRevealArt = [];
    private readonly List<Resource> _revealedArt = [];
    private readonly HashSet<long> _seenReveals = [];
    private DuelReveal? _shownReveal;
    private string? _duelId, _selectedHand, _selectedFocus, _selectedInspection;
    private string[] _effectOrder = [];
    private bool _submitted, _aftermathPlayed, _animateEntry;
    internal Vector2 CardClickPoint(string id) => _tiles[id].HasMeta("upright") ? _tiles[id].GetGlobalTransform() * (_tiles[id].Size / 2) : _table.ProjectCanvas(_tiles[id].Position + _tiles[id].Size / 2);
    private double _revealClock, _commandCooldown, _cleanupClock, _focusClock;
    private float _scale;
    private Vector2 _offset;
    private Rect2 _controlsRect;
    private string? _phaseUiKey;
    private double _phaseUiClock = .25;

    public override void _Ready()
    {
        DuelUi.Prepare(); Theme = DuelUi.Theme(); SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore;
        _scene = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_scene);
        _stage = new DuelCinematicStage { ReducedMotion = ReducedMotion }; _scene.AddChild(_stage);
        _table = new TableSurface { PlanePitch = DuelCinematicStage.Pitch, PlaneDepth = DuelCinematicStage.Depth, CanvasExtent = new Vector2(1.1f, 1.1f) }; _scene.AddChild(_table);
        _plane = new Control(); _table.Canvas.AddChild(_plane);
        _uprights = new Control { MouseFilter = MouseFilterEnum.Ignore }; _scene.AddChild(_uprights);
        _flights = new Control { MouseFilter = MouseFilterEnum.Ignore }; _scene.AddChild(_flights);
        _hud = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_hud);
        CreateCinema();
        Resized += Layout; Layout();
    }
    public void Present(SessionSnapshot snapshot)
    {
        if (Snapshot?.View is { } previous && previous.Version == snapshot.View.Version && previous.ViewerId == snapshot.View.ViewerId && previous.Scenario == snapshot.View.Scenario) return;
        var changedSeat = Snapshot?.View.ViewerId != snapshot.View.ViewerId;
        _previousZones = (Snapshot?.View.Players.SelectMany(p => p.Hand.Concat(p.Focused ?? []).Concat(p.Discard)) ?? []).ToDictionary(c => c.Id, c => c.Zone);
        _focusChoiceTarget = null; _focusChoiceScrollX = 0;
        HideCardHover(); Snapshot = snapshot; _submitted = false; _selectedDeck = false; _dragCard = null; ClearReading();
        if (changedSeat) { CloseGuidance(); _selectedHand = null; _selectedFocus = null; _selectedInspection = null; }
        var record = snapshot.View.LastDuel;
        if (record?.Id != _duelId) { _pendingRevealArt.Clear(); _revealedArt.Clear(); ResetCinema(); _animateEntry = record is not null; _duelId = record?.Id; _seenReveals.Clear(); _revealQueue.Clear(); _shownReveal = null; _revealClock = 0; _aftermathPlayed = false; }
        foreach (var reveal in record?.Reveals ?? [])
            if (_seenReveals.Add(reveal.Sequence))
            {
                _revealQueue.Enqueue(reveal);
                if (!ReducedMotion) foreach (var card in reveal.Cards)
                    if (card.Definition?.ArtworkKey is { } path)
                    {
                        if (CardFaces.Has(path)) CardTile.Artwork(card.Definition); // decode now, off the reveal's critical frame
                        else if (!ResourceLoader.HasCached(path) && ResourceLoader.Exists(path) && _pendingRevealArt.Add(path))
                            if (ResourceLoader.LoadThreadedRequest(path, "Texture2D") != Error.Ok) _pendingRevealArt.Remove(path);
                    }
            }
        if (ReducedMotion) while (_revealQueue.TryDequeue(out var frame)) _shownReveal = frame;
        if (_selectedHand is { } hand && !snapshot.View.Players[snapshot.View.ViewerId].Hand.Any(c => c.Id == hand)) _selectedHand = null;
        if (_selectedFocus is { } focus && !(snapshot.View.Players[snapshot.View.ViewerId].Focused ?? []).Any(c => c.Id == focus)) _selectedFocus = null;
        var order = snapshot.Actions.FirstOrDefault(a => a.Type == "orderDuelFocusEffects")?.TargetIds ?? [];
        if (!_effectOrder.Order().SequenceEqual(order.Order())) _effectOrder = order.ToArray();
        if (!ExternalEntrance && !_openingSeen && record is { Result: null } && snapshot.View.PendingDuel?.Stage == "Focusing") StartOpening();
        RenderCards(!changedSeat); RenderActions();
    }
    public void ResetPresentation()
    {
        _pendingRevealArt.Clear(); _revealedArt.Clear();
        ResetCinema(); HideCardHover(); Snapshot = null; PlaceMasthead(); ResetGuidance(); _duelId = null; _seenReveals.Clear(); _revealQueue.Clear(); _shownReveal = null; _revealClock = 0; _aftermathPlayed = false;
        _selectedHand = null; _selectedFocus = null; _selectedInspection = null; _selectedDeck = false; _dragCard = null; _cleanupClock = 0; _effectOrder = []; _commandCooldown = 0; _focusClock = 0; _animateEntry = false; ClearReading();
    }
    private void Layout()
    {
        if (_stage is null) return;
        HideCardHover();
        ClearStory();
        foreach (var flight in _flights.GetChildren()) { _flights.RemoveChild(flight); flight.QueueFree(); }
        var size = GetViewportRect().Size;
        _stage.Resize(size); _scale = _stage.ContentScale; _offset = _stage.ContentOffset;
        var density = _scale * DuelCinematicStage.Density;
        _table.Scale = Vector2.One * density;
        _table.ResizePlane(DuelCinematicStage.StageCanvas / DuelCinematicStage.Density, Math.Max(.8f, density * 1.2f));
        _table.Position = _stage.TableOffset; // after ResizePlane, which resets Position when no drawing area is given
        _plane.Size = _table.CanvasSize; _hud.Size = size; _scene.Size = size;
        _uprights.Position = _offset; _uprights.Scale = Vector2.One * _scale;
        ResetCamera();
        RenderCards(false); RenderActions();
    }
    private static Vector2 ToPlane(Vector2 point) => TableSurface.UnprojectOffset((point + new Vector2(0, DuelCinematicStage.MatLift) - DuelCinematicStage.StageCanvas / 2) / DuelCinematicStage.Density, DuelCinematicStage.Pitch, DuelCinematicStage.Depth);
    private void Select(CardView card)
    {
        if (card.Definition is null || PresentationBusy || InputLocked || _commandCooldown > 0) return;
        _selectedDeck = false;
        if (card.Zone == Zone.Hand && card.OwnerId == Snapshot!.View.ViewerId) { _selectedHand = _selectedHand == card.Id ? null : card.Id; _selectedInspection = _selectedHand; _coachingDismissed.Add(Snapshot.View.ViewerId); _dontShowAgain = false; }
        // A commitment is readable immediately, even off-turn and after a blind deck focus.
        // Keep selection only when the engine offers a replacement for this particular card.
        else if (!_resultArranged && card.Zone == Zone.Focused && card.OwnerId == Snapshot!.View.ViewerId &&
                Legal.Any(a => a.Type == "duelistRefocus" && a.Command!.CardId == card.Id))
        { _selectedFocus = _selectedFocus == card.Id ? null : card.Id; _selectedInspection = _selectedFocus; }
        else { Inspect(card); return; }
        RefreshSelection();
        RenderLanding(); RenderActions();
    }
    private void RefreshSelection()
    {
        foreach (var tile in _tiles.Values) { tile.Highlighted = tile.Card.Id == _selectedHand || tile.Card.Id == _selectedFocus; if (!tile.Highlighted) tile.ReleaseFocus(); tile.QueueRedraw(); }
        foreach (var pile in _piles.Values) { pile.Selected = _selectedDeck && pile.OwnerId == Snapshot!.View.ViewerId; if (!pile.Selected) pile.ReleaseFocus(); pile.QueueRedraw(); }
    }
    private IEnumerable<ActionOption> Legal => Snapshot?.Actions.Where(a => a.Enabled && a.Command is not null) ?? [];
    private void RenderActions()
    {
        if (!CinematicBusy) ClearStory();
        RenderActionsCore(); RenderGuidance(); RefreshVerdict();
        var phaseKey = $"{Snapshot?.View.LastDuel?.Id}:{Snapshot?.View.PendingDuel?.Stage}:{Snapshot?.View.PendingChoice?.Type}:{Snapshot?.View.PriorityPlayerId}:{PresentationBusy}:{CanContinue}";
        if (_phaseUiKey != phaseKey) { _phaseUiKey = phaseKey; _phaseUiClock = ReducedMotion ? .25 : 0; }
        _hud.Modulate = new Color(1, 1, 1, Ease(_phaseUiClock / .25));
        if (!PresentationBusy && _reading is null && Snapshot?.View.PendingDuel?.Stage is "AwaitingAccept" or "Reaction") ShowStakes(true);
    }
    private void RenderActionsCore()
    {
        if (_hud is null) return;
        ClearReading();
        foreach (var c in _hud.GetChildren()) { _hud.RemoveChild(c); c.QueueFree(); }
        _reading = null;
        if (Snapshot is null) return;
        PlaceMasthead();
        var view = Snapshot.View; var stage = view.PendingDuel?.Stage;
        var ribbon = new Control { Position = _offset + new Vector2(640, 290) * _scale, Size = new Vector2(392, 42) * _scale, MouseFilter = MouseFilterEnum.Ignore };
        _hud.AddChild(ribbon); ribbon.Visible = view.LastDuel is not null;
        _status = DuelUi.Text(Status(), 19, Palette.Paper); _status.Name = "Duel status"; _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.VerticalAlignment = VerticalAlignment.Center; _status.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ribbon.AddChild(_status);
        _status.AddThemeColorOverride("font_shadow_color", Colors.Black); _status.AddThemeConstantOverride("shadow_offset_x", 1); _status.AddThemeConstantOverride("shadow_offset_y", 2);
        _status.AddThemeFontSizeOverride("font_size", Math.Max(13, (int)(19 * _scale)));
        if (stage == "Focusing")
        {
            _status.Text = view.PriorityPlayerId == view.ViewerId ? "YOUR MOVE" : "OPPONENT’S MOVE";
            _status.AddThemeFontOverride("font", DuelUi.Heading); _status.AddThemeFontSizeOverride("font_size", Math.Max(22, (int)((view.PriorityPlayerId == view.ViewerId ? 70 : 56) * _scale)));
            ribbon.Position = _offset + new Vector2(606, 280) * _scale; ribbon.Size = new Vector2(460, 56) * _scale;
        }
        _controlsRect = new Rect2(_offset + new Vector2(1350, 760) * _scale, new Vector2(295, 150) * _scale);
        Control? inspectionParent = null;
        RenderTableLabels();
        _actions = new VBoxContainer { Position = _controlsRect.Position, Size = _controlsRect.Size }; _actions.AddThemeConstantOverride("separation", Math.Max(2, (int)(4 * _scale))); _hud.AddChild(_actions);
        if (PresentationBusy) { ribbon.Visible = false; return; }
        if (view.PendingChoice?.Type == "focusEffectChoice") { ribbon.Visible = false; RenderFocusChoice(); return; }
        if (view.LastDuel?.Result is { } result)
        {
            var record = view.LastDuel;
            var outcome = result.Kind == "Refused" ? "Challenge refused" : result.WinnerId is null ? "Equal totals · both lose" : $"{(result.WinnerId == record.Challenger.Id ? record.Challenger : record.Challenged).Definition!.Name} wins";
            _status.Text = outcome;
            ribbon.Visible = !_resultArranged;
            ribbon.Position = _offset + new Vector2(644, 170) * _scale;
            ribbon.Size = new Vector2(384, 136) * _scale;
            _status.AddThemeFontOverride("font", DuelUi.Heading);
            var headingSize = Math.Max(28, (int)(48 * _scale));
            _status.AddThemeFontSizeOverride("font_size", headingSize);
            _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (!record.Complete)
            {
                var priority = DuelUi.Text($"{view.Players[view.PriorityPlayerId].Name}’s reaction", Math.Max(14, (int)(20 * _scale)));
                priority.HorizontalAlignment = HorizontalAlignment.Center;
                priority.Position = _offset + new Vector2(660, 265) * _scale; priority.Size = new Vector2(352, 48) * _scale; priority.AutowrapMode = TextServer.AutowrapMode.WordSmart; _hud.AddChild(priority);
            }
            if (CanContinue) { RenderResultFooter(record); return; }
        }

        if (stage == "Focusing")
        {
            SubmitButton("Strike", Legal.FirstOrDefault(a => a.Type == "strike")?.Command);
            var discard = Legal.Where(a => a.Command!.Choice == "discard").ToArray();
            if (discard.Length > 0)
            {
                var row = new HBoxContainer(); _actions.AddChild(row); inspectionParent = row;
                Button("Focus from discard…", () => ChoicePanel("Choose a public discard", discard), row).SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }
        }
        else if (stage == "DuelistRefocus" && view.PriorityPlayerId == view.ViewerId)
        {
            SubmitButton("Replace from selected hand card", Legal.FirstOrDefault(a => a.Command!.CardId == _selectedFocus && a.Command.TargetId == _selectedHand && _selectedFocus is not null && _selectedHand is not null)?.Command);
            SubmitButton("Replace from deck", Legal.FirstOrDefault(a => a.Command!.CardId == _selectedFocus && a.Command.Choice == "deck" && _selectedFocus is not null)?.Command);
            SubmitButton("Keep focused cards", Legal.FirstOrDefault(a => a.Command!.CardId is null)?.Command);
        }
        else if (stage == "OrderingEffects" && view.PriorityPlayerId == view.ViewerId)
        {
            Button("Arrange Focus Effects…", ShowOrder);
        }
        else foreach (var option in Legal.Where(a => a.Type is "acceptDuel" or "declineDuel" or "bowToRefuseDuel" or "declineReaction" or "useAbility" or "overconfidentDraw"))
            if (view.LastDuel is not null) SubmitButton(option.Label, option.Command);
        if (_selectedInspection is { } selected && _tiles.TryGetValue(selected, out var tile)) Button(inspectionParent is null ? "Inspect selected card" : "Inspect", () => Inspect(tile.Card), inspectionParent);
    }
    private string Status()
    {
        var v = Snapshot!.View;
        return v.PendingChoice is { } pending ? $"{v.Players[v.PriorityPlayerId].Name} · {pending.Prompt}" : v.PendingDuel?.Stage switch
        {
            "AwaitingAccept" => $"{v.Players[v.PriorityPlayerId].Name} · accept or refuse the challenge",
            "Focusing" => v.PriorityPlayerId == v.ViewerId ? (Legal.Any(a => a.Type == "focus") ? "Your move" : "Your move · Strike") : "Opponent’s move",
            "DuelistRefocus" => $"{v.Players[v.PriorityPlayerId].Name} · Duelist: select a focus card and its replacement, or keep your focus",
            "OrderingEffects" => $"{v.Players[v.PriorityPlayerId].Name} · choose the order of Focus Effects",
            _ => ""
        };
    }
    internal string EssentialStatus => _status?.Text ?? "";
    private void Text(string text) { var label = DuelUi.Wrapped(text, Math.Max(13, (int)(16 * _scale)), Palette.Paper); _actions.AddChild(label); }
    private Button Button(string text, Action pressed, Control? parent = null)
    {
        var primary = text is "Strike" or "Continue" or "Resolve in this order";
        var buttonScale = _reading is null ? _scale : 1;
        var b = DuelUi.Button(text, () => { if (!_submitted && _commandCooldown <= 0) pressed(); }, primary, buttonScale, text == "Strike" ? 85 : primary ? 25 : 17);
        if (parent is null or VBoxContainer) b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        b.CustomMinimumSize = new Vector2(0, Math.Max(25, (text == "Strike" ? 84 : primary ? 48 : 34) * buttonScale));
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var style = (StyleBoxTexture)b.GetThemeStylebox(state); style.ContentMarginTop = style.ContentMarginBottom = 4 * buttonScale;
        }
        b.TooltipText = text; (parent ?? _actions).AddChild(b); return b;
    }
    private Button SubmitButton(string text, GameCommand? command, Control? parent = null)
    { var b = Button(text, () => Submit(command!), parent); b.Disabled = command is null || _submitted || _commandCooldown > 0; return b; }
    private void Submit(GameCommand command)
    {
        if (InputLocked || _submitted || _commandCooldown > 0 || PresentationBusy || Snapshot?.View.Version != command.ExpectedVersion) return;
        _submitted = true; _commandCooldown = .18; foreach (var button in _actions.GetChildren().OfType<Button>()) button.Disabled = true;
        CommandRequested?.Invoke(command);
    }
    public void SubmissionFinished() { _submitted = false; RenderActions(); }
    private VBoxContainer Panel(string title, float height = 620)
    {
        HideCardHover(); ClearReading(); var shade = new ColorRect { Color = new Color(0, 0, 0, .68f), Size = Size, MouseFilter = MouseFilterEnum.Stop }; _hud.AddChild(shade); _reading = shade; ReadingChanged?.Invoke(true); _hud.ZIndex = 60; _cinemaLayer.Hide(); RenderGuidance();
        return DuelUi.Modal(shade, Size, new Vector2(690, height), title);
    }
    private void ChoicePanel(string title, ActionOption[] choices)
    {
        var box = Panel(title, Math.Clamp(160 + choices.Length * 70, 280, 620));
        foreach (var a in choices)
        {
            var row = new HBoxContainer(); box.AddChild(row); SubmitButton(a.Label, a.Command, row).SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var card = Snapshot!.View.Players.SelectMany(p => p.Discard).FirstOrDefault(c => c.Id == a.CardId);
            if (card is not null) Button("Inspect", () => Inspect(card), row).AutowrapMode = TextServer.AutowrapMode.Off;
        }
        Button("Back", ClearReading, box);
    }
    private void ShowOrder()
    {
        var box = Panel("Focus Effect order", 690);
        box.AddChild(DuelUi.Wrapped("Move a card earlier, then resolve the complete order.", 18, Palette.PaperDim));
        var cards = Snapshot!.View.LastDuel!.Reveals.Last().Cards.ToDictionary(c => c.Id);
        var grid = new GridContainer { Columns = Math.Clamp(_effectOrder.Length, 1, 3), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 18); box.AddChild(grid);
        for (var i = 0; i < _effectOrder.Length; i++)
        {
            var index = i; var card = cards[_effectOrder[i]];
            var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 8); grid.AddChild(column);
            var face = new CardTile(card with { Bowed = false }, true, Inspect, _ => { }) { CustomMinimumSize = new Vector2(185, 205), FaceHeight = 190, ShowTokenRail = false, ReducedMotion = true, Material = CardPaper.Reading }; column.AddChild(face);
            column.AddChild(DuelUi.Wrapped(card.Definition!.Name, 19));
            column.AddChild(DuelUi.Text(Snapshot.View.Players[card.OwnerId].Name, 16, Palette.PaperDim));
            var button = Button($"{i + 1}. Move earlier", () => { if (index > 0) (_effectOrder[index - 1], _effectOrder[index]) = (_effectOrder[index], _effectOrder[index - 1]); ShowOrder(); }, column);
            button.Disabled = index == 0;
        }
        var action = Legal.Single(a => a.Type == "orderDuelFocusEffects").Command!;
        SubmitButton("Resolve in this order", action with { CardIds = _effectOrder.ToArray() }, box); Button("Back", ClearReading, box);
    }
    private void ShowConsequences(DuelRecord record)
    {
        var box = RecapPanel(out var actions); var result = record.Result!;
        box.AddChild(DuelUi.Wrapped(VerdictReason(record, result), 22, Palette.Paper));
        if (result.Kind == "Resolved") box.AddChild(DuelUi.Wrapped($"{record.Challenger.Definition!.Name}: {result.ChallengerStat} {StatName(record.Stat)} → {result.ChallengerTotal}\n{record.Challenged.Definition!.Name}: {result.ChallengedStat} {StatName(record.Stat)} → {result.ChallengedTotal}", 19));
        if (record.Beats is { Length: > 0 } beats)
            foreach (var beat in beats)
            {
                var title = beat.Kind == "FocusEffect" ? "Focus Effect" : beat.Kind == "Aftermath" ? "After the duel" : "Consequence";
                box.AddChild(DuelUi.Wrapped($"{title} · {beat.Source?.Definition?.Name ?? beat.SourceLabel ?? "Duel"}", 21));
                foreach (var line in beat.Lines) box.AddChild(DuelUi.Wrapped(line, 18));
                if (beat.Source is { } source) Button($"Inspect {source.Definition!.Name}", () => Inspect(source), box);
            }
        else foreach (var e in result.Consequences) box.AddChild(DuelUi.Wrapped(e.Message, 17));
        foreach (var p in result.Provinces.Where(p => p.After != p.Before)) box.AddChild(DuelUi.Wrapped($"{Snapshot!.View.Players[p.PlayerId].Name} · province {p.Index + 1}: {p.Before} → {p.After} strength", 17));
        foreach (var id in result.EffectOrder) { var card = record.Reveals.Last().Cards.First(c => c.Id == id); box.AddChild(DuelUi.Wrapped($"Focus Effect: {card.Definition!.Name}", 17)); }
        box.AddChild(DuelUi.Wrapped($"Returns to {result.ReturnPhase} · {Snapshot!.View.Players[result.ReturnPriority].Name} has priority.", 17));
        if (record.Reveals.LastOrDefault() is { } reveal) Button("Review revealed cards", () => ShowCards("Revealed cards · history", reveal.Cards, "Duel history · these cards have already moved to their final destinations."), actions);
        if (CanContinue) { var space = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }; actions.AddChild(space); Button("Continue", () => { ClearReading(); PresentationFinished?.Invoke(); }, actions); }
    }
    private void Inspect(CardView card)
    {
        if (card.Definition is null) return;
        HideCardHover(); ClearReading(); var shade = new ColorRect { Color = new Color(0, 0, 0, .65f), Size = Size }; _hud.AddChild(shade); _reading = shade; ReadingChanged?.Invoke(true); _hud.ZIndex = 60; _cinemaLayer.Hide(); RenderGuidance();
        // Shared full-face + rules component, with the approved lacquer shell.
        var preview = CardPreview.Create(card, InspectionContext(card), null, Math.Min(Size.Y / 1080f, Math.Min((Size.X - 48) / 750f, (Size.Y - 130) / 558f)));
        DuelUi.SkinInspection(preview); _pinnedInspectionId = card.Id; _selectedInspection = card.Id;
        _inspectionFace = FindFace(preview);
        preview.Size = preview.CustomMinimumSize; preview.Position = (Size - preview.Size) / 2;
        shade.AddChild(preview);
        var close = Button("Close inspection", ClearReading, shade); close.Position = new Vector2(Size.X / 2 - 120, preview.Position.Y + preview.Size.Y + 18); close.Size = new Vector2(240, 38);

    }
    private static CardTile? FindFace(Node node) => node as CardTile ?? node.GetChildren().Select(FindFace).FirstOrDefault(c => c is not null);
    public void ClearReading() { _pinnedInspectionId = null; _inspectionFace = null; if (_reading is null) return; _hud.GetNodeOrNull<Control>("DuelResultFooter")?.Show(); _hud.RemoveChild(_reading); _reading.QueueFree(); _reading = null; ReadingChanged?.Invoke(false); _hud.ZIndex = 0; _cinemaLayer.Visible = CinematicBusy; RenderGuidance(); }
    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree() || InputLocked) return;
        if (CinematicBusy && _reading is null)
        {
            if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Escape }) { SkipCinematic(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape && _helpOpen) { CloseGuidance(); GetViewport().SetInputAsHandled(); return; }
            if (_guidanceLayer is not null && GetViewport().GuiGetFocusOwner() is { } focused && _guidanceLayer.IsAncestorOf(focused)) return;
            if (key.Keycode == Key.Escape && _reading is not null) { ClearReading(); GetViewport().SetInputAsHandled(); return; }
            if (_reading is not null) return;
            if (FocusChoicePending) return;
            if (key.Keycode is Key.Left or Key.Right && Snapshot is not null && !PresentationBusy)
            {
                var hand = Snapshot.View.Players[Snapshot.View.ViewerId].Hand;
                if (hand.Length > 0) { var index = Array.FindIndex(hand, c => c.Id == _selectedHand); Select(hand[(index + (key.Keycode == Key.Right ? 1 : hand.Length - 1) + hand.Length) % hand.Length]); GetViewport().SetInputAsHandled(); }
            }
            if (key.Keycode == Key.D) { SelectPile(Snapshot!.View.ViewerId, Zone.FateDeck); GetViewport().SetInputAsHandled(); }
            if (key.Keycode == Key.Enter && SelectedFocusCommand() is { } commit) { Submit(commit); GetViewport().SetInputAsHandled(); }
            if (key.Keycode == Key.I && _selectedInspection is { } id && _tiles.TryGetValue(id, out var tile)) { Inspect(tile.Card); GetViewport().SetInputAsHandled(); }
        }
        if (e is InputEventMouse m)
        {
            _lastHoverPointer = m.Position; UpdateLandingCue(m.Position);
            if (m is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _pinnedInspectionId is { } pinned &&
                (_inspectionFace?.GetGlobalRect().HasPoint(m.Position) == true || UprightAt(m.Position)?.Card.Id == pinned))
            {
                ClearReading(); if (_selectedHand == pinned) _selectedHand = null; if (_selectedFocus == pinned) _selectedFocus = null;
                _selectedInspection = null; RefreshSelection(); RenderLanding(); RenderActions(); GetViewport().SetInputAsHandled(); return;
            }
            if (m is InputEventMouseButton) HideCardHover();
            if (_guidanceRect.HasPoint(m.Position) || UprightAt(m.Position) is not null) { _table.ExitMouse(); return; }
            if (FocusChoicePending) { _table.ExitMouse(); return; }
            if (_reading is null && HandlePlacement(m)) return;
            if (_reading is null && !_controlsRect.HasPoint(m.Position) && m.Position.Y > _offset.Y + 340 * _scale)
            {
                _table.ForwardMouse(m);
            }
            else _table.ExitMouse();
        }
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) { HideCardHover(); return; }
        if (!InputLocked && _reading is null) { _phaseUiClock = Math.Min(.25, _phaseUiClock + delta); _hud.Modulate = new Color(1, 1, 1, ReducedMotion ? 1 : Ease(_phaseUiClock / .25)); }
        ProcessCardHover(delta);
        if (_commandCooldown > 0) { _commandCooldown -= delta; if (_commandCooldown <= 0) { RenderLanding(); RenderActions(); } }
        _stage.ReducedMotion = ReducedMotion; AdvanceWind(delta); _stage.Advance(InputLocked || _reading is not null ? 0 : delta);
        if (CinematicBusy) { AdvanceCinema(delta); return; }
        if (InputLocked || _reading is not null) return;
        if (_focusClock > 0) { _focusClock = ReducedMotion ? 0 : Math.Max(0, _focusClock - delta); if (_focusClock <= 0) { RenderLanding(); RenderActions(); } }
        if (_cleanupClock > 0) { _cleanupClock -= delta; if (_cleanupClock <= 0) { RenderCards(false); RenderActions(); } }
        if (_revealClock > 0) { _revealClock -= delta; if (_revealClock <= 0) { RenderCards(); RenderActions(); } }
        // Only identities already revealed by the engine are loaded here. Poll without blocking
        // a rendered frame; the short held backs remain until their permitted artwork is ready.
        foreach (var path in _pendingRevealArt.ToArray())
        {
            var state = ResourceLoader.LoadThreadedGetStatus(path);
            if (state == ResourceLoader.ThreadLoadStatus.InProgress) continue;
            if (state == ResourceLoader.ThreadLoadStatus.Loaded) _revealedArt.Add(ResourceLoader.LoadThreadedGet(path));
            _pendingRevealArt.Remove(path);
        }
        if (_revealClock <= 0 && _pendingRevealArt.Count == 0 && _revealQueue.TryDequeue(out var reveal))
        { _shownReveal = reveal; _revealClock = ReducedMotion ? 0 : 1.1; RenderCards(); RenderActions(); return; }
        if (!PresentationBusy && !_aftermathPlayed && Snapshot?.View.LastDuel?.Result is { })
        {
            StartEnding();
        }
        else if (!PresentationBusy && _aftermathPlayed) StartAftermathStory();
    }
}
