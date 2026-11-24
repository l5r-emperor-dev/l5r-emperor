using Godot;
using L5R.Core;
using System.Reflection;
using System.Text.Json;

namespace L5R.Client;

public partial class Main
{
    private bool _shownCaptureStarted;
    private MatchEngine? _shownFixture;
    private bool _shownReviewBusy;
    private Button[] _shownReviewButtons = [];
    private async Task RunShownReview(Func<Task> action)
    {
        if (_shownReviewBusy) return;
        _shownReviewBusy = true; foreach (var button in _shownReviewButtons) button.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("This demo has changed. Use Restart demo to prepare it again."); GD.PushWarning(e.Message); }
        finally
        {
            _shownReviewBusy = false;
            foreach (var button in _shownReviewButtons) if (IsInstanceValid(button)) button.Disabled = false;
        }
    }
    private void ProcessShownCapture(double delta)
    {
        _captureClock += delta;
        if (_shownCaptureStarted || _captureClock < 1.5) return;
        _shownCaptureStarted = true; _ = CaptureShownHistory();
    }
    private static MatchEngine CreateShownFixture()
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        SupportFixtureCard(state, 10292, 0, Zone.Home);
        var kahoku = CardCatalog.Cards.Values.Single(d => d.Abilities?.Contains("kahoku-exp2-look") == true);
        SupportFixtureCard(state, kahoku.Id, 0, Zone.Home);
        var region = SupportFixtureCard(state, 9865, 0, Zone.DynastyDeck); state.Players[0].DynastyDeck.Insert(0, region.Id);
        foreach (var definition in CardCatalog.Cards.Values.Where(d => !d.ScenarioOnly && d.Type == CardType.Strategy && (d.Keywords ?? []).Contains("Political")).Take(3))
        { var card = SupportFixtureCard(state, definition.Id, 0, Zone.FateDeck); state.Players[0].FateDeck.Insert(0, card.Id); }
        return (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
    }
    private async Task ShownStep(string type, string? ability = null, string? choice = null)
    {
        var actions = _shownFixture!.QueryActions(_shownFixture.State.PriorityPlayerId);
        var option = actions.First(a => a.Enabled && a.Type == type && (ability is null || a.Command?.AbilityId == ability) && (choice is null || a.Command?.Choice == choice));
        await _session.Submit(option.Command!); await WaitDecisionPresentation();
    }
    private async Task FirstShownReveal()
    {
        await ShownStep("useAbility", "shizuka-search"); await ShownStep("chooseSearchDeck", choice: "Dynasty");
        await ShownStep("selectSearchCard", choice: "oracle-l5r-9865"); await ShownStep("chooseSearchProvince");
    }
    private async Task NextShownReveal()
    {
        if (_shownFixture!.State.ShownCards.Count >= 2) { SetNotice("Both prepared reveals are complete. Restart the demo to replay them."); return; }
        await ShownStep("pass"); await ShownStep("useAbility", "kahoku-exp2-look");
        var answer = _shownFixture.QueryActions(0).First(a => a.Enabled && a.Type == "arrangeTopCards" && a.Command!.CardIds!.Any(t => t.EndsWith(":show", StringComparison.Ordinal)));
        await _session.Submit(answer.Command!); await WaitDecisionPresentation();
    }
    private async Task PrepareShownReview()
    {
        _shownFixture = CreateShownFixture(); await LoadSupportFixture(_shownFixture, 1); await FirstShownReveal();
        ToggleShownCards(true);
        if (_captureMode.Contains("review"))
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            var next = SidebarSkin.Button("Show another card", () => _ = RunShownReview(NextShownReveal));
            var restart = SidebarSkin.Button("Restart demo", () => _ = RunShownReview(PrepareShownReview));
            _shownReviewButtons = [next, restart];
            next.SizeFlagsHorizontal = restart.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            next.CustomMinimumSize = restart.CustomMinimumSize = new(0, 44); row.AddChild(next); row.AddChild(restart);
            // Demo controls occupy the existing top menu area, separate from the live history frame.
            if (_root.GetNodeOrNull<Control>("ShownReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
            row.Name = "ShownReviewControls"; Place(_root, row, 350, 114, 640, 50);
        }
        await EventFrames(12);
    }
    private async Task ShownHover(ShownCardEntry row)
    {
        _shownScroll.EnsureControlVisible(row); await EventFrames(6);
        var point = row.GetGlobalRect().GetCenter(); var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point }; motion.SetMeta(CaptureMouseTag, true); GetViewport().PushInput(motion, true);
        await EventFrames(35);
    }
    private async Task SaveShownCapture(string suffix)
    {
        await EventFrames(8);
        var path = Path.ChangeExtension(_capturePath, null) + suffix + ".png";
        SupportRequire(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "Could not save shown-card capture");
    }
    private async Task CaptureShownHistory()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareShownReview();
            if (_captureMode.Contains("review")) { GD.Print("SHOWN_REVIEW_READY: Shizuka reveal prepared; Show another card uses Kahoku."); return; }
            // Issue #1319: the reveal arrives as a held card that waits for a click; the history then keeps it.
            await WaitFor(() => ProceedButton is { Visible: true } && _shownBeatsStarted.SequenceEqual(["oracle-l5r-9865"]), "The first reveal was not held on screen");
            await SaveShownCapture("-held"); await PausedMouse(ProceedButton!.GetGlobalRect().GetCenter(), true);
            await WaitFor(() => !_playHeld && !PlayShowcaseBusy, "Proceed did not dismiss the first reveal");
            var version = _session.Snapshot!.View.Version;
            SupportRequire(OpponentShows.Length == 1 && _shownOpen, "Initial reveal missing");
            SupportRequire(_shownList.GetChildren().OfType<ShownCardEntry>().Single().Record.SourceName == "Ikoma Shizuka", "Source context missing");
            var row = _shownList.GetChildren().OfType<ShownCardEntry>().Single();
            await ShownHover(row); SupportRequire(_shownPreview is not null, "Native hover did not enlarge card");
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_shownPreview!.GetGlobalRect()), "Hover preview is clipped outside the window"); await SaveShownCapture("-hover");
            await SupportClick(row); SupportRequire(_shownPinned, "Click did not pin inspection");
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_shownPreview!.GetGlobalRect()), "Pinned inspection is clipped outside the window");
            await SupportClick(StabilizationButtons(_shownPreview!).Single(b => b.Text == "Close inspection"));
            await SupportClick(_shownToggle); SupportRequire(!_shownOpen, $"Minimize failed: panel={_shownPanel.GetGlobalRect()} min={_shownPanel.GetCombinedMinimumSize()} scroll={_shownScroll.Size}/{_shownScroll.GetCombinedMinimumSize()} toggle={_shownToggle.GetGlobalRect()} hover={GetViewport().GuiGetHoveredControl()?.GetPath()}");
            SupportRequire(_session.Snapshot.View.Version == version, "Inspection sent a game command");
            // PR #434: the recipient cannot miss an incoming show. Issue #1319: it arrives as a held card (not by forcing the history open), read once dismissed.
            await NextShownReveal(); await WaitFor(() => ProceedButton is { Visible: true } && _shownBeatsStarted.Count == 2, "An incoming show was not held on screen");
            SupportRequire(!_shownOpen && ShownUnread == 0 && OpponentShows.Length == 2, "An incoming show also forced the history open, or stayed unread");
            await SaveShownCapture("-new"); await PausedMouse(ProceedButton!.GetGlobalRect().GetCenter(), true);
            await WaitFor(() => !_playHeld && !PlayShowcaseBusy, "Proceed did not dismiss the incoming show");
            await SupportClick(_shownToggle); SupportRequire(_shownOpen && ShownUnread == 0 && OpponentShows.Length == 2, "Maximize lost history or unread state");
            row = _shownList.GetChildren().OfType<ShownCardEntry>().First(); row.GrabFocus(); await EventFrames(8);
            SupportRequire(_shownPreview is not null, "Keyboard focus did not enlarge card"); await SaveShownCapture("-history"); HideShownPreview();
            // The record survives moving the shown card out of its original location; the UI never follows a hidden instance.
            var snapshot = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(_session.Snapshot.View))!;
            SupportRequire(snapshot.Players[0].Hand.Length == 0 && snapshot.ShownCards!.Length == 2, "Opponent hand exposed or records lost in a detached snapshot");
            _session.SwitchPracticeSeat(); await EventFrames(15); SupportRequire(OpponentShows.Length == 0, "Own reveals were shown as the opponent's history");
            ToggleShownCards(true); await SaveShownCapture("-empty");
            _session.SwitchPracticeSeat(); await EventFrames(15); SupportRequire(OpponentShows.Length == 2 && ShownUnread == 0, "Seat switch lost history or read state");
            // Stress the append-only list using copies of the two real engine records. This is explicitly fixture-only history.
            var state = _shownFixture!.State; var original = state.ShownCards.ToArray();
            for (var i = 2; i < 14; i++) state.ShownCards.Add(original[i % 2] with { Sequence = i + 1, TurnNumber = i / 2 + 1, EventSequence = 100 + i });
            var longHistory = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
            await LoadSupportFixture(longHistory, 1); ToggleShownCards(true); await EventFrames(15);
            var last = _shownList.GetChildren().OfType<ShownCardEntry>().Last(); await ShownHover(last);
            SupportRequire(_shownList.GetChildren().OfType<ShownCardEntry>().Count() == 14 && _shownScroll.ScrollVertical > 0 && _shownPreview is not null, "Older history is unreachable"); await SaveShownCapture("-long-history");
            SupportRequire(_shownPanel.GetGlobalRect().End.X <= GetViewport().GetVisibleRect().End.X + 1, "History exceeds the window");
            await LoadSupportFixture(CreateShownFixture(), 1); ToggleShownCards(true); SupportRequire(OpponentShows.Length == 0 && ShownUnread == 0, "New match retained prior history");
            var decisionState = MatchEngine.CreateDecisionShowcase("fukui").State;
            var yumaru = SupportFixtureCard(decisionState, 5181, 0, Zone.Battlefield, 0);
            SupportFixtureCard(decisionState, 6481, 0, Zone.Attached, 0).AttachedTo = yumaru.Id;
            var decisionEngine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [decisionState])!;
            await LoadSupportFixture(decisionEngine, 0); ToggleShownCards(true);
            var move = decisionEngine.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "yumaru-move-home");
            await _session.Submit(move.Command! with { TargetId = move.TargetIds!.First() }); await WaitDecisionPresentation();
            SupportRequire(!_shownOpen && GenericDecisionActive && DecisionButton("Confirm choice").IsVisibleInTree(), "An incoming required decision stayed behind history");
            await CaptureShownBeats();
            GD.Print("SHOWN_HISTORY_PASS: native hover/click/keyboard, two real opponent reveals, minimized arrival, privacy, both seats, long history and new-match reset"); GetTree().Quit();
        }
        catch (Exception e) { await SaveShownCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
