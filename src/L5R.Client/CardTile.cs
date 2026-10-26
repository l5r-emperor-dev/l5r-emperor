using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class CardTile : Button
{
    public CardView Card { get; private set; } = null!;
    public bool Highlighted { get; set; }
    /// <summary>The tutorial's pointer (issue #935): a soft gold pulse around the card the lesson is talking about. Never selects or enables anything.</summary>
    public bool Pointed { get; set; }
    // ---- A printed part of the card the tutorial swells and shrinks back (issue #1075) -------------------------------------------------------------------------------------------
    // Where the Gold Cost coin and the Honor Requirement box sit on a Personality's printed face, as fractions of the scan (centre, half width, half height).
    private static readonly Dictionary<string, (float U, float V, float HalfW, float HalfH)> EmphasisParts = new()
    {
        [TutorialEmphasis.Cost] = (.486f, .605f, .075f, .052f),
        [TutorialEmphasis.HonorRequirement] = (.369f, .600f, .056f, .046f),
    };
    public const double EmphasisSeconds = 4;
    private string? _emphasis; private ulong _emphasisStart; private bool _emphasisWasActive;
    /// <summary>Starts (or, with null, ends) the swell of one printed part; every tile of one note shares the start time so a rebuilt tile carries on where it was. It repeats until the note is left.</summary>
    public void Emphasize(string? part, ulong startedMsec) { if (_emphasis == part && _emphasisStart == startedMsec) return; _emphasis = part; _emphasisStart = startedMsec; QueueRedraw(); }
    private double EmphasisAge => (Time.GetTicksMsec() - _emphasisStart) / 1000.0;
    public bool EmphasisActive => _emphasis is not null; // repeats for as long as the note is showing
    /// <summary>Called every frame by the tutorial: keeps the swell drawing while it lasts and clears it once it is over.</summary>
    public void TickEmphasis() { var active = EmphasisActive; if (active || _emphasisWasActive) QueueRedraw(); _emphasisWasActive = active; }
    private void DrawEmphasis(Texture2D texture)
    {
        if (!EmphasisActive || !EmphasisParts.TryGetValue(_emphasis!, out var part)) return;
        var face = FaceSize();
        Vector2 At(float u, float v) => ProjectFacePoint(new Vector2(u * face.X, v * face.Y), face);
        var grow = ReducedMotion ? 1f : 1f + 2.2f * MathF.Sin(MathF.PI * (float)(EmphasisAge % EmphasisSeconds / EmphasisSeconds)); // up for two seconds, back down for two, again and again
        var halfW = part.HalfW * grow; var halfH = part.HalfH * grow;
        var corners = new[] { At(part.U - halfW, part.V - halfH), At(part.U + halfW, part.V - halfH), At(part.U + halfW, part.V + halfH), At(part.U - halfW, part.V + halfH) };
        if (!ReducedMotion)
        {
            var uv = new[] { new Vector2(part.U - part.HalfW, part.V - part.HalfH), new Vector2(part.U + part.HalfW, part.V - part.HalfH), new Vector2(part.U + part.HalfW, part.V + part.HalfH), new Vector2(part.U - part.HalfW, part.V + part.HalfH) };
            DrawColoredPolygon(corners, Colors.White, uv, texture);
        }
        DrawPolyline(corners.Append(corners[0]).ToArray(), Palette.Gold, ReducedMotion ? 3f : 2.5f, true);
    }
    public bool QuietSelection { get; set; }
    public bool LegalTarget { get; set; }
    public bool ChosenTarget { get; set; }
    public bool DecisionSelected { get; set; }
    public bool? PreviousBowed { get; set; }
    private float _bowAngle;
    private float _bowDestination;
    public float VisualBowAngle => _bowAngle;
    public bool UprightPreview { get; set; }
    public bool CelestialMount { get; set; }
    public float VisualFaceAngle => _bowAngle + RestAngle + (!UprightPreview && FallenArt.TurnsOnTable(Card) ? Mathf.Pi : 0);
    public bool SelectedSource { get; set; }
    public bool Compact { get; set; }
    public float FaceHeight { get; set; }
    public float RestAngle { get; set; }
    public Func<Vector2, Vector2>? ProjectToScreen { get; set; }
    public bool ReducedMotion { get; set; }
    public int? ContextualGoldProduction { get; set; }
    public string ContextHelp { get; set; } = "";
    private static Texture2D? _atlas;
    private static Texture2D? _dynastyBack;
    private bool _hover, _enterPending, _exitDeferred;
    private Action<CardView>? _inspected;
    private static ulong _lastPointerMotion;
    private static CardTile? _hoveredTile;
    private static string? _hoveredCardId; // a rebuilt tile for the same card keeps the hover the pointer already had
    private static bool RecentPointerMotion() => Time.GetTicksMsec() - _lastPointerMotion < 150;
    private void EnterHover()
    {
        _enterPending = false; _exitDeferred = false;
        if (_hoveredTile is { } previous && previous != this && IsInstanceValid(previous)) previous.LeaveHover();
        _hoveredTile = this; _hoveredCardId = Card.Id; _hover = true; QueueRedraw(); _inspected?.Invoke(Card);
    }
    private void LeaveHover()
    {
        _exitDeferred = false; _hover = false; if (_hoveredTile == this) { _hoveredTile = null; _hoveredCardId = null; }
        QueueRedraw();
    }
    /// <summary>Issue #635: set from outside (a hovered row in a target/performer list) to show this card exactly as if the pointer were over it; it fires no inspection.</summary>
    public bool ForcedHover { get => _forcedHover; set { if (_forcedHover == value) return; _forcedHover = value; QueueRedraw(); } }
    private bool _forcedHover;
    private bool Hovered => _hover || ForcedHover;
    private float _lift;
    private float _hoverScale = 1;
    private StyleBox? _discardNotch;
    public float InteractionScale => _hoverScale;
    public bool IsInspectionHover => Hovered || HasFocus();
    public bool IsSelected => Highlighted || SelectedSource || ChosenTarget || DecisionSelected;
    public static readonly Color HoverColor = new("ffda89");
    public static readonly Color SelectionColor = new("9ceafa");
    // Capped LRU (issue #546): unbounded before this, PublishedArt held every distinct card texture drawn on the
    // board/hand for the process lifetime. Mirrors CatalogueScans' cache so a long session with many decks/games
    // doesn't grow this without bound.
    private const int PublishedArtCap = 200;
    private static readonly Dictionary<string, Texture2D> PublishedArt = [];
    private static readonly LinkedList<string> PublishedArtOrder = [];
    public static Texture2D? Artwork(CardDefinition definition)
    {
        if (definition.ScenarioOnly) return Atlas;
        if (definition.ArtworkKey is not string path) return null;
        if (PublishedArt.TryGetValue(path, out var texture)) { PublishedArtOrder.Remove(path); PublishedArtOrder.AddLast(path); return texture; }
        // The art pack is the shipped source; the JPG is the development fallback (the export leaves it out).
        texture = CardFaces.Get(path) ?? (ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null);
        if (texture is null) return null;
        PublishedArt[path] = texture;
        PublishedArtOrder.AddLast(path);
        while (PublishedArt.Count > PublishedArtCap && PublishedArtOrder.First is { } oldest) { PublishedArt.Remove(oldest.Value); PublishedArtOrder.RemoveFirst(); }
        return texture;
    }

    public CardTile() { }
    public CardTile(CardView card, bool compact, Action<CardView> clicked, Action<CardView> inspected)
    {
        Card = card;
        Compact = compact;
        Flat = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Material = CardPaper.Table;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = card.Definition is null ? "Face-down card. Its identity is hidden." : $"{card.Definition.Name}\n{card.Definition.Type} · {card.Definition.GoldCost} Gold\n{card.Definition.Text}";
        if (CreatedCopyStatus.VisibleOn(card)) TooltipText += "\n" + CreatedCopyStatus.Description(card);
        Pressed += () => clicked(Card);
        // A steady hover (playtest 2026-10): cards moving under a stationary pointer (flights, cleanup, refills) must not make the enlarged card pop up and down. Only real pointer
        // motion starts or ends a hover; a card that slides in or out beneath a still pointer waits for the next movement.
        _inspected = inspected;
        MouseEntered += () => { if (RecentPointerMotion() || _hoveredCardId == Card.Id) EnterHover(); else _enterPending = true; };
        MouseExited += () => { _enterPending = false; if (_hover && !RecentPointerMotion()) _exitDeferred = true; else LeaveHover(); };
        GuiInput += e => { if (e is InputEventMouseMotion) { _lastPointerMotion = Time.GetTicksMsec(); if (_enterPending) EnterHover(); } };
        FocusEntered += () => { QueueRedraw(); inspected(Card); };
        FocusExited += QueueRedraw;
        Resized += QueueRedraw;
    }

    public override void _Ready()
    {
        if (CreatedCopyStatus.VisibleOn(Card)) { CopySeal = new CreatedCopySeal { CardAnchor = this }; AddChild(CopySeal); }
        if (!UprightPreview && ActionLockStatus.VisibleOn(Card))
        {
            RestrictionSeal = new ActionLockSeal { CardAnchor = this }; AddChild(RestrictionSeal);
        }
        if (ShowTokenRail && CardTokenRail.HasRail(Card))
        {
            AlignUnitRightEdge = true;
            TokenRail = new CardTokenRail { CardAnchor = this, Offset = TokenRailOffset, Companions = TokenRailCompanions };
            AddChild(TokenRail);
        }
        _bowDestination = Card.Bowed ? Mathf.Pi / 2 : 0;
        _bowAngle = (PreviousBowed ?? Card.Bowed) ? Mathf.Pi / 2 : 0;
        if (ReducedMotion) _bowAngle = _bowDestination;
    }

    public override void _Process(double delta)
    {
        if (_exitDeferred && Input.GetLastMouseVelocity().LengthSquared() > 1) { _exitDeferred = false; if (!_HasPoint(GetLocalMousePosition())) LeaveHover(); }
        AdvanceStatFeedback(delta);
        // The rail cap must stay under the pointer. Enlarging its parent moves this
        // small target into/out of the card face and repeatedly restarts the tooltip.
        var inspecting = (Hovered || HasFocus() && !DecisionSelected) && TokenRail?.GrantSeal?.IsHovered() != true && RestrictionSeal?.IsHovered() != true && CopySeal?.IsHovered() != true;
        var targetLift = inspecting ? Card.Zone is Zone.Hand or Zone.Resolving ? 40f : ProjectToScreen is null ? 4f : 0f : DecisionSelected ? 18f : 0f;
        var targetScale = inspecting ? Card.Zone is Zone.Hand or Zone.Resolving ? 1.22f : 1.06f : 1f;
        // An attachment lies under its Personality: selecting or focusing it must not lift it over the very card it is attached to (only a deliberate hover does).
        ZIndex = Hovered ? 70 : Card.AttachedTo is not null ? 0 : HasFocus() ? 20 : IsSelected ? 10 : 0;
        if (Mathf.IsEqualApprox(_bowAngle, _bowDestination) && Mathf.IsEqualApprox(_lift, targetLift) && Mathf.IsEqualApprox(_hoverScale, targetScale)) return;
        _bowAngle = ReducedMotion ? _bowDestination : Mathf.MoveToward(_bowAngle, _bowDestination, (float)delta * Mathf.Pi * 3);
        // Reduced motion preserves visible feedback and makes its transition immediate.
        _lift = ReducedMotion ? targetLift : Mathf.MoveToward(_lift, targetLift, (float)delta * 300);
        _hoverScale = ReducedMotion ? targetScale : Mathf.MoveToward(_hoverScale, targetScale, (float)delta * 1.65f);
        QueueRedraw();
    }

    private Vector2 FaceSize()
    {
        var height = FaceHeight > 0 ? FaceHeight : Math.Min(Size.X, Size.Y) - 12;
        var art = Card?.Definition is { ScenarioOnly: false } printed ? Artwork(printed) : null;
        return new Vector2(height * (art is null ? .7142857f : art.GetWidth() / (float)art.GetHeight()), height);
    }

    // The same projected quadrilateral drives drawing, hit testing and arrows.
    // Bow rotation takes place in the table plane before camera foreshortening.
    public bool AlignUnitRightEdge { get; set; }
    public bool ShowTokenRail { get; set; } = true;
    public float TokenRailOffset { get; set; }
    public CardTile[] TokenRailCompanions { get; set; } = [];
    public Func<string, string?>? TokenSourceName { get; set; }
    public CardTokenRail? TokenRail { get; private set; }
    public CreatedCopySeal? CopySeal { get; private set; }
    public ActionLockSeal? RestrictionSeal { get; private set; }

    private Vector2 ProjectFacePoint(Vector2 point, Vector2 face)
    {
        var p = ((point - face / 2) * _hoverScale).Rotated(VisualFaceAngle);
        var center = Size / 2;
        if (AlignUnitRightEdge)
        {
            var angle = VisualFaceAngle;
            var extent = (Math.Abs(Mathf.Cos(angle)) * face.X + Math.Abs(Mathf.Sin(angle)) * face.Y) * _hoverScale / 2;
            center.X = Size.X - 6 - extent;
        }
        return center + new Vector2(0, -_lift) + p;
    }

    private Vector2[] LocalFaceCorners(float grow = 0)
    {
        var face = FaceSize();
        return new[] { new Vector2(-grow, -grow), new Vector2(face.X + grow, -grow), face + Vector2.One * grow, new Vector2(-grow, face.Y + grow) }
            .Select(p => ProjectFacePoint(p, face)).ToArray();
    }

    public override bool _HasPoint(Vector2 point)
    {
        if (Geometry2D.IsPointInPolygon(point, LocalFaceCorners(2))) return true;
        if (!_hover || Card.Zone is not (Zone.Hand or Zone.Resolving)) return false;
        // Keep the original pick area while lifting. Otherwise a pointer on the
        // bottom edge repeatedly enters/exits as the card moves away from it.
        return ContainsRestingFace(point);
    }

    internal bool ContainsRestingFace(Vector2 point)
    {
        var resting = (point - Size / 2).Rotated(-VisualFaceAngle) + FaceSize() / 2;
        return new Rect2(Vector2.Zero, FaceSize()).Grow(2).HasPoint(resting);
    }

    public Vector2[] FaceCornersInCanvas() => LocalFaceCorners().Select(p => GetGlobalTransform() * p).ToArray();

    public Vector2[] FaceCornersOnViewport() => LocalFaceCorners().Select(p => ProjectToScreen?.Invoke(GetGlobalTransform() * p) ?? GetGlobalTransform() * p).ToArray();

    private void DrawInteractionHalo()
    {
        var pointed = Pointed && !IsSelected;
        if (!IsSelected && !IsInspectionHover && !LegalTarget && !pointed) return;
        if (QuietSelection && !pointed) return;
        var color = DecisionSelected ? Palette.Paper : IsSelected ? SelectionColor : pointed ? Palette.Gold : HoverColor;
        var strength = IsSelected ? 1f : pointed ? (ReducedMotion ? .9f : .7f + .3f * MathF.Sin(Time.GetTicksMsec() / 260f)) : IsInspectionHover ? .82f : .28f;
        var edge = LocalFaceCorners(2);
        var closed = edge.Append(edge[0]).ToArray();
        // Broad soft light with a bright core; no solid fill over the original scan.
        var perspectiveWeight = ProjectToScreen is null ? 1f : 2.2f;
        foreach (var (width, alpha) in new[] { (28f, .07f), (20f, .12f), (13f, .22f), (8f, .40f) })
            DrawPolyline(closed, color with { A = alpha * strength }, width * perspectiveWeight, true);
    }

    private void DrawInteractionEdge()
    {
        var active = IsSelected || IsInspectionHover || LegalTarget || Pointed;
        var color = DecisionSelected ? Palette.Paper : Pointed && !IsSelected ? Palette.Gold : IsSelected ? QuietSelection ? Palette.Gold : SelectionColor : IsInspectionHover || LegalTarget ? HoverColor : new Color("8e866b");
        var edge = LocalFaceCorners(active ? 1.6f : 1);
        DrawPolyline(edge.Append(edge[0]).ToArray(), color with { A = active ? 1 : .58f }, QuietSelection ? 1.5f : active ? (IsSelected ? 3.2f : 2.3f) * (ProjectToScreen is null ? 1 : 2) : .9f, true);
    }

    private void DrawPrintedFace(Texture2D texture)
    {
        var corners = LocalFaceCorners();
        DrawInteractionHalo();
        if (CelestialMount) SupportCardArt.DrawCelestial(this, corners);
        for (var spread = 3; spread >= 1; spread--)
            DrawColoredPolygon(LocalFaceCorners(spread).Select(p => p + new Vector2(.6f, .8f + _lift * .5f)).ToArray(), new Color(.008f, .006f, .004f, .055f));
        DrawColoredPolygon(corners.Select(p => p + new Vector2(0, .7f)).ToArray(), new Color("a29174"));
        DrawColoredPolygon(corners, Colors.White, new Vector2[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, texture);
        DrawInteractionEdge();
        DrawStatInlays();
        DrawEmphasis(texture);
        DrawPublicStatus();
        DrawDishonorMarker();
        if (DecisionSelected) DrawDiscardSelection();
    }

    private void DrawDiscardSelection()
    {
        var corners = LocalFaceCorners(); var top = corners[1]; var bottom = (corners[2] + corners[3]) / 2;
        DrawCircle(top, 18, Palette.Ink); DrawArc(top, 18, 0, Mathf.Tau, 40, Palette.Gold, 2, true);
        DrawPolyline([top + new Vector2(-8, 0), top + new Vector2(-2, 6), top + new Vector2(9, -8)], Palette.Gold, 3, true);
        var rect = new Rect2(bottom + new Vector2(-78, 3), new Vector2(156, 30));
        DrawStyleBox(_discardNotch ??= SidebarSkin.Surface(true), rect);
        DrawString(Palette.Body, rect.Position + new Vector2(0, 22), "Fate discard", HorizontalAlignment.Center, rect.Size.X, 18, Palette.Paper);
    }

    public string[] PublicStatusLabels => Card.Definition is null || Card.Zone is not (Zone.Home or Zone.Battlefield or Zone.Attached) ? [] :
        (Card.GrantedKeywords ?? []).Where(k => !CardChanges.IsPermanent(k.Duration) && k.CopiedFrom is null).Select(k => "+ " + k.Keyword).Distinct().ToArray();
    private void DrawDishonorMarker()
    {
        if (UprightPreview || !FallenArt.TurnsOnTable(Card)) return;
        var corners = LocalFaceCorners();
        var width = Math.Max(10, FaceSize().Y * .10f);
        // Lower right stays clear of the left/near token rail and upper-right Force tab.
        // The marker remains readable while the physical printed face turns.
        FallenArt.Medallion(this, new Vector2(corners.Max(p => p.X) + 3 + width / 2, corners.Max(p => p.Y) - width / 2), width, true);
    }
    private void DrawPublicStatus()
    {
        var labels = PublicStatusLabels;
        if (labels.Length == 0) return;
        var face = FaceSize();
        var origin = ProjectFacePoint(Vector2.Zero, face);
        var x = (ProjectFacePoint(Vector2.Right, face) - origin);
        var y = (ProjectFacePoint(Vector2.Down, face) - origin);
        DrawSetTransformMatrix(new Transform2D(x, y, origin));
        // Two slim overlay bands across the lower artwork; never cover printed statistics/name/rules.
        var shown = labels.Take(2).ToArray();
        if (labels.Length > 2) shown[1] += $" · {labels.Length - 2} more";
        for (var i = 0; i < shown.Length; i++)
        {
            var rect = new Rect2(face.X * .035f, face.Y * .395f + i * face.Y * .08f, face.X * .93f, face.Y * .074f);
            var dishonorable = shown[i] == "Dishonorable";
            var statusHeading = shown[i].ToUpperInvariant();
            DrawStyleBox(Palette.Box(dishonorable ? new Color("492924") : new Color("202f33"), dishonorable ? new Color("b37c67") : Palette.Gold, 0, 1), rect);
            var size = Math.Max(11, (int)(face.Y * .06f));
            while (size > 8 && Palette.Title.GetStringSize(statusHeading, fontSize: size).X > rect.Size.X - 6) size--;
            DrawString(Palette.Title, rect.Position + new Vector2(3, rect.Size.Y * .80f), statusHeading, HorizontalAlignment.Center, rect.Size.X - 6, size, Palette.Paper);
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }
    public static Texture2D? Atlas
    {
        get
        {
            if (_atlas is null && ResourceLoader.Exists("res://Art/training-atlas.png")) _atlas = GD.Load<Texture2D>("res://Art/training-atlas.png");
            return _atlas;
        }
    }

    public static Rect2 ArtRegion(CardDefinition def, Texture2D atlas)
    {
        if (!def.ScenarioOnly) return new Rect2(Vector2.Zero, atlas.GetSize());
        var quadrant = def.Type == CardType.Personality ? (def.Clan == "Lion" ? 1 : 0) : def.Type == CardType.Holding ? 2 : 3;
        var half = atlas.GetSize() / 2;
        return new Rect2(new Vector2(quadrant % 2 * half.X, quadrant / 2 * half.Y), half);
    }

    // The engine numbers a Personality when it first enters play (CardView.UnitNumber, issue #611); the card id is opaque and means nothing here. Empty before that.
    public static string UnitLabel(CardView card) => card.UnitNumber > 0 ? "Unit " + card.UnitNumber : "";

    internal static string? LastPreviewCardId;
    internal static Control? LastPreviewControl;
    public override Control _MakeCustomTooltip(string forText) => HasMeta("flat_tooltip") ? null! : CreateReadingPreview();

    public Control CreateReadingPreview()
    {
        LastPreviewCardId = Card.Id;
        return LastPreviewControl = CardPreview.Create(Card, ContextHelp, ContextualGoldProduction, GetTree().Root.GetVisibleRect().Size.Y / 1080f, TokenSourceName);
    }

    public override void _Draw()
    {
        if (Card is null || Size.X <= 1 || Size.Y <= 1) return;
        // Reserve the same square footprint in either orientation, including the transition.
        var height = FaceHeight > 0 ? FaceHeight : Math.Min(Size.X, Size.Y) - 12;
        var original = Card.Definition is { ScenarioOnly: false } printed ? Artwork(printed) : null;
        if (original is not null) { DrawPrintedFace(original); return; }
        if (Card.Definition is null)
        {
            _dynastyBack ??= GD.Load<Texture2D>("res://Art/original-dynasty-back.png");
            if (_dynastyBack is not null) { DrawPrintedFace(_dynastyBack); return; }
        }
        var ratio = original is null ? .7142857f : original.GetWidth() / (float)original.GetHeight();
        var faceSize = new Vector2(height * ratio, height);
        DrawInteractionHalo();
        DrawInteractionEdge();
        DrawSetTransformMatrix(new Transform2D(Vector2.Right, Vector2.Down, Size / 2 + new Vector2(0, -_lift)) * Transform2D.Identity.Rotated(VisualFaceAngle).ScaledLocal(Vector2.One * _hoverScale).TranslatedLocal(-faceSize / 2));
        var r = new Rect2(Vector2.Zero, faceSize);
        var border = SelectedSource || ChosenTarget ? Palette.Crane : Highlighted || LegalTarget || Hovered || HasFocus() ? Palette.Gold : new Color("8e866b");
        // Broad penumbra, close contact shadow, and a thin paper edge. The original
        // scan keeps its printed content; paper grading is a reversible material.
        for (var spread = 3; spread >= 1; spread--)
            DrawStyleBox(Palette.Box(new Color(.01f, .009f, .006f, .045f), null, 0, 3), new Rect2(new Vector2(2 + _lift * .3f, 3 + _lift * .6f), faceSize).Grow(spread));
        DrawStyleBox(Palette.Box(new Color(.012f, .01f, .006f, .48f), null, 0, 2), new Rect2(new Vector2(1.5f, 2.5f), faceSize));
        DrawRect(new Rect2(new Vector2(0, 1.4f), faceSize), new Color("b0a185"));
        if (Card.Definition is null)
        {
            _dynastyBack ??= GD.Load<Texture2D>("res://Art/original-dynasty-back.png");
            if (_dynastyBack is not null)
            {
                DrawTextureRect(_dynastyBack, r, false);
                if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || Hovered || HasFocus()) DrawRect(r.Grow(2), border, false, 1.5f, true);
                return;
            }
            DrawStyleBox(Palette.Box(Palette.Ink, border, 0), r);
            DrawRect(new Rect2(7, 7, faceSize.X - 14, faceSize.Y - 14), new Color("4a624c"), false, 1);
            var center = faceSize / 2;
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.Tau / 5;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * faceSize.X * .11f;
                DrawArc(point, faceSize.X * .15f, 0, Mathf.Tau, 48, new Color("94885e"), 1.4f, true);
            }
            DrawString(Palette.Body, new Vector2(0, faceSize.Y - 14), "DYNASTY", HorizontalAlignment.Center, faceSize.X, height < 110 ? 8 : 11, Palette.PaperDim);
            return;
        }
        var d = Card.Definition;
        if (original is not null)
        {
            // Printed borders, statistics and rules are part of the original card.
            // Never crop, tint for bowing, or paint replacement text over the scan.
            DrawTextureRect(original, r, false);
            DrawRect(r.Grow(1), border with { A = .65f }, false, 1, true);
            if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || Hovered || HasFocus())
            {
                DrawRect(r.Grow(3), border with { A = .20f }, false, 5, true);
                DrawRect(r.Grow(2), border, false, 1.6f, true);
            }
            return;
        }
        DrawStyleBox(Palette.Box(Palette.Paper, border, 0), r);
        var artHeight = height < 110 ? faceSize.Y * .34f : Compact ? faceSize.Y * .42f : faceSize.Y * .48f;
        var art = new Rect2(6, 6, faceSize.X - 12, artHeight);
        if (Artwork(d) is { } texture)
            DrawTextureRectRegion(texture, art, ArtRegion(d, texture), Card.Bowed ? new Color(.68f, .7f, .66f, 1) : Colors.White);
        else
        {
            DrawRect(art, new Color("6b7963"));
            for (var i = 0; i < 5; i++) DrawArc(art.GetCenter(), (i + 1) * 7, .2f, 5.8f, 44, Palette.PaperDim, 1, true);
        }
        if (height < 110)
        {
            var wordsSmall = d.Name.ToUpperInvariant().Split(' '); var smallLines = new List<string>(); var current = "";
            foreach (var word in wordsSmall)
            {
                var next = current.Length == 0 ? word : current + " " + word;
                if (Palette.Title.GetStringSize(next, HorizontalAlignment.Left, -1, 10).X > faceSize.X - 10 && current.Length > 0) { smallLines.Add(current); current = word; }
                else current = next;
            }
            if (current.Length > 0) smallLines.Add(current);
            for (var i = 0; i < Math.Min(2, smallLines.Count); i++) DrawString(Palette.Title, new Vector2(5, art.End.Y + 11 + i * 10), smallLines[i], HorizontalAlignment.Left, faceSize.X - 10, 10, Palette.Ink);
            if (d.GoldProduction > 0) DrawString(Palette.Body, new Vector2(5, faceSize.Y - 4), $"{ContextualGoldProduction ?? d.GoldProduction} GOLD", HorizontalAlignment.Left, faceSize.X - 10, 9, Palette.Ink);
            if (Card.Bowed) { DrawRect(new Rect2(3, 4, 36, 12), Palette.Ink); DrawString(Palette.Body, new Vector2(5, 13), "BOWED", HorizontalAlignment.Left, 34, 8, Palette.Paper); }
            if (Card.Zone is Zone.Hand or Zone.Province && d.GoldCost > 0)
            {
                DrawCircle(new Vector2(faceSize.X - 10, 12), 8, Palette.Gold);
                DrawString(Palette.Body, new Vector2(faceSize.X - 17, 16), d.GoldCost.ToString(), HorizontalAlignment.Center, 14, 10, Palette.Ink);
            }
            if (SelectedSource || ChosenTarget) { DrawCircle(new Vector2(faceSize.X - 9, 11), 7, Palette.Crane); DrawString(Palette.Body, new Vector2(faceSize.X - 14, 15), "✓", HorizontalAlignment.Center, 10, 10, Palette.Ink); }
            if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || HasFocus()) DrawRect(new Rect2(1, 1, faceSize.X - 2, faceSize.Y - 2), border, false, 2);
            return;
        }
        var nameSize = height < 110 ? 10 : Compact ? 12 : 15;
        var nameY = art.End.Y + (height < 110 ? 11 : Compact ? 16 : 20);
        var words = d.Name.ToUpperInvariant().Split(' ');
        var lines = new List<string>();
        var line = "";
        foreach (var word in words)
        {
            var proposed = line.Length == 0 ? word : line + " " + word;
            if (Palette.Title.GetStringSize(proposed, HorizontalAlignment.Left, -1, nameSize).X > faceSize.X - 14 && line.Length > 0) { lines.Add(line); line = word; }
            else line = proposed;
        }
        if (line.Length > 0) lines.Add(line);
        foreach (var text in lines.Take(2))
        {
            DrawString(Palette.Title, new Vector2(7, nameY), text, HorizontalAlignment.Left, faceSize.X - 14, nameSize, Palette.Ink);
            nameY += nameSize + 1;
        }
        if (!Compact)
            DrawString(Palette.Body, new Vector2(7, faceSize.Y - 40), d.Type.ToString().ToUpperInvariant(), HorizontalAlignment.Left, faceSize.X - 14, 10, new Color("69715f"));
        var statText = d.Type switch
        {
            CardType.Personality => $"{Card.Force} F   {Card.Chi} C   {d.PersonalHonor} H",
            CardType.Stronghold => $"{ContextualGoldProduction ?? d.GoldProduction} GOLD",
            CardType.Holding => $"{ContextualGoldProduction ?? d.GoldProduction} GOLD",
            _ => d.Type.ToString().ToUpperInvariant()
        };
        DrawLine(new Vector2(7, faceSize.Y - 27), new Vector2(faceSize.X - 7, faceSize.Y - 27), new Color("aea58a"), 1);
        DrawString(Palette.Body, new Vector2(7, faceSize.Y - 10), statText, HorizontalAlignment.Left, faceSize.X - 14, height < 110 ? 9 : Compact ? 11 : 13, Palette.Ink);
        if (Card.Zone is Zone.Province or Zone.Hand && d.GoldCost > 0)
        {
            DrawCircle(new Vector2(faceSize.X - 18, 18), 13, Palette.Gold);
            DrawString(Palette.Body, new Vector2(faceSize.X - 28, 23), d.GoldCost.ToString(), HorizontalAlignment.Center, 20, 14, Palette.Ink);
        }
        if (Card.Bowed)
        {
            DrawStyleBox(Palette.Box(Palette.Ink, null, 0), new Rect2(4, 8, Math.Min(62, faceSize.X - 8), 20));
            DrawString(Palette.Body, new Vector2(7, 22), "BOWED", HorizontalAlignment.Left, faceSize.X - 14, 11, Palette.Paper);
        }
        if (SelectedSource)
        {
            DrawCircle(new Vector2(faceSize.X - 15, 17), 11, Palette.Crane);
            DrawString(Palette.Body, new Vector2(faceSize.X - 24, 22), "✓", HorizontalAlignment.Center, 18, 14, Palette.Ink);
        }
        if (d.Type == CardType.Personality && Card.UnitNumber > 0)
        {
            var label = UnitLabel(Card);
            DrawStyleBox(Palette.Box(Palette.Ink, null, 0), new Rect2(5, art.End.Y - 17, 63, 17));
            DrawString(Palette.Body, new Vector2(8, art.End.Y - 5), label, HorizontalAlignment.Left, 61, 10, Palette.Paper);
        }
        if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || HasFocus()) DrawRect(new Rect2(1, 1, faceSize.X - 2, faceSize.Y - 2), border, false, 2);
    }
}
