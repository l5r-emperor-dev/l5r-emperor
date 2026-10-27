using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Personality pass (issue #477), batch 4: personality-wave-71 -- fifteen Crane, Scorpion and Ninja Personalities, driven through QueryActions and the commands it offers.
// Shosuro Rokuta (oracle-l5r-7156) is not wired (its created Personality's Proxy scan is not available); tools/content/ee_batch4_blockers.json records why.
internal static class EmperorPersonalityBatch4Tests
{
    private const string Keigo = "oracle-l5r-497", Kitiaru = "oracle-l5r-501", Michiru = "oracle-l5r-503", Genshi = "oracle-l5r-4166", Seishi = "oracle-l5r-4220", Tadanobu = "oracle-l5r-4224",
        Makoto = "oracle-l5r-2118", Hurunayi = "oracle-l5r-790", Mitsuo = "oracle-l5r-840", Nitoshi = "oracle-l5r-850", Rentatsu = "oracle-l5r-866", Waru = "oracle-l5r-923",
        Kameyoi = "oracle-l5r-7117", Tanzaki = "oracle-l5r-7169", Yorimi = "oracle-l5r-7371",
        WindOfTheMoon = "oracle-l5r-9339", IncriminatingTestimony = "oracle-l5r-10153", TrustingInstinct = "oracle-l5r-10402", TempleOfHotei = "oracle-l5r-7875";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static GameCommand Pass(MatchEngine e, int player) => new("pass", player, e.Version);

    public static void Run(TestRunner runner) => runner.Run("emperor-personality-batch4", [
        ("Published shape: the fifteen Personalities print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in personality-wave-71's manifest", PublishedShape),
        ("Asahina Keigo: bowed and discarding a hand card, he produces Gold equal to its Focus Value -- never suggested, only when the payer names the card, once a turn", KeigoGold),
        ("Asahina Kitiaru: copies one of the target's Political abilities that does not itself copy abilities, for the turn -- not a non-Political one", KitiaruCopy),
        ("Asahina Michiru: the barrier negates the next destruction of the Personality by another player's action this turn and moves him home; then it is spent", MichiruBarrier),
        ("Kakita Genshi: the target's controller may dishonor him; otherwise Genshi's controller gains 1 Honor and locks one of his abilities until his next turn begins", GenshiShame),
        ("Kakita Seishi: bows to rehonor a dishonorable Personality -- not an honorable one, not while bowed", SeishiRehonor),
        ("Kakita Tadanobu: after a duel he won, discards the Imperial Favor to draw two cards -- not without the Favor, not after a duel he lost", TadanobuDraw),
        ("Doji Makoto: Political Open takes the Imperial Favor; his Iaijutsu Battle duel destroys the loser", MakotoActions),
        ("Bayushi Hurunayi: Ninja is removed from him until the turn ends", HurunayiNinja),
        ("Bayushi Mitsuo: after a Battle action that moved him resolves, an additional Battle action -- not for an action that moved someone else", MitsuoAdditionalAction),
        ("Bayushi Nitoshi: the Limited action destroys a different Personality of yours to dishonor the target and cost his controller 3 Honor; the Battle action destroys a dishonorable Personality or a card without attachments", NitoshiActions),
        ("Bayushi Rentatsu: while he stays bowed his controller may keep him from straightening and each of that player's turns begins with a chosen player losing 2 Honor", RentatsuHold),
        ("Bayushi Waru: bows a Personality in a Combat Segment, or as a Limited action while his controller has a Courtier; one use for both", WaruBow),
        ("Shosuro Kameyoi: a dishonorable target's controller loses 1 Honor, or 2 with a base Personal Honor of 3 or more; then he is dishonored", KameyoiShame),
        ("Shosuro Tanzaki: gives another Personality +4F, never himself", TanzakiBoost),
        ("Soshi Yorimi: twice a turn the cost of bowing him is ignored for a Political action or an action on a Spell, and only for those", YorimiWaiver),
        ("Soshi Yorimi's waiver reads declared costs: every Political action and every Spell's action whose printed cost bows a card declares that cost as data", YorimiCostAudit),
    ]);

    // Audit: Yorimi's waiver is applied where an action's declared costs are planned and paid (AbilityCosts.cs). A Political action or an action on a Spell whose printed constraints block bows a card
    // ("Bow this Shugenja", "Bow Rin") but pays it inside its own execution case would never be waived, so each such ability must declare it (Costs or CostOptions).
    private static void YorimiCostAudit()
    {
        var offenders = new List<string>();
        foreach (var card in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && !c.CreatedOnly))
            foreach (var id in card.Abilities ?? [])
            {
                var ability = AbilityCatalog.All[id];
                var political = ability.ActionKeywords?.Contains("Political") == true || (card.Keywords ?? []).Contains("Political");
                if (!political && card.Type != CardType.Spell) continue;
                if (ability.Costs is not null || ability.CostOptions is not null || ability.Timing == "Reaction" && ability.Trigger is "paying-gold-cost") continue;
                var constraints = ability.Label.Contains(':') ? ability.Label[..ability.Label.LastIndexOf(':')] : "";
                if (System.Text.RegularExpressions.Regex.IsMatch(constraints, @"bow", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) offenders.Add($"{id} ({card.Name})");
            }
        Check(offenders.Count == 0, "Political or Spell actions that bow a card as a cost without declaring it: " + string.Join("; ", offenders.Distinct()));
    }

    private static void Manifest(string root, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", "personality-wave-71.txt"));
        Eq(listed.Length, ids.Length, "the manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the personality-wave-71 manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Stats(string id, string name, int force, int chi, int cost, int ph, string[] abilities, string effect = "none")
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph && c.Effect == effect,
                $"{c.Name} ({id}): force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            foreach (var ability in abilities) Check(AbilityCatalog.All.ContainsKey(ability), ability);
        }
        Stats(Keigo, "Asahina Keigo", 1, 3, 8, 2, ["keigo-produce-gold"]);
        Stats(Kitiaru, "Asahina Kitiaru", 0, 4, 6, 4, ["kitiaru-copy"]);
        Stats(Michiru, "Asahina Michiru", 2, 4, 7, 3, ["michiru-barrier"]);
        Stats(Genshi, "Kakita Genshi", 3, 3, 7, 3, ["genshi-shame"]);
        Stats(Seishi, "Kakita Seishi", 0, 2, 2, 2, ["seishi-rehonor"]);
        Stats(Tadanobu, "Kakita Tadanobu", 2, 4, 8, 3, ["tadanobu-draw"]);
        Stats(Makoto, "Doji Makoto", 5, 6, 10, 5, ["makoto-favor", "makoto-duel"]);
        Stats(Hurunayi, "Bayushi Hurunayi", 2, 2, 4, 1, ["hurunayi-remove-ninja"]);
        Stats(Mitsuo, "Bayushi Mitsuo", 3, 4, 7, 1, ["mitsuo-additional-action"]);
        Stats(Nitoshi, "Bayushi Nitoshi", 8, 5, 13, 1, ["nitoshi-frame", "nitoshi-destroy"]);
        Stats(Rentatsu, "Bayushi Rentatsu", 0, 3, 7, 1, ["rentatsu-hold"]);
        Stats(Waru, "Bayushi Waru", 4, 3, 8, 2, ["waru-bow-battle", "waru-bow-limited"]);
        Stats(Kameyoi, "Shosuro Kameyoi", 2, 4, 7, 1, ["kameyoi-shame"]);
        Stats(Tanzaki, "Shosuro Tanzaki", 2, 4, 7, 2, ["tanzaki-boost"]);
        Stats(Yorimi, "Soshi Yorimi", 2, 4, 7, 1, [], "yorimi-bow-waiver");
        Check(CardCatalog.Get(Keigo).Keywords!.Contains("Shugenja") && CardCatalog.Get(Keigo).Keywords!.Contains("Air"), "Keigo is an Air Shugenja");
        Check(CardCatalog.Get(Rentatsu).Keywords!.Contains("Unique") && CardCatalog.Get(Nitoshi).Keywords!.Contains("Ninja"), "Rentatsu is Unique, Nitoshi a Ninja");
        Eq(CardCatalog.Get(Kitiaru).HonorRequirement, 10, "Kitiaru's Honor Requirement");
        Eq(CardCatalog.Get(Rentatsu).HonorRequirement, null, "Rentatsu prints no Honor Requirement");
        foreach (var political in new[] { "kitiaru-copy", "makoto-favor", "nitoshi-frame", "nitoshi-destroy", "kameyoi-shame", "tanzaki-boost" })
            Check(AbilityCatalog.All[political].ActionKeywords!.Contains("Political"), $"{political} is a Political action");
        Check(AbilityCatalog.All["genshi-shame"].ActionKeywords!.Contains("Iaijutsu") && AbilityCatalog.All["makoto-duel"].ActionKeywords!.Contains("Iaijutsu"), "Genshi's and Makoto's are Iaijutsu actions");
        Check(AbilityCatalog.All["tadanobu-draw"].ActionKeywords!.SequenceEqual(["Favor", "Political"]), "Tadanobu's is a Favor Political Reaction");
        Check(PublishedContent.PersonalityWave71.Cards.All(c => (c.Abilities ?? []).Length > 0 || c.Effect != "none"), "no card of the wave is blank");
        Manifest(root, Keigo, Kitiaru, Michiru, Genshi, Seishi, Tadanobu, Makoto, Hurunayi, Mitsuo, Nitoshi, Rentatsu, Waru, Kameyoi, Tanzaki, Yorimi);
        // Shosuro Rokuta (oracle-l5r-7156) is wired in personality-wave-78 by proxy group Z (issue #492) once his Apprentice scan arrived: EmperorProxyZTests covers him.
    }

    // ---- Keigo ------------------------------------------------------------------------------------------------------------------------------------

    private static void KeigoGold()
    {
        var s = Table(Phase.Action, 0);
        var keigo = Add(s, Keigo, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-invest-follower", 0, Zone.Hand); // Gold Cost 2
        var focus = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus Value 3
        var blank = Add(s, "training-crane-samurai", 0, Zone.Hand); // Focus Value 0
        var e = MatchEngine.FromTestState(s);
        Check(!e.PaymentOptions(0, 2).Any(p => p.SourceIds.Contains(keigo.Id)), "the engine never suggests Keigo as a source: what he produces is the payer's choice");
        var equip = new GameCommand("equip", 0, e.Version, CardId: follower.Id, SourceIds: [keigo.Id], TargetId: host.Id);
        Check(Rejection(e, equip).Contains("Name a card"), "naming no card to discard is refused");
        Check(Rejection(e, equip with { PaymentDiscards = new() { [keigo.Id] = blank.Id } }).Contains("produce 0"), "a card with no Focus Value produces nothing");
        Check(Rejection(e, equip with { PaymentDiscards = new() { [keigo.Id] = follower.Id } }).Contains("cannot also be discarded"), "the card being paid for cannot be the one discarded");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, equip with { PaymentDiscards = new() { [keigo.Id] = focus.Id } });
        Check(e.State.Cards[keigo.Id].Bowed, "Keigo bowed");
        Eq(e.State.Cards[focus.Id].Zone, Zone.FateDiscard, "the named card was discarded");
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower was equipped, paid for by Keigo's 3 Gold");
        Eq(e.State.Players[0].Hand.Count, handBefore - 2, "the equipped card and the discarded one left the hand");
        Check(!e.PaymentOptions(0, 1).Any(p => p.SourceIds.Contains(keigo.Id)) && e.GoldProduced(keigo.Id, 0) == 0, "a bowed Keigo produces nothing");

        // The same through an ability that costs Gold (Urgency's shape): the payment names the discard on the ability command.
        var s2 = Table(Phase.Action, 0);
        var keigo2 = Add(s2, Keigo, 0, Zone.Home);
        var fixture = Add(s2, "training-cost-fixture", 0, Zone.Home);
        var focus2 = Add(s2, "training-focus-strategy", 0, Zone.Hand);
        Add(s2, "training-crane-samurai", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var draw = new GameCommand("useAbility", 0, e2.Version, fixture.Id, AbilityId: "training-urgency-draw", SourceIds: [keigo2.Id]);
        Check(Rejection(e2, draw).Length > 0, "no discard named: refused");
        var deckBefore = e2.State.Players[0].FateDeck.Count;
        Accept(e2, draw with { PaymentDiscards = new() { [keigo2.Id] = focus2.Id } });
        Eq(e2.State.Players[0].FateDeck.Count, deckBefore - 1, "the 2-Gold ability drew its card");
        Check(e2.State.Cards[keigo2.Id].Bowed && e2.State.Cards[focus2.Id].Zone == Zone.FateDiscard, "Keigo bowed and the card was discarded");

        // The same for recruiting a Personality (a purchase from a province in the Dynasty Phase).
        var s4 = MatchEngine.CreateEconomyScenario().State;
        var keigo4 = Add(s4, Keigo, 0, Zone.Home);
        var focus4 = Add(s4, "training-focus-strategy", 0, Zone.Hand);
        var provinceCard = s4.Players[0].Provinces[0].CardId!; s4.Cards[provinceCard].DefinitionId = "training-honor-personality"; s4.Cards[provinceCard].FaceUp = true;
        s4.Players[0].Honor = 20;
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, new GameCommand("purchase", 0, e4.Version, CardId: provinceCard, SourceIds: [keigo4.Id], PaymentDiscards: new() { [keigo4.Id] = focus4.Id }));
        Check(e4.State.Cards[provinceCard].Zone == Zone.Home && e4.State.Cards[keigo4.Id].Bowed && e4.State.Cards[focus4.Id].Zone == Zone.FateDiscard, "a 3-Gold Personality was recruited with Keigo's 3 Gold from a Focus Value 3 card");

        // Keigo is not a Gold source at all once his use is spent, and he is not one from a hand.
        var s3 = Table(Phase.Action, 0);
        var keigo3 = Add(s3, Keigo, 0, Zone.Home); keigo3.AbilityUses["keigo-produce-gold"] = s3.TurnNumber;
        Check(!MatchEngine.FromTestState(s3).PaymentOptions(0, 1).Any(p => p.SourceIds.Contains(keigo3.Id)), "spent for the turn");
    }

    // ---- Kitiaru ----------------------------------------------------------------------------------------------------------------------------------

    private static void KitiaruCopy()
    {
        var s = Table(Phase.Action, 0);
        var kitiaru = Add(s, Kitiaru, 0, Zone.Home);
        var tanzaki = Add(s, Tanzaki, 1, Zone.Home);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "kitiaru-copy", kitiaru.Id, tanzaki.Id));
        Check(e.State.Cards[kitiaru.Id].GrantedAbilities.Any(g => g.AbilityId == "tanzaki-boost" && g.Duration == GrantDuration.Turn), "Kitiaru copied Tanzaki's Political ability for the turn");
        Accept(e, Pass(e, 1));
        Accept(e, Offer(e, 0, "tanzaki-boost", kitiaru.Id, ally.Id));
        Eq(e.CardForceForTest(ally.Id), CardCatalog.Get("training-crane-samurai").Force + 4, "the copied ability is Kitiaru's own: +4F");

        // Makoto has a Political ability (the Favor) and a non-Political one (the duel): only the Political one is copied, with no choice to make.
        var s2 = Table(Phase.Action, 0);
        var kitiaru2 = Add(s2, Kitiaru, 0, Zone.Home);
        var makoto = Add(s2, Makoto, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "kitiaru-copy", kitiaru2.Id, makoto.Id));
        Check(e2.State.PendingChoice is null, "a single Political ability is taken without asking");
        Check(e2.State.Cards[kitiaru2.Id].GrantedAbilities.Select(g => g.AbilityId).SequenceEqual(["makoto-favor"]), "only Makoto's Political ability, not his Iaijutsu duel");

        // Two Political abilities: the player chooses.
        var s3 = Table(Phase.Action, 0);
        var kitiaru3 = Add(s3, Kitiaru, 0, Zone.Home);
        var nitoshi = Add(s3, Nitoshi, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "kitiaru-copy", kitiaru3.Id, nitoshi.Id));
        Eq(e3.State.PendingChoice?.Type, "copyAbility", "two Political abilities to choose from");
        var offered = e3.QueryActions(0).Where(a => a.Type == "chooseCopyAbility").Select(a => a.Command!.Choice).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Check(offered.SequenceEqual(["nitoshi-destroy", "nitoshi-frame"]), "both of Nitoshi's Political abilities are offered: " + string.Join(",", offered));

        // A target with no Political ability: the action is still taken, and copies nothing.
        var s4 = Table(Phase.Action, 0);
        var kitiaru4 = Add(s4, Kitiaru, 0, Zone.Home);
        var plain = Add(s4, "training-lion-samurai", 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "kitiaru-copy", kitiaru4.Id, plain.Id));
        Check(e4.State.Cards[kitiaru4.Id].GrantedAbilities.Count == 0, "nothing to copy");
        // An ability that itself copies abilities is never copied: a second Kitiaru's copy action is not offered.
        var s5 = Table(Phase.Action, 0);
        var kitiaru5 = Add(s5, Kitiaru, 0, Zone.Home);
        var other = Add(s5, Kitiaru, 1, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "kitiaru-copy", kitiaru5.Id, other.Id));
        Check(e5.State.Cards[kitiaru5.Id].GrantedAbilities.Count == 0, "an ability that copies abilities is not itself copied");
    }

    // ---- Michiru ----------------------------------------------------------------------------------------------------------------------------------

    private static void MichiruBarrier()
    {
        var s = Table(Phase.Action, 0);
        var michiru = Add(s, Michiru, 0, Zone.Home);
        var guarded = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "michiru-barrier", michiru.Id).TargetIds!.Contains(guarded.Id) && !Enabled(e, 0, "michiru-barrier", michiru.Id).TargetIds!.Contains(enemy.Id), "only your own Personality can be targeted");
        Accept(e, Offer(e, 0, "michiru-barrier", michiru.Id, guarded.Id));
        Eq(e.State.Cards[guarded.Id].BarrierTurn, e.State.TurnNumber, "the barrier stands for the turn");
        // The next destruction by another player's action this turn: negated, and he moves home.
        var battle = Edit(e, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0];
            st.Cards[guarded.Id].Zone = Zone.Battlefield; st.Cards[guarded.Id].ProvinceIndex = 0;
            Add(st, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
            Add(st, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        });
        var destroyers = battle.State.Cards.Values.Where(c => c.DefinitionId == "training-no-attachments-destroyer").OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Accept(battle, Offer(battle, 1, "training-destroy-no-attachments", destroyers[0].Id, guarded.Id));
        Eq(battle.State.Cards[guarded.Id].Zone, Zone.Home, "the destruction was negated and he was moved home");
        Eq(battle.State.Cards[guarded.Id].BarrierTurn, -1, "the barrier is spent");
        // The barrier is one shot: the second destruction goes through (he is brought back to the battlefield first).
        var again = Edit(battle, st =>
        {
            st.Cards[guarded.Id].Zone = Zone.Battlefield; st.Cards[guarded.Id].ProvinceIndex = 0; st.PriorityPlayerId = 1;
        });
        Accept(again, Offer(again, 1, "training-destroy-no-attachments", destroyers[1].Id, guarded.Id));
        Eq(again.State.Cards[guarded.Id].Zone, Zone.Dead, "the next destruction is not negated");

        // The barrier ends with the turn.
        var later = Edit(battle, st =>
        {
            st.Cards[guarded.Id].Zone = Zone.Battlefield; st.Cards[guarded.Id].ProvinceIndex = 0; st.Cards[guarded.Id].BarrierTurn = 3; st.TurnNumber = 4; st.PriorityPlayerId = 1;
        });
        Accept(later, Offer(later, 1, "training-destroy-no-attachments", destroyers[1].Id, guarded.Id));
        Eq(later.State.Cards[guarded.Id].Zone, Zone.Dead, "a barrier from an earlier turn is gone");
    }

    // ---- Genshi -----------------------------------------------------------------------------------------------------------------------------------

    private static void GenshiShame()
    {
        // His controller dishonors him: nothing further happens.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var genshi = Add(s, Genshi, 0, Zone.Home);
        var target = Add(s, "training-dual-attacker", 1, Zone.Home); // training-honor-cost-attack and training-weapon-attack
        var own = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Batch336Support.Options(e, 0, "genshi-shame", genshi.Id).Any(o => (o.TargetIds ?? []).Contains(own.Id)), "only another player's Personality can be targeted");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "genshi-shame", genshi.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the target's controller decides");
        Eq(e.State.PendingChoice?.PlayerId, 1, "player 1 chooses");
        var options = e.QueryActions(1).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(options.Select(o => o.Command!.Choice).OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(["dishonor", "refuse"]), "dishonor him, or not");
        Accept(e, new GameCommand("chooseEitherOr", 1, e.Version, Choice: "dishonor"));
        Check(e.State.Cards[target.Id].Dishonorable, "he was dishonored");
        Eq(Honor(e, 0), honor, "no Honor gained");
        Check(e.State.PendingChoice is null && e.State.DisabledAbilities.Count == 0, "and nothing is locked");

        // He refuses: Genshi's controller gains 1 Honor and chooses one of his abilities, locked until Genshi's controller's next turn begins.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var genshi2 = Add(s2, Genshi, 0, Zone.Home);
        var target2 = Add(s2, "training-dual-attacker", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Accept(e2, Offer(e2, 0, "genshi-shame", genshi2.Id, target2.Id));
        Accept(e2, new GameCommand("chooseEitherOr", 1, e2.Version, Choice: "refuse"));
        Eq(Honor(e2, 0), honor2 + 1, "Genshi's controller gained 1 Honor");
        Check(!e2.State.Cards[target2.Id].Dishonorable, "he was not dishonored");
        Eq(e2.State.PendingChoice?.Type, "effectChoice", "Genshi's controller chooses one of the Personality's abilities");
        var lockChoices = e2.QueryActions(0).Where(a => a.Type == "chooseEffect" && a.Enabled).Select(a => a.Command!).ToArray();
        Check(lockChoices.Select(c => c.Choice).OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(["training-honor-cost-attack", "training-weapon-attack"]), "one option per ability: " + string.Join(",", lockChoices.Select(c => c.Choice)));
        Accept(e2, lockChoices.Single(c => c.Choice == "training-weapon-attack"));
        Check(e2.State.PendingChoice is null || e2.State.PendingChoice.Type != "effectChoice", "the choice is made");
        Check(e2.State.DisabledAbilities.Any(d => d.CardId == target2.Id && d.UsageKey == "training-weapon-attack" && d.UntilPlayerId == 0), "the ability is locked until player 0's next turn begins");
        Check(!e2.State.Cards[genshi2.Id].Bowed, "Genshi pays no cost");
        // In a battle this turn, and on the other player's turn, the locked ability is refused and the other is not.
        var battle = Edit(e2, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 1;
            st.Cards[target2.Id].Zone = Zone.Battlefield; st.Cards[target2.Id].ProvinceIndex = 0;
            Add(st, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        });
        Check(Batch336Support.Options(battle, 1, "training-weapon-attack", target2.Id).All(o => !o.Enabled), "the chosen ability may not be used");
        Check(CanUse(battle, 1, "training-honor-cost-attack", target2.Id), "his other ability still may");
        var nextTurn = Edit(battle, st => st.TurnNumber++);
        Check(Batch336Support.Options(nextTurn, 1, "training-weapon-attack", target2.Id).All(o => !o.Enabled), "the lock outlasts the turn it was made in");
        var begun = Edit(nextTurn, st => st.Players[0].TurnsTaken++);
        Check(CanUse(begun, 1, "training-weapon-attack", target2.Id), "until Genshi's controller's next turn begins, and no longer");

        // A dishonorable Genshi performing the action is rehonored instead of his controller gaining the Honor.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var genshi3 = Add(s3, Genshi, 0, Zone.Home); genshi3.Dishonorable = true;
        var target3 = Add(s3, "training-recon-attacker", 1, Zone.Home); // one ability only: locked without asking
        var e3 = MatchEngine.FromTestState(s3);
        var honor3 = Honor(e3, 0);
        Accept(e3, Offer(e3, 0, "genshi-shame", genshi3.Id, target3.Id));
        Accept(e3, new GameCommand("chooseEitherOr", 1, e3.Version, Choice: "refuse"));
        Eq(Honor(e3, 0), honor3, "a dishonorable performer's Honor gain is his rehonoring instead");
        Check(!e3.State.Cards[genshi3.Id].Dishonorable, "Genshi was rehonored");
        Check(e3.State.PendingChoice?.Type != "effectChoice" && e3.State.DisabledAbilities.Any(d => d.CardId == target3.Id && d.UsageKey == "training-weapon-attack"), "a single ability is locked without a choice");
    }

    // ---- Seishi -----------------------------------------------------------------------------------------------------------------------------------

    private static void SeishiRehonor()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var seishi = Add(s, Seishi, 0, Zone.Home);
        var shamed = Add(s, "training-lion-samurai", 1, Zone.Home); shamed.Dishonorable = true;
        var honorable = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "seishi-rehonor", seishi.Id).TargetIds!;
        Check(targets.Contains(shamed.Id) && !targets.Contains(honorable.Id), "only a dishonorable Personality is offered, from either side");
        Accept(e, Offer(e, 0, "seishi-rehonor", seishi.Id, shamed.Id));
        Check(!e.State.Cards[shamed.Id].Dishonorable, "rehonored");
        Check(e.State.Cards[seishi.Id].Bowed, "Seishi bowed as the cost");
        // Bowed, he cannot.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var seishi2 = Add(s2, Seishi, 0, Zone.Home, bowed: true);
        var shamed2 = Add(s2, "training-lion-samurai", 1, Zone.Home); shamed2.Dishonorable = true;
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "seishi-rehonor", seishi2.Id), "a bowed Seishi cannot pay the cost");
        // With nobody dishonorable there is no target, so no action.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var seishi3 = Add(s3, Seishi, 0, Zone.Home);
        Add(s3, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "seishi-rehonor", seishi3.Id), "no dishonorable Personality: no target");
    }

    // ---- Tadanobu ---------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine ChallengeWonBy(int strongPlayer, bool withFavor, out CardInstance tadanobu, out CardInstance enemy)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        tadanobu = Bf(s, Tadanobu, 0);
        var challenge = Add(s, "training-duel-challenge", 0, Zone.Hand);
        enemy = Bf(s, "training-lion-samurai", 1);
        if (strongPlayer == 1) enemy.Modifiers.Add(new("test", "test", "Chi", 10));
        if (withFavor) s.FavorPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: challenge.Id, PerformerId: tadanobu.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 1, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 10; guard++)
            Accept(e, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", duel.TurnPlayerId, e.Version));
        return e;
    }

    private static void TadanobuDraw()
    {
        var e = ChallengeWonBy(0, true, out var tadanobu, out _);
        Eq(e.State.PendingChoice?.Type, "duelWonReaction", "the duel-won window opens for the winner");
        Eq(e.State.PendingChoice?.CardId, tadanobu.Id, "scoped to the Personality who won");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Use(e, 0, "tadanobu-draw", tadanobu.Id));
        Eq(e.State.Players[0].Hand.Count, hand + 2, "two cards drawn");
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded as the cost");

        // Without the Favor the Reaction cannot be used.
        var noFavor = ChallengeWonBy(0, false, out var tadanobu2, out _);
        Eq(noFavor.State.PendingChoice?.Type, "duelWonReaction");
        Check(!CanUse(noFavor, 0, "tadanobu-draw", tadanobu2.Id), "the Favor must be his controller's");
        Check(Rejection(noFavor, Use(noFavor, 0, "tadanobu-draw", tadanobu2.Id)).Contains("Imperial Favor"), "refused with the reason");

        // A duel he lost gives no window.
        var lost = ChallengeWonBy(1, true, out _, out _);
        Check(lost.State.PendingChoice?.Type != "duelWonReaction", "he did not win: no Reaction");
    }

    // ---- Makoto -----------------------------------------------------------------------------------------------------------------------------------

    private static void MakotoActions()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var makoto = Add(s, Makoto, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "makoto-favor", makoto.Id).Command!);
        Eq(e.State.FavorPlayerId, 0, "Makoto took the Imperial Favor");

        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var makoto2 = Bf(s2, Makoto, 0);
        var loser = Bf(s2, "training-lion-samurai", 1);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "makoto-duel", makoto2.Id, loser.Id));
        Check(e2.State.PendingDuel is not null && e2.State.PendingChoice?.Type != "acceptChallenge", "an unrefusable challenge");
        for (var guard = 0; e2.State.PendingDuel is { } duel && guard < 10; guard++)
            Accept(e2, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e2.State.PriorityPlayerId, e2.Version) : new GameCommand("strike", duel.TurnPlayerId, e2.Version));
        Eq(e2.State.Cards[loser.Id].Zone, Zone.Dead, "the loser was destroyed");
        Eq(e2.State.Cards[makoto2.Id].Zone, Zone.Battlefield, "the winner stands");

        // A stronger opponent wins: Makoto is the loser and is destroyed.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var makoto3 = Bf(s3, Makoto, 0);
        var strong = Bf(s3, "training-lion-samurai", 1); strong.Modifiers.Add(new("test", "test", "Chi", 20));
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "makoto-duel", makoto3.Id, strong.Id));
        for (var guard = 0; e3.State.PendingDuel is { } duel && guard < 10; guard++)
            Accept(e3, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e3.State.PriorityPlayerId, e3.Version) : new GameCommand("strike", duel.TurnPlayerId, e3.Version));
        Eq(e3.State.Cards[makoto3.Id].Zone, Zone.Dead, "Makoto lost his own duel and was destroyed");
        Check(e3.State.Cards[strong.Id].Zone == Zone.Battlefield, "the winner stands");
    }

    // ---- Hurunayi ---------------------------------------------------------------------------------------------------------------------------------

    private static void HurunayiNinja()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var hurunayi = Add(s, Hurunayi, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(hurunayi.Id, "Ninja") && e.HasKeyword(hurunayi.Id, "Courtier"), "a Ninja Courtier to begin with");
        Accept(e, Enabled(e, 0, "hurunayi-remove-ninja", hurunayi.Id).Command!);
        Check(!e.HasKeyword(hurunayi.Id, "Ninja"), "Ninja was removed from him");
        Check(e.HasKeyword(hurunayi.Id, "Courtier") && e.HasKeyword(hurunayi.Id, "Scorpion Clan"), "and nothing else");
        // It lasts the turn and no longer: the next player's turn begins with it restored.
        var startTurn = e.State.TurnNumber;
        RunUntil(e, () => e.State.TurnNumber > startTurn);
        Check(e.HasKeyword(hurunayi.Id, "Ninja"), "Ninja is back once the turn has ended");
    }

    // ---- Mitsuo -----------------------------------------------------------------------------------------------------------------------------------

    private static void MitsuoAdditionalAction()
    {
        // An enemy Battle action moves Mitsuo home (the move-both-home fixture moves its target and its source).
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var mitsuo = Bf(s, Mitsuo, 0);
        Bf(s, "training-crane-samurai", 0); // another unit of his controller's stays at the battlefield: presence for the Reaction
        var mover = Bf(s, "training-move-source", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-move-both-home", mover.Id, mitsuo.Id));
        Eq(e.State.Cards[mitsuo.Id].Zone, Zone.Home, "the action moved Mitsuo home");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "his Reaction window opens");
        var reaction = e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "mitsuo-additional-action");
        Accept(e, reaction.Command!);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "his controller may take an additional Battle action");

        // His own move counts too: Trusting Instinct moves its performing Personality to the battle (a move to the battlefield that names no target).
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        Bf(s2, "training-crane-samurai", 0);
        var mitsuo2 = Add(s2, Mitsuo, 0, Zone.Home);
        Bf(s2, "training-crane-veteran", 1);
        var instinct = Add(s2, TrustingInstinct, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: instinct.Id, AbilityId: "trusting-instinct-move", PerformerId: mitsuo2.Id));
        Eq(e2.State.Cards[mitsuo2.Id].Zone, Zone.Battlefield, "Mitsuo moved to the battlefield");
        Eq(e2.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window opens for a move he made himself");
        Check(e2.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "mitsuo-additional-action"), "his Reaction is offered");

        // An action that moved someone else does not trigger him.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var mitsuo3 = Bf(s3, Mitsuo, 0);
        var bystander = Bf(s3, "training-crane-samurai", 0);
        var mover3 = Bf(s3, "training-move-source", 1);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-move-both-home", mover3.Id, bystander.Id));
        Eq(e3.State.Cards[bystander.Id].Zone, Zone.Home, "the bystander moved home");
        Eq(e3.State.Cards[mitsuo3.Id].Zone, Zone.Battlefield, "Mitsuo did not");
        Check(!e3.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "mitsuo-additional-action"), "so his Reaction is not offered");
    }

    // ---- Nitoshi ----------------------------------------------------------------------------------------------------------------------------------

    private static void NitoshiActions()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var nitoshi = Add(s, Nitoshi, 0, Zone.Home);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Home);
        var spare = Add(s, "training-crane-veteran", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        // The destroyed Personality must be another one of yours than the target.
        Check(Rejection(e, Use(e, 0, "nitoshi-frame", nitoshi.Id, spare.Id, selections: [Pick("target", spare.Id), Pick("victim", spare.Id)])).Length > 0, "the victim must differ from the target");
        Check(Rejection(e, Use(e, 0, "nitoshi-frame", nitoshi.Id, target.Id, selections: [Pick("target", target.Id), Pick("victim", target.Id)])).Length > 0, "the victim is one of your own");
        var honor = Honor(e, 1);
        Accept(e, Use(e, 0, "nitoshi-frame", nitoshi.Id, target.Id, selections: [Pick("target", target.Id), Pick("victim", victim.Id)]));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the Personality of his own was destroyed as the cost");
        Check(e.State.Cards[target.Id].Dishonorable, "the target was dishonored");
        Eq(Honor(e, 1), honor - 3, "and his controller lost 3 Honor");
        Check(e.State.Cards[nitoshi.Id].Zone == Zone.Home && e.State.Cards[spare.Id].Zone == Zone.Home, "nobody else was destroyed");

        // Battle: a dishonorable Personality with attachments, a Follower, and a bare Personality are targets; an honorable Personality with an attachment is not.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var nitoshi2 = Bf(s2, Nitoshi, 0);
        var shamedWithFollower = Bf(s2, "training-lion-veteran", 1); shamedWithFollower.Dishonorable = true;
        Add(s2, "training-invest-follower", 1, Zone.Attached, province: 0, parent: shamedWithFollower.Id);
        var honorableWithFollower = Bf(s2, "training-lion-courtier", 1);
        var follower = Add(s2, "training-invest-follower", 1, Zone.Attached, province: 0, parent: honorableWithFollower.Id);
        var bare = Bf(s2, "training-lion-samurai", 1);
        var e2 = MatchEngine.FromTestState(s2);
        var targets = Enabled(e2, 0, "nitoshi-destroy", nitoshi2.Id).TargetIds!;
        Check(targets.Contains(shamedWithFollower.Id), "a dishonorable Personality, attachments and all");
        Check(targets.Contains(follower.Id), "a Follower is a card without attachments in a unit");
        Check(targets.Contains(bare.Id), "a Personality without attachments");
        Check(!targets.Contains(honorableWithFollower.Id), "an honorable Personality with an attachment is not");
        Accept(e2, Offer(e2, 0, "nitoshi-destroy", nitoshi2.Id, shamedWithFollower.Id));
        Eq(e2.State.Cards[shamedWithFollower.Id].Zone, Zone.Dead, "the dishonorable Personality was destroyed");
        var e3 = Edit(e2, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e3, 0, "nitoshi-destroy", nitoshi2.Id), "one use per turn");
    }

    // ---- Rentatsu ---------------------------------------------------------------------------------------------------------------------------------

    private static void RentatsuHold()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var rentatsu = Add(s, Rentatsu, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "rentatsu-hold", rentatsu.Id).Command!);
        Check(e.State.Cards[rentatsu.Id].Bowed && e.State.Cards[rentatsu.Id].RentatsuHeld, "he bowed as the cost and the hold stands");
        // Player 0's next turn begins: a player is chosen to lose 2 Honor.
        RunUntil(e, () => e.State.PendingChoice?.Type == "rentatsuLoss");
        Eq(e.State.PendingChoice?.PlayerId, 0, "his controller chooses");
        Check(e.State.Cards[rentatsu.Id].Bowed, "he is still bowed as the turn begins");
        var honor0 = Honor(e, 0); var honor1 = Honor(e, 1);
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseRentatsuLoss").ToArray();
        Eq(choices.Length, 2, "one option per player");
        Accept(e, choices.Single(a => a.Command!.TargetPlayerId == 1).Command!);
        Eq(Honor(e, 1), honor1 - 2, "the chosen player lost 2 Honor");
        Eq(Honor(e, 0), honor0, "the other did not");
        // The Straighten Phase then offers to leave him bowed.
        Eq(e.State.PendingChoice?.Type, "declineStraighten", "his straightening may be negated");
        var leave = e.QueryActions(0).Single(a => a.Type == "declineStraighten" && a.Command!.CardIds is { Length: 1 } ids && ids[0] == rentatsu.Id);
        Accept(e, leave.Command!);
        Check(e.State.Cards[rentatsu.Id].Bowed && e.State.Cards[rentatsu.Id].RentatsuHeld, "he stayed bowed, and the hold goes on");
        // ...and the loss comes again with the next of his controller's turns; this time he is allowed to straighten.
        Check(e.State.PendingChoice is null, "the turn goes on");
        RunUntil(e, () => e.State.PendingChoice?.Type == "rentatsuLoss");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseRentatsuLoss" && a.Command!.TargetPlayerId == 0).Command!);
        Eq(Honor(e, 0), honor0 - 2, "this time his own controller chose himself");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "declineStraighten" && a.Command!.CardIds is { Length: 0 }).Command!);
        Check(!e.State.Cards[rentatsu.Id].Bowed && !e.State.Cards[rentatsu.Id].RentatsuHeld, "he straightened, and his effect ended");
        var turnsTaken = e.State.Players[0].TurnsTaken;
        RunUntil(e, () => { Check(e.State.PendingChoice?.Type != "rentatsuLoss", "no further Honor loss: he no longer remains bowed"); return e.State.Players[0].TurnsTaken > turnsTaken && e.State.Phase == Phase.Action; });
        // A Rentatsu bowed some other way (never held) drains nothing.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var plain = Add(s2, Rentatsu, 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.ActivePlayerId == 0 && e2.State.Players[0].TurnsTaken > 1 && e2.State.Phase == Phase.Action);
        Check(!e2.State.Cards[plain.Id].Bowed, "a Rentatsu who was merely bowed straightens as usual");
    }

    // ---- Waru -------------------------------------------------------------------------------------------------------------------------------------

    private static void WaruBow()
    {
        // Battle half: always a Combat Segment.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var waru = Bf(s, Waru, 0);
        var enemy = Bf(s, "training-lion-samurai", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "waru-bow-battle", waru.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the Personality was bowed");
        // One use for both halves.
        var later = Edit(e, st => { st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.CurrentBattleProvince = null; Add(st, "training-courtier-personality", 0, Zone.Home); });
        Check(!CanUse(later, 0, "waru-bow-limited", waru.Id), "the Limited half is spent by the Battle half");

        // Limited half: only with a Courtier of yours.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var waru2 = Add(s2, Waru, 0, Zone.Home);
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "waru-bow-limited", waru2.Id), "without a Courtier the Limited action is not available");
        Check(Rejection(e2, Use(e2, 0, "waru-bow-limited", waru2.Id, target2.Id)).Contains("Courtier"), "refused with the reason");
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var waru3 = Add(s3, Waru, 0, Zone.Home);
        Add(s3, "training-courtier-personality", 0, Zone.Home);
        var target3 = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "waru-bow-limited", waru3.Id, target3.Id));
        Check(e3.State.Cards[target3.Id].Bowed, "bowed as a Limited action");
        // The Battle half is not usable outside a Combat Segment.
        Check(!CanUse(e2, 0, "waru-bow-battle", waru2.Id), "a Battle action needs a Combat Segment");
    }

    // ---- Kameyoi ----------------------------------------------------------------------------------------------------------------------------------

    private static void KameyoiShame()
    {
        (MatchEngine, string) Setup(string targetDef, bool dishonorable)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var kameyoi = Add(s, Kameyoi, 0, Zone.Home);
            var target = Add(s, targetDef, 1, Zone.Home); target.Dishonorable = dishonorable;
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "kameyoi-shame", kameyoi.Id, target.Id));
            return (e, target.Id);
        }
        Check(CardCatalog.Get("training-lion-courtier").PersonalHonor >= 3 && CardCatalog.Get("training-lion-samurai").PersonalHonor < 3, "the fixtures straddle a base Personal Honor of 3");
        var baseline = MatchEngine.CreateEconomyScenario().State.Players[1].Honor;
        var (honorableGame, honorableId) = Setup("training-lion-samurai", false);
        Check(honorableGame.State.Cards[honorableId].Dishonorable, "an honorable target is dishonored");
        Eq(Honor(honorableGame, 1), baseline, "and his controller loses nothing");
        var (low, lowId) = Setup("training-lion-samurai", true);
        Eq(Honor(low, 1), baseline - 1, "a dishonorable target with a base Personal Honor below 3 costs his controller 1 Honor");
        Check(low.State.Cards[lowId].Dishonorable, "and he stays dishonorable");
        var (high, highId) = Setup("training-lion-courtier", true);
        Eq(Honor(high, 1), baseline - 2, "a base Personal Honor of 3 or more costs 2");
        Check(high.State.Cards[highId].Dishonorable, "still dishonorable");
    }

    // ---- Tanzaki ----------------------------------------------------------------------------------------------------------------------------------

    private static void TanzakiBoost()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var tanzaki = Add(s, Tanzaki, 0, Zone.Home);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "tanzaki-boost", tanzaki.Id).TargetIds!;
        Check(targets.Contains(ally.Id) && targets.Contains(enemy.Id) && !targets.Contains(tanzaki.Id), "another Personality, of either side, never himself");
        var before = e.CardForceForTest(enemy.Id);
        Accept(e, Offer(e, 0, "tanzaki-boost", tanzaki.Id, enemy.Id));
        Eq(e.CardForceForTest(enemy.Id), before + 4, "+4F");
    }

    // ---- Yorimi -----------------------------------------------------------------------------------------------------------------------------------

    private static void YorimiWaiver()
    {
        // Three Spells' actions ("Bow this Shugenja"): the first two do not bow him, the third does.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var yorimi = Add(s, Yorimi, 0, Zone.Home);
        var winds = Enumerable.Range(0, 3).Select(_ => Add(s, WindOfTheMoon, 0, Zone.Attached, parent: yorimi.Id)).ToArray();
        var dead = Add(s, "training-lion-samurai", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        for (var i = 0; i < 2; i++)
        {
            Accept(e, Offer(e, 0, "wind-moon-honor", winds[i].Id, dead.Id));
            Check(!e.State.Cards[yorimi.Id].Bowed, $"Spell action {i + 1}: the cost of bowing Yorimi was ignored");
            Accept(e, Pass(e, 1));
        }
        Accept(e, Offer(e, 0, "wind-moon-honor", winds[2].Id, dead.Id));
        Check(e.State.Cards[yorimi.Id].Bowed, "the third time this turn he is bowed as the cost");
        Eq(e.PersonalHonorForTest(dead.Id), CardCatalog.Get("training-lion-samurai").PersonalHonor + 3, "all three actions took effect");

        // The allowance is per turn.
        var next = Edit(e, st =>
        {
            st.TurnNumber++; st.Cards[yorimi.Id].Bowed = false; st.PriorityPlayerId = 0; st.ActivePlayerId = 0;
            foreach (var wind in winds) st.Cards[wind.Id].AbilityUses.Clear();
        });
        Accept(next, Offer(next, 0, "wind-moon-honor", winds[0].Id, dead.Id));
        Check(!next.State.Cards[yorimi.Id].Bowed, "a new turn: the allowance is back");

        // A Political action: Incriminating Testimony's performing Courtier is Yorimi.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        Bf(s2, "training-crane-samurai", 0);
        var yorimi2 = Add(s2, Yorimi, 0, Zone.Home);
        var attacker = Bf(s2, "training-lion-samurai", 1);
        var testimony = Add(s2, IncriminatingTestimony, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "incriminating-testimony-bow", testimony.Id, attacker.Id, yorimi2.Id));
        Check(e2.State.Cards[attacker.Id].Bowed, "the target was bowed");
        Check(!e2.State.Cards[yorimi2.Id].Bowed, "a Political action: the cost of bowing Yorimi was ignored");

        // An action that is neither Political nor on a Spell bows him as usual (Temple of Hotei: "Bow your performing Shugenja").
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var yorimi3 = Add(s3, Yorimi, 0, Zone.Home);
        var temple = Add(s3, TempleOfHotei, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "temple-of-hotei-honor", temple.Id, yorimi3.Id).Command!);
        Check(e3.State.Cards[yorimi3.Id].Bowed, "no waiver for a plain action");
        Check(e3.State.Cards[yorimi3.Id].AbilityUses.Count == 0, "and no use of the allowance was spent");

        // Ignoring the cost of bowing him also means he need not be unbowed: a bowed Yorimi may take the Spell's action; a bowed Shugenja without the trait may not.
        var s5 = Table(Phase.Action, 0); s5.PriorityPlayerId = 0;
        var bowedYorimi = Add(s5, Yorimi, 0, Zone.Home, bowed: true);
        var wind5 = Add(s5, WindOfTheMoon, 0, Zone.Attached, parent: bowedYorimi.Id);
        var bowedOther = Add(s5, "training-seeds5-air-shugenja", 0, Zone.Home, bowed: true);
        var wind5b = Add(s5, WindOfTheMoon, 0, Zone.Attached, parent: bowedOther.Id);
        Add(s5, "training-lion-samurai", 1, Zone.Dead);
        var e5 = MatchEngine.FromTestState(s5);
        Check(CanUse(e5, 0, "wind-moon-honor", wind5.Id), "a bowed Yorimi need not pay the cost of bowing");
        Check(!CanUse(e5, 0, "wind-moon-honor", wind5b.Id), "another bowed Shugenja cannot");

        // The trait is Yorimi's own: another Shugenja's cost is not waived.
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var caster = Add(s4, "training-seeds5-air-shugenja", 0, Zone.Home);
        var wind4 = Add(s4, WindOfTheMoon, 0, Zone.Attached, parent: caster.Id);
        var dead4 = Add(s4, "training-lion-samurai", 1, Zone.Dead);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "wind-moon-honor", wind4.Id, dead4.Id));
        Check(e4.State.Cards[caster.Id].Bowed, "another Shugenja bows as usual");
    }
}
