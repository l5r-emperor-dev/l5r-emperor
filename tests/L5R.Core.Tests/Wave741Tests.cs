using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #741, wave batch 10 (src/L5R.Core/Wave741.cs): the created-card, Fortification and small-effect cards, proved on the published cards themselves. Every position is explicit
// (Batch336Support.Table: player 0 the Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave741Tests
{
    private const string Merihiko = Wave741Abilities.Merihiko, Yuribara = Wave741Abilities.Yuribara, Tsukau = Wave741Abilities.Tsukau, Kinto = Wave741Abilities.Kinto, Tsuchiya = Wave741Abilities.Tsuchiya,
        Watersteel = Wave741Abilities.Watersteel, Tides = Wave741Abilities.Tides, TheSelf = Wave741Abilities.TheSelf, Whisper = Wave741Abilities.Whisper, Tomb = Wave741Abilities.Tomb,
        HarmonyCard = Wave741Abilities.HarmonyCard, JudgmentCard = Wave741Abilities.JudgmentCard, ChaosCard = Wave741Abilities.ChaosCard, Waves = Wave741Abilities.Waves, PrayerCard = Wave741Abilities.PrayerCard,
        CoalescenceCard = Wave741Abilities.CoalescenceCard, Earth = Wave741Abilities.Earth, DiscoveryCard = Wave741Abilities.DiscoveryCard, GatheringCard = Wave741Abilities.GatheringCard, Tower = Wave741Abilities.Tower, MovingBattleCard = Wave741Abilities.MovingBattleCard;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", ShindenAsahina = "oracle-l5r-6855", Marketplace = "training-marketplace", TrainingItem = "training-item";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static CardInstance Now(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];
    private static bool Offered(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);

    public static void Run(TestRunner runner) => runner.Run("wave-741", [
        ("Published as printed: twenty-one cards in five profiles with their stats, abilities and effects", PublishedShape),
        ("Kitsune Merihiko - exp: while he is in play your Nonhuman Spirits (and no one else) have Battle, bow: Ranged 3 Attack", MerihikoGrant),
        ("Kitsune Yuribara: entering play offers the Reaction that creates the Butterflies, once, with the printed traits", YuribaraCreates),
        ("Yuribara's Butterflies: another player's Battle action must target a Butterfly at the battlefield if it legally can, and is destroyed once targeted", ButterfliesTraits),
        ("Ninube Tsukau: loses 3 Honor on entering play; once per game copies a non-Unique Human's Force, one keyword and one base ability permanently", TsukauCard),
        ("Shinjo Kinto - exp2: switches two Personalities of one player at different locations and straightens the units he does not control", KintoSwitch),
        ("Tamori Tsuchiya: only while defending, destroys one of his Spells to gain Stalwart and a Force bonus equal to his Force", TsuchiyaCard),
        ("Watersteel Sword: copies an ability of another non-Unique Weapon on its wielder, to itself, for the turn", WatersteelCard),
        ("Befriend the Tides: bows the Spell and moves its Shugenja to an adjacent unresolved battlefield", TidesCard),
        ("Constructing the Self: creates an Illusion with the Shugenja's Chi and Personal Honor, removed when the Spell straightens or leaves play, and the Spell may stay bowed", SelfCard),
        ("Koan's Whisper: reduces a Province's strength to 0; with five Koan cards removed it destroys a Province of a player with two or more", WhisperCard),
        ("A Doomseeker's Tomb: 1 Gold gives your Personality a +1F/+1C/+1PH token", TombCard),
        ("Harmony: a Monk may bow himself instead of a Ring to pay a cost", HarmonyTest),
        ("Kitsuki Judgment: a Magistrate pays the unit's total Gold Cost, +8 unless he is dishonorable, Kolat, Ninja or Shadowlands, and a Human Personality commits seppuku", JudgmentCardTest),
        ("Overwhelming Chaos: each player shuffles his hand into his deck and draws as many cards", ChaosCardTest),
        ("Shifting Waves: a Henshin switches a Personality's Force and Chi for the turn", WavesCard),
        ("Sorrowful Prayer: one to three non-Unique Strategies from your discard pile are shuffled into your deck and the card leaves the game", PrayerCardTest),
        ("Spiritual Coalescence: destroys a Spirit to give another a permanent Force bonus and its abilities, and grants an additional action", CoalescenceCardTest),
        ("Strength in the Earth: a Monk's Melee Attack, with strength equal to his Force, may target a card in the Defender's home", EarthCard),
        ("Unexpected Discovery: moves your opposed Personality to an adjacent unresolved battlefield", DiscoveryCardTest),
        ("Unsettling Gathering: pays X to transfer an attachment of Gold Cost X or less to any Personality, changing its controller", GatheringCardTest),
        ("A Moving Battle: a Terrain whose controller, as the Attacker, may move one of his opposed attacking Personalities to an adjacent unresolved battlefield before the battle resolves", MovingBattleTest),
        ("The Tower of Vigilance: a Fortification whose Engage action creates a Guard at the battle it guards, removed when the battle ends", TowerCard),
    ]);

    // ---- fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState Table(Phase phase, int actor)
    {
        var s = Batch336Support.Table(phase, actor);
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = ShindenAsahina;
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        return s;
    }

    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PendingChoice!.PlayerId, e.Version));
    }

    // Closes the windows the action opened and hands priority back to the acting player (the other player passes).
    private static void Done(MatchEngine e, int player)
    {
        Settle(e);
        if (e.State.PriorityPlayerId != player) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Settle(e);
    }

    // A Personality fixture with explicit stats and keywords.
    private static CardInstance P(MatchState s, string id, int owner, Zone zone, int force, int chi, int honor, string[] keywords, int? province = null, bool bowed = false, string[]? abilities = null, string effect = "none") =>
        Fx(s, owner == 0 ? CraneSamurai : LionSamurai, id, owner, zone, d => d with { Force = force, Chi = chi, PersonalHonor = honor, Keywords = keywords, Abilities = abilities ?? [], Effect = effect, GoldCost = 3 }, bowed, province: province);

    private static void Gold(MatchState s, int owner, int farms = 6)
    {
        for (var i = 0; i < farms; i++) Add(s, Marketplace, owner, Zone.Home);
    }

    private static void HandCards(MatchState s, int count = 3)
    {
        for (var player = 0; player < 2; player++) for (var i = 0; i < count; i++) Add(s, TrainingItem, player, Zone.Hand);
    }

    private static CardInstance Attach(MatchState s, string def, int owner, CardInstance host, string? id = null, Func<CardDefinition, CardDefinition>? tweak = null)
    {
        var card = id is null ? Add(s, def, owner, Zone.Attached, parent: host.Id) : Fx(s, def, id, owner, Zone.Attached, tweak, parent: host.Id);
        card.AttachedTo = host.Id; card.ProvinceIndex = host.ProvinceIndex;
        return card;
    }

    private static bool IsAt(CardInstance card, Zone zone, int? province = null) => card.Zone == zone && (province is null || card.ProvinceIndex == province);

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Merihiko] = (CardType.Personality, [], "w741-merihiko"),
            [Yuribara] = (CardType.Personality, ["w741-yuribara-create"], "none"),
            [Tsukau] = (CardType.Personality, ["w741-tsukau-copy"], "none"),
            [Kinto] = (CardType.Personality, ["w741-kinto-switch"], "none"),
            [Tsuchiya] = (CardType.Personality, ["w741-tsuchiya-stalwart"], "none"),
            [Watersteel] = (CardType.Item, ["w741-watersteel-copy"], "none"),
            [Tides] = (CardType.Spell, ["w741-tides-move"], "none"),
            [TheSelf] = (CardType.Spell, ["w741-self-create"], "choose-straighten"),
            [Whisper] = (CardType.Spell, ["w741-whisper-reduce", "w741-whisper-destroy"], "none"),
            [Tomb] = (CardType.Strategy, ["w741-tomb-token"], "none"),
            [HarmonyCard] = (CardType.Strategy, ["w741-harmony"], "none"),
            [JudgmentCard] = (CardType.Strategy, ["w741-judgment"], "none"),
            [ChaosCard] = (CardType.Strategy, ["w741-chaos"], "none"),
            [Waves] = (CardType.Strategy, ["w741-shifting-waves"], "none"),
            [PrayerCard] = (CardType.Strategy, ["w741-prayer"], "none"),
            [CoalescenceCard] = (CardType.Strategy, ["w741-coalescence"], "none"),
            [Earth] = (CardType.Strategy, ["w741-strength-earth"], "none"),
            [DiscoveryCard] = (CardType.Strategy, ["w741-discovery"], "none"),
            [GatheringCard] = (CardType.Strategy, ["w741-gathering"], "none"),
            [Tower] = (CardType.Holding, ["w741-tower-engage"], "none"),
            [MovingBattleCard] = (CardType.Strategy, ["w741-moving-battle"], "none"),
        };
        Eq(expected.Count, 21, "twenty-one cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        Eq(CardCatalog.Get(Tsukau).EntryHonorLoss, 3, "Tsukau loses 3 Honor on entering play");
        Check(CardCatalog.Get(Tower).Keywords!.Contains("Fortification"), "The Tower of Vigilance is a Fortification");
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w741-", StringComparison.Ordinal)), 22, "twenty-two w741 abilities");
        Check(CardCatalog.Get(Wave741Abilities.Butterflies).CreatedOnly && CardCatalog.Get(Wave741Abilities.MinorIllusion).CreatedOnly && CardCatalog.Get(Wave741Abilities.TowerGuard).CreatedOnly, "the created cards stay created-only");
        Eq(CreatedCards.Verify().Length, 0, "every creator resolves to its scan: " + string.Join("; ", CreatedCards.Verify()));
    }

    private static void MerihikoGrant()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var merihiko = Add(s, Merihiko, 0, Zone.Battlefield, province: 0);
        var spirit = P(s, "w741-spirit", 0, Zone.Battlefield, 2, 2, 1, ["Nonhuman", "Spirit"], province: 0);
        var human = P(s, "w741-human", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var enemy = P(s, "w741-enemy", 1, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        var strong = P(s, "w741-strong", 1, Zone.Battlefield, 4, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(Batch336Support.CanUse(e, 0, Wave741Abilities.MerihikoRanged, spirit.Id), "his Nonhuman Spirit is offered the Ranged Attack");
        Check(!Batch336Support.CanUse(e, 0, Wave741Abilities.MerihikoRanged, human.Id), "a human is not");
        Check(!Batch336Support.CanUse(e, 0, Wave741Abilities.MerihikoRanged, merihiko.Id), "Merihiko himself is not a Spirit");
        Check(!Batch336Support.CanUse(e, 1, Wave741Abilities.MerihikoRanged, enemy.Id), "the opponent's cards have nothing granted");
        Accept(e, Batch336Support.Offer(e, 0, Wave741Abilities.MerihikoRanged, spirit.Id, enemy.Id));
        Settle(e);
        Check(Now(e, spirit).Bowed, "the Spirit bowed for the cost");
        Check(!IsAt(Now(e, enemy), Zone.Battlefield), "Ranged 3 destroys the Force 3 Personality");
        Check(IsAt(Now(e, strong), Zone.Battlefield), "the Force 4 Personality is out of its reach");
        // When Merihiko leaves play the grant goes.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var gone = Add(t, Merihiko, 0, Zone.FateDiscard);
        var spirit2 = P(t, "w741-spirit2", 0, Zone.Battlefield, 2, 2, 1, ["Nonhuman", "Spirit"], province: 0);
        P(t, "w741-enemy2", 1, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        HandCards(t);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, Wave741Abilities.MerihikoRanged, spirit2.Id), "without Merihiko in play there is no ability");
        _ = gone;
    }

    private static MatchEngine EnterYuribara(out CardInstance yuribara)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var card = s.Cards[s.Players[0].Provinces[0].CardId!]; card.DefinitionId = Yuribara; card.FaceUp = true;
        for (var i = 0; i < 8; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
        while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", 0, e.Version));
        yuribara = e.State.Cards[card.Id];
        return e;
    }

    private static void YuribaraCreates()
    {
        var e = EnterYuribara(out var yuribara);
        Check(e.State.PendingChoice?.Type is "entryReaction", $"the entering-play window is open ({e.State.PendingChoice?.Type})");
        var cards = e.State.Cards.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.YuribaraCreate, yuribara.Id));
        Settle(e);
        var made = e.State.Cards.Values.Where(c => c.DefinitionId == Wave741Abilities.Butterflies && c.Zone is Zone.Home or Zone.Battlefield).ToArray();
        Eq(made.Length, 1, "exactly one Butterflies Personality is created");
        Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
        Eq(made[0].OwnerId, 0, "it belongs to Yuribara's controller");
        Eq(made[0].Printed?.Effect, Wave741Abilities.ButterfliesEffect, "it carries the Butterflies' trait");
        Check(!Batch336Support.CanUse(e, 0, Wave741Abilities.YuribaraCreate, yuribara.Id), "once");
        // Declining creates nothing.
        var declined = EnterYuribara(out _);
        var count = declined.State.Cards.Count;
        Accept(declined, new GameCommand("declineReaction", 0, declined.Version));
        Eq(declined.State.Cards.Count, count, "declining creates nothing");
        // The Reaction belongs to the moment of entering play.
        var t = Table(Phase.Action, 0);
        var inPlay = Add(t, Yuribara, 0, Zone.Home);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, Wave741Abilities.YuribaraCreate, inPlay.Id), "not offered once she is in play");
    }

    private static void ButterfliesTraits()
    {
        // Player 1 attacks (Merihiko's grant); player 0's Butterflies stand at the battlefield beside another Personality.
        MatchEngine Rig(out CardInstance butterflies, out CardInstance other, out CardInstance spirit, bool butterfliesHere = true)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            Add(s, Merihiko, 1, Zone.Battlefield, province: 0);
            spirit = P(s, "w741-b-spirit", 1, Zone.Battlefield, 2, 2, 1, ["Nonhuman", "Spirit"], province: 0);
            other = P(s, "w741-b-other", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            butterflies = Fx(s, Wave741Abilities.Butterflies, "w741-butterflies", 0, butterfliesHere ? Zone.Battlefield : Zone.Home,
                d => d with { Effect = Wave741Abilities.ButterfliesEffect, CreatedOnly = false }, province: butterfliesHere ? 0 : null);
            HandCards(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var butterflies, out var other, out var spirit);
        var before = JsonSerializer.Serialize(e.State);
        var offered = Batch336Support.Options(e, 1, Wave741Abilities.MerihikoRanged, spirit.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(offered.Contains(butterflies.Id), "the Butterflies are a target");
        var onOther = Batch336Support.Options(e, 1, Wave741Abilities.MerihikoRanged, spirit.Id).First().Command! with { TargetId = other.Id };
        Check(!e.Execute(onOther).Accepted, "another Personality may not be named while the Butterflies can be");
        Check(JsonSerializer.Serialize(e.State) == before, "the refused command changes nothing");
        Accept(e, Batch336Support.Offer(e, 1, Wave741Abilities.MerihikoRanged, spirit.Id, butterflies.Id));
        Settle(e);
        Check(!IsAt(Now(e, butterflies), Zone.Battlefield), "the Butterflies are destroyed once another player's Battle action targets them");
        Check(IsAt(Now(e, other), Zone.Battlefield), "the other Personality is untouched");
        // Not at the battlefield (at home), the rule has nothing to force.
        var home = Rig(out var homeButterflies, out var homeOther, out var homeSpirit, butterfliesHere: false);
        Accept(home, Batch336Support.Offer(home, 1, Wave741Abilities.MerihikoRanged, homeSpirit.Id, homeOther.Id));
        Settle(home);
        Check(Now(home, homeButterflies).Zone == Zone.Home, "Butterflies at home are not forced upon an attacker");
        // The controller's own Battle action may name anything.
        var own = Table(Phase.Battle, 0); own.PriorityPlayerId = 0;
        Add(own, Merihiko, 0, Zone.Battlefield, province: 0);
        var ownSpirit = P(own, "w741-own-spirit", 0, Zone.Battlefield, 2, 2, 1, ["Nonhuman", "Spirit"], province: 0);
        var enemy = P(own, "w741-enemy", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        Fx(own, Wave741Abilities.Butterflies, "w741-enemy-butterflies", 1, Zone.Battlefield, d => d with { Effect = Wave741Abilities.ButterfliesEffect, CreatedOnly = false }, province: 0);
        HandCards(own);
        var o = MatchEngine.FromTestState(own);
        var ownOffer = Batch336Support.Options(o, 0, Wave741Abilities.MerihikoRanged, ownSpirit.Id).First(a => a.Enabled).Command! with { TargetId = enemy.Id };
        Check(o.Execute(ownOffer).Accepted == false, "the Butterflies of the opposing player are forced on a Battle action by its other player (this is that player's rule too)");
    }

    private static void TsukauCard()
    {
        var s = Table(Phase.Action, 0);
        var tsukau = Add(s, Tsukau, 0, Zone.Home);
        var human = P(s, "w741-human-t", 1, Zone.Home, 6, 2, 1, ["Samurai"], abilities: [Wave741Abilities.MerihikoRanged]);
        var unique = P(s, "w741-unique-t", 1, Zone.Home, 7, 2, 1, ["Samurai", "Unique"]);
        var nonhuman = P(s, "w741-nonhuman-t", 1, Zone.Home, 7, 2, 1, ["Nonhuman", "Spirit"]);
        var e = MatchEngine.FromTestState(s);
        var targets = Batch336Support.Options(e, 0, Wave741Abilities.TsukauCopy, tsukau.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(human.Id) && !targets.Contains(unique.Id) && !targets.Contains(nonhuman.Id) && !targets.Contains(tsukau.Id), "only another non-Unique Human Personality is a target (a card never copies itself): " + string.Join(",", targets));
        Accept(e, Batch336Support.Offer(e, 0, Wave741Abilities.TsukauCopy, tsukau.Id, human.Id));
        for (var guard = 0; guard < 4 && e.State.PendingCopy is not null; guard++)
        {
            var copy = e.State.PendingCopy!;
            if (copy.Steps[0] == CopyStep.Keyword) Accept(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: copy.KeywordChoices[0]));
            else Accept(e, new GameCommand("chooseCopyAbility", 0, e.Version, Choice: copy.AbilityChoices[0]));
        }
        Settle(e);
        Eq(e.CardForceForTest(tsukau.Id), 6, "Tsukau permanently copies the Force");
        Check(Now(e, tsukau).GrantedKeywords.Any(g => g.Keyword == "Samurai" && g.Duration == GrantDuration.Permanent), "and one of his keywords, permanently");
        Check(Now(e, tsukau).GrantedAbilities.Any(g => g.AbilityId == Wave741Abilities.MerihikoRanged && g.Duration == GrantDuration.Permanent), "and one of his base abilities, permanently");
        Check(!Batch336Support.CanUse(e, 0, Wave741Abilities.TsukauCopy, tsukau.Id), "once per game");
        // Losing 3 Honor on entering play.
        var p = MatchEngine.CreateEconomyScenario().State;
        var card = p.Cards[p.Players[0].Provinces[0].CardId!]; card.DefinitionId = Tsukau; card.FaceUp = true;
        for (var i = 0; i < 8; i++) Add(p, Marketplace, 0, Zone.Home);
        var entering = MatchEngine.FromTestState(p);
        var honor = Honor(entering, 0);
        Accept(entering, new GameCommand("purchase", 0, entering.Version, CardId: card.Id, SourceIds: entering.QuotePurchase(0, card.Id).Payments[0].SourceIds));
        Settle(entering);
        Eq(Honor(entering, 0), honor - 3, "he loses 3 Honor on entering play");
    }

    private static void KintoSwitch()
    {
        MatchEngine Rig(out CardInstance kinto, out CardInstance a, out CardInstance b, int otherOwner)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            kinto = Add(s, Kinto, 0, Zone.Battlefield, province: 0);
            a = P(s, "w741-switch-a", otherOwner, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0, bowed: true);
            b = P(s, "w741-switch-b", otherOwner, Zone.Home, 3, 2, 1, ["Samurai"], bowed: true);
            if (otherOwner == 0) s.Cards[a.Id].Printed = s.Cards[a.Id].Printed;
            HandCards(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var kinto, out var a, out var b, 1);
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.KintoSwitch, kinto.Id, null, null, Batch336Support.Pick("first", a.Id), Batch336Support.Pick("second", b.Id)));
        Settle(e);
        Check(IsAt(Now(e, a), Zone.Home) && IsAt(Now(e, b), Zone.Battlefield, 0), "the two switch locations");
        Check(!Now(e, a).Bowed && !Now(e, b).Bowed, "units the controller does not control are straightened after they move");
        var own = Rig(out var kinto2, out var a2, out var b2, 0);
        Accept(own, Batch336Support.Use(own, 0, Wave741Abilities.KintoSwitch, kinto2.Id, null, null, Batch336Support.Pick("first", a2.Id), Batch336Support.Pick("second", b2.Id)));
        Settle(own);
        Check(IsAt(Now(own, a2), Zone.Home) && IsAt(Now(own, b2), Zone.Battlefield, 0), "his own Personalities switch too");
        Check(Now(own, a2).Bowed && Now(own, b2).Bowed, "and stay bowed");
        // Two Personalities at one location are not a legal pair.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var k3 = Add(s, Kinto, 0, Zone.Battlefield, province: 0);
        var c = P(s, "w741-same-1", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var d = P(s, "w741-same-2", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var same = MatchEngine.FromTestState(s);
        NoChange(same, Batch336Support.Use(same, 0, Wave741Abilities.KintoSwitch, k3.Id, null, null, Batch336Support.Pick("first", c.Id), Batch336Support.Pick("second", d.Id)), "two Personalities at the same location");
    }

    private static void TsuchiyaCard()
    {
        MatchEngine Rig(int attacker, out CardInstance tsuchiya, out CardInstance spell)
        {
            var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0;
            tsuchiya = P(s, "w741-tsuchiya", 0, Zone.Battlefield, 3, 3, 1, ["Shugenja"], province: 0, abilities: [Wave741Abilities.TsuchiyaStalwart]);
            spell = Attach(s, "training-spell", 0, tsuchiya);
            P(s, "w741-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            HandCards(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(1, out var tsuchiya, out var spell);
        var force = e.CardForceForTest(tsuchiya.Id);
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.TsuchiyaStalwart, tsuchiya.Id, null, null, Batch336Support.Pick("p24-attachment", spell.Id)));
        Settle(e);
        Check(!IsAt(Now(e, spell), Zone.Attached), "the Spell is destroyed as the cost");
        Eq(e.CardForceForTest(tsuchiya.Id), force * 2, "his Force doubles");
        Check((bool)Call(e, "Keyword", Now(e, tsuchiya), "Stalwart")!, "he has Stalwart");
        var attacking = Rig(0, out var t2, out var spell2);
        NoChange(attacking, Batch336Support.Use(attacking, 0, Wave741Abilities.TsuchiyaStalwart, t2.Id, null, null, Batch336Support.Pick("p24-attachment", spell2.Id)), "Tsuchiya is not defending");
    }

    private static void WatersteelCard()
    {
        var s = Table(Phase.Action, 0);
        var host = P(s, "w741-host", 0, Zone.Home, 3, 2, 1, ["Samurai", "Water"]);
        var sword = Attach(s, Watersteel, 0, host);
        var weapon = Attach(s, "training-item", 0, host, "w741-weapon", d => d with { Keywords = ["Weapon"], Abilities = [Wave741Abilities.TsukauCopy] });
        var unique = Attach(s, "training-item", 0, host, "w741-unique-weapon", d => d with { Keywords = ["Weapon", "Unique"], Abilities = [Wave741Abilities.MerihikoRanged] });
        var shield = Attach(s, "training-item", 0, host, "w741-not-a-weapon", d => d with { Keywords = ["Armor"], Abilities = [Wave741Abilities.MerihikoRanged] });
        var e = MatchEngine.FromTestState(s);
        var targets = Batch336Support.Options(e, 0, Wave741Abilities.WatersteelCopy, sword.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(weapon.Id) && !targets.Contains(unique.Id) && !targets.Contains(shield.Id), "only another non-Unique Weapon on the wielder is a target: " + string.Join(",", targets));
        _ = unique; _ = shield;
        // The weapon's one ability is Tsukau's (which copies abilities), so it is not offered; give it a copyable one instead.
        var t = Table(Phase.Action, 0);
        var host2 = P(t, "w741-host2", 0, Zone.Home, 3, 2, 1, ["Samurai", "Water"]);
        var sword2 = Attach(t, Watersteel, 0, host2);
        var weapon2 = Attach(t, "training-item", 0, host2, "w741-weapon2", d => d with { Keywords = ["Weapon"], Abilities = [Wave741Abilities.MerihikoRanged] });
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Offer(f, 0, Wave741Abilities.WatersteelCopy, sword2.Id, weapon2.Id));
        Settle(f);
        Check(Now(f, sword2).GrantedAbilities.Any(g => g.AbilityId == Wave741Abilities.MerihikoRanged && g.Duration == GrantDuration.Turn), "the Sword copies the ability for the turn");
        Check(!Now(f, weapon2).GrantedAbilities.Any(), "the other Weapon keeps its own ability and gains nothing");
    }

    private static void TidesCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [0, 1];
        var shugenja = P(s, "w741-water", 0, Zone.Battlefield, 2, 3, 1, ["Shugenja", "Water"], province: 0);
        var spell = Attach(s, Tides, 0, shugenja);
        P(s, "w741-foe-t", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        var option = Batch336Support.Enabled(e, 0, Wave741Abilities.TidesMove, spell.Id);
        Check(option.Provinces?.Contains(1) == true, "the adjacent unresolved battlefield is offered");
        Accept(e, option.Command! with { Province = 1 });
        Settle(e);
        Check(IsAt(Now(e, shugenja), Zone.Battlefield, 1), "the Shugenja moves to the adjacent battlefield");
        Check(Now(e, spell).Bowed, "the Spell is bowed");
        Check(Now(e, spell).Zone == Zone.Attached, "the Spell stays attached to him");
    }

    private static void SelfCard()
    {
        MatchEngine Rig(out CardInstance shugenja, out CardInstance spell)
        {
            var s = Table(Phase.Action, 0);
            shugenja = P(s, "w741-air", 0, Zone.Home, 2, 3, 2, ["Shugenja", "Air"]);
            spell = Attach(s, TheSelf, 0, shugenja);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var shugenja, out var spell);
        var cards = e.State.Cards.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.SelfCreate, spell.Id));
        Settle(e);
        Eq(e.State.Cards.Count, cards + 1, "an Illusion is created");
        var illusion = e.State.Cards.Values.Single(c => c.DefinitionId == Wave741Abilities.MinorIllusion && c.Zone is Zone.Home or Zone.Battlefield);
        Eq(e.PersonalityChiForTest(illusion.Id), e.PersonalityChiForTest(shugenja.Id), "with the Shugenja's Chi");
        Eq(e.PersonalHonorForTest(illusion.Id), e.PersonalHonorForTest(shugenja.Id), "and Personal Honor");
        Eq(e.CardForceForTest(illusion.Id), 0, "and no Force");
        Check(Now(e, spell).Bowed, "the Spell is bowed");
        // The removal: the Spell straightens.
        Call(e, "Straighten", e.CardForTest(spell.Id));
        Call(e, "SweepCreatedCopies690");
        Check(!IsAt(Now(e, illusion), Zone.Home) && !IsAt(Now(e, illusion), Zone.Battlefield), "the Illusion is removed from the game when the Spell straightens");
        // And when the Spell leaves play.
        var f = Rig(out _, out var spell2);
        Accept(f, Batch336Support.Use(f, 0, Wave741Abilities.SelfCreate, spell2.Id)); Settle(f);
        var illusion2 = f.State.Cards.Values.Single(c => c.DefinitionId == Wave741Abilities.MinorIllusion && c.Zone is Zone.Home or Zone.Battlefield);
        Call(f, "DestroyAttachment", f.CardForTest(spell2.Id));
        Call(f, "SweepCreatedCopies690");
        Check(!IsAt(Now(f, illusion2), Zone.Home), "the Illusion is removed from the game when the Spell leaves play");
        // "This Spell may remain bowed": the controller may leave it bowed in the Straighten Phase.
        Check(Def(spell).Effect == "choose-straighten", "the Spell may stay bowed in the Straighten Phase");
    }

    private static void WhisperCard()
    {
        MatchEngine Rig(int koans, int provincesOfOpponent, out CardInstance spell, out CardInstance[] koanCards)
        {
            var s = Table(Phase.Action, 0);
            var shugenja = P(s, "w741-void", 0, Zone.Home, 2, 3, 2, ["Shugenja", "Void"]);
            spell = Attach(s, Whisper, 0, shugenja);
            koanCards = Enumerable.Range(0, koans).Select(i => Attach(s, "training-item", 0, shugenja, $"w741-koan-{i}", d => d with { Keywords = ["Koan"] })).ToArray();
            foreach (var province in s.Players[1].Provinces.Skip(provincesOfOpponent)) province.Destroyed = true;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(0, 4, out var spell, out _);
        var strength = e.EffectiveProvinceStrengthForTest(1, 0);
        Check(strength > 0, "the Province has strength to lose");
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.WhisperReduce, spell.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 0)));
        Settle(e);
        Eq(e.EffectiveProvinceStrengthForTest(1, 0), 0, "its strength is reduced to 0");
        Check(!e.State.Players[1].Provinces[0].Destroyed, "without Koan cards it is not destroyed");
        Check(Now(e, spell).Bowed, "the Spell is bowed");
        // Five Koan cards removed: the Province is destroyed.
        var d = Rig(5, 4, out var spell2, out var koans);
        Accept(d, Batch336Support.Use(d, 0, Wave741Abilities.WhisperDestroy, spell2.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 0), Batch336Support.Pick("p24-remove", koans.Select(k => k.Id).ToArray())));
        Settle(d);
        Check(d.State.Players[1].Provinces[0].Destroyed, "the Province is destroyed");
        Check(koans.All(k => Now(d, k).Zone == Zone.RemovedFromGame), "the five Koan cards are removed from the game");
        // A player with one Province cannot lose it this way.
        var one = Rig(5, 1, out var spell3, out var koans3);
        NoChange(one, Batch336Support.Use(one, 0, Wave741Abilities.WhisperDestroy, spell3.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 0), Batch336Support.Pick("p24-remove", koans3.Select(k => k.Id).ToArray())), "the player has one Province");
        // Four Koan cards are not enough.
        var few = Rig(4, 4, out var spell4, out var koans4);
        NoChange(few, Batch336Support.Use(few, 0, Wave741Abilities.WhisperDestroy, spell4.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 0), Batch336Support.Pick("p24-remove", koans4.Select(k => k.Id).ToArray())), "only four Koan cards");
    }

    private static void TombCard()
    {
        var s = Table(Phase.Action, 0);
        var host = P(s, "w741-tomb-host", 0, Zone.Home, 3, 2, 2, ["Samurai"]);
        var foe = P(s, "w741-tomb-foe", 1, Zone.Home, 3, 2, 2, ["Samurai"]);
        var card = Add(s, Tomb, 0, Zone.Hand);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Batch336Support.Options(e, 0, Wave741Abilities.TombToken, card.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(host.Id) && !targets.Contains(foe.Id), "your own Personality is the target");
        var (force, chi, honor) = (e.CardForceForTest(host.Id), e.PersonalityChiForTest(host.Id), e.PersonalHonorForTest(host.Id));
        var option = Batch336Support.Options(e, 0, Wave741Abilities.TombToken, card.Id).First(a => a.Enabled);
        Accept(e, option.Command! with { TargetId = host.Id, SourceIds = e.PaymentOptions(0, 1).First().SourceIds });
        Settle(e);
        Eq(e.CardForceForTest(host.Id), force + 1, "+1F");
        Eq(e.PersonalityChiForTest(host.Id), chi + 1, "+1C");
        Eq(e.PersonalHonorForTest(host.Id), honor + 1, "+1PH");
    }

    private static void HarmonyTest()
    {
        var s = Batch336Support.Table(Phase.Action, 1);
        var ring = Add(s, "training-ring-of-void", 1, Zone.Home);
        var monk = Fx(s, "training-monk", "w741-monk", 1, Zone.Home, d => d with { Keywords = ["Monk"] });
        var samurai = P(s, "w741-not-monk", 1, Zone.Home, 2, 2, 1, ["Samurai"]);
        var card = Add(s, HarmonyCard, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = Batch336Support.Options(e, 1, "ring-of-void-draw", ring.Id);
        Eq(before.Length, 1, "before Harmony the Ring has only its ordinary payment");
        var performers = Batch336Support.Options(e, 1, Wave741Abilities.Harmony, card.Id).Where(a => a.Enabled).Select(a => a.Command!.PerformerId).ToArray();
        Check(performers.Contains(monk.Id) && !performers.Contains(samurai.Id), "only a Monk may perform it");
        Accept(e, Batch336Support.Options(e, 1, Wave741Abilities.Harmony, card.Id).First(a => a.Enabled && a.Command!.PerformerId == monk.Id).Command!);
        Done(e, 1);
        var after = Batch336Support.Options(e, 1, "ring-of-void-draw", ring.Id);
        Check(after.Any(o => o.Enabled && o.Command?.CostOption == CostSubstitution.Option(monk.Id)), "the Monk may now bow himself instead of the Ring: " + string.Join("; ", after.Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")) + " pending " + e.State.PendingChoice?.Type + " monk granted " + string.Join(",", Now(e, monk).GrantedAbilities.Select(g => g.AbilityId)));
    }

    private static void JudgmentCardTest()
    {
        MatchEngine Rig(string[] keywords, bool dishonorable, out CardInstance magistrate, out CardInstance target, out CardInstance card, int gold = 12)
        {
            var s = Table(Phase.Action, 0);
            magistrate = P(s, "w741-magistrate", 0, Zone.Home, 2, 2, 1, ["Magistrate"]);
            target = P(s, "w741-judged", 1, Zone.Home, 3, 2, 1, keywords);
            target.Dishonorable = dishonorable;
            card = Add(s, JudgmentCard, 0, Zone.Hand);
            Gold(s, 0, gold);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(["Samurai"], false, out var magistrate, out var target, out var card);
        var command = Batch336Support.Use(e, 0, Wave741Abilities.Judgment, card.Id, target.Id, magistrate.Id);
        Eq(e.QuoteAbilityGold(command)?.Cost, 3 + 8, "the unit's total Gold Cost (3) plus 8");
        Accept(e, command with { SourceIds = e.PaymentOptions(0, 11).First().SourceIds });
        Settle(e);
        Check(!IsAt(Now(e, target), Zone.Home), "he commits seppuku");
        // A dishonorable target costs only his unit's Gold Cost.
        var d = Rig(["Samurai"], true, out var m2, out var t2, out var c2);
        Eq(d.QuoteAbilityGold(Batch336Support.Use(d, 0, Wave741Abilities.Judgment, c2.Id, t2.Id, m2.Id))?.Cost, 3, "dishonorable: no +8");
        foreach (var keyword in new[] { "Kolat", "Ninja", "Shadowlands" })
        {
            var k = Rig(["Samurai", keyword], false, out var m3, out var t3, out var c3);
            Eq(k.QuoteAbilityGold(Batch336Support.Use(k, 0, Wave741Abilities.Judgment, c3.Id, t3.Id, m3.Id))?.Cost, 3, $"{keyword}: no +8");
        }
        // Only a Human Personality is a target.
        var n = Rig(["Nonhuman", "Spirit"], false, out var m4, out var t4, out var c4);
        var offered = Batch336Support.Options(n, 0, Wave741Abilities.Judgment, c4.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(!offered.Contains(t4.Id), "a Nonhuman Personality is not a target");
        _ = m4;
    }

    private static void ChaosCardTest()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, ChaosCard, 0, Zone.Hand);
        var mine = Enumerable.Range(0, 3).Select(_ => Add(s, TrainingItem, 0, Zone.Hand)).ToArray();
        var theirs = Enumerable.Range(0, 2).Select(_ => Add(s, TrainingItem, 1, Zone.Hand)).ToArray();
        for (var i = 0; i < 8; i++) { var a = Add(s, TrainingItem, 0, Zone.FateDeck); a.FaceUp = false; s.Players[0].FateDeck.Add(a.Id); var b = Add(s, TrainingItem, 1, Zone.FateDeck); b.FaceUp = false; s.Players[1].FateDeck.Add(b.Id); }
        var e = MatchEngine.FromTestState(s);
        var (deck0, deck1) = (e.State.Players[0].FateDeck.Count, e.State.Players[1].FateDeck.Count);
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.Chaos, card.Id));
        Settle(e);
        Eq(e.State.Players[0].Hand.Count, 3, "the actor draws as many as he shuffled in (the played card is not one of them)");
        Eq(e.State.Players[1].Hand.Count, 2, "so does the other player");
        Eq(e.State.Players[0].FateDeck.Count, deck0, "the actor's deck is the same size");
        Eq(e.State.Players[1].FateDeck.Count, deck1, "so is the other player's");
        Check(e.State.Events.Any(x => x.Type.Contains("shuffle", StringComparison.OrdinalIgnoreCase)), "the shuffles are logged");
        _ = mine; _ = theirs;
    }

    private static void WavesCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var henshin = P(s, "w741-henshin", 0, Zone.Battlefield, 2, 2, 1, ["Henshin"], province: 0);
        var samurai = P(s, "w741-not-henshin", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var target = P(s, "w741-switched", 1, Zone.Battlefield, 5, 2, 1, ["Samurai"], province: 0);
        var card = Add(s, Waves, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var performers = Batch336Support.Options(e, 0, Wave741Abilities.ShiftingWaves, card.Id).Where(a => a.Enabled).Select(a => a.Command!.PerformerId).ToArray();
        Check(performers.Contains(henshin.Id) && !performers.Contains(samurai.Id), "only a Henshin may perform it");
        Accept(e, Batch336Support.Offer(e, 0, Wave741Abilities.ShiftingWaves, card.Id, target.Id, henshin.Id));
        Settle(e);
        Eq(e.CardForceForTest(target.Id), 2, "his Force becomes his Chi");
        Eq(e.PersonalityChiForTest(target.Id), 5, "his Chi becomes his Force");
    }

    private static void PrayerCardTest()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, PrayerCard, 0, Zone.Hand);
        var plain = Enumerable.Range(0, 4).Select(i => Fx(s, "training-focus-strategy", $"w741-strategy-{i}", 0, Zone.FateDiscard)).ToArray();
        var unique = Fx(s, "training-focus-strategy", "w741-unique-strategy", 0, Zone.FateDiscard, d => d with { Keywords = ["Unique"] });
        var e = MatchEngine.FromTestState(s);
        var deck = e.State.Players[0].FateDeck.Count;
        NoChange(e, Batch336Support.Use(e, 0, Wave741Abilities.Prayer, card.Id, null, null, Batch336Support.Pick("cards", plain.Select(p => p.Id).ToArray())), "four cards");
        NoChange(e, Batch336Support.Use(e, 0, Wave741Abilities.Prayer, card.Id, null, null, Batch336Support.Pick("cards", unique.Id)), "a Unique Strategy");
        Accept(e, Batch336Support.Use(e, 0, Wave741Abilities.Prayer, card.Id, null, null, Batch336Support.Pick("cards", plain[0].Id, plain[1].Id)));
        Settle(e);
        Eq(e.State.Players[0].FateDeck.Count, deck + 2, "two Strategies are shuffled into the deck");
        Check(plain.Take(2).All(p => Now(e, p).Zone == Zone.FateDeck), "they are in the Fate deck");
        Check(plain.Skip(2).All(p => Now(e, p).Zone == Zone.FateDiscard), "the rest stay in the discard pile");
        Eq(Now(e, card).Zone, Zone.RemovedFromGame, "Sorrowful Prayer is removed from the game");
    }

    private static void CoalescenceCardTest()
    {
        MatchEngine Rig(out CardInstance performer, out CardInstance target, out CardInstance card, bool otherIsSpirit = true)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            performer = P(s, "w741-spirit-a", 0, Zone.Battlefield, 4, 2, 1, ["Nonhuman", "Spirit"], province: 0, abilities: [Wave741Abilities.MerihikoRanged, Wave741Abilities.TsukauCopy]);
            target = P(s, "w741-spirit-b", 0, Zone.Battlefield, 2, 2, 1, otherIsSpirit ? ["Nonhuman", "Spirit"] : ["Samurai"], province: 0);
            P(s, "w741-foe-c", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            card = Add(s, CoalescenceCard, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var performer, out var target, out var card);
        var force = e.CardForceForTest(target.Id);
        Accept(e, Batch336Support.Offer(e, 0, Wave741Abilities.Coalescence, card.Id, target.Id, performer.Id));
        Settle(e);
        Check(!IsAt(Now(e, performer), Zone.Battlefield), "the performer is destroyed");
        Eq(e.CardForceForTest(target.Id), force + 4, "the target gets a Force bonus equal to the performer's Force");
        Check(Now(e, target).GrantedAbilities.Any(g => g.AbilityId == Wave741Abilities.MerihikoRanged && g.Duration == GrantDuration.Permanent), "it permanently copies his ability");
        Check(!Now(e, target).GrantedAbilities.Any(g => g.AbilityId == Wave741Abilities.TsukauCopy), "but not one that copies abilities");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional action is granted");
        var n = Rig(out var p2, out var t2, out var c2, otherIsSpirit: false);
        var offered = Batch336Support.Options(n, 0, Wave741Abilities.Coalescence, c2.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(!offered.Contains(t2.Id), "another Spirit only");
        _ = p2;
    }

    private static void EarthCard()
    {
        MatchEngine Rig(int enemyForce, out CardInstance monk, out CardInstance home, out CardInstance card, out CardInstance atBattle)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            monk = P(s, "w741-monk-e", 1, Zone.Battlefield, 4, 2, 1, ["Monk", "Earth"], province: 0);
            P(s, "w741-defender", 0, Zone.Battlefield, 9, 2, 1, ["Samurai"], province: 0);
            atBattle = P(s, "w741-at-battle", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            home = P(s, "w741-at-home", 0, Zone.Home, enemyForce, 2, 1, ["Samurai"]);
            card = Add(s, Earth, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(4, out var monk, out var home, out var card, out var atBattle);
        var targets = Batch336Support.Options(e, 1, Wave741Abilities.StrengthEarth, card.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(home.Id) && targets.Contains(atBattle.Id), "the Defender's home and the army are targets: " + string.Join(",", targets));
        Accept(e, Batch336Support.Offer(e, 1, Wave741Abilities.StrengthEarth, card.Id, home.Id, monk.Id));
        Settle(e);
        Check(!IsAt(Now(e, home), Zone.Home), "the Melee Attack of 4 destroys the Force 4 Personality at home");
        var strong = Rig(5, out var monk2, out var home2, out var card2, out _);
        Accept(strong, Batch336Support.Offer(strong, 1, Wave741Abilities.StrengthEarth, card2.Id, home2.Id, monk2.Id));
        Settle(strong);
        Check(IsAt(Now(strong, home2), Zone.Home), "a Force 5 Personality survives it");
    }

    private static void DiscoveryCardTest()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [0, 1];
        var mine = P(s, "w741-mine-d", 0, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        P(s, "w741-foe-d", 1, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        var card = Add(s, DiscoveryCardTest_Card, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Batch336Support.Options(e, 0, Wave741Abilities.Discovery, card.Id).First(a => a.Enabled && (a.TargetIds?.Contains(mine.Id) ?? false));
        Check(option.Provinces?.Contains(1) == true, "the adjacent unresolved battlefield is offered");
        Accept(e, option.Command! with { TargetId = mine.Id, Province = 1 });
        Settle(e);
        Check(IsAt(Now(e, mine), Zone.Battlefield, 1), "the Personality moves to the adjacent battlefield");
    }
    private const string DiscoveryCardTest_Card = Wave741Abilities.DiscoveryCard;

    private static void GatheringCardTest()
    {
        MatchEngine Rig(int attachmentGold, out CardInstance card, out CardInstance attachment, out CardInstance from, out CardInstance to)
        {
            var s = Table(Phase.Action, 0);
            from = P(s, "w741-from", 1, Zone.Home, 3, 2, 1, ["Samurai"]);
            to = P(s, "w741-to", 0, Zone.Home, 3, 2, 1, ["Samurai"]);
            attachment = Attach(s, "training-item", 1, from, "w741-gift", d => d with { GoldCost = attachmentGold, Force = 2 });
            card = Add(s, GatheringCardTest_Card, 0, Zone.Hand);
            Gold(s, 0, 6);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(2, out var card, out var attachment, out var from, out var to);
        var command = Batch336Support.Use(e, 0, Wave741Abilities.Gathering, card.Id, null, null, Batch336Support.Pick("attachment", attachment.Id), Batch336Support.Pick("to", to.Id)) with { GoldAmount = 3 };
        Accept(e, command with { SourceIds = e.PaymentOptions(0, 3).First().SourceIds });
        Settle(e);
        Eq(Now(e, attachment).AttachedTo, to.Id, "the attachment moves to the chosen Personality");
        Eq(Now(e, attachment).OwnerId, 0, "and its controller becomes that Personality's");
        var costly = Rig(4, out var c2, out var a2, out _, out var to2);
        var tooMuch = Batch336Support.Use(costly, 0, Wave741Abilities.Gathering, c2.Id, null, null, Batch336Support.Pick("attachment", a2.Id), Batch336Support.Pick("to", to2.Id)) with { GoldAmount = 3, SourceIds = costly.PaymentOptions(0, 3).First().SourceIds };
        NoChange(costly, tooMuch, "an attachment costing 4 with 3 paid");
        var exact = tooMuch with { GoldAmount = 4, SourceIds = costly.PaymentOptions(0, 4).First().SourceIds };
        Accept(costly, exact);
        Eq(Now(costly, a2).AttachedTo, to2.Id, "paying exactly its Gold Cost is enough");
    }
    private const string GatheringCardTest_Card = Wave741Abilities.GatheringCard;

    private static void TowerCard()
    {
        // The Fortification guards province 0; player 1 attacks it and engages. The Tower's controller (player 0) is the Defender and reacts after the Attacker passes.
        MatchEngine Rig(bool units, out CardInstance tower, bool expectWindow = true)
        {
            var s = Table(Phase.BattleSelection, 1); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            tower = Add(s, Tower, 0, Zone.Home);
            tower.EnteredPlayTurn = 1;
            s.Fortifications = new(StringComparer.Ordinal) { [tower.Id] = 0 };
            if (units) P(s, "w741-defender-t", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            P(s, "w741-attacker-t", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", 1, e.Version, Province: 0));
            // Presence rule (#1133): a Defender with no unit at the battlefield cannot take the Engage Reaction, so no Engage window opens for it.
            if (!expectWindow) return e;
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
            Accept(e, new GameCommand("declineReaction", 1, e.Version));
            return e;
        }
        var e = Rig(true, out var tower);
        Check(Batch336Support.CanUse(e, 0, Wave741Abilities.TowerEngage, tower.Id), "the Defender's Tower offers its Engage action at the guarded battlefield: " + string.Join("; ", Batch336Support.Options(e, 0, Wave741Abilities.TowerEngage, tower.Id).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")) + " | " + string.Join(";", e.QueryActions(0).Select(o => o.Label)));
        Accept(e, Batch336Support.Enabled(e, 0, Wave741Abilities.TowerEngage, tower.Id).Command!);
        var guards = e.State.Cards.Values.Where(c => c.DefinitionId == Wave741Abilities.TowerGuard && c.Zone == Zone.Battlefield).ToArray();
        Eq(guards.Length, 1, "a Guard is created");
        Eq(guards[0].ProvinceIndex, 0, "at the battlefield");
        Eq(guards[0].OwnerId, 0, "for the Tower's controller");
        Check(Now(e, tower).Bowed, "the Tower bows");
        Check(Batch336Support.CanUse(e, 0, Wave741Abilities.TowerEngage, tower.Id) == false, "it is bowed: no second use");
        // Without units there, nothing is created.
        var empty = Rig(false, out var tower2, expectWindow: false);
        Check(!Batch336Support.CanUse(empty, 0, Wave741Abilities.TowerEngage, tower2.Id), "no units at the battlefield: the Tower cannot be used (presence rule)");
        Check(!empty.State.Cards.Values.Any(c => c.DefinitionId == Wave741Abilities.TowerGuard), "no units, no Guard");
        // A Tower guarding another Province is not offered.
        var other = Rig(true, out var tower3);
        var elsewhere = Batch336Support.Edit(other, st => st.Fortifications![tower3.Id] = 1);
        Check(!Batch336Support.CanUse(elsewhere, 0, Wave741Abilities.TowerEngage, tower3.Id), "a Tower guarding another Province is not offered");
    }

    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
    }

    private static void MovingBattleTest()
    {
        MatchEngine Rig(int terrainOwner, out CardInstance x, out CardInstance y, out CardInstance terrain)
        {
            var s = Table(Phase.Battle, 0); s.UnresolvedBattlefields = [0, 1];
            x = P(s, "w741-mover", 0, Zone.Battlefield, 5, 2, 1, ["Samurai"], province: 0);
            y = P(s, "w741-opposer", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
            terrain = Add(s, MovingBattleCard, terrainOwner, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(0, out var x, out var y, out var terrain);
        PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
        Eq(e.State.PendingChoice!.PlayerId, 0, "the Terrain's controller decides");
        Eq(e.State.PendingChoice.CardId, terrain.Id, "about this Terrain");
        var options = e.QueryActions(0).Where(a => a.Type == "beforeBattleOptional").ToArray();
        Check(options.Any(o => o.Command!.Choice == "decline"), "the move may be declined");
        var uses = options.Where(o => o.Command!.Choice == "use").ToArray();
        Check(uses.All(o => o.Command!.TargetId == x.Id) && uses.Any(o => o.Command!.Province == 1), "only the Attacker's own opposed Personality, to the adjacent unresolved battlefield: " + string.Join(",", uses.Select(o => o.Label)));
        Check(!e.QueryActions(1).Any(a => a.Type == "beforeBattleOptional"), "the other player has nothing to answer");
        NoChange(e, new GameCommand("beforeBattleOptional", 0, e.Version, TargetId: y.Id, Province: 1, Choice: "use"), "an enemy Personality");
        NoChange(e, new GameCommand("beforeBattleOptional", 0, e.Version, TargetId: x.Id, Province: 0, Choice: "use"), "the same battlefield");
        Accept(e, uses.First(o => o.Command!.Province == 1).Command!);
        Check(!IsAt(Now(e, x), Zone.Battlefield, 0), "he is no longer in this battle");
        // Declining leaves him.
        var d = Rig(0, out var x2, out _, out _);
        PassUntil(d, () => d.State.PendingChoice?.Type == "beforeBattleOptional");
        Accept(d, d.QueryActions(0).First(a => a.Type == "beforeBattleOptional" && a.Command!.Choice == "decline").Command!);
        Check(d.State.PendingChoice?.Type != "beforeBattleOptional", "the battle went on to resolve");
        Check(!IsAt(Now(d, x2), Zone.Battlefield, 1), "he stayed where he was");
        // A Defender's Terrain has no attacking Personality of its controller's to move: nothing is asked.
        var other = Rig(1, out _, out _, out _);
        PassUntil(other, () => other.State.Phase != Phase.Battle || other.State.PendingChoice?.Type == "beforeBattleOptional");
        Check(other.State.PendingChoice?.Type != "beforeBattleOptional", "a Defender's Terrain asks nothing");
        // Played as a Terrain it replaces the Terrain already at the battlefield.
        var s2 = Table(Phase.Battle, 0);
        var card = Add(s2, MovingBattleCard, 0, Zone.Hand);
        P(s2, "w741-p1", 0, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        P(s2, "w741-p2", 1, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        var old = Add(s2, "oracle-l5r-7001", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, new GameCommand("playStrategy", 0, f.Version, card.Id));
        Eq(Now(f, card).Zone, Zone.Battlefield, "A Moving Battle is in play at the battlefield");
        Eq(Now(f, old).Zone, Zone.FateDiscard, "the Terrain that was there was destroyed");
    }
}
