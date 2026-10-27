using Godot;

namespace L5R.Client;

/// <summary>
/// A sidebar zone that is as tall as its content up to <see cref="MaxHeight"/>, then scrolls (mouse wheel, or the bar at its right).
/// The sidebar's fixed zones (battle header, guide, decision and phase buttons) sit in a column of fixed height, so a zone that grew without limit
/// (a long list of announced Reactions, a long prompt) pushed the buttons below it off the panel and the player could not reach them.
/// </summary>
public partial class SidebarScroll : ScrollContainer
{
    public float MaxHeight { get; init; } = 260;

    public SidebarScroll()
    {
        HorizontalScrollMode = ScrollMode.Disabled; VerticalScrollMode = ScrollMode.Auto;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ChildEnteredTree += node => { if (node is Control control) { control.MinimumSizeChanged += FitContent; control.VisibilityChanged += FitContent; FitContent(); } };
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
