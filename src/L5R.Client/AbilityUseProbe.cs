using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Issue #1309 native review: real scans with crafted, read-only ability-use views (grey box, clan "NX" counter, Tactician marks).</summary>
public partial class AbilityUseProbe : Control
{
    private string _output = ProjectSettings.GlobalizePath("res://../../artifacts/checks/ability-use.png");
    private int _frames;

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--output=")) _output = arg[9..];
        Theme = SidebarSkin.Theme(); GetWindow().Title = "Used abilities · native review";
        var size = GetViewportRect().Size;
        AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/navy-mat-v4.png"), Size = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore });
        Caption("USED ABILITIES · ON THE CARD ART", new Vector2(28, 12), 26);

        var tomaru = CardCatalog.Get("oracle-l5r-8893");
        var keep = CardCatalog.Get("oracle-l5r-1106");
        var tactician = CardCatalog.Get("oracle-l5r-10060");
        var atsumichi = CardCatalog.Get("oracle-l5r-2060");
        var tk = "tomaru-attack-3";
        CardView V(string id, CardDefinition def, AbilityUseView[]? uses = null, TacticalUseView? tactical = null, bool bowed = false) =>
            new(id, def, 0, Zone.Home, true, bowed, null, null, def.Force, def.Chi, AbilityUses: uses, TacticalUses: tactical);

        var row1 = new (CardView Card, string Label)[]
        {
            (V("t0", tomaru, [new(tk, [tk], 0, 2, false, false)]), "Tomaru · unused"),
            (V("t1", tomaru, [new(tk, [tk], 1, 2, false, false)]), "after one use · 1X"),
            (V("t2", tomaru, [new(tk, [tk], 2, 2, true, false)]), "exhausted · grey + 2X"),
            (V("k1", keep, [new("border-first", ["border-first"], 0, 0, true, true), new("border-game", ["border-game"], 0, 1, false, false)]), "Border Keep · first turn over"),
            (V("k2", keep, [new("border-first", ["border-first"], 0, 0, true, true), new("border-game", ["border-game"], 1, 1, true, true)]), "once-per-game used too"),
        };
        var row2 = new (CardView Card, string Label)[]
        {
            (V("m0", tactician, tactical: new(1, 1)), "printed Tactician used"),
            (V("m1", tactician, tactical: new(1, 1), bowed: true), "…and bowed"),
            (V("a0", atsumichi, [new("atsumichi-bow-straighten", ["atsumichi-bow", "atsumichi-straighten"], 1, 1, true, false)]) with
                { GrantedKeywords = [new("Tactician", "oracle-l5r-224", GrantDuration.Permanent)], TacticalUses = new(1, 1) }, "granted Tactician (seal) used"),
            (V("a1", atsumichi) with { GrantedKeywords = [new("Tactician", "oracle-l5r-224", GrantDuration.Turn)], TacticalUses = new(1, 1) }, "temporary Tactician used"),
        };
        // Issue #1329: printed keywords the Personality has lost for now are covered in the paper's own colour.
        var row3 = new (CardView Card, string Label)[]
        {
            (V("r0", CardCatalog.Get("oracle-l5r-4382")) with { RemovedPrintedKeywords = ["Lion Clan"] }, "lost Lion Clan"),
            (V("r1", CardCatalog.Get("oracle-l5r-10081")) with { RemovedPrintedKeywords = ["Ninja"] }, "lost Ninja"),
            (V("r2", CardCatalog.Get("oracle-l5r-10446")) with { RemovedPrintedKeywords = ["Crane Clan", "Duelist"] }, "lost Crane Clan and Duelist"),
            (V("r3", CardCatalog.Get("oracle-l5r-10446")), "Crane Clan and Duelist kept"),
        };
        Row(row1, 70, 300);
        Row(row2, 455, 300);
        Row(row3, 800, 240);
        var big = CardPreview.Face(row3[2].Card, 420, 300); big.Position = new Vector2(1500, 410); AddChild(big);

        Check(AbilityUseArt.Marks(row1[0].Card) is ({ Length: 0 }, { Length: 0 }), "an unused ability draws nothing");
        Check(AbilityUseArt.Marks(row1[1].Card) is ({ Length: 0 }, [(_, "1X")]), "one use draws only the counter");
        Check(AbilityUseArt.Marks(row1[2].Card) is ({ Length: 1 }, [(_, "2X")]), "exhausted draws the box and 2X");
        Check(AbilityUseArt.Marks(row1[3].Card).Shaded.Length == 1 && AbilityUseArt.Marks(row1[4].Card).Shaded.Length == 2, "Border Keep boxes");
        Check(AbilityUseArt.Marks(row2[0].Card).Shaded.Length == 1, "printed Tactician keyword box");
        Check(AbilityUseArt.Marks(row2[3].Card).Shaded.Length == 0, "a granted Tactician has no printed keyword to box");
        Check(AbilityUseArt.ClanColor("Mantis") != AbilityUseArt.ClanColor("Crane"), "clan colours differ");
        Check(AbilityUseArt.RemovedKeywordCovers(row3[0].Card).Length == 1 && AbilityUseArt.RemovedKeywordCovers(row3[1].Card).Length == 1, "a removed clan alignment and Ninja are covered");
        Check(AbilityUseArt.RemovedKeywordCovers(row3[2].Card).Length == 2 && AbilityUseArt.RemovedKeywordCovers(row3[3].Card).Length == 0, "two removed keywords, two covers; none kept covered");
    }

    private void Row((CardView Card, string Label)[] cards, float y, float height)
    {
        for (var i = 0; i < cards.Length; i++)
        {
            var x = 28 + i * (height + 70f);
            var tile = new CardTile(cards[i].Card, true, _ => { }, _ => { }) { Position = new Vector2(x, y), Size = new Vector2(height + 60, height), FaceHeight = height - 12, ReducedMotion = true };
            AddChild(tile);
            Caption(cards[i].Label, new Vector2(x, y + height + 2), 17);
        }
    }

    private void Caption(string text, Vector2 at, int size)
    {
        var label = new Label { Text = text, Position = at };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", Palette.Paper);
        AddChild(label);
    }

    private static void Check(bool ok, string what) { if (!ok) throw new Exception("ABILITY USE PROBE FAIL: " + what); GD.Print("PASS " + what); }

    public override void _Process(double delta)
    {
        if (++_frames != 20) return;
        RenderingServer.ForceDraw();
        DirAccess.MakeDirRecursiveAbsolute(_output.GetBaseDir());
        GetViewport().GetTexture().GetImage().SavePng(_output);
        GD.Print("ABILITY USE PROBE PASS: " + _output);
        GetTree().Quit();
    }
}
