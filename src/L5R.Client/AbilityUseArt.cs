using System.Text.Json;
using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>
/// Issue #1309: marks used printed abilities on the card art itself. Nothing is drawn outside the card.
/// A spent ability gets a 60%-transparent grey box over its printed paragraph; a multi-use ability shows "1X", "2X"... in the clan colour;
/// a printed Tactician who has performed his Tactical action(s) this turn gets the box over the keyword.
/// The engine decides what is spent (CardView.AbilityUses / TacticalUses); this class only draws it.
/// Rectangles come from Content/ability-regions.json (artwork file -> pixel boxes in the scan), made by tools/content/ability_regions.
/// </summary>
public static class AbilityUseArt
{
    public const string RegionsPath = "res://Content/ability-regions.json";
    public static readonly Color Shade = new(.5f, .5f, .5f, .40f);
    public const string TacticianUsedText = "This personality has used tactical advantage this turn.";

    public sealed record Region(string Key, string[] Ids, Rect2 Box);
    public sealed record CardRegions(Vector2 Size, Region[] Abilities, Rect2? Tactician);

    private static Dictionary<string, CardRegions>? _regions;

    public static CardRegions? For(CardDefinition def)
    {
        if (def.ArtworkKey is not { } path) return null;
        _regions ??= Load();
        return _regions.TryGetValue(path[(path.LastIndexOf('/') + 1)..], out var r) ? r : null;
    }

    private static Dictionary<string, CardRegions> Load()
    {
        var result = new Dictionary<string, CardRegions>(StringComparer.Ordinal);
        if (!Godot.FileAccess.FileExists(RegionsPath)) return result;
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(RegionsPath));
        foreach (var card in doc.RootElement.GetProperty("cards").EnumerateObject())
        {
            var v = card.Value;
            static Rect2 Box(JsonElement b) => new(b[0].GetSingle(), b[1].GetSingle(), b[2].GetSingle() - b[0].GetSingle(), b[3].GetSingle() - b[1].GetSingle());
            var abilities = v.TryGetProperty("a", out var a) ? a.EnumerateArray().Select(r => new Region(r.GetProperty("k").GetString()!, r.GetProperty("ids").EnumerateArray().Select(i => i.GetString()!).ToArray(), Box(r.GetProperty("r")))).ToArray() : [];
            Rect2? tactician = v.TryGetProperty("t", out var t) ? Box(t) : null;
            result[card.Name] = new CardRegions(new Vector2(v.GetProperty("w").GetSingle(), v.GetProperty("h").GetSingle()), abilities, tactician);
        }
        return result;
    }

    /// <summary>The clan colour of the "NX" counter; unaligned cards use ink.</summary>
    public static Color ClanColor(string clan) => clan switch
    {
        "Crab" => new Color("3d6ea8"),
        "Crane" => new Color("2f8fbf"),
        "Dragon" => new Color("2e8a4a"),
        "Lion" => new Color("c08b12"),
        "Mantis" => new Color("5f9a1e"),
        "Phoenix" => new Color("d4621a"),
        "Scorpion" => new Color("b3202a"),
        "Unicorn" => new Color("7d3f9e"),
        "Spider" => new Color("4b4b4b"),
        _ => Palette.Ink,
    };

    public static bool TacticianSpent(CardView card) => card.TacticalUses is { } t && t.Used >= t.Cap;
    public static bool PrintedTactician(CardView card) => (card.Definition?.Keywords ?? []).Contains("Tactician");

    /// <summary>The marks to draw, in scan pixels: grey boxes and counters. Empty when nothing is used or no rectangles are known for this card.</summary>
    public static (Rect2[] Shaded, (Rect2 Box, string Text)[] Counters) Marks(CardView card)
    {
        if (card.Definition is not { } def || For(def) is not { } regions) return ([], []);
        var shaded = new List<Rect2>(); var counters = new List<(Rect2, string)>();
        foreach (var use in card.AbilityUses ?? [])
        {
            var region = regions.Abilities.FirstOrDefault(r => r.Key == use.Key || r.Ids.Intersect(use.AbilityIds).Any());
            if (region is null) continue;
            if (use.Spent) shaded.Add(region.Box);
            if (use.Max > 1 && use.Used > 0) counters.Add((region.Box, $"{use.Used}X"));
        }
        if (card.TacticalUses is { } tactical && regions.Tactician is { } keyword && PrintedTactician(card))
        {
            if (tactical.Used >= tactical.Cap) shaded.Add(keyword);
            if (tactical.Cap > 1 && tactical.Used > 0) counters.Add((keyword, $"{tactical.Used}X"));
        }
        return (shaded.ToArray(), counters.ToArray());
    }
}
