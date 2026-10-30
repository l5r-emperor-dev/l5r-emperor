namespace L5R.Core;

/// <summary>Rules shared by deck construction and match play for the levelled Experienced keyword.</summary>
public static class Experienced
{
    /// <summary>The keyword Coils of Madness prints as "Experienced" with a superscript "CoM" (Bayushi Kachiko, Isawa Tsuke): the
    /// catalogue spells it as one word. It is the Experienced keyword at level 1 (issue #672, P23).</summary>
    public const string CoilsOfMadnessKeyword = "ExperiencedCoM";

    /// <summary>Returns zero without Experienced, one for Experienced (and for the Coils of Madness form
    /// "ExperiencedCoM"), and the printed number for Experienced N. If more than one form is present, the
    /// highest level applies. "Inexperienced" refers to an experience level of -1 (Comprehensive Rules, Glossary:
    /// Experienced/Inexperienced), so a version of its title with no experience keyword may overlay it and it may overlay nothing.</summary>
    public static int Level(IEnumerable<string>? keywords)
    {
        var level = 0; var list = keywords?.ToArray() ?? [];
        foreach (var raw in list)
        {
            var keyword = raw.Trim();
            if (keyword.Equals("Experienced", StringComparison.OrdinalIgnoreCase) || IsCoilsOfMadnessForm(keyword)) level = Math.Max(level, 1);
            else if (keyword.StartsWith("Experienced ", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(keyword[12..].Trim(), out var parsed) && parsed > 0)
                level = Math.Max(level, parsed);
        }
        return level == 0 && IsInexperienced(list) ? -1 : level;
    }

    /// <summary>True for a card that prints the Inexperienced keyword (level -1, below a card with no version keyword at all).</summary>
    public static bool IsInexperienced(IEnumerable<string>? keywords) =>
        (keywords ?? []).Any(k => k.Trim().Equals("Inexperienced", StringComparison.OrdinalIgnoreCase));

    // "ExperiencedCoM", or the same two words with a space between them.
    private static bool IsCoilsOfMadnessForm(string keyword) =>
        keyword.Equals(CoilsOfMadnessKeyword, StringComparison.OrdinalIgnoreCase) || keyword.Equals("Experienced CoM", StringComparison.OrdinalIgnoreCase);

    private const string InexperiencedSuffix = " - inexperienced";

    /// <summary>Returns the shared in-game title after removing the version notation used by the
    /// imported catalogue and deck lists. Experience versions remain distinct construction cards,
    /// but Overlay compares this title.</summary>
    public static string BaseTitle(string title)
    {
        var trimmed = title.Trim();
        // "Hida O-Ushi - inexperienced" is the catalogue's name for the Inexperienced version of "Hida O-Ushi".
        if (trimmed.EndsWith(InexperiencedSuffix, StringComparison.OrdinalIgnoreCase)) return trimmed[..^InexperiencedSuffix.Length].TrimEnd();
        var legacy = trimmed.LastIndexOf(" - exp", StringComparison.OrdinalIgnoreCase);
        if (legacy >= 0 && trimmed[(legacy + 6)..].All(char.IsDigit)) return trimmed[..legacy].TrimEnd();

        var experienced = trimmed.LastIndexOf(" Experienced", StringComparison.OrdinalIgnoreCase);
        if (experienced >= 0)
        {
            var suffix = trimmed[(experienced + 12)..].Trim();
            if (suffix.Length == 0 || suffix.All(char.IsDigit)) return trimmed[..experienced].TrimEnd();
        }
        return trimmed;
    }

    /// <summary>Construction identity distinguishes otherwise same-titled cards by level. This also
    /// applies to non-Personalities, whose Experienced keyword has no in-match effect.</summary>
    public static string DeckName(string title, IEnumerable<string>? keywords)
    {
        var keywordList = keywords?.ToArray() ?? [];
        var level = Level(keywordList);
        // Imported legacy lists already use "- exp"/"- expN" to distinguish versions. Keep that
        // stable instead of appending a second qualifier while still canonicalizing raw titles.
        var legacy = title.LastIndexOf(" - exp", StringComparison.OrdinalIgnoreCase);
        if (legacy >= 0 && title[(legacy + 6)..].All(char.IsDigit)) return title;
        if (level == 1 && title.EndsWith(" Experienced", StringComparison.OrdinalIgnoreCase)) return title;
        if (level > 1 && title.EndsWith($" Experienced {level}", StringComparison.OrdinalIgnoreCase)) return title;
        // A card whose level 1 comes only from the Coils of Madness form keeps its catalogue name: no other version of its title is in the
        // pool, and renaming it would turn every saved deck that lists it into one naming an unknown card (issue #672, P23).
        if (level == 1 && !keywordList.Any(k => k.Trim().Equals("Experienced", StringComparison.OrdinalIgnoreCase))) return title;
        return level switch
        {
            <= 0 => title,
            1 => $"{title} Experienced",
            _ => $"{title} Experienced {level}"
        };
    }
}
