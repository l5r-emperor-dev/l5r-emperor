using L5R.Core;
using static RegressionAssertions;

// The Shadow's Embrace pass, batch 3 (issue #476): the eighteen Strategies of strategy-wave-56, driven through QueryActions and the commands it offers. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), a fixture Personality substituted in as each card needs, granted whichever keyword the
// card's own text requires.
internal static class ShadowsEmbraceBatch3Tests
{
    private const string BurningFury = "oracle-l5r-9896", CapturedConvict = "oracle-l5r-9897", CowedAndDefeated = "oracle-l5r-9898", CrowdedStreets = "oracle-l5r-9900",
        DeepRoots = "oracle-l5r-9901", DefensiveGrill = "oracle-l5r-9902", DirectThePath = "oracle-l5r-9903", DuelToFirstBlood = "oracle-l5r-9904", FieldsOfVictory = "oracle-l5r-9906", ForgeYourSoul = "oracle-l5r-9907",
        ForgedDocuments = "oracle-l5r-9908", Forthrightness = "oracle-l5r-9909", Grief = "oracle-l5r-9910", CastTwoShadows = "oracle-l5r-9911", KitsukiMethods = "oracle-l5r-9912",
        KuonsLegacy = "oracle-l5r-9913", LessonsNeverForgotten = "oracle-l5r-9914", MethodicalQuestioning = "oracle-l5r-9915", OneShout = "oracle-l5r-9917";
    private const string GameOfDice = "oracle-l5r-25";

    private static readonly string[] Wave = [BurningFury, CapturedConvict, CowedAndDefeated, CrowdedStreets, DeepRoots, DefensiveGrill, DirectThePath, DuelToFirstBlood, FieldsOfVictory,
        ForgeYourSoul, ForgedDocuments, Forthrightness, Grief, CastTwoShadows, KitsukiMethods, KuonsLegacy, LessonsNeverForgotten, MethodicalQuestioning, OneShout];
    // Crippled Sensei and Easy Persuasion stay unwired (their blockers are in tools/content/build_card_inventory.py).
    private static readonly string[] Unwired = []; // Crippled Sensei is published in strategy-wave-103; Easy Persuasion in wave 90.

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Grant(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static bool Fought(MatchEngine e, string message) => e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains(message));

    public static void Run(TestRunner runner) => runner.Run("shadows-embrace-batch-3", [
        ("Published shape: strategy-wave-56 is exactly the nineteen wired Strategies, each with its scan's stats, keywords and text, its ability ids, its art and its manifest line; the two unwired cards are not published", PublishedShape),
        ("Burning Fury: its performing unbowed Personality's Melee Attack has strength 3 plus his Fire tokens plus his controller's Rings -- enough to destroy a Force 6 Personality only with them", BurningFuryScenario),
        ("Captured Convict: a Magistrate moves an enemy Personality home; a dishonorable one then hands its player the gain-or-lose-2-Honor choice, an honorable one asks nothing", CapturedConvictScenario),
        ("Cowed and Defeated: dishonors the target and costs his controller 1 Honor; the next action of another player's that targets the performer costs him 2 more, once; a battle that destroys the performer does the same", CowedAndDefeatedScenario),
        ("Crowded Streets: the Defender who plays it takes an additional Battle action; after an action his Personality at the battlefield performed resolves, he may move that performer home", CrowdedStreetsScenario),
        ("Deep Roots: a 7-base-Force performer loses his Force penalties, ignores a contribution negation and contributes his Force while bowed; a smaller Personality is not a performer", DeepRootsScenario),
        ("Defensive Grill: without a unit at the battle, moves a Personality there to another unresolved battlefield where he is opposed -- an unopposed one is not offered", DefensiveGrillScenario),
        ("Direct the Path: a Henshin puts an enemy Personality without attachments on top of its owner's Dynasty deck, and an enemy Follower on top of its owner's Fate deck", DirectThePathScenario),
        ("Duel to First Blood: refused, its player draws two; accepted, the loser bows, and its Focus Effect adds 1 Focus Value to each of its player's focused cards; Battle and Limited share one use", DuelToFirstBloodScenario),
        ("Fields of Victory: before the battle resolves, its controller's stronger army reduces each enemy Personality's and Follower's Force to 0; a weaker one changes nothing", FieldsOfVictoryScenario),
        ("Forge Your Soul: the top four Fate cards are shown; a shown Weapon may be attached to the performer at 4 less Gold, then the deck may be shuffled; a Kensai who showed no Weapon draws a card", ForgeYourSoulScenario),
        ("Forged Documents: the target's controller orders his province cards at the bottom of his Dynasty deck and the provinces refill face-up from its top", ForgedDocumentsScenario),
        ("Forthrightness: after the shown Strategy's next action resolves with no other Strategy action announced, the Samurai permanently gains 'Limited: Gain 1 Honor'; another Strategy first, and nothing is granted", ForthrightnessScenario),
        ("Grief: after an action destroys a player's Personality, dishonors another Personality of that player's and costs him its base Personal Honor; a second Grief has nothing left to react to", GriefScenario),
        ("I Cast Two Shadows: after an action moves the player's Ninja home from a battlefield, he moves to a different battlefield", CastTwoShadowsScenario),
        ("Kitsuki Methods: the target's controller discards a card at random, or refuses and the acting player chooses 2 Honor or the target's base Personal Honor loss; with no hand there is no refusal to make", KitsukiMethodsScenario),
        ("Kuon's Legacy: straightens a 7-base-Force performer, gives him +3F and locks his actions, optionally bowing a card at his location -- at home in the Action Phase, at the battlefield in battle", KuonsLegacyScenario),
        ("Lessons Never Forgotten: its Yojimbo's Melee 4 Attack, then -- only while its player owns a dead Courtier or Shugenja -- the choice of an attachment at the battlefield to destroy", LessonsNeverForgottenScenario),
        ("Methodical Questioning: looks at and reorders the top three cards of either Fate deck; bowing the Samurai returns the card to its owner's hand from the discard pile as the turn ends", MethodicalQuestioningScenario),
        ("One Shout: its performing opposed Personality gets +2F, or +4F with an attachment; unopposed, it is not offered", OneShoutScenario),
    ]);

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        var published = PublishedContent.StrategyWave56.Cards.Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();
        Check(published.SequenceEqual(Wave.Order(StringComparer.Ordinal)), "exactly the wired cards are published: " + string.Join(",", published));
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-56.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.Order(StringComparer.Ordinal).SequenceEqual(Wave.Order(StringComparer.Ordinal)), "the manifest pins exactly the wired cards");
        var expected = new Dictionary<string, (string[] Abilities, string Keywords, int Focus)>
        {
            [BurningFury] = (["burning-fury-attack"], "Fire", 2),
            [CapturedConvict] = (["captured-convict-move"], "", 3),
            [CowedAndDefeated] = (["cowed-defeated-dishonor"], "Political", 2),
            [CrowdedStreets] = ([], "Terrain", 1),
            [DeepRoots] = (["deep-roots-negate"], "", 2),
            [DefensiveGrill] = (["defensive-grill-move"], "", 3),
            [DirectThePath] = (["direct-the-path-return"], "Kiho", 2),
            [DuelToFirstBlood] = (["first-blood-focus", "first-blood-duel-battle", "first-blood-duel-limited"], "Iaijutsu", 1),
            [FieldsOfVictory] = (["fields-of-victory-zero"], "Terrain", 2),
            [ForgeYourSoul] = (["forge-your-soul-show"], "", 2),
            [ForgedDocuments] = (["forged-documents-order"], "", 4),
            [Forthrightness] = (["forthrightness-show"], "Bushido Virtue,Honesty", 2),
            [Grief] = (["grief-dishonor"], "", 3),
            [CastTwoShadows] = (["cast-two-shadows-move"], "Ninja", 3),
            [KitsukiMethods] = (["kitsuki-methods-interrogate"], "Political", 4),
            [KuonsLegacy] = (["kuons-legacy-rage"], "", 2),
            [LessonsNeverForgotten] = (["lessons-never-forgotten-attack"], "", 3),
            [MethodicalQuestioning] = (["methodical-questioning-look", "methodical-questioning-bow-look"], "Dark Virtue,Knowledge", 3),
            [OneShout] = (["one-shout-boost"], "", 3),
        };
        foreach (var id in Wave)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.GoldCost == 0 && !card.ScenarioOnly, $"{card.Name}: a published Strategy with the scan's Gold Cost 0");
            Check(card.Abilities!.SequenceEqual(expected[id].Abilities), $"{card.Name}: ability ids {string.Join(",", card.Abilities!)}");
            Check(card.Abilities!.All(AbilityCatalog.All.ContainsKey), $"{card.Name}: every ability id is defined");
            Eq(string.Join(",", card.Keywords ?? []), expected[id].Keywords, $"{card.Name}'s keywords");
            Eq(card.Focus, expected[id].Focus, $"{card.Name}'s Focus Value");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(CardCatalog.Get(CrowdedStreets).Effect, "crowded-streets", "Crowded Streets' trait is its Effect");
        Check(CardCatalog.Get(BurningFury).Text.StartsWith("Battle: Choose your performing unbowed Personality: Melee Attack"), "Burning Fury prints the scan's text");
        Check(CardCatalog.Get(DefensiveGrill).Text.StartsWith("Battle: Even if you control no units at the current battlefield"), "Defensive Grill prints the scan's text");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
    }

    private static void BurningFuryScenario()
    {
        foreach (var fueled in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0);
            var fury = Add(s, BurningFury, 0, Zone.Hand);
            var monk = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
            var victim = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
            if (fueled)
            {
                monk.Tokens.Add(new("tok-1", "Fire", "test", "Force", 1)); monk.Tokens.Add(new("tok-2", "Fire", "test", "Force", 1));
                Add(s, "training-ring-of-air", 0, Zone.Home);
            }
            var e = MatchEngine.FromTestState(s);
            Eq(e.AttackStrengthForTest("burning-fury-attack", monk.Id), fueled ? 6 : 3, "3, plus two Fire tokens and one Ring");
            var option = Enabled(e, 0, "burning-fury-attack", fury.Id, monk.Id);
            Check(option.TargetIds!.Contains(victim.Id), "a Force 6 Personality is a legal target whatever the strength");
            Accept(e, Offer(e, 0, "burning-fury-attack", fury.Id, victim.Id, monk.Id));
            for (var guard = 0; e.State.PendingAttack is not null && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
            if (fueled) Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "strength 6 destroys the Force 6 Personality");
            else Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "strength 3 does not destroy the Force 6 Personality");
            Eq(e.State.Cards[fury.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        }
    }

    private static void CapturedConvictScenario()
    {
        var s = Table(Phase.Battle, 0);
        var convict = Add(s, CapturedConvict, 0, Zone.Hand);
        var magistrate = Grant(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
        var villain = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        villain.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 1);
        Accept(e, Offer(e, 0, "captured-convict-move", convict.Id, villain.Id, magistrate.Id));
        Eq(e.State.Cards[villain.Id].Zone, Zone.Home, "he was moved home");
        Check(e.State.PendingChoice is { Type: "effectChoice", PlayerId: 0 }, "a dishonorable target hands the player the Honor choice");
        var lose = e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "opponent-lose");
        Accept(e, lose.Command!);
        Eq(Honor(e, 1), before - 2, "the chosen player lost 2 Honor");

        // An honorable, non-Kolat, non-Ninja, non-Shadowlands target: moved home, nothing asked.
        var s2 = Table(Phase.Battle, 0);
        var convict2 = Add(s2, CapturedConvict, 0, Zone.Hand);
        var magistrate2 = Grant(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
        var honest = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var plain = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "captured-convict-move", convict2.Id, plain.Id), "a Personality without Magistrate does not perform it");
        var honor0 = Honor(e2, 0); var honor1 = Honor(e2, 1);
        Accept(e2, Offer(e2, 0, "captured-convict-move", convict2.Id, honest.Id, magistrate2.Id));
        Eq(e2.State.Cards[honest.Id].Zone, Zone.Home, "moved home");
        Check(e2.State.PendingChoice?.Type != "effectChoice", "no choice is asked");
        Check(Honor(e2, 0) == honor0 && Honor(e2, 1) == honor1, "and nobody's Honor changed");
    }

    private static void CowedAndDefeatedScenario()
    {
        // The next action of another player's that targets the performer.
        var s = Table(Phase.Battle, 0);
        var cowed = Add(s, CowedAndDefeated, 0, Zone.Hand);
        var courtier = Add(s, "training-courtier-personality", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 1);
        Accept(e, Offer(e, 0, "cowed-defeated-dishonor", cowed.Id, target.Id, courtier.Id));
        Check(e.State.Cards[target.Id].Dishonorable, "the target is dishonored");
        Eq(Honor(e, 1), before - 1, "his controller lost 1 Honor");
        Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, courtier.Id));
        Check(e.State.Cards[courtier.Id].Bowed, "the other player's action targeted and bowed the performer");
        Eq(Honor(e, 1), before - 3, "and once it resolved, the target's controller lost 2 more Honor");
        Check(e.State.CowedAndDefeatedWatches.Count == 0, "the watch is spent");

        // A battle whose resolution destroys the performer.
        var s2 = Table(Phase.Battle, 0);
        var cowed2 = Add(s2, CowedAndDefeated, 0, Zone.Hand);
        var magistrate = Grant(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate"); // Force 3
        var target2 = Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "cowed-defeated-dishonor", cowed2.Id, target2.Id, magistrate.Id));
        for (var guard = 0; guard < 8 && !Fought(e2, "Province 1"); guard++)
            Accept(e2, e2.QueryActions(e2.State.PriorityPlayerId).First(a => a.Enabled && a.Type is "pass" or "declineReaction").Command!);
        Eq(e2.State.Cards[magistrate.Id].Zone, Zone.Dead, "the battle destroyed the performer");
        Check(e2.GetView(0).Events.Any(ev => ev.Type == "watchFired" && ev.Message.Contains("battle")), "Cowed and Defeated fired for the battle's destruction");
        Check(e2.State.CowedAndDefeatedWatches.Count == 0, "and is spent");
    }

    private static void CrowdedStreetsScenario()
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0; // the Defender's opportunity
        var streets = Add(s, CrowdedStreets, 0, Zone.Hand);
        var shout = Add(s, OneShout, 0, Zone.Hand);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var play = e.QueryActions(0).Single(a => a.Type == "playStrategy" && a.CardId == streets.Id);
        Accept(e, play.Command!);
        Eq(e.State.Cards[streets.Id].Zone, Zone.Battlefield, "Crowded Streets is in play at the battlefield");
        Eq(e.State.PriorityPlayerId, 0, "the Defender keeps priority for his additional Battle action");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional action is granted");
        Accept(e, Enabled(e, 0, "one-shout-boost", shout.Id, defender.Id).Command!);
        Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 0 }, "after his performed action resolves, the Terrain's controller is asked: " + e.State.PendingChoice?.Type);
        var home = e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "home");
        Accept(e, home.Command!);
        Eq(e.State.Cards[defender.Id].Zone, Zone.Home, "the performer moved home");
        Check(e.State.PendingChoice is null, "the action's tail carried on");
        Eq(e.State.PriorityPlayerId, 1, "and priority passed on");

        // The Attacker who plays it has no additional action.
        var s2 = Table(Phase.Battle, 1);
        var streets2 = Add(s2, CrowdedStreets, 1, Zone.Hand);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "playStrategy" && a.CardId == streets2.Id).Command!);
        Check(!e2.State.AdditionalActions.Any(g => g.PlayerId == 1), "the Attacker takes no additional action");
        Eq(e2.State.PriorityPlayerId, 0);
    }

    private static void DeepRootsScenario()
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        s.BattleNumber = 1;
        var roots = Add(s, DeepRoots, 0, Zone.Hand);
        var giant = Add(s, "training-high-force-personality", 0, Zone.Battlefield, province: 0); // printed Force 7
        var small = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        giant.Modifiers.Add(new("test-source", "test", "Force", -3));
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "deep-roots-negate", roots.Id, small.Id), "a Personality with less than 7 base Force does not perform it");
        Accept(e, Enabled(e, 0, "deep-roots-negate", roots.Id, giant.Id).Command!);
        Eq(e.CardForceForTest(giant.Id), 7, "his current Force penalty is negated");
        // Bowed, and barred from contributing (Defensive Blockade's stamp), he still contributes; a new penalty is not applied.
        e = Batch336Support.Edit(e, st =>
        {
            st.Cards[giant.Id].Bowed = true; st.Cards[giant.Id].ForceContributionNegatedBattle = 1;
            st.Cards[small.Id].Bowed = true;
        });
        for (var guard = 0; guard < 8 && !Fought(e, "Province 1"); guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type is "pass" or "declineReaction").Command!);
        Check(Fought(e, "attacking Force 6, defending Force 7"), "the bowed, blockaded giant contributed his 7 (the bowed small one nothing): " + string.Join(" | ", e.GetView(0).Events.Where(ev => ev.Type == "battleResolved").Select(ev => ev.Message)));
    }

    private static void DefensiveGrillScenario()
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        s.UnresolvedBattlefields = [0, 1, 2];
        var grill = Add(s, DefensiveGrill, 0, Zone.Hand);
        var raider = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1); // battlefield 1: the raider would be opposed there
        Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 2); // battlefield 2: only his own side
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "defensive-grill-move", grill.Id);
        Check(!e.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.ProvinceIndex == 0), "player 0 has no unit at the current battlefield (Absent)");
        var provinces = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "defensive-grill-move").Select(a => a.Command!.Province).ToArray();
        Check(provinces.Contains(1) && !provinces.Contains(2), "only a battlefield where he would be opposed is offered: " + string.Join(",", provinces));
        Reject(e, option.Command! with { TargetId = raider.Id, Province = 2 });
        Accept(e, option.Command! with { TargetId = raider.Id, Province = 1 });
        Check(e.State.Cards[raider.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 1 }, "he was moved to battlefield 1");
    }

    private static void DirectThePathScenario()
    {
        var s = Table(Phase.Battle, 0);
        var path = Add(s, DirectThePath, 0, Zone.Hand);
        var path2 = Add(s, DirectThePath, 0, Zone.Hand);
        var henshin = Grant(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Henshin");
        var henshin2 = Grant(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Henshin");
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "direct-the-path-return", path.Id, henshin.Id);
        Check(!option.TargetIds!.Contains(host.Id) && option.TargetIds.Contains(follower.Id) && option.TargetIds.Contains(bare.Id), "a card with attachments is not a target; a bare one and a Follower are");
        Accept(e, option.Command! with { TargetId = bare.Id });
        Eq(e.State.Cards[bare.Id].Zone, Zone.DynastyDeck);
        Eq(e.State.Players[1].DynastyDeck[0], bare.Id, "the Personality is on top of its owner's Dynasty deck");
        if (e.State.PriorityPlayerId == 1) Accept(e, new("pass", 1, e.Version));
        Accept(e, Offer(e, 0, "direct-the-path-return", path2.Id, follower.Id, henshin2.Id));
        Eq(e.State.Players[1].FateDeck[0], follower.Id, "the Follower is on top of its owner's Fate deck");
    }

    private static void DuelToFirstBloodScenario()
    {
        // Refused (the Limited half): its player draws two cards.
        {
            var s = Table(Phase.Action, 0);
            var blood = Add(s, DuelToFirstBlood, 0, Zone.Hand);
            var duelist = Add(s, "training-crane-samurai", 0, Zone.Home);
            var rival = Add(s, "training-lion-samurai", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "first-blood-duel-battle"), "the Battle half is not offered in the Action Phase");
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, Offer(e, 0, "first-blood-duel-limited", blood.Id, rival.Id, duelist.Id));
            Check(e.State.PendingDuel is { Stage: "AwaitingAccept" }, "the challenge may be refused");
            Accept(e, new("declineDuel", 1, e.Version));
            Eq(e.State.Players[0].Hand.Count, hand - 1 + 2, "the Strategy was spent and its player drew two cards");
            Check(!e.State.Cards[rival.Id].Bowed && !e.State.Cards[duelist.Id].Bowed, "no duel, no bow");
        }
        // Accepted (the Battle half): the Focus Effect's +1 on each focused card decides it, and the loser bows.
        {
            var s = Table(Phase.Battle, 0);
            var blood = Add(s, DuelToFirstBlood, 0, Zone.Hand);
            var focus = Add(s, DuelToFirstBlood, 0, Zone.Hand); // focused: FV 1, +1 from its own Focus Effect
            var duelist = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
            var rival = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "first-blood-duel-battle", blood.Id, rival.Id, duelist.Id));
            Accept(e, new("acceptDuel", 1, e.Version));
            for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
            Check(e.State.PendingDuel is { Stage: "Focusing", TurnPlayerId: 1 }, "focusing begins with the challenged player");
            Accept(e, new("focus", 1, e.Version, Choice: "deck"));
            var challengedFocus = e.State.PendingDuel!.ChallengedFocused[0];
            Accept(e, new("focus", 0, e.Version, focus.Id));
            Accept(e, new("strike", 1, e.Version));
            for (var guard = 0; e.State.PendingDuel is { } pending && guard < 6; guard++)
                Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version)
                    : pending.Stage == "OrderingEffects" ? new GameCommand("orderDuelFocusEffects", 0, e.Version, CardIds: [focus.Id]) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
            var result = e.State.LastDuel!.Result!;
            Eq(result.ChallengerTotal, 3 + 1 + 1, "Chi 3, Focus Value 1, and +1 from Duel to First Blood's Focus Effect");
            Eq(result.ChallengedTotal, 3 + CardCatalog.Get(e.State.Cards[challengedFocus].DefinitionId).Focus, "the other side has no bonus");
            var loser = result.WinnerId is null ? null : result.LoserIds.Single();
            if (loser is not null) Check(e.State.Cards[loser].Bowed, "the duel's loser is bowed");
            else Check(e.State.Cards[duelist.Id].Bowed && e.State.Cards[rival.Id].Bowed, "a tie: both lose and bow");
        }
    }

    private static void FieldsOfVictoryScenario()
    {
        foreach (var stronger in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1);
            s.PriorityPlayerId = 0;
            var terrain = Add(s, FieldsOfVictory, 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
            if (stronger) Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); // Force 5
            var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            Add(s, "training-follower", 1, Zone.Attached, parent: lion.Id, province: 0); // Force 2
            var e = MatchEngine.FromTestState(s);
            for (var guard = 0; guard < 8 && !Fought(e, "Province 1"); guard++)
                Accept(e, e.QueryActions(e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId).First(a => a.Enabled && a.Type is "pass" or "declineReaction").Command!);
            Check(stronger ? Fought(e, "attacking Force 0, defending Force 8") : Fought(e, "attacking Force 6, defending Force 3"),
                (stronger ? "the stronger army reduced the enemy Personality and Follower to 0" : "a weaker army changes nothing") + ": " + string.Join(" | ", e.GetView(0).Events.Where(ev => ev.Type == "battleResolved").Select(ev => ev.Message)));
            _ = terrain;
        }
    }

    private static void ForgeYourSoulScenario()
    {
        // A Weapon among the shown cards: attached at its cost less 4 (3 - 4: nothing to pay), then no shuffle -- the other shown cards stay on top, and nothing is drawn.
        {
            var s = Table(Phase.Action, 0);
            var forge = Add(s, ForgeYourSoul, 0, Zone.Hand);
            var smith = Grant(Add(s, "training-crane-samurai", 0, Zone.Home), "Kensai");
            var top = new[] { Add(s, "training-item", 0, Zone.FateDeck), Add(s, "training-costly-weapon", 0, Zone.FateDeck), Add(s, "training-follower", 0, Zone.FateDeck), Add(s, "training-item", 0, Zone.FateDeck) };
            s.Players[0].FateDeck.InsertRange(0, top.Select(c => c.Id));
            var e = MatchEngine.FromTestState(s);
            var gold = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, Enabled(e, 0, "forge-your-soul-show", forge.Id, smith.Id).Command!);
            Eq((e.GetView(1).ShownCards ?? []).Count(c => c.ShownByPlayerId == 0), 4, "the top four cards are shown to the opponent");
            Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 0 }, "the attach decision is open");
            var attach = e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "attach-1");
            Check(e.QueryActions(0).Count(a => a.Type == "chooseEitherOr") == 2, "one option per shown Weapon, and none");
            Accept(e, attach.Command!);
            Check(e.State.Cards[top[1].Id] is { Zone: Zone.Attached } weapon && weapon.AttachedTo == smith.Id, "the Weapon is attached to the performer");
            Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed), gold, "paying 4 less Gold: nothing was paid");
            Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 0 }, "then the shuffle decision");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "keep").Command!);
            Check(e.State.Players[0].FateDeck.Take(3).SequenceEqual([top[0].Id, top[2].Id, top[3].Id]), "the rest stay on top, unshuffled");
            Eq(e.State.Players[0].Hand.Count, hand - 1, "a Weapon was shown: no card is drawn");
            Check(e.State.PendingEitherOr is null && e.State.PriorityPlayerId == 1, "the action finished");
        }
        // No Weapon shown: a Kensai draws a card (after the shuffle decision); a performer who is not a Kensai does not.
        foreach (var kensai in new[] { true, false })
        {
            var s = Table(Phase.Action, 0);
            var forge = Add(s, ForgeYourSoul, 0, Zone.Hand);
            var smith = Add(s, "training-crane-samurai", 0, Zone.Home);
            if (kensai) Grant(smith, "Kensai");
            var top = Enumerable.Range(0, 4).Select(_ => Add(s, "training-follower", 0, Zone.FateDeck)).ToArray();
            s.Players[0].FateDeck.InsertRange(0, top.Select(c => c.Id));
            var e = MatchEngine.FromTestState(s);
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, Enabled(e, 0, "forge-your-soul-show", forge.Id, smith.Id).Command!);
            Check(e.QueryActions(0).Where(a => a.Type == "chooseEitherOr").Select(a => a.Command!.Choice).Order().SequenceEqual(["keep", "shuffle"]), "no Weapon shown: only the shuffle decision");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "shuffle").Command!);
            Eq(e.State.Players[0].Hand.Count, hand - 1 + (kensai ? 1 : 0), kensai ? "the Kensai drew a card" : "a non-Kensai draws nothing");
            Check(e.State.PendingEitherOr is null, "the action finished");
        }
    }

    private static void ForgedDocumentsScenario()
    {
        var s = Table(Phase.Action, 0);
        var forged = Add(s, ForgedDocuments, 0, Zone.Hand);
        var mark = Add(s, "training-lion-samurai", 1, Zone.Home);
        var owner = s.Players[1];
        var inProvinces = owner.Provinces.Where(p => !p.Destroyed && p.CardId is not null).OrderBy(p => p.Index).Select(p => p.CardId!).ToArray();
        Check(inProvinces.Length >= 2, "the Lion has cards in several provinces");
        s.Cards[inProvinces[0]].FaceUp = true;
        var topOfDeck = owner.DynastyDeck.Take(inProvinces.Length).ToArray();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "forged-documents-order", forged.Id, mark.Id));
        Check(e.State.PendingChoice is { Type: "effectChoice", PlayerId: 1 }, "the target's controller chooses the order");
        var orders = e.QueryActions(1).Where(a => a.Type == "chooseEffect").ToArray();
        Eq(orders.Length, Enumerable.Range(1, inProvinces.Length).Aggregate(1, (a, b) => a * b), "one option per order of his occupied provinces");
        Check(orders.All(o => !o.Label.Contains(CardCatalog.Get(s.Cards[inProvinces[1]].DefinitionId).Name) || s.Cards[inProvinces[1]].FaceUp), "a face-down card is never named");
        var reversed = orders.Single(o => o.Command!.Choice == "order-" + string.Join("-", Enumerable.Range(0, inProvinces.Length).Reverse().Select(i => owner.Provinces.First(p => p.CardId == inProvinces[i]).Index)));
        Accept(e, reversed.Command!);
        var deck = e.State.Players[1].DynastyDeck;
        Check(deck.TakeLast(inProvinces.Length).SequenceEqual(inProvinces.Reverse()), "the province cards are at the bottom, in the chosen order (the last lowest)");
        var refilled = e.State.Players[1].Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Index).Select(p => p.CardId!).ToArray();
        Check(refilled.Order(StringComparer.Ordinal).SequenceEqual(topOfDeck.Order(StringComparer.Ordinal)), "the provinces refilled from the top of the deck");
        Check(refilled.All(id => e.State.Cards[id].FaceUp), "face-up");
        Eq(e.State.Cards[forged.Id].Zone, Zone.FateDiscard);
    }

    private static void ForthrightnessScenario()
    {
        foreach (var interrupted in new[] { false, true })
        {
            var s = Table(Phase.Action, 0);
            var forth = Add(s, Forthrightness, 0, Zone.Hand);
            var dice = Add(s, GameOfDice, 0, Zone.Hand);
            var other = Add(s, MethodicalQuestioning, 0, Zone.Hand);
            var samurai = Add(s, "training-samurai", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var show = Enabled(e, 0, "forthrightness-show", forth.Id, samurai.Id);
            Check(show.TargetIds!.Contains(dice.Id) && !show.TargetIds.Contains(forth.Id), "another Strategy in the hand is shown");
            Accept(e, show.Command! with { TargetId = dice.Id });
            Check((e.GetView(1).ShownCards ?? []).Any(c => c.Definition.Id == GameOfDice), "the opponent sees the shown Strategy");
            if (e.State.PriorityPlayerId == 1) Accept(e, new("pass", 1, e.Version));
            if (interrupted)
            {
                Accept(e, Enabled(e, 0, "methodical-questioning-look", other.Id, samurai.Id).Command!);
                Accept(e, e.QueryActions(0).First(a => a.Type == "chooseLookDeck").Command!);
                Accept(e, e.QueryActions(0).First(a => a.Type == "arrangeTopCards").Command!);
                if (e.State.PriorityPlayerId == 1) Accept(e, new("pass", 1, e.Version));
            }
            Accept(e, Enabled(e, 0, "game-of-dice-draw", dice.Id).Command!);
            var granted = e.State.Cards[samurai.Id].GrantedAbilities.Any(g => g.AbilityId == "forthrightness-honor" && g.Duration == GrantDuration.Permanent);
            Eq(granted, !interrupted, interrupted ? "another Strategy action came first: nothing is granted" : "the Samurai permanently gains the ability");
            Check(e.State.ForthrightnessWatches.Count == 0, "the watch is spent either way");
            if (!interrupted)
            {
                if (e.State.PriorityPlayerId == 1) Accept(e, new("pass", 1, e.Version));
                var before = Honor(e, 0);
                Accept(e, Enabled(e, 0, "forthrightness-honor", samurai.Id).Command!);
                Eq(Honor(e, 0), before + 1, "Limited: Gain 1 Honor");
            }
        }
    }

    private static void GriefScenario()
    {
        var s = Table(Phase.Battle, 0);
        var fury = Add(s, BurningFury, 0, Zone.Hand);
        var grief = Add(s, Grief, 0, Zone.Hand);
        var grief2 = Add(s, Grief, 0, Zone.Hand);
        var monk = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2
        var mourner = Add(s, "training-lion-veteran", 1, Zone.Home); // base Personal Honor 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "burning-fury-attack", fury.Id, victim.Id, monk.Id));
        for (var guard = 0; e.State.PendingAttack is not null && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the action destroyed the Lion's Personality");
        Check(e.State.PendingChoice is { Type: "afterActionResolvedReaction" }, "the after-action window is open: " + e.State.PendingChoice?.Type);
        var option = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "grief-dishonor" && a.CardId == grief.Id);
        Check(option is not null && option.TargetIds!.Contains(mourner.Id) && !option.TargetIds.Contains(monk.Id), "Grief targets a Personality of the player who lost one");
        var before = Honor(e, 1);
        Accept(e, option!.Command! with { TargetId = mourner.Id });
        Check(e.State.Cards[mourner.Id].Dishonorable, "he is dishonored");
        Eq(Honor(e, 1), before - 3, "his controller lost his base Personal Honor");
        Check(!e.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "grief-dishonor" && a.CardId == grief2.Id), "only one copy of Grief reacts to that one resolution");
    }

    private static void CastTwoShadowsScenario()
    {
        var s = Table(Phase.Battle, 1);
        s.UnresolvedBattlefields = [0, 1];
        var convict = Add(s, CapturedConvict, 1, Zone.Hand);
        var shadows = Add(s, CastTwoShadows, 0, Zone.Hand);
        var magistrate = Grant(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Magistrate");
        var ninja = Grant(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); // presence (issue #1133): the Strategy's player keeps a unit at the battlefield after the Ninja was moved home
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "captured-convict-move", convict.Id, ninja.Id, magistrate.Id));
        Eq(e.State.Cards[ninja.Id].Zone, Zone.Home, "the Ninja was moved home from battlefield 0");
        // A Ninja: Captured Convict's own Honor choice comes first.
        if (e.State.PendingChoice is { Type: "effectChoice" }) Accept(e, e.QueryActions(1).First(a => a.Type == "chooseEffect" && a.Command!.Choice == "self-gain").Command!);
        Check(e.State.PendingChoice is { Type: "afterActionResolvedReaction" }, "the after-action window is open: " + e.State.PendingChoice?.Type);
        var offers = e.QueryActions(0).Where(a => a.Enabled && a.Command?.AbilityId == "cast-two-shadows-move" && a.CardId == shadows.Id).ToArray();
        Check(offers.Length > 0 && offers.All(o => o.Command!.Province == 1), "the move names a different battlefield only: " + string.Join(",", offers.Select(o => o.Command!.Province)));
        Accept(e, offers[0].Command! with { TargetId = ninja.Id });
        Check(e.State.Cards[ninja.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 1 }, "he cast his second shadow at battlefield 1");
    }

    private static void KitsukiMethodsScenario()
    {
        (MatchEngine E, CardInstance Methods, CardInstance Courtier, CardInstance Target) Build(bool opponentHand)
        {
            var s = Table(Phase.Action, 0);
            var methods = Add(s, KitsukiMethods, 0, Zone.Hand);
            var courtier = Add(s, "training-courtier-personality", 0, Zone.Home);
            var target = Add(s, "training-lion-samurai", 1, Zone.Home); // base Personal Honor 2
            if (opponentHand) { Add(s, GameOfDice, 1, Zone.Hand); Add(s, GameOfDice, 1, Zone.Hand); }
            return (MatchEngine.FromTestState(s), methods, courtier, target);
        }
        {
            var (e, methods, courtier, target) = Build(true);
            var h0 = Honor(e, 0); var h1 = Honor(e, 1);
            Accept(e, Offer(e, 0, "kitsuki-methods-interrogate", methods.Id, target.Id, courtier.Id));
            Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 1 }, "the target's controller decides first");
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "discard").Command!);
            Eq(e.State.Players[1].Hand.Count, 1, "a card was discarded at random");
            Check(Honor(e, 0) == h0 && Honor(e, 1) == h1, "and nobody's Honor changed");
            Check(e.State.PendingChoice is null, "the action finished");
        }
        {
            var (e, methods, courtier, target) = Build(true);
            var h1 = Honor(e, 1);
            Accept(e, Offer(e, 0, "kitsuki-methods-interrogate", methods.Id, target.Id, courtier.Id));
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "refuse").Command!);
            Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 0 }, "a refusal hands the acting player his choice");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "shame").Command!);
            Eq(Honor(e, 1), h1 - 2, "the target's controller lost his base Personal Honor");
            Eq(e.State.Players[1].Hand.Count, 2, "no card was discarded");
            Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 1, "the action finished and priority passed on");
        }
        {
            var (e, methods, courtier, target) = Build(false);
            var h0 = Honor(e, 0);
            Accept(e, Offer(e, 0, "kitsuki-methods-interrogate", methods.Id, target.Id, courtier.Id));
            Check(e.State.PendingChoice is { Type: "eitherOr", PlayerId: 0 }, "with an empty hand there is nothing to discard: the acting player chooses at once");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "honor").Command!);
            Eq(Honor(e, 0), h0 + 2, "gain 2 Honor");
        }
    }

    private static void KuonsLegacyScenario()
    {
        // The Open half, at home: a Holding at his location may be bowed.
        {
            var s = Table(Phase.Action, 0);
            var kuon = Add(s, KuonsLegacy, 0, Zone.Hand);
            var shout = Add(s, OneShout, 0, Zone.Hand);
            var berserker = Add(s, "training-high-force-personality", 0, Zone.Home, bowed: true);
            var small = Add(s, "training-crane-samurai", 0, Zone.Home);
            var holding = s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding && !c.Bowed);
            var enemyHome = Add(s, "training-lion-samurai", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, "kuons-legacy-rage", kuon.Id, small.Id), "a Personality with less than 7 base Force does not perform it");
            var option = Enabled(e, 0, "kuons-legacy-rage", kuon.Id, berserker.Id);
            var candidates = option.Slots!.Single(sl => sl.Slot == "bow").CardIds;
            Check(candidates.Contains(holding.Id) && candidates.Contains(small.Id) && !candidates.Contains(enemyHome.Id), "a card at his location (his home), never the other player's home");
            Accept(e, option.Command! with { Selections = [Pick("bow", holding.Id)], TargetId = holding.Id });
            Check(!e.State.Cards[berserker.Id].Bowed, "he straightened");
            Eq(e.CardForceForTest(berserker.Id), 10, "+3F");
            Check(e.State.Cards[holding.Id].Bowed, "the Holding was bowed");
            Check(e.State.Cards[berserker.Id].ActionLocks.Count > 0, "he may not perform actions");
            _ = shout;
        }
        // The Battle half: an enemy card at the battlefield; the bow is optional.
        {
            var s = Table(Phase.Battle, 0);
            var kuon = Add(s, KuonsLegacy, 0, Zone.Hand);
            var shout = Add(s, OneShout, 0, Zone.Hand);
            var berserker = Add(s, "training-high-force-personality", 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, "kuons-legacy-rage", kuon.Id, berserker.Id);
            Check(option.Slots!.Single(sl => sl.Slot == "bow").CardIds.Contains(enemy.Id), "the enemy at his battlefield is a candidate");
            Accept(e, option.Command! with { Selections = [], TargetId = null });
            Check(!e.State.Cards[enemy.Id].Bowed, "no card was named: nothing bowed");
            if (e.State.PriorityPlayerId == 1) Accept(e, new("pass", 1, e.Version));
            Check(!CanUse(e, 0, "one-shout-boost", shout.Id, berserker.Id), "his eyes went dead: he performs no other action this turn");
        }
    }

    private static void LessonsNeverForgottenScenario()
    {
        foreach (var mourning in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0);
            var lessons = Add(s, LessonsNeverForgotten, 0, Zone.Hand);
            var yojimbo = Grant(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Yojimbo");
            if (mourning) Add(s, "training-courtier-personality", 0, Zone.Dead);
            var victim = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            var host = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            var item = Add(s, "training-item", 1, Zone.Attached, parent: host.Id, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "lessons-never-forgotten-attack", lessons.Id, victim.Id, yojimbo.Id));
            for (var guard = 0; e.State.PendingAttack is not null && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "Melee 4 destroyed the Force 4 Personality");
            if (!mourning)
            {
                Check(e.State.PendingChoice?.Type != "effectChoice", "without a dead Courtier or Shugenja nothing more is asked");
                Eq(e.State.Cards[item.Id].Zone, Zone.Attached);
                continue;
            }
            Check(e.State.PendingChoice is { Type: "effectChoice", PlayerId: 0 }, "the optional attachment target is offered");
            var destroy = e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "destroy-" + item.Id);
            Check(e.QueryActions(0).Any(a => a.Type == "chooseEffect" && a.Command!.Choice == "none"), "destroying none is offered");
            Accept(e, destroy.Command!);
            Eq(e.State.Cards[item.Id].Zone, Zone.FateDiscard, "the attachment was destroyed");
            Eq(e.State.Cards[lessons.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
            Check(e.State.PendingChoice is null, "the action finished");
        }
    }

    private static void MethodicalQuestioningScenario()
    {
        foreach (var bow in new[] { true, false })
        {
            var s = Table(Phase.Action, 0);
            var questioning = Add(s, MethodicalQuestioning, 0, Zone.Hand);
            var samurai = Add(s, "training-samurai", 0, Zone.Home);
            var theirTop = s.Players[1].FateDeck.Take(3).ToArray();
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, bow ? "methodical-questioning-bow-look" : "methodical-questioning-look", questioning.Id, samurai.Id).Command!);
            Eq(e.State.Cards[samurai.Id].Bowed, bow, bow ? "the Samurai was bowed" : "the Samurai stays unbowed");
            var deckChoice = e.QueryActions(0).Single(a => a.Type == "chooseLookDeck" && a.Command!.TargetPlayerId == 1);
            Accept(e, deckChoice.Command!);
            var reversed = new[] { "2:top", "1:top", "0:top" };
            Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: reversed));
            Check(e.State.Players[1].FateDeck.Take(3).SequenceEqual(theirTop.Reverse()), "the opponent's top three are back in the chosen order");
            Eq(e.State.Cards[questioning.Id].Zone, Zone.FateDiscard, "the card is in the discard pile");
            Check(!CanUse(e, 0, "methodical-questioning-look", questioning.Id), "and one use a turn is spent");
            Batch336Support.RunUntil(e, () => e.State.ActivePlayerId == 1);
            Eq(e.State.Cards[questioning.Id].Zone, bow ? Zone.Hand : Zone.FateDiscard, bow ? "the card returned to its owner's hand before the turn ended" : "without the bow it stays in the discard pile");
        }
    }

    private static void OneShoutScenario()
    {
        var s = Table(Phase.Battle, 0);
        var shout = Add(s, OneShout, 0, Zone.Hand);
        var shout2 = Add(s, OneShout, 0, Zone.Hand);
        var bare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var armed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-item", 0, Zone.Attached, parent: armed.Id, province: 0);
        var athome = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "one-shout-boost", shout.Id, athome.Id), "a Personality away from the battlefield is not opposed");
        Accept(e, Enabled(e, 0, "one-shout-boost", shout.Id, bare.Id).Command!);
        Eq(e.CardForceForTest(bare.Id), 5, "+2F without an attachment (bowed or not)");
        if (e.State.PriorityPlayerId == 1) Accept(e, new("pass", 1, e.Version));
        Accept(e, Enabled(e, 0, "one-shout-boost", shout2.Id, armed.Id).Command!);
        Eq(e.CardForceForTest(armed.Id), 3 + 2 + 4, "+4F with an attachment (his Item's +2 included)");

        var s2 = Table(Phase.Battle, 0);
        var lone = Add(s2, OneShout, 0, Zone.Hand);
        var alone = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "one-shout-boost", lone.Id, alone.Id), "unopposed, it is not offered");
    }
}
