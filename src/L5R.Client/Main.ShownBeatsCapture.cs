using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

// Issue #1319: the held "card shown to you" beats, driven by real engine plays. Run from the shown-card check (tools/check-shown-cards.sh) after its
// history checks: Scouting the Pass (two cards of the Defender's hand, the case from Mendon's playtest) and The Otoro Estate (face-down province cards).
public partial class Main
{
    private static MatchEngine ShownBeatEngine(MatchState state) =>
        (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;

    // Engaged at the Defender's province 0, with the Engage window open: the Attacker (seat 1, the viewer) holds Scouting the Pass and an unbowed Scout there.
    private static MatchEngine CreateScoutingFixture()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        SupportFixtureCard(s, "training-scout-samurai", 1, Zone.Battlefield, 0);
        SupportFixtureCard(s, 6513, 1, Zone.Hand);
        SupportFixtureCard(s, "training-lion-samurai", 0, Zone.Battlefield, 0);
        foreach (var definition in new[] { "training-crane-samurai", "training-lion-samurai", "training-follower" }) SupportFixtureCard(s, definition, 0, Zone.Hand);
        var engine = ShownBeatEngine(s);
        engine.Execute(new GameCommand("selectBattle", 1, engine.Version, Province: 0));
        return engine;
    }

    /// <summary>Each newly shown card is held, one at a time, until Proceed; nothing else moves on meanwhile. Returns the captions in order.</summary>
    private async Task<string[]> ProceedThroughShownBeats(int expected, string what, string capture)
    {
        var captions = new List<string>();
        for (var i = 0; i < expected; i++)
        {
            await WaitFor(() => ProceedButton is { Visible: true }, $"{what}: card {i + 1} of {expected} was not held", 12);
            await EventFrames(10);
            SupportRequire(_playHeld && Actions.Length == 0, $"{what}: local input was not held while a shown card was on screen");
            captions.Add(_playCurrent!.GetChildren().OfType<Label>().First(l => l.Name != "ProceedHint").Text);
            if (i == 0) await SaveShownCapture(capture);
            await EventFrames(_reducedMotion ? 60 : 120); // longer than any timed hold: a shown card is not dismissed by the clock
            SupportRequire(ProceedButton is { Visible: true } && _playHeld, $"{what}: a shown card was dismissed without a click");
            await PausedMouse(ProceedButton!.GetGlobalRect().GetCenter(), true);
            await WaitFor(() => ProceedButton is not { Visible: true }, $"{what}: Proceed did not dismiss card {i + 1}");
        }
        await WaitFor(() => !_playHeld && !PlayShowcaseBusy, $"{what}: input stayed held after the last shown card");
        return captions.ToArray();
    }

    private async Task CaptureShownBeats()
    {
        // Held even with Paused Animation off, and without its Settings reminder.
        _preferences.PausedAnimation = false; ApplyPreferences(false);

        // Scouting the Pass: the Attacker looks at two cards of the Defender's hand.
        var scouting = CreateScoutingFixture();
        await LoadSupportFixture(scouting, 1); await EventFrames(10);
        if (scouting.State.PriorityPlayerId != 1) await _session.Submit(new GameCommand("declineReaction", scouting.State.PriorityPlayerId, scouting.Version));
        var play = scouting.QueryActions(1).First(a => a.Enabled && a.Command?.AbilityId == "p5-scouting-pass" && a.Command.PerformerId is not null);
        var started = _shownBeatsStarted.Count;
        await _session.Submit(play.Command!);
        SupportRequire(View.HandSights?.Length == 2, "Scouting the Pass: the engine did not show two cards");
        var captions = await ProceedThroughShownBeats(2, "Scouting the Pass", "-scouting");
        SupportRequire(_shownBeatsStarted.Skip(started).Order().SequenceEqual(View.HandSights!.Select(s => s.DefinitionId).Order()), "Scouting the Pass: the held cards are not the cards seen");
        SupportRequire(captions.All(c => c.Contains("Scouting the Pass")) && captions[0].EndsWith("1 of 2") && captions[1].EndsWith("2 of 2"), "Scouting the Pass: captions do not name the card and count: " + string.Join(" | ", captions));
        SupportRequire(!_proceedHintShown, "The Paused Animation reminder appeared for a shown card");
        SupportRequire(!_shownOpen && OpponentShows.Length == 0, "A private look was put in the public shown-card history");

        // The Otoro Estate: the viewer looks at the opponent's face-down province cards.
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        state.Cards[state.Players[0].StrongholdId].DefinitionId = "oracle-l5r-8246";
        foreach (var province in state.Players[1].Provinces.Take(2)) if (province.CardId is { } hidden) state.Cards[hidden].FaceUp = false; // two face-down cards to look at
        var otoro = ShownBeatEngine(state);
        await LoadSupportFixture(otoro, 0); await EventFrames(10);
        var look = otoro.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "otoro-look" && a.Command.TargetPlayerId == 1);
        started = _shownBeatsStarted.Count;
        await _session.Submit(look.Command!);
        var seen = View.ProvinceSights?.Length ?? 0;
        SupportRequire(seen > 0, "The Otoro Estate: the engine showed no face-down card");
        captions = await ProceedThroughShownBeats(seen, "The Otoro Estate", "-province");
        SupportRequire(_shownBeatsStarted.Count - started == seen && captions.All(c => c.Contains("Face down in") && c.Contains("province")), "The Otoro Estate: captions do not say where each card lies: " + string.Join(" | ", captions));

        // Nothing is shown again when the same view is drawn again, after a seat switch or for a new match.
        Render(); await EventFrames(10);
        SupportRequire(!PlayShowcaseBusy && _shownBeatsStarted.Count - started == seen, "A shown card was shown twice");
        GD.Print($"SHOWN_BEATS_PASS: Scouting the Pass held 2 hand cards; The Otoro Estate held {seen} province cards; each waited for a click with Paused Animation off");
    }
}
