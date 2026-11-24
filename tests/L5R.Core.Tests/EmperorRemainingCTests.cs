using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group C: strategy-wave-60, driven through QueryActions and the commands it offers. Every scenario is an explicit position on the economy
// table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires.
internal static class EmperorRemainingCTests
{
    private const string Retribution = "oracle-l5r-6290", Rhetoric = "oracle-l5r-6312", SacrificeOfPawns = "oracle-l5r-6439", RockyTerrain = "oracle-l5r-6374", ShadowsWalk = "oracle-l5r-6676", StayPut = "oracle-l5r-7474", EmpressAddress = "oracle-l5r-8059", FiresOfWar = "oracle-l5r-8084", RumorsTravel = "oracle-l5r-6406", SelflessDefense = "oracle-l5r-6584", ShamefulInjury = "oracle-l5r-6698", SnakeTattoo = "oracle-l5r-7310", Sniping = "oracle-l5r-7312",
        SolidDefense = "oracle-l5r-7329", SpinningHeelKick = "oracle-l5r-7413", StrategicStrike = "oracle-l5r-7525", SummonedToJustice = "oracle-l5r-7639", SuperiorOpponent = "oracle-l5r-7654", TellTheTale = "oracle-l5r-7862",
        HeightOfCourage = "oracle-l5r-8126";
    private const string Incapacitated = "oracle-l5r-3730", OvercomeAdversity = "oracle-l5r-9918", WeakPoison = "oracle-l5r-99";

    // Every card of the group wired in this pass, in manifest order.
    private static readonly string[] Wired = [Retribution, Rhetoric, RockyTerrain, RumorsTravel, SacrificeOfPawns, SelflessDefense, ShadowsWalk, ShamefulInjury, SnakeTattoo, Sniping, SolidDefense, SpinningHeelKick, StayPut, StrategicStrike, SummonedToJustice, SuperiorOpponent, TellTheTale, EmpressAddress, FiresOfWar, HeightOfCourage];

    // The group's Strategies left unwired (each with a note in tools/content/ee_remC_blockers.json): never published.
    // Issue #492: Surprise Attack was wired in strategy-wave-62 (rulings pass R1); The Compassion of the Unicorn, The Courage of the Mantis and The Honesty of the Phoenix in strategy-wave-63 (rulings pass R2).
    private static readonly string[] Unwired = [];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static int ForceMods(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-C", [
        ("Published shape: strategy-wave-60 is exactly the wired Strategies, each with its scan's stats, keywords, text and ability ids, its art and its manifest entry; the unwired ones are not published", PublishedShape),
        ("Tell the Tale: a Limited action gaining 1 Honor, only on its player's own turn", TellTheTaleScenario),
        ("Strategic Strike: a performing Scout bows for a Ranged 6 Attack, which may target a Personality with Followers only with Reconnaissance", StrategicStrikeScenario),
        ("Sniping: after a Ranged Attack is targeted its strength doubles (a Melee Attack is not a target for it); a Reaction played from the hand and offered in the attack window", SnipingScenario),
        ("The Height of Courage: before another player's action moves your Personality, a Reaction from the hand negates that movement; its Battle half gives a Personality and his Followers +1F", HeightOfCourageScenario),
        ("Selfless Defense: a Yojimbo moves an enemy Personality home (bowed only with a Courtier or Shugenja controlled); a destroyed Yojimbo moves your Courtiers and Shugenja home", SelflessDefenseScenario),
        ("Summoned to Justice: a Courtier at any location moves an attacking Personality home and bows him, and a dishonorable one costs his controller his base Personal Honor", SummonedToJusticeScenario),
        ("Spinning Heel Kick: a Monk straightens and the additional Battle action granted must be performed by him", SpinningHeelKickScenario),
        ("Shameful Injury: Absent; negates the Force bonuses and penalties from actions on a Personality, current and new, and keeps a trait's", ShamefulInjuryScenario),
        ("Solid Defense: a Reaction negating another player's Honor losses -- free with a 5-Force Personality, else by discarding the Imperial Favor; its Battle half moves a bare Personality home", SolidDefenseScenario),
        ("Rhetoric: a Courtier bows to make a chosen player take another player's Honor gain or loss instead, when his Chi covers it", RhetoricScenario),
        ("Snake Tattoo: after another player's Battle action targets your Tattooed Personality a Reaction from the hand moves him to an adjacent battlefield and, since he is then no longer a legal target, negates its effects", SnakeTattooScenario),
        ("Superior Opponent: as a Focus Effect its player may destroy an attachment at the current battlefield if his duelist is there; its Battle half bows each Follower and Personality in an enemy unit with Force up to his Duelist's Chi", SuperiorOpponentScenario),
        ("Retribution: after an Attack Phase the Defender declares an additional one against the Attacker -- the roles swap for its whole length, only one Personality may be assigned and he and his Followers get +1F, and the turn's player is restored for the Dynasty Phase", RetributionScenario),
        ("Sacrifice of Pawns: a Courtier is destroyed as the cost; the target's controller may destroy the target, or else the acting player searches the Dynasty deck for a Personality and brings him into play paying two more Gold (discarded when unplayable)", SacrificeOfPawnsScenario),
        ("Rocky Terrain: before the battle's resolution its controller may bow no, one or two attacking Personalities at the battlefield (a bowed one, and a defender, are not offered), which changes who wins", RockyTerrainScenario),
        ("Shadows Walk: bowing one or more Shadowlands Ninja performers it destroys a bare attachment, or takes control of a Personality with Chi up to their number and gives him Ninja and Shadowlands; it costs 3 Honor either way", ShadowsWalkScenario),
        ("Stay Put: a Kensai bows an enemy card without Weapons or Armor and may attach a Weapon from the hand -- at full price, or 4 less while he defends", StayPutScenario),
        ("The Fires of War: each ability on its player's Stronghold may be used a second time this turn, and no third", FiresOfWarScenario),
        ("The Empress' Address: after an action or a battle destroyed one of your Personalities in a defending army a Courtier bows to bring your non-Unique Clan Personality out of your discard pile into your home, and you gain 2 Honor", EmpressAddressScenario),
        ("Rumors Travel: reduces every Honor gain by 1 outside the Attack Phase until its player's next turn begins; its Battle half moves a Personality to the battle and straightens him", RumorsTravelScenario),
    ]);

    private static void PublishedShape()
    {
        Check(PublishedContent.StrategyWave60.Cards.Select(c => c.Id).SequenceEqual(Wired), "strategy-wave-60 publishes exactly the wired cards");
        void Shape(string id, string name, int focus, string keywords, params string[] abilities)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == (id == ShadowsWalk ? 4 : 0) && card.Focus == focus, $"{name}: {card.Type} {card.Name} {card.GoldCost} {card.Focus}"); // Shadows Walk prints a Gold Cost of 4
            Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
            Check(card.Text.Split('\n').Distinct().Count() == card.Text.Split('\n').Length, $"{name}'s text is printed once");
        }
        Shape(Retribution, "Retribution", 4, "", "retribution-declare");
        Shape(Rhetoric, "Rhetoric", 3, "Political", "rhetoric-redirect");
        Shape(RumorsTravel, "Rumors Travel", 3, "Political", "rumors-travel-reduce", "rumors-travel-move");
        Shape(SelflessDefense, "Selfless Defense", 4, "", "selfless-defense-move", "selfless-defense-destroy");
        Shape(ShamefulInjury, "Shameful Injury", 4, "", "shameful-injury-negate");
        Shape(SnakeTattoo, "Snake Tattoo", 2, "Tattoo", "snake-tattoo-move");
        Shape(SacrificeOfPawns, "Sacrifice of Pawns", 3, "Political", "sacrifice-pawns-destroy");
        Shape(RockyTerrain, "Rocky Terrain", 4, "Terrain", "rocky-terrain-bow");
        Shape(ShadowsWalk, "Shadows Walk", 1, "Fear,Ninja", "shadows-walk-take");
        Shape(StayPut, "Stay Put", 3, "", "stay-put-bow", "stay-put-bow-attach", "stay-put-bow-attach-discount");
        Shape(EmpressAddress, "The Empress' Address", 2, "Political", "empress-address-action", "empress-address-battle");
        Shape(FiresOfWar, "The Fires of War", 4, "Unique", "fires-of-war-second-use");
        Shape(Sniping, "Sniping", 3, "", "sniping-boost");
        Shape(SolidDefense, "Solid Defense", 3, "", "solid-defense-negate", "solid-defense-negate-favor", "solid-defense-move");
        Shape(SpinningHeelKick, "Spinning Heel Kick", 4, "Air,Kiho", "spinning-heel-kick-straighten");
        Shape(StrategicStrike, "Strategic Strike", 2, "", "strategic-strike-attack");
        Shape(SummonedToJustice, "Summoned to Justice", 4, "Political", "summoned-justice-move");
        Shape(SuperiorOpponent, "Superior Opponent", 4, "Fear,Iaijutsu", "superior-opponent-focus", "superior-opponent-bow");
        Shape(TellTheTale, "Tell the Tale", 1, "", "tell-the-tale-honor");
        Shape(HeightOfCourage, "The Height of Courage", 2, "", "height-courage-negate", "height-courage-boost");
        Check(CardCatalog.Get(SolidDefense).Text.StartsWith("Political Reaction: Before the resolution of another player's action, discard the Imperial Favor unless you control a Personality with 5 or higher Force"),
            "Solid Defense publishes its scan's wording");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-60.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.SequenceEqual(Wired), "the manifest lists exactly the wired cards");
        foreach (var id in Wired)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // Passes for whoever must act until `stop` holds.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static void TellTheTaleScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, TellTheTale, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "tell-the-tale-honor", card.Id).Command!);
        Eq(Honor(e, 0), honor + 1, "one Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var t = Table(Phase.Action, 1);
        var other = Add(t, TellTheTale, 0, Zone.Hand); t.PriorityPlayerId = 0;
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "tell-the-tale-honor", other.Id), "Limited: only the active player");
    }

    private static void StrategicStrikeScenario()
    {
        MatchEngine Build(bool recon, out CardInstance card, out CardInstance scout, out CardInstance enemy, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, StrategicStrike, 0, Zone.Hand);
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Force(enemy, -2); // Force 2
            follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
            if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var scout, out var enemy, out var follower);
        var options = Options(e, 0, "strategic-strike-attack", card.Id, scout.Id).Where(o => o.Enabled).ToArray();
        Check(options.Length == 1, "a Scout performer is offered");
        var targets = options[0].TargetIds ?? [];
        Check(targets.Contains(enemy.Id), "with Reconnaissance a Personality with attached Followers may be targeted");
        Accept(e, Offer(e, 0, "strategic-strike-attack", card.Id, enemy.Id, scout.Id));
        Check(e.State.Cards[scout.Id].Bowed, "the Scout was bowed as the cost");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Ranged 6 destroys a Personality of Force 2 (the Follower's Force is not his)");
        var without = Build(false, out var card2, out var scout2, out var enemy2, out var follower2);
        var offered = Options(without, 0, "strategic-strike-attack", card2.Id, scout2.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(!offered.Contains(enemy2.Id) && offered.Contains(follower2.Id), "without Reconnaissance only the Follower is a target, the Personality behind it is not");
        // Only a Scout may perform it, and only an unbowed one.
        var t = Table(Phase.Battle, 0);
        var card3 = Add(t, StrategicStrike, 0, Zone.Hand);
        var samurai = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bowedScout = Add(t, "training-scout-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(t);
        Check(!CanUse(e3, 0, "strategic-strike-attack", card3.Id, samurai.Id) && !CanUse(e3, 0, "strategic-strike-attack", card3.Id, bowedScout.Id), "neither a non-Scout nor a bowed Scout may perform it");
    }

    private static void SnipingScenario()
    {
        // A Melee Attack: Sniping does not react to it.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var sniping2 = Add(s2, Sniping, 0, Zone.Hand);
        var victim = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        var archer = Add(s2, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // "training-weapon-attack": Melee 2
        var e = MatchEngine.FromTestState(s2);
        var melee = Offer(e, 1, "training-weapon-attack", archer.Id, victim.Id);
        Accept(e, melee);
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack opens its window: " + string.Join(" | ", e.State.Events.TakeLast(4).Select(x => x.Message)));
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled), "Sniping reacts only to a Ranged Attack");
        // Ranged 3 (Training followed-target attacker) on a Force 5 Personality would fail; doubled to 6 it destroys him.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var sniping3 = Add(s3, Sniping, 0, Zone.Hand);
        Add(s3, "training-attack-reactor", 0, Zone.Home); // a second Reaction keeps the window open once Sniping is played, so the strength can be read (issue #644)
        var victim3 = Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var archer3 = Add(s3, "training-followed-attacker", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-followed-recon-attack", archer3.Id, victim3.Id));
        Eq(e3.State.PendingChoice?.Type, "attackReaction", "the Attack opens its window");
        Eq(e3.State.PendingAttack!.Strength, 3, "Ranged 3");
        var reaction = e3.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled);
        Accept(e3, reaction.Command!);
        Eq(e3.State.PendingAttack!.Strength, 6, "the strength doubles");
        PassUntil(e3, () => e3.State.PendingChoice is null);
        Eq(e3.State.Cards[victim3.Id].Zone, Zone.Dead, "and it destroys the 5-Force Personality");
        Eq(e3.State.Cards[sniping3.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Played twice: each doubles the current strength (a second copy in the same window).
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 1;
        var one = Add(s4, Sniping, 0, Zone.Hand); var two = Add(s4, Sniping, 1, Zone.Hand);
        Add(s4, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once both copies are played, so the strength can be read (issue #644)
        var victim4 = Add(s4, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var archer4 = Add(s4, "training-followed-attacker", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "training-followed-recon-attack", archer4.Id, victim4.Id));
        var first = e4.State.PriorityPlayerId;
        Accept(e4, e4.QueryActions(first).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled).Command!);
        var second = e4.State.PriorityPlayerId;
        Accept(e4, e4.QueryActions(second).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled).Command!);
        Eq(e4.State.PendingAttack!.Strength, 12, "3 doubled twice");
        Check(one.Id.Length > 0 && two.Id.Length > 0, "fixture");
    }

    private static void HeightOfCourageScenario()
    {
        MatchEngine Build(out CardInstance height, out CardInstance defender, out CardInstance other, out CardInstance incapacitated)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            height = Add(s, HeightOfCourage, 0, Zone.Hand);
            defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            other = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            incapacitated = Add(s, Incapacitated, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var height, out var defender, out var other, out var incapacitated);
        Accept(e, Offer(e, 1, "incapacitated", incapacitated.Id, defender.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the movement is offered for negation");
        var offers = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "height-courage-negate").ToArray();
        Check(offers.Length == 1 && offers[0].Enabled && offers[0].Command!.PerformerId == defender.Id, "one option: the Personality the action would move, and no other");
        Accept(e, offers[0].Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[defender.Id].Zone, Zone.Battlefield, "his movement was negated");
        Eq(e.State.Cards[height.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Declined: the same position resolves normally.
        var d = Build(out _, out var moved, out _, out var card);
        Accept(d, Offer(d, 1, "incapacitated", card.Id, moved.Id));
        PassUntil(d, () => d.State.PendingChoice is null);
        Eq(d.State.Cards[moved.Id].Zone, Zone.Home, "declined: he moved home");
        // The Battle half: +1F for the performer and each of his Followers.
        var b = Table(Phase.Battle, 0);
        var card2 = Add(b, HeightOfCourage, 0, Zone.Hand);
        var lead = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(b, "training-follower", 0, Zone.Attached, province: 0, parent: lead.Id);
        var homeBody = Add(b, "training-crane-samurai", 0, Zone.Home);
        var eb = MatchEngine.FromTestState(b);
        Check(!CanUse(eb, 0, "height-courage-boost", card2.Id, homeBody.Id), "a Personality at home is not at the battle: he may not perform the Battle action");
        Accept(eb, Enabled(eb, 0, "height-courage-boost", card2.Id, lead.Id).Command!);
        Eq(ForceMods(eb, lead.Id), 1, "+1F for him");
        Eq(ForceMods(eb, follower.Id), 1, "and +1F for his Follower");
    }

    private static void SelflessDefenseScenario()
    {
        MatchEngine Build(bool courtier, out CardInstance card, out CardInstance yojimbo, out CardInstance enemy, out CardInstance ally)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            card = Add(s, SelflessDefense, 0, Zone.Hand);
            yojimbo = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Yojimbo");
            ally = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), courtier ? "Courtier" : "Samurai");
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var yojimbo, out var enemy, out var ally);
        Accept(e, Offer(e, 0, "selfless-defense-move", card.Id, enemy.Id, yojimbo.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality moved home");
        Check(e.State.Cards[enemy.Id].Bowed, "and, with a Courtier controlled, he is bowed");
        var n = Build(false, out var card2, out var yojimbo2, out var enemy2, out _);
        Accept(n, Offer(n, 0, "selfless-defense-move", card2.Id, enemy2.Id, yojimbo2.Id));
        Check(n.State.Cards[enemy2.Id].Zone == Zone.Home && !n.State.Cards[enemy2.Id].Bowed, "without a Courtier or Shugenja he is only moved");
        // A bowed Yojimbo may not perform the first half.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var card3 = Add(t, SelflessDefense, 0, Zone.Hand);
        var bowedYojimbo = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Yojimbo");
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "selfless-defense-move", card3.Id, bowedYojimbo.Id), "the first half needs an unbowed Yojimbo");
        // The second half: the Yojimbo is destroyed as the cost, and each chosen Courtier or Shugenja goes home.
        var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = 0;
        var card5 = Add(s5, SelflessDefense, 0, Zone.Hand);
        var yojimbo5 = Kw(Add(s5, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Yojimbo"); // no "unbowed" is printed
        var courtier5 = Kw(Add(s5, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
        var shugenja5 = Kw(Add(s5, "training-shugenja", 0, Zone.Battlefield, province: 0), "Shugenja");
        var samurai5 = Add(s5, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        var option = Enabled(e5, 0, "selfless-defense-destroy", card5.Id, yojimbo5.Id);
        var slot = option.Slots!.Single(x => x.Slot == "targets");
        Check(slot.CardIds.Contains(courtier5.Id) && slot.CardIds.Contains(shugenja5.Id) && !slot.CardIds.Contains(samurai5.Id) && !slot.CardIds.Contains(yojimbo5.Id),
            "the Courtier and the Shugenja are candidates; a Samurai, and the Yojimbo himself, are not");
        Accept(e5, option.Command! with { Selections = [Pick("targets", courtier5.Id, shugenja5.Id)], TargetId = courtier5.Id });
        Eq(e5.State.Cards[yojimbo5.Id].Zone, Zone.Dead, "the Yojimbo was destroyed as the cost");
        Check(e5.State.Cards[courtier5.Id].Zone == Zone.Home && e5.State.Cards[shugenja5.Id].Zone == Zone.Home, "both moved home");
        Eq(e5.State.Cards[samurai5.Id].Zone, Zone.Battlefield, "the others stay");
    }

    private static void SummonedToJusticeScenario()
    {
        MatchEngine Build(bool dishonorable, out CardInstance card, out CardInstance courtier, out CardInstance attacker)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            card = Add(s, SummonedToJustice, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier"); // at any location: at home
            attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            attacker.Dishonorable = dishonorable;
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var courtier, out var attacker);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 0, "summoned-justice-move", card.Id, attacker.Id, courtier.Id));
        Check(e.State.Cards[attacker.Id].Zone == Zone.Home && e.State.Cards[attacker.Id].Bowed, "the attacking Personality moved home and bowed");
        Eq(Honor(e, 1), honor - 2, "dishonorable: his controller loses his base Personal Honor (2)");
        var clean = Build(false, out var card2, out var courtier2, out var attacker2);
        var honor2 = Honor(clean, 1);
        Accept(clean, Offer(clean, 0, "summoned-justice-move", card2.Id, attacker2.Id, courtier2.Id));
        Check(clean.State.Cards[attacker2.Id].Zone == Zone.Home && Honor(clean, 1) == honor2, "an honorable one costs nothing");
        // Not a Courtier, not unbowed, and not a defender's Personality.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var card3 = Add(t, SummonedToJustice, 0, Zone.Hand);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var samurai = Add(t, "training-crane-samurai", 0, Zone.Home);
        var bowedCourtier = Kw(Add(t, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var foe = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(t);
        Check(!CanUse(e3, 0, "summoned-justice-move", card3.Id, samurai.Id) && !CanUse(e3, 0, "summoned-justice-move", card3.Id, bowedCourtier.Id), "the performer is an unbowed Courtier");
        var options = Options(e3, 0, "summoned-justice-move", card3.Id);
        Check(options.All(o => !o.Enabled) && foe.Id.Length > 0, "no Courtier, no action");
    }

    private static void SpinningHeelKickScenario()
    {
        MatchEngine Build(out CardInstance kick, out CardInstance monk, out CardInstance other, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0);
            kick = Add(s, SpinningHeelKick, 0, Zone.Hand);
            monk = Kw(Add(s, "training-battle-bower", 0, Zone.Battlefield, bowed: true, province: 0), "Monk"); // no "unbowed" is printed: a bowed Monk straightens
            other = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var kick, out var monk, out var other, out var enemy);
        Check(!CanUse(e, 0, "spinning-heel-kick-straighten", kick.Id, other.Id), "only a Monk may perform it");
        Accept(e, Enabled(e, 0, "spinning-heel-kick-straighten", kick.Id, monk.Id).Command!);
        Check(!e.State.Cards[monk.Id].Bowed, "the Monk straightened");
        var grant = e.State.AdditionalActions.Single(g => g.PlayerId == 0);
        Eq(grant.PerformerId, monk.Id, "the additional action is his");
        // Issue #1333: both seats see who must perform it, and which card gave it.
        foreach (var seat in new[] { 0, 1 })
            Check(e.GetView(seat).HeldActions is [{ PlayerId: 0, Kind: "additional", SourceName: "Spinning Heel Kick", Count: 1, MayDecline: true } held] && held.PerformerId == monk.Id, $"seat {seat}: the Monk's held action is shown");
        Eq(e.State.PriorityPlayerId, 0, "and its player keeps priority to take it");
        // Another Personality's action is an ordinary action: it spends nothing and forfeits the grant.
        var wrong = Build(out var kick2, out var monk2, out var other2, out var enemy2);
        Accept(wrong, Enabled(wrong, 0, "spinning-heel-kick-straighten", kick2.Id, monk2.Id).Command!);
        Accept(wrong, Offer(wrong, 0, "training-bow-at-battle", other2.Id, enemy2.Id));
        Check(wrong.State.AdditionalActions.Count == 0 && wrong.State.PriorityPlayerId == 1, "an action of another Personality was an ordinary action: the burst is over and priority passed");
        // The Monk's own action spends the grant and keeps his player's priority.
        var right = Build(out var kick3, out var monk3, out _, out var enemy3);
        Accept(right, Enabled(right, 0, "spinning-heel-kick-straighten", kick3.Id, monk3.Id).Command!);
        Accept(right, Offer(right, 0, "training-bow-at-battle", monk3.Id, enemy3.Id));
        Check(right.State.Cards[enemy3.Id].Bowed, "the Monk's additional action resolved");
        Check(right.State.AdditionalActions.Count == 0, "the grant is spent");
    }

    private static void ShamefulInjuryScenario()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var card = Add(s, ShamefulInjury, 0, Zone.Hand); // player 0 controls no unit at the battlefield: Absent
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        target.Modifiers.Add(new("fx", "weak-poison-force-zero", "Force", -4)); // from an action
        target.Modifiers.Add(new("fx", "a-trait", "Force", 1)); // a standing trait's
        var poison = Add(s, WeakPoison, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "shameful-injury-negate", card.Id, target.Id));
        Check(e.State.Cards[target.Id].Modifiers.Where(m => m.SuppressedTurn != e.State.TurnNumber).All(m => m.Effect == "a-trait"), "the action's penalty is set aside, the trait's bonus stays");
        Check(e.State.Cards[target.Id].ForceModifiersNegatedTurn == e.State.TurnNumber, "new ones are refused this turn");
        // A Personality not at the battlefield is no target.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var card2 = Add(t, ShamefulInjury, 0, Zone.Hand);
        var away = Add(t, "training-lion-samurai", 1, Zone.Home);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(Options(MatchEngine.FromTestState(t), 0, "shameful-injury-negate", card2.Id).All(o => !(o.TargetIds ?? []).Contains(away.Id)), "it targets a Personality at the current battlefield");
        Check(poison.Id.Length > 0, "fixture");
    }

    private static void SolidDefenseScenario()
    {
        MatchEngine Build(int force, bool favor, out CardInstance card, out CardInstance paragon, out CardInstance attacker)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            card = Add(s, SolidDefense, 0, Zone.Hand);
            var lead = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Force(lead, force - 3);
            var adversity = Add(s, OvercomeAdversity, 1, Zone.Hand);
            paragon = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Paragon");
            attacker = adversity;
            if (favor) s.FavorPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        // With a Personality of 5 or higher Force nothing is discarded, and the Imperial Favor (held) stays.
        var e = Build(5, true, out var card, out _, out var adversity);
        var lion = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var crane = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 1, "overcome-adversity", adversity.Id, crane.Id, lion.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the loss is offered for negation, where it would be applied (Mendon's R4 ruling: Solid Defense may be played on any Honor change)");
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "solid-defense-negate-favor" && a.Enabled), "with a 5-Force Personality the Favor is not the way to pay");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "solid-defense-negate" && a.Enabled).Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(Honor(e, 1), honor, "the 5 Honor the action would cost was negated");
        Eq(e.State.FavorPlayerId, 0, "nothing was discarded");
        // Without one, the Imperial Favor is discarded.
        var f = Build(4, true, out var card2, out _, out var adversity2);
        var crane2 = f.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var lion2 = f.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var honorF = Honor(f, 1);
        Accept(f, Offer(f, 1, "overcome-adversity", adversity2.Id, crane2.Id, lion2.Id));
        Check(!f.QueryActions(0).Any(a => a.Command?.AbilityId == "solid-defense-negate" && a.Enabled), "the free half needs a Personality of 5 or higher Force");
        Accept(f, f.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "solid-defense-negate-favor" && a.Enabled).Command!);
        PassUntil(f, () => f.State.PendingChoice is null);
        Eq(Honor(f, 1), honorF, "negated");
        Eq(f.State.FavorPlayerId, (int?)null, "the Imperial Favor was discarded");
        // With neither, no window opens for it.
        var g = Build(4, false, out _, out _, out var adversity3);
        var crane3 = g.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var lion3 = g.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        Accept(g, Offer(g, 1, "overcome-adversity", adversity3.Id, crane3.Id, lion3.Id));
        Check(g.State.PendingChoice is null || g.State.PendingChoice.Type != "honorChangeReaction", "nobody can pay for it: no window");
        Check(card.Id.Length > 0 && card2.Id.Length > 0, "fixture");
        // The Battle half: a Personality without attachments goes home; one with an attachment is no target.
        var b = Table(Phase.Battle, 0);
        var half = Add(b, SolidDefense, 0, Zone.Hand);
        Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bare = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(b, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        var eb = MatchEngine.FromTestState(b);
        var targets = Options(eb, 0, "solid-defense-move", half.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(bare.Id) && !targets.Contains(armed.Id), "a Personality without attachments only");
        Accept(eb, Offer(eb, 0, "solid-defense-move", half.Id, bare.Id));
        Eq(eb.State.Cards[bare.Id].Zone, Zone.Home, "moved home");
    }

    private static void RhetoricScenario()
    {
        MatchEngine Build(int chiPenalty, out CardInstance rhetoric, out CardInstance courtier, out CardInstance tale)
        {
            var s = Table(Phase.Action, 0);
            tale = Add(s, TellTheTale, 0, Zone.Hand);
            rhetoric = Add(s, Rhetoric, 1, Zone.Hand);
            courtier = Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier"); // Chi 3
            if (chiPenalty != 0) courtier.Modifiers.Add(new("fx", "fx", "Chi", chiPenalty));
            return MatchEngine.FromTestState(s);
        }
        var e = Build(0, out var rhetoric, out var courtier, out var tale);
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Enabled(e, 0, "tell-the-tale-honor", tale.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "an action that gains Honor opens the window where the gain would be applied, for either player's Rhetoric (Mendon's R4 ruling)");
        var mine = e.QueryActions(1).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "rhetoric-redirect" && a.Enabled).ToArray();
        Check(mine.Any(o => o.Command!.TargetPlayerId == 1) && mine.Any(o => o.Command!.TargetPlayerId == 0), "either player may be the one chosen");
        Accept(e, mine.Single(o => o.Command!.TargetPlayerId == 1).Command!);
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(Honor(e, 0), h0, "the original player did not gain");
        Eq(Honor(e, 1), h1 + 1, "the chosen player took the gain instead");
        // His Chi must cover the change: with 0 Chi a gain of 1 is not redirected (the Reaction is still played and spent).
        var weak = Build(-3, out var rhetoric2, out var courtier2, out var tale2);
        var w0 = Honor(weak, 0); var w1 = Honor(weak, 1);
        Accept(weak, Enabled(weak, 0, "tell-the-tale-honor", tale2.Id).Command!);
        Accept(weak, weak.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "rhetoric-redirect" && a.Enabled && a.Command.TargetPlayerId == 1).Command!);
        PassUntil(weak, () => weak.State.PendingChoice is null);
        Check(Honor(weak, 0) == w0 + 1 && Honor(weak, 1) == w1, "Chi 0 does not cover a gain of 1: the original player gains it");
        // A bowed Courtier, and an action that changes no Honor, offer nothing.
        var s = Table(Phase.Action, 0);
        var tale3 = Add(s, TellTheTale, 0, Zone.Hand);
        Add(s, Rhetoric, 1, Zone.Hand);
        Kw(Add(s, "training-lion-courtier", 1, Zone.Home, bowed: true), "Courtier");
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Enabled(e3, 0, "tell-the-tale-honor", tale3.Id).Command!);
        Check(e3.State.PendingChoice is null, "no unbowed Courtier: no window");
        Check(rhetoric.Id.Length > 0 && rhetoric2.Id.Length > 0 && courtier2.Id.Length > 0, "fixture");
    }

    private static void RumorsTravelScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, RumorsTravel, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "rumors-travel-reduce", card.Id).Command!);
        Eq(e.State.RemC.RumorsTravelOwners.Count, 1, "the effect is in force");
        var before = Honor(e, 0);
        e.GainHonorForTest(0, 1, "test");
        Eq(Honor(e, 0), before, "a gain of 1 outside a battle is reduced to nothing");
        e.GainHonorForTest(1, 3, "test");
        Eq(Honor(e, 1), 6 + 3 - 1 + 0, "another player's gain is reduced too (the Lion starts at 6 Honor)");
        e.GainHonorForTest(0, -2, "test");
        Eq(Honor(e, 0), before - 2, "a loss is not");
        var battle = Batch336Support.Edit(e, st => { st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        var atBattle = Honor(battle, 0);
        battle.GainHonorForTest(0, 2, "test");
        Eq(Honor(battle, 0), atBattle + 2, "during a battle nothing is reduced");
        // It lasts until its player's NEXT turn begins.
        var later = Batch336Support.Edit(e, st => { });
        Batch336Support.RunUntil(later, () => later.State.ActivePlayerId == 1 && later.State.Phase == Phase.Action);
        Eq(later.State.RemC.RumorsTravelOwners.Count, 1, "still in force during the other player's turn");
        Batch336Support.RunUntil(later, () => later.State.ActivePlayerId == 0 && later.State.Phase == Phase.Action);
        Eq(later.State.RemC.RumorsTravelOwners.Count, 0, "over when its player's next turn begins");
        // The Battle half: enemy units at the battlefield are needed; the performer moves there and straightens if he moved.
        MatchEngine Build(bool enemies, out CardInstance move, out CardInstance body)
        {
            var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
            move = Add(t, RumorsTravel, 0, Zone.Hand);
            body = Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true);
            if (enemies) Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(t);
        }
        var m = Build(true, out var card2, out var body2);
        Accept(m, Enabled(m, 0, "rumors-travel-move", card2.Id, body2.Id).Command!);
        Check(m.State.Cards[body2.Id].Zone == Zone.Battlefield && !m.State.Cards[body2.Id].Bowed, "he moved to the battlefield and straightened");
        var n = Build(false, out var card3, out var body3);
        Check(!CanUse(n, 0, "rumors-travel-move", card3.Id, body3.Id), "with no enemy units there it is not playable");
    }
    private static void SnakeTattooScenario()
    {
        MatchEngine Build(bool adjacent, bool tattooed, out CardInstance tattoo, out CardInstance target, out CardInstance bower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            if (adjacent) s.UnresolvedBattlefields = [0, 1];
            tattoo = Add(s, SnakeTattoo, 0, Zone.Hand);
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (tattooed) Kw(target, "Tattooed");
            bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, true, out var tattoo, out var target, out var bower);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeting opens the window");
        var offer = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "snake-tattoo-move" && a.Enabled);
        Check(offer.Provinces is { Length: 1 } && offer.Provinces[0] == 1, "the one adjacent battlefield is offered");
        Accept(e, offer.Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        var moved = e.State.Cards[target.Id];
        Check(moved.Zone == Zone.Battlefield && moved.ProvinceIndex == 1, "he moved to the adjacent battlefield");
        Check(!moved.Bowed, "and, no longer a legal target, the action's bowing was negated");
        Eq(e.State.Cards[tattoo.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Declined: the action bows him where he stands.
        var d = Build(true, true, out _, out var target2, out var bower2);
        Accept(d, Offer(d, 1, "training-bow-at-battle", bower2.Id, target2.Id));
        PassUntil(d, () => d.State.PendingChoice is null);
        Check(d.State.Cards[target2.Id].Bowed && d.State.Cards[target2.Id].ProvinceIndex == 0, "declined: he was bowed where he stood");
        // No adjacent battlefield to go to, or not a Tattooed Personality: no window.
        var alone = Build(false, true, out _, out var target3, out var bower3);
        Accept(alone, Offer(alone, 1, "training-bow-at-battle", bower3.Id, target3.Id));
        Check(alone.State.PendingChoice is null && alone.State.Cards[target3.Id].Bowed, "with no adjacent unresolved battlefield nothing is offered");
        var plain = Build(true, false, out _, out var target4, out var bower4);
        Accept(plain, Offer(plain, 1, "training-bow-at-battle", bower4.Id, target4.Id));
        Check(plain.State.PendingChoice is null, "only a Tattooed Personality");
        // An Attack is a Battle action that targets him too: he moves away and the Attack, no longer having a legal target, does nothing.
        var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = 1; s5.UnresolvedBattlefields = [0, 1];
        Add(s5, SnakeTattoo, 0, Zone.Hand);
        var tattooed = Kw(Add(s5, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tattooed"); // Force 3
        var archer = Add(s5, "training-followed-attacker", 1, Zone.Battlefield, province: 0); // Ranged 3
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 1, "training-followed-recon-attack", archer.Id, tattooed.Id));
        Eq(e5.State.PendingChoice?.Type, "attackReaction", "the Attack's window opens");
        Accept(e5, e5.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "snake-tattoo-move" && a.Enabled).Command!);
        PassUntil(e5, () => e5.State.PendingChoice is null);
        Check(e5.State.Cards[tattooed.Id].Zone == Zone.Battlefield && e5.State.Cards[tattooed.Id].ProvinceIndex == 1, "he moved to the adjacent battlefield and survived the Attack");
    }

    private static void SuperiorOpponentScenario()
    {
        // The Focus Effect: player 0's Personality duels player 1's; attachments stand at the battlefield on both, and on a Personality at home.
        (MatchEngine E, CardInstance Mine, CardInstance MyArm, CardInstance TheirArm, CardInstance HomeArm) Duel(bool mineHome)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); mine.Modifiers.Add(new("test-source", "test", "Chi", 5));
            var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var homebody = Add(s, "training-crane-samurai", 0, Zone.Home);
            var myArm = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: mine.Id);
            var theirArm = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: theirs.Id);
            var homeArm = Add(s, "training-weapon", 0, Zone.Attached, parent: homebody.Id);
            var duel = Add(s, "training-duel-challenge", 0, Zone.Hand); var card = Add(s, SuperiorOpponent, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-duel-challenge", duel.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("acceptDuel", 1, e.Version));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: card.Id));
            if (mineHome) e = Batch336Support.Edit(e, st => { st.Cards[mine.Id].Zone = Zone.Home; st.Cards[mine.Id].ProvinceIndex = null; st.Cards[myArm.Id].ProvinceIndex = null; });
            Accept(e, new GameCommand("strike", 1, e.Version));
            return (e, mine, myArm, theirArm, homeArm);
        }
        var (e1, _, myArm1, theirArm1, homeArm1) = Duel(false);
        Eq(e1.State.PendingChoice?.Type, "focusEffectChoice", "the Focus Effect asks its player");
        var picks = e1.QueryActions(0).Where(x => x.Type == "chooseFocusEffect" && x.Command?.TargetId is not null).Select(x => x.Command!.TargetId).ToArray();
        Check(picks.Length == 2 && picks.Contains(myArm1.Id) && picks.Contains(theirArm1.Id) && !picks.Contains(homeArm1.Id), "the attachments at the current battlefield, either player's, and not one at home: " + string.Join(",", picks));
        Check(!e1.QueryActions(1).Any(x => x.Type == "chooseFocusEffect"), "only the Focus Effect's player chooses");
        Accept(e1, e1.QueryActions(0).First(x => x.Type == "chooseFocusEffect" && x.Command?.TargetId == theirArm1.Id).Command!);
        Check(e1.State.Cards[theirArm1.Id].Zone != Zone.Attached && e1.State.Cards[myArm1.Id].Zone == Zone.Attached, "the chosen attachment is destroyed and no other");
        var (e2, _, myArm2, theirArm2, _) = Duel(false);
        Accept(e2, e2.QueryActions(0).First(x => x.Type == "chooseFocusEffect" && x.Command?.Choice == "decline").Command!);
        Check(e2.State.Cards[myArm2.Id].Zone == Zone.Attached && e2.State.Cards[theirArm2.Id].Zone == Zone.Attached, "it is a may: declined, nothing is destroyed");
        var (e3, _, _, _, _) = Duel(true);
        Check(e3.State.PendingChoice?.Type != "focusEffectChoice", "when his Personality in the duel is not at the battlefield nothing is asked");
        // Battle: bow each Follower and Personality in an enemy unit with Force up to the Duelist's Chi.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        var card4 = Add(s4, SuperiorOpponent, 0, Zone.Hand);
        var duelist = Kw(Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Duelist"); // Chi 3
        var notDuelist = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var host = Add(s4, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var small = Add(s4, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); // Force 2
        var big = Add(s4, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); Force(big, 2); // Force 4
        var elsewhere = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "superior-opponent-bow", card4.Id, notDuelist.Id), "only a Duelist may perform it");
        Accept(e4, Offer(e4, 0, "superior-opponent-bow", card4.Id, host.Id, duelist.Id));
        Check(e4.State.Cards[host.Id].Bowed && e4.State.Cards[small.Id].Bowed, "the Personality (Force 3) and the small Follower (Force 2) are bowed");
        Check(!e4.State.Cards[big.Id].Bowed && !e4.State.Cards[elsewhere.Id].Bowed, "the Follower of Force 4 is not, nor is another unit");
    }
    private static void RockyTerrainScenario()
    {
        MatchEngine Build(int terrainOwner, out CardInstance terrain, out CardInstance a, out CardInstance b, out CardInstance c, out CardInstance defender)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            terrain = Add(s, RockyTerrain, terrainOwner, Zone.Battlefield, province: 0);
            a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            b = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            c = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
            defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
            return MatchEngine.FromTestState(s);
        }
        var e = Build(0, out var terrain, out var a, out var b, out var c, out var defender);
        PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
        Eq(e.State.PendingChoice!.PlayerId, 0, "the Terrain's controller is asked");
        var options = e.QueryActions(0).Where(o => o.Type == "beforeBattleOptional").ToArray();
        Check(options.Any(o => o.Command!.Choice == "decline"), "it is a may: bowing none is offered");
        var named = options.Where(o => o.Command!.Choice == "use").SelectMany(o => new[] { o.Command!.TargetId }.Concat(o.Command.CardIds ?? [])).Distinct().ToArray();
        Check(named.Contains(a.Id) && named.Contains(b.Id) && !named.Contains(c.Id) && !named.Contains(defender.Id), "only the unbowed attacking Personalities: " + string.Join(",", named));
        Check(options.Count(o => o.Command!.Choice == "use") == 3, "one option for each of the two, and one for the pair");
        Accept(e, options.Single(o => o.Command!.Choice == "use" && o.Command.CardIds is { Length: 1 }).Command!);
        Check(e.State.Cards[a.Id].Bowed && e.State.Cards[b.Id].Bowed, "both attacking Personalities were bowed");
        // Bowed, neither contributes: the defender (3) beats the attackers (0) and destroys them.
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(e.State.Cards[a.Id].Zone == Zone.Dead && e.State.Cards[b.Id].Zone == Zone.Dead && e.State.Cards[defender.Id].Zone != Zone.Dead, "the battle went to the defender");
        // Declined: the attackers (8) win.
        var d = Build(0, out _, out var a2, out _, out _, out var defender2);
        PassUntil(d, () => d.State.PendingChoice?.Type == "beforeBattleOptional");
        Accept(d, d.QueryActions(0).Single(o => o.Type == "beforeBattleOptional" && o.Command!.Choice == "decline").Command!);
        PassUntil(d, () => d.State.PendingChoice is null);
        Check(d.State.Cards[defender2.Id].Zone == Zone.Dead && d.State.Cards[a2.Id].Zone != Zone.Dead, "declined: the attackers won and the defender died");
        Check(terrain.Id.Length > 0, "fixture");
    }

    private static void ShadowsWalkScenario()
    {
        MatchEngine Build(int performers, out CardInstance card, out CardInstance[] ninjas, out CardInstance enemy, out CardInstance follower, out CardInstance armed)
        {
            var s = Table(Phase.Action, 0);
            card = Add(s, ShadowsWalk, 0, Zone.Hand);
            ninjas = Enumerable.Range(0, performers).Select(_ => Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Ninja", "Shadowlands")).ToArray();
            enemy = Add(s, "training-lion-samurai", 1, Zone.Home); // Chi 3
            armed = Add(s, "training-lion-samurai", 1, Zone.Home);
            follower = Add(s, "training-follower", 1, Zone.Attached, parent: armed.Id);
            return MatchEngine.FromTestState(s);
        }
        GameCommand Command(MatchEngine e, CardInstance card, CardInstance[] ninjas, CardInstance target) =>
            Enabled(e, 0, "shadows-walk-take", card.Id).Command! with { TargetId = target.Id, Selections = [Pick("performers", ninjas.Select(n => n.Id).ToArray()), Pick("target", target.Id)] };
        // Three performers take control of a Chi-3 Personality.
        var e = Build(3, out var card, out var ninjas, out var enemy, out var follower, out var armed);
        var slots = Enabled(e, 0, "shadows-walk-take", card.Id).Slots!;
        Check(slots.Single(x => x.Slot == "target").CardIds.Contains(enemy.Id) && slots.Single(x => x.Slot == "target").CardIds.Contains(follower.Id) && !slots.Single(x => x.Slot == "target").CardIds.Contains(armed.Id),
            "a card without attachments: the Follower and the bare Personality, not the one wearing it");
        var honor = Honor(e, 0);
        Accept(e, Command(e, card, ninjas, enemy));
        var taken = e.State.Cards[enemy.Id];
        Eq(taken.OwnerId, 0, "control of him is taken");
        Check(e.HasKeyword(enemy.Id, "Ninja") && e.HasKeyword(enemy.Id, "Shadowlands"), "and he permanently has Ninja and Shadowlands");
        Check(ninjas.All(n => e.State.Cards[n.Id].Bowed), "every performer was bowed as the cost");
        Eq(Honor(e, 0), honor - 3, "3 Honor lost");
        // Two performers do not cover his Chi 3: nothing but the Honor.
        var two = Build(2, out var card2, out var ninjas2, out var enemy2, out _, out _);
        var honor2 = Honor(two, 0);
        Accept(two, Command(two, card2, ninjas2, enemy2));
        Check(two.State.Cards[enemy2.Id].OwnerId == 1 && !two.HasKeyword(enemy2.Id, "Ninja") && Honor(two, 0) == honor2 - 3, "Chi above the number of performers: he is untouched, the Honor is still lost");
        // An attachment is destroyed.
        var attach = Build(1, out var card3, out var ninjas3, out _, out var follower3, out _);
        var honor3 = Honor(attach, 0);
        Accept(attach, Command(attach, card3, ninjas3, follower3));
        Check(attach.State.Cards[follower3.Id].Zone == Zone.FateDiscard && Honor(attach, 0) == honor3 - 3, "the bare attachment was destroyed, and 3 Honor lost");
        // A Loyal Personality of another Clan may not be controlled.
        var s4 = Table(Phase.Action, 0);
        var card4 = Add(s4, ShadowsWalk, 0, Zone.Hand);
        var team = Enumerable.Range(0, 3).Select(_ => Kw(Add(s4, "training-crane-samurai", 0, Zone.Home), "Ninja", "Shadowlands")).ToArray();
        var loyal = Kw(Add(s4, "training-lion-samurai", 1, Zone.Home), "Loyal");
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Command(e4, card4, team, loyal));
        Check(e4.State.Cards[loyal.Id].OwnerId == 1, "a Loyal Lion Personality stays the Lion player's");
        // Only unbowed Shadowlands Ninja perform: a bowed one, and a plain Samurai, are not candidates.
        var s5 = Table(Phase.Action, 0);
        var card5 = Add(s5, ShadowsWalk, 0, Zone.Hand);
        var good = Kw(Add(s5, "training-crane-samurai", 0, Zone.Home), "Ninja", "Shadowlands");
        var bowedNinja = Kw(Add(s5, "training-crane-samurai", 0, Zone.Home, bowed: true), "Ninja", "Shadowlands");
        var plain = Add(s5, "training-crane-samurai", 0, Zone.Home);
        var onlyNinja = Kw(Add(s5, "training-crane-samurai", 0, Zone.Home), "Ninja");
        Add(s5, "training-lion-samurai", 1, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        var candidates = Enabled(e5, 0, "shadows-walk-take", card5.Id).Slots!.Single(x => x.Slot == "performers").CardIds;
        Check(candidates.Contains(good.Id) && !candidates.Contains(bowedNinja.Id) && !candidates.Contains(plain.Id) && !candidates.Contains(onlyNinja.Id), "the performers are unbowed Shadowlands Ninja Personalities");
    }

    private static void StayPutScenario()
    {
        MatchEngine Build(int attacker, out CardInstance card, out CardInstance kensai, out CardInstance weapon, out CardInstance enemy, out CardInstance armed)
        {
            var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0;
            card = Add(s, StayPut, 0, Zone.Hand);
            kensai = Add(s, "training-kensai", 0, Zone.Battlefield, province: 0);
            weapon = Add(s, "oracle-l5r-9879", 0, Zone.Hand); // Pure Strike Blade: a Weapon costing 6
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
            return MatchEngine.FromTestState(s);
        }
        // Without the attach: bow the enemy card only.
        var e = Build(0, out var card, out var kensai, out var weapon, out var enemy, out var armed);
        var plainTargets = Options(e, 0, "stay-put-bow", card.Id, kensai.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(plainTargets.Contains(enemy.Id) && !plainTargets.Contains(armed.Id), "an enemy card without an attached Weapon; the armed one is not a target");
        Accept(e, Offer(e, 0, "stay-put-bow", card.Id, enemy.Id, kensai.Id));
        Check(e.State.Cards[enemy.Id].Bowed && e.State.Cards[weapon.Id].Zone == Zone.Hand, "it is bowed and nothing is attached");
        // As the Attacker the attach costs its full price (6): the discounted action is refused.
        var a = Build(0, out var cardA, out var kensaiA, out var weaponA, out var enemyA, out _);
        var full = Enabled(a, 0, "stay-put-bow-attach", cardA.Id, kensaiA.Id);
        Eq(full.Gold!.Cost, 6, "the Weapon's whole price");
        Check(!CanUse(a, 0, "stay-put-bow-attach-discount", cardA.Id, kensaiA.Id), "the discount is for a defending Kensai");
        Accept(a, full.Command! with { TargetId = weaponA.Id, Selections = [Pick("weapon", weaponA.Id), Pick("target", enemyA.Id)] });
        Check(a.State.Cards[enemyA.Id].Bowed && a.State.Cards[weaponA.Id].Zone == Zone.Attached && a.State.Cards[weaponA.Id].AttachedTo == kensaiA.Id, "the enemy card is bowed and the Weapon is attached to the Kensai");
        // As the Defender it costs 4 less (2), and the full-price action is refused.
        var d = Build(1, out var cardD, out var kensaiD, out var weaponD, out var enemyD, out _);
        var cheap = Enabled(d, 0, "stay-put-bow-attach-discount", cardD.Id, kensaiD.Id);
        Eq(cheap.Gold!.Cost, 2, "4 less while he is defending");
        Check(!CanUse(d, 0, "stay-put-bow-attach", cardD.Id, kensaiD.Id), "the full price applies to an attacking Kensai only");
        Accept(d, cheap.Command! with { TargetId = weaponD.Id, Selections = [Pick("weapon", weaponD.Id), Pick("target", enemyD.Id)] });
        Check(d.State.Cards[weaponD.Id].AttachedTo == kensaiD.Id && d.State.Cards[enemyD.Id].Bowed, "both effects happened");
        // A bowed Kensai, and a Personality without the keyword, may not perform it.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        var card3 = Add(s3, StayPut, 0, Zone.Hand);
        var bowedKensai = Add(s3, "training-kensai", 0, Zone.Battlefield, bowed: true, province: 0);
        var samurai = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "stay-put-bow", card3.Id, bowedKensai.Id) && !CanUse(e3, 0, "stay-put-bow", card3.Id, samurai.Id), "an unbowed Kensai performs it");
    }

    private static void FiresOfWarScenario()
    {
        const string Otoro = "oracle-l5r-8246";
        var s = Table(Phase.Action, 0);
        var sh = s.Cards[s.Players[0].StrongholdId]; sh.DefinitionId = Otoro;
        var fires = Add(s, FiresOfWar, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "otoro-look", sh.Id).Any(o => o.Enabled), "the Stronghold's Open action is offered");
        Accept(e, Options(e, 0, "otoro-look", sh.Id).First(o => o.Enabled).Command!);
        Accept(e, e.QueryActions(1).First(o => o.Type == "pass").Command!);
        Check(!CanUse(e, 0, "otoro-look", sh.Id), "once a turn: it is spent");
        Accept(e, Enabled(e, 0, "fires-of-war-second-use", fires.Id).Command!);
        Accept(e, e.QueryActions(1).First(o => o.Type == "pass").Command!);
        Check(CanUse(e, 0, "otoro-look", sh.Id), "a second time this turn");
        Accept(e, Options(e, 0, "otoro-look", sh.Id).First(o => o.Enabled).Command!);
        Accept(e, e.QueryActions(1).First(o => o.Type == "pass").Command!);
        Check(!CanUse(e, 0, "otoro-look", sh.Id), "but no third");
        // Only its player's own Stronghold: the opponent's abilities keep their limit. A copy played on a later turn is a new grant.
        var later = Batch336Support.Edit(e, st => st.TurnNumber += 2);
        Check(CanUse(later, 0, "otoro-look", sh.Id), "the next turn everything is fresh");
    }

    private static void EmpressAddressScenario()
    {
        MatchEngine Build(bool reactorDefends, out CardInstance card, out CardInstance courtier, out CardInstance lost, out CardInstance ok, out CardInstance unique, out CardInstance foreign, out CardInstance attackerCard)
        {
            var s = Table(Phase.Battle, reactorDefends ? 1 : 0); s.PriorityPlayerId = 1;
            card = Add(s, EmpressAddress, 0, Zone.Hand);
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
            lost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3: a Ranged 3 Attack destroys him
            attackerCard = Add(s, "training-followed-attacker", 1, Zone.Battlefield, province: 0);
            ok = Add(s, "training-crane-veteran", 0, Zone.Dead);
            unique = Add(s, "training-unique-personality", 0, Zone.Dead);
            foreign = Add(s, "training-lion-samurai", 0, Zone.Dead);
            return MatchEngine.FromTestState(s);
        }
        // Another player's Ranged Attack (an action) destroys a defending Personality: the window opens for its owner.
        var e = Build(true, out var card, out var courtier, out var lost, out var ok, out var unique, out var foreign, out var archer);
        Accept(e, Offer(e, 1, "training-followed-recon-attack", archer.Id, lost.Id));
        PassUntil(e, () => e.State.PendingChoice is null || e.QueryActions(0).Any(o => o.Command?.AbilityId == "empress-address-action"));
        var offers = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId == "empress-address-action" && o.Enabled).ToArray();
        Check(offers.Length > 0, "the Reaction is offered after the action");
        var targets = offers.SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(ok.Id) && targets.Contains(lost.Id) && !targets.Contains(unique.Id) && !targets.Contains(foreign.Id), "a non-Unique Personality with the player's own Clan, in the discard pile (the one just destroyed included): " + string.Join(",", targets));
        var honor = Honor(e, 0);
        Accept(e, offers.First(o => o.TargetIds!.Contains(ok.Id)).Command! with { TargetId = ok.Id });
        Check(e.State.Cards[ok.Id].Zone == Zone.Home && e.State.Cards[ok.Id].OwnerId == 0, "he is in his controller's home");
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        Eq(Honor(e, 0), honor + 2, "2 Honor gained");
        // A Personality that was attacking is not "in a defending army": no window.
        var att = Build(false, out _, out _, out var lost2, out _, out _, out _, out var archer2);
        Accept(att, Offer(att, 1, "training-followed-recon-attack", archer2.Id, lost2.Id));
        Check(!att.QueryActions(0).Any(o => o.Command?.AbilityId == "empress-address-action" && o.Enabled), "an attacking Personality's destruction does not qualify");
        // Battle: the Attacker wins and destroys the defenders; the Defender may react after the resolution.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        var card3 = Add(s3, EmpressAddress, 0, Zone.Hand);
        var empress = Kw(Add(s3, "training-crane-courtier", 0, Zone.Home), "Empress");
        var defender = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var back = Add(s3, "training-crane-veteran", 0, Zone.Dead);
        var e3 = MatchEngine.FromTestState(s3);
        PassUntil(e3, () => e3.QueryActions(0).Any(o => o.Command?.AbilityId == "empress-address-battle" && o.Enabled));
        var honor3 = Honor(e3, 0);
        Accept(e3, e3.QueryActions(0).First(o => o.Command?.AbilityId == "empress-address-battle" && o.Enabled && (o.TargetIds ?? []).Contains(back.Id)).Command! with { TargetId = back.Id });
        Check(e3.State.Cards[back.Id].Zone == Zone.Home && e3.State.Cards[empress.Id].Bowed && Honor(e3, 0) == honor3 + 2, "after the battle: brought into play at home, the Empress bowed, 2 Honor");
        Check(e3.State.Cards[defender.Id].Zone == Zone.Dead && card3.Id.Length > 0, "the defender died in the resolution");
        // A battle the Defender won destroyed none of its Personalities.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        Add(s4, EmpressAddress, 0, Zone.Hand);
        Kw(Add(s4, "training-crane-courtier", 0, Zone.Home), "Courtier");
        Add(s4, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s4, "training-crane-veteran", 0, Zone.Dead);
        var e4 = MatchEngine.FromTestState(s4);
        PassUntil(e4, () => e4.State.PendingChoice is null);
        Check(!e4.State.Events.Any(x => x.Type == "reactionWindow" && x.Message.Contains("After-resolution")), "the Defender won: nothing was destroyed on its side, so no window opened");
    }
    private static void SacrificeOfPawnsScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance courtier, out CardInstance target, params string[] deck)
        {
            var s = Table(Phase.Action, 0);
            card = Add(s, SacrificeOfPawns, 0, Zone.Hand);
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
            target = Add(s, "training-lion-samurai", 1, Zone.Home);
            foreach (var def in deck) { var c = Add(s, def, 0, Zone.DynastyDeck); s.Players[0].DynastyDeck.Insert(0, c.Id); }
            return MatchEngine.FromTestState(s);
        }
        GameCommand Decision(MatchEngine e, string key) => e.QueryActions(1).Single(o => o.Type == "chooseEitherOr" && o.Command!.Choice == key && o.Enabled).Command!;
        // His controller destroys the target: nothing further happens.
        var e = Build(out var card, out var courtier, out var target, "training-honor-personality");
        var deckSize = e.State.Players[0].DynastyDeck.Count;
        Accept(e, Offer(e, 0, "sacrifice-pawns-destroy", card.Id, target.Id, courtier.Id));
        Eq(e.State.Cards[courtier.Id].Zone, Zone.Dead, "the performing Courtier was destroyed as the cost");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the target's controller decides");
        Eq(e.State.PendingChoice!.PlayerId, 1);
        Check(e.QueryActions(1).Count(o => o.Type == "chooseEitherOr") == 2, "destroy the target, or not");
        Accept(e, Decision(e, "destroy"));
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the target was destroyed");
        Check(e.State.PendingSearch is null && e.State.Players[0].DynastyDeck.Count == deckSize, "no search: the deck is untouched");
        // He refuses: the acting player searches the Dynasty deck and brings a Personality into play, paying his price plus 2.
        var r = Build(out var card2, out var courtier2, out var target2, "training-honor-personality", "training-crane-dignitary", "training-unique-personality");
        Accept(r, Offer(r, 0, "sacrifice-pawns-destroy", card2.Id, target2.Id, courtier2.Id));
        Accept(r, Decision(r, "refuse"));
        Eq(r.State.Cards[target2.Id].Zone, Zone.Home, "the target lives");
        Eq(r.State.PendingChoice?.Type, "searchSelect", "the acting player searches");
        var offered = r.QueryActions(0).Where(o => o.Type == "selectSearchCard").Select(o => o.Command!.Choice).ToArray();
        Check(new[] { "training-honor-personality", "training-crane-dignitary", "training-unique-personality" }.All(offered.Contains), "every Personality of the deck may be chosen: " + string.Join(",", offered));
        Check(!r.QueryActions(1).Any(o => o.Type == "selectSearchCard"), "only the searching player chooses");
        Accept(r, r.QueryActions(0).Single(o => o.Type == "selectSearchCard" && o.Command!.Choice == "training-honor-personality").Command!);
        Eq(r.State.PendingChoice?.Type, "searchRecruitPay", "the price is paid");
        var pay = r.QueryActions(0).Single(o => o.Type == "chooseSearchPayment" && o.Command!.Choice != "decline");
        Eq(pay.Gold!.Cost, 5, "Gold Cost 3 plus two more (no Clan discount, no allegiance)"); QuoteCarriesSources(pay.Gold!, "search recruit (Personality)");
        Accept(r, pay.Command!);
        var entered = r.State.Cards.Values.Single(c => c.DefinitionId == "training-honor-personality");
        Check(entered.Zone == Zone.Home && entered.OwnerId == 0 && !r.State.Players[0].DynastyDeck.Contains(entered.Id), "he came into play at home, out of the deck");
        Check(r.State.PendingSearch is null && r.State.PendingChoice is null && r.State.PriorityPlayerId == 1, "the action finished and priority passed");
        // A Personality that cannot be played is discarded: a Honor Requirement above the player's Honor.
        var u = Build(out var card3, out var courtier3, out var target3, "training-crane-dignitary");
        Accept(u, Offer(u, 0, "sacrifice-pawns-destroy", card3.Id, target3.Id, courtier3.Id));
        Accept(u, Decision(u, "refuse"));
        Accept(u, u.QueryActions(0).Single(o => o.Type == "selectSearchCard" && o.Command!.Choice == "training-crane-dignitary").Command!);
        Check(u.State.Cards.Values.Any(c => c.DefinitionId == "training-crane-dignitary" && c.Zone == Zone.DynastyDiscard) && u.State.PendingSearch is null, "unable to play him (Honor Requirement 12): he is discarded");
        // ...a Unique Personality the player already controls, and a Personality the payer cannot afford.
        var s4 = Table(Phase.Action, 0);
        var card4 = Add(s4, SacrificeOfPawns, 0, Zone.Hand);
        var courtier4 = Kw(Add(s4, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var target4 = Add(s4, "training-lion-samurai", 1, Zone.Home);
        Add(s4, "training-unique-personality", 0, Zone.Home);
        var copy = Add(s4, "training-unique-personality", 0, Zone.DynastyDeck); s4.Players[0].DynastyDeck.Insert(0, copy.Id);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "sacrifice-pawns-destroy", card4.Id, target4.Id, courtier4.Id));
        Accept(e4, Decision(e4, "refuse"));
        Accept(e4, e4.QueryActions(0).Single(o => o.Type == "selectSearchCard" && o.Command!.Choice == "training-unique-personality").Command!);
        Eq(e4.State.Cards[copy.Id].Zone, Zone.DynastyDiscard, "a second Unique copy is discarded, not played");
        // A bowed Courtier, and a Personality that is not a Courtier, may not perform it.
        var s5 = Table(Phase.Action, 0);
        var card5 = Add(s5, SacrificeOfPawns, 0, Zone.Hand);
        var bowed = Kw(Add(s5, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var samurai = Add(s5, "training-crane-samurai", 0, Zone.Home);
        Add(s5, "training-lion-samurai", 1, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        Check(!CanUse(e5, 0, "sacrifice-pawns-destroy", card5.Id, bowed.Id) && !CanUse(e5, 0, "sacrifice-pawns-destroy", card5.Id, samurai.Id), "an unbowed Courtier performs it");
    }
    private static void RetributionScenario()
    {
        MatchEngine Build(bool holdsIt, out CardInstance card, out CardInstance lead, out CardInstance second, out CardInstance follower, out CardInstance defender)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            for (var i = 1; i < 4; i++) s.Players[1].Provinces[i].Destroyed = true; // player 1 keeps one province: one battlefield in the additional phase
            card = Add(s, Retribution, 0, holdsIt ? Zone.Hand : Zone.FateDiscard);
            Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // the first phase's attacker (Force 6) beats
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // ... player 0's lone defender (Force 3)
            lead = Add(s, "training-crane-veteran", 0, Zone.Home); // Force 5, at home for the additional phase
            second = Add(s, "training-crane-samurai", 0, Zone.Home);
            follower = Add(s, "training-follower", 0, Zone.Attached, parent: lead.Id);
            defender = Add(s, "training-lion-veteran", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var lead, out var second, out var follower, out var foe);
        PassUntil(e, () => e.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Eq(e.State.PriorityPlayerId, 0, "the Defender holds the window");
        Check(e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Command?.AbilityId == "retribution-declare" && o.Enabled) && e.QueryActions(0).Any(o => o.Type == "declineReaction"), "the Reaction, or a pass");
        Check(!e.QueryActions(1).Any(o => o.Type == "useAbility"), "the Attacker is offered nothing");
        Accept(e, e.QueryActions(0).Single(o => o.Type == "useAbility" && o.Command?.AbilityId == "retribution-declare").Command!);
        Eq(e.State.Phase, Phase.InfantryDeclaration, "an additional Attack Phase begins");
        Eq(e.State.ActivePlayerId, 0, "the Defender is its Attacker");
        Check(e.State.UnresolvedBattlefields.SequenceEqual([0]) && e.State.PriorityPlayerId == 0, "a battlefield at each of player 1's provinces; player 0 assigns");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Only one Personality may be assigned: both at once are refused; one at a time is offered per unit and province.
        var attackOptions = e.QueryActions(0).Where(o => o.Type == "attack" && o.Command is not null).ToArray();
        Check(attackOptions.Length == 2 && attackOptions.All(o => o.Command!.Assignments!.Count == 1), "one option per (unit, province): " + string.Join(";", attackOptions.Select(o => o.Label)));
        Reject(e, new("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [lead.Id] = 0, [second.Id] = 0 }));
        Accept(e, attackOptions.Single(o => o.Command!.Assignments!.ContainsKey(lead.Id)).Command!);
        Eq(ForceMods(e, lead.Id), 1, "the Personality has +1F after he assigns");
        Eq(ForceMods(e, follower.Id), 1, "and so does each Follower in his unit");
        Check(e.State.Cards[lead.Id].Zone == Zone.Battlefield, "he is assigned");
        // The rest plays as an Attack Phase (the defenders assign, the battle is fought) and the turn's player is restored for the Dynasty Phase.
        Batch336Support.RunUntil(e, () => e.State.Phase == Phase.Dynasty);
        Eq(e.State.ActivePlayerId, 1, "player 1's turn goes on: the Dynasty Phase is his");
        Eq(e.State.RemC.RetributionRoot, -1, "nothing of the swap remains");
        // Declined: the phase just ends.
        var d = Build(true, out _, out _, out _, out _, out _);
        PassUntil(d, () => d.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Accept(d, d.QueryActions(0).Single(o => o.Type == "declineReaction").Command!);
        Check(d.State.Phase == Phase.Dynasty && d.State.ActivePlayerId == 1 && d.State.PendingChoice is null, "declined: the Dynasty Phase of the turn's player");
        // Without a Retribution in hand no window opens.
        var n = Build(false, out _, out _, out _, out _, out _);
        PassUntil(n, () => n.State.Phase == Phase.Dynasty);
        Check(n.State.ActivePlayerId == 1, "no window: the Dynasty Phase follows the last battle");
        Check(foe.Id.Length > 0, "fixture");
    }
}
