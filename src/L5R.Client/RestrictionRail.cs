using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>The outer track of the approved shared rail. Every sentence is supplied by Core.</summary>
public partial class RestrictionRail : Button
{
    public required CardTokenRail Rail { get; init; }
    public Rect2 Body { get; private set; }
    private static Texture2D? _sprites, _hardware;
    public static bool HasEffects(CardView card) => card.FaceUp && card.Definition is not null && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Stronghold or Zone.Region && card.Restrictions is { Length: > 0 };
    public static string Describe(CardView card) => string.Join("\n\n", (card.Restrictions ?? []).Select(r => r.Label + "\n" + r.Duration + (r.SourceName is null ? "" : "\nSource: " + r.SourceName)));
    public override void _Ready()
    {
        Flat = true; MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.All;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = Describe(Rail.CardAnchor.Card); SetMeta("flat_tooltip", TooltipText); AccessibilityName = TooltipText;
        Pressed += () => Rail.CardAnchor.EmitSignal(BaseButton.SignalName.Pressed);
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public void Arrange(Rect2 tokenBody, bool horizontal)
    {
        Body = horizontal ? new Rect2(tokenBody.Position + new Vector2(0, tokenBody.Size.Y + 2), tokenBody.Size) : new Rect2(tokenBody.Position - new Vector2(tokenBody.Size.X + 2, 0), tokenBody.Size);
        Size = Body.Size;
        QueueRedraw();
    }
    public override bool _HasPoint(Vector2 point) => Body.HasPoint(point);
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
    public override void _Draw()
    {
        _sprites ??= GD.Load<Texture2D>("res://Art/Restrictions/restriction-sprites.png");
        _hardware ??= GD.Load<Texture2D>("res://Art/card-token-hardware.png");
        var horizontal = Body.Size.X > Body.Size.Y;
        if (horizontal)
        {
            DrawSetTransform(new Vector2(Body.Position.X, Body.End.Y), -Mathf.Pi / 2);
            DrawTextureRectRegion(_hardware, new Rect2(0, 0, Body.Size.Y, Body.Size.X), new Rect2(147, 60, 211, 1414));
            DrawSetTransformMatrix(Transform2D.Identity);
        }
        else DrawTextureRectRegion(_hardware, Body, new Rect2(147, 60, 211, 1414));
        var groups = Rail.CardAnchor.Card.Restrictions!.GroupBy(r => (r.Kind, r.Protective)).ToArray();
        var thickness = Math.Min(Body.Size.X, Body.Size.Y);
        var cell = Math.Min(thickness * 1.2f, Math.Max(Body.Size.X, Body.Size.Y) / Math.Max(1, groups.Length));
        var diameter = Math.Min(thickness * .82f, cell * .82f);
        for (var i = 0; i < groups.Length; i++)
        {
            var center = horizontal ? new Vector2(Body.Position.X + cell * (i + .5f), Body.GetCenter().Y) : new Vector2(Body.GetCenter().X, Body.Position.Y + cell * (i + .5f));
            var index = groups[i].Key.Protective ? 2 : groups[i].Key.Kind == "straighten" ? 1 : 0;
            var source = index switch { 0 => new Rect2(143, 115, 468, 480), 1 => new Rect2(842, 117, 472, 478), _ => new Rect2(1550, 118, 468, 476) };
            if (!groups[i].Key.Protective && groups[i].Key.Kind is not ("move" or "straighten"))
                DrawTextureRect(ActionLockStatus.Seal, new Rect2(center - Vector2.One * diameter / 2, Vector2.One * diameter), false);
            else DrawTextureRectRegion(_sprites, new Rect2(center - Vector2.One * diameter / 2, Vector2.One * diameter), source);
        }
        if (IsHovered() || HasFocus()) DrawRect(Body, Palette.Gold, false, 1);
    }
}
