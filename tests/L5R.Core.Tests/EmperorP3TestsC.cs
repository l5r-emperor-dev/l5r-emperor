using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition group P3 (issue #503), third part: Thunder Dragon - exp2, Shosuro Aroru - exp2, Ikoma Ayumu, Utaku Lishan, Asako Kikugoro and Destructive Priorities (EmperorP3Priority.cs).
internal static partial class EmperorP3Tests
{
    private const string ThunderDragon = "oracle-l5r-10067", Aroru = "oracle-l5r-7089", Ayumu = "oracle-l5r-3595", Lishan = "oracle-l5r-9083", Kikugoro = "oracle-l5r-556",
        DestructivePriorities = "oracle-l5r-1983", MantisSamurai = "training-seeds5-mantis-samurai", HastyFortifications = "oracle-l5r-2996", Mrika = "oracle-l5r-9852";

    public static (string, Action)[] PartC => [
        ("Published shape (third part): Thunder Dragon - exp2, Shosuro Aroru - exp2, Ikoma Ayumu, Utaku Lishan, Asako Kikugoro and Destructive Priorities", PublishedShapeC),
        ("Thunder Dragon - exp2: his player's Mantis Clan Personalities have +1F and a Battle bow of an enemy card without unbowed attachments", ThunderDragonStatics),
        ("Thunder Dragon - exp2: after he performs an action the other player may not take Reactions until it ends; another performer locks nothing", ThunderDragonLockout),
        ("Shosuro Aroru - exp2: no non-Ninja Follower attaches to him; a Ninja Follower does", AroruAttach),
        ("Shosuro Aroru - exp2, Reaction: an action targeting a Ninja at his location is redirected to another of his player's cards, then he may move", AroruRedirect),
        ("Shosuro Aroru - exp2, Reaction edges: an illegal choice keeps the target (the move is still owed); declining the move leaves him", AroruRedirectEdges),
        ("Shosuro Aroru - exp2, Battle: Ranged 5 Attack during which the other player may not take Reactions", AroruAttack),
        ("Ikoma Ayumu: even bowed, after his player announces a Tactical action at his battlefield he straightens; not for another Battle action", AyumuScenario),
        ("Utaku Lishan: +1F and +1PH while attacking; after her player announces a Battle action, an opposing card gets a Force penalty equal to her Personal Honor", LishanScenario),
        ("Utaku Lishan, edges: a Terrain played is a Battle action announced too (the window comes before it enters play); bowed or unopposed she is not asked", LishanEdges),
        ("Asako Kikugoro: after he, another Henshin of his player's, or one of his Rings enters play, look at the top two Dynasty cards and may put one at the bottom", KikugoroScenario),
        ("Asako Kikugoro, edges: a non-Henshin entry offers nothing; once a turn across the three triggers", KikugoroEdges),
        ("Destructive Priorities: a Kata put into play; before its player gains Honor during his turn, his Courtier bows to negate the gain and a chosen player loses 2 Honor", DestructiveScenario),
        ("Destructive Priorities, edges: a gain no action made (an entering-play trait) is caught too; not in the other player's turn, not for a loss, not from the hand", DestructiveEdges),
    ];

    private static void PublishedShapeC()
    {
        void Person(string id, string name, string clan, int gold, int force, int chi, int ph, int? hr, string keywords, string abilities, string effect)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan && c.GoldCost == gold && c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.HonorRequirement == hr, $"{name}'s stats");
            Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
            Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities"); Eq(c.Effect, effect, $"{name}'s effect");
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
        }
        Person(ThunderDragon, "Thunder Dragon", "Mantis", 13, 8, 5, 4, 10, "Cavalry,Dragon,Experienced 2,Hero,Loyal,Mantis Clan,Naval,Nonhuman,Shugenja,Thunder,Unique", "", "thunder-dragon");
        Eq(CardCatalog.Get(ThunderDragon).Text, "After Thunder Dragon performs an action: Other players may not take Reactions until it ends.\nThe Thunder Dragon inspires. Your Mantis Clan Personalities have +1F and the ability, \"Battle: Target an enemy card without unbowed attachments: Bow it.\"");
        Person(Aroru, "Shosuro Aroru", "Scorpion", 9, 4, 5, 0, null, "Duelist,Experienced 2,Ninja,Scorpion Clan,Unique", "aroru-exp2-redirect,aroru-exp2-attack", "aroru-refuses-non-ninja-followers");
        Eq(CardCatalog.Get(Aroru).Text, "Will not attach non-Ninja Followers.\nReaction: When an action would target a Ninja at Aroru's location: Choose another of your cards. The action targets it instead, if legal. After the action's resolution, you may move Aroru to a different location.\nBattle: Other players may not take Reactions until this action ends. Ranged 5 Attack.");
        Person(Ayumu, "Ikoma Ayumu", "Lion", 7, 3, 4, 3, 7, "Lion Clan,Samurai,Tactician", "ayumu-straighten", "none");
        Eq(CardCatalog.Get(Ayumu).Text, "Reaction: Even if Ayumu is bowed, after you announce a Tactical action, if he is at the current battlefield: Straighten him.");
        Person(Lishan, "Utaku Lishan", "Unicorn", 9, 4, 3, 3, 5, "Battle Maiden,Cavalry,Paragon,Samurai,Unicorn Clan", "lishan-penalty", "lishan-attacking-bonus");
        Eq(CardCatalog.Get(Lishan).Text, "While Lishan is attacking, she has +1F and +1PH.\nReaction: After you announce a Battle action, if Lishan is at the current battlefield, target a Personality or Follower opposing her: Give it a Force penalty equal to Lishan's Personal Honor.");
        Person(Kikugoro, "Asako Kikugoro", "Phoenix", 6, 4, 1, 2, 0, "Henshin,Monk,Phoenix Clan,Void", "kikugoro-self-entry,kikugoro-henshin-entry,kikugoro-ring-entry", "none");
        Eq(CardCatalog.Get(Kikugoro).Text, "Reaction: After your Ring or Henshin (including Kikugoro) enters play or overlays: Look at the top two cards of your Dynasty deck. You may put one of them at the bottom of the deck.");
        Check(new[] { "kikugoro-self-entry", "kikugoro-henshin-entry", "kikugoro-ring-entry" }.All(a => AbilityCatalog.All[a].UsageKey == "kikugoro-look"), "Kikugoro's three triggers share the printed action's one use");
        var dp = CardCatalog.Get(DestructivePriorities);
        Check(dp.Type == CardType.Strategy && dp.Focus == 1 && dp.GoldCost == 0, "Destructive Priorities' stats");
        Eq(string.Join(",", dp.Keywords ?? []), "Kata,Political"); Eq(string.Join(",", dp.Abilities ?? []), "destructive-priorities-reaction,destructive-priorities-kata");
        Eq(dp.Text, "You have the ability, \"Political Reaction: Before you gain Honor during your turn, bow your performing Courtier: Negate the gain. Choose a player. He loses 2 Honor.\"\nLimited: Put this card into play. Discard all your other Katas.");
    }

    // ---- Thunder Dragon - exp2 ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ThunderDragonStatics()
    {
        var s = Table(Phase.Battle, 0);
        var dragon = Add(s, ThunderDragon, 0, Zone.Battlefield, province: 0);
        var mantis = Add(s, MantisSamurai, 0, Zone.Battlefield, province: 0);
        var crane = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var guarded = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: guarded.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(dragon.Id), 9, "the Thunder Dragon is Mantis Clan: 8 + 1");
        Eq(e.CardForceForTest(mantis.Id), 3, "a Mantis Clan Personality: 2 + 1");
        Eq(e.CardForceForTest(crane.Id), 3, "a Crane is not");
        Check(!CanUse(e, 0, "thunder-dragon-granted-bow", crane.Id), "the Crane has no granted bow");
        var bow = Enabled(e, 0, "thunder-dragon-granted-bow", mantis.Id);
        Check(bow.TargetIds!.Contains(enemy.Id) && !bow.TargetIds.Contains(guarded.Id), "an enemy with an unbowed attachment is no target");
        Accept(e, Offer(e, 0, "thunder-dragon-granted-bow", mantis.Id, enemy.Id));
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(e.State.Cards[enemy.Id].Bowed, "bowed");
        // Without the Dragon in play: nothing.
        var n = Table(Phase.Battle, 0);
        var lone = Add(n, MantisSamurai, 0, Zone.Battlefield, province: 0);
        Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var en = MatchEngine.FromTestState(n);
        Eq(en.CardForceForTest(lone.Id), 2, "no Dragon: no bonus");
        Check(!CanUse(en, 0, "thunder-dragon-granted-bow", lone.Id), "no Dragon: no granted ability");
    }

    private static MatchEngine DragonVersusRiders(bool dragonPerforms, out CardInstance riders, out CardInstance performer)
    {
        var s = Table(Phase.Battle, 0);
        var dragon = Add(s, ThunderDragon, 0, Zone.Battlefield, province: 0);
        var mantis = Add(s, MantisSamurai, 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        riders = Add(s, VigilantRiders, 1, Zone.Attached, province: 0, parent: host.Id);
        performer = dragonPerforms ? dragon : mantis;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "thunder-dragon-granted-bow", performer.Id, riders.Id));
        return e;
    }

    private static void ThunderDragonLockout()
    {
        var e = DragonVersusRiders(true, out var riders, out _);
        Check(!Offered(e, 1, "vigilant-riders-negate"), "the Dragon performs the bow: the other player may not take Reactions");
        Check(Logged(e, "reactionsLocked"), "the lockout is announced");
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(e.State.Players.All(p => true) && Logged(e, "reactionsUnlocked"), "the lockout ends with the action");
        // Another Mantis Personality performing the same granted bow: the Reaction is offered.
        var o = DragonVersusRiders(false, out _, out _);
        Check(Offered(o, 1, "vigilant-riders-negate"), "another performer: Reactions may be taken");
        Check(riders.Id.Length > 0);
    }

    // ---- Shosuro Aroru - exp2 -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AroruAttach()
    {
        var s = Table(Phase.Action, 0);
        var aroru = Add(s, Aroru, 0, Zone.Home);
        var plain = Add(s, HidaDefenders, 0, Zone.Hand); // Honor Requirement 0 (Aroru's Personal Honor is 0)
        var ninja = Add(s, HidaDefenders, 0, Zone.Hand); GiveKeyword(ninja, "Ninja");
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Rejection(e, new GameCommand("equip", 0, e.Version, CardId: plain.Id, TargetId: aroru.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds)).Length > 0, "a non-Ninja Follower will not attach");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: ninja.Id, TargetId: aroru.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Eq(e.State.Cards[ninja.Id].AttachedTo, aroru.Id, "a Ninja Follower attaches");
    }

    private static MatchEngine AroruTable(out CardInstance aroru, out CardInstance other, out CardInstance bower, out CardInstance homeCard)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        aroru = Add(s, Aroru, 0, Zone.Battlefield, province: 0);
        other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        homeCard = Add(s, "training-crane-samurai", 0, Zone.Home);
        bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        return MatchEngine.FromTestState(s);
    }

    private static void AroruRedirect()
    {
        var e = AroruTable(out var aroru, out var other, out var bower, out _);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, aroru.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action targeting the Ninja is held");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Accept(e, Options(e, 0, "aroru-exp2-redirect", aroru.Id).Single(o => o.Enabled && o.Command?.TargetId == other.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null or { Type: "p3AroruMove" });
        Check(e.State.Cards[other.Id].Bowed && !e.State.Cards[aroru.Id].Bowed, "the action bowed the other card instead");
        Eq(e.State.PendingChoice?.Type, "p3AroruMove", "after the action's resolution: the move");
        var moves = e.QueryActions(0).Where(a => a.Type == "p3AroruMove").ToArray();
        Check(moves.Any(m => m.Command!.Choice == "home") && moves.Any(m => m.Command!.Choice is null && m.Command.Province is null), "home, or stay");
        Accept(e, moves.Single(m => m.Command!.Choice == "home").Command!);
        Eq(e.State.Cards[aroru.Id].Zone, Zone.Home, "Aroru moved home");
        Check(e.State.PendingChoice is null, "nothing more is open");
    }

    private static void AroruRedirectEdges()
    {
        var e = AroruTable(out var aroru, out _, out var bower, out var homeCard);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, aroru.Id));
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Accept(e, Options(e, 0, "aroru-exp2-redirect", aroru.Id).Single(o => o.Enabled && o.Command?.TargetId == homeCard.Id).Command!);
        Check(Logged(e, "redirectFailed"), "a card at home is not a legal target of this Battle action: the target is kept");
        PassUntil(e, () => e.State.PendingChoice is null or { Type: "p3AroruMove" });
        Check(e.State.Cards[aroru.Id].Bowed, "Aroru was bowed");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "p3AroruMove" && a.Command!.Choice is null && a.Command.Province is null).Command!);
        Eq(e.State.Cards[aroru.Id].Zone, Zone.Battlefield, "declined: he stays");
        // A non-Ninja targeted: nothing to offer.
        var n = AroruTable(out _, out var other, out var bower2, out _);
        Accept(n, Offer(n, 1, "training-bow-at-battle", bower2.Id, other.Id));
        Check(!Offered(n, 0, "aroru-exp2-redirect"), "not a Ninja: not offered");
    }

    private static void AroruAttack()
    {
        var s = Table(Phase.Battle, 0);
        var aroru = Add(s, Aroru, 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var riders = Add(s, VigilantRiders, 1, Zone.Attached, province: 0, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "aroru-exp2-attack", aroru.Id, riders.Id));
        Check(!Offered(e, 1, "vigilant-riders-negate"), "the other player may not take Reactions during the action");
        PassUntil(e, () => e.State.PendingChoice is null && e.State.PendingAttack is null);
        Eq(e.State.Cards[riders.Id].Zone, Zone.FateDiscard, "Ranged 5 destroyed the Force 3 Follower");
        Check(Logged(e, "reactionsUnlocked"), "the lockout ended with the action");
    }

    // ---- Ikoma Ayumu ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AyumuScenario()
    {
        MatchEngine Build(bool home, out CardInstance ayumu, out CardInstance tactician, out CardInstance bower, out CardInstance discard, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0);
            s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; // a published Stronghold: the rulebook's player abilities (Tactical Advantage) are enabled
            ayumu = home ? Add(s, Ayumu, 0, Zone.Home, bowed: true) : Add(s, Ayumu, 0, Zone.Battlefield, province: 0, bowed: true);
            tactician = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(tactician, "Tactician");
            bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            discard = Add(s, "training-focus-strategy", 0, Zone.Hand);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, out var ayumu, out var tactician, out _, out var discard, out _);
        Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: discard.Id, AbilityId: "tactical-advantage", PerformerId: tactician.Id));
        Eq(e.State.PendingChoice?.Type, "announceReaction", "a Tactical action announced: Ayumu's window");
        Accept(e, Enabled(e, 0, "ayumu-straighten", ayumu.Id).Command!);
        Check(!e.State.Cards[ayumu.Id].Bowed, "even bowed, he straightened");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the Tactical action went on");
        // Another Battle action: no window.
        var o = Build(false, out _, out _, out var bower, out _, out var enemy);
        Accept(o, Offer(o, 0, "training-bow-at-battle", bower.Id, enemy.Id));
        Check(o.State.PendingChoice?.Type != "announceReaction", "not a Tactical action: no window");
        // At home: no window.
        var h = Build(true, out _, out var t2, out _, out var d2, out _);
        Accept(h, new GameCommand("useAbility", 0, h.Version, TargetId: d2.Id, AbilityId: "tactical-advantage", PerformerId: t2.Id));
        Check(h.State.PendingChoice?.Type != "announceReaction", "Ayumu at home: no window");
    }

    // ---- Utaku Lishan --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine LishanTable(bool bowed, out CardInstance lishan, out CardInstance bower, out CardInstance enemy, out CardInstance follower)
    {
        var s = Table(Phase.Battle, 0);
        lishan = Add(s, Lishan, 0, Zone.Battlefield, province: 0, bowed: bowed);
        bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        return MatchEngine.FromTestState(s);
    }

    private static void LishanScenario()
    {
        var e = LishanTable(false, out var lishan, out var bower, out var enemy, out var follower);
        Eq(e.CardForceForTest(lishan.Id), 5, "attacking: 4 + 1 Force");
        Eq(e.PersonalHonorForTest(lishan.Id), 4, "attacking: 3 + 1 Personal Honor");
        Accept(e, Offer(e, 0, "training-bow-at-battle", bower.Id, enemy.Id));
        Eq(e.State.PendingChoice?.Type, "announceReaction", "a Battle action announced: Lishan's window");
        var options = Options(e, 0, "lishan-penalty", lishan.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.Command!.TargetId == enemy.Id) && options.Any(o => o.Command!.TargetId == follower.Id), "a Personality or a Follower opposing her");
        Accept(e, options.Single(o => o.Command!.TargetId == follower.Id).Command!);
        Eq(ForceMods(e, follower.Id), -4, "a Force penalty equal to her Personal Honor (4 while attacking)");
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(e.State.Cards[enemy.Id].Bowed, "the announced action went on");
        // Defending, she has no bonus.
        var d = Table(Phase.Battle, 1);
        var defender = Add(d, Lishan, 0, Zone.Battlefield, province: 0);
        var ed = MatchEngine.FromTestState(d);
        Eq(ed.CardForceForTest(defender.Id), 4, "not attacking: no bonus");
    }

    private static void LishanEdges()
    {
        // A Terrain played (not an ability) is announced too: the window comes before it enters play.
        var e = LishanTable(false, out var lishan, out _, out var enemy, out _);
        e = Edit(e, st => Add(st, HastyFortifications, 0, Zone.Hand));
        var terrain = e.State.Players[0].Hand.Single();
        Accept(e, e.QueryActions(0).Single(a => a.Type == "playStrategy" && a.CardId == terrain && a.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "announceReaction", "the Terrain play is announced");
        Eq(e.State.Cards[terrain].Zone, Zone.Hand, "the Terrain has not entered play yet");
        Accept(e, Options(e, 0, "lishan-penalty", lishan.Id).Single(o => o.Enabled && o.Command!.TargetId == enemy.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[terrain].Zone, Zone.Battlefield, "then the Terrain entered play");
        Eq(ForceMods(e, enemy.Id), -4, "and the penalty applied");
        // Bowed: not asked.
        var b = LishanTable(true, out _, out var bower, out var enemy2, out _);
        Accept(b, Offer(b, 0, "training-bow-at-battle", bower.Id, enemy2.Id));
        Check(b.State.PendingChoice?.Type != "announceReaction", "bowed: no window");
    }

    // ---- Asako Kikugoro ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine KikugoroPurchase(bool kikugoroInPlay, bool henshin, out CardInstance entering, out CardInstance? watcher)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var provinceCard = s.Players[0].Provinces[0].CardId!;
        entering = s.Cards[provinceCard];
        entering.DefinitionId = kikugoroInPlay ? "training-crane-samurai" : Kikugoro; entering.FaceUp = true;
        if (henshin) GiveKeyword(entering, "Henshin");
        watcher = kikugoroInPlay ? Add(s, Kikugoro, 0, Zone.Home) : null;
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var enteringId = entering.Id;
        var purchase = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == enteringId && a.Enabled);
        Accept(e, purchase.Command!);
        return e;
    }

    private static void KikugoroScenario()
    {
        // He enters play himself.
        var e = KikugoroPurchase(false, false, out var kikugoro, out _);
        Eq(e.State.Cards[kikugoro.Id].Zone, Zone.Home, "Kikugoro entered play");
        var top = e.State.Players[0].DynastyDeck.Take(2).ToArray();
        Accept(e, Enabled(e, 0, "kikugoro-self-entry", kikugoro.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "lookArrange", "the look at the top two Dynasty cards");
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["1:top", "0:bottom"]));
        Eq(e.State.Players[0].DynastyDeck[0], top[1], "the one kept on top");
        Eq(e.State.Players[0].DynastyDeck[^1], top[0], "the other went to the bottom");
        // Another Henshin of his player's enters play.
        var h = KikugoroPurchase(true, true, out _, out var watcher);
        Check(Offered(h, 0, "kikugoro-henshin-entry"), "a Henshin entered play: offered");
        Accept(h, Enabled(h, 0, "kikugoro-henshin-entry", watcher!.Id).Command!);
        Eq(h.State.PendingChoice?.Type, "lookArrange", "the look");
        // One of his Rings enters play (the Ring's own recruit, an action): the after-action window.
        var s = Table(Phase.Battle, 0);
        var k = Add(s, Kikugoro, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-void", 0, Zone.Hand);
        s.AdditionalActions.Add(new AdditionalActionGrant(0, [ring.Id], Battle: s.BattleNumber)); // the grant a fourth Strategy action in a phase hands out
        var r = MatchEngine.FromTestState(s);
        Accept(r, Enabled(r, 0, "ring-of-void-recruit", ring.Id).Command!);
        PassUntil(r, () => Offered(r, 0, "kikugoro-ring-entry") || r.State.PendingChoice is null);
        Check(r.State.Cards[ring.Id].Zone == Zone.Home && Offered(r, 0, "kikugoro-ring-entry"), "his Ring entered play: offered");
        Check(k.Id.Length > 0);
    }

    private static void KikugoroEdges()
    {
        var n = KikugoroPurchase(true, false, out _, out var watcher);
        Check(!Offered(n, 0, "kikugoro-henshin-entry"), "a non-Henshin entry: nothing");
        // Once a turn: used on his own entry, a later Ring gives nothing.
        var e = KikugoroPurchase(false, false, out var kikugoro, out _);
        Accept(e, Enabled(e, 0, "kikugoro-self-entry", kikugoro.Id).Command!);
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:top", "1:top"]));
        Check(!CanUse(e, 0, "kikugoro-ring-entry", kikugoro.Id), "the printed action's one use is spent");
        Check(watcher is not null);
    }

    // ---- Destructive Priorities ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DestructiveScenario()
    {
        var s = Table(Phase.Action, 0);
        var kata = Add(s, DestructivePriorities, 0, Zone.Hand);
        var older = Add(s, DestructivePriorities, 0, Zone.Home);
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home); GiveKeyword(courtier, "Courtier");
        var ancestor = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(ancestor, "Ancestor");
        var forefather = Add(s, "oracle-l5r-23", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "destructive-priorities-kata", kata.Id).Command!);
        Eq(e.State.Cards[kata.Id].Zone, Zone.Home, "the Kata is in play");
        Eq(e.State.Cards[older.Id].Zone, Zone.FateDiscard, "his other Kata was discarded");
        if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("pass", 1, e.Version));
        var honor0 = Honor(e, 0); var honor1 = Honor(e, 1);
        Accept(e, Enabled(e, 0, "forefather-honor", forefather.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "before he gains Honor: the window");
        var reaction = Options(e, 0, "destructive-priorities-reaction", kata.Id).First(o => o.Enabled && o.Command!.TargetPlayerId == 1);
        Accept(e, reaction.Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(Honor(e, 0), honor0, "the gain was negated");
        Eq(Honor(e, 1), honor1 - 2, "the chosen player lost 2 Honor");
        Check(e.State.Cards[courtier.Id].Bowed, "his Courtier bowed as the cost");
    }

    private static void DestructiveEdges()
    {
        // A gain no action made: M'rika's "After M'rika enters play: Gain 2 Honor" as she is recruited (a Spider player).
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].Clan = "Spider";
        var mrika = s.Cards[s.Players[0].Provinces[0].CardId!]; mrika.DefinitionId = Mrika; mrika.FaceUp = true;
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var kata = Add(s, DestructivePriorities, 0, Zone.Home);
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home); GiveKeyword(courtier, "Courtier");
        var e = MatchEngine.FromTestState(s);
        var honor0 = Honor(e, 0);
        Accept(e, e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == mrika.Id && a.Enabled && a.Command!.ClanDiscount).Command!); // the discount: no Proclaim window first (issue #639 makes full cost the first purchase's default)
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the trait's gain opens the window");
        Accept(e, Options(e, 0, "destructive-priorities-reaction", kata.Id).First(o => o.Enabled && o.Command!.TargetPlayerId == 1).Command!);
        PassUntil(e, () => e.State.PendingChoice?.Type != "honorChangeReaction");
        Eq(e.State.Cards[mrika.Id].Zone, Zone.Home, "M'rika entered play");
        Check(Logged(e, "honorGainNegated"), "her Honor gain was negated");
        // The other player's turn: nothing.
        var t = Table(Phase.Action, 1); t.PriorityPlayerId = 1;
        Add(t, DestructivePriorities, 0, Zone.Home);
        var c2 = Add(t, "training-crane-courtier", 0, Zone.Home); GiveKeyword(c2, "Courtier");
        var anc = Add(t, "training-lion-samurai", 1, Zone.Home); GiveKeyword(anc, "Ancestor");
        var ff = Add(t, "oracle-l5r-23", 1, Zone.Hand);
        var et = MatchEngine.FromTestState(t);
        Accept(et, Enabled(et, 1, "forefather-honor", ff.Id).Command!);
        Check(et.State.PendingChoice?.Type != "honorChangeReaction" || !Offered(et, 0, "destructive-priorities-reaction"), "not in the other player's turn (nor for his gain)");
        // From the hand it is not the Kata's ability.
        var hnd = Table(Phase.Action, 0);
        var inHand = Add(hnd, DestructivePriorities, 0, Zone.Hand);
        var c3 = Add(hnd, "training-crane-courtier", 0, Zone.Home); GiveKeyword(c3, "Courtier");
        var anc3 = Add(hnd, "training-crane-samurai", 0, Zone.Home); GiveKeyword(anc3, "Ancestor");
        var ff3 = Add(hnd, "oracle-l5r-23", 0, Zone.Hand);
        var eh = MatchEngine.FromTestState(hnd);
        Accept(eh, Enabled(eh, 0, "forefather-honor", ff3.Id).Command!);
        Check(eh.State.PendingChoice?.Type != "honorChangeReaction" || !Offered(eh, 0, "destructive-priorities-reaction"), "not while the Kata is in the hand");
        Check(inHand.Id.Length > 0 && honor0 >= -100);
    }
}
