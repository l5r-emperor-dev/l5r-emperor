using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition follow-ups (issue #498): Mendon's rulings after PR #497 -- Rend the Soul and No Hiding Place (strategy-wave-66), the Ring search primitive with Enslaved Djinn (follower-wave-32),
// Celestial Sword of the Dragon - exp (item-wave-17) and Asako Chukage (personality-wave-80), the Kurohito's Perfection audit and Makito's reading of "a card without attachments in his unit".
// Every position is built explicitly and driven only through QueryActions and the commands it offers (the primitives are in src/L5R.Core/EmperorFollowups*.cs).
internal static class EmperorFollowupsTests
{
    private const string Rend = "oracle-l5r-6260", NoHiding = "oracle-l5r-5599", Djinn = "oracle-l5r-10680", DragonSword = "oracle-l5r-10112", Chukage = "oracle-l5r-533",
        Kaitoko = "oracle-l5r-549", Volcano = "oracle-l5r-9186", Meiyu = "oracle-l5r-8550", Sosuke = "oracle-l5r-9811", Nitoshi = "oracle-l5r-850", Iaimiko = "oracle-l5r-10077",
        Makito = "oracle-l5r-4806", Kurohito = "oracle-l5r-10258", PlansWithinPlans = "oracle-l5r-5984", Halls = "oracle-l5r-2950", Ichizo = "oracle-l5r-5104", HonorNeverFalls = "oracle-l5r-3426",
        FirstBlood = "oracle-l5r-9904", RingOfAir = "oracle-l5r-6333", RingOfWater = "oracle-l5r-6339", RingOfVoid = "oracle-l5r-6338", RingOfEarth = "oracle-l5r-6334", RingOfFire = "oracle-l5r-6335";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void RunUntil(MatchEngine e, Func<bool> stop) => Batch336Support.RunUntil(e, stop);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));
    private static CardInstance StrongholdOf(MatchState s, int player, string definition) { var card = s.Cards[s.Players[player].StrongholdId]; card.DefinitionId = definition; return card; }
    private static string DefinitionWith(string ability) => CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains(ability) == true).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand[] Actions(MatchEngine e, int player, string type) => e.QueryActions(player).Where(a => a.Type == type && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);

    public static void Run(TestRunner runner) => runner.Run("emperor-followups", [
        ("Published shape: Rend the Soul and No Hiding Place (strategy-wave-66), Enslaved Djinn (follower-wave-32), Celestial Sword of the Dragon - exp (item-wave-17) and Asako Chukage (personality-wave-80) print their scans' stats, keywords, text and ids, ship their art and are in their manifests", PublishedShape),
        // "A card without attachments in his unit" (Makito's reading).
        ("Asako Kaitoko (Makito's reading): an Item without attachments in the target's unit is a card to destroy, like a Follower; with no attachment the Personality himself; a card with attachments is never offered", KaitokoAnyUnitCard),
        ("Volcano Tattoo (Makito's reading): an enemy Item in a unit whose Personality has equal or lower Chi is a legal target, destroyed and straightening a Fire performer; a Personality with attachments, or an Item of a higher-Chi unit, is not", VolcanoAnyUnitCard),
        ("Daidoji Sosuke, Shiba Iaimiko and Bayushi Nitoshi (the same reading): an Item without attachments in the unit may be the card bowed or destroyed; the Personality with attachments may not", OtherUnitCardReaders),
        // Kurohito's Perfection audit.
        ("Kurohito's Perfection: the twin taken on another player's turn (Plans Within Plans' Limited instead of Open) draws as its taker's own End Phase begins, not as the current turn's does", KurohitoOwnEndPhase),
        ("Kurohito's Perfection: the Favor's Battle action (send an attacker home) needs no card discard, is untouched by the Celestial and is never delayed; the twin is not offered in a battle", KurohitoFavorBattleUnchanged),
        // The Ring search primitive and its cards.
        ("Enslaved Djinn: after it enters play its controller searches the discard pile first -- a copy there must be taken, the deck is not searched or shuffled -- and puts it into his hand; priority then passes on as after the Equip", DjinnDiscardFirst),
        ("Enslaved Djinn: with no copy in the discard pile the deck is searched (and reshuffled); with none anywhere the search finds nothing and the deck is still reshuffled", DjinnDeckOrNothing),
        ("Celestial Sword of the Dragon - exp: attaches only to a Dragon Clan Personality; +1F per Dragon Clan Personality its controller controls; entering play gains 2 Honor and searches the discard pile, then the deck, for a Ring put into play (not counting for Enlightenment) or into the hand", DragonSword_),
        ("Asako Chukage: his entering-play search looks at the discard pile, then the hand, then the deck for a Ring of Water -- a Ring of Water in the discard pile must be taken, one only in the hand is found there (it leaves the hand if put into play); another Ring never qualifies", ChukageSearch),
        ("Asako Chukage, Reaction: even bowed, after an action moved one of his controller's Personalities he moves to that Personality's location and his unit straightens; nothing for an enemy's move, and no straightening when he is already there", ChukageFollow_),
        ("A Ring the searches put into play does not count towards an Enlightenment Victory: five Rings in play, one of them from the Sword's search, win nothing", RingSearchEnlightenment),
        // Rend the Soul.
        ("Rend the Soul, Focus Effect: its Focus Value is raised to 5 for the totals; if its owner's Personality then wins, the duel becomes a tie -- both lose and both take the loser's consequences", RendFocusWinnerLoses),
        ("Rend the Soul, Focus Effect, edges: if its owner's Personality loses anyway nothing changes; the raise is a set-to, ordered with Duel to First Blood's +1 (5 then +1 is 6, +1 then 5 is 5)", RendFocusEdges),
        ("Rend the Soul, Focus Effect, ordering: the rewritten outcome is what Honor Never Falls sees -- the Rend owner's Personality lost, so his consequences are delayed", RendWithHonorNeverFalls),
        ("Rend the Soul, Battle: +5F to the performing opposed Personality; the army he is in would win, so the battle is tied -- both armies destroyed, tie Honor, no province destroyed", RendBattleTie),
        ("Rend the Soul, Battle, edges: his army losing resolves as usual; if he is no longer in the winning army it wins, but the marking Attacker destroys no province; the mark lapses with the battle", RendBattleEdges),
        // No Hiding Place and the target selection step.
        ("No Hiding Place: after its player announces a Ranged Attack the announce window offers it; played, the Attack's target is selected afterwards -- a Personality without Followers in the Defender's home is legal and is destroyed at -1 strength", NoHidingPlaceHome),
        ("No Hiding Place, edges: without it a card at home is never a legal target; the -1 strength can make the Attack miss; the Attacker's own home, a followed Personality at home and a Melee Attack get nothing; a hand with no copy opens no window", NoHidingPlaceEdges),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), string.Join(",", ids), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
            }
        }
        var rend = CardCatalog.Get(Rend);
        Check(rend.Type == CardType.Strategy && rend.Name == "Rend the Soul" && rend.GoldCost == 0 && rend.Focus == 0, "Rend the Soul's stats");
        Eq(string.Join(",", rend.Abilities ?? []), "rend-the-soul-focus,rend-the-soul-battle");
        Eq(rend.Text, "As a Focus Effect: Raise this card's Focus Value to 5. If your Personality wins this duel, he loses instead (the other Personality still also loses).\nBattle: Choose your performing opposed Personality: Give him +5F. If the army he is in during this battle's resolution wins, this battle is tied instead. You will not destroy the province in this battle's resolution.");
        var hiding = CardCatalog.Get(NoHiding);
        Check(hiding.Type == CardType.Strategy && hiding.Name == "No Hiding Place" && hiding.GoldCost == 0 && hiding.Focus == 3, "No Hiding Place's stats");
        Eq(string.Join(",", hiding.Abilities ?? []), "no-hiding-place");
        Eq(hiding.Text, "Reaction: After you announce an action with a Ranged Attack: Give one Ranged Attack from the action -1 strength. That Ranged Attack may target a card in the Defender's home (if otherwise legal).");
        Manifest("strategy-wave-66", NoHiding, Rend);
        var djinn = CardCatalog.Get(Djinn);
        Check(djinn.Type == CardType.Follower && djinn.Name == "Enslaved Djinn" && djinn.GoldCost == 4 && djinn.Force == 2 && djinn.Chi == 0 && djinn.Focus == 1 && djinn.HonorRequirement is null, "Enslaved Djinn's stats");
        Eq(string.Join(",", djinn.Keywords ?? []), "Djinn,Fire,Nonhuman,Spirit"); Eq(djinn.Effect, "enslaved-djinn-search"); Eq((djinn.Abilities ?? []).Length, 0);
        Eq(djinn.Text, "After this Follower enters play, search your discard pile, then deck, for another Enslaved Djinn. Show it. Put it into your hand.");
        Manifest("follower-wave-32", Djinn);
        var sword = CardCatalog.Get(DragonSword);
        Check(sword.Type == CardType.Item && sword.Name == "Celestial Sword of the Dragon" && sword.GoldCost == 8 && sword.Force == 1 && sword.Chi == 1 && sword.Focus == 4 && sword.AttachRequiresKeyword == "Dragon Clan", "the Sword's stats");
        Eq(string.Join(",", sword.Keywords ?? []), "Unique,Weapon,Experienced,Suryn"); Eq(sword.Effect, "celestial-sword-force-dragon");
        Eq(sword.Text, "Will only attach to a Dragon Clan Personality.\nThis card has +1F for each Dragon Clan Personality you control.\nAfter this card enters play: Gain 2 Honor.\nAfter this card enters play: Search your discard pile, then deck, for a Ring. Show it. Put it into play or your hand.");
        Manifest("item-wave-17", DragonSword);
        var chukage = CardCatalog.Get(Chukage);
        Check(chukage.Type == CardType.Personality && chukage.Name == "Asako Chukage" && chukage.Clan == "Phoenix" && chukage.GoldCost == 10 && chukage.Force == 4 && chukage.Chi == 5 && chukage.PersonalHonor == 3 && chukage.HonorRequirement == 6, "Chukage's stats");
        Eq(string.Join(",", (chukage.Keywords ?? []).Order(StringComparer.Ordinal)), "Cavalry,Elemental Master,Henshin,Monk,Phoenix Clan,Shugenja,Unique,Water");
        Eq(string.Join(",", chukage.Abilities ?? []), "chukage-follow"); Eq(chukage.Effect, "chukage-ring-search");
        Eq(chukage.Text, "After Chukage enters play: Search your discard pile, then hand, then deck, for a Ring of Water. Put it into play or in your hand.\nReaction: Even if Chukage is bowed, after the resolution of an action that moved your Personality: Move Chukage to the Personality's location. Straighten Chukage's unit if he moved.");
        Manifest("personality-wave-80", Chukage);
        Eq(AbilityCatalog.All["rend-the-soul-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["rend-the-soul-battle"].Timing, "Battle");
        Eq(AbilityCatalog.All["no-hiding-place"].Timing, "Reaction"); Eq(AbilityCatalog.All["chukage-follow"].Timing, "Reaction");
    }

    // ---- "a card without attachments in his unit" ---------------------------------------------------------------------------------------------------------

    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }

    private static void KaitokoAnyUnitCard()
    {
        // A Ninja with one Item (no Follower): the Item is the one card without attachments -- destroyed at once.
        var s = Battle(); var kaitoko = Add(s, Kaitoko, 0, Zone.Battlefield, province: 0);
        var ninja = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(ninja, "Ninja");
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: ninja.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "kaitoko-weaken", kaitoko.Id, ninja.Id));
        Check(e.State.Cards[item.Id].Zone != Zone.Attached, "the Item without attachments in his unit was destroyed");
        Eq(e.State.Cards[ninja.Id].Zone, Zone.Battlefield, "he has an attachment, so he is not a card without attachments");
        // An Item and a Follower: the acting player chooses between them.
        var s2 = Battle(); var k2 = Add(s2, Kaitoko, 0, Zone.Battlefield, province: 0);
        var shadow = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(shadow, "Shadowlands");
        var item2 = Add(s2, "training-item", 1, Zone.Attached, province: 0, parent: shadow.Id);
        var follower = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: shadow.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "kaitoko-weaken", k2.Id, shadow.Id));
        var choices = Actions(e2, 0, "chooseEffect");
        Eq(string.Join(",", choices.Select(c => c.Choice).Order(StringComparer.Ordinal)), string.Join(",", new[] { item2.Id, follower.Id }.Order(StringComparer.Ordinal)), "the Item and the Follower are offered, the Personality is not");
        Accept(e2, choices.Single(c => c.Choice == item2.Id));
        Check(e2.State.Cards[item2.Id].Zone != Zone.Attached && e2.State.Cards[follower.Id].Zone == Zone.Attached, "the chosen Item alone was destroyed");
        // Negative: a Follower that itself carries an attachment is not a card without attachments.
        var s3 = Battle(); var k3 = Add(s3, Kaitoko, 0, Zone.Battlefield, province: 0);
        var kolat = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(kolat, "Kolat");
        var carrier = Add(s3, "training-follower", 1, Zone.Attached, province: 0, parent: kolat.Id);
        var onCarrier = Add(s3, "training-item", 1, Zone.Attached, province: 0, parent: carrier.Id);
        var bare = Add(s3, "training-item", 1, Zone.Attached, province: 0, parent: kolat.Id);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "kaitoko-weaken", k3.Id, kolat.Id));
        Eq(e3.State.Cards[carrier.Id].Zone, Zone.Attached, "the Follower with an attachment is never the card destroyed");
        Check(e3.State.Cards[bare.Id].Zone != Zone.Attached, "the Item without attachments was");
        Eq(e3.State.Cards[onCarrier.Id].Zone, Zone.Attached, "and the attachment's own attachment is not in question");
    }

    private static void VolcanoAnyUnitCard()
    {
        var s = Battle(); s.ActivePlayerId = 0;
        var meiyu = Add(s, Meiyu, 0, Zone.Battlefield, province: 0); // Fire, Monk, Tattooed, Chi 3
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: host.Id);
        var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Chi 4
        var strongItem = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: strong.Id);
        var volcano = Add(s, Volcano, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "volcano-tattoo-destroy", volcano.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(item.Id), "an Item without attachments in a unit whose Personality's Chi is equal is a legal target");
        Check(!targets.Contains(host.Id), "a Personality with an attachment is not");
        Check(!targets.Contains(strongItem.Id), "an Item of a unit whose Personality has higher Chi is not");
        Accept(e, Offer(e, 0, "volcano-tattoo-destroy", volcano.Id, item.Id, meiyu.Id));
        Check(e.State.Cards[item.Id].Zone != Zone.Attached, "the Item was destroyed");
        Check(!e.State.Cards[meiyu.Id].Bowed, "an attachment was destroyed and the performer is Fire: he straightens");
        Eq(e.State.Cards[host.Id].Zone, Zone.Battlefield, "its Personality stays");
    }

    private static void OtherUnitCardReaders()
    {
        // Daidoji Sosuke: bow a card without attachments in the target's unit -- the Item, offered with the Follower.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
        var sosuke = Add(s, Sosuke, 0, Zone.Home);
        var crab = Add(s, "training-lion-samurai", 1, Zone.Home);
        var item = Add(s, "training-item", 1, Zone.Attached, parent: crab.Id);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: crab.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "sosuke-tariffs", sosuke.Id, crab.Id));
        var choices = Actions(e, 0, "chooseEffect");
        Check(choices.Any(c => c.Choice == item.Id) && choices.Any(c => c.Choice == follower.Id) && choices.All(c => c.Choice != crab.Id), "Sosuke: the Item and the Follower are offered, the Personality is not");
        Accept(e, choices.Single(c => c.Choice == item.Id));
        Check(e.State.Cards[item.Id].Bowed && !e.State.Cards[follower.Id].Bowed && !e.State.Cards[crab.Id].Bowed, "the Item alone was bowed");
        // Bayushi Nitoshi: "a card without attachments in a unit" -- an Item is a legal target; its Personality (not dishonorable, with attachments) is not.
        var s2 = Battle(); s2.ActivePlayerId = 0;
        var nitoshi = Add(s2, Nitoshi, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyItem = Add(s2, "training-item", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e2 = MatchEngine.FromTestState(s2);
        var targets = Options(e2, 0, "nitoshi-destroy", nitoshi.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(enemyItem.Id) && !targets.Contains(enemy.Id), "Nitoshi: the Item is a target, the honorable Personality with an attachment is not");
        Accept(e2, Offer(e2, 0, "nitoshi-destroy", nitoshi.Id, enemyItem.Id));
        Check(e2.State.Cards[enemyItem.Id].Zone != Zone.Attached, "the Item was destroyed");
        // Shiba Iaimiko: the optional second target may be an Item without attachments in the first target's unit.
        var s3 = Battle(); s3.ActivePlayerId = 0;
        var iaimiko = Add(s3, Iaimiko, 0, Zone.Battlefield, province: 0);
        GiveKeyword(Add(s3, "training-crane-samurai", 0, Zone.Home), "Shugenja"); // "and you control a Shugenja"
        var weak = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var weakItem = Add(s3, "training-item", 1, Zone.Attached, province: 0, parent: weak.Id); // +2F: 6 -- give -4F more so that "now 0"
        Force(weak, -2);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!e3.Execute(Use(e3, 0, "iaimiko-weaken", iaimiko.Id, selections: [Pick("target", weak.Id), Pick("destroy", weak.Id)])).Accepted, "Iaimiko: the Personality with an attachment is not a card without attachments");
        Accept(e3, Use(e3, 0, "iaimiko-weaken", iaimiko.Id, selections: [Pick("target", weak.Id), Pick("destroy", weakItem.Id)]));
        Eq(e3.CardForceForTest(weak.Id), 0, "Iaimiko: -4F, now 0");
        Check(e3.State.Cards[weakItem.Id].Zone != Zone.Attached, "Iaimiko: the Item without attachments in his unit was destroyed (a Shugenja is controlled)");
    }

    // ---- Kurohito's Perfection ------------------------------------------------------------------------------------------------------------------------------

    private static void KurohitoOwnEndPhase()
    {
        // Player 1's turn, Action Phase; player 0 armed Plans Within Plans earlier and holds the Imperial Favor and Kurohito's Perfection.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
        StrongholdOf(s, 0, Halls);
        Add(s, Kurohito, 0, Zone.Celestial);
        s.Players[0].PlansWithinPlansArmed = true; s.Players[0].PlansWithinPlansSets = 1;
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "favor-cycle-kurohito"), "Plans Within Plans lets him take the Limited twin at his Open opportunity on player 1's turn");
        Accept(e, Enabled(e, 0, "favor-cycle-kurohito").Command!);
        var delayed = e.State.DelayedEffects.Single();
        Check(delayed.Kind == DelayedKinds.Draw && delayed.PlayerId == 0 && delayed.Until == DelayUntil.EndPhaseBegins, "the draw waits for his End Phase");
        // The End Phase is not a resting point: run to the next turn (player 0's) and look at what happened as player 1's End Phase began.
        RunUntil(e, () => e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action);
        Eq(e.State.DelayedEffects.Count, 1, "player 1's End Phase began and ended: not his, so the draw still waits");
        Check(!e.State.Events.Any(x => x.Type == "delayedEffectApplied"), "and nothing was drawn for it");
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Eq(e.State.DelayedEffects.Count, 0, "his own End Phase began: the draw was applied");
        var events = e.State.Events.Select(x => x.Type + ":" + x.Message).ToList();
        var applied = events.FindIndex(x => x.StartsWith("delayedEffectApplied:") && x.Contains("draws a card"));
        Check(applied > 0 && events.Take(applied).Last(x => x.StartsWith("phaseChanged:")) == "phaseChanged:Phase: End.", "applied as an End Phase began");
        Check(e.State.Events.Any(x => x.Type == "delayedEffectApplied" && x.Message.Contains("draws a card") && x.PlayerId == 0), "he drew the delayed card");
    }

    private static void KurohitoFavorBattleUnchanged()
    {
        // Player 1 attacks; player 0 (the Defender) holds the Favor and Kurohito's Perfection, and an empty hand.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
        StrongholdOf(s, 0, Halls);
        Add(s, Kurohito, 0, Zone.Celestial);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.State.Players[0].Hand.Count, 0, "no card to discard");
        Check(!CanUse(e, 0, "favor-cycle-kurohito"), "the twin (a Limited action) is not offered in a battle");
        Accept(e, Offer(e, 0, "favor-battle", null!, attacker.Id));
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the Favor's Battle action sent the attacker home, with no card discarded");
        Eq(e.State.FavorPlayerId, null, "the Favor was discarded as its cost");
        Check(e.State.DelayedEffects.Count == 0, "nothing was delayed: Kurohito's Perfection does not touch this ability");
        // Negative: without the Favor it is not offered at all.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0; StrongholdOf(s2, 0, Halls); Add(s2, Kurohito, 0, Zone.Celestial);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "favor-battle"), "no Favor, no Favor Battle action");
    }

    // ---- The Ring search primitive --------------------------------------------------------------------------------------------------------------------------

    private static MatchState ActionTable(int gold = 6)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        for (var i = 0; i < gold; i++) Add(s, "training-marketplace", 0, Zone.Home);
        return s;
    }

    private static void EquipFromHand(MatchEngine e, CardInstance card, CardInstance target, int cost) =>
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(0, cost).First().SourceIds, TargetId: target.Id));

    private static string[] SelectableDefinitions(MatchEngine e) => Actions(e, 0, "selectSearchCard").Select(c => c.Choice!).ToArray();

    private static void DjinnDiscardFirst()
    {
        var s = ActionTable();
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var djinn = Add(s, Djinn, 0, Zone.Hand);
        var inDiscard = Add(s, Djinn, 0, Zone.FateDiscard);
        var inDeck = Add(s, Djinn, 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, inDeck.Id);
        var e = MatchEngine.FromTestState(s);
        EquipFromHand(e, djinn, host, 4);
        Eq(e.State.Cards[djinn.Id].Zone, Zone.Attached, "the Djinn entered play");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "its trait's search opened");
        Eq(e.GetView(0).PendingSearch?.Deck, SearchDeck.FateDiscard, "the discard pile is searched first");
        Check(SelectableDefinitions(e).SequenceEqual([Djinn]), "the copy there must be taken");
        Check(!e.QueryActions(0).Any(a => a.Type == "passSearch"), "no passing on to the deck while one qualifies here");
        var shuffles = e.State.Events.Count(x => x.Type == "deckShuffled");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Djinn));
        Eq(e.State.Cards[inDiscard.Id].Zone, Zone.Hand, "the discard-pile copy is in his hand");
        Check(e.State.Players[0].Hand.Contains(inDiscard.Id), "listed in the hand");
        Eq(e.State.Cards[inDeck.Id].Zone, Zone.FateDeck, "the deck copy stays");
        Eq(e.State.Events.Count(x => x.Type == "deckShuffled"), shuffles, "the deck was not searched, so not shuffled");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == Djinn && x.PlayerId == 0), "shown");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search ended");
        Eq(e.State.PriorityPlayerId, 1, "priority passes on as after any Equip");
    }

    private static void DjinnDeckOrNothing()
    {
        var s = ActionTable();
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var djinn = Add(s, Djinn, 0, Zone.Hand);
        var inDeck = Add(s, Djinn, 0, Zone.FateDeck); s.Players[0].FateDeck.Add(inDeck.Id);
        var e = MatchEngine.FromTestState(s);
        EquipFromHand(e, djinn, host, 4);
        Eq(e.GetView(0).PendingSearch?.Deck, SearchDeck.FateDiscard, "the discard pile first");
        Check(SelectableDefinitions(e).Length == 0, "nothing qualifies there");
        Accept(e, Actions(e, 0, "passSearch").First(c => c.Choice is null));
        Eq(e.GetView(0).PendingSearch?.Deck, SearchDeck.Fate, "then the deck");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Djinn));
        Eq(e.State.Cards[inDeck.Id].Zone, Zone.Hand, "found in the deck");
        Check(e.State.Events.Any(x => x.Type == "deckShuffled" && x.PlayerId == 0), "the searched deck was reshuffled");
        // None anywhere: nothing found, the deck is still reshuffled, play goes on.
        var s2 = ActionTable();
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Home); var djinn2 = Add(s2, Djinn, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var hand = e2.State.Players[0].Hand.Count;
        EquipFromHand(e2, djinn2, host2, 4);
        Accept(e2, Actions(e2, 0, "passSearch").First(c => c.Choice is null));
        Check(SelectableDefinitions(e2).Length == 0, "no copy in the deck either");
        Accept(e2, Actions(e2, 0, "passSearch").First());
        Eq(e2.State.Players[0].Hand.Count, hand - 1, "nothing entered the hand (the Djinn itself left it)");
        Check(e2.State.Events.Count(x => x.Type == "deckShuffled" && x.PlayerId == 0) >= 1, "the deck was searched, so reshuffled");
        Check(e2.State.PendingSearch is null && e2.State.PendingChoice is null && e2.State.PriorityPlayerId == 1, "the search ended and priority passed on");
    }

    private static void DragonSword_()
    {
        var s = ActionTable(8);
        s.Players[0].Clan = "Dragon";
        var dragon = Add(s, "training-tattooed", 0, Zone.Home); GiveKeyword(dragon, "Dragon Clan"); // a Dragon Clan Personality (Clan Dragon, the Dragon Clan keyword)
        var otherDragon = Add(s, "training-tattooed", 0, Zone.Home); GiveKeyword(otherDragon, "Dragon Clan");
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        var sword = Add(s, DragonSword, 0, Zone.Hand);
        var ringInDiscard = Add(s, RingOfAir, 0, Zone.FateDiscard);
        var ringInDeck = Add(s, RingOfFire, 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, ringInDeck.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Any(a => a.Type == "equip" && a.CardId == sword.Id && a.Enabled && (a.Command?.TargetId == crane.Id || (a.TargetIds ?? []).Contains(crane.Id))), "it will only attach to a Dragon Clan Personality");
        Check(!e.Execute(new GameCommand("equip", 0, e.Version, CardId: sword.Id, SourceIds: e.PaymentOptions(0, 8).First().SourceIds, TargetId: crane.Id)).Accepted, "a hand-made equip on a non-Dragon is refused");
        var honor = Honor(e, 0);
        var force = e.CardForceForTest(dragon.Id);
        EquipFromHand(e, sword, dragon, 8);
        Eq(Honor(e, 0), honor + 2, "entering play: gain 2 Honor");
        Eq(e.CardForceForTest(dragon.Id), force + 1 + 2, "+1F printed, and +1F for each of the two Dragon Clan Personalities he controls");
        Eq(e.GetView(0).PendingSearch?.Deck, SearchDeck.FateDiscard, "the Ring search looks at the discard pile first");
        Check(SelectableDefinitions(e).SequenceEqual([RingOfAir]), "the Ring there must be taken");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfAir));
        Eq(e.State.PendingChoice?.Type, "searchDestination", "into play or into the hand");
        Eq(Actions(e, 0, "chooseSearchDestination").Length, 2, "two choices");
        Accept(e, Actions(e, 0, "chooseSearchDestination").Single(c => c.Choice == "play"));
        var ring = e.State.Cards[ringInDiscard.Id];
        Check(ring.Zone == Zone.Home && ring.FaceUp && !ring.Bowed, "the Ring is in play");
        Check(ring.AbilityUses.ContainsKey("ee-e:no-enlightenment"), "and marked: it does not count towards an Enlightenment Victory");
        Eq(e.State.Cards[ringInDeck.Id].Zone, Zone.FateDeck, "the deck is not searched");
        Eq(e.State.PriorityPlayerId, 1, "the Equip's priority passes on");
        // The hand instead: from the deck (the discard pile holds no Ring), unmarked, in the hand.
        var s2 = ActionTable(8); s2.Players[0].Clan = "Dragon";
        var d2 = Add(s2, "training-tattooed", 0, Zone.Home); GiveKeyword(d2, "Dragon Clan");
        var sword2 = Add(s2, DragonSword, 0, Zone.Hand);
        var deckRing = Add(s2, RingOfWater, 0, Zone.FateDeck); s2.Players[0].FateDeck.Add(deckRing.Id);
        var e2 = MatchEngine.FromTestState(s2);
        EquipFromHand(e2, sword2, d2, 8);
        Accept(e2, Actions(e2, 0, "passSearch").First(c => c.Choice is null));
        Accept(e2, new GameCommand("selectSearchCard", 0, e2.Version, Choice: RingOfWater));
        Accept(e2, Actions(e2, 0, "chooseSearchDestination").Single(c => c.Choice == "hand"));
        Check(e2.State.Cards[deckRing.Id].Zone == Zone.Hand && e2.State.Players[0].Hand.Contains(deckRing.Id), "the Ring is in the hand");
        Check(!e2.State.Cards[deckRing.Id].AbilityUses.ContainsKey("ee-e:no-enlightenment"), "not marked: it did not enter play");
    }

    private static (MatchEngine Engine, string CardId) RecruitChukage(Action<MatchState> more)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = Chukage; s.Cards[cardId].FaceUp = true;
        more(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(Chukage).GoldCost).First().SourceIds));
        for (var guard = 0; e.State.PendingChoice is { Type: "proclaim" or "entryReaction" } && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        return (e, cardId);
    }

    private static void ChukageSearch()
    {
        // A Ring of Water in the hand and one in the deck, none in the discard pile (a Ring of Air there does not qualify).
        CardInstance? inHand = null, inDeck = null, air = null;
        var (e, chukage) = RecruitChukage(s =>
        {
            air = Add(s, RingOfAir, 0, Zone.FateDiscard);
            inHand = Add(s, RingOfWater, 0, Zone.Hand);
            inDeck = Add(s, RingOfWater, 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, inDeck.Id);
        });
        Eq(e.State.Cards[chukage].Zone, Zone.Home, "Chukage entered play");
        Eq(e.GetView(0).PendingSearch?.Deck, SearchDeck.FateDiscard, "his search looks at the discard pile first");
        Check(SelectableDefinitions(e).Length == 0, "a Ring of Air does not qualify");
        Accept(e, Actions(e, 0, "passSearch").First(c => c.Choice is null));
        Eq(e.GetView(0).PendingSearch?.Deck, SearchDeck.Hand, "then the hand");
        Check(SelectableDefinitions(e).SequenceEqual([RingOfWater]), "the Ring of Water in the hand is found there");
        Check(!e.QueryActions(0).Any(a => a.Type == "passSearch"), "and must be taken: the deck is not searched");
        var shuffles = e.State.Events.Count(x => x.Type == "deckShuffled");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfWater));
        Accept(e, Actions(e, 0, "chooseSearchDestination").Single(c => c.Choice == "play"));
        Check(e.State.Cards[inHand!.Id].Zone == Zone.Home && !e.State.Players[0].Hand.Contains(inHand.Id), "it left the hand by entering play");
        Check(e.State.Cards[inHand.Id].AbilityUses.ContainsKey("ee-e:no-enlightenment"), "marked: no Enlightenment");
        Eq(e.State.Cards[inDeck!.Id].Zone, Zone.FateDeck, "the deck copy stays");
        Eq(e.State.Events.Count(x => x.Type == "deckShuffled"), shuffles, "neither the hand nor the unsearched deck is shuffled");
        Eq(e.State.Cards[air!.Id].Zone, Zone.FateDiscard, "the Ring of Air stays in the discard pile");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null && e.State.PriorityPlayerId == 0, "the Dynasty purchase gives priority back to its player");
        // A Ring of Water in the discard pile: taken from there (the hand is not shown), into the hand this time.
        CardInstance? discarded = null, handCopy = null;
        var (e2, _) = RecruitChukage(s => { discarded = Add(s, RingOfWater, 0, Zone.FateDiscard); handCopy = Add(s, RingOfWater, 0, Zone.Hand); });
        Eq(e2.GetView(0).PendingSearch?.Deck, SearchDeck.FateDiscard, "the discard pile first");
        Check(SelectableDefinitions(e2).SequenceEqual([RingOfWater]) && !e2.QueryActions(0).Any(a => a.Type == "passSearch"), "the copy there must be taken");
        Accept(e2, new GameCommand("selectSearchCard", 0, e2.Version, Choice: RingOfWater));
        Accept(e2, Actions(e2, 0, "chooseSearchDestination").Single(c => c.Choice == "hand"));
        Check(e2.State.Cards[discarded!.Id].Zone == Zone.Hand && e2.State.Players[0].Hand.Contains(discarded.Id), "the discard copy went to the hand");
        Check(e2.State.Players[0].Hand.Contains(handCopy!.Id), "the hand copy is still there");
    }

    private static void ChukageFollow_()
    {
        // Player 1 attacks with a Personality; his Chukage stands bowed at the battlefield too. Player 0 sends the attacker home with the Favor's Battle action.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
        StrongholdOf(s, 0, Halls);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var chukage = Add(s, Chukage, 1, Zone.Battlefield, bowed: true, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: chukage.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "favor-battle", null!, attacker.Id));
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker was moved home");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window after the action opened");
        var follow = Options(e, 1, "chukage-follow", chukage.Id).Where(o => o.Enabled).ToArray();
        Check(follow.Length > 0 && follow.All(o => (o.TargetIds ?? []).SequenceEqual([attacker.Id])), "Chukage may react, though bowed, naming the moved Personality");
        if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, Offer(e, 1, "chukage-follow", chukage.Id, attacker.Id));
        Eq(e.State.Cards[chukage.Id].Zone, Zone.Home, "Chukage moved to the Personality's location");
        Check(!e.State.Cards[chukage.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "he moved, so his unit straightened");
        // Negative: an enemy Personality was moved -- his controller's Chukage has nothing to react to.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0; s2.FavorPlayerId = 0;
        StrongholdOf(s2, 0, Halls);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ownChukage = Add(s2, Chukage, 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "favor-battle", null!, attacker2.Id));
        Check(!CanUse(e2, 0, "chukage-follow", ownChukage.Id), "an enemy Personality's move is not one of his controller's Personalities moving");
        // Already at the location: no move, no straightening. (Presence: in a battle the Reaction's Personality must itself be at the battlefield, so Chukage stands there
        // and the moved Personality is brought to that same battlefield by his controller's own action -- he is already at the location.)
        var s3 = Table(Phase.Battle, 1);
        var mover3 = Add(s3, "training-move-source", 1, Zone.Battlefield, province: 0);
        var homeChukage = Add(s3, Chukage, 1, Zone.Battlefield, bowed: true, province: 0);
        var moved3 = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-move-current", mover3.Id, moved3.Id));
        Eq(e3.State.Cards[moved3.Id].Zone, Zone.Battlefield, "the Personality was moved to the battlefield");
        Eq(e3.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window after the action opened");
        if (e3.State.PriorityPlayerId != 1) Accept(e3, new GameCommand("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        Accept(e3, Offer(e3, 1, "chukage-follow", homeChukage.Id, moved3.Id));
        Check(e3.State.Cards[homeChukage.Id].Zone == Zone.Battlefield && e3.State.Cards[homeChukage.Id].Bowed, "he was already there: he did not move and stays bowed");
    }

    private static void RingSearchEnlightenment()
    {
        // Four Rings in play by their own text, the fifth put into play by the Sword's search: at the start of player 0's next turn, no Enlightenment Victory.
        var s = ActionTable(8); s.Players[0].Clan = "Dragon";
        foreach (var def in new[] { RingOfAir, RingOfWater, RingOfVoid, RingOfEarth }) Add(s, def, 0, Zone.Home);
        var dragon = Add(s, "training-tattooed", 0, Zone.Home); GiveKeyword(dragon, "Dragon Clan");
        var sword = Add(s, DragonSword, 0, Zone.Hand);
        var fire = Add(s, RingOfFire, 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        EquipFromHand(e, sword, dragon, 8);
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingOfFire));
        Accept(e, Actions(e, 0, "chooseSearchDestination").Single(c => c.Choice == "play"));
        Eq(e.State.Cards[fire.Id].Zone, Zone.Home, "the fifth Ring is in play");
        RunUntil(e, () => e.State.WinnerId is not null || e.State.TurnNumber > s.TurnNumber + 1 && e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action);
        Check(e.State.WinnerId is null, "no Enlightenment Victory: the searched Ring does not count");
    }

    // ---- Rend the Soul, Focus Effect ------------------------------------------------------------------------------------------------------------------------

    private static void ClosePassingDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void DriveToFocusEffects(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
    }

    // Player 1's Mirumoto Ichizo, Experienced ("duel him. Destroy the duel's loser") challenges player 0's Personality with Chi `targetChi`; Ichizo's Chi is `ichizoChi`. Player 0 focuses `focus` (in order).
    private static (MatchEngine E, CardInstance Ichizo, CardInstance Target, CardInstance[] Focused) Duel(int ichizoChi, int targetChi, string[] focus, string[]? order = null)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ichizoDef = CardCatalog.Get(Ichizo);
        ChiBoost(ichizo, ichizoChi - ichizoDef.Chi); ChiBoost(target, targetChi - 3);
        Force(target, 10);
        var cards = focus.Select(f => Add(s, f, 0, Zone.Hand)).ToArray();
        var fillers = focus.Select(_ => Add(s, "training-item", 1, Zone.Hand)).ToArray(); // Focus 0: player 1's answers when he must focus before player 0's next card
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        ClosePassingDuel(e);
        for (var i = 0; i < cards.Length; i++)
        {
            if (e.State.PendingDuel is { Stage: "Focusing" } d && d.TurnPlayerId == 1) Accept(e, new GameCommand("focus", 1, e.Version, CardId: fillers[i].Id));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: cards[i].Id));
        }
        DriveToFocusEffects(e);
        if (e.State.PendingDuel is { Stage: "OrderingEffects" })
            Accept(e, new GameCommand("orderDuelFocusEffects", e.State.ActivePlayerId, e.Version, CardIds: (order ?? focus).Select(def => cards.First(c => c.DefinitionId == def).Id).ToArray()));
        return (e, ichizo, target, cards);
    }

    private static void RendFocusWinnerLoses()
    {
        // Player 0's Personality has Chi 3 against Ichizo's 6: behind by 3 before focusing. Rend the Soul (printed Focus 0) counts 5, so he wins -- and so loses instead: both lose.
        var (e, ichizo, target, focused) = Duel(6, 3, [Rend]);
        var result = e.State.LastDuel!.Result!;
        Eq(result.ChallengedTotal, 3 + 5, "Rend the Soul's Focus Value counts as 5 in the totals");
        Eq(result.ChallengerTotal, 6, "Ichizo struck without focusing");
        Eq(result.WinnerId, null, "his Personality won (8 against 6), so he loses instead: there is no winner");
        Eq(result.LoserIds.Length, 2, "both Personalities lose");
        Eq(result.ResolutionReason, "Rend the Soul · the winner loses too");
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the Rend owner's Personality took the loser's consequence (destroyed)");
        Eq(e.State.Cards[ichizo.Id].Zone, Zone.Dead, "and the other Personality still also loses (destroyed)");
        Check(Logged(e, "duelWinnerLoses"), "logged");
        Eq(e.State.Cards[focused[0].Id].Zone, Zone.FateDiscard, "the focused Rend the Soul is spent");
    }

    private static void RendFocusEdges()
    {
        // He loses anyway (Ichizo's Chi 20): nothing changes -- only he is destroyed.
        var (e, ichizo, target, _) = Duel(20, 3, [Rend]);
        var result = e.State.LastDuel!.Result!;
        Eq(result.WinnerId, ichizo.Id, "Ichizo won");
        Check(result.LoserIds.SequenceEqual([target.Id]), "only the Rend owner's Personality lost");
        Eq(e.State.Cards[ichizo.Id].Zone, Zone.Battlefield, "the winner is untouched");
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the loser is destroyed");
        Check(!Logged(e, "duelWinnerLoses"), "no rewrite");
        // The raise is a set-to, ordered with Duel to First Blood: First Blood then Rend -> Rend is 5 (+ First Blood's own 1+1); Rend then First Blood -> Rend is 6.
        var firstBloodFocus = CardCatalog.Get(FirstBlood).Focus;
        var (a, _, _, _) = Duel(20, 3, [FirstBlood, Rend], [FirstBlood, Rend]);
        Eq(a.State.LastDuel!.Result!.ChallengedTotal, 3 + (firstBloodFocus + 1) + 5, "First Blood first: +1 to both, then Rend set to 5");
        var (b, _, _, _) = Duel(20, 3, [FirstBlood, Rend], [Rend, FirstBlood]);
        Eq(b.State.LastDuel!.Result!.ChallengedTotal, 3 + (firstBloodFocus + 1) + 6, "Rend first (5), then First Blood's +1 to both");
    }

    private static void RendWithHonorNeverFalls()
    {
        // Player 0 focuses Rend the Soul and Honor Never Falls (Focus 3): 3 + 5 + 3 = 11 against Ichizo's 6 (+ his deck focus, at most a handful): he wins, loses instead, and Honor Never Falls
        // (which delays the owner's own losing consequences) now sees his loss -- his destruction is delayed to the end of the turn; Ichizo is destroyed at once.
        var (e, ichizo, target, _) = Duel(6, 3, [Rend, HonorNeverFalls]);
        var result = e.State.LastDuel!.Result!;
        Eq(result.ChallengedTotal, 3 + 5 + CardCatalog.Get(HonorNeverFalls).Focus, "his total");
        Eq(result.WinnerId, null, "he won, so he loses instead");
        Eq(result.LoserIds.Length, 2, "both lost");
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "Honor Never Falls delays the Rend owner's own destruction");
        Check(e.State.DelayedEffects.Any(d => d.Kind == DelayedKinds.Destroy && d.CardId == target.Id && d.Until == DelayUntil.EndOfTurn), "recorded until the end of the turn");
        Eq(e.State.Cards[ichizo.Id].Zone, Zone.Dead, "the other loser is destroyed at once");
    }

    // ---- Rend the Soul, Battle ------------------------------------------------------------------------------------------------------------------------------

    private static void PassTwice(MatchEngine e) { Accept(e, PassC(e)); Accept(e, PassC(e)); }

    // Player 1 attacks province 0 of player 0 with `attackers`; player 0 defends with `defenders`. Player 1 holds Rend the Soul. Priority to player 1 (the Attacker's action).
    private static (MatchEngine E, CardInstance Marked, CardInstance[] Attackers, CardInstance[] Defenders, CardInstance Rend) RendBattle(int markedForce, int otherAttackForce, int defenseForce)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var marked = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(marked, markedForce - 4);
        var attackers = new List<CardInstance> { marked };
        if (otherAttackForce > 0) { var other = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(other, otherAttackForce - 4); attackers.Add(other); }
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(defender, defenseForce - 3);
        var rend = Add(s, Rend, 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), marked, attackers.ToArray(), [defender], rend);
    }

    private static void RendBattleTie()
    {
        var (e, marked, attackers, defenders, rend) = RendBattle(4, 0, 6);
        var force = e.CardForceForTest(marked.Id);
        Accept(e, Enabled(e, 1, "rend-the-soul-battle", rend.Id, marked.Id).Command!);
        Eq(e.CardForceForTest(marked.Id), force + 5, "+5F: 9 against 6, his army would win");
        var honor1 = Honor(e, 1); var honor0 = Honor(e, 0);
        PassTwice(e);
        Check(Logged(e, "battleTiedInstead"), "the battle is tied instead");
        Eq(e.State.Cards[marked.Id].Zone, Zone.Dead, "a tie: the attacking army is destroyed");
        Eq(e.State.Cards[defenders[0].Id].Zone, Zone.Dead, "and the defending army");
        Check(e.State.Players[0].Provinces[0].Destroyed == false, "no province is destroyed");
        Eq(Honor(e, 1), honor1 + 1, "tie Honor: 1 per enemy card destroyed");
        Eq(Honor(e, 0), honor0 + 1, "for both players");
        Check(e.State.Cards[rend.Id].Zone == Zone.FateDiscard, "Rend the Soul was spent");
    }

    private static void RendBattleEdges()
    {
        // Negative: even with +5F his army loses (4+5 = 9 against 20): the defenders win as usual.
        var (e, marked, _, defenders, rend) = RendBattle(4, 0, 20);
        Accept(e, Enabled(e, 1, "rend-the-soul-battle", rend.Id, marked.Id).Command!);
        PassTwice(e);
        Check(!Logged(e, "battleTiedInstead"), "no tie");
        Eq(e.State.Cards[marked.Id].Zone, Zone.Dead, "the attacking army lost and is destroyed");
        Check(e.State.Cards[defenders[0].Id].Zone != Zone.Dead, "the defender survives");
        // He is no longer in the winning army (sent home before the resolution): the rest of the army wins, but the marking Attacker destroys no province however large the margin.
        var (e2, marked2, attackers2, defenders2, rend2) = RendBattle(4, 30, 1);
        Accept(e2, Enabled(e2, 1, "rend-the-soul-battle", rend2.Id, marked2.Id).Command!);
        e2 = Edit(e2, st => { st.Cards[marked2.Id].Zone = Zone.Home; st.Cards[marked2.Id].ProvinceIndex = null; });
        PassTwice(e2);
        Check(!Logged(e2, "battleTiedInstead"), "the marked Personality is not in the winning army: no tie");
        Eq(e2.State.Cards[defenders2[0].Id].Zone, Zone.Dead, "the attackers won");
        Check(!e2.State.Players[0].Provinces[0].Destroyed, "but the marking Attacker will not destroy the province");
        Check(Logged(e2, "provinceDestructionBarred"), "logged");
        // A tie at zero with an empty side has no outcome at all: his army would win unopposed at resolution (the defender left), so nothing is destroyed.
        var (e4, marked4, _, defenders4, rend4) = RendBattle(4, 0, 1);
        Accept(e4, Enabled(e4, 1, "rend-the-soul-battle", rend4.Id, marked4.Id).Command!);
        e4 = Edit(e4, st => { st.Cards[defenders4[0].Id].Zone = Zone.Home; st.Cards[defenders4[0].Id].ProvinceIndex = null; });
        PassTwice(e4);
        Check(Logged(e4, "battleTiedInstead"), "tied instead");
        Check(e4.State.Cards[marked4.Id].Zone != Zone.Dead && !e4.State.Players[0].Provinces[0].Destroyed, "a tie with an empty side: no outcome, no destruction");
        // "Your performing OPPOSED Personality": with no enemy unit at the battlefield it cannot be taken.
        var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = 1;
        var lone = Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var rend5 = Add(s5, Rend, 1, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(s5), 1, "rend-the-soul-battle", rend5.Id, lone.Id), "an unopposed Personality is not a legal performer");
        // Control: the same margin without Rend the Soul destroys the province.
        var (e3, _, _, defenders3, _) = RendBattle(4, 30, 1);
        Accept(e3, PassC(e3)); PassTwice(e3);
        Eq(e3.State.Cards[defenders3[0].Id].Zone, Zone.Dead, "control: the attackers won");
        Check(e3.State.Players[0].Provinces[0].Destroyed, "control: the province is destroyed");
    }

    // ---- No Hiding Place ------------------------------------------------------------------------------------------------------------------------------------

    // Player 1 (the Attacker) has a Ranged 4 Attack Personality at the battlefield; player 0 (the Defender) has a defender there and `homeForce` Personality at home. Priority to player 1.
    private static (MatchEngine E, CardInstance Archer, CardInstance AtHome, CardInstance? Hiding, string Ability) Archery(int homeForce, bool withHiding = true, Action<MatchState>? more = null, string ability = "shinjo-eun-sahng-attack")
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var archer = Add(s, DefinitionWith(ability), 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var atHome = Add(s, "training-crane-samurai", 0, Zone.Home); Force(atHome, homeForce - 3);
        var hiding = withHiding ? Add(s, NoHiding, 1, Zone.Hand) : null;
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), archer, atHome, hiding, ability);
    }

    private static void NoHidingPlaceHome()
    {
        var (e, archer, atHome, hiding, ability) = Archery(3);
        var announce = Enabled(e, 1, ability, archer.Id).Command! with { TargetId = null };
        Accept(e, announce);
        Eq(e.State.PendingChoice?.Type, "announceReaction", "announcing a Ranged Attack opens the announce window");
        var offered = Options(e, 1, "no-hiding-place", hiding!.Id).Where(o => o.Enabled).ToArray();
        Check(offered.Length == 1, "No Hiding Place is offered to its player: " + string.Join("; ", e.QueryActions(1).Select(a => a.Label)));
        Accept(e, offered[0].Command!);
        Check(Logged(e, "announcedAttackModified"), "recorded on the announced action");
        for (var guard = 0; e.State.PendingChoice is { Type: "announceReaction" } && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "attackTargetSelect", "the Attack's target is selected after the announce window");
        var targets = Actions(e, 1, "chooseAttackTarget").Where(c => c.Choice != "none").Select(c => c.TargetId).ToArray();
        Check(targets.Contains(atHome.Id), "a Personality without Followers in the Defender's home is a legal target now");
        Check(Actions(e, 1, "chooseAttackTarget").Any(c => c.Choice == "none"), "no target at all is also offered");
        Accept(e, Actions(e, 1, "chooseAttackTarget").Single(c => c.TargetId == atHome.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 6; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[atHome.Id].Zone, Zone.Dead, "Ranged 4 - 1 = 3 against Force 3: destroyed in the Defender's home");
        Eq(e.State.Cards[hiding.Id].Zone, Zone.FateDiscard, "No Hiding Place was spent");
        Check(e.State.AnnouncedAttackModifiers.Count == 0, "the record was released once the Attack resolved");
    }

    private static void NoHidingPlaceEdges()
    {
        // Without it: a card at home is never a legal Attack target, and announcing opens no window (nothing to react with).
        var (e, archer, atHome, _, ability) = Archery(3, withHiding: false);
        Check(!(Options(e, 1, ability, archer.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Contains(atHome.Id)), "not offered");
        Check(!e.Execute(Enabled(e, 1, ability, archer.Id).Command! with { TargetId = atHome.Id }).Accepted, "a hand-made command is refused");
        Accept(e, Enabled(e, 1, ability, archer.Id).Command! with { TargetId = null });
        Check(e.State.PendingChoice?.Type != "announceReaction", "no copy in hand: no announce window");
        // The -1 strength makes the Attack miss: Force 4 at home against Ranged 4 - 1.
        var (m, archer2, strong, hiding2, ability2) = Archery(4);
        Accept(m, Enabled(m, 1, ability2, archer2.Id).Command! with { TargetId = null });
        Accept(m, Options(m, 1, "no-hiding-place", hiding2!.Id).First(o => o.Enabled).Command!);
        for (var guard = 0; m.State.PendingChoice is { Type: "announceReaction" } && guard < 4; guard++) Accept(m, new GameCommand("declineReaction", m.State.PriorityPlayerId, m.Version));
        Accept(m, Actions(m, 1, "chooseAttackTarget").Single(c => c.TargetId == strong.Id));
        for (var guard = 0; m.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 6; guard++) Accept(m, new GameCommand("declineReaction", m.State.PriorityPlayerId, m.Version));
        Eq(m.State.Cards[strong.Id].Zone, Zone.Home, "strength 3 against Force 4: he survives");
        // The Attacker's own home, and a Personality with a Follower in the Defender's home, are not offered.
        CardInstance? own = null, followed = null;
        var (f, archer3, _, hiding3, ability3) = Archery(1, more: st =>
        {
            own = Add(st, "training-lion-samurai", 1, Zone.Home);
            followed = Add(st, "training-crane-samurai", 0, Zone.Home);
            Add(st, "training-follower", 0, Zone.Attached, parent: followed.Id);
        });
        Accept(f, Enabled(f, 1, ability3, archer3.Id).Command! with { TargetId = null });
        Accept(f, Options(f, 1, "no-hiding-place", hiding3!.Id).First(o => o.Enabled).Command!);
        for (var guard = 0; f.State.PendingChoice is { Type: "announceReaction" } && guard < 4; guard++) Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        var targets = Actions(f, 1, "chooseAttackTarget").Select(c => c.TargetId).ToArray();
        Check(!targets.Contains(own!.Id), "the Attacker's own home is not the Defender's");
        Check(!targets.Contains(followed!.Id), "a Personality with a Follower is still immune (if otherwise legal)");
        // A Melee Attack (a Weapon's): no window, No Hiding Place not offered.
        CardInstance? weapon = null;
        var (g, _, _, hiding4, _) = Archery(1, more: st =>
        {
            var swordsman = Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            weapon = Add(st, "training-reserve-item", 1, Zone.Attached, province: 0, parent: swordsman.Id);
        });
        Accept(g, Enabled(g, 1, "training-weapon-attack", weapon!.Id).Command! with { TargetId = null });
        Check(g.State.PendingChoice?.Type != "announceReaction" && !CanUse(g, 1, "no-hiding-place", hiding4!.Id), "a Melee Attack gets nothing");
    }
}
