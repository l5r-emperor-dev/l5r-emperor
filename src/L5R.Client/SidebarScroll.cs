using Godot;

namespace L5R.Client;

/// <summary>
/// A sidebar zone that is as tall as its content up to <see cref="MaxHeight"/>, then scrolls (mouse wheel, or the bar at its right).
/// The sidebar's fixed zones (battle header, guide, decision and phase buttons) sit in a column of fixed height, so a zone that grew without limit
/// (a long list of announced Reactions, a long prompt) pushed the buttons below it off the panel and the player could not reach them.
/// </summary>
public partial class SidebarScroll : ScrollContainer
{
    private float _maxHeight = 260;
    private readonly HashSet<ulong> _watched = [];
    /// <summary>The tallest the zone grows before it scrolls. Settable so a zone can make room for extra content (issue #1333: the held-action box).</summary>
    public float MaxHeight { get => _maxHeight; set { if (_maxHeight == value) return; _maxHeight = value; if (IsInsideTree()) FitContent(); } }

    public SidebarScroll()
    {
        HorizontalScrollMode = ScrollMode.Disabled; VerticalScrollMode = ScrollMode.Auto;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ChildEnteredTree += node =>
        {
            if (node is not Control control) return;
            // A child re-enters the tree when the table is reparented (a match duel opens and closes) and keeps its connections: connect it once, or Godot logs each duplicate.
            if (!_watched.Add(control.GetInstanceId())) { FitContent(); return; }
            control.MinimumSizeChanged += FitContent; control.VisibilityChanged += FitContent; FitContent();
            // Content added to an empty (hidden) zone shows at once, not a frame later from _Process: a button rendered this frame (a drafted action's Confirm) is on screen and pressable
            // as soon as it exists. Emptying still hides the zone from _Process.
            if (control is Container) control.ChildEnteredTree += _ => { if (!Visible) Visible = true; };
        };
    }

    public override void _Ready()
    {
        var bar = GetVScrollBar();
        bar.CustomMinimumSize = new Vector2(12, 0);
        var track = new StyleBoxFlat { BgColor = new Color(0.87f, 0.82f, 0.69f, 0.12f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 };
        bar.AddThemeStyleboxOverride("scroll", track);
        foreach (var (name, color) in new[] { ("grabber", new Color("a89f88")), ("grabber_highlight", new Color("dfd1af")), ("grabber_pressed", new Color("f8d590")) })
            bar.AddThemeStyleboxOverride(name, new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
    }

    /// <summary>A zone with nothing in it takes no room (not even the gap the column keeps between zones), as a hidden control did before the zones scrolled.</summary>
    public override void _Process(double delta)
    {
        var empty = GetChildren().OfType<Control>().All(c => !c.Visible || c is Container && c.GetChildCount() == 0);
        if (Visible == empty) Visible = !empty;
        FitContent();
    }

    /// <summary>The native minimum is zero along the scrolling axis, so the zone asks for its content's height (never more than the cap) through its custom minimum, kept current as the content changes.</summary>
    private void FitContent()
    {
        var content = 0f;
        foreach (var child in GetChildren())
            if (child is Control { Visible: true } control) content = Math.Max(content, control.GetCombinedMinimumSize().Y);
        var height = Math.Min(content, MaxHeight);
        if (!Mathf.IsEqualApprox(CustomMinimumSize.Y, height)) CustomMinimumSize = new Vector2(0, height);
    }
}
