using Godot;
using L5R.Core;

namespace L5R.Client;

public static class MyCollection
{
    private static readonly Dictionary<string, MyCollectionStore> Stores = [];
    public static string? OverridePath;
    public static MyCollectionStore Store
    {
        get
        {
            var path = OverridePath ?? ProjectSettings.GlobalizePath("user://my-collection.json");
            if (!Stores.TryGetValue(path, out var store)) Stores[path] = store = new MyCollectionStore(path);
            return store;
        }
    }
    private static CollectionPrinting[]? _printings;
    /// <summary>The printings the collection file lists (Content/collection-printings.txt): set, rarity, clan and copy limit of each card, which the catalogue itself does not record.</summary>
    public static CollectionPrinting[] Printings => _printings ??= Load();
    private static CollectionPrinting[] Load()
    {
        using var file = Godot.FileAccess.Open("res://Content/collection-printings.txt", Godot.FileAccess.ModeFlags.Read);
        return file is null ? [] : CollectionPrintings.Parse(CollectionPrintings.Decode(file.GetBuffer((long)file.GetLength())));
    }
    public static CollectionIdentity[] Identities(CatalogueData data) => data.Cards.Select(c =>
        new CollectionIdentity(c.Id, Experienced.BaseTitle(c.Name), Experienced.DeckName(c.Name, c.Definition?.Keywords ?? c.Keywords))).ToArray();
    public static MyCollectionEditor Show(Control parent, CatalogueData data, CatalogueScans scans, CatalogueBrowserBar browser)
    {
        var existing = parent.GetChildren().OfType<MyCollectionEditor>().FirstOrDefault();
        if (existing is not null) return existing;
        browser.CloseMenus();
        var editor = new MyCollectionEditor(data, scans, browser) { Name = "MyCollectionEditor" };
        parent.AddChild(editor); editor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); return editor;
    }
}
/// <summary>Focused local inventory editing and review-before-apply CSV flow. Drafts never enter this editor.</summary>
public partial class MyCollectionEditor : Control
{
    private readonly CatalogueData _data;
    private readonly CatalogueScans _scans;
    private readonly CatalogueBrowserBar _browser;
    private readonly MyCollectionStore _store = MyCollection.Store;
    private readonly Control _canvas = new();
    private Panel _panel = null!;
    private DeckSurface _cards = null!;
    private LineEdit _search = null!;
    private OwnershipFilter _editOwnership;
    private Label _notice = null!;
    private Button _undo = null!;
    private CollectionImportPlan? _plan;
    private CollectionImportMode _mode;
    private string _filename = "";
    public CollectionImportPlan? ImportPlan => _plan;
    public MyCollectionEditor(CatalogueData data, CatalogueScans scans, CatalogueBrowserBar browser)
    { _data = data; _scans = scans; _browser = browser; }
    public override void _Ready()
    {
        Theme = CatalogueArt.Theme(); MouseFilter = MouseFilterEnum.Stop; ZIndex = 20;
        var shade = new ColorRect { Color = new Color(0, 0, 0, .7f) }; AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_canvas); _store.Changed += StoreChanged; TreeExiting += () => _store.Changed -= StoreChanged;
        Resized += Fit; BuildEditor(); Fit();
    }
    private static void Put(Control parent, Control child, float x, float y, float w, float h)
    { parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private void StartPanel(string title)
    {
        Palette.Clear(_canvas); _panel = new Panel(); _panel.AddThemeStyleboxOverride("panel", CatalogueArt.Frame(24)); _canvas.AddChild(_panel);
        Put(_panel, CatalogueArt.Label(title, 35, true), 30, 20, 670, 50);
    }
    private void Fit()
    {
        var dimensions = _plan is null ? new Vector2(1200, 710) : new Vector2(820, 840);
        var scale = Math.Min(1f, Math.Min(Size.X / (dimensions.X + 60), Size.Y / (dimensions.Y + 60)));
        _canvas.Scale = Vector2.One * scale;
        var position = (Size - dimensions * scale) / 2;
        position.Y = Math.Clamp(position.Y + (_plan is null ? 35 : 15) * scale, 0, Math.Max(0, Size.Y - dimensions.Y * scale - 12));
        _canvas.Position = position;
        if (_panel is not null) _panel.Size = dimensions;
    }
    public void Close() { _browser.EditCollectionButton.GrabFocus(); QueueFree(); }
    private void BuildEditor()
    {
        _plan = null; StartPanel("Edit my collection");
        var title = _panel.GetChildren().OfType<Label>().First(); title.HorizontalAlignment = HorizontalAlignment.Center; title.Size = new Vector2(1140, 50);
        var saved = CatalogueArt.Label("Changes save automatically on this device", 17); saved.HorizontalAlignment = HorizontalAlignment.Center;
        Put(_panel, saved, 30, 58, 1140, 28);
        Put(_panel, CatalogueArt.Button("Import CSV", ImportFile), 832, 25, 110, 42);
        Put(_panel, CatalogueArt.Button("Export CSV", () => SaveCsv(_store.Export(MyCollection.Identities(_data), MyCollection.Printings), "Collection.csv")), 954, 25, 110, 42);
        var done = CatalogueArt.Button("Done", Close); DeckSkin.Primary(done); Put(_panel, done, 1076, 25, 100, 42);
        _search = new LineEdit { PlaceholderText = "Search your current card selection…", ClearButtonEnabled = true };
        _search.Text = _browser.Search.Text; Put(_panel, _search, 30, 108, 800, 44);
        _editOwnership = OwnershipFilter.All;
        var ownershipButtons = new List<Button>();
        for (var i = 0; i < 3; i++)
        {
            var selection = (OwnershipFilter)i;
            var button = CatalogueArt.Button(new[] { "All cards", "Owned", "Unowned" }[i], () =>
            {
                _editOwnership = selection; FilterCards();
                for (var n = 0; n < ownershipButtons.Count; n++) CatalogueArt.Selected(ownershipButtons[n], n == (int)selection);
            });
            Put(_panel, button, 770 + i * 133, 108, 131, 44); ownershipButtons.Add(button); CatalogueArt.Selected(button, i == 0);
        }
        _search.Size = new Vector2(720, 44); _search.TextChanged += _ => FilterCards();
        _cards = new DeckSurface { Group = "None", ShowHeadings = false, MakeCard = EditTile, CardWidth = 254 };
        Put(_panel, _cards, 30, 173, 1160, 450);
        _notice = Palette.Wrapped("", 17, CatalogueArt.Gold); Put(_panel, _notice, 30, 642, 890, 64);
        var separator = new ColorRect { Color = new Color("6d5935"), MouseFilter = MouseFilterEnum.Ignore }; Put(_panel, separator, 30, 665, 1140, 1);
        _undo = CatalogueArt.Button("Undo last change", () => Try(_store.Undo)); Put(_panel, _undo, 486, 671, 228, 28);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) _undo.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        StoreChanged(); FilterCards(); Fit();
    }
    private void FilterCards()
    {
        // Match the caller's current facets but do not trap inventory editing behind its ownership filter.
        var cards = CatalogueData.Search(_browser.CardsWithoutOwnership.Where(c => _store.Includes(c.Id, _editOwnership)), _search.Text).ToArray();
        if (!_cards.Cards.SequenceEqual(cards)) _cards.Reset(cards, true);
        if (cards.Length == 0) _notice.Text = "No cards in this selection. Close the editor and adjust your browsing filters.";
    }
    private Control EditTile(CatalogueData.Card card)
    {
        var tile = new Control(); var face = new DeckFace(card, _scans); tile.AddChild(face); face.Pressed += () => Inspect(card);
        var label = CatalogueArt.Label(_store.Count(card.Id) > 0 ? "Owned" : "Not owned", 18); label.HorizontalAlignment = HorizontalAlignment.Center; label.SetMeta("owned_label_id", card.Id); tile.AddChild(label);
        var count = new LineEdit { Name = "OwnedQuantity", Text = _store.Count(card.Id).ToString(), Alignment = HorizontalAlignment.Center, MaxLength = 5, AccessibilityName = "Owned copies of " + card.Name };
        count.AddThemeConstantOverride("minimum_character_width", 1);
        count.AddThemeFontSizeOverride("font_size", 22);
        count.AddThemeStyleboxOverride("normal", CatalogueArt.ControlStyle(new Color("0a1014"), new Color("8c754b"), 4, 0));
        count.AddThemeStyleboxOverride("focus", CatalogueArt.ControlStyle(new Color("16212a"), CatalogueArt.Gold, 4, 0));
        count.SetMeta("card_id", card.Id); tile.AddChild(count);
        void Commit()
        {
            if (!int.TryParse(count.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
            { Try(() => throw new FormatException("Owned quantity must be a whole number.")); count.Text = _store.Count(card.Id).ToString(); return; }
            if (!Try(() => _store.Set(card.Id, value))) count.Text = _store.Count(card.Id).ToString();
        }
        count.TextSubmitted += _ => Commit(); count.FocusExited += Commit;
        var minus = CatalogueArt.Button("−", () => Try(() => _store.Set(card.Id, Math.Max(0, _store.Count(card.Id) - 1)))); minus.AccessibilityName = "Reduce owned copies of " + card.Name; minus.Disabled = _store.Count(card.Id) == 0; minus.SetMeta("owned_minus_id", card.Id); tile.AddChild(minus);
        var plus = CatalogueArt.Button("+", () => Try(() => _store.Set(card.Id, Math.Min(MyCollectionStore.MaximumCount, _store.Count(card.Id) + 1)))); plus.AccessibilityName = "Increase owned copies of " + card.Name; tile.AddChild(plus);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var left = (StyleBoxFlat)Theme.GetStylebox(state, "Button").Duplicate(); left.CornerRadiusTopRight = left.CornerRadiusBottomRight = 0; minus.AddThemeStyleboxOverride(state, left);
            var right = (StyleBoxFlat)Theme.GetStylebox(state, "Button").Duplicate(); right.CornerRadiusTopLeft = right.CornerRadiusBottomLeft = 0; plus.AddThemeStyleboxOverride(state, right);
        }
        tile.Resized += () =>
        {
            var w = tile.Size.X; face.Position = Vector2.Zero; face.Size = new Vector2(w, w * 1.4f);
            label.Position = new Vector2(0, w * 1.4f + 4); label.Size = new Vector2(w, 26);
            count.Position = new Vector2(w / 2 - 38, w * 1.4f + 34); count.Size = new Vector2(76, 44);
            minus.Position = new Vector2(w / 2 - 78, w * 1.4f + 34); minus.Size = new Vector2(40, 44);
            plus.Position = new Vector2(w / 2 + 38, w * 1.4f + 34); plus.Size = new Vector2(40, 44);
        };
        return tile;
    }
    private void StoreChanged()
    {
        if (_plan is not null) return;
        if (_undo is null) return;
        _undo.Disabled = !_store.CanUndo;
        _notice.Text = _store.LoadError;
        foreach (var entry in Walk(_panel).OfType<LineEdit>().Where(e => e.HasMeta("card_id")))
            if (!entry.HasFocus()) entry.Text = _store.Count(entry.GetMeta("card_id").AsString()).ToString();
        foreach (var label in Walk(_panel).OfType<Label>().Where(l => l.HasMeta("owned_label_id")))
            label.Text = _store.Count(label.GetMeta("owned_label_id").AsString()) > 0 ? "Owned" : "Not owned";
        foreach (var minus in Walk(_panel).OfType<Button>().Where(b => b.HasMeta("owned_minus_id")))
            minus.Disabled = _store.Count(minus.GetMeta("owned_minus_id").AsString()) == 0;
        FilterCards();
    }
    private static IEnumerable<Node> Walk(Node node) { foreach (var c in node.GetChildren()) { yield return c; foreach (var n in Walk(c)) yield return n; } }
    private bool Try(Action action)
    {
        try { action(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        { var dialog = new AcceptDialog { Title = "My collection", DialogText = e.Message, Theme = Theme }; AddChild(dialog); dialog.Confirmed += dialog.QueueFree; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(700, 240)); return false; }
    }
    private void Inspect(CatalogueData.Card card)
    {
        var dialog = new AcceptDialog { Title = card.Name, Theme = Theme }; AddChild(dialog);
        var face = new DeckFace(card, _scans) { CustomMinimumSize = new Vector2(320, 460), MouseFilter = MouseFilterEnum.Ignore }; dialog.AddChild(face);
        dialog.Confirmed += dialog.QueueFree; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(350, 530));
    }
    private void ImportFile()
    {
        var file = new FileDialog { Title = "Import my collection", FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, Filters = ["*.csv ; Collection CSV"], Theme = Theme };
        AddChild(file); file.FileSelected += path => { file.QueueFree(); Try(() => { if (new FileInfo(path).Length > 1_000_000) throw new FormatException("CSV exceeds 1 MB."); PreviewCsv(CollectionPrintings.Decode(File.ReadAllBytes(path)), Path.GetFileName(path)); }); };
        file.Canceled += file.QueueFree; file.PopupCentered(new Vector2I(960, 640));
    }
    public void PreviewCsv(string csv, string filename = "my-cards.csv")
    { _plan = _store.Preview(csv, MyCollection.Identities(_data), MyCollection.Printings); _filename = filename; _mode = CollectionImportMode.UpdateListed; BuildImport(); }
    private void BuildImport()
    {
        var plan = _plan!; StartPanel("Review collection import");
        var heading = _panel.GetChildren().OfType<Label>().First(); heading.HorizontalAlignment = HorizontalAlignment.Center; heading.AddThemeFontSizeOverride("font_size", 31); heading.Size = new Vector2(760, 45);
        Put(_panel, CatalogueArt.Label(_filename + " · Nothing has been saved yet", 18), 30, 73, 760, 30);
        for (var i = 0; i < 3; i++)
        {
            var step = CatalogueArt.Label(new[] { "1  Choose file", "2  Review changes", "3  Apply" }[i], 18);
            step.HorizontalAlignment = HorizontalAlignment.Center; if (i == 1) step.AddThemeColorOverride("font_color", CatalogueArt.Gold);
            Put(_panel, step, 30 + i * 260, 120, 240, 34);
        }
        var modes = new List<Button>();
        for (var i = 0; i < 2; i++)
        {
            var mode = (CollectionImportMode)i;
            var option = CatalogueArt.Button(i == 0 ? "Update listed cards" : "Replace entire collection", () => { _mode = mode; BuildImport(); });
            option.Name = i == 0 ? "UpdateListedMode" : "ReplaceCollectionMode"; option.Icon = DeckSkin.Icon(_mode == mode ? "selected" : "radio"); option.AddThemeConstantOverride("h_separation", 10);
            Put(_panel, option, 30 + i * 385, 166, 375, 42); CatalogueArt.Selected(option, _mode == mode); modes.Add(option);
        }
        var listed = plan.Rows.Where(r => !r.Excluded && r.Id is not null).ToArray();
        var omitted = _store.Buckets.Keys.Count(b => !listed.Any(r => r.Bucket == b || !r.Printing && r.Id == MyCollectionStore.CardOf(b)));
        var explanation = _mode == CollectionImportMode.UpdateListed ? "Listed counts replace current totals. Unlisted cards stay unchanged." :
            $"Replace ALL totals. {omitted} currently owned entries not listed become zero. A backup is kept.";
        Put(_panel, Palette.Wrapped(explanation, 17, CatalogueArt.Gold), 30, 214, 760, 48);
        HBoxContainer Line()
        { var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 15); return line; }
        Label Cell(string text, int width, int size = 18)
        { var label = CatalogueArt.Label(text, size); label.CustomMinimumSize = new Vector2(width, 40); return label; }
        var header = Line(); Put(_panel, header, 30, 266, 760, 40); header.AddChild(Cell("Card", 350));
        foreach (var text in new[] { "Current", "Imported", "Change" }) header.AddChild(Cell(text, 105, 17));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(_panel, scroll, 30, 310, 760, 400);
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 12); scroll.AddChild(rows);
        // A real collection file has thousands of rows and almost all of them are ready: only rows that need a decision are drawn in full (a node tree per row is what made big files slow),
        // then a short sample of the changes; the rest are counted. Everything is still applied.
        var repeated = plan.Rows.Where(r => !r.Excluded && r.Id is not null).GroupBy(r => r.Bucket).Where(g => g.Count() > 1).SelectMany(g => g).ToHashSet();
        var attention = plan.Rows.Where(r => r.Error.Length > 0 || r.Id is null || repeated.Contains(r) || r.Guess.Length > 0).Take(150).ToArray();
        var sample = plan.Rows.Where(r => !attention.Contains(r)).Take(25).ToArray();
        var shown = attention.Concat(sample).ToArray();
        var hidden = plan.Rows.Length - shown.Length;
        if (hidden > 0)
            rows.AddChild(Palette.Wrapped($"Showing {attention.Length} rows that need a decision and {sample.Length} others. {hidden} more rows are ready and will be applied as listed.", 16, CatalogueArt.Muted));
        foreach (var row in shown)
        {
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); rows.AddChild(box);
            var line = Line(); box.AddChild(line); var nameCell = new HBoxContainer { CustomMinimumSize = new Vector2(350, 58) }; nameCell.AddThemeConstantOverride("separation", 12); line.AddChild(nameCell);
            var matched = row.Id is null ? null : _data.Cards.FirstOrDefault(c => c.Id == row.Id);
            if (matched is not null)
            {
                var thumbnail = new DeckFace(matched, _scans) { CustomMinimumSize = new Vector2(40, 58) }; nameCell.AddChild(thumbnail); thumbnail.Pressed += () => Inspect(matched);
            }
            var name = Cell(matched?.Name ?? row.Name, matched is null ? 350 : 298); name.TooltipText = $"CSV row {row.Line} · " + row.Name; nameCell.AddChild(name);
            var current = row.Id is null ? 0 : row.Printing ? _store.CountOf(row.Bucket!) : _store.Count(row.Id);
            foreach (var value in new[] { row.Id is null ? "—" : current.ToString(), row.Count.ToString(), row.Id is null ? "—" : (row.Count - current).ToString("+0;-0;0") })
            {
                var cell = Cell(value, 105, 20);
                if (line.GetChildCount() == 3 && row.Id is not null) cell.AddThemeColorOverride("font_color", row.Count >= current ? new Color("9fbc88") : new Color("e39379"));
                line.AddChild(cell);
            }
            var excluded = new HBoxContainer(); box.AddChild(excluded);
            var hint = Cell(row.Error.Length > 0 ? row.Error : row.Excluded ? "Excluded from this import" : row.Id is null ? "Choose the closest card" : row.Guess.Length > 0 ? $"CSV row {row.Line} · {row.Guess}" : $"CSV row {row.Line}" + (row.Printing && row.PrintingLabel.Length > 0 ? " · " + row.PrintingLabel : ""), 600, 15); hint.AddThemeColorOverride("font_color", CatalogueArt.Muted); excluded.AddChild(hint);
            var exclude = new CheckBox { Text = "Exclude", ButtonPressed = row.Excluded }; excluded.AddChild(exclude); exclude.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            exclude.AddThemeIconOverride("checked", DeckSkin.Icon("checked")); exclude.AddThemeIconOverride("unchecked", DeckSkin.Icon("unchecked"));
            exclude.Toggled += yes => { row.Excluded = yes; BuildImport(); };
            if (row.Error.Length == 0 && row.Id is null)
            {
                var choiceScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(740, 194) }; box.AddChild(choiceScroll);
                var choices = new HBoxContainer(); choices.AddThemeConstantOverride("separation", 22); choiceScroll.AddChild(choices);
                foreach (var identity in row.Candidates)
                {
                    var candidate = _data.Cards.Single(c => c.Id == identity.Id); var level = Experienced.Level(candidate.Definition?.Keywords ?? candidate.Keywords);
                    var column = new VBoxContainer(); choices.AddChild(column);
                    var face = new DeckFace(candidate, _scans) { CustomMinimumSize = new Vector2(110, 154) }; column.AddChild(face); face.Pressed += () => { row.Id = identity.Id; BuildImport(); };
                    var button = CatalogueArt.Button(level == 0 ? "Base" : level == 1 ? "Experienced" : "Experienced " + level, () => { row.Id = identity.Id; BuildImport(); });
                    button.AddThemeFontSizeOverride("font_size", 15); button.TooltipText = identity.VersionName; button.CustomMinimumSize = new Vector2(110, 30); column.AddChild(button);
                }
            }
            box.AddChild(new HSeparator());
        }
        Put(_panel, Palette.Wrapped(plan.Problems.FirstOrDefault() ?? $"{plan.Rows.Count(r => !r.Excluded)} listed totals ready" + (plan.Skipped > 0 ? $"; {plan.Skipped} rows with no count skipped" : "") + ". Repeated imports replace totals.", 17, CatalogueArt.Gold), 30, 724, 760, 43);
        Put(_panel, CatalogueArt.Button("Back", BuildEditor), 30, 782, 112, 40); Put(_panel, CatalogueArt.Button("Cancel", BuildEditor), 154, 782, 112, 40);
        Put(_panel, CatalogueArt.Button("CSV template", () => SaveCsv(MyCollectionStore.Template(MyCollection.Printings), "Collection Template.csv")), 282, 782, 176, 40);
        var leaveOut = CatalogueArt.Button("Exclude unresolved", () => { plan.ExcludeUnresolved(); BuildImport(); }); leaveOut.Name = "ExcludeUnresolved"; leaveOut.Disabled = !plan.HasUnresolved;
        leaveOut.TooltipText = "Leave out every row that has no card yet, so the rest can be applied."; Put(_panel, leaveOut, 370, 782, 188, 40);
        var apply = CatalogueArt.Button("Apply changes", ApplyImport); apply.Disabled = !plan.CanApply; apply.Name = "ApplyCollectionImport"; Put(_panel, apply, 568, 782, 222, 40); if (plan.CanApply) DeckSkin.Primary(apply);
        Fit();
    }
    public void ApplyImport()
    {
        if (_plan is null) return;
        void Apply() { if (Try(() => _store.Apply(_plan!, _mode))) BuildEditor(); }
        if (_mode == CollectionImportMode.Replace)
        {
            var confirm = new ConfirmationDialog { Title = "Replace entire collection?", DialogText = "All collection totals will be replaced by this reviewed file. Unlisted cards become unowned. You can undo this import.", Theme = Theme };
            AddChild(confirm); confirm.Confirmed += () => { confirm.QueueFree(); Apply(); }; confirm.Canceled += confirm.QueueFree; confirm.PopupCentered(new Vector2I(720, 250));
        }
        else Apply();
    }
    public void SaveCsv(string csv, string filename)
    {
        var file = new FileDialog { Title = "Export collection CSV", FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Filesystem, CurrentFile = filename, Filters = ["*.csv ; CSV"], Theme = Theme };
        AddChild(file); file.FileSelected += path => { Try(() => File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true))); file.QueueFree(); };
        file.Canceled += file.QueueFree; file.PopupCentered(new Vector2I(960, 640));
    }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && !GetChildren().OfType<Window>().Any(w => w.Visible))
        { if (_plan is null) Close(); else BuildEditor(); GetViewport().SetInputAsHandled(); }
    }
}
