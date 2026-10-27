using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Personality pass (issue #477), batch 6: personality-wave-73 -- Kitsuki Yataku, Nishimura, Genmyo, Qalyar, Seppun Washi, Yasuki Jekku, Yasuki Tanimura, Yung, Daigotsu Ishibashi
// and Daigotsu Kanpeki - exp, driven through QueryActions and the commands it offers.
internal static class EmperorPersonalityBatch6Tests
{
    private const string Yataku = "oracle-l5r-4446", Nishimura = "oracle-l5r-5595", Genmyo = "oracle-l5r-2793", Qalyar = "oracle-l5r-6117", Washi = "oracle-l5r-6620",
        Jekku = "oracle-l5r-9443", Tanimura = "oracle-l5r-9464", Yung = "oracle-l5r-9697", Ishibashi = "oracle-l5r-1728", Kanpeki = "oracle-l5r-1733",
        Shigeyuki = "oracle-l5r-2152", RingOfVoid = "oracle-l5r-6338", RingOfAir = "oracle-l5r-6333", Revenge = "oracle-l5r-6302", CastAside = "oracle-l5r-1225", AtAnyCost = "oracle-l5r-639",
        Weapon = "oracle-l5r-10124", Kiho = "oracle-l5r-2342";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("emperor-personality-batch6", [
        ("Published shape: the ten Personalities of personality-wave-73 print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in the wave manifest", PublishedShape),
        ("Kitsuki Yataku: after an action targets a Samurai, bow him and the Samurai's controller may not refuse the challenge -- not for a non-Samurai, an unrefusable action or a bowed Yataku", YatakuNoRefusal),
        ("Nishimura: bows an enemy card without attachments; an optional Kiho or Weapon from the hand is discarded to draw a card", NishimuraBowAndDraw),
        ("Genmyo: copies one keyword of another Personality of his controller's until the turn ends", GenmyoCopy),
        ("Qalyar: after engaging at his battlefield, Reconnaissance there and its province gets +2 or -2 strength, once", QalyarRecon),
        ("Seppun Washi: an alternate Gold source of 2 that bows him, once a turn", WashiGold),
        ("Yasuki Jekku: Political Limited, pays base Personal Honor minus 1, dishonors and costs the controller 1 or 2 Honor", JekkuDishonor),
        ("Yasuki Tanimura: 3 Gold that only pays for an action, without bowing him, once a turn", TanimuraGold),
        ("Yasuki Tanimura: after another player's Battle action targets him the player pays 4 Gold or loses 3 Honor -- in the before-action window and the attack window, not for anything else", TanimuraPolitical),
        ("Yung: after one of his controller's Rings enters play, bow him to search the Fate deck for a Ring", YungSearch),
        ("Daigotsu Ishibashi: shows a Dark Virtue card in hand for a Ranged Attack of Focus Value plus one", IshibashiAttack),
        ("Daigotsu Kanpeki - exp: an additional Battle action after a Strategy's Battle action he performed; destroys an enemy Personality with lower Force", KanpekiActions),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Stats(string id, string name, string clan, int force, int chi, int honor, int cost, int? requirement, string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name.StartsWith(name) && c.Type == CardType.Personality && c.Clan == clan && c.Force == force && c.Chi == chi && c.PersonalHonor == honor && c.GoldCost == cost && c.HonorRequirement == requirement && c.Effect == "none",
                $"{c.Name} ({id}): {c.Clan} force={c.Force} chi={c.Chi} honor={c.PersonalHonor} cost={c.GoldCost} req={c.HonorRequirement} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(!c.ScenarioOnly && !string.IsNullOrEmpty(c.Text), $"{c.Name} is a playable card with its printed text");
        }
        Stats(Yataku, "Kitsuki Yataku", "Dragon", 1, 4, 4, 7, 10, ["yataku-no-refuse"]);
        Stats(Nishimura, "Nishimura", "Spider", 4, 2, 0, 7, null, ["nishimura-bow"]);
        Stats(Genmyo, "Genmyo", "Unaligned", 3, 2, 1, 4, null, ["genmyo-copy"]);
        Stats(Qalyar, "Qalyar", "Unaligned", 2, 3, 2, 3, null, ["qalyar-recon-plus", "qalyar-recon-minus"]);
        Stats(Washi, "Seppun Washi", "Unaligned", 1, 3, 2, 6, null, ["washi-produce-gold"]);
        Stats(Jekku, "Yasuki Jekku", "Crab", 2, 4, 0, 7, null, ["jekku-dishonor"]);
        Stats(Tanimura, "Yasuki Tanimura", "Crab", 2, 4, 1, 8, null, ["tanimura-produce-gold", "tanimura-political-pay"]);
        Stats(Yung, "Yung", "Unaligned", 0, 3, 1, 6, 5, ["yung-search-ring"]);
        Stats(Ishibashi, "Daigotsu Ishibashi", "Spider", 4, 3, 0, 7, null, ["ishibashi-attack"]);
        Stats(Kanpeki, "Daigotsu Kanpeki", "Spider", 9, 5, 0, 15, null, ["kanpeki-additional-action", "kanpeki-destroy"]);
        Check(CardCatalog.Get(Yataku).Keywords!.Contains("Samurai") && CardCatalog.Get(Qalyar).Keywords!.Contains("Scout") && CardCatalog.Get(Kanpeki).Keywords!.Contains("Experienced"), "printed keywords");
        Eq(PublishedContent.PersonalityWave73.Cards.Length, 10, "the wave publishes exactly its ten wired cards");
        Check(PublishedContent.PersonalityWave73.Cards.All(c => (c.Abilities ?? []).Length > 0), "no card of the wave is blank");
        Manifest(root, "personality-wave-73", Yataku, Nishimura, Genmyo, Qalyar, Washi, Jekku, Tanimura, Yung, Ishibashi, Kanpeki);
        // The shape of each ability as printed.
        Eq(AbilityCatalog.All["yataku-no-refuse"].Trigger, "before-action-resolves"); Check(AbilityCatalog.All["yataku-no-refuse"].ActionKeywords is ["Political"]);
        Check(AbilityCatalog.All["qalyar-recon-plus"].ActionKeywords is ["Recon"] && AbilityCatalog.All["qalyar-recon-minus"].UsageKey == "qalyar-recon" && AbilityCatalog.All["qalyar-recon-plus"].UsageKey == "qalyar-recon");
        Eq(AbilityCatalog.All["washi-produce-gold"].PaymentProduction, 2);
        Eq(AbilityCatalog.All["tanimura-produce-gold"].PaymentProduction, 3); Check(AbilityCatalog.All["tanimura-produce-gold"].PaymentActionsOnly && !AbilityCatalog.All["tanimura-produce-gold"].PaymentBows);
        Check(AbilityCatalog.All["jekku-dishonor"].ActionKeywords is ["Political"] && AbilityCatalog.All["jekku-dishonor"].Timing == "Limited");
        Check(AbilityCatalog.All["ishibashi-attack"].ActionKeywords is ["Fear"]);
    }

    // ---- Kitsuki Yataku ----

    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "afterActionResolvedReaction" }; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static MatchEngine Challenge(bool withYataku, string targetDefinition, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var shig = Add(s, Shigeyuki, 0, Zone.Battlefield, province: 0);
        var foe = Add(s, targetDefinition, 1, Zone.Battlefield, province: 0);
        foe.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get(targetDefinition).PersonalHonor));
        if (withYataku) Add(s, Yataku, 1, Zone.Battlefield, province: 0);
        edit?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "shigeyuki-duel", shig.Id, foe.Id));
        return e;
    }

    private static void YatakuNoRefusal()
    {
        // Baseline: nobody holds a reaction, so the challenge is refusable and waits for the challenged player.
        var plain = Challenge(false, Washi);
        Check(plain.State.PendingDuel is { Refusable: true, Stage: "AwaitingAccept" }, "without Yataku the challenge is refusable");

        // With Yataku: the window opens for his controller, he bows, and the challenge cannot be refused.
        var e = Challenge(true, Washi);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the before-action window opens for Yataku's controller");
        var yataku = e.State.Cards.Values.Single(c => c.DefinitionId == Yataku);
        Accept(e, Enabled(e, 1, "yataku-no-refuse", yataku.Id).Command!);
        Check(e.State.Cards[yataku.Id].Bowed, "Yataku bowed as his cost");
        Settle(e);
        Check(e.State.PendingDuel is { Refusable: false }, "the challenge may not be refused");
        Check(e.State.PendingDuel!.Stage != "AwaitingAccept", "and no refusal is offered: the duel goes straight on");
        Check(!e.QueryActions(1).Any(a => a.Type == "declineDuel" && a.Enabled), "the Samurai's controller has no refuse action");

        // Declining the reaction leaves the challenge refusable.
        var declined = Challenge(true, Washi);
        Settle(declined);
        Check(declined.State.PendingDuel is { Refusable: true, Stage: "AwaitingAccept" }, "an unused Yataku changes nothing");

        // Not for a target that is no Samurai: no window opens at all.
        var notSamurai = Challenge(true, "training-lion-samurai");
        Check(notSamurai.State.PendingChoice?.Type != "beforeActionReaction" && notSamurai.State.PendingDuel is { Refusable: true }, "no Samurai targeted, nothing to react to");

        // Not while bowed.
        var bowed = Challenge(true, Washi, s => s.Cards.Values.Single(c => c.DefinitionId == Yataku).Bowed = true);
        Check(bowed.State.PendingChoice?.Type != "beforeActionReaction" && bowed.State.PendingDuel is { Refusable: true }, "a bowed Yataku cannot react");

        // Not against an action that creates no refusable challenge: an ordinary bow on a Samurai opens no window for him.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var samurai = Add(s, Washi, 1, Zone.Battlefield, province: 0);
        Add(s, Yataku, 1, Zone.Battlefield, province: 0);
        var other = MatchEngine.FromTestState(s);
        Accept(other, Offer(other, 0, "training-bow-at-battle", bower.Id, samurai.Id));
        Check(other.State.PendingChoice?.Type != "beforeActionReaction", "Yataku only answers an action that creates a challenge");
    }

    // ---- Nishimura ----

    private static void NishimuraBowAndDraw()
    {
        MatchEngine Setup(out CardInstance nishimura, out CardInstance foe, out CardInstance weapon, out CardInstance kiho, out CardInstance plain)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            nishimura = Add(s, Nishimura, 0, Zone.Battlefield, province: 0);
            foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            weapon = Add(s, Weapon, 0, Zone.Hand); kiho = Add(s, Kiho, 0, Zone.Hand); plain = Add(s, "training-crane-samurai", 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // Discarding a Weapon draws a card.
        var e = Setup(out var n, out var foe1, out var weapon, out var kiho, out var plain);
        var offer = Enabled(e, 0, "nishimura-bow", n.Id);
        Check(offer.Slots!.Single(x => x.Slot == "discard").CardIds.ToHashSet().SetEquals([weapon.Id, kiho.Id]), "the optional slot lists exactly the Kiho and the Weapon in hand");
        Check(offer.Slots!.Single(x => x.Slot == "target").CardIds.Contains(foe1.Id), "the enemy card is a legal target");
        var deck = e.State.Players[0].FateDeck.Count;
        Accept(e, Use(e, 0, "nishimura-bow", n.Id, null, null, Pick("target", foe1.Id), Pick("discard", weapon.Id)));
        Check(e.State.Cards[foe1.Id].Bowed, "the target is bowed");
        Eq(e.State.Cards[weapon.Id].Zone, Zone.FateDiscard, "the Weapon was discarded");
        Eq(e.State.Players[0].Hand.Count, 3, "two cards left in hand and one drawn");
        Eq(e.State.Players[0].FateDeck.Count, deck - 1, "the card came from the Fate deck");

        // Without a discard: only the bow.
        var e2 = Setup(out var n2, out var foe2, out _, out _, out _);
        Accept(e2, Use(e2, 0, "nishimura-bow", n2.Id, null, null, Pick("target", foe2.Id)));
        Check(e2.State.Cards[foe2.Id].Bowed && e2.State.Players[0].Hand.Count == 3, "bowed, nothing discarded, nothing drawn");

        // A card that is neither a Kiho nor a Weapon cannot be the discard.
        var e3 = Setup(out var n3, out var foe3, out _, out _, out var plain3);
        Check(Rejection(e3, Use(e3, 0, "nishimura-bow", n3.Id, null, null, Pick("target", foe3.Id), Pick("discard", plain3.Id))).Length > 0, "a plain card is refused");

        // A target with attachments is illegal.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var n4 = Add(s, Nishimura, 0, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var e4 = MatchEngine.FromTestState(s);
        var targets4 = Options(e4, 0, "nishimura-bow", n4.Id).Single().Slots!.Single(x => x.Slot == "target").CardIds;
        Check(!targets4.Contains(armed.Id), "an enemy Personality with attachments cannot be targeted");
        Check(targets4.Length == 1, "(its Follower, which has none, can be)");
        Check(plain.Id.Length > 0);
    }

    // ---- Genmyo ----

    private static void GenmyoCopy()
    {
        var s = Table(Phase.Action, 0);
        var genmyo = Add(s, Genmyo, 0, Zone.Home);
        var donor = Add(s, Washi, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "genmyo-copy", genmyo.Id).Any(o => o.TargetIds?.Contains(genmyo.Id) == true), "he cannot copy from himself");
        Accept(e, Offer(e, 0, "genmyo-copy", genmyo.Id, donor.Id));
        Eq(e.State.PendingChoice?.Type, "copyKeyword", "several keywords: the player chooses one");
        var offered = e.QueryActions(0).Where(a => a.Type == "chooseCopyKeyword").Select(a => a.Command!.Choice).ToHashSet();
        Check(offered.SetEquals(["Banker", "Imperial", "Imperial Bank of Rokugan", "Samurai", "Soul of Seppun Ujifusa"]), "every keyword of the donor: " + string.Join(",", offered));
        Accept(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: "Samurai"));
        Check(e.State.PendingChoice is null, "the copy is finished");
        Check(e.State.Cards[genmyo.Id].GrantedKeywords.Any(g => g.Keyword == "Samurai") && !e.State.Cards[genmyo.Id].GrantedKeywords.Any(g => g.Keyword == "Banker"), "exactly one keyword was copied");
        Eq(e.State.Cards[genmyo.Id].GrantedKeywords.Single().Duration, GrantDuration.Turn, "an ongoing effect with no stated duration lasts until the turn ends");
        // Once a turn.
        Check(!CanUse(e, 0, "genmyo-copy", genmyo.Id), "used once this turn");
    }

    // ---- Qalyar ----

    private static MatchEngine Engaging(Action<MatchState>? edit = null, string qalyarDefinition = Qalyar, int qalyarProvince = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, qalyarDefinition, 0, Zone.Battlefield, province: qalyarProvince);
        edit?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        return e;
    }

    private static void QalyarRecon()
    {
        foreach (var (ability, delta) in new[] { ("qalyar-recon-plus", 2), ("qalyar-recon-minus", -2) })
        {
            var e = Engaging();
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
            var qalyar = e.State.Cards.Values.Single(c => c.DefinitionId == Qalyar);
            var before = e.GetView(0).Players[1].Provinces[0].Strength;
            Accept(e, Use(e, 0, ability, qalyar.Id));
            Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Qalyar's controller has Reconnaissance at the battlefield");
            Eq(e.GetView(0).Players[1].Provinces[0].Strength, before + delta, $"the battlefield's province has {delta:+#;-#} strength");
            // One printed ability: the other amount is spent too.
            var other = ability == "qalyar-recon-plus" ? "qalyar-recon-minus" : "qalyar-recon-plus";
            Check(!CanUse(e, 0, other, qalyar.Id) && !CanUse(e, 0, ability, qalyar.Id), "one use per turn for the whole printed ability");
        }
        // The effect on the province ends with the turn: it is a timed effect.
        var t = Engaging();
        var q = t.State.Cards.Values.Single(c => c.DefinitionId == Qalyar);
        Accept(t, Use(t, 0, "qalyar-recon-plus", q.Id));
        Check(t.State.ProvinceStrengthEffects.Any(x => x.Amount == 2 && x.Turn == t.State.TurnNumber), "recorded as a this-turn effect");

        // Qalyar must stand at the battlefield being engaged: at home she is not offered the Reaction.
        var away = Engaging(s => s.Cards.Values.Single(c => c.DefinitionId == Qalyar).Zone = Zone.Home);
        Check(!Options(away, 0, "qalyar-recon-plus").Any(o => o.Enabled), "a Qalyar at home cannot react to the engagement");
    }

    // ---- Seppun Washi / Yasuki Tanimura / Yasuki Jekku ----

    private static void BowAllGold(MatchState s, int owner)
    {
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
    }

    private static CardDefinition PersonalityWithHonor(int honor) =>
        CardCatalog.Cards.Values.Where(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.PersonalHonor == honor && c.Effect == "none" && (c.Abilities ?? []).Length == 0)
            .OrderBy(c => c.Id, StringComparer.Ordinal).First();

    private static void WashiGold()
    {
        var s = Table(Phase.Action, 0);
        BowAllGold(s, 0);
        var washi = Add(s, Washi, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(washi.Id, 0), 2, "Washi produces 2 Gold");
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == washi.Id).GoldProduced, 2, "and says so in the view");
        Check(e.PaymentOptions(0, 2).Any(o => o.SourceIds.SequenceEqual([washi.Id])), "a 2 Gold payment offers Washi");
        // Paying with him bows him and spends the Reaction for the turn.
        var target = PersonalityWithHonor(3);
        var s2 = Table(Phase.Action, 0);
        BowAllGold(s2, 0);
        var washi2 = Add(s2, Washi, 0, Zone.Home);
        var jekku = Add(s2, Jekku, 0, Zone.Home);
        var victim = Add(s2, target.Id, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var offer = Enabled(e2, 0, "jekku-dishonor", jekku.Id);
        Accept(e2, Offer(e2, 0, "jekku-dishonor", jekku.Id, victim.Id) with { SourceIds = [washi2.Id] });
        Check(e2.State.Cards[washi2.Id].Bowed, "Washi bowed to produce the Gold");
        Eq(e2.State.Cards[washi2.Id].AbilityUses["washi-produce-gold"], e2.State.TurnNumber, "his Reaction is spent");
        Check(offer.Gold is not null, "the action was priced");
        // Bowed, he produces nothing.
        var s3 = Table(Phase.Action, 0);
        BowAllGold(s3, 0);
        var washi3 = Add(s3, Washi, 0, Zone.Home, bowed: true);
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.PaymentOptions(0, 2).All(o => !o.SourceIds.Contains(washi3.Id)), "a bowed Washi is no source");
    }

    private static void JekkuDishonor()
    {
        foreach (var honor in new[] { 3, 4, 2, 0 })
        {
            var target = PersonalityWithHonor(honor);
            var s = Table(Phase.Action, 0);
            var jekku = Add(s, Jekku, 0, Zone.Home);
            var victim = Add(s, target.Id, 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var expectedCost = Math.Max(0, honor - 1);
            var offer = Options(e, 0, "jekku-dishonor", jekku.Id).First(o => o.Enabled && (o.TargetIds?.Contains(victim.Id) ?? false));
            Eq(offer.TargetGold![victim.Id].Cost, expectedCost, $"a target with base Personal Honor {honor} costs {expectedCost} Gold");
            var honorBefore = e.State.Players[1].Honor;
            Accept(e, Offer(e, 0, "jekku-dishonor", jekku.Id, victim.Id) with { SourceIds = offer.TargetGold![victim.Id].Payments.FirstOrDefault()?.SourceIds ?? [] });
            Check(e.State.Cards[victim.Id].Dishonorable, $"the Personality (base Personal Honor {honor}) is dishonored");
            Eq(e.State.Players[1].Honor, honorBefore - (honor >= 3 ? 2 : 1), $"his controller loses {(honor >= 3 ? 2 : 1)} Honor");
        }
        // The base (printed) Personal Honor prices it, not the current one: a bonus changes nothing.
        var t = PersonalityWithHonor(2);
        var s2 = Table(Phase.Action, 0);
        var j2 = Add(s2, Jekku, 0, Zone.Home);
        var v2 = Add(s2, t.Id, 1, Zone.Home);
        v2.Modifiers.Add(new("test-source", "test", "PersonalHonor", 5));
        var e2 = MatchEngine.FromTestState(s2);
        Eq(Options(e2, 0, "jekku-dishonor", j2.Id).First(o => o.Enabled).TargetGold![v2.Id].Cost, 1, "priced on the printed value");
        var before = e2.State.Players[1].Honor;
        Accept(e2, Offer(e2, 0, "jekku-dishonor", j2.Id, v2.Id) with { SourceIds = Options(e2, 0, "jekku-dishonor", j2.Id).First(o => o.Enabled).TargetGold![v2.Id].Payments.First().SourceIds });
        Eq(e2.State.Players[1].Honor, before - 1, "and the loss reads the printed value too (2 is below 3)");
        // Limited: usable in the Action phase on his controller's turn, once, and not in a battle.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var j3 = Add(s3, Jekku, 0, Zone.Battlefield, province: 0);
        Add(s3, PersonalityWithHonor(2).Id, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "jekku-dishonor", j3.Id), "a Limited action is not a Battle action");
    }

    private static void TanimuraGold()
    {
        var target = PersonalityWithHonor(4); // costs 3 Gold: exactly Tanimura's production
        var s = Table(Phase.Action, 0);
        BowAllGold(s, 0);
        var tanimura = Add(s, Tanimura, 0, Zone.Home);
        var jekku = Add(s, Jekku, 0, Zone.Home);
        var victim = Add(s, target.Id, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var jekkuCard = e.State.Cards[jekku.Id];
        var actionSubject = new PaymentSubject(jekkuCard, PaymentKind.Action);
        Eq(e.GoldProduced(tanimura.Id, 0, actionSubject), 3, "paying for an action he produces 3 Gold");
        Check(e.PaymentOptions(0, 3, actionSubject).Any(o => o.SourceIds.SequenceEqual([tanimura.Id])), "the payment for an action offers him");
        Check(e.PaymentOptions(0, 3, new PaymentSubject(jekkuCard, PaymentKind.Recruit)).All(o => !o.SourceIds.Contains(tanimura.Id)), "a recruit is not an action: he is no source for it");
        Check(e.PaymentOptions(0, 3).All(o => !o.SourceIds.Contains(tanimura.Id)), "a payment that is for nothing in particular does not offer him");
        Accept(e, Offer(e, 0, "jekku-dishonor", jekku.Id, victim.Id) with { SourceIds = [tanimura.Id] });
        Check(!e.State.Cards[tanimura.Id].Bowed, "producing Gold does not bow him: his printed text has no bow cost");
        Eq(e.State.Cards[tanimura.Id].AbilityUses["tanimura-produce-gold"], e.State.TurnNumber, "but the Reaction is used up for the turn");
        Check(e.State.Cards[victim.Id].Dishonorable, "the action was paid for and resolved");
        // Used once: he is no source again this turn.
        Check(e.PaymentOptions(0, 3, actionSubject).All(o => !o.SourceIds.Contains(tanimura.Id)), "a second payment this turn cannot use him");
        // Bowed (by some other effect), he is no source at all.
        var s2 = Table(Phase.Action, 0);
        BowAllGold(s2, 0);
        var t2 = Add(s2, Tanimura, 0, Zone.Home, bowed: true);
        var j2 = Add(s2, Jekku, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.PaymentOptions(0, 3, new PaymentSubject(e2.State.Cards[j2.Id], PaymentKind.Action)).All(o => !o.SourceIds.Contains(t2.Id)), "a bowed Tanimura produces nothing");
    }

    // ---- Yasuki Tanimura's Political Reaction ----

    private static MatchEngine TargetedBy(string attackerAbilityHolder, Action<MatchState>? edit = null, int bowerOwner = 1)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var tanimura = Add(s, Tanimura, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, attackerAbilityHolder, 1, Zone.Battlefield, province: 0);
        edit?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        return e;
    }

    private static void TanimuraPolitical()
    {
        // The acting player (1) takes a Battle action targeting Tanimura; the window opens for his controller (0), who reacts; player 1 pays 4 Gold.
        MatchEngine Start(Action<MatchState>? edit = null)
        {
            var e = TargetedBy("training-battle-bower", edit);
            var bower = e.State.Cards.Values.Single(c => c.DefinitionId == "training-battle-bower");
            var tanimura = e.State.Cards.Values.Single(c => c.DefinitionId == Tanimura);
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, tanimura.Id));
            return e;
        }
        var e = Start();
        var tan = e.State.Cards.Values.Single(c => c.DefinitionId == Tanimura);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for Tanimura's controller");
        Accept(e, Enabled(e, 0, "tanimura-political-pay", tan.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the acting player chooses");
        Eq(e.State.PendingChoice!.PlayerId, 1, "the player who took the action decides");
        var options = e.QueryActions(1).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(options.Length == 2 && options.All(o => o.Enabled), "pay 4 Gold or lose 3 Honor, both possible: " + string.Join(";", options.Select(o => o.Label + o.Enabled)));
        var honor = e.State.Players[1].Honor;
        var pay = options.First(o => o.Command!.Choice == "pay");
        Accept(e, pay.Command!);
        Eq(e.State.Players[1].Honor, honor, "paying the Gold costs no Honor");
        Settle(e);
        Check(e.State.Cards[tan.Id].Bowed, "and the action went on (his bow, as the action's effect)");

        // Not paying: he loses 3 Honor.
        var e2 = Start();
        var tan2 = e2.State.Cards.Values.Single(c => c.DefinitionId == Tanimura);
        Accept(e2, Enabled(e2, 0, "tanimura-political-pay", tan2.Id).Command!);
        var honor2 = e2.State.Players[1].Honor;
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "lose").Command!);
        Eq(e2.State.Players[1].Honor, honor2 - 3, "he lost 3 Honor");

        // A player who cannot pay 4 Gold has nothing to decide: he loses the Honor.
        var e3 = Start(s => BowAllGold(s, 1));
        var tan3 = e3.State.Cards.Values.Single(c => c.DefinitionId == Tanimura);
        var honor3 = e3.State.Players[1].Honor;
        Accept(e3, Enabled(e3, 0, "tanimura-political-pay", tan3.Id).Command!);
        Check(e3.State.PendingChoice?.Type != "eitherOr", "no decision when only one option is possible");
        Eq(e3.State.Players[1].Honor, honor3 - 3, "the Honor is lost automatically");

        // Only once a turn.
        Check(!CanUse(e2, 0, "tanimura-political-pay", tan2.Id), "used once");

        // The attack window too: a Ranged Attack (a Battle action) that names Tanimura. Player 1 (Ishibashi, showing Cast Aside the Weak: strength 5) attacks him; his controller reacts.
        var s6 = Table(Phase.Battle, 1); s6.PriorityPlayerId = 1;
        var tan6 = Add(s6, Tanimura, 0, Zone.Battlefield, province: 0);
        var ishibashi6 = Add(s6, Ishibashi, 1, Zone.Battlefield, province: 0);
        var virtue6 = Add(s6, CastAside, 1, Zone.Hand);
        var e6 = MatchEngine.FromTestState(s6);
        Accept(e6, Options(e6, 1, "ishibashi-attack", ishibashi6.Id).Single().Command! with { TargetId = tan6.Id });
        Eq(e6.State.PendingChoice?.Type, "attackReaction", "the attack window opens");
        Accept(e6, Enabled(e6, 0, "tanimura-political-pay", tan6.Id).Command!);
        Eq(e6.State.PendingChoice?.Type, "eitherOr", "the attacking player chooses");
        Eq(e6.State.PendingChoice!.PlayerId, 1, "he decides");
        var honor6 = e6.State.Players[1].Honor;
        Accept(e6, e6.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "lose").Command!);
        Eq(e6.State.Players[1].Honor, honor6 - 3, "he lost 3 Honor");
        Eq(e6.State.PendingChoice?.Type, "attackReaction", "the attack window is open again");
        Settle(e6);
        Check(e6.State.PendingChoice is null && e6.State.Cards[tan6.Id].Zone == Zone.Dead, "and the Attack (strength 5 against Force 2) then resolved");
        Check(virtue6.Id.Length > 0);

        // Not for an action that is not a Battle action: Jekku's Political Limited action targeting him in the Action phase opens no window for him.
        var s4 = Table(Phase.Action, 1); s4.PriorityPlayerId = 1;
        var tan4 = Add(s4, Tanimura, 0, Zone.Home);
        var jekku4 = Add(s4, Jekku, 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "jekku-dishonor", jekku4.Id, tan4.Id));
        Check(e4.State.PendingChoice?.Type != "beforeActionReaction" && e4.State.Cards[tan4.Id].Dishonorable, "a Limited action opens no window for him");

        // Not for an action of his own controller's: player 0's own Battle action naming him.
        var s5 = Table(Phase.Battle, 0); s5.PriorityPlayerId = 0;
        var tan5 = Add(s5, Tanimura, 0, Zone.Battlefield, province: 0);
        var bower5 = Add(s5, "training-penalty-source", 0, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "training-penalty-force-own", bower5.Id, tan5.Id));
        Check(e5.State.PendingChoice?.Type != "beforeActionReaction", "his own controller's action gives him nothing to react to");
    }

    // ---- Yung ----

    private static void YungSearch()
    {
        MatchEngine Setup(Action<MatchState>? edit = null)
        {
            var s = Table(Phase.Action, 0);
            Add(s, Yung, 0, Zone.Home);
            Add(s, RingOfVoid, 0, Zone.Hand);
            // A Ring on top of the Fate deck, and the rest as the scenario had it.
            var ring = Add(s, RingOfAir, 0, Zone.FateDeck); s.Players[0].Hand.Remove(ring.Id); s.Players[0].FateDeck.Insert(0, ring.Id);
            edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Setup();
        var yung = e.State.Cards.Values.Single(c => c.DefinitionId == Yung);
        var voidRing = e.State.Cards.Values.Single(c => c.DefinitionId == RingOfVoid);
        Check(!CanUse(e, 0, "yung-search-ring", yung.Id), "no Ring has entered play yet");
        Accept(e, Use(e, 0, "ring-of-void-recruit", voidRing.Id));
        Eq(e.State.Cards[voidRing.Id].Zone, Zone.Home, "the Ring entered play");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "Yung's window opens");
        Accept(e, Enabled(e, 0, "yung-search-ring", yung.Id).Command!);
        Check(e.State.Cards[yung.Id].Bowed, "Yung bowed as his cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search opens");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(entries.Any(x => x.DefinitionId == RingOfAir && x.Eligible) && entries.Where(x => x.Eligible).All(x => x.Definition.Type == CardType.Ring), "only Rings are eligible");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfAir));
        var inHand = e.State.Players[0].Hand.Select(id => e.State.Cards[id].DefinitionId).ToArray();
        Check(inHand.Contains(RingOfAir), "the Ring was put in the hand");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == RingOfAir), "and shown");
        Check(e.State.PendingSearch is null, "the search is over");
        Check(e.State.PendingChoice is null, "the after-action window Yung was played in is given back and, with nothing left to take in it, closes (issue #644)");

        // An action that puts no Ring into play gives Yung nothing to react to.
        var s2 = Table(Phase.Action, 0);
        var yung2 = Add(s2, Yung, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "yung-search-ring", yung2.Id), "nothing to react to");

        // Another player's Ring entering play is not "one of your Rings".
        var s3 = Table(Phase.Action, 1);
        Add(s3, Yung, 0, Zone.Home);
        var enemyRing = Add(s3, RingOfVoid, 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 1, "ring-of-void-recruit", enemyRing.Id));
        Check(e3.State.PendingChoice?.Type != "afterActionResolvedReaction" || !e3.QueryActions(0).Any(a => a.Command?.AbilityId == "yung-search-ring" && a.Enabled), "an enemy Ring gives Yung nothing");
    }

    // ---- Daigotsu Ishibashi ----

    private static CardInstance InHand(MatchEngine e, string definition) => e.State.Cards.Values.Single(c => c.DefinitionId == definition && c.Zone == Zone.Hand && c.OwnerId == 0);

    private static void IshibashiAttack()
    {
        MatchEngine Setup(string targetForceDefinition, Action<MatchState>? edit = null)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            Add(s, Ishibashi, 0, Zone.Battlefield, province: 0);
            Add(s, targetForceDefinition, 1, Zone.Battlefield, province: 0);
            Add(s, CastAside, 0, Zone.Hand); Add(s, AtAnyCost, 0, Zone.Hand); Add(s, Weapon, 0, Zone.Hand);
            edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        // Cast Aside the Weak has Focus Value 4: strength 5. A Force 4 target (training-lion-samurai) is legal and destroyed.
        var e = Setup("training-lion-samurai");
        var ishibashi = e.State.Cards.Values.Single(c => c.DefinitionId == Ishibashi);
        var foe = e.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-samurai" && c.Zone == Zone.Battlefield);
        var castAside = InHand(e, CastAside);
        var atAnyCost = InHand(e, AtAnyCost);
        var options = Options(e, 0, "ishibashi-attack", ishibashi.Id);
        Eq(options.Length, 2, "one offered action per Dark Virtue that may be shown (the Weapon is not one)");
        Check(options.All(o => o.Label.Contains("shows")), "each says which card it shows");
        Check(options.Any(o => o.Enabled && o.Command!.CardIds is [var a] && a == castAside.Id), "Cast Aside the Weak (Focus 4) is offered");
        var focusOne = options.Single(o => o.Command!.CardIds is [var a] && a == atAnyCost.Id);
        Check(focusOne.Enabled && (focusOne.TargetIds ?? []).Contains(foe.Id), "At Any Cost (Focus 1, strength 2) cannot destroy a Force 4 target but may still target it");
        var strong = options.Single(o => o.Command!.CardIds is [var a] && a == castAside.Id);
        Check(strong.TargetIds!.Contains(foe.Id), "strength 5 reaches a Force 4 target");
        var cardsInHand = e.State.Players[0].Hand.Count;
        Accept(e, strong.Command! with { TargetId = foe.Id });
        Check(e.State.ShownCards.Any(x => x.DefinitionId == CastAside), "the Dark Virtue was shown");
        Eq(e.State.Players[0].Hand.Count, cardsInHand, "showing it costs no card: it stays in the hand");
        Eq(e.State.Cards[castAside.Id].Zone, Zone.Hand);
        Check(e.State.PendingAttack is not null || e.State.Cards[foe.Id].Zone == Zone.Dead, "the Ranged Attack was made");
        Settle(e);
        Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "Force 4 against strength 5: destroyed");

        // A weaker card: strength 2 (Focus 1 + 1) may target a Force 4 one (a legal target) but fails to destroy it.
        var weak = Setup("training-lion-samurai");
        var wIshibashi = weak.State.Cards.Values.Single(c => c.DefinitionId == Ishibashi);
        var wFoe = weak.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-samurai" && c.Zone == Zone.Battlefield);
        var wAtAnyCost = InHand(weak, AtAnyCost);
        var weakTry = Setup("training-lion-samurai");
        var wtIshibashi = weakTry.State.Cards.Values.Single(c => c.DefinitionId == Ishibashi);
        var wtFoe = weakTry.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-samurai" && c.Zone == Zone.Battlefield);
        var wtAtAnyCost = InHand(weakTry, AtAnyCost);
        Accept(weakTry, Use(weakTry, 0, "ishibashi-attack", wtIshibashi.Id, wtFoe.Id) with { CardIds = [wtAtAnyCost.Id] });
        Check(weakTry.State.ShownCards.Any(x => x.DefinitionId == AtAnyCost), "the Dark Virtue was shown");
        Settle(weakTry);
        Eq(weakTry.State.Cards[wtFoe.Id].Zone, Zone.Battlefield, "the Attack's strength is Focus plus one, so Force 4 is too strong for Focus 1: it survives");

        // Something that is not a Dark Virtue cannot be shown; nor can nothing.
        var wWeapon = InHand(weak, Weapon);
        Check(Rejection(weak, Use(weak, 0, "ishibashi-attack", wIshibashi.Id, wFoe.Id) with { CardIds = [wWeapon.Id] }).Length > 0, "a Weapon is not a Dark Virtue");
        Check(Rejection(weak, Use(weak, 0, "ishibashi-attack", wIshibashi.Id, wFoe.Id)).Length > 0, "a card must be shown");

        // No Dark Virtue in hand: the ability is listed disabled.
        var none = Setup("training-lion-samurai", st => { foreach (var id in st.Players[0].Hand.ToArray()) { st.Cards.Remove(id); st.Players[0].Hand.Remove(id); } });
        var none1 = Options(none, 0, "ishibashi-attack").Single();
        Check(!none1.Enabled, "nothing to show, nothing to do");
    }

    // ---- Daigotsu Kanpeki - exp ----

    private static void KanpekiActions()
    {
        // Destroy: an enemy Personality with lower Force.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var kanpeki = Add(s, Kanpeki, 0, Zone.Battlefield, province: 0);
        var weaker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var stronger = Add(s, "training-force-seven-bower", 1, Zone.Battlefield, province: 0);
        stronger.Modifiers.Add(new("test-source", "test", "Force", 3)); // Force 10: not lower than Kanpeki's 9
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "kanpeki-destroy", kanpeki.Id).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(weaker.Id) && !targets.Contains(stronger.Id), "only an enemy Personality with lower Force than Kanpeki's 9: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "kanpeki-destroy", kanpeki.Id, weaker.Id));
        Eq(e.State.Cards[weaker.Id].Zone, Zone.Dead, "he is destroyed");
        // His Force is compared as it stands: with equal Force the target is not lower.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var k2 = Add(s2, Kanpeki, 0, Zone.Battlefield, province: 0);
        var same = Add(s2, "training-force-seven-bower", 1, Zone.Battlefield, province: 0);
        same.Modifiers.Add(new("test-source", "test", "Force", 2));
        var e2 = MatchEngine.FromTestState(s2);
        Check(!Options(e2, 0, "kanpeki-destroy", k2.Id).SelectMany(o => o.TargetIds ?? []).Contains(same.Id), "equal Force is not lower");

        // Additional action: after a Strategy's Battle action Kanpeki performed.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var k3 = Add(s3, Kanpeki, 0, Zone.Battlefield, province: 0);
        var other = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s3, "training-performer-strategy", 0, Zone.Hand);
        var strategy2 = Add(s3, "training-performer-strategy", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "training-performer-boost", strategy.Id, null, k3.Id));
        Eq(e3.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for Kanpeki");
        var react = e3.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "kanpeki-additional-action");
        Accept(e3, react.Command!);
        Check(e3.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");

        // Not when another Personality performed the Strategy's action.
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        Add(s4, Kanpeki, 0, Zone.Battlefield, province: 0);
        var o4 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var st4 = Add(s4, "training-performer-strategy", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 0, "training-performer-boost", st4.Id, null, o4.Id));
        Check(!e4.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "kanpeki-additional-action"), "another performer's Strategy action does not trigger Kanpeki");

        // Not when the action is his own printed Battle action, which is no Strategy's.
        var s5 = Table(Phase.Battle, 0); s5.PriorityPlayerId = 0;
        var k5 = Add(s5, Kanpeki, 0, Zone.Battlefield, province: 0);
        var v5 = Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "kanpeki-destroy", k5.Id, v5.Id));
        Check(!e5.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "kanpeki-additional-action"), "an action that is not from a Strategy does not trigger him");
        Check(other.Id.Length > 0 && strategy2.Id.Length > 0);
    }
}
