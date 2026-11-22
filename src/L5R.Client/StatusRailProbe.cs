using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>
/// Native check for #1279: a card with a status but no tokens shows no token track. Every in-play card type is
/// stepped through each status kind on its own, all of them together, with and without a permanent-change seal,
/// and against token controls. Public-view fixtures only; the engine supplies these rows in real games.
/// </summary>
public partial class StatusRailProbe : Control
{
    private readonly List<CardTile> _tiles = [];
    private string _output = "/tmp/status-rails";
    private double _clock;
    private int _combo = -1, _clicks, _checked;
    private bool _clickSent;
    private Control? _board;

    private static readonly CardRestrictionView[] Kinds =
    [
        new("act", "Cannot perform actions", "Until turn ends", "Fixture source"),
        new("move", "Movement to battlefields is negated", "Until turn ends"),
        new("straighten", "Cannot be straightened by effects", "Until turn ends"),
        new("target", "Cannot be targeted by actions performed by the affected performer", "Until turn ends"),
        new("rule", "Will not issue challenges", "Until turn ends"),
        new("contribute", "Does not contribute Force", "Until this battle ends"),
        new("protect", "Protected from destruction from card effects", "Until turn ends", Protective: true),
    ];
    private static readonly CardToken[] SomeTokens = [new("t1", "Fire", "fixture", "force", 1), new("t2", "Poison", "fixture", "force", -1)];
    private static readonly GrantedKeyword[] Permanent = [new("Cavalry", "fixture", "Permanent")];
    private static readonly (string Name, CardRestrictionView[] Statuses, bool Seal, bool Tokens)[] Combos =
        Kinds.Select(k => (k.Kind, new[] { k }, false, false))
        .Concat([
            ("all-statuses", Kinds, false, false),
            ("all-statuses-seal", Kinds, true, false),
            ("seal-only", Array.Empty<CardRestrictionView>(), true, false),
            ("move-straighten-act", Kinds.Where(k => k.Kind is "move" or "straighten" or "act").ToArray(), false, false),
            ("tokens-and-statuses", Kinds, false, true),
            ("tokens-only", Array.Empty<CardRestrictionView>(), false, true),
        ]).ToArray();

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--output=")) _output = arg[9..];
        DirAccess.MakeDirRecursiveAbsolute(_output);
        Theme = Palette.MakeTheme(); GetWindow().GrabFocus();
        var size = GetViewportRect().Size;
        AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/navy-mat-v4.png"), Size = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
    }

    private static CardDefinition Def(CardType type, string? name = null) =>
        CardCatalog.Cards.Values.Where(d => d.Type == type && (name is null || d.Name == name)).OrderBy(d => d.Id, StringComparer.Ordinal).First();

    // Every card type that can be in play and carry a public status: Personalities at home, at a battlefield and bowed;
    // the three attachment types; Holdings (Bamboo Harvesters, as reported, upright and bowed); the Stronghold; a Region; a Ring.
    private static (string Id, CardDefinition Def, Zone Zone, bool Bowed)[] Fixtures() =>
    [
        ("personality-home", Def(CardType.Personality), Zone.Home, false),
        ("personality-battle", Def(CardType.Personality), Zone.Battlefield, false),
        ("personality-bowed", Def(CardType.Personality), Zone.Battlefield, true),
        ("follower", Def(CardType.Follower), Zone.Attached, false),
        ("item", Def(CardType.Item), Zone.Attached, false),
        ("spell", Def(CardType.Spell), Zone.Attached, false),
        ("holding", Def(CardType.Holding, "Bamboo Harvesters"), Zone.Home, false),
        ("holding-bowed", Def(CardType.Holding, "Bamboo Harvesters"), Zone.Home, true),
        ("stronghold", Def(CardType.Stronghold), Zone.Stronghold, false),
        ("region", Def(CardType.Region), Zone.Region, false),
        ("ring", Def(CardType.Ring), Zone.Home, false),
    ];

    private void Build((string Name, CardRestrictionView[] Statuses, bool Seal, bool Tokens) combo)
    {
        _board?.QueueFree(); _tiles.Clear();
        _board = new Control { Size = GetViewportRect().Size, MouseFilter = MouseFilterEnum.Ignore }; AddChild(_board);
        var heading = Palette.Text($"STATUS WITHOUT TOKENS · {combo.Name.ToUpperInvariant()}", 26, Palette.Gold, true); heading.Position = new Vector2(30, 14); _board.AddChild(heading);
        var fixtures = Fixtures(); var size = GetViewportRect().Size;
        var perRow = 6; var side = Math.Min(230f, (size.X - 60) / perRow - 70); var column = (size.X - 60) / perRow;
        for (var i = 0; i < fixtures.Length; i++)
        {
            var (id, def, zone, bowed) = fixtures[i];
            var card = new CardView(id, def, 0, zone, true, bowed, zone is Zone.Battlefield or Zone.Region ? 0 : null, null, 2,
                GrantedKeywords: combo.Seal ? Permanent : null, Tokens: combo.Tokens ? SomeTokens : null,
                Restrictions: combo.Statuses.Length > 0 ? combo.Statuses : null);
            var tile = new CardTile(card, true, _ => _clicks++, _ => { })
            {
                Position = new Vector2(30 + column * (i % perRow) + 70, 90 + (i / perRow) * (side + 120)),
                Size = new Vector2(side * .95f, side),
                FaceHeight = side - 12,
                ReducedMotion = true,
                TokenSourceName = _ => "Fixture source"
            };
            _board.AddChild(tile); _tiles.Add(tile);
        }
    }

    private static float Thickness(Rect2 body) => Math.Min(body.Size.X, body.Size.Y);
    private static Vector2[] IconCorners(Control c) => new[] { Vector2.Zero, new Vector2(c.Size.X, 0), c.Size, new Vector2(0, c.Size.Y) }.Select(p => c.GetGlobalTransform() * p).ToArray();
    private static float OverlapArea(Vector2[] a, Vector2[] b) => Geometry2D.IntersectPolygons(a, b).Sum(p => Math.Abs(Enumerable.Range(0, p.Length).Sum(i => p[i].Cross(p[(i + 1) % p.Length]))) / 2);

    private void Validate()
    {
        var combo = Combos[_combo];
        foreach (var tile in _tiles)
        {
            var card = tile.Card; var where = $"{combo.Name} / {card.Id}";
            var tokens = CardTokenRail.HasTokens(card); var statuses = RestrictionRail.HasEffects(card); var seal = CardChanges.HasSeal(card);
            if (combo.Statuses.Length > 0 && !statuses) throw new Exception($"{where}: an in-play status was not presented");
            if (!(tokens || statuses || seal)) { if (tile.TokenRail is not null) throw new Exception($"{where}: rail with nothing to show"); continue; }
            var rail = tile.TokenRail ?? throw new Exception($"{where}: status/seal not presented");
            // #1314: one icon per status kind and token group, the seal first; five to a column, more columns outward.
            var expectedIcons = RestrictionRail.Groups(card).Length + CardTokenRail.Groups(card).Length;
            if (rail.Icons.Length != expectedIcons) throw new Exception($"{where}: {rail.Icons.Length} icons for {expectedIcons} statuses/token groups");
            if (rail.Icons.Count(i => i.Restriction is not null) != RestrictionRail.Groups(card).Length) throw new Exception($"{where}: a status kind has no icon");
            var expectedColumns = Math.Max(1, (expectedIcons + (seal ? 1 : 0) + CardTokenRail.PerColumn - 1) / CardTokenRail.PerColumn);
            if (CardTokenRail.Tracks(card) != expectedColumns || Math.Abs(CardTokenRail.WidthFor(card) - (CardTokenRail.Width * expectedColumns + (expectedColumns - 1) * CardTokenRail.Gap)) > .01f)
                throw new Exception($"{where}: layout reserves {CardTokenRail.WidthFor(card)} for {expectedColumns} column(s)");
            if (!rail.IconsWithinCardHeight()) throw new Exception($"{where}: icons run past the card's height");
            if (rail.MouseFilter != MouseFilterEnum.Ignore || rail.FocusMode != FocusModeEnum.None) throw new Exception($"{where}: the stack itself takes the pointer");
            foreach (var icon in rail.Icons)
            {
                if (string.IsNullOrWhiteSpace(icon.GetMeta("flat_tooltip").AsString())) throw new Exception($"{where}: icon has no explanation");
                foreach (var face in _tiles)
                    if (OverlapArea(IconCorners(icon), face.FaceCornersOnViewport()) > .25f) throw new Exception($"{where}: icon covers card {face.Card.Id}");
            }
            if (seal && rail.GrantSeal is null) throw new Exception($"{where}: permanent-change seal missing");
            foreach (var other in _tiles)
                if (OverlapArea(rail.CornersOnViewport(), other.FaceCornersOnViewport()) > .25f) throw new Exception($"{where}: rail covers {other.Card.Id}");
            _checked++;
        }
    }

    public override void _Process(double delta)
    {
        _clock += delta; if (_clock < .45) return; _clock = 0;
        try
        {
            if (_combo >= 0)
            {
                Validate();
                // Native input on the first status-only combo: clicking the status track still selects its card.
                if (_combo == 0 && !_clickSent)
                {
                    var point = _tiles.First(t => t.Card.Id == "holding").TokenRail!.Icons.First(i => i.Restriction is not null).CenterOnViewport();
                    GetWindow().GrabFocus(); GetViewport().WarpMouse(point);
                    GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
                    GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                    GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                    GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }, true);
                    _clickSent = true; return;
                }
                if (_combo == 0 && _clicks != 1) throw new Exception($"Status-icon click selects its card exactly once: {_clicks}");
                RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng($"{_output}/{Combos[_combo].Name}.png");
            }
            if (++_combo >= Combos.Length)
            {
                GD.Print($"STATUS_RAIL_PASS: {Combos.Length} status combinations × {_tiles.Count} in-play card fixtures; {_checked} rails checked; one icon per status kind and token group; columns of five within the card height; icons explain themselves; seal kept; native status-icon click.");
                GetTree().Quit(); return;
            }
            Build(Combos[_combo]);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetViewport().GetTexture().GetImage().SavePng($"{_output}/failure.png"); GetTree().Quit(1); }
    }
}
