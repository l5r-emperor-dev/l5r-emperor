using Godot;
namespace L5R.Client;

/// <summary>Virtual rows keep 2,149 cards responsive without instantiating the entire collection.</summary>
public partial class CatalogueGrid : ScrollContainer
{
    private readonly Control _sheet = new();
    private readonly Dictionary<int, CatalogueCardTile> _tiles = [];
    private CatalogueData.Card[] _cards = [];
    private readonly CatalogueScans _scans;
    private int _columns = 6;
    private float _cellWidth = 240, _rowHeight = 360, _cardWidth = 222;
    private string? _selected;
    private int? _resizeAnchor;
    private float _resizeOffset;
    public void BeginCardResize()
    {
        var row = (int)(ScrollVertical / _rowHeight);
        _resizeAnchor = row * _columns; _resizeOffset = ScrollVertical - row * _rowHeight;
    }
    public void EndCardResize() => _resizeAnchor = null;
    public Action<CatalogueData.Card>? Selected;
    /// <summary>Optional full-size scan preview shown while a card face is hovered or focused.</summary>
    public CardScanHover? Hover { get; set; }
    public float CardWidth { get; set; } = 230;
    public int VisibleTileCount => _tiles.Count;
    public CatalogueData.Card[] Cards => _cards;
    public CatalogueGrid(CatalogueScans scans)
    {
        _scans = scans; FocusMode = FocusModeEnum.All; ClipContents = true; HorizontalScrollMode = ScrollMode.Disabled; VerticalScrollMode = ScrollMode.Auto;
        AddChild(_sheet); Resized += LayoutCards; GetVScrollBar().ValueChanged += _ => { Hover?.Dismiss(); Refresh(); };
        _scans.Changed += RedrawCards; MyCollection.Store.Changed += RedrawCards;
        TreeExiting += () => { _scans.Changed -= RedrawCards; MyCollection.Store.Changed -= RedrawCards; };
    }
    public void SetCards(CatalogueData.Card[] cards, bool reset = true)
    { _cards = cards; foreach (var tile in _tiles.Values) { _sheet.RemoveChild(tile); tile.QueueFree(); } _tiles.Clear(); if (reset) ScrollVertical = 0; LayoutCards(); }
    public void Select(string? id) { _selected = id; RedrawCards(); }
    public void LayoutCards()
    {
        if (Size.X < 10) return;
        var anchor = _resizeAnchor ?? (_columns > 0 ? (int)(ScrollVertical / _rowHeight) * _columns : 0);
        var offset = _resizeAnchor is not null ? _resizeOffset : ScrollVertical - (int)(ScrollVertical / _rowHeight) * _rowHeight;
        var desired = CardWidth + 16;
        _columns = Math.Max(1, (int)((Size.X - 12) / desired));
        _cellWidth = (Size.X - 18) / _columns;
        _cardWidth = Math.Min(_cellWidth - 16, CardWidth);
        _rowHeight = _cardWidth * 1.4f + 57;
        _sheet.CustomMinimumSize = new Vector2(0, (float)Math.Max(Size.Y, Math.Ceiling((double)_cards.Length / _columns) * _rowHeight));
        _sheet.Size = new Vector2(Size.X - 18, _sheet.CustomMinimumSize.Y);
        ScrollVertical = (int)(anchor / _columns * _rowHeight + offset); Refresh();
    }
    private void Refresh()
    {
        if (!IsInsideTree() || Size.Y < 10) return;
        var first = Math.Max(0, ((int)(ScrollVertical / _rowHeight) - 1) * _columns);
        var end = Math.Min(_cards.Length, ((int)((ScrollVertical + Size.Y) / _rowHeight) + 2) * _columns);
        foreach (var key in _tiles.Keys.Where(i => i < first || i >= end).ToArray()) { _sheet.RemoveChild(_tiles[key]); _tiles[key].QueueFree(); _tiles.Remove(key); }
        for (var i = first; i < end; i++)
        {
            if (!_tiles.TryGetValue(i, out var tile))
            {
                var card = _cards[i]; tile = new CatalogueCardTile(card, _scans); tile.Pressed += () => { Hover?.Dismiss(); Selected?.Invoke(card); }; tile.Step = direction => EditNeighbour(card.Id, direction); Hover?.Track(tile, () => card); _sheet.AddChild(tile); _tiles[i] = tile;
            }
            tile.Position = new Vector2(i % _columns * _cellWidth + (_cellWidth - _cardWidth) / 2, i / _columns * _rowHeight + 7);
            tile.Size = new Vector2(_cardWidth, _rowHeight - 12); tile.Chosen = tile.Card.Id == _selected; tile.QueueRedraw();
        }
    }
    private void RedrawCards() { foreach (var tile in _tiles.Values) { tile.Chosen = tile.Card.Id == _selected; tile.QueueRedraw(); } }
    public void EnsureCard(string id)
    {
        var index = Array.FindIndex(_cards, c => c.Id == id); if (index < 0) return;
        var top = index / _columns * _rowHeight; var bottom = top + _rowHeight;
        if (top < ScrollVertical) ScrollVertical = (int)top;
        else if (bottom > ScrollVertical + Size.Y) ScrollVertical = (int)(bottom - Size.Y);
        Refresh();
    }
    /// <summary>Opens the Owned editor of the card after (+1) or before (-1) this one in reading order, scrolling it into view; the end of the list stops.</summary>
    public void EditNeighbour(string id, int direction)
    {
        var index = Array.FindIndex(_cards, c => c.Id == id) + direction;
        if (index < 0 || index >= _cards.Length) return;
        EnsureCard(_cards[index].Id); Tile(_cards[index].Id)?.BeginEdit();
    }
    public CatalogueCardTile? Tile(string id) => _tiles.Values.FirstOrDefault(t => t.Card.Id == id);
}
public partial class CatalogueCardTile : Button
{
    public CatalogueData.Card Card { get; }
    public bool Chosen { get; set; }
    private readonly CatalogueScans _scans;
    public CatalogueCardTile(CatalogueData.Card card, CatalogueScans scans)
    {
        Card = card; _scans = scans; AccessibilityName = card.Name; AccessibilityDescription = card.Playable ? "Playable game version. Select to inspect." : "Reference card, not yet playable. Select to inspect."; Flat = true; Text = "";
        MouseDefaultCursorShape = CursorShape.PointingHand; TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        // The Owned numeral is its own small button, so a click on it edits the count instead of selecting the card.
        _numeral = new Button { Name = "OwnedNumeral", Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.Ibeam, TooltipText = "Click to change how many you own.", AccessibilityName = "Edit owned copies of " + card.Name };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) _numeral.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _numeral.MouseEntered += QueueRedraw; _numeral.MouseExited += QueueRedraw; _numeral.Pressed += BeginEdit; AddChild(_numeral);
        TreeExiting += () => { if (_editor is not null) Accept(); };
    }
    private readonly Button _numeral;
    private Control? _editor;
    private LineEdit? _field;
    /// <summary>True while the Owned count is being typed.</summary>
    public bool Editing => _editor is not null;
    private Rect2 NumeralRect()
    {
        var font = Palette.Body; var count = MyCollection.Store.Count(Card.Id).ToString();
        var prefix = font.GetStringSize("Owned: ", HorizontalAlignment.Left, -1, 16).X; var number = font.GetStringSize(count, HorizontalAlignment.Left, -1, 16).X;
        return new Rect2((Size.X - prefix - number) / 2 + prefix - 4, Size.X * 1.4f + 3, Math.Max(number, 10) + 8, 22);
    }
    /// <summary>Asked to move editing to the next (+1) or previous (-1) card; the grid knows the order.</summary>
    public Action<int>? Step;
    public void BeginEdit()
    {
        if (_editor is not null) return;
        var row = new HBoxContainer { Name = "OwnedEditor", MouseFilter = MouseFilterEnum.Stop }; row.AddThemeConstantOverride("separation", 3);
        var field = new LineEdit { Name = "OwnedField", Text = MyCollection.Store.Count(Card.Id).ToString(), Alignment = HorizontalAlignment.Center, MaxLength = 5, CustomMinimumSize = new Vector2(62, 26), AccessibilityName = "Owned copies of " + Card.Name };
        field.AddThemeFontSizeOverride("font_size", 16); field.AddThemeConstantOverride("minimum_character_width", 1);
        field.AddThemeStyleboxOverride("normal", CatalogueArt.ControlStyle(new Color("0a1014"), new Color("8c754b"), 3, 0));
        field.AddThemeStyleboxOverride("focus", CatalogueArt.ControlStyle(new Color("16212a"), CatalogueArt.Gold, 3, 0));
        field.TextChanged += text => { var digits = new string(text.Where(char.IsAsciiDigit).ToArray()); if (digits != text) { field.Text = digits; field.CaretColumn = digits.Length; } };
        field.TextSubmitted += _ => Accept();
        field.GuiInput += e =>
        {
            if (e is not InputEventKey { Pressed: true } key) return;
            if (key.Keycode == Key.Escape) { field.GetViewport().SetInputAsHandled(); Revert(); }
            else if (key.Keycode == Key.Tab) { field.GetViewport().SetInputAsHandled(); var step = Step; var direction = key.ShiftPressed ? -1 : 1; Accept(); step?.Invoke(direction); }
        };
        var yes = new OwnedGlyph(true) { Name = "OwnedAccept", TooltipText = "Keep this number (Enter).", AccessibilityName = "Accept owned copies" }; yes.Pressed += Accept;
        var no = new OwnedGlyph(false) { Name = "OwnedRevert", TooltipText = "Put the old number back (Esc).", AccessibilityName = "Revert owned copies" }; no.Pressed += Revert;
        row.AddChild(field); row.AddChild(yes); row.AddChild(no);
        var width = row.GetCombinedMinimumSize().X;
        row.Position = new Vector2(Math.Max(0, (Size.X - width) / 2 + (Size.X >= 190 ? 26 : 0)), Size.X * 1.4f + 2); row.Size = new Vector2(width, 26);
        _editor = row; _field = field; _numeral.Visible = false; AddChild(row); QueueRedraw();
        field.GrabFocus(); field.SelectAll();
    }
    private void CloseEdit()
    {
        _editor?.QueueFree(); _editor = null; _field = null; _numeral.Visible = true; QueueRedraw();
    }
    /// <summary>Keeps the typed number: an empty field counts as no change.</summary>
    private void Accept()
    {
        if (_editor is null || _field is null) return;
        var ok = int.TryParse(_field.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value);
        CloseEdit();
        if (!ok) return;
        try { MyCollection.Store.Set(Card.Id, value); }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or InvalidOperationException) { GD.PushWarning("Owned count not saved: " + e.Message); }
    }
    private void Revert() => CloseEdit();
    public override void _Input(InputEvent @event)
    {
        // A click anywhere outside the field and its buttons keeps the number (the click itself still does what it was aimed at).
        if (_editor is not null && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && !_editor.GetGlobalRect().HasPoint(click.GlobalPosition)) Accept();
    }
    public override void _Draw()
    {
        var face = new Rect2(0, 0, Size.X, Size.X * 1.4f);
        var hot = IsHovered() || HasFocus() || Chosen;
        if (hot) for (var i = 8; i > 0; i--) DrawRect(face.Grow(i), new Color(.91f, .66f, .29f, (Chosen ? .10f : .065f) * (1 - i / 9f)), false, 2);
        DrawRect(face.Grow(2), new Color("7b6034")); DrawRect(face.Grow(1), new Color("12100c"));
        var tex = _scans.Get(Card.Printings.FirstOrDefault());
        if (tex is not null)
        {
            var scale = Math.Min(face.Size.X / tex.GetWidth(), face.Size.Y / tex.GetHeight()); var size = tex.GetSize() * scale;
            DrawTextureRect(tex, new Rect2(face.Position + (face.Size - size) / 2, size), false);
        }
        else
        {
            DrawStyleBox(CatalogueArt.Frame(12), face);
            DrawString(Palette.Body, new Vector2(12, face.Size.Y * .43f), Card.Name, HorizontalAlignment.Center, face.Size.X - 24, 18, CatalogueArt.Ivory);
            DrawString(Palette.Body, new Vector2(12, face.Size.Y * .54f), string.Join(" · ", Card.Types), HorizontalAlignment.Center, face.Size.X - 24, 16, CatalogueArt.Muted);
            DrawString(Palette.Body, new Vector2(8, face.Size.Y - 24), _scans.State(Card.Printings.FirstOrDefault()).Split(" · ")[0], HorizontalAlignment.Center, face.Size.X - 16, 13, CatalogueArt.Muted);
        }
        if (hot) DrawRect(face.Grow(2), CatalogueArt.Gold, false, 2);
        if (_editor is null)
        {
            var numeral = NumeralRect(); _numeral.Position = numeral.Position; _numeral.Size = numeral.Size;
            var number = MyCollection.Store.Count(Card.Id).ToString(); var lit = _numeral.IsHovered();
            DrawString(Palette.Body, new Vector2(numeral.Position.X + 4 - Palette.Body.GetStringSize("Owned: ", HorizontalAlignment.Left, -1, 16).X, face.Size.Y + 19), "Owned: ", HorizontalAlignment.Left, -1, 16, CatalogueArt.Ivory);
            DrawString(Palette.Body, new Vector2(numeral.Position.X + 4, face.Size.Y + 19), number, HorizontalAlignment.Left, -1, 16, lit ? CatalogueArt.Gold : CatalogueArt.Ivory);
            if (lit) DrawLine(new Vector2(numeral.Position.X + 4, face.Size.Y + 22), new Vector2(numeral.End.X - 4, face.Size.Y + 22), CatalogueArt.Gold, 1);
        }
        else if (Size.X >= 190) DrawString(Palette.Body, new Vector2(_editor.Position.X - 56, face.Size.Y + 19), "Owned:", HorizontalAlignment.Left, -1, 16, CatalogueArt.Ivory);
        if (!Card.Playable) DrawString(Palette.Body, new Vector2(0, face.Size.Y + 40), "Not yet playable", HorizontalAlignment.Center, Size.X, 14, CatalogueArt.Muted);
    }
}
/// <summary>The small check (accept) and x (revert) beside an Owned number being edited; drawn, so no font needs the glyphs.</summary>
public partial class OwnedGlyph : Button
{
    private readonly bool _accept;
    public OwnedGlyph(bool accept)
    {
        _accept = accept; Flat = true; FocusMode = FocusModeEnum.None; CustomMinimumSize = new Vector2(24, 26); MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var color = _accept ? new Color("5fbf6a") : new Color("d9534f"); var box = new Rect2(Vector2.Zero, Size);
        DrawRect(box, new Color(color, IsHovered() ? .30f : .14f)); DrawRect(box, color, false, 1);
        var c = Size / 2;
        if (_accept) DrawPolyline([c + new Vector2(-6, 0), c + new Vector2(-2, 5), c + new Vector2(6, -5)], color, 2.4f, true);
        else { DrawLine(c + new Vector2(-5, -5), c + new Vector2(5, 5), color, 2.4f, true); DrawLine(c + new Vector2(-5, 5), c + new Vector2(5, -5), color, 2.4f, true); }
    }
}
