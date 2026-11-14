using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group G: item-wave-15 (Armor of the Heavens, Blade of Perfection, Chrysanthemum Blossom, Clan Standard, Gift Armor, Kensai's Blade,
// Maga-yari, Modifications, Reserve Weapon, Sasumata, Tsuruchi Daikyu) and follower-wave-31 (Bandit Gang, Ikiryo, Ikoma House Guard, Kaiu House Guard, Legion of Pain, Moto House Guard, Tamori
// House Guard, Tsuruchi House Guard), driven through QueryActions and the commands it offers on explicit positions of the economy table (player 0 the Crane, player 1 the Lion).
internal static class EmperorRemainingGTests
{
    private const string MichitsuId = "oracle-l5r-10060", Armor = "oracle-l5r-446", Blade = "oracle-l5r-1010", Blossom = "oracle-l5r-1340", Standard = "oracle-l5r-1407", GiftArmor = "oracle-l5r-2798", KensaiBlade = "oracle-l5r-4323",
        MagaYari = "oracle-l5r-4790", Modifications = "oracle-l5r-5214", ReserveWeapon = "oracle-l5r-6269", Sasumata = "oracle-l5r-6478", Daikyu = "oracle-l5r-8823",
        Bandit = "oracle-l5r-683", Ikiryo = "oracle-l5r-3589", IkomaGuard = "oracle-l5r-3611", KaiuGuard = "oracle-l5r-4118", Legion = "oracle-l5r-4699", MotoGuard = "oracle-l5r-5329",
        TamoriGuard = "oracle-l5r-7776", TsuruchiGuard = "oracle-l5r-8837";
    private static readonly string[] Items = [Armor, Blade, Blossom, Standard, GiftArmor, KensaiBlade, MagaYari, Modifications, ReserveWeapon, Sasumata, Daikyu];
    private static readonly string[] Followers = [Bandit, Ikiryo, IkomaGuard, KaiuGuard, Legion, MotoGuard, TamoriGuard, TsuruchiGuard];

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-G", [
        ("Published shape: item-wave-15 and follower-wave-31 publish exactly the 19 wired cards with their printed stats, keywords, Effect and ability ids, ship their art and list them in their manifests", PublishedShape),
        ("Tsuruchi Daikyu: bows to make a Ranged 5 Attack -- Force 5 dies, Force 6 is a legal target that survives, and it cannot be used again while bowed", DaikyuAttack),
        ("Modifications: a Battle action that draws a card", ModificationsDraw),
        ("Sasumata: bows itself to bow an enemy card without a Weapon or Armor -- a Personality with either is not a target, an attachment and a Follower are", SasumataBow),
        ("Tsuruchi House Guard: Ranged 5 Attack compared against Force or Gold Cost, the players choice (ruling R5)", TsuruchiGuardAttack),
        ("Tamori House Guard: an enemy Personality or Follower gets -4F and may not perform actions this turn", TamoriGuardStone),
        ("Ikoma House Guard: Melee Attack with strength equal to its Personality's Personal Honor plus two, and 1 Honor when it destroyed a card", IkomaGuardAttack),
        ("Kaiu House Guard: destroys an enemy card without attachments whose unit has lower total Force than its own, and gains 1 Honor", KaiuGuardDestroy),
        ("Moto House Guard: with enemy units at the battlefield, moves its Personality there and then Ranged 4 Attacks -- also from a Personality that is already there, never without enemy units", MotoGuardMoveAttack),
        ("Ikiryo: destroyed as the cost, its Ranged Attack may target a Personality with Followers, its strength is the Chi of the Personality it was attached to, who is dishonored and costs Honor equal to that Chi", IkiryoAttack),
        ("Legion of Pain: an entry Honor loss, and actions performed by Personalities with a Gold Cost of 7 or lower may not target its Personality (a costlier Personality's, and a Follower's, may)", LegionOfPainBar),
        ("Armor of the Heavens: after another player's action targets its Personality, a Political Reaction lets either player gain or lose 2 Honor -- not for another Personality, nor for the wielder's own player's action", ArmorOfTheHeavens),
        ("Blade of Perfection: straightens after it bows; after another player's action destroyed it or its Personality it attaches from the discard pile to a Personality of yours, paying its cost", BladeOfPerfection),
        ("Clan Standard: your Clan Personalities at its battlefield have +1F, +2F with a Follower -- not at home, not another Clan, not the opponent's", ClanStandard),
        ("Gift Armor: negates Chi penalties current and new, and the destruction of the card itself but not of its Personality", GiftArmorNegations),
        ("Kensai's Blade: +1F while attached to a Kensai, and after engaging it attaches from the hand to a Personality of yours at the battlefield, paying its cost", KensaisBlade),
        ("Maga-yari: 3 less Gold to attach to a Paragon or Kensai, and it will not be transferred", MagaYariRules),
        ("Reserve Weapon: instead of focusing in a duel, +3 to the duel stat until the duel ends", ReserveWeaponDuel),
        ("Reserve Weapon: each substitution is one of the four focus opportunities; at the cap only a Strike is offered", ReserveWeaponCap),
        ("Reserve Weapon: the same cap in the replayable Duel Demo position", ReserveWeaponCapDuelDemo),
        ("Chrysanthemum Blossom: after its Personality is assigned, a Reaction gives him a +1F token and 1 Honor", ChrysanthemumBlossom),
        ("Bandit Gang: 2 Honor on entering play; after a battle's resolution, at the battlefield, its controller pays 2 Gold or it is destroyed", BanditGang),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, province: host.ProvinceIndex, parent: host.Id);
    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void PassTo(MatchEngine e, int player) => Accept(e, new GameCommand("pass", player, e.Version));
    private static bool Gone(MatchEngine e, CardInstance card) => e.State.Cards[card.Id].Zone is Zone.Dead or Zone.FateDiscard or Zone.DynastyDiscard;
    private static void Gold(MatchState s, int owner, int marketplaces = 4) { for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", owner, Zone.Home); }
    private static string[] TargetsOf(MatchEngine e, int player, string ability, string cardId) => Options(e, player, ability, cardId).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Stats(string id, string name, CardType type, int cost, int force, int chi, int? honorRequirement, int focus, string keywords, string effect, string[] abilities, int entryLoss = 0, string? attachesTo = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == type && c.GoldCost == cost && c.Force == force && c.Chi == chi && c.HonorRequirement == honorRequirement && c.Focus == focus && c.Effect == effect
                    && string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)) == keywords && (c.EntryHonorLoss ?? 0) == entryLoss && c.AttachRequiresKeyword == attachesTo,
                $"{c.Name} ({id}): cost={c.GoldCost} force={c.Force} chi={c.Chi} hr={c.HonorRequirement} focus={c.Focus} keywords={string.Join(",", c.Keywords ?? [])} effect={c.Effect} entryLoss={c.EntryHonorLoss} attaches={c.AttachRequiresKeyword}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
            Check(AbilityCatalog.All.ContainsKey(id) == false && abilities.All(AbilityCatalog.All.ContainsKey), $"{c.Name}'s abilities are catalogued");
        }
        Stats(Armor, "Armor of the Heavens", CardType.Item, 4, 3, 0, null, 4, "Armor,Imperial", "none", ["armor-heavens-gain", "armor-heavens-lose"]);
        Stats(Blade, "Blade of Perfection", CardType.Item, 4, 3, 1, null, 3, "Weapon", "blade-perfection-rebound", ["blade-perfection-attach"]);
        Stats(Blossom, "Chrysanthemum Blossom", CardType.Item, 1, 0, 0, null, 4, "Imperial", "none", ["chrysanthemum-blossom-boost"]);
        Stats(Standard, "Clan Standard", CardType.Item, 3, 1, 0, null, 4, "Standard", "clan-standard", []);
        Stats(GiftArmor, "Gift Armor", CardType.Item, 4, 3, 0, null, 2, "Armor", "gift-armor", []);
        Stats(KensaiBlade, "Kensai's Blade", CardType.Item, 3, 2, 1, null, 3, "Weapon", "force-to-kensai-wielder", ["kensais-blade-attach"]);
        Stats(MagaYari, "Maga-yari", CardType.Item, 5, 3, 1, null, 3, "Weapon", "maga-yari", []);
        Stats(Modifications, "Modifications", CardType.Item, 7, 4, 1, null, 2, "Weapon", "none", ["modifications-draw"]);
        Stats(ReserveWeapon, "Reserve Weapon", CardType.Item, 3, 3, 0, null, 3, "Weapon", "reserve-weapon", []);
        Stats(Sasumata, "Sasumata", CardType.Item, 3, 2, 1, null, 3, "Weapon", "none", ["sasumata-bow"]);
        Stats(Daikyu, "Tsuruchi Daikyu", CardType.Item, 7, 5, 0, null, 3, "Weapon", "none", ["tsuruchi-daikyu-attack"]);
        Stats(Bandit, "Bandit Gang", CardType.Follower, 0, 2, 0, 0, 1, "Bandit", "bandit-gang", [], entryLoss: 2);
        Stats(Ikiryo, "Ikiryo", CardType.Follower, 3, 0, 0, 0, 3, "Nonhuman,Shadowlands,Spirit", "none", ["ikiryo-attack"]);
        Stats(IkomaGuard, "Ikoma House Guard", CardType.Follower, 4, 3, 0, 2, 4, "Scout,Unique", "none", ["ikoma-house-guard-attack"], attachesTo: "Lion Clan");
        Stats(KaiuGuard, "Kaiu House Guard", CardType.Follower, 4, 3, 0, 0, 4, "Siege,Unique", "none", ["kaiu-house-guard-destroy"], attachesTo: "Crab Clan");
        Stats(Legion, "Legion of Pain", CardType.Follower, 6, 5, 0, 0, 3, "Lost,Shadowlands", "legion-of-pain-bar", [], entryLoss: 2);
        Stats(MotoGuard, "Moto House Guard", CardType.Follower, 5, 3, 0, 1, 4, "Cavalry,Unique", "none", ["moto-house-guard-attack"], attachesTo: "Unicorn Clan");
        Stats(TamoriGuard, "Tamori House Guard", CardType.Follower, 4, 3, 0, 1, 4, "Earth,Unique", "none", ["tamori-house-guard-stone"], attachesTo: "Dragon Clan");
        Stats(TsuruchiGuard, "Tsuruchi House Guard", CardType.Follower, 4, 3, 0, 0, 4, "Scout,Unique", "none", ["tsuruchi-house-guard-attack"], attachesTo: "Mantis Clan");
        // The waves publish exactly the wired cards, none blank, and their manifests pin them.
        Check(PublishedContent.ItemWave15.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Items.OrderBy(i => i, StringComparer.Ordinal)), "item-wave-15 is exactly the wired Items");
        Check(PublishedContent.FollowerWave31.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Followers.OrderBy(i => i, StringComparer.Ordinal)), "follower-wave-31 is exactly the wired Followers");
        foreach (var card in PublishedContent.ItemWave15.Cards.Concat(PublishedContent.FollowerWave31.Cards)) Check((card.Abilities ?? []).Length > 0 || card.Effect != "none", $"{card.Name} is not blank");
        foreach (var (wave, ids) in new[] { ("item-wave-15", Items), ("follower-wave-31", Followers) })
        {
            var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(listed.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave}'s manifest pins exactly the wired cards");
        }
    }

    // ---- Items with a plain Battle action ----

    private static void DaikyuAttack()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var bow = Attach(s, Daikyu, host);
        var five = Bf(s, "training-lion-samurai", 1); Force(five, 1); // Force 5
        var six = Bf(s, "training-lion-veteran", 1); // Force 6
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "tsuruchi-daikyu-attack", bow.Id).TargetIds!;
        Check(targets.Contains(five.Id) && targets.Contains(six.Id), "Force 6 is above the strength but still a legal target");
        var e6 = MatchEngine.FromTestState(s);
        Accept(e6, Offer(e6, 0, "tsuruchi-daikyu-attack", bow.Id, six.Id));
        Check(!Gone(e6, six) && e6.State.Cards[bow.Id].Bowed, "Force 6 survives a Ranged 5 Attack, and the bow was the cost");
        Accept(e, Offer(e, 0, "tsuruchi-daikyu-attack", bow.Id, five.Id));
        Check(Gone(e, five) && e.State.Cards[bow.Id].Bowed && !Gone(e, six), "Force 5 is destroyed and the bow is bowed");
        PassTo(e, 1);
        Check(!CanUse(e, 0, "tsuruchi-daikyu-attack", bow.Id), "a bowed bow cannot be used again");
    }

    private static void ModificationsDraw()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var weapon = Attach(s, Modifications, host);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "modifications-draw", weapon.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1, "drew a card");
        PassTo(e, 1);
        Check(!CanUse(e, 0, "modifications-draw", weapon.Id), "once per turn");
        // Not in a home army: a Battle action needs its Personality at the battlefield.
        var away = Battle(); var stay = Add(away, "training-crane-samurai", 0, Zone.Home); var w2 = Add(away, Modifications, 0, Zone.Attached, parent: stay.Id); Bf(away, "training-crane-samurai", 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, "modifications-draw", w2.Id), "a Personality at home does not take a Battle action");
    }

    private static void SasumataBow()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var pole = Attach(s, Sasumata, host);
        var bare = Bf(s, "training-lion-samurai", 1);
        var armed = Bf(s, "training-lion-samurai", 1); var weapon = Attach(s, "training-weapon", armed);
        var armored = Bf(s, "training-lion-samurai", 1); Attach(s, "training-armor", armored);
        var spelled = Bf(s, "training-lion-samurai", 1); var spell = Attach(s, "training-spell", spelled);
        var followed = Bf(s, "training-lion-samurai", 1); var follower = Attach(s, "training-follower", followed);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "sasumata-bow", pole.Id).TargetIds!;
        Check(targets.Contains(bare.Id) && targets.Contains(follower.Id) && targets.Contains(spell.Id) && targets.Contains(weapon.Id), "a bare Personality, and a Follower, Spell or Weapon (none carries a Weapon or Armor of its own): " + string.Join(",", targets));
        Check(!targets.Contains(armed.Id) && !targets.Contains(armored.Id), "a Personality with a Weapon or with Armor is not a target");
        Check(!targets.Contains(host.Id), "not its own side");
        Accept(e, Offer(e, 0, "sasumata-bow", pole.Id, bare.Id));
        Check(e.State.Cards[bare.Id].Bowed && e.State.Cards[pole.Id].Bowed && !e.State.Cards[armed.Id].Bowed, "the target bowed, and the Sasumata bowed as the cost");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 0, "sasumata-bow", pole.Id, follower.Id));
        Check(e2.State.Cards[follower.Id].Bowed, "a Follower can be bowed");
    }

    private static void TsuruchiGuardAttack()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var guard = Attach(s, TsuruchiGuard, host);
        var cheap = Bf(s, "training-lion-samurai", 1); Force(cheap, 5); // Force 9, Gold Cost 5
        var dear = Bf(s, "training-lion-veteran", 1); // Force 6, Gold Cost 8
        var weak = Bf(s, "training-crane-courtier", 1); // Force 2, Gold Cost 6
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "tsuruchi-house-guard-attack", guard.Id).TargetIds!;
        Check(targets.Contains(cheap.Id) && targets.Contains(dear.Id) && targets.Contains(weak.Id), "every card in the enemy army is a legal target");
        var e1 = MatchEngine.FromTestState(s);
        Accept(e1, Offer(e1, 0, "tsuruchi-house-guard-attack", guard.Id, dear.Id));
        Check(!Gone(e1, dear), "Force 6 and Gold Cost 8 are both above the strength: it survives");
        var e2 = MatchEngine.FromTestState(s);
        // Issue #492 (ruling R5): "you may compare against Gold Cost" is the player's choice, each way its own listed option.
        Accept(e2, Batch336Support.OfferChoice(e2, 0, "tsuruchi-house-guard-attack", guard.Id, cheap.Id, "gold"));
        Check(Gone(e2, cheap), "compared against its Gold Cost (5), not its Force (9)");
        var e2Force = MatchEngine.FromTestState(s);
        Accept(e2Force, Batch336Support.OfferChoice(e2Force, 0, "tsuruchi-house-guard-attack", guard.Id, cheap.Id, "force"));
        Check(!Gone(e2Force, cheap), "the player chose Force (9): above the strength, it survives");
        Accept(e, Batch336Support.OfferChoice(e, 0, "tsuruchi-house-guard-attack", guard.Id, weak.Id, "force"));
        Check(Gone(e, weak), "compared against its Force (2), not its Gold Cost (6)");
        var eGold = MatchEngine.FromTestState(s);
        Accept(eGold, Batch336Support.OfferChoice(eGold, 0, "tsuruchi-house-guard-attack", guard.Id, weak.Id, "gold"));
        Check(!Gone(eGold, weak), "the player chose Gold Cost (6): above the strength, it survives");
    }

    private static void TamoriGuardStone()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var guard = Attach(s, TamoriGuard, host);
        var victim = Bf(s, "training-lion-veteran", 1); var pole = Attach(s, Sasumata, victim);
        var other = Bf(s, "training-lion-samurai", 1); var follower = Attach(s, "training-follower", other);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "tamori-house-guard-stone", guard.Id).TargetIds!;
        Check(targets.Contains(victim.Id) && targets.Contains(follower.Id) && !targets.Contains(host.Id), "an enemy Personality or Follower: " + string.Join(",", targets));
        var force = e.CardForceForTest(victim.Id);
        Accept(e, Offer(e, 0, "tamori-house-guard-stone", guard.Id, victim.Id));
        Eq(e.CardForceForTest(victim.Id), force - 4, "-4F");
        Check(e.State.Cards[victim.Id].ActionLocks.Count == 1, "it may not perform actions");
        Check(!CanUse(e, 1, "sasumata-bow", pole.Id), "an action of the encased Personality (through his Sasumata) is refused");
        var eF = MatchEngine.FromTestState(s);
        Accept(eF, Offer(eF, 0, "tamori-house-guard-stone", guard.Id, follower.Id));
        Eq(eF.CardForceForTest(follower.Id), 0, "a Follower of Force 2 goes to 0");
        Check(eF.State.Cards[follower.Id].ActionLocks.Count == 1, "and may not perform actions either");
    }

    private static void IkomaGuardAttack()
    {
        var s = Battle(); var host = Bf(s, "training-lion-samurai", 0); var guard = Attach(s, IkomaGuard, host); // Personal Honor 2: strength 4
        var four = Bf(s, "training-lion-samurai", 1); // Force 4
        var six = Bf(s, "training-lion-veteran", 1); // Force 6
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "ikoma-house-guard-attack", guard.Id).TargetIds!;
        Check(targets.Contains(four.Id) && targets.Contains(six.Id), "Force 6 is above the strength but still a legal target");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "ikoma-house-guard-attack", guard.Id, four.Id));
        Check(Gone(e, four), "Melee 4 (Personal Honor 2 plus two) destroyed the Force 4 target");
        Eq(Honor(e, 0), honor + 1, "and gained 1 Honor");
        var e6 = MatchEngine.FromTestState(s);
        Accept(e6, Offer(e6, 0, "ikoma-house-guard-attack", guard.Id, six.Id));
        Check(!Gone(e6, six), "the Force 6 target survives"); Eq(Honor(e6, 0), honor, "and no Honor is gained when nothing was destroyed");
        // A higher Personal Honor raises the strength: +1 makes it 5, so a Force 5 target dies.
        var s5 = Battle(); var host5 = Bf(s5, "training-lion-samurai", 0); host5.Modifiers.Add(new("test-source", "test", "PersonalHonor", 1)); var guard5 = Attach(s5, IkomaGuard, host5);
        var five = Bf(s5, "training-lion-samurai", 1); Force(five, 1);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "ikoma-house-guard-attack", guard5.Id, five.Id));
        Check(Gone(e5, five), "strength follows his live Personal Honor (3 plus two)");
        // Dishonorable, his Personal Honor is 0: strength 2.
        var sd = Battle(); var hostD = Bf(sd, "training-lion-samurai", 0); hostD.Dishonorable = true; var guardD = Attach(sd, IkomaGuard, hostD);
        var three = Bf(sd, "training-crane-samurai", 1); // Force 3
        var ed = MatchEngine.FromTestState(sd);
        Accept(ed, Offer(ed, 0, "ikoma-house-guard-attack", guardD.Id, three.Id));
        Check(!Gone(ed, three), "a dishonorable Personality has Personal Honor 0: Melee 2 does not destroy Force 3");
    }

    private static void KaiuGuardDestroy()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var guard = Attach(s, KaiuGuard, host); // unit Force 3 + 3 = 6
        var bareLow = Bf(s, "training-lion-samurai", 1); // 4 < 6
        var bareEqual = Bf(s, "training-lion-veteran", 1); // 6, not lower
        var courtier = Bf(s, "training-crane-courtier", 1); var lowFollower = Attach(s, "training-follower", courtier); // unit 2 + 2 = 4
        var veteran = Bf(s, "training-crane-veteran", 1); var highFollower = Attach(s, "training-follower", veteran); // unit 5 + 2 = 7
        var armed = Bf(s, "training-crane-samurai", 1); Attach(s, "training-weapon", armed); // has an attachment
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "kaiu-house-guard-destroy", guard.Id).TargetIds!;
        Check(targets.Contains(bareLow.Id) && targets.Contains(lowFollower.Id), "a bare Personality and a Follower whose unit's total Force is lower: " + string.Join(",", targets));
        Check(!targets.Contains(bareEqual.Id) && !targets.Contains(highFollower.Id) && !targets.Contains(armed.Id) && !targets.Contains(courtier.Id) && !targets.Contains(veteran.Id),
            "an equal total, a unit with a higher total and a Personality with attachments are not");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "kaiu-house-guard-destroy", guard.Id, bareLow.Id));
        Check(Gone(e, bareLow), "destroyed"); Eq(Honor(e, 0), honor + 1, "gained 1 Honor");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 0, "kaiu-house-guard-destroy", guard.Id, lowFollower.Id));
        Check(Gone(e2, lowFollower) && e2.State.Cards[courtier.Id].Zone == Zone.Battlefield, "a Follower is destroyed alone");
    }

    private static void MotoGuardMoveAttack()
    {
        // From home: the Personality moves to the battlefield, then Ranged 4 Attacks.
        var s = Battle(); var host = Add(s, "training-crane-samurai", 0, Zone.Home); var guard = Add(s, MotoGuard, 0, Zone.Attached, parent: host.Id);
        var target = Bf(s, "training-crane-samurai", 1); var tough = Bf(s, "training-lion-veteran", 1);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "moto-house-guard-attack", guard.Id).TargetIds!.Contains(tough.Id), "Force 6 is above the strength but still a legal target");
        var eTough = MatchEngine.FromTestState(s);
        Accept(eTough, Offer(eTough, 0, "moto-house-guard-attack", guard.Id, tough.Id));
        Check(eTough.State.Cards[host.Id].Zone == Zone.Battlefield && !Gone(eTough, tough), "he moved, and the Force 6 target survived the Ranged 4 Attack");
        Accept(e, Offer(e, 0, "moto-house-guard-attack", guard.Id, target.Id));
        Check(e.State.Cards[host.Id].Zone == Zone.Battlefield && e.State.Cards[host.Id].ProvinceIndex == 0 && e.State.Cards[guard.Id].ProvinceIndex == 0, "his unit moved to the battlefield");
        Check(Gone(e, target), "and the Ranged 4 Attack destroyed the Force 3 target");
        // Already there: no move needed, "if he is now there" still holds.
        var sh = Battle(); var here = Bf(sh, "training-crane-samurai", 0); var g2 = Attach(sh, MotoGuard, here); var t2 = Bf(sh, "training-crane-samurai", 1);
        var eh = MatchEngine.FromTestState(sh);
        Accept(eh, Offer(eh, 0, "moto-house-guard-attack", g2.Id, t2.Id));
        Check(Gone(eh, t2), "a Personality already at the battlefield still Attacks");
        // No enemy units at the battlefield: the action is not available.
        var none = Battle(); var h3 = Add(none, "training-crane-samurai", 0, Zone.Home); var g3 = Add(none, MotoGuard, 0, Zone.Attached, parent: h3.Id); Bf(none, "training-crane-samurai", 0);
        Check(!CanUse(MatchEngine.FromTestState(none), 0, "moto-house-guard-attack", g3.Id), "without enemy units at the current battlefield the action is refused");
    }

    private static void IkiryoAttack()
    {
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var ghost = Attach(s, Ikiryo, host); // Chi 3
        var followed = Bf(s, "training-crane-samurai", 1); var shield = Attach(s, "training-follower", followed); // Force 3 with a Follower
        var tough = Bf(s, "training-lion-veteran", 1); // Force 6
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "ikiryo-attack", ghost.Id).TargetIds!;
        Check(targets.Contains(followed.Id) && targets.Contains(shield.Id) && targets.Contains(tough.Id), "it may target a Personality with attached Followers: " + string.Join(",", targets));
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "ikiryo-attack", ghost.Id, followed.Id));
        Check(e.State.Cards[ghost.Id].Zone is Zone.FateDiscard, "it was destroyed as the cost");
        Check(Gone(e, followed), "strength 3 (the Chi of the Personality it was attached to) destroyed the Force 3 Personality with a Follower");
        Eq(Honor(e, 0), honor - 3, "Honor lost equal to that Personality's Chi");
        Check(e.State.Cards[host.Id].Dishonorable, "and he is dishonored");
        // A live Chi: +2 raises the strength; a Force 6 target then dies, and 5 Honor is lost.
        var s2 = Battle(); var host2 = Bf(s2, "training-crane-samurai", 0); host2.Modifiers.Add(new("test-source", "test", "Chi", 3)); var ghost2 = Attach(s2, Ikiryo, host2);
        var tough2 = Bf(s2, "training-lion-veteran", 1);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "ikiryo-attack", ghost2.Id, tough2.Id));
        Check(Gone(e2, tough2), "Chi 6 destroyed the Force 6 target"); Eq(Honor(e2, 0), Honor(e, 0) + 3 - 6, "and 6 Honor was lost");
        // Force 6 survives a Chi 3 Attack, though a legal target.
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Offer(e3, 0, "ikiryo-attack", ghost.Id, tough.Id));
        Check(!Gone(e3, tough), "the Force 6 target survives strength 3");
    }

    private static void LegionOfPainBar()
    {
        var s = Battle(1);
        var host = Bf(s, "training-crane-samurai", 0); Attach(s, Legion, host);
        var cheap = Bf(s, "training-lion-samurai", 1); var cheapPole = Attach(s, Sasumata, cheap); // Gold Cost 5
        var dear = Bf(s, "training-lion-veteran", 1); var dearPole = Attach(s, Sasumata, dear); // Gold Cost 8
        var other = Bf(s, "training-crane-veteran", 0);
        s.PriorityPlayerId = 1;
        var e = MatchEngine.FromTestState(s);
        var cheapTargets = TargetsOf(e, 1, "sasumata-bow", cheapPole.Id);
        var dearTargets = TargetsOf(e, 1, "sasumata-bow", dearPole.Id);
        Check(!cheapTargets.Contains(host.Id) && cheapTargets.Contains(other.Id), "a Personality with Gold Cost 5 cannot target the Legion's Personality, but another one: " + string.Join(",", cheapTargets));
        Check(dearTargets.Contains(host.Id), "a Personality with Gold Cost 8 can");
        Check(e.Execute(new GameCommand("useAbility", 1, e.Version, cheapPole.Id, AbilityId: "sasumata-bow", TargetId: host.Id)) is { Accepted: false }, "and the command is refused");
        // A Follower's action (Tamori House Guard) is not performed by a Personality.
        var s2 = Battle(1); var h2 = Bf(s2, "training-crane-samurai", 0); Attach(s2, Legion, h2);
        var lion = Bf(s2, "training-lion-samurai", 1); var stone = Attach(s2, TamoriGuard, lion); s2.PriorityPlayerId = 1;
        Check(TargetsOf(MatchEngine.FromTestState(s2), 1, "tamori-house-guard-stone", stone.Id).Contains(h2.Id), "an action performed by a Follower may target the Personality");
        // Exactly 7 is barred: a Personality with Gold Cost 7.
        var s3 = Battle(1); var h3 = Bf(s3, "training-crane-samurai", 0); Attach(s3, Legion, h3);
        var seven = Bf(s3, "training-lion-samurai", 1); seven.Modifiers.Add(new("test-source", "test", "GoldCost", 2)); var pole7 = Attach(s3, Sasumata, seven); s3.PriorityPlayerId = 1;
        Check(!TargetsOf(MatchEngine.FromTestState(s3), 1, "sasumata-bow", pole7.Id).Contains(h3.Id), "7 or lower is barred");
        var s4 = Battle(1); var h4 = Bf(s4, "training-crane-samurai", 0); Attach(s4, Legion, h4);
        var eight = Bf(s4, "training-lion-samurai", 1); eight.Modifiers.Add(new("test-source", "test", "GoldCost", 3)); var pole8 = Attach(s4, Sasumata, eight); s4.PriorityPlayerId = 1;
        Check(TargetsOf(MatchEngine.FromTestState(s4), 1, "sasumata-bow", pole8.Id).Contains(h4.Id), "and 8 is not");
        // Entering play: 2 Honor lost (the printed entry loss).
        Eq(CardCatalog.Get(Legion).EntryHonorLoss, 2, "After this card enters play: Lose 2 Honor");
    }

    // STUBS
    private static void ArmorOfTheHeavens()
    {
        (MatchEngine E, CardInstance Wearer, CardInstance Bystander, CardInstance Bow) Position()
        {
            var s = Table(Phase.Battle, 1);
            var wearer = Bf(s, "training-crane-courtier", 0); Attach(s, Armor, wearer); // Force 2 + 3 from the armor: within Ranged 5
            var bystander = Bf(s, "training-crane-veteran", 0);
            var lion = Bf(s, "training-lion-samurai", 1); var bow = Attach(s, Daikyu, lion);
            return (MatchEngine.FromTestState(s), wearer, bystander, bow);
        }
        ActionOption Choice(MatchEngine e, int holder, string ability, int chosen) =>
            e.QueryActions(holder).First(o => o.Type == "useAbility" && o.Command?.AbilityId == ability && o.Command.TargetPlayerId == chosen);
        // Another player's Attack targets the wearer: the targeted-by-action window opens for his controller.
        var (e, wearer, bystander, bow) = Position();
        Accept(e, Offer(e, 1, "tsuruchi-daikyu-attack", bow.Id, wearer.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack window opens");
        Eq(e.State.PriorityPlayerId, 0, "the targeted player holds the Reaction");
        var offered = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId is "armor-heavens-gain" or "armor-heavens-lose").ToArray();
        Eq(offered.Length, 4, "gain or lose, for either player");
        Check(offered.All(o => o.Enabled), "all four are enabled");
        // Issue #1240: each choice says only who gains or loses, without repeating the printed trigger, and the label matches the command it carries.
        Eq(Choice(e, 0, "armor-heavens-gain", 0).Label, "You Gain 2 Honor", "your gain"); Eq(Choice(e, 0, "armor-heavens-lose", 0).Label, "You Lose 2 Honor", "your loss");
        Eq(Choice(e, 0, "armor-heavens-gain", 1).Label, "Opponent Gains 2 Honor", "the opponent's gain"); Eq(Choice(e, 0, "armor-heavens-lose", 1).Label, "Opponent Loses 2 Honor", "the opponent's loss");
        var mine = Honor(e, 0); var theirs = Honor(e, 1);
        Accept(e, Choice(e, 0, "armor-heavens-lose", 1).Command!);
        Eq(Honor(e, 1), theirs - 2, "the chosen player lost 2 Honor"); Eq(Honor(e, 0), mine, "and only he");
        Check(!e.QueryActions(e.State.PriorityPlayerId).Any(o => o.Type == "useAbility" && o.Command?.AbilityId is "armor-heavens-gain" or "armor-heavens-lose" && o.Enabled), "once per turn: the other half is not offered either");
        // The gain half, for the controller, in a fresh position.
        var (g, gWearer, _, gBow) = Position();
        Accept(g, Offer(g, 1, "tsuruchi-daikyu-attack", gBow.Id, gWearer.Id));
        var gMine = Honor(g, 0);
        Accept(g, Choice(g, 0, "armor-heavens-gain", 0).Command!);
        Eq(Honor(g, 0), gMine + 2, "or a player gains 2 Honor");
        // Passing, the Attack resolves as usual against the wearer (Force 2 plus the armor's 3 against Ranged 5).
        for (var guard = 0; g.State.PendingChoice is not null && guard < 6; guard++) Accept(g, new GameCommand("declineReaction", g.State.PriorityPlayerId, g.Version));
        Check(Gone(g, gWearer), "the Attack itself still resolves");
        // Another Personality of his is targeted: no Reaction (it is for the Personality the armor is on).
        var (b, _, bBystander, bBow) = Position();
        Accept(b, Offer(b, 1, "tsuruchi-daikyu-attack", bBow.Id, bBystander.Id));
        Check(b.State.PendingChoice is null, "an Attack on another Personality opens no window: nobody holds a Reaction to it");
        // His own action targeting the wearer: "another player" only.
        var own = Table(Phase.Battle, 0); own.PriorityPlayerId = 0;
        var ownWearer = Bf(own, "training-crane-samurai", 0); Attach(own, Armor, ownWearer);
        var ownSource = Bf(own, "training-penalty-source", 0); Bf(own, "training-lion-samurai", 1);
        var eo = MatchEngine.FromTestState(own);
        Accept(eo, Offer(eo, 0, "training-penalty-force-own", ownSource.Id, ownWearer.Id));
        Check(eo.State.PendingChoice is null, "an action of his own player's targeting the wearer offers no Reaction");
        // Playing the Armor itself (the Equip command, by its own controller, before it is in play) opens no window for it, whoever holds priority.
        foreach (var phase in new[] { Phase.Action })
        {
            var q = Table(phase, 0); q.PriorityPlayerId = 0;
            var host = Add(q, "training-crane-samurai", 0, Zone.Home); Add(q, "training-lion-samurai", 1, Zone.Home);
            var armor = Add(q, Armor, 0, Zone.Hand); Gold(q, 0, 4);
            var eq = MatchEngine.FromTestState(q);
            Accept(eq, new GameCommand("equip", 0, eq.Version, CardId: armor.Id, SourceIds: eq.PaymentOptions(0, 4).First().SourceIds, TargetId: host.Id));
            Check(eq.State.Cards[armor.Id].Zone == Zone.Attached, $"the Armor is equipped in the {phase} phase");
            Check(eq.State.PendingChoice is null, $"equipping the Armor in the {phase} phase opens no Armor of the Heavens window");
            Check(!eq.QueryActions(0).Any(o => o.Command?.AbilityId is "armor-heavens-gain" or "armor-heavens-lose" && o.Enabled), $"and offers no Armor reaction afterwards ({phase})");
        }
    }
    private static void BladeOfPerfection()
    {
        // "After this card bows: Straighten it": an enemy Sasumata bows it, and it straightens; another Weapon stays bowed.
        var s = Battle(1); s.PriorityPlayerId = 1;
        var wearer = Bf(s, "training-crane-samurai", 0); var blade = Attach(s, Blade, wearer); var plainWeapon = Attach(s, "training-weapon", Bf(s, "training-crane-samurai", 0));
        var lion = Bf(s, "training-lion-samurai", 1); var pole = Attach(s, Sasumata, lion);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "sasumata-bow", pole.Id, blade.Id));
        Check(!e.State.Cards[blade.Id].Bowed, "it bowed and straightened at once");
        Check(e.State.Events.Any(ev => ev.Type == "bladeStraightened"), "and the log says why");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 1, "sasumata-bow", pole.Id, plainWeapon.Id));
        Check(e2.State.Cards[plainWeapon.Id].Bowed, "an ordinary Weapon stays bowed");
        // "After the resolution of another player's action that destroyed this card or its Personality": an Attack destroys the wearer.
        MatchEngine Destroyed(bool personality, out CardInstance ally, out CardInstance b, out CardInstance w)
        {
            var d = Battle(1); d.PriorityPlayerId = 1;
            w = Bf(d, "training-crane-courtier", 0); b = Attach(d, Blade, w); // Force 2 + 3 within Ranged 5
            ally = Add(d, "training-crane-veteran", 0, Zone.Home); Gold(d, 0, 3);
            var attacker = Bf(d, "training-lion-samurai", 1);
            if (personality) { var bow = Attach(d, Daikyu, attacker); var en = MatchEngine.FromTestState(d); Accept(en, Offer(en, 1, "tsuruchi-daikyu-attack", bow.Id, w.Id)); return en; }
            var michitsu = Bf(d, MichitsuId, 1); var em = MatchEngine.FromTestState(d); Accept(em, Offer(em, 1, "michitsu-destroy", michitsu.Id, b.Id)); return em;
        }
        var e3 = Destroyed(true, out var ally, out var blade3, out var wearer3);
        Check(Gone(e3, wearer3) && e3.State.Cards[blade3.Id].Zone == Zone.FateDiscard, "the wearer and his Blade are in the discard piles");
        Eq(e3.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for the Blade's controller");
        var offer = Enabled(e3, 0, "blade-perfection-attach", blade3.Id, ally.Id);
        Check(offer.Gold is { Cost: 4 }, "paying all costs: its own Gold cost of 4");
        Accept(e3, offer.Command!);
        Check(e3.State.Cards[blade3.Id].Zone == Zone.Attached && e3.State.Cards[blade3.Id].AttachedTo == ally.Id, "it is attached to the chosen performing Personality");
        Check(e3.State.Events.Any(ev => ev.Type == "payment" && ev.Message.Contains("paid 4 Gold for Blade of Perfection")), "and its cost of 4 Gold was paid");
        // The Blade itself destroyed (Akodo Michitsu): reattached to the same Personality.
        var e4 = Destroyed(false, out var ally4, out var blade4, out var wearer4);
        Check(e4.State.Cards[blade4.Id].Zone == Zone.FateDiscard && e4.State.Cards[wearer4.Id].Zone == Zone.Battlefield, "only the Blade was destroyed");
        Accept(e4, Enabled(e4, 0, "blade-perfection-attach", blade4.Id, wearer4.Id).Command!);
        Check(e4.State.Cards[blade4.Id].AttachedTo == wearer4.Id, "it may go back to the same Personality");
        // Not for a destruction it was not part of: an Attack destroys a different Personality, the Blade is still attached.
        var n = Battle(1); n.PriorityPlayerId = 1;
        var wn = Bf(n, "training-crane-samurai", 0); var bn = Attach(n, Blade, wn); var other = Bf(n, "training-crane-samurai", 0);
        Gold(n, 0, 3); var ln = Bf(n, "training-lion-samurai", 1); var bowN = Attach(n, Daikyu, ln);
        var en2 = MatchEngine.FromTestState(n);
        Accept(en2, Offer(en2, 1, "tsuruchi-daikyu-attack", bowN.Id, other.Id));
        Check(en2.State.PendingChoice is null && en2.State.Cards[bn.Id].Zone == Zone.Attached, "another Personality's destruction offers nothing");
        // Not for a Blade in the discard pile that another action destroyed earlier: the window names the action that has just resolved.
        var late = Destroyed(true, out _, out var blade5, out _);
        for (var guard = 0; late.State.PendingChoice is not null && guard < 6; guard++) Accept(late, new GameCommand("declineReaction", late.State.PriorityPlayerId, late.Version));
        Check(late.State.PendingChoice is null && late.State.Cards[blade5.Id].Zone == Zone.FateDiscard, "declining leaves it in the discard pile");
        Check(!CanUse(late, 0, "blade-perfection-attach", blade5.Id), "and it is not offered outside that window");
    }
    private static void ClanStandard()
    {
        var s = Battle();
        var wielder = Bf(s, "training-crane-samurai", 0); Attach(s, Standard, wielder); // Force 3, +1 from the Standard's own Force, +1 from its effect
        var ally = Bf(s, "training-crane-veteran", 0); // Force 5
        var followed = Bf(s, "training-crane-courtier", 0); Attach(s, "training-follower", followed); // Force 2, a Follower: +2
        var otherClan = Bf(s, "training-lion-samurai", 0); // Lion: not the player's Clan (Crane)
        var home = Add(s, "training-crane-samurai", 0, Zone.Home);
        var elsewhere = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1);
        var enemy = Bf(s, "training-crane-samurai", 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(wielder.Id), 3 + 1 + 1, "the wielder is one of the Clan Personalities at the battlefield");
        Eq(e.CardForceForTest(ally.Id), 5 + 1, "+1F");
        Eq(e.CardForceForTest(followed.Id), 2 + 2, "+2F with a Follower (the Follower's own Force is separate)");
        Eq(e.CardForceForTest(otherClan.Id), 4, "another Clan's Personality gets nothing");
        Eq(e.CardForceForTest(home.Id), 3, "nor one at home");
        Eq(e.CardForceForTest(elsewhere.Id), 3, "nor one at another battlefield");
        Eq(e.CardForceForTest(enemy.Id), 3, "nor the opponent's, though his Personality carries the Crane Clan alignment");
        // A second Standard stacks; the effect ends with the Item.
        var two = Battle(); var w2 = Bf(two, "training-crane-samurai", 0); Attach(two, Standard, w2); var w3 = Bf(two, "training-crane-veteran", 0); Attach(two, Standard, w3);
        Eq(MatchEngine.FromTestState(two).CardForceForTest(w2.Id), 3 + 1 + 2, "each Standard gives its own bonus");
        var bare = Battle(); var lone = Bf(bare, "training-crane-veteran", 0);
        Eq(MatchEngine.FromTestState(bare).CardForceForTest(lone.Id), 5, "without a Standard nothing changes");
    }
    private static void GiftArmorNegations()
    {
        // New Chi penalties are negated whatever their source (an enemy's training action -1 Chi).
        var s = Battle(1); s.PriorityPlayerId = 1;
        var wearer = Bf(s, "training-crane-veteran", 0); Attach(s, GiftArmor, wearer);
        var plain = Bf(s, "training-crane-samurai", 0);
        var source = Bf(s, "training-penalty-source", 1);
        var e = MatchEngine.FromTestState(s);
        var chi = e.CardChiForTest(wearer.Id); var plainChi = e.CardChiForTest(plain.Id);
        Accept(e, Offer(e, 1, "training-penalty-chi", source.Id, wearer.Id));
        Eq(e.CardChiForTest(wearer.Id), chi, "the Chi penalty was not applied to the wearer");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 1, "training-penalty-chi", source.Id, plain.Id));
        Eq(e2.CardChiForTest(plain.Id), plainChi - 1, "an unarmored Personality takes it");
        // Chi penalties already on him are negated the moment it attaches: through the Equip command.
        var q = Table(Phase.Action, 0);
        var target = Add(q, "training-crane-veteran", 0, Zone.Home); target.Modifiers.Add(new("test-source", "test", "Chi", -2)); target.Modifiers.Add(new("test-source", "test", "Force", -1));
        var item = Add(q, GiftArmor, 0, Zone.Hand); Gold(q, 0, 4);
        var eq = MatchEngine.FromTestState(q);
        Eq(eq.CardChiForTest(target.Id), 2, "the Chi 4 veteran with a -2 penalty");
        Accept(eq, new GameCommand("equip", 0, eq.Version, CardId: item.Id, SourceIds: eq.PaymentOptions(0, 4).First().SourceIds, TargetId: target.Id));
        Eq(eq.CardChiForTest(target.Id), 4, "the current Chi penalty is negated when the armor attaches");
        Check(eq.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -1), "a Force penalty is not a Chi penalty and stays");
        // The card's own destruction is negated: an enemy action that destroys an attachment (Akodo Michitsu's Battle).
        var d = Battle(1); d.PriorityPlayerId = 1;
        var w2 = Bf(d, "training-crane-veteran", 0); var a2 = Attach(d, GiftArmor, w2); var other = Attach(d, "training-weapon", w2);
        var michitsu = Bf(d, MichitsuId, 1);
        var ed = MatchEngine.FromTestState(d);
        Accept(ed, Offer(ed, 1, "michitsu-destroy", michitsu.Id, other.Id));
        Check(Gone(ed, other), "an ordinary attachment is destroyed by that action");
        var ed2 = MatchEngine.FromTestState(d);
        Accept(ed2, Offer(ed2, 1, "michitsu-destroy", michitsu.Id, a2.Id));
        Check(ed2.State.Cards[a2.Id].Zone == Zone.Attached && !Gone(ed2, a2), "the Gift Armor's destruction is negated");
        // But its Personality being destroyed takes it with him: a Ranged Attack on the wearer.
        var p = Battle(1); p.PriorityPlayerId = 1;
        var w3 = Bf(p, "training-crane-courtier", 0); var a3 = Attach(p, GiftArmor, w3); // Force 2 + 3 within Ranged 5
        var lion = Bf(p, "training-lion-samurai", 1); var bow = Attach(p, Daikyu, lion);
        var ep = MatchEngine.FromTestState(p);
        Accept(ep, Offer(ep, 1, "tsuruchi-daikyu-attack", bow.Id, w3.Id));
        Check(Gone(ep, w3) && Gone(ep, a3), "the armor is destroyed with its Personality: that destruction is not negated");
    }
    private static void KensaisBlade()
    {
        // +1F while attached to a Kensai.
        var s = Battle(); var plain = Bf(s, "training-crane-samurai", 0); Attach(s, KensaiBlade, plain);
        var kensai = Bf(s, "training-crane-samurai", 0); GiveKeyword(kensai, "Kensai"); Attach(s, KensaiBlade, kensai);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(plain.Id), 3 + 2, "a Personality who is not a Kensai has the printed +2");
        Eq(e.CardForceForTest(kensai.Id), 3 + 2 + 1, "a Kensai has +1F more");
        // After engaging: attach it from the hand to a Personality of yours at the current battlefield, paying its cost.
        MatchEngine Engaged(out CardInstance blade, out CardInstance atBattle, out CardInstance atHome)
        {
            var q = MatchEngine.CreateEconomyScenario().State;
            q.Phase = Phase.BattleSelection; q.ActivePlayerId = 0; q.PriorityPlayerId = 0; q.CurrentBattleProvince = null; q.UnresolvedBattlefields = [0];
            atBattle = Add(q, "training-crane-samurai", 0, Zone.Battlefield, province: 0); atHome = Add(q, "training-crane-veteran", 0, Zone.Home);
            Add(q, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            blade = Add(q, KensaiBlade, 0, Zone.Hand); Gold(q, 0, 3);
            var en = MatchEngine.FromTestState(q);
            Accept(en, new GameCommand("selectBattle", 0, en.Version, Province: 0));
            return en;
        }
        var eg = Engaged(out var blade, out var atBattle, out var atHome);
        Eq(eg.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        var offer = Enabled(eg, 0, "kensais-blade-attach", blade.Id, atBattle.Id);
        Check(offer.Gold is { Cost: 3 }, "paying all costs: its Gold cost of 3");
        Check(!CanUse(eg, 0, "kensais-blade-attach", blade.Id, atHome.Id), "the performing Personality must be at the current battlefield: not one at home");
        Accept(eg, offer.Command!);
        Check(eg.State.Cards[blade.Id].Zone == Zone.Attached && eg.State.Cards[blade.Id].AttachedTo == atBattle.Id, "attached to him");
        Check(!eg.State.Players[0].Hand.Contains(blade.Id), "and out of the hand");
        Check(eg.State.Events.Any(ev => ev.Type == "payment" && ev.Message.Contains("paid 3 Gold for Kensai's Blade")), "its cost of 3 Gold was paid");
        // Not from the discard pile, and not outside the Engage window.
        var s2 = Battle(); var host = Bf(s2, "training-crane-samurai", 0); var inHand = Add(s2, KensaiBlade, 0, Zone.Hand); Gold(s2, 0, 3);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "kensais-blade-attach", inHand.Id), "after the Engage window it is not a Reaction any more");
    }
    private static void MagaYariRules()
    {
        // 3 less Gold to attach to a Paragon or a Kensai.
        var s = Table(Phase.Action, 0);
        var paragon = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(paragon, "Paragon");
        var kensai = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(kensai, "Kensai");
        var plain = Add(s, "training-crane-veteran", 0, Zone.Home);
        var yari = Add(s, MagaYari, 0, Zone.Hand); Gold(s, 0, 4);
        var e = MatchEngine.FromTestState(s);
        int Cost(CardInstance target)
        {
            var option = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == yari.Id);
            return option.TargetGold is { } byTarget && byTarget.TryGetValue(target.Id, out var quote) ? quote.Cost : option.Gold!.Cost;
        }
        Eq(Cost(paragon), 2, "a Paragon: 5 less 3"); Eq(Cost(kensai), 2, "a Kensai too"); Eq(Cost(plain), 5, "anyone else pays the printed 5");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: yari.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds, TargetId: paragon.Id));
        Check(e.State.Cards[yari.Id].AttachedTo == paragon.Id, "attached to the Paragon for 2 Gold");
        // "This card will not be transferred": an Experienced overlay discards it, where another attachment goes along.
        var o = MatchEngine.CreateEconomyScenario().State;
        o.Phase = Phase.Dynasty; o.ActivePlayerId = 0; o.PriorityPlayerId = 0; o.Players[0].Clan = "Scorpion"; o.Players[0].Honor = 0;
        var higher = o.Cards[o.Players[0].Provinces[0].CardId!]; higher.DefinitionId = "training-paneki-exp3"; higher.FaceUp = true;
        var lower = Add(o, "training-paneki-base", 0, Zone.Home);
        var stay = Add(o, "training-weapon", 0, Zone.Attached, parent: lower.Id); var lost = Add(o, MagaYari, 0, Zone.Attached, parent: lower.Id);
        var eo = MatchEngine.FromTestState(o);
        Accept(eo, new GameCommand("overlay", 0, eo.Version, CardId: higher.Id, TargetId: lower.Id, SourceIds: []));
        Check(eo.State.Cards[stay.Id].AttachedTo == higher.Id, "an ordinary attachment moves to the overlaying Personality");
        Check(eo.State.Cards[lost.Id].Zone == Zone.FateDiscard && eo.State.Cards[lost.Id].AttachedTo is null, "the Maga-yari is not transferred: it is discarded");
        // Nor by a transfer action (Daigotsu Yamato's "Transfer it to Yamato" names a Follower; the Personality-to-Personality transfer helpers ask the same rule).
        Check(CardCatalog.Get(MagaYari).Effect == "maga-yari", "the transfer rule is the card's Effect");
    }
    private static void ReserveWeaponDuel()
    {
        MatchEngine Duel(bool weapon, bool onOther = false)
        {
            var s = Battle(); // player 0 the Attacker, player 0 challenges
            var mine = Bf(s, "training-crane-samurai", 0); // Force 3
            var bystander = Bf(s, "training-crane-samurai", 0);
            if (weapon) Attach(s, ReserveWeapon, onOther ? bystander : mine); // +3 Force from the weapon itself
            var theirs = Bf(s, "training-lion-veteran", 1); // Force 6
            var challenge = Add(s, "training-duel-force", 0, Zone.Hand);
            Add(s, "training-duel-challenge", 1, Zone.Hand); // a hand card with Focus Value 2 for the other side
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-duel-force", challenge.Id, theirs.Id, mine.Id));
            Eq(e.State.PendingDuel?.Stage, "Focusing", "the duel is in the Focusing stage, the challenged player first");
            return e;
        }
        // Instead of focusing: +3 to the duel stat. Player 1 focuses a card (Focus Value 2); player 0 (Force 3 + 3 with the weapon = 6) reserves.
        var e1 = Duel(weapon: true);
        var focusCard = e1.State.Players[1].Hand[0];
        Accept(e1, new GameCommand("focus", 1, e1.Version, CardId: focusCard));
        var reserve = e1.QueryActions(0).Where(a => a.Type == "focus" && a.Command?.Choice == "reserve").ToArray();
        Check(reserve.Length == 1 && reserve[0].Enabled, "player 0 is offered the Reserve Weapon beside the ordinary focus and strike");
        Check(!e1.QueryActions(1).Any(a => a.Type == "focus" && a.Command?.Choice == "reserve"), "and only the side whose Personality wears it");
        Accept(e1, reserve[0].Command!);
        Eq(e1.State.PendingDuel!.ChallengerFocused.Count, 0, "he focused no card");
        Eq(e1.State.PendingDuel!.TurnPlayerId, 1, "the turn passes to the other side, as after a focus");
        Accept(e1, new GameCommand("strike", 1, e1.Version));
        var won = e1.GetView(0).LastDuel!.Result!;
        Eq(won.ChallengerTotal, 3 + 3 + 3, "Force 3, the weapon's 3 and the +3 to his duel stat");
        Eq(won.ChallengedTotal, 6 + 2, "the veteran's Force 6 and the focused card's Focus Value 2");
        Check(won.WinnerId is not null && e1.State.Cards[won.WinnerId].OwnerId == 0, "so he won it");
        // Without reserving, the same duel is lost (6 against 8).
        var e2 = Duel(weapon: true);
        Accept(e2, new GameCommand("focus", 1, e2.Version, CardId: e2.State.Players[1].Hand[0]));
        Accept(e2, new GameCommand("strike", 0, e2.Version));
        var lost = e2.GetView(0).LastDuel!.Result!;
        Eq(lost.ChallengerTotal, 6, "no bonus when he did not reserve");
        Check(lost.WinnerId is not null && e2.State.Cards[lost.WinnerId].OwnerId == 1, "and the other side won");
        // The bonus lasts until the duel ends, and only that duel: a fresh position has none (PendingDuel.StatBonuses is the duel's own).
        Check(e1.State.PendingDuel is null, "the duel is over");
        // Not offered without the weapon, nor when it is on another Personality of his than the one dueling.
        var none = Duel(weapon: false); Accept(none, new GameCommand("focus", 1, none.Version, CardId: none.State.Players[1].Hand[0]));
        Check(!none.QueryActions(0).Any(a => a.Command?.Choice == "reserve"), "no weapon, no offer");
        var other = Duel(weapon: true, onOther: true); Accept(other, new GameCommand("focus", 1, other.Version, CardId: other.State.Players[1].Hand[0]));
        Check(!other.QueryActions(0).Any(a => a.Command?.Choice == "reserve"), "a weapon on a Personality who is not dueling is not offered");
        Check(other.Execute(new GameCommand("focus", 0, other.Version, CardId: other.State.Cards.Values.First(c => c.DefinitionId == ReserveWeapon).Id, Choice: "reserve")) is { Accepted: false }, "and the command is refused");
        // It may be used again on his next opportunity (a trait without a use limit), but each use takes the place of a focus and counts against the four-focus cap: 2 reserves = +6.
        var twice = Duel(weapon: true);
        Accept(twice, new GameCommand("focus", 1, twice.Version, CardId: twice.State.Players[1].Hand[0]));
        Accept(twice, twice.QueryActions(0).First(a => a.Command?.Choice == "reserve").Command!);
        Accept(twice, new GameCommand("focus", 1, twice.Version, Choice: "deck"));
        Accept(twice, twice.QueryActions(0).First(a => a.Command?.Choice == "reserve").Command!);
        Eq(twice.State.PendingDuel!.StatBonuses.Values.Sum(), 6, "two reserves, +3 each");
    }
    // Issue #915 (EE-BATTLE-VAL-048, EE-Challenges-and-duels-018): the substitution is a focus opportunity. After four opportunities, replacements included, the engine offers only a Strike.
    private static void ReserveWeaponCap()
    {
        // Player 1 challenges, so player 0 (the weapon's owner) is the challenged player and acts first in the Focusing stage.
        var s = Battle(attacker: 1); s.PriorityPlayerId = 1;
        var mine = Bf(s, "training-crane-samurai", 0);
        Attach(s, ReserveWeapon, mine);
        Attach(s, ReserveWeapon, mine); // a second weapon changes nothing: the cap is per player
        var theirs = Bf(s, "training-lion-veteran", 1);
        var challenge = Add(s, "training-duel-force", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-duel-force", challenge.Id, mine.Id, theirs.Id));
        Eq(e.State.PendingDuel?.Stage, "Focusing", "the duel is in the Focusing stage");
        Eq(e.State.PendingDuel!.TurnPlayerId, 0, "the weapon's owner is the challenged player and acts first");
        for (var opportunity = 1; opportunity <= 4; opportunity++)
        {
            var offered = e.QueryActions(0).Where(a => a.Enabled && a.Command?.Choice == "reserve").ToArray();
            Check(offered.Length == 2 && e.QueryActions(0).Any(a => a.Type == "strike"), $"opportunity {opportunity}: Reserve Weapon is offered beside the Strike");
            Accept(e, offered[0].Command!);
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        }
        Eq(e.State.PendingDuel!.StatBonuses.Values.Sum(), 12, "four substitutions, +3 each");
        Eq(e.State.PendingDuel!.TurnPlayerId, 0, "it is again the weapon owner's turn");
        var atCap = e.QueryActions(0).Where(a => a.Enabled).ToArray();
        Check(atCap.Any(a => a.Type == "strike"), "at the cap a Strike is offered");
        Check(!atCap.Any(a => a.Type == "focus"), "and no focus or Reserve Weapon substitution is listed");
        var weapon = e.State.Cards.Values.First(c => c.DefinitionId == ReserveWeapon);
        Check(e.Execute(new GameCommand("focus", 0, e.Version, CardId: weapon.Id, Choice: "reserve")) is { Accepted: false }, "a fifth substitution is refused by validation, agreeing with the listing");
        Check(e.State.PendingDuel!.Stage == "Focusing" && e.State.PendingDuel.StatBonuses.Values.Sum() == 12, "the refusal changed nothing");
        Accept(e, atCap.First(a => a.Type == "strike").Command!);
        Check(e.State.PendingDuel is null || e.State.PendingDuel.Stage != "Focusing", "the Strike ends focusing");
    }
    // The replayable Duel Demo position with a real, published Reserve Weapon on a duellist.
    private static void ReserveWeaponCapDuelDemo()
    {
        var e = MatchEngine.CreateDuelDemo();
        var begin = e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId == DuelDemoScenarios.Get("steel").ChallengeAbility);
        Accept(e, begin.Command!);
        var duel = e.State.PendingDuel!;
        Eq(duel.Stage, "Focusing");
        var first = e.State.PendingDuel!.TurnPlayerId;
        var host = first == duel.ChallengerPlayerId ? duel.ChallengerId : duel.ChallengedId;
        var s = e.State; // State is a copy: build the position with a real Reserve Weapon on the duellist and enough cards for the other side, then continue from it
        Add(s, ReserveWeapon, first, Zone.Attached, province: s.Cards[host].ProvinceIndex, parent: host);
        var other = 1 - first;
        for (var i = 0; i < 4; i++) Add(s, "training-duel-challenge", other, Zone.Hand);
        e = MatchEngine.FromTestState(s);
        for (var i = 0; i < 4; i++)
        {
            Accept(e, e.QueryActions(first).First(a => a.Enabled && a.Command?.Choice == "reserve").Command!);
            Accept(e, e.QueryActions(other).First(a => a.Enabled && a.Type == "focus").Command!);
        }
        Check(!e.QueryActions(first).Any(a => a.Enabled && a.Type == "focus"), "Duel Demo: no substitution after four opportunities");
        Check(e.QueryActions(first).Any(a => a.Enabled && a.Type == "strike"), "Duel Demo: Strike is offered");
        Check(e.Execute(new GameCommand("focus", first, e.Version, CardId: e.State.Cards.Values.First(c => c.DefinitionId == ReserveWeapon).Id, Choice: "reserve")) is { Accepted: false }, "Duel Demo: a fifth substitution is refused");
    }
    private static void ChrysanthemumBlossom()
    {
        MatchState Maneuvers(Phase phase, int actor)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = phase; s.ActivePlayerId = 0; s.PriorityPlayerId = actor; s.UnresolvedBattlefields = [0, 1, 2, 3];
            return s;
        }
        // The Attacker assigns the Personality wearing it (and another one without): a Reaction window opens.
        var s = Maneuvers(Phase.InfantryDeclaration, 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); var blossom = Add(s, Blossom, 0, Zone.Attached, parent: host.Id);
        var plain = Add(s, "training-crane-veteran", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [host.Id] = 0, [plain.Id] = 1 }));
        Eq(e.State.Cards[host.Id].Zone, Zone.Battlefield, "he was assigned");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "after the assignment a Reaction window opens");
        Eq(e.State.PriorityPlayerId, 0, "for the Reaction's holder");
        var force = e.CardForceForTest(host.Id); var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "chrysanthemum-blossom-boost", blossom.Id).Command!);
        Eq(e.CardForceForTest(host.Id), force + 1, "a +1F token");
        Check(e.State.Cards[host.Id].Tokens.Any(t => t.Stat == "Force" && t.Amount == 1), "it is a token");
        Eq(Honor(e, 0), honor + 1, "and 1 Honor");
        Check(!CanUse(e, 0, "chrysanthemum-blossom-boost", blossom.Id), "once per turn");
        for (var guard = 0; e.State.PendingChoice is not null && guard < 6; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Phase, Phase.DefenseAssignment, "the assignment then carries on to the Defender");
        Eq(e.State.PriorityPlayerId, 1, "with the Defender to assign");
        // Declining leaves everything as it was.
        var d = Maneuvers(Phase.InfantryDeclaration, 0);
        var dh = Add(d, "training-crane-samurai", 0, Zone.Home); var db = Add(d, Blossom, 0, Zone.Attached, parent: dh.Id);
        var ed = MatchEngine.FromTestState(d);
        Accept(ed, new GameCommand("attack", 0, ed.Version, Assignments: new Dictionary<string, int> { [dh.Id] = 2 }));
        var forceD = ed.CardForceForTest(dh.Id); var honorD = Honor(ed, 0);
        for (var guard = 0; ed.State.PendingChoice is not null && guard < 6; guard++) Accept(ed, new GameCommand("declineReaction", ed.State.PriorityPlayerId, ed.Version));
        Eq(ed.CardForceForTest(dh.Id), forceD, "no token when it was declined"); Eq(Honor(ed, 0), honorD, "and no Honor");
        Eq(ed.State.Phase, Phase.DefenseAssignment, "the assignment carried on");
        // A Personality without it, or assigning nobody: no window at all.
        var n = Maneuvers(Phase.InfantryDeclaration, 0);
        var loner = Add(n, "training-crane-samurai", 0, Zone.Home); var blossomHome = Add(n, "training-crane-veteran", 0, Zone.Home); Add(n, Blossom, 0, Zone.Attached, parent: blossomHome.Id);
        var en = MatchEngine.FromTestState(n);
        Accept(en, new GameCommand("attack", 0, en.Version, Assignments: new Dictionary<string, int> { [loner.Id] = 0 }));
        Check(en.State.PendingChoice is null && en.State.Phase == Phase.DefenseAssignment, "assigning another Personality opens nothing");
        // The Defender's assignment counts too, and the window is his.
        var df = Maneuvers(Phase.DefenseAssignment, 1);
        var guardMan = Add(df, "training-lion-samurai", 1, Zone.Home); var db2 = Add(df, Blossom, 1, Zone.Attached, parent: guardMan.Id);
        var edf = MatchEngine.FromTestState(df);
        Accept(edf, new GameCommand("assignDefense", 1, edf.Version, Assignments: new Dictionary<string, int> { [guardMan.Id] = 0 }));
        Eq(edf.State.PendingChoice?.Type, "afterActionResolvedReaction", "the Defender assigned it");
        Eq(edf.State.PriorityPlayerId, 1, "his window");
        var honor1 = Honor(edf, 1);
        Accept(edf, Enabled(edf, 1, "chrysanthemum-blossom-boost", db2.Id).Command!);
        Eq(Honor(edf, 1), honor1 + 1, "he gained 1 Honor");
        // A dishonorable Personality is rehonored instead of granting his own controller Honor (an action performed by a dishonorable Personality).
        var dis = Maneuvers(Phase.InfantryDeclaration, 0);
        var sad = Add(dis, "training-crane-samurai", 0, Zone.Home); sad.Dishonorable = true; var db3 = Add(dis, Blossom, 0, Zone.Attached, parent: sad.Id);
        var edis = MatchEngine.FromTestState(dis); var honorDis = Honor(edis, 0);
        Accept(edis, new GameCommand("attack", 0, edis.Version, Assignments: new Dictionary<string, int> { [sad.Id] = 0 }));
        Accept(edis, Enabled(edis, 0, "chrysanthemum-blossom-boost", db3.Id).Command!);
        Check(!edis.State.Cards[sad.Id].Dishonorable && Honor(edis, 0) == honorDis, "a dishonorable Personality is rehonored instead");
    }
    private static void BanditGang()
    {
        // 2 Honor when it enters play: attached from the hand (Equip).
        var q = Table(Phase.Action, 0);
        var wearer = Add(q, "training-crane-samurai", 0, Zone.Home); var gang = Add(q, Bandit, 0, Zone.Hand);
        var eq = MatchEngine.FromTestState(q); var before = Honor(eq, 0);
        Accept(eq, new GameCommand("equip", 0, eq.Version, CardId: gang.Id, SourceIds: [], TargetId: wearer.Id));
        Eq(Honor(eq, 0), before - 2, "After this card enters play: Lose 2 Honor");
        // After a battle resolution ends, at the current battlefield: pay 2 Gold or destroy it. The winner's Follower, the battle resolved by two passes.
        (MatchEngine E, CardInstance Gang) Won(bool gold = true, bool loses = false)
        {
            var s = Battle();
            var winner = Bf(s, loses ? "training-crane-samurai" : "training-crane-veteran", 0); var g = Attach(s, Bandit, winner); // Force 3 / 5 + 2
            Bf(s, "training-lion-veteran", 1); // Force 6
            if (gold) Gold(s, 0, 2);
            else foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0 || c.Id == s.Players[0].StrongholdId)) c.Bowed = true;
            var e = MatchEngine.FromTestState(s);
            PassTo(e, 0); PassTo(e, 1);
            return (e, g);
        }
        var (e1, g1) = Won();
        Eq(e1.State.PendingChoice?.Type, "eitherOr", "the Follower's controller must choose");
        Eq(e1.State.PriorityPlayerId, 0, "he decides");
        var pay = e1.QueryActions(0).First(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "pay"); Check(pay.Enabled, "paying is possible: " + pay.Reason);
        Accept(e1, pay.Command!);
        Check(e1.State.Cards[g1.Id].Zone == Zone.Attached && e1.State.PendingChoice is null, "paid: it stays, and the battle carries on to its end");
        Check(e1.State.Events.Any(ev => ev.Type == "payment" && ev.Message.Contains("paid 2 Gold for Bandit Gang")), "the 2 Gold was paid");
        var (e2, g2) = Won();
        Accept(e2, e2.QueryActions(0).First(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "destroy").Command!);
        Check(e2.State.Cards[g2.Id].Zone == Zone.FateDiscard && e2.State.PendingChoice is null, "not paid: it is destroyed");
        // Unable to pay: the one option left is taken automatically.
        var (e3, g3) = Won(gold: false);
        Check(e3.State.PendingChoice is null && e3.State.Cards[g3.Id].Zone == Zone.FateDiscard, "with no Gold to pay it is destroyed without a decision");
        Check(e3.State.Events.Any(ev => ev.Type == "eitherOrAutoResolved"), "and the log says it was automatic");
        // Its host destroyed in the resolution: the Follower went with him, so nothing is asked.
        var (e4, g4) = Won(loses: true);
        Check(e4.State.PendingChoice is null && e4.State.Cards[g4.Id].Zone == Zone.FateDiscard, "its Personality lost: nothing to pay for");
        // Two of them: each of their controllers is asked in turn.
        var two = Battle(); var w1 = Bf(two, "training-crane-veteran", 0); var w2 = Bf(two, "training-crane-veteran", 0); var ga = Attach(two, Bandit, w1); var gb = Attach(two, Bandit, w2);
        Bf(two, "training-lion-samurai", 1); Gold(two, 0, 2);
        var e5 = MatchEngine.FromTestState(two); PassTo(e5, 0); PassTo(e5, 1);
        Eq(e5.State.PendingChoice?.Type, "eitherOr", "the first Follower asks");
        Accept(e5, e5.QueryActions(0).First(a => a.Command?.Choice == "pay").Command!);
        Eq(e5.State.PendingChoice?.Type, "eitherOr", "then the second");
        Accept(e5, e5.QueryActions(0).First(a => a.Command?.Choice == "destroy").Command!);
        Eq(new[] { ga, gb }.Count(g => e5.State.Cards[g.Id].Zone == Zone.FateDiscard), 1, "one paid, one destroyed");
        Check(e5.State.PendingChoice is null, "and the battle finished");
        // A Follower not at the battlefield (its Personality at home) is never asked.
        var home = Battle(); var stay = Add(home, "training-crane-veteran", 0, Zone.Home); var gh = Add(home, Bandit, 0, Zone.Attached, parent: stay.Id);
        Bf(home, "training-crane-samurai", 0); Bf(home, "training-lion-samurai", 1);
        var eh = MatchEngine.FromTestState(home); PassTo(eh, 0); PassTo(eh, 1);
        Check(eh.State.PendingChoice is null && eh.State.Cards[gh.Id].Zone == Zone.Attached, "a Bandit Gang at home is not at the battlefield");
    }
}
