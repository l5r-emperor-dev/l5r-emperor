using System.Collections.Concurrent;
using System.Text.Json;
using L5R.Client;
using L5R.Core;
using L5R.Server;

var storage = Path.Combine(Path.GetTempPath(), "l5r-client-transport-" + Guid.NewGuid().ToString("N"));
const string duelRoom = "DUEL2345";
var duelTokens = Enumerable.Range(0, 2).Select(_ => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))).ToArray();
await new RoomStore(storage).SaveAsync(new PersistedRoom(1, duelRoom, DateTimeOffset.UtcNow,
        duelTokens.Select((token, i) => new SeatCredential(i, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))))).ToArray(), MatchEngine.CreateDuelDemo().ExportReplay(), []));
var app = await ServerApplication.BuildAsync(["--urls", "http://127.0.0.1:0", "--L5R:StoragePath", storage,
    "--Logging:LogLevel:Default", "Critical"]);
var failures = 0;
try
{
    await app.StartAsync();
    var url = app.Urls.Single();
    var cases = new (string Name, Func<Task> Test)[]
    {
        ("current battle values survive both public server envelopes; review export stays unavailable", BattleValuesProjection),
        ("payment source methods and private entry candidates survive the server envelope", () => { GameplayControlsProjection(); return Task.CompletedTask; }),
        ("public response context survives server state and client projection without changing actions or exposing a hand", () => { ReactionPresentationProjection(); return Task.CompletedTask; }),
        ("prepared review positions retain replay export restrictions until ordinary practice starts", PreparedReviewTests.Run),
        ("a slow practice opponent decides on a copy off the UI thread, plays a ready answer once, reports a refusal and drops a late answer (issue #1214)", PracticeOpponentTests.Run),
        ("a local crash report holds the exception, the last errors and the viewer's position, and scrubs tokens and home paths (issue #905)", () => { CrashBundleTests.Run(); return Task.CompletedTask; }),
        ("private duel round trip retains own focus, result and priority through reconnect", () => PrivateDuel(url,duelRoom,duelTokens)),
        ("live duel queue preserves immediate results, reconnects and back-to-back presentations", DuelQueue),
        ("custom Practice keeps selected seat, rejects invalid replacement and exports replay", CustomPlay),
        ("Duel Demo isolates practice, serializes results and rejects stale commands", DemoSession),
        ("Leaving a private table disconnects without conceding, and the seat resumes", () => LeaveTable(url)),
        ("client create/join, typed actions, commands and authenticated actor", () => LivePlay(url)),
        ("published match transmits Border Keep ability and restores it on reconnect", () => PublishedPlay(url)),
        ("client disconnect/resume and overlapping reconnects", () => Reconnect(url)),
        ("shown-card history arrives on both seats and survives live reconnect without exposing other deck cards", ShownHistoryReconnect),
        ("client rejects insecure remote URLs and invalid room reservations", () => InvalidConnections(url)),
        ("client command payload carries every GameCommand field the server accepts (issue #303)", () => { OutgoingPayload(); return Task.CompletedTask; }),
        ("movement destinations/movers and alternative costs survive the server envelope", () => { MovementActionProjection(); return Task.CompletedTask; }),
        ("Attack choices preserve NoTarget, card targets, province targets and explicit nulls through both wire directions", () => { AttackTargetProjection(); return Task.CompletedTask; }),
        ("alternative costs survive the server envelope and selected cost submission for the first four previews", () => { AlternativeCostProjection(); return Task.CompletedTask; }),
        ("deck search: the searcher's deck viewer, the public shown-cards record and every search action survive the server envelope and the client's deserialization (issue #312)", () => { SearchViewsCrossTheWire(); return Task.CompletedTask; }),
        ("look at the top cards: the viewer's ordered cards, the deck choice over any of four decks (the opponent's included) and every look action survive the server envelope and the client's deserialization (issue #322)", () => { LookViewsCrossTheWire(); return Task.CompletedTask; }),
        ("the action resolution area is omitted from every payload outside an open action and crosses the wire complete while one is open (issue #917)", () => { ResolutionAreaCrossesTheWire(); return Task.CompletedTask; }),
        ("a copied card's frame fields (IsCopy, CopyOfCardId, CopyOfDefinitionId, CopyConditions) and a controlled card's RealOwnerId survive the server envelope and the client's deserialization (issue #501)", () => { CopyAndControlFieldsCrossTheWire(); return Task.CompletedTask; })
    };
    foreach (var (name, test) in cases)
    {
        Console.WriteLine($"RUN  {name}");
        try { await test(); Console.WriteLine($"PASS {name}"); }
        catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}\n{exception}"); }
    }
    Console.WriteLine($"{cases.Length - failures}/{cases.Length} client transport integration checks passed.");
}
finally
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await app.StopAsync(timeout.Token);
    await app.DisposeAsync();
    if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
}
return failures == 0 ? 0 : 1;

static void GameplayControlsProjection()
{
    foreach (var kind in new[] { "reaction", "entry" })
    {
        var engine = MatchEngine.CreateGameplayControlsShowcase(kind);
        var seat = engine.State.PriorityPlayerId;
        for (var viewer = 0; viewer < 2; viewer++)
        {
            var actions = engine.QueryActions(viewer);
            var json = JsonSerializer.Serialize(new StateMessage("state", "ROOM", viewer, engine.Version, engine.GetView(viewer), actions.Select(AvailableAction.FromCore).ToArray(), [true, true]), Protocol.Json);
            using var document = JsonDocument.Parse(json);
            var received = document.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            received = received.Select(a => a with { Command = a.Command is null ? null : a.Command with { PlayerId = viewer, ExpectedVersion = engine.Version } }).ToArray();
            Check(JsonSerializer.Serialize(received, Protocol.Json) == JsonSerializer.Serialize(actions, Protocol.Json), "Source IDs, production, conflicts and candidate IDs changed in transit");
            if (kind == "reaction" && viewer == seat)
                Check(received.Any(a => a.Gold?.Sources?.Any(m => m.Kind == "reaction" && a.Gold.Sources.Any(b => b.Kind == "bow" && b.CardId == m.CardId)) == true), "Purchase quote dropped independent Bow/Reaction methods");
            if (kind == "entry")
                Check(received.Any(a => a.DecisionCandidateIds?.Length > 0) == (viewer == seat), "Private entry candidates were dropped or exposed to the waiting seat");
        }
    }
}

static void ReactionPresentationProjection()
{
    var engine = MatchEngine.CreateGameplayControlsShowcase("honor-reaction");
    Check(engine.State.PendingChoice?.Type == "honorChangeReaction", "Fixture must offer a real optional Honor response");
    var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    ReactionPresentationView? publicContext = null;
    foreach (var viewer in new[] { 0, 1 })
    {
        var view = engine.GetView(viewer);
        Check(view.ReactionPresentation is not null, "Server omitted the active response context");
        if (publicContext is null) publicContext = view.ReactionPresentation;
        else Check(publicContext == view.ReactionPresentation, "Response context exposed seat-specific information");
        var actions = engine.QueryActions(viewer);
        var message = new StateMessage("state", "ROOM", viewer, engine.Version, view,
            actions.Select(AvailableAction.FromCore).ToArray(), [true, true]);
        var json = JsonSerializer.Serialize(message, Protocol.Json);
        var current = JsonSerializer.Deserialize<StateMessage>(json, Protocol.Json)!;
        Check(current.View.ReactionPresentation == view.ReactionPresentation, "Strict current server state lost the response context");
        using var document = JsonDocument.Parse(json);
        var receivedView = document.RootElement.GetProperty("view").Deserialize<MatchView>(web)!;
        Check(receivedView.ReactionPresentation == view.ReactionPresentation, "Actual client Web projection lost response context");
        Check(receivedView.Players.Single(p => p.Id != viewer).Hand.Length == 0, "Response context exposed opponent hand");
        var receivedActions = document.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(web)!;
        receivedActions = receivedActions.Select(a => a with { Command = a.Command is null ? null : a.Command with { PlayerId = viewer, ExpectedVersion = engine.Version } }).ToArray();
        Check(JsonSerializer.Serialize(receivedActions, Protocol.Json) == JsonSerializer.Serialize(actions, Protocol.Json), "Presentation changed the authoritative offered commands");
        var older = JsonSerializer.SerializeToNode(message, Protocol.Json)!;
        older["view"]!.AsObject().Remove("reactionPresentation");
        Check(older.Deserialize<StateMessage>(Protocol.Json)!.View.ReactionPresentation is null,
            "Older state payload must remain accepted without inventing response context");
    }
}

static async Task BattleValuesProjection()
{
    var engine = MatchEngine.CreateBattleValuesShowcase("bonus");
    var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    foreach (var seat in new[] { 0, 1 })
    {
        var view = engine.GetView(seat);
        var message = new StateMessage("state", "ABCDEFGH", seat, view.Version, view, [], [true, true]);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(message, Protocol.Json));
        var received = document.RootElement.GetProperty("view").Deserialize<MatchView>(web)!;
        Check(received.Battlefields!.Single().Values == view.Battlefields!.Single().Values, "Server envelope changed authoritative battle values");
        Check(received.Players.Single(p => p.Id != seat).Hand.Length == 0, "Battle values exposed opponent hand");
        var older = JsonSerializer.SerializeToNode(view, web)!;
        older["battlefields"]![0]!.AsObject().Remove("values");
        Check(older.Deserialize<MatchView>(web)!.Battlefields!.Single().Values is null, "Older payload invented battle values");
    }
    using var session = new MatchSession(); session.StartBattleValuesReview("standard");
    Check(session.ExportPracticeReplay() is null, "Prepared fixture exported an unsupported replay");
    var before = session.Snapshot!.View.Battlefields!.Single().Values;
    session.SwitchPracticeSeat();
    Check(session.Snapshot!.View.Battlefields!.Single().Values == before, "Review seat switch changed totals");
    await session.Submit(new GameCommand("concede", session.Snapshot.View.ViewerId, session.Snapshot.View.Version));
    Check(session.Snapshot!.View.Battlefields!.All(b => b.Values is null), "Concession retained battle values in client session");
    session.StartPractice(false, true, 0);
    Check(!session.BattleValuesReview && session.ExportPracticeReplay() is not null, "Ordinary practice kept preview restrictions");
}

static void AttackTargetProjection()
{
    var engine = MatchEngine.CreateEconomyScenario(); var view = engine.GetView(0);
    var offered = new ActionOption("useAbility", "Attack", true, null,
        new GameCommand("useAbility", 0, view.Version, "source", TargetId: "card", AbilityId: "attack"),
        "source", ["card"], [2], NoTarget: true, Attack: new("Ranged", "source"));
    view = view with { PendingAttack = new("Melee", "performer") };
    var message = new StateMessage("state", "ABCDEFGH", 0, view.Version, view, [AvailableAction.FromCore(offered)], [true, true]);
    using var document = JsonDocument.Parse(JsonSerializer.Serialize(message, Protocol.Json));
    var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var received = document.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(web)!.Single();
    Check(received.Attack == offered.Attack, "The server dropped the attack type/source");
    var receivedView = document.RootElement.GetProperty("view").Deserialize<MatchView>(web)!;
    Check(receivedView.PendingAttack == view.PendingAttack, "A pending reaction lost its attack cue");
    var olderAction = JsonSerializer.SerializeToNode(offered, web)!; olderAction.AsObject().Remove("attack");
    Check(olderAction.Deserialize<ActionOption>(web)!.Attack is null, "Older servers must not imply an attack type");
    var olderView = JsonSerializer.SerializeToNode(view, web)!; olderView.AsObject().Remove("pendingAttack");
    Check(olderView.Deserialize<MatchView>(web)!.PendingAttack is null, "Older views must omit the pending cue safely");
    Check(received.NoTarget && received.TargetIds!.SequenceEqual(["card"]) && received.Provinces!.SequenceEqual([2]) && received.Movement is null,
        "The Attack's three choices were lost or confused with a movement");
    foreach (var command in new[]{
        offered.Command! with {TargetId=null,Province=null},
        offered.Command! with {TargetId="card",Province=null},
        offered.Command! with {TargetId=null,Province=2}})
    {
        var json = JsonSerializer.Serialize(MatchSession.ToWire(command), web);
        var bound = JsonSerializer.Deserialize<WireGameCommand>(json, Protocol.Json)!.BindToSeat(0, view.Version);
        Check(bound.TargetId == command.TargetId && bound.Province == command.Province && bound.AbilityId == command.AbilityId && bound.CardId == command.CardId,
            "No target/card/province command was changed during submission");
        Check(bound.Assignments is null, "An Attack target was encoded as an army assignment");
    }
}

static void MovementActionProjection()
{
    var engine = MatchEngine.CreateEconomyScenario(); var view = engine.GetView(0);
    var option = new ActionOption("useAbility", "Movement preview", true, null,
        new GameCommand("useAbility", 0, view.Version, "source", TargetId: "target", Province: 2, AbilityId: "move", CostOption: "bow"),
        "source", ["target"], [0, 2], AlternativeCost: new("move", "performer", "bow", "Bow performer", "Movement preview"),
        Movement: new(1, ["performer"], true));
    var message = new StateMessage("state", "ABCDEFGH", 0, view.Version, view, [AvailableAction.FromCore(option)], [true, true]);
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(message, Protocol.Json));
    var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var received = json.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(web)!.Single();
    received = received with { Command = received.Command! with { PlayerId = 0, ExpectedVersion = view.Version } };
    Check(JsonSerializer.Serialize(received, web) == JsonSerializer.Serialize(option, web), "The server projection dropped or altered action metadata");
    var command = received.Command! with { Province = 0 };
    var wire = JsonSerializer.Serialize(MatchSession.ToWire(command), web);
    var bound = JsonSerializer.Deserialize<WireGameCommand>(wire, Protocol.Json)!.BindToSeat(0, view.Version);
    Check(JsonSerializer.Serialize(bound) == JsonSerializer.Serialize(command), "The selected province/target did not survive the submitted command");
    Check(bound.Assignments is null, "A movement must not submit attack assignments");
}

static void AlternativeCostProjection()
{
    var view = MatchEngine.CreateEconomyScenario().GetView(0);
    foreach (var cost in new[] { "bow", "destroy" })
    {
        var offered = new ActionOption("useAbility", "Choose a cost", true, null,
            new GameCommand("useAbility", 0, view.Version, "source", AbilityId: "ability", CostOption: cost),
            "source", AlternativeCost: new("ability", "source", cost, cost + " this card", "Choose a cost"));
        var message = new StateMessage("state", "ABCDEFGH", 0, view.Version, view, [AvailableAction.FromCore(offered)], [true, true]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(message, Protocol.Json));
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var received = json.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(web)!.Single();
        Check(received.AlternativeCost == offered.AlternativeCost, "The server dropped the authoritative alternative-cost presentation");
        var wire = JsonSerializer.Serialize(MatchSession.ToWire(received.Command!), web);
        var bound = JsonSerializer.Deserialize<WireGameCommand>(wire, Protocol.Json)!.BindToSeat(0, view.Version);
        Check(bound.CostOption == cost && bound.CardId == "source" && bound.AbilityId == "ability", "The selected cost changed during submission");
    }
}

static async Task PrivateDuel(string url, string room, string[] tokens)
{
    using var crane = new MatchSession(); using var lion = new MatchSession();
    var seats = tokens.Select((t, i) => new RoomSeat(room, i, t, Protocol.Version, MatchEngine.ContentVersion)).ToArray();
    await crane.Resume(url, seats[0]); await lion.Resume(url, seats[1]);
    await WaitFor(() => crane.Snapshot?.PlayersConnected?.All(c => c) == true && lion.Snapshot?.PlayersConnected?.All(c => c) == true);
    var flows = new[] { new MatchDuelFlow(), new MatchDuelFlow() }; flows[0].Observe(crane.Snapshot); flows[1].Observe(lion.Snapshot);
    await crane.Submit(crane.Snapshot!.Actions.First(a => a.Command?.AbilityId == "steel-on-steel-duel").Command!);
    await Both(1); flows[0].Observe(crane.Snapshot); flows[1].Observe(lion.Snapshot);
    Check(flows.All(f => f.Current?.View.PendingDuel?.Stage == "Focusing"), "Both real clients route accepted challenge");
    await lion.Submit(lion.Snapshot!.Actions.Single(a => a.Command?.Choice == "deck").Command!); await Both(2);
    Check(lion.Snapshot!.View.Players[1].Focused!.Single().Definition is not null && crane.Snapshot!.View.Players[1].Focused!.Length == 0, "Blind deck focus is inspectable only by its owner");
    var identity = lion.Generation;
    lion.Disconnect(); await WaitFor(() => crane.Snapshot?.PlayersConnected?[1] == false);
    await lion.Resume(url, seats[1]); await WaitFor(() => lion.Connected && !lion.Busy && lion.Snapshot?.View.Version == 2 && crane.Snapshot?.PlayersConnected?.All(c => c) == true);
    Check(lion.Generation == identity, "Reconnect retains this match presentation identity");
    Check(lion.Snapshot!.View.Players[1].Focused!.Single().Definition is not null, "Reconnect restores private focus without a new commitment");
    var courtesy = crane.Snapshot!.View.Players[0].Hand.Single(c => c.Definition!.Id == "oracle-l5r-7996");
    await crane.Submit(crane.Snapshot.Actions.Single(a => a.Type == "focus" && a.Command!.CardId == courtesy.Id).Command!); await Both(3);
    await lion.Submit(lion.Snapshot.Actions.Single(a => a.Type == "strike").Command!); await Both(4);
    foreach (var (session, index) in new[] { (crane, 0), (lion, 1) })
    {
        var snapshot = session.Snapshot!; flows[index].Observe(snapshot);
        Check(flows[index].Current!.View.LastDuel is { Complete: true, Result.ChallengerTotal: 8, Result.ChallengedTotal: 7 }, "Each client retains the identical complete result");
        flows[index].Complete(snapshot.View.LastDuel!.Id); flows[index].Observe(snapshot);
        Check(flows[index].Current is null && session.Snapshot!.View.Version == 4, "Continue is presentation-only, and a duplicate snapshot cannot reopen it");
        Check(snapshot.View.PendingDuel is null && snapshot.View.PriorityPlayerId == 1 && snapshot.View.Phase == Phase.Battle, "Battle and priority belong to the engine after the return");
    }
    await lion.Submit(lion.Snapshot.Actions.First(a => a.Type == "pass" && a.Enabled).Command!); await Both(5);
    Check(crane.Snapshot!.View.Version == 5, "The original private match continues after the duel");
    async Task Both(long version) => await WaitFor(() => crane.Snapshot?.View.Version == version && lion.Snapshot?.View.Version == version && !crane.Busy && !lion.Busy);
}

static async Task DuelQueue()
{
    using var session = new MatchSession(); session.StartDuelDemo("steel", 205, 1);
    var flow = new MatchDuelFlow(); flow.Observe(session.Snapshot); await session.BeginDuelDemo(); flow.Observe(session.Snapshot);
    var generation = session.Generation;
    var id = flow.Current!.View.LastDuel!.Id; var initial = session.Snapshot!;
    Check(flow.Current.View.PendingDuel?.Stage == "Focusing", "Routes the real pending duel");
    await session.Submit(session.Snapshot!.Actions.Single(a => a.Type == "strike").Command!);
    var result = session.Snapshot!; flow.Observe(result); flow.Observe(result);
    Check(flow.Current!.View.LastDuel is { Complete: true, Result: not null }, "Immediate strike retains complete result through repeated snapshots");
    var resumed = new MatchDuelFlow(); resumed.Observe(initial);
    Check(resumed.Current is not null, "Reconnect resumes an unfinished duel");
    var historic = new MatchDuelFlow(); historic.Observe(result); Check(historic.Current is null, "Initial connection never replays an old completed duel");
    // Simulate subsequent permitted packets arriving while the first verdict is still being read.
    var next = initial with { View = initial.View with { Version = result.View.Version + 1, LastDuel = initial.View.LastDuel! with { Id = "second-duel" } } };
    flow.Observe(next); Check(flow.Current!.View.LastDuel!.Id == id, "New duel cannot overwrite a held verdict");
    flow.Complete(id); Check(flow.Current!.View.LastDuel!.Id == "second-duel", "Next duel follows Continue");
    flow.Observe(next); flow.Complete("second-duel"); flow.Observe(next); Check(flow.Current is null, "Completed presentation does not restart on duplicate snapshot");
    flow.Observe(initial with { RoomCode = "NEWROOM" }); Check(flow.Current is not null, "Different match resets presentation history");
    flow.Reset(); Check(flow.Current is null, "Seat handoff clears retained private snapshots");
    session.StartDuelDemo("steel", 205, 1); Check(session.Generation != generation, "A replacement practice can discard queued snapshots from the old match, even with the same seed and scenario");
}

static async Task DemoSession()
{
    using var practice = new MatchSession(); practice.StartPractice(false, true);
    var saved = practice.ExportPracticeReplay();
    using var demo = new MatchSession(); demo.StartDuelDemo("steel", 205, 1);
    Check(demo.Snapshot!.Mode == "Duel Demo" && demo.Snapshot.View.ViewerId == 1, "Correct isolated demo seat");
    await demo.BeginDuelDemo(); await demo.BeginDuelDemo();
    Check(demo.Snapshot!.View.Version == 1, "Begin is idempotent");
    var command = demo.Snapshot.Actions.Single(a => a.Command?.Choice == "deck").Command!;
    await Task.WhenAll(demo.Submit(command), demo.Submit(command));
    Check(demo.Snapshot!.View.Version == 2, "Rapid stale command is rejected");
    var privateCopy = System.Text.Json.JsonSerializer.Deserialize<SessionSnapshot>(System.Text.Json.JsonSerializer.Serialize(demo.Snapshot))!;
    var ownFocus = privateCopy.View.Players[1].Focused!.Single();
    Check(ownFocus.Definition is not null && !ownFocus.FaceUp, "Serialized owner session exposes committed blind focus while it stays face down");
    demo.SwitchPracticeSeat();
    Check(demo.Snapshot!.View.Players[1].Focused!.Length == 0 && demo.Snapshot.View.Players[1].FocusCount == 1, "Counts cross session without hidden identities");
    var card = demo.Snapshot.View.Players[0].Hand.Single(c => c.Definition!.Id == "oracle-l5r-7996");
    await demo.Submit(demo.Snapshot.Actions.First(a => a.Command?.Type == "focus" && a.Command.CardId == card.Id).Command!);
    Check(demo.Snapshot!.View.Players[0].Focused!.Single().Definition!.Id == card.Definition!.Id, "Hand focus remains privately readable off-turn");
    demo.SwitchPracticeSeat();
    Check(demo.Snapshot!.View.Players[1].Focused!.Single().Definition!.Id == ownFocus.Definition!.Id && demo.Snapshot.View.Players[0].Focused!.Length == 0, "Returning to the owner restores its focus without the other seat identity");
    await demo.Submit(demo.Snapshot.Actions.Single(a => a.Type == "strike").Command!);
    var json = System.Text.Json.JsonSerializer.Serialize(demo.Snapshot);
    var copy = System.Text.Json.JsonSerializer.Deserialize<SessionSnapshot>(json)!;
    Check(copy.View.LastDuel is { Complete: true, Result.ChallengerTotal: 8, Result.ChallengedTotal: 7 }, "Retained result survives typed transport envelope");
    Check(copy.View.LastDuel!.Reveals[0].Cards.Length == 2, "Reveal survives immediate resolution");
    var replay = System.Text.Json.JsonSerializer.Deserialize<ReplayRecord>(demo.ExportPracticeReplay()!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    Check(MatchEngine.Replay(replay).GetView(0).LastDuel!.Result!.ChallengerTotal == 8, "Export replays");
    demo.StartDuelDemo("steel", 205, 0); Check(demo.Snapshot!.View.Version == 0, "Restart returns prepared position");
    Check(practice.ExportPracticeReplay() == saved, "Separate practice never advances or resets");
}

static async Task LeaveTable(string url)
{
    using var online = new MatchSession(); await online.CreateOrJoin(url, null, true);
    await WaitFor(() => online.Snapshot is not null && !online.Busy);
    var seat = online.Seat!; var version = online.Snapshot!.View.Version;
    online.LeavePrivateTable(); Check(!online.Online && online.Snapshot is null, "Private seat cleared");
    await online.Resume(url, seat); await WaitFor(() => online.Connected && online.Snapshot is not null && !online.Busy);
    Check(online.Snapshot!.View.WinnerId is null && online.Snapshot.View.Version == version, "Leaving did not concede or advance room");
}

static async Task LivePlay(string url)
{
    using var host = new MatchSession();
    using var guest = new MatchSession();
    RoomSeat? savedHost = null;
    var notices = new ConcurrentQueue<string>();
    host.SeatCreated += (seat, _) => savedHost = seat;
    host.Notice += notices.Enqueue;
    guest.Notice += notices.Enqueue;
    await host.CreateOrJoin(url, null);
    await WaitFor(() => host.Snapshot is not null && !host.Busy);
    Check(savedHost is not null && savedHost == host.Seat, "Creating a room delivers a resumable seat.");
    await guest.CreateOrJoin(url, host.Seat!.RoomCode);
    await WaitFor(() => guest.Snapshot is not null && !guest.Busy);
    await WaitFor(() => host.Snapshot?.PlayersConnected?.All(value => value) == true
        && guest.Snapshot?.PlayersConnected?.All(value => value) == true);
    Check(host.Connected && guest.Connected && host.Online && guest.Online, "Both sessions are connected.");
    Check(host.Seat.PlayerId == 0 && guest.Seat!.PlayerId == 1, "Joining creates the second seat.");
    foreach (var session in new[] { host, guest })
    {
        var snapshot = session.Snapshot!;
        Check(snapshot.Mode == "Private room" && snapshot.RoomCode == host.Seat.RoomCode, "The client consumes the room envelope.");
        Check(snapshot.View.Players[1 - snapshot.View.ViewerId].Hand.Length == 0, "The opponent's hand is private.");
        Check(snapshot.Actions.Length > 0, "Typed action deserialization succeeds.");
        Check(snapshot.Actions.Where(a => a.Command is not null).All(a => a.Command!.PlayerId == session.Seat!.PlayerId
            && a.Command.ExpectedVersion == snapshot.View.Version), "Templates bind the authenticated actor and latest version.");
    }
    var actor = host.Snapshot!.View.PriorityPlayerId == 0 ? host : guest;
    var pass = actor.Snapshot!.Actions.First(a => a.Enabled && a.Command?.Type == "pass").Command!;
    await actor.Submit(pass);
    await WaitFor(() => host.Snapshot!.View.Version == 1 && guest.Snapshot!.View.Version == 1 && !actor.Busy);
    Check(notices.IsEmpty, "An actual legal command reaches the server without schema errors.");

    var count = notices.Count;
    await actor.Submit(new GameCommand("pass", actor.Seat!.PlayerId, 0));
    await WaitFor(() => !actor.Busy && notices.Count > count);
    Check(actor.Snapshot!.View.Version == 1, "A stale rejection clears Busy without modifying the view.");

    // Deliberately supply a different actor: the client must omit it on the wire, and the server must bind the token.
    await actor.Submit(new GameCommand("concede", 1 - actor.Seat.PlayerId, 1));
    await WaitFor(() => host.Snapshot!.View.Version == 2 && guest.Snapshot!.View.Version == 2 && !actor.Busy);
    Check(actor.Snapshot!.View.WinnerId == 1 - actor.Seat.PlayerId,
        "The command payload omits playerId, and the authenticated seat is the player that concedes.");
}

static async Task Reconnect(string url)
{
    using var host = new MatchSession();
    using var guest = new MatchSession();
    await host.CreateOrJoin(url, null);
    await WaitFor(() => host.Snapshot is not null && !host.Busy);
    var saved = host.Seat!;
    var matchKey = host.MatchKey;
    await guest.CreateOrJoin(url, saved.RoomCode);
    await WaitFor(() => guest.Snapshot is not null && !guest.Busy);
    var actor = host.Snapshot!.View.PriorityPlayerId == 0 ? host : guest;
    await actor.Submit(actor.Snapshot!.Actions.First(a => a.Command?.Type == "pass").Command!);
    await WaitFor(() => host.Snapshot!.View.Version == 1 && guest.Snapshot!.View.Version == 1);
    host.Disconnect();
    Check(!host.Connected && !host.Online && !host.Busy, "Disconnect leaves no active transport.");
    await host.Resume(url, saved);
    await WaitFor(() => host.Snapshot?.View.Version == 1 && !host.Busy);
    Check(host.Seat == saved && host.Snapshot!.View.ViewerId == saved.PlayerId, "Resume retains identity and authoritative state.");
    Check(host.MatchKey == matchKey, "Reconnect keeps the inspection identity.");

    // A canceled older Connect/Receive must never disconnect or overwrite the latest session.
    var old = host.Resume(url, saved);
    var latest = host.Resume(url, saved);
    try { await old; } catch (Exception e) when (e is OperationCanceledException or System.Net.WebSockets.WebSocketException or ObjectDisposedException) { }
    await latest;
    await WaitFor(() => host.Connected && host.Snapshot?.View.Version == 1 && !host.Busy);
    await host.Submit(new GameCommand("concede", saved.PlayerId, 1));
    await WaitFor(() => host.Snapshot?.View.Version == 2 && !host.Busy);
    Check(host.Snapshot!.View.WinnerId == 1, "The latest connection remains usable after overlapping reconnects.");
}

static async Task ShownHistoryReconnect()
{
    var directory = Path.Combine(Path.GetTempPath(), "l5r-shown-history-" + Guid.NewGuid().ToString("N"));
    const string room = "SHWNTEST";
    var tokens = new[] { Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N") };
    static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    var engine = MatchEngine.CreateDeckSearchShowcase(315);
    var source = engine.State.Cards.Values.Single(c => c.OwnerId == 0 && c.DefinitionId == "oracle-l5r-10292").Id;
    foreach (var command in new[] { new GameCommand("useAbility", 0, 0, source, AbilityId: "shizuka-search"), new GameCommand("chooseSearchDeck", 0, 1, Choice: "Fate") })
        Check(engine.Execute(command).Accepted, "Prepare private search");
    await new RoomStore(directory).SaveAsync(new PersistedRoom(1, room, DateTimeOffset.UtcNow,
        tokens.Select((token, seat) => new SeatCredential(seat, Hash(token))).ToArray(), engine.ExportReplay(), []));
    var server = await ServerApplication.BuildAsync(["--urls", "http://127.0.0.1:0", "--L5R:StoragePath", directory, "--Logging:LogLevel:Default", "Critical"]);
    try
    {
        await server.StartAsync(); var address = server.Urls.Single();
        using var first = new MatchSession(); using var other = new MatchSession();
        var seats = tokens.Select((token, seat) => new RoomSeat(room, seat, token, 1, Protocol.ContentVersion)).ToArray();
        await first.Resume(address, seats[0]); await other.Resume(address, seats[1]);
        await WaitFor(() => first.Snapshot?.PlayersConnected?.All(x => x) == true && other.Snapshot?.PlayersConnected?.All(x => x) == true);
        Check(other.Snapshot!.View.PendingSearch is null && (other.Snapshot.View.ShownCards?.Length ?? 0) == 0, "No private search contents reach the opponent before the reveal");
        await first.Submit(first.Snapshot!.Actions.Single(a => a.Type == "selectSearchCard" && a.Command?.Choice == "training-terrain").Command!);
        await WaitFor(() => first.Snapshot?.View.ShownCards?.Length == 1 && other.Snapshot?.View.ShownCards?.Length == 1);
        var before = JsonSerializer.Serialize(other.Snapshot!.View.ShownCards);
        Check(before == JsonSerializer.Serialize(first.Snapshot!.View.ShownCards), "Both sessions receive exactly the same public show");
        var key = other.MatchKey; other.Disconnect(); await other.Resume(address, seats[1]);
        await WaitFor(() => other.Connected && !other.Busy && other.Snapshot?.View.ShownCards?.Length == 1);
        Check(other.MatchKey == key && JsonSerializer.Serialize(other.Snapshot!.View.ShownCards) == before, "Reconnect restores the history with stable inspection identity");
        var visible = other.Snapshot!.View;
        Check(visible.Players[0].Hand.Length == 0 && visible.PendingSearch is null, "Reconnect cannot expose the searcher's hidden hand or viewer");
        Check(!before.Contains("training-costed-region"), "An unshown deck card is absent from history");
        using var fresh = new MatchSession(); await fresh.Resume(address, seats[1]);
        await WaitFor(() => fresh.Snapshot?.View.ShownCards?.Length == 1);
        Check(JsonSerializer.Serialize(fresh.Snapshot!.View.ShownCards) == before, "A fresh client reconstructs history entirely from the server");
        var oldKey = fresh.MatchKey; fresh.StartPractice(false, true);
        Check(fresh.MatchKey != oldKey && (fresh.Snapshot!.View.ShownCards?.Length ?? 0) == 0, "A new match cannot inherit the old inspection history");
    }
    finally
    {
        await server.StopAsync(); await server.DisposeAsync();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

// The outgoing mapping is an explicit field list, so a new GameCommand field is silently dropped online unless it is
// added there (DeckPosition and TargetPlayerId were). Compare against GameCommand by reflection, and prove the
// server's strict schema (unmapped members are errors) accepts exactly what the client sends.
static void OutgoingPayload()
{
    var command = new GameCommand("useAbility", 1, 9, "card", ["source"], 2, true, true, new() { ["unit"] = 1 }, ["card-a"], "target",
        "ability", "performer", "choice", "Top", 1, 4, "destroy", [new SlotSelection("slot", ["card-b"], 1)]);
    var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    var json = System.Text.Json.JsonSerializer.Serialize(MatchSession.ToWire(command), web);
    using var document = System.Text.Json.JsonDocument.Parse(json);
    var sent = document.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    var expected = typeof(GameCommand).GetProperties().Where(p => p.Name is not ("PlayerId" or "ExpectedVersion" or "EqualityContract"))
        .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet();
    Check(sent.SetEquals(expected), $"The client wire payload and GameCommand disagree: missing [{string.Join(",", expected.Except(sent))}] extra [{string.Join(",", sent.Except(expected))}]");
    var wire = System.Text.Json.JsonSerializer.Deserialize<WireGameCommand>(json, Protocol.Json)!;
    Check(System.Text.Json.JsonSerializer.Serialize(wire.BindToSeat(1, 9)) == System.Text.Json.JsonSerializer.Serialize(command), "The server binds exactly the command the client meant, GoldAmount included.");
    // One advertised protocol for both ends: the client's hello may never claim a version whose members the server rejects.
    Check(MatchEngine.ProtocolVersion == Protocol.Version, $"The client advertises protocol {MatchEngine.ProtocolVersion} while the server speaks {Protocol.Version}");
}

// Issue #917: a view member that is declared WhenWritingNull (MatchView.ResolutionArea) is absent from every payload outside an open action, by design (byte-identical to before);
// ResolutionAreaCrossesTheWire proves it arrives, and arrives complete, while an action is open.
static bool OmittedWhenNull(System.Reflection.PropertyInfo property) =>
    property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), false).Cast<System.Text.Json.Serialization.JsonIgnoreAttribute>().Any(a => a.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

static void ResolutionAreaCrossesTheWire()
{
    var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    var s = MatchEngine.CreateEconomyScenario().State;
    foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
    s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
    CardInstance AddCard(string def, int owner, Zone zone, int? province = null) { var c = new CardInstance { Id = $"wire-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, ProvinceIndex = province }; s.Cards.Add(c.Id, c); if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id); return c; }
    var strike = AddCard("oracle-l5r-7525", 0, Zone.Hand); AddCard("oracle-l5r-5599", 0, Zone.Hand);
    AddCard("training-scout-samurai", 0, Zone.Battlefield, 0); var enemy = AddCard("training-lion-samurai", 1, Zone.Battlefield, 0);
    var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [s])!;
    string Wire(int viewer) { var view = engine.GetView(viewer); return System.Text.Json.JsonSerializer.Serialize(new StateMessage("state", "ABCDEFGH", viewer, view.Version, view, engine.QueryActions(viewer).Select(AvailableAction.FromCore).ToArray(), [true, true]), Protocol.Json); }
    foreach (var viewer in new[] { 0, 1 }) Check(!Wire(viewer).Contains("resolutionArea"), "outside an open action the payload has no resolutionArea key");
    var announce = engine.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "strategic-strike-attack" && a.CardId == strike.Id && (a.TargetIds?.Contains(enemy.Id) ?? false)).Command! with { TargetId = enemy.Id };
    var result = engine.Execute(announce); Check(result.Accepted, "announcement refused: " + result.Error);
    foreach (var viewer in new[] { 0, 1 })
    {
        using var document = System.Text.Json.JsonDocument.Parse(Wire(viewer));
        var wireView = document.RootElement.GetProperty("view");
        Check(wireView.TryGetProperty("resolutionArea", out var area) && area.GetArrayLength() == 1 && area[0].GetProperty("card").GetProperty("id").GetString() == strike.Id, $"seat {viewer}: the resolution area crosses the wire with the card's identity");
        var received = wireView.Deserialize<MatchView>(web)!;
        Check(System.Text.Json.JsonSerializer.Serialize(received, web) == System.Text.Json.JsonSerializer.Serialize(engine.GetView(viewer), web), $"seat {viewer}: the client's view equals the engine's");
        Check(received.ResolutionArea![0].Card.Zone == Zone.Resolving && received.Players[0].Hand.All(c => c.Id != strike.Id), $"seat {viewer}: Resolving survives the wire and the card is not in a hand");
    }
}

// Issue #312: MatchView crosses the wire as the core record itself (the server serializes StateMessage.View, the client
// deserializes MatchView with the same web defaults), so a new view field can only be lost by a mapping layer or an ignore
// attribute. Prove it for the two new fields and, by reflection, for EVERY MatchView property, on the exact server envelope; and
// prove every search action (deck choice, select, pass, province with its Gold quote, discard) survives the AvailableAction mapping.
static void SearchViewsCrossTheWire()
{
    var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    static string SourceOf(MatchEngine e, string definition) => e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == definition).Id;
    // A fresh engine rebuilt from the journal, the way a restarted server holds the room: stage snapshots the wire must carry.
    static MatchEngine Snap(MatchEngine e) => MatchEngine.Replay(e.ExportReplay());
    static void Step(MatchEngine e, GameCommand command) { var result = e.Execute(command); if (!result.Accepted) throw new InvalidOperationException(result.Error); }
    var stages = new List<(string Name, MatchEngine Engine)>();
    var dynasty = MatchEngine.CreateDeckSearchShowcase();
    Step(dynasty, new("useAbility", 0, dynasty.Version, SourceOf(dynasty, "oracle-l5r-10292"), AbilityId: "shizuka-search")); stages.Add(("deck choice", Snap(dynasty)));
    Step(dynasty, new("chooseSearchDeck", 0, dynasty.Version, Choice: "Dynasty")); stages.Add(("list", Snap(dynasty)));
    Step(dynasty, new("selectSearchCard", 0, dynasty.Version, Choice: "training-costed-region")); stages.Add(("province", Snap(dynasty)));
    Step(dynasty, new("chooseSearchProvince", 0, dynasty.Version, Province: 0, SourceIds: dynasty.QueryActions(0).First(a => a.Type == "chooseSearchProvince").Command!.SourceIds)); stages.Add(("done", Snap(dynasty)));
    var fate = MatchEngine.CreateDeckSearchShowcase();
    Step(fate, new("useAbility", 0, fate.Version, SourceOf(fate, "oracle-l5r-10292"), AbilityId: "shizuka-search"));
    Step(fate, new("chooseSearchDeck", 0, fate.Version, Choice: "Fate")); Step(fate, new("selectSearchCard", 0, fate.Version, Choice: "training-terrain")); stages.Add(("discard", Snap(fate)));

    var seenTypes = new HashSet<string>();
    foreach (var (stage, engine) in stages)
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = engine.GetView(viewer); var actions = engine.QueryActions(viewer);
            // Exactly the server's envelope, serialized with the server's options; the client reads "view" and "actions" as MatchSession.Receive does.
            var message = new StateMessage("state", "ABCDEFGH", viewer, view.Version, view, actions.Select(AvailableAction.FromCore).ToArray(), [true, true]);
            var json = System.Text.Json.JsonSerializer.Serialize(message, Protocol.Json);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var wireView = document.RootElement.GetProperty("view");
            var received = wireView.Deserialize<MatchView>(web)!;
            Check(System.Text.Json.JsonSerializer.Serialize(received, web) == System.Text.Json.JsonSerializer.Serialize(view, web), $"{stage}/viewer {viewer}: the client's MatchView differs from the engine's");
            foreach (var property in typeof(MatchView).GetProperties())
                Check(wireView.TryGetProperty(char.ToLowerInvariant(property.Name[0]) + property.Name[1..], out _) || OmittedWhenNull(property), $"MatchView.{property.Name} is missing from the wire envelope: a field would be dropped online");
            var receivedActions = document.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(web)!;
            Check(receivedActions.Length == actions.Length, $"{stage}: an action was dropped");
            for (var i = 0; i < actions.Length; i++)
            {
                // The client binds the actor and version onto every template (MatchSession.Receive); compare after the same binding.
                var got = receivedActions[i] with { Command = receivedActions[i].Command is null ? null : receivedActions[i].Command! with { PlayerId = viewer, ExpectedVersion = view.Version } };
                Check(System.Text.Json.JsonSerializer.Serialize(got, web) == System.Text.Json.JsonSerializer.Serialize(actions[i], web), $"{stage}: action '{actions[i].Type}' changed on the wire");
                if (actions[i].Type is "chooseSearchDeck" or "selectSearchCard" or "passSearch" or "chooseSearchProvince" or "chooseSearchDiscard") seenTypes.Add(actions[i].Type);
            }
            var searcher = viewer == 0;
            if (stage != "done")
                Check(searcher ? received.PendingSearch is not null : received.PendingSearch is null, $"{stage}/viewer {viewer}: the deck viewer must reach only the searcher");
            switch (stage)
            {
                case "deck choice":
                    if (searcher) Check(received.PendingSearch!.DeckChoices!.Length == 2 && received.PendingSearch.Entries is null, "The deck choice carries counts and no contents");
                    else Check(!json.Contains("training-costed-region") && !json.Contains("oracle-l5r-9865"), "The opponent's envelope names no deck card");
                    break;
                case "list":
                    if (searcher) Check(received.PendingSearch!.Entries!.Length > 1 && received.PendingSearch.Entries.All(e => e.Definition.Id == e.DefinitionId) && received.PendingSearch.Entries.Any(e => !e.Eligible && e.Reason is not null), "The entries carry their definition, eligibility and reason");
                    else Check(!json.Contains("training-costed-region") && !json.Contains("oracle-l5r-9865"), "The opponent's envelope names no deck card");
                    break;
                case "province":
                    if (searcher) Check(receivedActions.Where(a => a.Type == "chooseSearchProvince").All(a => a.Gold is { Cost: 2, Kind: GoldCostKind.Fixed } && a.Command!.SourceIds is { Length: > 0 }), "The Gold quote and the suggested payment reach the client");
                    Check(received.ShownCards is [{ Definition.Id: "training-costed-region" }] && received.ShownCards[0].ShownByPlayerId == 0, "The shown card is already public");
                    break;
                case "done":
                    Check(received.PendingSearch is null && received.ShownCards is [{ Definition.Id: "training-costed-region", Deck: "Dynasty" }], "The public record survives after the search");
                    break;
                case "discard":
                    if (searcher) Check(received.PendingSearch!.Stage == "ChooseDiscard" && received.PendingSearch.Selected?.Id == "training-terrain", "The discard stage shows the found card");
                    Check(received.ShownCards is [{ Definition.Id: "training-terrain", Deck: "Fate" }], "Both players receive the record");
                    break;
            }
        }
    foreach (var type in new[] { "chooseSearchDeck", "selectSearchCard", "chooseSearchProvince", "chooseSearchDiscard" })
        Check(seenTypes.Contains(type), $"The test never carried a {type} action over the wire");
    // The commands a search sends fit the strict server command with nothing dropped: Choice, Province, SourceIds and TargetId are all carried.
    var command = new GameCommand("chooseSearchProvince", 1, 9, Choice: "Dynasty", Province: 2, SourceIds: ["a"], TargetId: "hand-card");
    var wire = System.Text.Json.JsonSerializer.Serialize(MatchSession.ToWire(command), web);
    var bound = System.Text.Json.JsonSerializer.Deserialize<WireGameCommand>(wire, Protocol.Json)!.BindToSeat(1, 9);
    Check(System.Text.Json.JsonSerializer.Serialize(bound) == System.Text.Json.JsonSerializer.Serialize(command), "A search command survives the client payload, the server's strict schema and the seat binding");
}

// Issue #322: the look view crosses the wire inside MatchView like the search view does, so prove it the same way -- on the exact server
// envelope, every MatchView property present, the client's deserialization identical to the engine's, the viewer's ordered cards reaching
// only the viewer (including when the viewer looks at the OPPONENT's deck), and every look action (deck choice, arrangement) surviving
// the AvailableAction mapping and the seat-bound command.
static void LookViewsCrossTheWire()
{
    var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    static string SourceOf(MatchEngine e) => e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-look-source").Id;
    static MatchEngine Snap(MatchEngine e) => MatchEngine.Replay(e.ExportReplay());
    static void Step(MatchEngine e, GameCommand command) { var result = e.Execute(command); if (!result.Accepted) throw new InvalidOperationException(result.Error); }
    var stages = new List<(string Name, MatchEngine Engine)>();
    var game = MatchEngine.CreateDeckSearchShowcase();
    Step(game, new("useAbility", 0, game.Version, SourceOf(game), AbilityId: "training-look-any")); stages.Add(("deck choice", Snap(game)));
    Step(game, new("chooseLookDeck", 0, game.Version, Choice: "Fate", TargetPlayerId: 1)); stages.Add(("opponent's deck", Snap(game)));
    Step(game, new("arrangeTopCards", 0, game.Version, CardIds: ["2:top", "0:discard", "1:bottom"])); stages.Add(("done", Snap(game)));
    var own = MatchEngine.CreateDeckSearchShowcase();
    Step(own, new("useAbility", 0, own.Version, SourceOf(own), AbilityId: "training-look-show"));
    stages.Add(("own deck, two cards", Snap(own)));
    Step(own, own.QueryActions(0).First(a => a.Type == "arrangeTopCards").Command!); stages.Add(("shown", Snap(own)));

    var seen = new HashSet<string>();
    foreach (var (stage, engine) in stages)
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = engine.GetView(viewer); var actions = engine.QueryActions(viewer);
            var message = new StateMessage("state", "ABCDEFGH", viewer, view.Version, view, actions.Select(AvailableAction.FromCore).ToArray(), [true, true]);
            var json = System.Text.Json.JsonSerializer.Serialize(message, Protocol.Json);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var wireView = document.RootElement.GetProperty("view");
            var received = wireView.Deserialize<MatchView>(web)!;
            Check(System.Text.Json.JsonSerializer.Serialize(received, web) == System.Text.Json.JsonSerializer.Serialize(view, web), $"{stage}/viewer {viewer}: the client's MatchView differs from the engine's");
            foreach (var property in typeof(MatchView).GetProperties())
                Check(wireView.TryGetProperty(char.ToLowerInvariant(property.Name[0]) + property.Name[1..], out _) || OmittedWhenNull(property), $"MatchView.{property.Name} is missing from the wire envelope: a field would be dropped online");
            var receivedActions = document.RootElement.GetProperty("actions").Deserialize<ActionOption[]>(web)!;
            Check(receivedActions.Length == actions.Length, $"{stage}: an action was dropped");
            for (var i = 0; i < actions.Length; i++)
            {
                var got = receivedActions[i] with { Command = receivedActions[i].Command is null ? null : receivedActions[i].Command! with { PlayerId = viewer, ExpectedVersion = view.Version } };
                Check(System.Text.Json.JsonSerializer.Serialize(got, web) == System.Text.Json.JsonSerializer.Serialize(actions[i], web), $"{stage}: action '{actions[i].Type}' changed on the wire");
                if (actions[i].Type is "chooseLookDeck" or "arrangeTopCards") seen.Add(actions[i].Type);
            }
            var viewerSide = viewer == 0;
            if (stage is "deck choice" or "opponent's deck" or "own deck, two cards")
                Check(viewerSide ? received.PendingLook is not null : received.PendingLook is null, $"{stage}/viewer {viewer}: the look reaches only the viewer");
            switch (stage)
            {
                case "deck choice":
                    if (viewerSide) Check(received.PendingLook!.DeckChoices!.Length == 4 && received.PendingLook.Cards is null && received.PendingLook.DeckChoices.Count(d => !d.OwnDeck) == 2, "Four decks offered, two of them the opponent's, and no cards before one is chosen");
                    break;
                case "opponent's deck":
                    if (viewerSide) Check(received.PendingLook is { Stage: "Arrange", OwnDeck: false, DeckOwnerId: 1 } l && l.Cards!.Length == 3 && l.Cards.All(c => c.Definition.Id == c.DefinitionId) && l.Cards.Select(c => c.Position).SequenceEqual(new[] { 0, 1, 2 }), "The opponent's top cards, in order, with their definitions");
                    else foreach (var card in engine.State.Players[1].FateDeck.Take(3)) Check(!json.Contains(engine.State.Cards[card].DefinitionId) && !json.Contains('"' + card + '"'), "The deck owner's envelope names a card of the deck being looked at");
                    break;
                case "own deck, two cards":
                    if (viewerSide) Check(received.PendingLook!.Requested == 2 && received.PendingLook.Cards!.Length == 2 && received.PendingLook.Rules![0].Disposition == "show" && received.PendingLook.Rules[0].Min == 1, "The caller's count and the required show reach the client");
                    break;
                case "shown":
                    Check(received.PendingLook is null && received.ShownCards is [{ Deck: "Fate" }] && received.ShownCards[0].Context.Contains("Fate deck look"), "The show is public and the look is over");
                    break;
            }
        }
    foreach (var type in new[] { "chooseLookDeck", "arrangeTopCards" }) Check(seen.Contains(type), $"The test never carried a {type} action over the wire");
    // The commands a look sends fit the strict server command: the deck choice rides Choice + TargetPlayerId, the arrangement rides CardIds.
    var command = new GameCommand("arrangeTopCards", 1, 9, CardIds: ["1:top", "0:bottom"], Choice: "Fate", TargetPlayerId: 0);
    var wire = System.Text.Json.JsonSerializer.Serialize(MatchSession.ToWire(command), web);
    var bound = System.Text.Json.JsonSerializer.Deserialize<WireGameCommand>(wire, Protocol.Json)!.BindToSeat(1, 9);
    Check(System.Text.Json.JsonSerializer.Serialize(bound) == System.Text.Json.JsonSerializer.Serialize(command), "A look command survives the client payload, the server's strict schema and the seat binding");
}

static async Task InvalidConnections(string url)
{
    using var session = new MatchSession();
    var insecureRejected = false;
    try { await session.CreateOrJoin("http://example.com", null); }
    catch (InvalidOperationException) { insecureRejected = true; }
    Check(insecureRejected && !session.Busy && !session.Connected, "Plaintext remote servers are rejected before sending a request.");
    var missingRejected = false;
    try { await session.CreateOrJoin(url, "NOTAROOM"); }
    catch (InvalidOperationException) { missingRejected = true; }
    Check(missingRejected && !session.Busy && !session.Connected, "An invalid room leaves the client usable.");
}

static async Task WaitFor(Func<bool> condition)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (!condition()) await Task.Delay(10, timeout.Token);
}
// Issue #501: the new optional CardView fields (item 32's copy frame, the real owner of a controlled card) are carried online exactly as the engine sets them.
static void CopyAndControlFieldsCrossTheWire()
{
    var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    var engine = MatchEngine.CreateEconomyScenario();
    var view = engine.GetView(0);
    var home = view.Players[0].Home;
    Check(home.Length >= 2, "the scenario has two cards at home");
    var marked = home.Select((c, i) => i == 0 ? c with { IsCopy = true, CopyOfCardId = "original-card", CopyOfDefinitionId = "training-lion-veteran", CopyConditions = "Removed from the game when your next turn begins." }
        : i == 1 ? c with { RealOwnerId = 1 } : c).ToArray();
    var players = view.Players.Select((p, i) => i == 0 ? p with { Home = marked } : p).ToArray();
    var sent = view with { Players = players };
    var message = new StateMessage("state", "ABCDEFGH", 0, sent.Version, sent, [], [true, true]);
    var json = System.Text.Json.JsonSerializer.Serialize(message, Protocol.Json);
    using var document = System.Text.Json.JsonDocument.Parse(json);
    var received = document.RootElement.GetProperty("view").Deserialize<MatchView>(web)!;
    Check(System.Text.Json.JsonSerializer.Serialize(received, web) == System.Text.Json.JsonSerializer.Serialize(sent, web), "the client's MatchView differs from the engine's");
    var copy = received.Players[0].Home[0];
    Check(copy.IsCopy && copy.CopyOfCardId == "original-card" && copy.CopyOfDefinitionId == "training-lion-veteran" && copy.CopyConditions == "Removed from the game when your next turn begins.", "the copy's frame fields arrived");
    Check(received.Players[0].Home[1].RealOwnerId == 1 && !received.Players[0].Home[1].IsCopy, "the real owner arrived");
    Check(received.Players[1].Home.All(c => !c.IsCopy && c.CopyOfCardId is null && c.RealOwnerId is null), "ordinary cards carry none of them");
}

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

static async Task PublishedPlay(string url)
{
    using var crane = new MatchSession(); using var lion = new MatchSession();
    await crane.CreateOrJoin(url, null, published: true);
    await WaitFor(() => crane.Snapshot is not null && !crane.Busy);
    await lion.CreateOrJoin(url, crane.Seat!.RoomCode);
    await WaitFor(() => lion.Snapshot is not null && !lion.Busy && crane.Snapshot?.PlayersConnected?.All(p => p) == true);
    Check(lion.Snapshot!.View.Scenario == PublishedContent.Scenario && lion.Snapshot.View.PriorityPlayerId == 1, "Published setup reaches both clients.");
    // Battle Phase timing (issue #310): the public battle-step view is part of every state message, identical for both seats, and survives the
    // strict server serializer and the client's own lenient one alike (an added view field must be optional so an older payload still parses).
    Check(lion.Snapshot.View.BattleStep is { Segment: BattleSteps.None, Step: BattleSteps.None, DecisionPlayerId: null } && crane.Snapshot!.View.BattleStep is { Segment: BattleSteps.None },
        "The battle-step view crosses the live wire (outside the Attack Phase: nothing in progress).");
    var stepView = lion.Snapshot.View with
    {
        BattleStep = new BattleStepView(BattleSteps.FightBattles, BattleSteps.Engage, 0, 0, 2, 1, BattleSteps.ReasonDefenderOverride, false,
        [new FirstOpportunityClaim(0, "Naval", "card-1"), new FirstOpportunityClaim(1, "Engage", null)], BattleSteps.LimitPerformerOnly, "card-2", 1, ["card-3", "delayed:0"], "card-3", "engageReaction",
        [new BeforeResolutionEffectView("card-3", BattleSteps.EffectTrait, 1, "card-3", "Heart of Rokugan"), new BeforeResolutionEffectView("delayed:0", BattleSteps.EffectDelayed, 0, "card-4", "bow Daidoji Yuki")])
    };
    Check(new BattleStepView(BattleSteps.Declaration, BattleSteps.DeclareOrDecline, 0, 0, null, null, null, false, [], null, null, 0, [], null, null) is { Segment: "Declaration", Step: "DeclareOrDecline" },
        "The Declaration Segment is one of the published steps (issue #354).");
    // The same discipline the GameCommand wire test applies: a member added to the public view later cannot silently fail to cross the wire.
    using (var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(stepView, Protocol.Json)))
        foreach (var type in new[] { typeof(MatchView), typeof(BattleStepView), typeof(FirstOpportunityClaim), typeof(BeforeResolutionEffectView) })
        {
            var element = type == typeof(MatchView) ? json.RootElement : type == typeof(BattleStepView) ? json.RootElement.GetProperty("battleStep")
                : type == typeof(BeforeResolutionEffectView) ? json.RootElement.GetProperty("battleStep").GetProperty("beforeResolutionEffects")[0] : json.RootElement.GetProperty("battleStep").GetProperty("engageClaims")[0];
            foreach (var property in type.GetProperties())
                Check(element.TryGetProperty(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name), out _) || OmittedWhenNull(property), $"{type.Name}.{property.Name} is missing from the serialized view.");
        }
    foreach (var options in new[] { Protocol.Json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) })
    {
        var message = new StateMessage("state", "ROOM", 0, 1, stepView, [], [true, true]);
        var round = System.Text.Json.JsonSerializer.Deserialize<StateMessage>(System.Text.Json.JsonSerializer.Serialize(message, options), Protocol.Json)!;
        Check(System.Text.Json.JsonSerializer.Serialize(round.View.BattleStep) == System.Text.Json.JsonSerializer.Serialize(stepView.BattleStep),
            "Every BattleStepView field, claims included, round-trips through the protocol serializer.");
        var older = System.Text.Json.JsonSerializer.Deserialize<StateMessage>(System.Text.Json.JsonSerializer.Serialize(message with { View = stepView with { BattleStep = null } }, options), Protocol.Json)!;
        Check(older.View.BattleStep is null, "A payload without the field still deserializes.");
    }
    var before = lion.Snapshot.View.Players[1].Provinces.Select(p => p.Card!.Id).ToArray();
    var option = lion.Snapshot.Actions.First(a => a.Enabled && a.Command?.AbilityId == "border-first");
    await lion.Submit(option.Command! with { CardIds = [before[0], before[2]] });
    await WaitFor(() => crane.Snapshot!.View.Version == 1 && lion.Snapshot!.View.Version == 1 && !lion.Busy);
    Check(lion.Snapshot!.View.Players[1].Provinces[0].Card!.Id != before[0], "Ability ID and multi-card selection were sent to the authoritative engine.");
    Check(lion.Snapshot.View.Players[1].Provinces[1].Card!.Id == before[1], "Unselected province was retained.");
    Check(crane.Snapshot!.View.Players[1].Provinces[2].Card!.Definition is not null, "Opponent receives revealed replacements.");
    lion.Disconnect(); await lion.Resume(url, lion.Seat!);
    await WaitFor(() => lion.Snapshot?.View.Version == 1 && !lion.Busy && lion.Connected);
    Check(lion.Snapshot!.View.Events.Any(e => e.Type == "abilityUsed"), "Ability state survives reconnection.");
    var wire = WireGameCommand.FromCore(new GameCommand("useAbility", 1, 1, "source", TargetId: "target", AbilityId: "shinden-choice", PerformerId: "performer", Choice: "honor"));
    var bound = wire.BindToSeat(0, 7);
    Check(bound.AbilityId == "shinden-choice" && bound.PerformerId == "performer" && bound.Choice == "honor" && bound.PlayerId == 0 && bound.ExpectedVersion == 7, "New fields survive wire conversion while actor/version remain authenticated.");
}

static async Task CustomPlay()
{
    using var session = new MatchSession { LocalDisplayName = "Custom player" };
    session.StartCustomPractice(CustomPractice.Preset("Lion"), CustomPractice.Preset("Crane"));
    var initial = session.Snapshot!;
    Check(initial.View.ViewerId == 0 && initial.View.Players[0].Clan == "Lion" && initial.View.Players[0].Name == "Custom player", "Chosen deck belongs to local seat zero.");
    try { session.StartCustomPractice(new Deck("Emperor", []), CustomPractice.Preset("Crane")); throw new Exception("Invalid deck accepted"); } catch (ArgumentException) { }
    Check(ReferenceEquals(session.Snapshot, initial), "Invalid selection must not discard a running Practice.");
    await session.Submit(new GameCommand("concede", 0, initial.View.Version));
    Check(session.Snapshot!.View.WinnerId == 1, "Custom commands execute in the authoritative local engine.");
}
