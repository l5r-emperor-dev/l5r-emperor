using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #503, pass group P5 (EmperorP5Hidden.cs): face-down province cards by place, a private look at a hand, a Personality put into play from a look, a copied Holding trait, a borrowed Strategy.
internal static partial class EmperorP5Tests
{
    private const string Petty = "oracle-l5r-5939", Searching = "oracle-l5r-6526", Scouting = "oracle-l5r-6513", Stolen = "oracle-l5r-7494", Learning = "oracle-l5r-10155";

    private static readonly (string, Action)[] HiddenTests =
    [
        ("Published shape (hidden): Petty Squabbles, Searching for Answers, Scouting the Pass and Learning (strategy-wave-98), Stolen Merchandise (holding-wave-45)", HiddenPublishedShape),
        ("Petty Squabbles, Limited: face-down province cards are offered by their place (never their identity) in sets of one or two, either player's; turned face-up, a Celestial enters play", PettyReveal),
        ("Petty Squabbles, Battle/Open: the owner of the targeted attachment may pay 4 Gold (it stays) or not (it returns to his hand); his own attachment is not a target", PettyReturn),
        ("Searching for Answers: the Defender looks at the top four of his Dynasty deck and may put a Personality into play at the battlefield ignoring his Gold Cost, the rest back; he is discarded after the battle; not for the Attacker; its Focus Effect gains 1 Honor for the winner only", SearchingCard),
        ("Scouting the Pass: after engaging, the Attacker's Scout gives Reconnaissance, two cards of the Defender's hand become visible to him alone, and his first Battle action must be performed by that Scout; not for the Defender", ScoutingCard),
        ("Stolen Merchandise: copies, for the turn, another player's Holding's base ability (usable by it) or its Gold-producing trait (it then produces that Gold); a Holding of its own controller or costing 5 or more is not a target", StolenCard),
        ("Learning: a Battle Strategy in the other player's discard pile gets a second use, the performer copies a keyword of an enemy Personality, and the player may play that Strategy with an additional action -- it is removed from the game after it resolves; declined, it goes back to its owner", LearningCard),
    ];

    private static void HiddenPublishedShape()
    {
        Card(Petty, CardType.Strategy, "Petty Squabbles", "p5-petty-reveal,p5-petty-return");
        Card(Searching, CardType.Strategy, "Searching for Answers", "p5-searching-focus,p5-searching-look");
        Card(Scouting, CardType.Strategy, "Scouting the Pass", "p5-scouting-pass");
        Card(Learning, CardType.Strategy, "Learning", "p5-learning");
        Manifest("strategy-wave-98", Headbutt, Disrupting, Bite, Econ, Guests, Mistrust, Doubts, Riddle, TwoFold, PriceOfHonor, Petty, Searching, Scouting, Learning);
        Card(Stolen, CardType.Holding, "Stolen Merchandise", "p5-stolen-ability,p5-stolen-gold");
        Eq(CardCatalog.Get(Stolen).GoldCost, 2, "Stolen Merchandise costs 2");
        Manifest("holding-wave-45", Stolen);
    }

    private static void PettyReveal()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var petty = Add(s, Petty, 0, Zone.Hand);
        var celestial = FaceDownIn(s, Blessings, 1, 2);
        var ownHidden = FaceDownIn(s, "training-crane-samurai", 0, 1);
        var e = MatchEngine.FromTestState(s);
        var offers = Options(e, 0, "p5-petty-reveal", petty.Id).Where(o => o.Enabled).ToArray();
        Check(offers.Length > 0 && offers.All(o => (o.Command!.CardIds ?? []).All(id => id.StartsWith("hidden-p", StringComparison.Ordinal))), "face-down cards are named by their place only");
        Check(!offers.Any(o => o.Label.Contains(CardCatalog.Get(Blessings).Name)), "no offer names a face-down card");
        var mine = ownHidden.Id;
        var pair = offers.First(o => o.Command!.CardIds!.SequenceEqual(new[] { "hidden-p0-province1", "hidden-p1-province2" }));
        Accept(e, pair.Command!);
        Check(e.State.Cards[mine].FaceUp, "the player's own face-down card was turned face-up");
        Eq(e.State.Cards[celestial.Id].Zone, Zone.Celestial, "the other player's Celestial was revealed and entered play");
        var fresh = MatchEngine.FromTestState(s);
        Reject(fresh, new GameCommand("useAbility", 0, fresh.Version, petty.Id, AbilityId: "p5-petty-reveal", CardIds: [mine]));
    }

    private static void PettyReturn()
    {
        foreach (var pay in new[] { true, false })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var petty = Add(s, Petty, 0, Zone.Hand);
            var host = Add(s, "training-lion-samurai", 1, Zone.Home);
            var follower = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id);
            var own = Add(s, "training-follower", 0, Zone.Attached, parent: Add(s, "training-crane-samurai", 0, Zone.Home).Id);
            for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var bowedBefore = e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Bowed);
            var targets = Options(e, 0, "p5-petty-return", petty.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
            Check(targets.Contains(follower.Id) && !targets.Contains(own.Id), "an attachment owned by another player only");
            Accept(e, Offer(e, 0, "p5-petty-return", petty.Id, follower.Id));
            Eq(e.State.PendingChoice?.Type, "eitherOr", "its owner decides");
            var option = e.QueryActions(1).First(o => o.Type == "chooseEitherOr" && o.Command?.Choice == (pay ? "pay" : "refuse"));
            Accept(e, option.Command!);
            if (pay) Check(e.State.Cards[follower.Id].Zone == Zone.Attached && e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Bowed) > bowedBefore, "he paid 4 Gold; the Follower stays");
            else Check(e.State.Cards[follower.Id].Zone == Zone.Hand && e.State.Players[1].Hand.Contains(follower.Id), "he did not pay: the Follower returned to his hand");
        }
    }

    private static void SearchingCard()
    {
        // The Defender (player 0) looks and puts a Personality into play at the battlefield.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var searching = Add(s, Searching, 0, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var top = Add(s, "training-crane-samurai", 0, Zone.DynastyDeck); s.Players[0].DynastyDeck.Insert(0, top.Id);
        s.Players[0].Honor = 10;
        var e = MatchEngine.FromTestState(s);
        var gold = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(e, Enabled(e, 0, "p5-searching-look", searching.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "lookArrange", "the look is open, private to the Defender");
        Check(e.GetView(0).PendingLook is not null && e.GetView(1).PendingLook is null, "only he sees the cards");
        var arrange = e.QueryActions(0).First(o => o.Type == "arrangeTopCards" && (o.Command!.CardIds ?? []).Contains("0:play"));
        Accept(e, arrange.Command!);
        Settle(e);
        Check(e.State.Cards[top.Id].Zone == Zone.Battlefield && e.State.Cards[top.Id].ProvinceIndex == 0 && e.State.Cards[top.Id].OwnerId == 0, "the Personality is in play at the battlefield");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed), gold, "no Gold was paid");
        RunUntil(e, () => !e.State.UnresolvedBattlefields.Contains(0) || e.State.Cards[top.Id].Zone != Zone.Battlefield);
        Check(e.State.Cards[top.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "discarded after the battle ended (or destroyed in it)");
        // Not for the Attacker.
        var a = Table(Phase.Battle, 0);
        var searching2 = Add(a, Searching, 0, Zone.Hand);
        Add(a, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(a), 0, "p5-searching-look", searching2.Id) || a.ActivePlayerId != 0, "the Attacker may not");
        var a2 = Table(Phase.Battle, 1); a2.PriorityPlayerId = 1;
        var searching3 = Add(a2, Searching, 1, Zone.Hand);
        Add(a2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(a2), 1, "p5-searching-look", searching3.Id), "the Attacker may not play it");

        // Focus Effect: the winner's owner gains 1 Honor; a loser's does not.
        foreach (var wins in new[] { true, false })
        {
            var d = Table(Phase.Battle, 0);
            var mine = Add(d, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var theirs = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            (wins ? mine : theirs).Modifiers.Add(new("test-source", "test", "Chi", 9));
            var duel = Add(d, "training-plain-duel-strategy", 0, Zone.Hand);
            var focused = Add(d, Searching, 0, Zone.Hand);
            var deckCard = Add(d, "training-focus-strategy", 1, Zone.FateDeck); d.Players[1].FateDeck.Insert(0, deckCard.Id);
            var de = MatchEngine.FromTestState(d);
            var honor = de.State.Players[0].Honor;
            Accept(de, Offer(de, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(de, new GameCommand("focus", 1, de.Version, Choice: "deck"));
            Accept(de, new GameCommand("focus", 0, de.Version, CardId: focused.Id));
            Accept(de, new GameCommand("strike", 1, de.Version));
            Eq(de.State.Players[0].Honor - honor >= 1, wins, wins ? "the winner's owner gained Honor" : "a lost duel gains nothing");
        }
    }

    private static void ScoutingCard()
    {
        string scout = "", card = "", other = "";
        var e = EngagedWithWindow(s =>
        {
            scout = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0).Id;
            other = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0).Id;
            card = Add(s, Scouting, 1, Zone.Hand).Id;
            Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Hand); Add(s, "training-lion-samurai", 0, Zone.Hand); Add(s, "training-follower", 0, Zone.Hand);
        });
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, Enabled(e, 1, "p5-scouting-pass", card, scout).Command!);
        Check(e.GetView(1).HandSights?.Length == 2, "the Attacker sees two cards of the Defender's hand");
        Check(e.GetView(0).HandSights is null, "the Defender's view carries nothing");
        Check(!e.GetView(1).HandSights!.Any(v => v.DefinitionId.Length == 0), "named by definition");
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.FirstActionPlayerId, 1, "the Attacker has the first chance");
        // Issue #1333: both seats are told who must take it and why.
        foreach (var seat in new[] { 0, 1 })
            Check(e.GetView(seat).HeldActions is [{ PlayerId: 1, Kind: "first", SourceName: "Scouting the Pass", MustPass: false } first] && first.PerformerId == scout, $"seat {seat}: the Scout's first action is shown");
        Check(e.State.Players[0].Provinces[0].Reconnaissance[1], "Reconnaissance there");
        var bowerTarget = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Id;
        Check(Rejection(e, Use(e, 1, "training-bow-at-battle", other, bowerTarget)).Length > 0, "his first Battle action must be performed by that Scout");
        // Not for the Defender.
        var d = EngagedWithWindow(s =>
        {
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0).Id;
            card = Add(s, Scouting, 0, Zone.Hand).Id;
        });
        Check(d.State.PendingChoice?.Type != "engageReaction" || !CanUse(d, 0, "p5-scouting-pass", card, scout), "the Defender may not play it");
    }

    // A battle engaged at province 0 (Attacker player 1) whose Engage window is left open.
    private static MatchEngine EngagedWithWindow(Action<MatchState> setup)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        setup(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 1, e.Version, Province: 0));
        return e;
    }

    private static void StolenCard()
    {
        var market = CardCatalog.Cards.Values.First(c => c.Id == "oracle-l5r-10214"); // a Market: "bow: draw a card if ..." and 2 Gold, base Gold Cost 3
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = "Crane";
        var stolen = Add(s, Stolen, 0, Zone.Home);
        var theirs = Add(s, market.Id, 1, Zone.Home);
        var costly = Add(s, CardCatalog.Cards.Values.First(c => c.Type == CardType.Holding && !c.ScenarioOnly && c.GoldCost >= 5 && c.GoldProduction > 0).Id, 1, Zone.Home);
        var mine = Add(s, market.Id, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "p5-stolen-gold", stolen.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(theirs.Id) && !targets.Contains(costly.Id) && !targets.Contains(mine.Id), "another player's Holding with a base Gold Cost from 1 to 4 only");
        Eq(e.GoldProduced(stolen.Id, 0), 0, "Stolen Merchandise produces nothing of its own");
        Accept(e, Options(e, 0, "p5-stolen-gold", stolen.Id).First(o => o.Enabled && o.TargetIds!.Contains(theirs.Id)).Command!);
        Eq(e.GoldProduced(stolen.Id, 0), 2, "it copied the Market's Gold-producing trait");
        var later = Edit(e, st => { });
        RunUntil(later, () => later.State.TurnNumber > s.TurnNumber);
        Eq(later.GoldProduced(stolen.Id, 0), 0, "until the end of the turn");
        // Its ability instead (the other way: one use).
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; s2.Players[0].Clan = "Crane";
        var stolen2 = Add(s2, Stolen, 0, Zone.Home);
        var theirs2 = Add(s2, market.Id, 1, Zone.Home);
        for (var i = 0; i < 3; i++) Add(s2, "training-crane-samurai", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var copy = Options(e2, 0, "p5-stolen-ability", stolen2.Id).First(o => o.Enabled && o.TargetIds!.Contains(theirs2.Id));
        Accept(e2, copy.Command!);
        Check(e2.AbilityIdsForTest(stolen2.Id).Contains("market-draw-crane"), "it has the Market's ability");
        Check(!Options(e2, 0, "p5-stolen-gold", stolen2.Id).Any(o => o.Enabled), "one use for the two ways");
        if (e2.State.PriorityPlayerId == 1) Accept(e2, PassCommand(e2));
        var hand = e2.State.Players[0].Hand.Count;
        Accept(e2, Enabled(e2, 0, "market-draw-crane", stolen2.Id).Command!);
        Eq(e2.State.Players[0].Hand.Count, hand + 1, "and uses it");
    }

    private static void LearningCard()
    {
        foreach (var play in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var learning = Add(s, Learning, 0, Zone.Hand);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(enemy, "Duelist");
            var borrowed = Add(s, Disrupting, 1, Zone.FateDiscard);
            var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var option = Options(e, 0, "p5-learning", learning.Id, performer.Id).First(o => o.Enabled && o.Command?.Choice == "Duelist");
            Accept(e, option.Command! with { TargetId = borrowed.Id });
            Check(e.HasKeyword(performer.Id, "Duelist"), "the performer copied Duelist");
            Eq(e.State.Cards[borrowed.Id].OwnerId, 0, "the Strategy is his to play while the additional action stands");
            Eq(e.State.PriorityPlayerId, 0, "the additional action keeps the opportunity");
            if (play)
            {
                Accept(e, Offer(e, 0, "p5-disrupting-communication", borrowed.Id, enemy.Id, scout.Id));
                Settle(e);
                Check(e.State.Cards[enemy.Id].Bowed, "the borrowed Strategy resolved");
                Eq(e.State.Cards[borrowed.Id].Zone, Zone.RemovedFromGame, "it was removed from the game");
                Eq(e.State.Cards[borrowed.Id].OwnerId, 1, "and is its owner's");
            }
            else
            {
                Accept(e, new GameCommand("pass", 0, e.Version));
                Eq(e.State.Cards[borrowed.Id].OwnerId, 1, "declined, it went back to its owner");
                Eq(e.State.Cards[borrowed.Id].Zone, Zone.FateDiscard, "in his discard pile");
            }
        }
        // Not a Strategy of his own.
        var n = Table(Phase.Battle, 0);
        var p = Add(n, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var l = Add(n, Learning, 0, Zone.Hand);
        Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var own = Add(n, Disrupting, 0, Zone.FateDiscard);
        Check(!Options(MatchEngine.FromTestState(n), 0, "p5-learning", l.Id, p.Id).Any(o => o.Enabled && (o.TargetIds ?? []).Contains(own.Id)), "a Strategy in his own discard pile is not one");
    }
}
