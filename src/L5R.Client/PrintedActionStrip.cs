using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Approved numbered strip. Uses only Core's current public status, never computes legality.</summary>
public partial class PrintedActionStrip : Button
{
    public required CardTile CardAnchor { get; init; }
    public static bool VisibleOn(CardView card) => card.FaceUp && card.Definition is not null && card.PrintedActions is { Length: > 0 };
    public static int Rows(CardView card) => VisibleOn(card) ? (card.PrintedActions!.Length + 1) / 2 : 0;
    public static float ReservedHeight(CardView card, float height) => Rows(card) * Math.Max(19, height * .10f) + (VisibleOn(card) ? 8 : 0);
    public static string Number(int index) => index < 10 ? new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[index] : (index + 1).ToString();
    public static string Describe(CardView card) => string.Join("\n\n", (card.PrintedActions ?? []).Select((a, i) => $"{Number(i)} · {(a.Spent ? "USED · limit reached" : "UNUSED · use remains")}\n{a.Timing}: {a.Label}")) + "\n\nA use remaining does not mean the action is legal now. Timing, costs and restrictions still apply.";
    public static Control Explanation(CardView card)
    {
        var box = new VBoxContainer(); box.SetMeta("printed_action_status", card.Id);
        box.AddChild(Palette.Wrapped("PRINTED ACTIONS", 14, Palette.Gold));
        foreach (var (action, i) in card.PrintedActions!.Select((a, i) => (a, i)))
            box.AddChild(Palette.Wrapped($"{Number(i)} · {(action.Spent ? "USED" : "UNUSED")} · {action.Timing}: {action.Label}", 16, action.Spent ? new Color("d6a98c") : Palette.Paper));
        box.AddChild(Palette.Wrapped("Unused means a use remains; timing, costs and restrictions still apply.", 13, Palette.PaperDim));
        return box;
    }
    public override void _Ready()
    {
        Flat = true;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = CardAnchor.Card.Definition!.Name + "\n" + Describe(CardAnchor.Card);
        SetMeta("flat_tooltip", TooltipText); AccessibilityName = TooltipText;
        MouseFilter = CardAnchor.UprightPreview ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        FocusMode = CardAnchor.UprightPreview ? FocusModeEnum.None : FocusModeEnum.All;
        Pressed += () => CardAnchor.EmitSignal(BaseButton.SignalName.Pressed);
        Arrange();
    }
    public override void _Process(double delta) => Arrange();
    private void Arrange()
    {
        var peers = (CardAnchor.ActionStripCompanions.Length > 0 ? CardAnchor.ActionStripCompanions : CardAnchor.TokenRailCompanions).Where(IsInstanceValid).ToArray();
        if (peers.Length == 0) peers = [CardAnchor];
        var inverse = CardAnchor.GetGlobalTransform().AffineInverse();
        var all = peers.SelectMany(c => c.FaceCornersInCanvas().Concat(c.TokenRail is { } rail
            ? new[] { rail.TotalBody.Position, rail.TotalBody.End, new Vector2(rail.TotalBody.End.X, rail.TotalBody.Position.Y), new Vector2(rail.TotalBody.Position.X, rail.TotalBody.End.Y) }.Select(p => rail.GetGlobalTransform() * p) : [])).Select(p => inverse * p).ToArray();
        var face = CardAnchor.FaceCornersInCanvas().Select(p => inverse * p).ToArray();
        var before = peers.Where(c => VisibleOn(c.Card)).OrderBy(c => c.Card.AttachedTo is null ? 0 : 1).ThenBy(c => c.Card.Id, StringComparer.Ordinal).TakeWhile(c => c != CardAnchor)
            .Sum(c => ReservedHeight(c.Card, c.FaceHeight));
        Position = new(face.Min(p => p.X), all.Max(p => p.Y) + CardAnchor.ActionStripBottomPadding + 6 + before);
        Size = new(Math.Max(80, face.Max(p => p.X) - face.Min(p => p.X)), Rows(CardAnchor.Card) * Math.Max(19, CardAnchor.FaceHeight * .10f));
        QueueRedraw();
    }
    public override bool _HasPoint(Vector2 point) => new Rect2(Vector2.Zero, Size).HasPoint(point);
    public Vector2[] CornersOnViewport() => new[] { Vector2.Zero, new Vector2(Size.X, 0), Size, new Vector2(0, Size.Y) }.Select(p => GetGlobalTransform() * p).Select(p => CardAnchor.ProjectToScreen?.Invoke(p) ?? p).ToArray();
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
    public override void _Draw()
    {
        var actions = CardAnchor.Card.PrintedActions!;
        var rowHeight = Size.Y / Rows(CardAnchor.Card);
        for (var i = 0; i < actions.Length; i++)
        {
            var columns = Math.Min(2, actions.Length - i / 2 * 2);
            var rect = new Rect2(i % 2 * Size.X / columns, i / 2 * rowHeight, Size.X / columns, rowHeight);
            DrawRect(rect, actions[i].Spent ? new Color("593323") : Palette.Ink);
            DrawRect(rect, Palette.Gold with { A = .65f }, false, 1);
            var text = Number(i) + (actions[i].Spent ? " USED" : " UNUSED");
            var fs = Math.Clamp((int)(rowHeight * .63f), 10, 17);
            while (fs > 8 && Palette.Body.GetStringSize(text, fontSize: fs).X > rect.Size.X - 6) fs--;
            DrawString(Palette.Body, rect.Position + new Vector2(4, rowHeight * .73f), text, fontSize: fs, modulate: actions[i].Spent ? new Color("e7b699") : Palette.Paper);
        }
        if (HasFocus() || IsHovered()) DrawRect(new Rect2(Vector2.Zero, Size), Palette.Paper, false, 1.5f);
    }
}
