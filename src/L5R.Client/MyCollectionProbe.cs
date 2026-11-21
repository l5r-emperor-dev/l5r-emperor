using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class MyCollectionProbe : Node
{
    private string _out = "";
    private int _checks;
    public override async void _Ready()
    {
        try
        {
            // Cocoa may restore a previous Godot window frame despite --resolution. Pin the review size.
            GetWindow().Title = "L5R — My Collection native checks";
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new Vector2I(1672, 941);
            if (OS.GetCmdlineUserArgs().Contains("--review-isolation-only")) { await CheckReviewIsolation(); GetTree().Quit(); return; }
            _out = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--out="))?[6..] ?? ProjectSettings.GlobalizePath("res://../../artifacts/my-collection-checks");
            Directory.CreateDirectory(_out);
            MyCollection.OverridePath = Path.Combine(_out, "inventory-" + Guid.NewGuid().ToString("N"), "collection.json");
            var library = new CatalogueScreen(); AddChild(library); library.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Wait(2);
            foreach (var search in new[] { "discarding", "  discarding   Fate  " })
            {
                library.Browser.Search.Text = search; library.Browser.Refresh(); await Wait(.2);
                var expected = library.Browser.Results.Select(c => c.Id).ToArray();
                Check(expected.Length > 0, "catalogue rules search has results: " + search.Trim());
                library.Browser.SetOwnership(OwnershipFilter.Owned); await Wait(.2);
                Click(library.Browser.EditCollectionButton); await Wait(.2);
                var searchEditor = library.GetChildren().OfType<MyCollectionEditor>().Single();
                Check(Walk(searchEditor).OfType<DeckSurface>().Single().Cards.Select(c => c.Id).SequenceEqual(expected), "collection editor shares rules and multi-term search while bypassing caller ownership: " + search.Trim());
                Click(Walk(searchEditor).OfType<Button>().Single(b => b.Text == "Done")); await Wait(.2);
                library.Browser.SetOwnership(OwnershipFilter.All);
            }
            library.Browser.Search.Text = "Asahina"; library.Browser.Refresh(); await Wait(.3);
            var card = library.Browser.Results.First(); MyCollection.Store.Set(card.Id, 3);
            library.Browser.SetOwnership(OwnershipFilter.Owned); await Wait(.2);
            Check(library.Grid.Cards.Length == 1 && library.Grid.Cards[0].Id == card.Id, "Library Owned filter uses shared physical inventory");
            library.Browser.SetOwnership(OwnershipFilter.Unowned); await Wait(.2); Check(!library.Grid.Cards.Any(c => c.Id == card.Id), "Library Unowned filter excludes owned card");
            library.Browser.SetOwnership(OwnershipFilter.All); await Capture("library");
            // The Owned numeral under a card edits in place: check or Enter or an outside click keeps the number, x or Esc puts the old one back.
            var libraryTile = library.Grid.Tile(card.Id) ?? throw new Exception("library tile for " + card.Name + " is not showing");
            async Task<LineEdit> OpenOwned()
            {
                Click(libraryTile.GetChildren().OfType<Button>().Single(b => b.Name == "OwnedNumeral")); await Wait(.2);
                var field = libraryTile.GetChildren().OfType<Control>().Single(c => c.Name == "OwnedEditor").GetChildren().OfType<LineEdit>().Single();
                field.SelectAll(); return field;
            }
            void Type(char digit) => Input.ParseInputEvent(new InputEventKey { Unicode = digit, Keycode = (Key)digit, Pressed = true });
            var owned = await OpenOwned(); Check(libraryTile.Editing && owned.Text == "3", "clicking the Owned numeral opens an editable field holding the current number");
            await Capture("owned-editing");
            Type('7'); await Wait(.1); Click(libraryTile.GetChildren().OfType<Control>().Single(c => c.Name == "OwnedEditor").GetChildren().OfType<Button>().Single(b => b.Name == "OwnedAccept")); await Wait(.2);
            Check(!libraryTile.Editing && MyCollection.Store.Count(card.Id) == 7, "the green check keeps the typed number");
            owned = await OpenOwned(); Type('9'); await Wait(.1); KeyPress(Key.Escape); await Wait(.2);
            Check(!libraryTile.Editing && MyCollection.Store.Count(card.Id) == 7, "Esc puts the old number back");
            owned = await OpenOwned(); Type('2'); await Wait(.1); Click(libraryTile.GetChildren().OfType<Control>().Single(c => c.Name == "OwnedEditor").GetChildren().OfType<Button>().Single(b => b.Name == "OwnedRevert")); await Wait(.2);
            Check(!libraryTile.Editing && MyCollection.Store.Count(card.Id) == 7, "the red x puts the old number back");
            owned = await OpenOwned(); Type('4'); await Wait(.1); KeyPress(Key.Enter); await Wait(.2);
            Check(!libraryTile.Editing && MyCollection.Store.Count(card.Id) == 4, "Enter keeps the typed number");
            owned = await OpenOwned(); Type('6'); await Wait(.1); Click(library.Browser.Search); await Wait(.2);
            Check(!libraryTile.Editing && MyCollection.Store.Count(card.Id) == 6, "clicking elsewhere keeps the typed number");
            owned = await OpenOwned(); Type('0'); await Wait(.1); KeyPress(Key.Enter); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 0, "0 is a valid number");
            // Tab accepts and moves to the next card's field (the first of the next row after the last in a row); Shift+Tab goes back the same way.
            var order = library.Grid.Cards; var tileAt = (int i) => library.Grid.Tile(order[i].Id) ?? throw new Exception("tile " + i + " is not showing");
            var editorOf = (CatalogueCardTile t) => t.GetChildren().OfType<Control>().SingleOrDefault(c => c.Name == "OwnedEditor")?.GetChildren().OfType<LineEdit>().Single();
            MyCollection.Store.Set(order[0].Id, 0); MyCollection.Store.Set(order[1].Id, 0); await Wait(.1);
            Click(tileAt(0).GetChildren().OfType<Button>().Single(b => b.Name == "OwnedNumeral")); await Wait(.2);
            editorOf(tileAt(0))!.SelectAll(); Type('8'); KeyPress(Key.Tab); await Wait(.3);
            Check(MyCollection.Store.Count(order[0].Id) == 8 && !tileAt(0).Editing && tileAt(1).Editing, "Tab accepts the number and opens the next card's field");
            var next = editorOf(tileAt(1))!; Check(next.HasFocus() && next.HasSelection() && next.GetSelectionFromColumn() == 0 && next.GetSelectionToColumn() == next.Text.Length, "the next field opens with its value highlighted");
            Type('5'); Input.ParseInputEvent(new InputEventKey { Keycode = Key.Tab, ShiftPressed = true, Pressed = true }); await Wait(.3);
            Check(MyCollection.Store.Count(order[1].Id) == 5 && tileAt(0).Editing && !tileAt(1).Editing, "Shift+Tab accepts and opens the previous card's field");
            KeyPress(Key.Escape); await Wait(.1);
            var columns = Enumerable.Range(1, order.Length - 1).First(i => tileAt(i).Position.Y > tileAt(0).Position.Y);
            Click(tileAt(columns - 1).GetChildren().OfType<Button>().Single(b => b.Name == "OwnedNumeral")); await Wait(.2); KeyPress(Key.Tab); await Wait(.3);
            Check(tileAt(columns).Editing, "Tab from the last card in a row opens the first card of the next row");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Tab, ShiftPressed = true, Pressed = true }); await Wait(.3);
            Check(tileAt(columns - 1).Editing, "Shift+Tab from the first card in a row opens the last card of the row above");
            KeyPress(Key.Escape); await Wait(.1);
            MyCollection.Store.Set(order[0].Id, 0); MyCollection.Store.Set(order[1].Id, 0);
            MyCollection.Store.Set(card.Id, 3); await Wait(.2);
            Click(library.Browser.EditCollectionButton); await Wait(.3);
            var editor = library.GetChildren().OfType<MyCollectionEditor>().Single();
            Check(editor.GetGlobalRect().Size == library.Size, "focused editor covers screen and blocks background");
            Click(Walk(editor).OfType<Button>().First(b => b.AccessibilityName == "Increase owned copies of " + card.Name)); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 4, "native plus edits owned count");
            var entry = Walk(editor).OfType<LineEdit>().Single(e => e.HasMeta("card_id") && e.GetMeta("card_id").AsString() == card.Id);
            entry.GrabFocus(); entry.SelectAll(); KeyPress(Key.Backspace); Input.ParseInputEvent(new InputEventKey { Unicode = '5', Keycode = Key.Key5, Pressed = true }); KeyPress(Key.Enter); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 5, "native direct numeric entry saves ownership");
            await Capture("editor");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Owned")); await Wait(.2);
            for (var i = 0; i < 5; i++) { Click(Walk(editor).OfType<Button>().Single(b => b.AccessibilityName == "Reduce owned copies of " + card.Name)); await Wait(.06); }
            Check(!Walk(editor).OfType<DeckFace>().Any(f => f.Card.Id == card.Id), "Owned editor tab removes a card immediately at zero");
            MyCollection.Store.Set(card.Id, 5); await Wait(.2);
            Check(Walk(editor).OfType<DeckFace>().Any(f => f.Card.Id == card.Id), "Owned editor tab responds to restored inventory");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "All cards")); await Wait(.2);
            var twins = library.Data.Cards.GroupBy(c => Experienced.BaseTitle(c.Name)).First(g => g.Count() > 1).ToArray();
            editor.PreviewCsv("card_name,owned_count\n\"" + Experienced.BaseTitle(twins[0].Name).Replace("\"", "\"\"") + "\",2\n"); await Wait(.3);
            Check(!editor.ImportPlan!.CanApply, "ambiguous version blocks apply in UI");
            Check(Walk(editor).OfType<Button>().Single(b => b.Name == "ApplyCollectionImport").Disabled, "Apply disabled for unresolved card");
            await Capture("csv-unresolved");
            var candidate = Walk(editor).OfType<DeckFace>().First(f => f.Card.Id == twins[0].Id); Click(candidate); await Wait(.2);
            Check(editor.ImportPlan.CanApply, "full version card click resolves exact catalogue identity");
            Click(Walk(editor).OfType<Button>().Single(b => b.Name == "ApplyCollectionImport")); await Wait(.2);
            Check(MyCollection.Store.Count(twins[0].Id) == 2, "reviewed CSV updates selected version");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Undo last change")); await Wait(.2);
            Check(MyCollection.Store.Count(twins[0].Id) == (twins[0].Id == card.Id ? 5 : 0), "native import undo restores previous collection");
            var exported = Path.Combine(_out, "native-export-" + Guid.NewGuid().ToString("N") + ".csv");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Export CSV")); await Wait(.2);
            var saveDialog = editor.GetChildren().OfType<FileDialog>().Single(); saveDialog.CurrentPath = exported;
            saveDialog.GetOkButton().GrabFocus(); KeyPress(Key.Enter); await Wait(.2);
            Check(File.Exists(exported) && File.ReadAllText(exported).Contains("Set,Card Name,Card Type,Rarity,Clan,Limit,Count") && File.ReadAllText(exported).Contains(MyCollection.Identities(library.Data).Single(i => i.Id == card.Id).VersionName), "native export file chooser writes the per-printing file with current inventory");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Import CSV")); await Wait(.2);
            var openDialog = editor.GetChildren().OfType<FileDialog>().Single(); openDialog.CurrentDir = Path.GetDirectoryName(exported)!;
            var filenameInput = openDialog.GetLineEdit(); filenameInput.GrabFocus(); filenameInput.SelectAll(); KeyPress(Key.Backspace);
            foreach (var character in Path.GetFileName(exported)) Input.ParseInputEvent(new InputEventKey { Unicode = character, Pressed = true });
            KeyPress(Key.Enter); await Wait(.5);
            Check(editor.ImportPlan?.CanApply == true && MyCollection.Store.Count(card.Id) == 5, "native import file chooser opens review without changing inventory: " + string.Join(" | ", editor.ImportPlan?.Problems ?? []));
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Cancel")); await Wait(.2);
            MyCollection.Store.Set(twins[0].Id, 3);
            editor.PreviewCsv($"card_id,owned_count\n{card.Id},1\n"); await Wait(.2);
            Click(Walk(editor).OfType<Button>().Single(b => b.Name == "ReplaceCollectionMode")); await Wait(.2);
            Click(Walk(editor).OfType<Button>().Single(b => b.Name == "ApplyCollectionImport")); await Wait(.2);
            var replaceDialog = editor.GetChildren().OfType<ConfirmationDialog>().Single(); replaceDialog.GetCancelButton().GrabFocus(); KeyPress(Key.Enter); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 5 && MyCollection.Store.Count(twins[0].Id) == 3, "cancel replacement preserves listed and omitted counts");
            Click(Walk(editor).OfType<Button>().Single(b => b.Name == "ApplyCollectionImport")); await Wait(.2);
            replaceDialog = editor.GetChildren().OfType<ConfirmationDialog>().Single(); replaceDialog.GetOkButton().GrabFocus(); KeyPress(Key.Enter); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 1 && MyCollection.Store.Count(twins[0].Id) == 0, "confirmed replacement clears omitted ownership");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Undo last change")); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 5 && MyCollection.Store.Count(twins[0].Id) == 3, "undo replacement restores the complete previous inventory");
            editor.PreviewCsv($"card_id,card_name,owned_count\n{card.Id},\"{card.Name}\",1\n"); await Wait(.2); await Capture("csv-ready");
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Cancel")); await Wait(.2);
            Check(MyCollection.Store.Count(card.Id) == 5, "cancel CSV review preserves inventory");
            var timer = System.Diagnostics.Stopwatch.StartNew(); editor.PreviewCsv(MyCollectionStore.Template(MyCollection.Printings).Replace("\"0\"", "\"1\"")); timer.Stop();
            Check(timer.ElapsedMilliseconds < 4000, $"a {MyCollection.Printings.Length}-row file opens its review in {timer.ElapsedMilliseconds} ms"); await Wait(.2);
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Cancel")); await Wait(.2);
            Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Done")); await Wait(.2);
            library.QueueFree(); await Wait(.3);
            var builder = new DeckBuilderScreen { StorageDirectory = Path.Combine(_out, "decks") }; AddChild(builder); builder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var source = PublishedContent.Snapshot.Decks.First(d => d.Clan == "Crane");
            var ids = new[] { source.StrongholdId, "oracle-l5r-1106", "oracle-l5r-679" }.Concat(source.Dynasty).Concat(source.Fate);
            builder.OpenPracticeDeck(new Deck("Emperor", ids.GroupBy(i => i).Select(g => new DeckEntry(builder.Data.Cards.Single(c => c.Id == g.Key).Name, g.Count())).ToArray()), "Collection review — Crane");
            foreach (var info in builder.Draft.Cards) MyCollection.Store.Set(info.Id, 10);
            var target = builder.Draft.Cards.First(c => c.SetupSlot.Length == 0 && builder.Draft.Count(c.Id) == 3); MyCollection.Store.Set(target.Id, 1); await Wait(2);
            Check(builder.MissingCards().Length == 1 && builder.MissingCards()[0].Need == 2, "deck shortage is exact and separate from legality");
            Check(builder.MissingCsv().Contains("needed_count"), "missing export cannot be mistaken for owned inventory CSV");
            var draft = DeckFormat.Write(builder.Draft.ToDeck()); var dirty = builder.Dirty;
            builder.Collection.ScrollVertical = 450; await Wait(.2);
            var browsePosition = builder.Collection.ScrollVertical;
            var visibleCard = Walk(builder.Collection).OfType<DeckFace>().First(f => f.IsVisibleInTree() && f.Card.Id != target.Id).Card;
            Check(browsePosition > 0, "builder catalogue is scrolled before ownership edit");
            Click(builder.Browser.EditCollectionButton); await Wait(.2); editor = builder.GetChildren().OfType<MyCollectionEditor>().Single();
            MyCollection.Store.Set(target.Id, 2); MyCollection.Store.Set(visibleCard.Id, 17); await Wait(.2); Click(Walk(editor).OfType<Button>().Single(b => b.Text == "Done")); await Wait(.2);
            Check(builder.Collection.ScrollVertical == browsePosition, "ownership editing preserves unchanged catalogue scroll position");
            Check(Walk(builder.Collection).OfType<Label>().Any(l => l.Text == "Owned: 17"), "ownership labels refresh without resetting the catalogue");
            Check(DeckFormat.Write(builder.Draft.ToDeck()) == draft && builder.Dirty == dirty, "collection editing preserves unsaved deck exactly");
            Check(builder.MissingCards()[0].Need == 1, "deck shortage updates immediately after collection edit");
            var targetCard = builder.Data.Cards.Single(c => c.Id == target.Id); builder.Adjust(targetCard, 1); await Wait(.2);
            Check(MyCollection.Store.Count(target.Id) == 2, "adding to deck never changes ownership");
            builder.Browser.SetOwnership(OwnershipFilter.Owned); await Wait(.2); Check(builder.Collection.Cards.All(c => MyCollection.Store.Count(c.Id) > 0), "Builder shares Owned filtering");
            builder.Browser.SetOwnership(OwnershipFilter.All); builder.Browser.OnlyClans("Crane"); builder.Browser.Types.Only("Personality"); builder.Browser.Refresh();
            await Wait(1); await Capture("deck-builder");
            Click(Walk(builder).OfType<Button>().Single(b => b.Text == "View missing cards")); await Wait(.3); await Capture("missing-list");
            KeyPress(Key.Escape); await Wait(.2);
            GetWindow().Size = new Vector2I(1280, 800); await Wait(.5); await Capture("deck-compact");
            Check(builder.Collection.GetGlobalRect().End.X <= 1280 && builder.Mat.GetGlobalRect().End.X <= 1280, "compact card grid and deck fit viewport");
            Click(builder.Browser.EditCollectionButton); await Wait(.3); editor = builder.GetChildren().OfType<MyCollectionEditor>().Single();
            Check(Walk(editor).OfType<Button>().Single(b => b.Text == "Done").GetGlobalRect().End.X <= 1280, "compact collection editor fits viewport");
            KeyPress(Key.Escape); await Wait(.2); Check(!builder.GetChildren().OfType<MyCollectionEditor>().Any(), "Escape closes collection editor without leaving draft");
            builder.QueueFree(); await Wait(.3);
            Check(new MyCollectionStore(MyCollection.OverridePath!).Count(target.Id) == 2, "shared collection persisted independently of decks");
            GD.Print($"COLLECTION NATIVE PASS: {_checks} assertions"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task CheckReviewIsolation()
    {
        var ordinary = ProjectSettings.GlobalizePath("user://my-collection.json");
        var before = File.Exists(ordinary) ? File.ReadAllBytes(ordinary) : null;
        MyCollection.OverridePath = null;
        var review = new CatalogueControlsReview(); AddChild(review); review.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await Wait(1);
        Check(MyCollection.OverridePath is { } path && path != ordinary && path.Contains("review-isolation-"), "prepared review uses its isolated collection path");
        var browser = Walk(review).OfType<CatalogueBrowserBar>().Single();
        Click(browser.EditCollectionButton); await Wait(.2);
        var editor = Walk(review).OfType<MyCollectionEditor>().Single();
        var card = Walk(editor).OfType<DeckFace>().First().Card;
        Click(Walk(editor).OfType<Button>().First(b => b.AccessibilityName == "Increase owned copies of " + card.Name)); await Wait(.2);
        Check(new MyCollectionStore(MyCollection.OverridePath!).Count(card.Id) == MyCollection.Store.Count(card.Id) && MyCollection.Store.Count(card.Id) > 0, "native review editor writes only its prepared inventory");
        var after = File.Exists(ordinary) ? File.ReadAllBytes(ordinary) : null;
        Check(before is null ? after is null : after is not null && before.SequenceEqual(after), "prepared review preserves ordinary collection bytes");
        GD.Print($"REVIEW ISOLATION PASS: {_checks} assertions");
    }
    private void Check(bool pass, string name) { if (!pass) throw new Exception(name); _checks++; GD.Print("PASS " + name); }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name) { await Wait(.1); RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_out, name + ".png")); }
    private static IEnumerable<Node> Walk(Node node) { foreach (var c in node.GetChildren()) { yield return c; foreach (var n in Walk(c)) yield return n; } }
    private static void Click(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }); }
    private static void KeyPress(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false }); }
}
