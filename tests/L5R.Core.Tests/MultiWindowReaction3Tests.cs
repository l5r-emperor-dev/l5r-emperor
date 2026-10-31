using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #830, reaction slice 3: for each listed Reaction of a multi-window card, the real trigger window is opened through ordinary play, the Reaction is
// shown to be on offer (enabled, to the right player only) with its printed effect, and shown NOT to be playable outside that window (plain Action /
// Battle phase with nothing pending, a closed window, the other player). Where the card also prints Battle/Open/Limited actions one of them is spot-checked
// to still work independently of the Reaction.
internal static partial class MultiWindowReaction3Tests
{
    public static void Run(TestRunner runner) => runner.Run("multi-window-reaction-3", [
        ("Kitsuki Horume (entry Reaction, choose a player): the entry window offers horume-lose to its owner only, the opponent loses 1 Honor, it is shut afterwards and in the plain Action phase", HorumeLose),
        ("Otomo Demiyah (Political entry Reaction): demiyah-lose is offered in his entry window to its owner only, loses 1 Honor, refused outside the window", DemiyahLose),
        ("Training choose-a-player personality (entry Reaction): training-choose-player-lose in the entry window only, to its owner only", TrainingChoosePlayerLose),
        ("Daigotsu Aya (entry Reaction + Open actions): the Dark Virtue is fetched in her entry window only; her Open keyword actions still work in the Action phase", AyaEntryDarkVirtue),
        ("Doji Razan (entry Invest Reaction + Limited): Invest 1 Gold gives +1C in the entry window only; the Limited destroy of a Fudo card is independent", RazanInvest),
        ("Celestial Sword of the Mantis (entry Reaction + Battle actions): +2 Honor in the entry window after Equip only; the Battle Force action still works", MantisSwordHonor),
        ("The Ancestral Sword of Hantei (entry Reaction + Battle): +2 Honor in the entry window after Equip only; the Battle bow action still works", HanteiSwordHonor),
        ("Know No Fear (targeted-by-action): a Battle action that bows the Samurai opens the window for his controller, the bowing is negated; not offered to the actor, with no action pending, or for a non-Samurai; the Fear half is the sibling Reaction", KnowNoFearNegateMove),
        ("The Loyalty of the Scorpion (targeted-by-action): a bowing Battle action on a Scorpion opens the window, the performing Scorpion is destroyed and the bow negated; not offered outside it; the destroy half is not offered for a bow", LoyaltyScorpionBow),
        ("Isawa Sakonoko (targeted-by-action, bow Sakonoko): his ally's destruction is negated when a Battle action targets the ally at his battlefield; not to the actor, not at another location, not in the plain Battle phase", SakonokoNegate),
        ("Armor of the Heavens (Political targeted-by-action): when another player's action targets the wearer its controller chooses a player to lose 2 Honor; not for the wearer's own action, the actor, or outside the window", ArmorHeavensLose),
        ("Well Protected (targeted-by-action, destroy his Armor): an Attack on a Personality is negated by destroying the Armor; not without an Armor, not to the attacker, not outside the window", WellProtectedDestroy),
        ("Yasuki Ikke (targeted-by-action, pay 2 Gold): a Battle action on a Merchant lets its controller make the actor lose 2 Honor; shares one use with ikke-negate; his Battle move-home still works", IkkeHonorLoss),
        ("Reverse Fate (Void Interrupt on a Spell): an action that discards a Fate card opens the window; the first discarded card gets +1FV; not for a Water Shugenja or outside the window", ReverseFatePlus),
        ("Akodo Kakihara (before-attack-resolves): a Melee/Ranged Attack on him opens the Attack window; his Force rises by his Personal Honor; not offered to the attacker or outside the window", KakiharaAttackForce),
        ("Khol Regulars (before-attack-resolves): its own Ranged Attack opens the Attack window and its Reaction adds +2 strength; not offered in the plain Battle phase nor to the other player; its Battle Attack is the independent action", KholRegularsStrength),
        ("Forest Cover (Recon Reaction after engaging): with a Scout at the battlefield the attach half is offered in the Engage window to the Attacker only, attaches a Follower and owes a draw; spent after one use; not offered once the window closes", ForestCoverAttach),
        ("Hidden Defenses (Reaction after engaging, enemy at the battlefield): a bowed Personality moves in and straightens in the Engage window only; not without an enemy unit; its Battle bow is independent", HiddenDefensesMove),
        ("Steal an Advantage (Engage with a Scout): in the Engage window a target enemy card loses one chosen ability; not without a Scout, not after the window; its Battle half works afterwards", StealAdvantageEngage),
        ("Reconnaissance Scouts (Engage at its own battlefield): both players' Scouts give Reconnaissance in their window, only at their own battlefield and only for their controller", ScoutsEngage),
        ("Never Safe (Reaction in the after-battle window from the discard pile): played in a battle, it returns to its player's hand when that battle resolves; not to the other player, not mid-battle", NeverSafeReturn),
        ("The Yari of the Shabura (return after its own Ranged Attack): destroyed as the cost of its Attack, it may be put into hand in the after-battle window; not mid-battle", YariReturn),
        ("Shosuro Ryoken (Political Reaction after a battle won at his battlefield): the enemy leader loses 2 Honor in the after-resolution window; not if he was at home; not to the other player", RyokenHonorLoss),
        ("Tales of Dishonor (Political Reaction after a battle you won): the loser loses 4 Honor in the window; not offered to the loser or mid-battle; its Battle straighten stays independent", TalesOfDishonor),
        ("False Ring of Fire (Interrupt, bow the Ring): in the Honor-change window an action's Honor loss is reduced by 2; not in a plain Battle position or to the other player; its Open recruit is independent", FalseRingOfFireReduce),
        ("Shiro Chugo (Political Reaction before an Honor change, performing Courtier): the Honor loss from another player's action is reduced; not without an unbowed Courtier or outside the window; his Battle Melee is independent", ChugoHonorReduce),
        ("Shosuro Makiko (Political Reaction, bow her): the Honor GAIN from another player's action is reduced by 2; not for a loss, not while bowed, not outside the window; her Open Favor action is independent", MakikoReduce),
        ("Unimpeachable Name (Reaction before an Honor loss): a loss from another player's action is reduced by 3 for its holder; not offered to the actor or mid-Action phase without a loss; its Battle bow is independent", UnimpeachableNameReduce),
        ("Tamori Tamoko (Reaction before an action resolves): another player's movement of a Personality can be negated; offered to her controller only; not outside the window; her Open action is independent", TamokoNegate),
        ("A Reaction is not tied to presence unless its text says so (issue #1152, reverses #1133): Tamoko's Reaction is offered from home and with no unit at the battlefield", ReactionNeedsPresence),
        ("Bayushi Irezu (Reaction before a duel begins, bow): a Scorpion Clan Personality challenged by another player's duel is replaced by Irezu; not once he is bowed, not outside the duel window; his Battle move is independent", IrezuDuelEnter),
        ("Asako Kikugoro (Reaction after your Henshin enters play): another Henshin purchased opens the look at the top two Dynasty cards; not for a non-Henshin, not to the other player or outside the entry window", KikugoroHenshinEntry),
        ("Kaiu Village (Reaction after your Personality enters play from a province, pay 2 Gold): creates and attaches a +1F Armor; not to the other player, not outside the entry window; the Weapon half is the sibling", KaiuVillageArmor),
        ("The Spider's Web (Reaction after your Personality enters play): a Weapon from the hand is attached at its own cost; not to the other player or outside the entry window", SpidersWebAttach),
        ("Centering the Soul (Reaction after an action resolves): the Ring that action bowed is straightened for the Spell bowing; not offered outside the after-action window", CenteringSoulStraighten),
        ("Closing the Gap (Reaction after an action that moved a Personality): the holder moves his performing Personality to the battlefield and straightens his unit; not for an action that moved nobody, not to the other player", ClosingTheGapToBattle),
        ("Goju Kaxt (Reaction after an action during which it was destroyed): a Kaxt is created at the current battlefield; once; not offered while it is in play", KaxtCreateBattle),
        ("Indecision (Reaction after an action resolves, his second hand-filling action): its player discards two cards; not after only one such action; its Battle move is independent", IndecisionDiscard),
        ("Lying in Ambush (Reaction after your action created a non-Shadowlands Personality at home): a 2F Samurai Personality is created at home; not offered mid-Action phase or to the other player", LyingInAmbushHome),
        ("The Empress' Address (Political Reaction after an action destroyed your defending Personality): bow a Courtier and return a non-Unique Personality; not to the attacker or outside the window", EmpressAddressAction),
        ("Stormless Fury (Reaction after your Battle action, from the hand): attaches to the performing Thunder Shugenja; not to the other player or in a plain Battle position", StormlessFuryAttach),
        ("Moto Rani - exp (paying for a Spell attaching to her): a dead Personality's Chi discounts the Spell; not for another Shugenja, not while bowed", RaniSpellDiscount),
        ("Profits (paying a Gold cost, discard it): its Wealth tokens are Gold for a payment; not with no tokens, not from the hand; its Limited put-into-play is independent", ProfitsGoldSource),
        ("Yasuki Tanimura (paying for an action, 3 Gold for that action only): offered as a payment source for an action only, once a turn, not while bowed", TanimuraGold),
        ("Yoritomo Singh - exp2 (paying an action's Gold cost): 3 Gold for an action, once a turn; not for a recruit or while bowed; his Battle destroy is independent", SinghGold),
    ]);

    // ---- helpers ----------------------------------------------------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Window(MatchEngine e) => e.State.PendingChoice?.Type ?? "none";

    // The card is bought from player 0's province 0 with plenty of Gold, as a player would; the returned engine has its entering-play window open.
    private static (MatchEngine E, string Id) Recruit(string def, Action<MatchState>? setup = null, int marketplaces = 12)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        setup?.Invoke(s);
        var id = s.Players[0].Provinces[0].CardId!; s.Cards[id].DefinitionId = def; s.Cards[id].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: id, SourceIds: e.PaymentOptions(0, CardCatalog.Get(def).GoldCost).First().SourceIds));
        return (e, id);
    }

    // Declines whatever Reaction windows are open (each by the player it is waiting on) until the position is quiet.
    private static void Close(MatchEngine e)
    {
        for (var guard = 0; guard < 16 && e.State.PendingChoice is not null; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // The ability is not on offer enabled to any listed player, and the very command that was accepted in its window is refused (re-sent with the current version).
    private static void Closed(MatchEngine e, string ability, string? cardId, GameCommand was, string where, params int[] players)
    {
        foreach (var p in players)
        {
            Check(!CanUse(e, p, ability, cardId), $"{ability} is offered to player {p} {where}");
            Reject(e, was with { PlayerId = p, ExpectedVersion = e.Version });
        }
    }

    private static CardInstance AtHome(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Home, bowed);

    // The command of the enabled option for a player target (the opponent).
    private static GameCommand PlayerOption(MatchEngine e, string ability, string cardId, int target) =>
        Options(e, 0, ability, cardId).Single(o => o.Enabled && o.Command!.TargetPlayerId == target).Command!;

    // ---- entry Reactions that choose a player ---------------------------------------------------------------------------------------------------------------
    private static void ChoosePlayerLose(string def, string ability)
    {
        var plain = Table(Phase.Action, 0); var inPlay = AtHome(plain, def, 0);
        var q = MatchEngine.FromTestState(plain);
        foreach (var p in new[] { 0, 1 }) Check(!CanUse(q, p, ability, inPlay.Id), $"{ability} is offered in the plain Action phase to player {p}");
        Reject(q, Use(q, 0, ability, inPlay.Id));

        var (e, id) = Recruit(def);
        Eq(Window(e), "entryReaction", "the entry window is open");
        Check(!CanUse(e, 1, ability, id), "the opponent is not offered another player's entry Reaction");
        var cmd = PlayerOption(e, ability, id, 1);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 1); var own = Honor(e, 0);
        Accept(e, cmd);
        Eq(Honor(e, 1), honor - 1, "the chosen player lost 1 Honor");
        Eq(Honor(e, 0), own, "and only that player");
        Close(e);
        Closed(e, ability, id, cmd, "after its window closed", 0, 1);
    }

    private static void HorumeLose() => ChoosePlayerLose(Def("horume-lose"), "horume-lose");
    private static void DemiyahLose() => ChoosePlayerLose(Def("demiyah-lose"), "demiyah-lose");
    private static void TrainingChoosePlayerLose() => ChoosePlayerLose("training-choose-player-personality", "training-choose-player-lose");

    private static string Def(string ability) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();

    // ---- Daigotsu Aya ---------------------------------------------------------------------------------------------------------------------------------------
    private static void AyaEntryDarkVirtue()
    {
        var aya = Def("aya-entry-dark-virtue"); string dv = "";
        var (e, id) = Recruit(aya, s => dv = Add(s, "training-dark-virtue", 0, Zone.FateDiscard).Id);
        Eq(Window(e), "entryReaction");
        Check(!CanUse(e, 1, "aya-entry-dark-virtue", id), "not offered to the opponent");
        var cmd = Enabled(e, 0, "aya-entry-dark-virtue", id).Command! with { TargetId = dv };
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Eq(e.State.Cards[dv].Zone, Zone.Hand, "the Dark Virtue came to her controller's hand");
        Close(e);
        Closed(e, "aya-entry-dark-virtue", id, cmd, "after her window closed", 0, 1);
        // Independent Open action: in the Action phase for either player's turn she can still take a keyword.
        var s2 = Table(Phase.Action, 0); var a2 = AtHome(s2, aya, 0);
        var q = MatchEngine.FromTestState(s2);
        Check(!CanUse(q, 0, "aya-entry-dark-virtue", a2.Id), "the entry Reaction is not an Action-phase action");
        Accept(q, Enabled(q, 0, "aya-duelist", a2.Id).Command!);
        Check(q.State.Cards[a2.Id].GrantedKeywords.Any(g => g.Keyword == "Duelist"), "the Open action still resolves");
    }

    // ---- Doji Razan -----------------------------------------------------------------------------------------------------------------------------------------
    private static void RazanInvest()
    {
        var razan = Def("wl1-razan-invest");
        var (e, id) = Recruit(razan);
        Check(Window(e) is "entryReaction" or "proclaim", "the entry window is open: " + Window(e));
        Check(!CanUse(e, 1, "wl1-razan-invest", id), "not offered to the opponent");
        var opt = Enabled(e, 0, "wl1-razan-invest", id);
        Eq(opt.Gold!.Cost, 1, "Invest costs 1 Gold");
        var chi = e.PersonalityChiForTest(id);
        Reject(e, opt.Command! with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, opt.Command!);
        Eq(e.PersonalityChiForTest(id), chi + 1, "a +1C token");
        Close(e);
        Closed(e, "wl1-razan-invest", id, opt.Command!, "after the entry window closed", 0, 1);
        var plain = Table(Phase.Action, 0); var r2 = AtHome(plain, razan, 0); var fudo = AtHome(plain, "training-fudo-personality", 1);
        var q = MatchEngine.FromTestState(plain);
        Check(!CanUse(q, 0, "wl1-razan-invest", r2.Id), "Invest is not an Action-phase action");
        Accept(q, Offer(q, 0, "wl1-razan-destroy", r2.Id, fudo.Id));
        Check(q.State.Cards[fudo.Id].Zone != Zone.Home, "the Limited destroy still works");
    }

    // ---- Item entry Reactions (Equip opens the window) ---------------------------------------------------------------------------------------------------------
    private static void SwordHonor(string sword, string host, string hostIdentity, string reaction, string otherAbility, Phase otherPhase, Func<MatchState, CardInstance, CardInstance[]> otherSetup)
    {
        var s = Table(Phase.Action, 0);
        for (var i = 0; i < 9; i++) AtHome(s, "training-marketplace", 0);
        var h = AtHome(s, host, 0); var item = Add(s, sword, 0, Zone.Hand);
        var inPlayItem = Add(s, sword, 0, Zone.Attached, parent: h.Id); inPlayItem.ProvinceIndex = null;
        var q = MatchEngine.FromTestState(s);
        foreach (var p in new[] { 0, 1 }) Check(!CanUse(q, p, reaction, inPlayItem.Id), $"{reaction} is offered in the plain Action phase to player {p}");
        Reject(q, Use(q, 0, reaction, inPlayItem.Id));

        var s2 = Table(Phase.Action, 0);
        for (var i = 0; i < 9; i++) AtHome(s2, "training-marketplace", 0);
        var h2 = AtHome(s2, host, 0); var item2 = Add(s2, sword, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s2);
        var equip = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == item2.Id && a.Enabled).Command! with { TargetId = h2.Id };
        equip = equip with { SourceIds = e.PaymentOptions(0, CardCatalog.Get(sword).GoldCost).First().SourceIds };
        Accept(e, equip);
        Eq(Window(e), "entryReaction", "Equip opened the entering-play window");
        Check(!CanUse(e, 1, reaction, item2.Id), "not offered to the opponent");
        var cmd = Enabled(e, 0, reaction, item2.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 0);
        Accept(e, cmd);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor");
        Close(e);
        Closed(e, reaction, item2.Id, cmd, "after the entry window closed", 0, 1);
        // Independent Battle action of the same card.
        var b = Table(Phase.Battle, 1);
        var bh = Add(b, hostIdentity, 1, Zone.Battlefield, province: 0);
        var targets = otherSetup(b, bh);
        var bi = Add(b, sword, 1, Zone.Attached, province: 0, parent: bh.Id);
        var f = MatchEngine.FromTestState(b);
        Check(!CanUse(f, 1, reaction, bi.Id), "the entry Reaction is not a Battle action");
        Check(CanUse(f, 1, otherAbility, bi.Id), $"{otherAbility} is independent of the Reaction");
    }

    private static void MantisSwordHonor() => SwordHonor(Def("celestial-sword-mantis-honor"), "training-seeds5-mantis-samurai", "training-seeds5-mantis-samurai", "celestial-sword-mantis-honor", "celestial-sword-mantis-force", Phase.Battle, (b, h) => []);
    private static void HanteiSwordHonor() => SwordHonor(Def("ancestral-sword-hantei-honor"), "oracle-l5r-1720", "oracle-l5r-1720", "ancestral-sword-hantei-honor", "ancestral-sword-hantei-bow", Phase.Battle,
        (b, h) => [Add(b, "training-lion-samurai", 0, Zone.Battlefield, province: 0)]);

    // ---- targeted-by-action and attack-window Reactions -------------------------------------------------------------------------------------------------------
    private static void ToPlayer(MatchEngine e, int player) { for (var i = 0; i < 4 && e.State.PriorityPlayerId != player; i++) Accept(e, new GameCommand(e.State.PendingChoice is null ? "pass" : "declineReaction", e.State.PriorityPlayerId, e.Version)); Eq(e.State.PriorityPlayerId, player, "priority reached the reactor"); }
    private static string DefWith(string ability) => CardCatalog.Cards.Values.Where(c => (c.Abilities ?? []).Contains(ability)).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, 0);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static bool Listed(MatchEngine e, int p, string ability, string? card = null) => CanUse(e, p, ability, card);

    // Player 1 (the Attacker, with priority) takes `action` on `actor` against `target`; returns the engine with the window open.
    private static MatchEngine Strike(MatchState s, string action, CardInstance actor, CardInstance target)
    {
        s.PriorityPlayerId = 1;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, action, actor.Id, target.Id));
        return e;
    }

    // The common outside-the-window check: the same position with nothing pending, the command refused and the ability not listed.
    private static void NotInPlainBattle(MatchState s, string ability, string cardId, GameCommand was)
    {
        s.PriorityPlayerId = 0;
        var q = MatchEngine.FromTestState(s);
        Closed(q, ability, cardId, was, "in a plain Battle phase with no action pending", 0, 1);
    }

    private static void KnowNoFearNegateMove()
    {
        MatchState Build(out CardInstance target, out CardInstance know, out CardInstance bower, bool samurai = true)
        {
            var s = Table();
            target = Bf(s, "training-crane-samurai", 0); if (samurai) Kw(target, "Samurai");
            know = Add(s, Def("know-no-fear-negate-move"), 0, Zone.Hand);
            bower = Bf(s, "training-battle-bower", 1);
            return s;
        }
        var st = Build(out var tgt, out var card, out var bower);
        var e = Strike(st, "training-bow-at-battle", bower, tgt);
        Eq(Window(e), "beforeActionReaction");
        Check(!Listed(e, 1, "know-no-fear-negate-move", card.Id), "not offered to the acting player");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "know-no-fear-negate-move", card.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Check(!e.State.Cards[tgt.Id].Bowed, "nothing has happened yet");
        Accept(e, cmd);
        Close(e);
        Check(!e.State.Cards[tgt.Id].Bowed, "his bowing was negated");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played from the hand");
        var plain = Build(out _, out var pk, out _);
        NotInPlainBattle(plain, "know-no-fear-negate-move", pk.Id, cmd);
        var s2 = Build(out var t2, out var k2, out var b2, samurai: false);
        var e2 = Strike(s2, "training-bow-at-battle", b2, t2);
        Check(!Listed(e2, 0, "know-no-fear-negate-move", k2.Id), "a non-Samurai target is not answered");
        // The sibling Reaction of the same card answers a Fear action instead.
        var f = Table(Phase.Action, 1);
        var hot = AtHome(f, "oracle-l5r-1726", 1); var samurai = AtHome(f, "training-crane-samurai", 0); Kw(samurai, "Samurai");
        var kn = Add(f, Def("know-no-fear-negate-fear"), 0, Zone.Hand);
        f.PriorityPlayerId = 1;
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Offer(fe, 1, "hotako-fear-lock", hot.Id, samurai.Id));
        ToPlayer(fe, 0);
        Check(CanUse(fe, 0, "know-no-fear-negate-fear", kn.Id), "the Fear half is offered in the same kind of window");
        Accept(fe, Enabled(fe, 0, "know-no-fear-negate-fear", kn.Id).Command!); Close(fe);
        Check(fe.State.Cards[samurai.Id].ActionLocks.Count == 0, "the Fear action's effects were negated");
    }

    private static void LoyaltyScorpionBow()
    {
        MatchState Build(out CardInstance target, out CardInstance sac, out CardInstance strat, out CardInstance actor)
        {
            var s = Table(Phase.Battle, 1);
            target = Bf(s, "training-scorpion-veteran", 0); sac = AtHome(s, "training-scorpion-veteran", 0);
            strat = Add(s, Def("loyalty-scorpion-bow"), 0, Zone.Hand); actor = Bf(s, "training-battle-bower", 1);
            return s;
        }
        var st = Build(out var tgt, out var sac, out var strat, out var actor);
        var e = Strike(st, "training-bow-at-battle", actor, tgt);
        Eq(Window(e), "beforeActionReaction");
        Check(!Listed(e, 1, "loyalty-scorpion-bow", strat.Id), "not to the actor");
        ToPlayer(e, 0);
        Check(!CanUse(e, 0, "loyalty-scorpion-destroy", strat.Id) && !CanUse(e, 0, "loyalty-scorpion-move", strat.Id), "only the bowing half answers a bow");
        var cmd = Enabled(e, 0, "loyalty-scorpion-bow", strat.Id, sac.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd); Close(e);
        Eq(e.State.Cards[sac.Id].Zone, Zone.Dead, "the performing Scorpion was destroyed");
        Check(!e.State.Cards[tgt.Id].Bowed, "the bowing was negated");
        Eq(e.State.Cards[strat.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        NotInPlainBattle(Build(out _, out _, out var s2, out _), "loyalty-scorpion-bow", s2.Id, cmd);
    }

    private static void SakonokoNegate()
    {
        MatchState Build(out CardInstance sak, out CardInstance ally, out CardInstance destroyer, bool sakonokoHome = false)
        {
            var s = Table(Phase.Battle, 1);
            sak = sakonokoHome ? AtHome(s, Def("sakonoko-negate"), 0) : Bf(s, Def("sakonoko-negate"), 0);
            ally = Bf(s, "training-crane-samurai", 0); destroyer = Bf(s, DefWith("training-destroy-no-attachments"), 1);
            return s;
        }
        var st = Build(out var sak, out var ally, out var destroyer);
        var e = Strike(st, "training-destroy-no-attachments", destroyer, ally);
        Eq(Window(e), "beforeActionReaction");
        Check(!Listed(e, 1, "sakonoko-negate", sak.Id), "not to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "sakonoko-negate", sak.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd); Close(e);
        Check(e.State.Cards[sak.Id].Bowed, "Sakonoko bowed as the cost");
        Eq(e.State.Cards[ally.Id].Zone, Zone.Battlefield, "the ally's destruction was negated");
        var away = Build(out var sak2, out var ally2, out var d2, sakonokoHome: true);
        var ea = Strike(away, "training-destroy-no-attachments", d2, ally2);
        Check(!Listed(ea, 0, "sakonoko-negate", sak2.Id), "a Sakonoko elsewhere does not cover the ally");
        NotInPlainBattle(Build(out var sak3, out _, out _), "sakonoko-negate", sak3.Id, cmd);
    }

    private static void ArmorHeavensLose()
    {
        MatchState Build(out CardInstance wearer, out CardInstance armor, out CardInstance bower)
        {
            var s = Table(Phase.Battle, 1);
            wearer = Bf(s, "training-crane-courtier", 0); armor = Attach(s, Def("armor-heavens-lose"), wearer);
            bower = Bf(s, "training-battle-bower", 1);
            return s;
        }
        var st = Build(out var wearer, out var armor, out var bower);
        var e = Strike(st, "training-bow-at-battle", bower, wearer);
        Eq(Window(e), "beforeActionReaction");
        Check(!e.QueryActions(1).Any(o => o.Enabled && o.Command?.AbilityId is "armor-heavens-lose" or "armor-heavens-gain"), "not offered to the acting player");
        ToPlayer(e, 0);
        var opt = Options(e, 0, "armor-heavens-lose", armor.Id).Single(o => o.Enabled && o.Command!.TargetPlayerId == 1);
        Reject(e, opt.Command! with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 1); var own = Honor(e, 0);
        Accept(e, opt.Command!);
        Eq(Honor(e, 1), honor - 2, "the chosen player lost 2 Honor"); Eq(Honor(e, 0), own, "and only he");
        Close(e);
        var own2 = Table(Phase.Battle, 0); own2.PriorityPlayerId = 0;
        var w2 = Bf(own2, "training-crane-courtier", 0); var a2 = Attach(own2, Def("armor-heavens-lose"), w2); var src = Bf(own2, "training-penalty-source", 0); Bf(own2, "training-lion-samurai", 1);
        var eo = MatchEngine.FromTestState(own2);
        Accept(eo, Offer(eo, 0, "training-penalty-force-own", src.Id, w2.Id));
        Check(eo.State.PendingChoice is null || !Listed(eo, 0, "armor-heavens-lose", a2.Id), "an action of its own controller's does not open the Reaction");
        NotInPlainBattle(Build(out _, out var a3, out _), "armor-heavens-lose", a3.Id, opt.Command!);
    }

    private static void WellProtectedDestroy()
    {
        MatchState Build(out CardInstance victim, out CardInstance armor, out CardInstance card, out CardInstance weapon, bool withArmor = true)
        {
            var s = Table(Phase.Battle, 1);
            var wielder = Bf(s, "training-lion-samurai", 1); weapon = Attach(s, "training-weapon-with-attack", wielder);
            victim = Bf(s, "training-shugenja", 0); armor = withArmor ? Attach(s, "training-armor", victim) : victim;
            card = Add(s, Def("pe-well-protected-destroy"), 0, Zone.Hand);
            return s;
        }
        var st = Build(out var victim, out var armor, out var card, out var weapon);
        var e = Strike(st, "training-weapon-attack", weapon, victim);
        Eq(Window(e), "attackReaction");
        Check(!Listed(e, 1, "pe-well-protected-destroy", card.Id), "not offered to the attacker");
        var opt = Options(e, 0, "pe-well-protected-destroy", card.Id).Single(o => o.Enabled);
        Eq(opt.Command!.TargetId, armor.Id, "one offer per Armor");
        Reject(e, opt.Command! with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, opt.Command!); Close(e);
        Check(e.State.Cards[armor.Id].Zone != Zone.Attached, "the Armor was destroyed");
        Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "his destruction was negated");
        var bare = Build(out var v2, out _, out var c2, out var w2, withArmor: false);
        var eb = Strike(bare, "training-weapon-attack", w2, v2);
        Check(!Listed(eb, 0, "pe-well-protected-destroy", c2.Id), "no Armor, nothing to destroy");
        NotInPlainBattle(Build(out _, out _, out var c3, out _), "pe-well-protected-destroy", c3.Id, opt.Command!);
    }

    private static void IkkeHonorLoss()
    {
        MatchState Build(out CardInstance ikke, out CardInstance nobu)
        {
            var s = Table(Phase.Battle, 1);
            ikke = Bf(s, Def("ikke-honor-loss"), 0); nobu = Bf(s, "oracle-l5r-9846", 1);
            for (var i = 0; i < 2; i++) AtHome(s, "training-marketplace", 0);
            return s;
        }
        var st = Build(out var ikke, out var nobu);
        var e = Strike(st, "nobu-move-home", nobu, ikke);
        Eq(Window(e), "beforeActionReaction");
        Check(!Listed(e, 1, "ikke-honor-loss", ikke.Id), "not offered to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "ikke-honor-loss", ikke.Id).Command!;
        var quote = e.QuoteAbilityGold(cmd)!; Eq(quote.Cost, 2, "pay 2 Gold");
        cmd = cmd with { SourceIds = quote.Payments.First().SourceIds };
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 1);
        Accept(e, cmd); Close(e);
        Eq(Honor(e, 1), honor - 2, "the acting player lost 2 Honor");
        Check(!CanUse(e, 0, "ikke-negate", ikke.Id), "the two outcomes share one use");
        NotInPlainBattle(Build(out var i3, out _), "ikke-honor-loss", i3.Id, cmd);
        var m = Table(Phase.Battle, 0);
        var home = AtHome(m, Def("ikke-honor-loss"), 0); var merchant = Bf(m, Def("ikke-honor-loss"), 0); Bf(m, "training-lion-samurai", 1);
        var em = MatchEngine.FromTestState(m);
        Accept(em, Offer(em, 0, "ikke-move-home", home.Id, merchant.Id));
        Eq(em.State.Cards[merchant.Id].Zone, Zone.Home, "his Merchant moved home");
    }

    private static void ReverseFatePlus()
    {
        MatchState Build(string element, out CardInstance spell, out CardInstance fires, out CardInstance victim)
        {
            var s = Table(); s.PriorityPlayerId = 1;
            fires = Add(s, DefWith("fires-of-turmoil"), 1, Zone.Hand); victim = Bf(s, "training-battle-bower", 0); Bf(s, "training-lion-samurai", 1);
            var host = Bf(s, "training-crane-samurai", 0); Kw(host, "Shugenja", element);
            spell = Add(s, Def("w737-reverse-fate-plus"), 0, Zone.Attached, province: 0, parent: host.Id);
            return s;
        }
        var st = Build("Void", out var spell, out var fires, out var victim);
        var topId = st.Players[1].FateDeck[0]; var printed = CardCatalog.Get(st.Cards[topId].DefinitionId).Focus;
        var e = MatchEngine.FromTestState(st);
        Accept(e, Offer(e, 1, "fires-of-turmoil", fires.Id, victim.Id));
        Eq(Window(e), "beforeActionReaction", "the action discards a top card: the window opens");
        Check(!Listed(e, 1, "w737-reverse-fate-plus", spell.Id), "not offered to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "w737-reverse-fate-plus", spell.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as the cost");
        Close(e);
        Eq(e.FocusValueForTest(topId), Math.Max(0, printed + 1), "the first discarded card read +1FV");
        var water = Build("Water", out var ws, out var wf, out var wv);
        var ew = Strike(water, "fires-of-turmoil", wf, wv);
        Check(!Listed(ew, 0, "w737-reverse-fate-plus", ws.Id), "a Water Shugenja cannot use it");
        NotInPlainBattle(Build("Void", out var s3, out _, out _), "w737-reverse-fate-plus", s3.Id, cmd);
    }

    private static void KakiharaAttackForce()
    {
        MatchState Build(out CardInstance kak, out CardInstance weapon)
        {
            var s = Table(Phase.Battle, 1);
            kak = Bf(s, "oracle-l5r-223", 0); Bf(s, "training-attack-reactor", 1);
            var wielder = Bf(s, "training-lion-samurai", 1); weapon = Attach(s, "training-weapon-with-attack", wielder);
            return s;
        }
        var st = Build(out var kak, out var weapon);
        var e = Strike(st, "training-weapon-attack", weapon, kak);
        Eq(Window(e), "attackReaction");
        Check(!Listed(e, 1, "kakihara-attack-force-bonus", kak.Id), "not offered to the attacker");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "kakihara-attack-force-bonus", kak.Id).Command!;
        Check(!Listed(e, 0, "kakihara-force-bonus", kak.Id), "the non-Attack half is not listed in the Attack window");
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var force = e.CardForceForTest(kak.Id);
        Accept(e, cmd);
        Eq(e.CardForceForTest(kak.Id), force + CardCatalog.Get("oracle-l5r-223").PersonalHonor, "a Force bonus equal to his Personal Honor");
        Close(e);
        NotInPlainBattle(Build(out var k2, out _), "kakihara-attack-force-bonus", k2.Id, cmd);
    }

    private static void KholRegularsStrength()
    {
        MatchState Build(out CardInstance regulars, out CardInstance target)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var host = Bf(s, "training-crane-samurai", 0); regulars = Attach(s, "oracle-l5r-4338", host);
            Bf(s, "training-attack-reactor", 1); target = Bf(s, "training-crane-samurai", 1);
            return s;
        }
        var st = Build(out var reg, out var target);
        var e = MatchEngine.FromTestState(st);
        Check(!Listed(e, 0, "khol-regulars-reaction", reg.Id), "not offered before any Attack is targeted");
        Accept(e, Offer(e, 0, "khol-regulars-attack", reg.Id, target.Id));
        Eq(Window(e), "attackReaction");
        Eq(e.State.PendingAttack?.Strength, 4, "Ranged 4");
        Check(!Listed(e, 1, "khol-regulars-reaction", reg.Id), "not offered to the other player");
        var cmd = Enabled(e, 0, "khol-regulars-reaction", reg.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Eq(e.State.PendingAttack?.Strength, 6, "+2 strength");
        Close(e);
        var q = MatchEngine.FromTestState(Build(out var r2, out _));
        Closed(q, "khol-regulars-reaction", r2.Id, cmd, "with no Attack pending", 0, 1);
        Check(CanUse(q, 0, "khol-regulars-attack", r2.Id), "its Battle Attack is independent of the Reaction");
    }

    // ---- "After engaging" Reactions -----------------------------------------------------------------------------------------------------------------------------
    // Player 0 attacks province 0 (BattleSelection); `setup` places the cards; "selectBattle" opens the Engage window with the Attacker holding priority first.
    private static MatchEngine Engage(Action<MatchState> setup, bool expectWindow = true)
    {
        var s = Table(Phase.BattleSelection, 0); s.PriorityPlayerId = 0;
        s.UnresolvedBattlefields = [0]; s.CurrentBattleProvince = null;
        setup(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        if (expectWindow) Eq(Window(e), "engageReaction", "the Engage window opens");
        return e;
    }

    private static void ForestCoverAttach()
    {
        CardInstance scout = null!, card = null!, follower = null!;
        void Setup(MatchState t)
        {
            t.Cards[t.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; for (var i = 0; i < 4; i++) AtHome(t, "training-marketplace", 0);
            scout = Kw(Bf(t, "training-crane-samurai", 0), "Scout"); Bf(t, "training-lion-samurai", 1);
            follower = Add(t, "training-follower", 0, Zone.Hand); card = Add(t, Def("wl-forest-cover-attach"), 0, Zone.Hand);
        }
        var e = Engage(Setup);
        Check(!Listed(e, 1, "wl-forest-cover-attach", card.Id), "not offered to the other player");
        var cmd = Offer(e, 0, "wl-forest-cover-attach", card.Id, follower.Id, scout.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance at the battlefield");
        Check(e.State.Cards[follower.Id].Zone == Zone.Attached && e.State.Cards[follower.Id].AttachedTo == scout.Id, "the Follower is attached to the performing Scout");
        Eq(e.State.ExtraEndPhaseDraws[0], 1, "an additional card in his next End Phase");
        Check(!CanUse(e, 0, "wl-forest-cover-recon", card.Id, scout.Id), "the sibling half shares the one use");
        // Once the Engage window is closed (an ordinary Battle position with the same cards) it is not offered.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var sc = Kw(Bf(b, "training-crane-samurai", 0), "Scout"); Bf(b, "training-lion-samurai", 1); var fo = Add(b, "training-follower", 0, Zone.Hand); var cd = Add(b, Def("wl-forest-cover-attach"), 0, Zone.Hand);
        var q = MatchEngine.FromTestState(b);
        Closed(q, "wl-forest-cover-attach", cd.Id, cmd with { CardId = cd.Id, TargetId = fo.Id, PerformerId = sc.Id }, "in the Battle phase after the Engage window", 0, 1);
    }

    private static void HiddenDefensesMove()
    {
        CardInstance mover = null!, card = null!;
        var e = Engage(t => { mover = Add(t, "training-crane-veteran", 0, Zone.Home, true); Bf(t, "training-lion-samurai", 1); Bf(t, "training-crane-samurai", 0); card = Add(t, Def("hidden-defenses-move"), 0, Zone.Hand); });
        Check(!Listed(e, 1, "hidden-defenses-move", card.Id), "not offered to the other player");
        var cmd = Enabled(e, 0, "hidden-defenses-move", card.Id, mover.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Check(e.State.Cards[mover.Id].Zone == Zone.Battlefield && e.State.Cards[mover.Id].ProvinceIndex == 0, "he moved to the battlefield");
        Check(!e.State.Cards[mover.Id].Bowed, "and straightened, since he moved");
        // No enemy unit at the battlefield: not offered in the window.
        var none = Engage(t => { Add(t, "training-crane-veteran", 0, Zone.Home, true); Bf(t, "training-crane-samurai", 0); Add(t, "training-lion-samurai", 1, Zone.Home); card = Add(t, Def("hidden-defenses-move"), 0, Zone.Hand); }, expectWindow: false);
        Check(!Listed(none, 0, "hidden-defenses-move", card.Id), "with no enemy unit at the battlefield it is not offered");
        // Not in an ordinary Battle position; the Battle bow of the same card works there (it needs its player to control no Personality).
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var hv = AtHome(b, "training-crane-veteran", 0); Bf(b, "training-lion-samurai", 1); var c2 = Add(b, Def("hidden-defenses-move"), 0, Zone.Hand);
        Closed(MatchEngine.FromTestState(b), "hidden-defenses-move", c2.Id, cmd with { CardId = c2.Id, PerformerId = hv.Id }, "after the Engage window", 0, 1);
        var bb = Table(Phase.Battle, 0); var foe = Bf(bb, "training-lion-samurai", 1); var c3 = Add(bb, Def("hidden-defenses-bow"), 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(bb);
        Accept(eb, Use(eb, 0, "hidden-defenses-bow", c3.Id, selections: Pick("targets", foe.Id)));
        Check(eb.State.Cards[foe.Id].Bowed, "the Battle half still bows its target");
    }

    private static void StealAdvantageEngage()
    {
        CardInstance card = null!, enemy = null!;
        MatchEngine Open(bool scout) => Engage(t => { card = Add(t, Def("steal-advantage-engage"), 0, Zone.Hand); var mine = Bf(t, "training-crane-samurai", 0); if (scout) Kw(mine, "Scout"); enemy = Bf(t, "training-dual-attacker", 1); }, expectWindow: scout);
        var e = Open(true);
        Check(!Listed(e, 1, "steal-advantage-engage", card.Id), "not offered to the other player");
        Check(!CanUse(e, 0, "steal-advantage-battle", card.Id), "no Battle action until the Engage window closes");
        var cmd = Offer(e, 0, "steal-advantage-engage", card.Id, enemy.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        var choose = e.QueryActions(0).Where(a => a.Type == "chooseEffect" && a.Enabled).Select(a => a.Command!).First(c => c.Choice == "training-honor-cost-attack");
        Accept(e, choose);
        Check(e.State.DisabledAbilities.Any(d => d.CardId == enemy.Id && d.UsageKey == "training-honor-cost-attack"), "the chosen ability is locked");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        var noScout = Open(false);
        Check(!CanUse(noScout, 0, "steal-advantage-engage", card.Id), "no Scout of mine at the battlefield: not offered");
        // Independent Battle half in an ordinary Battle position.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var c2 = Add(b, Def("steal-advantage-battle"), 0, Zone.Hand); Bf(b, "training-crane-samurai", 0); var en2 = Bf(b, "training-recon-attacker", 1);
        var eb = MatchEngine.FromTestState(b);
        Check(!CanUse(eb, 0, "steal-advantage-engage", c2.Id), "the Engage half is not a Battle-phase action");
        Accept(eb, Offer(eb, 0, "steal-advantage-battle", c2.Id, en2.Id));
        Check(eb.State.DisabledAbilities.Any(d => d.CardId == en2.Id), "the Battle half locks the ability");
    }

    private static void ScoutsEngage()
    {
        CardInstance scouts = null!, mine = null!;
        MatchEngine Open(bool attacker) => Engage(t =>
        {
            if (attacker) { mine = Bf(t, "training-crane-samurai", 0); scouts = Attach(t, Def("wl-scouts-engage"), mine); Bf(t, "training-lion-samurai", 1); }
            else { mine = Bf(t, "training-lion-samurai", 1); scouts = Attach(t, Def("wl-scouts-engage"), mine); Bf(t, "training-crane-samurai", 0); }
        });
        var e = Open(true);
        Check(!Listed(e, 1, "wl-scouts-engage", scouts.Id), "the Attacker's Scouts are not offered to the Defender");
        var cmd = Enabled(e, 0, "wl-scouts-engage", scouts.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[0], "no Reconnaissance yet");
        Accept(e, cmd);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "the Attacker has Reconnaissance at the battlefield");
        // The Defender's Scouts, after the Attacker passes.
        var h = Open(false);
        Check(!Listed(h, 0, "wl-scouts-engage", scouts.Id), "the Defender's Scouts are not offered to the Attacker");
        ToPlayer(h, 1);
        Accept(h, Enabled(h, 1, "wl-scouts-engage", scouts.Id).Command!);
        Check(h.State.Players[1].Provinces[0].Reconnaissance[1], "the Defender has Reconnaissance at his own battlefield");
        // Away from the battlefield, or in an ordinary Battle position, it is not offered.
        var away = Engage(t => { var home = AtHome(t, "training-crane-samurai", 0); scouts = Attach(t, Def("wl-scouts-engage"), home); Bf(t, "training-crane-samurai", 0); Bf(t, "training-lion-samurai", 1); }, expectWindow: false);
        Check(!Listed(away, 0, "wl-scouts-engage", scouts.Id), "its Personality must be at the engaged battlefield");
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var m2 = Bf(b, "training-crane-samurai", 0); var s2 = Attach(b, Def("wl-scouts-engage"), m2); Bf(b, "training-lion-samurai", 1);
        Closed(MatchEngine.FromTestState(b), "wl-scouts-engage", s2.Id, cmd with { CardId = s2.Id }, "after the Engage window", 0, 1);
    }

    // ---- "After a battle's resolution" Reactions -------------------------------------------------------------------------------------------------------------------
    private static void PassToResolution(MatchEngine e)
    {
        for (var i = 0; i < 40 && Window(e) != "afterResolutionReaction"; i++)
        {
            if (e.State.Phase != Phase.Battle && e.State.Phase != Phase.BattleSelection) return;
            if (e.State.UnresolvedBattlefields.Count == 0 && e.State.CurrentBattleProvince is null) return;
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var pass = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, "nothing to pass with in " + e.State.Phase + " (" + Window(e) + ")");
            Accept(e, pass!.Command!);
        }
    }

    // Player 1 attacks province 0 with three Personalities that beat the lone defender of player 0.
    private static MatchState WonBattle(out CardInstance a, out CardInstance d)
    {
        var s = Table(Phase.Battle, 1);
        a = Bf(s, "training-crane-samurai", 1); Force(a, 5); Force(Bf(s, "training-lion-samurai", 1), 5);
        d = Bf(s, "training-water-personality", 0);
        return s;
    }

    private static void NeverSafeReturn()
    {
        var s = WonBattle(out var ninja, out var target); Kw(ninja, "Ninja");
        var card = Add(s, Def("never-safe-return"), 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 1, "never-safe-return", card.Id), "not offered from the hand mid-battle");
        Accept(e, Offer(e, 1, "never-safe-penalty", card.Id, target.Id, ninja.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Battle action was played and discarded");
        Check(!CanUse(e, 1, "never-safe-return", card.Id), "not offered mid-battle from the discard pile");
        PassToResolution(e);
        Eq(Window(e), "afterResolutionReaction");
        Check(!CanUse(e, 0, "never-safe-return", card.Id), "not offered to the other player");
        var cmd = Enabled(e, 1, "never-safe-return", card.Id).Command!;
        Reject(e, cmd with { PlayerId = 0, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "it is back in its player's hand");
        Close(e);
        Closed(e, "never-safe-return", card.Id, cmd, "after the window closed", 0, 1);
    }

    private static void YariReturn()
    {
        var b = Table(Phase.Battle, 0);
        var holder = Kw(Bf(b, "training-crane-samurai", 0), "Crane Clan"); var spear = Attach(b, Def("pe-yari-return"), holder);
        var sturdy = Bf(b, "training-lion-samurai", 1);
        var e = MatchEngine.FromTestState(b);
        Check(!CanUse(e, 0, "pe-yari-return", spear.Id), "not offered while the Yari is in play");
        Accept(e, Offer(e, 0, "pe-yari-ranged", spear.Id, sturdy.Id));
        Eq(e.State.Cards[spear.Id].Zone, Zone.FateDiscard, "the Yari was destroyed as the cost of its own Attack");
        Check(!CanUse(e, 0, "pe-yari-return", spear.Id), "not offered mid-battle");
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Eq(Window(e), "afterResolutionReaction", "the window after the battle's resolution");
        Check(!CanUse(e, 1, "pe-yari-return", spear.Id), "not offered to the other player");
        var cmd = Enabled(e, 0, "pe-yari-return", spear.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Eq(e.State.Cards[spear.Id].Zone, Zone.Hand, "the Yari is in its owner's hand");
        Close(e);
        Closed(e, "pe-yari-return", spear.Id, cmd, "after the window closed", 0, 1);
    }

    private static void RyokenHonorLoss()
    {
        CardInstance ryoken = null!;
        MatchEngine Resolve(bool there)
        {
            var s = Table(Phase.Battle, 0); s.ActivePlayerId = 0; s.PriorityPlayerId = 1;
            ryoken = Add(s, Def("ryoken-honor-loss"), 0, there ? Zone.Battlefield : Zone.Home, province: there ? 0 : null);
            Force(Bf(s, "training-lion-samurai", 0), 10); Bf(s, "training-crane-samurai", 1);
            var e = MatchEngine.FromTestState(s);
            for (var guard = 0; guard < 6 && Window(e) != "afterResolutionReaction" && e.State.Phase == Phase.Battle; guard++) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        var e = Resolve(true);
        Eq(Window(e), "afterResolutionReaction");
        Check(!CanUse(e, 1, "ryoken-honor-loss", ryoken.Id), "not offered to the other player");
        var cmd = Enabled(e, 0, "ryoken-honor-loss", ryoken.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 1);
        Accept(e, cmd);
        Eq(Honor(e, 1), honor - 2, "the enemy leader lost 2 Honor");
        Close(e);
        Closed(e, "ryoken-honor-loss", ryoken.Id, cmd, "after the window closed", 0, 1);
        var away = Resolve(false);
        Check(Window(away) != "afterResolutionReaction" || !CanUse(away, 0, "ryoken-honor-loss", ryoken.Id), "Ryoken at home: no Reaction");
        // Mid-battle (before any resolution) it is not offered.
        var mid = Table(Phase.Battle, 0); mid.PriorityPlayerId = 0;
        var r2 = Add(mid, Def("ryoken-honor-loss"), 0, Zone.Battlefield, province: 0); Bf(mid, "training-lion-samurai", 1);
        Closed(MatchEngine.FromTestState(mid), "ryoken-honor-loss", r2.Id, cmd with { CardId = r2.Id }, "mid-battle", 0, 1);
    }

    private static void TalesOfDishonor()
    {
        var s = WonBattle(out _, out _);
        var tales = Add(s, Def("tales-of-dishonor-lose-honor"), 1, Zone.Hand);
        var mag = Kw(Bf(s, "training-crane-veteran", 1), "Magistrate");
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 1, "tales-of-dishonor-lose-honor", tales.Id), "not offered mid-battle");
        // The Battle half of the same card works, independently, in the battle.
        var batt = e.Clone();
        Accept(batt, Enabled(batt, 1, "tales-of-dishonor-straighten", tales.Id, mag.Id).Command!);
        PassToResolution(e);
        Eq(Window(e), "afterResolutionReaction");
        Check(!CanUse(e, 0, "tales-of-dishonor-lose-honor", tales.Id), "not offered to the loser");
        var cmd = Enabled(e, 1, "tales-of-dishonor-lose-honor", tales.Id).Command!;
        Reject(e, cmd with { PlayerId = 0, ExpectedVersion = e.Version });
        var loser = Honor(e, 0); var winner = Honor(e, 1);
        Accept(e, cmd);
        Eq(Honor(e, 0), loser - 4, "the loser lost 4 Honor"); Eq(Honor(e, 1), winner, "the winner did not");
        Close(e);
        Closed(e, "tales-of-dishonor-lose-honor", tales.Id, cmd, "after the window closed", 0, 1);
    }

    // ---- Honor-change window Reactions --------------------------------------------------------------------------------------------------------------------------
    // Player 1's honor-strike Personality makes the target's controller (player 0) lose 2 Honor; the window before that loss opens for player 0's Reaction.
    private static MatchEngine HonorLoss(Action<MatchState> setup, out CardInstance victim, out CardInstance striker)
    {
        var s = Table(Phase.Battle, 1);
        victim = Bf(s, "training-crane-samurai", 0); striker = Bf(s, "training-honor-strike-samurai", 1);
        setup(s);
        return Strike(s, "training-honor-strike", striker, victim);
    }

    private static void FalseRingOfFireReduce()
    {
        CardInstance ring = null!;
        var e = HonorLoss(s => ring = Add(s, Def("wl-false-ring-of-fire-reduce"), 0, Zone.Home), out _, out _);
        Eq(Window(e), "honorChangeReaction");
        Check(!Listed(e, 1, "wl-false-ring-of-fire-reduce", ring.Id), "not offered to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "wl-false-ring-of-fire-reduce", ring.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 0);
        Accept(e, cmd); Close(e);
        Check(e.State.Cards[ring.Id].Bowed, "the Ring bowed as the cost");
        Eq(Honor(e, 0), honor, "the 2 Honor loss was reduced by 2");
        var b = Table(Phase.Battle, 1); b.PriorityPlayerId = 0; var r2 = Add(b, Def("wl-false-ring-of-fire-reduce"), 0, Zone.Home); Bf(b, "training-lion-samurai", 1);
        Closed(MatchEngine.FromTestState(b), "wl-false-ring-of-fire-reduce", r2.Id, cmd with { CardId = r2.Id }, "with no Honor change pending", 0, 1);
        // Independent Open recruit from the hand at a maximum hand size of 10.
        var t = Table(Phase.Action, 0); var hand = Add(t, Def("wl-false-ring-of-fire-reduce"), 0, Zone.Hand); t.Players[0].MaxHandBonus = 2;
        var q = MatchEngine.FromTestState(t);
        Check(!CanUse(q, 0, "wl-false-ring-of-fire-reduce", hand.Id), "the Interrupt is not played from a hand");
        var hp = Honor(q, 0);
        Accept(q, Enabled(q, 0, "wl-false-ring-of-fire-recruit", hand.Id).Command!);
        Eq(q.State.Cards[hand.Id].Zone, Zone.Home, "the Ring was put into play"); Eq(Honor(q, 0), hp - 2, "lose 2 Honor");
    }

    private static void ChugoHonorReduce()
    {
        CardInstance chugo = null!, courtier = null!;
        var e = HonorLoss(s => { s.Cards[s.Players[0].StrongholdId].DefinitionId = Def("chugo-honor-reduce"); chugo = s.Cards[s.Players[0].StrongholdId]; courtier = AtHome(s, "training-courtier-personality", 0); }, out _, out _);
        Eq(Window(e), "honorChangeReaction");
        Check(!e.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "chugo-honor-reduce"), "not offered to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "chugo-honor-reduce", chugo.Id, courtier.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 0);
        Accept(e, cmd); Close(e);
        Eq(Honor(e, 0), honor, "the 2 Honor loss was reduced to nothing");
        // No unbowed Courtier: not offered in the window.
        var none = HonorLoss(s => { s.Cards[s.Players[0].StrongholdId].DefinitionId = Def("chugo-honor-reduce"); chugo = s.Cards[s.Players[0].StrongholdId]; AtHome(s, "training-courtier-personality", 0, bowed: true); }, out _, out _);
        Check(Window(none) != "honorChangeReaction" || !CanUse(none, 0, "chugo-honor-reduce", chugo.Id), "a bowed Courtier cannot perform it");
        // Plain Battle position and the independent Battle Melee.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0; b.Cards[b.Players[0].StrongholdId].DefinitionId = Def("chugo-honor-reduce");
        var cid = b.Players[0].StrongholdId; var cu = AtHome(b, "training-courtier-personality", 0); var mine = Bf(b, "training-crane-samurai", 0); var foe = Bf(b, "training-lion-samurai", 1);
        var q = MatchEngine.FromTestState(b);
        Closed(q, "chugo-honor-reduce", cid, cmd with { CardId = cid, PerformerId = cu.Id }, "with no Honor change pending", 0, 1);
        Check(CanUse(q, 0, "chugo-melee", cid, mine.Id), "his Melee Battle action is independent of the Reaction"); _ = foe;
    }

    private static void MakikoReduce()
    {
        MatchState Build(out CardInstance makiko, out CardInstance akahiko, out CardInstance target, bool bowed = false)
        {
            var s = Table(Phase.Battle, 1);
            makiko = Add(s, Def("wlpt-makiko-reduce"), 0, Zone.Battlefield, bowed, 0); akahiko = Bf(s, "oracle-l5r-10278", 1); target = Bf(s, "training-crane-samurai", 0);
            return s;
        }
        var st = Build(out var mk, out var ak, out var tg);
        var e = Strike(st, "wlpt-akahiko-home", ak, tg);
        Eq(Window(e), "honorChangeReaction", "before the Honor gain from the action's effects");
        Check(!Listed(e, 1, "wlpt-makiko-reduce", mk.Id), "not offered to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "wlpt-makiko-reduce", mk.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 1);
        Accept(e, cmd); Close(e);
        Check(e.State.Cards[mk.Id].Bowed, "bowing Makiko is the cost");
        Eq(Honor(e, 1), honor, "a gain of 1, reduced by 2, is 0");
        var bowed = Build(out var bm, out var ba, out var bt, bowed: true);
        var eb = Strike(bowed, "wlpt-akahiko-home", ba, bt);
        Check(Window(eb) != "honorChangeReaction" || !CanUse(eb, 0, "wlpt-makiko-reduce", bm.Id), "a bowed Makiko is not offered");
        NotInPlainBattle(Build(out var pm, out _, out _), "wlpt-makiko-reduce", pm.Id, cmd);
        var a = Table(Phase.Action, 0); var m2 = AtHome(a, Def("wlpt-makiko-reduce"), 0);
        var qa = MatchEngine.FromTestState(a);
        Accept(qa, Enabled(qa, 0, "makiko-favor", m2.Id).Command!);
        Eq(qa.State.FavorPlayerId, 0, "her Open action takes the Imperial Favor");
    }

    private static void UnimpeachableNameReduce()
    {
        MatchState Build(out CardInstance accusation, out CardInstance target, out CardInstance name)
        {
            var s = Table(Phase.Action, 0);
            accusation = Add(s, "oracle-l5r-637", 0, Zone.Hand); target = Kw(AtHome(s, "training-lion-samurai", 1), "Kolat"); name = Add(s, Def("unimpeachable-name-reaction"), 1, Zone.Hand);
            return s;
        }
        var st = Build(out var acc, out var tgt, out var name);
        var e = MatchEngine.FromTestState(st);
        Check(!CanUse(e, 1, "unimpeachable-name-reaction", name.Id), "not offered with no action pending");
        var h1 = Honor(e, 1);
        Accept(e, Offer(e, 0, "accusation-honor-loss", acc.Id, tgt.Id));
        Eq(Window(e), "honorChangeReaction", "the loss opens the window");
        Check(!Listed(e, 0, "unimpeachable-name-reaction", name.Id), "not offered to the acting player");
        ToPlayer(e, 1);
        var cmd = Options(e, 1, "unimpeachable-name-reaction", name.Id).First(o => o.Enabled).Command!;
        Reject(e, cmd with { PlayerId = 0, ExpectedVersion = e.Version });
        Accept(e, cmd); Close(e);
        Eq(Honor(e, 1), h1, "the loss was reduced by 3 to nothing");
        Eq(e.State.Cards[name.Id].Zone, Zone.FateDiscard, "and Unimpeachable Name was played");
        var plain = MatchEngine.FromTestState(Build(out _, out _, out var n2));
        Closed(plain, "unimpeachable-name-reaction", n2.Id, cmd with { CardId = n2.Id }, "with no action pending", 0, 1);
        // Independent Battle bow.
        var b = Table(Phase.Battle, 0); var shug = Kw(Bf(b, "training-crane-samurai", 0), "Shugenja"); var foe = Bf(b, "training-lion-samurai", 1); var n3 = Add(b, Def("unimpeachable-name-reaction"), 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "unimpeachable-name-bow", n3.Id, foe.Id, shug.Id));
        Check(eb.State.Cards[foe.Id].Bowed, "the Battle half still bows its target");
    }

    // ---- other "before an action resolves" / duel windows -------------------------------------------------------------------------------------------------------
    private static void TamokoNegate()
    {
        // Determined Challenge moves a Personality to the battle: Tamoko negates that movement.
        MatchState Build(out CardInstance card, out CardInstance perf, out CardInstance target, out CardInstance tam)
        {
            var s = Table(Phase.Battle, 1);
            card = Add(s, Def("determined-challenge-move"), 1, Zone.Hand); perf = Bf(s, "training-lion-samurai", 1);
            target = AtHome(s, "training-crane-samurai", 0, bowed: true); tam = Bf(s, Def("tamoko-negate"), 0);
            return s;
        }
        var st = Build(out var card, out var perf, out var target, out var tam);
        var e = MatchEngine.FromTestState(st);
        Accept(e, Use(e, 1, "determined-challenge-move", card.Id, target.Id, performerId: perf.Id));
        Eq(Window(e), "beforeActionReaction", "the window opens for Tamoko's controller: the action moves a Personality");
        Check(!Listed(e, 1, "tamoko-negate", tam.Id), "not offered to the actor");
        ToPlayer(e, 0);
        var cmd = Enabled(e, 0, "tamoko-negate", tam.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd); Close(e);
        Check(e.State.Cards[target.Id].Zone == Zone.Home && e.State.Cards[target.Id].Bowed, "his movement was negated: he stayed home, still bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the action's card is still spent");
        var plain = Build(out _, out _, out _, out var t2); plain.PriorityPlayerId = 0;
        Closed(MatchEngine.FromTestState(plain), "tamoko-negate", t2.Id, cmd with { CardId = t2.Id }, "with no action pending", 0, 1);
        // Independent Open: on the other player's turn she can target the active player's unbowed Personality.
        var o = Table(Phase.Action, 1); o.PriorityPlayerId = 0; var tm = AtHome(o, Def("tamoko-negate"), 0); var active = AtHome(o, "training-lion-samurai", 1);
        var q = MatchEngine.FromTestState(o);
        Check(Options(q, 0, "tamoko-open", tm.Id).Any(x => x.Enabled && (x.TargetIds ?? []).Contains(active.Id)), "her Open action is independent of the Reaction");
    }

    // Mendon's ruling (2026-10-31, issue #1152, reverses #1133): a Reaction is not tied to presence unless its text requires it. Tamoko's Reaction says nothing of the kind, so it is offered wherever she is:
    // at the battlefield, at home with another unit of her player's there, and at home with none.
    private static void ReactionNeedsPresence()
    {
        (MatchEngine E, CardInstance Tam) Play(bool tamokoAtBattle, bool otherUnit)
        {
            var s = Table(Phase.Battle, 1);
            var card = Add(s, Def("determined-challenge-move"), 1, Zone.Hand); var perf = Bf(s, "training-lion-samurai", 1);
            var target = AtHome(s, "training-crane-samurai", 0, bowed: true);
            var tam = tamokoAtBattle ? Bf(s, Def("tamoko-negate"), 0) : AtHome(s, Def("tamoko-negate"), 0);
            if (otherUnit) Bf(s, "training-crane-samurai", 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 1, "determined-challenge-move", card.Id, target.Id, performerId: perf.Id));
            ToPlayer(e, 0);
            return (e, tam);
        }
        var (present, tamoko) = Play(tamokoAtBattle: true, otherUnit: false);
        Check(Listed(present, 0, "tamoko-negate", tamoko.Id), "she is at the battlefield: her Reaction is offered");
        var (home, homeTamoko) = Play(tamokoAtBattle: false, otherUnit: true);
        Check(Listed(home, 0, "tamoko-negate", homeTamoko.Id), "she is at home: her Reaction is offered");
        var (alone, aloneTamoko) = Play(tamokoAtBattle: false, otherUnit: false);
        Check(Listed(alone, 0, "tamoko-negate", aloneTamoko.Id), "no unit of her player's at the battlefield: a Reaction needs no presence");
    }

    private static void IrezuDuelEnter()
    {
        MatchState Build(bool bowed, out CardInstance irezu, out CardInstance scorpion, out CardInstance duelist, out CardInstance strategy)
        {
            var s = Table(Phase.Battle, 1);
            irezu = Bf(s, Def("irezu-duel-enter"), 0, bowed); scorpion = Kw(Bf(s, "training-scorpion-veteran", 0), "Scorpion Clan");
            duelist = Bf(s, "training-lion-samurai", 1); strategy = Add(s, "training-duel-force", 1, Zone.Hand);
            return s;
        }
        var st = Build(false, out var irezu, out var scorpion, out var duelist, out var strat);
        var e = MatchEngine.FromTestState(st);
        Accept(e, Use(e, 1, "training-duel-force", strat.Id, scorpion.Id, performerId: duelist.Id));
        Eq(Window(e), "duelReaction", "the mandatory duel opens the before-the-duel window");
        Check(!Listed(e, 1, "irezu-duel-enter", irezu.Id), "not offered to the challenger");
        var cmd = Enabled(e, 0, "irezu-duel-enter", irezu.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Check(e.State.Cards[irezu.Id].Bowed, "Irezu bowed to pay his Reaction's cost");
        Check(e.State.PendingDuel is { } pending && pending.ChallengedId == irezu.Id && pending.ChallengerId == duelist.Id, "the duel now involves Irezu instead of the Scorpion Clan Personality");
        var bowed = Build(true, out var bi, out var bs, out var bd, out var bst);
        var eb = MatchEngine.FromTestState(bowed);
        Accept(eb, Use(eb, 1, "training-duel-force", bst.Id, bs.Id, performerId: bd.Id));
        Check(!CanUse(eb, 0, "irezu-duel-enter", bi.Id), "a bowed Irezu cannot pay the Reaction's cost");
        var plain = Build(false, out var pi, out _, out _, out _); plain.PriorityPlayerId = 0;
        Closed(MatchEngine.FromTestState(plain), "irezu-duel-enter", pi.Id, cmd with { CardId = pi.Id }, "with no duel pending", 0, 1);
        // Independent Battle move.
        var m = Table(Phase.Battle, 0); m.PriorityPlayerId = 0; var home = AtHome(m, Def("irezu-duel-enter"), 0); var sc = Kw(Bf(m, "training-scorpion-veteran", 0), "Scorpion Clan"); Bf(m, "training-lion-samurai", 1);
        var q = MatchEngine.FromTestState(m);
        Check(Options(q, 0, "irezu-move", home.Id).Any(x => x.Enabled && (x.TargetIds ?? []).Contains(sc.Id)), "his Battle move is independent of the Reaction");
    }

    // ---- entering-play watchers ---------------------------------------------------------------------------------------------------------------------------------
    private static void KikugoroHenshinEntry()
    {
        CardInstance watcher = null!; string entering = "";
        MatchEngine Buy(bool henshin)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            entering = s.Players[0].Provinces[0].CardId!; s.Cards[entering].DefinitionId = "training-crane-samurai"; s.Cards[entering].FaceUp = true;
            if (henshin) Kw(s.Cards[entering], "Henshin");
            watcher = AtHome(s, Def("kikugoro-henshin-entry"), 0);
            for (var i = 0; i < 6; i++) AtHome(s, "training-marketplace", 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering && a.Enabled).Command!);
            return e;
        }
        var h = Buy(true);
        Check(!Listed(h, 1, "kikugoro-henshin-entry", watcher.Id), "not offered to the other player");
        var cmd = Enabled(h, 0, "kikugoro-henshin-entry", watcher.Id).Command!;
        Reject(h, cmd with { PlayerId = 1, ExpectedVersion = h.Version });
        Accept(h, cmd);
        Eq(Window(h), "lookArrange", "the look at the top two Dynasty cards");
        var n = Buy(false);
        Check(!Listed(n, 0, "kikugoro-henshin-entry", watcher.Id), "a non-Henshin entry offers nothing");
        var a = Table(Phase.Action, 0); var k = AtHome(a, Def("kikugoro-henshin-entry"), 0);
        Closed(MatchEngine.FromTestState(a), "kikugoro-henshin-entry", k.Id, cmd with { CardId = k.Id }, "with no entering Personality", 0, 1);
    }

    private static void KaiuVillageArmor()
    {
        CardInstance village = null!;
        void Setup(MatchState s) { village = Add(s, Def("kaiu-village-armor"), 0, Zone.Region, province: 3); s.Players[0].Provinces[3].RegionId = village.Id; }
        var (e, host) = Recruit("training-crane-samurai", Setup);
        Check(Window(e) is "entryReaction" or "proclaim", "the entry window is open");
        Check(!Listed(e, 1, "kaiu-village-armor", village.Id), "not offered to the other player");
        var opt = Enabled(e, 0, "kaiu-village-armor", village.Id);
        Eq(opt.Gold!.Cost, 2, "pay 2 Gold");
        Reject(e, opt.Command! with { PlayerId = 1, ExpectedVersion = e.Version });
        var force = e.CardForceForTest(host); var known = e.State.Cards.Keys.ToHashSet();
        Accept(e, opt.Command!);
        var armor = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        Check(armor.Zone == Zone.Attached && armor.AttachedTo == host && armor.OwnerId == 0, "a Kaiu Armor attached to the entering Personality");
        Eq(e.CardForceForTest(host), force + 1, "+1F");
        Check(CanUse(e, 0, "kaiu-village-weapon", village.Id), "the Weapon half is the sibling Reaction and is still open");
        Close(e);
        Closed(e, "kaiu-village-armor", village.Id, opt.Command!, "after the entry window closed", 0, 1);
        var a = Table(Phase.Action, 0); var v2 = Add(a, Def("kaiu-village-armor"), 0, Zone.Region, province: 3); a.Players[0].Provinces[3].RegionId = v2.Id;
        Closed(MatchEngine.FromTestState(a), "kaiu-village-armor", v2.Id, opt.Command! with { CardId = v2.Id }, "with no entering Personality", 0, 1);
    }

    private static void SpidersWebAttach()
    {
        string web = ""; CardInstance weapon = null!;
        var (e, entered) = Recruit("training-crane-clan-samurai", s => { web = s.Players[0].StrongholdId; s.Cards[web].DefinitionId = Def("spiders-web-attach"); weapon = Add(s, "training-weapon", 0, Zone.Hand); }, marketplaces: 8);
        Check(Window(e) is "entryReaction" or "proclaim", "the entry window is open");
        Check(!e.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "spiders-web-attach"), "not offered to the other player");
        var cmd = Offer(e, 0, "spiders-web-attach", web, weapon.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Check(!CanUse(e, 0, "spiders-web-attach-kensai", web), "the Kensai discount is for a Kensai only");
        Accept(e, cmd);
        Check(e.State.Cards[weapon.Id].Zone == Zone.Attached && e.State.Cards[weapon.Id].AttachedTo == entered, "the Weapon is attached to the entering Personality");
        Close(e);
        Closed(e, "spiders-web-attach", web, cmd, "after the entry window closed", 0, 1);
        var a = Table(Phase.Action, 0); a.Cards[a.Players[0].StrongholdId].DefinitionId = Def("spiders-web-attach"); var w2 = Add(a, "training-weapon", 0, Zone.Hand);
        Closed(MatchEngine.FromTestState(a), "spiders-web-attach", a.Players[0].StrongholdId, cmd with { CardId = a.Players[0].StrongholdId, TargetId = w2.Id }, "with no entering Personality", 0, 1);
    }

    // ---- "after an action resolves" Reactions -------------------------------------------------------------------------------------------------------------------
    private static CardInstance FateTop(MatchState s, int owner) { var c = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }
    private static void PassC(MatchEngine e) => Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));

    private static void CenteringSoulStraighten()
    {
        MatchState Build(out CardInstance centering, out CardInstance ring, out CardInstance chukage)
        {
            var s = Table(Phase.Action, 0);
            var shugenja = Kw(AtHome(s, "training-crane-samurai", 0), "Shugenja");
            centering = Add(s, Def("centering-soul-straighten"), 0, Zone.Attached, parent: shugenja.Id);
            chukage = AtHome(s, "oracle-l5r-10072", 0); ring = AtHome(s, "training-ring-of-void", 0);
            for (var i = 0; i < 6; i++) FateTop(s, 0);
            return s;
        }
        var st = Build(out var centering, out var ring, out var chukage);
        var e = MatchEngine.FromTestState(st);
        Check(!CanUse(e, 0, "centering-soul-straighten", centering.Id), "not offered with no action resolved");
        Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
        Eq(Window(e), "afterActionResolvedReaction", "an action bowed a Ring");
        Check(!Listed(e, 1, "centering-soul-straighten", centering.Id), "not offered to the other player");
        Check(!CanUse(e, 0, "centering-soul-straighten-destroy", centering.Id), "not Void: the destroying sibling is not offered");
        var cmd = Offer(e, 0, "centering-soul-straighten", centering.Id, ring.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Check(!e.State.Cards[ring.Id].Bowed, "the Ring was straightened");
        Check(e.State.Cards[centering.Id].Bowed && e.State.Cards[centering.Id].Zone == Zone.Attached, "the Spell bowed as its cost and stays");
        Close(e);
        Closed(e, "centering-soul-straighten", centering.Id, cmd, "after the window closed", 0, 1);
    }

    private static void ClosingTheGapToBattle()
    {
        MatchState Build(out CardInstance card, out CardInstance moved, out CardInstance reserve, out CardInstance spell)
        {
            var s = Table(Phase.Battle, 1);
            var attacker = Bf(s, "training-shugenja", 1); spell = Attach(s, Def("tf-typhoon-surge-move"), attacker);
            Bf(s, "training-crane-samurai", 0); moved = AtHome(s, "training-crane-veteran", 0); reserve = AtHome(s, "training-merchant", 0, bowed: true);
            card = Add(s, Def("pe-closing-the-gap-to-battle"), 0, Zone.Hand);
            return s;
        }
        var st = Build(out var card, out var moved, out var reserve, out var spell);
        var e = MatchEngine.FromTestState(st);
        Check(!CanUse(e, 0, "pe-closing-the-gap-to-battle", card.Id, reserve.Id), "not offered with no action resolved");
        Accept(e, Offer(e, 1, "tf-typhoon-surge-move", spell.Id, moved.Id));
        Eq(Window(e), "afterActionResolvedReaction", "the action moved a Personality to the battlefield");
        Check(!Listed(e, 1, "pe-closing-the-gap-to-battle", card.Id), "the other player holds none");
        Check(CanUse(e, 0, "pe-closing-the-gap-home", card.Id), "the sibling way (home) is offered in the same window");
        var cmd = Use(e, 0, "pe-closing-the-gap-to-battle", card.Id, null, reserve.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Eq(e.State.Cards[reserve.Id].Zone, Zone.Battlefield, "he is at the battlefield"); Check(!e.State.Cards[reserve.Id].Bowed, "and his unit is straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // An action that moved nobody: no Reaction.
        var q = Table(Phase.Battle, 1);
        var sh = Bf(q, "training-shugenja", 1); var kw = Attach(q, "oracle-l5r-10701", sh); Bf(q, "training-crane-samurai", 0); var cg = Add(q, Def("pe-closing-the-gap-to-battle"), 0, Zone.Hand); var victim = Bf(q, "training-crane-samurai", 0);
        var qe = MatchEngine.FromTestState(q);
        Accept(qe, Offer(qe, 1, "tf-kiyoshi-bow", kw.Id, victim.Id));
        Check(Window(qe) != "afterActionResolvedReaction" || !CanUse(qe, 0, "pe-closing-the-gap-to-battle", cg.Id), "a bow moves nobody: Closing the Gap is not offered");
    }

    private static void KaxtCreateBattle()
    {
        MatchState Build(out CardInstance kaxt, out CardInstance destroyer)
        {
            var s = Table(Phase.Battle, 1);
            var host = Bf(s, "training-crane-samurai", 0); kaxt = Attach(s, Def("proxy677-kaxt-create-battle"), host); destroyer = Bf(s, "training-no-attachments-destroyer", 1);
            return s;
        }
        var st = Build(out var kaxt, out var destroyer);
        var e = MatchEngine.FromTestState(st);
        Check(!CanUse(e, 0, "proxy677-kaxt-create-battle", kaxt.Id), "not offered while it is in play");
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, kaxt.Id));
        Eq(e.State.Cards[kaxt.Id].Zone, Zone.FateDiscard, "Goju Kaxt was destroyed");
        Eq(Window(e), "afterActionResolvedReaction");
        Check(!Listed(e, 1, "proxy677-kaxt-create-battle", kaxt.Id), "not offered to the other player");
        var known = e.State.Cards.Keys.ToHashSet();
        var cmd = Use(e, 0, "proxy677-kaxt-create-battle", kaxt.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Check(CanUse(e, 0, "proxy677-kaxt-create-home", kaxt.Id), "the home way is the sibling Reaction");
        Accept(e, cmd);
        var created = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        Check(created.OwnerId == 0 && created.Zone == Zone.Battlefield && created.ProvinceIndex == 0, "a Kaxt at the current battlefield");
        Check(e.HasKeyword(created.Id, "Ninja") && e.CardForceForTest(created.Id) == 4, "a 4F Ninja Personality");
        Check(!CanUse(e, 0, "proxy677-kaxt-create-battle", kaxt.Id) && !CanUse(e, 0, "proxy677-kaxt-create-home", kaxt.Id), "once: one Kaxt for one destruction");
    }

    private static void IndecisionDiscard()
    {
        MatchState Build(out CardInstance first, out CardInstance second, out CardInstance indecision)
        {
            var s = Table(Phase.Action, 1);
            first = Add(s, "training-bushido-virtue", 1, Zone.Hand); second = Add(s, "training-bushido-virtue", 1, Zone.Hand);
            for (var i = 0; i < 3; i++) Add(s, "training-item", 1, Zone.Hand);
            indecision = Add(s, Def("indecision-discard"), 0, Zone.Hand);
            return s;
        }
        var st = Build(out var first, out var second, out var indecision);
        var e = MatchEngine.FromTestState(st);
        Accept(e, Use(e, 1, "training-limited-draw", first.Id));
        Check(Window(e) != "afterActionResolvedReaction" && !CanUse(e, 0, "indecision-discard", indecision.Id), "one hand-filling action: no window, not offered");
        PassC(e);
        Accept(e, Use(e, 1, "training-limited-draw", second.Id));
        Eq(Window(e), "afterActionResolvedReaction", "the second action opens the window");
        Eq(e.State.PriorityPlayerId, 0, "Indecision's holder is asked");
        Check(!CanUse(e, 1, "indecision-discard", indecision.Id), "not offered to the acting player");
        var cmd = Use(e, 0, "indecision-discard", indecision.Id);
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var hand = e.State.Players[1].Hand.ToArray();
        Accept(e, cmd);
        Eq(Window(e), "deciderChoice", "the player of the action chooses what he discards");
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, CardId: indecision.Id, TargetId: hand[0]));
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, CardId: indecision.Id, TargetId: hand[1]));
        if (e.State.PendingDeciderChoice is not null) Accept(e, new GameCommand("chooseDecider", 1, e.Version, CardId: indecision.Id, Choice: "done"));
        Eq(e.State.Players[1].Hand.Count, hand.Length - 2, "two cards discarded");
        Eq(e.State.Cards[indecision.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0; var mover = AtHome(b, "training-crane-samurai", 0); Bf(b, "training-lion-samurai", 1); var i2 = Add(b, Def("indecision-discard"), 0, Zone.Hand);
        var q = MatchEngine.FromTestState(b);
        Check(CanUse(q, 0, "indecision-move", i2.Id, mover.Id), "its Battle move is independent of the Reaction");
        Closed(q, "indecision-discard", i2.Id, cmd with { CardId = i2.Id }, "in a plain Battle position", 0, 1);
    }

    private static void LyingInAmbushHome()
    {
        MatchState Build(out CardInstance nimuro, out CardInstance card)
        {
            var s = Table(Phase.Action, 0);
            for (var i = 0; i < 6; i++) AtHome(s, "training-marketplace", 0);
            nimuro = AtHome(s, "oracle-l5r-10463", 0); card = Add(s, Def("pf-lying-in-ambush-home"), 0, Zone.Hand);
            return s;
        }
        var st = Build(out var nimuro, out var card);
        var e = MatchEngine.FromTestState(st);
        Check(!CanUse(e, 0, "pf-lying-in-ambush-home", card.Id), "not offered with no creating action resolved");
        Accept(e, Enabled(e, 0, "nimuro-create", nimuro.Id).Command!);
        Eq(Window(e), "afterActionResolvedReaction", "the creating action resolved");
        Check(!Listed(e, 1, "pf-lying-in-ambush-home", card.Id), "not offered to the other player");
        var cmd = Enabled(e, 0, "pf-lying-in-ambush-home", card.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var known = e.State.Cards.Keys.ToHashSet();
        Accept(e, cmd);
        var made = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        Check(made.OwnerId == 0 && made.Zone == Zone.Home && e.CardForceForTest(made.Id) == 2 && e.HasKeyword(made.Id, "Samurai"), "a 2F Samurai Personality created at home");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
    }

    private static void EmpressAddressAction()
    {
        MatchState Build(out CardInstance card, out CardInstance courtier, out CardInstance lost, out CardInstance ok, out CardInstance archer)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            card = Add(s, Def("empress-address-action"), 0, Zone.Hand); courtier = Kw(AtHome(s, "training-crane-courtier", 0), "Courtier");
            lost = Bf(s, "training-crane-samurai", 0); archer = Bf(s, "training-followed-attacker", 1); ok = Add(s, "training-crane-veteran", 0, Zone.Dead);
            return s;
        }
        var st = Build(out var card, out var courtier, out var lost, out var ok, out var archer);
        var e = MatchEngine.FromTestState(st);
        Accept(e, Offer(e, 1, "training-followed-recon-attack", archer.Id, lost.Id));
        for (var i = 0; i < 10 && !CanUse(e, 0, "empress-address-action", card.Id) && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(CanUse(e, 0, "empress-address-action", card.Id), "the Reaction is offered after the action");
        Check(!Listed(e, 1, "empress-address-action", card.Id), "not offered to the attacker");
        var cmd = Options(e, 0, "empress-address-action", card.Id).First(o => o.Enabled && (o.TargetIds ?? []).Contains(ok.Id)).Command! with { TargetId = ok.Id };
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        var honor = Honor(e, 0);
        Accept(e, cmd);
        Check(e.State.Cards[ok.Id].Zone == Zone.Home && e.State.Cards[ok.Id].OwnerId == 0, "he is in his controller's home");
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        Eq(Honor(e, 0), honor + 2, "2 Honor gained");
        var plain = Build(out _, out _, out _, out _, out _); plain.PriorityPlayerId = 0;
        var p = MatchEngine.FromTestState(plain);
        Closed(p, "empress-address-action", plain.Cards.Values.First(c => c.DefinitionId == Def("empress-address-action")).Id, cmd, "in a plain Battle position", 0, 1);
    }

    private static void StormlessFuryAttach()
    {
        MatchState Build(out CardInstance first, out CardInstance fury)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            first = Bf(s, "training-thunder-shugenja", 0); Bf(s, "training-thunder-shugenja", 0); Bf(s, "training-crane-veteran", 1);
            fury = Add(s, Def("stormless-fury-attach"), 0, Zone.Hand);
            return s;
        }
        var st = Build(out var first, out var fury);
        var e = MatchEngine.FromTestState(st);
        Check(!CanUse(e, 0, "stormless-fury-attach", fury.Id), "not offered before a Battle action resolved");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: first.Id, AbilityId: "training-weapon-attack"));
        Eq(Window(e), "battleActionReaction");
        Check(!Listed(e, 1, "stormless-fury-attach", fury.Id), "not offered to the other player");
        var cmd = Enabled(e, 0, "stormless-fury-attach", fury.Id).Command!;
        Reject(e, cmd with { PlayerId = 1, ExpectedVersion = e.Version });
        Accept(e, cmd);
        Check(e.State.Cards[fury.Id].Zone == Zone.Attached && e.State.Cards[fury.Id].AttachedTo == first.Id, "attached to the performing Thunder Shugenja");
        var plain = Build(out _, out var f2);
        Closed(MatchEngine.FromTestState(plain), "stormless-fury-attach", f2.Id, cmd with { CardId = f2.Id }, "in a plain Battle position", 0, 1);
    }

    // ---- "when paying a Gold cost" Reactions (payment sources) ------------------------------------------------------------------------------------------------
    private static void BowAllGold(MatchState s, int owner) { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true; }
    private static string Token(CardInstance card, string mode) => card.Id + "#" + mode;

    private static void RaniSpellDiscount()
    {
        MatchEngine Table6(out CardInstance rani, out CardInstance dead, out CardInstance spell, out CardInstance bazaar, bool raniBowed = false)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-6855"; // Shinden Asahina: Equip (Battle) as a player ability
            BowAllGold(s, 0);
            rani = Add(s, Def("rani-exp-spell-discount"), 0, Zone.Battlefield, raniBowed, 0); Bf(s, "training-lion-samurai", 1);
            dead = Add(s, "training-crane-samurai", 0, Zone.Dead); spell = Add(s, "oracle-l5r-1471", 0, Zone.Hand); bazaar = AtHome(s, "oracle-l5r-6529", 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Table6(out var rani, out var dead, out var spell, out var bazaar);
        var discount = Token(rani, "discount:" + dead.Id);
        var attaching = new PaymentSubject(e.State.Cards[spell.Id], PaymentKind.Equip, Wielder: e.State.Cards[rani.Id]);
        Eq(e.GoldProduced(discount, 0, attaching), 3, "the dead Personality's printed Chi comes off the Spell's cost");
        Check(e.PaymentOptions(0, 6, attaching).Any(p => p.SourceIds.ToHashSet().SetEquals([bazaar.Id, Token(bazaar, "reaction"), discount])), "6 Gold: the Bazaar's 3 and Rani's 3");
        var equip = Enabled(e, 0, "equip-battle", performerId: rani.Id);
        Accept(e, equip.Command! with { TargetId = spell.Id, SourceIds = [bazaar.Id, Token(bazaar, "reaction"), discount] });
        Eq(e.State.Cards[spell.Id].AttachedTo, rani.Id, "the Spell attached to Rani");
        Eq(e.State.Cards[dead.Id].Zone, Zone.RemovedFromGame, "the dead Personality was removed from the game");
        Eq(e.State.Cards[rani.Id].AbilityUses.GetValueOrDefault("rani-exp-spell-discount"), e.State.TurnNumber, "her Reaction is spent");
        // Outside her payment: an Item attaching to her, a Spell for another Shugenja, a recruit, or a bowed Rani gets no discount.
        var f = Table6(out var rani2, out _, out var spell2, out _);
        bool Discounts(MatchEngine en, PaymentSubject subject) => en.PaymentOptions(0, 6, subject).Any(p => p.SourceIds.Any(id => id.Contains("#discount:")));
        Check(!Discounts(f, new PaymentSubject(f.State.Cards[spell2.Id], PaymentKind.Recruit)), "paying for a recruit is not a Spell attaching to Rani");
        Check(!Discounts(f, new PaymentSubject(f.State.Cards[spell2.Id], PaymentKind.Equip)), "a payment with no wielder is not a Spell attaching to Rani");
        var bowed = Table6(out var rb, out _, out var sb, out _, raniBowed: true);
        Check(!Discounts(bowed, new PaymentSubject(bowed.State.Cards[sb.Id], PaymentKind.Equip, Wielder: bowed.State.Cards[rb.Id])), "a bowed Rani takes no Reaction");
        // The Battle straighten (even if bowed) is independent of the Reaction.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0; var r3 = Add(s3, Def("rani-exp-spell-discount"), 0, Zone.Battlefield, true, 0); Bf(s3, "training-lion-samurai", 1);
        var g = MatchEngine.FromTestState(s3);
        Accept(g, Enabled(g, 0, "rani-exp-straighten", r3.Id).Command!);
        Check(!g.State.Cards[r3.Id].Bowed, "her Battle action straightens her");
    }

    private static void ProfitsGoldSource()
    {
        MatchState Build(out CardInstance host, out CardInstance profits, out CardInstance empty, out CardInstance weapon, int tokens = 3)
        {
            var s = Table(Phase.Action, 0); BowAllGold(s, 0);
            host = AtHome(s, "training-crane-samurai", 0); profits = AtHome(s, Def("pay-profits-gold"), 0); empty = AtHome(s, Def("pay-profits-gold"), 0);
            for (var i = 0; i < tokens; i++) profits.Tokens.Add(new CardToken("w" + i, "Wealth", "test", null, 0));
            weapon = Add(s, "training-costly-weapon", 0, Zone.Hand);
            return s;
        }
        var st = Build(out var host, out var profits, out var empty, out var weapon);
        var e = MatchEngine.FromTestState(st);
        var gold = profits.Id + "#profits-gold";
        var subject = new PaymentSubject(weapon, PaymentKind.Equip, Wielder: host);
        var sources = e.PaymentSourcesForTest(0, subject);
        Eq(sources.Single(x => x.Id == gold).Produced, 3, "Gold equal to its Wealth tokens");
        Check(!sources.Any(x => x.Id == empty.Id + "#profits-gold"), "a Profits with no Wealth token produces nothing");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: weapon.Id, TargetId: host.Id, SourceIds: [gold]));
        Eq(e.State.Cards[weapon.Id].AttachedTo, host.Id, "paid for with the discarded Profits' 3 Gold");
        Eq(e.State.Cards[profits.Id].Zone, Zone.FateDiscard, "choosing it discarded Profits");
        Check(e.State.Cards[empty.Id].Zone == Zone.Home, "the other Profits is untouched");
        // In the hand it is no source; and its Limited put-into-play is independent.
        var h = Table(Phase.Action, 0); BowAllGold(h, 0); var hh = AtHome(h, "training-crane-samurai", 0); var inHand = Add(h, Def("pay-profits-gold"), 0, Zone.Hand); inHand.Tokens.Add(new CardToken("w", "Wealth", "test", null, 0)); var hw = Add(h, "training-costly-weapon", 0, Zone.Hand);
        var he = MatchEngine.FromTestState(h);
        Check(!he.PaymentSourcesForTest(0, new PaymentSubject(hw, PaymentKind.Equip, Wielder: hh)).Any(x => x.CardId == inHand.Id), "Profits in hand offers nothing");
        Accept(he, Enabled(he, 0, "pay-profits-enter", inHand.Id).Command!);
        Eq(he.State.Cards[inHand.Id].Zone, Zone.Home, "Profits is put into play");
    }

    private static void TanimuraGold()
    {
        var s = Table(Phase.Action, 0); BowAllGold(s, 0);
        var tan = AtHome(s, Def("tanimura-produce-gold"), 0); var host = AtHome(s, "training-crane-samurai", 0); var item = Add(s, "training-item", 0, Zone.Hand); var item2 = Add(s, "training-item", 0, Zone.Hand); var recruit = Add(s, "training-lion-samurai", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(tan.Id, 0, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip)), 3, "paying for an action he produces 3 Gold");
        Check(e.PaymentOptions(0, 3, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip)).Any(o => o.SourceIds.SequenceEqual([tan.Id])), "the payment for an action offers him");
        Check(e.PaymentOptions(0, 3, new PaymentSubject(e.State.Cards[recruit.Id], PaymentKind.Recruit)).All(o => !o.SourceIds.Contains(tan.Id)), "a recruit is not an action: he is no source for it");
        Check(e.PaymentOptions(0, 3).All(o => !o.SourceIds.Contains(tan.Id)), "a payment that is for nothing in particular does not offer him");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, TargetId: host.Id, SourceIds: [tan.Id]));
        Eq(e.State.Cards[item.Id].AttachedTo, host.Id, "the action was paid for with his Gold");
        Check(!e.State.Cards[tan.Id].Bowed, "producing Gold does not bow him");
        Eq(e.State.Cards[tan.Id].AbilityUses["tanimura-produce-gold"], e.State.TurnNumber, "the Reaction is used up for the turn");
        Check(e.PaymentOptions(0, 3, new PaymentSubject(e.State.Cards[item2.Id], PaymentKind.Equip)).All(o => !o.SourceIds.Contains(tan.Id)), "a second payment this turn cannot use him");
        var b = Table(Phase.Action, 0); BowAllGold(b, 0); var t2 = AtHome(b, Def("tanimura-produce-gold"), 0, bowed: true); var i2 = Add(b, "training-item", 0, Zone.Hand);
        var be = MatchEngine.FromTestState(b);
        Check(be.PaymentOptions(0, 3, new PaymentSubject(be.State.Cards[i2.Id], PaymentKind.Equip)).All(o => !o.SourceIds.Contains(t2.Id)), "a bowed Tanimura produces nothing");
        // The card's other Reaction is a different window (a Battle action targeting him) and is not listed in the Action phase.
        Check(!CanUse(e, 0, "tanimura-political-pay", tan.Id) && !CanUse(e, 1, "tanimura-political-pay", tan.Id), "the targeted-by-action sibling is not on offer outside its window");
    }

    private static void SinghGold()
    {
        var s = Table(Phase.Action, 0); BowAllGold(s, 0);
        var singh = AtHome(s, Def("singh-exp2-gold"), 0); var host = AtHome(s, "training-crane-samurai", 0); var item = Add(s, "training-item", 0, Zone.Hand); var item2 = Add(s, "training-item", 0, Zone.Hand); var recruit = Add(s, "training-lion-samurai", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(singh.Id, 0, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip)), 3, "3 Gold for an action (an Equip)");
        Check(!e.PaymentOptions(0, 1, new PaymentSubject(e.State.Cards[recruit.Id], PaymentKind.Recruit)).Any(p => p.SourceIds.Contains(singh.Id)), "a recruit is not an action: nothing");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, TargetId: host.Id, SourceIds: [singh.Id]));
        Check(!e.State.Cards[singh.Id].Bowed, "producing does not bow him");
        Eq(e.State.Cards[singh.Id].AbilityUses.GetValueOrDefault("singh-exp2-gold"), e.State.TurnNumber, "the Reaction is spent");
        Check(!e.PaymentOptions(0, 2, new PaymentSubject(e.State.Cards[item2.Id], PaymentKind.Equip)).Any(p => p.SourceIds.Contains(singh.Id)), "once a turn");
        var b = Table(Phase.Action, 0); BowAllGold(b, 0); var s2 = AtHome(b, Def("singh-exp2-gold"), 0, bowed: true); var i3 = Add(b, "training-item", 0, Zone.Hand);
        var be = MatchEngine.FromTestState(b);
        Check(!be.PaymentOptions(0, 2, new PaymentSubject(be.State.Cards[i3.Id], PaymentKind.Equip)).Any(p => p.SourceIds.Contains(s2.Id)), "a bowed Singh produces nothing");
        // The independent Battle destroy, paid with Gold equal to the target's cost.
        var bt = Table(Phase.Battle, 0); bt.PriorityPlayerId = 0; var sg = Bf(bt, Def("singh-exp2-gold"), 0); var bare = Bf(bt, "training-lion-samurai", 1);
        var eb = MatchEngine.FromTestState(bt);
        var option = Enabled(eb, 0, "singh-exp2-destroy", sg.Id);
        var quote = option.TargetGold![bare.Id];
        Accept(eb, option.Command! with { TargetId = bare.Id, SourceIds = quote.Payments.First().SourceIds });
        Close(eb);
        Eq(eb.State.Cards[bare.Id].Zone, Zone.Dead, "his Battle destroy is independent of the Reaction");
    }
}
