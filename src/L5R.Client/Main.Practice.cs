using Godot;
using L5R.AI;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private CanvasLayer? _practiceLayer;
    private bool PracticeVisible => _practiceLayer is not null && IsInstanceValid(_practiceLayer);
    private void ClosePracticeSetup()
    {
        _practiceLayer?.QueueFree(); _practiceLayer = null;
        _opponentClock = 0;
        if (!MenuVisible) Render();
    }
    /// <summary>Issue #1214: one decision of the Hard opponent, on a worker thread. <paramref name="copy"/> is a private copy of the match: the sampler redeals every card hidden from the bot's seat on it, so the bot
    /// reasons about worlds consistent with what it could see, never the real hidden cards. A failure plays the Champion's move instead, so the opponent always acts.</summary>
    private static GameCommand? ChooseHard(MatchView view, ActionOption[] actions, MatchEngine copy, BotContext memory)
    {
        if (view.PendingDuel is not null) return DuelOpponent.Choose(view, actions);
        try { memory.Sampler = seed => copy.Sample(view.ViewerId, seed); return TrainingOpponent.Choose(view, actions, memory).Command; }
        catch (Exception e) when (e is not OutOfMemoryException) { GD.PushWarning("Hard opponent fell back to the Champion: " + e.Message); return TrainingOpponent.Choose(view, actions, new BotContext()).Command; }
    }
    private void ShowPracticeSetup()
    {
        if (PracticeVisible) return;
        HideTableTooltip(); ReleaseTableFocus(); _cameraDragging = false;
        _practiceLayer = new CanvasLayer { Layer = 90 }; AddChild(_practiceLayer);
        var screen = new PracticeSetupScreen { Profile = _preferencesProfile }; _practiceLayer.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.Closed += ClosePracticeSetup;
        screen.StartMatch = (your, opponent) =>
        {
            void Begin()
            {
                try
                {
                    // Core validates before the active session is disconnected.
                    _session.StartCustomPractice(your, opponent); _session.AllowIdleBattlePasses(); _botLevel = screen.Difficulty; _botMemory = null;
                    ResetMatchPresentation(); _autoOpponent = true; ClosePracticeSetup(); EnterTable();
                }
                catch (ArgumentException e) { var (dialog, body) = Dialog("Deck needs attention", 660, 380); body.AddChild(Palette.Wrapped(e.Message, 22)); }
            }
            if (_session.Online || _session.Snapshot is not null)
                Confirm("Start a new Practice?", _session.Online ? "Leave your private table and start Practice? This disconnects your seat; it does not concede." : "Replace the current Practice? Export a replay first if you want to keep it.", Begin);
            else Begin();
        };
    }
}
