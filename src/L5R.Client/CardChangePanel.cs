using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>B's icon-led lacquer panel. Native text, wrapping and scrolling remain accessible.</summary>
public partial class CardChangePanel : PanelContainer
{
    internal static string? LastCardId;
    public Vector2? FixedSize { get; set; }
    public override void _Process(double delta) { if (FixedSize is { } fixedSize && Size != fixedSize) Size = fixedSize; }
    public override void _Draw() => CardChangeArt.Frame(this, new Rect2(Vector2.Zero, Size));
    public override void _Ready() => Resized += QueueRedraw;
    public static CardChangePanel Create(CardView card, Func<string, string?>? sourceName = null, bool compact = false, Action<string>? inspectSource = null, bool copiedOnly = false)
    {
        var panel = new CardChangePanel { Theme = SidebarSkin.Theme(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var margin = new StyleBoxEmpty
        {
            ContentMarginLeft = compact ? 22 : 52,
            ContentMarginRight = compact ? 22 : 34,
            ContentMarginTop = compact ? 25 : 24,
            ContentMarginBottom = compact ? 25 : 22
        };
        panel.AddThemeStyleboxOverride("panel", margin);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", compact ? 14 : 8); panel.AddChild(body);
        var entries = CardChanges.Entries(card).Where(e => !copiedOnly || e.Copied).OrderBy(e => CardChanges.IsPermanent(e.Duration) ? 0 : 1).ToArray();
        var permanent = entries.Count(e => CardChanges.IsPermanent(e.Duration));
        var copies = entries.Count(e => e.Copied);
        var commonCopyDuration = copies > 0 && copies == entries.Length && entries.Select(e => e.Duration).Distinct().Count() == 1;
        var heading = Palette.Text(copies > 0 && copies == entries.Length ? "CURRENTLY COPIED" : copies > 0 ? "CARD CHANGES" : permanent > 0 ? "PERMANENT CHANGES" : "CARD EFFECTS", compact ? 19 : 27, Palette.Gold, true);
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart; heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(heading); body.AddChild(Divider(ornament: true));
        var name = Palette.Wrapped(card.Definition?.Name ?? "Card", compact ? 21 : 28);
        name.AddThemeFontOverride("font", SidebarSkin.Bold); name.HorizontalAlignment = HorizontalAlignment.Center; if (!compact) body.AddChild(name); else name.Free();
        if (!compact) body.AddChild(Divider());
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", compact ? 15 : 10);
        scroll.AddChild(rows); body.AddChild(scroll);
        // A fixed-height tooltip scrolls when necessary; in the inspector the panel grows with
        // content, using the inspector's existing scroll container instead.
        scroll.VerticalScrollMode = compact ? ScrollContainer.ScrollMode.Disabled : ScrollContainer.ScrollMode.Auto;
        var commonSource = entries.Select(e => (e.Source, e.Copied, e.SourceLabel)).Distinct().Count() == 1;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (i == permanent && permanent > 0 && i < entries.Length)
                rows.AddChild(Palette.Text(copies > 0 ? "COPIED / TEMPORARY" : "TEMPORARY / ATTACHED", compact ? 14 : 19, Palette.PaperDim, true));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", compact ? 12 : 32); rows.AddChild(row);
            var diameter = compact ? 42 : 78;
            row.AddChild(new CardChangeMedallion
            {
                Kind = entry.Kind,
                CustomMinimumSize = new Vector2(diameter, diameter),
                SizeFlagsVertical = SizeFlags.ShrinkBegin,
                MouseFilter = MouseFilterEnum.Ignore
            });
            var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; words.AddThemeConstantOverride("separation", compact ? 6 : 6); row.AddChild(words);
            var ability = entry.Kind is not ("Keyword" or "Stat");
            var title = Palette.Wrapped(entry.Name + (ability && !commonCopyDuration ? " · " + CardChanges.DurationLabel(entry.Duration) : ""), compact ? 19 : 27); title.AddThemeFontOverride("font", SidebarSkin.Bold); words.AddChild(title);
            var kind = entry.Kind == "Keyword" ? (entry.Copied ? "Copied keyword" : "Granted keyword") : entry.Kind == "Stat" ? "Stat bonus" : entry.Copied ? "Copied ability" : "Granted effect";
            if (!ability && !commonCopyDuration) words.AddChild(Palette.Wrapped(kind + " · " + CardChanges.DurationLabel(entry.Duration), compact ? 14 : 20, Palette.Paper));
            if (!string.IsNullOrWhiteSpace(entry.Text))
            {
                var text = Palette.Wrapped(entry.Text, compact ? 17 : 20); text.AddThemeConstantOverride("line_spacing", compact ? 3 : 0); words.AddChild(text);
            }
            // Issue #1309: the granted Tactician has performed his Tactical action(s) this turn.
            if (entry.Kind == "Keyword" && entry.Name == "Tactician" && AbilityUseArt.TacticianSpent(card))
                words.AddChild(Palette.Wrapped(AbilityUseArt.TacticianUsedText, compact ? 15 : 20, new Color("e05a4a")));
            if (!commonSource) words.AddChild(Source(entry, sourceName, compact ? 14 : 17, inspectSource));
            if (i < entries.Length - 1) rows.AddChild(Divider());
        }
        if (entries.Length == 0) rows.AddChild(Palette.Wrapped("No granted changes.", compact ? 16 : 21, Palette.PaperDim));
        if (commonSource && entries.Length > 0)
        {
            body.AddChild(Divider());
            var footer = new MarginContainer(); footer.AddThemeConstantOverride("margin_left", compact ? 0 : 110);
            footer.AddChild(Source(entries[0], sourceName, compact ? 15 : 20, inspectSource)); body.AddChild(footer);
        }
        if (commonCopyDuration) body.AddChild(Palette.Wrapped(CardChanges.DurationLabel(entries[0].Duration), compact ? 15 : 20, Palette.PaperDim));
        return panel;
    }
    private static Control Divider(bool ornament = false)
    {
        if (ornament) return new CardChangeDivider { CustomMinimumSize = new Vector2(0, 10), MouseFilter = MouseFilterEnum.Ignore };
        var line = new HSeparator { CustomMinimumSize = new Vector2(0, 3), MouseFilter = MouseFilterEnum.Ignore };
        line.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color("8e7443"), Thickness = 1 }); return line;
    }
    private static Control Source(CardChanges.Entry entry, Func<string, string?>? resolve, int size, Action<string>? inspect)
    {
        var id = entry.Source; var name = entry.SourceLabel ?? CardChanges.SourceName(id, resolve); var prefix = entry.Copied ? "Copied from: " : "Source: ";
        if (inspect is not null && resolve?.Invoke(id) is not null)
        {
            var button = new LinkButton
            {
                Text = prefix + name,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = "Inspect " + name
            };
            button.AddThemeFontOverride("font", Palette.Body); button.AddThemeColorOverride("font_color", Palette.Crane); button.AddThemeFontSizeOverride("font_size", size);
            button.Pressed += () => inspect(id); return button;
        }
        return Palette.Wrapped(prefix + name, size, Palette.Paper);
    }
    public static Control Tooltip(CardView card, Func<string, string?>? resolve, Vector2 viewport, float? maxHeight = null)
    {
        LastCardId = card.Id;
        var scale = Math.Clamp(viewport.Y / 1080f, .65f, 1.25f);
        var width = Math.Min(CardChangeArt.PanelWidth, (viewport.X - 40) / scale);
        var estimated = 224d;
        foreach (var entry in CardChanges.Entries(card))
            estimated += entry.Text.Length == 0 ? 101 : 60 + Math.Max(1, Math.Ceiling(Palette.Body.GetStringSize(entry.Text, fontSize: 20).X / (width - 190))) * 26;
        var design = new Vector2(width, Math.Min(Math.Max(285, (float)estimated), (maxHeight ?? viewport.Y - 65) / scale));
        var shell = new Control { CustomMinimumSize = design * scale, Size = design * scale, MouseFilter = MouseFilterEnum.Ignore };
        shell.SetMeta("permanent_changes", card.Id);
        var panel = Create(card, resolve); shell.AddChild(panel); panel.FixedSize = design; panel.Size = design; panel.Scale = Vector2.One * scale;
        return shell;
    }
}

/// <summary>B's original central diamond with a native rule that fits the panel width.</summary>
public partial class CardChangeDivider : Control
{
    public override void _Draw()
    {
        // Source B’s central diamond and thin rule, with the ornament at original scale.
        var y = Size.Y / 2;
        DrawLine(new Vector2(0, y), new Vector2(Size.X / 2 - 10, y), new Color("8e7443"));
        DrawLine(new Vector2(Size.X / 2 + 10, y), new Vector2(Size.X, y), new Color("8e7443"));
        DrawTextureRectRegion(CardChangeArt.B, new Rect2(Size.X / 2 - 8, y - 8, 16, 16), new Rect2(1068, 157, 16, 16));
    }
}

/// <summary>A's small rail-cap seal: one marker, live count, independent focus and tooltip.</summary>
public partial class PermanentGrantSeal : Button
{
    public required CardTile CardAnchor { get; init; }
    private CanvasLayer? _focusLayer;
    internal bool HasFocusPanel => IsInstanceValid(_focusLayer);
    private bool TacticianUsed => AbilityUseArt.TacticianSpent(CardAnchor.Card) && !AbilityUseArt.PrintedTactician(CardAnchor.Card)
        && (CardAnchor.Card.GrantedKeywords ?? []).Any(k => k.Keyword == "Tactician" && CardChanges.IsPermanent(k.Duration));
    public PermanentGrantSeal()
    {
        Flat = true; FocusMode = FocusModeEnum.All; MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps; MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusExited += CloseFocusPanel;
    }
    public override void _Ready()
    {
        var count = CardChanges.Marked(CardAnchor.Card).Length;
        var kind = CardChanges.Entries(CardAnchor.Card).Any(e => e.Copied) ? "granted/copied" : "permanent";
        TooltipText = $"{CardAnchor.Card.Definition?.Name} · {count} {kind} change{(count == 1 ? "" : "s")}";
        if (TacticianUsed) TooltipText += "\n" + AbilityUseArt.TacticianUsedText;
        Pressed += () => { CloseFocusPanel(); CardAnchor.EmitSignal(BaseButton.SignalName.Pressed); };
        FocusEntered += ShowFocusPanel;
    }
    // Projected cards use Main's screen-space overlay. Godot may request a native
    // tooltip even after its text is cleared; never create a competing popup there.
    public override Control _MakeCustomTooltip(string text) => CardAnchor.ProjectToScreen is not null ? null! : CreateReadingPreview();
    public Control CreateReadingPreview() => CardChangePanel.Tooltip(CardAnchor.Card, CardAnchor.TokenSourceName, GetTree().Root.GetVisibleRect().Size);
    public override void _Draw()
    {
        // Approved A's single-grant cap contains no baked quantity. Crop the original art,
        // not a hand-drawn approximation; add the current aggregate count separately.
        // Issue #1309: a permanently granted Tactician who has performed his Tactical action(s) this turn turns the seal red.
        var red = TacticianUsed ? new Color(1f, .35f, .3f) : Colors.White;
        DrawTextureRectRegion(CardChangeArt.A, new Rect2(Vector2.Zero, Size), new Rect2(95, 719, 88, 86), red);
        var count = CardChanges.Marked(CardAnchor.Card).Length;
        if (count > 1)
        {
            // Use just the source's knot on the left, leaving a lacquer field for the live numeral.
            DrawTextureRectRegion(CardChangeArt.A, new Rect2(Size.X * .13f, Size.Y * .17f, Size.X * .77f, Size.Y * .65f), new Rect2(153, 190, 50, 45));
            DrawTextureRectRegion(CardChangeArt.A, new Rect2(Size.X * .09f, Size.Y * .26f, Size.X * .51f, Size.Y * .48f), new Rect2(110, 746, 56, 51));
            var fs = Math.Max(5, (int)(Size.Y * .43f));
            while (fs > 5 && Palette.Body.GetStringSize(count.ToString(), fontSize: fs).X > Size.X * .35f) fs--;
            DrawString(Palette.Body, new Vector2(Size.X * .58f, Size.Y * .65f), count.ToString(), HorizontalAlignment.Center, Size.X * .35f, fs, Palette.Paper);
        }
        if (IsHovered() || HasFocus()) DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), Palette.Gold with { A = .65f }, false, 1);
    }
    private void ShowFocusPanel()
    {
        CloseFocusPanel(); QueueRedraw();
        var viewport = GetTree().Root.GetVisibleRect().Size;
        var popup = CardChangePanel.Tooltip(CardAnchor.Card, CardAnchor.TokenSourceName, viewport);
        _focusLayer = new CanvasLayer { Layer = 95 }; GetTree().Root.AddChild(_focusLayer); _focusLayer.AddChild(popup);
        var corners = CardAnchor.FaceCornersOnViewport();
        var right = corners.Max(p => p.X) + 24; var top = corners.Min(p => p.Y);
        if (right + popup.Size.X > viewport.X - 12) right = corners.Min(p => p.X) - popup.Size.X - 24;
        popup.Position = new Vector2(Math.Clamp(right, 12, Math.Max(12, viewport.X - popup.Size.X - 12)), Math.Clamp(top, 12, Math.Max(12, viewport.Y - popup.Size.Y - 12)));
    }
    private void CloseFocusPanel() { if (IsInstanceValid(_focusLayer)) _focusLayer!.QueueFree(); _focusLayer = null; QueueRedraw(); }
    public override void _GuiInput(InputEvent e) { if (e.IsActionPressed("ui_cancel")) { CloseFocusPanel(); AcceptEvent(); } }
    public override void _ExitTree() { if (IsInstanceValid(_focusLayer)) _focusLayer!.QueueFree(); }
}
