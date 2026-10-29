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
        var stage = new Control { Name = "CardShow", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 60 };
        _root.AddChild(stage);
        stage.Size = new Vector2(620, height + 120); stage.Position = CardPlayTiming.Center - new Vector2(stage.Size.X / 2, height / 2);
        var face = CardPreview.Face(card, height, width);
        face.MouseFilter = Control.MouseFilterEnum.Ignore;
        face.Size = new Vector2(width, height); face.Position = new Vector2((stage.Size.X - width) / 2, 0);
        stage.AddChild(face);
        var caption = Palette.Text(card.Definition!.Name, 17, Palette.Gold);
        caption.HorizontalAlignment = HorizontalAlignment.Center; caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        caption.Position = new Vector2(10, height + 14); caption.Size = new Vector2(stage.Size.X - 20, 30);
        stage.AddChild(caption);
        _cardShow = stage;
    }

    private void CloseCardShow()
    {
        if (_cardShow is { } stage && IsInstanceValid(stage)) stage.QueueFree();
        _cardShow = null;
    }

    /// <summary>While a card is on show, a press inside it is kept from the table and a press anywhere else closes it (and is not also a table click).</summary>
    private bool CardShowSwallows(InputEvent @event)
    {
        if (!CardShowOpen) return false;
        if (@event is InputEventMouseButton { Pressed: true } press)
        {
            var face = _cardShow!.GetChildren().OfType<CardTile>().FirstOrDefault();
            if (face is null || !face.GetGlobalRect().HasPoint(press.Position)) CloseCardShow();
            return true;
        }
        return @event is InputEventMouseButton or InputEventMouseMotion;
    }
}
