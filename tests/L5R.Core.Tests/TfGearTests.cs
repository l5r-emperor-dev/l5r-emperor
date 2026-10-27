using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Twenty Festivals gear (issue #622, batch tf-gear): Followers, Items and Spells wired from the printed text of their earliest Emperor-era scan. Every scenario is an explicit position on the economy table
// (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the current battlefield) driven only through QueryActions and the commands the engine offers.
internal static class TfGearTests
{
    private const string BandOfBrothers = "oracle-l5r-10891", CamelMounts = "oracle-l5r-10682", Lookout = "oracle-l5r-9870",
        LostBlade = "oracle-l5r-10693", OminousArmor = "oracle-l5r-10907", RedHungerFang = "oracle-l5r-10696", SeppunBlade = "oracle-l5r-10695", TigerClaw = "oracle-l5r-10910", VersatileBlade = "oracle-l5r-10912",
        Conflagration = "oracle-l5r-1449", HitomisDevotion = "oracle-l5r-10917", KiyoshisWrath = "oracle-l5r-10701", SailorsWarning = "oracle-l5r-10920", StealTheFlame = "oracle-l5r-10704",
        ThunderResounds = "oracle-l5r-10699", TyphoonSurge = "oracle-l5r-10543",
        Conscripts = "oracle-l5r-10004", TsuruchiDaikyu = "oracle-l5r-8823", TempestOfFlame = "oracle-l5r-7866", DragonsFavor = "oracle-l5r-18", Crane = "training-crane-samurai", Lion = "training-lion-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);
    private static GameCommand Equip(MatchEngine e, int player, CardInstance card, CardInstance host, int gold) =>
        new("equip", player, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(player, gold).First().SourceIds, TargetId: host.Id);

    public static void Run(TestRunner runner) => runner.Run("tf-gear", [
        ("Published shape: every card carries its printed stats, keywords, text and art, the packaged provenance names the Emperor-era printing, and each is in its wave's manifest", PublishedShape),
        ("Band of Brothers: its entering-play window creates a 1F Follower for each of one or two chosen Personalities; three, none or a foreign Personality is refused", BandOfBrothersScenario),
        ("Camel Mounts: attaches only to a Human and gives Cavalry; its Home Battle move brings the unit to a battlefield where it would be opposed, from home", CamelMountsScenario),
        ("Lookout: after its player announces a Ranged Attack, destroying it lets that one Ranged Attack target a card in the Defender's home -- from anywhere, never for a Melee Attack, never without it", LookoutScenario),
        ("Lost Blade of the Maharaja: bows this card to bow a Personality (either player's) whose Force is equal to or lower than the wielder's", LostBladeScenario),
        ("Ominous Armor: another player's card destroying the Personality destroys the Armor instead; his own player's, the battle's casualty sweep and an unarmored unit are unchanged", OminousArmorScenario),
        ("Red Hunger's Fang: 3 Honor is lost entering play; bowing it gives an enemy Personality a -1F/-1C Poison token", RedHungerScenario),
        ("Seppun's Blessed Blade: the Force modifier equals the wielder's Personal Honor (0 while dishonorable), and follows it as it changes", SeppunScenario),
        ("Tiger Claw: bows this card for a Melee 3 Attack by the wielder", TigerClawScenario),
        ("Versatile Blade: discarding a card straightens the wielder; its Home Battle move discards a card to bring him to a battlefield where he would be opposed", VersatileBladeScenario),
        ("Conflagration: a Fire Shugenja's Ranged Attacks have +1 strength per Spell (never a non-Fire one's); its Battle action bows it for +3F until the turn ends", ConflagrationScenario),
        ("Hitomi's Devotion: battle resolution does not destroy its unit while defending, nor while attacking if the Shugenja is Earth; other units still fall", HitomisDevotionScenario),
        ("Kiyoshi's Wrath: bows this card to bow an enemy Follower or Personality with 4 or lower Force; stronger, own and out-of-play cards are refused", KiyoshisWrathScenario),
        ("Sailor's Warning: names your Personality, then aims a Ranged Attack with strength equal to his Chi, and destroys the Spell", SailorsWarningScenario),
        ("Steal the Candle's Flame: its Interrupt gives a pending Melee or Ranged Attack +2 strength; its Battle action is a Ranged 0 Attack", StealTheFlameScenario),
        ("The Thunder Resounds: copies one ability of a Spell at the battlefield (or a Thunder Spell anywhere) onto this Spell until the turn ends", ThunderResoundsScenario),
        ("Typhoon Surge: moves a Personality from any location to the battlefield, straightens his unit if the actor does not control him, and destroys the Spell", TyphoonSurgeScenario),
        ("Negation: a negated action still pays its printed cost (the bow, the discard, the destroyed Lookout) and none of its effects happens", NegatedActionsPayTheirCosts),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));

    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(new HashSet<string>(listed).SetEquals(ids), $"{wave} manifest is exactly its wired cards");
        foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, ids.Length, $"{wave} has a provenance row per card");
    }

    private static string PrintingOf(string wave, string id) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray()
            .Single(r => r.GetProperty("card_id").GetString() == id).GetProperty("printing_set")[0].GetString()!;

    private static void Shape(string id, CardType type, string name, int gold, int force, int chi, int focus, string[] keywords, string[] abilities, string effect)
    {
        var d = CardCatalog.Get(id);
        Check(d.Type == type && d.Name == name && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.Focus == focus, $"{name}: printed stats");
        Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
        Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
        Eq(d.Effect, effect, $"{name}: effect");
        Check(d.Clan == "Unaligned", $"{name}: Unaligned");
    }

    private static void PublishedShape()
    {
        Manifest("follower-wave-45", BandOfBrothers, CamelMounts, Lookout);
        Manifest("item-wave-41", LostBlade, OminousArmor, RedHungerFang, SeppunBlade, TigerClaw, VersatileBlade);
        Manifest("spell-wave-50", Conflagration, HitomisDevotion, KiyoshisWrath, SailorsWarning, StealTheFlame, ThunderResounds, TyphoonSurge);
        // The printing the card is packaged from is the earliest Emperor-era one, not Oracle's later template.
        Eq(PrintingOf("follower-wave-45", BandOfBrothers), "Aftermath"); Eq(PrintingOf("follower-wave-45", CamelMounts), "Gates of Chaos"); Eq(PrintingOf("follower-wave-45", Lookout), "The Shadow's Embrace");
        Eq(PrintingOf("item-wave-41", OminousArmor), "Aftermath"); Eq(PrintingOf("item-wave-41", SeppunBlade), "Gates of Chaos");
        Eq(PrintingOf("spell-wave-50", Conflagration), "Emperor Edition"); Eq(PrintingOf("spell-wave-50", TyphoonSurge), "Coils of Madness");

        Shape(BandOfBrothers, CardType.Follower, "Band of Brothers", 8, 2, 0, 2, ["Ronin"], [TfGearAbilities.BandCreate], "none");
        Shape(CamelMounts, CardType.Follower, "Camel Mounts", 6, 3, 0, 2, ["Camel", "Nonhuman", "Cavalry"], [TfGearAbilities.CamelMove], "none");
        Shape(Lookout, CardType.Follower, "Lookout", 4, 3, 0, 2, ["Ninja"], [TfGearAbilities.LookoutDestroy], "none");
        Eq(CardCatalog.Get(CamelMounts).AttachRequiresKeyword, "Human"); Eq(CardCatalog.Get(CamelMounts).AttachGrantsKeyword, "Cavalry"); Eq(CardCatalog.Get(Lookout).AttachRequiresKeyword, "Ninja");
        // Weapons and Armor carry only the keyword printed on the scan (Oracle's later Two-Handed / One-Handed / Sword / Gaijin / Ninja are not printed).
        Shape(LostBlade, CardType.Item, "Lost Blade of the Maharaja", 7, 3, 1, 3, ["Weapon"], [TfGearAbilities.LostBladeBow], "none");
        Shape(OminousArmor, CardType.Item, "Ominous Armor", 3, 2, 0, 1, ["Armor"], [], TfGearAbilities.OminousArmorEffect);
        Shape(RedHungerFang, CardType.Item, "Red Hunger's Fang", 4, 2, 0, 4, ["Weapon"], [TfGearAbilities.RedHungerPoison], "none");
        Eq(CardCatalog.Get(RedHungerFang).EntryHonorLoss, 3);
        Shape(SeppunBlade, CardType.Item, "Seppun's Blessed Blade", 5, 0, 2, 2, ["Weapon"], [], TfGearAbilities.SeppunEffect);
        Shape(TigerClaw, CardType.Item, "Tiger Claw", 5, 2, 0, 1, ["Weapon"], [TfGearAbilities.TigerClawAttack], "none");
        Shape(VersatileBlade, CardType.Item, "Versatile Blade", 4, 2, 1, 4, ["Weapon"], [TfGearAbilities.VersatileStraighten, TfGearAbilities.VersatileMove], "none");
        Shape(Conflagration, CardType.Spell, "Conflagration", 2, 0, 0, 2, ["Fire"], [TfGearAbilities.ConflagrationBoost], TfGearAbilities.ConflagrationEffect);
        Shape(HitomisDevotion, CardType.Spell, "Hitomi's Devotion", 2, 0, 0, 3, ["Earth"], [], TfGearAbilities.DevotionEffect);
        Shape(KiyoshisWrath, CardType.Spell, "Kiyoshi's Wrath", 0, 0, 0, 2, [], [TfGearAbilities.KiyoshiBow], "none");
        Shape(SailorsWarning, CardType.Spell, "Sailor's Warning", 0, 0, 0, 1, [], [TfGearAbilities.SailorWarning], "none");
        Shape(StealTheFlame, CardType.Spell, "Steal the Candle's Flame", 0, 0, 0, 3, [], [TfGearAbilities.StealInterrupt, TfGearAbilities.StealAttack], "none");
        Shape(ThunderResounds, CardType.Spell, "The Thunder Resounds", 0, 0, 0, 1, [], [TfGearAbilities.ThunderResoundsCopy], "none");
        Shape(TyphoonSurge, CardType.Spell, "Typhoon Surge", 2, 0, 0, 2, [], [TfGearAbilities.TyphoonMove], "none");
        // The printed text, never Oracle's later wording (Band of Brothers' created Follower is not Ronin; Tiger Claw has no Ninja prefix and no Oracle parenthesis).
        Check(CardCatalog.Get(BandOfBrothers).Text == "After this Follower enters play, target one or two of your Personalities. Create and attach a 1F Follower to each of them.", "Band of Brothers' printed text");
        Check(CardCatalog.Get(TigerClaw).Text == "Battle, Bow this card: Melee 3 Attack.", "Tiger Claw's printed text");
        Check(CardCatalog.Get(Conflagration).Text.Contains("actions he performs") && !CardCatalog.Get(Conflagration).Text.Contains("in this unit"), "Conflagration prints 'actions he performs'");
        Check(CardCatalog.Get(StealTheFlame).Text.Contains("Fire Interrupt") && CardCatalog.Get(StealTheFlame).Text.Contains("Ranged 0 Attack"), "Steal the Candle's Flame prints an Interrupt");
        // Every Reaction/Battle action of the batch is a data entry; the second halves of the sequenced Attack are not on a card.
        foreach (var id in new[] { TfGearAbilities.SailorAttack }) Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
    }

    // ---- Band of Brothers -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Band, CardInstance Host, CardInstance Other, CardInstance Foreign) BandInPlay()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home); var other = Add(s, Lion, 0, Zone.Home); var foreign = Add(s, Lion, 1, Zone.Home);
        var band = Add(s, BandOfBrothers, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Equip(e, 0, band, host, 8));
        return (e, band, host, other, foreign);
    }

    private static void BandOfBrothersScenario()
    {
        var (e, band, host, other, foreign) = BandInPlay();
        Eq(e.State.Cards[band.Id].Zone, Zone.Attached, "the Follower entered play on its Personality");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "its entering-play window is open");
        var offer = Enabled(e, 0, TfGearAbilities.BandCreate, band.Id);
        var hosts = offer.Slots!.Single(x => x.Slot == "hosts");
        Check(hosts.Min == 1 && hosts.Max == 2 && hosts.CardIds.Contains(host.Id) && hosts.CardIds.Contains(other.Id) && !hosts.CardIds.Contains(foreign.Id), "one or two of the player's own Personalities");
        // Refused: three Personalities, a foreign one, and none -- with no state change.
        Reject(e, offer.Command! with { TargetId = null, Selections = [Pick("hosts", host.Id, other.Id, host.Id)] });
        Reject(e, offer.Command! with { TargetId = null, Selections = [Pick("hosts", foreign.Id)] });
        Reject(e, offer.Command! with { TargetId = null, Selections = [Pick("hosts")] });
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == Conscripts), 0, "nothing was created by the refused attempts");
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("hosts", host.Id, other.Id)] });
        var created = e.State.Cards.Values.Where(c => c.DefinitionId == Conscripts).ToArray();
        Eq(created.Length, 2, "one Follower for each chosen Personality");
        Check(created.Any(c => c.AttachedTo == host.Id) && created.Any(c => c.AttachedTo == other.Id) && created.All(c => c.OwnerId == 0 && c.Zone == Zone.Attached), "one attached to each");
        Check(created.All(c => CardCatalog.Get(c.DefinitionId).Force == 1 && (CardCatalog.Get(c.DefinitionId).Keywords ?? []).Length == 0), "a plain 1F Follower, not Ronin");

        // One chosen Personality gets one.
        var (e2, band2, host2, _, _) = BandInPlay();
        Accept(e2, Enabled(e2, 0, TfGearAbilities.BandCreate, band2.Id).Command! with { TargetId = null, Selections = [Pick("hosts", host2.Id)] });
        Eq(e2.State.Cards.Values.Count(c => c.DefinitionId == Conscripts && c.AttachedTo == host2.Id), 1, "a single chosen Personality receives a single Follower");
        Eq(e2.State.Cards.Values.Count(c => c.DefinitionId == Conscripts), 1);
        // It is a Reaction: it may be declined.
        var (e3, _, _, _, _) = BandInPlay();
        Accept(e3, Decline(e3));
        Eq(e3.State.Cards.Values.Count(c => c.DefinitionId == Conscripts), 0, "declining creates nothing");
    }

    // ---- Camel Mounts ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CamelMountsScenario()
    {
        // "Will only attach to a Human" (no Nonhuman keyword), and "This Personality has Cavalry".
        var s = Table(Phase.Action, 0);
        var human = Add(s, Crane, 0, Zone.Home); var beast = Kw(Add(s, Lion, 0, Zone.Home), "Nonhuman");
        var camel = Add(s, CamelMounts, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Equip(e, 0, camel, beast, 6));
        Accept(e, Equip(e, 0, camel, human, 6));
        Check(e.State.Cards[human.Id].GrantedKeywords.Any(g => g.Keyword == "Cavalry"), "the Personality has Cavalry");
        Check(e.State.Cards[beast.Id].GrantedKeywords.All(g => g.Keyword != "Cavalry"), "nobody else does");

        // Home Battle: the unit is at home, an enemy unit holds the battlefield: the move is offered (even with no unit there) and brings the unit in.
        var b = Table(Phase.Battle, 0);
        var rider = Add(b, Crane, 0, Zone.Home); var mount = Attach(b, CamelMounts, rider); Add(b, Lion, 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Enabled(be, 0, TfGearAbilities.CamelMove, mount.Id).Command!);
        Eq(be.State.Cards[rider.Id].Zone, Zone.Battlefield, "the Personality moved to the battlefield"); Eq(be.State.Cards[rider.Id].ProvinceIndex, 0);
        Eq(be.State.Cards[mount.Id].ProvinceIndex, 0, "and the Follower with him");

        // Not opposed there: no enemy unit at the battlefield.
        var n = Table(Phase.Battle, 0);
        var r2 = Add(n, Crane, 0, Zone.Home); var m2 = Attach(n, CamelMounts, r2);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, TfGearAbilities.CamelMove, m2.Id), "it would not be opposed: not offered");
        Reject(ne, Use(ne, 0, TfGearAbilities.CamelMove, m2.Id, performerId: r2.Id));
        // Already there.
        var a = Table(Phase.Battle, 0);
        var r3 = Add(a, Crane, 0, Zone.Battlefield, province: 0); var m3 = Attach(a, CamelMounts, r3); Add(a, Lion, 1, Zone.Battlefield, province: 0);
        var ae = MatchEngine.FromTestState(a);
        Check(!CanUse(ae, 0, TfGearAbilities.CamelMove, m3.Id), "a unit already at the battlefield has nothing to move");
        // A bowed Follower cannot act.
        var w = Table(Phase.Battle, 0);
        var r4 = Add(w, Crane, 0, Zone.Home); var m4 = Attach(w, CamelMounts, r4, bowed: true); Add(w, Lion, 1, Zone.Battlefield, province: 0);
        var we = MatchEngine.FromTestState(w);
        Check(!CanUse(we, 0, TfGearAbilities.CamelMove, m4.Id), "abilities on bowed cards cannot be used");
    }

    // ---- Lookout --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Lookout, CardInstance Bow, CardInstance Archer, CardInstance Present, CardInstance HomeTarget) LookoutTable(bool withLookout = true, bool lookoutAtHome = true)
    {
        var s = Table(Phase.Battle, 1);
        var archer = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var bow = Attach(s, TsuruchiDaikyu, archer);                       // Battle, bow this card: Ranged 5 Attack
        var present = Add(s, Crane, 0, Zone.Battlefield, province: 0);      // the card the announced Attack provisionally names
        var homeTarget = Add(s, Crane, 0, Zone.Home);                       // a card in the Defender's home
        CardInstance? lookout = null;
        if (withLookout)
        {
            var ninja = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Ninja");
            lookout = Attach(s, Lookout, ninja);
        }
        var e = MatchEngine.FromTestState(s);
        return (e, lookout!, bow, archer, present, homeTarget);
    }

    // Passes the announce window on (the reactor used his Reaction; the other side has none) until the announced Attack goes on.
    private static void CloseAnnounceWindow(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "announceReaction" } && guard < 4; guard++) Accept(e, Decline(e));
    }

    private static void LookoutScenario()
    {
        // Without Lookout a card in the Defender's home is no legal target.
        var plain = LookoutTable(withLookout: false);
        Reject(plain.Engine, Use(plain.Engine, 1, "tsuruchi-daikyu-attack", plain.Bow.Id, plain.HomeTarget.Id));

        // With it (the Lookout's Ninja unit stands at the battlefield: presence rule, #1133): announce, react, then choose the home target.
        var (e, lookout, bow, archer, present, homeTarget) = LookoutTable();
        Accept(e, Use(e, 1, "tsuruchi-daikyu-attack", bow.Id, present.Id));
        Eq(e.State.PendingChoice?.Type, "announceReaction", "the announce window is open for the watcher");
        Eq(e.State.PendingChoice?.CardId, lookout.Id, "and it is the Lookout's");
        Accept(e, Enabled(e, 1, TfGearAbilities.LookoutDestroy, lookout.Id).Command!);
        CloseAnnounceWindow(e);
        Check(e.State.Cards[lookout.Id].Zone != Zone.Attached, "the Lookout was destroyed as the cost");
        Eq(e.State.PendingChoice?.Type, "attackTargetSelect", "the Attack's target is selected now");
        Check(e.State.PendingAttackTargetSelection!.Candidates.Contains(homeTarget.Id) && e.State.PendingAttackTargetSelection.Candidates.Contains(present.Id), "the Defender's home card is a legal target");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseAttackTarget" && a.Command?.TargetId == homeTarget.Id).Command!);
        Eq(e.State.Cards[homeTarget.Id].Zone, Zone.Dead, "the Ranged 5 Attack destroyed the card in the Defender's home");
        Eq(e.State.Cards[present.Id].Zone, Zone.Battlefield, "the card at the battlefield was not hit");
        Check(!e.State.AnnouncedAttackModifiers.Any(), "the permission was spent with that Attack");

        // The Attack is "one Ranged Attack": a later Ranged Attack of another action has no such permission.
        var (e2, lookout2, bow2, archer2, present2, home2) = LookoutTable();
        Accept(e2, Use(e2, 1, "tsuruchi-daikyu-attack", bow2.Id, present2.Id));
        Accept(e2, Enabled(e2, 1, TfGearAbilities.LookoutDestroy, lookout2.Id).Command!);
        CloseAnnounceWindow(e2);
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "chooseAttackTarget" && a.Command?.Choice == "none").Command!);
        Reject(e2, Use(e2, 1, "tsuruchi-daikyu-attack", bow2.Id, home2.Id));

        // The window is a choice: declining leaves the Lookout in play and the Attack on its announced target.
        var (e3, lookout3, bow3, archer3, present3, home3) = LookoutTable();
        Accept(e3, Use(e3, 1, "tsuruchi-daikyu-attack", bow3.Id, present3.Id));
        Accept(e3, Decline(e3));
        Eq(e3.State.Cards[lookout3.Id].Zone, Zone.Attached, "declined: still in play");
        Eq(e3.State.Cards[home3.Id].Zone, Zone.Home, "and the home card is untouched");

        // A Melee Attack announces no Ranged Attack: the Lookout is not offered a window.
        var m = Table(Phase.Battle, 1);
        var attacker = Add(m, Lion, 1, Zone.Battlefield, province: 0); var sword = Attach(m, "oracle-l5r-10124", attacker); // Unparalleled Sword: Battle: Melee 4 Attack
        var ninja = Kw(Add(m, Lion, 1, Zone.Battlefield, province: 0), "Ninja"); var lookoutM = Attach(m, Lookout, ninja);
        var victim = Add(m, Crane, 0, Zone.Battlefield, province: 0);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Use(me, 1, "unparalleled-sword-attack", sword.Id, victim.Id));
        Check(me.State.PendingChoice?.Type != "announceReaction", "no announce window for a Melee Attack");
        Eq(me.State.Cards[lookoutM.Id].Zone, Zone.Attached, "the Lookout is untouched");
    }

    // ---- Lost Blade of the Maharaja ---------------------------------------------------------------------------------------------------------------------------------------

    private static void LostBladeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0);                 // Force 3, +3 from the Blade = 6
        var blade = Attach(s, LostBlade, wielder);
        var weak = Add(s, Crane, 1, Zone.Battlefield, province: 0);                    // 3 <= 6
        var equalForce = Add(s, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(equalForce, 2);   // 4 + 2 = 6 == 6
        var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(strong, 3);          // 7 > 6
        var ally = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(wielder.Id), 6, "wielder Force with the Blade");
        var targets = Enabled(e, 0, TfGearAbilities.LostBladeBow, blade.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && targets.Contains(equalForce.Id) && targets.Contains(ally.Id) && targets.Contains(wielder.Id), "any Personality of Force equal to or lower: enemy, allied and the wielder");
        Check(!targets.Contains(strong.Id), "a stronger Personality is not offered");
        Reject(e, Use(e, 0, TfGearAbilities.LostBladeBow, blade.Id, strong.Id));
        Accept(e, Offer(e, 0, TfGearAbilities.LostBladeBow, blade.Id, equalForce.Id));
        Check(e.State.Cards[equalForce.Id].Bowed, "the target is bowed");
        Check(e.State.Cards[blade.Id].Bowed && !e.State.Cards[wielder.Id].Bowed, "this card (not the wielder) is bowed as the cost");
        Check(!CanUse(e, 0, TfGearAbilities.LostBladeBow, blade.Id), "a bowed Blade cannot act again");

        // Stalwart negates the bow but the cost is still paid.
        var t = Table(Phase.Battle, 0);
        var w2 = Add(t, Crane, 0, Zone.Battlefield, province: 0); var b2 = Attach(t, LostBlade, w2);
        var stalwart = Add(t, "training-stalwart-personality", 1, Zone.Battlefield, province: 0);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, TfGearAbilities.LostBladeBow, b2.Id, stalwart.Id));
        Check(!te.State.Cards[stalwart.Id].Bowed && te.State.Cards[b2.Id].Bowed, "Stalwart negates the first bow; the Blade was still bowed");
    }

    // ---- Ominous Armor ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OminousArmorScenario()
    {
        // Another player's action destroys the armored Personality: the Armor is destroyed instead, the unit (and its other attachments) stays.
        var s = Table(Phase.Battle, 1);
        var wearer = Add(s, Crane, 0, Zone.Battlefield, province: 0); var armor = Attach(s, OminousArmor, wearer); var follower = Attach(s, "oracle-l5r-10893", wearer);
        var e = MatchEngine.FromTestState(s);
        Eq(e.DestroyUnitsForTest(wearer.Id), 0, "nothing was destroyed as a unit");
        Eq(e.State.Cards[wearer.Id].Zone, Zone.Battlefield, "the Personality is still in play");
        Eq(e.State.Cards[armor.Id].Zone, Zone.FateDiscard, "the Armor was destroyed instead");
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "his other attachments are untouched");
        // With the Armor gone the next such destruction takes him.
        e.DestroyUnitsForTest(wearer.Id);
        Eq(e.State.Cards[wearer.Id].Zone, Zone.Dead, "unarmored, he is destroyed");

        // A real Ranged Attack of the other player's: Tsuruchi Daikyu's Ranged 5 would destroy the Force 5 wearer (Force 3 + Armor 2); the Armor goes instead.
        var r = Table(Phase.Battle, 1);
        var archer = Add(r, Lion, 1, Zone.Battlefield, province: 0); var daikyu = Attach(r, TsuruchiDaikyu, archer);
        var shot = Add(r, Crane, 0, Zone.Battlefield, province: 0); var shotArmor = Attach(r, OminousArmor, shot);
        var re = MatchEngine.FromTestState(r);
        Eq(re.CardForceForTest(shot.Id), 5, "Force 3 + the Armor's +2");
        Accept(re, Use(re, 1, "tsuruchi-daikyu-attack", daikyu.Id, shot.Id));
        Eq(re.State.Cards[shot.Id].Zone, Zone.Battlefield, "the Ranged 5 Attack did not destroy him"); Eq(re.State.Cards[shotArmor.Id].Zone, Zone.FateDiscard, "the Armor was destroyed instead");

        // His own player's card is not "another player's card".
        var own = Table(Phase.Battle, 0);
        var w2 = Add(own, Crane, 0, Zone.Battlefield, province: 0); var a2 = Attach(own, OminousArmor, w2);
        var oe = MatchEngine.FromTestState(own);
        oe.DestroyUnitsForTest(w2.Id);
        Eq(oe.State.Cards[w2.Id].Zone, Zone.Dead, "destroyed by his own player's action"); Eq(oe.State.Cards[a2.Id].Zone, Zone.FateDiscard);

        // A battle's casualty sweep is not a card's effect: the armored loser is destroyed with his Armor.
        var b = Table(Phase.Battle, 1);
        var defender = Add(b, Crane, 0, Zone.Battlefield, province: 0); var a3 = Attach(b, OminousArmor, defender);
        Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Pass(be)); Accept(be, Pass(be));
        Eq(be.State.Cards[defender.Id].Zone, Zone.Dead, "the battle resolution destroyed him"); Eq(be.State.Cards[a3.Id].Zone, Zone.FateDiscard, "and the Armor with him");
    }

    // ---- Red Hunger's Fang -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RedHungerScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home); var fang = Add(s, RedHungerFang, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, Equip(e, 0, fang, host, 4));
        Eq(Honor(e, 0), before - 3, "after it enters play, lose 3 Honor");

        var b = Table(Phase.Battle, 0);
        var wielder = Add(b, Crane, 0, Zone.Battlefield, province: 0); var item = Attach(b, RedHungerFang, wielder);
        var enemy = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); var frail = Add(b, Crane, 1, Zone.Battlefield, province: 0); Batch336Support.Force(frail, 0);
        var ally = Add(b, Lion, 0, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        var targets = Enabled(be, 0, TfGearAbilities.RedHungerPoison, item.Id).TargetIds!;
        Check(targets.Contains(enemy.Id) && targets.Contains(frail.Id) && !targets.Contains(ally.Id) && !targets.Contains(wielder.Id), "enemy Personalities only: " + string.Join(",", targets));
        Reject(be, Use(be, 0, TfGearAbilities.RedHungerPoison, item.Id, ally.Id));
        var forceBefore = be.CardForceForTest(enemy.Id); var chiBefore = be.CardChiForTest(enemy.Id);
        Accept(be, Offer(be, 0, TfGearAbilities.RedHungerPoison, item.Id, enemy.Id));
        Eq(be.CardForceForTest(enemy.Id), forceBefore - 1, "-1F"); Eq(be.CardChiForTest(enemy.Id), chiBefore - 1, "-1C");
        var token = be.State.Cards[enemy.Id].Tokens.Single();
        Check(token.Type == "Poison" && token.Stat == "Force" && token.Amount == -1 && token.Stat2 == "Chi" && token.Amount2 == -1, "one -1F/-1C Poison token");
        Check(be.State.Cards[item.Id].Bowed, "this card was bowed");

        // A Personality left with no Chi is destroyed by the penalty.
        var c = Table(Phase.Battle, 0);
        var w3 = Add(c, Crane, 0, Zone.Battlefield, province: 0); var i3 = Attach(c, RedHungerFang, w3);
        var chiOne = Add(c, Crane, 1, Zone.Battlefield, province: 0); chiOne.Modifiers.Add(new("test", "test", "Chi", -2));
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Offer(ce, 0, TfGearAbilities.RedHungerPoison, i3.Id, chiOne.Id));
        Eq(ce.State.Cards[chiOne.Id].Zone, Zone.Dead, "a Personality with 1 Chi is destroyed by the -1C token");
    }

    // ---- Seppun's Blessed Blade ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SeppunScenario()
    {
        var s = Table(Phase.Battle, 0);
        var samurai = Add(s, Crane, 0, Zone.Battlefield, province: 0);                  // Force 3, Personal Honor 2
        var other = Add(s, Lion, 0, Zone.Battlefield, province: 0);                     // Force 4, Personal Honor 2
        var veteran = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); // Force 5, Personal Honor 3
        Attach(s, SeppunBlade, samurai); Attach(s, SeppunBlade, veteran);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalHonorForTest(samurai.Id), 2);
        Eq(e.CardForceForTest(samurai.Id), 3 + 2, "Force 3 plus a modifier equal to his Personal Honor 2");
        Eq(e.CardForceForTest(veteran.Id), 5 + 3, "his Personal Honor 3");
        Eq(e.CardForceForTest(other.Id), 4, "an unarmed Personality is unchanged");
        // It follows the wielder's Personal Honor as it changes, and is 0 while he is dishonorable.
        var t = e.State;
        t.Cards[samurai.Id].Modifiers.Add(new("test", "test", "PersonalHonor", 2));
        var e2 = MatchEngine.FromTestState(t);
        Eq(e2.CardForceForTest(samurai.Id), 3 + 4, "his Personal Honor is now 4");
        var d = e2.State; d.Cards[samurai.Id].Dishonorable = true;
        var e3 = MatchEngine.FromTestState(d);
        Eq(e3.PersonalHonorForTest(samurai.Id), 0, "a dishonorable Personality's Personal Honor is 0");
        Eq(e3.CardForceForTest(samurai.Id), 3, "so the modifier is 0");
    }

    // ---- Tiger Claw ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TigerClawScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Lion, 0, Zone.Battlefield, province: 0); var claw = Attach(s, TigerClaw, wielder);
        var weak = Add(s, Crane, 1, Zone.Battlefield, province: 0);          // Force 3
        var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0);         // Force 4
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest(TfGearAbilities.TigerClawAttack, wielder.Id), 3, "Melee 3");
        var targets = Enabled(e, 0, TfGearAbilities.TigerClawAttack, claw.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && targets.Contains(strong.Id), "any enemy card may be targeted; the Attack compares Force when it resolves: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, TfGearAbilities.TigerClawAttack, claw.Id, weak.Id));
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "the Melee 3 Attack destroyed the Force 3 target");
        Check(e.State.Cards[claw.Id].Bowed && !e.State.Cards[wielder.Id].Bowed, "this card was bowed as the cost");
        Check(!CanUse(e, 0, TfGearAbilities.TigerClawAttack, claw.Id), "a bowed Tiger Claw cannot attack again");
        // Force 4 is beyond Melee 3: the Attack is taken and destroys nothing.
        var h = Table(Phase.Battle, 0);
        var hw = Add(h, Lion, 0, Zone.Battlefield, province: 0); var hc = Attach(h, TigerClaw, hw); var big = Add(h, Lion, 1, Zone.Battlefield, province: 0);
        var he = MatchEngine.FromTestState(h);
        Accept(he, Offer(he, 0, TfGearAbilities.TigerClawAttack, hc.Id, big.Id));
        Eq(he.State.Cards[big.Id].Zone, Zone.Battlefield, "Force 4 is not destroyed by a Melee 3 Attack");
        Check(he.State.Cards[hc.Id].Bowed, "but the cost was paid");
        // A Personality with an attached Follower is not a legal Melee target (the Attack goes through the Follower).
        var f = Table(Phase.Battle, 0);
        var w2 = Add(f, Lion, 0, Zone.Battlefield, province: 0); var c2 = Attach(f, TigerClaw, w2);
        var shielded = Add(f, Crane, 1, Zone.Battlefield, province: 0); var guard = Attach(f, Conscripts, shielded);
        var fe = MatchEngine.FromTestState(f);
        var ft = Enabled(fe, 0, TfGearAbilities.TigerClawAttack, c2.Id).TargetIds!;
        Check(ft.Contains(guard.Id) && !ft.Contains(shielded.Id), "the Follower, not the Personality it shields");
    }

    // ---- Versatile Blade -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void VersatileBladeScenario()
    {
        // Battle: discard a card to straighten this Personality.
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, bowed: true, province: 0); var blade = Attach(s, VersatileBlade, wielder);
        var discard = Add(s, Lion, 0, Zone.Hand);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, TfGearAbilities.VersatileStraighten, blade.Id);
        Check(offer.TargetIds!.SequenceEqual([discard.Id]), "the card to discard is chosen from the hand");
        Accept(e, Offer(e, 0, TfGearAbilities.VersatileStraighten, blade.Id, discard.Id));
        Check(!e.State.Cards[wielder.Id].Bowed, "the Personality is straightened");
        Eq(e.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the discarded card is in the discard pile");
        Check(!e.State.Players[0].Hand.Contains(discard.Id), "and not in the hand");
        // With no card to discard there is no action.
        var t = Table(Phase.Battle, 0);
        var w2 = Add(t, Crane, 0, Zone.Battlefield, bowed: true, province: 0); var b2 = Attach(t, VersatileBlade, w2);
        var te = MatchEngine.FromTestState(t);
        Check(!CanUse(te, 0, TfGearAbilities.VersatileStraighten, b2.Id), "no card in hand: not offered");
        Reject(te, Use(te, 0, TfGearAbilities.VersatileStraighten, b2.Id));
        Check(te.State.Cards[w2.Id].Bowed, "the refusal changed nothing");

        // Home Battle: if he would be opposed, discard a card to move him to the current battlefield (from home).
        var h = Table(Phase.Battle, 0);
        var home = Add(h, Crane, 0, Zone.Home); var hb = Attach(h, VersatileBlade, home); var card = Add(h, Lion, 0, Zone.Hand);
        Add(h, Lion, 1, Zone.Battlefield, province: 0);
        var he = MatchEngine.FromTestState(h);
        Accept(he, Offer(he, 0, TfGearAbilities.VersatileMove, hb.Id, card.Id));
        Eq(he.State.Cards[home.Id].Zone, Zone.Battlefield, "he moved to the battlefield"); Eq(he.State.Cards[home.Id].ProvinceIndex, 0);
        Eq(he.State.Cards[hb.Id].ProvinceIndex, 0, "the Blade with him");
        Eq(he.State.Cards[card.Id].Zone, Zone.FateDiscard, "the card was discarded");
        // Not opposed: nothing is moved and no card is discarded.
        var n = Table(Phase.Battle, 0);
        var home2 = Add(n, Crane, 0, Zone.Home); var nb = Attach(n, VersatileBlade, home2); var card2 = Add(n, Lion, 0, Zone.Hand);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, TfGearAbilities.VersatileMove, nb.Id), "no enemy unit there: not offered");
        Reject(ne, Use(ne, 0, TfGearAbilities.VersatileMove, nb.Id, card2.Id, performerId: home2.Id));
        Eq(ne.State.Cards[card2.Id].Zone, Zone.Hand, "the card stayed in hand");
    }

    // ---- Conflagration ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ConflagrationScenario()
    {
        var s = Table(Phase.Battle, 0);
        var fire = Add(s, "training-seeds5-fire-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(s, Conflagration, fire);
        var water = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0); var spell2 = Attach(s, Conflagration, water);
        var plainSpell = Add(s, StealTheFlame, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        // "While this Shugenja is Fire, Ranged Attacks from actions he performs have +1 strength."
        Eq(e.RangedStrengthForTest(TfGearAbilities.StealAttack, 0, fire.Id), 1, "Ranged 0, +1 for a Fire Shugenja with Conflagration");
        Eq(e.RangedStrengthForTest(TfGearAbilities.StealAttack, 0, water.Id), 0, "a non-Fire Shugenja gets nothing");
        Eq(e.RangedStrengthForTest("tsuruchi-daikyu-attack", 0, fire.Id), 6, "any Ranged Attack he performs");
        Eq(e.RangedStrengthForTest(TfGearAbilities.TigerClawAttack, 0, fire.Id), 3, "never a Melee Attack");
        // Battle: Bow this card: Give this Shugenja +3F.
        var before = e.CardForceForTest(fire.Id);
        Accept(e, Enabled(e, 0, TfGearAbilities.ConflagrationBoost, spell.Id).Command!);
        Eq(e.CardForceForTest(fire.Id), before + 3, "+3F");
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[fire.Id].Bowed, "the Spell is bowed as the cost, not the Shugenja");
        Check(!CanUse(e, 0, TfGearAbilities.ConflagrationBoost, spell.Id), "bowed: not again");
        Check(e.State.Cards[fire.Id].Modifiers.Any(m => m.Source == spell.Id && m.Stat == "Force" && m.Amount == 3), "a modifier from the Spell (a this-turn effect, the engine's default duration)");
        // Two copies stack, and a Fire wielder performing an actual Ranged Attack hits harder.
        var d = Table(Phase.Battle, 0);
        var f2 = Add(d, "training-seeds5-fire-shugenja", 0, Zone.Battlefield, province: 0); Attach(d, Conflagration, f2); Attach(d, Conflagration, f2);
        var de = MatchEngine.FromTestState(d);
        Eq(de.RangedStrengthForTest(TfGearAbilities.StealAttack, 0, f2.Id), 2, "each Conflagration adds +1");
    }

    // ---- Hitomi's Devotion -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HitomisDevotionScenario()
    {
        // Defending: the Shugenja (Force 1) loses to the attacker, and his unit survives the resolution.
        var d = Table(Phase.Battle, 1);
        var shugenja = Add(d, "training-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(d, HitomisDevotion, shugenja); var follower = Attach(d, Conscripts, shugenja);
        var bare = Add(d, Crane, 0, Zone.Battlefield, province: 0);
        Add(d, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(d);
        Accept(e, Pass(e)); Accept(e, Pass(e));
        Check(e.State.Cards[shugenja.Id].Zone != Zone.Dead, "the defending Shugenja was not destroyed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "his Spell stays"); Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "and his Follower");
        Eq(e.State.Cards[bare.Id].Zone, Zone.Dead, "the other defender was destroyed");

        // The same Shugenja without the Spell is destroyed.
        var c = Table(Phase.Battle, 1);
        var plain = Add(c, "training-shugenja", 0, Zone.Battlefield, province: 0); Add(c, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Pass(ce)); Accept(ce, Pass(ce));
        Eq(ce.State.Cards[plain.Id].Zone, Zone.Dead, "control: without the Spell he is destroyed");

        // Attacking and losing: a non-Earth Shugenja is destroyed, an Earth one is not.
        foreach (var earth in new[] { false, true })
        {
            var a = Table(Phase.Battle, 1);
            var attacker = Add(a, "training-shugenja", 1, Zone.Battlefield, province: 0); if (earth) Kw(attacker, "Earth");
            var devotion = Attach(a, HitomisDevotion, attacker);
            var mate = Add(a, Crane, 1, Zone.Battlefield, province: 0);
            Add(a, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var ae = MatchEngine.FromTestState(a);
            Accept(ae, Pass(ae)); Accept(ae, Pass(ae));
            Eq(ae.State.Cards[attacker.Id].Zone != Zone.Dead, earth, earth ? "an Earth Shugenja attacking is spared" : "a non-Earth Shugenja attacking is destroyed");
            Eq(ae.State.Cards[mate.Id].Zone, Zone.Dead, "his unprotected ally is destroyed");
            if (earth) Eq(ae.State.Cards[devotion.Id].Zone, Zone.Attached, "his Spell stays");
        }
        // A tie destroys both armies except the protected unit.
        var t = Table(Phase.Battle, 1);
        var tied = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0); Attach(t, HitomisDevotion, tied); Batch336Support.Force(tied, 3); // Force 4
        Add(t, Lion, 1, Zone.Battlefield, province: 0);                                                                                       // Force 4
        var te = MatchEngine.FromTestState(t);
        Accept(te, Pass(te)); Accept(te, Pass(te));
        Check(te.State.Cards[tied.Id].Zone != Zone.Dead, "a protected defender survives a tie");
    }

    // ---- Kiyoshi's Wrath -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KiyoshisWrathScenario()
    {
        var s = Table(Phase.Battle, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(s, KiyoshisWrath, shugenja);
        var lion = Add(s, Lion, 1, Zone.Battlefield, province: 0);                     // Force 4
        var strong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);    // Force 6
        var guardHost = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); var guard = Attach(s, Conscripts, guardHost); // an enemy Follower, Force 1
        var ally = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, TfGearAbilities.KiyoshiBow, spell.Id).TargetIds!;
        Check(targets.Contains(lion.Id) && targets.Contains(guard.Id), "an enemy Personality or Follower with 4 or lower Force");
        Check(!targets.Contains(strong.Id) && !targets.Contains(guardHost.Id) && !targets.Contains(ally.Id), "not a stronger one, nor an own card: " + string.Join(",", targets));
        Reject(e, Use(e, 0, TfGearAbilities.KiyoshiBow, spell.Id, strong.Id));
        Reject(e, Use(e, 0, TfGearAbilities.KiyoshiBow, spell.Id, ally.Id));
        Accept(e, Offer(e, 0, TfGearAbilities.KiyoshiBow, spell.Id, lion.Id));
        Check(e.State.Cards[lion.Id].Bowed, "the target is bowed");
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed, "this card is bowed as the cost");
        // The Force is judged as it stands: a penalty brings a stronger one under 4.
        var t = Table(Phase.Battle, 0);
        var sh = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0); var sp = Attach(t, KiyoshisWrath, sh);
        var big = Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Batch336Support.Force(big, -2);
        var te = MatchEngine.FromTestState(t);
        Check(Enabled(te, 0, TfGearAbilities.KiyoshiBow, sp.Id).TargetIds!.Contains(big.Id), "Force 6 - 2 = 4 qualifies");
        Check(!CanUse(e, 0, TfGearAbilities.KiyoshiBow, spell.Id), "a bowed Spell cannot act again");
        // An enemy Follower is bowed too.
        var f = Table(Phase.Battle, 0);
        var fsh = Add(f, "training-shugenja", 0, Zone.Battlefield, province: 0); var fsp = Attach(f, KiyoshisWrath, fsh);
        var fhost = Add(f, "training-crane-veteran", 1, Zone.Battlefield, province: 0); var fguard = Attach(f, Conscripts, fhost);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Offer(fe, 0, TfGearAbilities.KiyoshiBow, fsp.Id, fguard.Id));
        Check(fe.State.Cards[fguard.Id].Bowed && !fe.State.Cards[fhost.Id].Bowed, "the Follower alone is bowed");
    }

    // ---- Sailor's Warning ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Spell, CardInstance Shugenja) SailorTable(Action<MatchState, CardInstance> extra)
    {
        var s = Table(Phase.Battle, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(s, SailorsWarning, shugenja);
        extra(s, shugenja);
        return (MatchEngine.FromTestState(s), spell, shugenja);
    }

    private static void SailorsWarningScenario()
    {
        CardInstance samurai = null!, vet = null!, target3 = null!, target4 = null!, foreign = null!;
        var (e, spell, _) = SailorTable((s, _) =>
        {
            vet = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);      // Chi 4
            samurai = Add(s, Crane, 0, Zone.Battlefield, province: 0);                      // Chi 3
            target3 = Add(s, Crane, 1, Zone.Battlefield, province: 0);                      // Force 3
            target4 = Add(s, Lion, 1, Zone.Battlefield, province: 0);                       // Force 4
            foreign = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        });
        var offer = Enabled(e, 0, TfGearAbilities.SailorWarning, spell.Id);
        var slot = offer.Slots!.Single(x => x.Slot == "personality");
        Check(slot.CardIds.Contains(vet.Id) && slot.CardIds.Contains(samurai.Id) && !slot.CardIds.Contains(foreign.Id), "your Personality only");
        Reject(e, offer.Command! with { TargetId = null, Selections = [Pick("personality", foreign.Id)] });
        Reject(e, offer.Command! with { TargetId = null, Selections = [Pick("personality")] });
        Check(!e.State.Cards[spell.Id].Bowed, "the refused attempts paid nothing");
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("personality", samurai.Id)] });
        Eq(e.State.PendingChoice?.Type, "sequencedTarget", "the Attack's target is chosen after the Personality is named");
        Check(e.State.Cards[spell.Id].Zone != Zone.Attached, "the Spell was destroyed");
        Check(e.State.Cards[spell.Id].Bowed, "after its bow cost");
        var candidates = e.State.PendingSequencedTarget!.Candidates;
        Check(candidates.Contains(target3.Id) && candidates.Contains(target4.Id), "the enemy cards are offered as the Attack's target");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseSequencedTarget" && a.Command?.TargetId == target3.Id).Command!);
        Eq(e.State.Cards[target3.Id].Zone, Zone.Dead, "Ranged 3 (his Chi) destroyed a Force 3 card");

        // The strength is the named Personality's Chi: Chi 3 does not destroy Force 4, Chi 4 does.
        foreach (var (named, destroyed) in new[] { ("samurai", false), ("vet", true) })
        {
            CardInstance who = null!, victim = null!;
            var (te, sp, _) = SailorTable((s, _) =>
            {
                who = named == "vet" ? Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0) : Add(s, Crane, 0, Zone.Battlefield, province: 0);
                victim = Add(s, Lion, 1, Zone.Battlefield, province: 0);
            });
            Accept(te, Enabled(te, 0, TfGearAbilities.SailorWarning, sp.Id).Command! with { TargetId = null, Selections = [Pick("personality", who.Id)] });
            Accept(te, te.QueryActions(0).Single(a => a.Type == "chooseSequencedTarget" && a.Command?.TargetId == victim.Id).Command!);
            Eq(te.State.Cards[victim.Id].Zone == Zone.Dead, destroyed, $"naming a Chi {te.CardChiForTest(who.Id)} Personality against Force 4");
        }
        // An Attack may be taken with no target, and the wielder may be the Personality named.
        CardInstance bystander = null!;
        var (ue, usp, ush) = SailorTable((s, _) => bystander = Add(s, Crane, 1, Zone.Battlefield, province: 0));
        Accept(ue, Enabled(ue, 0, TfGearAbilities.SailorWarning, usp.Id).Command! with { TargetId = null, Selections = [Pick("personality", ush.Id)] });
        Accept(ue, ue.QueryActions(0).Single(a => a.Type == "chooseSequencedTarget" && a.Command?.Choice == "none").Command!);
        Eq(ue.State.Cards[bystander.Id].Zone, Zone.Battlefield, "no target, nothing destroyed");
        Check(ue.State.Cards[usp.Id].Zone != Zone.Attached, "but the Spell is destroyed all the same");
    }

    // ---- Steal the Candle's Flame ----------------------------------------------------------------------------------------------------------------------------------------------

    private static void StealTheFlameScenario()
    {
        // Fire Battle: Ranged 0 Attack.
        var s = Table(Phase.Battle, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(s, StealTheFlame, shugenja);
        var drained = Add(s, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(drained, -4);   // Force 0
        var normal = Add(s, Crane, 1, Zone.Battlefield, province: 0);                                         // Force 3
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest(TfGearAbilities.StealAttack, shugenja.Id), 0, "Ranged 0");
        var targets = Enabled(e, 0, TfGearAbilities.StealAttack, spell.Id).TargetIds!;
        Check(targets.Contains(drained.Id) && targets.Contains(normal.Id), "either may be targeted; Force is compared when the Attack resolves: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, TfGearAbilities.StealAttack, spell.Id, normal.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Spell's own Interrupt could react to its Attack, so the window opens");
        Eq(e.State.PendingAttack!.Strength, 0, "a Ranged 0 Attack");
        for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" } && guard < 4; guard++) Accept(e, Decline(e));
        Eq(e.State.Cards[normal.Id].Zone, Zone.Battlefield, "Force 3 is beyond strength 0");
        // The Spell's own Interrupt (or Conflagration's +1) is what gives a Ranged 0 Attack its edge: +2 destroys a Force 2 card.
        var u = Table(Phase.Battle, 0);
        var ush = Add(u, "training-shugenja", 0, Zone.Battlefield, province: 0); var usp = Attach(u, StealTheFlame, ush);
        var weak = Add(u, Crane, 1, Zone.Battlefield, province: 0); Batch336Support.Force(weak, -1);        // Force 2
        Add(u, "training-attack-reactor", 0, Zone.Home); // a second Reaction keeps the window open once the Interrupt is played, so the strength can be read (issue #644)
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Offer(ue, 0, TfGearAbilities.StealAttack, usp.Id, weak.Id));
        Accept(ue, Enabled(ue, 0, TfGearAbilities.StealInterrupt, usp.Id).Command!);
        Eq(ue.State.PendingAttack!.Strength, 2, "0 + 2");
        for (var guard = 0; ue.State.PendingChoice is { Type: "attackReaction" } && guard < 4; guard++) Accept(ue, Decline(ue));
        Eq(ue.State.Cards[weak.Id].Zone, Zone.Dead, "the boosted Attack destroyed the Force 2 card");
        Check(ue.State.Cards[usp.Id].Bowed, "the Interrupt's bow cost was paid");

        // Fire Interrupt: bow this card, a pending Melee or Ranged Attack gets +2 strength.
        var t = Table(Phase.Battle, 1);
        var archer = Add(t, Lion, 1, Zone.Battlefield, province: 0); var bow = Attach(t, TsuruchiDaikyu, archer);
        var guarded = Add(t, Crane, 0, Zone.Battlefield, province: 0); var sh = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0); var sp = Attach(t, StealTheFlame, sh);
        Add(t, "training-attack-reactor", 0, Zone.Home); // a second Reaction keeps the window open once the Interrupt is played, so the strength can be read (issue #644)
        var te = MatchEngine.FromTestState(t);
        Accept(te, Use(te, 1, "tsuruchi-daikyu-attack", bow.Id, guarded.Id));
        Eq(te.State.PendingChoice?.Type, "attackReaction", "the Attack is pending");
        Eq(te.State.PendingAttack!.Strength, 5);
        var interrupt = Enabled(te, 0, TfGearAbilities.StealInterrupt, sp.Id);
        Accept(te, interrupt.Command!);
        Eq(te.State.PendingAttack!.Strength, 7, "+2 strength");
        Check(te.State.Cards[sp.Id].Bowed, "this card was bowed");
        Check(!CanUse(te, 0, TfGearAbilities.StealInterrupt, sp.Id), "it cannot be used again");
        // Outside an Attack there is nothing to give strength to.
        var o = Table(Phase.Battle, 0);
        var osh = Add(o, "training-shugenja", 0, Zone.Battlefield, province: 0); var osp = Attach(o, StealTheFlame, osh);
        var oe = MatchEngine.FromTestState(o);
        Reject(oe, Use(oe, 0, TfGearAbilities.StealInterrupt, osp.Id));
        Check(!oe.State.Cards[osp.Id].Bowed, "refused with no cost paid");
    }

    // ---- The Thunder Resounds --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ThunderResoundsScenario()
    {
        var s = Table(Phase.Battle, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var resounds = Attach(s, ThunderResounds, shugenja);
        var fire = Add(s, "training-seeds5-fire-shugenja", 1, Zone.Battlefield, province: 0); var tempest = Attach(s, TempestOfFlame, fire);   // a Spell at this battlefield
        var homeThunder = Add(s, "training-shugenja", 1, Zone.Home); var favor = Attach(s, DragonsFavor, homeThunder);                         // a Thunder Spell at home
        var homePlain = Add(s, "training-seeds5-fire-shugenja", 1, Zone.Home); var homeTempest = Attach(s, TempestOfFlame, homePlain);          // a non-Thunder Spell at home
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, TfGearAbilities.ThunderResoundsCopy, resounds.Id).TargetIds!;
        Check(targets.Contains(tempest.Id), "a Spell at this battlefield");
        Check(targets.Contains(favor.Id), "a Thunder Spell at any location");
        Check(!targets.Contains(homeTempest.Id), "a non-Thunder Spell at home is not a target");
        Check(!targets.Contains(resounds.Id), "never itself");
        Reject(e, Use(e, 0, TfGearAbilities.ThunderResoundsCopy, resounds.Id, homeTempest.Id));
        Accept(e, Offer(e, 0, TfGearAbilities.ThunderResoundsCopy, resounds.Id, tempest.Id));
        var granted = e.State.Cards[resounds.Id].GrantedAbilities.Single();
        Eq(granted.AbilityId, "tempest-of-flame-attack"); Eq(granted.Duration, GrantDuration.Turn); Eq(granted.CopiedFromName, "Tempest of Flame");
        Check(e.AbilityIdsForTest(resounds.Id).Contains("tempest-of-flame-attack"), "the Spell now has the copied ability");
        // ...and a Thunder Spell anywhere gives its own.
        var t = Table(Phase.Battle, 0);
        var sh = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0); var rs = Attach(t, ThunderResounds, sh);
        var home = Add(t, "training-shugenja", 1, Zone.Home); var fv = Attach(t, DragonsFavor, home);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, TfGearAbilities.ThunderResoundsCopy, rs.Id, fv.Id));
        Eq(te.State.Cards[rs.Id].GrantedAbilities.Single().AbilityId, "dragons-favor-attack", "a Thunder Spell at home is copied");
        // The grant ends with the turn.
        Eq(te.State.Cards[rs.Id].GrantedAbilities.Single().Duration, GrantDuration.Turn, "recorded as a this-turn grant (the turn-end sweep is the copy primitive's)");
    }

    // ---- Typhoon Surge ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TyphoonSurgeScenario()
    {
        // An enemy Personality at home is moved to the battlefield, bowed and straightened with his unit.
        var s = Table(Phase.Battle, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(s, TyphoonSurge, shugenja);
        var enemy = Add(s, Lion, 1, Zone.Home, bowed: true); var enemyFollower = Attach(s, Conscripts, enemy, bowed: true); enemyFollower.ProvinceIndex = null;
        var own = Add(s, Crane, 0, Zone.Home, bowed: true);
        var item = Add(s, "oracle-l5r-10124", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, TfGearAbilities.TyphoonMove, spell.Id).TargetIds!;
        Check(targets.Contains(enemy.Id) && targets.Contains(own.Id) && !targets.Contains(item.Id), "a Personality at any location, either player's: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, TfGearAbilities.TyphoonMove, spell.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "moved to the battlefield"); Eq(e.State.Cards[enemy.Id].ProvinceIndex, 0);
        Check(!e.State.Cards[enemy.Id].Bowed && !e.State.Cards[enemyFollower.Id].Bowed, "his unit was straightened because the acting player does not control him");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Spell was destroyed");

        // Your own Personality is moved but not straightened.
        var t = Table(Phase.Battle, 0);
        var sh = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0); var sp = Attach(t, TyphoonSurge, sh);
        var mine = Add(t, Crane, 0, Zone.Home, bowed: true);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, TfGearAbilities.TyphoonMove, sp.Id, mine.Id));
        Eq(te.State.Cards[mine.Id].Zone, Zone.Battlefield, "moved");
        Check(te.State.Cards[mine.Id].Bowed, "your own Personality stays bowed");

        // An Absent Battle action: a player with no unit at the battlefield may take it (the standing move-to-battle ruling).
        var u = Table(Phase.Battle, 0);
        var away = Add(u, "training-shugenja", 0, Zone.Home); var spellAway = Attach(u, TyphoonSurge, away);
        var other = Add(u, Crane, 0, Zone.Home); var notAPersonality = Add(u, "oracle-l5r-10124", 0, Zone.Home);
        var ue = MatchEngine.FromTestState(u);
        Check(CanUse(ue, 0, TfGearAbilities.TyphoonMove, spellAway.Id), "offered without a unit at the battlefield: " + string.Join("; ", ue.QueryActions(0).Where(a => a.Type == "useAbility").Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        Reject(ue, Use(ue, 0, TfGearAbilities.TyphoonMove, spellAway.Id, notAPersonality.Id));
        Accept(ue, Offer(ue, 0, TfGearAbilities.TyphoonMove, spellAway.Id, other.Id));
        Eq(ue.State.Cards[other.Id].Zone, Zone.Battlefield, "an Absent action moved him in");
    }

    // ---- negation --------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Every action of the batch declares its costs as data, so a whole-action negation (NegateEveryAction) leaves the cost paid and skips every effect.
    private static void NegatedActionsPayTheirCosts()
    {
        MatchEngine Negating(MatchState s) { var e = MatchEngine.FromTestState(s); e.NegateEveryAction = true; return e; }

        // Kiyoshi's Wrath, Lost Blade, Tiger Claw, Red Hunger's Fang: the card is bowed, nothing happens to the target.
        foreach (var (ability, def, host) in new[] { (TfGearAbilities.KiyoshiBow, KiyoshisWrath, "training-shugenja"), (TfGearAbilities.LostBladeBow, LostBlade, Crane),
            (TfGearAbilities.TigerClawAttack, TigerClaw, Crane), (TfGearAbilities.RedHungerPoison, RedHungerFang, Crane) })
        {
            var s = Table(Phase.Battle, 0);
            var wielder = Add(s, host, 0, Zone.Battlefield, province: 0); var card = Attach(s, def, wielder);
            var target = Add(s, Crane, 1, Zone.Battlefield, province: 0);
            var e = Negating(s);
            Accept(e, Offer(e, 0, ability, card.Id, target.Id));
            Check(e.State.Cards[card.Id].Bowed, $"{ability}: the bow was paid");
            Check(!e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].Zone == Zone.Battlefield && e.State.Cards[target.Id].Tokens.Count == 0, $"{ability}: no effect happened");
        }

        // Conflagration: bowed, no +3F.
        var c = Table(Phase.Battle, 0);
        var fire = Add(c, "training-seeds5-fire-shugenja", 0, Zone.Battlefield, province: 0); var conflagration = Attach(c, Conflagration, fire);
        var ce = Negating(c); var force = ce.CardForceForTest(fire.Id);
        Accept(ce, Enabled(ce, 0, TfGearAbilities.ConflagrationBoost, conflagration.Id).Command!);
        Check(ce.State.Cards[conflagration.Id].Bowed, "Conflagration is bowed"); Eq(ce.CardForceForTest(fire.Id), force, "and gives no Force");

        // Versatile Blade: the card is discarded, the wielder stays bowed.
        var v = Table(Phase.Battle, 0);
        var vw = Add(v, Crane, 0, Zone.Battlefield, bowed: true, province: 0); var blade = Attach(v, VersatileBlade, vw); var discard = Add(v, Lion, 0, Zone.Hand);
        var ve = Negating(v);
        Accept(ve, Offer(ve, 0, TfGearAbilities.VersatileStraighten, blade.Id, discard.Id));
        Eq(ve.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the discard was paid"); Check(ve.State.Cards[vw.Id].Bowed, "he was not straightened");

        // Lookout: destroyed as the cost, and the Attack gains no permission.
        var l = Table(Phase.Battle, 1);
        var archer = Add(l, Lion, 1, Zone.Battlefield, province: 0); var bow = Attach(l, TsuruchiDaikyu, archer);
        var present = Add(l, Crane, 0, Zone.Battlefield, province: 0); var homeCard = Add(l, Crane, 0, Zone.Home);
        var ninja = Kw(Add(l, Lion, 1, Zone.Battlefield, province: 0), "Ninja"); var lookout = Attach(l, Lookout, ninja);
        var le = MatchEngine.FromTestState(l);
        Accept(le, Use(le, 1, "tsuruchi-daikyu-attack", bow.Id, present.Id));
        le.NegateEveryAction = true;
        Accept(le, Enabled(le, 1, TfGearAbilities.LookoutDestroy, lookout.Id).Command!);
        Check(le.State.Cards[lookout.Id].Zone != Zone.Attached, "the Lookout was destroyed");
        Check(!le.State.AnnouncedAttackModifiers.Any(), "a negated Lookout grants nothing");

        // Typhoon Surge: nothing moves and the Spell is not destroyed (its destruction is an effect, not a cost).
        var t = Table(Phase.Battle, 0);
        var tsh = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0); var spell = Attach(t, TyphoonSurge, tsh); var far = Add(t, Lion, 1, Zone.Home, bowed: true);
        var te = Negating(t);
        Accept(te, Offer(te, 0, TfGearAbilities.TyphoonMove, spell.Id, far.Id));
        Eq(te.State.Cards[far.Id].Zone, Zone.Home, "he did not move"); Check(te.State.Cards[far.Id].Bowed, "nor straighten");
        Eq(te.State.Cards[spell.Id].Zone, Zone.Attached, "and the Spell is still there");

        // A bowed Lookout cannot react at all.
        var b = Table(Phase.Battle, 1);
        var barcher = Add(b, Lion, 1, Zone.Battlefield, province: 0); var bbow = Attach(b, TsuruchiDaikyu, barcher); var btarget = Add(b, Crane, 0, Zone.Battlefield, province: 0);
        var bninja = Kw(Add(b, Lion, 1, Zone.Battlefield, province: 0), "Ninja"); Attach(b, Lookout, bninja, bowed: true);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Use(be, 1, "tsuruchi-daikyu-attack", bbow.Id, btarget.Id));
        Check(be.State.PendingChoice?.Type != "announceReaction", "a bowed Lookout opens no announce window");
    }
}
