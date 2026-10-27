using System.Text.Json;
using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Move to a destination the ability's own rule defines (issue #336): Sea Troll Predator moves its target AND itself to the same adjacent unresolved
// battlefield -- an enemy card sent somewhere other than home, and the source going with it, are both new. The destination is data on the ability
// (home / current battlefield / adjacent unresolved / another unresolved), the engine lists the legal battlefields in ActionOption.Provinces (the
// province indices the assignment-with-arrows flow already draws) and the command names one in Province. The other rules and mover shapes are
// proven on synthetic fixtures. Readings and alternatives: docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md.
internal static class MoveDestinationTests
{
    private const string Predator = "oracle-l5r-6522", Move = "predator-move";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Sea Troll Predator is the printed card (Force 6, Chi 4, no Honor Requirement, Cost 8, Personal Honor 0, Unaligned Shadowlands Nonhuman Troll Earth Tactician), whole -- EntryHonorLoss 3 and one Battle move on the adjacent-unresolved rule for source and target together", PublishedShape),
            ("Predator moves the Personality and himself to the same adjacent unresolved battlefield, whichever the player chooses -- both arrive with their attachments, neither is left at the battlefield they fought at, and nothing else moves", MovesBothToTheChosenBattlefield),
            ("Which battlefields are legal: adjacent to the current one along the DEFENDER's province line (destroyed provinces slide together), unresolved (not one already fought, not one nobody attacked), never the current one -- and never the attacker's own province line", LegalDestinationRules),
            ("Refused, atomically: a destination that is not listed (the current battlefield, a far one, a resolved one, none, out of range), a target that is not an enemy Personality at the current battlefield, a Predator who is not there; with no legal destination the action is offered disabled", Validation),
            ("Predator used by the Defender: the same adjacency (the defender's province line decides it, not whose action it is), and the same result", DefenderUsesPredator),
            ("Predator's two movements are separate effects (Mendon's ruling): negate Predator's own move and the target still moves; negate the target's (Tamori Tamoko's reaction, or a mover-negating reaction) and Predator still moves; both negated, neither moves; the WHOLE action negated, neither moves", EachMoveNegatedSeparately),
            ("The battle state follows: the current battle resolves with what remains at it, the moved units fight the battle at their new battlefield with their Force counted there, and everyone still ends the Attack Phase home", BattleStateFollowsTheMove),
            ("Every destination rule and mover shape on fixtures: current battlefield (target from home), another unresolved battlefield (target), adjacent unresolved (source alone, or performer and target), home (source and target together)", EveryRuleAndMover),
            ("Predator is NOT an Absent Battle action (#363's MovesToBattle, decided here): his text needs him at the current battlefield opposing the target and his move takes units OUT of that battle to an adjacent one, so he keeps the ordinary presence rule -- refused from home and from another battlefield, offered with no unit of the acting player's needed beyond his own; a move whose destination IS the current battlefield is MovesToBattle and needs no presence", PredatorIsNotAbsent),
            ("A Reaction played in a per-target window that opens a window of its own (#359 SuspendedWindows) hands the first window back as it was, with its target: Predator's move, a mover-negating Reaction naming Predator, and the Predator owner's whole-action negation of that Reaction", NestedWindowKeepsItsTarget),
            ("Adjacency between battlefields follows the DEFENDER's province line on every call site -- Predator's destinations, 'a Personality at an adjacent battlefield' (Battle, target) and Moto Gunso's boost -- whichever player owns the card or takes the action, with the two players' lines made to differ", AdjacencyIsTheDefendersLine),
            ("The option's Provinces (the destinations to draw) and its command's Province (the choice) survive JSON, and every offered destination validates and resolves on a fresh engine", OfferedDestinationsValidate),
        ];
        runner.Run("move-destination-moveto", [("The destination rule of each move ability reaches the client (and survives JSON), so a drag can be offered without reading card text; actions that do not move cards report none", MoveToReported)]);
        runner.Run("move-destination", tests);
        runner.Run("move-destination-preview", [("Hawado exposes all destinations and only herself as mover; province Attacks are distinct", HawadoPreview)]);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // The Attack Phase with player 1 attacking and player 0 defending. The battle under way is at province `current`; `unresolved` are the battlefields still
    // to be fought (the current one included, as in the engine). Predator (player 1) and his target (player 0) stand at `current`, each with an Item.
    private sealed record Rig(MatchState S, CardInstance Predator, CardInstance Target, CardInstance PredatorItem, CardInstance TargetItem);
    private static Rig Map(int current = 1, int[]? unresolved = null, string predator = Predator, int predatorOwner = 1)
    {
        var s = Table(); s.ActivePlayerId = 1; s.PriorityPlayerId = predatorOwner; s.CurrentBattleProvince = current; s.UnresolvedBattlefields = (unresolved ?? [0, 1, 2]).ToList();
        var p = Add(s, predator, predatorOwner, Zone.Battlefield, province: current);
        var t = Add(s, "training-battle-bower", 1 - predatorOwner, Zone.Battlefield, province: current);
        var pi = Add(s, "training-item", predatorOwner, Zone.Attached, province: current, parent: p.Id);
        var ti = Add(s, "training-item", 1 - predatorOwner, Zone.Attached, province: current, parent: t.Id);
        return new(s, p, t, pi, ti);
    }

    private static int[] Destinations(MatchEngine e, Rig r, int player = 1) => Options(e, player, Move, r.Predator.Id).Single().Provinces ?? [];

    private static GameCommand MoveTo(MatchEngine e, Rig r, int province, int player = 1) => Offer(e, player, Move, r.Predator.Id, r.Target.Id) with { Province = province };

    private static void PublishedShape()
    {
        var p = CardCatalog.Get(Predator);
        Check(!p.ScenarioOnly && p.Type == CardType.Personality && p.Name == "Sea Troll Predator" && p.Clan == "Unaligned");
        Eq(p.Force, 6); Eq(p.Chi, 4); Eq(p.GoldCost, 8); Eq(p.PersonalHonor, 0); Check(p.HonorRequirement is null, "no Honor Requirement");
        foreach (var k in new[] { "Tactician", "Earth", "Nonhuman", "Shadowlands", "Troll" }) Check(p.Keywords!.Contains(k), k);
        Eq(p.Text, "After Predator enters play: Lose 3 Honor.\nBattle: Target a Personality opposing Predator at the current battlefield: Move the Personality and Predator to the same adjacent unresolved battlefield.");
        Eq(p.EntryHonorLoss, 3); Eq(p.Effect, "none"); Check(p.Abilities!.SequenceEqual(new[] { Move }));
        var a = AbilityCatalog.All[Move];
        Eq(a.Timing, "Battle"); Eq(a.Target, "enemy-personality"); Eq(a.Move, new MoveSpec(MoveSpec.SourceAndTarget, MoveSpec.AdjacentUnresolved));
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", Predator + ".jpg")), "the scan is shipped");
        Eq(p.ArtworkKey, $"res://Content/Published/{Predator}.jpg");
    }

    private static void MovesBothToTheChosenBattlefield()
    {
        foreach (var destination in new[] { 0, 2 })
        {
            var r = Map();
            var bystanderAtCurrent = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 1);
            var elsewhere = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: destination);
            var e = MatchEngine.FromTestState(r.S);
            var option = Enabled(e, 1, Move, r.Predator.Id);
            Check(option.Movement is { DefenderId: 0, TargetMoves: true } && option.Movement.FixedMoverIds.SequenceEqual([r.Predator.Id]), "Predator previews his own movement plus the selected target");
            Check(option.Provinces!.ToHashSet().SetEquals([0, 2]), "both adjacent unresolved battlefields are offered: " + string.Join(",", option.Provinces!));
            Check(option.TargetIds!.ToHashSet().SetEquals([r.Target.Id, bystanderAtCurrent.Id]), "the Personalities opposing him at the current battlefield");
            var bowedBefore = e.State.Cards[r.Predator.Id].Bowed;
            Accept(e, MoveTo(e, r, destination));
            foreach (var moved in new[] { r.Predator, r.Target, r.PredatorItem, r.TargetItem })
            {
                Eq(e.State.Cards[moved.Id].ProvinceIndex, destination, $"{moved.Id} is at the chosen battlefield");
                Check(e.State.Cards[moved.Id].Zone == (moved.Zone == Zone.Attached ? Zone.Attached : Zone.Battlefield), $"{moved.Id} keeps its zone");
            }
            Eq(e.State.Cards[r.Target.Id].OwnerId, 0, "his controller is unchanged");
            Check(e.State.Cards[bystanderAtCurrent.Id].ProvinceIndex == 1 && e.State.Cards[elsewhere.Id].ProvinceIndex == destination, "no other card moved");
            Eq(e.State.Cards[r.Predator.Id].Bowed, bowedBefore, "moving bows nobody");
            Check(e.State.Events.Count(x => x.Type == "unitMovedToBattlefield") == 2, "both movements are in the public log");
            Eq(e.State.CurrentBattleProvince, 1, "the battle in progress is still the current one");
            Eq(e.State.PriorityPlayerId, 0, "priority passed on");
            Check(!e.GetView(0).Players[1].Army.Any(c => c.Id == r.Predator.Id && c.ProvinceIndex == 1), "and the view shows him gone from it");
        }
    }

    private static void LegalDestinationRules()
    {
        // (1) The line: 0-1-2-3 with the current battlefield at 1 gives {0, 2}, though 3 is unresolved too.
        var line = Map(unresolved: [0, 1, 2, 3]);
        Check(Destinations(MatchEngine.FromTestState(line.S), line).ToHashSet().SetEquals([0, 2]), "adjacent only: 0 and 2, not 3");
        // (2) Already fought: an adjacent battlefield that is no longer unresolved is not offered.
        var fought = Map(unresolved: [1, 2]);
        Check(Destinations(MatchEngine.FromTestState(fought.S), fought).SequenceEqual(new[] { 2 }), "province 0 was fought already");
        // (3) Nobody attacked it: an adjacent province that is not a battlefield at all is not offered.
        var nobody = Map(unresolved: [0, 1]);
        Check(Destinations(MatchEngine.FromTestState(nobody.S), nobody).SequenceEqual(new[] { 0 }), "province 2 is not a battlefield");
        // (4) A destroyed province of the DEFENDER slides its neighbours together: with province 2 gone, 1 is next to 3.
        var slid = Map(unresolved: [0, 1, 3]); slid.S.Players[0].Provinces[2].Destroyed = true;
        Check(Destinations(MatchEngine.FromTestState(slid.S), slid).ToHashSet().SetEquals([0, 3]), "0 and 3: the destroyed province is skipped");
        // (5) ...but the ATTACKER's own destroyed provinces are another line and change nothing.
        var mine = Map(unresolved: [0, 1, 2]); mine.S.Players[1].Provinces[2].Destroyed = true; mine.S.Players[1].Provinces[0].Destroyed = true;
        Check(Destinations(MatchEngine.FromTestState(mine.S), mine).ToHashSet().SetEquals([0, 2]), "the attacker's own province line is not the one that decides adjacency");
        // (6) The ends of the line have one neighbour, and a battlefield next to only resolved ones has none.
        var end = Map(current: 0, unresolved: [0, 1, 3]);
        Check(Destinations(MatchEngine.FromTestState(end.S), end).SequenceEqual(new[] { 1 }), "province 0 is next to 1 only");
        var alone = Map(unresolved: [1]);
        var e = MatchEngine.FromTestState(alone.S);
        var listed = Options(e, 1, Move, alone.Predator.Id).Single();
        Check(!listed.Enabled && listed.Provinces is { Length: 0 } && listed.Reason is not null, "no legal destination: offered disabled, with the reason and nothing to draw: " + listed.Reason);
        // (7) The current battlefield is never a destination, whatever the rest.
        Check(!Destinations(MatchEngine.FromTestState(line.S), line).Contains(1), "not the battlefield he is at");
    }

    private static void Validation()
    {
        var r = Map(unresolved: [0, 1, 2, 3]);
        var ownAtBattle = Add(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 1);
        var enemyAway = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var enemyFollower = Add(r.S, "training-follower", 0, Zone.Attached, province: 1, parent: r.Target.Id);
        var e = MatchEngine.FromTestState(r.S);
        GameCommand Cmd(string? target, int? province, string? source = null) => new("useAbility", 1, e.Version, source ?? r.Predator.Id, AbilityId: Move, TargetId: target, Province: province);
        Accept(MatchEngine.FromTestState(r.S), Cmd(r.Target.Id, 0) with { ExpectedVersion = MatchEngine.FromTestState(r.S).Version });
        foreach (var bad in new int?[] { null, 1, 3, -1, 4, 99 }) Reject(e, Cmd(r.Target.Id, bad));
        Reject(e, Cmd(ownAtBattle.Id, 0)); Reject(e, Cmd(enemyAway.Id, 0)); Reject(e, Cmd(enemyFollower.Id, 0)); Reject(e, Cmd(r.TargetItem.Id, 0)); Reject(e, Cmd(null, 0));
        Reject(e, Cmd(r.Predator.Id, 0));
        // A resolved battlefield: the same command is refused once province 0 has been fought.
        var fought = Map(unresolved: [1, 2]);
        Reject(MatchEngine.FromTestState(fought.S), new GameCommand("useAbility", 1, MatchEngine.FromTestState(fought.S).Version, fought.Predator.Id, AbilityId: Move, TargetId: fought.Target.Id, Province: 0));
        // Predator must be at the current battlefield to act at all.
        var away = Map(); away.S.Cards[away.Predator.Id].ProvinceIndex = 0;
        var eAway = MatchEngine.FromTestState(away.S);
        Check(Options(eAway, 1, Move, away.Predator.Id).All(o => !o.Enabled), "not offered from another battlefield");
        Reject(eAway, new GameCommand("useAbility", 1, eAway.Version, away.Predator.Id, AbilityId: Move, TargetId: away.Target.Id, Province: 2));
        // Once a turn.
        var used = Map(); var e2 = MatchEngine.FromTestState(used.S);
        Accept(e2, MoveTo(e2, used, 2));
        e2 = Edit(e2, s => { s.PriorityPlayerId = 1; s.CurrentBattleProvince = 2; });
        Check(Options(e2, 1, Move, used.Predator.Id).All(o => !o.Enabled), "one use a turn");
    }

    private static void DefenderUsesPredator()
    {
        // Predator (player 0, the Defender) and an attacking target (player 1), current battlefield 1, the defender's province 2 destroyed.
        var r = Map(current: 1, unresolved: [0, 1, 3], predatorOwner: 0); r.S.Players[0].Provinces[2].Destroyed = true;
        var e = MatchEngine.FromTestState(r.S);
        Check(Destinations(e, r, player: 0).ToHashSet().SetEquals([0, 3]), "the Defender's own province line: 2 is gone, so 3 is next to 1");
        Accept(e, MoveTo(e, r, 3, player: 0));
        Check(new[] { r.Predator, r.Target, r.PredatorItem, r.TargetItem }.All(c => e.State.Cards[c.Id].ProvinceIndex == 3), "the defending Predator and the attacking Personality arrive together");
        Eq(e.State.Cards[r.Target.Id].OwnerId, 1, "and the Attacker still controls his");
    }

    // ---- negation of the two movements (Mendon's ruling, issue #336) -----------------------------------------------------------------
    // "Move the Personality and Predator": each is its own movement, so negating one leaves the other, unless an effect also negates it or the whole
    // action is negated. Player 0 (the target's controller) holds the reactions; the window opens for the action's target.
    private static void Decline(MatchEngine e) => Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
    private static void CloseWindows(MatchEngine e) { for (var guard = 0; guard < 10 && e.State.PendingChoice is { Type: "beforeActionReaction" }; guard++) Decline(e); Check(e.State.PendingChoice is null, "the windows closed"); }
    private static GameCommand NegateMover(MatchEngine e, string mover)
    {
        var option = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-mover" && a.Command.TargetId == mover);
        Check(option is not null, $"a mover-negating reaction naming {mover} is offered: " + string.Join("; ", e.QueryActions(0).Select(a => $"{a.Type} {a.Command?.AbilityId} {a.Command?.TargetId} [{a.Enabled}]")));
        return option!.Command!;
    }

    private static void EachMoveNegatedSeparately()
    {
        // (1) Predator's own movement negated: the target still moves, Predator and his Item stay.
        var r = Map(); Add(r.S, "training-mover-negator", 0, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, MoveTo(e, r, 2));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the negator opens the before-action window");
        Accept(e, NegateMover(e, r.Predator.Id)); CloseWindows(e);
        Eq(e.State.Cards[r.Predator.Id].ProvinceIndex, 1, "Predator stayed"); Eq(e.State.Cards[r.PredatorItem.Id].ProvinceIndex, 1, "with his Item");
        Eq(e.State.Cards[r.Target.Id].ProvinceIndex, 2, "the target moved on his own"); Eq(e.State.Cards[r.TargetItem.Id].ProvinceIndex, 2);
        Check(e.State.Events.Any(x => x.Type == "moveNegated"), "the negation is in the log");
        Check(e.State.Cards[r.Predator.Id].AbilityUses.ContainsKey(Move), "the action was still taken");
        // (2) The target's movement negated by a real card (Tamori Tamoko's reaction): only the target stays, Predator goes.
        var r2 = Map(); Add(r2.S, "training-tamoko", 0, Zone.Battlefield, province: 1);
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, MoveTo(e2, r2, 2));
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "tamoko-negate").Command!); CloseWindows(e2);
        Eq(e2.State.Cards[r2.Target.Id].ProvinceIndex, 1, "the target stayed"); Eq(e2.State.Cards[r2.TargetItem.Id].ProvinceIndex, 1, "and so did his Item");
        Eq(e2.State.Cards[r2.Predator.Id].ProvinceIndex, 2, "Predator went on his own"); Eq(e2.State.Cards[r2.PredatorItem.Id].ProvinceIndex, 2);
        // (2b) ...and the same by naming the target through the mover-negating fixture.
        var r2b = Map(); Add(r2b.S, "training-mover-negator", 0, Zone.Battlefield, province: 1);
        var e2b = MatchEngine.FromTestState(r2b.S);
        Accept(e2b, MoveTo(e2b, r2b, 0)); Accept(e2b, NegateMover(e2b, r2b.Target.Id)); CloseWindows(e2b);
        Check(e2b.State.Cards[r2b.Target.Id].ProvinceIndex == 1 && e2b.State.Cards[r2b.Predator.Id].ProvinceIndex == 0, "the target stayed, Predator went to the chosen battlefield");
        // (3) Both negated by two effects: neither moves (the action itself is not negated -- it was taken).
        var r3 = Map(); Add(r3.S, "training-mover-negator", 0, Zone.Battlefield, province: 1); Add(r3.S, "training-tamoko", 0, Zone.Battlefield, province: 1);
        var e3 = MatchEngine.FromTestState(r3.S);
        Accept(e3, MoveTo(e3, r3, 2));
        Accept(e3, NegateMover(e3, r3.Predator.Id));
        Decline(e3); // a reaction hands the opportunity over: player 1 passes, and player 0 plays the second
        Accept(e3, e3.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "tamoko-negate").Command!); CloseWindows(e3);
        Check(new[] { r3.Predator, r3.Target, r3.PredatorItem, r3.TargetItem }.All(c => e3.State.Cards[c.Id].ProvinceIndex == 1), "both negated: nothing moved");
        Eq(e3.State.Events.Count(x => x.Type == "moveNegated"), 2, "two negations logged");
        // (4) The whole action negated: neither moves.
        var r4 = Map(); Add(r4.S, "training-action-negator", 0, Zone.Battlefield, province: 1);
        var e4 = MatchEngine.FromTestState(r4.S);
        Accept(e4, MoveTo(e4, r4, 2));
        Accept(e4, e4.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-action").Command!); CloseWindows(e4);
        Check(new[] { r4.Predator, r4.Target, r4.PredatorItem, r4.TargetItem }.All(c => e4.State.Cards[c.Id].ProvinceIndex == 1), "the action negated whole: neither moved");
        Check(e4.State.Cards[r4.Predator.Id].AbilityUses.ContainsKey(Move), "it was still taken");
        // (5) No reaction played: both move (the baseline the cases above differ from).
        var r5 = Map(); Add(r5.S, "training-mover-negator", 0, Zone.Battlefield, province: 1);
        var e5 = MatchEngine.FromTestState(r5.S);
        Accept(e5, MoveTo(e5, r5, 2)); CloseWindows(e5);
        Check(new[] { r5.Predator, r5.Target }.All(c => e5.State.Cards[c.Id].ProvinceIndex == 2), "a window passed unused: both moved");
    }

    // ---- Predator and #363's MovesToBattle ---------------------------------------------------------------------------------------------------
    private static void PredatorIsNotAbsent()
    {
        Check(!AbilityCatalog.All[Move].MovesToBattle, "Predator's ability is not marked MovesToBattle");
        Check(AbilityCatalog.All["training-move-current"].MovesToBattle, "a move whose destination is the current battlefield is");
        foreach (var id in new[] { "training-move-anywhere", "training-move-source-adjacent", "training-move-both-home", Move })
            Check(!AbilityCatalog.All[id].MovesToBattle, $"{id}: a move between battlefields or home is not a move to the battle");
        // Predator at home: he is not at the current battlefield, so nothing is offered enabled and a command sent anyway is refused.
        var r = Map();
        r.S.Cards[r.Predator.Id].Zone = Zone.Home; r.S.Cards[r.Predator.Id].ProvinceIndex = null; r.S.Cards[r.PredatorItem.Id].ProvinceIndex = null;
        var e = MatchEngine.FromTestState(r.S);
        Check(Options(e, 1, Move, r.Predator.Id).All(o => !o.Enabled), "from home he is offered disabled");
        Reject(e, new GameCommand("useAbility", 1, e.Version, r.Predator.Id, AbilityId: Move, TargetId: r.Target.Id, Province: 2));
        // Another unit of his side at the battlefield does not stand in for him: the action is HIS presence and the source's own.
        var r2 = Map();
        r2.S.Cards[r2.Predator.Id].Zone = Zone.Home; r2.S.Cards[r2.Predator.Id].ProvinceIndex = null; r2.S.Cards[r2.PredatorItem.Id].ProvinceIndex = null;
        Add(r2.S, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var e2 = MatchEngine.FromTestState(r2.S);
        Check(Options(e2, 1, Move, r2.Predator.Id).All(o => !o.Enabled), "a friendly unit at the battlefield does not make Predator's action Absent either");
        // Present he needs nothing else: alone at the battlefield with the target (no other unit of his side) he is offered enabled.
        var r3 = Map();
        var e3 = MatchEngine.FromTestState(r3.S);
        Check(Options(e3, 1, Move, r3.Predator.Id).Any(o => o.Enabled), "present, he is offered enabled");
        // The contrast: a move whose destination IS the current battlefield needs no presence -- no unit of the acting player's anywhere, the source at home.
        var s = Table(); s.PriorityPlayerId = 1; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var source = Add(s, "training-move-source", 1, Zone.Home); var body = Add(s, "training-crane-samurai", 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(s);
        var offer = Options(e4, 1, "training-move-current", source.Id).FirstOrDefault(o => o.Enabled);
        Check(offer is not null, "a move to the current battlefield is offered with no unit at the battlefield: " + string.Join("; ", Options(e4, 1, "training-move-current", source.Id).Select(o => o.Reason)));
        Accept(e4, offer!.Command! with { TargetId = body.Id });
        Check(e4.State.Cards[body.Id].Zone == Zone.Battlefield && e4.State.Cards[body.Id].ProvinceIndex == 0, "and it brings the Personality in");
        // Whereas a move between battlefields, from the same position, keeps the ordinary presence rule.
        var s2 = Table(); s2.PriorityPlayerId = 1; s2.CurrentBattleProvince = 0; s2.UnresolvedBattlefields = [0, 1];
        var source2 = Add(s2, "training-move-source", 1, Zone.Home); Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s2);
        Check(Options(e5, 1, "training-move-anywhere", source2.Id).All(o => !o.Enabled), "a move to another battlefield needs the acting player at the current one");
    }

    // ---- a Reaction that opens a window of its own, over a per-target window ----------------------------------------------------------------------
    private static void NestedWindowKeepsItsTarget()
    {
        var r = Map();
        Add(r.S, "training-mover-negator", 0, Zone.Home);   // player 0 (the target's controller): a Reaction naming a card the action moves
        Add(r.S, "training-action-negator", 1, Zone.Home);  // player 1 (Predator's): after another player's action targets a card of his, negate its effects
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, MoveTo(e, r, 2));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction"); Eq(e.State.ReactionWindowTargetId, r.Target.Id, "the window is about Predator's target");
        // Player 0 plays his Reaction naming PREDATOR (a card of player 1's): a Reaction is an action, so it opens a window of its own for player 1's reactor.
        Accept(e, NegateMover(e, r.Predator.Id));
        Eq(e.State.SuspendedWindows.Count, 1, "the first window is set aside, not overwritten");
        Eq(e.State.SuspendedWindows[0].ReactionWindowTargetId, r.Target.Id, "with the target it was about");
        Eq(e.State.ReactionWindowTargetId, r.Predator.Id, "the new window is about Predator");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-action").Command!);
        Decline(e); Decline(e);
        Eq(e.State.SuspendedWindows.Count, 0, "the nested window closed and gave the first one back");
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the first window is open again"); Eq(e.State.ReactionWindowTargetId, r.Target.Id, "still about Predator's target");
        CloseWindows(e);
        Check(new[] { r.Predator, r.Target }.All(c => e.State.Cards[c.Id].ProvinceIndex == 2), "player 1 negated player 0's Reaction: nobody's movement was negated, both moved");
    }

    // ---- adjacency is the DEFENDER's line (Mendon's ruling, issue #336) -------------------------------------------------------------------
    // Player 1 attacks, player 0 defends. The two players' province lines are made to differ: whichever line the engine consults shows in the answer.
    // Current battlefield 0; province 1 of ONE player's line is destroyed, so on that line 0 and 2 are adjacent (and on the other 0 and 1 are).
    private static void AdjacencyIsTheDefendersLine()
    {
        MatchState Line(bool defenderLineSlid, bool attackerLineSlid)
        {
            var s = Table(); s.ActivePlayerId = 1; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0, 1, 2];
            if (defenderLineSlid) s.Players[0].Provinces[1].Destroyed = true;
            if (attackerLineSlid) s.Players[1].Provinces[1].Destroyed = true;
            return s;
        }
        // (1) 'A Personality at an adjacent battlefield' (Battle, target): the card is owned by the attacker (player 1) or the defender (player 0).
        bool Boost(bool defenderLineSlid, bool attackerLineSlid, int owner, int targetProvince)
        {
            var s = Line(defenderLineSlid, attackerLineSlid); s.PriorityPlayerId = owner;
            var source = Add(s, "training-adjacent-booster", owner, Zone.Battlefield, province: 0);
            var target = Add(s, "training-crane-samurai", 1 - owner, Zone.Battlefield, province: targetProvince);
            var e = MatchEngine.FromTestState(s);
            return e.Execute(new GameCommand("useAbility", owner, e.Version, CardId: source.Id, TargetId: target.Id, AbilityId: "training-adjacent-boost")).Accepted;
        }
        foreach (var owner in new[] { 1, 0 })
        {
            Check(Boost(defenderLineSlid: true, attackerLineSlid: false, owner, 2), $"owner {owner}: the defender's province 1 is destroyed, so battlefield 2 is next to 0 -- whoever owns the card");
            Check(!Boost(defenderLineSlid: false, attackerLineSlid: true, owner, 2), $"owner {owner}: the ATTACKER's destroyed province changes nothing: 2 is not next to 0");
            Check(Boost(defenderLineSlid: false, attackerLineSlid: true, owner, 1), $"owner {owner}: 1 is still next to 0");
        }
        // (2) Moto Gunso: +1F to each of his controller's other Followers at the current battlefield and each adjacent one.
        (int at1, int at2) Gunso(bool defenderLineSlid, bool attackerLineSlid, int owner)
        {
            var s = Line(defenderLineSlid, attackerLineSlid); s.PriorityPlayerId = owner;
            var host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
            var gunso = Add(s, "oracle-l5r-5320", owner, Zone.Attached, province: 0, parent: host.Id);
            var host1 = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 1); var f1 = Add(s, "training-follower", owner, Zone.Attached, province: 1, parent: host1.Id);
            var host2 = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 2); var f2 = Add(s, "training-follower", owner, Zone.Attached, province: 2, parent: host2.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", owner, e.Version, CardId: gunso.Id, AbilityId: "moto-gunso-boost"));
            int Boosted(CardInstance f) => e.State.Cards[f.Id].Modifiers.Count(m => m.Effect == "moto-gunso-boost");
            return (Boosted(f1), Boosted(f2));
        }
        foreach (var owner in new[] { 1, 0 })
        {
            Check(Gunso(true, false, owner) == (0, 1), $"owner {owner}: the defender's line slid -- the Follower at battlefield 2 is boosted, the one at 1 is not");
            Check(Gunso(false, true, owner) == (1, 0), $"owner {owner}: only the attacker's line slid -- the Follower at battlefield 1 is boosted, the one at 2 is not");
            Check(Gunso(false, false, owner) == (1, 0), $"owner {owner}: neither slid -- 1 only");
        }
        // (3) Predator: the same rule, on the same two lines (a Predator owned by the attacker or by the defender).
        foreach (var owner in new[] { 1, 0 })
        {
            var s = Line(defenderLineSlid: true, attackerLineSlid: false); s.PriorityPlayerId = owner;
            var predator = Add(s, Predator, owner, Zone.Battlefield, province: 0); Add(s, "training-battle-bower", 1 - owner, Zone.Battlefield, province: 0);
            Check(Options(MatchEngine.FromTestState(s), owner, Move, predator.Id).Single().Provinces!.SequenceEqual(new[] { 2 }), $"owner {owner}: Predator's only destination is 2 on the defender's slid line");
            var t = Line(defenderLineSlid: false, attackerLineSlid: true); t.PriorityPlayerId = owner;
            var predator2 = Add(t, Predator, owner, Zone.Battlefield, province: 0); Add(t, "training-battle-bower", 1 - owner, Zone.Battlefield, province: 0);
            Check(Options(MatchEngine.FromTestState(t), owner, Move, predator2.Id).Single().Provinces!.SequenceEqual(new[] { 1 }), $"owner {owner}: and 1 on the unslid defender's line, whatever the attacker's own line looks like");
        }
    }

    private static void BattleStateFollowsTheMove()
    {
        // Province 1 (current): Predator 6 and B 4 attack; T 3 and D 2 defend. Province 0: C 1 attacks, E 1 defends. Predator takes T to province 0.
        var r = Map();
        Force(r.Target, 1); // the bower's Force 2 + 1 = 3
        var b = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 1); // Force 4
        var d = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 1); // 2
        var c = Add(r.S, "training-battle-bower", 1, Zone.Battlefield, province: 0); Force(c, -1); // 1
        var ee = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 0); Force(ee, -1); // 1
        // Each Item adds 1 Force to its unit (training-item: +2), which moves with the unit.
        foreach (var unit in new[] { r.PredatorItem, r.TargetItem }) r.S.Cards[unit.Id].DefinitionId = "training-zero-cost-item";
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, MoveTo(e, r, 0));
        Eq(e.GetView(1).Players[1].Army.Where(x => x.ProvinceIndex == 1 && x.Definition!.Type == CardType.Personality).Count(), 1, "only B is left of the attackers at the current battlefield");
        // Both players pass: the current battle resolves with what remains at it.
        void Pass() { Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "pass" && a.Enabled).Command!); }
        Pass(); Pass();
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message == "Province 2: attacking Force 4, defending Force 2."), "province 2 was decided by B (4) against D (2) alone: " + string.Join(" | ", e.State.Events.Where(x => x.Type == "battleResolved").Select(x => x.Message)));
        Eq(e.State.Phase, Phase.BattleSelection);
        Check(e.State.UnresolvedBattlefields.OrderBy(x => x).SequenceEqual(new[] { 0, 2 }), "the fought battlefield is resolved; the moved units' battlefield is still to be fought");
        Check(e.State.Cards[r.Predator.Id].Zone == Zone.Battlefield && e.State.Cards[r.Predator.Id].ProvinceIndex == 0 && e.State.Cards[r.Target.Id].ProvinceIndex == 0, "and they are waiting there");
        // The battle at province 1 (index 0): Predator 6 + Item 1 + C 1 = 8 against T 3 + Item 1 + E 1 = 5.
        Accept(e, e.QueryActions(1).First(a => a.Type == "selectBattle" && a.Command!.Province == 0).Command!);
        Pass(); Pass();
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message == "Province 1: attacking Force 8, defending Force 5."), "Predator's 6 and the target's 3, with their Items (+1 each), count where they arrived: " + string.Join(" | ", e.State.Events.Where(x => x.Type == "battleResolved").Select(x => x.Message)));
    }

    private static void MoveToReported()
    {
        var s = Table(); s.PriorityPlayerId = 1; s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 3];
        var source = Add(s, "training-move-source", 1, Zone.Battlefield, province: 1); Add(s, "training-crane-samurai", 1, Zone.Home); Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        Eq(Enabled(e, 1, "training-move-current", source.Id).MoveTo, MoveSpec.CurrentBattlefield);
        Eq(Enabled(e, 1, "training-move-both-home", source.Id).MoveTo, MoveSpec.Home);
        Eq(Enabled(e, 1, "training-move-anywhere", source.Id).MoveTo, MoveSpec.AnyUnresolved);
        Eq(Enabled(e, 1, "training-move-source-adjacent", source.Id).MoveTo, MoveSpec.AdjacentUnresolved);
        var offered = Enabled(e, 1, "training-move-current", source.Id);
        Eq(JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(offered))!.MoveTo, MoveSpec.CurrentBattlefield);
        var actions = e.QueryActions(1);
        Check(actions.Where(a => a.Type != "useAbility").All(a => a.MoveTo is null), "an action that is not an ability reports no destination");
        Check(actions.Any(a => a.Type != "useAbility"), "the fixture offers actions that are not abilities (so the check above is not vacuous)");
    }

    private static void EveryRuleAndMover()
    {
        // current-battlefield: your own Personality at home is brought to the battlefield (the source is there, so no Absent is needed).
        var s = Table(); s.PriorityPlayerId = 1;
        var source = Add(s, "training-move-source", 1, Zone.Battlefield, province: 0); var homebody = Add(s, "training-crane-samurai", 1, Zone.Home); var homeItem = Add(s, "training-item", 1, Zone.Attached, parent: homebody.Id);
        var enemy = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var current = Enabled(e, 1, "training-move-current", source.Id);
        Check(current.Provinces is null && current.TargetIds!.Contains(homebody.Id) && !current.TargetIds.Contains(enemy.Id), "no battlefield to choose: nothing to draw; a Personality of yours at home is the target");
        Accept(e, current.Command! with { TargetId = homebody.Id });
        Check(e.State.Cards[homebody.Id].Zone == Zone.Battlefield && e.State.Cards[homebody.Id].ProvinceIndex == 0 && e.State.Cards[homeItem.Id].ProvinceIndex == 0, "he and his Item are at the current battlefield");
        // unresolved-battlefield: another one, chosen; an enemy target.
        var s2 = Table(); s2.PriorityPlayerId = 1; s2.CurrentBattleProvince = 1; s2.UnresolvedBattlefields = [0, 1, 3];
        var src2 = Add(s2, "training-move-source", 1, Zone.Battlefield, province: 1); var tgt2 = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e2 = MatchEngine.FromTestState(s2);
        var any = Enabled(e2, 1, "training-move-anywhere", src2.Id);
        Check(any.Movement is { TargetMoves: true, FixedMoverIds.Length: 0 }, "A target-only move does not preview the source moving");
        Check(any.Provinces!.ToHashSet().SetEquals([0, 3]), "every OTHER unresolved battlefield, adjacent or not: " + string.Join(",", any.Provinces!));
        Reject(e2, any.Command! with { TargetId = tgt2.Id, Province = 1 });
        Accept(e2, any.Command! with { TargetId = tgt2.Id, Province = 3 });
        Check(e2.State.Cards[tgt2.Id].ProvinceIndex == 3 && e2.State.Cards[src2.Id].ProvinceIndex == 1, "only the target moved");
        // adjacent-unresolved, the source alone (no target at all).
        var s3 = Table(); s3.PriorityPlayerId = 1; s3.CurrentBattleProvince = 1; s3.UnresolvedBattlefields = [0, 1, 2];
        var src3 = Add(s3, "training-move-source", 1, Zone.Battlefield, province: 1); var stay = Add(s3, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e3 = MatchEngine.FromTestState(s3);
        var self = Enabled(e3, 1, "training-move-source-adjacent", src3.Id);
        Check(self.Movement is { TargetMoves: false } && self.Movement.FixedMoverIds.SequenceEqual([src3.Id]), "Source-only preview");
        Check(self.Provinces!.ToHashSet().SetEquals([0, 2]) && self.Command!.TargetId is null, "the source's own move needs no target");
        Accept(e3, self.Command! with { Province = 2 });
        Check(e3.State.Cards[src3.Id].ProvinceIndex == 2 && e3.State.Cards[stay.Id].ProvinceIndex == 1, "only Predator-shaped source moved");
        // home: source and target together, no battlefield named.
        var s4 = Table(); s4.PriorityPlayerId = 1;
        var src4 = Add(s4, "training-move-source", 1, Zone.Battlefield, province: 0); var tgt4 = Add(s4, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        var home = Enabled(e4, 1, "training-move-both-home", src4.Id);
        Check(home.Provinces is null, "home names no battlefield");
        Reject(e4, home.Command! with { TargetId = tgt4.Id, Province = 0 });
        Accept(e4, home.Command! with { TargetId = tgt4.Id });
        Check(e4.State.Cards[src4.Id].Zone == Zone.Home && e4.State.Cards[tgt4.Id].Zone == Zone.Home, "both went home");
        // performer-and-target: the card the ability names as its performer goes with the target -- and not the ability's own source.
        var s5 = Table(); s5.PriorityPlayerId = 1; s5.CurrentBattleProvince = 1; s5.UnresolvedBattlefields = [0, 1, 2];
        var src5 = Add(s5, "training-move-source", 1, Zone.Battlefield, province: 1); var perf5 = Add(s5, "training-crane-samurai", 1, Zone.Battlefield, province: 1);
        var tgt5 = Add(s5, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e5 = MatchEngine.FromTestState(s5);
        var pair = Enabled(e5, 1, "training-move-performer-and-target", src5.Id, perf5.Id);
        Check(pair.Movement is { TargetMoves: true } && pair.Movement.FixedMoverIds.SequenceEqual([perf5.Id]), "Performer moves, not the source of the ability");
        Check(pair.Provinces!.ToHashSet().SetEquals([0, 2]), "adjacent unresolved again");
        Accept(e5, pair.Command! with { TargetId = tgt5.Id, Province = 0 });
        Check(e5.State.Cards[perf5.Id].ProvinceIndex == 0 && e5.State.Cards[tgt5.Id].ProvinceIndex == 0 && e5.State.Cards[src5.Id].ProvinceIndex == 1, "the performer and the target moved, the source stayed");
    }

    private static void HawadoPreview()
    {
        var s = Table(); s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 2];
        var hawado = Add(s, "oracle-l5r-7103", 1, Zone.Home, bowed: true);
        var cost = Add(s, "training-lion-samurai", 0, Zone.DynastyDiscard);
        Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 2);
        var e = MatchEngine.FromTestState(s); var option = Enabled(e, 1, "hawado-move-battlefield", hawado.Id);
        Check(option.Provinces!.ToHashSet().SetEquals([0, 2]), "Hawado exposes every validated destination");
        Check(option.Movement is { DefenderId: 0, TargetMoves: false } && option.Movement.FixedMoverIds.SequenceEqual([hawado.Id]), "The removed Dynasty target is not a moving unit");
        Accept(e, option.Command! with { TargetId = cost.Id, Province = 2 });
        Check(e.State.Cards[hawado.Id].ProvinceIndex == 2 && !e.State.Cards[hawado.Id].Bowed && e.State.Cards[cost.Id].Zone == Zone.RemovedFromGame, "Existing movement, straightening and cost remain unchanged");
        foreach (var enemy in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield)) { enemy.Zone = Zone.Home; enemy.ProvinceIndex = null; }
        var blocked = Options(MatchEngine.FromTestState(s), 1, "hawado-move-battlefield", hawado.Id).Single();
        Check(!blocked.Enabled && blocked.Provinces is { Length: 0 } && blocked.Reason is not null, "No legal destination remains an explained disabled action");
        var attackState = Table(); var attacker = Add(attackState, "training-province-attacker", 1, Zone.Battlefield, province: 0);
        var attack = Enabled(MatchEngine.FromTestState(attackState), 1, "training-province-attack", attacker.Id);
        Check(attack.Provinces is { Length: > 0 } && attack.Movement is null, "A province-targeting Attack is not a movement");
    }

    private static void OfferedDestinationsValidate()
    {
        var r = Map(unresolved: [0, 1, 2, 3]);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 1, Move, r.Predator.Id);
        var back = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(option))!;
        Check(JsonSerializer.Serialize(back.Movement) == JsonSerializer.Serialize(option.Movement), "Mover metadata survives JSON");
        Check(back.Provinces!.SequenceEqual(option.Provinces!) && back.Command!.Province == option.Command!.Province, "the destinations and the default choice survive JSON");
        Check(option.Command!.Province is int first && option.Provinces!.Contains(first), "the default command already names one of the listed battlefields");
        foreach (var province in option.Provinces!)
        {
            var fresh = MatchEngine.FromTestState(r.S);
            Accept(fresh, option.Command! with { ExpectedVersion = fresh.Version, Province = province });
            Check(fresh.State.Cards[r.Predator.Id].ProvinceIndex == province && fresh.State.Cards[r.Target.Id].ProvinceIndex == province, $"destination {province}");
        }
    }
}
