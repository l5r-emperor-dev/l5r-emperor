using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>One public instance flag; neither a CreatedOnly definition nor copied abilities imply this status.</summary>
public static class CreatedCopyStatus
{
    private static Texture2D? _seal;
    // Keep the generated pixels; the atlas removes only excess transparent padding.
    public static Texture2D Seal => _seal ??= new AtlasTexture
    {
        Atlas = GD.Load<Texture2D>("res://Art/CreatedCopies/ghost-copy-marker.png"),
        Region = new Rect2(160, 126, 956, 956)
    };
    public static bool VisibleOn(CardView card) => card.CreatedCopy && card.FaceUp && card.Definition is not null;
    public static string Description(CardView card) => "Created copy\n" + (card.Zone == Zone.RemovedFromGame
        ? "Removed from the game." : "Removed from the game when it leaves play.");
    public static Control Explanation(CardView card)
    {
        var words = new VBoxContainer(); words.AddThemeConstantOverride("separation", 3); words.SetMeta("created_copy_status", card.Id);
        words.AddChild(Palette.Wrapped("Created copy", 19, Palette.Gold));
        words.AddChild(Palette.Wrapped(card.Zone == Zone.RemovedFromGame ? "Removed from the game." : "Removed from the game when it leaves play.", 16, Palette.PaperDim));
        return words;
    }
}

/// <summary>Approved overlapping ghost outlines with restrained wisps, outside the lower-left scan and token rail. Clicking only inspects.</summary>
public partial class CreatedCopySeal : Button
{
    public required CardTile CardAnchor { get; init; }
    public override void _Ready()
    {
        Flat = true; TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = CardAnchor.Card.Definition?.Name + "\n" + CreatedCopyStatus.Description(CardAnchor.Card);
        SetMeta("flat_tooltip", TooltipText); AccessibilityName = TooltipText;
        MouseFilter = CardAnchor.UprightPreview ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        FocusMode = CardAnchor.UprightPreview ? FocusModeEnum.None : FocusModeEnum.All;
        Pressed += () => CardAnchor.EmitSignal(BaseButton.SignalName.Pressed);
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        PositionAtCard();
    }
    public override void _Process(double delta) => PositionAtCard();
    private void PositionAtCard()
    {
        var inverse = CardAnchor.GetGlobalTransform().AffineInverse();
        var corners = CardAnchor.FaceCornersInCanvas().Select(p => inverse * p).ToArray();
        var diameter = CardAnchor.ProjectToScreen is null ? Math.Clamp(CardAnchor.FaceHeight * .17f, 22, 48) : Math.Max(24, CardAnchor.FaceHeight * .20f);
        var extent = Vector2.One * diameter;
        if (CardAnchor.ProjectToScreen is { } project)
        {
            var transform = CardAnchor.GetGlobalTransform();
            var corner = new Vector2(corners.Min(p => p.X), corners.Max(p => p.Y));
            var origin = transform * corner;
            var minimum = 22 * GetTree().Root.GetVisibleRect().Size.Y / 1080f;
            extent.X = Math.Max(extent.X, minimum / Math.Max(.01f, project(transform * (corner + Vector2.Right)).DistanceTo(project(origin))));
            extent.Y = Math.Max(extent.Y, minimum / Math.Max(.01f, project(transform * (corner + Vector2.Down)).DistanceTo(project(origin))));
        }
        var left = corners.Min(p => p.X);
        if (CardAnchor.TokenRail is { } rail)
        {
            var body = rail.TotalBody;
            left = Math.Min(left, new[] { body.Position, body.End, new Vector2(body.End.X, body.Position.Y), new Vector2(body.Position.X, body.End.Y) }
                .Select(p => (inverse * rail.GetGlobalTransform() * p).X).Min());
        }
        Size = extent;
        Position = new(left - extent.X * .96f, corners.Max(p => p.Y) - extent.Y);
        QueueRedraw();
    }
    public override bool _HasPoint(Vector2 point) => ((point - Size / 2) / Size).Length() <= .46f;
    public override Control _MakeCustomTooltip(string text) => CardAnchor.ProjectToScreen is null ? ReadingTooltip.Create(this, text) : null!;
    public override void _Draw()
    {
        DrawTextureRect(CreatedCopyStatus.Seal, new Rect2(Vector2.Zero, Size), false);
        if (HasFocus()) DrawArc(Size / 2, Size.X * .46f, 0, Mathf.Tau, 40, Palette.Gold, 1.5f, true);
    }
    public Vector2 CenterOnViewport()
    {
        var point = GetGlobalTransform() * (Size / 2);
        return CardAnchor.ProjectToScreen?.Invoke(point) ?? point;
    }
}
