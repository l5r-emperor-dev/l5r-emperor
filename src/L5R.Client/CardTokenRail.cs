using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Only presents public token instances. It never applies their effects.</summary>
public partial class CardTokenRail : Button
{
    public const float Width = 36;
    public required CardTile CardAnchor { get; init; }
    public float Offset { get; set; }
    public CardTile[] Companions { get; set; } = [];
    public Rect2 Body { get; private set; }
    public RestrictionRail? Restrictions { get; private set; }
    public Rect2 TotalBody => Restrictions is null ? Body : Body.Merge(Restrictions.Body);
    // #1279 (Mendon): the token track appears only when the card carries tokens. A status alone takes the inner
    // position by itself; a permanent-change seal alone keeps one track's room with no empty hardware behind it.
    public static int Tracks(CardView card) => Math.Max(1, (HasTokens(card) ? 1 : 0) + (RestrictionRail.HasEffects(card) ? 1 : 0));
    public static float WidthFor(CardView card) => Width * Tracks(card) + (Tracks(card) - 1) * 2;
    public bool TokenTrackVisible => HasTokens(CardAnchor.Card);
    public PermanentGrantSeal? GrantSeal { get; private set; }
    public static bool HasRail(CardView card) => HasTokens(card) || CardChanges.HasSeal(card) || RestrictionRail.HasEffects(card);
    public float ContentScale { get; private set; } = 1;
    public record Group(string Type, string? Stat, int Amount, CardToken[] Instances);
    public static bool HasTokens(CardView card) => card.FaceUp && card.Definition is not null && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Stronghold && card.Tokens is { Length: > 0 };
    public static Group[] Groups(CardView card) => !HasTokens(card) ? [] : card.Tokens!
        .GroupBy(t => (t.Type, t.Stat, t.Amount)).Select(g => new Group(g.Key.Type, g.Key.Stat, g.Key.Amount, g.ToArray()))
        .OrderBy(g => g.Type, StringComparer.Ordinal).ThenBy(g => g.Stat, StringComparer.Ordinal).ThenBy(g => g.Amount).ToArray();
    private Group[] _groups = [];
    private bool _horizontal;
    private static Texture2D? _icons, _hardware;
    private static readonly string[] Types = ["Fire", "Poison", "Sake", "Revenge", "Obsession", "Plague", "Frenzy", "Training", "Blood", "Wealth", "Madness", "Charging", "Arrow", "Favor", "Master"];
    public static string Delta(string? stat, int amount) => string.IsNullOrWhiteSpace(stat) ? "" : $"{amount:+0;-0;0}{stat.ToLowerInvariant() switch { "force" => "F", "chi" => "C", "personalhonor" => "PH", "goldproduction" => " Gold production", "goldcost" => " Gold cost", _ => " " + stat }}";
    public CardTokenRail()
    {
        Flat = true; MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.All;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Ready()
    {
        _groups = Groups(CardAnchor.Card);
        if (RestrictionRail.HasEffects(CardAnchor.Card)) { Restrictions = new RestrictionRail { Rail = this }; AddChild(Restrictions); }
        if (CardChanges.HasSeal(CardAnchor.Card))
        {
            GrantSeal = new PermanentGrantSeal { CardAnchor = CardAnchor }; AddChild(GrantSeal);
        }
        Pressed += () => CardAnchor.EmitSignal(BaseButton.SignalName.Pressed);
        if (!TokenTrackVisible) FocusMode = FocusModeEnum.None; // nothing of its own to read; the status track and seal take focus
        var lines = new List<string> { CardAnchor.Card.Definition?.Name ?? "Card", "Tokens", "" };
        foreach (var group in _groups)
        {
            lines.Add($"{group.Type} ×{group.Instances.Length}" + (Delta(group.Stat, group.Amount) is { Length: > 0 } d ? $" · {d} per token" : ""));
            foreach (var source in group.Instances.GroupBy(t => t.Source))
                lines.Add($"  {CardAnchor.TokenSourceName?.Invoke(source.Key) ?? "Card effect"} ×{source.Count()}");
        }
        lines.Add("\nToken quantities are separate from current card statistics.\nClick to select this card.");
        TooltipText = string.Join("\n", lines); SetMeta("flat_tooltip", TooltipText);
        PositionAtCard();
    }
    public override Control _MakeCustomTooltip(string text) => CardAnchor.ProjectToScreen is not null ? null! : CreateReadingPreview();
    public Control CreateReadingPreview() => _groups.Length == 0 && GrantSeal is not null
        ? CardChangePanel.Tooltip(CardAnchor.Card, CardAnchor.TokenSourceName, GetTree().Root.GetVisibleRect().Size)
        : ReadingTooltip.Create(this, TooltipText.Length > 0 ? TooltipText : GetMeta("flat_tooltip").AsString());
    public override void _Process(double delta) => PositionAtCard();
    public void PositionAtCard()
    {
        if (!IsInstanceValid(CardAnchor)) return;
        Visible = HasRail(CardAnchor.Card);
        if (!Visible) return;
        var inverse = CardAnchor.GetGlobalTransform().AffineInverse();
        var corners = CardAnchor.FaceCornersInCanvas().Select(p => inverse * p).ToArray();
        // Follow the card through its bow animation. Upright: left edge; bowed: near edge.
        _horizontal = Math.Abs(Mathf.Sin(CardAnchor.VisualBowAngle)) > .72f;
        var edgeIndex = _horizontal
            ? Enumerable.Range(0, 4).MaxBy(i => (corners[i].Y + corners[(i + 1) % 4].Y) / 2)
            : Enumerable.Range(0, 4).MinBy(i => (corners[i].X + corners[(i + 1) % 4].X) / 2);
        var edge = new[] { corners[edgeIndex], corners[(edgeIndex + 1) % 4] }.OrderBy(p => _horizontal ? p.X : p.Y).ToArray();
        var direction = (edge[1] - edge[0]).Normalized();
        var width = Width * Math.Max(12, CardAnchor.FaceHeight) / 198f;
        Rotation = direction.Angle() - (_horizontal ? 0 : Mathf.Pi / 2);
        Position = edge[0] + new Vector2(-direction.Y, direction.X) * (width * .5f + Offset * 1.01f + .06f);
        // A separately bowed attachment cannot put its near-edge rail through the
        // Personality. Collect its rail below the complete folded stack, in its
        // own band; the tooltip/click still belongs to that attachment.
        if (_horizontal && Companions.Length > 0)
        {
            var peers = Companions.Where(IsInstanceValid).ToArray();
            var bottom = peers.SelectMany(c => c.FaceCornersInCanvas()).Select(p => inverse * p).Max(p => p.Y);
            var before = peers.Where(c => c.Card.Bowed && HasRail(c.Card))
                .OrderBy(c => c.Card.AttachedTo is null ? 0 : 1).ThenBy(c => c.Card.Id, StringComparer.Ordinal)
                .TakeWhile(c => c != CardAnchor).Sum(c => WidthFor(c.Card) * Math.Max(12, c.FaceHeight) / 198f + 2);
            Position = new Vector2(Position.X, bottom + width * .5f + before + 2);
        }
        if (!_horizontal && Companions.Length > 0)
        {
            var peers = Companions.Where(IsInstanceValid).ToArray();
            var left = peers.SelectMany(c => c.FaceCornersInCanvas()).Select(p => inverse * p).Min(p => p.X);
            var before = peers.Where(c => HasRail(c.Card) && !c.Card.Bowed)
                .OrderBy(c => c.Card.AttachedTo is null ? 0 : 1).ThenBy(c => c.Card.Id, StringComparer.Ordinal)
                .TakeWhile(c => c != CardAnchor).Sum(c => WidthFor(c.Card) * Math.Max(12, c.FaceHeight) / 198f + 2);
            Position = new Vector2(left - width * .5f - before - 2, Position.Y);
        }
        Body = _horizontal ? new Rect2(0, -width * .5f, edge[0].DistanceTo(edge[1]), width) : new Rect2(-width * .5f, 0, width, edge[0].DistanceTo(edge[1]));
        Restrictions?.Arrange(Body, _horizontal, inner: !TokenTrackVisible, head: GrantSeal is null ? 0 : (_horizontal ? Body.Size.Y : Body.Size.X) * 1.02f);
        Size = TotalBody.Size;
        if (GrantSeal is not null)
        {
            var thickness = _horizontal ? Body.Size.Y : Body.Size.X;
            GrantSeal.Position = Body.Position;
            GrantSeal.Size = new Vector2(thickness, thickness * .97f);
        }
        QueueRedraw();
    }
    public Vector2[] CornersOnViewport() => new[] { TotalBody.Position, new Vector2(TotalBody.End.X, TotalBody.Position.Y), TotalBody.End, new Vector2(TotalBody.Position.X, TotalBody.End.Y) }
        .Select(p => GetGlobalTransform() * p).Select(p => CardAnchor.ProjectToScreen?.Invoke(p) ?? p).ToArray();
    public override bool _HasPoint(Vector2 p) => TokenTrackVisible && Body.Grow(2).HasPoint(p);
    public override void _Draw()
    {
        if (!TokenTrackVisible) return;
        _hardware ??= GD.Load<Texture2D>("res://Art/card-token-hardware.png");
        _icons ??= GD.Load<Texture2D>("res://Art/card-token-source.png");
        // Sample only the opaque rail; the generator's checkerboard never enters the mesh.
        var rail = new Rect2(147, 60, 211, 1414);
        if (_horizontal)
        {
            DrawSetTransform(new Vector2(Body.Position.X, Body.End.Y), -Mathf.Pi / 2);
            DrawTextureRectRegion(_hardware, new Rect2(0, 0, Body.Size.Y, Body.Size.X), rail);
            DrawSetTransformMatrix(Transform2D.Identity);
        }
        else DrawTextureRectRegion(_hardware, Body, rail);
        var length = _horizontal ? Body.Size.X : Body.Size.Y;
        var thickness = _horizontal ? Body.Size.Y : Body.Size.X;
        var reserved = GrantSeal is null ? 0 : thickness * 1.02f;
        var available = length - thickness * .45f - reserved;
        var normalCell = thickness * (GrantSeal is null ? 1.18f : 1f);
        if (_groups.Length == 0) return;
        // One through four groups preserve the default design; five/six compress smoothly.
        var cell = Math.Min(normalCell, available / _groups.Length);
        ContentScale = Math.Min(1, cell / normalCell);
        var diameter = thickness * (_horizontal ? .54f : GrantSeal is null ? .76f : .68f) * ContentScale;
        var start = reserved + (_groups.Length <= 4 ? Math.Min(GrantSeal is null ? thickness * .75f : 0, Math.Max(thickness * .2f, length - cell * _groups.Length - thickness * .2f)) : thickness * .22f);
        for (var i = 0; i < _groups.Length; i++)
        {
            var group = _groups[i];
            var center = _horizontal ? new Vector2(Body.Position.X + start + cell * i + diameter * .5f, Body.GetCenter().Y)
                : new Vector2(Body.GetCenter().X, Body.Position.Y + start + cell * i + diameter * .5f);
            DrawIcon(this, _icons, group.Type, center, diameter);
            var count = $"×{group.Instances.Length}";
            var size = Math.Max(4, (int)(thickness * (_horizontal ? .34f : GrantSeal is null ? .44f : .32f) * ContentScale));
            while (size > 3 && Palette.Body.GetStringSize(count, fontSize: size).X > thickness * .92f) size--;
            var countWidth = _horizontal ? cell - diameter : thickness * .96f;
            while (size > 3 && Palette.Body.GetStringSize(count, fontSize: size).X > countWidth) size--;
            var point = _horizontal ? new Vector2(center.X + diameter * .5f, center.Y + size * .32f) : new Vector2(center.X - thickness * .48f, center.Y + diameter * .5f + size * .85f);
            DrawStringOutline(Palette.Body, point, count, HorizontalAlignment.Center, countWidth, size, 1, new Color("111517"));
            DrawString(Palette.Body, point, count, HorizontalAlignment.Center, countWidth, size, new Color("e5ded0"));
            if (!Types.Contains(group.Type, StringComparer.OrdinalIgnoreCase))
            {
                var label = Delta(group.Stat, group.Amount); if (label.Length == 0) label = "?";
                var fs = Math.Max(4, (int)(diameter * .38f));
                while (fs > 3 && Palette.Body.GetStringSize(label, fontSize: fs).X > diameter * .8f) fs--;
                DrawString(Palette.Body, new Vector2(center.X - diameter * .45f, center.Y + fs * .3f), label, HorizontalAlignment.Center, diameter * .9f, fs, Palette.Ink);
            }
        }
        if (IsHovered() || HasFocus()) DrawRect(Body.Grow(.5f), Palette.Gold with { A = .7f }, false, 1);
    }
    public static void DrawIcon(CanvasItem canvas, Texture2D art, string type, Vector2 center, float diameter)
    {
        var index = Array.FindIndex(Types, t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = 15;
        var source = new Vector2(208 + 280 * (index % 4), 157 + 221 * (index / 4));
        var points = Enumerable.Range(0, 64).Select(i => Vector2.FromAngle(i * Mathf.Tau / 64)).ToArray();
        canvas.DrawColoredPolygon(points.Select(p => center + p * diameter * .5f).ToArray(), Colors.White, points.Select(p => (source + p * 94) / art.GetSize()).ToArray(), art);
        if (index == 15) canvas.DrawTextureRectRegion(art, new Rect2(center - new Vector2(diameter * .32f, diameter * .22f), new Vector2(diameter * .64f, diameter * .44f)), new Rect2(1012, 868, 32, 8));
    }
}
