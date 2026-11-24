using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _multiCaptureStarted, _multiReviewBusy;
    private MatchEngine _multiFixture = null!;
    private Button[] _multiReviewButtons = [];
    private string _multiSource = "", _multiEnemy = "";
    private void ProcessMultiCapture(double delta)
    {
        _captureClock += delta; if (_multiCaptureStarted || _captureClock < 1.5) return;
        _multiCaptureStarted = true; _ = CaptureMulti();
    }
    private async Task PrepareMultiReview(string mode = "bonds")
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); }
        var battle = mode is "yoshitada" or "honorable" or "reactions" or "fear" or "brothers";
        state.Phase = battle ? Phase.Battle : Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        state.CurrentBattleProvince = battle ? 0 : null; state.UnresolvedBattlefields = battle ? [0] : [];
        var zone = battle ? Zone.Battlefield : Zone.Home; int? province = battle ? 0 : null;
        var own = mode == "brothers" ? SupportFixtureCard(state, "training-samurai", 0, zone, province) : SupportFixtureCard(state, mode is "yoshitada" or "honorable" or "fear" ? 2179 : 3636, 0, zone, province);
        if (mode == "brothers") SupportFixtureCard(state, "training-samurai", 0, zone, province);
        var enemy = SupportFixtureCard(state, mode == "reactions" ? 223 : 2060, 1, zone, province); _multiEnemy = enemy.Id;
        var source = mode == "brothers" ? SupportFixtureCard(state, 1164, 0, Zone.Hand) : mode is "yoshitada" or "honorable" ? own : mode == "bonds" ? SupportFixtureCard(state, 1092, 0, Zone.Hand) :
            mode == "fear" ? SupportFixtureCard(state, 2493, 0, Zone.Hand) : SupportFixtureCard(state, "training-slots-source", 0, zone, province);
        _multiSource = source.Id;
        var ability = mode switch { "brothers" => "p2-brothers-in-battle", "bonds" => "bonds-of-coin-destroy", "yoshitada" or "honorable" => "yoshitada-bow", "fear" => "fear-me-move", "mixed" => "training-slots-mixed", "hand" => "training-slots-discard", "reactions" => "training-slots-two-personalities", _ => "training-slots-up-to-three" };
        if (mode == "yoshitada") enemy.Dishonorable = true;
        if (mode is "bonds" or "optional")
        {
            for (var i = 0; i < 4; i++) { var host = i < 2 ? own : enemy; SupportFixtureCard(state, 615, host.OwnerId, Zone.Attached).AttachedTo = host.Id; }
            SupportFixtureCard(state, "training-item", 0, Zone.Attached).AttachedTo = own.Id;
        }
        if (mode == "mixed") { SupportFixtureCard(state, 2867, 0, Zone.Home); SupportFixtureCard(state, 4664, 1, Zone.Home); }
        if (mode == "hand") { SupportFixtureCard(state, 1092, 0, Zone.Hand); SupportFixtureCard(state, 2493, 0, Zone.Hand); SupportFixtureCard(state, 690, 1, Zone.Hand); }
        if (mode == "reactions") SupportFixtureCard(state, "training-merchant-free-guard", 1, Zone.Battlefield, 0);
        _multiFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_multiFixture, 0);
        var action = Actions.FirstOrDefault(a => a.Enabled && a.CardId == source.Id && a.Command?.AbilityId == ability);
        SupportRequire(action is not null, "Fixture action not offered: " + ability + "; " + string.Join("; ", Actions.Where(a => a.CardId == source.Id).Select(a => a.Label + ": " + a.Reason)));
        Choose(action!); await EventFrames(20);
        if (_captureMode.Contains("review")) BuildMultiReviewControls();
    }
    private void BuildMultiReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("MultiReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "MultiReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 4);
        var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Bonds of Coin", "bonds"), ("Yoshitada", "yoshitada"), ("Honorable", "honorable"), ("Fear Me!", "fear"), ("Groups*", "mixed"), ("Optional*", "optional"), ("Hand*", "hand"), ("Reactions*", "reactions") })
        { var button = SidebarSkin.Button(label, () => _ = RunMultiReview(() => PrepareMultiReview(mode))); if (label.EndsWith('*')) button.TooltipText = "Training fixture for the generic engine contract; not a published card."; buttons.Add(button); }
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat())); _multiReviewButtons = buttons.ToArray();
        foreach (var b in buttons) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.CustomMinimumSize = new(0, 44); b.AddThemeFontSizeOverride("font_size", 13); row.AddChild(b); }
        Place(_root, row, 350, 114, 1160, 46); FitMultiReviewControls();
    }
    private void FitMultiReviewControls()
    { if (_root.GetNodeOrNull<Control>("MultiReviewControls") is { } row) row.Size = new(Math.Min(1160, _layoutWidth - SidebarWidth - 22 - 350 - 18), 46); }
    private async Task RunMultiReview(Func<Task> action)
    {
        if (_multiReviewBusy) return; _multiReviewBusy = true; foreach (var b in _multiReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("Restart a target scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _multiReviewBusy = false; foreach (var b in _multiReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private MultiTargetFace MultiFace(string slot, string id) => StabilizationButtons(_inspector).OfType<MultiTargetFace>().Single(b => b.Card.Id == id && b.GetMeta("slot").AsString() == slot);
    private async Task OpenMultiGroup(string slot)
    { if (!_multiExpanded.Contains(slot)) await SupportClick((Button)_inspector.FindChild("MultiGroup_" + slot, true, false)!); }
    private async Task ConfirmMulti()
    { await SupportClick(_paymentConfirm!); await WaitDecisionPresentation(); await EventFrames(25); }
    private async Task ClickMultiBoard(string id)
    {
        await ReactionCaptureFocus();
        var point = MultiTargetPoint(_tiles.Single(t => t.Card.Id == id));
        PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(5);
        GD.Print($"MULTI_TARGETS_BOARD_CLICK: {id} at {point}; focus={GetWindow().HasFocus()}, blocked={CameraBlocked}, hovered={(TableHovered as CardTile)?.Card.Id}, busy={PresentationBusy}");
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(12);
    }
    // Issue #1327: Running Battle (an enemy target plus an optional attachment) that two Scouts could perform. Who performs is asked first, on the table or in the list, and Back returns to it;
    // the engine must receive the Scout the player picked, not the first one offered.
    private async Task CaptureMultiPerformer()
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); }
        state.Phase = Phase.Battle; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null; state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0];
        var madoka = SupportFixtureCard(state, 5271, 0, Zone.Battlefield, 0); var yashiro = SupportFixtureCard(state, 8896, 0, Zone.Battlefield, 0, bowed: true);
        var enemy = SupportFixtureCard(state, 2060, 1, Zone.Battlefield, 0); var card = SupportFixtureCard(state, 9924, 0, Zone.Hand);
        _multiFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_multiFixture, 0);
        var offered = Actions.Where(a => a.Enabled && a.CardId == card.Id && a.Command?.AbilityId == "running-battle").Select(a => a.Command!.PerformerId).ToHashSet();
        SupportRequire(offered.SetEquals(new[] { madoka.Id, yashiro.Id }), "Running Battle fixture: both Scouts should be offered: " + string.Join(",", offered));
        Choose(Actions.First(a => a.Enabled && a.CardId == card.Id && a.Command?.AbilityId == "running-battle")); await EventFrames(20);
        bool Rows(params string[] ids) => ids.All(id => StabilizationButtons(_inspector).Any(b => b.HasMeta("performer_row") && b.GetMeta("performer_row").AsString() == id));
        SupportRequire(PerformerStepActive && !MultiTargetActive && Rows(madoka.Id, yashiro.Id), "The performer was not asked first: step=" + PerformerStepActive + " multi=" + MultiTargetActive);
        SupportRequire(_tiles.Where(t => t.Card.Id == yashiro.Id).All(t => t.LegalTarget), "Yashiro is not highlighted as a performer");
        await SupportCapture("-performer");
        await ClickMultiBoard(yashiro.Id);
        SupportRequire(MultiTargetActive && _chosen?.Command?.PerformerId == yashiro.Id, "Clicking Yashiro on the table did not make him the performer: " + _chosen?.Command?.PerformerId + " notice=" + _notice.Text);
        await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Name == "FlowBack"));
        SupportRequire(PerformerStepActive && !MultiTargetActive, "Back did not return to choosing the performer");
        await SupportClick(StabilizationButtons(_inspector).Single(b => b.HasMeta("performer_row") && b.GetMeta("performer_row").AsString() == yashiro.Id));
        SupportRequire(MultiTargetActive && _chosen?.Command?.PerformerId == yashiro.Id, "Choosing Yashiro in the list did not make him the performer");
        await SupportClick(MultiFace(MultiUpFront.First().Slot, enemy.Id)); await SupportCapture("-performer-targets"); await ConfirmMulti();
        var played = _multiFixture.ExportReplay().Commands.Last(c => c.AbilityId == "running-battle");
        SupportRequire(played.PerformerId == yashiro.Id && played.Selections?.Any(sel => sel.CardIds?.Contains(enemy.Id) == true) == true, "The engine did not receive Yashiro as the performer: " + played.PerformerId);
    }
    private async Task CaptureMulti()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            if (_captureMode.Contains("brothers"))
            {
                await PrepareMultiReview("brothers"); var brothers = MultiUpFront.Single(); var samurai = brothers.CardIds.Take(2).ToArray();
                foreach (var id in samurai) await SupportClick(MultiFace(brothers.Slot, id));
                SupportRequire(MultiReady && _paymentConfirm is { Disabled: false }, "Brothers in Battle: Confirm disabled with two Samurai chosen: " + _paymentConfirm?.Text);
                await ConfirmMulti(); SupportRequire(_multiFixture.State.Cards[samurai[0]].Zone == Zone.Battlefield && _multiFixture.State.P2.BrothersGroups.Count == 1, "Brothers in Battle did not resolve: groups=" + _multiFixture.State.P2.BrothersGroups.Count + " pending=" + View.PendingChoice?.Type + " notice=" + _notice.Text + " events=" + string.Join("|", _multiFixture.State.Events.TakeLast(4).Select(e => e.Type)));
                GD.Print("MULTI_TARGETS_PASS"); GetTree().Quit(); return;
            }
            await PrepareMultiReview();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal)) { GD.Print("MULTI_TARGETS_REVIEW_READY: published actions and clearly marked training fixtures."); return; }
            var slot = MultiUpFront.Single(); var ids = slot.CardIds; var version = View.Version;
            SupportRequire(ids.Length == 4 && _paymentConfirm!.Disabled && !MultiSelectedIds.Any(), "Required targets defaulted or fixture candidates wrong");
            await ClickMultiBoard(ids[2]); SupportRequire(MultiSelectedIds.SequenceEqual(new[] { ids[2] }), "Board attachment selection failed");
            await SupportClick(MultiFace(slot.Slot, ids[0])); await SupportClick(MultiFace(slot.Slot, ids[1]));
            SupportRequire(MultiFace(slot.Slot, ids[3]).Disabled && View.Version == version && IntentArrows().Count(a => a.Label is not null) == 3, "Max bound or arrow count wrong");
            SupportRequire(!StabilizationButtons(_inspector).Any(b => b.HasMeta("move") || b.Text is "‹" or "›"), "Redundant target arrows remain");
            MultiFace(slot.Slot, ids[2]).GrabFocus(); await LookKey(Key.Enter);
            await SupportClick(MultiFace(slot.Slot, ids[2]));
            SupportRequire(MultiSelectedIds.SequenceEqual(new[] { ids[0], ids[1], ids[2] }), "Deselect/reselect did not preserve chosen order");
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_paymentConfirm!.GetGlobalRect()), "Target confirmation is offscreen");
            await SupportCapture("-selected");
            await SupportClick(StabilizationButtons(_inspector).Single(b => b.HasMeta("inspect_card") && b.GetMeta("inspect_card").AsString() == ids[2]));
            SupportRequire(_multiInspection?.Visible == true, "Inspection did not open");
            _session.SwitchPracticeSeat(); await EventFrames(15); SupportRequire(!MultiTargetActive && _multiInspection is null && _multiCards.Count == 0, "Private draft survived seat change");
            await SupportCapture("-waiting");
            await PrepareMultiReview(); slot = MultiUpFront.Single(); ids = slot.CardIds;
            foreach (var id in ids.Take(3)) await SupportClick(MultiFace(slot.Slot, id));
            await ConfirmMulti(); SupportRequire(ids.Take(3).All(id => _multiFixture.State.Cards[id].Zone != Zone.Attached) && _multiFixture.State.Cards[ids[3]].Zone == Zone.Attached, "Engine destroyed the wrong set");
            foreach (var mode in new[] { "yoshitada", "honorable", "fear" })
            {
                await PrepareMultiReview(mode); slot = MultiUpFront.Single();
                SupportRequire(!StabilizationButtons(_inspector).Any(b => b.HasMeta("player")), "Conditional player choice appeared up front");
                await SupportClick(MultiFace(slot.Slot, _multiEnemy)); await SupportCapture("-" + mode);
                await ConfirmMulti();
                if (mode == "honorable") { SupportRequire(View.PendingChoice is null, "Honorable target prompted a follow-up"); continue; }
                SupportRequire(View.PendingChoice?.Type == "effectChoice" && GenericDecisionActive && DecisionButton("Confirm choice").Disabled, "Real follow-up not presented explicitly");
                SupportRequire(mode == "fear" ? _multiFixture.State.Cards[_multiEnemy].Zone == Zone.Home : _multiFixture.State.Cards[_multiEnemy].Bowed, "First effect did not resolve before follow-up");
                var honor = _multiFixture.State.Players[0].Honor;
                var choice = Actions.Single(a => a.Command?.Choice == "self-gain");
                await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text.Contains(choice.Label)));
                await SupportClick(DecisionButton("Confirm choice")); await WaitDecisionPresentation(); await EventFrames(20);
                SupportRequire(_multiFixture.State.Players[0].Honor == honor + 2 && View.PendingChoice is null, "Honor follow-up result wrong");
            }
            await PrepareMultiReview("mixed"); var selections = MultiUpFront.ToArray();
            // Issue #969: a card that only another open group accepts switches to that group when it is clicked on the table, and a second click removes it again.
            var cardSlots = selections.Where(s => s.Kind == TargetSlotKinds.Card).ToArray();
            var firstGroup = cardSlots[0]; var secondGroup = cardSlots[1];
            var onlySecond = secondGroup.CardIds.Except(firstGroup.CardIds).First();
            SupportRequire(_multiActiveSlot == firstGroup.Slot && _tiles.Where(t => t.Card.Id == onlySecond).All(t => t.LegalTarget), "Every open group's candidates should be highlighted on the table");
            SelectCard(FindCard(onlySecond)!); await EventFrames(4);
            SupportRequire(_multiCards[secondGroup.Slot].Contains(onlySecond), "Clicking a card for another group did not select it there"); // issue #1004: a full group then hands over to the next one, so the open group is not asserted
            SelectCard(FindCard(onlySecond)!); await EventFrames(4);
            SupportRequire(!_multiCards[secondGroup.Slot].Contains(onlySecond), "Clicking the chosen card again did not remove it");
            foreach (var s in selections.Where(s => s.Kind == TargetSlotKinds.Card)) { await OpenMultiGroup(s.Slot); await SupportClick(MultiFace(s.Slot, s.CardIds.Last())); }
            SupportRequire(_paymentConfirm!.Disabled, "Required player defaulted"); await OpenMultiGroup("player");
            await SupportClick(StabilizationButtons(_inspector).Single(b => b.HasMeta("player") && b.GetMeta("player").AsInt32() == 1));
            SupportRequire(MultiReady && MultiCommand().Selections!.Length == 3, "Independent groups missing"); await SupportCapture("-groups");
            // Issue #1004: with every group answered the summary lists each answer, and Back reopens the last one with its answer cleared.
            SupportRequire(StabilizationText(_inspector).Contains("SUMMARY") && StabilizationButtons(_decisionActions).Any(b => b.Text.Contains("Back")), "The answered groups should show a summary and a Back button");
            var answered = MultiUpFront.Count(s => MultiCount(s) > 0); MultiBack(); await EventFrames(4);
            SupportRequire(MultiUpFront.Count(s => MultiCount(s) > 0) == answered - 1 && _multiExpanded.Count == 1 && !MultiReady && !StabilizationText(_inspector).Contains("SUMMARY"), "Back should clear the last answer and reopen that group");
            await OpenMultiGroup("player");
            await SupportClick(StabilizationButtons(_inspector).Single(b => b.HasMeta("player") && b.GetMeta("player").AsInt32() == 1));
            SupportRequire(MultiReady, "The group answered again after Back");
            var targets = MultiSelectedIds.ToArray(); var opponentHonor = _multiFixture.State.Players[1].Honor; await ConfirmMulti();
            SupportRequire(targets.All(id => _multiFixture.State.Cards[id].Bowed) && _multiFixture.State.Players[1].Honor == opponentHonor + 1, "Mixed group engine result wrong");
            await PrepareMultiReview("optional"); SupportRequire(!_paymentConfirm!.Disabled && !MultiSelectedIds.Any(), "Optional zero refused"); await ConfirmMulti();
            SupportRequire(_multiFixture.State.Cards.Values.Count(c => c.Zone == Zone.Attached) == 5, "Optional zero destroyed cards");
            await PrepareMultiReview("hand"); slot = MultiUpFront.Single(); SupportRequire(slot.CardIds.All(id => _multiFixture.State.Players[0].Hand.Contains(id)), "Private candidates expose opponent hand");
            var hand = slot.CardIds[0]; MultiFace(slot.Slot, hand).GrabFocus(); await LookKey(Key.Enter); await SupportCapture("-hand");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(10);
            SupportRequire(_paymentConfirm!.Disabled && !MultiSelectedIds.Any(), "Busy connection retained/submitted draft");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(10);
            await SupportClick(MultiFace(slot.Slot, hand)); await ConfirmMulti(); SupportRequire(_multiFixture.State.Cards[hand].Zone == Zone.FateDiscard, "Private selection did not discard");
            await PrepareMultiReview("reactions"); var guard = MultiUpFront.First().CardIds.Single(id => _multiFixture.State.Cards[id].DefinitionId == "training-merchant-free-guard");
            await SupportClick(MultiFace("first", guard)); await OpenMultiGroup("second"); await SupportClick(MultiFace("second", _multiEnemy)); await ConfirmMulti();
            var seen = new List<string>();
            for (var n = 0; n < 8 && View.PendingChoice?.Type == "beforeActionReaction"; n++)
            {
                var current = View.PendingChoice.CardId!; seen.Add(current);
                SupportRequire(_tiles.Single(t => t.Card.Id == current).LegalTarget, "Current reaction target not highlighted");
                if (View.ViewerId != View.PriorityPlayerId) { _session.SwitchPracticeSeat(); await EventFrames(10); }
                SupportRequire(_tableViewport.GetGlobalRect().HasPoint(ScreenCenter(_tiles.Single(t => t.Card.Id == current))), "Reaction target is outside the visible table");
                if (n == 0) await SupportCapture("-reaction");
                await _session.Submit(Actions.Single(a => a.Enabled && a.Type == "declineReaction").Command!); await WaitDecisionPresentation(); await EventFrames(15);
            }
            SupportRequire(seen.Distinct().SequenceEqual(new[] { guard, _multiEnemy }) && View.PendingChoice is null, "Reaction windows did not preserve target order");
            await CaptureMultiPerformer();
            if (_captureMode.Contains("review")) { await SupportClick(_multiReviewButtons.Single(b => b.Text == "Bonds of Coin")); await EventFrames(90); SupportRequire(MultiTargetActive && _chosen?.Command?.AbilityId == "bonds-of-coin-destroy", "Prepared navigation failed"); }
            GD.Print("MULTI_TARGETS_PASS: native board/keyboard selection, count bounds, ordered arrows, exact engine effects, conditional follow-ups, independent slots, optional zero, hand privacy, seat/busy reset and per-target reactions"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
