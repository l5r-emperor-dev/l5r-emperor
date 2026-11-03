using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Favor cluster (issue #667, item-wave-45): Mempo of the Amethyst Champion and the "after you discard the Imperial Favor" window it needs. Every scenario is an explicit position on the economy
// table (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the current battlefield), driven only through QueryActions and the commands the engine offers.
internal static class FavorMempoTests
{
    private const string Mempo = FavorMempoAbilities.Mempo, Take = FavorMempoAbilities.TakeFavor, Confusion = "oracle-l5r-1450", Crane = "training-crane-samurai", Lion = "training-lion-samurai";
    private const string Wave = "item-wave-45";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Options(e, player, ability, cardId);

    // A real Stronghold, so the rulebook player abilities (the Favor actions) are offered at all.
    private static MatchState Published(MatchState s) { foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }

    public static void Run(TestRunner runner) => runner.Run("favor-mempo", [
        ("Published shape: Mempo of the Amethyst Champion carries the printed stats, keywords, text, attach restriction and Reaction of its scan, ships art, names the Promotional-Emperor printing and is in the wave's manifest", PublishedShape),
        ("Attaches to an Amethyst Champion ignoring costs: free for that Champion only", AttachesFree),
        ("Any unit: the controller's rulebook Favor Political Battle action may target any player's Personality, attacking or not; nobody else's action is widened", AnyUnitTarget),
        ("One additional Favor use per turn: the controller's rulebook Favor action may be used twice, never three times; another player, a player without Mempo and a non-Favor ability are not widened", ExtraUse),
        ("After you discard the Imperial Favor: the Reaction takes it back, may be declined, and is offered only to the player who discarded it, while Mempo is attached", TakeBackReaction),
        ("A negated Favor action still discarded the Imperial Favor: the Reaction is offered after Confusion at Court negated it, and the negation is unharmed", AfterNegation),
        ("Every Favor discard goes through one helper: a cost is recorded on the action (the rulebook actions included), an Event's effect is not, and a repeat records once per player", DiscardSites),
    ]);

    private static string RepoRoot() => Batch336Support.RepoRoot();

    // ---- shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", Wave + ".txt"));
        Check(listed.SequenceEqual([Mempo]), "the manifest is exactly Mempo");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", Wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, 1, "a provenance row");
        Eq(sources[0].GetProperty("printing_set")[0].GetString(), "Promotional&ndash;Emperor", "the Promotional-Emperor printing");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", Mempo + ".jpg")), "it ships art");
        var d = CardCatalog.Get(Mempo);
        Check(d.Name == "Mempo of the Amethyst Champion" && d.Type == CardType.Item && d.Clan == "Unaligned" && d.GoldCost == 3 && d.Force == 0 && d.Chi == 0 && d.Focus == 4, "printed stats");
        Check(d.Keywords!.SequenceEqual(["Unique"]), "printed keywords");
        Check(d.Abilities!.SequenceEqual([Take]) && d.Effect == FavorMempoAbilities.MempoEffect && d.AttachRequiresKeyword == "Amethyst Champion", "abilities, effect and attach restriction");
        Check(d.Text.StartsWith("Attaches to an Amethyst Champion ignoring costs.") && d.Text.EndsWith("Reaction: After you discard the Imperial Favor: Take the Imperial Favor."), "printed text");
        var reaction = AbilityCatalog.All[Take];
        Check(reaction.Timing == "Reaction" && reaction.Trigger == AfterActionReactionTriggers.AfterActionResolved, "a Reaction in the after-action window");
    }

    // ---- attach --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card, CardInstance Host) HandRig(params string[] hostKeywords)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Kw(Add(s, Crane, 0, Zone.Home), hostKeywords); var card = Add(s, Mempo, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), card, host);
    }

    private static GameCommand Equip(MatchEngine e, CardInstance card, CardInstance host, int gold) =>
        new("equip", 0, e.Version, CardId: card.Id, SourceIds: gold == 0 ? [] : e.PaymentOptions(0, gold).First().SourceIds, TargetId: host.Id);

    private static void AttachesFree()
    {
        var (e, card, host) = HandRig("Amethyst Champion");
        var bowedBefore = e.State.Cards.Values.Count(c => c.Bowed);
        Accept(e, Equip(e, card, host, 0));
        Eq(e.State.Cards[card.Id].Zone, Zone.Attached, "Mempo attached for nothing");
        Eq(e.State.Cards.Values.Count(c => c.Bowed), bowedBefore, "nothing was bowed to pay for it");
        var (e2, c2, h2) = HandRig("Ruby Champion");
        Reject(e2, Equip(e2, c2, h2, 0)); Reject(e2, Equip(e2, c2, h2, 3));
        var (e3, c3, h3) = HandRig();
        Reject(e3, Equip(e3, c3, h3, 0));
        Check(!e3.QueryActions(0).Any(a => a.Type == "equip" && a.Enabled && a.CardId == c3.Id && (a.TargetIds?.Contains(h3.Id) ?? false)), "a Personality who is no Amethyst Champion is not offered");
    }

    // ---- any unit ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Attacker, CardInstance Defender, CardInstance Second) BattleRig(bool mempo, int mempoOwner = 1, int favorHolder = 1)
    {
        var s = Published(Batch336Support.Table(Phase.Battle, 1)); s.PriorityPlayerId = favorHolder; s.FavorPlayerId = favorHolder;
        var attacker = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Amethyst Champion");
        var second = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var defender = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Amethyst Champion");
        if (mempo) Attach(s, Mempo, mempoOwner == 1 ? attacker : defender);
        return (MatchEngine.FromTestState(s), attacker, defender, second);
    }

    private static bool CanTarget(MatchEngine e, int player, CardInstance target) =>
        Options(e, player, "favor-battle").Any(a => a.Enabled && (a.TargetIds?.Contains(target.Id) ?? false));

    private static void AnyUnitTarget()
    {
        var (without, attacker, defender, _) = BattleRig(mempo: false);
        Check(CanTarget(without, 1, defender) == false, "without Mempo an Attacker may not target a defending unit");
        Check(CanTarget(without, 1, attacker) == false, "nor his own unit");
        var (withMempo, attacker2, defender2, second2) = BattleRig(mempo: true);
        Check(CanTarget(withMempo, 1, defender2), "with Mempo the controller may target a defending enemy unit, which is not attacking");
        Check(CanTarget(withMempo, 1, attacker2) && CanTarget(withMempo, 1, second2), "and his own units, as any player's unit");
        // Nobody else's action is widened: the other player holds the Favor and has no Mempo; his own units stay illegal, the attacker's legal.
        var (other, a3, d3, _) = BattleRig(mempo: true, mempoOwner: 1, favorHolder: 0);
        Check(CanTarget(other, 0, a3) && !CanTarget(other, 0, d3), "the opponent's Favor action is as the rulebook has it: attackers only");
        // Only the rulebook action: another ability with the same category is not widened (Hisano's Guide-style 'enemy-attacking' targets stay attackers).
        Check(AbilityCatalog.All["favor-battle"].Target == "enemy-attacking", "the rulebook action's category is unchanged");
        // Using it on a defending unit moves it home (the action's effect) and discards the Favor as the cost.
        Accept(withMempo, Batch336Support.Offer(withMempo, 1, "favor-battle", null!, defender2.Id));
        var mempoCard = withMempo.State.Cards.Values.Single(c => c.DefinitionId == Mempo);
        Check(Offered(withMempo, 1, mempoCard), "after the Favor Battle action Mempo's Reaction takes the Favor back");
        PassWindows(withMempo);
        Eq(withMempo.State.Cards[defender2.Id].Zone, Zone.Home, "the defending unit moved home");
        Eq(withMempo.State.FavorPlayerId, null, "the Imperial Favor was discarded as the cost");
        // Own unit: legal, and it moves home.
        var (own, ownUnit, _, _) = BattleRig(mempo: true);
        Accept(own, Batch336Support.Offer(own, 1, "favor-battle", null!, ownUnit.Id)); PassWindows(own);
        Eq(own.State.Cards[ownUnit.Id].Zone, Zone.Home, "his own unit moved home");
    }

    private static void PassWindows(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
        {
            var decline = e.QueryActions(e.State.PendingChoice.PlayerId).FirstOrDefault(a => a.Enabled && a.Type is "declineReaction" or "pass");
            if (decline?.Command is null) { var who = e.State.PriorityPlayerId; Accept(e, new("declineReaction", who, e.Version)); continue; }
            Accept(e, decline.Command);
        }
    }

    // ---- one additional use --------------------------------------------------------------------------------------------------------------------------------------------------------

    // Gives the Favor back to a player between two uses (the Reaction itself is once per turn), as a table edit.
    private static MatchEngine WithFavor(MatchEngine e, int player) => Batch336Support.Edit(e, s => { s.FavorPlayerId = player; s.PriorityPlayerId = player; });

    private static void ExtraUse()
    {
        var (e, a, d, second) = BattleRig(mempo: true);
        Accept(e, Batch336Support.Offer(e, 1, "favor-battle", null!, d.Id)); PassWindows(e);
        e = WithFavor(e, 1);
        Check(CanTarget(e, 1, second), "a second Favor Battle action the same turn is offered with Mempo");
        Accept(e, Batch336Support.Offer(e, 1, "favor-battle", null!, second.Id)); PassWindows(e);
        Eq(e.State.Cards[second.Id].Zone, Zone.Home, "the second use resolved");
        e = WithFavor(e, 1);
        Check(!CanTarget(e, 1, a), "but not a third");
        Check(Options(e, 1, "favor-battle").All(o => !o.Enabled), "every option of the third is disabled");
        var rejected = Batch336Support.Rejection(e, new("useAbility", 1, e.Version, AbilityId: "favor-battle", TargetId: a.Id));
        Check(rejected.Length > 0, "the third use is refused");

        // Without Mempo the second is refused.
        var (n, na, _, _) = BattleRig(mempo: false, favorHolder: 0);
        Accept(n, Batch336Support.Offer(n, 0, "favor-battle", null!, na.Id)); PassWindows(n);
        n = WithFavor(n, 0);
        Check(Options(n, 0, "favor-battle").All(o => !o.Enabled), "without Mempo a second use the same turn is refused");

        // Mempo is its controller's: the other player gets no extra use.
        var (o, oa, od, os) = BattleRig(mempo: true, mempoOwner: 1, favorHolder: 0);
        Accept(o, Batch336Support.Offer(o, 0, "favor-battle", null!, oa.Id)); PassWindows(o);
        o = WithFavor(o, 0);
        Check(Options(o, 0, "favor-battle").All(x => !x.Enabled), "the opponent of Mempo's controller has no additional use");

        // A turn later the count starts afresh.
        var (t, _, td, ts) = BattleRig(mempo: true);
        Accept(t, Batch336Support.Offer(t, 1, "favor-battle", null!, td.Id)); PassWindows(t);
        t = Batch336Support.Edit(t, s => { s.TurnNumber++; s.FavorPlayerId = 1; s.PriorityPlayerId = 1; });
        Check(CanTarget(t, 1, ts), "next turn the first use is available again");

        // The hook itself: with Mempo in play its controller's Favor abilities (the four rulebook actions, a card's printed Favor keyword) gain one use, a non-Favor ability and the other player none.
        var (h, _, _, _) = BattleRig(mempo: true);
        foreach (var id in FavorMempoAbilities.RulebookFavorActions) Eq(h.ExtraUsesForTest(1, id), 1, id + " gains an additional use");
        Eq(h.ExtraUsesForTest(0, "favor-battle"), 0, "the opponent's Favor action gains none");
        Eq(h.ExtraUsesForTest(1, "lobby"), 0, "Lobby (Political, no Favor keyword) gains none");
        Eq(h.ExtraUsesForTest(1, "baiko-favor"), 0, "a Political Open that takes the Favor is no Favor ability");
        Eq(h.ExtraUsesForTest(1, "baiko-shame"), 1, "a card's printed Favor ability gains one");
        Eq(h.ExtraUsesForTest(1, "elite-sentry-straighten"), 0, "an unlimited ability has nothing to add to");
        var (plain, _, _, _) = BattleRig(mempo: false);
        Eq(plain.ExtraUsesForTest(1, "favor-battle"), 0, "no Mempo, no extra use");
    }

    // ---- the Reaction ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Mempo, CardInstance Spare) CycleRig(bool mempo, int favorHolder = 1, int mempoOwner = 1)
    {
        var s = Published(Batch336Support.Table(Phase.Action, favorHolder)); s.FavorPlayerId = favorHolder;
        var champion = Kw(Add(s, mempoOwner == 1 ? Lion : Crane, mempoOwner, Zone.Home), "Amethyst Champion");
        var card = mempo ? Attach(s, Mempo, champion) : champion;
        var spare = Add(s, Lion, favorHolder, Zone.Hand);
        return (MatchEngine.FromTestState(s), card, spare);
    }

    private static GameCommand Cycle(MatchEngine e, int player, CardInstance spare) => new("useAbility", player, e.Version, AbilityId: "favor-cycle", TargetId: spare.Id);

    private static bool Offered(MatchEngine e, int player, CardInstance mempo) => Options(e, player, Take, mempo.Id).Any(o => o.Enabled);

    private static void TakeBackReaction()
    {
        var (e, mempo, spare) = CycleRig(true);
        Accept(e, Cycle(e, 1, spare));
        Eq(e.State.FavorPlayerId, null, "the Favor was discarded as the cost");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window after the Favor action opens for the player who discarded it");
        Check(Offered(e, 1, mempo), "Mempo's controller may take the Favor back");
        Check(!Offered(e, 0, mempo), "the other player is not offered it");
        Accept(e, Options(e, 1, Take, mempo.Id).Single(o => o.Enabled).Command!);
        Eq(e.State.FavorPlayerId, 1, "he took the Imperial Favor");
        Check(e.State.Events.Any(x => x.Type == "favorTaken"), "recorded as a take");
        PassWindows(e);
        Check(e.State.PendingChoice is null, "the window closed");

        // Optional: declined, the Favor stays discarded.
        var (d, dm, ds) = CycleRig(true);
        Accept(d, Cycle(d, 1, ds));
        Check(Offered(d, 1, dm), "offered again");
        Accept(d, new("declineReaction", 1, d.Version)); PassWindows(d);
        Eq(d.State.FavorPlayerId, null, "declined: the Imperial Favor stays discarded");
        Check(d.State.PendingChoice is null, "and play went on");

        // Without Mempo in play: no window, nothing asked of anyone.
        var (n, _, ns) = CycleRig(false);
        Accept(n, Cycle(n, 1, ns));
        Check(n.State.PendingChoice is null, "without Mempo no window opens");
        Eq(n.State.PriorityPlayerId, 0, "priority passed on as before");

        // Mempo's controller did not discard the Favor: the other player did.
        var (o, om, os) = CycleRig(true, favorHolder: 0, mempoOwner: 1);
        Accept(o, Cycle(o, 0, os));
        Check(o.State.PendingChoice?.Type != "afterActionResolvedReaction" || !Offered(o, 1, om), "an opponent's discard is not 'you discard': no Reaction for Mempo's controller");
        PassWindows(o);

        // The rulebook Favor action discards it as a cost even when it takes no card: an action that does not discard the Favor opens nothing.
        var (m, mm, _) = CycleRig(true, favorHolder: 1);
        Check(m.State.Cards[mm.Id].Zone == Zone.Attached, "Mempo is attached");
        m = Batch336Support.Edit(m, s => s.FavorPlayerId = null);
        Check(!m.QueryActions(1).Any(a => a.Enabled && a.CardId == mm.Id && a.Command?.AbilityId == Take), "no Reaction without a discard to react to");

        // Only the rulebook actions that send a Personality home from battle or cycle a card trigger it: Shunya's Honor action discards the Favor too, but opens nothing.
        var sh = Published(Batch336Support.Table(Phase.Action, 1)); sh.FavorPlayerId = 1;
        var shChampion = Kw(Add(sh, Lion, 1, Zone.Home), "Amethyst Champion"); var shMempo = Attach(sh, Mempo, shChampion);
        var shunya = Add(sh, "oracle-l5r-2158", 1, Zone.Home);
        var she = MatchEngine.FromTestState(sh);
        Accept(she, Options(she, 1, "shunya-honor", shunya.Id).First(o => o.Enabled && o.Command?.CostOption is null).Command!);
        Eq(she.State.FavorPlayerId, null, "Shunya's action discarded the Favor");
        Check(she.State.PendingChoice is null || !Offered(she, 1, shMempo), "but Mempo's Reaction is not offered after it");
        Check(!AbilityCatalog.All[Take].Label.Contains("negated") && FavorMempoAbilities.TakeBackTriggers.SequenceEqual(["favor-battle", "favor-cycle", "favor-cycle-kurohito"]), "the triggering actions are the Battle and card-cycling ones");

        // A Mempo that left play is no longer a source.
        var (g, gm, gs) = CycleRig(true);
        g = Batch336Support.Edit(g, s => { var m2 = s.Cards[gm.Id]; m2.Zone = Zone.FateDiscard; m2.AttachedTo = null; });
        Accept(g, Cycle(g, 1, gs));
        Check(g.State.PendingChoice is null, "an unattached Mempo opens no window");
    }

    // ---- negation ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AfterNegation()
    {
        var s = Published(Batch336Support.Table(Phase.Action, 1)); s.FavorPlayerId = 1;
        var champion = Kw(Add(s, Lion, 1, Zone.Home), "Amethyst Champion"); var mempo = Attach(s, Mempo, champion);
        var spare = Add(s, Lion, 1, Zone.Hand); var answer = Add(s, Confusion, 0, Zone.Hand);
        var handBefore = s.Players[1].Hand.Count;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Cycle(e, 1, spare));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the other player may answer a Favor action");
        Accept(e, Options(e, 0, "confusion-at-court-negate", answer.Id).Single(o => o.Enabled).Command!);
        DeclineBefore(e);
        Eq(e.State.FavorPlayerId, null, "the negated Favor action still discarded the Imperial Favor");
        Eq(e.State.Players[1].Hand.Count, handBefore - 1, "its effects were negated: the discarded card is a cost, the card it would draw is not drawn");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "yet 'after you discard the Imperial Favor' happened: the window opens");
        Check(Offered(e, 1, mempo), "Mempo's controller may take the Favor back");
        Check(!e.QueryActions(1).Any(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is not (Take or null)), "nothing else may react to the negated action's record");
        Accept(e, Options(e, 1, Take, mempo.Id).Single(o => o.Enabled).Command!);
        Eq(e.State.FavorPlayerId, 1, "taken back");
        PassWindows(e);
        Check(e.State.PendingChoice is null && e.State.LastResolvedAction is not null, "the window closed normally");

        // The same without Mempo: the negation behaves as it always did.
        var t = Published(Batch336Support.Table(Phase.Action, 1)); t.FavorPlayerId = 1;
        var tspare = Add(t, Lion, 1, Zone.Hand); var tanswer = Add(t, Confusion, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Cycle(te, 1, tspare));
        Accept(te, Options(te, 0, "confusion-at-court-negate", tanswer.Id).Single(o => o.Enabled).Command!);
        DeclineBefore(te); PassWindows(te);
        Eq(te.State.FavorPlayerId, null, "without Mempo: still discarded");
        Check(te.State.PendingChoice is null, "and no window opens");
    }

    private static void DeclineBefore(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" } && guard < 8; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // ---- every Favor discard ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DiscardSites()
    {
        // The rulebook Favor Battle action (cost verb), a card's Favor action (Baiko's Limited) and the Battle action all route through one helper: each records its discarder.
        var (e, mempo, spare) = CycleRig(true);
        Accept(e, Cycle(e, 1, spare));
        Check(e.State.LastResolvedAction is { } facts && facts.FavorDiscarders.SequenceEqual([1]), "the rulebook Favor action records its discarder on the action");
        PassWindows(e);
        var (b, _, bd, _) = BattleRig(true);
        Accept(b, Batch336Support.Offer(b, 1, "favor-battle", null!, bd.Id));
        Check(b.State.LastResolvedAction is { } bf && bf.FavorDiscarders.SequenceEqual([1]) || b.State.PendingChoice is not null, "so does the Battle action");
        PassWindows(b);
        // The helper's own contract: an effect (Burning Dreams) is not a cost, so no record; a cost is recorded once per player.
        var h = MatchEngine.FromTestState(Published(Batch336Support.Table(Phase.Action, 1)));
        Check(h.ReleaseFavorForTest(1, false).Count == 0, "an effect's discard is not recorded");
        Check(h.ReleaseFavorForTest(1, true).SequenceEqual([1]), "a cost's discard is recorded");
        Check(h.ReleaseFavorForTest(1, true).SequenceEqual([1]), "once per player");
    }
}
