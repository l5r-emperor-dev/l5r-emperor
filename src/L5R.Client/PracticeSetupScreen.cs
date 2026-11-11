using Godot;
using L5R.AI;
using L5R.Core;
namespace L5R.Client;

public sealed record PracticeChoice(string Name, Deck? Deck, PracticeDeckReview Review, string? Path = null, string Source = "Saved deck");

public partial class PracticeSetupScreen : Control
{
    public event Action? Closed;
    public Action<Deck, Deck> StartMatch = (_, _) => { };
    public string? StorageDirectory;
    /// <summary>The player profile the remembered deck choices belong to (captures use a throwaway one).</summary>
    public string Profile = "default";
    /// <summary>Issue #1214: how hard the practice opponent plays; remembered with the deck choices.</summary>
    public BotLevel Difficulty { get; private set; } = BotLevel.Easy;
    public string Page { get; private set; } = "setup";
    public int ActiveSlot { get; private set; }
    public PracticeChoice[] Choices { get; private set; } = [];
    public PracticeButton StartButton { get; private set; } = null!;
    public LineEdit Search { get; private set; } = null!;
    public readonly Control Canvas = new() { Size = new Vector2(1672, 941) };
    private readonly Control _page = new();
    private readonly CatalogueData _data = new();
    private readonly CatalogueScans _scans = new();
    private readonly List<PracticeChoice> _saved = [];
    private DeckFileStore _store = null!;
    private bool _presets, _editing;
    private string _query = "", _notice = "";
    public override void _Ready()
    {
        Theme = SidebarSkin.Theme(); MouseFilter = MouseFilterEnum.Stop;
        _store = StorageDirectory is null ? PreloadedDecks.OpenStore() : new DeckFileStore(StorageDirectory);
        Choices = [Preset("Crane"), Preset("Lion")];
        RestoreSelection();
        var black = new ColorRect { Color = new Color("05090d"), MouseFilter = MouseFilterEnum.Ignore }; AddChild(black); black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(Canvas); Canvas.AddChild(new TextureRect { Texture = PracticeArt.Background, Size = Canvas.Size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore }); Canvas.AddChild(_page); _page.Size = Canvas.Size;
        Resized += Fit; TreeExiting += _scans.Close; ReloadFiles(); ShowSetup(); Fit();
    }
    private void Fit() { var scale = Math.Min(Size.X / 1672f, Size.Y / 941f); Canvas.Scale = Vector2.One * scale; Canvas.Position = (Size - Canvas.Size * scale) / 2; }
    public static PracticeChoice Preset(string clan) { var deck = CustomPractice.Preset(clan); return new(clan + " — Starter", deck, CustomPractice.Review(deck), Source: "Built-in preset"); }
    private PracticeChoice Read(string path)
    {
        try { var deck = DeckFormat.Parse(DeckFileStore.Read(path)); return new(System.IO.Path.GetFileNameWithoutExtension(path), deck, CustomPractice.Review(deck), path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentException) { return new(System.IO.Path.GetFileNameWithoutExtension(path), null, new(0, 0, "Unaligned", null, [e.Message]), path); }
    }
    public void ReloadFiles()
    {
        _saved.Clear(); try { _saved.AddRange(_store.Files().Select(Read)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _notice = "Cannot read saved decks: " + e.Message; }
        for (var i = 0; i < Choices.Length; i++) if (Choices[i].Path is { } path) Choices[i] = Read(path);
    }
    private static T Put<T>(Control parent, T child, float x, float y, float w, float h) where T : Control { parent.AddChild(child); child.Position = new(x, y); child.Size = new(w, h); return child; }
    private Label Text(string value, float x, float y, float w, float h, int size = 24, bool heading = false, bool bold = false, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left, Control? parent = null)
    {
        var label = new Label { Text = heading ? value.ToUpperInvariant() : value, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = align, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, TooltipText = value };
        label.AddThemeFontOverride("font", heading ? Palette.HeadingLabel(size) : bold ? PracticeArt.Bold : Palette.Body); label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color ?? Palette.Paper);
        return Put(parent ?? _page, label, x, y, w, h);
    }
    private PracticeButton Button(string text, Action action, float x, float y, float w, float h, bool primary = false, Control? parent = null)
        => Put(parent ?? _page, new PracticeButton(text, action) { Primary = primary, PointSize = primary ? 30 : 24 }, x, y, w, h);
    private void Header()
    {
        Button("‹  Back", () => { if (Page == "browser") ShowSetup(); else Closed?.Invoke(); }, 40, 20, 158, 48);
        Text("Practice setup", 400, 5, 850, 55, 48, true, align: HorizontalAlignment.Center);
        var line = new Control { MouseFilter = MouseFilterEnum.Ignore }; Put(_page, line, 32, 67, 1607, 1); line.Draw += () => PracticeArt.Rule(line, 0, 0, 1607);
        Text("LEGEND OF THE FIVE RINGS\nEMPEROR EDITION", 1330, 16, 300, 46, 15, color: Palette.Gold, align: HorizontalAlignment.Right);
    }
    private string Stronghold(PracticeChoice choice) => choice.Review.StrongholdId is { } id ? _data.Cards.FirstOrDefault(c => c.Id == id)?.Name ?? "Choose a Stronghold" : "Choose a Stronghold";
    private void Crest(PracticeChoice c, float x, float y, float w, float h, Control? parent = null)
    {
        var icon = CatalogueArt.Clan(c.Review.Clan);
        if (c.Review.Clan is "Crane" or "Lion")
        {
            icon.Texture = new AtlasTexture { Atlas = PracticeArt.A, Region = c.Review.Clan == "Crane" ? new Rect2(193, 133, 123, 125) : new Rect2(879, 132, 126, 129) };
            var shader = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/Practice/crest-mask.gdshader") }; var region = ((AtlasTexture)icon.Texture).Region; shader.SetShaderParameter("region", new Vector4(region.Position.X / PracticeArt.A.GetWidth(), region.Position.Y / PracticeArt.A.GetHeight(), region.Size.X / PracticeArt.A.GetWidth(), region.Size.Y / PracticeArt.A.GetHeight())); icon.Material = shader;
        }
        Put(parent ?? _page, icon, x, y, w, h);
    }
    private void Artwork(PracticeChoice c, float x, float y, float w, float h, Control? parent = null)
    {
        var card = _data.Cards.FirstOrDefault(a => a.Id == c.Review.StrongholdId);
        if (card is not null) { var button = Button("", () => Inspect(card), x, y, w, h, parent: parent); button.TooltipText = "Inspect " + card.Name; Put(button, new PracticeStrongholdArt(card, _scans), 0, 0, w, h); }
        else Text("Select a Stronghold", x, y, w, h, 23, color: Palette.Gold, align: HorizontalAlignment.Center, parent: parent);
    }
    private static string Status(PracticeChoice c) => c.Review.Ready ? "Ready to play" : c.Review.Issues.FirstOrDefault() ?? "Needs attention";
    // The last deck chosen for each side is kept from session to session: a built-in preset by its clan, a saved deck by its file. A slot whose deck is gone or unreadable falls back to its default.
    private string SelectionFile => StorageDirectory is null ? $"user://practice-selection-{Profile}.cfg" : System.IO.Path.TrimEndingDirectorySeparator(StorageDirectory) + ".selection.cfg";
    private string Remembered(PracticeChoice choice) => choice.Path is { } path ? "path:" + path : "preset:" + choice.Name.Split(' ')[0];
    private void SaveSelection()
    {
        var config = new ConfigFile();
        for (var i = 0; i < Choices.Length; i++) config.SetValue("practice", "slot" + i, Remembered(Choices[i]));
        config.SetValue("practice", "difficulty", Difficulty.ToString());
        config.Save(SelectionFile);
    }
    private void RestoreSelection()
    {
        var config = new ConfigFile();
        if (config.Load(SelectionFile) != Error.Ok) return;
        Difficulty = BotLevels.Parse(config.GetValue("practice", "difficulty", "").AsString());
        for (var i = 0; i < Choices.Length; i++)
        {
            var value = config.GetValue("practice", "slot" + i, "").AsString();
            if (value.StartsWith("preset:", StringComparison.Ordinal) && value[7..] is "Crane" or "Lion") Choices[i] = Preset(value[7..]);
            else if (value.StartsWith("path:", StringComparison.Ordinal) && System.IO.File.Exists(value[5..]) && Read(value[5..]) is { Deck: not null } restored) Choices[i] = restored;
        }
    }
    public void SetDifficulty(BotLevel level) { Difficulty = level; SaveSelection(); if (Page == "browser") BuildBrowser(); else ShowSetup(); }
    public void Select(PracticeChoice choice) { Choices[ActiveSlot] = choice; SaveSelection(); _notice = ""; if (Page == "browser") BuildBrowser(); else ShowSetup(); }
    public void ShowSetup()
    {
        Page = "setup"; Palette.Clear(_page); Header();
        Text("Choose your deck and your practice opponent.", 350, 68, 980, 38, 25, align: HorizontalAlignment.Center);
        for (var i = 0; i < 2; i++) BuildDeckPanel(i, i == 0 ? 155 : 842);
        Button("Open deck builder", () => { ActiveSlot = 0; Edit(null); }, 40, 835, 300, 55).Bare = true;
        Text("Difficulty", 372, 826, 140, 52, 26, align: HorizontalAlignment.Right);
        foreach (var (level, i) in Enum.GetValues<BotLevel>().Select((l, i) => (l, i)))
        {
            var button = Button(level.Label(), () => SetDifficulty(level), 530 + i * 195, 826, 180, 52); button.Selected = Difficulty == level; button.AccessibilityName = "Difficulty " + level.Label();
        }
        Text(Difficulty.Describe(), 372, 880, 860, 32, 20, color: Palette.PaperDim, align: HorizontalAlignment.Center);
        StartButton = Button("START PRACTICE", Start, 1250, 822, 385, 72, true); StartButton.Disabled = Choices.Any(c => !c.Review.Ready); StartButton.QueueRedraw();
        if (_notice.Length > 0) Text(_notice, 360, 897, 1240, 32, 18, color: PracticeArt.Amber);
        Text("Saved decks · Local Practice", 1150, 902, 480, 25, 15, color: Palette.PaperDim, align: HorizontalAlignment.Right);
    }
    private void BuildDeckPanel(int slot, float x)
    {
        var c = Choices[slot]; Put(_page, new PracticePanel(), x, 120, 666, 686);
        Text(slot == 0 ? "Your deck" : "Opponent's deck", x + 179, 142, 470, 62, 40, true);
        Artwork(c, x + 33, 244, 602, 194); Crest(c, x + 39, 134, 125, 125);
        Text(c.Name, x + 30, 448, 606, 49, 31, bold: true, align: HorizontalAlignment.Center);
        Text(c.Source, x + 30, 491, 606, 35, 24, align: HorizontalAlignment.Center);
        Text("Stronghold: " + Stronghold(c), x + 38, 528, 590, 38, 24, align: HorizontalAlignment.Center);
        Put(_page, new PracticeDeckTotals(c.Review.Dynasty, c.Review.Fate), x + 105, 580, 446, 70);
        var rules = new Control { MouseFilter = MouseFilterEnum.Ignore }; Put(_page, rules, x, 120, 666, 686); rules.Draw += () => { PracticeArt.Rule(rules, 20, 540, 626); PracticeArt.Rule(rules, 123, 407, 420); PracticeArt.Rule(rules, 123, 447, 420); PracticeArt.Rule(rules, 179, 96, 453); };
        if (c.Review.Ready) { Put(_page, new PracticeReadyIcon(), x + 241, 674, 42, 42); Text("Ready to play", x + 296, 677, 240, 39, 26, color: PracticeArt.Ready); }
        else
        {
            var line = Text("!  " + Status(c), x + 33, 663, 600, 70, 22, color: PracticeArt.Amber, align: HorizontalAlignment.Center);
            line.MouseFilter = MouseFilterEnum.Pass; line.TooltipText = string.Join("\n", c.Review.Issues); Button("Edit deck", () => Edit(Choices[slot]), x + 355, 731, 235, 52);
        }
        Button("Change deck", () => Browse(slot), x + (slot == 0 || !c.Review.Ready ? 85 : 204), 731, 235, 52);
        if (c.Review.Ready && slot == 0) Button("Edit deck", () => Edit(Choices[slot]), x + 355, 731, 235, 52);
    }
    public void Browse(int slot) { ActiveSlot = slot; Page = "browser"; _query = ""; _presets = _saved.Count == 0; BuildBrowser(); }
    private void BuildBrowser()
    {
        Page = "browser"; Palette.Clear(_page); Header();
        Put(_page, new PracticePanel { Browser = true }, 30, 95, 1030, 790); Put(_page, new PracticePanel { Browser = true }, 1070, 95, 572, 790);
        Text(ActiveSlot == 0 ? "Choose your deck" : "Choose opponent's deck", 65, 100, 850, 62, 40, true);
        var saved = Button("Saved decks", () => { _presets = false; BuildBrowser(); }, 65, 176, 210, 50); saved.Selected = !_presets;
        var preset = Button("Presets", () => { _presets = true; BuildBrowser(); }, 289, 176, 185, 50); preset.Selected = _presets;
        Search = Put(_page, new LineEdit { Text = _query, PlaceholderText = "Search decks…", ClearButtonEnabled = true }, 690, 176, 340, 50);
        var list = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(_page, list, 58, 244, 975, 550);
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 8); list.AddChild(rows);
        void Fill()
        {
            Palette.Clear(rows); var entries = (_presets ? new[] { Preset("Crane"), Preset("Lion") } : _saved.ToArray()).Where(c => (c.Name + " " + Stronghold(c)).Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var c in entries)
            {
                var row = new PracticeButton("", () => Select(c)) { Outline = true, DividerAt = 669, OutlineColor = Choices[ActiveSlot].Name == c.Name && Choices[ActiveSlot].Path == c.Path ? PracticeArt.Amber : new Color("715931"), Quiet = true, CustomMinimumSize = new Vector2(960, 133), SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AccessibilityName = c.Name + " · " + Status(c); row.TooltipText = string.Join("\n", c.Review.Issues); rows.AddChild(row);
                var card = _data.Cards.FirstOrDefault(a => a.Id == c.Review.StrongholdId); if (card is not null) Put(row, new PracticeStrongholdArt(card, _scans), 5, 4, 222, 124);
                Text(c.Name, 252, 4, 405, 46, c.Name.Length > 32 ? 22 : 27, bold: true, parent: row); Text(Stronghold(c), 252, 45, 410, 33, 22, parent: row);
                Crest(c, 251, 83, 48, 46, row); Text($"Dynasty {c.Review.Dynasty} · Fate {c.Review.Fate}", 312, 88, 340, 30, 21, parent: row);
                if (c.Review.Ready) { Put(row, new PracticeReadyIcon(), 696, 48, 36, 36); Text("Ready to play", 747, 47, 199, 38, 23, color: PracticeArt.Ready, parent: row); } else Text("!  " + Status(c), 696, 10, 251, 110, 22, color: PracticeArt.Amber, parent: row);
            }
            if (entries.Length == 0) rows.AddChild(Palette.Wrapped(_query.Length > 0 ? "No matching decks." : "No saved decks yet. Import a deck, build one, or choose a preset.", 26));
        }
        Search.TextChanged += value => { _query = value; Fill(); }; Fill();
        Button("Import deck", Import, 60, 816, 274, 48); Button("Deck builder", () => Edit(null), 365, 816, 270, 48);
        Text("Your match", 1110, 100, 470, 60, 40, true);
        for (var i = 0; i < 2; i++)
        {
            var n = i; var c = Choices[i]; var y = i == 0 ? 190 : 420;
            var selected = ActiveSlot == i;
            var summary = new PracticeButton("", () => Browse(n)) { Quiet = true, RichOutline = selected, Outline = true, OutlineColor = selected ? PracticeArt.Amber : new Color("705930") }; Put(_page, summary, 1097, y, 520, 216);
            summary.AccessibilityName = "Choose " + (i == 0 ? "your" : "opponent's") + " deck";
            Text(i == 0 ? "Your deck" : "Opponent's deck", 22, 4, 476, 40, 22, parent: summary);
            Crest(c, 22, 51, 125, 125, summary);
            Text(c.Name, 178, 43, 325, 62, c.Name.Length > 27 ? 22 : 26, bold: true, parent: summary);
            Text(Stronghold(c), 178, 104, 320, 36, 22, parent: summary);
            Text($"Dynasty {c.Review.Dynasty} · Fate {c.Review.Fate}", 178, 142, 320, 30, 21, parent: summary);
            Text(selected ? "Choosing this deck" : "Change deck  ›", 178, 177, 315, 31, 21, color: selected ? PracticeArt.Ready : Palette.Paper, parent: summary);
        }
        var ready = Choices.All(c => c.Review.Ready); Text(ready ? "Both decks are ready." : "Review your deck before playing.", 1110, 680, 485, 45, 24, color: ready ? PracticeArt.Ready : PracticeArt.Amber, align: HorizontalAlignment.Center);
        Button("Edit selected deck", () => Edit(Choices[ActiveSlot]), 1135, 731, 459, 48);
        StartButton = Button("START PRACTICE", Start, 1115, 792, 504, 67, true); StartButton.Disabled = !ready;
    }
    private void Inspect(CatalogueData.Card card)
    {
        var texture = _scans.Get(card.Printings.FirstOrDefault()); if (texture is null) return;
        var dialog = new AcceptDialog { Title = card.Name, Theme = SidebarSkin.Theme() }; AddChild(dialog);
        dialog.AddChild(new TextureRect { Texture = texture, CustomMinimumSize = new Vector2(350, 480), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        dialog.Confirmed += dialog.QueueFree; dialog.CloseRequested += dialog.QueueFree; dialog.GetOkButton().Text = "Close";
        dialog.PopupCentered(new Vector2I(380, 550));
    }
    private void Import()
    {
        var dialog = new FileDialog { Title = "Import Practice deck", UseNativeDialog = true, FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, Filters = ["*.txt ; Deck text files"] }; AddChild(dialog);
        dialog.FileSelected += path => { dialog.QueueFree(); var c = Read(path); _saved.RemoveAll(d => d.Path == path); _saved.Insert(0, c); _presets = false; Select(c); }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(900, 650));
    }
    private void Edit(PracticeChoice? choice)
    {
        if (_editing) return; _editing = true; var editSlot = choice is null ? ActiveSlot : Array.IndexOf(Choices, choice); if (editSlot < 0) editSlot = ActiveSlot;
        var layer = new CanvasLayer { Layer = 96 }; GetTree().Root.AddChild(layer);
        var editor = new DeckBuilderScreen { StorageDirectory = _store.DirectoryPath }; layer.AddChild(editor); editor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (choice?.Path is { } path && choice.Deck is not null) editor.OpenFile(path);
        else if (choice?.Deck is { } deck) editor.OpenPracticeDeck(deck, choice.Name + " copy");
        editor.Closed += () => { var savedPath = editor.SavedPath; layer.QueueFree(); _editing = false; ReloadFiles(); if (savedPath is not null) { Choices[editSlot] = Read(savedPath); SaveSelection(); } ShowSetup(); };
    }
    private void Start()
    {
        ReloadFiles(); if (Choices.Any(c => !c.Review.Ready || c.Deck is null)) { _notice = "A selected deck needs attention."; ShowSetup(); return; }
        try { StartMatch(Choices[0].Deck!, Choices[1].Deck!); } catch (ArgumentException e) { _notice = e.Message; ShowSetup(); }
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!_editing && @event is InputEventKey { Pressed: true, Keycode: Key.Escape }) { if (Page == "browser") ShowSetup(); else Closed?.Invoke(); GetViewport().SetInputAsHandled(); }
    }
}
