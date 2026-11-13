using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Generalized Gold payment (issue #303): the ONE authoritative quote. GoldParityTests pins that every rule
// still costs what it cost; these cases pin what is new -- every costed action exposes a GoldQuote on its
// ActionOption (fixed, target-derived with a per-target map, player-chosen variable), the engine validates
// and charges exactly that quote, and the abilities the old engine offered with a doomed payment (or never
// charged at all) now behave.
internal static class GoldQuoteTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("A fixed-cost ability now exposes its quote: Kharmic is offered enabled with 2 Gold and the engine's suggested sources", KharmicOffered),
            ("An entering-play Invest is offered with its fixed quote and suggested sources, and disabled when unaffordable", InvestOffered),
            ("A non-Strategy Discipline is now offered (it was disabled by a stale cost chain) and its cost is actually charged", DisciplineCharged),
            ("Noritada's Invest now charges its 3 Gold (it validated the payment but never bowed the sources)", NoritadaCharged),
            ("Low Market is a target-derived quote: every legal target ships its own quote, priced 4-2 and the 1 floor", LowMarketPerTarget),
            ("Equip (Battle) and Your Clan Needs You! ship per-target quotes; Shinden Shorai ships the recruit quote", DerivedFamily),
            ("Unit-total rules: Disgraceful Conduct's total minus 4 (floor 0) and A Stain Cleansed's total plus 2, over a Personality plus its attachments", UnitTotals),
            ("At Any Cost's shape prices from a dead Personality's own printed cost", DeadCardCost),
            ("Urgency's shape: a fixed 2 Gold ability draws a card and bows exactly the chosen sources", FixedAbility),
            ("Variable cost: the quote carries min, max and per-amount payments; the chosen amount is validated, charged and read by the effect", VariableAmount),
            ("Variable cost bounds: below min, above the rule's max, above production, stray amounts and sources for a free amount are all rejected atomically", VariableBounds),
            ("Variable cost: excess production is lost exactly as in every other payment", VariableExcessLost),
            ("A GoldAmount on a fixed or derived cost is rejected; the same field on a free ability is rejected too", StrayAmount),
            ("QuoteAbilityGold on a partial command: unknown ability is null, a derived cost with no target reports it, an out-of-range amount reports it", PartialCommands),
            ("playStrategy pays its printed cost: the option carries a fixed quote and suggested sources, an unpayable one is disabled, the command charges", PlayStrategyPaid),
            ("Purchase and Reserve options carry a Gold quote with a term-by-term basis that agrees with the purchase quote", PurchaseAndReserveQuotes),
            ("Ordinary equip ships per-target quotes when the wielder changes the price (Katagi) and a single quote otherwise", EquipQuotes),
            ("Every enabled offered command of the cost fixture validates on a fresh replay, including the variable-cost ones", FixtureOffersValidate),
            ("A Spell in hand whose Battle ability is wielder-relative (The Earth's Hunger) no longer makes QueryActions throw during a battle", UnattachedSpellInHand),
            ("Quotes survive JSON round trips with Gold, TargetGold and per-amount payments intact", QuoteJson),
        ];
        runner.Run("gold-quote", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static ActionOption Option(MatchEngine e, Func<ActionOption, bool> where, int player = 0) =>
        e.QueryActions(player).Single(where);
    private static ActionOption Ability(MatchEngine e, string abilityId, int player = 0) =>
        e.QueryActions(player).Single(a => a.Type == "useAbility" && (a.Command?.AbilityId == abilityId || a.Label.StartsWith(AbilityCatalog.All[abilityId].Label))
            && a.Command is not ({ ClanDiscount: true } or { ClanAllegiance: true })); // issue #1232: the full-cost way to pay, not the Clan discount / allegiance ones

    private static int Production(MatchEngine e) =>
        e.State.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)
            .Sum(c => e.GoldProduced(c.Id, 0));

    // The offered command, retargeted and re-sourced exactly the way the client does it from a quote.
    private static GameCommand Retarget(ActionOption option, string target) =>
        option.Command! with { TargetId = target, SourceIds = option.TargetGold![target].Payments[0].SourceIds };

    private static (MatchEngine E, CardInstance Source) Fixture(Action<MatchState, CardInstance>? more = null, int marketplaces = 6)
    {
        var s = GoldParityTests.Action();
        for (var i = 0; i < marketplaces; i++) GoldParityTests.Add(s, "training-marketplace", 0, Zone.Home);
        var source = GoldParityTests.Add(s, "training-cost-fixture", 0, Zone.Home);
        more?.Invoke(s, source);
        return (MatchEngine.FromTestState(s), source);
    }

    private static void PaidEvent(MatchEngine e, int cost)
    {
        var payment = e.State.Events.Last(x => x.Type == "payment");
        Check(payment.Message.Contains($"paid {cost} Gold"), payment.Message);
    }

    private static void KharmicOffered()
    {
        var (e, _) = GoldParityTests.Kharmic();
        var option = Ability(e, "kharmic-discard");
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.Gold?.Kind, GoldCostKind.Fixed);
        Eq(option.Gold!.Cost, 2);
        Check(option.Gold.Basis.Contains("Fixed by this ability"), option.Gold.Basis);
        Check(option.Gold.Payments.Length > 0 && option.Gold.Error is null);
        Check(option.TargetGold is null, "A fixed cost needs no per-target map");
        Check(option.Command!.SourceIds!.SequenceEqual(option.Gold.Payments[0].SourceIds), "The command carries the first suggested payment");
        Accept(MatchEngine.FromTestState(e.State), option.Command);
        // Unaffordable: the option is disabled with the shortfall instead of vanishing or being doomed.
        var poor = e.State; foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var broke = MatchEngine.FromTestState(poor);
        var disabled = Ability(broke, "kharmic-discard");
        Check(!disabled.Enabled && disabled.Reason!.Contains("2 Gold"), disabled.Reason ?? "");
    }

    private static void InvestOffered()
    {
        var (e, _) = GoldParityTests.TrainingInvest();
        var option = Option(e, a => a.Type == "useAbility" && a.Command!.AbilityId == "training-invest");
        Check(option.Enabled);
        Eq(option.Gold!.Cost, 3); Eq(option.Gold.Kind, GoldCostKind.Fixed);
        Check(option.Command!.SourceIds!.Length > 0, "The reaction now carries the engine's suggested sources");
        var before = MatchEngine.FromTestState(e.State);
        Accept(before, option.Command);
        Check(option.Command.SourceIds.All(id => before.State.Cards[id].Bowed));
        var poor = e.State; foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var broke = MatchEngine.FromTestState(poor);
        var disabled = Option(broke, a => a.Type == "useAbility" && a.Label.StartsWith("Training: Invest 3 Gold"));
        Check(!disabled.Enabled && disabled.Reason == "Insufficient unbowed Gold production.", disabled.Reason ?? "");
    }

    private static void DisciplineCharged()
    {
        var (e, command) = GoldParityTests.DisciplineItem();
        var option = Ability(e, "training-discipline-ability");
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.Gold!.Cost, 2);
        Accept(e, option.Command!);
        Check(option.Command!.SourceIds!.All(id => e.State.Cards[id].Bowed), "The Discipline's Gold cost is charged");
        PaidEvent(e, 2);
        Eq(e.State.Cards[e.State.Cards.Values.Single(c => c.DefinitionId == "training-discipline-item").Id].Zone, Zone.RemovedFromGame);
    }

    private static void NoritadaCharged()
    {
        var (e, command) = GoldParityTests.NoritadaInvest();
        var pay = e.PaymentOptions(0, 3).First();
        Accept(e, command(pay.SourceIds));
        Check(pay.SourceIds.All(id => e.State.Cards[id].Bowed), "Noritada's Invest now bows the sources that pay for it");
        PaidEvent(e, 3);
    }

    private static void LowMarketPerTarget()
    {
        var s = GoldParityTests.Action();
        var lowMarket = GoldParityTests.Add(s, "oracle-l5r-4777", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = lowMarket.Id;
        GoldParityTests.Add(s, "training-crane-clan-samurai", 0, Zone.Home);
        var guard = GoldParityTests.Add(s, "training-follower-crane-guard", 0, Zone.Hand);   // cost 4 -> 2
        var item = GoldParityTests.Add(s, "training-item", 0, Zone.Hand);                    // cost 2 -> 0 floored to 1
        var e = MatchEngine.FromTestState(s);
        var option = Ability(e, "low-market-equip");
        Check(option.Enabled, option.Reason ?? "");
        Check(option.TargetIds!.Contains(guard.Id) && option.TargetIds.Contains(item.Id), "Both attachments are legal targets");
        Check(option.TargetGold is not null && option.TargetIds!.All(option.TargetGold.ContainsKey), "One quote per legal target");
        Eq(option.TargetGold![guard.Id].Cost, 2); Eq(option.TargetGold[item.Id].Cost, 1);
        Eq(option.TargetGold[guard.Id].Kind, GoldCostKind.Derived);
        Check(option.TargetGold[item.Id].Basis.Contains("cost 2") && option.TargetGold[item.Id].Basis.Contains("− 2") && option.TargetGold[item.Id].Basis.Contains("min 1"), option.TargetGold[item.Id].Basis);
        Check(option.Gold == option.TargetGold[option.Command!.TargetId!], "Gold is the quote of the option's own (first legal) target");
        foreach (var target in new[] { guard.Id, item.Id })
            Accept(MatchEngine.FromTestState(e.State), Retarget(option, target));
        // Sources that do not cover the chosen target's own price are rejected atomically.
        Reject(MatchEngine.FromTestState(e.State), option.Command! with { TargetId = guard.Id, SourceIds = [] });
    }

    private static void DerivedFamily()
    {
        // Equip (Battle): per-Spell quotes.
        var s = GoldParityTests.Battle("oracle-l5r-1218");
        var shugenja = GoldParityTests.Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        GoldParityTests.Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var cheap = GoldParityTests.Add(s, "training-spell-with-ability", 0, Zone.Hand);
        var dear = GoldParityTests.Add(s, "oracle-l5r-9344", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var battle = Ability(e, "equip-battle");
        Check(battle.Enabled, battle.Reason ?? "");
        Eq(battle.TargetGold![cheap.Id].Cost, 1); Eq(battle.TargetGold[dear.Id].Cost, 2);
        Check(battle.TargetGold[dear.Id].Basis.Contains("cost 2"));
        foreach (var target in battle.TargetGold.Keys) Accept(MatchEngine.FromTestState(e.State), Retarget(battle, target));

        // Your Clan Needs You!: recruit quote minus 6.
        var (ycny, ycnyCommand) = GoldParityTests.YourClanNeedsYou("training-crane-veteran");
        var veteran = ycnyCommand([]).TargetId!;
        var ycnyState = ycny.State;
        var second = GoldParityTests.Add(ycnyState, "training-nonhuman-personality", 0, Zone.Province, province: 2);
        ycnyState.Players[0].Provinces[2].CardId = second.Id;
        // The fixture replaced province cards in place; drop the displaced ones so no orphan is offered as a target.
        foreach (var orphan in ycnyState.Cards.Values.Where(c => c.Zone == Zone.Province && !ycnyState.Players[c.OwnerId].Provinces.Any(p => p.CardId == c.Id)).ToArray()) ycnyState.Cards.Remove(orphan.Id);
        var e2 = MatchEngine.FromTestState(ycnyState);
        var rally = Ability(e2, "your-clan-needs-you");
        Check(rally.Enabled, rally.Reason ?? "");
        Eq(rally.TargetGold![second.Id].Cost, 0);
        Eq(rally.TargetGold[veteran].Cost, 2);
        Check(rally.TargetGold[veteran].Basis.Contains("recruit cost 8") && rally.TargetGold[veteran].Basis.Contains("− 6"), rally.TargetGold[veteran].Basis);
        Eq(rally.TargetGold[second.Id].Payments.Length, 1); // the free quote's single, empty payment
        foreach (var target in rally.TargetGold.Keys) Accept(MatchEngine.FromTestState(e2.State), Retarget(rally, target));

        // Shinden Shorai: the recruit's full quote, its Lion-Clan Honor Requirement waiver reflected in enabling.
        var (shorai, _) = GoldParityTests.ShindenShorai("training-lion-samurai");
        var shoraiOption = Ability(shorai, "shinden-shorai-recruit");
        Check(shoraiOption.Enabled, shoraiOption.Reason ?? "");
        Eq(shoraiOption.Gold!.Cost, 5); Eq(shoraiOption.Gold.Kind, GoldCostKind.Derived);
        Check(shoraiOption.Gold.Basis.Contains("recruit cost 5"), shoraiOption.Gold.Basis);
        Accept(MatchEngine.FromTestState(shorai.State), shoraiOption.Command!);
    }

    private static void UnitTotals()
    {
        var (e, source) = Fixture((s, _) =>
        {
            var veteran = GoldParityTests.Add(s, "training-lion-veteran", 1, Zone.Home);           // 8
            GoldParityTests.Add(s, "training-follower", 1, Zone.Attached, attachedTo: veteran.Id);  // 2
            GoldParityTests.Add(s, "training-item", 1, Zone.Attached, attachedTo: veteran.Id);      // 2   => unit 12
            GoldParityTests.Add(s, "training-lion-samurai", 1, Zone.Home);                          // 5
            GoldParityTests.Add(s, "training-nonhuman-personality", 1, Zone.Home);                  // 3
        });
        string Own(string definition) => e.State.Cards.Values.Single(c => c.DefinitionId == definition && c.OwnerId == 1 && c.Zone == Zone.Home).Id;
        var veteranId = Own("training-lion-veteran");
        var samuraiId = Own("training-lion-samurai");
        var smallId = Own("training-nonhuman-personality");

        var disgrace = Ability(e, "training-disgrace-bow");
        Eq(disgrace.Gold!.Kind, GoldCostKind.Derived);
        Eq(disgrace.TargetGold![veteranId].Cost, 8);                    // 12 - 4
        Eq(disgrace.TargetGold[samuraiId].Cost, 1);                     // 5 - 4
        Eq(disgrace.TargetGold[smallId].Cost, 0);                       // 3 - 4, floored at 0
        Check(disgrace.TargetGold[veteranId].Basis.Contains("8 + 2 + 2 = 12") && disgrace.TargetGold[veteranId].Basis.Contains("− 4") && disgrace.TargetGold[veteranId].Basis.Contains("min 0"), disgrace.TargetGold[veteranId].Basis);
        var pay = Retarget(disgrace, veteranId) with { CardId = source.Id };
        Accept(e, pay);
        Check(e.State.Cards[veteranId].Bowed, "The effect resolved");
        Check(pay.SourceIds!.All(id => e.State.Cards[id].Bowed)); PaidEvent(e, 8);

        var (e2, source2) = Fixture((s, _) =>
        {
            var veteran = GoldParityTests.Add(s, "training-lion-veteran", 1, Zone.Home, bowed: true);
            GoldParityTests.Add(s, "training-follower", 1, Zone.Attached, attachedTo: veteran.Id);
            GoldParityTests.Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        });
        var stain = Ability(e2, "training-stain-straighten");
        var unit = e2.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-veteran" && c.OwnerId == 1 && c.Zone == Zone.Home).Id;
        var lone = e2.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-samurai" && c.OwnerId == 1 && c.Zone == Zone.Home).Id;
        Eq(stain.TargetGold![unit].Cost, 12);                            // (8 + 2) + 2
        Eq(stain.TargetGold[lone].Cost, 7);                              // 5 + 2, no floor involved
        Check(stain.TargetGold[unit].Basis.Contains("8 + 2 = 10") && stain.TargetGold[unit].Basis.Contains("+ 2"), stain.TargetGold[unit].Basis);
        Accept(e2, Retarget(stain, lone) with { CardId = source2.Id });
        Check(!e2.State.Cards[lone].Bowed, "Straightened"); PaidEvent(e2, 7);
        // One Gold short of the derived amount is rejected atomically.
        var (e3, source3) = Fixture((s, _) => GoldParityTests.Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true));
        var stain3 = Ability(e3, "training-stain-straighten");
        var target3 = e3.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-samurai" && c.OwnerId == 1 && c.Zone == Zone.Home).Id;
        var under = e3.PaymentOptions(0, 6).First().SourceIds;
        Check(under.Sum(id => e3.GoldProduced(id, 0)) >= 6);
        Reject(e3, new GameCommand("useAbility", 0, e3.Version, CardId: source3.Id, TargetId: target3, AbilityId: "training-stain-straighten", SourceIds: [under[0]]) with { });
    }

    private static void DeadCardCost()
    {
        var (e, source) = Fixture((s, _) => GoldParityTests.Add(s, "training-crane-veteran", 0, Zone.Dead));
        var dead = e.State.Cards.Values.Single(c => c.Zone == Zone.Dead).Id;
        var option = Ability(e, "training-at-any-cost");
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.TargetGold![dead].Cost, 8); Eq(option.TargetGold[dead].Kind, GoldCostKind.Derived);
        Check(option.TargetGold[dead].Basis.Contains("cost 8"), option.TargetGold[dead].Basis);
        var honor = e.State.Players[0].Honor;
        Accept(e, Retarget(option, dead) with { CardId = source.Id });
        Eq(e.State.Players[0].Honor, honor + 1); PaidEvent(e, 8);
    }

    private static void FixedAbility()
    {
        var (e, source) = Fixture();
        var option = Ability(e, "training-urgency-draw");
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.Gold!.Kind, GoldCostKind.Fixed); Eq(option.Gold.Cost, 2);
        Check(option.TargetGold is null);
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, option.Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1);
        Check(option.Command!.SourceIds!.All(id => e.State.Cards[id].Bowed)); PaidEvent(e, 2);
        // Exactly the chosen sources bow: an alternative payment leaves the rest ready.
        var (e2, _) = Fixture();
        var alt = Ability(e2, "training-urgency-draw").Gold!.Payments.Last();
        var cmd = Ability(e2, "training-urgency-draw").Command! with { SourceIds = alt.SourceIds };
        Accept(e2, cmd);
        Eq(e2.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0), alt.SourceIds.Length);
    }

    private static void VariableAmount()
    {
        var (e, source) = Fixture();
        var production = Production(e);
        var option = Ability(e, "training-charity-honor");
        Check(option.Enabled, option.Reason ?? "");
        var quote = option.Gold!;
        Eq(quote.Kind, GoldCostKind.Variable); Eq(quote.MinAmount, 0); Eq(quote.MaxAmount, production);
        Eq(quote.Cost, 0, "The default amount is the minimum");
        Check(quote.Basis.Contains("zero or more"), quote.Basis);
        for (var amount = 0; amount <= production; amount++)
            Check(quote.PaymentsByAmount!.TryGetValue(amount, out var options) && options.Length > 0 && options.All(p => p.Produced >= amount && p.Cost == amount), $"Payments for {amount}");
        Check(quote.PaymentsByAmount![3].Length <= 8, "Alternatives per amount are bounded");
        Check(quote.PaymentsByAmount.Values.All(v => v.First().Excess == v.Min(p => p.Excess)), "Best first");
        Check(option.Command!.GoldAmount is null && option.Command.SourceIds is { Length: 0 }, "The offered command defaults to the free minimum");

        var chosen = quote.PaymentsByAmount[5][0];
        var honor = e.State.Players[0].Honor;
        Accept(e, option.Command with { GoldAmount = 5, SourceIds = chosen.SourceIds });
        Eq(e.State.Players[0].Honor, honor + 5, "The effect reads the amount actually chosen");
        Check(chosen.SourceIds.All(id => e.State.Cards[id].Bowed)); PaidEvent(e, 5);
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0), chosen.SourceIds.Length, "Only the chosen sources bowed");
        // The default (no amount, no sources) is a legal free use.
        var (free, _) = Fixture();
        var honorBefore = free.State.Players[0].Honor;
        Accept(free, Ability(free, "training-charity-honor").Command!);
        Eq(free.State.Players[0].Honor, honorBefore);
    }

    private static void VariableBounds()
    {
        var (e, source) = Fixture();
        var production = Production(e);
        GameCommand Charity(int? amount, string[]? sources, string id = "training-charity-honor") =>
            new("useAbility", 0, e.Version, CardId: source.Id, AbilityId: id, GoldAmount: amount, SourceIds: sources);
        string[] For(int amount) => e.PaymentOptions(0, amount).First().SourceIds;

        Reject(e, Charity(-1, []));
        Reject(e, Charity(3, For(2).Length == 0 ? [] : For(2)));                        // 2 Gold produced, 3 chosen
        Check(e.PaymentOptions(0, production + 1).Length == 0, "Nothing pays more than the total production");
        Reject(e, Charity(production + 1, e.State.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray()));
        Reject(e, Charity(0, For(3)));                                                   // a free amount takes no sources
        Accept(e, Charity(production, For(production)));                                 // the very top of the range
        Eq(Production(e), 0, "Everything bowed to pay the maximum");

        // A rule with its own bounds: 1..4.
        var (c, capped) = Fixture();
        GameCommand Capped(int? amount, string[]? sources) => new("useAbility", 0, c.Version, CardId: capped.Id, AbilityId: "training-charity-honor-capped", GoldAmount: amount, SourceIds: sources);
        var quote = Ability(c, "training-charity-honor-capped").Gold!;
        Eq(quote.MinAmount, 1); Eq(quote.MaxAmount, 4); Eq(quote.Cost, 1, "The default amount is the rule's minimum");
        Check(quote.PaymentsByAmount!.Keys.OrderBy(k => k).SequenceEqual([1, 2, 3, 4]), "Payments only for the legal amounts");
        Reject(c, Capped(0, []));                                                        // below the minimum
        Reject(c, Capped(5, c.PaymentOptions(0, 5).First().SourceIds));                  // above the maximum, though payable
        Reject(c, Capped(null, []));                                                     // the default is 1, which needs a source
        Accept(c, Capped(null, c.PaymentOptions(0, 1).First().SourceIds));               // the default amount, paid
        var (c2, capped2) = Fixture();
        Accept(c2, new GameCommand("useAbility", 0, c2.Version, CardId: capped2.Id, AbilityId: "training-charity-honor-capped", GoldAmount: 4, SourceIds: c2.PaymentOptions(0, 4).First().SourceIds));
        Eq(c2.State.Players[0].Honor, MatchEngine.FromTestState(Fixture().E.State).State.Players[0].Honor + 4);
    }

    private static void VariableExcessLost()
    {
        var (e, source) = Fixture();
        var single = e.State.Cards.Values.First(c => c.OwnerId == 0 && !c.Bowed && c.Zone == Zone.Home && c.DefinitionId == "training-marketplace");
        var honor = e.State.Players[0].Honor;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-charity-honor", GoldAmount: 1, SourceIds: [single.Id]));
        Eq(e.State.Players[0].Honor, honor + 1, "Pays 1, gains 1");
        var payment = e.State.Events.Last(x => x.Type == "payment");
        Check(payment.Message.Contains("paid 1 Gold") && payment.Message.Contains("producing 3") && payment.Message.Contains("2 excess was lost"), payment.Message);
        Check(e.State.Cards[single.Id].Bowed, "The whole source bowed; its excess Gold cannot fund anything else");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-charity-honor", GoldAmount: 1, SourceIds: [single.Id]));
    }

    private static void StrayAmount()
    {
        var (k, _) = GoldParityTests.Kharmic();
        var option = Ability(k, "kharmic-discard");
        Reject(k, option.Command! with { GoldAmount = 2 });
        Accept(k, option.Command);
        var (e, source) = Fixture();
        Reject(e, Ability(e, "training-urgency-draw").Command! with { GoldAmount = 2 });
        var target = Ability(e, "training-at-any-cost");
        var (e2, source2) = Fixture((s, _) => GoldParityTests.Add(s, "training-crane-veteran", 0, Zone.Dead));
        var derived = Ability(e2, "training-at-any-cost");
        Reject(e2, Retarget(derived, derived.TargetGold!.Keys.Single()) with { CardId = source2.Id, GoldAmount = 8 });
        Reject(e, Ability(e, "training-urgency-draw").Command! with { GoldAmount = 0, SourceIds = [] });
    }

    private static void PartialCommands()
    {
        var (e, source) = Fixture((s, _) => GoldParityTests.Add(s, "training-crane-veteran", 0, Zone.Dead));
        Check(e.QuoteAbilityGold(new GameCommand("useAbility", 0, e.Version, AbilityId: "no-such-ability")) is null);
        Check(e.QuoteAbilityGold(new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "shunya-honor")) is null, "A free ability has no quote");
        var noTarget = e.QuoteAbilityGold(new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-at-any-cost"))!;
        Eq(noTarget.Kind, GoldCostKind.Derived); Check(noTarget.Error is not null && noTarget.Payments.Length == 0, "A derived cost with no target is not payable yet");
        var over = e.QuoteAbilityGold(new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-charity-honor-capped", GoldAmount: 9))!;
        Check(over.Error is not null && over.Error.Contains("at most 4"), over.Error ?? "");
        var under = e.QuoteAbilityGold(new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-charity-honor-capped", GoldAmount: 0))!;
        Check(under.Error is not null && under.Error.Contains("at least 1"), under.Error ?? "");
        Check(e.QuoteAbilityGold(new GameCommand("useAbility", 7, e.Version, CardId: source.Id, AbilityId: "training-urgency-draw")) is null, "An unknown player has no quote");
    }

    private static void PlayStrategyPaid()
    {
        string paidId = "", freeId = "";
        MatchEngine Table(bool poor = false)
        {
            var s = GoldParityTests.Battle("oracle-l5r-1218");
            GoldParityTests.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            paidId = GoldParityTests.Add(s, "training-paid-rally", 0, Zone.Hand).Id;
            freeId = GoldParityTests.Add(s, "training-rally", 0, Zone.Hand).Id;
            if (poor) foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
            return MatchEngine.FromTestState(s);
        }
        var e = Table();
        var paid = Option(e, a => a.Type == "playStrategy" && a.CardId == paidId);
        var free = Option(e, a => a.Type == "playStrategy" && a.CardId == freeId);
        Check(paid.Enabled && paid.Gold is { Kind: GoldCostKind.Fixed, Cost: 2 } && paid.Gold.Basis.Contains("cost 2"), paid.Reason ?? "");
        Check(free.Gold is null && free.Command!.SourceIds is null, "A free Strategy has no quote and, as before, no sources");
        Check(paid.Command!.SourceIds!.SequenceEqual(paid.Gold!.Payments[0].SourceIds));
        Accept(e, free.Command!); // free still needs no payment
        var e2 = Table();
        var offered = Option(e2, a => a.Type == "playStrategy" && a.CardId == paidId);
        Reject(e2, offered.Command! with { SourceIds = null });
        Accept(e2, offered.Command);
        Check(offered.Command.SourceIds!.All(id => e2.State.Cards[id].Bowed)); PaidEvent(e2, 2);
        var brokeTable = Table(poor: true);
        var broke = Option(brokeTable, a => a.Type == "playStrategy" && a.CardId == paidId);
        Check(!broke.Enabled && broke.Reason == "Insufficient unbowed Gold production.", broke.Reason ?? "");
    }

    private static void PurchaseAndReserveQuotes()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-crane-samurai"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var purchases = e.QueryActions(0).Where(a => a.Type == "purchase" && a.CardId == cardId).ToArray();
        Eq(purchases.Length, 4);
        foreach (var option in purchases)
        {
            Check(option.Gold is not null && option.Quote is not null);
            Eq(option.Gold!.Cost, option.Quote!.Cost); Eq(option.Gold.Kind, GoldCostKind.Fixed);
            Check(option.Gold.Payments.SequenceEqual(option.Quote.Payments), "The same suggested payments");
            Check(option.Gold.Basis.Contains("Crane training samurai cost 5"), option.Gold.Basis);
        }
        var discount = purchases.Single(a => a.Quote!.ClanDiscount && !a.Quote.ClanAllegiance).Gold!;
        Eq(discount.Cost, 3); Check(discount.Basis.Contains("− 2 (Clan discount)"), discount.Basis);
        var allegiance = purchases.Single(a => !a.Quote!.ClanDiscount && a.Quote.ClanAllegiance).Gold!;
        Eq(allegiance.Cost, 7); Check(allegiance.Basis.Contains("+ 2 (Clan allegiance)"), allegiance.Basis);

        var (reserve, _) = GoldParityTests.Reserve();
        var reserveOptions = reserve.QueryActions(0).Where(a => a.Type == "recruitReserve").ToArray();
        Check(reserveOptions.Length > 0);
        foreach (var option in reserveOptions.Where(a => a.Enabled))
        {
            Check(option.Quote is not null && option.Gold is not null, "recruitReserve carries its purchase quote and Gold quote");
            Eq(option.Gold!.Cost, option.Quote!.Cost);
            Accept(MatchEngine.FromTestState(reserve.State), option.Command!);
        }
        // Prosperous-style clamp: a cost pushed below zero says so.
        var lowered = e.QuotePurchase(0, cardId, clanDiscount: true);
        Check(lowered.Basis is not null && !lowered.Basis.Contains("min 0"));
    }

    private static void EquipQuotes()
    {
        var s = GoldParityTests.Action();
        for (var i = 0; i < 2; i++) GoldParityTests.Add(s, "training-marketplace", 0, Zone.Home);
        var weapon = GoldParityTests.Add(s, "training-weapon", 0, Zone.Hand);
        var plain = GoldParityTests.Add(s, "training-crane-samurai", 0, Zone.Home);
        var katagi = GoldParityTests.Add(s, "oracle-l5r-5112", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Option(e, a => a.Type == "equip" && a.CardId == weapon.Id);
        Check(option.Enabled, option.Reason ?? "");
        Check(option.TargetGold is not null, "Katagi makes the price wielder-dependent");
        Eq(option.TargetGold![plain.Id].Cost, 1); Eq(option.TargetGold[katagi.Id].Cost, 0);
        Eq(option.TargetGold[katagi.Id].Kind, GoldCostKind.Derived); Eq(option.TargetGold[plain.Id].Kind, GoldCostKind.Fixed);
        Check(option.TargetGold[katagi.Id].Basis.Contains("− 4") && option.TargetGold[katagi.Id].Basis.Contains("min 0"), option.TargetGold[katagi.Id].Basis);
        foreach (var target in new[] { plain.Id, katagi.Id })
            Accept(MatchEngine.FromTestState(e.State), Retarget(option, target));
        // No wielder-dependent term: one quote and no map.
        var s2 = GoldParityTests.Action();
        GoldParityTests.Add(s2, "training-marketplace", 0, Zone.Home);
        var item = GoldParityTests.Add(s2, "training-item", 0, Zone.Hand);
        GoldParityTests.Add(s2, "training-crane-samurai", 0, Zone.Home); GoldParityTests.Add(s2, "training-crane-veteran", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var option2 = Option(e2, a => a.Type == "equip" && a.CardId == item.Id);
        Eq(option2.Gold!.Cost, 2); Check(option2.TargetGold is null); Eq(option2.TargetIds!.Length, 2);
        // Unaffordable: disabled with the shortfall.
        var poor = e2.State; foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var disabled = Option(MatchEngine.FromTestState(poor), a => a.Type == "equip" && a.CardId == item.Id);
        Check(!disabled.Enabled && disabled.Reason == "Insufficient unbowed Gold production.", disabled.Reason ?? "");
    }

    private static void FixtureOffersValidate()
    {
        var (e, _) = Fixture((s, _) =>
        {
            GoldParityTests.Add(s, "training-lion-samurai", 1, Zone.Home);
            GoldParityTests.Add(s, "training-crane-veteran", 0, Zone.Dead);
        });
        var enabled = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Enabled).ToArray();
        var ids = enabled.Select(a => a.Command!.AbilityId).ToHashSet();
        foreach (var expected in new[] { "training-urgency-draw", "training-charity-honor", "training-charity-honor-capped", "training-disgrace-bow", "training-stain-straighten", "training-at-any-cost" })
            Check(ids.Contains(expected), $"{expected} is offered");
        OfferedCommandsValidate(e, () => MatchEngine.FromTestState(e.State));
    }

    private static void UnattachedSpellInHand()
    {
        var s = GoldParityTests.Battle("oracle-l5r-1218");
        GoldParityTests.Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        GoldParityTests.Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var hunger = GoldParityTests.Add(s, "oracle-l5r-9889", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var actions = e.QueryActions(0);
        var own = actions.SingleOrDefault(a => a.Type == "useAbility" && a.CardId == hunger.Id);
        Check(own is null || !own.Enabled, "The unattached Spell's own ability is not usable from hand");
        Check(actions.Any(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId == "equip-battle"), "Equip (Battle) is offered for the Spell in hand");
    }

    private static void QuoteJson()
    {
        var (e, _) = Fixture((s, _) => GoldParityTests.Add(s, "training-lion-samurai", 1, Zone.Home));
        foreach (var option in e.QueryActions(0).Where(a => a.Gold is not null))
        {
            var back = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(option, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Eq(back.Gold!.Cost, option.Gold!.Cost); Eq(back.Gold.Kind, option.Gold.Kind); Eq(back.Gold.Basis, option.Gold.Basis);
            Eq(back.Gold.Payments.Length, option.Gold.Payments.Length);
            Eq(back.TargetGold?.Count, option.TargetGold?.Count);
            Eq(back.Gold.PaymentsByAmount?.Count, option.Gold.PaymentsByAmount?.Count);
            if (option.Gold.PaymentsByAmount is not null)
                foreach (var (amount, payments) in option.Gold.PaymentsByAmount)
                    Check(back.Gold.PaymentsByAmount![amount].Select(p => string.Join(",", p.SourceIds)).SequenceEqual(payments.Select(p => string.Join(",", p.SourceIds))));
        }
        // An older payload with no quotes still loads.
        var legacy = JsonSerializer.Deserialize<ActionOption>("{\"type\":\"purchase\",\"label\":\"legacy\",\"enabled\":true}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(legacy.Gold is null && legacy.TargetGold is null);
    }
}
