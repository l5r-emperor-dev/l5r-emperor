namespace L5R.Core;

// Issue #1206 (battle lookahead): a copy of the match that one player could believe in. A bot that searches ahead plays its candidate actions out on these copies,
// so it may use the real engine to resolve every card without ever reading what it cannot see: each card hidden from it is redealt from the pool of cards
// that could be there, and only the card identities move (ids, zones, counts and every public fact stay as they are).
public sealed partial class MatchEngine
{
    /// <summary>A detached copy in which every card hidden from <paramref name="viewerId"/> is redealt: the other player's hand with their Fate deck, each
    /// Dynasty deck with its owner's face-down Province cards, and each Fate deck (its order is hidden from both players), with a fresh shuffle generator.
    /// Not replayable and never shown to a player; it exists only to be played forward.</summary>
    public MatchEngine Sample(int viewerId, ulong seed)
    {
        var sample = Clone();
        var state = sample._state;
        var random = new StableRandom(seed);
        foreach (var player in state.Players)
        {
            Redeal(state, player.Id == viewerId ? player.FateDeck : [.. player.FateDeck, .. player.Hand], random);
            Redeal(state, [.. player.DynastyDeck, .. state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Province && !c.FaceUp).Select(c => c.Id)], random);
        }
        state.RngState = random.State; // the copy's later shuffles continue from the redeal, not from the real match's generator
        return sample;
    }

    private static void Redeal(MatchState state, IReadOnlyList<string> ids, StableRandom random)
    {
        var faces = ids.Select(id => (state.Cards[id].DefinitionId, state.Cards[id].Printed)).ToList();
        random.Shuffle(faces);
        for (var i = 0; i < ids.Count; i++) (state.Cards[ids[i]].DefinitionId, state.Cards[ids[i]].Printed) = faces[i];
    }
}
