namespace L5R.Core;

// Issue #1115: "<action> is barred by <card>." -- ONE way to tell a player that a card's printed restriction stopped what they tried to do (or quietly stopped something that was about to happen).
//
//   * A REFUSED command: the engine rejects it with BarNotice.Message as the error (MatchEngine.RequireNotBarred), so the text travels over the existing error channel, local and online alike.
//   * A SUPPRESSED effect (an extra action that is not granted, a move that does not happen): nothing is rejected, so the engine emits a "barred" event whose Message is BarNotice.Message
//     (MatchEngine.EmitBarred), addressed to the player the bar affects.
//
// A client shows either one as an OK dialog by recognising the sentence with TryParse. The sentence is the whole contract: a card that bars something only has to say what and who.
public sealed record BarNotice(string Action, string Source)
{
    public const string EventType = "barred";
    private const string Middle = " is barred by ";

    /// <summary>The sentence the player reads: "Assigning to attack is barred by Shieldman."</summary>
    public string Message => $"{(Action.Length > 0 ? char.ToUpperInvariant(Action[0]) + Action[1..] : Action)}{Middle}{Source}.";

    /// <summary>The notice a refusal or "barred" event carries, or null when the message is any other sentence.</summary>
    public static BarNotice? TryParse(string? message)
    {
        if (message is null || !message.EndsWith('.')) return null;
        var split = message.IndexOf(Middle, StringComparison.Ordinal);
        if (split <= 0) return null;
        var source = message[(split + Middle.Length)..^1];
        return source.Length == 0 ? null : new(message[..split], source);
    }
}

public sealed partial class MatchEngine
{
    /// <summary>Require that refuses with the "barred by" sentence: <paramref name="action"/> is what the player tried ("assigning to attack"), <paramref name="source"/> the card whose text bars it.</summary>
    private static void RequireNotBarred(bool allowed, string action, string source) => Require(allowed, new BarNotice(action, source).Message);

    /// <summary>Tells the player a card stopped something without rejecting a command (the effect simply does not happen).</summary>
    private void EmitBarred(int playerId, string action, string source, string? cardId = null) => Emit(BarNotice.EventType, new BarNotice(action, source).Message, playerId, cardId);
}
