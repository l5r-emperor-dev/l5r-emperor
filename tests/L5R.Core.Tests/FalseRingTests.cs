using static RegressionAssertions;
using L5R.Core;

// The False Rings (issue #370, Mendon's ruling): "The False Ring of Air has no mechanism by which it may be played directly from hand. In fact, none of
// the false rings do. They should only be playable when they meet the condition and their second action that requires the ring to bow may only be used
// once it is in play." Air `oracle-l5r-10531`, Earth `10532` and Water `10535` are published (Fire `10533` and Void `10534` since issue #674, ring-wave-3) (the
// same rule applies when they are: put-into-play only, from the hand, on their condition; the second action a declared bow cost). Everything is checked
// through QueryActions, the way the client sees it. See docs/decisions/2026-09-20-false-rings.md.
internal static class FalseRingTests
{
    private const string Air = "oracle-l5r-10531", Earth = "oracle-l5r-10532", Water = "oracle-l5r-10535";
    private const string AirForce = "false-ring-of-air-force", WaterAttack = "false-ring-of-water-attack";
    private const string AirRecruit = "false-ring-of-air-recruit", WaterRecruit = "false-ring-of-water-recruit", EarthRecruit = "false-ring-of-earth-recruit";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Air, Earth, Water, Fire and Void are the wired False Rings; each second ability bows the Ring as a declared cost with no hand alternative, and the put-into-play abilities declare none", PublishedShape),
            ("False Ring of Air / Water: the second ability is never OFFERED from a hand or a discard pile (not even disabled), is rejected there, and is offered once the Ring is in play and unbowed; a bowed Ring is refused", SecondAbilityOnlyInPlay),
            ("False Ring of Air / Water in play: using the second ability bows the Ring first (a cost) and applies its effect; the Ring is not bowed by a refused attempt", SecondAbilityBowsTheRing),
            ("A False Ring in hand is offered exactly one action -- its put-into-play -- and only when its condition holds (Water: Province Strength 11 or greater; 10 refused); the Honor loss is paid", WaterPutIntoPlay),
            ("False Ring of Air: put into play only after a win with an opposing unit (granted at resolution); not after a loss and not after an unopposed win; the Ring leaves the hand only that way and loses 2 Honor", AirPutIntoPlay),
            ("False Ring of Earth: put into play only on its duel condition (no grant, no play), from the hand; its -1 maximum hand size applies only while it is IN PLAY, not from a hand", EarthPutIntoPlayAndStatic),
            ("A False Ring already in play cannot 'put itself into play' again (no second 2 Honor loss), for every recruit ability; a Ring in a discard pile has no action at all", NoRecruitFromPlayOrDiscard),
            ("The TRUE Rings keep their printed hand play ('bow this card or discard it from your hand'): Air, Earth and Void are offered and playable from the hand", TrueRingsKeepTheirHandPlay),
        ];
        runner.Run("false-rings", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null)
    {
        var c = new CardInstance { Id = $"fr-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private static MatchState BattleTable(int active = 0, int priority = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = active; s.PriorityPlayerId = priority;
        return s;
    }

    private static ActionOption[] Options(MatchEngine e, CardInstance card, int player = 0) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == card.Id).ToArray();

    private static ActionOption? Option(MatchEngine e, CardInstance card, string ability, int player = 0) =>
        Options(e, card, player).SingleOrDefault(a => a.Label.StartsWith(AbilityCatalog.All[ability].Label));

    private static GameCommand Use(MatchEngine e, CardInstance card, string ability, string? target = null, int player = 0) =>
        new("useAbility", player, e.Version, CardId: card.Id, TargetId: target, AbilityId: ability);

    // ---- shape ---------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        foreach (var (id, name) in new[] { (Air, "False Ring of Air"), (Earth, "False Ring of Earth"), (Water, "False Ring of Water") })
        {
            var c = CardCatalog.Get(id);
            Check(!c.ScenarioOnly && c.Type == CardType.Ring && c.Name == name, id);
        }
        foreach (var name in new[] { "False Ring of Fire", "False Ring of the Void" }) // wired since issue #674 (ring-wave-3, WlSpellsRingsTests)
            Check(CardCatalog.Cards.Values.Any(c => !c.ScenarioOnly && c.Name == name), name + " is wired");
        foreach (var second in new[] { AirForce, WaterAttack })
        {
            var costs = AbilityCatalog.All[second].Costs!;
            Check(costs.Length == 1 && costs[0].Verb == CostVerb.Bow && costs[0].Subject == CostSubject.Source && !costs[0].UnlessInHand, second + ": a real bow, no hand alternative");
        }
        foreach (var recruit in new[] { AirRecruit, WaterRecruit, EarthRecruit })
            Check(AbilityCatalog.All[recruit].Costs is null or { Length: 0 }, recruit + ": the put-into-play declares no bow");
        // The only abilities in the catalogue that bow their source with a hand alternative are the TRUE Rings (printed: "bow this card or discard it from your hand").
        var withHandAlternative = AbilityCatalog.All.Where(p => p.Value.Costs?.Any(c => c.UnlessInHand) == true).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Eq(string.Join(",", withHandAlternative), "ring-of-air-straighten,ring-of-earth-lower,ring-of-earth-raise,ring-of-fire-destroy,ring-of-void-draw,ring-of-water-move-battlefield,ring-of-water-move-home");
    }

    // ---- the second ability ---------------------------------------------------------------------------------------
    // Air (Absent Battle: needs no unit of its own) and Water (Battle: needs presence) each get a table with an enemy target at the battlefield.
    private static MatchEngine Table(string ring, Zone zone, bool bowed, out CardInstance card, out CardInstance target)
    {
        var s = BattleTable();
        card = Add(s, ring, 0, zone, bowed: bowed);
        if (ring == Water) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        target = Add(s, ring == Water ? "training-lion-courtier" : "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return MatchEngine.FromTestState(s);
    }

    private static void SecondAbilityOnlyInPlay()
    {
        foreach (var (ring, ability) in new[] { (Air, AirForce), (Water, WaterAttack) })
        {
            foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard, Zone.DynastyDiscard })
            {
                var e = Table(ring, zone, false, out var card, out var target);
                Check(Option(e, card, ability) is null, $"{ability}: not offered (not even disabled) from {zone}");
                Check(Options(e, card).All(o => o.Command?.AbilityId != ability), $"{ability}: no offered command from {zone}");
                Reject(e, Use(e, card, ability, target.Id));
            }
            var inPlay = Table(ring, Zone.Home, false, out var ring1, out var target1);
            var offer = Option(inPlay, ring1, ability)!;
            Check(offer is not null && offer.Enabled, $"{ability}: offered enabled once in play and unbowed: {offer?.Reason}");
            Check(offer!.TargetIds!.Contains(target1.Id));
            OfferedCommandsValidate(inPlay, () => MatchEngine.FromTestState(inPlay.State));
            // A bowed Ring cannot pay the bow: refused (and not enabled).
            var bowed = Table(ring, Zone.Home, true, out var ring2, out var target2);
            Check(Option(bowed, ring2, ability) is null or { Enabled: false }, $"{ability}: a bowed Ring is not offered as usable");
            Reject(bowed, Use(bowed, ring2, ability, target2.Id));
        }
    }

    private static void SecondAbilityBowsTheRing()
    {
        var air = Table(Air, Zone.Home, false, out var airRing, out var airTarget);
        Accept(air, Use(air, airRing, AirForce, airTarget.Id));
        Check(air.State.Cards[airRing.Id].Bowed, "Air: bowed as the cost");
        Eq(air.GetView(1).Players[1].Army.Single(c => c.Id == airTarget.Id).Force, 1, "Air: 4 - 3");
        var water = Table(Water, Zone.Home, false, out var waterRing, out var waterTarget);
        Accept(water, Use(water, waterRing, WaterAttack, waterTarget.Id));
        Check(water.State.Cards[waterRing.Id].Bowed, "Water: bowed as the cost");
        Eq(water.State.Cards[waterTarget.Id].Zone, Zone.Dead, "Water: the Ranged 3 Attack destroyed a Force 2 Personality");
        // A refused attempt bows nothing (the ring was already bowed for the first).
        var again = Table(Water, Zone.Home, true, out var ring3, out var target3);
        Reject(again, Use(again, ring3, WaterAttack, target3.Id));
        Check(again.State.Cards[target3.Id].Zone == Zone.Battlefield, "nothing happened");
    }

    // ---- put into play ---------------------------------------------------------------------------------------------
    private static void WaterPutIntoPlay()
    {
        MatchEngine Table(int strength, out CardInstance ring)
        {
            var s = BattleTable(active: 0, priority: 1);          // player 1 is the Defender: the current battlefield's province is theirs
            s.Players[1].Provinces.Single(p => p.Index == 0).Strength = strength;
            ring = Add(s, Water, 1, Zone.Hand);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);   // presence: it is a plain Battle action
            return MatchEngine.FromTestState(s);
        }
        var low = Table(10, out var lowRing);
        var lowOptions = Options(low, lowRing, 1);
        Check(lowOptions.All(o => !o.Enabled), "Strength 10: nothing is offered enabled");
        Check(lowOptions.All(o => o.Label.StartsWith(AbilityCatalog.All[WaterRecruit].Label)), "and the only listed action is the put-into-play, never the attack");
        Reject(low, Use(low, lowRing, WaterRecruit, player: 1));
        Reject(low, Use(low, lowRing, WaterAttack, "x", player: 1));

        var high = Table(11, out var highRing);
        var options = Options(high, highRing, 1);
        Eq(options.Length, 1, "exactly one action from the hand");
        Check(options[0].Enabled && options[0].Command!.AbilityId == WaterRecruit, options[0].Reason ?? "");
        var honor = high.State.Players[1].Honor;
        Accept(high, options[0].Command!);
        Eq(high.State.Cards[highRing.Id].Zone, Zone.Home); Check(!high.State.Players[1].Hand.Contains(highRing.Id));
        Eq(high.State.Players[1].Honor, honor - 2, "the 2 Honor loss is paid");
    }

    private static void AirPutIntoPlay()
    {
        // A battle at province 0 that player 0 (the Attacker) fights; the outcome decides whether the Ring is recruitable at the NEXT battle.
        MatchEngine Fought(bool win, bool opposed, out CardInstance ring)
        {
            var s = BattleTable();
            ring = Add(s, Air, 0, Zone.Hand);
            Add(s, win ? "training-lion-veteran" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);     // Force 6 or 3
            if (opposed) Add(s, win ? "training-crane-samurai" : "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        bool Granted(MatchEngine e, CardInstance ring) => e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is { Length: 1 } ids && ids[0] == ring.Id);
        Check(Granted(Fought(true, true, out var wonRing), wonRing), "won a battle with an opposing unit: recruitable");
        Check(!Granted(Fought(false, true, out var lostRing), lostRing), "lost: not recruitable");
        Check(!Granted(Fought(true, false, out var freeRing), freeRing), "won with no unit on the opposing side: not recruitable");

        // The recruit is then offered from the hand and is the only way out: exactly one action, a put-into-play that costs 2 Honor.
        var won = Fought(true, true, out var ring);
        var next = won.State;
        next.Phase = Phase.Battle; next.PriorityPlayerId = 0; next.ActivePlayerId = 0; next.CurrentBattleProvince = 0; next.UnresolvedBattlefields = [0];
        Add(next, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(next);
        var options = Options(e2, ring);
        Eq(options.Length, 1, "a hand False Ring has one action");
        Check(options[0].Enabled && options[0].Command!.AbilityId == AirRecruit, options[0].Reason ?? "");
        var honor = e2.State.Players[0].Honor;
        Accept(e2, options[0].Command!);
        Eq(e2.State.Cards[ring.Id].Zone, Zone.Home); Eq(e2.State.Players[0].Honor, honor - 2);
        // Without the condition the recruit is refused outright.
        var s3 = BattleTable(); var cold = Add(s3, Air, 0, Zone.Hand); Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Reject(e3, Use(e3, cold, AirRecruit)); Check(Options(e3, cold).All(o => !o.Enabled), "no enabled action for a hand Ring whose condition is not met");
    }

    private static void EarthPutIntoPlayAndStatic()
    {
        var s = BattleTable(active: 1, priority: 1);
        var ring = Add(s, Earth, 1, Zone.Hand);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, ring, EarthRecruit, player: 1));
        Check(Options(e, ring, 1).All(o => !o.Enabled), "no duel won: nothing enabled");
        // (The duel conditions themselves -- winner and loser sharing no keywords -- are pinned by RingOfEarth in the core suite.)

        // The static: "Other players have -1 maximum hand size" only while the Ring is IN PLAY.
        string EndPhaseChoice(Zone zone)
        {
            var t = MatchEngine.CreateEconomyScenario().State;
            Add(t, Earth, 1, zone);
            while (t.Players[0].Hand.Count < 7) Add(t, "training-rally", 0, Zone.Hand);
            var x = MatchEngine.FromTestState(t);
            Accept(x, new GameCommand("endDynasty", 0, x.Version));
            return x.State.PendingChoice?.Type ?? "none";
        }
        Eq(EndPhaseChoice(Zone.Home), "discardHand", "in play: 8 cards after the draw, limit 7");
        Check(EndPhaseChoice(Zone.Hand) != "discardHand", "in the hand it changes nothing (limit 8)");
        Check(EndPhaseChoice(Zone.FateDiscard) != "discardHand", "nor in a discard pile");
    }

    private static void NoRecruitFromPlayOrDiscard()
    {
        // A Ring in play whose recruit condition holds (Water, Strength 11) still cannot be "put into play" again, and pays nothing.
        var s = BattleTable(active: 0, priority: 1);
        s.Players[1].Provinces.Single(p => p.Index == 0).Strength = 11;
        var inPlay = Add(s, Water, 1, Zone.Home);
        var inDiscard = Add(s, Water, 1, Zone.FateDiscard);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[1].Honor;
        Reject(e, Use(e, inPlay, WaterRecruit, player: 1));
        Reject(e, Use(e, inDiscard, WaterRecruit, player: 1));
        Check(Option(e, inPlay, WaterRecruit, 1) is null or { Enabled: false }, "not offered from play");
        Eq(Options(e, inDiscard, 1).Length, 0, "a Ring in a discard pile has no action at all");
        Eq(e.State.Players[1].Honor, honor);
        // The same for the grant-based recruits: a Ring in play holds no grant, and the ability itself demands the hand.
        var t = BattleTable(); var airInPlay = Add(t, Air, 0, Zone.Home); Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        t.AdditionalActions.Add(new(0, [airInPlay.Id], false));
        var a = MatchEngine.FromTestState(t);
        Reject(a, Use(a, airInPlay, AirRecruit));
    }

    // ---- the true Rings (report: covered by their own printed text, unchanged) -------------------------------------------
    private static void TrueRingsKeepTheirHandPlay()
    {
        // Ring of Earth (Battle, Absent): "bow this card or discard it from your hand: Give a province +3 or -3 strength".
        var s = BattleTable();
        var earth = Add(s, "oracle-l5r-6334", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        // Its province target is enumerated by QueryActions (issue #379): one option per Province owner, the Provinces listed on it; Ring of Earth's own tests are RingGrantTests.
        var raise = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == earth.Id && a.Command?.AbilityId == "ring-of-earth-raise" && a.Command.TargetPlayerId == 1);
        Check(raise.Enabled, raise.Reason ?? "offered enabled from the hand");
        Accept(e, raise.Command! with { Province = 0 });
        Eq(e.State.Cards[earth.Id].Zone, Zone.FateDiscard, "discarded instead of bowed");
        // Ring of Air (Battle/Open): "choose your performing Personality: Straighten his unit".
        var t = MatchEngine.CreateEconomyScenario().State; t.Phase = Phase.Action; t.ActivePlayerId = 0; t.PriorityPlayerId = 0;
        var air = Add(t, "oracle-l5r-6333", 0, Zone.Hand);
        var bowed = Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var ea = MatchEngine.FromTestState(t);
        var straighten = Option(ea, air, "ring-of-air-straighten")!;
        Check(straighten is not null && straighten.Enabled, straighten?.Reason ?? "offered from the hand");
        Accept(ea, straighten!.Command! with { PerformerId = bowed.Id });
        Check(!ea.State.Cards[bowed.Id].Bowed && ea.State.Cards[air.Id].Zone == Zone.FateDiscard);
        // Ring of the Void (Limited): "bow this card or discard it from your hand: Draw a card".
        var v = MatchEngine.CreateEconomyScenario().State; v.Phase = Phase.Action; v.ActivePlayerId = 0; v.PriorityPlayerId = 0;
        var voidRing = Add(v, "oracle-l5r-6338", 0, Zone.Hand);
        var ev = MatchEngine.FromTestState(v);
        var draw = Option(ev, voidRing, "ring-of-void-draw")!;
        Check(draw is not null && draw.Enabled, draw?.Reason ?? "offered from the hand");
    }
}
