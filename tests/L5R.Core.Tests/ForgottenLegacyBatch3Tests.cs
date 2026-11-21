using L5R.Core;
using static RegressionAssertions;

// Forgotten Legacy pass, batch 3 (issue #467): the ten cards of strategy-wave-54, driven through QueryActions and the commands it offers. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), a fixture Personality substituted in as each card needs, granted whichever keyword
// the card's own text requires.
internal static class ForgottenLegacyBatch3Tests
{
    private const string GameOfDice = "oracle-l5r-25", YojimbosDuty = "oracle-l5r-101", AccidentalConfession = "oracle-l5r-109", BrokenAlliance = "oracle-l5r-1150",
        CastAside = "oracle-l5r-1225", Consideration = "oracle-l5r-1461", CreatingOrder = "oracle-l5r-1557", DuelOfHaiku = "oracle-l5r-2245", EntrenchedPosition = "oracle-l5r-2355", FeintAndStrike = "oracle-l5r-2506";

    private static readonly string[] Wave = [GameOfDice, YojimbosDuty, AccidentalConfession, BrokenAlliance, CastAside, Consideration, CreatingOrder, DuelOfHaiku, EntrenchedPosition, FeintAndStrike];

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

    public static void Run(TestRunner runner) => runner.Run("forgotten-legacy-batch-3", [
        ("Published shape: strategy-wave-54 is exactly the ten wired cards, each with its scan's stats, keywords and text, its ability ids, its art and its manifest line", PublishedShape),
        ("A Game of Dice: costs 3 Gold (the cost printed inside its Limited action is its Gold Cost) and makes this player's next End Phase draw two more cards", GameOfDiceDraws),
        ("A Game of Dice: its 3 Gold is modified by the same effects as a centrally priced Strategy (Yoritomo Kanaye - exp, as against Mysterious Deaths)", GameOfDiceCostTerms),
        ("A Yojimbo's Duty: its Yojimbo is destroyed as the cost and a discarded Courtier or Shugenja enters play at home for his recruit cost less the Yojimbo's PRINTED Gold Cost plus 1; separately, its performer straightens and an additional Action-phase action is granted", YojimbosDutyScenario),
        ("Accidental Confession: an unbowed Courtier and the Imperial Favor (discarded) dishonor a target Personality and lock his assignment and movement to battlefields for the turn -- refused without the Favor", AccidentalConfessionScenario),
        ("Broken Alliance: destroys a target attachment for its Gold Cost minus two; separately, moves its performing Personality from home to an opposed current battlefield and straightens him -- refused when the battlefield is unopposed", BrokenAllianceScenario),
        ("Cast Aside the Weak: straightens its performing Samurai's whole unit; separately, bows a target enemy card without attachments and destroys it only when the Samurai is a Paragon", CastAsideScenario),
        ("Consideration: a Tactician gives another of his controller's Personalities +3F, and the next action of another player's that targets him this turn is negated (its costs still paid); the shield is then spent", ConsiderationScenario),
        ("Creating Order: negates the Force bonuses and penalties from actions on one or two Personalities (a trait's modifier is kept) and refuses new ones this turn; or straightens both their units -- one shared use", CreatingOrderScenario),
        ("Creating Order hides the Force modifiers from actions without deleting them: a permanent one counts again from the next turn with its source and permanence, a turn's one whose duration ended does not, and the hidden state survives a save", CreatingOrderSuppresses),
        ("Duel of Haiku: an Open duel on Personal Honor; the winner's controller gains 1 Honor and the loser is bowed and dishonored", DuelOfHaikuScenario),
        ("Entrenched Position: the Defender negates a Battle action the Attacker announces while he still holds the first opportunity he claimed (the action is still taken); once the Attacker has ceded priority no window opens; its Battle half bows an enemy card without attachments", EntrenchedPositionScenario),
        ("Feint & Strike: its Commander with a Follower destroys an attachment in the target's unit and moves him home (no attachment slot without a Follower); as a Focus Effect, the duel winner's controller must discard a card", FeintAndStrikeScenario),
    ]);

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        var published = PublishedContent.StrategyWave54.Cards.Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();
        Check(published.SequenceEqual(Wave.Order(StringComparer.Ordinal)), "exactly the wired cards are published: " + string.Join(",", published));
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-54.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.Order(StringComparer.Ordinal).SequenceEqual(Wave.Order(StringComparer.Ordinal)), "the manifest pins exactly the wired cards");
        var expected = new Dictionary<string, string[]>
        {
            [GameOfDice] = ["game-of-dice-draw"],
            [YojimbosDuty] = ["yojimbos-duty-recruit", "yojimbos-duty-straighten"],
            [AccidentalConfession] = ["accidental-confession-dishonor"],
            [BrokenAlliance] = ["broken-alliance-destroy", "broken-alliance-move"],
            [CastAside] = ["cast-aside-straighten", "cast-aside-bow"],
            [Consideration] = ["consideration-boost"],
            [CreatingOrder] = ["creating-order-negate", "creating-order-straighten"],
            [DuelOfHaiku] = ["duel-of-haiku"],
            [EntrenchedPosition] = ["entrenched-negate", "entrenched-bow"],
            [FeintAndStrike] = ["feint-strike-focus", "feint-strike-move"],
        };
        foreach (var id in Wave)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.GoldCost == (card.Id == GameOfDice ? 3 : 0), $"{card.Name}: a Strategy with the scan's Gold Cost 0 (A Game of Dice prints its 3 inside the Limited action, which is its Gold Cost)");
            Check(card.Abilities!.SequenceEqual(expected[id]), $"{card.Name}: ability ids {string.Join(",", card.Abilities!)}");
            Check(card.Abilities!.All(AbilityCatalog.All.ContainsKey), $"{card.Name}: every ability id is defined");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Check(CardCatalog.Get(GameOfDice).Text == "Limited: Draw two additional cards during your next End Phase.", "A Game of Dice prints the scan's text");
        Check(CardCatalog.Get(AccidentalConfession).Keywords!.SequenceEqual(["Imperial", "Political"]), "Accidental Confession carries the scan's keywords (no Favor)");
        Check(CardCatalog.Get(CreatingOrder).Keywords!.Contains("Unique") && CardCatalog.Get(DuelOfHaiku).Keywords!.Contains("Political"), "Creating Order is Unique, Duel of Haiku Political");
        Eq(CardCatalog.Get(FeintAndStrike).Focus, 2, "Feint & Strike's Focus Value");
    }

    private static void GameOfDiceDraws()
    {
        var s = Table(Phase.Action, 0);
        var dice = Add(s, GameOfDice, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var gold = Enabled(e, 0, "game-of-dice-draw", dice.Id).Gold;
        Eq(gold?.Cost, 3, "it costs 3 Gold, the amount printed inside its Limited action");
        Accept(e, Enabled(e, 0, "game-of-dice-draw", dice.Id).Command!);
        Eq(e.State.ExtraEndPhaseDraws[0], 2, "two extra End Phase draws are owed");
        var handBefore = e.State.Players[0].Hand.Count;
        Batch336Support.RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase is Phase.Straighten or Phase.Events or Phase.Action);
        Eq(e.State.Players[0].Hand.Count, handBefore + 3, "the End Phase drew three: the usual one and the two additional ones");
        Eq(e.State.ExtraEndPhaseDraws[0], 0, "and owes no more");
    }

    private static void GameOfDiceCostTerms()
    {
        // The cost printed inside a Limited action is the card's Gold Cost, so an effect that changes what Strategies cost changes it exactly as it changes a card with a central cost.
        const string MysteriousDeaths = "oracle-l5r-5468", Kanaye = "oracle-l5r-10470"; // Kanaye - exp: "Actions on your Strategies cost 1 less Gold."
        int Quote(MatchState s, string card, string ability)
        {
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, ability, card);
            return e.QuoteAbilityGold(option.Command!)!.Cost;
        }
        var s = Table(Phase.Action, 0);
        var dice = Add(s, GameOfDice, 0, Zone.Hand); var deaths = Add(s, MysteriousDeaths, 0, Zone.Hand);
        Eq(Quote(s, dice.Id, "game-of-dice-draw"), 3, "A Game of Dice costs its 3");
        Eq(Quote(s, deaths.Id, "mysterious-deaths-destroy"), 6, "Mysterious Deaths costs its printed 6");
        Add(s, Kanaye, 0, Zone.Home);
        Eq(Quote(s, dice.Id, "game-of-dice-draw"), 2, "Kanaye takes 1 off A Game of Dice");
        Eq(Quote(s, deaths.Id, "mysterious-deaths-destroy"), 5, "and 1 off Mysterious Deaths, the same way");
    }

    private static void YojimbosDutyScenario()
    {
        var s = Table(Phase.Action, 0);
        var duty = Add(s, YojimbosDuty, 0, Zone.Hand);
        var yojimbo = Grant(Add(s, "training-shugenja", 0, Zone.Home), "Yojimbo"); // printed Gold Cost 3
        yojimbo.Modifiers.Add(new("test-source", "test", "GoldCost", -2)); // his CURRENT Gold Cost is 1: the discount still reads the printed 3
        var courtier = Grant(Add(s, "training-lion-veteran", 0, Zone.DynastyDiscard), "Courtier"); // printed Gold Cost 8
        var notCourtier = Add(s, "training-lion-samurai", 0, Zone.DynastyDiscard);
        s.Players[0].Honor = 10;
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "yojimbos-duty-recruit", duty.Id, yojimbo.Id);
        Check(option.TargetIds!.Contains(courtier.Id) && !option.TargetIds.Contains(notCourtier.Id), "only a discarded Courtier or Shugenja is a target");
        var recruit = e.QuotePurchase(0, courtier.Id, fromDeadPile: true);
        Check(recruit.Error is null, "the recruit itself is legal: " + recruit.Error);
        Eq(option.TargetGold![courtier.Id].Cost, Math.Max(0, recruit.Cost - 4), "his recruit cost less the Yojimbo's printed Gold Cost 3 plus 1");
        Accept(e, Offer(e, 0, "yojimbos-duty-recruit", duty.Id, courtier.Id, yojimbo.Id));
        Eq(e.State.Cards[courtier.Id].Zone, Zone.Home, "the Courtier entered play at home");
        Eq(e.State.Cards[yojimbo.Id].Zone, Zone.Dead, "the Yojimbo was destroyed as the cost");

        // The straighten half: a bowed performer at home straightens, and an additional Action-phase action follows.
        var s2 = Table(Phase.Action, 0);
        var duty2 = Add(s2, YojimbosDuty, 0, Zone.Hand);
        var bowed = Add(s2, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Batch336Support.Use(e2, 0, "yojimbos-duty-straighten", duty2.Id, performerId: bowed.Id));
        Check(!e2.State.Cards[bowed.Id].Bowed, "the performer straightened");
        Check(e2.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional action is granted");
        Eq(e2.State.PriorityPlayerId, 0, "and the player keeps priority to take it");
    }

    private static void AccidentalConfessionScenario()
    {
        var s = Table(Phase.Action, 0);
        var confession = Add(s, AccidentalConfession, 0, Zone.Hand);
        var courtier = Add(s, "training-courtier-personality", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        s.FavorPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "accidental-confession-dishonor", confession.Id, target.Id, courtier.Id));
        Check(e.State.Cards[target.Id].Dishonorable, "the target is dishonored");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        Check(e.State.Cards[target.Id].MovementLocks.Any(l => l.Kinds.HasFlag(MovementKinds.Assign) && l.Kinds.HasFlag(MovementKinds.ToBattlefield)), "he may not be assigned, and will not move, to battlefields this turn");

        var s2 = Table(Phase.Action, 0);
        var confession2 = Add(s2, AccidentalConfession, 0, Zone.Hand);
        var courtier2 = Add(s2, "training-courtier-personality", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Home);
        s2.FavorPlayerId = 1;
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "accidental-confession-dishonor", confession2.Id, courtier2.Id), "refused without the Imperial Favor");
    }

    private static void BrokenAllianceScenario()
    {
        var s = Table(Phase.Action, 0);
        var alliance = Add(s, BrokenAlliance, 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id); // Gold Cost 2
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "broken-alliance-destroy", alliance.Id);
        Eq(option.TargetGold![follower.Id].Cost, 0, "its Gold Cost 2 minus two");
        Accept(e, Offer(e, 0, "broken-alliance-destroy", alliance.Id, follower.Id));
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "the attachment was destroyed");

        var s2 = Table(Phase.Battle, 0);
        var alliance2 = Add(s2, BrokenAlliance, 0, Zone.Hand);
        var mover = Add(s2, "training-crane-samurai", 0, Zone.Home, bowed: true);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Batch336Support.Use(e2, 0, "broken-alliance-move", alliance2.Id, performerId: mover.Id));
        Check(e2.State.Cards[mover.Id].Zone == Zone.Battlefield && e2.State.Cards[mover.Id].ProvinceIndex == 0, "he moved to the current battlefield");
        Check(!e2.State.Cards[mover.Id].Bowed, "and straightened because he moved");

        var s3 = Table(Phase.Battle, 0);
        var alliance3 = Add(s3, BrokenAlliance, 0, Zone.Hand);
        var mover3 = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "broken-alliance-move", alliance3.Id, mover3.Id), "refused with no enemy units at the current battlefield");
    }

    private static void CastAsideScenario()
    {
        var s = Table(Phase.Action, 0);
        var cast = Add(s, CastAside, 0, Zone.Hand);
        var samurai = Grant(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true), "Samurai");
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: samurai.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Use(e, 0, "cast-aside-straighten", cast.Id, performerId: samurai.Id));
        Check(!e.State.Cards[samurai.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "the Samurai's whole unit straightened");

        foreach (var paragon in new[] { false, true })
        {
            var s2 = Table(Phase.Battle, 0);
            var cast2 = Add(s2, CastAside, 0, Zone.Hand);
            var samurai2 = Grant(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai");
            if (paragon) Grant(samurai2, "Paragon");
            var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Offer(e2, 0, "cast-aside-bow", cast2.Id, enemy.Id, samurai2.Id));
            if (paragon) Eq(e2.State.Cards[enemy.Id].Zone, Zone.Dead, "a Paragon's target is destroyed");
            else Check(e2.State.Cards[enemy.Id].Bowed && e2.State.Cards[enemy.Id].Zone == Zone.Battlefield, "a non-Paragon's target is only bowed");
        }
    }

    private static void ConsiderationScenario()
    {
        var s = Table(Phase.Action, 0);
        var consideration = Add(s, Consideration, 0, Zone.Hand);
        var tactician = Add(s, "training-tactician", 0, Zone.Home);
        var ward = Add(s, "training-crane-samurai", 0, Zone.Home);
        var bower = Add(s, "training-bow-any-source", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Enabled(e, 0, "consideration-boost", consideration.Id, tactician.Id).TargetIds!.Contains(tactician.Id), "the Tactician cannot target himself");
        Accept(e, Offer(e, 0, "consideration-boost", consideration.Id, ward.Id, tactician.Id));
        Eq(e.State.Cards[ward.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), 3, "+3F");
        Eq(e.State.PriorityPlayerId, 1, "the other player has the next opportunity");
        Accept(e, Offer(e, 1, "training-bow-any", bower.Id, ward.Id));
        Check(!e.State.Cards[ward.Id].Bowed, "the other player's action targeting him was negated");
        Check(e.State.Cards[bower.Id].AbilityUses.ContainsKey("training-bow-any"), "the negated action was still taken (its use is spent)");
        Eq(e.State.Cards[ward.Id].TargetShieldTurn, -1, "the shield is spent");
    }

    private static void CreatingOrderScenario()
    {
        var s = Table(Phase.Action, 0);
        var order = Add(s, CreatingOrder, 0, Zone.Hand);
        var first = Add(s, "training-lion-samurai", 1, Zone.Home);
        var second = Add(s, "training-crane-samurai", 0, Zone.Home);
        first.Modifiers.Add(new("fx", "weak-poison-force-zero", "Force", -4)); // from an action (A Weak Poison)
        first.Modifiers.Add(new("fx", "a-trait", "Force", 1)); // a standing trait's, not an action's
        second.Modifiers.Add(new("fx", "consideration-boost", "Force", 3)); // a bonus from an action
        var poison = Add(s, "oracle-l5r-99", 1, Zone.Hand); // A Weak Poison, for a NEW penalty afterwards
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "creating-order-negate", order.Id);
        Accept(e, option.Command! with { TargetId = first.Id, Selections = [Pick("targets", first.Id, second.Id)] });
        Check(e.State.Cards[first.Id].Modifiers.Where(m => m.SuppressedTurn != e.State.TurnNumber).All(m => m.Effect == "a-trait"), "the action's penalty is set aside, the trait's bonus stays");
        Check(e.State.Cards[second.Id].Modifiers.All(m => m.SuppressedTurn == e.State.TurnNumber), "the action's bonus is set aside on the second target too");
        Check(!CanUse(e, 0, "creating-order-straighten", order.Id), "one shared use");
        Accept(e, Offer(e, 1, "weak-poison-force-zero", poison.Id, first.Id));
        Check(e.State.Cards[first.Id].Modifiers.Where(m => m.SuppressedTurn != e.State.TurnNumber).All(m => m.Effect == "a-trait"), "a new Force penalty from an action is not applied this turn");
        Check(e.State.Cards[first.Id].Modifiers.All(m => m.Effect is "a-trait" or "weak-poison-force-zero"), "and it is not kept hidden either");

        var s2 = Table(Phase.Action, 0);
        var order2 = Add(s2, CreatingOrder, 0, Zone.Hand);
        var a = Add(s2, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var aFollower = Add(s2, "training-follower", 0, Zone.Attached, bowed: true, parent: a.Id);
        var b = Add(s2, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        var option2 = Enabled(e2, 0, "creating-order-straighten", order2.Id);
        Accept(e2, option2.Command! with { TargetId = a.Id, Selections = [Pick("targets", a.Id, b.Id)] });
        Check(!e2.State.Cards[a.Id].Bowed && !e2.State.Cards[aFollower.Id].Bowed && !e2.State.Cards[b.Id].Bowed, "both targets' units straightened");
    }

    private static void CreatingOrderSuppresses()
    {
        var s = Table(Phase.Action, 0);
        var order = Add(s, CreatingOrder, 0, Zone.Hand);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var printed = CardCatalog.Get("training-lion-samurai").Force;
        target.Modifiers.Add(new("fx", "consideration-boost", "Force", 2, Permanent: true)); // an action's permanent +2
        target.Modifiers.Add(new("fx", "consideration-boost", "Force", 3)); // an action's bonus for the turn
        target.Modifiers.Add(new("fx", "a-trait", "Force", 1, Permanent: true)); // a standing trait's
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(target.Id), printed + 6, "before");
        Accept(e, Enabled(e, 0, "creating-order-negate", order.Id).Command! with { TargetId = target.Id, Selections = [Pick("targets", target.Id)] });
        Eq(e.CardForceForTest(target.Id), printed + 1, "during the prevention only the trait counts");
        Eq(e.State.Cards[target.Id].Modifiers.Count, 3, "nothing was deleted");
        Check(e.State.Cards[target.Id].Modifiers.Any(m => m.Permanent && m.Source == "fx" && m.Amount == 2), "the permanent modifier keeps its source, amount and permanence");

        // The save: a detached copy through JSON keeps the hidden state.
        var saved = System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(e.State))!;
        Eq(MatchEngine.FromTestState(saved).CardForceForTest(target.Id), printed + 1, "after a save and load it is still hidden this turn");

        // The next turn: the turn's own modifier is swept as ever (its duration ended during the prevention); the permanent one is back.
        var later = saved;
        later.Cards[target.Id].Modifiers.RemoveAll(m => !m.Permanent); // FinishTurn's sweep
        later.TurnNumber++;
        Eq(MatchEngine.FromTestState(later).CardForceForTest(target.Id), printed + 3, "the permanent +2 applies again with the trait's +1; the turn's +3 ended during the prevention and does not return");
    }

    private static void DuelOfHaikuScenario()
    {
        var s = Table(Phase.Action, 0);
        var haiku = Add(s, DuelOfHaiku, 0, Zone.Hand);
        var poet = Add(s, "training-crane-veteran", 0, Zone.Home); // Personal Honor 3
        poet.Modifiers.Add(new("test-source", "test", "PersonalHonor", 6)); // a margin no focused card can overturn
        var rival = Add(s, "training-lion-samurai", 1, Zone.Home); // Personal Honor 2
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, Offer(e, 0, "duel-of-haiku", haiku.Id, rival.Id, poet.Id));
        Check(e.State.PendingDuel is { } duel && duel.AbilityId == "duel-of-haiku", "the duel was issued");
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Eq(Honor(e, 0), before + 1, "the winner's controller gained 1 Honor");
        Check(e.State.Cards[rival.Id].Bowed && e.State.Cards[rival.Id].Dishonorable, "the loser is bowed and dishonored");
        Check(!e.State.Cards[poet.Id].Dishonorable, "the winner is not");
    }

    private static void EntrenchedPositionScenario()
    {
        // Player 1 attacks and holds the first opportunity he claimed (an Engage claim, Naval Invasion): the Defender (player 0) has not yet had his first normal one.
        MatchEngine Rig(bool attackerLead, out CardInstance attackerCard, out CardInstance defenderCard, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1);
            s.BattleNumber = 1; s.FirstActionPlayerId = 1;
            if (attackerLead) s.AttackerLeadBattle = 1;
            attackerCard = Add(s, EntrenchedPosition, 1, Zone.Hand);
            defenderCard = Add(s, EntrenchedPosition, 0, Zone.Hand);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var attackerCard, out var defenderCard, out var victim);
        Accept(e, Offer(e, 1, "entrenched-bow", attackerCard.Id, victim.Id));
        Check(e.State.PendingChoice is { Type: "announceReaction", PlayerId: 0 }, "the Defender is offered the Reaction: " + e.State.PendingChoice?.Type);
        var reaction = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "entrenched-negate");
        Check(reaction is not null, "Entrenched Position is offered from the Defender's hand");
        Accept(e, reaction!.Command!);
        // The window closes once the players pass (the other Reactions of the window's own round), and the held action resolves negated.
        for (var guard = 0; e.State.PendingChoice?.Type == "announceReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is null, "the window closed");
        Check(!e.State.Cards[victim.Id].Bowed, "the Attacker's Battle action was negated");
        Eq(e.State.Cards[attackerCard.Id].Zone, Zone.FateDiscard, "and still taken: its Strategy is spent");
        Eq(e.State.Cards[defenderCard.Id].Zone, Zone.FateDiscard, "Entrenched Position was played");
        Eq(e.State.PriorityPlayerId, 0, "the Defender now has his first normal opportunity");
        Eq(e.State.AttackerLeadBattle, -1, "the Attacker's lead is over");

        // Without the lead (the Defender held the first opportunity, or the Attacker has already ceded priority) no window opens.
        var e2 = Rig(false, out var attackerCard2, out _, out var victim2);
        Accept(e2, Offer(e2, 1, "entrenched-bow", attackerCard2.Id, victim2.Id));
        Check(e2.State.PendingChoice is null && e2.State.Cards[victim2.Id].Bowed, "an Attacker's action after the Defender's first opportunity resolves normally");

        // The Battle half: bow a target enemy card without attachments.
        var s3 = Table(Phase.Battle, 0);
        var bow = Add(s3, EntrenchedPosition, 0, Zone.Hand);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var withFollower = Add(s3, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s3, "training-follower", 1, Zone.Attached, province: 0, parent: withFollower.Id);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!Enabled(e3, 0, "entrenched-bow", bow.Id).TargetIds!.Contains(withFollower.Id), "a card with attachments is not a target");
        Accept(e3, Offer(e3, 0, "entrenched-bow", bow.Id, enemy.Id));
        Check(e3.State.Cards[enemy.Id].Bowed, "the enemy card bowed");
    }

    private static void FeintAndStrikeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var feint = Add(s, FeintAndStrike, 0, Zone.Hand);
        var commander = Grant(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Commander");
        Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: commander.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Use(e, 0, "feint-strike-move", feint.Id, enemy.Id, commander.Id, Pick("target", enemy.Id), Pick("attachment", enemyFollower.Id)));
        Eq(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "the attachment in his unit was destroyed");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "and he moved home");

        var s2 = Table(Phase.Battle, 0);
        var feint2 = Add(s2, FeintAndStrike, 0, Zone.Hand);
        var bareCommander = Grant(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Commander");
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower2 = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: enemy2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!e2.Execute(Batch336Support.Use(e2, 0, "feint-strike-move", feint2.Id, enemy2.Id, bareCommander.Id, Pick("target", enemy2.Id), Pick("attachment", enemyFollower2.Id))).Accepted,
            "without a Follower of his own the Commander may not destroy an attachment");
        Accept(e2, Batch336Support.Use(e2, 0, "feint-strike-move", feint2.Id, enemy2.Id, bareCommander.Id, Pick("target", enemy2.Id)));
        Check(e2.State.Cards[enemy2.Id].Zone == Zone.Home && e2.State.Cards[enemyFollower2.Id].Zone == Zone.Attached, "he still moves home, his Follower untouched");

        // The Focus Effect: player 0 challenges (Steel on Steel) and focuses Feint & Strike; his Personality wins, so he must discard a card.
        var s3 = Table(Phase.Battle, 0);
        var mine = Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        mine.Modifiers.Add(new("test-source", "test", "Chi", 5));
        var theirs = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duelCard = Add(s3, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s3, FeintAndStrike, 0, Zone.Hand);
        var spare = Add(s3, "training-focus-strategy", 0, Zone.Hand);
        var top = Add(s3, "training-focus-strategy", 1, Zone.FateDeck); s3.Players[1].FateDeck.Insert(0, top.Id);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "steel-on-steel-duel", duelCard.Id, theirs.Id, mine.Id));
        Accept(e3, new GameCommand("focus", 1, e3.Version, Choice: "deck"));
        Accept(e3, new GameCommand("focus", 0, e3.Version, CardId: focused.Id));
        Accept(e3, new GameCommand("strike", 1, e3.Version));
        Check(e3.State.Cards[theirs.Id].Zone == Zone.Dead, "player 0's Personality won the duel");
        Check(e3.State.PendingChoice is { Type: "forcedDiscard", PlayerId: 0 }, "the winner's controller must discard a card: " + e3.State.PendingChoice?.Type);
        Accept(e3, new GameCommand("forcedDiscard", 0, e3.Version, TargetId: spare.Id));
        Eq(e3.State.Cards[spare.Id].Zone, Zone.FateDiscard, "the discarded card");
    }
}
