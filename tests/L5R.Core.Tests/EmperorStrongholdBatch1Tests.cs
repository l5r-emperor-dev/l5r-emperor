using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Stronghold pass (issue #486), batch 1: the seven Strongholds of stronghold-wave-17 (Eternal Victory Dojo, Hidden Falls Dojo, Keep of the Dead, Koshin Keep, Kyuden Otomo - exp,
// Library of Rebirth, Midday Shadow Court), driven through QueryActions and the commands it offers. Halls of the Forgotten was not wired here (it waited for the shared delay mechanism); it is wired and published in stronghold-wave-20 (issue #492, ruling pass R2).
internal static class EmperorStrongholdBatch1Tests
{
    private const string EternalVictory = "oracle-l5r-2388", HallsOfTheForgotten = "oracle-l5r-2950", HiddenFalls = "oracle-l5r-3257", KeepOfTheDead = "oracle-l5r-4310", KoshinKeep = "oracle-l5r-4541",
        KyudenOtomo = "oracle-l5r-4646", LibraryOfRebirth = "oracle-l5r-4724", MiddayShadowCourt = "oracle-l5r-5037", Shunya = "oracle-l5r-2158";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardInstance StrongholdOf(MatchState s, int player, string definition) { var card = s.Cards[s.Players[player].StrongholdId]; card.DefinitionId = definition; return card; }
    private static void Pass(MatchEngine e) => Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));

    public static void Run(TestRunner runner) => runner.Run("emperor-stronghold-batch1", [
        ("Published shape: seven Strongholds print their stats, keywords, Effect and ability ids, ship their art and are listed in stronghold-wave-17's manifest; Halls of the Forgotten is published in stronghold-wave-20", PublishedShape),
        ("Eternal Victory Dojo: after your Tactical action resolves, a card is drawn after the next End Phase begins -- not for a non-Tactical action or another player's Tactical action; owed to its controller at whoever's End Phase", EternalVictoryDraw),
        ("Eternal Victory Dojo: the deck may hold four copies each of one or two non-Unique Strategies with Battle abilities -- not of a third, not without the Stronghold, not of a Unique or non-Battle Strategy", EternalVictoryDeckRule),
        ("Keep of the Dead: no Honor loss from Spider Clan and Fate cards you own, and Follower Honor Requirements are ignored -- for its controller only", KeepOfTheDeadStatics),
        ("Keep of the Dead: the performing Commander moves to the current battlefield if enemy units are there (or he is assigned there) and his unit straightens if he moved -- Absent, only a Commander", KeepOfTheDeadMove),
        ("Koshin Keep: Reconnaissance after engaging; then, this battle, a Ranged Attack that does not destroy its target reduces its Force by the strength and allows one more Ranged Attack on the same card only", KoshinKeepRanged),
        ("Kyuden Otomo - exp: taking the Imperial Favor lets its controller discard it for a Favor token; a Courtier bows to add one; a token may be discarded in place of the Favor as a cost", KyudenOtomoTokens),
        ("Kyuden Otomo - exp: Favor tokens on it never stop its controller lobbying; only holding the Imperial Favor itself does (Mendon, issue #1272)", KyudenOtomoLobby),
        ("Library of Rebirth: produces 4 Gold for a Spell during the Combat Segment even while bowed and without bowing; its Battle action straightens a performing Shugenja and grants an additional Battle action", LibraryGold),
        ("Library of Rebirth and Small Farm pay for The Swell of Strength in a battle: Equip (Battle) is offered for an unbowed opposed Water Shugenja, with the Library bowed or not", LibrarySwellOfStrength),
        ("The Swell of Strength: a Shugenja of another element may equip it in a battle, the bowed Library of Rebirth and a Small Farm pay for it, and only its Focus Effect is limited to Water Shugenja", SwellEquipByNonWaterShugenja),
        ("Midday Shadow Court: Lobby treats its controller's Honor as 20 higher; its Political Open (even bowed) dishonors an enemy Personality and costs his controller 1 Honor when he is next assigned or an action he performed ends", MiddayWatch),
        ("Hidden Falls Dojo: Reconnaissance at one or two provinces' battlefields and, after the Cavalry Maneuvers Segment, an additional Infantry Maneuvers Segment for its controller's Scouts, to those provinces only", HiddenFallsSegment),
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
        void Stronghold(string id, string name, string clan, int production, int strength, int honor, string effect, string[] abilities, params string[] keywords)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Stronghold && c.Name == name && c.Clan == clan && c.GoldProduction == production && c.ProvinceStrength == strength && c.StartingHonor == honor && c.GoldCost == 0,
                $"{name}: type={c.Type} clan={c.Clan} production={c.GoldProduction} strength={c.ProvinceStrength} honor={c.StartingHonor}");
            Eq(c.Effect, effect, $"{name}'s effect");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{name}'s abilities");
            Eq(string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)), string.Join(",", keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}'s keywords");
        }
        Stronghold(EternalVictory, "Eternal Victory Dojo", "Lion", 3, 7, 7, "eternal-victory-dojo", ["eternal-victory-draw"], "Dojo");
        Stronghold(HiddenFalls, "Hidden Falls Dojo", "Crane", 4, 7, 5, "none", ["hidden-falls-recon"], "Dojo");
        Stronghold(KeepOfTheDead, "Keep of the Dead", "Spider", 4, 7, 0, "keep-of-the-dead", ["keep-of-dead-move"]);
        Stronghold(KoshinKeep, "Koshin Keep", "Mantis", 4, 7, 2, "none", ["koshin-keep-recon"]);
        Stronghold(KyudenOtomo, "Kyuden Otomo", "Crane", 4, 6, 6, "kyuden-otomo-exp", ["otomo-favor-token", "otomo-courtier-token"], "Experienced", "Imperial");
        Stronghold(LibraryOfRebirth, "Library of Rebirth", "Phoenix", 4, 6, 6, "none", ["library-rebirth-gold", "library-rebirth-straighten"]);
        Stronghold(MiddayShadowCourt, "Midday Shadow Court", "Scorpion", 4, 7, 1, "midday-shadow-court", ["midday-shadow-watch"]);
        Manifest(root, "stronghold-wave-17", EternalVictory, HiddenFalls, KeepOfTheDead, KoshinKeep, KyudenOtomo, LibraryOfRebirth, MiddayShadowCourt);
        Eq(PublishedContent.StrongholdWave17.Cards.Length, 7, "stronghold-wave-17 holds exactly the seven wired Strongholds");
        // Issue #492, ruling pass R2: Halls of the Forgotten is wired now (the shared delay mechanism) and published in stronghold-wave-20 (EmperorRulingR2Tests).
        Check(CardCatalog.Cards.ContainsKey(HallsOfTheForgotten) && PublishedContent.StrongholdWave20.Cards.Any(c => c.Id == HallsOfTheForgotten), "Halls of the Forgotten is published in stronghold-wave-20");
        // The scans' own wording (the catalogue's reworded Oracle text differs for Hidden Falls Dojo and Library of Rebirth).
        Check(CardCatalog.Get(HiddenFalls).Text.Contains("additional Infantry Maneuvers Segment") && CardCatalog.Get(HiddenFalls).Text.Contains("After each Cavalry Maneuvers Segment"), "Hidden Falls Dojo prints the scan's text");
        Check(CardCatalog.Get(LibraryOfRebirth).Text.Contains("when paying for a Spell during the Combat Segment"), "Library of Rebirth prints the scan's text");
        Eq(AbilityCatalog.All["hidden-falls-recon"].Timing, "Open"); Eq(AbilityCatalog.All["hidden-falls-recon"].ActionKeywords?.Single(), "Recon");
        Eq(AbilityCatalog.All["koshin-keep-recon"].Timing, "Reaction"); Eq(AbilityCatalog.All["koshin-keep-recon"].Trigger, "after-engaging");
        Eq(AbilityCatalog.All["midday-shadow-watch"].ActionKeywords?.Single(), "Political");
    }

    // ---- Eternal Victory Dojo ---------------------------------------------------------------------------------------------------------------------------
    private static void EternalVictoryDraw()
    {
        // Player 0's own Tactical action (a Strategy granted the Tactical keyword, Akodo Kobi's fixture): the after-action window offers the Dojo's Reaction.
        MatchState Setup(bool tacticalKeyword, int actor)
        {
            var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor;
            StrongholdOf(s, 0, EternalVictory);
            var strategy = Add(s, "training-performer-strategy", actor, Zone.Hand);
            if (tacticalKeyword) GiveKeyword(strategy, "Tactical");
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            s.Cards.Values.First(c => c.OwnerId == actor && c.Zone == Zone.Battlefield).Bowed = false;
            return s;
        }
        var s1 = Setup(true, 0);
        var strategy1 = s1.Cards.Values.Single(c => c.Zone == Zone.Hand && c.OwnerId == 0);
        var performer1 = s1.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var dojo = s1.Cards[s1.Players[0].StrongholdId];
        var e = MatchEngine.FromTestState(s1);
        Accept(e, Enabled(e, 0, "training-performer-boost", strategy1.Id, performerId: performer1.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for the Tactical action");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "eternal-victory-draw", dojo.Id).Command!);
        Eq(string.Join(",", e.State.PendingEndPhaseDraws), "0", "a draw is owed to the Dojo's controller");
        Eq(e.State.Players[0].Hand.Count, handBefore, "nothing is drawn yet");

        // Not for a non-Tactical action of its controller's, nor for another player's Tactical action.
        var s2 = Setup(false, 0);
        var e2 = MatchEngine.FromTestState(s2);
        var strategy2 = s2.Cards.Values.Single(c => c.Zone == Zone.Hand && c.OwnerId == 0);
        Accept(e2, Enabled(e2, 0, "training-performer-boost", strategy2.Id, performerId: s2.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Id).Command!);
        Check(e2.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(e2, 0, "eternal-victory-draw"), "a non-Tactical action does not trigger the Dojo");
        var s3 = Setup(true, 1);
        var e3 = MatchEngine.FromTestState(s3);
        var strategy3 = s3.Cards.Values.Single(c => c.Zone == Zone.Hand && c.OwnerId == 1);
        Accept(e3, Enabled(e3, 1, "training-performer-boost", strategy3.Id, performerId: s3.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id).Command!);
        Check(!CanUse(e3, 0, "eternal-victory-draw"), "another player's Tactical action does not trigger the Dojo");

        // The draw comes as the next End Phase begins -- the Dojo controller's own, or the other player's -- before the ordinary End Phase draw.
        foreach (var active in new[] { 0, 1 })
        {
            var s = Table(Phase.Dynasty, active); s.PriorityPlayerId = active; s.PendingEndPhaseDraws = [0];
            foreach (var player in s.Players) { player.Hand.Clear(); }
            var end = MatchEngine.FromTestState(s);
            var owner = end.State.Players[0].Hand.Count; var other = end.State.Players[1].Hand.Count;
            Accept(end, new GameCommand("pass", active, end.Version));
            Eq(end.State.Players[0].Hand.Count, owner + (active == 0 ? 2 : 1), $"the Dojo's controller drew the owed card as {(active == 0 ? "his own" : "the opponent's")} End Phase began (plus his ordinary draw on his own turn)");
            Eq(end.State.Players[1].Hand.Count, other + (active == 1 ? 1 : 0), "the other player drew only his ordinary card");
            Check(end.State.PendingEndPhaseDraws.Count == 0, "the owed draw is spent");
        }
    }

    private static void EternalVictoryDeckRule()
    {
        // The definition-derived flags the deck builder reads.
        Check(DeckWorkshop.StrategyCopyRuleOf(CardCatalog.Get(EternalVictory)) && !DeckWorkshop.StrategyCopyRuleOf(CardCatalog.Get(KeepOfTheDead)), "only Eternal Victory Dojo prints the rule");
        Check(DeckWorkshop.BattleStrategyOf(CardCatalog.Get("training-performer-strategy")) && !DeckWorkshop.BattleStrategyOf(CardCatalog.Get("training-focus-strategy")) && !DeckWorkshop.BattleStrategyOf(CardCatalog.Get(EternalVictory)),
            "a Strategy with a Battle ability is a Battle Strategy; one without, and a Stronghold, is not");

        DeckCardInfo Strategy(string id, bool battle, params string[] keywords) => new(id, id, ["Strategy"], ["Fate"], keywords, true, BattleStrategy: battle);
        DeckWorkshop Workshop(bool dojo)
        {
            var pool = new List<DeckCardInfo>
            {
                new("SH-Dojo", "Test Dojo", ["Stronghold"], ["Other"], ["Dojo"], true, StrategyCopyRule: true),
                new("SH-Plain", "Test Stronghold", ["Stronghold"], ["Other"], [], true),
                Strategy("Battle A", true), Strategy("Battle B", true), Strategy("Battle C", true), Strategy("Unique Battle", true, "Unique"), Strategy("Open Only", false),
            };
            var w = new DeckWorkshop(pool);
            w.SelectSetup(dojo ? "Test Dojo" : "Test Stronghold");
            return w;
        }
        bool Flagged(DeckWorkshop w, string card) => w.Review().OverLimit.Contains(card);

        var withDojo = Workshop(true);
        withDojo.Adjust("Battle A", 4); withDojo.Adjust("Battle B", 4);
        Check(!Flagged(withDojo, "Battle A") && !Flagged(withDojo, "Battle B"), "four copies each of two Battle Strategies are legal with the Dojo");
        withDojo.Adjust("Battle C", 4);
        Check(Flagged(withDojo, "Battle C") && !Flagged(withDojo, "Battle A"), "a third title with four copies is not (the two most-copied keep the exception)");
        withDojo.Adjust("Battle A", 1);
        Check(Flagged(withDojo, "Battle A"), "five copies exceed the Dojo's four");
        var noDojo = Workshop(false); noDojo.Adjust("Battle A", 4);
        Check(Flagged(noDojo, "Battle A"), "without the Dojo four copies exceed the usual three");
        var other = Workshop(true); other.Adjust("Open Only", 4); other.Adjust("Unique Battle", 2);
        Check(Flagged(other, "Open Only") && Flagged(other, "Unique Battle"), "a Strategy without a Battle ability, and a Unique one, gain nothing");
        Check(withDojo.Review().Warnings.Any(w => w.Contains("Battle C")), "the excess is reported as a warning");
    }

    // ---- Keep of the Dead ---------------------------------------------------------------------------------------------------------------------------------
    private static void KeepOfTheDeadStatics()
    {
        // A Fate (Strategy) card's Honor cost for its own controller is negated; without the Stronghold it applies.
        foreach (var keep in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            if (keep) StrongholdOf(s, 0, KeepOfTheDead);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-honor-cost-strategy", 0, Zone.Hand);
            var before = s.Players[0].Honor;
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: strategy.Id, AbilityId: "training-honor-cost-attack"));
            Eq(e.State.Players[0].Honor, keep ? before : before - 2, keep ? "no Honor loss from a Fate card you own" : "the Honor cost applies without the Stronghold");
        }
        // ... and a Spider Clan Personality's own Honor loss (a Reaction on entering play).
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            StrongholdOf(s, 0, KeepOfTheDead);
            Add(s, "training-marketplace", 0, Zone.Home);
            var cardId = s.Players[0].Provinces[0].CardId!;
            s.Cards[cardId].DefinitionId = "training-spider-honor-loss"; s.Cards[cardId].FaceUp = true;
            var before = s.Players[0].Honor;
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "training-honor-loss-entry"));
            Eq(e.State.Players[0].Honor, before, "no Honor loss from a Spider Clan card you own");
        }
        // Only for its controller: the opponent's Fate card still costs the opponent Honor when the Keep belongs to the other player.
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            StrongholdOf(s, 0, KeepOfTheDead);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
            var before = s.Players[1].Honor;
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: strategy.Id, AbilityId: "training-honor-cost-attack"));
            Eq(e.State.Players[1].Honor, before - 2, "the other player's Fate cards are not covered");
        }
        // Follower Honor Requirements are ignored: a Follower needing 1 Personal Honor attaches to a Personality with 0 (dishonorable), for the Keep's controller only.
        foreach (var (owner, keep) in new[] { (0, true), (0, false), (1, true) })
        {
            var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner;
            if (keep) StrongholdOf(s, 0, KeepOfTheDead);
            var host = Add(s, owner == 0 ? "training-crane-samurai" : "training-lion-samurai", owner, Zone.Home); host.Dishonorable = true;
            var follower = Add(s, "training-follower", owner, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var command = new GameCommand("equip", owner, e.Version, CardId: follower.Id, SourceIds: e.PaymentOptions(owner, 2).First().SourceIds, TargetId: host.Id);
            if (keep && owner == 0) { Accept(e, command); Eq(e.State.Cards[follower.Id].AttachedTo, host.Id, "attached despite the Honor Requirement"); }
            else Check(!e.Execute(command).Accepted, $"without the Keep's control ({(keep ? "opponent's" : "no")} Keep) the Honor Requirement stands");
        }
    }

    private static void KeepOfTheDeadMove()
    {
        MatchState Setup(bool enemyThere, out CardInstance keep, out CardInstance commander)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            keep = StrongholdOf(s, 0, KeepOfTheDead);
            commander = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(commander, "Commander");
            if (enemyThere) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return s;
        }
        var s1 = Setup(true, out var keep1, out var commander1);
        var plain = Add(s1, "training-crane-veteran", 0, Zone.Home);
        var follower = Add(s1, "training-follower", 0, Zone.Attached, bowed: true, province: null, parent: commander1.Id);
        var e1 = MatchEngine.FromTestState(s1);
        Check(Options(e1, 0, "keep-of-dead-move", keep1.Id, plain.Id).All(o => !o.Enabled), "only a Commander may perform it");
        Accept(e1, Enabled(e1, 0, "keep-of-dead-move", keep1.Id, commander1.Id).Command!);
        Eq(e1.State.Cards[commander1.Id].Zone, Zone.Battlefield, "the Commander moved to the current battlefield without any unit of his controller there");
        Eq(e1.State.Cards[commander1.Id].ProvinceIndex, 0);
        Check(!e1.State.Cards[commander1.Id].Bowed && !e1.State.Cards[follower.Id].Bowed, "his unit straightened as he moved");
        Eq(e1.State.Cards[follower.Id].ProvinceIndex, 0, "his Follower went with him");

        // No enemy unit there: nothing happens (the action is taken, the Commander stays home and bowed).
        var s2 = Setup(false, out var keep2, out var commander2);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "keep-of-dead-move", keep2.Id, commander2.Id).Command!);
        Eq(e2.State.Cards[commander2.Id].Zone, Zone.Home, "no enemy units at the battlefield: he did not move");
        Check(e2.State.Cards[commander2.Id].Bowed, "and was not straightened");

        // Already assigned there ("he assigned there"): he stays, nothing straightens (he did not move).
        var s3 = Setup(true, out var keep3, out var commander3);
        commander3.Zone = Zone.Battlefield; commander3.ProvinceIndex = 0;
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "keep-of-dead-move", keep3.Id, commander3.Id).Command!);
        Check(e3.State.Cards[commander3.Id].Bowed && e3.State.Cards[commander3.Id].ProvinceIndex == 0, "a Commander already there does not move, so nothing straightens");
    }

    // ---- Koshin Keep ------------------------------------------------------------------------------------------------------------------------------------------
    private static void KoshinKeepRanged()
    {
        // Player 0 attacks and, after engaging, uses the Keep (or not). The target's destruction is negated for the turn (a destruction shield, Megumi's Guidance's shape), so a legal
        // Ranged 2 Attack against a 2 Force target does not destroy it -- the case the Keep's additional effects are for.
        MatchEngine Engaged(bool useKeep, bool shielded, out CardInstance keep, out CardInstance archer, out CardInstance target, out CardInstance other)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
            keep = StrongholdOf(s, 0, KoshinKeep);
            archer = Add(s, "training-recon-actor", 0, Zone.Battlefield, province: 0);
            Add(s, "training-recon-actor", 0, Zone.Battlefield, province: 0); // a second archer: each card's ability is usable once per turn
            target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(target, 2 - CardCatalog.Get("training-lion-samurai").Force);
            if (shielded) target.DestructionShieldTurn = s.TurnNumber;
            other = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Force(other, 2 - CardCatalog.Get("training-lion-veteran").Force);
            if (shielded) other.DestructionShieldTurn = s.TurnNumber;
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens for the Keep's Reaction");
            if (useKeep) Accept(e, Enabled(e, 0, "koshin-keep-recon", keep.Id).Command!);
            while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.Phase, Phase.Battle);
            while (e.State.PriorityPlayerId != 0) Pass(e); // the Defender passes; player 0 acts
            return e;
        }
        void Settle(MatchEngine e) { while (e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }
        int Reductions(MatchEngine e, CardInstance card) => e.State.Cards[card.Id].Modifiers.Count(m => m.Effect == "koshin-keep-recon");

        var e = Engaged(true, true, out var keep, out var archer, out var target, out var other);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "the Keep's controller has Reconnaissance at the current battlefield");
        Accept(e, Offer(e, 0, "training-recon-attack", archer.Id, target.Id));
        Settle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the Ranged 2 Attack did not destroy its (shielded) target");
        Eq(Reductions(e, target), 1, "its Force was reduced by the Attack's strength");
        Eq(e.State.Cards[target.Id].Modifiers.Single(m => m.Effect == "koshin-keep-recon").Amount, -2, "by 2");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.RangedTargetId == target.Id), "an additional Ranged Attack on the same card was granted");
        Eq(e.State.PriorityPlayerId, 0, "its controller keeps the action");
        // Only a Ranged Attack on the same card is allowed now: not another target, not another kind of action.
        var archer2 = e.State.Cards.Values.Single(c => c.DefinitionId == "training-recon-actor" && c.Id != archer.Id);
        var refusal = Rejection(e, new GameCommand("useAbility", 0, e.Version, CardId: archer2.Id, TargetId: other.Id, AbilityId: "training-recon-attack"));
        Check(refusal.Contains("Koshin Keep"), "another target is refused: " + refusal);
        Check(!CanUse(e, 0, "training-recon-duel", archer.Id), "another kind of Battle action is unavailable");
        Check(Options(e, 0, "training-recon-attack", archer2.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(target.Id) ?? false) && !(o.TargetIds?.Contains(other.Id) ?? false)), "the Attack is offered against the same card only");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: archer2.Id, TargetId: target.Id, AbilityId: "training-recon-attack"));
        Settle(e);
        Eq(Reductions(e, target), 2, "the additional Attack also did not destroy its target: reduced again, and (still) another additional Ranged Attack on the same card");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.RangedTargetId == target.Id));
        // Nor may a Terrain be played from the hand meanwhile.
        var eTerrain = Edit(e, st => Add(st, "oracle-l5r-2996", 0, Zone.Hand));
        var terrain = eTerrain.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-2996");
        Check(Rejection(eTerrain, new GameCommand("playStrategy", 0, eTerrain.Version, terrain.Id)).Contains("Koshin Keep"), "a Terrain may not be played in place of the additional Ranged Attack");
        // Passing forfeits it.
        Pass(e);
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "passing forfeits the additional action");

        // An Attack that destroys its target has none of the additional effects.
        var d = Engaged(true, false, out _, out var archerD, out var targetD, out _);
        Accept(d, Offer(d, 0, "training-recon-attack", archerD.Id, targetD.Id));
        Settle(d);
        Check(!d.State.Cards.ContainsKey(targetD.Id) || d.State.Cards[targetD.Id].Zone != Zone.Battlefield, "the Attack destroyed its target");
        Check(!d.State.AdditionalActions.Any(), "so no additional Ranged Attack is granted");
        // Core rules (issue #488): a target whose Force is above the strength is legal to target, and the Attack simply fails to destroy it -- the Keep's main use, with no destruction shield needed.
        var strong0 = Engaged(true, false, out _, out var archerS, out var targetS, out _);
        var strong = Edit(strong0, st => Force(st.Cards[targetS.Id], 3)); // Force 2 -> 5, above the Ranged 2 Attack
        Accept(strong, Offer(strong, 0, "training-recon-attack", archerS.Id, targetS.Id));
        Settle(strong);
        Eq(strong.State.Cards[targetS.Id].Zone, Zone.Battlefield, "a Force 5 target survives the Ranged 2 Attack that was legally aimed at it");
        Eq(Reductions(strong, targetS), 1, "and the Keep's reduction applies to it");
        Check(strong.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.RangedTargetId == targetS.Id), "and an additional Ranged Attack on the same card is granted");

        // The Keep's Reaction not used at the engagement: this battle's Ranged Attacks have no additional effects.
        var n = Engaged(false, true, out _, out var archerN, out var targetN, out _);
        Check(!n.State.Players[1].Provinces[0].Reconnaissance[0], "no Reconnaissance unless the Reaction was used");
        Accept(n, Offer(n, 0, "training-recon-attack", archerN.Id, targetN.Id));
        Settle(n);
        Eq(Reductions(n, targetN), 0, "an unused Keep reduces nothing");
        Check(!n.State.AdditionalActions.Any(), "and grants nothing");
    }

    // ---- Kyuden Otomo - exp -----------------------------------------------------------------------------------------------------------------------------
    private static void KyudenOtomoTokens()
    {
        // "After you take the Imperial Favor: You may discard it; if you did, add a Favor token": Lobby takes the Favor, then the Reaction discards it for a token.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var otomo = StrongholdOf(s, 0, KyudenOtomo);
        s.Players[0].Honor = s.Players[1].Honor + 5;
        var lobbyist = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var lobby = e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby").Command!;
        Accept(e, lobby with { TargetId = lobbyist.Id });
        Eq(e.State.FavorPlayerId, 0, "Lobby took the Imperial Favor");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window opens after taking the Favor");
        Accept(e, Enabled(e, 0, "otomo-favor-token", otomo.Id).Command!);
        Eq(e.State.FavorPlayerId, null, "the Favor was discarded");
        Eq(e.State.Cards[otomo.Id].Tokens.Count(t => t.Type == "Favor"), 1, "and a Favor token added to the Stronghold");
        while (e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));

        // A trait usable while bowed: the Stronghold bowed, the Reaction still applies.
        var sb = Table(Phase.Action, 0); sb.PriorityPlayerId = 0;
        var otomoBowed = StrongholdOf(sb, 0, KyudenOtomo); otomoBowed.Bowed = true;
        sb.Players[0].Honor = sb.Players[1].Honor + 5;
        var lobbyistB = Add(sb, "training-crane-samurai", 0, Zone.Home);
        var eb = MatchEngine.FromTestState(sb);
        Accept(eb, eb.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby").Command! with { TargetId = lobbyistB.Id });
        Check(CanUse(eb, 0, "otomo-favor-token", otomoBowed.Id), "the trait is usable while the Stronghold is bowed");
        // Taking the Favor otherwise (an action of another player) does not offer it to this player.
        var so = Table(Phase.Action, 1); so.PriorityPlayerId = 1;
        var otomoNo = StrongholdOf(so, 0, KyudenOtomo);
        StrongholdOf(so, 1, EternalVictory); // player abilities (Lobby) need a published Stronghold
        so.Players[1].Honor = so.Players[0].Honor + 5;
        var lobbyistNo = Add(so, "training-lion-samurai", 1, Zone.Home);
        var eo = MatchEngine.FromTestState(so);
        Accept(eo, eo.QueryActions(1).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby").Command! with { TargetId = lobbyistNo.Id });
        Check(!CanUse(eo, 0, "otomo-favor-token", otomoNo.Id), "another player taking the Favor is not \"you take the Imperial Favor\"");

        // "Political Open: Bow your performing honorable Courtier: Add a Favor token to this card."
        var sc = Table(Phase.Action, 0); sc.PriorityPlayerId = 0;
        var otomoC = StrongholdOf(sc, 0, KyudenOtomo);
        var courtier = Add(sc, "training-courtier-personality", 0, Zone.Home);
        var dishonorable = Add(sc, "training-courtier-personality", 0, Zone.Home); dishonorable.Dishonorable = true;
        var bowedCourtier = Add(sc, "training-courtier-personality", 0, Zone.Home, bowed: true);
        var notCourtier = Add(sc, "training-crane-samurai", 0, Zone.Home);
        var ec = MatchEngine.FromTestState(sc);
        foreach (var bad in new[] { dishonorable, bowedCourtier, notCourtier }) Check(!CanUse(ec, 0, "otomo-courtier-token", otomoC.Id, bad.Id), "only an unbowed honorable Courtier may perform it");
        Accept(ec, Enabled(ec, 0, "otomo-courtier-token", otomoC.Id, courtier.Id).Command!);
        Eq(ec.State.Cards[otomoC.Id].Tokens.Count(t => t.Type == "Favor"), 1, "a Favor token was added");
        Check(ec.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");

        // Substitution: a Favor token may be discarded in place of the Imperial Favor as a cost (Doji Shunya's Limited).
        MatchState WithToken(bool holdsFavor)
        {
            var st = Table(Phase.Action, 0); st.PriorityPlayerId = 0;
            var stronghold = StrongholdOf(st, 0, KyudenOtomo);
            stronghold.Tokens.Add(new CardToken("tok-1", "Favor", "test-fixture", null, 0));
            if (holdsFavor) st.FavorPlayerId = 0;
            Add(st, Shunya, 0, Zone.Home);
            return st;
        }
        foreach (var holdsFavor in new[] { true, false })
        {
            var st = WithToken(holdsFavor);
            var shunya = st.Cards.Values.Single(c => c.DefinitionId == Shunya);
            var es = MatchEngine.FromTestState(st);
            var shunyaOptions = Options(es, 0, "shunya-honor", shunya.Id);
            var tokenOption = shunyaOptions.Single(o => o.Command?.CostOption == "favor-token" || o.Label.Contains("Favor token"));
            Check(tokenOption.Enabled, "the token option is offered enabled");
            Eq(shunyaOptions.Any(o => o.Enabled && o.Command?.CostOption is null), holdsFavor, "the ordinary option needs the Imperial Favor");
            var honor = es.State.Players[0].Honor;
            Accept(es, tokenOption.Command!);
            Eq(es.State.Players[0].Honor, honor + 2, "the action resolved");
            Eq(es.State.Cards.Values.Single(c => c.Id == st.Players[0].StrongholdId).Tokens.Count(t => t.Type == "Favor"), 0, "the token was discarded");
            Eq(es.State.FavorPlayerId, holdsFavor ? 0 : (int?)null, "the Imperial Favor was not discarded");
        }
        // Without a token, and without the Favor, the action is unavailable.
        var sn = Table(Phase.Action, 0); sn.PriorityPlayerId = 0; StrongholdOf(sn, 0, KyudenOtomo); Add(sn, Shunya, 0, Zone.Home);
        var en = MatchEngine.FromTestState(sn);
        Check(Options(en, 0, "shunya-honor").All(o => !o.Enabled), "no Favor and no token: unavailable");
        // The token is the Stronghold's alone: another player's Otomo token does not pay for this player's action.
        var sx = Table(Phase.Action, 0); sx.PriorityPlayerId = 0; StrongholdOf(sx, 1, KyudenOtomo).Tokens.Add(new CardToken("tok-2", "Favor", "test-fixture", null, 0)); Add(sx, Shunya, 0, Zone.Home);
        var ex = MatchEngine.FromTestState(sx);
        Check(Options(ex, 0, "shunya-honor").All(o => !o.Enabled), "an opponent's Favor token is not yours to discard");
    }

    // Mendon (issue #1272): a player with Favor tokens on Kyuden Otomo may Lobby again; the only bar is holding the Imperial Favor itself (CR: Lobby takes the Favor "whether it is controlled by
    // another player or by no player"). Tokens are not the Favor.
    private static void KyudenOtomoLobby()
    {
        bool LobbyOffered(int? favor, int tokens)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var otomo = StrongholdOf(s, 0, KyudenOtomo);
            for (var i = 0; i < tokens; i++) otomo.Tokens.Add(new CardToken("tok-" + i, "Favor", "test-fixture", null, 0));
            s.FavorPlayerId = favor; s.Players[0].Honor = s.Players[1].Honor + 5;
            Add(s, "training-crane-samurai", 0, Zone.Home);
            return MatchEngine.FromTestState(s).QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby");
        }
        Check(LobbyOffered(null, 2), "tokens on Otomo, nobody holds the Favor: Lobby is offered");
        Check(LobbyOffered(1, 2), "tokens on Otomo, the opponent holds the Favor: Lobby is offered");

        // The whole loop: Lobby, trade the Favor for a token, and on the next turn Lobby again.
        var sl = Table(Phase.Action, 0); sl.PriorityPlayerId = 0;
        var keep = StrongholdOf(sl, 0, KyudenOtomo); StrongholdOf(sl, 1, EternalVictory);
        sl.Players[0].Honor = sl.Players[1].Honor + 5;
        var first = Add(sl, "training-crane-samurai", 0, Zone.Home); var second = Add(sl, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(sl);
        Accept(e, e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby").Command! with { TargetId = first.Id });
        Accept(e, Enabled(e, 0, "otomo-favor-token", keep.Id).Command!);
        while (e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[keep.Id].Tokens.Count(t => t.Type == "Favor"), 1, "the Favor became a token");
        var advanced = 0;
        while (!(e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action && e.State.TurnNumber > sl.TurnNumber && e.State.PriorityPlayerId == 0) && advanced++ < 200)
        {
            var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(o => o.Enabled && o.Command?.Type is "pass" or "endPhase" or "declineReaction" or "passPriority")?.Command
                ?? e.QueryActions(e.State.PriorityPlayerId).First(o => o.Enabled && o.Command is not null).Command!;
            Accept(e, pass);
        }
        Check(e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action, "reached the Otomo player's next Action Phase");
        Check(e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby" ), "next turn, with a Favor token on Otomo, Lobby is offered again");
        Check(!LobbyOffered(0, 0), "holding the Imperial Favor: no Lobby");
        Check(!LobbyOffered(0, 2), "holding the Imperial Favor and tokens: no Lobby");
    }

    // ---- Library of Rebirth with the real Swell of Strength (playtest report, issue #794) --------------------------------------------------------------------
    // A Shugenja may equip a Spell that does not share an element keyword with it, and the Library's Reaction is not element-bound. The Swell's "Water" belongs to its Focus Effect only.
    private static void SwellEquipByNonWaterShugenja()
    {
        const string Swell = "oracle-l5r-8366", FireShugenja = "oracle-l5r-592", WaterShugenja = "oracle-l5r-3850", SmallFarm = "oracle-l5r-7303";
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var library = StrongholdOf(s, 0, LibraryOfRebirth); library.Bowed = true;
        var libraryId = library.Id;
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != libraryId && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        Add(s, SmallFarm, 0, Zone.Home);
        var fire = Add(s, FireShugenja, 0, Zone.Battlefield, province: 0);
        var water = Add(s, WaterShugenja, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var swell = Add(s, Swell, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        foreach (var shugenja in new[] { fire, water })
            Check(e.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == shugenja.Id),
                "Equip (Battle) offers The Swell of Strength to " + CardCatalog.Get(shugenja.DefinitionId).Name);
        Accept(e, Enabled(e, 0, "equip-battle", performerId: fire.Id).Command! with { TargetId = swell.Id });
        Eq(e.State.Cards[swell.Id].AttachedTo, fire.Id, "a Fire Shugenja equipped the Water Spell, paid with the Library's Reaction and the Small Farm");
        Check(e.State.Cards[library.Id].Bowed, "the Library stays bowed");
        // Only the Focus Effect is limited to "one of your Water Shugenja".
        var focusCandidates = (List<string>)typeof(MatchEngine).GetMethod("AttachWielderCandidates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(e, [0, swell.Id])!;
        Check(focusCandidates.Contains(water.Id) && !focusCandidates.Contains(fire.Id), "the Swell's Focus Effect may attach only to a Water Shugenja: " + string.Join(",", focusCandidates));
    }

    private static void LibrarySwellOfStrength()
    {
        const string Swell = "oracle-l5r-8366", Kojiro = "oracle-l5r-3850", SmallFarm = "oracle-l5r-7303";
        foreach (var libraryBowed in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var library = StrongholdOf(s, 0, LibraryOfRebirth); library.Bowed = libraryBowed;
            var libraryId = library.Id;
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != libraryId && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            Add(s, SmallFarm, 0, Zone.Home);
            var kojiro = Add(s, Kojiro, 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var swell = Add(s, Swell, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var offered = e.QueryActions(0).Where(a => a.Command?.AbilityId == "equip-battle" && a.CardId != null).ToArray();
            var report = string.Join(" || ", e.QueryActions(0).Where(a => a.Command?.AbilityId == "equip-battle" || a.Type == "equip").Select(a => $"{a.Type}/{a.Command?.AbilityId} card={a.CardId} enabled={a.Enabled} reason={a.Reason} perf={a.Command?.PerformerId}"));
            Check(e.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == kojiro.Id),
                $"Equip (Battle) is offered for a Water Shugenja with Library bowed={libraryBowed}: {report}");
            Check(e.PaymentOptions(0, CardCatalog.Get(Swell).GoldCost, new PaymentSubject(swell, PaymentKind.Equip)).Length > 0, $"The Swell of Strength is payable with Library bowed={libraryBowed}");
        }
    }

    // ---- Library of Rebirth -----------------------------------------------------------------------------------------------------------------------------
    private static void LibraryGold()
    {
        // A Spell equipped in a Combat Segment, every other Gold source bowed and the Library itself bowed: its Reaction pays, without unbowing it.
        MatchState Setup(bool libraryBowed, out CardInstance library, out CardInstance spell, out CardInstance shugenja)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            library = StrongholdOf(s, 0, LibraryOfRebirth); library.Bowed = libraryBowed;
            var libraryId = library.Id;
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != libraryId && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            spell = Add(s, "training-spell", 0, Zone.Hand);
            return s;
        }
        var s1 = Setup(true, out var library1, out var spell1, out var shugenja1);
        var e1 = MatchEngine.FromTestState(s1);
        // Issue #501: the Reaction's Gold is its own payment token ("<card>#reaction"); the Stronghold's own id is its bow.
        Check(e1.PaymentOptions(0, 1, new PaymentSubject(spell1, PaymentKind.Equip)).Any(p => p.SourceIds.SequenceEqual([library1.Id + "#reaction"])), "in a Combat Segment the bowed Library pays for a Spell");
        Check(e1.PaymentOptions(0, 1, new PaymentSubject(Add(s1, "training-follower", 0, Zone.Hand), PaymentKind.Equip)).Length == 0, "but not for a Follower");
        var equip = Enabled(e1, 0, "equip-battle", performerId: shugenja1.Id);
        Accept(e1, equip.Command! with { TargetId = spell1.Id });
        Eq(e1.State.Cards[spell1.Id].AttachedTo, shugenja1.Id, "the Spell was attached, paid with the Library's Reaction");
        Check(e1.State.Cards[library1.Id].Bowed, "the Library stays bowed: it produced by its Reaction, not by bowing");
        Check(e1.State.Events.Any(ev => ev.Type == "goldSourceUsed" && ev.CardId == library1.Id), "the use is logged");
        // Once per turn: it cannot pay a second Spell this turn.
        var e1b = Edit(e1, st => Add(st, "training-spell", 0, Zone.Hand));
        Check(e1b.PaymentOptions(0, 1, new PaymentSubject(e1b.State.Cards.Values.First(cc => cc.Zone == Zone.Hand && cc.OwnerId == 0 && cc.DefinitionId == "training-spell"), PaymentKind.Equip)).Length == 0, "the Reaction is spent for the turn");

        // Unbowed: the Reaction pays first and the Stronghold stays unbowed (its own production remains available).
        var s2 = Setup(false, out var library2, out var spell2, out var shugenja2);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "equip-battle", performerId: shugenja2.Id).Command! with { TargetId = spell2.Id });
        Check(!e2.State.Cards[library2.Id].Bowed, "an unbowed Library that pays by its Reaction stays unbowed");

        // Outside a Combat Segment (the Action Phase Equip of a Spell at home), a bowed Library cannot pay.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var library3 = StrongholdOf(s3, 0, LibraryOfRebirth); library3.Bowed = true;
        foreach (var card in s3.Cards.Values.Where(x => x.OwnerId == 0 && x.Id != library3.Id && CardCatalog.Get(x.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var spell3 = Add(s3, "training-spell", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.PaymentOptions(0, 1, new PaymentSubject(spell3, PaymentKind.Equip)).Length == 0, "outside the Combat Segment the bowed Library produces nothing");

        // Its Battle action: straighten the performing Shugenja and take an additional Battle action.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        var library4 = StrongholdOf(s4, 0, LibraryOfRebirth);
        var bowedShugenja = Add(s4, "training-shugenja", 0, Zone.Battlefield, bowed: true, province: 0);
        var samurai = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var away = Add(s4, "training-shugenja", 0, Zone.Home, bowed: true);
        Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "library-rebirth-straighten", library4.Id, samurai.Id) && !CanUse(e4, 0, "library-rebirth-straighten", library4.Id, away.Id), "a non-Shugenja, or one away from the battlefield, may not perform it");
        Accept(e4, Enabled(e4, 0, "library-rebirth-straighten", library4.Id, bowedShugenja.Id).Command!);
        Check(!e4.State.Cards[bowedShugenja.Id].Bowed, "the performing Shugenja straightened");
        Check(e4.State.AdditionalActions.Any(g => g.PlayerId == 0), "and an additional Battle action was granted");
        Eq(e4.State.PriorityPlayerId, 0, "its controller keeps the action");
    }

    // ---- Midday Shadow Court ---------------------------------------------------------------------------------------------------------------------------
    private static void MiddayWatch()
    {
        // Lobby: "your Honor is treated as 20 points higher". Player 0 has 10 less Family Honor than player 1.
        foreach (var midday in new[] { true, false })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            if (midday) StrongholdOf(s, 0, MiddayShadowCourt);
            s.Players[0].Honor = 10; s.Players[1].Honor = 20;
            var lobbyist = Add(s, "training-crane-samurai", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var offer = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId == "lobby").ToArray();
            Eq(offer.Any(o => o.Enabled), midday, midday ? "Lobby is legal with the Court despite lower Family Honor" : "without the Court a lower Family Honor may not Lobby");
            if (midday) { Accept(e, offer.First(o => o.Enabled).Command! with { TargetId = lobbyist.Id }); Eq(e.State.FavorPlayerId, 0); Eq(e.State.Players[0].Honor, 10, "only the comparison is changed, not the Honor itself"); }
        }

        // The watch, set even though the Stronghold is bowed; fired by the target's assignment (as a Defender here) ...
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var court = StrongholdOf(s, 0, MiddayShadowCourt); court.Bowed = true;
            var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
            var target = Add(s, "training-lion-samurai", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Check(!Options(e, 0, "midday-shadow-watch", court.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(mine.Id) ?? false)), "it targets another player's Personality, not your own");
            var honor = e.State.Players[1].Honor;
            Accept(e, Offer(e, 0, "midday-shadow-watch", court.Id, target.Id));
            Check(!e.State.Cards[target.Id].Dishonorable, "nothing happens yet");
            Pass(e); Pass(e);
            Eq(e.State.Phase, Phase.AttackDeclaration);
            Accept(e, new GameCommand("declareAttack", 0, e.Version));
            Pass(e); // the Attacker assigns no Infantry
            Eq(e.State.Phase, Phase.DefenseAssignment);
            Check(!e.State.Cards[target.Id].Dishonorable, "still nothing: he has not been assigned");
            Accept(e, e.QueryActions(1).First(o => o.Type == "assignDefense" && o.Enabled).Command!);
            Check(e.State.Cards[target.Id].Dishonorable, "assigned: he is dishonored");
            Eq(e.State.Players[1].Honor, honor - 1, "and his controller lost 1 Honor");
            Check(e.State.AssignmentWatches.All(w => w.Kind != MatchEngine.WatchMiddayShadow), "the watch is spent");
        }
        // ... or by the end of an action he performed (an Open action of his, in the same Action Phase).
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var court = StrongholdOf(s, 0, MiddayShadowCourt);
            var target = Add(s, "training-recon-actor", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var honor = e.State.Players[1].Honor;
            Accept(e, Offer(e, 0, "midday-shadow-watch", court.Id, target.Id));
            Eq(e.State.PriorityPlayerId, 1);
            Accept(e, Enabled(e, 1, "training-plain-open", target.Id).Command!);
            Check(e.State.Cards[target.Id].Dishonorable, "the action he performed ended: he is dishonored");
            Eq(e.State.Players[1].Honor, honor - 1, "and his controller lost 1 Honor");
            // "The next time": a second action of his does nothing more.
            var e2 = Edit(e, st => { st.PriorityPlayerId = 1; st.Cards[target.Id].AbilityUses.Clear(); });
            var again = e2.State.Players[1].Honor;
            Accept(e2, Enabled(e2, 1, "training-plain-open", target.Id).Command!);
            Eq(e2.State.Players[1].Honor, again, "the watch fires once");
        }
        // Another Personality's action, or one of his own before the watch, does not fire it.
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var court = StrongholdOf(s, 0, MiddayShadowCourt);
            var target = Add(s, "training-lion-samurai", 1, Zone.Home);
            var actor = Add(s, "training-recon-actor", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "midday-shadow-watch", court.Id, target.Id));
            Accept(e, Enabled(e, 1, "training-plain-open", actor.Id).Command!);
            Check(!e.State.Cards[target.Id].Dishonorable, "an action another Personality performed does not fire the watch");
        }
    }

    // ---- Hidden Falls Dojo ----------------------------------------------------------------------------------------------------------------------------------
    private static void HiddenFallsSegment()
    {
        // Player 0 (Attacker) targets provinces 1 and 2 of player 1's; afterward, Scouts only, to those battlefields only.
        MatchState Setup(int holder, out CardInstance dojo, out CardInstance scout, out CardInstance plain)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            dojo = StrongholdOf(s, holder, HiddenFalls);
            scout = Add(s, "training-recon-actor", holder, Zone.Home); // Samurai, Scout
            plain = Add(s, "training-crane-samurai", holder, Zone.Home);
            return s;
        }
        var s1 = Setup(0, out var dojo1, out var scout1, out var plain1);
        // An idle Cavalry Maneuvers segment is skipped (issue #627), so each player holds a Cavalry unit to keep the segment this test is about.
        Add(s1, "training-cavalry", 0, Zone.Home); Add(s1, "training-cavalry", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s1);
        var options = Options(e, 0, "hidden-falls-recon", dojo1.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.Command!.Provinces is [1, 2]) && options.Any(o => o.Command!.Provinces is [3]) && options.All(o => o.Command!.Provinces is { Length: 1 or 2 }), "one option per set of one or two provinces");
        Accept(e, options.First(o => o.Command!.Provinces is [1, 2]).Command!);
        Check(e.State.Players[1].Provinces[1].Reconnaissance[0] && e.State.Players[1].Provinces[2].Reconnaissance[0], "Reconnaissance at the battlefields of the targeted provinces");
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[0] && !e.State.Players[1].Provinces[3].Reconnaissance[0], "and only there");
        Pass(e); Pass(e);
        Accept(e, new GameCommand("declareAttack", 0, e.Version));
        Pass(e); Pass(e);   // Infantry Maneuvers: neither player assigns
        Eq(e.State.Phase, Phase.CavalryDeclaration);
        Pass(e); Pass(e);   // Cavalry Maneuvers: neither player assigns
        Eq(e.State.Phase, Phase.InfantryDeclaration, "an additional Infantry Maneuvers Segment follows the Cavalry Maneuvers Segment");
        Check(e.State.ExtraInfantrySegment, "and it is the additional one");
        var attackOptions = e.QueryActions(0).Where(o => o.Type == "attack").ToArray();
        Check(attackOptions.All(o => o.Command!.Assignments!.Keys.All(k => k == scout1.Id)) && attackOptions.Select(o => o.Command!.Assignments!.Values.Single()).Order().SequenceEqual([1, 2]),
            "only the Scout, only to the targeted provinces");
        Check(Rejection(e, new GameCommand("attack", 0, e.Version, Assignments: new() { [plain1.Id] = 1 })).Contains("Scout"), "a non-Scout may not be assigned");
        Check(Rejection(e, new GameCommand("attack", 0, e.Version, Assignments: new() { [scout1.Id] = 0 })).Contains("targeted"), "nor a Scout to a province that was not targeted");
        Accept(e, new GameCommand("attack", 0, e.Version, Assignments: new() { [scout1.Id] = 1 }));
        Eq(e.State.Cards[scout1.Id].ProvinceIndex, 1, "the Scout was assigned to the targeted battlefield");
        Eq(e.State.Phase, Phase.BattleSelection, "the Defender has nobody to assign, so the maneuvers end");
        Check(!e.State.ExtraInfantrySegment, "the additional segment is over");

        // Without the Dojo's action there is no additional segment.
        var s2 = Table(Phase.AttackDeclaration, 0); s2.PriorityPlayerId = 0; StrongholdOf(s2, 0, HiddenFalls);
        Add(s2, "training-cavalry", 0, Zone.Home); Add(s2, "training-cavalry", 1, Zone.Home); // each holds a Cavalry unit so the Cavalry segment still exists (#627)
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("declareAttack", 0, e2.Version));
        Pass(e2); Pass(e2); Pass(e2); Pass(e2);
        Eq(e2.State.Phase, Phase.BattleSelection, "no additional segment unless the Recon Open was used");

        // A holder with no Scout at home has nobody to assign: no additional segment is prompted.
        var s5 = Table(Phase.Action, 0); s5.PriorityPlayerId = 0;
        var dojo5 = StrongholdOf(s5, 0, HiddenFalls); Add(s5, "training-crane-samurai", 0, Zone.Home);
        Add(s5, "training-cavalry", 0, Zone.Home); Add(s5, "training-cavalry", 1, Zone.Home); // each holds a Cavalry unit so the Cavalry segment still exists (#627)
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Enabled(e5, 0, "hidden-falls-recon", dojo5.Id).Command!);
        Pass(e5); Pass(e5);
        Accept(e5, new GameCommand("declareAttack", 0, e5.Version));
        Pass(e5); Pass(e5); Pass(e5); Pass(e5);
        Eq(e5.State.Phase, Phase.BattleSelection, "no Scout to assign: the additional segment is skipped");
        Check(e5.State.Players[1].Provinces.Any(p => p.Reconnaissance[0]), "though Reconnaissance was still gained");

        // The Defender's Dojo: player 1 (the Defender) targets his own provinces; after the Cavalry segment only his Scouts, to his provinces 0 and 2, in the additional segment.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 1;
        var dojo3 = StrongholdOf(s3, 1, HiddenFalls);
        var scout3 = Add(s3, "training-recon-actor", 1, Zone.Home);
        Add(s3, "training-lion-samurai", 1, Zone.Home);
        Add(s3, "training-cavalry", 0, Zone.Home); Add(s3, "training-cavalry", 1, Zone.Home); // each holds a Cavalry unit so the Cavalry segment still exists (#627)
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 1, "hidden-falls-recon", dojo3.Id).Command! with { Provinces = [0, 2], Province = 0 });
        Check(e3.State.Players[1].Provinces[0].Reconnaissance[1], "the Defender has Reconnaissance at his own province's battlefield");
        Pass(e3); Pass(e3);
        Accept(e3, new GameCommand("declareAttack", 0, e3.Version));
        Pass(e3); Pass(e3); Pass(e3); Pass(e3);
        Eq(e3.State.Phase, Phase.DefenseAssignment, "the Attacker has no Dojo: the additional segment is the Defender's alone");
        Check(e3.State.ExtraInfantrySegment);
        var defend = e3.QueryActions(1).Where(o => o.Type == "assignDefense").ToArray();
        Check(defend.Select(o => o.Command!.Assignments!.Values.Single()).Order().SequenceEqual([0, 2]) && defend.All(o => o.Command!.Assignments!.Keys.SequenceEqual([scout3.Id])), "his Scout, to the targeted battlefields");
        Accept(e3, defend.First().Command!);
        Eq(e3.State.Phase, Phase.BattleSelection);

        // Illegal targets: more than two provinces, none, a duplicate.
        var e4 = MatchEngine.FromTestState(Setup(0, out var dojo4, out _, out _));
        foreach (var bad in new int[][] { [0, 1, 2], [], [1, 1] })
            Check(!e4.Execute(new GameCommand("useAbility", 0, e4.Version, CardId: dojo4.Id, AbilityId: "hidden-falls-recon", Provinces: bad, Province: bad.Length > 0 ? bad[0] : null)).Accepted, $"[{string.Join(",", bad)}] is not one or two provinces");
    }
}
