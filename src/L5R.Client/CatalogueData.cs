using Godot;
using L5R.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace L5R.Client;

/// <summary>Read-only catalogue adapter shared by browsing and future deck selection.</summary>
public sealed class CatalogueData
{
    public sealed class Card
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string[] Types { get; set; } = [];
        public string[] DeckTypes { get; set; } = [];
        public string[] Clans { get; set; } = [];
        public string[] Keywords { get; set; } = [];
        public Dictionary<string, string[]> Stats { get; set; } = [];
        public string CurrentOracleText { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public Printing[] Printings { get; set; } = [];
        public CardDefinition? Definition => CardCatalog.Cards.GetValueOrDefault(Id);
        public bool Playable => Definition is { ScenarioOnly: false } && Id.StartsWith("oracle-l5r-", StringComparison.Ordinal);
        public string SearchText => string.Join(" ", new[] { Name, CurrentOracleText }.Concat(Clans).Concat(Keywords).Concat(Types));
        private string[]? _actions;
        /// <summary>Printed action labels (Limited, Open, Battle, Battle/Open, Reaction); empty when none are printed.</summary>
        public string[] Actions => _actions ??= CatalogueActions.Printings(CurrentOracleText);
        /// <summary>Printed Focus Value, or null when the card has none.</summary>
        public int? Focus => Stats.TryGetValue("focus", out var values) && int.TryParse(values.FirstOrDefault(), out var focus) ? focus : null;
        public double Numeric(string stat) => Stats.TryGetValue(stat, out var values) && double.TryParse(values.FirstOrDefault(), out var value) ? value : double.MaxValue;
    }
    public sealed class Printing
    {
        public string Id { get; set; } = "";
        public string Set { get; set; } = "";
        public string PrintingId { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string[] Numbers { get; set; } = [];
        public string ResourcePath { get; set; } = "";
        public string Label => Set + (Numbers.Length > 0 ? " · " + string.Join(" / ", Numbers) : "");
    }
    private sealed class Bundle { public int SchemaVersion { get; set; } public Card[] Cards { get; set; } = []; }
    private sealed class PrintingBundle { public int SchemaVersion { get; set; } public Dictionary<string, Printing[]> Cards { get; set; } = []; }
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public Card[] Cards { get; }
    public CatalogueData()
    {
        var bundle = Read<Bundle>("res://Content/library.json");
        var printings = Read<PrintingBundle>("res://Content/catalogue-printings.json");
        if (bundle.SchemaVersion != 1 || printings.SchemaVersion != 1) throw new InvalidOperationException("Unsupported catalogue format.");
        Cards = bundle.Cards;
        foreach (var card in Cards)
        {
            var variants = printings.Cards.GetValueOrDefault(card.Id, []);
            // The packaged supported face is a separately pinned printing, never silently
            // relabelled as a different Oracle variant or used to grant support to that variant.
            if (card.Definition is { } def && !string.IsNullOrEmpty(def.ArtworkKey) && (CardFaces.Has(def.ArtworkKey) || ResourceLoader.Exists(def.ArtworkKey)))
                variants = new[] { new Printing { Id = card.Id + "-pinned", Set = "Game printing", ResourcePath = def.ArtworkKey } }.Concat(variants).ToArray();
            card.Printings = variants;
        }
    }
    private static T Read<T>(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read) ?? throw new InvalidOperationException("Missing catalogue resource: " + path);
        return JsonSerializer.Deserialize<T>(file.GetAsText(), Json) ?? throw new InvalidOperationException("Invalid catalogue data.");
    }
    /// <summary>Printing sets in Mendon's approved presentation order; anything else (including the
    /// mojibake-encoded Promotional set labels currently in the catalogue) sorts into "Other".</summary>
    public static readonly string[] SetOrder =
    [
        "Before the Dawn", "War of Honor", "Forgotten Legacy", "Second City", "Promotional–Celestial",
        "Emperor Edition", "Promotional–Emperor", "Embers of War", "The Shadow's Embrace", "Seeds of Decay",
        "Honor and Treachery", "Emperor Edition Gempukku", "Torn Asunder", "Coils of Madness", "Gates of Chaos",
        "Aftermath", "Promotional Ivory",
    ];
    /// <summary>A card reprinted across sets sorts by the earliest set it appeared in.</summary>
    public static int SetRank(Card card) => card.Printings.Where(p => p.Set != "Game printing")
        .Select(p => { var i = Array.IndexOf(SetOrder, p.Set); return i < 0 ? SetOrder.Length : i; })
        .DefaultIfEmpty(SetOrder.Length).Min();
    public Card[] Filter(string search = "", IEnumerable<string>? clans = null, string family = "All", IEnumerable<string>? types = null, IEnumerable<string>? keywords = null, bool playable = false, string sort = "Name A–Z", string set = "")
    {
        var clanSet = clans?.Where(c => c != "All").ToArray() ?? [];
        var typeSet = types?.ToArray() ?? [];
        var keywordSet = keywords?.ToArray() ?? [];
        var query = Search(Cards, search).Where(c => (!playable || c.Playable)
            && (clanSet.Length == 0 || clanSet.Any(clan => clan == "Unaligned" ? c.Clans.Length == 0 || c.Clans.Contains(clan) : c.Clans.Contains(clan)))
            && (family == "All" || (family == "Strongholds" ? c.Types.Contains("Stronghold") : c.DeckTypes.Contains(family)))
            && (typeSet.Length == 0 || typeSet.Any(c.Types.Contains)) && (keywordSet.Length == 0 || keywordSet.Any(c.Keywords.Contains))
            && (set == "" || c.Printings.Any(p => p.Set == set)));
        return (sort switch
        {
            "Name Z–A" => query.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "Gold cost" => query.OrderBy(c => c.Numeric("cost")).ThenBy(c => c.Name),
            "Force" => query.OrderBy(c => c.Numeric("force")).ThenBy(c => c.Name),
            "Chi" => query.OrderBy(c => c.Numeric("chi")).ThenBy(c => c.Name),
            "Set" => query.OrderBy(SetRank).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        }).ToArray();
    }
    public static IEnumerable<Card> Search(IEnumerable<Card> cards, string search)
    {
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return cards.Where(c => terms.All(term => c.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
    public static string PlainText(string text) => Regex.Replace(System.Net.WebUtility.HtmlDecode(text).Replace(":bow:", "[Bow]"), @"<[^>]+>|:g(\d+|star):", m => m.Groups[1].Success ? "[" + m.Groups[1].Value + " Gold]" : "");
}
