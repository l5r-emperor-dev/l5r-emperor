using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private PanelContainer _shownPanel = null!;
    private VBoxContainer _shownList = null!;
    private ScrollContainer _shownScroll = null!;
    private Button _shownToggle = null!;
    private Label _shownSubtitle = null!;
    private bool _shownOpen;
    private string? _shownMatchKey;
    private readonly Dictionary<int, int> _shownReadSequence = [];
    private int _shownViewer = -1;
    private long _shownObservedVersion = -1;
    private int[] _shownSequences = [];
    private Control? _shownPreview;
    private ShownCardEntry? _shownHovered;
    private double _shownHoverTime;
    private bool _shownPinned;
    private ShownCardView[] OpponentShows => (_session.Snapshot?.View.ShownCards ?? [])
        .Where(s => s.ShownByPlayerId != _session.Snapshot!.View.ViewerId).OrderByDescending(s => s.Sequence).ToArray();
    private int ShownUnread => OpponentShows.Count(s => s.Sequence > _shownReadSequence.GetValueOrDefault(_session.Snapshot!.View.ViewerId));

    private void BuildShownCards()
    {
        _shownToggle = SidebarSkin.Button("Shown cards · 0", () => ToggleShownCards()); _shownToggle.Theme = SidebarSkin.Theme(); _shownToggle.ZIndex = 61;
        _shownToggle.AccessibilityName = "Open shown-card history"; _root.AddChild(_shownToggle);
        _shownPanel = new PanelContainer { Theme = SidebarSkin.Theme(), Visible = false, ZIndex = 60 };
        _shownPanel.AddThemeStyleboxOverride("panel", SidebarSkin.Surface()); _root.AddChild(_shownPanel);
        _shownPanel.Size = new(SidebarWidth, 892);
        _shownPanel.MinimumSizeChanged += () => _shownPanel.SetDeferred(Control.PropertyName.Size, new Vector2(SidebarWidth, 892));
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); _shownPanel.AddChild(body);
        var heading = new HBoxContainer(); body.AddChild(heading);
        var title = Palette.Text("SHOWN CARDS", 27, title: true); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(title);
        var close = SidebarSkin.Button("−", () => ToggleShownCards(false)); close.CustomMinimumSize = new(44, 44); close.AccessibilityName = close.TooltipText = "Minimize shown cards"; close.AddThemeFontSizeOverride("font_size", 26);
        foreach (var state in new[] { "normal", "hover", "pressed" }) { var style = SidebarSkin.Surface(true); style.ContentMarginLeft = style.ContentMarginRight = 4; style.ContentMarginTop = style.ContentMarginBottom = 4; close.AddThemeStyleboxOverride(state, style); }
        heading.AddChild(close);
        _shownSubtitle = Palette.Wrapped("Revealed by your opponent", 17, Palette.PaperDim); body.AddChild(_shownSubtitle); body.AddChild(new HSeparator());
        _shownScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(_shownScroll);
        _shownList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _shownList.AddThemeConstantOverride("separation", 12); _shownScroll.AddChild(_shownList);
        body.AddChild(Palette.Wrapped("Hover or focus to enlarge · click to keep open", 14, Palette.PaperDim));
        var acknowledge = SidebarSkin.Button("Okay", () => ToggleShownCards(false)); acknowledge.CustomMinimumSize = new(0, 52);
        acknowledge.AccessibilityName = "Acknowledge shown cards and return"; body.AddChild(acknowledge);
        FitShownCards();
    }
    private void FitShownCards()
    {
        if (_shownToggle is null) return;
        var x = _layoutWidth - SidebarWidth - 22;
        _shownToggle.Position = new(x, 998); _shownToggle.Size = new(SidebarWidth, 56);
        _shownPanel.Position = new(x, 94); _shownPanel.Size = new(SidebarWidth, 892);
        _inspectorPanel.Size = new(SidebarWidth, 892);
        HideShownPreview();
    }
    private void ObserveShownCards()
    {
        if (_session.Snapshot is null) { _shownPanel.Hide(); _shownToggle.Hide(); HideShownPreview(); return; }
        _shownToggle.Visible = !LookPanelOpen && !SearchPanelOpen;
        var live = _session.Snapshot.View;
        if (_shownMatchKey != _session.MatchKey)
        {
            _shownMatchKey = _session.MatchKey; _shownReadSequence.Clear(); _shownOpen = false;
            _shownSequences = []; _shownViewer = -1; HideShownPreview();
        }
        // Return a newly required decision to the foreground; inspection never consumes or changes it.
        if (_shownOpen && live.Version != _shownObservedVersion && live.PendingChoice?.PlayerId == live.ViewerId)
            _shownOpen = false;
        var records = OpponentShows;
        var sequences = records.Select(s => s.Sequence).ToArray();
        var viewerChanged = _shownViewer != live.ViewerId;
        var changed = viewerChanged || !sequences.SequenceEqual(_shownSequences);
        if (changed)
        {
            // Force the reveal open rather than leaving it to a toast the recipient can miss (Mendon's review, PR
            // #434): both when a new show lands while they are already seated, AND when they arrive at this seat
            // (a local "pass device" hand-off, the delivery mechanism itself) with an unread one already waiting.
            // They get a chance to actually inspect the card before doing anything else. The "Minimize to return
            // to your decision" subtitle already existed for exactly this case.
            // Issue #1319: a show arriving while seated is now presented as a held card (Main.ShownBeats.cs, which marks it read), so only the
            // hand-off arrival still opens the history here.
            if (ShownUnread > 0 && (viewerChanged || sequences.Length > _shownSequences.Length))
            {
                SetNotice($"{records[0].Definition.Name} was shown by {live.Players.Single(p => p.Id == records[0].ShownByPlayerId).Name}. Review Shown cards.");
                _shownOpen = true;
            }
            _shownViewer = live.ViewerId; _shownSequences = sequences;
            PopulateShownCards();
        }
        _shownObservedVersion = live.Version;
        if (_shownOpen) MarkShownRead();
        _shownPanel.Visible = _shownOpen && !LookPanelOpen && !SearchPanelOpen;
        _inspectorPanel.Visible = !LookPanelOpen && !SearchPanelOpen && !_shownOpen && (!EventDecisionActive || PresentationBusy);
        _eventSidebar.Visible = !_shownOpen && EventDecisionActive && !PresentationBusy;
        _shownToggle.Text = $"Shown cards · {records.Length}" + (ShownUnread > 0 ? $" · {ShownUnread} new" : "") + (_shownOpen ? "  ▾" : "  ▴");
        _shownToggle.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: ShownUnread > 0 ? new Color(1.35f, 1.25f, .9f) : Colors.White));
        _shownToggle.TooltipText = ShownUnread > 0 ? $"{ShownUnread} newly shown cards. Open history to review." : "Review every card your opponent deliberately showed this match.";
        _shownSubtitle.Text = live.PendingChoice?.PlayerId == live.ViewerId ? "Minimize to return to your decision" : "Revealed by your opponent";
        if (!_shownOpen) HideShownPreview();
    }
    private void MarkShownRead()
    { if (OpponentShows.FirstOrDefault() is { } newest) _shownReadSequence[_session.Snapshot!.View.ViewerId] = newest.Sequence; }
    private void ToggleShownCards(bool? open = null)
    {
        if (_session.Snapshot is null) return;
        _shownOpen = open ?? !_shownOpen; HideTableTooltip(); HideShownPreview(); ReleaseTableFocus();
        if (_shownOpen) { PopulateShownCards(); MarkShownRead(); }
        ObserveShownCards();
    }
    private void PopulateShownCards()
    {
        HideShownPreview(); Palette.Clear(_shownList);
        var records = OpponentShows;
        if (records.Length == 0)
        {
            _shownList.AddChild(Palette.Wrapped("No cards shown yet.", 22));
            _shownList.AddChild(Palette.Wrapped("Cards your opponent deliberately shows will stay here for this match. Their hand and deck remain private.", 18, Palette.PaperDim)); return;
        }
        foreach (var record in records)
        {
            var player = _session.Snapshot!.View.Players.Single(p => p.Id == record.ShownByPlayerId);
            var entry = new ShownCardEntry(record, player.Name, record.Sequence > _shownReadSequence.GetValueOrDefault(_session.Snapshot.View.ViewerId));
            entry.Pressed += () => ShowShownPreview(entry, true);
            entry.FocusEntered += () => ShowShownPreview(entry, false);
            entry.FocusExited += () => { if (!_shownPinned && _shownHovered == entry) HideShownPreview(); };
            entry.MouseEntered += () => { if (!_shownPinned) { _shownHovered = entry; _shownHoverTime = 0; } };
            entry.MouseExited += () => { if (!_shownPinned && _shownHovered == entry) { _shownHovered = null; HideShownPreview(); } };
            _shownList.AddChild(entry);
        }
        _shownScroll.ScrollVertical = 0;
    }
    private void ProcessShownCards(double delta)
    {
        if (!_shownOpen) return;
        if (_shownHovered is { } entry && IsInstanceValid(entry) && !_shownPinned && _shownPreview is null && (_shownHoverTime += delta) > .3)
            ShowShownPreview(entry, false);
    }
    private void ShowShownPreview(ShownCardEntry entry, bool pinned)
    {
        HideShownPreview(); _shownHovered = entry; _shownPinned = pinned; HideTableTooltip();
        var panel = new PanelContainer { Theme = SidebarSkin.Theme(), ZIndex = 80, MouseFilter = pinned ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", SidebarSkin.Surface()); _shownPreview = panel; _root.AddChild(panel);
        var extent = new Vector2(482, pinned ? 760 : 700); panel.Size = extent;
        panel.MinimumSizeChanged += () => panel.SetDeferred(Control.PropertyName.Size, extent);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 8); panel.AddChild(body);
        body.AddChild(CardPreview.Face(ShownCardEntry.PrintedCard(entry.Record), 610, 436));
        body.AddChild(Palette.Wrapped($"{entry.Record.Context} · Turn {entry.Record.TurnNumber}", 17, Palette.PaperDim));
        if (pinned) body.AddChild(SidebarSkin.Button("Close inspection", HideShownPreview));
        panel.Position = new(Math.Max(8, _shownPanel.Position.X - 500), Math.Clamp(160, 94, 1080 - 780)); panel.Size = new(482, pinned ? 760 : 700);
        if (!pinned) IgnoreShownInput(panel);
    }
    private static void IgnoreShownInput(Node node)
    { if (node is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) IgnoreShownInput(child); }
    private void HideShownPreview()
    { if (_shownPreview is not null && IsInstanceValid(_shownPreview)) _shownPreview.QueueFree(); _shownPreview = null; _shownHovered = null; _shownPinned = false; _shownHoverTime = 0; }
}
