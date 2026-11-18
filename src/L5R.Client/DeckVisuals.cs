using Godot;
namespace L5R.Client;

/// <summary>Full catalogue face, with physical copy edges. Art is never distorted or replaced.</summary>
public partial class DeckFace : Button
{
    public CatalogueData.Card Card { get; }
    private readonly CatalogueScans _scans;
    public int Copies = 1;
    public bool Selected;
    private Texture2D? _drawn; // the scan the last _Draw recorded; the canvas holds only its RID (#1281)
    public DeckFace(CatalogueData.Card card, CatalogueScans scans)
    {
        Card = card; _scans = scans; Flat = true; AccessibilityName = card.Name; MouseDefaultCursorShape = CursorShape.PointingHand;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(s, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        scans.Changed += QueueRedraw; TreeExiting += () => scans.Changed -= QueueRedraw;
    }
    public override void _Draw()
    {
        var face = new Rect2(3, 2, Size.X - 6, Size.Y - 10);
        for (var i = Math.Min(Copies - 1, 5); i > 0; i--) { DrawRect(new Rect2(face.Position + new Vector2(i * .5f, i * 1.3f), face.Size), new Color("b7a47c")); DrawRect(new Rect2(face.Position + new Vector2(i * .5f, i * 1.3f), face.Size), new Color("24201b"), false, 1); }
        if (IsHovered() || HasFocus() || Selected)
        {
            var glow = new StyleBoxFlat { BgColor = Colors.Transparent, ShadowColor = new Color(.95f, .64f, .2f, .5f), ShadowSize = 12, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 }; glow.Draw(GetCanvasItem(), face.Grow(3));
        }
        DrawRect(face.Grow(2), new Color("8a7045")); DrawRect(face, new Color("070b0e"));
        var tex = _drawn = _scans.Get(Card.Printings.FirstOrDefault());
        if (tex is not null) { var k = Math.Min(face.Size.X / tex.GetWidth(), face.Size.Y / tex.GetHeight()); var size = tex.GetSize() * k; DrawTextureRect(tex, new Rect2(face.Position + (face.Size - size) / 2, size), false); }
        else { DrawString(Palette.Body, new Vector2(8, face.Size.Y * .45f), Card.Name, HorizontalAlignment.Center, Size.X - 16, 16, CatalogueArt.Ivory); DrawString(Palette.Body, new Vector2(8, face.Size.Y * .55f), "Loading scan…", HorizontalAlignment.Center, Size.X - 16, 14, CatalogueArt.Muted); }
        DeckSkin.SharedFrame.Draw(GetCanvasItem(), face.Grow(2));
        if (IsHovered() || HasFocus() || Selected) DrawRect(face.Grow(2), new Color("e4ba66"), false, 1);
    }
}

public partial class DeckThumbnail : Control
{
    private readonly CatalogueData.Card _card;
    private readonly CatalogueScans _scans;
    private Texture2D? _drawn; // held for the recorded draw (#1281)
    public DeckThumbnail(CatalogueData.Card card, CatalogueScans scans)
    { _card = card; _scans = scans; MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.LinearWithMipmaps; scans.Changed += QueueRedraw; TreeExiting += () => scans.Changed -= QueueRedraw; }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("171b1d"));
        var tex = _drawn = _scans.Get(_card.Printings.FirstOrDefault());
        if (tex is not null)
        {
            var crop = new Rect2(tex.GetWidth() * .1f, tex.GetHeight() * .15f, tex.GetWidth() * .8f, tex.GetHeight() * .43f);
            var ratio = (Size.X - 2) / (Size.Y - 2); var fitted = crop.Size;
            if (fitted.X / fitted.Y > ratio) fitted.X = fitted.Y * ratio; else fitted.Y = fitted.X / ratio;
            DrawTextureRectRegion(tex, new Rect2(1, 1, Size.X - 2, Size.Y - 2), new Rect2(crop.Position + (crop.Size - fitted) / 2, fitted));
        }
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("8a7045"), false, 1);
    }
}

/// <summary>Virtualized collection rows and grouped draft tiles share the same bounded layout.</summary>
public partial class DeckSurface : ScrollContainer
{
    // Key is the card itself for a card entry (the same Filter()/Sort() pass in DeckBuilderScreen always
    // returns the SAME Card instances, never new ones -- it only ever selects from the one stable catalogue
    // array) or "heading:count" for a header, so an unchanged header (same text, same shown-count) is reused
    // too, and a changed one gets fresh fields rather than stale ones.
    private sealed record Entry(CatalogueData.Card? Card, string Heading, Rect2 Rect, int Count = 0)
    {
        public object Key => Card is not null ? Card : (object)$"heading:{Heading}:{Count}";
    }
    private readonly Control _sheet = new();
    private List<Entry> _entries = [];
    private Entry? _resizeAnchor;
    private float _resizeOffset;
    public void BeginCardResize() { _resizeAnchor = _entries.FirstOrDefault(e => e.Card is not null && e.Rect.End.Y > ScrollVertical); _resizeOffset = _resizeAnchor is null ? 0 : ScrollVertical - _resizeAnchor.Rect.Position.Y; }
    public void EndCardResize() => _resizeAnchor = null;
    private readonly Dictionary<object, Control> _visible = [];
    public CatalogueData.Card[] Cards = [];
    public bool Rows, Setup;
    public bool ShowHeadings = true;
    public float CardWidth = 196, RowHeight = 60;
    public string Group = "Type";
    public Func<CatalogueData.Card, Control> MakeCard = null!;
    public int VisibleCount => _visible.Count;
    public DeckSurface()
    {
        HorizontalScrollMode = ScrollMode.Disabled; VerticalScrollMode = ScrollMode.Auto; ClipContents = true; AddChild(_sheet);
        Resized += Reposition; GetVScrollBar().ValueChanged += _ => Refresh();
    }
    public void Reset(CatalogueData.Card[] cards, bool resetScroll = false)
    { Cards = cards; if (resetScroll) ScrollVertical = 0; Relayout(); }
    // Full rebuild: every visible tile is freed and recreated. Required whenever Cards itself changed, or a
    // persisting card's DISPLAYED state could have (Adjust()'s copy count, the compare overlay's selection,
    // an over-limit color) -- MakeTile/MakeRow (DeckBuilderScreen.Cards.cs) bake that state into the Control
    // once at creation time, never live-binding it, so reusing a tile would show it stale.
    public void Relayout() => Layout(rebuild: true);
    // A plain resize (the DeckSurface's own Resized signal, and DeckBuilderScreen.Fit() placing it): the card
    // list, its order/grouping and every tile's displayed state are all unchanged, only column width/positions
    // move -- reuse the existing tiles (keyed by card identity) and just reposition them instead of destroying
    // and recreating every one of them, which is what made a window resize the dominant allocation cost here.
    public void Reposition() => Layout(rebuild: false);
    private void Layout(bool rebuild)
    {
        if (Size.X < 40) return;
        if (rebuild) { foreach (var c in _visible.Values) { _sheet.RemoveChild(c); c.QueueFree(); } _visible.Clear(); }
        var anchor = !rebuild ? _resizeAnchor ?? _entries.FirstOrDefault(e => e.Card is not null && e.Rect.End.Y > ScrollVertical) : null;
        var offset = _resizeAnchor is not null ? _resizeOffset : anchor is null ? 0 : ScrollVertical - anchor.Rect.Position.Y;
        var entries = new List<Entry>();
        float y = 4, w = Size.X - 18;
        if (Rows)
        {
            foreach (var card in Cards) { entries.Add(new(card, "", new Rect2(2, y, w - 4, RowHeight - 2))); y += RowHeight; }
        }
        else
        {
            var width = Setup ? Math.Min(300, (w - 60) / 3) : CardWidth;
            var columns = Setup ? Math.Max(1, Math.Min(3, (int)(w / (width + 22)))) : Math.Max(1, (int)(w / (width + 22))); var cell = w / columns;
            var height = width * 1.4f + (Setup ? 90 : 142);
            string Key(CatalogueData.Card c) => Setup ? "" : Group == "Gold" ? "GOLD " + (c.Numeric("cost") == double.MaxValue ? "—" : c.Numeric("cost").ToString()) : Group == "None" ? "CARDS" : string.Join(" / ", c.Types.Select(t => t == "Personality" ? "Personalities" : t == "Strategy" ? "Strategies" : t + "s")).ToUpperInvariant();
            foreach (var group in Cards.GroupBy(Key))
            {
                if (ShowHeadings && group.Key.Length > 0) { entries.Add(new(null, group.Key, new Rect2(8, y, w - 16, 30), group.Count())); y += 34; }
                var i = 0; foreach (var card in group) { entries.Add(new(card, "", new Rect2(i % columns * cell + (cell - width) / 2, y + i / columns * height, width, height - 10))); i++; }
                y += (float)Math.Ceiling((double)i / columns) * height + 12;
            }
        }
        _entries = entries;
        _sheet.CustomMinimumSize = new Vector2(0, Math.Max(Size.Y, y)); _sheet.Size = new Vector2(w, Math.Max(Size.Y, y));
        if (anchor is not null && entries.FirstOrDefault(e => ReferenceEquals(e.Card, anchor.Card)) is { } moved) ScrollVertical = (int)Math.Max(0, moved.Rect.Position.Y + offset);
        Refresh();
    }
    private void Refresh()
    {
        if (!IsInsideTree()) return;
        var visible = _entries.Where(e => e.Rect.End.Y >= ScrollVertical - 50 && e.Rect.Position.Y < ScrollVertical + Size.Y + 50).ToDictionary(e => e.Key);
        foreach (var key in _visible.Keys.Where(k => !visible.ContainsKey(k)).ToArray()) { _sheet.RemoveChild(_visible[key]); _visible[key].QueueFree(); _visible.Remove(key); }
        foreach (var (key, entry) in visible)
        {
            if (!_visible.TryGetValue(key, out var item))
            { item = entry.Card is null ? new DeckSectionTitle(entry.Heading, entry.Count) : MakeCard(entry.Card); _sheet.AddChild(item); _visible[key] = item; }
            item.Position = entry.Rect.Position; item.Size = entry.Rect.Size;
        }
    }
}

public partial class DeckSectionTitle(string title, int count) : Control
{
    public override void _Draw()
    {
        var font = CatalogueArt.Heading(23); var width = font.GetStringSize(title, fontSize: 23).X;
        DrawString(font, new Vector2(0, 22), title, HorizontalAlignment.Left, -1, 23, DeckSkin.Text);
        DrawString(Palette.Body, new Vector2(width + 16, 22), $"({count} shown)", HorizontalAlignment.Left, -1, 17, new Color("c8bb99"));
        DrawLine(new Vector2(0, 29), new Vector2(Size.X, 29), new Color(.6f, .44f, .22f, .6f), 1);
    }
}
