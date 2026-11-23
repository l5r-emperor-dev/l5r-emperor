using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native public-view layout/input fixture, not playable card definitions.</summary>
public partial class TokenRailProbe : Control
{
    private readonly List<CardTile> _cards = [];
    private readonly List<(CardTile Card, Rect2 Body)> _attackBodies = [];
    private string _output = "/tmp/token-rails.png";
    private double _clock;
    private int _clicks, _stage;
    private bool _restrictions;
    private bool _small, _perspective, _comparison, _bowedAttachments;
    private int _hoverRetries;
    private TableSurface? _surface;
    private Control? _host;
    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs()) { if (arg.StartsWith("--output=")) _output = arg[9..]; _restrictions |= arg == "--restrictions"; _small |= arg == "--small"; _perspective |= arg == "--perspective"; _comparison |= arg == "--comparison"; _bowedAttachments |= arg == "--bowed-attachments"; }
        Theme = Palette.MakeTheme(); GetWindow().GrabFocus();
        var size = GetViewportRect().Size;
        AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/navy-mat-v4.png"), Size = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
        var heading = Palette.Text("TOKEN AND RESTRICTION RAILS · NATIVE REVIEW", 30, Palette.Gold, true); heading.Position = new Vector2(30, 18); AddChild(heading);
        var caption = Palette.Text("Public-view fixtures • 1 / 4 / 5 / 6 groups • original card faces • live counts", 18); caption.Position = new Vector2(30, 58); AddChild(caption);
        var defs = PublishedContent.Snapshot.Cards.Concat(PublishedContent.PersonalityWave2.Cards).ToArray();
        var personality = defs.First(d => d.Type == CardType.Personality && d.Name == "Togashi Torazu");
        var types = new[] { "Fire", "Poison", "Sake", "Training", "Blood", "Madness" };
        CardToken[] Tokens(int n) => Enumerable.Range(0, n).SelectMany(i => Enumerable.Range(0, i == 0 ? 12 : 1).Select(j => new CardToken($"{i}-{j}", types[i], "fixture-source", i == 0 ? "force" : null, i == 0 ? 1 : 0))).ToArray();
        if (_perspective)
        {
            _surface = new TableSurface(); AddChild(_surface); _surface.ResizePlane(size, 1);
            _host = new Control { Position = _surface.CanvasSize / 2 - size * .83f / 2, Scale = Vector2.One * .83f, MouseFilter = MouseFilterEnum.Ignore }; _surface.Canvas.AddChild(_host);
        }
        if (_comparison)
        {
            var sourceAtlas = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://Art/unit-ribbon-reference.png"), Region = new Rect2(55, 114, 438, 490) };
            AddChild(new TextureRect { Texture = sourceAtlas, Position = new Vector2(100, 160), Size = new Vector2(438, 490), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore });
            var label = Palette.Text("Approved C source", 24); label.Position = new Vector2(150, 120); AddChild(label);
            var live = Palette.Text("Native renderer • approved token art", 24); live.Position = new Vector2(810, 120); AddChild(live);
            Face(new CardView("comparison", personality, 0, Zone.Home, true, false, null, null, 3, 3, UnitForce: 3, Tokens: Enumerable.Range(0, 3).Select(i => new CardToken("c" + i, "Fire", "source", "force", 1)).ToArray()), new Vector2(825, 160), 490);
            return;
        }
        var side = _small ? 150f : 260f; var column = size.X / 4;
        for (var i = 0; i < 4; i++)
        {
            var card = new CardView($"p-{i}", personality, 0, Zone.Home, true, _restrictions && i % 2 == 1, null, null, 12, 3, Dishonorable: _restrictions && i >= 2, UnitForce: 12, Tokens: Tokens(new[] { 1, 4, 5, 6 }[i]));
            Face(card, new Vector2(column * i + column * .25f, 100), side);
        }
        var y = _small ? 330 : 430;
        var f = defs.First(d => d.Type == CardType.Follower);
        var item = defs.First(d => d.Type == CardType.Item);
        var stronghold = defs.First(d => d.Type == CardType.Stronghold);
        var holding = defs.First(d => d.Type == CardType.Holding);
        Face(new CardView("bow", personality, 1, Zone.Battlefield, true, true, 1, null, 12, 3, UnitForce: 0, Tokens: Tokens(6)), new Vector2(45, y), side);
        Face(new CardView("hold", holding, 0, Zone.Home, true, false, null, null, 0, Tokens: [new("w", "Wealth", "fixture-source", null, 0)]), new Vector2(column + 60, y), side);
        Face(new CardView("fort", stronghold, 0, Zone.Stronghold, true, false, null, null, 0, Tokens: [new("fav", "Favor", "fixture-source"), new("fav2", "Favor", "fixture-source")], Restrictions: _restrictions ? [new("act", "Cannot perform actions", "Until turn ends")] : null), new Vector2(column * 2 + 60, y), side);
        var baseX = column * 3 + column * .20f;
        Face(new CardView("item", item, 0, Zone.Attached, true, _bowedAttachments, null, "stack", 0, Tokens: [new("i", "Training", "fixture-source")]), new Vector2(baseX, y), side, 72);
        Face(new CardView("follower", f, 0, Zone.Attached, true, _bowedAttachments, null, "stack", 5, Tokens: [new("f", "Fire", "fixture-source")]), new Vector2(baseX, y + 25), side, 36);
        Face(new CardView("stack", personality, 0, Zone.Home, true, false, null, null, 12, 3, UnitForce: 17, Tokens: Tokens(2)), new Vector2(baseX, y + 50), side);
        foreach (var tile in _cards.Where(c => c.Card.Id is "item" or "follower" or "stack"))
            tile.TokenRail!.Companions = _cards.Where(c => c.Card.Id is "item" or "follower" or "stack").ToArray();
        // Mixed-source types remain separate by effect and hidden/out-of-play cards reveal no rail.
        var source = _cards[0].Card;
        var blood = source with { Tokens = [new("a", "Blood", "x", "force", 1), new("b", "Blood", "y", "chi", -1)] };
        if (CardTokenRail.Groups(blood).Length != 2 || CardTokenRail.Groups(source with { Definition = null }).Length != 0 || CardTokenRail.Groups(source with { Zone = Zone.Hand }).Length != 0) throw new Exception("Token grouping/privacy regression");
    }
    private CardTile Face(CardView card, Vector2 position, float side, float offset = 0)
    {
        if (_restrictions && card.Definition?.Type == CardType.Personality)
            card = card with
            {
                Tokens = Enumerable.Range(0, 6).Select(i => new CardToken("stress-" + i, new[] { "Fire", "Poison", "Sake", "Training", "Blood", "Madness" }[i], "fixture")).ToArray(),
                CreatedCopy = true,
                GrantedKeywords = [new("Berserker", "fixture", "Permanent")],
                Restrictions = [new("move", "Cannot move", "Until turn ends", "Layout fixture"), new("straighten", "Cannot straighten", "Until turn ends", "Layout fixture"), new("protect", "Protected from destruction", "Until battle ends", "Layout fixture", Protective: true)]
            };
        var tile = new CardTile(card, true, _ => _clicks++, _ => { }) { Position = position, Size = new Vector2(side * .95f, side), FaceHeight = side - 12, AlignUnitRightEdge = true, ChosenTarget = _restrictions, ReducedMotion = true, TokenRailOffset = offset * (side - 12) / 198, TokenSourceName = _ => "Source card" };
        tile.ProjectToScreen = _surface is null ? null : _surface.ProjectCanvas;
        ((Node?)_host ?? this).AddChild(tile); _cards.Add(tile);
        if (card.UnitForce.HasValue)
        {
            var standard = new UnitStandard { CardAnchor = tile, Card = card, Projection = _surface is null ? null : _surface.ProjectCanvas, Unprojection = _surface is null ? p => p : _surface.ScreenToCanvas, Explanation = UnitStandard.ExplainUnit(card) };
            ((Node?)_host ?? this).AddChild(standard);
            if (_restrictions)
            {
                var tab = new AttackSourceTab { ReducedMotion = true, Desired = new AttackCueView("Ranged", card.Id) };
                tab.Placement = _ =>
                {
                    var corners = tile.FaceCornersOnViewport();
                    var edge = Enumerable.Range(0, 4).MaxBy(i => (corners[i].X + corners[(i + 1) % 4].X) / 2);
                    var ends = new[] { corners[edge], corners[(edge + 1) % 4] }.OrderBy(v => v.Y).ToArray();
                    var width = Math.Clamp((corners.Max(v => v.X) - corners.Min(v => v.X)) * .24f, 32, 52);
                    var body = new Rect2(ends[0].Lerp(ends[1], .07f), new Vector2(width, width * 396 / 562));
                    _attackBodies.RemoveAll(b => b.Card == tile); _attackBodies.Add((tile, body)); return body;
                };
                tab.Arrange = (_, body, progress) => standard.ReserveAttackSpace(new Rect2(body.Position, body.Size + new Vector2(0, 5)), progress);
                AddChild(tab);
            }
        }
        return tile;
    }
    private Vector2[] VisibleFace(CardTile face)
    {
        var polygon = face.FaceCornersOnViewport();
        if (face.Card.AttachedTo is null) return polygon;
        // A folded attachment is intentionally behind the next card. Validate its
        // exposed title strip; testing its hidden face would forbid normal stacks.
        var next = _cards.Where(c => c.Card.Id == face.Card.AttachedTo || c.Card.AttachedTo == face.Card.AttachedTo)
            .Where(c => c.Position.Y > face.Position.Y).ToArray();
        var bottom = next.SelectMany(c => c.FaceCornersOnViewport()).Min(p => p.Y);
        var clip = new[] { new Vector2(-10000, -10000), new Vector2(10000, -10000), new Vector2(10000, bottom), new Vector2(-10000, bottom) };
        return Geometry2D.IntersectPolygons(polygon, clip).FirstOrDefault() ?? [];
    }
    private static float OverlapArea(Vector2[] a, Vector2[] b) => b.Length < 3 ? 0 : Geometry2D.IntersectPolygons(a, b).Sum(p => Math.Abs(Enumerable.Range(0, p.Length).Sum(i => p[i].Cross(p[(i + 1) % p.Length]))) / 2);
    public override void _Input(InputEvent e) { if (_surface is not null && e is InputEventMouse mouse) _surface.ForwardMouse(mouse); }
    public override void _Process(double delta)
    {
        _clock += delta; if (_clock < 1.1) return;
        try
        {
            var rails = _cards.Select(c => c.TokenRail!).ToArray();
            foreach (var rail in rails)
            {
                if (!GetViewportRect().Encloses(new Rect2(rail.CornersOnViewport()[0], Vector2.Zero).Expand(rail.CornersOnViewport()[2]))) throw new Exception($"Token rail clipped: {rail.CardAnchor.Card.Id}");
                foreach (var other in rails.Where(other => other != rail))
                    if (OverlapArea(rail.CornersOnViewport(), other.CornersOnViewport()) > .25f) throw new Exception($"Rails overlap: {rail.CardAnchor.Card.Id}, {other.CardAnchor.Card.Id}");
                foreach (var attack in _attackBodies)
                    if (OverlapArea(rail.CornersOnViewport(), new[] { attack.Body.Position, new Vector2(attack.Body.End.X, attack.Body.Position.Y), attack.Body.End, new Vector2(attack.Body.Position.X, attack.Body.End.Y) }) > .25f) throw new Exception("Rail covers attack tab");
                foreach (var face in _cards)
                    if (OverlapArea(rail.CornersOnViewport(), VisibleFace(face)) > .25f) throw new Exception($"Rail {rail.CardAnchor.Card.Id} covers {face.Card.Id}");
                if (!rail.IconsWithinCardHeight()) throw new Exception($"Icons run past the card's height: {rail.CardAnchor.Card.Id}");
            }
            var first = rails[0].Icons.First(i => i.Token is not null); var point = first.CenterOnViewport(); // #1314: hover one token icon
            if (_stage == 0)
            {
                ReadingTooltip.LastText = null;
                GetWindow().GrabFocus(); GetViewport().NotifyMouseEntered(); GetViewport().WarpMouse(point);
                GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
                _stage = 1; _clock = 0; return;
            }
            if (_stage == 1)
            {
                // A small icon is a small target: on a just-opened window the first native hover can be missed. Wait, re-hovering only if needed, before failing.
                if (!_perspective && ReadingTooltip.LastText?.Contains(" token ×") != true)
                {
                    if (++_hoverRetries > 4) throw new Exception("Native token tooltip did not open: last=" + ReadingTooltip.LastText + " hovered=" + (GetViewport().GuiGetHoveredControl() is CardIcon hi ? hi.CardAnchor.Card.Id + "/" + hi.TooltipText.Length + "/" + hi.IsVisibleInTree() + "/" + (hi == first) : GetViewport().GuiGetHoveredControl()?.GetType().Name) + " focus=" + GetWindow().HasFocus());
                    // Re-send motion only if the pointer left the icon; a motion event restarts Godot's tooltip timer.
                    if (GetViewport().GuiGetHoveredControl() is not CardIcon) { GetViewport().WarpMouse(point); GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One }, true); }
                    _clock = 0; return;
                }
                GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }, true);
                _stage = 2; _clock = 0; return;
            }
            if (_clicks != 1) throw new Exception($"Native token-icon click selects owner exactly once: {_clicks}");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(5, 5) });
            RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(_output);
            GD.Print(_comparison ? "SOURCE_COMPARISON_PASS: 1 native host; owner click; source artwork beside native renderer." : "TOKEN_RAIL_PASS: 10 hosts; 1/4/5/6 groups; ×12; bowed; attachment ownership; hidden/out-of-play suppression; Blood effect grouping; native owner click."); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetViewport().GetTexture().GetImage().SavePng(_output + ".failure.png"); GetTree().Quit(1); }
    }
}
