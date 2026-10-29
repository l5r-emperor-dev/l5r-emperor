using System.Globalization;
using System.Text;
using System.Text.Json;
using L5R.Core;

namespace L5R.Client;

/// <summary>Local physical ownership only; no game legality, printings or deck files are changed.</summary>
public sealed record CollectionIdentity(string Id, string Name, string VersionName);
public enum OwnershipFilter { All, Owned, Unowned }
public enum CollectionImportMode { UpdateListed, Replace }
public sealed class CollectionImportRow
{
    public int Line { get; init; }
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public string? Id { get; set; }
    public string Error { get; init; } = "";
    public CollectionIdentity[] Candidates { get; init; } = [];
    public bool Excluded { get; set; }
    /// <summary>True for a row of the per-printing file (issue #807); Edition and Rarity then say which printing it counts.</summary>
    public bool Printing { get; init; }
    public string Edition { get; init; } = "";
    public string Rarity { get; init; } = "";
    /// <summary>Set when the name matched no card exactly but one card is clearly the one meant (a typo, a missing letter); says which and how sure. The row is applied to that card.</summary>
    public string Guess { get; set; } = "";
    /// <summary>The ownership bucket this row sets: the card's ID alone for the older file, or ID with the printing suffix.</summary>
    public string? Bucket => Id is null ? null : Printing ? Id + CollectionPrintings.Suffix(Edition, Rarity) : Id;
    public string PrintingLabel => Edition.Length == 0 ? "" : Rarity.Length == 0 ? Edition : Edition + " · " + Rarity;
}
public sealed class CollectionImportPlan
{
    public long Revision { get; init; }
    public CollectionImportRow[] Rows { get; init; } = [];
    /// <summary>Rows not read because their Count is blank or "-".</summary>
    public int Skipped { get; init; }
    public string[] Problems => Rows.Where(r => !r.Excluded && (r.Error.Length > 0 || r.Id is null))
        .Select(r => $"Row {r.Line}: {(r.Error.Length > 0 ? r.Error : "choose a card version")}").Concat(
            Rows.Where(r => !r.Excluded && r.Id is not null).GroupBy(r => r.Bucket)
                .Where(g => g.Count() > 1).Select(g => $"Repeated card {g.First().Name}{(g.First().Printing && g.First().PrintingLabel.Length > 0 ? " (" + g.First().PrintingLabel + ")" : "")}; exclude a duplicate or correct the CSV.")).ToArray();
    public bool CanApply => Rows.Any(r => !r.Excluded) && Problems.Length == 0;
    /// <summary>Leaves out every row still waiting for a decision (an unknown name, a version not chosen), so the rest can be applied.</summary>
    public void ExcludeUnresolved() { foreach (var row in Rows.Where(r => r.Error.Length > 0 || r.Id is null)) row.Excluded = true; }
    public bool HasUnresolved => Rows.Any(r => !r.Excluded && (r.Error.Length > 0 || r.Id is null));
}
public sealed class MyCollectionStore
{
    public const int MaximumCount = 99999;
    private sealed record FileData(int SchemaVersion, Dictionary<string, int> Counts);
    private readonly string _path, _undoPath;
    // Ownership buckets (issue #807): a card's ID alone is copies of no recorded printing; "ID@EDITION" or "ID@EDITION@Rarity" are copies of that printing. The card's total is the sum.
    private Dictionary<string, int> _counts = [];
    private Dictionary<string, int> _totals = [];
    public IReadOnlyDictionary<string, int> Counts => _totals.AsReadOnly();
    /// <summary>Every bucket, printing by printing.</summary>
    public IReadOnlyDictionary<string, int> Buckets => _counts.AsReadOnly();
    public long Revision { get; private set; }
    public string LoadError { get; private set; } = "";
    public bool CanUndo => LoadError.Length == 0 && File.Exists(_undoPath);
    public event Action? Changed;
    public MyCollectionStore(string path)
    {
        _path = path; _undoPath = path + ".previous";
        try { if (File.Exists(path)) { _counts = Read(path); _totals = Totals(_counts); } }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
        { LoadError = "Collection could not be loaded. The original file is preserved: " + e.Message; }
    }
    public int Count(string id) => _totals.GetValueOrDefault(id);
    /// <summary>Copies recorded against one bucket (a card ID with or without its printing suffix).</summary>
    public int CountOf(string bucket) => _counts.GetValueOrDefault(bucket);
    public static string CardOf(string bucket) { var at = bucket.IndexOf('@'); return at < 0 ? bucket : bucket[..at]; }
    private static Dictionary<string, int> Totals(Dictionary<string, int> buckets)
    {
        var totals = new Dictionary<string, int>();
        foreach (var (bucket, count) in buckets) { var id = CardOf(bucket); totals[id] = totals.GetValueOrDefault(id) + count; }
        return totals;
    }
    public bool Includes(string id, OwnershipFilter filter) => filter == OwnershipFilter.All || (filter == OwnershipFilter.Owned ? Count(id) > 0 : Count(id) == 0);
    public void Set(string id, int count)
    {
        if (count is < 0 or > MaximumCount) throw new FormatException($"Owned count must be 0–{MaximumCount}.");
        if (string.IsNullOrWhiteSpace(id)) throw new FormatException("A catalogue card ID is required.");
        var current = Count(id);
        if (current == count) return;
        // The total is what is edited. Extra copies are recorded against no printing; fewer copies come off the unassigned ones first, then printing by printing from the last.
        var next = new Dictionary<string, int>(_counts);
        if (count > current) next[id] = next.GetValueOrDefault(id) + count - current;
        else
        {
            var remove = current - count;
            foreach (var bucket in next.Keys.Where(b => CardOf(b) == id).OrderByDescending(b => b == id).ThenByDescending(b => b, StringComparer.Ordinal).ToArray())
            {
                if (remove == 0) break;
                var take = Math.Min(remove, next[bucket]); remove -= take;
                if (take == next[bucket]) next.Remove(bucket); else next[bucket] -= take;
            }
        }
        Commit(next);
    }
    public void Undo()
    {
        EnsureWritable();
        if (!CanUndo) return;
        var previous = Read(_undoPath); Write(_path, previous); _counts = previous; _totals = Totals(previous);
        File.Delete(_undoPath); Revision++; Changed?.Invoke();
    }
    public void Apply(CollectionImportPlan plan, CollectionImportMode mode)
    {
        EnsureWritable();
        if (plan.Revision != Revision) throw new InvalidOperationException("Your collection changed after this preview. Reopen the CSV to review current totals.");
        if (plan.Rows.Any(r => !r.Excluded && (r.Count is < 0 or > MaximumCount || !r.Candidates.Any(c => c.Id == r.Id)))) throw new FormatException("Invalid resolved card or quantity.");
        if (!plan.CanApply) throw new FormatException("Resolve or explicitly exclude every invalid/ambiguous row before applying.");
        var next = mode == CollectionImportMode.Replace ? new Dictionary<string, int>() : new Dictionary<string, int>(_counts);
        var rewritten = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in plan.Rows.Where(r => !r.Excluded))
        {
            if (!row.Printing)
            {
                // The older file states a card's total: every printing it was recorded against is replaced by that one figure.
                foreach (var bucket in next.Keys.Where(b => CardOf(b) == row.Id).ToArray()) next.Remove(bucket);
                if (row.Count > 0) next[row.Id!] = row.Count;
                continue;
            }
            // A card listed by printing is described by the file: copies recorded against no printing are replaced by the printings it lists.
            if (rewritten.Add(row.Id!)) next.Remove(row.Id!);
            if (row.Count == 0) next.Remove(row.Bucket!); else next[row.Bucket!] = row.Count;
        }
        Commit(next);
    }
    private void EnsureWritable() { if (LoadError.Length > 0) throw new IOException(LoadError); }
    private void Commit(Dictionary<string, int> next)
    {
        EnsureWritable();
        if (_counts.Count == next.Count && _counts.All(kv => next.GetValueOrDefault(kv.Key) == kv.Value)) return;
        Write(_undoPath, _counts); Write(_path, next); _counts = next; _totals = Totals(next); Revision++; Changed?.Invoke();
    }
    private static Dictionary<string, int> Read(string path)
    {
        if (new FileInfo(path).Length > 4_000_000) throw new FormatException("Collection file exceeds 4 MB.");
        var data = JsonSerializer.Deserialize<FileData>(File.ReadAllText(path)) ?? throw new FormatException("Empty collection file.");
        if (data.SchemaVersion is not (1 or 2) || data.Counts is null || data.Counts.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value is < 0 or > MaximumCount))
            throw new FormatException("Invalid collection schema or quantities.");
        return data.Counts.Where(kv => kv.Value > 0).GroupBy(kv => CollectionPrintings.Migrate(kv.Key)).ToDictionary(g => g.Key, g => Math.Min(MaximumCount, g.Sum(kv => kv.Value)));
    }
    private static void Write(string path, Dictionary<string, int> counts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, new FileData(2, counts)); file.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public CollectionImportPlan Preview(string csv, CollectionIdentity[] cards, CollectionPrinting[]? printings = null)
    {
        var records = CollectionCsv.Parse(csv);
        if (records.Count < 2) throw new FormatException("CSV needs a header and at least one card row.");
        var header = records[0].Select(s => s.Trim().TrimStart('\uFEFF').ToLowerInvariant()).ToArray();
        if (header.Distinct().Count() != header.Length) throw new FormatException("CSV has repeated header columns.");
        if (CollectionPrintings.IsPrintingFile(header)) return PreviewPrintings(records, header, cards, printings);
        var idIndex = Array.IndexOf(header, "card_id"); var nameIndex = Array.IndexOf(header, "card_name");
        var countIndex = Array.IndexOf(header, "owned_count");
        if ((idIndex < 0 && nameIndex < 0) || countIndex < 0) throw new FormatException("Use card_id and/or card_name, plus owned_count. Download the template.");
        var rows = new List<CollectionImportRow>();
        for (var i = 1; i < records.Count; i++)
        {
            var fields = records[i]; if (fields.All(string.IsNullOrWhiteSpace)) continue;
            string Field(int index) => index < 0 || index >= fields.Length ? "" : fields[index].Trim();
            var id = Field(idIndex); var name = Field(nameIndex); var error = "";
            if (fields.Length != header.Length) error = "Column count does not match the header.";
            if (!int.TryParse(Field(countIndex), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count is < 0 or > MaximumCount)
                error = $"Owned count must be a whole number from 0 to {MaximumCount}.";
            var candidates = id.Length > 0 ? cards.Where(c => c.Id == id).ToArray() :
                cards.Where(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || c.VersionName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (id.Length > 0 && name.Length > 0 && candidates.Length == 1 &&
                !candidates[0].Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !candidates[0].VersionName.Equals(name, StringComparison.OrdinalIgnoreCase))
                error = "Card name does not match this catalogue ID.";
            if (candidates.Length == 0 && error.Length == 0) error = id.Length > 0 ? "Unknown catalogue ID." : "Unknown card name.";
            rows.Add(new CollectionImportRow
            {
                Line = i + 1,
                Name = name.Length > 0 ? name : id,
                Count = count,
                Id = error.Length == 0 && candidates.Length == 1 ? candidates[0].Id : null,
                Error = error,
                Candidates = candidates
            });
        }
        if (rows.Count == 0) throw new FormatException("The CSV contains no card rows.");
        return new CollectionImportPlan { Revision = Revision, Rows = rows.ToArray() };
    }
    // The collection file (issues #807, #1141): Set, Card Name, Card Type, Rarity, Clan, Limit, Count. A Count of "-" or nothing is not read; 0 clears that printing.
    private CollectionImportPlan PreviewPrintings(List<string[]> records, string[] header, CollectionIdentity[] cards, CollectionPrinting[]? printings)
    {
        int Col(string name) => Array.IndexOf(header, name);
        int nameIndex = Col("card name"), editionIndex = Col("set"), rarityIndex = Col("rarity"), countIndex = Col("count");
        var editions = printings?.Select(p => p.Set).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(e => e, e => e, StringComparer.OrdinalIgnoreCase);
        var byKey = cards.ToLookup(c => CollectionPrintings.Key(c.VersionName));
        var byBase = cards.ToLookup(c => CollectionPrintings.Key(c.Name));
        var rows = new List<CollectionImportRow>(); var skipped = 0;
        // The names no card has: ranked once each, side by side, before the rows are read (a file lists a name once per printing).
        var unknown = records.Skip(1).Where(r => r.Length > Math.Max(nameIndex, countIndex) && r[countIndex].Trim() is not ("" or "-" or "0")).Select(r => r[nameIndex])
            .Where(name => name.Trim().Length > 0 && !byKey[CollectionPrintings.Key(name)].Any() && !byBase[CollectionPrintings.Key(name)].Any()).Select(CollectionPrintings.NormalizeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var closest = new System.Collections.Concurrent.ConcurrentDictionary<string, (CollectionIdentity Item, double Score)[]>(StringComparer.OrdinalIgnoreCase);
        var levels = cards.ToDictionary(c => c, c => DeckTroubleshooter.SplitExperience(c.VersionName).Level);
        Parallel.ForEach(unknown, name => closest[name] = DeckTroubleshooter.Rank(name, cards, c => c.Name, c => c.VersionName, c => levels[c]));
        for (var i = 1; i < records.Count; i++)
        {
            var fields = records[i]; if (fields.All(string.IsNullOrWhiteSpace)) continue;
            string Field(int index) => index < 0 || index >= fields.Length ? "" : fields[index].Trim();
            var rawCount = Field(countIndex);
            if (rawCount is "" or "-") { skipped++; continue; }
            var name = Field(nameIndex); var edition = Field(editionIndex); var error = "";
            if (fields.Length != header.Length) error = "Column count does not match the header.";
            if (!int.TryParse(rawCount, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count is < 0 or > MaximumCount)
                error = $"Count must be a whole number from 0 to {MaximumCount}, .";
            if (error.Length == 0 && edition.Length > 0 && editions is not null)
            { if (editions.TryGetValue(edition, out var known)) edition = known; else error = $"Unknown set {edition}."; }
            var key = CollectionPrintings.Key(name);
            var candidates = byKey[key].ToArray(); var exact = candidates.Length == 1; var guess = "";
            if (candidates.Length == 0) candidates = byBase[key].ToArray();
            if (candidates.Length == 0 && error.Length == 0 && !(rawCount == "0"))
            {
                // No card of that name: the closest by the deck troubleshooter's scoring (typos, a missing letter). One clear winner of the Experience level the row names is used and shown for
                // checking; anything less sure is offered as the closest cards to choose from.
                var wanted = DeckTroubleshooter.SplitExperience(CollectionPrintings.NormalizeName(name)).Level;
                var ranked = closest.GetValueOrDefault(CollectionPrintings.NormalizeName(name)) ?? [];
                candidates = ranked.Select(r => r.Item).ToArray();
                if (ranked.Length > 0 && ranked[0].Score >= .9 && (ranked.Length == 1 || ranked[0].Score - ranked[1].Score >= .06) && DeckTroubleshooter.SplitExperience(ranked[0].Item.VersionName).Level == wanted)
                { exact = true; guess = $"Matched \"{ranked[0].Item.VersionName}\" by closest name"; }
            }
            // A printing the catalogue cannot name or pick a version for, listed as 0 owned (every unowned row of the template), has nothing to record and nothing to review.
            if (error.Length == 0 && rawCount == "0" && !exact) { skipped++; continue; }
            if (candidates.Length == 0 && error.Length == 0) error = "Unknown card name.";
            rows.Add(new CollectionImportRow
            {
                Line = i + 1,
                Name = name,
                Count = count,
                Id = error.Length == 0 && exact ? candidates[0].Id : null, // a title that matches only other versions is never guessed
                Guess = guess,
                Error = error,
                Candidates = candidates,
                Printing = true,
                Edition = edition,
                Rarity = Field(rarityIndex)
            });
        }
        if (rows.Count == 0) throw new FormatException("The CSV contains no collectible card rows.");
        // A misspelled row whose card and printing the file also lists under the right spelling is that same row twice: the guess is left out.
        var spelledRight = rows.Where(r => r.Guess.Length == 0 && r.Id is not null).Select(r => r.Bucket).ToHashSet();
        foreach (var row in rows.Where(r => r.Guess.Length > 0 && spelledRight.Contains(r.Bucket))) { row.Excluded = true; row.Guess += "; the file lists this printing again under the right name, so this row is left out"; }
        return new CollectionImportPlan { Revision = Revision, Rows = rows.ToArray(), Skipped = skipped };
    }
    /// <summary>The collection as the per-printing file: every printing of the list in its own order with the copies owned (0 when none), then a row for any
    /// copies the list has no printing for (recorded against no printing, or a card the list does not name).</summary>
    public string Export(CollectionIdentity[] cards, CollectionPrinting[] printings)
    {
        var byKey = cards.ToLookup(c => CollectionPrintings.Key(c.VersionName));
        var rows = new List<string[]>(); var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var printing in printings)
        {
            var matches = byKey[CollectionPrintings.Key(printing.Name)].ToArray();
            var bucket = matches.Length == 1 ? matches[0].Id + printing.Suffix : null;
            var count = bucket is not null && written.Add(bucket) ? CountOf(bucket).ToString(CultureInfo.InvariantCulture) : "0";
            rows.Add([printing.Set, printing.Name, printing.Type, printing.Rarity, printing.Clan, printing.Limit, count]);
        }
        foreach (var (bucket, owned) in _counts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (written.Contains(bucket)) continue;
            var id = CardOf(bucket); var parts = bucket.Split('@');
            var name = cards.FirstOrDefault(c => c.Id == id)?.VersionName ?? id;
            rows.Add([parts.Length > 1 ? parts[1] : "", name, "", parts.Length > 2 ? parts[2] : "", "", "", owned.ToString(CultureInfo.InvariantCulture)]);
        }
        return CollectionPrintings.Header + "\r\n" + string.Concat(rows.Select(row => string.Join(",", row.Select(f => "\"" + f.Replace("\"", "\"\"") + "\"")) + "\r\n"));
    }
    /// <summary>The empty collection file: every printing of the list with 0 owned.</summary>
    public static string Template(CollectionPrinting[] printings) => CollectionPrintings.Header + "\r\n" + string.Concat(printings.Select(p =>
        string.Join(",", new[] { p.Set, p.Name, p.Type, p.Rarity, p.Clan, p.Limit, "0" }.Select(f => "\"" + f.Replace("\"", "\"\"") + "\"")) + "\r\n"));
    public string Export(CollectionIdentity[] cards) => CollectionCsv.Write(
        _counts.OrderBy(p => p.Key).Select(p => new[] { p.Key, cards.FirstOrDefault(c => c.Id == p.Key)?.VersionName ?? "", p.Value.ToString(CultureInfo.InvariantCulture) }));
}
public static class CollectionCsv
{
    public static string Write(IEnumerable<string[]> rows) => "card_id,card_name,owned_count\r\n" +
        string.Concat(rows.Select(row => string.Join(",", row.Select(s => "\"" + s.Replace("\"", "\"\"") + "\"")) + "\r\n"));
    public static List<string[]> Parse(string text)
    {
        if (text.Length > 1_000_000) throw new FormatException("CSV exceeds the 1 MB limit.");
        if (text.StartsWith("\uFEFF", StringComparison.Ordinal)) text = text[1..];
        var rows = new List<string[]>(); var fields = new List<string>(); var value = new StringBuilder();
        bool quoted = false, closed = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; } else { quoted = false; closed = true; } }
                else value.Append(c);
            }
            else if (c == '"' && value.Length == 0 && !closed) quoted = true;
            else if (c is ',' or '\r' or '\n')
            {
                fields.Add(value.ToString()); value.Clear(); closed = false;
                if (c != ',') { rows.Add(fields.ToArray()); fields.Clear(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
            }
            else if (closed || c == '"') throw new FormatException("Malformed CSV quoting.");
            else value.Append(c);
            if (rows.Count > 10000 || fields.Count > 32) throw new FormatException("CSV has too many rows or columns.");
        }
        if (quoted) throw new FormatException("CSV has an unclosed quoted field.");
        if (value.Length > 0 || fields.Count > 0 || closed) { fields.Add(value.ToString()); rows.Add(fields.ToArray()); }
        return rows;
    }
}

