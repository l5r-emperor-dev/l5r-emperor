using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #1115: a card's restriction stopped what the player tried to do ("Assigning to attack ... is barred by Shieldman."). Core words it once (BarNotice) and sends it two ways: as the
// refusal of a command (MatchSession.Notice) and as a "barred" event for an effect that quietly did not happen. Either way the player gets this OK dialog, one at a time.
public partial class Main
{
    private readonly Queue<BarNotice> _barQueue = new();
    private AcceptDialog? _barDialog;
    private BarNotice? _barShown;
    private string? _barEventsMatch;
    private int _barEventsSeen;

    private void ShowBarred(BarNotice bar)
    {
        if (_barShown == bar || _barQueue.Contains(bar)) return;
        _barQueue.Enqueue(bar);
        if (_barDialog is null) ShowNextBarred();
    }

    private void ShowNextBarred()
    {
        _barDialog = null; _barShown = null;
        if (!_barQueue.TryDequeue(out var bar)) return;
        var (dialog, body) = Dialog($"Barred by {bar.Source}", 560, 220);
        body.AddChild(Palette.Wrapped(bar.Message, 20, Palette.Paper));
        dialog.GetOkButton().Text = "OK";
        _barDialog = dialog; _barShown = bar;
        dialog.TreeExited += () => { if (_barDialog == dialog) ShowNextBarred(); };
    }

    /// <summary>Shows the "barred" events addressed to the viewer that arrived since the last look. A match's first look only records how many there are: a reconnect or a restored game does not replay old ones.</summary>
    private void ObserveBarredEvents()
    {
        var events = View.Events;
        if (_barEventsMatch != _session.MatchKey) { _barEventsMatch = _session.MatchKey; _barEventsSeen = events.Length; return; }
        for (var i = _barEventsSeen; i < events.Length; i++)
            if (events[i] is { Type: BarNotice.EventType, PlayerId: { } who } e && who == View.ViewerId && BarNotice.TryParse(e.Message) is { } bar) ShowBarred(bar);
        _barEventsSeen = events.Length;
    }
}
