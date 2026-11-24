using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Personality pass (issue #477), batch 3: the 12 of the batch's 15 candidate cards wired in personality-wave-70, driven through QueryActions and the
// commands it offers on explicit positions of the economy table (player 0 the Crane, player 1 the Lion). The other three (Shinjo Ki-Chang - exp, Kitsune Denhei, Kitsune Gina)
// each create a Proxy card whose scan is not in the scan pack, so they stay unwired and unpublished (tools/content/ee_batch3_blockers.json).
internal static class EmperorPersonalityBatch3Tests
{
    private const string Naleesh = "oracle-l5r-5354", Shigeru = "oracle-l5r-5370", Tetsuo = "oracle-l5r-5382", Itao = "oracle-l5r-6895", Ryoko = "oracle-l5r-9094", Shunshi = "oracle-l5r-4002",
        Samuru = "oracle-l5r-8874", Tomaru = "oracle-l5r-8893", Unmei = "oracle-l5r-8995", Hama = "oracle-l5r-9552", Hiromi = "oracle-l5r-9558", Naoto = "oracle-l5r-9611",
        Tsujiken = "oracle-l5r-10045", WaterBo = "oracle-l5r-10026";
    private static readonly string[] Wired = [Naleesh, Shigeru, Tetsuo, Itao, Ryoko, Shunshi, Samuru, Tomaru, Unmei, Hama, Hiromi, Naoto];
    private static readonly string[] Unwired = []; // issue #492: Ki-Chang (6905) and Gina (4460) are wired in personality-wave-77 (proxy group Y), Denhei (4456) in personality-wave-78 (proxy group Z)

    public static void Run(TestRunner runner) => runner.Run("emperor-personality-batch3", [
        ("Published shape: personality-wave-70 publishes exactly the 12 wired cards with their printed stats, keywords, Effect and ability ids, ships their art and lists them in its manifest; the three unwired cards are not in the catalogue", PublishedShape),
        ("Tsuruchi Tomaru: Ranged 3 Attack twice per turn, Force 4 is a legal target that survives, never a third time", TomaruAttack),
        ("Tsuruchi Samuru: Ranged 6 Attack compared against Gold Cost -- a high-Force cheap Personality dies, a costly one survives, a Personality with a Follower is not a target", SamuruAttack),
        ("Moto Tetsuo: only with a Cavalry unit of his controller's at the battlefield; moves him there and then Ranged 4 Attacks -- no Attack when he was already there", TetsuoMoveAttack),
        ("Shinjo Itao: after entering play attaches a Follower from the hand, paying its cost; an Item in the hand is not offered", ItaoAttach),
        ("Utaku Ryoko: after engaging at her battlefield gives an opposing Personality or Follower a Force penalty equal to her Personal Honor; not from home", RyokoPenalty),
        ("Iuchi Shunshi: an Open lock on a Personality without attachments -- assignment and movement to battlefields, this turn", ShunshiLock),
        ("Iuchi Shunshi: only a Personality without attachments -- any attached Follower, Item or Spell, or an attachment token, on either side, takes him off the target list, and a command naming him is refused", ShunshiOnlyBarePersonalities),
        ("Unmei: takes a keyword from another Mantis Clan Personality of her controller's with it, gets +3F and the Ranged 5 Attack while Samurai or Shugenja", UnmeiMirror),
        ("Yoritomo Hama: bows or straightens a card with lower Gold Cost than hers in a unit, one use for both", HamaToggle),
        ("Yoritomo Hiromi: destroys an enemy card or unit with a lower total Gold Cost than her unit; the destruction and bowing from her Battle actions are not negated", HiromiThunder),
        ("Yoritomo Naoto: abilities on the cards in an enemy unit may not be used until the battle ends", NaotoLock),
        ("Moto Naleesh: bows the unit and destroys its cards without attachments and without Cavalry; his unit straightens in the other player's Straighten Phase", NaleeshOverrun),
        ("Moto Shigeru: +1F with an Item; a Limited action targeting your Personality is negated at the price of Personal Honor, and seppuku at 0", ShigeruNegate),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static CardInstance Follow(MatchState s, CardInstance host, string def = "training-follower") => Add(s, def, host.OwnerId, Zone.Attached, province: host.ProvinceIndex, parent: host.Id);
    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void PassTo(MatchEngine e, int player) => Accept(e, new GameCommand("pass", player, e.Version));
    private static bool Gone(MatchEngine e, CardInstance card) => e.State.Cards[card.Id].Zone is Zone.Dead or Zone.FateDiscard or Zone.DynastyDiscard;

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, int ph, string keywords, string effect, string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph
                    && Keywords(id) == keywords && c.Effect == effect,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} keywords={Keywords(id)} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Stats(Naleesh, "Moto Naleesh", "Unicorn", 6, 5, 13, 4, "Cavalry,Clan Champion,Commander,Kami,Loyal,Naga,Paragon,Samurai,Tactician,Unicorn Clan,Unique", "naleesh-straightens", ["naleesh-overrun"]);
        Stats(Shigeru, "Moto Shigeru", "Unicorn", 3, 3, 8, 2, "Cavalry,Samurai,Soul of Moto Gurban,Unicorn Clan", "shigeru-item-force", ["shigeru-negate"]);
        Stats(Tetsuo, "Moto Tetsuo", "Unicorn", 4, 2, 8, 2, "Samurai,Soul of Moto Shanyu,Unicorn Clan,White Guard", "none", ["tetsuo-move-attack"]);
        Stats(Itao, "Shinjo Itao", "Unicorn", 3, 3, 7, 3, "Cavalry,Samurai,Soul of Shinjo Goshi,Unicorn Clan", "none", ["itao-attach"]);
        Stats(Ryoko, "Utaku Ryoko", "Unicorn", 4, 2, 8, 3, "Battle Maiden,Cavalry,Paragon,Samurai,Soul of Utaku Liu-Xeung,Unicorn Clan", "none", ["ryoko-penalty"]);
        Stats(Shunshi, "Iuchi Shunshi", "Unicorn", 4, 3, 11, 3, "Doomseeker,Shugenja,Soul of Iuchi Karasu Experienced 2,Unicorn Clan,Unique", "none", ["shunshi-lock"]);
        Stats(Samuru, "Tsuruchi Samuru", "Mantis", 4, 4, 7, 3, "Magistrate,Mantis Clan,Samurai,Soul of Tsuruchi Mochisa Experienced,Unique,Yojimbo", "none", ["samuru-attack"]);
        Stats(Tomaru, "Tsuruchi Tomaru", "Mantis", 4, 3, 7, 2, "Mantis Clan,Samurai,Scout", "none", ["tomaru-attack-3"]);
        Stats(Unmei, "Unmei", "Mantis", 2, 3, 8, 1, "Mantis Clan,Nonhuman,Shapeshifter,Spirit,Unique", "unmei-mirror", []);
        Stats(Hama, "Yoritomo Hama", "Mantis", 4, 4, 8, 2, "Extortionist,Magistrate,Mantis Clan,Naval,Samurai,Soul of Yoritomo Manzo", "none", ["hama-bow", "hama-straighten"]);
        Stats(Hiromi, "Yoritomo Hiromi", "Mantis", 8, 5, 13, 3, "Clan Champion,Loyal,Mantis Clan,Naval,Samurai,Thunder,Unique", "hiromi-unnegatable", ["hiromi-thunder"]);
        Stats(Naoto, "Yoritomo Naoto", "Mantis", 6, 3, 9, 2, "Mantis Clan,Naval,Samurai,Soul of Yoritomo Isoshi", "none", ["naoto-lock"]);

        // The wave publishes exactly the wired cards (the personality-wave-63 blank-card lesson): no unwired card, and no card without an ability or an Effect.
        Check(PublishedContent.PersonalityWave70.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)),
            "personality-wave-70 is exactly the wired cards: " + string.Join(",", PublishedContent.PersonalityWave70.Cards.Select(c => c.Id)));
        Check(PublishedContent.PersonalityWave70.Cards.All(c => (c.Abilities ?? []).Length > 0 || c.Effect != "none"), "no card of the wave is blank");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-70.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)), "the manifest pins exactly the wired cards");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and unpublished");
        foreach (var abilityId in Wired.SelectMany(id => CardCatalog.Get(id).Abilities ?? []).Append("unmei-attack")) Check(AbilityCatalog.All.ContainsKey(abilityId), $"{abilityId} is catalogued");
    }

    private static void TomaruAttack()
    {
        var s = Battle(); var tomaru = Bf(s, Tomaru, 0);
        var a = Bf(s, "training-crane-samurai", 1); var b = Bf(s, "training-crane-samurai", 1); var c = Bf(s, "training-crane-samurai", 1);
        var tough = Bf(s, "training-lion-samurai", 1); // Force 4
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "tomaru-attack-3", tomaru.Id).TargetIds!;
        Check(targets.Contains(a.Id) && targets.Contains(tough.Id), "Force 3 is within a Ranged 3 Attack, and Force 4 is still a legal target");
        var e0 = MatchEngine.FromTestState(s);
        Accept(e0, Offer(e0, 0, "tomaru-attack-3", tomaru.Id, tough.Id));
        Check(!Gone(e0, tough), "but a Force 4 target survives the Ranged 3 Attack");
        Accept(e, Offer(e, 0, "tomaru-attack-3", tomaru.Id, a.Id));
        Check(Gone(e, a), "the first Attack destroyed its target");
        PassTo(e, 1);
        Accept(e, Offer(e, 0, "tomaru-attack-3", tomaru.Id, b.Id));
        Check(Gone(e, b), "the second Attack of the turn destroyed its target");
        PassTo(e, 1);
        Check(!CanUse(e, 0, "tomaru-attack-3", tomaru.Id), "twice per turn: not a third time");
        Check(!Gone(e, c) && !Gone(e, tough), "the others were not touched");
    }

    private static void SamuruAttack()
    {
        var s = Battle(); var samuru = Bf(s, Samuru, 0);
        var cheap = Bf(s, "training-lion-samurai", 1); Force(cheap, 5); // Force 9, Gold Cost 5
        var dear = Bf(s, "training-lion-veteran", 1); // Force 6, Gold Cost 8
        var host = Bf(s, "training-crane-veteran", 1); var follower = Follow(s, host); // the Follower: Force 2, Gold Cost 2
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "samuru-attack", samuru.Id).TargetIds!;
        Check(targets.Contains(cheap.Id), "Gold Cost 5 is within a Ranged 6 Attack though its Force is 9");
        Check(targets.Contains(dear.Id), "Gold Cost 8 is out of reach though its Force is 6, but still a legal target");
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Offer(e3, 0, "samuru-attack", samuru.Id, dear.Id));
        Check(!Gone(e3, dear), "Gold Cost 8 survives the Ranged 6 Attack though its Force is only 6");
        Check(targets.Contains(follower.Id) && !targets.Contains(host.Id), "a Follower is a target, a Personality with a Follower is not");
        Accept(e, Offer(e, 0, "samuru-attack", samuru.Id, cheap.Id));
        Check(Gone(e, cheap), "destroyed by comparing his Gold Cost");
        PassTo(e, 1);
        // The Follower: Gold Cost 2 and Force 2 -- destroyed the same way (the once-per-turn use is spent, so another engine takes it).
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 0, "samuru-attack", samuru.Id, follower.Id));
        Check(Gone(e2, follower), "the Follower was destroyed");
    }

    private static void TetsuoMoveAttack()
    {
        // A Cavalry unit of his controller's at the battlefield: he moves there, then Ranged 4 Attacks.
        var s = Battle(); var tetsuo = Add(s, Tetsuo, 0, Zone.Home);
        var cavalry = Bf(s, "training-crane-samurai", 0); GiveKeyword(cavalry, "Cavalry");
        var enemy = Bf(s, "training-crane-samurai", 1); var tough = Bf(s, "training-lion-veteran", 1);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "tetsuo-move-attack", tetsuo.Id).TargetIds!.Contains(tough.Id), "Force 6 is out of a Ranged 4 Attack's reach but still a legal target");
        var e1 = MatchEngine.FromTestState(s);
        Accept(e1, Offer(e1, 0, "tetsuo-move-attack", tetsuo.Id, tough.Id));
        Check(e1.State.Cards[tetsuo.Id].Zone == Zone.Battlefield && !Gone(e1, tough), "he moves, and the Force 6 target survives the Ranged 4 Attack");
        Accept(e, Offer(e, 0, "tetsuo-move-attack", tetsuo.Id, enemy.Id));
        Check(e.State.Cards[tetsuo.Id].Zone == Zone.Battlefield && e.State.Cards[tetsuo.Id].ProvinceIndex == 0, "Tetsuo moved to the battlefield");
        Check(Gone(e, enemy), "and his Ranged 4 Attack destroyed the Force 3 target");
        // No Cavalry unit at the battlefield: the action is not available.
        var none = Battle(); var t2 = Add(none, Tetsuo, 0, Zone.Home); Bf(none, "training-crane-samurai", 0);
        var ne = MatchEngine.FromTestState(none);
        Check(!CanUse(ne, 0, "tetsuo-move-attack", t2.Id), "without a Cavalry unit at the battlefield the action is refused");
        // A Cavalry Personality with a Follower that is not Cavalry is not a Cavalry unit.
        var mixed = Battle(); var t3 = Add(mixed, Tetsuo, 0, Zone.Home); var rider = Bf(mixed, "training-crane-samurai", 0); GiveKeyword(rider, "Cavalry"); Follow(mixed, rider);
        var me = MatchEngine.FromTestState(mixed);
        Check(!CanUse(me, 0, "tetsuo-move-attack", t3.Id), "one non-Cavalry Follower breaks a Cavalry unit");
        // Already at the battlefield: he does not move, so there is no Attack.
        var there = Battle(); var t4 = Bf(there, Tetsuo, 0); var rider2 = Bf(there, "training-crane-samurai", 0); GiveKeyword(rider2, "Cavalry"); var target = Bf(there, "training-crane-samurai", 1);
        var te = MatchEngine.FromTestState(there);
        Accept(te, Offer(te, 0, "tetsuo-move-attack", t4.Id, target.Id));
        Check(!Gone(te, target), "he did not move (already there), so the Ranged Attack did not happen");
    }

    private static (MatchEngine Engine, string CardId) Recruit(string definitionId, Action<MatchState>? arrange = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        arrange?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static void ItaoAttach()
    {
        CardInstance follower = null!, item = null!, other = null!;
        var (e, itaoId) = Recruit(Itao, s => { follower = Add(s, "training-follower", 0, Zone.Hand); item = Add(s, "training-item", 0, Zone.Hand); other = Add(s, "training-follower", 1, Zone.Hand); });
        Eq(e.State.PendingChoice?.Type, "entryReaction", "his entry window is open");
        var targets = Enabled(e, 0, "itao-attach", itaoId).TargetIds!;
        Check(targets.Contains(follower.Id) && !targets.Contains(item.Id) && !targets.Contains(other.Id), "a Follower in your hand only: " + string.Join(",", targets));
        var gold = Enabled(e, 0, "itao-attach", itaoId).Gold;
        Eq(gold?.Cost, CardCatalog.Get("training-follower").GoldCost, "paying all costs");
        Accept(e, Offer(e, 0, "itao-attach", itaoId, follower.Id));
        Check(e.State.Cards[follower.Id].Zone == Zone.Attached && e.State.Cards[follower.Id].AttachedTo == itaoId, "the Follower is attached to Itao");
        Check(!e.State.Players[0].Hand.Contains(follower.Id), "and left the hand");
        // Nothing to attach: no Follower in the hand, so the Reaction is not available.
        var (bare, bareId) = Recruit(Itao, s => { foreach (var id in s.Players[0].Hand.ToArray()) s.Cards.Remove(id); s.Players[0].Hand.Clear(); s.Cards.Add("fx-item-only", new CardInstance { Id = "fx-item-only", DefinitionId = "training-item", OwnerId = 0, Zone = Zone.Hand, FaceUp = false }); s.Players[0].Hand.Add("fx-item-only"); });
        Check(!CanUse(bare, 0, "itao-attach", bareId), "no Follower in hand: nothing to attach");
    }

    private static void RyokoPenalty()
    {
        MatchState Engage(Action<MatchState> arrange)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            arrange(s);
            return s;
        }
        CardInstance ryoko = null!, enemy = null!, follower = null!, ally = null!;
        var s0 = Engage(s => { ryoko = Add(s, Ryoko, 0, Zone.Battlefield, province: 0); ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var host = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); follower = Follow(s, host); });
        var e = MatchEngine.FromTestState(s0);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        var targets = Enabled(e, 0, "ryoko-penalty", ryoko.Id).TargetIds!;
        Check(targets.Contains(enemy.Id) && targets.Contains(follower.Id) && !targets.Contains(ally.Id), "an opposing Personality or Follower, not her own side: " + string.Join(",", targets));
        var force = e.CardForceForTest(enemy.Id);
        Accept(e, Offer(e, 0, "ryoko-penalty", ryoko.Id, enemy.Id));
        Eq(e.CardForceForTest(enemy.Id), Math.Max(0, force - CardCatalog.Get(Ryoko).PersonalHonor), "a Force penalty equal to her Personal Honor (3)");
        // The Follower (Force 2) goes to 0.
        CardInstance ryoko2 = null!;
        var e2 = MatchEngine.FromTestState(Engage(s => { ryoko2 = Add(s, Ryoko, 0, Zone.Battlefield, province: 0); var host = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); follower = Follow(s, host); }));
        Accept(e2, new GameCommand("selectBattle", 0, e2.Version, Province: 0));
        Accept(e2, Offer(e2, 0, "ryoko-penalty", ryoko2.Id, follower.Id));
        Eq(e2.CardForceForTest(follower.Id), 0, "the Follower's Force went to 0");
        // A higher Personal Honor is a bigger penalty; and from home (not at the battlefield) she has no reaction.
        var e3 = MatchEngine.FromTestState(Engage(s => { Add(s, Ryoko, 0, Zone.Home); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); }));
        Accept(e3, new GameCommand("selectBattle", 0, e3.Version, Province: 0));
        Check(!e3.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "ryoko-penalty"), "Ryoko at home does not react to an engagement at a battlefield she is not at");
    }

    private static void ShunshiLock()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shunshi = Add(s, Shunshi, 0, Zone.Home);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Home);
        var followed = Add(s, "training-lion-veteran", 1, Zone.Home); Follow(s, followed);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "shunshi-lock", shunshi.Id).TargetIds!;
        Check(targets.Contains(bare.Id) && !targets.Contains(followed.Id), "a Personality without attachments only: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "shunshi-lock", shunshi.Id, bare.Id));
        var locks = e.State.Cards[bare.Id].MovementLocks;
        Check(locks.Count == 1 && locks[0].Turn == e.State.TurnNumber && locks[0].Kinds.HasFlag(MovementKinds.Assign) && locks[0].Kinds.HasFlag(MovementKinds.ToBattlefield),
            "he may not be assigned, and will not move, to battlefields this turn");
        Check(e.State.Cards[followed.Id].MovementLocks.Count == 0, "the other was not touched");
    }

    private static void ShunshiOnlyBarePersonalities()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shunshi = Add(s, Shunshi, 0, Zone.Home);
        var bare = new[] { Add(s, "training-lion-samurai", 1, Zone.Home), Add(s, "training-crane-samurai", 0, Zone.Home) };
        var covered = new List<(CardInstance Card, string What)>();
        foreach (var owner in new[] { 0, 1 })
        {
            var follower = Add(s, "training-crane-veteran", owner, Zone.Home); Follow(s, follower); covered.Add((follower, $"player {owner}'s, with a Follower"));
            var item = Add(s, "training-crane-veteran", owner, Zone.Home); Add(s, "training-item", owner, Zone.Attached, parent: item.Id); covered.Add((item, $"player {owner}'s, with an Item"));
            var spell = Add(s, "training-crane-veteran", owner, Zone.Home); spell.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent));
            Add(s, "training-bow-spell", owner, Zone.Attached, parent: spell.Id); covered.Add((spell, $"player {owner}'s, with a Spell"));
            foreach (var token in new[] { "Follower", "Weapon", "Armor" })
            {
                var tokened = Add(s, "training-crane-veteran", owner, Zone.Home); tokened.Tokens.Add(new($"tok-{tokened.Id}", token, "test", "Force", 1));
                covered.Add((tokened, $"player {owner}'s, with a {token} token"));
            }
        }
        var forceToken = Add(s, "training-lion-veteran", 1, Zone.Home); forceToken.Tokens.Add(new("tok-force", "Force", "test", "Force", 1)); // a plain +F token is not an attachment
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "shunshi-lock", shunshi.Id).TargetIds!;
        foreach (var b in bare) Check(targets.Contains(b.Id), $"{b.Id}, without attachments, is a target");
        Check(targets.Contains(forceToken.Id), "a Personality with only a Force token has no attachment and is a target");
        Check(targets.Contains(shunshi.Id), "Shunshi himself, without attachments, is a target");
        foreach (var (card, what) in covered)
        {
            Check(!targets.Contains(card.Id), $"{what} is not a target");
            var refused = Batch336Support.Rejection(e, new GameCommand("useAbility", 0, e.Version, CardId: shunshi.Id, TargetId: card.Id, AbilityId: "shunshi-lock"));
            Check(!string.IsNullOrEmpty(refused), $"{what}: a command naming him is refused");
            Check(e.State.Cards[card.Id].MovementLocks.Count == 0, $"{what}: and is not locked");
        }
        Check(targets.All(id => e.State.Cards[id].Zone != Zone.Attached && CardCatalog.Get(e.State.Cards[id].DefinitionId).Type == CardType.Personality), "only Personalities are offered");
    }

    private static void UnmeiMirror()
    {
        // Another Mantis Clan Personality with Samurai and Scout (Tsuruchi Tomaru): Unmei has both, +3F, and the Ranged 5 Attack.
        var s = Battle(); var unmei = Bf(s, Unmei, 0); Bf(s, Tomaru, 0); var enemy = Bf(s, "training-lion-samurai", 1); Force(enemy, 1); // Force 5
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(unmei.Id, "Samurai") && e.HasKeyword(unmei.Id, "Scout"), "Samurai and Scout, from Tomaru");
        Check(!e.HasKeyword(unmei.Id, "Shugenja") && !e.HasKeyword(unmei.Id, "Naval") && !e.HasKeyword(unmei.Id, "Cavalry"), "no keyword nobody else of hers has");
        Eq(e.CardForceForTest(unmei.Id), CardCatalog.Get(Unmei).Force + 3, "+3F while she has Samurai");
        Accept(e, Offer(e, 0, "unmei-attack", unmei.Id, enemy.Id));
        Check(e.State.Cards[unmei.Id].Bowed, "she bowed as the cost");
        Check(Gone(e, enemy), "the Ranged 5 Attack destroyed the Force 5 target");
        // Nobody to mirror: no keywords, no bonus, no ability.
        var alone = Battle(); var u2 = Bf(alone, Unmei, 0); Bf(alone, "training-crane-samurai", 0);
        var ae = MatchEngine.FromTestState(alone);
        Check(!ae.HasKeyword(u2.Id, "Samurai") && ae.CardForceForTest(u2.Id) == CardCatalog.Get(Unmei).Force, "alone she has no keyword and no +3F");
        Check(!CanUse(ae, 0, "unmei-attack", u2.Id), "and no Attack");
        // The other Mantis Personality must be controlled by her controller (not the opponent's), and must have the keyword itself.
        var foe = Battle(); var u3 = Bf(foe, Unmei, 0); Bf(foe, Tomaru, 1);
        Check(!MatchEngine.FromTestState(foe).HasKeyword(u3.Id, "Samurai"), "an enemy Mantis Personality gives her nothing");
        // When the other Personality leaves, she loses it again.
        var gone = MatchEngine.FromTestState(s);
        var tomaru = gone.State.Cards.Values.Single(c => c.DefinitionId == Tomaru);
        var lost = Edit(gone, st => st.Cards[tomaru.Id].Zone = Zone.Dead);
        Check(!lost.HasKeyword(unmei.Id, "Samurai") && lost.CardForceForTest(unmei.Id) == CardCatalog.Get(Unmei).Force, "with Tomaru dead she is plain again");
        // Shugenja counts as well as Samurai for the bonus (a Mantis Shugenja fixture).
        var shu = Battle(); var u4 = Bf(shu, Unmei, 0); var kitsune = Bf(shu, "training-crane-samurai", 0); kitsune.DefinitionId = "training-shugenja";
        GiveKeyword(kitsune, "Mantis Clan");
        var se = MatchEngine.FromTestState(shu);
        Check(se.State.Cards[kitsune.Id].DefinitionId == "training-shugenja" && !se.HasKeyword(u4.Id, "Shugenja"), "a Phoenix Clan Shugenja is not a Mantis Clan Personality for her");
    }

    private static void HamaToggle()
    {
        var s = Battle(); var hama = Bf(s, Hama, 0);
        var cheap = Bf(s, "training-crane-samurai", 1); var equal = Bf(s, "training-lion-veteran", 1); // Gold Cost 5 and 8 (Hama's is 8)
        var host = Bf(s, "training-crane-veteran", 1); var follower = Follow(s, host);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hama-bow", hama.Id).TargetIds!;
        Check(targets.Contains(cheap.Id) && targets.Contains(follower.Id), "a card in a unit with lower Gold Cost: a Personality or a Follower");
        Check(!targets.Contains(equal.Id) && !targets.Contains(host.Id), "an equal Gold Cost is not lower");
        Accept(e, Offer(e, 0, "hama-bow", hama.Id, cheap.Id));
        Check(e.State.Cards[cheap.Id].Bowed, "bowed");
        PassTo(e, 1);
        Check(!CanUse(e, 0, "hama-straighten", hama.Id), "bow or straighten is one use");
        // Straightening a bowed card (a Follower this time).
        var s2 = Battle(); var h2 = Bf(s2, Hama, 0); var host2 = Bf(s2, "training-crane-veteran", 1); var f2 = Follow(s2, host2); f2.Bowed = true;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "hama-straighten", h2.Id, f2.Id));
        Check(!e2.State.Cards[f2.Id].Bowed, "straightened");
    }

    private static void HiromiThunder()
    {
        var s = Battle(); var hiromi = Bf(s, Hiromi, 0); // Gold Cost 13
        var bare = Bf(s, "training-lion-samurai", 1); // 5
        var unit = Bf(s, "training-lion-veteran", 1); var f1 = Follow(s, unit); // 8 + 2 = 10
        var big = Bf(s, "training-crane-veteran", 1); Follow(s, big); Follow(s, big); var f3 = Follow(s, big); // 8 + 6 = 14
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hiromi-thunder", hiromi.Id).TargetIds!;
        Check(targets.Contains(bare.Id) && targets.Contains(unit.Id), "a bare card and a unit with a lower total Gold Cost");
        Check(!targets.Contains(big.Id), "a unit whose total Gold Cost (14) is not lower than her unit's (13)");
        Check(targets.Contains(f3.Id), "one of its Followers (a card without attachments, Gold Cost 2) is");
        Accept(e, Offer(e, 0, "hiromi-thunder", hiromi.Id, unit.Id));
        Check(Gone(e, unit) && Gone(e, f1), "the unit was destroyed with his Follower");
        // Her own unit's total counts: with a costly Item attached she cannot reach a card that she could before.
        var heavy = Battle(); var h2 = Bf(heavy, Hiromi, 0); Follow(heavy, h2, "training-item"); var t2 = Bf(heavy, "training-lion-veteran", 1); var pricey = Follow(heavy, t2); // 8 + 2 = 10 < 13 + 2
        var he = MatchEngine.FromTestState(heavy);
        Check(Enabled(he, 0, "hiromi-thunder", h2.Id).TargetIds!.Contains(t2.Id) && pricey.Zone == Zone.Attached, "her unit's total includes her attachments (15)");

        // "Will not be negated": a destruction shield (Megumi's Guidance) and a per-card negation do not stop her; they stop another Personality's destroy.
        var shield = Battle(); var h3 = Bf(shield, Hiromi, 0); var victim = Bf(shield, "training-lion-samurai", 1); victim.DestructionShieldTurn = shield.TurnNumber;
        shield.NegatedEffects.Add(new("destroy", victim.Id));
        var se = MatchEngine.FromTestState(shield);
        Accept(se, Offer(se, 0, "hiromi-thunder", h3.Id, victim.Id));
        Check(Gone(se, victim), "her destruction was neither shielded nor negated");
        var control = Battle(); var destroyer = Bf(control, "training-no-attachments-destroyer", 0); var v2 = Bf(control, "training-lion-samurai", 1); v2.DestructionShieldTurn = control.TurnNumber;
        var ce = MatchEngine.FromTestState(control);
        Accept(ce, Offer(ce, 0, "training-destroy-no-attachments", destroyer.Id, v2.Id));
        Check(!Gone(ce, v2), "control: the same shield stops another Personality's destroy action");

        // Her bowing (a Water Bo she wields) is not negated by Stalwart either; another wielder's is.
        var bow = Battle(); var h4 = Bf(bow, Hiromi, 0); Add(bow, WaterBo, 0, Zone.Attached, province: 0, parent: h4.Id);
        var stalwart = Bf(bow, "training-lion-samurai", 1); GiveKeyword(stalwart, "Stalwart");
        var be = MatchEngine.FromTestState(bow);
        Accept(be, Offer(be, 0, "water-bo-bow", be.State.Cards.Values.Single(c => c.DefinitionId == WaterBo).Id, stalwart.Id));
        Check(be.State.Cards[stalwart.Id].Bowed, "Hiromi's bow got through Stalwart");
        var plain = Battle(); var other = Bf(plain, "training-crane-samurai", 0); Add(plain, WaterBo, 0, Zone.Attached, province: 0, parent: other.Id);
        var stalwart2 = Bf(plain, "training-lion-samurai", 1); GiveKeyword(stalwart2, "Stalwart");
        var pe = MatchEngine.FromTestState(plain);
        Accept(pe, Offer(pe, 0, "water-bo-bow", pe.State.Cards.Values.Single(c => c.DefinitionId == WaterBo).Id, stalwart2.Id));
        Check(!pe.State.Cards[stalwart2.Id].Bowed, "control: another wielder's bow is negated by Stalwart");
    }

    private static void NaotoLock()
    {
        var s = Battle(0); s.PriorityPlayerId = 0;
        var naoto = Bf(s, Naoto, 0);
        var bower = Bf(s, "training-battle-bower", 1); var follower = Follow(s, bower);
        var bystander = Bf(s, "training-battle-bower", 1); var target = Bf(s, "training-crane-samurai", 0);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "naoto-lock", naoto.Id).TargetIds!.Contains(bower.Id), "an enemy unit is a target");
        Accept(e, Offer(e, 0, "naoto-lock", naoto.Id, bower.Id));
        var locked = e.State.BattleAbilityLocks.Select(l => l.CardId).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        Check(locked.SequenceEqual(new[] { bower.Id, follower.Id }.OrderBy(i => i, StringComparer.Ordinal)), "the Personality and his Follower, the cards now in the unit");
        Check(!CanUse(e, 1, "training-bow-at-battle", bower.Id), "his ability may not be used");
        Check(CanUse(e, 1, "training-bow-at-battle", bystander.Id), "a card outside the unit still can");
        Check(target.Id != bystander.Id);
        // The lock ends with the battle.
        RunUntil(e, () => e.State.BattleAbilityLocks.Count == 0);
        Eq(e.State.BattleAbilityLocks.Count, 0, "gone once the battle ended");
        // An action that would have been used before is available without the lock.
        var free = MatchEngine.FromTestState(s);
        Check(CanUse(free, 1, "training-bow-at-battle", bower.Id) || free.State.PriorityPlayerId == 0, "control: with no lock his ability is his own");
    }

    private static void NaleeshOverrun()
    {
        var s = Battle(); var naleesh = Bf(s, Naleesh, 0);
        var mounted = Bf(s, "training-lion-samurai", 1); GiveKeyword(mounted, "Cavalry");
        var cavalryFollower = Follow(s, mounted); GiveKeyword(cavalryFollower, "Cavalry");
        var footFollower = Follow(s, mounted);
        var item = Follow(s, mounted, "training-item");
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "naleesh-overrun", naleesh.Id).TargetIds!.Contains(mounted.Id), "an enemy unit is a target");
        Accept(e, Offer(e, 0, "naleesh-overrun", naleesh.Id, mounted.Id));
        Check(e.State.Cards[mounted.Id].Bowed && e.State.Cards[cavalryFollower.Id].Bowed && e.State.Cards[footFollower.Id].Bowed || Gone(e, footFollower), "the unit was bowed");
        Check(Gone(e, footFollower) && Gone(e, item), "the Follower and Item without Cavalry were destroyed");
        Check(!Gone(e, cavalryFollower), "the Cavalry Follower was not");
        Check(!Gone(e, mounted), "the Personality had attachments (and Cavalry): not destroyed");
        // A bare non-Cavalry Personality is destroyed; a bare Cavalry one is only bowed.
        var s2 = Battle(); var n2 = Bf(s2, Naleesh, 0); var foot = Bf(s2, "training-lion-samurai", 1); var rider = Bf(s2, "training-lion-samurai", 1); GiveKeyword(rider, "Cavalry");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "naleesh-overrun", n2.Id, foot.Id));
        Check(Gone(e2, foot), "a bare non-Cavalry Personality was destroyed");
        var e3 = MatchEngine.FromTestState(s2);
        Accept(e3, Offer(e3, 0, "naleesh-overrun", n2.Id, rider.Id));
        Check(e3.State.Cards[rider.Id].Bowed && !Gone(e3, rider), "a bare Cavalry Personality was only bowed");
        // His unit straightens in the other player's Straighten Phase; another Personality of his does not.
        var st = Table(Phase.Action, 0); st.PriorityPlayerId = 0;
        var nal = Add(st, Naleesh, 0, Zone.Home, bowed: true); var fol = Follow(st, nal); fol.Bowed = true; var stranger = Add(st, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var se = MatchEngine.FromTestState(st);
        RunUntil(se, () => se.State.ActivePlayerId == 1 && se.State.Phase == Phase.Action);
        Check(!se.State.Cards[nal.Id].Bowed && !se.State.Cards[fol.Id].Bowed, "his unit straightened in the other player's Straighten Phase");
        Check(se.State.Cards[stranger.Id].Bowed, "another Personality of his did not");
    }

    private static void ShigeruNegate()
    {
        // +1F while she has an Item (not a Follower).
        var f = Table(Phase.Action, 0); var sh = Add(f, Shigeru, 0, Zone.Home); var fe0 = MatchEngine.FromTestState(f);
        var baseForce = fe0.CardForceForTest(sh.Id);
        Eq(baseForce, CardCatalog.Get(Shigeru).Force, "no Item, no bonus");
        var withItem = Table(Phase.Action, 0); var sh2 = Add(withItem, Shigeru, 0, Zone.Home); Add(withItem, "training-item", 0, Zone.Attached, parent: sh2.Id);
        var itemForce = CardCatalog.Get("training-item").Force;
        Eq(MatchEngine.FromTestState(withItem).CardForceForTest(sh2.Id), baseForce + itemForce + 1, "+1F with an Item");
        var withFollower = Table(Phase.Action, 0); var sh3 = Add(withFollower, Shigeru, 0, Zone.Home); Follow(withFollower, sh3);
        Eq(MatchEngine.FromTestState(withFollower).CardForceForTest(sh3.Id), baseForce, "a Follower is not an Item: no +1F (and a Follower's Force is the unit's, not hers)");

        // The opponent's Limited action (Tsujiken's dishonor) targets a Personality of Shigeru's controller's: negated; two other Personalities take her PH (2) to 0: seppuku.
        CardInstance shigeru = null!, mine = null!, tsujiken = null!;
        MatchEngine Setup(int others)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1; s.Players[1].TurnsTaken = 3; s.Players[0].TurnsTaken = 3;
            shigeru = Add(s, Shigeru, 0, Zone.Home); mine = Add(s, "training-crane-samurai", 0, Zone.Home);
            for (var i = 1; i < others; i++) Add(s, "training-crane-veteran", 0, Zone.Home);
            tsujiken = Add(s, Tsujiken, 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        void React(MatchEngine e)
        {
            for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" }; guard++)
            {
                var who = e.State.PriorityPlayerId;
                var take = e.QueryActions(who).FirstOrDefault(x => x.Enabled && x.Command?.AbilityId == "shigeru-negate");
                Accept(e, take?.Command ?? e.QueryActions(who).First(x => x.Type == "declineReaction").Command!);
            }
        }
        var two = Setup(2);
        var honorBefore = Honor(two, 0);
        Accept(two, Offer(two, 1, "tsujiken-exp-dishonor", tsujiken.Id, mine.Id));
        Check(two.State.PendingChoice is { Type: "beforeActionReaction" }, "the Limited action is held for her reaction: " + two.State.PendingChoice?.Type);
        React(two);
        Check(!two.State.Cards[mine.Id].Dishonorable, "the action's effects were negated: not dishonored");
        Eq(Honor(two, 0), honorBefore, "and his controller lost no Honor");
        Check(Gone(two, shigeru), "two other Personalities: her Personal Honor (2) is now 0, she committed seppuku");
        // One other Personality: PH 2 - 1 = 1, she stays.
        var one = Setup(1);
        Accept(one, Offer(one, 1, "tsujiken-exp-dishonor", tsujiken.Id, mine.Id));
        React(one);
        Check(!one.State.Cards[mine.Id].Dishonorable && !Gone(one, shigeru), "one other Personality: negated, and she survives");
        Eq(one.PersonalHonorForTest(shigeru.Id), CardCatalog.Get(Shigeru).PersonalHonor - 1, "her Personal Honor was permanently reduced by 1");
        // A Battle action (not Limited) that targets her controller's Personality is not answered by her.
        var b = Table(Phase.Battle, 1); b.PriorityPlayerId = 1;
        Add(b, Shigeru, 0, Zone.Home); var target = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(b, "training-battle-bower", 1, Zone.Battlefield, province: 0); Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, "training-bow-at-battle", bower.Id, target.Id));
        Check(be.State.PendingChoice is null or { Type: not "beforeActionReaction" } || !be.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId == "shigeru-negate"), "a Battle action does not open her reaction");
        Check(be.State.Cards[target.Id].Bowed, "the Battle action resolved");
    }
}
