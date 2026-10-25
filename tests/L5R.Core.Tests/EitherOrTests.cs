using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// The opposing player's Either/Or choice (issue #338) and its three consumers: Yoritomo Sakuma (oracle-l5r-9624: a payment-time alternate Gold
// source, and a Political Reaction that makes the acting player pay 2 Gold or have the action negated), Unfortunate Hesitation
// (oracle-l5r-9941: the target's controller bows him or must pass at his next opportunity this phase), and the general pieces they are built
// on (EitherOr.cs, ActionNegation.cs, ForcedPass.cs, GoldQuotes.cs' alternate source). Every reading is in
// docs/decisions/2026-09-19-copy-and-either-or-primitives.md. Each test builds an explicit position and drives it only through QueryActions and the
// commands the engine offers.
internal static class EitherOrTests
{
    private const string Sakuma = "oracle-l5r-9624", Hesitation = "oracle-l5r-9941", Adi = "oracle-l5r-9485", ThreeWay = "training-either-or-three";
    private const string Negate = "sakuma-political-negate", Produce = "sakuma-produce-gold", Bow = "training-bow-any", Charity = "training-charity-honor";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Sakuma is the printed card (Force 1, Chi 3, no Honor Requirement, Cost 8, Personal Honor 1, Mantis Courtier Bully Merchant) with the scan's '(remaining)' in his text and his two abilities, and Unfortunate Hesitation the printed Strategy (Cost 0, Focus 2, Battle/Open); the Political action tag is on every wired Political action", PublishedShape),
            ("Alternate Gold source (Sakuma's first ability): he counts as a 2 Gold producer inside any payment -- offered in the quote, validated, and bowed by being chosen -- but the engine never suggests him when printed producers cover the cost; a bowed or spent one, or an opponent's, is refused", AlternateGoldSource),
            ("Sakuma's Political Reaction, the acting player pays: the window offers it to his controller against another player's Political action targeting one of his Personalities, the ACTING player decides (public decision, private quote), pays 2 Gold through the payment path, the window is restored and the action then resolves in full", PoliticalReactionPaid),
            ("Issue #913: the Either/Or Gold quote carries the payer's payment methods, so a published Jade Mine's second way to bow ('#alt') is offered in Sources and is accepted by the engine when it is the chosen payment", EitherOrQuoteSources),
            ("Sakuma's Political Reaction, the acting player does not pay: the action is negated -- its effects do not happen, its use is still spent -- the window is restored, and priority passes on as after any action", PoliticalReactionNegated),
            ("When the acting player cannot pay 2 Gold (or the performer is Protected) there is no decision: the negation applies at once and is announced, and the reaction finishes like any other", CannotPayAutoNegates),
            ("When Sakuma may react: only to ANOTHER player's Political action (the ability's Political tag or a Political card's own keyword) that targets one of HIS controller's Personalities; not to a non-Political action, his controller's own, a non-Personality target, or while he is bowed", WhenSakumaMayReact),
            ("A negated action is still TAKEN: the Gold cost is paid, a Strategy played from hand is spent, the once-per-turn use is stamped and Additional Actions are forfeited -- but the effects (and costs an ability pays inside its own effects) are not", NegatedActionIsStillTaken),
            ("The Either/Or is public and its quote private: both players see what is being decided and by whom, the deciding player's options carry the payment quote and the other player's do not, the waiting player has no decision, and no hidden card id crosses to either", EitherOrViewAndPrivacy),
            ("The general primitive with three options (pay 2 Gold / lose 2 Honor / let the opponent gain 2 Honor): each is applied and announced, an unavailable one is listed disabled with the reason, one legal option resolves by itself, and every offered command validates on a fresh engine", ThreeWayChoice),
            ("Either/Or rejections leave the state untouched: the wrong player, an unknown or unavailable option, a payment that is short, wrong or unnecessary, a stale version -- and Seppuku, an Open action taken only at one's own turn to act with nothing pending (issue #368), is refused while one is open", EitherOrRejections),
            ("Unfortunate Hesitation, the controller bows him: the Personality bows, nothing is owed, and priority passes on; the Strategy left the hand as it was played and is a public discard", HesitationBow),
            ("Unfortunate Hesitation, the controller does not: he owes a pass at his next opportunity this phase -- only pass is legal (every Battle, Limited, Open action, played Strategy, Reserve recruit and equip listed disabled with the reason), and that pass settles it", HesitationForcedPass),
            ("Unfortunate Hesitation has nothing to choose when the Personality is already bowed (the pass is simply owed), and works on his controller's own Personality (the acting player is then the one who decides)", HesitationAlreadyBowedAndOwn),
            ("The forced pass lasts one PRINTED phase: it lapses when the phase moves on, the whole Attack Phase is one phase, another player's or an earlier phase's entry does not bind, and one pass settles one owed pass", HesitationPhaseScope),
            ("The forced pass restricts only Battle, Limited and Open actions: a Reaction in a window is still usable, Dynasty purchasing and the Attack Phase's own assignment decisions are untouched, and it works as a Battle action at the battlefield too", HesitationScope),
            ("The Either/Or showcase (the UI issue's scripted position) is an ordinary replayable match: Hesitation played against Yogo Adi asks player 1, the three-option fixture then asks player 0, and the replay reproduces the state", EitherOrShowcase),
            ("The bespoke opposing-player choices that predate the primitive are untouched: Shosuro Rin's shame still offers its dishonor-or-bow choice to the targeted Personality's controller", RinShameUnchanged),
        ];
        runner.Run("either-or", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static void Same(IEnumerable<string> actual, IEnumerable<string> expected, string message = "Sequences differ") =>
        Check(actual.SequenceEqual(expected), $"{message}: expected [{string.Join(", ", expected)}]; got [{string.Join(", ", actual)}]");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"eo-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) { var st = e.State; edit(st); return MatchEngine.FromTestState(st); }

    private static MatchState Table(Phase phase = Phase.Action, int actor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    private static CardInstance Now(MatchEngine e, string id) => e.State.Cards[id];
    private static bool Produces(CardInstance c) => CardCatalog.Get(c.DefinitionId).GoldProduction > 0;

    // Every card of the player that produces Gold is bowed, so a test controls exactly which sources are ready.
    private static void BowAllGold(MatchState s, int player) { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == player && Produces(c))) c.Bowed = true; }

    private static ActionOption? Listed(MatchEngine e, int player, string abilityId, string? cardId = null) =>
        e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && (cardId is null || a.CardId == cardId) && a.Label.StartsWith(AbilityCatalog.All[abilityId].Label, StringComparison.Ordinal));

    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == cardId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return option!.Command! with { TargetId = targetId };
    }

    // An untargeted action, through its offered command.
    private static GameCommand Take(MatchEngine e, int player, string ability, string cardId)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability);
        Check(option is not null, $"{ability} on {cardId} was not offered to player {player}");
        return option!.Command!;
    }

    private static ActionOption[] Answers(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEitherOr").ToArray();

    private static void Answer(MatchEngine e, int player, string key)
    {
        var option = Answers(e, player).SingleOrDefault(a => a.Command?.Choice == key);
        Check(option is { Enabled: true, Command: not null }, $"answering {key} was not offered");
        Accept(e, option!.Command!);
    }

    private static void Pass(MatchEngine e, int player) => Accept(e, new GameCommand("pass", player, e.Version));
    private static void PassReaction(MatchEngine e, int player) => Accept(e, new GameCommand("declineReaction", player, e.Version));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }

    // Player 1 (the actor) holds Yogo Adi (Political Open: "give your provinces +2 strength" unless the target is dishonorable); player 0 holds
    // Sakuma and an ally. The action targets the ally, who is one of Sakuma's controller's Personalities.
    private sealed record Rig(MatchState S, CardInstance Sakuma, CardInstance Ally, CardInstance Adi);
    private static Rig Political()
    {
        var s = Table(Phase.Action, 1);
        var sakuma = Add(s, Sakuma, 0, Zone.Home);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Home);
        var adi = Add(s, Adi, 1, Zone.Home);
        return new(s, sakuma, ally, adi);
    }

    // Adi's action against the ally, up to the reaction window with player 0 (Sakuma's controller) holding priority.
    private static MatchEngine OpenWindow(Rig r)
    {
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, "yogo-adi-choice", r.Adi.Id, r.Ally.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 0, "Sakuma's controller holds the first opportunity");
        return e;
    }

    // Sakuma's reaction, taken from the window through the offered option.
    private static void ReactWithSakuma(MatchEngine e, CardInstance sakuma)
    {
        var option = e.QueryActions(0).SingleOrDefault(a => a.Type == "useAbility" && a.CardId == sakuma.Id && a.Command?.AbilityId == Negate);
        Check(option is { Enabled: true }, "Sakuma's Political Reaction was not offered: " + string.Join("; ", e.QueryActions(0).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        Accept(e, option!.Command!);
    }

    private static int ProvinceStrength(MatchEngine e, int player) => e.ProvinceStrengthForTest(player, 0);

    // ---- published shape -----------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var k = CardCatalog.Get(Sakuma);
        Check(!k.ScenarioOnly && k.Type == CardType.Personality && k.Name == "Yoritomo Sakuma" && k.Clan == "Mantis");
        // Verified against the scan (Force 1 / Chi 3 / no Honor Requirement / Cost 8 / Personal Honor 1).
        Eq(k.Force, 1); Eq(k.Chi, 3); Eq(k.GoldCost, 8); Eq(k.PersonalHonor, 1); Check(k.HonorRequirement is null, "no Honor Requirement");
        foreach (var w in new[] { "Mantis Clan", "Courtier", "Bully", "Merchant" }) Check(k.Keywords!.Contains(w), w);
        Eq(k.Text, "Reaction: When paying a Gold Cost, bow Sakuma: Produce 2 Gold.\nPolitical Reaction: After another player's Political action targets one of your Personalities, bow Sakuma: The player may pay 2 Gold. If he did not, negate the action's (remaining) effects.");
        Same(k.Abilities!, [Produce, Negate]);
        Eq(AbilityCatalog.All[Produce].PaymentProduction, 2); Eq(AbilityCatalog.All[Produce].Timing, "Reaction"); Eq(AbilityCatalog.All[Produce].Trigger, "paying-gold-cost");
        Eq(AbilityCatalog.All[Negate].Trigger, "before-action-resolves"); Check(AbilityCatalog.All[Negate].ActionKeywords is ["Political"]);
        var h = CardCatalog.Get(Hesitation);
        Check(!h.ScenarioOnly && h.Type == CardType.Strategy && h.Name == "Unfortunate Hesitation");
        Eq(h.GoldCost, 0); Eq(h.Focus, 2); Same(h.Abilities!, ["unfortunate-hesitation"]);
        Eq(h.Text, "Battle/Open: Target a Personality: His controller may choose to bow him. If he did not choose this, he must pass at his next opportunity this phase to take a Battle, Limited, or Open action.");
        Eq(AbilityCatalog.All["unfortunate-hesitation"].Timing, "BattleOrOpen"); Eq(AbilityCatalog.All["unfortunate-hesitation"].Target, "personality");
        foreach (var id in new[] { Sakuma, Hesitation })
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        // Every wired Political action carries the tag (a published text that prints "Political <Timing>" and is wired), so Sakuma sees them all.
        foreach (var card in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Id != "oracle-l5r-7428" /* Spirit of the Truth: the Political action is the one it grants */ && System.Text.RegularExpressions.Regex.IsMatch(c.Text, @"\bPolitical (Open|Limited|Battle|Reaction)")))
            foreach (var id in card.Abilities!)
                if (AbilityCatalog.All[id].Timing != "Reaction" || card.Id == Sakuma)
                    Check(AbilityCatalog.All[id].ActionKeywords?.Contains("Political") == true || card.Text.Split('\n').Where(l => !System.Text.RegularExpressions.Regex.IsMatch(l, @"\bPolitical (Open|Limited|Battle|Reaction)")).Any(),
                        $"{card.Name}'s {id} prints Political but is not tagged");
        Check(new[] { "yogo-adi-choice", "rin-shame", "kodo-straighten", "jakuei-bow", "slanderer-dishonor", "rumormonger-penalty", "soshi-conditional-bow", "shunya-honor" }
            .All(id => AbilityCatalog.All[id].ActionKeywords?.Contains("Political") == true), "the wired Political actions are tagged");
    }

    // ---- the alternate Gold source ---------------------------------------------------------------------------------
    private static void AlternateGoldSource()
    {
        // Only Border Keep (2 Gold) and Sakuma (2) are ready; a variable cost of up to 4 needs both.
        var s = Table();
        BowAllGold(s, 0);
        var sakuma = Add(s, Sakuma, 0, Zone.Home); // added first: his id sorts before Border Keep's, so only the engine's preference puts Border Keep first
        var bk = Add(s, "training-border-keep", 0, Zone.Home);
        var fixture = Add(s, "training-cost-fixture", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == sakuma.Id).GoldProduced, 2, "the engine says what he can produce");
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == fixture.Id).GoldProduced, null, "an ordinary Personality produces nothing");
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == bk.Id).GoldProduced, 2);
        var charity = Listed(e, 0, Charity, fixture.Id)!;
        var quote = charity.Gold!;
        Eq(quote.Kind, GoldCostKind.Variable); Eq(quote.MaxAmount, 4, "the ceiling counts him");
        Check(quote.PaymentsByAmount![4].Any(p => p.SourceIds.ToHashSet().SetEquals([bk.Id, sakuma.Id])), "paying 4 uses both");
        Check(quote.PaymentsByAmount[3].All(p => p.SourceIds.Contains(sakuma.Id)), "3 cannot be paid without him");
        // When printed producers cover the cost he is not suggested (bowing him costs his controller his reactions), though he is a legal alternative.
        var two = quote.PaymentsByAmount[2];
        Same(two[0].SourceIds, [bk.Id]); Check(two.Any(p => p.SourceIds.SequenceEqual([sakuma.Id])), "he is still offered as an alternative for 2");
        // Paying 3 with both: both bow, the cost is charged, his use is spent, and the log says why he bowed.
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: fixture.Id, AbilityId: Charity, GoldAmount: 3, SourceIds: [bk.Id, sakuma.Id]));
        Check(Now(e, sakuma.Id).Bowed && Now(e, bk.Id).Bowed, "both bowed");
        Check(e.State.Events.Any(x => x.Type == "goldSourceUsed" && x.CardId == sakuma.Id && x.Message.Contains("2 Gold")), "his use is announced");
        Check(e.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 3 Gold") && e.State.Events.Last(x => x.Type == "payment").Message.Contains("producing 4"), "the excess is lost like any payment");
        Eq(Now(e, sakuma.Id).AbilityUses[Produce], e.State.TurnNumber, "the Reaction is spent for the turn");
        // A bowed Sakuma is not a source; and even straightened again the same turn he cannot be used twice (the use is spent); next turn he can.
        Pass(e, 1);
        var after = e.GetView(0).Players[0].Home.Single(c => c.Id == sakuma.Id);
        Eq(after.GoldProduced, null, "spent: he produces nothing until next turn");
        e = Edit(e, st => { st.Cards[sakuma.Id].Bowed = false; st.Cards[bk.Id].Bowed = false; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.Phase = Phase.Action; st.PendingChoice = null; });
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: fixture.Id, AbilityId: Charity, GoldAmount: 3, SourceIds: [bk.Id, sakuma.Id]));
        e = Edit(e, st => st.TurnNumber += 2);
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == sakuma.Id).GoldProduced, 2, "a new turn");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: fixture.Id, AbilityId: Charity, GoldAmount: 3, SourceIds: [bk.Id, sakuma.Id]));
        // Bowed and not his controller's: refused.
        s = Table();
        BowAllGold(s, 0);
        bk = Add(s, "training-border-keep", 0, Zone.Home);
        sakuma = Add(s, Sakuma, 0, Zone.Home, bowed: true);
        var enemySakuma = Add(s, Sakuma, 1, Zone.Home);
        fixture = Add(s, "training-cost-fixture", 0, Zone.Home);
        e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: fixture.Id, AbilityId: Charity, GoldAmount: 3, SourceIds: [bk.Id, sakuma.Id]));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: fixture.Id, AbilityId: Charity, GoldAmount: 3, SourceIds: [bk.Id, enemySakuma.Id]));
        Eq(Listed(e, 0, Charity, fixture.Id)!.Gold!.MaxAmount, 2, "a bowed Sakuma adds nothing to the ceiling");
    }

    private static void EitherOrQuoteSources()
    {
        var r = Political();
        BowAllGold(r.S, 1);
        var mine = Add(r.S, "oracle-l5r-10614", 1, Zone.Home); // Jade Mine: bow for 2, or its second way for 3
        var e = OpenWindow(r);
        ReactWithSakuma(e, r.Sakuma);
        var pay = Answers(e, 1).Single(a => a.Command?.Choice == "pay");
        QuoteCarriesSources(pay.Gold!, "Either/Or action quote");
        var view = e.GetView(1).PendingEitherOr!.Options.Single(o => o.Key == "pay").Gold!;
        QuoteCarriesSources(view, "Either/Or view quote");
        var methods = view.Sources!.Where(m => m.CardId == mine.Id).ToArray();
        Eq(methods.Length, 2, "both of Jade Mine's ways are offered");
        Check(methods.Any(m => m.Id == mine.Id + "#alt" && m.Produced == 3), "its published alternative token is listed");
        Check(!e.State.Cards[mine.Id].Bowed, "nothing is bowed yet");
        Accept(e, new GameCommand("chooseEitherOr", 1, e.Version, CardId: r.Sakuma.Id, Choice: "pay", SourceIds: [mine.Id + "#alt"]));
        Check(Now(e, mine.Id).Bowed && Now(e, mine.Id).StraightenHolds.Count == 1, "the engine accepted the token and bowed Jade Mine under its hold");
    }

    // ---- the Political Reaction ---------------------------------------------------------------------------------------
    private static void PoliticalReactionPaid()
    {
        var r = Political();
        var e = OpenWindow(r);
        var before = ProvinceStrength(e, 1);
        ReactWithSakuma(e, r.Sakuma);
        Check(Now(e, r.Sakuma.Id).Bowed, "bowing Sakuma is the cost");
        // The acting player -- not Sakuma's controller -- decides, and holds priority for it.
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice?.PlayerId, 1); Eq(e.State.PriorityPlayerId, 1);
        var decider = e.GetView(1).PendingEitherOr!; var waiting = e.GetView(0).PendingEitherOr!;
        Eq(decider.ChoiceId, "sakuma-pay-or-negate"); Eq(decider.SourceCardId, r.Sakuma.Id); Eq(decider.SourceName, "Yoritomo Sakuma"); Eq(decider.SourceDefinitionId, Sakuma);
        Eq(decider.ActingPlayerId, 1); Eq(decider.DeciderPlayerId, 1); Check(decider.ViewerDecides && !waiting.ViewerDecides, "only the deciding viewer decides");
        Same(decider.Options.Select(o => o.Key), ["pay", "negate"]); Same(decider.Options.Select(o => o.Kind), [EitherOrKind.Avoid, EitherOrKind.Accept]);
        Check(decider.Options.All(o => o.Legal && o.Reason is null), "both are available");
        Eq(decider.Options[0].Gold!.Cost, 2); Check(decider.Options[0].Gold!.Payments.Length > 0, "the quote carries the engine's suggested sources");
        Check(decider.Options[1].Gold is null, "the consequence costs nothing");
        Check(waiting.Options.All(o => o.Gold is null), "the waiting player sees the options, not the quote");
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "Sakuma's controller waits");
        Eq(e.GetView(1).PendingChoice!.Prompt, "Yoritomo Sakuma: pay 2 Gold, or your action's remaining effects are negated.");
        var offers = Answers(e, 1);
        Check(offers.All(a => a.Enabled && a.Command is { Type: "chooseEitherOr", PlayerId: 1 }), "complete, enabled commands"); Check(offers[0].Command!.SourceIds is { Length: > 0 }, "the payment is pre-filled");
        var payers = offers[0].Command!.SourceIds!;
        Answer(e, 1, "pay");
        Check(payers.All(id => Now(e, id).Bowed), "the sources were bowed"); Check(e.State.Events.Any(x => x.Type == "payment" && x.PlayerId == 1 && x.Message.Contains("paid 2 Gold")), "paid through the ordinary payment path");
        // The window is back, priority flipped as after any reaction, and nothing is negated.
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 1); Check(!e.State.DeferredActionNegated && e.State.PendingEitherOr is null);
        Eq(ProvinceStrength(e, 1), before, "the action has not resolved yet");
        Pass(e, 1); Pass(e, 0);
        Eq(ProvinceStrength(e, 1), before + 2, "the action resolved in full"); Check(e.State.PendingChoice is null);
        Eq(e.State.PriorityPlayerId, 0, "priority passed on as after any action");
    }

    private static void PoliticalReactionNegated()
    {
        var r = Political();
        var e = OpenWindow(r);
        var before = ProvinceStrength(e, 1);
        ReactWithSakuma(e, r.Sakuma);
        Answer(e, 1, "negate");
        Check(e.State.DeferredActionNegated, "the deferred action is marked negated"); Eq(e.State.PendingChoice?.Type, "beforeActionReaction");
        Check(e.State.Events.Any(x => x.Type == "actionNegated"), "public");
        Pass(e, 1); Pass(e, 0);
        Eq(ProvinceStrength(e, 1), before, "the effects did not happen");
        Check(!e.State.DeferredActionNegated && e.State.PendingChoice is null && e.State.PendingNegatableAbility is null, "everything is cleared");
        Eq(Now(e, r.Adi.Id).AbilityUses["yogo-adi-choice"], e.State.TurnNumber, "the action was still taken: its use is spent");
        Check(e.State.Events.Any(x => x.Type == "abilityUsed" && x.Message.Contains("(negated)")), "and logged as negated");
        Eq(e.State.PriorityPlayerId, 0, "priority passed on as after any action");
        // It cannot be taken again this turn.
        Pass(e, 0);
        var again = Listed(e, 1, "yogo-adi-choice", r.Adi.Id)!;
        Check(!again.Enabled, "spent");
    }

    private static void CannotPayAutoNegates()
    {
        var r = Political();
        BowAllGold(r.S, 1);
        var e = OpenWindow(r);
        var before = ProvinceStrength(e, 1);
        ReactWithSakuma(e, r.Sakuma);
        Check(e.State.PendingEitherOr is null && e.State.PendingChoice?.Type == "beforeActionReaction", "no decision: the window is simply back");
        Check(e.State.DeferredActionNegated, "negated"); Eq(e.State.PriorityPlayerId, 1, "priority flipped as after any reaction");
        Check(e.State.Events.Any(x => x.Type == "eitherOrAutoResolved" && x.Message.Contains("Do not pay")), "announced");
        Pass(e, 1); Pass(e, 0);
        Eq(ProvinceStrength(e, 1), before, "negated");
        // A Protected performer cannot be negated: nothing to pay to avoid it, so again no decision -- and the action goes ahead.
        r = Political();
        r.Adi.Protections = [new("test-fixture", "Turn")];
        e = OpenWindow(r); before = ProvinceStrength(e, 1);
        ReactWithSakuma(e, r.Sakuma);
        Check(e.State.PendingEitherOr is null && !e.State.DeferredActionNegated, "protected: not negated, no decision");
        Check(e.State.Events.Any(x => x.Type == "actionProtected"), "Protection is announced");
        Pass(e, 1); Pass(e, 0);
        Eq(ProvinceStrength(e, 1), before + 2, "the protected action resolved");
    }

    private static void WhenSakumaMayReact()
    {
        bool Offered(MatchEngine e) => e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == Negate && a.Enabled);
        // Yes: Political action, another player's, targeting one of his controller's Personalities (Sakuma himself included).
        var r = Political(); var e = OpenWindow(r);
        Check(Offered(e), "Political action, another player's, at his controller's Personality");
        r = Political();
        e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, "yogo-adi-choice", r.Adi.Id, r.Sakuma.Id));
        Check(Offered(e), "Sakuma is one of his controller's Personalities");
        // A Political Strategy: the card's own keyword makes its action Political.
        r = Political(); var strategy = Add(r.S, "training-political-strategy", 1, Zone.Hand);
        e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, Bow, strategy.Id, r.Ally.Id));
        Check(Offered(e), "an action of a Political card is Political");
        // No: a non-Political action of the same shape (an ordinary Open bow): nothing reacts to it, so no window opens at all.
        r = Political(); var bower = Add(r.S, "training-skin-donor", 1, Zone.Home);
        e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, Bow, bower.Id, r.Ally.Id));
        Check(e.State.PendingChoice is null && Now(e, r.Ally.Id).Bowed, "a non-Political action is not reacted to by Sakuma: it simply resolves");
        // No: his controller's own Political action targeting his own Personality.
        var s = Table(Phase.Action, 0);
        var sakuma = Add(s, Sakuma, 0, Zone.Home); var own = Add(s, "training-crane-samurai", 0, Zone.Home); var adi = Add(s, Adi, 0, Zone.Home);
        e = MatchEngine.FromTestState(s);
        var targets = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "yogo-adi-choice").SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Length == 0, "Yogo Adi only targets ANOTHER player's Personality");
        // ...and one that CAN target his own controller's Personality (Shosuro Rin shames any honorable Personality): still no reaction for Sakuma.
        var rin = Add(s, "oracle-l5r-7151", 0, Zone.Home);
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "rin-shame", rin.Id, own.Id));
        Eq(e.State.PendingChoice?.Type, "rinShame", "his controller's own Political action opens no window for Sakuma");
        // No: a Political action whose target is not a Personality (Agasha Kodo straightens a Holding).
        r = Political(); var kodo = Add(r.S, "oracle-l5r-142", 1, Zone.Home); var holding = Add(r.S, "training-marketplace", 0, Zone.Home, bowed: true);
        e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, "kodo-straighten", kodo.Id, holding.Id));
        Check(e.State.PendingChoice is null, "a Holding target opens no window for Sakuma");
        // No: a bowed Sakuma.
        r = Political(); r.Sakuma.Bowed = true;
        e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, "yogo-adi-choice", r.Adi.Id, r.Ally.Id));
        Check(e.State.PendingChoice is null, "a bowed Sakuma cannot react: no window opens");
    }

    private static void NegatedActionIsStillTaken()
    {
        // A Political Strategy that costs 2 Gold: the cost is paid, the card leaves the hand and the use is spent, but the target does not bow.
        var r = Political();
        var strategy = Add(r.S, "training-political-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(r.S);
        var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == strategy.Id && a.Command?.AbilityId == Bow);
        Eq(option.Gold!.Cost, 2);
        var paying = option.Command!.SourceIds!;
        Accept(e, option.Command with { TargetId = r.Ally.Id });
        Eq(Now(e, strategy.Id).Zone, Zone.Resolving, "in the resolution area (issue #917), not the hand: the action is deferred whole while the window is open");
        Check(paying.All(id => !Now(e, id).Bowed), "and nothing is paid yet");
        ReactWithSakuma(e, r.Sakuma);
        Answer(e, 1, "negate");
        Pass(e, 1); Pass(e, 0);
        Check(!Now(e, r.Ally.Id).Bowed, "the effect (bow the target) did not happen");
        Check(paying.All(id => Now(e, id).Bowed), "the Gold cost was paid");
        Eq(Now(e, strategy.Id).Zone, Zone.FateDiscard, "the Strategy was played and discarded");
        Eq(Now(e, strategy.Id).AbilityUses[Bow], e.State.TurnNumber, "its use is spent");
        Eq(e.State.PriorityPlayerId, 0);
    }

    private static void EitherOrViewAndPrivacy()
    {
        var r = Political();
        var secret = Add(r.S, "training-item", 1, Zone.Hand); var secret2 = Add(r.S, "training-follower", 1, Zone.Hand);
        var e = OpenWindow(r);
        ReactWithSakuma(e, r.Sakuma);
        // Nothing crosses that should not: the waiting player's view lists no quote and none of the decider's hand.
        var waitingJson = JsonSerializer.Serialize(e.GetView(0));
        HiddenIdsAbsent(e.GetView(0), [secret.Id, secret2.Id]);
        Check(e.GetView(0).PendingEitherOr!.Options.All(o => o.Gold is null), "no quote for the waiting player");
        Check(!waitingJson.Contains("\"Payments\""), "and no payment suggestion of the decider's sources in his view");
        Check(e.GetView(1).PendingEitherOr!.Options[0].Gold!.Payments.Length > 0, "the decider's own quote is there");
        // No decision outside an open Either/Or.
        Answer(e, 1, "negate");
        Check(e.GetView(0).PendingEitherOr is null && e.GetView(1).PendingEitherOr is null, "gone once answered");
        // Concede is always available (it cuts through), and a stale or foreign answer is refused.
        Check(e.QueryActions(1).Any(a => a.Type == "concede"));
    }

    private static void ThreeWayChoice()
    {
        MatchEngine Rig3(Action<MatchState>? more = null)
        {
            var s = Table(Phase.Action, 0);
            var src = Add(s, "training-either-or-source", 0, Zone.Home);
            more?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        void Use(MatchEngine e) => Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == ThreeWay).Command!);

        // Pay: player 1 pays 2 Gold; nothing else happens; priority passes on.
        var e = Rig3(); var h0 = e.State.Players[0].Honor; var h1 = e.State.Players[1].Honor;
        Use(e);
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice?.PlayerId, 1); Eq(e.State.PriorityPlayerId, 1);
        var view = e.GetView(1).PendingEitherOr!;
        Same(view.Options.Select(o => o.Key), ["pay", "honor", "accept"]); Same(view.Options.Select(o => o.Kind), [EitherOrKind.Avoid, EitherOrKind.Avoid, EitherOrKind.Accept]);
        Eq(view.ActingPlayerId, 0); Eq(view.DeciderPlayerId, 1); Eq(view.AffectedCardId, null);
        Answer(e, 1, "pay");
        Eq(e.State.Players[0].Honor, h0); Eq(e.State.Players[1].Honor, h1); Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 1, "resolved; priority passes on as after any action");
        // Honor: he loses 2.
        e = Rig3(); Use(e); Answer(e, 1, "honor");
        Eq(e.State.Players[1].Honor, h1 - 2); Eq(e.State.Players[0].Honor, h0);
        // Accept: the acting player gains 2.
        e = Rig3(); Use(e); Answer(e, 1, "accept");
        Eq(e.State.Players[0].Honor, h0 + 2); Eq(e.State.Players[1].Honor, h1);
        Check(e.State.Events.Any(x => x.Type == "eitherOrChosen" && x.PlayerId == 1), "the choice is public");
        // Unavailable options are listed disabled with the reason: no Gold ready, no Honor to lose.
        e = Rig3(st => { BowAllGold(st, 1); st.Players[1].Honor = 0; });
        Use(e);
        Check(e.State.PendingEitherOr is null, "only 'accept' is legal: nothing to choose"); Eq(e.State.Players[0].Honor, h0 + 2, "so it was taken automatically");
        Check(e.State.Events.Any(x => x.Type == "eitherOrAutoResolved"), "and announced");
        e = Rig3(st => BowAllGold(st, 1));
        Use(e);
        var opts = e.QueryActions(1).Where(a => a.Type == "chooseEitherOr").ToArray();
        Eq(opts.Length, 3); Check(!opts[0].Enabled && opts[0].Reason == "You cannot produce 2 Gold." && opts[0].Command is null, "'pay' is disabled, with the reason");
        Check(opts[1].Enabled && opts[2].Enabled);
        Check(!e.GetView(1).PendingEitherOr!.Options[0].Legal && e.GetView(1).PendingEitherOr!.Options[0].Reason == "You cannot produce 2 Gold.", "the view says the same");
        // Every offered command validates on a fresh engine that reached the same position.
        foreach (var option in e.QueryActions(1).Where(a => a.Type == "chooseEitherOr" && a.Enabled))
        {
            var f = Rig3(st => BowAllGold(st, 1)); Use(f);
            Accept(f, option.Command!);
        }
        var g = Rig3(); Use(g);
        foreach (var option in g.QueryActions(1).Where(a => a.Type == "chooseEitherOr" && a.Enabled))
        {
            var f = Rig3(); Use(f);
            Accept(f, option.Command!);
        }
    }

    private static void EitherOrRejections()
    {
        var s = Table(Phase.Action, 0);
        Add(s, "training-either-or-source", 0, Zone.Home);
        var sacrificial = Add(s, "training-skin-donor", 1, Zone.Home); sacrificial.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == ThreeWay).Command!);
        var pay = Answers(e, 1)[0].Command!;
        Reject(e, pay with { PlayerId = 0 });                                   // not the deciding player
        Reject(e, pay with { Choice = "bow" });                                 // unknown option
        Reject(e, pay with { Choice = null });                                  // no option
        Reject(e, pay with { SourceIds = [] });                                 // payment short
        Reject(e, pay with { SourceIds = ["nonexistent"] });                    // not a Gold source
        Reject(e, pay with { ExpectedVersion = e.Version - 1 });                // stale
        Reject(e, pay with { Choice = "honor" });                               // a free option with sources it does not need
        Reject(e, new GameCommand("pass", 1, e.Version));                       // not an answer
        Reject(e, new GameCommand("orderSeppuku", 1, e.Version, CardId: sacrificial.Id)); // an Open action is not available while a decision is pending
        Check(!e.QueryActions(1).Any(a => a.Type == "orderSeppuku"), "Seppuku is not offered mid-choice");
        // A disabled option cannot be forced through a hand-built command.
        var s2 = Table(Phase.Action, 0); Add(s2, "training-either-or-source", 0, Zone.Home); BowAllGold(s2, 1);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, f.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == ThreeWay).Command!);
        Reject(f, new GameCommand("chooseEitherOr", 1, f.Version, Choice: "pay", SourceIds: []));
    }

    // ---- Unfortunate Hesitation ---------------------------------------------------------------------------------------
    // Player 0 (active) holds the Strategy; player 1 controls the target (and a fixture whose free Open action shows what an opportunity offers).
    private sealed record Hes(MatchState S, CardInstance Card, CardInstance Target, CardInstance Fixture, CardInstance Own);
    private static Hes Hesitate(Phase phase = Phase.Action)
    {
        var s = Table(phase, 0);
        var card = Add(s, Hesitation, 0, Zone.Hand);
        if (phase == Phase.Battle) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = phase == Phase.Battle ? Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0) : Add(s, "training-lion-samurai", 1, Zone.Home);
        var fixture = Add(s, "training-cost-fixture", 1, Zone.Home);
        var own = Add(s, "training-cost-fixture", 0, Zone.Home);
        return new(s, card, target, fixture, own);
    }

    private static void HesitationBow()
    {
        var h = Hesitate();
        var e = MatchEngine.FromTestState(h.S);
        Accept(e, Offer(e, 0, "unfortunate-hesitation", h.Card.Id, h.Target.Id));
        Eq(Now(e, h.Card.Id).Zone, Zone.FateDiscard, "spent as it was played"); Check(Now(e, h.Card.Id).FaceUp, "a public discard");
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice?.PlayerId, 1, "the Personality's controller decides"); Eq(e.State.PriorityPlayerId, 1);
        var view = e.GetView(0).PendingEitherOr!;
        Eq(view.SourceName, "Unfortunate Hesitation"); Eq(view.AffectedCardId, h.Target.Id); Eq(view.AffectedName, "Lion training samurai"); Check(!view.ViewerDecides, "the player who played it waits");
        Same(view.Options.Select(o => o.Key), ["bow", "pass"]);
        Check(view.Options.All(o => o.Gold is null && o.Legal), "no Gold is involved");
        Check(!Now(e, h.Target.Id).Bowed, "nothing has happened to him yet");
        Answer(e, 1, "bow");
        Check(Now(e, h.Target.Id).Bowed, "he bowed"); Eq(e.State.ForcedPasses.Count, 0, "and nothing is owed");
        Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 1, "priority passes on as after any action");
        // He may act freely at once.
        var free = Listed(e, 1, Charity, h.Fixture.Id)!;
        Check(free.Enabled, free.Reason ?? "");
    }

    private static void HesitationForcedPass()
    {
        var h = Hesitate();
        var e = MatchEngine.FromTestState(h.S);
        Accept(e, Offer(e, 0, "unfortunate-hesitation", h.Card.Id, h.Target.Id));
        Answer(e, 1, "pass");
        Check(!Now(e, h.Target.Id).Bowed, "he did not bow"); Eq(e.State.ForcedPasses.Count, 1); Eq(e.State.ForcedPasses[0].PlayerId, 1);
        Eq(e.State.PriorityPlayerId, 1, "it is his opportunity");
        // What he is offered: pass (and concede); every action listed, disabled, with the reason -- never hidden.
        var actions = e.QueryActions(1);
        Check(actions.Any(a => a.Type == "pass" && a.Enabled), "pass stays legal");
        var charity = Listed(e, 1, Charity, h.Fixture.Id)!;
        Check(!charity.Enabled && charity.Reason == "Taking any action but passing is barred by Unfortunate Hesitation.", charity.Reason ?? "");
        Check(actions.Where(a => a.Type is "useAbility" or "playStrategy" or "recruitReserve" or "equip").All(a => !a.Enabled), "no Battle, Limited or Open action");
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: h.Fixture.Id, AbilityId: Charity, GoldAmount: 0, SourceIds: []));
        // The pass settles it.
        Pass(e, 1);
        Eq(e.State.ForcedPasses.Count, 0, "settled"); Check(e.State.Events.Any(x => x.Type == "forcedPassTaken" && x.PlayerId == 1), "announced");
        Eq(e.State.PriorityPlayerId, 0);
        // His next opportunity is free again: the opponent takes an action of their own, and it is his turn to act.
        Accept(e, Take(e, 0, Charity, h.Own.Id));
        var after = Listed(e, 1, Charity, h.Fixture.Id)!;
        Check(after.Enabled, "no longer restricted");
    }

    private static void HesitationAlreadyBowedAndOwn()
    {
        // Already bowed: he cannot choose to bow him, so there is nothing to choose and the pass is simply owed.
        var h = Hesitate(); h.Target.Bowed = true;
        var e = MatchEngine.FromTestState(h.S);
        Accept(e, Offer(e, 0, "unfortunate-hesitation", h.Card.Id, h.Target.Id));
        Check(e.State.PendingEitherOr is null && e.State.PendingChoice is null, "no decision opens");
        Eq(e.State.ForcedPasses.Count, 1); Eq(e.State.ForcedPasses[0].PlayerId, 1); Eq(e.State.PriorityPlayerId, 1, "priority passed on as after any action");
        Check(e.State.Events.Any(x => x.Type == "eitherOrAutoResolved"), "announced");
        // His own Personality: the ACTING player is his controller, and so the one who decides.
        h = Hesitate();
        var mine = Add(h.S, "training-lion-samurai", 0, Zone.Home);
        e = MatchEngine.FromTestState(h.S);
        Accept(e, Offer(e, 0, "unfortunate-hesitation", h.Card.Id, mine.Id));
        Eq(e.State.PendingChoice?.PlayerId, 0); Eq(e.State.PriorityPlayerId, 0);
        Check(e.GetView(0).PendingEitherOr!.ViewerDecides && !e.GetView(1).PendingEitherOr!.ViewerDecides);
        Answer(e, 0, "pass");
        Eq(e.State.ForcedPasses[0].PlayerId, 0); Eq(e.State.PriorityPlayerId, 1, "the opponent acts next");
        Pass(e, 1);
        // Back to the player who owes the pass: only pass is legal for him.
        var own = Listed(e, 0, Charity, h.Own.Id)!;
        Check(!own.Enabled && own.Reason!.Contains("barred by Unfortunate Hesitation"), own.Reason ?? "");
        Pass(e, 0);
        Eq(e.State.ForcedPasses.Count, 0); Eq(e.State.Phase, Phase.AttackDeclaration, "two passes in a row ended the Action Phase");
    }

    // Battle: player 1 has a Battle-action Personality at the battlefield (it bows an enemy Personality there); player 0 a Personality there.
    private static (MatchState S, CardInstance Bower, CardInstance Enemy) BattleRig(int priority = 1)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = priority;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        return (s, bower, mine);
    }

    private const string ForcedReason = "Taking any action but passing is barred by Unfortunate Hesitation.";

    private static void HesitationPhaseScope()
    {
        var battleAction = "training-bow-at-battle";
        // An entry from an EARLIER printed phase (the Action Phase) does not bind in the Attack Phase: it lapsed.
        var (s, bower, enemy) = BattleRig();
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.Action), "stale"));
        var e = MatchEngine.FromTestState(s);
        Check(Listed(e, 1, battleAction, bower.Id)!.Enabled, "a lapsed entry does not restrict");
        Accept(e, Offer(e, 1, battleAction, bower.Id, enemy.Id));
        // The whole Attack Phase is one phase: an entry made at attack declaration binds in the Battle step.
        (s, bower, enemy) = BattleRig();
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.AttackDeclaration), "made-at-declaration"));
        e = MatchEngine.FromTestState(s);
        var listed = Listed(e, 1, battleAction, bower.Id)!;
        Check(!listed.Enabled && listed.Reason == ForcedReason, "bound: " + listed.Reason);
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: bower.Id, TargetId: enemy.Id, AbilityId: battleAction));
        // Another player's entry does not bind him.
        (s, bower, enemy) = BattleRig();
        s.ForcedPasses.Add(new(0, PrintedPhases.Instance(s.TurnNumber, Phase.Battle), "someone-else"));
        e = MatchEngine.FromTestState(s);
        Check(Listed(e, 1, battleAction, bower.Id)!.Enabled, "the other player's debt is not his");
        // One pass settles one owed pass: two entries, one pass, one left -- and it still binds his next opportunity.
        (s, bower, enemy) = BattleRig();
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.Battle), "first"));
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.Battle), "second"));
        e = MatchEngine.FromTestState(s);
        Pass(e, 1);
        Eq(e.State.ForcedPasses.Count, 1, "one pass, one debt");
        Eq(e.State.PriorityPlayerId, 0);
        // The state is cleaned when the phase moves on: an entry left over from the Action Phase is gone once the Dynasty Phase ends the turn.
        s = Table(Phase.Dynasty, 0);
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.Action), "stale"));
        e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("endDynasty", 0, e.Version));
        Eq(e.State.ForcedPasses.Count, 0, "lapsed entries are dropped when a new phase begins");
    }

    private static void HesitationScope()
    {
        // A Reaction is not a Battle, Limited or Open action: player 1 owes a pass, and still may use his reaction in a window player 0's action opens.
        var s = Table(Phase.Action, 0);
        var actor = Add(s, "training-skin-donor", 0, Zone.Home);
        var suppressor = Add(s, "training-before-action-suppressor", 1, Zone.Home);
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.Action), "owed"));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Bow, actor.Id, suppressor.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 1);
        var reaction = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "training-before-action-suppress");
        Check(reaction.Enabled, "the reaction stays usable while a pass is owed: " + reaction.Reason);
        Accept(e, reaction.Command!);
        Eq(e.State.ForcedPasses.Count, 1, "using a reaction does not settle the debt");
        // The Attack Phase's own assignment decisions are not actions: declaring an attack (or not) is untouched.
        s = Table(Phase.AttackDeclaration, 0);
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Home);
        s.ForcedPasses.Add(new(0, PrintedPhases.Instance(s.TurnNumber, Phase.AttackDeclaration), "owed"));
        e = MatchEngine.FromTestState(s);
        var declare = e.QueryActions(0).Where(a => a.Type == "declareAttack" && a.Enabled).ToArray();
        Check(declare.Length > 0 && e.QueryActions(0).Any(a => a.Type == "pass" && a.Enabled), "declaring an attack, and passing, stay available");
        Accept(e, declare[0].Command!);
        Eq(e.State.ForcedPasses.Count, 1, "declaring is not the pass that settles it");
        // As a Battle action at the battlefield: the controller of the target declines, and his Battle actions are then disabled while pass stays.
        var h = Hesitate(Phase.Battle);
        var bower = Add(h.S, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        e = MatchEngine.FromTestState(h.S);
        Accept(e, Offer(e, 0, "unfortunate-hesitation", h.Card.Id, h.Target.Id));
        Answer(e, 1, "pass");
        var actions = e.QueryActions(1);
        var owedBattle = actions.Single(a => a.Type == "useAbility" && a.CardId == bower.Id && a.Label.StartsWith(AbilityCatalog.All["training-bow-at-battle"].Label));
        Check(!owedBattle.Enabled && owedBattle.Reason == ForcedReason, "his Battle action is disabled: " + owedBattle.Reason);
        Check(actions.Any(a => a.Type == "pass" && a.Enabled), "pass is legal");
        Pass(e, 1);
        Eq(e.State.ForcedPasses.Count, 0); Eq(e.State.PriorityPlayerId, 0);
    }

    private static void EitherOrShowcase()
    {
        var e = MatchEngine.CreateEitherOrShowcase();
        Eq(e.State.Phase, Phase.Action); Eq(e.State.PriorityPlayerId, 0);
        CardInstance Find(int owner, string def) => e.State.Cards.Values.First(c => c.OwnerId == owner && c.DefinitionId == def);
        var adi = Find(1, Adi); var hes = Find(0, Hesitation); var sakuma = Find(0, Sakuma);
        Check(e.State.Players[0].Hand.Contains(hes.Id), "Hesitation is in player 0's hand");
        // Player 0 plays Unfortunate Hesitation on Yogo Adi: player 1 decides (and bows him).
        Accept(e, Offer(e, 0, "unfortunate-hesitation", hes.Id, adi.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice?.PlayerId, 1);
        Answer(e, 1, "bow");
        // Player 1 then uses the three-option fixture: this time the opposing player is player 0.
        Accept(e, Take(e, 1, ThreeWay, Find(1, "training-either-or-source").Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice?.PlayerId, 0, "the opposing player is player 0 this time");
        Check(e.QueryActions(0).Any(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "pay"), "player 0 may pay");
        Answer(e, 0, "accept");
        // Player 0 passes; Yogo Adi (bowed by the choice, so the position after a turn is needed) cannot act, but the table holds Sakuma for player 1's Political actions.
        Check(sakuma.Id.Length > 0);
        var replay = MatchEngine.Replay(e.ExportReplay());
        Eq(System.Text.Json.JsonSerializer.Serialize(replay.State), System.Text.Json.JsonSerializer.Serialize(e.State), "the replay reproduces the state");
    }

    private static void RinShameUnchanged()
    {
        var s = Table(Phase.Action, 0);
        var rin = Add(s, "oracle-l5r-7151", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "rin-shame", rin.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "rinShame"); Eq(e.State.PendingChoice?.PlayerId, 1);
        Check(e.GetView(1).PendingEitherOr is null, "the bespoke choice does not use the general one");
        Same(e.QueryActions(1).Where(a => a.Type == "chooseRinShame").Select(a => a.Command!.Choice!), ["dishonor", "decline"]);
    }
}
