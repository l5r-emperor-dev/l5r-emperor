using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // This is display context, never a second reaction state machine. Core supplies
    // timing and the consequence of declining; its existing action supplies the command.
    private bool ReactionFlowOwnsControls => !PresentationBusy && View.WinnerId is null &&
        View.ReactionPresentation is { DeclineLabel.Length: > 0 } && View.PendingDuel is null &&
        !EventDecisionActive && !GenericDecisionActive && !EitherOrActive && !OpponentPaymentActive &&
        !CopyDecisionActive && !LookDecisionActive && View.PendingProvinceOrder is null;

    private static Label ReactionContextText(string text, int size, Color color)
    {
        var label = Palette.Wrapped(text, size, color);
        // A wrapped header must reserve its height before the scrolling body is
        // allocated, including on the first frame of a new response window.
        label.CustomMinimumSize = new(0, MathF.Ceiling(Palette.Body.GetMultilineStringSize(text,
            width: SidebarWidth - 52, fontSize: size).Y));
        return label;
    }

    private void RenderReactionContext()
    {
        if (PresentationBusy || View.WinnerId is not null) return;
        if (View.ReactionPresentation is { } reaction && View.PendingDuel is null)
        {
            var own = View.PriorityPlayerId == View.ViewerId;
            if (reaction.DeclineLabel.Length > 0)
            {
                var heading = Palette.Text(own ? _chosen is null ? "Your response" : "Prepare reaction"
                    : View.Players.Single(p => p.Id == View.PriorityPlayerId).Clan + " responds", 25, title: true);
                heading.Name = "ReactionDecisionHeader"; heading.HorizontalAlignment = HorizontalAlignment.Center;
                _sidebarHeader.AddChild(heading);
            }
            var timing = ReactionContextText(reaction.Title, 16, Palette.Gold);
            timing.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(timing);
            var context = ReactionContextText(BattleActionText.WithoutIds(reaction.Context), 18, Palette.Paper);
            context.Name = "ReactionContext"; _sidebarHeader.AddChild(context);
            var status = ReactionContextText(reaction.Status, 15, Palette.PaperDim);
            status.Name = "ReactionStatus"; _sidebarHeader.AddChild(status);
            if (View.ReactionOrder is { } order) RenderReactionOrder(order);
            if (reaction.ParentContext is { Length: > 0 } parent)
                _sidebarHeader.AddChild(ReactionContextText(BattleActionText.WithoutIds(parent), 15, Palette.PaperDim));
            _sidebarHeader.AddChild(new HSeparator());
        }
        if (_reactionReceipt is { } receipt)
        {
            var label = ReactionContextText(receipt, 17, Palette.Gold);
            label.Name = "ReactionReceipt"; _sidebarHeader.AddChild(label);
        }
    }

    // Issue #803: the announce / order / resolve procedure of a window both players hold a Reaction for. Core owns every decision; this only says where the window is and lists the Reactions.
    private void RenderReactionOrder(ReactionOrderView order)
    {
        var turnClan = View.Players.Single(p => p.Id == order.TurnPlayerId).Clan;
        var mine = order.TurnPlayerId == View.ViewerId;
        var instruction = order.Stage switch
        {
            "Announce" => mine ? "Announce every Reaction you will play in this window, then choose Done." : $"Waiting for {turnClan} to announce their Reactions.",
            "Order" => mine ? "Choose which announced Reaction resolves next." : $"Waiting for {turnClan} to choose the order the Reactions resolve in.",
            _ => "The announced Reactions resolve in this order.",
        };
        var heading = ReactionContextText(instruction, 17, Palette.Gold); heading.Name = "ReactionOrderInstruction"; _sidebarHeader.AddChild(heading);
        string Describe(ReactionOrderItemView item) => $"{item.CardName} · {BattleActionText.WithoutIds(item.Label)} ({View.Players.Single(p => p.Id == item.PlayerId).Clan})";
        for (var i = 0; i < order.Ordered.Length; i++)
        {
            var line = ReactionContextText($"{i + 1}. {Describe(order.Ordered[i])}", 15, i == 0 && order.Stage == "Resolve" ? Palette.Paper : Palette.PaperDim);
            line.Name = "ReactionOrderQueued"; _sidebarHeader.AddChild(line);
        }
        foreach (var item in order.Announced)
        {
            var line = ReactionContextText("Announced: " + Describe(item), 15, Palette.PaperDim);
            line.Name = "ReactionOrderAnnounced"; _sidebarHeader.AddChild(line);
        }
    }

    private bool RenderReactionFlow()
    {
        if (!ReactionFlowOwnsControls) return false;
        // Existing multi-target, movement, alternative-cost and required-choice
        // pickers run before this method and retain their own review/commit controls.
        if (_chosen is not null && (_chosen.Type is not ("useAbility" or "playStrategy" or "equip") ||
            _chosen.Command?.CardIds is not null || _chosen.AlternativeCost is not null ||
            _chosen.CardId is { } supportId && FindCard(supportId) is { } support && IsSupportCard(support))) return false;
        _guide.Visible = false;
        if (_chosen is { } draft)
        {
            if (draft.CardId is { } id && FindCard(id) is { Definition: not null } source) AddDecisionSource(source, null);
            RenderReactionPerformers();
            RenderTargeted();
            return true;
        }
        if (BattleTracker.InBattle(View.Phase))
        {
            RenderBattlefieldNavigation();
            RenderBattleActionPanel(); // issue #1169: the Engage window and every other battle Reaction window show the record too
        }
        if (View.PriorityPlayerId != View.ViewerId)
        {
            _inspector.AddChild(Palette.Wrapped("Waiting for " + View.Players.Single(p => p.Id == View.PriorityPlayerId).Clan + " to respond.", 19, Palette.Crane));
            if (!_session.Online && !_autoOpponent)
                _decisionActions.AddChild(SidebarSkin.Button("Pass device to other player", SwitchSeat));
            return true;
        }
        var options = Actions.Where(a => a.Type is not ("declineReaction" or "concede" or "wait" or "orderSeppuku") && (a.Enabled || a.Type is not ("useAbility" or "playStrategy" or "equip"))).ToArray();
        if (options.Length > 0)
        {
            _inspector.AddChild(Palette.Text("Choose a reaction", 18, Palette.Gold));
            foreach (var group in options.GroupBy(a => a.CardId))
            {
                if (group.Key is { } id && FindCard(id) is { Definition: not null } source)
                {
                    var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
                    var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); panel.AddChild(row);
                    row.AddChild(CardPreview.Face(source, 112, 80));
                    var actions = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    actions.AddThemeConstantOverride("separation", 7); row.AddChild(actions);
                    actions.AddChild(Palette.Wrapped(source.Definition.Name, 21, Palette.Gold));
                    foreach (var option in group)
                    {
                        actions.AddChild(Palette.Wrapped(Placard(option), 15, Palette.PaperDim));
                        AddAction(actions, option);
                        if (!option.Enabled && option.Reason is { } reason)
                            actions.AddChild(Palette.Wrapped(reason, 14, Palette.PaperDim));
                    }
                    _inspector.AddChild(panel);
                }
                else foreach (var option in group) AddAction(_inspector, option);
            }
        }
        RenderReactionDecline();
        return true;
    }

    private bool HasContextualReactionDecline => View.ReactionPresentation is { DeclineLabel.Length: > 0 } &&
        Actions.Any(a => a.Type == "declineReaction" && a.Enabled && a.Command is not null);

    private void RenderReactionDecline()
    {
        if (!HasContextualReactionDecline || _chosen is not null && !GenericDecisionActive) return;
        if (Actions.FirstOrDefault(a => a.Type == "declineReaction" && a.Enabled && a.Command is not null) is { } decline)
        {
            var presentation = View.ReactionPresentation!;
            var consequence = Palette.Wrapped(presentation.DeclineConsequence, 16, Palette.PaperDim);
            consequence.Name = "ReactionDeclineConsequence"; _decisionActions.AddChild(consequence);
            var label = presentation.DeclineLabel;
            // The public contract keeps an unplayed hand source unnamed. Only its
            // deciding owner can name this single ordered item from their own view.
            if (View.PendingChoice is { Type: "beforeBattleReaction", CardId: { } id } pending &&
                pending.PlayerId == View.ViewerId && FindCard(id)?.Definition?.Name is { } name)
                label = "Decline " + name;
            var finish = SidebarSkin.Button(label, () => _ = Submit(decline.Command!), true);
            finish.Name = "ReactionDecline"; finish.Disabled = !decline.Enabled || !DecisionCanSubmit;
            finish.TooltipText = presentation.DeclineConsequence;
            _decisionActions.AddChild(finish);
        }
    }

    private void IdentifyReactionOption(Button button, ActionOption option)
    {
        if (View.ReactionPresentation is null || option.Type is "declineReaction" or "concede" or "wait") return;
        button.Name = "ReactionOption";
        button.SetMeta("reaction_card", option.CardId ?? "");
        button.SetMeta("reaction_ability", option.Command?.AbilityId ?? "");
        button.SetMeta("reaction_performer", option.Command?.PerformerId ?? "");
    }

    private string ReactionOptionLabel(ActionOption option)
    {
        var label = CommitActionLabel(option);
        if (option.Command?.PerformerId is { } performer && performer != option.CardId && FindCard(performer)?.Definition?.Name is { } performerName)
            label += " · " + performerName;
        if (option.TargetIds is not { Length: > 0 } && option.Command?.TargetId is { } target && FindCard(target)?.Definition?.Name is { } targetName)
            label += " · " + targetName;
        if (option.Command?.TargetPlayerId is { } player)
            label += player == View.ViewerId ? " · you" : " · your opponent";
        return label;
    }

    // Issue #962: during a reaction window the card a reaction belongs to is clicked on the table. A click prepares the reaction (the "Play Reaction" press commits it); in the announce and order steps
    // of a window both players hold a Reaction for, a click announces, retracts or orders the card's one Reaction. Passing, declining and "Done announcing" stay as buttons.
    private static bool IsReactionOrderingOption(ActionOption a) => a.Type is "reactionAnnounce" or "reactionUnannounce" or "reactionOrder";

    private bool ReactionSourcesClickable => ReactionFlowOwnsControls && _chosen is null && View.PriorityPlayerId == View.ViewerId && DecisionCanSubmit;

    private ActionOption[] ReactionOrderingOptions(string cardId) => Actions.Where(a => a.Enabled && a.CardId == cardId && a.Command is not null && IsReactionOrderingOption(a)).ToArray();

    private HashSet<string> ReactionSourceCardIds() => !ReactionSourcesClickable ? [] : Actions.Where(a => a.Enabled && a.CardId is not null && a.Command is not null
        && (IsReactionOrderingOption(a) || a.Type is "useAbility" or "playStrategy" or "equip")).Select(a => a.CardId!).ToHashSet(StringComparer.Ordinal);

    private bool SelectReactionSourceCard(CardView card)
    {
        if (!ReactionSourcesClickable) return false;
        var ordering = ReactionOrderingOptions(card.Id);
        if (ordering.Length > 0)
        {
            if (ordering.Length > 1) { SetNotice("This card has more than one choice in this window. Use the list on the right."); return true; }
            _ = Submit(ordering[0].Command!); return true;
        }
        var choices = PlayChoices(card);
        if (choices.Length == 0) return false;
        if (choices.Length > 1) { SetNotice("This card has more than one reaction available. Choose one in the list on the right."); _selected = card; RenderInspector(); Highlight(); return true; }
        StartPlay(choices[0]);
        return true;
    }

    private void SelectReactionSource(ActionOption option)
    {
        if (View.ReactionPresentation is not null && option.CardId is { } id &&
            option.Type is "useAbility" or "playStrategy" or "equip") _selected = FindCard(id);
    }

    private ActionOption[] ReactionPerformerOptions() => ReactionFlowOwnsControls && !_reviewingAlternativeCost &&
        _chosen is { CardId: { } source, Command: { } command } ? Actions.Where(a => a.Enabled &&
            a.CardId == source && a.Type == _chosen.Type && a.Command is { PerformerId: not null } candidate &&
            candidate.AbilityId == command.AbilityId).ToArray() : [];

    private Dictionary<string, Action> ReactionPerformerClicks()
    {
        var choices = ReactionPerformerOptions();
        var performers = choices.Select(a => a.Command!.PerformerId!).Distinct().ToArray();
        return performers.Length < 2 ? [] : performers.ToDictionary(id => id, id => (Action)(() => ChooseReactionPerformer(id)));
    }

    private void RenderReactionPerformers()
    {
        var performers = ReactionPerformerClicks();
        if (performers.Count < 2) return;
        _inspector.AddChild(Palette.Text("Choose a performer", 17, Palette.Gold));
        var picker = NewChoiceList(DecisionCanSubmit);
        picker.Name = "ReactionPerformerPicker";
        var ids = performers.Keys.ToArray();
        foreach (var id in ids) picker.AddItem(FindCard(id)?.Definition?.Name ?? "Performer", id);
        picker.Selected = Array.IndexOf(ids, _chosen?.Command?.PerformerId);
        picker.ItemSelected += i => performers[ids[i]]();
        _inspector.AddChild(picker);
    }

    private void ChooseReactionPerformer(string id)
    {
        if (!DecisionCanSubmit || _chosen?.Command is not { } current) return;
        var choices = ReactionPerformerOptions().Where(a => a.Command!.PerformerId == id).ToArray();
        if (choices.Length == 0) return;
        var selected = choices.FirstOrDefault(a => current.TargetId is { } target &&
            (a.TargetIds?.Contains(target) == true || a.Command!.TargetId == target)) ?? choices[0];
        var keepTarget = current.TargetId is { } chosenTarget && selected.TargetIds?.Contains(chosenTarget) == true;
        if (keepTarget) selected = selected with { Command = selected.Command! with { TargetId = current.TargetId } };
        Choose(selected, keepTarget);
    }

    // Source names come from this player's permitted view. This changes the verb on
    // a review button, not its command or when the engine applies its effects.
    private string CommitActionLabel(ActionOption action)
    {
        var source = action.CardId is { } id ? FindCard(id) : null;
        var name = source?.Definition?.Name;
        return action.Type switch
        {
            "purchase" or "recruitReserve" => name is null ? "Recruit card" : "Recruit " + name,
            "equip" => name is null ? "Attach card" : "Attach " + name,
            "playStrategy" => name is null ? "Play card" : "Play " + name,
            "useAbility" when source?.Definition?.Type == CardType.Strategy => name is null ? "Play reaction" : "Play " + name,
            "useAbility" => name is null ? "Use ability" : "Use " + name,
            _ => "Commit action"
        };
    }

    // The stateful action button: while a reaction window is open, committing a prepared reaction reads "Play Reaction"; the window's own decline reads
    // "Skip Reaction" before anything is played and "Resolve Reaction" once the round is closing (both come from Core's presentation metadata).
    private string CommitButtonLabel(ActionOption action) =>
        View.ReactionPresentation is { DeclineLabel.Length: > 0 } && View.PendingChoice is not null && action.CardId is not null && action.Command is not null
            && action.Type is "useAbility" or "playStrategy" ? "Play Reaction" : CommitActionLabel(action);

    private sealed record ReactionSubmission(long Version, int PlayerId, string? CardId, string Name, long EventSequence);
    private ReactionSubmission? _reactionSubmission;
    private string? _reactionReceipt;
    private int _reactionFlowViewer = -1, _reactionReceiptTurn;
    private long _reactionFlowVersion = -1, _reactionReceiptSequence = -1, _reactionFlowEventSequence = -1;

    private void ResetReactionFlow()
    {
        _reactionSubmission = null; _reactionReceipt = null;
        _reactionFlowViewer = -1; _reactionFlowVersion = -1; _reactionReceiptSequence = -1; _reactionFlowEventSequence = -1;
    }

    private void TrackReactionSubmission(GameCommand command)
    {
        if (command.Type == "declineReaction") { _reactionSubmission = null; return; }
        if (View.ReactionPresentation is null || command.Type is not ("useAbility" or "playStrategy"))
        {
            if (View.PendingChoice is null) { _reactionSubmission = null; _reactionReceipt = null; }
            return;
        }
        // Only an actually offered reaction may create a receipt. Passing can resume
        // the original action and emit abilityUsed; that is never attributed to us.
        if (!Actions.Any(a => a.Enabled && a.Command is { } offered && offered.Type == command.Type &&
            offered.CardId == command.CardId && offered.AbilityId == command.AbilityId && offered.PlayerId == command.PlayerId)) return;
        var name = command.CardId is { } id && FindCard(id) is { } source ? ShortName(source) : "Reaction";
        _reactionSubmission = new(command.ExpectedVersion, command.PlayerId, command.CardId, name, View.Events.LastOrDefault()?.Sequence ?? -1);
        _reactionReceipt = null;
    }

    private void ObserveReactionFlow(MatchView snapshot)
    {
        var sequence = snapshot.Events.LastOrDefault()?.Sequence ?? -1;
        if (_reactionFlowViewer != -1 && (_reactionFlowViewer != snapshot.ViewerId || snapshot.Version < _reactionFlowVersion || sequence < _reactionFlowEventSequence)) ResetReactionFlow();
        _reactionFlowViewer = snapshot.ViewerId; _reactionFlowVersion = snapshot.Version;
        _reactionFlowEventSequence = sequence;
        if (_reactionReceipt is not null && (snapshot.TurnNumber != _reactionReceiptTurn ||
            snapshot.Events.Any(e => e.Sequence > _reactionReceiptSequence && e.Type is "abilityUsed" or "strategyPlayed"))) _reactionReceipt = null;
        if (_reactionSubmission is not { } submitted || snapshot.Version <= submitted.Version) return;
        var accepted = snapshot.Events.Any(e => e.Sequence > submitted.EventSequence &&
            e.PlayerId == submitted.PlayerId && e.CardId == submitted.CardId && e.Type is "abilityUsed" or "strategyPlayed");
        if (accepted)
        {
            // Played is deliberately weaker than resolved: an effect may still have
            // an opponent response, an at-resolution target or another required choice.
            _reactionReceipt = "Reaction played · " + submitted.Name;
            _reactionReceiptTurn = snapshot.TurnNumber;
            _reactionReceiptSequence = snapshot.Events.LastOrDefault()?.Sequence ?? submitted.EventSequence;
            _reactionSubmission = null;
        }
        else if (snapshot.PendingChoice is null && snapshot.ReactionPresentation is null) _reactionSubmission = null;
    }
}
