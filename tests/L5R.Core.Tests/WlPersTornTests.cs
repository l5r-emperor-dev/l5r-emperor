using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #674, cluster "pers-torn": twenty-one Torn Asunder Personalities (personality-wave-131), each wired from its own scan with existing mechanisms (src/L5R.Core/WlPersTorn.cs).
// Driven only through QueryActions and the commands the engine offers, on explicit positions (Batch336Support.Table: player 0 the Crane, player 1 the Lion, province 0 the current
// battlefield, the active player the Attacker). Every card is checked for its printed behaviour and for the cases that must be refused or must not apply, a rejection leaving the state alone.
internal static class WlPersTornTests
{
    private const string Akahiko = "oracle-l5r-10278", Nanae = "oracle-l5r-10279", Izuna = "oracle-l5r-10302", Kyuudo = "oracle-l5r-10303", Suzukaze = "oracle-l5r-10304", Wakui = "oracle-l5r-10310",
        Ibara = "oracle-l5r-10280", Iza = "oracle-l5r-10281", Fubuko = "oracle-l5r-10318", Shaocheng = "oracle-l5r-10325", Nagai = "oracle-l5r-10274", Kyoshiro = "oracle-l5r-10285",
        Ira = "oracle-l5r-10297", Riku = "oracle-l5r-10323", Tetsaka = "oracle-l5r-10307", Sihung = "oracle-l5r-10327", Makiko = "oracle-l5r-10311", Shinoko = "oracle-l5r-10313",
        Suying = "oracle-l5r-10330", Daito = "oracle-l5r-10277", Tansen = "oracle-l5r-10301",
        Tesskss = "oracle-l5r-9959", Gakku = "oracle-l5r-2765", Tamahime = "oracle-l5r-10194", TamahimeExp = "oracle-l5r-10193";
    /// <summary>The printed Naval carriers of this wave (BattleTimingTests pins the others) and its one move-to-battle ability (BattleAbsentMovesTests pins the others).</summary>
    internal static readonly string[] NavalCarriers = [Ira, Tansen];
    internal static readonly string[] MovesToBattleIds = ["wlpt-ibara-oppose"];
    private static readonly string[] CardIds = [Akahiko, Nanae, Izuna, Kyuudo, Suzukaze, Wakui, Ibara, Iza, Fubuko, Shaocheng, Nagai, Kyoshiro, Ira, Riku, Tetsaka, Sihung, Makiko, Shinoko, Suying, Daito, Tansen];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);

    private static void Rejected(MatchEngine e, GameCommand command, string? contains = null)
    {
        var before = Json(e);
        var error = Batch336Support.Rejection(e, command);
        if (contains is not null) Check(error.Contains(contains, StringComparison.OrdinalIgnoreCase), $"rejection says '{contains}': {error}");
        Eq(Json(e), before, "a rejected action changes nothing");
    }

    // Declines every open window and passes until nothing is pending (an action's before-action window, an after-action window).
    private static void Settle(MatchEngine e, int limit = 14)
    {
        for (var guard = 0; guard < limit && e.State.PendingChoice is not null; guard++)
        {
            var actor = e.State.PriorityPlayerId;
            var option = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass");
            Check(option is not null, $"nothing to decline for player {actor} in {e.State.PendingChoice!.Type}: " + string.Join("; ", e.QueryActions(actor).Select(x => $"{x.Type}:{x.Label} [{x.Enabled}] {x.Reason}")));
            Accept(e, option!.Command!);
        }
    }

    public static void Run(TestRunner runner) => runner.Run("wl-pers-torn", [
        ("Published as printed: the manifest, art, Torn Asunder provenance, stats, keywords, text, abilities and effects of all twenty-one cards, read off their scans", PublishedShape),
        ("The packager is rerunnable and asserts its count, unique ids and the requested printing", PackagerContract),
        ("Asahina Akahiko: Melee and Ranged Attacks targeting him have -4 strength, and only him", AkahikoStatic),
        ("Asahina Akahiko: Battle: an enemy Personality and Akahiko go home, and the Honor is gained; an own Personality is no target and the action needs him at the battle", AkahikoHome),
        ("Asahina Nanae - exp: the four choices apply in the printed order (straighten, -4F, bow a Shadowlands target); she acts bowed, as Battle and as Open", NanaeChoices),
        ("Asahina Nanae - exp: refused for a target out of the battle in Battle and for a non-Personality; each rejection changes nothing", NanaeIllegal),
        ("Asako Izuna - exp: the Political Limited takes the Imperial Favor; the bow reaches only another player's card in a unit and the Honor choice follows Kolat, Ninja or Shadowlands", IznuaBow),
        ("Asako Kyuudo and Kaiu Nagai: Elite while defending (a bowed defender still contributes), never attacking; Kyuudo's +3F likewise", EliteWhileDefending),
        ("Asako Suzukaze: a Holding that produces gets 1 more Gold this turn; a Holding with no production gets none; it ends with the turn", SuzukazeGold),
        ("Bayushi Wakui: another player's action that would target a Magistrate at his battlefield targets him instead, if legal", WakuiRedirect),
        ("Daidoji Ibara: moves to oppose the target and gives it -3F, bowing it if Mantis Clan; no -3F when she does not end opposing it", IbaraOpposes),
        ("Doji Iza and Mirumoto Kyoshiro: each duels only another player's named Personality and destroys the duel's loser", NamedDuels),
        ("Fubuko: pays 1 less as a Dragon Clan player; the grant plays a Kiho Strategy from the discard pile, performed by Fubuko, which is then removed from the game", FubukoKiho),
        ("Iuchi Shaocheng: on entering, a card in a province is turned face up; a non-Unique Personality is discarded and becomes honorably dead; a Unique one, or any other card, stays", ShaochengTurn),
        ("Moshi Ira: after an action targets a card in his unit, its performer has -3F once the action has resolved; not before, not without a performer, not for a card outside his unit", IraWeakens),
        ("Riku: another player's action that would target Tamahime targets Riku instead, if legal; the Iaijutsu duel destroys its loser and targets only enemy Personalities", RikuRedirectAndDuel),
        ("Shiba Tetsaka: bowing a performing Shugenja gives him Cavalry or Naval for the turn, one use for both", TetsakaKeywords),
        ("Shinjo Sihung: after your Tactical action resolves, the Personality who performed it gets +2F; not for another player's or a non-Tactical action", SihungBoost),
        ("Shosuro Makiko - exp: takes the Imperial Favor (bowing); her Reaction, before an Honor gain from an action, reduces every gain from it by 2 to at most 0, losses untouched", MakikoReduces),
        ("Soshi Shinoko: gives any Personality Brash for the turn, and the Defender may draw when it is assigned to attack", ShinokoBrash),
        ("Utaku Suying: gains 2 Honor after a battle resolution in which her army destroyed a defending army or province; not as a defender, not when nothing was destroyed", SuyingHonor),
        ("Yasuki Daito: bowing him, or paying 4 Gold, negates the target's straightening until the next Straighten Phase ends; one use for both ways", DaitoBribes),
        ("Yoritomo Tansen: a Ranged 3 Attack, or the current battlefield's province gets -3 strength; one use for both", TansenChoices),
        ("Every enabled command of the new actions validates on a fresh engine", OfferedCommandsValidate),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", "personality-wave-131.txt")).Where(l => l.Length > 0).ToArray();
        Check(new HashSet<string>(listed).SetEquals(CardIds) && listed.Length == CardIds.Length, "personality-wave-131's manifest is exactly its cards");
        foreach (var id in CardIds) Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/personality-wave-131-sources.json"))).RootElement;
        Eq(sources.GetArrayLength(), CardIds.Length, "one provenance row per card");
        foreach (var row in sources.EnumerateArray())
        {
            Check(row.GetProperty("image_path").GetString()!.Contains("/oracle/torn-asunder/"), $"{row.GetProperty("card_id")} comes from the Torn Asunder scan");
            Check(row.GetProperty("printing_set").EnumerateArray().Select(x => x.GetString()).SequenceEqual(["Torn Asunder"]), $"{row.GetProperty("card_id")} is the Torn Asunder printing");
        }

        // (Force, Chi, Personal Honor, Gold, Honor Requirement, keywords, abilities, effect) read off each scan.
        void Is(string id, string name, string clan, int force, int chi, int ph, int gold, int? hr, string[] keywords, string[] abilities, string effect)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Type}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr && c.Focus == 0, $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, effect, $"{name}: effect");
            Check(AbilityCatalog.All.Keys.Intersect(abilities).Count() == abilities.Length, $"{name}: every ability id exists");
            Check(c.Text.Length > 0 && !c.Text.Contains('–'), $"{name}: printed text, ASCII hyphens");
        }
        Is(Akahiko, "Asahina Akahiko", "Crane", 2, 4, 3, 7, 2, ["Cavalry", "Air", "Crane Clan", "Shugenja"], ["wlpt-akahiko-home"], "none");
        Is(Nanae, "Asahina Nanae", "Crane", 2, 4, 4, 8, 9, ["Unique", "Air", "Crane Clan", "Experienced", "Jade", "Jade Champion", "Shugenja"], ["wlpt-nanae-jade"], "none");
        Is(Izuna, "Asako Izuna", "Phoenix", 3, 4, 3, 9, 6, ["Unique", "Air", "Amethyst Champion", "Courtier", "Experienced", "Inquisitor", "Magistrate", "Phoenix Clan", "Shugenja", "Villain"],
            ["noboru-exp2-favor", "wlpt-izuna-bow-battle", "wlpt-izuna-bow-limited"], "none");
        Is(Kyuudo, "Asako Kyuudo", "Phoenix", 4, 3, 3, 8, 4, ["Earth", "Inquisitor", "Magistrate", "Phoenix Clan", "Shugenja"], [], "wlpt-kyuudo");
        Is(Suzukaze, "Asako Suzukaze", "Phoenix", 4, 3, 2, 7, 0, ["Air", "Henshin", "Monk", "Phoenix Clan"], ["wlpt-suzukaze-gold"], "none");
        Is(Wakui, "Bayushi Wakui", "Scorpion", 1, 3, 1, 4, 0, ["Magistrate", "Samurai", "Scorpion Clan", "Yojimbo"], ["wlpt-wakui-redirect"], "none");
        Is(Ibara, "Daidoji Ibara", "Crane", 4, 3, 2, 8, 5, ["Crane Clan", "Iron Crane", "Samurai", "Scout"], ["wlpt-ibara-oppose"], "none");
        Is(Iza, "Doji Iza", "Crane", 2, 3, 4, 6, 6, ["Duelist", "Crane Clan", "Magistrate", "Samurai"], ["wlpt-iza-duel"], "none");
        Is(Fubuko, "Fubuko", "Spider", 4, 4, 1, 8, 1, ["Kensai", "Monk", "Order of the Spider", "Spider Clan"], ["wlpt-fubuko-kiho"], "dragon-clan-discount");
        Is(Shaocheng, "Iuchi Shaocheng", "Unicorn", 2, 2, 1, 5, null, ["Cavalry", "Death Priest", "Shugenja", "Unicorn Clan", "Water"], ["wlpt-shaocheng-turn"], "none");
        Is(Nagai, "Kaiu Nagai", "Crab", 4, 4, 3, 8, 3, ["Tactician", "Crab Clan", "Samurai", "Siege"], [], "wlpt-nagai");
        Is(Kyoshiro, "Mirumoto Kyoshiro", "Dragon", 3, 3, 3, 6, 5, ["Duelist", "Courtier", "Dragon Clan", "Magistrate"], ["wlpt-kyoshiro-duel"], "none");
        Is(Ira, "Moshi Ira", "Mantis", 4, 4, 2, 8, null, ["Naval", "Mantis Clan", "Shugenja", "Thunder"], ["wlpt-ira-weaken"], "none");
        Is(Riku, "Riku", "Unaligned", 3, 5, 3, 7, 4, ["Duelist", "Unique", "Commander", "Ronin", "Samurai", "Yojimbo"], ["wlpt-riku-redirect", "wlpt-riku-duel"], "none");
        Is(Tetsaka, "Shiba Tetsaka", "Phoenix", 4, 4, 1, 8, null, ["Duelist", "Phoenix Clan", "Samurai", "Yojimbo"], ["wlpt-tetsaka-cavalry", "wlpt-tetsaka-naval"], "none");
        Is(Sihung, "Shinjo Sihung", "Unicorn", 4, 3, 2, 8, 4, ["Tactician", "Samurai", "Unicorn Clan"], ["wlpt-sihung-boost"], "none");
        Is(Makiko, "Shosuro Makiko", "Scorpion", 2, 4, 1, 9, null, ["Loyal", "Unique", "Courtier", "Experienced", "Scorpion Clan", "Seductress"], ["makiko-favor", "wlpt-makiko-reduce"], "none");
        Is(Shinoko, "Soshi Shinoko", "Scorpion", 2, 4, 2, 7, 0, ["Cavalry", "Courtier", "Scorpion Clan"], ["wlpt-shinoko-brash"], "none");
        Is(Suying, "Utaku Suying", "Unicorn", 4, 3, 3, 8, 4, ["Cavalry", "Battle Maiden", "Paragon", "Samurai", "Unicorn Clan"], [], "wlpt-suying");
        Is(Daito, "Yasuki Daito", "Crab", 2, 4, 0, 8, null, ["Courtier", "Crab Clan", "Merchant"], ["wlpt-daito-bow", "wlpt-daito-gold"], "none");
        Is(Tansen, "Yoritomo Tansen", "Mantis", 2, 3, 1, 6, null, ["Naval", "Mantis Clan", "Samurai", "Scout"], ["wlpt-tansen-ranged", "wlpt-tansen-province"], "none");

        var akahiko = CardCatalog.Get(Akahiko);
        Check(akahiko.AttackDefenseKind == "Both" && akahiko.AttackDefenseAmount == -4 && akahiko.AttackDefenseScope == "Self", "Akahiko's passive defense is data: Melee and Ranged, -4, himself only");
        Check(CardCatalog.Get(Riku).Text.Contains("Iaijutsu Battle:") && AbilityCatalog.All["wlpt-riku-duel"].ActionKeywords!.Contains("Iaijutsu") && AbilityCatalog.All["wlpt-riku-duel"].IssuesDuel, "the scan's Iaijutsu Battle, not the catalogue's plain Battle");
        Check(AbilityCatalog.All["wlpt-iza-duel"] is { IssuesDuel: true, DuelRefusable: false, Timing: "Open" } && AbilityCatalog.All["wlpt-kyoshiro-duel"] is { IssuesDuel: true, DuelRefusable: false, Timing: "Open" }, "Iza and Kyoshiro duel (no refusal) in an Open action");
        Check(AbilityCatalog.All["wlpt-tetsaka-cavalry"].UsageKey == AbilityCatalog.All["wlpt-tetsaka-naval"].UsageKey && AbilityCatalog.All["wlpt-daito-bow"].UsageKey == AbilityCatalog.All["wlpt-daito-gold"].UsageKey
            && AbilityCatalog.All["wlpt-tansen-ranged"].UsageKey == AbilityCatalog.All["wlpt-tansen-province"].UsageKey && AbilityCatalog.All["wlpt-izuna-bow-battle"].UsageKey == AbilityCatalog.All["wlpt-izuna-bow-limited"].UsageKey,
            "each pair of ways to take one printed action shares a use");
        Check(AbilityCatalog.All["wlpt-daito-gold"].Cost is { } cost && cost.Amount == 4, "Daito's alternative is exactly 4 Gold");
        Check(NavalCarriers.All(id => CardCatalog.Get(id).Keywords!.Contains("Naval")), "Moshi Ira and Yoritomo Tansen print Naval (the pinned inventory of BattleTimingTests leaves them to this suite)");
        Check(AbilityCatalog.All.Where(p => p.Value.MovesToBattle && p.Key.StartsWith("wlpt-", StringComparison.Ordinal)).Select(p => p.Key).SequenceEqual(MovesToBattleIds), "Ibara's is this wave's one move-to-battle ability");
        Check(TraitReactions.Is("wlpt-shaocheng-turn"), "Shaocheng's entry trait is a trait, not a Reaction a Reaction ban stops");
        // Existing printings are untouched: the pinned ids still resolve.
        Check(CardCatalog.Get("oracle-l5r-7126").Name == "Shosuro Makiko", "the inexperienced Makiko is still her own card");
    }

    private static void PackagerContract()
    {
        var root = Batch336Support.RepoRoot();
        var script = File.ReadAllText(Path.Combine(root, "tools/content/package_personality_wave_131.py"));
        Check(script.Contains("assert len(cards) == len(CARDS) == 21 == len({card[\"Id\"] for card in cards})") && script.Contains("Torn Asunder") && script.Contains("printing_set"), "the packager asserts count, unique ids and the printing");
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/personality-wave-131.json"))).RootElement;
        Eq(json.GetProperty("Profile").GetString(), "personality-wave-131");
        Eq(json.GetProperty("Cards").GetArrayLength(), 21);
        Check(json.GetProperty("Cards").EnumerateArray().Select(c => c.GetProperty("Id").GetString()).Distinct().Count() == 21, "unique ids");
    }

    // ---- Akahiko -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AkahikoStatic()
    {
        // Melee 7 (Gakku) destroys a Force 5 card, but not a Force 5 Akahiko (7 - 4 = 3); the same Force, a bare Crane samurai, is destroyed.
        var s = Table(Phase.Battle, 0);
        var gakku = Add(s, Gakku, 0, Zone.Battlefield, province: 0);
        var akahiko = Add(s, Akahiko, 1, Zone.Battlefield, province: 0); Force(akahiko, 3);
        var plain = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Force(plain, 2);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(akahiko.Id), 5); Eq(e.CardForceForTest(plain.Id), 5);
        Accept(e, Use(e, 0, "promo-gakku-melee", gakku.Id, akahiko.Id));
        Settle(e);
        Eq(e.State.Cards[akahiko.Id].Zone, Zone.Battlefield, "Melee 7 -4 = 3 does not reach his Force 5");
        var control = MatchEngine.FromTestState(s);
        Accept(control, Use(control, 0, "promo-gakku-melee", gakku.Id, plain.Id));
        Settle(control);
        Eq(control.State.Cards[plain.Id].Zone, Zone.Dead, "the same attack destroys a Force 5 card with no such trait");

        // Ranged: Tansen's Ranged 3 (-4) cannot destroy even a Force 2 Akahiko; it destroys a Force 3 Crane samurai.
        var r = Table(Phase.Battle, 0);
        var tansen = Add(r, Tansen, 0, Zone.Battlefield, province: 0);
        var a2 = Add(r, Akahiko, 1, Zone.Battlefield, province: 0);
        var c3 = Add(r, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var re = MatchEngine.FromTestState(r);
        Accept(re, Use(re, 0, "wlpt-tansen-ranged", tansen.Id, a2.Id));
        Settle(re);
        Eq(re.State.Cards[a2.Id].Zone, Zone.Battlefield, "Ranged 3 -4 destroys nothing, not even Force 2");
        var re2 = MatchEngine.FromTestState(r);
        Accept(re2, Use(re2, 0, "wlpt-tansen-ranged", tansen.Id, c3.Id));
        Settle(re2);
        Eq(re2.State.Cards[c3.Id].Zone, Zone.Dead, "Ranged 3 destroys Force 3");

        // "Targeting Akahiko" only: his friend at the battlefield takes the full strength.
        var f = Table(Phase.Battle, 0);
        var g = Add(f, Gakku, 0, Zone.Battlefield, province: 0);
        Add(f, Akahiko, 1, Zone.Battlefield, province: 0);
        var friend = Add(f, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Force(friend, 4);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Use(fe, 0, "promo-gakku-melee", g.Id, friend.Id));
        Settle(fe);
        Eq(fe.State.Cards[friend.Id].Zone, Zone.Dead, "a friend of his is not protected (Melee 7 against Force 7)");
    }

    private static void AkahikoHome()
    {
        var s = Table(Phase.Battle, 0);
        var akahiko = Add(s, Akahiko, 0, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-akahiko-home", akahiko.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(mine.Id) && !option.TargetIds.Contains(akahiko.Id), "enemy Personalities only");
        Rejected(e, Use(e, 0, "wlpt-akahiko-home", akahiko.Id, mine.Id));
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "wlpt-akahiko-home", akahiko.Id, enemy.Id));
        Settle(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality went home");
        Eq(e.State.Cards[akahiko.Id].Zone, Zone.Home, "and so did Akahiko");
        Eq(Honor(e, 0), honor + 1, "Gain 1 Honor");
        Check(e.State.Cards[mine.Id].Zone == Zone.Battlefield, "nobody else moves");
        var away = Table(Phase.Battle, 0);
        var home = Add(away, Akahiko, 0, Zone.Home);
        Add(away, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(away, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, "wlpt-akahiko-home", home.Id), "a Battle action needs him at the battlefield");
        Check(!CanUse(MatchEngine.FromTestState(Table(Phase.Action, 0)), 0, "wlpt-akahiko-home", akahiko.Id), "Battle only");
    }

    // ---- Nanae ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Nanae, CardInstance Target) NanaeTable(Phase phase, bool nanaeBowed, bool targetBowed, bool shadowlands, int nanaeActor = 0)
    {
        var s = Table(phase, nanaeActor);
        var nanae = Add(s, Nanae, nanaeActor, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, nanaeBowed, phase == Phase.Battle ? 0 : null);
        var target = Add(s, "training-lion-samurai", 1 - nanaeActor, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, targetBowed, phase == Phase.Battle ? 0 : null);
        if (shadowlands) Kw(target, "Shadowlands");
        return (MatchEngine.FromTestState(s), nanae, target);
    }

    private static void ChooseNanae(MatchEngine e, string option)
    {
        Eq(e.State.PendingChoice?.Type, "effectChoice", "Nanae's choice is open");
        var offered = e.QueryActions(0).Where(a => a.Type == "chooseEffect").ToArray();
        Check(offered.Select(a => a.Command!.Choice).OrderBy(x => x).SequenceEqual(["both", "neither", "straighten", "weaken"]), "all four options are listed: " + string.Join(",", offered.Select(a => a.Command!.Choice)));
        Accept(e, offered.Single(a => a.Command!.Choice == option).Command!);
    }

    private static void NanaeChoices()
    {
        // Battle, Nanae bowed, a bowed Shadowlands target: straighten and -4F, and then it is bowed again (printed order).
        var (e, nanae, target) = NanaeTable(Phase.Battle, nanaeBowed: true, targetBowed: true, shadowlands: true);
        var force = e.CardForceForTest(target.Id);
        Accept(e, Offer(e, 0, "wlpt-nanae-jade", nanae.Id, target.Id));
        Settle(e, 0);
        ChooseNanae(e, "both");
        Settle(e);
        Eq(e.CardForceForTest(target.Id), force - 4, "-4F");
        Check(e.State.Cards[target.Id].Bowed, "he was straightened, then bowed because he is Shadowlands");
        Check(e.State.Cards[nanae.Id].Bowed, "she acted while bowed and costs nothing");

        // Not Shadowlands: straighten only -- he ends straightened; no -4F.
        var (e2, n2, t2) = NanaeTable(Phase.Battle, false, targetBowed: true, shadowlands: false);
        var f2 = e2.CardForceForTest(t2.Id);
        Accept(e2, Offer(e2, 0, "wlpt-nanae-jade", n2.Id, t2.Id));
        ChooseNanae(e2, "straighten");
        Settle(e2);
        Check(!e2.State.Cards[t2.Id].Bowed, "straightened, and not bowed again (not Shadowlands)");
        Eq(e2.CardForceForTest(t2.Id), f2, "no penalty chosen");

        // -4F only, on a straightened Shadowlands target: it is bowed by the third sentence.
        var (e3, n3, t3) = NanaeTable(Phase.Battle, false, targetBowed: false, shadowlands: true);
        var f3 = e3.CardForceForTest(t3.Id);
        Accept(e3, Offer(e3, 0, "wlpt-nanae-jade", n3.Id, t3.Id));
        ChooseNanae(e3, "weaken");
        Eq(e3.CardForceForTest(t3.Id), f3 - 4, "-4F");
        Check(e3.State.Cards[t3.Id].Bowed, "bowed as a Shadowlands Personality");

        // Neither: a Shadowlands target is bowed all the same; a plain one is untouched.
        var (e4, n4, t4) = NanaeTable(Phase.Battle, false, targetBowed: false, shadowlands: true);
        Accept(e4, Offer(e4, 0, "wlpt-nanae-jade", n4.Id, t4.Id));
        ChooseNanae(e4, "neither");
        Check(e4.State.Cards[t4.Id].Bowed, "Bow him if he is Shadowlands, whatever was chosen");
        var (e5, n5, t5) = NanaeTable(Phase.Battle, false, targetBowed: false, shadowlands: false);
        var before5 = e5.CardForceForTest(t5.Id);
        Accept(e5, Offer(e5, 0, "wlpt-nanae-jade", n5.Id, t5.Id));
        ChooseNanae(e5, "neither");
        Check(!e5.State.Cards[t5.Id].Bowed && e5.CardForceForTest(t5.Id) == before5, "nothing happens to a plain target");

        // The Open half, bowed and at home, any Personality in play, her own included.
        var o = Table(Phase.Action, 0);
        var nanaeHome = Add(o, Nanae, 0, Zone.Home, bowed: true);
        var ally = Add(o, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var oe = MatchEngine.FromTestState(o);
        var open = Enabled(oe, 0, "wlpt-nanae-jade", nanaeHome.Id);
        Check(open.TargetIds!.Contains(ally.Id) && open.TargetIds.Contains(nanaeHome.Id), "any Personality, hers included");
        Accept(oe, Offer(oe, 0, "wlpt-nanae-jade", nanaeHome.Id, ally.Id));
        ChooseNanae(oe, "straighten");
        Check(!oe.State.Cards[ally.Id].Bowed, "straightened in the Action Phase");
    }

    private static void NanaeIllegal()
    {
        // Battle: a target at home is out of the battle.
        var s = Table(Phase.Battle, 0);
        var nanae = Add(s, Nanae, 0, Zone.Battlefield, province: 0);
        var away = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Rejected(e, Use(e, 0, "wlpt-nanae-jade", nanae.Id, away.Id));
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: s.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id);
        var e2 = MatchEngine.FromTestState(s);
        Rejected(e2, Use(e2, 0, "wlpt-nanae-jade", nanae.Id, follower.Id));
        // A Battle action needs her at the battle; the Open half does not.
        var h = Table(Phase.Battle, 0);
        var home = Add(h, Nanae, 0, Zone.Home);
        Add(h, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(h, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(h), 0, "wlpt-nanae-jade", home.Id), "in a battle she must be at it");
    }

    // ---- Izuna ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IznuaBow()
    {
        // The Political Limited takes the Imperial Favor.
        var f = Table(Phase.Action, 0);
        var izuna = Add(f, Izuna, 0, Zone.Home);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Use(fe, 0, "noboru-exp2-favor", izuna.Id));
        Settle(fe);
        Eq(fe.State.FavorPlayerId, 0, "she took the Imperial Favor");

        // Limited (Action Phase): another player's card in a unit -- the Personality, an attached Follower -- never her own.
        var s = Table(Phase.Action, 0);
        var i = Add(s, Izuna, 0, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Attach(s, "training-follower", enemy);
        var holding = Add(s, "training-farm", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-izuna-bow-limited", i.Id);
        var offered = option.Slots?.FirstOrDefault(o => o.Slot == "target")?.CardIds ?? option.TargetIds ?? [];
        Check(offered.Contains(enemy.Id) && offered.Contains(follower.Id), "another player's Personality and attached card: " + string.Join(",", offered));
        Check(!offered.Contains(mine.Id) && !offered.Contains(i.Id) && !offered.Contains(holding.Id), "not her own cards, not a Holding");
        var honor = (Honor(e, 0), Honor(e, 1));
        Accept(e, Use(e, 0, "wlpt-izuna-bow-limited", i.Id, selections: Pick("target", enemy.Id)));
        Settle(e);
        Check(e.State.Cards[enemy.Id].Bowed, "the target is bowed");
        Check(e.State.PendingChoice is null && (Honor(e, 0), Honor(e, 1)) == honor, "an ordinary Lion samurai asks nothing and costs no Honor");
        Rejected(e, Use(e, 0, "wlpt-izuna-bow-limited", i.Id, selections: Pick("target", mine.Id)));
        Check(!CanUse(e, 0, "wlpt-izuna-bow-battle", i.Id), "the Battle half is not offered in the Action Phase, and shares the one use");

        // A Shadowlands target: the bow, then the choice of a player who gains or loses 2 Honor.
        var d = Table(Phase.Action, 0);
        var i2 = Add(d, Izuna, 0, Zone.Home);
        var dark = Kw(Add(d, "training-lion-samurai", 1, Zone.Home), "Shadowlands");
        var de = MatchEngine.FromTestState(d);
        Accept(de, Use(de, 0, "wlpt-izuna-bow-limited", i2.Id, selections: Pick("target", dark.Id)));
        Check(de.State.Cards[dark.Id].Bowed, "bowed first");
        Eq(de.State.PendingChoice?.Type, "effectChoice", "the Honor choice follows");
        var choices = de.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).OrderBy(x => x).ToArray();
        Check(choices.SequenceEqual(["opponent-gain", "opponent-lose", "self-gain", "self-lose"]), "the four answers: " + string.Join(",", choices));
        var opponentBefore = Honor(de, 1);
        Accept(de, de.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "opponent-lose").Command!);
        Eq(Honor(de, 1), opponentBefore - 2, "the opponent loses 2 Honor");

        // The Battle half, in a battle, against an attached Ninja Follower of an enemy unit.
        var b = Table(Phase.Battle, 0);
        var i3 = Add(b, Izuna, 0, Zone.Battlefield, province: 0);
        var host = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ninjaFollower = Kw(Attach(b, "training-follower", host), "Ninja");
        var be = MatchEngine.FromTestState(b);
        var honorBefore = Honor(be, 0);
        Accept(be, Use(be, 0, "wlpt-izuna-bow-battle", i3.Id, selections: Pick("target", ninjaFollower.Id)));
        Check(be.State.Cards[ninjaFollower.Id].Bowed, "the attached card is bowed");
        Eq(be.State.PendingChoice?.Type, "effectChoice", "Ninja asks for the Honor choice too");
        Accept(be, be.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "self-gain").Command!);
        Eq(Honor(be, 0), honorBefore + 2, "the chosen player (himself) gained 2 Honor");
    }

    // ---- Kyuudo and Nagai ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static int UnitForce(MatchEngine e, int viewer, string id) => e.GetView(viewer).Players[viewer].Army.Single(c => c.Id == id).UnitForce ?? 0;

    private static void EliteWhileDefending()
    {
        var s = Table(Phase.Battle, 1); // player 1 attacks, player 0 defends
        var nagai = Add(s, Nagai, 0, Zone.Battlefield, bowed: true, province: 0);
        var kyuudo = Add(s, Kyuudo, 0, Zone.Battlefield, bowed: true, province: 0);
        var attackingNagai = Add(s, Nagai, 1, Zone.Battlefield, bowed: true, province: 0);
        var attackingKyuudo = Add(s, Kyuudo, 1, Zone.Battlefield, bowed: true, province: 0);
        var homeNagai = Add(s, Nagai, 0, Zone.Home);
        var homeKyuudo = Add(s, Kyuudo, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(nagai.Id, "Elite") && e.HasKeyword(kyuudo.Id, "Elite"), "Elite while defending");
        Check(!e.HasKeyword(attackingNagai.Id, "Elite") && !e.HasKeyword(attackingKyuudo.Id, "Elite"), "not while attacking");
        Check(!e.HasKeyword(homeNagai.Id, "Elite") && !e.HasKeyword(homeKyuudo.Id, "Elite"), "not at home");
        Check(!CardCatalog.Get(Nagai).Keywords!.Contains("Elite") && !CardCatalog.Get(Kyuudo).Keywords!.Contains("Elite"), "Elite is nothing printed");
        Eq(UnitForce(e, 0, nagai.Id), 4, "a bowed defending Nagai still contributes his Force (Elite)");
        Eq(UnitForce(e, 0, kyuudo.Id), 7, "and a bowed defending Kyuudo contributes his Force with the +3F");
        Eq(UnitForce(e, 1, attackingNagai.Id), 0, "bowed and attacking, he contributes nothing");
        Eq(UnitForce(e, 1, attackingKyuudo.Id), 0, "neither does she");
        Eq(e.CardForceForTest(kyuudo.Id), 7, "+3F while defending");
        Eq(e.CardForceForTest(attackingKyuudo.Id), 4, "printed Force while attacking");
        Eq(e.CardForceForTest(homeKyuudo.Id), 4, "printed Force at home");
        Eq(e.CardForceForTest(nagai.Id), 4, "Nagai has no Force bonus");

        // The roles swap with the turn: with player 0 the Attacker, the same cards stop being Elite and the other side's start.
        var swapped = Table(Phase.Battle, 0);
        var n0 = Add(swapped, Nagai, 0, Zone.Battlefield, province: 0);
        var n1 = Add(swapped, Nagai, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(swapped);
        Check(!e2.HasKeyword(n0.Id, "Elite") && e2.HasKeyword(n1.Id, "Elite"), "defending is read live, from whose battle it is");
    }

    // ---- Suzukaze ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardDefinition BarrenHolding() =>
        CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding && d.GoldProduction == 0 && d.Effect == "none" && (d.Abilities is null || d.Abilities.Length == 0) && d.Text == "");

    private static void SuzukazeGold()
    {
        var s = Table(Phase.Action, 0);
        var suzukaze = Add(s, Suzukaze, 0, Zone.Home);
        var farm = Add(s, "training-farm", 0, Zone.Home);
        var enemyFarm = Add(s, "training-farm", 1, Zone.Home);
        var none = Add(s, BarrenHolding().Id, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-suzukaze-gold", suzukaze.Id);
        Check(option.TargetIds!.Contains(farm.Id) && option.TargetIds.Contains(enemyFarm.Id) && option.TargetIds.Contains(none.Id), "any Holding, either side's");
        Check(!option.TargetIds.Contains(suzukaze.Id), "not a Personality");
        Eq(e.GoldProduced(farm.Id, 0), 2, "a Farm produces 2");
        Accept(e, Offer(e, 0, "wlpt-suzukaze-gold", suzukaze.Id, farm.Id));
        Settle(e);
        Eq(e.GoldProduced(farm.Id, 0), 3, "1 additional Gold when it bows to produce Gold");
        Eq(e.GoldProduced(enemyFarm.Id, 1), 2, "another Holding is untouched");
        var modifier = e.State.Cards[farm.Id].Modifiers.Single(m => m.Stat == "GoldProduction");
        Check(modifier.Amount == 1 && !modifier.Permanent, "a turn-long modifier (R-001), not a permanent one");
        Check(!CanUse(e, 0, "wlpt-suzukaze-gold", suzukaze.Id), "a Limited action: once a turn");

        // A Holding that produces nothing gets nothing; an enemy Holding may be named (it helps its controller).
        var t = Table(Phase.Action, 0);
        var s2 = Add(t, Suzukaze, 0, Zone.Home);
        var barren2 = Add(t, BarrenHolding().Id, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 0, "wlpt-suzukaze-gold", s2.Id, barren2.Id));
        Eq(e2.GoldProduced(barren2.Id, 0), 0, "no production to add to");
        Check(!e2.State.Cards[barren2.Id].Modifiers.Any(m => m.Stat == "GoldProduction"), "and no modifier is left on it");
        var u = Table(Phase.Action, 0);
        var s3 = Add(u, Suzukaze, 0, Zone.Home);
        var theirs = Add(u, "training-farm", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(u);
        Accept(e3, Offer(e3, 0, "wlpt-suzukaze-gold", s3.Id, theirs.Id));
        Eq(e3.GoldProduced(theirs.Id, 1), 3, "an enemy Holding produces more too");
        var inBattle = Table(Phase.Battle, 0);
        var s4 = Add(inBattle, Suzukaze, 0, Zone.Battlefield, province: 0);
        Add(inBattle, "training-farm", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(inBattle), 0, "wlpt-suzukaze-gold", s4.Id), "Limited: not in a battle");
    }

    // ---- Wakui ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Wakui, CardInstance Magistrate, CardInstance Tess) WakuiTable()
    {
        var s = Table(Phase.Battle, 1);
        var wakui = Add(s, Wakui, 0, Zone.Battlefield, province: 0);
        var magistrate = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
        var tess = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(s), wakui, magistrate, tess);
    }

    private static void WakuiRedirect()
    {
        var (e, wakui, magistrate, tess) = WakuiTable();
        Accept(e, Offer(e, 1, "tsuji-bow", tess.Id, magistrate.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "an action that targets a Magistrate at his battlefield opens the window");
        Accept(e, Enabled(e, 0, "wlpt-wakui-redirect", wakui.Id).Command!);
        Settle(e);
        Check(e.State.Cards[wakui.Id].Bowed, "the action targets Wakui instead: he is bowed");
        Check(!e.State.Cards[magistrate.Id].Bowed, "and the Magistrate is not");

        // Declined: the Magistrate is bowed.
        var (d, w2, m2, t2) = WakuiTable();
        Accept(d, Offer(d, 1, "tsuji-bow", t2.Id, m2.Id));
        Settle(d);
        Check(d.State.Cards[m2.Id].Bowed && !d.State.Cards[w2.Id].Bowed, "a Reaction is optional");

        // Not a Magistrate.
        var s = Table(Phase.Battle, 1);
        var w = Add(s, Wakui, 0, Zone.Battlefield, province: 0);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var t = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        var p = MatchEngine.FromTestState(s);
        Accept(p, Offer(p, 1, "tsuji-bow", t.Id, plain.Id));
        Check(!CanUse(p, 0, "wlpt-wakui-redirect", w.Id), "a Personality who is no Magistrate is not covered");
        // Wakui at home has no battlefield.
        var home = Table(Phase.Battle, 1);
        var hw = Add(home, Wakui, 0, Zone.Home);
        var hm = Kw(Add(home, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
        var ht = Add(home, Tesskss, 1, Zone.Battlefield, province: 0);
        var he = MatchEngine.FromTestState(home);
        Accept(he, Offer(he, 1, "tsuji-bow", ht.Id, hm.Id));
        Check(!CanUse(he, 0, "wlpt-wakui-redirect", hw.Id), "Wakui at home has no battlefield: no Reaction for a Magistrate at the battle");
        // A bowed Wakui takes no Reaction.
        var bowed = Table(Phase.Battle, 1);
        var bw = Add(bowed, Wakui, 0, Zone.Battlefield, bowed: true, province: 0);
        var bm = Kw(Add(bowed, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
        var bt = Add(bowed, Tesskss, 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(bowed);
        Accept(be, Offer(be, 1, "tsuji-bow", bt.Id, bm.Id));
        Check(!CanUse(be, 0, "wlpt-wakui-redirect", bw.Id), "a bowed Wakui takes no Reaction");
        // The Magistrate Wakui himself is the target: nothing to redirect.
        var (self, sw, _, st) = WakuiTable();
        Accept(self, Offer(self, 1, "tsuji-bow", st.Id, sw.Id));
        Check(!CanUse(self, 0, "wlpt-wakui-redirect", sw.Id), "an action that targets Wakui himself is not redirected to him");
    }

    // ---- Ibara ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IbaraOpposes()
    {
        var s = Table(Phase.Battle, 0);
        var ibara = Add(s, Ibara, 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Attach(s, "training-follower", enemy);
        var athome = Add(s, "training-lion-samurai", 1, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-ibara-oppose", ibara.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(follower.Id), "an enemy Personality or Follower");
        Check(!option.TargetIds.Contains(mine.Id) && !option.TargetIds.Contains(athome.Id), "not her own, not one out of the battle");
        Rejected(e, Use(e, 0, "wlpt-ibara-oppose", ibara.Id, mine.Id));
        Rejected(e, Use(e, 0, "wlpt-ibara-oppose", ibara.Id, athome.Id));
        var force = e.CardForceForTest(enemy.Id);
        Accept(e, Offer(e, 0, "wlpt-ibara-oppose", ibara.Id, enemy.Id));
        Settle(e);
        Eq(e.State.Cards[ibara.Id].Zone, Zone.Battlefield, "she moved to the battlefield");
        Eq(e.State.Cards[ibara.Id].ProvinceIndex, 0, "to the target's battlefield");
        Eq(e.CardForceForTest(enemy.Id), force - 3, "-3F");
        Check(!e.State.Cards[enemy.Id].Bowed, "not Mantis Clan: not bowed");

        // A Mantis Clan Personality is bowed too (and she needed no move: she already opposes him).
        var m = Table(Phase.Battle, 0);
        var ib = Add(m, Ibara, 0, Zone.Battlefield, province: 0);
        var mantis = Add(m, Tansen, 1, Zone.Battlefield, province: 0);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Offer(me, 0, "wlpt-ibara-oppose", ib.Id, mantis.Id));
        Settle(me);
        Eq(me.CardForceForTest(mantis.Id), 0, "-3F on a Force 2 Personality (it floors at 0)");
        Check(me.State.Cards[mantis.Id].Bowed, "if it is Mantis Clan, bow it");
        Eq(me.State.Cards[ib.Id].Zone, Zone.Battlefield, "she opposes it");
        // A Follower of a Mantis Clan Personality is not itself Mantis Clan: -3F, not bowed.
        var f = Table(Phase.Battle, 0);
        var ib2 = Add(f, Ibara, 0, Zone.Battlefield, province: 0);
        var host = Add(f, Tansen, 1, Zone.Battlefield, province: 0);
        var fol = Attach(f, "training-follower", host);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Offer(fe, 0, "wlpt-ibara-oppose", ib2.Id, fol.Id));
        Settle(fe);
        Eq(fe.CardForceForTest(fol.Id), 0, "-3F on a Follower of Force 2");
        Check(!fe.State.Cards[fol.Id].Bowed && !fe.State.Cards[host.Id].Bowed, "an unaligned Follower is not bowed, and its Mantis host is untouched");

        Check(!CanUse(MatchEngine.FromTestState(Table(Phase.Action, 0)), 0, "wlpt-ibara-oppose", ibara.Id), "Battle only");
    }

    // ---- duels: Iza, Kyoshiro, Riku -------------------------------------------------------------------------------------------------------------------------------------------

    private static void RunDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null, "the duel is over");
    }

    private static void ChiBonus(CardInstance card, int amount) => card.Modifiers.Add(new("test-source", "test", "Chi", amount));

    private static void NamedDuels()
    {
        var s = Table(Phase.Action, 0);
        var iza = Add(s, Iza, 0, Zone.Home); ChiBonus(iza, 3);
        var kyoshiro = Add(s, Kyoshiro, 1, Zone.Home);
        var other = Add(s, "training-lion-samurai", 1, Zone.Home);
        var ownKyoshiro = Add(s, Kyoshiro, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-iza-duel", iza.Id);
        Check(option.TargetIds!.SequenceEqual([kyoshiro.Id]), "only another player's Mirumoto Kyoshiro: " + string.Join(",", option.TargetIds!));
        Rejected(e, Use(e, 0, "wlpt-iza-duel", iza.Id, other.Id));
        Rejected(e, Use(e, 0, "wlpt-iza-duel", iza.Id, ownKyoshiro.Id));
        Accept(e, Offer(e, 0, "wlpt-iza-duel", iza.Id, kyoshiro.Id));
        Check(e.State.PendingDuel is not null && e.State.PendingDuel.Stage != "AwaitingAccept", "he may not refuse: the duel is under way");
        RunDuel(e);
        Eq(e.State.LastDuel!.Result!.WinnerId, iza.Id, "Iza (Chi 6) beats Kyoshiro (Chi 3)");
        Eq(e.State.Cards[kyoshiro.Id].Zone, Zone.Dead, "the loser is destroyed");
        Eq(e.State.Cards[iza.Id].Zone, Zone.Home, "the winner stands");

        // Iza loses: she is the loser and is destroyed.
        var t = Table(Phase.Action, 0);
        var iza2 = Add(t, Iza, 0, Zone.Home);
        var kyo2 = Add(t, Kyoshiro, 1, Zone.Home); ChiBonus(kyo2, 3);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 0, "wlpt-iza-duel", iza2.Id, kyo2.Id));
        RunDuel(e2);
        Eq(e2.State.Cards[iza2.Id].Zone, Zone.Dead, "the challenger who loses is destroyed too");
        Eq(e2.State.Cards[kyo2.Id].Zone, Zone.Home);

        // Kyoshiro duels her.
        var k = Table(Phase.Action, 0);
        var kyo = Add(k, Kyoshiro, 0, Zone.Home); ChiBonus(kyo, 3);
        var enemyIza = Add(k, Iza, 1, Zone.Home);
        var otherCrane = Add(k, "training-crane-samurai", 1, Zone.Home);
        var ownIza = Add(k, Iza, 0, Zone.Home);
        var ke = MatchEngine.FromTestState(k);
        Check(Enabled(ke, 0, "wlpt-kyoshiro-duel", kyo.Id).TargetIds!.SequenceEqual([enemyIza.Id]), "only another player's Doji Iza");
        Rejected(ke, Use(ke, 0, "wlpt-kyoshiro-duel", kyo.Id, otherCrane.Id));
        Rejected(ke, Use(ke, 0, "wlpt-kyoshiro-duel", kyo.Id, ownIza.Id));
        Accept(ke, Offer(ke, 0, "wlpt-kyoshiro-duel", kyo.Id, enemyIza.Id));
        RunDuel(ke);
        Eq(ke.State.Cards[enemyIza.Id].Zone, Zone.Dead, "Kyoshiro wins and she is destroyed");
        Check(!CanUse(ke, 0, "wlpt-kyoshiro-duel", kyo.Id), "one use a turn");

        // Open: not in a battle.
        var inBattle = Table(Phase.Battle, 0);
        var bi = Add(inBattle, Iza, 0, Zone.Battlefield, province: 0);
        Add(inBattle, Kyoshiro, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(inBattle), 0, "wlpt-iza-duel", bi.Id), "Open actions are Action Phase actions (R-030)");
    }

    // ---- Fubuko --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card) Recruit(string definition, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        setup?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        return (e, e.State.Cards[cardId]);
    }

    private static GameCommand PurchaseFromProvince(MatchEngine e, int player, string cardId) =>
        new("purchase", player, e.Version, CardId: cardId, SourceIds: e.QuotePurchase(player, cardId).Payments[0].SourceIds);

    private static void Richer(MatchState s, int holdings = 6) { for (var i = 0; i < holdings; i++) Add(s, "training-marketplace", 0, Zone.Home); }

    private const string FallingLeaf = "oracle-l5r-2454", StrikingVoid = "oracle-l5r-7587";

    private static void FubukoKiho()
    {
        var (dragon, f) = Recruit(Fubuko, s => { Richer(s, 8); s.Players[0].Clan = "Dragon"; });
        Eq(dragon.QuotePurchase(0, f.Id).Cost, 7, "a Dragon Clan player pays 1 less (8 -> 7)");
        var (crane, f2) = Recruit(Fubuko, s => Richer(s, 8));
        Eq(crane.QuotePurchase(0, f2.Id).Cost, 8, "anyone else pays the printed 8");

        var s = Table(Phase.Battle, 0);
        var fubuko = Add(s, Fubuko, 0, Zone.Battlefield, province: 0);
        var monk = Add(s, Suzukaze, 0, Zone.Battlefield, province: 0); // another Monk, who may not perform it
        var kiho = Add(s, FallingLeaf, 0, Zone.FateDiscard);
        var inHand = Add(s, StrikingVoid, 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-fubuko-kiho", fubuko.Id);
        Check(option.TargetIds!.Contains(kiho.Id), "a Kiho Strategy in your discard pile");
        Check(!option.TargetIds.Contains(inHand.Id), "not one in the hand");
        Accept(e, Offer(e, 0, "wlpt-fubuko-kiho", fubuko.Id, kiho.Id));
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.FromDiscard && g.ScopeCardIds!.Contains(kiho.Id) && g.PerformerId == fubuko.Id), "an additional action for that card alone, which Fubuko must perform");
        // The Strategy is playable from the discard pile: with Fubuko as the performer, not with another Monk.
        var play = Batch336Support.Options(e, 0, "falling-leaf-strike", kiho.Id);
        Check(play.Any(o => o.Enabled), "its Battle action is offered from the discard pile: " + string.Join("; ", play.Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        Rejected(e, Use(e, 0, "falling-leaf-strike", kiho.Id, enemy.Id, performerId: monk.Id));
        Accept(e, Use(e, 0, "falling-leaf-strike", kiho.Id, enemy.Id, performerId: fubuko.Id));
        Settle(e);
        Eq(e.State.Cards[kiho.Id].Zone, Zone.RemovedFromGame, "After it resolves, remove the Strategy from the game");
        Eq(e.CardForceForTest(enemy.Id), 1, "its effect happened (-3F on a Force 4 Lion samurai)");

        // A Kiho in the hand is no target, and a non-Kiho in the discard pile neither.
        var t = Table(Phase.Battle, 0);
        var fu = Add(t, Fubuko, 0, Zone.Battlefield, province: 0);
        Add(t, "oracle-l5r-1004", 0, Zone.FateDiscard); // Blade of Champions: an Item, not a Strategy
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var te = MatchEngine.FromTestState(t);
        Check(!CanUse(te, 0, "wlpt-fubuko-kiho", fu.Id), "with no Kiho Strategy in the discard pile there is no target");
    }

    // ---- Shaocheng -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShaochengTurn()
    {
        MatchEngine Scenario(out CardInstance shaocheng, out CardInstance faceDownLion, out CardInstance faceDownUnique, out CardInstance holdingCard)
        {
            var (e0, card) = Recruit(Shaocheng, s => Richer(s, 4));
            var st = e0.State;
            var lion = st.Cards[st.Players[1].Provinces[1].CardId!]; lion.DefinitionId = "training-lion-samurai"; lion.FaceUp = false;
            var unique = st.Cards[st.Players[1].Provinces[2].CardId!]; unique.DefinitionId = Riku; unique.FaceUp = false;
            var holding = st.Cards[st.Players[0].Provinces[1].CardId!]; holding.DefinitionId = "training-farm"; holding.FaceUp = false;
            var e = MatchEngine.FromTestState(st);
            shaocheng = e.State.Cards[card.Id]; faceDownLion = e.State.Cards[lion.Id]; faceDownUnique = e.State.Cards[unique.Id]; holdingCard = e.State.Cards[holding.Id];
            return e;
        }
        var e1 = Scenario(out var sh, out var lion, out var unique, out var holding);
        Accept(e1, PurchaseFromProvince(e1, 0, sh.Id));
        // The entry window (Proclaim first, when it is offered) leads to the Reaction.
        for (var guard = 0; guard < 4 && !CanUse(e1, 0, "wlpt-shaocheng-turn", sh.Id) && e1.State.PendingChoice is not null; guard++)
            Accept(e1, new("declineReaction", e1.State.PriorityPlayerId, e1.Version));
        var option = Enabled(e1, 0, "wlpt-shaocheng-turn", sh.Id);
        Check(option.TargetIds!.Contains(lion.Id) && option.TargetIds.Contains(unique.Id) && option.TargetIds.Contains(holding.Id), "a card in any province, face down or not, either player's");
        Check(!option.TargetIds.Contains(sh.Id), "not a card in play");
        var lionProvince = lion.ProvinceIndex!.Value;
        Accept(e1, Offer(e1, 0, "wlpt-shaocheng-turn", sh.Id, lion.Id));
        Eq(e1.State.Cards[lion.Id].Zone, Zone.Dead, "a non-Unique Personality is discarded and becomes dead");
        Check(!e1.State.Cards[lion.Id].Dishonorable, "honorably dead");
        Check(e1.State.Players[1].Provinces[lionProvince].CardId is { } refill && refill != lion.Id, "the province is refilled as after any discard from it");

        // A Unique Personality is only turned face up.
        var e2 = Scenario(out var sh2, out _, out var unique2, out _);
        Accept(e2, PurchaseFromProvince(e2, 0, sh2.Id));
        for (var guard = 0; guard < 4 && !CanUse(e2, 0, "wlpt-shaocheng-turn", sh2.Id) && e2.State.PendingChoice is not null; guard++)
            Accept(e2, new("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Accept(e2, Offer(e2, 0, "wlpt-shaocheng-turn", sh2.Id, unique2.Id));
        Check(e2.State.Cards[unique2.Id].FaceUp && e2.State.Cards[unique2.Id].Zone == Zone.Province, "a Unique Personality is turned face up and stays");

        // A card that is not a Personality is only turned face up.
        var e3 = Scenario(out var sh3, out _, out _, out var holding3);
        Accept(e3, PurchaseFromProvince(e3, 0, sh3.Id));
        for (var guard = 0; guard < 4 && !CanUse(e3, 0, "wlpt-shaocheng-turn", sh3.Id) && e3.State.PendingChoice is not null; guard++)
            Accept(e3, new("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        Accept(e3, Offer(e3, 0, "wlpt-shaocheng-turn", sh3.Id, holding3.Id));
        Check(e3.State.Cards[holding3.Id].FaceUp && e3.State.Cards[holding3.Id].Zone == Zone.Province, "a Holding is turned face up and stays");

        // Declined (a Reaction is optional), and not offered with the card out of its entry.
        var e4 = Scenario(out var sh4, out var lion4, out _, out _);
        Accept(e4, PurchaseFromProvince(e4, 0, sh4.Id));
        Settle(e4);
        Check(!e4.State.Cards[lion4.Id].FaceUp, "declined: nothing is turned");
        Check(!CanUse(e4, 0, "wlpt-shaocheng-turn", sh4.Id), "and it is not an action a Personality in play may take later");
    }

    // ---- Moshi Ira -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IraWeakens()
    {
        var s = Table(Phase.Battle, 1); // player 1 attacks; Ira defends
        var ira = Add(s, Ira, 0, Zone.Battlefield, province: 0);
        var tess = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(tess.Id);
        Accept(e, Offer(e, 1, "tsuji-bow", tess.Id, ira.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "an action that targets him opens the window");
        Accept(e, Enabled(e, 0, "wlpt-ira-weaken", ira.Id).Command!);
        Eq(e.CardForceForTest(tess.Id), force, "the -3F waits for the action to resolve");
        Settle(e);
        Check(e.State.Cards[ira.Id].Bowed, "the action itself went on (Ira is bowed)");
        Eq(e.CardForceForTest(tess.Id), force - 3, "the performer has -3F after the action resolved");
        Check(e.State.WlPersTorn.IraNotes.Count == 0, "the note is spent");

        // A card attached to him is in his unit.
        var (f, ira2, fol2, tess2) = IraTable(attachedTarget: true);
        Accept(f, Offer(f, 1, "tsuji-bow", tess2.Id, fol2.Id));
        Accept(f, Enabled(f, 0, "wlpt-ira-weaken", ira2.Id).Command!);
        Settle(f);
        Eq(f.CardForceForTest(tess2.Id), 0, "an action that targets his Follower weakens its performer too (3 - 3)");

        // Declined: no penalty.
        var (d, ira3, _, tess3) = IraTable(attachedTarget: false);
        Accept(d, Offer(d, 1, "tsuji-bow", tess3.Id, ira3.Id));
        Settle(d);
        Eq(d.CardForceForTest(tess3.Id), 3, "a Reaction is optional");

        // A card outside his unit is not covered.
        var o = Table(Phase.Battle, 1);
        var ira4 = Add(o, Ira, 0, Zone.Battlefield, province: 0);
        var buddy = Add(o, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var tess4 = Add(o, Tesskss, 1, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 1, "tsuji-bow", tess4.Id, buddy.Id));
        Check(!CanUse(oe, 0, "wlpt-ira-weaken", ira4.Id), "a card in another unit does not offer it");
        Settle(oe);
        Eq(oe.CardForceForTest(tess4.Id), 3, "and nothing happens to the performer");
    }

    private static (MatchEngine E, CardInstance Ira, CardInstance Follower, CardInstance Tess) IraTable(bool attachedTarget)
    {
        var s = Table(Phase.Battle, 1);
        var ira = Add(s, Ira, 0, Zone.Battlefield, province: 0);
        var follower = Attach(s, "training-follower", ira);
        var tess = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(s), ira, follower, tess);
    }

    // ---- Riku ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RikuRedirectAndDuel()
    {
        foreach (var printing in new[] { Tamahime, TamahimeExp })
        {
            var s = Table(Phase.Battle, 1);
            var riku = Add(s, Riku, 0, Zone.Battlefield, province: 0);
            var tamahime = Add(s, printing, 0, Zone.Battlefield, province: 0);
            var tess = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "tsuji-bow", tess.Id, tamahime.Id));
            Accept(e, Enabled(e, 0, "wlpt-riku-redirect", riku.Id).Command!);
            Settle(e);
            Check(e.State.Cards[riku.Id].Bowed && !e.State.Cards[tamahime.Id].Bowed, $"the action targets Riku instead of Tamahime ({printing})");
        }
        // Not for another Personality, not when Riku is bowed, and an own action is nobody else's.
        var o = Table(Phase.Battle, 1);
        var riku2 = Add(o, Riku, 0, Zone.Battlefield, province: 0);
        var friend = Add(o, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var tess2 = Add(o, Tesskss, 1, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 1, "tsuji-bow", tess2.Id, friend.Id));
        Check(!CanUse(oe, 0, "wlpt-riku-redirect", riku2.Id), "an action on anyone but Tamahime is not redirected");
        var b = Table(Phase.Battle, 1);
        var riku3 = Add(b, Riku, 0, Zone.Battlefield, bowed: true, province: 0);
        var tam3 = Add(b, Tamahime, 0, Zone.Battlefield, province: 0);
        var tess3 = Add(b, Tesskss, 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, "tsuji-bow", tess3.Id, tam3.Id));
        Check(!CanUse(be, 0, "wlpt-riku-redirect", riku3.Id), "a bowed Riku takes no Reaction");

        // The Iaijutsu Battle duel: only enemy Personalities; the loser is destroyed.
        var d = Table(Phase.Battle, 0);
        var rikuD = Add(d, Riku, 0, Zone.Battlefield, province: 0);
        var enemy = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(d, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d);
        var option = Enabled(de, 0, "wlpt-riku-duel", rikuD.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(mine.Id), "an enemy Personality only");
        Rejected(de, Use(de, 0, "wlpt-riku-duel", rikuD.Id, mine.Id));
        Accept(de, Offer(de, 0, "wlpt-riku-duel", rikuD.Id, enemy.Id));
        RunDuel(de);
        Eq(de.State.LastDuel!.Result!.WinnerId, rikuD.Id, "Riku (Chi 5) beats a Chi 3 Lion samurai");
        Eq(de.State.Cards[enemy.Id].Zone, Zone.Dead, "the loser is destroyed");
        var inAction = Table(Phase.Action, 0);
        var r2 = Add(inAction, Riku, 0, Zone.Home);
        Add(inAction, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(inAction), 0, "wlpt-riku-duel", r2.Id), "a Battle action");
    }

    // ---- Tetsaka -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TetsakaKeywords()
    {
        foreach (var (ability, keyword, other) in new[] { ("wlpt-tetsaka-cavalry", "Cavalry", "wlpt-tetsaka-naval"), ("wlpt-tetsaka-naval", "Naval", "wlpt-tetsaka-cavalry") })
        {
            var s = Table(Phase.Action, 0);
            var tetsaka = Add(s, Tetsaka, 0, Zone.Home);
            var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
            var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
            var bowedShugenja = Add(s, "training-shugenja", 0, Zone.Home, bowed: true);
            var e = MatchEngine.FromTestState(s);
            Check(!e.HasKeyword(tetsaka.Id, keyword), $"{keyword} is not printed");
            Rejected(e, Use(e, 0, ability, tetsaka.Id, performerId: samurai.Id));
            Rejected(e, Use(e, 0, ability, tetsaka.Id, performerId: bowedShugenja.Id));
            Accept(e, Use(e, 0, ability, tetsaka.Id, performerId: shugenja.Id));
            Settle(e);
            Check(e.HasKeyword(tetsaka.Id, keyword), $"he has {keyword}");
            Check(e.State.Cards[shugenja.Id].Bowed && !e.State.Cards[tetsaka.Id].Bowed, "the performing Shugenja is bowed, Tetsaka is not");
            Check(e.State.Cards[tetsaka.Id].GrantedKeywords.Any(g => g.Keyword == keyword && g.Duration == GrantDuration.Turn), "for the turn (R-001)");
            Check(!CanUse(e, 0, other, tetsaka.Id) && !CanUse(e, 0, ability, tetsaka.Id), "Cavalry and Naval share the one use");
        }
        var inBattle = Table(Phase.Battle, 0);
        var t2 = Add(inBattle, Tetsaka, 0, Zone.Battlefield, province: 0);
        Add(inBattle, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(inBattle), 0, "wlpt-tetsaka-cavalry", t2.Id), "Open: an Action Phase action");
    }

    // ---- Sihung --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string Taigo = "oracle-l5r-10100";

    private static void SihungBoost()
    {
        var s = Table(Phase.Battle, 0);
        var sihung = Add(s, Sihung, 0, Zone.Battlefield, province: 0);
        var taigo = Add(s, Taigo, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(taigo.Id);
        Accept(e, Use(e, 0, "taigo-tactical-bow", taigo.Id, selections: Pick("target", enemy.Id)));
        Settle(e, 0);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "after the Tactical action resolved");
        Accept(e, Enabled(e, 0, "wlpt-sihung-boost", sihung.Id).Command!);
        Eq(e.CardForceForTest(taigo.Id), force + 2, "the performer has +2F");
        Eq(e.CardForceForTest(sihung.Id), 4, "Sihung himself is not the performer");
        Settle(e);

        // A non-Tactical action does not offer it.
        var n = Table(Phase.Battle, 0);
        var sihung2 = Add(n, Sihung, 0, Zone.Battlefield, province: 0);
        var tess = Add(n, Tesskss, 0, Zone.Battlefield, province: 0);
        var enemy2 = Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 0, "tsuji-bow", tess.Id, enemy2.Id));
        Settle(ne);
        Check(!CanUse(ne, 0, "wlpt-sihung-boost", sihung2.Id), "Tess'kss's bow is not Tactical");

        // Another player's Tactical action is not "your" action.
        var o = Table(Phase.Battle, 1);
        var sihungDefender = Add(o, Sihung, 0, Zone.Battlefield, province: 0);
        var taigoEnemy = Add(o, Taigo, 1, Zone.Battlefield, province: 0);
        var defenderCrane = Add(o, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Use(oe, 1, "taigo-tactical-bow", taigoEnemy.Id, selections: Pick("target", defenderCrane.Id)));
        Settle(oe, 0);
        if (oe.State.PendingChoice is not null) Check(!CanUse(oe, 0, "wlpt-sihung-boost", sihungDefender.Id), "an opposing player's Tactical action is not yours");
        Settle(oe);
        Eq(oe.CardForceForTest(taigoEnemy.Id), 4, "no bonus for the other side's performer");
    }

    // ---- Makiko --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MakikoReduces()
    {
        // Political Open: bow Makiko, take the Imperial Favor.
        var f = Table(Phase.Action, 0);
        var mk = Add(f, Makiko, 0, Zone.Home);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Use(fe, 0, "makiko-favor", mk.Id));
        Settle(fe);
        Eq(fe.State.FavorPlayerId, 0, "she took the Imperial Favor");
        Check(fe.State.Cards[mk.Id].Bowed, "bowing Makiko was the cost");

        // Another player's action gains Honor: Akahiko's "Gain 1 Honor" (player 1 acts, Makiko is player 0's).
        var s = Table(Phase.Battle, 1);
        var makiko = Add(s, Makiko, 0, Zone.Battlefield, province: 0);
        var akahiko = Add(s, Akahiko, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honor = (Honor(e, 0), Honor(e, 1));
        Accept(e, Offer(e, 1, "wlpt-akahiko-home", akahiko.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "before the Honor gain from the action's effects");
        Accept(e, Enabled(e, 0, "wlpt-makiko-reduce", makiko.Id).Command!);
        Settle(e);
        Check(e.State.Cards[makiko.Id].Bowed, "bowing Makiko is the cost");
        Eq(Honor(e, 1), honor.Item2, "a gain of 1, reduced by 2, is 0");
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "the action itself resolved: he went home");

        // Declined: the gain stands.
        var s2 = Table(Phase.Battle, 1);
        Add(s2, Makiko, 0, Zone.Battlefield, province: 0);
        var a2 = Add(s2, Akahiko, 1, Zone.Battlefield, province: 0);
        var t2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var h2 = Honor(e2, 1);
        Accept(e2, Offer(e2, 1, "wlpt-akahiko-home", a2.Id, t2.Id));
        Settle(e2);
        Eq(Honor(e2, 1), h2 + 1, "a Reaction is optional");

        // Bowed Makiko cannot pay the bow; a Honor LOSS is not hers to reduce (the window does not offer her).
        var b = Table(Phase.Battle, 1);
        var bm = Add(b, Makiko, 0, Zone.Battlefield, bowed: true, province: 0);
        var a3 = Add(b, Akahiko, 1, Zone.Battlefield, province: 0);
        var t3 = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, "wlpt-akahiko-home", a3.Id, t3.Id));
        Check(be.State.PendingChoice?.Type != "honorChangeReaction" || !CanUse(be, 0, "wlpt-makiko-reduce", bm.Id), "a bowed Makiko is not offered");
        Settle(be);

        var l = Table(Phase.Action, 1);
        var izuna = Add(l, Izuna, 1, Zone.Home);
        var lm = Add(l, Makiko, 0, Zone.Home);
        var dark = Kw(Add(l, "training-lion-samurai", 0, Zone.Home), "Shadowlands");
        var le = MatchEngine.FromTestState(l);
        var lossBefore = Honor(le, 0);
        Accept(le, Use(le, 1, "wlpt-izuna-bow-limited", izuna.Id, selections: Pick("target", dark.Id)));
        Accept(le, le.QueryActions(1).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "opponent-lose").Command!);
        Check(le.State.PendingChoice?.Type != "honorChangeReaction", "a loss opens no window for Makiko");
        Eq(Honor(le, 0), lossBefore - 2, "the loss of 2 stands");
        Check(!le.State.Cards[lm.Id].Bowed, "and she was not bowed");

        // The reduction belongs to the one action: the next gain (another action) is whole.
        var s4 = Table(Phase.Battle, 1);
        Add(s4, Makiko, 0, Zone.Battlefield, province: 0);
        var a4 = Add(s4, Akahiko, 1, Zone.Battlefield, province: 0);
        var t4 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "wlpt-akahiko-home", a4.Id, t4.Id));
        Accept(e4, e4.QueryActions(0).Single(a => a.Enabled && a.Command?.AbilityId == "wlpt-makiko-reduce").Command!);
        Settle(e4);
        Check(e4.State.WlPersTorn.HonorGainReducedActionKey is not null, "the mark stands for that action");
    }

    // ---- Shinoko -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShinokoBrash()
    {
        var s = Table(Phase.Action, 0);
        var shinoko = Add(s, Shinoko, 0, Zone.Home);
        var own = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpt-shinoko-brash", shinoko.Id);
        Check(option.TargetIds!.Contains(own.Id) && option.TargetIds.Contains(enemy.Id) && option.TargetIds.Contains(shinoko.Id), "any Personality");
        Check(!e.HasKeyword(own.Id, "Brash"), "Brash is not printed");
        Accept(e, Offer(e, 0, "wlpt-shinoko-brash", shinoko.Id, enemy.Id));
        Settle(e);
        Check(e.HasKeyword(enemy.Id, "Brash") && !e.HasKeyword(own.Id, "Brash"), "only the target has Brash");
        Check(e.State.Cards[enemy.Id].GrantedKeywords.Any(g => g.Keyword == "Brash" && g.Duration == GrantDuration.Turn), "for the turn (R-001)");
        Check(!CanUse(e, 0, "wlpt-shinoko-brash", shinoko.Id), "once a turn");

        var inBattle = Table(Phase.Battle, 0);
        var sh = Add(inBattle, Shinoko, 0, Zone.Battlefield, province: 0);
        Add(inBattle, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(inBattle), 0, "wlpt-shinoko-brash", sh.Id), "Open: an Action Phase action");
    }

    // ---- Suying --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ResolveBattle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.CurrentBattleProvince is not null && e.State.PendingChoice?.Type != "afterResolutionReaction"; guard++)
            Accept(e, new("pass", e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.Version));
        Settle(e);
    }

    private static int HonorAfterAttack(string attackerCard, bool withDefender, int attackerForce, int provinceStrength)
    {
        var s = Table(Phase.Battle, 0);
        var attacker = Add(s, attackerCard, 0, Zone.Battlefield, province: 0); Force(attacker, attackerForce);
        if (withDefender) Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        s.Players[1].Provinces[0].Strength = provinceStrength;
        var e = MatchEngine.FromTestState(s);
        ResolveBattle(e);
        return Honor(e, 0);
    }

    private static void SuyingHonor()
    {
        // The same battle with a plain Unicorn samurai in her place: the difference is exactly her 2 Honor.
        const string Plain = "training-lion-veteran";
        Eq(HonorAfterAttack(Suying, true, 20, 3) - HonorAfterAttack(Plain, true, 20, 3), 2, "she destroyed a defending army (and the province): 2 Honor");
        Eq(HonorAfterAttack(Suying, false, 20, 3) - HonorAfterAttack(Plain, false, 20, 3), 2, "the province alone, with no defending army, is enough");
        Eq(HonorAfterAttack(Suying, true, 20, 99) - HonorAfterAttack(Plain, true, 20, 99), 2, "an army alone, with the province standing, is enough");

        // Nothing destroyed: her side loses or ties without killing; no Honor.
        var lose = Table(Phase.Battle, 0);
        var weak = Add(lose, Suying, 0, Zone.Battlefield, province: 0); Force(weak, -3);
        Add(lose, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var le = MatchEngine.FromTestState(lose);
        var before = Honor(le, 0);
        ResolveBattle(le);
        Check(Honor(le, 0) <= before, "a beaten Suying gains nothing");

        // As a defender, her army destroys no defending army.
        var d = Table(Phase.Battle, 1);
        var defender = Add(d, Suying, 0, Zone.Battlefield, province: 0); Force(defender, 20);
        Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var swapped = Table(Phase.Battle, 1);
        var plainDefender = Add(swapped, Plain, 0, Zone.Battlefield, province: 0); Force(plainDefender, 20);
        Add(swapped, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d); var se = MatchEngine.FromTestState(swapped);
        ResolveBattle(de); ResolveBattle(se);
        Eq(Honor(de, 0), Honor(se, 0), "no extra Honor for a defending Suying");
    }

    // ---- Daito ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DaitoBribes()
    {
        var s = Table(Phase.Action, 0);
        var daito = Add(s, Daito, 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        Richer(s);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "wlpt-daito-bow", daito.Id).TargetIds!.Contains(target.Id) && Enabled(e, 0, "wlpt-daito-gold", daito.Id).TargetIds!.Contains(target.Id), "either way, any Personality");
        Accept(e, Offer(e, 0, "wlpt-daito-bow", daito.Id, target.Id));
        Settle(e);
        Check(e.State.Cards[daito.Id].Bowed, "Daito was bowed");
        Eq(e.State.Cards[target.Id].StraightenHolds.Count, 1, "the target's straightening is held");
        var hold = e.State.Cards[target.Id].StraightenHolds[0];
        Check(hold.Until == StraightenPoint.StraightenPhaseEnds && hold.Owner is null, "until after the next Straighten Phase ends, whoever's turn it is");
        Check(!CanUse(e, 0, "wlpt-daito-gold", daito.Id), "bowing him or paying: one use");

        // The 4 Gold way: Daito stays straightened, 4 Gold is paid (the sources bow).
        var t = Table(Phase.Action, 0);
        var d2 = Add(t, Daito, 0, Zone.Home);
        var victim = Add(t, "training-lion-samurai", 1, Zone.Home, bowed: true);
        Richer(t);
        var ge = MatchEngine.FromTestState(t);
        var quote = ge.PaymentOptions(0, 4);
        Check(quote.Length > 0, "4 Gold can be paid");
        var bowedBefore = t.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(ge, new GameCommand("useAbility", 0, ge.Version, CardId: d2.Id, AbilityId: "wlpt-daito-gold", TargetId: victim.Id, SourceIds: quote.First().SourceIds));
        Check(!ge.State.Cards[d2.Id].Bowed, "Daito is not bowed when 4 Gold is paid");
        Check(ge.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed) > bowedBefore, "the Gold sources were bowed");
        Eq(ge.State.Cards[victim.Id].StraightenHolds.Count, 1, "the straightening is held");
        // Without the Gold, or with a bowed Daito, nothing happens.
        var n = Table(Phase.Action, 0);
        var d3 = Add(n, Daito, 0, Zone.Home, bowed: true);
        var v3 = Add(n, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, "wlpt-daito-bow", d3.Id), "a bowed Daito cannot bow himself");
        Rejected(ne, new GameCommand("useAbility", 0, ne.Version, CardId: d3.Id, AbilityId: "wlpt-daito-gold", TargetId: v3.Id, SourceIds: []));

        // The held target stays bowed through the next Straighten Phase, and straightens in the one after.
        var run = Table(Phase.Action, 0);
        var d4 = Add(run, Daito, 0, Zone.Home);
        var v4 = Add(run, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var free = Add(run, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var re = MatchEngine.FromTestState(run);
        Accept(re, Offer(re, 0, "wlpt-daito-bow", d4.Id, v4.Id));
        Settle(re);
        Batch336Support.RunUntil(re, () => re.State.ActivePlayerId == 1 && re.State.Phase == Phase.Action);
        Check(!re.State.Cards[free.Id].Bowed, "an unheld card straightened in that Straighten Phase");
        Check(re.State.Cards[v4.Id].Bowed, "the held one did not");
        Eq(re.State.Cards[v4.Id].StraightenHolds.Count, 0, "the hold is released once that Straighten Phase has ended");
    }

    // ---- Tansen --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TansenChoices()
    {
        var s = Table(Phase.Battle, 0);
        var tansen = Add(s, Tansen, 0, Zone.Battlefield, province: 0);
        var three = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var four = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "wlpt-tansen-ranged", tansen.Id) && CanUse(e, 0, "wlpt-tansen-province", tansen.Id), "both halves are offered");
        Accept(e, Use(e, 0, "wlpt-tansen-ranged", tansen.Id, four.Id));
        Settle(e);
        Eq(e.State.Cards[four.Id].Zone, Zone.Battlefield, "Ranged 3 does not reach Force 4");
        Check(!CanUse(e, 0, "wlpt-tansen-province", tansen.Id) && !CanUse(e, 0, "wlpt-tansen-ranged", tansen.Id), "the two halves share one use");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Use(e2, 0, "wlpt-tansen-ranged", tansen.Id, three.Id));
        Settle(e2);
        Eq(e2.State.Cards[three.Id].Zone, Zone.Dead, "Ranged 3 destroys Force 3");

        // The other half: the current battlefield's province gets -3 strength (until the turn ends).
        var p = Table(Phase.Battle, 0);
        var t2 = Add(p, Tansen, 0, Zone.Battlefield, province: 0);
        Add(p, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var pe = MatchEngine.FromTestState(p);
        var strength = pe.ProvinceStrengthForTest(1, 0);
        Accept(pe, Use(pe, 0, "wlpt-tansen-province", t2.Id));
        Settle(pe);
        Eq(pe.ProvinceStrengthForTest(1, 0), strength - 3, "-3 strength");
        Eq(pe.ProvinceStrengthForTest(1, 1), pe.ProvinceStrengthForTest(1, 1), "(only that province)");
        Eq(Batch336Support.StrengthNextTurn(pe, 1, 0), strength, "and it ends with the turn (R-001)");
        Check(!CanUse(pe, 0, "wlpt-tansen-ranged", t2.Id), "one use");
        var absent = Table(Phase.Battle, 0);
        var home = Add(absent, Tansen, 0, Zone.Home);
        Add(absent, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(absent, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(absent), 0, "wlpt-tansen-province", home.Id), "a Battle action needs him at the battlefield");
    }

    // ---- every new command validates -------------------------------------------------------------------------------------------------------------------------------------------

    private static void OfferedCommandsValidate()
    {
        MatchEngine Fresh()
        {
            var s = Table(Phase.Battle, 0);
            Add(s, Akahiko, 0, Zone.Battlefield, province: 0);
            Add(s, Nanae, 0, Zone.Battlefield, province: 0);
            Add(s, Izuna, 0, Zone.Battlefield, province: 0);
            Add(s, Ibara, 0, Zone.Battlefield, province: 0);
            Add(s, Riku, 0, Zone.Battlefield, province: 0);
            Add(s, Tansen, 0, Zone.Battlefield, province: 0);
            Add(s, Fubuko, 0, Zone.Battlefield, province: 0);
            Add(s, FallingLeaf, 0, Zone.FateDiscard);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Attach(s, "training-follower", enemy);
            Add(s, Kyoshiro, 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        MatchEngine FreshAction()
        {
            var s = Table(Phase.Action, 0);
            Add(s, Nanae, 0, Zone.Home); Add(s, Izuna, 0, Zone.Home); Add(s, Suzukaze, 0, Zone.Home); Add(s, Iza, 0, Zone.Home); Add(s, Tetsaka, 0, Zone.Home);
            Add(s, "training-shugenja", 0, Zone.Home); Add(s, Makiko, 0, Zone.Home); Add(s, Shinoko, 0, Zone.Home); Add(s, Daito, 0, Zone.Home);
            Add(s, "training-farm", 0, Zone.Home);
            Add(s, Kyoshiro, 1, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); Richer(s);
            return MatchEngine.FromTestState(s);
        }
        foreach (var fresh in new Func<MatchEngine>[] { Fresh, FreshAction })
        {
            var source = fresh();
            var options = source.QueryActions(0).Where(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is { } id && id.StartsWith("wlpt-", StringComparison.Ordinal)).ToArray();
            Check(options.Length >= 6, $"the position offers the new actions ({options.Length})");
            foreach (var option in options)
            {
                Accept(fresh(), option.Command!);
                if (option.Command!.CardIds is null && option.Command.Selections is null)
                    foreach (var target in option.TargetIds ?? []) Accept(fresh(), option.Command! with { TargetId = target });
            }
        }
    }
}
