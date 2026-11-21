using L5R.Client;
using L5R.Core;

// Issue #1214: the Hard practice opponent thinks for seconds, so it decides on a copy of the match on another thread while the table stays live. These cases pin the session half: the first call only
// starts the decision, nothing at the table changes until it is ready, a ready answer is played once, a refusal is reported at the position it was chosen for, and a new match drops a late answer.
internal static class PracticeOpponentTests
{
    public static async Task Run()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static long Version(MatchSession s) => s.Snapshot!.View.Version;
        static GameCommand Legal(ActionOption[] actions) => actions.First(a => a.Enabled && a.Command is not null && a.Type != "concede").Command!;
        static async Task ToOpponent(MatchSession s)
        {
            for (var i = 0; i < 600 && s.Snapshot!.View.PriorityPlayerId != 1; i++) await s.Submit(Legal(s.Snapshot.Actions));
            Check(s.Snapshot!.View.PriorityPlayerId == 1, "the opponent never got priority");
        }
        static void Wait(MatchSession s) { for (var i = 0; i < 500 && s.OpponentThinking; i++) Thread.Sleep(10); Check(!s.OpponentThinking, "the opponent's decision never finished"); }

        using var session = new MatchSession();
        session.StartCustomPractice(CustomPractice.Preset("Lion"), CustomPractice.Preset("Crane")); session.AllowIdleBattlePasses();
        await ToOpponent(session);
        var gate = new ManualResetEventSlim(); var thread = -1; var seen = 0L; var refusals = new List<long>();
        GameCommand? Choose(MatchView view, ActionOption[] actions, MatchEngine copy) { thread = Environment.CurrentManagedThreadId; seen = copy.Version; gate.Wait(); return Legal(actions); }
        void Refused(GameCommand command, string? error, long version) => refusals.Add(version);

        // The first call starts the decision on a worker; the real match is untouched until the answer is ready.
        var before = Version(session);
        Check(!session.StepPracticeOpponentAsync(Choose, Refused) && session.OpponentThinking, "the first call starts a decision and plays nothing");
        Check(!session.StepPracticeOpponentAsync(Choose, Refused) && Version(session) == before, "a decision still being made leaves the match alone");
        gate.Set(); Wait(session);
        Check(thread != Environment.CurrentManagedThreadId && seen == before, "the decision ran off this thread, on a copy of the match as it stood");
        Check(session.StepPracticeOpponentAsync(Choose, Refused) && Version(session) > before, "a ready answer is played");
        Check(refusals.Count == 0, "no refusal for a legal move");

        // A refused answer is reported against the position it was chosen for, and the opponent decides again.
        await ToOpponent(session); before = Version(session);
        GameCommand? Bogus(MatchView view, ActionOption[] actions, MatchEngine copy) => Legal(actions) with { Type = "bogus" };
        session.StepPracticeOpponentAsync(Bogus, Refused); Wait(session);
        Check(!session.StepPracticeOpponentAsync(Bogus, Refused) && refusals.SequenceEqual([before]) && Version(session) == before, "a refusal is reported at its position and changes nothing");
        Check(session.StepPracticeOpponentAsync(Bogus, Refused) is false && session.OpponentThinking, "the opponent then chooses again");
        Wait(session);

        // A new match drops an answer that was still being made for the old one.
        gate.Reset(); session.StartCustomPractice(CustomPractice.Preset("Lion"), CustomPractice.Preset("Crane")); var fresh = Version(session);
        Check(!session.StepPracticeOpponentAsync(Choose, Refused) && !session.OpponentThinking && Version(session) == fresh, "a new match has no pending decision");
        gate.Set();
    }
}
