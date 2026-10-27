using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #833, single-reaction slice 6: cards that print exactly one Reaction (or entering-play trait modelled as one) whose own trigger window the generic probe (MultiWindowAuditTests)
// cannot open. Each test opens the real window through ordinary engine play (a purchase, an equip, a Recruit in battle, a Battle action, an engage ...), proves the ability is offered
// enabled to its own player only, refused for the other player, accepted with the printed effect asserted by number/zone, and that once the window is closed (or was never opened) the very
// same command is neither offered nor accepted.
internal static partial class SingleReaction6Tests
{
    public static void Run(TestRunner runner) => runner.Run("single-reaction-6", [
        ("Bayushi Wateru (wlpa-wateru-additional): recruited into a battle by either player she grants one additional Battle action to her controller; not in the Dynasty Phase nor with no window", WateruAdditional),
        ("Clay Kshatriya (clay-kshatriya-invest): paying 5 in his entry window gives two +1F tokens, to his controller only, once, not after the window", ClayKshatriyaInvest),
        ("Goju Asuka (goju-asuka-give): in her entry window her controller may give control of her to the other player; declining keeps her", GojuAsukaGive),
        ("Hantei XVI (w734-hantei-search): in his entry window a non-Unique Political Strategy is searched from the discard pile into the hand", HanteiSearch),
        ("Jade Pearl Inn (w739-inn-search): after it is recruited from a Province the Dynasty deck is searched and the Province refilled face up with a cost-1 Gold Holding", JadePearlInnSearch),
        ("Kitsu Tamao (wlpa-tamao-create): after she is recruited a 2F/2C/3PH Lion Clan Ancestor Samurai Spirit is created at home, once", KitsuTamaoCreate),
        ("Kitsune Hisano (hisano-create-guide): after she enters play the Mantis Fox Guide is created once per game, and its Battle ability moves an enemy attacker home", KitsuneHisanoCreate),
        ("Kitsune Yuribara (w741-yuribara-create): after she enters play a 0F/2C/3PH Mantis Butterfly Cavalry Spirit is created at home, once", YuribaraCreate),
        ("Small-Time Bully (small-time-bully-dishonor): after it enters play with a Courtier or Magistrate you may dishonor a Personality; not without one", SmallTimeBullyDishonor),
        ("Training Invest follower (training-invest): paying 3 in its entry window gives one +1F token, once, not after the window", TrainingInvest),
        ("Training personality-entry watcher (training-personality-entry-watch): attaches from hand to a Personality as it enters play, for its controller only", TrainingPersonalityEntryWatch),
        ("Shinjo Ki-Chang (kichang-exp-create): after your Personality enters play a 1F Cavalry Follower is created and attached to it, not to Ki-Chang", KiChangCreate),
        ("Burn the Towers (burn-towers-recon): in the Engage window either role's unbowed Scout gives Reconnaissance and the province -3 strength until end of turn", BurnTheTowersRecon),
        ("Surrounded! (p2-surrounded-recon): in the Engage window either role's unbowed Scout gives Reconnaissance and its player's card-effect Ranged Attacks +2 strength this battle", SurroundedRecon),
        ("Seeking the Truth (seeking-truth-recon): Reconnaissance, a non-Unique Battle Strategy from the discard pile to hand, and a random discard after a battle that destroyed nothing", SeekingTheTruthRecon),
        ("Torch's Flame Flickers (torch-flame-recon): in the Engage window either role's Scout (bowed or not) gives Reconnaissance and negates the next effect that bows his Personalities there", TorchsFlameFlickersRecon),
        ("The Golden Plains (golden-plains-recon): in the Engage window either role gets Reconnaissance and a Plain of Gold at the battlefield, none where a Terrain already is", GoldenPlainsRecon),
        ("Daigotsu Tenbatsu (tenbatsu-engage-look): after engaging at his battlefield, look at and reorder the top three Fate cards and optionally discard a Dark Virtue to draw", TenbatsuEngageLook),
        ("The Tower of Vigilance (w741-tower-engage): the Defender bows it in the Engage window to create a Guard at the battlefield, removed after the battle", TowerOfVigilanceEngage),
        ("Recovering What Was Lost (wl-sg-recovering-lost): in the Engage window moves your home Personality to an opposed battlefield, +2F if a Tactician", RecoveringWhatWasLost),
        ("Akodo Kobi (kobi-recruit): after another player's Battle action destroys your Lion Clan Personality it is queued and he is recruited free when the turn ends", AkodoKobiRecruit),
        ("Eternal Victory Dojo (eternal-victory-draw): after your Tactical action resolves a card is owed and drawn when the next End Phase begins", EternalVictoryDojoDraw),
        ("Measure of Devotion (measure-of-devotion-create): after a Battle action destroys your Deathseeker an Oathsworn Deathseeker is created at the battlefield and you gain 1 Honor", MeasureOfDevotionCreate),
        ("Shieldman (shieldman-honor): after another player's action destroys another card in its unit you gain 2 Honor", ShieldmanHonor),
        ("Isawa Kimi (kimi-exp2-discard): even bowed, after an action puts a card in another player's hand he chooses and discards one", IsawaKimiDiscard),
        ("Training after-action watcher (training-after-action-resolved-gain): after an action destroys your Personality you gain 3 Honor", TrainingAfterActionWatcher),
        ("Without Mercy (without-mercy-action): after your Samurai's Battle action that targeted no enemy card you take an additional Battle action", WithoutMercyAction),
        ("Bad Kharma (bad-kharma-negate): from the hand against another player's announced Strategy action when his Family Honor is -1 or lower, negating its effects and costs; not otherwise", BadKharmaNegate),
        ("Hida Fujita (fujita-negate-follower-destruction): at the battlefield, before another player's action resolves, negates the destruction of your Followers; not from home", HidaFujitaNegate),
        ("Improper Papers (wl-improper-papers): before an action resolves, a targeted Personality's movement from its effects is negated; naming one who is not moved changes nothing", ImproperPapersNegate),
        ("Moto Taigo (w737-taigo-interrupt): bowing him before an action that moves units lets you destroy a card without attachments in one of them", MotoTaigoInterrupt),
        ("Vigilant Riders (vigilant-riders-negate): after another player's action targets a card in its unit its destruction is negated; not for another unit", VigilantRidersNegate),
        ("Utaku Lishan (lishan-penalty): after you announce a Battle action she gives an opposing Personality or Follower a Force penalty equal to her Personal Honor (+1 attacking)", UtakuLishanPenalty),
        ("Kalani's Landing (w816-kalani-reduce): after either player's action is announced its controller gives a card in play with a Gold Cost -2 Gold Cost until end of turn, once per turn", KalanisLandingReduce),
        ("Archery Range (archery-range-reaction): either side's Ranged Attack held in its window gets +1 strength; not a Melee Attack", ArcheryRangeBoost),
        ("Luxurious Silk (luxurious-silk-gold): even bowed it pays 2 Gold for an action by its Reaction, once a turn, not for a recruit, only for its controller", LuxuriousSilkGold),
        ("One Koku (w829-koku-gold): in hand it is a 1 Gold payment source for its owner only, discarded when chosen", OneKokuGold),
        ("Rhetoric (rhetoric-redirect): before another player gains Honor from an action your bowed Courtier redirects the gain to a chosen player if his Chi covers it", RhetoricRedirect),
        ("Hofukushu's Vengeance (pe-hofukushus-vengeance-declare): the Defender discards it after an Attack Phase to declare an additional Attack Phase with one Personality", HofukushusVengeance),
        ("Treasure Hunter (promo-treasure-hunter-retire): after a battle in which its army destroyed a province it retires into a bowed Charter Holding; not for the Defender", TreasureHunterRetire),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }
    private static string Snapshot(MatchEngine e) => System.Text.Json.JsonSerializer.Serialize(e.State);

    // Who holds the open window: the player the engine lists a "pass" for (an announce window belongs to the card's controller, whoever announced).
    private static int Holder(MatchEngine e)
    {
        var first = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
        foreach (var p in new[] { first, 1 - first })
            if (e.QueryActions(p).Any(a => a.Type == "declineReaction" && a.Enabled)) return p;
        return first;
    }

    // Declines every window the engine has open (nobody here holds another Reaction), so the action or entry has fully resolved. Stops at a decision that is not a pass.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
        {
            var who = Holder(e);
            if (!e.QueryActions(who).Any(a => a.Type == "declineReaction" && a.Enabled)) return;
            Accept(e, new GameCommand("declineReaction", who, e.Version));
        }
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner and to nobody else, refused when the other player submits it; on a copy where the window is
    // declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`). windowType may list alternatives with '|'.
    private static GameCommand InWindow(MatchEngine e, string windowType, int owner, string ability, string cardId, string? performerId = null, string? targetId = null)
    {
        Check(windowType.Split('|').Contains(e.State.PendingChoice?.Type) || windowType == "entryReaction" && e.State.PendingChoice?.Type == "proclaim", ability + ": the window is " + e.State.PendingChoice?.Type + ", not " + windowType); // a Proclaim-eligible recruit shares its entry window with the Proclaim
        if (Holder(e) != owner) Accept(e, new GameCommand("declineReaction", Holder(e), e.Version)); // the other player holds the first opportunity and has nothing to react with
        Check(e.State.PendingChoice is not null, ability + ": the window closed when the other player declined");
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner}" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        var command = targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
        Check(!CanUse(e, 1 - owner, ability, cardId), ability + " is listed enabled for the other player");
        Reject(e, command with { PlayerId = 1 - owner });
        var closed = e.Clone();
        Settle(closed);
        Check(closed.State.PendingChoice is null, ability + ": the window did not close");
        Check(!CanUse(closed, owner, ability, cardId, performerId), ability + " is still offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        return command;
    }

    // A fresh position in which the card sits in play and no window was ever opened: not offered to either player, and its command is refused.
    private static void NeverOpened(MatchEngine e, string ability, string cardId, GameCommand command)
    {
        Check(e.State.PendingChoice is null, "a window is open");
        Check(!CanUse(e, 0, ability, cardId) && !CanUse(e, 1, ability, cardId), ability + " is offered with no window open");
        var before = Snapshot(e);
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = 0 });
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = 1 });
        Eq(Snapshot(e), before, ability + ": a refused command left the state alone");
    }

    // ---- entering-play windows ----------------------------------------------------------------------------------------------------------------------------------------------
    // A card bought from player 0's province 0 in the Dynasty Phase, leaving its entering-play window open.
    private static (MatchEngine E, string Id) Buy(string def, Action<MatchState>? tweak = null, int gold = 14)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = "Dragon"; s.Players[0].Honor = 30;
        Gold(s, 0, gold);
        var entering = s.Cards[s.Players[0].Provinces[0].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false });
        Accept(e, option.Command!);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"{def} bought, but the window is {e.State.PendingChoice?.Type}");
        return (e, entering.Id);
    }

    // The same card standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, out CardInstance card, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        card = Add(s, def, 0, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();

    private static void WateruAdditional()
    {
        const string ability = "wlpa-wateru-additional";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            // Recruited as a Reserve card into a battle (opposed by the other player's unit): her Ninja Interrupt window opens.
            var s = Table(Phase.Battle, owner); s.PriorityPlayerId = owner;
            var wateru = s.Cards[s.Players[owner].Provinces[0].CardId!]; wateru.DefinitionId = def; wateru.FaceUp = true;
            Gold(s, owner, 6);
            Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("recruitReserve", owner, e.Version, CardId: wateru.Id, SourceIds: e.PaymentOptions(owner, 8).First().SourceIds));
            Eq(e.State.Cards[wateru.Id].Zone, Zone.Battlefield, "Wateru was recruited into the battle");
            var command = InWindow(e, "entryReaction", owner, ability, wateru.Id);
            var grants = e.State.AdditionalActions.Count(g => g.PlayerId == owner);
            Accept(e, command);
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == owner), grants + 1, $"player {owner}: one additional Battle action was granted");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 1 - owner), 0, "the other player got none");
            Check(!CanUse(e, owner, ability, wateru.Id), "taken once for this entry");
        }
        // Recruited in the Dynasty Phase there is no battle: the Interrupt is not offered, the command is refused and nothing changes.
        var (d, id) = Buy(def);
        Check(!CanUse(d, 0, ability, id), "offered outside a battle");
        var before = Snapshot(d);
        Reject(d, Use(d, 0, ability, id));
        Eq(Snapshot(d), before, "the refused command changed nothing");
        Eq(d.State.AdditionalActions.Count, 0, "no grant outside a battle");
        // Standing in play with no entry window at all.
        var h = AtHome(def, out var card);
        NeverOpened(h, ability, card.Id, Use(h, 0, ability, card.Id));
    }

    private static void ClayKshatriyaInvest()
    {
        const string ability = "clay-kshatriya-invest";
        var (e, command) = GoldParityTests.ClayInvest();
        Check(e.State.PendingChoice?.Type == "entryReaction", "the Follower's entry window is open");
        var follower = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10678");
        Eq(follower.OwnerId, 0);
        var force = e.CardForceForTest(follower.Id);
        var pay = e.PaymentOptions(0, 5).First();
        Check(CanUse(e, 0, ability, follower.Id), "offered to its controller");
        Check(!CanUse(e, 1, ability, follower.Id), "not offered to the other player");
        Reject(e, command(pay.SourceIds) with { PlayerId = 1 });
        // The Invest cost is printed as 5 Gold: less is refused, state unchanged.
        var short4 = e.PaymentOptions(0, 4).First();
        if (short4.SourceIds.Length > 0 && short4.SourceIds.Sum(id => e.GoldProduced(id, 0)) < 5)
        {
            var before = Snapshot(e);
            Reject(e, command(short4.SourceIds));
            Eq(Snapshot(e), before, "an underpayment changed nothing");
        }
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, follower.Id), "no longer offered once the window is declined");
        Reject(closed, command(pay.SourceIds) with { ExpectedVersion = closed.Version });
        Accept(e, command(pay.SourceIds));
        Eq(e.CardForceForTest(follower.Id), force + 2, "two +1F tokens: +2 Force");
        Eq(e.State.Cards[follower.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 2, "exactly two +1F tokens");
        Check(pay.SourceIds.All(id => e.State.Cards[id].Bowed), "the 5 Gold was paid from the offered sources");
        Check(!CanUse(e, 0, ability, follower.Id), "once");
        // Standing in play with no window: not offered.
        var h = AtHome("oracle-l5r-10678", out var idle);
        NeverOpened(h, ability, idle.Id, command(pay.SourceIds) with { CardId = idle.Id });
    }

    private static void GojuAsukaGive()
    {
        const string ability = "goju-asuka-give";
        var (e, id) = Buy(Def(ability));
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var asuka = e.State.Cards[id];
        Check(asuka.OwnerId == 1 && asuka.OriginalOwnerId == 0 && asuka.Zone == Zone.Home, "control of her passed to the other player's home");
        Check(e.GetView(0).Players[1].Home.Any(c => c.Id == id && c.RealOwnerId == 0), "the view agrees: the other player's home, owned by her recruiter");
        // "You may": declining leaves her with her recruiter.
        var (d, kept) = Buy(Def(ability));
        Settle(d);
        Eq(d.State.Cards[kept].OwnerId, 0, "declined: she stays");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void HanteiSearch()
    {
        const string ability = "w734-hantei-search";
        var alliance = "oracle-l5r-10965"; // a non-Unique Political Strategy
        Check(CardCatalog.Get(alliance) is { Type: CardType.Strategy } a && !(a.Keywords ?? []).Contains("Unique") && (a.Keywords ?? []).Contains("Political"), "the fixture is a non-Unique Political Strategy");
        var (e, id) = Buy(Def(ability), s => { Add(s, alliance, 0, Zone.FateDiscard); });
        var political = e.State.Cards.Values.Single(c => c.DefinitionId == alliance && c.Zone == Zone.FateDiscard);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, command);
        Check(e.State.PendingSearch is not null, "the search opens");
        for (var guard = 0; guard < 4 && e.State.PendingSearch is not null; guard++)
        {
            var pick = e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Command is { Type: "chooseSearchDeck", Choice: "FateDiscard" })
                ?? e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Command is { Type: "selectSearchCard" });
            Check(pick is not null, "a search choice is offered");
            Accept(e, pick!.Command!.Type == "selectSearchCard" ? pick.Command with { Choice = alliance } : pick.Command);
        }
        Eq(e.State.Cards[political.Id].Zone, Zone.Hand, "the Political Strategy went from the discard pile to the hand");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "exactly one card was taken");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void JadePearlInnSearch()
    {
        const string ability = "w739-inn-search";
        const string farmDef = "sr6-cost1-producer";
        var (e, id) = Buy(Def(ability), s =>
        {
            // (The top card refills the Province first.) Three Holdings behind it in the Dynasty deck: a printed-cost-1 Gold producer (the only legal find), a cost-2 one and a Unique cost-1 one.
            var legal = s.Cards[s.Players[0].DynastyDeck[1]]; legal.DefinitionId = farmDef; legal.Printed = CardCatalog.Get("training-marketplace") with { Id = farmDef, GoldCost = 1, GoldProduction = 2 };
            var dear = s.Cards[s.Players[0].DynastyDeck[2]]; dear.DefinitionId = "sr6-cost2-producer"; dear.Printed = CardCatalog.Get("training-marketplace") with { Id = "sr6-cost2-producer", GoldCost = 2, GoldProduction = 2 };
            var unique = s.Cards[s.Players[0].DynastyDeck[3]]; unique.DefinitionId = "sr6-unique-producer"; unique.Printed = CardCatalog.Get("training-marketplace") with { Id = "sr6-unique-producer", GoldCost = 1, GoldProduction = 2, Keywords = [.. CardCatalog.Get("training-marketplace").Keywords ?? [], "Unique"] };
        });
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Check(e.State.PendingSearch is not null, "the Dynasty deck search opens");
        Eq(e.State.PendingSearch!.Deck, SearchDeck.Dynasty);
        var entry = e.GetView(0).PendingSearch!.Entries!.First(c => c.DefinitionId == farmDef);
        Check(entry.Eligible, "a non-Unique Gold-producing Holding of printed cost 1 qualifies");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(!entries.Single(c => c.DefinitionId == "sr6-cost2-producer").Eligible && !entries.Single(c => c.DefinitionId == "sr6-unique-producer").Eligible, "a printed-cost-2 one and a Unique one do not");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: farmDef));
        var refill = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(refill.DefinitionId == farmDef && refill.FaceUp, "the found Holding refills the Province face up");
        Eq(e.State.Cards[id].Zone, Zone.Home, "the Inn is in play");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void KitsuTamaoCreate()
    {
        const string ability = "wlpa-tamao-create";
        var (e, id) = Buy(Def(ability));
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var d = CardCatalog.Get(made.DefinitionId);
        Check(d is { Force: 2, Chi: 2, PersonalHonor: 3 } && new[] { "Lion Clan", "Ancestor", "Samurai", "Spirit" }.All(k => (d.Keywords ?? []).Contains(k)), "a 2F/2C/3PH Lion Clan Ancestor Samurai Spirit");
        Check(made.OwnerId == 0 && made.Zone == Zone.Home, "created at home for Tamao's controller");
        Check(!CanUse(e, 0, ability, id), "once");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void KitsuneHisanoCreate()
    {
        const string ability = "hisano-create-guide";
        var (e, id) = Buy(Def(ability));
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var d = CardCatalog.Get(made.DefinitionId);
        Check(d is { Type: CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3 } && new[] { "Mantis Clan", "Nonhuman", "Fox", "Spirit", "Unique" }.All(k => (d.Keywords ?? []).Contains(k)), "a 2F/2C/3PH Mantis Clan Nonhuman Fox Spirit Unique Personality");
        Check(made.OwnerId == 0 && made.Zone == Zone.Home, "created at home for Hisano's controller");
        Check(d.Text.Contains("Move him home", StringComparison.Ordinal), "it carries the printed ability");
        Check(!CanUse(e, 0, ability, id), "once per game");
        // The created Personality's own printed ability: Battle, target an enemy attacking Personality: move him home.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var guide = Add(s, made.DefinitionId, 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        var enemyOnly = Options(b, 0, "hisano-guide-move", guide.Id).First(o => o.Enabled);
        Check(enemyOnly.TargetIds!.Contains(attacker.Id) && !enemyOnly.TargetIds.Contains(defender.Id), "only the enemy attacking Personality is a target");
        Accept(b, Offer(b, 0, "hisano-guide-move", guide.Id, attacker.Id));
        Eq(b.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker was moved home");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void YuribaraCreate()
    {
        const string ability = "w741-yuribara-create";
        var (e, id) = Buy(Def(ability));
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Settle(e);
        var made = e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();
        Eq(made.Length, 1, "exactly one card is created");
        var d = CardCatalog.Get(made[0].DefinitionId);
        Check(d is { Type: CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 3 } && new[] { "Mantis Clan", "Nonhuman", "Butterfly", "Cavalry", "Spirit" }.All(k => (d.Keywords ?? []).Contains(k)), "a 0F/2C/3PH Mantis Clan Nonhuman Butterfly Cavalry Spirit Personality");
        Check(made[0].OwnerId == 0 && made[0].Zone == Zone.Home, "created at home for Yuribara's controller");
        Check(!CanUse(e, 0, ability, id), "once");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void SmallTimeBullyDishonor()
    {
        const string ability = "small-time-bully-dishonor";
        MatchEngine Setup(string keyword, out string bullyId, out string victimId, out string ownId)
        {
            string v = "", o = "";
            var (e, id) = Buy(Def(ability), s => { v = Add(s, "training-lion-samurai", 1, Zone.Home).Id; var mine = Add(s, "training-crane-samurai", 0, Zone.Home); o = mine.Id; if (keyword != "") Kw(mine, keyword); }, 2);
            bullyId = id; victimId = v; ownId = o;
            return e;
        }
        var e = Setup("Courtier", out var bully, out var victim, out var own);
        Check(e.State.Cards[bully].Bowed, "the Holding entered play bowed");
        Check(e.State.PendingChoice?.Type == "entryReaction", "its entry window is open");
        var command = InWindow(e, "entryReaction", 0, ability, bully, null, victim);
        var option = Options(e, 0, ability, bully).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(victim) && option.TargetIds.Contains(own), "any Personality in play may be dishonored");
        Accept(e, command);
        Check(e.State.Cards[victim].Dishonorable && !e.State.Cards[own].Dishonorable, "only the chosen Personality was dishonored");
        // A Magistrate counts too; with neither Courtier nor Magistrate it is not offered.
        var m = Setup("Magistrate", out var bully2, out _, out _);
        Check(CanUse(m, 0, ability, bully2), "a Magistrate qualifies");
        var n = Setup("", out var bully3, out _, out _);
        Check(!CanUse(n, 0, ability, bully3), "no Courtier or Magistrate: not offered");
        var h = AtHome(Def(ability), out var idle, (s, c) => { Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Courtier"); Add(s, "training-lion-samurai", 1, Zone.Home); });
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void TrainingInvest()
    {
        const string ability = "training-invest";
        var (e, command) = GoldParityTests.TrainingInvest();
        var follower = e.State.Cards.Values.Single(c => c.DefinitionId == "training-invest-follower");
        var force = e.CardForceForTest(follower.Id);
        var pay = e.PaymentOptions(0, 3).First();
        Check(CanUse(e, 0, ability, follower.Id) && !CanUse(e, 1, ability, follower.Id), "offered to its controller only");
        Reject(e, command(pay.SourceIds) with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, follower.Id), "gone with the window");
        Reject(closed, command(pay.SourceIds) with { ExpectedVersion = closed.Version });
        Accept(e, command(pay.SourceIds));
        Eq(e.CardForceForTest(follower.Id), force + 1, "one +1F token");
        Eq(e.State.Cards[follower.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 1);
        Check(!CanUse(e, 0, ability, follower.Id), "once");
        var h = AtHome("training-invest-follower", out var idle);
        NeverOpened(h, ability, idle.Id, command(pay.SourceIds) with { CardId = idle.Id });
    }

    private static void TrainingPersonalityEntryWatch()
    {
        const string ability = "training-personality-entry-watch";
        var (e, command) = GoldParityTests.EntryWatcher();
        var watcher = e.State.Cards.Values.Single(c => c.DefinitionId == "training-personality-entry-watcher");
        var entered = e.State.Cards.Values.Single(c => c.DefinitionId == "training-crane-clan-samurai" && c.Zone == Zone.Home);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", "a Personality entered play");
        Check(e.State.Cards[watcher.Id].Zone == Zone.Hand, "the watcher waits in hand");
        var honor = Honor(e, 0);
        var open = Options(e, 0, ability, watcher.Id);
        Check(open.Any(o => o.Enabled), "offered to the entering Personality's controller");
        Check(!CanUse(e, 1, ability, watcher.Id), "not offered to the other player");
        var pay = e.PaymentOptions(0, 2).First().SourceIds; // the Follower's printed Gold Cost, paid like an ordinary Equip
        var cmd = command(pay);
        Reject(e, cmd with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, watcher.Id), "gone with the window");
        Reject(closed, cmd with { ExpectedVersion = closed.Version });
        Accept(e, cmd);
        Eq(e.State.Cards[watcher.Id].Zone, Zone.Attached, "attached from hand");
        Eq(e.State.Cards[watcher.Id].AttachedTo, entered.Id, "to the Personality as it entered");
        Eq(Honor(e, 0), honor + 1, "the attach gave its entry Honor");
        Check(pay.All(id => e.State.Cards[id].Bowed), "the Gold Cost was paid from the offered sources");
    }

    private static void KiChangCreate()
    {
        const string ability = "kichang-exp-create";
        string kichang = "";
        var (e, entering) = Buy("training-crane-samurai", s => kichang = Add(s, Def(ability), 0, Zone.Home).Id);
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, kichang);
        Accept(e, command);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var d = CardCatalog.Get(made.DefinitionId);
        Check(d is { Type: CardType.Follower, Force: 1 } && (d.Keywords ?? []).Contains("Cavalry"), "a 1F Cavalry Follower");
        Check(made.Zone == Zone.Attached && made.AttachedTo == entering && made.OwnerId == 0, "attached to the entering Personality, not to Ki-Chang");
        Eq(e.CardForceForTest(made.Id), 1);
        // Another player's Personality entering offers nothing to this Ki-Chang.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var idle = Add(s2, Def(ability), 0, Zone.Home);
        NeverOpened(MatchEngine.FromTestState(s2), ability, idle.Id, command with { CardId = idle.Id });
    }

    // ---- the Engage window ----------------------------------------------------------------------------------------------------------------------------------------------------
    // Player 0 attacks province 0 (player 1's) and selects the battle; `arrange` lays out the table first. With select false the battle is never selected, so no Engage window opens.
    private static MatchEngine Engaging(Action<MatchState> arrange, bool select = true, bool expectWindow = true)
    {
        var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        if (select) { Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0)); if (expectWindow) Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens"); }
        return e;
    }

    // Declines windows until the battle's priority is reached with nothing pending.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.State.PriorityPlayerId }.SelectMany(p => e.QueryActions(p)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the position was not reached");
    }

    // A recon Strategy in `owner`'s hand with a performing Scout of his at province 0, opposed by a unit of the other player. Returns the engine in the Engage window.
    private static MatchEngine ReconTable(int owner, string def, out CardInstance card, out CardInstance scout, out CardInstance plain, bool select = true, bool bowedScout = false, Action<MatchState>? more = null)
    {
        CardInstance c = null!, sc = null!, pl = null!;
        var e = Engaging(s =>
        {
            sc = Add(s, "training-scout-samurai", owner, Zone.Battlefield, bowedScout, 0);
            pl = Add(s, "training-crane-veteran", owner, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
            c = Add(s, def, owner, Zone.Hand);
            more?.Invoke(s);
        }, select, expectWindow: !bowedScout);
        (card, scout, plain) = (c, sc, pl);
        return e;
    }

    // The shared proof for a Reaction taken in the Engage window by either battle role: offered to its player (with the Scout as performer), not to the other player, not for a non-Scout,
    // gone once the window is declined, and neither offered nor accepted before the battle is selected.
    private static void EngageBothRoles(string ability, string def, Action<MatchEngine, int, CardInstance, CardInstance, GameCommand> effect, bool scoutMustBeUnbowed = true)
    {
        foreach (var owner in new[] { 0, 1 })
        {
            var e = ReconTable(owner, def, out var card, out var scout, out var plain);
            Check(!CanUse(e, owner, ability, card.Id, plain.Id), $"player {owner}: a Personality who is not a Scout may not perform");
            var command = InWindow(e, "engageReaction", owner, ability, card.Id, scout.Id);
            effect(e, owner, card, scout, command);
            var idle = ReconTable(owner, def, out var card2, out _, out _, select: false);
            NeverOpened(idle, ability, card2.Id, command with { CardId = card2.Id });
            if (scoutMustBeUnbowed)
            {
                var bowed = ReconTable(owner, def, out var card3, out var scout3, out _, bowedScout: true);
                Check(!CanUse(bowed, owner, ability, card3.Id, scout3.Id), $"player {owner}: a bowed Scout may not perform (unbowed is printed); the window is {bowed.State.PendingChoice?.Type}");
            }
        }
    }

    private static void BurnTheTowersRecon()
    {
        const string ability = "burn-towers-recon";
        EngageBothRoles(ability, Def(ability), (e, owner, card, scout, command) =>
        {
            var strength = e.ProvinceStrengthForTest(1, 0); var other = e.ProvinceStrengthForTest(1, 1);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            Check(!e.State.Players[1].Provinces[0].Reconnaissance[1 - owner], "the other player does not");
            Eq(e.ProvinceStrengthForTest(1, 0), strength - 3, "the battlefield's province has -3 strength");
            Eq(Batch336Support.StrengthNextTurn(e, 1, 0), strength, "until end of turn");
            Eq(e.ProvinceStrengthForTest(1, 1), other, "another province is untouched");
            Check(!CanUse(e, owner, ability, card.Id, scout.Id), "the Strategy left the hand");
        });
    }

    private static void SurroundedRecon()
    {
        const string ability = "p2-surrounded-recon";
        EngageBothRoles(ability, Def(ability), (e, owner, card, scout, command) =>
        {
            Check(e.State.Players[owner].Hand.Contains(card.Id), "in hand before");
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            Eq(e.RangedStrengthForTest("shika-ranged", owner, null), 6, "+2 to his Ranged Attacks from card effects this battle");
            Eq(e.RangedStrengthForTest("shika-ranged", 1 - owner, null), 4, "not the other player's");
            Eq(e.RangedStrengthForTest("kazuko-fear-melee", owner, null), 3, "not a Melee Attack");
            Eq(Batch336Support.Edit(e, st => st.BattleNumber++).RangedStrengthForTest("shika-ranged", owner, null), 4, "another battle: no bonus");
        });
    }

    private static void SeekingTheTruthRecon()
    {
        const string ability = "seeking-truth-recon";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance battleStrategy = null!, openStrategy = null!;
            var e = ReconTable(owner, def, out var card, out var scout, out var plain, more: s =>
            {
                battleStrategy = Add(s, "oracle-l5r-6047", owner, Zone.FateDiscard); // Precision: a non-Unique Strategy with a Battle action
                openStrategy = Add(s, "oracle-l5r-99", owner, Zone.FateDiscard); // A Weak Poison: Open only
            });
            Check(!CanUse(e, owner, ability, card.Id, plain.Id), $"player {owner}: a non-Scout may not perform");
            if (e.State.PriorityPlayerId != owner) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Check(!CanUse(e, 1 - owner, ability, card.Id), "not offered to the other player");
            var open = Options(e, owner, ability, card.Id, scout.Id).First(o => o.Enabled);
            var slot = open.Slots!.Single(x => x.Slot == "strategy");
            Check(slot.CardIds.Contains(battleStrategy.Id) && !slot.CardIds.Contains(openStrategy.Id), "only the non-Unique BATTLE Strategy in the discard pile is offered");
            var command = open.Command! with { Selections = [Pick("strategy", battleStrategy.Id)], TargetId = null };
            Reject(e, command with { PlayerId = 1 - owner });
            var closed = e.Clone(); Settle(closed);
            Check(!CanUse(closed, owner, ability, card.Id, scout.Id), "gone once the window is declined");
            Reject(closed, command with { ExpectedVersion = closed.Version });
            Accept(e, command);
            Check(e.State.Players[owner].Hand.Contains(battleStrategy.Id), "the Battle Strategy returned to the hand");
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], "Reconnaissance at the battlefield");
            var idle = ReconTable(owner, def, out var card2, out _, out _, select: false);
            NeverOpened(idle, ability, card2.Id, command with { CardId = card2.Id });
        }
        // The consequence, for the Attacker: "After this battle ends, if you did not destroy the battlefield's province or an army, discard a card at random."
        foreach (var scoutWins in new[] { false, true })
        {
            var e = ReconTable(0, def, out var card, out var scout, out _, more: s =>
            {
                Add(s, "oracle-l5r-6047", 0, Zone.FateDiscard); Add(s, "training-lion-samurai", 0, Zone.Hand); Add(s, "training-crane-samurai", 0, Zone.Hand);
                foreach (var c in s.Cards.Values.Where(c => c.Zone == Zone.Battlefield && (c.OwnerId == 0) == scoutWins)) c.Modifiers.Add(new("test-source", "test", "Force", 30));
            });
            var pick = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-6047");
            Accept(e, Options(e, 0, ability, card.Id, scout.Id).First(o => o.Enabled).Command! with { Selections = [Pick("strategy", pick.Id)], TargetId = null });
            var hand = e.State.Players[0].Hand.Count;
            PassUntil(e, () => e.State.CurrentBattleProvince is null);
            if (scoutWins) { Eq(e.State.Players[0].Hand.Count, hand, "the enemy army was destroyed: no discard"); Check(!e.GetView(0).Events.Any(ev => ev.Type == "randomDiscard"), "none is announced"); }
            else { Eq(e.State.Players[0].Hand.Count, hand - 1, "no province or army destroyed: a card was discarded at random"); Check(e.GetView(0).Events.Any(ev => ev.Type == "randomDiscard"), "announced"); }
        }
    }

    private static void TorchsFlameFlickersRecon()
    {
        const string ability = "torch-flame-recon";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            CardInstance bowerA = null!, bowerB = null!;
            var e = ReconTable(owner, def, out var card, out var scout, out var plain, more: s => { bowerA = Add(s, "training-battle-bower", foe, Zone.Battlefield, province: 0); bowerB = Add(s, "training-battle-bower", foe, Zone.Battlefield, province: 0); });
            Check(!CanUse(e, owner, ability, card.Id, plain.Id), $"player {owner}: only a Scout may perform");
            var command = InWindow(e, "engageReaction", owner, ability, card.Id, scout.Id);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            PassUntil(e, () => e.State.PendingChoice is null);
            if (e.State.PriorityPlayerId != foe) Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type == "pass").Command!);
            // The first effect that bows one of his Personalities there is negated; the next is not.
            Accept(e, Offer(e, foe, "training-bow-at-battle", bowerA.Id, plain.Id));
            Settle(e);
            Check(!e.State.Cards[plain.Id].Bowed, "the next card effect that bows his Personality was negated");
            Check(e.State.TorchWatches.Single().Fired, "and the watch is spent");
            var again = Batch336Support.Edit(e, st => st.PriorityPlayerId = foe);
            Accept(again, Offer(again, foe, "training-bow-at-battle", bowerB.Id, plain.Id));
            Check(again.State.Cards[plain.Id].Bowed, "a second effect bows him: only the next one was negated");
            // A bowed Scout may perform (no unbowed is printed): the window opens and he is offered to his player.
            var b = ReconTable(owner, def, out var card2, out var scout2, out _, bowedScout: true);
            Eq(b.State.PendingChoice?.Type, "engageReaction", "a bowed Scout still opens the window");
            if (b.State.PriorityPlayerId != owner) Accept(b, new GameCommand("declineReaction", b.State.PriorityPlayerId, b.Version));
            Check(CanUse(b, owner, ability, card2.Id, scout2.Id), "a bowed Scout is a legal performer");
            var idle = ReconTable(owner, def, out var card3, out _, out _, select: false);
            NeverOpened(idle, ability, card3.Id, command with { CardId = card3.Id });
        }
    }

    private static void GoldenPlainsRecon()
    {
        const string ability = "golden-plains-recon";
        var plainOfGold = CreatedCards.Resolve(AbilityCatalog.All[ability].Creates!).Id;
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance plains = null!;
            MatchEngine Build(bool terrain, bool select = true)
            {
                return Engaging(s =>
                {
                    plains = s.Cards[s.Players[owner].StrongholdId]; plains.DefinitionId = Def(ability);
                    Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                    Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                    if (terrain) Add(s, "training-terrain", 1 - owner, Zone.Battlefield, province: 0);
                }, select);
            }
            var e = Build(false);
            var command = InWindow(e, "engageReaction", owner, ability, plains.Id);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            var created = e.State.Cards.Values.Single(c => c.DefinitionId == plainOfGold);
            Check(created.Zone == Zone.Battlefield && created.ProvinceIndex == 0 && created.OwnerId == owner, "the Plain of Gold is in play at the battlefield, its creator's");
            Check(!CanUse(e, owner, ability, plains.Id), "once");
            // A battlefield that already has a Terrain: Reconnaissance still, nothing created.
            var t = Build(true);
            Accept(t, InWindow(t, "engageReaction", owner, ability, plains.Id));
            Check(t.State.Players[1].Provinces[0].Reconnaissance[owner], "Reconnaissance is still gained where a Terrain stands");
            Check(t.State.Cards.Values.All(c => c.DefinitionId != plainOfGold), "and no Plain of Gold is created there");
            // A bowed Stronghold does not react; before the battle is selected nothing is offered.
            var bowed = Build(false);
            var b = Batch336Support.Edit(bowed, st => st.Cards[plains.Id].Bowed = true);
            Check(!CanUse(b, owner, ability, plains.Id), "a bowed Stronghold cannot react");
            var idle = Build(false, select: false);
            NeverOpened(idle, ability, plains.Id, command);
        }
    }

    private static void TenbatsuEngageLook()
    {
        const string ability = "tenbatsu-engage-look";
        const string darkVirtue = "oracle-l5r-639"; // At Any Cost
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance tenbatsu = null!, virtue = null!; CardInstance[] top = null!;
            MatchEngine Build(bool home, bool select = true) => Engaging(s =>
            {
                tenbatsu = Add(s, Def(ability), owner, home ? Zone.Home : Zone.Battlefield, province: home ? null : 0);
                Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                virtue = Add(s, darkVirtue, owner, Zone.Hand);
                var made = new CardInstance[3];
                for (var i = 0; i < 3; i++) { made[i] = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, made[i].Id); }
                top = [.. made.Reverse()]; // top[0] is the top card
            }, select, expectWindow: !home);
            var e = Build(false);
            var command = InWindow(e, "engageReaction", owner, ability, tenbatsu.Id);
            Accept(e, command);
            Eq(e.State.PendingChoice?.Type, "lookArrange", "the look at the top three is open");
            Accept(e, new GameCommand("arrangeTopCards", owner, e.Version, CardIds: ["2:top", "1:top", "0:top"]));
            Eq(string.Join(",", e.State.Players[owner].FateDeck.Take(3)), string.Join(",", top[2].Id, top[1].Id, top[0].Id), "the three cards were put back in the chosen order");
            Eq(e.State.PendingChoice?.Type, "p3DarkVirtueDiscard", "then the Dark Virtue decision");
            var hand = e.State.Players[owner].Hand.Count;
            var choices = e.QueryActions(owner).Where(a => a.Type == "p3DiscardDarkVirtue").ToArray();
            Check(choices.Any(o => o.Command!.TargetId == virtue.Id) && choices.Any(o => o.Command!.TargetId is null), "discard the Dark Virtue, or decline");
            Accept(e, choices.First(o => o.Command!.TargetId == virtue.Id).Command!);
            Eq(e.State.Cards[virtue.Id].Zone, Zone.FateDiscard, "the Dark Virtue was discarded");
            Eq(e.State.Players[owner].Hand.Count, hand, "and a card drawn in its place");
            Check(e.State.Players[owner].Hand.Contains(top[2].Id), "the drawn card is the re-ordered top card");
            Check(!CanUse(e, owner, ability, tenbatsu.Id), "once");
            // Tenbatsu at home is not at the engaged battlefield; before the battle is selected there is nothing to react to.
            var h = Build(true);
            Check(h.State.PendingChoice?.Type != "engageReaction" || !CanUse(h, h.State.PriorityPlayerId, ability, tenbatsu.Id), "Tenbatsu at home has no Reaction to the engagement");
            var idle = Build(false, select: false);
            NeverOpened(idle, ability, tenbatsu.Id, command);
        }
    }

    private static void TowerOfVigilanceEngage()
    {
        const string ability = "w741-tower-engage";
        // The Fortification guards player 1's province 0, which player 0 attacks; its controller is the Defender and reacts once the Attacker has passed.
        CardInstance tower = null!;
        MatchEngine Build(bool units, bool select = true, int guarded = 0) => Engaging(s =>
        {
            tower = Add(s, Def(ability), 1, Zone.Home); tower.EnteredPlayTurn = 1;
            s.Fortifications = new(StringComparer.Ordinal) { [tower.Id] = guarded };
            if (units) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        }, select, expectWindow: guarded == 0 && units);
        var e = Build(true);
        var command = InWindow(e, "engageReaction", 1, ability, tower.Id);
        Accept(e, command);
        var guards = e.State.Cards.Values.Where(c => c.DefinitionId == Wave741Guard && c.Zone == Zone.Battlefield).ToArray();
        Eq(guards.Length, 1, "one Guard is created");
        Check(guards[0].ProvinceIndex == 0 && guards[0].OwnerId == 1, "at the battlefield, for the Tower's controller");
        var guardDef = CardCatalog.Get(Wave741Guard);
        Check(guardDef is { Force: 0, Chi: 2, PersonalHonor: 2 } && (guardDef.Keywords ?? []).Contains("Guard") && (guardDef.Keywords ?? []).Contains("Samurai"), "a 0F/2C/2PH Samurai Guard");
        Check(e.State.Cards[tower.Id].Bowed, "the Tower bowed as its cost");
        Check(!CanUse(e, 1, ability, tower.Id), "a bowed Tower cannot be used again");
        PassUntil(e, () => e.State.CurrentBattleProvince is null);
        Check(e.State.Cards[guards[0].Id].Zone == Zone.RemovedFromGame, "removed from the game after the battle ended");
        // No units of his at the battlefield: nothing is created. A Tower guarding another Province, or no battle at all: not offered.
        var empty = Build(false);
        Check(empty.State.PendingChoice?.Type != "engageReaction" && !CanUse(empty, 1, ability, tower.Id), "no units at the battlefield: the Tower is not offered (presence rule)");
        Check(empty.State.Cards.Values.All(c => c.DefinitionId != Wave741Guard), "no units, no Guard");
        var other = Build(true, guarded: 1);
        Check(other.State.PendingChoice?.Type != "engageReaction" || !CanUse(other, 1, ability, tower.Id), "a Tower guarding another Province is not offered");
        var idle = Build(true, select: false);
        NeverOpened(idle, ability, tower.Id, command);
    }
    private const string Wave741Guard = Wave741Abilities.TowerGuard;

    private static void RecoveringWhatWasLost()
    {
        const string ability = "wl-sg-recovering-lost";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance card = null!, home = null!;
            MatchEngine Build(bool tactician, bool opposed = true, bool select = true) => Engaging(s =>
            {
                card = Add(s, Def(ability), owner, Zone.Hand);
                home = Add(s, "training-crane-veteran", owner, Zone.Home);
                if (tactician) Kw(home, "Tactician");
                if (owner == 0) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // the Attacker must assign; the Defender's reaction is Absent (no presence)
                if (opposed) Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                else if (owner == 1) Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            }, select, expectWindow: opposed);
            var e = Build(true);
            var force = e.CardForceForTest(home.Id);
            var command = InWindow(e, "engageReaction", owner, ability, card.Id, null, home.Id);
            Accept(e, command);
            Eq(e.State.Cards[home.Id].Zone, Zone.Battlefield, "he moved to the battlefield"); Eq(e.State.Cards[home.Id].ProvinceIndex, 0, "the current one");
            Eq(e.CardForceForTest(home.Id), force + 2, "+2F as a Tactician");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
            // Not a Tactician: moved, no bonus.
            var plain = Build(false);
            var plainForce = plain.CardForceForTest(home.Id);
            Accept(plain, InWindow(plain, "engageReaction", owner, ability, card.Id, null, home.Id));
            Eq(plain.State.Cards[home.Id].Zone, Zone.Battlefield, "moved");
            Eq(plain.CardForceForTest(home.Id), plainForce, "no bonus without Tactician");
            // Not opposed: not offered.
            if (owner == 0)
            {
                var alone = Build(true, opposed: false);
                Check(!CanUse(alone, 0, ability, card.Id), "not offered when he would not be opposed");
                Reject(alone, Use(alone, 0, ability, card.Id, home.Id));
            }
            var idle = Build(true, select: false);
            NeverOpened(idle, ability, card.Id, command);
        }
    }

    // ---- the after-action-resolved window -------------------------------------------------------------------------------------------------------------------------------------
    private const string AfterAction = "afterActionResolvedReaction";

    // Player `foe` acts in a battle (Table's priority is his) with a destroyer that kills `victim`; `arrange` adds the reacting side. Returns the engine in the window the action opened.
    private static MatchEngine Destroyed(int foe, Func<MatchState, CardInstance> victim, Action<MatchState>? arrange = null, bool act = true, bool expectWindow = true)
    {
        CardInstance destroyer = null!, target = null!;
        var s = Table(Phase.Battle, foe); s.PriorityPlayerId = foe;
        destroyer = Add(s, "training-no-attachments-destroyer", foe, Zone.Battlefield, province: 0);
        target = victim(s);
        arrange?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        if (act) { Accept(e, Offer(e, foe, "training-destroy-no-attachments", destroyer.Id, target.Id)); if (expectWindow) Eq(e.State.PendingChoice?.Type, AfterAction, "the action's resolution opens the window"); }
        return e;
    }

    private static void AkodoKobiRecruit()
    {
        const string ability = "kobi-recruit";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance kobi = null!, victim = null!;
            MatchEngine Build(bool lion = true, bool act = true) => Destroyed(foe, s => victim = Kw(Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0), lion ? new[] { "Lion Clan" } : Array.Empty<string>()), s => kobi = Add(s, Def(ability), owner, Zone.Battlefield, province: 0), act, lion);
            var e = Build();
            var command = InWindow(e, AfterAction, owner, ability, kobi.Id);
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the Lion Clan Personality is dead");
            Accept(e, command);
            Eq(e.State.PendingEndPhaseRecruits.Count, 1, "the recruit is queued, not applied at once");
            Check(e.State.PendingEndPhaseRecruits[0].PlayerId == owner && e.State.PendingEndPhaseRecruits[0].CardId == victim.Id, "for the dead Personality's controller");
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "still dead until the turn ends");
            // When the turn ends he is recruited without paying his Gold Cost or meeting his Honor Requirement.
            var turnEnd = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.PendingChoice = null; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; st.ActivePlayerId = foe; st.PriorityPlayerId = foe; st.Players[owner].Honor = 0; });
            Accept(turnEnd, new GameCommand("endDynasty", foe, turnEnd.Version));
            Check(turnEnd.State.Cards[victim.Id].Zone == Zone.Home && turnEnd.State.Cards[victim.Id].OwnerId == owner, "recruited at home when the turn ended");
            Check(turnEnd.State.PendingEndPhaseRecruits.Count == 0, "the queue is drained");
            // A destroyed Personality who is not Lion Clan opens nothing for Kobi; before any action nothing is offered.
            var plain = Build(lion: false);
            Check(plain.State.PendingChoice?.Type != AfterAction || !CanUse(plain, owner, ability, kobi.Id), "not offered: the destroyed Personality was not Lion Clan");
            NeverOpened(Build(act: false), ability, kobi.Id, command);
        }
    }

    private static void EternalVictoryDojoDraw()
    {
        const string ability = "eternal-victory-draw";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance dojo = null!, strategy = null!, performer = null!;
            MatchEngine Build(int actor, bool tactical = true)
            {
                var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor;
                dojo = s.Cards[s.Players[owner].StrongholdId]; dojo.DefinitionId = Def(ability);
                strategy = Add(s, "training-performer-strategy", actor, Zone.Hand);
                if (tactical) Kw(strategy, "Tactical");
                performer = Add(s, "training-crane-samurai", actor, Zone.Battlefield, province: 0);
                Add(s, "training-lion-samurai", 1 - actor, Zone.Battlefield, province: 0);
                return MatchEngine.FromTestState(s);
            }
            var e = Build(owner);
            Accept(e, Enabled(e, owner, "training-performer-boost", strategy.Id, performer.Id).Command!);
            var command = InWindow(e, AfterAction, owner, ability, dojo.Id);
            var hand = e.State.Players[owner].Hand.Count;
            Accept(e, command);
            Eq(string.Join(",", e.State.PendingEndPhaseDraws), owner.ToString(), "a draw is owed to the Dojo's controller");
            Eq(e.State.Players[owner].Hand.Count, hand, "nothing is drawn yet");
            // The card comes as the next End Phase begins (here the controller's own turn: plus his ordinary draw).
            var end = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.PendingChoice = null; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; st.ActivePlayerId = owner; st.PriorityPlayerId = owner; });
            var before = end.State.Players[owner].Hand.Count;
            Accept(end, new GameCommand("pass", owner, end.Version));
            Eq(end.State.Players[owner].Hand.Count, before + 2, "the owed card and the ordinary End Phase draw");
            Check(end.State.PendingEndPhaseDraws.Count == 0, "the owed draw is spent");
            // Not for a non-Tactical action of his own, nor for the other player's Tactical action; nothing before an action.
            var plain = Build(owner, tactical: false);
            Accept(plain, Enabled(plain, owner, "training-performer-boost", strategy.Id, performer.Id).Command!);
            Check(plain.State.PendingChoice?.Type != AfterAction || !CanUse(plain, owner, ability, dojo.Id), "a non-Tactical action does not trigger the Dojo");
            var theirs = Build(1 - owner);
            Accept(theirs, Enabled(theirs, 1 - owner, "training-performer-boost", strategy.Id, performer.Id).Command!);
            Check(theirs.State.PendingChoice?.Type != AfterAction || !CanUse(theirs, owner, ability, dojo.Id), "another player's Tactical action does not trigger the Dojo");
            NeverOpened(Build(owner), ability, dojo.Id, command);
        }
    }

    private static void MeasureOfDevotionCreate()
    {
        const string ability = "measure-of-devotion-create";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance card = null!, seeker = null!;
            MatchEngine Build(bool deathseeker = true, bool act = true) => Destroyed(foe, s => seeker = deathseeker ? Kw(Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0), "Deathseeker") : Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0), s => card = Add(s, Def(ability), owner, Zone.Hand), act, deathseeker);
            var e = Build();
            var command = InWindow(e, AfterAction, owner, ability, card.Id);
            var honor = Honor(e, owner);
            Accept(e, command);
            var made = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9710");
            var d = CardCatalog.Get(made.DefinitionId);
            Check(d is { Force: 2, Chi: 2, PersonalHonor: 0 } && new[] { "Lion Clan", "Samurai", "Deathseeker" }.All(k => (d.Keywords ?? []).Contains(k)), "a 2F/2C/0PH Lion Clan Samurai Deathseeker");
            Check(made.Zone == Zone.Battlefield && made.ProvinceIndex == 0 && made.OwnerId == owner, "created at the current battlefield for the Strategy's owner");
            Eq(Honor(e, owner), honor + 1, "gained 1 Honor");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            var plain = Build(deathseeker: false);
            Check(plain.State.PendingChoice?.Type != AfterAction || !CanUse(plain, owner, ability, card.Id), "not a Deathseeker: not offered");
            NeverOpened(Build(act: false), ability, card.Id, command);
        }
    }

    private static void ShieldmanHonor()
    {
        const string ability = "shieldman-honor";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance shieldman = null!, follower = null!, caravan = null!;
            // The other player's Elephant Caravan (a Fear action) destroys a Follower of a unit; Shieldman is attached to the Personality of that unit (or of another one).
            MatchEngine Build(bool inUnit = true, bool act = true)
            {
                var s = Table(Phase.Battle, foe); s.PriorityPlayerId = foe;
                var host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                var elsewhere = Add(s, "training-crane-veteran", owner, Zone.Battlefield, province: 0);
                shieldman = Add(s, Def(ability), owner, Zone.Attached, province: 0, parent: host.Id);
                follower = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: inUnit ? host.Id : elsewhere.Id);
                var enemyHost = Add(s, "training-lion-samurai", foe, Zone.Battlefield, province: 0);
                caravan = Add(s, Def("elephant-caravan-fear"), foe, Zone.Attached, province: 0, parent: enemyHost.Id);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Offer(e, foe, "elephant-caravan-fear", caravan.Id, follower.Id));
                return e;
            }
            var e = Build();
            Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "another card of the unit was destroyed");
            var command = InWindow(e, AfterAction, owner, ability, shieldman.Id);
            var honor = Honor(e, owner);
            Accept(e, command);
            Eq(Honor(e, owner), honor + 2, "gained 2 Honor");
            Check(!CanUse(e, owner, ability, shieldman.Id), "taken once for this action");
            var elsewhere2 = Build(inUnit: false);
            Check(elsewhere2.State.PendingChoice?.Type != AfterAction || !CanUse(elsewhere2, owner, ability, shieldman.Id), "a card destroyed in another unit does not count");
            NeverOpened(Build(act: false), ability, shieldman.Id, command);
        }
    }

    private static void IsawaKimiDiscard()
    {
        const string ability = "kimi-exp2-discard";
        foreach (var owner in new[] { 0, 1 })
        {
            var drawer = 1 - owner;
            CardInstance kimi = null!, market = null!, held = null!;
            MatchEngine Build(bool own = false, bool act = true)
            {
                var actor = own ? owner : drawer;
                var s = Table(Phase.Action, actor); s.PriorityPlayerId = actor;
                s.Players[actor].Clan = "Mantis";
                kimi = Add(s, Def(ability), owner, Zone.Home, bowed: true);
                market = Add(s, "oracle-l5r-10211", actor, Zone.Home);
                held = Add(s, "training-crane-samurai", actor, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Enabled(e, actor, "market-draw-mantis", market.Id).Command!);
                return e;
            }
            var e = Build();
            Eq(e.State.Players[drawer].Hand.Count, 2, "he drew a card");
            var command = InWindow(e, AfterAction, owner, ability, kimi.Id);
            Accept(e, command);
            Eq(e.State.PendingChoice?.Type, "deciderChoice", "the other player chooses");
            Eq(e.State.PendingChoice?.PlayerId, drawer, "and it is he who decides");
            var picks = e.QueryActions(drawer).Where(a => a.Type == "chooseDecider" && a.Enabled).ToArray();
            Eq(picks.Length, 2, "any card of his hand");
            Check(e.QueryActions(owner).All(a => a.Type != "chooseDecider" || !a.Enabled), "Kimi's controller does not choose");
            Accept(e, picks.Single(a => a.Command!.TargetId == held.Id).Command!);
            Eq(e.State.Players[drawer].Hand.Count, 1, "a card was discarded");
            Eq(e.State.Cards[held.Id].Zone, Zone.FateDiscard, "the card he chose");
            // Kimi's own controller's draw puts no card into another player's hand.
            var own = Build(own: true);
            Check(own.State.PendingChoice?.Type != AfterAction || !CanUse(own, owner, ability, kimi.Id), "no card went into another player's hand");
            NeverOpened(Build(act: false), ability, kimi.Id, command);
        }
    }

    private static void TrainingAfterActionWatcher()
    {
        const string ability = "training-after-action-resolved-gain";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance watcher = null!;
            MatchEngine Build(bool act = true, bool ownVictim = true) => Destroyed(foe, s => Add(s, "training-crane-samurai", ownVictim ? owner : foe, Zone.Battlefield, province: 0), s => watcher = Add(s, "training-after-action-watcher", owner, Zone.Hand), act, ownVictim);
            var e = Build();
            var command = InWindow(e, AfterAction, owner, ability, watcher.Id);
            var honor = Honor(e, owner); var foeHonor = Honor(e, foe);
            Accept(e, command);
            Eq(Honor(e, owner), honor + 3, "gained 3 Honor");
            Eq(Honor(e, foe), foeHonor, "the other player's Honor is unchanged");
            NeverOpened(Build(act: false), ability, watcher.Id, command);
        }
    }

    private static void WithoutMercyAction()
    {
        const string ability = "without-mercy-action";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance samurai = null!, mercy = null!, strategy = null!;
            MatchEngine Build(int actor, bool isSamurai = true, bool act = true)
            {
                var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor;
                samurai = Add(s, "training-crane-samurai", actor, Zone.Battlefield, province: 0);
                if (isSamurai) Kw(samurai, "Samurai");
                Add(s, "training-lion-samurai", 1 - actor, Zone.Battlefield, province: 0);
                strategy = Add(s, "training-performer-strategy", actor, Zone.Hand);
                mercy = Add(s, Def(ability), owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Enabled(e, actor, "training-performer-boost", strategy.Id, samurai.Id).Command!);
                return e;
            }
            var e = Build(owner);
            var command = InWindow(e, AfterAction, owner, ability, mercy.Id);
            Accept(e, command);
            Eq(e.State.Cards[mercy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == owner), 1, "an additional Battle action was granted to its owner");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 1 - owner), 0, "none to the other player");
            Settle(e);
            Eq(e.State.PriorityPlayerId, owner, "priority is back with its owner, who takes the additional action first");
            var plain = Build(owner, isSamurai: false);
            Check(plain.State.PendingChoice?.Type != AfterAction || !CanUse(plain, owner, ability, mercy.Id), "not a Samurai performer: not offered");
            var theirs = Build(1 - owner);
            Check(theirs.State.PendingChoice?.Type != AfterAction || !CanUse(theirs, owner, ability, mercy.Id), "another player's action: not offered");
            NeverOpened(Build(owner, act: false), ability, mercy.Id, command);
        }
        // An action that targeted an enemy card does not qualify.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var source = Kw(Add(s2, "training-penalty-source", 0, Zone.Battlefield, province: 0), "Samurai");
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mercy2 = Add(s2, Def(ability), 0, Zone.Hand);
        var t = MatchEngine.FromTestState(s2);
        Accept(t, Offer(t, 0, "training-penalty-force", source.Id, enemy.Id));
        Check(t.State.PendingChoice?.Type != AfterAction || !CanUse(t, 0, ability, mercy2.Id), "an enemy card was targeted: not offered");
    }

    // ---- before-action and announce windows -------------------------------------------------------------------------------------------------------------------------------------
    private static int ForceMods(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    private static void BadKharmaNegate()
    {
        const string ability = "bad-kharma-negate";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance forefather = null!, kharma = null!, ancestor = null!, target = null!;
            MatchEngine Limited(int honor = -1, bool act = true, bool own = false)
            {
                var actor = own ? owner : foe;
                var s = Table(Phase.Action, actor); s.PriorityPlayerId = actor; s.Players[actor].Honor = honor;
                forefather = Add(s, Def("forefather-honor"), actor, Zone.Hand);
                Kw(Add(s, "training-lion-samurai", actor, Zone.Home), "Ancestor"); Kw(Add(s, "training-lion-veteran", actor, Zone.Home), "Ancestor");
                kharma = Add(s, Def(ability), owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Enabled(e, actor, "forefather-honor", forefather.Id).Command!);
                return e;
            }
            // The Limited action of a Strategy by a player with Family Honor -1: the other player reacts from his hand and the action is negated (no Honor gained).
            var e = Limited();
            Eq(e.State.PendingChoice?.PlayerId, owner, "the window is for the other player");
            Check(e.QueryActions(owner).Where(a => a.Type == "useAbility" && a.Command is not null).All(a => AbilityCatalog.All[a.Command!.AbilityId!].Timing == "Reaction"), "only Reactions are on offer against a held action");
            var command = InWindow(e, "unimpeachableNameReaction", owner, ability, kharma.Id);
            Accept(e, command);
            Settle(e);
            Eq(Honor(e, foe), -1, "the negated action gained no Honor");
            Eq(e.State.Cards[forefather.Id].Zone, Zone.FateDiscard, "the action was taken: the Strategy left the hand");
            Eq(e.State.Cards[kharma.Id].Zone, Zone.FateDiscard, "and Bad Kharma was discarded");
            Check(e.State.PendingChoice is null, "the window closed");
            // Costs are ignored: the Battle action's cost (destroying an Ancestor) is not paid, and nothing happens.
            var b = Table(Phase.Battle, foe); b.PriorityPlayerId = foe; b.Players[foe].Honor = -3;
            var strategy = Add(b, Def("forefather-destroy"), foe, Zone.Hand);
            ancestor = Kw(Add(b, "training-lion-samurai", foe, Zone.Battlefield, province: 0), "Ancestor");
            target = Add(b, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
            var kharma2 = Add(b, Def(ability), owner, Zone.Hand);
            var e2 = MatchEngine.FromTestState(b);
            Accept(e2, Offer(e2, foe, "forefather-destroy", strategy.Id, target.Id, ancestor.Id));
            Accept(e2, InWindow(e2, "unimpeachableNameReaction", owner, ability, kharma2.Id));
            Settle(e2);
            Eq(e2.State.Cards[ancestor.Id].Zone, Zone.Battlefield, "he ignores the action's costs: the Ancestor was not destroyed");
            Eq(e2.State.Cards[target.Id].Zone, Zone.Battlefield, "and the effects were negated");
            Eq(e2.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was still taken");
            // Family Honor above -1: no window; the player's own action never opens it for himself; before any action nothing is offered.
            var zero = Limited(honor: 0);
            Check(zero.State.PendingChoice is null, "Family Honor of 0 is not -1 or lower: no window");
            Eq(Honor(zero, foe), 2, "the action resolved: 1 Honor for each of his two Ancestors");
            var mine = Limited(honor: -5, own: true);
            Check(mine.State.PendingChoice is null, "Bad Kharma reacts to another player's action only");
            NeverOpened(Limited(act: false), ability, kharma.Id, command);
        }
    }

    private static void HidaFujitaNegate()
    {
        const string ability = "fujita-negate-follower-destruction";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance fujita = null!, follower = null!, attack = null!, host = null!;
            MatchEngine Build(bool atBattle = true, bool act = true, bool aimAtHost = false)
            {
                var s = Table(Phase.Battle, foe); s.PriorityPlayerId = foe;
                fujita = atBattle ? Add(s, Def(ability), owner, Zone.Battlefield, province: 0) : Add(s, Def(ability), owner, Zone.Home);
                host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                follower = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: host.Id);
                Add(s, "training-lion-samurai", foe, Zone.Battlefield, province: 0);
                attack = Add(s, "training-honor-cost-strategy", foe, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Offer(e, foe, "training-honor-cost-attack", attack.Id, aimAtHost ? host.Id : follower.Id));
                return e;
            }
            var e = Build();
            var command = InWindow(e, "beforeActionReaction|attackReaction", owner, ability, fujita.Id);
            Accept(e, command);
            PassUntil(e, () => e.State.PendingChoice is null && e.State.PendingAttack is null);
            Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower's destruction by the Attack was negated");
            // Declined: the Follower is destroyed. Fujita at home is not offered.
            var d = Build();
            PassUntil(d, () => d.State.PendingChoice is null && d.State.PendingAttack is null);
            Eq(d.State.Cards[follower.Id].Zone, Zone.FateDiscard, "declined: the Follower was destroyed");
            var home = Build(atBattle: false);
            Check(!CanUse(home, owner, ability, fujita.Id) && !CanUse(home, home.State.PriorityPlayerId, ability, fujita.Id), "Fujita at home: not offered");
            PassUntil(home, () => home.State.PendingChoice is null && home.State.PendingAttack is null);
            Eq(home.State.Cards[follower.Id].Zone, Zone.FateDiscard, "and the Follower was destroyed");
            NeverOpened(Build(act: false), ability, fujita.Id, command);
        }
    }

    private static void ImproperPapersNegate()
    {
        const string ability = "wl-improper-papers";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance papers = null!, source = null!, target = null!, other = null!;
            MatchEngine Build(bool act = true)
            {
                var s = Table(Phase.Battle, foe); s.PriorityPlayerId = foe; s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 3];
                source = Add(s, "training-move-source", foe, Zone.Battlefield, province: 1);
                target = Add(s, "training-battle-bower", owner, Zone.Battlefield, province: 1);
                other = Add(s, "training-crane-samurai", owner, Zone.Home);
                papers = Add(s, Def(ability), owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Enabled(e, foe, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 });
                return e;
            }
            var e = Build();
            var offers = e.QueryActions(owner).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
            if (Holder(e) != owner) { Accept(e, new GameCommand("declineReaction", Holder(e), e.Version)); offers = e.QueryActions(owner).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray(); }
            Check(offers.Length >= 2 && offers.All(o => o.Enabled && o.Command!.CardId == papers.Id), "any Personality may be targeted, as printed: " + string.Join(",", offers.Select(o => o.Label)));
            var command = InWindow(Build(), "beforeActionReaction", owner, ability, papers.Id, null, target.Id);
            Accept(e, offers.Single(o => o.Command!.TargetId == target.Id).Command!);
            PassUntil(e, () => e.State.PendingChoice is null);
            Eq(e.State.Cards[target.Id].ProvinceIndex, 1, "his movement was negated: he stays at battlefield 1");
            Eq(e.State.Cards[papers.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            // Naming a Personality the action does not move changes nothing: the movement goes ahead.
            var wrong = Build();
            if (Holder(wrong) != owner) Accept(wrong, new GameCommand("declineReaction", Holder(wrong), wrong.Version));
            Accept(wrong, wrong.QueryActions(owner).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Command.TargetId == other.Id).Command!);
            PassUntil(wrong, () => wrong.State.PendingChoice is null);
            Eq(wrong.State.Cards[target.Id].ProvinceIndex, 3, "a Personality that is not moved named: the movement is not negated");
            NeverOpened(Build(act: false), ability, papers.Id, command);
        }
    }

    private static void MotoTaigoInterrupt()
    {
        const string ability = "w737-taigo-interrupt";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance taigo = null!, source = null!, target = null!;
            MatchEngine Build(bool follower = false, bool act = true, bool moves = true)
            {
                var s = Table(Phase.Battle, foe); s.PriorityPlayerId = foe; s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 3];
                source = Add(s, moves ? "training-move-source" : "training-battle-bower", foe, Zone.Battlefield, province: 1);
                target = Add(s, "training-battle-bower", owner, Zone.Battlefield, province: 1);
                taigo = Add(s, Def(ability), owner, Zone.Battlefield, province: 1);
                if (follower) Add(s, "training-follower", owner, Zone.Attached, province: 1, parent: target.Id);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, moves ? Enabled(e, foe, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 } : Offer(e, foe, "training-bow-at-battle", source.Id, target.Id));
                return e;
            }
            var e = Build();
            var command = InWindow(e, "beforeActionReaction", owner, ability, taigo.Id);
            Accept(e, command);
            Check(e.State.Cards[taigo.Id].Bowed, "Taigo bowed as the cost");
            Settle(e);
            Eq(e.State.Cards[target.Id].ProvinceIndex, 3, "the action moved the unit");
            if (e.State.PendingEventChoice is null) e.OpenQueuedChoicesForTest(e.State.PriorityPlayerId);
            Check(e.State.PendingChoice is { Type: "eventChoice" } && e.State.PendingEventChoice is { Kind: "p8-watch" } && e.State.PendingEventChoice.PlayerId == owner, "Taigo's controller is asked which card to destroy");
            var options = e.QueryActions(owner).Where(a => a.Type == "eventChoice").ToArray();
            Check(options.Any(a => a.Command!.TargetId == target.Id) && options.Any(a => a.Command!.Choice == "none"), "the moved unit's card, or none");
            Accept(e, options.First(a => a.Command!.TargetId == target.Id).Command!);
            Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the card without attachments was destroyed");
            // A unit with an attachment: its attachment is a choice, the host is not.
            var f = Build(follower: true);
            Accept(f, InWindow(f, "beforeActionReaction", owner, ability, taigo.Id));
            Settle(f);
            if (f.State.PendingEventChoice is null) f.OpenQueuedChoicesForTest(f.State.PriorityPlayerId);
            var choices = f.QueryActions(owner).Where(a => a.Type == "eventChoice").ToArray();
            Check(choices.Any(a => a.Command!.TargetId is not null && a.Command.TargetId != target.Id) && !choices.Any(a => a.Command!.TargetId == target.Id), "only the card without attachments is offered");
            // An action that moves nothing opens no window; before any action nothing is offered.
            var still = Build(moves: false);
            Check(still.State.PendingChoice?.Type != "beforeActionReaction" || !CanUse(still, owner, ability, taigo.Id), "no unit moves, so no Interrupt window");
            NeverOpened(Build(act: false), ability, taigo.Id, command);
        }
    }

    private static void VigilantRidersNegate()
    {
        const string ability = "vigilant-riders-negate";
        foreach (var foe in new[] { 0, 1 })
        {
            var owner = 1 - foe;
            CardInstance riders = null!, attack = null!, otherFollower = null!;
            MatchEngine Build(bool act = true, bool otherUnit = false)
            {
                var s = Table(Phase.Battle, foe); s.PriorityPlayerId = foe;
                var host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                riders = Add(s, Def(ability), owner, Zone.Attached, province: 0, parent: host.Id);
                var otherHost = Add(s, "training-crane-veteran", owner, Zone.Battlefield, province: 0);
                otherFollower = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: otherHost.Id);
                Add(s, "training-lion-samurai", foe, Zone.Battlefield, province: 0);
                attack = Add(s, "training-honor-cost-strategy", foe, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Offer(e, foe, "training-honor-cost-attack", attack.Id, otherUnit ? otherFollower.Id : riders.Id));
                return e;
            }
            var e = Build();
            var command = InWindow(e, "beforeActionReaction|attackReaction", owner, ability, riders.Id);
            Accept(e, command);
            PassUntil(e, () => e.State.PendingChoice is null && e.State.PendingAttack is null);
            Eq(e.State.Cards[riders.Id].Zone, Zone.Attached, "its destruction from the Attack was negated");
            var declined = Build();
            PassUntil(declined, () => declined.State.PendingChoice is null && declined.State.PendingAttack is null);
            Eq(declined.State.Cards[riders.Id].Zone, Zone.FateDiscard, "declined: the Ranged 3 Attack destroyed the Force 3 Follower");
            var other = Build(otherUnit: true);
            Check(!CanUse(other, owner, ability, riders.Id) && !CanUse(other, other.State.PriorityPlayerId, ability, riders.Id), "an action targeting another unit offers nothing");
            NeverOpened(Build(act: false), ability, riders.Id, command);
        }
    }

    private static void UtakuLishanPenalty()
    {
        const string ability = "lishan-penalty";
        foreach (var attacking in new[] { true, false })
            foreach (var owner in new[] { 0, 1 })
            {
                CardInstance lishan = null!, bower = null!, enemy = null!, follower = null!, mine = null!;
                MatchEngine Build(bool bowed = false, bool act = true, bool theirAction = false)
                {
                    var s = Table(Phase.Battle, attacking ? owner : 1 - owner); s.PriorityPlayerId = theirAction ? 1 - owner : owner;
                    lishan = Add(s, Def(ability), owner, Zone.Battlefield, bowed, 0);
                    bower = Add(s, "training-battle-bower", owner, Zone.Battlefield, province: 0);
                    enemy = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                    follower = Add(s, "training-follower", 1 - owner, Zone.Attached, province: 0, parent: enemy.Id);
                    mine = Add(s, "training-battle-bower", 1 - owner, Zone.Battlefield, province: 0);
                    var e = MatchEngine.FromTestState(s);
                    if (act) Accept(e, theirAction ? Offer(e, 1 - owner, "training-bow-at-battle", mine.Id, bower.Id) : Offer(e, owner, "training-bow-at-battle", bower.Id, enemy.Id));
                    return e;
                }
                var e = Build();
                var honor = e.PersonalHonorForTest(lishan.Id);
                Eq(honor, attacking ? 4 : 3, "her Personal Honor: +1 only while attacking");
                var options = Options(e, owner, ability, lishan.Id).Where(o => o.Enabled).ToArray();
                Check(options.Any(o => o.Command!.TargetId == enemy.Id) && options.Any(o => o.Command!.TargetId == follower.Id), "a Personality or a Follower opposing her");
                Check(options.All(o => o.Command!.TargetId != bower.Id), "her own side is not a target");
                var command = InWindow(e, "announceReaction", owner, ability, lishan.Id, null, follower.Id);
                Accept(e, command);
                Eq(ForceMods(e, follower.Id), -honor, "a Force penalty equal to her Personal Honor");
                PassUntil(e, () => e.State.PendingChoice is null);
                Check(e.State.Cards[enemy.Id].Bowed, "the announced action went on");
                // Bowed or at home: not asked. The other player's announced action: not hers.
                var bowed = Build(bowed: true);
                Check(bowed.State.PendingChoice?.Type != "announceReaction", "bowed: no window");
                var theirs = Build(theirAction: true);
                Check(!CanUse(theirs, owner, ability, lishan.Id), "only an action she announced opens it");
                NeverOpened(Build(act: false), ability, lishan.Id, command);
            }
    }

    private static void KalanisLandingReduce()
    {
        const string ability = "w816-kalani-reduce";
        foreach (var owner in new[] { 0, 1 })
            foreach (var actor in new[] { 0, 1 })
            {
                CardInstance kalani = null!, priced = null!, free = null!, announcer = null!;
                MatchEngine Build(bool act = true)
                {
                    var s = Table(Phase.Action, actor); s.PriorityPlayerId = actor;
                    kalani = s.Cards[s.Players[owner].StrongholdId]; kalani.DefinitionId = Def(ability);
                    announcer = Add(s, "training-crane-samurai", actor, Zone.Home);
                    announcer.GrantedAbilities.Add(new("training-plain-open", "test-fixture", GrantDuration.Permanent));
                    priced = Add(s, "training-lion-veteran", 1 - owner, Zone.Home); priced.Printed = CardCatalog.Get("training-lion-veteran") with { GoldCost = 3 };
                    free = Add(s, "training-crane-samurai", owner, Zone.Home); free.Printed = CardCatalog.Get("training-crane-samurai") with { GoldCost = 0 };
                    var e = MatchEngine.FromTestState(s);
                    if (act) Accept(e, Use(e, actor, "training-plain-open", announcer.Id));
                    return e;
                }
                var e = Build();
                Eq(e.CardGoldCostForTest(priced.Id), 3, "fixture: costs 3");
                Eq(e.State.PendingChoice?.Type, "announceReaction", "the announce window opens for Kalani's Landing's controller, whoever announced");
                Eq(e.State.PendingChoice?.PlayerId, owner);
                var option = Options(e, owner, ability, kalani.Id).First(o => o.Enabled);
                Check(option.TargetIds!.Contains(priced.Id) && !option.TargetIds.Contains(free.Id), "a card in play with a Gold Cost, not one with none");
                var command = InWindow(e, "announceReaction", owner, ability, kalani.Id, null, priced.Id);
                var before = Snapshot(e);
                Reject(e, command with { TargetId = free.Id });
                Eq(Snapshot(e), before, "an illegal target is refused with nothing changed");
                Accept(e, command);
                Eq(e.CardGoldCostForTest(priced.Id), 1, "3 - 2 = 1");
                Check(e.State.Cards[priced.Id].Modifiers.Any(m => m.Stat == "GoldCost" && m.Amount == -2 && !m.Permanent), "a turn-long modifier");
                Eq(e.CardGoldCostForTest(free.Id), 0, "nothing else changed");
                Check(e.State.Cards[kalani.Id].AbilityUses.ContainsKey(ability), "the use is spent");
                NeverOpened(Build(act: false), ability, kalani.Id, command);
            }
    }

    private static void ArcheryRangeBoost()
    {
        const string ability = "archery-range-reaction";
        foreach (var owner in new[] { 0, 1 })
            foreach (var shooter in new[] { 0, 1 })
            {
                CardInstance range = null!, prey = null!;
                MatchEngine Build(bool melee = false, bool withRange = true, bool act = true)
                {
                    var s = Table(Phase.Battle, shooter); s.PriorityPlayerId = shooter;
                    var gun = Add(s, "training-dual-attacker", shooter, Zone.Battlefield, province: 0);
                    prey = Add(s, "training-lion-samurai", 1 - shooter, Zone.Battlefield, province: 0); // Force 4
                    range = Add(s, Def(ability), owner, Zone.Home);
                    Add(s, "training-attack-reactor", 1 - owner, Zone.Home); // the other side still holds a Reaction, so the window stays open to read the strength
                    if (!withRange) range.Zone = Zone.FateDiscard;
                    var e = MatchEngine.FromTestState(s);
                    if (act) Accept(e, Offer(e, shooter, melee ? "training-weapon-attack" : "training-honor-cost-attack", gun.Id, prey.Id));
                    return e;
                }
                var e = Build();
                Eq(e.State.PendingChoice?.Type, "attackReaction", "the Ranged Attack waits for reactions");
                var strength = e.State.PendingAttack!.Strength;
                Eq(strength, 3, "Ranged 3");
                var command = InWindow(e, "attackReaction", owner, ability, range.Id);
                Accept(e, command);
                Eq(e.State.PendingAttack?.Strength, strength + 1, "+1 strength on the Attack");
                Check(!CanUse(e, owner, ability, range.Id), "once for this Attack");
                Settle(e);
                Eq(e.State.Cards[prey.Id].Zone, Zone.Dead, "Ranged 3 + 1 destroys the Force 4 target");
                // The control: declined, Ranged 3 does not reach Force 4.
                var declined = Build();
                Settle(declined);
                Eq(declined.State.Cards[prey.Id].Zone, Zone.Battlefield, "without the boost the Attack falls short");
                var melee = Build(melee: true);
                Check(melee.State.PendingChoice?.Type != "attackReaction" || !CanUse(melee, owner, ability, range.Id), "only a Ranged Attack");
                NeverOpened(Build(act: false), ability, range.Id, command);
            }
    }

    private static void LuxuriousSilkGold()
    {
        const string ability = "luxurious-silk-gold";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance silk = null!, spell = null!, shugenja = null!;
            MatchEngine Build(bool spent = false)
            {
                var s = Table(Phase.Battle, 1 - owner); s.PriorityPlayerId = owner;
                s.Cards[s.Players[owner].StrongholdId].DefinitionId = "oracle-l5r-1218"; // the player abilities (equip in battle) need a published Stronghold
                silk = Add(s, Def(ability), owner, Zone.Home, bowed: true);
                foreach (var card in s.Cards.Values.Where(c => c.OwnerId == owner && c.Id != silk.Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
                shugenja = Add(s, "training-shugenja", owner, Zone.Battlefield, province: 0);
                Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                spell = Add(s, "training-spell", owner, Zone.Hand);
                if (spent) silk.AbilityUses[ability] = s.TurnNumber;
                return MatchEngine.FromTestState(s);
            }
            var e = Build();
            var token = silk.Id + "#reaction";
            var cost = CardCatalog.Get("training-spell").GoldCost;
            var subject = new PaymentSubject(spell, PaymentKind.Equip);
            Check(cost is > 0 and <= 2, "the Spell costs 1 or 2 Gold, which the Silk alone covers");
            var pay = e.PaymentOptions(owner, cost, subject).FirstOrDefault(p => p.SourceIds.SequenceEqual([token]));
            Check(pay is not null, "the bowed Silk pays for an action by its Reaction");
            Check(!e.PaymentOptions(owner, 3, subject).Any(p => p.SourceIds.SequenceEqual([token])), "it produces exactly 2 Gold");
            Check(e.PaymentOptions(owner, 1, new PaymentSubject(Add(e.State, "training-crane-samurai", owner, Zone.Hand), PaymentKind.Recruit)).Length == 0, "not for a recruit");
            Check(!e.PaymentOptions(1 - owner, cost, new PaymentSubject(spell, PaymentKind.Equip)).Any(p => p.SourceIds.Contains(token)), "the other player's payment never includes it");
            Accept(e, new GameCommand("useAbility", owner, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell.Id, SourceIds: pay!.SourceIds));
            Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "the Spell was equipped with the Silk's Gold");
            Eq(e.State.Cards[spell.Id].AttachedTo, shugenja.Id);
            Check(e.State.Cards[silk.Id].Bowed, "the Silk stays bowed");
            Eq(e.State.Cards[silk.Id].AbilityUses[ability], e.State.TurnNumber, "the Reaction is spent for the turn");
            var spent = Build(spent: true);
            Check(spent.PaymentOptions(owner, 1, new PaymentSubject(spell, PaymentKind.Equip)).Length == 0, "the Reaction is spent for the turn: no payment");
            // Not offered outside any payment: in the Action Phase with nothing being paid for there is no option at all.
            Check(e.QueryActions(owner).All(a => a.Command?.AbilityId != ability), "never offered as a button: it is a payment source");
        }
    }

    private static void OneKokuGold()
    {
        const string ability = "w829-koku-gold";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance host = null!, weapon = null!, keep = null!, koku = null!, theirs = null!;
            MatchEngine Build(bool kokuInHand = true)
            {
                var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner;
                foreach (var card in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
                host = Add(s, "training-crane-samurai", owner, Zone.Home); keep = Add(s, "training-border-keep", owner, Zone.Home); keep.Bowed = false; weapon = Add(s, "training-costly-weapon", owner, Zone.Hand);
                koku = Add(s, Def(ability), owner, kokuInHand ? Zone.Hand : Zone.FateDiscard); theirs = Add(s, Def(ability), 1 - owner, Zone.Hand);
                return MatchEngine.FromTestState(s);
            }
            var e = Build();
            var token = koku.Id + "#koku";
            var subject = new PaymentSubject(weapon, PaymentKind.Equip, Wielder: host);
            var sources = e.PaymentSourcesForTest(owner, subject);
            Check(sources.Any(x => x.Id == token && x.Produced == 1), "in hand, One Koku is worth 1 Gold toward a Gold cost");
            Check(!sources.Any(x => x.Id == theirs.Id + "#koku"), "the other player's One Koku is not a source");
            Check(!e.PaymentSourcesForTest(1 - owner, subject).Any(x => x.Id == token), "nor is it a source of the other player");
            Check(e.QueryActions(owner).All(a => a.Command?.AbilityId != ability), "never listed as a button: it is a payment source");
            var cost = CardCatalog.Get("training-costly-weapon").GoldCost;
            var option = e.PaymentOptions(owner, cost, subject).FirstOrDefault(o => o.SourceIds.Contains(token));
            Check(option is not null, "a payment option names the Koku when it is needed");
            var command = new GameCommand("equip", owner, e.Version, CardId: weapon.Id, TargetId: host.Id, SourceIds: option!.SourceIds);
            Reject(e, command with { PlayerId = 1 - owner });
            Accept(e, command);
            Eq(e.State.Cards[koku.Id].Zone, Zone.FateDiscard, "choosing it discarded One Koku");
            Eq(e.State.Cards[weapon.Id].AttachedTo, host.Id, "the Weapon was bought with the Koku's Gold");
            Check(e.State.Cards[theirs.Id].Zone == Zone.Hand, "the other player's Koku is untouched");
            // Out of the hand (discarded already) it is not a source; not for nothing being paid.
            var gone = Build(kokuInHand: false);
            Check(!gone.PaymentSourcesForTest(owner, subject).Any(x => x.Id == token), "outside the hand it produces nothing");
            // Also toward a recruit ("when paying a Gold cost").
            var fresh = Build().State;
            var person = Add(fresh, "training-crane-samurai", owner, Zone.Hand);
            Check(MatchEngine.FromTestState(fresh).PaymentSourcesForTest(owner, new PaymentSubject(person, PaymentKind.Recruit)).Any(x => x.Id == token), "also toward a recruit");
        }
    }

    private static void RhetoricRedirect()
    {
        const string ability = "rhetoric-redirect";
        foreach (var actor in new[] { 0, 1 })
        {
            var owner = 1 - actor;
            CardInstance rhetoric = null!, courtier = null!, tale = null!;
            MatchEngine Build(int chiBonus = 0, bool act = true, bool bowed = false)
            {
                var s = Table(Phase.Action, actor); s.PriorityPlayerId = actor;
                tale = Add(s, Def("tell-the-tale-honor"), actor, Zone.Hand);
                rhetoric = Add(s, Def(ability), owner, Zone.Hand);
                courtier = Kw(Add(s, "training-lion-courtier", owner, Zone.Home, bowed), "Courtier"); // Chi 3
                if (chiBonus != 0) courtier.Modifiers.Add(new("fx", "fx", "Chi", chiBonus));
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Enabled(e, actor, "tell-the-tale-honor", tale.Id).Command!);
                return e;
            }
            var e = Build();
            var a0 = Honor(e, actor); var o0 = Honor(e, owner);
            Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "an action that gains Honor opens the window where the gain would be applied");
            var mine = Options(e, owner, ability, rhetoric.Id).Where(o => o.Enabled).ToArray();
            Check(mine.Any(o => o.Command!.TargetPlayerId == owner) && mine.Any(o => o.Command!.TargetPlayerId == actor), "either other player may be the one chosen");
            Check(!CanUse(e, actor, ability, rhetoric.Id), "not offered to the acting player");
            var command = InWindow(e, "honorChangeReaction", owner, ability, rhetoric.Id);
            Accept(e, mine.Single(o => o.Command!.TargetPlayerId == owner).Command!);
            Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
            PassUntil(e, () => e.State.PendingChoice is null);
            Eq(Honor(e, actor), a0, "the original player did not gain");
            Eq(Honor(e, owner), o0 + 1, "the chosen player took the gain instead");
            // His Chi must cover the change: with 0 Chi a gain of 1 stays with the original player.
            var weak = Build(-3);
            var w0 = Honor(weak, actor); var w1 = Honor(weak, owner);
            Accept(weak, weak.QueryActions(owner).Single(x => x.Type == "useAbility" && x.Command?.AbilityId == ability && x.Enabled && x.Command.TargetPlayerId == owner).Command!);
            PassUntil(weak, () => weak.State.PendingChoice is null);
            Check(Honor(weak, actor) == w0 + 1 && Honor(weak, owner) == w1, "Chi 0 does not cover a gain of 1: the original player gains it");
            // A bowed Courtier opens no window; before any action nothing is offered.
            var bowed = Build(bowed: true);
            Check(bowed.State.PendingChoice is null, "no unbowed Courtier: no window");
            NeverOpened(Build(act: false), ability, rhetoric.Id, command);
        }
    }

    private static void HofukushusVengeance()
    {
        const string ability = "pe-hofukushus-vengeance-declare";
        foreach (var defender in new[] { 0, 1 })
        {
            var attacker = 1 - defender;
            CardInstance celestial = null!, lead = null!, second = null!;
            MatchEngine Build(int holder, bool win = true)
            {
                var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = defender;
                for (var i = 1; i < 4; i++) s.Players[attacker].Provinces[i].Destroyed = true; // the Attacker keeps one province: one battlefield in the additional phase
                celestial = Add(s, Def(ability), holder, Zone.Celestial);
                Add(s, "training-lion-veteran", attacker, Zone.Battlefield, province: 0); // beats the lone defender
                Add(s, "training-crane-samurai", defender, Zone.Battlefield, province: 0);
                lead = Add(s, "training-crane-veteran", defender, Zone.Home);
                second = Add(s, "training-crane-samurai", defender, Zone.Home);
                Add(s, "training-lion-veteran", attacker, Zone.Home);
                return MatchEngine.FromTestState(s);
            }
            var e = Build(defender);
            PassUntil(e, () => e.State.PendingChoice?.Type == "attackPhaseEndReaction");
            Eq(e.State.PriorityPlayerId, defender, "the Defender holds the window");
            Check(!e.QueryActions(attacker).Any(o => o.Type == "useAbility"), "the Attacker is offered nothing");
            var command = InWindow(e, "attackPhaseEndReaction", defender, ability, celestial.Id);
            Accept(e, command);
            Eq(e.State.Phase, Phase.InfantryDeclaration, "an additional Attack Phase begins");
            Eq(e.State.ActivePlayerId, defender, "the Defender is its Attacker");
            Eq(e.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
            var attackOptions = e.QueryActions(defender).Where(o => o.Type == "attack" && o.Command is not null).ToArray();
            Check(attackOptions.Length == 2 && attackOptions.All(o => o.Command!.Assignments!.Count == 1), "only one Personality may be assigned: one option per (unit, province)");
            Reject(e, new("attack", defender, e.Version, Assignments: new Dictionary<string, int> { [lead.Id] = 0, [second.Id] = 0 }));
            Accept(e, attackOptions.First().Command!);
            Eq(e.State.Cards.Values.Count(c => c.OwnerId == defender && c.Zone == Zone.Battlefield && (c.Id == lead.Id || c.Id == second.Id)), 1, "exactly one Personality was assigned");
            // The Attacker's own Hofukushu opens no window; at no other time is it offered.
            var theirs = Build(attacker);
            PassUntil(theirs, () => theirs.State.Phase == Phase.Dynasty);
            Eq(theirs.State.ActivePlayerId, attacker, "the Attacker's Hofukushu opens no window: the Dynasty Phase follows the last battle");
            var idle = Table(Phase.Action, defender); var idleCard = Add(idle, Def(ability), defender, Zone.Celestial);
            Check(!CanUse(MatchEngine.FromTestState(idle), defender, ability, idleCard.Id), "not in the Action Phase");
            NeverOpened(Build(defender), ability, celestial.Id, command);
        }
    }

    private static void TreasureHunterRetire()
    {
        const string ability = "promo-treasure-hunter-retire";
        const string charter = "oracle-l5r-10806";
        foreach (var attacker in new[] { 0, 1 })
        {
            var defender = 1 - attacker;
            CardInstance hunter = null!;
            MatchEngine Build(int force = 30, bool onDefender = false, Action<MatchState>? tweak = null)
            {
                var s = Table(Phase.Battle, attacker);
                var lead = Add(s, "training-lion-veteran", attacker, Zone.Battlefield, province: 0); Batch336Support.Force(lead, force);
                var guard = Add(s, "training-crane-samurai", defender, Zone.Battlefield, province: 0);
                hunter = onDefender ? Add(s, Def(ability), defender, Zone.Attached, province: 0, parent: guard.Id) : Add(s, Def(ability), attacker, Zone.Attached, province: 0, parent: lead.Id);
                tweak?.Invoke(s);
                var e = MatchEngine.FromTestState(s);
                for (var step = 0; step < 8 && e.State.PendingChoice?.Type != "afterResolutionReaction" && e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince is not null; step++)
                    Accept(e, new GameCommand("pass", e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.Version));
                return e;
            }
            var e = Build();
            Eq(e.State.PendingChoice?.Type, "afterResolutionReaction", "the after-resolution window opens");
            Eq(e.State.PendingAfterResolution!.ProvinceDestroyedBy, attacker, "precondition: the Attacker destroyed the province");
            var command = InWindow(e, "afterResolutionReaction", attacker, ability, hunter.Id);
            var holdings = e.State.Cards.Values.Count(c => c.OwnerId == attacker && c.DefinitionId == charter);
            Accept(e, command);
            Check(e.State.Cards[hunter.Id].Zone is Zone.FateDiscard or Zone.Dead, "the Follower was destroyed as the cost");
            var created = e.State.Cards.Values.Where(c => c.OwnerId == attacker && c.DefinitionId == charter && c.Zone == Zone.Home).ToArray();
            Eq(created.Length, holdings + 1, "a Holding was created at home");
            Check(created.All(h => h.Bowed), "it entered play bowed");
            Eq(CardCatalog.Get(charter).GoldProduction, 2, "it bows to produce 2 Gold");
            Settle(e);
            // Declined: nothing happens. No province destroyed: no window. A Defender's Treasure Hunter: not offered. Outside a battle: not offered.
            var declined = Build(); Settle(declined);
            Check(declined.State.Cards[hunter.Id].Zone == Zone.Attached, "a Reaction is optional");
            var noProvince = Build(0, tweak: s => s.Players[defender].Provinces[0].Strength = 99);
            Check(noProvince.State.PendingChoice?.Type != "afterResolutionReaction" && !CanUse(noProvince, attacker, ability, hunter.Id), "no province destroyed: no Reaction");
            var onDefender = Build(onDefender: true);
            Check(onDefender.State.PendingChoice?.Type != "afterResolutionReaction" || !CanUse(onDefender, defender, ability, hunter.Id), "a Defender's Follower is not offered it");
            var idle = Table(Phase.Action, attacker);
            var host = Add(idle, "training-lion-samurai", attacker, Zone.Home);
            var idleHunter = Add(idle, Def(ability), attacker, Zone.Attached, parent: host.Id);
            var h = MatchEngine.FromTestState(idle);
            NeverOpened(h, ability, idleHunter.Id, command with { CardId = idleHunter.Id });
        }
    }
}
