using System.Text.RegularExpressions;

namespace L5R.Client;

/// <summary>One row of the collection file (issues #807, #1141): the set, card name, type, rarity, clan and copy limit of a printing. The catalogue itself has no printing data, so the
/// list shipped as Content/collection-printings.txt is what the collection file is read against. The set and rarity name the ownership bucket; Limit is U (Unique), S (Singular),
/// X (the card allows more than the usual number of copies) or N (normal).</summary>
public sealed record CollectionPrinting(string Set, string Name, string Type, string Rarity, string Clan = "", string Limit = "")
{
    /// <summary>The bucket's printing name: the set.</summary>
    public string Edition => Set;
    /// <summary>The ownership bucket suffix for this printing: "@Set" or "@Set@Rarity" (one card can be printed twice in a set at different rarities).</summary>
    public string Suffix => CollectionPrintings.Suffix(Set, Rarity);
}

/// <summary>The collection file format: <c>Set,Card Name,Card Type,Rarity,Clan,Limit,Count</c>, one row per printing, 0 where none is owned. Pure string logic, so the store and its tests need no Godot.</summary>
public static class CollectionPrintings
{
    public const string Header = "Set,Card Name,Card Type,Rarity,Clan,Limit,Count";

    /// <summary>Buckets recorded before issue #1141 named a printing by a short edition code; they are renamed to the set when a collection is read.</summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyEditions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BtD"] = "Before the Dawn",
        ["TA"] = "Torn Asunder",
        ["GoC"] = "Gates of Chaos",
        ["EE"] = "Emperor Edition",
        ["SC"] = "Second City",
        ["CoM"] = "Coils of Madness",
        ["AM"] = "Aftermath",
        ["EoW"] = "Embers of War",
        ["SoD"] = "Seeds of Decay",
        ["H&T"] = "Honor & Treachery",
        ["TSE"] = "The Shadow's Embrace",
        ["FL"] = "Forgotten Legacy",
        ["WoH"] = "War of Honor"
    };

    /// <summary>A bucket name with its printing's edition code, if it has one, spelled as the set.</summary>
    public static string Migrate(string bucket)
    {
        var parts = bucket.Split('@');
        if (parts.Length < 2 || !LegacyEditions.TryGetValue(parts[1], out var set)) return bucket;
        parts[1] = set; return string.Join("@", parts);
    }

    public static string Suffix(string edition, string rarity) => edition.Length == 0 ? "" : rarity.Length == 0 ? "@" + edition : "@" + edition + "@" + rarity;

    /// <summary>The card name as the catalogue spells it: curly quotes straightened, spaces collapsed, and the file's "(Exp)", "(Exp.)", "(Exp2)", "(Inexp)" notation turned into
    /// the catalogue's " - exp", " - exp2", " - inexperienced".</summary>
    public static string NormalizeName(string name)
    {
        var text = Regex.Replace(name.Replace('\u2019', '\'').Replace('\u2018', '\'').Replace('\u201C', '"').Replace('\u201D', '"').Replace('\u00A0', ' ').Trim(), @"\s+", " ");
        const string trailingType = @"(?:\s+(?:Personality|Item|Follower|Spell|Strategy|Holding|Event|Region|Ring|Stronghold|Celestial))?";
        var match = Regex.Match(text, @"^(.*?)\s*\((inexp|inexperienced|exp(?:erienced)?\.?\s*(\d*))\.?\)" + trailingType + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) match = Regex.Match(text, @"^(.*?[^-\s])\s+(inexperienced|experienced(?:\s+(\d+|CoM))?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return text;
        var title = match.Groups[1].Value.TrimEnd();
        if (match.Groups[2].Value.StartsWith("inexp", StringComparison.OrdinalIgnoreCase)) return title + " - inexperienced";
        var level = match.Groups[3].Value;
        return title + " - exp" + (level is "" or "1" || level.Equals("CoM", StringComparison.OrdinalIgnoreCase) ? "" : level); // Coils of Madness' "Experienced CoM" is the catalogue's plain " - exp"
    }

    /// <summary>Case- and apostrophe-insensitive comparison key for names.</summary>
    public static string Key(string name) => NormalizeName(name).ToLowerInvariant();

    /// <summary>Reads the printing list: a header naming at least Set and Card Name, then one row per printing.</summary>
    public static CollectionPrinting[] Parse(string csv)
    {
        var records = CollectionCsv.Parse(csv);
        if (records.Count < 2) return [];
        var header = records[0].Select(Heading).ToArray();
        int Col(string name) => Array.IndexOf(header, name);
        int set = Col("set"), name = Col("card name"), type = Col("card type"), rarity = Col("rarity"), clan = Col("clan"), limit = Col("limit");
        if (name < 0 || set < 0) throw new FormatException("The printing list needs Set and Card Name columns.");
        string Field(string[] r, int i) => i < 0 || i >= r.Length ? "" : r[i].Trim();
        return records.Skip(1).Where(r => r.Any(f => f.Trim().Length > 0)).Select(r =>
            new CollectionPrinting(Field(r, set), Field(r, name), Field(r, type), Field(r, rarity), Field(r, clan), Field(r, limit))).ToArray();
    }

    public static string Heading(string text) => text.Trim().TrimStart('\uFEFF').ToLowerInvariant();

    /// <summary>True when the header row is the per-printing file (it has Set, Card Name and Count) rather than the older card_id/card_name/owned_count file.</summary>
    public static bool IsPrintingFile(IEnumerable<string> header)
    {
        var names = header.Select(Heading).ToArray();
        return names.Contains("card name") && names.Contains("set") && names.Contains("count");
    }

    /// <summary>Decodes a file the way a spreadsheet saved it: UTF-8 (with or without a BOM) when it is valid, otherwise Windows-1252.</summary>
    public static string Decode(byte[] bytes)
    {
        try { return new System.Text.UTF8Encoding(false, true).GetString(bytes); }
        catch (System.Text.DecoderFallbackException)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(1252).GetString(bytes);
        }
    }
}
