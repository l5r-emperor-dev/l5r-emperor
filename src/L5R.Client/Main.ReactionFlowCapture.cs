using Godot;
using L5R.Core;
using System.Reflection;
using System.Net.WebSockets;

namespace L5R.Client;

// Issue #722: exercise player gestures against published cards and engine-created
// windows. Prepared positions are fixtures; all decisions after loading use GUI input.
public partial class Main
{
    private bool _reactionCaptureStarted;
    private sealed record ReactionCaptureFixture(MatchEngine Engine, string Source, string Target, string? SecondSource = null, string? Performer = null);

    private void ProcessReactionFlowCapture(double delta)
    {
        _captureClock += delta;
        if (_reactionCaptureStarted || _captureClock < 1.5) return;
        _reactionCaptureStarted = true; _ = CaptureReactionFlow();
    }

    private static MatchEngine ReactionCaptureEngine(MatchState state) =>
        (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;

    private static MatchState ReactionCaptureTable(int seat, Phase phase = Phase.Battle)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        // Keep the real decks/provinces, but isolate each prepared position from the
        // starter army and hand so unrelated reactions cannot contaminate the probe.
        foreach (var player in state.Players) { player.Hand.Clear(); player.TurnsTaken = 2; }
        foreach (var card in state.Cards.Values.Where(c => c.Zone is Zone.Hand or Zone.Home or Zone.Attached or Zone.Battlefield or Zone.Celestial or Zone.EventInPlay).ToArray())
            state.Cards.Remove(card.Id);
        state.Phase = phase; state.ActivePlayerId = state.PriorityPlayerId = seat;
        state.CurrentBattleProvince = phase == Phase.BattleSelection ? null : 0;
        state.UnresolvedBattlefields = [0]; state.PendingChoice = null;
        foreach (var stronghold in state.Cards.Values.Where(c => c.Zone == Zone.Stronghold)) stronghold.Bowed = false;
        return state;
    }

    private static void ReactionCaptureAccept(MatchEngine engine, GameCommand command)
    {
        var result = engine.Execute(command);
        SupportRequire(result.Accepted, "Prepared reaction trigger refused: " + result.Error);
    }

    private static ReactionCaptureFixture ReactionCaptureBow(int seat, bool followup = false, bool nested = false)
    {
        var state = ReactionCaptureTable(seat);
        var ruya = SupportFixtureCard(state, 7789, seat, Zone.Battlefield, 0);
        var second = followup ? SupportFixtureCard(state, 7789, seat, Zone.Battlefield, 0).Id : null;
        var horibe = SupportFixtureCard(state, 6885, seat, Zone.Battlefield, 0);
        var first = SupportFixtureCard(state, 2060, 1 - seat, Zone.Battlefield, 0);
        var target = SupportFixtureCard(state, 3636, 1 - seat, Zone.Battlefield, 0);
        var hizumi = nested ? SupportFixtureCard(state, 8521, 1 - seat, Zone.Battlefield, 0) : null;
        if (hizumi is not null) hizumi.AbilityUses["hizumi-negate-straighten"] = state.TurnNumber;
        var engine = ReactionCaptureEngine(state);
        var trigger = engine.QueryActions(seat).Single(a => a.Enabled && a.CardId == horibe.Id && a.Command?.AbilityId == "horibe-bow");
        ReactionCaptureAccept(engine, trigger.Command! with { TargetId = first.Id });
        SupportRequire(engine.State.PendingChoice?.Type == "bowReaction", "Published Horibe did not open Ruya's bow window");
        if (hizumi is not null)
        {
            var ready = engine.State; ready.Cards[hizumi.Id].AbilityUses.Clear();
            engine = ReactionCaptureEngine(ready);
        }
        // Watcher order belongs to Core. Two identical Ruyas need not be offered
        // in the fixture's insertion order, so exercise the actual first source.
        var offered = engine.State.PendingChoice!.CardId!;
        return new(engine, offered, target.Id, followup ? offered == ruya.Id ? second : ruya.Id : null);
    }

    private static ReactionCaptureFixture ReactionCaptureForest(int seat)
    {
        var state = ReactionCaptureTable(seat, Phase.BattleSelection);
        // Forest Cover's actual printed Scout performers, actual Follower and actual
        // Gold sources; no client-authored payment or legality decisions.
        var first = SupportFixtureCard(state, 3636, seat, Zone.Battlefield, 0);
        var performer = SupportFixtureCard(state, 3636, seat, Zone.Battlefield, 0);
        SupportFixtureCard(state, 2060, 1 - seat, Zone.Battlefield, 0);
        var follower = SupportFixtureCard(state, 9147, seat, Zone.Hand);
        var forest = SupportFixtureCard(state, 10372, seat, Zone.Hand);
        for (var i = 0; i < 3; i++) SupportFixtureCard(state, 4840, seat, Zone.Home);
        var engine = ReactionCaptureEngine(state);
        ReactionCaptureAccept(engine, new("selectBattle", seat, engine.Version, Province: 0));
        SupportRequire(engine.State.PendingChoice?.Type == "engageReaction", "Forest Cover's Engage window did not open");
        return new(engine, forest.Id, follower.Id, first.Id, performer.Id);
    }

    private static ReactionCaptureFixture ReactionCaptureOrdered(int seat)
    {
        var state = ReactionCaptureTable(seat);
        SupportFixtureCard(state, 3636, seat, Zone.Battlefield, 0);
        SupportFixtureCard(state, 2060, 1 - seat, Zone.Battlefield, 0);
        var own = SupportFixtureCard(state, 9930, seat, Zone.Hand);
        var other = SupportFixtureCard(state, 9930, 1 - seat, Zone.Hand);
        var engine = ReactionCaptureEngine(state);
        ReactionCaptureAccept(engine, new("pass", seat, engine.Version));
        ReactionCaptureAccept(engine, new("pass", 1 - seat, engine.Version));
        SupportRequire(engine.State.PendingChoice?.Type == "beforeBattleOrder", "Two Steady Resolve items did not require actual active-player ordering");
        return new(engine, own.Id, other.Id);
    }

    private T ReactionControl<T>(string name) where T : Control => _root.FindChildren("*", "", true, false).OfType<T>()
        .Single(c => c.Name.ToString() == name && c.IsVisibleInTree());

    private Button ReactionOption(string source, string ability, string? performer = null) => StabilizationButtons(_inspector)
        .Single(b => b.IsVisibleInTree() && !b.Disabled && b.HasMeta("reaction_card") && b.GetMeta("reaction_card").AsString() == source &&
            b.GetMeta("reaction_ability").AsString() == ability && (performer is null || b.GetMeta("reaction_performer").AsString() == performer));

    private async Task PrepareReactionCapture(ReactionCaptureFixture fixture, int seat, bool input = true)
    {
        await LoadSupportFixture(fixture.Engine, seat);
        await WaitReactionCapture();
        FocusBattle(); await EventFrames(8);
        if (input && !GetWindow().HasFocus()) GD.Print("REACTION_FLOW_FOCUS_REQUIRED: activate the game window for native input checks.");
        for (var attempt = 0; input && attempt < 1800 && !GetWindow().HasFocus(); attempt++)
        {
            if (attempt % 60 == 0) GetWindow().GrabFocus();
            await EventFrames(1);
        }
        SupportRequire(!input || GetWindow().HasFocus(), "Native reaction window did not receive focus");
        SupportRequire(_selected is null && _chosen is null, "Fixture unexpectedly preselected a source or action");
    }

    private async Task WaitReactionCapture()
    {
        await EventFrames(5);
        for (var i = 0; i < 900 && (PresentationBusy || PlayShowcaseBusy || _session.Busy); i++) await EventFrames(1);
        SupportRequire(!PresentationBusy && !PlayShowcaseBusy && !_session.Busy && !_playHeld, "Reaction presentation did not continue by itself");
        await EventFrames(6);
    }

    /// <summary>
    /// A bot that plays many Reactions fills the sidebar's header with one line per announced or queued Reaction. The header scrolls, and the buttons below it stay on the panel where they can be pressed.
    /// Stuffs the header with far more lines than fit and checks the panel did not grow, the buttons below are still inside it, and the header's scroll bar appears and moves.
    /// </summary>
    private async Task CheckSidebarOverflow()
    {
        var panel = _inspectorPanel.GetGlobalRect();
        for (var i = 1; i <= 60; i++) _sidebarHeader.AddChild(Palette.Wrapped($"Announced: Mantis Reaction number {i} with a long description that wraps onto a second line of the sidebar", 15, Palette.PaperDim));
        await EventFrames(6);
        var scroll = (ScrollContainer)_sidebarHeader.GetParent();
        SupportRequire(_inspectorPanel.GetGlobalRect().Size.Y <= panel.Size.Y + 1, "A long list of Reactions grew the sidebar past its panel");
        var bar = scroll.GetVScrollBar();
        SupportRequire(bar.Visible && bar.MaxValue > bar.Page + 50, "The sidebar header has no scroll bar for a long list of Reactions");
        foreach (var button in StabilizationButtons(_inspectorPanel).Where(b => b.IsVisibleInTree() && b.Text.Length > 0 && !_sidebarHeader.IsAncestorOf(b)))
            SupportRequire(_inspectorPanel.GetGlobalRect().Encloses(button.GetGlobalRect()), "A sidebar button is pushed off the panel by the long list: " + button.Text);
        scroll.ScrollVertical = (int)bar.MaxValue; await EventFrames(3);
        SupportRequire(scroll.ScrollVertical > 0, "The sidebar header does not scroll");
        scroll.ScrollVertical = 0;
        for (var i = _sidebarHeader.GetChildCount() - 1; i >= _sidebarHeader.GetChildCount() - 60; i--) _sidebarHeader.GetChild(i).QueueFree();
        await EventFrames(3);
    }

    private void CheckReactionCaptureLayout()
    {
        var header = ReactionControl<Label>("ReactionDecisionHeader");
        var context = ReactionControl<Label>("ReactionContext");
        var status = ReactionControl<Label>("ReactionStatus");
        foreach (var control in new Control[] { header, context, status })
        {
            SupportRequire(control.Size.X > 100 && control.Size.Y > 12, "Reaction context has no readable layout area: " + control.Name + " " + control.Size);
            SupportRequire(_sidebarHeader.GetGlobalRect().Encloses(control.GetGlobalRect()), "Reaction context extends behind the scrolling body: " + control.Name);
            SupportRequire(GetViewport().GetVisibleRect().Encloses(control.GetGlobalRect()), "Reaction context leaves the supported viewport: " + control.Name);
            SupportRequire(!_tableViewport.GetGlobalRect().Intersects(control.GetGlobalRect()) && !_hand.GetGlobalRect().Intersects(control.GetGlobalRect()), "Reaction context overlaps the table or hand");
        }
        SupportRequire(!StabilizationButtons(_root).Any(b => b.IsVisibleInTree() && b.Text == "Pass reaction"), "Ambiguous Pass reaction remains visible");
        if (View.PriorityPlayerId == View.ViewerId && _chosen is null)
        {
            var decline = ReactionControl<Button>("ReactionDecline");
            SupportRequire(!decline.Disabled && _inspectorPanel.GetGlobalRect().Encloses(decline.GetGlobalRect()), "Explicit continuation is clipped or disabled");
            SupportRequire(!string.IsNullOrWhiteSpace(decline.TooltipText), "Continuation has no consequence");
        }
    }

    private void ReactionCaptureMouse(Vector2 point, bool doubleClick = false)
    {
        PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true, DoubleClick = doubleClick });
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false });
    }

    private void ReactionCaptureKey(Key key, bool pressed, bool echo = false) =>
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = pressed, Echo = echo }, true);

    private async Task ReactionCaptureFocus()
    {
        GetWindow().GrabFocus(); await EventFrames(3);
        for (var attempt = 0; attempt < 1800 && !GetWindow().HasFocus(); attempt++)
        {
            if (attempt % 60 == 0) GetWindow().GrabFocus();
            await EventFrames(1);
        }
        SupportRequire(GetWindow().HasFocus(), "Native reaction gesture has no foreground window focus");
    }

    private async Task ReactionNativeClick(Control control)
    {
        // A native capture sequence can lose foreground focus between gestures.
        // Restore real OS focus; do not bypass the game's unfocused-input guard.
        await ReactionCaptureFocus(); await SupportClick(control);
    }

    private async Task CheckDisconnectedReactionCapture()
    {
        var before = View.Version; var pending = View.PendingChoice;
        var socketField = typeof(MatchSession).GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var disconnected = new ClientWebSocket();
        try
        {
            // Fixture-only disconnected transport: retain the last public snapshot,
            // exactly as the game does after a live connection is interrupted.
            socketField.SetValue(_session, disconnected); Render(); await EventFrames(8);
            SupportRequire(_session.Online && !_session.Connected && View.Version == before && View.PendingChoice == pending,
                "Disconnect simulation changed the authoritative response opportunity");
            SupportRequire(ReactionControl<Label>("ReactionContext").Text.Length > 0 &&
                StabilizationButtons(_root).Any(b => b.IsVisibleInTree() && !b.Disabled && b.Text == "Reconnect"),
                "Disconnected reaction lost its context or its recovery control");
            SupportRequire(StabilizationButtons(_root).Where(b => b.IsVisibleInTree() && b.HasMeta("reaction_ability")).All(b => b.Disabled) &&
                !StabilizationButtons(_root).Any(b => b.IsVisibleInTree() && b.Name == "ReactionDecline" && !b.Disabled),
                "Disconnected player can still play or decline a reaction");
            await SupportCapture("-disconnected");
        }
        finally
        {
            socketField.SetValue(_session, null); disconnected.Dispose(); Render(); GetWindow().GrabFocus(); await EventFrames(8);
        }
        for (var attempt = 0; attempt < 1800 && !GetWindow().HasFocus(); attempt++)
        {
            if (attempt % 60 == 0) GetWindow().GrabFocus();
            await EventFrames(1);
        }
        SupportRequire(GetWindow().HasFocus(), "Disconnected fixture restoration did not return native window focus");
        SupportRequire(View.Version == before && View.PendingChoice == pending, "Restoring the fixture advanced the reaction");
    }

    private async Task FinishReactionCaptureWindows()
    {
        // A second Ruya may react to the first Reaction's bow in a new window,
        // then still be offered in the restored original window. Each is a real
        // choice: advance only with a fresh native gesture for that opportunity.
        for (var i = 0; i < 8 && View.PendingChoice is not null; i++)
        {
            if (View.ViewerId != View.PriorityPlayerId) await SwitchReactionCaptureSeat();
            CheckReactionCaptureLayout(); var before = View.Version;
            await ReactionNativeClick(ReactionControl<Button>("ReactionDecline")); await WaitReactionCapture();
            SupportRequire(View.Version == before + 1, "An explicit continuation submitted more than once");
        }
        SupportRequire(View.PendingChoice is null, "Explicit continuations did not finish the engine's remaining reaction windows");
    }

    // Issue #789: hovering a sidebar option marks the table copy it would act through, and hovering that copy lights the option.
    private async Task CheckReactionTableLink(ReactionCaptureFixture fixture)
    {
        // One option per performing Scout is enough: the engine may offer several (for example, the Follower from hand or from the discard pile) for each.
        var options = StabilizationButtons(_inspector).Where(b => b.IsVisibleInTree() && b.HasMeta("reaction_performer") && b.GetMeta("reaction_performer").AsString().Length > 0)
            .GroupBy(b => b.GetMeta("reaction_performer").AsString()).Select(g => g.First()).ToArray();
        SupportRequire(options.Length == 2, $"Forest Cover does not list both performing Scouts as sidebar options (found {options.Length})");
        async Task Point(Vector2 p) { PushCaptureMouse(new InputEventMouseMotion { Position = p, GlobalPosition = p }); await EventFrames(6); }
        foreach (var option in options)
        {
            var performer = option.GetMeta("reaction_performer").AsString();
            for (Node? parent = option.GetParent(); parent is not null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(option);
            await EventFrames(5);
            await Point(option.GetGlobalTransformWithCanvas() * (option.Size / 2));
            // An option may also name its target or source card; the point is that the OTHER identical Scout is never marked.
            var otherScout = options.Select(o => o.GetMeta("reaction_performer").AsString()).Single(id => id != performer);
            SupportRequire(_tiles.Where(t => t.Card.Id == performer).All(t => t.ForcedHover) && _tiles.Where(t => t.Card.Id == otherScout).All(t => !t.ForcedHover),
                "Hovering a Forest Cover option did not mark its performing Scout, or marked the other identical Scout, on the table");
            await Point(new Vector2(4, 4));
            SupportRequire(_tiles.All(t => !t.ForcedHover), "Leaving a Forest Cover option left a table card marked");
            var tile = _tiles.Single(t => t.Card.Id == performer);
            await Point(ScreenCenter(tile)); await EventFrames(6);
            SupportRequire(option.Modulate != Colors.White, "Hovering the performing Scout on the table did not light its option");
            await Point(new Vector2(4, 4));
            SupportRequire(option.Modulate == Colors.White, "Leaving the performing Scout left its option lit");
        }
    }

    private async Task SwitchReactionCaptureSeat()
    {
        _session.SwitchPracticeSeat(); await EventFrames(16);
        FocusBattle(); await EventFrames(8);
    }

    private async Task DraftRuya(ReactionCaptureFixture fixture)
    {
        var version = View.Version;
        await ReactionNativeClick(ReactionOption(fixture.Source, "ruya-bow"));
        SupportRequire(_chosen?.Command is { AbilityId: "ruya-bow", TargetId: null } && View.Version == version, "Reaction selection submitted or chose a target implicitly");
        var commit = ReactionControl<Button>("CommitAction");
        SupportRequire(commit.Disabled && commit.Text == "Play Reaction", "Incomplete reaction may be committed or its commit is not the stateful Play Reaction button");
        var target = _tiles.Single(t => t.Card.Id == fixture.Target);
        SupportRequire(target.LegalTarget, "The actual legal target is not highlighted");
        await ReactionNativeClick(target);
        SupportRequire(_chosen?.Command?.TargetId == fixture.Target && View.Version == version && !ReactionControl<Button>("CommitAction").Disabled,
            "Native card click did not retain a target draft without mutating the game");
    }

    private async Task CaptureReactionFlow()
    {
        try
        {
            var seat = _captureMode.Contains("seat1") ? 1 : 0;
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); _preferences.PausedAnimation = false; ApplyPreferences(false);
            var layout = _captureMode.Contains("layout");
            var forestReview = _captureMode.Contains("review") && _captureMode.Contains("forest");
            var fixture = forestReview ? ReactionCaptureForest(seat) : ReactionCaptureBow(seat); await PrepareReactionCapture(fixture, seat, input: !layout);
            CheckReactionCaptureLayout(); await SupportCapture("-offer");
            if (layout)
            {
                await CheckSidebarOverflow();
                GD.Print("REACTION_FLOW_LAYOUT_PASS: rendered public context and decision bounds; seat=" + seat + "; reduced=" + _preferences.ReducedMotion + "; input not exercised.");
                GetTree().Quit(); return;
            }
            if (forestReview) { GD.Print("REACTION_FLOW_REVIEW_READY: real Forest Cover offered by two Scouts; choose a performer, the Follower target, then bow holdings to pay 2 Gold."); return; }
            if (_captureMode.Contains("review")) { GD.Print("REACTION_FLOW_REVIEW_READY: real Horibe action resolved; Ruya may respond without inspecting her source first."); return; }
            var before = View.Version;
            var pending = View.PendingChoice;
            var stale = Actions.Single(a => a.Enabled && a.CardId == fixture.Source && a.Command?.AbilityId == "ruya-bow").Command! with { ExpectedVersion = before - 1 };
            await Submit(stale); await EventFrames(3);
            SupportRequire(View.Version == before && View.PendingChoice == pending &&
                !_root.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.IsVisibleInTree() && l.Name == "ReactionReceipt"),
                "A stale command advanced the window or invented a played-reaction receipt");
            await CheckDisconnectedReactionCapture();
            await DraftRuya(fixture); await SupportCapture("-target");
            await ReactionCaptureFocus(); ReactionCaptureKey(Key.Escape, true); ReactionCaptureKey(Key.Escape, false); await EventFrames(8);
            SupportRequire(View.Version == before && _chosen is null && View.PendingChoice?.Type == "bowReaction" && !MenuVisible,
                "Escape changed the game, declined the response, or opened a menu instead of cancelling the uncommitted draft");
            await SupportCapture("-cancelled"); await DraftRuya(fixture);
            await ReactionNativeClick(ReactionControl<Button>("CommitAction")); await WaitReactionCapture();
            SupportRequire(View.Version == before + 1 && fixture.Engine.State.Cards[fixture.Target].Bowed, "Reaction did not bow its selected target exactly once");
            SupportRequire(View.PendingChoice is null && !Actions.Any(a => a.Type == "declineReaction") && !_playHeld,
                "Engine-closed reaction requires a dismissal or another pass");
            SupportRequire(View.Events.Any(e => e.Type == "reactionClosed"), "Automatic window closure was not recorded by the engine");
            var receipt = ReactionControl<Label>("ReactionReceipt").Text;
            SupportRequire(receipt.Contains("Reaction played", StringComparison.Ordinal) && receipt.Contains("Tamori Ruya", StringComparison.Ordinal) &&
                !receipt.Contains("resolved", StringComparison.OrdinalIgnoreCase), "Accepted reaction has no permitted played receipt or claims every nested effect has resolved");
            await SupportCapture("-auto-closed");

            // Issue #962: the reacting card is clicked on the table; the click prepares the reaction and the Play Reaction press commits it.
            fixture = ReactionCaptureBow(seat); await PrepareReactionCapture(fixture, seat);
            var versionBeforeClick = View.Version; var sourceTile = _tiles.Single(t => t.Card.Id == fixture.Source);
            SupportRequire(sourceTile.LegalTarget, "The card holding the reaction is not highlighted on the table");
            await ReactionNativeClick(sourceTile);
            SupportRequire(_chosen?.Command is { AbilityId: "ruya-bow", TargetId: null } && View.Version == versionBeforeClick, "Clicking the reacting card did not prepare its reaction (or it submitted at once)");
            SupportRequire(ReactionControl<Button>("CommitAction") is { Disabled: true, Text: "Play Reaction" }, "The prepared reaction may be committed before a target is chosen");
            await ReactionNativeClick(_tiles.Single(t => t.Card.Id == fixture.Target));
            SupportRequire(_chosen?.Command?.TargetId == fixture.Target && !ReactionControl<Button>("CommitAction").Disabled && View.Version == versionBeforeClick, "Clicking the target did not complete the prepared reaction");

            // One gesture must never activate the next, still-meaningful response.
            fixture = ReactionCaptureBow(seat, followup: true); await PrepareReactionCapture(fixture, seat); await DraftRuya(fixture);
            before = View.Version; var commitPoint = ReactionControl<Button>("CommitAction").GetGlobalRect().GetCenter();
            await ReactionCaptureFocus(); ReactionCaptureMouse(commitPoint); ReactionCaptureMouse(commitPoint, doubleClick: true); await WaitReactionCapture();
            SupportRequire(View.Version == before + 1 && View.PendingChoice?.Type == "bowReaction" && Actions.Any(a => a.Enabled && a.CardId == fixture.SecondSource),
                "A rapid double-click submitted twice or consumed the remaining reaction opportunity");
            CheckReactionCaptureLayout(); await SupportCapture("-double-click-guard");
            await ReactionNativeClick(ReactionControl<Button>("ReactionDecline")); await WaitReactionCapture();
            SupportRequire(View.Version == before + 2, "A fresh deliberate continuation did not submit exactly once");
            await FinishReactionCaptureWindows();

            fixture = ReactionCaptureBow(seat, followup: true); await PrepareReactionCapture(fixture, seat); await DraftRuya(fixture);
            before = View.Version; await ReactionCaptureFocus(); ReactionControl<Button>("CommitAction").GrabFocus();
            // Native Godot buttons activate on release. Holding and repeating the
            // confirmation key must not turn that gesture into several commands.
            ReactionCaptureKey(Key.Enter, true); ReactionCaptureKey(Key.Enter, true, echo: true); await EventFrames(8);
            SupportRequire(View.Version == before, "A held confirmation key committed before its release");
            ReactionCaptureKey(Key.Enter, false); await WaitReactionCapture();
            SupportRequire(View.Version == before + 1 && View.PendingChoice?.Type == "bowReaction", "Keyboard commit did not leave the real follow-up response");
            await ReactionCaptureFocus(); ReactionControl<Button>("ReactionDecline").GrabFocus(); ReactionCaptureKey(Key.Enter, true, echo: true); await EventFrames(8);
            SupportRequire(View.Version == before + 1 && View.PendingChoice?.Type == "bowReaction", "Holding Enter consumed the next reaction opportunity");
            await SupportCapture("-held-key-guard");
            await ReactionCaptureFocus(); ReactionControl<Button>("ReactionDecline").GrabFocus();
            ReactionCaptureKey(Key.Enter, true); ReactionCaptureKey(Key.Enter, false); await WaitReactionCapture();
            SupportRequire(View.Version == before + 2, "A new keyboard gesture could not continue after release exactly once");
            await FinishReactionCaptureWindows();

            fixture = ReactionCaptureBow(seat, nested: true); await PrepareReactionCapture(fixture, seat);
            await SwitchReactionCaptureSeat(); CheckReactionCaptureLayout(); before = View.Version;
            SupportRequire(Actions.All(a => a.Type is "wait" or "concede") && !StabilizationButtons(_root).Any(b => b.IsVisibleInTree() && b.Name.ToString().StartsWith("ReactionOption")),
                "Waiting seat received another player's private reaction controls");
            var publicCard = _tiles.Single(t => t.Card.Id == fixture.Target); await ReactionNativeClick(publicCard);
            SupportRequire(View.Version == before && _selected?.Id == fixture.Target, "Waiting-player card inspection submitted or was blocked");
            await SupportCapture("-waiting");
            bool WaitingPreviewReady() => GetWindow().HasFocus() && _tableTooltip is not null && CardTile.LastPreviewCardId == fixture.Target &&
                GetViewport().GetVisibleRect().Encloses(_tableTooltip.GetGlobalRect()) && View.Version == before;
            var publicPoint = Vector2.Zero;
            for (var attempt = 0; attempt < 5 && !WaitingPreviewReady(); attempt++)
            {
                await ReactionCaptureFocus(); publicCard = _tiles.Single(t => t.Card.Id == fixture.Target); publicPoint = ScreenCenter(publicCard);
                HideTableTooltip(); PushCaptureMouse(new InputEventMouseMotion { Position = publicPoint, GlobalPosition = publicPoint });
                for (var frame = 0; frame < 60 && GetWindow().HasFocus() && !WaitingPreviewReady(); frame++) await EventFrames(1);
            }
            SupportRequire(WaitingPreviewReady(),
                $"The waiting player cannot read a bounded full-card preview without changing the match (focus={GetWindow().HasFocus()}, blocked={CameraBlocked}, point={publicPoint}, hovered={(TableHovered as CardTile)?.Card.Id}, tooltip={_tableTooltip is not null}, preview={CardTile.LastPreviewCardId}, time={_tableHoverTime})");
            await SaveShownCapture("-waiting-inspection"); HideTableTooltip();
            await SwitchReactionCaptureSeat(); await DraftRuya(fixture);
            await ReactionNativeClick(ReactionControl<Button>("CommitAction")); await WaitReactionCapture();
            SupportRequire(fixture.Engine.State.SuspendedWindows.Count > 0 && View.ReactionPresentation is { IsNested: true, ParentContext.Length: > 0 }, "Nested response lost its public parent context");
            await SupportCapture("-nested-waiting");
            for (var i = 0; i < 6 && View.PendingChoice is not null; i++)
            {
                if (View.ViewerId != View.PriorityPlayerId)
                {
                    await SwitchReactionCaptureSeat();
                    SupportRequire(!_root.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.IsVisibleInTree() && l.Name == "ReactionReceipt"),
                        "Switching seats retained the other player's reaction receipt");
                }
                CheckReactionCaptureLayout(); if (i == 0) await SupportCapture("-nested-response");
                await ReactionNativeClick(ReactionControl<Button>("ReactionDecline")); await WaitReactionCapture();
            }
            SupportRequire(View.PendingChoice is null && fixture.Engine.State.SuspendedWindows.Count == 0 && fixture.Engine.State.Cards[fixture.Target].Bowed,
                "Nested responses did not restore and finish the original action");

            fixture = ReactionCaptureForest(seat); await PrepareReactionCapture(fixture, seat); CheckReactionCaptureLayout(); before = View.Version;
            await CheckReactionTableLink(fixture);
            SupportRequire(Actions.Where(a => a.Enabled && a.Command?.AbilityId == "wl-forest-cover-attach").Select(a => a.Command!.PerformerId).Distinct().Count() == 2,
                "The real Forest Cover fixture does not offer both performing Scouts");
            await ReactionNativeClick(ReactionOption(fixture.Source, "wl-forest-cover-attach", fixture.SecondSource));
            SupportRequire(_chosen?.Command?.PerformerId == fixture.SecondSource && _tiles.Single(t => t.Card.Id == fixture.Performer).LegalTarget,
                "The other engine-offered performer is not available directly on the table");
            await ReactionNativeClick(_tiles.Single(t => t.Card.Id == fixture.Performer));
            SupportRequire(_chosen is { Command: { TargetId: null } performerDraft } && performerDraft.PerformerId == fixture.Performer && View.Version == before,
                "Choosing the second real performer mutated the game or implicitly selected the Follower");
            await ReactionNativeClick(_tiles.Single(t => t.Card.Id == fixture.Target));
            var quote = QuoteFor(_chosen!); SupportRequire(quote is { Cost: 2 } && _chosen?.Command?.TargetId == fixture.Target,
                "Follower target did not obtain its authoritative Gold quote");
            if (quote is { Payments.Length: > 0 }) { GoldClickHoldings(quote.Payments[0].SourceIds); await EventFrames(3); } // the player bows holdings to pay (#731)
            SupportRequire(!ReactionControl<Button>("CommitAction").Disabled && ReactionControl<Button>("CommitAction").Text.StartsWith("Play Reaction", StringComparison.Ordinal), "Prepared paid reaction lacks its single Play Reaction commit");
            await SupportCapture("-performer-cost"); await ReactionNativeClick(ReactionControl<Button>("CommitAction")); await WaitReactionCapture();
            SupportRequire(View.Version == before + 1 && fixture.Engine.State.Cards[fixture.Target] is { Zone: Zone.Attached } follower && follower.AttachedTo == fixture.Performer &&
                fixture.Engine.State.Cards[fixture.Source].Zone == Zone.FateDiscard && fixture.Engine.State.ExtraEndPhaseDraws[seat] == 1,
                "Forest Cover did not pay and attach to the selected actual performer in one submission");
            await SupportCapture("-attachment-result");

            fixture = ReactionCaptureOrdered(seat); await PrepareReactionCapture(fixture, seat); before = View.Version;
            var order = Actions.First(a => a.Enabled && a.Type == "beforeBattleOrder" && a.Command!.TargetId == "reaction:" + fixture.Source);
            var orderIndex = Actions.Where(a => a.Type == "beforeBattleOrder").ToList().IndexOf(order);
            await ReactionNativeClick(StabilizationButtons(_inspector).Where(b => b.Text == order.Label).ElementAt(orderIndex));
            SupportRequire(View.Version == before && _decisionSelection == order, "Ordering the before-resolution item submitted before confirmation");
            await ReactionNativeClick(DecisionButton("Confirm choice")); await WaitReactionCapture();
            SupportRequire(View.PendingChoice is { Type: "beforeBattleReaction" } && View.PendingChoice.CardId == fixture.Source,
                "The selected ordered reaction did not open for its actual owner");
            CheckReactionCaptureLayout(); SupportRequire(ReactionControl<Button>("ReactionDecline").Text.Contains("Steady Resolve"), "Ordered reaction decline does not name the card being declined");
            await SupportCapture("-ordered-reaction"); await ReactionNativeClick(ReactionControl<Button>("ReactionDecline")); await WaitReactionCapture();
            SupportRequire(View.PendingChoice is { Type: "beforeBattleReaction" } && View.PendingChoice.CardId == fixture.Target,
                "Declining one ordered item incorrectly skipped the opponent's different item");
            if (View.ViewerId != View.PriorityPlayerId) await SwitchReactionCaptureSeat();
            CheckReactionCaptureLayout(); await ReactionNativeClick(ReactionControl<Button>("ReactionDecline")); await WaitReactionCapture();
            SupportRequire(View.PendingChoice is null && View.Events.Any(e => e.Type == "battleResolved"), "The ordered reactions did not finish into actual battle resolution");
            await SupportCapture("-ordered-result");
            GD.Print("REACTION_FLOW_PASS: published reactions; no source-inspection prerequisite; native performers, target and Gold review; stale-command rejection; disconnected context/recovery; Escape draft cancellation; exactly one commit; accepted played receipt and seat isolation; automatic engine closure; preserved optional responses; mouse double-click and held-key safety; both public waiting and nested context; ordered named decline; seat=" + seat + "; reduced=" + _preferences.ReducedMotion);
            GetTree().Quit();
        }
        catch (Exception error) { await SupportCapture("-failure"); GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
