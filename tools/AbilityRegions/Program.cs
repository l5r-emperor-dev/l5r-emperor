using System.Text.Json;
using L5R.Core;

// Issue #1309: lists every playable in-play card whose printed abilities the client marks when used (or that prints Tactician),
// with its artwork file, printed text and the ability groups that share a limit. tools/content/ability_regions/build.py
// locates each group's paragraph on the scan from this list.
if (args.Length != 1) { Console.Error.WriteLine("usage: dotnet run --project tools/AbilityRegions -- <out.json>"); return 2; }
var cards = AbilityRegionCards.Rows().Select(c => new
{
    id = c.Card.Id,
    name = c.Card.Name,
    clan = c.Card.Clan,
    art = c.Card.ArtworkKey,
    text = c.Card.Text,
    tactician = c.PrintedTactician,
    groups = c.Groups.Select(g => new { key = g.Key, ids = g.Ids, labels = g.Ids.Select(id => AbilityCatalog.All[id].Label).ToArray() }).ToArray(),
}).ToArray();
File.WriteAllText(args[0], JsonSerializer.Serialize(cards));
Console.WriteLine($"{cards.Length} cards");
return 0;
