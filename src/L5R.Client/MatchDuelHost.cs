using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Normal-match host for the very same screen used by DuelDemoHost. Never owns/replaces the session.</summary>
public partial class MatchDuelHost : Control
{
    public MatchSession Session { get; init; } = null!;
    public MenuPreferences Preferences { get; init; } = null!;
    public event Action? Finished;
    public event Action? Reconnect;
    public event Action? Leave;
    public event Action? SwitchSeat;
    public DuelScreen Screen { get; private set; } = null!;
    public bool Covered { get; set; } = true;
    public bool Paused => Covered || _menuOpen || _handoff || _help || Screen.HasPrivateInspection || Screen.PresentationBusy || Unavailable;
    private bool _menuOpen, _help, _sending, _handoff, _matchEnded;
    public bool CanReturn => Screen.CanContinue || _matchEnded;
    private Control _overlay = null!;
    private Label _connection = null!;
    private bool Unavailable => Session.Busy || Session.Online && (!Session.Connected || Session.Snapshot?.PlayersConnected?.Any(c => !c) == true);
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore; Theme = DuelUi.Theme();
        Screen = new DuelScreen { ReducedMotion = Preferences.ReducedMotion, ExternalEntrance = true, InputLocked = true }; AddChild(Screen);
        Screen.SetGuidanceEnabled(Preferences.DuelGuidance);
        Screen.SaveGuidancePreference = enabled => { var prior = Preferences.DuelGuidance; Preferences.DuelGuidance = enabled; if (Preferences.Save() == Error.Ok) return true; Preferences.DuelGuidance = prior; return false; };
        Screen.GuidanceHelpChanged += open => _help = open;
        Screen.CommandRequested += command => _ = Submit(command);
        Screen.PresentationFinished += () => { if (!Covered) Finished?.Invoke(); };
        Screen.ReadingChanged += open => _overlay.Visible = !open && !Covered;
        _overlay = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_overlay); _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += BuildOverlay; BuildOverlay();
    }
    public void Present(SessionSnapshot snapshot)
    {
        Screen.Present(snapshot);
        var ended = snapshot.View.WinnerId is not null && snapshot.View.LastDuel?.Complete != true;
        if (ended != _matchEnded) { _matchEnded = ended; if (ended) Screen.SkipCinematic(); BuildOverlay(); }
        RefreshLock();
    }
    public void Enter(bool introduce)
    {
        Covered = false; Screen.SetEntranceCovered(false);
        Screen.CompleteExternalEntrance(introduce); RefreshLock();
    }
    public void Notify(string text) { if (_connection is not null) { _connection.Text = text; _connection.Show(); } }
    private void RefreshLock()
    {
        Screen.InputLocked = Covered || _menuOpen || _handoff || _sending || _matchEnded || Unavailable && !CanReturn;
        _overlay.Visible = !Covered && !Screen.HasPrivateInspection;
        if (Session.Online)
        {
            _connection.Text = !Session.Connected ? "Connection lost · Menu → Reconnect" : Session.Snapshot?.PlayersConnected?.Any(c => !c) == true ? "Waiting for your opponent to reconnect" : "";
            _connection.Visible = _connection.Text.Length > 0;
        }
    }
    private async Task Submit(GameCommand command)
    {
        if (_sending) return; // the command in flight answers the screen
        if (Paused || Session.Snapshot?.View.Version != command.ExpectedVersion) { Screen.SubmissionFinished(); return; } // not sent: release the screen rather than leave it waiting
        _sending = true; RefreshLock();
        try { await Session.Submit(command); }
        finally { _sending = false; Screen.SubmissionFinished(); RefreshLock(); }
    }
    private void BuildOverlay()
    {
        if (_overlay is null) return;
        Palette.Clear(_overlay);
        var size = GetViewportRect().Size;
        var menu = DuelUi.Button("Menu", () => SetMenu(true), fontSize: 34); menu.Position = new Vector2(18, 16); menu.Size = new Vector2(115, 42); _overlay.AddChild(menu);
        _connection = DuelUi.Text("", 18, Palette.Paper); _connection.Position = new Vector2(25, size.Y - 35); _connection.Size = new Vector2(size.X - 50, 30); _overlay.AddChild(_connection);
        if (_matchEnded)
        {
            var veil = new ColorRect { Color = new Color(0, 0, 0, .6f), Size = size }; _overlay.AddChild(veil);
            var box = DuelUi.Modal(veil, size, new Vector2(580, 310), "Match complete");
            var view = Screen.Snapshot!.View;
            box.AddChild(DuelUi.Wrapped($"{view.Players.Single(p => p.Id == view.WinnerId).Clan} wins · {view.VictoryReason}", 24));
            box.AddChild(DuelUi.Wrapped("The match ended before this duel finished.", 18, Palette.PaperDim));
            box.AddChild(DuelUi.Button("Return to table", () => Finished?.Invoke(), true));
        }
        else if (_handoff)
        {
            var curtain = new ColorRect { Color = new Color("101017"), Size = size }; _overlay.AddChild(curtain);
            var handoff = DuelUi.Modal(curtain, size, new Vector2(560, 260), "Pass the seat");
            handoff.AddChild(DuelUi.Wrapped("Private cards are hidden. Pass the device before revealing the other seat.", 21));
            handoff.AddChild(DuelUi.Button("Reveal this seat", () => { _handoff = false; Screen.Show(); BuildOverlay(); }, true));
        }
        else if (_menuOpen && !_confirmConcede)
        {
            var veil = new ColorRect { Color = new Color(0, 0, 0, .55f), Size = size }; _overlay.AddChild(veil);
            var box = DuelUi.Modal(veil, size, new Vector2(530, Session.Online ? 600 : 520), "Duel");
            void Button(string label, Action action, bool primary = false) => box.AddChild(DuelUi.Button(label, action, primary));
            Button("Resume", () => SetMenu(false), true);
            Button("Challenge details…", () => { SetMenu(false); Screen.OpenChallenge(); });
            Button("Duel guidance…", () => { SetMenu(false); Screen.OpenGuidance(); });
            Button("Public piles…", () => { SetMenu(false); Screen.OpenPublicPiles(); });
            if (!Session.Online)
            {
                var seat = DuelUi.Button("Switch seat", () => { _menuOpen = false; _handoff = true; Screen.ClearReading(); Screen.Hide(); SwitchSeat?.Invoke(); BuildOverlay(); });
                seat.Disabled = Screen.PresentationBusy; box.AddChild(seat);
            }
            if (Session.Online) Button("Reconnect", () => { SetMenu(false); Reconnect?.Invoke(); });
            if (ConcedeOption is not null) Button("Concede match…", OpenConcede);
            // Leaving uses the main application's confirmation and retains its usual room flow.
            Button("Return to menu…", () => Leave?.Invoke());
        }
        if (_confirmConcede && !_matchEnded) BuildConcede();
        RefreshLock();
    }
    private void SetMenu(bool open) { _confirmConcede = false; _menuOpen = open; Screen.ClearReading(); BuildOverlay(); }
    public override void _Process(double delta) => RefreshLock();
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!Covered && !_handoff && e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) { if (_confirmConcede) CancelConcede(); else SetMenu(!_menuOpen); GetViewport().SetInputAsHandled(); }
    }
}
