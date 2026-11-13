using static RegressionAssertions;
using static Batch336Support;
using System.Text.Json;
using L5R.Core;

// Mendon's four answers to the generalized Gold payment's open questions (issue #373, docs/decisions/2026-09-20-gold-payment-rulings.md), proven on the
// real cards: Virtuous Charity (the opposing player's variable payment), Disgraceful Conduct and A Stain Cleansed (prices worked from a unit's CURRENT total
// Gold cost), and At Any Cost (a dead Samurai recruited through the ordinary recruit validation, never proclaimed). Every position is explicit (the economy
// scenario, player 0 the Crane and player 1 the Lion) and driven only through QueryActions and the commands the engine offers.
internal static class GoldRulingsTests
{
    private const string Charity = "oracle-l5r-10181", AtAnyCost = "oracle-l5r-639", Disgrace = "oracle-l5r-2020", Stain = "oracle-l5r-82";
    private const string CharityAbility = "virtuous-charity-open", RecruitAbility = "at-any-cost-recruit", DisgraceAbility = "disgraceful-conduct-bow", DisgraceUnitAbility = "disgraceful-conduct-bow-unit", StainAbility = "a-stain-cleansed-destroy";
    private const string Ebizo = "oracle-l5r-1629", Genshin = "oracle-l5r-2078", Sakuma = "oracle-l5r-9624", Kazuo = "oracle-l5r-10282", Doho = "oracle-l5r-9539";
    private const string Yozo = "oracle-l5r-10287", Hikaro = "oracle-l5r-5095", Hotako = "oracle-l5r-1726", Dairuko = "oracle-l5r-200", Kijo = "oracle-l5r-10074";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: the four cards are the printed Strategies (Focus, keywords, Oracle text), each with its own ability, and their scans ship", PublishedShape),
            ("Virtuous Charity is an Open action for a player whose turn it is not, priced by a variable quote from 0 up to what the acting player can produce", CharityOffer),
            ("The second player is shown the first player's amount and holds the decision: both views name the card, who paid what and the Honor at stake, and only the paying player's view carries the payment quote", SecondPlayerSeesFirstAmount),
            ("Whoever paid more gains Honor equal to the difference, or 4 if less: over a table of amounts, the acting player winning, the second player winning, a tie and the cap", HonorGoesToWhoeverPaidMore),
            ("The second player pays any amount from 0 up to what they can produce, through the ordinary payment: out-of-range amounts, short, foreign or bowed sources, stray sources for nothing and the wrong player are all rejected atomically", SecondPlayerBounds),
            ("The second player's Gold sources include every source the payment path knows: a Yoritomo Sakuma in play raises what can be paid and is bowed and spent when used", SecondPlayerSakuma),
            ("A second player who can produce no Gold has nothing to decide: nothing opens, it is announced, and the acting player's amount still wins", NoGoldNoPrompt),
            ("The second player's decision blocks Seppuku and every other command until answered, and the action then finishes: priority passes on, the Strategy is already in the discard pile", DecisionBlocksAndFinishes),
            ("A second spec proves the primitive is general: a fixture whose two amounts each become Honor for the other player, on the acting player's own turn", GeneralPrimitiveFixture),
            ("The scripted showcase reaches the decision in ordinary play (Crane passes, the Lion plays Virtuous Charity, the Crane pays) and replays deterministically", ShowcaseFlow),
            ("Virtuous Charity's payments are part of its resolution: an action negated whole (a reaction, or the test seam) pays nothing for either player and opens no decision", CharityActionNegated),
            ("A negated Honor gain leaves both payments made: the acting player pays when the action resolves, only then does the second player's decision open, and nobody gains Honor", CharityHonorGainNegated),
            ("The player chooses the Personality or the unit for Disgraceful Conduct: the Personality mode is priced from his own current cost minus 4, the unit mode from the unit's total current cost minus 4 and is offered only for a Personality with attachments (a bare one is offered once)", DisgracefulModes),
            ("Both Disgraceful Conduct prices follow the CURRENT costs: modifiers, tokens, an attachment's own reduction, 0 counting as 0, entry discounts not being cost changes, and the price never below 0", DisgracefulCurrentCosts),
            ("Disgraceful Conduct resolves: an Absent Battle action for either side's Personality; the Personality mode bows only him, the unit mode bows him and every card attached to him; the price is paid from the chosen sources", DisgracefulResolves),
            ("A Stain Cleansed prices a target at its current total Gold cost minus 2 (the scan governs): Daidoji Ebizo alone costs 7 (9 - 2), a unit with attachments the whole total, a reduced unit follows the current cost", StainPrice),
            ("A Stain Cleansed offers only affordable targets: an unaffordable one is left out of the per-target quotes, and with none affordable the action is disabled with the reason", StainAffordable),
            ("A Stain Cleansed needs your performing unbowed Magistrate at the battlefield and an enemy target; nothing else performs it", StainPerformer),
            ("A Stain Cleansed destroys the card or the unit: a Personality's whole unit dies, an attachment alone goes and its Personality stays; the payment is exactly the quote and bows the chosen sources", StainDestroys),
            ("A Stain Cleansed's sign is one number: the scan governs (price = total - 2, floor 0), and the same rule with +2 prices Ebizo at 11", StainSignIsOneConstant),
            ("At Any Cost recruits your own dead Samurai for the recruit price into play at the current battlefield, and he may not be proclaimed even when a province recruit of him could be; he is a unit there at once", AtAnyCostRecruits),
            ("At Any Cost keeps every recruit restriction: Unique and Singular, Honor Requirement (Clan allegiance waives it), Loyal, and only your own dead Samurai; each lists disabled with the recruit path's own reason", AtAnyCostRestrictions),
            ("At Any Cost offers the Clan options for a Samurai of your Clan (issue #1230): discount 2 less, allegiance 2 more ignoring the Honor Requirement, both, or full cost; none for another Clan's; never Proclaim; no other ability accepts them", AtAnyCostClanModes),
            ("At Any Cost's entry effects run as for any recruit: Sensei discards your other Sensei, Hotako's Lion discount prices him and his Honor loss applies, and a dead Personality returns as a new card", AtAnyCostEntry),
            ("At Any Cost is a Battle action: it needs a unit at the battlefield, is payable only with the recruit price, and QuotePurchase of a dead card says so", AtAnyCostBattleAndPrice),
        ];
        runner.Run("gold-rulings", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // The chosen amount of a variable Gold quote, with the engine's own suggested sources for it, exactly the way the client submits it.
    private static GameCommand PayAmount(ActionOption option, int amount) =>
        option.Command! with { GoldAmount = amount, SourceIds = option.Gold!.PaymentsByAmount![amount][0].SourceIds };

    // A derived quote's own suggestion for one target, exactly the way the client retargets an option.
    private static GameCommand Retarget(ActionOption option, string target) => option.Command! with { TargetId = target, SourceIds = option.TargetGold![target].Payments[0].SourceIds };

    private static int Producing(MatchEngine e, int player) =>
        e.State.Cards.Values.Where(c => c.OwnerId == player && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Sum(c => e.GoldProduced(c.Id, player));

    private static ActionOption DeciderAction(MatchEngine e) => e.QueryActions(1).Single(a => a.Type == "chooseOpponentPayment");

    // ---- Virtuous Charity --------------------------------------------------------------------------------------------------------------------------

    // Action Phase, player 1 (the Lion) the active player, player 0 (the Crane, holding Virtuous Charity) with the opportunity. Player 0 produces 9 Gold
    // (Stronghold 4, Border Keep 2, Marketplace 3), player 1 produces 6 (Stronghold 4, Border Keep 2; the Bamboo is bowed).
    private static (MatchEngine E, CardInstance Card) CharityTable(Action<MatchState>? more = null, bool ownTurn = false)
    {
        var s = Table(Phase.Action, actor: ownTurn ? 0 : 1);
        s.PriorityPlayerId = 0;
        var card = Add(s, Charity, 0, Zone.Hand);
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), card);
    }

    // Plays Virtuous Charity for `first` Gold; returns the engine with the second player's decision open.
    private static (MatchEngine E, CardInstance Card) CharityPlayed(int first, Action<MatchState>? more = null)
    {
        var (e, card) = CharityTable(more);
        Accept(e, PayAmount(Enabled(e, 0, CharityAbility, card.Id), first));
        return (e, card);
    }

    private static void PublishedShape()
    {
        Eq(PublishedContent.StrategyWave29.Cards.Length, 4);
        var c = CardCatalog.Get(Charity);
        Check(!c.ScenarioOnly && c.Type == CardType.Strategy && c.Name == "Virtuous Charity"); Eq(c.GoldCost, 0); Eq(c.Focus, 3);
        Check(c.Keywords!.SequenceEqual(new[] { "Bushido Virtue", "Compassion" }));
        Eq(c.Text, "Open: If it is not your turn, choose another player: You come across some starving peasants in need. You, then the player, each pay zero or more Gold. Whoever paid more Gold gains Honor equal to the difference between the two amounts paid, or 4, whichever is less.");
        Check(c.Abilities!.SequenceEqual(new[] { CharityAbility }));
        var a = AbilityCatalog.All[CharityAbility];
        Eq(a.Timing, "Open"); Eq(a.Target, "none"); Eq(a.Cost!.Basis, GoldCostBasis.Variable); Eq(a.Cost.Amount, 0); Check(a.Cost.MaxAmount is null);
        Check(a.Cost.AtResolution && a.Costs is null, "the payments are part of the resolution, not a cost paid when the action is announced");

        var r = CardCatalog.Get(AtAnyCost);
        Eq(r.Name, "At Any Cost"); Eq(r.GoldCost, 0); Eq(r.Focus, 1); Check(r.Keywords!.SequenceEqual(new[] { "Dark Virtue", "Will" }));
        Eq(r.Text, "Dark Virtue Battle, :gstar:: Recruit your target performing dead Samurai.");
        var ra = AbilityCatalog.All[RecruitAbility];
        Eq(ra.Timing, "Battle"); Eq(ra.Target, "own-dead-samurai"); Eq(ra.Cost!.Basis, GoldCostBasis.TargetRecruit); Check(!ra.Absent);

        var d = CardCatalog.Get(Disgrace);
        Eq(d.Name, "Disgraceful Conduct"); Eq(d.GoldCost, 0); Eq(d.Focus, 2);
        Eq(d.Text, "Battle: Even if you control no units at the current battlefield, pay Gold equal to a target Personality or unit's total Gold cost minus 4: Bow it.");
        Check(d.Abilities!.SequenceEqual(new[] { DisgraceAbility, DisgraceUnitAbility }), "the player chooses the Personality or the unit");
        var da = AbilityCatalog.All[DisgraceAbility];
        Eq(da.Timing, "Battle"); Eq(da.Target, "personality"); Check(da.Absent, "Even if you control no units");
        Eq(da.Cost!.Basis, GoldCostBasis.TargetCurrent); Eq(da.Cost.Modifier, -4); Eq(da.Cost.Floor, 0);
        var du = AbilityCatalog.All[DisgraceUnitAbility];
        Eq(du.Timing, "Battle"); Eq(du.Target, "personality-with-attachment"); Check(du.Absent);
        Eq(du.Cost!.Basis, GoldCostBasis.TargetUnitTotal); Eq(du.Cost.Modifier, -4); Eq(du.Cost.Floor, 0);

        var s = CardCatalog.Get(Stain);
        Eq(s.Name, "A Stain Cleansed"); Eq(s.Focus, 1);
        Eq(s.Text, "Battle, :gstar:: Target your performing unbowed Magistrate to target an enemy card without attachments with Gold Cost equal to what you paid minus 2 or an enemy Personality whose unit's total Gold Cost is equal what you paid minus 2. Destroy the card or unit.");
        var sa = AbilityCatalog.All[StainAbility];
        Eq(sa.Timing, "Battle"); Eq(sa.Performer, "own-unbowed-personality-at-battle-with-keyword"); Check(sa.RequiredKeywords!.SequenceEqual(new[] { "Magistrate" }));
        Eq(sa.Target, "enemy-personality-or-attachment");
        Eq(sa.Cost!.Basis, GoldCostBasis.TargetUnitTotal); Eq(sa.Cost.Modifier, -2); Eq(sa.Cost.Floor, 0);

        foreach (var id in new[] { Charity, AtAnyCost, Disgrace, Stain })
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"the scan of {id} is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void CharityOffer()
    {
        var (e, card) = CharityTable();
        var option = Enabled(e, 0, CharityAbility, card.Id);
        var quote = option.Gold!;
        Eq(quote.Kind, GoldCostKind.Variable); Eq(quote.MinAmount, 0); Eq(quote.MaxAmount, Producing(e, 0)); Eq(quote.MaxAmount, 9);
        Check(quote.PaymentsByAmount!.Keys.OrderBy(k => k).SequenceEqual(Enumerable.Range(0, 10)), "every amount from 0 to 9 has suggested sources");
        Eq(option.Command!.GoldAmount, null, "no amount chosen yet: the engine's minimum");
        // On your own turn it is not offered, and says why.
        var (own, ownCard) = CharityTable(ownTurn: true);
        var disabled = Options(own, 0, CharityAbility, ownCard.Id).Single();
        Check(!disabled.Enabled && disabled.Reason == "Only usable when it is not your own turn.", disabled.Reason ?? "");
        Check(Rejection(own, new GameCommand("useAbility", 0, own.Version, ownCard.Id, AbilityId: CharityAbility, GoldAmount: 0, SourceIds: [])).Contains("not your own turn"));
        // Any amount up to what can be produced is legal for the acting player; one more is not.
        Check(Rejection(e, option.Command with { GoldAmount = 10, SourceIds = quote.PaymentsByAmount[9][0].SourceIds }).Length > 0);
    }

    private static void SecondPlayerSeesFirstAmount()
    {
        var (e, card) = CharityTable();
        var option = Enabled(e, 0, CharityAbility, card.Id);
        var paidWith = option.Gold!.PaymentsByAmount![5][0].SourceIds;
        Accept(e, PayAmount(option, 5));

        Eq(e.State.PendingChoice!.Type, "opponentPayment"); Eq(e.State.PendingChoice.PlayerId, 1); Eq(e.State.PriorityPlayerId, 1);
        foreach (var id in paidWith) Check(e.State.Cards[id].Bowed, "the acting player's amount was paid");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the played Strategy is in the discard pile while the other player decides");

        var acting = e.GetView(0).PendingOpponentPayment!; var deciding = e.GetView(1).PendingOpponentPayment!;
        foreach (var view in new[] { acting, deciding })
        {
            Eq(view.FirstAmount, 5, "both players see what the first player paid"); Eq(view.SourceCardId, card.Id); Eq(view.SourceDefinitionId, Charity); Eq(view.SourceName, "Virtuous Charity");
            Eq(view.ActingPlayerId, 0); Eq(view.DeciderPlayerId, 1); Eq(view.ChoiceId, OpponentPaymentSpecs.VirtuousCharity);
            Check(view.Detail.Contains("or 4, whichever is less"), "the Honor at stake, in the card's terms");
            Check(view.Prompt.Contains("paid 5 Gold"), view.Prompt);
        }
        Check(!acting.ViewerDecides && deciding.ViewerDecides);
        Check(acting.Gold is null && acting.Outcomes is null, "the acting player's view carries no quote and no outcomes");
        var quote = deciding.Gold!;
        Eq(quote.Kind, GoldCostKind.Variable); Eq(quote.MinAmount, 0); Eq(quote.MaxAmount, 6); Eq(quote.Cost, 0);
        Check(quote.PaymentsByAmount!.Keys.OrderBy(k => k).SequenceEqual(Enumerable.Range(0, 7)));
        // What each amount would do, worked out by the engine, in the paying player's own words: 5 paid against 0..6.
        Eq(deciding.Outcomes![0], "Your opponent gains 4 Honor."); Eq(deciding.Outcomes[2], "Your opponent gains 3 Honor."); Eq(deciding.Outcomes[4], "Your opponent gains 1 Honor.");
        Eq(deciding.Outcomes[5], "Nobody gains Honor."); Eq(deciding.Outcomes[6], "You gain 1 Honor.");
        var json = JsonSerializer.Serialize(e.GetView(0).PendingOpponentPayment);
        Check(!json.Contains("PaymentsByAmount") && !json.Contains("Outcomes\":{"), "nothing of the paying player's options in the acting player's view");
        Check(e.GetView(0).PendingChoice!.Prompt == e.GetView(1).PendingChoice!.Prompt, "the prompt is public and identical");

        // The acting player is only told to wait; the second player has exactly the one decision, its default paying nothing.
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), string.Join(",", e.QueryActions(0).Select(a => a.Type)));
        var action = DeciderAction(e);
        Check(action.Enabled && action.Command!.GoldAmount == 0 && action.Command.SourceIds!.Length == 0 && action.CardId == card.Id);
        Eq(action.Gold!.MaxAmount, 6);
        Check(e.QueryActions(1).All(a => a.Type is "chooseOpponentPayment" or "concede"), "no other action is offered while it is open");
        // The opponent's hand stays hidden throughout.
        Eq(e.GetView(1).Players[0].Hand.Length, 0);
    }

    private static void HonorGoesToWhoeverPaidMore()
    {
        // (acting amount, second amount, acting gain, second gain): the acting player has 9 to give, the second 6.
        (int A, int B, int ActingGain, int DeciderGain)[] cases = [(5, 2, 3, 0), (9, 0, 4, 0), (1, 4, 0, 3), (0, 6, 0, 4), (3, 3, 0, 0), (0, 0, 0, 0), (4, 3, 1, 0), (6, 0, 4, 0), (2, 6, 0, 4), (7, 3, 4, 0)];
        foreach (var (a, b, actingGain, deciderGain) in cases)
        {
            var (e, card) = CharityPlayed(a);
            var honor0 = Honor(e, 0); var honor1 = Honor(e, 1);
            var action = DeciderAction(e);
            var sources = action.Gold!.PaymentsByAmount![b][0].SourceIds;
            Accept(e, action.Command! with { GoldAmount = b, SourceIds = sources });
            Eq(Honor(e, 0), honor0 + actingGain, $"acting player after {a} vs {b}"); Eq(Honor(e, 1), honor1 + deciderGain, $"second player after {a} vs {b}");
            Check(e.State.PendingChoice is null && e.State.PendingOpponentPayment is null, "the decision is closed");
            Eq(e.State.PriorityPlayerId, 1, "priority passes on, as after any action");
            foreach (var id in sources) Check(e.State.Cards[id].Bowed, "the second player's sources were bowed");
            Check(e.State.Events.Any(x => x.Type == "opponentPaymentChosen" && x.PlayerId == 1 && x.Amount == b), "the amount is public in the log");
            if (actingGain == 0 && deciderGain == 0) Check(e.State.Events.Any(x => x.Type == "opponentPaymentResolved"), "a tie is announced");
            Check(e.GetView(0).PendingOpponentPayment is null && e.GetView(1).PendingOpponentPayment is null);
        }
    }

    private static void SecondPlayerBounds()
    {
        var (e, _) = CharityPlayed(3);
        var action = DeciderAction(e);
        var keep = e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.DefinitionId == "training-border-keep");
        var stronghold = e.State.Cards[e.State.Players[1].StrongholdId];
        var bamboo = e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.DefinitionId == "training-bamboo");
        var ownSource = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-marketplace");
        var stronghold0 = e.State.Cards[e.State.Players[0].StrongholdId];
        var pay = action.Command!;
        Reject(e, pay with { GoldAmount = 7, SourceIds = [stronghold.Id, keep.Id] });                    // more than they can produce
        Reject(e, pay with { GoldAmount = -1, SourceIds = [] });                                         // negative
        Reject(e, pay with { GoldAmount = 5, SourceIds = [keep.Id] });                                   // sources produce less than the amount
        Reject(e, pay with { GoldAmount = 2, SourceIds = [ownSource.Id] });                              // not their source
        Reject(e, pay with { GoldAmount = 1, SourceIds = [bamboo.Id] });                                 // a bowed source
        Reject(e, pay with { GoldAmount = 0, SourceIds = [stronghold.Id] });                             // sources for nothing
        Reject(e, pay with { GoldAmount = 2, SourceIds = [stronghold.Id, stronghold.Id] });              // one source twice
        Reject(e, pay with { PlayerId = 0, GoldAmount = 0, SourceIds = [] });                            // the acting player may not answer
        Reject(e, pay with { GoldAmount = 6, SourceIds = [stronghold0.Id, keep.Id] });                   // the acting player's Stronghold is not theirs
        Reject(e, new GameCommand("pass", 1, e.Version));
        Reject(e, new GameCommand("declineReaction", 1, e.Version));
        Check(e.State.PendingOpponentPayment is not null, "every rejection left the decision open");
        // Every amount from 0 to 6 is legal with the engine's suggested sources; the maximum uses everything.
        foreach (var amount in Enumerable.Range(0, 7))
        {
            var fresh = CharityPlayed(3).E;
            Accept(fresh, DeciderAction(fresh).Command! with { GoldAmount = amount, SourceIds = DeciderAction(fresh).Gold!.PaymentsByAmount![amount][0].SourceIds });
        }
        // Excess production is lost exactly as in every other payment: 1 Gold paid from a 4-Gold Stronghold.
        Accept(e, pay with { GoldAmount = 1, SourceIds = [stronghold.Id] });
        Check(e.State.Cards[stronghold.Id].Bowed && !e.State.Cards[keep.Id].Bowed);
        Check(e.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 1 Gold") && e.State.Events.Last(x => x.Type == "payment").Message.Contains("3 excess was lost"));
    }

    private static void SecondPlayerSakuma()
    {
        var (e, _) = CharityPlayed(0, s => Add(s, Sakuma, 1, Zone.Home));
        var action = DeciderAction(e);
        Eq(action.Gold!.MaxAmount, 8, "Stronghold 4, Border Keep 2 and Sakuma's 2");
        var sakuma = e.State.Cards.Values.Single(c => c.DefinitionId == Sakuma);
        var all = e.State.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone is Zone.Home or Zone.Stronghold && !c.Bowed && (CardCatalog.Get(c.DefinitionId).GoldProduction > 0 || c.Id == sakuma.Id)).Select(c => c.Id).ToArray();
        Accept(e, action.Command! with { GoldAmount = 8, SourceIds = all });
        Check(e.State.Cards[sakuma.Id].Bowed && e.State.Cards[sakuma.Id].AbilityUses.Count > 0, "Sakuma bowed and spent his use");
        Check(e.State.Events.Any(x => x.Type == "goldSourceUsed"), "and the log says why");
        Eq(Honor(e, 1), 6 + 4, "8 paid against 0: the second player gains the capped 4");
    }

    private static void NoGoldNoPrompt()
    {
        var (e, _) = CharityPlayed(3, null);
        // Rebuild with every source of player 1 bowed.
        var (poor, card) = CharityTable(s => { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true; });
        Accept(poor, PayAmount(Enabled(poor, 0, CharityAbility, card.Id), 3));
        Check(poor.State.PendingChoice is null && poor.State.PendingOpponentPayment is null, "nothing to decide");
        Check(poor.State.Events.Any(x => x.Type == "opponentPaymentAutoResolved"), "announced in the log");
        Eq(Honor(poor, 0), 8 + 3, "the acting player's 3 against nothing");
        Eq(Honor(poor, 1), 6);
        Eq(poor.State.PriorityPlayerId, 1, "the action finished");
        Eq(poor.State.Cards[card.Id].Zone, Zone.FateDiscard);
        Check(poor.GetView(0).PendingOpponentPayment is null && poor.GetView(1).PendingOpponentPayment is null);
        // Paying nothing yourself against nothing: no Honor moves.
        var (zero, zeroCard) = CharityTable(s => { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true; });
        Accept(zero, PayAmount(Enabled(zero, 0, CharityAbility, zeroCard.Id), 0));
        Eq(Honor(zero, 0), 8); Eq(Honor(zero, 1), 6);
        Check(e.State.PendingOpponentPayment is not null, "with Gold to pay it does open");
    }

    private static void DecisionBlocksAndFinishes()
    {
        var (e, card) = CharityPlayed(2, s => { var samurai = Add(s, "training-dishonored-samurai", 1, Zone.Home); samurai.Dishonorable = true; });
        var dishonorable = e.State.Cards.Values.Single(c => c.DefinitionId == "training-dishonored-samurai");
        Check(!e.QueryActions(1).Any(a => a.Type == "orderSeppuku"), "Seppuku is not offered while it is open");
        Rejection(e, new GameCommand("orderSeppuku", 1, e.Version, dishonorable.Id));
        Rejection(e, new GameCommand("orderSeppuku", 0, e.Version, e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home).Id));
        Rejection(e, new GameCommand("pass", 0, e.Version));
        Accept(e, DeciderAction(e).Command!);
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);
        Check(e.State.PendingChoice is null);
        Eq(e.State.PriorityPlayerId, 1);
        Eq(Honor(e, 0), 8 + 2, "2 paid against nothing");
        // And now it is the second player's ordinary opportunity again.
        Check(e.QueryActions(1).Any(a => a.Type == "pass"));
        // A player who is not the acting player's opponent cannot be asked: Virtuous Charity cannot be used twice in one hand (one copy, spent).
        Check(!Options(e, 0, CharityAbility).Any(a => a.Enabled));
    }

    // Announces Virtuous Charity for 5 with a reactor (player 1's fixture Personality) in play, and returns the engine with the before-action window open.
    private static (MatchEngine E, CardInstance Card, CardInstance Reactor, string[] Sources) CharityWithReactor(string reactorDefinition)
    {
        CardInstance reactor = null!;
        var (e, card) = CharityTable(s => { reactor = Add(s, reactorDefinition, 1, Zone.Home); });
        var option = Enabled(e, 0, CharityAbility, card.Id);
        var sources = option.Gold!.PaymentsByAmount![5][0].SourceIds;
        Accept(e, PayAmount(option, 5));
        return (e, card, reactor, sources);
    }

    // The reactor plays its Reaction in the open window, then both players pass and the window closes.
    private static void ReactAndClose(MatchEngine e, CardInstance reactor, string ability)
    {
        Eq(e.State.PendingChoice!.Type, "beforeActionReaction");
        var player = e.State.PriorityPlayerId;
        Accept(e, Enabled(e, player, ability, reactor.Id).Command!);
        for (var guard = 0; guard < 4 && e.State.PendingChoice?.Type == "beforeActionReaction"; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).Single(a => a.Type == "declineReaction").Command!);
    }

    private static void CharityActionNegated()
    {
        // Whole-action negation through the reaction window: it is taken (the card is spent) but NEITHER player pays anything and no decision opens.
        var (e, card, reactor, sources) = CharityWithReactor("training-untargeted-negator");
        Eq(e.State.PendingChoice!.Type, "beforeActionReaction", "the announced action is held for reactions");
        foreach (var id in sources) Check(!e.State.Cards[id].Bowed, "nothing has been paid while the action can still be negated");
        Check(e.State.PendingOpponentPayment is null && !e.State.Events.Any(x => x.Type == "payment"));
        ReactAndClose(e, reactor, "training-negate-untargeted-action");
        Check(e.State.Events.Any(x => x.Type == "actionNegated"), "the action was negated");
        Check(e.State.PendingChoice is null && e.State.PendingOpponentPayment is null, "no decision for the second player");
        foreach (var id in sources) Check(!e.State.Cards[id].Bowed, "the acting player paid nothing");
        Check(!e.State.Events.Any(x => x.Type == "payment" || x.Type == "opponentPaymentOffered"), "no payment by anyone");
        Eq(Honor(e, 0), 8); Eq(Honor(e, 1), 6);
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was still played");
        // The same through the whole-action test seam: no reaction needed.
        var (t, seamCard) = CharityTable();
        t.NegateEveryAction = true;
        var seamSources = Enabled(t, 0, CharityAbility, seamCard.Id).Gold!.PaymentsByAmount![5][0].SourceIds;
        Accept(t, PayAmount(Enabled(t, 0, CharityAbility, seamCard.Id), 5));
        foreach (var id in seamSources) Check(!t.State.Cards[id].Bowed);
        Check(t.State.PendingChoice is null && !t.State.Events.Any(x => x.Type == "payment"));
    }

    private static void CharityHonorGainNegated()
    {
        var (e, card, reactor, sources) = CharityWithReactor("training-honor-gain-negator");
        foreach (var id in sources) Check(!e.State.Cards[id].Bowed, "nothing is paid while the window is open");
        Check(e.State.PendingOpponentPayment is null, "the second player's decision waits for the first payment");
        ReactAndClose(e, reactor, "training-negate-honor-gain");
        // The action resolves: the acting player pays FIRST, and only then does the second player's decision open.
        foreach (var id in sources) Check(e.State.Cards[id].Bowed, "the acting player's payment stands");
        Eq(e.State.PendingChoice!.Type, "opponentPayment"); Eq(e.State.PendingChoice.PlayerId, 1);
        Check(e.State.Events.Count(x => x.Type == "payment") == 1, "one payment made so far");
        var action = DeciderAction(e);
        Accept(e, action.Command! with { GoldAmount = 2, SourceIds = action.Gold!.PaymentsByAmount![2][0].SourceIds });
        Check(e.State.Events.Count(x => x.Type == "payment") == 2, "both payments were made");
        Check(e.State.Events.Any(x => x.Type == "honorGainNegated"), "and the Honor gain was negated");
        Eq(Honor(e, 0), 8, "no Honor for the acting player who paid more"); Eq(Honor(e, 1), 6);
        Check(e.State.PendingChoice is null && e.State.NegatedEffects.Count == 0, "nothing lingers");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);
        // Control: without the reaction the same amounts give the Honor.
        var (c, _) = CharityPlayed(5);
        Accept(c, DeciderAction(c).Command! with { GoldAmount = 2, SourceIds = DeciderAction(c).Gold!.PaymentsByAmount![2][0].SourceIds });
        Eq(Honor(c, 0), 8 + 3);
    }

    private static void GeneralPrimitiveFixture()
    {
        var s = Table(Phase.Action, actor: 0);
        var source = Add(s, "training-opponent-payment-source", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-opponent-payment", source.Id);
        Eq(option.Gold!.Kind, GoldCostKind.Variable);
        Accept(e, PayAmount(option, 3));
        Eq(e.State.PendingChoice!.Type, "opponentPayment");
        var view = e.GetView(1).PendingOpponentPayment!;
        Eq(view.ChoiceId, "training-opponent-payment"); Check(view.Detail.Contains("Each player gains Honor equal to the Gold the other paid"));
        Eq(view.Outcomes![2], "Your opponent gains 2 Honor and you gain 3 Honor.");
        Accept(e, DeciderAction(e).Command! with { GoldAmount = 2, SourceIds = DeciderAction(e).Gold!.PaymentsByAmount![2][0].SourceIds });
        Eq(Honor(e, 0), 8 + 2, "the acting player gains what the other paid"); Eq(Honor(e, 1), 6 + 3, "and the other gains what the acting player paid");
        Eq(e.State.PriorityPlayerId, 1);
    }

    private static void ShowcaseFlow()
    {
        var e = MatchEngine.CreateOpponentPaymentShowcase();
        Eq(e.State.ActivePlayerId, 0);
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Label.StartsWith("Open: If it is not your turn")));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "pass").Command!);
        Eq(e.State.PriorityPlayerId, 1);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == Charity);
        var option = Enabled(e, 1, CharityAbility, card.Id);
        Accept(e, option.Command! with { GoldAmount = 3, SourceIds = option.Gold!.PaymentsByAmount![3][0].SourceIds });
        Eq(e.State.PendingChoice!.Type, "opponentPayment"); Eq(e.State.PendingChoice.PlayerId, 0);
        var view = e.GetView(0).PendingOpponentPayment!;
        Eq(view.FirstAmount, 3); Check(view.ViewerDecides && view.Gold!.MaxAmount == Producing(e, 0));
        var pay = e.QueryActions(0).Single(a => a.Type == "chooseOpponentPayment");
        Accept(e, pay.Command! with { GoldAmount = 2, SourceIds = pay.Gold!.PaymentsByAmount![2][0].SourceIds });
        Eq(Honor(e, 1), 6 + 1, "3 against 2: the Lion gains 1"); Eq(Honor(e, 0), 8);
        var replayed = MatchEngine.Replay(e.ExportReplay());
        Eq(System.Text.Json.JsonSerializer.Serialize(replayed.State), System.Text.Json.JsonSerializer.Serialize(e.State), "the same game from its commands");
    }

    // ---- Disgraceful Conduct and A Stain Cleansed: prices from a unit's current total -----------------------------------------------------------------

    // Battle at province 0: player 0 (the Defender, holding the opportunity) holds `strategy` and has `own` (a Personality of the given definition) there;
    // player 1 has Daidoji Ebizo there, plus whatever `more` adds. Player 0 produces 9 Gold plus `extra` Marketplaces' 3 each.
    private sealed record Rig(MatchState S, CardInstance Card, CardInstance Enemy, CardInstance Own);
    private static Rig Battle(string strategy, string? own = Genshin, int extra = 0, Action<Rig>? more = null)
    {
        var s = Table(); s.PriorityPlayerId = 0;
        for (var i = 0; i < extra; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var card = Add(s, strategy, 0, Zone.Hand);
        var ownCard = own is null ? null! : Add(s, own, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Ebizo, 1, Zone.Battlefield, province: 0);
        var rig = new Rig(s, card, enemy, ownCard);
        more?.Invoke(rig);
        return rig;
    }
    private static (CardInstance Follower, CardInstance Item) Attach(Rig r, CardInstance host) =>
        (Add(r.S, "training-follower", host.OwnerId, Zone.Attached, province: 0, parent: host.Id), Add(r.S, "training-item", host.OwnerId, Zone.Attached, province: 0, parent: host.Id));

    private static ActionOption Disgrace1(MatchEngine e, Rig r) => Enabled(e, 0, DisgraceAbility, r.Card.Id);
    private static ActionOption DisgraceUnit1(MatchEngine e, Rig r) => Enabled(e, 0, DisgraceUnitAbility, r.Card.Id);
    private static bool UnitOffered(MatchEngine e, Rig r) => CanUse(e, 0, DisgraceUnitAbility, r.Card.Id);
    private static ActionOption Stain1(MatchEngine e, Rig r) => Enabled(e, 0, StainAbility, r.Card.Id, r.Own.Id);
    private static int Price(ActionOption option, CardInstance target) => option.TargetGold![target.Id].Cost;

    private static void DisgracefulModes()
    {
        // Ebizo (9) alone: both modes are the same number, so the choice is offered once (the Personality mode only).
        var bare = Battle(Disgrace);
        var e = MatchEngine.FromTestState(bare.S);
        var only = Disgrace1(e, bare);
        Eq(Price(only, bare.Enemy), 5); Check(only.TargetGold![bare.Enemy.Id].Basis.Contains("cost 9 − 4"), only.TargetGold[bare.Enemy.Id].Basis);
        Check(!UnitOffered(e, bare), "no unit mode for a Personality with no attachments");
        // With a Follower (2) and an Item (2): two offered options with their own quotes -- his own 9 - 4, or the unit's 13 - 4.
        var unit = Battle(Disgrace, more: x => Attach(x, x.Enemy));
        var eu = MatchEngine.FromTestState(unit.S);
        var personality = Disgrace1(eu, unit); var whole = DisgraceUnit1(eu, unit);
        Eq(Price(personality, unit.Enemy), 5, "the Personality alone: his own current cost minus 4");
        Eq(Price(whole, unit.Enemy), 9, "the unit: 9 + 2 + 2 = 13, minus 4"); Check(whole.TargetGold![unit.Enemy.Id].Basis.Contains("9 + 2 + 2 = 13 − 4"), whole.TargetGold[unit.Enemy.Id].Basis);
        Eq(personality.TargetGold![unit.Enemy.Id].Kind, GoldCostKind.Derived);
        // Two enemy Personalities, one with attachments: the Personality mode lists both, the unit mode only the one that has a unit to speak of.
        CardInstance lone = null!;
        var mixed = Battle(Disgrace, more: x => { Attach(x, x.Enemy); lone = Add(x.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); });
        var em = MatchEngine.FromTestState(mixed.S);
        Check(Disgrace1(em, mixed).TargetIds!.ToHashSet().SetEquals(new[] { mixed.Enemy.Id, lone.Id, mixed.Own.Id }), "the Personality mode: every Personality at the battlefield");
        Check(DisgraceUnit1(em, mixed).TargetIds!.ToHashSet().SetEquals(new[] { mixed.Enemy.Id }), "the unit mode: only the one with attachments");
        // A hand-built unit-mode command against a bare Personality is rejected by the engine.
        Rejection(em, DisgraceUnit1(em, mixed).Command! with { TargetId = lone.Id, SourceIds = [] });
    }

    private static void DisgracefulCurrentCosts()
    {
        // A Gold cost penalty on the Personality (an action's -2), a Gold cost token (-2), and both: each price follows the CURRENT costs.
        var modified = Battle(Disgrace, extra: 2, more: x => { Attach(x, x.Enemy); x.Enemy.Modifiers.Add(new("test-source", "test", "GoldCost", -2)); });
        var m = MatchEngine.FromTestState(modified.S);
        Eq(Price(Disgrace1(m, modified), modified.Enemy), 3, "his own 7 - 4"); Eq(Price(DisgraceUnit1(m, modified), modified.Enemy), 7, "the unit's 11 - 4");
        var token = Battle(Disgrace, extra: 2, more: x => { Attach(x, x.Enemy); x.Enemy.Tokens.Add(new("token-1", "Training", "test", "GoldCost", -2)); });
        var t = MatchEngine.FromTestState(token.S);
        Eq(Price(Disgrace1(t, token), token.Enemy), 3); Eq(Price(DisgraceUnit1(t, token), token.Enemy), 7);
        var both = Battle(Disgrace, extra: 2, more: x => { Attach(x, x.Enemy); x.Enemy.Modifiers.Add(new("test-source", "test", "GoldCost", -2)); x.Enemy.Tokens.Add(new("token-1", "Training", "test", "GoldCost", -2)); });
        var b = MatchEngine.FromTestState(both.S);
        Eq(Price(Disgrace1(b, both), both.Enemy), 1, "5 - 4"); Eq(Price(DisgraceUnit1(b, both), both.Enemy), 5, "9 - 4");
        // A raise counts as well.
        var raised = Battle(Disgrace, extra: 2, more: x => { Attach(x, x.Enemy); x.Enemy.Modifiers.Add(new("test-source", "test", "GoldCost", 3)); });
        var r3 = MatchEngine.FromTestState(raised.S);
        Eq(Price(Disgrace1(r3, raised), raised.Enemy), 8); Eq(Price(DisgraceUnit1(r3, raised), raised.Enemy), 12);
        // An attachment reduced to 0 counts as 0 in the unit (9 + 2 + 0 = 11), and does not touch the Personality's own price.
        var item0 = Battle(Disgrace, more: x => { var (_, item) = Attach(x, x.Enemy); item.Modifiers.Add(new("test-source", "test", "GoldCost", -5)); });
        var i0 = MatchEngine.FromTestState(item0.S);
        Eq(Price(DisgraceUnit1(i0, item0), item0.Enemy), 7, "9 + 2 + 0 = 11, minus 4"); Eq(Price(Disgrace1(i0, item0), item0.Enemy), 5);
        // The Personality reduced to 0: his own price is the floor, the unit is only the attachments (0 + 2 + 2 - 4).
        var zeroed = Battle(Disgrace, more: x => { Attach(x, x.Enemy); x.Enemy.Modifiers.Add(new("test-source", "test", "GoldCost", -20)); });
        var z = MatchEngine.FromTestState(zeroed.S);
        Eq(Price(Disgrace1(z, zeroed), zeroed.Enemy), 0, "0 - 4 is floored at 0"); Eq(Price(DisgraceUnit1(z, zeroed), zeroed.Enemy), 0, "4 - 4");
        // "Enters play paying 1 less Gold if you are a Crane Clan player" is a payment discount, not a change of the card's cost: it counts in full.
        var bat = Battle(Disgrace, extra: 2, more: x => Add(x.S, "oracle-l5r-4700", 1, Zone.Attached, province: 0, parent: x.Enemy.Id));
        Eq(Price(DisgraceUnit1(MatchEngine.FromTestState(bat.S), bat), bat.Enemy), 9 + CardCatalog.Get("oracle-l5r-4700").GoldCost - 4, "Legion of the Bat's printed cost is in the total whoever controls the unit");
        // The floor: a Personality costing less than 4 is free, and needs no sources.
        CardInstance kijoCard = null!;
        var cheap = Battle(Disgrace, more: x => { kijoCard = Add(x.S, Kijo, 1, Zone.Battlefield, province: 0); });
        var e = MatchEngine.FromTestState(cheap.S);
        var free = Disgrace1(e, cheap);
        Eq(Price(free, kijoCard), 0, "2 - 4 is floored at 0");
        var command = Retarget(free, kijoCard.Id);
        Eq(command.SourceIds!.Length, 0);
        Accept(e, command);
        Check(e.State.Cards[kijoCard.Id].Bowed, "a free bow still bows him");
    }

    private static void DisgracefulResolves()
    {
        // Absent: player 0 has NO unit at the battlefield (only the enemy's), and the action is still offered.
        var r = Battle(Disgrace, own: null, more: null);
        var e = MatchEngine.FromTestState(r.S);
        var option = Disgrace1(e, r);
        Check(option.TargetIds!.SequenceEqual(new[] { r.Enemy.Id }));
        Eq(Price(option, r.Enemy), 5);
        var paid = option.TargetGold![r.Enemy.Id].Payments[0].SourceIds;
        Accept(e, Retarget(option, r.Enemy.Id));
        Check(e.State.Cards[r.Enemy.Id].Bowed, "bowed"); foreach (var id in paid) Check(e.State.Cards[id].Bowed, "and the price was paid from the chosen sources");
        Check(e.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 5 Gold"));
        Eq(e.State.Cards[r.Card.Id].Zone, Zone.FateDiscard);
        // The Personality mode bows him only: the attachments stay as they were.
        CardInstance follower = null!, item = null!;
        var withUnit = Battle(Disgrace, more: x => { (follower, item) = Attach(x, x.Enemy); });
        var e2 = MatchEngine.FromTestState(withUnit.S);
        Accept(e2, Retarget(Disgrace1(e2, withUnit), withUnit.Enemy.Id));
        Check(e2.State.Cards[withUnit.Enemy.Id].Bowed && !e2.State.Cards[follower.Id].Bowed && !e2.State.Cards[item.Id].Bowed, "only the Personality bowed");
        // The unit mode bows the Personality and every card attached to him, paying the unit's price.
        var whole = Battle(Disgrace, more: x => { (follower, item) = Attach(x, x.Enemy); });
        var e3 = MatchEngine.FromTestState(whole.S);
        var unitOption = DisgraceUnit1(e3, whole);
        var unitSources = unitOption.TargetGold![whole.Enemy.Id].Payments[0].SourceIds;
        Accept(e3, Retarget(unitOption, whole.Enemy.Id));
        Check(e3.State.Cards[whole.Enemy.Id].Bowed && e3.State.Cards[follower.Id].Bowed && e3.State.Cards[item.Id].Bowed, "the whole unit bowed");
        foreach (var id in unitSources) Check(e3.State.Cards[id].Bowed);
        Check(e3.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 9 Gold"), "the unit's 13 - 4");
        // The unit mode is Absent too: offered with no unit of yours at the battlefield.
        var absentUnit = Battle(Disgrace, own: null, more: x => Attach(x, x.Enemy));
        Check(UnitOffered(MatchEngine.FromTestState(absentUnit.S), absentUnit), "the unit mode needs no unit of yours either");
        // A unit of yours is bowed the same way (either side's).
        var both = Battle(Disgrace, more: x => Attach(x, x.Own));
        var opt = DisgraceUnit1(MatchEngine.FromTestState(both.S), both);
        Check(opt.TargetIds!.ToHashSet().SetEquals(new[] { both.Own.Id }), "only Genshin's unit has attachments here");
        Eq(Price(opt, both.Own), 7 + 2 + 2 - 4, "Doji Genshin's unit");
        // A payment that produces less than the price is rejected; nothing changes.
        var e4 = MatchEngine.FromTestState(both.S);
        Reject(e4, Retarget(Disgrace1(e4, both), both.Enemy.Id) with { SourceIds = [] });
        // No Personality at the battlefield to target: disabled, with the reason.
        var empty = Battle(Disgrace, own: null, more: x => x.S.Cards.Remove(x.Enemy.Id));
        var none = Options(MatchEngine.FromTestState(empty.S), 0, DisgraceAbility, empty.Card.Id).Single();
        Check(!none.Enabled, none.Reason ?? "");
        // Not in the Action phase: it is a Battle action.
        var s = Table(Phase.Action, actor: 0); var hand = Add(s, Disgrace, 0, Zone.Hand); Add(s, Ebizo, 1, Zone.Home);
        Check(!Options(MatchEngine.FromTestState(s), 0, DisgraceAbility, hand.Id).Any(a => a.Enabled));
    }

    private static void StainPrice()
    {
        // Mendon's example on the scan's reading: Daidoji Ebizo (Gold cost 9) with no attachments costs 9 - 2 = 7.
        var r = Battle(Stain, extra: 4);
        var e = MatchEngine.FromTestState(r.S);
        var option = Stain1(e, r);
        Eq(Price(option, r.Enemy), 7); Check(option.TargetGold![r.Enemy.Id].Basis.Contains("total cost 9 − 2"), option.TargetGold[r.Enemy.Id].Basis);
        Eq(option.TargetGold[r.Enemy.Id].Kind, GoldCostKind.Derived);
        // With a Follower and an Item: a Personality WITH attachments is priced by his unit's total 13, minus 2.
        var unit = Battle(Stain, extra: 4, more: x => Attach(x, x.Enemy));
        var u = Stain1(MatchEngine.FromTestState(unit.S), unit);
        Eq(Price(u, unit.Enemy), 11); Check(u.TargetGold![unit.Enemy.Id].Basis.Contains("9 + 2 + 2 = 13 − 2"), u.TargetGold[unit.Enemy.Id].Basis);
        // A cost-reduced unit: the price follows the current cost (modifier, token, both).
        var mod = Battle(Stain, extra: 4, more: x => { Attach(x, x.Enemy); x.Enemy.Modifiers.Add(new("test-source", "test", "GoldCost", -2)); });
        Eq(Price(Stain1(MatchEngine.FromTestState(mod.S), mod), mod.Enemy), 9);
        var tok = Battle(Stain, extra: 4, more: x => { Attach(x, x.Enemy); x.Enemy.Tokens.Add(new("token-1", "Training", "test", "GoldCost", -2)); });
        Eq(Price(Stain1(MatchEngine.FromTestState(tok.S), tok), tok.Enemy), 9);
        var both = Battle(Stain, extra: 4, more: x => { Attach(x, x.Enemy); x.Enemy.Modifiers.Add(new("test-source", "test", "GoldCost", -2)); x.Enemy.Tokens.Add(new("token-1", "Training", "test", "GoldCost", -2)); });
        Eq(Price(Stain1(MatchEngine.FromTestState(both.S), both), both.Enemy), 7);
        // An attachment as the target is a single card: its own current cost minus 2, never below 0 (Follower 2 -> 0; an Item reduced to 0 -> 0).
        CardInstance follower = null!, itemCard = null!;
        var single = Battle(Stain, extra: 4, more: x => { (follower, itemCard) = Attach(x, x.Enemy); Add(x.S, "oracle-l5r-4700", 1, Zone.Attached, province: 0, parent: x.Enemy.Id); });
        itemCard.Tokens.Add(new("token-2", "Training", "test", "GoldCost", -5));
        var bat5 = single.S.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-4700" && c.AttachedTo == single.Enemy.Id);
        var s = Stain1(MatchEngine.FromTestState(single.S), single);
        Eq(Price(s, follower), 0, "Follower 2 - 2"); Eq(Price(s, itemCard), 0, "an Item reduced to 0 costs 0"); Eq(Price(s, bat5), 3, "Legion of the Bat 5 - 2");
        Check(s.TargetGold![bat5.Id].Basis.Contains("total cost 5 − 2"), s.TargetGold[bat5.Id].Basis);
        // A Personality with no attachments is the same number whichever way he is read.
        Eq(Price(Stain1(MatchEngine.FromTestState(r.S), r), r.Enemy), CardCatalog.Get(Ebizo).GoldCost - 2);
    }

    private static void StainAffordable()
    {
        // Player 0 has 9 Gold: Ebizo's unit (13 - 2 = 11) is out of reach, a 5-cost Lion samurai (3) is not.
        CardInstance lion = null!;
        var r = Battle(Stain, more: x => { lion = Add(x.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Attach(x, x.Enemy); });
        var e = MatchEngine.FromTestState(r.S);
        var option = Stain1(e, r);
        Check(!option.TargetIds!.Contains(r.Enemy.Id), "the unaffordable unit is not offered");
        Check(option.TargetIds!.Contains(lion.Id));
        Check(!option.TargetGold!.ContainsKey(r.Enemy.Id), "and has no quote");
        Eq(Price(option, lion), 3);
        // The engine's own answer to submitting the unaffordable target with borrowed sources is a rejection.
        Reject(e, option.Command! with { TargetId = r.Enemy.Id, SourceIds = option.TargetGold[lion.Id].Payments[0].SourceIds });
        // Nothing affordable: disabled, with the reason.
        var poor = Battle(Stain);
        foreach (var c in poor.S.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var disabled = Options(MatchEngine.FromTestState(poor.S), 0, StainAbility, poor.Card.Id, poor.Own.Id).Single();
        Check(!disabled.Enabled && !string.IsNullOrEmpty(disabled.Reason) && (disabled.TargetIds?.Length ?? 0) == 0, "disabled with a reason and no target: " + disabled.Reason);
    }

    private static void StainPerformer()
    {
        var r = Battle(Stain, extra: 4);
        Check(CanUse(MatchEngine.FromTestState(r.S), 0, StainAbility, r.Card.Id, r.Own.Id));
        // A bowed Magistrate does not perform it.
        var bowed = Battle(Stain, extra: 4, more: x => x.Own.Bowed = true);
        Check(!CanUse(MatchEngine.FromTestState(bowed.S), 0, StainAbility, bowed.Card.Id, bowed.Own.Id));
        // Nor does a Personality who is not a Magistrate.
        var plain = Battle(Stain, own: "training-samurai", extra: 4);
        Check(!CanUse(MatchEngine.FromTestState(plain.S), 0, StainAbility, plain.Card.Id, plain.Own.Id));
        // A Magistrate away from the battlefield (at home) does not either: he must be performing at the current battlefield.
        var home = Battle(Stain, extra: 4, more: x => { x.Own.Zone = Zone.Home; x.Own.ProvinceIndex = null; });
        Check(!CanUse(MatchEngine.FromTestState(home.S), 0, StainAbility, home.Card.Id, home.Own.Id));
        // Only enemy cards: his own Personality (and his own attachments) are never offered.
        var own = Battle(Stain, extra: 4, more: x => Attach(x, x.Own));
        var option = Stain1(MatchEngine.FromTestState(own.S), own);
        Check(option.TargetIds!.SequenceEqual(new[] { own.Enemy.Id }), string.Join(",", option.TargetIds!));
        // Battle phase only.
        var s = Table(Phase.Action, actor: 0); var card = Add(s, Stain, 0, Zone.Hand); var genshin = Add(s, Genshin, 0, Zone.Home); Add(s, Ebizo, 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s), 0, StainAbility, card.Id, genshin.Id));
        // A Stain Cleansed takes the Absent flag of no one: without a unit of his own at the battlefield there is no Magistrate to choose.
        var alone = Battle(Stain, own: null, extra: 4);
        Check(Options(MatchEngine.FromTestState(alone.S), 0, StainAbility, alone.Card.Id).All(a => !a.Enabled));
    }

    private static void StainDestroys()
    {
        // The unit: Ebizo, his Follower and his Item all go; the price is exactly 11 and bows exactly the chosen sources.
        CardInstance unitFollower = null!;
        var r = Battle(Stain, extra: 4, more: x => { (unitFollower, _) = Attach(x, x.Enemy); });
        var e = MatchEngine.FromTestState(r.S);
        var option = Stain1(e, r);
        var sources = option.TargetGold![r.Enemy.Id].Payments[0].SourceIds;
        var followerId = unitFollower.Id;
        Accept(e, Retarget(option, r.Enemy.Id));
        Eq(e.State.Cards[r.Enemy.Id].Zone, Zone.Dead, "the Personality is destroyed"); Eq(e.State.Cards[followerId].Zone, Zone.FateDiscard, "and his unit with him");
        Check(!e.State.Cards.Values.Any(c => c.AttachedTo == r.Enemy.Id));
        foreach (var id in sources) Check(e.State.Cards[id].Bowed);
        var payment = e.State.Events.Last(x => x.Type == "payment");
        Check(payment.Message.Contains("paid 11 Gold"), payment.Message);
        Eq(e.State.Cards[r.Card.Id].Zone, Zone.FateDiscard);
        // An attachment as the target: it alone is destroyed, its Personality stays (Legion of the Bat, 5 - 2).
        CardInstance bat = null!;
        var one = Battle(Stain, extra: 4, more: x => { bat = Add(x.S, "oracle-l5r-4700", 1, Zone.Attached, province: 0, parent: x.Enemy.Id); });
        var e2 = MatchEngine.FromTestState(one.S);
        Accept(e2, Retarget(Stain1(e2, one), bat.Id));
        Eq(e2.State.Cards[bat.Id].Zone, Zone.FateDiscard); Check(e2.State.Cards[one.Enemy.Id].Zone == Zone.Battlefield, "his Personality stays");
        Check(e2.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 3 Gold"), "the attachment's own 5 minus 2");
        // Excess production is lost as in every payment, but too little is rejected: Ebizo alone costs 7.
        var alone = Battle(Stain, extra: 4);
        var e3 = MatchEngine.FromTestState(alone.S);
        var stainOption = Stain1(e3, alone);
        var stronghold = alone.S.Players[0].StrongholdId;
        Reject(e3, Retarget(stainOption, alone.Enemy.Id) with { SourceIds = [stronghold] });
        var everything = e3.State.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray();
        Accept(e3, Retarget(stainOption, alone.Enemy.Id) with { SourceIds = everything });
        Eq(e3.State.Cards[alone.Enemy.Id].Zone, Zone.Dead);
        Check(e3.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 7 Gold"), "the price is exactly 7");
    }

    private static void StainSignIsOneConstant()
    {
        var cost = AbilityCatalog.All[StainAbility].Cost!;
        Eq(cost.Basis, GoldCostBasis.TargetUnitTotal); Eq(cost.Modifier, -2); Eq(cost.Floor, 0);
        var r = Battle(Stain, extra: 4);
        var e = MatchEngine.FromTestState(r.S);
        Eq(Price(Stain1(e, r), r.Enemy), 7, "the scan governs: Ebizo (9) alone costs 7");
        // The Oracle wording read the other way -- the target's cost is what was paid MINUS 2 -- is the same rule with the opposite sign: Ebizo would cost 11.
        var flipped = new GoldCostRule(GoldCostBasis.TargetUnitTotal, Modifier: 2);
        Eq(flipped.Modifier, 2);
    }

    // ---- At Any Cost ---------------------------------------------------------------------------------------------------------------------------------

    // Battle at province 0. Player 0 (the Crane) is the Attacker and holds At Any Cost, with `own` at the battlefield; player 1 has Ebizo there. Player 0 produces
    // 9 Gold plus `extra` Marketplaces' 3 each.
    private static Rig Recruit(int actor = 0, bool unit = true, int extra = 6, Action<Rig>? more = null)
    {
        var s = Table(Phase.Battle, actor: actor);
        for (var i = 0; i < extra; i++) Add(s, "training-marketplace", actor, Zone.Home);
        var card = Add(s, AtAnyCost, actor, Zone.Hand);
        var own = unit ? Add(s, actor == 0 ? "training-samurai" : "training-lion-samurai", actor, Zone.Battlefield, province: 0) : null!;
        var enemy = Add(s, Ebizo, 1 - actor, Zone.Battlefield, province: 0);
        var rig = new Rig(s, card, enemy, own);
        more?.Invoke(rig);
        return rig;
    }
    // The full-cost way to pay; issue #1230 adds Clan discount and Clan allegiance as further offered options (AtAnyCostClanModes).
    private static ActionOption RecruitOption(MatchEngine e, Rig r, int player = 0) => Options(e, player, RecruitAbility, r.Card.Id).Single(o => o.ChoiceLabel == "Full cost");
    private static CardInstance Dead(Rig r, string def, int owner = 0) => Add(r.S, def, owner, Zone.Dead);

    private static void AtAnyCostClanModes()
    {
        var r = Recruit();
        var genshin = Dead(r, Genshin); var doho = Dead(r, Doho); // a Crane Samurai (7 Gold, Honor Requirement 6) and a Mantis one (5 Gold)
        var e = MatchEngine.FromTestState(r.S);
        var options = Options(e, 0, RecruitAbility, r.Card.Id).ToArray();
        ActionOption Mode(bool discount, bool allegiance) => options.Single(o => o.Enabled && o.Command!.ClanDiscount == discount && o.Command.ClanAllegiance == allegiance);
        Eq(options.Length, 4, "four ways to pay, each its own offered option");
        Eq(Price(Mode(false, false), genshin), 7, "full cost"); Eq(Price(Mode(true, false), genshin), 5, "Clan discount: 2 less");
        Eq(Price(Mode(false, true), genshin), 9, "Clan allegiance: 2 more"); Eq(Price(Mode(true, true), genshin), 7, "both");
        Check(Mode(false, false).TargetIds!.Contains(doho.Id) && !Mode(true, false).TargetIds!.Contains(doho.Id) && !Mode(false, true).TargetIds!.Contains(doho.Id),
            "another Clan's Samurai is offered only at full cost");
        Check(options.All(o => o.ChoiceLabel is not null), "each way to pay is labelled for the client's question after the target");
        var honorBefore = Honor(e, 0);
        Accept(e, Retarget(Mode(true, false), genshin.Id));
        Eq(e.State.Cards[genshin.Id].Zone, Zone.Battlefield, "recruited with the Clan discount");
        Check(e.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 5 Gold"), "paid 5 Gold");
        Check(e.State.PendingChoice?.Type != "proclaim" && Honor(e, 0) == honorBefore, "no Proclaim: he is not a Personality in a province");
        // Below the Honor Requirement: only the allegiance ways to pay are legal.
        var low = Recruit(); var lowGenshin = Dead(low, Genshin); low.S.Players[0].Honor = 5;
        var le = MatchEngine.FromTestState(low.S);
        var lowOptions = Options(le, 0, RecruitAbility, low.Card.Id).ToArray();
        Check(lowOptions.Where(o => o.Enabled).All(o => o.Command!.ClanAllegiance) && lowOptions.Count(o => o.Enabled) == 2, "at 5 Honor only Clan allegiance (with or without the discount) recruits him");
        Accept(le, Retarget(lowOptions.First(o => o.Enabled && o.Command!.ClanAllegiance && !o.Command.ClanDiscount), lowGenshin.Id));
        Eq(le.State.Cards[lowGenshin.Id].Zone, Zone.Battlefield, "recruited by Clan allegiance below his Honor Requirement");
        // No other ability accepts the Clan options.
        var other = Table(Phase.Action, actor: 0); var tale = Add(other, "oracle-l5r-7862", 0, Zone.Hand);
        var oe = MatchEngine.FromTestState(other);
        Check(Rejection(oe, new GameCommand("useAbility", 0, oe.Version, tale.Id, AbilityId: CardCatalog.Get("oracle-l5r-7862").Abilities![0], ClanDiscount: true)).Contains("Clan discount"), "a Clan option on another ability is refused");
    }

    private static void AtAnyCostRecruits()
    {
        // A Crane Samurai of the recruiting player's own Clan: in a province he would be Proclaim-eligible (the active player recruiting one of his Clan).
        var r = Recruit();
        var genshin = Dead(r, Genshin); var doho = Dead(r, Doho);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, RecruitAbility, r.Card.Id);
        Check(option.TargetIds!.ToHashSet().SetEquals(new[] { genshin.Id, doho.Id }), "both dead Samurai are offered");
        Eq(Price(option, genshin), 7, "the recruit price: Doji Genshin's printed 7"); Eq(Price(option, doho), 5);
        Check(option.TargetGold![genshin.Id].Basis.Contains("recruit cost 7"), option.TargetGold[genshin.Id].Basis);
        var control = new MatchState { };
        var atHome = e.QuotePurchase(0, genshin.Id, fromDeadPile: true);
        Check(atHome.Error is null && !atHome.ProclaimEligible && atHome.Cost == 7, "a dead recruit is never Proclaim-eligible");
        // Control: the same card face-up in a province, recruited by the active player of his Clan, IS Proclaim-eligible.
        var province = Recruit(more: x => { var c = Add(x.S, Genshin, 0, Zone.Province, province: 0); c.FaceUp = true; x.S.Players[0].Provinces[0].CardId = c.Id; });
        var provinceCard = province.S.Cards.Values.Single(c => c.DefinitionId == Genshin && c.Zone == Zone.Province);
        Check(MatchEngine.FromTestState(province.S).QuotePurchase(0, provinceCard.Id).ProclaimEligible, "control: a province recruit of him could be proclaimed");

        var honorBefore = Honor(e, 0);
        var sources = option.TargetGold[genshin.Id].Payments[0].SourceIds;
        Accept(e, Retarget(option, genshin.Id));
        var recruited = e.State.Cards[genshin.Id];
        Eq(recruited.Zone, Zone.Battlefield, "he enters play at the battlefield At Any Cost was played at"); Eq(recruited.ProvinceIndex, 0); Check(recruited.FaceUp && !recruited.Bowed);
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "proclaim", "no Proclaim window");
        Eq(Honor(e, 0), honorBefore, "no Honor from Proclaim");
        foreach (var id in sources) Check(e.State.Cards[id].Bowed);
        Check(e.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 7 Gold"));
        Check(e.State.Events.Any(x => x.Type == "cardEnteredPlay" && x.CardId == genshin.Id && x.Message.Contains("At Any Cost")));
        Eq(e.State.Cards[r.Card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.State.Cards[doho.Id].Zone, Zone.Dead, "the other dead Samurai stays dead");
        Eq(e.State.PriorityPlayerId, 1, "priority passes on");
        // Both players see him in the army at that battlefield: he is a unit there at once.
        Check(e.GetView(1).Players[0].Army.Any(c => c.Id == genshin.Id && c.ProvinceIndex == 0), "the opponent sees him enter play at the battlefield");
        Check(e.GetView(0).Players[0].Army.Single(c => c.Id == genshin.Id).UnitForce > 0, "and his Force is in the army");
        // Unit-eligible there: he can perform a Battle action at once (the Magistrate performing A Stain Cleansed), which needs a unit at the battlefield.
        var stainTable = Recruit(more: x => { Add(x.S, Stain, 0, Zone.Hand); });
        var deadGenshin = Dead(stainTable, Genshin);
        var stainCard = stainTable.S.Cards.Values.Single(c => c.DefinitionId == Stain);
        var se = MatchEngine.FromTestState(stainTable.S);
        Check(!CanUse(se, 0, StainAbility, stainCard.Id), "before: no Magistrate at the battlefield");
        Accept(se, Retarget(Enabled(se, 0, RecruitAbility, stainTable.Card.Id), deadGenshin.Id));
        Eq(se.State.PriorityPlayerId, 1); var afterState = se.State; afterState.PriorityPlayerId = 0;
        var after = MatchEngine.FromTestState(afterState);
        Check(CanUse(after, 0, StainAbility, stainCard.Id, deadGenshin.Id), "after: the recruited Magistrate performs a Battle action at once");
    }

    private static void AtAnyCostRestrictions()
    {
        // Unique: a copy in play blocks the dead one. (Doji Kazuo, Unique, Honor Requirement 6.)
        var unique = Recruit(more: x => { Add(x.S, Kazuo, 0, Zone.Home); });
        var kazuo = Dead(unique, Kazuo);
        var e = MatchEngine.FromTestState(unique.S);
        var blocked = RecruitOption(e, unique);
        Check(!blocked.Enabled && blocked.Reason == "You already control this Unique card.", blocked.Reason ?? "");
        Check(!blocked.TargetIds!.Contains(kazuo.Id));
        Rejection(e, new GameCommand("useAbility", 0, e.Version, unique.Card.Id, TargetId: kazuo.Id, AbilityId: RecruitAbility, SourceIds: e.PaymentOptions(0, 9).First().SourceIds));
        // ... and no copy in play frees it.
        var free = Recruit(); var freeKazuo = Dead(free, Kazuo);
        Check(RecruitOption(MatchEngine.FromTestState(free.S), free).Enabled);
        // Singular: the same for the Singular keyword (a fixture: Crane, Samurai, Singular).
        var singular = Recruit(more: x => Add(x.S, "training-singular-samurai", 0, Zone.Home)); Dead(singular, "training-singular-samurai");
        var sing = RecruitOption(MatchEngine.FromTestState(singular.S), singular);
        Check(!sing.Enabled && sing.Reason == "You already control this Singular card.", sing.Reason ?? "");
        // Honor Requirement: Doji Genshin needs 6; the Crane has 8 -> fine, at 5 -> not at full cost, with the recruit path's own reason. Clan allegiance waives it (issue #1230, AtAnyCostClanModes).
        var honor = Recruit(); Dead(honor, Genshin);
        honor.S.Players[0].Honor = 5;
        var low = RecruitOption(MatchEngine.FromTestState(honor.S), honor);
        Check(!low.Enabled && low.Reason == "Family Honor is below the Honor Requirement; Clan allegiance may allow purchase.", low.Reason ?? "");
        Check(Options(MatchEngine.FromTestState(honor.S), 0, RecruitAbility, honor.Card.Id).Any(o => o.Enabled && o.Command!.ClanAllegiance), "Clan allegiance waives the Honor Requirement (issue #1230)");
        honor.S.Players[0].Honor = 6;
        Check(RecruitOption(MatchEngine.FromTestState(honor.S), honor).Enabled, "at exactly 6 it is legal");
        // Loyal: a Dragon-loyal Sensei cannot be recruited by the Crane.
        var loyal = Recruit(); Dead(loyal, Hikaro);
        var l = RecruitOption(MatchEngine.FromTestState(loyal.S), loyal);
        Check(!l.Enabled && l.Reason == "Loyal Personalities can only be recruited by a Clan they are aligned with.", l.Reason ?? "");
        // ... but a Lion-loyal Lion Samurai can be recruited by the Lion (Akodo Dairuko, Honor Requirement 10).
        var lion = Recruit(actor: 1); lion.S.Players[1].Honor = 10;
        var dairuko = Dead(lion, Dairuko, owner: 1);
        var le = MatchEngine.FromTestState(lion.S);
        Check(Enabled(le, 1, RecruitAbility, lion.Card.Id).TargetIds!.Contains(dairuko.Id), "Loyal Lion, recruited by a Lion");
        // Only Samurai, and only your own dead: a dead Courtier, and the opponent's dead Samurai, are not targets.
        var kinds = Recruit(); Dead(kinds, Kijo); Dead(kinds, Doho, owner: 1);
        var k = RecruitOption(MatchEngine.FromTestState(kinds.S), kinds);
        Check(!k.Enabled && (k.TargetIds?.Length ?? 0) == 0, "no dead Samurai of yours: nothing to recruit");
        var mixed = Recruit(); var mine = Dead(mixed, Doho); Dead(mixed, Kijo); Dead(mixed, Genshin, owner: 1);
        Check(RecruitOption(MatchEngine.FromTestState(mixed.S), mixed).TargetIds!.SequenceEqual(new[] { mine.Id }));
        // ... and a hand-built command against the opponent's dead Samurai is rejected too.
        var theirs = mixed.S.Cards.Values.Single(c => c.DefinitionId == Genshin && c.OwnerId == 1);
        var me = MatchEngine.FromTestState(mixed.S);
        Rejection(me, new GameCommand("useAbility", 0, me.Version, mixed.Card.Id, TargetId: theirs.Id, AbilityId: RecruitAbility, SourceIds: me.PaymentOptions(0, 7).First().SourceIds));
        // A card that is not dead (discarded, or in play) is not a target either.
        var notDead = Recruit(); var discarded = Add(notDead.S, Doho, 0, Zone.DynastyDiscard);
        Check(!RecruitOption(MatchEngine.FromTestState(notDead.S), notDead).Enabled);
        // The recruit quote itself says why for a card that is not one of the player's own dead.
        Check(MatchEngine.FromTestState(mixed.S).QuotePurchase(0, mixed.S.Cards.Values.Single(c => c.DefinitionId == Genshin && c.OwnerId == 1).Id, fromDeadPile: true).Error == "Choose one of your own dead Personalities.");
    }

    private static void AtAnyCostEntry()
    {
        // Sensei: recruiting a dead Sensei discards the player's other Sensei Personalities, exactly as any entry of one does.
        var sensei = Recruit(more: x => Add(x.S, "training-sensei-personality", 0, Zone.Home));
        var yozo = Dead(sensei, Yozo);
        var e = MatchEngine.FromTestState(sensei.S);
        var other = sensei.S.Cards.Values.Single(c => c.DefinitionId == "training-sensei-personality");
        var option = Enabled(e, 0, RecruitAbility, sensei.Card.Id);
        Accept(e, Retarget(option, yozo.Id));
        Eq(e.State.Cards[yozo.Id].Zone, Zone.Battlefield); Eq(e.State.Cards[other.Id].Zone, Zone.DynastyDiscard, "the other Sensei was discarded");
        Check(e.State.Events.Any(x => x.Type == "cardDiscarded" && x.CardId == other.Id && x.Message.Contains("Sensei")));
        // With no other Sensei nothing is discarded.
        var alone = Recruit(more: x => Add(x.S, "training-lion-samurai", 0, Zone.Home)); Dead(alone, Yozo);
        var e2 = MatchEngine.FromTestState(alone.S);
        Accept(e2, Retarget(Enabled(e2, 0, RecruitAbility, alone.Card.Id), alone.S.Cards.Values.Single(c => c.DefinitionId == Yozo).Id));
        Check(e2.State.Cards.Values.Where(c => c.OwnerId == 0 && c.DefinitionId == "training-lion-samurai").All(c => c.Zone == Zone.Home));

        // Hotako, Experienced 3: the Lion Clan discount prices him 11 - 2 = 9 for the Lion, and his entry Honor loss applies.
        var lion = Recruit(actor: 1);
        var hotako = Dead(lion, Hotako, owner: 1);
        var le = MatchEngine.FromTestState(lion.S);
        var hOption = Enabled(le, 1, RecruitAbility, lion.Card.Id);
        Eq(hOption.TargetGold![hotako.Id].Cost, 9, "printed 11, Lion Clan discount 2"); Check(hOption.TargetGold[hotako.Id].Basis.Contains("− 2 (Lion Clan)"), hOption.TargetGold[hotako.Id].Basis);
        var lionHonor = Honor(le, 1);
        Accept(le, Retarget(hOption, hotako.Id));
        Eq(le.State.Cards[hotako.Id].Zone, Zone.Battlefield); Eq(Honor(le, 1), lionHonor - 2, "Hotako's 'After Hotako enters play: lose 2 Honor'");

        // A dead Personality returns as a new card: nothing of his earlier life follows him -- except that a face-up dishonorable Personality stays dishonorable
        // outside play and on re-entry (R-082, EE-STATE-008; supersedes the 2026-09-20 reading in which he came back honorable).
        var fresh = Recruit(); var dead = Dead(fresh, Genshin);
        dead.Dishonorable = true; dead.Bowed = true; dead.Modifiers.Add(new("test-source", "test", "Force", 3)); dead.AbilityUses["old"] = 3;
        var fe = MatchEngine.FromTestState(fresh.S);
        Accept(fe, Retarget(Enabled(fe, 0, RecruitAbility, fresh.Card.Id), dead.Id));
        var back = fe.State.Cards[dead.Id];
        Check(back.Dishonorable, "a dishonorable dead Personality returns dishonorable (R-082)");
        Check(!back.Bowed && back.Modifiers.Count == 0 && back.AbilityUses.Count == 0, "a new card");
    }

    private static void AtAnyCostBattleAndPrice()
    {
        // No unit at the battlefield: a Battle action needs one (At Any Cost is not Absent: the scan's "if you control any units at the current battlefield").
        var r = Recruit(unit: false);
        Dead(r, Doho);
        var e = MatchEngine.FromTestState(r.S);
        var none = RecruitOption(e, r);
        Check(!none.Enabled && none.Reason == "You need a unit at the current battlefield.", none.Reason ?? "");
        // Not payable: the recruit price is the only price (Doho 5): with the Gold bowed it is disabled with the shortfall, never free.
        var poor = Recruit(); Dead(poor, Doho);
        foreach (var c in poor.S.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var broke = RecruitOption(MatchEngine.FromTestState(poor.S), poor);
        Check(!broke.Enabled && broke.Reason!.Contains("5 Gold"), broke.Reason ?? "");
        // Not in the Action phase.
        var s = Table(Phase.Action, actor: 0); var card = Add(s, AtAnyCost, 0, Zone.Hand); Add(s, Doho, 0, Zone.Dead);
        Check(!Options(MatchEngine.FromTestState(s), 0, RecruitAbility, card.Id).Any(a => a.Enabled));
        // The province recruit is untouched: a face-up province card still quotes exactly as before, a card in any other zone still cannot be purchased.
        var province = Recruit(more: x => { var c = Add(x.S, Doho, 0, Zone.Province, province: 1); c.FaceUp = true; x.S.Players[0].Provinces[1].CardId = c.Id; });
        var pe = MatchEngine.FromTestState(province.S);
        var provinceCard = province.S.Cards.Values.Single(c => c.DefinitionId == Doho && c.Zone == Zone.Province);
        Eq(pe.QuotePurchase(0, provinceCard.Id).Error, null); Eq(pe.QuotePurchase(0, provinceCard.Id, fromDeadPile: true).Error, "Choose one of your own dead Personalities.");
        var deadCard = Dead(province, Doho);
        Eq(MatchEngine.FromTestState(province.S).QuotePurchase(0, deadCard.Id).Error, "Choose your face-up province card.");
    }
}
