using Godot;
using System.Text.Json;

namespace L5R.Client;

public partial class Main
{
    private (AcceptDialog Dialog, VBoxContainer Body) Dialog(string title, int width = 550, int height = 420)
    {
        var dialog = new AcceptDialog { Title = title, Theme = SidebarSkin.Theme(), Exclusive = true, Transient = true, WrapControls = false, MinSize = new Vector2I(width, height) };
        AddChild(dialog);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 14); dialog.AddChild(body);
        dialog.GetOkButton().Text = "Close";
        dialog.GetOkButton().CustomMinimumSize = new Vector2(140, 48);
        dialog.CloseRequested += dialog.QueueFree; dialog.Confirmed += dialog.QueueFree;
        PresentDialog(dialog, width, height); return (dialog, body);
    }

    /// <summary>
    /// A dialog that supplies its own way out instead of the built-in Close button. Hiding only the button leaves its (invisible) button row over the bottom of the body, where it took every click:
    /// the table menu's "Return to game" sat under it and did nothing. Hiding the row too gives the body the whole dialog.
    /// </summary>
    private static void HideDialogButtons(AcceptDialog dialog)
    {
        dialog.GetOkButton().Hide();
        if (dialog.GetOkButton().GetParent() is Control row) row.Hide();
    }

    private void PresentDialog(Window dialog, int width, int height)
    {
        var scale = GetViewport().GetVisibleRect().Size.Y / 1080f;
        dialog.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        // AcceptDialog computes its child layout using ContentScaleFactor.
        // A virtual ContentScaleSize alone expands the frame but leaves controls small.
        dialog.ContentScaleSize = Vector2I.Zero;
        dialog.ContentScaleFactor = scale;
        dialog.MinSize = Vector2I.Zero;
        dialog.PopupCentered(new Vector2I(Mathf.RoundToInt(width * scale), Mathf.RoundToInt(height * scale)));
    }

    private void Confirm(string title, string message, Action yes, string? yesText = null, string? noText = null)
    {
        var dialog = new ConfirmationDialog { Title = title, DialogText = message, Theme = SidebarSkin.Theme(), Exclusive = true, Transient = true };
        if (yesText is not null) dialog.OkButtonText = yesText;
        if (noText is not null) dialog.CancelButtonText = noText;
        AddChild(dialog); dialog.Confirmed += () => { yes(); dialog.QueueFree(); }; dialog.Canceled += dialog.QueueFree;
        dialog.CloseRequested += dialog.QueueFree; PresentDialog(dialog, 540, 170);
    }

    private void ShowPractice() => ShowPracticeSetup();

    private void ShowSettings() => ShowFrontendOptions();

    private void ShowOnline()
    {
        var (dialog, body) = Dialog("Private match", 650, 740);
        body.AddChild(Palette.Text("Meet across the table", 28, title: true));
        body.AddChild(Palette.Wrapped("Create a room, then give its code to your opponent. Both players must connect to the same server.", 16, Palette.PaperDim));
        body.AddChild(Palette.Text("Server address", 13, Palette.Gold));
        var address = new LineEdit { Text = _savedServer, PlaceholderText = "https://your-server.example" }; body.AddChild(address);
        body.AddChild(Palette.Text("Room code · leave empty to create", 13, Palette.Gold));
        var room = new LineEdit { PlaceholderText = "Room code", MaxLength = 12 }; body.AddChild(room);
        var published = new CheckBox { Text = "Create with published Lion / Crane decks", ButtonPressed = true }; body.AddChild(published);
        var status = Palette.Wrapped(_session.Online ? $"Current room: {_session.Seat?.RoomCode}" : "For local testing, start the server with tools/run-server.sh.", 14, Palette.Muted); body.AddChild(status);
        Button connect = null!;
        connect = SidebarSkin.Button("Enter private match", async () =>
        {
            connect.Disabled = true; status.Text = "Connecting…";
            try
            {
                await _session.CreateOrJoin(address.Text, room.Text, published.ButtonPressed);
                SetNotice($"Room {_session.Seat!.RoomCode} · share this code with your opponent.");
                if (IsInstanceValid(dialog)) dialog.QueueFree();
                _selected = null; _chosen = null;
            }
            // Closed while connecting (issue #1103): the player has moved on, so there is nothing left to tell.
            catch (Exception e) when (!IsInstanceValid(dialog)) { if (e is OperationCanceledException) SetNotice(e.Message); }
            catch (Exception e) { status.Text = e.Message; connect.Disabled = false; }
        }, true); body.AddChild(connect);
        if (_savedSeat is not null) body.AddChild(SidebarSkin.Button($"Reconnect to {_savedSeat.RoomCode}", async () => { if (await Reconnect() && IsInstanceValid(dialog)) dialog.QueueFree(); }));
        body.AddChild(Palette.Wrapped("Your player seat is saved locally for reconnecting. Remote play requires an HTTPS server; hosting is not provisioned by this prototype.", 13, Palette.Muted));
    }

    private async Task<bool> Reconnect()
    {
        if (_savedSeat is null) { SetNotice("No saved private seat on this device."); return false; }
        try { await _session.Resume(_savedServer, _savedSeat); _selected = null; _chosen = null; return true; }
        catch (Exception e) { SetNotice(e.Message); return false; }
    }

    private void SaveSeat(RoomSeat seat, string server)
    {
        _savedSeat = seat; _savedServer = server;
        try
        {
            var path = SeatPath;
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new SavedConnection(server, seat), Json));
            // Create with owner-only access from the first write on Unix; tokens never enter a replay or log.
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = System.IO.FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporary, options)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetNotice("This seat could not be saved to disk. Reconnect is available while the app stays open.");
        }
    }

    private void LoadSeat()
    {
        try
        {
            var path = SeatPath;
            if (!File.Exists(path)) return;
            var saved = JsonSerializer.Deserialize<SavedConnection>(File.ReadAllText(path), Json);
            _savedSeat = saved?.Seat; _savedServer = saved?.Server ?? _savedServer;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { GD.PushWarning("The previous room seat could not be loaded."); }
    }
    private sealed record SavedConnection(string Server, RoomSeat Seat);

    private AcceptDialog? _seatHandoff;
    private void SwitchSeat()
    {
        if (_session.Online || _session.Busy || _session.Snapshot is null
            || _seatHandoff is not null && IsInstanceValid(_seatHandoff)) return;
        var generation = _session.Generation;
        var originalSeat = _session.Snapshot.View.ViewerId;
        _cameraDragging = false; HideTableTooltip(); ReleaseTableFocus();
        _root.Visible = false;
        var (dialog, body) = Dialog("Pass the device", 530, 310);
        _seatHandoff = dialog;
        var switched = false;
        HideDialogButtons(dialog);
        dialog.Canceled += dialog.QueueFree;
        dialog.TreeExiting += () =>
        {
            _seatHandoff = null;
            // Closing/cancelling is never permission to reveal a different private view.
            // A stale dialog also cannot change or uncover a replacement session.
            if (!IsInsideTree() || IsQueuedForDeletion() || !IsInstanceValid(_root)
                || _session.Generation != generation || _session.Online
                || _session.Snapshot?.View.ViewerId != (switched ? 1 - originalSeat : originalSeat)) return;
            if (switched) Render();
            if (!MenuVisible && !PracticeVisible && !MatchDuelOpen) _root.Visible = true;
        };
        body.AddChild(Palette.Wrapped("Hand the device to the other player before revealing their private hand.", 21));
        body.AddChild(SidebarSkin.Button("I'm ready · reveal my hand", () =>
        {
            if (switched || dialog.IsQueuedForDeletion()) return;
            if (_session.Generation != generation || _session.Online || _session.Busy
                || _session.Snapshot?.View.ViewerId != originalSeat) { dialog.QueueFree(); return; }
            switched = true;
            _selected = null; _selectedHomeReminder = null; _chosen = null;
            _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; _expandedUnits.Clear();
            _presentationSnapshot = null; _motionView = null; _battlePresentationStage = 0;
            _session.SwitchPracticeSeat(); dialog.QueueFree();
        }, true));
        body.AddChild(SidebarSkin.Button("Cancel handoff", dialog.QueueFree));
    }

    private void OpenDecksLocation()
    {
        Directory.CreateDirectory(DecksDirectory);
        OS.ShellOpen(DecksDirectory);
    }

    private void ExportReplay()
    {
        var replay = _session.ExportPracticeReplay();
        if (replay is null) { SetNotice("Practice replays can be exported from a local match."); return; }
        var name = "practice-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
        var path = ProjectSettings.GlobalizePath("user://" + name);
        File.WriteAllText(path, replay);
        SetNotice("Replay saved: " + path);
        OS.ShellOpen(ProjectSettings.GlobalizePath("user://"));
    }

    private void ShowHistory()
    {
        if (_session.Snapshot is null) { SetNotice("Connect to a match to see its history."); return; }
        var (_, body) = Dialog("Match history", 760, 620);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 400), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(rows);
        var events = (_session.Snapshot?.View.Events ?? View.Events).ToArray();
        for (var i = events.Length - 1; i >= 0;)
        {
            var end = i;
            var battle = Array.FindLastIndex(events, i, i + 1, e => e.Type == "battleResolved");
            if (battle >= 0 && !events.Skip(battle + 1).Take(i - battle).Any(e => e.Type == "battleResolved" || e.Type == "phaseChanged"))
            {
                var details = new VBoxContainer { Visible = false }; var excerpt = events[battle..(end + 1)];
                var caption = excerpt.FirstOrDefault(e => e.Type == "provinceDestroyed")?.Message ?? events[battle].Message;
                rows.AddChild(SidebarSkin.Button(caption + " · Details", () => details.Visible = !details.Visible));
                foreach (var e in excerpt) details.AddChild(Palette.Wrapped(e.Message, 15, Palette.PaperDim));
                rows.AddChild(details); i = battle - 1;
            }
            else { rows.AddChild(Palette.Wrapped($"{events[i].Sequence:000}   {events[i].Message}", 15, Palette.PaperDim)); i--; }
        }
    }

    private void ShowCoverage()
    {
        if (_session.Snapshot is null) { SetNotice("Open a practice or connect to a match to inspect its rules coverage."); return; }
        var (_, body) = Dialog("Prototype rules coverage", 700, 540);
        body.AddChild(Palette.Text("A rules-first foundation", 27, title: true));
        body.AddChild(Palette.Wrapped("Implemented: turn sequence, one-payment Gold, Clan costs, Proclaim, infantry assignment, published Lion/Crane abilities, Imperial Favor, Border Keep refresh, attachments, province destruction, Honor and Dishonor timing, hidden information and deterministic replay.", 16));
        foreach (var limitation in View.Limitations) body.AddChild(Palette.Wrapped("• " + limitation, 14, Palette.PaperDim));
        body.AddChild(Palette.Wrapped("The selected 40-card pool is playable. Other imported decks and catalogue records remain reference material.", 15, Palette.Gold));
    }
}
