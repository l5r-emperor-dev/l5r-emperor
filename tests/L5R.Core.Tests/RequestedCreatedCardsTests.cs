using L5R.Core;
using static Support337;
using static RegressionAssertions;

internal static class RequestedCreatedCardsTests
{
    public static void Run(TestRunner runner) => runner.Run("requested-created-cards", [
        ("All requested creators and scans are published with art, pinned IDs, and resolvable creation specs", PublishedShape),
        ("Kakita Saki creates protective Armor on her target Personality", KakitaSakiArmor),
        ("Kitsune Kichi creates her Badger on entry, which reduces an enemy Personality's Force to zero", KitsuneKichiBadger),
        ("Natural Aviary pays 2 Gold to create and attach an Aviary Falcon", NaturalAviaryFalcon),
        ("Fruitless Search creates a temporary Emerald Magistrate after engaging an enemy", FruitlessSearch),
        ("Grateful Reward attaches Mounted Ashigaru and rewards a Paragon Samurai", GratefulReward),
        ("Our Ancestor's Call creates one or two clan-aligned Ancestor Spirits at an opposed battle", OurAncestorsCall),
        ("Yari of Air creates an Air Yari once per game, which weakens the enemy army", YariOfAir),
        ("Crippled Sensei reacts when an action destroys your Personality and creates Maimed Retainer", CrippledSenseiAction),
        ("Crippled Sensei also reacts to a Personality killed by battle resolution", CrippledSenseiBattle),
        ("Crippled Sensei's mandatory Focus Effect lets its owner choose which player loses 2 Honor", CrippledSenseiFocus),
        ("Sword for Hire creates an Assassin, runs an unrefusable duel, destroys the loser, and removes the Assassin", SwordForHire),
        ("Vanquisher's Blades creates Readied Blade only when it can legally attach to its wielder", VanquishersBlades),
        ("Maimed Retainer destroys itself to straighten and rehonor a Personality", MaimedRetainer),
        ("Air Yari weakens the enemy army and Kichi's Badger reduces an enemy Personality to zero Force", CreatedCardBattles),
    ]);

    private static void Check(bool ok, string message) => Expect(ok, message);

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["personality-wave-118"] = ["oracle-l5r-9814", "oracle-l5r-9829", "oracle-l5r-9996", "oracle-l5r-9995", "user-scan-kichis-badger", "user-scan-ancestor-spirit"],
            ["strategy-wave-103"] = ["oracle-l5r-2708", "oracle-l5r-2895", "oracle-l5r-7696", "oracle-l5r-9899"],
            ["item-wave-39"] = ["oracle-l5r-9883", "oracle-l5r-9749", "user-scan-kakita-armor", "user-scan-air-yari"],
            ["spell-wave-47"] = ["oracle-l5r-9886", "oracle-l5r-9894"],
            ["holding-wave-46"] = ["user-scan-maimed-retainer"],
            ["follower-wave-42"] = ["oracle-l5r-10010", "user-scan-aviary-falcon"],
            ["region-wave-20"] = ["oracle-l5r-9866"],
        };
        foreach (var (wave, ids) in expected)
        {
            var manifest = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
            Check(new HashSet<string>(manifest).SetEquals(ids), $"{wave} manifest");
            foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
        }
        foreach (var id in new[] { "oracle-l5r-9996", "oracle-l5r-10010", "oracle-l5r-9995", "oracle-l5r-9749", "user-scan-kichis-badger",
            "user-scan-ancestor-spirit", "user-scan-kakita-armor", "user-scan-air-yari", "user-scan-maimed-retainer", "user-scan-aviary-falcon" })
            Check(CardCatalog.Get(id).CreatedOnly, $"{id} is created-only");
        Check(CreatedCards.Verify().Length == 0, string.Join("; ", CreatedCards.Verify()));
        Eq(CreatedCards.Resolve(AbilityCatalog.All["natural-aviary-create"].Creates!).Id, "user-scan-aviary-falcon");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["our-ancestors-call-create"].Creates!).Id, "user-scan-ancestor-spirit");
    }

    private static void KakitaSakiArmor()
    {
        var s = Table(Phase.Action);
        var saki = Put(s, "oracle-l5r-9814", 0, Zone.Home);
        var host = Put(s, "training-crane-samurai", 0, Zone.Home);
        var e = Engine(s);
        Accept(e, Offer(e, 0, "kakita-saki-create-armor", saki.Id, host.Id));
        var armor = e.State.Cards.Values.Single(c => c.DefinitionId == "user-scan-kakita-armor");
        Eq(armor.Zone, Zone.Attached); Eq(armor.AttachedTo, host.Id);
        Eq(CardCatalog.Get(armor.DefinitionId).AttackDefenseAmount, -3);
    }

    private static void KitsuneKichiBadger()
    {
        var s = Table(Phase.Action);
        var kichi = Put(s, "oracle-l5r-9829", 0, Zone.Home);
        s.PendingChoice = new("entryReaction", 0, kichi.Id, "Kichi enters play");
        var e = Engine(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: kichi.Id, AbilityId: "kichi-create-badger"));
        var badger = e.State.Cards.Values.Single(c => c.DefinitionId == "user-scan-kichis-badger");
        Eq(badger.Zone, Zone.Home); Eq(badger.OwnerId, 0);
    }

    private static void NaturalAviaryFalcon()
    {
        var s = Table(Phase.Action);
        var aviary = Put(s, "oracle-l5r-9866", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = aviary.Id;
        var host = Put(s, "training-crane-samurai", 0, Zone.Home);
        Put(s, "training-marketplace", 0, Zone.Home);
        var e = Engine(s);
        var offered = Offers(e, 0, "natural-aviary-create").FirstOrDefault(a => a.TargetIds?.Contains(host.Id) == true && a.Enabled);
        Check(offered is not null, "Natural Aviary is available with a 2 Gold source and legal host");
        Accept(e, offered!.Command!);
        var falcon = e.State.Cards.Values.Single(c => c.DefinitionId == "user-scan-aviary-falcon");
        Eq(falcon.Zone, Zone.Attached); Eq(falcon.AttachedTo, host.Id);
    }

    private static void Engage(MatchEngine e, int player)
    {
        Accept(e, e.QueryActions(0).First(a => a.Type == "selectBattle" && a.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        while (e.State.PriorityPlayerId != player)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void FruitlessSearch()
    {
        var s = Table(Phase.BattleSelection);
        s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Put(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var search = Put(s, "oracle-l5r-2708", 0, Zone.Hand);
        var e = Engine(s);
        Engage(e, 0);
        Accept(e, Offer(e, 0, "fruitless-search-create", search.Id));
        var magistrate = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9996");
        Eq(magistrate.Zone, Zone.Battlefield); Eq(magistrate.ProvinceIndex, 0);
        Check(e.State.CreatedUntilBattleEndsIds.Contains(magistrate.Id), "removed after this battle");
    }

    private static void GratefulReward()
    {
        var s = Table(Phase.BattleSelection);
        s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var samurai = Put(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); // no "unbowed" printed: a bowed Samurai performs it (R-035, issue #1228)
        samurai.GrantedKeywords.Add(new("Samurai", "test-fixture", GrantDuration.Permanent));
        samurai.GrantedKeywords.Add(new("Paragon", "test-fixture", GrantDuration.Permanent));
        var away = Put(s, "training-crane-samurai", 0, Zone.Home);
        away.GrantedKeywords.Add(new("Samurai", "test-fixture", GrantDuration.Permanent));
        Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var reward = Put(s, "oracle-l5r-2895", 0, Zone.Hand);
        var honor = s.Players[0].Honor;
        var e = Engine(s);
        Engage(e, 0);
        Check(!Offers(e, 0, "grateful-reward-create").Any(a => a.Enabled && a.Command?.PerformerId == away.Id), "a Samurai at home is not at the current battlefield");
        var offer = Offers(e, 0, "grateful-reward-create").First(a => a.Enabled && a.Command?.PerformerId == samurai.Id);
        Accept(e, offer.Command!);
        var follower = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10010");
        Eq(follower.Zone, Zone.Attached); Eq(follower.AttachedTo, samurai.Id);
        Eq(e.State.Players[0].Honor, honor + 2);
    }

    private static void OurAncestorsCall()
    {
        var s = Table(Phase.Battle, actor: 1); s.PriorityPlayerId = 0;
        s.CurrentBattleProvince = 0;
        var shugenja = Put(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        shugenja.GrantedKeywords.Add(new("Water", "test-fixture", GrantDuration.Permanent));
        Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var call = Put(s, "oracle-l5r-9886", 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var e = Engine(s);
        Accept(e, Offer(e, 0, "our-ancestors-call-create", call.Id, performerId: shugenja.Id));
        var spirits = e.State.Cards.Values.Where(c => c.DefinitionId == "user-scan-ancestor-spirit").ToArray();
        Eq(spirits.Length, 2);
        foreach (var spirit in spirits) { Eq(spirit.Zone, Zone.Battlefield); Check(e.HasKeyword(spirit.Id, "Crane Clan"), "printed clan alignment"); }
    }

    private static void YariOfAir()
    {
        var s = Table(Phase.Action);
        var shugenja = Put(s, "training-shugenja", 0, Zone.Home);
        var spell = Put(s, "oracle-l5r-9894", 0, Zone.Attached, parent: shugenja.Id);
        var e = Engine(s);
        Accept(e, Offer(e, 0, "yari-of-air-create", spell.Id, shugenja.Id));
        var yari = e.State.Cards.Values.Single(c => c.DefinitionId == "user-scan-air-yari");
        Eq(yari.Zone, Zone.Attached); Eq(yari.AttachedTo, shugenja.Id);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: spell.Id, AbilityId: "yari-of-air-create", TargetId: shugenja.Id));
    }

    private static void CrippledSenseiAction()
    {
        var s = Table(Phase.Battle, actor: 1);
        var destroyer = Put(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var victim = Put(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sensei = Put(s, "oracle-l5r-9899", 0, Zone.Hand);
        var e = Engine(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction");
        var option = Offers(e, 0, "crippled-sensei-create-action").First(a => a.Enabled && a.Command?.CardId == sensei.Id);
        Accept(e, option.Command!);
        var retainer = e.State.Cards.Values.Single(c => c.DefinitionId == "user-scan-maimed-retainer");
        Eq(retainer.Zone, Zone.Home);
    }

    private static void CrippledSenseiBattle()
    {
        var s = Table(Phase.Battle, actor: 1);
        var attacker = Put(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        attacker.Modifiers.Add(new("test", "test", "Force", 10));
        var victim = Put(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sensei = Put(s, "oracle-l5r-9899", 0, Zone.Hand);
        var e = Engine(s);
        for (var i = 0; i < 40 && e.State.PendingChoice?.Type != "afterResolutionReaction"; i++)
        {
            var player = e.State.PriorityPlayerId;
            var pass = e.QueryActions(player).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, "battle can pass to resolution");
            Accept(e, pass!.Command!);
        }
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead);
        Eq(e.State.PendingChoice?.Type, "afterResolutionReaction");
        var option = Offers(e, 0, "crippled-sensei-create-battle").First(a => a.Enabled && a.Command?.CardId == sensei.Id);
        Accept(e, option.Command!);
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == "user-scan-maimed-retainer"), 1);
    }

    private static void CrippledSenseiFocus()
    {
        var s = Table(Phase.Battle, actor: 0); s.PriorityPlayerId = 0;
        var mine = Put(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var theirs = Put(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var duel = Put(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var sensei = Put(s, "oracle-l5r-9899", 0, Zone.Hand);
        var top = Put(s, "training-focus-strategy", 1, Zone.FateDeck);
        s.Players[1].FateDeck.Insert(0, top.Id);
        var honor = s.Players[1].Honor;
        var e = Engine(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: sensei.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        Eq(e.State.PendingChoice?.Type, "focusEffectChoice");
        var picks = e.QueryActions(0).Where(a => a.Type == "chooseFocusEffect").ToArray();
        Check(picks.Length == 2 && picks.All(a => a.Command?.Choice != "decline"), "either player, no decline");
        Accept(e, picks.Single(a => a.Command?.TargetId == "1").Command!);
        Eq(e.State.Players[1].Honor, honor - 2);
        Eq(e.State.Cards[sensei.Id].Zone, Zone.FateDiscard);
    }

    private static void SwordForHire()
    {
        var s = Table(Phase.Action);
        var target = Put(s, "training-lion-samurai", 1, Zone.Home);
        var sword = Put(s, "oracle-l5r-7696", 0, Zone.Hand);
        for (var i = 0; i < 5; i++) Put(s, "training-marketplace", 0, Zone.Home);
        var e = Engine(s);
        Accept(e, Offer(e, 0, "sword-for-hire-create", sword.Id, target.Id));
        var assassin = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9995");
        Check(e.State.PendingDuel is { Refusable: false } pending && pending.ChallengerId == assassin.Id && pending.ChallengedId == target.Id, "Assassin issues the unrefusable duel");
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 8; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version)
                : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead);
        Eq(e.State.Cards[assassin.Id].Zone, Zone.RemovedFromGame);
    }

    private static void VanquishersBlades()
    {
        var s = Table(Phase.Action);
        var host = Put(s, "training-crane-samurai", 0, Zone.Home);
        host.GrantedKeywords.Add(new("Kensai", "test-fixture", GrantDuration.Permanent));
        var blades = Put(s, "oracle-l5r-9883", 0, Zone.Attached, parent: host.Id);
        s.PendingChoice = new("entryReaction", 0, blades.Id, "Vanquisher's Blades entered play");
        var e = Engine(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: blades.Id, AbilityId: "vanquishers-blades-create"));
        var readied = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9749");
        Eq(readied.Zone, Zone.Attached); Eq(readied.AttachedTo, host.Id);
    }

    private static void MaimedRetainer()
    {
        var s = Table(Phase.Action);
        var retainer = Put(s, "user-scan-maimed-retainer", 0, Zone.Home);
        var target = Put(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        target.Dishonorable = true;
        var e = Engine(s);
        Accept(e, Offer(e, 0, "maimed-retainer-restore", retainer.Id, target.Id));
        Eq(e.State.Cards[retainer.Id].Zone, Zone.RemovedFromGame);
        Check(!e.State.Cards[target.Id].Bowed && !e.State.Cards[target.Id].Dishonorable, "target straightened and rehonored");
    }

    private static void CreatedCardBattles()
    {
        var s = Table(Phase.Battle);
        var host = Put(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var yari = Put(s, "user-scan-air-yari", 0, Zone.Attached, province: 0, parent: host.Id);
        var badger = Put(s, "user-scan-kichis-badger", 0, Zone.Battlefield, province: 0);
        var enemy = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = Engine(s);
        var original = e.CardForceForTest(enemy.Id);
        Accept(e, Offer(e, 0, "air-yari-penalty", yari.Id));
        Eq(e.CardForceForTest(enemy.Id), original - 1);
        if (e.State.PriorityPlayerId == 1)
            Accept(e, e.QueryActions(1).First(a => a.Type == "pass" && a.Enabled).Command!);
        Accept(e, Offer(e, 0, "kichis-badger-reduce", badger.Id, enemy.Id));
        Eq(e.CardForceForTest(enemy.Id), 0);
    }
}
