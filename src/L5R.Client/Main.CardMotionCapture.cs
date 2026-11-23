using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

// Native probe for the card showcase on Events-Phase reveals ("Flip Dynasty Cards", Events/Regions/Celestials entering play, and an opponent's purchase).
// Real engine events drive every beat: the opponent's provinces hold a Region, a Personality, a Celestial and Harsh Choices, and ending the viewer's
// Dynasty Phase starts the opponent's turn. Mode: published-card-motion.
public partial class Main
{
    private bool _motionCaptureStarted;
    private void ProcessCardMotionCapture(double delta)
    {
        _captureClock += delta; if (_motionCaptureStarted || _captureClock < 1.5) return;
        _motionCaptureStarted = true; _ = CaptureCardMotion();
    }
    private async Task<string[]> PrepareRevealFixture()
    {
        var state = SupportFixture().State; state.Phase = Phase.Dynasty; state.Players[0].DynastyActionTaken = true;
        var ids = new[] { SupportFixtureCard(state, 4777, 1, Zone.Province, 0).Id, SupportFixtureCard(state, 10292, 1, Zone.Province, 1).Id,
            SupportFixtureCard(state, 4658, 1, Zone.Province, 2).Id, SupportFixtureCard(state, 2978, 1, Zone.Province, 3).Id };
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        _playsStarted.Clear();
        await _session.Submit(Actions.Single(a => a.Type == "endDynasty" && a.Enabled).Command!);
        return ids;
    }
    // Know Their Minds (a player report: "a Personality is discarded instead of turned face down"): the engine leaves it face-down in its province, but the view
    // replaces its id with the province's hidden card, and the table motion used to read that as a card returned to the Dynasty deck and fly its face there.
    private async Task CaptureTurnedFaceDown()
    {
        var state = SupportFixture().State;
        foreach (var province in state.Players[1].Provinces) if (province.CardId is { } hidden) state.Cards[hidden].FaceUp = false;
        var target = SupportFixtureCard(state, "training-lion-samurai", 1, Zone.Province, 0); target.FaceUp = true;
        SupportFixtureCard(state, "oracle-l5r-4487", 0, Zone.Hand);
        var samurai = SupportFixtureCard(state, "training-crane-samurai", 0, Zone.Home); samurai.GrantedKeywords.Add(new("Samurai", "card-motion-fixture", GrantDuration.Permanent));
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        var option = Actions.First(a => a.Enabled && a.Command?.AbilityId == "know-their-minds-choose" && a.Command.PerformerId == samurai.Id);
        await _session.Submit(option.Command! with { TargetPlayerId = 1 });
        await WaitFor(() => _flights.Count > 0, "Turned face-down: no table motion", 10);
        SupportRequire(View.Players[1].Provinces[0].Card is { Definition: null, FaceUp: false }, "Turned face-down: the province does not show a face-down card");
        SupportRequire(!VisibleCards(View).Any(c => c.Id == target.Id), "Turned face-down: the card is still visible");
        var deck = PileCorners(TablePiles.First(p => p.OwnerId == 1 && p.PileZone == Zone.DynastyDeck));
        SupportRequire(_flights.All(f => f.Destination()[0].DistanceTo(deck[0]) > 1), "Turned face-down: a card flew to the Dynasty deck");
        SupportRequire(_flights.Any(f => f.Face == TablePile.DynastyBack && f.Back is not null && f.Destination()[0].DistanceTo(f.From[0]) < 1), "Turned face-down: no flip in place");
        await EventFrames(10); await SupportCapture("-turned-face-down");
    }
    private (string CardId, bool Flip)[] StartedAmong(string[] ids) => _playsStarted.Where(b => ids.Contains(b.CardId)).ToArray();
    private async Task CaptureCardMotion()
    {
        try
        {
            _preferences.PausedAnimation = false;
            // Flip Off (the default): only the Region, Celestial and Event that enter play are shown; the Personality turns over unseen.
            _preferences.ReducedMotion = false; _preferences.FlipDynastyCards = false; ApplyPreferences(false);
            var ids = await PrepareRevealFixture();
            await WaitFor(() => StartedAmong(ids).Length == 3 && !PlayShowcaseBusy, "Flip Off: Region/Celestial/Event were not all shown in turn", 30);
            var off = StartedAmong(ids);
            SupportRequire(off.Select(b => b.CardId).SequenceEqual([ids[0], ids[2], ids[3]]) && off.All(b => !b.Flip), "Flip Off: wrong cards or a flip was shown");

            // Flip On: every revealed card turns over, one at a time, in province order.
            _preferences.FlipDynastyCards = true;
            ids = await PrepareRevealFixture();
            await WaitFor(() => StartedAmong(ids).Length >= 1 && _playCurrent is CardFlip, "Flip On: no flip was drawn on the table", 10);
            SupportRequire(_playsStarted.Count(b => b.CardId == _playsStarted[^1].CardId) == 1, "Flip On: a card was shown twice");
            SupportRequire(_root.GetChildren().OfType<CardFlip>().Count() >= 3, "Flip On: the face-down cards were not all covered on the table");
            await EventFrames(8); await SupportCapture("-flip-mid");
            var seen = 0;
            for (var t = 0.0; t < 40 && (StartedAmong(ids).Length < 4 || PlayShowcaseBusy); t += 1.0 / 20)
            {
                SupportRequire(_playCurrent is null || GetChildren().Count(c => c is Control && c == _playCurrent) <= 1, "Two cards were shown at once");
                seen = Math.Max(seen, _playQueue.Count); await EventFrames(3);
            }
            var on = StartedAmong(ids);
            SupportRequire(on.Select(b => b.CardId).SequenceEqual(ids) && on.All(b => b.Flip), $"Flip On: expected all four in province order flipped, got {string.Join(",", on.Select(b => b.CardId))}");
            SupportRequire(seen >= 1, "Flip On: beats were not queued behind one another");

            // Paused Animation holds an Event/Region/Celestial entering play, but never the flip of a card that stays in its province.
            _preferences.PausedAnimation = true;
            ids = await PrepareRevealFixture();
            await WaitFor(() => ProceedButton is not null, "Paused: the Region entering play was not held", 15);
            SupportRequire(_playsStarted[^1].CardId == ids[0], "Paused: first held card is not the Region");
            PausedKey(Key.Enter);
            await WaitFor(() => _playsStarted.Any(b => b.CardId == ids[1]), "Paused: the Personality flip did not follow", 15);
            await EventFrames(30);
            SupportRequire(ProceedButton is null, "Paused: a flip of a card that stays in its province was held");
            await WaitFor(() => ProceedButton is not null && _playsStarted[^1].CardId == ids[2], "Paused: the Celestial was not held", 15);
            PausedKey(Key.Enter);
            await WaitFor(() => ProceedButton is not null && _playsStarted[^1].CardId == ids[3], "Paused: the Event was not held", 15);
            PausedKey(Key.Enter);
            await WaitFor(() => !PlayShowcaseBusy && !_playHeld, "Paused: showcase did not finish", 15);
            _preferences.PausedAnimation = false;

            // Reduced motion: no turning at all; only the Region, Celestial and Event entering play are shown.
            _preferences.ReducedMotion = true; ApplyPreferences(false);
            ids = await PrepareRevealFixture();
            await WaitFor(() => _playCurrent is not null, "Reduced: nothing shown", 10);
            await WaitFor(() => StartedAmong(ids).Length == 3 && !PlayShowcaseBusy, "Reduced: Region/Celestial/Event not all shown", 30);
            SupportRequire(!_root.GetChildren().OfType<CardFlip>().Any() && StartedAmong(ids).All(b => !b.Flip), "Reduced: a flip was drawn");
            _preferences.ReducedMotion = false; _preferences.FlipDynastyCards = false; ApplyPreferences(false);
            await CaptureTurnedFaceDown();
            GD.Print("CARD_MOTION_PASS: Flip Off shows only entering Event/Region/Celestial; Flip On turns every revealed card over on the table, in turn; Paused holds entries not flips; reduced motion skips the turn; a Personality turned face-down flips in its province instead of flying to the Dynasty deck.");
            GetTree().Quit();
        }
        catch (Exception e) { try { await SupportCapture("-failure"); } catch { } GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
