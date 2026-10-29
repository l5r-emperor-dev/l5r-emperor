using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #1138: S shows the card under the pointer (or, failing that, the selected card) large, like the card-played showcase with animations off: it appears
// at once and goes at once. It stays until S, Esc or a click outside the card. Only the viewer's projected CardView is drawn, so a face-down card cannot be shown.
public partial class Main
{
    private Control? _cardShow;

    private bool CardShowOpen => _cardShow is not null && IsInstanceValid(_cardShow);

    /// <summary>Handles S (open or close) from _UnhandledKeyInput; false when S is not ours to take.</summary>
    private bool ToggleCardShow()
    {
        if (CardShowOpen) { CloseCardShow(); return true; }
        if (_playCurrent is not null && IsInstanceValid(_playCurrent)) return false; // a played card is already on show
        var card = CardTile.HoveredCard ?? _selected;
        if (card?.Definition is null) return false;
        OpenCardShow(card);
        return true;
    }

    private void OpenCardShow(CardView card)
    {
        HideTableTooltip();
        var height = CardPlayTiming.CardHeight; var width = height * .72f;
        var stage = new Control { Name = "CardShow", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 60, Modulate = new Color(1, 1, 1, 0) };
        _root.AddChild(stage);
        stage.Size = new Vector2(620, height + 120); stage.Position = CardPlayTiming.Center - new Vector2(stage.Size.X / 2, height / 2);
        var face = CardPreview.Face(card, height, width);
        face.MouseFilter = Control.MouseFilterEnum.Ignore;
        face.Size = new Vector2(width, height); face.Position = new Vector2((stage.Size.X - width) / 2, 0);
        face.PivotOffset = face.Size / 2;
        stage.AddChild(face);
        var caption = Palette.Text(card.Definition!.Name, 17, Palette.Gold);
        caption.HorizontalAlignment = HorizontalAlignment.Center; caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        caption.Position = new Vector2(10, height + 14); caption.Size = new Vector2(stage.Size.X - 20, 30);
        stage.AddChild(caption);
        _cardShow = stage;
        // The card-played showcase's own entrance (fade, and a small grow unless reduced motion is on), but it is never timed: it stays until dismissed.
        var tween = stage.CreateTween();
        if (_reducedMotion) tween.TweenProperty(stage, "modulate:a", 1.0, .15);
        else
        {
            face.Scale = new Vector2(.7f, .7f);
            tween.SetParallel(true);
            tween.TweenProperty(stage, "modulate:a", 1.0, CardPlayTiming.Intro);
            tween.TweenProperty(face, "scale", Vector2.One, CardPlayTiming.Intro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
    }

    private void CloseCardShow()
    {
        if (_cardShow is { } stage && IsInstanceValid(stage)) stage.QueueFree();
        _cardShow = null;
    }

    /// <summary>While a card is on show, a click anywhere closes it, and neither the click nor the pointer's motion reaches the table.</summary>
    private bool CardShowSwallows(InputEvent @event)
    {
        if (!CardShowOpen) return false;
        if (@event is InputEventMouseButton { Pressed: true }) CloseCardShow();
        return @event is InputEventMouseButton or InputEventMouseMotion;
    }
}
