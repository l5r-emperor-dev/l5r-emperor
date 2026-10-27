using L5R.Core;
using static RegressionAssertions;

// Issue #503, group P4: the watches, shields and locks (src/L5R.Core/EmperorP4Watches.cs).
internal static partial class EmperorP4Tests
{
    private const string DrawingOnTheMountain = "oracle-l5r-2236", MenacingSky = "oracle-l5r-8219", FirmCensure = "oracle-l5r-2580", ScarsOfWar = "oracle-l5r-6490",
        ShowOfRestraint = "oracle-l5r-7197", SnowHasTeeth = "oracle-l5r-8342", Hunger = "oracle-l5r-3514", TooCloseToHome = "oracle-l5r-8633",
        EarthFlows = "oracle-l5r-8038", BlightedRegion = "oracle-l5r-1042", HastyFortifications = "oracle-l5r-2996";

    private static (string Name, Action Body)[] WatchTests() => [
        ("Published shape (watches): Drawing on the Mountain and The Menacing Sky (spell-wave-38), Hunger (item-wave-30), Too Close to Home (event-wave-21), Firm Censure, Scars of War, Show of Restraint, The Snow Has Teeth (strategy-wave-94)", WatchesPublishedShape),
        ("Drawing on the Mountain: while its Shugenja is Earth his controller's provinces have +1 strength (not otherwise)", DrawingStatic),
        ("Drawing on the Mountain: bowing the Shugenja and destroying the Spell, a bowed Personality's next straightening this game is negated -- once, even a later turn's; an unbowed Personality is no target", DrawingNegatesNextStraighten),
        ("Firm Censure: the target's next action this turn has its effects negated; a non-Samurai target without attachments is bowed, a Samurai is not", FirmCensureNegatesNextAction),
        ("Scars of War: after each time the Holding bows its controller loses 1 Honor (a bow noticed once, and again after it straightens and bows again); another Holding costs nothing", ScarsOfWarBows),
        ("Show of Restraint: current Force penalties on the Monk are removed; with a Weapon the next effect that bows him is negated (not the one after); without a Weapon nothing is shielded", ShowOfRestraintShield),
        ("Show of Restraint: with a Weapon the next effect that moves him home is negated", ShowOfRestraintMoveShield),
        ("The Menacing Sky: after the watched Personality assigns he is bowed; a Thunder Shugenja is not bowed as the cost, another is", MenacingSkyAssign),
        ("The Menacing Sky: an action that moves him to a battlefield bows him as it resolves; the watch is spent", MenacingSkyMoved),
        ("The Snow Has Teeth: after engaging, the province's strength is capped at its base for the battle, the next effect moving one of its player's Personalities from the battlefield is negated (only the next), and the next Terrain to enter play is destroyed", SnowHasTeethScenario),
        ("Too Close to Home: its owner may destroy a Region as it is revealed; until his next turn begins no movement to or from battlefields happens; declining destroys nothing", TooCloseToHomeScenario),
        ("Hunger: its controller must declare an attack and assign its wielder; a player who may not attack is not bound", HungerObligation),
        ("Hunger: after an action targets its wielder his movement from that action is negated; without the Reaction he moves", HungerReaction),
    ];

    private static void WatchesPublishedShape()
    {
        Shape(DrawingOnTheMountain, CardType.Spell, "Drawing on the Mountain", "drawing-mountain-negate",
            "While this Shugenja is Earth, your provinces have +1 strength.\nBattle/Open: Bow this Shugenja, destroy this card, and target a bowed Personality: Negate his next straightening this game.", "Earth");
        Eq(CardCatalog.Get(DrawingOnTheMountain).Effect, EmperorP4WatchAbilities.DrawingOnTheMountainEffect);
        Shape(MenacingSky, CardType.Spell, "The Menacing Sky", "menacing-sky-watch",
            "Limited: Bow this Shugenja unless he is Thunder, and target a Personality: After the next time (this turn) he assigns or an action resolves which moved him to a battlefield, bow him.", "Thunder");
        Check(CardCatalog.Get(MenacingSky).GoldCost == 2 && CardCatalog.Get(MenacingSky).Focus == 2 && CardCatalog.Get(DrawingOnTheMountain).GoldCost == 0 && CardCatalog.Get(DrawingOnTheMountain).Focus == 3, "the Spells' costs and Focus");
        Manifest("spell-wave-38", DrawingOnTheMountain, MenacingSky);
        var hunger = CardCatalog.Get(Hunger);
        Check(hunger.GoldCost == 6 && hunger.Force == 5 && hunger.Chi == 1 && hunger.Focus == 4, "Hunger's stats");
        Eq(hunger.Effect, EmperorP4WatchAbilities.HungerEffect);
        Shape(Hunger, CardType.Item, "Hunger", "hunger-negate-move",
            "You must declare an attack each turn and assign this Personality to attack if possible.\nReaction: After an action targets this Personality: Negate this Personality's movement from the action's effects.", "Unique,Weapon");
        Manifest("item-wave-30", Hunger);
        Shape(TooCloseToHome, CardType.Event, "Too Close to Home", "",
            "You may choose and destroy a Region. Until your next turn begins, negate all movement to and from battlefields.");
        Eq(CardCatalog.Get(TooCloseToHome).Effect, EmperorP4WatchAbilities.TooCloseToHomeEffect);
        Manifest("event-wave-21", TooCloseToHome);
        Shape(FirmCensure, CardType.Strategy, "Firm Censure", "firm-censure-negate",
            "Open: Choose your performing Samurai and target another player's Personality or Follower: Negate the effects of the next action performed by the targeted card (this turn). Bow it if it has no attachments and is not a Samurai.");
        Shape(ScarsOfWar, CardType.Strategy, "Scars of War", "scars-of-war-mark",
            "Limited: Bow your performing Courtier and target a Holding: Until the game ends, after each time it bows or an ability on it resolves, its controller loses 1 Honor.", "Political");
        Shape(ShowOfRestraint, CardType.Strategy, "Show of Restraint", "show-of-restraint",
            "Battle: Choose your performing Monk: Negate all current Force penalties on him from actions. If he has a Weapon, negate the next card effect that moves him home or bows him.", "Kiho");
        Shape(SnowHasTeeth, CardType.Strategy, "The Snow Has Teeth", "snow-has-teeth",
            "Reaction: After engaging: The current battlefield's province has a maximum strength equal to its base strength until the end of the battle. Negate the next effect that would move one of your Personalities from its battlefield. After the next time this battle a Terrain enters play, destroy it.", "Winter");
        foreach (var (id, gold, focus) in new[] { (FirmCensure, 0, 1), (ScarsOfWar, 2, 1), (ShowOfRestraint, 0, 3), (SnowHasTeeth, 0, 1) })
            Check(CardCatalog.Get(id).GoldCost == gold && CardCatalog.Get(id).Focus == focus, $"{id}'s Gold Cost and Focus");
        Manifest("strategy-wave-94", GamesOfWill, PillarTattoo, PrecedingReputation, TooMuch, YojimboAssemble, RideTheBreeze, FirmCensure, ScarsOfWar, ShowOfRestraint, SnowHasTeeth,
            "oracle-l5r-6034", "oracle-l5r-634", "oracle-l5r-5605", "oracle-l5r-6552");
    }

    // ---- Drawing on the Mountain ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Shugenja(MatchState s, int owner, Zone zone, params string[] keywords)
    {
        var card = Add(s, Samurai, owner, zone, province: zone == Zone.Battlefield ? 0 : null);
        GiveKeyword(card, "Shugenja");
        foreach (var k in keywords) GiveKeyword(card, k);
        return card;
    }

    private static void DrawingStatic()
    {
        var s = Table(Phase.Action, 1);
        var earth = Shugenja(s, 1, Zone.Home, "Earth");
        Add(s, DrawingOnTheMountain, 1, Zone.Attached, parent: earth.Id);
        var e = MatchEngine.FromTestState(s);
        var t = Table(Phase.Action, 1);
        var plain = Shugenja(t, 1, Zone.Home);
        Add(t, DrawingOnTheMountain, 1, Zone.Attached, parent: plain.Id);
        var n = MatchEngine.FromTestState(t);
        Eq(e.ProvinceStrengthForTest(1, 0), n.ProvinceStrengthForTest(1, 0) + 1, "+1 strength while the Shugenja is Earth");
        Eq(e.ProvinceStrengthForTest(0, 0), n.ProvinceStrengthForTest(0, 0), "the other player's provinces are untouched");
    }

    private static void DrawingNegatesNextStraighten()
    {
        var s = Table(Phase.Action, 1);
        var shugenja = Shugenja(s, 1, Zone.Home, "Earth");
        var spell = Add(s, DrawingOnTheMountain, 1, Zone.Attached, parent: shugenja.Id);
        var bowed = Add(s, Samurai, 0, Zone.Home, bowed: true);
        var unbowed = Add(s, Scout, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, EmperorP4WatchAbilities.DrawingOnTheMountain, spell.Id).Any(o => o.TargetIds?.Contains(unbowed.Id) == true), "an unbowed Personality is not offered");
        Accept(e, Offer(e, 1, EmperorP4WatchAbilities.DrawingOnTheMountain, spell.Id, bowed.Id));
        Check(C(e, shugenja).Bowed, "the Shugenja bowed as a cost");
        Eq(C(e, spell).Zone, Zone.FateDiscard, "the Spell destroyed as a cost");
        var later = Edit(e, st => st.TurnNumber += 2); // "this game": still waiting turns later
        Check(!later.StraightenForTest(bowed.Id), "his next straightening is negated");
        Check(later.StraightenForTest(bowed.Id), "only the next one");
    }

    // ---- Firm Censure -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FirmCensureNegatesNextAction()
    {
        var s = Table(Phase.Action, 1);
        var samurai = Add(s, Scout, 1, Zone.Home); // Samurai
        var censured = Add(s, "training-bow-enemy-source", 0, Zone.Home); GiveKeyword(censured, "Samurai"); // will not be bowed
        var victim = Add(s, Samurai, 1, Zone.Home);
        var bare = Add(s, "training-bow-any-source", 0, Zone.Home);
        var card = Add(s, FirmCensure, 1, Zone.Hand); var card2 = Add(s, FirmCensure, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4WatchAbilities.FirmCensure, card.Id, censured.Id, samurai.Id));
        Check(!C(e, censured).Bowed, "a Samurai target is not bowed");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, new GameCommand("useAbility", 0, e.Version, censured.Id, AbilityId: "training-bow-enemy", TargetId: victim.Id));
        Check(!C(e, victim).Bowed, "the censured card's next action had its effects negated");
        Check(Logged(e, "actionNegated"), "announced");
        // A non-Samurai target without attachments is bowed.
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, EmperorP4WatchAbilities.FirmCensure, card2.Id, bare.Id, samurai.Id));
        Check(C(e, bare).Bowed, "bowed: no attachments and not a Samurai");
        Check(!Options(e, 1, EmperorP4WatchAbilities.FirmCensure).Any(o => o.TargetIds?.Contains(victim.Id) == true), "an own card is never a target");
    }

    // ---- Scars of War -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ScarsOfWarBows()
    {
        var s = Table(Phase.Action, 1);
        var courtier = Add(s, "training-crane-courtier", 1, Zone.Home); GiveKeyword(courtier, "Courtier");
        var marked = Add(s, Holding, 0, Zone.Home);
        var other = Add(s, Holding, 0, Zone.Home);
        var card = Add(s, ScarsOfWar, 1, Zone.Hand);
        Add(s, "training-marketplace", 1, Zone.Home); // pays the 2 Gold
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4WatchAbilities.ScarsOfWar, card.Id, marked.Id, courtier.Id));
        Check(C(e, courtier).Bowed, "the Courtier bowed as the cost");
        var honor = Honor(e, 0);
        // One command at a time in the Action Phase: the Holding's state is set, then any command is taken (the watch looks after every command).
        MatchEngine Tick(MatchEngine m, bool markedBowed, bool otherBowed = false)
        {
            var next = Edit(m, st => { st.Cards[marked.Id].Bowed = markedBowed; st.Cards[other.Id].Bowed = otherBowed; st.Phase = Phase.Action; st.PriorityPlayerId = 1; st.ConsecutivePasses = 0; });
            Accept(next, new GameCommand("pass", 1, next.Version));
            return next;
        }
        e = Tick(e, true, true);
        Eq(Honor(e, 0), honor - 1, "it bowed: its controller lost 1 Honor (the other Holding cost nothing)");
        e = Tick(e, true, true);
        Eq(Honor(e, 0), honor - 1, "still bowed: nothing more");
        e = Tick(e, false);
        e = Tick(e, true);
        Eq(Honor(e, 0), honor - 2, "bowed again: 1 more");
        // An ability on the marked Holding resolves: its controller loses 1 Honor (it did not bow).
        var t = Table(); t.PriorityPlayerId = 0;
        var holding = Add(t, "training-bow-holding", 0, Zone.Home);
        Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var enemy = Add(t, Samurai, 1, Zone.Battlefield, province: 0);
        t.P4.ScarsMarks.Add(new(holding.Id, 1, t.TurnNumber, t.BattleNumber, "Scars of War"));
        t.P4.ScarsLastBowed[holding.Id] = false;
        var h = MatchEngine.FromTestState(t);
        var before = Honor(h, 0);
        Accept(h, new GameCommand("useAbility", 0, h.Version, holding.Id, AbilityId: "training-bow-at-battle", TargetId: enemy.Id));
        Check(C(h, enemy).Bowed, "the Holding's ability resolved");
        Eq(Honor(h, 0), before - 1, "its controller lost 1 Honor");
    }

    // ---- Show of Restraint ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShowOfRestraintShield()
    {
        var s = Table();
        var monk = Add(s, Scout, 1, Zone.Battlefield, province: 0); GiveKeyword(monk, "Monk"); // Gold Cost 3
        monk.Modifiers.Add(new("enemy", "enemy-action", "Force", -1));
        Add(s, "training-weapon", 1, Zone.Attached, parent: monk.Id);
        var enemy = Add(s, Samurai, 0, Zone.Battlefield, province: 0); // Gold Cost 5
        var restraint = Add(s, ShowOfRestraint, 1, Zone.Hand);
        var will1 = Add(s, GamesOfWill, 0, Zone.Hand); var will2 = Add(s, GamesOfWill, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4WatchAbilities.ShowOfRestraint, restraint.Id, performerId: monk.Id));
        Eq(e.CardForceForTest(monk.Id), 3, "the penalty is gone (2F + the Weapon's 1)");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, EmperorP4Abilities.GamesOfWillBow, will1.Id, monk.Id, enemy.Id));
        Check(!C(e, monk).Bowed, "the next effect that bows him is negated");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, EmperorP4Abilities.GamesOfWillBow, will2.Id, monk.Id, enemy.Id));
        Check(C(e, monk).Bowed, "the one after is not");
        // Without a Weapon: the penalty goes, no shield.
        var t = Table();
        var monk2 = Add(t, Scout, 1, Zone.Battlefield, province: 0); GiveKeyword(monk2, "Monk");
        var enemy2 = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var restraint2 = Add(t, ShowOfRestraint, 1, Zone.Hand); var will3 = Add(t, GamesOfWill, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Use(e2, 1, EmperorP4WatchAbilities.ShowOfRestraint, restraint2.Id, performerId: monk2.Id));
        e2 = Edit(e2, st => st.PriorityPlayerId = 0);
        Accept(e2, Offer(e2, 0, EmperorP4Abilities.GamesOfWillBow, will3.Id, monk2.Id, enemy2.Id));
        Check(C(e2, monk2).Bowed, "no Weapon, no shield");
    }

    private static void ShowOfRestraintMoveShield()
    {
        var s = Table();
        var monk = Add(s, Scout, 1, Zone.Battlefield, province: 0); GiveKeyword(monk, "Monk");
        Add(s, "training-weapon", 1, Zone.Attached, parent: monk.Id);
        var enemyMonk = Add(s, Samurai, 0, Zone.Battlefield, province: 0); GiveKeyword(enemyMonk, "Monk");
        var restraint = Add(s, ShowOfRestraint, 1, Zone.Hand);
        var breeze = Add(s, RideTheBreeze, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4WatchAbilities.ShowOfRestraint, restraint.Id, performerId: monk.Id));
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, EmperorP4Abilities.RideTheBreeze, breeze.Id, monk.Id, enemyMonk.Id));
        Eq(C(e, monk).Zone, Zone.Battlefield, "the move home is negated");
    }

    // ---- The Menacing Sky ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MenacingSkyAssign()
    {
        var s = Table(Phase.Action, 1);
        var thunder = Shugenja(s, 1, Zone.Home, "Thunder");
        var sky = Add(s, MenacingSky, 1, Zone.Attached, parent: thunder.Id);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4WatchAbilities.MenacingSky, sky.Id, attacker.Id));
        Check(!C(e, thunder).Bowed, "a Thunder Shugenja is not bowed as the cost");
        var declared = Edit(e, st => { st.Phase = Phase.AttackDeclaration; st.PriorityPlayerId = 1; });
        Accept(declared, new("declareAttack", 1, declared.Version));
        Accept(declared, new("attack", 1, declared.Version, Assignments: new Dictionary<string, int> { [attacker.Id] = 0 }));
        Check(C(declared, attacker).Bowed, "he assigned: bowed");
        // Another Shugenja is bowed as the cost.
        var t = Table(Phase.Action, 1);
        var plain = Shugenja(t, 1, Zone.Home);
        var sky2 = Add(t, MenacingSky, 1, Zone.Attached, parent: plain.Id);
        var target = Add(t, Samurai, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, EmperorP4WatchAbilities.MenacingSky, sky2.Id, target.Id));
        Check(C(e2, plain).Bowed, "not Thunder: bowed as the cost");
        Check(!C(e2, target).Bowed, "nothing happened to the target yet");
    }

    private static void MenacingSkyMoved()
    {
        var s = Table(); // player 1 attacks, player 0 defends
        var shugenja = Shugenja(s, 0, Zone.Battlefield);
        var flows = Add(s, EarthFlows, 0, Zone.Attached, parent: shugenja.Id);
        Add(s, Samurai, 1, Zone.Battlefield, province: 0);
        var watched = Add(s, "training-lion-samurai", 0, Zone.Home);
        s.P4.MenacingSkyWatches.Add(new(watched.Id, 1, s.TurnNumber, s.BattleNumber, "The Menacing Sky"));
        s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "earth-flows-move", flows.Id, watched.Id));
        Eq(C(e, watched).Zone, Zone.Battlefield, "moved to the battlefield");
        Check(C(e, watched).Bowed, "the action resolved: he is bowed");
        Eq(e.State.P4.MenacingSkyWatches.Count, 0, "the watch is spent");
    }

    // ---- The Snow Has Teeth --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SnowHasTeethScenario()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var attacker = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var defender = Add(s, Samurai, 1, Zone.Battlefield, province: 0);
        var defender2 = Add(s, Scout, 1, Zone.Battlefield, province: 0);
        var snow = Add(s, SnowHasTeeth, 1, Zone.Hand);
        var hasty = Add(s, HastyFortifications, 0, Zone.Hand);
        s.Players[1].Provinces[0].Strength += 3; // raised above its base
        var e = MatchEngine.FromTestState(s);
        var raised = e.ProvinceStrengthForTest(1, 0);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opened");
        if (e.State.PriorityPlayerId == 0) Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Accept(e, new GameCommand("useAbility", 1, e.Version, snow.Id, AbilityId: EmperorP4WatchAbilities.SnowHasTeeth));
        Check(e.ProvinceStrengthForTest(1, 0) < raised, "capped at its base strength");
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        // The next Terrain to enter play is destroyed.
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, new GameCommand("playStrategy", 0, e.Version, hasty.Id));
        Eq(C(e, hasty).Zone, Zone.FateDiscard, "the Terrain was destroyed as it entered play");
        // The next effect moving one of player 1's Personalities from the battlefield is negated; the one after is not.
        var moved = Edit(e, st => { });
        Check(!moved.MoveToBattlefieldForTest(defender.Id, 1), "a move to another battlefield is a move from its battlefield: negated");
        Eq(C(moved, defender).ProvinceIndex, 0, "the first move is negated");
        Check(moved.MoveToBattlefieldForTest(defender2.Id, 1), "the next one happens");
        Eq(C(moved, attacker).Zone, Zone.Battlefield, "the attacker is untouched");
    }

    // ---- Too Close to Home ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine RevealEvent(string def, out CardInstance evt, Action<MatchState>? prepare = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
        evt = Add(s, def, 1, Zone.Province, province: 3); evt.FaceUp = false; s.Players[1].Provinces[3].CardId = evt.Id;
        prepare?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Batch336Support.RunUntil(e, () => e.State.PendingChoice is { Type: "eventChoice" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        return e;
    }

    private static void TooCloseToHomeScenario()
    {
        CardInstance? region = null;
        var e = RevealEvent(TooCloseToHome, out var evt, s =>
        {
            region = Add(s, BlightedRegion, 0, Zone.Region, province: 1); s.Players[0].Provinces[1].RegionId = region.Id;
        });
        Eq(e.State.PendingChoice?.Type, "eventChoice", "its owner decides");
        Check(e.QueryActions(1).Any(a => a.Type == "eventChoice" && a.Command?.Choice == "none"), "destroying none is offered");
        Accept(e, new GameCommand("eventChoice", 1, e.Version, TargetId: region!.Id));
        Eq(C(e, region).Zone, Zone.DynastyDiscard, "the Region was destroyed");
        Eq(C(e, evt).Zone, Zone.EventInPlay, "the Event stays in play");
        var unit = Edit(e, st => Add(st, Samurai, 1, Zone.Home));
        var mover = unit.State.Cards.Values.Last(c => c.DefinitionId == Samurai && c.OwnerId == 1);
        Check(!unit.MoveToBattlefieldForTest(mover.Id, 0), "no movement to a battlefield while it is in play");
        // It ends as its owner's next turn begins.
        Eq(C(e, evt).ExpiresAtTurnsTaken, e.State.Players[1].TurnsTaken + 1, "until his next turn begins");
        // Declining: no Region destroyed.
        CardInstance? kept = null;
        var d = RevealEvent(TooCloseToHome, out _, s => { kept = Add(s, BlightedRegion, 0, Zone.Region, province: 1); s.Players[0].Provinces[1].RegionId = kept.Id; });
        Accept(d, new GameCommand("eventChoice", 1, d.Version, Choice: "none"));
        Eq(C(d, kept!).Zone, Zone.Region, "declined: the Region stays");
    }

    // ---- Hunger -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HungerObligation()
    {
        MatchEngine Build(bool mayNotAttack, out CardInstance wielder, out CardInstance other)
        {
            var s = Table(Phase.AttackDeclaration, 1); s.PriorityPlayerId = 1; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [];
            wielder = Add(s, "training-lion-samurai", 1, Zone.Home);
            Add(s, Hunger, 1, Zone.Attached, parent: wielder.Id);
            other = Add(s, Samurai, 1, Zone.Home);
            s.Players[1].MayNotAttackThisTurn = mayNotAttack;
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(false, out var wielder, out var other);
            Reject(e, new GameCommand("pass", 1, e.Version));
            Accept(e, new GameCommand("declareAttack", 1, e.Version));
            Reject(e, new GameCommand("attack", 1, e.Version, Assignments: new Dictionary<string, int> { [other.Id] = 0 }));
            Accept(e, new GameCommand("attack", 1, e.Version, Assignments: new Dictionary<string, int> { [wielder.Id] = 0 }));
            Eq(C(e, wielder).Zone, Zone.Battlefield, "Hunger's Personality was assigned to attack");
        }
        {
            var e = Build(true, out _, out _);
            Accept(e, new GameCommand("pass", 1, e.Version));
            Eq(e.State.Phase, Phase.Dynasty, "a player who may not attack declines as usual");
        }
    }

    private static void HungerReaction()
    {
        MatchEngine Build(out CardInstance wielder, out CardInstance hunger, out CardInstance breeze, out CardInstance monk)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            hunger = Add(s, Hunger, 0, Zone.Attached, province: 0, parent: wielder.Id);
            monk = Add(s, Samurai, 1, Zone.Battlefield, province: 0); GiveKeyword(monk, "Monk");
            breeze = Add(s, RideTheBreeze, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var wielder, out var hunger, out var breeze, out var monk);
        Accept(e, Offer(e, 1, EmperorP4Abilities.RideTheBreeze, breeze.Id, wielder.Id, monk.Id));
        Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == EmperorP4WatchAbilities.HungerNegateMove && a.Enabled), "the Reaction is offered");
        Accept(e, new GameCommand("useAbility", 0, e.Version, hunger.Id, AbilityId: EmperorP4WatchAbilities.HungerNegateMove));
        for (var i = 0; i < 6 && e.State.PendingChoice is not null && e.State.PendingChoice.Type != "effectChoice"; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(C(e, wielder).Zone, Zone.Battlefield, "his movement from the action was negated");
        // Declining: he moves.
        var d = Build(out var wielder2, out _, out var breeze2, out var monk2);
        Accept(d, Offer(d, 1, EmperorP4Abilities.RideTheBreeze, breeze2.Id, wielder2.Id, monk2.Id));
        for (var i = 0; i < 6 && d.State.PendingChoice is not null; i++) Accept(d, new GameCommand("declineReaction", d.State.PriorityPlayerId, d.Version));
        Eq(C(d, wielder2).Zone, Zone.Home, "moved home");
    }
}
