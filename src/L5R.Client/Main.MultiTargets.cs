using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<string, List<string>> _multiCards = [];
    private readonly Dictionary<string, int> _multiPlayers = [];
    private readonly Dictionary<string, (int Owner, int Index)> _multiProvinces = [];
    private readonly HashSet<string> _multiExpanded = [];
    private string? _multiDraftKey, _multiActiveSlot, _multiMatch, _reactionCameraKey;
    private Window? _multiInspection;
    private static bool HasMultiTargets(ActionOption? action) => action is { Slots.Length: > 0, Command: not null };
    // Issue #1327: with several legal performers, who performs is asked first (PerformerStepActive); the target groups and their table clicks wait until he is chosen.
    private bool MultiTargetActive => View.WinnerId is null && HasMultiTargets(_chosen) &&
        _chosen!.Command!.PlayerId == View.ViewerId && View.PriorityPlayerId == View.ViewerId && _chosen.Command.ExpectedVersion == View.Version && !PerformerStepActive;
    private IEnumerable<TargetSlotOption> MultiUpFront => _chosen?.Slots?.Where(s => s.Timing == TargetSlotTimings.UpFront) ?? [];
    private string MultiKey => $"{_session.MatchKey}/{View.ViewerId}/{View.Version}/{_chosen?.CardId}/{_chosen?.Command?.AbilityId}/{_chosen?.Command?.PerformerId}/{DecisionCanSubmit}";
    private void ClearMultiTargets()
    {
        if (_multiInspection is not null && IsInstanceValid(_multiInspection)) { _multiInspection.Hide(); _multiInspection.QueueFree(); }
        _multiInspection = null; _multiCards.Clear(); _multiPlayers.Clear(); _multiProvinces.Clear(); _multiExpanded.Clear(); _multiDraftKey = _multiActiveSlot = _multiMatch = null;
    }
    private void BeginMultiTargets()
    {
        ClearMultiTargets(); if (!MultiTargetActive) return;
        _multiDraftKey = MultiKey; _multiMatch = _session.MatchKey;
        foreach (var slot in MultiUpFront.Where(s => s.Kind == TargetSlotKinds.Card)) _multiCards[slot.Slot] = [];
        _multiActiveSlot = MultiUpFront.FirstOrDefault()?.Slot;
        foreach (var provinceSlot in MultiUpFront.Where(s => s.Kind == TargetSlotKinds.Province)) _multiExpanded.Add(provinceSlot.Slot); // a short list of provinces: shown at once
        if (_multiActiveSlot is not null) _multiExpanded.Add(_multiActiveSlot);
        _chosen = _chosen! with { Command = _chosen!.Command! with { Selections = [], TargetId = null } };
    }
    // The choosing flow (issue #1004): one group is open at a time. A group that is full hands the board to the next group that still has room (a province after its cards, a player after a target); when
    // every group is full none is open and the summary is shown. Opening a group by its header closes the others.
    private void OpenMultiSlot(string slotId) { _multiActiveSlot = slotId; _multiExpanded.Clear(); _multiExpanded.Add(slotId); }
    private void MultiAdvance(string slotId)
    {
        var slots = MultiUpFront.ToList(); var at = slots.FindIndex(s => s.Slot == slotId);
        if (at < 0 || MultiCount(slots[at]) < slots[at].Max) return;
        var next = slots.Skip(at + 1).Concat(slots.Take(at)).FirstOrDefault(s => MultiCount(s) < s.Max);
        if (next is not null) OpenMultiSlot(next.Slot); // the last group stays open so a choice can still be swapped
    }
    private bool MultiAllFull => MultiUpFront.All(s => MultiCount(s) >= s.Max);
    /// <summary>Back: reopen the most recently answered group (the last one with a selection, before the open one when a group is open) and clear its answer.</summary>
    private void MultiBack()
    {
        // Issue #1327: with no group answered yet, Back returns to choosing who performs (when there was a choice).
        if (!MultiUpFront.Any(s => MultiCount(s) > 0) && _performerPicked && PerformerClicks().Count > 0) { _performerPicked = false; RenderInspector(); Highlight(); return; }
        var slots = MultiUpFront.ToList(); var open = slots.FindIndex(s => s.Slot == _multiActiveSlot && _multiExpanded.Contains(s.Slot));
        var from = open < 0 ? slots.Count : open + (MultiCount(slots[open]) > 0 ? 1 : 0); // an open group that already has an answer is the one Back undoes
        var previous = slots.Take(from).LastOrDefault(s => MultiCount(s) > 0) ?? slots.Take(from).LastOrDefault();
        if (previous is null) return;
        _multiCards.GetValueOrDefault(previous.Slot)?.Clear(); _multiPlayers.Remove(previous.Slot); _multiProvinces.Remove(previous.Slot);
        OpenMultiSlot(previous.Slot); RefreshMultiTargets();
    }
    private string MultiSelectionText(TargetSlotOption slot)
    {
        string Name(string id) => FindCard(id) is { } c ? ShortName(c) : "?";
        return slot.Kind == TargetSlotKinds.Card ? string.Join(", ", (_multiCards.GetValueOrDefault(slot.Slot) ?? []).Select(Name))
            : slot.Kind == TargetSlotKinds.Province ? _multiProvinces.TryGetValue(slot.Slot, out var pr) ? ProvinceLabel(pr.Owner, pr.Index) : ""
            : _multiPlayers.TryGetValue(slot.Slot, out var pl) ? (pl == View.ViewerId ? "You" : "Your opponent") : "";
    }
    private int MultiCount(TargetSlotOption slot) => slot.Kind == TargetSlotKinds.Card ? _multiCards.GetValueOrDefault(slot.Slot)?.Count ?? 0
        : slot.Kind == TargetSlotKinds.Province ? _multiProvinces.ContainsKey(slot.Slot) ? 1 : 0 : _multiPlayers.ContainsKey(slot.Slot) ? 1 : 0;
    private bool MultiReady => MultiUpFront.All(s => MultiCount(s) >= s.Min && MultiCount(s) <= s.Max);
    private IEnumerable<string> MultiSelectedIds => MultiUpFront.SelectMany(s => _multiCards.GetValueOrDefault(s.Slot) ?? []);
    private GameCommand MultiCommand()
    {
        var slots = MultiUpFront.Select(s => s.Kind == TargetSlotKinds.Card ? new SlotSelection(s.Slot, _multiCards[s.Slot].ToArray()) :
            s.Kind == TargetSlotKinds.Province ? _multiProvinces.TryGetValue(s.Slot, out var province) ? new SlotSelection(s.Slot, ProvinceOwner: province.Owner, Province: province.Index) : new SlotSelection(s.Slot) :
            new SlotSelection(s.Slot, PlayerId: _multiPlayers.TryGetValue(s.Slot, out var player) ? player : null)).ToArray();
        return _chosen!.Command! with { Selections = slots, TargetId = MultiUpFront.Where(s => s.Kind == TargetSlotKinds.Card && !s.NotPrimary).SelectMany(s => _multiCards[s.Slot]).FirstOrDefault() }; // Core's primary target skips performing and cost-only slots
    }
    private void RefreshMultiTargets(string? focus = null)
    {
        var oldTarget = _chosen!.Command!.TargetId;
        _chosen = _chosen with { Command = MultiCommand() }; _alternativeCostConfirmed = false;
        if (_chosen.Command.TargetId is { } target && oldTarget != target && _chosen.TargetGold?.ContainsKey(target) == true) ResuggestSources();
        RenderInspector(); Highlight();
        if (focus is not null && _inspector.FindChildren("*", "Button", true, false).OfType<MultiTargetFace>().FirstOrDefault(b => b.GetMeta("focus_key").AsString() == focus) is { } button && !button.Disabled)
            button.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void ToggleMultiCard(string slotId, string id)
    {
        if (!MultiTargetActive || !DecisionCanSubmit || _multiDraftKey != MultiKey) return;
        var slot = MultiUpFront.FirstOrDefault(s => s.Slot == slotId && s.Kind == TargetSlotKinds.Card);
        if (slot?.CardIds.Contains(id) != true) return;
        var selected = _multiCards[slotId];
        if (!selected.Remove(id))
        {
            if (selected.Count >= slot.Max) { SetNotice($"This group allows at most {slot.Max}. Remove a selection first."); return; }
            selected.Add(id);
        }
        _multiActiveSlot = slotId; _multiExpanded.Add(slotId);
        MultiAdvance(slotId);
        RefreshMultiTargets(slotId + "/" + id);
    }
    /// <summary>The card groups that can take this card now: it is a candidate and the group has room, or it is already chosen there and a click would remove it.</summary>
    private IEnumerable<TargetSlotOption> MultiOpenCardSlots(string cardId) => MultiUpFront.Where(s => s.Kind == TargetSlotKinds.Card && s.CardIds.Contains(cardId)
        && (_multiCards.GetValueOrDefault(s.Slot)?.Contains(cardId) == true || (_multiCards.GetValueOrDefault(s.Slot)?.Count ?? 0) < s.Max));

    private bool SelectMultiCard(CardView card)
    {
        if (!MultiTargetActive) return false;
        var slot = MultiUpFront.FirstOrDefault(s => s.Slot == _multiActiveSlot && s.Kind == TargetSlotKinds.Card);
        if (slot?.CardIds.Contains(card.Id) == true) { ToggleMultiCard(slot.Slot, card.Id); return true; }
        // Issue #969: a card that only another open group accepts switches to that group (a card both groups accept stays a choice for the player).
        var others = MultiOpenCardSlots(card.Id).Where(s => s.Slot != _multiActiveSlot).ToArray();
        if (others.Length == 1) { ToggleMultiCard(others[0].Slot, card.Id); return true; }
        if (others.Length > 1) { SetNotice("This card fits more than one group. Open the group you want in the sidebar, then click it."); return true; }
        // A Gold source can still be chosen through the existing payment panel. A board click during target
        // selection always belongs to the active group, including when the candidate is itself a Holding.
        SetNotice("Choose a gold-outlined card for the active group, or open another group in the sidebar."); return true;
    }
    // A province slot (Benediction, issue #699) reuses the province picker's presentation: one list in two groups (the opponent's provinces, then your own), each row naming the province
    // number and the card in it or that it is face down, hovering a row highlights the province on the table, and a click on the table's province chooses it too. Core offers the
    // legal provinces and validates the selection; the client only presents them.
    private TargetSlotOption? MultiProvinceSlot => MultiTargetActive ? MultiUpFront.FirstOrDefault(s => s.Slot == _multiActiveSlot && s.Kind == TargetSlotKinds.Province) : null;
    private bool MultiProvinceActive => MultiProvinceSlot is not null;
    private bool MultiProvinceLegal(int owner, int province) => MultiProvinceSlot?.ProvinceChoices?.Any(c => c.Owner == owner && c.Index == province) == true;
    private bool MultiProvinceChosen(int owner, int province) => MultiProvinceSlot is { } slot && _multiProvinces.TryGetValue(slot.Slot, out var chosen) && chosen.Owner == owner && chosen.Index == province;
    private void ChooseMultiProvince(int owner, int index, string? slotId = null)
    {
        if (slotId is not null && MultiTargetActive && MultiUpFront.Any(s => s.Slot == slotId && s.Kind == TargetSlotKinds.Province)) _multiActiveSlot = slotId; // a row names its own slot
        if (!DecisionCanSubmit || _multiDraftKey != MultiKey || MultiProvinceSlot is not { } slot || !MultiProvinceLegal(owner, index)) return;
        if (!_multiProvinces.Remove(slot.Slot, out var old) || old != (owner, index)) _multiProvinces[slot.Slot] = (owner, index);
        MultiAdvance(slot.Slot); RefreshMultiTargets();
    }
    private void RenderMultiProvinceRows(TargetSlotOption slot)
    {
        _inspector.AddChild(Palette.Wrapped("Click a highlighted province on the table or choose below. Hover a row to find its province on the table.", 14, Palette.PaperDim));
        foreach (var (heading, owner) in new[] { ("OPPONENT'S PROVINCES", View.Players.First(p => p.Id != View.ViewerId).Id), ("YOUR PROVINCES", View.ViewerId) })
        {
            var rows = (slot.ProvinceChoices ?? []).Where(c => c.Owner == owner).Select(c => c.Index).Distinct().Order().ToArray();
            if (rows.Length == 0) continue;
            _inspector.AddChild(Palette.Text(heading, 11, Palette.Gold));
            foreach (var index in rows)
            {
                var (ownerId, provinceIndex) = (owner, index);
                var chosen = _multiProvinces.TryGetValue(slot.Slot, out var current) && current == (ownerId, provinceIndex);
                var label = ProvinceLabel(ownerId, provinceIndex);
                var row = SidebarSkin.Button((chosen ? "✓  " : "○  ") + label, () => ChooseMultiProvince(ownerId, provinceIndex, slot.Slot));
                row.Alignment = HorizontalAlignment.Left; row.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.ClipText = true;
                row.SetMeta("province_target_row", $"{ownerId}:{provinceIndex}"); row.SetMeta("slot", slot.Slot);
                row.Disabled = !DecisionCanSubmit; row.TooltipText = label;
                if (chosen) row.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.35f, 1.2f, .9f)));
                row.MouseEntered += () => HoverProvinceTarget(ownerId, provinceIndex, true);
                row.MouseExited += () => HoverProvinceTarget(ownerId, provinceIndex, false);
                row.FocusEntered += () => HoverProvinceTarget(ownerId, provinceIndex, true);
                row.FocusExited += () => HoverProvinceTarget(ownerId, provinceIndex, false);
                row.TreeExiting += () => HoverProvinceTarget(ownerId, provinceIndex, false);
                _inspector.AddChild(row);
            }
        }
    }
    private bool RenderMultiTargets()
    {
        if (!MultiTargetActive) { ClearMultiTargets(); return false; }
        if (_multiDraftKey != MultiKey) BeginMultiTargets();
        if (PresentationBusy || _reviewingAlternativeCost) return false;
        var action = _chosen!; var key = _multiDraftKey; _guide.Visible = false;
        var source = FindCard(action.CardId ?? action.Command!.CardId ?? "");
        var title = Palette.Wrapped(source?.Definition?.Name.ToUpperInvariant() ?? "CHOOSE TARGETS", 26);
        title.AddThemeFontOverride("font", Palette.HeadingFace); title.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(title);
        _sidebarHeader.AddChild(new HSeparator());
        var context = new HBoxContainer(); context.AddThemeConstantOverride("separation", 12); _inspector.AddChild(context);
        if (source is { Definition: not null })
        {
            var face = new DecisionSourceFace(source, 140); context.AddChild(face); LinkToTable(face, source.Id); face.Pressed += () => InspectMultiCard(source);
        }
        var explanation = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; explanation.AddThemeConstantOverride("separation", 8); context.AddChild(explanation);
        var label = action.Command!.PerformerId is { } performer ? action.Label.Replace($" ({performer})", "", StringComparison.Ordinal) : action.Label;
        explanation.AddChild(Palette.Wrapped(label, 18));
        if (action.Command.PerformerId is { } id && FindCard(id) is { } performing)
            explanation.AddChild(Palette.Wrapped("Performer: " + ShortName(performing), 16, Palette.PaperDim));
        _inspector.AddChild(Palette.Wrapped("Choose on the board or below. Click a chosen card again to remove it.", 16, Palette.PaperDim));
        foreach (var slot in MultiUpFront)
        {
            var count = MultiCount(slot); var open = _multiExpanded.Contains(slot.Slot);
            var header = SidebarSkin.Button((open ? "▾ " : "▸ ") + slot.Label + $"\n{count} selected · Choose {(slot.Min == slot.Max ? slot.Min.ToString() : $"{slot.Min}–{slot.Max}")}", () =>
            {
                if (!DecisionCanSubmit || key != _multiDraftKey) return;
                OpenMultiSlot(slot.Slot);
                RenderInspector(); Highlight();
            });
            header.Name = "MultiGroup_" + slot.Slot; header.CustomMinimumSize = new(0, 78); header.AddThemeFontSizeOverride("font_size", 18);
            if (_multiActiveSlot == slot.Slot) header.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.35f, 1.22f, .9f)));
            header.Disabled = !DecisionCanSubmit; _inspector.AddChild(header);
            if (!open) continue;
            if (slot.Kind == TargetSlotKinds.Player)
            {
                foreach (var playerId in slot.PlayerIds)
                {
                    var player = View.Players.Single(p => p.Id == playerId); var selected = _multiPlayers.GetValueOrDefault(slot.Slot, -1) == playerId;
                    var button = SidebarSkin.Button((selected ? "✓ " : "") + (playerId == View.ViewerId ? "You" : "Your opponent") + " · " + player.Clan, () =>
                    {
                        if (!DecisionCanSubmit || key != _multiDraftKey) return;
                        if (!_multiPlayers.Remove(slot.Slot, out var old) || old != playerId) _multiPlayers[slot.Slot] = playerId;
                        MultiAdvance(slot.Slot); RefreshMultiTargets();
                    });
                    button.SetMeta("slot", slot.Slot); button.SetMeta("player", playerId); button.Disabled = !DecisionCanSubmit; _inspector.AddChild(button);
                }
            }
            else if (slot.Kind == TargetSlotKinds.Province) RenderMultiProvinceRows(slot);
            else
            {
                var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10); _inspector.AddChild(grid);
                foreach (var targetId in slot.CardIds.OrderBy(id => _multiCards[slot.Slot].IndexOf(id) is var order && order >= 0 ? order : int.MaxValue))
                {
                    if (FindCard(targetId) is not { Definition: not null } target) continue;
                    var selected = _multiCards[slot.Slot].IndexOf(targetId);
                    var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 4); grid.AddChild(column);
                    var ordinal = MultiUpFront.TakeWhile(s => s.Slot != slot.Slot).Sum(s => _multiCards.GetValueOrDefault(s.Slot)?.Count ?? 0) + selected + 1;
                    var face = new MultiTargetFace(target, selected >= 0 ? ordinal : null);
                    face.SetMeta("slot", slot.Slot); face.SetMeta("card", targetId); face.SetMeta("focus_key", slot.Slot + "/" + targetId);
                    face.Disabled = !DecisionCanSubmit || selected < 0 && count >= slot.Max;
                    face.Pressed += () => { if (key == _multiDraftKey) ToggleMultiCard(slot.Slot, targetId); }; column.AddChild(face);
                    var inspect = SidebarSkin.Button("Inspect", () => InspectMultiCard(target)); inspect.CustomMinimumSize = new(164, 32);
                    inspect.AddThemeFontSizeOverride("font_size", 15); inspect.AutowrapMode = TextServer.AutowrapMode.Off;
                    var inspectStyle = SidebarSkin.Surface(true); inspectStyle.ContentMarginTop = inspectStyle.ContentMarginBottom = 4;
                    inspect.AddThemeStyleboxOverride("normal", inspectStyle); inspect.SetMeta("inspect_card", targetId); column.AddChild(inspect);

                }
            }
        }
        foreach (var followup in action.Slots!.Where(s => s.Timing == TargetSlotTimings.AtResolution))
        {
            _inspector.AddChild(new HSeparator());
            var note = Palette.Wrapped(followup.Label, 17, Palette.Gold);
            note.TooltipText = string.Join("\n", followup.OptionLabels ?? []); _inspector.AddChild(note);
            _inspector.AddChild(Palette.Wrapped("The game will ask this only if the condition is met.", 15, Palette.PaperDim));
        }
        if (MultiAllFull && MultiUpFront.Count() > 1)
        {
            _inspector.AddChild(new HSeparator());
            _inspector.AddChild(Palette.Text("SUMMARY", 11, Palette.Gold));
            foreach (var slot in MultiUpFront) if (MultiSelectionText(slot) is { Length: > 0 } text) _inspector.AddChild(Palette.Wrapped("• " + slot.Label + ": " + text, 16, Palette.Paper));
        }
        else if (MultiUpFront.FirstOrDefault(s => s.Slot == _multiActiveSlot) is { } openSlot && MultiCount(openSlot) >= openSlot.Min && MultiCount(openSlot) < openSlot.Max
            && MultiUpFront.SkipWhile(s => s.Slot != openSlot.Slot).Skip(1).Any())
        {
            var done = SidebarSkin.Button("Done with this group", () => { var next = MultiUpFront.SkipWhile(s => s.Slot != openSlot.Slot).Skip(1).First(); OpenMultiSlot(next.Slot); RenderInspector(); Highlight(); });
            done.Name = "MultiNextGroup"; done.Disabled = !DecisionCanSubmit; _inspector.AddChild(done);
        }
        var total = MultiUpFront.Sum(MultiCount);
        if (MultiUpFront.Any(s => MultiCount(s) > 0) || _performerPicked && PerformerClicks().Count > 0)
        { var back = SidebarSkin.Button("← Back", MultiBack); back.Name = "FlowBack"; back.Disabled = !DecisionCanSubmit; _decisionActions.AddChild(back); }
        _decisionActions.AddChild(Palette.Wrapped($"{total} selected" + (MultiReady ? " · Cards resolve in selection order." : " · Complete each required group."), 17, MultiReady ? Palette.Gold : Palette.PaperDim));
        if (action.AlternativeCost is not null && !_alternativeCostConfirmed)
        {
            var costs = SidebarSkin.Button("Review cost choices", () => { _reviewingAlternativeCost = true; _decisionSelection = null; RenderInspector(); }, true);
            costs.Disabled = !MultiReady || !DecisionCanSubmit; _decisionActions.AddChild(costs);
        }
        else
        {
            var (required, ready) = RenderGoldPanel(action);
            _paymentConfirm = SidebarSkin.Button(CommitButtonLabel(action) + (total > 0 ? $" · {total} target{(total == 1 ? "" : "s")}" : "") + (required > 0 ? $" · {required} Gold" : ""), () =>
            {
                if (!DecisionCanSubmit || !MultiTargetActive || key != _multiDraftKey || !MultiReady || !ready) return;
                _ = Submit(MultiCommand() with { SourceIds = required > 0 ? _sources.ToArray() : [] });
            }, true);
            _paymentConfirm.Name = "CommitAction";
            _paymentConfirm.Disabled = !DecisionCanSubmit || !MultiReady || !ready; _decisionActions.AddChild(_paymentConfirm);
        }
        _decisionActions.AddChild(SidebarSkin.Button("Cancel action", CancelChoice)); return true;
    }
    private void InspectMultiCard(CardView card)
    {
        if (!MultiTargetActive || _multiDraftKey != MultiKey) return;
        if (_multiInspection is not null && IsInstanceValid(_multiInspection)) { _multiInspection.Hide(); _multiInspection.QueueFree(); }
        var (window, body) = Dialog(card.Definition?.Name ?? "Card", 650, 780); _multiInspection = window; body.AddChild(CardPreview.Face(card, 610));
    }
    private void FrameReactionTarget()
    {
        var key = View.PendingChoice?.Type == "beforeActionReaction" ? $"{_session.MatchKey}/{View.ViewerId}/{View.PendingChoice.CardId}" : null;
        if (_reactionCameraKey == key) return;
        if (PresentationBusy) return;
        _reactionCameraKey = key;
        if (key is null) return;
        if (BattleActive) FocusBattle();
        else if (_tiles.FirstOrDefault(t => t.Card.Id == View.PendingChoice!.CardId && _world.IsAncestorOf(t)) is { } tile)
        {
            var region = new Rect2(ScreenToWorld(ScreenCenter(tile)) - new Vector2(240, 260), new Vector2(480, 520));
            _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
        }
    }
    private void RenderReactionTarget()
    {
        if (View.PendingChoice?.Type != "beforeActionReaction" || FindCard(View.PendingChoice.CardId ?? "") is not { Definition: not null } target) return;
        _inspector.AddChild(Palette.Wrapped("Reaction for " + ShortName(target), 20, Palette.Gold));
        var face = new DecisionSourceFace(target, 190); face.Pressed += () => InspectAttackCard(target); LinkToTable(face, target.Id); _inspector.AddChild(face);
    }
    private bool MultiSelectionsAllowed(ActionOption option) => option.Slots is not null && option.Slots.Where(s => s.Timing == TargetSlotTimings.UpFront).All(s =>
        s.Kind == TargetSlotKinds.Card ? _multiCards.TryGetValue(s.Slot, out var selected) && selected.Count >= s.Min && selected.Count <= s.Max && selected.All(s.CardIds.Contains) :
        s.Kind == TargetSlotKinds.Province ? _multiProvinces.TryGetValue(s.Slot, out var province) ? s.ProvinceChoices?.Any(c => c.Owner == province.Owner && c.Index == province.Index) == true : s.Min == 0 :
        _multiPlayers.TryGetValue(s.Slot, out var player) ? s.PlayerIds.Contains(player) : s.Min == 0);
    private void FocusMultiTargets()
    {
        if (!MultiTargetActive) return;
        var candidates = MultiUpFront.SelectMany(s => s.CardIds).Append(_chosen!.Command!.PerformerId ?? _chosen.CardId ?? "").ToHashSet();
        var bounds = _tiles.Where(t => candidates.Contains(t.Card.Id) && _world.IsAncestorOf(t)).Select(t =>
            new Rect2(ScreenToWorld(ScreenCenter(t)) - new Vector2(90, 110), new Vector2(180, 220))).ToArray();
        if (bounds.Length == 0) return;
        var region = bounds.Aggregate((a, b) => a.Merge(b)).Grow(60); _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }
    private Vector2 MultiTargetPoint(CardTile tile)
    {
        if (tile.Card.AttachedTo is not null && _world.IsAncestorOf(tile))
            return _tableSurface.ProjectCanvas(tile.GetGlobalRect().Position + new Vector2(tile.Size.X / 2, Math.Min(10, tile.Size.Y / 2)));
        return ScreenCenter(tile);
    }
}
