using Godot;
using L5R.Core;
namespace L5R.Client;

public static class DeckBuilderDialog
{
    public static void Show(Node parent)
    {
        var root = parent.GetTree().Root; if (root.GetNodeOrNull("DeckBuilderLayer") is not null) return;
        var returnFocus = parent.GetViewport().GuiGetFocusOwner();
        var layer = new CanvasLayer { Name = "DeckBuilderLayer", Layer = 85 }; root.AddChild(layer);
        layer.TreeExited += () =>
        {
            if (GodotObject.IsInstanceValid(returnFocus) && returnFocus.IsInsideTree() && returnFocus.IsVisibleInTree())
                returnFocus.CallDeferred(Control.MethodName.GrabFocus);
        };
        try { var screen = new DeckBuilderScreen(); layer.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); screen.Closed += layer.QueueFree; }
        catch (Exception e) { layer.QueueFree(); GD.PushError(e.Message); }
    }
}

public partial class DeckBuilderScreen : Control
{
    public event Action? Closed;
    public Action? OpenLibrary;
    public CatalogueData Data { get; } = new();
    public CatalogueScans Scans { get; } = new();
    public DeckWorkshop Draft { get; private set; } = null!;
    public DeckFileStore Store { get; private set; } = null!;
    public string? StorageDirectory;
    public DeckSurface Mat { get; private set; } = null!;
    public DeckSurface Collection { get; private set; } = null!;
    public LineEdit Search { get; private set; } = null!;
    public bool Dirty { get; private set; }
    public string Page { get; private set; } = "decks";
    public string Family { get; private set; } = "Dynasty";
    private readonly Control _canvas = new(), _header = new(), _setupStrip = new(), _left = new(), _toolbar = new(), _setupTools = new(), _deckFooter = new(), _library = new(), _overlay = new(), _viewStrip = new();
    private readonly HashSet<string> _compare = [];
    private Dictionary<string, CatalogueData.Card> _byId = [];
    // The catalogue-wide DeckCardInfo materialization (each one running per-card ability lookups, DeckWorkshop.cs)
    // is expensive and the catalogue never changes during this screen's lifetime; built once here instead of
    // once per NewDraft() call -- BuildDeckList() (DeckBuilderScreen.Storage.cs) calls NewDraft() once per saved
    // deck file just to get a summary, so this was previously O(saved decks x catalogue size) on every visit.
    private DeckCardInfo[] _pool = [];
    private Button _dynasty = null!, _fate = null!, _compareButton = null!, _save = null!;
    private Label _shortages = null!;
    private Label _status = null!, _dirtyLabel = null!, _count = null!, _empty = null!;
    private LineEdit _name = null!;
    public CatalogueBrowserBar Browser { get; private set; } = null!;
    private OptionButton _sort = null!, _group = null!;
    private string _setupSlot = "", _setupClan = "Crane", _savedFingerprint = "", _savedName = "";
    private string? _currentPath;
    private float _w = 1672, _h = 941;
    private const float SidebarWidth = 620, MatLeft = 656;
    private bool _building, _list = true;
    private CardScanHover _scanHover = null!;
    private Button _viewAll = null!, _viewDynasty = null!, _viewAllFate = null!, _viewFate = null!;
    private bool _deckOnly;
    /// <summary>True while the collection pane lists only cards already in the current deck's active family.</summary>
    public bool DeckViewOnly => _deckOnly;
    public Control ViewStrip => _viewStrip;
    private bool _previousEmbed;
    private Control _lines = new() { MouseFilter = MouseFilterEnum.Ignore };
    private CatalogueData.Card[] _inspectionCards = [];
    public override void _Ready()
    {
        _previousEmbed = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        Theme = DeckSkin.Theme();
        MouseFilter = MouseFilterEnum.Stop;
        _byId = Data.Cards.ToDictionary(c => c.Id);
        _pool = Data.Cards.Select(c => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Definition?.Keywords?.ToArray() ?? c.Keywords, c.Playable, c.Definition?.CreatedOnly ?? false, c.Definition?.DeckCopyLimit, c.Definition is null ? 0 : DeckWorkshop.FateMinimumReductionOf(c.Definition), c.Definition is not null && DeckWorkshop.StrategyCopyRuleOf(c.Definition), c.Definition is not null && DeckWorkshop.BattleStrategyOf(c.Definition), c.Definition is null || DeckWorkshop.CountsTowardMinimumOf(c.Definition))).ToArray();
        Draft = NewDraft();
        Store = StorageDirectory is null ? PreloadedDecks.OpenStore() : new DeckFileStore(StorageDirectory);
        var bg = new TextureRect { Texture = GD.Load<Texture2D>("res://Art/DeckBuilder/work-mat.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }; AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_lines); _lines.Draw += DrawFrames;
        AddChild(_canvas); foreach (var c in new[] { _header, _setupStrip, _left, _toolbar, _setupTools, _deckFooter, _library, _viewStrip }) _canvas.AddChild(c);
        Browser = new CatalogueBrowserBar(Data, ["Dynasty", "Fate"], "Dynasty", NotSetupOnly); _canvas.AddChild(Browser); Search = Browser.Search; _sort = Browser.SortControl;
        Browser.EditCollection += () => { HideHover(); MyCollection.Show(this, Data, Scans, Browser); };
        Browser.Changed += ApplyFilters;
        Collection = new DeckSurface { Group = "None", ShowHeadings = false, MakeCard = MakeCollectionTile }; _canvas.AddChild(Collection);
        Mat = new DeckSurface { MakeCard = MakeTile }; _canvas.AddChild(Mat);
        var controlsTheme = CatalogueArt.Theme();
        foreach (var control in new Control[] { _header, _toolbar, _deckFooter, Collection, Mat }) control.Theme = controlsTheme;
        Collection.CardWidth = Mat.CardWidth = (float)Browser.CardSize.Value;
        Browser.PopupBounds = () => Collection.GetGlobalRect();
        Browser.CardSize.DragStarted += () => { Collection.BeginCardResize(); Mat.BeginCardResize(); }; Browser.CardSize.DragEnded += _ => { Collection.EndCardResize(); Mat.EndCardResize(); };
        Browser.CardSizeChanged += width => { Collection.CardWidth = width; Mat.CardWidth = width; HideHover(); Collection.Reposition(); Mat.Reposition(); };
        _status = CatalogueArt.Label("", 17); _canvas.AddChild(_status); _count = CatalogueArt.Label("", 17); _canvas.AddChild(_count);
        _empty = CatalogueArt.Label("", 24); _empty.HorizontalAlignment = HorizontalAlignment.Center; _canvas.AddChild(_empty);
        BuildHeader(); BuildCollectionControls(); BuildToolbar(); BuildViewStrip();
        _canvas.AddChild(_overlay); _overlay.Visible = false;
        _scanHover = new CardScanHover(Scans); AddChild(_scanHover); _scanHover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += Fit; TreeExiting += Scans.Close; TreeExiting += () => GetViewport().GuiEmbedSubwindows = _previousEmbed; Fit(); ShowDecks();
    }
    private DeckWorkshop NewDraft() => new(_pool);
    private static void Put(Control parent, Control child, float x, float y, float w, float h)
    { parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private static void Place(Control child, float x, float y, float w, float h) { child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private static OptionButton Option(string[] values, Action changed)
    { var o = new OptionButton { FitToLongestItem = false }; foreach (var v in values) o.AddItem(v); o.ItemSelected += _ => changed(); return o; }
    private void BuildHeader()
    {
        Put(_header, CatalogueArt.Button("‹ My decks", () => Guard(ShowDecks)), 24, 14, 140, 42);
        Put(_header, CatalogueArt.Label("Deck builder", 43, true), 198, 12, 355, 48);
        _name = new LineEdit { PlaceholderText = "Name your deck", MaxLength = 80, Theme = CatalogueArt.Theme() }; Put(_header, _name, 575, 16, 390, 42);
        _name.TextChanged += _ => { if (!_building) { Dirty = true; UpdateStatus(); } };
        _dirtyLabel = CatalogueArt.Label("", 16); _header.AddChild(_dirtyLabel);
        _save = CatalogueArt.Button("Save draft", () => SaveDraft()); _header.AddChild(_save);
        DeckSkin.Primary(_save);
        var copy = new MenuButton { Name = "Copy", Text = "", Icon = DeckSkin.Icon("more"), IconAlignment = HorizontalAlignment.Center, Flat = false, TooltipText = "More deck actions", AccessibilityName = "More deck actions" }; copy.AddThemeFontSizeOverride("font_size", 25); _header.AddChild(copy);
        var menu = copy.GetPopup(); menu.AddItem("Save a copy…", 0); menu.AddItem("Export draft…", 1); menu.IdPressed += id => { if (id == 0) SaveCopy(); else ExportFile(Draft.ToDeck(), _name.Text); };
        var library = CatalogueArt.Button("Card Library", () => Guard(() =>
        {
            if (OpenLibrary is not null) OpenLibrary();
            else
            {
                var root = GetTree().Root;
                TreeExited += () => Callable.From(() => LibraryDialog.Show(root)).CallDeferred();
                Closed?.Invoke();
            }
        })); library.Name = "OpenLibrary"; _header.AddChild(library);
        var brand = CatalogueArt.Label("LEGEND OF THE FIVE RINGS\nEMPEROR EDITION", 14); brand.Name = "Brand"; brand.HorizontalAlignment = HorizontalAlignment.Right; brand.AddThemeColorOverride("font_color", DeckSkin.Gold); _header.AddChild(brand);
    }
    private void BuildCollectionControls()
    {
        Browser.Visible = Page == "editor" && _setupSlot.Length == 0;
        _left.Visible = _setupSlot.Length > 0;
    }
    private void BuildToolbar()
    {
        _group = Option(["Group: Type", "Group: Gold", "Group: None"], () => RefreshMat());
        Put(_deckFooter, _group, 8, 2, 202, 34);
        var mat = CatalogueArt.Button("Mat", () => { _list = false; RefreshMat(); }); mat.Name = "MatTab"; Put(_deckFooter, mat, 220, 2, 100, 34);
        var list = CatalogueArt.Button("List", () => { _list = true; RefreshMat(); }); list.Name = "ListTab"; Put(_deckFooter, list, 328, 2, 100, 34);
        _compareButton = CatalogueArt.Button("Compare (0)", () => ShowComparison(_compare.Select(id => _byId[id]).ToArray())); Put(_deckFooter, _compareButton, 224, 42, 204, 34);
        _shortages = CatalogueArt.Label("", 15); Put(_toolbar, _shortages, 8, 5, 252, 27);
        Put(_toolbar, CatalogueArt.Button("View missing cards", ShowMissing), 264, 2, 164, 34);
        Put(_deckFooter, CatalogueArt.Button("Export missing", ExportMissing), 8, 42, 206, 34);
    }
    private void Fit()
    {
        var scale = Math.Min(1f, Math.Min(Size.X / 1440f, Size.Y / 800f)); if (scale <= 0) return;
        _canvas.Scale = Vector2.One * scale; _w = Size.X / scale; _h = Size.Y / scale; _canvas.Size = new Vector2(_w, _h);
        Place(_header, 0, 0, _w, 76);
        Place(_name, 560, 16, Math.Clamp(_w - 1130, 220, 390), 42);
        Place(_dirtyLabel, _w - 548, 22, 150, 30); Place(_save, _w - 388, 15, 172, 42); Place(_header.GetNode<Control>("Copy"), _w - 204, 15, 38, 42);
        _header.GetNode<Control>("Brand").Hide(); Place(_header.GetNode<Control>("OpenLibrary"), _w - 154, 15, 136, 42);
        Place(Browser, 28, 78, _w - 56, Browser.BarHeight);
        var top = 78 + Browser.BarHeight + 18;
        if (_setupSlot.Length == 0)
        {
            var right = _w - 470;
            Place(_viewStrip, 20, top, right - 40, 38); Place(Collection, 20, top + 42, right - 40, _h - top - 94);
            Place(_setupStrip, right, top, 442, 143);
            Place(_toolbar, right, top + 143, 442, 40);
            Place(_deckFooter, right, _h - 132, 442, 82);
            Place(Mat, right, top + 187, 442, Math.Max(60, _h - top - 325)); Mat.RowHeight = 56;
            Place(_count, 28, _h - 38, right - 48, 26);
            Place(_empty, right + 16, top + 220, 408, 100);
            Place(_status, right + 8, _h - 46, 422, 34); _status.AddThemeFontSizeOverride("font_size", 15);
        }
        else
        {
            Place(_setupStrip, 14, 78, _w - 28, 76); Place(_left, 16, 158, SidebarWidth, _h - 210);
            Place(_setupTools, MatLeft, 158, _w - MatLeft - 18, 58);
            Place(Mat, MatLeft, 216, _w - MatLeft - 18, _h - 269);
            Place(_empty, MatLeft + 30, 360, _w - MatLeft - 64, 100);
            Place(_count, 24, _h - 40, SidebarWidth, 25); Place(_status, MatLeft + 16, _h - 43, _w - MatLeft - 40, 28);
        }
        Place(_library, 0, 0, _w, _h); _overlay.Size = _canvas.Size;
        if (IsInsideTree()) { BuildSetupStrip(); if (Page == "decks") BuildDeckList(); else if (_setupSlot.Length > 0) BuildSetupChoices(); }
        Collection.Reposition(); Mat.Reposition(); _lines.QueueRedraw();
    }
    private void DrawFrames()
    {
        var s = _canvas.Scale.X;
        if (Page == "decks") return;
        var rect = _setupSlot.Length == 0 ? new Rect2(_setupStrip.Position, new Vector2(_setupStrip.Size.X, _h - _setupStrip.Position.Y - 52)) : new Rect2(13, 155, SidebarWidth + 12, _h - 205);
        DeckSkin.SharedFrame.Draw(_lines.GetCanvasItem(), new Rect2(rect.Position * s, rect.Size * s));
        if (_setupSlot.Length > 0) DeckSkin.SharedFrame.Draw(_lines.GetCanvasItem(), new Rect2((MatLeft - 10) * s, 155 * s, (_w - MatLeft - 4) * s, (_h - 205) * s));
    }
    private void BuildSetupStrip()
    {
        Palette.Clear(_setupStrip); if (Draft is null) return;
        var stronghold = Draft.Cards.FirstOrDefault(c => c.SetupSlot == "Stronghold"); var clan = stronghold is null ? _setupClan : _byId[stronghold.Id].Clans.FirstOrDefault() ?? "Unaligned";
        var compact = _setupSlot.Length == 0;
        if (compact)
        {
            Put(_setupStrip, CatalogueArt.Clan(clan), 10, 8, 34, 34); Put(_setupStrip, CatalogueArt.Label("Your deck", 27, true), 55, 6, 355, 38);
            var setupX = 10f;
            foreach (var slot in DeckWorkshop.SetupSlots)
            {
                var info = Draft.Cards.FirstOrDefault(c => c.SetupSlot == slot); var card = info is null ? null : _byId[info.Id];
                var label = slot == "Bamboo Harvesters" ? "Bamboo" : slot;
                var button = CatalogueArt.Button(label, () => SetSetup(slot)); button.AccessibilityName = "Choose " + slot;
                button.ClipText = true; button.AddThemeFontSizeOverride("font_size", 14); button.TooltipText = slot + ": " + (card?.Name ?? "Select…");
                Put(_setupStrip, button, setupX, 48, 134, 32); setupX += 140;
            }
            _dynasty = CatalogueArt.Button("", () => SetFamily("Dynasty")); _fate = CatalogueArt.Button("", () => SetFamily("Fate"));
            Put(_setupStrip, _dynasty, 10, 90, 204, 46); Put(_setupStrip, _fate, 224, 90, 204, 46);
            _dynasty.AddThemeFontSizeOverride("font_size", 16); _fate.AddThemeFontSizeOverride("font_size", 16);
        }
        else
        {
            Put(_setupStrip, CatalogueArt.Clan(clan), 9, 8, 58, 58); var x = 78f;
            foreach (var slot in DeckWorkshop.SetupSlots)
            {
                var info = Draft.Cards.FirstOrDefault(c => c.SetupSlot == slot); var card = info is null ? null : _byId[info.Id];
                if (card is not null) Put(_setupStrip, new DeckThumbnail(card, Scans), x, 9, 45, 55);
                var button = CatalogueArt.Button(slot + "\n" + (card?.Name ?? "Select…"), () => SetSetup(slot)); button.Alignment = HorizontalAlignment.Left; button.ClipText = true; button.AddThemeFontSizeOverride("font_size", 16); Put(_setupStrip, button, x + 53, 6, 163, 61); x += 221;
            }
            _dynasty = CatalogueArt.Button("", () => SetFamily("Dynasty")); _fate = CatalogueArt.Button("", () => SetFamily("Fate"));
            Put(_setupStrip, _dynasty, _w - 480, 7, 220, 62); Put(_setupStrip, _fate, _w - 252, 7, 220, 62);
        }
        UpdateStatus();
    }
    private void BuildViewStrip()
    {
        _viewStrip.Name = "CollectionViewStrip"; _viewStrip.Theme = CatalogueArt.Theme();
        // All Dynasty Cards | Dynasty Deck | All Fate Cards | Fate Deck (issue #792): "All" browses every card of that family, "Deck" only the cards in this deck.
        _viewAll = CatalogueArt.Button("", () => { _deckOnly = false; SetFamily("Dynasty"); }); _viewAll.Name = "ViewAllDynastyCards";
        _viewDynasty = CatalogueArt.Button("", () => { _deckOnly = true; SetFamily("Dynasty"); }); _viewDynasty.Name = "ViewDynastyDeck";
        _viewAllFate = CatalogueArt.Button("", () => { _deckOnly = false; SetFamily("Fate"); }); _viewAllFate.Name = "ViewAllFateCards";
        _viewFate = CatalogueArt.Button("", () => { _deckOnly = true; SetFamily("Fate"); }); _viewFate.Name = "ViewFateDeck";
        foreach (var b in new[] { _viewAll, _viewDynasty, _viewAllFate, _viewFate }) { b.ToggleMode = true; b.ClipText = true; b.AddThemeFontSizeOverride("font_size", 17); b.FocusMode = FocusModeEnum.All; _viewStrip.AddChild(b); }
        _viewAll.TooltipText = "Browse every Dynasty card that matches the filters."; _viewDynasty.TooltipText = "Show only the Dynasty cards in this deck.";
        _viewAllFate.TooltipText = "Browse every Fate card that matches the filters."; _viewFate.TooltipText = "Show only the Fate cards in this deck.";
        _viewStrip.Resized += () => { var w = _viewStrip.Size.X; var each = Math.Min(300f, (w - 24) / 4); var x = 0f; foreach (var b in new[] { _viewAll, _viewDynasty, _viewAllFate, _viewFate }) { Place(b, x, 0, each, 36); x += each + 8; } };
    }
    private void UpdateViewStrip(DeckReview r)
    {
        if (_viewAll is null) return;
        var all = Browser.Results.Length;
        bool allDynasty = !_deckOnly && Family == "Dynasty", dynastyDeck = _deckOnly && Family == "Dynasty", allFate = !_deckOnly && Family == "Fate", fateDeck = _deckOnly && Family == "Fate";
        _viewAll.Text = allDynasty ? $"All Dynasty Cards ({all:N0} shown)" : "All Dynasty Cards"; _viewDynasty.Text = $"Dynasty Deck ({r.Dynasty} cards)";
        _viewAllFate.Text = allFate ? $"All Fate Cards ({all:N0} shown)" : "All Fate Cards"; _viewFate.Text = $"Fate Deck ({r.Fate} cards)";
        _viewAll.SetPressedNoSignal(allDynasty); _viewDynasty.SetPressedNoSignal(dynastyDeck); _viewAllFate.SetPressedNoSignal(allFate); _viewFate.SetPressedNoSignal(fateDeck);
        CatalogueArt.Selected(_viewAll, allDynasty); CatalogueArt.Selected(_viewDynasty, dynastyDeck); CatalogueArt.Selected(_viewAllFate, allFate); CatalogueArt.Selected(_viewFate, fateDeck);
    }
    public void SetFamily(string family) { Family = family; Browser.SetFamily(family); SetSetup(""); ApplyFilters(); RefreshMat(true); }
    public void SetSetup(string slot)
    {
        _setupSlot = slot; HideHover(); Browser.CloseMenus(); Palette.Clear(_left); BuildCollectionControls();
        _toolbar.Visible = slot.Length == 0; _deckFooter.Visible = slot.Length == 0; _setupTools.Visible = slot.Length > 0; Collection.Visible = slot.Length == 0; _viewStrip.Visible = slot.Length == 0 && Page == "editor";
        if (slot.Length > 0) BuildSetupChoices(); else { ApplyFilters(); RefreshMat(true); }
        Fit(); UpdateStatus();
    }
    private void BuildSetupChoices()
    {
        Palette.Clear(_left);
        Put(_left, CatalogueArt.Button("Collection", () => SetSetup("")), 12, 6, 290, 40); var setupTab = CatalogueArt.Button("Setup", () => { }); DeckSkin.Selected(setupTab, true); Put(_left, setupTab, 310, 6, 290, 40);
        var clans = Option(new[] { "Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Unaligned" }, () => { });
        clans.Select(Array.IndexOf(new[] { "Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Unaligned" }, _setupClan));
        clans.ItemSelected += i => { _setupClan = clans.GetItemText((int)i); BuildSetupChoices(); }; Put(_left, clans, 12, 62, 588, 42);
        var y = 124f; foreach (var slot in DeckWorkshop.SetupSlots)
        {
            var info = Draft.Cards.FirstOrDefault(c => c.SetupSlot == slot); var b = CatalogueArt.Button(slot + "\n" + (info?.Name ?? "Select a card"), () => SetSetup(slot)); b.Alignment = HorizontalAlignment.Left;
            var style = DeckSkin.Plate(slot == _setupSlot ? 1 : 0); style.ContentMarginLeft = 88; style.ContentMarginRight = 26; b.AddThemeStyleboxOverride("normal", style);
            foreach (var state in new[] { "hover", "pressed" }) { var h = DeckSkin.Plate(1); h.ContentMarginLeft = 88; h.ContentMarginRight = 26; b.AddThemeStyleboxOverride(state, h); }
            b.AccessibilityName = "Choose " + slot; b.AddThemeFontSizeOverride("font_size", 18); b.ClipText = true; Put(_left, b, 12, y, 588, 84);
            if (info is not null) Put(b, new DeckThumbnail(_byId[info.Id], Scans), 8, 8, 68, 68);
            var mark = new TextureRect { Texture = DeckSkin.Icon(info is null ? "radio" : "selected"), MouseFilter = MouseFilterEnum.Ignore }; Put(b, mark, 555, 32, 20, 20); y += 96;
        }
        Put(_left, Palette.Wrapped("Choose one of each.\nKept outside both decks.\n\nBorder Keep starts in play.\nBamboo Harvesters starts bowed for the second player only.", 18, CatalogueArt.Ivory), 16, y + 20, 555, 225);
        Palette.Clear(_setupTools); _setupTools.Visible = true; _toolbar.Visible = false; Put(_setupTools, CatalogueArt.Label("Choose your " + _setupSlot, 32, true), 14, 6, _toolbar.Size.X - 190, 48);
        Put(_setupTools, CatalogueArt.Button("Back to deck", () => SetSetup("")), _toolbar.Size.X - 170, 8, 150, 38);
        var infos = Data.Cards.Where(c => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Keywords, c.Playable).SetupSlot == _setupSlot);
        if (_setupSlot == "Stronghold") infos = infos.Where(c => c.Clans.Contains(_setupClan) || (_setupClan == "Unaligned" && c.Clans.Length == 0));
        var cards = infos.OrderByDescending(c => c.Playable).ThenBy(c => c.Name).ToArray(); _count.Text = "Setup · kept outside deck totals"; Mat.Setup = true; Mat.Rows = false; Mat.Reset(cards, true); _empty.Visible = cards.Length == 0; _empty.Text = "No setup choices for this Clan.";
    }
    private static bool NotSetupOnly(CatalogueData.Card c) => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Keywords, c.Playable).SetupSlot.Length == 0;
    public void ApplyFilters()
    {
        if (_building || Page == "decks" || _setupSlot.Length > 0) return;
        var changedFamily = Family != Browser.Family; Family = Browser.Family;
        Browser.Refresh(false);
        // The deck views list the current deck's cards among the filtered results; a card removed to zero copies stays until the next refresh so rapid clicks never shift tiles under the pointer.
        var results = _deckOnly ? Browser.Results.Where(c => Draft.Count(c.Id) > 0).ToArray() : Browser.Results;
        Collection.Reset(results, changedFamily || !Collection.Cards.SequenceEqual(results));
        _count.Text = _deckOnly ? $"{results.Length:N0} {Family} deck cards shown · Click a full card to inspect" : $"{Collection.Cards.Length:N0} collection results · Click a full card to inspect";
        RefreshMat(changedFamily); UpdateStatus();
    }
    public void RefreshMat(bool reset = false)
    {
        if (Page == "decks" || _setupSlot.Length > 0) return;
        var ids = Draft.Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains(Family)).Select(c => c.Id).ToHashSet();
        var cards = Data.Filter(sort: _sort.Text).Where(c => ids.Contains(c.Id)).OrderByDescending(c => Draft.Count(c.Id) > MyCollection.Store.Count(c.Id)).ToArray();
        CatalogueArt.Selected(_deckFooter.GetNode<Button>("MatTab"), !_list); CatalogueArt.Selected(_deckFooter.GetNode<Button>("ListTab"), _list);
        Mat.Setup = false; Mat.Rows = _list; Mat.Group = _group.Selected switch { 1 => "Gold", 2 => "None", _ => "Type" }; Mat.Reset(cards, reset);
        _empty.Visible = cards.Length == 0; _empty.Text = "Your " + Family + " mat is empty.\nAdd cards from the collection to begin.";
    }
    public void Adjust(CatalogueData.Card card, int delta)
    { Draft.Adjust(card.Id, delta); Dirty = true; HideHover(); UpdateStatus(); Collection.Relayout(); RefreshMat(); }
    private void UpdateStatus()
    {
        if (_status is null || Draft is null) return; var r = Draft.Review();
        var missing = MissingCards(); if (_shortages is not null) { _shortages.Text = missing.Length == 0 ? "Collection: no missing copies" : $"{missing.Sum(c => c.Need)} copies missing · {missing.Length} {(missing.Length == 1 ? "card" : "cards")}"; _shortages.AddThemeColorOverride("font_color", missing.Length == 0 ? CatalogueArt.Muted : CatalogueArt.Gold); }
        if (_dynasty is not null)
        {
            _dynasty.Text = $"DYNASTY  {r.Dynasty}\n40 minimum"; _fate.Text = $"FATE  {r.Fate}\n40 minimum";
            DeckSkin.Selected(_dynasty, Family == "Dynasty"); if (Family == "Dynasty") { var brass = DeckSkin.Plate(2); brass.ModulateColor = new Color(.58f, .5f, .38f, 1); _dynasty.AddThemeStyleboxOverride("normal", brass); }
            _fate.AddThemeStyleboxOverride("normal", DeckSkin.Plate(3));
            _fate.AddThemeColorOverride("font_color", Family == "Fate" ? new Color("ffebae") : DeckSkin.Text);
        }
        UpdateViewStrip(r);
        _status.Text = r.Warnings.Length > 0 ? $"{r.Warnings.Length} deck checks to review  ·  Draft can still be saved" : "Basic checks passed  ·  Draft can be saved";
        _status.TooltipText = string.Join("\n", r.Warnings); _status.MouseFilter = MouseFilterEnum.Stop;
        _dirtyLabel.Text = Dirty ? "Unsaved changes" : "Saved locally"; _compareButton.Text = $"Compare ({_compare.Count})"; _compareButton.Disabled = _compare.Count == 0;
    }
    public override void _Input(InputEvent e)
    {
        if (GetChildren().OfType<MyCollectionEditor>().Any()) return;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _overlay.Visible)
        { CloseOverlay(); GetViewport().SetInputAsHandled(); }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (GetChildren().OfType<MyCollectionEditor>().Any()) return;
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.F11) return;
            if (key.Keycode == Key.Escape) { if (_overlay.Visible) CloseOverlay(); else if (Page == "decks") Closed?.Invoke(); else Guard(ShowDecks); }
            else if ((key.CtrlPressed || key.MetaPressed) && key.Keycode == Key.S && Page != "decks") SaveDraft();
            else if ((key.CtrlPressed || key.MetaPressed) && key.Keycode == Key.F && Page != "decks" && _setupSlot.Length == 0) Search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton) GetViewport().SetInputAsHandled();
    }
}
