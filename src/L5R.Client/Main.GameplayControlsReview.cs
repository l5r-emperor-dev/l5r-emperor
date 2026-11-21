using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private HBoxContainer? _controlsReviewBar;
    private OptionButton? _controlsReviewPicker;
    private string _controlsReviewKind = "reaction";
    private static readonly string[] ControlsReviewKinds = ["reaction", "destruction", "discount", "entry", "event", "honor", "honor-reaction", "killing", "infiltrator", "greasing", "favor", "additional"];
    private async Task StartGameplayControlsReview(string? checks, string? startupCapture = null)
    {
        try
        {
            _autoOpponent = false;
            _controlsReviewBar = new HBoxContainer { Theme = SidebarSkin.Theme(), Position = new(370, 150), Size = new(900, 54) };
            _root.AddChild(_controlsReviewBar);
            _controlsReviewPicker = new OptionButton { CustomMinimumSize = new(420, 54), FitToLongestItem = false, ClipText = true };
            foreach (var kind in ControlsReviewKinds) _controlsReviewPicker.AddItem(kind switch
            {
                "reaction" => "Payment · Bow and Reaction",
                "destruction" => "Payment · Destroy a Holding",
                "discount" => "Payment · Remove for a discount",
                "entry" => "Entry cost · Hand card or Sake token",
                "event" => "Event · Optional draw",
                "honor" => "Honor · Lose or prevent loss",
                "honor-reaction" => "Honor · Reaction opportunity",
                "killing" => "Battle · Choose a Personality",
                "infiltrator" => "Straighten · Choose a Personality",
                "greasing" => "Battle · Optional Gold payment",
                "favor" => "Favor · Kurohito’s Perfection",
                _ => "Optional · Additional action"
            });
            _controlsReviewPicker.ItemSelected += i => { _ = PrepareGameplayControlsReview(ControlsReviewKinds[(int)i]); };
            _controlsReviewBar.AddChild(_controlsReviewPicker);
            foreach (var button in new[] { SidebarSkin.Button("Restart", () => { _ = PrepareGameplayControlsReview(_controlsReviewKind); }), SidebarSkin.Button("Other seat", SwitchSeat) })
            { button.CustomMinimumSize = new(160, 54); button.AutowrapMode = TextServer.AutowrapMode.Off; _controlsReviewBar.AddChild(button); }
            var initial = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--controls-scenario=", StringComparison.Ordinal))?[20..] ?? (_captureMode == "gameplay-controls-choices-review" ? "entry" : _captureMode == "gameplay-controls-optional-review" ? "additional" : "reaction");
            await PrepareGameplayControlsReview(initial);
            EnterTable();
            GD.Print("GAMEPLAY_CONTROLS_REVIEW_READY: real engine scenarios; restart resets the position.");
            if (startupCapture is not null) { await EventFrames(8); GetViewport().GetTexture().GetImage().SavePng(startupCapture); }
            if (checks is not null) await CheckGameplayControlsReview(checks);
        }
        catch (Exception e) { GD.PrintErr(e); if (checks is not null) GetTree().Quit(1); }
    }
    private async Task PrepareGameplayControlsReview(string kind)
    {
        _controlsReviewKind = kind;
        _controlsReviewPicker!.Select(Array.IndexOf(ControlsReviewKinds, kind));
        if (_session.Snapshot is not null) ResetDecisionSelection();
        var engine = MatchEngine.CreateGameplayControlsShowcase(kind);
        await LoadSupportFixture(engine, engine.State.PriorityPlayerId);
        if (kind == "reaction") Choose(Actions.First(a => a.Enabled && a.Type == "purchase" && a.CardId == Self.Provinces[0].Card?.Id));
        else if (kind == "destruction") Choose(Actions.First(a => a.Enabled && a.Command?.AbilityId == "kharmic-discard"));
        else if (kind == "discount")
        {
            var rani = Self.Army.Single(c => c.Definition?.Id == "oracle-l5r-5361");
            var action = Actions.First(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == rani.Id &&
                a.TargetGold?.Values.Any(q => q.Sources?.Any(m => m.Kind == "discount") == true) == true);
            var target = action.TargetGold!.First(q => q.Value.Cost > 0 && q.Value.Sources?.Any(m => m.Kind == "discount") == true).Key;
            Choose(action); ChooseTarget(target);
        }
        else if (kind == "favor") SelectHomeReminder(View.ViewerId, null);
        else if (kind == "greasing")
            SelectCard(Self.Hand.Single(c => c.Definition?.Id == "oracle-l5r-2896"));
        RenderInspector(); Highlight(); await EventFrames(8);
    }
    private async Task CheckGameplayControlsReview(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            foreach (var kind in ControlsReviewKinds)
            {
                await PrepareGameplayControlsReview(kind);
                var version = View.Version;
                if (kind is "reaction" or "destruction" or "discount")
                {
                    GoldPickFirstTarget(); // issue #581: a targeted action starts with no target; the player picks one through the list of legal cards
                    GoldRequire(_chosen is not null && QuoteFor(_chosen)?.Sources?.Length > 0 && _sources.Count == 0, $"{kind} has no usable payment, or opened pre-selected (chosen={_chosen?.Label}; sources={(_chosen is null ? -1 : QuoteFor(_chosen)?.Sources?.Length)}; confirmDisabled={_paymentConfirm?.Disabled}; targets={_chosen?.TargetIds?.Length}; target={_chosen?.Command?.TargetId})");
                    _sources.Clear(); RenderInspector();
                    GoldRequire(_paymentConfirm is { Disabled: true }, kind + " confirmed without sources");
                    var quote = QuoteFor(_chosen!)!;
                    var chosen = kind == "reaction" ? quote.Sources!.Where(m => m.Kind is "bow" or "reaction").ToArray() :
                        quote.Sources!.Where(m => m.Kind == (kind == "discount" ? "discount" : "destroy")).Take(1).ToArray();
                    if (kind == "destruction" && chosen.Length == 1)
                    {
                        // Issue #971: the card paid with is clicked on the table: a click chooses its method, a second click clears it.
                        var payer = FindCard(chosen[0].CardId)!;
                        SelectCard(payer); await EventFrames(3);
                        GoldRequire(_sources.Contains(chosen[0].Id), "Clicking the card on the table did not choose its payment method");
                        SelectCard(payer); await EventFrames(3);
                        GoldRequire(!_sources.Contains(chosen[0].Id), "Clicking the card again did not clear its payment method");
                        SelectCard(payer); await EventFrames(3);
                        GoldRequire(_sources.Contains(chosen[0].Id) && _paymentConfirm is { Disabled: false }, "The table-chosen payment did not cover the cost");
                    }
                    foreach (var method in chosen)
                    {
                        var control = _inspector.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>().Single(b => b.GetMeta("payment_source_id", "").AsString() == method.Id);
                        control.ButtonPressed = true; await EventFrames(2);
                    }
                    GoldRequire(_paymentConfirm is { Disabled: false } && View.Version == version, kind + " selection committed or failed coverage");
                    if (kind == "reaction")
                    {
                        var keigo = Self.Home.Single(c => c.Definition?.Id == "oracle-l5r-497");
                        var unresolved = quote.Sources!.Single(m => m.CardId == keigo.Id);
                        var unavailable = _inspector.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>()
                            .Single(b => b.GetMeta("payment_source_id", "").AsString() == unresolved.Id);
                        GoldRequire(unresolved.Produced == 0 && unavailable.Disabled, "Variable discard source was offered without a discard picker");
                        _sources.Add(unresolved.Id); RenderInspector();
                        GoldRequire(_paymentConfirm is { Disabled: true }, "Unknown payment choice enabled confirmation alongside sufficient Gold");
                        _sources.Remove(unresolved.Id); RenderInspector();
                    }
                    await GetTree().ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, kind + ".png"));
                    _paymentConfirm!.EmitSignal(Button.SignalName.Pressed); await EventFrames(18); _uiQueue.Clear(); Render();
                    GoldRequire(View.Version > version, kind + " engine refused payment");
                }
                else if (kind is "entry" or "event" or "honor" or "honor-reaction" or "killing" or "infiltrator")
                {
                    GoldRequire(GenericDecisionActive && GenericDecisionConfirm().Disabled, kind + " has no explicit choice");
                    var option = Actions.First(a => a.Enabled && a.Type is not ("concede" or "wait") && a.Command is not null);
                    SelectCandidateOption(option);
                    GoldRequire(!GenericDecisionConfirm().Disabled && View.Version == version, kind + " selected by default or committed early");
                    await EventFrames(3);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, kind + ".png"));
                    var originalSeat = View.ViewerId; _session.SwitchPracticeSeat(); await EventFrames(8); Render();
                    GoldRequire(_candidateTray?.Visible != true && !_inspector.FindChildren("*", "CheckBox", true, false).Any(), kind + " leaked candidates to waiting seat");
                    _session.SwitchPracticeSeat(); await EventFrames(8); Render();
                    GoldRequire(View.ViewerId == originalSeat && GenericDecisionConfirm().Disabled, kind + " kept another seat’s selection");
                    SelectCandidateOption(Actions.First(a => a.Enabled && a.Type is not ("concede" or "wait") && a.Command is not null));
                    CommitDecision(); await WaitDecisionPresentation(); await EventFrames(18); Render();
                    GoldRequire(View.Version > version, kind + " engine refused decision");
                }
                else
                {
                    await EventFrames(3);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, kind + ".png"));
                    if (kind == "additional") GoldRequire(_phaseActions.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text == "Decline the additional action"), "Additional-action decline missing");
                }
            }
            GD.Print("UI_SMOKE_PASS: gameplay-controls source methods, exact commands, confirmation, private choices and seat changes.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
    }

    // The generic decision's commit button. In a reaction window (an honor-change reaction) it is the window's "Choose a reaction" / "Play Reaction" button (named CommitAction, since 044b2424),
    // elsewhere "Confirm choice".
    private Button GenericDecisionConfirm() => HasContextualReactionDecline
        ? StabilizationButtons(_root).Single(b => b.IsVisibleInTree() && b.Name == "CommitAction") : DecisionButton("Confirm choice");
}
