using Godot;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private int _menuProbeStep;
    private long _menuProbeVersion;
    private void MenuAssert(bool condition, string message) { if (!condition) throw new InvalidOperationException("MENU_CHECK: " + message); GD.Print("MENU_CHECK: " + message); }
    private static void Click(Button button) => button.EmitSignal(BaseButton.SignalName.Pressed);
    private void ProcessMenuCapture(double delta)
    {
        _captureClock += delta;
        if (_captureClock < .7) return;
        var menu = _titleMenu!;
        if (_menuProbeStep == 0)
        {
            MenuAssert(_session.Snapshot is null && MenuVisible, "startup has no match or hidden opponent turns");
            MenuAssert(Mathf.IsEqualApprox(_preferences.LanternStrength, .85f), "new menu defaults to approved 85 percent lighting");
            if (_captureMode == "menu-options") menu.ShowOptions();
            if (_captureMode == "menu-play") menu.ShowPlay();
            if (_captureMode == "menu-tutorial") menu.ShowTutorialTracks();
            if (_captureMode == "menu-tutorial-track") menu.ShowTutorialTrack(L5R.Core.TutorialCurriculum.Tracks[1]);
            if (_captureMode == "menu-selection") menu.Entries.GetChild<Button>(4).GrabFocus();
            if (_captureMode is "menu-name" or "menu-name-input") menu.ShowName(1);
            if (_captureMode == "menu-library") LibraryDialog.Show(this);
            if (_captureMode == "menu-private")
            {
                _enteringPrivate = true; ShowOnline();
                var dialog = GetChildren().OfType<AcceptDialog>().Last();
                var body = dialog.GetChildren().OfType<VBoxContainer>().Single();
                body.GetChildren().OfType<LineEdit>().First().Text = "invalid-server";
                Click(body.GetChildren().OfType<Button>().First(b => b.Text == "Enter private match"));
                MenuAssert(MenuVisible && _session.Snapshot is null, "invalid private server keeps the player in the menu without starting practice");
            }
            if (_captureMode.Contains("reduced")) { _preferences.ReducedMotion = true; ApplyPreferences(false); }
            if (_captureMode.EndsWith("later", StringComparison.Ordinal)) menu.ReviewTime = 22;
            else menu.ReviewTime = 0;
            _menuProbeStep++;
        }
        if (_captureMode == "menu-film")
        {
            if (_filmFrames >= 80) { GetTree().Quit(); return; }
            Directory.CreateDirectory(_capturePath!);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_capturePath!, $"{_filmFrames:0000}.png"));
            menu.ReviewTime = ++_filmFrames / 8f;
            return;
        }
        if (_captureMode == "menu-input")
        {
            void KeyEvent(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); }
            void MouseClick(Control control) { var point = control.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }); Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); }
            if (_menuProbeStep == 1) { KeyEvent(Key.Up); _menuProbeStep = 20; _captureClock = 0; return; }
            if (_menuProbeStep == 20) { MenuAssert(menu.SelectedEntry == 5 && menu.Entries.GetChild<Button>(5).HasFocus(), "Up wraps to Quit and moves the visible selection"); KeyEvent(Key.Down); _menuProbeStep = 21; _captureClock = 0; return; }
            if (_menuProbeStep == 21) { MenuAssert(menu.SelectedEntry == 0, "Down wraps back to Play"); KeyEvent(Key.Enter); _menuProbeStep = 2; _captureClock = 0; return; }
            if (_menuProbeStep == 2) { MenuAssert(menu.Page == "play", "native Enter activates focused Play"); KeyEvent(Key.Escape); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 3) { MenuAssert(menu.Page == "main", $"native Escape returns from clan selection (page={menu.Page}, focus={GetViewport().GuiGetFocusOwner()?.GetPath()})"); MouseClick(menu.Entries.GetChildren().OfType<Button>().Single(b => b.Text == "OPTIONS")); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 4) { MenuAssert(menu.Page == "options", "native mouse hits Options at rendered coordinates"); MouseClick(menu.Entries.GetChildren().OfType<CheckBox>().First(c => c.Text.StartsWith("Reduced motion", StringComparison.Ordinal))); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 5) { MenuAssert(_reducedMotion, "native mouse toggles reduced motion"); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_preferences.Path)); KeyEvent(Key.F11); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 6) { MenuAssert(IsFullscreen, "native F11 enters fullscreen"); KeyEvent(Key.F11); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 7) { MenuAssert(!IsFullscreen, "native F11 returns to windowed play"); KeyEvent(Key.Escape); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 8) { MenuAssert(menu.Page == "main", "Options returns to main menu"); KeyEvent(Key.Escape); _menuProbeStep++; _captureClock = 0; return; }
            if (_menuProbeStep == 9) { MenuAssert(menu.Page == "quit", "quit requires confirmation"); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_preferences.Path)); _menuProbeStep++; }
        }
        if (_captureMode == "menu-tutorial-scroll")
        {
            void Motion(Vector2 at) => Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            void Button(Vector2 at, MouseButton button, bool pressed) => Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = pressed, Factor = 1 });
            var scroll = menu.TrackScroll;
            if (_menuProbeStep == 1) { menu.ShowTutorialTracks(); Click(menu.Entries.GetChildren().OfType<Button>().Single(b => b.Text == "LEVEL UP YOUR PLAY")); _menuProbeStep = 30; _captureClock = 0; return; }
            if (_menuProbeStep == 30)
            {
                scroll = menu.TrackScroll!; var bar = scroll.GetVScrollBar();
                MenuAssert(menu.Page == "tutorial-track" && bar.MaxValue > bar.Page && scroll.ScrollVertical == 0, "Level Up Your Play opens a list taller than its view, at the top");
                var at = scroll.GetGlobalRect().GetCenter(); Motion(at); Button(at, MouseButton.WheelDown, true); Button(at, MouseButton.WheelDown, false);
                _menuProbeStep = 31; _captureClock = 0; return;
            }
            if (_menuProbeStep == 31)
            {
                scroll = menu.TrackScroll!; var bar = scroll.GetVScrollBar();
                MenuAssert(scroll.ScrollVertical > 0, $"the mouse wheel scrolls the list (now {scroll.ScrollVertical})");
                var before = scroll.ScrollVertical; _menuProbeVersion = before;
                var rect = bar.GetGlobalRect(); var grabberHeight = rect.Size.Y * (float)(bar.Page / bar.MaxValue); var grabberY = rect.Position.Y + (rect.Size.Y - grabberHeight) * (float)(bar.Value / (bar.MaxValue - bar.Page)) + grabberHeight / 2; var x = rect.GetCenter().X;
                Motion(new Vector2(x, grabberY)); Button(new Vector2(x, grabberY), MouseButton.Left, true); Motion(new Vector2(x, grabberY + 60)); Button(new Vector2(x, grabberY + 60), MouseButton.Left, false);
                _menuProbeStep = 32; _captureClock = 0; return;
            }
            if (_menuProbeStep == 32)
            {
                scroll = menu.TrackScroll!;
                MenuAssert(scroll.ScrollVertical > _menuProbeVersion, $"dragging the scroll bar scrolls the list ({_menuProbeVersion} to {scroll.ScrollVertical})");
                MenuAssert(menu.Entries.GetChildren().OfType<Button>().Count() == L5R.Core.TutorialCurriculum.Tracks[1].Entries.Length, "every lesson of the track is in the list");
                _menuProbeStep = 33; _captureClock = 0; return;
            }
        }
        if (_captureMode == "menu-tutorial-direct")
        {
            if (_menuProbeStep == 1) { menu.ShowTutorialTracks(); Click(menu.Entries.GetChildren().OfType<Button>().Single(b => b.Text == "PLAYING THE GAME")); _menuProbeStep = 30; _captureClock = 0; return; }
            if (_menuProbeStep == 30) { MenuAssert(!MenuVisible && _session.Snapshot is not null, "Playing the Game starts the Basics lesson directly, with no list between"); _menuProbeStep = 31; }
        }
        if (_captureMode == "menu-name-input")
        {
            if (_menuProbeStep == 1)
            {
                var field = menu.Entries.GetChildren().OfType<LineEdit>().Single(); field.Text = "Mendon"; field.GrabFocus();
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true }, true);
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false }, true);
                _menuProbeStep++; _captureClock = 0; return;
            }
            if (_menuProbeStep == 2)
            {
                MenuAssert(!MenuVisible && Self.Name == "Mendon", "native Enter submits the local name and enters the chosen deck");
                ShowMainMenu(); menu.ShowName(1);
                MenuAssert(menu.Entries.GetChildren().OfType<LineEdit>().Single().Text == "Mendon", "next name entry is prefilled");
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_preferences.Path));
                _menuProbeStep++;
            }
        }
        if (_captureMode == "menu-overlays")
        {
            void Escape() { Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false }); }
            if (_menuProbeStep == 1) { menu.Entries.GetChild<Button>(2).GrabFocus(); Click(menu.Entries.GetChild<Button>(2)); _menuProbeStep = 2; _captureClock = 0; return; }
            if (_menuProbeStep == 2) { MenuAssert(GetTree().Root.GetNodeOrNull("CatalogueLayer") is not null, "Library entry opens the live catalogue"); Escape(); _menuProbeStep = 3; _captureClock = 0; return; }
            if (_menuProbeStep == 3) { MenuAssert(GetTree().Root.GetNodeOrNull("CatalogueLayer") is null && menu.Page == "main", "Library Escape closes the overlay without opening Quit behind it"); MenuAssert(menu.Entries.GetChild<Button>(2).HasFocus(), "Library returns keyboard focus to its menu entry"); menu.Entries.GetChild<Button>(3).GrabFocus(); Click(menu.Entries.GetChild<Button>(3)); _menuProbeStep = 4; _captureClock = 0; return; }
            if (_menuProbeStep == 4) { MenuAssert(GetTree().Root.GetNodeOrNull("DeckBuilderLayer") is not null, "Deck Builder entry opens the live workshop"); Escape(); _menuProbeStep = 5; _captureClock = 0; return; }
            if (_menuProbeStep == 5) { MenuAssert(GetTree().Root.GetNodeOrNull("DeckBuilderLayer") is null && menu.Page == "main", "Deck Builder Escape returns to the unchanged menu"); MenuAssert(menu.Entries.GetChild<Button>(3).HasFocus(), "Deck Builder returns keyboard focus to its menu entry"); _menuProbeStep = 6; }
        }
        if (_captureMode == "menu-flow")
        {
            if (_menuProbeStep == 1)
            {
                Click(menu.Entries.GetChild<Button>(0)); MenuAssert(menu.Page == "play", "Play opens match choices");
                Click(menu.Entries.GetChild<Button>(0)); MenuAssert(PracticeVisible && _session.Snapshot is null, "Practice setup opens before creating a match");
                var setup = _practiceLayer!.GetChildren().OfType<PracticeSetupScreen>().Single();
                setup.Browse(0); setup.Select(PracticeSetupScreen.Preset("Lion")); setup.Browse(1); setup.Select(PracticeSetupScreen.Preset("Crane")); setup.ShowSetup();
                _preferences.DisplayName = "Alfredo"; _preferences.Save(); ApplyPreferences(false); Click(setup.StartButton);
                MenuAssert(!MenuVisible && !PracticeVisible && Self.Clan == "Lion" && Self.Name == "Alfredo", "chosen decks and saved display name start custom Practice");
                _session.SwitchPracticeSeat(); MenuAssert(View.Players.Single(p => p.Clan == "Lion").Name == "Alfredo" && Self.Name != "Alfredo", "passing device does not move name to opponent"); _session.SwitchPracticeSeat();
                _menuProbeVersion = View.Version; ShowMainMenu(); _captureClock = 0; _menuProbeStep++; return;
            }
            if (_menuProbeStep == 2)
            {
                MenuAssert(View.Version == _menuProbeVersion, "practice stays paused behind main menu");
                menu.ShowPlay(); Click(menu.Entries.GetChild<Button>(0)); MenuAssert(!MenuVisible && View.Version == _menuProbeVersion, "return resumes unchanged practice");
                ShowMainMenu(); menu.ShowPlay(); Click(menu.Entries.GetChild<Button>(1)); MenuAssert(PracticeVisible, "new Practice opens selection without replacing active match");
                var setup = _practiceLayer!.GetChildren().OfType<PracticeSetupScreen>().Single();
                var expected = View.Version; Click(setup.StartButton); var confirm = GetChildren().OfType<ConfirmationDialog>().Single(d => d.Visible);
                MenuAssert(confirm.Visible && View.Version == expected, "replacing an existing match requires confirmation");
                confirm.EmitSignal(ConfirmationDialog.SignalName.Canceled); ClosePracticeSetup(); MenuAssert(View.Version == expected, "cancel replacement keeps current match");
                menu.ShowOptions();
                var motion = menu.Entries.GetChildren().OfType<CheckBox>().First(c => c.Text.StartsWith("Reduced motion", StringComparison.Ordinal)); motion.ButtonPressed = true;
                var paused = menu.Entries.GetChildren().OfType<CheckBox>().First(c => c.Text.StartsWith("Paused Animation", StringComparison.Ordinal));
                MenuAssert(!_preferences.PausedAnimation && paused.Text == "Paused Animation · Off", "Paused Animation defaults to Off");
                paused.ButtonPressed = true;
                MenuAssert(_preferences.PausedAnimation && paused.Text == "Paused Animation · On", "Paused Animation toggles On");
                var pausedLoaded = new MenuPreferences(_preferencesProfile); pausedLoaded.Load(); MenuAssert(pausedLoaded.PausedAnimation, "Paused Animation persists through a fresh settings instance");
                paused.ButtonPressed = false;
                pausedLoaded.Load(); MenuAssert(!pausedLoaded.PausedAnimation, "Paused Animation Off persists");
                var flip = menu.Entries.GetChildren().OfType<CheckBox>().First(c => c.Text.StartsWith("Flip Dynasty Cards", StringComparison.Ordinal));
                MenuAssert(!_preferences.FlipDynastyCards && flip.Text == "Flip Dynasty Cards · Off", "Flip Dynasty Cards defaults to Off");
                flip.ButtonPressed = true;
                pausedLoaded.Load(); MenuAssert(_preferences.FlipDynastyCards && pausedLoaded.FlipDynastyCards, "Flip Dynasty Cards toggles On and persists");
                flip.ButtonPressed = false;
                pausedLoaded.Load(); MenuAssert(!pausedLoaded.FlipDynastyCards, "Flip Dynasty Cards Off persists");
                MenuAssert(_reducedMotion && _preferences.ReducedMotion, "reduced motion applies to game and menu");
                var loaded = new MenuPreferences(_preferencesProfile); loaded.Load(); MenuAssert(loaded.ReducedMotion, "options persist through a fresh settings instance");
                var slider = menu.Entries.GetChildren().OfType<HSlider>().Single(); slider.Value = .4;
                loaded.Load();
                MenuAssert(Mathf.IsEqualApprox(loaded.LanternStrength, .4f), "lantern strength persists through the real Options slider");
                MenuAssert(Mathf.IsEqualApprox(loaded.Atmosphere, .65f), "saving new menu strength preserves the historical atmosphere preference");
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(loaded.Path));
                _menuProbeStep++;
            }
        }
        // Keep a stationary desktop pointer from replacing this keyboard-focus
        // fixture when a new native window appears underneath it.
        if (_captureMode == "menu-selection") menu.Entries.GetChild<Button>(4).GrabFocus();
        if (_captureClock < 1.3) return;
        var path = _capturePath!;
        if (menu.Page == "main")
        {
            foreach (var entry in menu.Entries.GetChildren().OfType<MenuEntry>())
            {
                MenuAssert(GetViewport().GetVisibleRect().Encloses(entry.GetGlobalRect()), "heading/menu entry fits viewport: " + entry.Text);
                MenuAssert(entry.SourceLettering, "main menu uses approved source lettering: " + entry.Text);
                MenuAssert(entry.Size.X <= 414 && entry.Size.Y <= 84, "menu hit area fits source lettering: " + entry.Text);
            }
        }
        GetViewport().GetTexture().GetImage().SavePng(path);
        File.WriteAllText(path + ".json", JsonSerializer.Serialize(new { Page = menu.Page, MenuVisible, MatchExists = _session.Snapshot is not null, Preferences = _preferences.ReducedMotion, LanternStrength = _preferences.LanternStrength, SelectedEntry = menu.SelectedEntry, Resolution = GetViewport().GetVisibleRect().Size.ToString() }));
        GD.Print("MENU_CAPTURE: " + path); GetTree().Quit();
    }
}
