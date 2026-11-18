using Godot;
namespace L5R.Client;

/// <summary>Image-generated lacquer/brass materials, with live labels and resizable nine-slice borders.</summary>
public static class DeckSkin
{
    public static readonly Color Text = new("f2e7cf"), Gold = new("e1bd72");
    public static Font Body { get; } = new FontVariation { BaseFont = Palette.Body, VariationEmbolden = .22f };
    public static StyleBoxTexture Plate(int state = 0, int padding = 10)
    {
        var texture = GD.Load<Texture2D>("res://Art/DeckBuilder/control-materials-v2.png"); var scale = texture.GetWidth() / 1536f;
        var y = new[] { 97f, 323f, 548f, 770f }[state];
        var atlas = new AtlasTexture { Atlas = texture, Region = new Rect2(62 * scale, y * scale, 1416 * scale, 154 * scale), FilterClip = true };
        return new StyleBoxTexture
        {
            Texture = atlas,
            TextureMarginLeft = 12,
            TextureMarginRight = 12,
            TextureMarginTop = 5,
            TextureMarginBottom = 5,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
    }
    public static StyleBoxTexture Parchment()
    {
        var texture = GD.Load<Texture2D>("res://Art/DeckBuilder/comparison-parchment-v2.png");
        return new StyleBoxTexture { Texture = texture, TextureMarginLeft = 18, TextureMarginRight = 18, TextureMarginTop = 18, TextureMarginBottom = 18, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20 };
    }
    // #1281: a draw call keeps only the texture's RID, not the Resource. A throwaway Frame() drawn in _Draw was
    // garbage-collected while the game idled, freeing its texture, and the recorded frame then rendered as Godot's
    // white fallback until something redrew it. Draw paths share this one held instance; nobody mutates it.
    public static StyleBoxTexture SharedFrame => _sharedFrame ??= Frame();
    private static StyleBoxTexture? _sharedFrame;
    public static StyleBoxTexture Frame()
    {
        var texture = GD.Load<Texture2D>("res://Art/DeckBuilder/panel-frame-v2.png"); var s = texture.GetWidth() / 1536f;
        return new StyleBoxTexture
        {
            Texture = new AtlasTexture { Atlas = texture, Region = new Rect2(25 * s, 25 * s, 1486 * s, 974 * s), FilterClip = true },
            TextureMarginLeft = 16,
            TextureMarginRight = 16,
            TextureMarginTop = 16,
            TextureMarginBottom = 16,
            DrawCenter = false,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 16,
            ContentMarginBottom = 16
        };
    }
    // A small fixed key space, and nothing a caller does to the returned Texture2D mutates
    // it (only ever assigned to an Icon/Texture property) -- memoized instead of re-rasterizing the SVG on
    // every call, unlike Plate() below, whose StyleBoxTexture result IS mutated by some callers (ModulateColor,
    // ExpandMarginTop, content margins), so caching that would leak one caller's mutation into every other holder.
    private static readonly Dictionary<string, Texture2D> IconCache = [];
    public static Texture2D Icon(string kind)
    {
        if (IconCache.TryGetValue(kind, out var cached)) return cached;
        var shape = kind switch
        {
            "more" => "<g fill='#ead7af'><circle cx='4' cy='10' r='1.5'/><circle cx='10' cy='10' r='1.5'/><circle cx='16' cy='10' r='1.5'/></g>",
            "knob" => "<circle cx='10' cy='10' r='6' fill='#efe9dc' stroke='#ac9062' stroke-width='1'/>",
            "arrow" => "<path d='M5 8 L10 13 L15 8 Z' fill='#ddbd79'/>",
            "checked" => "<rect x='2' y='2' width='16' height='16' rx='2' fill='#ddbd79' stroke='#ffe4a4'/><path d='M5 10 L9 14 L15 6' fill='none' stroke='#241d12' stroke-width='2'/>",
            "tick" => "<path d='M5 10 L9 14 L15 6' fill='none' stroke='#f2e7cf' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/>",
            "radio" => "<circle cx='10' cy='10' r='8' fill='#11191b' stroke='#eacb86' stroke-width='1.5'/>",
            "selected" => "<circle cx='10' cy='10' r='8' fill='#d4af62' stroke='#ffe6a9' stroke-width='1.5'/><circle cx='10' cy='10' r='4' fill='#172126'/>",
            _ => "<rect x='2' y='2' width='16' height='16' rx='2' fill='#0a1519' stroke='#ddbd79' stroke-width='1.5'/>"
        };
        var image = new Image(); image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20' viewBox='0 0 20 20'>" + shape + "</svg>");
        var texture = ImageTexture.CreateFromImage(image); IconCache[kind] = texture; return texture;
    }
    public static Theme Theme()
    {
        var t = CatalogueArt.Theme(); t.DefaultFont = Body; t.DefaultFontSize = 18;
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            t.SetStylebox("normal", type, Plate()); t.SetStylebox("hover", type, Plate(1)); t.SetStylebox("pressed", type, Plate(1));
            var disabled = Plate(); disabled.ModulateColor = new Color(.65f, .65f, .65f, .9f); t.SetStylebox("disabled", type, disabled);
            var focus = Plate(1); focus.DrawCenter = false; t.SetStylebox("focus", type, focus);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) t.SetColor(state, type, Text);
            t.SetColor("font_disabled_color", type, new Color("9f987f")); t.SetColor("font_shadow_color", type, new Color(0, 0, 0, .8f)); t.SetConstant("shadow_offset_y", type, 1);
        }
        t.SetStylebox("normal", "LineEdit", Plate()); t.SetStylebox("focus", "LineEdit", Plate(1)); t.SetColor("font_color", "LineEdit", Text);
        t.SetColor("font_placeholder_color", "LineEdit", new Color("aaa18d"));
        t.SetStylebox("slider", "HSlider", new StyleBoxLine { Color = new Color("6c695e"), Thickness = 2 });
        t.SetStylebox("grabber_area", "HSlider", new StyleBoxLine { Color = new Color("e5dbc2"), Thickness = 2 }); t.SetStylebox("grabber_area_highlight", "HSlider", new StyleBoxLine { Color = Gold, Thickness = 2 });
        t.SetIcon("grabber", "HSlider", Icon("knob")); t.SetIcon("grabber_highlight", "HSlider", Icon("knob"));
        var dialog = new DeckPanelStyle(); t.SetStylebox("panel", "AcceptDialog", dialog);
        var window = Plate(); window.ExpandMarginTop = 32; t.SetStylebox("embedded_border", "Window", window); t.SetStylebox("embedded_unfocused_border", "Window", window);
        t.SetColor("title_color", "Window", Text); t.SetFont("title_font", "Window", Body); t.SetFontSize("title_font_size", "Window", 20);
        t.SetStylebox("panel", "PopupMenu", new DeckPanelStyle()); t.SetStylebox("hover", "PopupMenu", Plate(1, 8)); t.SetStylebox("panel", "TooltipPanel", Plate(0, 12));
        t.SetIcon("arrow", "OptionButton", Icon("arrow")); t.SetIcon("checked", "CheckBox", Icon("checked")); t.SetIcon("unchecked", "CheckBox", Icon("unchecked"));
        return t;
    }
    public static void Selected(Button button, bool selected, int material = 1)
    {
        button.AddThemeStyleboxOverride("normal", Plate(selected ? material : 0));
        button.AddThemeColorOverride("font_color", selected ? new Color("ffebae") : Text);
    }
    public static void Primary(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed" }) button.AddThemeStyleboxOverride(state, Plate(2));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) button.AddThemeColorOverride(state, new Color("211708"));
        button.AddThemeFontOverride("font", new FontVariation { BaseFont = Palette.Body, VariationEmbolden = .65f }); button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
    }
}

/// <summary>Preserves the cloth's proportions behind a separately sliced gilded frame.</summary>
public partial class DeckPanelStyle : StyleBox
{
    public bool FullArt;
    private static Texture2D? Cloth;
    public DeckPanelStyle() { ContentMarginLeft = 16; ContentMarginRight = 16; ContentMarginTop = 16; ContentMarginBottom = 16; }
    public override Vector2 _GetMinimumSize() => new(32, 32);
    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
        var cloth = Cloth ??= GD.Load<Texture2D>("res://Art/DeckBuilder/work-mat.png"); // held: the canvas keeps only its RID (#1281)
        if (FullArt) { RenderingServer.CanvasItemAddTextureRect(canvasItem, rect, cloth.GetRid()); return; }
        var size = cloth.GetSize() * .65f; var aspect = rect.Size.X / rect.Size.Y;
        if (size.X / size.Y > aspect) size.X = size.Y * aspect; else size.Y = size.X / aspect;
        RenderingServer.CanvasItemAddTextureRectRegion(canvasItem, rect, cloth.GetRid(), new Rect2((cloth.GetSize() - size) / 2, size));
        DeckSkin.SharedFrame.Draw(canvasItem, rect);
    }
}
