using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>One distinct action a selected card can legally take now. Variants differ only by performer, target or alternative cost.</summary>
internal sealed record PlayChoice(string Key, ActionOption Option, ActionOption[] Variants);

public partial class Main
{
    // The card and action the player is being asked to confirm. Held as a key, not an option, so a refreshed snapshot re-resolves it.
    private (string CardId, string Key)? _playPrompt;

    /// <summary>The ability a play belongs to, whatever it is played as. Used to tell a variant of an available action from an action that is not available at all.</summary>
    private static string PlayBaseKey(ActionOption option) => option.Type + ":" + (option.Command?.AbilityId ?? option.AlternativeCost?.AbilityId ?? "");

    /// <summary>
    /// One key per distinct play. Core lists a separate option for each thing a card can be played as: where it goes (a Fate deck's top or bottom), which card it creates or shows, and what it compares. Those are
    /// different plays and must never collapse into the first. The performer, the cost option, a province's owner and, for "choose a player", the player stay variants of one play: they are chosen afterwards,
    /// on the table or in the draft (NeedsPlayerPick makes the draft ask, so a player target never resolves for "you" by default).
    /// </summary>
    private static string PlayKey(ActionOption option)
    {
        var key = PlayBaseKey(option);
        if (option.Command is not { } command) return key;
        if (command.Choice is { } choice && option.ChoiceLabel is null) key += ":choice" + choice; // a choice made after the target (ChoiceLabel) is a step of one play, not another play
        if (command.CreatedCard is { } created) key += ":creates" + created;
        if (command.DeckPosition is { } position) key += ":deck" + position;
        if (command.CardIds is { Length: > 0 } shown && !(command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && ability.Target == "provinces")) key += ":shows" + string.Join("+", shown);
        return key;
    }

    private bool IsOwnHandCard(string? id) => id is not null && FindCard(id) is { Zone: Zone.Hand } card && card.OwnerId == View.ViewerId;

    /// <summary>The distinct legal actions of a card. The engine already disables an action that is out of phase or lacks its performer, so an enabled option is a legal one.</summary>
    private PlayChoice[] PlayChoices(CardView card) => Actions
        .Where(a => a.Enabled && a.CardId == card.Id && a.Command is not null && a.Type is "equip" or "playStrategy" or "useAbility")
        .GroupBy(PlayKey).Select(g => new PlayChoice(g.Key, g.First(), g.ToArray())).ToArray();

    private static bool IsLimited(PlayChoice choice) =>
        choice.Option.Command?.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing.Contains("Limited", StringComparison.Ordinal);

    /// <summary>The action to assume: the only legal one, or, while Plans Within Plans is armed, the Limited action it permits. Otherwise the player picks.</summary>
    private PlayChoice? AssumedPlay(PlayChoice[] choices)
    {
        if (choices.Length == 1) return choices[0];
        if (View.Players.Single(p => p.Id == View.ViewerId).PlansWithinPlansArmed && choices.Count(IsLimited) == 1) return choices.Single(IsLimited);
        return null;
    }

    /// <summary>Selecting a playable hand card starts the play flow without a trip through the sidebar's action list.</summary>
    private void BeginDirectPlay(CardView card)
    {
        _playPrompt = null;
        if (!IsOwnPlayableCard(card.Id) || card.ActionLocked || View.PendingChoice is not null || View.WinnerId is not null || View.PriorityPlayerId != View.ViewerId || _chosen is not null) return;
        if (AssumedPlay(PlayChoices(card)) is not { } choice) return;
        // An action that still needs a performer, target or payment goes straight to choosing them on the table; only an action with nothing left to choose asks to be confirmed (that press is its final Confirm).
        if (NeedsFurtherInput(choice)) StartPlay(choice); else _playPrompt = (card.Id, choice.Key);
    }

    /// <summary>
    /// Clicking one of your own cards in play starts its ability when that is unambiguous: exactly one ability can be used now. If several of your Personalities could perform it, they are highlighted on the table
    /// and the player clicks one. A card with several usable abilities keeps today's behaviour (the card is selected and its abilities are listed). Hovering the card still inspects it.
    /// </summary>
    private bool StartInPlayAbility(CardView card)
    {
        if (card.OwnerId != View.ViewerId || card.Zone is Zone.Hand or Zone.Province or Zone.Resolving || card.ActionLocked || _chosen is not null) return false;
        if (View.PendingChoice is not null || View.WinnerId is not null || View.PriorityPlayerId != View.ViewerId || _session.Busy || PresentationBusy) return false;
        var choices = PlayChoices(card).Where(c => c.Option.Type == "useAbility").ToArray();
        if (choices.Length != 1) return false; // several performers are fine: the Personalities are highlighted on the table and clicked (PerformerClicks)
        StartPlay(choices[0]);
        return _chosen is not null;
    }

    private void BeginPlayPrompt(ActionOption option)
    {
        if (option.CardId is null || _session.Busy || PresentationBusy) return;
        if (FindCard(option.CardId) is { } card && PlayChoices(card).FirstOrDefault(c => c.Key == PlayKey(option)) is { } choice && NeedsFurtherInput(choice))
        { _playPrompt = null; StartPlay(choice); return; }
        _playPrompt = (option.CardId, PlayKey(option));
        RenderInspector(); RenderPhaseActions(); Highlight();
    }

    private PlayChoice? PromptedChoice() =>
        _playPrompt is { } prompt && FindCard(prompt.CardId) is { } card && IsOwnPlayableCard(card.Id) ? PlayChoices(card).FirstOrDefault(c => c.Key == prompt.Key) : null;

    /// <summary>True when playing the action needs a performer, target, province, movement, cost payment or alternative cost. Otherwise confirming plays it outright.</summary>
    private bool NeedsFurtherInput(PlayChoice choice)
    {
        var option = choice.Option;
        return option.RequiresInput() || NeedsPlayerPick(choice) || NeedsTargetPick(option) || IsProvinceTarget(option) || HasAttackTargets(option)
            || choice.Variants.Select(v => v.Command?.PerformerId).Distinct().Count() > 1; // Core's own fields first (ActionOption.RequiresInput); only what the client alone knows after
    }

    private bool RenderPlayPrompt()
    {
        if (PromptedChoice() is not { } choice || FindCard(_playPrompt!.Value.CardId) is not { } card) { _playPrompt = null; return false; }
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); panel.AddChild(box); _inspector.AddChild(panel);
        box.AddChild(Palette.Text("PLAY THIS CARD?", 12, Palette.Gold));
        box.AddChild(Palette.Wrapped(card.Definition?.Name ?? "Card", 22));
        box.AddChild(Palette.Wrapped(Placard(choice.Option), 17, Palette.Gold));
        box.AddChild(Palette.Wrapped(NeedsFurtherInput(choice)
            ? "Confirm to begin. You then choose any performer, target and payment."
            : "Nothing else to choose. Confirm to play it.", 14, Palette.PaperDim));
        AddPlayAlternatives(box, card, choice.Key);
        var confirm = SidebarSkin.Button("Confirm play", ConfirmPlay, true); confirm.Disabled = _session.Busy;
        _decisionActions.AddChild(confirm);
        _decisionActions.AddChild(SidebarSkin.Button("Cancel", CancelChoice));
        return true;
    }

    /// <summary>The card's other legal actions as "Play instead" rows, and why any other action cannot be taken now (issue #911). Shown in the confirm prompt and in the draft once a play has begun.</summary>
    private void AddPlayAlternatives(Node box, CardView card, string currentKey)
    {
        var choices = PlayChoices(card);
        foreach (var other in choices.Where(c => c.Key != currentKey))
        {
            var option = other.Option;
            var switchTo = SidebarSkin.Button("Play instead: " + Placard(option), () => BeginPlayPrompt(option));
            switchTo.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(switchTo);
        }
        // Issue #911: when a card's other action cannot be taken now (a second A Paragon's Strength once the enemy it could send home is gone), say so here instead of silently offering only the one left. A disabled sibling
        // of an available play (the "you" variant of a card that can only target another player) is not another action.
        var available = choices.Select(c => WithoutPlayerSuffix(c.Option.Label)).ToHashSet(StringComparer.Ordinal);
        foreach (var unavailable in Actions.Where(o => !o.Enabled && o.CardId == card.Id && o.Type is "equip" or "playStrategy" or "useAbility" && o.Reason is not null && !available.Contains(WithoutPlayerSuffix(o.Label)))
                     .GroupBy(o => WithoutPlayerSuffix(o.Label)).Select(g => g.First()))
            box.AddChild(Palette.Wrapped($"Not available now: {Placard(unavailable)} ({unavailable.Reason})", 13, Palette.PaperDim));
    }

    // Core appends " · you" / " · your opponent" to each player's option of a "choose a player" action. A disabled one of those beside an enabled sibling is the same action, not another one.
    private static string WithoutPlayerSuffix(string label) =>
        label.EndsWith(" · your opponent", StringComparison.Ordinal) ? label[..^" · your opponent".Length] : label.EndsWith(" · you", StringComparison.Ordinal) ? label[..^" · you".Length] : label;

    private void ConfirmPlay()
    {
        if (PromptedChoice() is not { } choice) { _playPrompt = null; RenderInspector(); return; }
        _playPrompt = null;
        if (!NeedsFurtherInput(choice) && choice.Option.Command is { } command) { _ = Submit(command); return; }
        StartPlay(choice);
    }

    /// <summary>A card with several legal actions lists each as a highlightable, clickable block under its printed text; a click begins that action.</summary>
    private void AddActionTextBlocks(VBoxContainer details, CardView card)
    {
        if (_chosen is not null || _playPrompt is not null || View.PendingChoice is not null || !IsOwnHandCard(card.Id)) return;
        var choices = PlayChoices(card);
        if (choices.Length < 2) return;
        details.AddChild(Palette.Text("CHOOSE AN ACTION", 11, Palette.Gold));
        foreach (var choice in choices)
        {
            var option = choice.Option;
            var block = new Button
            {
                Text = Placard(option),
                Flat = false,
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0, 40),
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                TooltipText = "Click to play this action. You confirm before anything happens."
            };
            block.SetMeta("action_text_block", true);
            block.AddThemeFontSizeOverride("font_size", 16);
            foreach (var state in new[] { "normal", "pressed", "hover", "focus" })
                block.AddThemeStyleboxOverride(state, new StyleBoxFlat
                {
                    BgColor = state == "hover" ? new Color(Palette.Gold, .16f) : new Color(1, 1, 1, .03f),
                    BorderColor = state == "hover" ? Palette.Gold : new Color(Palette.Gold, .35f),
                    BorderWidthLeft = 2,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                    BorderWidthBottom = 1,
                    ContentMarginLeft = 10,
                    ContentMarginRight = 8,
                    ContentMarginTop = 6,
                    ContentMarginBottom = 6
                });
            block.Pressed += () => BeginPlayPrompt(option);
            details.AddChild(block);
        }
    }
}
