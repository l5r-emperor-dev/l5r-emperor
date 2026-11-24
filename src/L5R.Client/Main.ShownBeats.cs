using L5R.Core;

namespace L5R.Client;

// Issue #1319 (Mendon's playtest, Scouting the Pass): every card shown to this player is presented on its own, one at a time, through the played-card
// showcase (Main.CardPlays.cs), and held until they press Proceed (or Enter/Esc) whatever the Paused Animation preference says. Three engine records
// carry a shown card, all read from this player's own view so nothing hidden is shown: a card the opponent shows (ShownCards, public), a card of
// another player's hand this player looked at (HandSights, private to them) and a face-down province card this player looked at (ProvinceSights,
// private to them). Only what newly appears is shown: a new match, a reconnect or a change of seat starts from what is already there.
public partial class Main
{
    private string? _shownBeatMatch;
    private int _shownBeatViewer = -1;
    private int _shownBeatSequence;
    private List<(int Owner, string Definition)> _handSightsSeen = [];
    private List<(int Owner, int Province, string Definition)> _provinceSightsSeen = [];

    /// <summary>The cards shown as held beats in this match, in order (the native checks read it).</summary>
    private readonly List<string> _shownBeatsStarted = [];

    private void ObserveShownBeats()
    {
        if (_session.Snapshot is null) return;
        var view = View;
        var hands = view.HandSights ?? [];
        var provinces = view.ProvinceSights ?? [];
        var shows = OpponentShows.OrderBy(s => s.Sequence).ToArray();
        if (_shownBeatMatch != _session.MatchKey || _shownBeatViewer != view.ViewerId)
        {
            if (_shownBeatMatch != _session.MatchKey) _shownBeatsStarted.Clear();
            _shownBeatMatch = _session.MatchKey; _shownBeatViewer = view.ViewerId;
            _shownBeatSequence = shows.LastOrDefault()?.Sequence ?? 0;
            _handSightsSeen = hands.Select(s => (s.OwnerId, s.DefinitionId)).ToList();
            _provinceSightsSeen = provinces.Select(s => (s.OwnerId, s.Province, s.DefinitionId)).ToList();
            return;
        }
        var beats = new List<PlayBeat>();
        foreach (var record in shows.Where(s => s.Sequence > _shownBeatSequence))
        {
            var name = view.Players.Single(p => p.Id == record.ShownByPlayerId).Name;
            beats.Add(new(ShownCardEntry.PrintedCard(record), $"{name} shows {record.Definition.Name} · {record.Context}", false, AlwaysHold: true));
        }
        if (shows.Length > 0) _shownBeatSequence = Math.Max(_shownBeatSequence, shows[^1].Sequence);

        var newHands = Added(hands, _handSightsSeen, s => (s.OwnerId, s.DefinitionId));
        _handSightsSeen = hands.Select(s => (s.OwnerId, s.DefinitionId)).ToList();
        var handReason = LatestOwnEvent(view, "handCardsLooked");
        for (var i = 0; i < newHands.Length; i++)
        {
            var sight = newHands[i];
            var owner = view.Players.Single(p => p.Id == sight.OwnerId).Name;
            beats.Add(new(ShownFace("hand-sight", i, sight.Definition, sight.OwnerId), (handReason ?? $"You look at a card in {owner}'s hand.") + Count(i, newHands.Length), false, AlwaysHold: true));
        }

        var newProvinces = Added(provinces, _provinceSightsSeen, s => (s.OwnerId, s.Province, s.DefinitionId));
        _provinceSightsSeen = provinces.Select(s => (s.OwnerId, s.Province, s.DefinitionId)).ToList();
        var provinceReason = LatestOwnEvent(view, "provincesLooked", "provinceLooked", "provinceCardsSwitched");
        for (var i = 0; i < newProvinces.Length; i++)
        {
            var sight = newProvinces[i];
            var owner = sight.OwnerId == view.ViewerId ? "your" : view.Players.Single(p => p.Id == sight.OwnerId).Name + "'s";
            var where = $"Face down in {owner} province {sight.Province + 1}";
            beats.Add(new(ShownFace("province-sight", i, sight.Definition, sight.OwnerId), (provinceReason is null ? where : provinceReason + "\n" + where) + Count(i, newProvinces.Length), false, AlwaysHold: true));
        }

        if (beats.Count == 0) return;
        // The history panel keeps every opponent show; the held card is how it arrives, so the panel is not also forced open (Main.ShownCards.cs).
        MarkShownRead();
        foreach (var beat in beats) { _shownBeatsStarted.Add(beat.Card.Definition!.Id); _playQueue.Enqueue(beat); }
        if (_playCurrent is null || !IsInstanceValid(_playCurrent)) StartNextCardPlay();
    }

    // The entries of now that were not there before, counting repeats: two copies of one card seen in a hand are two cards.
    private static T[] Added<T, TKey>(IEnumerable<T> now, IEnumerable<TKey> before, Func<T, TKey> key) where TKey : notnull
    {
        var remaining = before.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        var added = new List<T>();
        foreach (var item in now)
        {
            if (remaining.TryGetValue(key(item), out var left) && left > 0) remaining[key(item)] = left - 1;
            else added.Add(item);
        }
        return added.ToArray();
    }

    private static string Count(int index, int total) => total > 1 ? $" · {index + 1} of {total}" : "";

    private static string? LatestOwnEvent(MatchView view, params string[] types) =>
        view.Events.LastOrDefault(e => e.PlayerId == view.ViewerId && types.Contains(e.Type))?.Message;

    private CardView ShownFace(string kind, int index, CardDefinition definition, int ownerId) =>
        new($"{kind}-{View.Version}-{index}", definition, ownerId, Zone.Hand, true, false, null, null, definition.Force, definition.Chi);
}
