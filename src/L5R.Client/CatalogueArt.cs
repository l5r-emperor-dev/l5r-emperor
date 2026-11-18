using Godot;
namespace L5R.Client;

public static class CatalogueArt
{
    public static readonly Color Ivory = new("e9dfc5"), Gold = new("bd9854"), Muted = new("aea489"), Navy = new("0a1014");
    public static Texture2D Cloth => _cloth ??= GD.Load<Texture2D>("res://Art/Catalogue/gallery-cloth.png"); // held for recorded draws (#1281)
    private static Texture2D? _cloth;
    private static readonly FontFile HeadingFile = GD.Load<FontFile>("res://Art/Fonts/WeatheredQuill-Regular.ttf");
    public static Font Heading(int size) => new FontVariation { BaseFont = HeadingFile, SpacingGlyph = -1, SpacingTop = -(int)(size * .55), SpacingBottom = -(int)(size * .4) };
    public static StyleBoxTexture Frame(int margin = 24) => new() { Texture = Cloth, TextureMarginLeft = margin, TextureMarginRight = margin, TextureMarginTop = margin, TextureMarginBottom = margin, ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin };
    public static Label Label(string text, int size = 18, bool heading = false)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        l.AddThemeFontOverride("font", heading ? Heading(size) : Palette.Body); l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", Ivory); l.Uppercase = heading; return l;
    }
    public static Theme Theme()
    {
        var theme = Palette.MakeTheme(); theme.DefaultFontSize = 18;
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            {
                var active = state is "hover" or "pressed";
                var box = ControlStyle(state == "focus" ? Colors.Transparent : active ? new Color("3c3020") : new Color("10171a"), state == "focus" ? new Color("c7d9e0") : active ? new Color("e4bd77") : new Color("6d5935"));
                if (active) { box.ShadowColor = new Color(.79f, .56f, .22f, .18f); box.ShadowSize = 7; }
                theme.SetStylebox(state, type, box);
            }
            theme.SetColor("font_color", type, Ivory); theme.SetColor("font_hover_color", type, Ivory); theme.SetColor("font_pressed_color", type, Ivory);
        }
        theme.SetStylebox("normal", "LineEdit", ControlStyle(new Color("10171a"), new Color("8c754b"), 12, 5));
        theme.SetStylebox("focus", "LineEdit", ControlStyle(new Color("16212a"), Gold, 12, 5));
        theme.SetStylebox("panel", "PopupMenu", Frame(14));
        theme.SetStylebox("hover", "PopupMenu", Palette.Box(new Color("46341f"), Gold, 8));
        theme.SetStylebox("panel", "PopupPanel", Frame(20));
        theme.SetColor("font_color", "PopupMenu", Ivory); theme.SetFont("font", "PopupMenu", Palette.Body);
        return theme;
    }
    public static StyleBoxFlat ControlStyle(Color fill, Color border, int padding = 10, int radius = 4)
    { var box = Palette.Box(fill, border, padding, radius); box.ContentMarginTop = box.ContentMarginBottom = 4; return box; }
    public static void Selected(Button button, bool selected) =>
        button.AddThemeStyleboxOverride("normal", ControlStyle(selected ? new Color("332b1f") : new Color("10171a"), selected ? Gold : new Color("51442d"), 8));
    public static Control Backdrop()
    {
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore }; p.AddThemeStyleboxOverride("panel", Frame()); return p;
    }
    public static Button Button(string text, Action action)
    { var b = new Button { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand }; b.Pressed += action; return b; }
    public static TextureRect Clan(string clan)
    {
        var region = clan switch { "Lion" => new Rect2(114, 225, 224, 218), "Crane" => new Rect2(574, 220, 225, 223), "Phoenix" => new Rect2(0, 0, 222, 222), "Scorpion" => new Rect2(234, 0, 219, 222), "Crab" => new Rect2(465, 0, 218, 222), "Unicorn" => new Rect2(689, 0, 220, 222), "Dragon" => new Rect2(340, 220, 224, 223), _ => new Rect2(5, 10, 548, 560) };
        Texture2D tex = clan is "Mantis" or "Spider" or "Imperial" ? GD.Load<Texture2D>("res://Art/clan-" + clan.ToLowerInvariant() + ".png") : new AtlasTexture { Atlas = GD.Load<Texture2D>("res://Art/" + (clan is "All" or "Unaligned" ? "koku-reference.png" : "clan-reference.png")), Region = region };
        var r = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/aged-emblem.gdshader") }; material.SetShaderParameter("decal_size", new Vector2(40, 40)); material.SetShaderParameter("white_cutout", clan is "All" or "Unaligned"); material.SetShaderParameter("saturation", .65f); r.Material = material; return r;
    }
}
