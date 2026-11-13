using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group D: the Strategies of strategy-wave-61, driven through QueryActions and the commands it offers. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires.
internal static class EmperorRemainingDTests
{
    private const string LawsStrength = "oracle-l5r-8194", LightOfJustice = "oracle-l5r-8199", PowerOfAWord = "oracle-l5r-8262", PowerOfOne = "oracle-l5r-8265", SerpentsDeception = "oracle-l5r-8311",
        SlowDeath = "oracle-l5r-8340", TrapIsSprung = "oracle-l5r-8394", UndetectableEnemy = "oracle-l5r-8961", UnseenAssailant = "oracle-l5r-9011", UsefulConnections = "oracle-l5r-9046",
        VeiledMenace = "oracle-l5r-9130", WallOfHonor = "oracle-l5r-9198", WellPrepared = "oracle-l5r-9287", WordsHaveStrength = "oracle-l5r-9377",
        KillingGrounds = "oracle-l5r-8188", ShadowCourt = "oracle-l5r-8313", SincerityOfTheDragon = "oracle-l5r-8338", TorchsFlameFlickers = "oracle-l5r-8639", WhiteShorePlain = "oracle-l5r-9323", TwistOfFate = "oracle-l5r-8917";
    private const string TacticalAssault = "oracle-l5r-9934";

    private static readonly string[] Wired = [KillingGrounds, LawsStrength, LightOfJustice, PowerOfAWord, PowerOfOne, SerpentsDeception, ShadowCourt, SincerityOfTheDragon, SlowDeath, TrapIsSprung,
        TorchsFlameFlickers, TwistOfFate, UndetectableEnemy, UnseenAssailant, UsefulConnections, VeiledMenace, WallOfHonor, WellPrepared, WhiteShorePlain, WordsHaveStrength];

    // The Strategies of the group left unwired (each with a blocker note in tools/content/ee_remD_blockers.json): never published.
    // Issue #492, ruling pass R2: The Honor of the Lion and The Loyalty of the Scorpion (delay Strategies) are wired and published now, in strategy-wave-63 (EmperorRulingR2Tests), so none is left unwired here.
    private static readonly string[] Unwired = [];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Deck(MatchState s, int owner) { var c = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }
    private static int Mod(MatchEngine e, string id, string stat) => e.State.Cards[id].Modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount);
    private static void SetForce(CardInstance card, int force) => card.Modifiers.Add(new("test-source", "test", "Force", force - CardCatalog.Get(card.DefinitionId).Force));
    private static void SetCost(CardInstance card, int cost) => card.Modifiers.Add(new("test-source", "test", "GoldCost", cost - CardCatalog.Get(card.DefinitionId).GoldCost));

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-D", [
        ("Published shape: strategy-wave-61 is exactly the wired Strategies, each with its scan's stats, keywords, text, ability ids and art; the group's unwired Strategies are not published", PublishedShape),
        ("The Slow Death: as a Focus Effect the duel's winner gets +3F; its Battle action straightens an opposed unit (either side's, Followers too) and has no target in an unopposed unit", SlowDeathScenario),
        ("Well Prepared: needs a Terrain the player controls; bows an unbowed card in a unit or straightens a bowed one -- one use between the two", WellPreparedScenario),
        ("Undetectable Enemy: after another player's action targets its player's Oni the action's effects are negated; no window for a non-Oni, and never against its own player's action", UndetectableEnemyScenario),
        ("Unseen Assailant: its Ninja bows to make a Ranged 3 Attack; against a Personality the player chooses to compare it with his Force or his Chi (ruling R5), against a Follower with its Force", UnseenAssailantScenario),
        ("The Trap is Sprung!: needs Reconnaissance and an unbowed Scout; bows an enemy card, and its whole unit for a Crane Clan player only", TrapIsSprungScenario),
        ("Veiled Menace: an unbowed Courtier makes a dishonorable Personality's controller lose 2 Honor, and 1 more each time he is assigned this turn, to either side", VeiledMenaceScenario),
        ("Useful Connections: a Courtier bows to give Cavalry, until the turn ends, to up to as many Personalities as his Chi and to each of their Followers", UsefulConnectionsScenario),
        ("Wall of Honor: the opposed performer moves home as a cost, the current battlefield's province gains or loses strength equal to the enemy units there this turn, and its player gains 2 Honor", WallOfHonorScenario),
        ("The Law's Strength: a Courtier or Magistrate moves a Personality home, and destroys him if that did not move him home", LawsStrengthScenario),
        ("The Light of Justice: a Magistrate at home moves to the battle when enemy units are there, straightens his unit, and an additional Battle action follows if a dishonorable Personality opposes him", LightOfJusticeScenario),
        ("The Power of One: a Commander straightens his unit (Battle or Open); or bows one or two enemy units whose combined total Gold Cost fits his unit's", PowerOfOneScenario),
        ("The Power of a Word: its Reaction lets the acting player lose 3 Honor or have the Battle action negated (an action or an Attack); its Battle action moves one to three enemy dishonorable Personalities home", PowerOfAWordScenario),
        ("The Serpent's Deception: a Monk moves home an enemy Personality whose unit has lower total Force, contributes Force while bowed this turn, and his player loses 3 Honor", SerpentsDeceptionScenario),
        ("Words Have Strength: after another player's Battle action or a battle's resolution destroyed one of its player's Courtiers or Magistrates, a chosen player loses 3 Honor -- one copy per resolution", WordsHaveStrengthScenario),
        ("Twist of Fate: as its player ends his Dynasty Phase he may use it to take one additional Limited or Open action, after which the Dynasty Phase carries on; a pass in the window, or in the action, ends the phase / the action, and only once a turn is he asked", TwistOfFateScenario),
        ("Torch's Flame Flickers: after engaging its Scout gives Reconnaissance, and the next card effect that bows the player's Personalities at the battlefield is negated (the whole effect, once); an enemy's are not covered", TorchsFlameFlickersScenario),
        ("White Shore Plain: a Terrain played at the battlefield gives each of its controller's Tacticians there the Reaction that adds his Chi to his Force before the battle resolves; nobody else has it", WhiteShorePlainScenario),
        ("The Killing Grounds: the first pass in the Combat Segment makes the passing player destroy one of his Personalities there (asked when he has several) -- once, and never for a player with none", KillingGroundsScenario),
        ("The Shadow Court: its defending Courtier bows an enemy Personality; when another player's action or a battle's resolution next destroys him this turn, its player draws and chooses a player who gains or loses 2 Honor", ShadowCourtScenario),
        ("The Sincerity of the Dragon: as a Focus Effect a winner's provinces get +2 strength for the turn; its Dragon Clan Personality names a number, and after his action from another Strategy of that Focus Value resolves he draws and a chosen player discards", SincerityOfTheDragonScenario),
    ]);

    private static void PublishedShape()
    {
        Check(PublishedContent.StrategyWave61.Cards.Select(c => c.Id).SequenceEqual(Wired), "strategy-wave-61 publishes exactly the wired cards");
        void Shape(string id, string name, int focus, string keywords, params string[] abilities)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == 0 && card.Focus == focus, $"{name}: {card.Type} {card.Name} {card.GoldCost} {card.Focus}");
            Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
            Check(card.Text.Split('\n').Distinct().Count() == card.Text.Split('\n').Length, $"{name}'s text is printed once");
        }
        Shape(KillingGrounds, "The Killing Grounds", 1, "Terrain");
        Shape(LawsStrength, "The Law's Strength", 3, "Political", "laws-strength-move");
        Shape(LightOfJustice, "The Light of Justice", 4, "Imperial,Political", "light-of-justice-move");
        Shape(PowerOfAWord, "The Power of a Word", 3, "Political", "power-word-reaction", "power-word-move");
        Shape(PowerOfOne, "The Power of One", 2, "", "power-one-straighten", "power-one-bow");
        Shape(SerpentsDeception, "The Serpent's Deception", 1, "Fear,Kiho", "serpents-deception-move");
        Shape(ShadowCourt, "The Shadow Court", 4, "", "shadow-court-bow");
        Shape(SincerityOfTheDragon, "The Sincerity of the Dragon", 4, "", "sincerity-dragon-focus", "sincerity-dragon-name");
        Shape(SlowDeath, "The Slow Death", 4, "", "slow-death-focus", "slow-death-straighten");
        Shape(TorchsFlameFlickers, "Torch's Flame Flickers", 1, "Recon", "torch-flame-recon");
        Shape(TwistOfFate, "Twist of Fate", 2, "", "twist-of-fate-grant");
        Shape(WhiteShorePlain, "White Shore Plain", 3, "Terrain");
        Eq(CardCatalog.Get(KillingGrounds).Effect, "killing-grounds", "The Killing Grounds' trait is its Effect");
        Eq(CardCatalog.Get(WhiteShorePlain).Effect, "white-shore-plain", "White Shore Plain's grant is its Effect");
        Shape(TrapIsSprung, "The Trap is Sprung!", 2, "", "trap-sprung-bow");
        Shape(UndetectableEnemy, "Undetectable Enemy", 1, "", "undetectable-enemy-negate");
        Shape(UnseenAssailant, "Unseen Assailant", 2, "Ninja", "unseen-assailant-attack");
        Shape(UsefulConnections, "Useful Connections", 3, "Political", "useful-connections-cavalry");
        Shape(VeiledMenace, "Veiled Menace", 2, "Political", "veiled-menace-loss");
        Shape(WallOfHonor, "Wall of Honor", 3, "", "wall-of-honor-raise", "wall-of-honor-lower");
        Shape(WellPrepared, "Well Prepared", 3, "", "well-prepared-bow", "well-prepared-straighten");
        Shape(WordsHaveStrength, "Words Have Strength", 2, "Political", "words-strength-resolution", "words-strength-action");
        Check(CardCatalog.Get(SlowDeath).Text.StartsWith("As a Focus Effect: After this duel ends, give its winner +3F."), "The Slow Death publishes its scan's wording");
        Check(CardCatalog.Get(WordsHaveStrength).Text.StartsWith("You may only play one copy of Words Have Strength in reaction to any one resolution."), "Words Have Strength prints its own limit");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-61.txt"));
        Check(listed.SequenceEqual(Wired), "the manifest lists exactly the wired cards");
        foreach (var id in Wired)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // Passes for whoever must act until `stop` holds.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static void DeclineWindows(MatchEngine e)
    {
        while (e.State.PendingChoice?.Type is "beforeActionReaction" or "attackReaction") Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void TwistOfFateScenario()
    {
        MatchEngine Build(out CardInstance twist, out CardInstance menace, out CardInstance courtier, out CardInstance victim, bool withTwist = true)
        {
            var s = Table(Phase.Dynasty, 0);
            twist = withTwist ? Add(s, TwistOfFate, 0, Zone.Hand) : Add(s, "training-focus-strategy", 0, Zone.Hand);
            menace = Add(s, VeiledMenace, 0, Zone.Hand); // an Open action
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
            victim = Add(s, "training-lion-veteran", 1, Zone.Home); victim.Dishonorable = true;
            return MatchEngine.FromTestState(s);
        }
        {
            // The window opens as the Dynasty Phase is ended; the Reaction takes one additional Open action, then the Dynasty Phase carries on.
            var e = Build(out var twist, out var menace, out var courtier, out var victim);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "endDynasty").Command!);
            Eq(e.State.PendingChoice?.Type, "twistOfFateReaction", "the window opens");
            Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase has not ended");
            var options = e.QueryActions(0);
            Check(options.Any(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "twist-of-fate-grant") && options.Any(a => a.Type == "declineReaction"), "its Reaction and the pass are offered");
            Check(options.All(a => a.Type is "useAbility" or "declineReaction" or "concede"), "nothing else is");
            Accept(e, options.Single(a => a.Type == "useAbility" && a.Command!.AbilityId == "twist-of-fate-grant").Command!);
            Eq(e.State.Cards[twist.Id].Zone, Zone.FateDiscard, "Twist of Fate was spent");
            Eq(e.State.Phase, Phase.Action, "the additional action is taken in the Action Phase");
            Eq(e.State.PriorityPlayerId, 0, "by its player");
            Check(e.State.TwistOfFateActive && e.State.PendingChoice is null, "no window is left open");
            var honor = Honor(e, 1);
            Accept(e, Offer(e, 0, "veiled-menace-loss", menace.Id, victim.Id, courtier.Id));
            Eq(Honor(e, 1), honor - 2, "the Open action resolved");
            Eq(e.State.Phase, Phase.Dynasty, "and the Dynasty Phase carries on");
            Eq(e.State.PriorityPlayerId, 0, "with its player");
            Check(!e.State.TwistOfFateActive && e.State.AdditionalActions.Count == 0, "nothing of the additional action is left");
            // No second action, and no second window: the phase may now end.
            Check(!e.QueryActions(0).Any(a => a.Type == "useAbility"), "a second Open action is not on offer in the Dynasty Phase");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "endDynasty").Command!);
            Check(e.State.PendingChoice?.Type != "twistOfFateReaction" && e.State.Phase != Phase.Dynasty, "the Dynasty Phase ended");
        }
        {
            // A Limited action: Thuggish Techniques bows a Personality of lower Force.
            var s = Table(Phase.Dynasty, 0);
            Add(s, TwistOfFate, 0, Zone.Hand);
            var thug = Add(s, "oracle-l5r-9938", 0, Zone.Hand);
            var bully = Add(s, "training-crane-veteran", 0, Zone.Home); SetForce(bully, 4);
            var weak = Add(s, "training-lion-samurai", 1, Zone.Home); SetForce(weak, 1);
            var e = MatchEngine.FromTestState(s);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "endDynasty").Command!);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command!.AbilityId == "twist-of-fate-grant").Command!);
            Accept(e, Offer(e, 0, "thuggish-techniques-bow", thug.Id, weak.Id, bully.Id));
            Check(e.State.Cards[weak.Id].Bowed, "Thuggish Techniques, a Limited action, bowed him");
            Eq(e.State.Phase, Phase.Dynasty, "and the Dynasty Phase carries on");
        }
        {
            // Passing the window: the Dynasty Phase ends and it is not asked again (a pass in the Dynasty Phase is the other way to end it).
            var e = Build(out var twist, out _, out _, out _);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "endDynasty").Command!);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "declineReaction").Command!);
            Check(e.State.Phase != Phase.Dynasty && e.State.PendingChoice?.Type != "twistOfFateReaction", "the phase ended");
            Eq(e.State.Cards[twist.Id].Zone, Zone.Hand, "Twist of Fate was not used");
        }
        {
            // Passing the additional action: the Dynasty Phase carries on, the Strategy stays spent; the window does not open a second time for an unused copy.
            var e = Build(out _, out _, out _, out _);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "endDynasty").Command!);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command!.AbilityId == "twist-of-fate-grant").Command!);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "pass").Command!);
            Eq(e.State.Phase, Phase.Dynasty, "back in the Dynasty Phase");
            Check(!e.State.TwistOfFateActive && e.State.PriorityPlayerId == 0, "with its player, the action forgone");
        }
        {
            // A pass command in the Dynasty Phase opens the same window.
            var e = Build(out _, out _, out _, out _);
            Accept(e, new("pass", 0, e.Version));
            Eq(e.State.PendingChoice?.Type, "twistOfFateReaction", "the window opens for a pass as well");
        }
        {
            // Without the card there is no window; and a Reaction that is not Twist of Fate is never offered in it.
            var e = Build(out _, out _, out _, out _, withTwist: false);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "endDynasty").Command!);
            Check(e.State.PendingChoice?.Type != "twistOfFateReaction" && e.State.Phase != Phase.Dynasty, "no window: the Dynasty Phase ended");
        }
    }

    private static void TorchsFlameFlickersScenario()
    {
        var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0, bowed: true); // no "unbowed" printed
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bowerA = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var bowerB = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var card = Add(s, TorchsFlameFlickers, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        Check(!CanUse(e, 0, "torch-flame-recon", card.Id, mine.Id) && CanUse(e, 0, "torch-flame-recon", card.Id, scout.Id), "only a Scout may perform, bowed or not");
        Accept(e, Enabled(e, 0, "torch-flame-recon", card.Id, scout.Id).Command!);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "its player has Reconnaissance there");
        PassUntil(e, () => e.State.PendingChoice is null);
        // The first effect that bows one of his Personalities there is negated; the next is not.
        Accept(e, Offer(e, 1, "training-bow-at-battle", bowerA.Id, mine.Id));
        DeclineWindows(e);
        Check(!e.State.Cards[mine.Id].Bowed, "the next card effect that bows his Personality was negated");
        Check(e.State.TorchWatches.Single().Fired, "and the watch is spent");
        var again = Batch336Support.Edit(e, st => st.PriorityPlayerId = 1);
        Accept(again, Offer(again, 1, "training-bow-at-battle", bowerB.Id, mine.Id));
        Check(again.State.Cards[mine.Id].Bowed, "a second effect bows him: only the next one was negated");

        // The effect that is negated is negated whole: nothing of his unit bows.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 1;
        var target = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var followerOfTarget = Add(s2, "training-follower", 0, Zone.Attached, province: 0, parent: target.Id);
        var attacker = Add(s2, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        s2.TorchWatches.Add(new TorchWatch(0, 0, s2.BattleNumber, "Torch's Flame Flickers"));
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-at-battle", attacker.Id, target.Id));
        DeclineWindows(e2);
        Check(!e2.State.Cards[target.Id].Bowed && !e2.State.Cards[followerOfTarget.Id].Bowed, "nothing of his unit bowed");

        // A watch covers only its own player's Personalities: player 1's watch does nothing for player 0's.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 1;
        var mine3 = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker3 = Add(s3, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        s3.TorchWatches.Add(new TorchWatch(1, 0, s3.BattleNumber, "Torch's Flame Flickers"));
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-at-battle", attacker3.Id, mine3.Id));
        DeclineWindows(e3);
        Check(e3.State.Cards[mine3.Id].Bowed, "a watch covers only its own player's Personalities");
    }

    private static void WhiteShorePlainScenario()
    {
        var s = Table(Phase.Battle, 0);
        var terrain = Add(s, WhiteShorePlain, 0, Zone.Hand);
        var tactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician");
        var plain = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var enemyTactician = Kw(Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Tactician");
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "playStrategy" && a.CardId == terrain.Id).Command!);
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain is in play at the battlefield");
        // Both players pass; before the battle resolves the controller's Tactician may use the granted Reaction.
        PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleReaction");
        Eq(e.State.PendingChoice!.CardId, tactician.Id, "the controller's Tactician is asked (nobody else has the ability)");
        var chi = e.PersonalityChiForTest(tactician.Id);
        Accept(e, Enabled(e, 0, "white-shore-plain-boost", tactician.Id).Command!);
        Eq(Mod(e, tactician.Id, "Force"), chi, "a Force bonus equal to his Chi");
        Eq(Mod(e, enemyTactician.Id, "Force"), 0, "the other player's Tactician has nothing");
        Eq(Mod(e, plain.Id, "Force"), 0, "and neither has a Personality who is not a Tactician");

        // A Tactician of the controller's at home does not have it either.
        var t = Table(Phase.Battle, 0);
        Add(t, WhiteShorePlain, 0, Zone.Battlefield, province: 0);
        var away = Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Tactician");
        Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var te = MatchEngine.FromTestState(t);
        Check(!te.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == "white-shore-plain-boost" && a.CardId == away.Id), "a Tactician away from the battlefield does not have the ability");
    }

    private static void KillingGroundsScenario()
    {
        MatchEngine Build(int passerUnits, out CardInstance[] passers, out CardInstance grounds)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1;
            grounds = Add(s, KillingGrounds, 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var made = new List<CardInstance>();
            for (var i = 0; i < passerUnits; i++) made.Add(Add(s, i % 2 == 0 ? "training-lion-samurai" : "training-lion-veteran", 1, Zone.Battlefield, province: 0));
            passers = made.ToArray();
            return MatchEngine.FromTestState(s);
        }
        {
            // The Defender passes with two Personalities there: he chooses one, it is destroyed, and the pass then completes.
            var e = Build(2, out var defenders, out _);
            Accept(e, new("pass", 1, e.Version));
            Eq(e.State.PendingChoice?.Type, "killingGroundsSelect", "the passing player must choose");
            Eq(e.State.PendingChoice!.PlayerId, 1, "he is the one who chooses");
            var options = e.QueryActions(1).Where(a => a.Type == "killingGroundsSelect").ToArray();
            Eq(options.Length, 2, "one option per Personality of his at the battlefield");
            Check(e.QueryActions(0).All(a => a.Type != "killingGroundsSelect"), "the other player has nothing to choose");
            Accept(e, options[0].Command!);
            Eq(defenders.Count(d => e.State.Cards[d.Id].Zone == Zone.Dead), 1, "one was destroyed");
            Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 0, "the pass went on: the other player has the next opportunity");
            // Mendon's ruling (issue #492, R4): "First time for each player" -- the OTHER player's first pass triggers it for him too (his lone Personality there is destroyed)...
            Accept(e, new("pass", 0, e.Version));
            Eq(e.State.Events.Count(ev => ev.Type == "killingGrounds"), 2, "the trait fired once for each player");
            Check(e.State.PendingChoice?.Type != "killingGroundsSelect", "one Personality: destroyed without asking");
        }
        {
            // One Personality: destroyed at once, no question.
            var e = Build(1, out var defenders, out _);
            Accept(e, new("pass", 1, e.Version));
            Eq(e.State.Cards[defenders[0].Id].Zone, Zone.Dead, "his only Personality there is destroyed");
            Check(e.State.PendingChoice is null, "nothing to ask");
        }
        {
            // None of his own there: nothing to destroy, but the first time is spent.
            var e = Build(0, out _, out var grounds);
            Accept(e, new("pass", 1, e.Version));
            Check(e.State.PendingChoice is null && e.State.Cards.Values.Count(c => c.Zone == Zone.Dead) == 0, "no Personality of his there: nothing is destroyed");
            Check(e.State.Cards[grounds.Id].AbilityUses.Count > 0, "and the trait is spent");
        }
        {
            // The Terrain is played from the hand as an ordinary Terrain, and whoever passes first loses his only Personality there.
            var s = Table(Phase.Battle, 0);
            var terrain = Add(s, KillingGrounds, 0, Zone.Hand);
            var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var defender = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var play = e.QueryActions(0).SingleOrDefault(a => a.Type == "playStrategy" && a.CardId == terrain.Id);
            Check(play is { Enabled: true }, "offered as a Terrain play: " + play?.Reason);
            Accept(e, play!.Command!);
            Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "in play at the battlefield");
            PassUntil(e, () => e.State.Cards[attacker.Id].Zone == Zone.Dead || e.State.Cards[defender.Id].Zone == Zone.Dead);
            Check(e.State.Cards[attacker.Id].Zone == Zone.Dead ^ e.State.Cards[defender.Id].Zone == Zone.Dead, "the first player to pass lost his only Personality there");
        }
    }

    private static void ShadowCourtScenario()
    {
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1; // player 0 attacks, player 1 defends
            var card = Add(s, ShadowCourt, 1, Zone.Hand);
            var courtier = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Courtier");
            var enemy = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var tactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician");
            var assault = Add(s, TacticalAssault, 0, Zone.Hand);
            Deck(s, 1);
            var e = MatchEngine.FromTestState(s);
            // The defending Courtier bows the enemy Personality; the watch is set.
            Accept(e, Offer(e, 1, "shadow-court-bow", card.Id, enemy.Id, courtier.Id));
            Check(e.State.Cards[enemy.Id].Bowed, "the target is bowed");
            Eq(e.State.ShadowCourtWatches.Count, 1, "the Courtier is watched for the turn");
            // Another player's action destroys him: his player draws and is asked whom, and how, to affect.
            e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
            var hand = e.State.Players[1].Hand.Count;
            var honor = (Honor(e, 0), Honor(e, 1));
            Accept(e, Offer(e, 0, "tactical-assault-destroy", assault.Id, courtier.Id, tactician.Id));
            DeclineWindows(e);
            Eq(e.State.Cards[courtier.Id].Zone, Zone.Dead, "the Courtier was destroyed by the other player's action");
            Eq(e.State.PendingChoice?.Type, "eitherOr", "his player is asked");
            Eq(e.State.PendingChoice!.PlayerId, 1, "the Courtier's controller decides");
            Eq(e.State.Players[1].Hand.Count, hand + 1, "he drew a card");
            Eq(e.QueryActions(1).Count(a => a.Type == "chooseEitherOr"), 4, "each player, gaining or losing 2 Honor");
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "opponent-lose").Command!);
            Eq(Honor(e, 0), honor.Item1 - 2, "the chosen player lost 2 Honor");
            Eq(Honor(e, 1), honor.Item2, "and the other's Honor is unchanged");
            Eq(e.State.ShadowCourtWatches.Count, 0, "the watch is spent");
        }
        {
            // The watch is for the Courtier who performed: another Courtier of his player's destroyed by another player's action starts nothing.
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var watched = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Courtier");
            var other = Kw(Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Courtier");
            var assault = Add(s, TacticalAssault, 0, Zone.Hand);
            var tactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician");
            s.ShadowCourtWatches.Add(new ShadowCourtWatch(watched.Id, 1, s.TurnNumber, assault.Id));
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "tactical-assault-destroy", assault.Id, other.Id, tactician.Id));
            DeclineWindows(e);
            Eq(e.State.Cards[other.Id].Zone, Zone.Dead, "another Courtier was destroyed");
            Check(e.State.PendingChoice?.Type != "eitherOr", "no decision: the watched Courtier is untouched");
            Eq(e.State.ShadowCourtWatches.Count, 1, "the watch waits");
            // And a watch from an earlier turn does nothing.
            var late = Batch336Support.Edit(MatchEngine.FromTestState(s), st => st.ShadowCourtWatches[0] = new ShadowCourtWatch(watched.Id, 1, st.TurnNumber - 1, assault.Id));
            Accept(late, Offer(late, 0, "tactical-assault-destroy", assault.Id, watched.Id, tactician.Id));
            DeclineWindows(late);
            Eq(late.State.Cards[watched.Id].Zone, Zone.Dead, "he was destroyed");
            Check(late.State.PendingChoice?.Type != "eitherOr", "the watch was for an earlier turn");
        }
        {
            // A battle resolution destroys the Courtier (the Attacker wins big): the decision comes before the resolution's Reactions and the battle then carries on.
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1;
            var courtier = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Courtier");
            var raider = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            SetForce(raider, 20);
            var card = Add(s, ShadowCourt, 1, Zone.FateDiscard);
            s.ShadowCourtWatches.Add(new ShadowCourtWatch(courtier.Id, 1, s.TurnNumber, card.Id));
            Deck(s, 1);
            var e = MatchEngine.FromTestState(s);
            var hand = e.State.Players[1].Hand.Count;
            PassUntil(e, () => e.State.PendingChoice?.Type == "eitherOr");
            Eq(e.State.Cards[courtier.Id].Zone, Zone.Dead, "the resolution destroyed him");
            Eq(e.State.Players[1].Hand.Count, hand + 1, "his player drew");
            var honor = Honor(e, 1);
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "self-gain").Command!);
            Eq(Honor(e, 1), honor + 2, "he chose himself and gaining");
            Check(e.State.PendingChoice?.Type != "eitherOr" && e.State.Phase != Phase.Finished, "the battle carried on");
        }
        {
            // A Personality who is not a Courtier may not perform, and the performing Courtier must be defending.
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1;
            var card = Add(s, ShadowCourt, 1, Zone.Hand);
            var plain = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var attackingCourtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 1, "shadow-court-bow", card.Id, plain.Id), "a Personality who is not a Courtier may not perform");
            var t = Table(Phase.Battle, 0); // player 0 attacks
            var card2 = Add(t, ShadowCourt, 0, Zone.Hand);
            var attacker = Kw(Add(t, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Check(!CanUse(MatchEngine.FromTestState(t), 0, "shadow-court-bow", card2.Id, attacker.Id), "the performing Courtier must be defending");
            Check(attackingCourtier.Id.Length > 0);
        }
    }

    private static void SincerityOfTheDragonScenario()
    {
        var dragonId = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Clan == "Dragon").Id;
        // The Focus Effect: player 0's Personality wins a Steel on Steel duel with the Sincerity focused: all his provinces +2 strength this turn.
        var s = Table(Phase.Battle, 0);
        var winner = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        winner.Modifiers.Add(new("test-source", "test", "Chi", 5));
        var loser = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duelCard = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s, SincerityOfTheDragon, 0, Zone.Hand);
        Deck(s, 1);
        var e = MatchEngine.FromTestState(s);
        var before = e.GetView(0).Players[0].Provinces.Select(p => p.Strength).ToArray();
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duelCard.Id, loser.Id, winner.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        Check(e.State.Cards[loser.Id].Zone == Zone.Dead, "player 0's Personality won the duel");
        var after = e.GetView(0).Players[0].Provinces.Select(p => p.Strength).ToArray();
        for (var i = 0; i < before.Length; i++) Eq(after[i], before[i] + 2, $"province {i + 1} has +2 strength");

        // A duel its owner lost gives nothing.
        var l = Table(Phase.Battle, 0);
        var weak = Add(l, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var strong = Add(l, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        strong.Modifiers.Add(new("test-source", "test", "Chi", 9));
        var duel2 = Add(l, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused2 = Add(l, SincerityOfTheDragon, 0, Zone.Hand);
        Deck(l, 1);
        var le = MatchEngine.FromTestState(l);
        var beforeLoss = le.GetView(0).Players[0].Provinces.Select(p => p.Strength).ToArray();
        Accept(le, Offer(le, 0, "steel-on-steel-duel", duel2.Id, strong.Id, weak.Id));
        Accept(le, new GameCommand("focus", 1, le.Version, Choice: "deck"));
        Accept(le, new GameCommand("focus", 0, le.Version, CardId: focused2.Id));
        Accept(le, new GameCommand("strike", 1, le.Version));
        Check(le.GetView(0).Players[0].Provinces.Select(p => p.Strength).SequenceEqual(beforeLoss), "the duel was lost: no strength");

        // The Open half: name a number; after the next resolution of an action from another Strategy with that base Focus Value it draws and a chosen player discards.
        MatchEngine Named(int number, out CardInstance menace, out CardInstance courtier, out CardInstance victim)
        {
            var a = Table(Phase.Action, 0);
            a.Players[0].Clan = "Dragon"; // "If you are a Dragon Clan player" (the corrected scan, issue #492 ruling R3)
            var sincerity = Add(a, SincerityOfTheDragon, 0, Zone.Hand);
            menace = Add(a, VeiledMenace, 0, Zone.Hand); // base Focus Value 2
            var dragon = Add(a, dragonId, 0, Zone.Home);
            courtier = Kw(Add(a, "training-crane-courtier", 0, Zone.Home), "Courtier");
            victim = Add(a, "training-lion-veteran", 1, Zone.Home); victim.Dishonorable = true;
            Deck(a, 0);
            Add(a, "training-focus-strategy", 1, Zone.Hand);
            Add(a, "training-focus-strategy", 0, Zone.Hand);
            var ae = MatchEngine.FromTestState(a);
            var named = Enabled(ae, 0, "sincerity-dragon-name", sincerity.Id, dragon.Id);
            Accept(ae, named.Command!);
            Eq(ae.State.PendingChoice?.Type, "effectChoice", "the number is a choice");
            Eq(ae.QueryActions(0).Count(o => o.Type == "chooseEffect"), 7, "numbers 0 to 6");
            Accept(ae, ae.QueryActions(0).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == $"name-{number}").Command!);
            Eq(ae.State.SincerityWatches.Single().Number, number, "the number is recorded");
            return ae;
        }
        {
            var ae = Named(2, out var menace, out var courtier, out var victim);
            var theirs = ae.State.Players[1].Hand.Count;
            ae = Batch336Support.Edit(ae, st => st.PriorityPlayerId = 0);
            var mine = ae.State.Players[0].Hand.Count;
            Accept(ae, Offer(ae, 0, "veiled-menace-loss", menace.Id, victim.Id, courtier.Id));
            Eq(ae.State.PendingChoice?.Type, "eitherOr", "the watched action resolved: its player chooses a player");
            Eq(ae.State.Players[0].Hand.Count, mine, "he drew (Veiled Menace left the hand)");
            Accept(ae, ae.QueryActions(0).Single(o => o.Type == "chooseEitherOr" && o.Command!.Choice == "opponent").Command!);
            Eq(ae.State.PendingChoice?.Type, "forcedDiscard", "the chosen player must discard a card");
            Eq(ae.State.PendingChoice!.PlayerId, 1, "player 1 is the one");
            var discard = ae.State.Players[1].Hand.First();
            Accept(ae, new GameCommand("forcedDiscard", 1, ae.Version, TargetId: discard));
            Eq(ae.State.Players[1].Hand.Count, theirs - 1, "he discarded a card");
            Eq(ae.State.SincerityWatches.Count, 0, "the watch is spent");
        }
        {
            // A base Focus Value that is not the number does not trigger it, and the watch waits.
            var ae = Named(3, out var menace, out var courtier, out var victim);
            ae = Batch336Support.Edit(ae, st => st.PriorityPlayerId = 0);
            Accept(ae, Offer(ae, 0, "veiled-menace-loss", menace.Id, victim.Id, courtier.Id));
            Check(ae.State.PendingChoice?.Type != "eitherOr", "Veiled Menace has base Focus Value 2, not 3");
            Eq(ae.State.SincerityWatches.Count, 1, "the watch waits");
        }
    }

    private static void SlowDeathScenario()
    {
        // The Battle action: an opposed unit of either side, Followers straightened with it.
        var s = Table(Phase.Battle, 0);
        var card = Add(s, SlowDeath, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var mineFollower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: mine.Id);
        var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, bowed: true, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "slow-death-straighten", card.Id);
        Check(option.TargetIds!.Contains(mine.Id) && option.TargetIds.Contains(theirs.Id), "either side's opposed unit is a target");
        Accept(e, Offer(e, 0, "slow-death-straighten", card.Id, mine.Id));
        Check(!e.State.Cards[mine.Id].Bowed && !e.State.Cards[mineFollower.Id].Bowed, "the whole unit straightened");
        Check(e.State.Cards[theirs.Id].Bowed, "the other unit is untouched");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");

        // An unopposed unit is not a target: with no enemy unit there the action has none at all.
        var t = Table(Phase.Battle, 0);
        var lonely = Add(t, SlowDeath, 0, Zone.Hand);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "slow-death-straighten", lonely.Id), "no opposed unit, no target");

        // The Focus Effect: player 0's Personality wins a Steel on Steel duel with The Slow Death focused, and gets +3F.
        var s3 = Table(Phase.Battle, 0);
        var winner = Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        winner.Modifiers.Add(new("test-source", "test", "Chi", 5));
        var loser = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duelCard = Add(s3, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s3, SlowDeath, 0, Zone.Hand);
        Deck(s3, 1);
        var e3 = MatchEngine.FromTestState(s3);
        var forceBefore = Mod(e3, winner.Id, "Force");
        Accept(e3, Offer(e3, 0, "steel-on-steel-duel", duelCard.Id, loser.Id, winner.Id));
        Accept(e3, new GameCommand("focus", 1, e3.Version, Choice: "deck"));
        Accept(e3, new GameCommand("focus", 0, e3.Version, CardId: focused.Id));
        Accept(e3, new GameCommand("strike", 1, e3.Version));
        Check(e3.State.Cards[loser.Id].Zone == Zone.Dead, "player 0's Personality won the duel");
        Eq(Mod(e3, winner.Id, "Force"), forceBefore + 3, "the winner has +3F");
    }

    private static void WellPreparedScenario()
    {
        MatchEngine Build(int terrainOwner, out CardInstance card, out CardInstance enemy, out CardInstance ownBowed)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, WellPrepared, 0, Zone.Hand);
            Add(s, "training-terrain", terrainOwner, Zone.Battlefield, province: 0);
            ownBowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
            enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        // A Terrain the opponent controls does not count.
        var foreign = Build(1, out var card0, out _, out _);
        Check(!CanUse(foreign, 0, "well-prepared-bow", card0.Id) && !CanUse(foreign, 0, "well-prepared-straighten", card0.Id), "the player controls no Terrain: not playable");

        var e = Build(0, out var card, out var enemy, out var ownBowed);
        var bow = Enabled(e, 0, "well-prepared-bow", card.Id);
        Check(bow.TargetIds!.Contains(enemy.Id) && bow.TargetIds.Contains(ownBowed.Id), "a card in a unit of either side may be targeted");
        Accept(e, Offer(e, 0, "well-prepared-bow", card.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");

        var f = Build(0, out var card2, out _, out var tired);
        Accept(f, Offer(f, 0, "well-prepared-straighten", card2.Id, tired.Id));
        Check(!f.State.Cards[tired.Id].Bowed, "straightened");
        // Two copies: the shared use is per card, so a second copy is a second use.
        var s2 = Table(Phase.Battle, 0);
        var one = Add(s2, WellPrepared, 0, Zone.Hand);
        var two = Add(s2, WellPrepared, 0, Zone.Hand);
        Add(s2, "training-terrain", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(s2);
        Accept(g, Offer(g, 0, "well-prepared-bow", one.Id, target.Id));
        Eq(g.State.Cards[one.Id].Zone, Zone.FateDiscard, "the first copy was played");
    }

    private static void UndetectableEnemyScenario()
    {
        // Player 1's Courtier moves player 0's Personality home with The Law's Strength; player 0 holds Undetectable Enemy.
        MatchEngine Build(bool oni, out CardInstance law, out CardInstance undetectable, out CardInstance victim, out CardInstance courtier)
        {
            var s = Table(Phase.Battle, 1);
            law = Add(s, LawsStrength, 1, Zone.Hand);
            courtier = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Courtier");
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (oni) Kw(victim, "Oni");
            undetectable = Add(s, UndetectableEnemy, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(true, out var law, out var undetectable, out var victim, out var courtier);
            Accept(e, Offer(e, 1, "laws-strength-move", law.Id, victim.Id, courtier.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeted Oni's player may react");
            Accept(e, Enabled(e, 0, "undetectable-enemy-negate", undetectable.Id).Command!);
            DeclineWindows(e);
            Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "the action's effects were negated: he was not moved home (nor destroyed)");
            Eq(e.State.Cards[undetectable.Id].Zone, Zone.FateDiscard, "Undetectable Enemy was spent");
        }
        {
            var e = Build(false, out var law, out var undetectable, out var victim, out var courtier);
            Accept(e, Offer(e, 1, "laws-strength-move", law.Id, victim.Id, courtier.Id));
            Check(e.State.PendingChoice?.Type != "beforeActionReaction", "no window for a Personality that is not an Oni");
            Eq(e.State.Cards[victim.Id].Zone, Zone.Home, "he was moved home");
        }
        {
            // Never against its own player's action: player 0 targets his own Oni.
            var s = Table(Phase.Battle, 0);
            var law = Add(s, LawsStrength, 0, Zone.Hand);
            var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            var oni = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Oni");
            Add(s, UndetectableEnemy, 0, Zone.Hand);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "laws-strength-move", law.Id, oni.Id, courtier.Id));
            Check(e.State.PendingChoice?.Type != "beforeActionReaction", "another player's action only");
        }
    }

    private static void UnseenAssailantScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance ninja, out CardInstance weakChi, out CardInstance strongChi, out CardInstance followed, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, UnseenAssailant, 0, Zone.Hand);
            ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
            weakChi = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            SetForce(weakChi, 9);
            weakChi.Modifiers.Add(new("test-source", "test", "Chi", 3 - CardCatalog.Get(weakChi.DefinitionId).Chi));
            strongChi = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            SetForce(strongChi, 9);
            strongChi.Modifiers.Add(new("test-source", "test", "Chi", 6 - CardCatalog.Get(strongChi.DefinitionId).Chi));
            followed = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
            SetForce(follower, 2);
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(out var card, out var ninja, out var weakChi, out _, out var followed, out var follower);
            Check(!CanUse(e, 0, "unseen-assailant-attack", card.Id, weakChi.Id), "only a Ninja may perform");
            var option = Enabled(e, 0, "unseen-assailant-attack", card.Id, ninja.Id);
            Check(option.NoTarget && option.TargetIds!.Contains(weakChi.Id) && option.TargetIds.Contains(follower.Id) && !option.TargetIds.Contains(followed.Id), "a Follower or a Personality without Followers, or no target at all");
            // Issue #492 (ruling R5): "you may compare it against his Chi" is the player's choice: Force (the default) or Chi, each its own listed option.
            var forceRun = Build(out var card1, out var ninja1, out var weak1, out _, out _, out _);
            Accept(forceRun, Batch336Support.OfferChoice(forceRun, 0, "unseen-assailant-attack", card1.Id, weak1.Id, "force", ninja1.Id));
            Eq(forceRun.State.Cards[weak1.Id].Zone, Zone.Battlefield, "the player compared Force (9): above the strength, he survives");
            Accept(e, Batch336Support.OfferChoice(e, 0, "unseen-assailant-attack", card.Id, weakChi.Id, "chi", ninja.Id));
            Check(e.State.Cards[ninja.Id].Bowed, "the Ninja bowed as the cost");
            Eq(e.State.Cards[weakChi.Id].Zone, Zone.Dead, "Force 9 is above the strength, but the player compared his Chi 3");
        }
        {
            var e = Build(out var card, out var ninja, out _, out var strongChi, out _, out _);
            Accept(e, Batch336Support.OfferChoice(e, 0, "unseen-assailant-attack", card.Id, strongChi.Id, "chi", ninja.Id));
            Eq(e.State.Cards[strongChi.Id].Zone, Zone.Battlefield, "Force 9 and Chi 6 are both above 3: he survives");
        }
        {
            var e = Build(out var card, out var ninja, out _, out _, out _, out var follower);
            Accept(e, Offer(e, 0, "unseen-assailant-attack", card.Id, follower.Id, ninja.Id));
            Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "a Follower is compared with its Force (2): destroyed");
            Check(!Batch336Support.Options(e, 0, "unseen-assailant-attack", card.Id, ninja.Id).Any(o => o.Enabled && o.Command?.Choice == "chi" && (o.TargetIds?.Contains(follower.Id) ?? false)), "Chi is not offered against a Follower");
        }
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, UnseenAssailant, 0, Zone.Hand);
            var tired = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Ninja");
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, "unseen-assailant-attack", card.Id, tired.Id), "a bowed Ninja may not perform (bowing him is the cost)");
        }
    }

    private static void TrapIsSprungScenario()
    {
        MatchEngine Build(int actor, bool recon, out CardInstance card, out CardInstance scout, out CardInstance target, out CardInstance follower)
        {
            var s = Table(Phase.Battle, actor);
            card = Add(s, TrapIsSprung, actor, Zone.Hand);
            scout = Kw(Add(s, actor == 0 ? "training-crane-samurai" : "training-lion-samurai", actor, Zone.Battlefield, province: 0), "Scout");
            target = Add(s, actor == 0 ? "training-lion-veteran" : "training-crane-veteran", 1 - actor, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", 1 - actor, Zone.Attached, province: 0, parent: target.Id);
            if (recon) s.Players[1 - actor].Provinces[0].Reconnaissance[actor] = true;
            return MatchEngine.FromTestState(s);
        }
        var none = Build(0, false, out var card0, out var scout0, out _, out _);
        Check(!CanUse(none, 0, "trap-sprung-bow", card0.Id, scout0.Id), "without Reconnaissance it is not playable");
        {
            // A Crane Clan player bows the target's whole unit.
            var e = Build(0, true, out var card, out var scout, out var target, out var follower);
            Accept(e, Offer(e, 0, "trap-sprung-bow", card.Id, target.Id, scout.Id));
            Check(e.State.Cards[target.Id].Bowed && e.State.Cards[follower.Id].Bowed, "a Crane Clan player bows the target and its unit");
        }
        {
            // Any other player bows the target only.
            var e = Build(1, true, out var card, out var scout, out var target, out var follower);
            Accept(e, Offer(e, 1, "trap-sprung-bow", card.Id, target.Id, scout.Id));
            Check(e.State.Cards[target.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "a Lion Clan player bows only the target");
        }
        {
            // The target may be a Follower alone (an enemy card): a Crane Clan player bows his unit too.
            var e = Build(0, true, out var card, out var scout, out var target, out var follower);
            Accept(e, Offer(e, 0, "trap-sprung-bow", card.Id, follower.Id, scout.Id));
            Check(e.State.Cards[follower.Id].Bowed && e.State.Cards[target.Id].Bowed, "targeting a Follower bows its unit as well");
        }
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, TrapIsSprung, 0, Zone.Hand);
            var tired = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Scout");
            Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            s.Players[1].Provinces[0].Reconnaissance[0] = true;
            Check(!CanUse(MatchEngine.FromTestState(s), 0, "trap-sprung-bow", card.Id, tired.Id), "a bowed Scout may not perform");
        }
    }

    private static void VeiledMenaceScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, VeiledMenace, 0, Zone.Hand);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var tired = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var victim = Add(s, "training-lion-veteran", 1, Zone.Home);
        victim.Dishonorable = true;
        var honorable = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "veiled-menace-loss", card.Id, tired.Id), "a bowed Courtier may not perform");
        var option = Enabled(e, 0, "veiled-menace-loss", card.Id, courtier.Id);
        Check(option.TargetIds!.Contains(victim.Id) && !option.TargetIds.Contains(honorable.Id), "only a dishonorable Personality is a target");
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 0, "veiled-menace-loss", card.Id, victim.Id, courtier.Id));
        Eq(Honor(e, 1), honor - 2, "his controller lost 2 Honor");
        Check(e.State.AssignmentWatches.Any(w => w.CardId == victim.Id && w.Kind == MatchEngine.WatchVeiledMenace), "the watch is set for the turn");

        // He is assigned as an attacker (his controller's turn): 1 more Honor.
        var attacking = Batch336Support.Edit(e, st => { st.ActivePlayerId = 1; st.Phase = Phase.AttackDeclaration; st.PriorityPlayerId = 1; });
        Accept(attacking, new("declareAttack", 1, attacking.Version));
        var before = Honor(attacking, 1);
        Accept(attacking, new("attack", 1, attacking.Version, Assignments: new Dictionary<string, int> { [victim.Id] = 0 }));
        Eq(Honor(attacking, 1), before - 1, "assigned to attack: his controller loses 1 Honor");
        Check(attacking.State.AssignmentWatches.Any(w => w.Kind == MatchEngine.WatchVeiledMenace), "the watch stays for each later assignment this turn");

        // Assigned as a defender (the other player attacks): 1 more Honor as well.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, VeiledMenace, 0, Zone.Hand);
        var courtier2 = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var attacker = Add(t, "training-crane-veteran", 0, Zone.Home);
        var defender = Add(t, "training-lion-veteran", 1, Zone.Home);
        defender.Dishonorable = true;
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 0, "veiled-menace-loss", card2.Id, defender.Id, courtier2.Id));
        var declared = Batch336Support.Edit(e2, st => { st.Phase = Phase.AttackDeclaration; st.PriorityPlayerId = 0; });
        Accept(declared, new("declareAttack", 0, declared.Version));
        Accept(declared, new("attack", 0, declared.Version, Assignments: new Dictionary<string, int> { [attacker.Id] = 0 }));
        var beforeDefense = Honor(declared, 1);
        var assign = declared.QueryActions(1).FirstOrDefault(a => a.Type == "assignDefense" && a.Enabled && a.Command!.Assignments!.ContainsKey(defender.Id));
        Check(assign is not null, "the Defender may assign his Personality");
        Accept(declared, assign!.Command!);
        Eq(Honor(declared, 1), beforeDefense - 1, "assigned to defend: his controller loses 1 Honor");
    }

    private static void UsefulConnectionsScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, UsefulConnections, 0, Zone.Hand);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier"); // Chi 3
        var tired = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var one = Add(s, "training-crane-samurai", 0, Zone.Home);
        var oneFollower = Add(s, "training-follower", 0, Zone.Attached, parent: one.Id);
        var two = Add(s, "training-lion-samurai", 1, Zone.Home);
        var three = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "useful-connections-cavalry", card.Id, tired.Id), "a bowed Courtier may not perform (bowing him is the cost)");
        var option = Enabled(e, 0, "useful-connections-cavalry", card.Id, courtier.Id);
        Eq(PersonalityChiOf(e, courtier.Id), 3, "the fixture Courtier has Chi 3");
        // Four targets exceed his Chi.
        Reject(e, option.Command! with { Selections = [Pick("targets", one.Id, two.Id, three.Id, courtier.Id)], TargetId = null });
        Accept(e, option.Command! with { Selections = [Pick("targets", one.Id, two.Id, three.Id)], TargetId = null });
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        foreach (var id in new[] { one.Id, two.Id, three.Id, oneFollower.Id })
            Check(e.State.Cards[id].GrantedKeywords.Any(k => k.Keyword == "Cavalry"), $"{id} has Cavalry");
        Check(!e.State.Cards[courtier.Id].GrantedKeywords.Any(k => k.Keyword == "Cavalry"), "an untargeted Personality does not");
        Check(e.State.Cards[one.Id].GrantedKeywords.Single(k => k.Keyword == "Cavalry").Duration == GrantDuration.Turn, "until the turn ends");
        // One target is enough.
        var t = Batch336Support.Edit(MatchEngine.FromTestState(s), st => { });
        Accept(t, Enabled(t, 0, "useful-connections-cavalry", card.Id, courtier.Id).Command! with { Selections = [Pick("targets", two.Id)], TargetId = null });
        Check(t.State.Cards[two.Id].GrantedKeywords.Any(k => k.Keyword == "Cavalry"), "one to Chi Personalities");
    }

    private static int PersonalityChiOf(MatchEngine e, string id) => e.PersonalityChiForTest(id);

    private static void WallOfHonorScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance performer)
        {
            var s = Table(Phase.Battle, 1); // player 1 attacks province 0 of player 0, the Defender, who uses Wall of Honor
            s.PriorityPlayerId = 0;
            card = Add(s, WallOfHonor, 0, Zone.Hand);
            performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        foreach (var raise in new[] { true, false })
        {
            var e = Build(out var card, out var performer);
            var ability = raise ? "wall-of-honor-raise" : "wall-of-honor-lower";
            var strength = e.GetView(0).Players[0].Provinces[0].Strength; var honor = Honor(e, 0);
            Accept(e, Enabled(e, 0, ability, card.Id, performer.Id).Command!);
            Eq(e.State.Cards[performer.Id].Zone, Zone.Home, "the performing Personality moved home as the cost");
            Eq(e.GetView(0).Players[0].Provinces[0].Strength, Math.Max(0, strength + (raise ? 2 : -2)), $"two enemy units: {(raise ? "+2" : "-2")} strength");
            Eq(Honor(e, 0), honor + 2, "2 Honor gained");
            Check(!CanUse(e, 0, "wall-of-honor-raise", card.Id) || e.State.Cards[card.Id].Zone == Zone.FateDiscard, "one use between raising and lowering");
        }
        {
            // Not opposed: no enemy unit at the battlefield.
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var card = Add(s, WallOfHonor, 0, Zone.Hand);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Check(!CanUse(MatchEngine.FromTestState(s), 0, "wall-of-honor-raise", card.Id, performer.Id), "an unopposed Personality may not perform");
        }
        {
            // A movement lock on the performer: he cannot pay the cost.
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var card = Add(s, WallOfHonor, 0, Zone.Hand);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            performer.MovementLocks.Add(new MovementLock("test", s.TurnNumber, MovementKinds.Home));
            Check(!CanUse(MatchEngine.FromTestState(s), 0, "wall-of-honor-raise", card.Id, performer.Id), "a Personality that may not move home cannot pay the cost");
        }
        {
            // "Your performing opposed Personality" prints no "unbowed": a bowed Personality performs it (R-035, issue #1228).
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var card = Add(s, WallOfHonor, 0, Zone.Hand);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, "wall-of-honor-raise", card.Id, performer.Id).Command!);
            Eq(e.State.Cards[performer.Id].Zone, Zone.Home, "the bowed performer moved home as the cost");
        }
    }

    private static void LawsStrengthScenario()
    {
        MatchEngine Build(string keyword, out CardInstance card, out CardInstance performer, out CardInstance target)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, LawsStrength, 0, Zone.Hand);
            performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); // no "unbowed" printed
            if (keyword != "") Kw(performer, keyword);
            target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var plain = Build("", out var card0, out var plainPerformer, out _);
        Check(!CanUse(plain, 0, "laws-strength-move", card0.Id, plainPerformer.Id), "a Personality that is neither a Courtier nor a Magistrate may not perform");
        foreach (var keyword in new[] { "Courtier", "Magistrate" })
        {
            var e = Build(keyword, out var card, out var performer, out var target);
            Accept(e, Offer(e, 0, "laws-strength-move", card.Id, target.Id, performer.Id));
            Eq(e.State.Cards[target.Id].Zone, Zone.Home, $"a {keyword} moved him home");
        }
        {
            // A Personality of the performer's own side is a legal target too ("a Personality").
            var s = Table(Phase.Battle, 0);
            var card = Add(s, LawsStrength, 0, Zone.Hand);
            var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(Enabled(e, 0, "laws-strength-move", card.Id, courtier.Id).TargetIds!.Contains(ally.Id), "any Personality is a target");
        }
        {
            // A movement that is prevented: he is destroyed instead.
            var e = Build("Courtier", out var card, out var performer, out var target);
            var locked = Batch336Support.Edit(e, st => st.Cards[target.Id].MovementLocks.Add(new MovementLock("test", st.TurnNumber, MovementKinds.Home)));
            Accept(locked, Offer(locked, 0, "laws-strength-move", card.Id, target.Id, performer.Id));
            Eq(locked.State.Cards[target.Id].Zone, Zone.Dead, "the move home did not happen: he is destroyed");
        }
    }

    private static void LightOfJusticeScenario()
    {
        MatchEngine Build(bool enemies, bool dishonorable, bool atHome, out CardInstance card, out CardInstance magistrate, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, LightOfJustice, 0, Zone.Hand);
            magistrate = Kw(atHome ? Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true) : Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Magistrate");
            follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: magistrate.Id, province: atHome ? null : 0);
            if (enemies)
            {
                var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
                enemy.Dishonorable = dishonorable;
            }
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(false, false, true, out var card, out var magistrate, out _);
            Check(!CanUse(e, 0, "light-of-justice-move", card.Id, magistrate.Id), "no enemy units at the current battlefield: not playable");
        }
        {
            var e = Build(true, false, false, out var card, out var magistrate, out _);
            Check(!CanUse(e, 0, "light-of-justice-move", card.Id, magistrate.Id), "the performing Magistrate must be at home");
        }
        {
            var e = Build(true, false, true, out var card, out var magistrate, out var follower);
            Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield), 0, "precondition: its player has no unit at the battlefield (an Absent move-to-battle action)");
            Accept(e, Enabled(e, 0, "light-of-justice-move", card.Id, magistrate.Id).Command!);
            Check(e.State.Cards[magistrate.Id].Zone == Zone.Battlefield && e.State.Cards[magistrate.Id].ProvinceIndex == 0, "he moved to the current battlefield");
            Check(!e.State.Cards[magistrate.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "his unit was straightened");
            Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "no dishonorable Personality opposes him: no additional action");
        }
        {
            var e = Build(true, true, true, out var card, out var magistrate, out _);
            Accept(e, Enabled(e, 0, "light-of-justice-move", card.Id, magistrate.Id).Command!);
            Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "a dishonorable Personality opposes him: an additional Battle action");
            Eq(e.State.PriorityPlayerId, 0, "and its player keeps priority to take it");
        }
    }

    private static void PowerOfOneScenario()
    {
        // Straighten: Battle/Open, any Commander (bowed too), his whole unit.
        var s = Table(Phase.Battle, 0);
        var card = Add(s, PowerOfOne, 0, Zone.Hand);
        var commander = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Commander");
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: commander.Id);
        var notCommander = Add(s, "training-crane-veteran", 0, Zone.Battlefield, bowed: true, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "power-one-straighten", card.Id, notCommander.Id), "only a Commander may perform");
        Accept(e, Enabled(e, 0, "power-one-straighten", card.Id, commander.Id).Command!);
        Check(!e.State.Cards[commander.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "his whole unit straightened");

        // The Open half, in the Action Phase, from home.
        var a = Table(Phase.Action, 0);
        var card2 = Add(a, PowerOfOne, 0, Zone.Hand);
        var homeCommander = Kw(Add(a, "training-crane-samurai", 0, Zone.Home, bowed: true), "Commander");
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Enabled(ae, 0, "power-one-straighten", card2.Id, homeCommander.Id).Command!);
        Check(!ae.State.Cards[homeCommander.Id].Bowed, "usable as an Open action");

        // Bow: the combined total Gold Cost of the targets fits the Commander's unit's.
        var b = Table(Phase.Battle, 0);
        var card3 = Add(b, PowerOfOne, 0, Zone.Hand);
        var leader = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Commander");
        SetCost(leader, 8);
        var cheapA = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetCost(cheapA, 3);
        var cheapAFollower = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: cheapA.Id); SetCost(cheapAFollower, 1);
        var cheapB = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); SetCost(cheapB, 4);
        var dear = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetCost(dear, 9);
        var be = MatchEngine.FromTestState(b);
        var option = Enabled(be, 0, "power-one-bow", card3.Id, leader.Id);
        var slot = option.Slots!.Single();
        Check(slot.CardIds.Contains(cheapA.Id) && slot.CardIds.Contains(cheapB.Id) && !slot.CardIds.Contains(dear.Id), "a unit dearer than his own is not a target");
        Reject(be, option.Command! with { Selections = [Pick("targets", cheapA.Id, cheapB.Id, dear.Id)], TargetId = null });
        // 3 + 1 (his Follower) + 4 = 8, exactly his unit's total: allowed.
        Accept(be, option.Command! with { Selections = [Pick("targets", cheapA.Id, cheapB.Id)], TargetId = null });
        Check(be.State.Cards[cheapA.Id].Bowed && be.State.Cards[cheapAFollower.Id].Bowed && be.State.Cards[cheapB.Id].Bowed, "both units bowed, Followers too");
        Check(!be.State.Cards[dear.Id].Bowed, "the third is untouched");

        // One more Gold and the pair is too dear.
        var c = Table(Phase.Battle, 0);
        var card4 = Add(c, PowerOfOne, 0, Zone.Hand);
        var leader2 = Kw(Add(c, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Commander");
        SetCost(leader2, 7);
        var x = Add(c, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetCost(x, 4);
        var y = Add(c, "training-lion-veteran", 1, Zone.Battlefield, province: 0); SetCost(y, 4);
        var ce = MatchEngine.FromTestState(c);
        var pair = Enabled(ce, 0, "power-one-bow", card4.Id, leader2.Id);
        Reject(ce, pair.Command! with { Selections = [Pick("targets", x.Id, y.Id)], TargetId = null });
        Accept(ce, pair.Command! with { Selections = [Pick("targets", x.Id)], TargetId = null });
        Check(ce.State.Cards[x.Id].Bowed, "a single unit that fits is bowed");
    }

    private static void PowerOfAWordScenario()
    {
        MatchEngine Build(out CardInstance law, out CardInstance word, out CardInstance courtier, out CardInstance actor)
        {
            var s = Table(Phase.Battle, 1);
            law = Add(s, LawsStrength, 1, Zone.Hand);
            actor = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Courtier");
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            word = Add(s, PowerOfAWord, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        {
            // The acting player chooses to lose 3 Honor: the action goes ahead.
            var e = Build(out var law, out var word, out var courtier, out var actor);
            var honor = Honor(e, 1);
            Accept(e, Offer(e, 1, "laws-strength-move", law.Id, courtier.Id, actor.Id));
            Accept(e, Enabled(e, 0, "power-word-reaction", word.Id).Command!);
            Eq(e.State.PendingChoice?.Type, "eitherOr", "the acting player is asked");
            Eq(e.State.PendingChoice!.PlayerId, 1, "the player whose action it is decides");
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "lose").Command!);
            DeclineWindows(e);
            Eq(Honor(e, 1), honor - 3, "he lost 3 Honor");
            Eq(e.State.Cards[courtier.Id].Zone, Zone.Home, "and the action went ahead: the Courtier moved home");
        }
        {
            // He does not: the action's effects are negated.
            var e = Build(out var law, out var word, out var courtier, out var actor);
            var honor = Honor(e, 1);
            Accept(e, Offer(e, 1, "laws-strength-move", law.Id, courtier.Id, actor.Id));
            Accept(e, Enabled(e, 0, "power-word-reaction", word.Id).Command!);
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "negate").Command!);
            DeclineWindows(e);
            Eq(Honor(e, 1), honor, "no Honor lost");
            Eq(e.State.Cards[courtier.Id].Zone, Zone.Battlefield, "the action's effects were negated: the Courtier stayed");
        }
        {
            // An Attack is held in its own window: the refusal negates the Attack.
            var s = Table(Phase.Battle, 1);
            var assailant = Add(s, UnseenAssailant, 1, Zone.Hand);
            var ninja = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Ninja");
            var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            SetForce(courtier, 1);
            var word = Add(s, PowerOfAWord, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "unseen-assailant-attack", assailant.Id, courtier.Id, ninja.Id));
            Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack is held for reactions");
            Accept(e, Enabled(e, 0, "power-word-reaction", word.Id).Command!);
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "negate").Command!);
            PassUntil(e, () => e.State.PendingChoice?.Type != "attackReaction");
            Eq(e.State.Cards[courtier.Id].Zone, Zone.Battlefield, "the negated Attack destroyed nothing");
        }
        {
            // Not a Courtier, and not a Battle action, gets no window.
            var s = Table(Phase.Battle, 1);
            var law = Add(s, LawsStrength, 1, Zone.Hand);
            var actor = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Courtier");
            var samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, PowerOfAWord, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "laws-strength-move", law.Id, samurai.Id, actor.Id));
            Check(e.State.PendingChoice?.Type != "beforeActionReaction", "only a Courtier's player may react");
        }
        {
            // The Battle action: one to three enemy dishonorable Personalities go home.
            var s = Table(Phase.Battle, 0);
            var word = Add(s, PowerOfAWord, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); a.Dishonorable = true;
            var b = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); b.Dishonorable = true;
            var honorable = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, "power-word-move", word.Id);
            var candidates = option.Slots!.Single().CardIds;
            Check(candidates.Contains(a.Id) && candidates.Contains(b.Id) && !candidates.Contains(honorable.Id), "only enemy dishonorable Personalities are targets");
            Accept(e, option.Command! with { Selections = [Pick("targets", a.Id, b.Id)], TargetId = null });
            Eq(e.State.Cards[a.Id].Zone, Zone.Home, "the first went home");
            Eq(e.State.Cards[b.Id].Zone, Zone.Home, "and the second");
            Eq(e.State.Cards[honorable.Id].Zone, Zone.Battlefield, "an honorable one stays");
        }
    }

    private static void SerpentsDeceptionScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, SerpentsDeception, 0, Zone.Hand);
        var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Monk"); // no "unbowed" printed
        SetForce(monk, 5);
        var weaker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(weaker, 3);
        var equal = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); SetForce(equal, 5);
        var strongerByFollower = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(strongerByFollower, 3);
        var extra = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: strongerByFollower.Id); SetForce(extra, 3);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "serpents-deception-move", card.Id, weaker.Id), "only a Monk may perform");
        var option = Enabled(e, 0, "serpents-deception-move", card.Id, monk.Id);
        Check(option.TargetIds!.Contains(weaker.Id) && !option.TargetIds.Contains(equal.Id) && !option.TargetIds.Contains(strongerByFollower.Id), "the target's unit must have LOWER total Force (his Followers count)");
        var forcesBefore = e.ArmyForcesForTest();
        Eq(forcesBefore.Attacker, 0, "the bowed Monk contributes nothing yet");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "serpents-deception-move", card.Id, weaker.Id, monk.Id));
        Eq(e.State.Cards[weaker.Id].Zone, Zone.Home, "the target moved home");
        Eq(Honor(e, 0), honor - 3, "3 Honor lost");
        Check(e.State.Cards[monk.Id].Bowed, "the Monk is still bowed");
        Eq(e.ArmyForcesForTest().Attacker, 5, "yet he contributes his Force to his army");
    }

    private static void WordsHaveStrengthScenario()
    {
        // After another player's Battle action: Tactical Assault destroys player 0's Courtier.
        MatchEngine Action(bool courtier, bool twoCopies, out CardInstance word, out CardInstance? second, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1);
            var assault = Add(s, TacticalAssault, 1, Zone.Hand);
            var tactician = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Tactician");
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (courtier) Kw(victim, "Courtier");
            word = Add(s, WordsHaveStrength, 0, Zone.Hand);
            second = twoCopies ? Add(s, WordsHaveStrength, 0, Zone.Hand) : null;
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "tactical-assault-destroy", assault.Id, victim.Id, tactician.Id));
            DeclineWindows(e);
            return e;
        }
        {
            var e = Action(true, true, out var word, out var second, out var victim);
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the Courtier was destroyed by the action");
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "its player may react");
            var honor = Honor(e, 1);
            var option = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "words-strength-action" && a.CardId == word.Id);
            Accept(e, option.Command! with { TargetPlayerId = 1 });
            Eq(Honor(e, 1), honor - 3, "the chosen player lost 3 Honor");
            // The second copy may not be played in reaction to the same resolution.
            var again = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.CardId == second!.Id && a.Command.AbilityId == "words-strength-action").ToArray();
            Check(again.All(a => !a.Enabled), "only one copy in reaction to any one resolution");
        }
        {
            var e = Action(true, false, out var word, out _, out var victim);
            var option = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "words-strength-action" && a.CardId == word.Id);
            var honor = Honor(e, 0);
            Accept(e, option.Command! with { TargetPlayerId = 0 });
            Eq(Honor(e, 0), honor - 3, "either player may be chosen: its own player loses the Honor");
        }
        {
            var e = Action(false, false, out _, out _, out var victim);
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "a Personality who is neither a Courtier nor a Magistrate was destroyed");
            Check(e.State.PendingChoice?.Type != "afterActionResolvedReaction", "no window: no Courtier or Magistrate was destroyed");
        }
        {
            // A Magistrate counts too, and one Personality of the OTHER player does not.
            var s = Table(Phase.Battle, 1);
            var assault = Add(s, TacticalAssault, 1, Zone.Hand);
            var tactician = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Tactician");
            var magistrate = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
            var word = Add(s, WordsHaveStrength, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "tactical-assault-destroy", assault.Id, magistrate.Id, tactician.Id));
            DeclineWindows(e);
            Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "words-strength-action"), "a Magistrate counts");
        }
        {
            // The battle's own resolution destroys player 0's Courtier (he loses to the Attacker), and the copy is played after it -- once.
            var s = Table(Phase.Battle, 1);
            var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
            var bystander = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var raider = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            SetForce(raider, 20);
            var word = Add(s, WordsHaveStrength, 0, Zone.Hand);
            var second = Add(s, WordsHaveStrength, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            PassUntil(e, () => e.State.PendingChoice?.Type == "afterResolutionReaction");
            Eq(e.State.Cards[courtier.Id].Zone, Zone.Dead, "the resolution destroyed the Courtier");
            var honor = Honor(e, 1);
            var option = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "words-strength-resolution" && a.CardId == word.Id);
            Accept(e, option.Command! with { TargetPlayerId = 1 });
            Eq(Honor(e, 1), honor - 3, "the chosen player lost 3 Honor");
            var again = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.CardId == second.Id && a.Command.AbilityId == "words-strength-resolution").ToArray();
            Check(again.All(a => !a.Enabled), "only one copy in reaction to this resolution");
        }
        {
            // A resolution that destroyed no Courtier or Magistrate of its player's offers nothing.
            var s = Table(Phase.Battle, 1);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var raider = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            SetForce(raider, 20);
            Add(s, WordsHaveStrength, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            PassUntil(e, () => e.State.Phase != Phase.Battle || e.State.PendingChoice?.Type == "afterResolutionReaction");
            Check(e.State.PendingChoice?.Type != "afterResolutionReaction", "no window: only a plain Samurai was destroyed");
        }
    }
}
