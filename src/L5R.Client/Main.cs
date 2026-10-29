using Godot;
using L5R.Core;
using L5R.AI;
using System.Collections.Concurrent;
using System.Text.Json;

namespace L5R.Client;

public partial class Main : Node
{
    private readonly MatchSession _session = new();
    private readonly ConcurrentQueue<Action> _uiQueue = new();
    private Control _root = null!;
    private Control _board = null!, _hand = null!, _unitDetail = null!;
    private GoldPaymentMeter _goldMeter = null!;
    private VBoxContainer _inspector = null!, _phaseActions = null!, _decisionActions = null!;
    private PhaseTrack _timeline = null!;
    private Control _clanHud = null!, _pileHud = null!;
    private Label _status = null!, _notice = null!, _seatLabel = null!, _guide = null!;
    private TableArtStage _table = null!;
    private CardView? _selected;
    private ActionOption? _chosen;
    private readonly HashSet<string> _sources = [];
    private readonly List<CardTile> _tiles = [];
    private readonly Dictionary<string, bool> _previousBows = [];
    private readonly Dictionary<string, bool> _currentBows = [];
    private long _bowVersion = -1;
    private long _displayedVersion = -1;
    private ulong _bowChangedAt;
    private readonly Dictionary<string, int> _assignments = [];
    private readonly HashSet<string> _expandedUnits = [];
    private string? _assignmentUnit;
    private BoardIntentOverlay _intentOverlay = null!;
    private readonly Dictionary<(int Owner, int Province), Button> _provinceButtons = [];
    private readonly Dictionary<int, Button> _laneSummaries = [];
    private bool _autoOpponent = true, _reducedMotion;
    private BotContext? _botMemory;
    private string? _botMemoryKey;
    private double _opponentClock, _captureClock;
    private string? _capturePath;
    private string _captureMode = "table";
    private bool _capturePrepared;
    private int _captureFrames, _filmFrames;
    private bool _fullscreenResumeSubmitted;
    private bool _pileHoverSent;
    private string? _hoverProbeCard;
    // Issue #975: the probe aims at the card only once it has stopped moving (a card still settling made the pointer land beside it) and keeps the pointer on it until the preview opens, within a limit.
    private Vector2 _hoverProbeLastPoint; private int _hoverProbeStillFrames, _hoverProbeAttempts, _hoverProbeWaitedForFocus;
    private bool HoverProbeFinished => _hoverProbeCard is not null && (CardTile.LastPreviewCardId == _hoverProbeCard || _hoverProbeAttempts >= 240);
    private void DriveHoverProbe()
    {
        var tile = _tiles.FirstOrDefault(t => t.Card.Id == (_hoverProbeCard ?? _selected?.Id));
        if (tile is null) return;
        // A tooltip, which carries the card preview, is shown only to a window that has focus: take it and wait for it, as the other native input probes do.
        if (!GetWindow().HasFocus()) { GetWindow().GrabFocus(); _hoverProbeStillFrames = 0; _hoverProbeWaitedForFocus++; return; }
        var point = ScreenCenter(tile);
        if (_hoverProbeCard is null)
        {
            _hoverProbeStillFrames = point.DistanceTo(_hoverProbeLastPoint) < .5f ? _hoverProbeStillFrames + 1 : 0; _hoverProbeLastPoint = point;
            if (_hoverProbeStillFrames < 12) return; // twelve consecutive frames at the same place
            _hoverProbeCard = tile.Card.Id; _hoverProbeVersion = View.Version; _hoverProbeCommand = _chosen?.Command;
            CardTile.LastPreviewCardId = null;
        }
        _hoverProbeAttempts++;
        GetViewport().WarpMouse(point);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One }, true);
        if (_hoverProbeAttempts == 1) GD.Print($"HOVER_PROBE: {point}, control={GetViewport().GuiGetHoveredControl()?.GetPath()}");
    }
    private long _hoverProbeVersion;
    private GameCommand? _hoverProbeCommand;
    private Button? _paymentConfirm;
    private RoomSeat? _savedSeat;
    private string _savedServer = "http://localhost:5210";
    private string _profile = "default";
    private string SeatPath => ProjectSettings.GlobalizePath($"user://private-seat-{_profile}.json");
    /// <summary>Where saved decks live -- plain files in DeckFormat.cs's text format (see #24),
    /// so they can be hand-edited or dropped into another player's local install.</summary>
    private string DecksDirectory => ProjectSettings.GlobalizePath("user://Decks");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private MatchView View => _battlefieldReviewView ?? _provinceReviewView ?? (_presentationSnapshot ?? _session.Snapshot)!.View;
    private ActionOption[] Actions => PresentationBusy || _playHeld || _battlefieldReviewView is not null || _provinceReviewView is not null || View.PendingProvinceOrder is not null ? [] : ReadableActions(_session.Snapshot!.Actions);
    private PlayerView Self => View.Players.Single(p => p.Id == View.ViewerId);

    public override void _Ready()
    {
        CrashReports.Install(CrashContext); // issue #905: a local crash report for a runtime-level failure; nothing is sent
        if (OS.GetCmdlineUserArgs().Contains("--write-crash-report")) { GD.Print("CRASH_REPORT: " + CrashReports.Write(new InvalidOperationException("crash report probe"))); SetProcess(false); SetProcessInput(false); SetProcessUnhandledInput(false); SetProcessUnhandledKeyInput(false); GetTree().Quit(); return; } // a check that the report can be written here
        GetWindow().MinSize = new Vector2I(1120, 700);
        PreloadedDecks.OpenStore(); // the shipped decks are in My Decks from the first launch (issue #810)
        GetTree().TreeChanged += () => _tooltipsDirty = true;
        _session.Changed += () =>
        {
            // Capture every permitted snapshot, even when several packets arrive within one rendered frame.
            var snapshot = _session.Snapshot; var generation = _session.Generation;
            _uiQueue.Enqueue(() =>
            {
                if (generation != _session.Generation) return;
                ObserveDecisionSession(snapshot);
                if (_matchDuelGeneration != generation) { _matchDuelFlow.Reset(); _matchDuelGeneration = generation; }
                _matchDuelFlow.Observe(snapshot); Render(); if (snapshot is not null) AutoProclaim(snapshot);
            });
        };
        _session.Notice += message => _uiQueue.Enqueue(() => { SetNotice(message); if (BarNotice.TryParse(message) is { } bar) ShowBarred(bar); });
        _session.SeatCreated += (seat, server) => _uiQueue.Enqueue(() => SaveSeat(seat, server));
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--capture=")) _capturePath = arg[10..];
            if (arg.StartsWith("--capture-mode=")) _captureMode = arg[15..];
            if (arg.StartsWith("--perf=")) _perfScenario = arg[7..];
            if (arg.StartsWith("--profile=") && arg[10..].Length is > 0 and <= 32 && arg[10..].All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) _profile = arg[10..];
        }
        GetWindow().Mode = _captureMode.Contains("fullscreen", StringComparison.Ordinal) ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Windowed;
        if (_capturePath is not null) GetWindow().GrabFocus();
        if (_captureMode == "battle-reference" && _capturePath is not null)
        { SetProcessInput(false); AddChild(new BattleArtReview(_capturePath)); SetProcess(false); return; }
        if (_captureMode == "support-reference" && _capturePath is not null)
        { SetProcessInput(false); AddChild(new SupportArtReview(_capturePath)); SetProcess(false); return; }
        if (_captureMode == "battle-escalation-reference" && _capturePath is not null)
        { SetProcessInput(false); AddChild(new BattleEscalationReview(_capturePath)); SetProcess(false); return; }
        if (_captureMode == "threshold-reference" && _capturePath is not null)
        { SetProcessInput(false); SetProcessUnhandledInput(false); SetProcessUnhandledKeyInput(false); AddChild(new ThresholdArtReview(_capturePath)); SetProcess(false); return; }
        if (_captureMode is "catalogue-filters-review" or "deck-filters-review" or "my-collection-review" or "deck-collection-review" || OS.GetCmdlineUserArgs().Any(a => a is "--catalogue-filters-review" or "--deck-filters-review" or "--my-collection-review" or "--deck-collection-review"))
        {
            SetProcessInput(false); SetProcessUnhandledInput(false); SetProcessUnhandledKeyInput(false); SetProcess(false);
            var review = new CatalogueControlsReview(); AddChild(review); review.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); return;
        }
        LoadSeat();
        _table = new TableArtStage { ReviewMode = false }; AddChild(_table);
        BuildShell();
        InitializeFrontend();
        if (_perfScenario is not null) { StartPerf(_perfScenario); return; }
        if (OS.GetCmdlineUserArgs().Contains("--gameplay-controls-review") || _captureMode is "gameplay-controls-review" or "gameplay-controls-choices-review" or "gameplay-controls-optional-review")
        {
            var startupCapture = _capturePath; _capturePath = null;
            var checks = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--controls-checks=", StringComparison.Ordinal))?[18..];
            _ = StartGameplayControlsReview(checks, startupCapture); return;
        }
        if (_capturePath is not null && _captureMode.StartsWith("published-interactions-", StringComparison.Ordinal))
        {
            var review = new InteractionReview(this, _captureMode, _capturePath);
            _capturePath = null; AddChild(review); return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--tutorial-review")) { StartTutorialReview(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--battle-values-review") || _captureMode == "battle-values-review")
        { StartBattleValuesReview(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--open-battlefield-review") || _captureMode is "open-battlefield-review" or "open-battlefield-correction-review")
        {
            var startupCapture = _capturePath; _capturePath = null; StartOpenBattlefieldReview();
            if (startupCapture is not null) Callable.From(() => { _ = CaptureOpenBattlefieldStartup(startupCapture); }).CallDeferred();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--province-review") || _captureMode == "province-review")
        {
            var startupCapture = _capturePath; _capturePath = null; StartProvinceReview();
            if (startupCapture is not null) Callable.From(() => { _ = CaptureProvinceStartup(startupCapture); }).CallDeferred();
            return;
        }
        if (_captureMode.StartsWith("duel-followup-", StringComparison.Ordinal) || OS.GetCmdlineUserArgs().Contains("--duel-match-review")) { StartMatchDuelCapture(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--duel-transition-review")) { StartTransitionReview(); return; }
        if (_capturePath is null || _captureMode.StartsWith("menu", StringComparison.Ordinal)) return;
        var published = _capturePath is null || _captureMode.StartsWith("published", StringComparison.Ordinal);
        if (_captureMode.StartsWith("gold-", StringComparison.Ordinal)) _session.StartShowcase(_captureMode.Split('-')[1]);
        else _session.StartPractice(!published, published, _capturePath is null ? 0 : 1);
        Render();
        SetNotice(published ? "Published Lion vs Crane · choose Practice to change Clan or scenario." : "Economy practice · choose a province card to preview its payment.");
    }

    public override void _Process(double delta)
    {
        while (_uiQueue.TryDequeue(out var update)) update();
        if (_perfScenario is not null) ProcessPerf(delta);
        if (_transitionReview) { try { ProcessTransitionReview(delta); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } return; }
        if (_captureMode.StartsWith("menu", StringComparison.Ordinal) && _capturePath is not null) { try { ProcessMenuCapture(delta); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } return; }
        if (MatchDuelOpen) { ProcessMatchDuel(delta); ProcessMatchDuelCapture(delta); return; }
        if (ProcessMatchDuelCapture(delta)) return;
        LayoutBattlefieldHeaders();
        if (MenuVisible || PracticeVisible && _capturePath is null) return;
        PulseTutorialPointers(delta); LayoutTutorialCoach();
        ProcessBattlePresentation(delta);
        if (_session.Snapshot is not null) SyncBloodCourts();
        ProcessTableCamera(delta);
        ProcessShownCards(delta);
        LayoutProvinceReview();
        LayoutOpenBattlefieldReview();
        LayoutBattleValuesReview();
        if (_session.Snapshot is not null && _matSections.ReducedMotion != _reducedMotion) SyncMatSections();
        ExpireBattleResolution();
        foreach (var tracker in _sidebarHeader.GetChildren().OfType<BattleTracker>()) tracker.ReducedMotion = _reducedMotion;
        foreach (var plaque in _clanHud.GetChildren().OfType<ClanPlaque>()) plaque.ReducedMotion = _reducedMotion;
        _fullscreenGate.Visible = false;
        if (_autoOpponent && !PresentationBusy && !PlayShowcaseBusy && !_session.Online && _capturePath is null) // issue #630: the practice opponent waits for a played card to finish showing
        {
            _opponentClock += delta;
            if (_opponentClock > (_reducedMotion ? .12 : .7))
            {
                _opponentClock = 0;
                // Issue #642: the bot keeps one memory per match (what it has seen played, what the engine refused) and chooses again after a refusal.
                if (_botMemory is null || _botMemoryKey != _session.MatchKey) { _botMemory = new BotContext(); _botMemoryKey = _session.MatchKey; }
                var botMemory = _botMemory;
                _session.StepPracticeOpponent((view, actions) => view.PendingDuel is not null ? DuelOpponent.Choose(view, actions) : TrainingOpponent.Choose(view, actions, botMemory).Command,
                    (command, error, version) => botMemory.NoteRejection(version, command, error));
            }
            // No delay: this only fires when declining is the player's sole legal option, so there is
            // nothing for them to weigh — unlike the opponent's move above, it needs no "thinking" pause.
            _session.StepPracticeReaction();
        }
        if (_capturePath is not null && _captureMode.StartsWith("published-decisions", StringComparison.Ordinal)) { ProcessDecisionCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-reaction-flow", StringComparison.Ordinal)) { ProcessReactionFlowCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-shown", StringComparison.Ordinal)) { ProcessShownCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-action-lock", StringComparison.Ordinal)) { ProcessActionLockCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-direct-play", StringComparison.Ordinal)) { ProcessDirectPlayCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-table-click", StringComparison.Ordinal)) { ProcessTableClickCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-drag", StringComparison.Ordinal)) { ProcessDragCapture(delta); return; } // issue #993
        if (_capturePath is not null && _captureMode.StartsWith("published-paused-animation", StringComparison.Ordinal)) { ProcessPausedAnimationCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-card-motion", StringComparison.Ordinal)) { ProcessCardMotionCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-attach-order", StringComparison.Ordinal)) { ProcessAttachOrderCapture(delta); return; } // issue #1014
        if (_capturePath is not null && _captureMode.StartsWith("published-province-target", StringComparison.Ordinal)) { ProcessProvinceTargetCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-search", StringComparison.Ordinal)) { ProcessSearchCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-created-copy", StringComparison.Ordinal)) { ProcessCreatedCopyCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-battle-sequence", StringComparison.Ordinal)) { ProcessBattleSequenceCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-multi-targets", StringComparison.Ordinal)) { ProcessMultiCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-top-cards", StringComparison.Ordinal)) { ProcessLookCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-opponent-payment", StringComparison.Ordinal)) { ProcessOpponentPaymentCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-either-or", StringComparison.Ordinal)) { ProcessEitherCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-copy", StringComparison.Ordinal)) { ProcessCopyCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-movement", StringComparison.Ordinal)) { ProcessMovementCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-attack-targets", StringComparison.Ordinal)) { ProcessAttackTargetCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-supports", StringComparison.Ordinal)) { ProcessSupportCapture(delta); return; }
        if (_capturePath is not null && _captureMode == "published-ink-film") { ProcessInkFilm(); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-stabilization", StringComparison.Ordinal)) { ProcessStabilizationCapture(delta); return; }
        if (_capturePath is not null && _captureMode.StartsWith("published-events", StringComparison.Ordinal)) { ProcessEventCapture(delta); return; }
        if (_capturePath is not null && _captureMode == "published-tactile") { ProcessTactileCapture(); return; }
        if (_capturePath is not null)
        {
            _captureClock += delta;
            if (!_capturePrepared && _captureClock > 1.2)
            {
                _capturePrepared = true;
                _captureClock = 0;
                if (_captureMode.StartsWith("published-home", StringComparison.Ordinal)) PreparePersistentHomeCapture();
                if (_captureMode.StartsWith("published-grants", StringComparison.Ordinal)) _ = PreparePermanentGrantCapture();
                if (_captureMode.StartsWith("published-fallen", StringComparison.Ordinal)) _ = PrepareFallenCapture();
                if (_captureMode is "published-choice" or "published-choice-confirm")
                {
                    for (var fixtureAttempt = 0; fixtureAttempt < 6 && View.PendingChoice?.Type != "shinden"; fixtureAttempt++)
                    {
                        if (fixtureAttempt > 0)
                        {
                            GD.Print("CHOICE_SETUP: practice ended without a Shinden decision; searching another real match before testing the UI.");
                            _session.StartPractice(false, true, 1);
                        }
                        var steps = 0;
                        while (View.PendingChoice?.Type != "shinden" && View.WinnerId is null && steps++ < 800)
                        {
                            _session.FollowPracticePriority();
                            var command = TrainingOpponent.Choose(View, Actions).Command ?? throw new InvalidOperationException("Capture opponent had no action.");
                            var version = View.Version;
                            _session.Submit(command).GetAwaiter().GetResult();
                            if (View.Version == version) throw new InvalidOperationException("Capture action was rejected.");
                        }
                    }
                    _session.FollowPracticePriority(); _uiQueue.Clear(); _selected = null; _chosen = null; Render();
                    if (View.PendingChoice?.Type != "shinden") throw new InvalidOperationException("Capture did not reach a Shinden decision.");
                    if (_captureMode == "published-choice-confirm")
                    {
                        var version = View.Version; var craneHonor = View.Players[0].Honor;
                        var button = _phaseActions.GetChildren().OfType<Button>().Single(b => b.Text == "Allow opponent to gain 1 Honor");
                        button.EmitSignal(Button.SignalName.Pressed);
                        if (View.Version != version + 1 || View.PendingChoice is not null || View.Players[0].Honor != craneHonor + 1)
                            throw new InvalidOperationException("Shinden choice UI smoke failed.");
                        GD.Print("UI_SMOKE_PASS: real published match reached Shinden; opponent's Honor choice resolved through the button handler.");
                    }
                }
                if (_captureMode is "published-refresh" or "published-refresh-confirm")
                {
                    var option = Actions.First(a => a.Enabled && a.Command?.AbilityId == "border-first");
                    _selected = FindCard(option.CardId!); Choose(option);
                    if (_captureMode == "published-refresh-confirm")
                    {
                        var before = Self.Provinces[0].Card!.Id;
                        SelectCard(Self.Provinces[0].Card!); SelectCard(Self.Provinces[0].Card!); SelectCard(Self.Provinces[0].Card!); // select, deselect, select again
                        _decisionActions.GetChildren().OfType<Button>().First().EmitSignal(Button.SignalName.Pressed);
                        if (View.Version != 1 || View.PendingChoice is not null || Self.Provinces[0].Card!.Id == before)
                            throw new InvalidOperationException("Published province refresh UI smoke failed.");
                        GD.Print("UI_SMOKE_PASS: published Border Keep selected and refreshed through the real controls.");
                    }
                }
                if (_captureMode.StartsWith("published-board", StringComparison.Ordinal))
                {
                    try { PrepareBoardCapture(); }
                    catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); _capturePath = null; return; }
                }
                if (_captureMode.StartsWith("gold-", StringComparison.Ordinal))
                {
                    try { PrepareGoldPaymentCapture(); }
                    catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); _capturePath = null; return; }
                }
                if (_captureMode == "library") LibraryDialog.Show(this);
                if (_captureMode == "published-rules") ShowRules();
                if (_captureMode == "published-lacquer-menu") ShowTableMenu();
                if (_captureMode == "published-lacquer-practice") ShowPractice();
                if (_captureMode == "published-lacquer-private") ShowOnline();
                if (_captureMode == "published-lacquer-coverage") ShowCoverage();
                if (_captureMode == "published-lacquer-history") ShowHistory();
                if (_captureMode == "published-lacquer-pile") ShowPile(View.ViewerId, Zone.Dead);
                if (_captureMode == "published-lacquer-confirm") Confirm("Leave this table?", "Return to the main menu? Your current practice will end.", () => { });
                if (_captureMode == "published-lacquer-tooltip")
                    Place(_root, ReadingTooltip.Create(this, "Bowed / unbowed\nA sideways card is bowed. Each attachment keeps its own bowed state."), 600, 260, 460, 180);
                if (_captureMode == "published-lacquer-card")
                    Place(_root, CardPreview.Create(View.Players.SelectMany(p => p.Home.Concat(p.Provinces.Where(x => x.Card is not null).Select(x => x.Card!))).First(c => c.Definition is not null), "Your hand", null), 430, 200, 750, 558);
                if (_captureMode is "payment" or "purchase")
                {
                    var option = Actions.First(a => a.Type == "purchase" && a.Enabled && a.Quote is { ClanDiscount: false, ClanAllegiance: false, ProclaimEligible: true });
                    _selected = FindCard(option.CardId!);
                    Choose(option);
                    if (_captureMode == "purchase")
                    {
                        foreach (var id in QuoteFor(option)!.Payments[0].SourceIds) SelectCard(FindCard(PaymentMethods(QuoteFor(option)!).First(m => m.Id == id).CardId)!); // the player bows holdings; nothing is pre-selected (#731)
                        _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
                        if (View.PendingChoice?.Type != "proclaim") throw new InvalidOperationException("UI purchase smoke failed: no Proclaim reaction.");
                        GD.Print("UI_SMOKE_PASS: confirmed payment through button handler; Proclaim reaction opened.");
                    }
                }
            }
            if (_capturePrepared) _captureFrames++;
            if (_captureMode.Contains("clarity", StringComparison.Ordinal)) MaintainClarityPointer();
            if (_captureMode.StartsWith("published-grants", StringComparison.Ordinal)) MaintainGrantPointer();
            if (_capturePrepared && _captureMode.Contains("camera", StringComparison.Ordinal)) ProcessCameraCapture();
            if (_capturePath is null) return;
            if (_capturePrepared && _captureMode.EndsWith("piletip", StringComparison.Ordinal) && _captureClock > .4 && !_pileHoverSent)
            {
                _pileHoverSent = true; ReadingTooltip.LastText = null;
                var pile = _pileHud.GetChildren().OfType<TablePile>().Single(p => p.OwnerId == View.ViewerId && p.PileZone == Zone.FateDeck);
                var point = ScreenCenter(pile);
                GetViewport().WarpMouse(point);
                GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One }, true);
            }
            if (_capturePrepared && _captureMode.EndsWith("gate-resume", StringComparison.Ordinal) && _captureClock > .6 && !_fullscreenResumeSubmitted)
            {
                _fullscreenResumeSubmitted = true;
                _UnhandledKeyInput(new InputEventKey { Keycode = Key.F11, Pressed = true });
            }
            if (_capturePrepared && _captureMode.Contains("candle", StringComparison.Ordinal) && _captureClock >= .5 && _filmFrames < 72)
            {
                // Record the real renderer at evenly sampled light phases; match state
                // and camera stay unchanged. No offline image compositing is involved.
                _table.PresentLight(_filmFrames / 12f, !_reducedMotion); _timeline.PresentLight(_filmFrames / 12f, !_reducedMotion);
                RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
                var folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_capturePath)!, "candle-frames");
                System.IO.Directory.CreateDirectory(folder);
                var result = GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, $"frame-{_filmFrames:000}.png"));
                if (result != Error.Ok) throw new InvalidOperationException("Could not save candle animation frame.");
                _filmFrames++;
            }

            if (_capturePrepared && _captureClock > .6 && _captureMode.EndsWith("hover", StringComparison.Ordinal) && !HoverProbeFinished)
                DriveHoverProbe();
            // These fixtures review settled menus, after the startup turn announcement clears.
            if ((_captureMode.StartsWith("published-lacquer-", StringComparison.Ordinal) || _captureMode.StartsWith("published-home", StringComparison.Ordinal) || _captureMode == "published-rules" || _captureMode.StartsWith("published-grants", StringComparison.Ordinal)) && _turnBloom.Playing) return;
            if (_capturePrepared && (!_captureMode.EndsWith("hover", StringComparison.Ordinal) || HoverProbeFinished) && (!_captureMode.StartsWith("published-fallen", StringComparison.Ordinal) || _fallenProbeDone) && (!_captureMode.StartsWith("published-grants", StringComparison.Ordinal) || _grantProbeDone) && (!_captureMode.Contains("courts", StringComparison.Ordinal) || _battleProbeDone) && (!_captureMode.Contains("ink", StringComparison.Ordinal) || _inkProbeDone) && (!_captureMode.Contains("deselect", StringComparison.Ordinal) || _deselectProbeDone) && (!_captureMode.Contains("standards", StringComparison.Ordinal) || _standardProbeDone) && (!_captureMode.Contains("hudicons", StringComparison.Ordinal) || _hudProbeDone) && (!_captureMode.Contains("clarity", StringComparison.Ordinal) || _clarityInputDone && _captureClock > _clarityReadyAt + 1) && (!_captureMode.Contains("camera", StringComparison.Ordinal) || _cameraProbeDone) && (!_captureMode.Contains("candle", StringComparison.Ordinal) || _filmFrames >= 72) && _captureClock > (_captureMode.Contains("gate", StringComparison.Ordinal) ? 3 : (_captureMode.Contains("clarity", StringComparison.Ordinal) || _captureMode.EndsWith("hover", StringComparison.Ordinal) || _captureMode.EndsWith("piletip", StringComparison.Ordinal)) ? 2 : 1) && _captureFrames >= 15)
            {
                if (_captureMode.StartsWith("published-board", StringComparison.Ordinal))
                {
                    try { ValidateBoardCapture(); }
                    catch (Exception e) { GD.PushError(e.Message); GetViewport().GetTexture().GetImage().SavePng(_capturePath); GetTree().Quit(1); _capturePath = null; return; }
                }
                // An occluded macOS window may stop presenting while process frames continue.
                // Capture an explicitly drawn, synchronized frame rather than its previous texture.
                RenderingServer.ForceDraw(false);
                RenderingServer.ForceSync();
                var error = GetViewport().GetTexture().GetImage().SavePng(_capturePath);
                try { ValidateLacquerMenus(); }
                catch (Exception e) { GD.PushError(e.Message); error = Error.Failed; }
                GD.Print($"CAPTURE_RESULT: {error} {_capturePath}");
                GetTree().Quit(error == Error.Ok ? 0 : 1);
                _capturePath = null;
            }
        }
    }

    private Vector2 DesignSize => new(_layoutWidth, 1080);
    private float _layoutWidth = 1920;
    private float BoardWidth => _layoutWidth - 238 - SidebarWidth - BattleStripReserve - 44;
    private const float SidebarWidth = 400;
    private const float HandCardSide = 284;
    private HBoxContainer _menu = null!;
    private Control _inspectorPanel = null!, _fullscreenGate = null!;
    private bool IsFullscreen => GetWindow().Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen;

    private void FitPresentation()
    {
        if (_transitionReview && _transitionRoom is not null) return;
        var available = GetViewport().GetVisibleRect().Size;
        var scale = available.Y / 1080f;
        // Root controls use an explicit scale, so rasterize system fonts at the
        // matching density instead of magnifying their 1080p glyph textures.
        ((FontFile)Palette.Body).Oversampling = Math.Max(1, scale);
        Palette.HeadingFace.Oversampling = Math.Max(1, scale);
        _layoutWidth = available.X / Math.Max(.1f, scale);
        _root.Size = DesignSize;
        _root.Scale = Vector2.One * scale;
        _root.Position = Vector2.Zero;
        if (_board is null) return;
        FitTableViewport();
        _hand.Position = new Vector2(250, 1060 - HandCardSide); _hand.Size = new Vector2(BoardWidth - 12, HandCardSide);
        PlaceGoldMeter();
        _inspectorPanel.Position = new Vector2(_layoutWidth - SidebarWidth - 22, 94);
        _inspectorPanel.Size = new Vector2(SidebarWidth, 960);
        FitShownCards();
        FitBattleSequence();
        FitSearchPanel();
        FitLookPanel();
        FitLookReviewControls();
        FitMultiReviewControls();
        FitEitherReviewControls();
        FitOpponentPaymentReviewControls();
        FitCopyPanel();
        FitCopyReviewControls();
        FitMovementReviewControls();
        FitAttackReviewControls();
        FitEventDecisions(); FitCandidateChoices();
        _status.Visible = false;
        _timeline.Position = new Vector2(344, 12); _timeline.Size = new Vector2(PhaseTrack.WidthForFrame(_layoutWidth - SidebarWidth - 30 - 344), 100);
        _menu.Position = new Vector2(_layoutWidth - 300, 28); _menu.Size = new Vector2(275, 40);
        _notice.Position = new Vector2(_layoutWidth - SidebarWidth - 22, 1060); _notice.Size = new Vector2(SidebarWidth, 19);
        _unitDetail.Size = DesignSize;
        _clanHud.Size = DesignSize;
        _fullscreenGate.Size = DesignSize;
        if (_session.Snapshot is not null) RenderBoard();
    }

    private static T Place<T>(Node parent, T child, float x, float y, float width, float height) where T : Control
    {
        parent.AddChild(child);
        child.Position = new Vector2(x, y); child.Size = new Vector2(width, height);
        return child;
    }

    private VBoxContainer _sidebarHeader = null!;

    private void BuildShell()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        _root = new Control { Theme = Palette.MakeTheme(), Size = DesignSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(_root);
        BuildTableViewport();
        _status = Place(_root, Palette.Text("", 17, Palette.PaperDim), 295, 15, 750, 26);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _timeline = Place(_root, new PhaseTrack(), 344, 12, 956, 100);
        _menu = Place(_root, new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }, 1345, 27, 549, 35);
        _menu.AddThemeConstantOverride("separation", 7);
        _menu.AddChild(new TableIconButton(TableGlyph.Menu, ShowTableMenu, "Menu"));
        _menu.AddChild(new TableIconButton(TableGlyph.Rules, ShowRules, "Rules and reference"));
        _matSections = new InkMatSections { MotionSettled = TableMotionsSettled }; _world.AddChild(_matSections);
        _tableViewport.AddChild(_matSections.CreateIconOverlay(WorldToScreen));
        _battleDecor = new TableBattleInk { Paint = DrawBattleInk }; _world.AddChild(_battleDecor);
        _board = Place(_world, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, WorldWidth, 1100);
        _pileHud = Place(_world, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, WorldWidth, 1100);
        _hand = Place(_root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 250, 1060 - HandCardSide, BoardWidth - 12, HandCardSide);
        _goldMeter = new GoldPaymentMeter(); _root.AddChild(_goldMeter);
        var inspectorPanel = Place(_root, new PanelContainer { Theme = SidebarSkin.Theme() }, 1488, 94, SidebarWidth, 960);
        _inspectorPanel = inspectorPanel;
        inspectorPanel.AddThemeStyleboxOverride("panel", SidebarSkin.Surface());
        var side = new VBoxContainer(); side.AddThemeConstantOverride("separation", 10); inspectorPanel.AddChild(side);
        _seatLabel = Palette.Text("", 13, Palette.Gold); _seatLabel.Visible = false; side.AddChild(_seatLabel);
        _sidebarHeader = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; side.AddChild(new SidebarScroll { MaxHeight = 250 }); side.GetChild<SidebarScroll>(-1).AddChild(_sidebarHeader);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; side.AddChild(scroll);
        _inspector = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _inspector.AddThemeConstantOverride("separation", 10); scroll.AddChild(_inspector);
        side.AddChild(new HSeparator());
        _guide = Palette.Wrapped("", 17, Palette.PaperDim); _guide.MouseFilter = Control.MouseFilterEnum.Stop; side.AddChild(new SidebarScroll { MaxHeight = 110 }); side.GetChild<SidebarScroll>(-1).AddChild(_guide);
        _decisionActions = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; side.AddChild(new SidebarScroll { MaxHeight = 210 }); side.GetChild<SidebarScroll>(-1).AddChild(_decisionActions);
        _phaseActions = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; side.AddChild(new SidebarScroll { MaxHeight = 170 }); side.GetChild<SidebarScroll>(-1).AddChild(_phaseActions);
        _notice = Place(_root, Palette.Text("", 13, Palette.PaperDim), 26, 1060, 1868, 19);
        _notice.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; _notice.MouseFilter = Control.MouseFilterEnum.Stop;
        _intentOverlay = new BoardIntentOverlay { MouseFilter = Control.MouseFilterEnum.Ignore, Arrows = IntentArrows };
        _tableViewport.AddChild(_intentOverlay); _intentOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _attackTab = new AttackSourceTab { ZIndex = -1, Observe = ObserveAttackTab, Placement = AttackTabPlacement, Arrange = ArrangeAttackTab };
        _root.AddChild(_attackTab);
        // Fixed placards overlap the projected table and the hand. Keep both drawing
        // and native GUI picking above those cards, but below expanded/modal views.
        _clanHud = Place(_root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 45 }, 0, 0, 1920, 1080);
        _unitDetail = Place(_root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 50 }, 0, 0, 1920, 1080);
        BuildEventDecisions();
        BuildCameraControls();
        BuildShownCards();
        BuildBattleFeedback();
        BuildBattleSequence();
        BuildFullscreenGate();
        GetViewport().SizeChanged += FitPresentation; FitPresentation();
    }

    private void BuildFullscreenGate()
    {
        _fullscreenGate = Place(_root, new ColorRect { Color = new Color(.025f, .022f, .017f, .97f), ZIndex = 100, Visible = false }, 0, 0, 1920, 1080);

    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F11)
        {
            GetWindow().Mode = IsFullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
            _preferences.Fullscreen = IsFullscreen;
            if (_capturePath is null) _preferences.Save();
            if (MenuVisible && _titleMenu!.Page == "options") _titleMenu.ShowOptions();
            GetViewport().SetInputAsHandled();
        }
        else if (_seatHandoff is not null && IsInstanceValid(_seatHandoff))
        {
            // A modal cancellation must not also open the background menu.
            if (key.Keycode == Key.Escape) _seatHandoff.QueueFree();
            GetViewport().SetInputAsHandled();
        }
        else if (MenuVisible || PracticeVisible || _transitionReview || MatchDuelOpen) return;
        else if (key.Keycode == Key.Escape && CardShowOpen) { CloseCardShow(); GetViewport().SetInputAsHandled(); } // issue #1138
        else if (key.Keycode == Key.S && !key.CtrlPressed && !key.MetaPressed && !key.AltPressed && ToggleCardShow()) GetViewport().SetInputAsHandled();
        else if (key.Keycode == Key.Escape && _playProceed is not null) { ProceedCardPlay(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Escape && _shownPreview is not null) { HideShownPreview(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Escape && _shownOpen) { ToggleShownCards(false); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Escape && (_chosen is not null || _decisionSelection is not null || _reviewingAlternativeCost))
        { CancelChoice(); ClearMultiTargets(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Escape && _battleSequenceOpen)
        { _battleSequenceOpen = false; RenderInspector(); _battleProgressStrip?.Present(ProgressSegment, false); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Escape && _expandedUnits.Count > 0)
        {
            _expandedUnits.Clear(); RenderBoard(); GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Escape) { ShowMainMenu(); GetViewport().SetInputAsHandled(); }
    }

    private void Render()
    {
        ObserveDecisionSession(_session.Snapshot);
        if (_transitionReview && _transitionRoom is not null) return;
        if (MenuVisible && _enteringPrivate && _session.Online && _session.Connected && _session.Snapshot is not null) { EnterTable(); return; }
        if (MenuVisible || PracticeVisible) return;
        if (_session.Snapshot is null)
        {
            _opponentPaymentKey = null; _eitherOrKey = null; _eitherNoticeMatch = null; _eitherNotice = null;
            if (_chosen?.Type is "chooseEitherOr" or "chooseOpponentPayment") { _chosen = null; _sources.Clear(); }
            ClearSearchPanel();
            ClearLookPanel();
            ClearMultiTargets();
            _battleSequenceOpen = false; if (_battleProgressStrip is not null) _battleProgressStrip.Hide();
            ClearCopyPanel(true); _copyNoticeMatch = null; _copyNotice = null;
            ObserveShownCards();
            _eventSidebar.Visible = false; _eventHandBar.Visible = false; _eventDiscardId = null;
            _status.Text = _session.Busy ? "Connecting to your private match…" : "Disconnected · use Private match to reconnect";
            _matSections.Sync([], true, "disconnected");
            foreach (var flight in _flights.Where(IsInstanceValid)) flight.Finish(); _flights.Clear(); // issue #1103: flights aim at the tiles freed below
            Palette.Clear(_board); Palette.Clear(_hand); Palette.Clear(_inspector); Palette.Clear(_phaseActions); Palette.Clear(_decisionActions); _tiles.Clear();
            return;
        }
        if (RouteMatchDuel()) return;
        ObserveBattleJournal();
        ObserveCardPlays(); // Issue #630: show a played card, then send it to the discard pile (Main.CardPlays.cs).
        PrepareBattlePresentation();
        RenderBattleFeedback();
        RenderProvinceOrder();
        SyncTutorialCoach();
        FitTableViewport();
        ObserveBattleResolution();
        FitBattleSequence();
        FitTableViewport();
        _hand.Size = new Vector2(BoardWidth - 12, HandCardSide);
        if (_displayedVersion != View.Version)
        {
            _displayedVersion = View.Version;
            if (View.Events.LastOrDefault() is { } latest) SetNotice(latest.Message);
            ObserveBarredEvents();
        }
        if (_chosen?.Command?.ExpectedVersion != View.Version) { _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; }
        if (_chosen?.Movement is not null && _chosen.Command?.PlayerId != View.ViewerId) { _chosen = null; _sources.Clear(); }
        if (HasMultiTargets(_chosen) && (_chosen!.Command!.PlayerId != View.ViewerId || _multiMatch != _session.MatchKey)) { _chosen = null; _sources.Clear(); ClearMultiTargets(); }
        if (HasAttackTargets(_chosen) && _chosen!.Command!.PlayerId != View.ViewerId) { _chosen = null; _sources.Clear(); }
        if (_session.Online && (!_session.Connected || (_session.Snapshot.PlayersConnected?.Any(p => !p) ?? false))) { _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; }
        if (_selected is not null) _selected = FindCard(_selected.Id);
        _seatLabel.Text = _session.Online ? $"ROOM {_session.Snapshot.RoomCode} · {Self.Clan}" : $"PRACTICE · {Self.Clan}";
        _status.Text = View.WinnerId is { } winner ? $"{View.Players[winner].Clan} wins · {View.VictoryReason}" : $"Turn {View.TurnNumber}  ·  {View.Players[View.ActionPhaseTurnPlayerId ?? View.ActivePlayerId].Clan}  ·  {(View.PriorityPlayerId == View.ViewerId ? "Your opportunity" : View.Players[View.PriorityPlayerId].Clan + " is deciding")}";
        if (View.Scenario is PublishedContent.Scenario or CustomPractice.Scenario) _status.Text += $"  ·  Favor: {(View.FavorPlayerId is int favor ? View.Players[favor].Clan : "unclaimed")}";
        if (_session.Online && _session.Snapshot.PlayersConnected is { } connected && connected.Any(p => !p)) _status.Text = $"Room {_session.Snapshot.RoomCode} · waiting for both players to connect";
        ObserveCopyResults();
        FrameDynasty(); // before the End Dynasty button is built
        RenderTimeline(); RenderBoard(); RenderInspector(); RenderPhaseActions(); Highlight(); FrameNewBattle(); FrameReactionTarget();
        RenderBattleFeedback();
        ObserveShownCards();
    }

    private void RenderTimeline()
    {
        _timeline.Present(View);
        _timeline.PresentActions(View, Actions, !_session.Busy && (!_session.Online || _session.Connected && !(_session.Snapshot!.PlayersConnected?.Any(p => !p) ?? false)));
        if (EventDecisionActive)
        {
            _timeline.PriorityLabel.Text = OwnEventDecision ? "YOUR DECISION" : "Opponent is deciding";
            _timeline.PriorityLabel.TooltipText = View.PendingChoice!.Prompt;
        }
        if (PresentationBusy)
        {
            _timeline.PriorityLabel.Text = _battleOutcomeText;
            _timeline.PriorityLabel.TooltipText = _battleOutcomeText;
            _timeline.PriorityLabel.Modulate = Palette.Gold;
        }
    }

    private CardTile Tile(CardView card, bool compact, float width, float height)
    {
        var previewFlip = _previewBowFlip == card.Id; if (previewFlip) _previewBowFlip = null;
        var previewBowed = _previewBows.Contains(card.Id) && !card.Bowed;
        if (previewBowed) card = card with { Bowed = true };
        var tile = new CardTile(card, compact, SelectCard, _ => { }) { CustomMinimumSize = new Vector2(width, card.Zone is Zone.Hand or Zone.Resolving ? height : PlaneHeight(height)), FaceHeight = height - 12, ReducedMotion = _reducedMotion, Highlighted = _selected?.Id == card.Id, SelectedSource = _sources.Contains(card.Id) || _chosen is not null && QuoteFor(_chosen)?.Sources?.Any(m => m.CardId == card.Id && _sources.Contains(m.Id)) == true, PreviousBowed = previewFlip ? !card.Bowed : Time.GetTicksMsec() - _bowChangedAt < 180 ? _previousBows.GetValueOrDefault(card.Id, card.Bowed) : card.Bowed, ContextualGoldProduction = Production(card, View.Players.Single(p => p.Id == card.OwnerId)) };
        tile.TokenSourceName = id => FindCard(id)?.Definition?.Name;
        tile.ContextHelp = card.AttachedTo is { } parent ? "Attached to " + (FindCard(parent)?.Definition?.Name ?? parent) + ". Moves with this unit." : card.Zone == Zone.Battlefield ? $"At {(card.ProvinceIndex is int at ? BattlefieldName(at) : "a battlefield")}'s battlefield. " + RulesHelp.Topic("force") : card.Zone == Zone.Home ? "At home. Not assigned to a province. " + (card.Bowed ? RulesHelp.Topic("bow") : "") : card.Zone == Zone.Hand ? "In your hand. Select to see legal actions and targets." : "Select to inspect available actions.";
        if (card.Zone == Zone.Province && card.ProvinceIndex is int province)
        {
            var owner = View.Players.Single(p => p.Id == card.OwnerId);
            tile.ContextHelp = $"{owner.Clan} · Province {province + 1} · {owner.Provinces.Single(p => p.Index == province).Strength} province strength";
        }
        if (card.Zone is Zone.Region or Zone.Celestial) tile.ContextHelp = SupportContext(card);
        tile.CelestialMount = card.Zone == Zone.Celestial;
        if (card.Zone is not (Zone.Hand or Zone.Resolving)) tile.ProjectToScreen = _tableSurface.ProjectCanvas; // issue #917: a card in the resolution area is drawn flat, like the hand, never on the table plane
        BindStatTransition(tile);
        _tiles.Add(tile); return tile;
    }

    private void SelectCard(CardView card)
    {
        if (_shownOpen) ToggleShownCards(false);
        if (_session.Busy || PresentationBusy || _playHeld || _endingDragPress) return;
        // Issues #950 and #958: Dark Experiments' province decision is a generic decision, which would take every card click. The card sitting in a province stands for that province, as it does for every other province choice.
        if (EventProvinceChoiceActive && card.Zone is Zone.Province or Zone.Region && card.ProvinceIndex is { } eventProvince) { SelectProvince(card.OwnerId, eventProvince); return; }
        if (SelectEventCard(card) || SelectPendingCandidate(card) || SelectAlternativeCostCard(card)) return;
        if (SelectMultiCard(card)) return;
        if (SelectDiscardCard(card)) return;
        if (SelectReactionSourceCard(card)) return; // issue #962: a reaction's card is clicked on the table
        if (AttackTargetActive && card.Zone is Zone.Province or Zone.Region && card.ProvinceIndex is { } attackProvince)
        { if (AttackProvinceTarget(card.OwnerId, attackProvince)) ChooseAttackTarget(AttackChoice.Province, province: attackProvince); return; }
        if (MovementActive && card.Zone is Zone.Province or Zone.Region && card.ProvinceIndex is { } destination)
        { ChooseMovementProvince(card.OwnerId, destination); return; }
        if (_chosen is { Type: "attack" or "assignDefense" } assignment)
        {
            if (assignment.TargetIds?.Contains(card.Id) == true)
            {
                // Issue #973: the unit being placed, clicked again while it already has a battlefield, goes back home (selecting another unit first and then its battlefield still reassigns).
                if (_assignmentUnit == card.Id && _assignments.Remove(card.Id)) { _selected = card; RenderBoard(); RenderInspector(); Highlight(); return; }
                _selected = card; _assignmentUnit = card.Id; RenderInspector(); Highlight(); return;
            }
            if (card.Zone == Zone.Province && card.OwnerId == 1 - View.ActivePlayerId && card.ProvinceIndex is int province) { SelectProvince(card.OwnerId, province); return; }
            SetNotice("Choose one of your highlighted units, then a highlighted province. Cancel to inspect other cards."); return;
        }
        if (ProvinceRefreshActive && _chosen!.TargetIds?.Contains(card.Id) == true)
        { if (!_refreshSelected.Remove(card.Id)) _refreshSelected.Add(card.Id); RenderInspector(); Highlight(); return; } // Border Keep's refresh: a click selects the province's card, a second click deselects it
        if (ChooseSupportTarget(card)) return;
        if (_chosen is { Type: "equip" or "playStrategy" or "useAbility", Command.CardIds: null } targeted && targeted.TargetIds?.Contains(card.Id) == true)
        {
            ChooseTarget(card.Id); return;
        }
        if (PerformerClicks() is { Count: > 0 } performerClicks && performerClicks.ContainsKey(card.Id))
        { PickPerformer(card.Id); return; }
        // Issue #973: an Attack that may be made with no target takes that choice from a click on the card that performs it, or on the card it belongs to.
        if (AttackTargetActive && _chosen!.NoTarget && (card.Id == _chosen.Command!.PerformerId || card.Id == (_chosen.CardId ?? _chosen.Command.CardId)))
        { ChooseAttackTarget(AttackChoice.NoTarget); return; }
        // Issue #960: while a player is being chosen, either player's stronghold stands for that player (the clan plaque does the same).
        if (StrongholdOwner(card) is int strongholdPlayer && ChoosePlayerTarget(strongholdPlayer)) return;
        // Issue #958: while a province is being chosen, the Dynasty card or Region sitting in a province stands for that province (a card that is itself a legal target or performer was taken above).
        if ((ProvinceTargetActive || MultiProvinceActive) && card.Zone is Zone.Province or Zone.Region && card.ProvinceIndex is { } cardProvince)
        { SelectProvince(card.OwnerId, cardProvince); return; }
        if (_chosen is not null && card.OwnerId == View.ViewerId && ClickablePaymentMethods(card).Length > 0)
        {
            if (_chosen.Type == "chooseOpponentPayment" && QuoteFor(_chosen) is { } paymentQuote && AmountFor(_chosen, paymentQuote) == 0)
            { SetNotice("Choose how much Gold to pay before selecting sources."); return; }
            CycleCardPayment(card); return;
        }
        // Clicking another card that has its own flow (a playable hand card, a purchasable province card) abandons the current review and starts that card's.
        if (_chosen is not null && (OwnFlowAction(card) is not null || Actions.Any(a => a.Type == "purchase" && a.CardId == card.Id && a.Enabled)))
        { ResetDecisionSelection(); _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; }
        // A misclick during a reviewed action must not silently discard its target or payment.
        if (_chosen is not null) { SetNotice("That card is not a legal target. Cancel the current action to inspect it."); return; }
        if (_selected?.Id == card.Id) { DeselectCard(); return; }
        _selectedHomeReminder = null; HighlightHomeReminders();
        _selected = card; _sources.Clear(); _playPrompt = null;
        var assign = Actions.FirstOrDefault(a => a.Enabled && a.Type is "attack" or "assignDefense" && a.TargetIds?.Contains(card.Id) == true);
        if (assign is not null) { Choose(assign); _assignmentUnit = card.Id; RenderInspector(); Highlight(); return; }
        var purchase = Actions.FirstOrDefault(a => a.Type == "purchase" && a.CardId == card.Id && a.Enabled);
        var multiSlot = card.ProvinceIndex is int index && card.Zone == Zone.Province &&
            ProvincePresentation.Slots(View.Players.Single(p => p.Id == card.OwnerId).Provinces.Single(p => p.Index == index)).Length > 1;
        if (purchase is not null && !multiSlot) Choose(purchase);
        else if (StartInPlayAbility(card)) { } // issue #954: the first click on a card in play with one usable ability starts it; hovering inspects
        else { BeginDirectPlay(card); RenderInspector(); RenderPhaseActions(); Highlight(); }
    }

    private void Highlight()
    {
        var selectedProvince = View.Players.SelectMany(p => p.Provinces.Where(q => _selected is not null && ProvincePresentation.Cards(q).Any(c => c.Id == _selected.Id))
            .Select(q => $"province:{p.Id}:{q.Index}")).FirstOrDefault();
        var rootId = _selected?.AttachedTo ?? _assignmentUnit ?? _selected?.Id;
        var selectedUnit = rootId is null ? null : FindCard(rootId);
        var provinceIndex = rootId is not null && _assignments.TryGetValue(rootId, out var draftProvince) ? draftProvince : selectedUnit?.ProvinceIndex;
        if (EventDecisionActive && View.PendingChoice is { Type: "harshChoices", CardId: { } eventId } && FindCard(eventId) is { ProvinceIndex: { } eventProvince } eventCard)
            selectedProvince = $"province:{eventCard.OwnerId}:{eventProvince}";
        _matSections.Emphasize(selectedProvince ?? (provinceIndex is int p && (selectedUnit?.Zone == Zone.Battlefield || _assignments.ContainsKey(rootId!)) ? $"battle:{1 - View.ActivePlayerId}:{p}" : null));
        var supportTargets = SupportDraftChoices().SelectMany(a => a.TargetIds ?? []).ToHashSet();
        var performerClicks = PerformerClicks(); var reactionSources = ReactionSourceCardIds();
        var tutorialPointed = TutorialPointed();
        foreach (var tile in _tiles)
        {
            tile.Pointed = tutorialPointed.Contains(tile.Card.Id); ApplyTutorialDeal(tile); ApplyTutorialEmphasis(tile);
            tile.DecisionSelected = OwnEventDecision && View.PendingChoice!.Type == "forcedDiscard" && tile.Card.Id == _eventDiscardId;
            tile.LegalTarget = EventDecisionActive && View.PendingChoice is { Type: "harshChoices", CardId: { } harshCard } && tile.Card.Id == harshCard;
            tile.Highlighted = tile.Card.Id == (_assignmentUnit ?? _selected?.Id) || rootId is not null && tile.Card.AttachedTo == rootId;
            tile.Highlighted |= GenericDecisionActive && tile.Card.Id == View.PendingChoice?.CardId;
            tile.Highlighted |= _chosen?.Command?.PerformerId == tile.Card.Id; // issue #789: the performer of the prepared action is marked on the table
            tile.DecisionSelected |= DiscardActive && _discardSelected.Contains(tile.Card.Id); // issue #791: the cards chosen to discard
            tile.LegalTarget |= DiscardActive && tile.Card.Zone == Zone.Hand && tile.Card.OwnerId == View.ViewerId && !_discardSelected.Contains(tile.Card.Id);
            tile.Highlighted |= _hoveredPaymentCardId == tile.Card.Id; // issue #667: the payment method under the pointer names its card
            tile.LegalTarget |= GenericDecisionActive && tile.Card.Id == View.PendingChoice?.CardId;
            tile.LegalTarget |= View.PendingChoice?.Type == "beforeActionReaction" && tile.Card.Id == View.PendingChoice.CardId;
            tile.DecisionSelected |= GenericDecisionActive && (tile.Card.Id == _decisionSelection?.Command?.TargetId || _decisionSelection?.DecisionCandidateIds?.Contains(tile.Card.Id) == true);
            tile.LegalTarget |= _chosen?.TargetIds?.Contains(tile.Card.Id) == true || supportTargets.Contains(tile.Card.Id);
            tile.LegalTarget |= performerClicks.ContainsKey(tile.Card.Id) || reactionSources.Contains(tile.Card.Id);
            tile.LegalTarget |= AlternativeCostCards().Contains(tile.Card.Id); // a hand card a discard way to pay may spend (Akodo Kamina)
            tile.DecisionSelected |= _reviewingAlternativeCost && _decisionSelection?.Command?.CardIds?.Contains(tile.Card.Id) == true;
            tile.LegalTarget |= MultiTargetActive && MultiUpFront.Any(s => s.Slot == _multiActiveSlot && s.CardIds.Contains(tile.Card.Id)) || MultiTargetActive && MultiOpenCardSlots(tile.Card.Id).Any(); // issue #969: every open group's candidates
            if (StrongholdOwner(tile.Card) is int strongholdOwner) tile.LegalTarget |= PlayerTargetLegal(strongholdOwner);
            tile.ChosenTarget = StrongholdOwner(tile.Card) is int chosenPlayer && PlayerTargetChosen(chosenPlayer) || _chosen?.Command?.TargetId == tile.Card.Id || ProvinceRefreshActive && _refreshSelected.Contains(tile.Card.Id) || _assignments.ContainsKey(tile.Card.Id) || MultiTargetActive && MultiSelectedIds.Contains(tile.Card.Id);
            tile.SelectedSource = _sources.Contains(tile.Card.Id) || _sources.Any(id => id.StartsWith(tile.Card.Id + "#", StringComparison.Ordinal)); tile.QueueRedraw(); // a payment token ("<card>#reaction") marks its card
        }
        HighlightPlayers(); HighlightPiles();
        foreach (var (key, button) in _provinceButtons)
        {
            var legal = _chosen is { Type: "attack" or "assignDefense" } && key.Owner == 1 - View.ActivePlayerId && _chosen.Provinces?.Contains(key.Province) == true
                || MovementDestination(key.Owner, key.Province) || AttackProvinceTarget(key.Owner, key.Province) || ProvinceTargetLegal(key.Owner, key.Province)
                || EventProvinceOption(key.Owner, key.Province) is not null;
            button.Modulate = ProvinceTargetChosen(key.Owner, key.Province) || EventProvinceChosen(key.Owner, key.Province) ? new Color(1.4f, 1.25f, .75f) : legal ? new Color(1.2f, 1.15f, .85f) : Colors.White;
            var provinceState = View.Players.Single(p => p.Id == key.Owner).Provinces.Single(p => p.Index == key.Province);
            var instruction = EventProvinceChoiceActive ? "Choose this province. Confirm separately.\n" : ProvinceTargetActive || MultiProvinceActive ? "Choose this province as the target. Confirm separately.\n" : AttackTargetActive ? "Target this battlefield’s province. Confirm separately.\n" : MovementActive ? "Choose this movement destination. Nothing moves until you confirm.\n" : "Assign the selected unit here. Nothing moves until you confirm.\n";
            button.TooltipText = RulesHelp.Tooltip($"Province {key.Province + 1} · {provinceState.Strength} province strength\n" + (legal ? instruction : "Inspect this province.\n") + RulesHelp.Topic("province"));
        }
        foreach (var (province, label) in _laneSummaries)
        {
            label.Text = "";
            label.TooltipText = BattlefieldProvince(province) is null ? BattlefieldPresentation.OpenHelp
                : $"{BattlefieldName(province)} battlefield" + (MovementDestination(1 - View.ActivePlayerId, province) ? " · choose this destination" : "");
        }
        _intentOverlay?.QueueRedraw();
    }

    // Core builds each targeted action's command with its first legal target filled in. The player, not the client or that default, picks the target (issue #581):
    // the panel starts on "Choose a target…". Attack targets and support cards have their own target flows and keep theirs.
    private bool NeedsTargetPick(ActionOption option) =>
        option is { Type: "equip" or "playStrategy" or "useAbility", Command: { TargetId: not null }, TargetIds: { Length: > 0 } } && (option.Command!.CardIds is null || option.AlternativeCost is not null) && !HasAttackTargets(option)
        && !(option.CardId is { } sourceId && FindCard(sourceId) is { } source && IsSupportCard(source));

    // Choose(...) for a card's other recruitment/payment variant keeps the Gold already gathered (and its holdings bowed) that the new quote still offers.
    private void Choose(ActionOption option, bool keepTarget, bool keepSources)
    {
        var kept = keepSources ? _sources.ToArray() : [];
        Choose(option, keepTarget);
        if (kept.Length == 0 || _chosen is null || !PlayerPicksPayment(_chosen) || QuoteFor(_chosen) is not { } quote) return;
        var offered = PaymentMethods(quote).Select(m => m.Id).ToHashSet();
        foreach (var id in kept) if (offered.Contains(id)) _sources.Add(id);
        ResuggestSources(); RenderInspector(); Highlight();
    }

    // The card a click starts its own flow for (a playable hand card; a purchasable province card is handled in SelectCard itself), or null.
    private ActionOption? OwnFlowAction(CardView card)
    {
        if (card.Zone != Zone.Hand || card.OwnerId != View.ViewerId || View.PendingChoice is not null) return null;
        var candidates = Actions.Where(a => a.Enabled && a.CardId == card.Id && a.Command is not null && a.Type is "playStrategy" or "equip" or "useAbility" or "recruitReserve").ToArray();
        return candidates.Length == 1 || candidates.Length > 1 && candidates.All(a => a.Type == "equip") ? candidates[0] : null;
    }

    private void Choose(ActionOption option, bool keepTarget = false)
    {
        if (_shownOpen) ToggleShownCards(false);
        if (!option.Enabled || _session.Busy || PresentationBusy || _playHeld) return;
        if (!keepTarget && NeedsTargetPick(option)) option = option with { Command = option.Command! with { TargetId = null } };
        ResetDecisionSelection();
        if (option.Movement is not null && option.Command is { } moveCommand) option = option with { Command = moveCommand with { Province = null } };
        if (IsProvinceTarget(option) && option.Command is { } provinceCommand) option = option with { Command = provinceCommand with { Province = null } }; // the player picks the province, not Core's first legal one
        _attackChoice = AttackChoice.Unselected;
        if (HasAttackTargets(option)) option = option with { Command = option.Command! with { TargetId = null, Province = null } };
        _chosen = option; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; _refreshSelected.Clear();
        if (!PlayerPicksPayment(option)) foreach (var id in option.Command?.SourceIds ?? []) _sources.Add(id);
        SyncPaymentAmount();
        if (HasMultiTargets(option)) BeginMultiTargets(); else ClearMultiTargets();
        if (option.Type is "attack" or "assignDefense") _assignmentUnit = option.TargetIds?.FirstOrDefault();
        RenderBoard(); RenderInspector(); RenderPhaseActions(); Highlight();
        if (MovementActive) FocusMovement();
        if (AttackTargetActive) FocusAttackTargets();
        if (MultiTargetActive) FocusMultiTargets();
    }

    private async Task Submit(GameCommand command)
    {
        if (_session.Snapshot is null || _submissionInFlight || _session.Busy ||
            command.ExpectedVersion != _session.Snapshot.View.Version || command.PlayerId != _session.Snapshot.View.ViewerId) return;
        if (_provinceReviewView is not null || _battlefieldReviewView is not null) { SetNotice("Presentation preview · game actions are disabled."); return; }
        if (PresentationBusy || _playHeld) return;
        if (_session.Online && (_session.Snapshot?.PlayersConnected?.Any(p => !p) ?? false))
        {
            SetNotice("Both players need to be connected before continuing."); return;
        }
        var token = ++_submissionToken;
        _submissionInFlight = true;
        RememberSubmissionInput();
        TrackReactionSubmission(command);
        _chosen = null; _playPrompt = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null;
        Highlight();
        try { await _session.Submit(command); }
        finally { if (_submissionToken == token) _submissionInFlight = false; }
    }

    private static string ShortName(CardView card) => card.Definition?.Name.Replace("training ", "").Replace("Training ", "") ?? "Hidden card";
    // The engine's own figure (CardView.GoldProduced, which also covers a Personality that can bow for Gold while a cost is being paid: Yoritomo
    // Sakuma, issue #338). The client holds no production rule of its own (issue #578); payment sources come from the quote (PaymentMethods).
    private static int Production(CardView card, PlayerView owner) => card.GoldProduced ?? 0;
    private CardView? FindCard(string id) => (View.ResolutionArea ?? []).Select(e => e.Card).Concat(View.Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Hand).Concat(p.Discard).Concat(p.RemovedFromGame).Concat(p.EventsInPlay ?? []).Concat(p.Celestials ?? []).Concat(p.Provinces.SelectMany(ProvincePresentation.Cards)).Append(p.Stronghold))).FirstOrDefault(c => c.Id == id);
    private static string HumanPhase(Phase phase) => phase switch { Phase.AttackDeclaration => "Declare an attack", Phase.InfantryDeclaration => "Assign attackers", Phase.DefenseAssignment => "Assign defenders", Phase.BattleSelection => "Choose a battlefield", Phase.DynastyDiscard => "Dynasty · discard", _ => phase.ToString() };
    private void SetNotice(string message) { _matchDuel?.Notify(message); _notice.Text = message; _notice.TooltipText = message; if (MenuVisible) _titleMenu!.Notify(message); }
    public override void _ExitTree() => _session.Dispose();
}
