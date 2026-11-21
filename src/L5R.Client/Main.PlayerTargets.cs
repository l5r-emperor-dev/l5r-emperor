using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // "Choose a player" (issue #960). Core offers one option per player for an ability whose target is a player ("Choose a player. He loses 2 Honor"). The player decides it by clicking either player's
    // stronghold or clan plaque, or from the two rows in the sidebar. An ability whose text names the player ("another player", "you") has only one legal option, which is used without asking.
    // The draft never picks a player for them: Confirm waits until one is chosen.

    private static bool IsPlayerTarget(ActionOption o) =>
        o is { Type: "equip" or "playStrategy" or "useAbility", Provinces: null, Movement: null, Attack: null, Command: { TargetPlayerId: not null, AbilityId: not null } } && !NamesItsPlayer(o);

    /// <summary>Issue #1240: Core labels each player's option by its outcome ("You Gain 2 Honor", "Opponent Loses 2 Honor": Armor of the Heavens) instead of the generic " · you" / " · your opponent"
    /// suffix. That option is already a complete choice: it is shown as Core words it and never reopens a player picker.</summary>
    private static bool NamesItsPlayer(ActionOption o) => o.Command?.TargetPlayerId is not null
        && !o.Label.EndsWith(" · you", StringComparison.Ordinal) && !o.Label.EndsWith(" · your opponent", StringComparison.Ordinal);

    private ActionOption[] PlayerSiblings(ActionOption o) => Actions.Where(a => a.Enabled && IsPlayerTarget(a) && a.Type == o.Type && a.CardId == o.CardId && a.Command!.AbilityId == o.Command?.AbilityId
        && a.Command.PerformerId == o.Command?.PerformerId && a.Command.CostOption == o.Command?.CostOption).ToArray();

    /// <summary>The drafted action is a "choose a player" with more than one legal player.</summary>
    private bool PlayerTargetActive => _chosen is { Provinces: null, Command.AbilityId: not null } chosen && chosen.Type is "equip" or "playStrategy" or "useAbility"
        && PlayerSiblings(chosen).Select(s => s.Command!.TargetPlayerId).Distinct().Count() > 1;

    /// <summary>A play whose Core options differ by which player is chosen, with more than one of them legal: the player has to pick.</summary>
    private static bool NeedsPlayerPick(PlayChoice choice) =>
        choice.Variants.Where(v => v.Enabled && IsPlayerTarget(v)).Select(v => v.Command!.TargetPlayerId).Distinct().Count() > 1;

    /// <summary>Begins a play from the table. When the player is a choice, the draft starts with none picked.</summary>
    private void StartPlay(PlayChoice choice)
    {
        var option = choice.Option;
        if (NeedsPlayerPick(choice) && option.Command is { } command) option = option with { Command = command with { TargetPlayerId = null } };
        SelectReactionSource(option); Choose(option, keepTarget: false);
    }

    private bool PlayerPicked => _chosen?.Command?.TargetPlayerId is not null;

    private bool MultiPlayerSlotActive => MultiTargetActive && MultiUpFront.Any(s => s.Slot == _multiActiveSlot && s.Kind == TargetSlotKinds.Player);

    /// <summary>The player (0 or 1) whose stronghold this card is, or null.</summary>
    private int? StrongholdOwner(CardView card) => View.Players.FirstOrDefault(p => p.Stronghold.Id == card.Id)?.Id;

    private bool PlayerTargetLegal(int player) =>
        PlayerTargetActive && PlayerSiblings(_chosen!).Any(s => s.Command!.TargetPlayerId == player)
        || MultiPlayerSlotActive && MultiUpFront.Any(s => s.Slot == _multiActiveSlot && s.Kind == TargetSlotKinds.Player && s.PlayerIds.Contains(player));

    private bool PlayerTargetChosen(int player) =>
        PlayerTargetActive && _chosen!.Command!.TargetPlayerId == player || MultiPlayerSlotActive && _multiActiveSlot is { } slot && _multiPlayers.GetValueOrDefault(slot, -1) == player;

    /// <summary>A click on a player's stronghold or clan plaque while a player is being chosen. Returns false when no player is being chosen.</summary>
    private bool ChoosePlayerTarget(int player)
    {
        if (MultiPlayerSlotActive && _multiActiveSlot is { } activeSlot)
        {
            if (!PlayerTargetLegal(player)) { SetNotice("Choose one of the highlighted players."); return true; }
            if (!_multiPlayers.Remove(activeSlot, out var old) || old != player) _multiPlayers[activeSlot] = player;
            MultiAdvance(activeSlot); RefreshMultiTargets(); return true;
        }
        if (!PlayerTargetActive) return false;
        if (PlayerSiblings(_chosen!).FirstOrDefault(s => s.Command!.TargetPlayerId == player) is not { } sibling) { SetNotice("Choose one of the highlighted players."); return true; }
        Choose(sibling, keepTarget: true);
        return true;
    }

    private void HighlightPlayers()
    {
        foreach (var (player, plaque) in _clanPlaques)
        {
            if (!IsInstanceValid(plaque)) continue;
            plaque.Modulate = PlayerTargetChosen(player) ? new Color(1.4f, 1.25f, .75f) : PlayerTargetLegal(player) ? new Color(1.2f, 1.15f, .85f) : Colors.White;
            plaque.Hit.TooltipText = PlayerTargetLegal(player) ? (PlayerTargetChosen(player) ? "Chosen player. " : "") + "Click to choose this player. Confirm separately." : plaque.HitTooltip;
        }
    }

    private void RenderPlayerPicker(ActionOption action)
    {
        _inspector.AddChild(Palette.Text("CHOOSE A PLAYER", 11, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped("Click a highlighted stronghold or clan plaque on the table, or choose below. Nothing happens until you confirm.", 13, Palette.PaperDim));
        foreach (var sibling in PlayerSiblings(action).OrderBy(s => s.Command!.TargetPlayerId == View.ViewerId ? 0 : 1))
        {
            var player = sibling.Command!.TargetPlayerId!.Value; var selected = action.Command!.TargetPlayerId == player; var pick = sibling;
            var row = SidebarSkin.Button((selected ? "✓ " : "") + (player == View.ViewerId ? "You" : "Your opponent") + " · " + View.Players.Single(p => p.Id == player).Clan, () => Choose(pick, keepTarget: true));
            row.SetMeta("player_row", player); row.Disabled = !DecisionCanSubmit; _inspector.AddChild(row);
        }
        if (!PlayerPicked) _inspector.AddChild(Palette.Wrapped("Choose a player to continue.", 13, Palette.Error));
    }
}
