using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace L5R.Core;

/// <summary>
/// Issue #1358: the text printed on each published card's pinned printing (content/printed-text.json, written by tools/content/printed_text.py from the Oracle catalogues), so a viewer can
/// tell whether the engine's corrected text says something the scan does not. The comparison ignores what a scan and a sentence can differ by without changing meaning: case, punctuation,
/// markup, Oracle's symbols (":bow:" reads "Bow this card" or "Bow", ":g2:" reads "2 Gold"), reminder text in parentheses and Oracle's later Event preamble ("After you reveal cards in your
/// Provinces ... if this Event is face-up in one of them,"). A card with no known printing never matches, so its text is always shown.
/// </summary>
public static partial class PrintedText
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Printed = new(Load);

    /// <summary>The printed text of the card's pinned printing, or null when none is known.</summary>
    public static string? For(string cardId) => Printed.Value.TryGetValue(cardId, out var text) ? text : null;

    /// <summary>True when the engine's text reads as the printing does, so the scan already says it.</summary>
    public static bool Matches(CardDefinition definition) =>
        For(definition.Id) is { } printed && Normalize(printed) == Normalize(definition.Text);

    public static string Normalize(string text)
    {
        var t = Markup().Replace(text, "");
        t = Entity().Replace(t, "");
        t = Reminder().Replace(t, ""); // reminder text in parentheses is not a rule; the engine drops or adds it
        t = EventPreamble().Replace(t, ""); // Oracle's later Event framing, not on an Emperor-era printing
        t = t.Replace(":bow:", " bow ", StringComparison.Ordinal);
        t = BowThisCard().Replace(t, "bow"); // ":bow:" is written "Bow this card" or "Bow"
        t = GoldSymbol().Replace(t, m => $" {m.Groups[1].Value} gold ");
        var builder = new StringBuilder(t.Length);
        foreach (var c in t.ToLowerInvariant()) if (c is >= 'a' and <= 'z' or >= '0' and <= '9') builder.Append(c);
        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = typeof(PrintedText).Assembly.GetManifestResourceStream("L5R.PrintedText.json")
            ?? throw new InvalidOperationException("Missing embedded printed-text.json.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("cards").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex Markup();
    [GeneratedRegex("&[#a-zA-Z0-9]+;")] private static partial Regex Entity();
    [GeneratedRegex(":g(\\d+):")] private static partial Regex GoldSymbol();
    [GeneratedRegex("bow this card", RegexOptions.IgnoreCase)] private static partial Regex BowThisCard();
    [GeneratedRegex("\\([^()]*\\)")] private static partial Regex Reminder();
    [GeneratedRegex("After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them, ", RegexOptions.IgnoreCase)] private static partial Regex EventPreamble();
}
