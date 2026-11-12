using Godot;
using L5R.AI;
using L5R.Core;
namespace L5R.Client;

/// <summary>Exercises native deck selection and start, with isolated local deck files.</summary>
public partial class PracticeSetupProbe : Node
{
    private PracticeSetupScreen _screen = null!;
    private string _out = "";
    private bool _started;
    public override async void _Ready()
    {
        try
        {
            _out = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--out="))?[6..] ?? ProjectSettings.GlobalizePath("res://../../artifacts/practice-checks"); Directory.CreateDirectory(_out);
            var storage = Path.Combine(_out, "fixtures-" + Guid.NewGuid().ToString("N")); var store = new DeckFileStore(storage);
            var crane = CustomPractice.Preset("Crane"); var path = store.Save("Crane — Court and Steel", crane);
            store.Save("Lion — Honour and Steel", CustomPractice.Preset("Lion"));
            var draft = CustomPractice.Workshop(); draft.Load(DeckFormat.Write(crane)); draft.Adjust(draft.Cards.First(c => c.SetupSlot == "" && c.Families.Contains("Dynasty")).Id, -2); store.Save("Crane — Work in progress", draft.ToDeck());
            store.Save("A very long deck name for checking text wrapping in the match preview", crane);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(10));
            _screen = new PracticeSetupScreen { StorageDirectory = storage }; AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            using var session = new MatchSession { LocalDisplayName = "Practice reviewer" };
            _screen.StartMatch = (a, b) => { session.StartCustomPractice(a, b); _started = true; };
            await Wait(2); await Capture("A-presets");
            Check(!_screen.StartButton.Disabled, "presets ready before start"); Click(Button("Change deck")); await Wait(1); await Capture("B-browser");
            Click(Walk(_screen).OfType<Button>().First(b => b.AccessibilityName.ToString().StartsWith("Crane — Court and Steel ·"))); await Wait(.5);
            await Capture("B-selected"); Check(!_started && session.Snapshot is null, "browsing does not start or replace match"); KeyPress(Key.Escape); await Wait(.5); await Capture("A-saved-deck");
            Click(Walk(_screen).OfType<Button>().First(b => b.TooltipText.StartsWith("Inspect "))); await Wait(.3); Check(Walk(_screen).OfType<AcceptDialog>().Any(d => d.Visible), "Stronghold inspection opens full original face"); await Capture("stronghold-inspection"); KeyPress(Key.Escape); await Wait(.3);
            Click(Button("Change deck")); await Wait(.3); Search("Work in progress"); await Wait(.5); await Capture("B-search");
            Click(Walk(_screen).OfType<Button>().First(b => b.AccessibilityName.ToString().StartsWith("Crane — Work in progress ·"))); await Wait(.3); KeyPress(Key.Escape); await Wait(.3);
            Check(_screen.StartButton.Disabled, "incomplete draft cannot start"); await Capture("C-inline-draft");
            Click(Button("Edit deck")); await Wait(2); Check(Walk(GetTree().Root).OfType<DeckBuilderScreen>().Single().Page == "editor", "edit opens the selected draft in existing builder"); await Capture("edit-draft");
            var builder = Walk(GetTree().Root).OfType<DeckBuilderScreen>().Single(); builder.OpenPracticeDeck(crane, "Crane — Repaired deck"); builder.SaveDraft(); await Wait(.2); builder.ShowDecks(); await Wait(.2);
            Click(Walk(builder).OfType<Button>().Single(b => b.Text == "Back to menu")); await Wait(.5); Check(!_screen.StartButton.Disabled && _screen.Choices[0].Name == "Crane — Repaired deck", "saved repair selected on return");
            GetWindow().Size = new Vector2I(1280, 720); await Wait(.5); await Capture("A-1280"); Click(Button("Change deck")); await Wait(.3); await Capture("B-1280");
            Search("nonesuch"); await Wait(.3); Check(Walk(_screen).OfType<Label>().Any(l => l.Text == "No matching decks."), "empty filtered list has feedback"); Search(""); await Wait(.3); KeyPress(Key.Escape); await Wait(.3);
            GetWindow().Size = new Vector2I(1672, 941); await Wait(.4);
            // A selected file can be removed or changed outside the application.
            File.Delete(_screen.Choices[0].Path!); Click(_screen.StartButton); await Wait(.3); Check(!_started && _screen.StartButton.Disabled, "deleted selected file is revalidated before launch"); await Capture("C-missing-file");
            _screen.Browse(0); _screen.Select(PracticeSetupScreen.Preset("Crane")); _screen.ShowSetup(); await Wait(.3);
            // Issue #1214: the opponent's difficulty starts Easy (the Champion, as before), is chosen on the setup page with three buttons, and says what each level is.
            Check(_screen.Difficulty == BotLevel.Easy && Enumerable.Range(0, _screen.DifficultyMenu.ItemCount).Select(i => _screen.DifficultyMenu.GetItemText(i)).SequenceEqual(["Easy", "Moderate", "Hard"]), "one drop-down offers Easy, Moderate and Hard, Easy first");
            await Capture("A-closed"); Check(!Walk(_screen).OfType<Label>().Any(l => l.Text.Contains("Champion") || l.Text.Contains("Contender") || l.Text.Contains("Conan")), "no internal bot name is shown");
            Click(_screen.DifficultyMenu); await Wait(.5); await Capture("A-dropdown"); Check(_screen.DifficultyMenu.GetPopup().Visible || _screen.DifficultyMenu.ButtonPressed, "clicking the drop-down opens its list");
            _screen.DifficultyMenu.GetPopup().Hide(); _screen.DifficultyMenu.Select(2); _screen.DifficultyMenu.EmitSignal(OptionButton.SignalName.ItemSelected, 2); await Wait(.3); Check(_screen.Difficulty == BotLevel.Hard, "Hard chosen from the list"); await Capture("A-hard");
            Click(_screen.StartButton); await Wait(.3);
            Check(_started && session.Snapshot?.View.Scenario == CustomPractice.Scenario, "native Start creates selected custom match"); Check(session.Snapshot!.View.ViewerId == 0 && session.Snapshot.View.Players[0].Name == "Practice reviewer", "player seat and name preserved");
            // The last choices are remembered from session to session: a preset for one side, a saved deck for the other; a deck that has since gone falls back to the default.
            _screen.Browse(0); _screen.Select(PracticeSetupScreen.Preset("Lion"));
            _screen.Browse(1); _screen.Select(new PracticeChoice("Crane — Court and Steel", crane, CustomPractice.Review(crane), path));
            _screen.QueueFree(); await Wait(.2);
            _screen = new PracticeSetupScreen { StorageDirectory = storage }; AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); await Wait(.5);
            Check(_screen.Choices[0].Name.StartsWith("Lion") && _screen.Choices[1].Path == path, "the last deck choices come back in a new session");
            Check(_screen.Difficulty == BotLevel.Hard, "the chosen difficulty comes back in a new session");
            _screen.QueueFree(); await Wait(.2); File.Delete(path);
            _screen = new PracticeSetupScreen { StorageDirectory = storage }; AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); await Wait(.5);
            Check(_screen.Choices[0].Name.StartsWith("Lion") && _screen.Choices[1].Name.StartsWith("Lion") && _screen.Choices[1].Path is null, "a remembered deck that no longer exists falls back to that side's default");
            _screen.QueueFree(); await Wait(.2);
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate(); AddChild(main); await Wait(2);
            var menu = Walk(main).OfType<TitleMenu>().Single(); Click(Walk(menu).OfType<Button>().Single(b => b.Text == "PLAY")); await Wait(.3); Click(Walk(menu).OfType<Button>().Single(b => b.Text == "PRACTICE")); await Wait(1);
            var actual = Walk(main).OfType<PracticeSetupScreen>().Single(); Check(actual.IsVisibleInTree(), "main menu enters Practice setup"); await Capture("entry-from-game");
            Click(actual.StartButton); await Wait(4); Check(!Walk(main).OfType<PracticeSetupScreen>().Any(), "main starts and closes setup"); await Capture("custom-in-game");
            GD.Print("PRACTICE SETUP PASS: native selection, search, inline validation, edit/save return, resize, missing file, start, seat/name and full-game entry, remembered deck choices");
            if (!OS.GetCmdlineUserArgs().Contains("--stay")) GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name) { await Wait(.15); RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_out, name + ".png")); GD.Print("CAPTURE " + name); }
    private static IEnumerable<Node> Walk(Node root) { foreach (var child in root.GetChildren()) { yield return child; foreach (var n in Walk(child)) yield return n; } }
    private Button Button(string text) => Walk(_screen).OfType<Button>().First(b => b.Text == text && b.IsVisibleInTree());
    private static void Click(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }); }
    private void Search(string text) { _screen.Search.GrabFocus(); _screen.Search.SelectAll(); KeyPress(Key.Backspace); foreach (var ch in text) { Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = true }); Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = false }); } }
    private static void KeyPress(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false }); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); GD.Print("PASS " + message); }
}
