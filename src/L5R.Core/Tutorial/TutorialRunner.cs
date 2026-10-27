namespace L5R.Core;

/// <summary>What the coach panel shows for the current step (issue #935). Highlights are card ids to point at; the panel never decides anything.</summary>
/// <param name="Glow">Parts of the screen to light up (see <see cref="TutorialGlow"/>).</param>
/// <param name="ShowDefinitionId">A card to show large in the middle of the table while the note is read (the "Show" flow).</param>
/// <param name="Slow">The presentations this step starts (province reveals, a played card) run at a quarter of their usual speed.</param>
/// <param name="AnimateOnly">When set, the only cards (by definition id) whose reveal and entering-play animations this step shows; the others reveal quietly (they are explained in a later step).</param>
/// <param name="ProvincesOnTable">False while the opening notes are read: the provinces are laid out at the step that introduces them.</param>
/// <param name="Emphasis">A printed part of every highlighted card to swell and shrink back over four seconds (see <see cref="TutorialEmphasis"/>).</param>
/// <param name="NextLessonId">On a lesson's last note: the lesson the curriculum lists next, offered beside the button back to the menu (its title is <paramref name="NextTitle"/>).</param>
public sealed record TutorialCoach(string StepId, string Title, string Body, string? Button, string[] HighlightCardIds, int Step, int Steps, bool Finished, string? Instruction = null,
    string[]? Glow = null, string? ShowDefinitionId = null, bool Slow = false, bool ProvincesOnTable = true, bool CanGoBack = false, string? ExitLabel = null, string[]? AnimateOnly = null,
    string? Emphasis = null, string? NextLessonId = null, string? NextTitle = null);

/// <summary>Printed parts of a card a note can swell: the Gold Cost coin and the Honor Requirement box.</summary>
public static class TutorialEmphasis
{
    public const string Cost = "cost", HonorRequirement = "honor-requirement";
}

/// <summary>Parts of the screen a note can light up.</summary>
public static class TutorialGlow
{
    public const string PhaseBar = "phase-bar", BattleBar = "battle-bar", Hand = "hand";
    /// <summary>Your Family Honor on your placard.</summary>
    public const string Honor = "honor";
}

/// <summary>One beat of the lesson. A narration step has a <see cref="Button"/> and waits for it; an action step has an <see cref="Allow"/> and waits for the player to make that move.
/// Everything else the engine would offer is shown as unavailable, so a click the lesson does not want never reaches the engine.</summary>
internal sealed class TutorialStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    /// <summary>Narration: the label of the button that moves on. Null for an action step.</summary>
    public string? Button { get; init; }
    /// <summary>What happens to the table when the step is done (its button is pressed, or its move is made): a hold is lifted, the opponent plays its part.</summary>
    public Action<TutorialContext>? Then { get; init; }
    /// <summary>Action step: which engine options stay available (type, ability, card), judged from the option's command.</summary>
    public Func<TutorialLookup, GameCommand, bool>? Allow { get; init; }
    /// <summary>Action step: the parameters the lesson requires on the command itself (the unit equipped, the Gold sources), checked when it is submitted.</summary>
    public Func<TutorialLookup, GameCommand, bool>? Accept { get; init; }
    /// <summary>Action step: exactly what to do for the lesson to move on, shown apart from the explanation. There is no button until it is done.</summary>
    public string? Instruction { get; init; }
    public string RefusalHint { get; init; } = "Follow the highlighted step first.";
    public Func<TutorialLookup, IEnumerable<string>>? Highlight { get; init; }
    /// <summary>Action step: the only cards the move may target, so the table offers just those.</summary>
    public Func<TutorialLookup, IEnumerable<string>>? Targets { get; init; }
    /// <summary>Action step: the only provinces an attack or defence may name.</summary>
    public int[]? OnlyProvinces { get; init; }
    public string[]? Glow { get; init; }
    public string? ShowDefinitionId { get; init; }
    public bool Slow { get; init; }
    /// <summary>Definition ids of the only cards whose reveal animations play when this step shows (see <see cref="TutorialCoach.AnimateOnly"/>).</summary>
    public string[]? AnimateOnly { get; init; }
    public string? Emphasis { get; init; }
    public bool IsAction => Allow is not null;
}

/// <summary>Resolves "the Border Keep" and "Matsu Hachiro" in a running lesson to card ids, for step predicates and highlights.</summary>
internal sealed class TutorialLookup(MatchEngine engine)
{
    public string DefinitionOf(string? cardId) => cardId is null ? "" : engine.TutorialDefinitionOf(cardId);
    public bool Is(string? cardId, string definitionId) => cardId is not null && DefinitionOf(cardId) == definitionId;
    public string[] Own(string definitionId, params Zone[] zones) => engine.TutorialCardIds(TutorialRunner.Seat, definitionId, zones);
    public string[] Foe(string definitionId, params Zone[] zones) => engine.TutorialCardIds(1 - TutorialRunner.Seat, definitionId, zones);
    public string Stronghold(int owner) => engine.TutorialStrongholdId(owner);
    public string? Province(int owner, int index) => engine.TutorialProvinceCardId(owner, index);
}

/// <summary>What a step may do to the table when it is done. The opponent only ever plays when a step asks it to, so the lesson can stop between its moves.</summary>
internal sealed class TutorialContext(TutorialRunner runner)
{
    public MatchEngine Engine => runner.Engine;
    public MatchState State => runner.Engine.TutorialState;
    /// <summary>Stop at the start of this phase of this player's turn (see <see cref="MatchEngine.TutorialHoldAt"/>).</summary>
    public void HoldAt(Phase phase, int playerId) => Engine.TutorialHoldAt(phase, playerId);
    public void Resume() => Engine.TutorialResume();
    public void StartTurn() => Engine.TutorialBeginTurn();
    public void Draw(int count) => Engine.TutorialDraw(TutorialRunner.Seat, count);
    /// <summary>Stop the Events phase at one of this player's provinces: before its card is turned face up, or (<paramref name="after"/>) with it face up and not yet resolved (see <see cref="MatchEngine.TutorialHoldReveal"/>).</summary>
    public void HoldReveal(int playerId, int province, bool after) => Engine.TutorialHoldReveal(playerId, province, after);
    /// <summary>The next Events phase leaves these provinces of this player face down (see <see cref="MatchEngine.TutorialKeepFaceDown"/>).</summary>
    public void KeepFaceDown(int playerId, params int[] provinces) { foreach (var province in provinces) Engine.TutorialKeepFaceDown(playerId, province); }
    /// <summary>The opponent uses its Border Keep's first-turn Limited action on these provinces (they are put at the bottom of its deck and refilled face up).</summary>
    public void FoeBorderKeep(params int[] provinces)
    {
        var foe = 1 - TutorialRunner.Seat;
        var option = Engine.QueryActions(foe).FirstOrDefault(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId == "border-first" && a.Command is not null)
            ?? throw new InvalidOperationException("The lesson expected the opponent's Border Keep first-turn action to be open.");
        var ids = provinces.Select(i => Engine.TutorialProvinceCardId(foe, i) ?? throw new InvalidOperationException($"Province {i + 1} is empty.")).ToArray();
        var result = Engine.Execute(option.Command! with { CardIds = ids });
        if (!result.Accepted) throw new InvalidOperationException("The opponent's Border Keep was refused: " + result.Error);
    }
    /// <summary>Makes the one engine option of this type that is open to you, for the moves the lesson narrates but does not ask you to click (Proclaiming).</summary>
    public void Auto(string type)
    {
        var option = Engine.QueryActions(TutorialRunner.Seat).FirstOrDefault(a => a.Enabled && a.Type == type && a.Command is not null)
            ?? throw new InvalidOperationException($"The lesson expected a '{type}' option.");
        var result = Engine.Execute(option.Command!);
        if (!result.Accepted) throw new InvalidOperationException($"The lesson's '{type}' was refused: {result.Error}");
    }
    /// <summary>The opponent plays its script until <paramref name="stop"/> holds or a decision is yours.</summary>
    public void Foe(Func<MatchState, bool>? stop = null) => runner.PlayFoe(stop, scripted: true);
}

/// <summary>Plays a scripted lesson over the real engine (issue #935). The engine stays the only authority on legality: the runner narrows what is offered, plays the practice opponent's side of the script,
/// and moves the coach panel along. Offline only, and the lesson's table is never used for a room.
/// Everything the player does (a button, a move) is kept, so <see cref="Back"/> can rebuild the table and play it again up to the step before: the engine is deterministic, so that is the scene as it was.</summary>
public sealed class TutorialRunner
{
    public const int Seat = 0;
    private readonly MatchEngine _engine;
    private readonly TutorialLesson _lesson;
    private readonly TutorialStep[] _steps;
    private readonly TutorialLookup _lookup;
    private readonly TutorialContext _context;
    private readonly List<GameCommand?> _inputs = [];
    private int _index;
    private int? _attackedProvince;
    private readonly HashSet<FoePurchase> _bought = [];

    private TutorialRunner(TutorialLesson lesson)
    {
        _lesson = lesson; _engine = MatchEngine.CreateTutorial(lesson.Setup); _steps = lesson.Steps; _lookup = new(_engine); _context = new(this);
        lesson.Opening?.Invoke(_context);
        PlayFoe(null, scripted: false);
    }

    /// <summary>The first lesson: Lion (you) against Crane on the pinned decks, the first turn and a half.</summary>
    public static TutorialRunner Start() => new(TutorialLessons.Default);
    internal static TutorialRunner Start(TutorialLesson lesson) => new(lesson);
    public static TutorialRunner Start(string lessonId) => new(TutorialLessons.Get(lessonId));

    public string LessonId => _lesson.Id;

    public MatchEngine Engine => _engine;
    public bool Finished => _index >= _steps.Length;
    public string? Refusal { get; private set; }
    public bool CanGoBack => _inputs.Count > 0;

    private TutorialStep? Current => Finished ? null : _steps[_index];

    public TutorialCoach Coach
    {
        get
        {
            if (Current is not { } step) return new("done", "Lesson complete", "", null, [], _steps.Length, _steps.Length, true);
            var next = _index == _steps.Length - 1 ? TutorialCurriculum.After(_lesson.Id) : null;
            return new(step.Id, step.Title, step.Body, step.Button, step.Highlight?.Invoke(_lookup).ToArray() ?? [], _index + 1, _steps.Length, false, step.Instruction,
                step.Glow, step.ShowDefinitionId, step.Slow, _index >= _lesson.ProvincesStep, CanGoBack, AnimateOnly: step.AnimateOnly, Emphasis: step.Emphasis, NextLessonId: next?.Id, NextTitle: next?.Title);
        }
    }

    /// <summary>The engine's own options for you, with everything the current step does not ask for switched off.</summary>
    public ActionOption[] Actions()
    {
        var offered = _engine.QueryActions(Seat);
        if (Current is not { IsAction: true } step || _engine.PriorityPlayerId != Seat || _engine.TutorialHeld)
            return offered.Select(a => a with { Enabled = false, Reason = a.Enabled ? "Read the tutorial note, then press its button." : a.Reason }).ToArray(); // an option the engine itself refuses keeps its own reason (Sachina: Loyal)
        var targets = step.Targets?.Invoke(_lookup).ToHashSet();
        return offered.Select(a => a.Enabled && a.Command is { } command && step.Allow!(_lookup, command)
            ? a with
            {
                // the command's default target moves with the narrowed list, so a table that submits it as it stands names a target the step accepts
                Command = targets is not null && a.TargetIds is not null && command.TargetId is { } preset && !targets.Contains(preset) && a.TargetIds.FirstOrDefault(targets.Contains) is { } first ? command with { TargetId = first } : command,
                TargetIds = targets is not null && a.TargetIds is not null ? a.TargetIds.Where(targets.Contains).ToArray() : a.TargetIds,
                Provinces = step.OnlyProvinces is { } only && a.Provinces is not null ? a.Provinces.Where(only.Contains).ToArray() : a.Provinces
            }
            : a with { Enabled = false, Reason = a.Enabled ? step.RefusalHint : a.Reason }).ToArray();
    }

    /// <summary>A move from the table. Anything the step does not ask for is refused with the step's hint, before the engine sees it.</summary>
    public CommandResult Execute(GameCommand command)
    {
        Refusal = null;
        if (Current is not { IsAction: true } step || !step.Allow!(_lookup, command) || step.Accept is { } accept && !accept(_lookup, command))
        {
            Refusal = Current?.RefusalHint ?? "The lesson is over.";
            return new(false, Refusal, _engine.GetView(Seat).Version, []);
        }
        var result = _engine.Execute(command);
        if (!result.Accepted) { Refusal = result.Error; return result; }
        if (command.Type == "attack") _attackedProvince = command.Assignments is { Count: > 0 } assignments ? assignments.Values.First() : command.Province;
        _inputs.Add(command);
        Advance(step);
        return result;
    }

    /// <summary>The button of a narration step.</summary>
    public bool Continue()
    {
        if (Current is not { IsAction: false } step) return false;
        _inputs.Add(null);
        Advance(step);
        return true;
    }

    private void Advance(TutorialStep step)
    {
        step.Then?.Invoke(_context);
        _index++;
        PlayFoe(null, scripted: false);
    }

    /// <summary>The table as it was before the player's last button or move: the lesson is started again and played to there. Null at the very first step.</summary>
    public TutorialRunner? Back()
    {
        if (_inputs.Count == 0) return null;
        var replay = Start(_lesson);
        foreach (var input in _inputs.Take(_inputs.Count - 1))
        {
            if (input is null) { if (!replay.Continue()) throw new InvalidOperationException("The lesson could not be replayed."); }
            else if (!replay.Execute(input).Accepted) throw new InvalidOperationException("The lesson could not be replayed: " + replay.Refusal);
        }
        return replay;
    }

    /// <summary>Plays the opponent's side, and declines your reactions on its turn, until a decision is yours. Unscripted (after every step) it only answers: passes, defends the province you
    /// attacked and plays its reactions on your turn, and never starts anything on its own turn, so the lesson can stop between the moves it narrates.</summary>
    internal void PlayFoe(Func<MatchState, bool>? stop, bool scripted)
    {
        for (var guard = 0; guard < 80 && _engine.WinnerId is null; guard++)
        {
            if (_engine.TutorialHeld || stop?.Invoke(_engine.TutorialState) == true) return;
            var seat = _engine.PriorityPlayerId;
            var options = _engine.QueryActions(seat).Where(a => a.Enabled && a.Command is not null).ToArray();
            ActionOption? pick;
            if (seat == Seat) pick = options.FirstOrDefault(a => a.Type == "declineReaction") is { } decline && _engine.TutorialState.ActivePlayerId != Seat && !StepWants(options) ? decline : null; // a Reaction the step asks for is yours to take
            else
            {
                var onItsOwnTurn = _engine.TutorialState.ActivePlayerId != Seat;
                var attacking = _engine.TutorialState.Phase != Phase.Action; // its own Attack and Dynasty phases are the lesson's to narrate
                if (!scripted && (onItsOwnTurn && attacking || _engine.TutorialState.Phase == Phase.Battle)) return; // and so is the first action of a battle
                pick = FoeChoice(options, scripted);
            }
            if (pick is null || !_engine.Execute(pick.Command!).Accepted) return;
        }
    }

    private bool StepWants(ActionOption[] options) => Current is { IsAction: true } step && options.Any(a => a.Type != "declineReaction" && step.Allow!(_lookup, a.Command!));

    /// <summary>The practice opponent's script: it declines reactions, passes, defends the province you attacked with whom it has, and, when the lesson asks, recruits (Daidoji Kenshi with the Clan Discount,
    /// the farms) and destroys your Follower with Never Afraid. It never does anything else, so the lesson meets exactly these plays.</summary>
    private ActionOption? FoeChoice(ActionOption[] options, bool scripted)
    {
        bool Plays(ActionOption a, string ability) => a.Type == "useAbility" && a.Command!.AbilityId == ability;
        ActionOption? Targeting(string ability, string definition, string? performer)
        {
            var option = options.FirstOrDefault(a => Plays(a, ability) && (performer is null || _lookup.Is(a.Command!.PerformerId ?? a.CardId, performer)));
            if (option?.TargetIds is not { Length: > 0 } targets) return null;
            var target = targets.FirstOrDefault(id => _lookup.DefinitionOf(id) == definition);
            return target is null ? null : option with { Command = option.Command! with { TargetId = target } };
        }
        ActionOption? Scripted(FoePurchase buy)
        {
            var option = options.FirstOrDefault(a => a.Type == "purchase" && _lookup.Is(a.Command!.CardId, buy.DefinitionId) && a.Command.ClanDiscount == buy.ClanDiscount && a.Command.ClanAllegiance == buy.ClanAllegiance);
            if (option is null) return null;
            var sources = buy.PaidBy.SelectMany(d => _lookup.Foe(d, Zone.Home, Zone.Stronghold).Take(1)).ToArray();
            return option with { Command = option.Command! with { SourceIds = sources } };
        }
        ActionOption? Attacks(int province)
        {
            if (options.FirstOrDefault(a => a.Type == "declareAttack") is { } declare) return declare;
            if (options.FirstOrDefault(a => a.Type == "attack") is not { } attack) return options.FirstOrDefault(a => a.Type == "selectBattle" && a.Command!.Province == province);
            var foe = 1 - Seat;
            var units = _engine.TutorialState.Cards.Values.Where(c => c.OwnerId == foe && c.Zone == Zone.Home && !c.Bowed && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Select(c => c.Id);
            return attack with { Command = attack.Command! with { Assignments = units.ToDictionary(id => id, _ => province) } };
        }
        ActionOption? Buys(string definition, bool discount) => options.FirstOrDefault(a => a.Type == "purchase" && a.Command!.ClanDiscount == discount && _lookup.Is(a.Command.CardId, definition));
        var script = _lesson.Foe;
        return (scripted && script.Purchases is { } plan ? options.FirstOrDefault(a => a.Type == "proclaim" && plan.Any(p => p.Proclaim)) : null) // a Proclaim the script asks for comes before declining the window it opens
            ?? options.FirstOrDefault(a => a.Type == "declineReaction")
            ?? options.FirstOrDefault(a => a.Type == "assignDefense" && _attackedProvince is int attacked && (a.Command!.Assignments?.Values.Contains(attacked) ?? a.Command.Province == attacked))
            ?? (scripted && script.AttackProvince is int province && _engine.TutorialState.ActivePlayerId != Seat ? Attacks(province) : null)
            ?? (scripted ? script.Abilities.Select((x, i) => Targeting(x.Ability, x.Target, script.Performers?.ElementAtOrDefault(i))).FirstOrDefault(o => o is not null) : null)
            ?? (scripted && script.Purchases is { } purchases ? purchases.Where(p => !_bought.Contains(p)).Select(p => { var o = Scripted(p); if (o is not null) _bought.Add(p); return o; }).FirstOrDefault(o => o is not null) : null)
            ?? (scripted && script.Purchases is null ? script.RecruitWithDiscount.Select(d => Buys(d, true)).Concat(script.Recruit.Select(d => Buys(d, false))).FirstOrDefault(o => o is not null) : null)
            ?? (scripted && script.Purchases is not null && script.Purchases.All(_bought.Contains) ? script.Discard?.Select(d => options.FirstOrDefault(a => a.Type == "discardProvince" && _lookup.Is(a.Command!.CardId, d))).FirstOrDefault(o => o is not null) : null)
            ?? options.FirstOrDefault(a => a.Type == "pass")
            ?? (scripted ? options.FirstOrDefault(a => a.Type == "endDynasty") : null);
    }
}
