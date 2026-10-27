using Godot;
using L5R.Core;
using System.Reflection;
namespace L5R.Client;

public partial class Main
{
    private bool _stabilizationCaptureStarted;
    private void ProcessStabilizationCapture(double delta)
    {
        _captureClock += delta;
        if (_stabilizationCaptureStarted || _captureClock < 1.5) return;
        _stabilizationCaptureStarted = true; CaptureStabilization();
    }
    private static (MatchEngine Engine, string Target, string? Source) StabilizationFixture(string mode)
    {
        var s = MatchEngine.CreatePublishedTestMatch(42).State;
        s.Phase = Phase.Battle; s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        CardInstance Add(string def, int owner, Zone zone)
        {
            var c = new CardInstance { Id = "stabilization-native-" + s.Cards.Count, DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = zone == Zone.Battlefield ? 0 : null };
            s.Cards[c.Id] = c; if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id); return c;
        }
        GameCommand? trigger = null; string target; string? source = null;
        if (mode.Contains("equip"))
        {
            s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = []; s.Players[0].Clan = "Lion";
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0)) c.Bowed = true;
            Add("oracle-l5r-430", 0, Zone.Home);
            target = Add(mode.Contains("free") ? "oracle-l5r-5112" : "oracle-l5r-2060", 0, Zone.Home).Id;
            source = Add("oracle-l5r-6481", 0, Zone.Hand).Id;
        }
        else if (mode.Contains("brash"))
        {
            s.Phase = Phase.InfantryDeclaration; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [];
            var brash = Add("oracle-l5r-10058", 1, Zone.Home); target = brash.Id;
            trigger = new("attack", 1, s.Version, Assignments: new() { [brash.Id] = 0 });
        }
        else if (mode.Contains("bow"))
        {
            source = Add("oracle-l5r-8521", 0, Zone.Battlefield).Id;
            var bower = Add("oracle-l5r-6885", 1, Zone.Battlefield); var bowed = Add("oracle-l5r-2060", 0, Zone.Battlefield); target = bowed.Id;
            trigger = new("useAbility", 1, s.Version, CardId: bower.Id, TargetId: bowed.Id, AbilityId: "horibe-bow");
        }
        else
        {
            source = Add("oracle-l5r-10056", 0, Zone.Home).Id;
            var mover = Add("oracle-l5r-4937", 1, Zone.Battlefield); var affected = Add("oracle-l5r-2060", 0, Zone.Battlefield); target = affected.Id;
            trigger = new("useAbility", 1, s.Version, CardId: mover.Id, TargetId: affected.Id, AbilityId: "incapacitated");
        }
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [s])!;
        if (trigger is not null) { var result = engine.Execute(trigger); if (!result.Accepted) throw new Exception(result.Error); }
        return (engine, target, source);
    }
    private static IEnumerable<Button> StabilizationButtons(Node node) => node.GetChildren().SelectMany(c =>
        c is Button b ? new[] { b }.Concat(StabilizationButtons(c)) : StabilizationButtons(c));
    private Button StabilizationButton(string text) => StabilizationButtons(this).FirstOrDefault(b => b.IsVisibleInTree() && !b.Disabled && b.Text.StartsWith(text, StringComparison.Ordinal))
        ?? throw new Exception("Missing enabled native button: " + text + " (visible: " + string.Join(" | ", StabilizationButtons(this).Where(b => b.IsVisibleInTree()).Select(b => (b.Disabled ? "[off] " : "") + b.Text)) + ")");
    private Button ReviewedCommitButton() => _paymentConfirm is { Disabled: false } button && button.IsVisibleInTree() && button.Name == "CommitAction"
        ? button : throw new Exception($"Missing visible enabled reviewed action commit (button: {(_paymentConfirm is null ? "none" : $"name={_paymentConfirm.Name} text={_paymentConfirm.Text} disabled={_paymentConfirm.Disabled} visible={_paymentConfirm.IsVisibleInTree()}")}; chosen: {_chosen?.Type}; sources: {_sources.Count})");
    private async Task StabilizationClick(Control target)
    {
        // Scroll the control fully into view first, as a player would: the centre of a partly clipped button can sit under the pinned footer (SupportClick does the same).
        if (target.GetViewport() == GetViewport())
        {
            for (Node? parent = target.GetParent(); parent is not null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(target);
            await EventFrames(5);
        }
        var p = target.GetGlobalTransformWithCanvas() * (target.Size / 2);
        var viewport = target.GetViewport();
        if (viewport != GetViewport())
        {
            // Embedded modal windows have their own GUI focus and input viewport.
            target.GrabFocus();
            viewport.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }, true);
            viewport.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }, true);
            await EventFrames(12); return;
        }
        void Push(InputEventMouse input) { input.SetMeta(CaptureMouseTag, true); viewport.PushInput(input, true); }
        Push(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        Push(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
        Push(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false });
        await EventFrames(12);
    }
    private async void CaptureStabilization()
    {
        try
        {
            void Check(bool value, string message) { if (!value) throw new Exception(message); }
            var fixture = StabilizationFixture(_captureMode); var other = _captureMode.Contains("opponent");
            _autoOpponent = false; _reducedMotion = true; _session.StartEventProbe(fixture.Engine, other ? 1 : 0);
            _uiQueue.Clear(); _presentationSnapshot = null; _battlePresentationStage = 0; _motionView = null; _selected = null; _chosen = null;
            Render(); ResetTableCamera(); GetWindow().GrabFocus(); await EventFrames(12);
            for (var n = 0; n < 180 && (_turnBloom.Playing || !GetWindow().HasFocus()); n++) await EventFrames(1);
            Check(GetWindow().HasFocus(), "Native window did not focus");
            var before = View.Version; var hand = Self.HandCount;
            if (other)
            {
                Check(Actions.All(a => a.Type is "wait" or "concede"), "Other seat received private/acting choices");
                Check(!StabilizationButtons(_root).Any(b => b.IsVisibleInTree() && b.Text == "Draw 1 card"), "Opponent has draw button");
            }
            else if (_captureMode.Contains("brash"))
            {
                Check(View.PendingChoice?.Type == "brashDraw" && _eventSidebar.Visible, "Brash panel absent");
                GetViewport().GetTexture().GetImage().SavePng(_capturePath!);
                var decline = _captureMode.Contains("decline"); await StabilizationClick(StabilizationButton(decline ? "Decline draw" : "Draw 1 card"));
                Check(View.Version == before + 1 && View.PendingChoice is null && View.Phase == Phase.DefenseAssignment, "Brash failed to resume");
                Check(Self.HandCount == hand + (decline ? 0 : 1), "Wrong Brash draw count");
            }
            else if (_captureMode.Contains("equip"))
            {
                // Select the public card, then use the actual payment controls and viewport input.
                SelectCard(FindCard(fixture.Source!)!); await EventFrames();
                var cost = _captureMode.Contains("free") ? 0 : 2;
                var option = Actions.Single(a => a.Type == "equip" && a.CardId == fixture.Source && a.Enabled);
                Check(option.TargetIds!.Contains(fixture.Target), "Expected target missing from quoted group");
                Check(_playPrompt is null && _chosen is { Type: "equip" }, "Selecting the equipment did not begin the equip (issue #952: an action that needs a target goes straight to choosing it)");
                ChooseTarget(fixture.Target!); await EventFrames();
                if (cost > 0 && QuoteFor(_chosen!) is { Payments.Length: > 0 } quote) { GoldClickHoldings(quote.Payments[0].SourceIds); await EventFrames(3); } // the player bows holdings to pay (#731)
                var button = ReviewedCommitButton();
                Check(cost == 0 ? !button.Text.Contains("Gold", StringComparison.Ordinal) : button.Text.EndsWith(" · 2 Gold", StringComparison.Ordinal), "Reviewed attachment commit has the wrong Gold caption");
                Check(_chosen is not null && QuoteFor(_chosen)?.Cost == cost, "Client did not retain authoritative quote");
                GetViewport().GetTexture().GetImage().SavePng(_capturePath!); await StabilizationClick(button);
                Check(View.Version == before + 1 && FindCard(fixture.Source!)?.Zone == Zone.Attached, "Discounted equip did not execute");
                Check(FindCard(fixture.Source!)?.AttachedTo == fixture.Target, "Equipment attached to wrong target");
            }
            else
            {
                var bow = _captureMode.Contains("bow"); Check(View.PendingChoice?.Type == (bow ? "bowReaction" : "beforeActionReaction"), "Reaction not opened by engine");
                var option = Actions.Single(a => a.Enabled && a.Command?.AbilityId == (bow ? "hizumi-negate-straighten" : "tamoko-negate"));
                GetViewport().GetTexture().GetImage().SavePng(_capturePath!);
                if (!_captureMode.Contains("pass"))
                {
                    await StabilizationClick(ReactionOption(option.CardId!, option.Command!.AbilityId!, option.Command.PerformerId));
                    // Both reactions open their reviewed action first and execute on Confirm (the Tamoko reaction lists no plain targets, so it takes the same review path as the Hizumi bow).
                    Check(View.Version == before, "Opening the reaction mutated the match"); GetViewport().GetTexture().GetImage().SavePng(_capturePath + ".target.png"); await StabilizationClick(ReviewedCommitButton());
                    Check(View.Version == before + 1, "Reaction did not execute exactly once");
                }
                for (var n = 0; n < 4 && View.PendingChoice is not null; n++)
                {
                    if (View.ViewerId != View.PriorityPlayerId) { await StabilizationClick(StabilizationButton("Pass device")); await StabilizationClick(StabilizationButton("I'm ready")); await EventFrames(); }
                    await StabilizationClick(StabilizationButtons(_decisionActions).Single(b => b.Name == "ReactionDecline" && b.IsVisibleInTree() && !b.Disabled));
                }
                Check(View.PendingChoice is null, "Reaction failed to resume");
                var target = FindCard(fixture.Target)!;
                Check(bow ? target.StraightenBlocked : target.Zone == (_captureMode.Contains("pass") ? Zone.Home : Zone.Battlefield), "Wrong reaction outcome");
            }
            await EventFrames(); GetViewport().GetTexture().GetImage().SavePng(other ? _capturePath! : _capturePath + ".resolved.png");
            GD.Print("STABILIZATION_NATIVE_PASS: " + _captureMode); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); RenderingServer.ForceDraw(false); GetViewport().GetTexture().GetImage().SavePng(_capturePath + ".failure.png"); GetTree().Quit(1); }
    }
}
