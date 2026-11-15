using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<int, Rect2> _removedBounds = [];
    private void MeasureRemovedPiles()
    {
        _removedBounds.Clear();
        if (!View.Players.Any(p => p.RemovedFromGame.Length > 0)) return;
        // A narrow independent column at the outer right, between the two seats.
        // Never shift the province slots or place a pile over expanding battle courts.
        var x = WorldWidth + 28;
        var center = _worldHeight / 2;
        foreach (var player in View.Players.Where(p => p.RemovedFromGame.Length > 0))
        {
            var bounds = new Rect2(x, center + (player.Id == View.ViewerId ? 12 : -262), 180, 250);
            _removedBounds[player.Id] = bounds;
            _matTargets.Add(new($"removed:{player.Id}", "Removed", bounds));
        }
        WorldWidth = x + 208;
    }
    private void RenderRemovedPiles()
    {
        foreach (var player in View.Players.Where(p => p.RemovedFromGame.Length > 0))
        {
            var bounds = _removedBounds[player.Id];
            var pile = new TablePile(player, Zone.RemovedFromGame, () => ShowRemovedGallery(player.Id));
            pile.AccessibilityName = $"{player.Clan} · Removed from the Game · {player.RemovedFromGame.Length} cards";
            pile.TooltipText = pile.AccessibilityName + "\nBoth players may inspect. Cannot be targeted or returned.";
            Place(_pileHud, pile, bounds.Position.X + 18, bounds.Position.Y + 13, 142, 192);
            var count = Palette.Text($"{player.Clan} · {player.RemovedFromGame.Length}", 18, Palette.Paper);
            count.HorizontalAlignment = HorizontalAlignment.Center; count.MouseFilter = Control.MouseFilterEnum.Ignore;
            Place(_pileHud, count, bounds.Position.X, bounds.End.Y - 30, bounds.Size.X, 28);
        }
    }
    private void ShowRemovedGallery(int owner)
    {
        var (dialog, body) = Dialog("Removed from the Game", 1220, 880);
        HideDialogButtons(dialog); dialog.Borderless = true;
        var header = new HBoxContainer(); body.AddChild(header);
        var heading = Palette.Text("Removed from the Game", 30, Palette.Paper, true); heading.HorizontalAlignment = HorizontalAlignment.Center; heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(heading);
        var close = SidebarSkin.Button("X", dialog.QueueFree);
        foreach (var state in new[] { "normal", "hover", "pressed" }) { var skin = SidebarSkin.Surface(true); skin.ContentMarginLeft = skin.ContentMarginRight = skin.ContentMarginTop = skin.ContentMarginBottom = 4; close.AddThemeStyleboxOverride(state, skin); }
        close.AddThemeFontSizeOverride("font_size", 24); close.AccessibilityName = "Close removed cards"; close.CustomMinimumSize = new Vector2(44, 44); header.AddChild(close);
        var tabs = new HBoxContainer(); body.AddChild(tabs);
        var note = Palette.Wrapped("Cannot be targeted or returned", 18, Palette.PaperDim); note.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(note);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 18); grid.AddThemeConstantOverride("v_separation", 18); scroll.AddChild(grid);
        var buttons = new Dictionary<int, Button>();
        void Populate(int id)
        {
            Palette.Clear(grid);
            foreach (var pair in buttons) pair.Value.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(pair.Key == id));
            var player = View.Players.Single(p => p.Id == id);
            if (player.RemovedFromGame.Length == 0) { grid.AddChild(Palette.Wrapped("No cards removed.", 20)); return; }
            foreach (var card in player.RemovedFromGame)
            {
                var column = new VBoxContainer(); grid.AddChild(column); column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var tile = new CardTile(card, true, _ => { }, _ => { }) { CustomMinimumSize = new Vector2(230, 328), FaceHeight = CreatedCopyStatus.VisibleOn(card) ? 266 : 322, ReducedMotion = true, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, ContextHelp = player.Clan + " · Removed from the Game · Inspection only" }; column.AddChild(tile);
                var family = card.Definition?.Type is CardType.Personality or CardType.Holding or CardType.Event ? "Dynasty" : "Fate";
                var caption = Palette.Text(CreatedCopyStatus.VisibleOn(card) ? "Created copy · " + family : family, 18, Palette.PaperDim); caption.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(caption);
            }
        }
        foreach (var player in View.Players)
        {
            var button = SidebarSkin.Button($"{player.Clan} ({player.RemovedFromGame.Length})", () => Populate(player.Id)); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; tabs.AddChild(button); buttons[player.Id] = button; var emblem = ClanPlaque.Emblem(player.Clan, new Vector2(42, 42)); button.AddChild(emblem); emblem.Position = new Vector2(22, 12);
        }
        void Fit() { grid.Columns = Math.Clamp((int)(scroll.Size.X / 248), 1, 4); }
        scroll.Resized += Fit;
        var hint = Palette.Text("Hover to enlarge", 16, Palette.PaperDim); hint.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(hint);
        body.AddChild(SidebarSkin.Button("Back to table", dialog.QueueFree)); Populate(owner);
    }
}
