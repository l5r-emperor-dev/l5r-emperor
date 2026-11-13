using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The Second City unwired-card pass (issue #264), part three: Great Sacrifice, Rage of the Spirits, On the Hunt, Tireless Efforts, Costly Opportunity, Silent Terror, Essence of Undeath
// and Goju Plot. Every position is built explicitly and played through QueryActions and the commands it offers. Readings and assumptions: docs/decisions/2026-09-21-second-city-pass.md.
internal static partial class SecondCityBattleTests
{
    private const string GreatSacrifice = "oracle-l5r-2900", Rage = "oracle-l5r-6141", OnTheHunt = "oracle-l5r-5704", Tireless = "oracle-l5r-8496", Costly = "oracle-l5r-1522",
        SilentTerror = "oracle-l5r-7261", Essence = "oracle-l5r-2380", GojuPlot = "oracle-l5r-2856", Conspiracy = "oracle-l5r-2011", InsultingGesture = "oracle-l5r-3779",
        BarredPassage = "oracle-l5r-708", Dainagon = "oracle-l5r-2070";

    public static void Run(TestRunner runner) => runner.Run("second-city-battle", [
        ("Published shape: the eight cards are the printed ones with the abilities named for them, ship their art, and their waves pin exactly them", PublishedShapes),
        ("Great Sacrifice: the performer is destroyed as the cost and an enemy unit whose total Force (attachments included) is within the performer's Force plus Personal Honor -- plus one when he is defending -- is destroyed", GreatSacrificeReach),
        ("Great Sacrifice's legality: a bowed or absent performer, an own unit, a unit beyond the reach, and a target the performer's destruction changes nothing about", GreatSacrificeLegality),
        ("Rage of the Spirits: its Shugenja and the performing Spirit both bow -- the Shugenja need not be at the battle -- the enemy card without attachments (Force at most the Spirit's) is destroyed, and an Earth Shugenja gains 1 Honor", RageOfTheSpirits),
        ("Rage of the Spirits' costs: a bowed Shugenja or a bowed Spirit cannot pay, a target above the Spirit's Force or with an attachment is refused", RageCosts),
        ("On the Hunt's Battle action attaches an attachment from the hand to a performing Samurai for 4 less Gold (never below 0), through the ordinary legality of an attachment, and pays that Gold", OnTheHuntEquips),
        ("On the Hunt's Open action straightens the unit of a performing Samurai who has an attachment -- the Samurai and his attachments -- and needs the attachment", OnTheHuntStraightens),
        ("Tireless Efforts' Battle action straightens a performing Personality, bowed or not, at the battle", TirelessStraightens),
        ("Tireless Efforts' other action removes the Force penalties a Personality has, and for the turn applies none -- a Force bonus stays, a Chi penalty still applies, and it ends with the turn", TirelessNegates),
        ("Costly Opportunity: the performing Samurai is destroyed as the cost, every OTHER card in the target's unit may not use its abilities, and the target -- a lone enemy card without attachments -- is destroyed", CostlyOpportunity),
        ("Silent Terror bows the target and, only when a card sits in the current battlefield's province, offers the choice to discard it (the province refills); when none does nothing is asked", SilentTerror_),
        ("Essence of Undeath: the Spell is destroyed as the cost, the dead Personality is paid for in full and enters at its Shugenja's location -- home or the battlefield -- permanently Shadowlands and Undead, and its controller loses 2 Honor", EssenceOfUndeath),
        ("Goju Plot: the performing Shadowlands Ninja is destroyed as the cost, a Ninja Personality of his controller's discard pile (dead or merely discarded) enters at the current battlefield for its recruit cost less the performer's Gold Cost, 2 Honor is lost and another Battle action may follow", GojuPlotRecruits),
        ("Discovering a Conspiracy: a Magistrate removes a Personality's Clan alignment for good and dishonors him; at a battlefield he is also bowed and a chosen player gains or loses 1 Honor -- asked only there", DiscoveringAConspiracy),
        ("Discovering a Conspiracy's Focus Effect: after the duel, its controller gains 2 Honor if his Personality won it -- and none if he lost", ConspiracyFocusEffect),
        ("Insulting Gesture: a Courtier's Political Open action takes the Imperial Favor; its Favor Political Battle action discards the Favor to dishonor, bow and send home an enemy Personality, whose controller loses 2 Honor -- and only a holder of the Favor may use it", InsultingGesture_),
        ("Doji Dainagon's Lobby is an Open action: he bows to take the Imperial Favor, while his controller's Family Honor is the higher and he has 1 Personal Honor, and it does not use up the Limited Lobby", DainagonLobbies),
        ("Barred Passage: a Hero who is the only Personality in his army bows an enemy Personality's whole unit; another Personality beside him, or a performer who is not a Hero, refuses it", BarredPassage_),
        ("Every enabled command offered for the cards of this pass validates and is accepted on a fresh engine", OfferedCommandsValidateFresh),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static CardView View(MatchEngine e, string id, int viewer = 0) => e.GetView(viewer).Players
        .SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.Hand).Append(p.Stronghold)).Single(c => c.Id == id);

    // Like Batch336Support.Offer, but the payment sent is the quote for THE chosen target (a target-priced action ships one quote per legal target).
    private static GameCommand OfferPaid(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        var option = Options(e, player, ability, cardId, performerId).FirstOrDefault(a => a.Enabled && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", Options(e, player, ability, cardId, performerId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        var command = option!.Command! with { TargetId = targetId };
        return option.TargetGold is { } quotes && quotes.TryGetValue(targetId, out var quote) ? command with { SourceIds = quote.Payments.FirstOrDefault()?.SourceIds ?? [] } : command;
    }

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        void Shipped(string id) { Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped"); Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg"); Check(!CardCatalog.Get(id).ScenarioOnly, id); }
        (string Id, string Name, CardType Type, int Focus, int Cost, string[] Abilities, string Text)[] cards =
        [
            (GreatSacrifice, "Great Sacrifice", CardType.Strategy, 1, 0, ["great-sacrifice-destroy"], "Battle: Destroy your performing unbowed Personality and target an enemy unit with total Force equal to or lower than your performer's Force, plus Personal Honor, plus one if he is defending: Destroy the enemy unit."),
            (Rage, "Rage of the Spirits", CardType.Spell, 2, 0, ["rage-of-the-spirits-destroy"], "Battle: Even if this unit is not at the current battlefield, bow this Shugenja and your performing Spirit, and target an enemy card without attachments and with Force lower than or equal to your Spirit's: Destroy the enemy card. If this Shugenja is Earth, gain 1 Honor."),
            (OnTheHunt, "On the Hunt", CardType.Strategy, 1, 0, ["on-the-hunt-equip", "on-the-hunt-straighten"], "Battle: Choose your performing Samurai: Attach an attachment in your hand to him, paying all costs but paying 4 less Gold. / Open: Choose your performing Samurai with an attachment: Straighten his unit."),
            (Tireless, "Tireless Efforts", CardType.Strategy, 1, 0, ["tireless-efforts-straighten", "tireless-efforts-negate"], "Battle: Choose your performing Personality: Straighten him. / Battle/Open: Choose your performing Personality: Negate all current and new Force penalties on him."),
            (Costly, "Costly Opportunity", CardType.Strategy, 1, 0, ["costly-opportunity-destroy"], "Battle: Destroy your target performing unbowed Samurai to target an enemy card without attachments. Abilities on other cards now in its unit may not be used. Destroy the targeted card."),
            (SilentTerror, "Silent Terror", CardType.Strategy, 1, 0, ["silent-terror-bow"], "Battle: Choose your performing unbowed Ninja Personality and target an enemy card: Bow it. You may discard a card in the current battlefield's province."),
            (Essence, "Essence of Undeath", CardType.Spell, 3, 2, ["essence-of-undeath-recruit"], "Battle/Open: Destroy this card and target your dead Personality: Bring him into play at this Shugenja's location, paying all costs. Permanently give him Shadowlands and Undead. Lose 2 Honor."),
            (GojuPlot, "Goju Plot", CardType.Strategy, 2, 0, ["goju-plot-recruit"], "Battle: Destroy your performing Shadowlands Ninja Personality and target a Ninja Personality in your discard pile: Bring him into play at the current battlefield, paying all costs but reducing his Gold cost to enter play by the performer's Gold Cost. Lose 2 Honor. You may take an additional Battle action."),
        ];
        foreach (var (id, name, type, focus, cost, abilities, text) in cards)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == type, $"{id} is {name}"); Eq(c.Focus, focus, name + " Focus"); Eq(c.GoldCost, cost, name + " Gold Cost");
            Eq(c.Text.Replace("\n", " / "), text, name + " text"); Check(c.Abilities!.SequenceEqual(abilities) && c.Effect == "none", $"{name}'s abilities");
            Shipped(id);
        }
        Check(CardCatalog.Get(SilentTerror).Keywords!.Contains("Ninja") && CardCatalog.Get(GojuPlot).Keywords!.Contains("Ninja"), "Silent Terror and Goju Plot are Ninja");
        Check(CardCatalog.Get(Rage).Keywords!.Contains("Earth") && CardCatalog.Get(Essence).Keywords!.Contains("Maho"), "Rage is Earth, Essence is Maho");
        Eq(PublishedContent.StrategyWave35.Cards.Length, 13); Eq(PublishedContent.SpellWave13.Cards.Length, 2);
        var dainagon = CardCatalog.Get(Dainagon);
        Check(dainagon.Name == "Doji Dainagon" && dainagon.Type == CardType.Personality && dainagon.GoldCost == 5 && dainagon.Force == 1 && dainagon.Chi == 3 && dainagon.PersonalHonor == 3 && dainagon.HonorRequirement == 6, "Doji Dainagon");
        Check(dainagon.Keywords!.Contains("Courtier") && dainagon.Keywords.Contains("Crane Clan") && dainagon.Keywords.Contains("Imperial") && dainagon.Text == "Dainagon may Lobby as an Open action." && dainagon.Abilities!.SequenceEqual(new[] { "dainagon-lobby" }), "his keywords and text");
        Shipped(Dainagon);
        var conspiracy = CardCatalog.Get(Conspiracy);
        Check(conspiracy.Focus == 4 && conspiracy.Abilities!.SequenceEqual(new[] { "discovering-conspiracy-focus", "discovering-conspiracy-expose" }) && conspiracy.Keywords!.Contains("Political"), "Discovering a Conspiracy: a Focus Effect and its action");
        Eq(AbilityCatalog.All["discovering-conspiracy-focus"].Timing, "FocusEffect");
        var gesture = CardCatalog.Get(InsultingGesture);
        Check(gesture.Focus == 3 && gesture.Keywords!.Contains("Favor") && gesture.Keywords.Contains("Political") && gesture.Abilities!.SequenceEqual(new[] { "insulting-gesture-take", "insulting-gesture-shame" }), "Insulting Gesture");
        Check(CardCatalog.Get(BarredPassage).Focus == 2 && CardCatalog.Get(BarredPassage).Keywords!.Contains("Heroic"), "Barred Passage");
        Shipped(Conspiracy); Shipped(InsultingGesture); Shipped(BarredPassage);
        Check(AbilityCatalog.All["rage-of-the-spirits-destroy"].Costs!.Select(c => (c.Verb, c.Subject)).SequenceEqual(new[] { (CostVerb.Bow, CostSubject.Wielder), (CostVerb.Bow, CostSubject.Performer) }), "both bows are declared costs");
        Check(AbilityCatalog.All["goju-plot-recruit"].Cost!.MinusPerformerCost && AbilityCatalog.All["essence-of-undeath-recruit"].Cost!.Basis == GoldCostBasis.TargetRecruit, "the recruit prices");
        Check(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-35-sources.json")).Contains("image_sha256"), "receipts are recorded");
    }

    // ---- Great Sacrifice --------------------------------------------------------------------------------------------------------------------------
    private static void GreatSacrificeReach()
    {
        // Player 0 attacks: performer Force 3 + Personal Honor 2 = reach 5. A Force-5 enemy unit is destroyed, the performer with it.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);       // Force 3, Personal Honor 2
        var strategy = Add(s, GreatSacrifice, 0, Zone.Hand);
        var five = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);             // Force 5
        var six = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);               // Force 6: beyond the reach
        var armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);             // Force 4 + an Item of Force 2 = 6
        Add(s, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "great-sacrifice-destroy", strategy.Id, performer.Id);
        var legal = option.TargetIds!.ToHashSet();
        Check(legal.Contains(five.Id) && !legal.Contains(six.Id) && !legal.Contains(armed.Id), "only units within the reach: " + string.Join(",", legal));
        Accept(e, Offer(e, 0, "great-sacrifice-destroy", strategy.Id, five.Id, performer.Id));
        Eq(e.State.Cards[five.Id].Zone, Zone.Dead, "the enemy unit is destroyed"); Eq(e.State.Cards[performer.Id].Zone, Zone.Dead, "and so is the performer, as the cost");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");

        // Defending adds one: player 1 attacks, player 0 defends with the same performer (reach 6): the Force-6 unit is now within it.
        var d = Table(Phase.Battle, 1); d.PriorityPlayerId = 0;
        var defender = Add(d, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(d, GreatSacrifice, 0, Zone.Hand);
        var big = Add(d, "training-lion-veteran", 1, Zone.Battlefield, province: 0);                 // Force 6
        var e2 = MatchEngine.FromTestState(d);
        Check(Enabled(e2, 0, "great-sacrifice-destroy", card.Id, defender.Id).TargetIds!.Contains(big.Id), "defending: Force 3 + Personal Honor 2 + 1 reaches a Force-6 unit");
        // Personal Honor counts as it is now: a Personal Honor penalty shortens the reach.
        var d2 = Table(Phase.Battle, 1); d2.PriorityPlayerId = 0;
        var weak = Add(d2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); weak.Modifiers.Add(new("test", "test", "PersonalHonor", -2));
        var card2 = Add(d2, GreatSacrifice, 0, Zone.Hand);
        var big2 = Add(d2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(d2), 0, "great-sacrifice-destroy", card2.Id, weak.Id) || !Enabled(MatchEngine.FromTestState(d2), 0, "great-sacrifice-destroy", card2.Id, weak.Id).TargetIds!.Contains(big2.Id), "Personal Honor is the current value");
    }

    private static void GreatSacrificeLegality()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var bowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home);
        var strategy = Add(s, GreatSacrifice, 0, Zone.Hand);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "great-sacrifice-destroy", strategy.Id, bowed.Id), "a bowed performer cannot be sacrificed");
        Check(!CanUse(e, 0, "great-sacrifice-destroy", strategy.Id, home.Id), "a performer at home is not at the battle");
        var ok = Enabled(e, 0, "great-sacrifice-destroy", strategy.Id, own.Id);
        Check(ok.TargetIds!.SequenceEqual(new[] { enemy.Id }), "only the enemy unit: " + string.Join(",", ok.TargetIds!));
        Reject(e, ok.Command! with { TargetId = own.Id });
        // The action is negated whole: the cost is still paid (an atomic, declared cost) and nothing is destroyed.
        var n = MatchEngine.FromTestState(s); n.NegateEveryAction = true;
        Accept(n, ok.Command! with { TargetId = enemy.Id, ExpectedVersion = n.Version });
        Check(n.State.Cards[enemy.Id].Zone != Zone.Dead, "a negated Great Sacrifice destroys nothing");
        Eq(n.State.Cards[own.Id].Zone, Zone.Dead, "but its performer was still paid as the cost");
    }

    // ---- Rage of the Spirits ------------------------------------------------------------------------------------------------------------------------
    private sealed record RageRig(MatchState S, CardInstance Shugenja, CardInstance Spell, CardInstance Spirit, CardInstance Enemy);
    private static RageRig RageTable(bool earth = true, bool shugenjaBowed = false, bool spiritBowed = false)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home, bowed: shugenjaBowed);            // his unit is NOT at the battlefield
        if (earth) GiveKeyword(shugenja, "Earth");
        var spell = Add(s, Rage, 0, Zone.Attached, parent: shugenja.Id);
        var spirit = Add(s, "training-lion-samurai", 0, Zone.Battlefield, bowed: spiritBowed, province: 0); GiveKeyword(spirit, "Spirit");   // Force 4
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);           // Force 3
        return new(s, shugenja, spell, spirit, enemy);
    }

    private static void RageOfTheSpirits()
    {
        foreach (var earth in new[] { true, false })
        {
            var r = RageTable(earth);
            var e = MatchEngine.FromTestState(r.S);
            var honor = Honor(e, 0);
            Accept(e, Offer(e, 0, "rage-of-the-spirits-destroy", r.Spell.Id, r.Enemy.Id, r.Spirit.Id));
            Eq(e.State.Cards[r.Enemy.Id].Zone, Zone.Dead, "the enemy card is destroyed");
            Check(e.State.Cards[r.Shugenja.Id].Bowed && e.State.Cards[r.Spirit.Id].Bowed, "the Shugenja and the Spirit are both bowed");
            Eq(Honor(e, 0), earth ? honor + 1 : honor, earth ? "an Earth Shugenja gains 1 Honor" : "a Shugenja who is not Earth gains none");
        }
    }

    private static void RageCosts()
    {
        var bowedShugenja = RageTable(shugenjaBowed: true);
        Check(!CanUse(MatchEngine.FromTestState(bowedShugenja.S), 0, "rage-of-the-spirits-destroy", bowedShugenja.Spell.Id), "a bowed Shugenja cannot be bowed as the cost");
        var bowedSpirit = RageTable(spiritBowed: true);
        Check(!CanUse(MatchEngine.FromTestState(bowedSpirit.S), 0, "rage-of-the-spirits-destroy", bowedSpirit.Spell.Id), "a bowed Spirit is not offered as the performer");
        var r = RageTable();
        var strong = Add(r.S, "training-lion-veteran", 1, Zone.Battlefield, province: 0);           // Force 6 > 4
        var armed = Add(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(r.S, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        var notSpirit = Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        Check(!CanUse(e, 0, "rage-of-the-spirits-destroy", r.Spell.Id, notSpirit.Id), "the performer must be a Spirit");
        var option = Enabled(e, 0, "rage-of-the-spirits-destroy", r.Spell.Id, r.Spirit.Id);
        Check(option.TargetIds!.Contains(r.Enemy.Id) && !option.TargetIds.Contains(strong.Id) && !option.TargetIds.Contains(armed.Id), "Force at most the Spirit's, and no attachments: " + string.Join(",", option.TargetIds!));
        Reject(e, option.Command! with { TargetId = strong.Id }); Reject(e, option.Command! with { TargetId = armed.Id });
        // The Spirit's own Force is read live: a +2 bonus brings the Force-6 card within reach.
        r.S.Cards[r.Spirit.Id].Modifiers.Add(new("test", "test", "Force", 2));
        Check(Enabled(MatchEngine.FromTestState(r.S), 0, "rage-of-the-spirits-destroy", r.Spell.Id, r.Spirit.Id).TargetIds!.Contains(strong.Id), "the Spirit's current Force is the limit");
    }

    // ---- On the Hunt --------------------------------------------------------------------------------------------------------------------------------
    private static void OnTheHuntEquips()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(samurai, "Samurai");
        var notSamurai = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, OnTheHunt, 0, Zone.Hand);
        var cheap = Add(s, "training-weapon", 0, Zone.Hand);                  // 1 Gold: free (never below 0)
        var blade = Add(s, "oracle-l5r-1004", 0, Zone.Hand);                  // Blade of Champions, 5 Gold: 1 Gold
        Add(s, "training-border-keep", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "on-the-hunt-equip", strategy.Id, notSamurai.Id), "only a Samurai may perform");
        var option = Enabled(e, 0, "on-the-hunt-equip", strategy.Id, samurai.Id);
        Check(option.TargetIds!.Contains(cheap.Id) && option.TargetIds.Contains(blade.Id), "attachments in the hand are the targets");
        var quotes = option.TargetGold!;
        Eq(quotes[cheap.Id].Cost, 0, "a 1-Gold Weapon costs nothing"); Eq(quotes[blade.Id].Cost, 1, "a 5-Gold Item costs 5 - 4 = 1");
        var goldBefore = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed && c.Zone is Zone.Stronghold or Zone.Home);
        Accept(e, OfferPaid(e, 0, "on-the-hunt-equip", strategy.Id, blade.Id, samurai.Id));
        Check(e.State.Cards[blade.Id].Zone == Zone.Attached && e.State.Cards[blade.Id].AttachedTo == samurai.Id, "the Item is attached to the performer");
        Check(!e.State.Players[0].Hand.Contains(blade.Id) && e.State.Events.Any(x => x.Type == "equipped" && x.Message.Contains("On the Hunt")), "out of the hand, logged");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed && c.Zone is Zone.Stronghold or Zone.Home) > goldBefore, "the Gold was paid by bowing a source");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // A Personality who cannot take the attachment (a second Armor/Weapon rule) refuses: the Blade is a Weapon, so a second Weapon without Kensai is not legal.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var armed = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(armed, "Samurai");
        Add(s2, "training-weapon", 0, Zone.Attached, province: 0, parent: armed.Id);
        var st2 = Add(s2, OnTheHunt, 0, Zone.Hand); Add(s2, "training-two-handed-weapon", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "on-the-hunt-equip", st2.Id, armed.Id), "the ordinary attachment limits apply (one Weapon)");
    }

    private static void OnTheHuntStraightens()
    {
        var s = Table(Phase.Action, 0);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(samurai, "Samurai");
        var item = Add(s, "training-item", 0, Zone.Attached, bowed: true, parent: samurai.Id);
        var bare = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(bare, "Samurai");
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true);
        Add(s, "training-item", 0, Zone.Attached, bowed: true, parent: courtier.Id);
        var strategy = Add(s, OnTheHunt, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "on-the-hunt-straighten", strategy.Id, samurai.Id), "a Samurai with an attachment");
        Check(!CanUse(e, 0, "on-the-hunt-straighten", strategy.Id, bare.Id), "not one without an attachment");
        Check(!CanUse(e, 0, "on-the-hunt-straighten", strategy.Id, courtier.Id), "not a non-Samurai");
        Accept(e, Enabled(e, 0, "on-the-hunt-straighten", strategy.Id, samurai.Id).Command!);
        Check(!e.State.Cards[samurai.Id].Bowed && !e.State.Cards[item.Id].Bowed, "his unit is straightened: the Samurai and his attachment");
        Check(e.State.Cards[bare.Id].Bowed && e.State.Cards[courtier.Id].Bowed, "no other unit is");
    }

    // ---- Tireless Efforts ------------------------------------------------------------------------------------------------------------------------------
    private static void TirelessStraightens()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var bowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var strategy = Add(s, Tireless, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "tireless-efforts-straighten", strategy.Id, home.Id), "a Personality at home is not at the battle");
        Accept(e, Enabled(e, 0, "tireless-efforts-straighten", strategy.Id, bowed.Id).Command!);
        Check(!e.State.Cards[bowed.Id].Bowed, "straightened, though he was bowed");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
    }

    private static void TirelessNegates()
    {
        // In the Action phase (its Open half): the current penalty goes, the bonus stays, a new penalty this turn is not applied, a Chi penalty still is.
        var s = Table(Phase.Action, 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        target.Modifiers.Add(new("test", "penalty", "Force", -2)); target.Modifiers.Add(new("test", "bonus", "Force", 1)); target.Modifiers.Add(new("test", "chi-penalty", "Chi", -1));
        var strategy = Add(s, Tireless, 0, Zone.Hand);
        var enemy = Add(s, "training-battle-bower", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(View(e, target.Id).Force, 2, "Force 3 - 2 + 1 before");
        Accept(e, Enabled(e, 0, "tireless-efforts-negate", strategy.Id, target.Id).Command!);
        Eq(View(e, target.Id).Force, 4, "the -2 is gone, the +1 stays");
        Check(e.State.Cards[target.Id].Modifiers.Count(m => m.SuppressedTurn != e.State.TurnNumber) == 2 && e.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Chi" && m.Amount == -1 && m.SuppressedTurn is null), "the Chi penalty is not touched");
        Eq(e.State.Cards[target.Id].ForcePenaltiesNegatedTurn, e.State.TurnNumber, "the flag is set for this turn");
        // A NEW penalty this turn (a Battle action of the enemy's own: -2F) is not applied. Built as a second position with the flag already set.
        var b = Table(Phase.Battle, 1);
        var mine = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); mine.ForcePenaltiesNegatedTurn = b.TurnNumber;
        var kouki = Add(b, "training-penalty-source", 1, Zone.Battlefield, province: 0);
        var other = Add(b, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        var forceBefore = View(eb, mine.Id).Force;
        Accept(eb, Offer(eb, 1, "training-penalty-force", kouki.Id, mine.Id));
        for (var guard = 0; eb.State.PendingChoice is { Type: "beforeActionReaction" } && guard < 6; guard++) Accept(eb, new GameCommand("declineReaction", eb.State.PriorityPlayerId, eb.Version));
        Eq(View(eb, mine.Id).Force, forceBefore, "a new Force penalty is not applied while it lasts");
        Check(eb.State.Events.Any(x => x.Type == "modifierNegated" && x.Message.Contains("Tireless Efforts")), "logged");
        // The same penalty on a Personality without the flag is applied, and the flag lapses with the turn.
        Eq(View(eb, other.Id).Force, 4, "the fixture penalty is -2F: Force 4 -> the control below shows it applies where nothing is negated");
        var t = e.State.TurnNumber;
        RunUntil(e, () => e.State.TurnNumber > t);
        Check(e.State.Cards[target.Id].ForcePenaltiesNegatedTurn != e.State.TurnNumber, "the turn has moved on: the flag no longer applies");
        _ = enemy;
    }

    // ---- Costly Opportunity -----------------------------------------------------------------------------------------------------------------------
    private static void CostlyOpportunity()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(samurai, "Samurai");   // the performer, destroyed as the cost
        var strategy = Add(s, Costly, 0, Zone.Hand);
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);    // a lone Follower: no attachments of its own
        var sibling = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: host.Id);
        var bystander = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);         // another unit: untouched
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "costly-opportunity-destroy", strategy.Id, samurai.Id);
        Check(option.TargetIds!.Contains(target.Id) && option.TargetIds.Contains(bystander.Id) && !option.TargetIds.Contains(host.Id), "cards without attachments: the Follower and the bare Personality, not the armed host");
        Accept(e, OfferPaid(e, 0, "costly-opportunity-destroy", strategy.Id, target.Id, samurai.Id));
        Eq(e.State.Cards[samurai.Id].Zone, Zone.Dead, "the Samurai was destroyed as the cost");
        Eq(e.State.Cards[target.Id].Zone, Zone.FateDiscard, "the targeted Follower is destroyed");
        Check(e.State.Cards[host.Id].ActionLocks.Count > 0 && e.State.Cards[sibling.Id].ActionLocks.Count > 0, "every other card in its unit -- the host and the other attachment -- may not use its abilities");
        Check(e.State.Cards[bystander.Id].ActionLocks.Count == 0, "no other unit is affected");
        Check(View(e, host.Id, 1).ActionLocked, "and the lock is visible");
        // A bare Personality target is simply destroyed: nothing else is in its unit.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var sam2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(sam2, "Samurai"); var st2 = Add(s2, Costly, 0, Zone.Hand);
        var lone = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "costly-opportunity-destroy", st2.Id, lone.Id, sam2.Id));
        Eq(e2.State.Cards[lone.Id].Zone, Zone.Dead, "destroyed");
        // Only a Samurai may be sacrificed, and he must be unbowed.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var bowed = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(bowed, "Samurai"); var st3 = Add(s3, Costly, 0, Zone.Hand);
        var courtier = Add(s3, "training-crane-courtier", 0, Zone.Battlefield, province: 0); Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "costly-opportunity-destroy", st3.Id, bowed.Id), "a bowed Samurai cannot be sacrificed");
        Check(!CanUse(e3, 0, "costly-opportunity-destroy", st3.Id, courtier.Id), "a non-Samurai cannot perform");
    }

    // ---- Silent Terror -----------------------------------------------------------------------------------------------------------------------------
    private static void SilentTerror_()
    {
        MatchEngine Build(out CardInstance ninja, out CardInstance enemy, out CardInstance card, bool emptyProvince = false)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            ninja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(ninja, "Ninja");
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            card = Add(s, SilentTerror, 0, Zone.Hand);
            if (emptyProvince) { var province = s.Players[1].Provinces.Single(p => p.Index == 0); if (province.CardId is { } id) { s.Cards[id].Zone = Zone.DynastyDiscard; s.Cards[id].ProvinceIndex = null; } province.CardId = null; }
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var ninja, out var enemy, out var card);
        var province0 = e.State.Players[1].Provinces.Single(p => p.Index == 0); var sitting = province0.CardId!;
        Accept(e, Offer(e, 0, "silent-terror-bow", card.Id, enemy.Id, ninja.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the target is bowed");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "a card sits in the province: the acting player is asked");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).OrderBy(x => x).ToArray();
        Check(choices.SequenceEqual(new[] { "discard", "keep" }), "discard or not: " + string.Join(",", choices));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "discard").Command!);
        Eq(e.State.Cards[sitting].Zone, Zone.DynastyDiscard, "the province's card was discarded, and is public in the discard pile");
        var after = e.State.Players[1].Provinces.Single(p => p.Index == 0);
        Check(after.CardId is not null && after.CardId != sitting, "the province was refilled");
        Check(e.State.PendingChoice is null, "the action finished");
        // Declining.
        var k = Build(out var ninja2, out var enemy2, out var card2);
        var kept = k.State.Players[1].Provinces.Single(p => p.Index == 0).CardId!;
        Accept(k, Offer(k, 0, "silent-terror-bow", card2.Id, enemy2.Id, ninja2.Id));
        Accept(k, k.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "keep").Command!);
        Check(k.State.Cards[kept].Zone == Zone.Province && k.State.Players[1].Provinces.Single(p => p.Index == 0).CardId == kept, "nothing was discarded");
        // No card in the province: nothing is asked, the bow still happens.
        var n = Build(out var ninja3, out var enemy3, out var card3, emptyProvince: true);
        Accept(n, Offer(n, 0, "silent-terror-bow", card3.Id, enemy3.Id, ninja3.Id));
        Check(n.State.Cards[enemy3.Id].Bowed && n.State.PendingChoice is null, "an empty province asks nothing");
        // The opponent never sees the choice as anything but a public one; the face-down card's identity is public only once discarded.
        var hidden = Build(out var ninja4, out var enemy4, out var card4);
        HiddenIdsAbsent(hidden.GetView(1), [card4.Id]);
    }

    // ---- Essence of Undeath ------------------------------------------------------------------------------------------------------------------------
    private sealed record UndeathRig(MatchState S, CardInstance Shugenja, CardInstance Spell, CardInstance Dead);
    private static UndeathRig UndeathTable(bool atBattle)
    {
        var s = atBattle ? Table(Phase.Battle, 0) : Table(Phase.Action, 0);
        s.PriorityPlayerId = 0;
        var shugenja = atBattle ? Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0) : Add(s, "training-shugenja", 0, Zone.Home);
        var spell = Add(s, Essence, 0, Zone.Attached, province: atBattle ? 0 : null, parent: shugenja.Id);
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead);                                   // 5 Gold, Honor Requirement 4
        Add(s, "training-border-keep", 0, Zone.Home); Add(s, "training-border-keep", 0, Zone.Home); Add(s, "training-border-keep", 0, Zone.Home);
        if (atBattle) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return new(s, shugenja, spell, dead);
    }

    private static void EssenceOfUndeath()
    {
        foreach (var atBattle in new[] { false, true })
        {
            var r = UndeathTable(atBattle);
            var e = MatchEngine.FromTestState(r.S);
            var honor = Honor(e, 0);
            var option = Enabled(e, 0, "essence-of-undeath-recruit", r.Spell.Id, r.Shugenja.Id);
            Check(option.TargetIds!.SequenceEqual(new[] { r.Dead.Id }), "only my own dead Personality is a target: " + string.Join(",", option.TargetIds!));
            var command = OfferPaid(e, 0, "essence-of-undeath-recruit", r.Spell.Id, r.Dead.Id, r.Shugenja.Id);
            var price = option.TargetGold![r.Dead.Id].Cost;
            Accept(e, command);
            var back = e.State.Cards[r.Dead.Id];
            Check(back.Zone == (atBattle ? Zone.Battlefield : Zone.Home) && (atBattle ? back.ProvinceIndex == 0 : back.ProvinceIndex is null), atBattle ? "he enters at the battlefield where his Shugenja stands" : "he enters at home, where his Shugenja is");
            Check(e.HasKeyword(back.Id, "Shadowlands") && e.HasKeyword(back.Id, "Undead") && back.GrantedKeywords.All(g => g.Duration == GrantDuration.Permanent), "permanently Shadowlands and Undead");
            Eq(e.State.Cards[r.Spell.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
            Eq(Honor(e, 0), honor - 2, "and 2 Honor lost");
            Check(price > 0 && e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed && c.Zone is Zone.Stronghold or Zone.Home) >= 1, $"he was paid for in full ({price} Gold)");
            Check(e.State.PendingEntryEffectsCardId is null && e.State.PendingChoice?.Type != "proclaim", "and cannot be proclaimed");
        }
        // Not affordable, not dead, not mine: not offered.
        var poor = UndeathTable(false);
        foreach (var c in poor.S.Cards.Values.Where(c => c.DefinitionId == "training-border-keep").ToArray()) poor.S.Cards.Remove(c.Id);
        poor.S.Cards[poor.S.Players[0].StrongholdId].Bowed = true;
        var e2 = MatchEngine.FromTestState(poor.S);
        var options = Options(e2, 0, "essence-of-undeath-recruit").Where(o => o.Command is not ({ ClanDiscount: true } or { ClanAllegiance: true })).ToArray(); // issue #1232: at full cost (the Clan discount is 2 less)
        Check(options.Length > 0 && options.All(o => !o.Enabled), "with no Gold to pay he cannot be brought back: " + string.Join("; ", options.Select(o => o.Reason)));
        var theirs = UndeathTable(false);
        var enemyDead = Add(theirs.S, "training-lion-samurai", 1, Zone.Dead);
        var e3 = MatchEngine.FromTestState(theirs.S);
        Check(!Enabled(e3, 0, "essence-of-undeath-recruit", theirs.Spell.Id, theirs.Shugenja.Id).TargetIds!.Contains(enemyDead.Id), "the opponent's dead Personality is not a target");
    }

    // ---- Goju Plot ------------------------------------------------------------------------------------------------------------------------------------
    private static void GojuPlotRecruits()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var performer = Add(s, "training-shadowlands-personality", 0, Zone.Battlefield, province: 0); GiveKeyword(performer, "Ninja");
        var plot = Add(s, GojuPlot, 0, Zone.Hand);
        var deadNinja = Add(s, "training-crane-veteran", 0, Zone.Dead); GiveKeyword(deadNinja, "Ninja");            // cost 8
        var discardedNinja = Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard); GiveKeyword(discardedNinja, "Ninja");   // cost 5
        var notNinja = Add(s, "training-crane-courtier", 0, Zone.Dead);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Dead); GiveKeyword(theirs, "Ninja");
        Add(s, "training-border-keep", 0, Zone.Home); Add(s, "training-border-keep", 0, Zone.Home); Add(s, "training-border-keep", 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var performerCost = CardCatalog.Get("training-shadowlands-personality").GoldCost;
        var option = Enabled(e, 0, "goju-plot-recruit", plot.Id, performer.Id);
        var legal = option.TargetIds!.ToHashSet();
        Check(legal.Contains(deadNinja.Id) && legal.Contains(discardedNinja.Id) && !legal.Contains(notNinja.Id) && !legal.Contains(theirs.Id), "my Ninja Personalities in my discard pile (dead or discarded), and no others: " + string.Join(",", legal)
            + " | dead=" + deadNinja.Id + " discarded=" + discardedNinja.Id + " notNinja=" + notNinja.Id + " theirs=" + theirs.Id + " | " + string.Join("; ", Options(e, 0, "goju-plot-recruit").Select(o => o.Label + " [" + o.Enabled + "] " + o.Reason)));
        var quote = option.TargetGold![discardedNinja.Id];
        var plain = QuoteFor(e, discardedNinja.Id);
        Eq(quote.Cost, Math.Max(0, plain - performerCost), $"the recruit cost {plain} less the performer's Gold Cost {performerCost}");
        var honor = Honor(e, 0);
        Accept(e, OfferPaid(e, 0, "goju-plot-recruit", plot.Id, discardedNinja.Id, performer.Id));
        Eq(e.State.Cards[discardedNinja.Id].Zone, Zone.Battlefield, "he is at the current battlefield"); Eq(e.State.Cards[discardedNinja.Id].ProvinceIndex, 0);
        Eq(e.State.Cards[performer.Id].Zone, Zone.Dead, "the performer was destroyed as the cost");
        Eq(Honor(e, 0), honor - 2, "2 Honor lost");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action may be taken");
        Check(e.State.PendingChoice?.Type != "proclaim", "he may not be proclaimed");
    }

    private static int QuoteFor(MatchEngine e, string cardId) => CardCatalog.Get(e.State.Cards[cardId].DefinitionId).GoldCost;

    private static void OfferedCommandsValidateFresh()
    {
        MatchState Build()
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(performer, "Samurai"); GiveKeyword(performer, "Ninja"); GiveKeyword(performer, "Spirit");
            Add(s, GreatSacrifice, 0, Zone.Hand); Add(s, OnTheHunt, 0, Zone.Hand); Add(s, Tireless, 0, Zone.Hand); Add(s, Costly, 0, Zone.Hand); Add(s, SilentTerror, 0, Zone.Hand);
            Add(s, "training-weapon", 0, Zone.Hand);
            var shugenja = Add(s, "training-shugenja", 0, Zone.Home); Add(s, Rage, 0, Zone.Attached, parent: shugenja.Id);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            return s;
        }
        var e = MatchEngine.FromTestState(Build());
        var ids = new[] { "great-sacrifice-destroy", "on-the-hunt-equip", "tireless-efforts-straighten", "tireless-efforts-negate", "costly-opportunity-destroy", "silent-terror-bow", "rage-of-the-spirits-destroy" };
        var offered = e.QueryActions(0).Where(a => a.Enabled && a.Command?.AbilityId is { } id && ids.Contains(id)).Select(a => a.Command!.AbilityId).Distinct().ToArray();
        Check(offered.Length == ids.Length, "all are offered: " + string.Join(",", offered));
        OfferedCommandsValidate(e, () => MatchEngine.FromTestState(Build()), alternateTargets: true);
    }
}
