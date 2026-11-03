using L5R.Core;
using static RegressionAssertions;

// The Shadow's Embrace pass (issue #476), batch 4: the Strategies of strategy-wave-57, driven through QueryActions and the commands it offers. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires.
internal static class ShadowsEmbraceBatch4Tests
{
    private const string OvercomeAdversity = "oracle-l5r-9918", ParalyzingTouch = "oracle-l5r-9919", SpiritualManipulation = "oracle-l5r-9929", GameHasChanged = "oracle-l5r-9935",
        PreservingBeauty = "oracle-l5r-9921", RunningBattle = "oracle-l5r-9924", SerenityInAir = "oracle-l5r-9926",
        SocialGrace = "oracle-l5r-9928", SubtleGames = "oracle-l5r-9932", SuddenMovement = "oracle-l5r-9933", TacticalAssault = "oracle-l5r-9934", LessonOfEarth = "oracle-l5r-9936",
        TheologicalIndecision = "oracle-l5r-9937", ThuggishTechniques = "oracle-l5r-9938", UnstableGround = "oracle-l5r-9942", VoidTattoo = "oracle-l5r-9943", WatchersInTheDark = "oracle-l5r-9944";
    private const string SkippingThePuddle = "oracle-l5r-7285", TsunamiTattoo = "oracle-l5r-8793", EbisusHonesty = "oracle-l5r-2276";

    private static readonly string[] Wired = [OvercomeAdversity, ParalyzingTouch, PreservingBeauty, RunningBattle, SerenityInAir, SocialGrace, SpiritualManipulation, SubtleGames,
        SuddenMovement, TacticalAssault, GameHasChanged, LessonOfEarth, TheologicalIndecision, ThuggishTechniques, UnstableGround, VoidTattoo, WatchersInTheDark];

    // The Strategies of the batch left unwired (each with a blocker note in tools/content/build_card_inventory.py): never published.
    // Issue #503 (pass group P5): Two-Fold Virtue (oracle-l5r-9939) is wired and published in strategy-wave-98, so it left this list.
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

    public static void Run(TestRunner runner) => runner.Run("shadows-embrace-batch-4", [
        ("Published shape: strategy-wave-57 is exactly the seventeen wired Strategies, each with its scan's stats, keywords, text, ability ids and art, and the batch's three unwired Strategies are not published", PublishedShape),
        ("Paralyzing Touch: its Monk moves an enemy Personality to another battlefield and bows him; as a Focus Effect the next action the duel's winner performs is negated (its use spent), and only that one", ParalyzingTouchScenario),
        ("Spiritual Manipulation: the active player must declare an attack (declining is refused and not offered) and assign the target in his Maneuvers segment; a player who may not attack is not bound", SpiritualManipulationScenario),
        ("The Game Has Changed: its Commander targets Personalities of one player; as it resolves its player straightens their bowed attachments one by one, then transfers attachments among them, each at most once", GameHasChangedScenario),
        ("Overcome Adversity: an unbowed Paragon reduces an enemy Personality's Force by his own Force, and its player draws, may take an additional Battle action and loses 5 Honor; a non-Paragon may not perform", OvercomeAdversityScenario),
        ("Preserving Beauty: only with enemy units at the battlefield; as the turn ends its player gains 3 Honor if that province still stands, and nothing once it was destroyed", PreservingBeautyScenario),
        ("Running Battle: its Scout gives the enemy -4F; with Reconnaissance one of his attachments goes to its owner's hand (refused without); then the Scout may move home or stay", RunningBattleScenario),
        ("Serenity in Air: put into play from the hand it discards its player's other Kata; in play it straightens his Personality in a battle with no discard, and in the Action Phase only by discarding a card", SerenityInAirScenario),
        ("Social Grace: its Reaction negates the dishonoring of its player's targeted Courtier (Subtle Games still draws); its Open action gives the enemy a Force penalty of the Courtier's Personal Honor and 2 Honor each time he is assigned to attack", SocialGraceScenario),
        ("Subtle Games: its Courtier bows to dishonor a Personality and its player draws a card; a bowed Courtier may not perform", SubtleGamesScenario),
        ("Sudden Movement: two Personalities of one player at home and at the battlefield switch and both units straighten; a movement lock on either negates both movements; as a Focus Effect the duel's winner gets +3F", SuddenMovementScenario),
        ("Tactical Assault: its Tactician destroys an enemy Follower or an enemy Personality without attachments; an attached Item is; a Personality with an attachment is not a target", TacticalAssaultScenario),
        ("The Lesson of Earth: a Monk from home moves an enemy Personality to the battlefield and straightens his unit unless the Monk is Earth", LessonOfEarthScenario),
        ("Theological Indecision: its Limited action discards a Celestial; its Battle/Open action gives a Personality with 3 or higher Chi -2C, and one with less is not a target", TheologicalIndecisionScenario),
        ("Thuggish Techniques: a performer bows itself to bow a Personality without attachments and with lower Force, or any Follower; an equal-Force Personality is not a target", ThuggishTechniquesScenario),
        ("Unstable Ground: never offered as a plain Terrain play; its Scout bows to destroy an enemy card without attachments and the card enters play at the battlefield; refused where a Terrain already is", UnstableGroundScenario),
        ("Void Tattoo: a non-Void Tattooed performer discards a card, a Void one does not; the Tattoo returns to the hand, the card is removed from the game, and an additional Battle action is granted", VoidTattooScenario),
        ("Watchers in the Dark: after engaging its Scout (bowed or not) gives its player Reconnaissance and every card in the target's unit may not use its abilities this turn", WatchersInTheDarkScenario),
    ]);

    private static void PublishedShape()
    {
        Check(PublishedContent.StrategyWave57.Cards.Select(c => c.Id).SequenceEqual(Wired), "strategy-wave-57 publishes exactly the wired cards");
        void Shape(string id, string name, int focus, string keywords, params string[] abilities)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == 0 && card.Focus == focus, $"{name}: {card.Type} {card.Name} {card.GoldCost} {card.Focus}");
            Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
            Check(!card.Text.Contains("\n" + card.Text.Split('\n')[0] + "\n") && card.Text.Split('\n').Distinct().Count() == card.Text.Split('\n').Length, $"{name}'s text is printed once");
        }
        Shape(OvercomeAdversity, "Overcome Adversity", 2, "", "overcome-adversity");
        Shape(ParalyzingTouch, "Paralyzing Touch", 1, "Atemi,Kiho", "paralyzing-touch-focus", "paralyzing-touch-move");
        Shape(SpiritualManipulation, "Spiritual Manipulation", 3, "", "spiritual-manipulation-force");
        Shape(GameHasChanged, "The Game Has Changed", 2, "", "game-has-changed");
        Shape(PreservingBeauty, "Preserving Beauty", 2, "", "preserving-beauty");
        Shape(RunningBattle, "Running Battle", 3, "", "running-battle");
        Shape(SerenityInAir, "Serenity in Air", 3, "Air,Kata", "serenity-air-enter", "serenity-air-straighten", "serenity-air-straighten-open");
        Shape(SocialGrace, "Social Grace", 3, "Political", "social-grace-negate", "social-grace-penalty");
        Shape(SubtleGames, "Subtle Games", 1, "Political", "subtle-games-dishonor");
        Shape(SuddenMovement, "Sudden Movement", 4, "", "sudden-movement-focus", "sudden-movement-switch");
        Shape(TacticalAssault, "Tactical Assault", 2, "Tactical", "tactical-assault-destroy");
        Shape(LessonOfEarth, "The Lesson of Earth", 2, "Earth,Kiho", "lesson-of-earth-move");
        Shape(TheologicalIndecision, "Theological Indecision", 2, "", "theological-indecision-discard", "theological-indecision-chi");
        Shape(ThuggishTechniques, "Thuggish Techniques", 3, "", "thuggish-techniques-bow");
        Shape(UnstableGround, "Unstable Ground", 2, "Mountain,Terrain", "unstable-ground-destroy");
        Shape(VoidTattoo, "Void Tattoo", 3, "Tattoo,Void", "void-tattoo-return", "void-tattoo-return-void");
        Shape(WatchersInTheDark, "Watchers in the Dark", 1, "Recon", "watchers-dark-recon");
        Check(CardCatalog.Get(SuddenMovement).Text.StartsWith("As a Focus Effect: After this duel ends, give its winner +3F."), "Sudden Movement publishes its scan's wording");
        Check(CardCatalog.Get(VoidTattoo).Text.StartsWith("Battle: Choose your performing unbowed Tattooed Personality, target a Tattoo Strategy"), "Void Tattoo publishes its scan's wording");
        // issue #494: The Perfect Moment (8257) and Shinobi Assault (9927) are published now in strategy-wave-64 and left this list; issue #619: so did Regret (9923), in strategy-wave-105.
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-57.txt"));
        Check(listed.SequenceEqual(Wired), "the manifest lists exactly the wired cards");
        foreach (var id in Wired)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // Passes for whoever must act until `stop` holds (the battle phase only).
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

    private static void OvercomeAdversityScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, OvercomeAdversity, 0, Zone.Hand);
        var paragon = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon");
        var plain = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Deck(s, 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "overcome-adversity", card.Id, plain.Id), "a Personality who is not a Paragon may not perform");
        var honor = Honor(e, 0); var hand = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "overcome-adversity", card.Id, enemy.Id, paragon.Id));
        Eq(e.State.Cards[enemy.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), -3, "his Force is reduced by the Paragon's Force (3)");
        Eq(e.State.Players[0].Hand.Count, hand, "the Strategy left the hand and a card was drawn");
        Eq(Honor(e, 0), honor - 5, "its player lost 5 Honor");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");
        Eq(e.State.PriorityPlayerId, 0, "and its player keeps priority to take it");
        // A bowed Paragon may not perform.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, OvercomeAdversity, 0, Zone.Hand);
        var bowed = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Paragon");
        Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "overcome-adversity", card2.Id, bowed.Id), "a bowed Paragon may not perform");
    }

    private static void PreservingBeautyScenario()
    {
        MatchEngine Build(bool enemies, out CardInstance card)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            card = Add(s, PreservingBeauty, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (enemies) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var none = Build(false, out var lonely);
        Check(!CanUse(none, 0, "preserving-beauty", lonely.Id), "without enemy units at the battlefield it is not playable");
        var e = Build(true, out var card);
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "preserving-beauty", card.Id).Command!);
        Eq(e.State.PreservingBeautyChecks.Count, 1, "the before-this-turn-ends check is queued");
        Eq(Honor(e, 0), honor, "no Honor yet");
        var held = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = 1; st.CurrentBattleProvince = null; });
        Accept(held, new("endDynasty", 1, held.Version));
        Eq(Honor(held, 0), honor + 3, "the province still stood as the turn ended: 3 Honor");
        Eq(held.State.PreservingBeautyChecks.Count, 0, "the check is spent");
        var lost = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = 1; st.CurrentBattleProvince = null; st.Players[0].Provinces[0].Destroyed = true; });
        Accept(lost, new("endDynasty", 1, lost.Version));
        Eq(Honor(lost, 0), honor, "the battlefield's province was destroyed: nothing");
    }

    private static void RunningBattleScenario()
    {
        MatchEngine Build(bool recon, out CardInstance card, out CardInstance scout, out CardInstance enemy, out CardInstance item)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, RunningBattle, 0, Zone.Hand);
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0, bowed: true); // no "unbowed" printed
            enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            item = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: enemy.Id);
            if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(true, out var card, out var scout, out var enemy, out var item);
            var option = Enabled(e, 0, "running-battle", card.Id, scout.Id);
            Check(option.Slots!.Single(x => x.Slot == "attachment").CardIds.Contains(item.Id), "with Reconnaissance his attachment may be targeted");
            Accept(e, option.Command! with { Selections = [Pick("target", enemy.Id), Pick("attachment", item.Id)], TargetId = null });
            Eq(e.State.Cards[enemy.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), -4, "-4F");
            Check(e.State.Players[1].Hand.Contains(item.Id) && e.State.Cards[item.Id].Zone == Zone.Hand, "the attachment went to its owner's hand");
            Eq(e.State.PendingChoice?.Type, "effectChoice", "the Scout may move home: asked now");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "home").Command!);
            Eq(e.State.Cards[scout.Id].Zone, Zone.Home, "the Scout moved home");
        }
        {
            var e = Build(false, out var card, out var scout, out var enemy, out var item);
            var option = Enabled(e, 0, "running-battle", card.Id, scout.Id);
            Reject(e, option.Command! with { Selections = [Pick("target", enemy.Id), Pick("attachment", item.Id)], TargetId = null });
            Accept(e, option.Command! with { Selections = [Pick("target", enemy.Id)], TargetId = null });
            Eq(e.State.Cards[item.Id].AttachedTo, enemy.Id, "without Reconnaissance nothing leaves his unit");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "stay").Command!);
            Eq(e.State.Cards[scout.Id].Zone, Zone.Battlefield, "the Scout stayed");
        }
    }

    private static void SerenityInAirScenario()
    {
        // Put into play from the hand: the player's other Kata (Skipping the Puddle, in play) is discarded.
        var s = Table(Phase.Action, 0);
        var serenity = Add(s, SerenityInAir, 0, Zone.Hand);
        var puddle = Add(s, SkippingThePuddle, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "serenity-air-straighten-open", serenity.Id), "from the hand its straighten is not usable (it must be in play)");
        Accept(e, Enabled(e, 0, "serenity-air-enter", serenity.Id).Command!);
        Eq(e.State.Cards[serenity.Id].Zone, Zone.Home, "Serenity in Air is in play");
        Eq(e.State.Cards[puddle.Id].Zone, Zone.FateDiscard, "the other Kata was discarded");

        // In play, in a battle: straighten with no discard.
        var b = Table(Phase.Battle, 0);
        var kata = Add(b, SerenityInAir, 0, Zone.Home);
        var bowed = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spare = Add(b, "training-focus-strategy", 0, Zone.Hand);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, "serenity-air-straighten-open", kata.Id), "the Action Phase half is not offered in a battle");
        Accept(be, Offer(be, 0, "serenity-air-straighten", kata.Id, bowed.Id));
        Check(!be.State.Cards[bowed.Id].Bowed, "straightened");
        Check(be.State.Players[0].Hand.Contains(spare.Id), "no card discarded in a battle");
        Eq(be.State.Cards[kata.Id].Zone, Zone.Home, "the Kata stays in play");

        // In the Action Phase: a card must be discarded.
        var a = Table(Phase.Action, 0);
        var kata2 = Add(a, SerenityInAir, 0, Zone.Home);
        var tired = Add(a, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var discard = Add(a, "training-focus-strategy", 0, Zone.Hand);
        var ae = MatchEngine.FromTestState(a);
        var option = Enabled(ae, 0, "serenity-air-straighten-open", kata2.Id);
        Reject(ae, option.Command! with { Selections = [Pick("target", tired.Id)], TargetId = null });
        Accept(ae, option.Command! with { Selections = [Pick("target", tired.Id), Pick("discard", discard.Id)], TargetId = null });
        Check(!ae.State.Cards[tired.Id].Bowed, "straightened");
        Eq(ae.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the card was discarded as the cost");
        Check(!CanUse(ae, 0, "serenity-air-straighten", kata2.Id) && !CanUse(ae, 0, "serenity-air-straighten-open", kata2.Id), "one Battle/Open action, used once per turn");
    }

    private static void SocialGraceScenario()
    {
        // Player 1 plays Subtle Games against player 0's Courtier; player 0 answers with Social Grace.
        var s = Table(Phase.Action, 1);
        var subtle = Add(s, SubtleGames, 1, Zone.Hand);
        var lionCourtier = Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
        var craneCourtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var grace = Add(s, SocialGrace, 0, Zone.Hand);
        Deck(s, 1);
        var e = MatchEngine.FromTestState(s);
        var hand1 = e.State.Players[1].Hand.Count;
        Accept(e, Offer(e, 1, "subtle-games-dishonor", subtle.Id, craneCourtier.Id, lionCourtier.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeted Courtier's player may react");
        Accept(e, Enabled(e, 0, "social-grace-negate", grace.Id).Command!);
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(!e.State.Cards[craneCourtier.Id].Dishonorable, "his dishonoring was negated");
        Check(e.State.Cards[lionCourtier.Id].Bowed, "the Lion Courtier still bowed as the cost");
        Eq(e.State.Players[1].Hand.Count, hand1, "Subtle Games left the hand and its player still drew");

        // Not offered against an action that does not dishonor (Theological Indecision's -2C).
        var t = Table(Phase.Action, 1);
        var indecision = Add(t, TheologicalIndecision, 1, Zone.Hand);
        var courtier = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier");
        Add(t, SocialGrace, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 1, "theological-indecision-chi", indecision.Id, courtier.Id));
        Check(te.State.PendingChoice?.Type != "beforeActionReaction", "no window opens for Social Grace against an action that does not dishonor");

        // The Open action on player 1's turn: -3F (the Courtier's Personal Honor), then 2 Honor when the target is assigned to attack.
        var o = Table(Phase.Action, 1); o.PriorityPlayerId = 0;
        var grace2 = Add(o, SocialGrace, 0, Zone.Hand);
        var mine = Kw(Add(o, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var attacker = Add(o, "training-lion-veteran", 1, Zone.Home);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 0, "social-grace-penalty", grace2.Id, attacker.Id, mine.Id));
        Eq(oe.State.Cards[attacker.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), -3, "a Force penalty equal to the Courtier's Personal Honor");
        var honor = Honor(oe, 0);
        var declared = Batch336Support.Edit(oe, st => { st.Phase = Phase.AttackDeclaration; st.PriorityPlayerId = 1; });
        Accept(declared, new("declareAttack", 1, declared.Version));
        Accept(declared, new("attack", 1, declared.Version, Assignments: new Dictionary<string, int> { [attacker.Id] = 0 }));
        Eq(Honor(declared, 0), honor + 2, "he was assigned to attack: 2 Honor");
        Check(declared.State.AssignmentWatches.Any(w => w.Kind == MatchEngine.WatchSocialGrace), "the watch stays for each later assignment this turn");
    }

    private static void SubtleGamesScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, SubtleGames, 0, Zone.Hand);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var tired = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        Deck(s, 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "subtle-games-dishonor", card.Id, tired.Id), "a bowed Courtier may not perform (bowing him is the cost)");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "subtle-games-dishonor", card.Id, enemy.Id, courtier.Id));
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed");
        Check(e.State.Cards[enemy.Id].Dishonorable, "the target is dishonored");
        Eq(e.State.Players[0].Hand.Count, hand, "the card was played and one drawn");
    }

    private static void SuddenMovementScenario()
    {
        MatchEngine Build(bool locked, out CardInstance card, out CardInstance atHome, out CardInstance atBattle)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, SuddenMovement, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            atHome = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
            atBattle = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0, bowed: true);
            if (locked) atHome.MovementLocks.Add(new MovementLock("test", s.TurnNumber, MovementKinds.ToBattlefield));
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(false, out var card, out var atHome, out var atBattle);
            var option = Enabled(e, 0, "sudden-movement-switch", card.Id);
            Check(option.Slots!.Single(x => x.Slot == "first").CardIds.Contains(atHome.Id), "a Personality at home may be switched");
            Reject(e, option.Command! with { Selections = [Pick("first", atBattle.Id), Pick("second", e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Id)], TargetId = null });
            Accept(e, option.Command! with { Selections = [Pick("first", atHome.Id), Pick("second", atBattle.Id)], TargetId = null });
            Check(e.State.Cards[atHome.Id].Zone == Zone.Battlefield && e.State.Cards[atHome.Id].ProvinceIndex == 0, "the one at home is now at the battlefield");
            Eq(e.State.Cards[atBattle.Id].Zone, Zone.Home, "the one at the battlefield is now at home");
            Check(!e.State.Cards[atHome.Id].Bowed && !e.State.Cards[atBattle.Id].Bowed, "both moved, so both units straightened");
        }
        {
            var e = Build(true, out var card, out var atHome, out var atBattle);
            Accept(e, Enabled(e, 0, "sudden-movement-switch", card.Id).Command! with { Selections = [Pick("first", atHome.Id), Pick("second", atBattle.Id)], TargetId = null });
            Eq(e.State.Cards[atHome.Id].Zone, Zone.Home, "his movement is negated");
            Eq(e.State.Cards[atBattle.Id].Zone, Zone.Battlefield, "so the other's is too: a switch is both movements or neither");
            Check(e.State.Cards[atHome.Id].Bowed && e.State.Cards[atBattle.Id].Bowed, "nothing moved, nothing straightened");
        }
        // The Focus Effect: player 0's Personality wins a Steel on Steel duel with Sudden Movement focused, and gets +3F.
        var s3 = Table(Phase.Battle, 0);
        var mine = Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        mine.Modifiers.Add(new("test-source", "test", "Chi", 5));
        var theirs = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duelCard = Add(s3, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s3, SuddenMovement, 0, Zone.Hand);
        Deck(s3, 1);
        var e3 = MatchEngine.FromTestState(s3);
        var forceBefore = e3.State.Cards[mine.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);
        Accept(e3, Offer(e3, 0, "steel-on-steel-duel", duelCard.Id, theirs.Id, mine.Id));
        Accept(e3, new GameCommand("focus", 1, e3.Version, Choice: "deck"));
        Accept(e3, new GameCommand("focus", 0, e3.Version, CardId: focused.Id));
        Accept(e3, new GameCommand("strike", 1, e3.Version));
        Check(e3.State.Cards[theirs.Id].Zone == Zone.Dead, "player 0's Personality won the duel");
        Eq(e3.State.Cards[mine.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), forceBefore + 3, "the winner has +3F");
    }

    private static void TacticalAssaultScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, TacticalAssault, 0, Zone.Hand);
        var tactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician");
        var armed = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "tactical-assault-destroy", card.Id, tactician.Id);
        Check(option.TargetIds!.Contains(follower.Id) && option.TargetIds.Contains(bare.Id) && !option.TargetIds.Contains(armed.Id), "a Follower or a bare Personality, never one with an attachment");
        Accept(e, Offer(e, 0, "tactical-assault-destroy", card.Id, bare.Id, tactician.Id));
        Eq(e.State.Cards[bare.Id].Zone, Zone.Dead, "destroyed");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, "tactical-assault-destroy", card.Id, follower.Id, tactician.Id));
        Eq(f.State.Cards[follower.Id].Zone, Zone.FateDiscard, "the Follower destroyed");
        Eq(f.State.Cards[armed.Id].Zone, Zone.Battlefield, "its Personality stays");
        // An attachment has no attachments of its own, so an attached Item (Blade of Perfection, reported untargetable) is a target too.
        var blade = Add(s, "oracle-l5r-1010", 1, Zone.Attached, province: 0, parent: armed.Id);
        var g = MatchEngine.FromTestState(s);
        Check(Enabled(g, 0, "tactical-assault-destroy", card.Id, tactician.Id).TargetIds!.Contains(blade.Id), "an attached Item is a target");
        Accept(g, Offer(g, 0, "tactical-assault-destroy", card.Id, blade.Id, tactician.Id));
        Check(g.State.Cards[blade.Id].Zone != Zone.Attached, "the Item is destroyed");
        Eq(g.State.Cards[armed.Id].Zone, Zone.Battlefield, "its Personality stays");
    }

    private static void LessonOfEarthScenario()
    {
        foreach (var earth in new[] { false, true })
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, LessonOfEarth, 0, Zone.Hand);
            var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Monk");
            if (earth) Kw(monk, "Earth");
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
            Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield), 0, "precondition: its player has no unit at the battlefield (an Absent move-to-battle action)");
            Accept(e, Offer(e, 0, "lesson-of-earth-move", card.Id, enemy.Id, monk.Id));
            Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield && e.State.Cards[enemy.Id].ProvinceIndex == 0, "he moved to the current battlefield");
            Eq(e.State.Cards[enemy.Id].Bowed, earth, earth ? "an Earth Monk does not straighten him" : "a Monk who is not Earth straightens his unit");
        }
    }

    private static void TheologicalIndecisionScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, TheologicalIndecision, 0, Zone.Hand);
        var celestial = Add(s, EbisusHonesty, 1, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "theological-indecision-discard", card.Id, celestial.Id));
        Eq(e.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded");

        var t = Table(Phase.Action, 0);
        var card2 = Add(t, TheologicalIndecision, 0, Zone.Hand);
        var three = Add(t, "training-lion-samurai", 1, Zone.Home); // 3C
        var two = Add(t, "training-ancestor", 1, Zone.Home); // 2C
        var te = MatchEngine.FromTestState(t);
        var option = Enabled(te, 0, "theological-indecision-chi", card2.Id);
        Check(option.TargetIds!.Contains(three.Id) && !option.TargetIds.Contains(two.Id), "only a Personality with 3 or higher Chi is a target");
        Accept(te, Offer(te, 0, "theological-indecision-chi", card2.Id, three.Id));
        Eq(te.State.Cards[three.Id].Modifiers.Where(m => m.Stat == "Chi").Sum(m => m.Amount), -2, "-2C");
    }

    private static void ThuggishTechniquesScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, ThuggishTechniques, 0, Zone.Hand);
        var thug = Add(s, "training-crane-samurai", 0, Zone.Home); // 3F
        var weaker = Add(s, "training-lion-courtier", 1, Zone.Home); // 2F, no attachments
        var equal = Add(s, "training-crane-samurai", 1, Zone.Home); // 3F
        var host = Add(s, "training-lion-veteran", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "thuggish-techniques-bow", card.Id, thug.Id);
        Check(option.TargetIds!.Contains(weaker.Id) && option.TargetIds.Contains(follower.Id), "a weaker bare Personality, or any Follower");
        Check(!option.TargetIds.Contains(equal.Id) && !option.TargetIds.Contains(host.Id), "not one with equal Force, nor one with an attachment");
        Accept(e, Offer(e, 0, "thuggish-techniques-bow", card.Id, weaker.Id, thug.Id));
        Check(e.State.Cards[thug.Id].Bowed && e.State.Cards[weaker.Id].Bowed, "the performer bowed as the cost, and the target bowed");

        // A Follower may perform, bowing itself: it bows an enemy Follower.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, ThuggishTechniques, 0, Zone.Hand);
        var mineHost = Add(t, "training-crane-samurai", 0, Zone.Home);
        var myFollower = Add(t, "training-follower", 0, Zone.Attached, parent: mineHost.Id);
        var theirHost = Add(t, "training-lion-veteran", 1, Zone.Home);
        var theirFollower = Add(t, "training-follower", 1, Zone.Attached, parent: theirHost.Id);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, "thuggish-techniques-bow", card2.Id, theirFollower.Id, myFollower.Id));
        Check(te.State.Cards[myFollower.Id].Bowed && te.State.Cards[theirFollower.Id].Bowed && !te.State.Cards[mineHost.Id].Bowed, "the Follower performed and bowed; the enemy Follower bowed");
    }

    private static void UnstableGroundScenario()
    {
        MatchState Build(bool terrain, out CardInstance card, out CardInstance scout, out CardInstance bare)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, UnstableGround, 0, Zone.Hand);
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (terrain) Add(s, "oracle-l5r-7001", 1, Zone.Battlefield, province: 0); // Shiotome Plains, already in play there
            return s;
        }
        var e = MatchEngine.FromTestState(Build(false, out var card, out var scout, out var bare));
        Check(!e.QueryActions(0).Any(a => a.Type == "playStrategy" && a.CardId == card.Id), "not offered through the generic Terrain play");
        Reject(e, new GameCommand("playStrategy", 0, e.Version, card.Id));
        Accept(e, Offer(e, 0, "unstable-ground-destroy", card.Id, bare.Id, scout.Id));
        Check(e.State.Cards[scout.Id].Bowed, "the Scout bowed as the cost");
        Eq(e.State.Cards[bare.Id].Zone, Zone.Dead, "the enemy card without attachments was destroyed");
        Check(e.State.Cards[card.Id].Zone == Zone.Battlefield && e.State.Cards[card.Id].ProvinceIndex == 0, "Unstable Ground is in play at the battlefield");
        Check(!e.State.Players[0].Hand.Contains(card.Id), "and out of the hand");

        var blocked = MatchEngine.FromTestState(Build(true, out var card2, out var scout2, out var bare2));
        Check(!CanUse(blocked, 0, "unstable-ground-destroy", card2.Id, scout2.Id), "a Terrain is already in play there");
        Reject(blocked, Batch336Support.Use(blocked, 0, "unstable-ground-destroy", card2.Id, bare2.Id, scout2.Id));
    }

    private static void VoidTattooScenario()
    {
        MatchEngine Build(bool isVoid, out CardInstance card, out CardInstance tattooed, out CardInstance tattoo, out CardInstance spare)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, VoidTattoo, 0, Zone.Hand);
            tattooed = Add(s, "training-tattooed", 0, Zone.Battlefield, province: 0);
            if (isVoid) Kw(tattooed, "Void");
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            tattoo = Add(s, TsunamiTattoo, 0, Zone.FateDiscard);
            spare = Add(s, "training-focus-strategy", 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(false, out var card, out var tattooed, out var tattoo, out var spare);
            Check(!CanUse(e, 0, "void-tattoo-return-void", card.Id, tattooed.Id), "the no-discard entry needs a Void Personality");
            var option = Enabled(e, 0, "void-tattoo-return", card.Id, tattooed.Id);
            Check(!option.Slots!.Single(x => x.Slot == "discard").CardIds.Contains(card.Id), "Void Tattoo is not the card it discards");
            Accept(e, option.Command! with { Selections = [Pick("target", tattoo.Id), Pick("discard", spare.Id)], TargetId = null });
            Check(e.State.Players[0].Hand.Contains(tattoo.Id), "the Tattoo is in the hand");
            Eq(e.State.Cards[spare.Id].Zone, Zone.FateDiscard, "a card was discarded (not Void)");
            Eq(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "Void Tattoo was removed from the game");
            Check(!e.State.Players[0].Hand.Contains(card.Id), "and left the hand");
            Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");
        }
        {
            var e = Build(true, out var card, out var tattooed, out var tattoo, out var spare);
            Check(!CanUse(e, 0, "void-tattoo-return", card.Id, tattooed.Id), "a Void Personality discards nothing");
            Accept(e, Offer(e, 0, "void-tattoo-return-void", card.Id, tattoo.Id, tattooed.Id));
            Check(e.State.Players[0].Hand.Contains(tattoo.Id) && e.State.Players[0].Hand.Contains(spare.Id), "the Tattoo returned and no card was discarded");
            Eq(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "Void Tattoo was removed from the game");
        }
    }

    private static void WatchersInTheDarkScenario()
    {
        var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); // not in the target's unit
        var card = Add(s, WatchersInTheDark, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        Accept(e, Offer(e, 0, "watchers-dark-recon", card.Id, bower.Id, scout.Id));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "its player has Reconnaissance there");
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(e.State.CurrentBattleProvince == 0 && e.State.PriorityPlayerId == 1, "the battle is under way, the Defender first");
        Check(CanUse(e, 1, "training-bow-at-battle", other.Id), "a card outside the target's unit still may");
        Check(!CanUse(e, 1, "training-bow-at-battle", bower.Id), "abilities on cards in the target's unit may not be used");
        Reject(e, Batch336Support.Use(e, 1, "training-bow-at-battle", bower.Id, scout.Id));
    }

    private static void ParalyzingTouchScenario()
    {
        // The Battle action: the enemy Personality moves to the other unresolved battlefield and bows.
        var s = Table(Phase.Battle, 0); s.UnresolvedBattlefields = [0, 1];
        var card = Add(s, ParalyzingTouch, 0, Zone.Hand);
        var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Monk");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var command = Offer(e, 0, "paralyzing-touch-move", card.Id, enemy.Id, monk.Id);
        Eq(command.Province, (int?)1, "the other unresolved battlefield is the destination");
        Accept(e, command);
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield && e.State.Cards[enemy.Id].ProvinceIndex == 1, "he moved to a different battlefield");
        Check(e.State.Cards[enemy.Id].Bowed, "and bowed, since he moved");

        // The Focus Effect: player 0 challenges with Steel on Steel and focuses Paralyzing Touch; player 1's bower wins, so the next action it performs is negated.
        var d = Table(Phase.Battle, 0);
        var mine = Add(d, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var spare = Add(d, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var winner = Add(d, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        winner.Modifiers.Add(new("test-source", "test", "Chi", 10));
        var otherBower = Add(d, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var duelCard = Add(d, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(d, ParalyzingTouch, 0, Zone.Hand);
        Deck(d, 1);
        var de = MatchEngine.FromTestState(d);
        Accept(de, Offer(de, 0, "steel-on-steel-duel", duelCard.Id, winner.Id, mine.Id));
        Accept(de, new GameCommand("focus", 1, de.Version, Choice: "deck"));
        Accept(de, new GameCommand("focus", 0, de.Version, CardId: focused.Id));
        Accept(de, new GameCommand("strike", 1, de.Version));
        Eq(de.State.Cards[mine.Id].Zone, Zone.Dead, "player 1's Personality won the duel");
        Eq(de.State.ParalyzingTouchWatches.Count, 1, "the winner's next action is watched");
        Eq(de.State.PriorityPlayerId, 1, "player 1 acts next");
        Accept(de, Offer(de, 1, "training-bow-at-battle", winner.Id, spare.Id));
        Check(!de.State.Cards[spare.Id].Bowed, "the winner's next action was negated");
        Check(de.GetView(0).Events.Any(ev => ev.Type == "actionNegated" && ev.Message.Contains("Paralyzing Touch")), "and the negation is announced");
        Eq(de.State.ParalyzingTouchWatches.Count, 0, "the watch is spent");
        Check(!CanUse(de, 1, "training-bow-at-battle", winner.Id), "the negated action's use was still spent");
        // Only that one action: another Personality's action is untouched.
        var later = Batch336Support.Edit(de, st => st.PriorityPlayerId = 1);
        Accept(later, Offer(later, 1, "training-bow-at-battle", otherBower.Id, spare.Id));
        Check(later.State.Cards[spare.Id].Bowed, "another Personality's action resolves as usual");
    }

    private static void SpiritualManipulationScenario()
    {
        MatchEngine Build(bool mayNotAttack, out CardInstance target, out CardInstance other)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
            var card = Add(s, SpiritualManipulation, 0, Zone.Hand);
            var spirit = Kw(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true), "Spirit"); // no "unbowed" printed
            target = Add(s, "training-lion-samurai", 1, Zone.Home);
            other = Add(s, "training-lion-veteran", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "spiritual-manipulation-force", card.Id, target.Id, spirit.Id));
            Eq(e.State.ForcedAttacks.Count, 1, "the forced attack is owed");
            return Batch336Support.Edit(e, st => { st.Phase = Phase.AttackDeclaration; st.PriorityPlayerId = 1; st.Players[1].MayNotAttackThisTurn = mayNotAttack; });
        }
        {
            var e = Build(false, out var target, out var other);
            Check(e.QueryActions(1).Any(a => a.Type == "pass" && !a.Enabled && (a.Reason ?? "").Contains("must declare an attack")), "declining is offered disabled, with the reason");
            Reject(e, new GameCommand("pass", 1, e.Version));
            Accept(e, new GameCommand("declareAttack", 1, e.Version));
            Eq(e.State.Phase, Phase.InfantryDeclaration, "the attack was declared");
            Check(e.QueryActions(1).Any(a => a.Type == "pass" && !a.Enabled), "assigning no Infantry is refused while the target can be assigned");
            Reject(e, new GameCommand("pass", 1, e.Version));
            Reject(e, new GameCommand("attack", 1, e.Version, Assignments: new Dictionary<string, int> { [other.Id] = 0 }));
            Accept(e, new GameCommand("attack", 1, e.Version, Assignments: new Dictionary<string, int> { [target.Id] = 0 }));
            Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "the target was assigned to attack");
        }
        {
            var e = Build(true, out _, out _);
            Accept(e, new GameCommand("pass", 1, e.Version));
            Eq(e.State.Phase, Phase.Dynasty, "a player who may not declare an attack declines as usual");
        }
    }

    private static void GameHasChangedScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, GameHasChanged, 0, Zone.Hand);
        var commander = Kw(Add(s, "training-crane-veteran", 0, Zone.Home), "Commander");
        var first = Add(s, "training-crane-samurai", 0, Zone.Home);
        var second = Add(s, "training-crane-courtier", 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Attached, bowed: true, parent: first.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "game-has-changed", card.Id, commander.Id);
        Reject(e, option.Command! with { Selections = [Pick("targets", first.Id, enemy.Id)], TargetId = null });
        Accept(e, option.Command! with { Selections = [Pick("targets", first.Id, second.Id)], TargetId = null });
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the straighten step is asked as it resolves");
        var straighten = e.QueryActions(0).Where(a => a.Type == "chooseEffect").ToArray();
        Check(straighten.Length == 2 && straighten.Any(a => a.Command!.Choice!.StartsWith("tghc-straighten:")), "straighten the bowed attachment, or move on");
        Accept(e, straighten.Single(a => a.Command!.Choice!.StartsWith("tghc-straighten:")).Command!);
        Check(!e.State.Cards[item.Id].Bowed, "the attachment straightened");
        var transfer = e.QueryActions(0).Where(a => a.Type == "chooseEffect").ToArray();
        var move = transfer.Where(a => a.Command!.Choice!.StartsWith("tghc-transfer:")).ToArray();
        Check(move.Length == 1 && move[0].Label.Contains(CardCatalog.Get("training-crane-courtier").Name), "then the transfer step: to the other target, or finish");
        Accept(e, move[0].Command!);
        Eq(e.State.Cards[item.Id].AttachedTo, second.Id, "the attachment was transferred");
        Check(e.State.PendingChoice is null, "each attachment transfers at most once: nothing is left to choose, the action ends");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "The Game Has Changed was played");
    }
}

