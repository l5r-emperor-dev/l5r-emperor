using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Issue #1169: the battle sidebar's action record. Presents View.BattleActions (the engine's own record of every action, Reaction and pass of the battle) one entry at a time, the
    // newest in view; the arrows step back and forward through the battle. Presentation only: nothing here reads engine internals or decides what an action did.
    private int _battleActionCursor, _battleActionCount;
    private bool _battleActionFollow = true;
    private const float BattleActionShare = .33f; // the record never takes more than this share of the sidebar's height

    private void RenderBattleActionPanel()
    {
        var actions = View.BattleActions ?? [];
        if (actions.Length != _battleActionCount) { _battleActionFollow = actions.Length > _battleActionCount || actions.Length == 0 || _battleActionFollow; _battleActionCount = actions.Length; }
        if (_battleActionFollow || _battleActionCursor >= actions.Length) _battleActionCursor = Math.Max(0, actions.Length - 1);
        var budget = Math.Max(190f, GetViewport().GetVisibleRect().Size.Y * BattleActionShare);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, actions.Length == 0 ? 0 : budget), ClipContents = true };
        panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);

        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 8); column.AddChild(header);
        var title = Palette.Text(actions.Length == 0 ? "Battle actions" : $"Action {_battleActionCursor + 1} of {actions.Length}", 17, Palette.Gold);
        title.AddThemeFontOverride("font", SidebarSkin.Bold); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(title);
        if (actions.Length == 0)
        {
            column.AddChild(Palette.Wrapped("No action has been taken in this battle yet.", 15, Palette.PaperDim));
            _inspector.AddChild(panel); return;
        }
        var back = SidebarSkin.Button("‹", () => StepBattleActions(-1)); var forward = SidebarSkin.Button("›", () => StepBattleActions(1));
        foreach (var arrow in new[] { back, forward }) arrow.CustomMinimumSize = new Vector2(52, 40);
        back.Disabled = _battleActionCursor <= 0; forward.Disabled = _battleActionCursor >= actions.Length - 1;
        back.TooltipText = "Earlier action"; forward.TooltipText = "Later action";
        header.AddChild(back); header.AddChild(forward);

        var entry = actions[_battleActionCursor];
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 12); column.AddChild(body);
        var faceHeight = Math.Max(120f, budget - 84);
        body.AddChild(BattleActionFace(entry, faceHeight));
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipContents = true }; details.AddThemeConstantOverride("separation", 3); body.AddChild(details);
        var clan = View.Players.FirstOrDefault(p => p.Id == entry.PlayerId)?.Clan ?? entry.PlayerName;
        var who = Palette.Wrapped($"{clan}{(entry.PlayerId == View.ViewerId ? " (you)" : "")} · {entry.Kind}", 16, Palette.Gold); who.AddThemeFontOverride("font", SidebarSkin.Bold); details.AddChild(who);
        if (entry.Kind == "Pass") details.AddChild(Palette.Wrapped("Passed.", 16, Palette.Paper));
        else
        {
            details.AddChild(Palette.Wrapped(entry.Source?.Definition?.Name ?? "Imperial Favor", 17, Palette.Paper));
            if (entry.Performer is { } performer) details.AddChild(Palette.Wrapped("Performer: " + performer, 15, Palette.PaperDim));
            if (entry.Targets.Length > 0) details.AddChild(Palette.Wrapped((entry.Targets.Length == 1 ? "Target: " : "Targets: ") + string.Join(", ", entry.Targets), 15, Palette.PaperDim));
            if (entry.Chosen.Length > 0) details.AddChild(Palette.Wrapped("Chose: " + string.Join(", ", entry.Chosen), 15, Palette.PaperDim));
            details.AddChild(Palette.Wrapped(entry.Brief, 15, Palette.Paper));
        }
        _inspector.AddChild(panel);
    }

    private void StepBattleActions(int direction)
    {
        var count = View.BattleActions?.Length ?? 0;
        _battleActionCursor = Math.Clamp(_battleActionCursor + direction, 0, Math.Max(0, count - 1));
        _battleActionFollow = _battleActionCursor >= count - 1; // stepping to the newest resumes following new actions
        RenderInspector();
    }

    /// <summary>The card an action comes from (clickable, opens the full face), or the Pass plaque for a pass.</summary>
    private Control BattleActionFace(BattleActionView entry, float height)
    {
        var width = height / 1.4f;
        if (entry.Source?.Definition is null) return PassPlaque(width, height);
        var button = new Button { Flat = true, CustomMinimumSize = new Vector2(width, height), FocusMode = Control.FocusModeEnum.None, TooltipText = "Show " + entry.Source.Definition.Name };
        var face = CardPreview.Face(entry.Source, height, width); face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); button.AddChild(face);
        var source = entry.Source;
        button.Pressed += () =>
        {
            var (_, body) = Dialog(source.Definition!.Name, 720, 720);
            body.AddChild(CardPreview.Face(source, 510));
            body.AddChild(Palette.Wrapped(source.Definition.Text, 17, Palette.Paper));
        };
        return button;
    }

    // The Pass plate: the game's brass-framed chalkboard plate (as the battle placards use) with PASS lettered on it. A stand-in for dedicated Pass artwork, which the repository does not have yet.
    private static Control PassPlaque(float width, float height)
    {
        var plate = new Control { CustomMinimumSize = new Vector2(width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.Draw += () => PlacardPlate.Draw(plate, plate.Size, false, false);
        PlacardPlate.Text(plate, "PASS", new Rect2(0, height / 2 - 22, width, 44), 30, Palette.Gold, true);
        return plate;
    }
}
