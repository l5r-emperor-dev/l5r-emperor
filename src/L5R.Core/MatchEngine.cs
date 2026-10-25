using System.Text.Json;

namespace L5R.Core;

/// <summary>Single-threaded authoritative simulator. Execute validates on a detached state and commits atomically.</summary>
public sealed partial class MatchEngine
{
    public const string ContentVersion = "emperor-lion-crane-0.5.0";
    /// <summary>The wire protocol both the server and the client advertise in the handshake. One constant, so a client
    /// can never claim a protocol whose command/state members the peer would reject (UnmappedMemberHandling.Disallow).
    /// 2: commands carry createdCard, and the state message carries the attack cue and per-action attack/movement/
    /// alternative-cost metadata.
    /// 4: PlayerView no longer carries DynastyDeckCount/FateDeckCount, and the look/search deck views no longer carry CardsRemaining (current deck sizes are hidden information, EE-STATE-014).</summary>
    public const int ProtocolVersion = 4;
    private MatchState _state;
    private readonly ulong _seed;
    private readonly string _scenario;
    private readonly List<GameCommand> _commands = [];
    private static readonly string[] Limits = [
        "Training fixtures only. These are synthetic cards and decks, not verified Emperor tournament content.",
        "Implemented: payment-specific Gold, Clan purchase options, Proclaim, infantry battles, basic attachments and two fixture Battle actions.",
        "Not implemented: published card abilities, Border Keep filtering, Imperial Favor, cavalry, duels, dishonored cards, Rings, events, regions, spells and general nested reactions."
    ];

    private MatchEngine(MatchState state, ulong seed, string scenario) { _state = state; _seed = seed; _scenario = scenario; EngineHooks.Initialize(); AssignUnitNumbers(); }
    // Issue #611: every Personality in play gets a match-unique number the first time it is seen there (once per committed command and at construction).
    // Cards are walked in dictionary (opaque-id) order, so which of several units entering together gets the lower number carries no setup information.
    private void AssignUnitNumbers()
    {
        foreach (var card in _state.Cards.Values)
            if (card.UnitNumber == 0 && InPlay(card) && Definition(card).Type == CardType.Personality) card.UnitNumber = ++_state.UnitSequence;
    }
    /// <summary>Detached diagnostic snapshot, containing hidden information. Never send to a client.</summary>
    public MatchState State => Copy(_state);
    public long Version => _state.Version;
    // Scalar reads for per-frame callers: State is a full detached copy (issue #875), so a client that only needs these asks for them directly.
    public int? WinnerId => _state.WinnerId;
    public int ActivePlayerId => _state.ActivePlayerId;
    public int PriorityPlayerId => _state.PriorityPlayerId;
    public bool HasPendingChoice => _state.PendingChoice is not null;
    public static MatchEngine CreateStarterMatch(ulong seed = 20260906) => Create(seed, "starter");
    public static MatchEngine CreateEconomyScenario(ulong seed = 20260906) => Create(seed, "economy");
    public static MatchEngine CreatePublishedMatch(ulong seed = 20260906) => Create(seed, PublishedContent.Scenario);
    /// <summary>Fixture match for the deck search primitive (issue #312): both players have Ikoma Shizuka and the
    /// training search source unbowed at home, decks that hold Regions and Terrains, and the Action Phase open for
    /// player 0. An ordinary, replayable engine state (its own scenario name, so ExportReplay/Replay and the server's
    /// journal recovery cover it) built for tests and for the UI issues' scripted capture -- no menu or room reaches it.</summary>
    public static MatchEngine CreateDeckSearchShowcase(ulong seed = 20260906) => Create(seed, DeckSearchScenario);
    public const string DeckSearchScenario = "deck-search";
    /// <summary>Fixture matches for the copy wizard and the Either/Or wizard (issue #338): ordinary, replayable engine states (each its own scenario name,
    /// so ExportReplay/Replay and the server's journal recovery cover them) built for tests and for the UI issues' scripted captures -- no menu or room
    /// reaches them. In the copy showcase player 0 controls Fukuzo, a Training skin receiver and a Training skin donor at home, and player 1's discard
    /// pile holds real Personalities to copy from; in the Either/Or showcase player 0 holds Unfortunate Hesitation, Yoritomo Sakuma and Shosuro Rin
    /// and player 1 Yogo Adi and a Training Either/Or source. Both open in player 0's Action Phase.</summary>
    public static MatchEngine CreateCopyShowcase(ulong seed = 20260906) => Create(seed, CopyShowcaseScenario);
    public static MatchEngine CreateAbilityGrantShowcase(ulong seed = 20260906) => Create(seed, AbilityGrantShowcaseScenario);
    public const string AbilityGrantShowcaseScenario = "ability-grant-showcase";
    public static MatchEngine CreateEitherOrShowcase(ulong seed = 20260906) => Create(seed, EitherOrShowcaseScenario);
    public const string CopyShowcaseScenario = "copy-showcase";
    public const string EitherOrShowcaseScenario = "either-or-showcase";
    /// <summary>Fixture match for the opposing player's variable payment (issue #373): player 1 (the Lion) holds Virtuous Charity and both players have several
    /// Gold sources at home; player 0 (the Crane) has the Action Phase. Player 0 passes, player 1 plays Virtuous Charity (it is not their turn) and chooses an
    /// amount, and player 0 then decides how much to pay.</summary>
    public static MatchEngine CreateOpponentPaymentShowcase(ulong seed = 20260906) => Create(seed, OpponentPaymentShowcaseScenario);
    public const string OpponentPaymentShowcaseScenario = "opponent-payment-showcase";
    // Frozen deck lists (PublishedContent.TestSnapshot) for the C# test suite only -- never used by the
    // client or RoomManager. Editing the live deck (CreatePublishedMatch) for manual playtesting must
    // never silently change what this exercises.
    public static MatchEngine CreatePublishedTestMatch(ulong seed = 20260906) => Create(seed, PublishedContent.TestScenario);
    internal static MatchEngine FromTestState(MatchState state) => new(Copy(state), 1, "test-only");

    // Trial-validate a command against the LIVE state directly, instead of RoomManager replaying every prior
    // command from turn one (issue #550, O(n) per submission -> O(n^2) over a match). Execute() already
    // copy-then-rolls-back _state internally on rejection, and every other instance field it touches is reset
    // to a fixed default at the top of every call (never carries meaning across commands) -- so cloning _state,
    // _commands (only ever read by ExportReplay, never re-executed here) and _customDecks is the whole state
    // this engine has. Public so RoomManager (a separate assembly) can call it; a trial substitute for
    // Replay(ExportReplay()), never crosses a trust boundary itself.
    public MatchEngine Clone()
    {
        var clone = new MatchEngine(Copy(_state), _seed, _scenario);
        clone._commands.AddRange(_commands);
        clone._customDecks = _customDecks?.Select(CustomPractice.CopyDeck).ToArray();
        return clone;
    }

    public ReplayRecord ExportReplay() => new(ContentVersion, _scenario, _seed, _commands.Select(Copy).ToArray(), _customDecks?.Select(CustomPractice.CopyDeck).ToArray(), _state.IdleBattlePasses);
    public static MatchEngine Replay(ReplayRecord replay)
    {
        if (replay.ContentVersion != ContentVersion) throw new ArgumentException("Replay content version is not supported.");
        var engine = replay.Scenario == CustomPractice.Scenario
            ? replay.CustomDecks is { Length: 2 } decks ? CreateCustomMatch(replay.Seed, decks[0], decks[1]) : throw new ArgumentException("Custom replay is missing its two deck snapshots.")
            : Create(replay.Seed, replay.Scenario);
        if (replay.IdleBattlePasses) engine.AllowIdleBattlePasses(); // match setup, before the first command (issue #894)
        foreach (var command in replay.Commands)
        {
            var result = engine.Execute(command);
            if (!result.Accepted) throw new ArgumentException($"Invalid replay command: {result.Error}");
        }
        return engine;
    }

    private static MatchEngine Create(ulong seed, string scenario)
    {
        if (scenario.StartsWith("duel-demo/", StringComparison.Ordinal)) return CreateDuelDemoState(seed, scenario[10..]);
        if (scenario == PublishedContent.Scenario) return CreatePublished(seed, PublishedContent.Scenario, PublishedContent.Snapshot);
        if (scenario == PublishedContent.TestScenario) return CreatePublished(seed, PublishedContent.TestScenario, PublishedContent.TestSnapshot);
        if (scenario is not ("starter" or "economy" or DeckSearchScenario or CopyShowcaseScenario or AbilityGrantShowcaseScenario or EitherOrShowcaseScenario or OpponentPaymentShowcaseScenario)) throw new ArgumentException("Unknown scenario.");
        var state = new MatchState { Players = [new() { Id = 0, Name = "Crane", Clan = "Crane", Honor = 8 }, new() { Id = 1, Name = "Lion", Clan = "Lion", Honor = 6 }] };
        var random = new StableRandom(seed);
        var nextId = 0; var ids = OpaqueCardIds(seed);
        CardInstance Add(int owner, string definition, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id = ids[nextId++], DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp, Bowed = bowed };
            state.Cards.Add(card.Id, card);
            return card;
        }
        foreach (var player in state.Players)
        {
            var clan = player.Clan.ToLowerInvariant();
            player.StrongholdId = Add(player.Id, $"training-{clan}-stronghold", Zone.Stronghold, true).Id;
            Add(player.Id, "training-border-keep", Zone.Home, true);
            if (player.Id == 1) Add(player.Id, "training-bamboo", Zone.Home, true, true);
            for (var n = 0; n < 40; n++)
            {
                var definition = n < 12 ? (n % 2 == 0 ? "training-farm" : "training-marketplace")
                    : $"training-{clan}-{new[] { "samurai", "courtier", "veteran", "dignitary" }[n % 4]}";
                player.DynastyDeck.Add(Add(player.Id, definition, Zone.DynastyDeck).Id);
                player.FateDeck.Add(Add(player.Id, new[] { "training-follower", "training-item", "training-rally", "training-pressure" }[n % 4], Zone.FateDeck).Id);
            }
            random.Shuffle(player.DynastyDeck);
            random.Shuffle(player.FateDeck);
            player.Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceState { Index = i, Strength = 6, PrintedStrength = 6 }).ToList();
        }
        state.RngState = random.State; // Deck search (issue #312): later shuffles continue this exact sequence.
        OrderCardsById(state);
        var engine = new MatchEngine(state, seed, scenario);
        foreach (var player in state.Players)
        {
            foreach (var province in player.Provinces) engine.Refill(player, province);
            for (var n = 0; n < 6; n++) engine.Draw(player);
        }
        if (scenario == "economy")
        {
            var player = state.Players[0];
            string[] definitions = ["training-crane-courtier", "training-crane-veteran", "training-marketplace", "training-crane-dignitary"];
            for (var i = 0; i < 4; i++)
            {
                var old = state.Cards[player.Provinces[i].CardId!];
                old.DefinitionId = definitions[i]; // Explicit scenario fixture, not a shuffled production deck.
            }
            Add(0, "training-marketplace", Zone.Home, true);
        }
        if (scenario == DeckSearchScenario)
        {
            // Deck search fixture (issue #312), see CreateDeckSearchShowcase: both players get Ikoma Shizuka and the
            // training search source in their home, and a few deck positions are rewritten to Regions (Fallow Lands
            // x2, another published Region, a costed training Region) and Terrains -- explicit fixture edits of an
            // otherwise ordinary shuffled deck, so the state stays reproducible from the seed and replayable.
            foreach (var player in state.Players)
            {
                Add(player.Id, "oracle-l5r-10292", Zone.Home, true);
                Add(player.Id, "training-search-source", Zone.Home, true);
                Add(player.Id, "training-look-source", Zone.Home, true);
                string[] dynasty = ["oracle-l5r-9865", "oracle-l5r-9865", "oracle-l5r-4777", "training-costed-region"];
                string[] fate = ["training-terrain", "training-terrain"];
                for (var i = 0; i < dynasty.Length; i++) state.Cards[player.DynastyDeck[i]].DefinitionId = dynasty[i];
                for (var i = 0; i < fate.Length; i++) state.Cards[player.FateDeck[i]].DefinitionId = fate[i];
            }
        }
        if (scenario == AbilityGrantShowcaseScenario)
        {
            // Two copies demonstrate that only the chosen Personality receives the permanent grant.
            Add(0, "oracle-l5r-8550", Zone.Home, true); Add(0, "oracle-l5r-8550", Zone.Home, true);
            var spirit = Add(0, "oracle-l5r-7424", Zone.Hand, true); state.Players[0].Hand.Add(spirit.Id);
        }
        if (scenario == CopyShowcaseScenario)
        {
            // Copy showcase (issue #338): Fukuzo and the two training fixtures at player 0's home; Tamori Tamoko (two abilities), Bayushi Kahoku
            // - exp2 and Ikoma Shizuka (dead or discarded, a Limited action each) and a training donor in player 1's Dynasty discard pile.
            Add(0, "oracle-l5r-2728", Zone.Home, true);
            Add(0, "training-skin-receiver", Zone.Home, true);
            foreach (var definition in new[] { "oracle-l5r-10056", "oracle-l5r-10479", "oracle-l5r-10292", "training-skin-donor" }) Add(1, definition, Zone.DynastyDiscard, true);
            Add(1, "oracle-l5r-10056", Zone.Dead, true);
        }
        if (scenario == EitherOrShowcaseScenario)
        {
            // Either/Or showcase (issue #338): Sakuma and Rin at player 0's home with Unfortunate Hesitation in hand; Yogo Adi (a Political action that
            // targets player 0's Personalities, so Sakuma may react) and the three-option fixture at player 1's home.
            Add(0, "oracle-l5r-9624", Zone.Home, true); Add(0, "oracle-l5r-7151", Zone.Home, true);
            var hesitation = Add(0, "oracle-l5r-9941", Zone.Hand, true); state.Players[0].Hand.Add(hesitation.Id);
            Add(1, "oracle-l5r-9485", Zone.Home, true); Add(1, "training-either-or-source", Zone.Home, true);
        }
        if (scenario == OpponentPaymentShowcaseScenario)
        {
            var charity = Add(1, "oracle-l5r-10181", Zone.Hand, true); state.Players[1].Hand.Add(charity.Id);
            foreach (var player in state.Players) for (var n = 0; n < 3; n++) Add(player.Id, "training-marketplace", Zone.Home, true);
        }
        engine.StartTurn();
        if (scenario == "economy") { state.Phase = Phase.Dynasty; engine.Emit("scenario", "Economy exercise: choose one payment at a time; Gold never carries over."); }
        return engine;
    }

    public CommandResult Execute(GameCommand command)
    {
        var turnAtStart = _state.TurnNumber;
        var result = ExecuteOne(command);
        if (result.Accepted) foreach (var sweep in EngineHooks.SweepsAfterAccepted.Items) sweep(this, turnAtStart);
        return result.Accepted ? RunPassAllBattles(result) : result; // "Pass all battles" (issue #628, PassAllBattles.cs)
    }

    private CommandResult ExecuteOne(GameCommand command)
    {
        if (command.PlayerId is < 0 or > 1) return Reject("Unknown player.");
        if (command.ExpectedVersion != _state.Version) return Reject("The board has changed. Refresh before acting.");
        if (_state.WinnerId is not null) return Reject("The match has finished.");
        var original = _state;
        // Ten Thousand Temples (issue #492, group F): the answer to an Honor loss decision replays the command that reached it, from the state it started in, with the decisions made so far.
        var execute = command; MatchState? cleanBase = null;
        _honorLossOccurrence = 0; _honorLossDecisions = [];
        _entryCostOccurrence = 0; // issue #501 (EmperorMechC.cs)
        ResetHonorChangeReplay(); // Mendon's R4 ruling (issue #492): the windows of an Honor change replay the command that reached it (EmperorRulingR4.cs).
        if (command.Type == "entryCostChoice") // Emperor Edition mechanics group C (issue #501): the answer to an additional cost of entering play replays the command that reached it (EmperorMechC.cs).
        {
            if (AnswerEntryCost(command, original, out cleanBase, out var entryReplay) is { } entryError) { _state = original; return Reject(entryError); }
            execute = entryReplay!;
        }
        else if (command.Type == "honorLossChoice")
        {
            if (original.PendingHonorLossDecision is not { } decision) return Reject("No Honor loss decision is pending.");
            if (command.PlayerId != decision.PlayerId || command.Choice is not ("yes" or "no")) return Reject("Answer the Honor loss decision: destroy Ten Thousand Temples to negate the loss, or not.");
            cleanBase = Copy(original);
            cleanBase.PendingHonorLossDecision = null; cleanBase.PendingChoice = decision.PriorChoice; cleanBase.PriorityPlayerId = decision.PriorPriority; cleanBase.ConsecutivePasses = decision.PriorPasses;
            _state = Copy(cleanBase);
            _honorLossDecisions = decision.Decisions.Append(command.Choice == "yes").ToList();
            _honorWindowsDone = decision.HonorWindowsDone;
            execute = decision.Command;
        }
        else _state = Copy(original);
        RunSweeps(EngineHooks.SweepsBeforeApply); // on the working copy: a rejected command leaves the live state untouched
        var firstEvent = _state.Events.Count;
        try
        {
            // Mendon's R4 ruling (issue #492): a command that reached an Honor change a Reaction may be played on was set aside and its window opened; when the window closes the command is run
            // again (from the state the window left), so this body runs once for a command and once more for each such replay.
            var replaying = false;
            while (true)
            {
                var aftermath = _state.LastDuel is { Complete: false, Result: not null } ? CaptureDuelEvidence() : null;
                var reactionSource = execute.CardId ?? _state.PendingChoice?.CardId;
                var reactionCard = aftermath is not null && reactionSource is not null && _state.Cards.TryGetValue(reactionSource, out var rc)
                    && rc.FaceUp && rc.Zone is not (Zone.Hand or Zone.FateDeck or Zone.DynastyDeck) ? ViewCard(rc) : null;
                var reactionLabel = _state.PendingChoice?.Type == "overconfidentDraw" ? "Overconfident" : null;
                var traitSnapshot = AfterCommandTraitSnapshot(); // "After this card bows/attaches" traits (issue #453 batch 5, SeedsOfDecayBatch5.cs)
                UsePaymentDiscards(execute); // Asahina Keigo (issue #477): the hand cards named for Gold sources that discard one.
                var plansSets = PlansOpportunitySets(execute); // Plans Within Plans (issue #492): is this command the armed player's opportunity to take an Open action?
                if (replaying)
                {
                    // The Reactions played in the window may have made the command it set aside illegal (a card it needs was bowed, a target left): it is then cancelled, and the window's state stands.
                    try { Apply(execute); }
                    catch (RuleViolation cancelled)
                    {
                        _paymentDiscards = [];
                        _state = Copy(_honorReplayBase ?? _state);
                        Emit("actionCancelled", $"The action could not go on after the Reactions played before its Honor change: {cancelled.Message}");
                        break;
                    }
                }
                else Apply(execute);
                if (_honorReplayCommand is { } replay) { _paymentDiscards = []; execute = replay; _honorReplayCommand = null; replaying = true; continue; }
                SpendPlansWithinPlans(execute, plansSets);
                _paymentDiscards = [];
                ApplyAfterCommandTraits(traitSnapshot);
                RunSweeps(EngineHooks.SweepsAfterApply);
                ReconcileResolutionArea(); // issue #917
                if (aftermath is not null) RecordDuelChanges(aftermath, "Aftermath", reactionCard, sourceLabel: reactionLabel);
                break;
            }
            if (_state.PendingHonorChange is null) _state.EntryCostAnswers.Clear(); // issue #501: the replayed command has finished; its entry-cost answers are spent
            AssignUnitNumbers();
            _state.Version++;
            RefreshDuelRecord();
            _commands.Add(Copy(command));
            return new(true, null, _state.Version, _state.Events.Skip(firstEvent).ToArray());
        }
        catch (HonorLossDecisionNeeded need)
        {
            // The command reached an Honor loss Ten Thousand Temples may negate: nothing it did is kept; its controller is asked, and the command is replayed with his answer.
            _paymentDiscards = [];
            var pendingBase = _honorReplayBase ?? cleanBase ?? original;
            _state = pendingBase;
            pendingBase.PendingHonorLossDecision = new PendingHonorLossDecision
            {
                Command = execute,
                Decisions = _honorLossDecisions.ToList(),
                PlayerId = need.PlayerId,
                TempleId = need.TempleId,
                Amount = need.Amount,
                Reason = need.Reason,
                PriorChoice = pendingBase.PendingChoice,
                PriorPriority = pendingBase.PriorityPlayerId,
                PriorPasses = pendingBase.ConsecutivePasses,
                HonorWindowsDone = _honorWindowsDone
            };
            pendingBase.PendingChoice = new("honorLossDecision", need.PlayerId, need.TempleId, $"Ten Thousand Temples: you would lose {-need.Amount} Honor ({need.Reason}). Destroy it to negate the loss?");
            pendingBase.PriorityPlayerId = need.PlayerId; pendingBase.ConsecutivePasses = 0;
            var decisionEvents = _state.Events.Count;
            Emit("honorLossDecision", $"{Player(need.PlayerId).Name} may destroy Ten Thousand Temples to negate an Honor loss of {-need.Amount} ({need.Reason}).", need.PlayerId, need.TempleId);
            _state.Version++;
            _commands.Add(Copy(command));
            _honorLossDecisions = [];
            return new(true, null, _state.Version, _state.Events.Skip(decisionEvents).ToArray());
        }
        catch (EntryCostDecisionNeeded need)
        {
            // Emperor Edition mechanics group C (issue #501): the command reached an additional cost of entering play with several ways to pay -- nothing it did is kept; its payer is asked.
            return SetAsideForEntryCost(need, command, execute, _honorReplayBase ?? cleanBase ?? original);
        }
        catch (HonorChangeWindowNeeded need)
        {
            // The command reached an Honor change from an action's effects that a Reaction may be played on: nothing it did is kept, the window is opened on the state it started from
            // (Reactions played in it change that state), and the command is run again when both players have passed (Execute's loop).
            _paymentDiscards = [];
            var pendingBase = _honorReplayBase ?? cleanBase ?? original;
            _state = pendingBase;
            var windowEvents = OpenHonorChangeWindow(need, execute);
            _state.Version++;
            _commands.Add(Copy(command));
            _honorLossDecisions = [];
            return new(true, null, _state.Version, _state.Events.Skip(windowEvents).ToArray());
        }
        catch (RuleViolation error)
        {
            _state = original; _paymentDiscards = [];
            return Reject(error.Message);
        }
        catch
        {
            _state = original; _paymentDiscards = [];
            throw;
        }
    }

    private CommandResult Reject(string error) => new(false, error, _state.Version, []);
    // A window a Reaction set aside (ReactionsAreActions.cs) is reopened the moment nothing that Reaction started is still waiting.
    private void Apply(GameCommand command)
    {
        var twistWasActive = _state.TwistOfFateActive; // Twist of Fate (issue #492, group D): the additional action ends with the first command taken during it that leaves nothing open.
        var suspendedBefore = _state.SuspendedWindows.Count;
        ApplyCore(command);
        if (_honorReplayCommand is not null) return; // an Honor-change window just closed: Execute runs the command it set aside again.
        var resumed = ResumeIdleSuspendedWindow() || _state.SuspendedWindows.Count < suspendedBefore;
        if (resumed) SettleReactionWindow(); // issue #644: a window given back after a Reaction of its own is not offered to a player with nothing to react with: a window given back after a Reaction of its own is not offered to a player with nothing to react with
        FinishSpentBeforeBattleItem();
        FinishTwistOfFateAction(twistWasActive);
        OpenAssignedReactionWindow(); // Chrysanthemum Blossom (issue #492 group G): "After this Personality is assigned".
        P4CommandTail(); // Scars of War (issue #503, EmperorP4Watches.cs): "after each time it bows".
        OpenEmperorP6QueuedDecision(); // Issue #503 (EmperorP6.cs): Suikihime's leaves-play watch; the refill decisions owed by the command.
        PassAllBattlesTail(); // "Pass all battles" (issue #628): the flags last only while battles are being fought
        EnterRevealedCelestials(); // issue #644: a Celestial turned face up anywhere enters play at once
        OpenP3EntryWindow(); // Easy Persuasion (issue #503): the window after a Personality entered play, for either player
        NitoshiCommandTail(); // Bayushi Nitoshi - inexperienced (issue #617): the hold on the card he took lasts until it is played or returned
        OpenP3OwedChoices(); // Shosuro Aroru - exp2 (issue #503): "After the action's resolution, you may move Aroru to a different location."
        CheckInokuchiWatches(); // Mirumoto Inokuchi (issue #501, EmperorMechA.cs): "If you don't, bow him" -- once his additional action is gone without being taken.
        NormalizeOwnerAnsweredPriority(); // Issue #642 (PendingPriority.cs): a choice answered by its owner gives its owner priority.
        SkipIdleCavalrySegments(); // Issue #627 (CavalrySkip.cs): a Cavalry Maneuvers segment where the acting player has nothing to do but pass.
        ReactionOrderTail(); // Issue #803 (ReactionOrdering.cs): announce, order and resolve the Reactions of a window both players hold one for.
    }
    private void ApplyCore(GameCommand command)
    {
        if (command.Type == "concede") { Win(1 - command.PlayerId, "Concession"); return; }
        // Order Seppuku (issue #368): an action at the player's own turn to act, with nothing pending. Dispatched ahead of the pending-choice
        // handlers only so that its ONE timing predicate (SeppukuTimingReason) gives the reason, whatever is open.
        if (command.Type == "orderSeppuku") { OrderSeppuku(command); return; }
        Require(_state.PendingHonorLossDecision is null, "Answer the Honor loss decision first (Ten Thousand Temples)."); // issue #492, group F: only "honorLossChoice" (handled by Execute) answers it.
        Require(_state.PendingEntryCostDecision is null, "Choose how to pay the additional cost of entering play first."); // issue #501: only "entryCostChoice" (handled by Execute) answers it.
        Require(command.PlayerId == _state.PriorityPlayerId, "Wait for your action opportunity.");
        if (_state.PendingReactionOrder is { } reactionOrder && Equals(_state.PendingChoice, reactionOrder.Window) && HandleReactionOrderCommand(reactionOrder, command)) return; // issue #803 (ReactionOrdering.cs)
        if (_state.PendingChoice is not null)
        {
            foreach (var handler in EngineHooks.PendingChoice.Items)
                if (handler(this, _state.PendingChoice.Type, command)) return;
            Require(command.Type == "discardHand", "Choose cards to discard to the hand limit.");
            DiscardHand(command);
            return;
        }
        if (_state.PendingDuel is not null) { ResolveDuelCommand(command); return; }
        if (command.Type == "passAllBattles") { command = BeginPassAllBattles(command); if (command.Type != "pass") return; } // issue #628: a pass that keeps passing
        // Unfortunate Hesitation (issue #338): a player who owes a pass at an ordinary Action/Battle opportunity may only pass.
        EnforceForcedPass(command);
        EnforceSearchPlay(command); // Gozu (issue #667, SearchPlayGozu.cs): the Terrain it found is owed as the next action; nothing else, and no pass, is legal until it is played.
        // Urgency (issue #453, batch 2): a player past his allowance of non-Reaction actions this phase may take none.
        EnforceActionAllowance(command);
        // Battle Phase timing (issue #310): a limit on the first-action holder's FIRST Battle action (Naval
        // Invasion's "which he must perform"; Rebuilding Kyuden Hida's "may not announce a Battle action before the
        // Defender's first normal opportunity"). Consumed by the holder's next accepted command; a useAbility's
        // own check lives in ValidateAbility (so QueryActions already withholds what would be rejected), the two
        // performer-less Battle actions are barred here. A rejected command reverts this clearing with everything else.
        // A Battle-phase equip (a granted timing, TimingGrants.cs) is a Battle action too, barred like the performer-less ones.
        if (_state.FirstActionRestriction is { } firstActionLimit && command.PlayerId == firstActionLimit.PlayerId && (command.Type is "pass" or "useAbility" or "playStrategy" or "recruitReserve" || command.Type == "equip" && _state.Phase == Phase.Battle))
        {
            // Jade's Roar - exp (Gempukku pass, issue #461): a Terrain is played by playStrategy, so a keyword-limited first action lets a card carrying that keyword through.
            var keywordCardPlayed = firstActionLimit.ActionKeyword is { } limitKeyword && command.Type == "playStrategy" && command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var limitCard) && Keyword(limitCard, limitKeyword);
            if (command.Type is "playStrategy" or "recruitReserve" or "equip" && !keywordCardPlayed) throw new RuleViolation(FirstActionLimitMessage(firstActionLimit));
            if (command.Type == "useAbility") ValidateFirstActionRestriction(command);
            _state.FirstActionRestriction = null;
        }
        InterruptPassAllBattles(command); // issue #628: an action in a battle ends the other player's "Pass all battles"
        NoteBattleActionTaken(command); // One Action, Two Strikes (issue #501, EmperorMechA.cs): "if the enemy leader has taken any Battle actions during this battle"
        switch (command.Type)
        {
            case "pass": Pass(); break;
            case "declineAdditionalAction": DeclineAdditionalAction(command); break; // Mendon's item 2 (issue #501): declining a "may" additional action is not passing
            case "purchase": Purchase(command); break;
            case "overlay": Overlay(command); break;
            case "discardProvince": DiscardProvince(command); break;
            case "endDynasty": Require(_state.Phase is Phase.Dynasty or Phase.DynastyDiscard, "Not in the Dynasty Phase."); if (BeginTwistOfFateWindow()) break; EndDynastyPhase(); break;
            case "declareAttack": DeclareAttackPhase(command); break;
            case "attack": AssignAttackers(command); break;
            case "assignDefense": AssignDefense(command); break;
            case "selectBattle": SelectBattle(command); break;
            case "equip": if (!P3AnnounceCommand(command, () => Equip(command))) Equip(command); break; // issue #503: a Battle action that is not an ability is announced too (Ikoma Ayumu, Utaku Lishan)
            case "playStrategy": if (!P3AnnounceCommand(command, () => PlayStrategy(command))) PlayStrategy(command); break;
            case "recruitReserve": if (!P3AnnounceCommand(command, () => RecruitReserve(command))) RecruitReserve(command); break;
            case "useAbility": UseAbility(command); break;
            default: throw new RuleViolation("Unsupported command.");
        }
    }

    // "After this X enters play" (core rules): a card's own Reaction fires every time it is placed
    // into a player's home, either standalone or as an attachment -- from a province, a hand, or (once
    // a card returns cards from there) a discard pile. Only "removed from the game" is permanent.
    private static string? EntryReactionAbility(CardInstance card) =>
        (Definition(card).Abilities ?? []).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-enters-play");

    // Opens the entering card's own "after entering play" Reaction, if it has one; every entry point
    // (Purchase, Equip, a played Terrain) calls this. Repeatable each time the card enters play, not
    // just once ever: clears any earlier use so a card that left and is entering again gets a fresh
    // window, per the printed trigger firing anew on each entry. closesTo controls who regains
    // priority once the window fully closes (both sides passed) -- see PassReaction.
    private bool OpenEntryReactionWindow(int playerId, CardInstance card, int? closesTo = null)
    {
        ApplyMandatoryEntryEffects(playerId, card);
        var reactionId = EntryReactionAbility(card);
        // Singh Remnants' shape: a DIFFERENT card (in hand or a discard pile) watching for this entry,
        // not the entering card's own reaction -- only relevant when a Personality is what entered.
        // Emperor Edition Stronghold pass (issue #486), batch 3: or the controller's own Stronghold watching the entry (The Spider's Web).
        var watchingReactionAvailable = Definition(card).Type == CardType.Personality && (AnyPersonalityEntryReactionAvailable(playerId) || StrongholdEntryWatcherAvailable(playerId, card) || InPlayEntryWatchers(playerId, card).Any());
        if (reactionId is null && !watchingReactionAvailable) return false;
        // Kitsuki Horume's own "choose a player, gain or lose" (issue #210) is the first
        // entry-reaction to share a UsageKey across two catalog entries -- the actual dedupe stamp
        // lives under the UsageKey (see ExecuteAbilityEffects' own `uses[ability.UsageKey ?? id]`),
        // not the bare ability id, so clearing a stale one from an earlier entry must look the same
        // key up, not assume UsageKey is always just the id itself.
        if (reactionId is not null) card.AbilityUses.Remove(AbilityCatalog.All[reactionId].UsageKey ?? reactionId);
        _state.PendingChoice = new("entryReaction", playerId, card.Id, "After entry: use this card's own reaction, or pass.", closesTo);
        _state.ConsecutivePasses = 0;
        Emit("reactionWindow", "Entry reaction window opened.", playerId, card.Id);
        return true;
    }
    // The mandatory, unconditional half of "a card enters play" (never an opt-in Reaction): Destined's
    // draw, an EntryHonorLoss, Sensei's discard. Split out of OpenEntryReactionWindow (issue #299) so a
    // card created from INSIDE another card's own already-open entry Reaction window (Kitsune Ohsuki's/
    // Tokoru's created token -- CreatePersonality's duringEntryReaction) still gets these without
    // opening a second window on top of, and thereby clobbering, the first.
    private void ApplyMandatoryEntryEffects(int playerId, CardInstance card)
    {
        P3NoteEntry(card); // Easy Persuasion (issue #503): a Personality entered play, with what was paid for him
        // Destined keyword (core rules): a mandatory Fate-deck draw every time a card carrying it
        // enters play -- unconditional, not an opt-in Reaction, so it fires here unconditionally
        // rather than through any Reaction window. Every entry point (Purchase, Equip, a played
        // Terrain, equip-battle's Spell attach) already funnels through OpenEntryReactionWindow, which
        // calls this first; CreatePersonality's nested mode calls it directly.
        if (Keyword(card, "Destined")) Draw(Player(playerId));
        // "After this card enters play, lose X Honor" with no printed "Reaction:" prefix (issue
        // #210) -- mandatory, same convention as Destined's own draw above, never an optional choice.
        if (Definition(card).EntryHonorLoss is int loss) GainHonor(playerId, -loss, $"{Definition(card).Name} entering play", card);
        // Sensei keyword (Mirumoto Hikaro / Mirumoto Yozo, Experienced 2): "After [this card] enters
        // play: Discard all your other Sensei Personalities" -- a trait (no Action:/Reaction: label),
        // so mandatory and unconditional, same convention as the two lines above; fires from any
        // entry path (Purchase, Equip, RecruitReserve, an Experienced overlay were one to exist), not
        // just Purchase.
        if (Definition(card).Effect == "discard-other-sensei") DiscardOtherSensei(playerId, card);
        // Daigotsu Bofana - exp (issue #333): "...all other players must discard a card, or two cards if they are Crab Clan" -- the
        // Honor loss is his EntryHonorLoss above; each other player then owes their discards (queued, so a Proclaim or entry window
        // already open is never overwritten -- see OpenOverconfidentDrawWindow).
        if (Definition(card).Effect == BofanaEntryDiscardEffect) QueueOtherPlayersEntryDiscards(playerId);
        // The Great Death (issue #431): "After The Great Death enters play: Create three 1F Shadowlands * Nonhuman * Raptor * Cavalry * Undead Followers, attach them to it, and lose 6 Honor." A trait, so
        // mandatory like the lines above (the Honor loss is its EntryHonorLoss).
        if (Definition(card).Effect == GreatDeathEntryEffect) CreateGreatDeathFollowers(playerId, card);
        // Seeds of Decay pass (issue #453), batch 4: the Sensei whose Effect field carries their other static trait (StaticKeywordRules.cs) discard the other Sensei too;
        // Daigotsu Yamato loses Brash for good if another player is Scorpion Clan (SeedsOfDecayBatch4.cs).
        if (HasSenseiEntryEffect(Definition(card).Effect)) DiscardOtherSensei(playerId, card);
        if (Definition(card).Effect == YamatoEntryEffect) RemoveBrashIfOpponentIsScorpion(card);
        ApplyShadowsEmbraceBatch2EntryEffects(playerId, card); // The Shadow's Embrace pass (issue #476), batch 2: M'rika's "After M'rika enters play: Gain 2 Honor."
        ApplyShadowsEmbraceBatch6EntryEffects(playerId, card); // Clearing Crew (The Shadow's Embrace pass, issue #476 batch 6).
        ApplyShadowsEmbraceBatch1EntryEffects(playerId, card); // The Shadow's Embrace pass (issue #476), batch 1: Atosa's Sensei discard.
        ApplyEmperorRemainingFEntryEffects(playerId, card); // Emperor Edition remaining-cards pass (issue #492), group F: Moneylender's Wealth tokens.
        ApplyEmperorFollowupsEntryEffects(playerId, card); // Emperor Edition follow-ups (issue #498): the Dragon Sword's Honor; the entry searches queued (EmperorFollowupsSearch.cs).
        ApplySearchPlayEntryEffects(playerId, card); // The Topaz Armor - exp (issue #667, SearchPlayTopaz.cs): "After this card enters play: Choose two keywords" is queued.
        ApplyEmperorP1EntryEffects(playerId, card); // Issue #503 (EmperorP1.cs): its Sensei discards and Extending Influence's Honor loss.
        ApplyRequestedCreatedCardsEntryEffects(playerId, card); // Yari of Air's mandatory Air-wielder Honor gain.
        ApplyVastPaddyEntry(playerId, card); // Vast Paddy Fields (issue #622, IvoryHoldings.cs): the province the Farm Holding was recruited from refills face-up.
        ApplyPromoEmperorEntryEffects(playerId, card); // Bandit's Sanctuary, Renewed Lands (issue #653, PromoEmperorHoldings.cs)
        ApplyWlPersGates1EntryEffects(playerId, card); // Boyoh Mercenary (issue #674, WlPersGates1.cs): "After Boyoh Mercenary enters play, discard a card."
        ApplyTfPersonalitiesEntryEffects(playerId, card); // Soshi Kodanshi (issue #622, TfPersonalities.cs): "After Kodanshi enters play from a province: Create ..." -- a trait.
        ApplyCreatedProxies677EntryEffects(playerId, card); // Abdollah, Kitsune Kichi - exp, Kuni Renyu - exp2 (issue #677, CreatedProxies677.cs): "After X enters play, ... create ..." -- a trait.
        ApplyWave827EntryEffects(playerId, card); // Kokujin (issue #827, Wave827.cs): "lose 6 Honor unless you are a Spider Clan player."
        ApplyWave734EntryEffects(playerId, card); // Bayushi Nitoshi - exp (issue #734, Wave734.cs): "After Nitoshi enters play, all players lose 3 Honor."
        ApplyWave740EntryEffects(card); // Ikoma Ayumu - exp (issue #740, Wave740.cs): "will not gain Fallen or Shadowlands".
        ApplyWave739EntryEffects(card); // Iweko Seiken (issue #739, Wave739.cs): "will not gain a Clan Alignment".
        ApplyWave737EntryEffects(playerId, card); // Iweko Shibatsu (issue #737, Wave737.cs): "After Shibatsu enters play, gain 3 Honor." and his Clan-alignment block.
        ApplyWlItemsMiscEntryEffects(playerId, card); // The Bestial Ones (issue #674, WlItemsMiscFollowers.cs): "After this Follower enters play, ... discard a card" -- a trait.
        ApplyWave835EntryEffects(playerId, card); // Blood of Isawa's Tribe (issue #835, Wave835.cs): "After this Spell attaches, choose an element keyword" -- a trait.
    }
    // Sensei (Mirumoto Hikaro / Mirumoto Yozo, Experienced 2): "your OTHER Sensei Personalities" --
    // every OTHER Sensei-keyword Personality this same controller has, either unbowed-or-bowed in
    // Zone.Home, or face-up in one of their own provinces (a face-down province card is hidden
    // information and cannot be identified, so it is never affected). A plain discard, not a
    // destruction -- no MarkDead, no "Dead" status, no Overconfident trigger even if the discarded
    // card happens to carry it, matching DiscardProvince's own direct Zone.DynastyDiscard assignment.
    private void DiscardOtherSensei(int playerId, CardInstance enteringCard)
    {
        var others = _state.Cards.Values.Where(c => c.Id != enteringCard.Id && c.OwnerId == playerId && Definition(c).Type == CardType.Personality
            && Keyword(c, "Sensei") && (c.Zone == Zone.Home || c.Zone == Zone.Province && c.FaceUp)).ToArray();
        foreach (var other in others)
        {
            if (other.Zone == Zone.Province)
            {
                var province = Player(playerId).Provinces.Single(p => p.CardId == other.Id);
                other.Zone = Zone.DynastyDiscard; other.ProvinceIndex = null; province.CardId = null;
                Emit("provinceDiscard", $"{Player(playerId).Name} discarded {Definition(other).Name} from a province (Sensei).", playerId, other.Id);
                Refill(Player(playerId), province);
            }
            else
            {
                other.Zone = Zone.DynastyDiscard;
                Emit("cardDiscarded", $"{Definition(other).Name} was discarded (Sensei).", playerId, other.Id);
            }
        }
    }

    private void Purchase(GameCommand command)
    {
        Require(_state.Phase == Phase.Dynasty, "Purchases are only allowed before province discarding in the Dynasty Phase.");
        var card = Card(command.CardId);
        var quote = QuotePurchase(command.PlayerId, card.Id, command.ClanDiscount, command.ClanAllegiance);
        Require(quote.Error is null, quote.Error ?? "");
        Player(command.PlayerId).DynastyActionTaken = true;
        PayAdditionalEntryCost(command.PlayerId, card); // En'you (issue #501, EmperorMechC.cs): an additional cost of entering play.
        Pay(command.PlayerId, card, quote.Cost, command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Recruit));
        if (Definition(card).Type == CardType.Personality) Player(command.PlayerId).PersonalityPurchaseTurn = _state.TurnNumber;
        var (province, slot) = FindProvinceSlot(Player(command.PlayerId), card.Id); // issue #554: either of Otosan Uchi's two slots
        ClearProvinceSlot(province, slot);
        _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = province.Index; // "enters play from a province" (Yogo Ugimori; issue #453, batch 2; Agasha Ryo, issue #477)
        card.Zone = Zone.Home; card.ProvinceIndex = null; card.FaceUp = true;
        // A Personality is straightened by default; entering bowed is only ever a direct
        // consequence of a printed effect, never a separate straighten-then-bow step.
        card.Bowed = Definition(card).Type == CardType.Holding || (Definition(card).Type == CardType.Personality && Definition(card).Effect == "enters-bowed");
        if (Definition(card).Type == CardType.Personality && Definition(card).Effect == "enters-dishonored") Dishonor(card);
        Emit("cardEnteredPlay", $"{Player(command.PlayerId).Name} brought {Definition(card).Name} into play{(card.Bowed ? " bowed" : "")}.", command.PlayerId, card.Id);
        AfterHoldingEnteredFromProvinceP6(province, card); // Dark Experiments (issue #503, EmperorP6.cs)
        QueueOverconfidentDraw(card);
        RefillProvinceSlot(Player(command.PlayerId), province, slot);
        TfAfterEnteredFromProvince(Player(command.PlayerId), province, slot, card); // Small Library (issue #622, TfHoldings.cs): "Refill it face-up"
        var reactionId = EntryReactionAbility(card);
        if (quote.ProclaimEligible)
        {
            _state.PendingEntryEffectsCardId = card.Id;
            if (reactionId is not null) card.AbilityUses.Remove(reactionId);
            _state.PendingChoice = new("proclaim", command.PlayerId, card.Id, "After entry: Proclaim for Honor" + (reactionId is not null ? ", use this card's own reaction," : "") + " or pass. Each may be used once.");
            _state.ConsecutivePasses = 0;
            Emit("reactionWindow", "Proclaim reaction window opened.", command.PlayerId, card.Id);
        }
        // Dynasty-phase purchasing never cedes priority away from the active player on its own
        // (they may keep buying) -- so unlike RecruitReserve below, an Overconfident window opened
        // here must resume back to the SAME purchasing player, not their opponent.
        else if (!OpenEntryReactionWindow(command.PlayerId, card)) OpenOverconfidentDrawWindow(command.PlayerId);
    }
    // Kitsu Akai's own Limited action, "Create a ... Personality" (issue #257): mints a brand-new
    // CardInstance from a CardDefinition that was never drawn from a deck or a Province -- the
    // engine's first "create a card" primitive (CreateToken only ever adds a stat marker to an
    // EXISTING card). Enters play through the same shared entry sequence Purchase above funnels
    // through (FaceUp, own entry-reaction window, Overconfident) but skips every purchase-specific
    // step (QuotePurchase's Gold cost, Proclaim eligibility, Refill) since nothing was recruited
    // from a province -- "created, not recruited," the user's own phrase. Returns true exactly when
    // OpenEntryReactionWindow/OpenOverconfidentDrawWindow returns true, mirroring their own bool
    // convention so the calling ExecuteAbilityEffects case can decide whether to return early, the
    // same shape low-market-equip/equip-battle already use. Only a Personality is implemented; a
    // created Holding/Region/Attachment is recorded as accepted future scope, not built until a
    // real card needs it (see docs/decisions).
    // duringEntryReaction (issue #299, Kitsune Ohsuki/Tokoru): the created card is being made by a
    // Reaction used INSIDE another card's own already-open "entryReaction"/"proclaim" window (the
    // creator's own "After X enters play: Create ..."). Opening a second window here would overwrite
    // that one's PendingChoice (silently dropping, e.g., the creator's own still-unused Proclaim), and
    // PendingChoice has no stack -- so only the mandatory half of entering play (Destined/EntryHonorLoss/
    // Sensei) is applied, Overconfident is still queued (the outer window's own eventual close drains
    // it, PassReaction), and this returns false so the calling Reaction falls through to its ordinary
    // "priority flips, window stays open" tail. The one thing not offered is a reaction TO the created
    // card's own entry (Singh Remnants-style watchers) -- a documented limit, since neither published
    // token has an entering-play Reaction of its own and no watcher is known to matter here.
    // battlefield (issue #394, "at this battlefield"): the province whose battle the card is created at, on its controller's side; null is home.
    // printed (issue #402): the card's own printed values when they are not the scan's; beforeWindow: the rest of the creating action, run before any entry window opens.
    // createdCopy (issue #398): the card is a created copy of another card, marked per instance (CardInstance.CreatedCopy); a copy of a card with printed values keeps them.
    // original (issue #501 item 32): the card copied, recorded for the view's duplicate frame (MarkAsCopy).
    private bool CreatePersonality(int ownerId, CardDefinition definition, bool duringEntryReaction, int? battlefield, out CardInstance card, CardDefinition? printed = null, Action? beforeWindow = null, bool createdCopy = false, CardInstance? original = null)
    {
        card = new CardInstance { Id = $"created-{++_state.CreatedCardSequence:D4}", DefinitionId = definition.Id, Printed = printed, OwnerId = ownerId, Zone = battlefield is null ? Zone.Home : Zone.Battlefield, ProvinceIndex = battlefield, FaceUp = true, CreatedCopy = createdCopy };
        _state.Cards.Add(card.Id, card);
        if (original is not null) MarkAsCopy(card, original); // issue #501 item 32: the copy's frame and the original's scan
        var title = (printed ?? definition).Name; var where = battlefield is null ? "" : " at the battlefield";
        Emit("cardCreated", createdCopy ? $"{Player(ownerId).Name} created a copy of {title}{where}." : $"{Player(ownerId).Name} created {title}{where}.", ownerId, card.Id);
        NotePersonalityCreated(card, battlefield); // issue #667 (PerformersAmbush.cs): the action's record of what it created
        QueueOverconfidentDraw(card);
        beforeWindow?.Invoke();
        if (duringEntryReaction) { ApplyMandatoryEntryEffects(ownerId, card); return false; }
        return OpenEntryReactionWindow(ownerId, card) || OpenOverconfidentDrawWindow(ownerId);
    }

    // The shared "bring a face-up province Personality into play, paying all costs" entry sequence
    // (issue #301): Purchase and RecruitReserve above each inline their own copy (they are commands with
    // their own priority rules); Your Clan Needs You! (issue #295, into the CURRENT BATTLEFIELD) and
    // Shinden Shorai (issue #301, into HOME) are the two ABILITY callers and share this one body --
    // leave the province (the recruit's cost was already charged by ExecuteAbilityEffects' single
    // PayForAbility, issue #303, from the ability's GoldCostRule), enter play (bowed/dishonored only if the card's own Effect says so),
    // queue Overconfident, refill the province, then open Proclaim / the entering card's own Reaction /
    // the Overconfident draw. battlefield names the province index to enter at (null: Home, Purchase's
    // own destination). Returns true exactly when a window was opened, so the calling execution case
    // knows to `return` (leaving the action pending) rather than fall through to its own tail.
    // proclaimResumesTo: who regains priority once an opened Proclaim window fully closes (null: the
    // active player, Purchase's own convention; Shinden Shorai, a Limited ACTION, cedes to the opponent
    // like every other Action-phase action).
    private bool BringProvincePersonalityIntoPlay(PlayerState player, CardInstance card, PurchaseQuote quote, int? battlefield, string via, int? proclaimResumesTo = null)
    {
        var (province, slot) = FindProvinceSlot(player, card.Id); // either of Otosan Uchi's two slots (found by the bot soak, issue #765: Masaya in a second slot)
        ClearProvinceSlot(province, slot);
        _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = province.Index; // an ability recruit from a province "enters play from a province" too (Soshi Kodanshi, issue #622), as Purchase and RecruitReserve record
        EnterPlayFromRecruit(player, card, battlefield, via);
        RefillProvinceSlot(player, province, slot);
        return OpenRecruitEntryWindows(player, card, quote.ProclaimEligible, proclaimResumesTo);
    }

    // At Any Cost (issue #373): a dead Personality is recruited as a fresh card, into play at the battlefield the action was played at (Mendon), through the same
    // entry sequence as a province recruit. It may not be proclaimed, so its quote is never Proclaim-eligible. Returns true exactly when a window was opened.
    // battlefield null: he enters at home. onEntry runs once he is in play, before any entry window opens (Essence of Undeath's permanent keywords and Honor loss).
    private bool BringDeadPersonalityIntoPlay(PlayerState player, CardInstance card, string via, int? battlefield, Action<CardInstance>? onEntry = null)
    {
        ResetCardState(card);
        EnterPlayFromRecruit(player, card, battlefield, via);
        onEntry?.Invoke(card);
        return OpenRecruitEntryWindows(player, card, false, null);
    }

    // Akodo Kobi (issue #440 follow-up): each queued recruit -- checked, at the moment it actually resolves, for whether the card is STILL dead (a later
    // effect could already have brought him back another way) -- enters at home the same way BringDeadPersonalityIntoPlay does, ignoring costs and Honor
    // Requirements by construction (no QuotePurchase call at all, the same reason that primitive already ignores them for At Any Cost/Goju Plot). Unlike
    // BringDeadPersonalityIntoPlay, this deliberately does NOT open an entering-play Reaction window: doing so mid-EndTurn would interrupt the turn's own
    // transition to Phase.End with nothing recorded to resume it correctly (ResumeAfterDeferredWindow has no case for "we were mid-EndTurn"), and no Lion
    // Clan Personality this pass concerns carries one -- a future card that does would need that resumption built first. The Overconfident draw entering
    // play still queues (EnterPlayFromRecruit's own QueueOverconfidentDraw) is picked up by EndTurn's very next check, same as any other entry there.
    private void ResolvePendingEndPhaseRecruits()
    {
        var due = _state.PendingEndPhaseRecruits.ToArray();
        _state.PendingEndPhaseRecruits.Clear();
        foreach (var pending in due)
        {
            if (!_state.Cards.TryGetValue(pending.CardId, out var card) || card.Zone != Zone.Dead) continue;
            ResetCardState(card);
            EnterPlayFromRecruit(Player(pending.PlayerId), card, null, pending.Via);
        }
    }

    private void EnterPlayFromRecruit(PlayerState player, CardInstance card, int? battlefield, string via)
    {
        PayAdditionalEntryCost(player.Id, card); // En'you (issue #501, EmperorMechC.cs): an additional cost of entering play, whatever recruits him.
        card.Zone = battlefield is null ? Zone.Home : Zone.Battlefield; card.ProvinceIndex = battlefield; card.FaceUp = true;
        card.Bowed = Definition(card).Effect == "enters-bowed";
        if (Definition(card).Effect == "enters-dishonored") Dishonor(card);
        Emit("cardEnteredPlay", $"{player.Name} brought {Definition(card).Name} into play via {via}{(card.Bowed ? " bowed" : "")}.", player.Id, card.Id);
        QueueOverconfidentDraw(card);
    }

    private bool OpenRecruitEntryWindows(PlayerState player, CardInstance card, bool proclaimEligible, int? proclaimResumesTo)
    {
        var reactionId = EntryReactionAbility(card);
        if (proclaimEligible)
        {
            _state.PendingEntryEffectsCardId = card.Id;
            if (reactionId is not null) card.AbilityUses.Remove(reactionId);
            _state.PendingChoice = new("proclaim", player.Id, card.Id, "After entry: Proclaim for Honor" + (reactionId is not null ? ", use this card's own reaction," : "") + " or pass. Each may be used once.", proclaimResumesTo);
            _state.ConsecutivePasses = 0;
            Emit("reactionWindow", "Proclaim reaction window opened.", player.Id, card.Id);
            return true;
        }
        return OpenEntryReactionWindow(player.Id, card, 1 - player.Id) || OpenOverconfidentDrawWindow(1 - player.Id);
    }

    /// <summary>Checks the special free recruitment of a higher-Experienced Personality over a
    /// lower-level Personality with the same title. Honor Requirement and Gold are deliberately not
    /// consulted; normal control restrictions still apply.</summary>
    public OverlayQuote QuoteOverlay(int playerId, string cardId, string targetId)
    {
        if (playerId is < 0 or > 1 || !_state.Cards.TryGetValue(cardId, out var card) || !_state.Cards.TryGetValue(targetId, out var target))
            return new(cardId, targetId, 0, 0, "Unknown card, target, or player.");
        if (ProvinceEntryLockError(card) is { } overlayLocked) return new(cardId, targetId, 0, 0, overlayLocked); // P11 (CardRestrictions.cs)
        var higherLevel = ExperienceLevel(card);
        var lowerLevel = ExperienceLevel(target);
        string? error = null;
        if (_state.Phase != Phase.Dynasty) error = "Overlay recruitment is only available during the Dynasty Phase.";
        else if (card.OwnerId != playerId || card.Zone != Zone.Province || !card.FaceUp || Definition(card).Type != CardType.Personality)
            error = "Choose your own face-up Personality in a province.";
        else if (target.OwnerId != playerId || !InPlay(target) || Definition(target).Type != CardType.Personality)
            error = "Choose your Personality in play.";
        else if (!string.Equals(Experienced.BaseTitle(Definition(card).Name), Experienced.BaseTitle(Definition(target).Name), StringComparison.OrdinalIgnoreCase))
            error = "Experienced Personalities may only overlay a Personality with the same title.";
        else if (higherLevel <= lowerLevel)
            error = "The overlaying Personality must have a higher Experience Level.";
        else if (!LoyaltyAllowsControl(card, Player(playerId).Clan, playerId, RecruitGateKind.Overlay))
            error = "Loyal Personalities can only be recruited by a Clan they are aligned with.";
        else if ((Keyword(card, "Unique") || Keyword(card, "Singular")) && _state.Cards.Values.Any(c => c.Id != target.Id && c.OwnerId == playerId && InPlay(c)
            && string.Equals(Experienced.BaseTitle(Definition(c).Name), Experienced.BaseTitle(Definition(card).Name), StringComparison.OrdinalIgnoreCase) && ExperienceLevel(c) == higherLevel))
            error = $"You already control this {(Keyword(card, "Unique") ? "Unique" : "Singular")} card.";
        return new(cardId, targetId, higherLevel, lowerLevel, error);
    }

    private void Overlay(GameCommand command)
    {
        Require(command.TargetId is not null, "Choose the lower-Experience Personality to overlay.");
        var card = Card(command.CardId);
        var target = Card(command.TargetId);
        var quote = QuoteOverlay(command.PlayerId, card.Id, target.Id);
        Require(quote.Error is null, quote.Error ?? "");
        Require((command.SourceIds ?? []).Length == 0, "No Gold sources are needed for an Overlay.");

        var player = Player(command.PlayerId);
        player.DynastyActionTaken = true;
        var province = player.Provinces.Single(p => p.CardId == card.Id);
        province.CardId = null;
        _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = province.Index; // an overlay recruit enters play from a province too (Kaiu Village, issue #492 ruling R3)

        // The old Personality leaves play and the new one enters. Queue keyword triggers while each
        // card still has the state that determines its own trigger.
        QueueOverconfidentDraw(target);
        TransferOverlayState(target, card);
        RemoveFromGame(target);
        Emit("personalityOverlayed", $"{player.Name} overlayed {Definition(target).Name} with Experience Level {quote.HigherLevel}.", player.Id, card.Id, quote.HigherLevel);
        QueueOverconfidentDraw(card);
        Refill(player, province);
        P3NoteOverlay(card); // Easy Persuasion (issue #503): an overlay is not an entry
        if (!OpenEntryReactionWindow(player.Id, card)) OpenOverconfidentDrawWindow(player.Id);
    }

    private void TransferOverlayState(CardInstance lower, CardInstance higher)
    {
        higher.Zone = lower.Zone;
        higher.ProvinceIndex = lower.ProvinceIndex;
        higher.FaceUp = true;
        higher.Bowed = lower.Bowed || Definition(higher).Effect == "enters-bowed";
        higher.Dishonorable = lower.Dishonorable || Definition(higher).Effect == "enters-dishonored";
        higher.StraightenBlockedTurn = Math.Max(higher.StraightenBlockedTurn, lower.StraightenBlockedTurn);
        higher.StraightenLockedTurn = Math.Max(higher.StraightenLockedTurn, lower.StraightenLockedTurn);
        higher.StraightenHolds.AddRange(lower.StraightenHolds);
        higher.PrintedHoldsSeeded = lower.PrintedHoldsSeeded;
        higher.MovementLocks.AddRange(lower.MovementLocks);
        higher.TargetShield = lower.TargetShield;
        higher.TargetShieldTurn = lower.TargetShieldTurn; higher.ForceModifiersNegatedTurn = lower.ForceModifiersNegatedTurn; // Forgotten Legacy pass, batch 3 (issue #467)
        higher.DeepRootsTurn = lower.DeepRootsTurn; // The Shadow's Embrace pass, batch 3 (issue #476)
        higher.TargetBars.AddRange(lower.TargetBars);
        foreach (var other in _state.Cards.Values) for (var i = 0; i < other.TargetBars.Count; i++) if (other.TargetBars[i].ActorCardId == lower.Id) other.TargetBars[i] = other.TargetBars[i] with { ActorCardId = higher.Id };
        higher.AssignedToAttackTurn = Math.Max(higher.AssignedToAttackTurn, lower.AssignedToAttackTurn);

        higher.Modifiers.AddRange(lower.Modifiers);
        higher.GrantedKeywords.AddRange(lower.GrantedKeywords);
        higher.GrantedAbilities.AddRange(lower.GrantedAbilities);
        higher.RemovedKeywords.AddRange(lower.RemovedKeywords.Where(k => !higher.RemovedKeywords.Contains(k, StringComparer.Ordinal)));
        higher.RemovedClanAffiliations.AddRange(lower.RemovedClanAffiliations.Where(c => !higher.RemovedClanAffiliations.Contains(c, StringComparer.Ordinal)));
        higher.Protections.AddRange(lower.Protections);
        higher.ActionLocks.AddRange(lower.ActionLocks);
        higher.Tokens.AddRange(lower.Tokens);
        foreach (var use in lower.AbilityUses)
            if (!higher.AbilityUses.TryGetValue(use.Key, out var current) || use.Value > current) higher.AbilityUses[use.Key] = use.Value;

        foreach (var attachment in _state.Cards.Values.Where(c => c.AttachedTo == lower.Id).ToArray())
        {
            // Cultist Infiltrator (oracle-l5r-1595): "This card will not be transferred" -- the one
            // printed exception to Overlay's own attachment-carryover above. Discarded instead of
            // left dangling on `lower` (about to be RemoveFromGame'd below, which does not itself
            // relocate an attachment still pointing at it). Its own OwnerId is untouched, so this
            // already lands in the correct original owner's discard pile by construction.
            if (Definition(attachment).Effect == "cultist-infiltrator" || WillNotBeTransferred(attachment)) // Maga-yari (issue #492 group G) shares the printed text
            {
                attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.Tokens.Clear();
                Emit("attachmentDestroyed", $"{Definition(attachment).Name} was not transferred and was discarded.", attachment.OwnerId, attachment.Id);
                continue;
            }
            attachment.AttachedTo = higher.Id;
            attachment.ProvinceIndex = higher.ProvinceIndex;
        }

        // The transferred state belongs only to the replacement. Clearing the terminal card avoids
        // retaining a second, stale copy in Removed from Game.
        ResetCardState(lower);
    }

    // The one place a card sheds its time in play (issue #850). A card that leaves play for a hand, a deck, a province or a pile, or that will enter play again (a dead Personality recruited, issue #373),
    // is a new object: nothing of its earlier life follows it. AbilityVocabularyTests-style reflection test CardResetTests fails when a CardInstance property is neither reset here nor listed as identity/location.
    internal static void ResetCardStateForTest(CardInstance card) => ResetCardState(card);
    private static bool KeepsDishonorOutsidePlay(CardInstance card) =>
        card.FaceUp && card.Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard or Zone.RemovedFromGame or Zone.Province && Definition(card).Type == CardType.Personality;
    private static void ResetCardState(CardInstance card)
    {
        card.Modifiers.Clear();
        card.GrantedKeywords.Clear();
        card.GrantedAbilities.Clear();
        card.RemovedKeywords.Clear();
        card.RemovedClanAffiliations.Clear();
        card.Protections.Clear();
        card.ActionLocks.Clear();
        card.Tokens.Clear();
        card.AbilityUses.Clear();
        card.RentatsuHeld = false; card.BarrierTurn = -1; // Emperor Edition Personality pass (issue #477), batch 4.
        card.Bowed = false;
        // R-082 (EE-STATE-008, Mendon 2026-10-13): a face-up dishonorable Personality stays dishonorable outside play and on re-entry, unless an effect rehonors him.
        // "Face up" is judged by where the card is when it is reset: a public zone (a discard pile, the dead pile, removed from the game, a face-up province).
        // A hand or a deck is hidden, so a Personality sent there (or reset before he is moved there) loses the status. A caller that resets a card still in play
        // and then moves it to a public zone (Your Clan Needs You!, Step Through the Void) carries the status across itself.
        card.Dishonorable = card.Dishonorable && KeepsDishonorOutsidePlay(card);
        card.StraightenBlockedTurn = -1;
        card.StraightenHolds.Clear();
        card.PrintedHoldsSeeded = false;
        card.MovementLocks.Clear();
        card.TargetShield = false;
        card.TargetBars.Clear();
        card.ForcePenaltiesNegatedTurn = -1;
        card.ForceModifiersNegatedTurn = -1; card.TargetShieldTurn = -1; // Forgotten Legacy pass, batch 3 (issue #467)
        card.DeepRootsTurn = -1; // The Shadow's Embrace pass, batch 3 (issue #476)
        card.AssignedToAttackTurn = -1;
        card.StraightenLockedTurn = -1; card.ForceContributionNegatedBattle = -1; card.AssignBowedTurn = -1; card.DestructionShieldTurn = -1; card.OpposedTurn = -1; card.ReturnAfterBattle = -1; // issue #850: every per-play stamp
        card.PerformedActionFromTitles.Clear();
    }

    // Reserve (core rules, issue #184): a Dynasty Personality with this keyword may be recruited as an
    // "absent battle action" -- the recruiting player needs no presence of their own at this
    // battlefield, unlike every other Battle action. Two conditions instead: the card is already
    // face up in its province (a face-down Reserve card cannot be activated), there is a battlefield
    // at that province (any currently unresolved one, not necessarily the one being resolved right
    // now), and the ENEMY has presence there. Recruited directly to the battlefield, skipping the
    // assignment step entirely -- no assignment-triggered Reaction exists in the engine yet, so there
    // is nothing to actually skip today, but nothing here waits for one before this Personality is
    // fully in play either. Deferred: Proclaim's own narrow exception for a Reserve recruit made
    // possible by a defender's battlefield-creation ability at the attacker's own province -- that
    // mechanic does not exist yet, so QuotePurchase's own ProclaimEligible (which already requires
    // playerId==ActivePlayerId) is reused as-is and simply never overridden for this narrow case.
    // Akodo Ashiko (oracle-l5r-193, issue #440): "If Ashiko is face-up in one of your Provinces, Recruit her..." is the Reserve keyword's own
    // shape (an absent battle action, only her own card, no separate granting card needed -- see the Reserve keyword's reminder text on Tamori
    // Shinji and its own kind), just printed on a card that carries no other Reserve-keyword capability, so IsReserveRecruitable substitutes her
    // own Effect string for the keyword check rather than adding "Reserve" to her printed Keywords (which she does not carry).
    private bool IsReserveRecruitable(CardInstance card) => Keyword(card, "Reserve") || Definition(card).Effect == "ashiko-outnumbered-reserve";

    private void RecruitReserve(GameCommand command)
    {
        Require(_state.Phase == Phase.Battle, "Reserve recruitment is an absent battle action, only available during the Battle Phase.");
        RequireNoRestrictedBurst(command.PlayerId); // Koshin Keep (Stronghold pass, issue #486 batch 1): only the additional Ranged Attack is allowed.
        var card = Card(command.CardId);
        Require(card.OwnerId == command.PlayerId && card.Zone == Zone.Province && card.FaceUp && Definition(card).Type == CardType.Personality && IsReserveRecruitable(card),
            "Choose your own face-up Reserve Personality in a province.");
        var provinceIndex = card.ProvinceIndex!.Value;
        Require(_state.UnresolvedBattlefields.Contains(provinceIndex), "There is no battlefield at this province.");
        Require(Army(1 - command.PlayerId, provinceIndex).Any(), "The enemy must have presence at this battlefield.");
        var quote = QuotePurchase(command.PlayerId, card.Id, command.ClanDiscount, command.ClanAllegiance, viaReserve: true);
        Require(quote.Error is null, quote.Error ?? "");
        PayAdditionalEntryCost(command.PlayerId, card); // issue #501 (EmperorMechC.cs)
        Pay(command.PlayerId, card, quote.Cost, command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Recruit));
        var (province, slot) = FindProvinceSlot(Player(command.PlayerId), card.Id); // issue #554: either of Otosan Uchi's two slots
        ClearProvinceSlot(province, slot);
        _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = province.Index; // "enters play from a province" (Yogo Ugimori; issue #453, batch 2; Agasha Ryo, issue #477)
        card.Zone = Zone.Battlefield; card.FaceUp = true;
        card.Bowed = Definition(card).Effect == "enters-bowed";
        if (Definition(card).Effect == "enters-dishonored") Dishonor(card);
        Emit("reserveRecruited", $"{Player(command.PlayerId).Name} recruited {Definition(card).Name} from Reserve.", command.PlayerId, card.Id);
        QueueOverconfidentDraw(card);
        RefillProvinceSlot(Player(command.PlayerId), province, slot);
        var reactionId = EntryReactionAbility(card);
        if (quote.ProclaimEligible)
        {
            _state.PendingEntryEffectsCardId = card.Id;
            if (reactionId is not null) card.AbilityUses.Remove(reactionId);
            _state.PendingChoice = new("proclaim", command.PlayerId, card.Id, "After entry: Proclaim for Honor" + (reactionId is not null ? ", use this card's own reaction," : "") + " or pass. Each may be used once.");
            _state.ConsecutivePasses = 0;
            Emit("reactionWindow", "Proclaim reaction window opened.", command.PlayerId, card.Id);
        }
        else if (OpenEntryReactionWindow(command.PlayerId, card, 1 - command.PlayerId)) return;
        else if (OpenOverconfidentDrawWindow(1 - command.PlayerId)) return;
        // An ordinary action (never itself an Additional Action) forfeits any of this player's own
        // still-unused grants, same rule as ExecuteAbilityEffects' own tail and MatchEngine.Pass.
        _state.AdditionalActions.RemoveAll(g => g.PlayerId == command.PlayerId && Forfeitable(g));
        NextAction(command.PlayerId);
    }

    private void OrderSeppuku(GameCommand command)
    {
        // Issue #368 (Mendon): Order Seppuku is an Open action taken as an action at the player's own turn to act in the Action Phase, with
        // nothing pending -- it never interrupts another player's action, the current action, a reaction window or any pending decision.
        // ONE predicate decides that moment for the command, the offered actions and the AI (SeppukuTiming.cs).
        var timing = SeppukuTimingReason(command.PlayerId);
        Require(timing is null, timing ?? "");
        // Unfortunate Hesitation (issue #351, Mendon): Order Seppuku is an Open action, so a player who owes a pass may not take it either.
        Require(!OwesForcedPass(command.PlayerId), ForcedPassReason);
        var card = Card(command.CardId);
        Require(card.OwnerId == command.PlayerId, "You may only order Seppuku for your own Personality.");
        Require(InPlay(card) && Definition(card).Type == CardType.Personality, "Choose your Personality in play.");
        Require(card.Dishonorable, "Only a Dishonorable Personality may perform Seppuku.");
        Require(!SeppukuBarredByPriceOfHonor(card), $"{Definition(card).Name} is not affected by the rulebook seppuku action this turn (The Price of Honor)."); // issue #503
        Require(!SeppukuBarredByControl(card), $"{Definition(card).Name} will not commit seppuku while the Personality that took control of him stays bowed (P'an Ku)."); // issue #827
        // "May not perform actions" (issue #336, Mendon: even innate actions): Order Seppuku is the one action a Personality has of its own.
        Require(!ActionLocked(card), $"{Definition(card).Name} may not perform actions this turn.");
        Require(Keyword(card, "Samurai") || Keyword(card, "Courtier") || Keyword(card, "Shugenja"), "Only a Samurai, Courtier, or Shugenja may perform Seppuku.");
        // Honorable before destruction: this is what spares the controller the dishonorable-death
        // Honor penalty applied generally in MarkDead.
        Rehonor(card);
        // Seppuku is an action like any other (issue #368): once it has resolved, the opportunity passes to the other player. An
        // Overconfident window it triggers (issue #223, MarkDead already queued it inside DestroyUnits below) closes to that player.
        DestroyUnits([card], actingPlayerId: command.PlayerId);
        Emit("seppuku", $"{Definition(card).Name} performed Seppuku, dying honorably.", command.PlayerId, card.Id);
        RecordFact(FactKinds.Seppuku, command.PlayerId, card.Id); // P6 (AfterFacts.cs)
        ClearActionAccumulators(); // P6: what the destruction recorded belongs to this action, not to the next one (Order Seppuku never reached FinishActionEffectTracking)
        if (OpenOverconfidentDrawWindow(1 - command.PlayerId)) return;
        // An ordinary action forfeits the player's own still-unused Additional Action grants, same as every other action's tail.
        _state.AdditionalActions.RemoveAll(g => g.PlayerId == command.PlayerId && Forfeitable(g));
        NextAction(command.PlayerId);
    }

    // viaReserve (issue #216, Tamori Shinji): true only for a Reserve recruit made as an absent
    // battle action while being attacked (RecruitReserve) -- "enters play for 4 less Gold if you are
    // the Defender" is true by construction on that one path (you may only ever Reserve-recruit your
    // OWN province's card, and doing so requires the enemy already has presence there, i.e. they are
    // attacking YOUR province), so no separate "are you the Defender" check is needed. An ordinary
    // Dynasty-phase Purchase of the same card never qualifies.
    // ignoreHonorRequirementIfPersonalityClan (issue #301, Shinden Shorai): "You may ignore the
    // Personality's Honor Requirement if he is Lion Clan" -- a waiver supplied by the RECRUITING ACTION
    // and keyed on the RECRUITED Personality's own Clan alignment, the opposite axis from
    // CardDefinition.HonorRequirementWaiverClan (a waiver printed on the Personality itself and keyed on
    // the recruiting PLAYER's Clan). Never set by an ordinary Purchase/Reserve recruit.
    // fromDeadPile (issue #373, At Any Cost): recruiting one of the player's own dead Personalities. Only where the card is differs: every restriction below,
    // the price with all its discounts, and Unique/Singular apply as for a province card -- except that such a Personality may not be proclaimed.
    public PurchaseQuote QuotePurchase(int playerId, string cardId, bool clanDiscount = false, bool clanAllegiance = false, bool viaReserve = false, string? ignoreHonorRequirementIfPersonalityClan = null, bool fromDeadPile = false, bool anyOwnerPile = false, bool fromDynastyDeck = false)
    {
        if (playerId is < 0 or > 1 || !_state.Cards.TryGetValue(cardId, out var card)) return new(cardId, 0, clanDiscount, clanAllegiance, false, "Unknown card or player.", []);
        var def = Definition(card); var player = Player(playerId);
        // The dead pile is the Personality's discard pile: Zone.Dead once he died, Zone.DynastyDiscard when he was merely discarded from a province (Goju Plot: "in your discard pile").
        // Black Pearl (Forgotten Legacy pass, issue #467): anyOwnerPile -- "a Personality in any player's discard pile", brought into play under the payer's control (TakeControl).
        // fromDynastyDeck (Emperor Edition remaining-cards pass, group C, issue #492: Sacrifice of Pawns's search): one of the player's own Dynasty deck, priced like a province card.
        if (fromDeadPile ? card.OwnerId != playerId && !anyOwnerPile || card.Zone is not (Zone.Dead or Zone.DynastyDiscard) : fromDynastyDeck ? card.OwnerId != playerId || card.Zone != Zone.DynastyDeck : card.OwnerId != playerId || card.Zone != Zone.Province || !card.FaceUp)
            return new(cardId, 0, clanDiscount, clanAllegiance, false, fromDeadPile ? "Choose one of your own dead Personalities." : "Choose your face-up province card.", []);
        if (ProvinceEntryLockError(card) is { } entryLocked) return new(cardId, 0, clanDiscount, clanAllegiance, false, entryLocked, []); // P11 (CardRestrictions.cs): "cards will not enter play from the province" -- every province entry asks QuotePurchase
        string? error = null;
        var inClan = def.Type == CardType.Personality && ClanAlignments(card).Contains(player.Clan);
        var gate = RecruitGateFor(card, playerId, player.Clan, viaReserve ? RecruitGateKind.Reserve : RecruitGateKind.Recruit); // P3 (issue #695, RecruitGates695.cs): a refusal, a waiver of the Honor Requirement
        if (fromDeadPile ? def.Type is not (CardType.Personality or CardType.Holding) : def.Type is not (CardType.Personality or CardType.Holding)) error = fromDeadPile ? "Only a Personality or a Holding can be recruited from a discard pile." : "This card cannot be purchased."; // a Holding too (issue #653: Govern the Land, Re-fortification)
        // Loyal (core rules Glossary): a Personality with one or more Clan affiliations may only be
        // controlled by a matching Clan. With none remaining it is Unaligned, so Loyal imposes no
        // Clan restriction, including for a player whose Stronghold represents no Clan.
        else if (gate.RefuseReason is { } gateRefusal) error = gateRefusal;
        else if (def.Type == CardType.Personality && !LoyaltyAllowsControl(card, player.Clan, player.Id)) error = "Loyal Personalities can only be recruited by a Clan they are aligned with.";
        else if ((clanDiscount || clanAllegiance) && !inClan) error = "Clan discount and allegiance require your Clan's Personality.";
        else if (clanDiscount && ImperialBarsClanDiscount(card, playerId)) error = "Journey's End Keep: Personalities whose only Clan alignment is Imperial cannot be bought with Clan discount."; // issue #771 (Wave771.cs)
        // Akodo Chiyo / Kitsu Ririko (issue #299): a Personality's own printed "Clan X players may ignore
        // / it enters play ignoring Honor Requirement" waiver (CardDefinition.HonorRequirementWaiverClan),
        // keyed on the RECRUITING player's own Clan -- this is the single check every entry path that
        // spends a recruit quote (Purchase, RecruitReserve, Your Clan Needs You!) already funnels through.
        else if (def.Type == CardType.Personality && def.HonorRequirement is int requirement && player.Honor < requirement + KazutoshiHonorRequirementIncrease(playerId)/*issue #503: Bayushi Kazutoshi*/ && !clanAllegiance && !HonorRequirementWaived(def, player) && !gate.IgnoreHonorRequirement && !player.IgnoresHonorRequirements // Alter History (issue #492, group F)
            && !(ignoreHonorRequirementIfPersonalityClan is string waivedClan && HasClanAffiliation(card, waivedClan))
            && !RoninIgnoresHonorRequirement(card, playerId)) error = "Family Honor is below the Honor Requirement; Clan allegiance may allow purchase."; // Palace of the Breaking Dawn (issue #486, batch 2)
        var prosperous = def.Effect == "prosperous-discount" ? _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality) : 0;
        var reserveDiscount = viaReserve && def.Effect == "reserve-defender-discount" ? 4 : 0;
        // Akodo Ashiko (oracle-l5r-193, issue #440): "reducing her Gold Cost by 2 if there are more units on the enemy side of the current battle
        // than on your side" -- true by construction only on a Reserve recruit (viaReserve), where card.ProvinceIndex is already the battlefield
        // (the same one RecruitReserve's own Require checked has enemy presence). Army(...).Count() is the live, always-current unit count at a
        // battlefield -- already the established primitive for exactly this comparison (e.g. barred-passage-bow's own army-size check), not
        // something that needs tracking or updating separately: it is read fresh every time a quote is asked for, including after intervening
        // Battle actions have changed either side's presence.
        var ashikoDiscount = viaReserve && def.Effect == "ashiko-outnumbered-reserve" && card.ProvinceIndex is int ashikoProvince
            && Army(1 - playerId, ashikoProvince).Count() > Army(playerId, ashikoProvince).Count() ? 2 : 0;
        // "Enters play paying N less Gold if you are a <Clan> Clan player" (issue #336, Daigotsu Hotako exp3): a printed term of the card that
        // stacks with the two Clan choices above; the same one ClanEntryDiscount gives Equip.
        var entryDiscount = ClanEntryDiscount(def, player);
        // Mukudori (Seeds of Decay, issue #453): "Your other Fudo cards enter play for 1 less Gold" -- a term of the price of every entry, like ClanEntryDiscount (StaticKeywordRules.cs).
        var keywordDiscount = KeywordEntryDiscount(card, playerId);
        // Yasuki Makoto (oracle-l5r-9449, Embers of War pass, issue #460 batch 2): "Your Holdings enter play for 1 less Gold" -- the same
        // per-source-in-play term shape as Mukudori's own keywordDiscount above, narrowed to CardType.Holding rather than a keyword.
        var holdingDiscount = HoldingEntryDiscount(card, playerId);
        // Palace of the Breaking Dawn (Emperor Edition Stronghold pass, issue #486 batch 2): "Your Ronin cards enter play paying 1 less Gold."
        var roninDiscount = RoninEntryDiscount(card, playerId);
        // The Shakash / The Sleepless One (Forgotten Legacy pass, issue #467 batch 2): "Enters play paying 1 less Gold [for each Nonhuman you control, up to 3]" -- the card's own printed term.
        var (nagaDiscount, nagaTerm) = ForgottenLegacyEntryDiscount(card, playerId);
        // A Gold Cost modifier on the card itself (Wind of the Moon's "+1 Gold Cost" on a face-up Personality out of play, Forgotten Legacy pass issue #467) is part of
        // the price: CardGoldCost is the printed cost plus every GoldCost modifier (none survives a card's return to a province or a recruit from a dead pile).
        var goldCost = CardGoldCost(card);
        var rawCost = goldCost - prosperous - reserveDiscount - ashikoDiscount - (entryDiscount?.Amount ?? 0) - keywordDiscount - holdingDiscount - roninDiscount - nagaDiscount + (clanAllegiance ? 2 : 0) - (clanDiscount ? 2 : 0);
        // Registered entry cost terms (issue #682, CostTerms.cs): the generic place a card prints a price term -- a waiver, a surcharge, a discount keyed on a Ring, the Defender or the opponent's Clan.
        var entryTerms = SumEntryCostTerms(card, playerId, viaReserve ? EntryCostKind.Reserve : EntryCostKind.Recruit);
        var cost = entryTerms.Ignore ? 0 : Math.Max(0, rawCost + entryTerms.Adjustment);
        // The human-readable derivation the payment panel shows (issue #303), term by term.
        var basis = $"{def.Name} cost {goldCost}" + (prosperous > 0 ? $" − {prosperous} (controlled Personalities)" : "") + (reserveDiscount > 0 ? $" − {reserveDiscount} (Reserve while defending)" : "")
            + (ashikoDiscount > 0 ? $" − {ashikoDiscount} (outnumbered)" : "")
            + (entryDiscount is { } entry ? $" − {entry.Amount} ({entry.Clan} Clan)" : "")
            + (keywordDiscount > 0 ? $" − {keywordDiscount} (Fudo, Mukudori)" : "")
            + (holdingDiscount > 0 ? $" − {holdingDiscount} (Yasuki Makoto)" : "")
            + (roninDiscount > 0 ? $" − {roninDiscount} (Palace of the Breaking Dawn)" : "")
            + (nagaDiscount > 0 ? $" − {nagaDiscount} ({nagaTerm})" : "")
            + (clanAllegiance ? " + 2 (Clan allegiance)" : "") + (clanDiscount ? " − 2 (Clan discount)" : "") + entryTerms.Basis + (!entryTerms.Ignore && rawCost + entryTerms.Adjustment < 0 ? " (min 0)" : "");
        if ((Keyword(card, "Unique") || Keyword(card, "Singular")) && ControlsCopyOf(playerId, def.Name, ExperienceLevel(card))) error = $"You already control this {(Keyword(card, "Unique") ? "Unique" : "Singular")} card.";
        error ??= AdditionalEntryCostUnpayable(card, playerId); // En'you (issue #501, EmperorMechC.cs): an additional cost of entering play he cannot pay bars the recruit.
        // Kakita Yusugi and Yogo Ugimori (issue #453, batch 2): the once-per-turn limit he ignores, and a lock on the player (ProclaimAllowed).
        var eligible = error is null && (inClan || def.Type == CardType.Personality && ProclaimGateEligible(card, player)) && !clanDiscount && !fromDeadPile && !fromDynastyDeck && playerId == _state.ActivePlayerId && ProclaimAllowed(player, def, card);
        return new(cardId, cost, clanDiscount, clanAllegiance, eligible, error, error is null ? PaymentOptions(playerId, cost, new PaymentSubject(card, PaymentKind.Recruit)) : [], basis, PaymentSourcesFor(playerId, new PaymentSubject(card, PaymentKind.Recruit))); // issue #501: the payment tokens
    }

    // True when the card's own printed waiver names the recruiting player's own Clan (see
    // CardDefinition.HonorRequirementWaiverClan). An empty/unset field never waives anything.
    private static bool HonorRequirementWaived(CardDefinition def, PlayerState player) =>
        def.HonorRequirementWaiverClan is string clan && string.Equals(clan, player.Clan, StringComparison.Ordinal);

    // paying: what the payment is for (ConditionalProduction.cs) -- a Holding that produces more for a kind of card or action says so only when it is asked for that one.
    public int GoldProduced(string cardId, int playerId, PaymentSubject? paying = null)
    {
        if (IsPaymentToken(cardId)) return PaymentTokenAmount(cardId, playerId, paying); // Issue #501 (EmperorMechE.cs): a Reaction's Gold, Zlkyt's destroyed Holding, Moto Rani's discount.
        var card = Card(cardId); var def = Definition(card);
        if (def.Effect == MoneylenderEffect) return MoneylenderTokens(card); // Moneylender (issue #492, group F): Gold equal to the Wealth tokens it can remove.
        // The Shadow's Embrace pass (issue #476), batch 6: a production modifier for the turn (The Moon's Imperative, The Sun's Aspiration) and Sheltered Port's count.
        var adjustment = (def.Type == CardType.Holding ? ShadowsEmbraceBatch6Production(card, playerId) + PromoProductionBonus(card, playerId) + PromoCelestialCProduction(card, playerId) : 0) + PromoEmperorProductionAdjustment(card, playerId, paying) + (def.Type == CardType.Holding && def.GoldProduction > 0 ? PromoFestivalProduction(card) : 0) + PaymentHookAdjustment(card, playerId, paying); // issue #653; issue #695: EngineHooks.ProductionAdjustment and the turn modifiers (PaymentHooks695.cs): Seppun Ritisharu (Holdings), The Imperial Treasury (PromoCelestialC.cs), Plantation (a Stronghold) and Kenro-ji-jin's Bounty (the Farms), the Festivals
        if (ConditionalProductionOf(card) is { } conditional && ProductionConditionMet(conditional, paying, card)) return Math.Max(0, ConditionalAmount(conditional, paying) + adjustment);
        // Library of Rebirth (Stronghold pass, issue #486 batch 1), Luxurious Silk, Seaside Bazaar: a Reaction's Gold is its own payment source (issue #501: "<card>#reaction", EmperorMechE.cs),
        // usable beside the card's bow; the card's own id is always its printed production.
        // A card with no printed production that can bow to produce Gold for a payment (Yoritomo Sakuma, issue #338) produces that much.
        if (def.GoldProduction == 0 && AlternateGoldSource(card, paying) is { } alternate) return alternate.Amount;
        return Math.Max(0, def.GoldProduction + adjustment + ClanGoldEffects.Bonus(def.Effect, Player(playerId).Clan)); // "<clan>-gold" Effects: ClanGold.cs
    }

    public PaymentOption[] PaymentOptions(int playerId, int cost, PaymentSubject? paying = null) => PaymentOptionsExcluding(playerId, cost, null, paying);
    // The same suggestion search over the producers NOT in `exclude` -- the second half of a two-part payment (a Discipline play from a
    // discard pile pays the Discipline cost first, then the action's own cost from other sources; see SplitPaymentOptions).
    private PaymentOption[] PaymentOptionsExcluding(int playerId, int cost, IReadOnlySet<string>? exclude, PaymentSubject? paying = null)
    {
        // A source whose amount is the payer's own choice (Asahina Keigo, issue #477) is never suggested.
        var held = HeldActionSources(playerId);
        var sources = Producers(playerId, paying).Where(c => (exclude is null || !exclude.Contains(c.Id)) && !held.Contains(c.Id) && !IsVariableGoldSource(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (cost == 0) return [new([], 0, 0, 0)];
        // Issue #501 (EmperorMechE.cs): the payment tokens -- a Reaction's Gold beside a card's bow, Zlkyt's destroyed Mine or Farm, Moto Rani's discount -- join the search as groups of
        // alternatives: at most one token of a card per payment (each is that card's once-a-turn Reaction).
        var groups = sources.Select(c => new[] { (Id: c.Id, Amount: GoldProduced(c.Id, playerId, paying)) }).ToList();
        var availableTokens = PaymentTokens(playerId, paying).Where(t => !PromoNeverSuggested(t) /* issue #667: Profits' reduction is only chosen */ && (exclude is null || !exclude.Contains(t.Card.Id) && (t.Subject is null || !exclude.Contains(t.Subject.Id)))).ToArray();
        foreach (var tokenGroup in availableTokens.Where(t => !TfIsAltToken(t)).GroupBy(t => t.Card.Id))
            groups.Add(tokenGroup.Select(t => (t.Id, t.Amount)).ToArray());
        TfMergeAltIntoGroups(groups, availableTokens); // Jade Mine, Slave Pits (issue #622, TfHoldings.cs): a second way to bow joins the card's own group
        // Issue #695: a token that requires something of the other chosen sources is tried last, so a combination is kept only when the sources already in it satisfy the token.
        var requirements = availableTokens.Where(t => t.RequiresOtherSources is not null).ToDictionary(t => t.Id, StringComparer.Ordinal);
        if (requirements.Count > 0) groups = [.. groups.Where(g => !g.Any(item => requirements.ContainsKey(item.Id))), .. groups.Where(g => g.Any(item => requirements.ContainsKey(item.Id)))];
        // Dynamic programming keeps useful alternatives without exponential work for large economies.
        var byTotal = new Dictionary<int, List<string[]>> { [0] = [[]] };
        foreach (var group in groups)
        {
            foreach (var pair in byTotal.OrderByDescending(p => p.Key).ToArray())
            {
                if (pair.Key >= cost) continue;
                foreach (var (id, production) in group)
                {
                    var total = pair.Key + production;
                    if (!byTotal.TryGetValue(total, out var combinations)) byTotal[total] = combinations = [];
                    foreach (var previous in pair.Value.ToArray())
                        if (combinations.Count < 12 && (!requirements.TryGetValue(id, out var needs) || needs.RequiresOtherSources!(this, playerId, [.. previous, id]))) combinations.Add([.. previous, id]);
                }
            }
        }
        return byTotal.Where(p => p.Key >= cost).SelectMany(p => p.Value.Select(ids => new PaymentOption(ids, p.Key, cost, p.Key - cost)))
            // Issue #667: the Coin Pouch is never suggested without its Holding; Near Miss, Profits and the Pouch are suggested only when nothing else pays as well (PromoPaymentSources.cs). Issue #695: nor is a token whose RequiresOtherSources the other sources fail (PaymentHooks695.cs).
            .Where(p => PromoPaymentOptionValid(p.SourceIds) && PaymentTokenConditionError(p.SourceIds, playerId, availableTokens) is null)
            // Zlkyt's destruction of a Holding (issue #501) is suggested only when nothing else pays.
            .OrderBy(p => p.SourceIds.Count(IsDestroyingPaymentToken)).ThenBy(p => PromoOptionalSourceCount(p.SourceIds))
            // A payment that bows no alternate source (a Personality that can bow for Gold, issue #338) is always preferred to one that does:
            // the engine never suggests bowing Sakuma when printed producers cover the cost. With no such card in play every count is 0.
            .ThenBy(p => p.SourceIds.Count(id => !IsPaymentToken(id) && Definition(Card(id)).GoldProduction == 0)).ThenBy(p => p.Excess)
            // Issue #501: of two payments equally good, the one that bows fewer cards (a Reaction's Gold instead of the bow) is suggested first.
            .ThenBy(p => p.SourceIds.Count(id => !IsPaymentToken(id))).ThenBy(p => p.SourceIds.Length).ThenBy(p => string.Join(",", p.SourceIds), StringComparer.Ordinal).Take(24).ToArray();
    }

    // Issue #825: the Gold sources the held (deferred) action of this player has already committed to. A payment SUGGESTED to a Reaction taken in its window never uses them again: they are spoken for
    // (validation stays as it was, so the held action's own payment, and a redirect that must still fit it, are judged as before).
    private IReadOnlySet<string> HeldActionSources(int playerId) =>
        _state.PendingNegatableAbility is { } held && held.PlayerId == playerId && held.SourceIds is { Length: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : new HashSet<string>();

    private IEnumerable<CardInstance> Producers(int playerId, PaymentSubject? paying = null) => _state.Cards.Values.Where(c => ProducerControlledBy(c, playerId) && ProducerZone(c, Definition(c)) // issue #695 (PaymentHooks695.cs): a lent Holding, an Attached card, an in-play Strategy
        && !c.Bowed && (Definition(c).GoldProduction > 0 || MoneylenderTokens(c) > 0 || AlternateGoldSource(c, paying) is not null || ConditionalProductionOf(c) is { } conditional && ProductionConditionMet(conditional, paying, c)));
    // Library of Rebirth (issue #486, "Even if this card is bowed"), Luxurious Silk and Seaside Bazaar: their Reaction's Gold is a payment token (issue #501, PaymentSourceIds).
    // exclude: cards the paying action bows or destroys as another of its costs (AbilityCosts.CostPaidCards) -- a card cannot be bowed twice, so it produces no Gold for the same action.
    private void ValidatePayment(int playerId, int cost, string[]? sourceIds, IReadOnlySet<string>? exclude = null, PaymentSubject? paying = null)
    {
        var ids = sourceIds ?? [];
        Require(exclude is null || !ids.Any(exclude.Contains), "A card bowed or destroyed as part of the action's cost cannot also be bowed for its Gold.");
        Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "A Gold source cannot be used twice in one payment.");
        Require(cost > 0 || ids.Length == 0, "No Gold sources are needed for this free action.");
        var producers = PaymentSourceIds(playerId, paying); // issue #501: a card's own production, or one of the payment tokens (EmperorMechE.cs)
        Require(ids.All(producers.Contains), "Every Gold source must be yours, in play, unbowed and able to produce Gold.");
        RequirePaymentTokens(ids, exclude); // issue #501: one token per card, a Holding destroyed by Zlkyt not also paid as another cost
        RequirePaymentTokenConditions(ids, playerId, paying); // issue #695: a token's RequiresOtherSources and a bowing token not also its card's own bow
        RequirePaymentDiscards(playerId, ids.Where(id => !IsPaymentToken(id)).ToArray(), paying); // Asahina Keigo (issue #477): the card each such source discards is named.
        var produced = ids.Sum(id => GoldProduced(id, playerId, paying));
        Require(produced >= cost, $"This payment needs {cost} Gold; selected sources produce {produced}.");
    }
    private void Pay(int playerId, CardInstance target, int cost, string[]? sourceIds, string? part = null, IReadOnlySet<string>? exclude = null, PaymentSubject? paying = null)
    {
        ValidatePayment(playerId, cost, sourceIds, exclude, paying);
        var allIds = sourceIds ?? [];
        var tokens = PaymentTokensChosen(allIds, playerId, paying); // issue #501 (EmperorMechE.cs): read before anything is paid
        var ids = allIds.Where(id => !IsPaymentToken(id)).ToArray();
        var produced = allIds.Sum(id => GoldProduced(id, playerId, paying));
        produced -= SpendMoneylenderTokens(ids, cost, playerId, paying); // Moneylender (issue #492, group F): only the Wealth tokens the payment needs are removed.
        // A card bowed for Gold through its own ability (Yoritomo Sakuma, issue #338) has used that Reaction: spent for the turn, and named
        // in the log so the opponent sees why it bowed.
        var staysStraight = new HashSet<string>(StringComparer.Ordinal); // Emperor Edition pass (issue #477), batch 6: Yasuki Tanimura produces without bowing.
        foreach (var id in ids)
            if (Definition(_state.Cards[id]).GoldProduction == 0 && AlternateGoldSource(_state.Cards[id], paying) is { } alternate)
            {
                _state.Cards[id].AbilityUses[AbilityCatalog.All[alternate.AbilityId].UsageKey ?? alternate.AbilityId] = _state.TurnNumber;
                var bows = AbilityCatalog.All[alternate.AbilityId].PaymentBows;
                if (!bows) staysStraight.Add(id);
                Emit("goldSourceUsed", $"{Definition(_state.Cards[id]).Name} {(bows ? "bowed to produce" : "produced")} {alternate.Amount} Gold.", playerId, id);
                if (AbilityCatalog.All[alternate.AbilityId].PaymentDiscardsFocus) DiscardForVariableGoldSource(_state.Cards[id], playerId);
            }
        foreach (var id in ids) if (!staysStraight.Contains(id)) _state.Cards[id].Bowed = true;
        UsePaymentTokens(tokens, playerId); // issue #501: each token's Reaction is spent (Library of Rebirth, Luxurious Silk, Seaside Bazaar produce without bowing; Zlkyt destroys; Rani removes)
        Emit("payment", $"{Player(playerId).Name} paid {cost} Gold for {Definition(target).Name}{(part is null ? "" : $" ({part})")}, producing {produced}; {produced - cost} excess was lost.", playerId, target.Id, produced - cost);
        // Temple to Fudo (Seeds of Decay, issue #453): ":bow:: Produce 2 Gold and lose 1 Honor" -- the loss is part of producing (SeedsOfDecayBatch4.cs).
        LoseHonorForProducers(playerId, ids);
        TfAfterProducersBowed(playerId, ids); // Temple Fortress (issue #622, TfHoldings.cs): "After the first time each turn you bow this card: Gain 1 Honor"
        AfterPaymentHooks(playerId, cost, produced, paying, allIds, ids, tokens); // issue #695: EngineHooks.AfterPayment (the entry records of Moneylender, Vast Paddy Fields and Easy Persuasion, and the Gold ledger)
    }

    private void Proclaim(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        var player = Player(command.PlayerId);
        Require(pending.PlayerId == command.PlayerId && command.PlayerId == _state.ActivePlayerId, "Only the entering Personality's controller may Proclaim during their turn.");
        Require(ProclaimRejection(player, Definition(Card(pending.CardId)), pending.CardId) is null, ProclaimRejection(player, Definition(Card(pending.CardId)), pending.CardId) ?? "Proclaim has already been used this turn.");
        // Turning the Story (issue #453, batch 2): "Before a Proclaim action resolves" -- the Proclaim waits for the opponent's Reaction when they hold one (SeedsOfDecayBatch2.cs).
        if (DeferProclaimForNegation(command)) return;
        FinishProclaim(command, negated: false);
    }
    private void PassReaction()
    {
        var pending = _state.PendingChoice!;
        Emit("reactionPassed", $"{Player(_state.PriorityPlayerId).Name} passed the reaction opportunity.", _state.PriorityPlayerId);
        _state.ConsecutivePasses++;
        if (_state.ConsecutivePasses == 2) CloseReactionWindow(pending);
        else
        {
            _state.PriorityPlayerId = 1 - _state.PriorityPlayerId;
            // Issue #644: the player who would answer has no Reaction in this window (they cannot Proclaim and hold nothing to react with): their pass is a given, so the window closes.
            if (SettledReactionWindows.Contains(pending.Type) && _state.PendingNegatableAbility is null && !ReactionWindowOffers(_state.PriorityPlayerId)) CloseReactionWindow(pending, "No reaction was left to take; the window closed.");
        }
    }

    // The reaction window `pending` is over (both players passed, or nothing is left to offer either): whatever it opened for resumes.
    private void CloseReactionWindow(PendingChoice pending, string reason = "Both players passed; the reaction window closed.")
    {
        {
            _state.PendingChoice = null;
            _state.ConsecutivePasses = 0;
            _state.ReactionOrderSkipped = null;
            if (pending.Type == "proclaim") ApplyPendingEntryEffects();
            Emit("reactionClosed", reason);
            if (pending.Type is "bowReaction" or "moveReaction" or "nikutaiReaction" or "battleActionReaction" && OpenNextWatcherTurn(pending.Type)) return; // issue #898: the next watching card asks, the action carries on after the last
            // The after-battle recruit window (issue #379): whatever was not used lapses with the battle, which then finishes.
            if (pending.Type == "battleRecruit") { ExpireBattleRecruitGrants(); CompleteBattleTransition(pending.ActingPlayerId!.Value); return; }
            // The after-resolution window (issue #417, AfterResolution.cs): the resolution goes on with the winners' return home.
            if (pending.Type == "afterResolutionReaction") { FinishAfterResolutionWindow(); return; }
            // Defaults to ActivePlayerId (Dynasty-phase purchasing and mid-Battle attacks never cede
            // priority away from the active player on their own); an entry-reaction window opened by
            // an Action-phase card (Equip, a played Terrain) instead sets ActingPlayerId to the
            // opponent, so the normal "any action cedes priority" rule still holds once it closes.
            // The Engage window (issue #310) is the one window that decides WHO resumes: whoever holds the
            // first opportunity once its claims resolve (the Defender by default).
            var resumeTo = pending.Type == "engageReaction" ? CloseEngageWindow() : pending.ActingPlayerId ?? _state.ActivePlayerId;
            resumeTo = AfterActionWindowResumeTo(pending, resumeTo); // issue #648: an additional Battle action granted inside the after-action window goes back to its taker
            if (pending.Type == "duelWonReaction" && P3FinishStashedDuel(resumeTo)) return; // Kakita Kenta (issue #503): the duel's action is now truly done
            // Overconfident (issue #223): a chance to react to anything THIS window's own resolution
            // caused to enter or leave play (Bayushi Mugoshi entering during a Proclaim/entryReaction
            // window and being slandered elsewhere, a Reaction that destroyed something, etc.) before
            // finally resuming where this window itself would have.
            if (OpenOverconfidentDrawWindow(resumeTo)) return;
            _state.PriorityPlayerId = resumeTo;
        }
    }

    // The deferred action, once its window has closed: negated as a whole (a reaction negated it: Yoritomo Sakuma, issue #338; Jinn-Kuen, issue #335) it is taken and
    // its costs paid but its effects are not applied (ActionNegation.cs), otherwise it resolves. A negation signal must never survive the one re-invocation it was set
    // for, so the per-card ones are cleared here.
    // A Battle action that is not an ability, set aside behind an announce window (issue #503, EmperorP3Priority.cs), runs as the window closes.
    private void RunSetAsideBattleCommand(GameCommand pending)
    {
        switch (pending.Type)
        {
            case "equip": Equip(pending); break;
            case "playStrategy": PlayStrategy(pending); break;
            case "recruitReserve": RecruitReserve(pending); break;
        }
    }
    private void ResolveDeferredAction(GameCommand pending)
    {
        var negated = _state.DeferredActionNegated; _state.DeferredActionNegated = false;
        // Issue #825: a Gold source the held action named was bowed or destroyed while its window was open (the other player's Reaction). The Gold was produced when the action was announced, but this
        // engine pays at resolution, so the payment can no longer be made: the action is aborted like one whose cost was blocked, rather than leaving a window nobody can close.
        if (HeldPaymentBroken(pending)) { AbortActionForBlockedCost(pending, brokenPayment: true); _state.NegatedEffects.Clear(); return; }
        if (negated) ResolveNegatedAction(pending, ignoreCosts: P3CostsIgnored(pending)); else ExecuteAbilityEffects(pending); // Iron Will (issue #503): its costs ignored
        _state.NegatedEffects.Clear();
        RestoreTemporaryRemovals(TemporaryRemovals.Action);
    }

    private void Pass()
    {
        Emit("passed", $"{Player(_state.PriorityPlayerId).Name} passed.", _state.PriorityPlayerId);
        // Twist of Fate (Emperor Edition remaining-cards pass, issue #492, group D): passing the additional action ends it, and the Dynasty Phase carries on.
        if (_state.TwistOfFateActive) { EndTwistOfFateAction(); return; }
        switch (_state.Phase)
        {
            case Phase.Action:
                {
                    // An additional action in the Action phase (Seeds of Decay, issue #453: Daigotsu Yamato, Shinjo Katsuo) may be taken as a pass (Mendon's item 2, issue #501): the pass spends one
                    // grant, and the holder keeps the priority while he holds another. Only both players passing in succession ends the phase (CountPass), not one player passing twice.
                    var passer = _state.PriorityPlayerId;
                    var keeps = PassSpendsAdditionalAction(passer);
                    if (CountPass(passer)) { _state.AdditionalActions.RemoveAll(Forfeitable); BeginPhaseAfterAction(); } // issue #667: the next printed phase of the turn's order
                    else _state.PriorityPlayerId = keeps ? passer : 1 - passer;
                    break;
                }
            // The Declaration Segment: passing IS declining to attack, and a declined attack ends the Attack Phase
            // right here -- no battlefield exists, so there are no Maneuvers segments and no defender prompts.
            case Phase.AttackDeclaration: DeclineAttackPhase(); break;
            // Assigning no Infantry is not declining the attack: it was already declared, so Cavalry Maneuvers
            // still gets its own opportunity and every battlefield is still fought (an attacker is "not
            // compelled to assign any units").
            case Phase.InfantryDeclaration:
            case Phase.CavalryDeclaration: AssignAttackers(new("attack", _state.PriorityPlayerId, _state.Version, Assignments: [])); break;
            case Phase.DefenseAssignment:
            case Phase.CavalryDefense:
                AssignDefense(new("assignDefense", _state.PriorityPlayerId, _state.Version, Assignments: []));
                break;
            case Phase.Battle:
                // Additional Action (issue #181): passing forfeits any of this player's own still-
                // unused grants rather than letting them linger for a future, ordinary turn -- the
                // whole point of an Additional Action is that it resolves immediately, in a burst.
                // The Killing Grounds (Emperor Edition remaining-cards pass, issue #492, group D): the first pass while it stands makes the passing player destroy one of his Personalities there.
                if (BeginKillingGroundsOnPass()) break;
                FinishBattlePass();
                break;
            case Phase.Dynasty:
            case Phase.DynastyDiscard: if (BeginTwistOfFateWindow()) break; EndDynastyPhase(); break;
            default: throw new RuleViolation("Choose a battlefield before continuing.");
        }
    }

    // The rest of a pass in the Combat Segment (Pass, and the Killing Grounds decision that interrupts it).
    private void FinishBattlePass()
    {
        // Mendon's item 2 (issue #501, EmperorMechA.cs): a pass by a player holding additional actions is one of them taken as a pass -- it spends one grant, and he keeps the priority while he holds
        // another; the battle resolves only when both players have passed in succession (CountPass), never on one player's passes alone.
        var passer = _state.PriorityPlayerId;
        var keeps = PassSpendsAdditionalAction(passer);
        EndAttackerLeadIfCeded(passer, keeps); // Entrenched Position (Forgotten Legacy pass, issue #467 batch 3)
        if (CountPass(passer)) ResolveBattle(); else _state.PriorityPlayerId = keeps ? passer : 1 - passer;
    }

    private void DiscardProvince(GameCommand command)
    {
        Require(_state.Phase is Phase.Dynasty or Phase.DynastyDiscard, "Only discard provinces at the end of the Dynasty Phase.");
        var card = Card(command.CardId);
        Require(card.OwnerId == command.PlayerId && card.Zone == Zone.Province && card.FaceUp, "Choose your face-up province card.");
        var province = Player(command.PlayerId).Provinces.Single(p => p.CardId == card.Id);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null;
        province.CardId = null;
        Player(command.PlayerId).DynastyActionTaken = true;
        Emit("provinceDiscard", $"{Player(command.PlayerId).Name} discarded {Definition(card).Name} from a province.", command.PlayerId, card.Id);
        Refill(Player(command.PlayerId), province);
        SetPhase(Phase.DynastyDiscard, command.PlayerId);
    }

    // The Declaration Segment (rules lead: "an attack is either declared or not"): one explicit decision by the
    // Attacker. Declaring creates a battlefield at EVERY one of the Defender's provinces, whether or not anyone is
    // ever assigned to it (core rules: even a battlefield with no units on either side has its one battle);
    // declining ends the Attack Phase at once (DeclineAttackPhase).
    private void DeclareAttackPhase(GameCommand command)
    {
        Require(_state.Phase == Phase.AttackDeclaration, "Declare an attack after the Action Phase.");
        Require(!Player(command.PlayerId).MayNotAttackThisTurn && !AttackLockedByEvent(command.PlayerId), "You may not declare an attack this turn.");
        _state.UnresolvedBattlefields = Player(1 - command.PlayerId).Provinces.Where(p => !p.Destroyed).Select(p => p.Index).Order().ToList();
        Emit("attackDeclared", $"{Player(command.PlayerId).Name} declared an attack: a battlefield exists at each of {Player(1 - command.PlayerId).Name}'s provinces.", command.PlayerId);
        PromoAttackPhaseCreated(command.PlayerId); // issue #653: Yozo's Inspiration, Diplomacy Talks
        Wave835AttackDeclared(); // issue #835: Imperial Deployment straightens his unit
        CreateTwoFrontsBattlefields(command.PlayerId); // Two Fronts (issue #492, rulings pass R1): a second battlefield at the marked province.
        SetPhase(Phase.InfantryDeclaration, command.PlayerId);
    }
    private void DeclineAttackPhase()
    {
        // Spiritual Manipulation (The Shadow's Embrace pass, issue #476, batch 4): "must declare a normal attack this turn".
        if (ForcedAttackDeclineRefusal(_state.PriorityPlayerId) is string forcedDecline) throw new RuleViolation(forcedDecline);
        Emit("attackDeclined", $"{Player(_state.PriorityPlayerId).Name} declined to attack; the Attack Phase ends.", _state.PriorityPlayerId);
        _state.UnresolvedBattlefields = []; R1.TwoFronts.RemoveAll(m => m.Turn <= _state.TurnNumber);
        BeginPhaseAfterAttack(_state.ActivePlayerId); // issue #667: the Dynasty Phase in the default order, otherwise the turn ends
    }
    // Infantry Maneuvers (InfantryDeclaration/DefenseAssignment) runs first, then Cavalry Maneuvers
    // (CavalryDeclaration/CavalryDefense), both on the battlefields the declaration already created: assigning
    // places units at them but never decides whether a battlefield exists.
    private void AssignAttackers(GameCommand command)
    {
        Require(_state.Phase is Phase.InfantryDeclaration or Phase.CavalryDeclaration, "Assign attackers after declaring an attack.");
        // Utaku Chikako's own restriction (issue #186): a broad "may not declare an attack this turn"
        // -- blocks any non-empty attacker assignment outright, Infantry or Cavalry alike, regardless
        // of what would otherwise grant the assignment opportunity (a future "Ambush"-style card
        // included, since this checks the assignment itself, not a specific path to it). An empty
        // assignment (declining to attack) is always still legal.
        Require(!(command.Assignments?.Count > 0) || !Player(command.PlayerId).MayNotAttackThisTurn && !AttackLockedByEvent(command.PlayerId), "You may not declare an attack this turn.");
        var cavalry = _state.Phase == Phase.CavalryDeclaration;
        RequireRetributionAssignment(command); // Emperor Edition remaining-cards pass, group C (issue #492): an additional Attack Phase assigns one Personality at most.
        // Spiritual Manipulation (The Shadow's Embrace pass, issue #476, batch 4): "and assign the target (if legal)" -- in the segment he can be assigned in.
        if (ForcedAssignmentRefusal(command.PlayerId, command.Assignments, cavalry) is string forcedAssignment) throw new RuleViolation(forcedAssignment);
        var targets = AttackableBattlefields(command.PlayerId).ToHashSet(); // every battlefield the declaration created (a province's, and Two Fronts' additional ones)
        Assign(command, targets, cavalry, false);
        NoteRetributionAssignments(command); // issue #492, group C: Retribution's +1F for the Personality and his Followers.
        Emit("attackersAssigned", $"{Player(command.PlayerId).Name} assigned {(command.Assignments?.Count ?? 0)} {(cavalry ? "Cavalry" : "Infantry")} attacker(s).", command.PlayerId);
        // Brash (core rules keyword, issue #216): the Defender may draw a card, independently, once
        // per Brash card just assigned to attack -- deferred here, before the usual phase transition,
        // so the Defender resolves every offer first; FinishBrashDraws makes that same transition
        // once the queue empties.
        var brashCount = command.Assignments?.Keys.Count(id => Keyword(Card(id), "Brash")) ?? 0;
        if (brashCount > 0)
        {
            _state.PendingBrashDraws = brashCount;
            _state.PendingChoice = new("brashDraw", 1 - command.PlayerId, null, "Brash: you may draw a card.");
            _state.PriorityPlayerId = 1 - command.PlayerId;
            Emit("brashDrawOffered", $"{Player(1 - command.PlayerId).Name} may draw a card ({brashCount} Brash {(brashCount == 1 ? "card" : "cards")} assigned to attack).", 1 - command.PlayerId);
            return;
        }
        if (_state.ExtraInfantrySegment) { AdvanceExtraInfantry(attackerStep: false); return; } // Hidden Falls Dojo (Stronghold pass, issue #486 batch 1): the additional Infantry Maneuvers Segment.
        SetPhase(cavalry ? Phase.CavalryDefense : Phase.DefenseAssignment, 1 - command.PlayerId);
    }
    private void ResolveBrashDraw(GameCommand command)
    {
        Require(command.Type == "brashDraw" && command.PlayerId == _state.PendingChoice!.PlayerId && command.Choice is "draw" or "decline", "Choose whether to draw a card.");
        if (command.Choice == "draw") Draw(Player(command.PlayerId));
        _state.PendingBrashDraws--;
        if (_state.PendingBrashDraws > 0) return;
        _state.PendingChoice = null;
        var cavalry = _state.Phase == Phase.CavalryDeclaration;
        if (_state.ExtraInfantrySegment) { AdvanceExtraInfantry(attackerStep: false); return; } // Hidden Falls Dojo (issue #486).
        SetPhase(cavalry ? Phase.CavalryDefense : Phase.DefenseAssignment, command.PlayerId);
    }
    private void AssignDefense(GameCommand command)
    {
        Require(_state.Phase is Phase.DefenseAssignment or Phase.CavalryDefense, "Defenders assign after the attacking army.");
        var cavalry = _state.Phase == Phase.CavalryDefense;
        Assign(command, _state.UnresolvedBattlefields.ToHashSet(), cavalry, true);
        // Hidden Falls Dojo (Stronghold pass, issue #486 batch 1): "After each Cavalry Maneuvers Segment there is an additional Infantry Maneuvers Segment" -- and that segment ends the maneuvers.
        if (_state.ExtraInfantrySegment) { _state.ExtraInfantrySegment = false; SetPhaseAfterManeuvers(_state.ActivePlayerId); return; }
        if (cavalry && BeginHiddenFallsSegment()) return;
        if (cavalry) SetPhaseAfterManeuvers(_state.ActivePlayerId); else SetPhase(Phase.CavalryDeclaration, _state.ActivePlayerId); // Warded Paths (issue #492, group E): the Defender may choose the battle order.
    }
    private void Assign(GameCommand command, HashSet<int> provinces, bool cavalry, bool isDefender)
    {
        foreach (var assignment in command.Assignments ?? [])
        {
            var card = Card(assignment.Key);
            Require(card.OwnerId == command.PlayerId && card.Zone == Zone.Home && (!card.Bowed || MayAssignBowed(card)) && Definition(card).Type == CardType.Personality, "Assign only your unbowed Personalities at home.");
            // Xijkt (issue #207 follow-up): "may assign during Cavalry Maneuvers if you are the
            // Defender" -- a narrow, card-specific Cavalry-eligibility override (checked here rather
            // than broadening IsCavalryUnit's own shared logic for one card), additive only: it never
            // removes his own default Infantry eligibility, only adds Cavalry while defending.
            var cavalryEligible = IsCavalryUnit(card) || isDefender && Definition(card).Effect == "cavalry-if-defender";
            Require(!cavalry || cavalryEligible, "Only a Cavalry unit may assign during Cavalry Maneuvers."); // a Cavalry unit may assign in either segment; an Infantry unit only in Infantry Maneuvers
            Require(!(cavalry && CavalryAssignmentBarredP6()), new BarNotice("Assigning Personalities during the Cavalry Maneuvers Segment", "Incredible Tragedy").Message); // issue #503
            Require(provinces.Contains(assignment.Value), "Choose an existing enemy province battlefield.");
            Require(!MovementLocked(card, MovementKinds.Assign), $"{Definition(card).Name} may not be assigned to a battlefield this turn.");
            if (_state.ExtraInfantrySegment) RequireHiddenFallsAssignment(card, assignment.Value); // Hidden Falls Dojo (Stronghold pass, issue #486 batch 1): its Scouts, to the provinces it targeted.
            Require(isDefender || !MovementLocked(card, MovementKinds.AssignAttack), $"{Definition(card).Name} may not be assigned to attack this turn."); // Emperor Edition Stronghold pass (issue #486), batch 3: The Khan's Estate.
            // The Shadow's Embrace pass (issue #476), batch 6: Shieldman's unit is never assigned to an attacking army; Star-Crossed Lovers never to each other's battlefields.
            RequireShieldmanMayAssign(card, isDefender);
            RequireAsukaMayAssign(card, isDefender); // Goju Asuka (issue #501): "may not assign ... into a defending army".
            RequireHoteiMayAssign(card, isDefender); // Hotei's Contentment (issue #503)
            RequireDelayedArrivalMayAssign(card, isDefender); // Delayed Arrival (issue #492, group F): "may not be assigned to attack you until your next turn begins".
            RequireStarCrossedMayAssign(card, assignment.Value, command.Assignments!);
            Require(!RestrictionBarsAssignment(card, isDefender), $"{Definition(card).Name} may not {(isDefender ? "defend" : "attack")}."); // P11 (CardRestrictions.cs): "may not attack" / "may not defend"
            RequireRestrictionPairsMayAssign(card, assignment.Value, command.Assignments!); // P11: "may not be assigned to the same battlefield as"
            card.Zone = Zone.Battlefield; card.ProvinceIndex = assignment.Value;
            _state.AssignedThisCommand.Add(card.Id); // Chrysanthemum Blossom (issue #492 group G).
            NoteIvoryAssignment(card, assignment.Value); // Back to the Front (issue #622): the battlefields he was assigned to this turn.
            foreach (var attached in Attachments(card.Id)) attached.ProvinceIndex = assignment.Value;
            // Tamori Tamoko's own Open action (issue #207 follow-up): "gain 1 Honor if he was NOT
            // assigned to attack your provinces this turn" -- a turn-stamped flag, the same
            // StraightenBlockedTurn convention, since by the time it is checked (End Phase) the
            // battle may have already resolved and returned this card home, losing a live Zone check.
            card.AssignedToAttackTurn = _state.TurnNumber;
            Emit("unitAssigned", $"{Definition(card).Name} assigned to province {assignment.Value + 1}.", command.PlayerId, card.Id);
            PromoEmperorAssigned(card, cavalry); // Tsuruchi Nishima's Naval, Fudo's Charging tokens (issue #653, PromoEmperorPersonalities.cs)
            FireMiddayShadowAssigned(card); // Midday Shadow Court (Stronghold pass, issue #486 batch 1): "After the next time this turn he is assigned" -- to either side.
            P4Assigned(card); // The Menacing Sky (issue #503, EmperorP4Watches.cs): "after the next time he assigns".
            Wave829Assigned(card); // issue #829: Minor Blackmail's watch
            FireVeiledMenaceAssigned(card); // Veiled Menace (Emperor Edition remaining-cards pass, issue #492, group D): "After each time he is assigned (this turn)" -- to either side.
            if (!isDefender) FireConsecrationWatches(card, assignment.Value); // Consecration (Emperor Edition remaining-cards pass, issue #492, group E): "assigns ... to an attacking army".
            // "After the next time this turn he is assigned to attack" (Imperial Protection, Fair Warning; issue #453, batch 2): an attacker's assignment only.
            if (!isDefender) FireAssignedToAttackWatches(card);
        }
    }
    // Cavalry (core rules Attack Phase): a unit is Cavalry only if the Personality AND every attached
    // Follower carry the Cavalry keyword -- there is no printed "Infantry" keyword, so any unit
    // missing Cavalry anywhere in it defaults to Infantry and must assign, if at all, during Infantry
    // Maneuvers, regardless of how many of its other cards are Cavalry.
    private bool IsCavalryUnit(CardInstance personality) => Keyword(personality, "Cavalry") &&
        Attachments(personality.Id).Where(c => Definition(c).Type == CardType.Follower).All(f => Keyword(f, "Cavalry"));
    private void SelectBattle(GameCommand command)
    {
        Require(_state.Phase == Phase.BattleSelection, "Battle order may be chosen between battles.");
        Require(command.Province is int && _state.UnresolvedBattlefields.Contains(command.Province.Value), "Choose an unresolved battlefield.");
        EngageBattle(command.Province!.Value, null);
    }
    // The Attacker has chosen a battle (SelectBattle), or an effect fights one (Shinobi Assault, Chikushudo's Trickery: EmperorRulingR1.cs). `firstOpportunity` is the player an effect grants the first
    // opportunity to take a Battle action or pass, a claim like any other (Defender's own wins); null for none.
    private void EngageBattle(int battlefield, int? firstOpportunity)
    {
        _state.CurrentBattleProvince = battlefield; _state.BattleNumber++;
        NoteP2BattlefieldFought(battlefield); // issue #503 (EmperorP2Keirei.cs): the battlefields of this Attack Phase, fought ones included
        _state.EnemyDestroyedOwnPersonalitySinceLastBattleAction = new bool[2]; // Revenge (issue #437): "this battle" bounds the window.
        _state.ReconStrategyActionsThisBattle = new int[2]; // Advanced Intelligence (Seeds of Decay pass, issue #453, batch 1): "this battle" too.
        // An effect's battle is fought inside the phase it was played in (BeginAsideBattle has already set it up); a chosen one is a phase of its own.
        if (R1.Aside is null) SetPhase(Phase.Battle, 1 - _state.ActivePlayerId); else _state.PriorityPlayerId = 1 - _state.ActivePlayerId;
        // Battle Phase timing (issue #310): the Defender holds the first opportunity by default (spec 4.4).
        // "Engaging" is exactly this moment -- the Attacker has just selected the battle (spec 4.1) -- so the
        // Engage window (spec 4.2) opens here, before any Battle action, whenever something could be played in
        // it; its claims can flip the first opportunity (CloseEngageWindow). Nothing playable: exactly the
        // pre-#310 flow, event stream included.
        _state.FirstActionPlayerId = 1 - _state.ActivePlayerId; _state.FirstActionReason = BattleSteps.ReasonDefault; _state.FirstActionRestriction = null;
        ApplyYoritomosGuidanceAfterEngaging(battlefield); // Yoritomo's Guidance (Emperor Edition remaining-cards pass, issue #492, group E): "After engaging, if the Attacker is a Mantis Clan player".
        var label = BattlefieldLabel(battlefield);
        if (OpenEngageWindow(battlefield))
        {
            if (firstOpportunity is int claimant) ClaimFirstOpportunity(claimant, claimant == _state.ActivePlayerId ? "Grant" : "Defender", null);
            Emit("battleStarted", $"Battle at {label}: engaged.");
            return;
        }
        if (firstOpportunity == _state.ActivePlayerId)
        {
            // No window opened: the Attacker's grant stands unopposed (ResolveFirstOpportunity's result for a lone Attacker claim).
            _state.FirstActionPlayerId = _state.ActivePlayerId; _state.FirstActionReason = BattleSteps.ReasonEngageGrant; _state.PriorityPlayerId = _state.ActivePlayerId;
            _state.AttackerLeadBattle = _state.BattleNumber;
            Emit("firstOpportunity", $"{Player(_state.ActivePlayerId).Name} has the first opportunity to take a Battle action or pass ({BattleSteps.ReasonEngageGrant}).", _state.ActivePlayerId);
        }
        MarkCombatSegmentBegan(); // Issue #503 (EmperorP5.cs): no Engage window, so the Combat Segment begins now (Headbutt).
        Emit("battleStarted", $"Battle at {label}: {(firstOpportunity == _state.ActivePlayerId ? "attacker" : "defender")} acts first.");
    }
    private void ResolveBattle()
    {
        var provinceIndex = _state.CurrentBattleProvince!.Value;
        var attacker = _state.ActivePlayerId; var defender = 1 - attacker;
        ExpireUntilResolutionProvinceStrength(); // Kaiu Hideaki (issue #477 batch 2): "until the battle's resolution begins".
        EndCombatSegmentShields(); // Chikushudo's Trickery (issue #492, rulings pass R1): "will not be destroyed during the battle's Combat Segment".
        // Before This Battle Resolves (issue #272): "occur after all players have passed but before
        // resolution is determined" -- deliberately checked BEFORE Army() is ever read below, since
        // Encircled Terrain's own effect (sending every other Personality home) changes who actually
        // fights. See PendingBeforeBattleResolve's own doc comment for the full sequencing.
        var sources = BeforeResolutionItemIds(provinceIndex);
        if (sources.Length > 0)
        {
            _state.PendingBeforeBattleResolve = new PendingBeforeBattleResolve { ProvinceIndex = provinceIndex, Attacker = attacker, Defender = defender, RemainingSourceIds = sources.ToList() };
            AdvanceBeforeBattleResolve();
            return;
        }
        ContinueResolveBattle(provinceIndex, attacker, defender);
    }
    // Every in-play card at this battlefield carrying a "before this battle resolves / before this battle's
    // resolution" trait (issue #310, generalizing issue #272's Encircled-Terrain-only list): a card in play
    // on the battlefield itself (a Terrain, a Personality) or attached to a unit there, whose Abilities name an
    // ability of Timing "BeforeResolution" -- or whose Effect names one, which is how Encircled Terrain (Effect
    // "encircled-before-battle") is registered without touching its published data. A list, not a single id,
    // since the rule is written for several simultaneous effects (ordered by the active player; see
    // AdvanceBeforeBattleResolve). Ordinal-sorted so the offered order is deterministic.
    private string[] BeforeBattleResolveSourceIds(int provinceIndex) =>
        _state.Cards.Values.Where(c => (c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex || c.Zone == Zone.Attached && c.ProvinceIndex == provinceIndex && Card(c.AttachedTo).Zone == Zone.Battlefield)
                && BeforeResolutionAbilityId(c) is not null)
            .Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();
    private static string? BeforeResolutionAbilityId(CardInstance card) =>
        (Definition(card).Abilities ?? []).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "BeforeResolution")
        ?? (AbilityCatalog.All.TryGetValue(Definition(card).Effect, out var effectAbility) && effectAbility.Timing == "BeforeResolution" ? Definition(card).Effect : null);
    // Drains PendingBeforeBattleResolve.RemainingSourceIds one at a time: zero left resumes ordinary
    // resolution; exactly one left resolves it directly (no real choice to offer); two or more open
    // "beforeBattleOrder" for the ACTIVE player (the core rule's own "whose turn it is") to pick which
    // resolves next.
    private void AdvanceBeforeBattleResolve()
    {
        var pending = _state.PendingBeforeBattleResolve!;
        if (pending.RemainingSourceIds.Count == 0)
        {
            var (p, a, d) = (pending.ProvinceIndex, pending.Attacker, pending.Defender);
            _state.PendingBeforeBattleResolve = null;
            ContinueResolveBattle(p, a, d);
            return;
        }
        if (pending.RemainingSourceIds.Count == 1)
        {
            pending.ActiveSourceId = pending.RemainingSourceIds[0];
            pending.RemainingSourceIds.RemoveAt(0);
            StartBeforeBattleEffect(pending);
            return;
        }
        _state.PendingChoice = new("beforeBattleOrder", _state.ActivePlayerId, null, "Choose which 'before this battle resolves' effect resolves next.");
        _state.PriorityPlayerId = _state.ActivePlayerId;
    }
    private void ResolveBeforeBattleOrder(GameCommand command)
    {
        var pending = _state.PendingBeforeBattleResolve!;
        Require(command.Type == "beforeBattleOrder" && command.PlayerId == _state.ActivePlayerId && command.TargetId is not null && pending.RemainingSourceIds.Contains(command.TargetId),
            "Choose one of the remaining 'before this battle resolves' effects.");
        pending.RemainingSourceIds.Remove(command.TargetId!);
        pending.ActiveSourceId = command.TargetId;
        _state.PendingChoice = null;
        StartBeforeBattleEffect(pending);
    }
    private void StartBeforeBattleEffect(PendingBeforeBattleResolve pending)
    {
        var itemId = pending.ActiveSourceId!;
        if (itemId.StartsWith(DelayedItemPrefix, StringComparison.Ordinal)) { StartDelayedItem(pending, itemId); return; }
        if (itemId.StartsWith(ReactionItemPrefix, StringComparison.Ordinal)) { StartReactionItem(pending, itemId); return; }
        var source = Card(itemId);
        var traitId = BeforeResolutionAbilityId(source)!;
        var province = pending.ProvinceIndex;
        // Each source reads the battlefield live, one at a time, so an earlier source's effect (a Personality sent
        // home, bowed or boosted) is already reflected when the next one applies -- which is why the order the
        // active player chooses (AdvanceBeforeBattleResolve) can change who fights.
        Emit("beforeBattleEffect", $"Before this battle resolves: {Definition(source).Name}.", source.OwnerId, source.Id);
        switch (traitId)
        {
            // Encircled Terrain (issue #272): "the Attacker and Defender, in the order of your
            // choice" -- "your" is THIS card's own controller, who picks whether the Attacker or the
            // Defender selects their own keeper Personality first.
            case "encircled-before-battle":
                pending.EncircledStage = "ChooseOrder";
                _state.PendingChoice = new("encircledOrder", source.OwnerId, source.Id, "Choose whether the Attacker or the Defender selects a Personality first.");
                _state.PriorityPlayerId = source.OwnerId;
                return;
            // Training fixtures (issue #310) proving the general path: a mandatory effect, a mandatory effect that
            // changes who fights, and an optional ("you may") one that waits for its controller's answer.
            case "training-before-resolution-boost":
                foreach (var own in Army(source.OwnerId, province).ToArray()) ApplyModifier(own, source.Id, traitId, "Force", 2);
                break;
            case "training-before-resolution-send-home":
                foreach (var weak in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Definition(c).Type == CardType.Personality && PersonalityForce(c) <= 3).ToArray())
                    ReturnHome(weak, false);
                break;
            case "training-before-resolution-may-bow":
                _state.PendingChoice = new("beforeBattleOptional", source.OwnerId, source.Id, $"Before this battle resolves: you may bow one enemy Personality at this battlefield ({Definition(source).Name}).");
                _state.PriorityPlayerId = source.OwnerId;
                return;
            // Heart of Darkness (Forgotten Legacy pass, issue #467 batch 4): the same optional choice, naming zero to two cards (ForgottenLegacyBatch4.cs).
            case "heart-of-darkness-bow":
                _state.PendingChoice = new("beforeBattleOptional", source.OwnerId, source.Id, $"Before this battle resolves: choose and bow zero to two cards at this battlefield with 4 or greater Force ({Definition(source).Name}).");
                _state.PriorityPlayerId = source.OwnerId;
                return;
            // Rocky Terrain (Emperor Edition remaining-cards pass, group C, issue #492): the same choice over the attacking Personalities (EmperorRemainingC.cs).
            case "rocky-terrain-bow":
                _state.PendingChoice = new("beforeBattleOptional", source.OwnerId, source.Id, $"Before this battle resolves: you may bow one or two attacking Personalities at this battlefield ({Definition(source).Name}).");
                _state.PriorityPlayerId = source.OwnerId;
                return;
            // Fields of Victory (The Shadow's Embrace pass, issue #476 batch 3; ShadowsEmbraceBatch3.cs): mandatory, read now.
            case "fields-of-victory-zero": FieldsOfVictory(source, pending); break;
            // Extended Maneuvers (Emperor Edition remaining-cards pass, group A, issue #492): its controller's two optional choices (EmperorRemainingA.cs), when there is anything to choose.
            case "extended-maneuvers-before-battle": if (OpenExtendedManeuversChoice(source, pending)) return; break;
            // Emperor Edition remaining-cards pass (issue #492), group B (EmperorRemainingB.cs): Heart of Rokugan's mandatory sweep; Hard Pressed's choice of a unit to move home.
            case "heart-of-rokugan-sweep": HeartOfRokuganSweep(source, pending); break;
            case "hard-pressed-move": if (BeginHardPressed(source, pending)) return; break;
            // Lakeside Retreat (issue #622, TfStrategiesB.cs): its controller straightens a Personality or Follower.
            case "lakeside-retreat-straighten": if (BeginLakesideRetreat(source, pending)) return; break;
            // A Moving Battle (issue #741, Wave741.cs): its controller may move one of his opposed attacking Personalities to an adjacent unresolved battlefield.
            case Wave741Abilities.MovingBattle: if (BeginWave741MovingBattle(source)) return; break;
            // Warded Premises (issue #835, Wave835.cs): mandatory, read now.
            case Wave835Abilities.WardedNegate: Wave835WardedNegate(source); break;
        }
        FinishBeforeBattleEffect(pending);
    }
    // The "you may" half of an optional before-resolution effect: its own controller either declines or names the one
    // target it applies to. Answered with command type "beforeBattleOptional" and Choice "use" (plus TargetId) or "decline".
    private void ResolveBeforeBattleOptional(GameCommand command)
    {
        var pending = _state.PendingBeforeBattleResolve!;
        var choice = _state.PendingChoice!;
        Require(command.Type == "beforeBattleOptional" && command.PlayerId == choice.PlayerId && command.Choice is "use" or "decline", "Use the effect or decline it.");
        if (IsHeartOfDarknessChoice(pending)) ResolveHeartOfDarkness(command, pending, choice.PlayerId);
        else if (IsExtendedManeuversChoice(pending)) ResolveExtendedManeuvers(command, pending, choice.PlayerId); // Extended Maneuvers (issue #492)
        else if (IsHardPressedChoice(pending)) ResolveHardPressed(command, pending, choice.PlayerId);
        else if (IsLakesideRetreatChoice(pending)) ResolveLakesideRetreat(command, choice.PlayerId); // Lakeside Retreat (issue #622)
        else if (IsWave741MovingBattleChoice(pending)) ResolveWave741MovingBattle(command, choice.PlayerId); // A Moving Battle (issue #741)
        else if (command.Choice == "use")
        {
            var target = Card(command.TargetId);
            Require(target.OwnerId != choice.PlayerId && target.Zone == Zone.Battlefield && target.ProvinceIndex == pending.ProvinceIndex && Definition(target).Type == CardType.Personality, "Choose an enemy Personality at this battlefield.");
            if (!StalwartNegatesBow(target, choice.PlayerId)) target.Bowed = true;
        }
        _state.PendingChoice = null;
        FinishBeforeBattleEffect(pending);
    }
    private void FinishBeforeBattleEffect(PendingBeforeBattleResolve pending)
    {
        pending.ActiveSourceId = null; pending.EncircledStage = null; pending.EncircledFirstSelectedId = null;
        AdvanceBeforeBattleResolve();
    }
    private void ResolveEncircledOrder(GameCommand command)
    {
        var pending = _state.PendingBeforeBattleResolve!;
        var choice = _state.PendingChoice!;
        Require(command.Type == "encircledOrder" && command.PlayerId == choice.PlayerId && command.Choice is "attacker" or "defender",
            "Choose the Attacker or the Defender to select first.");
        var firstPlayer = command.Choice == "attacker" ? pending.Attacker : pending.Defender;
        PromptEncircled(pending, choice.CardId, firstPlayer, true);
    }
    // A player with no Personality at this battlefield has nothing to keep and nothing to choose, so his turn to select passes by itself (found by the bot soak, issue #642:
    // the match used to stall on a prompt with no answer).
    private bool HasEncircledCandidate(int playerId, PendingBeforeBattleResolve pending) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == pending.ProvinceIndex && Definition(c).Type == CardType.Personality);
    private void PromptEncircled(PendingBeforeBattleResolve pending, string? terrainId, int player, bool first)
    {
        if (!HasEncircledCandidate(player, pending))
        {
            if (first) { pending.EncircledFirstSelectedId = null; PromptEncircled(pending, terrainId, 1 - player, false); }
            else FinishEncircled(pending, terrainId, null);
            return;
        }
        pending.EncircledStage = first ? "FirstSelect" : "SecondSelect";
        _state.PendingChoice = new("encircledSelect", player, terrainId, "Choose one of your Personalities at this battlefield to keep; every other Personality here will be sent home.");
        _state.PriorityPlayerId = player;
    }
    private void FinishEncircled(PendingBeforeBattleResolve pending, string? terrainId, string? lastKeptId)
    {
        var keepIds = new HashSet<string>(StringComparer.Ordinal);
        if (pending.EncircledFirstSelectedId is not null) keepIds.Add(pending.EncircledFirstSelectedId);
        if (lastKeptId is not null) keepIds.Add(lastKeptId);
        foreach (var other in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == pending.ProvinceIndex && Definition(c).Type == CardType.Personality && !keepIds.Contains(c.Id)).ToArray())
            ReturnHome(other, false);
        Emit("encircledTerrainResolved", "Encircled Terrain: every other Personality at this battlefield was sent home.", pending.Attacker, terrainId);
        _state.PendingChoice = null;
        FinishBeforeBattleEffect(pending);
    }
    private void ResolveEncircledSelect(GameCommand command)
    {
        var pending = _state.PendingBeforeBattleResolve!;
        var choice = _state.PendingChoice!;
        Require(command.Type == "encircledSelect" && command.PlayerId == choice.PlayerId && command.TargetId is not null, "Choose one of your Personalities at this battlefield.");
        var card = Card(command.TargetId!);
        Require(card.OwnerId == command.PlayerId && card.Zone == Zone.Battlefield && card.ProvinceIndex == pending.ProvinceIndex && Definition(card).Type == CardType.Personality,
            "Choose one of your own Personalities at this battlefield.");
        if (pending.EncircledStage == "FirstSelect")
        {
            pending.EncircledFirstSelectedId = card.Id;
            PromptEncircled(pending, choice.CardId, 1 - command.PlayerId, false);
            return;
        }
        FinishEncircled(pending, choice.CardId, card.Id);
    }
    // The original body of ResolveBattle, unchanged, now reached either directly (no "before this
    // battle resolves" effects in play) or once PendingBeforeBattleResolve's own sequence empties.
    private void ContinueResolveBattle(int provinceIndex, int attacker, int defender)
    {
        var a = Army(attacker, provinceIndex).ToArray(); var d = Army(defender, provinceIndex).ToArray();
        MarkOpposed(a, d); // Sentinel Spirit (issue #453, batch 2): "if it was opposed this turn".
        var (af, df) = ArmyForces(a, d, attacker, defender);
        // Battles without a province (Mendon, issue #492 rulings pass R1): a battlefield whose province is gone (created without one, or destroyed before now) resolves as normal except that
        // no province may be destroyed, so nothing that triggers on destroying a province can trigger.
        var battleProvince = BattlefieldProvince(provinceIndex);
        Emit("battleResolved", $"{(battleProvince is null && provinceIndex >= FirstAdditionalBattlefield ? "A battlefield without a province" : $"Province {provinceIndex + 1}")}: attacking Force {af}, defending Force {df}.");
        // Reconnaissance resets to No for both players at this battlefield once its battle resolves,
        // regardless of the outcome -- the only trigger that clears it.
        ResetBattlefieldReconnaissance(provinceIndex, defender);
        var attackerWon = af > df; var defenderWon = df > af;
        // Rend the Soul (issue #498, EmperorFollowupsDuel.cs): the army a marked Personality is in would win, so the battle is tied instead.
        if (RendTheSoulTiesBattle(a, d, attackerWon, defenderWon)) { attackerWon = false; defenderWon = false; }
        // What the resolution did, for the Reactions that name it (AfterResolution.cs).
        var destroyedByPlayer = new int[2]; var provinceDestroyedNow = false; var losers = new List<int>();
        // Aramasu's Legacy (issue #501, EmperorMechA.cs): during its Attack Phase "battle resolution does not destroy defending armies or provinces", and a win is recorded "(even against no units)".
        var aramasuSpares = AramasuResolutionActive(attacker);
        if (aramasuSpares && attackerWon) NoteAramasuBattleWon();
        if (attackerWon)
        {
            // Yoritomo Iwashi (issue #453, batch 3): every Personality of the winning attacking army remembers this victory (SeedsOfDecayBatch3.cs).
            MarkAttackVictory(a);
            var destroyedDefenders = aramasuSpares ? 0 : DestroyUnits(TfResolutionDestroyable(d, true)); // issue #622: Hitomi's Devotion spares its unit (TfGear.cs)
            destroyedByPlayer[attacker] = destroyedDefenders; losers.Add(defender);
            if (!RehonorWinners(a)) { GainHonor(attacker, ResolutionHonor(attacker, defender, destroyedDefenders * 2), "Battle victory"); CheckEarthFlowsBonus(attacker, a); }
            var province = battleProvince;
            var destroyedProvince = province is not null && !aramasuSpares && !PerfectMomentBarsDestruction(attacker) && af > df + EffectiveProvinceStrength(province) && !RendBarsProvinceDestruction(attacker); // Rend the Soul (issue #498): "You will not destroy the province in this battle's resolution"
            if (destroyedProvince && Wave829JizoReplacesDestruction(attacker, provinceIndex)) destroyedProvince = false;
            if (destroyedProvince) { DestroyProvince(defender, province!); NotePerfectMomentDestruction(attacker); }
            if (destroyedProvince) AfterProvinceDestroyedByResolution(attacker, defender, province!, a); // Alter History, Boastful Proclamation (issue #492, group F).
            provinceDestroyedNow = destroyedProvince;
            // Vital Pathways (issue #272): "After this battlefield's province is destroyed, if you
            // are the Attacker" -- checked here, at the actual moment of destruction, since
            // DestroyProvince only ever runs in this same attacker-won branch and a later hook would
            // be too late to reason about "was it destroyed by THIS resolution" cleanly. d is this
            // resolution's own (pre-destruction) defending army snapshot, so d.Length>0 exactly
            // captures "your current army was ever opposed during this battle" for a single-round
            // battle engine.
            if (destroyedProvince) ResolveVitalPathwaysDraw(provinceIndex, attacker, d.Length > 0);
            if (destroyedDefenders > 0 || destroyedProvince) AttackerDestructionBonus(attacker, a);
            // Ring recruitment (issue #194): checked with this resolution's own facts, before the
            // per-battle flags below reset. False Ring of Air and Ring of Earth require an outright
            // win; Ring of Water and Ring of Fire do not (see the tie branch below).
            GrantRingRecruit(Player(attacker), d.Length > 0, "false-ring-of-air-recruit");
            GrantRingRecruit(Player(attacker), d.Length > 0, "ring-of-earth-recruit");
            CheckRingOfWaterRecruit(Player(attacker), destroyedDefenders > 0 || destroyedProvince);
            GrantRingRecruit(Player(attacker), destroyedDefenders > 0 || destroyedProvince, "ring-of-fire-recruit");
        }
        else if (defenderWon)
        {
            var destroyedAttackers = DestroyUnits(TfResolutionDestroyable(a, false));
            destroyedByPlayer[defender] = destroyedAttackers; losers.Add(attacker);
            if (!RehonorWinners(d)) { GainHonor(defender, ResolutionHonor(defender, attacker, destroyedAttackers * 2), "Battle victory"); CheckEarthFlowsBonus(defender, d); }
            GrantRingRecruit(Player(defender), a.Length > 0, "false-ring-of-air-recruit");
            GrantRingRecruit(Player(defender), a.Length > 0, "ring-of-earth-recruit");
            CheckRingOfWaterRecruit(Player(defender), destroyedAttackers > 0);
            GrantRingRecruit(Player(defender), destroyedAttackers > 0, "ring-of-fire-recruit");
        }
        else if (a.Length > 0 && d.Length > 0)
        {
            // Tie (issue #186): check and rehonor BEFORE destruction -- a dishonorable member about to
            // be destroyed in the same tie must never also take its own dishonorable-death Honor
            // penalty (MarkDead) on top of this battle's own gain negation.
            var attackerHadDishonorable = RehonorWinners(a);
            var defenderHadDishonorable = RehonorWinners(d);
            var destroyedA = DestroyUnits(TfResolutionDestroyable(a, false)); var destroyedD = aramasuSpares ? 0 : DestroyUnits(TfResolutionDestroyable(d, true)); // Aramasu's Legacy (issue #501): the defending army is not destroyed, the attacking one is
            // A tie: both players are the battle's loser (Mendon, issue #417: Tales of Dishonor).
            destroyedByPlayer[attacker] = destroyedD; destroyedByPlayer[defender] = destroyedA; losers.Add(attacker); losers.Add(defender);
            // The Earth Flows (issue #264) is deliberately NOT checked here: its own text requires
            // being "in the WINNING army," which a tie -- by definition -- never has.
            if (!attackerHadDishonorable) GainHonor(attacker, ResolutionHonor(attacker, defender, destroyedD), "Tied battle");
            if (!defenderHadDishonorable) GainHonor(defender, ResolutionHonor(defender, attacker, destroyedA), "Tied battle");
            if (destroyedD > 0) AttackerDestructionBonus(attacker, a);
            // Ring of Water/Fire (issue #194): no "win" requirement, unlike False Ring of Air/Ring of
            // Earth -- a tie still counts as "a battle resolves" for both players independently.
            CheckRingOfWaterRecruit(Player(attacker), destroyedD > 0);
            CheckRingOfWaterRecruit(Player(defender), destroyedA > 0);
            GrantRingRecruit(Player(attacker), destroyedD > 0, "ring-of-fire-recruit");
            GrantRingRecruit(Player(defender), destroyedA > 0, "ring-of-fire-recruit");
        }
        // After the losing units are destroyed, the province is destroyed and Honor is settled: the Reactions that name this moment (AfterResolution.cs), before the winners go home.
        MarkSeekingTheTruthResolution(destroyedByPlayer, provinceDestroyedNow ? attacker : null); // Forgotten Legacy pass (issue #467 batch 4).
        Wave738ProvinceDestroyed(attacker, provinceDestroyedNow); // issue #738 (Wave738.cs): Journey's End Siege, "after the first time you destroy a Province in battle this turn".
        WlGrantVoidRecruit(attacker, provinceDestroyedNow); // issue #674 (WlSpellsRings.cs): False Ring of the Void's recruit, "after a battle resolves where you destroyed a Province"
        FireCowedAndDefeatedBattleWatches(a, d); // The Shadow's Embrace pass (issue #476), batch 3: "a battle resolves which destroyed him" (ShadowsEmbraceBatch3.cs).
        PromoApplyEventResolution(a, d, attacker, destroyedByPlayer, provinceDestroyedNow); // A Great Victory (issue #653)
        PromoApplyResolution(a, d, attacker, destroyedByPlayer, provinceDestroyedNow); // Imminent Victory (issue #653, PromoEmperorFateReactions.cs): the +1F/+1C tokens at battle resolution.
        ApplyWlStratTornResolutionTraits(destroyedByPlayer, provinceDestroyedNow ? attacker : null); // issue #674 (WlStratTorn.cs): Spoils of Exploration's draw.
        ApplyShibasGuidanceTrait(destroyedByPlayer, provinceDestroyedNow ? attacker : null); // Shiba's Guidance (Emperor Edition remaining-cards pass, issue #492, group E): "After a battle resolution ends in which a Phoenix Clan player destroyed ...".
        WlPersTornApplyResolution(a, attacker, destroyedByPlayer, provinceDestroyedNow); // Utaku Suying (issue #674, WlPersTorn.cs): "after a battle resolution in which Suying's army destroyed a defending army or province"
        var resolutionFacts = NewAfterResolutionFacts(provinceIndex, attacker, attackerWon, a, destroyedByPlayer, provinceDestroyedNow, losers);
        // Words Have Strength (Emperor Edition remaining-cards pass, issue #492, group D): the cards the resolution destroyed are the losing army (both armies in a tie).
        resolutionFacts.DestroyedCardIds = (attackerWon ? (aramasuSpares ? [] : d) : defenderWon ? a : (a.Length > 0 && d.Length > 0 ? (aramasuSpares ? a : a.Concat(d).ToArray()) : [])).Select(c => c.Id).ToList(); // Aramasu's Legacy (issue #501): a spared army was not destroyed
        // The Shadow Court (Emperor Edition remaining-cards pass, issue #492, group D): "after the next time this turn ... a battle resolution destroys your Courtier" -- a trait, before the Reactions.
        if (OpenShadowCourtResolutionTrait(resolutionFacts)) return;
        if (BeginBanditGangTraits(resolutionFacts)) return; // Bandit Gang (issue #492 group G): a trait, so before any Reaction to the resolution's end.
        if (OpenAfterResolutionWindow(resolutionFacts)) return;
        ContinueAfterResolution(provinceIndex, attacker, attackerWon, a);
    }
    // The rest of the resolution once its after-resolution Reactions are done: the Conqueror's choice, the return home, the close of the battlefield.
    private void ContinueAfterResolution(int provinceIndex, int attacker, bool attackerWon, CardInstance[] a)
    {
        // Conqueror (core rules Glossary): only relevant when the attacker won -- the attacking army
        // (a) is untouched in that branch (only defenders are destroyed), so candidates read off it
        // here are still accurate even if the choice must be deferred to the attacker.
        var conquerorCandidates = attackerWon ? a.Where(c => c.Zone == Zone.Battlefield && Keyword(c, "Conqueror")).Select(c => c.Id).ToArray() : [];
        // The Plain of Glass (issue #503, EmperorP5.cs): "You may use the rulebook Conquest ability twice per turn during the same battle" -- two Conquerors of the battle.
        if (PlainOfGlassConquestTwice(attacker) && conquerorCandidates.Length == 2) { FinishBattleResolution(attacker, provinceIndex, a, conquerorCandidates[0], conquerorCandidates[1]); return; }
        if (conquerorCandidates.Length > 1)
        {
            _state.PendingConqueror = new PendingConqueror { PlayerId = attacker, ProvinceIndex = provinceIndex, CandidateIds = conquerorCandidates };
            _state.PendingChoice = new("conqueror", attacker, null, "Choose a Conqueror personality to straighten instead of returning home bowed.");
            _state.PriorityPlayerId = attacker;
            Emit("conquerorChoice", "Choose a Conqueror personality to straighten.", attacker);
            return; // FinishBattleResolution runs once the attacker picks (ResolveConqueror).
        }
        FinishBattleResolution(attacker, provinceIndex, a, conquerorCandidates.Length == 1 ? conquerorCandidates[0] : null);
    }
    private void ResolveConqueror(GameCommand command)
    {
        var pending = _state.PendingConqueror!;
        Require(command.Type == "chooseConqueror" && command.PlayerId == pending.PlayerId && command.TargetId is not null && pending.CandidateIds.Contains(command.TargetId),
            "Choose one of the eligible Conqueror personalities.");
        var chosenId = command.TargetId!;
        var a = Army(pending.PlayerId, pending.ProvinceIndex).ToArray();
        _state.PendingChoice = null; _state.PendingConqueror = null;
        if (ReopenConquerorChoiceForPlainOfGlass(pending, chosenId)) return; // The Plain of Glass (issue #503, EmperorP5.cs): its second Conquest in the same battle.
        var firstConqueror = P5.FirstConquerorId;
        FinishBattleResolution(pending.PlayerId, pending.ProvinceIndex, a, firstConqueror ?? chosenId, firstConqueror is null ? null : chosenId);
    }
    // Common tail of battle resolution: attacking units return home (bowed, except the Conqueror
    // choice, if any), then the battlefield closes out. Split out so a deferred Conqueror choice can
    // reach the same completion as the immediate (0 or 1 candidate) path.
    private void FinishBattleResolution(int attacker, int provinceIndex, CardInstance[] a, string? conquerorUnbowedId, string? secondConquerorId = null)
    {
        P5.FirstConquerorId = null; // issue #503: The Plain of Glass's pending first choice is spent
        var willNotBow = _state.ResolutionWillNotBowPlayerIds.ToArray();
        Wave737NatsuMarksUnbowed(a); // Natsu-Togumara's Guidance (issue #737, Wave737.cs): "battle resolution does not bow cards in that Personality's unit."
        // Hiruma Hikazu (The Shadow's Embrace pass, issue #476 batch 1): the Personalities with unbowed Followers in his army, read before anything bows.
        var hikazuSpared = HikazuSparedPersonalities(a);
        var aramasuUnbowed = AramasuResolutionActive(attacker) ? AramasuUnbowedByResolution(a.Where(c => c.Zone == Zone.Battlefield)) : null; // Aramasu's Legacy (issue #501): "does not bow your Mantis Clan Magistrates or your attachments"
        foreach (var card in a.Where(c => c.Zone == Zone.Battlefield))
        {
            // Conqueror (issue #492, ruling R5 on whole-unit bow/straighten): "Straighten his unit. It will not bow from the resolution" -- every card of it straightens (each unless its own straightening is
            // prevented), not just the return home unbowed.
            var conquered = card.Id == conquerorUnbowedId || card.Id == secondConquerorId; // issue #503: The Plain of Glass's second Conqueror
            if (conquered) StraightenUnit(card);
            var wasBowed = card.Bowed;
            ReturnHome(card, !conquered && !willNotBow.Contains(card.OwnerId) && !ResolutionWillNotBow(card));
            if (hikazuSpared.Contains(card.Id)) card.Bowed = wasBowed; // "(Their Followers still bow.)"
            if (conquered) Emit("conquerorStraightened", $"{Definition(card).Name} returned home unbowed (Conqueror).", card.OwnerId, card.Id);
        }
        RestoreAramasuUnbowed(aramasuUnbowed);
        // Terrain leaves the battlefield here, after its printed effects (already applied earlier in
        // ResolveBattle) but before the battlefield itself closes out -- never carried into a later
        // battle at the same province, or across turns. Looting the Ruins (issue #272) is the one
        // exception: "After this battle resolves, if this Terrain is in play, put it into your hand"
        // overrides the default discard fate, so it is left in play here and handled instead by the
        // dedicated After This Battle Resolves hook below (ResolveAfterBattleResolveEffects), which
        // runs strictly after this sweep -- exactly the "after this battle resolves" ordering its own
        // printed text requires.
        foreach (var terrain in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && Keyword(c, "Terrain") && Definition(c).Effect != "looting-return-to-hand").ToArray())
            DiscardTerrain(terrain);
        _state.UnresolvedBattlefields.Remove(provinceIndex); _state.CurrentBattleProvince = null;
        R1.MovementNegated.Remove(provinceIndex); // Chikushudo's Trickery: "negate all movement to it" ends with the battlefield.
        Wave735EndBattle(); // The Inevitable Grasp of Conquest (issue #735, Wave735.cs): "this battle" ends here.
        // After This Battle Resolves (issue #272): stashed here since CurrentBattleProvince is
        // already cleared above, and the Overconfident draw window (below) may defer this whole tail
        // across a command boundary before FinishBattleReturnHomeAndTransition ever reads it back --
        // same reasoning PendingConqueror.ProvinceIndex already documents.
        _state.ResolvedBattleProvinceIndex = provinceIndex;
        // Ashigaru Elite's boost is "for the remainder of the battle," not the turn -- a turn can
        // contain more than one battle (Infantry/Cavalry Maneuvers), so it must not carry into the next.
        _state.AshigaruBoost = new int[2];
        // First-opportunity state (issue #310) is battle-scoped too: a later battle starts from the default again.
        _state.PendingEngage = null; _state.FirstActionPlayerId = null; _state.FirstActionReason = null; _state.FirstActionRestriction = null;
        // A delayed effect and a "will not bow" promise belong to their battle: both lapse with it, applied or not.
        _state.DelayedBattleEffects.Clear(); _state.ResolutionWillNotBowPlayerIds.Clear(); _state.ResolutionWillNotBowCardIds.Clear();
        // "Remove those Followers from the game after the battle ends" (Kyuden Hida - exp): wherever they stand now (attached, or discarded with a destroyed host).
        foreach (var createdId in _state.CreatedUntilBattleEndsIds.ToArray())
            if (_state.Cards.TryGetValue(createdId, out var created) && created.Zone != Zone.RemovedFromGame) RemoveFromGame(created);
        _state.CreatedUntilBattleEndsIds.Clear();
        DiscardP5AfterBattle(); // issue #503 (EmperorP5Hidden.cs): Searching for Answers' "Discard him after the battle ends".
        // Additional Action grants/restrictions (issue #181) are battle-scoped too -- Two Hands'
        // "ceases during battle resolution" is explicit, and an unused Again!/Equip(Battle) grant
        // makes no sense carried into a battle it was never meant for.
        // Ring recruitment (issue #194): a grant scoped to a Ring card (added above, in ResolveBattle,
        // BEFORE this same call) must survive this Clear() -- it represents a right earned as of THIS
        // battle's own conclusion, to be used in a later window, unlike an ordinary Additional Action
        // grant (which genuinely is scoped to just this battle).
        // (Issue #379: only a recruit grant -- the one kind that carries a battle -- survives: this battle's is kept for the aftermath window that
        // follows (OpenBattleRecruitWindow), where it is the last chance to use it, and lapses there.)
        _state.AdditionalActions.RemoveAll(Forfeitable);
        _state.PendingAdditionalAction = null; _state.NoNewAdditionalActionsThisBattle = false; _state.NoNewAdditionalActionsBy = null;
        // An unused Attack-target grant ("your next Ranged Attack action this battle") ends with the battle.
        _state.AttackTargetGrants.Clear();
        _state.BattleAbilityLocks.Clear(); // Yoritomo Naoto (Emperor Edition Personality pass, issue #477): "until this battle ends".
        // Force redirection (issue #216) is "for the duration of the battle," same reasoning.
        _state.ForceRedirects.Clear();
        // Ring of Water's own recruitment condition (issue #194) is battle-scoped, same reasoning.
        foreach (var p in _state.Players) { p.TerrainPlayedThisBattle = false; p.TerrainDestroyedThisBattle = false; }
        if (_state.WinnerId is not null) return;
        // Overconfident (issue #223): a chance to react to anything this battle's own resolution
        // (DestroyUnits, above) just killed, before the battle actually closes out. Deferred here,
        // by the same "let ResolveOverconfidentDraw recognize the state shape" convention as its own
        // resume logic below, rather than stashing attacker/provinceIndex for a callback.
        if (OpenOverconfidentDrawWindow(attacker)) return;
        FinishBattleReturnHomeAndTransition(attacker);
    }
    // Split out so ResolveOverconfidentDraw's own deferred resume (see above) can re-enter exactly
    // here once every queued Overconfident offer this battle produced has been resolved.
    private void FinishBattleReturnHomeAndTransition(int attacker)
    {
        // Your Clan Needs You! (oracle-l5r-9687, issue #295): "After the battle ends, if he is in
        // play..." -- checked first, before anything else below, since this is the earliest point at
        // which the just-finished battle's own outcome (including FinishBattleResolution's own
        // attacker-return sweep and any Overconfident window) has fully settled where every card in
        // play now sits. Deferring here (a province choice) re-enters this same function from scratch
        // once answered, so nothing below runs twice.
        if (CheckYourClanNeedsYouReturn()) return;
        // The shared delay mechanism (issue #492, EmperorRulingR2.cs): what was delayed "until after this battle ends" is applied now, the units having returned home (Halls of the Forgotten, Deadly Orders).
        // A window such a destruction opens (an Overconfident draw) is answered first; the battle's end resumes here afterwards, and nothing is left to apply the second time.
        if (ApplyDelayedEffects(DelayUntil.EndOfBattle) && (_state.WinnerId is not null || OpenOverconfidentDrawWindow(attacker))) return;
        // Seeking the Truth (Forgotten Legacy pass, issue #467 batch 4): "After this battle ends, if you did not destroy the battlefield's province or an army, discard a card at random."
        ResolveSeekingTheTruthWatches();
        // After This Battle Resolves (issue #272): "after the resolution of the current battle but
        // before the next battlefield is chosen. If it is the last battlefield to resolve, then
        // return the card to the player's hand and then end the battle phase" -- run first, so the
        // return-to-hand always happens before the UnresolvedBattlefields.Count==0 branch below ends
        // the Battle phase, exactly the order Looting the Ruins' own printed text requires.
        ResolveAfterBattleResolveEffects();
        // "After the resolution of a battle...: you may put this Ring into play" (issue #379): the recruit grants this battle produced get their
        // window here, still inside the battle, and lapse with it.
        if (OpenBattleRecruitWindow(attacker)) return;
        CompleteBattleTransition(attacker);
    }
    // The last step of a battle: the next battlefield is chosen, or the Battle phase ends. Split from FinishBattleReturnHomeAndTransition so the
    // recruit window's close (PassReaction) can continue exactly here.
    private void CompleteBattleTransition(int attacker)
    {
        foreach (var card in _state.Cards.Values) card.GrantedAbilities.RemoveAll(g => g.Duration == GrantDuration.Battle);
        EndBattleActionLocks(); // Emperor Edition remaining-cards pass (issue #492), group B: Nerve Strike's lock lasts until the battle ends.
        if (_state.UnresolvedBattlefields.Count == 0 && R1.Aside is not null) FinishAsideBattle(attacker); // a battle fought inside the Action Phase (Shinobi Assault, Chikushudo's Trickery)
        else if (_state.UnresolvedBattlefields.Count == 0)
        {
            var aramasuUnbowed = AramasuResolutionActive(attacker) ? AramasuUnbowedByResolution(_state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.OwnerId == attacker)) : null; // Aramasu's Legacy (issue #501)
            foreach (var card in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield).ToArray()) ReturnHome(card, card.OwnerId == attacker);
            RestoreAramasuUnbowed(aramasuUnbowed);
            ClearAdditionalBattlefields(); // the additional battlefields cease to exist with the Attack Phase
            EndAttackPhase(attacker); // Emperor Edition remaining-cards pass, group C (issue #492): Retribution's window, then the Dynasty Phase.
        }
        else SetPhase(Phase.BattleSelection, BattleSelectionChooser(attacker)); // Warded Paths (Emperor Edition remaining-cards pass, issue #492, group E).
    }
    // Your Clan Needs You! (oracle-l5r-9687, issue #295): "discard all his attachments and put him
    // into one of your provinces, face-up, discarding any card there" -- checked once per watched id,
    // one PendingChoice at a time (a province choice is genuine player agency, unlike Earth Flows'
    // own unconditional Honor gain). Processes/removes every watched id it can resolve outright (a
    // card no longer in play, or with no province left to place him in, needs no choice at all);
    // returns true the moment it must pause for one, leaving any further watched ids for the next call
    // once ResolveYourClanNeedsYouProvince re-enters FinishBattleReturnHomeAndTransition.
    private bool CheckYourClanNeedsYouReturn()
    {
        foreach (var id in _state.YourClanNeedsYouWatchIds.ToArray())
        {
            _state.YourClanNeedsYouWatchIds.Remove(id);
            if (!_state.Cards.TryGetValue(id, out var card) || !InPlay(card)) continue;
            // "Discard" (not "destroy") his attachments -- a plain removal, not DestroyAttachment's own
            // destruction (which would spuriously fire Expendable/Destroyed-during-battle bonuses for
            // something that was never actually destroyed).
            foreach (var attachment in Attachments(card.Id).ToArray())
            {
                attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.FaceUp = true; attachment.Tokens.Clear();
                RemoveAttachmentGrants(card, attachment.Id);
                Emit("attachmentDiscarded", $"{Definition(attachment).Name} was discarded.", attachment.OwnerId, attachment.Id);
            }
            if (card.Zone == Zone.Battlefield) ReturnHome(card, false);
            // Returning to a province is a fresh start, the same reset ToDeck() already applies to a
            // card headed back into a deck -- stale Modifiers/GrantedKeywords/Tokens/AbilityUses have
            // no meaning once this card is sitting face-up in a province again, ready to be purchased.
            var wasDishonorable = card.Dishonorable; // R-082: he goes into a province face up, a public zone, so the status travels with him
            ResetCardState(card);
            card.Dishonorable = wasDishonorable;
            var owner = Player(card.OwnerId);
            if (!owner.Provinces.Any(p => !p.Destroyed)) continue; // no province left to place him in -- an extremely rare edge case; he simply stays home
            _state.PendingChoice = new("yourClanNeedsYouProvince", card.OwnerId, id, $"{Definition(card).Name}: choose a province to place him into, face-up (discarding any card already there).");
            _state.PriorityPlayerId = card.OwnerId; _state.ConsecutivePasses = 0;
            Emit("reactionWindow", "Your Clan Needs You!: choose a province.", card.OwnerId, id);
            return true;
        }
        return false;
    }
    private void ResolveYourClanNeedsYouProvince(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "chooseYourClanNeedsYouProvince" && command.PlayerId == pending.PlayerId && command.Province is int provinceIndex
            && Player(pending.PlayerId).Provinces.Any(p => p.Index == provinceIndex && !p.Destroyed && !HoldsNoDynasty(p)), // issue #503: Illusory Defense's province
            "Choose one of your own non-destroyed provinces.");
        var card = Card(pending.CardId!);
        var province = Player(pending.PlayerId).Provinces.Single(p => p.Index == command.Province!.Value);
        if (province.CardId is string existingId)
        {
            var existing = Card(existingId);
            existing.Zone = Zone.DynastyDiscard; existing.ProvinceIndex = null; existing.FaceUp = true;
            Emit("provinceDiscard", $"{Player(pending.PlayerId).Name} discarded {Definition(existing).Name} from a province (Your Clan Needs You!).", pending.PlayerId, existingId);
        }
        card.Zone = Zone.Province; card.ProvinceIndex = province.Index; card.FaceUp = true;
        province.CardId = card.Id;
        Emit("cardPlacedInProvince", $"{Definition(card).Name} was placed face-up into a province (Your Clan Needs You!).", pending.PlayerId, card.Id);
        _state.PendingChoice = null;
        FinishBattleReturnHomeAndTransition(_state.ActivePlayerId);
    }
    // Looting the Ruins (issue #272): "After this battle resolves, if this Terrain is in play, put it
    // into your hand" -- unconditional, no PendingChoice/Reaction window ("requires no interruption
    // of play to execute"). Exempted from FinishBattleResolution's own generic Terrain-discard sweep
    // above specifically so it can still be Zone.Battlefield here; any other Terrain at this same
    // province is already gone by the time this runs.
    private void ResolveAfterBattleResolveEffects()
    {
        var provinceIndex = _state.ResolvedBattleProvinceIndex;
        _state.ResolvedBattleProvinceIndex = null;
        ResolveWlStratACNoWayOut(provinceIndex); // No Way Out (issue #674, WlStratACEffects.cs): "After this battle resolves, if its Province was not destroyed, destroy the targets"
        if (provinceIndex is null) return;
        foreach (var terrain in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex.Value && Definition(c).Effect == "looting-return-to-hand").ToArray())
            ReturnToHand(terrain);
    }
    // Vital Pathways (issue #272): "After this battlefield's province is destroyed, if you are the
    // Attacker: Draw a card, or two cards if your current army was ever opposed during this battle."
    // Its own scan corrects the issue's paraphrase -- this is a Terrain (Zone.Battlefield), not a
    // Region, and has no separate Defender clause at all.
    private void ResolveVitalPathwaysDraw(int provinceIndex, int attacker, bool opposed)
    {
        var vitalPathways = _state.Cards.Values.FirstOrDefault(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && c.OwnerId == attacker && Definition(c).Effect == "vital-pathways-draw");
        if (vitalPathways is null) return;
        Draw(Player(attacker));
        if (opposed) Draw(Player(attacker));
        Emit("vitalPathwaysDraw", $"{Player(attacker).Name} drew {(opposed ? "two cards" : "a card")} (Vital Pathways).", attacker, vitalPathways.Id);
    }
    // Plains of the Maiden: after the Attacker destroys a province or army in resolution, gain Honor
    // equal to their surviving Battle Maidens at this battlefield. Checked against units still in the
    // battlefield zone after destruction, so a Battle Maiden destroyed in the same (tied) resolution
    // does not count toward her own bonus.
    private void AttackerDestructionBonus(int attacker, CardInstance[] attackingArmy)
    {
        if (Definition(Card(Player(attacker).StrongholdId)).Effect != "plains-honor") return;
        var maidens = attackingArmy.Count(c => c.Zone == Zone.Battlefield && Keyword(c, "Battle Maiden"));
        if (maidens > 0) GainHonor(attacker, maidens, "Plains of the Maiden");
    }
    // Dishonor/Rehonor (core rules Glossary, issue #186): "if a dishonorable personality is in an
    // army that wins or ties a battle and destroys cards in an opposing army, all of the dishonorable
    // personalities in the winner's army are rehonored and the FULL honor gain from winning is
    // negated." Called (and, for a tie, rehonoring applied) before that army's own units are
    // destroyed, in case it is also on the losing end of a tie. Returns whether any rehonoring
    // happened, so the caller knows whether to skip its own GainHonor entirely.
    private bool RehonorWinners(CardInstance[] army)
    {
        var dishonorable = army.Where(c => c.Zone == Zone.Battlefield && c.Dishonorable).ToArray();
        foreach (var card in dishonorable) Rehonor(card);
        return dishonorable.Length > 0;
    }
    // The Earth Flows (issue #264): "After you gain Honor from destroying an army in battle
    // resolution, if this Shugenja is Earth and in the winning army: Gain 2 Honor." Checked only from
    // the two outright-win branches, immediately after their own GainHonor call actually fires (never
    // the tie branch -- a tie has no "winning army" by definition). winningArmy is ResolveBattle's own
    // pre-resolution snapshot (a/d), not re-derived, matching how every other post-battle bonus here
    // already reads it.
    private void CheckEarthFlowsBonus(int winnerId, CardInstance[] winningArmy)
    {
        foreach (var spell in winningArmy.SelectMany(c => Attachments(c.Id)).Where(s => Definition(s).Effect == "earth-flows-honor" && Keyword(Card(s.AttachedTo!), "Earth")))
            GainHonor(winnerId, 2, "The Earth Flows", spell);
    }

    private void Equip(GameCommand command)
    {
        if (_state.Phase == Phase.Battle || HoldsOnlyAbilityScopedBurst(command.PlayerId)) RequireNoRestrictedBurst(command.PlayerId); // Koshin Keep (Stronghold pass, issue #486 batch 1); issue #721: an Action-Phase Equip is not one of a grant's named abilities.
        var card = Card(command.CardId); var target = Card(command.TargetId); var def = Definition(card);
        var fromDiscard = card.Zone == Zone.FateDiscard && RulebookDiscardSource("equip", command.PlayerId, command.TargetId, card); // P19 (RulebookModifiers.cs): the owner's discard pile, when a modifier allows it
        Require(card.OwnerId == command.PlayerId && (card.Zone == Zone.Hand || fromDiscard) && def.Type is CardType.Item or CardType.Follower or CardType.Spell, "Choose a supported attachment from your hand.");
        // Equip (Limited) attaches a Spell too (Mendon's ruling, issue #501): only to a Shugenja (core rules Glossary), every other restriction as for any attachment. A Spell in a battle
        // is Equip (Battle)'s, a separate player ability with its own rules; the timing grants (Foothills Keep, Glory of the Shogun) name Weapons, Items and Followers only (EquipTimingError).
        Require(def.Type != CardType.Spell || Keyword(target, "Shugenja"), "Only a Shugenja can attach a Spell.");
        // Equip (Limited) -- or, with a player-level timing grant (Foothills Keep: "Equipping Weapons is a Battle/Open action for you", TimingGrants.cs), an
        // Open equip in any player's action segment or a Battle equip: then the wielder must be a Personality at the current battlefield, not at Home.
        var equipTimingError = ResolveEquipTiming(command.PlayerId, card, out var spentTimingGrant);
        Require(equipTimingError is null, equipTimingError ?? "");
        var inBattle = _state.Phase == Phase.Battle;
        // Cultist Infiltrator (oracle-l5r-1595): "You may attach this card to a Personality you do
        // not control" -- the one printed exception to Equip's own ordinary "your Personality"
        // restriction, an Effect-keyed override rather than a new command/ability (its own OwnerId
        // stays with whoever equips it either way -- CardInstance.OwnerId is never reassigned, see
        // docs/decisions/2026-09-12-unique-singular-control-transfer.md -- so "returned to the
        // original owner's discard pile" when it and/or its host is later destroyed is already
        // correct by construction; DestroyUnits' own attachment loop below already keys off the
        // attachment's own unchanged OwnerId, never its host's).
        Require((target.OwnerId == command.PlayerId || def.Effect == "cultist-infiltrator") && (inBattle ? AtBattle(target) : target.Zone == Zone.Home) && Definition(target).Type == CardType.Personality,
            inBattle ? "Choose your Personality at the current battlefield." : def.Effect == "cultist-infiltrator" ? "Choose a Personality at home." : "Choose your Personality at home.");
        Require(def.AttachRequiresKeyword is null || KeywordOrHuman(target, def.AttachRequiresKeyword), $"Will only attach to a {def.AttachRequiresKeyword} Personality.");
        // Tempest of Flame (issue #566): "Attaches to a Fire or Void Shugenja" -- an OR of two keywords, which the single-string AttachRequiresKeyword above cannot
        // express, so it is its own separate, Effect-keyed check (the same shape as the cultist-infiltrator override just below).
        Require(def.Effect != "tempest-of-flame" || Keyword(target, "Fire") || Keyword(target, "Void"), "Will only attach to a Fire or Void Shugenja.");
        Require(def.AttachExcludesKeyword is null || !KeywordOrHuman(target, def.AttachExcludesKeyword), $"Will not attach to a {def.AttachExcludesKeyword} Personality.");
        Require(def.AttachRequiresName is null || Definition(target).Name == def.AttachRequiresName, $"Only attaches to {def.AttachRequiresName}.");
        Require(def.Type != CardType.Follower || def.HonorRequirement is null || IgnoresFollowerHonorRequirement(target) || CardPersonalHonor(target) >= def.HonorRequirement || RoninIgnoresHonorRequirement(card, command.PlayerId), "The Personality does not meet this Follower's Personal Honor Requirement.");
        Require(AttachmentAllowed(card, target), "A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
        // Unique/Singular (issue #202): Equip had no in-play-duplicate check at all before this -- only
        // QuotePurchase (Personality/Holding) did. Generalizes that same check to cover Follower/Item/
        // Spell entry too, for both keywords.
        Require(!(Keyword(card, "Unique") || Keyword(card, "Singular")) || !ControlsCopyOf(command.PlayerId, def.Name, ExperienceLevel(card)),
            $"You already control this {(Keyword(card, "Unique") ? "Unique" : "Singular")} card.");
        // The cost (Katagi's, Satoshi's Dual Warfans', Legion of the Bat's and Champion of Thunder's
        // discounts included) is EquipGoldCost in GoldQuotes.cs -- the same function that prices this
        // equip for QueryActions' quote, so the quote and the charge cannot drift (issue #303).
        Pay(command.PlayerId, card, EquipGoldCost(command.PlayerId, card, target).Cost, command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Equip, Wielder: target)); // issue #501: the wielder
        SpendPlayerTimingGrant(spentTimingGrant); // P19: a card-free once-per-turn grant is spent once the cost is paid
        _state.CurrentActionAffected = []; // Emperor Edition Personality pass (issue #477 batch 2): what Equip records (NoteAttachedFollowerFromHand) is this action's alone.
        if (!fromDiscard) { Player(command.PlayerId).Hand.Remove(card.Id); Player(command.PlayerId).AttachedFromHandThisTurn = true; NoteAttachedFromHandForGlory(command.PlayerId, card); NoteAttachedFollowerFromHand(card, target); } // P19: an attachment from a discard pile was not attached from a hand
        card.Zone = Zone.Attached; card.AttachedTo = target.Id; card.FaceUp = true;
        PromoPaymentAttached(card); // Coin Pouch (issue #667): "This card enters play bowed."
        if (inBattle) card.ProvinceIndex = target.ProvinceIndex; // the Weapon stands where its wielder does, so it is at the battlefield with him
        if (def.AttachGrantsKeyword is string granted) GrantKeyword(target, granted, card.Id, "Attachment");
        Emit("equipped", $"{def.Name} attached to {Definition(target).Name}.", command.PlayerId, card.Id);
        AfterWave740Equip(command.PlayerId, card, fromDiscard); // issue #740 (Wave740.cs): Harbingers of War's draw
        if (OpenEntryReactionWindow(command.PlayerId, card, 1 - command.PlayerId)) return;
        if (inBattle)
        {
            // A Battle action like any other: it uses an Additional Action grant of this player's when one is unscoped, and otherwise forfeits them.
            var usedGrant = ConsumeAdditionalActionGrant(command.PlayerId, null);
            if (!usedGrant) _state.AdditionalActions.RemoveAll(g => g.PlayerId == command.PlayerId && Forfeitable(g));
            var stays = usedGrant && _state.AdditionalActions.Any(g => g.PlayerId == command.PlayerId && Forfeitable(g));
            if (FinishEquipTracking(command.PlayerId, stays ? command.PlayerId : 1 - command.PlayerId)) return; // Hida Takeuchi (issue #477 batch 2).
            NextAction(command.PlayerId, stays);
            return;
        }
        var staysWithGrant = SpendEquipGrant(command, card); // issue #721 (AdditionalActionScope.cs): an Action-Phase Equip spends (or forfeits) the grants its holder has
        if (FinishEquipTracking(command.PlayerId, staysWithGrant ? command.PlayerId : 1 - command.PlayerId)) return; // Hida Takeuchi (issue #477 batch 2).
        NextAction(command.PlayerId, staysWithGrant);
    }
    private void PlayStrategy(GameCommand command)
    {
        Require(_state.Phase == Phase.Battle, "This Strategy can only be played during a battle.");
        RequireNoRestrictedBurst(command.PlayerId); // Koshin Keep (Stronghold pass, issue #486 batch 1).
        var card = Card(command.CardId); var def = Definition(card);
        Require(card.OwnerId == command.PlayerId && card.Zone == Zone.Hand && def.Type == CardType.Strategy, "Choose a supported Strategy from your hand.");
        var province = _state.CurrentBattleProvince!.Value;
        Require(Army(command.PlayerId, province).Any() || TerrainPlaysWithoutPresence(def) && Keyword(card, "Terrain"), "You need a unit at the current battlefield to take a Battle action."); // Flooded Pass (issue #492): "Even if you control no units".
        if (Keyword(card, "Terrain")) { RequireJahanAllowsTerrain(command.PlayerId); RequireRuleFlagAllowsTerrain(command.PlayerId); } // Jahan (PromoCelestialA.cs): checked before anything is paid
        // A Strategy pays its own printed Gold cost (issue #303: the same QuoteStrategyGold that
        // QueryActions ships to the client; free ones -- every training fixture -- charge nothing and,
        // as before, accept no sources).
        if (def.GoldCost > 0 && StrategyPlayCost(command.PlayerId, card).Cost is > 0 and var strategyCost) Pay(command.PlayerId, card, strategyCost, command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Play)); // issue #682: after the registered ability cost terms
        // Terrain (core rules Glossary): the printed effects only apply at the battlefield where it was
        // played -- itself just stays there as a card, not a target for a Personality-shaped effect --
        // and the default limit is at most one Terrain per battlefield until the old one is destroyed.
        if (Keyword(card, "Terrain"))
        {
            // Unstable Ground (The Shadow's Embrace pass, issue #476, batch 4): a Terrain whose own Battle ability is its play (a cost and a target) is played only through that ability.
            Require(!TerrainPlaysThroughOwnAbility(card), $"{def.Name} is played through its own Battle action.");
            Require(command.TargetId is null, "Playing a Terrain does not target another card.");
            var existingTerrain = _state.Cards.Values.FirstOrDefault(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Keyword(c, "Terrain"));
            if (existingTerrain is not null)
            {
                // Encircled Terrain / Looting the Ruins (issue #272): "Battle: Destroy a Terrain (if
                // able). Put this Terrain into play" -- unlike the ordinary "at most one Terrain per
                // battlefield" default (Vital Pathways' own "if there is no Terrain in play" plays by
                // that default unchanged), these two may replace an already-in-play Terrain outright.
                if (def.Effect is "encircled-before-battle" or "looting-return-to-hand" or "ancient-feud" or TfStrategiesAAbilities.HoldTheWallsEffect || TfStrategiesBTerrainReplaces(def) || WlStratACTerrainReplaces(def) || Wave741TerrainReplaces(def)) { DiscardTerrain(existingTerrain); if (existingTerrain.Zone != Zone.Battlefield) Player(command.PlayerId).TerrainDestroyedThisBattle = true; } // P6: the Terrain this one replaced was destroyed (Ring of Water's condition)
                // The Marshalling Fields (issue #501): once per battle its controller ignores the restriction of another Terrain being in play -- the new Terrain enters play beside it.
                else { Require(MarshallingFieldsWaiverAvailable(command.PlayerId), "This battlefield already has a Terrain in play."); SpendMarshallingFieldsWaiver(command.PlayerId, card); }
            }
            Player(command.PlayerId).Hand.Remove(card.Id);
            card.Zone = Zone.Battlefield; card.ProvinceIndex = province; card.FaceUp = true;
            Player(command.PlayerId).TerrainPlayedThisBattle = true; // Ring of Water's own recruitment condition (issue #194)
            // Fields of Mercy (oracle-l5r-2530): its own destroyed-Personality counter starts at 0
            // only from this exact moment -- never retroactively for destructions before it entered
            // play. A fresh watch per entry (DiscardTerrain removes this one's entry when it leaves).
            if (def.Effect == "fields-of-mercy") _state.FieldsOfMercyWatches.Add(new() { CardId = card.Id });
            Emit("terrainPlayed", $"{Player(command.PlayerId).Name} played {def.Name} as Terrain at this battlefield.", command.PlayerId, card.Id);
            P4TerrainEnteredPlay(card); // The Snow Has Teeth (issue #503): "After the next time this battle a Terrain enters play, destroy it."
            FireMarshallingFieldsDraw(command.PlayerId); // The Marshalling Fields (issue #501): playing a Terrain is a Terrain action that resolved.
            QueueP1TerrainPlayTraits(command.PlayerId); // Akodo Tamisu (issue #503): a Terrain action resolved.
            // Hasty Fortifications: "(including this card's)" -- putting it into play is one of the Battle actions it counts.
            if (def.Effect == "hasty-fortifications") AddHastyBonus(card);
            // The Shadow's Embrace pass (issue #476), batch 3 (ShadowsEmbraceBatch3.cs): playing a Terrain is an action on a Strategy (Forthrightness), and Crowded Streets
            // lets the Defender take an additional Battle action.
            NoteForthrightnessStrategyAction(command.PlayerId, card);
            ResolveForthrightnessWatches(command.PlayerId, card);
            var terrainGrant = CrowdedStreetsDefenderGrant(command.PlayerId, card);
            // Plain of Gold (Emperor Edition Stronghold pass, issue #486 batch 2): "Take an additional Battle action to play it" -- a grant scoped to this very card is spent by its play, and the
            // player keeps priority only while he holds another.
            var scopedGrantUsed = _state.AdditionalActions.Any(g => g.PlayerId == command.PlayerId && GrantUsableNow(g) && g.ScopeCardIds is not null && g.ScopeCardIds.Contains(card.Id)) && ConsumeAdditionalActionGrant(command.PlayerId, card.Id);
            var scopedStays = scopedGrantUsed && _state.AdditionalActions.Any(g => g.PlayerId == command.PlayerId && Forfeitable(g));
            if (OpenEntryReactionWindow(command.PlayerId, card, terrainGrant || scopedStays ? command.PlayerId : 1 - command.PlayerId)) return;
            NextAction(command.PlayerId, terrainGrant || scopedStays);
            return;
        }
        var target = Card(command.TargetId);
        Require(target.Zone == Zone.Battlefield && target.ProvinceIndex == province && Definition(target).Type == CardType.Personality, "Target a Personality at the current battlefield.");
        switch (def.Effect)
        {
            case "force-three": Require(target.OwnerId == command.PlayerId, "Training rally targets your Personality."); ApplyModifier(target, card.Id, def.Effect, "Force", 3); break;
            case "bow-small": Require(target.OwnerId != command.PlayerId && PersonalityForce(target) <= 3, "Training pressure targets an enemy Personality with 3 or less Force."); if (!StalwartNegatesBow(target, command.PlayerId)) target.Bowed = true; break;
            default: throw new RuleViolation("This card effect is not implemented.");
        }
        Player(command.PlayerId).Hand.Remove(card.Id); card.Zone = Zone.FateDiscard; card.FaceUp = true;
        Emit("strategyPlayed", $"{Player(command.PlayerId).Name} played {def.Name} targeting {Definition(target).Name}.", command.PlayerId, card.Id);
        NextAction(command.PlayerId);
    }

    // stayWithPlayer (issue #181): true only when the action just resolved was itself Additional-
    // Action-related (consumed a grant, or -- Again!/Two Hands themselves -- just created one) AND
    // the player still has an unused grant afterward. An ordinary action never sets this, even if
    // unrelated grants happen to be sitting unused for this player -- only the specific action that
    // touched them may skip ceding priority.
    private void NextAction(int playerId, bool stayWithPlayer = false)
    {
        if (Wave835FoeDuelEnded()) return; // issue #835 (Wave835.cs): the duel The Empire's Foe Exposed opened in the Events Phase is over -- the Events Phase goes on, not an Action Phase action
        if (ActionPhaseJustEnded()) return; // P13 (RuleFlags.cs): the action ended the Action Phase itself, so priority already belongs to the phase that began
        _state.ConsecutivePasses = 0; _state.PriorityPlayerId = stayWithPlayer ? playerId : 1 - playerId; EndAttackerLeadIfCeded(playerId, stayWithPlayer);
        // Glory of the Shogun (issue #492, group F): an optional draw the action owes (its attach) is asked before priority passes on.
        if (_state.PendingChoice is null && _state.PendingEventChoices.Count > 0) OpenQueuedEventChoice(stayWithPlayer ? playerId : 1 - playerId);
        if (_state.PendingChoice is null && _state.PendingEntrySearches.Count > 0) OpenQueuedEntrySearch(stayWithPlayer ? playerId : 1 - playerId); // issue #498: an entry search the action (an Equip) owes
        OpenEmperorP1Deferred(stayWithPlayer ? playerId : 1 - playerId, playerId); // Issue #503 (EmperorP1.cs): a queued trait, then a discard Hida Osote owes.
    }
    private IEnumerable<CardInstance> Army(int playerId, int province) => _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Definition(c).Type == CardType.Personality);
    // Province adjacency (core rules): a player's provinces sit in a line, 1-2-3-4. A destroyed
    // province is skipped entirely -- its former neighbors become directly adjacent to each other,
    // compounding across multiple destructions (destroy both middle provinces and the two ends become
    // adjacent to each other). Equivalent to the "provinces slide together" mental model, and derived
    // on demand from each ProvinceState's own Destroyed flag rather than tracked as separate routed
    // state -- no renumbering, no adjacency table to keep in sync as provinces are destroyed/refilled.
    // Not hardcoded to 4: the loop bound is Provinces.Count, so this also covers a match with more
    // provinces (a 5th/6th added by a game effect: fate and dynasty decks shift one card-space apart
    // and a new face-down dynasty card is placed in the new space, numbered 5/6) with no code change --
    // that's simply a new ProvinceState appended to the list; adjacency for it falls out unchanged.
    private IEnumerable<int> AdjacentProvinces(int playerId, int index)
    {
        var provinces = Player(playerId).Provinces;
        for (var i = index - 1; i >= 0; i--) if (!provinces[i].Destroyed) { yield return i; break; }
        for (var i = index + 1; i < provinces.Count; i++) if (!provinces[i].Destroyed) { yield return i; break; }
    }
    // By default a Personality carries at most one Armor and one Weapon. Kensai (printed or granted --
    // Keyword() already checks both) raises the Weapon cap to two. Kama independently raises it to two
    // without granting Kensai, but the two do not stack: a Kensai wielding a Kama is still capped at
    // two Weapons, not three.
    private bool AttachmentAllowed(CardInstance attachment, CardInstance target)
    {
        if (HostRefusesAttachment(attachment, target)) return false; // The Shadow's Embrace pass (issue #476), batch 2: "Will not attach cards" / "Will not attach Followers or Items".
        if (RestrictionRefusesAttachment(Definition(attachment), target)) return false; // P11 (CardRestrictions.cs): a stored or static "will not attach ..."
        if (WillNotAttachItems(Definition(attachment), target)) return false; // Chiisai (The Shadow's Embrace pass, issue #476 batch 1): "Will not attach Items."
        if (Keyword(attachment, "Armor")) return !Attachments(target.Id).Any(c => Keyword(c, "Armor"));
        if (Keyword(attachment, "Weapon"))
        {
            var existingWeapons = Attachments(target.Id).Where(c => Keyword(c, "Weapon")).ToArray();
            // Two-Handed overrides Kensai and Kama entirely: whenever a Two-Handed Weapon is involved
            // (already attached, or the one being attached), at most one Weapon total is ever allowed.
            if (Keyword(attachment, "Two-Handed") || existingWeapons.Any(c => Keyword(c, "Two-Handed"))) return existingWeapons.Length == 0;
            var weaponCap = Keyword(target, "Kensai") || IsKama(attachment) || existingWeapons.Any(IsKama) ? 2 : 1; // IsKama: the keyword, or the printed Kama's own Effect (issue #453 batch 5)
            return existingWeapons.Length < weaponCap;
        }
        return true;
    }
    private IEnumerable<CardInstance> Attachments(string id) => _state.Cards.Values.Where(c => c.Zone == Zone.Attached && c.AttachedTo == id);
    // "Without attachments" (core rules): a boolean condition, not a count -- Followers/Items/Spells
    // always qualify as attachments; so do tokens specifically designated as one (a Follower token, a
    // Weapon token, an Armor token), unlike an ordinary stat/status token (Fire, Poison, Madness...).
    // An attachment can never itself have attachments, so this is always false for one -- no special
    // case needed, since nothing in this engine attaches to an attachment or grants it one of these
    // token types.
    private static readonly string[] AttachmentTokenTypes = ["Follower", "Weapon", "Armor"];
    private bool HasAttachments(CardInstance card) => Attachments(card.Id).Any() || card.Tokens.Any(t => AttachmentTokenTypes.Contains(t.Type));
    // "Human" is never printed as a keyword -- a Personality/Follower without "Nonhuman" printed is
    // Human by default (core rules Glossary).
    // "Will only attach to a Crab Clan or Naga Personality" (Promotional-Emperor items, PromoEmperorItems.cs): a '|' lists alternatives, any of which satisfies the restriction.
    private bool KeywordOrHuman(CardInstance card, string keyword) => keyword.Contains('|') ? keyword.Split('|').Any(k => KeywordOrHuman(card, k)) : keyword == "Human" ? !Keyword(card, "Nonhuman") : Keyword(card, keyword);
    // An attachment's own "Will only/not attach to X" restriction and Follower Honor Requirement,
    // checked together wherever a legal target is decided (Equip's own Require and the offered-action
    // legal-targets filter both call this, so they can never disagree).
    private bool AttachmentLegalTarget(CardDefinition attachmentDef, CardInstance target) =>
        !RestrictionRefusesAttachment(attachmentDef, target) && // P11 (CardRestrictions.cs)
        !WillNotAttachItems(attachmentDef, target) && // Chiisai (The Shadow's Embrace pass, issue #476 batch 1): "Will not attach Items."
        (attachmentDef.AttachRequiresKeyword is null || KeywordOrHuman(target, attachmentDef.AttachRequiresKeyword)) &&
        (attachmentDef.AttachExcludesKeyword is null || !KeywordOrHuman(target, attachmentDef.AttachExcludesKeyword)) &&
        (attachmentDef.AttachRequiresName is null || Definition(target).Name == attachmentDef.AttachRequiresName) &&
        (attachmentDef.Type != CardType.Follower || attachmentDef.HonorRequirement is null || IgnoresFollowerHonorRequirement(target) || CardPersonalHonor(target) >= attachmentDef.HonorRequirement || RoninFollowerIgnoresHonorRequirement(attachmentDef, target)); // Keep of the Dead (Stronghold pass, issue #486 batch 1): "You ignore Honor Requirements on your Followers."
    // "Current" Personal Honor (core rules Glossary): a Dishonorable Personality's is fixed at 0 until
    // Rehonor restores them -- fixed at 0, not merely reduced, so a persistent modifier (e.g. a token)
    // is not removed by this and resumes applying once honorable again. The handful of rules that
    // deliberately key off the *printed* value regardless of current status (Dishonorable death's
    // Honor penalty, Proclaim's grant) intentionally do not use this helper.
    private int CardPersonalHonor(CardInstance card) => Wave734PersonalHonorCap(card) is int cap ? Math.Min(CardPersonalHonorUncapped(card), cap) : CardPersonalHonorUncapped(card); // Armor of the Mad, Shosuro Chizuru (issue #734)
    private int CardPersonalHonorUncapped(CardInstance card) => card.Dishonorable && !DishonorablePersonalHonorOverridden(card) ? 0 : Definition(card).PersonalHonor + StatModifier(card, "PersonalHonor")
        + StaticPersonalHonorBonus(card); // Akodo Hotaka (issue #453, batch 2): "+1PH if any other player is Dragon Clan".
    // Utaku Chikako's own passive (issue #186): "Your dishonorable Battle Maidens do not have a
    // maximum Personal Honor of 0" -- a Personality's own passive (not a Stronghold's), so checked
    // generically against any in-play Personality the SAME owner controls carrying this Effect,
    // rather than fixed to one specific card.
    // Utaku Ji-Yun - exp (issue #734): "Your dishonorable Paragons do not have a maximum Personal Honor of 0 from the rulebook."
    private bool DishonorablePersonalHonorOverridden(CardInstance card) =>
        Keyword(card, "Battle Maiden") && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Effect == "battle-maiden-dishonor-immunity")
        || Wave734ParagonsOverride(card);
    // "force-with-spell" (Asako Ume), "force-if-enemy-dishonorable" (Bayushi Kahoku) and
    // "force-max-dead-pile" (Moto Shunsuke) are always-on passive bonuses, unlike the attacking-only
    // attacking-two/attacking-four Effects above.
    // A Force cap (issue #768, ForceCaps.cs) lowers the total below; it is applied here, once, so every reader of a Personality's Force sees it.
    private int PersonalityForce(CardInstance card) => ApplyForceCaps(card, PersonalityForceUncapped(card));
    private int PersonalityForceUncapped(CardInstance card) => Math.Max(0, PersonalityForceRaw(card));
    // The Force total before the floor at 0 and before any cap: what a Set measures its bonus or penalty from (core rules Glossary, "Set"), so the stated value is also right once a cap leaves or a penalty ends.
    private int PersonalityForceRaw(CardInstance card) => Definition(card).Force + StatModifier(card, "Force") + TerrainForceAdjustment(card) + RegisteredPersonalityForce(card) + (IsAttacking(card) ? (Definition(card).Effect == "attacking-two" ? 2 : Definition(card).Effect == "attacking-four" ? 4 :
        // Matsu Kaido (oracle-l5r-4934, issue #299): "Kaido has +1F while attacking" -- the plain +1
        // counterpart to attacking-two/attacking-four (Matsu Yoshito's own conditional variant below
        // starts from the same +1 base).
        Definition(card).Effect == "attacking-one" ? 1 :
        // Matsu Yoshito (issue #198): "+1F while attacking, or +2F if the Defender is Dragon Clan" --
        // the Defender is always the other player while this card is attacking.
        Definition(card).Effect == "attacking-vs-dragon-bonus" ? (Player(1 - card.OwnerId).Clan == "Dragon" ? 2 : 1) : 0) - (HasHalls(card.OwnerId) && Keyword(card, "Ancestor") ? 1 : 0) : 0)
        // Pride of the Hand (issue #198): "+1F while you control a Terrain at its battlefield" --
        // unlike the attacking-only cases above, this applies whenever the card is at a battlefield
        // (attacking or defending) that also has one of its own controller's Terrains in play.
        + (Definition(card).Effect == "force-with-own-terrain-at-battlefield" && AtBattle(card) && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == card.ProvinceIndex && Keyword(c, "Terrain")) ? 1 : 0)
        // Rokku - experienced (oracle-l5r-6376, Embers of War pass, issue #460 batch 2): "Rokku has +1F while a Lion Clan player controls
        // him" -- unconditional on attacking or location, gated only by his own controller's Stronghold Clan (RokkuClanForceBonus).
        + RokkuClanForceBonus(card)
        // An Item's printed "+F" box (and any conditional bonus it grants the wielder, e.g. Kensai's
        // Blade's "+1F to a Personality with Kensai") always conveys to the wielder, whether the Item
        // is bowed or not -- unlike a Follower's own Force, which stays with the Follower itself (see
        // FollowerForce) and only joins the unit total in BattleForce when unbowed or Elite.
        + Attachments(card.Id).Where(c => Definition(c).Type == CardType.Item).Sum(c => Definition(c).Force + ItemForceToWielder(c, card))
        // Scouring Flood (the first Spell wired with a static Force bonus): unlike an Item, a Spell
        // never prints its own numeric Force stat box -- "This Personality has +2F" is purely a text
        // effect, so it is Effect-keyed on the ATTACHED SPELL itself (the same shape
        // ItemForceToWielder already uses for an Item's own conditional bonus), always applying --
        // bowed or not, every phase -- since no published Spell needs a conditional version yet.
        + StaticSpells(card).Sum(c => Definition(c).Effect == "scouring-flood-force" || Definition(c).Effect == "wave-persists-force" && Keyword(card, "Water") ? 2 : 0)
        // The Swell of Strength (oracle-l5r-8366, issue #460 batch 5): "This Shugenja has +2F" -- Scouring Flood's own Spell-Effect-keyed static bonus shape; always applies to whoever wields it (any Shugenja may equip it; only its Focus Effect is limited to Water Shugenja).
        + StaticSpells(card).Count(c => Definition(c).Effect == "swell-of-strength-force") * 2
        // Strength of the Tsunami: "This Shugenja has +6F" -- only the Shugenja it is attached to gets it.
        + StaticSpells(card).Where(c => Definition(c).Effect == "tsunami-force" && Keyword(card, "Shugenja")).Sum(_ => 6)
        + (Definition(card).Effect == "force-with-spell" && Attachments(card.Id).Any(c => Definition(c).Type == CardType.Spell) ? 1 : 0)
        + (Definition(card).Effect == "force-with-spell-2" && Attachments(card.Id).Any(c => Definition(c).Type == CardType.Spell) ? 2 : 0)
        // Ember's Final Fire: "While this Shugenja is Fire, he has +1F". Stone-Hewed Shrine: "Defending Earth Personalities have +1F while at this province's battlefield."
        + StaticSpells(card).Count(c => Definition(c).Effect == "embers-final-fire-force" && Keyword(card, "Fire"))
        + 2 * StaticSpells(card).Count(c => Definition(c).Effect == "death-defeated-force")
        + (card.Zone == Zone.Battlefield && Keyword(card, "Earth") && IsDefendingUnit(card) && _state.Cards.Values.Any(r => r.Zone == Zone.Region && r.ProvinceIndex == (card.ProvinceIndex is int shrineBattlefield ? BattlefieldProvinceIndex(shrineBattlefield) : null) && Definition(r).Effect == "stone-hewed-shrine-force") ? 1 : 0)
        + (Definition(card).Effect == "force-if-enemy-dishonorable" && _state.Cards.Values.Any(c => c.OwnerId != card.OwnerId && Definition(c).Type == CardType.Personality && InPlay(c) && c.Dishonorable) ? 2 : 0)
        // "The highest number of dead Personalities in any one discard pile" -- the greatest per-player
        // count of Zone.Dead Personalities, i.e. the largest single pile, not the combined total.
        + (Definition(card).Effect == "force-max-dead-pile" ? Math.Max(DeadCount(0), DeadCount(1)) : 0)
        // Dragon Clan review (issue #207): Tamori Kazushige / Togashi Korimi -- "+2F while defending",
        // the same shape FollowerForce's own force-while-defending already covers, just for a
        // Personality directly rather than an attached Follower.
        + (Definition(card).Effect == "force-while-defending-personality" && IsDefendingUnit(card) ? 2 : 0)
        // Tamori Yayu (issue #216): "+2F ... while it is not your turn" -- the Cavalry-keyword half
        // of the same printed sentence is a live conditional check in Keyword() itself, not a
        // GrantedKeyword (which would be a durable, not continuously re-evaluated, grant).
        + (Definition(card).Effect == "cavalry-force-while-not-your-turn" && card.OwnerId != _state.ActivePlayerId ? 2 : 0)
        // Kitsuki Miroken: "+2F while opposing a dishonorable or Gaijin card" -- checked against the
        // whole opposing army at the current battlefield, not a specific targeted card.
        + (Definition(card).Effect == "force-vs-dishonorable-or-gaijin-opposed" && AtBattle(card) && Army(1 - card.OwnerId, card.ProvinceIndex ?? -1).Any(c => c.Dishonorable || Keyword(c, "Gaijin")) ? 2 : 0)
        // Akodo Uehara (oracle-l5r-305, Embers of War pass, issue #460): "Uehara has +1F while he opposes a Phoenix Clan Personality" -- Kitsuki Miroken's own
        // opposing-army shape, just a Clan check (HasClanAffiliation) in place of dishonorable/Gaijin.
        + (Definition(card).Effect == "uehara-force-vs-phoenix" && AtBattle(card) && Army(1 - card.OwnerId, card.ProvinceIndex ?? -1).Any(c => Definition(c).Type == CardType.Personality && HasClanAffiliation(c, "Phoenix")) ? 1 : 0)
        // Tamori Muzu: "Your other Earth Personalities have +1F while defending" -- army-scoped (not
        // unit-scoped, since Muzu's own bonus reaches every OTHER Earth-keyword card he controls at
        // the same battlefield, not just his own unit) and keyword-based, following
        // ArmyBattleMaidenBoost's own established shape, plus a defending condition on the RECEIVING
        // card rather than the granter.
        + (IsDefendingUnit(card) && Keyword(card, "Earth") ? _state.Cards.Values.Count(c => c.Id != card.Id && c.OwnerId == card.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == card.ProvinceIndex && Definition(c).Effect == "boost-earth-personalities-while-defending" && Keyword(c, "Earth")) : 0)
        // Yoritomo Minori (oracle-l5r-9605, issue #301): "+1F for each other Magistrate in her army" --
        // a self-received, count-based bonus (unlike ArmyBattleMaidenBoost below, which a Battle
        // Maiden receives FROM other cards' own Effect), so it is Effect-keyed on the receiving card
        // itself, with no attacking/defending gate at all.
        + (Definition(card).Effect == "force-per-other-magistrate-in-army" ? OtherMagistratesInArmy(card) : 0)
        // Immolation (oracle-l5r-10127, issue #453 batch 5): "This Shugenja has +3F, or +4F while he is Fire" -- Effect-keyed on the attached Spell, like tsunami-force above.
        + StaticSpells(card).Where(c => Definition(c).Effect == "immolation-force" && Keyword(card, "Shugenja")).Sum(_ => Keyword(card, "Fire") ? 4 : 3)
        + RemAItemForceModifiers(card) // A Pure Stroke (Emperor Edition remaining-cards pass, group A, issue #492): the modifiers raised on the wielder's Items.
        + AshigaruBattleBoost(card) + ArmyBattleMaidenBoost(card);
    // "Her army" (Yoritomo Minori): every Personality the same owner has at the same battlefield --
    // Army(), the engine's own definition -- so a card not at a battlefield has no army and no bonus.
    // "Magistrate" is a Keyword() check (printed or granted -- Justice of the Crane grants it), and the
    // card itself is excluded ("other"). Bowed ones still count: no "unbowed" is printed.
    private int OtherMagistratesInArmy(CardInstance card) =>
        card.Zone == Zone.Battlefield && card.ProvinceIndex is int province
            ? Army(card.OwnerId, province).Count(c => c.Id != card.Id && Keyword(c, "Magistrate"))
            : 0;
    // "Dead" (core rules Glossary): a status a card carries once it enters Zone.Dead, distinct from
    // merely being discarded (Zone.DynastyDiscard/FateDiscard) -- both live in the same client-facing
    // PlayerView.Discard array (see GetView), told apart there by each CardView's own Zone. This is
    // the single shared source of truth for "how many of this player's own Personalities are dead
    // right now" -- Moto Shunsuke's own force-max-dead-pile Effect above, PlayerView.DeadCount below,
    // and any future card keyed off the same count (a real, recurring Unicorn Clan theme) all read
    // through here, rather than each re-deriving its own filter over _state.Cards.
    private int DeadCount(int playerId) => _state.Cards.Values.Count(c => c.Zone == Zone.Dead && c.OwnerId == playerId && Definition(c).Type == CardType.Personality);
    // An Item's own conditional Force bonus to its wielder, keyed by the Item's Effect string --
    // distinct from a Follower's self-referential conditional Force (FollowerForce below), since an
    // Item's bonus always targets the Personality, never itself.
    private int ItemForceToWielder(CardInstance item, CardInstance wielder) => Definition(item).Effect switch
    {
        "force-to-kensai-wielder" => Keyword(wielder, "Kensai") ? 1 : 0,
        // Celestial Sword of the Mantis / of the Scorpion - exp (issue #453 batch 5): "This card has +1F for each <Clan> Personality you control" -- the wielder's controller's, the wielder himself included.
        "celestial-sword-force-mantis" => ControlledClanPersonalities(wielder.OwnerId, "Mantis"),
        "celestial-sword-force-scorpion" => ControlledClanPersonalities(wielder.OwnerId, "Scorpion"),
        // Embers of War pass, batch 6 (issue #460): the Lion, Unicorn and (Spider-Clan) Ancestral Sword of Hantei siblings share the same "+1F for
        // each <Clan> Personality you control" shape as the Mantis/Scorpion swords above.
        "celestial-sword-force-lion" => ControlledClanPersonalities(wielder.OwnerId, "Lion"),
        "celestial-sword-force-unicorn" => ControlledClanPersonalities(wielder.OwnerId, "Unicorn"),
        "celestial-sword-force-spider" => ControlledClanPersonalities(wielder.OwnerId, "Spider"),
        // The Shadow's Embrace pass (issue #476), batch 5: the Crab, Crane and Phoenix swords, the same shape.
        "celestial-sword-force-crab" => ControlledClanPersonalities(wielder.OwnerId, "Crab"),
        "celestial-sword-force-crane" => ControlledClanPersonalities(wielder.OwnerId, "Crane"),
        "celestial-sword-force-phoenix" => ControlledClanPersonalities(wielder.OwnerId, "Phoenix"),
        // Emperor Edition follow-ups (issue #498): Celestial Sword of the Dragon - exp, the same shape.
        DragonSwordEffect => ControlledClanPersonalities(wielder.OwnerId, "Dragon"),
        _ => 0
    };
    // A Follower's own standalone Force, including any static conditional bonus it grants itself
    // (e.g. "+2F while defending") -- distinct from an Item's, which always conveys to the wielder
    // instead (see PersonalityForce/ItemForceToWielder). Feeds both CardForce (targeting, always
    // visible regardless of bow state) and BattleForce (unit total, gated on unbowed-or-Elite).
    private int FollowerForce(CardInstance card) => Math.Max(0, FollowerForceRaw(card));
    private int FollowerForceRaw(CardInstance card) => Definition(card).Force + StatModifier(card, "Force") + P5Force(card)/* issue #503 */+ PromoItemsFollowerForce(card) /* Battle Standard of the Naga */ + WlItemsMiscFollowerForce(card) /* A Fever in the Blood (issue #674) */ + TerrainForceAdjustment(card) + GinasFoxForce(card) + PromoCelestialCForce(card) + ForgottenLegacyBatch5Force(card) + ShadowsEmbraceBatch5Force(card) + RegisteredFollowerForce(card) + (Definition(card).Effect switch
    {
        "force-while-defending" => IsDefendingUnit(card) ? 2 : 0,
        // Home Guard (oracle-l5r-3413, issue #435): "This card has +3F while defending" -- the same
        // shape as force-while-defending above, at 3 instead of 2.
        "force-while-defending-3" => IsDefendingUnit(card) ? 3 : 0,
        // Disciples of Ganesh: "+2F while defending" -- shares FollowerForce's existing case, plus a
        // separate destruction-triggered Honor gain (see DestroyedDuringBattleBonus, keyed by the same
        // Effect string) -- one card, two engine hooks, since Effect is a single field per card.
        "force-while-defending-honor-if-destroyed" => IsDefendingUnit(card) ? 2 : 0,
        // Family Sensei / Merchant Guard (core rules): self-referential, even though the condition
        // counts/checks other cards -- the same shape PersonalityForce's conditional bonuses already
        // cover, not the "boost OTHER cards" aggregate primitives below.
        "force-with-dojo" => ControlsKeyword(card.OwnerId, "Dojo") ? 1 : 0,
        "force-with-market" => ControlsKeyword(card.OwnerId, "Market") ? 1 : 0,
        // Ashigaru Guards: "+1F for every two Farms you control" -- a controlled-card COUNT, still
        // self-referential (the bonus lands on this card), same shape as PersonalityForce's
        // force-max-dead-pile.
        "force-per-two-farms" => _state.Cards.Values.Count(c => c.OwnerId == card.OwnerId && InPlay(c) && Keyword(c, "Farm")) / 2,
        // Brothers of the Great Lake: "+1F for each OTHER [same name] in this army" -- counts sibling
        // copies at the same battlefield, excluding itself. A Follower's own Zone is Attached, not
        // Battlefield, so this resolves through its parent Personality first (same as ArmyBattleMaidenBoost).
        "force-per-other-same-name-in-army" => BattlefieldParent(card) is { } parent
            ? _state.Cards.Values.Count(c => c.Id != card.Id && Definition(c).Name == Definition(card).Name
                && BattlefieldParent(c) is { } otherParent && otherParent.OwnerId == parent.OwnerId && otherParent.ProvinceIndex == parent.ProvinceIndex)
            : 0,
        // Ninja Guards: "Force bonus equal to its Personality's printed Force" -- reads the WIELDER's
        // own printed stat, the opposite data-flow direction from ItemForceToWielder (which conveys an
        // Item's bonus TO the wielder); here the Follower's own Force is keyed off the wielder instead.
        "force-equals-wielder-printed-force" => card.AttachedTo is null ? 0 : Definition(Card(card.AttachedTo)).Force,
        _ => 0
    }) + UnitFollowerBoost(card) + AkioFollowerBoost(card) + AshigaruBattleBoost(card) + ArmyBattleMaidenBoost(card) + ZanshoFollowerBoost(card)
        + XiaoFollowerBoost(card); // Moto Xiao (Forgotten Legacy pass, issue #467 batch 2): "Followers in Xiao's army have +1F."
    // "Boost other cards in the unit" primitive (Senior Infantry Legion's shape): distinct from the
    // self-referential conditional bonuses above, this is a card that grants a static Force bonus to
    // every OTHER Follower attached to the same Personality (a unit is a Personality and all of its
    // attachments -- core rules Glossary). Excludes itself, so two copies of the same booster in one
    // unit grant each other the bonus but never themselves. Always-on and static, so it belongs here
    // in FollowerForce rather than behind an activated ability.
    private int UnitFollowerBoost(CardInstance follower) =>
        follower.AttachedTo is null ? 0 : Attachments(follower.AttachedTo).Where(c => c.Id != follower.Id).Sum(c => Definition(c).Effect switch
        {
            "boost-followers-in-unit" => 1,
            // Aulus's shape: keyword-based rather than type-based, still unit-scoped -- "Legionnaires
            // in this unit have +1F" reuses the exact same unit-scoping as Senior Infantry Legion, just
            // gated by a keyword instead of the Follower type (already implicit, since UnitFollowerBoost
            // only ever feeds FollowerForce).
            "boost-legionnaires-in-unit" => Keyword(follower, "Legionnaire") ? 1 : 0,
            _ => 0
        });
    // Aggregate/group Force effects, second shape (Ashigaru Elite): unlike UnitFollowerBoost's static
    // per-unit bonus, this is an activated, stacking, per-battle bonus that reaches every Ashigaru-
    // keyword card the performer controls at the CURRENT battlefield -- not just its own unit. Read
    // from MatchState.AshigaruBoost (incremented once per use, reset to zero whenever the battle
    // resolves -- see FinishBattleResolution), and gated by AtBattle so it never leaks to a card
    // sitting at a different or no-longer-current battlefield.
    private int AshigaruBattleBoost(CardInstance card) => AtBattle(card) && Keyword(card, "Ashigaru") ? _state.AshigaruBoost[card.OwnerId] : 0;
    // Aggregate/group Force effects, third shape (Utaku Elite Guard): like UnitFollowerBoost, a static
    // "boost others" bonus, but keyword-based (Battle Maiden) rather than type-based (Follower), and
    // army-scoped -- every OTHER Battle-Maiden card the same owner controls in the same battlefield
    // army (same province, both in Zone.Battlefield, following an attachment through to its parent
    // Personality) -- rather than confined to one unit. Excludes itself, matching Senior Infantry
    // Legion's "not to itself" rule: two Utaku Elite Guards in one army grant each other, not themselves.
    // The real card's own text ("...have +1F while attacking") additionally gates on IsAttacking --
    // reusing the same helper PersonalityForce's attacking-two/attacking-four Effects already use.
    private int ArmyBattleMaidenBoost(CardInstance card)
    {
        if (!Keyword(card, "Battle Maiden")) return 0;
        var parent = BattlefieldParent(card);
        if (parent is null || !IsAttacking(parent)) return 0;
        return _state.Cards.Values.Count(c => c.Id != card.Id && c.OwnerId == parent.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == parent.ProvinceIndex
            && Definition(c).Type == CardType.Personality && Definition(c).Effect == "boost-battle-maidens-in-army");
    }
    // Shared by both army-scoped aggregate effects above: a card's own Personality "unit" at the
    // battlefield -- itself, if it's the Personality directly on the battlefield, or (for an attached
    // Follower, whose own Zone is Attached, never Battlefield) its wielder, if THAT is on the
    // battlefield. Null when neither the card nor its wielder is actually at a battlefield.
    private CardInstance? BattlefieldParent(CardInstance card)
    {
        var parent = card.Zone == Zone.Attached ? Card(card.AttachedTo!) : card;
        return parent.Zone == Zone.Battlefield ? parent : null;
    }
    // A bowed ordinary Personality suppresses only its OWN Force at resolution (issue #216
    // correctness fix) -- a Follower that is itself unbowed still contributes its own Force
    // regardless of its host's bowed state; only a bowed, non-Elite FOLLOWER suppresses itself.
    // Preserve the explicit Elite-Personality exception accepted in #19/#17.
    // An Elite Follower cannot bypass its non-Elite Personality's bowed state -- the Personality's
    // OWN suppression is independent, but a Follower still checks its OWN bowed status too.
    // EliteEffect: true for a card with the actual printed/granted Elite keyword, but also for a
    // wielder carrying an attachment that grants only Elite's effect without the keyword itself
    // (Seppun Heavy Elite's shape -- see AttachGrantsEliteEffect).
    private bool EliteEffect(CardInstance card) => Keyword(card, "Elite") || Attachments(card.Id).Any(a => Definition(a).AttachGrantsEliteEffect
        || Definition(a).AttachGrantsEliteEffectWhileWielderHas is string wielderKeyword && Keyword(card, wielderKeyword) && !(Definition(a).Type == CardType.Spell && SpellStaticSilenced(a)))
        // Incite the Phantoms (oracle-l5r-3732, issue #460 batch 5): "While this Shugenja is Earth, your Spirits contribute Force to their
        // army even while bowed" -- broader than Bo of Water's own single-card AttachGrantsEliteEffectWhileWielderHas: every Spirit the
        // controller owns, not only the Spell's own wielder (EmbersOfWarBatch5.cs, InciteThePhantomsActive).
        || (Keyword(card, "Spirit") && InciteThePhantomsActive(card.OwnerId));
    // Deep Roots (The Shadow's Embrace pass, issue #476 batch 3): "He contributes Force even while bowed" (DeepRooted, ShadowsEmbraceBatch3.cs).
    private int PersonalityBattleContribution(CardInstance card) => card.Bowed && !EliteEffect(card) && !DeepRooted(card) || ForceContributionNegated(card) ? 0 : PersonalityForce(card);
    private int FollowerBattleContribution(CardInstance card) => Attachments(card.Id)
        .Where(c => (!c.Bowed || EliteEffect(c)) && !ForceContributionNegated(c) && Definition(c).Type == CardType.Follower)
        .Sum(FollowerForce);
    private int BattleForce(CardInstance card) => PersonalityBattleContribution(card) + FollowerBattleContribution(card);
    // Force redirection (issue #216, Togashi Noboru): each army's own total, honoring any active
    // ForceRedirect -- a redirected card's own Personality contribution (never its Followers', which
    // always stay with their own controller) counts toward the BENEFICIARY's total instead of its
    // own controller's, but only while the beneficiary still has at least one unit in THEIR OWN
    // current army (re-checked here, at actual resolution time, not when the redirect was granted).
    private (int Attacker, int Defender) ArmyForces(CardInstance[] a, CardInstance[] d, int attacker, int defender)
    {
        int af = 0, df = 0;
        // Hida Horu (Emperor Edition Personality pass, issue #477 batch 2): he contributes no Force while his army has two or more units (his Followers still do).
        int Contribution(CardInstance c, CardInstance[] army) => HoruContributesNothing(c, army) || Wave734ContributesNothing(c) ? 0 : StatTermContribution(c, PersonalityBattleContribution(c)); // StatTermContribution: "contributes his Force twice" (StatTerms.cs, issue #672)
        foreach (var c in a) { if (RedirectsTo(c, attacker, defender, a, d) == defender) df += Contribution(c, a); else af += Contribution(c, a); af += FollowerBattleContribution(c); }
        foreach (var c in d) { if (RedirectsTo(c, attacker, defender, a, d) == attacker) af += Contribution(c, d); else df += Contribution(c, d); df += FollowerBattleContribution(c); }
        // Seeds of Decay pass (issue #453), batch 1: Megumi's Guidance's "+2 total Force, or +3 if you are the Defender, while your army is opposed" (SeedsOfDecayBatch1.cs).
        af += CelestialArmyBonus(attacker, a, d, defending: false); df += CelestialArmyBonus(defender, d, a, defending: true);
        // Emperor Edition Personality pass (issue #477), batch 5: Isawa Norimichi's "+5 total Force while there are any units in your army".
        af += ArmyForceBonusFor(attacker, a); df += ArmyForceBonusFor(defender, d);
        // Static army totals (EngineHooks.ArmyForce, issue #672): Bayushi Manami - exp's "During battle resolution, Manami's army has +2F" (StatTerms.cs).
        af += RegisteredArmyForce(attacker, a, d, defending: false); df += RegisteredArmyForce(defender, d, a, defending: true);
        return (af, df);
    }
    private int? RedirectsTo(CardInstance card, int attacker, int defender, CardInstance[] a, CardInstance[] d)
    {
        var redirect = _state.ForceRedirects.FirstOrDefault(r => r.TargetId == card.Id);
        if (redirect is null) return null;
        var beneficiaryArmy = redirect.ToPlayerId == attacker ? a : d;
        return beneficiaryArmy.Length > 0 ? redirect.ToPlayerId : null;
    }
    // "Dead / Destroy" (core rules Glossary): entering the Dead zone, distinct from merely being
    // discarded. A Personality who is still Dishonorable at this moment costs its controller Honor
    // equal to his base Personal Honor -- Seppuku (and any future rehonor-before-death effect) spares
    // this by clearing Dishonorable first (see Rehonor). This is the one shared entry point every
    // path to "becoming dead" -- combat destruction or otherwise -- must go through.
    // "When a card is destroyed it is placed in its respective discard pile. If it is a Personality,
    // it becomes dead" (core rules Glossary) -- only a Personality actually gets the separate "Dead"
    // status (Zone.Dead); every other type destroyed this way goes straight to its own deck's
    // discard pile instead, the same zones DestroyAttachment/DiscardTerrain already use for an
    // attached/Terrain card. DestroyUnits is the one caller that can reach either branch (a
    // battle-army Personality, but also Furugaro/Unseen Assassins/Scholarly Hermit's own Follower
    // self-destroys); MakeDead's own callers are always Personality-gated already, so they only ever
    // take the Dead branch.
    private void MarkDead(CardInstance card)
    {
        // Overconfident (issue #223): "leaves play" specifically means dying WHILE actually in play --
        // MakeDead's own path (a card already sitting in a discard pile, not in play, that later
        // becomes dead) does not qualify, matching the user's own "discarded from provinces/deck does
        // not trigger this" carve-out. Checked before the Zone changes, using the card's own status
        // right up to the moment it actually died.
        if (InPlay(card)) QueueOverconfidentDraw(card);
        // Fair Warning (issue #453, batch 2): "after the next time this turn he ... is destroyed" -- a destruction while in play.
        if (InPlay(card)) FireDestroyedWatches(card);
        card.Zone = Definition(card).Type == CardType.Personality ? Zone.Dead
            : Definition(card).Type is CardType.Follower or CardType.Item or CardType.Spell or CardType.Strategy or CardType.Ring ? Zone.FateDiscard
            : Zone.DynastyDiscard;
        card.FaceUp = true; card.Tokens.Clear();
        if (Definition(card).Type == CardType.Personality && card.Dishonorable) GainHonor(card.OwnerId, -Definition(card).PersonalHonor, DishonorableDeathReason, card);
        // Yasuki Tsujiken (oracle-l5r-9469): "Until Tsujiken leaves play..." -- his own watches end
        // the moment HE dies (a no-op RemoveAll for every other Personality, since TsujikenId only
        // ever matches marks placed by this exact CardInstance).
        if (Definition(card).Type == CardType.Personality) _state.TsujikenWatches.RemoveAll(w => w.TsujikenId == card.Id);
    }
    // Overconfident (core rules keyword, issue #223): "each other player may draw a card after an
    // Overconfident card enters or leaves play" -- both triggers share this one queuing helper (the
    // entering-play half is called directly from Purchase/RecruitReserve, the leaves-play half from
    // MarkDead above) and the one draining pair below.
    private void QueueOverconfidentDraw(CardInstance card)
    {
        if (Definition(card).Type == CardType.Personality && Keyword(card, "Overconfident")) _state.PendingOverconfidentDraws.Add(1 - card.OwnerId);
    }
    // Drains one queued offer at a time -- called at every point the engine is about to hand priority
    // back normally, whether because no other reaction window ever opened here, or because one that
    // did just closed (see PassReaction). closesTo is where control resumes once the WHOLE queue (not
    // just this one offer) is empty.
    // Times of Strife (issue #259): drains PendingForcedDiscards FIRST, before the
    // Overconfident queue below -- both are simple "queue of players owed a deferred window"
    // drains sharing this one function rather than needing their own copy of every call site (there
    // are around a dozen), so a forced discard (Times of Strife, Daigotsu Bofana - exp's entry) composes for
    // free with every existing point that already calls this to hand priority back normally. A player with
    // an empty hand owes nothing.
    private bool OpenOverconfidentDrawWindow(int closesTo)
    {
        ApplyWlPersGates1DeferredLosses(); // Gojiro, Hajime (issue #679, WlPersGates1.cs): the Honor loss listed below an Invest, once its entry window has closed.
        // Emperor Edition remaining-cards pass (issue #492), group F: an optional draw owed by an Event (Boastful Proclamation, Glory of the Shogun), asked here like the forced discards below.
        if (OpenQueuedEventChoice(closesTo)) return true;
        if (OpenQueuedFactWindow(closesTo)) return true; // P6 (AfterFacts.cs): the facts that happened since the last safe point
        if (OpenQueuedEntrySearch(closesTo)) return true; // Emperor Edition follow-ups (issue #498): an "After this card enters play: search ..." trait, once the entry windows are done.
        if (_state.PendingForcedDiscards.Count > 0)
        {
            var owed = _state.PendingForcedDiscards[0];
            _state.PendingForcedDiscards.RemoveAt(0);
            if (Player(owed.PlayerId).Hand.Count == 0) return OpenOverconfidentDrawWindow(closesTo);
            _state.PendingChoice = new("forcedDiscard", owed.PlayerId, null, $"{owed.Reason}: discard a card.", closesTo);
            _state.PriorityPlayerId = owed.PlayerId;
            Emit("forcedDiscardOffered", $"{Player(owed.PlayerId).Name} must discard a card ({owed.Reason}).", owed.PlayerId);
            return true;
        }
        // Overconfident is not a Reaction (Mendon's R4 ruling on Cherry Blossom Festival: "Overconfident and traits are not reactions (despite triggering like them)"): its draw is offered whatever an Event bars.
        if (_state.PendingOverconfidentDraws.Count == 0) return false;
        var playerId = _state.PendingOverconfidentDraws[0];
        _state.PendingOverconfidentDraws.RemoveAt(0);
        _state.PendingChoice = new("overconfidentDraw", playerId, null, "Overconfident: you may draw a card.", closesTo);
        _state.PriorityPlayerId = playerId;
        Emit("overconfidentDrawOffered", $"{Player(playerId).Name} may draw a card (Overconfident).", playerId);
        return true;
    }
    private void ResolveOverconfidentDraw(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "overconfidentDraw" && command.PlayerId == pending.PlayerId && command.Choice is "draw" or "decline", "Choose whether to draw a card.");
        if (command.Choice == "draw") Draw(Player(command.PlayerId));
        _state.PendingChoice = null;
        ResumeAfterDeferredWindow(pending.ActingPlayerId!.Value);
    }
    // The one answer to a forced discard: the discarding player names a card of their own hand (private to them; the
    // discard pile is public). A discard opened when an action was announced (Daigotsu Bofana) then lets that action
    // carry on exactly where it stopped; a queued one hands priority back like the other deferred windows.
    private void ResolveForcedDiscard(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "forcedDiscard" && command.PlayerId == pending.PlayerId && command.TargetId is not null && Player(command.PlayerId).Hand.Contains(command.TargetId)
            && !(_state.ForcedDiscardContinuation is { } held && CommandNamesCard(held, command.TargetId)), "Choose a card from your hand to discard.");
        var player = Player(command.PlayerId);
        player.Hand.Remove(command.TargetId!); var card = Card(command.TargetId); card.Zone = Zone.FateDiscard; card.FaceUp = true;
        Emit("handDiscard", $"{player.Name} discarded {Definition(card).Name} ({pending.Prompt.Replace(": discard a card.", "")}).", player.Id, card.Id);
        _state.PendingChoice = null;
        if (_state.ForcedDiscardContinuation is { } announced)
        {
            var resume = _state.ForcedDiscardResume; _state.ForcedDiscardContinuation = null; _state.ForcedDiscardResume = null;
            if (resume == "action") ResolveDeferredActionAfterItsWindows(announced);
            else if (resume == "attack") ResolveAttack();
            else { ContinueSuspendedReaction(announced); AnnounceAbility(announced); }
            return;
        }
        ResumeAfterDeferredWindow(pending.ActingPlayerId!.Value);
    }
    // A card the announced command itself uses (its source, target, performer or chosen cards): never one of the hand
    // cards an announced action could be made to discard out from under itself.
    private static bool CommandNamesCard(GameCommand command, string cardId) =>
        command.CardId == cardId || command.TargetId == cardId || command.PerformerId == cardId || command.CardIds?.Contains(cardId) == true;
    // Shared tail for both deferred-window resolvers above: try the next queued window, then the
    // FinishBattleResolution special case, then hand priority back normally. Factored out once a
    // second resolver (ResolveTimesOfStrifeDiscard) needed the identical sequence ResolveOverconfidentDraw
    // already had inline.
    private void ResumeAfterDeferredWindow(int resumeTo)
    {
        if (OpenOverconfidentDrawWindow(resumeTo)) return;
        // FinishBattleResolution's own tail (FinishBattleReturnHomeAndTransition) defers itself for
        // this window -- recognized generically by the state shape a resolved-but-not-yet-transitioned
        // battle is always left in (Phase still Battle, but CurrentBattleProvince already cleared),
        // rather than a stashed callback (MatchState stays a plain, copyable state bag). resumeTo IS
        // attacker here: FinishBattleResolution always passes its own attacker as closesTo.
        if (_state.Phase == Phase.Battle && _state.CurrentBattleProvince is null && _state.WinnerId is null)
        {
            FinishBattleReturnHomeAndTransition(resumeTo);
            return;
        }
        _state.PriorityPlayerId = resumeTo;
        _state.ConsecutivePasses = 0;
    }
    private void Rehonor(CardInstance card)
    {
        if (InPlay(card) && Delay(card, DelayedKinds.Rehonor)) return; // The shared delay mechanism (issue #492, EmperorRulingR2.cs).
        card.Dishonorable = false;
    }
    // Dishonor (core rules Glossary, issue #186): gives a Personality the Dishonorable status --
    // treated as Personal Honor 0 (see CardPersonalHonor) until Rehonor restores them. The 180-degree
    // rotation the rules describe is purely visual, driven client-side off this same flag; no engine
    // state beyond it.
    // byPlayerId: the player whose card's effect dishonors him, when the caller knows it better than the action being resolved (an Either/Or or a
    // decider's choice answers a separate command); null reads it off the running action (Utaku Ji-Yun, Forgotten Legacy pass issue #467 batch 2).
    private void Dishonor(CardInstance card, int? byPlayerId = null)
    {
        // "all" (Champion of Thunder) or "dishonor" (Social Grace, The Shadow's Embrace pass issue #476 batch 4): NegatesEffect reads both.
        if (NegatesEffect(card, "dishonor"))
        {
            Emit("dishonorNegated", $"{Definition(card).Name}'s dishonoring was negated before it resolved.", card.OwnerId, card.Id);
            return;
        }
        if (Delay(card, DelayedKinds.Dishonor, byPlayerId)) return; // The shared delay mechanism (issue #492, EmperorRulingR2.cs).
        if (JiYunNegatesDishonor(card, byPlayerId)) return;
        if (SuikihimeRefusesDishonor(card)) return; // Otomo Suikihime (issue #503): will not become dishonorable.
        if (RestrictionRefusesDishonor(card)) return; // P11 (CardRestrictions.cs): a stored or static "will not become dishonorable"
        if (TryConsumeWatch(WatchKind.Dishonor, card, byPlayerId ?? EffectSourcePlayer())) return; // P7 (PeriodWatches.cs)
        var wasDishonorable = card.Dishonorable;
        card.Dishonorable = true;
        if (!wasDishonorable) AfterPersonalityDishonored(card, byPlayerId); // Successful Bounty (issue #492, group F).
    }
    // A card already sitting in a discard pile (Zone.DynastyDiscard/FateDiscard) -- never destroyed
    // via DestroyUnits, so it carries no attachments/tokens to cascade -- becomes dead. "Honorably
    // dead" (Glossary) means dead and honorable; honorable:false leaves its current status as-is.
    private void MakeDead(CardInstance card, bool honorable)
    {
        if (honorable) Rehonor(card);
        MarkDead(card);
        Emit("becameDead", $"{Definition(card).Name} became {(honorable ? "honorably " : "")}dead.", card.OwnerId, card.Id);
    }
    // Lady Matsu's Rage (the first Celestial, issue TBD): "After your action resolves that destroyed
    // any Personalities: Remove them from the game." actingPlayerId names whoever's ACTION this
    // destroy is a direct consequence of -- passed only from the specific ability/Attack/duel-outcome
    // call sites below (never from ResolveBattle's own three casualty-sweep calls, which pass none),
    // and only from Action-Timed sources (Battle/Limited/Open, or a raw command like Seppuku) --
    // never from a Reaction (Mirumoto Rokai's own self-destroy cost stays untouched) or from an
    // indirect/derived rule consequence (a Chi-to-0 auto-destroy triggered by reducing Chi/destroying
    // an attachment is not itself "the action's own destroy," the same principle that already
    // excludes battle resolution's own Force-comparison sweep).
    private int DestroyUnits(CardInstance[] cards, bool combatDestruction = false, int? actingPlayerId = null)
    {
        var count = 0;
        var promoNegated = PromoNegatedDestructions(cards, actingPlayerId);
        foreach (var card in cards)
        {
            // Rugashi Bazaar (issue #492, group F): "This card will not be destroyed." -- the destruction simply fails (it is not a negation).
            if (WillNotBeDestroyed(card)) { Emit("destructionFailed", $"{Definition(card).Name} will not be destroyed.", card.OwnerId, card.Id); continue; }
            // Mirumoto Rokai (issue #207 follow-up): "another player's action targets your
            // Personality at Rokai's location, destroy Rokai: negate your Personality's destruction
            // from the action's effects" -- consumed here, the single shared choke point behind
            // every ability-triggered destroy (this is NEVER set for a bulk battle-resolution
            // destroy, only ever by the defer-then-reexecute mechanism immediately re-invoking the
            // one specific ability that was paused for this exact reaction).
            if (NegatesEffect(card, "destroy"))
            {
                Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated before it resolved.", card.OwnerId, card.Id);
                continue;
            }
            if (Delay(card, DelayedKinds.Destroy, actingPlayerId, arg: combatDestruction ? "combat" : null)) continue; // The shared delay mechanism (issue #492, EmperorRulingR2.cs): Spirit of the Berserker, Halls of the Forgotten, a delayed duel loss.
            // Seeds of Decay pass (issue #453), batch 1: an action's effect (actingPlayerId is set exactly then) is negated by Wide Valley at its battlefield and by Megumi's Guidance's shield.
            if (actingPlayerId is not null && WideValleyNegatesDestruction(card)) continue;
            if (actingPlayerId is int shieldActor && DestructionShielded(card, shieldActor)) continue;
            if (actingPlayerId is not null && NegishiNegatesDestruction(card)) continue;
            if (ImmuneNegates(card, ImmunityKind.Destroy, actingPlayerId, "destruction")) continue; // P12 (ImmunityLedger.cs)
            if (TryConsumeWatch(WatchKind.Destroy, card, actingPlayerId)) continue; // P7 (PeriodWatches.cs): after the delay and the protections above, before the effect
            if (promoNegated.Contains(card.Id)) continue; // issue #653: Expansion, The Topaz Championship's trait
            if (actingPlayerId is int armorActor && TfOminousArmorSubstitutes(card, armorActor)) continue; // issue #622: Ominous Armor, "destroy this Item instead" (TfGear.cs)
            foreach (var attachment in Attachments(card.Id).ToArray())
            {
                RemoveAttachmentGrants(card, attachment.Id);
                attachment.LastHostId = card.Id; // issue #492 group G: Blade of Perfection's "its Personality".
                attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.Tokens.Clear(); count++;
                DestroyedDuringBattleBonus(attachment);
                ExpendableDraw(attachment);
                AfterCardDestroyed(attachment); // Makito (issue #496)
            }
            // Fields of Mercy (oracle-l5r-2530): "if a card effect has destroyed another Personality
            // in that army while this card was in play: negate the Personality's destruction." Only
            // the Personality-specific portion below (MarkDead, leaving the battlefield, Overconfident/
            // Expendable/Halls-of-Memory/Lady-Matsu's-Rage consequences) is skipped -- her attachments
            // were already destroyed unconditionally above, matching "an effect that destroys an
            // entire unit would destroy all cards attached to the personality but not the personality."
            if (Definition(card).Type == CardType.Personality && FieldsOfMercyNegatesPersonalityDestruction(card) && !HiromiUnnegatable())
            {
                Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated by Fields of Mercy (attachments were still destroyed).", card.OwnerId, card.Id);
                continue;
            }
            var destroyedFrom = card.Zone == Zone.Battlefield ? card.ProvinceIndex : null; // Emperor Edition remaining-cards pass, group C (issue #492): where he stood, for "in a defending army" (The Empress' Address).
            card.ProvinceIndex = null;
            MarkDead(card);
            count++;
            PromoAfterPersonalityDestroyed(card, actingPlayerId); // issue #653: Ritual of Sacrifice
            // Action-effect tracking (issue #437): only ever recorded when this destruction came from a specific action (actingPlayerId is set) --
            // never for the mass casualty sweep a battle's own resolution performs directly (see the comment above on IsCreatedCard's own check).
            if (actingPlayerId is not null) RecordActionEffect(card.Id, DestroyedKind, fromBattlefield: destroyedFrom);
            // The destruction itself, and everything it normally triggers (the Dishonorable-death
            // penalty MarkDead already applied above, ExpendableDraw, DestroyedDuringBattleBonus,
            // Overconfident), still fully happens -- Lady Matsu's Rage only overrides where the
            // Personality ends UP afterward. Attachments are unaffected (her own text names only the
            // Personality itself), so the attachment-discard loop above is left untouched.
            // A created card (Kitsu Akai's Lion Ancestor, issue #257) has no physical printed card
            // to return to any discard pile -- checked first and unconditionally (not gated on
            // actingPlayerId like Lady Matsu's Rage below), since battle resolution's own casualty
            // sweep -- the most common way one actually dies -- calls this with no actingPlayerId at
            // all.
            if (IsCreatedCard(card))
            {
                card.Zone = Zone.RemovedFromGame;
                Emit("removedFromGame", $"{Definition(card).Name} was destroyed and removed from the game.", card.OwnerId, card.Id);
            }
            else if (actingPlayerId is int aid && Definition(card).Type == CardType.Personality && ControlsCelestialEffect(aid, "lady-matsus-rage"))
            {
                card.Zone = Zone.RemovedFromGame;
                Emit("removedFromGame", $"{Definition(card).Name} was destroyed and removed from the game (Lady Matsu's Rage).", card.OwnerId, card.Id);
            }
            if (combatDestruction && _state.Phase == Phase.Battle && HasHalls(card.OwnerId) && Keyword(card, "Ancestor")) GainHonor(card.OwnerId, 1, "Halls of Memory — Combat Segment destruction");
            DestroyedDuringBattleBonus(card);
            ExpendableDraw(card);
            Emit("unitDestroyed", $"{Definition(card).Name} and its attachments were destroyed.", card.OwnerId, card.Id);
            AfterCardDestroyed(card); // Makito (issue #496): "After a card is destroyed: Give Makito +1F."
            P4AfterDestroyed(card); // Gozaru no Oni (issue #503, EmperorP4.cs): "After Gozaru is destroyed: It becomes discarded (not dead)."
        }
        return count;
    }
    // Disciples of Ganesh's shape: "After this Follower is destroyed during battle, gain 1 Honor" --
    // a static Effect checked at every destruction path (the sweep above, and DestroyAttachment's own
    // single-card path), gated on the Battle phase rather than combatDestruction specifically, since the
    // printed condition is "during battle" (the phase), not "as a result of battle resolution."
    private void DestroyedDuringBattleBonus(CardInstance card)
    {
        if (_state.Phase == Phase.Battle && Definition(card).Effect is "gain-honor-if-destroyed" or "force-while-defending-honor-if-destroyed") GainHonor(card.OwnerId, 1, $"{Definition(card).Name} destroyed during battle");
    }
    // Expendable keyword (core rules): a mandatory Fate-deck draw whenever a card carrying it is
    // destroyed by any effect -- distinct from merely being discarded (core rules Glossary: Destroyed
    // vs. Discarded). Checked at every real "destroy" path -- here, and DestroyAttachment's own
    // single-attachment path -- never at a plain discard (DiscardFate, hand discards, etc.), since
    // those never call these functions. Keyword-keyed, not Effect-keyed: any card type can carry it.
    private void ExpendableDraw(CardInstance card)
    {
        if (Keyword(card, "Expendable")) Draw(Player(card.OwnerId));
    }
    private void DestroyProvince(int owner, ProvinceState province)
    {
        BeforeDestroyOtosanUchiProvince(owner, province); // Plains of Otosan Uchi (issue #554): "Before this Province is destroyed, lose 10 Honor," and its own second slot's card.
        province.Destroyed = true;
        // Village Guardian (oracle-l5r-9162, issue #285): the single choke point for "a province
        // destroyed this game" -- set once, never cleared, read by village-guardian-draw's own count.
        _state.ProvinceEverDestroyed[owner] = true;
        // Ohsuki's Sparrow (oracle-l5r-10023, issue #299): the windowed twin of the flag above, cleared
        // at the end of the OWNER's own next turn (FinishTurn) -- see MatchState's own doc comment.
        _state.ProvinceDestroyedSinceTurnEnd[owner] = true;
        if (province.CardId is string id) { var card = Card(id); card.Zone = Zone.DynastyDiscard; card.FaceUp = true; card.ProvinceIndex = null; }
        province.CardId = null;
        // A Region attaches to the province itself -- it cannot outlive the province it is attached to.
        if (province.RegionId is string regionId) { var region = Card(regionId); region.Zone = Zone.DynastyDiscard; region.ProvinceIndex = null; }
        province.RegionId = null;
        DiscardFortificationsOf(owner, province.Index); // P15 (FortificationHoldings.cs): the Fortifications that guarded it go with it
        Emit("provinceDestroyed", $"{Player(owner).Name}'s province {province.Index + 1} was destroyed.", owner);
        if (Player(owner).Provinces.All(p => p.Destroyed)) Win(1 - owner, "Military victory");
    }
    private void ReturnHome(CardInstance card, bool bow)
    {
        card.Zone = Zone.Home; card.ProvinceIndex = null;
        if (bow) card.Bowed = true;
        foreach (var attachment in Attachments(card.Id)) { attachment.ProvinceIndex = null; if (bow) attachment.Bowed = true; }
    }
    // Dragon Clan review (issue #207): the mirror image of ReturnHome -- a Personality already in
    // play (Mirumoto Michi/Togashi Rikyou's own shape: "move him to the current battlefield") moves
    // to reinforce the current battle rather than leaving it, unlike Reserve's own dynasty-recruit
    // path (which brings a fresh card straight from its province, never a card already in play).
    // Returns whether the card moved: false only when Kyuden Kitsune negated the movement (the card stays where it was; callers ignore the result unless it matters).
    private bool MoveToBattlefield(CardInstance card, int province)
    {
        if ((card.Zone != Zone.Battlefield || card.ProvinceIndex != province) && (MovementLockNegates(card, MovementKinds.ToBattlefield) || MovementToBattlefieldNegated(card, province) || KitsuneNegatesMovement(card, province) || UguluNegatesMovement(card, province))) return false;
        // The Shadow's Embrace pass (issue #476), batch 6: Shieldman "will not move into an attacking army"; Star-Crossed Lovers "will not move to each other's battlefields".
        if ((card.Zone != Zone.Battlefield || card.ProvinceIndex != province) && (ShieldmanNegatesMove(card) || StarCrossedNegatesMove(card, province))) return false; // Goju Asuka (issue #501): "will not move into a defending army".
        if ((card.Zone != Zone.Battlefield || card.ProvinceIndex != province) && (AsukaNegatesMove(card) || HoteiNegatesMove(card))) return false; // Hotei's Contentment (issue #503)
        if (P4NegatesMoveToBattlefield(card, province)) return false; // The Snow Has Teeth (issue #503, EmperorP4Watches.cs): a move from its battlefield. // Goju Asuka (issue #501): "will not move into a defending army".
        // Legion of the Bat (issue #279): "after the resolution of an action that moved a target
        // enemy Infantry Personality to this card's battlefield" -- every printed "move to the
        // current battlefield" ability (Mirumoto Michi/Togashi Rikyou/Intervening Spirit/Takasho/
        // Houken, Experienced) funnels through this one function, so queuing here, keyed on whether
        // the card genuinely started somewhere else, IS "reuse MoveToBattlefield itself" rather than
        // duplicating its logic at each of those call sites. Ordinary Attack/Defense assignment goes
        // through Assign() instead, never this function; RecruitReserve moves a fresh card from its
        // own province, also never through here -- both are excluded for free, no extra check needed.
        var arrivedFromElsewhere = card.Zone != Zone.Battlefield || card.ProvinceIndex != province;
        card.Zone = Zone.Battlefield; card.ProvinceIndex = province;
        foreach (var attachment in Attachments(card.Id)) attachment.ProvinceIndex = province;
        if (arrivedFromElsewhere) _state.PendingMoveReactionCardId = card.Id;
        if (arrivedFromElsewhere) FireConsecrationWatches(card, province); // Consecration (issue #492, group E): "moves to an attacking army".
        if (arrivedFromElsewhere) FireKachikoJoinWatches(card); // Bayushi Kachiko (issue #778): "joins an attacking army".
        // Bayushi Mitsuo (Emperor Edition Personality pass, issue #477, batch 4): "a Battle action that moved Mitsuo" -- every move an action makes is on the action's record, including the
        // unit that moves itself (its own source or performer), which TryMoveToBattlefield's target moves are not the only ones.
        if (arrivedFromElsewhere && _runningCommand is not null) RecordActionEffect(card.Id, MovedKind, toBattlefield: province);
        return true;
    }

    // Asahina Tsugio / Asako Kijo (issue #223): "other players' maximum hand sizes are increased by
    // 1" -- the printed 8 becomes a per-player computed value, raised by 1 for every in-play card an
    // OPPONENT controls carrying this Effect (either of them, on either side, could stack).
    // False Ring of Earth (oracle-l5r-10532, issue #295): "Other players have -1 maximum hand size" --
    // a separate Effect from increase-other-hand-size, so an increaser and a decreaser net out.
    // HandLimitFor also names each contributing card for the public hand-limit placard.
    private HandLimitView HandLimitFor(int playerId)
    {
        var modifiers = _state.Cards.Values
            .Where(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Effect is "increase-other-hand-size" or "decrease-other-hand-size"
                // Kazehime (The Shadow's Embrace pass, issue #476 batch 1): "Your maximum hand size is increased by one."
                || c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == "increase-own-hand-size")
            .Select(c => new HandLimitModifier(c.Id, Definition(c).Name, Definition(c).Effect is "increase-other-hand-size" or "increase-own-hand-size" ? 1 : -1))
            .Concat(EmperorRemainingEHandLimit(playerId)).ToArray(); // Togashi's Guidance (Emperor Edition remaining-cards pass, issue #492, group E).
        modifiers = [.. modifiers, .. P2HandLimit(playerId, 8 + modifiers.Sum(m => m.Amount) + Player(playerId).MaxHandBonus)]; // issue #503: The Tapestry Perceived, Pillowbook (EmperorP2Hand.cs)
        // Benefices of the Emperor (issue #492, group F): "permanently increase his maximum hand size by three".
        if (Player(playerId).MaxHandBonus > 0) modifiers = [.. modifiers, new HandLimitModifier("", "Benefices of the Emperor", Player(playerId).MaxHandBonus)];
        return new(8, 8 + modifiers.Sum(m => m.Amount), modifiers);
    }
    private int MaxHandSize(int playerId) => HandLimitFor(playerId).Maximum;
    private void EndTurn()
    {
        // Abandoning the Fortunes (issue #492, group F): "before each player's turn ends" -- the player whose turn is ending may owe a choice first.
        if (BeginAbandoningTheFortunes()) return;
        // Embracing Virtue (RegretEmbracingVirtue.cs): "Before an End Phase begins, if this card is in your discard pile: You may put it in your hand" -- ahead of the End Phase's draw and hand-size discard.
        if (BeginEmbracingVirtue()) return;
        // Tamori Tamoko's own Open action (issue #207 follow-up): "before the turn ends, gain 1
        // Honor if he was not assigned to attack your provinces this turn" -- drained here,
        // unconditionally, the same turn every one of these was queued (her own Open Timing is only
        // ever usable "if it is not your turn," so every pending check here belongs to THIS turn
        // actually ending now).
        foreach (var check in _state.PendingTamokoHonorChecks)
            if (Card(check.TargetId).AssignedToAttackTurn != _state.TurnNumber) GainHonor(check.BeneficiaryPlayerId, 1, "Tamori Tamoko", Card(check.TargetId));
        _state.PendingTamokoHonorChecks.Clear();
        ResolveProvincesHeldHonorChecks(); // Forgotten Legacy pass (issue #467), batch 1: Asahina Shigemitsu's "before it ends" Honor.
        ResolveShadowsEmbraceBatch1TurnEnd(); // The Shadow's Embrace pass (issue #476), batch 1: Bayushi Misaki's check, Agasha Kurou's removal.
        ResolveWlStratTornTurnEnd(); // issue #674 (WlStratTorn.cs): Forgotten Teachings' "(this turn)" ends.
        ResolvePreservingBeautyChecks(); // The Shadow's Embrace pass (issue #476), batch 4: Preserving Beauty's "before this turn ends".
        // Spirit of the Berserker: a destruction put off "until before this turn ends" happens now.
        ControlReturnsAtTurnEnd(); // Black Marketeering (issue #501, EmperorMechB.cs): "Give control of him to his owner before the turn ends."
        ResolveSearchPlayTurnEnd(); // Masaya (issue #667, SearchPlayMasaya.cs): legacy drain of the pre-P21 debt list.
        CaptureInPlayDuringTurn(); // P21 (ZoneExtensions.cs): who was in play during this player's turn
        ClearActionScopedModifiers(); // P6 (AfterFacts.cs): a backstop for an action that never reached its tail
        ApplyDelayedEffects(DelayUntil.EndOfTurn);
        ReturnSteppedUnits(); // Step Through the Void (issue #503, EmperorP5Zones.cs): "Before this turn ends, put his unit into play". // The shared delay mechanism (issue #492, EmperorRulingR2.cs): what was delayed "until before this turn ends" (Spirit of the Berserker's destruction, In the Heart of Battle's).
        ResolveEmperorRemainingBTurnEnd(); // Emperor Edition remaining-cards pass (issue #492), group B.
        ResolveShadowsEmbraceBatch5TurnEnd(); // The Shadow's Embrace pass (issue #476), batch 5: Celestial Sword of the Phoenix's "remove him from the game before the turn ends".
        ResolveReturnsToHandBeforeTurnEnds(); // The Shadow's Embrace pass (issue #476), batch 3: Methodical Questioning (ShadowsEmbraceBatch3.cs).
        // A window a delayed destruction opened (Overconfident's draw) is answered first; the turn then ends when the player ends the phase again.
        if (_state.WinnerId is not null || OpenOverconfidentDrawWindow(_state.ActivePlayerId)) return;
        // Lady Matsu's Rage: "this turn" (its own printed parenthetical) -- any watch that never fired
        // (the target never straightened) simply expires here, with no further consequence.
        _state.PendingLadyMatsusRageWatches.Clear();
        // Akodo Kobi (issue #440 follow-up): "before the current turn ends" -- exactly here, the next End Phase reached by ANY player (this function runs
        // for every player's own turn ending, not just one). A second Overconfident check catches anything a freshly-entered Personality just queued.
        ResolvePendingEndPhaseRecruits();
        if (_state.WinnerId is not null || OpenOverconfidentDrawWindow(_state.ActivePlayerId)) return;
        var player = Player(_state.ActivePlayerId);
        SetPhase(Phase.End, player.Id);
        if (player.DishonorLossPending) { Win(1 - player.Id, "Dishonor defeat at the beginning of the End Phase"); return; }
        if (TutorialHold(Phase.End, player.Id, () => ContinueEndPhase(player))) return; // the tutorial's table can stop at the start of the End phase
        ContinueEndPhase(player);
    }
    private void ContinueEndPhase(PlayerState player)
    {
        ResolvePendingEndPhaseDraws(); // Eternal Victory Dojo (Stronghold pass, issue #486 batch 1): "Draw a card after the next End Phase begins."
        if (_state.WinnerId is not null) return;
        DrawTurnRuleEndTurnCards(player); // P4 (TurnBoundaryRules.cs): a standing rule's extra end-of-turn draw, judged before the End Phase's own draw
        if (!_tutorialNoEndDraw) Draw(player); // a tutorial table that keeps hands as the lesson set them (TutorialSetup.NoEndDraw)
        // Ingenuity (issue #453, batch 2): "draw an additional card during your next End Phase".
        DrawExtraEndPhaseCards(player);
        var maxHand = MaxHandSize(player.Id);
        if (player.Hand.Count > maxHand) _state.PendingChoice = new("discardHand", player.Id, null, $"Discard {player.Hand.Count - maxHand} card(s) to return to {maxHand}.");
        else FinishTurn();
    }
    private void DiscardHand(GameCommand command)
    {
        var player = Player(command.PlayerId); var ids = command.CardIds ?? []; var maxHand = MaxHandSize(command.PlayerId);
        Require(ids.Length == player.Hand.Count - maxHand && ids.Distinct().Count() == ids.Length && ids.All(player.Hand.Contains), $"Select exactly enough different cards from your hand to return to {maxHand}.");
        foreach (var id in ids) { player.Hand.Remove(id); var card = Card(id); card.Zone = Zone.FateDiscard; card.FaceUp = true; Emit("handDiscard", $"{player.Name} discarded {Definition(card).Name}.", player.Id, id); }
        _state.PendingChoice = null; FinishTurn();
    }
    private void FinishTurn()
    {
        if (MadnessLossAtTurnEnd()) return; // P13 (RuleFlags.cs): a madness-loss flag
        MechDTurnEnds(); // Issue #501, mechanics group D (EmperorMechD.cs): "remove him from the game when the turn ends" (Yajinden); the Recon counters and the attach log restart.
        ClearReconnaissanceAtTurnEnd(); // Hidden Falls Dojo (Stronghold pass, issue #486 batch 1): a battlefield exists only within its Attack Phase, and so does Reconnaissance at it.
        // A Ring's recruit grant made for a battle that was never fought (issue #379) does not carry into another turn; no other grant outlives its battle.
        _state.AdditionalActions.Clear();
        RestoreTemporaryRemovals(TemporaryRemovals.Turn);
        AdvanceStraightenHolds(StraightenPoint.TurnEnds);
        foreach (var card in _state.Cards.Values) { card.Modifiers.RemoveAll(m => !m.Permanent); card.GrantedKeywords.RemoveAll(g => g.Duration == "Turn"); card.GrantedAbilities.RemoveAll(g => g.Duration is GrantDuration.Turn or GrantDuration.Battle); card.Protections.RemoveAll(p => p.Duration == "Turn"); card.ActionLocks.RemoveAll(l => l.Duration == ActionLockUntilTurnEnd); }
        // Ohsuki's Sparrow (issue #299): "since your last turn ended" -- the turn that is ending right
        // now is the ACTIVE player's own, so it restarts that one player's window; the other player's
        // flag (which may already be set by destruction during this very turn) is left untouched until
        // THEIR turn ends.
        _state.ProvinceDestroyedSinceTurnEnd[_state.ActivePlayerId] = false;
        _state.HonorLostToOtherActionSinceTurnEnded[_state.ActivePlayerId] = false; // Rugashi Bazaar (issue #492, group F): "since your last turn ended".
        _state.TurnNumber++; _state.ActivePlayerId = 1 - _state.ActivePlayerId;
        StartTurn();
    }
    private void StartTurn()
    {
        foreach (var each in _state.Players) each.ProclaimedCardId = null; // a new turn: every Personality may be Proclaimed again (SeedsOfDecayBatch2.cs ProclaimRejection)
        var player = Player(_state.ActivePlayerId);
        _state.PriorityPlayerId = player.Id;
        // Akodo Hotaka (issue #453, batch 2) lowers the 40 his controller needs (HonorVictoryThreshold).
        if (player.Honor >= HonorVictoryThreshold(player.Id)) { Win(player.Id, "Honor victory at the start of the turn"); return; }
        // Enlightenment victory (issue #194): begins their turn with all five REAL elemental Rings
        // (Fire/Water/Air/Earth/Void) in play -- checked by Keyword, not a fixed card id, since more
        // than one printing of a given Ring exists. Every False Ring carries its own matching
        // elemental keyword too (e.g. False Ring of Air prints "Air"), so this alone is not enough
        // to exclude them -- but all five False Rings also carry "Fudo", which no real Ring does,
        // so that keyword is the actual False-Ring exclusion.
        if (!EnlightenmentBarred(player.Id) && RingElements.All(e => _state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Ring && Keyword(c, e) && !Keyword(c, "Fudo") && CountsForEnlightenment(c))))
        { Win(player.Id, "Enlightenment victory"); return; }
        player.TurnsTaken++;
        PromoTurnBegins(player); // issue #653 (PromoEmperorEvents.cs): Losing the Way
        RestrictionsTurnBegin(); // P11 (CardRestrictions.cs): delayed Celestial entries, expired restrictions
        MechDTurnBegins(player); // Issue #501, mechanics group D (EmperorMechD.cs): "remove ... from the game when your next turn begins" (Amazing Feat).
        player.MayNotAttackThisTurn = false;
        player.FavorActionsThisTurn = 0;
        player.AttachedFromHandThisTurn = false;
        ExpireEmperorRemainingCAtTurnStart(player); // Emperor Edition remaining-cards pass, group C (issue #492): Rumors Travel's "until your next turn begins".
        _state.HonorLostToOtherActionSinceTurnBegan[player.Id] = false; // Kitsu Tamasine (issue #477, batch 5): "since your last turn began".
        player.DestroyedEnemyPersonalityActionsThisTurn = 0;
        // "Until your second turn from now begins" (issue #259): the one place a turn begins for
        // THIS specific player, so the one place their own bounded-duration ongoing Event's own
        // effect can end -- moved to DynastyDiscard the same way an ordinary Event's own resolve-
        // once-then-discard already does (Zone.EventInPlay's own doc comment names this as the
        // intended future shape). Scoped to c.OwnerId==player.Id: only THIS player's own TurnsTaken
        // just advanced, so only their own cards' expiry could possibly be reached right now.
        foreach (var expired in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.EventInPlay && c.ExpiresAtTurnsTaken == player.TurnsTaken).ToArray())
        {
            OnForgottenLegacyBatch5EventExpired(expired); // Willing Spirits' delayed effect (issue #467).
            OnEmperorRemainingFEventExpired(expired); // Boastful Proclamation's start-of-turn effect, Delayed Arrival's target (issue #492, group F).
            expired.Zone = Zone.DynastyDiscard;
            Emit("eventExpired", $"{Definition(expired).Name}'s effect ended.", expired.OwnerId, expired.Id);
        }
        ResolveShadowsEmbraceBatch5TurnBegins(); // The Shadow's Embrace pass (issue #476), batch 5: Blood of the Preserver's units re-enter play "after the next turn begins".
        ApplyBayushisGuidanceTrait(player); // Bayushi's Guidance (Emperor Edition remaining-cards pass, issue #492, group E): "After a player's turn begins, if he is Scorpion Clan: Each other player loses 1 Honor."
        AdvanceStraightenHolds(StraightenPoint.TurnBegins);
        if (RunTurnBegins(player)) return; // P4 (TurnBoundary.cs): durations that end as the turn begins, then the "after a turn begins" callbacks; a pending choice resumes through ResumeTurnBegins
        StraightenPhase(player);
    }
    private static readonly string[] RingElements = ["Fire", "Water", "Air", "Earth", "Void"];
    private void StraightenPhase(PlayerState player)
    {
        SetPhase(Phase.Straighten, player.Id);
        // Bayushi Rentatsu (Emperor Edition Personality pass, issue #477, batch 4): "after each of your turns begins, choose a player; he loses 2 Honor" while he remains bowed (EmperorPersonalityBatch4.cs).
        if (BeginBlessingsChoice(player)) return; // Blessings of the Shi-Tien Yen-Wang (issue #503, EmperorP5Zones.cs): "After your turn begins: You may choose your dead Personality ..."
        if (BeginRentatsuLoss(player)) return;
        ContinueStraightenPhase(player);
    }
    private void ContinueStraightenPhase(PlayerState player)
    {
        // Goju Asuka / Only a Shadow (issue #501, EmperorMechB.cs): "After a Straighten Phase begins: Choose your bowed non-X Personality. Negate his straightening this phase."
        if (BeginInfiltratorStraighten(player)) return;
        // A card with the choose-straighten effect lets its controller decide, per turn, whether to
        // straighten it instead of that being automatic or a fixed forced-skip condition.
        if (EligibleForOptionalStraighten(player.Id).Length > 0)
        {
            _state.PendingChoice = new("declineStraighten", player.Id, null, "Choose which cards to leave bowed instead of straightening.");
            return;
        }
        FinishStraightening(player, []);
    }
    private string[] EligibleForOptionalStraighten(int playerId) => _state.Cards.Values.Where(c => c.OwnerId == playerId && (c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Attached
        && Definition(c).Effect == "choose-straighten" && c.StraightenBlockedTurn != _state.TurnNumber || RentatsuMayStayBowed(c) || PanKuMayStayBowedInPhase(c))).Select(c => c.Id).ToArray();
    private void ResolveDeclineStraighten(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "declineStraighten" && command.PlayerId == pending.PlayerId, "Choose which cards to leave bowed instead of straightening.");
        var eligible = EligibleForOptionalStraighten(pending.PlayerId).ToHashSet(StringComparer.Ordinal);
        var declined = (command.CardIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
        Require(declined.All(eligible.Contains), "Only cards with the choose-straighten ability can be left bowed here.");
        _state.PendingChoice = null;
        FinishStraightening(Player(pending.PlayerId), declined);
    }
    private void FinishStraightening(PlayerState player, string[] declinedIds)
    {
        _w827StraightenPhase = true; // issue #827: the Straighten Phase's own straightening of P'an Ku was the controller's choice (declineStraighten)
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone is Zone.Home or Zone.Stronghold or Zone.Attached))
        {
            if (declinedIds.Contains(card.Id)) Emit("straightenDeclined", $"{Definition(card).Name} was left bowed by choice.", card.OwnerId, card.Id);
            else Straighten(card);
        }
        StraightenRegions(player); // Emperor Edition remaining-cards pass (issue #492), group F: a Region bowed as a cost straightens with its province's owner's other cards.
        StraightenWindOfTheMoonShugenja(player); // Wind of the Moon (issue #467): "he straightens during each player's Straighten Phase".
        StraightenNaleeshUnits(player); // Moto Naleesh (issue #477): "Naleesh's unit straightens during each player's Straighten Phase".
        Emit("straightened", $"{player.Name}'s turn: eligible cards straightened.", player.Id);
        AdvanceStraightenHolds(StraightenPoint.StraightenPhaseEnds);
        // Cultist Infiltrator (oracle-l5r-1595): "After each Straighten Phase: Give this Personality
        // a -1C token" -- fires once per turn's single Straighten Phase, for every Cultist
        // Infiltrator in play regardless of whose turn it is (no "your own turn" qualifier printed)
        // or which player controls its host (it may be attached to an enemy Personality). Routed
        // through CreateToken so its own existing Chi<=0 death check (shared with every other
        // Chi-token path) still fires immediately if this brings the host to 0 or below.
        foreach (var infiltrator in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == "cultist-infiltrator").ToArray())
            CreateToken(Card(infiltrator.AttachedTo!), "Cultist", infiltrator.Id, "Chi", -1);
        _w827StraightenPhase = false;
        if (TutorialHold(Phase.Straighten, player.Id, () => EventsPhase(player))) return; // the tutorial's table can stop on the Straighten phase
        EventsPhase(player);
    }
    private void EventsPhase(PlayerState player)
    {
        SetPhase(Phase.Events, player.Id);
        ExpireEmperorRemainingFEventsAtEventsPhase(player); // Cherry Blossom Festival, Disgrace, Successful Bounty (issue #492, group F): "until your Events Phase ... begins".
        Wave738EventsPhaseBegins(); // issue #738 (Wave738.cs): Suck the Marrow, "after each Events Phase begins".
        _state.EventsRevealNext = 0;
        ContinueEventsReveal(player);
    }
    // Split from EventsPhase (issue #259, Harsh Choices) so a reveal that needs a genuine player
    // decision (destroy this province or not) can defer through a PendingChoice mid-loop, then resume
    // with whatever provinces remain, instead of forcing every revealed Event to resolve unconditionally
    // and synchronously the way Suspicions' shape always could. ResolveHarshChoices calls back in here
    // once its own choice resolves.
    private void ContinueEventsReveal(PlayerState player)
    {
        // Deck search (issue #312): a reveal that deferred to a player decision (Harsh Choices, Imperial Gift's
        // search) resumes from the next province, never from the start -- restarting at province 0 would reveal, in
        // this same phase, the face-down card the deferred Event's own province had just been refilled with.
        foreach (var province in player.Provinces.Where(p => p.Index >= _state.EventsRevealNext && !p.Destroyed && (p.CardId is not null || p.CardId2 is not null)))
        {
            // Plains of Otosan Uchi (issue #554): a province with a second Dynasty slot is routed wholesale through its
            // own reveal-order-aware handler instead of the single-slot block below, which only ever reads CardId.
            if (province.CardId2 is not null || _state.OtosanUchiProvinceIndex == province.Index)
            {
                if (HandleOtosanUchiProvinceReveal(player, province)) return;
                continue;
            }
            var card = Card(province.CardId!);
            if (!card.FaceUp && TutorialRevealHold(player.Id, province.Index, after: false, () => ContinueEventsReveal(player))) return; // tutorial only: stop before this province is revealed
            _state.EventsRevealNext = province.Index + 1;
            if (!card.FaceUp)
            {
                card.FaceUp = true;
                Emit("provinceRevealed", $"{player.Name} revealed {Definition(card).Name} in province {province.Index + 1}.", player.Id, card.Id);
                if (TutorialRevealHold(player.Id, province.Index, after: true, () => { if (!ResolveRevealedCard(player, province, card)) ContinueEventsReveal(player); })) return; // tutorial only: stop with the card face up, before it resolves
                if (ResolveRevealedCard(player, province, card)) return;
            }
            // An Event already face up (refilled face up by Border Keep's cycle or the Kharmic replacement) was never
            // revealed here, but Events resolve only in the Events Phase: it resolves now instead of staying stuck
            // (rules lead ruling, docs/decisions/2026-09-22-suspicions-owner-events-phase.md).
            else if (Definition(card).Type == CardType.Event && ResolveEvent(player, province, card)) return;
        }
        if (RunAfterProvincesRevealed(player)) return; // P5 (ProvinceRevealEvents.cs): Events that stay face up in their province fire now; a decision pauses the phase
        AdvanceStraightenHolds(StraightenPoint.EventsRevealEnds);
        // Suspicions-shaped ongoing Event effects: re-checked every Events Phase (see ResolveOngoingEvents),
        // not just the phase that reveals them, so a card already sitting in Zone.EventInPlay from a
        // previous turn triggers here too.
        ResolveOngoingEvents(player);
        if (P3AfterEventsPhase(player)) return; // Daigotsu Miki (issue #503): "After your Events Phase"
        if (TutorialHold(Phase.Events, player.Id, () => SetPhase(Phase.Action, player.Id))) return; // the tutorial's table can stop with the Events phase revealed
        SetPhase(Phase.Action, player.Id);
    }
    // What revealing a card in the Events Phase does, by type; true when the resolution deferred to a player decision (the caller stops the reveal loop).
    private bool ResolveRevealedCard(PlayerState player, ProvinceState province, CardInstance card)
    {
        var type = Definition(card).Type;
        if (type == CardType.Event) return ResolveEvent(player, province, card);
        if (type == CardType.Region) return ResolveRegion(player, province, card);
        if (type == CardType.Celestial) return ResolveCelestialCore(player, province, card, pauseReveal: true); // P6: the entry decisions pause the reveal
        // Tested Land (Seeds of Decay, issue #453): "After a Holding is revealed in this Province, discard it and this Region." (SeedsOfDecayBatch4.cs)
        if (type == CardType.Holding) DiscardRevealedHoldingOnTestedLand(player, province, card);
        return false;
    }
    // An Event (core rules Glossary): removed from its province and resolved immediately upon reveal --
    // before refilling -- then discarded, unless its own printed text is an ongoing effect (Suspicions'
    // "until the end of the game" shape, or issue #259's own newer "until your second turn from now
    // begins" bounded shape -- see CardInstance.ExpiresAtTurnsTaken), in which case it goes to
    // Zone.EventInPlay instead of the discard pile, as a physical reminder that the effect is still
    // active. Effect-keyed, the same convention every other one-off card behavior in this file already
    // uses. Returns true when resolution instead deferred to a PendingChoice (Harsh Choices' own "you
    // may destroy this province" -- the only one of these needing a genuine decision at reveal time),
    // in which case ContinueEventsReveal's own loop must stop here; ResolveHarshChoices resumes it.
    private bool ResolveEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (StaysFaceUpInProvince(card)) return false; // P5 (ProvinceRevealEvents.cs): it neither resolves at reveal nor leaves its province
        if (P3BeforeEvent(player, province, card) is { } p3Event) return p3Event; // Forging Destiny (issue #503): the window before an Event resolves
        if (RegisteredResolveEvent(player, province, card) is { } registeredEvent) return registeredEvent; // issue #653 (PromoEmperorEvents.cs)
        if (Definition(card).Effect == "harsh-choices")
        {
            _state.PendingChoice = new("harshChoices", player.Id, card.Id, "Harsh Choices: you may destroy this province to draw 3 cards.");
            return true;
        }
        // Imperial Gift (issue #312): "Gain 2 Honor and search your Fate deck for an Item, show it, and put it into
        // your hand." The Honor first, then the search (deck search primitive, DeckSearch.cs) -- which defers through
        // its own PendingChoice like Harsh Choices above; ResumeEventAfterSearch discards the Event, refills the
        // province and resumes the reveal loop once the search has shuffled and finished. The Event stays in its
        // province, face up, until then.
        // Signs and Portents (issue #322): "Look at the top four cards of any Fate deck. You may discard one of them. Put the rest
        // back in any order." -- the look primitive (DeckLook.cs), deferred through its own PendingChoice exactly like Imperial
        // Gift's search; the Event is discarded and the reveal resumes once the arrangement is submitted.
        if (LookCatalog.ForEventEffect(Definition(card).Effect) is { } eventLook)
        {
            BeginLook(eventLook, card, player.Id, "event");
            return true;
        }
        if (Definition(card).Effect == "imperial-gift")
        {
            GainHonor(player.Id, 2, "Imperial Gift", card);
            BeginSearch(SearchCatalog.ImperialGift, card, player.Id, "event");
            return true;
        }
        // Offering Reverence (Seeds of Decay, issue #453): creates an Ancestor per dead Personality, or asks whether to shuffle itself into the deck (SeedsOfDecayBatch4.cs).
        if (Definition(card).Effect == OfferingReverenceEffect) return ResolveOfferingReverence(player, province, card);
        // The Shadow's Embrace pass (issue #476), batch 6 (ShadowsEmbraceBatch6.cs): Discovering the Temple's search defers like Imperial Gift's; Star-Crossed Lovers' choice
        // like Offering Reverence's.
        if (Definition(card).Effect == DiscoveringTheTempleEffect) { BeginSearch(SearchCatalog.DiscoveringTheTemple, card, player.Id, "event"); return true; }
        if (Definition(card).Effect == StarCrossedLoversEffect) return ResolveStarCrossedLovers(player, province, card);
        if (Definition(card).Effect == IvorySpellAbilities.WisdomGainedEffect) return ResolveWisdomGained(player, card); // issue #622 (IvorySpells.cs): each player may search for a Ring, in turn order.
        // Emperor Edition remaining-cards pass (issue #492), group F (EmperorRemainingFEvents.cs): twelve Events, most of which stay in play for a while or ask a player a question.
        if (IsEmperorRemainingFEvent(Definition(card).Effect)) return ResolveEmperorRemainingFEvent(player, province, card);
        if (IsEmperorP2Event(Definition(card).Effect)) return ResolveEmperorP2Event(player, province, card); // issue #503 (EmperorP2*.cs)
        if (Definition(card).Effect == EmperorP4WatchAbilities.TooCloseToHomeEffect) return ResolveTooCloseToHome(player, province, card); // Too Close to Home (issue #503, EmperorP4Watches.cs)
        if (IsEmperorP5Event(Definition(card).Effect)) return ResolveEmperorP5Event(player, province, card); // Issue #503 (EmperorP5.cs): Starting on the Path, Never Enough Soldiers.
        if (IsEmperorP6Event(Definition(card).Effect)) return ResolveEmperorP6Event(player, province, card); // Issue #503 (EmperorP6.cs): four Events.
        province.CardId = null; card.ProvinceIndex = null;
        switch (Definition(card).Effect)
        {
            case "suspicions":
                card.Zone = Zone.EventInPlay;
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play: each player loses 1 Honor at the end of every Events Phase until the effect ends.", player.Id, card.Id);
                break;
            case "formal-apology":
                card.Zone = Zone.EventInPlay;
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play: watching for {player.Name}'s Honor to fall below its starting value.", player.Id, card.Id);
                CheckFormalApology(player.Id);
                break;
            case "rebuilding-kyuden-hida" or "times-of-strife" or "war-weariness":
                card.Zone = Zone.EventInPlay;
                // player.TurnsTaken already reflects THIS turn's own increment (StartTurn, before
                // EventsPhase ever runs) -- +2 lands on this same player's own second-next turn, not
                // +4, since TurnsTaken only counts their own turns (see the field's own doc comment).
                card.ExpiresAtTurnsTaken = player.TurnsTaken + 2;
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play until {player.Name}'s second turn from now begins.", player.Id, card.Id);
                break;
            // Forgotten Legacy pass (issue #467), batch 5: Assault in the Jungle, Burning Dreams, Willing Spirits (ForgottenLegacyBatch5.cs).
            case AssaultInTheJungleEffect or BurningDreamsEffect or WillingSpiritsEffect:
                ResolveForgottenLegacyBatch5Event(player, card);
                break;
            // The Shadow's Embrace pass (issue #476), batch 6: On and Wartime Preparations last until the game ends (Suspicions' shape).
            case OnEffect:
                card.Zone = Zone.EventInPlay;
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play: at the end of each phase, each player's Honor is lowered to what it was at the start of the phase plus 3 plus any Honor he gained from battle resolution.", player.Id, card.Id);
                break;
            case WartimePreparationsEffect:
                card.Zone = Zone.EventInPlay;
                Emit("eventEnteredPlay", $"{Definition(card).Name} stays in play: from each turn's second bowing (in the Action Phase) or assignment-preventing effect of another player's card on, {player.Name}'s Personalities are spared.", player.Id, card.Id);
                break;
            default:
                card.Zone = Zone.DynastyDiscard;
                Emit("eventResolved", $"{Definition(card).Name} resolved and was discarded.", player.Id, card.Id);
                break;
        }
        Refill(player, province);
        return false;
    }
    // Harsh Choices (issue #259): resolves the "you may destroy this province" choice ResolveEvent
    // deferred -- the province and card are still exactly as ResolveEvent left them (neither ever
    // cleared province.CardId/card.ProvinceIndex when deferring), so DestroyProvince's own normal
    // behavior (it discards whatever card currently sits at province.CardId itself) handles discarding
    // Harsh Choices for free on the destroy branch; the decline branch discards/refills it manually,
    // the same as ResolveEvent's own default case.
    private const int HarshChoicesDraw = 3; // "destroy this province to draw 3 cards" (the view and the resolution share it)
    private void ResolveHarshChoices(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "harshChoices" && command.PlayerId == pending.PlayerId && command.Choice is "destroy" or "decline", "Choose whether to destroy this province.");
        var card = Card(pending.CardId!);
        var province = Player(pending.PlayerId).Provinces[card.ProvinceIndex!.Value];
        var player = Player(pending.PlayerId);
        _state.PendingChoice = null;
        if (command.Choice == "destroy")
        {
            Emit("eventResolved", $"{Definition(card).Name}: {player.Name} destroys the province to draw 3 cards.", player.Id, card.Id);
            DestroyProvince(player.Id, province);
            // Military defeat is immediate. Do not draw or reveal later Events after victory.
            if (_state.WinnerId is not null) return;
            for (var i = 0; i < HarshChoicesDraw; i++) Draw(player);
        }
        else
        {
            province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
            Emit("eventResolved", $"{Definition(card).Name} resolved and was discarded.", player.Id, card.Id);
            Refill(player, province);
        }
        if (_state.WinnerId is null) ContinueEventsReveal(player);
    }
    // A Region (core rules Glossary): attaches to the province it was revealed in -- unless that
    // province already has a Region attached, in which case (like any Region that fails to attach for
    // any reason) it is simply discarded -- then the province refills immediately, same as an Event.
    // Unlike Event, a Region that DOES attach never moves on from Zone.Region on its own; there is no
    // "resolve once, then discard" step, since its whole printed effect (a standing trait, a Reaction,
    // or both) lives for as long as it stays attached.
    // Returns true when the reveal paused for a decision (Tested Land's choice of enemy province, Seeds of Decay, issue #453 -- SeedsOfDecayBatch4.cs).
    private bool ResolveRegion(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect == TestedLandEffect) return ResolveTestedLand(player, province, card);
        province.CardId = null; card.ProvinceIndex = null;
        if (province.RegionId is not null)
        {
            card.Zone = Zone.DynastyDiscard;
            Emit("regionDiscarded", $"{Definition(card).Name} was discarded: {player.Name}'s province {province.Index + 1} already has a Region attached.", player.Id, card.Id);
        }
        else AttachRegionToProvince(player, province, card);
        Refill(player, province);
        return false;
    }
    // The one place a Region enters play attached to a province: the Events Phase reveal above, and a Region found
    // by a deck search that the searcher attaches to a province of their own choice (issue #312, Ikoma Shizuka).
    private void AttachRegionToProvince(PlayerState player, ProvinceState province, CardInstance card)
    {
        card.Zone = Zone.Region; card.ProvinceIndex = province.Index; card.FaceUp = true; province.RegionId = card.Id;
        Emit("regionAttached", $"{Definition(card).Name} attached to {player.Name}'s province {province.Index + 1}.", player.Id, card.Id);
        AfterRegionAttachedP6(province, card); // Dark Experiments (issue #503, EmperorP6.cs)
        AfterRegionAttachedOtosanUchi(province, card); // Plains of Otosan Uchi (issue #554): creates the province's second Dynasty slot.
        PromoCelestialCRegionEntered(player, card); // Deadlands (PromoCelestialC.cs): "After this card enters play: Lose 4 Honor."
    }
    // A Celestial (core rules Glossary): unlike Event/Region, it enters play the instant it is
    // revealed regardless of WHICH phase that reveal happens in -- today the Events Phase reveal loop
    // above is the only place a province card is ever turned face up, so this is only ever actually
    // reached from there, but (unlike Event/Region, whose own resolution is deferred to the
    // controller's next Events Phase if revealed elsewhere) a future "reveal a province card" effect
    // outside the Events Phase must still call ResolveCelestial immediately from wherever it reveals
    // the card, not defer it. A player may control more than one Celestial, but never more than one AT
    // ONCE: entering play discards every other Celestial that SAME player currently controls (not the
    // opponent's). Goes straight to Zone.Celestial -- never attaches to a province the way Region does,
    // and (unlike Event) never resolves-once-then-discards; it just stays in play until something
    // removes it.
    // pauseReveal (P6, AfterFacts.cs): the Events Phase reveal asks for the entry decisions of the Celestial it reveals and carries on once they are answered (true: it paused).
    private void ResolveCelestial(PlayerState player, ProvinceState province, CardInstance card) => ResolveCelestialCore(player, province, card, pauseReveal: false);

    private bool ResolveCelestialCore(PlayerState player, ProvinceState province, CardInstance card, bool pauseReveal)
    {
        if (RestrictionDelaysCelestial(player, province, card)) return false; // P11 (CardRestrictions.cs): a locked province's Celestial enters when the next turn begins
        province.CardId = null; card.ProvinceIndex = null;
        Wave829CelestialRevealed(player, card); // issue #829: Shrine to the Heavens asks only when a Celestial is revealed in a Province
        var paused = EnterCelestialPlayCore(player, card, pauseReveal);
        Refill(player, province);
        return paused;
    }
    // The entry itself (issue #667: The Sacred Rosary of the Constrictors puts a Celestial into play with no province): it replaces the controller's other Celestials and runs the entry traits.
    // P6 (AfterFacts.cs): a registered entry decision (Natsu-Togumara's choice of a Personality, Shrine to the Heavens' choice of a Celestial to keep) is asked after the Celestial is in play; when one
    // spares a card, the discard of the other Celestials waits for the answers. Returns true when the Events Phase reveal paused on the first decision (pauseReveal).
    private void EnterCelestialPlay(PlayerState player, CardInstance card) { Wave829CelestialRevealed(player, null); EnterCelestialPlayCore(player, card, pauseReveal: false); }

    private bool EnterCelestialPlayCore(PlayerState player, CardInstance card, bool pauseReveal)
    {
        var hadRyoshun = ControlsCelestialEffect(player.Id, RyoshunsGuidanceEffect); // Ryoshun's Guidance (issue #467): its +1C leaves with it.
        var asks = EngineHooks.CelestialEntryChoices.Items.Length == 0 ? [] : CelestialEntryAsks(player, card);
        var deferDiscard = asks.Any(a => a.Choice.SparesFromDiscard);
        if (deferDiscard) (_state.CelestialDiscardDebts ??= []).Add(new CelestialDiscardDebt { CelestialId = card.Id, PlayerId = player.Id });
        else DiscardOtherCelestials(player, card);
        card.Zone = Zone.Celestial;
        Emit("celestialEnteredPlay", $"{Definition(card).Name} entered play.", player.Id, card.Id);
        RecordFact(FactKinds.CelestialEntered, player.Id, card.Id); // P6 (AfterFacts.cs)
        Wave829CelestialEntered(player, card); // issue #829 (Wave829.cs): Benten's Blessing queues its choice of Personalities
        ApplyDaigotsusGuidanceTrait(); // Daigotsu's Guidance (Emperor Edition remaining-cards pass, issue #492, group E): "After a Celestial enters play (including this one): Discard all Kami Celestials in play."
        if (hadRyoshun && Definition(card).Effect != RyoshunsGuidanceEffect) CheckChiAfterRyoshunLeaves(player.Id);
        return asks.Count > 0 && QueueCelestialEntryAsks(player, card, asks, pauseReveal);
    }
    // Lady Matsu's Rage's own destroy-redirect trait is Effect-keyed the same way RegionStrengthBonus
    // is, checked from DestroyUnits' own single shared choke point (see its own doc comment).
    private bool ControlsCelestialEffect(int playerId, string effect) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.Celestial && Definition(c).Effect == effect);
    // Same idea, for a bounded-duration ongoing Event sitting in Zone.EventInPlay (issue #259:
    // Formal Apology's own watch, Times of Strife/War Weariness's own "is either player's copy
    // currently active" checks). Unlike ControlsCelestialEffect, callers needing a GLOBAL check
    // (Times of Strife's province clamp, War Weariness's bow -- neither is scoped to "your own"
    // anywhere in its printed text) pass either player id explicitly rather than this helper trying
    // to guess which side's copy should count.
    private bool ControlsEventEffect(int playerId, string effect) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.EventInPlay && Definition(c).Effect == effect);
    // Fallow Lands' shape (the first Region carrying a static province-strength bonus): live-
    // recomputed from whichever Region is currently attached, the same "read live off Attachments()"
    // approach PersonalityForce uses for an Item's own Force convey to its wielder -- NOT a one-shot
    // mutation the way Ring of Earth's raise/lower or a Dueling win bonus apply directly to
    // province.Strength, so it automatically reverses the moment the Region is no longer attached
    // (e.g. its province is destroyed -- see DestroyProvince), with no separate cleanup needed here.
    // Every read of a province's actual strength (battle resolution, the client view, False Ring of
    // Water's own threshold check) must go through this, never the raw province.Strength field alone.
    private static readonly Dictionary<string, int> RegionStrengthBonus = new(StringComparer.Ordinal)
    {
        ["fallow-lands"] = 2,
    };
    private int EffectiveProvinceStrength(ProvinceState province)
    {
        // The CURRENT strength: raised and lowered by everything that has acted on the province, and by the Region and the Terrain bonuses still in play.
        var strength = province.Strength + (province.RegionId is string id && RegionStrengthBonus.TryGetValue(Definition(Card(id)).Effect, out var bonus) ? bonus : 0) + ProvinceSourceBonuses(province) + StaticProvinceBonus(province)
            + TimedProvinceStrength(province) // Seeds of Decay (issue #453), batch 2: strength given for the turn (SeedsOfDecayBatch2.cs).
            + RemATerrainProvinceBonus(province); // Flooded Pass (Emperor Edition remaining-cards pass, group A, issue #492).
        strength = Math.Max(0, strength); // Core rules, Stats: most stats floor at zero (Kaiu Esumi's -2, issue #477 batch 2, is the first penalty that could take a province below it).
        // A limit (TerrainStatics.cs: Times of Strife, issue #259; Ancient Feud, issue #418) is a CEILING on what is read, never a lowering of the current strength: a province already at or
        // below it is untouched, and one above it reads as the maximum until the limit ends.
        return ProvinceStrengthMax(province) is int max ? Math.Min(strength, max) : strength;
    }
    // The net change from the printed strength that the view shows (ProvinceView.StrengthModifier). A destroyed province has none; one without a recorded printed strength cannot say.
    private int StrengthModifierOf(ProvinceState province) => province.Destroyed || province.PrintedStrength <= 0 ? 0 : EffectiveProvinceStrength(province) - province.PrintedStrength;
    private string[]? StrengthNotesOf(ProvinceState province)
    {
        if (StrengthModifierOf(province) == 0) return null;
        var notes = TimedProvinceStrengthParts(province).Where(part => part.Amount != 0).Select(part => $"{part.Source} {part.Amount:+#;-#}").ToArray();
        return notes.Length == 0 ? null : notes;
    }
    // Suspicions (the first published ongoing Event -- core rules Glossary, "until the end of the
    // game"): re-checked every Events Phase rather than snapshotted once, the same living-state
    // approach GrantedKeyword.Duration "Permanent" uses for a keyword grant -- a card sitting in
    // Zone.EventInPlay IS the "effect still active" flag, so nothing else needs to remember it
    // separately, and nothing needs to clear it (Suspicions' own effect never ends). It triggers
    // "before each of your Action Phases" -- only on its owner's own turns -- and each trigger
    // affects BOTH players' Honor once, including the very turn Suspicions
    // itself is revealed (ResolveEvent above already moved it into Zone.EventInPlay earlier in this
    // same EventsPhase call).
    private void ResolveOngoingEvents(PlayerState activePlayer)
    {
        foreach (var card in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay).ToArray())
        {
            // Suspicions' own printed order: "each player in turn order starting with you" -- the
            // revealing/active player's own loss resolves first, then the other player's. It fires
            // only "before each of YOUR Action Phases": on its owner's turns, never the opponent's.
            if (Definition(card).Effect == "suspicions" && card.OwnerId == activePlayer.Id)
                foreach (var playerId in new[] { activePlayer.Id, 1 - activePlayer.Id })
                    GainHonor(playerId, -1, $"Suspicions (end of {activePlayer.Name}'s Events Phase)", card);
        }
    }
    private void Draw(PlayerState player)
    {
        if (player.FateDeck.Count == 0) { Emit("deckEmpty", $"{player.Name}'s Fate deck is empty; no card drawn.", player.Id); return; }
        if (P2CardIntoHandBlocked(player.Id)) return; // issue #503: The Scorpion Wall is Finished
        if (Wave835DefersDraw(player)) return; // issue #835: the Heavens' Blessing makes a card an own Strategy draws a 2 Gold payment
        var id = player.FateDeck[0]; player.FateDeck.RemoveAt(0); player.Hand.Add(id);
        Card(id).Zone = Zone.Hand;
        NoteCardIntoHand(player.Id); // Isawa Kimi - exp 2 (issue #477, batch 5): "an action that put any cards into another player's hand".
        Emit("cardDrawn", $"{player.Name} drew a Fate card.", player.Id); // Deliberately excludes hidden identity.
    }
    private void Refill(PlayerState player, ProvinceState province)
    {
        if (province.Destroyed || province.CardId is not null || player.DynastyDeck.Count == 0 || HoldsNoDynasty(province)) return; // issue #503: Illusory Defense's province holds no Dynasty cards
        var id = player.DynastyDeck[0]; player.DynastyDeck.RemoveAt(0);
        province.CardId = id; var card = Card(id); card.Zone = Zone.Province; card.ProvinceIndex = province.Index; card.FaceUp = false;
        if (RefillWantsFaceUp(player, province)) // P21 (ZoneExtensions.cs): a registered feature makes this one refill face up
        {
            card.FaceUp = true;
            Emit("provinceRefilledFaceUp", $"{player.Name} refilled province {province.Index + 1} face up with {Definition(card).Name}.", player.Id, card.Id);
            AfterProvinceRefilledP6(player, province, faceUp: true);
            return;
        }
        Emit("provinceRefilled", $"{player.Name} refilled province {province.Index + 1} face down.", player.Id); // Neither player can inspect it.
        AfterProvinceRefilledP6(player, province, faceUp: false); // Issue #503 (EmperorP6.cs): Susaiken's / Distant Expansion's face-up refill, Ietsuno's look.
    }
    // Honor loss/gain is permanent by default, with no floor or ceiling and no memory of past
    // changes -- but a standing passive (a Stronghold's printed text, keyed by its Effect below) can
    // negate a loss outright when its source is a card the losing player owns and matching Clan/Fate
    // criteria (core rules Glossary). Only ever checked against the LOSING player's own Stronghold:
    // "cards you own" never reaches an opponent's passive. Only losses are covered so far -- no
    // published or training card yet negates a gain, but nothing here assumes losses are the only
    // negatable direction if one shows up.
    private static readonly Dictionary<string, (string? Clan, bool Fate)> HonorLossImmunity = new(StringComparer.Ordinal)
    {
        ["training-honor-immunity"] = ("Spider", true),
        ["keep-of-the-dead"] = ("Spider", true), // Keep of the Dead (Stronghold pass, issue #486 batch 1): "You do not lose Honor from Spider Clan and Fate cards you own."
        // Steel Soul Dojo (Emperor Edition Stronghold pass, issue #486 batch 2): "You do not lose Honor from Spider Clan and Fate cards you own." (its other trait is FireSteelSoulTrait).
        ["steel-soul-dojo"] = ("Spider", true),
        ["p5-plain-of-glass"] = ("Spider", true), // The Plain of Glass (issue #503, EmperorP5.cs): "You do not lose Honor from Spider Clan and Fate cards you own."
    };
    private static bool IsFateCardType(CardType type) => type is CardType.Strategy or CardType.Follower or CardType.Item or CardType.Spell or CardType.Ring; // core rules, Fate card types: Strategy and Ring, and the attachments
    private bool HonorLossNegated(int playerId, CardInstance? source)
    {
        if (source is null || source.OwnerId != playerId) return false;
        var stronghold = Definition(Card(Player(playerId).StrongholdId)).Effect;
        if (FootholdNegatesLoss(stronghold)) return true; // Foothold of the Mad (issue #766, Foothold.cs): "You do not lose Honor from cards you own."
        if (HonorLossNegatedByKeyword(stronghold, source)) return true; // Seeds of Decay pass (issue #453), batch 1: The Castle of Order's "Ninja cards you own".
        if (!HonorLossImmunity.TryGetValue(stronghold, out var rule)) return false;
        var def = Definition(source);
        return (rule.Clan is not null && HasClanAffiliation(source, rule.Clan)) || (rule.Fate && IsFateCardType(def.Type));
    }
    /// <summary>The reason a Dishonorable Personality's death costs its controller Honor. That loss is the rulebook's (core rules Glossary, "Dead / Destroy"), not an effect of any card or action.</summary>
    internal const string DishonorableDeathReason = "Dishonorable death";
    // True while GainHonor works out a rulebook change (a Dishonorable death's loss): the card and action in progress did not cause it, so every channel that asks "from a card's effect" /
    // "from the action's effects" stays out of it, the stronghold Honor-loss immunities included (2026-10-13 ruling).
    private bool _rulebookHonorChange;
    private void GainHonor(int playerId, int amount, string reason, CardInstance? source = null)
    {
        var outer = _rulebookHonorChange;
        _rulebookHonorChange = reason == DishonorableDeathReason;
        try { GainHonorCore(playerId, amount, reason, source); } finally { _rulebookHonorChange = outer; }
    }
    private void GainHonorCore(int playerId, int amount, string reason, CardInstance? source)
    {
        if (amount == 0) return;
        if (!_rulebookHonorChange && PromoEmperorHonorLossPrevented(playerId, amount, source)) return; // Den of Iniquity (issue #653, PromoEmperorHoldings.cs): "Other players will not lose Honor from your Spells."
        (playerId, amount) = ApplyHonorChangeHooks(HonorChangeStage.Standing, playerId, amount, reason, source); // P14 (HonorModifiers.cs): standing modifiers, so the window below opens with the modified amount
        if (amount == 0) return;
        OfferHonorChangeWindow(playerId, amount, reason); // Mendon's R4 ruling (issue #492): Rhetoric, Solid Defense and Shiro Chugo may be played on ANY Honor change from an action's effects.
        if (P3HonorGainNegated(playerId, amount, reason)) return; // Destructive Priorities (issue #503): "Negate the gain."
        if (DeedsAndWordsReduces(playerId, amount, reason)) return; // Deeds and Words (issue #622, IvoryHoldings.cs): "Reduce it to 0."
        // Emperor Edition remaining-cards pass, group C (issue #492): Rhetoric's redirect, Solid Defense's negation of losses and Rumors Travel's reduction of a gain (EmperorRemainingC.cs).
        (playerId, amount) = EmperorRemainingCHonorChange(playerId, amount, reason);
        if (amount == 0) return;
        // The Forgotten Temple (Forgotten Legacy pass, issue #467 batch 2): no gain at all, no loss from the player's own cards, and every other loss reduced to 1.
        amount = ForgottenTempleHonorChange(playerId, amount, reason, source);
        if (amount == 0) return;
        // A Dishonorable death's loss is the rulebook's, not a card's: "You do not lose Honor from cards you own" does not prevent it (official rulings on Fields of the Dead, The Fingers of Bone and The Shadowed Dojo; Mendon, 2026-10-13).
        if (amount < 0 && !_rulebookHonorChange && !CurrentActionUnnegatable() && HonorLossNegated(playerId, source)) // issue #501: an action whose effects will not be negated (The Heir's Wrath, Suitengu's Torch)
        {
            Emit("honorLossNegated", $"{Player(playerId).Name}'s Honor loss from {reason} was negated.", playerId, source?.Id);
            return;
        }
        // Unimpeachable Name (oracle-l5r-8989): "Reduce each Honor loss from its effects by 3
        // (minimum 0)" -- active only while resolving the one specific command this player's own
        // just-played Reaction targeted (see ResolveOrDeferUnimpeachableName), so every negative
        // Honor change of theirs during that single resolution is reduced, not just the first.
        if (amount < 0 && !_rulebookHonorChange && !CurrentActionUnnegatable() && HonorLossNegatedWithEffects(playerId))
        {
            Emit("honorLossNegated", $"{Player(playerId).Name}'s Honor loss from {reason} was negated with the action's effects.", playerId, source?.Id);
            return;
        }
        // Unimpeachable Name played in the window before this loss (Mendon's R4 ruling): the same reduction, for the action it was played on.
        if (amount < 0 && !_rulebookHonorChange && playerId == _state.UnimpeachablePlayerId && _state.UnimpeachableActionKey is { } unimpeachableKey && unimpeachableKey == CurrentActionKey())
        {
            var reducedByName = Math.Min(0, amount + 3);
            Emit("honorLossReduced", $"{Player(playerId).Name}'s Honor loss from {reason} was reduced by Unimpeachable Name{(reducedByName == 0 ? " to 0" : "")}.", playerId, source?.Id);
            amount = reducedByName;
            if (amount == 0) return;
        }
        if (!_rulebookHonorChange && RenyuReducesLoss(playerId, amount, reason)) return; // Kuni Renyu (issue #503, EmperorP6.cs): Honor losses from an action that targeted him are reduced to 0.
        if (amount < 0 && !_rulebookHonorChange && playerId == _state.HonorLossReductionPlayerId)
        {
            var reduced = Math.Min(0, amount + 3);
            if (reduced == 0) { Emit("honorLossReduced", $"{Player(playerId).Name}'s Honor loss from {reason} was reduced to 0 by Unimpeachable Name.", playerId, source?.Id); return; }
            Emit("honorLossReduced", $"{Player(playerId).Name}'s Honor loss from {reason} was reduced by Unimpeachable Name.", playerId, source?.Id);
            amount = reduced;
        }
        // Shiro Chugo (Emperor Edition Stronghold pass, issue #486 batch 2): "Reduce any Honor gain or loss from the action's effects by 2."
        (playerId, amount) = ApplyHonorChangeHooks(HonorChangeStage.Applied, playerId, amount, reason, source); // P14 (HonorModifiers.cs): Chugo's reduction is its first registrant
        amount = WlPersTornReducedHonorGain(playerId, amount, reason); // Shosuro Makiko - exp (issue #674, WlPersTorn.cs): "Reduce all Honor gains from it by 2"
        if (amount == 0) return;
        // Yamazaki - Experienced (The Shadow's Embrace pass, issue #476, batch 2): "Once per phase, before you lose exactly 1 Honor: Negate the loss." -- the loss as it would now happen.
        if (amount == -1 && !CurrentActionUnnegatable() && YamazakiNegatesLoss(playerId, reason)) return; // issue #501: an unnegatable action's loss is not negated
        // Ten Thousand Temples (issue #492, group F): "Before you lose Honor, destroy this card: Negate the Honor loss." -- asked before the loss happens (the command replays with the answer).
        if (amount < 0 && !CurrentActionUnnegatable() && TenThousandTemplesNegates(playerId, amount, reason)) return; // issue #501: nor by Ten Thousand Temples
        if (!_rulebookHonorChange) amount = TfNexusIncreasedLoss(playerId, amount, source); // Nexus of Lies (issue #622, TfHoldings.cs): the loss as it lands
        amount = TfStrategiesBHonorLoss(playerId, amount, reason, source); // Mercantile Conflict (TfStrategiesB.cs): "the next two Honor losses from your cards are increased by 1 each".
        var player = Player(playerId); player.Honor += amount;
        if (amount < 0) RecordHonorLoss(playerId); // An Insult Answered (issue #453, batch 2): the action that cost this player Honor.
        if (amount > 0) _state.CurrentActionHonorGained = true; // Moksha Patam (issue #453 batch 5): read when the action in progress is done.
        NoteBattleResolutionHonor(playerId, amount, reason); // On (The Shadow's Embrace pass, issue #476 batch 6).
        if (player.Honor <= -20) player.DishonorLossPending = true;
        Emit("honorChanged", $"{player.Name}: {amount:+#;-#;0} Honor ({reason}).", playerId, Amount: amount);
        if (amount < 0 && !_rulebookHonorChange) P4HonorLost(playerId, source); // No Time for Games (issue #503, EmperorP4More.cs): "after the next time (this turn) you lose Honor from another player's card effect".
        // Formal Apology (issue #259): checked after EVERY Honor change, gain or loss alike, since the
        // single shared choke point every Honor change already funnels through here means "may be
        // negated, modified, or redirected by cards that affect honor gains" (the user's own
        // clarification) is automatically true for this trigger's own +5 -- it is just another
        // GainHonor call, subject to whatever future hook applies to any other one. Re-entrant safe:
        // CheckFormalApology discards its own watch card before calling GainHonor for the +5, so the
        // recursive call here finds nothing left to trigger.
        CheckFormalApology(playerId);
    }
    // Formal Apology (issue #259): "After the next time this game your Honor is below its starting
    // value, you have a tea ceremony; gain 5 Honor" -- a one-shot watch, stateless beyond the card's
    // own presence in Zone.EventInPlay (no separate MatchState list needed, unlike Lady Matsu's
    // Rage's own multi-step delayed watch): the FIRST card of this Effect still sitting there for this
    // player is, by definition, still un-triggered, since triggering immediately discards it. Checked
    // both here (every Honor change) and once immediately when the card itself enters play (ResolveEvent),
    // since Honor could already be below starting the moment it resolves.
    private void CheckFormalApology(int playerId)
    {
        var watch = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == playerId && c.Zone == Zone.EventInPlay && Definition(c).Effect == "formal-apology");
        if (watch is null || Player(playerId).Honor >= Definition(Card(Player(playerId).StrongholdId)).StartingHonor) return;
        watch.Zone = Zone.DynastyDiscard;
        Emit("eventResolved", $"{Definition(watch).Name}: {Player(playerId).Name} has a tea ceremony.", playerId, watch.Id);
        GainHonor(playerId, 5, "Formal Apology", watch);
    }
    // Dishonor/Rehonor (core rules Glossary, issue #186): "if one or more personalities are the
    // performer of an action that would grant its controller Honor, the gain is negated and the
    // performer is restored to honorable status" instead -- e.g. Doji Shunya's own ability, Shamate
    // Keep's chosen performer. Distinct from a card's own explicit "rehonor him" text (e.g. No Time
    // For Games), which just calls Rehonor directly; this is the generic derived version for an
    // ability that does not spell it out itself.
    private void GainHonorFromPerformer(CardInstance performer, int amount, string reason)
    {
        if (performer.Dishonorable) { Rehonor(performer); return; }
        GainHonor(performer.OwnerId, amount, reason, performer);
    }
    // Kitsuki Horume (issue #210): "Choose a player. He gains or loses 1 Honor" -- a LOSS, or a
    // gain targeting someone other than Horume's own controller, is a plain Honor change on the
    // chosen player. A gain targeting Horume's OWN controller specifically is "an action performed
    // by Horume that would grant his own controller Honor" -- exactly GainHonorFromPerformer's own
    // shape (Doji Shunya/Shamate Keep's precedent): if Horume is Dishonorable, rehonor him instead
    // of granting the Honor.
    private void ApplyPlayerHonorChoice(CardInstance performer, int targetPlayerId, int amount)
    {
        if (amount > 0 && targetPlayerId == performer.OwnerId) GainHonorFromPerformer(performer, amount, Definition(performer).Name);
        else GainHonor(targetPlayerId, amount, Definition(performer).Name, performer);
    }
    private void Win(int winner, string reason) { _state.WinnerId = winner; _state.VictoryReason = reason; _state.Phase = Phase.Finished; _state.PendingChoice = null; Emit("matchFinished", $"{Player(winner).Name} wins: {reason}.", winner); }
    private void SetPhase(Phase phase, int priority)
    {
        var phaseFrom = _state.Phase;
        if (PhaseFamily(phase) != PhaseFamily(_state.Phase)) ApplyDelayedEffectsAtPhaseEnd(_state.Phase); // The shared delay mechanism (issue #492, EmperorRulingR2.cs): what waits for the end of the phase that is ending.
        OnPhaseBoundary(_state.Phase, phase); // On (The Shadow's Embrace pass, issue #476 batch 6): the end of one phase and the start of the next.
        ResetMechDPhaseRecords(_state.Phase, phase); // Issue #501, mechanics group D (EmperorMechD.cs): the per-phase cards-into-hand records.
        // War Weariness (issue #259): reset only on the three phases that genuinely begin once per
        // turn -- NOT Phase.Battle, which this same function re-enters once per battlefield (see
        // SelectBattle); the user's own clarification is that the whole Attack-Declaration-through-
        // battle-resolution stretch counts as a single "battle phase" for this effect, so Phase.
        // AttackDeclaration's own once-per-turn entry (Pass, Phase.Action case) is the correct reset
        // point for it, not Phase.Battle itself.
        if (phase is Phase.Action or Phase.AttackDeclaration or Phase.Dynasty) _state.WarWearinessBowedThisPhase = false;
        PruneForcedPasses(phase);
        // Wave Man (issue #279): "After an Attack Phase ends: Straighten this unit" -- a trait (no
        // Action:/Reaction: prefix), so mandatory and automatic, never an optional choice, same
        // convention as EntryHonorLoss/Destined above. The Attack Phase ends in exactly two places --
        // DeclineAttackPhase (a player declined to attack outright: no battlefield was ever created) and
        // FinishBattleReturnHomeAndTransition (the last battlefield resolved) -- and BOTH, and only
        // those, ever call SetPhase(Phase.Dynasty, ...) (grep confirms it; CreateEconomyScenario's own
        // direct `state.Phase=Phase.Dynasty` field assignment bypasses SetPhase entirely, so it never
        // reaches here). One check here therefore covers both paths without needing two separate hooks.
        if (_state.Phase == Phase.Action && phase != Phase.Action) AdvanceStraightenHolds(StraightenPoint.ActionPhaseEnds);
        // "until the phase ends" holds (issue #453 batch 5, Ronin Ambushers): the whole Attack-declaration-through-battle stretch is one phase (the War Weariness convention above).
        if (PhaseFamily(phase) != PhaseFamily(_state.Phase)) AdvanceStraightenHolds(StraightenPoint.PhaseEnds);
        ResolveShadowsEmbraceBatch1PhaseEnd(phase); // The Shadow's Embrace pass (issue #476), batch 1: Bayushi Misaki's "before this phase ends".
        PromoPhaseEnds(phase); // Promotional-Emperor pass (issue #653, PromoEmperorFate.cs): Elemental Adroitness's "before this phase ends, put this Strategy into your hand".
        if (phase == Phase.Dynasty) foreach (var p in _state.Players) p.DynastyActionTaken = false; // a new Dynasty Phase: nothing bought/discarded yet
        if (phase == Phase.Dynasty) DragonsBreathHonor();
        // Sentinel Spirit (issue #453, batch 2): "after the next Dynasty Phase begins".
        if (phase == Phase.Dynasty) ResolveSentinelWatches();
        if (phase == Phase.Dynasty && !DynastyBeforeAttack()) StraightenWaveMen(); // issue #667 (PhaseOrder.cs): with the Dynasty Phase first it is not the end of the Attack Phase
        NotePhaseOrder(phase); // issue #667: the phase-order marker
        _state.Phase = phase; _state.PriorityPlayerId = priority; _state.ConsecutivePasses = 0; Emit("phaseChanged", $"Phase: {phase}.");
        FirePhaseBegins(phaseFrom, phase); // issue #816 (Wave816.cs): "after each phase begins" traits
        // The shared delay mechanism (issue #492, EmperorRulingR2.cs): what waits for the next Dynasty Phase (or End Phase) to begin is applied as it does.
        if (phase == Phase.Dynasty) ApplyDelayedEffects(DelayUntil.NextDynastyPhase);
        if (phase == Phase.End) ApplyDelayedEffects(DelayUntil.EndPhaseBegins, EndPhaseIsOwners); // issue #498: "YOUR End Phase" -- only the active player's delayed effects are due
        if (phase == Phase.Dynasty) Wave821OpenDynastyWindow(); // issue #821 (Wave821.cs): the Governor's Estate's Reaction, after everything else that begins with the phase
    }
    private void Emit(string type, string message, int? playerId = null, string? cardId = null, int? Amount = null) => _state.Events.Add(new(_state.Events.Count + 1, type, message, playerId, cardId, Amount));
    private PlayerState Player(int id) => _state.Players[id];
    private CardInstance Card(string? id) { Require(id is not null && _state.Cards.ContainsKey(id), "Unknown card."); return _state.Cards[id!]; }
    private static CardDefinition Definition(CardInstance card) => card.Printed ?? CardCatalog.Get(card.DefinitionId);
    private static void Require(bool condition, string message) { if (!condition) throw new RuleViolation(message); }
    private sealed class RuleViolation(string message) : Exception(message);
    // Issue #863: a deep copy equal to the JSON round trip below (StateCloner.cs reproduces its semantics from the serializer's own contract), several times faster.
    internal static T Copy<T>(T value) => ReferenceJsonCopyForTests ? CopyViaJson(value) : StateCloner.Clone(value)!;
    /// <summary>Tests only: run this thread's engines on the original JSON copy, the reference StateClonerTests plays the fast copy against.</summary>
    [ThreadStatic] internal static bool ReferenceJsonCopyForTests;
    /// <summary>The original copy, kept as the reference StateClonerTests compares Copy against.</summary>
    internal static T CopyViaJson<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    // Card ids are handed out in a seeded permutation, not deck-list order: a face-down card's id (offered in a target list, an action
    // command or a view) must not reveal which deck-list entry it is. A separate generator keeps the setup shuffles' sequence unchanged.
    // Cards are added in deck-list order, and every "for each card in the match" enumeration (target lists, default commands, first-match
    // lookups) walks the dictionary in insertion order -- which would put deck-list position back into option order. Rebuild it in
    // opaque-id order so enumeration order carries no setup information (issue #611). Setup shuffles use the deck lists, not this order.
    private static void OrderCardsById(MatchState state) =>
        state.Cards = state.Cards.OrderBy(c => c.Key, StringComparer.Ordinal).ToDictionary(c => c.Key, c => c.Value);
    private static string[] OpaqueCardIds(ulong seed)
    {
        var ids = Enumerable.Range(1, 2000).Select(n => $"card-{n:D4}").ToArray();
        new StableRandom(seed ^ 0xA5A5C0DE5EEDF00DUL).Shuffle(ids);
        return ids;
    }
    private sealed class StableRandom(ulong seed)
    {
        private ulong _value = seed;
        // The generator's whole state is this one value (deck search, issue #312): persisted in
        // MatchState.RngState so a mid-game shuffle continues the exact sequence the setup shuffles started.
        public ulong State => _value;
        private ulong Next()
        {
            // SplitMix64; owned algorithm makes shuffles stable across .NET versions.
            var z = (_value += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        public void Shuffle<T>(IList<T> cards)
        {
            for (var i = cards.Count - 1; i > 0; i--) { var j = (int)(Next() % (ulong)(i + 1)); (cards[i], cards[j]) = (cards[j], cards[i]); }
        }
    }
}
