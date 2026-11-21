using Godot;
namespace L5R.Client;

/// <summary>Live navigation over the approved B6 folding-screen plate; no game state lives here.</summary>
public partial class TitleMenu : Control
{
    public Action PracticeSetup = () => { };
    /// <summary>Starts the lesson with this id. The menu lists every lesson of the curriculum; one not built yet is a placeholder.</summary>
    public Action<string>? Tutorial;
    public Action<int> StartMatch = _ => { };
    public Action PrivateMatch = () => { }, Library = () => { }, DeckBuilder = () => { }, Resume = () => { }, PreferencesChanged = () => { };
    public Func<bool> CanResume = () => false;
    public Func<bool> IsOnline = () => false;
    public MenuPreferences Preferences = null!;
    public string Page { get; private set; } = "main";
    public ShaderMaterial AtmosphereMaterial { get; private set; } = null!;
    public Control Canvas { get; private set; } = null!;
    public VBoxContainer Entries { get; private set; } = null!;
    private Label _message = null!;
    /// <summary>The scrolling lesson list of a long tutorial track (null on every other page); its Back button stays fixed below it.</summary>
    public ScrollContainer? TrackScroll { get; private set; }
    private Button? _trackBack;
    private float _time, _emphasis = 1;
    public int SelectedEntry { get; private set; }
    public float? ReviewTime;
    public override void _Ready()
    {
        Theme = Palette.MakeTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(black); black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Canvas = new Control { Size = new Vector2(1672, 941) }; AddChild(Canvas);
        AtmosphereMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/lantern-study.gdshader") };
        AtmosphereMaterial.SetShaderParameter("clean_plate", GD.Load<Texture2D>("res://Art/lantern-study-clean.png"));
        Canvas.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/lantern-study.png"), Size = Canvas.Size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Material = AtmosphereMaterial, MouseFilter = MouseFilterEnum.Ignore });
        Entries = new VBoxContainer { Position = new Vector2(1094, 363), Size = new Vector2(380, 0) };
        Entries.AddThemeConstantOverride("separation", 14); Canvas.AddChild(Entries);
        _message = Palette.Wrapped("", 18, Palette.PaperDim); _message.Position = new Vector2(1056, 807); _message.Size = new Vector2(414, 54); _message.MouseFilter = MouseFilterEnum.Ignore; Canvas.AddChild(_message);
        Resized += Fit; Fit(); ShowMain();
    }
    private void Fit()
    {
        if (Canvas is null) return;
        var scale = Math.Min(Size.X / 1672f, Size.Y / 941f);
        Canvas.Scale = Vector2.One * scale;
        // Retina windows can round a requested odd height up by one pixel.
        // Avoid a half-pixel translation blurring the entire approved plate.
        Canvas.Position = ((Size - Canvas.Size * scale) / 2).Round();
    }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (!Preferences.ReducedMotion) _time += (float)delta;
        AtmosphereMaterial.SetShaderParameter("clock", ReviewTime ?? _time);
        AtmosphereMaterial.SetShaderParameter("strength", Preferences.LanternStrength);
        _emphasis = Preferences.ReducedMotion ? 1 : Mathf.Lerp(_emphasis, 1, 1 - Mathf.Exp(-(float)delta * 12));
        AtmosphereMaterial.SetShaderParameter("emphasis", _emphasis);
        AtmosphereMaterial.SetShaderParameter("motion", Preferences.ReducedMotion ? 0f : 1f);
    }
    private void Clear(string page)
    {
        if (TrackScroll is not null)
        {
            TrackScroll.RemoveChild(Entries); Canvas.AddChild(Entries);
            Canvas.RemoveChild(TrackScroll); TrackScroll.QueueFree(); TrackScroll = null;
            if (_trackBack is not null) { Canvas.RemoveChild(_trackBack); _trackBack.QueueFree(); _trackBack = null; }
        }
        Page = page; Palette.Clear(Entries);
        Entries.Position = page == "main" ? new Vector2(1094, 363) : new Vector2(1056, 351);
        Entries.Size = new Vector2(page == "main" ? 380 : 414, 0);
        Entries.CustomMinimumSize = Vector2.Zero;
        Entries.AddThemeConstantOverride("separation", page == "options" ? 6 : 8);
        AtmosphereMaterial.SetShaderParameter("submenu", page != "main");
        _message.Position = page == "main" ? new Vector2(1056, 862) : new Vector2(1056, 777);
        _message.Size = page.StartsWith("tutorial") ? new Vector2(414, 100) : new Vector2(414, 54);
        _message.Text = "";
    }
    /// <summary>Largest menu font size (from 52) at which every given entry fits: one size for a whole set of entries, so none looks smaller than its neighbours.</summary>
    private static int FitSize(IEnumerable<string> texts)
    {
        var fontSize = 52;
        foreach (var text in texts)
            while (fontSize > 20 && (Palette.Title.GetHeight(fontSize) > 62 || Palette.Title.GetStringSize(text.ToUpperInvariant(), fontSize: fontSize).X > 390)) fontSize--;
        return fontSize;
    }
    /// <summary>The one font size of every tutorial menu entry: the track list, each track's lessons and their Back.</summary>
    private static int TutorialFontSize => FitSize(Core.TutorialCurriculum.Tracks.Select(t => t.Title).Concat(Core.TutorialCurriculum.All.Select(e => e.Title)).Append("Back"));
    private Button Entry(string text, Action action, int fontSize = 52)
    {
        var button = new MenuEntry { Text = text.ToUpperInvariant(), ReducedMotion = () => Preferences.ReducedMotion, CustomMinimumSize = new Vector2(0, 62), Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = CursorShape.PointingHand };
        // This face has generous vertical metrics. Fit its full line box, not just
        // the glyph width, so five menu entries stay inside the artwork.
        while (fontSize > 20 && (Palette.Title.GetHeight(fontSize) > 62 || Palette.Title.GetStringSize(button.Text, fontSize: fontSize).X > 390)) fontSize--;
        button.AddThemeFontOverride("font", Palette.Title); button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 8 });
        button.AddThemeColorOverride("font_color", new Color("dfd1af"));
        button.AddThemeColorOverride("font_hover_color", new Color("ffe0a0"));
        button.AddThemeColorOverride("font_focus_color", new Color("f8d590"));
        button.Pressed += action; Entries.AddChild(button); return button;
    }
    private void FocusFirst() { if (Entries.GetChildren().OfType<BaseButton>().FirstOrDefault() is Control c) Callable.From(() => { if (IsInstanceValid(c) && c.IsInsideTree() && c.IsVisibleInTree()) c.GrabFocus(); }).CallDeferred(); }
    public void Notify(string message) => _message.Text = message;
    public void ShowMain()
    {
        Clear("main");
        Entries.AddThemeConstantOverride("separation", 5);
        SelectedEntry = 0; _emphasis = 1;
        AtmosphereMaterial.SetShaderParameter("selection", 0);
        Entry("Play", ShowPlay);
        Entry("Private Match", PrivateMatch);
        Entry("Card Library", Library);
        Entry("Deck Builder", DeckBuilder);
        Entry("Options", ShowOptions);
        Entry("Quit", ShowQuit);
        var buttons = Entries.GetChildren().OfType<MenuEntry>().ToArray();
        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i; var button = buttons[i];
            button.SourceLettering = true;
            button.CustomMinimumSize = new Vector2(380, i == 0 ? 84 : 64);
            // The visible glyphs are the exact approved raster lettering; Button
            // text retains the accessible name and normal Godot input behavior.
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                button.AddThemeColorOverride(state, Colors.Transparent);
            button.FocusEntered += () => SelectEntry(index);
            button.MouseEntered += () => button.GrabFocus();
            button.FocusNeighborTop = buttons[(i + buttons.Length - 1) % buttons.Length].GetPath();
            button.FocusNeighborBottom = buttons[(i + 1) % buttons.Length].GetPath();
        }
        if (CanResume()) _message.Text = IsOnline() ? "Your private table continues online. Play → Return to table." : "Your practice is paused. Play → Return to table.";
        FocusFirst();
    }
    private void SelectEntry(int index)
    {
        if (SelectedEntry == index) return;
        SelectedEntry = index; _emphasis = Preferences.ReducedMotion ? 1 : 0;
        AtmosphereMaterial.SetShaderParameter("selection", index);
    }
    public void ShowPlay()
    {
        Clear("play");
        if (CanResume()) Entry("Return to table", Resume, 44);
        Entry("Practice", PracticeSetup);
        Entry("Tutorial", ShowTutorialTracks, 43);
        Entry("Back", ShowMain, 43);
        _message.Text = "Choose saved decks or presets for a match against the practice opponent.";
        FocusFirst();
    }
    /// <summary>The tutorial menu: the tracks of the curriculum, then the lessons of one. Every lesson is listed and none is locked; one that is not built yet is dimmed and shows what it will cover.</summary>
    public void ShowTutorialTracks()
    {
        Clear("tutorial");
        foreach (var track in Core.TutorialCurriculum.Tracks)
        {
            var open = track;
            var button = Entry(track.Title, () => OpenTutorialTrack(open), TutorialFontSize);
            button.FocusEntered += () => _message.Text = track.Entries.Length == 1 ? track.Entries[0].Subtitle : $"{track.Entries.Count(e => e.Built)} of {track.Entries.Length} lessons built.";
        }
        Entry("Back", ShowPlay, TutorialFontSize);
        _message.Text = "Choose a track. Every lesson is open from the start.";
        FocusFirst();
    }
    /// <summary>Back from a lesson: the list of the lesson's track, or the track list when the track is a single lesson that starts straight away.</summary>
    public void ShowTutorialFor(string? lessonId)
    {
        if (lessonId is not null && Core.TutorialCurriculum.TrackOf(lessonId) is { Entries.Length: > 1 } track) ShowTutorialTrack(track);
        else ShowTutorialTracks();
    }
    /// <summary>A track holding one built lesson starts it straight away; any other opens its list.</summary>
    private void OpenTutorialTrack(Core.TutorialTrack track)
    {
        if (track.Entries is [{ Built: true } only] && Tutorial is { } start) start(only.Id);
        else ShowTutorialTrack(track);
    }
    public void ShowTutorialTrack(Core.TutorialTrack track, string? unbuiltLessonId = null)
    {
        Clear("tutorial-track");
        // The lessons scroll (mouse wheel, or drag the bar); Back stays fixed below them.
        var scroll = new ScrollContainer { Position = new Vector2(1056, 351), Size = new Vector2(430, 342), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        var bar = scroll.GetVScrollBar();
        bar.CustomMinimumSize = new Vector2(12, 0);
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color(0.87f, 0.82f, 0.69f, 0.12f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        foreach (var (name, color) in new[] { ("grabber", new Color("a89f88")), ("grabber_highlight", new Color("dfd1af")), ("grabber_pressed", new Color("f8d590")) })
            bar.AddThemeStyleboxOverride(name, new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        Canvas.RemoveChild(Entries); scroll.AddChild(Entries); Canvas.AddChild(scroll); TrackScroll = scroll;
        Entries.Position = Vector2.Zero; Entries.CustomMinimumSize = new Vector2(408, 0);
        foreach (var entry in track.Entries)
        {
            var lesson = entry;
            var button = Entry(entry.Title, () => { if (lesson.Built && Tutorial is { } start) start(lesson.Id); }, TutorialFontSize);
            button.TooltipText = entry.Subtitle;
            var describe = lesson.Built ? lesson.Subtitle : ("Not built yet. " + lesson.Subtitle).Trim();
            if (!entry.Built)
            {
                button.AddThemeColorOverride("font_color", new Color("8a8170")); button.AddThemeColorOverride("font_hover_color", new Color("a89f88")); button.AddThemeColorOverride("font_focus_color", new Color("a89f88"));
            }
            button.FocusEntered += () => _message.Text = describe;
            button.MouseEntered += () => _message.Text = describe;
        }
        var back = Entry("Back", ShowTutorialTracks, TutorialFontSize); Entries.RemoveChild(back); Canvas.AddChild(back);
        back.Position = new Vector2(1056, 701); back.Size = new Vector2(414, 62); _trackBack = back;
        _message.Text = "Dimmed lessons are placeholders: they are not built yet.";
        FocusFirst();
        // Next Tutorial on a lesson whose successor is a placeholder lands on it, so the menu says it is not built yet and what it will cover.
        if (unbuiltLessonId is not null && Array.FindIndex(track.Entries, e => e.Id == unbuiltLessonId) is >= 0 and var at && Entries.GetChild(at) is Control waiting)
            Callable.From(() => { if (IsInstanceValid(waiting) && waiting.IsVisibleInTree()) waiting.GrabFocus(); }).CallDeferred();
    }
    private void Begin(int seat)
    {
        if (!CanResume()) { ShowName(seat); return; }
        Clear("replace");
        Entries.AddChild(Palette.Wrapped(IsOnline() ? "Leave your private table and start a new practice? This disconnects your seat; it does not concede." : "Replace the current practice? Unsaved progress will be lost.", 25));
        Entry("Start new practice", () => ShowName(seat), 39);
        Entry("Back", ShowPlay, 43); FocusFirst();
    }
    public void ShowName(int seat)
    {
        Clear("name");
        Entries.AddChild(Palette.Text("Take your seat", 36, Palette.Gold, true));
        Entries.AddChild(Palette.Wrapped(seat == 0 ? "Crane · Shinden Asahina" : "Lion · Halls of Memory", 25, Palette.PaperDim));
        Entries.AddChild(Palette.Text("Your display name", 24));
        var name = new LineEdit { Text = Preferences.DisplayName, PlaceholderText = "Enter your name", MaxLength = 24, CustomMinimumSize = new Vector2(0, 60), TooltipText = "Up to 24 characters. Remembered on this device; no account or login." };
        name.AddThemeFontSizeOverride("font_size", 30);
        name.AddThemeStyleboxOverride("normal", Palette.Box(new Color("10181d"), new Color("796442"), 12, 2));
        name.AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Gold, 12, 2));
        Entries.AddChild(name);
        Entries.AddChild(Palette.Wrapped("Remembered on this device. No account or login.", 21, Palette.PaperDim));
        void Start()
        {
            var clean = MenuPreferences.CleanName(name.Text);
            if (clean.Length == 0) { Notify("Enter a name to take your seat."); name.GrabFocus(); return; }
            Preferences.DisplayName = clean;
            if (Preferences.Save() != Error.Ok) { Notify("Your name could not be saved. Check local storage and try again."); return; }
            PreferencesChanged(); StartMatch(seat);
        }
        var play = Entry("Begin match", Start, 48);
        play.Disabled = MenuPreferences.CleanName(name.Text).Length == 0;
        name.TextChanged += value => play.Disabled = MenuPreferences.CleanName(value).Length == 0;
        name.TextSubmitted += _ => Start();
        Entry("Back", ShowPlay, 40);
        Callable.From(() => { if (IsInstanceValid(name) && name.IsInsideTree() && name.IsVisibleInTree()) name.GrabFocus(); }).CallDeferred();
    }
    public void ShowQuit()
    {
        Clear("quit");
        Entries.AddChild(Palette.Wrapped("Quit the game? Local practice progress is not saved between launches.", 26));
        Entry("Quit game", () => GetTree().Quit(), 45); Entry("Back", ShowMain, 43); FocusFirst();
    }
    public void ShowOptions()
    {
        Clear("options");
        var heading = Palette.Text("Options", 34, Palette.Gold, true); Entries.AddChild(heading);
        var display = Palette.Button(Preferences.Fullscreen ? "Display: Fullscreen" : "Display: Windowed", () => { });
        display.Pressed += () => { Preferences.Fullscreen = !Preferences.Fullscreen; Changed(); display.Text = Preferences.Fullscreen ? "Display: Fullscreen" : "Display: Windowed"; };
        StyleOption(display); display.CustomMinimumSize = new Vector2(0, 40); display.AddThemeFontSizeOverride("font_size", 22); Entries.AddChild(display);
        Toggle("VSync", Preferences.VSync, value => Preferences.VSync = value);
        Toggle("Reduced motion", Preferences.ReducedMotion, value => Preferences.ReducedMotion = value, "Keeps the lantern steady.");
        Toggle("Paused Animation", Preferences.PausedAnimation, value => Preferences.PausedAnimation = value, "Holds a card your opponent plays on screen until you press Proceed (or Enter).");
        Toggle("Flip Dynasty Cards", Preferences.FlipDynastyCards, value => Preferences.FlipDynastyCards = value, "In the Events Phase, shows each Dynasty card being turned face-up, one at a time.");
        var strength = Palette.Text($"Lantern flicker · {Preferences.LanternStrength:P0}", 22); Entries.AddChild(strength);
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = .05, Value = Preferences.LanternStrength, Modulate = Palette.Gold, CustomMinimumSize = new Vector2(0, 28), TooltipText = "Strength of the slow lantern light across the screen and room. Approved default: 85%." }; Entries.AddChild(slider);
        slider.ValueChanged += value => { Preferences.LanternStrength = (float)value; strength.Text = $"Lantern flicker · {Preferences.LanternStrength:P0}"; Changed(); };
        Entries.AddChild(Palette.Wrapped("F11: fullscreen · ↑↓ / Tab: navigate · Enter: select", 19, Palette.PaperDim));
        Entry("Back", ShowMain, 48).CustomMinimumSize = new Vector2(0, 54);
        _message.Text = "Changes apply immediately and are saved on this device.";
        display.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void Toggle(string text, bool value, Action<bool> change, string tip = "")
    {
        var check = new CheckBox { Text = text + (value ? " · On" : " · Off"), ButtonPressed = value, CustomMinimumSize = new Vector2(0, 34), TooltipText = tip }; check.AddThemeFontSizeOverride("font_size", 22);
        StyleOption(check);
        check.Toggled += enabled => { check.Text = text + (enabled ? " · On" : " · Off"); change(enabled); Changed(); }; Entries.AddChild(check);
    }
    private static void StyleOption(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
        {
            var box = Palette.Box(state == "focus" ? Colors.Transparent : new Color(.025f, .040f, .059f, state == "normal" ? .65f : .9f), new Color(.63f, .49f, .27f, state == "focus" ? .9f : .4f), 8, 0);
            box.BorderWidthTop = 0; box.BorderWidthLeft = 0; box.BorderWidthRight = 0;
            button.AddThemeStyleboxOverride(state, box);
        }
    }
    private void Changed()
    {
        PreferencesChanged();
        _message.Text = Preferences.Save() == Error.Ok ? "Settings saved on this device." : "Settings applied, but could not be saved on this device.";
    }
    // Catalogue and deck-builder overlays handle _UnhandledInput. Consuming
    // Escape in the earlier key phase would open Quit behind those overlays.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (Page != "main") ShowMain(); else if (CanResume()) Resume(); else ShowQuit();
        GetViewport().SetInputAsHandled();
    }
}

public partial class MenuEntry : Button
{
    public Func<bool> ReducedMotion = () => false;
    public bool SourceLettering;
    private float _glow, _time;
    private readonly StyleBoxFlat[] _halos = Enumerable.Range(1, 18).Select(i => Palette.Box(Colors.Transparent, null, 0, i)).ToArray();
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || SourceLettering) return;
        var target = IsHovered() || HasFocus() ? 1f : 0f;
        _glow = ReducedMotion() ? target : Mathf.Lerp(_glow, target, 1 - Mathf.Exp(-(float)delta * 9));
        if (!ReducedMotion()) _time += (float)delta;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (SourceLettering || _glow < .002) return;
        float width = Math.Min(Size.X - 10, Palette.Title.GetStringSize(Text, fontSize: GetThemeFontSize("font_size")).X + 55);
        float pulse = ReducedMotion() ? 1 : .92f + .08f * Mathf.Sin(_time * 1.3f);
        for (var i = 18; i >= 1; i--)
        {
            _halos[i - 1].BgColor = new Color(.95f, .54f, .16f, _glow * pulse * .008f);
            DrawStyleBox(_halos[i - 1], new Rect2(5 - i, Size.Y * .68f - i, width + i * 2, i * 2 + 4));
        }
        for (var i = 0; i < 5; i++)
            DrawLine(new Vector2(10 + i * 13, Size.Y - 13 + i), new Vector2(width - i * 19, Size.Y - 13 + i), new Color(.88f, .56f, .20f, _glow * (.12f + i * .02f)), 1);
    }
}
