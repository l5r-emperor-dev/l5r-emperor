using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Part of SecondCityBattleTests: Discovering a Conspiracy, Insulting Gesture, Doji Dainagon and Barred Passage (the Political and Favor cards).
internal static partial class SecondCityBattleTests
{
    // ---- Discovering a Conspiracy -------------------------------------------------------------------------------------------------------------------
    private static void DiscoveringAConspiracy()
    {
        // Open (the Action phase): a Magistrate at home names a Personality at home -- no battlefield, so no bow and no question.
        var s = Table(Phase.Action, 0);
        var magistrate = Add(s, "training-magistrate-personality", 0, Zone.Home);
        var card = Add(s, Conspiracy, 0, Zone.Hand);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var bystander = Add(s, "training-lion-courtier", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(target.Id, "Unaligned"), "he has a Clan");
        var option = Enabled(e, 0, "discovering-conspiracy-expose", card.Id, magistrate.Id);
        Check(option.TargetIds!.Contains(target.Id) && option.TargetIds.Contains(magistrate.Id), "any Personality, either side's");
        Accept(e, Offer(e, 0, "discovering-conspiracy-expose", card.Id, target.Id, magistrate.Id));
        Check(e.HasKeyword(target.Id, "Unaligned") && e.State.Cards[target.Id].RemovedClanAffiliations.Contains("Lion"), "his Clan alignment is gone for good");
        Check(e.State.Cards[target.Id].Dishonorable, "dishonored");
        Check(!e.State.Cards[target.Id].Bowed && e.State.PendingChoice is null, "at home: not bowed, and no choice is asked");
        Check(!e.State.Cards[bystander.Id].Dishonorable && e.State.Cards[bystander.Id].RemovedClanAffiliations.Count == 0, "nobody else is touched");
        Check(e.State.Events.Any(x => x.Type == "clanAlignmentRemoved"), "logged");

        // Battle: at the battlefield he is also bowed, and the acting player chooses a player who gains or loses 1 Honor.
        foreach (var (choice, self, gain) in new[] { ("self-gain", true, 1), ("opponent-gain", false, 1), ("self-lose", true, -1), ("opponent-lose", false, -1) })
        {
            var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
            var mag = Add(b, "training-magistrate-personality", 0, Zone.Battlefield, province: 0);
            var c2 = Add(b, Conspiracy, 0, Zone.Hand);
            var t2 = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var eb = MatchEngine.FromTestState(b);
            var (mine, theirs) = (Honor(eb, 0), Honor(eb, 1));
            Accept(eb, Offer(eb, 0, "discovering-conspiracy-expose", c2.Id, t2.Id, mag.Id));
            Check(eb.State.Cards[t2.Id].Bowed && eb.State.Cards[t2.Id].Dishonorable && eb.HasKeyword(t2.Id, "Unaligned"), "bowed, dishonored and unaligned");
            Eq(eb.State.PendingChoice?.Type, "effectChoice", "the player choice is asked at resolution");
            var offered = eb.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).OrderBy(x => x).ToArray();
            Check(offered.SequenceEqual(new[] { "opponent-gain", "opponent-lose", "self-gain", "self-lose" }), "the four answers: " + string.Join(",", offered));
            Accept(eb, eb.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == choice).Command!);
            var expected = self ? (mine + gain, theirs) : (mine, theirs + gain);
            Check((Honor(eb, 0), Honor(eb, 1)) == expected, $"{choice}: Honor is now {Honor(eb, 0)}/{Honor(eb, 1)}, expected {expected.Item1}/{expected.Item2}");
        }
        // A Stalwart target's bow is negated but the question is still asked (he is at a battlefield). Only a Magistrate may perform.
        var st = Table(Phase.Battle, 0); st.PriorityPlayerId = 0;
        var m3 = Add(st, "training-magistrate-personality", 0, Zone.Battlefield, province: 0); var other = Add(st, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var c3 = Add(st, Conspiracy, 0, Zone.Hand); var stalwart = Add(st, "training-stalwart-personality", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(st);
        Check(!CanUse(e3, 0, "discovering-conspiracy-expose", c3.Id, other.Id), "a Personality who is not a Magistrate cannot perform");
        Accept(e3, Offer(e3, 0, "discovering-conspiracy-expose", c3.Id, stalwart.Id, m3.Id));
        Check(!e3.State.Cards[stalwart.Id].Bowed && e3.State.PendingChoice?.Type == "effectChoice", "Stalwart negates the bow, the choice is still asked");
    }

    private static void ConspiracyFocusEffect()
    {
        foreach (var challengerWins in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            // Player 0 challenges with Chi 4 (win) or Chi 3 (loss) and focuses the Strategy (Focus 4); player 1 answers with Chi 4 or 5 and a Focus 3 card off his deck: 8 v 7, or 7 v 8.
            var mine = Add(s, challengerWins ? "training-crane-veteran" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var theirs = Add(s, challengerWins ? "training-lion-veteran" : "training-paneki-exp3", 1, Zone.Battlefield, province: 0);
            var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
            var conspiracy = Add(s, Conspiracy, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, 0);
            Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: conspiracy.Id));
            Accept(e, new GameCommand("strike", 1, e.Version));
            Eq(e.State.Cards[conspiracy.Id].Zone, Zone.FateDiscard, "the focused card is spent");
            var challengerWon = e.State.Cards[theirs.Id].Zone == Zone.Dead;
            Eq(challengerWon, challengerWins, challengerWins ? "8 against 7: his Personality wins" : "7 against 8: his Personality loses");
            Eq(Honor(e, 0), challengerWon ? honor + 2 : honor, challengerWon ? "he won: +2 Honor" : "he lost: nothing");
        }
    }

    // ---- Insulting Gesture -------------------------------------------------------------------------------------------------------------------------
    private static void InsultingGesture_()
    {
        // Take the Favor: Political Open, performed by an unbowed Courtier.
        var s = Table(Phase.Action, 0);
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home); GiveKeyword(courtier, "Courtier");
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var bowedCourtier = Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true); GiveKeyword(bowedCourtier, "Courtier");
        var card = Add(s, InsultingGesture, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.State.FavorPlayerId is null, "nobody holds the Favor");
        Check(!CanUse(e, 0, "insulting-gesture-take", card.Id, samurai.Id) && !CanUse(e, 0, "insulting-gesture-take", card.Id, bowedCourtier.Id), "only an unbowed Courtier performs it");
        Accept(e, Enabled(e, 0, "insulting-gesture-take", card.Id, courtier.Id).Command!);
        Eq(e.State.FavorPlayerId, 0, "the Imperial Favor is his controller's now");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");

        // The Favor half: not offered without the Favor; with it, the Favor is discarded and the target dishonored, bowed and sent home, and its controller loses 2 Honor.
        MatchEngine Battle(int? favorHolder, out CardInstance card2, out CardInstance target)
        {
            var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
            Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            card2 = Add(b, InsultingGesture, 0, Zone.Hand);
            target = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            b.FavorPlayerId = favorHolder;
            return MatchEngine.FromTestState(b);
        }
        var without = Battle(null, out var c0, out _);
        var offers = Options(without, 0, "insulting-gesture-shame", c0.Id);
        Check(offers.Length > 0 && offers.All(o => !o.Enabled) && offers.Any(o => o.Reason!.Contains("Imperial Favor")), "without the Favor it is refused: " + string.Join("; ", offers.Select(o => o.Reason)));
        var with = Battle(0, out var c1, out var target);
        var theirs = Honor(with, 1);
        Accept(with, Offer(with, 0, "insulting-gesture-shame", c1.Id, target.Id));
        Check(with.State.FavorPlayerId is null, "the Favor was discarded");
        Check(with.State.Cards[target.Id].Dishonorable && with.State.Cards[target.Id].Bowed, "dishonored and bowed");
        Eq(with.State.Cards[target.Id].Zone, Zone.Home, "and sent home -- still bowed");
        Eq(Honor(with, 1), theirs - 2, "his controller loses 2 Honor");
        // The opponent's Favor is not yours to discard.
        var theirFavor = Battle(1, out var c2, out _);
        Check(!CanUse(theirFavor, 0, "insulting-gesture-shame", c2.Id), "an opponent's Favor cannot be discarded");
    }

    // ---- Doji Dainagon -------------------------------------------------------------------------------------------------------------------------------
    private static void DainagonLobbies()
    {
        // Player 0 has the higher Family Honor: Dainagon (Open, in the Action phase, even on the opponent's turn) bows and takes the Favor.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        s.Players[0].Honor = 10; s.Players[1].Honor = 5;
        var dainagon = Add(s, Dainagon, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var lobby = Enabled(e, 0, "dainagon-lobby", dainagon.Id);
        Accept(e, lobby.Command!);
        Eq(e.State.FavorPlayerId, 0, "he took the Imperial Favor"); Check(e.State.Cards[dainagon.Id].Bowed, "and bowed as the cost");
        // Not with an equal or lower Family Honor, nor while bowed.
        var low = Table(Phase.Action, 0); low.Players[0].Honor = 5; low.Players[1].Honor = 5; Add(low, Dainagon, 0, Zone.Home);
        var el = MatchEngine.FromTestState(low);
        var refused = Options(el, 0, "dainagon-lobby");
        Check(refused.Length > 0 && refused.All(o => !o.Enabled) && refused.Any(o => o.Reason!.Contains("Family Honor")), "an equal Family Honor is not enough: " + string.Join("; ", refused.Select(o => o.Reason)));
        var bowed = Table(Phase.Action, 0); bowed.Players[0].Honor = 10; Add(bowed, Dainagon, 0, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(bowed), 0, "dainagon-lobby"), "a bowed Dainagon cannot lobby");
        // R-098 (Mendon, 2026-11-18): "Dainagon may Lobby as an Open action" is the player's one Lobby taken at another time, so the two share Lobby's once-per-turn use, in either order.
        var both = Table(Phase.Action, 0); both.Players[0].Honor = 10; both.Players[1].Honor = 5;
        both.Cards[both.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";   // player abilities (Lobby) are offered only with a published Stronghold
        var d2 = Add(both, Dainagon, 0, Zone.Home); var samurai = Add(both, "training-crane-samurai", 0, Zone.Home);
        var eb = MatchEngine.FromTestState(both);
        Accept(eb, Enabled(eb, 0, "dainagon-lobby", d2.Id).Command!);
        var again = Edit(eb, st => { st.PriorityPlayerId = 0; st.FavorPlayerId = null; });
        Check(!CanUse(again, 0, "lobby"), "his Open Lobby spent the player's Lobby for the turn: " + string.Join("; ", again.QueryActions(0).Where(o => o.Command?.AbilityId == "lobby").Select(o => o.Label + " [" + o.Enabled + "] " + o.Reason)));
        var first = MatchEngine.FromTestState(both);
        var limited = Enabled(first, 0, "lobby");
        Accept(first, (limited.Command! with { TargetId = samurai.Id }));
        var after = Edit(first, st => { st.PriorityPlayerId = 0; st.FavorPlayerId = null; });
        var dainagonAfter = Options(after, 0, "dainagon-lobby");
        Check(dainagonAfter.Length > 0 && dainagonAfter.All(o => !o.Enabled) && dainagonAfter.Any(o => o.Reason == "You have already Lobbied this turn."), "after the Limited Lobby, Dainagon may not Lobby the same turn: " + string.Join("; ", dainagonAfter.Select(o => o.Reason)));
        var nextTurn = Edit(first, st => { st.PriorityPlayerId = 0; st.FavorPlayerId = null; st.TurnNumber++; st.ActivePlayerId = 1; });
        Check(CanUse(nextTurn, 0, "dainagon-lobby"), "on the next turn (the opponent's) Dainagon may Lobby again");
    }

    // ---- Barred Passage ---------------------------------------------------------------------------------------------------------------------------------
    private static void BarredPassage_()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var hero = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(hero, "Hero");
        var card = Add(s, BarredPassage, 0, Zone.Hand);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: target.Id);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: target.Id);
        var bystander = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "barred-passage-bow", card.Id, target.Id, hero.Id));
        Check(e.State.Cards[target.Id].Bowed && e.State.Cards[item.Id].Bowed && e.State.Cards[follower.Id].Bowed, "the Personality and every card attached to him are bowed");
        Check(!e.State.Cards[bystander.Id].Bowed, "another unit is not");
        // Not alone: a second Personality of the Hero's in his army refuses it. Not a Hero: refused.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var hero2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(hero2, "Hero");
        Add(s2, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var card2 = Add(s2, BarredPassage, 0, Zone.Hand); Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var options = Options(e2, 0, "barred-passage-bow", card2.Id, hero2.Id);
        Check(options.Length > 0 && options.All(o => !o.Enabled) && options.Any(o => o.Reason!.Contains("only Personality in his army")), "with a second Personality beside him: " + string.Join("; ", options.Select(o => o.Reason)));
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var plain = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var card3 = Add(s3, BarredPassage, 0, Zone.Hand); Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "barred-passage-bow", card3.Id, plain.Id), "a performer who is not a Hero cannot use it");
        // His army counts only the acting player's own Personalities at THIS battlefield: one at home does not spoil it.
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var hero4 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(hero4, "Hero");
        Add(s4, "training-crane-courtier", 0, Zone.Home); var card4 = Add(s4, BarredPassage, 0, Zone.Hand); Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(CanUse(MatchEngine.FromTestState(s4), 0, "barred-passage-bow", card4.Id, hero4.Id), "a Personality at home is not in his army");
    }
}
