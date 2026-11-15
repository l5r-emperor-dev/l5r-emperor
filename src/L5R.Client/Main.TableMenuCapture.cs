using Godot;
namespace L5R.Client;

// Native check of the table menu's "Return to game": opened from the table's Menu icon and dismissed by a real mouse click, as a player does.
public partial class Main
{
    private bool _tableMenuCaptureStarted;

    private void ProcessTableMenuCapture(double delta)
    {
        _captureClock += delta;
        if (_tableMenuCaptureStarted || _captureClock < 2) return;
        _tableMenuCaptureStarted = true; _ = CaptureTableMenu();
    }

    private AcceptDialog? OpenTableMenu() => GetChildren().OfType<AcceptDialog>().LastOrDefault(d => !d.IsQueuedForDeletion() && d.Title == "The table");

    private async Task CaptureTableMenu()
    {
        try
        {
            var version = View.Version;
            var icon = _menu.GetChildren().OfType<TableIconButton>().Single(b => b.Glyph == TableGlyph.Menu);
            await SupportClick(icon); await EventFrames(10);
            var dialog = OpenTableMenu(); SupportRequire(dialog is { Visible: true }, "The Menu icon did not open the table menu");
            await SupportCapture("-open");
            var button = HudDescendants(dialog!).OfType<Button>().Single(b => b.Text == "Return to game" && b.IsVisibleInTree());
            // The dialog is an embedded window: its controls are positioned in its own (scaled) canvas, so the click goes where the button is drawn on screen.
            var center = button.GetGlobalRect().GetCenter() * dialog!.ContentScaleFactor + dialog.Position;
            GD.Print($"TABLE_MENU: button at {button.GetGlobalRect()} scale {dialog.ContentScaleFactor} window {dialog.Position}/{dialog.Size}; clicking {center}");
            void Push(InputEventMouse input) { input.SetMeta(CaptureMouseTag, true); Input.ParseInputEvent(input); }
            Push(new InputEventMouseMotion { Position = center, GlobalPosition = center }); await EventFrames(4);
            Push(new InputEventMouseButton { Position = center, GlobalPosition = center, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }); await EventFrames(2);
            Push(new InputEventMouseButton { Position = center, GlobalPosition = center, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(12);
            await SupportCapture("-after");
            SupportRequire(OpenTableMenu() is null, "Return to game left the table menu open");
            SupportRequire(View.Version == version && !MenuVisible, "Returning to the game changed the match or left a menu up");
            GD.Print("TABLE_MENU_PASS: a real click on Return to game closes the table menu and leaves the match as it was"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
