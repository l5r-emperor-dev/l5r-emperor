using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Embers of War pass (issue #460), batch 2: the 12 of the batch's 25 candidate Personalities wired in personality-wave-63, driven through
// QueryActions and the commands it offers. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the
// Lion), a Scorpion/Crab/Lion fixture Personality or Player.Clan override substituted in as each card needs. The other 13 stay unwired and,
// per this pass's established convention, are never published at all (see BLOCKERS in tools/content/build_card_inventory.py).
internal static class EmbersOfWarBatch2Tests
{
    private const string Miro = "oracle-l5r-4386", Tomiko = "oracle-l5r-5286", Isul = "oracle-l5r-5332", Rata = "oracle-l5r-6165",
        Rokku = "oracle-l5r-6376", Sakti = "oracle-l5r-6454", Junpei = "oracle-l5r-6901", Yoshihara = "oracle-l5r-7373", Sugihara = "oracle-l5r-7615",
        Tomaru = "oracle-l5r-8894", Makoto = "oracle-l5r-9449", Kanaye = "oracle-l5r-9579";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);

    public static void Run(TestRunner runner) => runner.Run("embers-of-war-batch-2", [
        ("Published shape: the 12 wired cards of personality-wave-63 print their catalogue stats, keywords and Effect/ability ids, ship their art and are listed in the wave manifest", PublishedShape),
        ("Kitsu Miro: Home Battle (Absent), bow, creates the 2F/2C/3PH Lion Ancestor token at the current battlefield, removed after the battle ends", MiroCreate),
        ("Moshi Tomiko: Ranged 6 Attack paid by bowing Tomiko herself or one of her attached Spells", TomikoAttack),
        ("Moto Isul: even at home (Absent), bows or straightens a target Personality (either side) -- one shared use", IsulToggle),
        ("Rata: after an action he performed destroys a Personality, draws a card -- not for a Personality destroyed by someone else's action or one that only bowed", RataDraw),
        ("Rokku - experienced: +1F only while a Lion Clan player controls him; even while bowed, after engaging with an enemy unit at the battlefield, moves there and straightens his unit", RokkuBoostAndMove),
        ("Sakti: bows a target enemy card with lower Force, and may move it home if it is a Personality", SaktiBow),
        ("Shinjo Junpei - experienced: even at home (Absent), his chosen unbowed Personality performs a Melee 4 Attack", JunpeiAttack),
        ("Soshi Yoshihara - experienced: locks a target Holding or Personality's own abilities for the turn; bows herself to dishonor a target Personality, losing his controller 1 Honor only when she is a Scorpion or Crab Clan player", YoshiharaLockAndDishonor),
        ("Sugihara: gives a target enemy Personality -4F, and grants an additional Battle action Sugihara alone may perform only once that brings the target to 0 Force", SugiharaPenalty),
        ("Tsuruchi Tomaru - experienced: Ranged 4 Attack, twice per turn", TomaruAttack),
        ("Tsuruchi Tomaru - experienced: recruited from a province for Gold, alone or over the base Tomaru (issue #803)", TomaruRecruit),
        ("Yasuki Makoto: his controller's Holdings enter play for 1 less Gold; bows to make a target's controller choose paying 4 Gold and 1 Honor, or the target is dishonored and moved home", MakotoLockAndHoldingDiscount),
        ("Yoritomo Kanaye: bows a target enemy card without attachments, destroying it too only if Gold equal to its own Gold Cost was paid", KanayeBow),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string clan, int force, int chi, int cost, string keywords, string[] abilities, string effect = "none")
        {
            var c = CardCatalog.Get(id);
            Check(c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && Keywords(id) == keywords && c.Effect == effect,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} keywords={Keywords(id)} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
        }

        // Wired.
        Stats(Miro, "Lion", 3, 3, 5, "Lion Clan,Shugenja,Sodan Senzo,Water", ["miro-create"]);
        Stats(Tomiko, "Mantis", 4, 4, 9, "Mantis Clan,Naval,Shugenja,Thunder", ["tomiko-attack"]);
        Stats(Isul, "Unicorn", 4, 3, 8, "Samurai,Tactician,Unicorn Clan", ["isul-bow", "isul-straighten"]);
        Stats(Rata, "Unaligned", 3, 3, 4, "Cultist,Gaijin", ["rata-draw"]);
        Stats(Rokku, "Unaligned", 4, 3, 6, "Experienced Akodo Rokku,Monk,Order of Strength,Tactician,Unique,Zealot", ["rokku-move"], "rokku-lion-force-bonus");
        Stats(Sakti, "Unaligned", 8, 1, 9, "Berserker,Gaijin", ["sakti-bow"]);
        Stats(Junpei, "Unicorn", 4, 4, 8, "Experienced,Samurai,Tactician,Unicorn Clan,Unique", ["junpei-attack"]);
        Stats(Yoshihara, "Scorpion", 2, 5, 8, "Advisor to the Jade Champion,Air,Courtier,Experienced,Magistrate,Scorpion Clan,Shugenja,Unique", ["yoshihara-lock", "yoshihara-dishonor"]);
        Stats(Sugihara, "Spider", 6, 3, 9, "Kensai,Monk,Order of the Spider,Spider Clan", ["sugihara-penalty"]);
        Stats(Tomaru, "Mantis", 4, 4, 9, "Experienced,Mantis Clan,Naval,Samurai,Scout,Unique", ["tomaru-attack"]);
        Stats(Makoto, "Crab", 2, 3, 7, "Courtier,Crab Clan,Kolat,Merchant", ["makoto-lock"], "makoto-holding-discount");
        Stats(Kanaye, "Mantis", 4, 3, 7, "Brute,Extortionist,Magistrate,Mantis Clan,Naval,Samurai", ["kanaye-bow"]);

        Manifest(root, "personality-wave-63", Miro, Tomiko, Isul, Rata, Rokku, Sakti, Junpei, Yoshihara, Sugihara, Tomaru, Makoto, Kanaye);
    }


    private static void MiroCreate()
    {
        var s = Table(Phase.Battle, 0);
        var miro = Add(s, Miro, 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "miro-create", miro.Id), "usable from home (Absent, Home Battle)");
        var beforeIds = e.State.Cards.Keys.ToHashSet();
        Accept(e, Use(e, 0, "miro-create", miro.Id));
        Check(e.State.Cards[miro.Id].Bowed, "Miro bowed as her own cost");
        var token = e.State.Cards.Values.FirstOrDefault(c => !beforeIds.Contains(c.Id));
        Check(token is not null, "a new card was created");
        Check(token!.Zone == Zone.Battlefield && token.ProvinceIndex == 0, "created at the current battlefield");
        var tokenDef = CardCatalog.Get(token.DefinitionId);
        Check(tokenDef.Type == CardType.Personality && tokenDef.Force == 2 && tokenDef.Chi == 2 && tokenDef.PersonalHonor == 3, "the token's stats");
        Check(e.State.CreatedUntilBattleEndsIds.Contains(token.Id), "removed from the game after the battle ends");
    }

    private static void TomikoAttack()
    {
        var s = Table(Phase.Battle, 0);
        var tomiko = Add(s, Tomiko, 0, Zone.Battlefield, province: 0);
        var spell = CardCatalog.Cards.Values.First(c => c.Type == CardType.Spell && !c.ScenarioOnly);
        var spellCard = Add(s, spell.Id, 0, Zone.Attached, province: 0, parent: tomiko.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        // Bowing the attached Spell instead of Tomiko herself is a legal performer choice.
        Accept(e, Use(e, 0, "tomiko-attack", tomiko.Id, enemy.Id, performerId: spellCard.Id));
        Check(e.State.Cards[spellCard.Id].Bowed && !e.State.Cards[tomiko.Id].Bowed, "the Spell bowed, not Tomiko herself");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "Ranged 6 destroyed the enemy Personality");
    }

    private static void IsulToggle()
    {
        var s = Table(Phase.Battle, 0);
        var isul = Add(s, Isul, 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "isul-bow", isul.Id), "usable from home (Absent)");
        Accept(e, Offer(e, 0, "isul-bow", isul.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "the target bowed");
        Check(!CanUse(e, 0, "isul-straighten", isul.Id), "the shared use is spent for the turn");
    }

    private static void RataDraw()
    {
        var s = Table(Phase.Battle, 0);
        var rata = Add(s, Rata, 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon-with-attack", 0, Zone.Attached, province: 0, parent: rata.Id);
        var enemy = Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // Force 2, dies to the Weapon's Melee 2
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "training-weapon-attack", weapon.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "precondition: the enemy Personality was destroyed by an action Rata performed (through his own Weapon)");
        Check(e.State.PendingChoice?.Type == "afterActionResolvedReaction", "his Reaction window opens");
        Accept(e, e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "rata-draw").Command!);
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "drew a card");
    }

    private static void RokkuBoostAndMove()
    {
        // +1F only while a Lion Clan player controls him (player 1 is the Lion in this shared scenario).
        var s = Table(Phase.Battle, 1);
        var rokkuLion = Add(s, Rokku, 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GetView(1).Players[1].Army.Single(c => c.Id == rokkuLion.Id).Force, CardCatalog.Get(Rokku).Force + 1, "+1F under his Lion controller");
        var s2 = Table(Phase.Battle, 0);
        var rokkuCrane = Add(s2, Rokku, 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.GetView(0).Players[0].Army.Single(c => c.Id == rokkuCrane.Id).Force, CardCatalog.Get(Rokku).Force, "no bonus under a Crane controller");

        // Even while bowed, after engaging, with an enemy unit at the battlefield: moves to it and straightens his unit. Reached through the
        // real BattleSelection -> selectBattle flow (the engage window's own real shape, not hand-rigged PendingEngage/PendingChoice state).
        var s3 = MatchEngine.CreateEconomyScenario().State;
        s3.Phase = Phase.BattleSelection; s3.ActivePlayerId = 0; s3.PriorityPlayerId = 0; s3.CurrentBattleProvince = null; s3.UnresolvedBattlefields = [0];
        var rokku3 = Add(s3, Rokku, 1, Zone.Home, bowed: true);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // the attacker's unit: enemy presence for Rokku's own (defending) controller
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // a unit of Rokku's own controller at the battlefield: presence for taking a Reaction in a battle
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, new("selectBattle", 0, e3.Version, Province: 0));
        Eq(e3.State.PendingChoice?.Type, "engageReaction", "the engage window opens");
        Eq(e3.State.PriorityPlayerId, 0, "the turn player (the Attacker) has the first opportunity");
        Accept(e3, new("declineReaction", 0, e3.Version)); // player 0 has nothing to play; priority passes to Rokku's own controller
        Accept(e3, Enabled(e3, 1, "rokku-move", rokku3.Id).Command!);
        Check(e3.State.Cards[rokku3.Id].Zone == Zone.Battlefield && e3.State.Cards[rokku3.Id].ProvinceIndex == 0, "moved to the engaged battlefield");
        Check(!e3.State.Cards[rokku3.Id].Bowed, "straightened his unit since he moved");
    }

    private static void SaktiBow()
    {
        var s = Table(Phase.Battle, 0);
        var sakti = Add(s, Sakti, 0, Zone.Battlefield, province: 0); // Force 8
        var weakerPersonality = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "sakti-bow", sakti.Id, weakerPersonality.Id));
        Check(e.State.Cards[weakerPersonality.Id].Bowed, "the weaker enemy card bowed");
        Check(e.State.Cards[weakerPersonality.Id].Zone == Zone.Home, "moved home since it is a Personality");
    }

    private static void JunpeiAttack()
    {
        var s = Table(Phase.Battle, 0);
        var junpei = Add(s, Junpei, 0, Zone.Home);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "junpei-attack", junpei.Id, performer.Id), "usable even with Junpei at home (Absent)");
        Accept(e, Use(e, 0, "junpei-attack", junpei.Id, enemy.Id, performerId: performer.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "Melee 4 destroyed the weaker enemy Personality");
    }

    private static void YoshiharaLockAndDishonor()
    {
        var s = Table(Phase.Action, 0);
        var yoshihara = Add(s, Yoshihara, 0, Zone.Battlefield, province: 0);
        var holding = CardCatalog.Cards.Values.First(c => c.Type == CardType.Holding && !c.ScenarioOnly);
        var holdingCard = Add(s, holding.Id, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "yoshihara-lock", yoshihara.Id, holdingCard.Id));
        Check(e.State.Cards[holdingCard.Id].ActionLocks.Count > 0, "the target Holding's own abilities are locked for the turn");

        // Bow herself, dishonor a target Personality: her controller's own Clan decides the 1-Honor penalty.
        var s2 = Table(Phase.Action, 0);
        s2.Players[0].Clan = "Scorpion";
        var yoshihara2 = Add(s2, Yoshihara, 0, Zone.Battlefield, province: 0);
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var honorBefore = e2.State.Players[1].Honor;
        Accept(e2, Offer(e2, 0, "yoshihara-dishonor", yoshihara2.Id, target2.Id));
        Check(e2.State.Cards[target2.Id].Dishonorable, "the target dishonored");
        Check(e2.State.Cards[yoshihara2.Id].Bowed, "Yoshihara bowed as her own cost");
        Eq(e2.State.Players[1].Honor, honorBefore - 1, "his controller lost 1 Honor, as she is a Scorpion Clan player");

        var s3 = Table(Phase.Action, 0);
        s3.Players[0].Clan = "Crane";
        var yoshihara3 = Add(s3, Yoshihara, 0, Zone.Battlefield, province: 0);
        var target3 = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        var honorBefore3 = e3.State.Players[1].Honor;
        Accept(e3, Offer(e3, 0, "yoshihara-dishonor", yoshihara3.Id, target3.Id));
        Eq(e3.State.Players[1].Honor, honorBefore3, "no Honor loss as a Crane Clan player");
    }

    private static void SugiharaPenalty()
    {
        var s = Table(Phase.Battle, 0);
        var sugihara = Add(s, Sugihara, 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 4: reaches exactly 0 after -4F
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "sugihara-penalty", sugihara.Id, target.Id));
        Eq(e.GetView(0).Players[1].Army.Single(c => c.Id == target.Id).Force, 0, "the target's Force is now 0");
        var additional = e.State.AdditionalActions.Where(g => g.PlayerId == 0).ToArray();
        Check(additional.Length == 1, "one additional Battle action was granted since the target's Force reached 0");

        // A weaker penalty that does not bring the target to 0 grants nothing.
        var s2 = Table(Phase.Battle, 0);
        var sugihara2 = Add(s2, Sugihara, 0, Zone.Battlefield, province: 0);
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4: -4F leaves 0 too, so use a stronger fixture instead
        Batch336Support.Force(target2, 3); // net Force 4+3-4=3, above 0
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "sugihara-penalty", sugihara2.Id, target2.Id));
        Check(e2.State.AdditionalActions.Count(g => g.PlayerId == 0) == 0, "no additional action when the target's Force is still above 0");
    }

    private static void TomaruAttack()
    {
        var s = Table(Phase.Battle, 0);
        var tomaru = Add(s, Tomaru, 0, Zone.Battlefield, province: 0);
        var enemy1 = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemy2 = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "tomaru-attack", tomaru.Id, enemy1.Id));
        Accept(e, new("pass", 1, e.Version));
        Check(CanUse(e, 0, "tomaru-attack", tomaru.Id), "usable a second time this turn");
        Accept(e, Offer(e, 0, "tomaru-attack", tomaru.Id, enemy2.Id));
        Accept(e, new("pass", 1, e.Version));
        Check(!CanUse(e, 0, "tomaru-attack", tomaru.Id), "spent after two uses this turn");
    }

    // Issue #803 (Mendon): a less experienced version in play never stops a more experienced one being recruited -- they are independent cards; only a copy of the same experience level does.
    private static void TomaruRecruit()
    {
        var s = Table(Phase.Dynasty, 0);
        var baseTomaru = Add(s, "oracle-l5r-8893", 0, Zone.Home, bowed: true);
        var exp = Add(s, Tomaru, 0, Zone.Province, province: 0); exp.FaceUp = true; s.Players[0].Provinces[0].CardId = exp.Id;
        var e = MatchEngine.FromTestState(s);
        Eq(e.QuotePurchase(0, exp.Id).Error, (string?)null, "bought for Gold alongside the base Tomaru");
        Check(e.QueryActions(0).Any(a => a.Type == "purchase" && a.Enabled && a.CardId == exp.Id), "the Buy option is enabled");
        Eq(e.QuoteOverlay(0, exp.Id, baseTomaru.Id).Error, (string?)null, "and may still overlay the base Tomaru");
        var s2 = Table(Phase.Dynasty, 0);
        Add(s2, Tomaru, 0, Zone.Home);
        var second = Add(s2, Tomaru, 0, Zone.Province, province: 0); second.FaceUp = true; s2.Players[0].Provinces[0].CardId = second.Id;
        Check(MatchEngine.FromTestState(s2).QuotePurchase(0, second.Id).Error is not null, "a second copy of the same experience level is still refused");
    }

    private static void MakotoLockAndHoldingDiscount()
    {
        var s = Table(Phase.Action, 0);
        var makoto = Add(s, Makoto, 0, Zone.Stronghold);
        var holding = CardCatalog.Cards.Values.First(c => c.Type == CardType.Holding && !c.ScenarioOnly);
        var holdingCard = Add(s, holding.Id, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.QuotePurchase(0, holdingCard.Id).Cost, Math.Max(0, holding.GoldCost - 1), "his Holdings enter play for 1 less Gold");

        // The Economic Home Battle bow: the target's controller decides.
        var s2 = Table(Phase.Battle, 0);
        var makoto2 = Add(s2, Makoto, 0, Zone.Home);
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(CanUse(e2, 0, "makoto-lock", makoto2.Id), "usable from home (Absent, Home Battle)");
        Accept(e2, Offer(e2, 0, "makoto-lock", makoto2.Id, target2.Id));
        Eq(e2.State.PendingEitherOr?.SpecId, EitherOrSpecs.MakotoPayOrDishonor);
        Eq(e2.State.PendingEitherOr?.DeciderPlayerId, 1, "the TARGET's controller decides, not Makoto's own controller");
        var honorBefore = e2.State.Players[1].Honor;
        Accept(e2, new("chooseEitherOr", 1, e2.Version, Choice: "refuse"));
        Check(e2.State.Cards[target2.Id].Dishonorable && e2.State.Cards[target2.Id].Zone == Zone.Home, "declining: dishonored and moved home");
        Eq(e2.State.Players[1].Honor, honorBefore, "no Honor lost by declining");
    }

    private static void KanayeBow()
    {
        var s = Table(Phase.Battle, 0);
        var kanaye = Add(s, Kanaye, 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        // Paying nothing: bows but does not destroy.
        Accept(e, Offer(e, 0, "kanaye-bow", kanaye.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].Zone == Zone.Battlefield, "bowed, not destroyed, when no Gold was paid");

        // Paying exactly the target's Gold Cost destroys it too. The Table() economy scenario's own Home/Stronghold production sources
        // (untouched by Table, which only clears hands) are used to pay it.
        var s2 = Table(Phase.Battle, 0);
        var kanaye2 = Add(s2, Kanaye, 0, Zone.Battlefield, province: 0);
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var goldCost = CardCatalog.Get(target2.DefinitionId).GoldCost;
        var e2 = MatchEngine.FromTestState(s2);
        var sources = e2.PaymentOptions(0, goldCost).First().SourceIds;
        Accept(e2, new("useAbility", 0, e2.Version, CardId: kanaye2.Id, AbilityId: "kanaye-bow", TargetId: target2.Id, GoldAmount: goldCost, SourceIds: sources));
        Check(e2.State.Cards[target2.Id].Zone == Zone.Dead, "destroyed once its own Gold Cost was paid");
    }
}
