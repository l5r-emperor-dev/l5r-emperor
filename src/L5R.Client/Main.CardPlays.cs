using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Issue #630: every timing, size and switch of the "card played" showcase in one place so it can be tuned in play.</summary>
internal static class CardPlayTiming
{
    /// <summary>Seconds the card eases in (fade, and a small grow unless reduced motion is on).</summary>
    public const double Intro = .42;
    /// <summary>Seconds a card played from a hand is held face-up before it travels to the discard pile.</summary>
    public const double Hold = 2.04;
    /// <summary>Seconds a card that stays in play (an ability of a card on the table) is held.</summary>
    public const double BoardHold = 1.44;
    /// <summary>Seconds the card takes to travel to the discard pile (or just fade, for a card that stays in play or under reduced motion).</summary>
    public const double Outro = .96;
    /// <summary>With reduced motion the card simply fades in, holds, and fades out, for this many seconds in all.</summary>
    public const double ReducedHold = 1.68;
    /// <summary>Hold is multiplied by this while more plays are waiting behind it.</summary>
    public const double QueuedHoldScale = .6;
    /// <summary>Card height in the 1920x1080 presentation space, and where its centre sits (over the table, left of the sidebar).</summary>
    public const float CardHeight = 440;
    public static readonly Vector2 Center = new(865, 500);
    /// <summary>False: only the other player's plays are shown (you already know what you just did).</summary>
    public const bool ShowOwnPlays = false;
    /// <summary>True: an ability used by a card that stays on the table (a Personality, Item, Holding...) is shown too, so the cause of a change is visible.</summary>
    public const bool ShowBoardAbilities = true;
}

// Issue #630 (Mendon's playtest): "when an opponent plays a card, show the card being played and then moved to the discard pile". Driven only by the
// public event log (strategyPlayed / abilityUsed carry the player and the card id) and the card's public face once it is played, so nothing hidden is
// shown early. The practice opponent waits while a beat is showing (PlayShowcaseBusy), so its next move cannot cut the card short.
public partial class Main
{
    /// <summary>Flip: the face-down card is turned over where it lies on the table (Cover covers it until its turn). Holdable: Paused Animation may stop on it (the flip of a card that merely stays in its province never does).</summary>
    /// <summary>Issue #1077: <paramref name="HoldSeconds"/> overrides how long the card is held (a tutorial Spell being attached: 8 seconds), and <paramref name="AnimateAttach"/> plays the slide out from under its Personality when the show ends.</summary>
    private sealed record PlayBeat(CardView Card, string Caption, bool Discarded, CardFlip? Cover = null, bool Holdable = true, double? HoldSeconds = null, bool AnimateAttach = false);

    /// <summary>Spells whose attachment is being shown (tutorial): the table's own slide waits for the show to end, then <see cref="AnimateAttachment"/> plays it.</summary>
    private readonly HashSet<string> _spellAttachShown = [];
    private const double SpellAttachHold = 8;

    // An attachment ("equipped") is not showcased: the large card would cover the very Personality it attaches to. It slides out from under that Personality on the table instead (Main.Motion.cs).
    // What an opponent playing a card looks like in the public log: a strategy or ability, a card brought into play (a purchase or "put into play"), an attachment, a Terrain, a Kata, an overlay.
    private static readonly HashSet<string> CardPlayEvents = ["strategyPlayed", "abilityUsed", "cardEnteredPlay", "terrainPlayed", "strategyEnteredPlay", "kataEnteredPlay", "personalityOverlayed"];
    // How an Event, Region or Celestial enters play: revealed in a province in the Events Phase, then resolved, attached or put in play. Shown for either player, since neither chose it.
    private static readonly HashSet<string> EntersPlayEvents = ["provinceRevealed", "eventEnteredPlay", "eventResolved", "regionAttached", "regionDiscarded", "celestialEnteredPlay"];

    private string? _playsMatchKey;
    private long _playsSeen = -1;
    private readonly Queue<PlayBeat> _playQueue = [];
    private Control? _playCurrent;
    /// <summary>Played cards this update is showcasing: the older quick discard flight skips them, so a card is not shown twice.</summary>
    private readonly HashSet<string> _showcasedPlays = [];

    /// <summary>Every beat that has started in this match, in order (the native checks read it).</summary>
    private readonly List<(string CardId, bool Flip)> _playsStarted = [];

    private bool PlayShowcaseBusy => _playQueue.Count > 0 || _playCurrent is not null && IsInstanceValid(_playCurrent);

    // Issue #660, "Paused Animation": with the preference On a played card is shown fully and stays until the player presses Proceed (button, Enter or
    // Esc). Only the local player's input is held (_playHeld gates card selection, actions and submits); the engine, a practice opponent (it already
    // waits on PlayShowcaseBusy) and an online opponent carry on, and further beats queue up and are held one at a time. With the preference Off nothing
    // below runs and the timing is the original one.
    private bool _playHeld;
    private Action? _playProceed;
    private Button? _proceedButton;
    private bool _proceedHintShown;

    private void SetPlayHeld(bool held)
    {
        if (_playHeld == held) return;
        _playHeld = held;
        // Actions/buttons are read from the engine view: draw them again (deferred, as this can be reached from inside Render).
        Callable.From(() => { if (_session.Snapshot is not null && IsInstanceValid(this)) Render(); }).CallDeferred();
    }

    /// <summary>Dismisses the card being held (Proceed, Enter or Esc). Idempotent: key auto-repeat or a double click only proceeds once.</summary>
    private bool ProceedCardPlay()
    {
        var proceed = _playProceed;
        if (proceed is null) return false;
        _playProceed = null;
        if (_playQueue.Count == 0) SetPlayHeld(false);
        proceed();
        return true;
    }

    private void ObserveCardPlays()
    {
        _showcasedPlays.Clear();
        if (_session.Snapshot is null) { ClearCardPlays(); return; }
        if (_playsMatchKey != _session.MatchKey)
        {
            // A new match (or a reconnect): never replay history, only what happens from here.
            ClearCardPlays();
            _playsMatchKey = _session.MatchKey; _playsSeen = View.Events.LastOrDefault()?.Sequence ?? -1;
            return;
        }
        var fresh = View.Events.Where(e => e.Sequence > _playsSeen).ToArray();
        if (fresh.Length > 0) _playsSeen = fresh[^1].Sequence;
        var cards = VisibleCards(View).Concat(View.Players.SelectMany(p => p.EventsInPlay ?? [])).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var beats = new Dictionary<string, PlayBeat>(); // insertion order is event order; Dictionary keeps it while nothing is removed
        var order = new List<string>();
        var claimedSlots = new HashSet<(int Player, int Province, int Slot)>();
        foreach (var e in fresh)
        {
            if (e.CardId is not string id || e.PlayerId is not int who || !cards.TryGetValue(id, out var card) || card.Definition is null) continue;
            var discarded = card.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Resolving; // issue #917: a card still resolving (paused on a choice) is on its way to the discard pile
            var entering = card.Definition.Type is CardType.Event or CardType.Region or CardType.Celestial && EntersPlayEvents.Contains(e.Type);
            var cover = e.Type == "provinceRevealed" && !beats.ContainsKey(id) ? CoverForFlip(card, who, claimedSlots) : null;
            if (entering || cover is not null)
            {
                // The later event of a reveal (resolved, attached, entered play) says more than "revealed", so it supplies the caption.
                if (beats.TryGetValue(id, out var shown)) { if (e.Type != "provinceRevealed") beats[id] = shown with { Caption = e.Message }; continue; }
                beats[id] = new(card, e.Message, discarded, cover, entering); order.Add(id);
                continue;
            }
            if (e.Type == "equipped" && TutorialActive && card.Definition.Type == CardType.Spell && card.AttachedTo is not null && !beats.ContainsKey(id))
            {
                beats[id] = new(card, e.Message, false, null, false, SpellAttachHold, true); order.Add(id); _spellAttachShown.Add(id);
                continue;
            }
            if (!CardPlayEvents.Contains(e.Type) || who == View.ViewerId && !CardPlayTiming.ShowOwnPlays || beats.ContainsKey(id)) continue;
            if (!discarded && !CardPlayTiming.ShowBoardAbilities) continue;
            beats[id] = new(card, e.Message, discarded); order.Add(id);
        }
        // A lesson step can limit the reveal animations to the cards it talks about; the others reveal quietly and are explained at their own step.
        var only = _session.Tutorial is { Finished: false } lesson ? lesson.Coach.AnimateOnly : null;
        if (only is not null)
            foreach (var id in order.Where(id => !only.Contains(beats[id].Card.Definition?.Id)).ToArray())
            {
                if (beats[id].Cover is { } quiet && IsInstanceValid(quiet)) quiet.QueueFree();
                order.Remove(id);
            }
        foreach (var id in order)
        {
            if (beats[id].Discarded) _showcasedPlays.Add(id);
            _playQueue.Enqueue(beats[id]);
        }
        if (_playCurrent is null || !IsInstanceValid(_playCurrent)) StartNextCardPlay();
    }

    /// <summary>"Flip Dynasty Cards": covers the face-down card where it lies on the table so it can be turned over in its turn. The cards are still hidden in the previous view (and in the tiles drawn
    /// from it, which this runs before replacing), so the slot is found there: a card that stays in its province by its own place, an Event/Region/Celestial (which leaves it) by being the next
    /// face-down slot of that player's reveal. Not under reduced motion.</summary>
    private CardFlip? CoverForFlip(CardView card, int who, HashSet<(int Player, int Province, int Slot)> claimed)
    {
        if (!(_preferences.FlipDynastyCards || TutorialActive) || _reducedMotion || CardTile.Artwork(card.Definition!) is not { } face) return null;
        if (_motionView is not { } prior || prior.ViewerId != View.ViewerId || prior.Players.FirstOrDefault(p => p.Id == who) is not { } before) return null;
        var hidden = before.Provinces.Where(p => !p.Destroyed)
            .SelectMany(p => ProvincePresentation.Slots(p).Where(s => s.Card is { Definition: null }).Select(s => (Province: p.Index, Slot: s.Index, Card: s.Card!)))
            .Where(h => !claimed.Contains((who, h.Province, h.Slot))).ToArray();
        (int Province, int Slot, CardView Card)? found = null;
        if (card.Zone == Zone.Province && card.ProvinceIndex is int index && View.Players.First(p => p.Id == who).Provinces.FirstOrDefault(p => p.Index == index) is { } now
            && ProvincePresentation.Slots(now).FirstOrDefault(s => s.Card?.Id == card.Id) is { } slot)
            found = hidden.Where(h => h.Province == index && h.Slot == slot.Index).Select(h => ((int, int, CardView)?)h).FirstOrDefault();
        else if (card.Zone != Zone.Province && hidden.Length > 0) found = hidden[0];
        if (found is not { } place) return null;
        var tile = _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Id == place.Card.Id);
        if (tile is null) return null;
        claimed.Add((who, place.Province, place.Slot));
        var slow = TutorialSlow; // the tutorial can ask for a reveal at a quarter of the speed
        var cover = new CardFlip { From = tile.FaceCornersOnViewport().Select(p => p / _root.Scale.X).ToArray(), Face = face, Back = TablePile.DynastyBack, Locate = () => _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Id == card.Id) ?? _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Id == place.Card.Id), Turn = .5 * slow, Linger = .45 * slow };
        _root.AddChild(cover);
        return cover;
    }

    private void ClearCardPlays()
    {
        foreach (var queued in _playQueue) if (queued.Cover is { } cover && IsInstanceValid(cover)) cover.QueueFree();
        _playQueue.Clear(); _playsStarted.Clear(); _spellAttachShown.Clear();
        if (_playCurrent is not null && IsInstanceValid(_playCurrent)) _playCurrent.QueueFree();
        _playCurrent = null; _playsMatchKey = null; _playsSeen = -1;
        _playProceed = null; _proceedButton = null; _proceedHintShown = false; SetPlayHeld(false);
    }

    private void StartNextCardPlay()
    {
        _playCurrent = null; _playProceed = null;
        if (_playQueue.Count == 0) { SetPlayHeld(false); return; }
        var beat = _playQueue.Dequeue();
        _playsStarted.Add((beat.Card.Id, beat.Cover is not null));
        if (beat.Cover is { } cover && IsInstanceValid(cover))
        {
            // Turned over on the table, in its own province, before anything else is shown of it.
            _playCurrent = cover;
            cover.Done = () =>
            {
                if (IsInstanceValid(cover)) cover.QueueFree();
                if (_playCurrent != cover) return;
                if (beat.Holdable) ShowBeat(beat); else StartNextCardPlay();
            };
            cover.Begin();
            return;
        }
        ShowBeat(beat);
    }

    private void ShowBeat(PlayBeat beat)
    {
        var paused = _preferences.PausedAnimation && beat.Holdable;
        if (paused) { SetPlayHeld(true); ReleaseTableFocus(); HideTableTooltip(); }
        var height = CardPlayTiming.CardHeight; var width = height * .72f;
        var stage = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 60, Modulate = new Color(1, 1, 1, 0) };
        _root.AddChild(stage);
        stage.Size = new Vector2(620, height + (paused ? 150 : 120)); stage.Position = CardPlayTiming.Center - new Vector2(stage.Size.X / 2, height / 2 + (paused ? 100 : 0)); // paused: up, so Proceed clears the hand
        var face = CardPreview.Face(beat.Card, height, width);
        face.MouseFilter = Control.MouseFilterEnum.Ignore;
        face.Size = new Vector2(width, height); face.Position = new Vector2((stage.Size.X - width) / 2, 0);
        face.PivotOffset = face.Size / 2;
        stage.AddChild(face);
        var caption = Palette.Text(beat.Caption, 17, Palette.Gold);
        caption.HorizontalAlignment = HorizontalAlignment.Center; caption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        caption.Position = new Vector2(10, height + 14); caption.Size = new Vector2(stage.Size.X - 20, paused ? 60 : 90);
        stage.AddChild(caption);
        _playCurrent = stage;

        var waiting = _playQueue.Count > 0 && !paused;
        var slow = TutorialSlow; // the tutorial can ask for a card to be shown for four times as long
        var hold = beat.HoldSeconds ?? (beat.Discarded ? CardPlayTiming.Hold : CardPlayTiming.BoardHold) * (waiting ? CardPlayTiming.QueuedHoldScale : 1) * slow;
        var tween = stage.CreateTween();
        if (_reducedMotion)
        {
            tween.TweenProperty(stage, "modulate:a", 1.0, .15);
            if (paused)
            {
                tween.TweenCallback(Callable.From(() => HoldCardPlay(stage, height, () =>
                {
                    if (!IsInstanceValid(stage)) return;
                    var fade = stage.CreateTween(); fade.TweenProperty(stage, "modulate:a", 0.0, .15); fade.TweenCallback(Callable.From(() => FinishCardPlay(stage)));
                })));
                return;
            }
            tween.TweenInterval(Math.Max(.2, beat.HoldSeconds ?? CardPlayTiming.ReducedHold * (waiting ? CardPlayTiming.QueuedHoldScale : 1) * slow - .3));
            tween.TweenProperty(stage, "modulate:a", 0.0, .15);
        }
        else
        {
            face.Scale = new Vector2(.7f, .7f);
            tween.SetParallel(true);
            tween.TweenProperty(stage, "modulate:a", 1.0, CardPlayTiming.Intro * slow);
            tween.TweenProperty(face, "scale", Vector2.One, CardPlayTiming.Intro * slow).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.SetParallel(false);
            // The outro is built when the hold ends so the pile's final position is read then, not when the card arrived.
            if (paused) tween.TweenCallback(Callable.From(() => HoldCardPlay(stage, height, () => PlayCardOutro(stage, face, caption, beat))));
            else { tween.TweenInterval(hold); tween.TweenCallback(Callable.From(() => PlayCardOutro(stage, face, caption, beat))); }
            return;
        }
        tween.TweenCallback(Callable.From(() => FinishCardPlay(stage, beat)));
    }

    /// <summary>The card is fully shown: float a Proceed button beneath it and wait. Enter reaches the button through ordinary GUI focus.</summary>
    private void HoldCardPlay(Control stage, float height, Action then)
    {
        if (!IsInstanceValid(stage) || _playCurrent != stage) return;
        var proceed = Palette.Button("Proceed", () => ProceedCardPlay(), true);
        _proceedButton = proceed; proceed.Name = "ProceedButton"; proceed.FocusMode = Control.FocusModeEnum.All; proceed.MouseFilter = Control.MouseFilterEnum.Stop;
        proceed.AddThemeFontSizeOverride("font_size", 22);
        proceed.Size = new Vector2(200, 46); proceed.Position = new Vector2((stage.Size.X - proceed.Size.X) / 2, height + 70);
        stage.AddChild(proceed);
        var hint = Palette.Text("Paused Animation is On. You can turn it off in Settings.", 15, Palette.PaperDim);
        hint.Name = "ProceedHint"; hint.HorizontalAlignment = HorizontalAlignment.Center; hint.MouseFilter = Control.MouseFilterEnum.Ignore; hint.Visible = false;
        hint.Position = new Vector2(10, height + 120); hint.Size = new Vector2(stage.Size.X - 20, 28);
        stage.AddChild(hint);
        // The first time the pointer reaches Proceed in a match, say where the setting lives. It never blocks the click.
        proceed.MouseEntered += () =>
        {
            if (_proceedHintShown) return;
            _proceedHintShown = true; hint.Visible = true;
            SetNotice("Paused Animation is On. You can turn it off in Settings.");
        };
        _playProceed = () => { proceed.Hide(); hint.Hide(); then(); };
        ReleaseTableFocus();
        Callable.From(() => { if (IsInstanceValid(proceed) && proceed.IsInsideTree()) proceed.GrabFocus(); }).CallDeferred();
    }

    private void PlayCardOutro(Control stage, Control face, Control caption, PlayBeat beat)
    {
        if (!IsInstanceValid(stage)) return;
        var pile = beat.Discarded ? TablePiles.FirstOrDefault(p => p.OwnerId == beat.Card.OwnerId && p.PileZone == beat.Card.Zone) : null;
        var tween = stage.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(caption, "modulate:a", 0.0, CardPlayTiming.Outro * .4);
        if (pile is not null)
        {
            // Travel to the pile's face on screen, shrinking as it goes, and fade out as it lands.
            var corners = PileCorners(pile);
            var target = corners.Aggregate(Vector2.Zero, (a, b) => a + b) / corners.Length;
            var pileWidth = (corners[1] - corners[0]).Length();
            var shrink = Mathf.Clamp(pileWidth / (CardPlayTiming.CardHeight * .72f), .1f, .6f);
            var faceCentre = stage.Position + face.Position + face.Size / 2;
            tween.TweenProperty(stage, "position", stage.Position + (target - faceCentre), CardPlayTiming.Outro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(face, "scale", new Vector2(shrink, shrink), CardPlayTiming.Outro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(stage, "modulate:a", 0.0, CardPlayTiming.Outro * .3).SetDelay(CardPlayTiming.Outro * .7);
        }
        else tween.TweenProperty(stage, "modulate:a", 0.0, CardPlayTiming.Outro * .6);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => FinishCardPlay(stage, beat)));
    }

    /// <summary>A Spell shown being attached (issue #1077) now slides out from under its Personality, as an attachment does on the table, so the player sees where it went.</summary>
    private void AnimateAttachment(string cardId)
    {
        _spellAttachShown.Remove(cardId);
        var tile = _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Id == cardId && _world.IsAncestorOf(t));
        if (tile is null || tile.Card.AttachedTo is not { } wielderId || tile.GetParent() is not Control stack) return;
        if (stack.GetChildren().OfType<CardTile>().FirstOrDefault(t => t.Card.Id == wielderId) is { } wielder && wielder.Position.DistanceTo(tile.Position) > .5f)
            tile.AddChild(new TableCardReflow { Card = tile, From = wielder.Position, To = tile.Position, ReducedMotion = () => _reducedMotion });
    }

    private void FinishCardPlay(Control stage, PlayBeat? beat = null)
    {
        if (beat is { AnimateAttach: true }) AnimateAttachment(beat.Card.Id);
        if (IsInstanceValid(stage)) stage.QueueFree();
        if (_playCurrent == stage) StartNextCardPlay();
    }
}
