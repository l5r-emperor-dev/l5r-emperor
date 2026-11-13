using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Honor and Treachery pass (issue #458): the cards of personality-wave-61, holding-wave-8, spell-wave-19, strategy-wave-51, stronghold-wave-13 and
// follower-wave-27, driven through QueryActions and the commands it offers. Every scenario is an explicit position on the economy table (player 0
// the Crane, player 1 the Lion), a Scorpion or Phoenix fixture Personality/Stronghold substituted in as each card needs.
internal static class HonorAndTreacheryPassTests
{
    private const string Shihiro = "oracle-l5r-10241", Irezu = "oracle-l5r-793", Kasumi = "oracle-l5r-10247", Utoro = "oracle-l5r-10249", Taiken = "oracle-l5r-10243",
        Mitsushen = "oracle-l5r-10245", Tsukimi = "oracle-l5r-10246", Chikata = "oracle-l5r-10250", Tsubo = "oracle-l5r-10252", Haruto = "oracle-l5r-10251",
        BorderKeep = "oracle-l5r-10240", Communion = "oracle-l5r-10254", DevotedYojimbo = "oracle-l5r-1992", EliteTraining = "oracle-l5r-10255",
        SacredTemple = "oracle-l5r-10256", TowersOfYogo = "oracle-l5r-8392";

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

    public static void Run(TestRunner runner) => runner.Run("honor-and-treachery", [
        ("Published shape: every card of the six waves prints its catalogue stats, keywords, Effect and ability ids, ships its art and is listed in its wave manifest", PublishedShape),
        ("Akodo Shihiro: before each of another player's actions resolves during a battle at his own battlefield, +1F until the turn ends -- unlimited uses, not at another battlefield, not his controller's own action, gone next turn", ShihiroBoost),
        ("Bayushi Irezu: from home, swaps places with another target Scorpion Clan Personality of his controller's -- refused while he is not at home, refused against a non-Scorpion; his Reaction substitutes him, bowed, for a Scorpion Clan Personality of his controller's another player's duel-issuing action targets, only while he is unbowed and at that location", IrezuMoveAndDuelSubstitute),
        ("Bayushi Kasumi: printed Elite only, contributing Force even while bowed", KasumiElite),
        ("Bayushi Utoro: even while bowed, discards a chosen hand card and straightens a target Personality -- the discard and the target are independent choices", UtoroStraighten),
        ("Border Keep - inexperienced: produces 2 Gold bowed; on the controller's first turn only, the province refresh is offered", BorderKeepFirstTurn),
        ("Communion with Earth: its wielder has Stalwart; its Battle transfers it to another Earth Shugenja of the same controller's, refusing a non-Earth or the current wielder", CommunionWithEarth),
        ("Devoted Yojimbo: bows an unbowed Follower or Personality of its controller's own army to straighten a target Follower or Personality of that same army -- refused for a card outside the army or already bowed", DevotedYojimboStraighten),
        ("Elite Training: permanently gives the chosen performer Elite", EliteTrainingGrant),
        ("Isawa Taiken: bows himself for a Ranged 4 Attack", TaikenAttack),
        ("Shiba Mitsushen: moves a target Personality of his controller's to his own battlefield and straightens the unit if it moved; already there, nothing moves and it is not straightened again", MitsushenMove),
        ("Shiba Tsukimi: twice per turn, straightens a target card in a unit and clears its Battle abilities' per-turn use so it may act again this turn", TsukimiRefresh),
        ("Shosuro Chikata: even while bowed, bows or straightens a target Scorpion Clan OR Dragon Clan Personality (either side) -- refused against a Personality of neither Clan", ChikataToggle),
        ("The Sacred Temple of the Phoenix - inexperienced: produces its printed base 3 Gold ordinarily, and 5 Gold restricted to that one card when paying to equip a Spell or recruit a Shugenja", SacredTempleProduction),
        ("The Towers of the Yogo: every Scorpion Clan Personality of its controller's, present or future, has its granted bow-to-bow ability -- gone the moment it leaves play, absent for a non-Scorpion or the opponent's Scorpion", TowersOfYogoGrant),
        ("Tsubo: even while bowed, straightens a target Personality of either side", TsuboStraighten),
        ("Yogo Haruto: attaches a Follower from his own discard pile to himself, paying 2 less Gold", HarutoAttach),
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

        var shihiro = CardCatalog.Get(Shihiro);
        Check(shihiro.Clan == "Lion" && shihiro.Force == 3 && shihiro.Chi == 4 && shihiro.GoldCost == 7 && Keywords(Shihiro).Contains("Lion Clan") && shihiro.Abilities!.SequenceEqual(["shihiro-boost"]), "Akodo Shihiro");
        var irezu = CardCatalog.Get(Irezu);
        Check(irezu.Clan == "Scorpion" && irezu.Force == 0 && irezu.Chi == 1 && irezu.GoldCost == 2 && Keywords(Irezu).Contains("Scorpion Clan") && irezu.Abilities!.SequenceEqual(["irezu-move", "irezu-duel-enter"]), "Bayushi Irezu");
        var kasumi = CardCatalog.Get(Kasumi);
        Check(kasumi.Clan == "Scorpion" && kasumi.Force == 4 && kasumi.Chi == 3 && kasumi.GoldCost == 7 && Keywords(Kasumi).Contains("Elite") && kasumi.Abilities!.Length == 0, "Bayushi Kasumi");
        var utoro = CardCatalog.Get(Utoro);
        Check(utoro.Force == 4 && utoro.Chi == 4 && utoro.GoldCost == 8 && utoro.Abilities!.SequenceEqual(["utoro-straighten"]) && AbilityCatalog.All["utoro-straighten"].Slots!.Length == 2, "Bayushi Utoro");
        var taiken = CardCatalog.Get(Taiken);
        Check(taiken.Clan == "Phoenix" && taiken.Force == 3 && taiken.Chi == 3 && taiken.GoldCost == 7 && taiken.Abilities!.SequenceEqual(["taiken-attack"]), "Isawa Taiken");
        var mitsushen = CardCatalog.Get(Mitsushen);
        Check(mitsushen.Clan == "Phoenix" && mitsushen.Force == 5 && mitsushen.Chi == 4 && mitsushen.GoldCost == 9 && mitsushen.Abilities!.SequenceEqual(["mitsushen-move"]), "Shiba Mitsushen");
        var tsukimi = CardCatalog.Get(Tsukimi);
        Check(tsukimi.Force == 6 && tsukimi.Chi == 5 && tsukimi.GoldCost == 10 && tsukimi.Abilities!.SequenceEqual(["tsukimi-refresh"]) && AbilityCatalog.All["tsukimi-refresh"].UsesPerTurn == 2, "Shiba Tsukimi");
        var chikata = CardCatalog.Get(Chikata);
        Check(chikata.Clan == "Scorpion" /* issue #492, ruling R5: Ninja is a keyword, never a Clan */ && Keywords(Chikata).Contains("Scorpion Clan") && chikata.Force == 3 && chikata.Chi == 3 && chikata.GoldCost == 7 && chikata.Abilities!.SequenceEqual(["chikata-bow", "chikata-straighten"]), "Shosuro Chikata");
        var tsubo = CardCatalog.Get(Tsubo);
        Check(tsubo.Force == 4 && tsubo.Chi == 4 && tsubo.GoldCost == 6 && tsubo.Abilities!.SequenceEqual(["tsubo-straighten"]), "Tsubo");
        var haruto = CardCatalog.Get(Haruto);
        Check(haruto.Clan == "Scorpion" && haruto.Force == 5 && haruto.Chi == 4 && haruto.GoldCost == 9 && haruto.Abilities!.SequenceEqual(["haruto-attach"]), "Yogo Haruto");
        Manifest(root, "personality-wave-61", Shihiro, Irezu, Kasumi, Utoro, Taiken, Mitsushen, Tsukimi, Chikata, Tsubo, Haruto);

        var borderKeep = CardCatalog.Get(BorderKeep);
        Check(borderKeep.Type == CardType.Holding && borderKeep.GoldProduction == 2 && borderKeep.Abilities!.SequenceEqual(["border-first"]), "Border Keep - inexperienced");
        Manifest(root, "holding-wave-8", BorderKeep);

        var communion = CardCatalog.Get(Communion);
        Check(communion.Type == CardType.Spell && Keywords(Communion) == "Earth" && communion.AttachGrantsKeyword == "Stalwart" && communion.Abilities!.SequenceEqual(["communion-transfer"]), "Communion with Earth");
        Manifest(root, "spell-wave-19", Communion);

        var yojimbo = CardCatalog.Get(DevotedYojimbo);
        Check(yojimbo.Type == CardType.Follower && yojimbo.Force == 2 && yojimbo.GoldCost == 3 && Keywords(DevotedYojimbo) == "Yojimbo" && yojimbo.Abilities!.SequenceEqual(["devoted-yojimbo-straighten"]), "Devoted Yojimbo");
        Manifest(root, "follower-wave-27", DevotedYojimbo);

        var eliteTraining = CardCatalog.Get(EliteTraining);
        Check(eliteTraining.Type == CardType.Strategy && eliteTraining.Abilities!.SequenceEqual(["elite-training-grant"]), "Elite Training");
        Manifest(root, "strategy-wave-51", EliteTraining);

        var sacredTemple = CardCatalog.Get(SacredTemple);
        Check(sacredTemple.Type == CardType.Stronghold && sacredTemple.Clan == "Phoenix" && sacredTemple.GoldProduction == 3 && sacredTemple.ProvinceStrength == 5
            && sacredTemple.Effect == "sacred-temple-phoenix-gold" && sacredTemple.Abilities!.Length == 0, "The Sacred Temple of the Phoenix - inexperienced");
        var towersOfYogo = CardCatalog.Get(TowersOfYogo);
        Check(towersOfYogo.Type == CardType.Stronghold && towersOfYogo.Clan == "Scorpion" && towersOfYogo.GoldProduction == 4 && towersOfYogo.ProvinceStrength == 7
            && towersOfYogo.Effect == "towers-of-yogo-grant" && towersOfYogo.Abilities!.Length == 0, "The Towers of the Yogo");
        Manifest(root, "stronghold-wave-13", SacredTemple, TowersOfYogo);
    }

    private static void ShihiroBoost()
    {
        var s = Table(Phase.Battle, 1);
        var shihiro = Add(s, Shihiro, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        int ShihiroForce(MatchEngine engine) => engine.GetView(0).Players[0].Army.Single(c => c.Id == shihiro.Id).Force;
        var baseForce = ShihiroForce(e);
        // Another player's Battle action resolving at his own battlefield: +1F, unlimited, not consumed by a first use.
        // The Reaction opens a before-action-resolves window (a Target "none" self-boost, so accepting its own offer applies it) before the enemy's action resolves.
        Accept(e, Use(e, 1, "training-bow-at-battle", enemy.Id, shihiro.Id));
        Check(e.State.PendingChoice?.Type == "beforeActionReaction", "the Reaction window opens");
        Accept(e, e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "shihiro-boost").Command!);
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(ShihiroForce(e) == baseForce + 1, "+1F after one of another player's actions resolved at his battlefield");
    }

    private static void IrezuMoveAndDuelSubstitute()
    {
        // The move: from home, swaps with another target Scorpion Clan Personality.
        var s = Table(Phase.Battle, 0);
        var irezu = Add(s, Irezu, 0, Zone.Home);
        var other = Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "irezu-move", irezu.Id, other.Id));
        Check(e.State.Cards[irezu.Id].Zone == Zone.Battlefield && e.State.Cards[irezu.Id].ProvinceIndex == 0, "Irezu moved to the current battlefield");
        Check(e.State.Cards[other.Id].Zone == Zone.Home, "the target Scorpion Clan Personality moved home");

        // Refused while not at home.
        var s2 = Table(Phase.Battle, 0);
        var atBattle = Add(s2, Irezu, 0, Zone.Battlefield, province: 0);
        var other2 = Add(s2, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "irezu-move", atBattle.Id), "Irezu at the battlefield already cannot use his Home ability");

        // Refused against a non-Scorpion.
        var s3 = Table(Phase.Battle, 0);
        var home3 = Add(s3, Irezu, 0, Zone.Home);
        var lion3 = Add(s3, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        var rejection = Rejection(e3, Use(e3, 0, "irezu-move", home3.Id, lion3.Id));
        Check(rejection.Length > 0, "a non-Scorpion Clan Personality is refused as the target");

        // The Reaction: another player's duel-issuing action targeting a Scorpion Clan Personality of Irezu's controller's, at his location, unbowed -- he substitutes himself, bowed as the cost.
        var s4 = Table(Phase.Battle, 1);
        var irezu4 = Add(s4, Irezu, 0, Zone.Battlefield, province: 0);
        var scorpion4 = Add(s4, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        scorpion4.GrantedKeywords.Add(new("Scorpion Clan", "test-fixture", GrantDuration.Permanent));
        var enemyDuelist = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duelStrategy = Add(s4, "training-duel-force", 1, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 1, "training-duel-force", duelStrategy.Id, scorpion4.Id, performerId: enemyDuelist.Id));
        // Issue #492 (ruling R5): the substitute enters only an accepted or mandatory duel: this Force duel is mandatory, so the "before the duel begins" window is open at once.
        Eq(e4.State.PendingChoice?.Type, "duelReaction", "the mandatory duel opens the before-the-duel window");
        var reaction = Enabled(e4, 0, "irezu-duel-enter", irezu4.Id);
        Accept(e4, reaction.Command!);
        Check(e4.State.Cards[irezu4.Id].Bowed, "Irezu bowed to pay his Reaction's cost");
        Check(e4.State.PendingDuel is { } pending && pending.ChallengedId == irezu4.Id && pending.ChallengerId == enemyDuelist.Id, "the duel now involves Irezu instead of the Scorpion Clan Personality");

        // Not offered once he is bowed.
        var s5 = Table(Phase.Battle, 1);
        var irezu5 = Add(s5, Irezu, 0, Zone.Battlefield, province: 0, bowed: true);
        var scorpion5 = Add(s5, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        scorpion5.GrantedKeywords.Add(new("Scorpion Clan", "test-fixture", GrantDuration.Permanent));
        var enemyDuelist5 = Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duelStrategy5 = Add(s5, "training-duel-force", 1, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Use(e5, 1, "training-duel-force", duelStrategy5.Id, scorpion5.Id, performerId: enemyDuelist5.Id));
        Check(!CanUse(e5, 0, "irezu-duel-enter", irezu5.Id), "a bowed Irezu cannot pay the Reaction's cost");
    }

    private static void KasumiElite()
    {
        var s = Table(Phase.Battle, 1);
        var kasumi = Add(s, Kasumi, 0, Zone.Battlefield, province: 0, bowed: true);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(kasumi.Id, "Elite"), "Kasumi has Elite");
    }

    private static void UtoroStraighten()
    {
        var s = Table(Phase.Battle, 0);
        var utoro = Add(s, Utoro, 0, Zone.Battlefield, province: 0, bowed: true);
        var target = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var handCard = Add(s, "training-weapon", 0, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var command = Enabled(e, 0, "utoro-straighten", utoro.Id).Command!;
        Accept(e, command with { TargetId = target.Id, Selections = [Pick("target", target.Id), Pick("discard", handCard.Id)] });
        Check(!e.State.Cards[target.Id].Bowed, "the target straightened");
        Check(e.State.Cards[handCard.Id].Zone == Zone.FateDiscard, "the chosen hand card was discarded");
        Check(e.State.Cards[utoro.Id].Bowed, "Utoro himself stays bowed: he acted even while bowed, no unbowing effect");
    }

    private static void BorderKeepFirstTurn()
    {
        var s = Table(Phase.Action, 0);
        s.Players[0].TurnsTaken = 1;
        var keep = Add(s, BorderKeep, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(keep.Id, 0), 2, "produces its printed 2 Gold");
        Check(CanUse(e, 0, "border-first"), "the first-turn province refresh is offered on turn one");
        var later = Edit(e, st => st.Players[0].TurnsTaken = 2);
        Check(!CanUse(later, 0, "border-first"), "not offered from the second turn on");
    }

    private static void CommunionWithEarth()
    {
        var s = Table(Phase.Battle, 0);
        var earth1 = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0); earth1.GrantedKeywords.Add(new("Earth", "test-fixture", GrantDuration.Permanent));
        var earth2 = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0); earth2.GrantedKeywords.Add(new("Earth", "test-fixture", GrantDuration.Permanent));
        var water = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0);
        var communion = Add(s, Communion, 0, Zone.Attached, province: 0, parent: earth1.Id);
        // AttachGrantsKeyword is applied once, at the moment of attaching (ExecuteAttach/Equip) -- a fixture placed pre-attached needs its own grant simulated.
        earth1.GrantedKeywords.Add(new("Stalwart", communion.Id, GrantDuration.Attachment));
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(earth1.Id, "Stalwart"), "the current wielder has Stalwart");
        Accept(e, Offer(e, 0, "communion-transfer", communion.Id, earth2.Id));
        Check(e.State.Cards[communion.Id].AttachedTo == earth2.Id, "Communion with Earth transferred to the other Earth Shugenja");
        Check(e.HasKeyword(earth2.Id, "Stalwart") && !e.HasKeyword(earth1.Id, "Stalwart"), "Stalwart moved with it (TransferAttachment's own AttachGrantsKeyword re-apply)");
        Check(!CanUse(e, 0, "communion-transfer", cardId: communion.Id, performerId: water.Id), "a non-Earth Shugenja is not a legal target");
    }

    private static void DevotedYojimboStraighten()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var yojimbo = Add(s, DevotedYojimbo, 0, Zone.Attached, province: 0, parent: host.Id);
        var bowedAlly = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var outsideArmy = Add(s, "training-lion-samurai", 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "devoted-yojimbo-straighten", yojimbo.Id, bowedAlly.Id, performerId: yojimbo.Id));
        Check(!e.State.Cards[bowedAlly.Id].Bowed, "the ally straightened");
        Check(e.State.Cards[yojimbo.Id].Bowed, "Devoted Yojimbo bowed as its own cost");
        Check(!CanUse(e, 0, "devoted-yojimbo-straighten", performerId: outsideArmy.Id), "a Personality outside the army cannot perform");
    }

    private static void EliteTrainingGrant()
    {
        var s = Table(Phase.Action, 0);
        var performer = Add(s, "training-lion-samurai", 0, Zone.Home);
        performer.Bowed = true; // "your performing Personality" prints no "unbowed" (R-035, issue #1228)
        var training = Add(s, EliteTraining, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "elite-training-grant", training.Id, performerId: performer.Id));
        Check(e.HasKeyword(performer.Id, "Elite"), "permanently given Elite, performed by a bowed Personality");
    }

    private static void TaikenAttack()
    {
        var s = Table(Phase.Battle, 0);
        var taiken = Add(s, Taiken, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "taiken-attack", taiken.Id, enemy.Id));
        Check(e.State.Cards[taiken.Id].Bowed, "Taiken bowed for the Attack");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "the Ranged 4 Attack destroyed the enemy Personality");
    }

    private static void MitsushenMove()
    {
        var s = Table(Phase.Battle, 0);
        var mitsushen = Add(s, Mitsushen, 0, Zone.Battlefield, province: 0);
        var ally = Add(s, "training-lion-samurai", 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "mitsushen-move", mitsushen.Id, ally.Id));
        Check(e.State.Cards[ally.Id].Zone == Zone.Battlefield && e.State.Cards[ally.Id].ProvinceIndex == 0 && !e.State.Cards[ally.Id].Bowed, "the ally moved to Mitsushen's battlefield and straightened");
    }

    private static void TsukimiRefresh()
    {
        var s = Table(Phase.Battle, 0);
        var tsukimi = Add(s, Tsukimi, 0, Zone.Battlefield, province: 0);
        var taiken = Add(s, Taiken, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemy2 = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        // Spend Taiken's own once-per-turn Attack first: its BowSource cost bows him, and his use is spent for the turn.
        Accept(e, Offer(e, 0, "taiken-attack", taiken.Id, enemy.Id));
        Check(e.State.Cards[taiken.Id].Bowed, "Taiken bowed to pay his Attack's cost");
        Check(!CanUse(e, 0, "taiken-attack", taiken.Id), "his once-per-turn Attack is spent for the turn");
        // Priority passed to the enemy after Taiken's action; his pass returns it to player 0 for a second Battle action.
        Accept(e, new("pass", 1, e.Version));
        Accept(e, Offer(e, 0, "tsukimi-refresh", tsukimi.Id, taiken.Id));
        Check(!e.State.Cards[taiken.Id].Bowed, "the targeted card straightened");
        Accept(e, new("pass", 1, e.Version));
        Check(CanUse(e, 0, "taiken-attack", taiken.Id), "its Battle ability's per-turn use was cleared, so it may act again this turn");
    }

    private static void ChikataToggle()
    {
        // The bow half, against a Scorpion Clan target -- and, sharing one use with the straighten half (UsageKey), the straighten half is spent for the turn afterward.
        var s = Table(Phase.Battle, 0);
        var chikata = Add(s, Chikata, 0, Zone.Battlefield, province: 0, bowed: true);
        var scorpionTarget = Add(s, "training-scorpion-veteran", 1, Zone.Battlefield, province: 0);
        scorpionTarget.GrantedKeywords.Add(new("Scorpion Clan", "test-fixture", GrantDuration.Permanent));
        var neither = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "chikata-bow", chikata.Id).Any(o => o.TargetIds?.Contains(neither.Id) == true), "a Personality of neither Clan is not a legal target");
        Accept(e, Offer(e, 0, "chikata-bow", chikata.Id, scorpionTarget.Id));
        Check(e.State.Cards[scorpionTarget.Id].Bowed, "the Scorpion Clan target bowed");
        Check(e.State.Cards[chikata.Id].Bowed, "Chikata himself acted even while bowed");
        Check(!CanUse(e, 0, "chikata-straighten", chikata.Id), "the shared use is spent for the turn");

        // The straighten half, against a Dragon Clan target, in a fresh turn.
        var s2 = Table(Phase.Battle, 0);
        var chikata2 = Add(s2, Chikata, 0, Zone.Battlefield, province: 0, bowed: true);
        var dragonTarget = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        dragonTarget.GrantedKeywords.Add(new("Dragon Clan", "test-fixture", GrantDuration.Permanent));
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "chikata-straighten", chikata2.Id, dragonTarget.Id));
        Check(!e2.State.Cards[dragonTarget.Id].Bowed, "the Dragon Clan target straightened");
    }

    private static void SacredTempleProduction()
    {
        var s = Table(Phase.Action, 0);
        var temple = Add(s, SacredTemple, 0, Zone.Stronghold);
        s.Players[0].StrongholdId = temple.Id;
        var spell = CardCatalog.Cards.Values.First(c => c.Type == CardType.Spell && !c.ScenarioOnly);
        var shugenja = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Keywords?.Contains("Shugenja") == true);
        var strategy = CardCatalog.Cards.Values.First(c => c.Type == CardType.Strategy && !c.ScenarioOnly);
        var spellCard = Add(s, spell.Id, 0, Zone.Hand);
        var shugenjaCard = Add(s, shugenja.Id, 0, Zone.Hand);
        var strategyCard = Add(s, strategy.Id, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(temple.Id, 0), 3, "its printed base production, with no payment named");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(spellCard, PaymentKind.Equip)), 5, "5 Gold, restricted, for paying to equip a Spell");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(shugenjaCard, PaymentKind.Recruit)), 5, "5 Gold, restricted, for paying to recruit a Shugenja");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(strategyCard, PaymentKind.Play)), 3, "its ordinary base production for a Strategy played from hand");
    }

    private static void TowersOfYogoGrant()
    {
        var s = Table(Phase.Battle, 0);
        var stronghold = Add(s, TowersOfYogo, 0, Zone.Stronghold);
        s.Players[0].StrongholdId = stronghold.Id;
        var scorpion = Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        var lion = Add(s, "training-lion-samurai", 0, Zone.Home);
        var enemyScorpion = Add(s, "training-scorpion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(scorpion.Id, "Elite") == false, "sanity: no unrelated grant");
        Accept(e, Offer(e, 0, "towers-of-yogo-bow", scorpion.Id, enemyScorpion.Id));
        Check(e.State.Cards[scorpion.Id].Bowed && e.State.Cards[enemyScorpion.Id].Bowed, "the granted ability bowed itself and the target");
        Check(!CanUse(e, 0, "towers-of-yogo-bow", lion.Id), "a non-Scorpion Clan Personality of his controller's has no granted ability");
        var noStronghold = Edit(e, st => { st.Cards[stronghold.Id].Zone = Zone.Dead; });
        Check(!CanUse(noStronghold, 0, "towers-of-yogo-bow", scorpion.Id), "with the Stronghold gone the grant is gone");
    }

    private static void TsuboStraighten()
    {
        var s = Table(Phase.Battle, 0);
        var tsubo = Add(s, Tsubo, 0, Zone.Battlefield, province: 0, bowed: true);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "tsubo-straighten", tsubo.Id, target.Id));
        Check(!e.State.Cards[target.Id].Bowed, "the target straightened");
        Check(e.State.Cards[tsubo.Id].Bowed, "Tsubo himself acted even while bowed");
    }

    private static void HarutoAttach()
    {
        var s = Table(Phase.Battle, 0);
        var haruto = Add(s, Haruto, 0, Zone.Battlefield, province: 0);
        var follower = CardCatalog.Cards.Values.First(c => c.Type == CardType.Follower && !c.ScenarioOnly && c.GoldCost >= 2);
        var followerCard = Add(s, follower.Id, 0, Zone.FateDiscard);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "haruto-attach", haruto.Id, followerCard.Id));
        Check(e.State.Cards[followerCard.Id].Zone == Zone.Attached && e.State.Cards[followerCard.Id].AttachedTo == haruto.Id, "the Follower attached to Haruto");
    }
}
