using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Explicit native UI fixture; never changes a playable deck or authoritative match.</summary>
public partial class FallenProbe : Control
{
    private string _output = "/tmp/fallen.png", _mode = "normal";
    private FallenGallery? _gallery;
    private PlayerView[] _players = [];
    private readonly List<CardTile> _tiles = [];
    private bool _interactive;
    private double _clock; private int _stage;
    public static CardView Example(string key, string id, int owner = 0, Zone zone = Zone.Dead, bool dishonorable = false, bool bowed = false)
    {
        var def = CardCatalog.Get(key);
        return new CardView(id, def, owner, zone, true, bowed, null, null, def.Force, def.Chi, Dishonorable: dishonorable);
    }
    public static PlayerView[] FixturePlayers(PlayerView[] original) => original.Select(p => p with
    {
        DeadCount = p.Id == 0 ? 3 : 2,
        Discard = new[] {
            Example("oracle-l5r-1645","fallen-kenshi-"+p.Id,p.Id),
            Example("oracle-l5r-502","fallen-konomi-"+p.Id,p.Id,dishonorable:true),
            Example("oracle-l5r-521","fallen-yasutora-"+p.Id,p.Id)
        }.Take(p.Id == 0 ? 3 : 2).Append(Example("oracle-l5r-1645", "ordinary-discard-" + p.Id, p.Id, Zone.DynastyDiscard)).ToArray(),
        RemovedFromGame = [Example("oracle-l5r-521", "removed-" + p.Id, p.Id, Zone.RemovedFromGame)]
    }).ToArray();
    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        { if (a.StartsWith("--output=")) _output = a[9..]; if (a.StartsWith("--mode=")) _mode = a[7..]; _interactive |= a == "--interactive"; }
        GetWindow().Title = "Fallen cards · native artwork review"; GetWindow().GrabFocus(); Theme = SidebarSkin.Theme();
        var session = new MatchSession(); session.StartPractice(false, true, 0); _players = FixturePlayers(session.Snapshot!.View.Players);
        AddChild(new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Art/navy-mat-v4.png"),
            Size = GetViewportRect().Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore
        });
        Caption("FALLEN CARDS · NATIVE REVIEW", new Vector2(25, 15), 28, true);
        if (_mode == "comparison")
        {
            AddChild(new TextureRect
            {
                Texture = new AtlasTexture { Atlas = FallenArt.B, Region = FallenArt.GallerySource },
                Position = new Vector2(20, 108),
                Size = FallenArt.GallerySource.Size,
                MouseFilter = MouseFilterEnum.Ignore
            });
            Caption("Approved B source", new Vector2(20, 69), 22);
            _gallery = new FallenGallery(_players, 0) { Position = new Vector2(1120, 108), Size = FallenGallery.DesignSize }; AddChild(_gallery);
            Caption("Native renderer · original scans and live text", new Vector2(1120, 69), 22);
            AddChild(new TextureRect
            {
                Texture = new AtlasTexture { Atlas = FallenArt.A, Region = FallenArt.CounterSource },
                Position = new Vector2(60, 782),
                Size = FallenArt.CounterSource.Size,
                MouseFilter = MouseFilterEnum.Ignore
            });
            AddChild(new FallenCounter { Player = _players[0], Position = new Vector2(645, 782), Size = FallenArt.CounterSource.Size });
            Caption("A source", new Vector2(60, 741), 22); Caption("Native counter", new Vector2(645, 741), 22);
            Caption("Source ornament pixels · Caudex / WeatheredQuill", new Vector2(1170, 795), 22);
            Caption("Illustrative public state · not a recorded match", new Vector2(1170, 839), 18);
        }
        else if (_mode == "orientation")
        {
            for (var i = 0; i < 4; i++)
            {
                var tile = new CardTile(Example("oracle-l5r-1645", "state-" + i, zone: Zone.Home, dishonorable: i >= 2, bowed: i % 2 == 1), true, _ => { }, _ => { })
                { Position = new Vector2(60 + i * 370, 130), Size = new Vector2(310, 350), FaceHeight = 295, ReducedMotion = true };
                AddChild(tile); _tiles.Add(tile);
                Caption(new[] { "Honorable", "Bowed", "Dishonorable", "Bowed + dishonorable" }[i], new Vector2(65 + i * 370, 500), 21);
            }
            var crowded = Example("oracle-l5r-1645", "crowded", zone: Zone.Home, dishonorable: true, bowed: true) with
            {
                Tokens = Enumerable.Range(0, 6).Select(i => new CardToken("t" + i, new[] { "Fire", "Wealth", "Favor", "Poison", "Blood", "Training" }[i], "source")).ToArray(),
                GrantedKeywords = [new("Berserker", "source", "Permanent")]
            };
            var tok = new CardTile(crowded, true, _ => { }, _ => { }) { Position = new Vector2(80, 625), Size = new Vector2(345, 345), FaceHeight = 285, ReducedMotion = true };
            AddChild(tok); _tiles.Add(tok);
            var reading = (CardTile)CardPreview.Face(crowded, 320, 240); reading.Position = new Vector2(650, 622); reading.Size = new Vector2(245, 332); AddChild(reading); _tiles.Add(reading);
            Caption("Six tokens + permanent grant", new Vector2(65, 990), 20);
            Caption("Reading stays upright", new Vector2(625, 990), 20);
            var marker = new FallenMedallion { Dishonorable = true, Position = new Vector2(1050, 620), Size = new Vector2(150, 150) }; AddChild(marker);
            Caption("The same status icon in inspection", new Vector2(1000, 795), 22);
        }
        else
        {
            var s = GetViewportRect().Size.Y / 1080f;
            var plaque = new ClanPlaque("Crane", 11, 2, false, () => { }, "Crane", "YOUR TURN", true, true, 5, _players[0].HandLimit)
            { Position = new Vector2(18, 100) * s, Size = new Vector2(310, 108) * s }; AddChild(plaque);
            var counter = new FallenCounter { Player = _players[0], Position = new Vector2(18, 212) * s, Size = new Vector2(310, 70) * s }; AddChild(counter);
            _gallery = new FallenGallery(_players, 0) { Position = new Vector2(355, 99) * s, Size = new Vector2(1480, 811) * s }; AddChild(_gallery);
            counter.Pressed += () => _gallery.SelectPlayer(0);
            Caption("A counter + B inspection", new Vector2(25, GetViewportRect().Size.Y - 56), 20);
            if (_mode == "many")
            {
                _players = _players.Select(p => p.Id != 0 ? p : p with
                {
                    DeadCount = 24,
                    Discard = Enumerable.Range(0, 24).Select(i =>
                    Example("oracle-l5r-1645", "many-" + i, dishonorable: i % 2 == 1) with { Definition = CardCatalog.Get("oracle-l5r-1645") with { Name = i == 23 ? "Daidoji Kenshi — a deliberately long public Personality name" : "Daidoji Kenshi " + (i + 1) } }).ToArray()
                }).ToArray();
                _gallery.Refresh(_players);
                counter.Player = _players[0]; counter.QueueRedraw();
            }
        }
        _gallery?.SelectCard("fallen-konomi-0");
    }
    private void Caption(string text, Vector2 at, int size, bool heading = false)
    { var l = Palette.Text(text, size, Palette.Paper, heading); l.Position = at; AddChild(l); }
    private void Click(Control c)
    {
        var p = c.GetGlobalTransform() * (c.Size / 2);
        GetWindow().GrabFocus(); GetViewport().WarpMouse(p);
        GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    public override void _Process(double delta)
    {
        if (_interactive) return;
        _clock += delta; if (_clock < 1.1) return;
        try
        {
            if (_mode == "orientation")
            {
                for (var i = 0; i < 4; i++)
                {
                    var expected = (i % 2 == 1 ? Mathf.Pi / 2 : 0) + (i >= 2 ? Mathf.Pi : 0);
                    if (!Mathf.IsEqualApprox(_tiles[i].VisualFaceAngle, expected)) throw new Exception("Dishonor/bow composition failed");
                    if (!_tiles[i]._HasPoint(_tiles[i].FaceCornersInCanvas().Select(p => _tiles[i].GetGlobalTransform().AffineInverse() * p).Aggregate(Vector2.Zero, (a, b) => a + b) / 4)) throw new Exception("Rotated card hit test failed");
                }
                if (_tiles[4].TokenRail?.IconsWithinCardHeight() != true || _tiles[4].TokenRail?.GrantSeal is null) throw new Exception("Crowded dishonorable card lost its rail/grant");
                if (!Mathf.IsZeroApprox(_tiles[5].VisualFaceAngle)) throw new Exception("Reading face is inverted");
                if (FallenArt.TurnsOnTable(_tiles[2].Card with { Definition = null }) || FallenArt.TurnsOnTable(_tiles[2].Card with { Zone = Zone.Dead })) throw new Exception("Hidden or dead card inverted");
            }
            else if (_mode == "many")
            {
                if (_gallery!.Rows.Count != 24) throw new Exception("Large gallery dropped rows");
                if (_stage == 0) { _gallery.ListScroll.ScrollVertical = 10000; _stage++; _clock = 0; return; }
                if (_gallery.ListScroll.ScrollVertical <= 0) throw new Exception("Large gallery cannot scroll");
                if (_stage == 1) { Click(_gallery.Rows[^1]); _stage++; _clock = 0; return; }
                if (_gallery.SelectedCardId != "many-23") throw new Exception("Last visible row could not be selected by native click");
            }
            else if (_gallery is not null && _mode != "comparison")
            {
                if (_stage == 0) { Click(_gallery.Rows[0]); _stage++; _clock = 0; return; }
                if (_stage == 1)
                {
                    if (_gallery.SelectedCardId != "fallen-kenshi-0") throw new Exception("Native row click did not select");
                    Click(_gallery.Tabs[1]); _stage++; _clock = 0; return;
                }
                if (_stage == 2)
                {
                    if (_gallery.OwnerId != 1 || _gallery.Rows.Count != 2) throw new Exception("Player tab did not switch");
                    if (_gallery.Rows.Any(r => r.Card.Zone != Zone.Dead)) throw new Exception("Other piles leaked into Fallen");
                    _gallery.Tabs[0].GrabFocus(); _stage++; _clock = 0; return;
                }
                if (_stage == 3)
                {
                    if (!_gallery.Tabs[0].HasFocus()) throw new Exception("Crane tab did not receive keyboard focus");
                    GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }, true);
                    GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }, true); _stage++; _clock = 0; return;
                }
                if (_stage == 4)
                {
                    if (_gallery.OwnerId != 0) throw new Exception("Keyboard tab activation failed");
                    var changed = _players.Select(p => p.Id != 0 ? p : p with { DeadCount = 0, Discard = [] }).ToArray();
                    _gallery.Refresh(changed); if (_gallery.Rows.Count != 0 || _gallery.SelectedCard is not null) throw new Exception("Stale selection after death pile emptied");
                    if (_mode == "empty") foreach (var counter in GetChildren().OfType<FallenCounter>())
                    { counter.Player = changed.Single(p => p.Id == counter.Player.Id); counter.QueueRedraw(); }
                    if (_mode != "empty") { _gallery.Refresh(_players); _gallery.SelectCard(_mode == "honorable" ? "fallen-kenshi-0" : "fallen-konomi-0"); }
                    _stage++; _clock = 0; return;
                }
            }
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(2, 2) });
            _gallery?.BackButton.ReleaseFocus();
            RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(_output);
            var checks = _mode switch
            {
                "comparison" => "source/native artwork capture",
                "orientation" => "four composed orientations, hit tests, crowded token/grant rail, upright reading, hidden/dead exclusion",
                "many" => "24 rows, scroll overflow, last-row selection",
                _ => "native row click, player tab click, keyboard activation, pile separation, empty-state refresh"
            };
            GD.Print("FALLEN_PASS: " + _mode + "; " + checks); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetViewport().GetTexture().GetImage().SavePng(_output + ".failure.png"); GetTree().Quit(1); }
    }
}
