using L5R.Client;
var dir = Path.Combine(Path.GetTempPath(), "l5r-collection-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir); int checks = 0;
void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
void Throws<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { Check(true, message); return; } throw new Exception(message); }
try
{
    var path = Path.Combine(dir, "collection.json"); var s = new MyCollectionStore(path);
    CollectionIdentity[] cards = [new("a", "Twin", "Twin"), new("b", "Twin", "Twin - Experienced"), new("c", "Comma, \"Quoted\"\nCard", "Comma, \"Quoted\"\nCard")];
    s.Set("a", 3); s.Set("b", 1); Check(new MyCollectionStore(path).Count("a") == 3, "ownership survives reopening");
    Check(s.Includes("a", OwnershipFilter.Owned) && s.Includes("c", OwnershipFilter.Unowned), "owned/unowned filters distinguish zero");
    s.Set("future-catalogue-id", 7); Check(s.Export(cards).Contains("future-catalogue-id"), "export preserves IDs absent from current catalogue"); s.Set("future-catalogue-id", 0);
    var export = s.Export(cards); var p = s.Preview(export, cards); Check(p.CanApply, "export reimports by stable IDs");
    var rev = s.Revision; s.Apply(p, CollectionImportMode.UpdateListed); Check(s.Count("a") == 3 && s.Revision == rev, "repeat import sets totals without adding");
    p = s.Preview("card_name,owned_count\nTwin,2\n", cards); Check(!p.CanApply && p.Rows[0].Candidates.Length == 2, "bare title never guesses an Experienced version");
    p.Rows[0].Id = "b"; s.Apply(p, CollectionImportMode.UpdateListed); Check(s.Count("b") == 2 && s.Count("a") == 3, "resolved version updates only listed cards");
    s.Undo(); Check(s.Count("b") == 1 && !s.CanUndo, "undo restores preceding full collection once");
    p = s.Preview("card_id,card_name,owned_count\na,Twin,0\n", cards); s.Apply(p, CollectionImportMode.UpdateListed); Check(s.Count("a") == 0 && s.Count("b") == 1, "explicit zero removes only supplied ownership");
    s.Undo(); p = s.Preview("card_id,owned_count\nb,4\n", cards); s.Apply(p, CollectionImportMode.Replace);
    Check(s.Count("a") == 0 && s.Count("b") == 4, "replace clears absent rows");
    new MyCollectionStore(path).Undo(); Check(new MyCollectionStore(path).Count("a") == 3, "persisted backup supports undo after restart");
    s = new MyCollectionStore(path);
    p = s.Preview("card_id,owned_count\na,1\na,2\n", cards); Check(!p.CanApply, "duplicate identity is not silently summed"); p.Rows[1].Excluded = true; Check(p.CanApply, "duplicate may be explicitly excluded");
    p = s.Preview("card_id,owned_count\nunknown,1\n", cards); Check(!p.CanApply, "unknown ID blocks import"); p.Rows[0].Excluded = true; Check(!p.CanApply, "all-excluded import cannot wipe the collection");
    Check(!s.Preview("card_id,card_name,owned_count\na,Wrong name,2\n", cards).CanApply, "conflicting ID and name require correction");
    foreach (var quantity in new[] { "-1", "2.5", "100000", "999999999999999999", "NaN", "+1" })
        Check(!s.Preview("card_id,owned_count\na," + quantity, cards).CanApply, "reject invalid quantity " + quantity);
    Check(!s.Preview("card_id,owned_count\na,1,extra", cards).CanApply, "column mismatch blocks apply");
    Throws<FormatException>(() => CollectionCsv.Parse("card_name,owned_count\n\"broken,3"), "unclosed CSV quote rejected");
    Throws<FormatException>(() => CollectionCsv.Parse("\"closed\"junk,1"), "trailing text after quote rejected");
    Throws<FormatException>(() => s.Preview("card_id,owned_count,owned_count\na,1,2", cards), "duplicate header rejected");
    Throws<FormatException>(() => s.Preview("card_name,quantity\na,1", cards), "nonstandard quantity header explained");
    s.Set("c", 8); p = s.Preview(s.Export(cards), cards); Check(p.CanApply && p.Rows.Any(r => r.Id == "c" && r.Count == 8), "quotes commas and newlines roundtrip");
    Check(s.Preview("\uFEFFcard_id,owned_count\r\na,2\r\n", cards).CanApply, "spreadsheet BOM and CRLF supported");
    Check(s.Preview("\uFEFF\"card_id\",\"owned_count\"\r\na,2\r\n", cards).CanApply, "BOM before quoted headers supported");
    p = s.Preview("card_id,owned_count\na,2\n", cards); s.Set("b", 3); Throws<InvalidOperationException>(() => s.Apply(p, CollectionImportMode.UpdateListed), "stale import preview cannot overwrite newer edits");
    var bad = Path.Combine(dir, "bad.json"); File.WriteAllText(bad, "{corrupt");
    var broken = new MyCollectionStore(bad); Check(broken.LoadError.Length > 0, "corrupt local file reported"); Throws<IOException>(() => broken.Set("a", 1), "corrupt local file cannot be silently replaced"); Check(File.ReadAllText(bad) == "{corrupt", "corrupt original remains intact");
    var unavailable = Path.Combine(dir, "blocked"); Directory.CreateDirectory(unavailable);
    var blocked = new MyCollectionStore(unavailable);
    // Moving a file onto a directory reports IOException on macOS and denied access on Windows.
    try { blocked.Set("a", 2); Check(false, "write failure reported"); }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Check(true, "write failure reported"); }
    Check(blocked.Count("a") == 0, "write failure leaves in-memory ownership unchanged");
    Throws<FormatException>(() => s.Set("a", -1), "manual negative count rejected");
    Throws<FormatException>(() => s.Preview(new string('x', 1_000_001), cards), "oversized import rejected");
    // ---- the collection file (issues #807, #1141): Set, Card Name, Card Type, Rarity, Clan, Limit, Count ----
    var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
    var printings = CollectionPrintings.Parse(CollectionPrintings.Decode(File.ReadAllBytes(Path.Combine(root!.FullName, "src/L5R.Client/Content/collection-printings.txt"))));
    Check(printings.Length == 2374 && printings.All(p => p.Set.Length > 0 && p.Rarity.Length > 0) && printings.Any(p => p.Rarity == "Promo") && !printings.Any(p => p.Set.Contains("â")), "the shipped list has every printing, each with a set and rarity, and clean set names");
    Check(printings.Count(p => p.Type == "Token") == 35 && printings.Where(p => p.Type == "Token").All(p => p.Rarity == "Token") && printings.All(p => p.Limit is "U" or "S" or "N" or "X" or ""), "Tokens are labelled Token in type and rarity, and every limit is U, S, N or X");
    Check(new[] { ("Itoku (Exp)", "itoku - exp"), ("Kaiu Esumi (Exp.)", "kaiu esumi - exp"), ("Shadow Dragon (Exp2)", "shadow dragon - exp2"), ("Bayushi Nitoshi (Inexp)", "bayushi nitoshi - inexperienced"), ("Daigotsu Hotako (Exp. 3)", "daigotsu hotako - exp3"),
        ("Daigotsu Takayasu (Exp) Personality", "daigotsu takayasu - exp"), ("Akodo Tsudoken Experienced 2", "akodo tsudoken - exp2"), ("Doji Hoturi Inexperienced", "doji hoturi - inexperienced"), ("Bayushi Kachiko Experienced CoM", "bayushi kachiko - exp"),
        ("A Forefather’s Vengeance", "a forefather's vengeance"), ("Plain Name", "plain name") }.All(t => CollectionPrintings.Key(t.Item1) == t.Item2), "the file's (Exp), (Exp.), (Exp2), (Inexp), Experienced N and curly-quote spellings map onto the catalogue's names");
    Check(CollectionPrintings.Decode(new byte[] { 0x41, 0x92, 0x73 }) == "A’s" && CollectionPrintings.Decode([0xEF, 0xBB, 0xBF, 0x41]).TrimStart('﻿') == "A", "a Windows-1252 file (curly apostrophes) and a UTF-8 file both decode");
    CollectionIdentity[] shelf = [new("t", "Itoku", "Itoku"), new("x", "Itoku", "Itoku - exp"), new("k", "Kyuden", "Kyuden"), new("g", "Bishamon's Tower", "Bishamon's Tower")];
    CollectionPrinting[] list = [new("Embers of War", "Itoku", "Personality", "Rare", "Crane", "U"), new("Embers of War", "Itoku (Exp)", "Personality", "Rare", "Crane", "U"), new("Before the Dawn", "Itoku", "Personality", "Common", "Crane", "U"),
        new("Embers of War", "Assassin", "Token", "Token"), new("The Shadow's Embrace", "Bishamon’s Tower", "Stronghold", "Decks", "", "N"), new("The Shadow's Embrace", "Bishamon’s Tower", "Stronghold", "Fixed", "", "N")];
    var ps = new MyCollectionStore(Path.Combine(dir, "printings.json"));
    var csv = "Set,Card Name,Card Type,Rarity,Clan,Limit,Count\r\nEmbers of War,Itoku,Personality,Rare,Crane,U,2\r\nEmbers of War,Itoku (Exp),Personality,Rare,Crane,U,1\r\nBefore the Dawn,Itoku,Personality,Common,Crane,U,3\r\n"
        + "Embers of War,Assassin,Token,Token,,,-\r\nThe Shadow's Embrace,Bishamon’s Tower,Stronghold,Decks,,N,1\r\nThe Shadow's Embrace,Bishamon’s Tower,Stronghold,Fixed,,N,2\r\n";
    var plan = ps.Preview(csv, shelf, list);
    Check(plan.CanApply && plan.Rows.Length == 5 && plan.Skipped == 1, "a per-printing file reads: the - row is skipped, curly apostrophes and (Exp) resolve, a card printed twice in one set at two rarities is two rows");
    Check(plan.Rows[0].Id == "t" && plan.Rows[1].Id == "x" && plan.Rows[0].Bucket == "t@Embers of War@Rare" && plan.Rows[2].Bucket == "t@Before the Dawn@Common", "each row names the exact version and printing bucket");
    ps.Apply(plan, CollectionImportMode.UpdateListed);
    Check(ps.Count("t") == 5 && ps.Count("x") == 1 && ps.Count("g") == 3 && ps.CountOf("g@The Shadow's Embrace@Fixed") == 2, "printings add up to the card's total, so ownership checks elsewhere see one figure");
    var exported = ps.Export(shelf, list);
    Check(exported.StartsWith("Set,Card Name,Card Type,Rarity,Clan,Limit,Count\r\n") && exported.Contains("\"Embers of War\",\"Assassin\",\"Token\",\"Token\",\"\",\"\",\"0\"") && exported.Contains("\"Itoku (Exp)\",\"Personality\",\"Rare\",\"Crane\",\"U\",\"1\""), "export writes the template's rows and names with the owned counts, and a card with none owned on a line with 0");
    var again = ps.Preview(exported, shelf, list); var before = ps.Buckets.OrderBy(p => p.Key).ToArray();
    ps.Apply(again, CollectionImportMode.Replace); Check(ps.Buckets.OrderBy(p => p.Key).SequenceEqual(before), "the exported file imports back to exactly the same counts, printing by printing");
    ps.Set("k", 4); Check(ps.Count("k") == 4 && ps.CountOf("k") == 4, "copies edited as a total are recorded against no printing");
    var withUnassigned = ps.Export(shelf, list); Check(withUnassigned.Contains("\"\",\"Kyuden\",\"\",\"\",\"\",\"\",\"4\""), "copies of no recorded printing are exported on a row of their own");
    var round = ps.Preview(withUnassigned, shelf, list); ps.Apply(round, CollectionImportMode.Replace); Check(ps.CountOf("k") == 4 && ps.Count("t") == 5, "and import back as the same unassigned copies");
    ps.Set("t", 2); Check(ps.Count("t") == 2 && ps.CountOf("t@Embers of War@Rare") == 0 && ps.CountOf("t@Before the Dawn@Common") == 2, "removing copies comes off unassigned copies first, then printings from the last bucket name backwards");
    var template = MyCollectionStore.Template(list); Check(template.Contains("\"Assassin\",\"Token\",\"Token\",\"\",\"\",\"0\"") && template.Contains("\"Itoku\",\"Personality\",\"Rare\",\"Crane\",\"U\",\"0\""), "the template lists every printing with 0 owned");
    var zero = MyCollectionStore.Template(printings).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
    Check(zero.Length == printings.Length && zero.All(line => line.EndsWith(",\"0\"")), "with nothing owned, every card is still reported, each on a line with 0 in the Count column");
    var legacyBucket = Path.Combine(dir, "legacy.json"); File.WriteAllText(legacyBucket, "{\"SchemaVersion\":2,\"Counts\":{\"t@EoW@Rare\":2,\"t@BtD\":1}}");
    var migrated = new MyCollectionStore(legacyBucket); Check(migrated.CountOf("t@Embers of War@Rare") == 2 && migrated.CountOf("t@Before the Dawn") == 1 && migrated.Count("t") == 3, "copies recorded against an edition code move to its set when the collection is read");
    var badRows = ps.Preview("Set,Card Name,Card Type,Rarity,Clan,Limit,Count\nEmbers of War,Nobody,Personality,Rare,,,1\nZZZ,Itoku,Personality,Rare,,,1\nEmbers of War,Itoku,Personality,Rare,,,many\n", shelf, list);
    Check(!badRows.CanApply && badRows.Rows[0].Error == "Unknown card name." && badRows.Rows[1].Error.Contains("ZZZ") && badRows.Rows[2].Error.Contains("Count"), "unknown names, editions and counts are reported row by row");
    var dup = ps.Preview("Set,Card Name,Card Type,Rarity,Clan,Limit,Count\nEmbers of War,Itoku,Personality,Rare,,,1\nEmbers of War,Itoku,Personality,Rare,,,2\n", shelf, list); Check(!dup.CanApply, "the same printing twice needs one row excluded");
    var legacy = ps.Preview("card_id,owned_count\nt,9\n", shelf); ps.Apply(legacy, CollectionImportMode.UpdateListed); Check(ps.Count("t") == 9 && ps.CountOf("t") == 9, "the older card_id file still sets a card's total, replacing its printings");
    // The real catalogue: how much of the template resolves without review.
    var library = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "src/L5R.Client/Content/library.json"))).RootElement.GetProperty("cards");
    var real = library.EnumerateArray().Select(c => { var name = c.GetProperty("name").GetString()!; var at = name.IndexOf(" - ", StringComparison.Ordinal); var baseTitle = at < 0 ? name : name[..at]; return new CollectionIdentity(c.GetProperty("id").GetString()!, baseTitle, name); }).ToArray();
    var resolved = new MyCollectionStore(Path.Combine(dir, "real.json")).Preview(MyCollectionStore.Template(printings).Replace("\"0\"", "\"1\""), real, printings);
    var unresolved = resolved.Rows.Count(r => r.Id is null);
    Console.WriteLine($"  template against the catalogue: {resolved.Rows.Length - unresolved} of {resolved.Rows.Length} rows resolve without review");
    Check(resolved.Rows.Length > 2000 && unresolved < 120, "all but a few of the template's rows resolve to a catalogue card without review");
    // ---- the closest-name guess (issue #807): the deck troubleshooter's scoring, for names the catalogue spells differently ----
    CollectionIdentity[] spelled = [new("w", "Advance Warning", "Advance Warning"), new("m", "Modifications", "Modifications"), new("i", "Itoku", "Itoku"), new("a", "Kaiu Esumi", "Kaiu Esumi"), new("b", "Kaiu Esumi", "Kaiu Esumi - exp")];
    var guessed = new MyCollectionStore(Path.Combine(dir, "guess.json")).Preview("Set,Card Name,Card Type,Rarity,Clan,Limit,Count\nBefore the Dawn,Advanced Warning,Strategy,Common,,N,2\nBefore the Dawn,Modifcations,Strategy,Common,,N,1\nEmbers of War,Itoku (Exp),Personality,Rare,,U,1\nEmbers of War,Kaiu Esumi (Exp.),Personality,Rare,,U,1\nBefore the Dawn,Zzzz Qqqq,Strategy,Common,,N,1\nBefore the Dawn,Zzzz Qqqq,Strategy,Common,,N,0\n", spelled, list);
    Check(guessed.Rows[0].Id == "w" && guessed.Rows[0].Guess.Contains("Advance Warning") && guessed.Rows[1].Id == "m", "a name one letter off the catalogue's is matched to it and shown for checking");
    Check(guessed.Rows[2].Id is null && guessed.Rows[2].Candidates.Any(c => c.Id == "i") && guessed.Rows[2].Error.Length == 0, "an Experienced row whose version is not in the catalogue is offered the base card, never silently assigned it");
    Check(guessed.Rows[3].Id is null && guessed.Rows[3].Candidates.Length >= 2 || guessed.Rows[3].Id == "b", "an (Exp.) row for a title with both versions never lands on the wrong one");
    Check(guessed.Rows[4].Error == "Unknown card name." && guessed.Rows.Length == 5, "a name nothing resembles is an error, and its zero-count twin is skipped");
    Check(!guessed.CanApply && guessed.HasUnresolved, "unresolved rows block apply until decided");
    guessed.ExcludeUnresolved(); Check(guessed.CanApply && !guessed.HasUnresolved && guessed.Rows.Count(r => !r.Excluded) >= 2, "Exclude unresolved leaves the matched rows ready to apply");
    var wholeFile = new MyCollectionStore(Path.Combine(dir, "whole.json")).Preview(MyCollectionStore.Template(printings).Replace("\"0\"", "\"1\""), real, printings);
    Console.WriteLine($"  template against the catalogue: {wholeFile.Rows.Count(r => r.Id is not null)} ready ({wholeFile.Rows.Count(r => r.Guess.Length > 0)} by closest name), {wholeFile.Rows.Count(r => r.Id is null)} to choose or exclude");
    foreach (var g in wholeFile.Rows.Where(r => r.Guess.Length > 0).GroupBy(r => r.Name)) Console.WriteLine("  guess: " + g.Key + " -> " + g.First().Guess);
    wholeFile.ExcludeUnresolved(); Check(wholeFile.CanApply, "the whole template can be applied once its unresolved rows are excluded: " + string.Join(" | ", wholeFile.Problems.Take(8)));
    string[] Printed(string text) => CatalogueActions.Printings(text);
    Check(Printed("Battle/Open: Target your bowed Personality: Straighten him.").SequenceEqual(["Battle/Open"]), "Battle/Open is its own printed action");
    Check(Printed("Battle/Open: Straighten.\nLimited, :g1:: Draw a card.").SequenceEqual(["Battle/Open", "Limited"]), "a card may print several actions");
    Check(Printed("Political Open: Copy.").SequenceEqual(["Open"]) && Printed("Iaijutsu Battle, :bow:: Duel.").SequenceEqual(["Battle"]), "trait words before the timing are ignored");
    Check(Printed("Reaction: When paying a Gold cost: Produce Gold.").SequenceEqual(["Reaction"]), "Reaction is detected");
    Check(Printed("").Length == 0 && Printed(":bow:: Produce 3 Gold.").Length == 0, "no printed action means None (Remote Village, empty text)");
    Check(Printed("(Limited, :g2:: Discard a Kharmic card.)").Length == 0 && Printed("Your Tacticians have the ability, \"Tactical Open: Give +1F.\"").Length == 0, "reminder text and granted abilities are not printed actions");
    Check(CatalogueActions.Matches(["Battle/Open"], "Battle") && CatalogueActions.Matches(["Battle/Open"], "Open") && CatalogueActions.Matches(["Battle/Open"], "Battle/Open"), "Battle and Open filters include Battle/Open cards");
    Check(!CatalogueActions.Matches(["Battle"], "Battle/Open") && !CatalogueActions.Matches(["Open"], "Battle/Open"), "Battle/Open filter excludes cards printing only Battle or Open");
    string[] both = ["Battle/Open", "Limited"];
    Check(new[] { "Battle", "Open", "Battle/Open", "Limited" }.All(choice => CatalogueActions.Matches(both, choice)) && !CatalogueActions.Matches(both, "Reaction") && !CatalogueActions.Matches(both, "None"), "Battle/Open + Limited appears in all four matching filters");
    Check(CatalogueActions.MatchesAny(both, ["Battle", "Open", "Battle/Open", "Limited"]) && CatalogueActions.MatchesAny(both, []), "any number of selected choices yields one match");
    Check(Printed("Battle/Limited: Look at the top card of your Fate deck.").SequenceEqual(["Battle/Limited"]) && Printed("Limited/Battle: Draw.").SequenceEqual(["Battle/Limited"]), "Battle/Limited is its own printed action");
    string[] bl = ["Battle/Limited"];
    Check(new[] { "Battle", "Limited", "Battle/Limited" }.All(choice => CatalogueActions.Matches(bl, choice)) && !new[] { "Open", "Battle/Open", "Reaction", "None" }.Any(choice => CatalogueActions.Matches(bl, choice)), "Battle/Limited appears in Battle, Limited and Battle/Limited only");
    Check(!CatalogueActions.Matches(["Battle"], "Battle/Limited") && !CatalogueActions.Matches(["Limited"], "Battle/Limited"), "Battle/Limited filter excludes cards printing only Battle or Limited");
    Check(CatalogueActions.MatchesAny(bl, ["Battle", "Limited", "Battle/Limited"]), "Battle/Limited with every matching choice selected yields one match");
    // Shadow Dragon prints Battle and Open as separate actions; Nitoshi (inexperienced) prints Battle/Limited.
    var dragon = Printed("After Shadow Dragon enters play: Lose 5 Honor.\nBattle: Target an enemy Personality: Take control of him.\nOpen: If Shadow Dragon is dead: Bring it into play.");
    Check(dragon.SequenceEqual(["Battle", "Open"]) && CatalogueActions.Matches(dragon, "Battle") && CatalogueActions.Matches(dragon, "Open") && !CatalogueActions.Matches(dragon, "Limited") && !CatalogueActions.Matches(dragon, "Battle/Open") && !CatalogueActions.Matches(dragon, "None"), "a card with several printed actions appears under each of them");
    Check(CatalogueActions.MatchesAny(dragon, ["Battle", "Open"]) && CatalogueActions.MatchesAny(dragon, ["Open"]) && CatalogueActions.MatchesAny(dragon, ["Battle"]) && !CatalogueActions.MatchesAny(dragon, ["Limited", "Reaction"]), "multi-action card matches when any selected choice fits");
    Check(CatalogueActions.Matches([], "None") && !CatalogueActions.Matches(["Limited"], "None"), "None matches only cards without printed actions");
    var tactical = BattleActionText.Present("Tactical Advantage: discard a card · performing Tactician +Force = its Focus Value · Akodo Toturi (card-0123)", "Akodo Toturi", "Tactical Advantage");
    Check(tactical == ("Akodo Toturi", "Tactical Advantage: discard a card · performing Tactician +Force = its Focus Value"), "a performed action leads with the Personality's title and keeps only the action text");
    var copied = BattleActionText.Present("Open: Straighten · Hida Kisada (card-0456) · copied from Kuni Yori", "Hida Kisada", null);
    Check(copied == ("Hida Kisada", "Open: Straighten · copied from Kuni Yori"), "provenance after the performer is kept, the id is not");
    Check(BattleActionText.Present("Reaction: Draw (card-0007)", null, "Kyuden Bayushi") == ("Kyuden Bayushi", "Reaction: Draw"), "with no performer the source card is the title and no id shows");
    Check(BattleActionText.WithoutIds("Bow Hida Kisada (card-0123): bow him (created-0004)") == "Bow Hida Kisada: bow him", "card and created-card ids never show");
    Check(BattleActionText.TitleFirst("Reaction: Redirect · Akodo Toturi (card-0123)", ["Akodo Toturi"]) == "Akodo Toturi · Reaction: Redirect", "the card concerned leads the placard, without its id");
    Check(BattleActionText.TitleFirst("Open: Bow · Not A Card", ["Akodo Toturi"]) == "Open: Bow · Not A Card", "a trailing name that is not one of the option's cards is left alone");
    Console.WriteLine($"COLLECTION DATA PASS: {checks} assertions");
}
finally { Directory.Delete(dir, true); }
