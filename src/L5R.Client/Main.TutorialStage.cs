using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A soft gold pulse drawn around part of the screen (the phase bar, the battle bar, the hand) while the tutorial is talking about it. Visual only: it never takes a click.</summary>
public partial class TutorialGlowFrame : Control
{
    public Func<Rect2?>? Area { get; set; }
    public bool ReducedMotion { get; set; }
    private const float Margin = 8;

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; ZIndex = 58; Visible = false; }

    public override void _Process(double delta)
    {
        if (Area?.Invoke() is not { } area || GetParent() is not Control parent) { Visible = false; return; }
        var inverse = parent.GetGlobalTransform().AffineInverse();
        var origin = inverse * area.Position; var far = inverse * area.End;
        Position = origin - new Vector2(Margin, Margin); Size = far - origin + new Vector2(Margin, Margin) * 2;
        Visible = true; QueueRedraw();
    }

    public override void _Draw()
    {
        var strength = ReducedMotion ? .9f : .7f + .3f * MathF.Sin(Time.GetTicksMsec() / 260f);
        var rect = new Rect2(Vector2.Zero, Size).Grow(-2);
        var corners = new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y), rect.Position };
        foreach (var (width, alpha) in new[] { (22f, .07f), (15f, .12f), (9f, .22f), (4f, .55f) })
            DrawPolyline(corners, Palette.Gold with { A = alpha * strength }, width, true);
    }
}

public partial class Main
{
    // ---- Glow ---------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private readonly Dictionary<string, TutorialGlowFrame> _tutorialGlows = [];

    private Rect2? TutorialGlowArea(string part)
    {
        Rect2? Visible(Control? control) => control is { } c && IsInstanceValid(c) && c.IsVisibleInTree() ? c.GetGlobalRect() : null;
        return part switch
        {
            TutorialGlow.PhaseBar => Visible(_timeline),
            TutorialGlow.BattleBar => Visible(_battleProgressStrip),
            TutorialGlow.Honor => _clanPlaques.TryGetValue(View.ViewerId, out var plaque) && IsInstanceValid(plaque) && plaque.IsVisibleInTree() ? plaque.HonorGlobalRect : null,
            // the Imperial Favor's reminder tile on your side, while you hold it (the Imperial Favor lesson)
            TutorialGlow.Favor => Visible(_homeReminders.FirstOrDefault(r => IsInstanceValid(r) && r.IsFavor && r.OwnerId == View.ViewerId)),
            TutorialGlow.Hand => _tiles.Where(t => IsInstanceValid(t) && t.IsVisibleInTree() && t.Card is { Zone: Zone.Hand } c && c.OwnerId == View.ViewerId).Select(t => t.GetGlobalRect())
                .Aggregate((Rect2?)null, (all, r) => all is { } a ? a.Merge(r) : r),
            _ => null
        };
    }

    private void SyncTutorialGlow(string[] parts)
    {
        foreach (var gone in _tutorialGlows.Keys.Where(k => !parts.Contains(k)).ToArray()) { if (IsInstanceValid(_tutorialGlows[gone])) _tutorialGlows[gone].QueueFree(); _tutorialGlows.Remove(gone); }
        foreach (var part in parts.Where(p => !_tutorialGlows.ContainsKey(p)))
        {
            var captured = part;
            var frame = new TutorialGlowFrame { Area = () => TutorialGlowArea(captured), ReducedMotion = _reducedMotion };
            _root.AddChild(frame); _tutorialGlows[part] = frame;
        }
    }

    // ---- The Show flow: a card held large in the middle of the table while its note is read ----------------------------------------------------------------------------------------
    private Control? _tutorialShow;
    private string? _tutorialShowId;

    private void SyncTutorialShow(string? definitionId)
    {
        if (definitionId == _tutorialShowId && (_tutorialShow is null || IsInstanceValid(_tutorialShow))) return;
        if (_tutorialShow is not null && IsInstanceValid(_tutorialShow)) _tutorialShow.QueueFree();
        _tutorialShow = null; _tutorialShowId = definitionId;
        if (definitionId is null) return;
        var definition = CardCatalog.Get(definitionId);
        var height = CardPlayTiming.CardHeight; var width = height * .72f;
        var stage = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 59, Modulate = new Color(1, 1, 1, 0), Name = "TutorialShow" };
        stage.Size = new Vector2(620, height + 70); stage.Position = CardPlayTiming.Center - new Vector2(stage.Size.X / 2, height / 2);
        var card = new CardView("tutorial-show", definition, TutorialRunner.Seat, Zone.Hand, true, false, null, null, definition.Force);
        var face = CardPreview.Face(card, height, width);
        face.MouseFilter = Control.MouseFilterEnum.Ignore; face.Size = new Vector2(width, height); face.Position = new Vector2((stage.Size.X - width) / 2, 0); face.PivotOffset = face.Size / 2;
        stage.AddChild(face);
        var caption = Palette.Text(definition.Name, 20, Palette.Gold);
        caption.HorizontalAlignment = HorizontalAlignment.Center; caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        caption.Position = new Vector2(10, height + 14); caption.Size = new Vector2(stage.Size.X - 20, 40);
        stage.AddChild(caption);
        _root.AddChild(stage); _tutorialShow = stage;
        var tween = stage.CreateTween();
        if (_reducedMotion) { tween.TweenProperty(stage, "modulate:a", 1.0, .15); return; }
        face.Scale = new Vector2(.7f, .7f);
        tween.SetParallel(true);
        tween.TweenProperty(stage, "modulate:a", 1.0, CardPlayTiming.Intro);
        tween.TweenProperty(face, "scale", Vector2.One, CardPlayTiming.Intro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    // ---- Laying out the Provinces, one at a time and slowly ----------------------------------------------------------------------------------------------------------------------
    // The opening notes are read on an empty cloth. The note that introduces the Provinces lays yours out left to right, each taking four times as long as a card normally does,
    // and then your opponent's four together.
    private const double TutorialDealSeconds = CardPlayTiming.Intro * 4 + .4, TutorialDealFade = CardPlayTiming.Intro * 4;
    private const int TutorialDealSlots = 5; // your four Provinces, then your opponent's
    private int _tutorialDeal = int.MaxValue;
    private bool _tutorialDealPending;
    private double _tutorialDealClock;
    private readonly Dictionary<int, double> _tutorialDealAt = [];

    private bool TutorialDealing => !_tutorialDealPending && _tutorialDeal < TutorialDealSlots || _tutorialDealAt.Values.Any(at => Time.GetTicksMsec() / 1000.0 - at < TutorialDealFade);

    private int TutorialDealSlot(CardView card) => card.OwnerId == TutorialRunner.Seat ? (card.ProvinceIndex ?? 0) + 1 : TutorialDealSlots;

    private void SyncTutorialDeal(TutorialCoach coach)
    {
        if (!coach.ProvincesOnTable) { _tutorialDeal = 0; _tutorialDealPending = true; _tutorialDealAt.Clear(); return; }
        if (_tutorialDealPending) { _tutorialDealPending = false; _tutorialDealClock = 0; _tutorialDeal = 0; ProcessTutorialDeal(0, true); }
    }

    /// <summary>Called every frame: lays out the next slot when its time has come.</summary>
    private void ProcessTutorialDeal(double delta, bool first = false)
    {
        if (_session.Tutorial is not { Finished: false } || _tutorialDealPending || _tutorialDeal >= TutorialDealSlots) return;
        _tutorialDealClock += delta;
        if (!first && _tutorialDealClock < TutorialDealSeconds) return;
        _tutorialDealClock = 0; _tutorialDeal++;
        _tutorialDealAt[_tutorialDeal] = Time.GetTicksMsec() / 1000.0;
        Render();
    }

    /// <summary>A Province card that has not been laid out yet is invisible and untouchable; one that has just been laid out fades in.</summary>
    private void ApplyTutorialDeal(CardTile tile)
    {
        if (_session.Tutorial is not { Finished: false } || tile.Card.Zone != Zone.Province) return;
        var slot = TutorialDealSlot(tile.Card);
        if (_tutorialDealPending || slot > _tutorialDeal)
        {
            tile.Modulate = new Color(1, 1, 1, 0); tile.MouseFilter = Control.MouseFilterEnum.Ignore; return;
        }
        if (_tutorialDealAt.TryGetValue(slot, out var at) && !_reducedMotion)
            tile.Modulate = new Color(1, 1, 1, (float)Math.Clamp((Time.GetTicksMsec() / 1000.0 - at) / TutorialDealFade, 0, 1));
    }

    private void UpdateTutorialDealFades() { if (_tutorialDealAt.Count > 0) foreach (var tile in _tiles) if (IsInstanceValid(tile)) ApplyTutorialDeal(tile); }

    /// <summary>The presentations a note starts (a Province reveal, a played card) run at a quarter of their usual speed when it asks.</summary>
    private double TutorialSlow => _session.Tutorial is { Finished: false } lesson && lesson.Coach.Slow ? 4.0 : 1.0;
}
