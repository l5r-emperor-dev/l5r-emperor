using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

// Issue #1114 native probe (mode published-supports-doji): with nothing to return, Doji's Guidance's sidebar says why; then its Open is used the way a player uses it -- click
// the Celestial, choose its Open, confirm the Strategy from the discard pile -- and the engine returns that Strategy to the hand.
public partial class Main
{
    private async Task CheckDojisGuidance()
    {
        // Nothing to return yet: the sidebar says why, without hovering.
        var empty = SupportFixture().State; empty.Players[0].Clan = "Crane";
        foreach (var old in empty.Cards.Values.Where(c => c.Zone == Zone.Celestial && c.OwnerId == 0)) old.Zone = Zone.DynastyDiscard;
        var idle = SupportFixtureCard(empty, 2181, 0, Zone.Celestial);
        await LoadSupportFixture((MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [empty])!, 0);
        await SupportClick(_tiles.Single(t => t.Card.Id == idle.Id));
        const string why = "Your Fate discard pile has no non-Unique Strategy with a Political ability.";
        SupportRequire(_chosen is null && _inspector.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == why), "The disabled Open does not show its reason in the sidebar");
        await SupportCapture("-doji-unavailable");
        DeselectCard(); await EventFrames(6);
        var state = SupportFixture().State;
        state.Players[0].Clan = "Crane";
        foreach (var old in state.Cards.Values.Where(c => c.Zone == Zone.Celestial && c.OwnerId == 0)) old.Zone = Zone.DynastyDiscard;
        var doji = SupportFixtureCard(state, 2181, 0, Zone.Celestial);
        var courtesy = SupportFixtureCard(state, 7996, 0, Zone.FateDiscard);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        await SupportClick(_tiles.Single(t => t.Card.Id == doji.Id));
        SupportRequire(_selected?.Id == doji.Id, "Doji's Guidance did not select");
        if (_chosen is null) // a Celestial with one available action opens its draft on the click; otherwise its sidebar offers the action
        {
            var use = StabilizationButtons(_inspector).FirstOrDefault(b => b.Text.StartsWith("Use Open ability", StringComparison.Ordinal));
            SupportRequire(use is { Disabled: false }, "Doji's Guidance offers no enabled Open button: " + string.Join(", ", StabilizationButtons(_inspector).Select(b => $"{b.Text}[{(b.Disabled ? "off" : "on")}]")));
            await SupportClick(use!);
        }
        SupportRequire(_chosen?.Command?.AbilityId == "dojis-guidance-return", "The Open button did not open the action");
        await SupportCapture("-doji-confirm");
        var confirm = StabilizationButton("Confirm action");
        SupportRequire(!confirm.Disabled, "Confirm action is disabled");
        await SupportClick(confirm); await EventFrames(40);
        SupportRequire(engine.State.Cards[courtesy.Id].Zone == Zone.Hand, $"The Strategy did not return to the hand (it is in {engine.State.Cards[courtesy.Id].Zone}); notice: {_notice.Text}");
        SupportRequire(engine.State.Cards[doji.Id].Zone == Zone.DynastyDiscard, "Doji's Guidance was not discarded as the cost");
        await SupportCapture("-doji-used");
        GD.Print("SUPPORT_PASS: Doji's Guidance Open returned the Political Strategy to the hand through the native controls");
    }
}
