using L5R.Core;
using static RegressionAssertions;

// Primitive P6 (issue #705, AfterFacts.cs): after-event records and windows. The registrations below are TEST-ONLY and inert unless a test sets Mode, so no other suite sees them; the cards that use the
// primitive are wired in a later wave.
internal static class AfterFactsTests
{
    private static string CelestialId => CardCatalog.Cards.Values.First(c => c.Type == CardType.Celestial && !c.ScenarioOnly).Id;
    private static string Mode = "";
    private static readonly List<string> Log = [];
    private static readonly string[] Kinds = [FactKinds.Seppuku, FactKinds.Proclaimed, FactKinds.AssignedToAttack, FactKinds.TurnedFaceUp, FactKinds.TerrainDestroyed, FactKinds.CelestialEntered, FactKinds.LeftPlay];

    public static void Run(TestRunner runner) => runner.Run("after-facts", [
        ("A fact nobody watches is never recorded: seppuku with no trait and no Reaction changes nothing, opens no window and leaves no record of the action behind", NothingWatches),
        ("Seppuku: a Reaction that names it opens the after-action window for its holder only; a mandatory trait runs first; the holder's own condition decides whether the window opens at all", Seppuku),
        ("Order Seppuku no longer leaves the cards it destroyed on the next action's record", SeppukuLeak),
        ("Proclaimed: the fact carries the Proclaimed Personality and opens the window once the Proclaim window has closed", Proclaimed),
        ("Assigned to attack: one fact per attacking Personality with its battlefield; a defender's assignment records nothing", AssignedToAttack),
        ("Turned face up: a province card revealed (or refilled face up) is a fact; a card that was already face up is not", TurnedFaceUp),
        ("Terrain destroyed: DiscardTerrain records it with its battlefield, and a Terrain replaced by another Terrain counts for Ring of Water", TerrainDestroyed),
        ("Left play: a card that leaves play is a fact with the zone it went to, and a card that stays is not", LeftPlay),
        ("A Celestial's entry asks its registered decision, the Events Phase reveal pauses on it and resumes, and the stored choice survives a save", CelestialChoice),
        ("A Celestial's entry decision that spares a Celestial holds the discard of the others until it is answered", CelestialSpares),
        ("The announce window asks a registered watcher for an action its filter accepts, the +1 lasts until the action ends, and an action the filter refuses opens nothing", Announce),
        ("A window with no usable Reaction opens no pass-only prompt, and a fact is never re-opened from inside its own window", NoEmptyWindows),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.Abilities.Register("TestP6Abilities", 19200, static abilities =>
        {
            foreach (var kind in Kinds) abilities.Add("test-p6-" + kind, new("Reaction", "Test: after " + kind, "none", Trigger: FactTriggers.For(kind)));
            abilities.Add("test-p6-open", new("Open", "Test: an Open action", "none"));
            abilities.Add("test-p6-announce", new("Reaction", "Test: after you announce an action", "none", Performer: "own-personality-with-keyword", RequiredKeywords: ["Samurai"], Trigger: AnnounceTriggers.WithPerformer));
            return abilities;
        });
        EngineHooks.RunEffects.Register("TestP6Run", 19200, static (engine, context) => RunTest(engine, context.id, context.source));
        FactRegistry.Trait("TestP6Trait", 19200, FactKinds.Seppuku, static (engine, fact) => Log.Add("trait:" + fact.CardId), () => Mode == "trait");
        FactRegistry.Condition("TestP6OnlyOthers", 19200, "test-p6-" + FactKinds.Seppuku, static context => Mode != "only-others" || context.Fact.PlayerId != context.PlayerId);
        AnnounceRegistry.Register("TestP6Announce", 19200, "test-p6-announce", static context => Mode == "announce" && context.Ability.Timing == "Open" && context.PerformerId is not null);
        CelestialEntryRegistry.Register("TestP6Celestial", 19200, static (engine, context) => Mode is "celestial" or "spare"
            ? new CelestialEntryChoice("Choose a Personality for the Celestial", engine.TestPersonalities(context.Player.Id), static (e, c, chosen) => Log.Add("chose:" + chosen), Optional: true)
            : null);
        CelestialEntryRegistry.Register("TestP6CelestialSpare", 19201, static (engine, context) => Mode == "spare"
            ? new CelestialEntryChoice("Choose a Celestial to keep", engine.TestOtherCelestials(context.Player.Id, context.Celestial.Id), static (e, c, chosen) => Log.Add("spared:" + chosen), Optional: true, SparesFromDiscard: true)
            : null);
    }

    private static bool? RunTest(MatchEngine engine, string id, CardInstance? source)
    {
        if (!id.StartsWith("test-p6-", StringComparison.Ordinal)) return null;
        Log.Add("reaction:" + id);
        if (id == "test-p6-announce" && source is not null) engine.ApplyUntilActionEnds(source, "test-p6", "test-announce", "Chi", 1);
        return false;
    }

    private static T With<T>(string mode, Func<T> body)
    {
        Mode = mode; Log.Clear();
        try { return body(); } finally { Mode = ""; }
    }

    // ---- fixtures ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Holder(MatchState s, int owner, string kind, Zone zone = Zone.Home, int? province = null)
    {
        var card = Batch336Support.Add(s, "training-crane-samurai", owner, zone, province: province);
        card.GrantedAbilities.Add(new("test-p6-" + kind, "test-fixture", GrantDuration.Permanent));
        return card;
    }

    private static void PassAll(MatchEngine e)
    {
        for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "afterActionResolvedReaction" }; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static (MatchEngine Engine, CardInstance Victim) SeppukuTable(Action<MatchState>? more = null)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var victim = Batch336Support.Add(s, "training-samurai", 0, Zone.Home); victim.Dishonorable = true;
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), victim);
    }

    private static GameCommand OrderSeppuku(MatchEngine e, CardInstance victim) => new("orderSeppuku", 0, e.Version, CardId: victim.Id);

    // ---- tests --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NothingWatches()
    {
        var (e, victim) = SeppukuTable();
        Accept(e, OrderSeppuku(e, victim));
        Check(e.State.QueuedFacts is null && e.State.PendingChoice is null, "no fact queued and no window opened");
        Check(e.State.LastResolvedAction is null || !FactTriggers.IsSentinel(e.State.LastResolvedAction.AbilityId), "no fact record");
        Check(!e.State.Cards.ContainsKey("missing") && e.State.Cards[victim.Id].Zone != Zone.Home, "the Personality did die");
    }

    private static void Seppuku()
    {
        var (e, victim) = SeppukuTable(s => Holder(s, 1, FactKinds.Seppuku));
        var holder = e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.GrantedAbilities.Count == 1);
        With("trait", () => { Accept(e, OrderSeppuku(e, victim)); return 0; });
        Check(Log.SequenceEqual(["trait:" + victim.Id]), "the trait ran, before any Reaction");
        Check(e.State.PendingChoice is { Type: "afterActionResolvedReaction" }, "the window opened: the holder has a Reaction to it");
        Eq(e.State.PriorityPlayerId, 1, "it opens to the holder");
        Check(e.State.LastResolvedAction is { Fact.Kind: FactKinds.Seppuku } facts && facts.PlayerId == 0 && facts.Affected.Any(a => a.CardId == victim.Id), "the record names the fact, the player and the card");
        Check(e.QueryActions(0).All(a => a.Type != "useAbility"), "the other player is offered none");
        var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.CardId == holder.Id);
        Log.Clear();
        With("trait", () => { Accept(e, option.Command!); return 0; });
        Check(Log.Contains("reaction:test-p6-seppuku"), "the Reaction resolved");
        PassAll(e);
        Check(e.State.PendingChoice is null, "the window closed");

        // The holder's own condition ("another player's Personality") decides: a holder whose own Personality committed seppuku is not offered the window.
        var (f, own) = SeppukuTable(s => { var h = Holder(s, 0, FactKinds.Seppuku); });
        With("only-others", () => { Accept(f, OrderSeppuku(f, own)); return 0; });
        Check(f.State.PendingChoice is null, "a Reaction whose condition fails opens no window");
    }

    private static void SeppukuLeak()
    {
        var (e, victim) = SeppukuTable();
        Accept(e, OrderSeppuku(e, victim));
        Check(e.State.CurrentActionAffected.Count == 0 && e.State.CurrentActionHonorLosers.Count == 0, "nothing the destruction recorded is left for the next action");
    }

    private static void Proclaimed()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Dynasty; s.Players[0].Clan = "Crane";
        var proclaimed = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var holder = Holder(s, 0, FactKinds.Proclaimed);
        s.PendingChoice = new("proclaim", 0, proclaimed.Id, "After entry: Proclaim for Honor or pass.");
        var e = MatchEngine.FromTestState(s);
        With("", () => { Accept(e, new GameCommand("proclaim", 0, e.Version)); return 0; });
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: not "afterActionResolvedReaction" } window && window.Type != "";)
        {
            guard++;
            if (e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Type == "declineReaction") is not { } pass) break;
            Accept(e, pass.Command!);
        }
        Check(e.State.PendingChoice is { Type: "afterActionResolvedReaction" } && e.State.LastResolvedAction is { Fact.Kind: FactKinds.Proclaimed } facts && facts.Fact!.CardId == proclaimed.Id,
            "the Proclaimed Personality's fact opened the window: " + e.State.PendingChoice?.Type);
        Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.CardId == holder.Id && a.Enabled), "its holder may react");
    }

    private static void AssignedToAttack()
    {
        var s = MatchEngine.CreatePublishedMatch().State;
        var attacker = Batch336Support.Add(s, "training-samurai", 0, Zone.Home);
        var defender = Batch336Support.Add(s, "training-samurai", 1, Zone.Home);
        var holder = Holder(s, 1, FactKinds.AssignedToAttack);
        s.Phase = Phase.AttackDeclaration; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("declareAttack", 0, e.Version));
        Accept(e, new GameCommand("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [attacker.Id] = 0 }));
        Check(e.State.PendingChoice is { Type: "afterActionResolvedReaction" } && e.State.LastResolvedAction is { Fact: { Kind: FactKinds.AssignedToAttack, Province: 0, PlayerId: 0 } } facts && facts.Fact.CardId == attacker.Id,
            "the attacker's assignment is a fact with its battlefield: " + e.State.PendingChoice?.Type);
        Check(e.QueryActions(1).Any(a => a.Type == "useAbility" && a.CardId == holder.Id && a.Enabled), "the defender's holder may react");
        PassAll(e);
        Eq(e.State.Phase, Phase.DefenseAssignment, "the defender assigns next");
        Accept(e, new GameCommand("assignDefense", 1, e.Version, Assignments: new Dictionary<string, int> { [defender.Id] = 0 }));
        Check(e.State.QueuedFacts is null && e.State.PendingChoice is not { Type: "afterActionResolvedReaction" } && e.State.LastResolvedAction?.Fact is not { Kind: FactKinds.AssignedToAttack, PlayerId: 1 }, "a defender's assignment records nothing");
    }

    private static void TurnedFaceUp()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var holder = Holder(s, 0, FactKinds.TurnedFaceUp);
        var up = s.Players[1].Provinces[0].CardId!; s.Cards[up].FaceUp = true;
        var down = s.Players[1].Provinces[1].CardId!; s.Cards[down].FaceUp = false;
        var e = MatchEngine.FromTestState(s);
        e.SweepFactsForTest(); // the first look: nothing is a change
        Check(e.State.QueuedFacts is null, "the first sweep only learns what was face up");
        var flipped = Batch336Support.Edit(e, st => st.Cards[down].FaceUp = true);
        flipped.SweepFactsForTest();
        Check(flipped.State.PendingChoice is { Type: "afterActionResolvedReaction" } && flipped.State.LastResolvedAction is { Fact: { Kind: FactKinds.TurnedFaceUp, Province: 1, PlayerId: 1 } } facts && facts.Fact.CardId == down,
            "the card that turned face up is a fact that opened the window: " + flipped.State.PendingChoice?.Type);
        Check(flipped.QueryActions(0).Any(a => a.Type == "useAbility" && a.CardId == holder.Id && a.Enabled), "its holder may react");
        PassAll(flipped);
        flipped.SweepFactsForTest();
        Check(flipped.State.PendingChoice is null && flipped.State.QueuedFacts is null, "and it is recorded once");
    }

    private static void TerrainDestroyed()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var terrain = Batch336Support.Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var destroyer = Batch336Support.Add(s, "training-terrain-destroyer", 0, Zone.Battlefield, province: 0);
        var holder = Holder(s, 1, FactKinds.TerrainDestroyed, Zone.Battlefield, 0); // #1133: at the battlefield to react in the battle
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: terrain.Id, AbilityId: "training-destroy-terrain"));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain was destroyed");
        Check(e.State.PendingChoice is { Type: "afterActionResolvedReaction" } && e.State.LastResolvedAction is { Fact: { Kind: FactKinds.TerrainDestroyed, Province: 0 } } facts && facts.Fact.CardId == terrain.Id, "the destruction is a fact with its battlefield");
        Check(e.QueryActions(1).Any(a => a.Type == "useAbility" && a.CardId == holder.Id && a.Enabled), "the Terrain's controller's holder may react");

        // A Terrain replaced by another Terrain was destroyed: Ring of Water's condition is set, and the replaced Terrain is a fact too.
        var t = Batch336Support.Table(Phase.Battle, 0);
        var old = Batch336Support.Add(t, "training-terrain", 1, Zone.Battlefield, province: 0);
        Batch336Support.Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var replacement = Batch336Support.Add(t, "oracle-l5r-397", 0, Zone.Hand); // Ancient Feud: "Destroy a Terrain (if able). Put this Terrain into play"
        Holder(t, 1, FactKinds.TerrainDestroyed, Zone.Battlefield, 0);
        var f = MatchEngine.FromTestState(t);
        Check(!f.State.Players[0].TerrainDestroyedThisBattle, "fixture: nothing destroyed yet");
        Accept(f, new GameCommand("playStrategy", 0, f.Version, CardId: replacement.Id));
        Eq(f.State.Cards[old.Id].Zone, Zone.FateDiscard, "the old Terrain was replaced");
        Check(f.State.Players[0].TerrainDestroyedThisBattle, "and counts as destroyed this battle");
        Check(f.State.LastResolvedAction is { Fact.Kind: FactKinds.TerrainDestroyed } replaced && replaced.Fact.CardId == old.Id, "and is a fact");
    }

    private static void LeftPlay()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var holder = Holder(s, 1, FactKinds.LeftPlay);
        var leaves = Batch336Support.Add(s, "training-crane-veteran", 0, Zone.Home);
        var stays = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        e.SweepFactsForTest(); // stamps entry turns
        var gone = Batch336Support.Edit(e, st => st.Cards[leaves.Id].Zone = Zone.Dead);
        gone.SweepFactsForTest();
        Check(gone.State.PendingChoice is { Type: "afterActionResolvedReaction" } && gone.State.LastResolvedAction is { Fact: { Kind: FactKinds.LeftPlay, Arg: "Dead", PlayerId: 0 } } facts && facts.Fact.CardId == leaves.Id,
            "the card that left play is a fact with its destination: " + gone.State.PendingChoice?.Type);
        Check(gone.QueryActions(1).Any(a => a.Type == "useAbility" && a.CardId == holder.Id && a.Enabled), "its holder may react");
        PassAll(gone);
        Check(gone.State.PendingChoice is null && gone.State.Cards[stays.Id].Zone == Zone.Home, "the card that stayed is not a fact");
    }

    private static void CelestialChoice()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var personality = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var celestial = Batch336Support.Add(s, CelestialId, 0, Zone.Province);
        s.Players[0].Provinces[2].CardId = celestial.Id; celestial.ProvinceIndex = 2; celestial.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        With("celestial", () => { e.CelestialEntryForTest(0, 2, celestial.Id, pauseReveal: true); return 0; });
        Check(e.State.PendingChoice is { Type: "eventChoice" } && e.State.PendingEventChoice is { Kind: "p6-celestial", Origin: "reveal" }, "the entry opened its decision and paused the reveal");
        Check(e.State.Cards[celestial.Id].Zone == Zone.Celestial, "the Celestial is in play");
        var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Check(options.Any(a => a.Command!.TargetId == personality.Id) && options.Any(a => a.Command!.Choice == "none"), "the decider may pick a Personality or none");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Check(json.Contains("p6-celestial") && System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!.PendingEventChoice is { Kind: "p6-celestial" }, "the pending decision survives a save");
        With("celestial", () => { Accept(e, options.First(a => a.Command!.TargetId == personality.Id).Command!); return 0; });
        Check(Log.Contains("chose:" + personality.Id), "the choice was applied");
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "eventChoice", "the decision is answered and the reveal carried on");

        // The real Events Phase: the Celestial is revealed face down in province 2; the reveal pauses on its decision (the later provinces are not revealed yet) and, once answered, finishes the phase.
        var t = Batch336Support.Table(Phase.Straighten, 0);
        var person = Batch336Support.Add(t, "training-crane-samurai", 0, Zone.Home);
        var hidden = Batch336Support.Add(t, CelestialId, 0, Zone.Province);
        t.Players[0].Provinces[1].CardId = hidden.Id; hidden.ProvinceIndex = 1; hidden.FaceUp = false;
        var later = t.Players[0].Provinces[3].CardId!; t.Cards[later].FaceUp = false;
        var f = MatchEngine.FromTestState(t);
        With("celestial", () => { f.EventsPhaseForTest(0); return 0; });
        Check(f.State.Phase == Phase.Events && f.State.PendingEventChoice is { Kind: "p6-celestial", Origin: "reveal" }, "the Events Phase paused on the Celestial's decision: " + f.State.Phase + "/" + f.State.PendingEventChoice?.Kind);
        Check(!f.State.Cards[later].FaceUp, "the provinces after it are not revealed yet");
        var answer = f.QueryActions(0).First(a => a.Type == "eventChoice" && a.Command!.TargetId == person.Id);
        With("celestial", () => { Accept(f, answer.Command!); return 0; });
        Check(f.State.Phase != Phase.Events && f.State.PendingChoice is null, "answered, the reveal resumed and the phase went on: " + f.State.Phase);
        Check(f.State.Cards[later].FaceUp, "the later provinces were revealed after the answer");
        Check(Log.Contains("chose:" + person.Id), "and the choice was applied");
    }

    private static void CelestialSpares()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var keep = Batch336Support.Add(s, CelestialId, 0, Zone.Celestial);
        var drop = Batch336Support.Add(s, CelestialId, 0, Zone.Celestial);
        var entering = Batch336Support.Add(s, CelestialId, 0, Zone.Province);
        s.Players[0].Provinces[2].CardId = entering.Id; entering.ProvinceIndex = 2; entering.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        With("spare", () => { e.CelestialEntryForTest(0, 2, entering.Id, pauseReveal: false); return 0; });
        Check(e.State.Cards[keep.Id].Zone == Zone.Celestial && e.State.Cards[drop.Id].Zone == Zone.Celestial, "no Celestial is discarded before the decisions are answered");
        Check(e.State.CelestialDiscardDebts is { Count: 1 }, "the discard is owed");
        e.OpenQueuedChoicesForTest(0);
        Check(e.State.PendingEventChoice is { Kind: "p6-celestial" }, "the first decision is open");
        for (var guard = 0; guard < 4 && e.State.PendingEventChoice is { Kind: "p6-celestial" } pending; guard++)
        {
            var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
            var pick = pending.Data[0] == "TestP6CelestialSpare" ? options.First(a => a.Command!.TargetId == keep.Id) : options.First(a => a.Command!.Choice == "none");
            With("spare", () => { Accept(e, pick.Command!); return 0; });
        }
        Eq(e.State.Cards[keep.Id].Zone, Zone.Celestial, "the Celestial the choice spared stays");
        Eq(e.State.Cards[drop.Id].Zone, Zone.DynastyDiscard, "the other is discarded once the decisions are answered");
        Eq(e.State.Cards[entering.Id].Zone, Zone.Celestial, "the entering Celestial is in play");
        Check(e.State.CelestialDiscardDebts is null, "and nothing is owed");
    }

    private static void Announce()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var watcher = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        watcher.GrantedAbilities.Add(new("test-p6-announce", "test-fixture", GrantDuration.Permanent));
        var actor = Batch336Support.Add(s, "training-samurai", 0, Zone.Home);
        actor.GrantedAbilities.Add(new("test-p6-open", "test-fixture", GrantDuration.Permanent));
        var e = MatchEngine.FromTestState(s);
        GameCommand Open() => new("useAbility", 0, e.Version, CardId: actor.Id, AbilityId: "test-p6-open");
        Mode = "announce"; Log.Clear();
        try
        {
            Accept(e, Open());
            Check(e.State.PendingChoice is { Type: "announceReaction" } && e.State.PendingChoice.CardId == watcher.Id, "the announce window opened for the registered watcher: " + e.State.PendingChoice?.Type);
            var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == watcher.Id && a.Enabled).ToArray();
            Check(offered.Length > 0 && offered.All(a => a.Command!.PerformerId is not null), "the Reaction is offered once per performer it may name: " + string.Join("; ", e.QueryActions(0).Select(o => $"{o.Type}/{o.Label}/{o.Enabled}/{o.Reason}")));
            Accept(e, offered[0].Command!);
            Check(Log.Contains("reaction:test-p6-announce"), "the Reaction resolved");
            Check(e.State.Cards[watcher.Id].Modifiers.Any(m => m.Effect == "test-announce") && e.State.ActionScopedModifiers is { Count: 1 }, "its +1 is in force while the announced action has not resolved");
            for (var guard = 0; guard < 4 && e.State.PendingChoice is { Type: "announceReaction" }; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Check(Log.Contains("reaction:test-p6-open"), "the announced action went on to resolve");
            PassAll(e);
            Check(!e.State.Cards[watcher.Id].Modifiers.Any(m => m.Effect == "test-announce") && e.State.ActionScopedModifiers is null, "and the +1 ended with it");
        }
        finally { Mode = ""; }

        // A filter that refuses the action: no window at all.
        var t = Batch336Support.Table(Phase.Action, 0);
        var w2 = Batch336Support.Add(t, "training-crane-samurai", 0, Zone.Home); w2.GrantedAbilities.Add(new("test-p6-announce", "test-fixture", GrantDuration.Permanent));
        var a2 = Batch336Support.Add(t, "training-samurai", 0, Zone.Home); a2.GrantedAbilities.Add(new("test-p6-open", "test-fixture", GrantDuration.Permanent));
        var f = MatchEngine.FromTestState(t);
        With("announce-none", () => { Accept(f, new GameCommand("useAbility", 0, f.Version, CardId: a2.Id, AbilityId: "test-p6-open")); return 0; });
        Check(f.State.PendingChoice is not { Type: "announceReaction" } && Log.Contains("reaction:test-p6-open"), "an action the filter refuses opens no window and just resolves");
    }

    private static void NoEmptyWindows()
    {
        var (e, victim) = SeppukuTable(s => { var h = Holder(s, 1, FactKinds.Seppuku); h.Bowed = true; });
        With("", () => { Accept(e, OrderSeppuku(e, victim)); return 0; });
        Check(e.State.PendingChoice is null, "the holder is bowed and cannot react, so nothing opens (no pass-only prompt)");
        Check(e.State.QueuedFacts is null, "and the fact is spent");

        // Two facts of one command (the seppuku and the Personality leaving play): the first window opens, the second waits for it to close and opens after it, never inside it.
        var (f, dead) = SeppukuTable(s => { var h = Holder(s, 1, FactKinds.Seppuku); h.GrantedAbilities.Add(new("test-p6-" + FactKinds.LeftPlay, "test-fixture", GrantDuration.Permanent)); });
        f.SweepFactsForTest(); // stamps entry turns before the command
        With("", () => { Accept(f, OrderSeppuku(f, dead)); return 0; });
        Check(f.State.PendingChoice is { Type: "afterActionResolvedReaction" } && f.State.LastResolvedAction is { Fact.Kind: FactKinds.Seppuku }, "the seppuku window opens first: " + f.State.LastResolvedAction?.Fact?.Kind);
        Check(f.State.QueuedFacts is { Count: > 0 } waiting && waiting.Any(x => x.Kind == FactKinds.LeftPlay), "the second fact waits, queued");
        var seppuku = f.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "test-p6-" + FactKinds.Seppuku);
        Check(f.QueryActions(1).All(a => a.Command?.AbilityId != "test-p6-" + FactKinds.LeftPlay), "its Reaction is not offered inside the first window");
        With("", () => { Accept(f, seppuku.Command!); return 0; });
        for (var guard = 0; guard < 6 && f.State.PendingChoice is { Type: "afterActionResolvedReaction" } && f.State.LastResolvedAction is { Fact.Kind: FactKinds.Seppuku }; guard++)
            Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        Check(f.State.PendingChoice is { Type: "afterActionResolvedReaction" } && f.State.LastResolvedAction is { Fact.Kind: FactKinds.LeftPlay }, "once the first window closed the second opened: " + f.State.PendingChoice?.Type + "/" + f.State.LastResolvedAction?.Fact?.Kind);
        PassAll(f);
        Check(f.State.PendingChoice is null && f.State.QueuedFacts is null, "and then nothing is left");
    }
}
