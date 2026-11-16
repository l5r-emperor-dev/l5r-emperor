using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<int, Rect2> _persistentBounds = [];
    private readonly Dictionary<int, float> _persistentHeights = [];
    private readonly List<HomeReminder> _homeReminders = [];
    private (int Owner, string? EffectId)? _selectedHomeReminder;
    private float _persistentWidth;
    private const float FavorWidth = 218, FavorHeight = 306, PersistentGap = 16, EffectHeight = 96;

    private static bool IsPersistentHomeCard(CardView card) => card.Zone == Zone.Home && card.AttachedTo is null &&
        card.Definition is { } definition && definition.Type is not (CardType.Personality or CardType.Holding or CardType.Region or CardType.Stronghold or CardType.Follower or CardType.Item or CardType.Spell) &&
        !(definition.Keywords ?? []).Contains("Terrain", StringComparer.OrdinalIgnoreCase);
    private static CardView[] PersistentCards(PlayerView player) => (player.Celestials ?? []).Concat(player.Home.Where(IsPersistentHomeCard)).ToArray();
    private static CardView[] EffectReminders(PlayerView player) => (player.EventsInPlay ?? []).Where(c => c.Definition is not null).ToArray();
    private float PersistentCardWidth(CardView card) => CardFootprint(card, CardHeight) + (card.Zone == Zone.Celestial ? 34 : 0);
    private static float PersistentRowHeight(CardView[] cards) => CardHeight + cards.Select(c => PrintedActionStrip.ReservedHeight(c, CardHeight)).DefaultIfEmpty(0).Max() + PersistentGap + (cards.Any(c => c.Zone == Zone.Celestial) ? 28 : 0);
    private bool HasPersistentHome(PlayerView player) => View.FavorPlayerId == player.Id || PersistentCards(player).Length > 0 || EffectReminders(player).Length > 0;

    private void MeasurePersistentHome()
    {
        _persistentBounds.Clear(); _persistentHeights.Clear(); _persistentWidth = 0;
        foreach (var player in View.Players)
        {
            if (!HasPersistentHome(player)) { _persistentHeights[player.Id] = 0; continue; }
            var cards = PersistentCards(player);
            var favor = View.FavorPlayerId == player.Id;
            var cardWidth = cards.Select(PersistentCardWidth).DefaultIfEmpty(158).Max();
            var columns = favor ? 2 : 3;
            var count = Math.Min(columns, cards.Length);
            var cardsWidth = count * cardWidth + Math.Max(0, count - 1) * PersistentGap;
            var reminders = EffectReminders(player).Length;
            var rightWidth = Math.Max(cardsWidth, reminders > 0 ? 300 : 0);
            var width = (favor ? FavorWidth + (rightWidth > 0 ? PersistentGap : 0) : 0) + rightWidth;
            var rows = (int)Math.Ceiling(cards.Length / (float)columns);
            var rightHeight = rows * PersistentRowHeight(cards) + reminders * (EffectHeight + 8);
            _persistentHeights[player.Id] = MatHeader + Math.Max(favor ? FavorHeight : 0, rightHeight) + MatPadding;
            _persistentWidth = Math.Max(_persistentWidth, width + MatPadding * 2);
        }
    }

    private void AddPersistentHome(PlayerView player)
    {
        if (!_persistentBounds.TryGetValue(player.Id, out var bounds)) return;
        var group = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        group.SetMeta("persistent_home_owner", player.Id);
        var x = MatPadding;
        if (View.FavorPlayerId == player.Id)
        {
            var favor = new HomeReminder(player.Id, player.Clan, null, () => SelectHomeReminder(player.Id, null));
            StyleHomeReminder(favor); Place(group, favor, x, MatHeader, FavorWidth, FavorHeight); _homeReminders.Add(favor);
            x += FavorWidth + PersistentGap;
        }
        var cards = PersistentCards(player); var columns = View.FavorPlayerId == player.Id ? 2 : 3;
        var width = cards.Select(PersistentCardWidth).DefaultIfEmpty(158).Max();
        for (var i = 0; i < cards.Length; i++)
        {
            var tile = Tile(cards[i], true, width, CardHeight);
            if (cards[i].Zone != Zone.Celestial) tile.ContextHelp = "Persistent card at home. Select to inspect the actions currently offered by the game.";
            Place(group, tile, x + i % columns * (width + PersistentGap), MatHeader + (cards.Any(c => c.Zone == Zone.Celestial) ? 16 : 0) + i / columns * PersistentRowHeight(cards), width, PlaneHeight(CardHeight));
        }
        var y = MatHeader + (int)Math.Ceiling(cards.Length / (float)columns) * PersistentRowHeight(cards);
        foreach (var effect in EffectReminders(player))
        {
            var reminder = new HomeReminder(player.Id, player.Clan, effect, () => SelectHomeReminder(player.Id, effect.Id));
            StyleHomeReminder(reminder); Place(group, reminder, x, y, Math.Max(300, Math.Min(columns, cards.Length) * (width + PersistentGap) - PersistentGap), EffectHeight);
            _homeReminders.Add(reminder); y += EffectHeight + 8;
        }
    }

    private void StyleHomeReminder(HomeReminder reminder)
    {
        // Inherit the current menu system rather than embedding a second, stale skin.
        for (Node? node = _inspector; node is not null; node = node.GetParent())
            if (node is PanelContainer panel)
            {
                reminder.InspectionTheme = panel.Theme;
                reminder.InspectionSurface = panel.GetThemeStylebox("panel");
                return;
            }
    }

    private void SelectHomeReminder(int owner, string? effectId)
    {
        if (_session.Busy || PresentationBusy) return;
        if (_chosen is not null) { SetNotice("This is a reminder. Cancel the current action to inspect it."); return; }
        if (_selectedHomeReminder == (owner, effectId)) { DeselectCard(); return; }
        _selected = null; _assignmentUnit = null; _sources.Clear(); _selectedHomeReminder = (owner, effectId);
        ReleaseTableFocus(); HideTableTooltip(); Highlight();
        RenderInspector(); RenderPhaseActions(); HighlightHomeReminders();
    }
    private void HighlightHomeReminders()
    {
        foreach (var reminder in _homeReminders)
        { reminder.Selected = _selectedHomeReminder == (reminder.OwnerId, reminder.Effect?.Id); reminder.QueueRedraw(); }
    }
    private bool RenderHomeReminderInspection()
    {
        if (_selectedHomeReminder is not { } selected) return false;
        var owner = View.Players.Single(p => p.Id == selected.Owner);
        var effect = selected.EffectId is null ? null : EffectReminders(owner).FirstOrDefault(c => c.Id == selected.EffectId);
        if (selected.EffectId is null ? View.FavorPlayerId != selected.Owner : effect is null)
        { _selectedHomeReminder = null; HighlightHomeReminders(); return false; }
        AddInspectionCaption(selected.EffectId is null ? "IMPERIAL FAVOR · " + owner.Clan.ToUpperInvariant() : "ONGOING EFFECT · " + owner.Clan.ToUpperInvariant());
        if (effect is null)
        {
            _inspector.AddChild(new TextureRect
            {
                Texture = HomeReminder.FavorFace(owner.Clan),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(0, 350)
            });
            _inspector.AddChild(Palette.Wrapped($"Held by {owner.Clan}. This reminder is not a card in play.", 16, Palette.PaperDim));
        }
        else
        {
            _inspector.AddChild(Palette.Wrapped(effect.Definition!.Name, 24));
            _inspector.AddChild(CardPreview.Face(effect, 300));
            _inspector.AddChild(Palette.Wrapped("Ongoing effect reminder · not a card in play", 15, Palette.Gold));
            _inspector.AddChild(Palette.Wrapped(effect.Definition.Text, 16, Palette.PaperDim));
        }
        return true;
    }
    private void AddHomeReminderActions()
    {
        if (_selectedHomeReminder is not { EffectId: null } selected || View.FavorPlayerId != selected.Owner || selected.Owner != View.ViewerId) return;
        // Commands, timing, costs and target sets are supplied by the engine, never reconstructed here.
        foreach (var action in Actions.Where(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is "favor-cycle" or "favor-battle" or "favor-cycle-kurohito"))
            AddAction(_inspector, action);
    }
}
