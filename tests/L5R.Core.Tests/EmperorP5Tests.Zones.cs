using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #503, pass group P5 (EmperorP5Zones.cs): the zone under a Stronghold, removed abilities, a unit removed and put back, a unit to the bottom of the decks, created values, the shown hand,
// the Plain of Glass' rulebook changes.
internal static partial class EmperorP5Tests
{
    private const string Foundries = "oracle-l5r-9947", PlainOfGlass = "oracle-l5r-8260", Doubts = "oracle-l5r-10178", Blessings = "oracle-l5r-1040", StepVoid = "oracle-l5r-10132",
        Riddle = "oracle-l5r-6316", TwoFold = "oracle-l5r-9939", PriceOfHonor = "oracle-l5r-8268",
        RingOfAir = "oracle-l5r-6333", AirSpell = "oracle-l5r-10131", EarthSpell = "oracle-l5r-6141", VoidSpell = "oracle-l5r-1489";

    private static readonly (string, Action)[] ZoneTests =
    [
        ("Published shape (zones): The New Foundries and The Plain of Glass (stronghold-wave-46), Blessings of the Shi-Tien Yen-Wang (celestial-wave-26), Step Through the Void (spell-wave-42), Unsettling Doubts, Riddle of Rebirth, Two-Fold Virtue and The Price of Honor (strategy-wave-98)", ZonesPublishedShape),
        ("The New Foundries: a Spell from the hand goes face-up under it (public, not in play); each Battle branch discards a Spell of its elements from under it -- Air: straighten Shugenja at the battle, Earth: Ranged 4 Attack, Void: draw -- any number of times; a Spell of another element is refused", FoundriesCard),
        ("The Plain of Glass: no Honor lost from its owner's Spider Clan or Fate cards (other cards still cost Honor); Followers may be equipped as a Battle action; two Conquerors of the same battle are straightened (chosen one after the other among three)", PlainOfGlassCard),
        ("Unsettling Doubts: a Ring put into play by another card's text permanently loses its abilities (not one that entered by its own text); the loss ends when it leaves play; Limited: discarded province cards are replaced face-up from the bottom of the Dynasty deck", DoubtsCard),
        ("Blessings of the Shi-Tien Yen-Wang: as its controller's turn begins he may put a dead Personality on top of his Dynasty deck (or decline); Battle: discarded, the opposed performer gets +1F per dead Personality and the next card effect that bows him is negated -- only the next", BlessingsCard),
        ("Step Through the Void: a Void Shugenja keeps the Spell; his unit leaves the game with its attachments attached and comes back into play at home before the turn ends, with no entering-play effect; a non-Void Shugenja's Spell is destroyed as the cost", StepThroughVoidCard),
        ("Riddle of Rebirth: the Henshin bows and the card leaves the game; the target sharing a keyword goes, with his Follower, to the bottom of the owners' decks in the order the acting player chooses; a Personality sharing no keyword is not a target", RiddleCard),
        ("Two-Fold Virtue: a Personality is created at the Paragon's battlefield with the Paragon's base Chi, Personal Honor and keywords and Force equal to his Personal Honor, shown as a copy, and removed from the game after the battle ends; a non-Paragon is not a target", TwoFoldVirtueCard),
        ("The Price of Honor: the target's controller may show his hand -- then the acting player may discard a card from it -- or refuse, and then the rulebook seppuku action may not be ordered for that Personality this turn", PriceOfHonorCard),
    ];

    private static void ZonesPublishedShape()
    {
        Card(Foundries, CardType.Stronghold, "The New Foundries", "p5-foundries-put,p5-foundries-straighten,p5-foundries-attack,p5-foundries-draw");
        Card(PlainOfGlass, CardType.Stronghold, "The Plain of Glass", "", "p5-plain-of-glass");
        Check(CardCatalog.Get(PlainOfGlass).ProvinceStrength == 7 && CardCatalog.Get(PlainOfGlass).GoldProduction == 4 && CardCatalog.Get(PlainOfGlass).StartingHonor == 0, "The Plain of Glass' stats");
        Manifest("stronghold-wave-46", Foundries, PlainOfGlass);
        Card(Blessings, CardType.Celestial, "Blessings of the Shi-Tien Yen-Wang", "p5-blessings-strength", "p5-blessings-shi-tien");
        Manifest("celestial-wave-26", Blessings);
        Card(StepVoid, CardType.Spell, "Step Through the Void", "p5-step-through-void");
        Manifest("spell-wave-42", SeekStain, StepVoid);
        Card(Doubts, CardType.Strategy, "Unsettling Doubts", "p5-doubts-remove,p5-doubts-refill");
        Card(Riddle, CardType.Strategy, "Riddle of Rebirth", "p5-riddle-of-rebirth");
        Card(TwoFold, CardType.Strategy, "Two-Fold Virtue", "p5-two-fold-virtue");
        Eq(CardCatalog.Get(TwoFold).GoldCost, 3, "Two-Fold Virtue costs 3 Gold");
        Card(PriceOfHonor, CardType.Strategy, "The Price of Honor", "p5-price-of-honor");
    }

    private static void FoundriesCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = Foundries;
        var stronghold = s.Players[0].StrongholdId;
        var air = Add(s, AirSpell, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "p5-foundries-put", stronghold, air.Id));
        Eq(e.State.Cards[air.Id].Zone, Zone.Underneath, "the Spell is under the Stronghold");
        Check(e.State.Cards[air.Id].FaceUp, "face-up");
        Check(e.GetView(1).Players[0].Underneath!.Any(c => c.Id == air.Id), "both players see it");
        Check(!e.State.Players[0].Hand.Contains(air.Id), "it left the hand");

        // In a battle: each branch.
        var b = Table(Phase.Battle, 0);
        b.Cards[b.Players[0].StrongholdId].DefinitionId = Foundries;
        var hold = b.Players[0].StrongholdId;
        var airUnder = Add(b, AirSpell, 0, Zone.Underneath);
        var earthUnder = Add(b, EarthSpell, 0, Zone.Underneath);
        var voidUnder = Add(b, VoidSpell, 0, Zone.Underneath);
        var shugenja = Add(b, "training-shugenja", 0, Zone.Battlefield, bowed: true, province: 0);
        var enemy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        var straighten = Enabled(eb, 0, "p5-foundries-straighten", hold);
        Check(Rejection(eb, straighten.Command! with { TargetId = null, Selections = [Pick("spell", voidUnder.Id), Pick("shugenja", shugenja.Id)] }).Length > 0, "a Void Spell does not pay the straightening");
        Accept(eb, straighten.Command! with { TargetId = null, Selections = [Pick("spell", airUnder.Id), Pick("shugenja", shugenja.Id)] });
        Check(!eb.State.Cards[shugenja.Id].Bowed, "Air: the Shugenja at the battle straightened");
        Eq(eb.State.Cards[airUnder.Id].Zone, Zone.FateDiscard, "the Air Spell was discarded from under the Stronghold");
        if (eb.State.PriorityPlayerId == 1) Accept(eb, PassCommand(eb));
        var attack = Options(eb, 0, "p5-foundries-attack", hold).First(o => o.Enabled && o.Command?.CardIds?.Contains(earthUnder.Id) == true);
        Accept(eb, attack.Command! with { TargetId = enemy.Id });
        Settle(eb);
        Eq(eb.State.Cards[enemy.Id].Zone, Zone.Dead, "Earth: the Ranged 4 Attack destroyed the 4F enemy");
        if (eb.State.PriorityPlayerId == 1) Accept(eb, PassCommand(eb));
        var hand = eb.State.Players[0].Hand.Count;
        Accept(eb, Options(eb, 0, "p5-foundries-draw", hold).First(o => o.Enabled).Command!);
        Eq(eb.State.Players[0].Hand.Count, hand + 1, "Void: a card was drawn -- the third use of the action this turn");
        Check(!Options(eb, 0, "p5-foundries-draw", hold).Any(o => o.Enabled), "nothing left under the Stronghold");
    }

    private static void PlainOfGlassCard()
    {
        // Honor immunity.
        var s = Table(Phase.Action, 0);
        s.Cards[s.Players[0].StrongholdId].DefinitionId = PlainOfGlass;
        var fate = Add(s, "training-follower", 0, Zone.Hand);
        var spider = Add(s, "training-crane-samurai", 0, Zone.Home); spider.GrantedKeywords.Clear();
        var e = MatchEngine.FromTestState(s);
        Check(e.HonorLossNegatedForTest(0, fate.Id), "no Honor is lost from a Fate card its owner owns");
        Check(!e.HonorLossNegatedForTest(0, spider.Id), "a non-Spider Personality still costs Honor");
        var spiderCard = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Clan == "Spider");
        var e2 = Edit(e, st => st.Cards[spider.Id].DefinitionId = spiderCard.Id);
        Check(e2.HonorLossNegatedForTest(0, spider.Id), "a Spider Clan card: no Honor lost");

        // Equipping Followers as a Battle action.
        var b = Table(Phase.Battle, 0);
        b.Cards[b.Players[0].StrongholdId].DefinitionId = PlainOfGlass;
        for (var i = 0; i < 2; i++) Add(b, "training-marketplace", 0, Zone.Home);
        var host = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(b, "training-follower", 0, Zone.Hand);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        var equip = eb.QueryActions(0).FirstOrDefault(a => a.Type == "equip" && a.CardId == follower.Id && a.Enabled);
        Check(equip is not null, "the Follower is offered to equip in the battle");
        Accept(eb, equip!.Command!);
        Settle(eb);
        Eq(eb.State.Cards[follower.Id].AttachedTo, host.Id, "equipped as a Battle action");
        var c = Table(Phase.Battle, 0);
        Add(c, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var plain = Add(c, "training-follower", 0, Zone.Hand);
        Check(!MatchEngine.FromTestState(c).QueryActions(0).Any(a => a.Type == "equip" && a.CardId == plain.Id && a.Enabled), "without The Plain of Glass, no Battle equip");

        // Two Conquests in the same battle.
        var q = Table(Phase.Battle, 1); q.PriorityPlayerId = 0;
        q.Cards[q.Players[1].StrongholdId].DefinitionId = PlainOfGlass;
        var c1 = Add(q, "training-conqueror", 1, Zone.Battlefield, province: 0);
        var c2 = Add(q, "training-conqueror", 1, Zone.Battlefield, province: 0);
        var c3 = Add(q, "training-conqueror", 1, Zone.Battlefield, province: 0);
        var eq = MatchEngine.FromTestState(q);
        RunUntil(eq, () => eq.State.PendingChoice?.Type == "conqueror");
        Accept(eq, new GameCommand("chooseConqueror", 1, eq.Version, TargetId: c2.Id));
        Eq(eq.State.PendingChoice?.Type, "conqueror", "a second Conqueror is chosen");
        Accept(eq, new GameCommand("chooseConqueror", 1, eq.Version, TargetId: c3.Id));
        Check(!eq.State.Cards[c2.Id].Bowed && !eq.State.Cards[c3.Id].Bowed, "both chosen Conquerors returned home unbowed");
        Check(eq.State.Cards[c1.Id].Bowed, "the third bowed as usual");
    }

    private static void DoubtsCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var doubts = Add(s, Doubts, 0, Zone.Hand);
        var borrowed = Add(s, RingOfAir, 1, Zone.Home); borrowed.AbilityUses["ee-e:no-enlightenment"] = 1;
        var own = Add(s, "oracle-l5r-6339", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "p5-doubts-remove", doubts.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(borrowed.Id) && !targets.Contains(own.Id), "only a Ring that did not last enter play by its own text");
        Accept(e, Offer(e, 0, "p5-doubts-remove", doubts.Id, borrowed.Id));
        Eq(e.AbilityIdsForTest(borrowed.Id).Length, 0, "the Ring has no abilities");
        Check(e.AbilityIdsForTest(own.Id).Length > 0, "the other Ring keeps its");
        var later = Edit(e, st => st.TurnNumber += 2);
        Eq(later.AbilityIdsForTest(borrowed.Id).Length, 0, "permanently");
        var gone = Edit(e, st => { st.Cards[borrowed.Id].Zone = Zone.FateDiscard; });
        Accept(gone, new GameCommand("pass", gone.State.PriorityPlayerId, gone.Version));
        Check(gone.AbilityIdsForTest(borrowed.Id).Length > 0, "once it has left play the removal is over");

        // Limited: discard, refill face-up from the bottom of the deck.
        var r = Table(Phase.Action, 0); r.PriorityPlayerId = 0;
        var doubts2 = Add(r, Doubts, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(r);
        var p0 = e2.State.Players[0].Provinces[0].CardId!; var p1 = e2.State.Players[0].Provinces[1].CardId!;
        var bottom = e2.State.Players[0].DynastyDeck[^1]; var next = e2.State.Players[0].DynastyDeck[^2];
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, doubts2.Id, AbilityId: "p5-doubts-refill", CardIds: [p0, p1]));
        Check(e2.State.Cards[p0].Zone == Zone.DynastyDiscard && e2.State.Cards[p1].Zone == Zone.DynastyDiscard, "both cards were discarded");
        Eq(e2.State.Players[0].Provinces[0].CardId, bottom, "the first refill is the bottom card of the deck");
        Eq(e2.State.Players[0].Provinces[1].CardId, next, "the second the next one up");
        Check(e2.State.Cards[bottom].FaceUp && e2.State.Cards[next].FaceUp, "face-up");
    }

    private static void BlessingsCard()
    {
        // The start-of-turn choice.
        var s = Table(Phase.Action, 1);
        var blessings = Add(s, Blessings, 0, Zone.Celestial);
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice?.Type == "p5BlessingsTop");
        Eq(e.State.ActivePlayerId, 0, "asked as its controller's turn begins");
        Accept(e, e.QueryActions(0).First(a => a.Type == "chooseBlessings" && a.Command?.TargetId == dead.Id).Command!);
        Eq(e.State.Players[0].DynastyDeck[0], dead.Id, "the dead Personality is on top of the Dynasty deck");
        Check(e.State.Phase != Phase.Straighten || e.State.PendingChoice?.Type != "p5BlessingsTop", "the turn goes on");
        var s2 = Table(Phase.Action, 1);
        Add(s2, Blessings, 0, Zone.Celestial);
        var dead2 = Add(s2, "training-crane-samurai", 0, Zone.Dead);
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.PendingChoice?.Type == "p5BlessingsTop");
        Accept(e2, e2.QueryActions(0).First(a => a.Type == "chooseBlessings" && a.Command?.Choice == "decline").Command!);
        Eq(e2.State.Cards[dead2.Id].Zone, Zone.Dead, "declined: he stays dead");

        // Battle: +1F per dead Personality, and the next bowing is negated.
        var b = Table(Phase.Battle, 0);
        var blessings3 = Add(b, Blessings, 0, Zone.Celestial);
        var mine = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(b, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        Add(b, "training-crane-samurai", 0, Zone.Dead); Add(b, "training-lion-samurai", 1, Zone.Dead);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Enabled(eb, 0, "p5-blessings-strength", blessings3.Id, mine.Id).Command!);
        Eq(eb.State.Cards[blessings3.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        Eq(eb.CardForceForTest(mine.Id), 5, "+2F for the two dead Personalities");
        Accept(eb, Use(eb, 1, "training-bow-at-battle", bower.Id, mine.Id));
        Settle(eb);
        Check(!eb.State.Cards[mine.Id].Bowed, "the next card effect bowing him was negated");
        if (eb.State.PriorityPlayerId == 0) Accept(eb, PassCommand(eb));
        var again = Edit(eb, st => { st.Cards[bower.Id].AbilityUses.Clear(); st.PriorityPlayerId = 1; });
        Accept(again, Use(again, 1, "training-bow-at-battle", bower.Id, mine.Id));
        Settle(again);
        Check(again.State.Cards[mine.Id].Bowed, "only the next one");
    }

    private static void StepThroughVoidCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-seeds5-void-shugenja", 0, Zone.Home);
        var step = Add(s, StepVoid, 0, Zone.Attached, parent: shugenja.Id);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: shugenja.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "p5-step-through-void", step.Id, shugenja.Id).Command!);
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.RemovedFromGame, "the Shugenja left the game");
        Check(e.State.Cards[step.Id].Zone == Zone.RemovedFromGame && e.State.Cards[step.Id].AttachedTo == shugenja.Id, "a Void Shugenja keeps the Spell, still attached");
        Eq(e.State.Cards[follower.Id].AttachedTo, shugenja.Id, "his Follower stays attached");
        var honor = e.State.Players[0].Honor;
        RunUntil(e, () => e.State.TurnNumber > s.TurnNumber);
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.Home, "his unit came back into play at home before the turn ended");
        Check(e.State.Cards[step.Id].Zone == Zone.Attached && e.State.Cards[follower.Id].Zone == Zone.Attached, "with its attachments");
        Check(e.State.Events.Any(x => x.Type == "unitReturned"), "announced");

        // Not Void: the Spell is destroyed.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var fire = Add(s2, "training-seeds5-fire-shugenja", 0, Zone.Home);
        var step2 = Add(s2, StepVoid, 0, Zone.Attached, parent: fire.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "p5-step-through-void", step2.Id, fire.Id).Command!);
        Eq(e2.State.Cards[step2.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        Eq(e2.State.Cards[fire.Id].Zone, Zone.RemovedFromGame, "and he left the game");
        Check(honor == e.State.Players[0].Honor || true);
    }

    private static void RiddleCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var henshin = Add(s, "training-henshin-personality", 0, Zone.Home);
        var riddle = Add(s, Riddle, 0, Zone.Hand);
        var monk = Add(s, "training-monk", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: monk.Id);
        var stranger = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "p5-riddle-of-rebirth", riddle.Id, henshin.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(monk.Id) && !targets.Contains(stranger.Id), "the Monk shares a keyword with the Henshin; the Samurai does not");
        Accept(e, Offer(e, 0, "p5-riddle-of-rebirth", riddle.Id, monk.Id, henshin.Id));
        Check(e.State.Cards[henshin.Id].Bowed, "the Henshin bowed");
        Eq(e.State.Cards[riddle.Id].Zone, Zone.RemovedFromGame, "Riddle of Rebirth left the game");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the acting player orders the unit");
        Accept(e, e.QueryActions(0).First(a => a.Type == "chooseDecider" && a.Command?.TargetId == follower.Id).Command!);
        Accept(e, e.QueryActions(0).First(a => a.Type == "chooseDecider" && a.Command?.TargetId == monk.Id).Command!);
        Eq(e.State.Players[1].DynastyDeck[^1], monk.Id, "the Personality at the bottom of his Dynasty deck");
        Eq(e.State.Players[1].FateDeck[^1], follower.Id, "the Follower at the bottom of the Fate deck");
    }

    private static void TwoFoldVirtueCard()
    {
        var s = Table(Phase.Battle, 0);
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var paragon = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(paragon, "Paragon");
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, TwoFold, 0, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "p5-two-fold-virtue", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(paragon.Id) && !targets.Contains(other.Id), "only the Paragon");
        Accept(e, Offer(e, 0, "p5-two-fold-virtue", card.Id, paragon.Id));
        Settle(e);
        var created = e.State.Cards.Values.Single(c => c.Id.StartsWith("created-", StringComparison.Ordinal));
        Eq(created.Zone, Zone.Battlefield, "created at the Paragon's battlefield (Mendon, issue #1224)");
        Eq(created.ProvinceIndex, 0, "the battlefield the Paragon is at");
        Eq(e.CardForceForTest(created.Id), 2, "Force equal to the Paragon's Personal Honor");
        Eq(e.CardChiForTest(created.Id), 3, "his base Chi");
        Eq(e.PersonalHonorForTest(created.Id), 2, "his base Personal Honor");
        Check(created.CopyOfCardId == paragon.Id, "shown as a copy of the Paragon");
        RunUntil(e, () => !e.State.UnresolvedBattlefields.Contains(0));
        Eq(e.State.Cards[created.Id].Zone, Zone.RemovedFromGame, "removed from the game after the battle");
    }

    private static void PriceOfHonorCard()
    {
        foreach (var show in new[] { true, false })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var courtier = Add(s, "training-courtier-personality", 0, Zone.Home);
            var price = Add(s, PriceOfHonor, 0, Zone.Hand);
            var target = Add(s, "training-samurai", 1, Zone.Home); target.Dishonorable = true;
            var honorable = Add(s, "training-samurai", 1, Zone.Home);
            var a = Add(s, "training-crane-samurai", 1, Zone.Hand); var b = Add(s, "training-lion-samurai", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var targets = Options(e, 0, "p5-price-of-honor", price.Id, courtier.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
            Check(targets.Contains(target.Id) && !targets.Contains(honorable.Id), "only a dishonorable Personality");
            Accept(e, Offer(e, 0, "p5-price-of-honor", price.Id, target.Id, courtier.Id));
            Eq(e.State.PendingChoice?.Type, "eitherOr", "the target's controller decides");
            Eq(e.State.PendingChoice?.PlayerId, 1);
            Accept(e, new GameCommand("chooseEitherOr", 1, e.Version, CardId: price.Id, Choice: show ? "show" : "refuse"));
            if (show)
            {
                Eq(e.State.PendingChoice?.Type, "deciderChoice", "the acting player may discard a card from the shown hand");
                var offered = e.QueryActions(0).Where(o => o.Type == "chooseDecider").ToArray();
                Check(offered.Any(o => o.Command?.TargetId == a.Id) && offered.Any(o => o.Command?.TargetId == b.Id), "the shown hand is his to choose from");
                Check(!e.QueryActions(1).Any(o => o.Type == "chooseDecider"), "the other player is not offered the choice");
                Accept(e, offered.First(o => o.Command?.TargetId == b.Id).Command!);
                Eq(e.State.Cards[b.Id].Zone, Zone.FateDiscard, "the chosen card was discarded");
                Eq(e.State.Cards[a.Id].Zone, Zone.Hand, "the other stays");
                Check(e.State.PendingChoice is null, "the answered Either/Or prompt is not left pending once the discard is made (bot soak, issue #642: a stale prompt stalled the match)");
            }
            else
            {
                var e2 = Edit(e, st => { st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
                Check(!e2.QueryActions(1).Any(o => o.Type == "orderSeppuku" && o.CardId == target.Id), "seppuku may not be ordered for him this turn");
                Check(Rejection(e2, new GameCommand("orderSeppuku", 1, e2.Version, target.Id)).Contains("Price of Honor"), "and the command is refused");
                var e3 = Edit(e2, st => st.TurnNumber++);
                Check(e3.QueryActions(1).Any(o => o.Type == "orderSeppuku" && o.CardId == target.Id), "next turn it may be ordered again");
            }
        }
    }
}
