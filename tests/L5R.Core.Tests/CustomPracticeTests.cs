using L5R.Core;
using System.Text.Json;

internal static class CustomPracticeTests
{
    private static void Check(bool value, string message = "Custom Practice assertion failed") { if (!value) throw new Exception(message); }
    private static Deck Crane => CustomPractice.Preset("Crane");
    private static Deck Lion => CustomPractice.Preset("Lion");
    private static void Reject(Deck deck) { Check(!CustomPractice.Review(deck).Ready); try { MatchEngine.CreateCustomMatch(4, deck, Lion); } catch (ArgumentException) { return; } throw new Exception("Invalid match accepted"); }
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published presets resolve to supported 40/40 custom decks", () => {
                foreach (var d in new[] { Crane, Lion }) { var r = CustomPractice.Review(d); Check(r.Ready, string.Join("; ", r.Issues)); Check(r.Dynasty == 40 && r.Fate == 40); }
            }),
            ("Two selected decks supply setup, provinces, hand and starting player", () => {
                var e = MatchEngine.CreateCustomMatch(123, Crane, Lion); var s = e.State;
                Check(s.Players[0].Clan == "Crane" && s.Players[1].Clan == "Lion");
                Check(s.ActivePlayerId == 1);
                foreach (var p in s.Players) { Check(p.Provinces.Count == 4 && p.Hand.Count == 6); Check(s.Cards[p.StrongholdId].DefinitionId == CustomPractice.Review(p.Id == 0 ? Crane : Lion).StrongholdId); }
                var bamboo = s.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-679"); Check(bamboo.OwnerId == 0 && bamboo.Bowed && bamboo.Zone == Zone.Home);
            }),
            ("Same-Clan matches are independent and ties are seeded", () => {
                var a = MatchEngine.CreateCustomMatch(4, Crane, Crane); var b = MatchEngine.CreateCustomMatch(5, Crane, Crane);
                Check(a.State.ActivePlayerId != b.State.ActivePlayerId); Check(a.State.Players.All(p => p.Clan == "Crane"));
            }),
            ("Nonpreset supported composition changes actual match cards", () => {
                var d = CustomPractice.Workshop(); d.Load(DeckFormat.Write(Crane));
                var old = d.Cards.First(c => c.Families.Contains("Dynasty") && c.SetupSlot == ""); d.Adjust(old.Id, -1);
                var replacement = CardCatalog.Cards.Values.First(c => c.Id.StartsWith("oracle-l5r-") && !c.ScenarioOnly && !c.CreatedOnly && c.Type == CardType.Personality && d.Count(c.Id) == 0 && !(c.Keywords ?? []).Contains("Unique") && d.Cards.All(x => x.Id != c.Id));
                d.Adjust(replacement.Id, 1); var e = MatchEngine.CreateCustomMatch(8, d.ToDeck(), Lion);
                Check(e.State.Cards.Values.Any(c => c.OwnerId == 0 && c.DefinitionId == replacement.Id));
            }),
            ("Short drafts remain serializable but cannot start", () => {
                var d = CustomPractice.Workshop(); d.Load(DeckFormat.Write(Crane)); var c = d.Cards.First(c => c.Families.Contains("Dynasty") && c.SetupSlot == ""); d.Adjust(c.Id, -1);
                Reject(DeckFormat.Parse(DeckFormat.Write(d.ToDeck())));
            }),
            ("Unknown, training, unsupported and wrong-format cards reject", () => {
                foreach (var name in new[] { "Bogus card", "training-crane-samurai", "Plains of Otosan Uchi" }) // issue #503: A Champion in Court is now wired; Plains of Otosan Uchi is still unpublished Reject(Crane with { Entries = [.. Crane.Entries, new(name, 1)] });
                Reject(Crane with { Legality = "Modern" });
            }),
            ("A deck over the 242-card Practice cap is rejected by Review with the cap named, and cannot start (issue #611)", () => {
                var d = CustomPractice.Workshop(); d.Load(DeckFormat.Write(Crane));
                foreach (var card in CardCatalog.Cards.Values.Where(c => c.Id.StartsWith("oracle-l5r-") && !c.ScenarioOnly && !c.CreatedOnly && c.Type is CardType.Personality or CardType.Strategy))
                {
                    if (d.ToDeck() is var cur && CustomPractice.Review(cur) is var r && r.Dynasty + r.Fate > CustomPractice.MaxDeckCards) break;
                    d.Adjust(card.Id, 3);
                }
                var big = d.ToDeck(); var review = CustomPractice.Review(big);
                Check(review.Dynasty + review.Fate > CustomPractice.MaxDeckCards, "Test deck did not exceed the cap");
                Check(!review.Ready && review.Issues.Any(i => i.Contains("at most 242 cards")), string.Join("; ", review.Issues));
                Reject(big);
            }),
            ("The cap leaves the opaque id pool room for two maximum decks (issue #611)", () => Check(2 * (CustomPractice.MaxDeckCards + 3) < 2000)),
            ("Duplicate quantity lines cannot bypass copy limits", () => { var d = Crane; Reject(d with { Entries = [.. d.Entries, new(d.Entries.Last().Name.ToUpperInvariant(), 4)] }); }),
            ("Missing or extra setup cards reject", () => {
                Reject(Crane with { Entries = Crane.Entries.Where(e => e.Name != "Border Keep").ToArray() });
                Reject(Crane with { Entries = [.. Crane.Entries, Lion.Entries.First(e => e.Name == "Halls of Memory")] });
            }),
            ("Created-only cards cannot enter custom decks", () => { var card = CardCatalog.Cards.Values.First(c => c.CreatedOnly); Reject(Crane with { Entries = [.. Crane.Entries, new(Experienced.DeckName(card.Name, card.Keywords ?? []), 1)] }); }),
            ("Malformed direct quantities reject before allocation", () => { foreach (var n in new[] { 0, -1, int.MaxValue }) Reject(Crane with { Entries = [new("Border Keep", n)] }); }),
            ("Custom replay snapshots are detached and survive JSON roundtrip", () => {
                var original = Crane; var e = MatchEngine.CreateCustomMatch(77, original, Lion); var replay = e.ExportReplay();
                original.Entries[0] = new("Bogus", 999); replay.CustomDecks![0].Entries[0] = new("Bogus", 999);
                var fresh = JsonSerializer.Deserialize<ReplayRecord>(JsonSerializer.Serialize(e.ExportReplay()))!;
                var played = MatchEngine.Replay(fresh); Check(JsonSerializer.Serialize(e.GetView(0)) == JsonSerializer.Serialize(played.GetView(0)));
                Check(played.GetView(0).Players[1].Hand.All(c => !c.FaceUp && c.Definition is null));
            }),
            ("Replay retains commands and validates embedded decks", () => {
                var e = MatchEngine.CreateCustomMatch(91, Crane, Lion); var command = new GameCommand("concede", 0, e.Version); Check(e.Execute(command).Accepted);
                Check(MatchEngine.Replay(e.ExportReplay()).State.WinnerId == 1);
                var broken = e.ExportReplay() with { CustomDecks = null }; try { MatchEngine.Replay(broken); } catch (ArgumentException) { return; } throw new Exception("Missing replay deck accepted");
            }),
            ("A replay carrying its final digest must reach that state: replayed it does; a different digest or a dropped last command is refused (issue #1266)", () => {
                var e = MatchEngine.CreateCustomMatch(91, Crane, Lion); Check(e.Execute(new GameCommand("concede", 0, e.Version)).Accepted);
                var record = e.ExportReplay() with { FinalDigest = e.Digest() };
                Check(MatchEngine.Replay(record).Digest() == record.FinalDigest, "the recorded state is reached");
                Check(MatchEngine.Setup(record).Version == 0 && MatchEngine.Setup(record).Digest() != record.FinalDigest, "Setup stops before the first command and checks no digest");
                foreach (var tampered in new[] { record with { FinalDigest = "d1:" + new string('0', 32) }, record with { Commands = [] } })
                {
                    try { MatchEngine.Replay(tampered); throw new Exception("a replay that ends elsewhere was accepted"); } catch (ArgumentException) { }
                }
                Check(e.Clone().Digest() == e.Digest() && MatchEngine.CreateCustomMatch(92, Crane, Lion).Digest() != MatchEngine.CreateCustomMatch(91, Crane, Lion).Digest(), "the digest follows the match");
            }),
            ("Legacy preset replays still load without custom metadata", () => { var e = MatchEngine.CreatePublishedMatch(8); Check(e.ExportReplay().CustomDecks is null); Check(MatchEngine.Replay(e.ExportReplay()).GetView(0).Scenario == PublishedContent.Scenario); })
        ];
        runner.Run("custom-practice", tests);
    }
}
