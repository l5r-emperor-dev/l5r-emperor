using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private void RenderInspector()
    {
        RenderInspectorBody();
        // A hand card's other actions stay one click away from any draft of its play (targets, attack targets, slots, movement), not only from the card.
        if (_chosen is { Type: "equip" or "playStrategy" or "useAbility", CardId: { } playedId } chosen && View.PendingChoice is null && IsOwnHandCard(playedId) && FindCard(playedId) is { } playedCard)
            AddPlayAlternatives(_inspector, playedCard, PlayKey(chosen));
    }

    private void RenderInspectorBody()
    {
        if (_chosen is null) { _performerPicked = false; _choicePicked = false; } // the choosing flow starts afresh with each drafted action
        BeginDiscardIfPending(); // issue #791: a required discard is announced and starts as a click-to-choose selection in the hand
        if (_candidateTray is not null) _candidateTray.Visible = false;
        var cardActionsRendered = false;
        Palette.Clear(_sidebarHeader); Palette.Clear(_inspector); Palette.Clear(_decisionActions); _paymentConfirm = null; _goldMeter.Dismiss();
        RenderEitherOrNotice();
        if (View.PendingAttack is { } pendingAttack)
            _sidebarHeader.AddChild(Palette.Wrapped(pendingAttack.Kind + " Attack · " +
                (FindCard(pendingAttack.SourceId)?.Definition?.Name ?? "Resolving"), 16, Palette.Gold));
        _guide.Text = View.PendingChoice?.Prompt ?? (_chosen is { Type: "attack" or "assignDefense" } ? "Select a unit, then its destination province. Review the arrows and confirm assignments." : _chosen is { Type: "purchase" or "recruitReserve" } ? "Choose ready Gold sources. Recruiting pays the cost and brings the card into play." : _chosen is not null && QuoteFor(_chosen) is not null ? "Review the cost and its basis, then choose ready Gold sources. Use the action button when your choices are ready." : ProvinceTargetActive ? "Choose a province in the list or on the table. Review your choice, then use the action button." : _chosen is not null ? "Review the highlighted target, then use the action button. Cancel to change your plan." : View.PriorityPlayerId != View.ViewerId ? "Your opponent is deciding. You can hover over visible cards to read them while you wait." : RulesHelp.PhaseGuide(View.Phase));
        _guide.Visible = View.PendingChoice is not null || _chosen is not null;
        _guide.TooltipText = RulesHelp.Tooltip(RulesHelp.PhaseGuide(View.Phase) + "\n\n" + RulesHelp.Topic("targets"));
        RenderReactionContext();
        if (View.WinnerId is null && View.PendingDuel?.Stage is ("AwaitingAccept" or "Reaction") && View.LastDuel is { } duel)
        {
            _guide.Hide();
            _inspector.AddChild(Palette.Text("CHALLENGE", 25, title: true));
            _inspector.AddChild(Palette.Wrapped($"{duel.Challenger.Definition!.Name} challenges {duel.Challenged.Definition!.Name}", 22));
            if (duel.Challenge is { } stakes) { _inspector.AddChild(Palette.Wrapped(stakes.AbilityLabel, 19, Palette.Gold)); _inspector.AddChild(Palette.Wrapped(stakes.LossTerms, 17)); if (stakes.Refusable) _inspector.AddChild(Palette.Wrapped(stakes.RefusalTerms, 17, Palette.PaperDim)); }
            if (View.PendingChoice is { } reaction) _inspector.AddChild(Palette.Wrapped(reaction.Prompt, 18));
            foreach (var option in Actions.Where(a => a.Enabled && a.Command is not null && a.Type is not ("concede" or "declineReaction")))
            { var command = option.Command!; _inspector.AddChild(SidebarSkin.Button(option.Label, () => _ = Submit(command))); }
            return;
        }
        if (View.PendingChoice?.Type is "eventChoice" or "honorLossDecision" or "entryCostDecision" or "honorChangeReaction" or "killingGroundsSelect" or "infiltratorStraighten" && RenderDecisionChooser())
        { RenderReactionDecline(); return; }
        if (RenderBattleSequence()) return;
        if (RenderLookDecision()) return;
        if (RenderSearchDecision()) return;
        if (RenderEventDecision()) return;
        if (RenderOpponentPayment()) return;
        if (RenderEitherOr()) return;
        if (RenderCopyTray()) return;
        if (RenderMultiTargets()) return;
        if (RenderDecisionChooser()) { RenderReactionDecline(); AddDecisionReviewNavigation(); return; }
        if (RenderMovement()) return;
        if (RenderAttackTargets()) return;
        if (RenderReactionFlow()) { AddDecisionReviewNavigation(); return; }
        if (RenderBattleFlow()) { AddDecisionReviewNavigation(); return; }
        if (PresentationBusy)
        {
            _guide.Visible = false;
            _sidebarHeader.AddChild(new BattleTracker
            {
                Snapshot = View,
                ReducedMotion = _reducedMotion,
                ResolutionMessage = _battleOutcomeText,
                PresentationInProgress = true,
                ActionOutcome = _actionOutcome
            });
            return;
        }
        if (View.WinnerId is { } winner)
        {
            _inspector.AddChild(Palette.Text("MATCH COMPLETE", 12, Palette.Gold));
            _inspector.AddChild(Palette.Text(View.Players[winner].Clan + " prevails", 28, title: true));
            _inspector.AddChild(Palette.Wrapped(View.VictoryReason ?? "Victory"));
            _inspector.AddChild(SidebarSkin.Button("Start another practice", ShowPractice, true));
            _inspector.AddChild(SidebarSkin.Button("Return to main menu", ShowMainMenu)); return; // issue #793
        }
        if (BattleTracker.InBattle(View.Phase) || _resolutionMessage is not null)
        {
            if (_selected is { } supportSelection && IsSupportCard(supportSelection) && !PresentationBusy)
                _sidebarHeader.AddChild(Palette.Wrapped($"Province {View.CurrentBattleProvince + 1} · " + (View.PriorityPlayerId == View.ViewerId ? "Your decision" : "Opponent may act"), 16, Palette.PaperDim));
            else _sidebarHeader.AddChild(new BattleTracker { Snapshot = View, Compact = _selected is not null || _chosen is not null || View.PendingChoice is not null, ReducedMotion = _reducedMotion, ResolutionMessage = _resolutionMessage });
        }
        if (!BattleTracker.InBattle(View.Phase) && _resolutionMessage is null && _selected is not { ActionLocked: true } && !(_selected is { } support && IsSupportCard(support)))
        {
            var heading = Palette.Text(HumanPhase(View.Phase), 25, title: true); heading.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(heading);
            var status = Palette.Wrapped(View.PriorityPlayerId == View.ViewerId ? "Your decision" : "Opponent may act", 18); status.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(status);
            _sidebarHeader.AddChild(new HSeparator());
        }
        if (Actions.Any(a => a.Enabled && a.Type == "declineAdditionalAction"))
            _inspector.AddChild(Palette.Wrapped("Additional action available. Choose an offered action or decline this opportunity.", 18, Palette.Gold));
        if (View.PendingChoice is { } pending)
        {
            _inspector.AddChild(Palette.Text(pending.Type == "discardHand" ? "HAND LIMIT" : pending.Type == "shinden" ? "YOUR CHOICE" : "REACTION WINDOW", 12, Palette.Gold));
            _inspector.AddChild(Palette.Wrapped(pending.Prompt, 20));
            if (pending.Type == "proclaim") _inspector.AddChild(Palette.Wrapped("Honor updates when Proclaim resolves. Each player then gets an opportunity to pass.", 14, Palette.PaperDim));
        }
        if (RenderHomeReminderInspection()) { }
        else if (RenderSupportInspection()) { }
        else if (_selected is { ActionLocked: true, Definition: not null } locked) RenderLockedInspection(locked);
        else if (ProvinceTargetActive) { } // the province list needs the room: the placard below names the card and its action
        else if (_selected?.Definition is { } def)
        {
            var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
            var details = new VBoxContainer(); details.AddThemeConstantOverride("separation", 12); panel.AddChild(details); _inspector.AddChild(panel);
            AddInspectionCaption("Card details", details);
            var name = Palette.Wrapped(def.Name, 24); name.AddThemeFontOverride("font", SidebarSkin.Bold); details.AddChild(name);
            if (CreatedCopyStatus.VisibleOn(_selected)) details.AddChild(CreatedCopyStatus.Explanation(_selected));
            if (CardChanges.Entries(_selected).Length > 0)
                details.AddChild(CardChangePanel.Create(_selected, id => FindCard(id)?.Definition?.Name, compact: true,
                    inspectSource: id => { if (FindCard(id) is { } source) SelectCard(source); }));
            // Copies use ordinary engine actions; keep those controls near their result summary.
            if (_chosen is null && View.PendingChoice is null && CardChanges.Entries(_selected).Any(e => e.Copied))
            {
                foreach (var action in Actions.Where(a => a.CardId == _selected.Id && a.Type is not "proclaim" && Listable(a))) AddAction(details, action);
                cardActionsRendered = true;
            }
            var artwork = CardTile.Artwork(def);
            if (artwork is not null)
            {
                var preview = CardPreview.Face(_selected, def.ScenarioOnly ? 190 : 360);
                details.AddChild(preview);
            }
            if (_selected.Dishonorable) details.AddChild(Palette.Wrapped("Dishonorable", 21, new Color("efaa9a")));
            if (_selected.StraightenBlocked) details.AddChild(Palette.Wrapped("Straightening is prevented for this turn.", 13, Palette.Gold));
            if (!string.IsNullOrWhiteSpace(def.Text)) details.AddChild(Palette.Wrapped(def.Text, 16, Palette.PaperDim));
            AddActionTextBlocks(details, _selected);
            if (def.Type == CardType.Personality && (_selected.Force != def.Force || _selected.Chi != def.Chi)) details.AddChild(Palette.Wrapped($"Current: {_selected.Force} Force · {_selected.Chi} Chi", 16, Palette.Gold));
            if (_selected.AttachedTo is { } parent) details.AddChild(Palette.Wrapped("Attached to " + (FindCard(parent)?.Definition?.Name ?? parent), 13, Palette.Crane));
        }
        else if (_selected is not null)
        {
            AddInspectionCaption("FACE-DOWN DYNASTY");
            _inspector.AddChild(Palette.Wrapped("This card's identity remains hidden until the rules reveal it.", 15, Palette.PaperDim));
        }
        else if (View.PendingChoice is null)
        {

            var help = Palette.Wrapped("Select a card to inspect it.", 16, Palette.PaperDim);
            help.MouseFilter = Control.MouseFilterEnum.Stop; help.TooltipText = RulesHelp.PhaseGuide(View.Phase);
            _inspector.AddChild(help);
        }
        if (_playPrompt is not null && _chosen is null && RenderPlayPrompt()) return;
        if (_chosen is { Type: "purchase" or "recruitReserve", Quote: not null }) { RenderPurchase(); return; }
        if (_chosen is { Type: "attack" or "assignDefense" }) { RenderAssignments(); return; }
        if (_chosen is { Type: "useAbility", Command.CardIds: not null }) { RenderProvinceRefresh(); return; }
        if (_chosen is { Type: "equip" or "playStrategy" or "useAbility" }) { RenderTargeted(); AddDecisionReviewNavigation(); return; }
        if (_chosen is { Type: "discardHand" }) { RenderHandDiscard(); return; }
        // An inspected reminder must never hide a new engine decision (e.g. hand limit).
        if (_selectedHomeReminder is not null && View.PendingChoice is null) { AddHomeReminderActions(); return; }
        if (View.PendingChoice?.Type is "bowReaction" or "beforeActionReaction")
        {
            RenderReactionTarget();
            foreach (var reaction in Actions.Where(a => a.Type == "useAbility" && Listable(a))) AddAction(_inspector, reaction);
            return;
        }
        if (_selected is { } supportCard && IsSupportCard(supportCard) && View.PendingChoice is null)
        { RenderSupportActions(supportCard); return; }
        if (!cardActionsRendered && _selected is { } listedCard) AddCardActions(_inspector, listedCard); // one entry per play; the performer, cost, player and target are asked in the draft (issue #1202)
        if (_selected is { ActionLocked: true, Definition: not null } inspected)
            _inspector.AddChild(SidebarSkin.Button("Inspect card", () => InspectLockedCard(inspected)));
        if (View.Phase is Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense)
        {
            var action = Actions.FirstOrDefault(a => a.Type is "attack" or "assignDefense" && a.Enabled);
            if (action is not null) _inspector.AddChild(SidebarSkin.Button("Assign units…", () => Choose(action), true));
        }
        foreach (var action in Actions.Where(a => a.Type is "selectBattle" or "discardHand" || a.Type == "useAbility" && a.CardId is null && Listable(a))) AddAction(_inspector, action);
        AddDecisionReviewNavigation();
    }

    // Issue #639: a Personality bought with Proclaim available is proclaimed once it is paid for. Choosing the Clan discount (which cannot Proclaim) is how a player declines.
    private string? _autoProclaimCardId;
    private void AutoProclaim(SessionSnapshot snapshot)
    {
        if (_autoProclaimCardId is not { } cardId) return;
        if (snapshot.View.PendingChoice is not { Type: "proclaim" } pending || pending.CardId != cardId) return;
        _autoProclaimCardId = null;
        if (snapshot.Actions.FirstOrDefault(a => a.Type == "proclaim" && a.Enabled && a.Command is not null) is { } proclaim) _ = Submit(proclaim.Command!);
    }

    // The recruit review for purchase and recruitReserve: their own mode picker and Proclaim notes (from the
    // engine's PurchaseQuote), then the ONE payment panel every costed action shares.
    private void RenderPurchase()
    {
        var choice = _chosen!;
        var reviewedVersion = choice.Command?.ExpectedVersion ?? View.Version;
        var quote = choice.Quote!;
        var variants = Actions.Where(a => a.Type == choice.Type && a.CardId == choice.CardId && a.Quote is not null).ToArray();
        _inspector.AddChild(Palette.Text(choice.Type == "recruitReserve" ? "RESERVE RECRUIT OPTIONS" : "PURCHASE OPTIONS", 11, Palette.Gold));
        if (variants.Length > 1)
        {
            var modes = new OptionButton { CustomMinimumSize = new Vector2(0, 38), FitToLongestItem = false, ClipText = true };
            foreach (var option in variants)
            {
                var q = option.Quote!;
                var mode = q.ClanDiscount ? q.ClanAllegiance ? "Discount + allegiance" : "Clan discount" : q.ClanAllegiance ? "Clan allegiance" : q.ProclaimEligible ? "Full cost · Proclaim eligible" : "Full cost";
                modes.AddItem($"{q.Cost} Gold · {mode}");
            }
            modes.Selected = Array.IndexOf(variants, choice);
            modes.ItemSelected += index => Choose(variants[(int)index], false, true); // switching Proclaim / Clan discount keeps the bowed holdings
            _inspector.AddChild(modes);
        }
        _inspector.AddChild(Palette.Wrapped(quote.ClanDiscount ? "Clan discount reduces the Gold cost by 2. This purchase cannot trigger Proclaim." : quote.ProclaimEligible ? "Paying without the Clan discount Proclaims the Personality as soon as it is paid for." : "This card is not eligible for Proclaim.", 13, Palette.PaperDim));
        if (quote.ClanAllegiance) _inspector.AddChild(Palette.Wrapped("Clan allegiance adds 2 Gold and ignores the Honor requirement.", 13, Palette.Crane));
        var (required, ready) = RenderGoldPanel(choice);
        AddConfirm(CommitButtonLabel(choice), required, ready,
            () =>
            {
                _autoProclaimCardId = choice.Type == "purchase" && quote.ProclaimEligible ? choice.CardId : null; // issue #639
                return new GameCommand(choice.Type, View.ViewerId, reviewedVersion, choice.CardId, required > 0 ? _sources.ToArray() : [], ClanDiscount: quote.ClanDiscount, ClanAllegiance: quote.ClanAllegiance);
            },
            "Cancel payment");
        // Issue #645: a face-up Dynasty card can be discarded straight from its purchase review, as a placard under Confirm purchase.
        if (choice.Type == "purchase" && Actions.FirstOrDefault(a => a.Type == "discardProvince" && a.CardId == choice.CardId && a.Command is not null) is { } discard)
        {
            var button = SidebarSkin.Button("Discard", () => ConfirmDiscardProvince(discard));
            button.Name = "DiscardProvinceCard"; button.Disabled = !discard.Enabled || _session.Busy || _session.Online && !_session.Connected;
            button.TooltipText = RulesHelp.Tooltip(discard.Label + "\n\n" + RulesHelp.ActionGuide(discard.Type));
            _decisionActions.AddChild(button); _decisionActions.MoveChild(button, 1);
        }
    }
    // Client-only guard (Core, the AI and probes that Submit directly are unaffected): ending the Dynasty Phase with nothing bought, overlaid or discarded.
    // A discard moves the match to DynastyDiscard, so only the Dynasty step can still be an empty one.
    private bool NeedsEndDynastyConfirmation() =>
        View.Phase == Phase.Dynasty && Self is { DynastyActionTaken: false };

    private void ConfirmDiscardProvince(ActionOption option) =>
        Confirm("Discard this province card?", "Discarding starts the Dynasty discard step. You cannot recruit more cards this phase.", () => _ = Submit(option.Command!));

    // The quote the panel shows for the chosen action, straight from the engine: the chosen target's own quote
    // when the cost depends on the target, otherwise the option's single quote. The client never derives a cost.
    private static GoldQuote? QuoteFor(ActionOption action) =>
        action.Command?.TargetId is string target && action.TargetGold is { } byTarget && byTarget.TryGetValue(target, out var targeted) ? targeted : action.Gold;

    // The amount this action pays now: the chosen one for a variable cost (default: the quote's minimum),
    // otherwise the quote's own Cost.
    private static int AmountFor(ActionOption action, GoldQuote quote) =>
        quote.Kind == GoldCostKind.Variable ? Math.Clamp(action.Command?.GoldAmount ?? quote.MinAmount ?? 0, quote.MinAmount ?? 0, quote.MaxAmount ?? quote.MinAmount ?? 0) : quote.Cost;

    // The engine's suggested and alternative payments for the amount being paid (per-amount for a variable cost).
    private static PaymentOption[] PaymentsFor(GoldQuote quote, int amount) =>
        quote.PaymentsByAmount is { } byAmount && byAmount.TryGetValue(amount, out var options) ? options : quote.Payments;

    private static string KindLabel(string kind) => kind switch { GoldCostKind.Variable => "YOU CHOOSE THE AMOUNT", GoldCostKind.Derived => "COST FROM YOUR TARGET", _ => "FIXED COST" };

    // Re-suggest sources from the engine's first payment for the quote now in force (called when the target or
    // the chosen amount changes; the player's own selection is only ever replaced by such a deliberate change).
    private void ResuggestSources()
    {
        if (_chosen is null || QuoteFor(_chosen) is not { } quote) { _sources.Clear(); return; }
        // The player's own picks survive a new quote when still offered; nothing is re-selected for them.
        var offered = PaymentMethods(quote).Select(m => m.Id).ToHashSet();
        _sources.RemoveWhere(id => !offered.Contains(id));
        SyncPaymentAmount();
        if (!PreviewBowedCards().SetEquals(_previewBows)) RenderBoard();
    }

    // Retarget the reviewed action: its command, the quote it is priced by and (when that quote is target-specific)
    // the suggested sources all follow, instantly and without a round trip.
    private void ChooseTarget(string id)
    {
        if (AttackTargetActive) { ChooseAttackTarget(AttackChoice.Card, id); return; }
        _alternativeCostConfirmed = false;
        if (_chosen?.Command is not { } command) return;
        var priced = _chosen.TargetGold?.ContainsKey(id) == true;
        var retargeted = RetargetVariant(_chosen, id); // a target that belongs to another variant (another Holding) selects that variant
        _chosen = retargeted with { Command = retargeted.Command! with { TargetId = id } };
        if (priced) ResuggestSources();
        if (MovementActive) RenderBoard();
        RenderInspector(); Highlight();
        if (MovementActive) FocusMovement();
    }

    private void ChooseAmount(int amount)
    {
        if (_chosen?.Command is not { } command || QuoteFor(_chosen) is not { Kind: GoldCostKind.Variable } quote) return;
        _chosen = _chosen with { Command = command with { GoldAmount = Math.Clamp(amount, quote.MinAmount ?? 0, quote.MaxAmount ?? quote.MinAmount ?? 0) } };
        ResuggestSources();
        RenderInspector(); Highlight();
    }

    // THE payment panel: what the action costs and why, the amount control for a player-chosen cost, the engine's
    // suggested and alternative payments, the sources to bow and the produced/required line. Every Gold action
    // (recruit, reserve recruit, equip, Strategy, any costed ability) draws it from its GoldQuote and nothing else.
    // Returns the amount to be paid and whether the chosen sources already cover it.
    private (int Required, bool Ready) RenderGoldPanel(ActionOption action)
    {
        if (QuoteFor(action) is not { } quote) return (0, true);
        var required = AmountFor(action, quote);
        var opponent = action.Type == "chooseOpponentPayment";
        if (opponent) RenderOpponentAmount(required, quote);
        else
        {
            _inspector.AddChild(Palette.Text("PAYMENT · " + KindLabel(quote.Kind), 11, Palette.Gold));
            _inspector.AddChild(Palette.Wrapped($"{required} Gold", 24, Palette.Gold));
            if (!string.IsNullOrWhiteSpace(quote.Basis)) _inspector.AddChild(Palette.Wrapped(quote.Basis, 13, Palette.PaperDim));
            if (quote.Kind == GoldCostKind.Variable)
                _inspector.AddChild(Palette.Wrapped($"Bow holdings to choose the amount: you pay the Gold you contribute ({quote.MinAmount ?? 0}–{quote.MaxAmount ?? quote.MinAmount ?? 0}).", 14, Palette.PaperDim));
        }
        if (quote.Error is { } error) _inspector.AddChild(Palette.Wrapped(error, 14, Palette.Error));
        var payments = PaymentsFor(quote, required);
        if (payments.Length > 1 && !opponent)
        {
            var suggestions = new OptionButton { CustomMinimumSize = new Vector2(0, 36), FitToLongestItem = false, ClipText = true };
            for (var i = 0; i < payments.Length; i++)
            {
                var p = payments[i];
                suggestions.AddItem($"{(i == 0 ? "Suggested" : "Alternative " + i)} · {p.SourceIds.Length} sources · {p.Excess} excess");
            }
            suggestions.ItemSelected += i => { _sources.Clear(); foreach (var id in payments[(int)i].SourceIds) _sources.Add(id); RefreshPaymentSelection(); };
            suggestions.Selected = Array.FindIndex(payments, p => p.SourceIds.ToHashSet().SetEquals(_sources));
            _inspector.AddChild(suggestions);
        }
        var variable = quote.Kind == GoldCostKind.Variable && !opponent;
        if (required > 0 || opponent || variable) RenderPaymentMethods(required, quote);
        else _inspector.AddChild(Palette.Wrapped("No Gold sources are needed.", 15, Palette.Crane));
        if (required > 0 || opponent || variable)
        {
            var picked = PaymentMethods(quote).Where(m => _sources.Contains(m.Id)).ToArray();
            var cost = variable ? (int?)null : Math.Max(0, required - picked.Where(m => m.Kind == "discount").Sum(m => m.Produced));
            UpdateGoldMeter(PaidGold(quote), cost);
        }
        return (required, quote.Error is null && PaymentSelectionReady(required, quote));
    }

    private void RenderAmountControl(int amount, int min, int max)
    {
        _inspector.AddChild(Palette.Text("HOW MUCH GOLD", 11, Palette.Gold));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        var less = SidebarSkin.Button("−", () => ChooseAmount(amount - 1)); less.Disabled = amount <= min; less.CustomMinimumSize = new Vector2(48, 38);
        var value = Palette.Text($"{amount} Gold  ({min}–{max})", 17); value.HorizontalAlignment = HorizontalAlignment.Center; value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var more = SidebarSkin.Button("+", () => ChooseAmount(amount + 1)); more.Disabled = amount >= max; more.CustomMinimumSize = new Vector2(48, 38);
        var most = SidebarSkin.Button("Max", () => ChooseAmount(max)); most.Disabled = amount >= max; most.CustomMinimumSize = new Vector2(60, 38);
        row.AddChild(less); row.AddChild(value); row.AddChild(more); row.AddChild(most);
        _inspector.AddChild(row);
    }

    // The confirm/cancel pair every reviewed action ends with. A free action confirms in one click with no
    // sources; a costed one names its cost and waits until the chosen sources cover it.
    private void AddConfirm(string verb, int required, bool ready, Func<GameCommand> build, string cancel, bool enabled = true)
    {
        // A variable cost (the player's choice of Gold) is offered at once; a zero-Gold pick must be confirmed deliberately.
        var variable = _chosen is { Type: not "chooseOpponentPayment" } chosen && QuoteFor(chosen) is { Kind: GoldCostKind.Variable };
        _paymentConfirm = SidebarSkin.Button(required > 0 ? $"{verb} · {required} Gold" : verb, () =>
        {
            var command = build();
            if (variable && required == 0)
                Confirm("Pay 0 gold?", "You have elected to pay 0 gold. Are you sure you would like to proceed?", () => _ = Submit(command), "Yes", "No");
            else _ = Submit(command);
        }, true);
        _paymentConfirm.Name = "CommitAction";
        _paymentConfirm.Disabled = !enabled || !ready || _session.Busy;
        // A fixed cost shows Confirm only once the holdings bowed so far cover it.
        _paymentConfirm.Visible = ready || required == 0 || variable;
        _decisionActions.AddChild(_paymentConfirm);
        _decisionActions.AddChild(SidebarSkin.Button(cancel, CancelChoice));
    }

    private int SelectedProduction() => _chosen is not null && QuoteFor(_chosen) is { Sources: { } methods }
        ? methods.Where(m => _sources.Contains(m.Id)).Sum(m => m.Produced)
        : 0; // Core's quote names every source and what it produces; the client keeps no card list of its own (issue #1028)

    private void RenderAssignments()
    {
        var action = _chosen!;
        var reviewedVersion = action.Command?.ExpectedVersion ?? View.Version;
        _inspector.AddChild(Palette.Text("ASSIGN YOUR UNITS", 12, Palette.Gold));
        if (_assignmentUnit is string selected && FindCard(selected) is { } unit)
            _inspector.AddChild(Palette.Wrapped("Selected: " + ShortName(unit) + (_assignments.ContainsKey(selected) ? " · click the unit again to send it home" : ""), 16, Palette.Crane));
        foreach (var id in action.TargetIds ?? [])
        {
            var card = FindCard(id)!;
            _inspector.AddChild(Palette.Wrapped($"{ShortName(card)}", 14));
            var provinces = action.Provinces ?? [];
            var picker = new OptionButton(); picker.AddItem("Stay at home");
            foreach (var province in provinces) picker.AddItem(BattlefieldName(province));
            picker.Selected = _assignments.TryGetValue(id, out var assigned) ? Array.IndexOf(provinces, assigned) + 1 : 0;
            picker.ItemSelected += i => { _selected = card; _assignmentUnit = id; if (i == 0) _assignments.Remove(id); else _assignments[id] = provinces[(int)i - 1]; RenderBoard(); RenderInspector(); Highlight(); };
            _inspector.AddChild(picker);
        }
        _decisionActions.AddChild(SidebarSkin.Button("Keep selected unit home", () => { if (_assignmentUnit is string id) _assignments.Remove(id); RenderBoard(); RenderInspector(); Highlight(); }));
        _decisionActions.AddChild(SidebarSkin.Button("Confirm assignments", () => _ = Submit(new GameCommand(action.Type, View.ViewerId, reviewedVersion, Assignments: new Dictionary<string, int>(_assignments))), true));
        _decisionActions.AddChild(SidebarSkin.Button("Cancel assignment", CancelChoice));
    }

    /// <summary>Issue #635: a list of legal cards whose rows and table tiles highlight each other (see CardChoiceList).</summary>
    private CardChoiceList NewChoiceList(bool enabled = true) => new(id => _tiles.Where(t => IsInstanceValid(t) && t.Card.Id == id).ToArray()) { Enabled = enabled };

    // Scrolls the sidebar to a control once layout has settled, so a pending choice is never hidden below the fold.
    private async void ScrollInspectorTo(Control target)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInstanceValid(target) || !target.IsInsideTree()) return;
        for (Node? parent = target.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) { scroll.EnsureControlVisible(target); return; }
    }

    private void RenderTargeted()
    {
        var action = _chosen!;
        if (action.CardId is { } supportId && FindCard(supportId) is { } support && IsSupportCard(support))
        { RenderSupportDraft(support, action); return; }
        var command = action.Command!;
        var targets = action.TargetIds ?? [];
        var target = command.TargetId; // issue #581: no default; Confirm waits for the player's pick when the action names legal targets
        var needsTarget = targets.Length > 0 && target is null;
        var needsChoice = ChoiceStepActive; // issue #1202: which of the target's options (the ability a Holding copies), asked once the target is chosen
        var needsProvince = IsProvinceTarget(action) && command.Province is null; // the player picks the province
        if (PerformerStepActive) { RenderPerformerStep(); return; } // choosing flow (issue #1004): the performer is the first, explicit choice
        _inspector.AddChild(Palette.Wrapped(Placard(action, IsProvinceTarget(action) ? WithoutProvinceOwner(action.Label) : action.ChoiceLabel is not null || VariantsOf(action).Length > 1 ? FlowLabel(action) : null), 17, Palette.Gold));
        if (needsTarget) _inspector.AddChild(Palette.Wrapped("Next: choose a target. Pick a highlighted card on the table or in your hand, or a row in the list below.", 17, Palette.Gold));
        else if (targets.Length > 0) _inspector.AddChild(Palette.Wrapped("Choose a highlighted card on the table or below. Review the arrow, then use the action button.", 13, Palette.PaperDim));
        if (command.PerformerId is string performer)
            _inspector.AddChild(Palette.Wrapped("Performer: " + ShortName(FindCard(performer)!) + (CardTile.UnitLabel(FindCard(performer)!) is { Length: > 0 } unit ? " · " + unit : ""), 14, Palette.Crane));
        if (!ReactionFlowOwnsControls && PerformerClicks().Count > 0) _inspector.AddChild(Palette.Wrapped("Choose the performing Personality: click a highlighted Personality on the table.", 15, Palette.Gold)); // issue #793
        // Choosing flow (issue #1004): only the first unanswered question is asked; answered ones leave the sidebar and appear in the summary.
        var needsPlayer = PlayerTargetActive && !PlayerPicked; // issue #960: "choose a player" is the player's decision, never a default
        var askProvince = needsProvince; var askPlayer = !askProvince && needsPlayer; var askTarget = !askProvince && !askPlayer && needsTarget; var askChoice = !askProvince && !askPlayer && !askTarget && needsChoice;
        var answeredAll = !needsProvince && !needsPlayer && !needsTarget && !needsChoice;
        if (IsProvinceTarget(action) && (askProvince || answeredAll)) RenderProvincePicker(action);
        if (PlayerTargetActive && (askPlayer || answeredAll)) RenderPlayerPicker(action);
        if (targets.Length > 0 && (askTarget || answeredAll))
        {
            _inspector.AddChild(Palette.Text(command.AbilityId is "prepared-death" or "defensive-stance" or "lobby" ? "CHOOSE A PERFORMER" : command.AbilityId == "favor-cycle" ? "CHOOSE A CARD TO DISCARD" : "CHOOSE A TARGET", 11, Palette.Gold));
            var picker = NewChoiceList(); // issue #635: every legal card is a visible row that lights its Personality on the table and is lit by it
            foreach (var id in targets)
            {
                var card = FindCard(id)!;
                // A target-derived cost is part of the choice: each target's own engine quote leads the label.
                var price = action.TargetGold?.TryGetValue(id, out var priced) == true ? $" · {priced.Cost} Gold" : "";
                picker.AddItem($"{ShortName(card)}{price} · {card.Force} F{(card.Bowed ? " · bowed" : "")}", id);
            }
            picker.Selected = target is null ? -1 : Array.IndexOf(targets, target);
            picker.ItemSelected += i => { if (targets[i] == _chosen?.Command?.TargetId) ClearTarget(); else ChooseTarget(targets[i]); }; // choosing the chosen row again clears it
            _inspector.AddChild(picker);
            if (needsTarget) _inspector.AddChild(Palette.Wrapped("Choose a target to continue.", 13, Palette.Error));
            if (needsTarget) ScrollInspectorTo(picker); // the list can sit below a long card text; bring the choice into view
        }
        if (askChoice) RenderChoicePicker(action);
        if (answeredAll) RenderFlowSummary(action);
        AddFlowBack(action);
        if (action.AlternativeCost is not null && !_alternativeCostConfirmed)
        {
            var review = SidebarSkin.Button("Review cost choices", () => { _reviewingAlternativeCost = true; _decisionSelection = null; RenderInspector(); }, true);
            review.Disabled = needsTarget; // the cost choice confirms a command, which needs the target the player has not picked yet
            _decisionActions.AddChild(review);
            _decisionActions.AddChild(SidebarSkin.Button("Cancel action", CancelChoice)); return;
        }
        if (needsTarget && action.TargetGold is not null)
        { // a target-derived cost belongs to the target: none is shown until the player picks one (issue #581)
            AddConfirm(CommitButtonLabel(action), 0, false, () => command, "Cancel action", false); return;
        }
        var (required, ready) = RenderGoldPanel(action);
        AddConfirm(CommitButtonLabel(action), required, ready, () => command with { SourceIds = required > 0 ? _sources.ToArray() : [], TargetId = target }, "Cancel action", action.Enabled && !needsTarget && !needsProvince && !needsPlayer && !needsChoice);
    }

    // Back to "Choose a target…": the action keeps its legal targets but none is selected.
    private void ClearTarget()
    {
        if (_chosen?.Command is not { } command) return;
        _chosen = _chosen with { Command = command with { TargetId = null } }; _choicePicked = false;
        RenderInspector(); Highlight();
    }

    // Border Keep's refresh: the Dynasty cards to refresh are picked by clicking them in their provinces (a second click deselects); the list keeps the order they were chosen in.
    private readonly List<string> _refreshSelected = [];
    private bool ProvinceRefreshActive => _chosen is { Type: "useAbility", Command.CardIds: not null };

    private void RenderProvinceRefresh()
    {
        var action = _chosen!;
        _refreshSelected.RemoveAll(id => action.TargetIds?.Contains(id) != true);
        _inspector.AddChild(Palette.Wrapped(action.Label, 18, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped("Click the highlighted Dynasty cards in your provinces to select them (click again to deselect). They go to the bottom of the Dynasty deck in the order you choose; replacements are revealed face-up.", 14, Palette.PaperDim));
        _inspector.AddChild(Palette.Wrapped(_refreshSelected.Count == 0 ? "No cards selected."
            : "Bottom order: " + string.Join(" → ", _refreshSelected.Select(id => "P" + ((FindCard(id)?.ProvinceIndex ?? 0) + 1))), 13, Palette.Crane));
        var confirm = SidebarSkin.Button("Confirm", () => _ = Submit(action.Command! with { CardIds = _refreshSelected.ToArray() }), true);
        confirm.Disabled = _refreshSelected.Count == 0;
        _decisionActions.AddChild(confirm);
        _decisionActions.AddChild(SidebarSkin.Button("Cancel", CancelChoice));
    }

    private void CancelChoice() { _playPrompt = null; ResetDecisionSelection(); SetNotice("Selection cancelled. No action was taken."); _selected = null; _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; RenderBoard(); RenderInspector(); RenderPhaseActions(); Highlight(); }

    // An ability or reaction that cannot be used right now (no legal target or performer, a condition unmet, Honor too low to Lobby) is not offered at all; the engine still
    // lists it with its reason, but a menu of things the player cannot do only hides the ones they can.
    private static bool Listable(ActionOption option) => option.Enabled || option.Type != "useAbility";

    private void AddAction(VBoxContainer container, ActionOption option)
    {
        if (IsProvinceTarget(option)) { AddProvinceTargetAction(container, option); return; }
        if (option.AlternativeCost is { } cost)
        {
            // Issue #1202: one entry for the ability, whichever of the player's cards could perform it; the performer is the first question of the draft.
            var key = $"{option.CardId}/{cost.AbilityId}";
            if (container.GetChildren().Any(c => c.HasMeta("alternative_cost_group") && c.GetMeta("alternative_cost_group").AsString() == key)) return;
            var choices = Actions.Where(a => a.CardId == option.CardId && a.AlternativeCost?.AbilityId == cost.AbilityId).ToArray();
            var representative = choices.FirstOrDefault(a => a.Enabled) ?? option;
            var performers = choices.Select(a => a.AlternativeCost!.PerformerId).Distinct().ToArray();
            var grouped = SidebarSkin.Button(cost.ActionLabel + (performers.Length == 1 && cost.PerformerId is { } id && FindCard(id) is { } performer ? " · " + performer.Definition?.Name : ""), () =>
            {
                SelectReactionSource(representative); Choose(representative);
            });
            grouped.SetMeta("alternative_cost_group", key); grouped.Disabled = !representative.Enabled || !DecisionCanSubmit;
            IdentifyReactionOption(grouped, representative); LinkToTable(grouped, ActionCards(representative));
            grouped.TooltipText = representative.Reason ?? cost.ActionLabel;
            container.AddChild(grouped);
            if (!representative.Enabled) container.AddChild(Palette.Wrapped(representative.Reason ?? "No available way to pay.", 15, Palette.PaperDim));
            return;
        }
        var label = option.Type == "discardProvince" ? "Discard" : option.Type is "purchase" or "recruitReserve" ? "Review payment…" : Placard(option);
        if (View.ReactionPresentation is not null && option.Type is "useAbility" or "playStrategy" or "equip")
            label = ReactionOptionLabel(option);
        var button = SidebarSkin.Button(label, () =>
        {
            SelectReactionSource(option);
            if (option.Type == "useAbility" && View.PendingChoice?.Type is "bowReaction" or "beforeActionReaction" && option.TargetIds is { Length: 0 } && option.Command is { } reaction && QuoteFor(option) is null) _ = Submit(reaction);
            else if (_chosen is null && View.PendingChoice is null && option.Type is "equip" or "playStrategy" or "useAbility" && IsOwnHandCard(option.CardId) && option.Command is not null) BeginPlayPrompt(option); // a reaction window's options are already a deliberate pick
            else if (option.Type is "purchase" or "recruitReserve" or "equip" or "playStrategy" or "attack" or "assignDefense" or "discardHand" or "useAbility" or "chooseOpponentPayment") Choose(option);
            else if (option.Type == "discardProvince") ConfirmDiscardProvince(option);
            else if (option.Type == "endDynasty" && option.Command is { } endCommand && NeedsEndDynastyConfirmation())
                Confirm("End your Dynasty phase?", "You have not bought or discarded anything from your provinces this turn. End your Dynasty phase anyway?", () => _ = Submit(endCommand));
            else if (option.Command is { } command) _ = Submit(command);
        }, option.Type is "pass" or "endDynasty" or "declineReaction", danger: option.Type == "declareAttack");
        button.Disabled = !option.Enabled || _session.Busy || _session.Online && !_session.Connected;
        IdentifyReactionOption(button, option); LinkToTable(button, ActionCards(option));
        button.TooltipText = RulesHelp.Tooltip((option.Reason is { } reasonText ? "Unavailable: " + reasonText + "\n\n" : option.Label + "\n\n") + RulesHelp.ActionGuide(option.Type));
        button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        button.ClipText = true; container.AddChild(button);
        if (_selected is { ActionLocked: true } locked && option.CardId == locked.Id && !option.Enabled && option.Reason is { } reason)
            container.AddChild(Palette.Wrapped(reason, 15, Palette.PaperDim));
    }

    private void RenderPhaseActions()
    {
        if (PresentationBusy) { Palette.Clear(_phaseActions); _phaseActions.AddChild(SidebarSkin.Button("Review battle", ShowBattleHistory)); return; }
        Palette.Clear(_phaseActions);
        if (_session.Snapshot is null) return;
        if (_battlefieldReviewView is not null)
        {
            _phaseActions.AddChild(Palette.Wrapped("Presentation preview\nUse the prepared scenarios above the table. Card effects are not running.", 17, Palette.PaperDim));
            return;
        }
        if (View.PendingProvinceOrder is { } order)
        {
            _phaseActions.AddChild(Palette.Wrapped(order.PlayerId == View.ViewerId ? "Choose an order in the tray." : "Waiting for the province's controller to choose an order.", 18, Palette.Gold));
            if (_session.Online && !_session.Connected)
                _phaseActions.AddChild(SidebarSkin.Button("Reconnect", () => _ = Reconnect(), true));
            else if (!_session.Online && !_autoOpponent && _provinceReviewView is null && order.PlayerId != View.ViewerId)
                _phaseActions.AddChild(SidebarSkin.Button("Pass device to deciding player", PassProvinceDecision));
            return;
        }
        if (_provinceReviewView is not null)
        {
            _phaseActions.AddChild(Palette.Wrapped("Presentation preview\nUse the prepared scenarios above the table. Card effects are not running.", 17, Palette.PaperDim));
            _phaseActions.AddChild(SidebarSkin.Button("Focus province", FocusProvinceReview));
            return;
        }
        if (View.WinnerId is not null) return;
        if (_session.Online && !_session.Connected)
        {
            _phaseActions.AddChild(Palette.Wrapped("Disconnected. Reconnect to continue.", 14, Palette.Error));
            _phaseActions.AddChild(SidebarSkin.Button("Reconnect", () => _ = Reconnect(), true)); return;
        }
        if (ReactionFlowOwnsControls) return;
        if (_chosen is not null || _playPrompt is not null || EventDecisionActive || GenericDecisionActive || EitherOrActive || OpponentPaymentActive || CopyDecisionActive || LookDecisionActive) return;
        _phaseActions.AddChild(new HSeparator());
        if (_session.Online && (_session.Snapshot.PlayersConnected?.Any(p => !p) ?? false))
        {
            _phaseActions.AddChild(Palette.Wrapped($"Room {_session.Snapshot.RoomCode}\nWaiting for your opponent to connect.", 16, Palette.Crane)); return;
        }
        if (View.Phase == Phase.Battle && View.PendingChoice is null && View.PriorityPlayerId == View.ViewerId && View.BattleStep?.ConsecutivePasses == 1)
            _phaseActions.AddChild(Palette.Wrapped("Passing now ends Battle actions.", 16, Palette.PaperDim));
        foreach (var a in Actions.Where(a => a.Type is "pass" or "passAllBattles" or "endDynasty" or "proclaim" or "declineReaction" or "declineAdditionalAction" or "chooseShinden" or "declareAttack")) AddAction(_phaseActions, a);
        if (View.PriorityPlayerId != View.ViewerId) _phaseActions.AddChild(Palette.Wrapped($"Waiting for {View.Players[View.PriorityPlayerId].Clan}…", 15, Palette.Crane));
        if (!_session.Online && !_autoOpponent) _phaseActions.AddChild(SidebarSkin.Button("Pass device to other player", SwitchSeat));
    }
}
