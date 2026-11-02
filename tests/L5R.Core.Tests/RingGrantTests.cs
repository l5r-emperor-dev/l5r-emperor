using static RegressionAssertions;
using L5R.Core;

// Two rulings by Mendon (issue #379), both checked through QueryActions the way the client sees them. See docs/decisions/2026-09-20-false-rings.md.
//   1. "Ring of Earth's battle action is an Absent Battle and may be used at any battle and may be discarded from the hand to do so." The province it names
//      is a command parameter, which QueryActions did not enumerate, so the hand play (and the in-play one) was never OFFERED enabled.
//   2. "A recruit grant at one battle is limited to that battle. If another effect grants the ability to recruit, it is only available at that battle."
//      Every Ring recruit grant carries the battle it belongs to (AdditionalActionGrant.Battle) and lapses with it.
internal static class RingGrantTests
{
    private const string Air = "oracle-l5r-6333", Earth = "oracle-l5r-6334", Fire = "oracle-l5r-6335", Void = "oracle-l5r-6338", Water = "oracle-l5r-6339";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Ring of Earth from the HAND at a battle where the player controls NO units: raise and lower are offered enabled for every Province of either player, and playing one discards the Ring and moves that Province's Strength by 3", EarthHandNoUnits),
            ("Ring of Earth from the hand at a battle where the player DOES control units: the same offers and the same effect", EarthHandWithUnits),
            ("Ring of Earth IN PLAY (bow): offered enabled, bows the Ring (which stays in play) and moves the Strength; a bowed Ring is offered disabled and refused; once per turn across both abilities", EarthInPlay),
            ("Ring of Earth is a Battle action: not offered enabled outside a battle (Action phase, from the hand or in play), and refused if submitted", EarthNotOutsideBattle),
            ("The other true Rings' hand plays are offered enabled through QueryActions and run: Air straightens, Water moves home, Void draws (Open, Action phase), Fire destroys (performer plus enemy target); each Ring goes to the discard pile", OtherTrueRingsFromHand),
            ("A recruit grant lapses with its battle: granted at battle 1 it is offered in the after-battle window and is gone at battle 2 in the same turn; a NEW grant at battle 2 is offered only in battle 2's window", GrantBelongsToItsBattle),
            ("The after-battle window is closed by its holder's pass (an opponent with nothing to play is not asked to pass) and the grant does not survive it; the Ring stays in the hand; using the grant in the window puts the Ring into play with no presence needed", AfterBattleWindow),
            ("A recruit grant survives the other actions and the passes of its own battle (its holder's ordinary actions, the opponent's, a pass) and is still there for the after-battle window", GrantSurvivesItsBattlesActions),
            ("A grant made outside a battle (Ring of Air's two Favor actions) belongs to the NEXT battle: not usable before it, and cleared at the end of the turn if that battle is never fought", GrantMadeBetweenBattles),
            ("Every Ring recruit grant is scoped: the grants the engine makes carry a battle, while the other Additional Action grants (Again!, Two Hands and the rest) carry none and keep their own removal", GrantsCarryTheirBattle),
        ];
        runner.Run("ring-grants", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null)
    {
        var c = new CardInstance { Id = $"rg-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // A battle in progress: the attacker is player 0, `number` counts it as the battle it is (SelectBattle numbers them 1, 2, ...).
    private static MatchState BattleTable(int number = 1, int priority = 0, params int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = priority; s.CurrentBattleProvince = battlefields.Length > 0 ? battlefields[0] : 0;
        s.UnresolvedBattlefields = battlefields.Length > 0 ? battlefields.ToList() : [0]; s.BattleNumber = number;
        return s;
    }

    private static GameCommand Command(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);

    private static ActionOption[] Offered(MatchEngine e, CardInstance card, string ability, int player = 0) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == card.Id && a.Command?.AbilityId == ability).ToArray();

    // The one option per (ability, whose Province) Ring of Earth is offered as.
    private static ActionOption EarthOption(MatchEngine e, CardInstance ring, string ability, int owner, int player = 0)
    {
        var option = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == ring.Id && a.Command?.AbilityId == ability && a.Command.TargetPlayerId == owner).ToArray();
        Eq(option.Length, 1, $"{ability} for player {owner}'s Province is offered exactly once");
        return option[0];
    }

    private static void EarthEnumeration(MatchEngine e, CardInstance ring, bool enabled)
    {
        var provinces = e.State.Players[0].Provinces.Count;
        foreach (var ability in new[] { "ring-of-earth-raise", "ring-of-earth-lower" })
            foreach (var owner in new[] { 0, 1 })
            {
                var o = EarthOption(e, ring, ability, owner);
                Eq(o.Enabled, enabled, $"{ability} / player {owner}: {o.Reason}");
                if (!enabled) continue;
                Check(o.Command!.Province is not null && o.Provinces is not null && o.Provinces.SequenceEqual(Enumerable.Range(0, provinces)), "every Province is a legal choice");
                Check(o.Label.EndsWith(owner == 0 ? "your Province" : "an enemy Province"), o.Label);
            }
    }

    // Plays the offered option, choosing `province` from its list, and returns the engine.
    private static void PlayEarth(MatchEngine e, CardInstance ring, string ability, int owner, int province)
    {
        var o = EarthOption(e, ring, ability, owner);
        Check(o.Provinces!.Contains(province));
        Accept(e, o.Command! with { Province = province });
    }

    // ---- ruling 1: Ring of Earth --------------------------------------------------------------------------------
    private static void EarthHandNoUnits()
    {
        var s = BattleTable(); // no unit of either player at the battlefield
        var ring = Add(s, Earth, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        EarthEnumeration(e, ring, enabled: true);
        var before = e.State.Players[1].Provinces.Select(p => e.ProvinceStrengthForTest(1, p.Index)).ToArray();
        PlayEarth(e, ring, "ring-of-earth-raise", 1, 2);
        var after = e.State;
        Eq(after.Cards[ring.Id].Zone, Zone.FateDiscard, "the discard-from-hand cost is paid: the Ring goes to the discard pile");
        Check(!after.Players[0].Hand.Contains(ring.Id), "and leaves the hand");
        Eq(e.ProvinceStrengthForTest(1, 2), before[2] + 3, "an enemy Province is raised by 3 (until end of turn)");
        Check(after.Players[1].Provinces.Where((p, i) => i != 2).Select(p => e.ProvinceStrengthForTest(1, p.Index)).SequenceEqual(before.Where((_, i) => i != 2)), "no other Province changes");

        var s2 = BattleTable(); var ring2 = Add(s2, Earth, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var own = e2.ProvinceStrengthForTest(0, 1);
        PlayEarth(e2, ring2, "ring-of-earth-lower", 0, 1);
        Eq(e2.ProvinceStrengthForTest(0, 1), own - 3, "one's own Province is lowered by 3");
        Eq(e2.State.Cards[ring2.Id].Zone, Zone.FateDiscard);
    }

    private static void EarthHandWithUnits()
    {
        var s = BattleTable();
        var ring = Add(s, Earth, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        EarthEnumeration(e, ring, enabled: true);
        var before = e.ProvinceStrengthForTest(1, 0);
        PlayEarth(e, ring, "ring-of-earth-lower", 1, 0);
        Eq(e.ProvinceStrengthForTest(1, 0), before - 3);
        Eq(e.State.Cards[ring.Id].Zone, Zone.FateDiscard);
    }

    private static void EarthInPlay()
    {
        foreach (var withUnits in new[] { false, true })
        {
            var s = BattleTable();
            var ring = Add(s, Earth, 0, Zone.Home);
            if (withUnits) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            EarthEnumeration(e, ring, enabled: true);
            var before = e.ProvinceStrengthForTest(0, 3);
            PlayEarth(e, ring, "ring-of-earth-raise", 0, 3);
            Eq(e.ProvinceStrengthForTest(0, 3), before + 3);
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "an in-play Ring bows instead and stays in play");
            Check(e.State.Cards[ring.Id].Bowed, "bowed");
            // Bowed, and the once-per-turn use is spent: both abilities are still listed (the choice), disabled, and refused when submitted anyway.
            Check(e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == ring.Id && a.Command?.AbilityId is "ring-of-earth-raise" or "ring-of-earth-lower").All(a => !a.Enabled), "no enabled option while bowed");
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-lower", TargetPlayerId: 1, Province: 0));
        }
        // A bowed Ring that has NOT yet been used this turn is refused too: the bow cost cannot be paid.
        var t = BattleTable(); var bowed = Add(t, Earth, 0, Zone.Home, bowed: true);
        var et = MatchEngine.FromTestState(t);
        Reject(et, new GameCommand("useAbility", 0, et.Version, CardId: bowed.Id, AbilityId: "ring-of-earth-raise", TargetPlayerId: 1, Province: 0));
        Check(et.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == bowed.Id && a.Command?.AbilityId is "ring-of-earth-raise" or "ring-of-earth-lower").All(a => !a.Enabled));
    }

    private static void EarthNotOutsideBattle()
    {
        foreach (var zone in new[] { Zone.Hand, Zone.Home })
        {
            var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            var ring = Add(s, Earth, 0, zone);
            var e = MatchEngine.FromTestState(s);
            var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == ring.Id && a.Command?.AbilityId is "ring-of-earth-raise" or "ring-of-earth-lower").ToArray();
            Check(offered.All(o => !o.Enabled), $"{zone}: no enabled Ring of Earth option in the Action phase");
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-raise", TargetPlayerId: 1, Province: 0));
            Eq(e.State.Cards[ring.Id].Zone, zone, "nothing was spent");
        }
        // Between battles (choosing the next battlefield) there is no battle either.
        var b = BattleTable(); b.Phase = Phase.BattleSelection; b.CurrentBattleProvince = null;
        var ringB = Add(b, Earth, 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(b);
        Check(eb.QueryActions(0).Where(a => a.CardId == ringB.Id && a.Type == "useAbility" && a.Command?.AbilityId is "ring-of-earth-raise" or "ring-of-earth-lower").All(o => !o.Enabled), "not between battles");
    }

    private static void OtherTrueRingsFromHand()
    {
        // Ring of Air (Battle/Open): straighten your bowed Personality.
        var a = BattleTable();
        var air = Add(a, Air, 0, Zone.Hand);
        var bowed = Add(a, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var ea = MatchEngine.FromTestState(a);
        var straighten = Offered(ea, air, "ring-of-air-straighten").Single();
        Check(straighten.Enabled, straighten.Reason ?? "Air is offered enabled");
        Check(straighten.TargetIds?.Contains(bowed.Id) == true, "with the bowed Personality as a target");
        Accept(ea, straighten.Command! with { TargetId = bowed.Id });
        Check(!ea.State.Cards[bowed.Id].Bowed); Eq(ea.State.Cards[air.Id].Zone, Zone.FateDiscard);

        // Ring of Water (Battle, Absent): move your Personality home.
        var w = BattleTable();
        var water = Add(w, Water, 0, Zone.Hand);
        var mine = Add(w, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ew = MatchEngine.FromTestState(w);
        var home = Offered(ew, water, "ring-of-water-move-home").Single();
        Check(home.Enabled, home.Reason ?? "Water is offered enabled");
        Check(home.TargetIds?.Contains(mine.Id) == true, "with the Personality at the battlefield as a target");
        Accept(ew, home.Command! with { TargetId = mine.Id });
        Eq(ew.State.Cards[mine.Id].Zone, Zone.Home); Eq(ew.State.Cards[water.Id].Zone, Zone.FateDiscard);

        // Ring of the Void (Open): draw a card.
        var v = MatchEngine.CreateEconomyScenario().State; v.Phase = Phase.Action; v.ActivePlayerId = 0; v.PriorityPlayerId = 0;
        var voidRing = Add(v, Void, 0, Zone.Hand);
        while (v.Players[1].Hand.Count <= v.Players[0].Hand.Count) Add(v, "training-follower", 1, Zone.Hand); // the Open draws only while an opponent holds more cards (issue #1157)
        var ev = MatchEngine.FromTestState(v);
        var libraryBefore = ev.State.Players[0].FateDeck.Count;
        var draw = Offered(ev, voidRing, "ring-of-void-draw").Single();
        Check(draw.Enabled, draw.Reason ?? "Void is offered enabled");
        Accept(ev, draw.Command!);
        Eq(ev.State.Cards[voidRing.Id].Zone, Zone.FateDiscard); Eq(ev.State.Players[0].FateDeck.Count, libraryBefore - 1, "a card was drawn");

        // Ring of Fire (Battle): destroy your performing Personality and an enemy Personality of equal or lower Chi.
        var f = BattleTable();
        var fire = Add(f, Fire, 0, Zone.Hand);
        var performer = Add(f, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var victim = Add(f, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var ef = MatchEngine.FromTestState(f);
        var destroy = ef.QueryActions(0).Where(o => o.Type == "useAbility" && o.CardId == fire.Id && o.Command?.AbilityId == "ring-of-fire-destroy").ToArray();
        Check(destroy.Length > 0 && destroy.All(o => o.Enabled), destroy.FirstOrDefault(o => !o.Enabled)?.Reason ?? "Fire is offered");
        var pick = destroy.First().Command!;
        Accept(ef, pick with { PerformerId = performer.Id, TargetId = victim.Id });
        Eq(ef.State.Cards[fire.Id].Zone, Zone.FateDiscard); Eq(ef.State.Cards[performer.Id].Zone, Zone.Dead); Eq(ef.State.Cards[victim.Id].Zone, Zone.Dead);
    }

    // ---- ruling 2: recruit grants -----------------------------------------------------------------------------
    // Two battlefields, attacker (player 0) wins both against a lone enemy Personality, so the Ring in hand is granted at each resolution.
    private static MatchState TwoBattles(out CardInstance ring)
    {
        var s = BattleTable(1, 0, 0, 1);
        ring = Add(s, Earth, 0, Zone.Hand);
        foreach (var province in new[] { 0, 1 })
        {
            Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: province);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: province);
        }
        return s;
    }

    private static bool RecruitOffered(MatchEngine e, CardInstance ring, string ability = "ring-of-earth-recruit") =>
        e.QueryActions(0).Any(a => a.Type == "useAbility" && a.CardId == ring.Id && a.Command?.AbilityId == ability && a.Enabled);

    private static void Resolve(MatchEngine e) { Accept(e, Command(e, "pass")); Accept(e, Command(e, "pass")); }

    private static void GrantBelongsToItsBattle()
    {
        var e = MatchEngine.FromTestState(TwoBattles(out var ring));
        Check(!RecruitOffered(e, ring), "before the battle resolves there is no grant to offer");
        Resolve(e);
        Eq(e.State.PendingChoice?.Type, "battleRecruit", "the win at battle 1 opens the after-battle window");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.Battle == 1 && g.ScopeCardIds?.SequenceEqual(new[] { ring.Id }) == true), "a grant tagged with battle 1");
        Check(RecruitOffered(e, ring), "offered at the battle it was granted at");
        Eq(e.State.Cards[ring.Id].Zone, Zone.Hand);

        // Decline the grant (the holder passes; the opponent has nothing to play and is not asked to pass, issue #644): the battle finishes and the Ring stays in the hand, its grant gone.
        Accept(e, Command(e, "declineReaction"));
        Eq(e.State.Phase, Phase.BattleSelection); Check(e.State.PendingChoice is null);
        Check(e.State.AdditionalActions.Count == 0, "the battle 1 grant lapsed with battle 1");
        Eq(e.State.Cards[ring.Id].Zone, Zone.Hand, "the Ring was not recruited");

        // Battle 2, same turn: nothing is offered and the play is refused.
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 1));
        Eq(e.State.BattleNumber, 2);
        Check(!RecruitOffered(e, ring), "not offered at battle 2");
        Reject(e, new GameCommand("useAbility", e.State.PriorityPlayerId, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-recruit"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-recruit"));

        // Winning battle 2 grants it afresh -- for battle 2 only.
        Resolve(e);
        Eq(e.State.PendingChoice?.Type, "battleRecruit");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.Battle == 2), "a new grant tagged with battle 2");
        Check(RecruitOffered(e, ring), "offered again, at battle 2");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == ring.Id && a.Command?.AbilityId == "ring-of-earth-recruit").Command!);
        Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "put into play from the hand at battle 2");
        Check(e.State.AdditionalActions.All(g => g.ScopeCardIds?.Contains(ring.Id) != true), "the grant is spent");
    }

    private static void AfterBattleWindow()
    {
        var table = TwoBattles(out var ring);
        // At Any Cost (a dead-pile recruit at the current battlefield, not a grant) keeps its Battle presence requirement and is not part of the window.
        var atAnyCost = Add(table, "oracle-l5r-639", 0, Zone.Hand); Add(table, "oracle-l5r-9539", 0, Zone.Dead);
        Check(!AbilityCatalog.All["at-any-cost-recruit"].Absent, "At Any Cost is not Absent");
        var e = MatchEngine.FromTestState(table);
        Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.CardId == atAnyCost.Id && a.Command?.AbilityId == "at-any-cost-recruit"), "At Any Cost is offered in the battle itself");
        Resolve(e);
        Check(!e.QueryActions(0).Any(a => a.CardId == atAnyCost.Id), "and is not offered in the after-battle window");
        // Order Seppuku is an Open action and may not interrupt an open window (the window is a PendingChoice, which SeppukuTiming already treats as pending).
        Check(e.SeppukuTimingReason(0) is not null && e.SeppukuTimingReason(1) is not null, "no Seppuku while the window is open");
        // The window lists the recruit, and only recruits (no other action), plus the pass.
        var listed = e.QueryActions(0);
        Check(listed.Any(a => a.Type == "declineReaction"), "it can be passed");
        Check(listed.Where(a => a.Type == "useAbility").All(a => a.Command?.AbilityId is "ring-of-earth-recruit"), "only the recruit is offered in it");
        // The opponent has nothing to play.
        Eq(e.State.PriorityPlayerId, 0, "the holder acts first"); Check(!e.QueryActions(1).Any(a => a.Type == "useAbility" && a.Enabled), "the opponent has nothing to play");
        // Absent: no unit of the holder's is needed at the battlefield -- send them home first and it is still offered.
        var s = e.State;
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.ProvinceIndex == 0)) { c.Zone = Zone.Home; c.ProvinceIndex = null; }
        var e2 = MatchEngine.FromTestState(s);
        Check(RecruitOffered(e2, ring), "no presence is needed");
        // The holder passes; the opponent holds nothing to play and is not asked to pass (issue #644): the window closes and the battle moves on.
        Accept(e2, Command(e2, "declineReaction"));
        Check(e2.State.PendingChoice is null && e2.State.Phase == Phase.BattleSelection, "the holder's pass closes it");
        Check(e2.State.AdditionalActions.Count == 0 && e2.State.Cards[ring.Id].Zone == Zone.Hand);
    }

    private static void GrantSurvivesItsBattlesActions()
    {
        // Ring of Air's grant sits ready for this battle (Battle 1); an Earth Ring in play supplies ordinary Battle actions of the holder to spend.
        var s = BattleTable(1, 0, 0);
        var air = Add(s, Air, 0, Zone.Hand);
        var earth = Add(s, Earth, 0, Zone.Home);
        Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        s.AdditionalActions.Add(new AdditionalActionGrant(0, [air.Id], Battle: 1));
        var e = MatchEngine.FromTestState(s);
        Check(RecruitOffered(e, air, "ring-of-air-recruit"), "offered at its battle");
        // The holder takes an ordinary action (which forfeits any other Additional Action grant): the recruit grant remains.
        Accept(e, EarthOption(e, earth, "ring-of-earth-raise", 1).Command! with { Province = 0 });
        Check(e.State.AdditionalActions.Any(g => g.ScopeCardIds?.Contains(air.Id) == true), "still granted after the holder's own action");
        // The opponent passes, the holder passes (a pass also forfeits ordinary grants): still granted, and priority keeps alternating normally.
        Accept(e, Command(e, "pass")); Accept(e, Command(e, "pass"));
        Check(e.State.PendingChoice?.Type == "battleRecruit" || e.State.Phase == Phase.Battle, "the battle is resolving");
        if (e.State.Phase == Phase.Battle && e.State.PendingChoice is null) Resolve(e);
        Eq(e.State.PendingChoice?.Type, "battleRecruit", "the grant is still there for the after-battle window");
        Check(RecruitOffered(e, air, "ring-of-air-recruit"), "and offered in it");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == air.Id && a.Command?.AbilityId == "ring-of-air-recruit").Command!);
        Eq(e.State.Cards[air.Id].Zone, Zone.Home);
    }

    private static void GrantMadeBetweenBattles()
    {
        // The Ring of Air test's own setup: the second Favor action of the turn, taken in the Action phase, grants the recruit.
        var s = MatchEngine.CreateEconomyScenario().State; s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Action; s.FavorPlayerId = 0; s.Players[0].FavorActionsThisTurn = 1; s.BattleNumber = 0;
        var ring = Add(s, "training-ring-of-air", 0, Zone.Hand);
        var shunya = Add(s, "training-honor-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: shunya.Id, AbilityId: "shunya-honor"));
        var grant = e.State.AdditionalActions.Single(g => g.ScopeCardIds?.Contains(ring.Id) == true);
        Eq(grant.Battle, 1, "made between battles: it belongs to the first battle to be fought");
        Check(!RecruitOffered(e, ring, "ring-of-air-recruit"), "not usable in the Action phase");
        // A grant for a later battle is not usable at an earlier one, however it got there: refused at battle 1, accepted at battle 2; and with a
        // grant for each battle on the same Ring, using it spends the one that belongs to the battle being fought.
        foreach (var (now, granted, usable) in new[] { (1, 2, false), (2, 2, true) })
        {
            var b = BattleTable(now, 0, 0); var r = Add(b, Air, 0, Zone.Hand);
            b.AdditionalActions.Add(new AdditionalActionGrant(0, [r.Id], Battle: granted));
            var eb = MatchEngine.FromTestState(b);
            Eq(RecruitOffered(eb, r, "ring-of-air-recruit"), usable, $"grant for battle {granted} at battle {now}");
            if (!usable) Reject(eb, new GameCommand("useAbility", 0, eb.Version, CardId: r.Id, AbilityId: "ring-of-air-recruit"));
        }
        var two = BattleTable(2, 0, 0); var both = Add(two, Air, 0, Zone.Hand);
        two.AdditionalActions.Add(new AdditionalActionGrant(0, [both.Id], Battle: 1)); two.AdditionalActions.Add(new AdditionalActionGrant(0, [both.Id], Battle: 2));
        var etwo = MatchEngine.FromTestState(two);
        Accept(etwo, new GameCommand("useAbility", 0, etwo.Version, CardId: both.Id, AbilityId: "ring-of-air-recruit"));
        Eq(etwo.State.AdditionalActions.Single().Battle, 1, "the grant for battle 2 was the one spent");
        // Ending the turn without that battle ever being fought drops it.
        var t = e.State; t.Phase = Phase.Dynasty; t.PriorityPlayerId = 0;
        var et = MatchEngine.FromTestState(t);
        Accept(et, new GameCommand("endDynasty", 0, et.Version));
        Check(et.State.AdditionalActions.Count == 0, "gone at the end of the turn");
    }

    private static void GrantsCarryTheirBattle()
    {
        // The engine's own recruit grants, at a battle's resolution: Ring of Earth (a win), Fire (destroyed an enemy card), and the False Ring of Air.
        foreach (var (definition, ability) in new[] { (Earth, "ring-of-earth-recruit"), (Fire, "ring-of-fire-recruit"), ("training-false-ring-of-air", "false-ring-of-air-recruit") })
        {
            var s = BattleTable(1, 0, 0);
            var ring = Add(s, definition, 0, Zone.Hand);
            Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Resolve(e);
            var grant = e.State.AdditionalActions.Single(g => g.ScopeCardIds?.Contains(ring.Id) == true);
            Eq(grant.Battle, 1, ability);
        }
        // A duel-won False Ring of Earth (Dueling) and the six ids the scoping covers are exactly the grant-based recruits.
        var scoped = AbilityCatalog.All.Where(p => p.Key.EndsWith("-recruit") && p.Value.Absent).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Eq(string.Join(",", scoped), "false-ring-of-air-recruit,false-ring-of-earth-recruit,ring-of-air-recruit,ring-of-earth-recruit,ring-of-fire-recruit,ring-of-void-recruit,ring-of-water-recruit,wl-false-ring-of-void-recruit");
        // Every other Additional Action grant stays untagged (it is cleared at battle end whatever its battle).
        var t = BattleTable(1, 0, 0);
        var host = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        t.AdditionalActions.Add(new AdditionalActionGrant(0, null));
        Check(t.AdditionalActions.All(g => g.Battle is null));
        var et = MatchEngine.FromTestState(t);
        Accept(et, Command(et, "pass")); Accept(et, Command(et, "pass"));
        Check(et.State.AdditionalActions.Count == 0, "an ordinary grant does not survive its battle");
    }
}
