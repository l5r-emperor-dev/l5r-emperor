using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private ActionOption? _decisionSelection;
    private long _decisionVersion = -1;
    private int _decisionViewer = -1;
    private bool _reviewingAlternativeCost;
    private bool _alternativeCostConfirmed;
    private bool GenericDecisionActive => View.WinnerId is null && !EventDecisionActive &&
        (View.PendingChoice?.Type is "effectChoice" or "preservingHonor" or "rinShame" or "conqueror" or "declineStraighten" or "forcedDiscard" or "beforeBattleOrder" or "beforeBattleOptional" or "encircledOrder" or "encircledSelect" or "eventChoice" or "honorLossDecision" or "entryCostDecision" or "honorChangeReaction" or "killingGroundsSelect" or "infiltratorStraighten" or "budgetSelect"
            || UnknownPendingDecision);

    // P25 (issue #723): a pending type the client has no screen of its own for (a feature registered it through EngineHooks.PendingActions) is presented as a generic decision: the engine's own options, one
    // selected, then confirmed. The set below is every type that existed before P25; each keeps the screen it already had, so a new decision kind needs no edit here.
    private static readonly HashSet<string> KnownPendingTypes = new(StringComparer.Ordinal)
    {
        "additionalActionReaction", "afterActionResolvedReaction", "afterResolutionReaction", "announceReaction", "attackPhaseEndReaction", "attackReaction", "attackTargetSelect", "battleActionReaction", "battleRecruit",
        "beforeActionReaction", "beforeBattleOptional", "beforeBattleOrder", "beforeBattleReaction", "bowReaction", "brashDraw", "budgetSelect", "conqueror", "copyAbility", "copyKeyword", "deciderChoice", "declineStraighten",
        "discardHand", "duelReaction", "duelWonReaction", "effectChoice", "eitherOr", "encircledOrder", "encircledSelect", "engageReaction", "entryCostDecision", "entryReaction", "eventChoice", "focusEffectChoice",
        "forcedDiscard", "harshChoices", "honorChangeReaction", "honorLossDecision", "infiltratorStraighten", "killingGroundsSelect", "lookArrange", "lookDeck", "moveReaction", "nikutaiReaction", "nitoshiHold",
        "offeringReverence", "opponentPayment", "overconfidentDraw", "p3AroruMove", "p3BeforeEventReaction", "p3DarkVirtueDiscard", "p3EntryReaction", "p3MikiDarkVirtue", "p5BlessingsTop", "preservingHonor", "proclaim",
        "proclaimNegateReaction", "provinceCardChoice", "provinceOrder", "rentatsuLoss", "rikateDetach", "rinShame", "searchDeck", "searchDestination", "searchDiscard", "searchRecruitPay", "searchRegionProvince", "searchSelect",
        "sequencedTarget", "shinden", "starCrossedLovers", "testedLandProvince", "turnBeginsChoice", "twistOfFateReaction", "unimpeachableNameReaction", "yourClanNeedsYouProvince"
    };
    private bool UnknownPendingDecision => View.PendingChoice is { } pending && !KnownPendingTypes.Contains(pending.Type) &&
        (pending.PlayerId != View.ViewerId || Actions.Any(a => a.Type is not ("concede" or "wait" or "orderSeppuku")));
    private bool DecisionCanSubmit => !_session.Busy && !PresentationBusy && !_playHeld && (!_session.Online ||
        _session.Connected && !(_session.Snapshot?.PlayersConnected?.Any(p => !p) ?? false));

    private ActionOption[] AlternativeCosts(ActionOption action) => action.AlternativeCost is not { } cost ? [] :
        Actions.Where(a => a.CardId == action.CardId && a.AlternativeCost is { } other &&
            other.AbilityId == cost.AbilityId && other.PerformerId == cost.PerformerId).ToArray();

    // The hand cards the cost review may discard (Akodo Kamina: "bow Kamina, or discard a card"): one engine-listed option per card, each naming it on CardIds.
    private HashSet<string> AlternativeCostCards() => !_reviewingAlternativeCost || _chosen?.AlternativeCost is null ? [] :
        AlternativeCosts(_chosen).Where(a => a.Enabled && a.Command?.CardIds is { Length: 1 }).Select(a => a.Command!.CardIds![0]).ToHashSet();

    // Clicking such a hand card selects its discard option, the same as picking it in the sidebar list; the choice is still confirmed separately.
    private bool SelectAlternativeCostCard(CardView card)
    {
        if (!_reviewingAlternativeCost || _chosen?.AlternativeCost is null) return false;
        var option = AlternativeCosts(_chosen).FirstOrDefault(a => a.Enabled && a.Command?.CardIds is [var id] && id == card.Id);
        if (option is null) return false;
        _decisionSelection = option; RenderInspector(); Highlight(); return true;
    }

    private void ResetDecisionSelection()
    {
        _decisionSelection = null;
        _reviewingAlternativeCost = false; _alternativeCostConfirmed = false;
        _decisionVersion = View.Version; _decisionViewer = View.ViewerId;
    }

    private bool RenderDecisionChooser()
    {
        if (_decisionVersion != View.Version || _decisionViewer != View.ViewerId) ResetDecisionSelection();
        if (PresentationBusy || !GenericDecisionActive && _chosen?.AlternativeCost is null) return false;
        var pending = GenericDecisionActive ? View.PendingChoice : null;
        var own = pending is null || pending.PlayerId == View.ViewerId && View.PriorityPlayerId == View.ViewerId;
        var contextualDecline = pending is not null && HasContextualReactionDecline;
        var options = pending is null ? OneRowPerCost(AlternativeCosts(_chosen!)) : Actions.Where(a => a.Type is not ("concede" or "wait") && a.Type != "orderSeppuku" &&
            !(contextualDecline && a.Type == "declineReaction")).ToArray();
        // Issue #1202: a draft with a way to pay asks one thing at a time -- the performer, the target, then how to pay. Once the first two are answered the cost is the next question.
        if (pending is null && _chosen is { Command: { } asked } && !_reviewingAlternativeCost && !_alternativeCostConfirmed && !PerformerStepActive
            && !(asked.TargetId is null && options.Any(a => a.TargetIds is { Length: > 0 }))) _reviewingAlternativeCost = true;
        _guide.Visible = false;
        var heading = Palette.Text(pending is null ? (_reviewingAlternativeCost || _alternativeCostConfirmed ? "HOW DO YOU PAY?" : PerformerStepActive ? "CHOOSE A PERFORMER" : "CHOOSE A TARGET") : own ? "YOUR CHOICE" : "OPPONENT’S CHOICE", 27, title: true);
        if (!contextualDecline)
        {
            heading.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(heading);
            _sidebarHeader.AddChild(new HSeparator());
        }
        if (pending?.Type is "beforeBattleOrder" or "beforeBattleOptional" or "encircledOrder" or "encircledSelect") RenderBattleFacts(true);
        if (pending is not null && BattleTracker.InBattle(View.Phase) && View.BattleActions is { Length: > 0 }) RenderBattleActionPanel(); // issue #1169
        var sourceId = pending?.CardId ?? _chosen?.CardId;
        var source = sourceId is null ? null : FindCard(sourceId);
        var prompt = pending?.Prompt is { } rawPrompt ? BattleActionText.WithoutIds(rawPrompt) : null;
        if (prompt?.IndexOf(" — ", StringComparison.Ordinal) is int split && split >= 0) prompt = prompt[..split] + ".";
        if (source is { Definition: not null }) AddDecisionSource(source, prompt);
        else if (prompt is not null) _inspector.AddChild(Palette.Wrapped(prompt, 19, Palette.Paper));
        if (pending is null && _chosen?.Command is { } draft)
        {
            if (_reviewingAlternativeCost || !PerformerStepActive) _inspector.AddChild(Palette.Wrapped(_reviewingAlternativeCost ? "Choose one cost to pay." : "Choose a target.", 22, Palette.Paper));
            if (_reviewingAlternativeCost && AlternativeCostCards().Count > 0)
            {
                _inspector.AddChild(Palette.Wrapped("Pay by the first option, or discard a card instead: select a card below, or click a highlighted card in your hand.", 17, Palette.PaperDim));
                Highlight(); // the hand cards that may be discarded light up as soon as the review opens
            }
            if (!PerformerStepActive && draft.PerformerId is { } performer && FindCard(performer) is { } card)
                _inspector.AddChild(Palette.Wrapped("Performer: " + card.Definition?.Name, 17, Palette.PaperDim));
            if (draft.TargetId is { } target && FindCard(target) is { } targetCard)
                _inspector.AddChild(Palette.Wrapped("Target: " + targetCard.Definition?.Name, 17, Palette.PaperDim));
        }
        if (pending is null && !_reviewingAlternativeCost && _chosen is { Command: { } draftCommand } draftAction)
        {
            if (_alternativeCostConfirmed)
            {
                var (required, ready) = RenderGoldPanel(draftAction);
                AddConfirm(CommitButtonLabel(draftAction), required, ready, () => draftCommand with { SourceIds = required > 0 ? _sources.ToArray() : [] }, "Cancel action"); return true;
            }
            if (PerformerStepActive) { RenderPerformerStep(); return true; } // the first question, on its own: nothing is pre-selected
            var targets = options.SelectMany(a => a.TargetIds ?? []).Distinct().ToArray();
            if (targets.Length > 0)
            {
                _inspector.AddChild(Palette.Wrapped("Choose a highlighted card on the table, or select it below.", 17, Palette.PaperDim));
                var picker = NewChoiceList(); // issue #635
                foreach (var id in targets) picker.AddItem(FindCard(id)?.Definition?.Name ?? "Target", id);
                picker.Selected = Array.IndexOf(targets, draftCommand.TargetId); picker.ItemSelected += i => ChooseTarget(targets[(int)i]); _inspector.AddChild(picker);
            }
            if (targets.Length > 0 && draftCommand.TargetId is null) _inspector.AddChild(Palette.Wrapped("Choose a target to continue.", 13, Palette.Error));
            AddFlowBack(draftAction); // back to the performer question
            _decisionActions.AddChild(SidebarSkin.Button("Cancel action", CancelChoice)); return true;
        }
        if (!own)
        {
            _inspector.AddChild(Palette.Wrapped("Waiting for " + View.Players.Single(p => p.Id == pending!.PlayerId).Name + " to choose.", 20, Palette.Crane));
            if (!_session.Online && !_autoOpponent) _decisionActions.AddChild(SidebarSkin.Button("Pass device to other player", SwitchSeat));
            return true;
        }
        if (IsMultiSelectPending(pending?.Type)) return RenderBudgetSelect(options); // Sleep (issue #667) and Benten's Blessing (issue #829): the multi-select panel, Main.BudgetSelect.cs
        if (pending?.Type == "shownChoice") return RenderShownChoice(options); // P25 (Main.ShownChoice.cs): the shown cards' faces
        if (pending is not null && RenderCandidateChoices(options)) return true;
        if (contextualDecline && options.Length == 0) return true;
        var rows = new List<(ActionOption Option, Button Button)>();
        var confirm = SidebarSkin.Button(contextualDecline ? _decisionSelection is { } selectedReaction ? CommitButtonLabel(selectedReaction) : "Choose a reaction" : "Confirm choice", CommitDecision, true);
        if (contextualDecline) confirm.Name = "CommitAction";
        foreach (var option in options)
        {
            // A cost option is usable only for the draft's engine-listed target. Never fabricate a disabled command.
            var matchesTarget = pending is not null || (HasMultiTargets(_chosen) ? MultiSelectionsAllowed(option) : _chosen?.Command?.TargetId is not { } target || option.TargetIds?.Contains(target) == true);
            var enabled = option.Enabled && option.Command is not null && matchesTarget;
            Button? button = null;
            button = SidebarSkin.Button(Placard(option, option.AlternativeCost?.Label ?? option.Label), () =>
            {
                _decisionSelection = option;
                if (contextualDecline) confirm.Text = CommitButtonLabel(option);
                if (pending is not null) { _chosen = null; _sources.Clear(); }
                if (QuoteFor(option) is not null) { _chosen = option; ResuggestSources(); RenderInspector(); return; }
                foreach (var row in rows)
                {
                    var selected = ReferenceEquals(row.Option, option);
                    row.Button.Text = (selected ? "✓  " : "") + (Placard(row.Option, row.Option.AlternativeCost?.Label ?? row.Option.Label));
                    row.Button.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: selected ? new Color(1.3f, 1.2f, .9f) : Colors.White));
                }
                confirm.Disabled = !DecisionCanSubmit; Highlight();
            });
            button.Disabled = !enabled || !DecisionCanSubmit;
            button.CustomMinimumSize = new(0, 74);
            rows.Add((option, button)); _inspector.AddChild(button);
            if (!enabled) _inspector.AddChild(Palette.Wrapped(option.Reason ?? "Unavailable for the selected target.", 15, Palette.PaperDim));
            if (ReferenceEquals(_decisionSelection, option)) button.Text = "✓  " + button.Text;
        }
        if (_decisionSelection is not null && !options.Contains(_decisionSelection)) _decisionSelection = null;
        confirm.Disabled = _decisionSelection is null || !DecisionCanSubmit;
        if (_decisionSelection is { } selectedPayment && QuoteFor(selectedPayment) is not null)
        {
            var (_, ready) = RenderGoldPanel(selectedPayment);
            confirm.Disabled |= !ready;
        }
        _decisionActions.AddChild(confirm);
        if (pending is null) AddCostBack();
        else if (_decisionSelection is not null) _decisionActions.AddChild(SidebarSkin.Button("Clear selection", () => { _decisionSelection = null; RenderInspector(); Highlight(); }));
        if (!DecisionReviewMode || !DecisionCanSubmit)
            _decisionActions.AddChild(Palette.Wrapped(DecisionCanSubmit ? "Select one option, then confirm." : "Your choice is paused until the connection is ready.", 14, Palette.PaperDim));
        return true;
    }

    /// <summary>
    /// One row per way to pay (issue #1202). Core lists each cost option once per performer, per player ("you" / "your opponent") and per target; the ones it disabled beside an enabled option of the same
    /// way are not another way to pay, so the player is not shown "Bow the Courtier" twice. A way that is disabled everywhere stays, with its reason.
    /// </summary>
    private static ActionOption[] OneRowPerCost(ActionOption[] options)
    {
        string Key(ActionOption a) => a.AlternativeCost?.Id + "|" + string.Join("+", a.Command?.CardIds ?? []) + "|" + (a.AlternativeCost?.Label ?? a.Label);
        var enabledKeys = options.Where(a => a.Enabled).Select(Key).ToHashSet();
        var seenDisabled = new HashSet<string>();
        return options.Where(a => a.Enabled || !enabledKeys.Contains(Key(a)) && seenDisabled.Add(Key(a))).ToArray();
    }

    /// <summary>Back from the cost question to the most recent answer: the target, else the performer. Nothing to go back to when the draft asked for neither.</summary>
    private void AddCostBack()
    {
        var hasTarget = _chosen?.Command?.TargetId is not null && (_chosen.TargetIds?.Length ?? 0) > 0;
        if (!hasTarget && !_performerPicked) return;
        _decisionActions.AddChild(SidebarSkin.Button(hasTarget ? "Back to target" : "Back to performer", () =>
        {
            _reviewingAlternativeCost = false; _decisionSelection = null;
            if (hasTarget) ClearTarget(); else { _performerPicked = false; RenderInspector(); Highlight(); }
        }));
    }

    private void AddDecisionSource(CardView source, string? prompt)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); _inspector.AddChild(row);
        row.AddChild(CardPreview.Face(source, 200, 140));
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; details.AddThemeConstantOverride("separation", 12); row.AddChild(details);
        var name = Palette.Wrapped(source.Definition!.Name, 22); name.AddThemeFontOverride("font", SidebarSkin.Bold); details.AddChild(name);
        if (prompt is not null)
        {
            var shortPrompt = prompt.StartsWith(source.Definition.Name + ": ", StringComparison.Ordinal) ? prompt[(source.Definition.Name.Length + 2)..] : prompt;
            var text = Palette.Wrapped(shortPrompt, 19, Palette.Paper); text.TooltipText = BattleActionText.WithoutIds(View.PendingChoice?.Prompt ?? prompt); details.AddChild(text);
        }
        var inspect = SidebarSkin.Button("Inspect source", () =>
        {
            var (_, body) = Dialog(source.Definition.Name, 720, 720);
            body.AddChild(CardPreview.Face(source, 510));
            body.AddChild(Palette.Wrapped(source.Definition.Text, 17, Palette.Paper));
        });
        inspect.CustomMinimumSize = new(0, 44); details.AddChild(inspect);
        _inspector.AddChild(new HSeparator());
    }

    private void CommitDecision()
    {
        if (!DecisionCanSubmit || _decisionVersion != View.Version || _decisionViewer != View.ViewerId ||
           _decisionSelection is not { Enabled: true, Command: { } command } selected || !Actions.Contains(selected)) return;
        if (GenericDecisionActive)
        {
            if (View.PendingChoice!.PlayerId != View.ViewerId || View.PriorityPlayerId != View.ViewerId) return;
            if (QuoteFor(selected) is { } quote)
            {
                if (!PaymentSelectionReady(AmountFor(selected, quote), quote)) return;
                command = command with { SourceIds = _sources.ToArray() };
            }
            _ = Submit(command); // Pending answers are already complete, including any CardIds or Choice.
        }
        else if (_reviewingAlternativeCost && _chosen?.Command is { } draft)
        {
            if (HasMultiTargets(_chosen))
            {
                if (!MultiSelectionsAllowed(selected)) return;
                _chosen = selected with { Command = command with { TargetId = draft.TargetId, Selections = draft.Selections } };
                _reviewingAlternativeCost = false; _decisionSelection = null; _alternativeCostConfirmed = true;
                ResuggestSources(); RenderInspector(); Highlight(); return;
            }
            if (draft.TargetId is { } target && selected.TargetIds?.Contains(target) != true) return;
            var reviewed = selected with { Command = command with { TargetId = draft.TargetId } };
            // Gold still goes through the existing engine-quoted panel when the ability has both kinds of cost.
            if (QuoteFor(reviewed) is not null) { _reviewingAlternativeCost = false; Choose(reviewed, keepTarget: true); _alternativeCostConfirmed = true; RenderInspector(); return; }
            _ = Submit(reviewed.Command!);
        }
    }
}
