using L5R.Core;
using static RegressionAssertions;

// Issue #833, single-reaction audit slice 3: cards that print exactly ONE Reaction. Each test opens the Reaction's real trigger window through ordinary
// engine play (a purchase, an assignment, an engage, a Battle or Limited action, a revealed Event ...), proves the right player is offered it and the
// other is not, accepts it and checks the printed effect with concrete numbers/zones, then proves it is not offered (and the same command is refused)
// once the window is closed or was never opened. Gold-source Reactions ("When paying a Gold cost") are driven through a Gold-priced action in each
// window the cost can be paid in. A forged pending state is used only where the comment says no real path exists.
internal static partial class SingleReaction3Tests
{
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static string Rejection(MatchEngine e, GameCommand c) => Batch336Support.Rejection(e, c);
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void RunUntil(MatchEngine e, Func<bool> stop, Action? each = null) => Batch336Support.RunUntil(e, stop, each);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }
    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static string Window(MatchEngine e) => e.State.PendingChoice?.Type ?? "";
    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);

    // Declines every window still open (nobody else holds a Reaction in these positions), so the action has fully resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, Decline(e));
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner, listed for nobody else and refused when the other player submits it; on a copy where
    // every window is declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`).
    private static GameCommand InWindow(MatchEngine e, int owner, string ability, string cardId, string? performerId = null, string? targetId = null, string? windowType = null)
    {
        Check(e.State.PendingChoice is not null, ability + ": no window is open");
        if (windowType is not null) Check(e.State.PendingChoice!.Type == windowType, $"{ability}: the window is {e.State.PendingChoice!.Type}, not {windowType}");
        if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e)); // the other player holds the first opportunity and has nothing to react with
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner}" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason} targets=[{string.Join(",", a.TargetIds ?? [])}] cmdTarget={a.Command?.TargetId}")));
        var command = option!.Command!;
        if (targetId is not null) command = command with { TargetId = targetId };
        if (performerId is not null) command = command with { PerformerId = performerId };
        Check(!CanUse(e, 1 - owner, ability, cardId), ability + " is listed enabled for the other player");
        Rejection(e, command with { PlayerId = 1 - owner });
        var closed = e.Clone();
        Settle(closed);
        Check(closed.State.PendingChoice is null, ability + ": the window did not close");
        Check(!CanUse(closed, owner, ability, cardId, performerId), ability + " is still offered after the window closed");
        Rejection(closed, command with { ExpectedVersion = closed.Version });
        return command;
    }

    // A fresh position in which the card sits in play and no window was ever opened: not offered to anyone, and its command is refused.
    private static void NeverOpened(MatchEngine e, int owner, string ability, string cardId, GameCommand command)
    {
        Check(e.State.PendingChoice is null, "a window is open");
        Check(!CanUse(e, owner, ability, cardId) && !CanUse(e, 1 - owner, ability, cardId), ability + " is offered with no window open");
        Rejection(e, command with { ExpectedVersion = e.Version, PlayerId = owner });
    }

    // ---- Gold-source Reactions: "When paying a Gold cost" -------------------------------------------------------------------------------------------------------------------
    private static void BowAllGold(MatchState s, int owner)
    {
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
    }

    // Runs a 2-Gold action of the acting player's (Battle: Shusaku's Repeatable boost; Action phase: Interrogation's Open draw) in all four windows, paid by the
    // reactor alone, and checks the reactor is no payment source for the other player. `discard` is the card Keigo must name.
    private static void GoldWindows(string reactorDef, string ability, int produces, bool needsDiscard, Action<MatchEngine, CardInstance, int> effect)
    {
        foreach (var scenario in new[] { "battle-attacker", "battle-defender", "action-active", "action-other" })
        {
            var battle = scenario.StartsWith("battle");
            var me = scenario is "battle-defender" or "action-active" ? 0 : 1;
            var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0);
            s.PriorityPlayerId = me;
            BowAllGold(s, 0); BowAllGold(s, 1);
            var reactor = Add(s, reactorDef, me, Zone.Home);
            CardInstance? pitch = null;
            if (needsDiscard) pitch = Add(s, "training-focus-strategy", me, Zone.Hand); // Focus Value 3
            CardInstance payer;
            string payAbility;
            if (battle)
            {
                payer = Add(s, Def("wlpa-shusaku-boost"), me, Zone.Battlefield, province: 0); payAbility = "wlpa-shusaku-boost";
                Add(s, "training-lion-samurai", 1 - me, Zone.Battlefield, province: 0);
            }
            else { payer = Add(s, Def("pe-interrogation-draw"), me, Zone.Hand); payAbility = "pe-interrogation-draw"; }
            Add(s, "training-focus-strategy", me, Zone.FateDeck); // something to draw
            var e = MatchEngine.FromTestState(s);
            Check(e.PaymentOptions(me, 2).Any(p => p.SourceIds.SequenceEqual([reactor.Id])) || needsDiscard, $"{ability} [{scenario}]: the engine offers the reactor to pay 2 Gold");
            Check(e.PaymentOptions(1 - me, 2).All(p => !p.SourceIds.Contains(reactor.Id)), $"{ability} [{scenario}]: the other player cannot pay with it");
            // Keigo's amount is the payer's own choice, so the engine never suggests him and lists a Keigo-only payment disabled: the command is built by hand.
            var command = needsDiscard ? Use(e, me, payAbility, payer.Id) with { SourceIds = [reactor.Id] } : Enabled(e, me, payAbility, payer.Id).Command! with { SourceIds = [reactor.Id] };
            if (needsDiscard) command = command with { PaymentDiscards = new() { [reactor.Id] = pitch!.Id } };
            var other = command with { PlayerId = 1 - me };
            Rejection(e, other);
            var hand = e.State.Players[me].Hand.Count; var force = e.CardForceForTest(payer.Id);
            Accept(e, command);
            Check(e.State.Cards[reactor.Id].Bowed, $"{ability} [{scenario}]: the reactor bowed to produce the Gold");
            Eq(e.State.Cards[reactor.Id].AbilityUses[ability], e.State.TurnNumber, $"{ability} [{scenario}]: spent for the turn");
            Check(!e.PaymentOptions(me, 1).Any(p => p.SourceIds.Contains(reactor.Id)), $"{ability} [{scenario}]: a bowed source produces nothing more");
            if (battle) Eq(e.CardForceForTest(payer.Id), force + 1, $"{ability} [{scenario}]: Shusaku's +1F, so the cost was met");
            else Eq(e.State.Players[me].Hand.Count, hand - 1 - (needsDiscard ? 1 : 0) + 1, $"{ability} [{scenario}]: Interrogation left the hand and a card was drawn");
            effect(e, reactor, produces);
            if (needsDiscard) Eq(e.State.Cards[pitch!.Id].Zone, Zone.FateDiscard, $"{ability} [{scenario}]: the named card was discarded");
        }
    }

    private static void KeigoGold()
    {
        const string ability = "keigo-produce-gold";
        var def = Def(ability);
        // Every window a Gold cost can be paid in: Keigo bows and discards a Focus Value 3 card for 3 Gold; a card with no Focus Value yields nothing.
        GoldWindows(def, ability, 3, true, (e, keigo, _) => { });
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            BowAllGold(s, 0);
            var keigo = Add(s, def, 0, Zone.Home);
            var blank = Add(s, "training-crane-samurai", 0, Zone.Hand); // Focus Value 0
            var urgency = Add(s, "training-cost-fixture", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var draw = Use(e, 0, "training-urgency-draw", urgency.Id) with { SourceIds = [keigo.Id] };
            Check(Rejection(e, draw).Length > 0, "naming no card to discard is refused");
            Check(Rejection(e, draw with { PaymentDiscards = new() { [keigo.Id] = blank.Id } }).Contains("produce 0"), "a card with no Focus Value produces nothing");
            Check(!e.State.Cards[keigo.Id].Bowed && e.State.Cards[blank.Id].Zone == Zone.Hand, "a refused payment changes nothing");
        }
        // A Dynasty-phase recruit is a Gold cost too (a 3-Gold Personality bought from a Province with Keigo's 3 Gold).
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            BowAllGold(s, 0);
            var keigo = Add(s, def, 0, Zone.Home);
            var pitch = Add(s, "training-focus-strategy", 0, Zone.Hand);
            var provinceCard = s.Players[0].Provinces[0].CardId!; s.Cards[provinceCard].DefinitionId = "training-honor-personality"; s.Cards[provinceCard].FaceUp = true;
            s.Players[0].Honor = 20;
            var e = MatchEngine.FromTestState(s);
            Check(e.State.Phase == Phase.Dynasty, "the Dynasty phase");
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: provinceCard, SourceIds: [keigo.Id], PaymentDiscards: new() { [keigo.Id] = pitch.Id }));
            Check(e.State.Cards[provinceCard].Zone == Zone.Home && e.State.Cards[keigo.Id].Bowed && e.State.Cards[pitch.Id].Zone == Zone.FateDiscard, "recruited with Keigo's 3 Gold");
        }
        // Spent, bowed, in hand or not his: no source.
        {
            var s = Table(Phase.Action, 0); BowAllGold(s, 0);
            var spent = Add(s, def, 0, Zone.Home); spent.AbilityUses[ability] = s.TurnNumber;
            var bowed = Add(s, def, 0, Zone.Home, bowed: true);
            var inHand = Add(s, def, 0, Zone.Hand);
            Add(s, "training-focus-strategy", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var pitch = e.State.Players[0].Hand.First(h => CardCatalog.Get(e.State.Cards[h].DefinitionId).Focus > 0);
            var urgency = Add(s, "training-cost-fixture", 0, Zone.Home); e = MatchEngine.FromTestState(s);
            foreach (var source in new[] { spent, bowed })
                Check(Rejection(e, Use(e, 0, "training-urgency-draw", urgency.Id) with { SourceIds = [source.Id], PaymentDiscards = new() { [source.Id] = pitch } }).Length > 0, "a spent or bowed Keigo is refused as a source");
            Check(e.PaymentOptions(0, 1).All(p => !p.SourceIds.Contains(inHand.Id)), "Keigo in hand is no source");
            Check(e.PaymentOptions(0, 1).All(p => !p.SourceIds.Contains(inHand.Id)), "Keigo in hand is no source");
        }
    }

    private static void WashiGold()
    {
        const string ability = "washi-produce-gold";
        var def = Def(ability);
        Eq(AbilityCatalog.All[ability].PaymentProduction, 2);
        GoldWindows(def, ability, 2, false, (e, washi, _) => Check(e.State.Cards[washi.Id].Bowed, "Washi bowed"));
        // A Limited action in the Action phase and a Dynasty recruit are Gold costs too; a bowed or spent Washi produces nothing.
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; BowAllGold(s, 0);
            var washi = Add(s, def, 0, Zone.Home);
            var urgency = Add(s, "training-cost-fixture", 0, Zone.Home);
            Add(s, "training-focus-strategy", 0, Zone.FateDeck);
            var e = MatchEngine.FromTestState(s);
            var deck = e.State.Players[0].FateDeck.Count;
            Accept(e, Use(e, 0, "training-urgency-draw", urgency.Id) with { SourceIds = [washi.Id] });
            Eq(e.State.Players[0].FateDeck.Count, deck - 1, "the 2-Gold Limited action drew its card, paid by Washi");
            Check(e.State.Cards[washi.Id].Bowed, "Washi bowed");
            Eq(e.State.Cards[washi.Id].AbilityUses[ability], e.State.TurnNumber, "his Reaction is spent");
        }
        {
            var s = MatchEngine.CreateEconomyScenario().State; BowAllGold(s, 0);
            var washi = Add(s, def, 0, Zone.Home);
            var provinceCard = s.Players[0].Provinces[0].CardId!; s.Cards[provinceCard].DefinitionId = "training-honor-personality"; s.Cards[provinceCard].FaceUp = true; // 3 Gold
            Add(s, "training-marketplace", 0, Zone.Home);
            s.Players[0].Honor = 20;
            var e = MatchEngine.FromTestState(s);
            Check(e.PaymentOptions(0, 2).Any(p => p.SourceIds.SequenceEqual([washi.Id])), "Washi pays a 2-Gold cost alone");
            Check(e.PaymentOptions(0, 3).Any(p => p.SourceIds.Contains(washi.Id) && p.SourceIds.Length == 2), "and a 3-Gold recruit with a Marketplace");
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: provinceCard, SourceIds: e.PaymentOptions(0, 3).First(p => p.SourceIds.Contains(washi.Id)).SourceIds));
            Check(e.State.Cards[provinceCard].Zone == Zone.Home && e.State.Cards[washi.Id].Bowed, "recruited with Washi's 2 Gold in the payment");
        }
        {
            var s = Table(Phase.Action, 0); BowAllGold(s, 0);
            var bowed = Add(s, def, 0, Zone.Home, bowed: true);
            var spent = Add(s, def, 0, Zone.Home); spent.AbilityUses[ability] = s.TurnNumber;
            var inHand = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Eq(e.GoldProduced(spent.Id, 0), 0, "spent: nothing");
            Check(e.PaymentOptions(0, 2).All(p => !p.SourceIds.Contains(inHand.Id) && !p.SourceIds.Contains(bowed.Id)), "and neither (nor one in hand) is offered");
        }
    }


    // ---- Bayushi Mitsuo: after a Battle action that moved him, an additional Battle action ---------------------------------------------------------------------------------------
    private static void MitsuoAdditional()
    {
        const string ability = "mitsuo-additional-action";
        var def = Def(ability);
        foreach (var me in new[] { 0, 1 })
            foreach (var own in new[] { false, true })
            {
                // Player 1 attacks. Presence (issue #1133): a Reaction in a battle needs its source at the battlefield, so an action that moved Mitsuo HOME could never be
                // answered by him. His own Trusting Instinct (own) or a Battle fixture of his controller's (training-move-current, !own) moves him from home to the
                // battlefield. Either way the controller holds the window and the extra action it grants.
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
                var mitsuo = Add(s, def, me, Zone.Home);
                var mover = Bf(s, "training-move-source", me);
                var bower = Bf(s, "training-battle-bower", me); var foe = Bf(s, "training-lion-samurai", 1 - me);
                var instinct = own ? Add(s, Def("trusting-instinct-move"), me, Zone.Hand) : null;
                var bystander = Bf(s, "training-crane-samurai", me);
                var e = MatchEngine.FromTestState(s);
                var label = $"me={me} own={own}";
                if (own) Accept(e, Use(e, me, "trusting-instinct-move", instinct!.Id, null, mitsuo.Id));
                else Accept(e, Offer(e, me, "training-move-current", mover.Id, mitsuo.Id));
                Eq(e.State.Cards[mitsuo.Id].Zone, Zone.Battlefield, label + ": the action moved Mitsuo");
                Eq(Window(e), "afterActionResolvedReaction", label);
                var command = InWindow(e, me, ability, mitsuo.Id, windowType: "afterActionResolvedReaction");
                Check(e.State.AdditionalActions.All(g => g.PlayerId != me), label + ": no extra action yet");
                Accept(e, command);
                Check(e.State.AdditionalActions.Any(g => g.PlayerId == me), label + ": Mitsuo's controller may take an additional action");
                Check(!CanUse(e, me, ability, mitsuo.Id), label + ": taken once");
                Settle(e);
                // The additional action is a Battle action: his controller uses one now (his own Battle fixture bows the enemy).
                Check(e.State.PendingChoice is null && e.State.AdditionalActions.Any(g => g.PlayerId == me), label + ": the extra action is still unused after the window");
                Accept(e, Offer(e, me, "training-bow-at-battle", bower.Id, foe.Id));
                Check(e.State.Cards[foe.Id].Bowed, label + ": the additional Battle action resolved");
                Check(e.State.AdditionalActions.All(g => g.PlayerId != me), label + ": and is spent");
                // The same move of somebody else does not trigger him.
                if (!own)
                {
                    var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 1 - me;
                    var m2 = Bf(t, def, me); var other = Bf(t, "training-crane-samurai", me); var mv = Bf(t, "training-move-source", 1 - me);
                    var f = MatchEngine.FromTestState(t);
                    Accept(f, Offer(f, 1 - me, "training-move-both-home", mv.Id, other.Id));
                    Eq(f.State.Cards[other.Id].Zone, Zone.Home, label + ": the bystander moved");
                    Eq(f.State.Cards[m2.Id].Zone, Zone.Battlefield, label + ": Mitsuo did not");
                    Check(!CanUse(f, me, ability, m2.Id), label + ": not offered");
                }
            }
        // Never opened: standing in the battle, no action has moved him.
        var n = Table(Phase.Battle, 1); n.PriorityPlayerId = 0;
        var idle = Bf(n, def, 0);
        var ne = MatchEngine.FromTestState(n);
        NeverOpened(ne, 0, ability, idle.Id, Use(ne, 0, ability, idle.Id));
    }

    // ---- Absolution: Interrupt before an Event resolves ------------------------------------------------------------------------------------------------------------------------
    private const string CherryBlossomFestival = "oracle-l5r-1314";
    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false;
        if (s.Players[owner].Provinces[province].CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }

    // Player 0 is in his Action Phase; driven with offered commands until player 1's Events Phase has revealed the Event (and its before-Event window opened).
    private static MatchEngine RevealNextTurn(Action<MatchState> prepare)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
        prepare(s);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice is { Type: "p3BeforeEventReaction" or "p3MikiDarkVirtue" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        return e;
    }

    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static void AbsolutionNegate()
    {
        const string ability = "absolution-negate";
        var def = Def(ability);
        foreach (var holder in new[] { 0, 1 })
        {
            CardInstance festival = null!, absolution = null!;
            var e = RevealNextTurn(s => { festival = FaceDownIn(s, CherryBlossomFestival, 1, 3); absolution = Add(s, def, holder, Zone.Hand); });
            Eq(Window(e), "p3BeforeEventReaction", "the window before the Event resolves");
            Check(e.State.Cards[festival.Id].FaceUp, "the Event was revealed");
            var command = InWindow(e, holder, ability, absolution.Id, windowType: "p3BeforeEventReaction");
            Accept(e, command);
            PassUntil(e, () => e.State.PendingChoice?.Type != "p3BeforeEventReaction");
            Eq(e.State.Cards[festival.Id].Zone, Zone.DynastyDiscard, $"holder {holder}: the negated Event was discarded without its effects");
            Eq(e.State.Cards[absolution.Id].Zone, Zone.FateDiscard, "Absolution is spent");
            Check(e.State.Players[1].Provinces[3].CardId is { } refill && refill != festival.Id, "the province was refilled");
            // Control: with the same position and nobody answering, the Event resolves and stays in play.
            CardInstance festival2 = null!;
            var control = RevealNextTurn(s => { festival2 = FaceDownIn(s, CherryBlossomFestival, 1, 3); Add(s, def, holder, Zone.Hand); });
            PassUntil(control, () => control.State.PendingChoice?.Type != "p3BeforeEventReaction");
            Check(control.State.Cards[festival2.Id].Zone != Zone.DynastyDiscard, "passed: the Event resolved (it stays in play)");
        }
        // Only a revealed Event opens it: with no Absolution held there is no window, and in an ordinary Action phase the card is not on offer and its command is refused.
        var none = RevealNextTurn(s => FaceDownIn(s, CherryBlossomFestival, 1, 3));
        Check(none.State.PendingChoice?.Type != "p3BeforeEventReaction", "no Interrupt held: no window");
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var held = Add(s2, def, 0, Zone.Hand);
        s2.Cards[s2.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; // player abilities need a published Stronghold
        var f = MatchEngine.FromTestState(s2);
        NeverOpened(f, 0, ability, held.Id, Use(f, 0, ability, held.Id));
    }

    // ---- An Insult Answered: Iaijutsu Revenge Reaction after another player's Honor-costing Limited/Open action ------------------------------------------------------------------
    private static void InsultAnsweredDuel()
    {
        const string ability = "insult-answered-duel";
        var def = Def(ability);
        var baikoDef = Def("baiko-shame");
        foreach (var performerStronger in new[] { false, true })
        {
            // Player 1 (active) has Baiko; his Limited action costs player 0 1 Honor (an Imperial Favor is needed for the action). Player 0 holds the Strategy and an unbowed Duelist.
            var s = Table(Phase.Action, 1);
            s.FavorPlayerId = 1;
            var baiko = Add(s, baikoDef, 1, Zone.Home);
            if (performerStronger) baiko.Modifiers.Add(new("test-source", "test", "Chi", 10));
            var shamed = Add(s, "training-crane-samurai", 0, Zone.Home); shamed.Dishonorable = true;
            var duelist = Add(s, "training-duelist-personality", 0, Zone.Home); // Chi 3, Duelist: wins ties
            var bowedDuelist = Add(s, "training-duelist-personality", 0, Zone.Home, bowed: true);
            var card = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, 0);
            Accept(e, Offer(e, 1, "baiko-shame", baiko.Id, shamed.Id));
            Eq(Honor(e, 0), honor - 1, "Baiko cost player 0 1 Honor");
            Eq(Window(e), "afterActionResolvedReaction");
            var offer = Enabled(e, 0, ability, card.Id);
            Check(offer.TargetIds!.SequenceEqual([baiko.Id]), "the action's performer is the only target");
            Check(!Options(e, 0, ability, card.Id, bowedDuelist.Id).Any(o => o.Enabled && o.Label.Contains($"({bowedDuelist.Id})")), "a bowed Duelist is not a performer");
            var command = InWindow(e, 0, ability, card.Id, performerId: duelist.Id, targetId: baiko.Id, windowType: "afterActionResolvedReaction");
            Accept(e, command);
            Check(e.State.PendingDuel is { } duel && duel.ChallengerId == duelist.Id && duel.ChallengedId == baiko.Id, "the Duelist challenges Baiko");
            RunDuel(e);
            Check(e.State.PendingDuel is null, "the duel finished");
            var winner = performerStronger ? baiko : duelist; var loser = performerStronger ? duelist : baiko;
            Eq(e.State.Cards[loser.Id].Zone, Zone.Dead, "the loser is destroyed");
            Check(e.State.Cards[winner.Id].Zone != Zone.Dead, "the winner survives");
            Eq(e.State.Cards[winner.Id].Tokens.Count(t => t.Type == "Revenge" && t.Stat == "Force" && t.Amount == 1), 2, "two +1F Revenge tokens on the winner");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        }
        // The Honor loss must be the holder's, from another player's action: an action that cost nobody Honor, and one the holder performed himself, open nothing for it.
        {
            var s = Table(Phase.Action, 1);
            var bower = Add(s, "training-skin-donor", 1, Zone.Home); var victim = Add(s, "training-crane-samurai", 0, Zone.Home);
            Add(s, "training-duelist-personality", 0, Zone.Home);
            var card = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-bow-any", bower.Id, victim.Id));
            Check(Window(e) != "afterActionResolvedReaction" || !CanUse(e, 0, ability, card.Id), "no Honor loss: no Reaction");
        }
        {
            // Player 0 shames his own Personality with his own Baiko: it is his action, not another player's. The Strategy in player 1's hand is not his to use either.
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            var baiko = Add(s, baikoDef, 0, Zone.Home); var own = Add(s, "training-crane-samurai", 0, Zone.Home); own.Dishonorable = true;
            Add(s, "training-duelist-personality", 0, Zone.Home); Add(s, "training-duelist-personality", 1, Zone.Home);
            var mine = Add(s, def, 0, Zone.Hand); var theirs = Add(s, def, 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, 0);
            Accept(e, Offer(e, 0, "baiko-shame", baiko.Id, own.Id));
            Eq(Honor(e, 0), honor - 1, "his own action cost him Honor");
            Check(Window(e) != "afterActionResolvedReaction" || !CanUse(e, 0, ability, mine.Id), "his own action: not offered to him");
            Check(!CanUse(e, 1, ability, theirs.Id), "player 1 lost no Honor: not offered to him");
        }
    }

    // A pending duel driven to its end: each player focuses from the deck, then the striker strikes.
    private static void RunDuel(MatchEngine e)
    {
        var focused = new HashSet<int>();
        for (var guard = 0; e.State.PendingDuel is not null && guard < 12; guard++)
        {
            if (e.State.PendingChoice is { Type: "duelReaction" }) { Accept(e, Decline(e)); continue; }
            if (e.State.PendingChoice is not null) break;
            var p = e.State.PriorityPlayerId;
            var options = e.QueryActions(p);
            var focus = options.FirstOrDefault(o => o.Type == "focus" && o.Command?.Choice == "deck");
            if (focus is not null && focused.Add(p)) { Accept(e, focus.Command!); continue; }
            var strike = options.FirstOrDefault(o => o.Type == "strike");
            if (strike is not null) { Accept(e, strike.Command!); continue; }
            var accept = options.FirstOrDefault(o => o.Type == "acceptDuel");
            if (accept is not null) { Accept(e, accept.Command!); continue; }
            var keep = options.FirstOrDefault(o => o.Type == "duelistRefocus" && o.Command?.CardId is null);
            if (keep is not null) { Accept(e, keep.Command!); continue; }
            break;
        }
    }

    // ---- Blade of Champions: when a Battle action would target a card in this unit, bow it: the action targets another of your cards ------------------------------------------------------------
    private static void BladeRedirect()
    {
        const string ability = "blade-of-champions-redirect";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            MatchState Rig(out CardInstance wielder, out CardInstance blade, out CardInstance q, out CardInstance r, out CardInstance bower, out CardInstance enemyCard, Phase phase = Phase.Battle)
            {
                var s = Table(phase, 1); s.PriorityPlayerId = foe;
                wielder = Bf(s, "training-lion-samurai", owner); blade = Add(s, def, owner, Zone.Attached, province: 0, parent: wielder.Id);
                q = Bf(s, "training-crane-samurai", owner); r = Bf(s, "training-shugenja", owner);
                bower = Bf(s, "training-battle-bower", foe); enemyCard = Bf(s, "training-lion-samurai", foe);
                return s;
            }
            var s = Rig(out var w, out var b, out var q, out var r, out var bower, out var enemy);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, foe, "training-bow-at-battle", bower.Id, w.Id));
            Eq(Window(e), "beforeActionReaction"); Eq(e.State.PriorityPlayerId, owner, "the window is the Blade's controller's");
            Check(Options(e, owner, ability, b.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { q.Id, r.Id }.OrderBy(x => x, StringComparer.Ordinal)),
                "exactly the controller's other Personalities the bow could legally target");
            Check(!e.State.Cards[b.Id].Bowed && !e.State.Cards[w.Id].Bowed, "nothing has happened yet");
            foreach (var bad in new[] { w.Id, b.Id, enemy.Id, bower.Id, "not-a-card" }) Rejection(e, new GameCommand("useAbility", owner, e.Version, CardId: b.Id, TargetId: bad, AbilityId: ability));
            var command = InWindow(e, owner, ability, b.Id, targetId: r.Id, windowType: "beforeActionReaction");
            Accept(e, command);
            Check(e.State.Cards[b.Id].Bowed, "the Blade is bowed as the cost");
            Eq(e.State.PendingNegatableAbility!.TargetId, r.Id, "the action now targets the chosen card");
            Check(!CanUse(e, owner, ability, b.Id), "and cannot react again");
            Settle(e);
            Check(e.State.Cards[r.Id].Bowed && !e.State.Cards[w.Id].Bowed && !e.State.Cards[q.Id].Bowed, "the action resolved on the chosen card only");
            // Declined: the action resolves on the original target and the Blade stays unbowed.
            var s2 = Rig(out var w2, out var b2, out _, out _, out var bower2, out _); var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Offer(e2, foe, "training-bow-at-battle", bower2.Id, w2.Id)); Settle(e2);
            Check(e2.State.Cards[w2.Id].Bowed && !e2.State.Cards[b2.Id].Bowed, "declined: the wielder is bowed, the Blade is not");
            // Not a Battle action, not this unit, bowed Blade: nothing opens.
            var s3 = Rig(out var w3, out var b3, out var q3, out _, out _, out _, Phase.Action); s3.ActivePlayerId = foe; s3.PriorityPlayerId = foe;
            var opener = Add(s3, "training-bow-any-source", foe, Zone.Home);
            var e3 = MatchEngine.FromTestState(s3);
            Accept(e3, Offer(e3, foe, "training-bow-any", opener.Id, w3.Id));
            Check(Window(e3) != "beforeActionReaction" && e3.State.Cards[w3.Id].Bowed, "an Open action is not a Battle action: it resolves at once");
            var s4 = Rig(out _, out _, out var q4, out _, out var bower4, out _); var e4 = MatchEngine.FromTestState(s4);
            Accept(e4, Offer(e4, foe, "training-bow-at-battle", bower4.Id, q4.Id));
            Check(Window(e4) != "beforeActionReaction" && e4.State.Cards[q4.Id].Bowed, "a card outside the Blade's unit does not open it");
            var s5 = Rig(out var w5, out var b5, out _, out _, out var bower5, out _); b5.Bowed = true; var e5 = MatchEngine.FromTestState(s5);
            Accept(e5, Offer(e5, foe, "training-bow-at-battle", bower5.Id, w5.Id));
            Check(Window(e5) != "beforeActionReaction" && e5.State.Cards[w5.Id].Bowed, "a bowed Blade cannot be used");
            // Never opened.
            var s6 = Rig(out _, out var b6, out _, out _, out _, out _); var e6 = MatchEngine.FromTestState(s6);
            NeverOpened(e6, owner, ability, b6.Id, command with { CardId = b6.Id });
        }
    }

    // ---- Chrysanthemum Blossom: after this Personality is assigned, +1F token and 1 Honor ------------------------------------------------------------------------------------------------------------
    private static MatchState Maneuvers(Phase phase, int actor)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = phase; s.ActivePlayerId = 0; s.PriorityPlayerId = actor; s.UnresolvedBattlefields = [0, 1, 2, 3];
        return s;
    }

    private static void ChrysanthemumBoost()
    {
        const string ability = "chrysanthemum-blossom-boost";
        var def = Def(ability);
        foreach (var attacker in new[] { true, false })
        {
            // The Attacker assigns in the Infantry Declaration (player 0 attacks); the Defender in the Defense Assignment (player 1). The wearer's controller holds the window.
            var owner = attacker ? 0 : 1;
            var s = Maneuvers(attacker ? Phase.InfantryDeclaration : Phase.DefenseAssignment, owner);
            var host = Add(s, "training-crane-samurai", owner, Zone.Home); var blossom = Add(s, def, owner, Zone.Attached, parent: host.Id);
            var plain = Add(s, "training-crane-veteran", owner, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var assign = new Dictionary<string, int> { [host.Id] = 0, [plain.Id] = 1 };
            Accept(e, new GameCommand(attacker ? "attack" : "assignDefense", owner, e.Version, Assignments: assign));
            Eq(e.State.Cards[host.Id].Zone, Zone.Battlefield, "he was assigned");
            Eq(Window(e), "afterActionResolvedReaction", "after the assignment a Reaction window opens");
            Eq(e.State.PriorityPlayerId, owner, "for the Reaction's holder");
            var command = InWindow(e, owner, ability, blossom.Id, windowType: "afterActionResolvedReaction");
            var force = e.CardForceForTest(host.Id); var honor = Honor(e, owner);
            Accept(e, command);
            Eq(e.CardForceForTest(host.Id), force + 1, "a +1F token");
            Check(e.State.Cards[host.Id].Tokens.Any(t => t.Stat == "Force" && t.Amount == 1), "it is a token");
            Eq(Honor(e, owner), honor + 1, "and 1 Honor");
            Eq(e.CardForceForTest(plain.Id), CardCatalog.Get("training-crane-veteran").Force, "the unequipped Personality gets nothing");
            Check(!CanUse(e, owner, ability, blossom.Id), "once per turn");
            Settle(e);
            Eq(e.State.Phase, attacker ? Phase.DefenseAssignment : e.State.Phase, "the assignment then carries on");
            // Declined: no token, no Honor.
            var d = Maneuvers(attacker ? Phase.InfantryDeclaration : Phase.DefenseAssignment, owner);
            var dh = Add(d, "training-crane-samurai", owner, Zone.Home); Add(d, def, owner, Zone.Attached, parent: dh.Id);
            var ed = MatchEngine.FromTestState(d); var forceD = ed.CardForceForTest(dh.Id); var honorD = Honor(ed, owner);
            Accept(ed, new GameCommand(attacker ? "attack" : "assignDefense", owner, ed.Version, Assignments: new Dictionary<string, int> { [dh.Id] = 2 }));
            Settle(ed);
            Eq(ed.CardForceForTest(dh.Id), forceD, "declined: no token"); Eq(Honor(ed, owner), honorD, "declined: no Honor");
            // A wearer who is not assigned, or an assignment of somebody else, opens nothing.
            var n = Maneuvers(attacker ? Phase.InfantryDeclaration : Phase.DefenseAssignment, owner);
            var loner = Add(n, "training-crane-samurai", owner, Zone.Home); var wearer = Add(n, "training-crane-veteran", owner, Zone.Home); var bl = Add(n, def, owner, Zone.Attached, parent: wearer.Id);
            var en = MatchEngine.FromTestState(n);
            Accept(en, new GameCommand(attacker ? "attack" : "assignDefense", owner, en.Version, Assignments: new Dictionary<string, int> { [loner.Id] = 0 }));
            Check(!CanUse(en, owner, ability, bl.Id) && !CanUse(en, 1 - owner, ability, bl.Id), "assigning another Personality opens nothing for the Blossom");
            // Never opened.
            var q = Maneuvers(Phase.Action, owner); q.ActivePlayerId = owner;
            var qh = Add(q, "training-crane-samurai", owner, Zone.Home); var qb = Add(q, def, owner, Zone.Attached, parent: qh.Id);
            var eq = MatchEngine.FromTestState(q);
            NeverOpened(eq, owner, ability, qb.Id, command with { CardId = qb.Id });
        }
        // A dishonorable wearer is rehonored by it instead of his controller gaining Honor (an Honor gain from a dishonorable Personality's action).
        var dis = Maneuvers(Phase.InfantryDeclaration, 0);
        var sad = Add(dis, "training-crane-samurai", 0, Zone.Home); sad.Dishonorable = true; var db = Add(dis, def, 0, Zone.Attached, parent: sad.Id);
        var edis = MatchEngine.FromTestState(dis); var honorDis = Honor(edis, 0);
        Accept(edis, new GameCommand("attack", 0, edis.Version, Assignments: new Dictionary<string, int> { [sad.Id] = 0 }));
        Accept(edis, Enabled(edis, 0, ability, db.Id).Command!);
        Check(!edis.State.Cards[sad.Id].Dishonorable && Honor(edis, 0) == honorDis, "a dishonorable Personality is rehonored instead");
    }

    // ---- Confusion at Court: before an action resolves whose base costs discard the Imperial Favor, negate its effects ---------------------------------------------------------------------------
    private static void ConfusionNegate()
    {
        const string ability = "confusion-at-court-negate";
        var def = Def(ability);
        var favorDef = Def("lords-favor-straighten");
        foreach (var holder in new[] { 0, 1 })
            foreach (var phase in new[] { Phase.Action, Phase.Battle })
            {
                var performer = 1 - holder;
                CardInstance play = null!, unit = null!, answer = null!;
                MatchEngine Rig(bool favor = true)
                {
                    var s = Table(phase, performer); s.FavorPlayerId = favor ? performer : null; s.PriorityPlayerId = performer;
                    play = Add(s, favorDef, performer, Zone.Hand);
                    unit = Add(s, "training-lion-samurai", performer, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, bowed: true, province: phase == Phase.Battle ? 0 : null);
                    answer = Add(s, def, holder, Zone.Hand);
                    if (phase == Phase.Battle) Add(s, "training-crane-samurai", holder, Zone.Battlefield, province: 0); // presence (issue #1133): a Reaction in a battle needs a unit at the battlefield
                    return MatchEngine.FromTestState(s);
                }
                var label = $"holder {holder} in {phase}";
                var e = Rig();
                Accept(e, Offer(e, performer, "lords-favor-straighten", play.Id, unit.Id));
                Eq(Window(e), "beforeActionReaction", label + ": the other player may react");
                Check(!CanUse(e, performer, ability, answer.Id), label + ": the performer does not hold it");
                var command = InWindow(e, holder, ability, answer.Id, windowType: "beforeActionReaction");
                Accept(e, command);
                Settle(e);
                Check(e.State.Cards[unit.Id].Bowed, label + ": the straightening was negated");
                Eq(e.State.FavorPlayerId, null, label + ": the Imperial Favor was still discarded (a negated action pays its costs)");
                Check(e.State.Cards[answer.Id].Zone != Zone.Hand, label + ": Confusion at Court was spent");
                Check(e.State.Cards[play.Id].Zone != Zone.Hand, label + ": the negated Strategy was still played");
                Check(e.State.Events.Any(x => x.Type == "actionNegated"), label + ": the negation is on the record");
                // Declined: the action resolves.
                var d = Rig();
                Accept(d, Offer(d, performer, "lords-favor-straighten", play.Id, unit.Id)); Settle(d);
                Check(!d.State.Cards[unit.Id].Bowed, label + ": declined, the unit is straightened");
                // An action whose base costs do not discard the Favor opens nothing (the Favor is held, but this Open action does not spend it).
                if (phase == Phase.Action)
                {
                    var s2 = Table(phase, performer); s2.FavorPlayerId = performer; s2.PriorityPlayerId = performer;
                    var bower = Add(s2, "training-bow-any-source", performer, Zone.Home); var victim = Add(s2, "training-crane-samurai", holder, Zone.Home);
                    var held = Add(s2, def, holder, Zone.Hand);
                    var e2 = MatchEngine.FromTestState(s2);
                    Accept(e2, Offer(e2, performer, "training-bow-any", bower.Id, victim.Id));
                    Check(!CanUse(e2, holder, ability, held.Id), label + ": no Favor discard among the costs: not offered");
                    Check(e2.State.PendingChoice is null || e2.State.PendingChoice.Type != "beforeActionReaction" || !CanUse(e2, holder, ability, held.Id), label);
                }
                // Never opened.
                var q = Rig(); NeverOpened(q, holder, ability, answer.Id, command with { CardId = answer.Id });
            }
    }

    // ---- Detained: after engaging, bow your performing Magistrate to move home an enemy dishonorable Personality; bow his unit; his controller loses 2 Honor ----------------------------------------------------
    // The Attacker (the active player) selects battle 0; the Engage window gives the Attacker the first opportunity and then the Defender.
    private static MatchEngine Engage(int attacker, Action<MatchState> arrange, bool expectWindow = true)
    {
        var t = Table(Phase.BattleSelection, attacker); t.PriorityPlayerId = attacker; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        arrange(t);
        var e = MatchEngine.FromTestState(t);
        Accept(e, new GameCommand("selectBattle", attacker, e.Version, Province: 0));
        if (expectWindow) Eq(Window(e), "engageReaction", "the Engage window is open");
        return e;
    }

    private static void DetainedMove()
    {
        const string ability = "detained-move";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownerAttacks in new[] { true, false })
            {
                var foe = 1 - owner; var attacker = ownerAttacks ? owner : foe;
                var label = $"owner {owner} {(ownerAttacks ? "attacking" : "defending")}";
                CardInstance card = null!, mag = null!, sinner = null!, follower = null!, honest = null!;
                var e = Engage(attacker, s =>
                {
                    mag = Kw(Bf(s, "training-crane-samurai", owner), "Magistrate"); sinner = Bf(s, "training-lion-samurai", foe); sinner.Dishonorable = true;
                    follower = Add(s, "training-follower", foe, Zone.Attached, province: 0, parent: sinner.Id);
                    honest = Bf(s, "training-lion-samurai", foe); card = Add(s, def, owner, Zone.Hand);
                });
                if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e)); // the Attacker has the first opportunity
                var offers = Options(e, owner, ability, card.Id, mag.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
                Check(offers.SequenceEqual([sinner.Id]), label + ": only the dishonorable enemy is a target: " + string.Join(",", offers));
                var command = InWindow(e, owner, ability, card.Id, performerId: mag.Id, targetId: sinner.Id, windowType: "engageReaction");
                var theirs = Honor(e, foe); var mine = Honor(e, owner);
                Accept(e, command);
                Eq(e.State.Cards[sinner.Id].Zone, Zone.Home, label + ": he is moved home");
                Check(e.State.Cards[sinner.Id].Bowed && e.State.Cards[follower.Id].Bowed, label + ": his unit is bowed");
                Check(e.State.Cards[mag.Id].Bowed, label + ": the Magistrate bowed as the cost");
                Eq(Honor(e, foe), theirs - 2, label + ": his controller loses 2 Honor");
                Eq(Honor(e, owner), mine, label + ": and the Magistrate's controller loses nothing");
                Eq(e.State.Cards[honest.Id].Zone, Zone.Battlefield, label + ": the honorable enemy is untouched");
                Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, label + ": the Strategy was played");
                // The performer must be an unbowed Magistrate; the target must be dishonorable and at the battlefield.
                var n = Engage(attacker, s =>
                {
                    Bf(s, "training-crane-samurai", owner); Kw(Bf(s, "training-crane-veteran", owner, bowed: true), "Magistrate");
                    var sn = Bf(s, "training-lion-samurai", foe); sn.Dishonorable = true; card = Add(s, def, owner, Zone.Hand);
                }, expectWindow: false);
                Check(Window(n) != "engageReaction" && !CanUse(n, owner, ability, card.Id), label + ": no unbowed Magistrate: no window, not offered");
                // Out of the Engage window (the first Battle action) it is not offered at all.
                var m = Engage(attacker, s =>
                {
                    mag = Kw(Bf(s, "training-crane-samurai", owner), "Magistrate"); sinner = Bf(s, "training-lion-samurai", foe); sinner.Dishonorable = true; card = Add(s, def, owner, Zone.Hand);
                });
                var late = m.Clone(); Settle(late);
                Check(late.State.PendingChoice is null && !CanUse(late, owner, ability, card.Id), label + ": after the Engage window it is gone");
                Rejection(late, command with { CardId = card.Id, ExpectedVersion = late.Version });
            }
    }

    // ---- Flesh Cutter Arrows: after a Ranged Attack performed by this Personality targets a card without attached Armor, bow this card: +2 strength -------------------------------------------------------------
    private static void FleshCutterArrows()
    {
        const string ability = "flesh-cutter-arrows-reaction";
        var def = Def(ability);
        var shikaro = Def("shikaro-attack");
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            (MatchEngine E, CardInstance Arrows, CardInstance Target) Rig(bool armor = false, bool attackWithOther = false, bool attack = true, int targetForce = 5)
            {
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = owner;
                var host = Bf(s, shikaro, owner); // Ranged 4
                var arrows = Add(s, def, owner, Zone.Attached, province: 0, parent: host.Id);
                var other = Bf(s, "training-dual-attacker", owner); // a second Ranged 3 attacker without the Arrows
                Add(s, "training-attack-reactor", foe, Zone.Home); // the opponent still holds a Reaction, so the window stays open to read the strength
                var target = Bf(s, "training-crane-samurai", foe);
                if (armor) Kw(Add(s, "training-weapon", foe, Zone.Attached, province: 0, parent: target.Id), "Armor");
                var probe = MatchEngine.FromTestState(s);
                Batch336Support.Force(target, targetForce - probe.CardForceForTest(target.Id));
                var e = MatchEngine.FromTestState(s);
                if (attack) Accept(e, attackWithOther ? Use(e, owner, "training-honor-cost-attack", other.Id, target.Id) : Use(e, owner, "shikaro-attack", host.Id, target.Id));
                return (e, arrows, target);
            }
            // Without the Reaction a Ranged 4 does not kill a Force-5 target; with +2 (strength 6) it does.
            var control = Rig(); Settle(control.E);
            Check(control.E.State.Cards[control.Target.Id].Zone != Zone.Dead, $"owner {owner}: Ranged 4 spares the Force 5 target");
            var rig = Rig();
            Eq(rig.E.State.PendingAttack?.Strength, 4, "Shikaro's Ranged 4 Attack is pending");
            var command = InWindow(rig.E, owner, ability, rig.Arrows.Id, windowType: "attackReaction");
            Accept(rig.E, command);
            Check(rig.E.State.Cards[rig.Arrows.Id].Bowed, "bowed as the cost");
            Eq(rig.E.State.PendingAttack?.Strength, 6, "4 base + 2 from the Arrows");
            Settle(rig.E);
            Eq(rig.E.State.Cards[rig.Target.Id].Zone, Zone.Dead, $"owner {owner}: strength 6 destroys the Force 5 target");
            // Armor on the target: not offered. Another Personality's Ranged Attack: not offered. No Attack yet: not offered.
            var armored = Rig(armor: true);
            Check(!CanUse(armored.E, owner, ability, armored.Arrows.Id), "a target with attached Armor: not offered");
            var other = Rig(attackWithOther: true);
            Eq(other.E.State.PendingAttack?.Kind, "Ranged");
            Check(!CanUse(other.E, owner, ability, other.Arrows.Id), "an Attack from another Personality: not offered");
            var quiet = Rig(attack: false);
            NeverOpened(quiet.E, owner, ability, quiet.Arrows.Id, command with { CardId = quiet.Arrows.Id });
        }
    }

    // ---- Grateful Reward: after engaging, choose your performing Samurai at the battlefield: create a 1F Ashigaru Cavalry Follower on him; a Paragon gains 2 Honor and draws ----------------------
    private static void GratefulRewardCreate()
    {
        const string ability = "grateful-reward-create";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownerAttacks in new[] { true, false })
                foreach (var paragon in new[] { true, false })
                {
                    var foe = 1 - owner; var attacker = ownerAttacks ? owner : foe;
                    var label = $"owner {owner} {(ownerAttacks ? "attacking" : "defending")} {(paragon ? "Paragon" : "plain Samurai")}";
                    CardInstance card = null!, samurai = null!, notSamurai = null!;
                    var e = Engage(attacker, s =>
                    {
                        samurai = Kw(Bf(s, "training-crane-samurai", owner), "Samurai"); if (paragon) Kw(samurai, "Paragon");
                        notSamurai = Bf(s, "training-shugenja", owner); Bf(s, "training-lion-samurai", foe); card = Add(s, def, owner, Zone.Hand);
                        Kw(Add(s, "training-crane-veteran", owner, Zone.Home), "Samurai"); // a Samurai at home is not "at the current battlefield"
                        Add(s, "training-focus-strategy", owner, Zone.FateDeck);
                    });
                    if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e));
                    var performers = Options(e, owner, ability, card.Id).Where(o => o.Enabled).Select(o => o.Command!.PerformerId).ToArray();
                    Check(performers.SequenceEqual([samurai.Id]), label + ": only the performing Samurai may be chosen: " + string.Join(",", performers));
                    var command = InWindow(e, owner, ability, card.Id, performerId: samurai.Id, windowType: "engageReaction");
                    var honor = Honor(e, owner); var hand = e.State.Players[owner].Hand.Count; var followers = e.State.Cards.Values.Count(c => c.Zone == Zone.Attached && c.AttachedTo == samurai.Id);
                    Accept(e, command);
                    var made = e.State.Cards.Values.Where(c => c.Zone == Zone.Attached && c.AttachedTo == samurai.Id).ToArray();
                    Eq(made.Length, followers + 1, label + ": one Follower attached to him");
                    var follower = made.Single();
                    Check(CardCatalog.Get(follower.DefinitionId).Type == CardType.Follower && follower.OwnerId == owner, label + ": a Follower of his controller's");
                    Eq(e.CardForceForTest(follower.Id), 1, label + ": 1F");
                    Check(e.HasKeyword(follower.Id, "Ashigaru") && e.HasKeyword(follower.Id, "Cavalry"), label + ": Ashigaru and Cavalry");
                    Eq(Honor(e, owner), honor + (paragon ? 2 : 0), label + (paragon ? ": a Paragon gains 2 Honor" : ": no Honor for a non-Paragon"));
                    Eq(e.State.Players[owner].Hand.Count, hand - 1 + (paragon ? 1 : 0), label + (paragon ? ": the Strategy left the hand and a card was drawn" : ": no draw"));
                    Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, label + ": the Strategy was played");
                    Eq(e.State.Cards[notSamurai.Id].Zone, Zone.Battlefield);
                }
        // No Samurai performing at the battlefield (only a Samurai at home / a non-Samurai at the battlefield): not offered, even with the window open for someone else.
        {
            CardInstance card = null!;
            var e = Engage(0, s =>
            {
                Bf(s, "training-shugenja", 0); Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Samurai"); card = Add(s, def, 0, Zone.Hand);
                Bf(s, "training-lion-samurai", 1); Add(s, "training-attack-reactor", 1, Zone.Home);
            }, expectWindow: false);
            Check(!CanUse(e, 0, ability, card.Id), "no Samurai at the battlefield: not offered");
            Rejection(e, Use(e, 0, ability, card.Id));
        }
    }

    // ---- Hero's Memorial: after another player's action destroyed one of your Personalities, bow this card: gain 2 Honor ----------------------------------------------------------------------
    private static void HerosMemorialHonor()
    {
        const string ability = "wl-heros-memorial-honor";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            // kind: "personality" = the enemy's action destroys one of the owner's Personalities; "follower" = it destroys a Follower of his instead; "own" = the owner's action destroys an enemy Personality.
            MatchEngine Rig(string kind, out CardInstance memorial, out int honorBefore)
            {
                var actor = kind == "own" ? owner : foe;
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = actor;
                var destroyer = Bf(s, "training-no-attachments-destroyer", actor);
                var mine = Bf(s, "training-crane-samurai", owner); var theirs = Bf(s, "training-lion-samurai", foe);
                var follower = kind == "follower" ? Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: mine.Id) : null;
                memorial = Add(s, def, owner, Zone.Home);
                var e = MatchEngine.FromTestState(s);
                honorBefore = Honor(e, owner);
                var victim = kind switch { "personality" => mine, "follower" => follower!, _ => theirs };
                Accept(e, Offer(e, actor, "training-destroy-no-attachments", destroyer.Id, victim.Id));
                Check(e.State.Cards[victim.Id].Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard, "the victim was destroyed");
                return e;
            }
            var w = Rig("personality", out var memorial, out var honor);
            Eq(Window(w), "afterActionResolvedReaction", $"owner {owner}: another player's action destroyed one of his Personalities");
            var command = InWindow(w, owner, ability, memorial.Id, windowType: "afterActionResolvedReaction");
            Accept(w, command);
            Eq(Honor(w, owner), honor + 2, $"owner {owner}: gain 2 Honor");
            Check(w.State.Cards[memorial.Id].Bowed, "bowing is the cost");
            Check(!CanUse(w, owner, ability, memorial.Id), "spent");
            // Declined: no Honor.
            var d = Rig("personality", out _, out _); Settle(d);
            Eq(Honor(d, owner), honor, "declined: no Honor");
            // Another player's action that destroyed none of his, and his own action that destroyed one of the enemy's: nothing to react to.
            var none = Rig("follower", out var m2, out _);
            Check(!CanUse(none, owner, ability, m2.Id), "the destroyed card was not a Personality of his");
            var own = Rig("own", out var m3, out _);
            Check(!CanUse(own, owner, ability, m3.Id), "his own action is not another player's");
            // A bowed Memorial holds no Reaction.
            var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = foe;
            var dd = Bf(s4, "training-no-attachments-destroyer", foe); var vm = Bf(s4, "training-crane-samurai", owner); var bm = Add(s4, def, owner, Zone.Home, bowed: true);
            var e4 = MatchEngine.FromTestState(s4);
            Accept(e4, Offer(e4, foe, "training-destroy-no-attachments", dd.Id, vm.Id));
            Check(!CanUse(e4, owner, ability, bm.Id), "a bowed Memorial cannot be bowed again");
            // Never opened.
            var q = Table(Phase.Action, 0); var qm = Add(q, def, 0, Zone.Home); var qe = MatchEngine.FromTestState(q);
            NeverOpened(qe, 0, ability, qm.Id, command with { CardId = qm.Id });
        }
    }

    // ---- Hida Takeuchi: after the resolution of an action that attached a Follower from your hand to Takeuchi, draw a card ----------------------------------------------------------------------
    private static void TakeuchiDraw()
    {
        const string ability = "takeuchi-draw";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            GameCommand Equip(MatchEngine e, CardInstance card, CardInstance host) =>
                new("equip", owner, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(owner, CardCatalog.Get(card.DefinitionId).GoldCost).FirstOrDefault()?.SourceIds ?? [], TargetId: host.Id);
            MatchEngine Rig(out CardInstance takeuchi, out CardInstance other, out CardInstance follower, out CardInstance weapon)
            {
                var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner;
                takeuchi = Add(s, def, owner, Zone.Home); other = Add(s, "training-crane-samurai", owner, Zone.Home);
                follower = Add(s, "training-follower", owner, Zone.Hand); weapon = Add(s, "training-weapon", owner, Zone.Hand);
                Add(s, "training-focus-strategy", owner, Zone.FateDeck);
                return MatchEngine.FromTestState(s);
            }
            var e = Rig(out var t, out var o, out var f, out var w);
            var hand = e.State.Players[owner].Hand.Count;
            Accept(e, Equip(e, f, t));
            Eq(Window(e), "afterActionResolvedReaction", $"owner {owner}: the after-action window opens for the holder of the Reaction");
            var command = InWindow(e, owner, ability, t.Id, windowType: "afterActionResolvedReaction");
            Accept(e, command);
            Eq(e.State.Players[owner].Hand.Count, hand - 1 + 1, "the Follower left the hand and one card was drawn");
            Settle(e);
            // Declined: no draw.
            var d = Rig(out var t2, out _, out var f2, out _);
            Accept(d, Equip(d, f2, t2)); Settle(d);
            Eq(d.State.Players[owner].Hand.Count, hand - 1, "declined: no draw");
            // Another host and a Weapon on Takeuchi open nothing.
            var oe = Rig(out var t3, out var h3, out var f3, out var w3);
            Accept(oe, Equip(oe, f3, h3));
            Check(Window(oe) != "afterActionResolvedReaction" || !CanUse(oe, owner, ability, t3.Id), "a Follower attached to another Personality opens nothing");
            var we = Rig(out var t4, out _, out _, out var w4);
            Accept(we, Equip(we, w4, t4));
            Check(Window(we) != "afterActionResolvedReaction" || !CanUse(we, owner, ability, t4.Id), "a Weapon attached to Takeuchi opens nothing");
            // Never opened.
            var q = Rig(out var t5, out _, out _, out _);
            NeverOpened(q, owner, ability, t5.Id, command with { CardId = t5.Id });
        }
    }

    // ---- Introspection: after one of your Rings enters play, search your Fate deck for a card and put it in your hand ----------------------------------------------------------------------------
    private static void IntrospectionSearch()
    {
        const string ability = "introspection-search";
        var def = Def(ability);
        var falseFire = Def(WlSpellsRingsAbilities.FalseFireRecruit);
        foreach (var scenario in new[] { "battle-attacker", "battle-defender", "action-active", "action-other" })
            foreach (var ringOwner in new[] { true, false })
            {
                var battle = scenario.StartsWith("battle");
                var me = scenario is "battle-defender" or "action-active" ? 0 : 1; // whoever plays the Ring (Open: either player; Battle: either side)
                var holder = ringOwner ? me : 1 - me;
                var label = $"{scenario} ring owner {me} holder {holder}";
                var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0); s.PriorityPlayerId = me;
                CardInstance ring, strategy;
                if (battle)
                {
                    s.Players[1 - s.ActivePlayerId].Provinces[0].Strength = 12;
                    Add(s, "training-crane-samurai", me, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1 - me, Zone.Battlefield, province: 0);
                    ring = Add(s, "training-false-ring-of-water", me, Zone.Hand);
                }
                else { ring = Add(s, falseFire, me, Zone.Hand); s.Players[me].MaxHandBonus = 2; }
                strategy = Add(s, def, holder, Zone.Hand);
                var top = Add(s, "training-lion-samurai", holder, Zone.FateDeck); s.Players[holder].Hand.Remove(top.Id); s.Players[holder].FateDeck.Insert(0, top.Id);
                var e = MatchEngine.FromTestState(s);
                Check(!CanUse(e, holder, ability, strategy.Id), label + ": no Ring has entered play yet");
                Accept(e, Enabled(e, me, battle ? "false-ring-of-water-recruit" : WlSpellsRingsAbilities.FalseFireRecruit, ring.Id).Command!);
                Eq(e.State.Cards[ring.Id].Zone, Zone.Home, label + ": the Ring is in play");
                if (!ringOwner)
                {
                    Check(!CanUse(e, holder, ability, strategy.Id), label + ": an enemy Ring gives it nothing to react to");
                    continue;
                }
                Eq(Window(e), "afterActionResolvedReaction", label + ": the window opens");
                var command = InWindow(e, holder, ability, strategy.Id, windowType: "afterActionResolvedReaction");
                var handBefore = e.State.Players[holder].Hand.Count;
                Accept(e, command);
                Eq(Window(e), "searchSelect", label + ": the Fate deck search opens");
                Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, label + ": the Strategy was spent");
                var entries = e.GetView(holder).PendingSearch!.Entries!;
                Check(entries.Length >= 1 && entries.All(x => x.Eligible), label + ": any card qualifies");
                Eq(e.GetView(1 - holder).PendingSearch, null, label + ": the other seat sees no search");
                Accept(e, new GameCommand("selectSearchCard", holder, e.Version, Choice: "training-lion-samurai"));
                Check(e.State.Players[holder].Hand.Contains(top.Id) || e.State.Players[holder].Hand.Any(id => e.State.Cards[id].DefinitionId == "training-lion-samurai"), label + ": the card was put in the hand");
                Eq(e.State.Players[holder].Hand.Count, handBefore - 1 + 1, label + ": the Strategy left the hand, the found card entered it");
                Check(e.State.PendingSearch is null, label + ": the search is over");
            }
        // Never opened.
        var q = Table(Phase.Action, 0); var held = Add(q, def, 0, Zone.Hand); var qe = MatchEngine.FromTestState(q);
        NeverOpened(qe, 0, ability, held.Id, Use(qe, 0, ability, held.Id));
    }

    // ---- Iuchi Kota: after an action targets one of your Personalities at Kota's battlefield, negate the Personality's movement ------------------------------------------------------------------
    private static void KotaNegateMove()
    {
        const string ability = "kota-negate-move";
        var def = Def(ability);
        var masafuni = Def("masafuni-move");
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            MatchEngine Rig(out CardInstance kota, out CardInstance ally, out CardInstance mover, int kotaProvince = 0)
            {
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = foe;
                kota = Add(s, def, owner, Zone.Battlefield, province: kotaProvince);
                ally = Bf(s, "training-crane-samurai", owner);
                mover = Bf(s, masafuni, foe);
                return MatchEngine.FromTestState(s);
            }
            var e = Rig(out var kota, out var ally, out var mover);
            Accept(e, Offer(e, foe, "masafuni-move", mover.Id, ally.Id));
            Eq(Window(e), "beforeActionReaction", $"owner {owner}: the action is held");
            var command = InWindow(e, owner, ability, kota.Id, windowType: "beforeActionReaction");
            Accept(e, command);
            Settle(e);
            Eq(e.State.Cards[ally.Id].Zone, Zone.Battlefield, "his movement was negated: he is still at the battlefield");
            Check(!CanUse(e, owner, ability, kota.Id) || e.State.PendingChoice is null, "spent or closed");
            // Control: without the Reaction the same action moves him home.
            var c = Rig(out _, out var ally2, out var mover2);
            Accept(c, Offer(c, foe, "masafuni-move", mover2.Id, ally2.Id)); Settle(c);
            Eq(c.State.Cards[ally2.Id].Zone, Zone.Home, "declined: he goes home");
            // Kota at another battlefield: not offered.
            var elsewhere = Rig(out var k3, out var ally4, out var mover4, kotaProvince: 1);
            Accept(elsewhere, Offer(elsewhere, foe, "masafuni-move", mover4.Id, ally4.Id));
            Check(!CanUse(elsewhere, owner, ability, k3.Id), "Kota answers for a Personality at his battlefield only");
            // An action that targets him but does not move him: not offered.
            var bs = Table(Phase.Battle, 1); bs.PriorityPlayerId = foe;
            var kb = Add(bs, def, owner, Zone.Battlefield, province: 0); var allyB = Bf(bs, "training-crane-samurai", owner); var bowerB = Bf(bs, "training-battle-bower", foe);
            var be = MatchEngine.FromTestState(bs);
            Accept(be, Offer(be, foe, "training-bow-at-battle", bowerB.Id, allyB.Id));
            Check(!CanUse(be, owner, ability, kb.Id), "a bow is not a movement: not offered");
            // Never opened.
            var q = Rig(out var k5, out _, out _);
            NeverOpened(q, owner, ability, k5.Id, command with { CardId = k5.Id });
        }
    }

    // ---- Kakita Kenta: after an action resolves during which Kenta won a duel, move a dishonorable enemy Personality at the battlefield home -------------------------------------------------------
    private static MatchEngine KentaDuel(int owner, bool kentaChallenges, bool kentaWins, bool dishonorable, out CardInstance kenta, out CardInstance victim)
    {
        var foe = 1 - owner;
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = kentaChallenges ? owner : foe;
        kenta = Add(s, Def("kenta-move-home"), owner, Zone.Battlefield, province: 0); // Force 2
        var rival = Add(s, "training-lion-samurai", foe, Zone.Battlefield, province: 0); // Force 4
        var challengerOwner = kentaChallenges ? owner : foe;
        if (kentaWins) Batch336Support.Force(rival, -3); else Batch336Support.Force(kenta, -2);
        victim = Add(s, "training-lion-courtier", foe, Zone.Battlefield, province: 0); victim.Dishonorable = dishonorable;
        var challenge = Add(s, "training-duel-force", challengerOwner, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        if (kentaChallenges) Accept(e, Offer(e, owner, "training-duel-force", challenge.Id, rival.Id, kenta.Id));
        else Accept(e, Offer(e, foe, "training-duel-force", challenge.Id, kenta.Id, rival.Id));
        Accept(e, new GameCommand("strike", e.State.PendingDuel!.TurnPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is not null && guard < 8; guard++)
        {
            if (e.State.PendingDuel is { Stage: "DuelistRefocus" }) { Accept(e, new GameCommand("duelistRefocus", owner, e.Version)); continue; }
            var step = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is not "concede");
            if (step is null) break;
            Accept(e, step.Command!);
        }
        return e;
    }

    private static void KentaMoveHome()
    {
        const string ability = "kenta-move-home";
        foreach (var owner in new[] { 0, 1 })
            foreach (var kentaChallenges in new[] { true, false })
            {
                var label = $"owner {owner} {(kentaChallenges ? "challenging" : "challenged")}";
                var e = KentaDuel(owner, kentaChallenges, true, true, out var kenta, out var victim);
                Check(e.GetView(owner).LastDuel!.Result!.WinnerId == kenta.Id, label + ": Kenta won the duel");
                Eq(Window(e), "afterActionResolvedReaction", label + ": the duel's action resolved: the after-action window");
                var command = InWindow(e, owner, ability, kenta.Id, targetId: victim.Id, windowType: "afterActionResolvedReaction");
                Accept(e, command);
                Eq(e.State.Cards[victim.Id].Zone, Zone.Home, label + ": the dishonorable enemy moved home");
                Check(!CanUse(e, owner, ability, kenta.Id), label + ": once");
                // A lost duel, and an honorable enemy, offer nothing.
                var lost = KentaDuel(owner, kentaChallenges, false, true, out var k2, out _);
                Check(lost.GetView(owner).LastDuel!.Result!.WinnerId != k2.Id, label + ": Kenta lost the duel");
                Check(Window(lost) != "afterActionResolvedReaction" || !CanUse(lost, owner, ability, k2.Id), label + ": a lost duel offers nothing");
                var honorable = KentaDuel(owner, kentaChallenges, true, false, out var k3, out var v3);
                Check(!Options(honorable, owner, ability, k3.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(v3.Id) ?? false)), label + ": an honorable enemy is not a target");
                Eq(honorable.State.Cards[v3.Id].Zone, Zone.Battlefield, label + ": and stays");
            }
    }

    // ---- Kimono of the Turquoise Champion: before an action resolves, bow it: negate any Honor losses until the action ends ---------------------------------------------------------------------------
    private static void KimonoNegateLosses()
    {
        const string ability = "pe-kimono-negate-losses";
        var def = Def(ability);
        var poison = Def("weak-poison-force-zero");
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownAction in new[] { false, true })
            {
                var foe = 1 - owner; var actor = ownAction ? owner : foe;
                var label = $"owner {owner}, {(ownAction ? "his own" : "the enemy's")} action";
                MatchEngine Rig(out CardInstance kimono, out CardInstance victim, bool bowed = false, bool cardIsWeakPoison = true)
                {
                    var s = Table(Phase.Action, actor); s.PriorityPlayerId = actor;
                    var card = Add(s, poison, actor, Zone.Hand); victim = Add(s, "training-lion-samurai", 1 - actor, Zone.Home);
                    var wearer = Add(s, "training-lion-samurai", owner, Zone.Home); kimono = Add(s, def, owner, Zone.Attached, bowed: bowed, parent: wearer.Id);
                    var e = MatchEngine.FromTestState(s);
                    Accept(e, Offer(e, actor, "weak-poison-force-zero", card.Id, victim.Id));
                    return e;
                }
                var honorBefore = Honor(MatchEngine.FromTestState(Table(Phase.Action, actor)), actor);
                var e = Rig(out var kimono, out var victim);
                Eq(Window(e), "honorChangeReaction", label + ": the window opens before the Honor loss");
                var command = InWindow(e, owner, ability, kimono.Id, windowType: "honorChangeReaction");
                Accept(e, command); Settle(e);
                Eq(Honor(e, actor), honorBefore, label + ": the Honor loss was negated");
                Check(e.State.Cards[kimono.Id].Bowed, label + ": the Kimono bowed as the cost");
                Check(e.State.Cards[victim.Id].Modifiers.Sum(m => m.Amount) < 0, label + ": the rest of the action stands");
                // Declined: the performer loses the 2 Honor.
                var d = Rig(out _, out _); Settle(d);
                Eq(Honor(d, actor), honorBefore - 2, label + ": declined, the loss stands");
                // A bowed Kimono holds no Reaction: no window at all.
                var b = Rig(out var bk, out _, bowed: true);
                Check(Window(b) != "honorChangeReaction" && !CanUse(b, owner, ability, bk.Id), label + ": a bowed Kimono opens no window");
                Eq(Honor(b, actor), honorBefore - 2, label + ": and the loss stands");
                // Never opened.
                var q = Table(Phase.Action, owner); var qh = Add(q, "training-lion-samurai", owner, Zone.Home); var qk = Add(q, def, owner, Zone.Attached, parent: qh.Id);
                var qe = MatchEngine.FromTestState(q);
                NeverOpened(qe, owner, ability, qk.Id, command with { CardId = qk.Id });
            }
    }

    // ---- entering-play Reactions ("After X enters play") ---------------------------------------------------------------------------------------------------------------------------------------
    // A Personality bought from `owner`'s province 0 in his own Dynasty Phase, leaving its entering-play window open. Player 1 is made the Dynasty player for his seat.
    private static (MatchEngine E, string Id) Buy(int owner, string def, Action<MatchState>? tweak = null, int gold = 14)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.ActivePlayerId = owner; s.PriorityPlayerId = owner;
        s.Players[owner].Honor = 30;
        Gold(s, owner, gold);
        var entering = s.Cards[s.Players[owner].Provinces[0].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Check(e.State.Phase == Phase.Dynasty, "the Dynasty phase");
        var purchases = e.QueryActions(owner).Where(a => a.Type == "purchase" && a.CardId == entering.Id).ToArray();
        var option = purchases.FirstOrDefault(a => a.Enabled && a.Command is { ClanDiscount: false }) ?? purchases.FirstOrDefault(a => a.Enabled);
        Check(option is not null, $"{def} is not purchasable: " + string.Join("; ", purchases.Select(a => $"[{a.Enabled}] {a.Reason}")));
        Accept(e, option!.Command!);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"{def} bought, but the window is {e.State.PendingChoice?.Type}");
        return (e, entering.Id);
    }

    // The same Personality standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, int owner, out CardInstance card, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner;
        card = Add(s, def, owner, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    private static void AyumiEntry()
    {
        const string ability = "ayumi-entry";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var (e, id) = Buy(owner, def);
            var honor = Honor(e, owner);
            var command = InWindow(e, owner, ability, id);
            Accept(e, command);
            Eq(Honor(e, owner), honor - 2, $"owner {owner}: lose 2 Honor, once");
            Check(!CanUse(e, owner, ability, id), "taken once for this entry");
            var h = AtHome(def, owner, out var ayumi);
            NeverOpened(h, owner, ability, ayumi.Id, command with { CardId = ayumi.Id });
        }
    }

    private static void DenheiCreate()
    {
        const string ability = "denhei-create";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var (e, id) = Buy(owner, def);
            var command = InWindow(e, owner, ability, id);
            var before = e.State.Cards.Keys.ToHashSet(); var honor = Honor(e, owner);
            Accept(e, command);
            var snake = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
            var created = CardCatalog.Get(snake.DefinitionId);
            Check(created.Type == CardType.Personality && created.Force == 2 && created.Chi == 2 && created.PersonalHonor == 3 && new HashSet<string>(created.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Snake", "Spirit"]), "the printed 2F/2C/3PH Snake");
            Check(snake.OwnerId == owner && snake.Zone == Zone.Home, "created at home, under its creator's control");
            Eq(Honor(e, owner), honor, "no Honor change");
            Check(!CanUse(e, owner, ability, id), "used once");
            // The Snake's own Open action: a bitten Personality's base abilities may not be used (offered in either player's Action phase).
            var dainagonDef = Def("dainagon-exp-favor");
            foreach (var active in new[] { owner, 1 - owner })
            {
                var s = e.State; s.Phase = Phase.Action; s.ActivePlayerId = active; s.PriorityPlayerId = owner; s.PendingChoice = null;
                var victim = Add(s, dainagonDef, owner, Zone.Home);
                var f = MatchEngine.FromTestState(s);
                Check(CanUse(f, owner, "dainagon-exp-favor", victim.Id), $"owner {owner}, active {active}: before the bite his printed Open action is available");
                Accept(f, Offer(f, owner, "denhei-snake-bite", snake.Id, victim.Id));
                Accept(f, new GameCommand("pass", 1 - owner, f.Version));
                Check(!CanUse(f, owner, "dainagon-exp-favor", victim.Id), $"owner {owner}, active {active}: his base abilities may not be used");
                Check(!CanUse(f, owner, "denhei-snake-bite", snake.Id), "the Snake's Open action is once a turn");
            }
            var h = AtHome(def, owner, out var denhei);
            NeverOpened(h, owner, ability, denhei.Id, command with { CardId = denhei.Id });
        }
    }

    private static void OhsukiCreate()
    {
        const string ability = "ohsuki-create";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var (e, id) = Buy(owner, def, s => s.Players[owner].Clan = "Mantis"); // a Loyal Personality
            var command = InWindow(e, owner, ability, id);
            var before = e.State.Cards.Keys.ToHashSet();
            Accept(e, command);
            var sparrow = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
            var created = CardCatalog.Get(sparrow.DefinitionId);
            Check(created.Type == CardType.Personality && created.Force == 0 && created.Chi == 2 && created.PersonalHonor == 3 && new HashSet<string>(created.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Sparrow", "Cavalry", "Spirit"]), "the printed 0F/2C/3PH Sparrow");
            Check(sparrow.OwnerId == owner && sparrow.Zone == Zone.Home, "created at home, under its creator's control");
            Check(!CanUse(e, owner, ability, id), "used once");
            // Its Limited action: gain 3 Honor if none of your provinces were destroyed since your last turn ended -- the active player only; refused after a destroyed province.
            foreach (var active in new[] { owner, 1 - owner })
                foreach (var destroyed in new[] { false, true })
                {
                    var s = e.State; s.Phase = Phase.Action; s.ActivePlayerId = active; s.PriorityPlayerId = owner; s.PendingChoice = null;
                    s.ProvinceDestroyedSinceTurnEnd[owner] = destroyed;
                    var f = MatchEngine.FromTestState(s);
                    var honor = Honor(f, owner);
                    var label = $"owner {owner}, active {active}, province destroyed {destroyed}";
                    if (active == owner && !destroyed)
                    {
                        Accept(f, Enabled(f, owner, "ohsuki-sparrow-honor", sparrow.Id).Command!);
                        Eq(Honor(f, owner), honor + 3, label + ": gain 3 Honor");
                    }
                    else
                    {
                        Check(!CanUse(f, owner, "ohsuki-sparrow-honor", sparrow.Id), label + ": not offered");
                        Rejection(f, Use(f, owner, "ohsuki-sparrow-honor", sparrow.Id));
                        Eq(Honor(f, owner), honor, label + ": unchanged");
                    }
                }
            var h = AtHome(def, owner, out var ohsuki);
            NeverOpened(h, owner, ability, ohsuki.Id, command with { CardId = ohsuki.Id });
        }
    }

    private static void MasayoshiCreate()
    {
        const string ability = "masayoshi-create";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var (e, id) = Buy(owner, def);
            var command = InWindow(e, owner, ability, id);
            var force = e.CardForceForTest(id); var chi = e.PersonalityChiForTest(id);
            var before = e.State.Cards.Keys.ToHashSet();
            Accept(e, command);
            var blade = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
            Check(CardCatalog.Get(blade.DefinitionId).Type == CardType.Item && e.HasKeyword(blade.Id, "Weapon"), "a Weapon Item");
            Check(blade.AttachedTo == id && blade.Zone == Zone.Attached && blade.OwnerId == owner, "attached to Masayoshi, under his controller");
            Eq(e.CardForceForTest(id), force + 4, "+4F"); Eq(e.PersonalityChiForTest(id), chi + 1, "+1C");
            Check(!CanUse(e, owner, ability, id), "used once");
            var h = AtHome(def, owner, out var masayoshi);
            NeverOpened(h, owner, ability, masayoshi.Id, command with { CardId = masayoshi.Id });
        }
    }

    private static void ToganinInvest()
    {
        const string ability = "p9-toganin-invest";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var (e, id) = Buy(owner, def, null, 20);
            Check(Options(e, owner, ability, id).Any(o => o.Enabled), "the Invest is offered as he enters play");
            Eq(Options(e, owner, ability, id).First(o => o.Enabled).Gold?.Cost, 5, "for 5 Gold");
            var command = InWindow(e, owner, ability, id);
            var force = e.CardForceForTest(id);
            Accept(e, command with { SourceIds = e.PaymentOptions(owner, 5).First().SourceIds });
            Eq(e.State.Cards[id].Tokens.Count(t => t.Type == "Invest" && t.Stat == "Force" && t.Amount == 1), 3, "three +1F tokens");
            Eq(e.CardForceForTest(id), force + 3, "three more Force");
            Check(!CanUse(e, owner, ability, id), "once");
            // Declined: no tokens, printed Force (+1F while any other player is Crane Clan).
            var (d, declined) = Buy(owner, def, null, 20);
            Accept(d, Decline(d)); Settle(d);
            Eq(d.State.Cards[declined].Tokens.Count, 0, "declined: no tokens");
            Eq(d.CardForceForTest(declined), CardCatalog.Get(def).Force + (d.State.Players[1 - owner].Clan == "Crane" ? 1 : 0), "and his printed Force (+1F if the other player is Crane Clan)");
            var h = AtHome(def, owner, out var toganin);
            NeverOpened(h, owner, ability, toganin.Id, command with { CardId = toganin.Id });
        }
    }

    private static void SpiderEntryHonorLoss()
    {
        const string ability = "training-honor-loss-entry";
        const string def = "training-spider-honor-loss";
        foreach (var owner in new[] { 0, 1 })
        {
            var (e, id) = Buy(owner, def);
            var honor = Honor(e, owner);
            var command = InWindow(e, owner, ability, id);
            Accept(e, command);
            Eq(Honor(e, owner), honor - 2, $"owner {owner}: lose 2 Honor after entering play");
            var h = AtHome(def, owner, out var spider);
            NeverOpened(h, owner, ability, spider.Id, command with { CardId = spider.Id });
        }
    }

    // Satoshi's Dual Warfans: an Item equipped from the hand in the Action phase (the active player); the Reaction draws a card.
    private static void SatoshiWarfansDraw()
    {
        const string ability = "satoshi-warfans-draw";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var s = Table(Phase.Action, owner); s.PriorityPlayerId = owner; s.Players[owner].Clan = "Lion";
            var host = Add(s, "training-crane-samurai", owner, Zone.Home);
            var warfans = Add(s, def, owner, Zone.Hand);
            Gold(s, owner, 4); Add(s, "training-focus-strategy", owner, Zone.FateDeck);
            var e = MatchEngine.FromTestState(s);
            var cost = e.QueryActions(owner).Single(a => a.Type == "equip" && a.CardId == warfans.Id).Gold?.Cost ?? CardCatalog.Get(def).GoldCost;
            Accept(e, new GameCommand("equip", owner, e.Version, CardId: warfans.Id, SourceIds: e.PaymentOptions(owner, cost).First().SourceIds, TargetId: host.Id));
            Eq(Window(e), "entryReaction", $"owner {owner}: the entering window");
            var command = InWindow(e, owner, ability, warfans.Id, windowType: "entryReaction");
            var hand = e.State.Players[owner].Hand.Count;
            Accept(e, command);
            Eq(e.State.Players[owner].Hand.Count, hand + 1, "the card was drawn");
            Check(!CanUse(e, owner, ability, warfans.Id), "used once");
            Eq(e.State.Cards[warfans.Id].AttachedTo, host.Id, "and the Item stays attached");
            // In play and no window (a fresh position): not offered.
            var t = Table(Phase.Action, owner); t.PriorityPlayerId = owner;
            var host2 = Add(t, "training-crane-samurai", owner, Zone.Home); var worn = Add(t, def, owner, Zone.Attached, parent: host2.Id);
            var h = MatchEngine.FromTestState(t);
            NeverOpened(h, owner, ability, worn.Id, command with { CardId = worn.Id });
        }
    }

    // The synthetic Terrain: played by playStrategy as a Battle action, then its "after entering play" Reaction gains 1 Honor.
    private static void TerrainEntryReaction()
    {
        const string ability = "training-terrain-entry-reaction";
        const string def = "training-terrain-entry-reaction";
        foreach (var owner in new[] { 0, 1 })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = owner;
            Bf(s, "training-crane-samurai", 0); Bf(s, "training-lion-samurai", 1);
            var terrain = Add(s, def, owner, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, owner);
            Accept(e, new GameCommand("playStrategy", owner, e.Version, CardId: terrain.Id));
            Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain is in play");
            Eq(Window(e), "entryReaction", $"owner {owner}: the entering window");
            var command = InWindow(e, owner, ability, terrain.Id, windowType: "entryReaction");
            Accept(e, command);
            Eq(Honor(e, owner), honor + 1, "gain 1 Honor");
            Check(e.State.PendingChoice is null, "the window closed: nothing is left to react with");
            Eq(e.State.PriorityPlayerId, 1 - owner, "priority passes to the opponent");
            // Declined: nothing.
            var d = MatchEngine.FromTestState(s); Accept(d, new GameCommand("playStrategy", owner, d.Version, CardId: terrain.Id)); Settle(d);
            Eq(Honor(d, owner), honor, "declined: no Honor");
            // In the Action phase a Terrain is not played (Battle only): refused, nothing changes.
            var a = Table(Phase.Action, owner); a.PriorityPlayerId = owner; var held = Add(a, def, owner, Zone.Hand);
            var ae = MatchEngine.FromTestState(a);
            Rejection(ae, new GameCommand("playStrategy", owner, ae.Version, CardId: held.Id));
            Eq(ae.State.Cards[held.Id].Zone, Zone.Hand, "still in hand");
        }
    }

    // ---- Legion of the Bat: after an action moved a target enemy Infantry Personality to this card's battlefield, bow the Personality -----------------------------------------------------------------
    private static void LegionOfTheBatBow()
    {
        const string ability = "legion-of-bat-bow";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var mover = 1 - owner; // the enemy's own Battle action moves its Personality from home to the current battlefield
            (MatchEngine E, CardInstance Legion, CardInstance Moved, CardInstance Move) Rig(bool cavalry = false, bool legionBowed = false, string? movedKeyword = null)
            {
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = mover;
                var host = Bf(s, "training-crane-samurai", owner);
                var legion = Add(s, def, owner, Zone.Attached, bowed: legionBowed, province: 0, parent: host.Id);
                var source = Bf(s, "training-move-source", mover);
                var moved = Add(s, "training-crane-samurai", mover, Zone.Home);
                if (cavalry) Kw(moved, "Cavalry");
                if (movedKeyword is not null) Kw(moved, movedKeyword);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, mover, "training-move-current", source.Id, moved.Id));
                return (e, legion, moved, source);
            }
            var rig = Rig();
            Eq(rig.E.State.Cards[rig.Moved.Id].Zone, Zone.Battlefield, "the enemy Infantry Personality arrived at the battlefield");
            Eq(Window(rig.E), "moveReaction", $"owner {owner}: the move window");
            var command = InWindow(rig.E, owner, ability, rig.Legion.Id, windowType: "moveReaction");
            Check(!rig.E.State.Cards[rig.Moved.Id].Bowed, "not bowed yet");
            Accept(rig.E, command);
            Check(rig.E.State.Cards[rig.Moved.Id].Bowed, $"owner {owner}: the moved Personality is bowed");
            Check(!CanUse(rig.E, owner, ability, rig.Legion.Id), "used once");
            // Declined: not bowed.
            var d = Rig(); Settle(d.E);
            Check(!d.E.State.Cards[d.Moved.Id].Bowed, "declined: not bowed");
            // A Cavalry Personality is not Infantry; a bowed Follower holds no Reaction.
            var c = Rig(cavalry: true);
            Check(Window(c.E) != "moveReaction" && !CanUse(c.E, owner, ability, c.Legion.Id), "Cavalry: no window");
            // Never opened.
            var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = mover;
            var h2 = Bf(s2, "training-crane-samurai", owner); var l2 = Add(s2, def, owner, Zone.Attached, province: 0, parent: h2.Id);
            NeverOpened(MatchEngine.FromTestState(s2), owner, ability, l2.Id, command with { CardId = l2.Id });
        }
    }

    // ---- Mirumoto Rokai: after another player's action targets your Personality at Rokai's location, destroy Rokai: negate your Personality's destruction -----------------------------------------------------
    private static void RokaiNegateDestruction()
    {
        const string ability = "rokai-negate-destruction";
        var def = Def(ability, "Mirumoto Rokai");
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            (MatchEngine E, CardInstance Rokai, CardInstance Target) Rig(bool rokaiAtHome = false, bool bowOnly = false)
            {
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = foe;
                var attacker = Bf(s, bowOnly ? "training-battle-bower" : "training-no-attachments-destroyer", foe);
                var rokai = rokaiAtHome ? Add(s, def, owner, Zone.Home) : Bf(s, def, owner);
                var target = Bf(s, "training-lion-samurai", owner); // no attachments: a legal destroy target, at the same location as Rokai
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, foe, bowOnly ? "training-bow-at-battle" : "training-destroy-no-attachments", attacker.Id, target.Id));
                return (e, rokai, target);
            }
            var rig = Rig();
            Eq(Window(rig.E), "beforeActionReaction", $"owner {owner}: the action is held");
            Eq(rig.E.State.Cards[rig.Target.Id].Zone, Zone.Battlefield, "not yet destroyed");
            var command = InWindow(rig.E, owner, ability, rig.Rokai.Id, windowType: "beforeActionReaction");
            Accept(rig.E, command);
            Eq(rig.E.State.Cards[rig.Rokai.Id].Zone, Zone.Dead, "Rokai's own cost: destroying himself");
            Settle(rig.E);
            Eq(rig.E.State.Cards[rig.Target.Id].Zone, Zone.Battlefield, "negated: the original destroy never applied");
            // Control: declined, the target is destroyed and Rokai lives.
            var d = Rig(); Settle(d.E);
            Eq(d.E.State.Cards[d.Target.Id].Zone, Zone.Dead, "declined: the Personality is destroyed");
            Eq(d.E.State.Cards[d.Rokai.Id].Zone, Zone.Battlefield, "and Rokai stands");
            // Not at Rokai's location, and an action that does not destroy.
            var far = Rig(rokaiAtHome: true);
            Check(Window(far.E) != "beforeActionReaction" || !CanUse(far.E, owner, ability, far.Rokai.Id), "Rokai at another location: not offered");
            var bow = Rig(bowOnly: true);
            Check(!CanUse(bow.E, owner, ability, bow.Rokai.Id), "a bow is not a destruction: not offered");
            var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = foe; var r5 = Bf(s5, def, owner);
            NeverOpened(MatchEngine.FromTestState(s5), owner, ability, r5.Id, command with { CardId = r5.Id });
        }
    }

    // ---- Ninja Tricks: after an action on a Strategy is announced, bow your performing Ninja at the current battlefield: negate the action's effects, lose 2 Honor ------------------------------------------
    private static void NinjaTricksNegate()
    {
        const string ability = "ninja-tricks-negate";
        var def = Def(ability);
        var ogre = Def("ogre-savagery-bow");
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            (MatchEngine E, CardInstance Ninja, CardInstance Trick, CardInstance Victim, CardInstance Ogre) Rig(bool ninja = true, bool ninjaBowed = false)
            {
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = foe;
                var n = Kw(Bf(s, "training-crane-samurai", owner, bowed: ninjaBowed), ninja ? ["Ninja"] : []);
                var victim = Bf(s, "training-crane-veteran", owner);
                var oni = Bf(s, "training-seeds5-shadowlands-oni", foe);
                var strategy = Add(s, ogre, foe, Zone.Hand); var trick = Add(s, def, owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, foe, "ogre-savagery-bow", strategy.Id, victim.Id, oni.Id));
                return (e, n, trick, victim, strategy);
            }
            var rig = Rig();
            Eq(Window(rig.E), "beforeActionReaction", $"owner {owner}: the window opens for the Strategy's action");
            var command = InWindow(rig.E, owner, ability, rig.Trick.Id, performerId: rig.Ninja.Id, windowType: "beforeActionReaction");
            var honor = Honor(rig.E, owner);
            Accept(rig.E, command);
            Check(rig.E.State.Cards[rig.Ninja.Id].Bowed, "the Ninja was bowed as the cost");
            Eq(Honor(rig.E, owner), honor - 2, "2 Honor was lost");
            Eq(rig.E.State.Cards[rig.Trick.Id].Zone, Zone.FateDiscard, "Ninja Tricks was spent");
            Settle(rig.E);
            Check(!rig.E.State.Cards[rig.Victim.Id].Bowed, "the action's effects were negated: he is not bowed");
            Eq(rig.E.State.Cards[rig.Ogre.Id].Zone, Zone.FateDiscard, "the action was still taken: the Strategy is spent");
            // Declined: the action resolves.
            var d = Rig(); Settle(d.E);
            Check(d.E.State.Cards[d.Victim.Id].Bowed, "declined: the action resolved"); Eq(Honor(d.E, owner), honor, "and no Honor was lost");
            // Without a Ninja, or with the Ninja bowed: no window; the action resolves.
            foreach (var (e, why) in new[] { (Rig(ninja: false), "no Ninja"), (Rig(ninjaBowed: true), "a bowed Ninja") })
            {
                Check(Window(e.E) != "beforeActionReaction" && !CanUse(e.E, owner, ability, e.Trick.Id), why + ": nothing to react with");
                Check(e.E.State.Cards[e.Victim.Id].Bowed, why + ": the action resolved");
            }
            // An action on a card in play is not an action on a Strategy.
            var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = foe;
            Kw(Bf(s3, "training-crane-samurai", owner), "Ninja"); var v3 = Bf(s3, "training-crane-veteran", owner); var t3 = Add(s3, def, owner, Zone.Hand);
            var bower = Bf(s3, "training-battle-bower", foe);
            var e3 = MatchEngine.FromTestState(s3);
            Accept(e3, Offer(e3, foe, "training-bow-at-battle", bower.Id, v3.Id));
            Check(!CanUse(e3, owner, ability, t3.Id), "an action on a card in play is not an action on a Strategy");
            // A Strategy's action in the Action phase is not at the current battlefield.
            var s4 = Table(Phase.Action, foe); s4.PriorityPlayerId = foe;
            Kw(Add(s4, "training-crane-samurai", owner, Zone.Home), "Ninja"); var t4 = Add(s4, def, owner, Zone.Hand);
            var favorer = Add(s4, "training-crane-samurai", foe, Zone.Home); var take = Add(s4, Def("lords-favor-take"), foe, Zone.Hand);
            var e4 = MatchEngine.FromTestState(s4);
            Accept(e4, Use(e4, foe, "lords-favor-take", take.Id, null, favorer.Id));
            Check(!CanUse(e4, owner, ability, t4.Id), "an Action-phase Strategy action: no battlefield, not offered");
            var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = foe; Kw(Bf(s5, "training-crane-samurai", owner), "Ninja"); var t5 = Add(s5, def, owner, Zone.Hand);
            NeverOpened(MatchEngine.FromTestState(s5), owner, ability, t5.Id, command with { CardId = t5.Id });
        }
    }

    // ---- Purify the Soul: before an action resolves, bow it: negate its effects which give Kolat, Ninja or Shadowlands to Personalities you control ------------------------------------------------------
    private static void PurifyTheSoulNegate()
    {
        const string ability = "purify-soul-negate";
        var def = Def(ability);
        var essenceDef = Def("essence-of-undeath-recruit");
        foreach (var owner in new[] { 0, 1 })
        {
            // Essence of Undeath would permanently give the Personality it brings back Shadowlands and Undead.
            (MatchEngine E, CardInstance? Purify, CardInstance Dead) Rig(bool withPurify = true, bool purifyBowed = false)
            {
                var a = Table(Phase.Action, owner); a.PriorityPlayerId = owner;
                var maho = Add(a, "training-shugenja", owner, Zone.Home);
                var essence = Add(a, essenceDef, owner, Zone.Attached, parent: maho.Id);
                CardInstance? purify = null;
                if (withPurify) { var guard = Add(a, "training-seeds5-air-shugenja", owner, Zone.Home); purify = Add(a, def, owner, Zone.Attached, bowed: purifyBowed, parent: guard.Id); }
                var dead = Add(a, "training-crane-samurai", owner, Zone.Dead);
                Gold(a, owner, 3);
                var e = MatchEngine.FromTestState(a);
                Accept(e, Offer(e, owner, "essence-of-undeath-recruit", essence.Id, dead.Id));
                return (e, purify, dead);
            }
            var rig = Rig();
            Eq(Window(rig.E), "beforeActionReaction", $"owner {owner}: a before-action window opened for Purify the Soul");
            var command = InWindow(rig.E, owner, ability, rig.Purify!.Id, windowType: "beforeActionReaction");
            Accept(rig.E, command);
            Check(rig.E.State.Cards[rig.Purify.Id].Bowed, "Purify the Soul bowed as the cost");
            Settle(rig.E);
            var back = rig.E.State.Cards[rig.Dead.Id];
            Eq(back.Zone, Zone.Home, "he came back into play");
            Check(!back.GrantedKeywords.Any(k => k.Keyword == "Shadowlands") && back.GrantedKeywords.Any(k => k.Keyword == "Undead"), "Shadowlands negated, Undead still given");
            // Control: without it Shadowlands is given; with a declined window too.
            var without = Rig(withPurify: false); Settle(without.E);
            Check(without.E.State.Cards[without.Dead.Id].GrantedKeywords.Any(k => k.Keyword == "Shadowlands"), "without it, Shadowlands is given");
            var declined = Rig(); Settle(declined.E);
            Check(declined.E.State.Cards[declined.Dead.Id].GrantedKeywords.Any(k => k.Keyword == "Shadowlands"), "declined: Shadowlands is given");
            // A bowed Purify the Soul holds no Reaction.
            var bowed = Rig(purifyBowed: true);
            Check(Window(bowed.E) != "beforeActionReaction" && !CanUse(bowed.E, owner, ability, bowed.Purify!.Id), "a bowed Spell: no window");
            // An action that gives none of the three keywords opens nothing for it.
            var s2 = Table(Phase.Action, owner); s2.PriorityPlayerId = owner;
            var guard2 = Add(s2, "training-seeds5-air-shugenja", owner, Zone.Home); var p2 = Add(s2, def, owner, Zone.Attached, parent: guard2.Id);
            var bower = Add(s2, "training-bow-any-source", owner, Zone.Home); var victim = Add(s2, "training-lion-samurai", 1 - owner, Zone.Home);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Offer(e2, owner, "training-bow-any", bower.Id, victim.Id));
            Check(!CanUse(e2, owner, ability, p2.Id), "a bow gives no keyword: not offered");
            // Never opened.
            var s3 = Table(Phase.Action, owner); s3.PriorityPlayerId = owner;
            var g3 = Add(s3, "training-seeds5-air-shugenja", owner, Zone.Home); var p3 = Add(s3, def, owner, Zone.Attached, parent: g3.Id);
            NeverOpened(MatchEngine.FromTestState(s3), owner, ability, p3.Id, command with { CardId = p3.Id });
        }
    }

    // ---- Redoubled Attack: after a battle's resolution ends, straighten one or two performing Personalities' units; they will not bow from the resolution ---------------------------------------------------------
    private static void PassToResolution(MatchEngine e)
    {
        for (var i = 0; i < 40 && e.State.PendingChoice is not { Type: "afterResolutionReaction" }; i++)
        {
            if (e.State.Phase != Phase.Battle && e.State.Phase != Phase.BattleSelection) return;
            if (e.State.UnresolvedBattlefields.Count == 0 && e.State.CurrentBattleProvince is null) return;
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var pass = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, "nothing to pass with in " + e.State.Phase + " (" + e.State.PendingChoice?.Type + ")");
            Accept(e, pass!.Command!);
        }
    }

    private static void RedoubledAttackStraighten()
    {
        const string ability = "redoubled-attack-straighten";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            (MatchEngine E, CardInstance A, CardInstance B, CardInstance C, CardInstance Card) Rig(bool resolve = true)
            {
                // Player 1 attacks; the owner's Personalities win the battle (whichever seat that is).
                var s = Table(Phase.Battle, 1);
                var a = Bf(s, "training-crane-samurai", owner); var b = Bf(s, "training-lion-samurai", owner); var c = Bf(s, "training-crane-veteran", owner);
                var loser = Bf(s, "training-water-personality", foe);
                foreach (var win in new[] { a, b, c }) Batch336Support.Force(win, 20);
                a.Bowed = true; b.Bowed = true;
                var card = Add(s, def, owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (resolve) PassToResolution(e);
                _ = loser;
                return (e, a, b, c, card);
            }
            SlotSelection Units(params CardInstance[] cards) => new("units", cards.Select(x => x.Id).ToArray());
            var rig = Rig();
            Eq(Window(rig.E), "afterResolutionReaction", $"owner {owner}: the window opens for the player holding Redoubled Attack");
            Eq(rig.E.State.PriorityPlayerId, owner, "and he has the first opportunity");
            Check(CanUse(rig.E, owner, ability, rig.Card.Id), "offered"); Check(!CanUse(rig.E, foe, ability, rig.Card.Id), "not to the other player");
            Rejection(rig.E, Use(rig.E, foe, ability, rig.Card.Id, null, null, Units(rig.A)));
            Rejection(rig.E, Use(rig.E, owner, ability, rig.Card.Id, null, null, Units(rig.A, rig.B, rig.C)));
            Check(rig.E.State.Cards[rig.A.Id].Zone == Zone.Battlefield && rig.E.State.Cards[rig.A.Id].Bowed, "the winners are still at the battlefield, still bowed");
            var closed = rig.E.Clone(); Settle(closed);
            Check(!CanUse(closed, owner, ability, rig.Card.Id), "after the window closed it is gone");
            Rejection(closed, Use(closed, owner, ability, rig.Card.Id, null, null, Units(rig.A)) with { ExpectedVersion = closed.Version });
            Accept(rig.E, Use(rig.E, owner, ability, rig.Card.Id, null, null, Units(rig.A, rig.B)));
            Check(!rig.E.State.Cards[rig.A.Id].Bowed && !rig.E.State.Cards[rig.B.Id].Bowed, "their units straightened");
            if (rig.E.State.PendingChoice is not null) // the promise lapses with the battle, so it is only readable while the window is still open
                Check(rig.E.State.ResolutionWillNotBowCardIds.Contains(rig.A.Id) && rig.E.State.ResolutionWillNotBowCardIds.Contains(rig.B.Id) && !rig.E.State.ResolutionWillNotBowCardIds.Contains(rig.C.Id), "and they will not bow");
            Eq(rig.E.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
            Settle(rig.E);
            // Winning Attackers go home bowed from the resolution; winning Defenders go home unbowed, so only the straightening is visible for them.
            Eq(rig.E.State.Cards[rig.A.Id].Zone, Zone.Home, "the winners went home");
            Check(!rig.E.State.Cards[rig.A.Id].Bowed && !rig.E.State.Cards[rig.B.Id].Bowed, "unbowed by the resolution");
            Eq(rig.E.State.Cards[rig.C.Id].Bowed, owner == 1, "the one not chosen bowed as usual (an Attacker)");
            // Control: declined, the winning Attackers go home bowed (the two that started bowed stay bowed as Defenders).
            var d = Rig(); Settle(d.E);
            Check(d.E.State.Cards[d.A.Id].Bowed && d.E.State.Cards[d.B.Id].Bowed, "declined: the two that started bowed are still bowed");
            Eq(d.E.State.Cards[d.C.Id].Bowed, owner == 1, "declined: the third bows only as an Attacker");
            // Before the resolution ends it is not offered.
            var q = Rig(resolve: false);
            NeverOpened(q.E, owner, ability, q.Card.Id, Use(q.E, owner, ability, q.Card.Id, null, null, Units(q.A)));
        }
    }

    // ---- Shinjo Hayan: after engaging, once per turn assign your unbowed Unicorn Clan Personality in your home to the current battlefield ------------------------------------------------------------------
    private static void HayanAssign()
    {
        const string ability = "hayan-assign";
        var def = Def(ability);
        var unicornDef = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Type == CardType.Personality && c.Clan == "Unicorn" && c.Effect == "none" && (c.Abilities ?? []).Length == 0).OrderBy(c => c.Id, StringComparer.Ordinal).First().Id;
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownerAttacks in new[] { true, false })
            {
                var foe = 1 - owner; var attacker = ownerAttacks ? owner : foe;
                var label = $"owner {owner} {(ownerAttacks ? "attacking" : "defending")}";
                CardInstance hayan = null!, unicorn = null!, tired = null!, crane = null!;
                var e = Engage(attacker, s =>
                {
                    hayan = Bf(s, def, owner, bowed: true); // a trait: usable while he is bowed; at the battlefield (presence, issue #1133)
                    Bf(s, "training-crane-samurai", owner); Bf(s, "training-lion-samurai", foe);
                    unicorn = Add(s, unicornDef, owner, Zone.Home); tired = Add(s, unicornDef, owner, Zone.Home, bowed: true); crane = Add(s, "training-crane-veteran", owner, Zone.Home);
                });
                if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e));
                var options = Options(e, owner, ability, hayan.Id);
                Check(options.Any(o => o.Enabled && (o.TargetIds?.Contains(unicorn.Id) ?? false)), label + ": the unbowed Unicorn is offered");
                Check(!options.Any(o => o.TargetIds?.Contains(tired.Id) == true || o.TargetIds?.Contains(crane.Id) == true), label + ": a bowed or non-Unicorn Personality is not");
                var command = InWindow(e, owner, ability, hayan.Id, targetId: unicorn.Id, windowType: "engageReaction");
                Accept(e, command);
                Check(e.State.Cards[unicorn.Id].Zone == Zone.Battlefield && e.State.Cards[unicorn.Id].ProvinceIndex == 0, label + ": assigned to the current battlefield");
                if (ownerAttacks) Eq(e.State.Cards[unicorn.Id].AssignedToAttackTurn, e.State.TurnNumber, label + ": as an attacker");
                else Check(e.State.Cards[unicorn.Id].AssignedToAttackTurn != e.State.TurnNumber, label + ": as a defender");
                Check(!CanUse(e, owner, ability, hayan.Id), label + ": once per turn");
                // Never opened (no battle engaged).
                var s2 = Table(Phase.Action, owner); s2.PriorityPlayerId = owner; var h2 = Add(s2, def, owner, Zone.Home); Add(s2, unicornDef, owner, Zone.Home);
                NeverOpened(MatchEngine.FromTestState(s2), owner, ability, h2.Id, command with { CardId = h2.Id });
            }
    }

    // ---- Shosuro Takazaki: Political Engage: other players do not gain Honor for destroying your cards in this battle's resolution ------------------------------------------------------------------
    private static void TakazakiEngage()
    {
        const string ability = "w738-takazaki-engage";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownerAttacks in new[] { true, false })
            {
                var foe = 1 - owner; var attacker = ownerAttacks ? owner : foe;
                var label = $"owner {owner} {(ownerAttacks ? "attacking" : "defending")}";
                int Gain(bool use, out MatchEngine engine)
                {
                    CardInstance takazaki = null!;
                    var e = Engage(attacker, s =>
                    {
                        takazaki = Bf(s, def, owner); Bf(s, "training-lion-samurai", owner);
                        var big = Bf(s, "training-lion-samurai", foe); Batch336Support.Force(big, 30); // the enemy wins and destroys the owner's units
                    });
                    var honor = Honor(e, foe);
                    if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e));
                    if (use)
                    {
                        var command = InWindow(e, owner, ability, takazaki.Id, windowType: "engageReaction");
                        Accept(e, command);
                        Check(e.State.RuleFlags?.Any(f => f.Kind == RuleFlagKinds.NoDestructionHonorForOthers && f.PlayerId == owner && f.Scope == RuleFlagScope.Battle) == true, label + ": a battle-scope flag for her controller");
                        Check(!CanUse(e, owner, ability, takazaki.Id), label + ": once");
                    }
                    PassUntil(e, () => e.State.CurrentBattleProvince is null && e.State.PendingChoice is null || e.State.Phase != Phase.Battle && e.State.Phase != Phase.BattleSelection);
                    Check(e.State.Cards[takazaki.Id].Zone != Zone.Battlefield, label + ": her unit was destroyed");
                    engine = e;
                    return Honor(e, foe) - honor;
                }
                var without = Gain(false, out _);
                var with = Gain(true, out _);
                Check(without > 0, $"{label}: without the action the winner gains Honor for the destroyed unit ({without})");
                Eq(with, 0, label + ": with it he gains none");
            }
        // Outside an Engage window the Reaction is not available.
        var idle = Table(Phase.Battle, 1); idle.PriorityPlayerId = 0; var t = Bf(idle, def, 0);
        var ie = MatchEngine.FromTestState(idle);
        NeverOpened(ie, 0, ability, t.Id, Use(ie, 0, ability, t.Id));
    }

    // ---- Sniping: after a Ranged Attack is targeted, give it a strength bonus equal to its current strength -----------------------------------------------------------------------------------------
    private static void SnipingBoost()
    {
        const string ability = "sniping-boost";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownAttack in new[] { true, false })
            {
                var actor = ownAttack ? owner : 1 - owner;
                var label = $"owner {owner}, {(ownAttack ? "his own" : "the enemy's")} Ranged Attack";
                (MatchEngine E, CardInstance Sniping, CardInstance Victim) Rig(string kind = "Ranged", bool attack = true)
                {
                    var s = Table(Phase.Battle, 1); s.PriorityPlayerId = actor;
                    var archer = Bf(s, kind == "Ranged" ? "training-followed-attacker" : "training-recon-attacker", actor);
                    var victim = Bf(s, "training-crane-veteran", 1 - actor); // Force 5
                    var sniping = Add(s, def, owner, Zone.Hand);
                    Add(s, "training-attack-reactor", 0, Zone.Home); Add(s, "training-attack-reactor", 1, Zone.Home); // keep the window open so the strength can be read
                    var e = MatchEngine.FromTestState(s);
                    if (attack) Accept(e, Offer(e, actor, kind == "Ranged" ? "training-followed-recon-attack" : "training-weapon-attack", archer.Id, victim.Id));
                    return (e, sniping, victim);
                }
                var rig = Rig();
                Eq(Window(rig.E), "attackReaction", label + ": the Attack opens its window");
                Eq(rig.E.State.PendingAttack!.Strength, 3, "Ranged 3");
                var command = InWindow(rig.E, owner, ability, rig.Sniping.Id, windowType: "attackReaction");
                Accept(rig.E, command);
                Eq(rig.E.State.PendingAttack!.Strength, 6, label + ": the strength doubles");
                Eq(rig.E.State.Cards[rig.Sniping.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
                Settle(rig.E);
                Eq(rig.E.State.Cards[rig.Victim.Id].Zone, Zone.Dead, label + ": and it destroys the 5-Force Personality");
                var control = Rig(); Settle(control.E);
                Check(control.E.State.Cards[control.Victim.Id].Zone != Zone.Dead, label + ": without it Ranged 3 does not");
                // A Melee Attack, and no Attack at all: not offered.
                var melee = Rig("Melee");
                Eq(melee.E.State.PendingAttack?.Kind, "Melee");
                Check(!CanUse(melee.E, owner, ability, melee.Sniping.Id), label + ": Sniping reacts only to a Ranged Attack");
                Rejection(melee.E, Use(melee.E, owner, ability, melee.Sniping.Id));
                var quiet = Rig(attack: false);
                NeverOpened(quiet.E, owner, ability, quiet.Sniping.Id, command with { CardId = quiet.Sniping.Id });
            }
    }

    // ---- Tamori Ruya: after the resolution of your action that bowed a Personality, target a Personality at the same location: bow him ----------------------------------------------------------------------------
    private static void RuyaBow()
    {
        const string ability = "ruya-bow";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            // kind: "battle" = her controller's Battle action bows an enemy Personality at the battlefield; "open-active"/"open-other" = his Open action in the Action phase, at home; "enemy" = the enemy's bow
            (MatchEngine E, CardInstance Ruya, CardInstance First, CardInstance Second, CardInstance Elsewhere) Rig(string kind)
            {
                var battle = kind == "battle" || kind == "enemy";
                var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : (kind == "open-active" ? owner : foe)); s.PriorityPlayerId = kind == "enemy" ? foe : owner;
                var zone = battle ? Zone.Battlefield : Zone.Home; int? province = battle ? 0 : null;
                var actor = kind == "enemy" ? foe : owner; var victimOwner = 1 - actor;
                var ruya = Add(s, def, owner, zone, false, province);
                var bower = battle ? Add(s, "training-battle-bower", actor, zone, false, province) : Add(s, "training-bow-any-source", actor, zone, false, province);
                // Home is a location of its own per player: at home the second Personality must be the owner's own to share Ruya's location.
                var first = Add(s, "training-crane-samurai", victimOwner, zone, false, province); var second = Add(s, "training-lion-samurai", battle ? victimOwner : actor, zone, false, province);
                var elsewhere = Add(s, "training-lion-veteran", victimOwner, Zone.Home);
                if (kind == "enemy") { /* the enemy bows one of the owner's Personalities */ first.OwnerId = owner; second.OwnerId = owner; }
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, actor, battle ? "training-bow-at-battle" : "training-bow-any", bower.Id, first.Id));
                return (e, ruya, first, second, elsewhere);
            }
            foreach (var kind in new[] { "battle", "open-active", "open-other" })
            {
                var label = $"owner {owner} {kind}";
                var rig = Rig(kind);
                Check(rig.E.State.Cards[rig.First.Id].Bowed, label + ": the action bowed a Personality");
                Eq(Window(rig.E), "bowReaction", label + ": her window opens for her controller's own action");
                var command = InWindow(rig.E, owner, ability, rig.Ruya.Id, targetId: rig.Second.Id, windowType: "bowReaction");
                Check(!Options(rig.E, owner, ability, rig.Ruya.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(rig.Elsewhere.Id) ?? false)), label + ": a Personality at another location is not a target");
                Accept(rig.E, command);
                Check(rig.E.State.Cards[rig.Second.Id].Bowed, label + ": she bowed the second Personality");
                Check(!CanUse(rig.E, owner, ability, rig.Ruya.Id), label + ": used once");
                Settle(rig.E);
                var declined = Rig(kind); Settle(declined.E);
                Check(!declined.E.State.Cards[declined.Second.Id].Bowed, label + ": declined, nothing more is bowed");
            }
            // The enemy's action that bowed a Personality is not "your action": no window for her controller.
            var enemy = Rig("enemy");
            Check(enemy.E.State.Cards[enemy.First.Id].Bowed, "the enemy's action bowed a Personality");
            Check(Window(enemy.E) != "bowReaction" && !CanUse(enemy.E, owner, ability, enemy.Ruya.Id), "not her controller's action: not offered");
            // An action that bowed no Personality (a Follower): no window.
            var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = owner;
            var r2 = Bf(s2, def, owner); var cardBower = Bf(s2, "training-card-bower", owner); var host = Bf(s2, "training-lion-samurai", foe);
            var fol = Add(s2, "training-follower", foe, Zone.Attached, province: 0, parent: host.Id);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Offer(e2, owner, "training-bow-enemy-card-at-battle", cardBower.Id, fol.Id));
            Check(e2.State.Cards[fol.Id].Bowed && !CanUse(e2, owner, ability, r2.Id), "a bowed Follower is not a bowed Personality: not offered");
            // Never opened.
            var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = owner; var r3 = Bf(s3, def, owner); Bf(s3, "training-lion-samurai", foe);
            NeverOpened(MatchEngine.FromTestState(s3), owner, ability, r3.Id, Use(MatchEngine.FromTestState(s3), owner, ability, r3.Id));
        }
    }

    // ---- The Perfect Moment: after the end of an Attack Phase in which you were the Defender, create an additional Attack Phase with the previous Attacker as Defender ------------------------------------------
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static void PassTwice(MatchEngine e) { Accept(e, PassC(e)); Accept(e, PassC(e)); }
    private static bool Event(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);

    private static void PerfectMomentDeclare()
    {
        const string ability = "perfect-moment-declare";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner; // the previous Attacker
            (MatchEngine E, CardInstance Card, CardInstance ParagonA, CardInstance ParagonB, CardInstance Plain, CardInstance MoveCard) Rig()
            {
                var s = Table(Phase.Battle, foe);
                s.PriorityPlayerId = owner;
                s.CurrentBattleProvince = 3; s.UnresolvedBattlefields = [3];
                var card = Add(s, def, owner, Zone.Hand);
                var moveCard = Add(s, "training-move-battle-strategy", owner, Zone.Hand);
                var paragonA = Kw(Add(s, "training-crane-samurai", owner, Zone.Home), "Paragon"); Batch336Support.Force(paragonA, 17);
                var paragonB = Kw(Add(s, "training-crane-samurai", owner, Zone.Home), "Battle Maiden"); Batch336Support.Force(paragonB, 17);
                var plain = Add(s, "training-crane-samurai", owner, Zone.Home); Batch336Support.Force(plain, 17);
                var e = MatchEngine.FromTestState(s);
                PassTwice(e); // the last battle of the other player's Attack Phase is over
                return (e, card, paragonA, paragonB, plain, moveCard);
            }
            var rig = Rig();
            Eq(Window(rig.E), "attackPhaseEndReaction", $"owner {owner}: the Defender may react to the end of the Attack Phase");
            var command = InWindow(rig.E, owner, ability, rig.Card.Id, windowType: "attackPhaseEndReaction");
            var e = rig.E;
            Accept(e, command);
            Eq(e.State.ActivePlayerId, owner, "its player declared the additional Attack Phase and is its Attacker");
            Eq(e.State.Phase, Phase.InfantryDeclaration);
            Eq(string.Join(",", e.State.UnresolvedBattlefields), "0,1,2,3", "a battlefield at each of the previous Attacker's provinces");
            Check(e.State.R1.PerfectMoment is { } moment && moment.PlayerId == owner, "its restrictions are in force");
            Check(Rejection(e, new("attack", owner, e.Version, Assignments: new() { [rig.Plain.Id] = 0 })).Length > 0, "a Personality who is neither a Paragon nor a Battle Maiden may not assign");
            Accept(e, new("attack", owner, e.Version, Assignments: new() { [rig.ParagonA.Id] = 0, [rig.ParagonB.Id] = 1 }));
            Accept(e, new("assignDefense", foe, e.Version, Assignments: new()));
            Eq(e.State.Phase, Phase.BattleSelection);
            Accept(e, new("selectBattle", owner, e.Version, Province: 0));
            Accept(e, PassC(e)); // the previous Attacker, now the Defender, has the first opportunity and passes
            Accept(e, Offer(e, owner, "training-move-to-battle", rig.MoveCard.Id, rig.Plain.Id));
            Eq(e.State.Cards[rig.Plain.Id].Zone, Zone.Home, "the movement of a plain Personality was negated");
            Check(Event(e, "moveNegated"), "and the log says it was");
            PassTwice(e);
            Check(e.State.Players[foe].Provinces[0].Destroyed, "the first battle destroyed province 0");
            Accept(e, new("selectBattle", owner, e.Version, Province: 1));
            PassTwice(e);
            Check(!e.State.Players[foe].Provinces[1].Destroyed, "the second province survives a victory that would have destroyed it");
            foreach (var other in new[] { 2, 3 }) { Accept(e, new("selectBattle", owner, e.Version, Province: other)); PassTwice(e); }
            Eq(e.State.Phase, Phase.Dynasty, "the additional Attack Phase is over");
            Eq(e.State.ActivePlayerId, foe, "the turn's own player is the active player again");
            Check(e.State.R1.PerfectMoment is null && e.State.Players[foe].Provinces.Count(p => p.Destroyed) == 1, "the restrictions ended with it, and exactly one province was destroyed");
            // Declined: the Dynasty Phase follows with no additional phase.
            var d = Rig(); Accept(d.E, Decline(d.E)); Settle(d.E);
            Check(d.E.State.R1.PerfectMoment is null && d.E.State.Phase != Phase.InfantryDeclaration, "declined: no additional Attack Phase");
            // Never opened (a battle still in progress): not offered.
            var s2 = Table(Phase.Battle, foe); s2.PriorityPlayerId = owner; var c2 = Add(s2, def, owner, Zone.Hand); Bf(s2, "training-crane-samurai", owner); Bf(s2, "training-lion-samurai", foe);
            NeverOpened(MatchEngine.FromTestState(s2), owner, ability, c2.Id, Use(MatchEngine.FromTestState(s2), owner, ability, c2.Id));
        }
    }

    // ---- To First Blood: after another player's Iaijutsu action targets your Personality, negate his destruction ---------------------------------------------------------------------------------------------
    private static void FinishDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 8; guard++)
        {
            var command = duel.Stage switch
            {
                "Focusing" => new GameCommand("strike", duel.TurnPlayerId, e.Version),
                "DuelistRefocus" => new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version),
                _ => throw new Exception($"unexpected duel stage {duel.Stage}")
            };
            Accept(e, command);
        }
    }

    private static void ToFirstBloodNegate()
    {
        const string ability = "to-first-blood-negate";
        var def = Def(ability);
        var ichizoDef = Def("ichizo-exp-duel");
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            (MatchEngine E, CardInstance Ichizo, CardInstance Target, CardInstance Card) Rig()
            {
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = foe;
                var ichizo = Bf(s, ichizoDef, foe); // Iaijutsu, Chi 4
                var target = Bf(s, "training-crane-samurai", owner); // Chi 3
                var card = Add(s, def, owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, foe, "ichizo-exp-duel", ichizo.Id, target.Id));
                return (e, ichizo, target, card);
            }
            var rig = Rig();
            Eq(Window(rig.E), "beforeActionReaction", $"owner {owner}: the window opens for a reactor holding To First Blood");
            var command = InWindow(rig.E, owner, ability, rig.Card.Id, windowType: "beforeActionReaction");
            Accept(rig.E, command);
            Settle(rig.E);
            FinishDuel(rig.E);
            Check(rig.E.State.Cards[rig.Target.Id].Zone is Zone.Battlefield or Zone.Home, "he lost the duel (Chi 3 against 4) but was not destroyed");
            Eq(rig.E.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            Check(rig.E.State.Events.Any(x => x.Type == "duelResolved" && x.Message.Contains("Mirumoto Ichizo")), "Ichizo won");
            // Control: without the reaction he is destroyed.
            var c = Rig(); Settle(c.E); FinishDuel(c.E);
            Eq(c.E.State.Cards[c.Target.Id].Zone, Zone.Dead, "without it the loser is destroyed");
            // It answers only an Iaijutsu action.
            var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = foe;
            var t3 = Bf(s3, "training-crane-samurai", owner); var c3 = Add(s3, def, owner, Zone.Hand); var b3 = Bf(s3, "training-battle-bower", foe);
            var e3 = MatchEngine.FromTestState(s3);
            Accept(e3, Offer(e3, foe, "training-bow-at-battle", b3.Id, t3.Id));
            Check(!CanUse(e3, owner, ability, c3.Id), "not against an action that is not Iaijutsu");
            // Never opened.
            var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = foe; Bf(s4, "training-crane-samurai", owner); var c4 = Add(s4, def, owner, Zone.Hand);
            NeverOpened(MatchEngine.FromTestState(s4), owner, ability, c4.Id, command with { CardId = c4.Id });
        }
    }

    // ---- Training Engage destroy: after engaging, target an enemy Personality: destroy him -------------------------------------------------------------------------------------------------------------
    private static void EngageDestroy()
    {
        const string ability = "training-engage-destroy";
        const string def = "training-engage-destroy-strategy";
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownerAttacks in new[] { true, false })
            {
                var foe = 1 - owner; var attacker = ownerAttacks ? owner : foe;
                var label = $"owner {owner} {(ownerAttacks ? "attacking" : "defending")}";
                CardInstance card = null!, mine = null!, theirs = null!;
                var e = Engage(attacker, s =>
                {
                    mine = Bf(s, "training-crane-samurai", owner); theirs = Bf(s, "training-lion-samurai", foe); card = Add(s, def, owner, Zone.Hand);
                });
                if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e));
                var targets = Options(e, owner, ability, card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
                Check(targets.SequenceEqual([theirs.Id]), label + ": only the enemy Personality is a target: " + string.Join(",", targets));
                var command = InWindow(e, owner, ability, card.Id, targetId: theirs.Id, windowType: "engageReaction");
                Rejection(e, command with { TargetId = mine.Id });
                Accept(e, command);
                Eq(e.State.Cards[theirs.Id].Zone, Zone.Dead, label + ": the enemy Personality is destroyed");
                Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield, label + ": his own is untouched");
                Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, label + ": the Strategy was played");
                // After the Engage window: gone.
                var late = Engage(attacker, s => { Bf(s, "training-crane-samurai", owner); Bf(s, "training-lion-samurai", foe); card = Add(s, def, owner, Zone.Hand); });
                var closed = late.Clone(); Settle(closed);
                Check(!CanUse(closed, owner, ability, card.Id), label + ": gone once the window closed");
            }
    }

    // ---- Training duel reaction (a Holding): a Reaction before a duel begins; either duel participant's side, or neither ---------------------------------------------------------------------------------------
    private static void DuelReactor()
    {
        const string ability = "training-duel-reaction";
        const string def = "training-duel-reactor";
        foreach (var holder in new[] { 0, 1 })
            foreach (var holderChallenges in new[] { true, false })
            {
                var other = 1 - holder; var challenger = holderChallenges ? holder : other;
                var label = $"holder {holder} {(holderChallenges ? "challenging" : "challenged")}";
                (MatchEngine E, CardInstance Holding) Rig(bool challenge = true)
                {
                    var s = Table(Phase.Battle, 1); s.PriorityPlayerId = challenger;
                    var mine = Bf(s, "training-crane-samurai", challenger); var rival = Bf(s, "training-lion-samurai", 1 - challenger);
                    var strategy = Add(s, "training-duel-force", challenger, Zone.Hand);
                    var holding = Add(s, def, holder, Zone.Home);
                    var e = MatchEngine.FromTestState(s);
                    if (challenge) Accept(e, Offer(e, challenger, "training-duel-force", strategy.Id, rival.Id, mine.Id));
                    return (e, holding);
                }
                var rig = Rig();
                Eq(Window(rig.E), "duelReaction", label + ": the window before the duel begins");
                var command = InWindow(rig.E, holder, ability, rig.Holding.Id, windowType: "duelReaction");
                var honor = Honor(rig.E, holder);
                Accept(rig.E, command);
                Eq(Honor(rig.E, holder), honor + 1, label + ": gain 1 Honor before the duel");
                Check(!CanUse(rig.E, holder, ability, rig.Holding.Id), label + ": once");
                Settle(rig.E); RunDuel(rig.E);
                Eq(Honor(rig.E, holder), honor + 1, label + ": and nothing more");
                // Declined: nothing.
                var d = Rig(); var hd = Honor(d.E, holder); Settle(d.E);
                Eq(Honor(d.E, holder), hd, label + ": declined, no Honor");
                // No duel: not offered.
                var q = Rig(challenge: false);
                NeverOpened(q.E, holder, ability, q.Holding.Id, command with { CardId = q.Holding.Id });
            }
    }

    // ---- Twist of Fate: before your Dynasty Phase ends, take an additional Limited or Open action ------------------------------------------------------------------------------------------------------------------
    private static void TwistOfFateGrant()
    {
        const string ability = "twist-of-fate-grant";
        var def = Def(ability);
        var menaceDef = Def("veiled-menace-loss");
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            (MatchEngine E, CardInstance Twist, CardInstance Menace, CardInstance Courtier, CardInstance Victim) Rig(bool withTwist = true)
            {
                var s = Table(Phase.Dynasty, owner); s.PriorityPlayerId = owner;
                var twist = withTwist ? Add(s, def, owner, Zone.Hand) : Add(s, "training-focus-strategy", owner, Zone.Hand);
                var menace = Add(s, menaceDef, owner, Zone.Hand); // an Open action
                var courtier = Kw(Add(s, "training-crane-courtier", owner, Zone.Home), "Courtier");
                var victim = Add(s, "training-lion-veteran", foe, Zone.Home); victim.Dishonorable = true;
                return (MatchEngine.FromTestState(s), twist, menace, courtier, victim);
            }
            var rig = Rig();
            Accept(rig.E, rig.E.QueryActions(owner).Single(a => a.Type == "endDynasty").Command!);
            Eq(Window(rig.E), "twistOfFateReaction", $"owner {owner}: the window opens as the Dynasty Phase is ended");
            Eq(rig.E.State.Phase, Phase.Dynasty, "the Dynasty Phase has not ended");
            var command = InWindow(rig.E, owner, ability, rig.Twist.Id, windowType: "twistOfFateReaction");
            Accept(rig.E, command);
            Eq(rig.E.State.Cards[rig.Twist.Id].Zone, Zone.FateDiscard, "Twist of Fate was spent");
            Eq(rig.E.State.Phase, Phase.Action, "the additional action is taken in the Action Phase");
            Eq(rig.E.State.PriorityPlayerId, owner, "by its player");
            var honor = Honor(rig.E, foe);
            Accept(rig.E, Offer(rig.E, owner, "veiled-menace-loss", rig.Menace.Id, rig.Victim.Id, rig.Courtier.Id));
            Eq(Honor(rig.E, foe), honor - 2, "the Open action resolved");
            Eq(rig.E.State.Phase, Phase.Dynasty, "and the Dynasty Phase carries on");
            Check(!rig.E.QueryActions(owner).Any(a => a.Type == "useAbility"), "a second additional action is not on offer");
            // Passing the window ends the Dynasty Phase; Twist stays in hand.
            var p = Rig(); Accept(p.E, p.E.QueryActions(owner).Single(a => a.Type == "endDynasty").Command!); Accept(p.E, Decline(p.E));
            Check(p.E.State.Phase != Phase.Dynasty, "declined: the phase ended"); Eq(p.E.State.Cards[p.Twist.Id].Zone, Zone.Hand, "Twist of Fate was not used");
            // Without the card there is no window. The other player's Dynasty Phase never offers it to the holder.
            var n = Rig(withTwist: false); Accept(n.E, n.E.QueryActions(owner).Single(a => a.Type == "endDynasty").Command!);
            Check(Window(n.E) != "twistOfFateReaction", "without the card there is no window");
            var s2 = Table(Phase.Dynasty, foe); s2.PriorityPlayerId = foe; var held = Add(s2, def, owner, Zone.Hand);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, e2.QueryActions(foe).Single(a => a.Type == "endDynasty").Command!);
            Check(Window(e2) != "twistOfFateReaction" && !CanUse(e2, owner, ability, held.Id), "another player's Dynasty Phase: not offered");
            // Never opened (the Action phase).
            var s3 = Table(Phase.Action, owner); s3.PriorityPlayerId = owner; var c3 = Add(s3, def, owner, Zone.Hand);
            NeverOpened(MatchEngine.FromTestState(s3), owner, ability, c3.Id, command with { CardId = c3.Id });
        }
    }

    // ---- Utaku Tairu: after a Personality assigns or moves, if he is now opposing Tairu, draw a card ----------------------------------------------------------------------------------------------------------------
    private static void TairuExpDraw()
    {
        const string ability = "tairu-exp-draw";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            // (a) Tairu's controller attacks (he is already assigned); the other player's defense assignment opposes him.
            MatchEngine AssignRig(int defendAt, out CardInstance tairu)
            {
                var s = MatchEngine.CreateEconomyScenario().State;
                s.Phase = Phase.DefenseAssignment; s.ActivePlayerId = owner; s.PriorityPlayerId = foe; s.UnresolvedBattlefields = [0, 1, 2, 3];
                tairu = Add(s, def, owner, Zone.Battlefield, province: 0);
                var guard = Add(s, "training-lion-samurai", foe, Zone.Home);
                Add(s, "training-focus-strategy", owner, Zone.FateDeck);
                var e = MatchEngine.FromTestState(s);
                Accept(e, new GameCommand("assignDefense", foe, e.Version, Assignments: new Dictionary<string, int> { [guard.Id] = defendAt }));
                return e;
            }
            var a = AssignRig(0, out var tairu);
            Eq(Window(a), "afterActionResolvedReaction", $"owner {owner}: the assignment opposing Tairu opens the window");
            var command = InWindow(a, owner, ability, tairu.Id, windowType: "afterActionResolvedReaction");
            var hand = a.State.Players[owner].Hand.Count;
            Accept(a, command);
            Eq(a.State.Players[owner].Hand.Count, hand + 1, "Tairu's player drew a card");
            Check(!CanUse(a, owner, ability, tairu.Id) || a.State.PendingChoice is null, "taken");
            var away = AssignRig(2, out var t2);
            Check(Window(away) != "afterActionResolvedReaction" || !CanUse(away, owner, ability, t2.Id), "assigned to another battlefield: nothing");
            // (b) Tairu defends; the other player moves a Personality to his battlefield.
            MatchEngine MoveRig(bool enemyMoves, out CardInstance tairu2)
            {
                var m = Table(Phase.Battle, 1); m.PriorityPlayerId = enemyMoves ? foe : owner;
                tairu2 = Bf(m, def, owner); Bf(m, "training-lion-samurai", foe);
                var mover = Add(m, "training-lion-samurai", enemyMoves ? foe : owner, Zone.Home);
                var strategy = Add(m, "training-move-battle-strategy", enemyMoves ? foe : owner, Zone.Hand);
                Add(m, "training-focus-strategy", owner, Zone.FateDeck);
                var e = MatchEngine.FromTestState(m);
                Accept(e, Offer(e, enemyMoves ? foe : owner, "training-move-to-battle", strategy.Id, mover.Id));
                return e;
            }
            var mv = MoveRig(true, out var t3);
            PassUntil(mv, () => CanUse(mv, owner, ability, t3.Id) || mv.State.PendingChoice is null);
            Check(CanUse(mv, owner, ability, t3.Id), "a Personality moved to oppose him: offered");
            var own = MoveRig(false, out var t4);
            PassUntil(own, () => own.State.PendingChoice is null || CanUse(own, owner, ability, t4.Id));
            Check(!CanUse(own, owner, ability, t4.Id), "his own side's Personality moving in does not oppose him");
            // Never opened.
            var q = Table(Phase.Battle, 1); q.PriorityPlayerId = owner; var tq = Bf(q, def, owner);
            NeverOpened(MatchEngine.FromTestState(q), owner, ability, tq.Id, command with { CardId = tq.Id });
        }
    }

    // ---- Watchers in the Dark: after engaging, your performing Scout at the battlefield and a card in a unit there: Reconnaissance there; abilities on cards in the target's unit may not be used ------------------------
    private static void WatchersDarkRecon()
    {
        const string ability = "watchers-dark-recon";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
            foreach (var ownerAttacks in new[] { true, false })
            {
                var foe = 1 - owner; var attacker = ownerAttacks ? owner : foe;
                var label = $"owner {owner} {(ownerAttacks ? "attacking" : "defending")}";
                CardInstance card = null!, scout = null!, bower = null!, outside = null!, homeScout = null!;
                MatchEngine Rig(bool useIt)
                {
                    var e = Engage(attacker, s =>
                    {
                        scout = Add(s, "training-scout-samurai", owner, Zone.Battlefield, bowed: true, province: 0);
                        homeScout = Add(s, "training-scout-samurai", owner, Zone.Home);
                        bower = Bf(s, "training-battle-bower", foe); outside = Bf(s, "training-battle-bower", foe); card = Add(s, def, owner, Zone.Hand);
                        // the target's unit is `bower` alone; a Follower of another unit is the "outside" check
                    });
                    if (e.State.PriorityPlayerId != owner) Accept(e, Decline(e));
                    if (useIt)
                    {
                        var performers = Options(e, owner, ability, card.Id).Where(o => o.Enabled).Select(o => o.Command!.PerformerId).Distinct().ToArray();
                        Check(performers.Contains(scout.Id) && !performers.Contains(homeScout.Id), label + ": the Scout at the battlefield may perform it, the one at home may not: " + string.Join(",", performers));
                        var command = InWindow(e, owner, ability, card.Id, performerId: scout.Id, targetId: bower.Id, windowType: "engageReaction");
                        Accept(e, command);
                        Check(e.State.Players[1 - attacker].Provinces[0].Reconnaissance[owner], label + ": its player has Reconnaissance there");
                    }
                    PassUntil(e, () => e.State.PendingChoice is null);
                    // The foe holds the priority (he passes the first opportunity if it is the owner's).
                    for (var guard = 0; e.State.PriorityPlayerId != foe && guard < 2; guard++) Accept(e, PassC(e));
                    return e;
                }
                var used = Rig(true);
                Check(CanUse(used, foe, "training-bow-at-battle", outside.Id), label + ": a card outside the target's unit still may");
                Check(!CanUse(used, foe, "training-bow-at-battle", bower.Id), label + ": abilities on cards in the target's unit may not be used");
                Rejection(used, Use(used, foe, "training-bow-at-battle", bower.Id, scout.Id));
                var control = Rig(false);
                Check(CanUse(control, foe, "training-bow-at-battle", bower.Id), label + ": declined, his ability is available as usual");
            }
    }

    public static void Run(TestRunner runner) => runner.Run("single-reaction-3", [
        ("Asahina Keigo: when paying a Gold cost in any window, bowing him and discarding a card produces its Focus Value (3 Gold); not for the other player, spent, bowed or a blank card", KeigoGold),
        ("Seppun Washi: when paying a Gold cost in any window, bowing him produces 2 Gold; once per turn, never for the other player", WashiGold),
        ("Bayushi Mitsuo: after a Battle action (the enemy's, or his own Trusting Instinct) moved him, his controller takes an additional Battle action; not for a bystander's move or with no window", MitsuoAdditional),
        ("Absolution: before a revealed Event resolves either holder may negate it (the Event is discarded unresolved, Absolution spent); passing lets it resolve; never offered outside the window", AbsolutionNegate),
        ("An Insult Answered: after another player's Honor-costing Limited action the loser's Duelist challenges the performer; the loser is destroyed and the winner gets two +1F Revenge tokens (either outcome); not for a harmless or own action", InsultAnsweredDuel),
        ("Blade of Champions: a Battle action against a card in its unit opens the controller's window (either seat); the Blade bows and the action lands on the chosen own card; declined it lands on the wielder; not for an Open action, another unit or a bowed Blade", BladeRedirect),
        ("Chrysanthemum Blossom: when its Personality is assigned (as Attacker or Defender) the controller gets a +1F token and 1 Honor, once; declined or another Personality assigned gives nothing; dishonorable wearer is rehonored", ChrysanthemumBoost),
        ("Confusion at Court: before either player's Favor-discarding action resolves (Action phase or Battle) the other player negates its effects while the Favor is still discarded; not for an action with no Favor cost", ConfusionNegate),
        ("Detained: after engaging (as Attacker or Defender) bow an unbowed Magistrate to move an enemy dishonorable Personality home, bow his unit, and his controller loses 2 Honor; not once the Engage window is over", DetainedMove),
        ("Flesh Cutter Arrows: when its wielder's Ranged Attack targets a card without Armor, bow it for +2 strength (4 becomes 6 and kills a Force 5), for either seat; not with Armor, another attacker or no Attack", FleshCutterArrows),
        ("Grateful Reward: after engaging (either seat) the performing Samurai gets a created 1F Ashigaru Cavalry Follower; a Paragon also gains 2 Honor and draws; non-Samurai never a performer", GratefulRewardCreate),
        ("Hero's Memorial: after another player's action destroys one of your Personalities (either seat) bow it to gain 2 Honor; not for the enemy's loss, your own action or a bowed Memorial", HerosMemorialHonor),
        ("Hida Takeuchi: after an Equip attaches a Follower from hand to him he draws his controller a card; not for another host or a Weapon", TakeuchiDraw),
        ("Introspection: after one of your Rings enters play (Battle or Action phase, either seat) search the Fate deck for any card into hand; not for an enemy Ring or before a Ring", IntrospectionSearch),
        ("Iuchi Kota: when an action would move your Personality at Kota's battlefield his controller negates the movement; not at home, another battlefield or for a bow", KotaNegateMove),
        ("Kakita Kenta: after an action in which Kenta won a duel (challenging or challenged, either seat) move a dishonorable enemy home; not after a loss or for an honorable target", KentaMoveHome),
        ("Kimono of the Turquoise Champion: before an action's Honor loss bow it to negate the loss (own or enemy action, either seat) while the rest of the action stands; not bowed", KimonoNegateLosses),
        ("Ikoma Ayumi: after she enters play (bought in either seat's Dynasty phase) her controller loses 2 Honor once; not offered at home", AyumiEntry),
        ("Kitsune Denhei: after he enters play the controller creates the printed 2F/2C/3PH Snake at home whose Open bite locks a Personality's base abilities (either seat's Action phase)", DenheiCreate),
        ("Kitsune Ohsuki: after she enters play the controller creates the printed 0F/2C/3PH Sparrow whose Limited gains 3 Honor for the active player with no province destroyed", OhsukiCreate),
        ("Masayoshi: after he enters play a +4F/+1C Weapon is created and attached to him, once; not offered at home", MasayoshiCreate),
        ("Yoritomo Toganin: Invest 5 Gold as he enters play gives three +1F tokens, once; declined gives none (+1F only while another player is Crane)", ToganinInvest),
        ("Training Spider honor-loss Personality: after entering play (either seat) its controller loses 2 Honor", SpiderEntryHonorLoss),
        ("Satoshi's Dual Warfans: after it enters play from the hand (Equip, either seat) draw a card, once; not for an Item already in play", SatoshiWarfansDraw),
        ("Training terrain with entry reaction: played by a Battle action (either seat), its entry Reaction gains 1 Honor; refused in the Action phase", TerrainEntryReaction),
        ("Legion of the Bat: after an action moves an enemy Infantry Personality to its battlefield its controller (either seat) bows him; not for Cavalry, declined or with no move", LegionOfTheBatBow),
        ("Mirumoto Rokai: after another player's action targets your Personality at his location destroy him to negate your Personality's destruction (either seat); not at another location or for a bow", RokaiNegateDestruction),
        ("Ninja Tricks: after another player's Strategy action is announced in a battle bow your Ninja to negate its effects and lose 2 Honor (either seat); not without an unbowed Ninja, for a card in play or in the Action phase", NinjaTricksNegate),
        ("Purify the Soul: before an action that gives your Personality Shadowlands resolves, bow the Spell to negate that keyword (Undead still given); not when bowed, declined or for a plain bow", PurifyTheSoulNegate),
        ("Redoubled Attack: after a battle's resolution ends the winning side's player straightens one or two performing Personalities' units and they do not bow from it; not before the resolution, not three, not for the other player", RedoubledAttackStraighten),
        ("Shinjo Hayan: after engaging (either seat, attacking or defending) assign an unbowed Unicorn Clan Personality from home to the battlefield, once per turn; bowed or non-Unicorn never offered", HayanAssign),
        ("Shosuro Takazaki: Political Engage (either seat) means the winner gains no Honor for destroying her controller's units in this battle's resolution; without it he does", TakazakiEngage),
        ("Sniping: after a Ranged Attack is targeted (own or enemy's, either seat) double its strength so a Force 5 target dies; not against a Melee Attack or with no Attack", SnipingBoost),
        ("Tamori Ruya: after her controller's own action bowed a Personality (Battle, or Open at home in either seat's Action phase) she bows a second Personality at her location; not for the enemy's bow or a bowed Follower", RuyaBow),
        ("The Perfect Moment: after an Attack Phase in which you defended, create an additional Attack Phase with the other player defending: only Paragons or Battle Maidens assign or move, at most one province destroyed (either seat)", PerfectMomentDeclare),
        ("To First Blood: after another player's Iaijutsu action targets your Personality (either seat) negate his destruction in the duel; not against a non-Iaijutsu action", ToFirstBloodNegate),
        ("Training Engage destroy: after engaging (either seat, attacking or defending) destroy a targeted enemy Personality, never one's own; gone after the Engage window", EngageDestroy),
        ("Training duel reaction (Holding): before a duel begins its holder gains 1 Honor, whether he challenges or is challenged; not without a duel", DuelReactor),
        ("Twist of Fate: before your Dynasty Phase ends take an additional Open or Limited action (either seat), then the phase carries on; not for the other player's phase or without the card", TwistOfFateGrant),
        ("Utaku Tairu: after a Personality assigns or moves to oppose him his controller draws a card (either seat); not for another battlefield or his own side's move", TairuExpDraw),
        ("Watchers in the Dark: after engaging (either seat) the Scout at the battlefield gives Reconnaissance there and the target's unit's abilities may not be used, other units unaffected", WatchersDarkRecon),
    ]);
}
