using Godot;
namespace L5R.Client;

public partial class CatalogueProbe : Node
{
    private CatalogueScreen _screen = null!;
    private string _output = "";
    public override async void _Ready()
    {
        try
        {
            // Cocoa may restore a previous Godot window frame despite --resolution. Pin the review size.
            GetWindow().Title = "L5R — Card Library native checks";
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new Vector2I(1672, 941);
            _output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--out="))?[6..] ?? ProjectSettings.GlobalizePath("res://../../artifacts/catalogue-checks"); Directory.CreateDirectory(_output);
            _screen = new CatalogueScreen(); AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Wait(2); Check(_screen.Scans.PackAvailable, "complete local artwork pack present");
            Check(_screen.Data.Cards.SelectMany(c => c.Printings).All(_screen.Scans.HasArtwork), "every printing including pinned game faces has WebP artwork");
            Check(_screen.Data.Cards.Length == 2156, "full reference catalogue");
            Check(_screen.Data.Cards.All(c => c.Printings.Length > 0), "every card has indexed scan variants");
            Check(_screen.Data.Filter(playable: true).All(c => c.Playable), "support comes from Core");
            Check(_screen.Data.Filter(clans: ["Crane"], types: ["Personality"]).All(c => c.Clans.Contains("Crane") && c.Types.Contains("Personality")), "combined clan and type filters");
            Check(_screen.Data.Filter(family: "Fate", keywords: ["Battle"]).All(c => c.DeckTypes.Contains("Fate") && c.Keywords.Contains("Battle")), "family and keyword filters");
            Check(_screen.Data.Filter(clans: ["Crane", "Mantis"]).All(c => c.Clans.Contains("Crane") || c.Clans.Contains("Mantis")), "additive multi-clan filter");
            var setSorted = _screen.Data.Filter(sort: "Set");
            Check(setSorted.SequenceEqual(setSorted.OrderBy(CatalogueData.SetRank).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)), "Set sort follows the presentation order");
            Check(_screen.Data.Filter(search: "nonesuchzzzz").Length == 0, "no results");
            // Every image is local, including on first use. Allow asynchronous decode to finish.
            await Wait(4); await Capture("gallery");
            var actions = Walk(_screen).OfType<MenuButton>().Single(b => b.AccessibilityName == "More library actions");
            Click(actions); await Wait(.2); actions.GetPopup().SetFocusedItem(0); PressKey(Godot.Key.Enter); await Wait(.4);
            var archive = Walk(_screen).OfType<Window>().Single(w => w.Title == "Reference library");
            Check(archive.Visible && Walk(archive).OfType<TabContainer>().Single().GetTabTitle(0) == "Imported decks", "native library menu retains imported decks and source archive");
            await Capture("source-archive"); PressKey(Godot.Key.Escape); await Wait(.3);
            Check(!GodotObject.IsInstanceValid(archive) && _screen.Grid.IsVisibleInTree(), "Escape closes only the source archive and returns to browsing");
            Click(_screen.Browser.Support.Trigger); await Wait(.3); Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "Not yet playable")); await Wait(.3); _screen.Browser.CloseMenus(); await Wait(.2); Check(_screen.Browser.Support.Active && _screen.Grid.Cards.Length == _screen.Data.Filter(playable: true).Length && _screen.Grid.Cards.Length > 0, "native playable filter");
            Click(FindButton(b => b.Text == "Deselect all")); await Wait(.3);
            Check(_screen.Grid.Cards.Length == 0, "deselect all clans leaves nothing showing");
            Click(FindButton(b => b.TooltipText == "Crane cards")); await Wait(.3);
            Check(_screen.Grid.Cards.Length > 0 && _screen.Grid.Cards.All(c => c.Playable && c.Clans.Contains("Crane")), "native clan filter combines with support");
            Click(FindButton(b => b.TooltipText == "Mantis cards")); await Wait(.3);
            Check(_screen.Grid.Cards.All(c => c.Playable && (c.Clans.Contains("Crane") || c.Clans.Contains("Mantis"))), "native additive multi-clan filter");
            await Capture("clans-filter");
            Click(FindButton(b => b.Text == "Select all")); await Wait(.3);
            Check(_screen.Grid.Cards.Length == _screen.Data.Filter(playable: true).Length, "select all clans matches unrestricted filter");
            Click(FindButton(b => b.Text == "Deselect all")); await Wait(.3);
            Check(_screen.Grid.Cards.Length == 0, "deselect all hides every clan");
            _screen.ResetFilters(); await Wait(.2); Click(FindButton(b => b.Text == "Fate")); await Wait(.3); Check(_screen.Grid.Cards.Length > 0 && _screen.Grid.Cards.All(c => c.DeckTypes.Contains("Fate")), "native family filter");
            Click(FindButton(b => b.Text.StartsWith("Types"))); await Wait(.3); Check(!Walk(_screen).OfType<CheckBox>().Any(c => c.IsVisibleInTree() && c.Text == "Personality"), "Fate-scoped Type list drops Dynasty-only types");
            Activate(PopupButton("Types", "Deselect all")); await Wait(.2); Check(_screen.Grid.Cards.Length == 0, "deselect all types leaves nothing showing");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Spell")); await Wait(.2);
            PressKey(Godot.Key.Escape); await Wait(.2);
            Click(FindButton(b => b.Text.StartsWith("Keywords"))); await Wait(.3);
            Check(_screen.Grid.GetGlobalRect().Encloses(_screen.Browser.Keywords.PopupRect), "keyword checklist anchored inside browser card area");
            var keywordSearch = _screen.Browser.Keywords.Search; EnterText(keywordSearch, "xyz-impossible"); await Wait(.2);
            Check(!Walk(_screen).OfType<CheckBox>().Any(c => c.IsVisibleInTree()), "keyword search narrows checklist without changing card results");
            EnterText(keywordSearch, ""); await Wait(.2);
            var fateSpellKeywords = _screen.Data.Cards.Where(c => c.Types.Contains("Spell") && c.DeckTypes.Contains("Fate")).SelectMany(c => c.Keywords).Distinct().ToHashSet();
            Check(Walk(_screen).OfType<CheckBox>().Where(c => c.IsVisibleInTree()).All(c => fateSpellKeywords.Contains(c.Text) || c.Text == "No keywords"), "keyword list scoped to current Type and Family selection");
            await Capture("keyword-search-cleared");
            Activate(Walk(_screen).OfType<Button>().First(b => b.Text == "Most common" && b.IsVisibleInTree())); await Wait(.2);
            var rows = Walk(_screen).OfType<CheckBox>().Where(c => c.IsVisibleInTree()).Select(c => c.Text).Where(t => t != "No keywords").ToArray();
            var expectedOrder = _screen.Data.Filter(family: "Fate", types: ["Spell"]).SelectMany(c => c.Keywords).GroupBy(k => k).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.Key).ToArray();
            Check(rows.SequenceEqual(expectedOrder), "most-common keyword order ranks by frequency within the current filter");
            await Capture("keywords"); PressKey(Godot.Key.Escape); await Wait(.2);
            _screen.ResetFilters(); await Wait(.2);
            Click(FindButton(b => b.Text == "Dynasty")); await Wait(.2); Click(FindButton(b => b.Text.StartsWith("Types"))); await Wait(.2);
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Personality")); await Wait(.2); _screen.Browser.Types.Close();
            Click(FindButton(b => b.Text == "Fate")); await Wait(.2);
            Check(!_screen.Browser.Types.Checked("Personality") && _screen.Browser.Types.Active, "an unchecked type stays unchecked, and listed, when the family changes");
            Click(FindButton(b => b.TooltipText == "Crane cards")); await Wait(.2);
            Check(_screen.Browser.ExcludedClans.SetEquals(["Crane"]), "unchecking the Crane box excludes only Crane");
            Click(FindButton(b => b.AccessibilityName == "Remove filter Clans")); await Wait(.2);
            Check(_screen.Browser.ExcludedClans.Count == 0 && _screen.Browser.Types.Active, "removable clan tag checks only the clans again");
            {
                var bar = _screen.Browser; bool Imp(CatalogueData.Card c) => c.Types.Contains("Personality") && c.Keywords.Contains("Imperial");
                bool Keep(CatalogueData.Card c) => c.Types.Contains("Stronghold") && c.Name.StartsWith("Journey's End Keep");
                bar.Reset(); bar.OnlyClans("Imperial"); await Wait(.2);
                Check(bar.Results.Length > 0 && bar.Results.All(c => Imp(c) || Keep(c)) && bar.Results.Any(Keep), "Imperial clan box shows Imperial Personalities, whatever their Clan, and the Journey's End Keeps");
                bar.ExcludedClans.Clear(); bar.ExcludedClans.Add("Imperial"); bar.Refresh(); await Wait(.2);
                Check(_screen.Data.Cards.Where(c => Imp(c) && c.Clans.Contains("Crane")).All(c => bar.Results.Contains(c)), "unchecking Imperial keeps Imperial Personalities that have another Clan");
                bar.Reset(); bar.Sets.Only("Embers of War"); bar.ExcludedClans.Add("Lion"); bar.Search.Text = "a"; bar.Refresh(); await Wait(.2);
                Click(FindButton(b => b.AccessibilityName == "Choose filters to clear")); await Wait(.3); await Capture("clear-all-menu");
                Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Printing set" && c.IsVisibleInTree())); await Wait(.2); PressKey(Godot.Key.Escape); await Wait(.2);
                Check(bar.KeptOnClear.SetEquals(["Printing set"]), "the Clear All menu lists Printing set and unchecking it keeps it");
                Click(FindButton(b => b.Text == "Clear All")); await Wait(.3);
                Check(bar.Sets.Active && bar.ExcludedClans.Count == 0 && bar.Search.Text == "", "Clear All resets checked filters and preserves an unchecked one");
                bar.KeptOnClear.Clear(); bar.ClearSelected(); await Wait(.2);
                Check(!bar.Sets.Active, "Clear All resets every filter once all are checked");
            }
            _screen.ResetFilters(); await Wait(.2);
            Search("Akodo"); await Wait(3); Check(_screen.Grid.Cards.All(c => c.SearchText.Contains("Akodo", StringComparison.OrdinalIgnoreCase)), "live search");
            var tile = _screen.Grid.Tile("oracle-l5r-229") ?? throw new Exception("Akodo Kisho tile absent"); _screen.Grid.EnsureControlVisible(tile); await Wait(.5); Click(tile); await Wait(1);
            Check(_screen.SelectedId == "oracle-l5r-229", "native click opens details"); Check(_screen.Grid.GetGlobalRect().End.X <= _screen.InspectorRect.Position.X, "inspector cannot cover gallery cards"); await Capture("inspection");
            tile = _screen.Grid.Tile("oracle-l5r-229") ?? throw new Exception("Selected tile absent after reflow"); _screen.Grid.EnsureControlVisible(tile); await Wait(.3); Click(tile); await Wait(.5); Check(_screen.SelectedId == "", "repeat click dismisses details");
            _screen.ResetFilters(); await Wait(.3);
            _screen.Grid.ScrollVertical = 4300; await Wait(.3);
            var sizeSlider = _screen.Browser.CardSize;
            var filtersBefore = _screen.Grid.Cards.Select(c => c.Id).ToArray();
            var anchorId = _screen.Grid.Cards.First(c => _screen.Grid.Tile(c.Id)?.GetGlobalRect().Intersects(_screen.Grid.GetGlobalRect()) == true).Id;
            var anchorTop = _screen.Grid.Tile(anchorId)!.GetGlobalRect().Position.Y;
            await DragSlider(sizeSlider, .9f); await Wait(.4); await Capture("slider-large");
            Check(sizeSlider.Value > 260 && _screen.Grid.CardWidth == (float)sizeSlider.Value, "native slider drag enlarges full catalogue faces");
            Check(_screen.Grid.Cards.Select(c => c.Id).SequenceEqual(filtersBefore), "card resizing preserves filter results");
            Check(_screen.Grid.Tile(anchorId) is not null, "card resizing preserves browsing anchor in virtual pool");
            Check(Math.Abs(_screen.Grid.Tile(anchorId)!.GetGlobalRect().Position.Y - anchorTop) <= 2, "enlarging cards preserves the partial-row scroll offset");
            var smallerAnchor = _screen.Grid.Cards.First(c => _screen.Grid.Tile(c.Id)?.GetGlobalRect().Intersects(_screen.Grid.GetGlobalRect()) == true).Id;
            var smallerTop = _screen.Grid.Tile(smallerAnchor)!.GetGlobalRect().Position.Y;
            await DragSlider(sizeSlider, .03f); await Wait(.4); await Capture("slider-small");
            Check(sizeSlider.Value < 150, "native slider drag reduces card size");
            Check(Math.Abs(_screen.Grid.Tile(smallerAnchor)!.GetGlobalRect().Position.Y - smallerTop) <= 2, "reducing cards preserves the partial-row scroll offset");
            sizeSlider.Value = 196; await Wait(.3); _screen.Grid.ScrollVertical = 4800; await Wait(1); Check(_screen.Grid.VisibleTileCount < 55, "bounded visible tile pool");
            var before = _screen.Grid.ScrollVertical; var other = _screen.Grid.Cards.First(); _screen.SelectCard(other); await Wait(.2); PressKey(Godot.Key.Escape); await Wait(.3); Check(_screen.SelectedId == "", "Escape dismisses inspector");
            Check(Math.Abs(_screen.Grid.ScrollVertical - before) <= 2, "inspection restores exact scrolling position");
            Search("nonesuchzzzz"); await Wait(.3); await Capture("empty");
            // Focus Value, Action Type and full-size hover (#580)
            _screen.ResetFilters(); await Wait(.2); var browser = _screen.Browser;
            Check(!browser.Focus.Trigger.Visible && !browser.Actions.Trigger.Visible, "Focus Value and Action Type stay hidden while browsing All");
            Click(FindButton(b => b.Text == "Strongholds")); await Wait(.2); Check(!browser.Actions.Trigger.Visible, "Action Type is not offered for Strongholds");
            Click(FindButton(b => b.Text == "Fate")); await Wait(.3);
            Check(browser.Focus.Trigger.Visible && browser.Actions.Trigger.Visible, "Focus Value and Action Type appear for Fate cards");
            Check(browser.Focus.Trigger.GetGlobalRect().End.Y <= _screen.Grid.GetGlobalRect().Position.Y && browser.Actions.Trigger.GetGlobalRect().End.Y <= _screen.Grid.GetGlobalRect().Position.Y, "Focus Value and Action Type sit inside the filter bar above the gallery");
            var fate = _screen.Data.Filter(family: "Fate");
            Click(browser.Focus.Trigger); await Wait(.3);
            Check(Walk(_screen).OfType<CheckBox>().Where(c => c.IsVisibleInTree()).Select(c => c.Text).SequenceEqual(["0", "1", "2", "3", "4", "5", "None"]), "Focus Value offers 0 through 5 and None");
            Activate(PopupButton("Focus Value", "Deselect all")); await Wait(.3);
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "3")); await Wait(.3);
            Check(_screen.Grid.Cards.Length > 0 && _screen.Grid.Cards.All(c => c.Focus == 3) && _screen.Grid.Cards.Length == fate.Count(c => c.Focus == 3), "Focus Value 3 matches exactly Fate cards with Focus 3");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "5")); await Wait(.3);
            Check(_screen.Grid.Cards.Length == fate.Count(c => c.Focus is 3 or 5), "Focus Value choices combine");
            await Capture("focus-filter"); browser.CloseMenus(); await Wait(.2);
            _screen.ResetFilters(); await Wait(.2); Click(FindButton(b => b.Text == "Fate")); await Wait(.3);
            Click(browser.Actions.Trigger); await Wait(.3);
            Check(Walk(_screen).OfType<CheckBox>().Where(c => c.IsVisibleInTree()).Select(c => c.Text).SequenceEqual(["Limited", "Open", "Battle", "Battle/Open", "Battle/Limited", "Reaction", "None"]), "Action Type offers the seven choices");
            Activate(PopupButton("Action Type", "Deselect all")); await Wait(.3);
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "Battle")); await Wait(.3);
            Check(_screen.Grid.Cards.Length > 0 && _screen.Grid.Cards.Length == fate.Count(c => c.Actions.Contains("Battle") || c.Actions.Contains("Battle/Open") || c.Actions.Contains("Battle/Limited")), "Battle includes cards printing Battle/Open or Battle/Limited");
            Check(_screen.Grid.Cards.Any(c => c.Actions.Contains("Battle/Open") && !c.Actions.Contains("Battle")), "Battle results include a Battle/Open-only card");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "Battle/Open")); await Wait(.3);
            Check(_screen.Grid.Cards.Select(c => c.Id).Distinct().Count() == _screen.Grid.Cards.Length, "a card appears once however many checked choices it satisfies");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "Battle")); await Wait(.3);
            Check(_screen.Grid.Cards.Length > 0 && _screen.Grid.Cards.All(c => c.Actions.Contains("Battle/Open")), "Battle/Open alone matches only cards printing Battle/Open");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "Battle/Open")); await Wait(.3); Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == "None")); await Wait(.3);
            Check(_screen.Grid.Cards.Length > 0 && _screen.Grid.Cards.All(c => c.Actions.Length == 0), "None matches only cards with no printed action");
            await Capture("action-filter"); browser.CloseMenus(); await Wait(.2);
            Click(FindButton(b => b.Text == "Dynasty")); await Wait(.3);
            Check(!browser.Focus.Trigger.Visible && browser.Actions.Trigger.Visible && browser.Actions.Checked("None") && !browser.Actions.Checked("Battle"), "Dynasty keeps Action Type, hides Focus Value");
            foreach (var name in new[] { "Akodo Kisho", "Asako Moeru", "Remote Village", "Shinjo's Children", "Focus" })
                Check(_screen.Data.Cards.Where(c => c.Name == name).All(c => c.Actions.Length == 0), name + " has no printed action");
            Check(_screen.Grid.Cards.Any(c => c.Name == "Remote Village") && _screen.Grid.Cards.Any(c => c.Name == "Akodo Kisho"), "None filter lists Dynasty cards without printed actions");
            Click(FindButton(b => b.Text == "All")); await Wait(.3);
            Check(!browser.Actions.Active && browser.ScopeNotice.Contains("Action Type"), "Action Type clears with an explanation when unavailable");
            _screen.ResetFilters(); await Wait(.3);
            var hoverCard = _screen.Grid.Cards[0]; var hoverTile = _screen.Grid.Tile(hoverCard.Id)!; var cardWidth = hoverTile.Size.X;
            HoverOver(hoverTile); await Wait(1.2);
            Check(_screen.Hover.Visible && _screen.Hover.Card?.Id == hoverCard.Id && _screen.Hover.Face.Size.X >= cardWidth * 1.5f - 1, "hover shows a scan at least 50% larger than the gallery face");
            Check(Math.Abs(_screen.Hover.Face.Size.Y / _screen.Hover.Face.Size.X - 1.4f) < .01f && new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size).Encloses(_screen.Hover.Face), "hover scan keeps card proportions inside the window");
            Check(!_screen.Hover.Face.Intersects(hoverTile.GetGlobalRect().Grow(-1)), "hover scan does not cover the hovered card");
            await Capture("hover-full-size");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }); await Wait(.4);
            Check(!_screen.Hover.Visible, "hover scan closes when the pointer leaves");
            Check(_screen.Scans.LoadedFromPackCount > 0, "hover uses the local WebP pack");
            _screen.Browser.CardSize.Value = 120; await Wait(.5); _screen.Grid.LayoutCards(); await Wait(.3);
            HoverOver(_screen.Grid.Tile(_screen.Grid.Cards[0].Id)!); await Wait(1.2);
            Check(_screen.Hover.Visible && _screen.Hover.Face.Size.X >= 120 * 1.5f, "smallest cards still enlarge");
            _screen.Browser.CardSize.Value = 196; Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }); await Wait(.3);
            _screen.ResetFilters(); Search("Akodo"); await Wait(.5); _screen.SelectCard(_screen.Data.Cards.First(c => c.Id == "oracle-l5r-229")); await Wait(.3);
            var window = GetWindow(); window.Size = new Vector2I(1280, 800); await Wait(.8); await Capture("compact");
            Click(FindButton(b => b.Text.StartsWith("Rules text"))); await Wait(.3); await Capture("rules");
            PressKey(Godot.Key.Escape); await Wait(.3); Click(_screen.Browser.Sets.Trigger); await Wait(.3); await Capture("sets"); PressKey(Godot.Key.Escape); await Wait(.3);
            var card = _screen.Data.Cards.First(c => !c.Playable); var fallback = new CatalogueData.Printing { Id = "probe-missing", SourceUrl = "https://images.oracleofthevoid.com/l5r/missing.jpg", Sha256 = new string('0', 64) }; _screen.Scans.Get(fallback); await Wait(.3); Check(_screen.Scans.State(fallback).Contains("unavailable"), "missing offline scan is explicit");
            var saved = card.Printings; card.Printings = [fallback]; _screen.ResetFilters(); Search(card.Name); await Wait(.5); await Capture("missing-scan"); card.Printings = saved;
            GD.Print("PLAYABLE COUNT " + _screen.Data.Filter(playable: true).Length);
            Check(_screen.Scans.TextureCount <= 80, "bounded texture cache");
            Check(_screen.Scans.LoadedFromPackCount > 30, "gallery and inspector load WebP from local pack");
            var pinned = _screen.Data.Cards.SelectMany(c => c.Printings).First(p => !string.IsNullOrEmpty(p.ResourcePath));
            var oldBytes = Godot.FileAccess.GetFileAsBytes(pinned.ResourcePath);
            var oldHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(oldBytes)).ToLowerInvariant();
            var migrationDir = ProjectSettings.GlobalizePath("user://catalogue-migration-probe"); Directory.CreateDirectory(migrationDir);
            var oldFile = Path.Combine(migrationDir, oldHash + ".jpg"); File.WriteAllBytes(oldFile, oldBytes);
            var untouched = Path.Combine(migrationDir, "unrelated.jpg"); File.WriteAllText(untouched, "preserve");
            var migration = new CatalogueScans(legacyCacheDirectory: migrationDir); migration.Get(pinned); await Wait(.5);
            Check(migration.TextureCount == 1 && !File.Exists(oldFile) && File.Exists(untouched), "verified WebP retires only matching legacy JPEG cache"); migration.Close();
            File.WriteAllText(oldFile, "not the recorded original"); migration = new CatalogueScans(legacyCacheDirectory: migrationDir); migration.Get(pinned); await Wait(.5);
            Check(migration.TextureCount == 1 && File.Exists(oldFile), "migration preserves files that do not match source receipt"); migration.Close(); File.Delete(oldFile); File.Delete(untouched); Directory.Delete(migrationDir);
            // A present ZIP with a damaged member must not produce a texture or fetch a JPEG.
            var original = _screen.Data.Cards.SelectMany(c => c.Printings).First(p => string.IsNullOrEmpty(p.ResourcePath));
            var corruptPath = "user://catalogue-corrupt-probe.zip";
            using (var writer = new ZipPacker()) { Check(writer.Open(corruptPath) == Error.Ok, "corrupt fixture created"); writer.StartFile(original.Sha256 + ".webp"); writer.WriteFile(new byte[] { 1, 2, 3 }); writer.CloseFile(); writer.Close(); }
            var corrupt = new CatalogueScans(corruptPath); corrupt.Get(original); await Wait(.4);
            Check(corrupt.TextureCount == 0 && corrupt.State(original).Contains("unavailable"), "corrupt WebP rejected without an HTTP fallback"); corrupt.Close(); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(corruptPath));
            _screen.QueueFree(); await Wait(.3); GD.Print("CATALOGUE PASS: data, native select/deselect, filters, virtual rows, Escape, compact layout, missing scans, texture bound"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name) { _screen.QueueRedraw(); await Wait(.1); RenderingServer.ForceDraw(); GD.Print("CAPTURE " + name); GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output, name + ".png")); }
    private void Search(string text) { _screen.Search.GrabFocus(); _screen.Search.SelectAll(); PressKey(Godot.Key.Backspace); foreach (var ch in text) { Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Godot.Key)char.ToUpperInvariant(ch), Pressed = true }); Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Godot.Key)char.ToUpperInvariant(ch), Pressed = false }); } }
    private Button PopupButton(string menu, string text) => Walk(_screen).OfType<PopupPanel>().First(p => p.Name == menu + "Checklist").GetChildren().SelectMany(c => Walk(c).Prepend(c)).OfType<Button>().First(b => b.Text == text);
    private static IEnumerable<Node> Walk(Node root) { foreach (var child in root.GetChildren()) { yield return child; foreach (var n in Walk(child)) yield return n; } }
    private static void HoverOver(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p - new Vector2(3, 3), GlobalPosition = p - new Vector2(3, 3) }); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); }
    private Button FindButton(Func<Button, bool> predicate) => Walk(_screen).OfType<Button>().First(predicate);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); GD.Print("PASS " + message); }
    private static void Click(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }); }
    private async Task DragSlider(HSlider slider, float fraction)
    {
        var rect = slider.GetGlobalRect();
        var valueFraction = (float)((slider.Value - slider.MinValue) / (slider.MaxValue - slider.MinValue));
        var start = rect.Position + new Vector2(12 + (rect.Size.X - 24) * valueFraction, rect.Size.Y / 2);
        var end = rect.Position + new Vector2(12 + (rect.Size.X - 24) * fraction, rect.Size.Y / 2);
        Input.ParseInputEvent(new InputEventMouseButton { Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Left, Pressed = true });
        for (var i = 1; i <= 6; i++) { var p = start.Lerp(end, i / 6f); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left }); await Wait(.025); }
        Input.ParseInputEvent(new InputEventMouseButton { Position = end, GlobalPosition = end, ButtonIndex = MouseButton.Left, Pressed = false });
    }
    private static void EnterText(LineEdit field, string text)
    {
        field.GrabFocus(); field.SelectAll(); PressKey(Godot.Key.Backspace);
        foreach (var ch in text) { Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Godot.Key)char.ToUpperInvariant(ch), Pressed = true }); Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Godot.Key)char.ToUpperInvariant(ch), Pressed = false }); }
    }
    private static void PressKey(Godot.Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false }); }
    /// <summary>Activates a focusable control (e.g. a checkbox inside a popover) via keyboard focus
    /// + Enter rather than a synthesized screen-space click, since global rects inside a popup
    /// Window do not map back to main-viewport click coordinates.</summary>
    private static void Activate(Control control) { control.GrabFocus(); PressKey(Godot.Key.Enter); }
}
