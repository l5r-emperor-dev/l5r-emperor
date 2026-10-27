using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #417 (stage 3 of Mendon's rulings of 2026-09-21): the after-resolution window -- after a battle's resolution destroyed the losing units and the province and settled Honor, while
// the winners are still at the battlefield, either player may play the Reactions that name it -- and the cards built on it: Never Safe (returns from a discard pile), Redoubled Attack
// (as many copies as are held; the units straighten and will not bow), Tales of Dishonor (the loser, or both players in a tie, loses 4 Honor); plus Quagmire, Hizumi's reaction for a Region.
internal static class AfterResolutionTests
{
    private const string NeverSafe = "oracle-l5r-5535", Redoubled = "oracle-l5r-6222", Tales = "oracle-l5r-7753", Quagmire = "oracle-l5r-6123";

    public static void Run(TestRunner runner) => runner.Run("after-resolution", [
        ("Published shape: Never Safe, Redoubled Attack, Tales of Dishonor and Quagmire are the printed cards with their abilities and art", PublishedShapes),
        ("The window opens only when a player holds a Reaction that could be played now: with none, the battle resolves and the winners go home bowed exactly as before", NoWindowWithoutReactions),
        ("Redoubled Attack: each copy in a hand is its own play, one or two Personalities at the battlefield straighten with their units and do not bow when the winners return home; a player holding none is not offered one", RedoubledAttack),
        ("Tales of Dishonor: after a resolution in which its player destroyed units or a province the battle's loser loses 4 Honor -- both players in a tie -- and it is not offered when nothing was destroyed", TalesOfDishonor),
        ("Tales of Dishonor's Battle action straightens a performing Magistrate", TalesStraightens),
        ("Never Safe: -4F for the turn, then in the after-resolution window the card returns from the discard pile to its player's hand -- only for that battle, only to the player who played it, and it can be played again", NeverSafeFlow),
        ("The after-resolution Reactions are on offer in that window only and to their own players only", OnlyInTheWindow),
        ("Quagmire (a Region): after an action bowed a Personality at a battlefield at its province or an adjacent one, bowing it negates his next straightening this turn; not for a farther battlefield, a bowed Region or its Attacker", QuagmireFlow),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    // Passes for whoever holds priority until the after-resolution window opens or the battle is over.
    private static void PassToResolution(MatchEngine e)
    {
        for (var i = 0; i < 40 && e.State.PendingChoice is not { Type: "afterResolutionReaction" }; i++)
        {
            if (e.State.Phase != Phase.Battle && e.State.Phase != Phase.BattleSelection) return;
            if (e.State.UnresolvedBattlefields.Count == 0 && e.State.CurrentBattleProvince is null) return;
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var pass = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, "nothing to pass with in " + e.State.Phase + " (" + e.State.PendingChoice?.Type + ")");
            Accept(e, pass!.Command!);
        }
    }

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "afterResolutionReaction" } && guard < 12; guard++)
        {
            var mover = e.State.PendingChoice.PlayerId == e.State.PriorityPlayerId ? e.State.PriorityPlayerId : e.State.PriorityPlayerId;
            Accept(e, e.QueryActions(mover).Single(a => a.Type == "declineReaction").Command!);
        }
    }

    private static bool Offered(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var ns = CardCatalog.Get(NeverSafe); var ra = CardCatalog.Get(Redoubled); var td = CardCatalog.Get(Tales); var q = CardCatalog.Get(Quagmire);
        Check(ns.Name == "Never Safe" && ns.Type == CardType.Strategy && ns.Keywords!.Contains("Ninja") && ns.Abilities!.SequenceEqual(new[] { "never-safe-penalty", "never-safe-return" }), "Never Safe");
        Check(ra.Name == "Redoubled Attack" && ra.Focus == 3 && ra.Abilities!.SequenceEqual(new[] { "redoubled-attack-straighten" }), "Redoubled Attack");
        Check(td.Name == "Tales of Dishonor" && td.Focus == 1 && td.Abilities!.SequenceEqual(new[] { "tales-of-dishonor-lose-honor", "tales-of-dishonor-straighten" }), "Tales of Dishonor");
        Check(q.Name == "Quagmire" && q.Type == CardType.Region && q.Keywords!.Contains("Swamp") && q.Abilities!.SequenceEqual(new[] { "quagmire-negate-straighten" }), "Quagmire");
        foreach (var id in new[] { NeverSafe, Redoubled, Tales, Quagmire })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Check(AbilityCatalog.All["never-safe-return"].FromDiscard && AbilityCatalog.All["never-safe-return"].Trigger == ReactionTriggers.AfterBattleResolution, "Never Safe's return is taken from a discard pile after the resolution");
        Check(AbilityCatalog.All["redoubled-attack-straighten"].Slots![0].Performing && AbilityCatalog.All["redoubled-attack-straighten"].Slots![0].Max == 2, "one or two performing Personalities");
    }

    // The winners: player 1 attacks province 0 with a and b (Force 4 and 4) against player 0's single defender (Force 2). Both attackers are bowed at the start.
    private static (MatchState S, CardInstance A, CardInstance B, CardInstance C, CardInstance D) Battle(bool attackersWin = true)
    {
        var s = Table(Phase.Battle, 1);
        var a = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var b = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var c = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var d = Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0);
        if (attackersWin) { Force(a, 5); Force(b, 5); Force(c, 5); } else Force(d, 30);
        return (s, a, b, c, d);
    }

    private static void NoWindowWithoutReactions()
    {
        var (s, a, b, c, d) = Battle();
        var e = MatchEngine.FromTestState(s);
        PassToResolution(e);
        Check(e.State.PendingChoice is not { Type: "afterResolutionReaction" }, "no Reaction is held: no window");
        Check(e.State.Cards[d.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the defender was destroyed");
        foreach (var id in new[] { a.Id, b.Id, c.Id }) Check(e.State.Cards[id].Zone == Zone.Home && e.State.Cards[id].Bowed, "the winners went home, bowed");
        Check(e.State.PendingAfterResolution is null, "and nothing is left pending");
    }

    private static void RedoubledAttack()
    {
        var (s, a, b, c, d) = Battle();
        var copy1 = Add(s, Redoubled, 1, Zone.Hand); var copy2 = Add(s, Redoubled, 1, Zone.Hand);
        a.Bowed = true; b.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        PassToResolution(e);
        Eq(e.State.PendingChoice?.Type, "afterResolutionReaction", "the window opens for the player holding Redoubled Attack");
        Eq(e.State.PriorityPlayerId, 1, "and the player who holds one has the first opportunity");
        Check(Offered(e, 1, "redoubled-attack-straighten"), "offered");
        Check(!Offered(e, 0, "redoubled-attack-straighten"), "not to the player holding none");
        Check(e.State.Cards[a.Id].Zone == Zone.Battlefield && e.State.Cards[a.Id].Bowed, "the winners are still at the battlefield, still bowed");
        // Three is too many, and a Personality who is not at the battlefield is not a performer.
        var elsewhere = Add(e.State, "training-lion-samurai", 1, Zone.Home);
        var first = Use(e, 1, "redoubled-attack-straighten", copy1.Id, null, null, Pick("units", a.Id, b.Id, c.Id));
        Reject(e, first);
        var farther = Edit(e, st => Add(st, "training-lion-samurai", 1, Zone.Home));
        Reject(farther, Use(farther, 1, "redoubled-attack-straighten", copy1.Id, null, null, Pick("units", farther.State.Cards.Values.Last().Id)));
        // Two performers: straightened, and marked as not bowing.
        Accept(e, Use(e, 1, "redoubled-attack-straighten", copy1.Id, null, null, Pick("units", a.Id, b.Id)));
        Check(!e.State.Cards[a.Id].Bowed && !e.State.Cards[b.Id].Bowed, "their units straightened");
        Check(e.State.ResolutionWillNotBowCardIds.Contains(a.Id) && e.State.ResolutionWillNotBowCardIds.Contains(b.Id) && !e.State.ResolutionWillNotBowCardIds.Contains(c.Id), "and they will not bow");
        Eq(e.State.Cards[copy1.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        // The second copy is a play of its own (only the player still holding it is offered it).
        var reopened = e.State.PendingChoice?.Type == "afterResolutionReaction";
        Check(reopened, "the window is still open");
        for (var i = 0; i < 4 && !Offered(e, 1, "redoubled-attack-straighten"); i++)
            if (e.State.PendingChoice?.Type == "afterResolutionReaction") Accept(e, e.QueryActions(e.State.PriorityPlayerId).Single(x => x.Type == "declineReaction").Command!);
        Check(Offered(e, 1, "redoubled-attack-straighten"), "the second copy is still on offer (each held copy is its own play)");
        Accept(e, Use(e, 1, "redoubled-attack-straighten", copy2.Id, null, null, Pick("units", c.Id)));
        ClosePassing(e);
        Eq(e.State.PendingChoice?.Type is "afterResolutionReaction", false, "both passed: the window closed");
        foreach (var id in new[] { a.Id, b.Id, c.Id }) Check(e.State.Cards[id].Zone == Zone.Home && !e.State.Cards[id].Bowed, "went home unbowed: " + id);
        Check(e.State.ResolutionWillNotBowCardIds.Count == 0, "the promise lapsed with the battle");

        // One copy for one of two: the other bows as usual.
        var (s2, a2, b2, c2, d2) = Battle();
        var only = Add(s2, Redoubled, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        PassToResolution(f);
        Accept(f, Use(f, 1, "redoubled-attack-straighten", only.Id, null, null, Pick("units", a2.Id)));
        ClosePassing(f);
        Check(!f.State.Cards[a2.Id].Bowed && f.State.Cards[b2.Id].Bowed && f.State.Cards[c2.Id].Bowed, "only the chosen Personality's unit keeps from bowing");
        Check(f.State.Cards[a2.Id].Zone == Zone.Home, "and he is home");

        // A defender holding one has nobody at the battlefield after losing: not offered.
        var (s3, _, _, _, _) = Battle();
        Add(s3, Redoubled, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(s3);
        PassToResolution(g);
        Check(g.State.PendingChoice is not { Type: "afterResolutionReaction" }, "a player with no Personality left at the battlefield is not asked");
    }

    private static void TalesOfDishonor()
    {
        // The attacker destroyed the defender: its Tales makes the defender (the loser) lose 4 Honor.
        var (s, a, b, c, d) = Battle();
        var tales = Add(s, Tales, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before0 = Honor(e, 0); var before1 = Honor(e, 1);
        PassToResolution(e);
        Eq(e.State.PendingChoice?.Type, "afterResolutionReaction", "the window opens for Tales of Dishonor");
        var afterBattle0 = Honor(e, 0); var afterBattle1 = Honor(e, 1);
        Accept(e, Enabled(e, 1, "tales-of-dishonor-lose-honor", tales.Id).Command!);
        Eq(Honor(e, 0), afterBattle0 - 4, "the loser lost 4 Honor");
        Eq(Honor(e, 1), afterBattle1, "the winner did not");
        ClosePassing(e);

        // Tie: both players are the battle's loser.
        var t = Table(Phase.Battle, 1);
        var ta = Add(t, "training-crane-samurai", 1, Zone.Battlefield, province: 0); var td = Add(t, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Force(ta, 10); Force(td, 10);
        ta.Modifiers.Add(new("test", "test", "Force", Def(td).Force - Def(ta).Force)); // equal Force on both sides
        var tie = Add(t, Tales, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        PassToResolution(f);
        Eq(f.State.PendingChoice?.Type, "afterResolutionReaction", "the tied battle opens the window");
        Check(f.State.PendingAfterResolution!.Losers.Count == 2, "both players are the battle's loser");
        var h0 = Honor(f, 0); var h1 = Honor(f, 1);
        Accept(f, Enabled(f, 1, "tales-of-dishonor-lose-honor", tie.Id).Command!);
        Eq(Honor(f, 0), h0 - 4, "one loser loses 4 Honor"); Eq(Honor(f, 1), h1 - 4, "and so does the other, its own player");

        // Units destroyed but the province stands (a narrow win): the units alone are enough.
        var w = Table(Phase.Battle, 1);
        var wa = Add(w, "training-crane-samurai", 1, Zone.Battlefield, province: 0); var wd = Add(w, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        wa.Modifiers.Add(new("test", "test", "Force", Def(wd).Force - Def(wa).Force + 1)); // one more Force than the defender: a win, but far from destroying the province
        Add(w, Tales, 1, Zone.Hand);
        var narrow = MatchEngine.FromTestState(w);
        PassToResolution(narrow);
        Check(narrow.State.PendingAfterResolution!.ProvinceDestroyedBy is null && narrow.State.PendingAfterResolution.DestroyedEnemyUnits[1] == 1, "the defender's unit was destroyed and the province was not");
        Check(Offered(narrow, 1, "tales-of-dishonor-lose-honor"), "Tales of Dishonor is offered for destroying units alone");

        // Nothing destroyed (no defenders, the province stands): not offered.
        var n = Table(Phase.Battle, 1);
        var lone = Add(n, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(n, Tales, 1, Zone.Hand);
        var g = MatchEngine.FromTestState(n);
        PassToResolution(g);
        Check(g.State.PendingChoice is not { Type: "afterResolutionReaction" } || !Offered(g, 1, "tales-of-dishonor-lose-honor"), "no units and no province destroyed: Tales is not offered");
        // The other player's Tales is judged by ITS player's destruction: the defender who destroyed nothing may not use it.
        var (s4, _, _, _, _) = Battle();
        Add(s4, Tales, 0, Zone.Hand);
        var k = MatchEngine.FromTestState(s4);
        PassToResolution(k);
        Check(k.State.PendingChoice is not { Type: "afterResolutionReaction" } || !Offered(k, 0, "tales-of-dishonor-lose-honor"), "the defender destroyed nothing: no Tales for him");
    }

    private static void TalesStraightens()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var magistrate = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true); GiveKeyword(magistrate, "Magistrate");
        var other = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Tales, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 1, "tales-of-dishonor-straighten", card.Id, magistrate.Id);
        Check(!Options(e, 1, "tales-of-dishonor-straighten", card.Id, other.Id).Any(o => o.Enabled), "only a Magistrate may perform it");
        Accept(e, option.Command!);
        Check(!e.State.Cards[magistrate.Id].Bowed && e.State.Cards[other.Id].Bowed, "the Magistrate is straightened, nobody else");
    }

    private static void NeverSafeFlow()
    {
        var (s, a, b, c, d) = Battle();
        GiveKeyword(a, "Ninja"); Force(d, 8);
        var card = Add(s, NeverSafe, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var force = e.GetView(1).Players.SelectMany(p => p.Army).Single(x => x.Id == d.Id).Force;
        Accept(e, Offer(e, 1, "never-safe-penalty", card.Id, d.Id, a.Id));
        Eq(e.GetView(1).Players.SelectMany(p => p.Army).Single(x => x.Id == d.Id).Force, force - 4, "-4F on the target");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is in the discard pile");
        Eq(e.State.Cards[card.Id].ReturnAfterBattle, e.State.BattleNumber, "remembering the battle it was played in");
        // The rest of the battle passes; the window offers the return to the player who played it.
        PassToResolution(e);
        Eq(e.State.PendingChoice?.Type, "afterResolutionReaction", "the window opens for a card in a discard pile");
        Check(Offered(e, 1, "never-safe-return") && !Offered(e, 0, "never-safe-return"), "offered to the player who played it, not the other");
        Check(!Offered(e, 1, "never-safe-penalty"), "and its Battle action is not offered in the window");
        var returnCommand = e.QueryActions(1).Single(x => x.Type == "useAbility" && x.Command?.AbilityId == "never-safe-return").Command!;
        Accept(e, returnCommand);
        Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "it is back in the hand (not discarded again as if just played)");
        Check(e.State.Players[1].Hand.Contains(card.Id) && e.State.Cards[card.Id].ReturnAfterBattle == -1, "in the player's hand list, with nothing left to return");
        ClosePassing(e);
        // It can be played again (a later battle).
        Check(e.State.Cards[card.Id].AbilityUses.Count == 0, "with fresh uses");

        // Only for the battle it was played in: another battle number, and it is not offered.
        var (s2, a2, _, _, d2) = Battle();
        GiveKeyword(a2, "Ninja");
        var old = Add(s2, NeverSafe, 1, Zone.FateDiscard); old.ReturnAfterBattle = s2.BattleNumber - 1;
        var f = MatchEngine.FromTestState(s2);
        PassToResolution(f);
        Check(f.State.PendingChoice is not { Type: "afterResolutionReaction" }, "a Never Safe played in an earlier battle is not offered: no window");
        // Not in the discard pile any more: not offered.
        var (s3, a3, _, _, _) = Battle();
        var gone = Add(s3, NeverSafe, 1, Zone.RemovedFromGame); gone.ReturnAfterBattle = s3.BattleNumber;
        var g = MatchEngine.FromTestState(s3);
        PassToResolution(g);
        Check(g.State.PendingChoice is not { Type: "afterResolutionReaction" }, "a card that is no longer in the discard pile is not offered");
        // The performer must be a Ninja; the target an enemy Personality or Follower.
        var (s4, a4, _, _, d4) = Battle();
        var ns = Add(s4, NeverSafe, 1, Zone.Hand);
        var ownFollower = Add(s4, "training-follower", 1, Zone.Attached, province: 0, parent: a4.Id);
        var enemyFollower = Add(s4, "training-follower", 0, Zone.Attached, province: 0, parent: d4.Id);
        var h = MatchEngine.FromTestState(s4);
        Check(!Options(h, 1, "never-safe-penalty", ns.Id).Any(o => o.Enabled), "no Ninja performing: not playable");
        var withNinja = Edit(h, st => GiveKeyword(st.Cards[a4.Id], "Ninja"));
        var targets = Enabled(withNinja, 1, "never-safe-penalty", ns.Id, a4.Id).TargetIds!;
        Check(targets.Contains(d4.Id) && targets.Contains(enemyFollower.Id) && !targets.Contains(ownFollower.Id) && !targets.Contains(a4.Id), "an enemy Personality or Follower, nobody of its own player's");
    }

    private static void OnlyInTheWindow()
    {
        // In the Action phase, or in the Battle phase before resolution, no after-resolution Reaction is offered.
        var s = Table(Phase.Battle, 1);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, Redoubled, 1, Zone.Hand); Add(s, Tales, 1, Zone.Hand);
        var neverSafe = Add(s, NeverSafe, 1, Zone.FateDiscard); neverSafe.ReturnAfterBattle = s.BattleNumber;
        var e = MatchEngine.FromTestState(s);
        foreach (var id in new[] { "redoubled-attack-straighten", "tales-of-dishonor-lose-honor", "never-safe-return" })
        {
            Check(!Offered(e, 1, id), $"{id} is not on offer during the battle");
            Check(Options(e, 1, id).Length == 0, $"{id} is not even listed (disabled) during the battle");
        }
        var t = Table(Phase.Action, 1);
        Add(t, Redoubled, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Check(!Offered(f, 1, "redoubled-attack-straighten"), "nor in the Action phase");
        Check(Options(f, 1, "redoubled-attack-straighten").Length == 0, "where it is not even listed as a disabled option");
        Reject(f, new GameCommand("useAbility", 1, f.Version, f.State.Cards.Values.Single(c => c.DefinitionId == Redoubled).Id, AbilityId: "redoubled-attack-straighten"));
    }

    private static void QuagmireFlow()
    {
        (MatchEngine E, string Victim, string Bower) Setup(int regionProvince, int regionOwner = 0, bool regionBowed = false)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            Add(s, Quagmire, regionOwner, Zone.Region, bowed: regionBowed, province: regionProvince);
            return (MatchEngine.FromTestState(s), victim.Id, bower.Id);
        }
        // An adjacent province (1): the printed range still names it, but Mendon's presence ruling (issue #1133) makes a Region present only at a battle on its own province, so in a battle at province 0 a Region at province 1 offers nothing.
        {
            var (e, victim, bower) = Setup(1);
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower, victim));
            // NOTE (#1133 follow-up): OpenBowReactionWindow/RegionIsNear still open the window for the adjacent Region; only the use is refused. Pinned here as "not usable", not as "no window".
            Check(!e.QueryActions(0).Any(x => x.Type == "useAbility" && x.Command?.AbilityId == "quagmire-negate-straighten" && x.Enabled), "the adjacent Region is not at this battle: its Reaction is not usable");
            Check(e.State.Cards[victim].Bowed && e.State.Cards[victim].StraightenBlockedTurn != e.State.TurnNumber, "he stays bowed and nothing is negated");
        }
        // Its own province (0): the reaction is offered after the action bowed the Defender's Personality.
        foreach (var province in new[] { 0 })
        {
            var (e, victim, bower) = Setup(province);
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower, victim));
            Eq(e.State.PendingChoice?.Type, "bowReaction", $"the Region at province {province} opens the reaction window");
            Check(e.State.Cards[victim].Bowed, "he was bowed");
            var option = e.QueryActions(0).Single(x => x.Type == "useAbility" && x.Command?.AbilityId == "quagmire-negate-straighten" && x.Enabled);
            Accept(e, option.Command! with { TargetId = victim });
            Eq(e.State.Cards[victim].StraightenBlockedTurn, e.State.TurnNumber, "his next straightening this turn is negated");
            Check(e.State.Cards.Values.Single(c => c.DefinitionId == Quagmire).Bowed, "and the Region bowed");
        }
        // Two provinces away, a bowed Region, or the Attacker's Region: no window.
        foreach (var (province, owner, bowed, why) in new[] { (2, 0, false, "two provinces away"), (0, 0, true, "a bowed Region"), (0, 1, false, "the Attacker's Region") })
        {
            var (e, victim, bower) = Setup(province, owner, bowed);
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower, victim));
            Check(e.State.PendingChoice is not { Type: "bowReaction" }, "no window for " + why);
            Check(e.State.Cards[victim].Bowed && e.State.Cards[victim].StraightenBlockedTurn != e.State.TurnNumber, "he stays bowed and nothing is negated");
        }
    }
}
