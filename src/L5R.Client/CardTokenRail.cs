using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>
/// #1314 (Mendon): a card's public markers as separate icons outside the card, with no rail hardware behind them: the permanent-change
/// seal, one icon per restriction/status kind and one per token group. They stack along the card's left edge (its near edge when bowed)
/// up to the card's height and start another column outward when full. Each icon explains itself on hover. Only presents public state.
/// </summary>
public partial class CardTokenRail : Control
{
    public const float Width = 36, Gap = 2;
    public const int PerColumn = 5; // (198 + Gap) / (Width + Gap): five icons fit the face height
    public required CardTile CardAnchor { get; init; }
    public float Offset { get; set; }
    public CardTile[] Companions { get; set; } = [];
    /// <summary>Every column, in this control's space.</summary>
    public Rect2 Body { get; private set; }
    public Rect2 TotalBody => Body;
    public static int IconCount(CardView card) => (CardChanges.HasSeal(card) ? 1 : 0) + RestrictionRail.Groups(card).Length + Groups(card).Length;
    public static int Tracks(CardView card) => Math.Max(1, (IconCount(card) + PerColumn - 1) / PerColumn);
    public static float WidthFor(CardView card) => Width * Tracks(card) + (Tracks(card) - 1) * Gap;
    public PermanentGrantSeal? GrantSeal { get; private set; }
    public CardIcon[] Icons { get; private set; } = [];
    public bool IconHovered => GrantSeal?.IsHovered() == true || Icons.Any(i => i.IsHovered());
    public static bool HasRail(CardView card) => HasTokens(card) || CardChanges.HasSeal(card) || RestrictionRail.HasEffects(card);
    public record Group(string Type, string? Stat, int Amount, CardToken[] Instances);
    public static bool HasTokens(CardView card) => card.FaceUp && card.Definition is not null && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Stronghold && card.Tokens is { Length: > 0 };
    public static Group[] Groups(CardView card) => !HasTokens(card) ? [] : card.Tokens!
        .GroupBy(t => (t.Type, t.Stat, t.Amount)).Select(g => new Group(g.Key.Type, g.Key.Stat, g.Key.Amount, g.ToArray()))
        .OrderBy(g => g.Type, StringComparer.Ordinal).ThenBy(g => g.Stat, StringComparer.Ordinal).ThenBy(g => g.Amount).ToArray();
    private bool _horizontal;
    internal static readonly string[] Types = ["Fire", "Poison", "Sake", "Revenge", "Obsession", "Plague", "Frenzy", "Training", "Blood", "Wealth", "Madness", "Charging", "Arrow", "Favor", "Master"];
    public static string Delta(string? stat, int amount) => string.IsNullOrWhiteSpace(stat) ? "" : $"{amount:+0;-0;0}{stat.ToLowerInvariant() switch { "force" => "F", "chi" => "C", "personalhonor" => "PH", "goldproduction" => " Gold production", "goldcost" => " Gold cost", _ => " " + stat }}";
    public CardTokenRail() { MouseFilter = MouseFilterEnum.Ignore; FocusMode = FocusModeEnum.None; }
    public override void _Ready()
    {
        var card = CardAnchor.Card;
        if (CardChanges.HasSeal(card)) { GrantSeal = new PermanentGrantSeal { CardAnchor = CardAnchor }; AddChild(GrantSeal); }
        var name = card.Definition?.Name ?? "Card";
        var icons = new List<CardIcon>();
        foreach (var group in RestrictionRail.Groups(card))
            icons.Add(new CardIcon { CardAnchor = CardAnchor, Restriction = group.Key, Explanation = name + "\n" + RestrictionRail.Describe(group) + "\n\nClick to select this card." });
        foreach (var group in Groups(card))
        {
            var lines = new List<string> { name, $"{group.Type} token ×{group.Instances.Length}" + (Delta(group.Stat, group.Amount) is { Length: > 0 } d ? $" · {d} per token" : "") };
            foreach (var source in group.Instances.GroupBy(t => t.Source))
                lines.Add($"  {CardAnchor.TokenSourceName?.Invoke(source.Key) ?? "Card effect"} ×{source.Count()}");
            lines.Add("\nToken quantities are separate from current card statistics.\nClick to select this card.");
            icons.Add(new CardIcon { CardAnchor = CardAnchor, Token = group, Explanation = string.Join("\n", lines) });
        }
        Icons = [.. icons];
        foreach (var icon in Icons) AddChild(icon);
        PositionAtCard();
    }
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
        var unit = Math.Max(12, CardAnchor.FaceHeight) / 198f;
        var width = Width * unit;
        Rotation = direction.Angle() - (_horizontal ? 0 : Mathf.Pi / 2);
        Position = edge[0] + new Vector2(-direction.Y, direction.X) * (width * .5f + Offset * 1.01f + .06f);
        // A separately bowed attachment cannot put its near-edge icons through the
        // Personality. Collect them below the complete folded stack, in its
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
        // Column 0 runs along the card edge from its top (bowed: its left end); further columns step outward, left when upright, down when bowed.
        var total = WidthFor(CardAnchor.Card) * unit;
        var length = edge[0].DistanceTo(edge[1]);
        Body = _horizontal ? new Rect2(0, -width * .5f, length, total) : new Rect2(width * .5f - total, 0, total, length);
        Size = Body.End;
        var step = width + Gap * unit;
        var slots = (GrantSeal is null ? Array.Empty<Control>() : [GrantSeal]).Concat(Icons).ToArray();
        for (var i = 0; i < slots.Length; i++)
        {
            int column = i / PerColumn, row = i % PerColumn;
            slots[i].Position = _horizontal ? new Vector2(row * step, -width * .5f + column * step) : new Vector2(-width * .5f - column * step, row * step);
            slots[i].Size = slots[i] == GrantSeal ? new Vector2(width, width * .97f) : new Vector2(width, width);
            slots[i].QueueRedraw();
        }
    }
    /// <summary>Every icon lies inside the stack's area: none runs past the card's height (or its length when bowed).</summary>
    public bool IconsWithinCardHeight() => (GrantSeal is null ? Array.Empty<Control>() : [GrantSeal]).Concat(Icons)
        .All(c => Body.Grow(.5f).Encloses(new Rect2(c.Position, c.Size)));
    public Vector2[] CornersOnViewport() => new[] { TotalBody.Position, new Vector2(TotalBody.End.X, TotalBody.Position.Y), TotalBody.End, new Vector2(TotalBody.Position.X, TotalBody.End.Y) }
        .Select(p => GetGlobalTransform() * p).Select(p => CardAnchor.ProjectToScreen?.Invoke(p) ?? p).ToArray();
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

/// <summary>One restriction kind or token group beside its card (#1314). Hover explains it; a click selects the card.</summary>
public partial class CardIcon : Button
{
    public required CardTile CardAnchor { get; init; }
    public (string Kind, bool Protective)? Restriction { get; init; }
    public CardTokenRail.Group? Token { get; init; }
    public required string Explanation { get; init; }
    private static Texture2D? _tokens;
    public override void _Ready()
    {
        Flat = true; TextureFilter = TextureFilterEnum.LinearWithMipmaps; MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = Explanation; SetMeta("flat_tooltip", Explanation); AccessibilityName = Explanation;
        Pressed += () => CardAnchor.EmitSignal(BaseButton.SignalName.Pressed);
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override bool _HasPoint(Vector2 point) => ((point - Size / 2) / Size).Length() <= .5f;
    public override Control _MakeCustomTooltip(string text) => CardAnchor.ProjectToScreen is null ? ReadingTooltip.Create(this, text) : null!;
    public override void _Draw()
    {
        var d = Math.Min(Size.X, Size.Y);
        var center = Size / 2;
        if (Restriction is { } key) RestrictionRail.DrawIcon(this, key, new Rect2(center - Vector2.One * d * .48f, Vector2.One * d * .96f));
        if (Token is { } group)
        {
            _tokens ??= GD.Load<Texture2D>("res://Art/card-token-source.png");
            CardTokenRail.DrawIcon(this, _tokens, group.Type, center, d * .92f);
            if (!CardTokenRail.Types.Contains(group.Type, StringComparer.OrdinalIgnoreCase))
            {
                var label = CardTokenRail.Delta(group.Stat, group.Amount); if (label.Length == 0) label = "?";
                var fs = Math.Max(4, (int)(d * .34f));
                while (fs > 3 && Palette.Body.GetStringSize(label, fontSize: fs).X > d * .75f) fs--;
                DrawString(Palette.Body, new Vector2(center.X - d * .42f, center.Y + fs * .3f), label, HorizontalAlignment.Center, d * .84f, fs, Palette.Ink);
            }
            if (group.Instances.Length > 1)
            {
                // The quantity sits on the medallion's lower right, outlined to read over any art.
                var count = $"×{group.Instances.Length}";
                var size = Math.Max(4, (int)(d * .38f));
                var point = new Vector2(d * .45f, d * 1.02f);
                DrawStringOutline(Palette.Body, point, count, HorizontalAlignment.Center, d * .6f, size, 3, new Color("111517"));
                DrawString(Palette.Body, point, count, HorizontalAlignment.Center, d * .6f, size, new Color("e5ded0"));
            }
        }
        if (IsHovered() || HasFocus()) DrawArc(center, d * .5f, 0, Mathf.Tau, 40, Palette.Gold, 1.2f, true);
    }
    public Vector2 CenterOnViewport()
    {
        var point = GetGlobalTransform() * (Size / 2);
        return CardAnchor.ProjectToScreen?.Invoke(point) ?? point;
    }
}
