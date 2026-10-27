using L5R.Core;
using static RegressionAssertions;

// Promotional-Celestial group D (item-wave-9401, spell-wave-9402), driven through QueryActions and the commands the engine offers. Positions are built on the economy table
// (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the current battlefield).
//
// Readings the tests document (none is a confirmed ruling):
//   * All Within the World is two Battle/Open entries sharing one use; EVERY Ring you control that shares an element keyword with the Shugenja is a legal Ring to straighten.
//   * Hunger of the Fire: Force <= max(the Shugenja's Force, the Shugenja's Chi); the unit Personality's Force is his current Force.
internal static class PromoCelestialDTests
{
    private const string AllWithin = "oracle-l5r-326", Channeling = "oracle-l5r-1296", ColonistsMark = "oracle-l5r-1432", HungerOfFire = "oracle-l5r-3517", InexpensiveArmor = "oracle-l5r-3742";
    private const string RideTheBreeze = "oracle-l5r-6317";
    private const string FireShugenja = "training-seeds5-fire-shugenja", WaterShugenja = "training-water-shugenja", Samurai = "training-lion-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId);
    private static CardInstance C(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];

    public static void Run(TestRunner runner) => runner.Run("promo-celestial-d", [
        ("Published shape: manifests, stats, keywords, text, abilities and art of all five cards", PublishedShape),
        ("All Within the World: straightens a Ring sharing an element with the Shugenja, or the Shugenja himself, once; refused without a shared element", AllWithinTheWorld),
        ("Channeling the Fallen: +5F to its Shugenja; bows for a Ranged 5 Attack, Ranged 6 if he is Fire", ChannelingTheFallen),
        ("Inexpensive Armor: Ranged Attacks targeting its wielder have -2 strength; Melee ones do not", InexpensiveArmorScenario),
        ("Hunger of the Fire: destroys a bare enemy unit card whose Personality has Force no higher than the Shugenja's Force or Chi; costs the Spell and bows a non-Fire Shugenja", HungerOfTheFire),
        ("Colonist's Mark: discarding a card negates the wielder's movement from a targeting action; declining lets it happen", ColonistsMarkScenario),
    ]);

    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));
    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(new HashSet<string>(listed).SetEquals(ids), $"{wave} manifest is exactly its wired cards");
        foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
    }

    private static void PublishedShape()
    {
        Manifest("item-wave-9401", InexpensiveArmor, ColonistsMark);
        Manifest("spell-wave-9402", AllWithin, Channeling, HungerOfFire);

        var armor = CardCatalog.Get(InexpensiveArmor);
        Check(armor.Type == CardType.Item && armor.Name == "Inexpensive Armor" && armor.GoldCost == 0 && armor.Focus == 2 && armor.Keywords!.SequenceEqual(["Armor"])
            && armor.AttackDefenseKind == "Ranged" && armor.AttackDefenseAmount == -2 && armor.AttackDefenseScope == "Wielder" && (armor.Abilities ?? []).Length == 0, "Inexpensive Armor's shape");
        var mark = CardCatalog.Get(ColonistsMark);
        Check(mark.Type == CardType.Item && mark.GoldCost == 0 && mark.Focus == 3 && mark.Force == 0 && mark.Chi == 0 && mark.Abilities!.SequenceEqual([PromoCelestialDAbilities.ColonistsMarkNegate]), "Colonist's Mark's shape");
        var all = CardCatalog.Get(AllWithin);
        Check(all.Type == CardType.Spell && all.GoldCost == 0 && all.Focus == 4 && all.Keywords!.SequenceEqual(["Unique"]) && all.Abilities!.SequenceEqual([PromoCelestialDAbilities.AllWithinRing, PromoCelestialDAbilities.AllWithinSelf]), "All Within the World's shape");
        var chan = CardCatalog.Get(Channeling);
        Check(chan.Type == CardType.Spell && chan.GoldCost == 8 && chan.Focus == 2 && chan.Keywords!.SequenceEqual(["Fire"]) && chan.Effect == PromoCelestialDAbilities.ChannelingForceEffect
            && chan.Abilities!.SequenceEqual([PromoCelestialDAbilities.ChannelingAttack]), "Channeling the Fallen's shape");
        var hunger = CardCatalog.Get(HungerOfFire);
        Check(hunger.Type == CardType.Spell && hunger.GoldCost == 0 && hunger.Focus == 2 && hunger.Keywords!.SequenceEqual(["Fire"]) && hunger.Abilities!.SequenceEqual([PromoCelestialDAbilities.HungerDestroy]), "Hunger of the Fire's shape");
    }

    // ---- All Within the World -------------------------------------------------------------------------------------------------------

    private static void AllWithinTheWorld()
    {
        // A Fire Shugenja with a Fire and an Air Ring: only the Fire Ring shares an element, so only it is a legal Ring; the Shugenja himself is the other way.
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, FireShugenja, 0, Zone.Home, bowed: true);
        var spell = Add(s, AllWithin, 0, Zone.Attached, parent: shugenja.Id);
        var fire = Add(s, "training-ring-of-fire-stub", 0, Zone.Home, bowed: true);
        var air = Add(s, "training-ring-of-air", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, PromoCelestialDAbilities.AllWithinRing, spell.Id, fire.Id, shugenja.Id));
        Check(!C(e, fire).Bowed, "the Fire Ring is straightened");
        Check(C(e, air).Bowed && C(e, shugenja).Bowed, "nothing else was straightened");
        Check(!Batch336Support.CanUse(e, 0, PromoCelestialDAbilities.AllWithinSelf, spell.Id, shugenja.Id), "the other way is spent: one use between the two ways");

        // The Air Ring is not a legal target (no shared element).
        var s2 = Table(Phase.Action, 0);
        var shugenja2 = Add(s2, FireShugenja, 0, Zone.Home, bowed: true);
        var spell2 = Add(s2, AllWithin, 0, Zone.Attached, parent: shugenja2.Id);
        var air2 = Add(s2, "training-ring-of-air", 0, Zone.Home, bowed: true);
        var fire2 = Add(s2, "training-ring-of-fire-stub", 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, Use(e2, 0, PromoCelestialDAbilities.AllWithinRing, spell2.Id, air2.Id, shugenja2.Id));
        // The Shugenja himself is the other way (a shared Fire Ring is in play).
        Accept(e2, Use(e2, 0, PromoCelestialDAbilities.AllWithinSelf, spell2.Id, null, shugenja2.Id));
        Check(!C(e2, shugenja2).Bowed && C(e2, fire2).Bowed && C(e2, air2).Bowed, "the Shugenja straightens, no Ring does");

        // A Shugenja with two shared elements: every Ring sharing one is a legal Ring (the chosen reading).
        var s3 = Table(Phase.Action, 0);
        var dual = Add(s3, FireShugenja, 0, Zone.Home); dual.GrantedKeywords.Add(new("Water", "test-fixture", GrantDuration.Permanent));
        var spell3 = Add(s3, AllWithin, 0, Zone.Attached, parent: dual.Id);
        var fire3 = Add(s3, "training-ring-of-fire-stub", 0, Zone.Home, bowed: true);
        var water3 = Add(s3, "training-ring-of-water", 0, Zone.Home, bowed: true);
        var e3 = MatchEngine.FromTestState(s3);
        var offered = Batch336Support.Options(e3, 0, PromoCelestialDAbilities.AllWithinRing, spell3.Id, dual.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToHashSet();
        Check(offered.SetEquals([fire3.Id, water3.Id]), "both Rings sharing an element are offered");

        // No Ring shares an element (or no Ring at all): neither way is available and nothing mutates.
        var s4 = Table(Phase.Action, 0);
        var shugenja4 = Add(s4, FireShugenja, 0, Zone.Home, bowed: true);
        var spell4 = Add(s4, AllWithin, 0, Zone.Attached, parent: shugenja4.Id);
        Add(s4, "training-ring-of-air", 0, Zone.Home, bowed: true);
        Add(s4, "training-ring-of-fire-stub", 1, Zone.Home, bowed: true); // a Fire Ring the OPPONENT controls does not count
        var e4 = MatchEngine.FromTestState(s4);
        Check(!Batch336Support.CanUse(e4, 0, PromoCelestialDAbilities.AllWithinSelf, spell4.Id, shugenja4.Id), "no shared element with a Ring you control: not usable");
        Reject(e4, Use(e4, 0, PromoCelestialDAbilities.AllWithinSelf, spell4.Id, null, shugenja4.Id));
        Check(C(e4, shugenja4).Bowed, "a rejected use does not straighten him");

        // Usable in a battle too (Battle/Open).
        var s5 = Table(Phase.Battle, 0);
        var shugenja5 = Add(s5, FireShugenja, 0, Zone.Battlefield, bowed: true, province: 0);
        var spell5 = Add(s5, AllWithin, 0, Zone.Attached, parent: shugenja5.Id, province: 0);
        Add(s5, "training-ring-of-fire-stub", 0, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Use(e5, 0, PromoCelestialDAbilities.AllWithinSelf, spell5.Id, null, shugenja5.Id));
        Check(!C(e5, shugenja5).Bowed, "straightened in a battle");
    }

    // ---- Channeling the Fallen ------------------------------------------------------------------------------------------------------

    private static void ChannelingTheFallen()
    {
        // +5F to the Shugenja, whatever his element; nobody else.
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, FireShugenja, 0, Zone.Home); // 2F
        var other = Add(s, Samurai, 0, Zone.Home);
        var before = MatchEngine.FromTestState(s);
        var otherForce = before.CardForceForTest(other.Id);
        var spell = Add(s, Channeling, 0, Zone.Attached, parent: shugenja.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(shugenja.Id), 2 + 5, "+5F");
        Eq(e.CardForceForTest(other.Id), otherForce, "no other card gains Force");

        // Ranged 6 for a Fire Shugenja: destroys a 6F target; Ranged 5 for a non-Fire one: spares a 6F target but destroys a 5F one.
        MatchEngine Battle(string shugenjaDef, int targetForce, out CardInstance sh, out CardInstance sp, out CardInstance target)
        {
            var b = Table(Phase.Battle, 0);
            sh = Add(b, shugenjaDef, 0, Zone.Battlefield, province: 0);
            sp = Add(b, Channeling, 0, Zone.Attached, province: 0, parent: sh.Id);
            target = Add(b, Samurai, 1, Zone.Battlefield, province: 0);
            Batch336Support.Force(target, targetForce - MatchEngine.FromTestState(b).CardForceForTest(target.Id));
            return MatchEngine.FromTestState(b);
        }
        var fire = Battle(FireShugenja, 6, out var fireShugenja, out var fireSpell, out var sixF);
        Accept(fire, Use(fire, 0, PromoCelestialDAbilities.ChannelingAttack, fireSpell.Id, sixF.Id));
        Eq(C(fire, sixF).Zone, Zone.Dead, "Fire: Ranged 6 destroys a 6F target");
        Check(C(fire, fireSpell).Bowed && !C(fire, fireShugenja).Bowed, "the Spell is bowed as the cost, not the Shugenja");

        var water = Battle(WaterShugenja, 6, out var waterShugenja, out var waterSpell, out var sixF2);
        Accept(water, Use(water, 0, PromoCelestialDAbilities.ChannelingAttack, waterSpell.Id, sixF2.Id));
        Check(C(water, sixF2).Zone != Zone.Dead, "non-Fire: Ranged 5 does not destroy a 6F target");

        var water5 = Battle(WaterShugenja, 5, out var ws5, out var wsp5, out var fiveF);
        Accept(water5, Use(water5, 0, PromoCelestialDAbilities.ChannelingAttack, wsp5.Id, fiveF.Id));
        Eq(C(water5, fiveF).Zone, Zone.Dead, "non-Fire: Ranged 5 destroys a 5F target");

        // A bowed Spell cannot attack.
        var bowed = Battle(FireShugenja, 3, out var bs, out var bsp, out var bt);
        var st = bowed.State; st.Cards[bsp.Id].Bowed = true;
        var rebuilt = MatchEngine.FromTestState(st);
        Reject(rebuilt, Use(rebuilt, 0, PromoCelestialDAbilities.ChannelingAttack, bsp.Id, bt.Id));
    }

    // ---- Inexpensive Armor ----------------------------------------------------------------------------------------------------------

    private static void InexpensiveArmorScenario()
    {
        // Channeling the Fallen from a Fire Shugenja is Ranged 6: against a 5F wielder wearing the Armor it is 4 and spares him; without the Armor it destroys him.
        MatchEngine Battle(bool armored, out CardInstance sp, out CardInstance sh, out CardInstance target)
        {
            var b = Table(Phase.Battle, 0);
            sh = Add(b, FireShugenja, 0, Zone.Battlefield, province: 0);
            sp = Add(b, Channeling, 0, Zone.Attached, province: 0, parent: sh.Id);
            target = Add(b, Samurai, 1, Zone.Battlefield, province: 0);
            Batch336Support.Force(target, 5 - MatchEngine.FromTestState(b).CardForceForTest(target.Id));
            if (armored) Add(b, InexpensiveArmor, 1, Zone.Attached, province: 0, parent: target.Id);
            return MatchEngine.FromTestState(b);
        }
        var with = Battle(true, out var sp1, out var sh1, out var t1);
        Accept(with, Use(with, 0, PromoCelestialDAbilities.ChannelingAttack, sp1.Id, t1.Id));
        Check(C(with, t1).Zone != Zone.Dead, "Ranged 6 - 2 = 4 does not destroy the armored 5F wielder");
        var without = Battle(false, out var sp2, out var sh2, out var t2);
        Accept(without, Use(without, 0, PromoCelestialDAbilities.ChannelingAttack, sp2.Id, t2.Id));
        Eq(C(without, t2).Zone, Zone.Dead, "without the Armor Ranged 6 destroys the 5F Personality");

        // Melee Attacks are not weakened: Daigotsu Kazuko's Melee 3 destroys a 3F armored wielder.
        var m = Table(Phase.Battle, 1);
        var wielder = Add(m, Samurai, 0, Zone.Battlefield, province: 0);
        Batch336Support.Force(wielder, 3 - MatchEngine.FromTestState(m).CardForceForTest(wielder.Id));
        Add(m, InexpensiveArmor, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var kazuko = Add(m, "oracle-l5r-1734", 1, Zone.Battlefield, province: 0);
        var em = MatchEngine.FromTestState(m);
        Accept(em, Batch336Support.Offer(em, 1, "kazuko-fear-melee", kazuko.Id, wielder.Id));
        for (var i = 0; i < 8 && em.State.PendingChoice is not null; i++) Accept(em, new GameCommand("declineReaction", em.State.PendingChoice.PlayerId, em.Version));
        Check(C(em, wielder).Zone == Zone.Dead, "Melee 3 is not weakened by Armor that only covers Ranged Attacks");
    }

    // ---- Hunger of the Fire ---------------------------------------------------------------------------------------------------------

    private static void HungerOfTheFire()
    {
        MatchEngine Battle(string shugenjaDef, int targetForce, out CardInstance sh, out CardInstance sp, out CardInstance target, Action<MatchState, CardInstance>? more = null, int shugenjaForceBonus = 0)
        {
            var b = Table(Phase.Battle, 0);
            sh = Add(b, shugenjaDef, 0, Zone.Battlefield, province: 0); // Fire: 2F/3C, Water: 2F/2C
            if (shugenjaForceBonus != 0) Batch336Support.Force(sh, shugenjaForceBonus);
            sp = Add(b, HungerOfFire, 0, Zone.Attached, province: 0, parent: sh.Id);
            target = Add(b, Samurai, 1, Zone.Battlefield, province: 0);
            Batch336Support.Force(target, targetForce - MatchEngine.FromTestState(b).CardForceForTest(target.Id));
            more?.Invoke(b, target);
            return MatchEngine.FromTestState(b);
        }
        // Fire Shugenja (2F, 3C): a 3F target is legal by Chi; he is not bowed; the Spell is destroyed; the bare Personality is destroyed.
        var e = Battle(FireShugenja, 3, out var fire, out var spell, out var three);
        Accept(e, Use(e, 0, PromoCelestialDAbilities.HungerDestroy, spell.Id, three.Id, fire.Id));
        Eq(C(e, three).Zone, Zone.Dead, "the 3F bare Personality is destroyed");
        Check(!C(e, fire).Bowed, "a Fire Shugenja is not bowed");
        Check(C(e, spell).Zone is Zone.FateDiscard, "the Spell is destroyed as a cost");

        // A 4F target is out of reach (Force 2, Chi 3): rejected, nothing changes.
        var high = Battle(FireShugenja, 4, out var fire2, out var spell2, out var four);
        Check(!Batch336Support.CanUse(high, 0, PromoCelestialDAbilities.HungerDestroy, spell2.Id, fire2.Id) || !(Batch336Support.Options(high, 0, PromoCelestialDAbilities.HungerDestroy, spell2.Id, fire2.Id)
            .Any(a => a.Enabled && (a.TargetIds?.Contains(four.Id) ?? false))), "a 4F target is not offered");
        Reject(high, Use(high, 0, PromoCelestialDAbilities.HungerDestroy, spell2.Id, four.Id, fire2.Id));
        Check(C(high, spell2).Zone == Zone.Attached && C(high, four).Zone == Zone.Battlefield, "a rejected use costs nothing");

        // Force raises the ceiling: the same Shugenja at 5F (Chi 3) reaches a 5F target by Force.
        var byForce = Battle(FireShugenja, 5, out var fire3, out var spell3, out var five, shugenjaForceBonus: 3);
        Accept(byForce, Use(byForce, 0, PromoCelestialDAbilities.HungerDestroy, spell3.Id, five.Id, fire3.Id));
        Eq(C(byForce, five).Zone, Zone.Dead, "Force 5 reaches a 5F target");

        // A non-Fire Shugenja (2F, 2C) is bowed, and cannot reach a 3F target.
        var water = Battle(WaterShugenja, 2, out var wsh, out var wsp, out var two);
        Accept(water, Use(water, 0, PromoCelestialDAbilities.HungerDestroy, wsp.Id, two.Id, wsh.Id));
        Check(C(water, wsh).Bowed && C(water, two).Zone == Zone.Dead, "a non-Fire Shugenja is bowed and the 2F target is destroyed");
        var waterHigh = Battle(WaterShugenja, 3, out var wsh2, out var wsp2, out var threeW);
        Reject(waterHigh, Use(waterHigh, 0, PromoCelestialDAbilities.HungerDestroy, wsp2.Id, threeW.Id, wsh2.Id));

        // "A card without attachments in a unit": a Personality carrying an Item is not legal, but the Item (bare) is, judged by the Personality's Force.
        CardInstance? item = null;
        var carried = Battle(FireShugenja, 3, out var fire4, out var spell4, out var host, (b, t) => item = Add(b, InexpensiveArmor, 1, Zone.Attached, province: 0, parent: t.Id));
        Reject(carried, Use(carried, 0, PromoCelestialDAbilities.HungerDestroy, spell4.Id, host.Id, fire4.Id));
        Accept(carried, Use(carried, 0, PromoCelestialDAbilities.HungerDestroy, spell4.Id, item!.Id, fire4.Id));
        Check(C(carried, item).Zone != Zone.Attached && C(carried, host).Zone == Zone.Battlefield, "only the bare Item is destroyed, its Personality survives");

        // The same unit when its Personality is too strong: the Item is not legal either.
        CardInstance? item2 = null;
        var strong = Battle(FireShugenja, 4, out var fire5, out var spell5, out _, (b, t) => item2 = Add(b, InexpensiveArmor, 1, Zone.Attached, province: 0, parent: t.Id));
        Reject(strong, Use(strong, 0, PromoCelestialDAbilities.HungerDestroy, spell5.Id, item2!.Id, fire5.Id));

        // Your own cards are never targets.
        var own = Battle(FireShugenja, 1, out var fire6, out var spell6, out _);
        var mine = Add(own.State, Samurai, 0, Zone.Battlefield, province: 0);
        var ownEngine = MatchEngine.FromTestState(own.State);
        Reject(ownEngine, Use(ownEngine, 0, PromoCelestialDAbilities.HungerDestroy, spell6.Id, mine.Id, fire6.Id));
    }

    // ---- Colonist's Mark ------------------------------------------------------------------------------------------------------------

    private static void ColonistsMarkScenario()
    {
        MatchEngine Build(out CardInstance wielder, out CardInstance mark, out CardInstance breeze, out CardInstance monk, out CardInstance handCard)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            wielder = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
            mark = Add(s, ColonistsMark, 0, Zone.Attached, province: 0, parent: wielder.Id); // province 0: an attachment is at the battlefield with its wielder (presence, issue #1133)
            handCard = Add(s, "training-focus-strategy", 0, Zone.Hand);
            monk = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); monk.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
            breeze = Add(s, RideTheBreeze, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // Ride the Breeze moves the targeted Personality home; the Reaction (discarding the one card in hand) negates it.
        var e = Build(out var wielder, out var mark, out var breeze, out var monk, out var handCard);
        Accept(e, Batch336Support.Offer(e, 1, "ride-the-breeze-move", breeze.Id, wielder.Id, monk.Id));
        var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoCelestialDAbilities.ColonistsMarkNegate && a.Enabled).ToArray();
        Check(offered.Length == 1 && offered[0].Command!.TargetId == handCard.Id, "the Reaction is offered once, for the one card in hand");
        // The opponent never sees the option.
        Check(!e.QueryActions(1).Any(a => a.Command?.AbilityId == PromoCelestialDAbilities.ColonistsMarkNegate), "the opponent is not offered it");
        Accept(e, offered[0].Command!);
        Eq(C(e, handCard).Zone, Zone.FateDiscard, "the card was discarded as the cost");
        for (var i = 0; i < 6 && e.State.PendingChoice is not null && e.State.PendingChoice.Type != "effectChoice"; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(C(e, wielder).Zone, Zone.Battlefield, "his movement from the action was negated");

        // Declining: he moves home and the card stays in hand.
        var d = Build(out var wielder2, out _, out var breeze2, out var monk2, out var hand2);
        Accept(d, Batch336Support.Offer(d, 1, "ride-the-breeze-move", breeze2.Id, wielder2.Id, monk2.Id));
        for (var i = 0; i < 6 && d.State.PendingChoice is not null; i++) Accept(d, new GameCommand("declineReaction", d.State.PriorityPlayerId, d.Version));
        Eq(C(d, wielder2).Zone, Zone.Home, "moved home");
        Eq(C(d, hand2).Zone, Zone.Hand, "nothing was discarded");

        // With an empty hand there is no card to discard: the Reaction is not usable.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var w3 = Add(s3, Samurai, 0, Zone.Battlefield, province: 0);
        Add(s3, ColonistsMark, 0, Zone.Attached, province: 0, parent: w3.Id);
        var m3 = Add(s3, "training-crane-samurai", 1, Zone.Battlefield, province: 0); m3.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
        var b3 = Add(s3, RideTheBreeze, 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Batch336Support.Offer(e3, 1, "ride-the-breeze-move", b3.Id, w3.Id, m3.Id));
        Check(!e3.QueryActions(0).Any(a => a.Command?.AbilityId == PromoCelestialDAbilities.ColonistsMarkNegate && a.Enabled), "no card to discard: not usable");
    }
}
