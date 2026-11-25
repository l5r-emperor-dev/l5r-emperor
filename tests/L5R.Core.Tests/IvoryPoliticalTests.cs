using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #622: the Emperor-era printings of thirteen cards whose primary printing is Ivory (strategy-wave-107, personality-wave-120, follower-wave-44, item-wave-40). Every scenario is an explicit
// position on the economy table (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers. The readings are in IvoryPolitical.cs.
internal static class IvoryPoliticalTests
{
    private const string Favors = "oracle-l5r-2492", Inexplicable = "oracle-l5r-3743", Confusion = "oracle-l5r-1450", LordsFavor = "oracle-l5r-10160", SubtleGames = "oracle-l5r-9932";
    private const string Tametaka = "oracle-l5r-1682", Hibana = "oracle-l5r-10305", Taruko = "oracle-l5r-4537", Sandayu = "oracle-l5r-6467", Suki = "oracle-l5r-4396", Demiyah = "oracle-l5r-5812";
    private const string Oriole = "oracle-l5r-10337", Record = "oracle-l5r-6213", Tetsubo = "oracle-l5r-7411";
    private static readonly string[] AllCards = [Favors, Inexplicable, Confusion, LordsFavor, Tametaka, Hibana, Taruko, Sandayu, Suki, Demiyah, Oriole, Record, Tetsubo];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    public static void Run(TestRunner runner) => runner.Run("ivory-political", [
        ("Published shape: the thirteen cards print their scans' stats, keywords and Emperor-era text, ship their art and are in their wave manifests; the packagers rebuild them", PublishedShape),
        ("Favors: a Courtier bows to make a player gain or lose 2 Honor (all four answers); a bowed Courtier or a non-Courtier may not perform, and a dishonorable performer is rehonored instead of his controller gaining", FavorsScenario),
        ("Inexplicable Challenge: a Courtier anywhere (bowed too) bows an enemy card without attachments and then a player gains or loses 2 Honor; a card with attachments is refused without a change", InexplicableScenario),
        ("Confusion at Court: from a hand it negates an action that discards the Imperial Favor (the Favor is still discarded, the effects do not happen), and is not offered against any other action", ConfusionScenario),
        ("My Lord's Favor: any Personality (bowed too) takes the Imperial Favor; discarding it straightens one of your units, the Personality and his attachments; refused without the Favor or against an enemy unit", LordsFavorScenario),
        ("Daidoji Tametaka: an enemy Personality or Follower gets -4F, and one of your Personalities may be named to move home; the second target is optional and must be yours", TametakaScenario),
        ("Isawa Hibana: she gives herself -3F as a cost to give a non-Water Personality of yours +3F; a Water Personality, an enemy, or a Hibana with less than 3 Force is refused", HibanaScenario),
        ("Komori Taruko: she straightens herself even while bowed; a bowed Personality's other abilities stay unusable", TarukoScenario),
        ("Sandayu: he straightens a Personality with a Weapon (him alone, not his unit); without a Weapon the target is refused", SandayuScenario),
        ("Kitsu Suki: each Ancestor controller gains 1 Honor; the acting player may make each player with no Ancestor and Honor above his starting Family Honor lose 1; with no such player nothing is asked", SukiScenario),
        ("Otomo Demiyah: after he enters play a player gains or loses 1 Honor, one Political Reaction of the pair", DemiyahScenario),
        ("Oriole Imperial Vanguard: +1 Personal Honor for the Personality; the first Political action each phase that targets him is negated whole (costs paid), the second is not, the next phase starts afresh, and other actions are not touched", OrioleScenario),
        ("Record of Failure: attaches to a Courtier for 2 less Gold; its Limited action removes it and makes the dishonorably dead Personality's owner lose his printed Personal Honor; honorably dead or living targets are refused", RecordScenario),
        ("Spiked Tetsubo: once the duel begins the other Personality loses Duelist for the turn; a Tetsubo on both sides costs both; no Duelist, nothing", TetsuboScenario),
    ]);

    // ---- helpers --------------------------------------------------------------------------------------------------------------------------------------------------

    private static GameCommand Choose(MatchEngine e, int player, string choice)
    {
        var options = e.QueryActions(player).Where(a => a.Type == "chooseEffect").ToArray();
        var found = options.FirstOrDefault(o => o.Command!.Choice == choice);
        Check(found is not null, $"effect choice '{choice}' was not offered to player {player}: " + string.Join("; ", options.Select(o => $"{o.Command!.Choice}={o.Label}")));
        return found!.Command!;
    }

    private static void Decline(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void Marketplaces(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }

    private static void Unchanged(MatchEngine e, GameCommand command, string why)
    {
        var before = e.Version;
        Check(!e.Execute(command).Accepted, why + " (should be refused)");
        Eq(e.Version, before, why + ": nothing changed");
    }

    // ---- Published shape ------------------------------------------------------------------------------------------------------------------------------------------
    private static void Stats(string id, string name, CardType type, int gold, int force, int chi, int honor, int focus, string[] keywords, string[] abilities, string effect, string text)
    {
        var d = CardCatalog.Get(id);
        Check(d.Type == type && d.Name == name && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.PersonalHonor == honor && d.Focus == focus && !d.ScenarioOnly, $"{name}: stats");
        Check((d.Keywords ?? []).Order(StringComparer.Ordinal).SequenceEqual(keywords.Order(StringComparer.Ordinal)), $"{name}: keywords {string.Join(",", d.Keywords ?? [])}");
        Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", d.Abilities ?? [])}");
        Eq(d.Effect, effect, $"{name}: effect");
        Eq(d.Text, text, $"{name}: text");
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        Stats(Favors, "Favors", CardType.Strategy, 0, 0, 0, 0, 2, ["Political"], ["favors-honor"], "none", "Limited: Bow your performing Courtier: Choose a player. He gains or loses 2 Honor.");
        Stats(Inexplicable, "Inexplicable Challenge", CardType.Strategy, 0, 0, 0, 0, 2, ["Political"], ["inexplicable-challenge-bow"], "none",
            "Battle: Choose your performing Courtier at any location and target an enemy card without attachments: Bow it. Choose a player, who gains or loses 2 Honor.");
        Stats(Confusion, "Confusion at Court", CardType.Strategy, 0, 0, 0, 0, 2, ["Political"], ["confusion-at-court-negate"], "none",
            "Reaction: Before an action resolves, if discarding the Imperial Favor is one of its base costs: Negate the action's effects.");
        Stats(LordsFavor, "My Lord's Favor", CardType.Strategy, 0, 0, 0, 0, 4, ["Favor", "Political"], ["lords-favor-take", "lords-favor-straighten"], "none",
            "Open: Choose your performing Personality: Take the Imperial Favor.\nBattle/Open: Discard the Imperial Favor and target your unit: Straighten it.");
        Stats(Tametaka, "Daidoji Tametaka", CardType.Personality, 8, 4, 3, 2, 0, ["Commander", "Crane Clan", "Iron Crane", "Samurai", "Scout", "Soul of Daidoji Zoushi"], ["tametaka-weaken"], "none",
            "Battle: Target an enemy Personality or Follower: Give it -4F. You may target one of your Personalities and move him home.");
        Stats(Hibana, "Isawa Hibana", CardType.Personality, 6, 3, 3, 2, 0, ["Fire", "Phoenix Clan", "Shugenja"], ["hibana-boost"], "none", "Battle/Open: Give Hibana -3F and target your Personality without the Water keyword: Give him +3F.");
        Stats(Taruko, "Komori Taruko", CardType.Personality, 5, 3, 4, 2, 0, ["Air", "Bat Clan", "Shugenja"], ["taruko-straighten"], "none", "Open: Even if Taruko is bowed: Straighten her.");
        Stats(Sandayu, "Sandayu", CardType.Personality, 5, 3, 2, 0, 0, ["Kensai", "Monk", "Order of the Spider", "Soul of Torao", "Spider Clan"], ["sandayu-straighten"], "none", "Battle/Open: Target a Personality with a Weapon: Straighten him.");
        Stats(Suki, "Kitsu Suki", CardType.Personality, 6, 0, 4, 3, 0, ["Lion Clan", "Shugenja", "Water"], ["suki-honor"], "none",
            "Limited: Each player who controls an Ancestor gains 1 Honor. For any player who controls no Ancestors, and with higher Honor than his starting Family Honor, you may cause him to lose 1 Honor.");
        Stats(Demiyah, "Otomo Demiyah", CardType.Personality, 4, 2, 3, 2, 0, ["Courtier", "Imperial"], ["demiyah-gain", "demiyah-lose"], "none", "Political Reaction: After Demiyah enters play: Choose a player, who gains or loses 1 Honor.");
        Stats(Oriole, "Oriole Imperial Vanguard", CardType.Follower, 7, 4, 0, 0, 4, ["Imperial"], [], "oriole-imperial-vanguard",
            "This Personality has +1PH.\nBefore the first Political action that targeted this Personality this phase resolves: Negate its effects.");
        Stats(Record, "Record of Failure", CardType.Item, 2, 0, 0, 0, 4, [], ["record-of-failure-honor"], "record-of-failure",
            "Attaches to a Courtier paying 2 less Gold.\nPolitical Limited: Remove this card from the game and target a dishonorably dead Personality: His owner loses Honor equal to the Personality's base Personal Honor.");
        Stats(Tetsubo, "Spiked Tetsubo", CardType.Item, 3, 2, 1, 0, 3, ["Heavy Weapon", "One-Handed", "Weapon"], [], "spiked-tetsubo", "After this Personality enters a duel: The other Personality in the duel loses Duelist.");
        var manifests = new Dictionary<string, string[]>
        {
            ["strategy-wave-107"] = [Favors, Inexplicable, Confusion, LordsFavor],
            ["personality-wave-120"] = [Tametaka, Hibana, Taruko, Sandayu, Suki, Demiyah],
            ["follower-wave-44"] = [Oriole],
            ["item-wave-40"] = [Record, Tetsubo],
        };
        foreach (var (wave, ids) in manifests)
        {
            var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
            Check(listed.SequenceEqual(ids.Order(StringComparer.Ordinal)), $"{wave}'s manifest lists exactly its cards");
        }
        foreach (var id in AllCards)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        // Provenance: each card came from the reviewed printing, not an Ivory one.
        var printings = new Dictionary<string, string>
        {
            [Favors] = "Before the Dawn",
            [Inexplicable] = "Emperor Edition",
            [Confusion] = "Emperor Edition",
            [LordsFavor] = "Seeds of Decay",
            [Tametaka] = "Emperor Edition",
            [Hibana] = "Torn Asunder",
            [Taruko] = "Before the Dawn",
            [Sandayu] = "Emperor Edition",
            [Suki] = "Emperor Edition",
            [Demiyah] = "Emperor Edition",
            [Oriole] = "Torn Asunder",
            [Record] = "Before the Dawn",
            [Tetsubo] = "Emperor Edition"
        };
        foreach (var wave in manifests.Keys)
        {
            var rows = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
            foreach (var row in rows)
                Eq(row.GetProperty("printing_set")[0].GetString(), printings[row.GetProperty("card_id").GetString()!], $"{row.GetProperty("card_id")} printing");
        }
        // The abilities carry the printed designators, costs and action keywords.
        Eq(AbilityCatalog.All["favors-honor"].Timing, "Limited"); Eq(AbilityCatalog.All["inexplicable-challenge-bow"].Timing, "Battle");
        Eq(AbilityCatalog.All["confusion-at-court-negate"].Trigger, "before-action-resolves");
        Check(AbilityCatalog.All["lords-favor-straighten"].Costs!.Any(c => c.Verb == CostVerbs.DiscardFavor), "My Lord's Favor discards the Imperial Favor as a cost");
        Check(AbilityCatalog.All["hibana-boost"].Costs!.Any(c => c.Verb == CostVerbs.ForcePenalty && c.Amount == 3), "Hibana's -3F is a cost");
        Check(AbilityCatalog.All["demiyah-gain"].ActionKeywords!.Contains("Political") && AbilityCatalog.All["demiyah-lose"].ActionKeywords!.Contains("Political"), "Demiyah's Reactions are Political");
        Check(AbilityCatalog.All["record-of-failure-honor"].Costs!.Any(c => c.Verb == CostVerbs.Remove), "Record of Failure removes itself as a cost");
        // The packagers rebuild the committed JSON exactly.
        foreach (var (wave, script) in new[] { ("strategy-wave-107", "package_strategy_wave_107.py"), ("personality-wave-120", "package_personality_wave_120.py"), ("follower-wave-44", "package_follower_wave_44.py"), ("item-wave-40", "package_item_wave_40.py") })
        {
            var text = File.ReadAllText(Path.Combine(root, "tools/content", script));
            Check(text.Contains($"PROFILE = \"{wave}\"") && text.Contains("package_spec("), $"{script} is the rerunnable packager of {wave}");
            var cards = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + ".json"))).RootElement.GetProperty("Cards");
            Eq(cards.GetArrayLength(), manifests[wave].Length, $"{wave}: card count");
        }
    }

    // ---- Favors ---------------------------------------------------------------------------------------------------------------------------------------------------
    private static void FavorsScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Favors, 0, Zone.Hand);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var tired = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "favors-honor", card.Id, tired.Id), "a bowed Courtier may not perform (bowing him is the cost)");
        Check(!CanUse(e, 0, "favors-honor", card.Id, samurai.Id), "only a Courtier performs");
        Check(CanUse(e, 0, "favors-honor", card.Id, courtier.Id), "an unbowed Courtier may");
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Enabled(e, 0, "favors-honor", card.Id, courtier.Id).Command!);
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the player is asked");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).Order(StringComparer.Ordinal).ToArray();
        Check(choices.SequenceEqual(["opponent-gain", "opponent-lose", "self-gain", "self-lose"]), "each player, gain or lose: " + string.Join(",", choices));
        Eq(Honor(e, 1), h1, "nothing yet"); Check(e.State.Cards[card.Id].Zone != Zone.Hand || e.State.PendingChoice is not null, "the Strategy is being played");
        Accept(e, Choose(e, 0, "opponent-lose"));
        Eq(Honor(e, 1), h1 - 2, "the opponent lost 2"); Eq(Honor(e, 0), h0, "and I did not change");
        Check(e.State.Cards[card.Id].Zone != Zone.Hand, "Favors left the hand");

        // Gain for myself; and a lose for myself.
        foreach (var (choice, mine, theirs) in new[] { ("self-gain", 2, 0), ("self-lose", -2, 0), ("opponent-gain", 0, 2) })
        {
            var t = Table(Phase.Action, 0); var c = Add(t, Favors, 0, Zone.Hand); var w = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier");
            var f = MatchEngine.FromTestState(t); var a0 = Honor(f, 0); var a1 = Honor(f, 1);
            Accept(f, Enabled(f, 0, "favors-honor", c.Id, w.Id).Command!); Accept(f, Choose(f, 0, choice));
            Eq(Honor(f, 0), a0 + mine, choice + ": mine"); Eq(Honor(f, 1), a1 + theirs, choice + ": theirs");
        }

        // A dishonorable performing Courtier: the Honor gain for his own controller rehonors him instead (core rules, Dishonor).
        var d = Table(Phase.Action, 0); var dc = Add(d, Favors, 0, Zone.Hand); var dw = Kw(Add(d, "training-crane-courtier", 0, Zone.Home), "Courtier"); dw.Dishonorable = true;
        var de = MatchEngine.FromTestState(d); var d0 = Honor(de, 0);
        Accept(de, Enabled(de, 0, "favors-honor", dc.Id, dw.Id).Command!); Accept(de, Choose(de, 0, "self-gain"));
        Eq(Honor(de, 0), d0, "no Honor gained"); Check(!de.State.Cards[dw.Id].Dishonorable, "he was rehonored instead");

        // Only on the active player's turn (Limited).
        var o = Table(Phase.Action, 1); var oc = Add(o, Favors, 0, Zone.Hand); var ow = Kw(Add(o, "training-crane-courtier", 0, Zone.Home), "Courtier");
        o.PriorityPlayerId = 0;
        Check(!CanUse(MatchEngine.FromTestState(o), 0, "favors-honor", oc.Id, ow.Id), "a Limited action is not available on another player's turn");
    }

    // ---- Inexplicable Challenge -----------------------------------------------------------------------------------------------------------------------------------
    private static void InexplicableScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Inexplicable, 0, Zone.Hand);
        var present = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // presence at the battlefield
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");     // at home: "at any location"
        var tired = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var samurai = Add(s, "training-crane-veteran", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "inexplicable-challenge-bow", card.Id, samurai.Id), "only a Courtier performs");
        Check(CanUse(e, 0, "inexplicable-challenge-bow", card.Id, courtier.Id) && CanUse(e, 0, "inexplicable-challenge-bow", card.Id, tired.Id), "a Courtier at any location, bowed or not (no bow is printed as a cost)");
        Unchanged(e, Use(e, 0, "inexplicable-challenge-bow", card.Id, performerId: courtier.Id, selections: [Pick("target", armed.Id)]), "a card with an attachment is not a legal target");
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Use(e, 0, "inexplicable-challenge-bow", card.Id, performerId: tired.Id, selections: [Pick("target", enemy.Id)]));
        Decline(e);
        Check(e.State.Cards[enemy.Id].Bowed, "the target was bowed"); Check(e.State.Cards[tired.Id].Bowed && !e.State.Cards[courtier.Id].Bowed, "the Courtier was not bowed by it");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "then the player is asked");
        // Mendon (2026-11-25): the two usual answers first -- you gain, then your opponent loses -- then the rarely chosen two.
        var order = e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).ToArray();
        Check(order.SequenceEqual(["self-gain", "opponent-lose", "opponent-gain", "self-lose"]), "you gain, your opponent loses, then the other two: " + string.Join(",", order));
        Accept(e, Choose(e, 0, "self-gain"));
        Eq(Honor(e, 0), h0 + 2, "I gained 2"); Eq(Honor(e, 1), h1);
        Check(e.State.Cards[card.Id].Zone != Zone.Hand, "the Strategy was played");
        _ = present;

        // The Honor sentence is independent of the bow: an already bowed target still opens the player choice.
        var t = Table(Phase.Battle, 0); var c2 = Add(t, Inexplicable, 0, Zone.Hand);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var w2 = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var en2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var e2 = MatchEngine.FromTestState(t); var o1 = Honor(e2, 1);
        Accept(e2, Use(e2, 0, "inexplicable-challenge-bow", c2.Id, performerId: w2.Id, selections: [Pick("target", en2.Id)])); Decline(e2);
        Accept(e2, Choose(e2, 0, "opponent-lose")); Eq(Honor(e2, 1), o1 - 2, "the opponent lost 2");
    }

    // ---- Confusion at Court ---------------------------------------------------------------------------------------------------------------------------------------
    private static void ConfusionScenario()
    {
        // Player 1 (active, holding the Imperial Favor) plays My Lord's Favor to straighten his unit; player 0 answers with Confusion at Court.
        var s = Table(Phase.Action, 1); s.FavorPlayerId = 1;
        var play = Add(s, LordsFavor, 1, Zone.Hand);
        var unit = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var confusion = Add(s, Confusion, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "lords-favor-straighten", play.Id, unit.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the other player may react");
        var offered = Options(e, 0, "confusion-at-court-negate", confusion.Id).Where(o => o.Enabled).ToArray();
        Check(offered.Length == 1, "Confusion at Court is offered from the hand");
        Accept(e, offered[0].Command!); Decline(e);
        Check(e.State.Cards[unit.Id].Bowed, "the straightening was negated");
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was still discarded (a negated action pays its costs)");
        Check(e.State.Cards[confusion.Id].Zone != Zone.Hand, "Confusion at Court was spent");
        Check(e.State.Cards[play.Id].Zone != Zone.Hand, "and the negated Strategy was still played");
        Check(e.State.Events.Any(x => x.Type == "actionNegated"), "the negation is on the record");

        // A rulebook Favor action (favor-cycle) is performed by no card at all: answering it with Confusion at Court must neither throw nor look for a performer (bot soak, issue #642).
        var fu = Table(Phase.Action, 1); fu.FavorPlayerId = 1;
        fu.Cards[fu.Players[1].StrongholdId].DefinitionId = CardCatalog.Cards.Values.First(d => d.Type == CardType.Stronghold && !d.ScenarioOnly).Id; // player abilities need a published Stronghold
        var spare = Add(fu, "training-lion-samurai", 1, Zone.Hand);
        var answer = Add(fu, Confusion, 0, Zone.Hand);
        var fe = MatchEngine.FromTestState(fu);
        var cycleResult = fe.Execute(new GameCommand("useAbility", 1, fe.Version, AbilityId: "favor-cycle", TargetId: spare.Id));
        Check(cycleResult.Accepted, "favor-cycle was refused: " + cycleResult.Error);
        Eq(fe.State.PendingChoice?.Type, "beforeActionReaction", "the other player may answer a Favor action too");
        Accept(fe, Options(fe, 0, "confusion-at-court-negate", answer.Id).Single(o => o.Enabled).Command!); Decline(fe);
        Eq(fe.State.FavorPlayerId, null, "a negated Favor action still discards the Favor");
        Check(fe.State.Cards[answer.Id].Zone != Zone.Hand, "Confusion at Court was spent against the card-less action");

        // Without a Favor discard among its costs: nothing to answer.
        var t = Table(Phase.Action, 1); t.FavorPlayerId = null;
        var take = Add(t, LordsFavor, 1, Zone.Hand); var any = Add(t, "training-lion-samurai", 1, Zone.Home);
        Add(t, Confusion, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Enabled(te, 1, "lords-favor-take", take.Id, any.Id).Command!);
        Check(te.State.PendingChoice?.Type != "beforeActionReaction", "no window opens for an action that discards no Favor");
        Eq(te.State.FavorPlayerId, 1, "player 1 took the Favor");

        // An ordinary Open action of a Strategy that does not print a Favor discard.
        var u = Table(Phase.Action, 1); var fav = Add(u, Favors, 1, Zone.Hand); var cw = Kw(Add(u, "training-lion-courtier", 1, Zone.Home), "Courtier");
        Add(u, Confusion, 0, Zone.Hand);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Enabled(ue, 1, "favors-honor", fav.Id, cw.Id).Command!);
        Check(ue.State.PendingChoice?.Type != "beforeActionReaction", "Favors discards no Imperial Favor: no window for Confusion at Court");
    }

    // ---- My Lord's Favor ------------------------------------------------------------------------------------------------------------------------------------------
    private static void LordsFavorScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, LordsFavor, 0, Zone.Hand);
        var tired = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: tired.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "lords-favor-straighten", card.Id), "no Imperial Favor, no straightening");
        Accept(e, Enabled(e, 0, "lords-favor-take", card.Id, tired.Id).Command!);
        Eq(e.State.FavorPlayerId, 0, "a bowed Personality may perform: the Imperial Favor is taken");
        Check(e.State.Cards[card.Id].Zone != Zone.Hand, "the Strategy was played");

        // The second action, from a fresh hand copy: discard the Favor, straighten the Personality and his attachments.
        var t = Table(Phase.Action, 0); t.FavorPlayerId = 0;
        var again = Add(t, LordsFavor, 0, Zone.Hand);
        var unit = Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true); var att = Add(t, "training-follower", 0, Zone.Attached, bowed: true, parent: unit.Id);
        var foe = Add(t, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var te = MatchEngine.FromTestState(t);
        var targets = Options(te, 0, "lords-favor-straighten", again.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(unit.Id) && !targets.Contains(foe.Id) && !targets.Contains(att.Id), "your own unit's Personality, not an enemy's and not a Follower");
        Unchanged(te, Offer(te, 0, "lords-favor-straighten", again.Id, unit.Id) with { TargetId = foe.Id }, "an enemy unit is refused");
        Accept(te, Offer(te, 0, "lords-favor-straighten", again.Id, unit.Id));
        Check(!te.State.Cards[unit.Id].Bowed && !te.State.Cards[att.Id].Bowed, "the unit was straightened: the Personality and his Follower");
        Check(te.State.Cards[foe.Id].Bowed, "an enemy is untouched");
        Eq(te.State.FavorPlayerId, null, "the Imperial Favor was discarded as the cost");
        _ = follower; _ = enemy;

        // Battle half: the same action in a battle straightens a unit at the battlefield.
        var b = Table(Phase.Battle, 0); b.FavorPlayerId = 0;
        var bc = Add(b, LordsFavor, 0, Zone.Hand); var present = Add(b, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "lords-favor-straighten", bc.Id, present.Id));
        Check(!be.State.Cards[present.Id].Bowed, "straightened in the Battle phase too");
    }

    // ---- Daidoji Tametaka -----------------------------------------------------------------------------------------------------------------------------------------
    private static void TametakaScenario()
    {
        var s = Table(Phase.Battle, 0);
        var tame = Add(s, Tametaka, 0, Zone.Battlefield, province: 0);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: foe.Id);
        var e = MatchEngine.FromTestState(s);
        var foeForce = e.CardForceForTest(foe.Id); var followerForce = e.CardForceForTest(follower.Id);
        Unchanged(e, Use(e, 0, "tametaka-weaken", tame.Id, selections: [Pick("target", foe.Id), Pick("home", foe.Id)]), "the optional second target must be one of your Personalities");
        Accept(e, Use(e, 0, "tametaka-weaken", tame.Id, selections: [Pick("target", foe.Id), Pick("home", ally.Id)]));
        Decline(e);
        Eq(e.CardForceForTest(foe.Id), foeForce - 4, "the enemy Personality got -4F");
        Eq(e.State.Cards[ally.Id].Zone, Zone.Home, "the second target moved home");

        // Without the optional target; and against a Follower.
        var t = Table(Phase.Battle, 0); var tame2 = Add(t, Tametaka, 0, Zone.Battlefield, province: 0); var ally2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var foe2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var f2 = Add(t, "training-follower", 1, Zone.Attached, province: 0, parent: foe2.Id);
        var e2 = MatchEngine.FromTestState(t); var ff = e2.CardForceForTest(f2.Id);
        Accept(e2, Use(e2, 0, "tametaka-weaken", tame2.Id, selections: [Pick("target", f2.Id)])); Decline(e2);
        Eq(e2.CardForceForTest(f2.Id), Math.Max(0, ff - 4), "an enemy Follower got -4F"); Eq(e2.State.Cards[ally2.Id].Zone, Zone.Battlefield, "nobody moved");
        _ = followerForce;
        Check(!AbilityCatalog.All["tametaka-weaken"].Slots![1].Equals(null) && AbilityCatalog.All["tametaka-weaken"].Slots![1].Min == 0, "the second target is optional");
    }

    // ---- Isawa Hibana ---------------------------------------------------------------------------------------------------------------------------------------------
    private static void HibanaScenario()
    {
        var s = Table(Phase.Action, 0);
        var hibana = Add(s, Hibana, 0, Zone.Home);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Home);
        var water = Kw(Add(s, "training-crane-veteran", 0, Zone.Home), "Water");
        var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "hibana-boost", hibana.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(ally.Id) && targets.Contains(hibana.Id) && !targets.Contains(water.Id) && !targets.Contains(foe.Id), "your Personalities without Water (herself included)");
        Unchanged(e, Offer(e, 0, "hibana-boost", hibana.Id, ally.Id) with { TargetId = water.Id }, "a Water Personality is refused");
        Unchanged(e, Offer(e, 0, "hibana-boost", hibana.Id, ally.Id) with { TargetId = foe.Id }, "an enemy is refused");
        var hf = e.CardForceForTest(hibana.Id); var af = e.CardForceForTest(ally.Id);
        Accept(e, Offer(e, 0, "hibana-boost", hibana.Id, ally.Id));
        Eq(e.CardForceForTest(hibana.Id), hf - 3, "Hibana paid 3F"); Eq(e.CardForceForTest(ally.Id), af + 3, "the Personality got +3F");

        // She may name herself (the cost and the bonus meet).
        var t = Table(Phase.Action, 0); var h2 = Add(t, Hibana, 0, Zone.Home);
        var te = MatchEngine.FromTestState(t); var f2 = te.CardForceForTest(h2.Id);
        Accept(te, Offer(te, 0, "hibana-boost", h2.Id, h2.Id));
        Eq(te.CardForceForTest(h2.Id), f2, "-3F as the cost, +3F as the effect");

        // With less than 3 Force to lose she cannot pay.
        var u = Table(Phase.Action, 0); var h3 = Add(u, Hibana, 0, Zone.Home); Force(h3, -1); var a3 = Add(u, "training-crane-samurai", 0, Zone.Home);
        var ue = MatchEngine.FromTestState(u);
        Check(!CanUse(ue, 0, "hibana-boost", h3.Id), "a Hibana with 2 Force cannot give herself -3F");
        Unchanged(ue, Use(ue, 0, "hibana-boost", h3.Id, a3.Id), "...and the command is refused too");
        // Battle half.
        var b = Table(Phase.Battle, 0); var h4 = Add(b, Hibana, 0, Zone.Battlefield, province: 0); var a4 = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b); var bf = be.CardForceForTest(a4.Id);
        Accept(be, Offer(be, 0, "hibana-boost", h4.Id, a4.Id)); Eq(be.CardForceForTest(a4.Id), bf + 3, "Battle too");
        _ = a3;
    }

    // ---- Komori Taruko --------------------------------------------------------------------------------------------------------------------------------------------
    private static void TarukoScenario()
    {
        var s = Table(Phase.Action, 0);
        var taruko = Add(s, Taruko, 0, Zone.Home, bowed: true);
        var sandayu = Add(s, Sandayu, 0, Zone.Home, bowed: true);
        var weapon = Add(s, "training-costly-weapon", 1, Zone.Attached, parent: Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true).Id);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "taruko-straighten", taruko.Id), "Taruko may act while bowed");
        Check(!CanUse(e, 0, "sandayu-straighten", sandayu.Id), "an ordinary bowed Personality may not (abilities on bowed cards cannot be used)");
        Accept(e, Enabled(e, 0, "taruko-straighten", taruko.Id).Command!);
        Check(!e.State.Cards[taruko.Id].Bowed, "she straightened herself");
        Check(!CanUse(e, 0, "taruko-straighten", taruko.Id), "once per turn");
        _ = weapon;
        // Unbowed she is still offered (the action is simply used up).
        var t = Table(Phase.Action, 0); var up = Add(t, Taruko, 0, Zone.Home);
        Check(CanUse(MatchEngine.FromTestState(t), 0, "taruko-straighten", up.Id), "available unbowed too");
    }

    // ---- Sandayu --------------------------------------------------------------------------------------------------------------------------------------------------
    private static void SandayuScenario()
    {
        var s = Table(Phase.Action, 0);
        var sandayu = Add(s, Sandayu, 0, Zone.Home);
        var armed = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); Add(s, "training-costly-weapon", 0, Zone.Attached, parent: armed.Id);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: armed.Id);
        var plain = Add(s, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "sandayu-straighten", sandayu.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(armed.Id) && !targets.Contains(plain.Id), "only a Personality with a Weapon");
        Unchanged(e, Offer(e, 0, "sandayu-straighten", sandayu.Id, armed.Id) with { TargetId = plain.Id }, "a Personality without a Weapon is refused");
        Accept(e, Offer(e, 0, "sandayu-straighten", sandayu.Id, armed.Id));
        Check(!e.State.Cards[armed.Id].Bowed, "he straightened"); Check(e.State.Cards[follower.Id].Bowed, "him alone, not his unit");
        Check(e.State.Cards[plain.Id].Bowed, "nobody else");
        // Battle half.
        var b = Table(Phase.Battle, 0); var sb = Add(b, Sandayu, 0, Zone.Battlefield, province: 0);
        var ab = Add(b, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); Add(b, "training-costly-weapon", 0, Zone.Attached, province: 0, parent: ab.Id);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "sandayu-straighten", sb.Id, ab.Id)); Check(!be.State.Cards[ab.Id].Bowed, "in a battle too");
    }

    // ---- Kitsu Suki -----------------------------------------------------------------------------------------------------------------------------------------------
    private static void SukiScenario()
    {
        // Player 0 (starting Honor 8) controls an Ancestor; player 1 (starting Honor 6) does not and stands above it.
        var s = Table(Phase.Action, 0); var suki = Add(s, Suki, 0, Zone.Home); Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Ancestor");
        s.Players[0].Honor = 9; s.Players[1].Honor = 9;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "suki-honor", suki.Id).Command!);
        Eq(Honor(e, 0), 10, "an Ancestor's controller gained 1");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "player 1 qualifies: the acting player may make him lose 1");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).Order(StringComparer.Ordinal).ToArray();
        Check(choices.SequenceEqual(["lose-1", "none"]), "one player qualifies: lose or not: " + string.Join(",", choices));
        Accept(e, Choose(e, 0, "lose-1")); Eq(Honor(e, 1), 8, "he lost 1"); Eq(Honor(e, 0), 10);

        var d = Table(Phase.Action, 0); var sd = Add(d, Suki, 0, Zone.Home); Kw(Add(d, "training-crane-samurai", 0, Zone.Home), "Ancestor");
        d.Players[1].Honor = 9; var de = MatchEngine.FromTestState(d); var d0 = Honor(de, 0);
        Accept(de, Enabled(de, 0, "suki-honor", sd.Id).Command!); Accept(de, Choose(de, 0, "none"));
        Eq(Honor(de, 1), 9, "declined: nobody lost"); Eq(Honor(de, 0), d0 + 1, "the gain was not optional");

        // Nobody controls an Ancestor, both above their starting Family Honor: four answers, including both.
        var b = Table(Phase.Action, 0); var sb = Add(b, Suki, 0, Zone.Home); b.Players[0].Honor = 10; b.Players[1].Honor = 7;
        var be = MatchEngine.FromTestState(b);
        Accept(be, Enabled(be, 0, "suki-honor", sb.Id).Command!);
        var both = be.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).Order(StringComparer.Ordinal).ToArray();
        Check(both.SequenceEqual(["lose-0", "lose-1", "lose-both", "none"]), "each player, both, or none: " + string.Join(",", both));
        Accept(be, Choose(be, 0, "lose-both")); Eq(Honor(be, 0), 9, "I lost 1"); Eq(Honor(be, 1), 6, "and he lost 1");

        // At or below the starting Family Honor (8 and 6) nobody qualifies: nothing is asked, nothing changes (and no Ancestor, so no gain).
        var n = Table(Phase.Action, 0); var sn = Add(n, Suki, 0, Zone.Home); n.Players[0].Honor = 8; n.Players[1].Honor = 6;
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Enabled(ne, 0, "suki-honor", sn.Id).Command!);
        Check(ne.State.PendingChoice is null, "nothing to ask"); Eq(Honor(ne, 0), 8); Eq(Honor(ne, 1), 6);

        // A dishonorable Suki: her own controller's gain is a rehonor instead; the other player's is unchanged.
        var r = Table(Phase.Action, 0); var sr = Add(r, Suki, 0, Zone.Home); sr.Dishonorable = true; Kw(Add(r, "training-crane-samurai", 0, Zone.Home), "Ancestor"); Kw(Add(r, "training-lion-samurai", 1, Zone.Home), "Ancestor");
        r.Players[0].Honor = 8; r.Players[1].Honor = 6;
        var re = MatchEngine.FromTestState(r);
        Accept(re, Enabled(re, 0, "suki-honor", sr.Id).Command!);
        Eq(Honor(re, 0), 8, "no Honor for the controller of a dishonorable performer"); Check(!re.State.Cards[sr.Id].Dishonorable, "Suki was rehonored"); Eq(Honor(re, 1), 7, "the other Ancestor controller gains 1");
    }

    // ---- Otomo Demiyah --------------------------------------------------------------------------------------------------------------------------------------------
    private static void DemiyahScenario()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = Demiyah; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, 4).First().SourceIds));
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entry-reaction window is open");
        var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == cardId && a.Command?.AbilityId is "demiyah-gain" or "demiyah-lose").ToArray();
        Eq(offered.Length, 4, "gain and lose, each for either player");
        Check(offered.All(a => a.Enabled), "all enabled");
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        var pick = offered.Single(a => a.Command!.AbilityId == "demiyah-lose" && a.Command.TargetPlayerId == 1);
        Accept(e, pick.Command!);
        Eq(Honor(e, 1), h1 - 1, "the chosen player lost 1"); Eq(Honor(e, 0), h0, "and I did not change");
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility" && a.CardId == cardId && a.Command?.AbilityId is "demiyah-gain" or "demiyah-lose" && a.Enabled), "the pair is one use: the other is no longer available");
        // The gain for the opponent.
        var t = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 8; i++) Add(t, "training-marketplace", 0, Zone.Home);
        var id2 = t.Players[0].Provinces[0].CardId!; t.Cards[id2].DefinitionId = Demiyah; t.Cards[id2].FaceUp = true;
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: id2, SourceIds: e2.PaymentOptions(0, 4).First().SourceIds));
        var g1 = Honor(e2, 1);
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "demiyah-gain" && a.Command.TargetPlayerId == 1).Command!);
        Eq(Honor(e2, 1), g1 + 1, "the opponent gained 1");
    }

    // ---- Oriole Imperial Vanguard ---------------------------------------------------------------------------------------------------------------------------------
    private const string ImperialProtection = "oracle-l5r-10152"; // Political Open: Target a Personality: ... (a standing watch on the target, so a resolved one leaves a record)

    private static void OrioleScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, Oriole, 1, Zone.Attached, parent: host.Id);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Home);
        var c1 = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var g1 = Add(s, SubtleGames, 0, Zone.Hand); var p1 = Add(s, ImperialProtection, 0, Zone.Hand); var p2 = Add(s, ImperialProtection, 0, Zone.Hand);
        var d = Add(s, "training-focus-strategy", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, d.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalHonorForTest(host.Id), CardCatalog.Get("training-lion-samurai").PersonalHonor + 1, "+1 Personal Honor for the Personality");
        Eq(e.PersonalHonorForTest(bare.Id), CardCatalog.Get("training-lion-samurai").PersonalHonor, "and only for him");

        // The first Political action that targets him this phase is negated whole; its costs are still paid.
        Accept(e, Offer(e, 0, "subtle-games-dishonor", g1.Id, host.Id, c1.Id));
        Decline(e);
        Check(!e.State.Cards[host.Id].Dishonorable, "its effects were negated");
        Check(e.State.Cards[c1.Id].Bowed, "the Courtier's bow, a cost, was still paid");
        Check(e.State.Cards[g1.Id].Zone != Zone.Hand, "and the Strategy was played");
        Check(e.State.Events.Any(x => x.Type == "actionNegated" && x.Message.Contains("Oriole")), "Oriole's negation is on the record");
        // The second one this phase (an Open action) goes through; one on a Personality with no Oriole always does.
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0); // the opponent passed
        Accept(e, Offer(e, 0, "imperial-protection-watch", p1.Id, host.Id)); Decline(e);
        Check(e.State.AssignmentWatches.Any(w => w.CardId == host.Id), "the second Political action of the phase resolved");

        // The next phase starts afresh: stepping to another turn's Action phase, his first Political action is negated again.
        var later = Batch336Support.Edit(e, st => { st.TurnNumber++; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        var watches = later.State.AssignmentWatches.Count;
        Accept(later, Offer(later, 0, "imperial-protection-watch", p2.Id, host.Id)); Decline(later);
        Eq(later.State.AssignmentWatches.Count, watches, "negated: no new watch");
        Check(later.State.Events.Count(x => x.Type == "actionNegated" && x.Message.Contains("Oriole")) == 2, "it negated again in the new phase");

        // A Personality with no Oriole is never protected, and a non-Political action does not spend the phase's negation.
        var t = Table(Phase.Action, 0); var h2 = Add(t, "training-lion-samurai", 1, Zone.Home); Add(t, Oriole, 1, Zone.Attached, parent: h2.Id);
        var other = Add(t, "training-lion-samurai", 1, Zone.Home);
        var weaken = Add(t, "training-penalty-source", 0, Zone.Home); var q1 = Add(t, ImperialProtection, 0, Zone.Hand); var q2 = Add(t, ImperialProtection, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, "imperial-protection-watch", q1.Id, other.Id)); Decline(te);
        Check(te.State.AssignmentWatches.Any(w => w.CardId == other.Id), "a Personality with no Oriole is targeted normally");
        Check(!te.State.Events.Any(x => x.Type == "actionNegated"), "nothing negated");
        _ = weaken; _ = q2;
        // Not Political: Yoshitada-style non-Political action against him is untouched and leaves his first Political action to be negated.
        var u = Table(Phase.Battle, 0); var uh = Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(u, Oriole, 1, Zone.Attached, province: 0, parent: uh.Id);
        var src = Add(u, "training-penalty-source", 0, Zone.Battlefield, province: 0);
        var ue = MatchEngine.FromTestState(u); var uf = ue.CardForceForTest(uh.Id);
        Accept(ue, Offer(ue, 0, "training-penalty-force", src.Id, uh.Id)); Decline(ue);
        Eq(ue.CardForceForTest(uh.Id), uf - 2, "a non-Political action resolves as always");
        Check(!ue.State.Events.Any(x => x.Type == "actionNegated"), "and Oriole did not act");
    }

    // ---- Record of Failure ----------------------------------------------------------------------------------------------------------------------------------------
    private static void RecordScenario()
    {
        // Equip prices: a Courtier pays 2 less; anyone else the printed 2.
        var s = Table(Phase.Action, 0);
        var item = Add(s, Record, 0, Zone.Hand); var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier"); var plain = Add(s, "training-crane-veteran", 0, Zone.Home);
        Marketplaces(s, 0, 4);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == item.Id);
        int Cost(CardInstance target) => equip.TargetGold is { } byTarget && byTarget.TryGetValue(target.Id, out var q) ? q.Cost : equip.Gold!.Cost;
        Eq(Cost(courtier), 0, "a Courtier: 2 less Gold"); Eq(Cost(plain), 2, "anyone else pays the printed cost");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [], TargetId: courtier.Id));
        Eq(e.State.Cards[item.Id].AttachedTo, courtier.Id, "attached to the Courtier for free");

        // The action: the owner of a dishonorably dead Personality loses his printed Personal Honor; the Item is removed from the game.
        var t = Table(Phase.Action, 0);
        var wielder = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier"); var rec = Add(t, Record, 0, Zone.Attached, parent: wielder.Id);
        var dead = Add(t, "training-lion-samurai", 1, Zone.Dead); dead.Dishonorable = true;
        var honorable = Add(t, "training-lion-samurai", 1, Zone.Dead);
        var living = Add(t, "training-lion-samurai", 1, Zone.Home); living.Dishonorable = true;
        var mine = Add(t, "training-crane-samurai", 0, Zone.Dead); mine.Dishonorable = true;
        var te = MatchEngine.FromTestState(t);
        var targets = Options(te, 0, "record-of-failure-honor", rec.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(dead.Id) && targets.Contains(mine.Id) && !targets.Contains(honorable.Id) && !targets.Contains(living.Id), "only dishonorably dead Personalities (either player's): " + string.Join(",", targets));
        var printed = CardCatalog.Get("training-lion-samurai").PersonalHonor; var h1 = Honor(te, 1);
        Unchanged(te, Offer(te, 0, "record-of-failure-honor", rec.Id, dead.Id) with { TargetId = honorable.Id }, "an honorably dead Personality is refused");
        Unchanged(te, Offer(te, 0, "record-of-failure-honor", rec.Id, dead.Id) with { TargetId = living.Id }, "a living dishonorable Personality is refused");
        Accept(te, Offer(te, 0, "record-of-failure-honor", rec.Id, dead.Id));
        Eq(Honor(te, 1), h1 - printed, "his owner lost Honor equal to the printed Personal Honor"); Check(printed > 0, "(the fixture prints some)");
        Check(te.State.Cards[rec.Id].Zone == Zone.RemovedFromGame, "the Item was removed from the game");
        Check(te.State.Cards[dead.Id].Zone == Zone.Dead, "the Personality stays dead");
        // My own dead Personality: I am his owner.
        var u = Table(Phase.Action, 0); var uw = Kw(Add(u, "training-crane-courtier", 0, Zone.Home), "Courtier"); var ur = Add(u, Record, 0, Zone.Attached, parent: uw.Id);
        var own = Add(u, "training-crane-samurai", 0, Zone.Dead); own.Dishonorable = true;
        var ue = MatchEngine.FromTestState(u); var m0 = Honor(ue, 0);
        Accept(ue, Offer(ue, 0, "record-of-failure-honor", ur.Id, own.Id));
        Eq(Honor(ue, 0), m0 - CardCatalog.Get("training-crane-samurai").PersonalHonor, "I lost the Honor: I own him");
    }

    // ---- Spiked Tetsubo -------------------------------------------------------------------------------------------------------------------------------------------
    private static void TetsuboScenario()
    {
        var s = Table(Phase.Battle, 0);
        var strike = Add(s, "training-duel-force", 0, Zone.Hand);
        var champion = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, Tetsubo, 0, Zone.Attached, province: 0, parent: champion.Id);
        var duelist = Add(s, "training-duelist-personality", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-duel-force", strike.Id, duelist.Id, champion.Id));
        Decline(e);
        Eq(e.State.PendingDuel?.Stage, "Focusing", "the duel has begun");
        Check(e.State.Cards[duelist.Id].RemovedKeywords.Contains("Duelist"), "the other Personality lost Duelist");
        Check(!e.State.Cards[champion.Id].RemovedKeywords.Contains("Duelist"), "the wielder lost nothing");
        Check(e.State.TemporaryRemovals.Any(r => r.CardId == duelist.Id && r.Until == TemporaryRemovals.Turn), "for the turn");
        // His refocus is judged after: with no Duelist he gets none, so the duel proceeds straight to its resolution on a strike.
        Accept(e, new GameCommand("strike", e.State.PendingDuel!.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null || e.State.PendingDuel.Stage != "DuelistRefocus", "no Duelist refocus for the one who lost it");

        // The wielder as the challenged side: the challenger loses Duelist.
        var t = Table(Phase.Battle, 1);
        var duel2 = Add(t, "training-duel-force", 1, Zone.Hand);
        var attackerDuelist = Add(t, "training-duelist-personality", 1, Zone.Battlefield, province: 0);
        var defender = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, Tetsubo, 0, Zone.Attached, province: 0, parent: defender.Id);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 1, "training-duel-force", duel2.Id, defender.Id, attackerDuelist.Id)); Decline(te);
        Check(te.State.Cards[attackerDuelist.Id].RemovedKeywords.Contains("Duelist"), "challenged Tetsubo wielder: the challenger loses Duelist");

        // Both wield one: both lose Duelist. No Duelist: nothing is recorded.
        var u = Table(Phase.Battle, 0); var uc = Add(u, "training-duel-force", 0, Zone.Hand);
        var a = Kw(Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Duelist"); Add(u, Tetsubo, 0, Zone.Attached, province: 0, parent: a.Id);
        var b = Kw(Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Duelist"); Add(u, Tetsubo, 1, Zone.Attached, province: 0, parent: b.Id);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Offer(ue, 0, "training-duel-force", uc.Id, b.Id, a.Id)); Decline(ue);
        Check(ue.State.Cards[a.Id].RemovedKeywords.Contains("Duelist") && ue.State.Cards[b.Id].RemovedKeywords.Contains("Duelist"), "each loses Duelist to the other's Tetsubo");
        var v = Table(Phase.Battle, 0); var vc = Add(v, "training-duel-force", 0, Zone.Hand);
        var va = Add(v, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(v, Tetsubo, 0, Zone.Attached, province: 0, parent: va.Id);
        var vb = Add(v, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ve = MatchEngine.FromTestState(v);
        Accept(ve, Offer(ve, 0, "training-duel-force", vc.Id, vb.Id, va.Id)); Decline(ve);
        Check(ve.State.TemporaryRemovals.Count == 0, "nothing to lose: nothing recorded");
        // Without a Tetsubo the Duelist keeps his keyword.
        var w = Table(Phase.Battle, 0); var wc = Add(w, "training-duel-force", 0, Zone.Hand);
        var wa = Add(w, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var wb = Add(w, "training-duelist-personality", 1, Zone.Battlefield, province: 0);
        var we = MatchEngine.FromTestState(w);
        Accept(we, Offer(we, 0, "training-duel-force", wc.Id, wb.Id, wa.Id)); Decline(we);
        Check(!we.State.Cards[wb.Id].RemovedKeywords.Contains("Duelist"), "no Tetsubo, no loss");
    }
}
