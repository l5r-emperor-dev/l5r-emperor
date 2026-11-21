using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class DeckBuilderScreen
{
    public void ShowDecks()
    {
        Page = "decks"; HideHover(); _overlay.Hide(); Browser.CloseMenus(); Browser.Hide();
        foreach (var c in new Control[] { _setupStrip, _left, _toolbar, _deckFooter, _setupTools, _viewStrip, Collection, Mat, _status, _count, _empty, _name, _save, _dirtyLabel, _header.GetNode<Control>("Copy") }) c.Hide();
        _header.Hide(); _library.Show(); BuildDeckList(); _lines.QueueRedraw();
    }
    private void BuildDeckList()
    {
        Palette.Clear(_library);
        string[] files; try { files = Store.Files(); } catch (Exception e) { Notice("Could not open the local deck folder: " + e.Message); files = []; }
        void Folder() { Directory.CreateDirectory(Store.DirectoryPath); OS.ShellOpen(Store.DirectoryPath); }
        if (files.Length == 0)
        {
            Put(_library, new DeckLibraryLanding(NewDeck, ImportDeck, Folder, () => Closed?.Invoke()), 0, 0, _library.Size.X, _library.Size.Y); return;
        }
        Put(_library, CatalogueArt.Label("My decks", 38, true), 40, 20, 500, 55);
        Put(_library, CatalogueArt.Button("Import deck", ImportDeck), _library.Size.X - 345, 15, 150, 44);
        Put(_library, CatalogueArt.Button("New deck", NewDeck), _library.Size.X - 177, 15, 150, 44);
        var back = CatalogueArt.Button("‹  Back", () => Closed?.Invoke()); back.AccessibilityName = "Back to menu"; Put(_library, back, 20, 72, 130, 44); // issue #1270: Back near the top, as on the Card Library and Practice pages
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(_library, scroll, 20, 128, _library.Size.X - 40, _library.Size.Y - 188);
        var grid = new GridContainer { Columns = Math.Max(1, (int)((_library.Size.X - 60) / 480)), SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 22); grid.AddThemeConstantOverride("v_separation", 22); scroll.AddChild(grid);
        foreach (var path in files)
        {
            var item = new PanelContainer { CustomMinimumSize = new Vector2(450, 285), SizeFlagsHorizontal = SizeFlags.ExpandFill }; item.AddThemeStyleboxOverride("panel", new DeckPanelStyle()); grid.AddChild(item);
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12); item.AddChild(box); var name = Path.GetFileNameWithoutExtension(path); box.AddChild(CatalogueArt.Label(name, 26));
            try
            {
                var draft = NewDraft(); draft.Load(DeckFileStore.Read(path)); var review = draft.Review();
                var hero = new HBoxContainer(); hero.AddThemeConstantOverride("separation", 20); box.AddChild(hero);
                var info = draft.Cards.FirstOrDefault(c => c.SetupSlot == "Stronghold"); if (info is not null) hero.AddChild(new DeckThumbnail(_byId[info.Id], Scans) { CustomMinimumSize = new Vector2(98, 92) });
                var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; hero.AddChild(details); details.AddChild(CatalogueArt.Label($"Dynasty {review.Dynasty}  ·  Fate {review.Fate}", 22)); details.AddChild(CatalogueArt.Label(review.ChecksPassed ? "Basic checks passed" : "Draft · needs review", 18)); details.AddChild(CatalogueArt.Label(File.GetLastWriteTime(path).ToString("g"), 15));
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); box.AddChild(row);
                row.AddChild(CatalogueArt.Button("Open in builder", () => OpenFile(path))); row.AddChild(CatalogueArt.Button("Duplicate", () => { if (!OpenFile(path)) return; _name.Text = name + " copy"; _currentPath = null; Dirty = true; UpdateStatus(); }));
                row.AddChild(CatalogueArt.Button("Export", () => ExportFile(draft.ToDeck(), name)));
                row.AddChild(CatalogueArt.Button("Delete Deck", () => ConfirmDelete(path, name)));
            }
            catch (Exception e) { box.AddChild(Palette.Wrapped("Needs repair: " + e.Message, 18, new Color("e8a896"))); var repair = new HBoxContainer(); repair.AddThemeConstantOverride("separation", 12); box.AddChild(repair); repair.AddChild(CatalogueArt.Button("Troubleshoot", () => { if (!TryTroubleshoot(path)) Notice("Nothing to suggest: " + e.Message); })); repair.AddChild(CatalogueArt.Button("Open folder", () => OS.ShellOpen(Store.DirectoryPath))); repair.AddChild(CatalogueArt.Button("Delete Deck", () => ConfirmDelete(path, Path.GetFileNameWithoutExtension(path)))); }
        }
        Put(_library, CatalogueArt.Button("Open deck folder", () => { Directory.CreateDirectory(Store.DirectoryPath); OS.ShellOpen(Store.DirectoryPath); }), 20, _library.Size.Y - 44, 220, 38);
        Put(_library, CatalogueArt.Button("Restore shipped decks", ConfirmRestoreShipped), 250, _library.Size.Y - 44, 260, 38);
        var stored = CatalogueArt.Label("Saved on this device · plain-text files you can share", 19); stored.HorizontalAlignment = HorizontalAlignment.Right; stored.VerticalAlignment = VerticalAlignment.Center;
        Put(_library, stored, 530, _library.Size.Y - 44, _library.Size.X - 600, 38); // clear of the frame's corner ornament
    }
    // The shipped decks also exist as read-only copies inside the game (and the tutorial plays only those); this puts a missing or edited one back in the deck folder.
    private void ConfirmRestoreShipped()
    {
        string[] names; try { names = PreloadedDecks.Restorable(Store); } catch (Exception e) { Notice("Could not check the shipped decks: " + e.Message); return; }
        if (names.Length == 0) { Notice("Every shipped deck is already in the deck folder, unchanged."); return; }
        var dialog = new ConfirmationDialog { Title = "Restore shipped decks?", DialogText = $"Put back the original {names.Length} shipped deck(s): {string.Join(", ", names)}.\n\nA changed deck is replaced by the original. Save your edits under another name first if you want to keep them.", Theme = Theme }; AddChild(dialog); dialog.GetOkButton().Text = "Restore";
        dialog.Confirmed += () => { dialog.QueueFree(); try { PreloadedDecks.Restore(Store, names); BuildDeckList(); } catch (Exception e) { Notice("Could not restore the shipped decks: " + e.Message); } };
        dialog.Canceled += dialog.QueueFree; dialog.PopupCentered();
    }
    public string? SavedPath => _currentPath;
    public void OpenPracticeDeck(Deck deck, string name)
    {
        var incoming = NewDraft(); incoming.Load(DeckFormat.Write(deck)); Draft = incoming;
        _currentPath = null; _savedFingerprint = ""; _savedName = ""; _compare.Clear();
        _building = true; _name.Text = name; _building = false; Dirty = true;
        var stronghold = incoming.Cards.FirstOrDefault(c => c.SetupSlot == "Stronghold");
        if (stronghold is not null) _setupClan = _byId[stronghold.Id].Clans.FirstOrDefault() ?? "Unaligned";
        EnterEditor();
    }
    public void NewDeck()
    {
        Draft = NewDraft(); foreach (var id in new[] { "oracle-l5r-1106", "oracle-l5r-679" }) if (_byId.TryGetValue(id, out var c)) Draft.SelectSetup(c.Id);
        _currentPath = null; _savedFingerprint = ""; _savedName = ""; _compare.Clear(); _building = true; _name.Text = "Untitled deck"; _building = false; Dirty = true;
        EnterEditor(); SetSetup("Stronghold");
    }
    private void EnterEditor()
    {
        Page = "editor"; _deckOnly = false; _header.Show(); _library.Hide(); foreach (var c in new Control[] { _setupStrip, _left, _toolbar, _deckFooter, Collection, Mat, _status, _count, _name, _save, _dirtyLabel, _header.GetNode<Control>("Copy") }) c.Show();
        BuildSetupStrip(); SetSetup(""); Fit(); UpdateStatus();
    }
    public bool OpenFile(string path)
    {
        try
        {
            var text = DeckFileStore.Read(path); var incoming = NewDraft(); incoming.Load(text);
            var name = Path.GetFileNameWithoutExtension(path);
            Draft = incoming; _compare.Clear(); _building = true; _name.Text = name; _building = false;
            var local = Path.GetDirectoryName(Path.GetFullPath(path)) == Store.DirectoryPath;
            _currentPath = local ? path : null; _savedFingerprint = local ? DeckFileStore.Fingerprint(path) : ""; _savedName = name; Dirty = !local;
            var stronghold = incoming.Cards.FirstOrDefault(c => c.SetupSlot == "Stronghold"); if (stronghold is not null) _setupClan = _byId[stronghold.Id].Clans.FirstOrDefault() ?? "Unaligned";
            EnterEditor(); return true;
        }
        catch (Exception e) { if (!TryTroubleshoot(path)) Notice("Could not load deck. Your open draft has not changed.\n\n" + e.Message); return false; }
    }
    private void ImportDeck()
    {
        var dialog = new FileDialog { Title = "Import Emperor deck", FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, UseNativeDialog = true, Filters = ["*.txt ; Deck text files"] }; AddChild(dialog);
        dialog.FileSelected += path => { dialog.QueueFree(); Guard(() => OpenFile(path)); }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(900, 650));
    }
    public void SaveDraft(Action? after = null)
    {
        try
        {
            var path = Store.PathFor(_name.Text);
            var exists = File.Exists(path); var same = path == _currentPath;
            var changed = same && exists && DeckFileStore.Fingerprint(path) != _savedFingerprint;
            void Save()
            {
                try { _currentPath = Store.Save(_name.Text, Draft.ToDeck(), exists); _savedFingerprint = DeckFileStore.Fingerprint(_currentPath); _savedName = _name.Text; Dirty = false; UpdateStatus(); _dirtyLabel.Text = "Saved just now"; after?.Invoke(); }
                catch (Exception e) { Notice("Could not save the deck. Your draft is still open.\n\n" + e.Message); }
            }
            if (exists && (!same || changed)) ConfirmOverwrite(changed ? "This deck file changed outside the game. Replace it with your open draft?" : "A deck with this name already exists. Replace it?", Save);
            else Save();
        }
        catch (Exception e) { Notice(e.Message); }
    }
    private void SaveCopy()
    {
        var dialog = new ConfirmationDialog { Title = "Save a copy", Theme = Theme, DialogText = "Choose a new name for this draft." }; AddChild(dialog);
        var field = new LineEdit { Text = _name.Text + " copy", CustomMinimumSize = new Vector2(450, 44), MaxLength = 80 }; dialog.AddChild(field); field.Position = new Vector2(12, 64); dialog.GetOkButton().Text = "Save";
        dialog.Confirmed += () => { _name.Text = field.Text; SaveDraft(); dialog.QueueFree(); }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(500, 180)); field.GrabFocus(); field.SelectAll();
    }
    private void ExportFile(Deck deck, string name)
    {
        var dialog = new FileDialog { Title = "Export deck", FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Filesystem, UseNativeDialog = true, CurrentFile = name + ".txt", Filters = ["*.txt ; Deck text files"] }; AddChild(dialog);
        dialog.FileSelected += path =>
        {
            try { var dir = Path.GetDirectoryName(path)!; var store = new DeckFileStore(dir); store.Save(Path.GetFileNameWithoutExtension(path), deck, true); Notice("Deck exported."); }
            catch (Exception e) { Notice("Could not export deck: " + e.Message); }
            dialog.QueueFree();
        }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(900, 650));
    }
    private void ConfirmDelete(string path, string name)
    {
        var dialog = new ConfirmationDialog { Title = "Delete deck?", DialogText = $"Delete \"{name}\"? The deck file is removed from this device and this cannot be undone.", Theme = Theme }; AddChild(dialog); dialog.GetOkButton().Text = "Delete";
        dialog.Confirmed += () =>
        {
            dialog.QueueFree();
            try { Store.Delete(path); if (_currentPath == path) { _currentPath = null; _savedFingerprint = ""; } BuildDeckList(); }
            catch (Exception e) { Notice("Could not delete the deck: " + e.Message); }
        }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(570, 170));
    }
    private void ConfirmOverwrite(string message, Action yes)
    {
        var dialog = new ConfirmationDialog { Title = "Replace saved deck?", DialogText = message, Theme = Theme }; AddChild(dialog); dialog.GetOkButton().Text = "Replace"; dialog.Confirmed += () => { dialog.QueueFree(); yes(); }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(570, 170));
    }
    private void Guard(Action next)
    {
        HideHover(); if (!Dirty || Page == "decks") { next(); return; }
        var dialog = new ConfirmationDialog { Title = "Unsaved deck", DialogText = "Save your changes before leaving this draft?", Theme = Theme }; AddChild(dialog); dialog.GetOkButton().Text = "Save"; dialog.AddButton("Discard changes", true, "discard");
        dialog.Confirmed += () => { dialog.QueueFree(); SaveDraft(next); }; dialog.CustomAction += action => { if (action == "discard") { Dirty = false; dialog.QueueFree(); next(); } }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(570, 180));
    }
}
