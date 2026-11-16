using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native input and rendering scenarios; storage is isolated from the player's decks.</summary>
public partial class DeckBuilderProbe : Node
{
    private DeckBuilderScreen _screen = null!;
    private string _out = "";
    public override async void _Ready()
    {
        try
        {
            // Cocoa may restore a previous Godot window frame despite --resolution. Pin the review size.
            GetWindow().Title = "L5R — Deck Builder native checks";
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new Vector2I(1672, 941);
            if (OS.GetCmdlineUserArgs().Contains("--navigation-only")) { await CheckWorkspaceNavigation(); GetTree().Quit(); return; }
            _out = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--out="))?[6..] ?? ProjectSettings.GlobalizePath("res://../../artifacts/deck-builder-checks"); Directory.CreateDirectory(_out);
            var storage = Path.Combine(_out, "decks-" + Guid.NewGuid().ToString("N"));
            _screen = new DeckBuilderScreen { StorageDirectory = storage }; AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Wait(2); await Capture("empty-decks");
            Check(Button("New deck").Size == Button("Import deck").Size && Button("New deck").Size == Button("Open deck folder").Size, "three equal landing actions");
            Button("New deck").GrabFocus(); await Wait(.2); await Capture("landing-focus"); Click(Button("New deck")); await Wait(3); await Capture("setup");
            Check(_screen.Draft.Count("Border Keep") == 1 && _screen.Draft.Count("Bamboo Harvesters") == 1, "default setup chosen outside deck totals");
            Check(_screen.Draft.Review().Dynasty == 0 && _screen.Draft.Review().Fate == 0, "setup excluded from counts");
            DeckFace[] Choices() => Walk(_screen).OfType<DeckFace>().Where(f => f.IsVisibleInTree() && f.AccessibilityName.StartsWith("Select ", StringComparison.Ordinal)).ToArray();
            Check(!Walk(_screen).OfType<Button>().Any(b => b.Text is "Select" or "Selected" && b.IsVisibleInTree()), "setup picker has no radio Select buttons (#1268)");
            var strongholds = Choices(); var first = strongholds[0].Card.Id; Click(strongholds[0]); await Wait(.5);
            Check(_screen.Draft.Cards.Count(c => c.SetupSlot == "Stronghold") == 1 && _screen.Draft.Count(first) == 1, "clicking a stronghold card selects it (#1268)"); await Capture("setup-selected");
            Check(!Walk(_screen).OfType<Label>().Any(l => l.Text == "Card inspection" && l.IsVisibleInTree()), "clicking a setup card selects it instead of opening the inspection");
            var second = Choices().FirstOrDefault(f => f.Card.Id != first);
            if (second is not null) { var id = second.Card.Id; Click(second); await Wait(.5); Check(_screen.Draft.Count(id) == 1 && _screen.Draft.Count(first) == 0 && _screen.Draft.Cards.Count(c => c.SetupSlot == "Stronghold") == 1, "clicking another stronghold switches to it (#1268)"); }
            Click(Walk(_screen).OfType<Button>().Single(b => b.AccessibilityName == "Choose Border Keep" && b.IsVisibleInTree())); await Wait(.4); var alternatives = Choices();
            if (alternatives.Length > 0) { Click(alternatives[0]); await Wait(.3); Check(_screen.Draft.Cards.Count(c => c.SetupSlot == "Border Keep") == 1 && _screen.Draft.Count(alternatives[0].Card.Id) == 1, "setup variant replaces previous starting card"); }
            Check(!Walk(_screen).OfType<OptionButton>().Any(o => o.GetItemText(0).StartsWith("Emperor Edition")), "no redundant printing dropdown");
            var source = PublishedContent.Snapshot.Decks.First(d => d.Clan == "Crane");
            var all = new[] { source.StrongholdId, "oracle-l5r-1106", "oracle-l5r-679" }.Concat(source.Dynasty).Concat(source.Fate);
            var entries = all.GroupBy(id => id).Select(g => new DeckEntry(_screen.Data.Cards.Single(c => c.Id == g.Key).Name, g.Count())).ToArray();
            var path = _screen.Store.Save("Crane — Court and Steel", new Deck("Emperor", entries)); Check(_screen.OpenFile(path), "published deck loads into builder"); await Wait(5); await Capture("mat");
            Check(!_screen.Collection.Rows && _screen.Collection.Size.X > _screen.Mat.Size.X, "collection displays full faces beside deck panel");
            // Deck views, Focus/Action filters and full-size hover (#580)
            var browser = _screen.Browser;
            Check(browser.Actions.Trigger.Visible && !browser.Focus.Trigger.Visible, "Dynasty builder offers Action Type but not Focus Value");
            Check(Button("All Dynasty Cards (" + _screen.Collection.Cards.Length.ToString("N0") + " shown)").ToggleMode && Button("Dynasty Deck (" + source.Dynasty.Length + " cards)").ToggleMode && Button("All Fate Cards").ToggleMode && Button("Fate Deck (" + source.Fate.Length + " cards)").ToggleMode, "All Dynasty Cards, Dynasty Deck, All Fate Cards and Fate Deck toggles show live counts, in that order (#792)");
            Check(_screen.Collection.GetGlobalRect().End.Y <= GetViewport().GetVisibleRect().Size.Y, "collection pane stays inside the window with the extra controls");
            var allCount = _screen.Collection.Cards.Length;
            Click(Button("Dynasty Deck (" + source.Dynasty.Length + " cards)")); await Wait(.6);
            var dynastyIds = _screen.Draft.Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains("Dynasty")).Select(c => c.Id).ToHashSet();
            Check(_screen.DeckViewOnly && _screen.Collection.Cards.Length > 0 && _screen.Collection.Cards.Select(c => c.Id).ToHashSet().SetEquals(dynastyIds), "Dynasty Deck shows only the Dynasty cards in the deck");
            await Capture("view-dynasty-deck");
            Click(Button("Fate Deck (" + source.Fate.Length + " cards)")); await Wait(.6);
            var fateIds = _screen.Draft.Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains("Fate")).Select(c => c.Id).ToHashSet();
            Check(_screen.Family == "Fate" && _screen.Collection.Cards.Select(c => c.Id).ToHashSet().SetEquals(fateIds), "Fate Deck shows only the Fate cards in the deck");
            Check(browser.Focus.Trigger.Visible && browser.Actions.Trigger.Visible && _screen.Collection.GetGlobalRect().End.Y <= GetViewport().GetVisibleRect().Size.Y, "Fate builder offers Focus Value and Action Type within the window");
            var focusValue = _screen.Collection.Cards.First(c => c.Focus is not null).Focus!.Value.ToString();
            Click(browser.Focus.Trigger); await Wait(.3); Activate(PopupButton("Focus Value", "Deselect all")); await Wait(.2); Activate(Walk(_screen).OfType<CheckBox>().First(c => c.IsVisibleInTree() && c.Text == focusValue)); await Wait(.4); browser.CloseMenus(); await Wait(.2);
            Check(_screen.Collection.Cards.Length > 0 && _screen.Collection.Cards.All(c => c.Focus.ToString() == focusValue && fateIds.Contains(c.Id)), "deck view honours the Focus Value filter");
            await Capture("view-fate-focus");
            browser.Reset(); await Wait(.3); _screen.SetFamily("Fate"); await Wait(.3);
            var viewBefore = _screen.Collection.Cards.Select(c => c.Id).ToHashSet(); var firstDeckCard = _screen.Collection.Cards[0];
            _screen.Adjust(firstDeckCard, -_screen.Draft.Count(firstDeckCard.Id)); await Wait(.3);
            Check(_screen.Collection.Cards.Select(c => c.Id).ToHashSet().SetEquals(viewBefore), "a card removed in a deck view stays until the view is refreshed");
            _screen.Adjust(firstDeckCard, 3); await Wait(.2);
            Click(Button("All Fate Cards")); await Wait(.6);
            Check(!_screen.DeckViewOnly && _screen.Family == "Fate" && _screen.Collection.Cards.Length > fateIds.Count, "All Fate Cards reveals every Fate card, not only the deck's");
            // Issue #792: All Dynasty Cards filters on Dynasty cards, and the Dynasty-only filters appear there.
            Check(!browser.PersonalHonor.Trigger.Visible && !browser.GoldProduction.Trigger.Visible, "Personal Honor and Gold Production are not offered for Fate cards");
            Click(Button("All Dynasty Cards")); await Wait(.6);
            Check(!_screen.DeckViewOnly && _screen.Family == "Dynasty" && _screen.Collection.Cards.All(c => c.DeckTypes.Contains("Dynasty")) && browser.PersonalHonor.Trigger.Visible && browser.GoldProduction.Trigger.Visible, "All Dynasty Cards shows Dynasty cards only and offers Personal Honor and Gold Production");
            browser.Reset(); await Wait(.3);
            browser.PersonalHonor.SetExcluded(["0", "1", "2", "3", "5+", "None"]); browser.Refresh(); await Wait(.3);
            Check(browser.Results.Length > 0 && browser.Results.All(c => c.Types.Contains("Personality") && c.Stats["ph"][0] == "4"), "the Personal Honor filter keeps only Personalities of that Personal Honor");
            browser.Reset(); await Wait(.3);
            foreach (var amount in new[] { "2", "3" })
            {
                browser.GoldProduction.SetExcluded(new[] { "0", "1", "2", "3", "4", "5+", "None" }.Where(o => o != amount)); browser.Refresh(); await Wait(.3);
                Check(browser.Results.Length > 0 && browser.Results.All(c => c.Types.Contains("Holding")), "the Gold Production filter keeps only Holdings");
                Check(browser.Results.Any(c => c.Name == "Ageless Shrine"), "a Holding that produces 2 or 3 Gold appears under " + amount);
            }
            browser.Reset(); await Wait(.3);
            var hoverFace = Walk(_screen.Collection).OfType<DeckFace>().First(f => f.IsVisibleInTree()); var faceWidth = hoverFace.GetGlobalRect().Size.X;
            HoverOver(hoverFace); await Wait(1.2); var hover = Walk(_screen).OfType<CardScanHover>().Single();
            Check(hover.Visible && hover.Card?.Id == hoverFace.Card.Id && hover.Face.Size.X >= faceWidth * 1.5f - 1, "builder hover shows a scan at least 50% larger than the collection face");
            Check(new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size).Encloses(hover.Face) && !hover.Face.Intersects(hoverFace.GetGlobalRect().Grow(-1)), "builder hover scan stays inside the window beside the card");
            await Capture("hover-full-size");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }); await Wait(.4); Check(!hover.Visible, "builder hover closes when the pointer leaves");
            Click(Button("List")); await Wait(.4); var rowName = Walk(_screen.Mat).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text.Length > 2);
            HoverOver(rowName); await Wait(1.2); Check(hover.Visible && hover.Face.Size.X >= 66, "list rows enlarge the scan beyond their thumbnail");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }); await Wait(.3); Click(Button("Mat")); await Wait(.3);
            _screen.SetFamily("Dynasty"); await Wait(.3);
            Search("Asahina"); await Wait(3); await Capture("collection-large"); Search(""); await Wait(.3);
            var more = Walk(_screen).OfType<MenuButton>().Single(); Click(more); await Wait(.3); Check(more.GetPopup().ItemCount == 2, "compact save-copy and export actions available"); await Capture("deck-actions"); KeyPress(Key.Escape); await Wait(.3);
            Click(Walk(_screen).OfType<Button>().First(b => b.Text == "Deselect all" && b.IsVisibleInTree())); await Wait(.2);
            Check(_screen.Collection.Cards.Length == 0, "Deselect all clans leaves nothing showing");
            Click(Walk(_screen).OfType<Button>().First(b => b.TooltipText == "Crane cards")); await Wait(.2);
            Check(_screen.Collection.Cards.Length > 0 && _screen.Collection.Cards.All(c => c.Clans.Contains("Crane")), "persistent clan medallion filters collection");
            Click(Walk(_screen).OfType<Button>().First(b => b.TooltipText == "Mantis cards")); await Wait(.2);
            Check(_screen.Collection.Cards.All(c => c.Clans.Contains("Crane") || c.Clans.Contains("Mantis")), "native additive multi-clan filter");
            await Capture("filter-menu");
            Click(Walk(_screen).OfType<Button>().First(b => b.Text == "Select all" && b.IsVisibleInTree())); await Wait(.2);
            Click(Walk(_screen).OfType<Button>().First(b => b.Text.StartsWith("Types") && b.IsVisibleInTree())); await Wait(.3);
            Check(!Walk(_screen).OfType<CheckBox>().Any(c => c.IsVisibleInTree() && c.Text == "Spell"), "Dynasty-scoped Type list drops Fate-only types");
            KeyPress(Key.Escape); await Wait(.3);
            Click(Walk(_screen).OfType<Button>().First(b => b.Text.StartsWith("Keywords") && b.IsVisibleInTree())); await Wait(.3);
            var dynastyKeywords = _screen.Data.Cards.Where(c => c.DeckTypes.Contains("Dynasty") && new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Keywords, c.Playable).SetupSlot.Length == 0).SelectMany(c => c.Keywords).Distinct().ToHashSet();
            // Exclude each collection tile's own "Compare" checkbox; only the popover's keyword rows matter here.
            Check(Walk(_screen).OfType<CheckBox>().Where(c => c.IsVisibleInTree() && c.Text != "Compare").All(c => dynastyKeywords.Contains(c.Text) || c.Text == "No keywords"), "keyword list scoped to current family selection");
            KeyPress(Key.Escape); await Wait(.3);
            var before = _screen.Draft.Review(); Check(before.Dynasty == source.Dynasty.Length && before.Fate == source.Fate.Length, "authoritative source totals");
            var card = _screen.Data.Cards.First(c => c.Name == "Daidoji Kenshi");
            Search(card.Name); await Wait(.6); var plus = Walk(_screen.Collection).OfType<Button>().Single(b => b.AccessibilityName == "Add " + card.Name); Click(plus); await Wait(.5); Check(_screen.Dirty, "native quantity edit marks draft dirty");
            _screen.Adjust(card, 4 - _screen.Draft.Count(card.Name)); await Wait(.4); Check(_screen.Draft.Review().OverLimit.Contains(card.Name), "over-limit count gets Core warning"); await Capture("over-limit");
            Search("nonesuchzzzz"); await Wait(.2); Check(_screen.Collection.Cards.Length == 0, "empty search results"); Search("");
            var members = _screen.Draft.Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains("Dynasty")).Take(3).Select(c => _screen.Data.Cards.Single(x => x.Id == c.Id)).ToArray();
            Click(Button("List")); await Wait(.3); Check(_screen.Mat.Rows, "list view selected through native input"); Click(Button("Mat")); await Wait(.3);
            var compareToggle = Walk(_screen.Collection).OfType<CheckBox>().First(c => c.IsVisibleInTree()); Click(compareToggle); await Wait(.3); Check(compareToggle.ButtonPressed, "native comparison selection");
            _screen.ShowComparison(members); await Wait(3); await Capture("compare"); KeyPress(Key.Escape); await Wait(.3);
            var events = _screen.Data.Cards.Where(c => c.Name == "A Fever in the Blood" || c.Name == "A Dangerous Game").GroupBy(c => c.Name).Select(g => g.First()).OrderBy(c => c.Name == "A Fever in the Blood" ? 0 : 1).ToArray();
            _screen.ShowComparison(events); await Wait(3); await Capture("compare-two");
            var parchments = Walk(_screen).OfType<Panel>().Where(p => p.Name.ToString().StartsWith("RulesParchment")).ToArray(); Check(parchments.Length == 2 && parchments.All(p => p.Size.Y >= 180), "two readable rules panels");
            var longRules = _screen.Data.Cards.OrderByDescending(c => (c.Playable ? c.Definition!.Text : CatalogueData.PlainText(c.CurrentOracleText)).Length).Take(3).ToArray(); _screen.ShowComparison(longRules); await Wait(2); await Capture("compare-long-rules");
            var rulesScroll = Walk(_screen).OfType<ScrollContainer>().First(s => s.Name == "RulesScroll" && s.GetVScrollBar().MaxValue > s.Size.Y); rulesScroll.ScrollVertical = 100; await Wait(.2); Check(rulesScroll.ScrollVertical > 0, "long comparison rules remain scrollable"); KeyPress(Key.Escape); await Wait(.3);
            var snapshot = DeckFormat.Write(_screen.Draft.ToDeck()); var bad = Path.Combine(_out, "malformed.txt"); File.WriteAllText(bad, "Legality: Emperor\n1 Missing Imaginary Card"); Check(!_screen.OpenFile(bad) && DeckFormat.Write(_screen.Draft.ToDeck()) == snapshot, "bad import preserves draft"); await Wait(.4); Check(_screen.TroubleshootDialog is { Visible: true }, "bad import offers the troubleshooter"); KeyPress(Key.Escape); await Wait(.4); Check(!Walk(_screen).OfType<AcceptDialog>().Any(d => d.Visible), "error notice dismissed with keyboard");
            _screen.SaveDraft(); await Wait(.3); Check(!_screen.Dirty && DeckFileStore.Read(path) == snapshot, "save and reopen exact text roundtrip");
            _screen.Adjust(card, 1); await Wait(.4); await Capture("before-guard"); Click(Button("‹ My decks")); await Wait(.3); await Capture("after-guard"); Check(Walk(_screen).OfType<ConfirmationDialog>().Any(), "unsaved navigation guarded"); KeyPress(Key.Escape); await Wait(.3); Check(_screen.Page == "editor" && _screen.Dirty, "cancel keeps draft");
            _screen.SaveDraft();
            File.AppendAllText(path, "\n"); _screen.Adjust(card, 1); _screen.SaveDraft(); await Wait(.3); Check(Walk(_screen).OfType<ConfirmationDialog>().Any(d => d.Visible), "external edit requires overwrite decision"); KeyPress(Key.Escape); await Wait(.3); Check(_screen.Dirty, "overwrite cancel preserves dirty draft");
            _screen.SaveDraft(); await Wait(.3); KeyPress(Key.Enter); await Wait(.3); Check(!_screen.Dirty, "confirmed overwrite saves draft");
            Click(Button("‹ My decks")); await Wait(2); Check(!_screen.ViewStrip.IsVisibleInTree(), "deck views are hidden on the My decks page"); await Capture("saved-decks"); Click(Button("Open in builder")); await Wait(.5);
            var slider = _screen.Browser.CardSize; var unchanged = _screen.Collection.Cards.Select(c => c.Id).ToArray();
            await DragSlider(slider, .9f); await Wait(.4); await Capture("slider-large");
            Check(slider.Value > 260 && _screen.Collection.CardWidth == (float)slider.Value, "native builder slider drag enlarges collection faces");
            Check(_screen.Collection.Cards.Select(c => c.Id).SequenceEqual(unchanged), "builder resize preserves filter results");
            await DragSlider(slider, .03f); await Wait(.4); await Capture("compact");
            Check(slider.Value < 150 && _screen.Mat.VisibleCount < 80 && _screen.Collection.VisibleCount < 55, "native smaller faces keep virtualized nodes bounded");
            GetWindow().Size = new Vector2I(1280, 800); await Wait(1); await Capture("small-window");
            Check(_screen.Mat.GetGlobalRect().End.X <= 1280, "mat within compact viewport");
            GetWindow().Size = new Vector2I(1440, 700); await Wait(.5); await Capture("short-window");
            var footer = Walk(_screen).OfType<Button>().Single(b => b.Text == "Export missing").GetParent<Control>();
            Check(_screen.Mat.GetGlobalRect().Size.Y >= 150 && _screen.Mat.GetGlobalRect().End.Y <= footer.GetGlobalRect().Position.Y, "short wide window retains usable deck rows without footer overlap");
            Check(footer.GetGlobalRect().End.Y <= 700 && _screen.Collection.GetGlobalRect().End.Y <= 700, "short wide window keeps deck and collection controls in viewport");
            GetWindow().Size = new Vector2I(1920, 1080); await Wait(.5); await Capture("wide-window");
            _screen.ShowComparison(events); await Wait(.4); await Capture("wide-compare"); KeyPress(Key.Escape); await Wait(.3);
            GetWindow().Size = new Vector2I(1280, 800); await Wait(.4); _screen.ShowComparison(events); await Wait(.4); await Capture("small-compare"); KeyPress(Key.Escape); await Wait(.3);
            _screen.ShowDecks(); await Wait(.3); await Capture("small-saved-decks"); GetWindow().Size = new Vector2I(1920, 1080); await Wait(.4);
            // Import troubleshooter (#655): the dialog and its Confirm Repairs button stay inside the window, at a small and a large size, even with many bad lines.
            var broken = Path.Combine(_screen.Store.DirectoryPath, "Troubleshooter probe.txt"); Directory.CreateDirectory(_screen.Store.DirectoryPath);
            File.WriteAllText(broken, string.Join('\n', "Legality: Ivory", "Border Keep", "Bamboo Harvesters", "Fukuzo - expFukuzo", "Taishuu - expTaishuu", "Daigotsu Aya - expExperienced", "Utaku Liu Xeung - exp", "Makito - exp",
                "3 Daigotsu Susaiken - exp", "3 Daigotsu Negishi - exp", "3 Daigotsu Matsuda - exp", "2 Miryoku no Shimaa", "3 Prosprous Village", "3 Platnum Mine", "3 Shinomen Marsh - exp"));
            foreach (var windowSize in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                GetWindow().Size = windowSize; await Wait(.4);
                Check(_screen.TroubleshootFile(broken), "troubleshooter opens for a deck with unknown cards"); await Wait(.6);
                var dialog = _screen.TroubleshootDialog!; var view = GetViewport().GetVisibleRect();
                Check(view.Encloses(new Rect2(dialog.Position, dialog.Size)), $"troubleshooter dialog {dialog.Size} stays inside the {windowSize.X}x{windowSize.Y} window");
                var confirm = dialog.GetOkButton();
                Check(confirm.Text == "Confirm Repairs" && confirm.IsVisibleInTree() && new Rect2(Vector2.Zero, dialog.Size).Encloses(confirm.GetGlobalRect()), "Confirm Repairs button is visible inside the dialog");
                var list = Walk(dialog).OfType<ScrollContainer>().First();
                Check(list.Size.Y >= 100 && list.GetGlobalRect().End.Y <= confirm.GetGlobalRect().Position.Y, "the card list scrolls above the button, which stays below the last errored card");
                await Capture($"troubleshooter-{windowSize.X}");
                if (windowSize.X == 1920)
                {
                    confirm.EmitSignal(BaseButton.SignalName.Pressed); await Wait(1.5); await Capture("troubleshooter-repaired");
                    Check(_screen.Draft.Count("Fukuzo") == 1 && _screen.Draft.Count("Taishuu") == 1, "Confirm Repairs applied the chosen cards to the draft");
                    Check(File.Exists(broken + ".bak") && File.ReadAllText(broken).StartsWith("Legality: Emperor"), "repaired deck file is rewritten in the builder format and the original is kept");
                    KeyPress(Key.Escape); await Wait(.3);
                }
                else { dialog.Hide(); dialog.QueueFree(); await Wait(.3); }
            }
            _screen.QueueFree(); await Wait(.3); var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate(); AddChild(main); await Wait(2); await Capture("main-menu");
            var menu = Walk(main).OfType<TitleMenu>().Single(); Check(menu.Entries.GetChildren().OfType<Button>().All(b => b.GetGlobalRect().End.Y <= GetViewport().GetVisibleRect().Size.Y), "all six menu entries fit in viewport"); var entry = Walk(menu).OfType<Button>().Single(b => string.Equals(b.Text, "Deck Builder", StringComparison.OrdinalIgnoreCase) && b.IsVisibleInTree()); Click(entry); await Wait(2);
            Check(GetTree().Root.GetNodeOrNull("DeckBuilderLayer") is not null, "main menu opens deck builder"); await Capture("menu-deck-builder");
            GD.Print("DECK BUILDER PASS: native setup, quantity, search, warnings, comparison, save/load, corrupt import, unsaved cancel, density, responsive captures");
            if (!OS.GetCmdlineUserArgs().Contains("--stay")) GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task CheckWorkspaceNavigation()
    {
        await Wait(.1); var root = GetTree().Root; root.GuiEmbedSubwindows = false;
        LibraryDialog.Show(root); await Wait(.5);
        var library = Walk(root).OfType<CatalogueScreen>().Single();
        library.Browser.CardSize.Value = 270;
        Click(Walk(library).OfType<Button>().Single(b => b.Name == "OpenBuilder")); await Wait(.5);
        var builder = Walk(root).OfType<DeckBuilderScreen>().Single();
        Check(!Walk(root).OfType<CatalogueScreen>().Any(), "ordinary Library navigation retires the old overlay");
        Check(root.GuiEmbedSubwindows, "Library to Builder preserves embedded checklist placement");
        Check(builder.Browser.CardSize.Value == 270, "card size preference follows Library to Builder");
        var source = L5R.Core.PublishedContent.Snapshot.Decks.First(d => d.Clan == "Crane");
        var entries = new[] { source.StrongholdId, "oracle-l5r-1106", "oracle-l5r-679" }.Concat(source.Dynasty).Concat(source.Fate)
            .GroupBy(id => id).Select(g => new DeckEntry(builder.Data.Cards.Single(c => c.Id == g.Key).Name, g.Count())).ToArray();
        builder.OpenPracticeDeck(new Deck("Emperor", entries), "Navigation review");
        Click(Walk(builder).OfType<Button>().Single(b => b.Name == "OpenLibrary")); await Wait(.3);
        Check(Walk(builder).OfType<ConfirmationDialog>().Any(d => d.Visible), "Library navigation guards an unsaved draft");
        KeyPress(Key.Escape); await Wait(.2);
        Check(builder.IsInsideTree() && builder.Dirty && !Walk(root).OfType<CatalogueScreen>().Any(), "cancel preserves the draft and current workspace");
        Click(Walk(builder).OfType<Button>().Single(b => b.Name == "OpenLibrary")); await Wait(.2);
        var guard = Walk(builder).OfType<ConfirmationDialog>().Single(d => d.Visible);
        Activate(Walk(guard, true).OfType<Button>().Single(b => b.Text == "Discard changes")); await Wait(.5);
        library = Walk(root).OfType<CatalogueScreen>().Single();
        Check(!Walk(root).OfType<DeckBuilderScreen>().Any() && root.GuiEmbedSubwindows, "Builder to Library retires overlay and preserves embedded menus");
        Check(library.Browser.CardSize.Value == 270, "card size preference follows Builder to Library");
        library.Browser.Keywords.Open(); await Wait(.2);
        Check(library.Browser.Keywords.IsOpen && library.Grid.GetGlobalRect().Encloses(library.Browser.Keywords.PopupRect), "switched workspace checklist remains inside the browser");
        library.Browser.CloseMenus(); Click(Walk(library).OfType<Button>().Single(b => b.Text == "‹  Back")); await Wait(.3);
        Check(!root.GuiEmbedSubwindows && !Walk(root).OfType<CatalogueScreen>().Any(), "closing the final workspace restores original popup mode");
        GD.Print("WORKSPACE NAVIGATION PASS");
    }
    private async Task Capture(string name) { await Wait(.15); RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_out, name + ".png")); GD.Print("CAPTURE " + name); }
    private static IEnumerable<Node> Walk(Node root, bool includeInternal = false) { foreach (var child in root.GetChildren(includeInternal)) { yield return child; foreach (var n in Walk(child, includeInternal)) yield return n; } }
    private static void HoverOver(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p - new Vector2(3, 3), GlobalPosition = p - new Vector2(3, 3) }); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); }
    private Button PopupButton(string menu, string text) => Walk(_screen).OfType<PopupPanel>().First(p => p.Name == menu + "Checklist").GetChildren().SelectMany(c => Walk(c).Prepend(c)).OfType<Button>().First(b => b.Text == text);
    private Button Button(string text) => Walk(_screen).OfType<Button>().First(b => b.Text == text && b.IsVisibleInTree());
    private static void Click(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }); }
    private void Search(string text) { _screen.Search.GrabFocus(); _screen.Search.SelectAll(); KeyPress(Key.Backspace); foreach (var ch in text) { Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = true }); Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = false }); } }
    private async Task DragSlider(HSlider slider, float fraction)
    {
        var rect = slider.GetGlobalRect(); var start = rect.Position + new Vector2(12, rect.Size.Y / 2);
        var end = rect.Position + new Vector2(12 + (rect.Size.X - 24) * fraction, rect.Size.Y / 2);
        Input.ParseInputEvent(new InputEventMouseButton { Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Left, Pressed = true });
        for (var i = 1; i <= 6; i++) { var p = start.Lerp(end, i / 6f); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left }); await Wait(.025); }
        Input.ParseInputEvent(new InputEventMouseButton { Position = end, GlobalPosition = end, ButtonIndex = MouseButton.Left, Pressed = false });
    }
    private static void KeyPress(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false }); }
    // A checkbox inside a popover Window's global rect does not map back to main-viewport click
    // coordinates, so popover content is activated via keyboard focus + Enter instead of Click().
    private static void Activate(Control control) { control.GrabFocus(); KeyPress(Key.Enter); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); GD.Print("PASS " + message); }
}
