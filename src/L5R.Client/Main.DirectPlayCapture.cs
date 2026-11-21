using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _directPlayStarted;

    private void ProcessDirectPlayCapture(double delta)
    {
        _captureClock += delta; if (_directPlayStarted || _captureClock < 1.5) return;
        _directPlayStarted = true; _ = CheckDirectPlay();
    }

    private IEnumerable<Button> ActionTextBlocks() => _inspector.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.HasMeta("action_text_block"));

    private async Task LoadDirectPlay(Action<MatchState> build, int seat = 0)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); p.TurnsTaken = 2; }
        state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        build(state);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, seat);
    }

    private static void BattleSetup(MatchState state)
    {
        state.Phase = Phase.Battle; state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.BattleNumber = 1;
        SupportFixtureCard(state, 229, 0, Zone.Battlefield, 0);               // Akodo Kisho, unbowed Paragon
        SupportFixtureCard(state, 3636, 1, Zone.Battlefield, 0);              // an enemy unit at the battlefield
    }

    private async Task CheckDirectPlay()
    {
        try
        {
            // 1. One legal, automatic action: selecting the card asks to confirm, and confirming plays it with nothing else chosen.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 7862, 0, Zone.Hand); });
            var tale = View.Players[0].Hand.Single();
            var honor = View.Players[0].Honor;
            SelectCard(tale); await EventFrames(4);
            SupportRequire(_playPrompt is not null && _chosen is null, "Selecting a playable card did not ask to confirm play");
            SupportRequire(StabilizationText(_inspector).Contains("PLAY THIS CARD?") && StabilizationText(_inspector).Contains("Nothing else to choose"), "Automatic action should say nothing else needs choosing");
            var cancel = StabilizationButtons(_decisionActions).Single(b => b.Text == "Cancel"); cancel.EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(2);
            SupportRequire(_playPrompt is null && _chosen is null && View.Players[0].Honor == honor, "Cancel must back out without acting");
            SelectCard(tale); await EventFrames(2);
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Confirm play")); await WaitDecisionPresentation(); await EventFrames(6);
            SupportRequire(View.Players[0].Honor == honor + 1 && _chosen is null, "Confirming Tell the Tale did not gain 1 Honor directly");

            // 2. Two legal actions: the card lists both as clickable action text; a click goes straight to choosing that action's targets, and the other action stays one click away.
            await LoadDirectPlay(s => { BattleSetup(s); SupportFixtureCard(s, 61, 0, Zone.Hand); });
            var paragon = View.Players[0].Hand.Single();
            SelectCard(paragon); await EventFrames(4);
            var blocks = ActionTextBlocks().ToArray();
            SupportRequire(_playPrompt is null && _chosen is null && blocks.Length == 2, $"Two legal actions should be offered as selectable text, found {blocks.Length}: " + string.Join(" | ", Actions.Where(x => x.CardId == paragon.Id).Select(x => $"{x.Type}/{x.Command?.AbilityId}/{x.Enabled}/{x.Reason}")));
            await SupportClick(blocks[0]); await EventFrames(2);
            SupportRequire(_playPrompt is null && _chosen is { Type: "useAbility" }, "Choosing an action's text should begin that action without a confirm press");
            var first = _chosen!.Command!.AbilityId;
            SupportRequire(StabilizationButtons(_inspector).Any(b => b.Text.StartsWith("Play instead:", StringComparison.Ordinal)), "The draft should keep the card's other action one click away; buttons: " + string.Join(" | ", StabilizationButtons(_inspector).Select(b => b.Text)) + " ;; choices " + PlayChoices(paragon).Length + " ;; multi " + MultiTargetActive + " ;; text: " + StabilizationText(_inspector));
            CancelChoice(); await EventFrames(2);
            SupportRequire(_playPrompt is null && _chosen is null, "Cancel returns to the card");
            SelectCard(paragon); await EventFrames(2);
            ActionTextBlocks().ElementAt(1).EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(2);
            SupportRequire(_chosen is { Type: "useAbility" } && _chosen.Command!.AbilityId != first, "The second action's text should begin the second action");

            // 3. Only one of the card's two actions is legal (no unbowed Kensai): assume it, no choice of action.
            await LoadDirectPlay(s => { BattleSetup(s); SupportFixtureCard(s, 5112, 0, Zone.Battlefield, 0, bowed: true); SupportFixtureCard(s, 5423, 0, Zone.Hand); });
            var moving = View.Players[0].Hand.Single();
            var legal = Actions.Where(a => a.Enabled && a.CardId == moving.Id).Select(a => a.Command?.AbilityId).Distinct().ToArray();
            SelectCard(moving); await EventFrames(4);
            SupportRequire(legal.Length == 1 && _playPrompt is not null && ActionTextBlocks().Count() == 0, $"With one legal action ({legal.Length}) it should be assumed");
            SupportRequire(PromptedChoice()!.Option.Command!.AbilityId == "moving-unmoving-approach", "The legal first action should be the one assumed (nothing else to choose, so the prompt is its final Confirm)");
            // 4. Issue #793: a Strategy that any of several Personalities may perform (Spirit of the Berserker) is performed by clicking a highlighted Personality on the table.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 229, 0, Zone.Home); SupportFixtureCard(s, 3636, 0, Zone.Home); SupportFixtureCard(s, 7420, 0, Zone.Hand); });
            var spirit = View.Players[0].Hand.Single();
            SelectCard(spirit); await EventFrames(4);
            SupportRequire(_chosen is not null && StabilizationText(_inspector).Contains("click a highlighted Personality on the table"), "The performer choice is not explained");
            var performerClicks = PerformerClicks();
            SupportRequire(performerClicks.Count == 2, $"Both Personalities should be clickable performers, found {performerClicks.Count}");
            var otherPerformer = performerClicks.Keys.Single(id => id != _chosen!.Command!.PerformerId);
            var performerTile = _tiles.Single(t => t.Card.Id == otherPerformer);
            SupportRequire(performerTile.LegalTarget, "The other performing Personality is not highlighted on the table");
            await SupportClick(performerTile); await EventFrames(6);
            SupportRequire(_chosen?.Command?.PerformerId == otherPerformer, "Clicking the Personality did not choose it as the performer");
            // Issue #1004, the choosing flow: the performer is asked first and alone, the click moves on, and Back returns to the question.
            SupportRequire(!PerformerStepActive && FlowCanGoBack(_chosen!), "After the performer is chosen the performer step is over and Back is offered");
            FlowBack(); await EventFrames(4);
            SupportRequire(PerformerStepActive && StabilizationText(_inspector).Contains("CHOOSE A PERFORMER") && StabilizationButtons(_inspector).Count(b => b.HasMeta("performer_row")) == 2, "Back should return to the performer question listing both performers");
            StabilizationButtons(_inspector).First(b => b.HasMeta("performer_row")).EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(4);
            SupportRequire(!PerformerStepActive, "One click on a performer row moves on");

            // 4b. Issue #1004, the choosing flow end to end with The Sound of Thunder: performer, then target, one question at a time; the summary appears once both are answered; Back undoes the last answer.
            CardInstance foe = null!;
            await LoadDirectPlay(s =>
            {
                BattleSetup(s);
                foreach (var home in new[] { 229, 3636 }) SupportFixtureCard(s, home, 0, Zone.Home).GrantedKeywords.Add(new("Battle Maiden", "test-fixture", GrantDuration.Permanent));
                foe = SupportFixtureCard(s, 3636, 1, Zone.Battlefield, 0); SupportFixtureCard(s, 8347, 0, Zone.Hand);
            });
            var thunder = View.Players[0].Hand.Single();
            SelectCard(thunder); await EventFrames(4);
            SupportRequire(PerformerStepActive && !StabilizationText(_inspector).Contains("CHOOSE A TARGET"), "Step one asks only for the performer");
            var maidenRow = StabilizationButtons(_inspector).First(b => b.HasMeta("performer_row"));
            maidenRow.EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(4);
            SupportRequire(!PerformerStepActive && StabilizationText(_inspector).Contains("CHOOSE A TARGET") && !StabilizationText(_inspector).Contains("SUMMARY"), "Step two asks only for the target");
            ChooseTarget(foe.Id); await EventFrames(4);
            var summary = StabilizationText(_inspector);
            SupportRequire(summary.Contains("SUMMARY") && summary.Contains("Move and Straighten (") && summary.Contains("Give (") && summary.Contains("-3F"), "The summary lists the effects: " + summary);
            FlowBack(); await EventFrames(4);
            SupportRequire(_chosen!.Command!.TargetId is null && !StabilizationText(_inspector).Contains("SUMMARY") && StabilizationText(_inspector).Contains("CHOOSE A TARGET"), "Back returns to the target question");

            // 4c. Issue #1004: A Paragon's Strength end to end -- the action, the performing Paragon, then the Ranged Attack's target -- one question at a time, with a summary and Back.
            await LoadDirectPlay(s => { BattleSetup(s); SupportFixtureCard(s, 3636, 0, Zone.Battlefield, 0).GrantedKeywords.Add(new("Paragon", "test-fixture", GrantDuration.Permanent)); SupportFixtureCard(s, 61, 0, Zone.Hand); });
            var strength = View.Players[0].Hand.Single();
            SelectCard(strength); await EventFrames(4); await SupportCapture("-paragon-1-action");
            await SupportClick(ActionTextBlocks().First(b => b.Text.Contains("Ranged", StringComparison.Ordinal))); await EventFrames(6); await SupportCapture("-paragon-2-performer");
            SupportRequire(PerformerStepActive, "A Paragon's Strength Ranged Attack should ask for the performer first: " + StabilizationText(_inspector));
            StabilizationButtons(_inspector).First(b => b.HasMeta("performer_row")).EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(6); await SupportCapture("-paragon-3-target");
            SupportRequire(AttackTargetActive && _attackChoice == AttackChoice.Unselected, "Then the Attack target is asked");
            ChooseAttackTarget(AttackChoice.Card, _chosen!.TargetIds![0]); await EventFrames(6); await SupportCapture("-paragon-4-summary");
            SupportRequire(StabilizationText(_inspector).Contains("SUMMARY") && StabilizationText(_inspector).Contains("Lose 4 Honor"), "The summary lists the Attack and the Honor loss: " + StabilizationText(_inspector));

            // 5. Issue #793: the Declare Attack button is a blood-red lacquer, not black.
            await LoadDirectPlay(s => { s.Phase = Phase.AttackDeclaration; SupportFixtureCard(s, 229, 0, Zone.Home); SupportFixtureCard(s, 3636, 1, Zone.Home); });
            var declare = StabilizationButtons(_phaseActions).FirstOrDefault(b => b.Text.Contains("Declare an attack", StringComparison.Ordinal));
            SupportRequire(declare is not null, "No Declare an attack button is offered: " + string.Join(" | ", Actions.Select(x => x.Type)));
            SupportRequire(declare!.GetThemeStylebox("normal") is StyleBoxTexture { ModulateColor: var red } && red.R > red.G * 3 && red.R > red.B * 3, "Declare an attack is not blood red"); await EventFrames(4); GetViewport().GetTexture().GetImage().SavePng(Path.ChangeExtension(_capturePath!, null) + "-declare.png");

            // 6. Issue #793: a finished match offers a return to the main menu as well as another practice.
            await LoadDirectPlay(s => { s.WinnerId = 0; s.VictoryReason = "Test victory"; SupportFixtureCard(s, 229, 0, Zone.Home); });
            var labels = StabilizationButtons(_inspector).Select(b => b.Text).ToArray();
            SupportRequire(labels.Contains("Return to main menu") && labels.Contains("Start another practice"), "The end of a match does not offer the main menu: " + string.Join(" | ", labels));
            // 7. Issues #952 and #960: "choose a player" is one play whose draft asks for the player and is answered on the table; it never resolves for "you" by default. A card that names "another player" decides it itself.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 229, 0, Zone.Home); SupportFixtureCard(s, 4487, 0, Zone.Hand); });
            var minds = View.Players[0].Hand.Single();
            var enabledPlayers = Actions.Where(a => a.Enabled && a.CardId == minds.Id && a.Command?.TargetPlayerId is not null).Select(a => a.Command!.TargetPlayerId!.Value).Distinct().Order().ToArray();
            SupportRequire(enabledPlayers.Length == 2, "Know Their Minds should be playable on either player: " + string.Join(" | ", Actions.Where(x => x.CardId == minds.Id).Select(x => $"{x.Type}/{x.Command?.AbilityId}/{x.Command?.TargetPlayerId}/{x.Enabled}/{x.Reason}")));
            SelectCard(minds); await EventFrames(4);
            SupportRequire(_chosen is { Type: "useAbility" } && PlayChoices(minds).Length == 1 && ActionTextBlocks().Count() == 0, "Choose a player is one play, begun at once");
            SupportRequire(!PlayerPicked && _paymentConfirm is { Disabled: true }, "No player may be picked for the player, and Confirm must wait");
            var foeHold = View.Players.First(p => p.Id != View.ViewerId).Stronghold;
            SupportRequire(_tiles.Single(t => t.Card.Id == foeHold.Id).LegalTarget, "The opponent's stronghold is not highlighted as a choice");
            SelectCard(foeHold); await EventFrames(3);
            SupportRequire(_chosen?.Command?.TargetPlayerId == 1 && _paymentConfirm is { Disabled: false }, $"Clicking the opponent's stronghold must choose the opponent (got {_chosen?.Command?.TargetPlayerId})");
            SelectCard(View.Players[0].Stronghold); await EventFrames(3);
            SupportRequire(_chosen?.Command?.TargetPlayerId == 0, "Clicking your own stronghold must choose you");
            await SupportClick(_clanPlaques[1].Hit); await EventFrames(3);
            SupportRequire(_chosen?.Command?.TargetPlayerId == 1, $"Clicking the opponent's clan plaque must choose the opponent (got {_chosen?.Command?.TargetPlayerId})");
            await LoadDirectPlay(s => { s.Phase = Phase.Action; s.Players[1].Honor = 30; SupportFixtureCard(s, 10380, 0, Zone.Hand); });
            var misstep = View.Players[0].Hand.Single();
            SelectCard(misstep); await EventFrames(4);
            SupportRequire(ActionTextBlocks().Count() == 0 && (_playPrompt is not null || _chosen is not null) && PlayChoices(misstep).Length == 1, "Imprudent Misstep names another player, so there is nothing to choose");
            SupportRequire(PlayChoices(misstep).Single().Option.Command!.TargetPlayerId == 1 && !StabilizationText(_inspector).Contains("Not available now", StringComparison.Ordinal), "It should play on the opponent without listing the you-variant as unavailable: " + string.Join("/", StabilizationText(_inspector).Split((char)10)));

            // 8. Issue #1228: Wall of Honor is played and its performer chosen by clicks on the table alone, a bowed Personality among the performers.
            CardInstance lishan = null!, chikako = null!;
            await LoadDirectPlay(s =>
            {
                BattleSetup(s);
                lishan = SupportFixtureCard(s, 9083, 0, Zone.Battlefield, 0, bowed: true);
                chikako = SupportFixtureCard(s, 10102, 0, Zone.Battlefield, 0);
                SupportFixtureCard(s, 9198, 0, Zone.Hand);
            });
            var wall = View.Players[0].Hand.Single();
            var wallHonor = View.Players[0].Honor;
            await SupportClick(_tiles.Single(t => t.Card.Id == wall.Id)); await EventFrames(6);
            SupportRequire(ActionTextBlocks().Count() == 0 && PlayChoices(wall).Length == 1, "Wall of Honor's increase and decrease are one play, not a choice of actions in the sidebar");
            SupportRequire(PerformerStepActive && PerformerClicks().ContainsKey(lishan.Id) && PerformerClicks().ContainsKey(chikako.Id), "Clicking the card goes straight to the performer, bowed Lishan included: " + string.Join(",", PerformerClicks().Keys));
            SupportRequire(_tiles.Single(t => t.Card.Id == lishan.Id).LegalTarget && _tiles.Single(t => t.Card.Id == chikako.Id).LegalTarget, "The performers are highlighted on the table");
            await SupportClick(_tiles.Single(t => t.Card.Id == lishan.Id)); await EventFrames(6);
            SupportRequire(_chosen?.Command?.PerformerId == lishan.Id && !PerformerStepActive, "Clicking bowed Lishan on the table chose her as the performer");
            var modes = StabilizationButtons(_inspector).Where(b => b.HasMeta("choice_row")).ToArray();
            SupportRequire(modes.Length == 2 && modes.Any(b => b.Text.StartsWith("Increase", StringComparison.Ordinal)) && modes.Any(b => b.Text.StartsWith("Decrease", StringComparison.Ordinal)) && _paymentConfirm is { Disabled: true },
                "Then increase or decrease is asked, and Confirm waits: " + string.Join(" | ", modes.Select(b => b.Text)));
            await SupportClick(modes.Single(b => b.Text.StartsWith("Decrease", StringComparison.Ordinal))); await EventFrames(4);
            SupportRequire(_chosen?.Command?.AbilityId == "wall-of-honor-lower" && _chosen.Command.PerformerId == lishan.Id && _paymentConfirm is { Disabled: false }, "Decrease keeps Lishan as the performer and Confirm is ready");
            await SupportClick(_paymentConfirm!); await WaitDecisionPresentation(); await EventFrames(8);
            SupportRequire(FindCard(lishan.Id)?.Zone == Zone.Home && View.Players[0].Honor == wallHonor + 2 && View.Events.Any(e => e.Message.Contains("Wall of Honor:", StringComparison.Ordinal) && e.Message.Contains("-1 strength", StringComparison.Ordinal)),
                $"Wall of Honor resolved: Lishan home, the battlefield's province at -1 strength, 2 Honor gained (Lishan {FindCard(lishan.Id)?.Zone}, Honor {wallHonor} -> {View.Players[0].Honor}; {string.Join(" ; ", View.Events.TakeLast(5).Select(l => l.Message))})");

            GD.Print("DIRECT_PLAY_PASS:confirm-first for an automatic action only, two-action text selection that begins the action, assumed single legal action begun at once, click-to-choose performer, Wall of Honor played and performed (bowed) by table clicks with its mode asked last, a player chosen by clicking a stronghold or plaque and a card that names the player, blood-red Declare Attack, end-of-match main menu.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
