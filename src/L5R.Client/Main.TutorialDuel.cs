using Godot;

namespace L5R.Client;

/// <summary>A lesson that reaches a duel (issue #1177) shows the real duel screen. The table, and the coach card that lives on it, are hidden while it is open, so the duel keeps its own copy of the coach card on its left;
/// it reads the same lesson, so a note's button and the lesson's own steps work exactly as they do at the table.</summary>
public partial class Main
{
    private TutorialCoachPanel? _duelCoach;

    /// <summary>Called once the duel's entrance has finished, and after every update while it is open: shows, updates or removes the coach card over the duel screen.</summary>
    private void SyncDuelCoach()
    {
        if (_matchDuelLayer is null || _matchDuel is null || _matchDuelPreparing || _session.Tutorial is not { } lesson)
        {
            DisposeDuelCoach(); return;
        }
        if (_duelCoach is null || !IsInstanceValid(_duelCoach))
        {
            _duelCoach = new TutorialCoachPanel { Continue = TutorialContinue, Back = TutorialBack, NextTutorial = TutorialNext };
            _matchDuelLayer.AddChild(_duelCoach);
        }
        var coach = lesson.Coach;
        if (_duelCoach.StepId != coach.StepId || !_duelCoach.Visible) _duelCoach.Present(coach);
        _duelCoach.Show(); LayoutDuelCoach();
        ReturnToTableForMove();
    }
    private string? _duelReturnedStep;

    /// <summary>A step that asks for a move at the table cannot be done from the duel screen: once the duel's result has been read, go back to the table for it.</summary>
    private void ReturnToTableForMove()
    {
        if (_session.Tutorial is not { Finished: false } lesson || _matchDuel is null || _matchDuelReturning || _matchDuelPreparing) return;
        var coach = lesson.Coach;
        if (coach.Button is null && _matchDuel.CanReturn && _matchDuel.Screen.CanContinue && !_matchDuel.Paused && _duelReturnedStep != coach.StepId)
        {
            _duelReturnedStep = coach.StepId; _ = ReturnFromMatchDuel();
        }
    }

    private void LayoutDuelCoach()
    {
        if (_duelCoach is not { Visible: true } panel) return;
        ReturnToTableForMove();
        var size = GetViewport().GetVisibleRect().Size; var scale = Math.Max(.5f, _root.Scale.X);
        panel.SetReady(!_matchDuel!.Screen.PresentationBusy && !_session.Busy);
        panel.Scale = new Vector2(scale, scale);
        panel.CustomMinimumSize = new Vector2(TutorialColumnWidth, 0);
        panel.Size = new Vector2(TutorialColumnWidth, Math.Max(300, size.Y / scale - 64 - 36));
        panel.Position = new Vector2(16, 64) * scale;
    }

    private void DisposeDuelCoach()
    {
        if (_duelCoach is not null && IsInstanceValid(_duelCoach)) _duelCoach.QueueFree();
        _duelCoach = null; _duelReturnedStep = null;
    }
}
