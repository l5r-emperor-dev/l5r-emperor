using L5R.Core;
using static RegressionAssertions;

// Issue #830, multi-window audit slice 6: twenty-eight abilities whose own card-specific preconditions the generic kitchen-sink harness could not satisfy, so it never saw them enabled. Each
// scenario below builds the card's real position (Imperial Favor, a Ring grant, dishonorable targets, tokens, Terrain, cards under a Holding ...) and proves the ability is offered AND accepted in
// every window its timing allows, refused (not offered, and the command rejected with the state untouched) in the windows it does not, and that its printed effect happens.
internal static class MultiWindowAudit6Tests
{
    private const string Chukage = "oracle-l5r-10072", Arakan = "oracle-l5r-10485", Kunji = "oracle-l5r-10225", Brawl = "oracle-l5r-1127", Quagmire = "oracle-l5r-6123", Folklore = "oracle-l5r-2621",
        HiddenDefenses = "oracle-l5r-3254", Koiso = "oracle-l5r-3849", Hinata = "oracle-l5r-9838", MerchantOutpost = "oracle-l5r-10267", LordsFavor = "oracle-l5r-10160", ReFortification = "oracle-l5r-6192",
        IndomitableHome = "oracle-l5r-3737", SetTheFieldsAflame = "oracle-l5r-9888", SecondCity = "oracle-l5r-8303", SkippingThePuddle = "oracle-l5r-7285", StuckInRokugan = "oracle-l5r-7593",
        Compassion = "oracle-l5r-7991", HostsAdvantage = "oracle-l5r-8141", Tachikaze = "oracle-l5r-10270", Torazu = "oracle-l5r-8590", ReturnToAction = "oracle-l5r-10953", VersatileBlade = "oracle-l5r-10912",
        SpittingLlama = "oracle-l5r-10686", Zansho = "oracle-l5r-10097", RingOfWater = "oracle-l5r-6339", RingOfFire = "oracle-l5r-6335", FalseRingOfAir = "oracle-l5r-10531";
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Veteran = "training-lion-veteran";

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit-6", [
        ("Asako Chukage (chukage-draw), Limited: bow your Ring to draw a card in the Action Phase on your own turn; refused on another player's turn and in the Battle phase", ChukageDraw),
        ("Brawl (brawl-destroy), Battle/Open: destroys a Region in the Battle phase (attacker, defender) and the Action Phase (active, other); a Terrain too in a battle", BrawlDestroy),
        ("Daigotsu Arakan - exp (wl-pc-arakan-battle), Battle: equips a Follower from the discard pile as attacker or defender, makes it Shadowlands Undead, loses 2 Honor; refused in the Action Phase", ArakanBattle),
        ("Daigotsu Arakan - exp (wl-pc-arakan-limited), Limited: the same equip in the Action Phase on your own turn; refused on another turn and in battle", ArakanLimited),
        ("False Ring of Air (false-ring-of-air-recruit), Battle: recruited from the hand in the after-battle window by the winning attacker and the winning defender, losing 2 Honor; never in the Action Phase", FalseRingAirRecruit),
        ("Ring of Water (ring-of-water-recruit), Battle: recruited in the after-battle window by the attacker and the defender holding the grant of a fourth Battle action of different titles; never in the Action Phase", RingOfWaterRecruit),
        ("Ring of Fire and Training Ring of Fire stub (ring-of-fire-recruit), Battle: recruited in the after-battle window by the winning attacker and defender who destroyed enemy cards; never in the Action Phase", RingOfFireRecruit),
        ("Folklore (promo-b-folklore-force), Battle/Open: -3F to an Unaligned Personality in all four windows (battle attacker, defender, Action Phase active player and other player)", FolkloreForce),
        ("Folklore (promo-b-folklore-move), Battle: a Human performer moves a Nonhuman Personality home as attacker and defender; refused in the Action Phase", FolkloreMove),
        ("Hidden Defenses (hidden-defenses-bow), Battle: with no Personality of yours in play, bows two Personalities and negates their straightening as attacker or defender; refused in the Action Phase", HiddenDefensesBow),
        ("Isawa Koiso (koiso-limited-destroy), Limited: bow Koiso, destroy a base-Fire Shugenja, discard a card, destroy a non-Unique card on your own Action Phase turn only", KoisoLimited),
        ("Kunji - exp (kunji-exp-return-free), Open: a Phoenix Clan player brings the honorably dead Kunji back for free in the Action Phase as active or other player; refused in the Battle phase", KunjiFree),
        ("Kunji - exp (kunji-exp-return-paid), Open: any other Clan brings the honorably dead Kunji back paying all costs in the Action Phase as active or other player; refused in the Battle phase", KunjiPaid),
        ("Merchant Outpost (wl-merchant-outpost-own), Limited: turns your own face-down province card face-up on your own Action Phase turn; refused on another turn and in battle", MerchantOutpostOwn),
        ("My Lord's Favor (lords-favor-straighten), Battle/Open: with the Imperial Favor, discard it to straighten your unit in all four windows", LordsFavorStraighten),
        ("Re-fortification (pe-re-fortification-holding), Limited: puts a Castle Holding from your discard pile into play bowed on your own Action Phase turn only", ReFortificationHolding),
        ("Re-fortification (pe-re-fortification-move), Battle: bow your Castle to move an enemy Personality home as attacker and defender; refused in the Action Phase", ReFortificationMove),
        ("Set the Fields Aflame (set-fields-terrain), Battle: destroy the Spell to destroy a Terrain at the battlefield as attacker and defender; refused in the Action Phase", SetFieldsTerrain),
        ("Set the Fields Aflame (set-fields-region), Open: destroy the Spell to lock a Region's abilities in the Action Phase as active or other player; refused in the Battle phase", SetFieldsRegion),
        ("Skipping the Puddle (skipping-the-puddle-move), Battle: a Water Shugenja moves to the battlefield where enemy units stand, as attacker and defender; refused in the Action Phase", SkippingThePuddleMove),
        ("Stuck in Rokugan (stuck-in-rokugan-discard), Limited: discards a Celestial in play on your own Action Phase turn only", StuckInRokuganDiscard),
        ("The Compassion of the Unicorn (compassion-unicorn-straighten), Battle/Open: an unbowed Unicorn performer straightens another unit and gains Honor in all four windows", CompassionStraighten),
        ("The Host's Advantage (hosts-advantage-destroy), Limited: discard the Imperial Favor to destroy an attachment on your own Action Phase turn only", HostsAdvantageDestroy),
        ("The Tachikaze (w739-tachikaze-battle), Battle: attach a Follower from under it to your Personality as attacker and defender; refused in the Action Phase", TachikazeBattle),
        ("The Tachikaze (w739-tachikaze-limited), Limited: attach a Follower from under it to your Personality on your own Action Phase turn only", TachikazeLimited),
        ("Togashi Torazu (torazu-fire-attack), Battle: destroys one of his Fire tokens to make a Ranged 3 Attack as attacker and defender; refused in the Action Phase", TorazuFireAttack),
        ("Return to Action (return-to-action-move), Battle: only the Defender moves a home Personality to the battlefield and straightens him; refused to the Attacker and in the Action Phase", ReturnToActionMove),
        ("Versatile Blade (tf-versatile-move), Battle: discard a card to move the wielder from home to a battlefield where he would be opposed, as attacker and defender; refused in the Action Phase", VersatileBladeMove),
    ]);


    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase, int actor) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);

    // ---- harness -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // One window an ability may (or may not) be used in. Battle: player 1 attacks, player 0 defends. Action: player 0 is the active player.
    private sealed record Win(string Name, Phase Phase, int Active, int Me);
    private static readonly Win Attacker = new("battle-attacker", Phase.Battle, 1, 1), Defender = new("battle-defender", Phase.Battle, 1, 0),
        ActionActive = new("action-active", Phase.Action, 0, 0), ActionOther = new("action-other", Phase.Action, 0, 1);
    private static readonly Win[] BattleWins = [Attacker, Defender], ActionWins = [ActionActive, ActionOther], AllWins = [Attacker, Defender, ActionActive, ActionOther];

    private static bool InBattle(Win w) => w.Phase == Phase.Battle;

    // The table for a window with the acting player holding priority. `ghostBattle` fakes a current battlefield in the Action Phase, so a Battle-only ability is refused by its timing alone.
    private static MatchState Start(Win w, bool ghostBattle = false)
    {
        var s = Table(w.Phase, w.Active);
        s.PriorityPlayerId = w.Me; s.BattleNumber = 1;
        if (ghostBattle && w.Phase != Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    // A unit card at the battlefield in a battle window, at home in an Action Phase window.
    private static CardInstance At(MatchState s, string def, int owner, Win w, bool bowed = false) => Add(s, def, owner, InBattle(w) ? Zone.Battlefield : Zone.Home, bowed, InBattle(w) ? 0 : null);
    private static CardInstance AtBattlefield(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, 0);
    private static void Richer(MatchState s, int owner, int holdings = 8) { for (var i = 0; i < holdings; i++) Add(s, "training-marketplace", owner, Zone.Home); }
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);

    private sealed class Case
    {
        public MatchEngine E = null!; public string Ability = "", Card = ""; public string? Target, Performer; public SlotSelection[]? Selections; public bool Pay;
        public int HandBefore, HonorBefore; public string[] Marks = [];
        public static Case Of(MatchState s, string ability, CardInstance card, string? target = null, string? performer = null, SlotSelection[]? selections = null, bool pay = false) =>
            new() { E = MatchEngine.FromTestState(s), Ability = ability, Card = card.Id, Target = target, Performer = performer, Selections = selections, Pay = pay };
        public CardInstance Get(string id) => E.State.Cards[id];
    }

    private static string Listing(Case c, int me) => string.Join("; ", Options(c.E, me, c.Ability, c.Card, c.Performer).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}"));

    // Every allowed window: the option is offered enabled, executes, and `verify` sees the printed effect. Every refused window: nothing is offered enabled, and the command is rejected with the state untouched.
    private static void Drive(string name, Win[] allowed, Win[] refused, Func<Win, Case> build, Action<Win, Case>? verify = null)
    {
        foreach (var w in allowed)
        {
            var c = build(w);
            var option = Options(c.E, w.Me, c.Ability, c.Card, c.Performer).FirstOrDefault(o => o.Enabled);
            Check(option is not null, $"{name} [{w.Name}]: {c.Ability} was not offered enabled to player {w.Me}: {Listing(c, w.Me)}");
            c.HandBefore = c.E.State.Players[w.Me].Hand.Count; c.HonorBefore = c.E.State.Players[w.Me].Honor;
            var command = option!.Command! with { TargetId = c.Selections is null ? c.Target ?? option.Command.TargetId : null, PerformerId = c.Performer ?? option.Command.PerformerId };
            if (c.Selections is { } selections) command = command with { Selections = selections };
            if (c.Pay && c.Target is { } paid && option.TargetGold is { } quotes && quotes.TryGetValue(paid, out var quote)) command = command with { SourceIds = quote.Payments[0].SourceIds };
            var result = c.E.Execute(command);
            Check(result.Accepted, $"{name} [{w.Name}]: {c.Ability} was refused when executed: {result.Error}");
            verify?.Invoke(w, c);
        }
        foreach (var w in refused)
        {
            var c = build(w);
            Check(!Options(c.E, w.Me, c.Ability, c.Card, c.Performer).Any(o => o.Enabled), $"{name} [{w.Name}]: {c.Ability} was offered enabled outside its window");
            Reject(c.E, Use(c.E, w.Me, c.Ability, c.Card, c.Target, c.Performer, c.Selections ?? []));
        }
    }

    // Declines every open window and passes until nothing is pending.
    private static void Settle(MatchEngine e, int limit = 16)
    {
        for (var guard = 0; guard < limit && e.State.PendingChoice is not null; guard++)
        {
            var actor = e.State.PendingChoice!.PlayerId;
            var option = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass")
                ?? e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass");
            Check(option is not null, $"nothing to decline in {e.State.PendingChoice!.Type}: " + string.Join("; ", e.QueryActions(actor).Select(x => $"{x.Type}:{x.Label} [{x.Enabled}] {x.Reason}")));
            Accept(e, option!.Command!);
        }
    }

    private static void Region(MatchState s, int owner, int province, string def = Quagmire)
    {
        var region = Add(s, def, owner, Zone.Region, province: province); s.Players[owner].Provinces[province].RegionId = region.Id;
    }

    // ---- Asako Chukage --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChukageDraw() => Drive("Chukage", [ActionActive], [ActionOther, Attacker, Defender], w =>
    {
        var s = Start(w);
        var chukage = Add(s, Chukage, w.Me, Zone.Home);
        var ring = Add(s, "training-ring-of-void", w.Me, Zone.Home);
        for (var i = 0; i < 6; i++) { var top = Add(s, "training-focus-strategy", w.Me, Zone.FateDeck); s.Players[w.Me].FateDeck.Insert(0, top.Id); }
        return Case.Of(s, "chukage-draw", chukage, ring.Id);
    }, (w, c) =>
    {
        Eq(c.E.State.Players[w.Me].Hand.Count, c.HandBefore + 1, "Chukage drew a card");
        Check(c.Get(c.Target!).Bowed, "the Ring was bowed as the cost");
    });

    // ---- Brawl ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BrawlDestroy()
    {
        Drive("Brawl region", AllWins, [], w =>
        {
            var s = Start(w);
            var brawl = Add(s, Brawl, w.Me, Zone.Hand);
            if (InBattle(w)) AtBattlefield(s, Crane, w.Me);
            Region(s, 1 - w.Me, 0);
            return Case.Of(s, "brawl-destroy", brawl, s.Players[1 - w.Me].Provinces[0].RegionId);
        }, (w, c) => Eq(c.Get(c.Target!).Zone, Zone.DynastyDiscard, "the Region was destroyed"));
        // A Terrain at the battlefield, destroyed in a battle by either side.
        Drive("Brawl terrain", BattleWins, [], w =>
        {
            var s = Start(w);
            var brawl = Add(s, Brawl, w.Me, Zone.Hand);
            AtBattlefield(s, Crane, w.Me);
            var terrain = AtBattlefield(s, "training-before-resolution-boost-terrain", 1 - w.Me);
            return Case.Of(s, "brawl-destroy", brawl, terrain.Id);
        }, (w, c) => Check(c.Get(c.Target!).Zone != Zone.Battlefield, "the Terrain was destroyed"));
    }

    // ---- Daigotsu Arakan - exp --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Case ArakanCase(Win w, string ability)
    {
        var s = Start(w, ghostBattle: ability.EndsWith("battle"));
        Richer(s, w.Me, 10);
        var arakan = At(s, Arakan, w.Me, w);
        var follower = Add(s, SpittingLlama, w.Me, Zone.FateDiscard); // non-Unique, 5 Gold
        return Case.Of(s, ability, arakan, follower.Id, arakan.Id, pay: true);
    }

    private static void ArakanVerify(Win w, Case c)
    {
        Settle(c.E);
        var follower = c.Get(c.Target!);
        Check(follower.Zone == Zone.Attached && follower.AttachedTo == c.Performer, "the Follower was equipped to Arakan");
        Check(c.E.HasKeyword(follower.Id, "Shadowlands") && c.E.HasKeyword(follower.Id, "Undead"), "permanently Shadowlands and Undead");
        Eq(c.E.State.Players[w.Me].Honor, c.HonorBefore - 2, "lose 2 Honor");
    }

    private static void ArakanBattle() => Drive("Arakan battle", BattleWins, ActionWins, w => ArakanCase(w, "wl-pc-arakan-battle"), ArakanVerify);
    private static void ArakanLimited() => Drive("Arakan limited", [ActionActive], [ActionOther, Attacker, Defender], w => ArakanCase(w, "wl-pc-arakan-limited"), ArakanVerify);

    // ---- Rings: recruited in the after-battle window ----------------------------------------------------------------------------------------------------------------------------------------

    // The winner (player `w.Me`) fights a lone enemy Personality at province 0 and gets the Ring granted at the battle's resolution; the Ring is then put into play from the hand in the after-battle window.
    private static void RingRecruit(string ringDef, string ability, Action<MatchState, Win> prime, int honorLoss)
    {
        foreach (var w in BattleWins)
        {
            var s = Start(w);
            var ring = Add(s, ringDef, w.Me, Zone.Hand);
            Force(AtBattlefield(s, Veteran, w.Me), 20);
            AtBattlefield(s, Crane, 1 - w.Me);
            prime(s, w);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, w.Me, ability, ring.Id), $"{ringDef} [{w.Name}]: not offered before the battle resolves");
            Reject(e, Use(e, w.Me, ability, ring.Id));
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.PendingChoice?.Type, "battleRecruit", $"{ringDef} [{w.Name}]: the after-battle window opened for the winner");
            var honor = Honor(e, w.Me);
            var option = Enabled(e, w.Me, ability, ring.Id);
            Check(option.Enabled, option.Reason ?? "");
            Accept(e, option.Command!);
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, $"{ringDef} [{w.Name}]: the Ring was put into play");
            Check(e.State.Cards[ring.Id].FaceUp, "face-up");
            Eq(Honor(e, w.Me), honor - honorLoss, $"{ringDef} [{w.Name}]: Honor lost on entering play");
        }
        // Never an Action Phase action, for either player, even with the Ring in hand.
        foreach (var w in ActionWins)
        {
            var s = Start(w);
            var ring = Add(s, ringDef, w.Me, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, w.Me, ability, ring.Id), $"{ringDef} [{w.Name}]: not offered in the Action Phase");
            Reject(e, Use(e, w.Me, ability, ring.Id));
        }
    }

    private static void FalseRingAirRecruit() => RingRecruit(FalseRingOfAir, "false-ring-of-air-recruit", (_, _) => { }, 2);

    // The grant a fourth Battle action of different titles hands out during the battle (RingGrantTests and the core Ring of Water case cover the count): it stays for the after-battle window.
    private static void RingOfWaterRecruit()
    {
        foreach (var w in BattleWins)
        {
            var s = Start(w);
            var ring = Add(s, RingOfWater, w.Me, Zone.Hand);
            Force(AtBattlefield(s, Veteran, w.Me), 20);
            AtBattlefield(s, Crane, 1 - w.Me);
            s.AdditionalActions.Add(new AdditionalActionGrant(w.Me, [ring.Id], Battle: s.BattleNumber));
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.PendingChoice?.Type, "battleRecruit", $"Ring of Water [{w.Name}]: the after-battle window opened for the winner");
            var option = Enabled(e, w.Me, "ring-of-water-recruit", ring.Id);
            Check(option.Enabled, option.Reason ?? "");
            Accept(e, option.Command!);
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, $"Ring of Water [{w.Name}]: the Ring was put into play");
        }
        // Never an Action Phase action, for either player, even with the Ring in hand.
        foreach (var w in ActionWins)
        {
            var s = Start(w);
            var ring = Add(s, RingOfWater, w.Me, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, w.Me, "ring-of-water-recruit", ring.Id), $"Ring of Water [{w.Name}]: not offered in the Action Phase");
            Reject(e, Use(e, w.Me, "ring-of-water-recruit", ring.Id));
        }
    }

    private static void RingOfFireRecruit()
    {
        RingRecruit(RingOfFire, "ring-of-fire-recruit", (_, _) => { }, 0);
        RingRecruit("training-ring-of-fire-stub", "ring-of-fire-recruit", (_, _) => { }, 0);
    }

    // ---- Folklore -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FolkloreForce() => Drive("Folklore force", AllWins, [], w =>
    {
        var s = Start(w);
        var card = Add(s, Folklore, w.Me, Zone.Hand);
        if (InBattle(w)) AtBattlefield(s, Crane, w.Me);
        var unaligned = At(s, "training-discard-attacher", 1 - w.Me, w);
        return Case.Of(s, "promo-b-folklore-force", card, unaligned.Id);
    }, (_, c) => Check(c.Get(c.Target!).Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3), "he has -3F"));

    private static void FolkloreMove() => Drive("Folklore move", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var card = Add(s, Folklore, w.Me, Zone.Hand);
        var human = At(s, Crane, w.Me, w);
        var oni = AtBattlefield(s, "training-nonhuman-personality", 1 - w.Me);
        return Case.Of(s, "promo-b-folklore-move", card, oni.Id, human.Id);
    }, (_, c) => Eq(c.Get(c.Target!).Zone, Zone.Home, "the Nonhuman Personality moved home"));

    // ---- Hidden Defenses ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HiddenDefensesBow() => Drive("Hidden Defenses", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var card = Add(s, HiddenDefenses, w.Me, Zone.Hand);
        var here = AtBattlefield(s, Lion, 1 - w.Me);
        var home = Add(s, Veteran, 1 - w.Me, Zone.Home);
        var c = Case.Of(s, "hidden-defenses-bow", card, selections: [Pick("targets", here.Id, home.Id)]);
        c.Target = null; c.Performer = null; c.Marks = [here.Id, home.Id];
        return c;
    }, (_, c) =>
    {
        foreach (var id in c.Marks) { Check(c.Get(id).Bowed, "bowed wherever he stood"); Eq(c.Get(id).StraightenLockedTurn, c.E.State.TurnNumber, "his straightening is negated for the turn"); }
    });

    // ---- Isawa Koiso ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KoisoLimited() => Drive("Koiso", [ActionActive], [ActionOther, Attacker, Defender], w =>
    {
        var s = Start(w);
        var koiso = Add(s, Koiso, w.Me, Zone.Home);
        var hinata = Add(s, Hinata, w.Me, Zone.Home);
        var enemy = Add(s, Crane, 1 - w.Me, Zone.Home);
        var discard = Add(s, "training-focus-strategy", w.Me, Zone.Hand);
        var c = Case.Of(s, "koiso-limited-destroy", koiso, selections: [Pick("target", enemy.Id), Pick("fire", hinata.Id), Pick("discard", discard.Id)]);
        c.Marks = [koiso.Id, hinata.Id, discard.Id, enemy.Id];
        return c;
    }, (_, c) =>
    {
        Settle(c.E);
        Check(c.Get(c.Marks[0]).Bowed, "Koiso bowed");
        Eq(c.Get(c.Marks[1]).Zone, Zone.Dead, "the Fire Shugenja was destroyed as a cost");
        Eq(c.Get(c.Marks[2]).Zone, Zone.FateDiscard, "the card was discarded");
        Eq(c.Get(c.Marks[3]).Zone, Zone.Dead, "the target was destroyed");
    });

    // ---- Kunji - exp ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KunjiFree() => Drive("Kunji free", ActionWins, BattleWins, w =>
    {
        var s = Start(w);
        s.Players[w.Me].Clan = "Phoenix";
        var kunji = Add(s, Kunji, w.Me, Zone.Dead);
        return Case.Of(s, "kunji-exp-return-free", kunji, kunji.Id);
    }, (_, c) => Eq(c.Get(c.Card).Zone, Zone.Home, "Kunji came back into play"));

    private static void KunjiPaid() => Drive("Kunji paid", ActionWins, BattleWins, w =>
    {
        var s = Start(w);
        s.Players[w.Me].Clan = "Lion"; Richer(s, w.Me, 10);
        var kunji = Add(s, Kunji, w.Me, Zone.Dead);
        return Case.Of(s, "kunji-exp-return-paid", kunji, kunji.Id);
    }, (_, c) =>
    {
        Eq(c.Get(c.Card).Zone, Zone.Home, "Kunji came back into play");
        Check(c.E.State.Events.Any(x => x.Type == "payment"), "paying all costs: a payment was made");
    });

    // ---- Merchant Outpost -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MerchantOutpostOwn() => Drive("Merchant Outpost", [ActionActive], [ActionOther, Attacker, Defender], w =>
    {
        var s = Start(w);
        var outpost = Add(s, MerchantOutpost, w.Me, Zone.Home);
        var down = s.Cards[s.Players[w.Me].Provinces[0].CardId!]; down.FaceUp = false;
        return Case.Of(s, "wl-merchant-outpost-own", outpost, down.Id);
    }, (_, c) =>
    {
        Check(c.Get(c.Target!).FaceUp, "your card is face-up");
        Check(!c.Get(c.Card).Bowed, "no bow for your own card");
    });

    // ---- My Lord's Favor ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LordsFavorStraighten() => Drive("My Lord's Favor", AllWins, [], w =>
    {
        var s = Start(w); s.FavorPlayerId = w.Me;
        var card = Add(s, LordsFavor, w.Me, Zone.Hand);
        var unit = At(s, Lion, w.Me, w, bowed: true);
        return Case.Of(s, "lords-favor-straighten", card, unit.Id);
    }, (_, c) =>
    {
        Settle(c.E);
        Check(!c.Get(c.Target!).Bowed, "the unit was straightened");
        Eq(c.E.State.FavorPlayerId, null, "the Imperial Favor was discarded");
    });

    // ---- Re-fortification -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ReFortificationHolding() => Drive("Re-fortification holding", [ActionActive], [ActionOther, Attacker, Defender], w =>
    {
        var s = Start(w);
        var card = Add(s, ReFortification, w.Me, Zone.Hand);
        var castle = Add(s, IndomitableHome, w.Me, Zone.DynastyDiscard);
        return Case.Of(s, "pe-re-fortification-holding", card, castle.Id);
    }, (_, c) =>
    {
        Eq(c.Get(c.Target!).Zone, Zone.Home, "the Castle is in play");
        Check(c.Get(c.Target!).Bowed, "bowed");
    });

    private static void ReFortificationMove() => Drive("Re-fortification move", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var card = Add(s, ReFortification, w.Me, Zone.Hand);
        var castle = Add(s, IndomitableHome, w.Me, Zone.Home);
        var enemy = AtBattlefield(s, Lion, 1 - w.Me);
        return Case.Of(s, "pe-re-fortification-move", card, enemy.Id, castle.Id);
    }, (_, c) =>
    {
        Eq(c.Get(c.Target!).Zone, Zone.Home, "the enemy Personality moved home");
        Check(c.Get(c.Performer!).Bowed, "the Castle was bowed");
    });

    // ---- Set the Fields Aflame ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SetFieldsTerrain() => Drive("Set the Fields Aflame terrain", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var air = At(s, "training-seeds5-air-shugenja", w.Me, w);
        var spell = Add(s, SetTheFieldsAflame, w.Me, Zone.Attached, false, air.ProvinceIndex, air.Id);
        AtBattlefield(s, Lion, 1 - w.Me);
        var terrain = AtBattlefield(s, "training-before-resolution-boost-terrain", 1 - w.Me);
        var c = Case.Of(s, "set-fields-terrain", spell, terrain.Id);
        return c;
    }, (_, c) =>
    {
        Settle(c.E);
        Check(c.Get(c.Target!).Zone != Zone.Battlefield, "the Terrain was destroyed");
        Eq(c.Get(c.Card).Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
    });

    private static void SetFieldsRegion() => Drive("Set the Fields Aflame region", ActionWins, BattleWins, w =>
    {
        var s = Start(w);
        var caster = At(s, "training-seeds5-air-shugenja", w.Me, w);
        var spell = Add(s, SetTheFieldsAflame, w.Me, Zone.Attached, false, caster.ProvinceIndex, caster.Id);
        var city = Add(s, SecondCity, 1 - w.Me, Zone.Region, province: 0); s.Players[1 - w.Me].Provinces[0].RegionId = city.Id;
        return Case.Of(s, "set-fields-region", spell, city.Id);
    }, (w, c) =>
    {
        Settle(c.E);
        Eq(c.Get(c.Card).Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        var locked = Edit(c.E, st => { st.PriorityPlayerId = 1 - w.Me; st.PendingChoice = null; });
        Check(!locked.QueryActions(1 - w.Me).Any(a => a.Type == "useAbility" && a.Enabled && a.CardId == c.Target), "the Region's abilities may not be used this turn");
    });

    // ---- Skipping the Puddle --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SkippingThePuddleMove() => Drive("Skipping the Puddle", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var kata = Add(s, SkippingThePuddle, w.Me, Zone.Home);
        var water = Kw(Add(s, "training-shugenja", w.Me, Zone.Home), "Water");
        AtBattlefield(s, Lion, 1 - w.Me);
        return Case.Of(s, "skipping-the-puddle-move", kata, performer: water.Id);
    }, (w, c) =>
    {
        var water = c.Get(c.Performer!);
        Check(water.Zone == Zone.Battlefield && water.ProvinceIndex == 0, "the Water Shugenja moved to the current battlefield");
        Check(c.E.State.AdditionalActions.Any(g => g.PlayerId == w.Me && g.ScopeCardIds is { Length: 1 } ids && ids[0] == water.Id), "and its controller may take an additional Battle action that he must perform");
    });

    // ---- Stuck in Rokugan -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void StuckInRokuganDiscard() => Drive("Stuck in Rokugan", [ActionActive], [ActionOther, Attacker, Defender], w =>
    {
        var s = Start(w);
        var card = Add(s, StuckInRokugan, w.Me, Zone.Hand);
        var celestial = Add(s, "training-bow-celestial", 1 - w.Me, Zone.Celestial);
        return Case.Of(s, "stuck-in-rokugan-discard", card, celestial.Id);
    }, (_, c) => Eq(c.Get(c.Target!).Zone, Zone.DynastyDiscard, "the Celestial was discarded from play"));

    // ---- The Compassion of the Unicorn ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CompassionStraighten() => Drive("Compassion of the Unicorn", AllWins, [], w =>
    {
        var s = Start(w);
        var card = Add(s, Compassion, w.Me, Zone.Hand);
        var unicorn = At(s, "training-battle-maiden", w.Me, w);
        var enemy = At(s, Veteran, 1 - w.Me, w, bowed: true);
        var follower = Add(s, "training-follower", 1 - w.Me, Zone.Attached, true, enemy.ProvinceIndex, enemy.Id);
        var c = Case.Of(s, "compassion-unicorn-straighten", card, enemy.Id, unicorn.Id);
        c.Marks = [follower.Id];
        return c;
    }, (w, c) =>
    {
        Settle(c.E);
        Check(!c.Get(c.Target!).Bowed && !c.Get(c.Marks[0]).Bowed, "the whole unit was straightened, Follower included");
        Eq(c.E.State.Players[w.Me].Honor, c.HonorBefore + 2, "2 Honor for a unit you do not control");
    });

    // ---- The Host's Advantage -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HostsAdvantageDestroy() => Drive("The Host's Advantage", [ActionActive], [ActionOther, Attacker, Defender], w =>
    {
        var s = Start(w); s.FavorPlayerId = w.Me;
        var card = Add(s, HostsAdvantage, w.Me, Zone.Hand);
        var host = Add(s, Lion, 1 - w.Me, Zone.Home);
        var item = Add(s, "training-item", 1 - w.Me, Zone.Attached, false, null, host.Id);
        return Case.Of(s, "hosts-advantage-destroy", card, item.Id);
    }, (_, c) =>
    {
        Settle(c.E);
        Check(c.Get(c.Target!).Zone != Zone.Attached, "the attachment was destroyed");
        Eq(c.E.State.FavorPlayerId, null, "the Imperial Favor was discarded");
    });

    // ---- The Tachikaze --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Case TachikazeCase(Win w, string ability, bool ghostBattle)
    {
        var s = Start(w, ghostBattle);
        Richer(s, w.Me, 6);
        var holding = Add(s, Tachikaze, w.Me, Zone.Home);
        var host = At(s, Crane, w.Me, w);
        var follower = Add(s, "training-follower", w.Me, Zone.FateDiscard);
        var under = MatchEngine.FromTestState(s);
        under.PutUnder(follower.Id, holding.Id, true); // a Follower collected under the Holding
        var c = Case.Of(under.State, ability, holding, selections: [Pick("attachment", follower.Id), Pick("personality", host.Id)]);
        c.Marks = [follower.Id, host.Id];
        return c;
    }

    private static void TachikazeVerify(Win w, Case c)
    {
        Settle(c.E);
        var follower = c.Get(c.Marks[0]);
        Eq(follower.Zone, Zone.Attached, "the Follower was attached from under the Holding");
        Eq(follower.AttachedTo, c.Marks[1], "to the Personality");
    }

    private static void TachikazeBattle() => Drive("Tachikaze battle", BattleWins, ActionWins, w => TachikazeCase(w, "w739-tachikaze-battle", true), TachikazeVerify);
    private static void TachikazeLimited() => Drive("Tachikaze limited", [ActionActive], [ActionOther, Attacker, Defender], w => TachikazeCase(w, "w739-tachikaze-limited", false), TachikazeVerify);

    // ---- Togashi Torazu -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TorazuFireAttack() => Drive("Torazu", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var torazu = AtBattlefield(s, Torazu, w.Me);
        torazu.Tokens.Add(new("t1", "Fire", "test-fixture", "Force", 1)); torazu.Tokens.Add(new("t2", "Fire", "test-fixture", "Force", 1));
        var enemy = AtBattlefield(s, Crane, 1 - w.Me);
        var c = Case.Of(s, "torazu-fire-attack", torazu); c.Marks = [enemy.Id];
        return c;
    }, (_, c) =>
    {
        Eq(c.Get(c.Card).Tokens.Count(t => t.Type == "Fire"), 1, "one Fire token was destroyed as the cost");
        Check(c.Get(c.Marks[0]).Zone != Zone.Battlefield, "the Ranged 3 Attack destroyed the enemy Personality (Chi 2): " + c.Get(c.Marks[0]).Zone);
    });

    // ---- Return to Action -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ReturnToActionMove() => Drive("Return to Action", [Defender], [Attacker, ActionActive, ActionOther], w =>
    {
        var s = Start(w, ghostBattle: true);
        var card = Add(s, ReturnToAction, w.Me, Zone.Hand);
        var bowed = Add(s, Crane, w.Me, Zone.Home, bowed: true);
        Add(s, "training-crane-veteran", w.Me, Zone.Home);
        AtBattlefield(s, Veteran, 1 - w.Me);
        return Case.Of(s, "return-to-action-move", card, bowed.Id);
    }, (_, c) =>
    {
        var moved = c.Get(c.Target!);
        Check(moved.Zone == Zone.Battlefield && moved.ProvinceIndex == 0, "moved to the current battlefield");
        Check(!moved.Bowed, "straightened after he moved");
    });

    // ---- Versatile Blade ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void VersatileBladeMove() => Drive("Versatile Blade", BattleWins, ActionWins, w =>
    {
        var s = Start(w, ghostBattle: true);
        var wielder = Add(s, Crane, w.Me, Zone.Home);
        var blade = Attach(s, VersatileBlade, wielder);
        var discard = Add(s, Lion, w.Me, Zone.Hand);
        AtBattlefield(s, Lion, 1 - w.Me);
        var c = Case.Of(s, "tf-versatile-move", blade, discard.Id);
        c.Marks = [wielder.Id];
        return c;
    }, (_, c) =>
    {
        Check(c.Get(c.Marks[0]).Zone == Zone.Battlefield && c.Get(c.Marks[0]).ProvinceIndex == 0, "the wielder moved to the battlefield");
        Eq(c.Get(c.Target!).Zone, Zone.FateDiscard, "the card was discarded");
    });
}
