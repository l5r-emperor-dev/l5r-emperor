using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Native check for the variant steps (issue #1202): Governor's Court asks "which Courtier", then "bow him or the Court", then confirms; Stolen Merchandise asks "which Holding", then "which of its
    // abilities", then confirms. Each question is one thing, Back undoes the latest answer, and the card's list shows one entry per play, not one per combination Core lists.
    private bool _variantStepsStarted;

    private void ProcessVariantStepsCapture(double delta)
    {
        _captureClock += delta; if (_variantStepsStarted || _captureClock < 1.5) return;
        _variantStepsStarted = true; _ = CheckVariantSteps();
    }

    private Button[] RowsWith(string meta) => StabilizationButtons(_inspector).Where(b => b.HasMeta(meta)).ToArray();

    private async Task CheckVariantSteps()
    {
        try
        {
            // 1. Governor's Court with two Courtiers: selecting the card begins its one play; the performer, then how to pay, then confirm -- each on its own, nothing pre-selected.
            await LoadDirectPlay(s =>
            {
                s.Phase = Phase.Action;
                SupportFixtureCard(s, 2878, 0, Zone.Home);                  // Governor's Court
                SupportFixtureCard(s, 142, 0, Zone.Home);                   // Agasha Kodo, a Courtier
                SupportFixtureCard(s, 790, 0, Zone.Home);                   // Bayushi Hurunayi, a Courtier
                SupportFixtureCard(s, 3636, 1, Zone.Home);                  // an enemy Personality, unbowed, so the other player can answer
            });
            var court = FindCardByName("Governor's Court");
            SupportRequire(court is not null, "Governor's Court was not on the table");
            SelectCard(court!); await EventFrames(4);
            SupportRequire(_chosen?.Command?.AbilityId == "governors-court-open" && PerformerStepActive, "Selecting the Court begins its one play at the performer question");
            SupportRequire(StabilizationText(_inspector).Contains("CHOOSE A PERFORMER") && !StabilizationText(_inspector).Contains("Review the performer and target"), "The first question is the performer: " + StabilizationText(_inspector));
            var performerRows = RowsWith("performer_row");
            SupportRequire(performerRows.Length == 2 && performerRows.All(b => !b.Text.StartsWith("✓", StringComparison.Ordinal)), $"Both Courtiers are offered and none is pre-selected, found {performerRows.Length}");
            SupportRequire(StabilizationButtons(_decisionActions).All(b => b.Text != "Review cost choices"), "No separate review button");
            await SupportCapture("-1-performer");
            var firstPerformer = performerRows[0].GetMeta("performer_row").AsString();
            await SupportClick(performerRows[0]); await EventFrames(4);
            SupportRequire(!PerformerStepActive && StabilizationText(_sidebarHeader).Contains("HOW DO YOU PAY?") && _chosen?.AlternativeCost is not null, "Then how to pay: " + StabilizationText(_inspector));
            var costs = StabilizationButtons(_inspector).Where(b => b.Text is "Bow the Courtier" or "Bow this card").ToArray();
            SupportRequire(costs.Length == 2, "Each way to pay is listed once: " + string.Join(" | ", StabilizationButtons(_inspector).Select(b => b.Text)));
            SupportRequire(DecisionButton("Confirm choice").Disabled, "Nothing is chosen for the player");
            await SupportCapture("-2-cost");
            // Back returns to the performer question; a different performer and the other way to pay.
            await SupportClick(DecisionButton("Back to performer")); await EventFrames(4);
            SupportRequire(PerformerStepActive, "Back returns to the performer question");
            await SupportClick(RowsWith("performer_row")[1]); await EventFrames(4);
            var performerCard = FindCard(_chosen!.Command!.PerformerId!)!;
            SupportRequire(performerCard.Id != firstPerformer, "The other Courtier is the performer");
            await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text == "Bow this card")); await EventFrames(3);
            SupportRequire(!DecisionButton("Confirm choice").Disabled, "A chosen way to pay can be confirmed");
            await SupportCapture("-3-confirm");
            var courtCard = court!;
            await SupportClick(DecisionButton("Confirm choice")); await WaitDecisionPresentation(); await EventFrames(6);
            SupportRequire(_chosen is null && FindCard(courtCard.Id)!.Bowed && !FindCard(performerCard.Id)!.Bowed, "Confirming bowed the Court and not the Courtier");

            // 2. Stolen Merchandise: two plays (copy an ability / copy a Gold trait); Holding, then which ability, then confirm.
            await LoadDirectPlay(s =>
            {
                s.Phase = Phase.Action;
                SupportFixtureCard(s, 7494, 0, Zone.Home);                  // Stolen Merchandise
                SupportFixtureCard(s, 7898, 1, Zone.Home);                  // Temples of Gisei Toshi: three abilities
                SupportFixtureCard(s, 10760, 1, Zone.Home);                 // The Turquoise Court: two
                SupportFixtureCard(s, 10265, 1, Zone.Home);                 // Green Lake Dojo: two
            });
            var stolen = FindCardByName("Stolen Merchandise");
            SupportRequire(stolen is not null, "Stolen Merchandise was not on the table");
            SelectCard(stolen!); await EventFrames(4);
            var plays = RowsWith("play_group");
            SupportRequire(plays.Length == 2, $"Stolen Merchandise should list two plays (an ability, a Gold trait), found {plays.Length}: " + string.Join(" | ", StabilizationButtons(_inspector).Select(b => b.Text)));
            var copyAbility = plays.Single(b => !b.Text.Contains("Gold-producing", StringComparison.Ordinal));
            await SupportCapture("-4-stolen-list");
            await SupportClick(copyAbility); await EventFrames(3);
            SupportRequire(StabilizationText(_inspector).Contains("CHOOSE A TARGET") && !StabilizationText(_inspector).Contains("CHOOSE ONE"), "The Holding is chosen first: " + StabilizationText(_inspector));
            var temples = FindCardByName("Temples of Gisei Toshi")!;
            SupportRequire(_chosen?.TargetIds is { Length: 3 } all && all.Contains(temples.Id), $"All three Holdings are legal targets of the one play, found {_chosen?.TargetIds?.Length}");
            ChooseTarget(temples.Id); await EventFrames(3);
            SupportRequire(StabilizationText(_inspector).Contains("CHOOSE ONE"), "Then which ability to copy: " + StabilizationText(_inspector));
            await SupportCapture("-5-stolen-choice");
            var choices = RowsWith("choice_row");
            SupportRequire(choices.Length == 3, $"Temples of Gisei Toshi has three abilities to copy, found {choices.Length}");
            SupportRequire(StabilizationButtons(_decisionActions).All(b => b.Name != "CommitAction" || b.Disabled), "Confirm waits for the choice");
            // Switching Holding offers that Holding's own abilities.
            ChooseTarget(FindCardByName("The Turquoise Court")!.Id); await EventFrames(3);
            SupportRequire(RowsWith("choice_row").Length == 2, "The Turquoise Court has two abilities to copy");
            await SupportClick(RowsWith("choice_row")[1]); await EventFrames(3);
            SupportRequire(_choicePicked && StabilizationText(_inspector).Contains("SUMMARY") && StabilizationText(_inspector).Contains("Use: "), "The choice is in the summary: " + StabilizationText(_inspector));
            SupportRequire(_chosen?.Command is { } carried && carried.TargetId == FindCardByName("The Turquoise Court")!.Id && carried.Choice is { } picked && picked.StartsWith("pe-turquoise-court", StringComparison.Ordinal), "The command carries the Holding and the ability picked");
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Name == "FlowBack")); await EventFrames(3);
            SupportRequire(StabilizationText(_inspector).Contains("CHOOSE ONE") && !_choicePicked, "Back returns to the ability question");
            await SupportClick(RowsWith("choice_row")[0]); await EventFrames(3);
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Name == "CommitAction")); await WaitDecisionPresentation(); await EventFrames(6);
            SupportRequire(_chosen is null && View.Events.TakeLast(6).Any(e => e.Message.Contains("Stolen Merchandise", StringComparison.Ordinal)), "Confirming used Stolen Merchandise: " + string.Join(" | ", View.Events.TakeLast(6).Select(e => e.Message)));

            // 3. Issue #1230: At Any Cost asks the dead Samurai, then how to pay -- full cost, Clan discount, Clan allegiance, or both -- each with its price; the discount recruits him for 2 less.
            CardInstance genshin = null!;
            await LoadDirectPlay(s =>
            {
                BattleSetup(s);
                s.Players[0].Clan = "Crane";
                for (var i = 0; i < 3; i++) SupportFixtureCard(s, "training-marketplace", 0, Zone.Home); // enough Gold for every way to pay (9 with allegiance)
                genshin = SupportFixtureCard(s, 2078, 0, Zone.Dead);         // Doji Genshin: Crane Samurai, 7 Gold, Honor Requirement 6
                SupportFixtureCard(s, 639, 0, Zone.Hand);                    // At Any Cost
            });
            var atAnyCost = View.Players[0].Hand.Single();
            SelectCard(atAnyCost); await EventFrames(4);
            SupportRequire(ActionTextBlocks().Count() == 0 && PlayChoices(atAnyCost).Length == 1, "At Any Cost's ways to pay are one play, not four actions: " + string.Join(" | ", Actions.Where(a => a.CardId == atAnyCost.Id).Select(a => a.Label)));
            SupportRequire(_chosen is not null && _chosen.TargetIds?.Contains(genshin.Id) == true && StabilizationText(_inspector).Contains("CHOOSE A TARGET"), "The dead Samurai is asked first: " + StabilizationText(_inspector));
            ChooseTarget(genshin.Id); await EventFrames(3);
            var ways = RowsWith("choice_row");
            SupportRequire(StabilizationText(_inspector).Contains("Choose how to pay") && ways.Length == 4, $"Then how to pay, four ways, found {ways.Length}: " + string.Join(" | ", ways.Select(b => b.Text)) + $" ;; choiceLabel={_chosen?.ChoiceLabel} target={_chosen?.Command?.TargetId} variants={ChoiceVariants(_chosen!).Length} picked={_choicePicked} offered=" + string.Join(" | ", Actions.Where(a => a.CardId == atAnyCost.Id).Select(a => $"{a.Enabled}/{a.ChoiceLabel}/{string.Join(",", a.TargetIds ?? [])}/{a.Reason}")) + " ;; " + StabilizationText(_inspector));
            SupportRequire(ways.Any(b => b.Text == "Full cost · 7 Gold") && ways.Any(b => b.Text == "Clan discount: 2 less Gold · 5 Gold") && ways.Any(b => b.Text.StartsWith("Clan allegiance", StringComparison.Ordinal) && b.Text.EndsWith("· 9 Gold", StringComparison.Ordinal)),
                "Each way shows its price: " + string.Join(" | ", ways.Select(b => b.Text)));
            SupportRequire(StabilizationButtons(_decisionActions).All(b => b.Name != "CommitAction" || b.Disabled), "Confirm waits for the way to pay");
            await SupportClick(ways.Single(b => b.Text.StartsWith("Clan discount: 2 less", StringComparison.Ordinal))); await EventFrames(3);
            SupportRequire(_chosen?.Command is { ClanDiscount: true, ClanAllegiance: false } discounted && discounted.TargetId == genshin.Id, "The discount is carried with the Samurai");
            foreach (var source in _chosen!.TargetGold![genshin.Id].Payments[0].SourceIds) await SupportClick(_tiles.Single(t => t.Card.Id == source)); // the player bows the suggested holdings on the table
            await EventFrames(3);
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Name == "CommitAction")); await WaitDecisionPresentation(); await EventFrames(6);
            SupportRequire(FindCard(genshin.Id)?.Zone == Zone.Battlefield && View.Events.TakeLast(8).Any(e => e.Message.Contains("paid 5 Gold", StringComparison.Ordinal)),
                "At Any Cost recruited Genshin for 5 Gold: " + string.Join(" | ", View.Events.TakeLast(8).Select(e => e.Message)));

            GD.Print("VARIANT_STEPS_PASS: Governor's Court asks the performer, then how to pay (each way once, nothing pre-selected, Back); Stolen Merchandise lists two plays and asks the Holding, then which ability; At Any Cost asks the dead Samurai, then the Clan discount or allegiance, each with its price.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private CardView? FindCardByName(string name) => View.Players.SelectMany(p => p.Home.Concat(p.Hand)).FirstOrDefault(c => c.Definition?.Name == name);
}
