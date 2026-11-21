using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #835: the cards Mendon's rulings of 2026-10-11 (third batch) unblocked (src/L5R.Core/Wave835.cs), proved on the published cards themselves. Player 0 is the Crane, player 1 the Lion; every position is
// explicit, driven through QueryActions and the commands the engine offers, and a refused command must leave the state unchanged.
internal static class Wave835Tests
{
    private const string CleansingBreath = Wave835Abilities.CleansingBreathCard, ImperialDeployment = Wave835Abilities.ImperialDeploymentCard, WardedPremises = Wave835Abilities.WardedPremisesCard,
        Duty = Wave835Abilities.DutyCard, Pierce = Wave835Abilities.PierceCard, Heavens = Wave835Abilities.HeavensCard, Unbearable = Wave835Abilities.UnbearableCard, Patronage = Wave835Abilities.PatronageCard, Blood = Wave835Abilities.BloodCard, Revelations = Wave835Abilities.RevelationsCard, Foe = Wave835Abilities.FoeCard, Egg = Wave835Abilities.EggCard;
    private const string TwoElements = "oracle-l5r-10857" /* Moshi Ikako: Air, Fire */, OneElement = "oracle-l5r-7797" /* Tamori Shosei: Earth */;
    private const string Farm = Wave829Abilities.Farm, FaceOfNinube = "oracle-l5r-2431", TravelingPeddler = "oracle-l5r-8729";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Item = "training-item";
    // Fixtures: published Personalities carrying the keywords the rulings turn on.
    private const string Kensai = "oracle-l5r-10663" /* Lao-she */, Monk = "oracle-l5r-9998" /* Guest */, Yojimbo = "oracle-l5r-10310" /* Bayushi Wakui, 4 Gold */, Courtier = "oracle-l5r-10013" /* Rokuta's Apprentice */,
        Shugenja = "oracle-l5r-11328" /* Ashalan */;

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, int province = 0, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province);
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static MatchState Poor(Phase phase = Phase.Action, int actor = 0)
    {
        var s = Batch336Support.Table(phase, actor);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        return s;
    }
    /// <summary>Poor, except for the cheapest Gold producers that together make <paramref name="gold"/> (the discounted Yojimbo costs 4 - 3 = 1).</summary>
    private static MatchState PoorWith(int gold, Phase phase = Phase.Action, int actor = 0)
    {
        var s = Poor(phase, actor); var made = 0;
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).OrderBy(c => CardCatalog.Get(c.DefinitionId).GoldProduction))
        { if (made >= gold) break; card.Bowed = false; made += CardCatalog.Get(card.DefinitionId).GoldProduction; }
        return s;
    }
    private static void Pass(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is not null && guard < 16; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    public static void Run(TestRunner runner) => runner.Run("wave-835", [
        ("Published as printed: Pierce the Tapestry is spelled correctly with its seven-copy limit; every card has its abilities, text and manifest", PublishedShape),
        ("A Cleansing Breath: 'He centers himself' does nothing; other players' preventions are negated for the turn; a Kensai straightens his unit", CleansingBreathCard),
        ("Imperial Deployment: only assigning is freed (he may still be barred from moving); his unit straightens when an attack is declared, unless a straightening is prevented", ImperialDeploymentCard),
        ("Too Close to Home bars movement between battlefields and to and from home, not assignment; the Perfect Moment restricts only its own player's Personalities", TooCloseAndPerfectMoment),
        ("Warded Premises: the Terrain negates only the Force bonuses actions gave, at its battlefield; it can be put into play with no units there, and not over another Terrain", WardedPremisesTerrain),
        ("Duty Over All Things: recruits a discarded (not dead) Yojimbo at the battlefield when an enemy Personality stands there, three Gold cheaper with a Courtier or Shugenja", DutyOverAllThings),
        ("Pierce the Tapestry: -3F; the third Pierce this battle destroys a target with no attachments; a Pierce that did not destroy grants an additional Pierce action", PierceTheTapestry),
        ("The Heavens' Blessing: no player can gain Honor in Action Phases this turn", HeavensHonor),
        ("The Heavens' Blessing: every card an own Strategy draws in an Action Phase is its own 2 Gold payment, declinable, never shared; a Holding's draw is free", HeavensDraws),
        ("Unbearable Weight: discards the top card of a chosen Fate deck, a Chi penalty of its Focus Value; a destroyed target's controller puts the Strategy in his hand, and it goes back to its owner's discard pile", UnbearableWeight),
        ("Blood of Isawa's Tribe: after it attaches, the Shugenja's element keywords, then the five elements, a confirmation for the same element; no pass, no Reaction window; the replacement lasts while it is attached", BloodOfIsawasTribe),
        ("Revelations: shows the top five Dynasty cards to both players, the other player may discard one, its owner arranges the rest, and the Province refills face up", RevelationsEvent),
        ("The Empire's Foe Exposed: a duel in the Events Phase between Unique Personalities, nothing unless each player has one, no question for a player with exactly one, destroying the loser", EmpiresFoeExposed),
        ("The Egg of P'an Ku - exp: bows your Personality, leaves the game, creates a copy of the second target in your home, bows it and holds it back until after your next turn begins", EggOfPanKu),
        ("Merchant Patronage: on an Honor change an action of another player's Holding makes, the Honor goes to the player you choose, one larger with a bowed Courtier", MerchantPatronage),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, CardType Type, string[] Abilities)[] cards = [
            (CleansingBreath, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.CleansingBreath]),
            (ImperialDeployment, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.ImperialDeployment]),
            (WardedPremises, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.WardedNegate, Wave835Abilities.WardedPlay]),
            (Duty, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.DutyDiscount, Wave835Abilities.DutyFull]),
            (Pierce, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.Pierce]),
            (Heavens, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.HeavensDraw, Wave835Abilities.HeavensHonor]),
            (Unbearable, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.Unbearable]),
            (Patronage, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.PatronageRedirect, Wave835Abilities.PatronageBow]),
            (Blood, "spell-wave-835", CardType.Spell, []),
            (Revelations, "event-wave-835", CardType.Event, []),
            (Foe, "event-wave-835", CardType.Event, []),
            (Egg, "item-wave-835", CardType.Item, [Wave835Abilities.Egg])];
        foreach (var (id, wave, type, abilities) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(string.Join(",", def.Abilities ?? []), string.Join(",", abilities), id + " abilities");
            Check(!def.CreatedOnly && !def.ScenarioOnly, id + " is a published, deck-legal card");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        // Mendon: Oracle of the Void misspells it "Tapestery"; it is spelled correctly here, with up to seven copies in a deck.
        Eq(CardCatalog.Get(Pierce).Name, "Pierce the Tapestry", "the local name is spelled correctly");
        Eq(CardCatalog.Get(Pierce).DeckCopyLimit, 7, "up to seven copies of this card in a deck");
        Check(!CardCatalog.Cards.Values.Any(d => d.Name.Contains("Tapestery")), "no published card keeps the misspelling");
        Check(CardCatalog.Get(CleansingBreath).Text.Contains("He centers himself."), "the printed text keeps the flavor sentence");
        var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-835-sources.json"))).RootElement.EnumerateArray().Single(r => r.GetProperty("card_id").GetString() == Pierce);
        Eq(source.GetProperty("oracle_name").GetString(), "Pierce the Tapestery", "the source row records Oracle's spelling");
    }

    // ---- A Cleansing Breath ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CleansingBreathCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var kensai = Add(s, Kensai, 0, Zone.Home, bowed: true);
        var follower = Add(s, "training-follower", 0, Zone.Attached, true, null, kensai.Id);
        var plain = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var enemy = Add(s, LionSamurai, 1, Zone.Home);
        var card = Add(s, CleansingBreath, 0, Zone.Hand);
        var e = Engine(s);
        // Any of his own Personalities at any location, never another player's.
        var targets = Batch336Support.Options(e, 0, Wave835Abilities.CleansingBreath, card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().OrderBy(x => x).ToArray();
        Eq(string.Join(",", targets), string.Join(",", new[] { kensai.Id, plain.Id }.OrderBy(x => x)), "your Personalities, at home or anywhere, and nobody else's");
        // A lock another player's card made: it binds until the Breath, then the Personality is free of it.
        var lockedState = e.State;
        lockedState.Cards[kensai.Id].MovementLocks.Add(new MovementLock("test", lockedState.TurnNumber, MovementKinds.Home | MovementKinds.ToBattlefield | MovementKinds.Assign, ByPlayer: 1));
        var e2 = Engine(lockedState);
        Check(e2.MovementLockedForTest(kensai.Id, MovementKinds.Assign) && e2.MovementLockedForTest(kensai.Id, MovementKinds.ToBattlefield), "the other player's lock binds before the Breath");
        Accept(e2, Batch336Support.Offer(e2, 0, Wave835Abilities.CleansingBreath, card.Id, kensai.Id));
        Check(!e2.MovementLockedForTest(kensai.Id, MovementKinds.Assign) && !e2.MovementLockedForTest(kensai.Id, MovementKinds.ToBattlefield) && !e2.MovementLockedForTest(kensai.Id, MovementKinds.Home), "assigning and moving are no longer prevented");
        Check(e2.IsImmuneForTest(kensai.Id, ImmunityKind.Prevention, 1), "other players' cards' preventions are negated");
        Check(!e2.State.Cards[kensai.Id].Bowed && !e2.State.Cards[follower.Id].Bowed, "a Kensai straightens his whole unit");
        // A "may not attack" another player's action stored is also an effect that prevents him assigning.
        var restricted = Batch336Support.Edit(e, st => st.Restrictions.Items.Add(new("restriction-cb", RestrictionKind.NoAttack, plain.Id, UntilTurn: st.TurnNumber, ByPlayer: 1)));
        Check((bool)Call(restricted, "RestrictionBarsAssignment", Call(restricted, "Card", plain.Id), false)!, "the other player's restriction bars him before the Breath");
        var breath = restricted.State.Cards.Values.First(c => c.DefinitionId == CleansingBreath);
        Accept(restricted, Batch336Support.Offer(restricted, 0, Wave835Abilities.CleansingBreath, breath.Id, plain.Id));
        Check(!(bool)Call(restricted, "RestrictionBarsAssignment", Call(restricted, "Card", plain.Id), false)!, "and no longer bars him after it");
        // Someone who is not a Kensai stays as he was.
        var e3 = Engine(e.State);
        var card3 = e3.State.Cards.Values.First(c => c.DefinitionId == CleansingBreath);
        Accept(e3, Batch336Support.Offer(e3, 0, Wave835Abilities.CleansingBreath, card3.Id, plain.Id));
        Check(e3.State.Cards[plain.Id].Bowed, "a Personality who is not a Kensai is not straightened");
        Check(e3.IsImmuneForTest(plain.Id, ImmunityKind.Prevention, 1) && !e3.IsImmuneForTest(plain.Id, ImmunityKind.Prevention, 0), "the immunity is against other players only");
    }

    // ---- Imperial Deployment --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImperialDeploymentCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var deployed = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var attached = Add(s, Item, 0, Zone.Attached, true, null, deployed.Id);
        var card = Add(s, ImperialDeployment, 0, Zone.Hand);
        deployed.MovementLocks.Add(new MovementLock("test", s.TurnNumber, MovementKinds.Assign | MovementKinds.AssignAttack | MovementKinds.ToBattlefield | MovementKinds.Home, ByPlayer: 1));
        var e = Engine(s);
        Check(e.MovementLockedForTest(deployed.Id, MovementKinds.Assign), "the lock stops him assigning before the card");
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.ImperialDeployment, card.Id, performerId: deployed.Id));
        Check(!e.MovementLockedForTest(deployed.Id, MovementKinds.Assign) && !e.MovementLockedForTest(deployed.Id, MovementKinds.AssignAttack), "only the effects that stop him assigning are negated");
        Check(e.MovementLockedForTest(deployed.Id, MovementKinds.ToBattlefield) && e.MovementLockedForTest(deployed.Id, MovementKinds.Home), "he may still be barred from moving: assigning is not moving");
        Check(e.IsImmuneForTest(deployed.Id, ImmunityKind.AssignPrevention, 1) && !e.IsImmuneForTest(deployed.Id, ImmunityKind.Prevention, 1), "an assigning-only immunity");
        // A "may not attack" that another player's action stored is an effect preventing assigning too; a rule's own restriction is not.
        var withRestriction = e.State;
        withRestriction.Restrictions.Items.Add(new("restriction-test", RestrictionKind.NoAttack, deployed.Id, UntilTurn: withRestriction.TurnNumber, ByPlayer: 1));
        var e2 = Engine(withRestriction);
        Check(!(bool)Call(e2, "RestrictionBarsAssignment", e2.State.Cards[deployed.Id], false)!, "another player's stored 'may not attack' no longer bars the assignment");
        var ownRule = e.State;
        ownRule.Restrictions.Items.Clear();
        ownRule.Restrictions.Items.Add(new("restriction-own", RestrictionKind.NoAttack, deployed.Id, UntilTurn: ownRule.TurnNumber));
        Check((bool)Call(Engine(ownRule), "RestrictionBarsAssignment", ownRule.Cards[deployed.Id], false)!, "a restriction with no other player's source still bars it");
        // The attack is declared: his unit straightens, once.
        var declaring = Engine(e.State);
        declaring.State.Phase = Phase.AttackDeclaration;
        var st = declaring.State; st.Phase = Phase.AttackDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0;
        var attack = Engine(st);
        Accept(attack, new GameCommand("declareAttack", 0, attack.Version));
        Check(!attack.State.Cards[deployed.Id].Bowed && !attack.State.Cards[attached.Id].Bowed, "after the attack is declared his unit straightens");
        Check(attack.State.Wave835 is null || attack.State.Wave835.Deployments.Count == 0, "the watch is spent");
        // Only a prevention of straightening stops it: 'prevent the next straightening' and 'will not straighten' do; nothing else does.
        foreach (var (name, hold) in new (string, Action<CardInstance, MatchState>)[] {
            ("prevent the next straightening", (c, state) => c.StraightenBlockedTurn = state.TurnNumber),
            ("will not straighten", (c, state) => c.StraightenLockedTurn = state.TurnNumber) })
        {
            var again = Engine(e.State);
            var held = again.State;
            held.Phase = Phase.AttackDeclaration; held.ActivePlayerId = 0; held.PriorityPlayerId = 0;
            hold(held.Cards[deployed.Id], held);
            var heldEngine = Engine(held);
            Accept(heldEngine, new GameCommand("declareAttack", 0, heldEngine.Version));
            Check(heldEngine.State.Cards[deployed.Id].Bowed, $"{name}: the mandatory straightening is barred");
        }
        // No attack declared, no straightening (it waits for this turn's attack only).
        Check(e.State.Cards[deployed.Id].Bowed, "he is not straightened by playing the card");
    }

    private static void TooCloseAndPerfectMoment()
    {
        var tooClose = CardCatalog.Cards.Values.Single(d => d.Effect == "too-close-to-home");
        var s = Batch336Support.Table(Phase.Action, 0);
        var unit = Add(s, CraneSamurai, 0, Zone.Home);
        Add(s, tooClose.Id, 1, Zone.EventInPlay);
        var e = Engine(s);
        Check(e.MovementLockedForTest(unit.Id, MovementKinds.ToBattlefield) && e.MovementLockedForTest(unit.Id, MovementKinds.Home), "Too Close to Home bars movement to a battlefield (from home or between battlefields) and home from a battlefield");
        Check(!e.MovementLockedForTest(unit.Id, MovementKinds.Assign) && !e.MovementLockedForTest(unit.Id, MovementKinds.AssignAttack), "it does not bar assignment to battlefields");
        // The Perfect Moment restricts only its own player's Personalities: another player's Personality (the one Imperial Deployment protects) is not affected by it.
        var pm = Batch336Support.Table(Phase.Action, 0);
        var mine = Add(pm, CraneSamurai, 0, Zone.Home); var theirs = Add(pm, LionSamurai, 1, Zone.Home);
        var pe = Engine(pm);
        var state = pe.State;
        state.R1 ??= new(); state.R1.PerfectMoment = new PerfectMomentState { PlayerId = 1 };
        var locked = Engine(state);
        Check(locked.MovementLockedForTest(theirs.Id, MovementKinds.Assign) && !locked.MovementLockedForTest(mine.Id, MovementKinds.Assign), "the Perfect Moment's player's Personalities may not assign; the other player's are not restricted by it");
    }

    // ---- Warded Premises ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WardedPremisesTerrain()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var mine = Bf(s, CraneSamurai, 0); var theirs = Bf(s, LionSamurai, 1); var elsewhere = Add(s, LionSamurai, 1, Zone.Battlefield, false, 2);
        var weapon = Add(s, Item, 0, Zone.Attached, false, 0, mine.Id);
        // Bonuses of every kind on cards at the battlefield.
        mine.Modifiers.Add(new("src", "defensive-stance", "Force", 3));                       // an action's temporary bonus: negated
        mine.Modifiers.Add(new("src", "defensive-stance", "Force", 2, Permanent: true));     // a permanent bonus: kept
        mine.Modifiers.Add(new("src", "test-trait", "Force", 1));                              // a trait's standing bonus: kept
        mine.Modifiers.Add(new("src", "defensive-stance", "Force", -2));                       // a penalty from an action: kept
        theirs.Modifiers.Add(new("src", "defensive-stance", "Force", 4));
        weapon.Modifiers.Add(new("src", "defensive-stance", "Force", 1));                      // an attachment given a bonus by an action: it is a card at the battlefield too
        elsewhere.Modifiers.Add(new("src", "defensive-stance", "Force", 5));                  // another battlefield: untouched
        mine.Tokens.Add(new("token-1", "Force", "src", "Force", 1));
        var terrain = Add(s, WardedPremises, 0, Zone.Battlefield, false, 0);
        var e = Engine(s);
        Check(((string[])Call(e, "BeforeBattleResolveSourceIds", 0)!).Contains(terrain.Id), "the Terrain joins the set of before-resolution effects at its battlefield");
        Call(e, "Wave835WardedNegate", e.State.Cards[terrain.Id]);
        var bonuses = (string id) => string.Join(",", e.State.Cards[id].Modifiers.Select(m => $"{m.Effect}:{m.Amount}{(m.Permanent ? "p" : "")}"));
        Eq(bonuses(mine.Id), "defensive-stance:2p,test-trait:1,defensive-stance:-2", "only the action's temporary bonus is gone: permanent, trait and penalty stay");
        Eq(bonuses(theirs.Id), "", "every card at the battlefield, either side's");
        Eq(bonuses(weapon.Id), "", "and the attachments there");
        Eq(bonuses(elsewhere.Id), "defensive-stance:5", "not another battlefield");
        Eq(e.State.Cards[mine.Id].Tokens.Count, 1, "tokens are untouched");
        // Its Battle action: with no units of yours there, if there is no Terrain.
        var play = Batch336Support.Table(Phase.Battle, 0);
        Bf(play, LionSamurai, 1);
        var card = Add(play, WardedPremises, 0, Zone.Hand);
        var pe = Engine(play);
        Accept(pe, Batch336Support.Use(pe, 0, Wave835Abilities.WardedPlay, card.Id));
        Eq(pe.State.Cards[card.Id].Zone, Zone.Battlefield, "put into play at the battlefield even though you control no units there");
        var blocked = Batch336Support.Table(Phase.Battle, 0);
        Bf(blocked, LionSamurai, 1);
        var other = Add(blocked, WardedPremises, 1, Zone.Battlefield, false, 0);
        var card2 = Add(blocked, WardedPremises, 0, Zone.Hand);
        var be = Engine(blocked);
        Check(!Batch336Support.CanUse(be, 0, Wave835Abilities.WardedPlay, card2.Id), "not when there is already a Terrain at the battlefield");
        Check(other.Zone == Zone.Battlefield, "the other Terrain stays");
    }

    // ---- Duty Over All Things ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DutyOverAllThings()
    {
        // With a Courtier in play: three Gold cheaper -- the 4 Gold Yojimbo costs 1, which is all the Gold the player has.
        var s = PoorWith(1, Phase.Battle, 0);
        Bf(s, CraneSamurai, 0); Add(s, Courtier, 0, Zone.Home);
        Bf(s, LionSamurai, 1);
        var duty = Add(s, Duty, 0, Zone.Hand);
        var discarded = Add(s, Yojimbo, 0, Zone.DynastyDiscard);
        var dead = Add(s, Yojimbo, 0, Zone.Dead);
        var others = Add(s, CraneSamurai, 0, Zone.DynastyDiscard);
        var theirs = Add(s, Yojimbo, 1, Zone.DynastyDiscard);
        var e = Engine(s);
        var discount = Batch336Support.Options(e, 0, Wave835Abilities.DutyDiscount, duty.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Eq(string.Join(",", discount), discarded.Id, "only your own discarded Yojimbo, never a dead one, a non-Yojimbo or the other player's");
        Check(!Batch336Support.CanUse(e, 0, Wave835Abilities.DutyFull, duty.Id), "with a Courtier the plain-price version is not offered");
        Accept(e, Batch336Support.Offer(e, 0, Wave835Abilities.DutyDiscount, duty.Id, discarded.Id));
        Pass(e);
        Eq(e.State.Cards[discarded.Id].Zone, Zone.Battlefield, "he enters play at the battlefield where it was played");
        Eq(e.State.Cards[discarded.Id].ProvinceIndex, 0, "at province 0");
        Eq(e.State.Cards[duty.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // A Shugenja counts as well as a Courtier.
        var sh = PoorWith(1, Phase.Battle, 0);
        Bf(sh, CraneSamurai, 0); Add(sh, Shugenja, 0, Zone.Home); Bf(sh, LionSamurai, 1);
        var d2 = Add(sh, Duty, 0, Zone.Hand); var y2 = Add(sh, Yojimbo, 0, Zone.DynastyDiscard);
        var e2 = Engine(sh);
        Check(Batch336Support.CanUse(e2, 0, Wave835Abilities.DutyDiscount, d2.Id), "a Shugenja gives the discount too");
        // Without either: the plain recruit, which this player cannot afford with no Gold.
        var plain = Poor(Phase.Battle, 0);
        Bf(plain, CraneSamurai, 0); Bf(plain, LionSamurai, 1);
        var d3 = Add(plain, Duty, 0, Zone.Hand); var y3 = Add(plain, Yojimbo, 0, Zone.DynastyDiscard);
        var e3 = Engine(plain);
        Check(!Batch336Support.CanUse(e3, 0, Wave835Abilities.DutyDiscount, d3.Id), "the discounted version needs a Courtier or Shugenja");
        Check(Batch336Support.Options(e3, 0, Wave835Abilities.DutyFull, d3.Id).Any(), "the plain version is listed");
        Check(!Batch336Support.CanUse(e3, 0, Wave835Abilities.DutyFull, d3.Id), "but not payable without Gold (it costs the full 4)");
        // And affordable with the Gold: the Strategy is played, paying the whole cost.
        var rich = Batch336Support.Table(Phase.Battle, 0);
        Bf(rich, CraneSamurai, 0); Bf(rich, LionSamurai, 1);
        var d4 = Add(rich, Duty, 0, Zone.Hand); var y4 = Add(rich, Yojimbo, 0, Zone.DynastyDiscard);
        var e4 = Engine(rich);
        Check(Batch336Support.CanUse(e4, 0, Wave835Abilities.DutyFull, d4.Id), "with Gold, the plain version is playable");
        // "If he would be opposed": no enemy Personality at the battlefield, no Duty.
        var open = Batch336Support.Table(Phase.Battle, 0);
        Bf(open, CraneSamurai, 0); Add(open, Courtier, 0, Zone.Home);
        var d5 = Add(open, Duty, 0, Zone.Hand); Add(open, Yojimbo, 0, Zone.DynastyDiscard);
        var e5 = Engine(open);
        Check(!Batch336Support.CanUse(e5, 0, Wave835Abilities.DutyDiscount, d5.Id) && !Batch336Support.CanUse(e5, 0, Wave835Abilities.DutyFull, d5.Id), "with no enemy Personality at the battle it may not be played");
    }

    // ---- Pierce the Tapestry --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PierceTheTapestry()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var monk = Bf(s, Monk, 0);
        var target = Bf(s, LionSamurai, 1);
        var guarded = Bf(s, LionSamurai, 1); Add(s, Item, 1, Zone.Attached, false, 0, guarded.Id);
        var first = Add(s, Pierce, 0, Zone.Hand); var second = Add(s, Pierce, 0, Zone.Hand); var third = Add(s, Pierce, 0, Zone.Hand); var fourth = Add(s, Pierce, 0, Zone.Hand);
        var e = Engine(s);
        var force = (string id) => (int)Call(e, "PersonalityForce", e.State.Cards[id])!;
        var start = force(target.Id);
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Pierce, first.Id, target.Id, monk.Id));
        Pass(e);
        Eq(force(target.Id), start - 3, "-3F");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true), "it did not destroy: an additional Battle action to play a Pierce the Tapestry");
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Pierce, second.Id, target.Id, monk.Id));
        Pass(e);
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "two plays this battle: not destroyed yet");
        // The third play has two other Pierce behind it, and the target has no attachments: destroyed. No additional action then.
        var grantsBefore = e.State.AdditionalActions.Count(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true);
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Pierce, third.Id, target.Id, monk.Id));
        Pass(e);
        Check(e.State.Cards[target.Id].Zone != Zone.Battlefield, "the third Pierce this battle destroys a target with no attachments");
        Check(e.State.AdditionalActions.Count(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true) <= Math.Max(0, grantsBefore - 1), "and it grants no further additional action");
        // A target with an attachment is only weakened however many Pierce came before, and the additional action is granted again.
        var armed = Batch336Support.Table(Phase.Battle, 0);
        var armedMonk = Bf(armed, Monk, 0); var armedTarget = Bf(armed, LionSamurai, 1); Add(armed, Item, 1, Zone.Attached, false, 0, armedTarget.Id);
        var plays = Enumerable.Range(0, 4).Select(_ => Add(armed, Pierce, 0, Zone.Hand)).ToArray();
        var ae = Engine(armed);
        foreach (var play in plays)
        {
            Accept(ae, Batch336Support.Use(ae, 0, Wave835Abilities.Pierce, play.Id, armedTarget.Id, armedMonk.Id));
            Pass(ae);
            Check(ae.State.Cards[armedTarget.Id].Zone == Zone.Battlefield, "a target with an attachment survives");
            Check(ae.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true), "so another Pierce may be played as an additional action");
        }
        // A Pierce needs its performing unbowed Monk, and an enemy Personality or Follower.
        var bare = Batch336Support.Table(Phase.Battle, 0);
        Bf(bare, CraneSamurai, 0); Bf(bare, LionSamurai, 1);
        var lone = Add(bare, Pierce, 0, Zone.Hand);
        Check(!Batch336Support.CanUse(Engine(bare), 0, Wave835Abilities.Pierce, lone.Id), "no Monk of yours at the battle, no Pierce");
    }

    // ---- The Heavens' Blessing ------------------------------------------------------------------------------------------------------------------------------------------------------

    // A published-profile match (player abilities exist only there), hands emptied, every Gold source of player 0 bowed: a test puts back the sources it wants.
    private static MatchState PublishedTable(Phase phase, int actor)
    {
        var s = MatchEngine.CreatePublishedMatch().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor; s.PendingChoice = null;
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        return s;
    }
    private const string Shrine = Wave829Abilities.Shrine; // a Holding that produces 2 Gold
    private static GameCommand Pay(MatchEngine e, int player) => Batch336Support.Enabled(e, player, Wave835Abilities.HeavensPay).Command!;
    private static GameCommand Decline(MatchEngine e, int player) => new("declineReaction", player, e.Version);

    private static void HeavensHonor()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var heavens = Add(s, Heavens, 0, Zone.Hand);
        var farm = Add(s, Farm, 0, Zone.Home); var theirs = Add(s, Farm, 1, Zone.Home);
        var e = Engine(s);
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.HeavensHonor, heavens.Id));
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        var honor = Batch336Support.Honor(e, 0);
        Accept(e, Batch336Support.Enabled(e, 0, Wave829Abilities.FarmHonor, farm.Id).Command!);
        Eq(Batch336Support.Honor(e, 0), honor, "no Honor is gained in an Action Phase this turn");
        // Either player: the other player's gain is stopped too (his Holding, used on his turn).
        var other = Batch336Support.Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        var theirHonor = Batch336Support.Honor(other, 1);
        Accept(other, Batch336Support.Enabled(other, 1, Wave829Abilities.FarmHonor, theirs.Id).Command!);
        Eq(Batch336Support.Honor(other, 1), theirHonor, "for either player");
        // Only this turn.
        var later = Batch336Support.Edit(e, st => { st.TurnNumber++; foreach (var c in st.Cards.Values) c.Bowed = false; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        var laterHonor = Batch336Support.Honor(later, 0);
        Accept(later, Batch336Support.Enabled(later, 0, Wave829Abilities.FarmHonor, farm.Id).Command!);
        Eq(Batch336Support.Honor(later, 0), laterHonor + 1, "the next turn Honor can be gained again");
    }

    private static MatchEngine WithBlessing(Action<MatchState> arrange, bool poor = false)
    {
        var s = PublishedTable(Phase.Action, 0);
        if (!poor) for (var i = 0; i < 3; i++) Add(s, Shrine, 0, Zone.Home);
        arrange(s);
        s.Wave835 = new Wave835State { HeavensActive = true };
        return Engine(s);
    }

    private static void HeavensDraws()
    {
        // Control: without the Blessing a Strategy draws freely.
        var free = Batch336Support.Table(Phase.Action, 0);
        var ninubeFree = Add(free, FaceOfNinube, 0, Zone.Hand); for (var i = 0; i < 3; i++) Add(free, CraneSamurai, 0, Zone.Hand);
        var fe = Engine(free);
        Accept(fe, Batch336Support.Use(fe, 0, "face-ninube-draw", ninubeFree.Id));
        Eq(fe.State.Players[0].Hand.Count, 3, "without the Blessing the shuffled cards are drawn back");

        // The Blessing played: Open, sets the game-long flag.
        var played = Batch336Support.Table(Phase.Action, 0);
        var heavens = Add(played, Heavens, 0, Zone.Hand);
        var pe = Engine(played);
        Accept(pe, Batch336Support.Use(pe, 0, Wave835Abilities.HeavensDraw, heavens.Id));
        Check(pe.State.Wave835?.HeavensActive == true, "until the game ends");

        // Three cards owed: each its own payment, declinable one by one. Pay for one, decline one, pay for the last.
        var e = WithBlessing(st => { Add(st, FaceOfNinube, 0, Zone.Hand); for (var i = 0; i < 3; i++) Add(st, CraneSamurai, 0, Zone.Hand); });
        var ninube = e.State.Cards.Values.First(c => c.DefinitionId == FaceOfNinube);
        Accept(e, Batch336Support.Use(e, 0, "face-ninube-draw", ninube.Id));
        Eq(e.State.PendingChoice?.Type, Wave835Abilities.HeavensWindow, "a window asks about the owed cards, after the Strategy's effects");
        Eq(e.State.Players[0].Hand.Count, 0, "no card was drawn yet: the hand was shuffled into the deck");
        Eq(e.State.Wave835!.HeavensOwed[0], 3, "three cards drawn from the Strategy are owed, each its own payment");
        Func<MatchEngine, int> gold = x => x.State.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Count(c => !c.Bowed);
        var unbowedBefore = gold(e);
        Accept(e, Pay(e, 0));
        Eq(e.State.Players[0].Hand.Count, 1, "one payment, one card");
        Check(gold(e) < unbowedBefore, "the 2 Gold was paid from a Gold source");
        Eq(e.State.PendingChoice?.Type, Wave835Abilities.HeavensWindow, "two cards are still owed");
        Accept(e, Decline(e, 0));
        Eq(e.State.Players[0].Hand.Count, 1, "a declined card is not drawn");
        Eq(e.State.Wave835!.HeavensOwed.GetValueOrDefault(0), 1, "one left");
        Accept(e, Pay(e, 0));
        Eq(e.State.Players[0].Hand.Count, 2, "the third was paid for");
        Check(e.State.PendingChoice is null, "the window is closed");
        Eq(e.State.PriorityPlayerId, 1, "priority goes back to where the Strategy left it: with the other player");

        // "Decline all" ends the window at once.
        var all = WithBlessing(st => { Add(st, FaceOfNinube, 0, Zone.Hand); for (var i = 0; i < 3; i++) Add(st, CraneSamurai, 0, Zone.Hand); });
        Accept(all, Batch336Support.Use(all, 0, "face-ninube-draw", all.State.Cards.Values.First(c => c.DefinitionId == FaceOfNinube).Id));
        Accept(all, new GameCommand(Wave835Abilities.HeavensDeclineAll, 0, all.Version));
        Check(all.State.PendingChoice is null && all.State.Players[0].Hand.Count == 0, "declining all draws nothing");

        // No Gold at all: nothing to ask, the cards are not drawn.
        var broke = WithBlessing(st => { Add(st, FaceOfNinube, 0, Zone.Hand); Add(st, CraneSamurai, 0, Zone.Hand); }, poor: true);
        Accept(broke, Batch336Support.Use(broke, 0, "face-ninube-draw", broke.State.Cards.Values.First(c => c.DefinitionId == FaceOfNinube).Id));
        Check(broke.State.PendingChoice is null && broke.State.Players[0].Hand.Count == 0, "a player who cannot pay 2 Gold does not draw, and is not asked");

        // One Gold source producing more than the 2 Gold: it pays one card, and its surplus is not carried to the next ("a 4 Gold producing holding cannot be split to pay both costs").
        var single = WithBlessing(st =>
        {
            var stronghold = st.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Stronghold);
            Check(CardCatalog.Get(stronghold.DefinitionId).GoldProduction >= 4, "the fixture Stronghold produces at least 4 Gold");
            stronghold.Bowed = false;
            Add(st, FaceOfNinube, 0, Zone.Hand); for (var i = 0; i < 2; i++) Add(st, CraneSamurai, 0, Zone.Hand);
        }, poor: true);
        Accept(single, Batch336Support.Use(single, 0, "face-ninube-draw", single.State.Cards.Values.First(c => c.DefinitionId == FaceOfNinube).Id));
        Eq(single.State.Wave835!.HeavensOwed[0], 2, "two cards owed");
        Accept(single, Pay(single, 0));
        Eq(single.State.Players[0].Hand.Count, 1, "the Stronghold's Gold paid for one card");
        Check(single.State.PendingChoice is null && single.State.Players[0].Hand.Count == 1, "the second cannot be paid from the same source: it is not drawn and the window closes");

        // A holding's draw is not "an effect of his own Strategies".
        if (CardCatalog.Cards.ContainsKey(TravelingPeddler) && (CardCatalog.Get(TravelingPeddler).Abilities ?? []).Length > 0)
        {
            var holding = WithBlessing(st => { Add(st, TravelingPeddler, 0, Zone.Home); });
            var peddler = holding.State.Cards.Values.First(c => c.DefinitionId == TravelingPeddler);
            var drawAbility = (CardCatalog.Get(TravelingPeddler).Abilities ?? []).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Limited");
            if (drawAbility is not null && Batch336Support.Options(holding, 0, drawAbility, peddler.Id).Any(o => o.Enabled))
            {
                var handSize = holding.State.Players[0].Hand.Count;
                Accept(holding, Batch336Support.Enabled(holding, 0, drawAbility, peddler.Id).Command!);
                Eq(holding.State.Players[0].Hand.Count, handSize + 1, "a Holding's draw is free");
                Check(holding.State.PendingChoice is null, "and nothing is asked");
            }
        }
    }

    // ---- Unbearable Weight ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // The offered command with its suggested payment, given the selections the player makes.
    private static GameCommand Paid(MatchEngine e, int player, string cardId, params SlotSelection[] selections) =>
        Batch336Support.Enabled(e, player, Wave835Abilities.Unbearable, cardId).Command! with { Selections = selections };

    private static void UnbearableWeight()
    {
        var s = PublishedTable(Phase.Action, 0);
        for (var i = 0; i < 2; i++) Add(s, Shrine, 0, Zone.Home); // 4 Gold
        var weight = Add(s, Unbearable, 0, Zone.Hand);
        var victim = Add(s, LionSamurai, 1, Zone.Home);           // Chi 3
        var top = Add(s, CleansingBreath, 1, Zone.FateDeck);       // Focus 4
        s.Players[1].FateDeck.Insert(0, top.Id);
        var e = Engine(s);
        // Not enough Gold: the printed 4 Gold is paid.
        var poor = Batch336Support.Edit(e, st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true; });
        Check(!Batch336Support.CanUse(poor, 0, Wave835Abilities.Unbearable, weight.Id), "it costs 4 Gold");
        var focus = CardCatalog.Get(CleansingBreath).Focus;
        Accept(e, Paid(e, 0, weight.Id, Batch336Support.PickPlayer(Wave835Abilities.DeckSlot, 1), Batch336Support.Pick(Wave835Abilities.WeightTargetSlot, victim.Id)));
        Eq(e.State.Cards[top.Id].Zone, Zone.FateDiscard, "the top card of the chosen Fate deck (the other player's) was discarded");
        Check(focus >= CardCatalog.Get(LionSamurai).Chi, "its Focus Value covers his Chi");
        Check(e.State.Cards[victim.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "a Chi penalty equal to its Focus Value destroyed him");
        var back = e.State.Cards[weight.Id];
        Eq(back.Zone, Zone.Hand, "his controller puts the Strategy in his hand");
        Eq(back.OwnerId, 1, "it is his to play"); Eq(back.OriginalOwnerId, 0, "and it remembers its owner");
        Check(e.State.Players[1].Hand.Contains(weight.Id) && !e.State.Players[0].Hand.Contains(weight.Id), "it is in his hand, no longer in the giver's");
        // He plays it as though he owned it, and when it is discarded it goes back to its owner's pile.
        var his = Batch336Support.Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; for (var i = 0; i < 2; i++) Add(st, Shrine, 1, Zone.Home); });
        var his2 = Engine(his.State);
        Accept(his2, Paid(his2, 1, weight.Id, Batch336Support.PickPlayer(Wave835Abilities.DeckSlot, 0)));
        Eq(his2.State.Cards[weight.Id].Zone, Zone.FateDiscard, "played and discarded");
        Eq(his2.State.Cards[weight.Id].OwnerId, 0, "back to its owner's discard pile");
        Check(his2.State.Cards[weight.Id].OriginalOwnerId is null, "no longer borrowed");

        // A target that survives: the Strategy stays in its owner's discard pile.
        var t = PublishedTable(Phase.Action, 0);
        for (var i = 0; i < 2; i++) Add(t, Shrine, 0, Zone.Home);
        var w2 = Add(t, Unbearable, 0, Zone.Hand); var big = Add(t, LionSamurai, 1, Zone.Home); big.Modifiers.Add(new("fixture", "fixture", "Chi", 5));
        var low = Add(t, CleansingBreath, 1, Zone.FateDeck); t.Players[1].FateDeck.Insert(0, low.Id);
        var te = Engine(t);
        Accept(te, Paid(te, 0, w2.Id, Batch336Support.PickPlayer(Wave835Abilities.DeckSlot, 1), Batch336Support.Pick(Wave835Abilities.WeightTargetSlot, big.Id)));
        Check(te.State.Cards[big.Id].Zone == Zone.Home, "a target with Chi to spare survives");
        Eq(te.State.Cards[w2.Id].Zone, Zone.FateDiscard, "and the Strategy is discarded as any Strategy");
        Eq(te.State.Cards[w2.Id].OwnerId, 0, "to its owner's pile");

        // The target is optional, and an own Fate deck may be chosen.
        var u = PublishedTable(Phase.Action, 0);
        for (var i = 0; i < 2; i++) Add(u, Shrine, 0, Zone.Home);
        var w3 = Add(u, Unbearable, 0, Zone.Hand); var own = Add(u, CraneSamurai, 0, Zone.FateDeck); u.Players[0].FateDeck.Insert(0, own.Id);
        var ue = Engine(u);
        Accept(ue, Paid(ue, 0, w3.Id, Batch336Support.PickPlayer(Wave835Abilities.DeckSlot, 0)));
        Eq(ue.State.Cards[own.Id].Zone, Zone.FateDiscard, "without a target the card is still discarded");
    }

    // ---- Merchant Patronage ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine PatronageTable(bool theirHolding = true, bool withCourtier = true)
    {
        var s = Batch336Support.Table(Phase.Action, 1);
        Add(s, Farm, theirHolding ? 1 : 0, Zone.Home);
        Add(s, Patronage, 0, Zone.Hand);
        if (withCourtier) Add(s, Courtier, 0, Zone.Home);
        s.PriorityPlayerId = 1;
        return Engine(s);
    }

    private static void ClosePatronageWindow(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "honorChangeReaction" } && guard < 8; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void MerchantPatronage()
    {
        // The other player's Holding gains him Honor: the window opens for the Honor change, and Patronage redirects the gain to its player.
        var e = PatronageTable();
        var farm = e.State.Cards.Values.First(c => c.DefinitionId == Farm);
        var patronage = e.State.Cards.Values.First(c => c.DefinitionId == Patronage);
        var courtier = e.State.Cards.Values.First(c => c.DefinitionId == Courtier);
        var honor0 = Batch336Support.Honor(e, 0); var honor1 = Batch336Support.Honor(e, 1);
        Accept(e, Batch336Support.Enabled(e, 1, Wave829Abilities.FarmHonor, farm.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the Honor change opens the Reaction window");
        var options = e.QueryActions(0).Where(a => a.Enabled && a.Command?.AbilityId is Wave835Abilities.PatronageRedirect or Wave835Abilities.PatronageBow).ToArray();
        Check(options.Any(o => o.Command!.AbilityId == Wave835Abilities.PatronageRedirect) && options.Any(o => o.Command!.AbilityId == Wave835Abilities.PatronageBow), "both the plain play and the Courtier-bowing play are offered");
        Accept(e, new GameCommand("useAbility", 0, e.Version, patronage.Id, AbilityId: Wave835Abilities.PatronageRedirect, TargetPlayerId: 0));
        ClosePatronageWindow(e);
        Eq(Batch336Support.Honor(e, 1), honor1, "the Holding's controller gains nothing");
        Eq(Batch336Support.Honor(e, 0), honor0 + 1, "the player who played Merchant Patronage gains it");
        Check(!e.State.Cards[courtier.Id].Bowed, "no Courtier was bowed");

        // Bowing a Courtier makes the gain larger by 1, in addition to the redirect.
        var b = PatronageTable();
        var farm2 = b.State.Cards.Values.First(c => c.DefinitionId == Farm); var p2 = b.State.Cards.Values.First(c => c.DefinitionId == Patronage); var c2 = b.State.Cards.Values.First(c => c.DefinitionId == Courtier);
        var h0 = Batch336Support.Honor(b, 0); var h1 = Batch336Support.Honor(b, 1);
        Accept(b, Batch336Support.Enabled(b, 1, Wave829Abilities.FarmHonor, farm2.Id).Command!);
        Accept(b, new GameCommand("useAbility", 0, b.Version, p2.Id, AbilityId: Wave835Abilities.PatronageBow, PerformerId: c2.Id, TargetPlayerId: 0));
        ClosePatronageWindow(b);
        Eq(Batch336Support.Honor(b, 0), h0 + 2, "one more Honor with a bowed Courtier");
        Eq(Batch336Support.Honor(b, 1), h1, "still redirected");
        Check(b.State.Cards[c2.Id].Bowed, "the Courtier is bowed");

        // The player may leave the gain with its player and still take the bonus (the chosen player is the original one).
        var k = PatronageTable();
        var fk = k.State.Cards.Values.First(c => c.DefinitionId == Farm); var pk = k.State.Cards.Values.First(c => c.DefinitionId == Patronage); var ck = k.State.Cards.Values.First(c => c.DefinitionId == Courtier);
        var k1 = Batch336Support.Honor(k, 1);
        Accept(k, Batch336Support.Enabled(k, 1, Wave829Abilities.FarmHonor, fk.Id).Command!);
        Accept(k, new GameCommand("useAbility", 0, k.Version, pk.Id, AbilityId: Wave835Abilities.PatronageBow, PerformerId: ck.Id, TargetPlayerId: 1));
        ClosePatronageWindow(k);
        Eq(Batch336Support.Honor(k, 1), k1 + 2, "the Holding's controller keeps it, one larger");

        // Your own Holding: the Reaction is not offered on it.
        var own = PatronageTable(theirHolding: false);
        var ownFarm = own.State.Cards.Values.First(c => c.DefinitionId == Farm);
        own.State.ActivePlayerId = 0;
        var ownState = own.State; ownState.ActivePlayerId = 0; ownState.PriorityPlayerId = 0;
        var own2 = Engine(ownState);
        Accept(own2, Batch336Support.Enabled(own2, 0, Wave829Abilities.FarmHonor, ownFarm.Id).Command!);
        Check(own2.State.PendingChoice?.Type != "honorChangeReaction", "an action of the Patronage player's own Holding opens no window for Merchant Patronage");

        // Without a Courtier only the plain redirect is offered.
        var none = PatronageTable(withCourtier: false);
        var nf = none.State.Cards.Values.First(c => c.DefinitionId == Farm);
        Accept(none, Batch336Support.Enabled(none, 1, Wave829Abilities.FarmHonor, nf.Id).Command!);
        var offered = none.QueryActions(0).Where(a => a.Enabled && a.Command?.AbilityId is Wave835Abilities.PatronageRedirect or Wave835Abilities.PatronageBow).Select(a => a.Command!.AbilityId).ToArray();
        Check(offered.Contains(Wave835Abilities.PatronageRedirect) && !offered.Contains(Wave835Abilities.PatronageBow), "without a Courtier only the plain redirect is offered");
    }


    // ---- Blood of Isawa's Tribe -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static GameCommand Answer(MatchEngine e, string type, string? choice = null) => new(type, e.State.PendingChoice!.PlayerId, e.Version, Choice: choice);
    private static string[] Offered(MatchEngine e) => e.QueryActions(e.State.PendingChoice!.PlayerId).Where(a => a.Enabled && a.Type != "concede").Select(a => a.Type + ":" + (a.Command?.Choice ?? "")).ToArray();
    private static bool HasKeyword(MatchEngine e, string cardId, string keyword) => (bool)Call(e, "Keyword", Call(e, "Card", cardId), keyword)!;

    private static MatchEngine BloodTable(string shugenja, out CardInstance host, out CardInstance spell)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        host = Add(s, shugenja, 0, Zone.Home);
        spell = Add(s, Blood, 0, Zone.Hand);
        s.PriorityPlayerId = 0;
        return Engine(s);
    }

    private static void BloodOfIsawasTribe()
    {
        var e = BloodTable(TwoElements, out var host, out var spell);
        Check(HasKeyword(e, host.Id, "Air") && HasKeyword(e, host.Id, "Fire") && !HasKeyword(e, host.Id, "Water"), "the Shugenja is Air and Fire");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: spell.Id, TargetId: host.Id, SourceIds: []));
        Eq(e.State.PendingChoice?.Type, Wave835Abilities.BloodWindow, "attaching opens the decision");
        Eq(string.Join(",", Offered(e)), $"{Wave835Abilities.BloodFrom}:Air,{Wave835Abilities.BloodFrom}:Fire", "first the element keywords the Shugenja has, and only those");
        Check(!e.QueryActions(0).Any(a => a.Type is "declineReaction" or "pass"), "there is no 'pass reaction': it is a mandatory trait");
        Reject(e, new GameCommand("pass", 0, e.Version));
        Reject(e, Answer(e, Wave835Abilities.BloodFrom, "Water"));
        Accept(e, Answer(e, Wave835Abilities.BloodFrom, "Fire"));
        Eq(string.Join(",", Offered(e).Where(o => o.StartsWith(Wave835Abilities.BloodTo, StringComparison.Ordinal))), string.Join(",", new[] { "Air", "Earth", "Fire", "Water", "Void" }.Select(x => $"{Wave835Abilities.BloodTo}:{x}")), "then the five elements, the same one included");
        // The same element: asks whether they are sure; cancelling goes back to the five elements.
        Accept(e, Answer(e, Wave835Abilities.BloodTo, "Fire"));
        Eq(string.Join(",", Offered(e)), $"{Wave835Abilities.BloodConfirm}:,{Wave835Abilities.BloodCancel}:", "the same keyword asks for a confirmation");
        Accept(e, Answer(e, Wave835Abilities.BloodCancel));
        Check(Offered(e).Any(o => o == $"{Wave835Abilities.BloodTo}:Water"), "cancelling returns to the five elements");
        Accept(e, Answer(e, Wave835Abilities.BloodTo, "Water"));
        Check(e.State.PendingChoice is null, "the decision is over");
        Check(HasKeyword(e, host.Id, "Water") && !HasKeyword(e, host.Id, "Fire") && HasKeyword(e, host.Id, "Air"), "while the Spell is attached, Fire is Water on him; Air is untouched");
        // The replacement ends with the attachment.
        Call(e, "DestroyAttachment", Call(e, "Card", spell.Id));
        Check(HasKeyword(e, host.Id, "Fire") && !HasKeyword(e, host.Id, "Water"), "when the Spell leaves, his keywords are his own again");

        // Confirming the same keyword changes nothing.
        var same = BloodTable(TwoElements, out var h2, out var s2);
        Accept(same, new GameCommand("equip", 0, same.Version, CardId: s2.Id, TargetId: h2.Id, SourceIds: []));
        Accept(same, Answer(same, Wave835Abilities.BloodFrom, "Air"));
        Accept(same, Answer(same, Wave835Abilities.BloodTo, "Air"));
        Accept(same, Answer(same, Wave835Abilities.BloodConfirm));
        Check(same.State.PendingChoice is null && HasKeyword(same, h2.Id, "Air") && HasKeyword(same, h2.Id, "Fire") && !HasKeyword(same, h2.Id, "Water"), "replacing a keyword with itself changes nothing");

        // A Shugenja with one element keyword is still asked (one option); one with none is not.
        var one = BloodTable(OneElement, out var h3, out var s3);
        Accept(one, new GameCommand("equip", 0, one.Version, CardId: s3.Id, TargetId: h3.Id, SourceIds: []));
        Eq(string.Join(",", Offered(one)), $"{Wave835Abilities.BloodFrom}:Earth", "one element keyword, one option");
        var none = BloodTable("oracle-l5r-11328", out var h4, out var s4);
        Accept(none, new GameCommand("equip", 0, none.Version, CardId: s4.Id, TargetId: h4.Id, SourceIds: []));
        Check(none.State.PendingChoice is null, "a Shugenja with no element keyword has nothing to replace");
    }

    // ---- Revelations --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Event) RevelationsTable()
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, Revelations, 0, Zone.Province);
        s.Players[0].Provinces[1].CardId = ev.Id; ev.ProvinceIndex = 1; ev.FaceUp = true;
        return (Engine(s), ev);
    }

    private static void RevelationsEvent()
    {
        var (e, ev) = RevelationsTable();
        var topFive = e.State.Players[0].DynastyDeck.Take(5).ToArray();
        Eq(topFive.Length, 5, "the fixture deck has cards");
        Call(e, "EventsPhaseForTest", 0);
        Eq(e.State.PendingChoice?.Type, "shownChoice", "the five cards are shown and the other player chooses");
        Eq(e.State.PendingChoice?.PlayerId, 1, "the other player decides");
        Eq(e.State.ShownCards.Count(c => c.SourceCardId == ev.Id), 5, "all five are in the public shown-cards record");
        var offered = e.QueryActions(1).Where(a => a.Type == "chooseShown").ToArray();
        Check(offered.Length >= 5, "one choice per shown card, and a way to choose none");
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "its owner waits");
        // The other player discards the third card shown.
        var discardedId = topFive[2];
        var third = e.State.ShownCards.Where(c => c.SourceCardId == ev.Id).OrderBy(c => c.Sequence).ElementAt(2).Sequence.ToString();
        Accept(e, offered.First(a => a.Command!.Choice == third).Command!);
        Eq(e.State.Cards[discardedId].Zone, Zone.DynastyDiscard, "the chosen card is discarded");
        Eq(e.State.PendingChoice?.Type, "lookArrange", "its owner arranges the rest");
        Eq(e.State.PendingChoice?.PlayerId, 0, "the owner arranges");
        var arrangements = e.QueryActions(0).Where(a => a.Type == "arrangeTopCards").ToArray();
        Check(arrangements.Length >= 1, "the arrangements are on offer");
        var reversed = arrangements.Last();
        Accept(e, reversed.Command!);
        var deck = e.State.Players[0].DynastyDeck;
        Check(!deck.Contains(discardedId), "the discarded card left the deck");
        var refilledWith = e.State.Players[0].Provinces[1].CardId!;
        Check(deck.Take(3).Append(refilledWith).OrderBy(x => x).SequenceEqual(topFive.Where(x => x != discardedId).OrderBy(x => x)), "the other four went back on top: the first refilled the Province, the other three are the top of the deck");
        Check(e.State.Cards[ev.Id].Zone == Zone.DynastyDiscard, "the Event is discarded");
        var province = e.State.Players[0].Provinces[1];
        Check(province.CardId is not null && e.State.Cards[province.CardId].FaceUp, "its Province refilled face up");
        Check(e.State.Phase == Phase.Action && e.State.PendingChoice is null, "the Events Phase went on: " + e.State.Phase);

        // Choosing none: all five go back for the owner to arrange.
        var (n, nev) = RevelationsTable();
        var five = n.State.Players[0].DynastyDeck.Take(5).ToArray();
        Call(n, "EventsPhaseForTest", 0);
        Accept(n, n.QueryActions(1).First(a => a.Type == "chooseShown" && a.Command!.Choice == "done").Command!);
        Eq(n.State.PendingChoice?.Type, "lookArrange", "nothing discarded, the owner arranges all five");
        Accept(n, n.QueryActions(0).First(a => a.Type == "arrangeTopCards").Command!);
        Check(n.State.Players[0].DynastyDeck.Take(4).Append(n.State.Players[0].Provinces[1].CardId!).OrderBy(x => x).SequenceEqual(five.OrderBy(x => x)), "all five went back on top (one of them refilled the Province)");
        Check(n.State.Phase == Phase.Action && n.State.Players[0].Provinces[1].CardId is { } refilled && n.State.Cards[refilled].FaceUp, "the Province refilled face up");

        // A Dynasty deck with nothing in it: nothing to show, the Event is discarded and the Province stays empty.
        var empty = Batch336Support.Table(Phase.Straighten, 0);
        var eev = Add(empty, Revelations, 0, Zone.Province);
        empty.Players[0].Provinces[1].CardId = eev.Id; eev.ProvinceIndex = 1; eev.FaceUp = true;
        empty.Players[0].DynastyDeck.Clear();
        var ee = Engine(empty);
        Call(ee, "EventsPhaseForTest", 0);
        Check(ee.State.PendingChoice is null && ee.State.Phase == Phase.Action && ee.State.Cards[eev.Id].Zone == Zone.DynastyDiscard, "an empty deck: the Event is discarded");
    }

    // ---- The Empire's Foe Exposed -------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Unique(MatchState s, string def, int owner, int chiBonus = 0)
    {
        var card = Add(s, def, owner, Zone.Home);
        card.GrantedKeywords.Add(new("Unique", "fixture", GrantDuration.Permanent));
        if (chiBonus != 0) card.Modifiers.Add(new("fixture", "fixture", "Chi", chiBonus));
        return card;
    }

    private static (MatchEngine E, CardInstance Event, CardInstance[] Mine, CardInstance[] Theirs) FoeTable(int mineUnique, int theirsUnique, int ownChi = 20)
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, Foe, 0, Zone.Province);
        s.Players[0].Provinces[1].CardId = ev.Id; ev.ProvinceIndex = 1; ev.FaceUp = false;
        var mine = Enumerable.Range(0, mineUnique).Select(i => Unique(s, CraneSamurai, 0, i == 0 ? ownChi : 0)).ToList();
        mine.Add(Add(s, CraneSamurai, 0, Zone.Home));                    // not Unique: never offered
        var theirs = Enumerable.Range(0, theirsUnique).Select(_ => Unique(s, LionSamurai, 1)).ToList();
        theirs.Add(Add(s, LionSamurai, 1, Zone.Home));
        return (Engine(s), ev, [.. mine], [.. theirs]);
    }

    private static void FinishFoeDuel(MatchEngine e)
    {
        for (var guard = 0; guard < 20 && (e.State.PendingDuel is not null || e.State.PendingChoice is not null); guard++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var option = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type == "focus" && a.Command.Choice == "deck" || a.Type is "strike" or "declineReaction" or "declineDuelReaction" or "duelistRefocus"));
            if (option is null) break;
            Accept(e, option.Command!);
        }
    }

    private static void EmpiresFoeExposed()
    {
        // Exactly one Unique Personality each: no question, the duel begins at once, in the Events Phase.
        var (e, ev, mine, theirs) = FoeTable(1, 1);
        Call(e, "EventsPhaseForTest", 0);
        Check(e.State.PendingDuel is { } duel && duel.ChallengerId == mine[0].Id && duel.ChallengedId == theirs[0].Id, "the owner's Unique Personality challenges the other player's");
        Check(e.State.Phase == Phase.Events, "the duel is fought during the Events Phase");
        FinishFoeDuel(e);
        Check(e.State.PendingDuel is null, "the duel finished");
        Check(e.State.Cards[theirs[0].Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the duel's loser is destroyed");
        Eq(e.State.Cards[mine[0].Id].Zone, Zone.Home, "the winner stays");
        Check(e.State.Cards[ev.Id].Zone == Zone.DynastyDiscard, "the Event is discarded");
        Check(e.State.Phase == Phase.Action && e.State.PendingChoice is null, "the Events Phase went on to the Action Phase: " + e.State.Phase);

        // Several Unique Personalities: the owner chooses his, then the other player chooses theirs; only Unique ones are listed.
        var (m, mev, mm, mt) = FoeTable(2, 2);
        Call(m, "EventsPhaseForTest", 0);
        Eq(m.State.PendingChoice?.Type, Wave835Abilities.FoeWindow, "the owner is asked first");
        Eq(m.State.PendingChoice?.PlayerId, 0, "the Event's owner");
        var ownerOptions = m.QueryActions(0).Where(a => a.Type == Wave835Abilities.FoePick).ToArray();
        Eq(string.Join(",", ownerOptions.Select(a => a.Command!.TargetId).OrderBy(x => x)), string.Join(",", mm.Take(2).Select(c => c.Id).OrderBy(x => x)), "only his Unique Personalities in play");
        Check(ownerOptions.All(a => a.DecisionCandidateIds is [var only] && only == a.Command!.TargetId), "each is a candidate card the client can highlight, by click or list");
        NoChange(m, new(Wave835Abilities.FoePick, 0, m.Version, TargetId: mm[2].Id), "a Personality that is not Unique is not a choice");
        NoChange(m, new(Wave835Abilities.FoePick, 0, m.Version, TargetId: mt[0].Id), "nor the other player's");
        NoChange(m, new(Wave835Abilities.FoePick, 1, m.Version, TargetId: mt[0].Id), "the other player waits");
        Check(m.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the other player has nothing to do yet");
        Accept(m, ownerOptions.First(a => a.Command!.TargetId == mm[1].Id).Command!);
        Eq(m.State.PendingChoice?.PlayerId, 1, "then the other player chooses theirs");
        var theirOptions = m.QueryActions(1).Where(a => a.Type == Wave835Abilities.FoePick).ToArray();
        Eq(string.Join(",", theirOptions.Select(a => a.Command!.TargetId).OrderBy(x => x)), string.Join(",", mt.Take(2).Select(c => c.Id).OrderBy(x => x)), "only their Unique Personalities");
        Accept(m, theirOptions.First(a => a.Command!.TargetId == mt[0].Id).Command!);
        Check(m.State.PendingDuel is { } d2 && d2.ChallengerId == mm[1].Id && d2.ChallengedId == mt[0].Id, "the chosen two duel");

        // One side has one Unique Personality, the other two: only the one with a choice is asked.
        var (o, oev, om, ot) = FoeTable(1, 2);
        Call(o, "EventsPhaseForTest", 0);
        Eq(o.State.PendingChoice?.PlayerId, 1, "the owner has exactly one: only the other player is asked");
        Accept(o, o.QueryActions(1).First(a => a.Type == Wave835Abilities.FoePick && a.Command!.TargetId == ot[1].Id).Command!);
        Check(o.State.PendingDuel is { } d3 && d3.ChallengerId == om[0].Id && d3.ChallengedId == ot[1].Id, "the duel begins");

        // Nobody Unique on one side: the Event is discarded without effect.
        foreach (var (a, b) in new[] { (0, 2), (2, 0), (0, 0) })
        {
            var (n, nev, _, _) = FoeTable(a, b);
            Call(n, "EventsPhaseForTest", 0);
            Check(n.State.PendingDuel is null && n.State.PendingChoice is null && n.State.Cards[nev.Id].Zone == Zone.DynastyDiscard && n.State.Phase == Phase.Action, $"{a} and {b} Unique Personalities: discarded without effect");
        }
    }

    // ---- The Egg of P'an Ku ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EggOfPanKu()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var egg = Add(s, Egg, 0, Zone.Attached, false, null, host.Id);
        var other = Add(s, CraneSamurai, 0, Zone.Home);
        var bowedAlready = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var enemy = Add(s, LionSamurai, 1, Zone.Home);
        enemy.GrantedKeywords.Add(new("Unique", "fixture", GrantDuration.Permanent));
        var e = Engine(s);
        var slots = Batch336Support.Options(e, 0, Wave835Abilities.Egg, egg.Id).First(o => o.Enabled).Slots!;
        Eq(string.Join(",", slots.Single(x => x.Slot == Wave835Abilities.EggBowSlot).CardIds.OrderBy(x => x)), string.Join(",", new[] { host.Id, other.Id }.OrderBy(x => x)), "the first target: one of your unbowed Personalities");
        Check(slots.Single(x => x.Slot == Wave835Abilities.EggCopySlot).CardIds.Contains(enemy.Id) && slots.Single(x => x.Slot == Wave835Abilities.EggCopySlot).CardIds.Contains(host.Id), "the second target is any Personality, the other player's included");
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Egg, egg.Id, selections: [Batch336Support.Pick(Wave835Abilities.EggBowSlot, other.Id), Batch336Support.Pick(Wave835Abilities.EggCopySlot, enemy.Id)]));
        Pass(e);
        Check(e.State.Cards[other.Id].Bowed, "the first target is bowed");
        Eq(e.State.Cards[egg.Id].Zone, Zone.RemovedFromGame, "the Item is removed from the game");
        var copy = e.State.Cards.Values.Single(c => c.DefinitionId == LionSamurai && c.OwnerId == 0 && c.Zone == Zone.Home);
        Eq(copy.OwnerId, 0, "the Egg's controller controls the copy");
        Eq(copy.Zone, Zone.Home, "in his home");
        Check(!copy.Bowed, "the copy is not bowed");
        Check(e.State.Cards[enemy.Id].Bowed, "the second target, the other player's Personality, is bowed");
        Check(e.State.Cards[enemy.Id].StraightenHolds.Any(h => h.Owner == 0 && h.Until == StraightenPoint.StraightenPhaseEnds), "and will not straighten until after the Egg's controller's next turn begins");
        Check((bool)Call(e, "RestrictionBarsAssignment", Call(e, "Card", enemy.Id), false)! && (bool)Call(e, "RestrictionBarsAssignment", Call(e, "Card", enemy.Id), true)!, "nor attack nor defend");
        Check(!(bool)Call(e, "RestrictionBarsAssignment", Call(e, "Card", copy.Id), false)!, "the copy itself may attack");
        Check(e.State.MechD.TimedRemovals.Any(r => r.CardId == copy.Id && r.PlayerId == 0), "the copy leaves the game after the Egg's controller's next turn begins");
        // The restrictions end as the Egg's controller's next turn begins.
        var next = Batch336Support.Edit(e, st => { st.TurnNumber++; st.Players[0].TurnsTaken++; });
        Check(!(bool)Call(next, "RestrictionBarsAssignment", Call(next, "Card", enemy.Id), false)! && !(bool)Call(next, "RestrictionBarsAssignment", Call(next, "Card", enemy.Id), true)!, "after his next turn begins the Personality may attack and defend again");
        // The copy is fresh: a Unique target is copied all the same ("ignoring costs and restrictions"), and the Egg can target a Personality of its controller's own.
        var own = Batch336Support.Table(Phase.Action, 0);
        var h2 = Add(own, CraneSamurai, 0, Zone.Home); var egg2 = Add(own, Egg, 0, Zone.Attached, false, null, h2.Id); var mine2 = Add(own, CraneSamurai, 0, Zone.Home);
        var oe = Engine(own);
        Accept(oe, Batch336Support.Use(oe, 0, Wave835Abilities.Egg, egg2.Id, selections: [Batch336Support.Pick(Wave835Abilities.EggBowSlot, h2.Id), Batch336Support.Pick(Wave835Abilities.EggCopySlot, mine2.Id)]));
        Pass(oe);
        Eq(oe.State.Cards.Values.Count(c => c.DefinitionId == CraneSamurai && c.OwnerId == 0 && c.Zone == Zone.Home), 3, "a copy of his own Personality too");
        // A refused command (the same card for the bow target that is already bowed) changes nothing.
        var refuse = Engine(s);
        NoChange(refuse, Batch336Support.Use(refuse, 0, Wave835Abilities.Egg, egg.Id, selections: [Batch336Support.Pick(Wave835Abilities.EggBowSlot, bowedAlready.Id), Batch336Support.Pick(Wave835Abilities.EggCopySlot, enemy.Id)]), "the first target must be unbowed");
        NoChange(refuse, Batch336Support.Use(refuse, 0, Wave835Abilities.Egg, egg.Id, selections: [Batch336Support.Pick(Wave835Abilities.EggBowSlot, enemy.Id), Batch336Support.Pick(Wave835Abilities.EggCopySlot, enemy.Id)]), "the first target is one of yours");
    }
}
