using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The Shadow's Embrace pass (issue #476), batch 6: the cards of holding-wave-12, follower-wave-30, event-wave-7, stronghold-wave-16 and celestial-wave-7, driven through
// QueryActions, the commands it offers and the turn flow. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training
// fixtures (and "test-fixture" keyword grants) standing in for the Personalities each card needs.
internal static class ShadowsEmbraceBatch6Tests
{
    private const string Fortress = "oracle-l5r-9796", PlatinumMine = "oracle-l5r-9798", ShelteredPort = "oracle-l5r-9799", SmallTimeBully = "oracle-l5r-9800",
        VersatileFarm = "oracle-l5r-9801", WhisperingArchive = "oracle-l5r-9802",
        ClearingCrew = "oracle-l5r-9867", ElephantCaravan = "oracle-l5r-9868", Shieldman = "oracle-l5r-9873", WastelandScout = "oracle-l5r-9874",
        DiscoveringTheTemple = "oracle-l5r-9791", On = "oracle-l5r-9793", StarCrossedLovers = "oracle-l5r-9794", WartimePreparations = "oracle-l5r-9795",
        BishamonsTower = "oracle-l5r-9945", TwinForksCity = "oracle-l5r-9946",
        MoonsImperative = "oracle-l5r-9789", SunsAspiration = "oracle-l5r-9790",
        PearlOfRage = "oracle-l5r-5903", NecessaryEvil = "oracle-l5r-5524";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static ActionOption Opt(MatchEngine e, int player, string ability, Func<ActionOption, bool>? match = null)
    {
        var found = Listed(e, player, ability).FirstOrDefault(a => a.Enabled && (match?.Invoke(a) ?? true));
        Check(found is not null, $"{ability} for player {player}: " + string.Join("; ", Options(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return found!;
    }
    // Player abilities (Fortress of Lidless Eyes' grant) are enabled only with a published Stronghold (the economy fixture's are scenario-only).
    private static MatchState Published(MatchState s) { foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }
    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false; s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }
    private static CardInstance FaceUpIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }
    private static void Decline(MatchEngine e) { while (e.State.PendingChoice is { } p && p.Type != "starCrossedLovers") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    public static void Run(TestRunner runner) => runner.Run("shadows-embrace-batch-6", [
        ("Published shape: exactly the eighteen wired cards are published across the five waves, with their printed stats, keywords, Effects and ability ids and shipped art", PublishedShape),
        ("Platinum Mine: 5 Gold, 6 when paying for an attachment (an Equip, or Equip (Battle)'s Spell), never for a recruit or an action", PlatinumMineProduction),
        ("Sheltered Port: 2 Gold plus 1 for each other Market or Port Holding its controller controls", ShelteredPortProduction),
        ("Whispering Archive: its controller's Spells enter play paying 1 less Gold (Equip (Battle)); a Follower pays in full", WhisperingArchiveDiscount),
        ("Small-Time Bully: recruited while its controller has a Courtier or Magistrate, it may dishonor a Personality; without one nothing is offered", SmallTimeBullyEntry),
        ("Versatile Farm: destroyed, it searches for a non-Unique Farm with another title and puts it into play unbowed for 2 less Gold (free, or paid, or declined)", VersatileFarmSearch),
        ("Fortress of Lidless Eyes: once per game its bow holds a Personality's straightening while it stays in play; any player may pay 6 Gold to destroy it, which frees him", FortressOfLidlessEyes_),
        ("The Moon's Imperative / The Sun's Aspiration: a Holding produces 1 less / more this turn; removed from the game, they discard province Personalities (refilled face-up) or shuffle discarded cards into their decks", MoonAndSun),
        ("Clearing Crew: equipped from the hand, its controller draws an additional card in the End Phase; attached from a discard pile it draws nothing", ClearingCrewDraw),
        ("Elephant Caravan: its Fear Battle destroys a bowed card or an attachment with equal or lower Force, and bows an unbowed one", ElephantCaravanFear),
        ("Wasteland Scout: after engaging at its battlefield, Reconnaissance there and +1F to its controller's other Followers there", WastelandScoutRecon),
        ("Shieldman: its unit may not be assigned to attack (it may defend), Attacks on its unit have -3 strength, and another player's action destroying another card of the unit earns 2 Honor", ShieldmanUnit),
        ("Bishamon's Tower: a Berserker (even bowed) or an unbowed Personality destroys an enemy card with lower Force and no attachments", BishamonsTowerDestroy),
        ("Twin Forks City: a Personality that wins a duel in a battle is shielded from the next bowing by another player's card's effect (once per Personality per battle)", TwinForksCityShield),
        ("Discovering the Temple: revealed, it searches the Fate deck or the Fate discard pile for Blood of the Preserver and puts it into the hand", DiscoveringTheTempleSearch),
        ("On: at the end of each phase each player's Honor is lowered to its start-of-phase value plus 3 plus the Honor from battle resolution; without On nothing changes", OnHonorLimit),
        ("Wartime Preparations: another player's second bowing effect in an Action Phase and second assignment-preventing effect in a turn are negated", WartimePreparationsNegations),
        ("Star-Crossed Lovers: revealed, its owner pairs two Personalities of one controller; they may never be assigned or move to each other's battlefields", StarCrossedLoversPair),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(listed.Where(l => l.Length > 0).OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Abilities(string id, params string[] expected)
        {
            var card = CardCatalog.Get(id);
            Check(!card.ScenarioOnly && (card.Abilities ?? []).SequenceEqual(expected), $"{card.Name}: abilities {string.Join(",", card.Abilities ?? [])}");
            foreach (var ability in expected) Check(AbilityCatalog.All.ContainsKey(ability), ability);
        }
        string[] Ids(PublishedSnapshot wave) => wave.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        string[] Sorted(params string[] ids) => ids.OrderBy(i => i, StringComparer.Ordinal).ToArray();
        Check(Ids(PublishedContent.HoldingWave12).SequenceEqual(Sorted(Fortress, PlatinumMine, ShelteredPort, SmallTimeBully, VersatileFarm, WhisperingArchive)), "holding-wave-12");
        Check(Ids(PublishedContent.FollowerWave30).SequenceEqual(Sorted(ClearingCrew, ElephantCaravan, Shieldman, WastelandScout)), "follower-wave-30");
        Check(Ids(PublishedContent.EventWave7).SequenceEqual(Sorted(DiscoveringTheTemple, On, StarCrossedLovers, WartimePreparations)), "event-wave-7");
        Check(Ids(PublishedContent.StrongholdWave16).SequenceEqual(Sorted(BishamonsTower, TwinForksCity)), "stronghold-wave-16");
        Check(Ids(PublishedContent.CelestialWave7).SequenceEqual(Sorted(MoonsImperative, SunsAspiration)), "celestial-wave-7");
        // The unwired cards of the batch are never published (the blank-card guard).
        // Issue #503: The New Foundries (9947) and Distant Expansion (9792) are wired and published, so they left this list.
        // Issue #566: Humble Farm (9797) is wired and published in holding-wave-47 once Mendon confirmed the "Humble Farmers" user-supplied scan is a genuine 2F Ashigaru match, so it left this list too.
        // Issue #622: Lookout (9870) is wired and published in follower-wave-45 (No Hiding Place's target-selection step made its effect buildable), so it left this list.
        foreach (var unwired in Array.Empty<string>())
            Check(!CardCatalog.Cards.ContainsKey(unwired), $"{unwired} is not published");

        var fortress = CardCatalog.Get(Fortress);
        Check(fortress.Type == CardType.Holding && fortress.GoldCost == 2 && fortress.GoldProduction == 0 && Keywords(Fortress) == "Kolat,Unique", "Fortress of Lidless Eyes");
        Abilities(Fortress, "fortress-lidless-bow");
        Check(AbilityCatalog.All["fortress-lidless-bow"] is { OncePerGame: true, NotCopyable: true }, "once per game, never copied");
        Check(AbilityCatalog.All["fortress-lidless-destroy"] is { PlayerAbility: true, Timing: "Open" }, "the granted ability is a player ability");
        var mine = CardCatalog.Get(PlatinumMine);
        Check(mine.GoldCost == 6 && mine.GoldProduction == 5 && Keywords(PlatinumMine) == "Mine" && mine.Effect == "platinum-mine-gold", "Platinum Mine");
        Abilities(PlatinumMine);
        var port = CardCatalog.Get(ShelteredPort);
        Check(port.GoldCost == 4 && port.GoldProduction == 2 && Keywords(ShelteredPort) == "Port" && port.Effect == "sheltered-port-gold", "Sheltered Port");
        Abilities(ShelteredPort);
        var bully = CardCatalog.Get(SmallTimeBully);
        Check(bully.GoldCost == 2 && bully.GoldProduction == 2 && Keywords(SmallTimeBully) == "Retainer", "Small-Time Bully");
        Abilities(SmallTimeBully, "small-time-bully-dishonor");
        var farm = CardCatalog.Get(VersatileFarm);
        Check(farm.GoldCost == 2 && farm.GoldProduction == 0 && Keywords(VersatileFarm) == "Farm", "Versatile Farm");
        Abilities(VersatileFarm, "versatile-farm-search");
        var archive = CardCatalog.Get(WhisperingArchive);
        Check(archive.GoldCost == 0 && archive.GoldProduction == 0 && archive.Effect == "whispering-archive-spell-discount", "Whispering Archive");
        Abilities(WhisperingArchive);
        Manifest(root, "holding-wave-12", Fortress, PlatinumMine, ShelteredPort, SmallTimeBully, VersatileFarm, WhisperingArchive);

        var crew = CardCatalog.Get(ClearingCrew);
        Check(crew.Type == CardType.Follower && crew.GoldCost == 2 && crew.Force == 2 && crew.HonorRequirement == 1 && crew.Focus == 2 && Keywords(ClearingCrew) == "Ashigaru" && crew.Effect == "clearing-crew-draw", "Clearing Crew");
        Abilities(ClearingCrew);
        var caravan = CardCatalog.Get(ElephantCaravan);
        Check(caravan.GoldCost == 9 && caravan.Force == 5 && caravan.HonorRequirement == 0 && caravan.Focus == 1 && Keywords(ElephantCaravan) == "Cavalry,Elephant,Nonhuman", "Elephant Caravan");
        Abilities(ElephantCaravan, "elephant-caravan-fear");
        Check(AbilityCatalog.All["elephant-caravan-fear"].ActionKeywords!.SequenceEqual(["Fear"]), "a Fear action");
        var shieldman = CardCatalog.Get(Shieldman);
        Check(shieldman.GoldCost == 0 && shieldman.Force == 3 && shieldman.HonorRequirement == 1 && shieldman.Focus == 3 && Keywords(Shieldman) == "" && shieldman.Effect == "shieldman"
            && shieldman is { AttackDefenseKind: "Both", AttackDefenseAmount: -3, AttackDefenseScope: "Unit" }, "Shieldman");
        Abilities(Shieldman, "shieldman-honor");
        var scout = CardCatalog.Get(WastelandScout);
        Check(scout.GoldCost == 4 && scout.Force == 3 && scout.HonorRequirement == 1 && scout.Focus == 2 && Keywords(WastelandScout) == "Cavalry,Scout", "Wasteland Scout");
        Abilities(WastelandScout, "wasteland-scout-recon");
        Manifest(root, "follower-wave-30", ClearingCrew, ElephantCaravan, Shieldman, WastelandScout);

        foreach (var (id, effect, keywords) in new[] { (DiscoveringTheTemple, "discovering-the-temple", "Unique"), (On, "on-honor-limit", "Unique"),
                     (StarCrossedLovers, "star-crossed-lovers", "Political,Unique"), (WartimePreparations, "wartime-preparations", "Unique") })
        {
            var ev = CardCatalog.Get(id);
            Check(ev.Type == CardType.Event && Keywords(id) == keywords && ev.Effect == effect, ev.Name);
            Abilities(id);
        }
        Manifest(root, "event-wave-7", DiscoveringTheTemple, On, StarCrossedLovers, WartimePreparations);

        var tower = CardCatalog.Get(BishamonsTower);
        Check(tower.Type == CardType.Stronghold && tower.Clan == "Crab" && tower.ProvinceStrength == 8 && tower.GoldProduction == 4 && tower.StartingHonor == 3, "Bishamon's Tower");
        Abilities(BishamonsTower, "bishamon-tower-destroy");
        var forks = CardCatalog.Get(TwinForksCity);
        Check(forks.Type == CardType.Stronghold && forks.Clan == "Crane" && forks.ProvinceStrength == 6 && forks.GoldProduction == 4 && forks.StartingHonor == 6 && forks.Effect == "twin-forks-city", "Twin Forks City");
        Abilities(TwinForksCity);
        Manifest(root, "stronghold-wave-16", BishamonsTower, TwinForksCity);

        var moon = CardCatalog.Get(MoonsImperative);
        Check(moon.Type == CardType.Celestial && Keywords(MoonsImperative) == "Obsidian,Unique", "The Moon's Imperative");
        Abilities(MoonsImperative, "moons-imperative-holding", "moons-imperative-discard");
        var sun = CardCatalog.Get(SunsAspiration);
        Check(sun.Type == CardType.Celestial && Keywords(SunsAspiration) == "Jade,Unique", "The Sun's Aspiration");
        Abilities(SunsAspiration, "suns-aspiration-holding", "suns-aspiration-shuffle");
        Manifest(root, "celestial-wave-7", MoonsImperative, SunsAspiration);
    }

    // ---- Holdings ---------------------------------------------------------------------------------------------------------------------------------
    private static void PlatinumMineProduction()
    {
        var s = Table(Phase.Action, 0);
        var mine = Add(s, PlatinumMine, 0, Zone.Home);
        var follower = Add(s, ElephantCaravan, 0, Zone.Hand);
        var recruit = FaceUpIn(s, "training-crane-samurai", 0, 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(mine.Id, 0), 5, "5 Gold ordinarily");
        Eq(e.GoldProduced(mine.Id, 0, new PaymentSubject(e.State.Cards[follower.Id], PaymentKind.Equip)), 6, "6 Gold when paying for an attachment");
        Eq(e.GoldProduced(mine.Id, 0, new PaymentSubject(e.State.Cards[recruit.Id], PaymentKind.Recruit)), 5, "5 Gold for a recruit");
        Eq(e.GoldProduced(mine.Id, 0, new PaymentSubject(e.State.Cards[mine.Id], PaymentKind.Action)), 5, "5 Gold for an action");
        // A 6-Gold attachment is paid by the Mine alone.
        Check(e.PaymentOptions(0, 6, new PaymentSubject(e.State.Cards[follower.Id], PaymentKind.Equip)).Any(p => p.SourceIds.SequenceEqual([mine.Id])), "the Mine alone pays 6 for an attachment");
        Check(!e.PaymentOptions(0, 6).Any(p => p.SourceIds.SequenceEqual([mine.Id])), "but not 6 for anything else");

        // Equip (Battle): the Spell it attaches is an attachment paid for.
        var s2 = Table(Phase.Battle, 1);
        var mine2 = Add(s2, PlatinumMine, 1, Zone.Home);
        var shugenja = Add(s2, "training-shugenja", 1, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var spell = Add(s2, PearlOfRage, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(Published(s2));
        var quote = e2.QueryActions(1).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "equip-battle" && a.Enabled);
        Accept(e2, quote.Command! with { TargetId = spell.Id, PerformerId = shugenja.Id, SourceIds = [mine2.Id] });
        Eq(e2.State.Cards[spell.Id].Zone, Zone.Attached, "the Spell attached, paid by the Mine");
    }

    private static void ShelteredPortProduction()
    {
        var s = Table(Phase.Action, 0);
        var port = Add(s, ShelteredPort, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(port.Id, 0), 2, "alone: 2 Gold");
        var market = Add(s, "training-marketplace", 0, Zone.Home); GiveKeyword(market, "Market");
        var otherPort = Add(s, "training-farm", 0, Zone.Home, bowed: true); GiveKeyword(otherPort, "Port");
        Add(s, "training-farm", 0, Zone.Home); // neither
        var enemyPort = Add(s, "training-farm", 1, Zone.Home); GiveKeyword(enemyPort, "Port");
        var e2 = MatchEngine.FromTestState(s);
        Eq(e2.GoldProduced(port.Id, 0), 4, "plus one for each other Market or Port its controller controls (bowed or not), not the opponent's");
    }

    private static void WhisperingArchiveDiscount()
    {
        GoldQuote Quote(bool archive)
        {
            var s = Table(Phase.Battle, 1);
            if (archive) Add(s, WhisperingArchive, 1, Zone.Home);
            Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, PearlOfRage, 1, Zone.Hand);
            var e = MatchEngine.FromTestState(Published(s));
            return e.QueryActions(1).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "equip-battle" && a.Enabled).Gold!;
        }
        Eq(Quote(false).Cost, 2, "Pearl of Rage costs 2");
        Eq(Quote(true).Cost, 1, "with Whispering Archive: 1");
        // A Follower is not a Spell.
        var s2 = Table(Phase.Action, 0);
        Add(s2, WhisperingArchive, 0, Zone.Home);
        Add(s2, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s2, "training-follower", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.QueryActions(0).First(a => a.Type == "equip" && a.CardId == follower.Id).Gold!.Cost, 2, "a Follower pays in full");
    }

    private static void SmallTimeBullyEntry()
    {
        MatchState Setup(bool courtier, out CardInstance bully, out CardInstance victim) => Setup2(courtier, out bully, out victim, out _);
        MatchState Setup2(bool courtier, out CardInstance bully, out CardInstance victim, out CardInstance mine)
        {
            var s = Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0;
            bully = FaceUpIn(s, SmallTimeBully, 0, 0);
            mine = Add(s, "training-crane-samurai", 0, Zone.Home);
            if (courtier) GiveKeyword(mine, "Courtier");
            victim = Add(s, "training-lion-samurai", 1, Zone.Home);
            return s;
        }
        var s1 = Setup(true, out var bully1, out var victim1);
        var e = MatchEngine.FromTestState(s1);
        Accept(e, new GameCommand("purchase", 0, e.Version, bully1.Id, e.QuotePurchase(0, bully1.Id).Payments[0].SourceIds));
        Check(e.State.Cards[bully1.Id].Bowed && e.State.Cards[bully1.Id].Zone == Zone.Home, "it entered play bowed");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "its entry window is open");
        Accept(e, Offer(e, 0, "small-time-bully-dishonor", bully1.Id, victim1.Id));
        Check(e.State.Cards[victim1.Id].Dishonorable, "the chosen Personality was dishonored");
        // Without a Courtier or Magistrate it is not offered.
        var s2 = Setup(false, out var bully2, out _);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, bully2.Id, e2.QuotePurchase(0, bully2.Id).Payments[0].SourceIds));
        Check(!CanUse(e2, 0, "small-time-bully-dishonor", bully2.Id), "no Courtier or Magistrate: nothing to dishonor");
        // Magistrate counts too, and passing is allowed (it is a "may").
        var s3 = Setup2(false, out var bully3, out var victim3, out var magistrate);
        GiveKeyword(magistrate, "Magistrate");
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, new GameCommand("purchase", 0, e3.Version, bully3.Id, e3.QuotePurchase(0, bully3.Id).Payments[0].SourceIds));
        Check(CanUse(e3, 0, "small-time-bully-dishonor", bully3.Id), "a Magistrate also qualifies");
        Decline(e3);
        Check(!e3.State.Cards[victim3.Id].Dishonorable, "declined: nobody is dishonored");
    }

    private static void VersatileFarmSearch()
    {
        // A 2-Gold Farm: free after the discount, it enters play unbowed; the Unique Farm and another Versatile Farm are not eligible.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var farm = Add(s, VersatileFarm, 0, Zone.Home);
        var deck = s.Players[0].DynastyDeck;
        s.Cards[deck[0]].DefinitionId = VersatileFarm;
        s.Cards[deck[1]].DefinitionId = "training-farm";
        int HomeFarms(MatchEngine m) => m.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Home && c.DefinitionId == "training-farm" && !c.Bowed);
        var e = MatchEngine.FromTestState(s);
        var farmsBefore = HomeFarms(e);
        var deckBefore = e.State.Players[0].DynastyDeck.Count;
        Accept(e, Use(e, 0, "versatile-farm-search", farm.Id));
        Eq(e.State.Cards[farm.Id].Zone, Zone.DynastyDiscard, "Versatile Farm was destroyed as the cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search is open");
        var view = e.GetView(0).PendingSearch!;
        Check(view.Entries!.Single(x => x.DefinitionId == VersatileFarm) is { Eligible: false }, "a Farm with the same title is not eligible");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "training-farm"));
        Eq(HomeFarms(e), farmsBefore + 1, "a training Farm entered play unbowed, for 0 Gold");
        Eq(e.State.Players[0].DynastyDeck.Count, deckBefore - 1, "it left the deck");
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "searchRecruitPay", "nothing was left to pay");

        // A 6-Gold Farm costs 4: the searcher pays for it (or declines, and it stays in the deck).
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var farm2 = Add(s2, VersatileFarm, 0, Zone.Home);
        for (var i = 0; i < 2; i++) Add(s2, "training-marketplace", 0, Zone.Home);
        var expensive = s2.Cards[s2.Players[0].DynastyDeck[0]]; expensive.DefinitionId = "training-expensive-holding"; GiveKeyword(expensive, "Farm");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "versatile-farm-search", farm2.Id));
        Accept(e2, new GameCommand("selectSearchCard", 0, e2.Version, Choice: "training-expensive-holding"));
        Eq(e2.State.PendingChoice?.Type, "searchRecruitPay", "it must be paid for");
        var pay = e2.QueryActions(0).First(a => a.Type == "chooseSearchPayment" && a.Enabled && a.Command!.Choice is null);
        Eq(pay.Gold!.Cost, 4, "6 less 2"); QuoteCarriesSources(pay.Gold!, "search recruit (holding)");
        var e3 = MatchEngine.FromTestState(e2.State);
        Accept(e2, pay.Command!);
        Check(e2.State.Cards[expensive.Id].Zone == Zone.Home && !e2.State.Cards[expensive.Id].Bowed, "paid: in play, unbowed");
        Accept(e3, new GameCommand("chooseSearchPayment", 0, e3.Version, Choice: "decline"));
        Check(e3.State.Cards[expensive.Id].Zone == Zone.DynastyDeck && e3.State.Players[0].DynastyDeck.Contains(expensive.Id), "declined: it stays in the deck");
    }

    private static void FortressOfLidlessEyes_()
    {
        var s = Published(Table(Phase.Action, 0)); s.PriorityPlayerId = 0;
        var fortress = Add(s, Fortress, 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "fortress-lidless-bow", fortress.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "he was bowed");
        // His own Straighten Phase does not straighten him while the Fortress is in play.
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Check(e.State.Cards[enemy.Id].Bowed, "still bowed after his controller's Straighten Phase");
        // Once per game.
        var later = Edit(e, st => { st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.TurnNumber += 1; });
        Check(!CanUse(later, 0, "fortress-lidless-bow", fortress.Id), "once per game");
        // Any player has the destroy ability while it is in play: the opponent pays 6 Gold.
        var destroy = Opt(e, 1, "fortress-lidless-destroy");
        Eq(destroy.Gold!.Cost, 6, "6 Gold");
        Accept(e, destroy.Command! with { TargetId = fortress.Id });
        Check(e.State.Cards[fortress.Id].Zone == Zone.DynastyDiscard, "the Fortress was destroyed");
        Check(Listed(e, 1, "fortress-lidless-destroy").Length == 0, "with no Fortress in play the ability is not even listed");
        // Its hold went with it: he straightens in his next Straighten Phase.
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.TurnNumber > later.State.TurnNumber + 1);
        Check(!e.State.Cards[enemy.Id].Bowed, "free again once the Fortress left play");
        // Not listed at all without a Fortress anywhere.
        var s2 = Published(Table(Phase.Action, 0)); s2.PriorityPlayerId = 0;
        var e2 = MatchEngine.FromTestState(s2);
        Check(Listed(e2, 0, "fortress-lidless-destroy").Length == 0, "dormant without a Fortress");
    }

    // ---- Celestials ---------------------------------------------------------------------------------------------------------------------------------
    private static void MoonAndSun()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var moon = Add(s, MoonsImperative, 0, Zone.Celestial);
        var sun = Add(s, SunsAspiration, 1, Zone.Celestial);
        var enemyFarm = Add(s, "training-farm", 1, Zone.Home);
        var ownFarm = Add(s, "training-farm", 0, Zone.Home);
        var unique = Add(s, Fortress, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Listed(e, 0, "moons-imperative-holding").Any(a => a.Enabled && (a.TargetIds ?? []).Contains(unique.Id)), "a Unique Holding is not a target");
        Accept(e, Offer(e, 0, "moons-imperative-holding", moon.Id, enemyFarm.Id));
        Eq(e.GoldProduced(enemyFarm.Id, 1), 1, "the Moon: 1 less");
        Accept(e, Offer(e, 1, "suns-aspiration-holding", sun.Id, ownFarm.Id));
        Eq(e.GoldProduced(ownFarm.Id, 0), 3, "the Sun: 1 more (either player's Holding)");
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Eq(e.GoldProduced(enemyFarm.Id, 1), 2, "for the turn only");

        // The Moon's second Open: one to three face-up Personalities in provinces are discarded and each province refilled face-up.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var moon2 = Add(s2, MoonsImperative, 0, Zone.Celestial);
        var theirs = FaceUpIn(s2, "training-lion-samurai", 1, 1);
        var mine = FaceUpIn(s2, "training-crane-samurai", 0, 2);
        var hidden = FaceDownIn(s2, "training-crane-samurai", 0, 3);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.Execute(Use(e2, 0, "moons-imperative-discard", moon2.Id, selections: [Pick("personalities", hidden.Id)])) is { Accepted: false }, "a face-down card is not a target");
        var offered = Opt(e2, 0, "moons-imperative-discard").Slots!.Single();
        Check(offered.CardIds.Contains(theirs.Id) && offered.CardIds.Contains(mine.Id) && !offered.CardIds.Contains(hidden.Id), "both players' face-up province Personalities are offered");
        var nextTheirs = e2.State.Players[1].DynastyDeck[0]; var nextMine = e2.State.Players[0].DynastyDeck[0];
        Accept(e2, Use(e2, 0, "moons-imperative-discard", moon2.Id, selections: [Pick("personalities", theirs.Id, mine.Id)]));
        Eq(e2.State.Cards[moon2.Id].Zone, Zone.RemovedFromGame, "the Celestial was removed from the game");
        Check(e2.State.Cards[theirs.Id].Zone == Zone.DynastyDiscard && e2.State.Cards[mine.Id].Zone == Zone.DynastyDiscard, "both were discarded");
        Check(e2.State.Players[1].Provinces[1].CardId == nextTheirs && e2.State.Cards[nextTheirs].FaceUp && e2.State.Players[0].Provinces[2].CardId == nextMine && e2.State.Cards[nextMine].FaceUp,
            "each province was refilled face-up from its owner's deck");

        // The Sun's second Open: one to three cards in its controller's discard piles go into their decks.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var sun3 = Add(s3, SunsAspiration, 0, Zone.Celestial);
        var fate = Add(s3, "training-follower", 0, Zone.FateDiscard);
        var dynasty = Add(s3, "training-farm", 0, Zone.DynastyDiscard);
        var dead = Add(s3, "training-crane-samurai", 0, Zone.Dead);
        var foreign = Add(s3, "training-follower", 1, Zone.FateDiscard);
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.Execute(Use(e3, 0, "suns-aspiration-shuffle", sun3.Id, selections: [Pick("cards", foreign.Id)])) is { Accepted: false }, "not another player's discard pile");
        Accept(e3, Use(e3, 0, "suns-aspiration-shuffle", sun3.Id, selections: [Pick("cards", fate.Id, dynasty.Id, dead.Id)]));
        Check(e3.State.Players[0].FateDeck.Contains(fate.Id) && e3.State.Cards[fate.Id].Zone == Zone.FateDeck, "the Fate card went into the Fate deck");
        Check(e3.State.Players[0].DynastyDeck.Contains(dynasty.Id) && e3.State.Players[0].DynastyDeck.Contains(dead.Id), "the Holding and the dead Personality went into the Dynasty deck");
        Eq(e3.State.Cards[sun3.Id].Zone, Zone.RemovedFromGame, "the Celestial was removed from the game");
    }

    // ---- Followers ---------------------------------------------------------------------------------------------------------------------------------
    private static void ClearingCrewDraw()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var crew = Add(s, ClearingCrew, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: crew.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds, TargetId: host.Id));
        Decline(e);
        Eq(e.State.ExtraEndPhaseDraws[0], 1, "an additional card is owed for the End Phase");
        var hand = e.State.Players[0].Hand.Count;
        RunUntil(e, () => e.State.ActivePlayerId == 1);
        Eq(e.State.Players[0].Hand.Count, hand + 2, "the ordinary End Phase draw and the additional one");

        // Attached from a discard pile: not an entry from the hand.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var attacher = Add(s2, "training-discard-attacher", 0, Zone.Home);
        var crew2 = Add(s2, ClearingCrew, 0, Zone.FateDiscard);
        var e2 = MatchEngine.FromTestState(s2);
        var attach = Listed(e2, 0, "training-attach-follower-free").First(a => a.Enabled && (a.TargetIds ?? []).Contains(crew2.Id));
        Accept(e2, attach.Command! with { TargetId = crew2.Id });
        Decline(e2);
        Eq(e2.State.Cards[crew2.Id].Zone, Zone.Attached, "attached from the discard pile");
        Eq(e2.State.ExtraEndPhaseDraws[0], 0, "no additional card");
    }

    private static void ElephantCaravanFear()
    {
        MatchState Setup(out CardInstance caravan, out CardInstance bowed, out CardInstance unbowed, out CardInstance follower, out CardInstance strong)
        {
            var s = Table(Phase.Battle, 1);
            var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            caravan = Add(s, ElephantCaravan, 1, Zone.Attached, province: 0, parent: host.Id);
            bowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
            unbowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var enemyHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: enemyHost.Id);
            strong = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(strong, 10);
            return s;
        }
        var e = MatchEngine.FromTestState(Setup(out var caravan, out var bowed, out var unbowed, out var follower, out var strong));
        Check(!Options(e, 1, "elephant-caravan-fear", caravan.Id).Any(a => a.Enabled && (a.TargetIds ?? []).Contains(strong.Id)), "a card with higher Force is not a target");
        Accept(e, Offer(e, 1, "elephant-caravan-fear", caravan.Id, bowed.Id));
        Check(e.State.Cards[bowed.Id].Zone == Zone.Dead, "a bowed Personality is destroyed");
        var e2 = MatchEngine.FromTestState(Setup(out caravan, out _, out unbowed, out _, out _));
        Accept(e2, Offer(e2, 1, "elephant-caravan-fear", caravan.Id, unbowed.Id));
        Check(e2.State.Cards[unbowed.Id].Zone == Zone.Battlefield && e2.State.Cards[unbowed.Id].Bowed, "an unbowed Personality is bowed, not destroyed");
        var e3 = MatchEngine.FromTestState(Setup(out caravan, out _, out _, out follower, out _));
        Accept(e3, Offer(e3, 1, "elephant-caravan-fear", caravan.Id, follower.Id));
        Eq(e3.State.Cards[follower.Id].Zone, Zone.FateDiscard, "an attachment is destroyed");
    }

    private static void WastelandScoutRecon()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var scout = Add(s, WastelandScout, 0, Zone.Attached, province: 0, parent: host.Id);
        var sibling = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var otherFollower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: other.Id);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home);
        var homeFollower = Add(s, "training-follower", 0, Zone.Attached, parent: home.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        Accept(e, Use(e, 0, "wasteland-scout-recon", scout.Id));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "its controller has Reconnaissance at the battlefield");
        int Bonus(CardInstance c) => e.State.Cards[c.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);
        Eq(Bonus(sibling), 1, "the other Follower in its unit: +1F");
        Eq(Bonus(otherFollower), 1, "another unit's Follower there: +1F");
        Eq(Bonus(scout), 0, "not itself");
        Eq(Bonus(homeFollower), 0, "not a Follower at home");
        Eq(Bonus(enemyFollower), 0, "not the enemy's");
    }

    private static void ShieldmanUnit()
    {
        // Assignment: the unit may defend but not attack.
        var s = Table(Phase.InfantryDeclaration, 0); s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [];
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, Shieldman, 0, Zone.Attached, parent: host.Id);
        var free = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Rejection(e, new GameCommand("attack", 0, e.Version, Assignments: new() { [host.Id] = 0 })).Contains("Shieldman"), "a Shieldman unit may not be assigned to attack");
        // Issue #1115: the refusal is the general "barred by" sentence, which a client turns into a dialog.
        var refusal = BarNotice.TryParse(Rejection(e, new GameCommand("attack", 0, e.Version, Assignments: new() { [host.Id] = 0 })));
        Check(refusal == new BarNotice("Assigning to attack or moving into an attacking army", "Shieldman") && refusal.Message == "Assigning to attack or moving into an attacking army is barred by Shieldman.", "the refusal names Shieldman as the bar");
        Check(BarNotice.TryParse("Choose a target.") is null && BarNotice.TryParse("is barred by .") is null, "any other sentence is not a bar");
        Accept(e, new GameCommand("attack", 0, e.Version, Assignments: new() { [free.Id] = 0 }));
        var sd = Table(Phase.DefenseAssignment, 1); sd.PriorityPlayerId = 0; sd.CurrentBattleProvince = null; sd.UnresolvedBattlefields = [0];
        Add(sd, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(sd, "training-crane-samurai", 0, Zone.Home);
        Add(sd, Shieldman, 0, Zone.Attached, parent: defender.Id);
        var ed = MatchEngine.FromTestState(sd);
        Accept(ed, new GameCommand("assignDefense", 0, ed.Version, Assignments: new() { [defender.Id] = 0 }));
        Eq(ed.State.Cards[defender.Id].Zone, Zone.Battlefield, "it defends");

        // Another player's action destroys another card of its unit: its controller may gain 2 Honor.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var mine = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var shieldman = Add(s2, Shieldman, 0, Zone.Attached, province: 0, parent: mine.Id);
        var victim = Add(s2, "training-follower", 0, Zone.Attached, province: 0, parent: mine.Id);
        var enemyHost = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var caravan = Add(s2, ElephantCaravan, 1, Zone.Attached, province: 0, parent: enemyHost.Id);
        var e2 = MatchEngine.FromTestState(s2);
        var honor = Honor(e2, 0);
        Accept(e2, Offer(e2, 1, "elephant-caravan-fear", caravan.Id, victim.Id));
        Eq(e2.State.Cards[victim.Id].Zone, Zone.FateDiscard, "the other Follower was destroyed");
        Eq(e2.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens");
        Accept(e2, Use(e2, 0, "shieldman-honor", shieldman.Id));
        Eq(Honor(e2, 0), honor + 2, "2 Honor");
        // Its own controller's action does not count.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var mine3 = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var shieldman3 = Add(s3, Shieldman, 0, Zone.Attached, province: 0, parent: mine3.Id);
        var caravan3 = Add(s3, ElephantCaravan, 0, Zone.Attached, province: 0, parent: mine3.Id);
        var enemy3 = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower3 = Add(s3, "training-follower", 1, Zone.Attached, province: 0, parent: enemy3.Id);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "elephant-caravan-fear", caravan3.Id, enemyFollower3.Id));
        Check(!CanUse(e3, 0, "shieldman-honor", shieldman3.Id), "an enemy card destroyed by its controller's own action is not one of its unit");

        // Attacks targeting its unit have -3 strength: Horse Archers' Ranged 4 cannot reach a 2F Follower of the unit, and reaches it without Shieldman.
        MatchEngine Archery(bool guarded, out CardInstance archers, out CardInstance target)
        {
            var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
            var archerHost = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            archers = Add(s4, "oracle-l5r-3456", 0, Zone.Attached, province: 0, parent: archerHost.Id);
            var defenderHost = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (guarded) Add(s4, Shieldman, 1, Zone.Attached, province: 0, parent: defenderHost.Id);
            target = Add(s4, "training-follower", 1, Zone.Attached, province: 0, parent: defenderHost.Id);
            return MatchEngine.FromTestState(s4);
        }
        var e4 = Archery(true, out var archers4, out var target4);
        Accept(e4, new GameCommand("useAbility", 0, e4.Version, CardId: archers4.Id, TargetId: target4.Id, AbilityId: "horse-archers-attack")); // still a legal target, just above the reduced strength
        Eq(e4.State.Cards[target4.Id].Zone, Zone.Attached, "the guarded Follower survives the -3 strength Attack");
        var e5 = Archery(false, out var archers5, out var target5);
        Accept(e5, new GameCommand("useAbility", 0, e5.Version, CardId: archers5.Id, TargetId: target5.Id, AbilityId: "horse-archers-attack"));
        Check(e5.State.Cards[target5.Id].Zone != Zone.Attached, "without Shieldman the Follower is destroyed");
    }

    // ---- Strongholds -------------------------------------------------------------------------------------------------------------------------------
    private static void BishamonsTowerDestroy()
    {
        MatchState Setup(out CardInstance tower, out CardInstance berserker, out CardInstance plain, out CardInstance weak, out CardInstance equal, out CardInstance equipped)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            tower = s.Cards[s.Players[1].StrongholdId]; tower.DefinitionId = BishamonsTower;
            berserker = Add(s, "training-berserker", 1, Zone.Battlefield, bowed: true, province: 0); Force(berserker, 3); // 5F, bowed
            plain = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0); // bowed, not a Berserker
            weak = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            equal = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(equal, 5 - CardCatalog.Get("training-crane-samurai").Force);
            equipped = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: equipped.Id);
            return s;
        }
        var e = MatchEngine.FromTestState(Setup(out var tower, out var berserker, out var plain, out var weak, out var equal, out var equipped));
        Eq(e.State.Cards[equal.Id].Modifiers.Sum(m => m.Amount) + CardCatalog.Get("training-crane-samurai").Force, 5, "fixture: the equal card has 5F");
        var byBerserker = Options(e, 1, "bishamon-tower-destroy", tower.Id, berserker.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(byBerserker.Contains(weak.Id) && !byBerserker.Contains(equal.Id) && !byBerserker.Contains(equipped.Id), "lower Force and no attachments only");
        Check(!Options(e, 1, "bishamon-tower-destroy", tower.Id, plain.Id).Any(a => a.Enabled), "a bowed non-Berserker may not perform it");
        Accept(e, Offer(e, 1, "bishamon-tower-destroy", tower.Id, weak.Id, performerId: berserker.Id));
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "destroyed by a bowed Berserker");
        // An unbowed Personality performs it too.
        var s2 = Setup(out var tower2, out _, out var plain2, out var weak2, out _, out _); plain2.Bowed = false;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "bishamon-tower-destroy", tower2.Id, weak2.Id, performerId: plain2.Id));
        Eq(e2.State.Cards[weak2.Id].Zone, Zone.Dead, "destroyed by an unbowed Personality");
    }

    private static void TwinForksCityShield()
    {
        MatchState Setup(out CardInstance winner, out CardInstance loser, out CardInstance duel, out CardInstance bower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            s.Cards[s.Players[0].StrongholdId].DefinitionId = TwinForksCity;
            winner = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); winner.Modifiers.Add(new("test", "test", "Force", 20));
            loser = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            duel = Add(s, "training-duel-force", 0, Zone.Hand);
            bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return s;
        }
        void Fight(MatchEngine e, CardInstance duel, CardInstance winner, CardInstance loser)
        {
            Accept(e, Offer(e, 0, "training-duel-force", duel.Id, loser.Id, performerId: winner.Id));
            for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
                Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
            Decline(e);
        }
        var e = MatchEngine.FromTestState(Setup(out var winner, out var loser, out var duel, out var bower));
        Fight(e, duel, winner, loser);
        Check(e.State.Cards[winner.Id].AbilityUses.ContainsKey("twin-forks-city:shield"), "the winner is shielded");
        var e1 = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e1, Offer(e1, 1, "training-bow-at-battle", bower.Id, winner.Id));
        Check(!e1.State.Cards[winner.Id].Bowed, "the next bowing from another player's card is negated");
        Check(!e1.State.Cards[winner.Id].AbilityUses.ContainsKey("twin-forks-city:shield"), "and the shield is spent");
        // Without Twin Forks City the winner is bowed as usual.
        var s2 = Setup(out var winner2, out var loser2, out var duel2, out var bower2);
        s2.Cards[s2.Players[0].StrongholdId].DefinitionId = CardCatalog.Get(s2.Cards[s2.Players[1].StrongholdId].DefinitionId).Id;
        var e2 = MatchEngine.FromTestState(s2);
        Fight(e2, duel2, winner2, loser2);
        var e2b = Edit(e2, st => st.PriorityPlayerId = 1);
        Accept(e2b, Offer(e2b, 1, "training-bow-at-battle", bower2.Id, winner2.Id));
        Check(e2b.State.Cards[winner2.Id].Bowed, "no shield: bowed");
    }

    // ---- Events ------------------------------------------------------------------------------------------------------------------------------------
    private static CardInstance BloodOfThePreserver(MatchState s, int owner, Zone zone)
    {
        var card = Add(s, "training-follower", owner, zone);
        card.Printed = CardCatalog.Get("training-follower") with { Name = "Blood of the Preserver" };
        return card;
    }

    private static void DiscoveringTheTempleSearch()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var temple = FaceDownIn(s, DiscoveringTheTemple, 1, 3);
        var blood = BloodOfThePreserver(s, 1, Zone.FateDiscard);
        Add(s, "training-follower", 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice?.Type == "searchDeck");
        var choices = e.QueryActions(1).Where(a => a.Type == "chooseSearchDeck").Select(a => a.Command!.Choice).ToArray();
        Check(choices.SequenceEqual([SearchDeck.Fate, SearchDeck.FateDiscard]), "the Fate deck or the Fate discard pile");
        Accept(e, new GameCommand("chooseSearchDeck", 1, e.Version, Choice: SearchDeck.FateDiscard));
        var view = e.GetView(1).PendingSearch!;
        Check(view.Entries!.Count(x => x.Eligible) == 1 && view.Entries!.Single(x => x.Eligible).Definition.Name == "Blood of the Preserver", "only the card titled Blood of the Preserver qualifies");
        Accept(e, new GameCommand("selectSearchCard", 1, e.Version, Choice: "training-follower"));
        Check(e.State.Players[1].Hand.Contains(blood.Id) && e.State.Cards[blood.Id].Zone == Zone.Hand, "it was put into the hand");
        Check(e.State.ShownCards.Any(c => c.Context.Contains("Fate discard pile")), "and shown");
        Eq(e.State.Cards[temple.Id].Zone, Zone.DynastyDiscard, "the Event resolved and was discarded");
        // None anywhere: the searcher passes.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        FaceDownIn(s2, DiscoveringTheTemple, 1, 3);
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.PendingChoice?.Type == "searchDeck");
        Accept(e2, new GameCommand("chooseSearchDeck", 1, e2.Version, Choice: SearchDeck.Fate));
        Accept(e2, new GameCommand("passSearch", 1, e2.Version));
        RunUntil(e2, () => e2.State.ActivePlayerId == 1 && e2.State.Phase == Phase.Action);
    }

    private static void OnHonorLimit()
    {
        // Revealed, it stays in play for good.
        var s0 = Table(Phase.Action, 0); s0.PriorityPlayerId = 0;
        var on0 = FaceDownIn(s0, On, 1, 3);
        var e0 = MatchEngine.FromTestState(s0);
        RunUntil(e0, () => e0.State.ActivePlayerId == 1 && e0.State.Phase == Phase.Action);
        Eq(e0.State.Cards[on0.Id].Zone, Zone.EventInPlay, "it stays in play");

        MatchEngine RunPhase(bool withOn, int gained, int battle)
        {
            var s = Table(L5R.Core.Phase.Action, 0); s.PriorityPlayerId = 0;
            if (withOn) Add(s, On, 1, Zone.EventInPlay);
            s.PhaseStartHonor = s.Players.Select(p => p.Honor).ToArray();
            s.Players[0].Honor += gained; s.Players[1].Honor += 2;
            s.PhaseBattleHonor[0] = battle;
            var e = MatchEngine.FromTestState(s);
            RunUntil(e, () => e.State.Phase != L5R.Core.Phase.Action);
            return e;
        }
        var start = Table(L5R.Core.Phase.Action, 0).Players.Select(p => p.Honor).ToArray();
        var limited = RunPhase(true, 7, 0);
        Eq(limited.State.Players[0].Honor, start[0] + 3, "7 gained this phase: lowered to the start plus 3");
        Eq(limited.State.Players[1].Honor, start[1] + 2, "2 gained: unchanged");
        Eq(limited.State.PhaseStartHonor![0], start[0] + 3, "the next phase starts from there");
        var battle = RunPhase(true, 7, 3);
        Eq(battle.State.Players[0].Honor, start[0] + 6, "Honor from battle resolution is added to the limit");
        var without = RunPhase(false, 7, 0);
        Eq(without.State.Players[0].Honor, start[0] + 7, "without On nothing is lowered");
        // A loss is never raised back.
        var s2 = Table(L5R.Core.Phase.Action, 0); s2.PriorityPlayerId = 0;
        Add(s2, On, 1, Zone.EventInPlay);
        s2.PhaseStartHonor = s2.Players.Select(p => p.Honor).ToArray(); s2.Players[0].Honor -= 4;
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.Phase != L5R.Core.Phase.Action);
        Eq(e2.State.Players[0].Honor, start[0] - 4, "a lower Honor stays as it is");
    }

    private static void WartimePreparationsNegations()
    {
        // Bowing: another player's first bowing effect this turn resolves, the second is negated.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 1;
        Add(s, WartimePreparations, 0, Zone.EventInPlay);
        var first = Add(s, "training-crane-samurai", 0, Zone.Home);
        var second = Add(s, "training-crane-samurai", 0, Zone.Home);
        var bower1 = Add(s, "training-card-bower", 1, Zone.Home);
        var bower2 = Add(s, "training-card-bower", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-enemy-card-open", bower1.Id, first.Id));
        Check(e.State.Cards[first.Id].Bowed, "the first is bowed");
        var e1 = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e1, Offer(e1, 1, "training-bow-enemy-card-open", bower2.Id, second.Id));
        Check(!e1.State.Cards[second.Id].Bowed, "the second is negated");

        // Assignment: another player's second assignment-preventing effect this turn is negated.
        var s2 = Published(Table(Phase.Action, 0)); s2.PriorityPlayerId = 1;
        Add(s2, WartimePreparations, 0, Zone.EventInPlay);
        var a = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var b = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Home);
        for (var i = 0; i < 5; i++) Add(s2, "training-marketplace", 1, Zone.Home);
        var evil1 = Add(s2, NecessaryEvil, 1, Zone.Hand);
        var evil2 = Add(s2, NecessaryEvil, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "necessary-evil-lock", evil1.Id, a.Id));
        Check(e2.State.Cards[a.Id].MovementLocks.Any(), "the first lock holds");
        var e2b = Edit(e2, st => st.PriorityPlayerId = 1);
        Accept(e2b, Offer(e2b, 1, "necessary-evil-lock", evil2.Id, b.Id));
        Check(!e2b.State.Cards[b.Id].MovementLocks.Any(), "the second is negated");
    }

    private static void StarCrossedLoversPair()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        FaceDownIn(s, StarCrossedLovers, 1, 3);
        var romeo = Add(s, "training-crane-samurai", 0, Zone.Home);
        var juliet = Add(s, "training-crane-samurai", 0, Zone.Home);
        var third = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice?.Type == "starCrossedLovers");
        var pairs = e.QueryActions(1).Where(o => o.Type == "starCrossedLovers").ToArray();
        Eq(pairs.Length, 3, "every pair of Personalities one player controls (the opponent's three)");
        Check(e.Execute(new GameCommand("starCrossedLovers", 1, e.Version, CardIds: [romeo.Id, romeo.Id])) is { Accepted: false }, "two different Personalities");
        Accept(e, new GameCommand("starCrossedLovers", 1, e.Version, CardIds: [romeo.Id, juliet.Id]));
        Check(e.State.StarCrossedPairs.Single().OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { romeo.Id, juliet.Id }.OrderBy(x => x, StringComparer.Ordinal)), "the pair is kept for the game");
        // Assignment: never to each other's battlefield, in one command or after the other.
        var st = e.State;
        st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null;
        foreach (var id in new[] { romeo.Id, juliet.Id, third.Id }) { st.Cards[id].Bowed = false; st.Cards[id].Zone = Zone.Home; st.Cards[id].ProvinceIndex = null; }
        var open = st.Players[1].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        var ea = MatchEngine.FromTestState(st);
        var together = Rejection(ea, new GameCommand("attack", 0, ea.Version, Assignments: new() { [romeo.Id] = open[0], [juliet.Id] = open[0] }));
        Check(together.Contains("Star-Crossed"), "not together: " + together);
        Accept(ea, new GameCommand("attack", 0, ea.Version, Assignments: new() { [romeo.Id] = open[0], [juliet.Id] = open[1], [third.Id] = open[0] }));
        Check(ea.State.Cards[romeo.Id].ProvinceIndex == open[0] && ea.State.Cards[juliet.Id].ProvinceIndex == open[1], "apart is fine");
    }
}
