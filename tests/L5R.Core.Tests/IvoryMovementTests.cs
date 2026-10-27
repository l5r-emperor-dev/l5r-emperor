using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #622, strategy-wave-106: the ten "movement" Strategies (Advance Warning, Ambush, Back to the Front, Fall Back!, Determined Challenge, Treachery and Deceit, Soul's Sacrifice, Sneak Attack,
// Inspired Devotion, Vigilant Eyes), each read from its Emperor-era scan. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), driven through
// QueryActions and the commands it offers; IvoryMovement.cs holds the readings.
internal static class IvoryMovementTests
{
    private const string AdvanceWarning = "oracle-l5r-121", Ambush = "oracle-l5r-344", BackToTheFront = "oracle-l5r-662", FallBack = "oracle-l5r-2445", Devotion = "oracle-l5r-3774",
        SneakAttack = "oracle-l5r-7311", SoulsSacrifice = "oracle-l5r-7385", Treachery = "oracle-l5r-8741", VigilantEyes = "oracle-l5r-9156", DeterminedChallenge = "oracle-l5r-10143";
    private const int Extra = MatchEngine.FirstAdditionalBattlefield;

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static void RejectedWithoutChange(MatchEngine e, GameCommand command, string? contains = null)
    {
        var before = Json(e);
        var error = Rejection(e, command);
        if (contains is not null) Check(error.Contains(contains, StringComparison.OrdinalIgnoreCase), $"rejection says '{contains}': {error}");
        Eq(Json(e), before, "a rejected action changes nothing");
    }
    private static GameCommand Cmd(MatchEngine e, int player, string ability, CardInstance card, string? performer = null, string? target = null, int? province = null) =>
        new("useAbility", player, e.Version, card.Id, AbilityId: ability, PerformerId: performer, TargetId: target, Province: province);
    private static GameCommand Decider(MatchEngine e, int player, string? cardId) =>
        e.QueryActions(player).Single(a => a.Type == "chooseDecider" && (cardId is null ? a.Command!.Choice == "done" : a.Command!.TargetId == cardId)).Command!;

    public static void Run(TestRunner runner) => runner.Run("ivory-movement", [
        ("Published shape: the ten Strategies print their scans' text, keywords and abilities, ship their art, and the manifest lists exactly them", PublishedShape),
        ("Advance Warning: the Scout goes home as a cost, his unit is straightened, the province's strength is raised or lowered by his Force; every illegal use is refused unchanged", AdvanceWarningBasics),
        ("Advance Warning: a Scout who may not move cannot pay the cost; a negated action still pays it; a battlefield with no province has no strength to change", AdvanceWarningEdges),
        ("Ambush: any unbowed Personality attacks a bowed target at a new battlefield, the Defender has the first opportunity, 5 Honor is lost after the battle", AmbushBattle),
        ("Ambush: only the two assigned Personalities may move to its battlefield, every other Personality's movement there is negated, and only while it is fought", AmbushNegatesOthers),
        ("Ambush: illegal uses are refused unchanged (bowed performer, performer away from home, outside the Action Phase)", AmbushIllegal),
        ("Back to the Front: assignments are recorded, a Personality who went home returns to the battlefield he was assigned to and is straightened", BackToTheFrontRealFlow),
        ("Back to the Front: only a battlefield he was assigned to this turn, a different battlefield needs him at the current one, a performer already there does not move and is not straightened", BackToTheFrontLegality),
        ("Fall Back!: he goes home and his unit is straightened, then the player may straighten one of his other units; nothing is asked when none has anything to straighten or he did not move", FallBackBasics),
        ("Determined Challenge: an enemy Personality is moved to the current battlefield and straightened; the performer must be an unbowed attacker; a locked move straightens nothing", DeterminedChallengeBasics),
        ("Treachery and Deceit: a Personality whose unit's total Force is 7 or higher, either side's, goes home; lesser units are not legal targets", TreacheryBasics),
        ("Treachery and Deceit, Determined Challenge and Soul's Sacrifice: Tamori Tamoko can negate the movement of the target", TamokoNegatesTheMoves),
        ("Soul's Sacrifice: an unbowed Samurai with 3 or more Personal Honor sends an enemy home and permanently gains Shadowlands and Undead, even if the move is negated", SoulsSacrificeBasics),
        ("Sneak Attack: after engaging, the Attacker has the first opportunity whoever plays it; a Defender's own claim still overrides it", SneakAttackClaims),
        ("Inspired Devotion: a Yojimbo gets +2F or the highest Chi of any Courtier or Shugenja its player controls; the Chi form needs one; Battle and Open", InspiredDevotionBasics),
        ("Vigilant Eyes: an unbowed Samurai gives a Personality a Force penalty equal to his own Force, until the end of the turn", VigilantEyesBasics),
    ]);

    // ---- Published shape ---------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var expected = new (string Id, string Name, int Focus, string[] Abilities, string Text)[]
        {
            (AdvanceWarning, "Advance Warning", 2, ["advance-warning-raise", "advance-warning-lower"], "Battle: Move your performing Scout Personality home: Straighten his unit. Raise or lower the current battlefield's province's strength by your Scout's Force."),
            (Ambush, "Ambush", 2, ["ambush-battle"], "Limited: Choose your performing unbowed Personality and target another player's Personality: Create a battlefield (not at any province). Assign your Personality to attack there. Assign the target to defend there, even if he is bowed. Other Personalities will not move there. Fight a battle there (after this action's resolution). After the battle ends, lose 5 Honor."),
            (BackToTheFront, "Back to the Front", 3, ["back-to-the-front-move"], "Battle: Choose your performing Personality: Move him to a battlefield he assigned to this turn. If he moved, straighten his unit."),
            (FallBack, "Fall Back!", 4, ["fall-back-move"], "Battle: Choose your performing Personality: Move him home. If he moved, straighten his unit, and you may straighten one of your other units (at any location)."),
            (Devotion, "Inspired Devotion", 4, ["inspired-devotion-bonus", "inspired-devotion-chi"], "Battle/Open: Choose your performing Yojimbo: Give him +2F or a Force bonus equal to the highest Chi of any Courtier or Shugenja you control."),
            (SneakAttack, "Sneak Attack", 3, ["sneak-attack-claim"], "Reaction: After engaging: The Attacker has the first opportunity to take a Battle action or pass in this battle."),
            (SoulsSacrifice, "Soul's Sacrifice", 2, ["souls-sacrifice-move"], "Battle: Choose your performing unbowed Samurai with 3 or higher Personal Honor and target an enemy Personality: Move him home. Permanently give your Samurai Shadowlands and Undead."),
            (Treachery, "Treachery and Deceit", 4, ["treachery-deceit-move"], "Battle: Target a Personality whose unit's total Force is 7 or higher: Move him home."),
            (VigilantEyes, "Vigilant Eyes", 4, ["vigilant-eyes-penalty"], "Open: Choose your performing unbowed Samurai and target a Personality: Give him a Force penalty equal to your Samurai's Force."),
            (DeterminedChallenge, "Determined Challenge", 3, ["determined-challenge-move"], "Battle: Choose your performing unbowed attacking Personality and target an enemy Personality: Move the target to the current battlefield. Straighten him if he moved."),
        };
        var root = RepoRoot();
        foreach (var (id, name, focus, abilities, text) in expected)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.Focus == focus && card.GoldCost == 0 && card.Effect == "none" && !card.ScenarioOnly, $"{name}'s scan");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities");
            Eq(card.Text, text, $"{name}'s printed text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Check(CardCatalog.Get(VigilantEyes).Keywords!.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(["Dark Virtue", "Insight"]), "Vigilant Eyes' keywords");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-106.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
        Check(listed.SequenceEqual(expected.Select(x => x.Id).Order(StringComparer.Ordinal)), "the manifest lists exactly the ten cards");
        // Each printing is the one that was read: Advance Warning is Before the Dawn, Determined Challenge Seeds of Decay, the rest Emperor Edition.
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-106-sources.json"))).RootElement.EnumerateArray()
            .ToDictionary(r => r.GetProperty("card_id").GetString()!, r => string.Join("/", r.GetProperty("printing_set").EnumerateArray().Select(x => x.GetString())));
        foreach (var (id, set) in expected.Select(x => (x.Id, Set: x.Id == AdvanceWarning ? "Before the Dawn" : x.Id == DeterminedChallenge ? "Seeds of Decay" : "Emperor Edition")))
            Eq(sources[id], set, $"{id}'s printing");
        Eq(AbilityCatalog.All["ambush-battle"].Timing, "Limited"); Eq(AbilityCatalog.All["sneak-attack-claim"].Timing, "Reaction");
        Eq(AbilityCatalog.All["inspired-devotion-bonus"].Timing, "BattleOrOpen"); Eq(AbilityCatalog.All["vigilant-eyes-penalty"].Timing, "Open");
        Check(AbilityCatalog.All["determined-challenge-move"].MovesToBattle && AbilityCatalog.All["determined-challenge-move"].Move!.Destination == MoveSpec.CurrentBattlefield, "Determined Challenge moves a Personality to the battle");
        Check(!AbilityCatalog.All["treachery-deceit-move"].MovesToBattle && AbilityCatalog.All["treachery-deceit-move"].Move!.Destination == MoveSpec.Home, "Treachery and Deceit moves home");
    }

    // ---- Advance Warning ---------------------------------------------------------------------------------------------------------------------------
    // Player 0 attacks (the actor); the Scout (a Samurai Scout, Force 2) stands bowed at battlefield 0 with a bowed Follower; the defender's province 0 is player 1's.
    private static (MatchState S, CardInstance Card, CardInstance Scout, CardInstance Follower) AdvanceRig()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, AdvanceWarning, 0, Zone.Hand);
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: scout.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return (s, card, scout, follower);
    }

    private static void AdvanceWarningBasics()
    {
        foreach (var (ability, delta) in new[] { ("advance-warning-raise", 2), ("advance-warning-lower", -2) })
        {
            var (s, card, scout, follower) = AdvanceRig();
            var e = MatchEngine.FromTestState(s);
            var strength = e.ProvinceStrengthForTest(1, 0);
            Check(CanUse(e, 0, ability, card.Id, scout.Id), $"{ability} is offered");
            Accept(e, Cmd(e, 0, ability, card, scout.Id));
            Eq(e.State.Cards[scout.Id].Zone, Zone.Home, "the Scout went home (the cost)");
            Check(!e.State.Cards[scout.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "his whole unit is straightened");
            Eq(e.ProvinceStrengthForTest(1, 0), strength + delta, "the Defender's province at the battlefield changes by the Scout's Force");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        }

        // Illegal: not a Scout, not at the battle, not mine -- refused with nothing changed.
        var (t, c2, scout2, _) = AdvanceRig();
        var plain = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var home = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Scout");
        var theirs = Kw(Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Scout");
        var f = MatchEngine.FromTestState(t);
        RejectedWithoutChange(f, Cmd(f, 0, "advance-warning-raise", c2, plain.Id));
        RejectedWithoutChange(f, Cmd(f, 0, "advance-warning-raise", c2, home.Id));
        RejectedWithoutChange(f, Cmd(f, 0, "advance-warning-raise", c2, theirs.Id));
        RejectedWithoutChange(f, Cmd(f, 0, "advance-warning-raise", c2));
        Check(!CanUse(f, 0, "advance-warning-raise", c2.Id, plain.Id) && !CanUse(f, 0, "advance-warning-raise", c2.Id, home.Id), "and they are not offered");
        // He need not be unbowed (the scan prints no such condition): the bowed Scout above was legal.
        Accept(f, Cmd(f, 0, "advance-warning-lower", c2, scout2.Id));
        // No Presence: the acting player has no unit at the battlefield, so no Scout can be at it.
        var u = Table(Phase.Battle, 1); var c3 = Add(u, AdvanceWarning, 0, Zone.Hand); Kw(Add(u, "training-crane-samurai", 0, Zone.Home), "Scout"); u.PriorityPlayerId = 0;
        Check(!CanUse(MatchEngine.FromTestState(u), 0, "advance-warning-raise", c3.Id), "a Scout at home cannot perform a Battle action");
    }

    private static void AdvanceWarningEdges()
    {
        // A Scout who may not move home cannot pay the cost.
        var (s, card, scout, _) = AdvanceRig();
        scout.MovementLocks.Add(new("test", s.TurnNumber, MovementKinds.Home));
        var e = MatchEngine.FromTestState(s);
        RejectedWithoutChange(e, Cmd(e, 0, "advance-warning-raise", card, scout.Id), "may not move");
        Check(!CanUse(e, 0, "advance-warning-raise", card.Id, scout.Id), "not offered either");

        // A negated action still pays its costs (the Scout goes home) and does nothing else.
        var (t, c2, scout2, follower2) = AdvanceRig();
        var f = MatchEngine.FromTestState(t);
        var strength = f.ProvinceStrengthForTest(1, 0);
        f.NegateEveryAction = true;
        Accept(f, Cmd(f, 0, "advance-warning-raise", c2, scout2.Id));
        Eq(f.State.Cards[scout2.Id].Zone, Zone.Home, "the cost was paid"); Check(f.State.Cards[scout2.Id].Bowed && f.State.Cards[follower2.Id].Bowed, "but his unit stays bowed");
        Eq(f.ProvinceStrengthForTest(1, 0), strength, "and the strength is unchanged");

        // A battlefield with no province: nothing to raise or lower, and nothing breaks; the rest of the action still happens.
        var (u, c3, scout3, follower3) = AdvanceRig();
        var g = MatchEngine.FromTestState(u);
        var battlefield = g.CreateAdditionalBattlefieldForTest(null);
        g = Edit(g, st => { st.CurrentBattleProvince = battlefield; st.UnresolvedBattlefields = [battlefield]; st.Cards[scout3.Id].ProvinceIndex = battlefield; st.Cards[follower3.Id].ProvinceIndex = battlefield; });
        var all = Enumerable.Range(0, 4).Select(i => g.ProvinceStrengthForTest(1, i)).ToArray();
        Accept(g, Cmd(g, 0, "advance-warning-raise", c3, scout3.Id));
        Check(Enumerable.Range(0, 4).Select(i => g.ProvinceStrengthForTest(1, i)).SequenceEqual(all), "no province's strength changed");
        Check(!g.State.Cards[scout3.Id].Bowed && g.State.Cards[scout3.Id].Zone == Zone.Home, "he went home and was straightened");
    }

    // ---- Ambush -----------------------------------------------------------------------------------------------------------------------------------
    private static void AmbushBattle()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Ambush, 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home); Force(performer, 6);
        var other = Add(s, "training-crane-courtier", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var bystander = Add(s, "training-lion-courtier", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var offer = Options(e, 0, "ambush-battle", card.Id, performer.Id).Single(o => o.Enabled);
        Check(Options(e, 0, "ambush-battle", card.Id, other.Id).Any(o => o.Enabled), "any unbowed Personality performs (no keyword needed)");
        Check(offer.TargetIds!.Contains(target.Id) && offer.TargetIds.Contains(bystander.Id) && !offer.TargetIds.Contains(performer.Id), "another player's Personality is the target, bowed or not");
        var honor = Honor(e, 0);
        Accept(e, offer.Command! with { TargetId = target.Id });
        var battlefield = e.State.CurrentBattleProvince!.Value;
        Check(battlefield >= Extra && e.BattlefieldsForTest().Single().Province is null, "a battlefield not at any province");
        Eq(e.State.ActivePlayerId, 0, "the player of Ambush is the Attacker");
        Check(e.State.Cards[performer.Id].ProvinceIndex == battlefield && e.State.Cards[target.Id].ProvinceIndex == battlefield && e.State.Cards[target.Id].Bowed, "his Personality attacks there, the bowed target defends there");
        Eq(e.State.Cards[bystander.Id].Zone, Zone.Home, "nobody else is there"); Eq(e.State.Cards[other.Id].Zone, Zone.Home);
        Eq(e.State.PriorityPlayerId, 1, "no first opportunity is claimed: the Defender acts first, as by default");
        Check(e.State.Cards[card.Id].Zone == Zone.FateDiscard, "the Strategy is in the discard pile");
        Eq(Honor(e, 0), honor, "no Honor is lost before the battle ends");
        Accept(e, PassC(e)); Accept(e, PassC(e));
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the battle is fought: the bowed defender contributes no Force");
        Eq(Honor(e, 0), honor + 2 - 5, "5 Honor is lost after the battle (and 2 gained for the destroyed army)");
        Eq(e.State.Phase, Phase.Action, "the Action Phase resumes");
        Eq(e.State.Players[1].Provinces.Count(p => p.Destroyed), 0, "no province is destroyed");
        Eq(e.BattlefieldsForTest().Length, 0, "the battlefield ceased to exist");
    }

    private static void AmbushNegatesOthers()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Ambush, 0, Zone.Hand);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home); Force(performer, 6);
        var mine = Add(s, "training-crane-courtier", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var theirs = Add(s, "training-lion-courtier", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Options(e, 0, "ambush-battle", card.Id, performer.Id).Single(o => o.Enabled).Command! with { TargetId = target.Id });
        var battlefield = e.State.CurrentBattleProvince!.Value;
        Check(!e.MoveToBattlefieldForTest(mine.Id, battlefield) && !e.MoveToBattlefieldForTest(theirs.Id, battlefield), "other Personalities will not move there, whoever's they are");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Home); Eq(e.State.Cards[theirs.Id].Zone, Zone.Home);
        Check(e.State.Events.Count(x => x.Type == "moveNegated") >= 2, "each negation is announced");
        // The two Personalities it assigned may still move there (they are not "other Personalities").
        e = Edit(e, st => { st.Cards[performer.Id].Zone = Zone.Home; st.Cards[performer.Id].ProvinceIndex = null; });
        Check(e.MoveToBattlefieldForTest(performer.Id, battlefield), "the Personality that was assigned may move back there");
        // A move to a different battlefield is untouched.
        Check(!e.MoveToBattlefieldForTest(mine.Id, battlefield), "still negated for the others");

        // Without Ambush (an ordinary Action Phase battle) nothing is negated: control, and the negation ends with the battle.
        var t = Table(Phase.Battle, 1);
        var plain = Add(t, "training-crane-courtier", 0, Zone.Home);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Check(f.MoveToBattlefieldForTest(plain.Id, 0), "no Ambush, no negation");
    }

    private static void AmbushIllegal()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Ambush, 0, Zone.Hand);
        var bowed = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var ready = Add(s, "training-crane-courtier", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        RejectedWithoutChange(e, Cmd(e, 0, "ambush-battle", card, bowed.Id, target.Id));
        RejectedWithoutChange(e, Cmd(e, 0, "ambush-battle", card, ready.Id, ready.Id));
        Check(!Options(e, 0, "ambush-battle", card.Id, bowed.Id).Any(o => o.Enabled), "a bowed Personality is not offered as the performer");
        // The performer must be at home to be assigned: a Personality at a battlefield is not.
        var t = Table(Phase.Action, 0);
        var c2 = Add(t, Ambush, 0, Zone.Hand);
        var away = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var tgt = Add(t, "training-lion-samurai", 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        RejectedWithoutChange(f, Cmd(f, 0, "ambush-battle", c2, away.Id, tgt.Id));
        // Not in a battle: Limited is an Action Phase action.
        var u = Table(Phase.Battle, 0);
        var c3 = Add(u, Ambush, 0, Zone.Hand); Add(u, "training-crane-samurai", 0, Zone.Home); Add(u, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(u), 0, "ambush-battle", c3.Id), "not offered in a battle");
    }

    // ---- Back to the Front ------------------------------------------------------------------------------------------------------------------------
    private static void BackToTheFrontRealFlow()
    {
        // Player 1 attacks province 0 with one Personality and player 0 defends with one; the assignments are made through the real Maneuvers segments.
        var s = Table(Phase.Action, 1);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Home);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Home);
        var back = Add(s, BackToTheFront, 1, Zone.Hand);
        var retreat = Add(s, FallBack, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.Phase == Phase.Battle);
        var recorded = e.State.IvoryMovement.Assigned;
        Check(recorded.TryGetValue(attacker.Id, out var a) && a.SequenceEqual([0]) && recorded.TryGetValue(defender.Id, out var d) && d.SequenceEqual([0]), "both assignments are on the record");
        Eq(e.State.IvoryMovement.Key, $"{e.State.TurnNumber}|1", "the record belongs to this Attack Phase");
        // The attacker retreats (Fall Back!), bowed, then returns (Back to the Front) and is straightened.
        e = Edit(e, st => { st.PriorityPlayerId = 1; st.ConsecutivePasses = 0; st.Cards[attacker.Id].Bowed = true; });
        Check(!CanUse(e, 1, "back-to-the-front-move", back.Id, attacker.Id) || e.State.Cards[attacker.Id].Zone == Zone.Battlefield, "(he is at the battlefield: moving there moves nothing)");
        Accept(e, Cmd(e, 1, "fall-back-move", retreat, attacker.Id));
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home); Check(!e.State.Cards[attacker.Id].Bowed, "Fall Back! straightened him");
        e = Edit(e, st => { st.Cards[attacker.Id].Bowed = true; st.PriorityPlayerId = 1; st.ConsecutivePasses = 0; });
        var option = Options(e, 1, "back-to-the-front-move", back.Id, attacker.Id).Single(o => o.Enabled);
        Eq(string.Join(",", option.Provinces ?? []), "0", "the battlefield he was assigned to is the one offered");
        Accept(e, option.Command! with { Province = 0 });
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Battlefield); Eq(e.State.Cards[attacker.Id].ProvinceIndex, (int?)0);
        Check(!e.State.Cards[attacker.Id].Bowed, "he moved, so his unit is straightened");
        Eq(e.State.Cards[back.Id].Zone, Zone.FateDiscard);
    }

    private static void BackToTheFrontLegality()
    {
        // Two battlefields to fight (0 current, 1 next). The record is set up directly: A (at 0) was assigned to 0 and 1, B (at home) to 1 only, C (at home) to 0 only, D (at home) to nothing.
        var s = Table(Phase.Battle, 1);
        s.UnresolvedBattlefields = [0, 1];
        var key = $"{s.TurnNumber}|{s.ActivePlayerId}";
        var cardA = Add(s, BackToTheFront, 1, Zone.Hand); var cardB = Add(s, BackToTheFront, 1, Zone.Hand);
        var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var b = Add(s, "training-lion-courtier", 1, Zone.Home, bowed: true);
        var c = Add(s, "training-lion-veteran", 1, Zone.Home, bowed: true);
        var dd = Add(s, "training-lion-dignitary", 1, Zone.Home);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        s.IvoryMovement = new() { Key = key, Assigned = new() { [a.Id] = [0, 1], [b.Id] = [1], [c.Id] = [0] } };
        var e = MatchEngine.FromTestState(s);
        // A is at the current battlefield: he may choose 0 (no move) or 1.
        var optionA = Options(e, 1, "back-to-the-front-move", cardA.Id, a.Id).Single(o => o.Enabled);
        Eq(string.Join(",", optionA.Provinces!.Order()), "0,1", "the battlefields he was assigned to");
        // C (home, assigned to the current battlefield): the Exception -- a move to the current battlefield needs neither presence nor location.
        var optionC = Options(e, 1, "back-to-the-front-move", cardA.Id, c.Id).Single(o => o.Enabled);
        Eq(string.Join(",", optionC.Provinces!), "0");
        // B (home, assigned only to the other battlefield): moving there is not the current battlefield, so he would have to be at the current one -- refused. D was assigned nowhere.
        RejectedWithoutChange(e, Cmd(e, 1, "back-to-the-front-move", cardA, b.Id, province: 1), "different battlefield");
        Check(!Options(e, 1, "back-to-the-front-move", cardA.Id, b.Id).Any(o => o.Enabled), "B has no legal destination, so he is not offered");
        Check(!Options(e, 1, "back-to-the-front-move", cardA.Id, dd.Id).Any(o => o.Enabled), "D was assigned to nothing: not offered");
        RejectedWithoutChange(e, Cmd(e, 1, "back-to-the-front-move", cardA, dd.Id, province: 0), "assigned");
        RejectedWithoutChange(e, Cmd(e, 1, "back-to-the-front-move", cardA, c.Id, province: 1), "assigned");
        RejectedWithoutChange(e, Cmd(e, 1, "back-to-the-front-move", cardA, c.Id)); // no battlefield named
        RejectedWithoutChange(e, Cmd(e, 1, "back-to-the-front-move", cardA, c.Id, province: 2));
        // A to 1: he moves to the other battlefield and is straightened.
        Accept(e, optionA.Command! with { Province = 1 });
        Eq(e.State.Cards[a.Id].ProvinceIndex, (int?)1); Check(!e.State.Cards[a.Id].Bowed, "moved: straightened");
        // A choosing the battlefield he already stands on moves nothing and straightens nothing ("if he moved").
        var f = MatchEngine.FromTestState(s);
        Accept(f, Options(f, 1, "back-to-the-front-move", cardB.Id, a.Id).Single(o => o.Enabled).Command! with { Province = 0 });
        Eq(f.State.Cards[a.Id].ProvinceIndex, (int?)0); Check(f.State.Cards[a.Id].Bowed, "he did not move, so he is not straightened");
        // C returns to the current battlefield from home.
        var g = MatchEngine.FromTestState(s);
        Accept(g, Cmd(g, 1, "back-to-the-front-move", cardA, c.Id, province: 0));
        Eq(g.State.Cards[c.Id].Zone, Zone.Battlefield); Check(!g.State.Cards[c.Id].Bowed);
        // A record from another Attack Phase is not this turn's.
        var h = Edit(MatchEngine.FromTestState(s), st => st.IvoryMovement.Key = "0|0");
        Check(!Options(h, 1, "back-to-the-front-move", cardA.Id, a.Id).Any(o => o.Enabled), "a stale record names nothing");
    }

    // ---- Fall Back! -------------------------------------------------------------------------------------------------------------------------------
    private static void FallBackBasics()
    {
        var s = Table(Phase.Battle, 1);
        var card = Add(s, FallBack, 1, Zone.Hand);
        var mover = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: mover.Id);
        var elsewhere = Add(s, "training-lion-courtier", 1, Zone.Home, bowed: true);                 // a bowed unit at home
        var fresh = Add(s, "training-lion-dignitary", 1, Zone.Home);                                // an unbowed one: nothing to straighten
        var away = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 1); away.Bowed = true; // a bowed unit at another location
        var their = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, 1, "fall-back-move", card, mover.Id));
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "he is home"); Check(!e.State.Cards[mover.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "his unit is straightened");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "now the player may straighten another of his units"); Eq(e.State.PendingChoice?.PlayerId, 1);
        var offered = e.QueryActions(1).Where(a => a.Type == "chooseDecider" && a.Command!.TargetId is not null).Select(a => a.Command!.TargetId!).Order(StringComparer.Ordinal).ToArray();
        Check(offered.SequenceEqual(new[] { elsewhere.Id, away.Id }.Order(StringComparer.Ordinal)), "his other units that have something to straighten, at any location: " + string.Join(",", offered));
        Check(!offered.Contains(their.Id) && !offered.Contains(fresh.Id) && !offered.Contains(mover.Id), "not the enemy's, not an unbowed one, not his own unit");
        Check(e.QueryActions(1).Any(a => a.Type == "chooseDecider" && a.Command!.Choice == "done"), "and he may choose none");
        Check(!e.QueryActions(0).Any(a => a.Type == "chooseDecider"), "the other player decides nothing");
        Accept(e, Decider(e, 1, away.Id));
        Check(!e.State.Cards[away.Id].Bowed && e.State.Cards[elsewhere.Id].Bowed, "the one he named is straightened, and only it");
        Check(e.State.PendingChoice is null && e.State.Cards[card.Id].Zone == Zone.FateDiscard, "the action is complete and the Strategy spent");

        // Declining straightens nothing.
        var f = MatchEngine.FromTestState(s);
        Accept(f, Cmd(f, 1, "fall-back-move", card, mover.Id));
        Accept(f, Decider(f, 1, null));
        Check(f.State.Cards[elsewhere.Id].Bowed && f.State.Cards[away.Id].Bowed && f.State.PendingChoice is null, "declined");

        // Nothing else to straighten: nothing is asked.
        var t = Table(Phase.Battle, 1);
        var c2 = Add(t, FallBack, 1, Zone.Hand); var m2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        Add(t, "training-lion-dignitary", 1, Zone.Home);
        var g = MatchEngine.FromTestState(t);
        Accept(g, Cmd(g, 1, "fall-back-move", c2, m2.Id));
        Check(g.State.PendingChoice is null && !g.State.Cards[m2.Id].Bowed && g.State.Cards[m2.Id].Zone == Zone.Home, "he went home, was straightened and nothing was asked");

        // He did not move (his movement is negated): nothing is straightened and nothing is asked, though another unit is bowed.
        var u = Table(Phase.Battle, 1);
        var c3 = Add(u, FallBack, 1, Zone.Hand); var m3 = Add(u, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var other = Add(u, "training-lion-courtier", 1, Zone.Home, bowed: true);
        m3.MovementLocks.Add(new("test", u.TurnNumber, MovementKinds.Home));
        var h = MatchEngine.FromTestState(u);
        Accept(h, Cmd(h, 1, "fall-back-move", c3, m3.Id));
        Check(h.State.Cards[m3.Id].Zone == Zone.Battlefield && h.State.Cards[m3.Id].Bowed && h.State.Cards[other.Id].Bowed && h.State.PendingChoice is null, "if he did not move, neither straighten happens");

        // Illegal: a Personality at home cannot perform a Battle action; an enemy's cannot be named.
        var v = Table(Phase.Battle, 1);
        var c4 = Add(v, FallBack, 1, Zone.Hand); Add(v, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var athome = Add(v, "training-lion-courtier", 1, Zone.Home); var enemy = Add(v, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var w = MatchEngine.FromTestState(v);
        RejectedWithoutChange(w, Cmd(w, 1, "fall-back-move", c4, athome.Id)); RejectedWithoutChange(w, Cmd(w, 1, "fall-back-move", c4, enemy.Id)); RejectedWithoutChange(w, Cmd(w, 1, "fall-back-move", c4));
    }

    // ---- Determined Challenge ---------------------------------------------------------------------------------------------------------------------
    private static void DeterminedChallengeBasics()
    {
        var s = Table(Phase.Battle, 1);
        var card = Add(s, DeterminedChallenge, 1, Zone.Hand);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bowedAttacker = Add(s, "training-lion-courtier", 1, Zone.Battlefield, bowed: true, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: target.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 1, "determined-challenge-move", card.Id, attacker.Id).Single(o => o.Enabled);
        Check(option.TargetIds!.Contains(target.Id) && !option.TargetIds.Contains(attacker.Id), "an enemy Personality, even one at home, is a target");
        Check(!Options(e, 1, "determined-challenge-move", card.Id, bowedAttacker.Id).Any(o => o.Enabled), "a bowed attacker cannot perform");
        RejectedWithoutChange(e, Cmd(e, 1, "determined-challenge-move", card, bowedAttacker.Id, target.Id));
        RejectedWithoutChange(e, Cmd(e, 1, "determined-challenge-move", card, attacker.Id, attacker.Id));
        Accept(e, option.Command! with { TargetId = target.Id });
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield); Eq(e.State.Cards[target.Id].ProvinceIndex, (int?)0);
        Check(!e.State.Cards[target.Id].Bowed, "he moved, so he is straightened"); Check(e.State.Cards[follower.Id].Bowed, "only him: 'Straighten him' does not name his unit");
        Eq(e.State.Cards[follower.Id].ProvinceIndex, (int?)0, "his unit moved with him");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);

        // An unbowed performer who is not attacking (a defender) cannot perform: player 0 defends.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var c2 = Add(t, DeterminedChallenge, 0, Zone.Hand);
        var defender = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var far = Add(t, "training-lion-courtier", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "determined-challenge-move", c2.Id, defender.Id), "a defending Personality cannot perform it");

        // The target is already at the battlefield: he does not move, so he is not straightened. A locked target does not move and is not straightened either.
        var u = Table(Phase.Battle, 1);
        var c3 = Add(u, DeterminedChallenge, 1, Zone.Hand); var perf = Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var there = Add(u, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var locked = Add(u, "training-crane-courtier", 0, Zone.Home, bowed: true); locked.MovementLocks.Add(new("test", u.TurnNumber, MovementKinds.ToBattlefield));
        var f = MatchEngine.FromTestState(u);
        Accept(f, Cmd(f, 1, "determined-challenge-move", c3, perf.Id, there.Id));
        Check(f.State.Cards[there.Id].Bowed, "he was already there: not moved, not straightened");
        var g = MatchEngine.FromTestState(u);
        Accept(g, Cmd(g, 1, "determined-challenge-move", c3, perf.Id, locked.Id));
        Eq(g.State.Cards[locked.Id].Zone, Zone.Home); Check(g.State.Cards[locked.Id].Bowed, "negated movement: no straighten");
        _ = far;
    }

    // ---- Treachery and Deceit ---------------------------------------------------------------------------------------------------------------------
    private static void TreacheryBasics()
    {
        var s = Table(Phase.Battle, 1);
        var card = Add(s, Treachery, 1, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);                          // his own unit at the battlefield (presence)
        var big = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);               // Force 5 ...
        var bigFollower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: big.Id); // ... + 2 (a bowed Follower still counts) = 7
        var small = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);             // 3
        var ownBig = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);             // Force 6 ...
        var ownFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: ownBig.Id); // ... + 2 = 8: a Personality of the acting player is a legal target too
        var home = Add(s, "training-crane-veteran", 0, Zone.Home); Add(s, "training-follower", 0, Zone.Attached, parent: home.Id); Add(s, "training-follower", 0, Zone.Attached, parent: home.Id); // 9, but not at the battle
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 1, "treachery-deceit-move", card.Id).Single(o => o.Enabled).TargetIds!;
        Check(targets.Contains(big.Id) && targets.Contains(ownBig.Id), "units of 7 or more, either side's");
        Check(!targets.Contains(small.Id), "a unit below 7 is not a target"); Check(!targets.Contains(home.Id), "a unit that is not at the battle is not a target (a Battle action)");
        RejectedWithoutChange(e, Cmd(e, 1, "treachery-deceit-move", card, target: small.Id));
        Accept(e, Cmd(e, 1, "treachery-deceit-move", card, target: big.Id));
        Eq(e.State.Cards[big.Id].Zone, Zone.Home, "he goes home"); Eq(e.State.Cards[bigFollower.Id].Zone, Zone.Attached, "his unit with him");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard); Check(!e.State.Cards[big.Id].Bowed, "nothing but the move");
        // The total is live: a Force penalty brings a unit under 7.
        var t = Table(Phase.Battle, 1);
        var c2 = Add(t, Treachery, 1, Zone.Hand); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var pen = Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(t, "training-follower", 0, Zone.Attached, province: 0, parent: pen.Id);
        var f = MatchEngine.FromTestState(t);
        Check(CanUse(f, 1, "treachery-deceit-move", c2.Id), "7 is enough");
        f = Edit(f, st => Force(st.Cards[pen.Id], -1));
        Check(!CanUse(f, 1, "treachery-deceit-move", c2.Id), "a unit at 6 is not");
    }

    // ---- Tamori Tamoko -----------------------------------------------------------------------------------------------------------------------------
    private static void TamokoNegatesTheMoves()
    {
        // Treachery and Deceit: the Defender's Tamoko negates the movement of the targeted Personality.
        var s = Table(Phase.Battle, 1);
        var card = Add(s, Treachery, 1, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var big = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: big.Id);
        var tamoko = Add(s, "training-tamoko", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cmd(e, 1, "treachery-deceit-move", card, target: big.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the movement is offered for negation");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: tamoko.Id, AbilityId: "tamoko-negate"));
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[big.Id].Zone, Zone.Battlefield, "negated: he never moved"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the card is still spent");
        Check(e.State.Events.Any(x => x.Type == "moveNegated"));

        // Soul's Sacrifice: negated, but the keywords are still given (independent sentences).
        var t = Table(Phase.Battle, 1);
        var c2 = Add(t, SoulsSacrifice, 1, Zone.Hand); var samurai = Kw(Add(t, "training-crane-dignitary", 1, Zone.Battlefield, province: 0), "Samurai");
        var enemy = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var tam2 = Add(t, "training-tamoko", 0, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Cmd(f, 1, "souls-sacrifice-move", c2, samurai.Id, enemy.Id));
        Eq(f.State.PendingChoice?.Type, "beforeActionReaction");
        Accept(f, new GameCommand("useAbility", 0, f.Version, CardId: tam2.Id, AbilityId: "tamoko-negate"));
        while (f.State.PendingChoice?.Type == "beforeActionReaction") Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        Eq(f.State.Cards[enemy.Id].Zone, Zone.Battlefield, "negated: he stays");
        Check(f.State.Cards[samurai.Id].GrantedKeywords.Any(g => g.Keyword == "Shadowlands") && f.State.Cards[samurai.Id].GrantedKeywords.Any(g => g.Keyword == "Undead"), "yet the Samurai has his keywords");

        // Determined Challenge: the Defender's Tamoko negates the target's movement to the battle; he is not straightened.
        var u = Table(Phase.Battle, 1);
        var c3 = Add(u, DeterminedChallenge, 1, Zone.Hand); var perf = Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var tgt = Add(u, "training-crane-samurai", 0, Zone.Home, bowed: true); var tam3 = Add(u, "training-tamoko", 0, Zone.Battlefield, province: 0);
        var g2 = MatchEngine.FromTestState(u);
        Accept(g2, Cmd(g2, 1, "determined-challenge-move", c3, perf.Id, tgt.Id));
        Eq(g2.State.PendingChoice?.Type, "beforeActionReaction");
        Accept(g2, new GameCommand("useAbility", 0, g2.Version, CardId: tam3.Id, AbilityId: "tamoko-negate"));
        while (g2.State.PendingChoice?.Type == "beforeActionReaction") Accept(g2, new GameCommand("declineReaction", g2.State.PriorityPlayerId, g2.Version));
        Eq(g2.State.Cards[tgt.Id].Zone, Zone.Home); Check(g2.State.Cards[tgt.Id].Bowed, "negated: he did not move, so he is not straightened");
    }

    // ---- Soul's Sacrifice -------------------------------------------------------------------------------------------------------------------------
    private static void SoulsSacrificeBasics()
    {
        var s = Table(Phase.Battle, 1);
        var card = Add(s, SoulsSacrifice, 1, Zone.Hand);
        var honored = Kw(Add(s, "training-crane-dignitary", 1, Zone.Battlefield, province: 0), "Samurai");      // Personal Honor 4
        var low = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Samurai");            // Personal Honor 2
        var noSamurai = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0);                     // Personal Honor 4, not a Samurai
        var bowedHonored = Kw(Add(s, "training-lion-veteran", 1, Zone.Battlefield, bowed: true, province: 0), "Samurai"); // Personal Honor 3 but bowed
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ownTarget = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var performers = Options(e, 1, "souls-sacrifice-move", card.Id).Where(o => o.Enabled).Select(o => o.Command!.PerformerId).ToArray();
        Check(performers.Contains(honored.Id) && !performers.Contains(low.Id) && !performers.Contains(noSamurai.Id) && !performers.Contains(bowedHonored.Id), "only an unbowed Samurai with 3 or more Personal Honor performs: " + string.Join(",", performers));
        RejectedWithoutChange(e, Cmd(e, 1, "souls-sacrifice-move", card, low.Id, enemy.Id)); RejectedWithoutChange(e, Cmd(e, 1, "souls-sacrifice-move", card, noSamurai.Id, enemy.Id));
        RejectedWithoutChange(e, Cmd(e, 1, "souls-sacrifice-move", card, bowedHonored.Id, enemy.Id)); RejectedWithoutChange(e, Cmd(e, 1, "souls-sacrifice-move", card, honored.Id, ownTarget.Id));
        Check(!Options(e, 1, "souls-sacrifice-move", card.Id, honored.Id).Single(o => o.Enabled).TargetIds!.Contains(ownTarget.Id), "the target is an enemy");
        Accept(e, Cmd(e, 1, "souls-sacrifice-move", card, honored.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy is sent home");
        var after = e.State.Cards[honored.Id];
        Check(after.GrantedKeywords.Any(g => g.Keyword == "Shadowlands" && g.Duration == GrantDuration.Permanent) && after.GrantedKeywords.Any(g => g.Keyword == "Undead" && g.Duration == GrantDuration.Permanent), "the Samurai permanently has Shadowlands and Undead");
        Check(e.State.Cards[low.Id].GrantedKeywords.All(g => g.Keyword != "Shadowlands"), "no one else does");
        // His Personal Honor counts as it stands: a dishonorable Samurai has none.
        var t = Table(Phase.Battle, 1);
        var c2 = Add(t, SoulsSacrifice, 1, Zone.Hand); var shamed = Kw(Add(t, "training-crane-dignitary", 1, Zone.Battlefield, province: 0), "Samurai"); shamed.Dishonorable = true;
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 1, "souls-sacrifice-move", c2.Id, shamed.Id), "a dishonorable Samurai's Personal Honor is 0");
    }

    // ---- Sneak Attack ------------------------------------------------------------------------------------------------------------------------------
    // Battle Selection with units on both sides; player 0 is the Attacker.
    private static MatchState SelectionTable()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return s;
    }

    private static void SneakAttackClaims()
    {
        // The Attacker plays it.
        var s = SelectionTable();
        var card = Add(s, SneakAttack, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens for it");
        Check(CanUse(e, 0, "sneak-attack-claim", card.Id), "offered after engaging");
        Accept(e, Cmd(e, 0, "sneak-attack-claim", card));
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.FirstActionPlayerId, 0, "the Attacker has the first opportunity"); Eq(e.State.FirstActionReason, BattleSteps.ReasonEngageGrant); Eq(e.State.PriorityPlayerId, 0);

        // The Defender plays it: it is still the ATTACKER who has the first opportunity.
        var t = SelectionTable();
        var c2 = Add(t, SneakAttack, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, new GameCommand("selectBattle", 0, f.Version, Province: 0));
        Check(!CanUse(f, 0, "sneak-attack-claim", c2.Id), "(it is the Defender's card)");
        Accept(f, new GameCommand("declineReaction", 0, f.Version));
        Accept(f, Cmd(f, 1, "sneak-attack-claim", c2));
        while (f.State.PendingChoice?.Type == "engageReaction") Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        Eq(f.State.FirstActionPlayerId, 0, "the Attacker holds it, whoever played the card"); Eq(f.State.PriorityPlayerId, 0);

        // Not after engaging: nothing in hand outside the window offers it.
        var u = Table(Phase.Battle, 0); var c3 = Add(u, SneakAttack, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(u), 0, "sneak-attack-claim", c3.Id), "not offered in the Combat Segment");
        var v = SelectionTable(); var c4 = Add(v, SneakAttack, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(v), 0, "sneak-attack-claim", c4.Id), "not offered before a battle is chosen");

        // A Defender's own first-opportunity claim (Daidoji Yuki, the Defender's) still overrides it.
        var w = SelectionTable();
        var c5 = Add(w, SneakAttack, 0, Zone.Hand);
        var yuki = Add(w, "oracle-l5r-1696", 1, Zone.Battlefield, province: 0); w.Cards[yuki.Id].Zone = Zone.Battlefield;
        foreach (var c in w.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && c.Id != yuki.Id).ToArray()) w.Cards.Remove(c.Id);
        Kw(yuki, "Scout");
        var g = MatchEngine.FromTestState(w);
        Accept(g, new GameCommand("selectBattle", 0, g.Version, Province: 0));
        // Both players hold a Reaction (issue #803): each announces what they hold, the turn player orders, and the announced Reactions are played.
        while (g.State.PendingChoice?.Type == "engageReaction")
        {
            var actor = g.State.PriorityPlayerId; var offered = g.QueryActions(actor);
            var pick = offered.FirstOrDefault(o => o.Enabled && o.Command is not null && o.Type is "reactionAnnounce" or "reactionOrder" or "useAbility")
                ?? offered.First(o => o.Enabled && o.Type is "reactionAnnounceDone" or "declineReaction");
            Accept(g, pick.Command!);
        }
        Eq(g.State.FirstActionPlayerId, 1, "the Defender's claim overrides the Attacker's Sneak Attack");
    }

    // ---- Inspired Devotion ------------------------------------------------------------------------------------------------------------------------
    private static void InspiredDevotionBasics()
    {
        foreach (var phase in new[] { Phase.Battle, Phase.Action })
        {
            var s = Table(phase, 0);
            var card = Add(s, Devotion, 0, Zone.Hand);
            var yojimbo = Kw(Add(s, "training-crane-samurai", 0, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null), "Yojimbo");
            var plain = Add(s, "training-crane-veteran", 0, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
            if (phase == Phase.Battle) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");                      // Chi 3
            var shugenja = Kw(Add(s, "training-crane-dignitary", 0, Zone.Home), "Shugenja");                       // Chi 4: the highest
            var theirs = Kw(Add(s, "training-lion-veteran", 1, Zone.Home), "Courtier");                            // Chi 4, but not mine
            var e = MatchEngine.FromTestState(s);
            var baseForce = e.CardForceForTest(yojimbo.Id);
            Check(!CanUse(e, 0, "inspired-devotion-bonus", card.Id, plain.Id), $"{phase}: only a Yojimbo may perform it");
            RejectedWithoutChange(e, Cmd(e, 0, "inspired-devotion-bonus", card, plain.Id));
            // +2F.
            var f = MatchEngine.FromTestState(s);
            Accept(f, Cmd(f, 0, "inspired-devotion-bonus", card, yojimbo.Id));
            Eq(f.CardForceForTest(yojimbo.Id), baseForce + 2, $"{phase}: +2F"); Eq(f.State.Cards[card.Id].Zone, Zone.FateDiscard);
            // The highest Chi of any Courtier or Shugenja I control: 4 (the Shugenja's), at any location, not the opponent's.
            var g = MatchEngine.FromTestState(s);
            Accept(g, Cmd(g, 0, "inspired-devotion-chi", card, yojimbo.Id));
            Eq(g.CardForceForTest(yojimbo.Id), baseForce + 4, $"{phase}: the highest Chi of a Courtier or Shugenja");
            // Without a Courtier or Shugenja the Chi form cannot be taken (the +2F form can).
            var h = Edit(MatchEngine.FromTestState(s), st => { st.Cards.Remove(courtier.Id); st.Cards.Remove(shugenja.Id); st.Players[0].Hand.RemoveAll(id => id == courtier.Id || id == shugenja.Id); });
            Check(!CanUse(h, 0, "inspired-devotion-chi", card.Id, yojimbo.Id) && CanUse(h, 0, "inspired-devotion-bonus", card.Id, yojimbo.Id), $"{phase}: no Courtier or Shugenja, no Chi form");
            RejectedWithoutChange(h, Cmd(h, 0, "inspired-devotion-chi", card, yojimbo.Id), "Courtier or Shugenja");
            // The opponent's Courtier alone does not count.
            Check(h.State.Cards.ContainsKey(theirs.Id));
        }
        // A Battle action needs the Yojimbo at the battle; Open does not: at home in a battle he cannot perform.
        var t = Table(Phase.Battle, 0);
        var c2 = Add(t, Devotion, 0, Zone.Hand); var home = Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Yojimbo"); Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "inspired-devotion-bonus", c2.Id, home.Id), "at home in a battle he cannot perform");
        // The two forms share one use: the card is a single Strategy, discarded when played.
        Eq(AbilityCatalog.All["inspired-devotion-bonus"].UsageKey, AbilityCatalog.All["inspired-devotion-chi"].UsageKey);
    }

    // ---- Vigilant Eyes -----------------------------------------------------------------------------------------------------------------------------
    private static void VigilantEyesBasics()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, VigilantEyes, 0, Zone.Hand);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Samurai"); Force(samurai, 2);          // Force 5
        var bowed = Kw(Add(s, "training-crane-veteran", 0, Zone.Home, bowed: true), "Samurai");
        var nonSamurai = Add(s, "training-crane-courtier", 0, Zone.Home);
        var target = Add(s, "training-lion-veteran", 1, Zone.Home);                                                // Force 6
        var own = Add(s, "training-crane-dignitary", 0, Zone.Home);                                                // Force 3: "a Personality" may be mine
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "vigilant-eyes-penalty", card.Id, bowed.Id).Any(o => o.Enabled) && !Options(e, 0, "vigilant-eyes-penalty", card.Id, nonSamurai.Id).Any(o => o.Enabled), "an unbowed Samurai performs");
        RejectedWithoutChange(e, Cmd(e, 0, "vigilant-eyes-penalty", card, bowed.Id, target.Id)); RejectedWithoutChange(e, Cmd(e, 0, "vigilant-eyes-penalty", card, nonSamurai.Id, target.Id));
        var offer = Options(e, 0, "vigilant-eyes-penalty", card.Id, samurai.Id).Single(o => o.Enabled);
        Check(offer.TargetIds!.Contains(target.Id) && offer.TargetIds.Contains(own.Id), "any Personality is a target");
        var force = e.CardForceForTest(target.Id);
        Accept(e, offer.Command! with { TargetId = target.Id });
        Eq(e.CardForceForTest(target.Id), force - 5, "a Force penalty equal to the Samurai's Force (5)"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);
        // The penalty is a Force modifier.
        Check(e.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -5), "an ordinary Force modifier: it ends with the turn like every effect that names no duration");
        // It cannot take a Personality's Force below 0.
        var f = MatchEngine.FromTestState(s);
        Accept(f, Options(f, 0, "vigilant-eyes-penalty", card.Id, samurai.Id).Single(o => o.Enabled).Command! with { TargetId = own.Id });
        Eq(f.CardForceForTest(own.Id), 0, "Force does not go below 0");
        // Open only: not in a battle at all (it is not a Battle action).
        var t = Table(Phase.Battle, 0); var c2 = Add(t, VigilantEyes, 0, Zone.Hand); Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai"); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "vigilant-eyes-penalty", c2.Id), "an Open action is not taken in a battle");
    }
}
