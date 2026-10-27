using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #833, single-reaction audit slice 2: cards that print exactly ONE Reaction. Each test opens the Reaction's real trigger window through ordinary
// engine play (a purchase, a Strategy, an engage, a Battle action ...), proves it is offered enabled to its own player only (the other player's identical
// command is refused), accepted with the printed effect checked against the card text, and then proves the same command is neither offered nor accepted
// once the window has closed or when the trigger never happened. Where a window had to be forged (no real path), the test says so.
internal static partial class SingleReaction2Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }
    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static bool Offered(MatchEngine e, int player, string ability, string? cardId = null) => Listed(e, player, ability).Any(a => a.Enabled && (cardId is null || a.CardId == cardId));

    // Declines whatever the window is until it closes.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 16 && e.State.PendingChoice is not null; guard++)
        {
            var step = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "passReaction" or "pass");
            if (step is null) break;
            Accept(e, step.Command!);
        }
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner, listed enabled for nobody else, refused if the other player submits it; on a copy
    // where the window is declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`).
    private static GameCommand InWindow(MatchEngine e, int owner, string ability, string cardId, string? performerId = null, string? targetId = null)
    {
        Check(e.State.PendingChoice is not null, ability + ": no window is open");
        if (e.State.PriorityPlayerId != owner && e.State.PendingChoice is not null && e.State.PendingChoice.PlayerId != owner) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the other player holds the first opportunity and has nothing to react with
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner} in {e.State.PendingChoice?.Type}: " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        var command = targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
        Check(!CanUse(e, 1 - owner, ability, cardId), ability + " is listed enabled for the other player");
        Reject(e, command with { PlayerId = 1 - owner });
        Closed(e, owner, ability, cardId, command, performerId);
        return command;
    }

    private static void Closed(MatchEngine e, int owner, string ability, string cardId, GameCommand command, string? performerId = null)
    {
        var closed = e.Clone();
        Settle(closed);
        Check(closed.State.PendingChoice is null, ability + ": the window did not close");
        Check(!CanUse(closed, owner, ability, cardId, performerId), ability + " is still offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
    }

    // No window ever opened: not offered to either player, and the command is refused.
    private static void NeverOpened(MatchEngine e, int owner, string ability, string cardId, GameCommand command)
    {
        Check(!CanUse(e, owner, ability, cardId) && !CanUse(e, 1 - owner, ability, cardId), ability + " is offered with no window open");
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = owner, CardId = cardId });
    }

    // A card bought from `buyer`'s province 0 in the Dynasty Phase, leaving its entering-play window open.
    private static (MatchEngine E, string Id) Buy(string def, Action<MatchState>? tweak = null, int gold = 14, int buyer = 0, int province = 0, bool window = true)
    {
        var s = Table(Phase.Dynasty, buyer); s.PriorityPlayerId = buyer;
        s.Players[buyer].Clan = "Dragon"; s.Players[buyer].Honor = 30;
        Gold(s, buyer, gold);
        var entering = s.Cards[s.Players[buyer].Provinces[province].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(buyer).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false });
        Accept(e, option.Command!);
        Check(!window || e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"{def} bought, but the window is {e.State.PendingChoice?.Type}");
        return (e, entering.Id);
    }

    // The same Personality standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, out CardInstance card, Action<MatchState, CardInstance>? tweak = null, int owner = 0)
    {
        var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner;
        card = Add(s, def, owner, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    private static int Force(MatchEngine e, string id) => e.CardForceForTest(id);

    public static void Run(TestRunner runner) => runner.Run("single-reaction-2", [
        ("Bayushi Makubesu: after he enters play his controller (only) creates two 1F Followers on one or two of their Personalities; not offered at home or after the window", MakubesuEntry),
        ("Kitsune Merihiko: after he enters play his controller (only) creates a 0F/2C/3PH Mantis Swarm at home; not offered at home or after the window", MerihikoEntry),
        ("Thick Marsh: after the Terrain enters play (Battle, either side) its player (only) bows an enemy card at the current battlefield; not elsewhere, not own cards", ThickMarshEntry),
        ("Yogo Ugimori: after he enters play from a province his controller (only) chooses a player locked out of Proclaim until the controller's next turn; not at home", UgimoriEntry),
        ("Shosuro Nigawa and Utaku Sayaka: Invest 3 after entering play gives two +1F tokens to his controller only, once; declined, unpaid or later gives nothing", InvestEntry),
        ("A Simple Yari: after one of your Personalities enters play the Yari equips to him paying its cost; not on an opponent's Personality, not after the window", YariEntry),
        ("Interesting Plains: after a Personality enters play from this province its controller (only) refills the province face-up; not another province, not a Holding", PlainsEntry),
        ("Mirumoto Ezuno: another player's action (Battle, either side, or Action phase) aimed at your Shugenja at his location is redirected to Ezuno by his controller only; not his own action, a non-Shugenja, Ezuno himself or another location", EzunoRedirect),
        ("Goju Yurishi: another player's Battle action targeting him (either side) lets his controller only move him home and negate its effects on him; not an Open action, not his controller's own action", YurishiDisappear),
        ("Hida Mochitoko: another player's action at your Follower at his location lets his controller only negate its bowing, twice per turn; not an enemy Follower, a Follower elsewhere or a third time", MochitokoNegate),
        ("Taishuu: another player's Political action (Battle or Open) aimed at your Personality lets his controller only negate its effects; not a non-Political action, not your own Political action", TaishuuNegate),
        ("Cavalry Detachment: another player's action targeting its Personality (any window) lets the Detachment's controller only remove it to create a 3F/1C/2PH Cavalry Samurai at his location taking his attachments; not own action, not another Personality", CavalryDetachmentSpawn),
        ("Ryoshun's Guidance: another player's action targeting one of your cards lets you discard the Celestial to send it to another of your cards at the same location; only its controller, only the same location", RyoshunRedirect),
        ("Benten's Blessing: another player's action targeting one of your Personalities lets you discard the Celestial to retarget your other performing Personality; only its controller, never the same Personality", BentenRedirect),
        ("Colonist's Mark: after an action targets its wielder either player's controller of the wielder discards a card to negate his movement; Battle windows (no Open move exists), not other wielders", ColonistsMark),
        ("The Pearl-Encrusted Staff of the Cobras: before a Battle action that would move a Personality resolves its controller targets him to negate his movement; either player's action, not outside a Battle", PearlStaff),
        ("Training Rokai: after another player's action would destroy your Personality at his location, destroying Rokai negates that destruction; only his controller, not another location or a non-destroying action", RokaiNegate),
        ("Training bow guard: before another player's action resolves its controller negates the bowing of his Personality it targets; either window, only its controller", BowGuard),
        ("Kaiu Hideaki: after engaging at his battlefield his controller (attacker or defender, only) raises the province's strength by 3 until the battle's resolution begins; not from home", HideakiEngage),
        ("Wasteland Scout: after engaging at its battlefield its controller (either side, only) has Reconnaissance there and gives his other Followers there +1F; not the enemy's, not at home", WastelandScoutEngage),
        ("Kyuden Hida (Exp): after engaging its controller (either side, only) has Reconnaissance and attaches 0F Scouts to one to three army Personalities, removed when the battle ends", KyudenHidaEngage),
        ("Sneak Attack: after engaging either side may play it from hand and the ATTACKER has the first opportunity; not before the engage, not after the window", SneakAttackEngage),
        ("Training Engage claim: after engaging either side may play it from hand and the ATTACKER has the first opportunity; not outside the Engage window", TrainingClaimEngage),
        ("New Cavalry Tactics: Engage for 3 Gold, two (Commander: three) 1F Elephant Followers on your Personality; only its player, only in the Engage window", CavalryTacticsEngage),
        ("Khalimpeh-jiak: Engage, bow it: an enemy Personality or Follower gets -2F; only enemies, only in the Engage window, only while unbowed", KhalimpehEngage),
        ("Asahina Akikusa: after an action on your own Air Spell resolves (Battle or Open) its controller gains 1 Honor; not for a Spell without Air or the opponent's Spell action", AkikusaHonor),
        ("Shinjo Byung: after a Tactical Battle action he performed resolves his controller (only) takes an additional Battle action; not when another performs it", ByungAdditional),
        ("Pillars of Virtue: after a Kiho Battle action its controller took resolves he (only) takes an additional Battle action; not non-Kiho, not the opponent's Kiho action", PillarsAdditional),
        ("Itoku (Exp): after an action bowed your non-Void Ring its controller straightens it and may use its abilities an additional time; not a Void Ring, not the opponent", ItokuRing),
        ("I Cast Two Shadows: after an action moved your Ninja Personality home you (only) move him to a different battlefield; not a non-Ninja, not the same battlefield", CastTwoShadows),
        ("Deep Snows: after a Battle action resolves (either player's) with no Terrain at the battlefield its owner puts it into play there; not with a Terrain, not after an Open action", DeepSnowsEnter),
        ("Akodo Shihiro: before each of another player's actions resolves during a battle at his battlefield its controller gives him +1F until the turn ends; not his own action, not the Action phase, not another battlefield", ShihiroBoost),
        ("Fallow Lands: after a Melee or Ranged Attack at its own province is targeted its defending controller (only) gives it -2 strength; not as the attacker, not another province", FallowLandsReduce),
        ("He's Mine!: before your target Personality enters a duel you bow him and your unbowed Samurai at the same location enters instead and bows after; only the reactor, not bowed, not elsewhere, no duel", HesMineSubstitute),
        ("Kitsuki Yataku: after an action targets a Samurai and would create a refusable challenge bowing him stops the Samurai's controller refusing; not a plain action, not a non-Samurai", YatakuNoRefuse),
        ("Red Crane Dojo: Tireless Reaction after an action targets your Ninja or Shadowlands Personality removes both from him until the action ends; only its controller, not another's Personality or a plain one", RedCraneDojo),
        ("Seppun Ryota: before an action resolves that targeted a Personality at his location he enters the duel it creates instead; only his controller, not from elsewhere", RyotaSubstitute),
        ("Mantle of the Jade Champion: even bowed, paying for a Spell lets its controller produce 4 Gold (once a turn); not for anything but a Spell, not the opponent, not a 5-Gold Spell alone", JadeMantleGold),
        ("Training rallying commander: before a battle at his battlefield resolves his controller's army is not bowed by its resolution (only that battle, only that army); bowed or at home differs", DairukoRally),
        ("Turning the Story: before a Proclaim resolves the player holding it negates the Proclaim's effects; only its holder, not with it in no hand", TurningTheStory),
    ]);

    // ---- entering-play windows -----------------------------------------------------------------------------------------------------------------------------------------------
    private const string Conscripts = "oracle-l5r-10004";

    private static void MakubesuEntry()
    {
        const string ability = "makubesu-create";
        var def = Def(ability);
        CardInstance ally = null!;
        var (e, id) = Buy(def, s => { ally = Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); });
        var command = InWindow(e, 0, ability, id);
        var hosts = Options(e, 0, ability, id).First(o => o.Enabled).Slots!.Single(x => x.Slot == "hosts");
        Check(hosts.CardIds.Contains(id) && hosts.CardIds.Contains(ally.Id) && hosts.CardIds.Length == 2, "only his controller's own Personalities may host: " + string.Join(",", hosts.CardIds));
        Accept(e, command with { TargetId = null, Selections = [Pick("hosts", id, ally.Id)] });
        var created = e.State.Cards.Values.Where(c => c.DefinitionId == Conscripts).ToArray();
        Eq(created.Length, 2, "two Followers");
        Check(created.All(c => c.OwnerId == 0 && c.Zone == Zone.Attached), "created attached, under their creator's control");
        Check(created.Any(c => c.AttachedTo == id) && created.Any(c => c.AttachedTo == ally.Id), "one on each chosen Personality");
        Eq(CardCatalog.Get(Conscripts).Force, 1, "the created Follower is printed 1F");
        Check(created.All(c => Force(e, c.Id) == 1), "each Follower has 1F");
        Check(!Offered(e, 0, ability, id), "taken once for this entry");
        var h = AtHome(def, out var m);
        NeverOpened(h, 0, ability, m.Id, command);
    }

    private static void MerihikoEntry()
    {
        const string ability = "merihiko-create";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var command = InWindow(e, 0, ability, id);
        var known = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var swarm = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        var swarmDef = CardCatalog.Get(swarm.DefinitionId);
        Check(swarm.OwnerId == 0 && swarm.Zone == Zone.Home && !swarm.Bowed, "the Swarm is created at home, unbowed, under his controller");
        Eq((swarmDef.Force, swarmDef.Chi, swarmDef.PersonalHonor), (0, 2, 3), "0F/2C/3PH");
        Check(new HashSet<string>(swarmDef.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Bee", "Cavalry", "Spirit", "Swarm"]), "Mantis Clan Nonhuman Bee Cavalry Spirit Swarm");
        Check(swarmDef.Abilities!.SequenceEqual(["merihikos-swarm-attack"]), "with the Battle bow-for-Ranged-4 ability");
        Eq(e.AttackStrengthForTest("merihikos-swarm-attack", swarm.Id), 4, "Ranged 4");
        Eq(AbilityCatalog.All["merihikos-swarm-attack"].Timing, "Battle", "a Battle ability");
        Check(!Offered(e, 0, ability, id), "taken once for this entry");
        var h = AtHome(def, out var m);
        NeverOpened(h, 0, ability, m.Id, command);
    }

    private static void ThickMarshEntry()
    {
        const string ability = "thick-marsh-entry";
        var def = Def(ability);
        foreach (var me in new[] { 0, 1 })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
            Add(s, "training-water-personality", me, Zone.Battlefield, province: 0);
            var marsh = Add(s, def, me, Zone.Hand);
            var enemyHere = Add(s, "training-lion-samurai", 1 - me, Zone.Battlefield, province: 0);
            var enemyElsewhere = Add(s, "training-lion-samurai", 1 - me, Zone.Battlefield, province: 1);
            var mine = Add(s, "training-crane-samurai", me, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, me, ability, marsh.Id), "not offered while the Strategy is still in hand");
            Accept(e, new GameCommand("playStrategy", me, e.Version, CardId: marsh.Id));
            Eq(e.State.Cards[marsh.Id].Zone, Zone.Battlefield, "Thick Marsh entered play");
            Eq(e.State.PendingChoice?.Type, "entryReaction", "its entry window opened");
            var option = Options(e, me, ability, marsh.Id).First(o => o.Enabled);
            Check(option.TargetIds!.Contains(enemyHere.Id) && !option.TargetIds.Contains(enemyElsewhere.Id) && !option.TargetIds.Contains(mine.Id), "only the enemy card at the current battlefield: " + string.Join(",", option.TargetIds!));
            var command = InWindow(e, me, ability, marsh.Id, targetId: enemyHere.Id);
            Reject(e, command with { TargetId = enemyElsewhere.Id });
            Reject(e, command with { TargetId = mine.Id });
            Accept(e, command);
            Check(e.State.Cards[enemyHere.Id].Bowed, $"player {me}: the enemy card at the battlefield is bowed");
            Check(!e.State.Cards[enemyElsewhere.Id].Bowed && !e.State.Cards[mine.Id].Bowed, "nothing else bowed");
            Check(!Offered(e, me, ability, marsh.Id), "taken once for this entry");
        }
    }

    private static void UgimoriEntry()
    {
        const string ability = "ugimori-proclaim-lock";
        var def = Def(ability);
        foreach (var chosen in new[] { 0, 1 })
        {
            var (e, id) = Buy(def, s => Add(s, "training-lion-samurai", 1, Zone.Home));
            InWindow(e, 0, ability, id);
            var option = Options(e, 0, ability, id).First(o => o.Enabled && o.Command!.TargetPlayerId == chosen);
            Accept(e, option.Command!);
            Check(e.State.ProclaimLocks.Any(l => l.PlayerId == chosen && l.OwnerId == 0), $"player {chosen} is locked out of Proclaim");
            Eq(e.State.ProclaimLocks.Count(l => l.OwnerId == 0), 1, "exactly the chosen player");
            Check(!Offered(e, 0, ability, id), "taken once for this entry");
            if (chosen == 1)
            {
                var t = e.State; t.PriorityPlayerId = 1; t.ActivePlayerId = 1; t.Phase = Phase.Dynasty; t.TurnNumber++; t.Players[1].Clan = "Lion"; t.Players[1].Honor = 30; Gold(t, 1, 8);
                var lion = t.Cards[t.Players[1].Provinces[0].CardId!]; lion.DefinitionId = "training-lion-samurai"; lion.FaceUp = true; t.PendingChoice = null;
                var locked = MatchEngine.FromTestState(t);
                Check(locked.QueryActions(1).Where(a => a.Type == "purchase" && a.CardId == lion.Id).All(a => a.Quote is { ProclaimEligible: false }), "locked: no Proclaim-eligible purchase");
                t.Players[0].TurnsTaken++;
                Check(MatchEngine.FromTestState(t).QueryActions(1).Any(a => a.Type == "purchase" && a.CardId == lion.Id && a.Quote is { ProclaimEligible: true }), "free once the controller's next turn has begun");
            }
        }
        var h = AtHome(def, out var u);
        Reject(h, Use(h, 0, ability, u.Id));
        Check(!CanUse(h, 0, ability, u.Id), "not entering from a province: not offered");
    }

    private static void InvestEntry()
    {
        const string ability = "wl2-invest-two-tokens";
        foreach (var (name, cost) in new[] { ("Shosuro Nigawa", 4), ("Utaku Sayaka", 5) })
        {
            var def = Def(ability, name);
            var (e, id) = Buy(def, null, 16);
            var option = Options(e, 0, ability, id).First(o => o.Enabled);
            var printed = Force(e, id);
            var pay = option.Command! with { SourceIds = e.PaymentOptions(0, 3).First().SourceIds };
            Check(!CanUse(e, 1, ability, id), "not offered to the opponent");
            Reject(e, pay with { PlayerId = 1 });
            Reject(e, option.Command! with { SourceIds = [] }); // Invest 3 must be paid
            Eq(Force(e, id), printed, "an unpaid attempt gave nothing");
            Closed(e, 0, ability, id, pay);
            Accept(e, pay);
            Eq(Force(e, id), printed + 2, name + ": two +1F tokens");
            Eq(e.State.Cards[id].Tokens.Count(t => t.Type == "Invest"), 2, "two Invest tokens");
            Check(!Offered(e, 0, ability, id), "the Invest is once");
            Reject(e, pay with { ExpectedVersion = e.Version });
            var h = AtHome(def, out var c);
            NeverOpened(h, 0, ability, c.Id, pay);
            Eq(CardCatalog.Get(def).GoldCost, cost, name + " costs " + cost);
        }
    }

    private static void YariEntry()
    {
        const string ability = "simple-yari-attach";
        var yariDef = Def(ability);
        const string samurai = "training-crane-clan-samurai";
        MatchEngine fresh(int buyer, out string yari, out string entered)
        {
            string yid = null!;
            var (e, id) = Buy(samurai, s => { yid = Add(s, yariDef, 0, Zone.Hand).Id; }, 14, buyer, 0, false);
            yari = yid; entered = id; return e;
        }
        var e = fresh(0, out var yariId, out var guy);
        var command = InWindow(e, 0, ability, yariId);
        var gold = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(e, command);
        var yari = e.State.Cards[yariId];
        Check(yari.Zone == Zone.Attached && yari.AttachedTo == guy && !e.State.Players[0].Hand.Contains(yariId), "the Yari is attached to him and has left the hand");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed) > gold || CardCatalog.Get(yariDef).GoldCost == 0, "its cost was paid");
        // The opponent's Personality entering is not "one of your Personalities".
        var o = fresh(1, out var yariId2, out _);
        Check(!Offered(o, 0, ability, yariId2), "not offered for the opponent's Personality");
        Reject(o, command with { CardId = yariId2, ExpectedVersion = o.Version });
        // Nothing entered: the Yari in hand is not offered.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; Gold(s, 0, 6);
        var held = Add(s, yariDef, 0, Zone.Hand); Add(s, samurai, 0, Zone.Home);
        var h = MatchEngine.FromTestState(s);
        NeverOpened(h, 0, ability, held.Id, command);
    }

    private static void PlainsEntry()
    {
        const string ability = "pe-interesting-plains-refill";
        var def = Def(ability);
        CardInstance Region(MatchState s, int owner, int province) { var r = Add(s, def, owner, Zone.Region, province: province); s.Players[owner].Provinces[province].RegionId = r.Id; return r; }
        string region = null!;
        var (e, entered) = Buy("training-crane-samurai", s => region = Region(s, 0, 0).Id);
        var refilled = e.State.Players[0].Provinces[0].CardId!;
        Check(!e.State.Cards[refilled].FaceUp, "the province refilled face-down");
        var command = InWindow(e, 0, ability, region);
        Accept(e, command);
        Check(e.State.Cards[refilled].FaceUp, "after the Reaction the province's card is face-up");
        Check(!Offered(e, 0, ability, region), "taken once for this entry");
        // A Region on the opponent's province is not offered to the buyer's opponent either (it reacts to its own province's entry).
        string theirs = null!;
        var (o, _) = Buy("training-crane-samurai", s => { Region(s, 0, 1); theirs = Region(s, 1, 0).Id; });
        Check(!Offered(o, 0, ability) && !Offered(o, 1, ability, theirs), "a Region on another province does not react");
        // Another province of the same player: not offered.
        var (p, _) = Buy("training-crane-samurai", s => region = Region(s, 0, 0).Id, 14, 0, 1);
        Check(!CanUse(p, 0, ability, region), "a Personality from another province: not offered");
        // A Holding is not a Personality.
        var (hld, _) = Buy("training-marketplace", s => region = Region(s, 0, 0).Id, 14, 0, 0, false);
        Check(!CanUse(hld, 0, ability, region), "a Holding entering play is not a Personality");
    }

    // ---- actions that target a card: the held action's "beforeActionReaction" window ------------------------------------------------------------------------------------------
    // Player 0 is always the reacting player and player 1 the acting player. Four layouts: the Battle phase with the actor attacking or defending, and the Action phase
    // with the actor the active player or not. Cards sit at the current battlefield in a Battle and at home in the Action phase.
    private sealed record Lay(string Name, Phase Phase, int Active)
    {
        public bool Battle => Phase == Phase.Battle;
        public Zone Zone => Battle ? Zone.Battlefield : Zone.Home;
        public int? Prov => Battle ? 0 : null;
    }
    private static readonly Lay[] Lays = [new("battle: actor attacks", Phase.Battle, 1), new("battle: actor defends", Phase.Battle, 0), new("action: actor active", Phase.Action, 1), new("action: reactor active", Phase.Action, 0)];
    private static string where = "";
    private static MatchState Setup(Lay l) { where = l.Name; var s = Table(l.Phase, l.Active); s.PriorityPlayerId = 1; return s; }
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        try { return Batch336Support.Offer(e, player, ability, cardId, targetId, performerId); }
        catch (Exception ex) { throw new Exception($"[{where}] {ex.Message}"); }
    }
    private static CardInstance Put(MatchState s, string def, int owner, Lay l, bool bowed = false) => Add(s, def, owner, l.Zone, bowed, l.Prov);
    // Player 1's bow action on a Personality (Battle or Open as the layout allows), or on any card.
    private static (CardInstance Card, string Ability) Bower(MatchState s, Lay l, int owner = 1, bool anyCard = false) =>
        anyCard ? (Put(s, "training-card-bower", owner, l), l.Battle ? "training-bow-enemy-card-at-battle" : "training-bow-enemy-card-open")
        : l.Battle ? (Put(s, "training-battle-bower", owner, l), "training-bow-at-battle") : (Put(s, "training-bow-any-source", owner, l), "training-bow-any");
    private static string? Window(MatchEngine e) => e.State.PendingChoice?.Type;
    private static void NoWindow(MatchEngine e, int player, string ability, string why) { Check(Window(e) != "beforeActionReaction" && !Offered(e, player, ability), ability + ": " + why); }

    private static void EzunoRedirect()
    {
        const string ability = "ezuno-redirect";
        var ezuno = Def(ability);
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var ez = Put(s, ezuno, 0, l); var shugenja = Put(s, "training-shugenja", 0, l);
            var (bower, bow) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, bow, bower.Id, shugenja.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the held action opens the window");
            Check(!e.State.Cards[shugenja.Id].Bowed, "the action is held: nothing has happened yet");
            var command = InWindow(e, 0, ability, ez.Id);
            Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the acting player is offered no reaction");
            Accept(e, command);
            Eq(e.State.PendingNegatableAbility!.TargetId, ez.Id, l.Name + ": the held action now targets Ezuno");
            Check(!Offered(e, 0, ability, ez.Id), "Ezuno is spent for the turn");
            Settle(e);
            Check(e.State.Cards[ez.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed, l.Name + ": the action resolved on Ezuno and left the Shugenja alone");
            // Not covered: a non-Shugenja, Ezuno himself, his controller's own action.
            var t = Setup(l); var ez2 = Put(t, ezuno, 0, l); var plain = Put(t, "training-crane-samurai", 0, l); Put(t, "training-shugenja", 0, l); var (b2, ab2) = Bower(t, l);
            var f = MatchEngine.FromTestState(t);
            Accept(f, Offer(f, 1, ab2, b2.Id, plain.Id)); NoWindow(f, 0, ability, "a non-Shugenja is not covered");
            var g = MatchEngine.FromTestState(t);
            Accept(g, Offer(g, 1, ab2, b2.Id, ez2.Id)); NoWindow(g, 0, ability, "an action at Ezuno himself is not redirected");
            if (l.Battle) continue; // no Battle fixture lets a player target his own Personality
            var u = Setup(l); Put(u, ezuno, 0, l); var sh3 = Put(u, "training-shugenja", 0, l); var (b3, ab3) = Bower(u, l, 0);
            u.PriorityPlayerId = 0;
            var h = MatchEngine.FromTestState(u);
            Accept(h, Offer(h, 0, ab3, b3.Id, sh3.Id)); NoWindow(h, 0, ability, "his controller's own action");
        }
        // A Shugenja at a different battlefield (Open action in the Action phase) is out of Ezuno's location.
        var o = Setup(Lays[2]); Put(o, ezuno, 0, Lays[2]); var away = Add(o, "training-shugenja", 0, Zone.Battlefield, province: 1); var (ob, oab) = Bower(o, Lays[2]);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 1, oab, ob.Id, away.Id)); NoWindow(oe, 0, ability, "a Shugenja at another location");
    }

    private static void YurishiDisappear()
    {
        const string ability = "yurishi-disappear";
        var def = Def(ability);
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var y = Put(s, def, 0, l); var (bower, bow) = Bower(s, l);
            Add(s, "training-farm", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, bow, bower.Id, y.Id));
            if (!l.Battle)
            {
                NoWindow(e, 0, ability, "an Open action is not a Battle action");
                Check(e.State.Cards[y.Id].Bowed, "and the action simply resolved");
                continue;
            }
            Eq(Window(e), "beforeActionReaction", l.Name + ": the window opens");
            var command = InWindow(e, 0, ability, y.Id);
            Accept(e, command);
            Eq(e.State.Cards[y.Id].Zone, Zone.Home, l.Name + ": he disappears home");
            Check(!e.State.Cards[y.Id].Bowed, "unbowed: moved, not bowed");
            Check(e.State.NegatedEffects.Any(n => n.CardId == y.Id && n.Kind == "all"), "no longer a legal target at the battlefield: its effects on him are negated");
            Settle(e);
            Check(!e.State.Cards[y.Id].Bowed && e.State.Cards[y.Id].Zone == Zone.Home, "the bow was negated");
            Check(!Offered(e, 0, ability, y.Id), "spent for the turn");
        }
    }

    private static void MochitokoNegate()
    {
        const string ability = "mochitoko-negate";
        var def = Def(ability);
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var moch = Put(s, def, 0, l); var host = Put(s, "training-lion-samurai", 0, l);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: l.Prov, parent: host.Id);
            var bowers = Enumerable.Range(0, 4).Select(_ => Bower(s, l, 1, true)).ToArray();
            var e = MatchEngine.FromTestState(s);
            for (var use = 1; use <= 2; use++)
            {
                Accept(e, Offer(e, 1, bowers[use - 1].Ability, bowers[use - 1].Card.Id, follower.Id));
                Eq(Window(e), "beforeActionReaction", $"{l.Name} use {use}: the window opens");
                var command = InWindow(e, 0, ability, moch.Id);
                Accept(e, command);
                Check(e.State.NegatedEffects.Any(n => n.CardId == follower.Id && n.Kind == "bow"), "its bowing is negated");
                Settle(e);
                Check(!e.State.Cards[follower.Id].Bowed && e.State.Cards[follower.Id].Zone == Zone.Attached, $"{l.Name} use {use}: the Follower is not bowed");
                if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            }
            Accept(e, Offer(e, 1, bowers[2].Ability, bowers[2].Card.Id, follower.Id));
            NoWindow(e, 0, ability, "twice per turn and no more");
            Check(e.State.Cards[follower.Id].Bowed, "the third bow lands");
            // An enemy Follower, and a Follower of his controller's at another location, are not covered.
            var t = Setup(l); Put(t, def, 0, l);
            var theirHost = Put(t, "training-lion-samurai", 1, l); var theirs = Add(t, "training-follower", 1, Zone.Attached, province: l.Prov, parent: theirHost.Id);
            var (b, ab) = Bower(t, l, 0, true); t.PriorityPlayerId = 0;
            var f = MatchEngine.FromTestState(t);
            Accept(f, Offer(f, 0, ab, b.Id, theirs.Id)); NoWindow(f, 0, ability, "an enemy Follower is not covered");
        }
        var o = Setup(Lays[0]); Put(o, def, 0, Lays[0]);
        var far = Add(o, "training-lion-samurai", 0, Zone.Battlefield, province: 1); var farFollower = Add(o, "training-follower", 0, Zone.Attached, province: 1, parent: far.Id);
        var (ob, oab) = Bower(o, Lays[0], 1, true);
        var oe = MatchEngine.FromTestState(o);
        var offerable = Options(oe, 1, oab, ob.Id).SelectMany(x => x.TargetIds ?? []).Contains(farFollower.Id);
        if (offerable) { Accept(oe, Offer(oe, 1, oab, ob.Id, farFollower.Id)); NoWindow(oe, 0, ability, "a Follower at another location"); }
    }

    private static void TaishuuNegate()
    {
        const string ability = "taishuu-political-negate";
        var def = Def(ability);
        var soshi = Def("soshi-conditional-bow");
        foreach (var l in Lays)
        {
            MatchState Build(out CardInstance taishuu, out CardInstance mine, out CardInstance actor, int actorOwner = 1)
            {
                var s = Setup(l);
                taishuu = Put(s, def, 0, l); mine = Put(s, "training-crane-samurai", 0, l); mine.Dishonorable = true;
                var host = Put(s, "training-lion-samurai", actorOwner, l);
                actor = Add(s, soshi, actorOwner, Zone.Attached, province: l.Prov, parent: host.Id); // Soshi House Guard is a Follower
                return s;
            }
            // Without Taishuu's reaction the Political action bows him (in a battle) and costs his controller 1 Honor.
            var s0 = Build(out _, out var m0, out var a0);
            var control = MatchEngine.FromTestState(s0);
            var honor = Honor(control, 0);
            Accept(control, Offer(control, 1, "soshi-conditional-bow", a0.Id, m0.Id));
            Settle(control);
            Eq(Honor(control, 0), honor - 1, l.Name + ": control, the dishonorable target's controller loses 1 Honor");
            var s = Build(out var taishuu, out var mine, out var actor);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "soshi-conditional-bow", actor.Id, mine.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the Political action opens the window");
            var command = InWindow(e, 0, ability, taishuu.Id);
            Accept(e, command);
            Settle(e);
            Eq(Honor(e, 0), honor, l.Name + ": the effects were negated, no Honor lost");
            Check(!e.State.Cards[mine.Id].Bowed, "and he is not bowed");
            Check(!Offered(e, 0, ability, taishuu.Id), "taken for this action");
            // A non-Political action (a plain bow) opens nothing for Taishuu.
            var t = Setup(l); Put(t, def, 0, l); var target = Put(t, "training-crane-samurai", 0, l); var (b, ab) = Bower(t, l);
            var f = MatchEngine.FromTestState(t);
            Accept(f, Offer(f, 1, ab, b.Id, target.Id)); NoWindow(f, 0, ability, "a non-Political action");
            // His controller's own Political action is not "another player's".
            var u = Build(out _, out var m2, out var a2, 0); u.PriorityPlayerId = 0;
            var h = MatchEngine.FromTestState(u);
            Accept(h, Offer(h, 0, "soshi-conditional-bow", a2.Id, m2.Id)); NoWindow(h, 0, ability, "his own controller's Political action");
        }
    }

    private const string DetachedPersonality = "oracle-l5r-10201";

    private static void CavalryDetachmentSpawn()
    {
        const string ability = "cavalry-detachment-spawn";
        var def = Def(ability);
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var host = Put(s, "training-lion-samurai", 0, l);
            var detachment = Add(s, def, 0, Zone.Attached, province: l.Prov, parent: host.Id);
            var other = Add(s, "training-follower", 0, Zone.Attached, province: l.Prov, parent: host.Id);
            var weapon = Add(s, "training-weapon", 0, Zone.Attached, province: l.Prov, parent: host.Id);
            var (actor, bow) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, bow, actor.Id, host.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the window opens for the Personality's controller");
            var command = InWindow(e, 0, ability, detachment.Id);
            Accept(e, command);
            Eq(e.State.Cards[detachment.Id].Zone, Zone.RemovedFromGame, "the Detachment is removed from the game");
            var created = e.State.Cards.Values.Single(c => c.DefinitionId == DetachedPersonality);
            var printed = CardCatalog.Get(DetachedPersonality);
            Eq((printed.Force, printed.Chi, printed.PersonalHonor), (3, 1, 2), "3F/1C/2PH");
            Check(created.Zone == l.Zone && created.ProvinceIndex == l.Prov && created.OwnerId == 0, l.Name + ": created at the target's location for the target's controller");
            Check(e.HasKeyword(created.Id, "Crane Clan") && e.HasKeyword(created.Id, "Samurai") && e.HasKeyword(created.Id, "Cavalry"), "Samurai Cavalry with the creator's Clan alignment");
            Check(e.State.Cards[other.Id].AttachedTo == created.Id && e.State.Cards[weapon.Id].AttachedTo == created.Id, "the target's attachments were transferred");
            Settle(e);
            Check(e.State.Cards[host.Id].Bowed && !e.State.Cards[created.Id].Bowed, "the held action resolved on the bare target");
            // An action aimed at another Personality, or the controller's own action, is not its trigger.
            var t = Setup(l); var host2 = Put(t, "training-lion-samurai", 0, l); Add(t, def, 0, Zone.Attached, province: l.Prov, parent: host2.Id);
            var ally = Put(t, "training-crane-samurai", 0, l); var (b2, ab2) = Bower(t, l);
            var f = MatchEngine.FromTestState(t);
            Accept(f, Offer(f, 1, ab2, b2.Id, ally.Id)); NoWindow(f, 0, ability, "an action on another Personality");
            if (l.Battle) continue;
            var u = Setup(l); var host3 = Put(u, "training-lion-samurai", 0, l); Add(u, def, 0, Zone.Attached, parent: host3.Id); var (b3, ab3) = Bower(u, l, 0); u.PriorityPlayerId = 0;
            var g = MatchEngine.FromTestState(u);
            Accept(g, Offer(g, 0, ab3, b3.Id, host3.Id)); NoWindow(g, 0, ability, "his own controller's action");
        }
    }

    private static void RyoshunRedirect()
    {
        const string ability = "ryoshun-redirect";
        var def = Def(ability);
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var ryoshun = Add(s, def, 0, Zone.Celestial);
            var victim = Put(s, "training-crane-samurai", 0, l); var standIn = Put(s, "training-crane-samurai", 0, l);
            var elsewhere = Add(s, "training-crane-samurai", 0, l.Battle ? Zone.Home : Zone.Battlefield, province: l.Battle ? null : 0);
            var (actor, bow) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, bow, actor.Id, victim.Id));
            var offered = Listed(e, 0, ability).Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
            Check(offered.SequenceEqual([standIn.Id]), l.Name + ": only another of his cards at the same location: " + string.Join(",", offered));
            var command = InWindow(e, 0, ability, ryoshun.Id, targetId: standIn.Id);
            Reject(e, command with { TargetId = elsewhere.Id });
            Reject(e, command with { TargetId = victim.Id });
            Accept(e, command);
            Eq(e.State.Cards[ryoshun.Id].Zone, Zone.DynastyDiscard, "discarded as the cost");
            Settle(e);
            Check(e.State.Cards[standIn.Id].Bowed && !e.State.Cards[victim.Id].Bowed, l.Name + ": the action bowed the stand-in");
            Check(!e.State.Cards[elsewhere.Id].Bowed, "nothing else");
            if (l.Battle) continue;
            var u = Setup(l); Add(u, def, 0, Zone.Celestial); var own = Put(u, "training-crane-samurai", 0, l); Put(u, "training-crane-samurai", 0, l); var (b, ab) = Bower(u, l, 0); u.PriorityPlayerId = 0;
            var f = MatchEngine.FromTestState(u);
            Accept(f, Offer(f, 0, ab, b.Id, own.Id)); NoWindow(f, 0, ability, "his own controller's action");
        }
    }

    private static void BentenRedirect()
    {
        const string ability = "w829-benten-redirect";
        var def = Def(ability);
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var benten = Add(s, def, 0, Zone.Celestial);
            var victim = Put(s, "training-crane-samurai", 0, l); var spare = Put(s, "training-crane-samurai", 0, l);
            var (actor, bow) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, bow, actor.Id, victim.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the window opens");
            var enabled = Listed(e, 0, ability).Where(o => o.Enabled).ToArray();
            Check(enabled.All(o => o.Command!.PerformerId != victim.Id) && enabled.Any(o => o.Command!.PerformerId == spare.Id), "the other Personality may be named, never the one the action targets");
            var command = enabled.First(o => o.Command!.PerformerId == spare.Id).Command!;
            Check(!CanUse(e, 1, ability, benten.Id), "not offered to the acting player");
            Reject(e, command with { PlayerId = 1 });
            Reject(e, command with { PerformerId = victim.Id });
            Closed(e, 0, ability, benten.Id, command);
            Accept(e, command);
            Eq(e.State.Cards[benten.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
            Check(e.State.Events.Any(x => x.Type == "actionRedirected"), "the redirect is logged");
            Settle(e);
            Check(e.State.Cards[spare.Id].Bowed && !e.State.Cards[victim.Id].Bowed, l.Name + ": the action resolved on the performing Personality");
            // A lone Personality has no other to redirect to; the controller's own action is not "another player's".
            var t = Setup(l); Add(t, def, 0, Zone.Celestial); var only = Put(t, "training-crane-samurai", 0, l); var (b2, ab2) = Bower(t, l);
            var f = MatchEngine.FromTestState(t);
            Accept(f, Offer(f, 1, ab2, b2.Id, only.Id)); NoWindow(f, 0, ability, "no other Personality to redirect to");
            if (l.Battle) continue;
            var u = Setup(l); Add(u, def, 0, Zone.Celestial); var a = Put(u, "training-crane-samurai", 0, l); Put(u, "training-crane-samurai", 0, l); var (b3, ab3) = Bower(u, l, 0); u.PriorityPlayerId = 0;
            var g = MatchEngine.FromTestState(u);
            Accept(g, Offer(g, 0, ab3, b3.Id, a.Id)); NoWindow(g, 0, ability, "his own controller's action");
        }
    }

    private static void ColonistsMark()
    {
        const string ability = "promo-d-colonists-mark-negate-move";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
        {
            var s = Setup(l);
            var wielder = Put(s, "training-crane-samurai", 0, l);
            var mark = Add(s, def, 0, Zone.Attached, province: l.Prov, parent: wielder.Id);
            var spare = Add(s, "training-farm", 0, Zone.Hand); Add(s, "training-farm", 0, Zone.Hand);
            var mover = Put(s, "training-move-source", 1, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-move-both-home", mover.Id, wielder.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the window opens");
            var options = Options(e, 0, ability, mark.Id).Where(o => o.Enabled).ToArray();
            Eq(options.Length, 2, "one offer per card of the hand");
            var command = InWindow(e, 0, ability, mark.Id, targetId: spare.Id);
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, command);
            Eq(e.State.Players[0].Hand.Count, hand - 1, "the chosen card was discarded");
            Check(!e.State.Players[0].Hand.Contains(spare.Id) && e.State.Cards[spare.Id].Zone is Zone.FateDiscard or Zone.DynastyDiscard, "and it is in the discard pile");
            Check(e.State.NegatedEffects.Any(n => n.CardId == wielder.Id && n.Kind == "move"), "the wielder's movement is negated");
            Settle(e);
            Eq(e.State.Cards[wielder.Id].Zone, Zone.Battlefield, l.Name + ": he was not moved home");
            Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the acting Personality's own movement is not negated");
            // Control: without the Mark's reaction the wielder is moved home.
            var c = Setup(l); var w2 = Put(c, "training-crane-samurai", 0, l); var m2 = Put(c, "training-move-source", 1, l);
            var ce = MatchEngine.FromTestState(c);
            Accept(ce, Offer(ce, 1, "training-move-both-home", m2.Id, w2.Id)); Settle(ce);
            Eq(ce.State.Cards[w2.Id].Zone, Zone.Home, "control: moved home");
            // No card to discard: nothing to offer. Another Personality targeted: not this Personality.
            var n = Setup(l); var w3 = Put(n, "training-crane-samurai", 0, l); Add(n, def, 0, Zone.Attached, province: l.Prov, parent: w3.Id); var m3 = Put(n, "training-move-source", 1, l);
            var ne = MatchEngine.FromTestState(n);
            Accept(ne, Offer(ne, 1, "training-move-both-home", m3.Id, w3.Id)); NoWindow(ne, 0, ability, "an empty hand has nothing to discard: no window opens for the Mark"); Check(!Options(ne, 0, ability).Any(x => x.Enabled), "and it is not offered");
            var o = Setup(l); var w4 = Put(o, "training-crane-samurai", 0, l); Add(o, def, 0, Zone.Attached, province: l.Prov, parent: w4.Id); var bystander = Put(o, "training-crane-samurai", 0, l);
            Add(o, "training-farm", 0, Zone.Hand); var m4 = Put(o, "training-move-source", 1, l);
            var oe = MatchEngine.FromTestState(o);
            Accept(oe, Offer(oe, 1, "training-move-both-home", m4.Id, bystander.Id)); NoWindow(oe, 0, ability, "the action targeted another Personality");
        }
        // Open windows: no Open action in the game moves a Personality, so an Open bow targeting the wielder does not offer it (nothing to negate).
        var a = Setup(Lays[2]); var w = Put(a, "training-crane-samurai", 0, Lays[2]); var m = Add(a, def, 0, Zone.Attached, parent: w.Id); Add(a, "training-farm", 0, Zone.Hand);
        var (b, ab) = Bower(a, Lays[2]);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 1, ab, b.Id, w.Id));
        if (Window(ae) == "beforeActionReaction") { var offered = Offered(ae, 0, ability, m.Id); Check(offered, "if the window opens the Mark is offered"); }
    }

    private static void PearlStaff()
    {
        const string ability = "pe-pearl-negate-movement";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
            foreach (var own in new[] { false, true })
            {
                var s = Setup(l);
                var holder = Kw(Put(s, "training-crane-samurai", own ? 1 : 0, l), "Spider Clan");
                var staff = Add(s, def, own ? 1 : 0, Zone.Attached, province: l.Prov, parent: holder.Id);
                var victim = Put(s, "training-crane-samurai", 0, l); var mover = Put(s, "training-move-source", 1, l);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, 1, "training-move-both-home", mover.Id, victim.Id));
                Eq(Window(e), "beforeActionReaction", $"{l.Name} (staff of player {(own ? 1 : 0)}): a Battle action that moves opens the window");
                var owner = own ? 1 : 0;
                var targets = Options(e, owner, ability, staff.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId!).ToArray();
                Check(targets.Contains(victim.Id) && targets.Contains(mover.Id), "each Personality (here both would be moved) may be named: " + string.Join(",", targets));
                var command = InWindow(e, owner, ability, staff.Id, targetId: victim.Id);
                Accept(e, command);
                Check(e.State.NegatedEffects.Any(n => n.CardId == victim.Id && n.Kind == "move"), "his movement is negated");
                Settle(e);
                Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "he was not moved home");
                Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the other Personality still moved");
                // The mover himself may be named instead.
                var t = Setup(l); var h2 = Kw(Put(t, "training-crane-samurai", 0, l), "Naga"); Add(t, def, 0, Zone.Attached, province: l.Prov, parent: h2.Id);
                var v2 = Put(t, "training-crane-samurai", 0, l); var m2 = Put(t, "training-move-source", 1, l);
                var te = MatchEngine.FromTestState(t);
                Accept(te, Offer(te, 1, "training-move-both-home", m2.Id, v2.Id));
                Accept(te, InWindow(te, 0, ability, Options(te, 0, ability).First(o => o.Enabled).CardId!, targetId: m2.Id));
                Settle(te);
                Check(te.State.Cards[m2.Id].Zone == Zone.Battlefield && te.State.Cards[v2.Id].Zone == Zone.Home, "naming the mover keeps him at the battlefield");
                // A Battle action that moves nobody opens nothing.
                var u = Setup(l); var h3 = Kw(Put(u, "training-crane-samurai", 0, l), "Spider Clan"); Add(u, def, 0, Zone.Attached, province: l.Prov, parent: h3.Id);
                var v3 = Put(u, "training-crane-samurai", 0, l); var (b3, ab3) = Bower(u, l);
                var ue = MatchEngine.FromTestState(u);
                Accept(ue, Offer(ue, 1, ab3, b3.Id, v3.Id)); NoWindow(ue, 0, ability, "a bow moves nobody");
            }
        // Outside a battle there is no Battle action: an Open action in the Action phase does not offer it.
        var a = Setup(Lays[2]); var hh = Kw(Put(a, "training-crane-samurai", 0, Lays[2]), "Spider Clan"); var st = Add(a, def, 0, Zone.Attached, parent: hh.Id); var target = Put(a, "training-crane-samurai", 0, Lays[2]); var (ob, oab) = Bower(a, Lays[2]);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 1, oab, ob.Id, target.Id)); NoWindow(ae, 0, ability, "an Open action");
        Reject(ae, Use(ae, 0, ability, st.Id, target.Id));
    }

    private static void RokaiNegate()
    {
        const string ability = "rokai-negate-destruction";
        var def = "training-rokai";
        foreach (var l in Lays.Where(x => x.Battle))
        {
            var s = Setup(l);
            var rokai = Put(s, def, 0, l); var victim = Put(s, "training-crane-samurai", 0, l);
            var killer = Put(s, "training-no-attachments-destroyer", 1, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-destroy-no-attachments", killer.Id, victim.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the destroying action opens the window");
            var command = InWindow(e, 0, ability, rokai.Id);
            Accept(e, command);
            Check(e.State.Cards[rokai.Id].Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard, "Rokai was destroyed as the cost");
            Settle(e);
            Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, l.Name + ": the destruction was negated");
            // Control: without Rokai the Personality dies.
            var c = Setup(l); var v2 = Put(c, "training-crane-samurai", 0, l); var k2 = Put(c, "training-no-attachments-destroyer", 1, l);
            var ce = MatchEngine.FromTestState(c);
            Accept(ce, Offer(ce, 1, "training-destroy-no-attachments", k2.Id, v2.Id)); Settle(ce);
            Eq(ce.State.Cards[v2.Id].Zone, Zone.Dead, "control: destroyed");
            // Rokai at another location, or an action that does not destroy, opens nothing.
            var t = Setup(l); Add(t, def, 0, Zone.Battlefield, province: 1); var v3 = Put(t, "training-crane-samurai", 0, l); var k3 = Put(t, "training-no-attachments-destroyer", 1, l);
            var te = MatchEngine.FromTestState(t);
            Accept(te, Offer(te, 1, "training-destroy-no-attachments", k3.Id, v3.Id)); NoWindow(te, 0, ability, "Rokai is at another location");
            var u = Setup(l); Put(u, def, 0, l); var v4 = Put(u, "training-crane-samurai", 0, l); var (b4, ab4) = Bower(u, l);
            var ue = MatchEngine.FromTestState(u);
            Accept(ue, Offer(ue, 1, ab4, b4.Id, v4.Id)); NoWindow(ue, 0, ability, "a bow does not destroy");
        }
    }

    private static void BowGuard()
    {
        const string ability = "training-bow-guard";
        const string def = "training-bow-guard-personality";
        foreach (var l in Lays)
        {
            var s = Setup(l);
            var guard = Put(s, def, 0, l); var mine = Put(s, "training-crane-samurai", 0, l);
            var (actor, bow) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, bow, actor.Id, mine.Id));
            Eq(Window(e), "beforeActionReaction", l.Name + ": the window opens");
            var command = InWindow(e, 0, ability, guard.Id);
            Accept(e, command);
            Check(e.State.NegatedEffects.Any(n => n.CardId == mine.Id && n.Kind == "bow"), "the bowing is negated");
            Settle(e);
            Check(!e.State.Cards[mine.Id].Bowed, l.Name + ": he was not bowed");
            // Control: without the guard he is bowed.
            var c = Setup(l); var m2 = Put(c, "training-crane-samurai", 0, l); var (a2, ab2) = Bower(c, l);
            var ce = MatchEngine.FromTestState(c);
            Accept(ce, Offer(ce, 1, ab2, a2.Id, m2.Id)); Settle(ce);
            Check(ce.State.Cards[m2.Id].Bowed, "control: bowed");
            // An enemy Personality is not his controller's: a bow of the actor's own Personality offers nothing to the guard's controller.
            var t = Setup(l); Put(t, def, 0, l); var theirs = Put(t, "training-crane-samurai", 1, l); var (a3, ab3) = Bower(t, l, 0); t.PriorityPlayerId = 0;
            if (l.Battle) continue;
            var te = MatchEngine.FromTestState(t);
            Accept(te, Offer(te, 0, ab3, a3.Id, theirs.Id)); NoWindow(te, 0, ability, "the target is not his controller's Personality");
        }
    }

    // ---- engage windows ---------------------------------------------------------------------------------------------------------------------------------------------------------
    // Battle Selection with player 0 the Attacker of the Defender's province 0, a unit on each side; `arrange` adds the card under test. selectBattle then opens the Engage window.
    private static MatchEngine Engaged(Action<MatchState> arrange, bool units = true)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        if (units) { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); }
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        return e;
    }
    // The Engage window is open; the other player, holding the first opportunity, has nothing to react with and passes so that `owner` holds it.
    private static void Declare(MatchEngine e, int owner = -1)
    {
        Eq(Window(e), "engageReaction", "the Engage window is open");
        if (owner >= 0 && e.State.PriorityPlayerId != owner) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static void FinishBattle(MatchEngine e)
    {
        for (var guard = 0; guard < 40 && (e.State.PendingChoice is not null || e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince is not null); guard++)
            Accept(e, new GameCommand(e.State.PendingChoice is not null ? "declineReaction" : "pass", e.State.PriorityPlayerId, e.Version));
    }

    private static void HideakiEngage()
    {
        const string ability = "hideaki-province";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance hideaki = null!;
            var e = Engaged(s => hideaki = Add(s, def, owner, Zone.Battlefield, province: 0));
            Declare(e);
            var baseStrength = e.ProvinceStrengthForTest(1, 0);
            var command = InWindow(e, owner, ability, hideaki.Id);
            Accept(e, command);
            Eq(e.ProvinceStrengthForTest(1, 0), baseStrength + 3, $"owner {owner}: +3 strength on the battlefield's province");
            Eq(e.ProvinceStrengthForTest(1, 1), e.ProvinceStrengthForTest(1, 1), "(only that province)");
            Check(!Offered(e, owner, ability, hideaki.Id), "once per window");
            for (var guard = 0; Window(e) == "engageReaction" && guard < 6; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.ProvinceStrengthForTest(1, 0), baseStrength + 3, "it lasts through the Combat Segment");
            FinishBattle(e);
            Eq(e.ProvinceStrengthForTest(1, 0), baseStrength, "gone once the resolution began");
            // Hideaki at home is not at the engaged battlefield.
            var away = Engaged(s => Add(s, def, owner, Zone.Home));
            Check(!Offered(away, owner, ability), "no Reaction away from the battlefield");
            Reject(away, Use(away, owner, ability, away.State.Cards.Values.First(c => c.DefinitionId == def).Id));
        }
    }

    private static void WastelandScoutEngage()
    {
        const string ability = "wasteland-scout-recon";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance scout = null!, sibling = null!, otherFollower = null!, homeFollower = null!, enemyFollower = null!;
            var e = Engaged(s =>
            {
                var host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                scout = Add(s, def, owner, Zone.Attached, province: 0, parent: host.Id);
                sibling = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: host.Id);
                var other = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                otherFollower = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: other.Id);
                var home = Add(s, "training-crane-samurai", owner, Zone.Home);
                homeFollower = Add(s, "training-follower", owner, Zone.Attached, parent: home.Id);
                var enemy = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                enemyFollower = Add(s, "training-follower", 1 - owner, Zone.Attached, province: 0, parent: enemy.Id);
            });
            Declare(e);
            var command = InWindow(e, owner, ability, scout.Id);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner] && !e.State.Players[1].Provinces[0].Reconnaissance[1 - owner], $"owner {owner}: only its controller has Reconnaissance at the battlefield");
            int Bonus(CardInstance c) => e.State.Cards[c.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);
            Eq(Bonus(sibling), 1, "the other Follower in its unit: +1F");
            Eq(Bonus(otherFollower), 1, "another unit's Follower there: +1F");
            Eq((Bonus(scout), Bonus(homeFollower), Bonus(enemyFollower)), (0, 0, 0), "not itself, not a Follower at home, not the enemy's");
            // The Scout at home has no engagement to react to.
            var away = Engaged(s => { var h = Add(s, "training-crane-samurai", owner, Zone.Home); Add(s, def, owner, Zone.Attached, parent: h.Id); });
            Check(!Offered(away, owner, ability), "a Scout at home: not offered");
        }
    }

    private static void KyudenHidaEngage()
    {
        const string ability = "kyuden-hida-exp-scouts";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var units = new List<CardInstance>();
            var e = Engaged(s =>
            {
                foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218";
                s.Cards[s.Players[owner].StrongholdId].DefinitionId = def;
                foreach (var d in new[] { "training-lion-samurai", "training-crane-samurai", "training-lion-veteran" }) units.Add(Add(s, d, owner, Zone.Battlefield, province: 0));
            });
            Declare(e);
            var stronghold = e.State.Players[owner].StrongholdId;
            var command = InWindow(e, owner, ability, stronghold);
            var chosen = new[] { units[0].Id, units[2].Id };
            Accept(e, command with { TargetId = null, CardIds = chosen });
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner] && !e.State.Players[1].Provinces[0].Reconnaissance[1 - owner], $"owner {owner}: Reconnaissance for the user only");
            var scouts = e.State.Cards.Values.Where(c => c.Zone == Zone.Attached && CardCatalog.Get(c.DefinitionId).Name == "Kyuden Hida Survivor").ToArray();
            Eq(scouts.Length, 2, "one 0F Scout per chosen Personality");
            Check(scouts.All(f => f.OwnerId == owner && f.Zone == Zone.Attached && chosen.Contains(f.AttachedTo!) && CardCatalog.Get(f.DefinitionId).Force == 0), "attached to the chosen Personalities, 0F");
            Check(scouts.Select(f => f.AttachedTo).Distinct().Count() == 2, "each got its own");
            FinishBattle(e);
            Check(scouts.All(f => e.State.Cards[f.Id].Zone == Zone.RemovedFromGame), "removed from the game after the battle ends");
            // No army, no Reaction.
            var none = Engaged(s => { foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; s.Cards[s.Players[owner].StrongholdId].DefinitionId = def; Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", owner, Zone.Home); }, false);
            Check(!Offered(none, owner, ability), "no Personality in the army: not offered");
        }
    }

    private static void ClaimTest(string ability, string def)
    {
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance card = null!;
            var e = Engaged(s => card = Add(s, def, owner, Zone.Hand));
            Declare(e);
            var command = InWindow(e, owner, ability, card.Id);
            Accept(e, command);
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            for (var guard = 0; Window(e) == "engageReaction" && guard < 6; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.FirstActionPlayerId, 0, $"played by player {owner}: the ATTACKER has the first opportunity");
            Eq(e.State.PriorityPlayerId, 0, "and holds priority");
            // In hand in an ordinary Battle, outside any Engage window, it is not available.
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = owner; var held = Add(s, def, owner, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var b = MatchEngine.FromTestState(s);
            NeverOpened(b, owner, ability, held.Id, command);
        }
    }
    private static void SneakAttackEngage() => ClaimTest("sneak-attack-claim", Def("sneak-attack-claim"));
    private static void TrainingClaimEngage() => ClaimTest("training-engage-attacker-first", "training-engage-claim-strategy");

    private static void CavalryTacticsEngage()
    {
        const string ability = "proxy677-cavalry-tactics";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
            foreach (var commander in new[] { false, true })
            {
                CardInstance card = null!, target = null!, enemy = null!;
                var e = Engaged(s =>
                {
                    card = Add(s, def, owner, Zone.Hand);
                    target = Add(s, commander ? "oracle-l5r-10316" : "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                    enemy = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                    Gold(s, owner, 6);
                }, false);
                Declare(e, owner);
                var option = Enabled(e, owner, ability, card.Id);
                Eq(option.Gold?.Cost, 3, "3 Gold");
                Check(!option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(target.Id), "only your Personality is a target");
                var pay = e.PaymentOptions(owner, 3).First().SourceIds;
                var command = InWindow(e, owner, ability, card.Id, targetId: target.Id) with { SourceIds = pay };
                Reject(e, command with { TargetId = enemy.Id });
                var bowedBefore = e.State.Cards.Values.Count(c => c.OwnerId == owner && c.Bowed);
                Accept(e, command);
                var elephants = e.State.Cards.Values.Where(c => c.Zone == Zone.Attached && c.AttachedTo == target.Id && CardCatalog.Get(c.DefinitionId).Keywords!.Contains("Elephant")).ToArray();
                Eq(elephants.Length, commander ? 3 : 2, $"owner {owner}: " + (commander ? "three for a Commander" : "two"));
                Check(elephants.All(x => CardCatalog.Get(x.DefinitionId).Force == 1 && new HashSet<string>(CardCatalog.Get(x.DefinitionId).Keywords!).SetEquals(["Elephant", "Nonhuman", "Cavalry"]) && x.OwnerId == owner), "each a 1F Elephant Nonhuman Cavalry Follower");
                Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
                Check(e.State.Cards.Values.Count(c => c.OwnerId == owner && c.Bowed) > bowedBefore, "the 3 Gold was paid");
            }
    }

    private static void KhalimpehEngage()
    {
        const string ability = "wl-khalimpeh-penalty";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance item = null!, enemy = null!, enemyFollower = null!, mine = null!;
            var e = Engaged(s =>
            {
                var host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                item = Add(s, def, owner, Zone.Attached, province: 0, parent: host.Id);
                mine = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: host.Id);
                enemy = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                enemyFollower = Add(s, "training-follower", 1 - owner, Zone.Attached, province: 0, parent: enemy.Id);
            }, false);
            Declare(e, owner);
            var targets = Options(e, owner, ability, item.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Concat(Options(e, owner, ability, item.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId!)).Distinct().ToArray();
            Check(targets.Contains(enemy.Id) && targets.Contains(enemyFollower.Id) && !targets.Contains(mine.Id), $"owner {owner}: only enemy Personalities and Followers: " + string.Join(",", targets));
            foreach (var victim in new[] { enemy, enemyFollower })
            {
                var copy = e.Clone();
                var force = Force(copy, victim.Id);
                var command = InWindow(copy, owner, ability, item.Id, targetId: victim.Id);
                Accept(copy, command);
                Eq(Force(copy, victim.Id), Math.Max(0, force - 2), $"{victim.Id}: -2F");
                Check(copy.State.Cards[item.Id].Bowed, "the item bowed as its cost");
                Check(!Offered(copy, owner, ability, item.Id), "a bowed item cannot be used again");
            }
            Reject(e, Use(e, owner, ability, item.Id, mine.Id));
            // A bowed item, or one used once the Engage window is over, is not available.
            var bowed = Engaged(s => { var h = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0); Add(s, def, owner, Zone.Attached, bowed: true, province: 0, parent: h.Id); Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0); }, false);
            Check(!Offered(bowed, owner, ability), "a bowed item");
            var late = Table(Phase.Battle, 0); late.PriorityPlayerId = owner; var h2 = Add(late, "training-crane-samurai", owner, Zone.Battlefield, province: 0); var it = Add(late, def, owner, Zone.Attached, province: 0, parent: h2.Id);
            var tgt = Add(late, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
            var le = MatchEngine.FromTestState(late);
            NeverOpened(le, owner, ability, it.Id, Use(le, owner, ability, it.Id, tgt.Id));
        }
    }

    // ---- after-action windows ---------------------------------------------------------------------------------------------------------------------------------------------------
    private static MatchState SetupFor(Lay l, int actor) { var s = Setup(l); s.PriorityPlayerId = actor; return s; }
    private static void ToAfter(MatchEngine e)
    {
        for (var guard = 0; guard < 40 && Window(e) is not ("afterActionResolvedReaction" or null); guard++)
            Accept(e, new GameCommand(Window(e) == "effectChoice" ? "pass" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static void AkikusaHonor()
    {
        const string ability = "akikusa-honor";
        var def = Def(ability);
        var scent = Def("scent-of-dishonor-bow");
        foreach (var l in Lays)
            foreach (var actor in new[] { 0, 1 })
            {
                // The acting player's Air Spell, and (for the second pass) the opponent holding Akikusa.
                var akikusaOwner = 0;
                CardInstance spell = null!, target = null!;
                string spellAbility = null!;
                MatchState Build(bool air = true)
                {
                    var s = SetupFor(l, actor);
                    Put(s, def, akikusaOwner, l);
                    var shugenja = Kw(Put(s, "training-shugenja", actor, l), "Air");
                    if (l.Battle)
                    {
                        spell = Add(s, "training-spell-with-ability", actor, Zone.Attached, province: l.Prov, parent: shugenja.Id); if (air) Kw(spell, "Air");
                        target = Put(s, "training-crane-samurai", actor, l); spellAbility = "training-spell-force-boost";
                        Put(s, "training-lion-samurai", 1 - actor, l);
                    }
                    else
                    {
                        spell = Add(s, scent, actor, Zone.Attached, parent: shugenja.Id);
                        target = Put(s, "training-lion-samurai", 1 - actor, l); target.Dishonorable = true; spellAbility = "scent-of-dishonor-bow";
                    }
                    return s;
                }
                var e = MatchEngine.FromTestState(Build());
                var honor = Honor(e, akikusaOwner);
                Accept(e, Offer(e, actor, spellAbility, spell.Id, target.Id));
                if (actor != akikusaOwner)
                {
                    ToAfter(e);
                    Check(!Offered(e, akikusaOwner, ability), $"{l.Name}: the opponent's Air Spell action is not an action on your Spell");
                    Check(Window(e) != "afterActionResolvedReaction" || e.State.PriorityPlayerId != akikusaOwner || !Offered(e, akikusaOwner, ability), "and no window for Akikusa");
                    continue;
                }
                Eq(Window(e), "afterActionResolvedReaction", l.Name + ": the after-action window opens for her");
                var command = InWindow(e, 0, ability, Put0(e, def));
                Accept(e, command);
                Eq(Honor(e, 0), honor + 1, l.Name + ": +1 Honor");
                Check(!Offered(e, 0, ability, command.CardId), "taken for this action");
                if (!l.Battle) continue;
                // A Spell without Air: nothing to react to.
                var n = MatchEngine.FromTestState(Build(false));
                Accept(n, Offer(n, actor, spellAbility, spell.Id, target.Id));
                Check(Window(n) != "afterActionResolvedReaction" && !Offered(n, 0, ability), "not an Air Spell");
            }
    }
    private static string Put0(MatchEngine e, string def) => e.State.Cards.Values.First(c => c.DefinitionId == def && c.OwnerId == 0).Id;

    private static void ByungAdditional()
    {
        const string ability = "byung-additional-action";
        var def = Def(ability);
        var junpeiDef = Def("junpei-attack");
        foreach (var l in Lays.Where(x => x.Battle))
        {
            MatchState Build(out CardInstance byung, out CardInstance junpei, out CardInstance enemy, out CardInstance extra, bool byungPerforms = true)
            {
                var s = SetupFor(l, 0);
                byung = Put(s, def, 0, l); junpei = Add(s, junpeiDef, 0, Zone.Home); enemy = Put(s, "training-lion-samurai", 1, l); extra = Put(s, "training-battle-bower", 0, l);
                return s;
            }
            var s0 = Build(out var byung, out var junpei, out var enemy, out var bower);
            var e = MatchEngine.FromTestState(s0);
            Accept(e, Offer(e, 0, "junpei-attack", junpei.Id, enemy.Id, byung.Id));
            ToAfter(e);
            Eq(Window(e), "afterActionResolvedReaction", l.Name + ": the after-action window opens for Byung");
            var command = InWindow(e, 0, ability, byung.Id);
            Accept(e, command);
            Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");
            Settle(e);
            Eq(e.State.PriorityPlayerId, 0, "the grant keeps priority");
            if (e.State.Cards[enemy.Id].Zone == Zone.Battlefield) Accept(e, Offer(e, 0, "training-bow-at-battle", bower.Id, enemy.Id));
            // Control: declining hands priority to the opponent.
            var c = MatchEngine.FromTestState(Build(out var byung2, out var junpei2, out var enemy2, out _));
            Accept(c, Offer(c, 0, "junpei-attack", junpei2.Id, enemy2.Id, byung2.Id));
            ToAfter(c); Settle(c);
            Check(c.State.AdditionalActions.All(g => g.PlayerId != 0), "control: no additional action when declined");
            // Not for the same Tactical action performed by another Personality.
            var s2 = Build(out _, out var junpei3, out var enemy3, out _);
            var other = Put(s2, "training-crane-samurai", 0, l);
            var o = MatchEngine.FromTestState(s2);
            Accept(o, Offer(o, 0, "junpei-attack", junpei3.Id, enemy3.Id, other.Id));
            ToAfter(o);
            Check(!Offered(o, 0, ability), "another performer's Tactical action does not trigger Byung");
        }
    }

    private static void PillarsAdditional()
    {
        const string ability = "pillars-virtue-additional-action";
        var def = Def(ability);
        var kihoDef = "oracle-l5r-7477"; // Steadfast Defense, keyword Kiho
        foreach (var l in Lays.Where(x => x.Battle))
            foreach (var actor in new[] { 0, 1 })
            {
                var s = SetupFor(l, actor);
                s.Cards[s.Players[0].StrongholdId].DefinitionId = def;
                var monk = Put(s, "training-monk", actor, l); Put(s, "training-lion-samurai", 1 - actor, l);
                var kiho = Add(s, kihoDef, actor, Zone.Hand);
                var bower = Put(s, "training-battle-bower", actor, l); var foe = Put(s, "training-crane-samurai", 1 - actor, l);
                var e = MatchEngine.FromTestState(s);
                var stronghold = s.Players[0].StrongholdId;
                Accept(e, Use(e, actor, "steadfast-defense-straighten", kiho.Id, performerId: monk.Id));
                ToAfter(e);
                if (actor == 1)
                {
                    Check(!Offered(e, 0, ability, stronghold), l.Name + ": another player's Kiho action is not one I took");
                    continue;
                }
                Eq(Window(e), "afterActionResolvedReaction", l.Name + ": the after-action window opens for Pillars");
                var command = InWindow(e, 0, ability, stronghold);
                Accept(e, command);
                Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");
                Settle(e);
                Eq(e.State.PriorityPlayerId, 0, "the grant keeps priority");
                Accept(e, Offer(e, 0, "training-bow-at-battle", bower.Id, foe.Id));
                // A Battle action that is not Kiho does not trigger it.
                var t = SetupFor(l, 0); t.Cards[t.Players[0].StrongholdId].DefinitionId = def;
                var b2 = Put(t, "training-battle-bower", 0, l); var f2 = Put(t, "training-lion-samurai", 1, l);
                var te = MatchEngine.FromTestState(t);
                Accept(te, Offer(te, 0, "training-bow-at-battle", b2.Id, f2.Id)); ToAfter(te);
                Check(!Offered(te, 0, ability, t.Players[0].StrongholdId), "a non-Kiho Battle action does not trigger it");
            }
    }

    private static void ItokuRing()
    {
        const string ability = "itoku-exp-ring-straighten";
        var def = Def(ability);
        const string ringOfAir = "oracle-l5r-6333", ringOfVoid = "oracle-l5r-6338";
        foreach (var l in Lays)
            foreach (var actor in new[] { 0, 1 })
            {
                var s = SetupFor(l, actor);
                var itoku = Put(s, def, 0, l);
                var ring = Add(s, ringOfAir, actor, Zone.Home);
                var bowedMan = Put(s, "training-crane-samurai", actor, l, bowed: true); var bowedMan2 = Put(s, "training-crane-samurai", actor, l, bowed: true);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, actor, "ring-of-air-straighten", ring.Id, bowedMan.Id));
                Check(e.State.Cards[ring.Id].Bowed, "the Ring bowed as its cost");
                ToAfter(e);
                if (actor == 1)
                {
                    Check(!Offered(e, 0, ability) && !Offered(e, 1, ability), l.Name + ": the opponent's Ring is not your Ring");
                    continue;
                }
                Eq(Window(e), "afterActionResolvedReaction", l.Name + ": the after-action window opens");
                var command = InWindow(e, 0, ability, itoku.Id, targetId: ring.Id);
                Accept(e, command);
                Check(!e.State.Cards[ring.Id].Bowed, "straightened");
                Settle(e);
                Check(!e.State.Cards[bowedMan.Id].Bowed && e.State.Cards[bowedMan2.Id].Bowed, "only the first use has happened");
                if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
                Accept(e, Offer(e, 0, "ring-of-air-straighten", ring.Id, bowedMan2.Id));
                Check(!e.State.Cards[bowedMan2.Id].Bowed, l.Name + ": the Ring's ability was usable an additional time this turn");
                // Control: without Itoku's reaction the Ring cannot be used a second time.
                var ctl = SetupFor(l, 0); var ring2 = Add(ctl, ringOfAir, 0, Zone.Home); var m1 = Put(ctl, "training-crane-samurai", 0, l, bowed: true); var m2 = Put(ctl, "training-crane-samurai", 0, l, bowed: true);
                var ce = MatchEngine.FromTestState(ctl);
                Accept(ce, Offer(ce, 0, "ring-of-air-straighten", ring2.Id, m1.Id)); Settle(ce);
                Check(!CanUse(ce, 0, "ring-of-air-straighten", ring2.Id), "control: a bowed Ring is spent for the turn");
                Check(m2.Id.Length > 0);
                // A Ring bowed by an unrelated cost opens nothing: the opponent's Personality's bow does not bow the Ring.
                var t = SetupFor(l, 0); Put(t, def, 0, l); Add(t, ringOfAir, 0, Zone.Home); var man = Put(t, "training-crane-samurai", 0, l, bowed: true);
                var (bw, bwAb) = Bower(t, l, 1); t.PriorityPlayerId = 1;
                var te = MatchEngine.FromTestState(t);
                Accept(te, Offer(te, 1, bwAb, bw.Id, man.Id)); ToAfter(te);
                Check(!Offered(te, 0, ability), "an action that bowed no Ring of yours");
            }
        // A Void Ring is excluded (Action phase, the Ring of the Void's Limited draw).
        var v = Setup(Lays[2]); v.ActivePlayerId = 0; v.PriorityPlayerId = 0; Put(v, def, 0, Lays[2]);
        var voidRing = Add(v, ringOfVoid, 0, Zone.Home); Add(v, "training-focus-strategy", 0, Zone.Hand);
        var ve = MatchEngine.FromTestState(v);
        Accept(ve, Enabled(ve, 0, "ring-of-void-draw", voidRing.Id).Command!);
        ToAfter(ve);
        Check(!Offered(ve, 0, ability), "no Itoku Reaction for a Void Ring");
    }

    private static void CastTwoShadows()
    {
        const string ability = "cast-two-shadows-move";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
            foreach (var ninjaUnit in new[] { true, false })
            {
                var s = SetupFor(l, 1);
                s.UnresolvedBattlefields = [0, 1];
                var convict = Add(s, "oracle-l5r-9897", 1, Zone.Hand);
                var shadows = Add(s, def, 0, Zone.Hand);
                var magistrate = Kw(Put(s, "training-lion-samurai", 1, l), "Magistrate");
                var moved = Put(s, "training-crane-samurai", 0, l); if (ninjaUnit) Kw(moved, "Ninja");
                Put(s, "training-lion-samurai", 0, l); // presence (issue #1133): the Strategy's player keeps a unit at the battlefield after the Ninja was moved home
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, 1, "captured-convict-move", convict.Id, moved.Id, magistrate.Id));
                Eq(e.State.Cards[moved.Id].Zone, Zone.Home, "the Personality was moved home from battlefield 0");
                for (var guard = 0; guard < 6 && Window(e) is "effectChoice" or "beforeActionReaction"; guard++)
                    Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Command is not null && a.Type is "chooseEffect" or "declineReaction").Command! with { });
                ToAfter(e);
                if (!ninjaUnit) { Check(!Offered(e, 0, ability, shadows.Id), l.Name + ": a non-Ninja unit is not covered"); continue; }
                Eq(Window(e), "afterActionResolvedReaction", l.Name + ": the after-action window is open");
                var offers = Listed(e, 0, ability).Where(o => o.Enabled).ToArray();
                Check(offers.Length > 0 && offers.All(o => o.Command!.Province == 1), "the move names a different battlefield only: " + string.Join(",", offers.Select(o => o.Command!.Province)));
                var command = InWindow(e, 0, ability, shadows.Id) with { TargetId = moved.Id, Province = 1 };
                Reject(e, command with { Province = 0 });
                Accept(e, command);
                Check(e.State.Cards[moved.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 1 }, l.Name + ": he cast his second shadow at battlefield 1");
                Eq(e.State.Cards[shadows.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            }
    }

    private static void DeepSnowsEnter()
    {
        const string ability = "deep-snows-enter";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
            foreach (var actor in new[] { 0, 1 })
            {
                var s = SetupFor(l, actor);
                var snows = Add(s, def, 0, Zone.Hand);
                var (bower, bow) = Bower(s, l, actor); var victim = Put(s, "training-lion-samurai", 1 - actor, l);
                var e = MatchEngine.FromTestState(s);
                Check(!e.QueryActions(0).Any(a => a.Type == "playStrategy" && a.CardId == snows.Id), "not played as an ordinary Terrain");
                Accept(e, Offer(e, actor, bow, bower.Id, victim.Id));
                ToAfter(e);
                Eq(Window(e), "afterActionResolvedReaction", $"{l.Name} actor {actor}: a Battle action resolved");
                var command = InWindow(e, 0, ability, snows.Id);
                Accept(e, command);
                Check(e.State.Cards[snows.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "Deep Snows is in play at the current battlefield");
                // A Terrain already there: not offered.
                var t = SetupFor(l, actor); var terrain = Add(t, "oracle-l5r-1921", 1, Zone.Battlefield, province: 0); terrain.FaceUp = true;
                Add(t, def, 0, Zone.Hand); var (b2, ab2) = Bower(t, l, actor); var v2 = Put(t, "training-lion-samurai", 1 - actor, l);
                var te = MatchEngine.FromTestState(t);
                Accept(te, Offer(te, actor, ab2, b2.Id, v2.Id)); ToAfter(te);
                Check(!Offered(te, 0, ability), "a Terrain stands at the battlefield: not offered");
            }
        // After an Open action (Action phase) it is not offered.
        var a = SetupFor(Lays[2], 1); Add(a, def, 0, Zone.Hand); var (ob, oab) = Bower(a, Lays[2]); var ov = Put(a, "training-crane-samurai", 0, Lays[2]);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 1, oab, ob.Id, ov.Id)); ToAfter(ae);
        Check(!Offered(ae, 0, ability), "not after a non-Battle action");
    }

    // ---- the rest: battle, duel, payment and proclaim windows --------------------------------------------------------------------------------------------------------------------
    private static void ShihiroBoost()
    {
        const string ability = "shihiro-boost";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
        {
            var s = Setup(l);
            var shihiro = Put(s, def, 0, l); var mine = Put(s, "training-crane-samurai", 0, l);
            var (b1, a1) = Bower(s, l); var (b2, a2) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            var printed = Force(e, shihiro.Id);
            for (var use = 1; use <= 2; use++)
            {
                var (bw, ab) = use == 1 ? (b1, a1) : (b2, a2);
                Accept(e, Offer(e, 1, ab, bw.Id, mine.Id));
                Eq(Window(e), "beforeActionReaction", $"{l.Name} action {use}: the window opens before the action resolves");
                Check(!e.State.Cards[mine.Id].Bowed || use == 2, "the action is still held");
                Accept(e, InWindow(e, 0, ability, shihiro.Id));
                Eq(Force(e, shihiro.Id), printed + use, $"each time: +1F ({use})");
                Settle(e);
                if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
                mine.Bowed = false; e.State.Cards[mine.Id].Bowed = false;
            }
            Check(e.State.Cards[shihiro.Id].Modifiers.Count(m => m.Stat == "Force" && m.Amount == 1 && !m.Permanent) == 2, "two temporary +1F modifiers (the turn-end sweep clears them)");
            // His own controller's action, an action outside a battle and a battle at another battlefield: no window.
            var t = Setup(l); Put(t, def, 0, l); var foe = Put(t, "training-lion-samurai", 1, l); var (ob, oab) = Bower(t, l, 0); t.PriorityPlayerId = 0;
            var te = MatchEngine.FromTestState(t);
            Accept(te, Offer(te, 0, oab, ob.Id, foe.Id)); NoWindow(te, 0, ability, "his own controller's action");
            var u = Setup(l); u.UnresolvedBattlefields = [0, 1]; Add(u, def, 0, Zone.Battlefield, province: 1); var m3 = Put(u, "training-crane-samurai", 0, l); var (b3, a3) = Bower(u, l);
            var ue = MatchEngine.FromTestState(u);
            Accept(ue, Offer(ue, 1, a3, b3.Id, m3.Id)); NoWindow(ue, 0, ability, "the battle is at another battlefield");
        }
        var a = Setup(Lays[2]); Put(a, def, 0, Lays[2]); var tgt = Put(a, "training-crane-samurai", 0, Lays[2]); var (ab4, aab) = Bower(a, Lays[2]);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 1, aab, ab4.Id, tgt.Id)); NoWindow(ae, 0, ability, "no battle in the Action phase");
    }

    private static void FallowLandsReduce()
    {
        const string ability = "fallow-lands-reaction";
        var def = Def(ability);
        // Player 1 attacks the Defender's (player 0's) province 0, where the Region lies; two Attacks: a Melee 2 and a Ranged 3.
        (MatchState S, CardInstance Fallow, CardInstance Follower, string Weapon, string AttackAbility) Build(bool ranged, int regionOwner = 0, int regionProvince = 0, int attacker = 1)
        {
            var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 1; s.UnresolvedBattlefields = [0, 1];
            var fallow = Add(s, def, regionOwner, Zone.Region, province: regionProvince); s.Players[regionOwner].Provinces[regionProvince].RegionId = fallow.Id;
            var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
            var bare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3: Ranged 3 destroys him, Ranged 1 does not
            var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (!ranged) { var weapon = Add(s, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id); return (s, fallow, follower, weapon.Id, "training-weapon-attack"); }
            var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
            return (s, fallow, bare, strategy.Id, "training-honor-cost-attack");
        }
        foreach (var ranged in new[] { false, true })
        {
            var (s, fallow, follower, source, attack) = Build(ranged);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, attack, source, follower.Id));
            Eq(Window(e), "attackReaction", "the targeted Attack waits for the Defender's reaction");
            var strength = e.State.PendingAttack!.Strength;
            Eq(strength, ranged ? 3 : 2, ranged ? "Ranged 3" : "Melee 2");
            var command = InWindow(e, 0, ability, fallow.Id);
            Accept(e, command);
            Settle(e);
            Eq(e.State.Cards[follower.Id].Zone, ranged ? Zone.Battlefield : Zone.Attached, ranged ? "Ranged 3 -2 = 1 cannot destroy a Force-3 Personality" : "Melee 2 -2 = 0 leaves a Force-1 Follower alone");
            // Control: no reaction, a Force-1 Follower dies to Melee 2.
            var (cs, _, cf, csrc, cab) = Build(ranged);
            var ce = MatchEngine.FromTestState(cs);
            Accept(ce, Offer(ce, 1, cab, csrc, cf.Id)); Settle(ce);
            Check(ce.State.Cards[cf.Id].Zone is not (Zone.Attached or Zone.Battlefield), ranged ? "control: Ranged 3 destroys the Force-3 Personality" : "control: Melee 2 destroys the Force-1 Follower");
            // The Region on a different province, or its controller the Attacker, is not under attack here.
            var (os, ofl, of, osrc, oab) = Build(ranged, 0, 1);
            var oe = MatchEngine.FromTestState(os);
            Accept(oe, Offer(oe, 1, oab, osrc, of.Id));
            Check(!Offered(oe, 0, ability, ofl.Id), "a Region on another province: not offered");
            var (xs, xfl, xf, xsrc, xab) = Build(ranged, 1, 0, 1);
            var xe = MatchEngine.FromTestState(xs);
            Accept(xe, Offer(xe, 1, xab, xsrc, xf.Id));
            Check(!Offered(xe, 1, ability, xfl.Id) && !Offered(xe, 0, ability, xfl.Id), "the Attacker's own Region does not react to his Attack");
        }
    }

    private static void FinishDuel(MatchEngine e)
    {
        for (var guard = 0; guard < 16 && e.State.PendingDuel is not null; guard++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var focus = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type == "focus" && a.Command.Choice == "deck" || a.Type == "strike"));
            Check(focus is not null, $"no focus by deck offered to player {mover} ({Window(e)})");
            Accept(e, focus!.Command!);
        }
        Check(e.State.PendingDuel is null, "the duel did not finish");
    }

    private static void HesMineSubstitute()
    {
        const string ability = "hes-mine-substitute";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
        {
            (MatchEngine E, CardInstance Card, CardInstance Victim, CardInstance Samurai, CardInstance Duelist) Build(bool bowed = false, bool elsewhere = false)
            {
                var s = Setup(l);
                var victim = Put(s, "training-crane-veteran", 0, l);
                var samurai = Kw(Add(s, "training-crane-samurai", 0, elsewhere ? Zone.Home : Zone.Battlefield, bowed, elsewhere ? null : 0), "Samurai");
                var duelist = Put(s, "training-lion-samurai", 1, l); var challenge = Add(s, "training-duel-force", 1, Zone.Hand);
                var card = Add(s, def, 0, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Use(e, 1, "training-duel-force", challenge.Id, victim.Id, performerId: duelist.Id));
                return (e, card, victim, samurai, duelist);
            }
            var (e, card, victim, samurai, duelist) = Build();
            Eq(Window(e), "duelReaction", l.Name + ": a mandatory duel opens the before-the-duel window at once");
            Eq(e.State.PendingDuel?.ChallengedId, victim.Id, "the Personality is the challenged one until replaced");
            var command = InWindow(e, 0, ability, card.Id, performerId: samurai.Id);
            Accept(e, command);
            Check(e.State.Cards[victim.Id].Bowed, "the Personality was bowed as the cost");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "He's Mine! was spent");
            Check(e.State.PendingDuel is { } duel && duel.ChallengedId == samurai.Id && duel.ChallengerId == duelist.Id, "the Samurai fights the duel instead");
            Check(!e.State.Cards[samurai.Id].Bowed, "he is not bowed while the duel is under way");
            FinishDuel(e);
            Check(e.State.Cards[samurai.Id].Bowed, "he bows after the duel ends");
            var (b, bcard, _, bsam, _) = Build(bowed: true);
            Check(!CanUse(b, 0, ability, bcard.Id, bsam.Id), "a bowed Samurai may not take his place");
            var (h, hcard, _, hsam, _) = Build(elsewhere: true);
            Check(!CanUse(h, 0, ability, hcard.Id, hsam.Id), "a Samurai at another location may not");
            var n = Setup(l); Put(n, "training-crane-veteran", 0, l); Kw(Put(n, "training-crane-samurai", 0, l), "Samurai"); var held = Add(n, def, 0, Zone.Hand);
            var ne = MatchEngine.FromTestState(n);
            Check(!CanUse(ne, 0, ability, held.Id), "no duel is pending: nothing to react to");
        }
        // The reactor's own duel: the challenger's controller replaces his own participant once the challenged player has passed.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var mine = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var sam = Kw(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai");
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var ch = Add(s2, "training-duel-force", 0, Zone.Hand); var hm = Add(s2, def, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "training-duel-force", ch.Id, enemy.Id, performerId: mine.Id));
        Eq(Window(e2), "duelReaction", "the mandatory duel opens the window");
        var c2 = InWindow(e2, 0, ability, hm.Id, performerId: sam.Id);
        Accept(e2, c2);
        Check(e2.State.PendingDuel is { } d2 && d2.ChallengerId == sam.Id && d2.ChallengedId == enemy.Id && e2.State.Cards[mine.Id].Bowed, "the Samurai now challenges, the replaced participant bowed");
    }

    private static void YatakuNoRefuse()
    {
        const string ability = "yataku-no-refuse";
        var yataku = Def(ability);
        var shigeyuki = "oracle-l5r-2152";
        foreach (var attacks in new[] { true, false })
            foreach (var yatakuOwner in new[] { 0, 1 })
            {
                MatchEngine Challenge(string targetDef, Action<MatchState>? edit = null)
                {
                    var s = Table(Phase.Battle, attacks ? 0 : 1); s.PriorityPlayerId = 0;
                    var shig = Add(s, shigeyuki, 0, Zone.Battlefield, province: 0);
                    var foe = Add(s, targetDef, 1, Zone.Battlefield, province: 0);
                    foe.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get(targetDef).PersonalHonor));
                    Add(s, yataku, yatakuOwner, Zone.Battlefield, province: 0); // presence (issue #1133): a Personality's Reaction in a battle needs him at the battlefield
                    edit?.Invoke(s);
                    var e = MatchEngine.FromTestState(s);
                    Accept(e, Offer(e, 0, "shigeyuki-duel", shig.Id, foe.Id));
                    return e;
                }
                const string samurai = "oracle-l5r-6620";
                var plain = MatchEngine.FromTestState(Batch336Support.Edit(Challenge("training-lion-samurai"), s => { }).State);
                var e = Challenge(samurai, s => { });
                var y = e.State.Cards.Values.Single(c => c.DefinitionId == yataku);
                Eq(Window(e), "beforeActionReaction", $"attacker {attacks} yataku of {yatakuOwner}: the before-action window opens");
                var command = InWindow(e, yatakuOwner, ability, y.Id);
                Accept(e, command);
                Check(e.State.Cards[y.Id].Bowed, "Yataku bowed as his cost");
                Settle(e);
                Check(e.State.PendingDuel is { Refusable: false } && e.State.PendingDuel.Stage != "AwaitingAccept", "the challenge may not be refused: the duel goes straight on");
                Check(!e.QueryActions(1).Any(a => a.Type == "declineDuel" && a.Enabled), "the Samurai's controller has no refuse action");
                // Control: unused, the challenge is refusable and waits for the answer.
                var declined = Challenge(samurai); Settle(declined);
                Check(declined.State.PendingDuel is { Refusable: true, Stage: "AwaitingAccept" }, "an unused Yataku changes nothing");
                // A target that is no Samurai: no window. A bowed Yataku cannot react.
                Check(plain.State.PendingChoice?.Type != "beforeActionReaction" || !Offered(plain, yatakuOwner, ability), "no Samurai (control state)");
                var notSamurai = Challenge("training-lion-samurai");
                Check(Window(notSamurai) != "beforeActionReaction" || !Offered(notSamurai, yatakuOwner, ability), "no Samurai targeted: nothing to react to");
                var bowed = Challenge(samurai, s => s.Cards.Values.Single(c => c.DefinitionId == yataku).Bowed = true);
                Check(!Offered(bowed, yatakuOwner, ability), "a bowed Yataku cannot react");
            }
    }

    private static void RedCraneDojo()
    {
        const string ability = "red-crane-dojo-ninja";
        var def = Def(ability);
        foreach (var l in Lays)
            foreach (var keyword in new[] { "Ninja", "Shadowlands" })
                foreach (var dojoBowed in new[] { false, true })
                {
                    var s = Setup(l);
                    var dojo = Add(s, def, 0, Zone.Home, dojoBowed);
                    var target = keyword == "Ninja" ? Kw(Put(s, "training-crane-samurai", 0, l), keyword) : Put(s, "training-shadowlands-personality", 0, l); // Shadowlands is a Clan alignment
                    var (bw, ab) = Bower(s, l);
                    var e = MatchEngine.FromTestState(s);
                    Accept(e, Offer(e, 1, ab, bw.Id, target.Id));
                    Eq(Window(e), "beforeActionReaction", $"{l.Name} {keyword} bowed={dojoBowed}: the window opens (Tireless: even with the Holding bowed)");
                    var command = InWindow(e, 0, ability, dojo.Id);
                    Check(keyword == "Ninja" ? e.HasKeyword(target.Id, keyword) : e.HasClanAlignmentForTest(target.Id, keyword), "before: he has " + keyword);
                    Accept(e, command);
                    Check(e.State.Cards[dojo.Id].Bowed, "the Holding is bowed");
                    Check(!e.HasKeyword(target.Id, "Ninja") && !e.HasKeyword(target.Id, "Shadowlands") && !e.HasClanAlignmentForTest(target.Id, "Shadowlands"), "Ninja and Shadowlands are removed from him until the action ends");
                    Settle(e);
                    if (keyword == "Ninja") Check(e.HasKeyword(target.Id, keyword), "and he has Ninja again once the action has ended");
                    else Check(e.State.Cards[target.Id].RemovedClanAffiliations.Count == 0 && e.HasClanAlignmentForTest(target.Id, "Shadowlands"), "and his Shadowlands alignment is his again");
                    Check(e.State.TemporaryRemovals.Count == 0 && e.State.Cards[target.Id].RemovedKeywords.Count == 0, "nothing is left removed");
                }
        foreach (var l in Lays)
        {
            // A Personality who is neither Ninja nor Shadowlands, and a stranger's Personality: no window for the Dojo.
            var s = Setup(l); Add(s, def, 0, Zone.Home); var plain = Put(s, "training-crane-samurai", 0, l); var (bw, ab) = Bower(s, l);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, ab, bw.Id, plain.Id)); NoWindow(e, 0, ability, "a Personality who is neither Ninja nor Shadowlands");
            var t = Setup(l); Add(t, def, 1, Zone.Home); var ninja = Kw(Put(t, "training-crane-samurai", 0, l), "Ninja"); var (b2, ab2) = Bower(t, l);
            var te = MatchEngine.FromTestState(t);
            Accept(te, Offer(te, 1, ab2, b2.Id, ninja.Id)); Check(!Offered(te, 0, ability) && !Offered(te, 1, ability), "the Dojo protects only its own controller's Personality");
        }
    }

    private static void RyotaSubstitute()
    {
        const string ability = "w740-ryota-substitute";
        var def = Def(ability);
        foreach (var l in Lays.Where(x => x.Battle))
            foreach (var refusable in new[] { false, true })
            {
                var duelAbility = refusable ? "training-duel-challenge" : "training-duel-force";
                MatchEngine Build(bool ryotaHome, out CardInstance ryota, out CardInstance challenged, out CardInstance challengeCard, out CardInstance challenger)
                {
                    var s = Setup(l);
                    challenged = Put(s, "training-lion-samurai", 0, l);
                    ryota = ryotaHome ? Add(s, def, 0, Zone.Home) : Put(s, def, 0, l);
                    challenger = Put(s, "training-crane-samurai", 1, l); challengeCard = Add(s, duelAbility, 1, Zone.Hand);
                    var e = MatchEngine.FromTestState(s);
                    Accept(e, Use(e, 1, duelAbility, challengeCard.Id, challenged.Id, performerId: challenger.Id));
                    return e;
                }
                var e = Build(false, out var ryota, out var target, out _, out var challenger);
                Eq(Window(e), "beforeActionReaction", $"{l.Name} refusable={refusable}: the held challenge opens a reaction window");
                var command = InWindow(e, 0, ability, ryota.Id);
                Accept(e, command);
                Settle(e);
                Check(e.State.PendingDuel is { } duel && duel.ChallengedId == ryota.Id && duel.ChallengerId == challenger.Id, "Ryota enters the duel instead of the target");
                Check(e.State.PendingDuel!.ChallengedId != target.Id, "the target does not");
                // At home he does not stand where the challenged Personality does.
                var f = Build(true, out var home, out var d2, out _, out _);
                Check(Window(f) != "beforeActionReaction" || !Offered(f, 0, ability, home.Id), "no window for Ryota elsewhere");
                Eq(f.State.PendingDuel?.ChallengedId, d2.Id, "the challenge keeps its target");
            }
    }

    private static void JadeMantleGold()
    {
        const string ability = "pe-jade-gold-for-spell";
        var def = Def(ability);
        const string spellWithCost4 = "oracle-l5r-10700";
        MatchEngine Rig(bool mantleBowed, out CardInstance mantle, out CardInstance spell, out CardInstance shugenja, string spellDef = "training-spell", int owner = 0)
        {
            var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner;
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            var champion = Kw(Add(s, "training-crane-samurai", owner, Zone.Home), "Jade Champion");
            mantle = Add(s, def.Length > 0 ? Def(ability) : def, owner, Zone.Attached, mantleBowed, null, champion.Id);
            shugenja = Add(s, "training-shugenja", owner, Zone.Home); spell = Add(s, spellDef, owner, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        foreach (var bowed in new[] { true, false })
        {
            var e = Rig(bowed, out var mantle, out var spell, out var shugenja);
            var paying = new PaymentSubject(spell, PaymentKind.Equip, Wielder: shugenja);
            var options = e.PaymentOptions(0, 1, paying);
            Check(options.Any(p => p.SourceIds.SequenceEqual([mantle.Id + "#reaction"])), $"bowed={bowed}: the Mantle pays for a Spell with its Reaction");
            Eq(e.PaymentOptions(0, 1, new PaymentSubject(Add(e.State, "training-follower", 0, Zone.Hand), PaymentKind.Equip)).Length, 0, "but for nothing else");
            Accept(e, new("equip", 0, e.Version, CardId: spell.Id, SourceIds: options.First(p => p.SourceIds.Contains(mantle.Id + "#reaction")).SourceIds, TargetId: shugenja.Id));
            Eq(e.State.Cards[spell.Id].AttachedTo, shugenja.Id, "the Spell was attached, paid with the Mantle's Gold");
            Eq(e.State.Cards[mantle.Id].Bowed, bowed, "the Mantle's Reaction does not bow it (and works while bowed)");
            Check(e.State.Events.Any(ev => ev.Type == "goldSourceUsed" && ev.CardId == mantle.Id), "the use is logged");
            var again = MatchEngine.FromTestState(Batch336Support.Edit(e, st => Add(st, "training-spell", 0, Zone.Hand)).State);
            var second = again.State.Cards.Values.First(c => c.Zone == Zone.Hand && c.OwnerId == 0 && c.DefinitionId == "training-spell");
            Eq(again.PaymentOptions(0, 1, new PaymentSubject(second, PaymentKind.Equip, Wielder: shugenja)).Length, 0, "the Reaction is spent for the turn");
        }
        // Exactly 4 Gold: a 4-Gold Spell is paid by the Mantle alone; a 5-Gold amount is not.
        var f = Rig(true, out var m4, out var s4, out var sh4, spellWithCost4);
        var pay4 = f.PaymentOptions(0, 4, new PaymentSubject(s4, PaymentKind.Equip, Wielder: sh4));
        Check(pay4.Any(p => p.SourceIds.SequenceEqual([m4.Id + "#reaction"])), "four Gold: the Mantle alone pays");
        Eq(f.PaymentOptions(0, 5, new PaymentSubject(s4, PaymentKind.Equip, Wielder: sh4)).Length, 0, "five Gold: the Mantle alone cannot, every other source is bowed");
        // The opponent cannot use the Mantle for his own Spell.
        CardInstance foeSpell = null!, foeShugenja = null!;
        var g = Batch336Support.Edit(Rig(true, out var m5, out _, out _), st => { foeSpell = Add(st, "training-spell", 1, Zone.Hand); foeShugenja = Add(st, "training-shugenja", 1, Zone.Home); });
        Check(!g.PaymentOptions(1, 1, new PaymentSubject(g.State.Cards[foeSpell.Id], PaymentKind.Equip, Wielder: g.State.Cards[foeShugenja.Id])).Any(p => p.SourceIds.Any(i => i.StartsWith(m5.Id))), "the opponent cannot spend the other player's Mantle");
        // Not in the Battle phase (no Equip there): the equip is refused.
        var b = Table(Phase.Battle, 1); b.PriorityPlayerId = 1;
        foreach (var card in b.Cards.Values.Where(c => c.OwnerId == 1 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var champ = Kw(Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0), "Jade Champion"); var bm = Add(b, def, 1, Zone.Attached, true, 0, champ.Id);
        var sp = Add(b, "training-spell", 1, Zone.Hand); var sh = Add(b, "training-shugenja", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Reject(be, new("equip", 1, be.Version, CardId: sp.Id, SourceIds: [bm.Id + "#reaction"], TargetId: sh.Id));
    }

    private static void DairukoRally()
    {
        const string ability = "dairuko-rally";
        const string def = "training-rallying-commander";
        MatchState Combat() { var s = Table(Phase.Battle, 0); foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; s.PriorityPlayerId = 1; s.UnresolvedBattlefields = [0, 1]; return s; }
        void Resolve(MatchEngine e) { Accept(e, new GameCommand("pass", 1, e.Version)); Accept(e, new GameCommand("pass", 0, e.Version)); }
        // Without the Reaction the winning attackers return home bowed.
        var cs = Combat(); Add(cs, def, 0, Zone.Home); var plainAlly = Add(cs, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(cs, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var plain = MatchEngine.FromTestState(cs);
        Resolve(plain); Settle(plain);
        Check(plain.State.Cards[plainAlly.Id].Zone == Zone.Home && plain.State.Cards[plainAlly.Id].Bowed, "control: the attackers bow after the resolution");
        foreach (var commanderBowed in new[] { false, true })
        {
            var s = Combat();
            var commander = Add(s, def, 0, Zone.Battlefield, commanderBowed, 0); var ally = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!Offered(e, 0, ability, commander.Id), "nothing before the battle resolves");
            Resolve(e);
            Eq(Window(e), "beforeBattleReaction", "the window opens before the resolution");
            var command = InWindow(e, 0, ability, commander.Id);
            Accept(e, command);
            Check(e.State.Cards[ally.Id].Zone == Zone.Home && !e.State.Cards[ally.Id].Bowed, $"bowed={commanderBowed}: the resolution did not bow the army");
            Eq(e.State.Cards[commander.Id].Bowed, commanderBowed, "nor the commander");
            Eq(e.State.ResolutionWillNotBowPlayerIds.Count, 0, "the promise lapsed with the battle");
            // Only that battle: the next one bows as usual.
            var st = e.State; foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home)) { c.Zone = Zone.Battlefield; c.ProvinceIndex = 1; c.Bowed = false; }
            var next = MatchEngine.FromTestState(st);
            Accept(next, next.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
            Settle(next); Resolve(next); Settle(next);
            Check(next.State.Cards[ally.Id].Bowed, "a later battle bows again");
        }
        // At home he is not at the battlefield: no window. A Defender's use does not stop the Attacker's army bowing.
        var h = Combat(); Add(h, def, 0, Zone.Home); Add(h, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(h, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var he = MatchEngine.FromTestState(h); Resolve(he);
        Check(Window(he) != "beforeBattleReaction", "at home: no window");
        var d = Combat(); var atk = Add(d, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var dc = Add(d, def, 1, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d); Resolve(de);
        var dcmd = InWindow(de, 1, ability, dc.Id);
        Accept(de, dcmd); Settle(de);
        Check(de.State.Cards[atk.Id].Bowed || de.State.Cards[atk.Id].Zone == Zone.Dead, "the Attacker's army is not the Defender's");
    }

    private static void TurningTheStory()
    {
        const string ability = "turning-the-story-negate";
        var def = Def(ability);
        foreach (var buyer in new[] { 0, 1 })
        {
            MatchEngine Proclaim(bool holds)
            {
                var s = Table(Phase.Dynasty, buyer); s.PriorityPlayerId = buyer;
                s.Players[buyer].Clan = "Crane"; s.Players[buyer].Honor = 30; Gold(s, buyer, 8);
                var entering = s.Cards[s.Players[buyer].Provinces[0].CardId!]; entering.DefinitionId = "training-crane-proclaim-loss"; entering.FaceUp = true;
                if (holds) Add(s, def, 1 - buyer, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                Accept(e, e.QueryActions(buyer).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false } && a.Quote is { ProclaimEligible: true }).Command!);
                Eq(Window(e), "proclaim", "the Proclaim window is open");
                return e;
            }
            var e = Proclaim(true);
            var honor = Honor(e, buyer);
            Accept(e, e.QueryActions(buyer).Single(a => a.Type == "proclaim").Command!);
            Eq(Window(e), "proclaimNegateReaction", "the other player is asked before the Proclaim resolves");
            Eq(e.State.PendingChoice?.PlayerId, 1 - buyer);
            var card = e.State.Cards.Values.Single(c => c.DefinitionId == def);
            var command = InWindow(e, 1 - buyer, ability, card.Id);
            Accept(e, command);
            Check(Window(e) is null, "the negated Proclaim left nothing to react to");
            Eq(Honor(e, buyer), honor - 2, "no Proclaim Honor (the entering loss still applies)");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Reaction was played and discarded");
            // Declining: the Proclaim resolves (+3 then the entering loss).
            var e2 = Proclaim(true); var h2 = Honor(e2, buyer);
            Accept(e2, e2.QueryActions(buyer).Single(a => a.Type == "proclaim").Command!); Settle(e2);
            Eq(Honor(e2, buyer), h2 + 3 - 2, "Proclaim +3, then the entering loss");
            // Nothing held: no window at all.
            var e3 = Proclaim(false);
            Accept(e3, e3.QueryActions(buyer).Single(a => a.Type == "proclaim").Command!);
            Check(Window(e3) is null, "no Reaction held: the Proclaim resolved at once");
            // Outside a Proclaim the card in hand is not offered.
            var s4 = Table(Phase.Action, buyer); s4.PriorityPlayerId = buyer; var held = Add(s4, def, 1 - buyer, Zone.Hand);
            var e4 = MatchEngine.FromTestState(s4);
            NeverOpened(e4, 1 - buyer, ability, held.Id, command with { CardId = held.Id });
        }
    }
}
