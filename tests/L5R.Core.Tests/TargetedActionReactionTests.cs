using static RegressionAssertions;
using L5R.Core;

// Redirection and protective reactions (issue #335): Ezuno, Blade of Champions, Yurishi, Jinn-Kuen exp2, Mochitoko exp and the primitives under
// them (TargetedActionReactions.cs, AbilityLimits.cs); readings in docs/decisions/2026-09-19-redirection-and-protective-reactions.md. Tests drive
// explicit positions through QueryActions and offered commands. Default table: Battle Phase, player 1 the Attacker and the acting player, player 0
// the Defender whose reacting cards stand at the Defender's province 0.
internal static class TargetedActionReactionTests
{
    private const string Ezuno = "oracle-l5r-5086", Blade = "oracle-l5r-1004", Yurishi = "oracle-l5r-2862", JinnKuen = "oracle-l5r-9445", Mochitoko = "oracle-l5r-3152";
    private const string Champion = "oracle-l5r-1292", Destrier = "oracle-l5r-10334", Llama = "oracle-l5r-10686", TokoruBear = "oracle-l5r-10025", Kazan = "oracle-l5r-4185";
    private const string Kouki = "oracle-l5r-10063", JusticeOfTheCrane = "oracle-l5r-10528", Incapacitated = "oracle-l5r-3730", EarthFlows = "oracle-l5r-8038";
    private const string Bow = "training-bow-at-battle", BowCard = "training-bow-enemy-card-at-battle", Destroy = "training-destroy-no-attachments";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Attack presentation uses engine kinds and explicit performers, survives reaction windows for both viewers, clears after resolution, and never exposes hidden sources", AttackPresentation),
            ("Published shape: the five cards are the printed ones (Ezuno F4 C3 Cost 7 Honor Req 3; Yurishi F4 C3 Cost 7 no Honor Req, Spider Clan, loses 3 Honor on entry; Jinn-Kuen F2 C4 Cost 7; Mochitoko F6 C3 Cost 9; Blade +3F +1C Cost 5 Focus 3 Unique Weapon), each whole and with its scan shipped, on ONE shared Reaction trigger", PublishedShape),
            ("The per-turn use limit: every ability keeps the default of once per turn, the ten former id-list exemptions are now Unlimited on their own definitions, and Mochitoko's is twice -- pinned against the old list", UseLimitParity),
            ("Ezuno redirects a non-Attack action (issue #335): another player's Battle action targeting his Shugenja at his location opens the before-action window for his controller, nothing has happened yet, the reaction makes the action target Ezuno, the acting player may still answer, and the action then resolves on Ezuno; the Shugenja is untouched and hidden information stays hidden", EzunoRedirectsAnAction),
            ("Ezuno is offered only for a legal redirect: never for his own controller's action, an action at Ezuno himself, a non-Shugenja, a Shugenja at another location (Home v battlefield, one battlefield v another), while bowed or spent -- and, Mendon's ruling, only when the action would be ACCEPTED against Ezuno (a target that must be dishonorable is not redirected to an honorable Ezuno)", EzunoOnlyWhenLegal),
            ("Ezuno redirects an Attack: the attack window opens because Ezuno is a legal target whatever his Force (Ranged 5 and Melee 2 alike; the strength only decides whether the Attack destroys him); redirected, the Attack targets Ezuno and the Shugenja survives, the Attack's paid costs stay paid, and the reactor is spent", EzunoRedirectsAnAttack),
            ("Redirection and costs: an action's costs are paid once, when it resolves, for the card it then targets -- and a redirect is only legal if the payment the acting player committed still fits the new target's price (target-priced action: cheaper new target is fine, dearer one is not)", RedirectAndCosts),
            ("Reactions key on the NEW target after a redirect (Mendon's question): a Champion of Thunder in the Shugenja's unit is offered before the redirect and not after; one in Ezuno's unit only after -- and a negation already played on the old card stays with the old card", ReactionsFollowTheNewTarget),
            ("Blade of Champions: a Battle action targeting a card in its unit opens the window; one option per legal new target of the controller's (never the original, the Blade, an enemy card, or a card the action could not legally target); choosing bows the Blade and the action lands on the chosen card; an illegal choice is rejected", BladeRedirects),
            ("Blade of Champions may redirect to ITSELF (Mendon): it is a candidate wherever the action accepts it, judged as though already bowed for the cost -- a bowed-card-only action still reaches the unbowed Blade", BladeMayRedirectToItself),
            ("Blade of Champions is offered only for a Battle action against a card in ITS unit, while unbowed and with a legal candidate: not for an Open action, a card outside the unit, a bowed Blade, or when no other card of the controller's could legally be targeted; a Follower or an Item in the unit counts as a card in the unit", BladeOnlyWhenApplicable),
            ("Blade of Champions and an Attack: the Attack is redirected to the chosen card, its strength follows the new target (a Destrier's -3) and the new target must be legal for it (a Force above the strength is still legal and simply survives; a Personality with a Follower to hide behind is not)", BladeRedirectsAnAttack),
            ("Yurishi: another player's Battle action targeting him lets him disappear -- home -- and, no longer a legal target for an action that needs him at the battlefield, the action's effects on him are negated; the action's other effects and costs stand; the event log says so", YurishiNegatesWhenNoLongerLegal),
            ("Yurishi's negation stops an Honor loss the action inflicts on his controller (an effect of the action, Mendon), while without the reaction it is lost", YurishiNegationStopsHonorLoss),
            ("Yurishi re-checks legality rather than assuming it: an action that does not need him at the battlefield (The Earth Flows, moving a Personality to it) is still legal after he moves home and resolves on him; and Battle actions only, another player's only, from a battlefield, from an unbowed Yurishi", YurishiRecheckStillLegal),
            ("Yurishi against an Attack: he disappears and the Ranged 5 Attack that would have destroyed him misses; without the reaction he dies (control), and a Melee too weak to hit him never opens a window", YurishiEscapesAnAttack),
            ("Yurishi enters play losing 3 Honor automatically (a mandatory entry effect, never offered as a choice)", YurishiEntryHonorLoss),
            ("Jinn-Kuen's Political Reaction: another player's action targeting a Merchant of his controller's -- Jinn-Kuen himself included -- offers the negation for 2 Gold through the Gold quote, and paying bows the chosen sources and negates the action's effects on the Merchant; not a non-Merchant, not another player's Merchant, not for a controller who cannot pay, and a cost of 0 is offered with no Gold at all", JinnKuenPaysAndNegates),
            ("Jinn-Kuen's negation is the blanket one: every kind of effect the action would apply to the Merchant is negated -- bow, Force penalty, movement, dishonor, destruction, an Attack's destruction -- and the acting player's own costs and the rest of the action still happen", JinnKuenBlanketNegation),
            ("Jinn-Kuen's Open action straightens a Holding or a Stronghold (either side's), and nothing else", JinnKuenStraightens),
            ("Daigotsu Bofana's announce-time trait fires ONCE PER ACTION per Bofana (Mendon): redirected away after it fired -> no second fire; redirected onto him -> exactly one fire, judged when the window closes and before the action resolves; two redirects -> still one; an Attack redirected onto him the same", BofanaTraitFiresOncePerAction),
            ("Mochitoko: twice per turn, after an action targets his controller's Follower at his location, its bowing and destruction are negated -- the first use, the second use, then no more that turn (not offered, the action resolves), and the next turn resets it; the use count is exactly what the limit reads", MochitokoTwicePerTurn),
            ("Mochitoko negates bow-only, destroy-only and bow-then-destroy actions (Tokoru's Bear bows a Follower and destroys it as an attachment: both negated at once, several kinds on one action) and a Melee Attack that would destroy the Follower", MochitokoKinds),
            ("Mochitoko and a Follower paid as a COST (Mendon): Hiruma Tensin's \"bow your performing Follower\" -- the window opens before the cost is paid, Mochitoko negates that bowing, and the ENTIRE action is negated and pays nothing (no effect, no Honor-loss cost, no draw); without him the cost is paid and the action resolves", MochitokoBlocksAFollowerCost),
            ("Mochitoko is offered only when it can matter: a Follower of his controller's whose host is at his location, targeted by an action that could bow or destroy it -- not an enemy Follower, a Follower elsewhere, a Personality, a pure Force penalty, while bowed, or with both uses spent", MochitokoOnlyWhenApplicable),
            ("Ranged and Melee Attacks need no card target (Mendon): every Attack is offered as legal with NO target (and the option says so), a card target as before, and -- for an ability that says so -- the current battlefield's province; an Attack that names no card (no target, or a province) opens no window although Ezuno, Yurishi, Mochitoko, the Blade and a strength reactor are all in play, while the same Attack aimed at a card does", AttacksNeedNoCardTarget),
            ("Yoritomo Sakuma (whole-action negation, #338) beside Ezuno's redirect: a Political action redirected onto Ezuno still lets Sakuma react -- pay 2 Gold and it resolves on Ezuno, decline and it is negated as a whole -- Ezuno's redirect and Sakuma's reaction may come in either order, and once Sakuma has negated the action Ezuno is offered nothing", SakumaBesideRedirect),
            ("Bofana's once-per-action guard holds by itself: with his once-per-phase limit wiped after the first judgement, the window's close does not judge him again for the same action, and the next action does", BofanaGuardIsPerAction),
            ("Carpenter Castle (Mendon, closes open question 5): \"If this targeted a card, gain 2 Honor\" is a TARGETING effect, not a hit effect -- +2 Honor once when the Attack hits, is negated, is redirected (Ezuno, Blade) or dodged (Yurishi), and none when it targeted no card or a province", CarpenterHonorFollowsTargeting),
            ("A cost-block predicate keyed on the cost's verb (Mendon ruling 2 / #352): a Follower bowed as a cost is blocked by a negated bow (or every effect), never by a negated destruction alone; and the acting player's own Honor cost is never negated with a card's effects", CostBlockIsPerVerb),
            ("Every enabled command offered inside a redirect or negation window validates and is accepted on a fresh engine (Ezuno, Blade with every candidate, Yurishi, Jinn-Kuen, Mochitoko)", OfferedCommandsAllValidate),
        ];
        runner.Run("targeted-action-reactions", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void AttackPresentation()
    {
        var (s, sh) = ReactiveDefence(); var (castle, stronghold) = Siege(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(1).Single(a => a.Enabled && a.Command?.AbilityId == "carpenter-castle-siege" && a.Command.PerformerId == castle.Id);
        Eq(option.Attack, new AttackCueView("Ranged", castle.Id));
        var version = e.Version;
        foreach (var player in new[] { 0, 1 }) Eq(e.GetView(player).PendingAttack, null);
        Eq(e.Version, version, "presentation queries mutated the match");
        Accept(e, SiegeAt(e, castle, stronghold, sh.Id));
        foreach (var player in new[] { 0, 1 }) Eq(e.GetView(player).PendingAttack, option.Attack);
        ClosePassing(e);
        foreach (var player in new[] { 0, 1 }) Eq(e.GetView(player).PendingAttack, null, "resolved Attack retained a cue");

        var r = MochitokoTable();
        var wielder = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var weapon = Add(r.S, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id);
        e = MatchEngine.FromTestState(r.S);
        option = e.QueryActions(1).Single(a => a.Enabled && a.CardId == weapon.Id && a.Command?.AbilityId == "training-weapon-attack");
        Eq(option.Attack, new AttackCueView("Melee", weapon.Id), "attachment source changed to its host");
        Accept(e, Offer(e, 1, "training-weapon-attack", weapon.Id, r.Follower.Id));
        Eq(e.GetView(0).PendingAttack, option.Attack);
        ClosePassing(e); Eq(e.GetView(1).PendingAttack, null);

        s = Table(actor: 0); Add(s, Mochitoko, 0, Zone.Battlefield, province: 0);
        var tensin = Add(s, "oracle-l5r-3332", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: tensin.Id);
        var enemy = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
        e = MatchEngine.FromTestState(s);
        option = e.QueryActions(0).Single(a => a.Enabled && a.Command?.AbilityId == "tensin-follower-attack" && a.Command.PerformerId == follower.Id);
        Eq(option.Attack, new AttackCueView("Ranged", follower.Id));
        Accept(e, Offer(e, 0, "tensin-follower-attack", tensin.Id, enemy.Id, follower.Id));
        Eq(Window(e), "beforeActionReaction");
        foreach (var player in new[] { 0, 1 }) Eq(e.GetView(player).PendingAttack, option.Attack);
        ClosePassing(e); Eq(e.GetView(0).PendingAttack, null);

        // Defensive projection: a queued command is not itself permission to expose its
        // card ID. A Strategy still in a private hand must not become a public mount.
        s = Table(); var hidden = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        s.PendingAnnouncedAction = new("useAbility", 1, s.Version, hidden.Id, AbilityId: "training-honor-cost-attack");
        e = MatchEngine.FromTestState(s); Eq(e.GetView(0).PendingAttack, null);
        s.PendingAnnouncedAction = new("useAbility", 1, s.Version, castle.Id, AbilityId: Bow);
        e = MatchEngine.FromTestState(s); Eq(e.GetView(0).PendingAttack, null);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"tr-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // MatchEngine.State is a detached copy, so a mid-test change is made on a copy and the engine rebuilt from it.
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) { var st = e.State; edit(st); return MatchEngine.FromTestState(st); }

    // The economy scenario with both hands emptied. Player 1 is the Attacker by default (the acting player); the battlefield is the Defender's province 0.
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability
            && a.Command.PerformerId == performerId && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == cardId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        // A target-priced action ships one quote per legal target: the payment offered is the one for THE chosen target.
        var command = option!.Command! with { TargetId = targetId };
        return option.TargetGold is { } quotes && quotes.TryGetValue(targetId, out var quote) ? command with { SourceIds = quote.Payments.FirstOrDefault()?.SourceIds ?? [] } : command;
    }

    private static string? Window(MatchEngine e) => e.State.PendingChoice?.Type;
    private static bool InWindow(MatchEngine e) => Window(e) is "beforeActionReaction" or "attackReaction";

    private static ActionOption[] Listed(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();

    // The one enabled offer of `ability` (optionally for a chosen target), as the command the engine proposes.
    private static GameCommand ReactionCommand(MatchEngine e, int player, string ability, string? targetId = null)
    {
        var enabled = Listed(e, player, ability).Where(a => a.Enabled && (targetId is null || a.Command!.TargetId == targetId)).ToArray();
        Check(enabled.Length == 1, $"{ability} for player {player}{(targetId is null ? "" : " -> " + targetId)}: expected exactly one enabled offer, got {enabled.Length} of "
            + string.Join("; ", Listed(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return enabled[0].Command!;
    }

    private static bool Offered(MatchEngine e, int player, string ability) => Listed(e, player, ability).Any(a => a.Enabled);

    // Both players pass in turn until the window closes and the held action resolves.
    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; InWindow(e) && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(!InWindow(e), "the window did not close");
    }

    private static bool EventLogged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);

    // Carpenter Castle Siege: Ranged 5, the Stronghold's Battle action with a Castle as performer. Player 1's Stronghold becomes Carpenter Castle.
    private static (CardInstance Castle, string StrongholdId) Siege(MatchState s, int owner = 1)
    {
        var strongholdId = s.Players[owner].StrongholdId;
        s.Cards[strongholdId].DefinitionId = "oracle-l5r-1218";
        return (Add(s, "training-castle", owner, Zone.Home), strongholdId);
    }
    private static GameCommand SiegeAt(MatchEngine e, CardInstance castle, string strongholdId, string targetId, int player = 1) =>
        new("useAbility", player, e.Version, CardId: strongholdId, PerformerId: castle.Id, TargetId: targetId, AbilityId: "carpenter-castle-siege");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }

    // ---- published shape and the limit -----------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.PersonalityWave30.Cards.Length, 4, "Ezuno, Yurishi, Jinn-Kuen, Mochitoko"); Eq(PublishedContent.ItemWave4.Cards.Length, 1, "Blade of Champions");
        var ez = CardCatalog.Get(Ezuno);
        Check(!ez.ScenarioOnly && ez.Type == CardType.Personality && ez.Name == "Mirumoto Ezuno" && ez.Clan == "Dragon");
        Eq(ez.Force, 4); Eq(ez.Chi, 3); Eq(ez.GoldCost, 7); Eq(ez.HonorRequirement, 3); Eq(ez.PersonalHonor, 3);
        foreach (var k in new[] { "Dragon Clan", "Samurai", "Duelist", "Yojimbo" }) Check(ez.Keywords!.Contains(k), k);
        Eq(ez.Text, "Reaction: When another player's action would target one of your Shugenja at Ezuno's location, it targets Ezuno instead, if legal.");
        Check(ez.Abilities!.SequenceEqual(new[] { "ezuno-redirect" }));
        var y = CardCatalog.Get(Yurishi);
        Check(!y.ScenarioOnly && y.Type == CardType.Personality && y.Name == "Goju Yurishi" && y.Clan == "Spider", "Yurishi's Clan is Spider (Ninja is only a keyword)");
        Eq(y.Force, 4); Eq(y.Chi, 3); Eq(y.GoldCost, 7); Eq(y.PersonalHonor, 0); Check(y.HonorRequirement is null, "no Honor Requirement"); Eq(y.EntryHonorLoss, 3);
        foreach (var k in new[] { "Spider Clan", "Shadowlands", "Ninja", "Dragon Child" }) Check(y.Keywords!.Contains(k), k);
        Check(y.Abilities!.SequenceEqual(new[] { "yurishi-disappear" }));
        var j = CardCatalog.Get(JinnKuen);
        Check(!j.ScenarioOnly && j.Type == CardType.Personality && j.Name == "Yasuki Jinn-Kuen" && j.Clan == "Crab");
        Eq(j.Force, 2); Eq(j.Chi, 4); Eq(j.GoldCost, 7); Eq(j.PersonalHonor, 1); Check(j.HonorRequirement is null);
        foreach (var k in new[] { "Merchant", "Experienced 2", "Unique", "Loyal", "Courtier" }) Check(j.Keywords!.Contains(k), k);
        Check(j.Abilities!.SequenceEqual(new[] { "jinn-kuen-straighten", "jinn-kuen-negate" }));
        var m = CardCatalog.Get(Mochitoko);
        Check(!m.ScenarioOnly && m.Type == CardType.Personality && m.Name == "Hida Mochitoko" && m.Clan == "Crab");
        Eq(m.Force, 6); Eq(m.Chi, 3); Eq(m.GoldCost, 9); Eq(m.PersonalHonor, 2); Eq(m.HonorRequirement, 0);
        Check(m.Abilities!.SequenceEqual(new[] { "mochitoko-negate" }));
        var b = CardCatalog.Get(Blade);
        Check(!b.ScenarioOnly && b.Type == CardType.Item && b.Name == "Blade of Champions");
        Eq(b.Force, 3); Eq(b.Chi, 1); Eq(b.GoldCost, 5); Eq(b.Focus, 3); Check(b.Keywords!.Contains("Weapon") && b.Keywords.Contains("Unique"));
        Eq(b.Text, "Reaction: When a Battle action would target a card in this unit, bow this card: The action targets another of your cards instead, if legal.");
        Check(b.Abilities!.SequenceEqual(new[] { "blade-of-champions-redirect" }));
        foreach (var id in new[] { Ezuno, Yurishi, JinnKuen, Mochitoko, Blade })
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        // One shared trigger; each printed ability is its own entry, with the printed limit, cost and timing.
        foreach (var id in new[] { "ezuno-redirect", "blade-of-champions-redirect", "yurishi-disappear", "jinn-kuen-negate", "mochitoko-negate" })
        { Eq(AbilityCatalog.All[id].Timing, "Reaction", id); Eq(AbilityCatalog.All[id].Trigger, ReactionTriggers.TargetedByAction, id); }
        Eq(AbilityCatalog.All["jinn-kuen-negate"].Cost, GoldCostRule.Fixed(2)); Eq(AbilityCatalog.All["mochitoko-negate"].UsesPerTurn, 2);
        Eq(AbilityCatalog.All["jinn-kuen-straighten"].Timing, "Open"); Eq(AbilityCatalog.All["jinn-kuen-straighten"].Target, "holding-or-stronghold");
        Eq(AbilityCatalog.All["blade-of-champions-redirect"].Target, "redirect-target-own-card");
    }

    private static void UseLimitParity()
    {
        // The exemptions ValidateAbility used to spell out as an id list (issue #335 moved them onto the definitions).
        var formerlyExempt = new[] { "tactical-advantage", "equip-battle", "training-ashigaru-boost", "ashigaru-elite-boost", "elite-sentry-straighten", "kharmic-discard",
            "neiru-equip", "naval-invasion", "ring-of-air-straighten", "ring-of-water-move-home",
            "wl-searing-siege-attack" /* "Repeatable" -- issue #674 */ };
        foreach (var (id, ability) in AbilityCatalog.All)
            Eq(ability.UsesPerTurn, id is "wl-pc-iaimiko-force" or "wl-pc-iaimiko-cavalry" or "wl-pc-iaimiko-conqueror" /* "three times per turn" -- issue #674 */ ? 3 : formerlyExempt.Contains(id) || id == "kaname-ranged-attack" || id == "wlpa-shusaku-boost" /* "Repeatable" -- issue #674 */ || id == "castle-of-order-redirect" /* once per BATTLE, its own stamp (issue #453) */
                || id == "shihiro-boost" /* "each time" -- issue #458 */ || id == "cursed-relic-attack" /* "Any number of times per turn" -- issue #467 batch 5 */ || id is "tsukimi-home" or "tsukimi-battlefield" /* "Twice per battle", its own stamp pair -- issue #477 batch 5 */
                || id == "otomo-favor-token" /* Kyuden Otomo - exp's triggered trait is not limited to once per turn -- issue #486 batch 1 */ || id == "kae-favor-negate" /* Kakita Kae's trait is repeatable -- issue #501 */ || id is "shadows-lair-move" or "spiders-web-attach" /* batch 3: once per battle (own stamp) / a trait -- issue #486 */
                || id is "p5-foundries-straighten" or "p5-foundries-attack" or "p5-foundries-draw" /* The New Foundries: "any number of times per turn" -- issue #503 */ || id is "p5-nes-equip" or "p5-nes-equip-open" or "p5-nes-equip-battle" or "p5-seek-stain-equip-limited" or "p5-seek-stain-equip-battle" /* the Equip rulebook action from a discard pile, "any number of times per turn" -- issue #503 */ || id == "wl-recruitment-station-equip" /* no printed limit: the bow is its cost -- issue #674 */ || id is "w739-hidden-battle" or "w739-hidden-limited" or "w739-tachikaze-battle" or "w739-tachikaze-limited" /* "any number of times per turn", the Equip from under a card -- issue #739 */ || id == "w835-heavens-pay" /* one payment per owed card, however many -- issue #835 */ ? AbilityUseLimit.Unlimited : id == "mochitoko-negate" ? 2 : id is "rika-bushido-virtue-bonus" or "tsuruchi-inexp-arrow-attack" /* "three times per turn" -- issue #672, P23 */ ? 3
                : id == "tsukimi-refresh" || id == "w821-governor-create" /* "Twice per turn" -- issue #821 */ || id == "tomaru-attack" /* "Twice per turn" -- issue #460 batch 2 */ || id == "tomaru-attack-3" /* "Twice per turn" -- issue #477 batch 3 */
                    || id == "sleepless-boost" /* "Twice per turn" -- issue #467 batch 2 */ || id == "promo-fudo-negate" /* "Twice per turn" -- issue #653 */ || id == "wl-border-keep-exp2-refresh" /* "Once or twice" -- issue #674 */ ? 2 : id == "wl-sturdy-boost" /* "Three times per turn" -- issue #674 */ ? 3 : 1, $"the limit of {id}");
        Eq(AbilityUseLimit.Unlimited, 0);
    }

    // ---- Mirumoto Ezuno ------------------------------------------------------------------------------------------------
    // Player 0 controls Ezuno and a Shugenja at the battlefield; player 1 has a Personality there whose Battle action bows an enemy Personality.
    private sealed record EzunoRig(MatchState S, CardInstance Ezuno, CardInstance Shugenja, CardInstance Bower);
    private static EzunoRig EzunoTable()
    {
        var s = Table();
        var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0);
        var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        return new(s, ez, sh, bower);
    }

    private static void EzunoRedirectsAnAction()
    {
        var r = EzunoTable();
        var hidden = Add(r.S, "training-farm", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Shugenja.Id));
        Eq(Window(e), "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 0, "Ezuno's controller holds the only reaction, so the first opportunity");
        Check(!e.State.Cards[r.Shugenja.Id].Bowed && !e.State.Cards[r.Ezuno.Id].Bowed, "the action is held whole: nothing has happened to anyone");
        Eq(e.State.PendingNegatableAbility!.TargetId, r.Shugenja.Id, "it targets the Shugenja for now");
        var offer = Listed(e, 0, "ezuno-redirect").Single();
        Check(offer.Enabled && offer.CardId == r.Ezuno.Id, "offered, enabled, on Ezuno");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the acting player waits and sees no reaction of the other side");
        HiddenIdsAbsent(e.GetView(1), [hidden.Id]);
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: r.Ezuno.Id, AbilityId: "ezuno-redirect")); // not his reaction
        Accept(e, offer.Command!);
        Eq(e.State.PendingNegatableAbility!.TargetId, r.Ezuno.Id, "the held action now targets Ezuno");
        Eq(Window(e), "beforeActionReaction", "the window stays open for whatever the acting player wants to answer with"); Eq(e.State.PriorityPlayerId, 1);
        Check(!Offered(e, 0, "ezuno-redirect"), "and Ezuno is spent");
        Eq(e.State.Cards[r.Ezuno.Id].AbilityUses["ezuno-redirect"], e.State.TurnNumber);
        Check(EventLogged(e, "actionRedirected"), "the redirect is in the public log");
        ClosePassing(e);
        Check(e.State.Cards[r.Ezuno.Id].Bowed, "the action resolved on Ezuno"); Check(!e.State.Cards[r.Shugenja.Id].Bowed, "and left the Shugenja alone");
        Eq(e.State.PriorityPlayerId, 0, "priority passed on as after any action");
    }

    // Whether player 1's action opens Ezuno's window, for a position adjusted by `tweak(state, ezuno, shugenja)`; an Open action from the Action phase when
    // `open`, else the Battle one. `targetEzuno` aims the action at Ezuno himself.
    private static bool OpensEzuno(Action<MatchState, CardInstance, CardInstance> tweak, bool open = false, bool targetEzuno = false, int actor = 1)
    {
        var s = Table(open ? Phase.Action : Phase.Battle, actor);
        var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0);
        var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var actorCard = open ? Add(s, "training-bow-any-source", actor, Zone.Home) : Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        tweak(s, ez, sh);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, actor, open ? "training-bow-any" : Bow, actorCard.Id, (targetEzuno ? ez : sh).Id));
        return Window(e) == "beforeActionReaction" && Offered(e, 0, "ezuno-redirect");
    }

    private static void EzunoOnlyWhenLegal()
    {
        Check(OpensEzuno((_, _, _) => { }), "control: another player's Battle action at his Shugenja");
        Check(OpensEzuno((_, _, _) => { }, open: true), "an Open action reaches him too (control for the location cases below: same battlefield)");
        Check(!OpensEzuno((_, _, _) => { }, targetEzuno: true), "an action at Ezuno himself is not redirected");
        Check(!OpensEzuno((_, _, sh) => sh.DefinitionId = "training-crane-samurai"), "a non-Shugenja is not covered");
        Check(!OpensEzuno((_, ez, _) => ez.Bowed = true), "a bowed Ezuno cannot react");
        Check(!OpensEzuno((s, ez, _) => ez.AbilityUses["ezuno-redirect"] = s.TurnNumber), "spent this turn");
        Check(OpensEzuno((s, ez, _) => ez.AbilityUses["ezuno-redirect"] = s.TurnNumber - 1), "used on an earlier turn: available again");
        Check(!OpensEzuno((_, ez, _) => ez.Zone = Zone.Dead), "not in play: nothing to monitor");
        // Locations (Open actions can reach anywhere): Home is a place of a player and each battlefield is its own.
        Check(OpensEzuno((_, ez, sh) => { ez.Zone = Zone.Home; ez.ProvinceIndex = null; sh.Zone = Zone.Home; sh.ProvinceIndex = null; }, open: true), "both at their controller's Home: one location");
        Check(!OpensEzuno((_, ez, _) => { ez.Zone = Zone.Home; ez.ProvinceIndex = null; }, open: true), "Ezuno at Home, the Shugenja at the battlefield");
        Check(!OpensEzuno((_, _, sh) => { sh.Zone = Zone.Home; sh.ProvinceIndex = null; }, open: true), "Ezuno at the battlefield, the Shugenja at Home");
        Check(!OpensEzuno((_, _, sh) => sh.ProvinceIndex = 1, open: true), "two different battlefields");
        // "Another player's action": his own controller's action never opens it (player 0 acts on his own Shugenja).
        Check(!OpensEzuno((_, _, _) => { }, open: true, actor: 0), "his own controller's action");
        // Mendon's ruling: legal means the ORIGINAL action would be accepted against Ezuno. Kazan's action needs a dishonorable target.
        foreach (var ezunoDishonorable in new[] { false, true })
        {
            var s = Table();
            var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); ez.Dishonorable = ezunoDishonorable;
            var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); sh.Dishonorable = true;
            var kazan = Add(s, Kazan, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "kazan-shame", kazan.Id, sh.Id));
            Eq(Window(e) == "beforeActionReaction", ezunoDishonorable, ezunoDishonorable ? "a dishonorable Ezuno is a legal target of an action that needs one: offered" : "an honorable Ezuno is not: no window, the action resolves on the Shugenja");
            if (!ezunoDishonorable) Check(e.State.Cards[sh.Id].Bowed && !e.State.Cards[ez.Id].Bowed, "the action resolved on the Shugenja");
            else
            {
                Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); ClosePassing(e);
                Check(e.State.Cards[ez.Id].Bowed && !e.State.Cards[sh.Id].Bowed, "and resolved on Ezuno");
            }
        }
    }

    private static void EzunoRedirectsAnAttack()
    {
        {
            // Ranged 5 reaches Ezuno (Force 4): the window opens for him, the redirect lands the Attack on him, and the Shugenja survives.
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            Add(s, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
            var e = MatchEngine.FromTestState(s);
            var honor = e.State.Players[1].Honor;
            Accept(e, SiegeAt(e, castle, stronghold, sh.Id));
            Eq(Window(e), "attackReaction"); Eq(e.State.PendingAttack!.TargetId, sh.Id); Eq(e.State.PriorityPlayerId, 0);
            Check(e.State.Cards[castle.Id].Bowed, "the Attack's cost was paid at declaration");
            var offer = Listed(e, 0, "ezuno-redirect").Single();
            Check(offer.Enabled, "offered in the attack window");
            Accept(e, offer.Command!);
            Eq(e.State.PendingAttack!.TargetId, ez.Id, "the pending Attack now targets Ezuno"); Eq(e.State.PendingAttack.Strength, 5); Eq(e.State.PendingAttack.Command!.TargetId, ez.Id);
            Check(e.State.Cards[castle.Id].Bowed, "the cost stays paid; nothing is refunded");
            ClosePassing(e);
            Check(e.State.Cards[ez.Id].Zone != Zone.Battlefield, "Force 4 <= 5: Ezuno was destroyed"); Eq(e.State.Cards[sh.Id].Zone, Zone.Battlefield, "the Shugenja lives");
            Eq(e.State.Players[1].Honor, honor + 2, "the Attack resolved as a hit (Carpenter Castle's Honor)");
            Eq(e.State.Cards[ez.Id].AbilityUses["ezuno-redirect"], e.State.TurnNumber);
        }
        {
            // Melee 2 against Ezuno (Force 4): a target above the Attack's strength is still a legal target (core rule), so the window opens;
            // declined, the Attack resolves on the Shugenja; redirected, it lands on Ezuno, who survives (4 > 2) and so does the Shugenja.
            foreach (var redirect in new[] { false, true })
            {
                var s = Table(); var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                var weapon = Add(s, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id);
                var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
                Add(s, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, 1, "training-weapon-attack", weapon.Id, sh.Id));
                Eq(Window(e), "attackReaction"); Check(Listed(e, 0, "ezuno-redirect").Single().Enabled, "Ezuno is a legal target whatever his Force: the redirect is offered");
                if (redirect) { Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); Eq(e.State.PendingAttack!.TargetId, ez.Id); }
                ClosePassing(e);
                if (redirect) { Eq(e.State.Cards[ez.Id].Zone, Zone.Battlefield, "Force 4 > 2: Ezuno survives"); Eq(e.State.Cards[sh.Id].Zone, Zone.Battlefield, "and the Shugenja lives"); }
                else Check(e.State.Cards[sh.Id].Zone != Zone.Battlefield, "the Attack resolved on the Shugenja");
            }
        }
        {
            // Raising Ezuno's Force above the Attack's strength does not make him an illegal target: the window still opens, and a redirected Attack does not destroy him.
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); ez.Modifiers.Add(new("test", "test", "Force", 2));
            var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, SiegeAt(e, castle, stronghold, sh.Id));
            Eq(Window(e), "attackReaction", "Force 6 > Ranged 5: Ezuno is still a legal target, so the redirect is offered");
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); ClosePassing(e);
            Eq(e.State.Cards[ez.Id].Zone, Zone.Battlefield, "Ranged 5 cannot destroy his Force 6"); Eq(e.State.Cards[sh.Id].Zone, Zone.Battlefield, "and the Shugenja lives");
        }
        {
            // An Attack played as a Strategy from hand: its source is discarded as the window opens, and legality (a target rule) must not depend on it.
            var s = Table(); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
            var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); ez.Modifiers.Add(new("test", "test", "Force", -1)); // Force 3, within Ranged 3
            var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-honor-cost-attack", strategy.Id, sh.Id));
            Eq(Window(e), "attackReaction"); Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is already spent");
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); ClosePassing(e);
            Check(e.State.Cards[ez.Id].Zone != Zone.Battlefield && e.State.Cards[sh.Id].Zone == Zone.Battlefield, "the Ranged 3 Attack went to Ezuno (Force 3) all the same");
        }
    }

    // Player 1 (Attacker) commits Gold to bow a Personality whose price depends on the target: the target unit's total Gold cost minus 4 (minimum 0).
    private static MatchEngine PricedTable(int shugenjaItems, out CardInstance ezuno, out CardInstance shugenja, out CardInstance source)
    {
        var s = Table(Phase.Action);
        ezuno = Add(s, Ezuno, 0, Zone.Home); shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        for (var i = 0; i < shugenjaItems; i++) Add(s, "training-item", 0, Zone.Attached, parent: shugenja.Id);
        source = Add(s, "training-cost-fixture", 1, Zone.Home);
        Add(s, "training-farm", 1, Zone.Home); Add(s, "training-farm", 1, Zone.Home);
        return MatchEngine.FromTestState(s);
    }

    private static void RedirectAndCosts()
    {
        {
            // A dearer original (unit total 3 + 3x2 = 9, price 5) redirected to Ezuno (7, price 3): the committed payment covers it.
            var e = PricedTable(3, out var ez, out var sh, out var source);
            var quote = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == source.Id && a.Command?.AbilityId == "training-disgrace-bow").TargetGold![sh.Id];
            Eq(quote.Cost, 5, "the price of the original target");
            var offered = Offer(e, 1, "training-disgrace-bow", source.Id, sh.Id);
            var producers = e.State.Cards.Values.Where(c => c.OwnerId == 1 && !c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray();
            Accept(e, offered);
            Eq(Window(e), "beforeActionReaction");
            Check(producers.All(id => !e.State.Cards[id].Bowed), "nothing has been paid: the action is held whole, so its costs wait for its resolution");
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect"));
            Check(producers.All(id => !e.State.Cards[id].Bowed), "and the redirect itself changes none of that");
            ClosePassing(e);
            var payments = e.State.Events.Where(x => x.Type == "payment").ToArray();
            Eq(payments.Length, 1, "the action's cost is paid exactly once"); Check(payments[0].Message.Contains("paid 3 Gold"), "for the card it now targets: " + payments[0].Message);
            Check(e.State.Cards[ez.Id].Bowed && !e.State.Cards[sh.Id].Bowed, "and Ezuno is the one bowed");
            Check(offered.SourceIds!.All(id => e.State.Cards[id].Bowed), "exactly the sources the acting player committed were bowed");
        }
        {
            // A cheaper original (unit total 3, price 0, no sources committed) cannot be redirected to Ezuno (price 3): the committed payment no longer fits.
            var e = PricedTable(0, out var ez, out var sh, out var source);
            Accept(e, Offer(e, 1, "training-disgrace-bow", source.Id, sh.Id));
            Check(Window(e) is null, "not legal for Ezuno: no window; the action resolves as declared");
            Check(e.State.Cards[sh.Id].Bowed && !e.State.Cards[ez.Id].Bowed);
        }
    }

    private static void ReactionsFollowTheNewTarget()
    {
        {
            // A Champion of Thunder in the Shugenja's unit answers an action at the Shugenja -- and stops once the action targets Ezuno.
            var r = EzunoTable(); var champion = Add(r.S, Champion, 0, Zone.Attached, province: 0, parent: r.Shugenja.Id);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Shugenja.Id));
            Check(Listed(e, 0, "champion-of-thunder-negate").Any(a => a.Enabled), "before the redirect, the target's own unit reacts");
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect"));
            Accept(e, new GameCommand("declineReaction", 1, e.Version));
            Check(!Listed(e, 0, "champion-of-thunder-negate").Any(), "after it the action targets Ezuno, so a reaction to the Shugenja's unit no longer applies");
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: champion.Id, TargetId: r.Shugenja.Id, AbilityId: "champion-of-thunder-negate"));
        }
        {
            // A Champion in EZUNO's unit is not offered while the Shugenja is the target, and is once the action targets Ezuno.
            var r = EzunoTable(); var champion = Add(r.S, Champion, 0, Zone.Attached, province: 0, parent: r.Ezuno.Id);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Shugenja.Id));
            Check(!Listed(e, 0, "champion-of-thunder-negate").Any(), "the action does not target his unit yet");
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect"));
            Accept(e, new GameCommand("declineReaction", 1, e.Version));
            Check(Listed(e, 0, "champion-of-thunder-negate").Any(a => a.Enabled), "now it does, and the reaction fires on the NEW target");
            Accept(e, ReactionCommand(e, 0, "champion-of-thunder-negate", r.Ezuno.Id));
            ClosePassing(e);
            Check(!e.State.Cards[r.Ezuno.Id].Bowed && !e.State.Cards[r.Shugenja.Id].Bowed, "Champion negated the action's effects on Ezuno, and the Shugenja was never touched");
            Check(champion.Id.Length > 0);
        }
        {
            // What was played against the OLD target stays with it: a negation on the Shugenja does not shield Ezuno.
            var r = EzunoTable(); Add(r.S, Champion, 0, Zone.Attached, province: 0, parent: r.Shugenja.Id);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Shugenja.Id));
            Accept(e, ReactionCommand(e, 0, "champion-of-thunder-negate", r.Shugenja.Id));
            Accept(e, new GameCommand("declineReaction", 1, e.Version));
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect"));
            ClosePassing(e);
            Check(e.State.Cards[r.Ezuno.Id].Bowed, "the negation was for the Shugenja; the action resolves in full on Ezuno");
            Check(e.State.NegatedEffects.Count == 0, "and no negation outlives the action");
        }
    }

    // ---- Blade of Champions --------------------------------------------------------------------------------------------
    // Player 0: a wielder with the Blade, two other Personalities at the battlefield (Q, R) and one at Home. Player 1: a Battle bower and another Personality.
    private sealed record BladeRig(MatchState S, CardInstance Wielder, CardInstance BladeCard, CardInstance Q, CardInstance R, CardInstance AtHome, CardInstance Bower, CardInstance Enemy);
    private static BladeRig BladeTable()
    {
        var s = Table();
        var p = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var blade = Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
        var q = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var r = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return new(s, p, blade, q, r, home, bower, enemy);
    }
    private static string[] BladeTargets(MatchEngine e) => Listed(e, 0, "blade-of-champions-redirect").Select(a => a.Command!.TargetId!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    private static string[] Sorted(params string[] ids) => ids.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static void BladeRedirects()
    {
        var r = BladeTable();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Wielder.Id));
        Eq(Window(e), "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 0);
        var offers = Listed(e, 0, "blade-of-champions-redirect");
        Check(offers.All(o => o.Enabled && o.CardId == r.BladeCard.Id), "each option is the Blade's, enabled");
        Check(BladeTargets(e).SequenceEqual(Sorted(r.Q.Id, r.R.Id)), "exactly the controller's other Personalities the bow could legally target: got " + string.Join(",", BladeTargets(e)));
        Check(offers.All(o => o.Label.Contains(CardCatalog.Get(e.State.Cards[o.Command!.TargetId!].DefinitionId).Name)), "each option names its card");
        Check(!e.State.Cards[r.BladeCard.Id].Bowed && !e.State.Cards[r.Wielder.Id].Bowed, "nothing has happened yet");
        // Illegal choices: the original target, the Blade, an enemy card, a card the action cannot reach (at Home), a nonsense id.
        foreach (var bad in new[] { r.Wielder.Id, r.BladeCard.Id, r.Enemy.Id, r.Bower.Id, r.AtHome.Id, "not-a-card" })
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: r.BladeCard.Id, TargetId: bad, AbilityId: "blade-of-champions-redirect"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: r.BladeCard.Id, AbilityId: "blade-of-champions-redirect")); // no new target chosen
        Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", r.R.Id));
        Check(e.State.Cards[r.BladeCard.Id].Bowed, "the Blade is bowed as the cost"); Eq(e.State.PendingNegatableAbility!.TargetId, r.R.Id);
        Check(!Offered(e, 0, "blade-of-champions-redirect"), "and cannot react again");
        Eq(e.State.Cards[r.BladeCard.Id].AbilityUses["blade-of-champions-redirect"], e.State.TurnNumber);
        ClosePassing(e);
        Check(e.State.Cards[r.R.Id].Bowed, "the action resolved on the chosen card"); Check(!e.State.Cards[r.Wielder.Id].Bowed && !e.State.Cards[r.Q.Id].Bowed, "and on nobody else");
        Check(EventLogged(e, "actionRedirected"));
        {
            // "Another of YOUR cards" even when the action would accept anyone's: Battle straightening targets any Personality, so the enemy's Personalities
            // (and the acting card itself) are legal targets of the action -- and still never candidates.
            var w = BladeTable(); var straightener = Add(w.S, "training-battle-straightener", 1, Zone.Battlefield, province: 0);
            var f = MatchEngine.FromTestState(w.S);
            Accept(f, Offer(f, 1, "training-straighten-at-battle", straightener.Id, w.Wielder.Id));
            Eq(Window(f), "beforeActionReaction");
            Check(BladeTargets(f).SequenceEqual(Sorted(w.Q.Id, w.R.Id)), "only the controller's own cards: " + string.Join(",", BladeTargets(f)));
            foreach (var enemy in new[] { w.Enemy.Id, w.Bower.Id, straightener.Id })
                Reject(f, new GameCommand("useAbility", 0, f.Version, CardId: w.BladeCard.Id, TargetId: enemy, AbilityId: "blade-of-champions-redirect"));
        }
    }

    private static void BladeMayRedirectToItself()
    {
        {
            // Mendon: the Blade may be the new target when it satisfies the targeting requirement. Bow-any-enemy-card accepts an Item.
            var r = BladeTable(); var follower = Add(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Wielder.Id); var cardBower = Add(r.S, "training-card-bower", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, BowCard, cardBower.Id, follower.Id));
            Check(BladeTargets(e).Contains(r.BladeCard.Id), "the Blade is a candidate for a redirect it satisfies");
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", r.BladeCard.Id)); ClosePassing(e);
            Check(e.State.Cards[r.BladeCard.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "the action landed on the Blade");
        }
        {
            // Judged after the cost: Hida Mimori destroys a BOWED enemy card. The unbowed Blade only qualifies because bowing it is the cost of the redirect.
            var s = Table(); var p = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var blade = Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: p.Id, bowed: true);
            var q = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); var awake = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            var mimori = Add(s, "oracle-l5r-3150", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "mimori-destroy", mimori.Id, follower.Id));
            Check(BladeTargets(e).SequenceEqual(Sorted(blade.Id, q.Id)), "the unbowed Blade counts as bowed once it pays; the unbowed Shugenja does not: " + string.Join(",", BladeTargets(e)));
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", blade.Id)); ClosePassing(e);
            Check(e.State.Cards[blade.Id].Zone != Zone.Attached && e.State.Cards[follower.Id].Zone == Zone.Attached && e.State.Cards[awake.Id].Zone == Zone.Battlefield, "the Blade itself was destroyed");
        }
    }

    private static void BladeOnlyWhenApplicable()
    {
        {
            // An Open action is not a Battle action.
            var s = Table(Phase.Action); var p = Add(s, "training-lion-samurai", 0, Zone.Home); Add(s, Blade, 0, Zone.Attached, parent: p.Id);
            Add(s, "training-crane-samurai", 0, Zone.Home); var source = Add(s, "training-bow-any-source", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-bow-any", source.Id, p.Id));
            Check(Window(e) is null && e.State.Cards[p.Id].Bowed, "an Open action resolves at once");
        }
        {
            // A card outside the Blade's unit.
            var r = BladeTable(); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Q.Id));
            Check(Window(e) is null && e.State.Cards[r.Q.Id].Bowed, "another Personality's unit is not this unit");
        }
        {
            // A bowed Blade cannot be used.
            var r = BladeTable(); r.BladeCard.Bowed = true; var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Wielder.Id));
            Check(Window(e) is null && e.State.Cards[r.Wielder.Id].Bowed);
        }
        {
            // No other card of the controller's could legally be targeted: nothing to redirect to.
            var s = Table(); var p = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, p.Id));
            Check(Window(e) is null && e.State.Cards[p.Id].Bowed, "no legal new target, no reaction, the action resolves");
        }
        {
            // A Follower or an Item in the unit is a card in the unit, and the wielder is then a legal new target too.
            var r = BladeTable(); var follower = Add(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Wielder.Id);
            var item = Add(r.S, "training-item", 0, Zone.Attached, province: 0, parent: r.Wielder.Id);
            var cardBower = Add(r.S, "training-card-bower", 1, Zone.Battlefield, province: 0);
            foreach (var target in new[] { follower, item })
            {
                var e = MatchEngine.FromTestState(r.S);
                Accept(e, Offer(e, 1, BowCard, cardBower.Id, target.Id));
                Eq(Window(e), "beforeActionReaction", "the " + CardCatalog.Get(target.DefinitionId).Type + " is a card in the Blade's unit");
                var sibling = target == follower ? item : follower;
                Check(BladeTargets(e).SequenceEqual(Sorted(r.Wielder.Id, r.Q.Id, r.R.Id, sibling.Id, r.BladeCard.Id)), "candidates: the wielder, the other Personalities, the unit's other attachment and the Blade itself (any enemy card is a legal bow target): " + string.Join(",", BladeTargets(e)));
                Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", r.Wielder.Id)); ClosePassing(e);
                Check(e.State.Cards[r.Wielder.Id].Bowed && !e.State.Cards[target.Id].Bowed, "redirected to the wielder");
            }
        }
    }

    private static void BladeRedirectsAnAttack()
    {
        (MatchEngine, MatchState, CardInstance, CardInstance, CardInstance, CardInstance, CardInstance, CardInstance) Rig(int destrierForce = 0)
        {
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var p = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); // Force 1 + the Blade's 3
            var blade = Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
            var q = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var destrier = Add(s, Destrier, 0, Zone.Attached, province: 0, parent: q.Id); destrier.Modifiers.Add(new("test", "test", "Force", destrierForce));
            var rr = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
            _ = blade; _ = castle;
            var e = MatchEngine.FromTestState(s);
            return (e, s, castle, p, blade, destrier, rr, q);
        }
        {
            var (e, s, castle, p, blade, destrier, rr, q) = Rig();
            Accept(e, SiegeAt(e, castle, s.Players[1].StrongholdId, p.Id));
            Eq(Window(e), "attackReaction");
            // Q has a Follower (immune to a direct Attack), the Blade is an Item: only the Follower and the bare Personality can be attacked.
            Check(BladeTargets(e).SequenceEqual(Sorted(destrier.Id, rr.Id)), "candidates: " + string.Join(",", BladeTargets(e)));
            Eq(e.State.PendingAttack!.Strength, 5);
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", destrier.Id));
            Eq(e.State.PendingAttack!.TargetId, destrier.Id); Eq(e.State.PendingAttack.Strength, 2, "the Destrier's -3 follows the Attack to its new target");
            Check(e.State.Cards[blade.Id].Bowed && e.State.Cards[castle.Id].Bowed, "the Blade was bowed and the Attack's own cost stays paid");
            ClosePassing(e);
            Check(e.State.Cards[destrier.Id].Zone != Zone.Attached, "Force 2 <= 2: the Destrier is destroyed"); Eq(e.State.Cards[p.Id].Zone, Zone.Battlefield, "the original target lives");
            Check(e.State.Cards[q.Id].Zone == Zone.Battlefield);
        }
        {
            // A Destrier of Force 3 exceeds the Attack's strength once its -3 applies (5 - 3 = 2 < 3), but is still a legal target: it is a candidate, and survives.
            var (e, s, castle, p, _, destrier, rr, _) = Rig(destrierForce: 1);
            Accept(e, SiegeAt(e, castle, s.Players[1].StrongholdId, p.Id));
            Check(BladeTargets(e).SequenceEqual(Sorted(destrier.Id, rr.Id)), "candidates: " + string.Join(",", BladeTargets(e)));
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", destrier.Id));
            Eq(e.State.PendingAttack!.Strength, 2);
            ClosePassing(e);
            Eq(e.State.Cards[destrier.Id].Zone, Zone.Attached, "Force 3 > 2: the Destrier survives"); Eq(e.State.Cards[p.Id].Zone, Zone.Battlefield, "the original target lives");
        }
    }

    // ---- Goju Yurishi --------------------------------------------------------------------------------------------------
    private static void YurishiNegatesWhenNoLongerLegal()
    {
        // Kitsu Kouki's action bows Kouki (the cost) and gives the target -3F; a Bow action bows it. Both need a target AT the battlefield.
        foreach (var (actionCard, ability) in new[] { ("training-battle-bower", Bow), (Kouki, "kouki-force") })
        {
            var s = Table(); var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0); var attackerCard = Add(s, actionCard, 1, Zone.Battlefield, province: 0);
            Add(s, "training-farm", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var force = e.GetView(0).Players[0].Army.Single(c => c.Id == y.Id).Force;
            Accept(e, Offer(e, 1, ability, attackerCard.Id, y.Id));
            Eq(Window(e), "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 0);
            var offer = Listed(e, 0, "yurishi-disappear").Single(); Check(offer.Enabled && offer.CardId == y.Id);
            Accept(e, offer.Command!);
            Eq(e.State.Cards[y.Id].Zone, Zone.Home, "he disappears into the shadows: home"); Check(!e.State.Cards[y.Id].Bowed, "and unbowed (he moved, he was not bowed by it)");
            Check(e.State.NegatedEffects.Any(n => n.CardId == y.Id && n.Kind == "all"), "he is no longer a legal target (the action needs him at the battlefield): the action's effects on him are negated");
            Check(EventLogged(e, "effectsNegated") && EventLogged(e, "unitMovedHome"), "both are in the log");
            ClosePassing(e);
            Check(!e.State.Cards[y.Id].Bowed, "the bow was negated");
            Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == y.Id).Force, force, "and so was the Force penalty");
            if (ability == "kouki-force") Check(e.State.Cards[attackerCard.Id].Bowed, "the acting card's own cost still stands");
            Eq(e.State.Cards[y.Id].AbilityUses["yurishi-disappear"], e.State.TurnNumber);
        }
    }

    private static void YurishiNegationStopsHonorLoss()
    {
        // Mendon: an Honor loss the action inflicts on the target's controller is an effect, negated with the rest; without the reaction it is lost.
        foreach (var reacts in new[] { false, true })
        {
            var s = Table(); var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0); var striker = Add(s, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = e.State.Players[0].Honor; var actorHonor = e.State.Players[1].Honor;
            Accept(e, Offer(e, 1, "training-honor-strike", striker.Id, y.Id));
            if (reacts) Accept(e, ReactionCommand(e, 0, "yurishi-disappear"));
            ClosePassing(e);
            Eq(e.State.Players[0].Honor, reacts ? honor : honor - 2, reacts ? "the Honor loss was negated with the effects on him" : "control: his controller loses 2 Honor");
            Eq(e.State.Players[1].Honor, actorHonor, "the acting player's own Honor is never touched by the negation");
        }
    }

    private static void YurishiRecheckStillLegal()
    {
        {
            // The Earth Flows: the Defender moves a Personality to the current battlefield -- no need for him to be there. Player 0 is the Attacker here.
            var s = Table(actor: 0); s.PriorityPlayerId = 1; s.UnresolvedBattlefields = [0, 1];
            var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 1); Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            var shugenja = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0); var spell = Add(s, EarthFlows, 1, Zone.Attached, province: 0, parent: shugenja.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "earth-flows-move", spell.Id, y.Id));
            Eq(Window(e), "beforeActionReaction");
            Accept(e, ReactionCommand(e, 0, "yurishi-disappear"));
            Eq(e.State.Cards[y.Id].Zone, Zone.Home, "he moved home");
            Check(!e.State.NegatedEffects.Any(), "the action does not need him at the battlefield: still a legal target, nothing negated");
            ClosePassing(e);
            Eq(e.State.Cards[y.Id].Zone, Zone.Battlefield, "so the action resolves on him: moved to the current battlefield"); Eq(e.State.Cards[y.Id].ProvinceIndex, 0);
        }
        // Not for an Open action, not for his own controller's action, not from Home, not while bowed.
        foreach (var (name, setup) in new (string, Func<MatchEngine>)[]
        {
            ("Yurishi at Home (The Earth Flows may target him there)", () =>
            {
                var s = Table(actor: 0); s.PriorityPlayerId = 1; var y = Add(s, Yurishi, 0, Zone.Home); Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
                var shugenja = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0); var spell = Add(s, EarthFlows, 1, Zone.Attached, province: 0, parent: shugenja.Id);
                var e = MatchEngine.FromTestState(s); Accept(e, Offer(e, 1, "earth-flows-move", spell.Id, y.Id)); return e;
            }),
            ("an Open action", () => { var s = Table(Phase.Action); var y = Add(s, Yurishi, 0, Zone.Home); var src = Add(s, "training-bow-any-source", 1, Zone.Home); var e = MatchEngine.FromTestState(s); Accept(e, Offer(e, 1, "training-bow-any", src.Id, y.Id)); return e; }),
            ("his own controller's Battle action", () => { var s = Table(actor: 0); var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0); var src = Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(s); Accept(e, Offer(e, 0, "training-straighten-at-battle", src.Id, y.Id)); return e; }),
            ("a bowed Yurishi", () => { var s = Table(); var y = Add(s, Yurishi, 0, Zone.Battlefield, bowed: true, province: 0); var src = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(s); Accept(e, Offer(e, 1, Bow, src.Id, y.Id)); return e; }),
        })
        {
            var e = setup();
            Check(Window(e) is null && !Offered(e, 0, "yurishi-disappear"), name + " never opens his window");
        }
    }

    private static void YurishiEscapesAnAttack()
    {
        {
            // Control: with nobody reacting, Ranged 5 destroys Yurishi (Force 4).
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, SiegeAt(e, castle, stronghold, y.Id)); Eq(Window(e), "attackReaction");
            ClosePassing(e);
            Check(e.State.Cards[y.Id].Zone != Zone.Battlefield && e.State.Cards[y.Id].Zone != Zone.Home, "control: destroyed when he does not react");
        }
        {
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0);
            Add(s, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
            var e = MatchEngine.FromTestState(s);
            var honor = e.State.Players[1].Honor;
            Accept(e, SiegeAt(e, castle, stronghold, y.Id));
            Accept(e, ReactionCommand(e, 0, "yurishi-disappear"));
            Eq(e.State.Cards[y.Id].Zone, Zone.Home); Eq(e.State.PendingAttack!.TargetId, y.Id, "the Attack still names him");
            ClosePassing(e);
            Eq(e.State.Cards[y.Id].Zone, Zone.Home, "and misses: he is at home, no longer a legal target");
            Check(e.State.Cards[castle.Id].Bowed, "the Attack's cost stands"); Check(e.State.PendingAttack is null && e.State.NegatedEffects.Count == 0);
            Eq(e.State.Players[1].Honor, honor + 2, "he dodged, but the Attack targeted a card: Carpenter Castle's Honor is gained anyway (Mendon)");
        }
        {
            // Melee: Yurishi at -2 Force is within Melee 2.
            var s = Table(); var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var weapon = Add(s, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id);
            var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0); y.Modifiers.Add(new("test", "test", "Force", -2));
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-weapon-attack", weapon.Id, y.Id)); Eq(Window(e), "attackReaction");
            Accept(e, ReactionCommand(e, 0, "yurishi-disappear")); ClosePassing(e);
            Eq(e.State.Cards[y.Id].Zone, Zone.Home, "a Melee Attack too");
        }
    }

    private static void YurishiEntryHonorLoss()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Players[0].Clan = "Spider";
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var y = s.Cards[s.Players[0].Provinces[0].CardId!]; y.DefinitionId = Yurishi; y.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[0].Honor;
        var buy = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == y.Id && a.Enabled && a.Command is { ClanDiscount: false, ClanAllegiance: false });
        Accept(e, buy.Command!);
        while (e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Players[0].Honor, honor - 3, "he cost his controller 3 Honor on entering, automatically");
    }

    // ---- Yasuki Jinn-Kuen - exp2 -------------------------------------------------------------------------------------
    private static string[] Producers(MatchState s, int owner) =>
        s.Cards.Values.Where(c => c.OwnerId == owner && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray();
    private static void BowProducers(MatchState s, int owner) { foreach (var id in Producers(s, owner)) s.Cards[id].Bowed = true; }

    private sealed record MerchantRig(MatchState S, CardInstance Jinn, CardInstance Merchant, CardInstance Plain, CardInstance Bower);
    private static MerchantRig MerchantTable()
    {
        var s = Table();
        var jinn = Add(s, JinnKuen, 0, Zone.Battlefield, province: 0);
        var merchant = Add(s, "training-merchant", 0, Zone.Battlefield, province: 0);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        return new(s, jinn, merchant, plain, bower);
    }

    private static void JinnKuenPaysAndNegates()
    {
        {
            var r = MerchantTable(); var e = MatchEngine.FromTestState(r.S);
            var producers = Producers(r.S, 0);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Merchant.Id));
            Eq(Window(e), "beforeActionReaction");
            var offer = Listed(e, 0, "jinn-kuen-negate").Single();
            Check(offer.Enabled && offer.CardId == r.Jinn.Id, "offered on Jinn-Kuen");
            Eq(offer.Gold!.Cost, 2, "the Gold quote is the ability's fixed 2"); Check(offer.Gold.Payments.Length > 0 && offer.Command!.SourceIds is { Length: > 0 }, "with the engine's suggested payment");
            Check(producers.All(id => !e.State.Cards[id].Bowed), "nothing paid yet");
            Reject(e, offer.Command! with { SourceIds = [] }); // an unpaid command
            Accept(e, offer.Command!);
            Check(offer.Command!.SourceIds!.All(id => e.State.Cards[id].Bowed), "the chosen sources were bowed to pay");
            Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("paid 2 Gold for Yasuki Jinn-Kuen")), "paid 2 Gold, in the log");
            Check(e.State.DeferredActionNegated && EventLogged(e, "actionNegated"), "the whole action is negated (ActionNegation.cs), not just its effects on the Merchant");
            Check(!Offered(e, 0, "jinn-kuen-negate"), "once per turn");
            ClosePassing(e);
            Check(!e.State.Cards[r.Merchant.Id].Bowed, "the bow never happened"); Check(e.State.NegatedEffects.Count == 0);
        }
        {
            // Jinn-Kuen is a Merchant himself; a plain Personality is not; and it is the controller's OWN Merchant that counts.
            var r = MerchantTable(); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Jinn.Id));
            Check(Offered(e, 0, "jinn-kuen-negate"), "he is a Merchant himself");
            Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); ClosePassing(e);
            Check(!e.State.Cards[r.Jinn.Id].Bowed);
        }
        {
            var r = MerchantTable(); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Plain.Id));
            Check(Window(e) is null && e.State.Cards[r.Plain.Id].Bowed, "a non-Merchant is not protected");
        }
        {
            // The other player's Merchant, targeted by Jinn-Kuen's controller's action: not his to protect.
            var s = Table(actor: 0); Add(s, JinnKuen, 0, Zone.Battlefield, province: 0);
            var enemyMerchant = Add(s, "training-merchant", 1, Zone.Battlefield, province: 0); var mine = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, Bow, mine.Id, enemyMerchant.Id));
            Check(Window(e) is null && e.State.Cards[enemyMerchant.Id].Bowed, "only a Merchant of Jinn-Kuen's own controller");
        }
        {
            // Cannot pay: no window is opened for a reaction that could not be taken.
            var r = MerchantTable(); BowProducers(r.S, 0); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Merchant.Id));
            Check(Window(e) is null && e.State.Cards[r.Merchant.Id].Bowed, "no Gold, no reaction: the action resolved");
        }
        {
            // Bowed Jinn-Kuen, or his single use spent, never opens it either.
            foreach (var tweak in new Action<MerchantRig>[] { r => r.Jinn.Bowed = true, r => r.Jinn.AbilityUses["jinn-kuen-negate"] = r.S.TurnNumber })
            {
                var r = MerchantTable(); tweak(r); var e = MatchEngine.FromTestState(r.S);
                Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Merchant.Id));
                Check(Window(e) is null && e.State.Cards[r.Merchant.Id].Bowed);
            }
        }
        {
            // A cost of 0 (a reduced price counts, through the same quote): offered to a controller with no Gold to spend at all.
            var s = Table(); var guard = Add(s, "training-merchant-free-guard", 0, Zone.Battlefield, province: 0); var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            BowProducers(s, 0); Check(Producers(s, 0).Length == 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, guard.Id));
            Eq(Window(e), "beforeActionReaction"); var offer = Listed(e, 0, "training-negate-merchant-free").Single();
            Check(offer.Enabled && offer.Gold!.Cost == 0 && offer.Command!.SourceIds is not { Length: > 0 }, "offered enabled at a quoted cost of 0 with no sources");
            Accept(e, offer.Command!); ClosePassing(e);
            Check(!e.State.Cards[guard.Id].Bowed, "and it negated the bow");
        }
        {
            // The printed text says "an action", not "another player's action": Jinn-Kuen's controller's own action targeting his own Merchant qualifies.
            var s = Table(Phase.Action, 0); Add(s, JinnKuen, 0, Zone.Home); var merchant = Add(s, "training-merchant", 0, Zone.Home); var source = Add(s, "training-bow-any-source", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-bow-any", source.Id, merchant.Id));
            Check(Offered(e, 0, "jinn-kuen-negate"), "any action targeting his controller's Merchant, his own included");
        }
    }

    private static void JinnKuenBlanketNegation()
    {
        // Each case: the acting player's action against the Merchant, the reaction paid, then what the action would have done to him must not have happened.
        var force = (MatchEngine e, string id) => e.GetView(0).Players[0].Army.Single(c => c.Id == id).Force;
        // The bow itself is the first case (see JinnKuenPaysAndNegates); the rest follow.
        // Force penalty (Kitsu Kouki bows himself as the cost and gives the target -3F).
        {
            var r = MerchantTable(); var kouki = Add(r.S, Kouki, 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "kouki-force", kouki.Id, r.Merchant.Id)); Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); ClosePassing(e);
            Eq(force(e, r.Merchant.Id), 2, "no Force penalty");
        }
        // Movement (Incapacitated, a Strategy played from hand).
        {
            var r = MerchantTable(); var strategy = Add(r.S, Incapacitated, 1, Zone.Hand); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "incapacitated", strategy.Id, r.Merchant.Id)); Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); ClosePassing(e);
            Eq(e.State.Cards[r.Merchant.Id].Zone, Zone.Battlefield, "not moved home"); Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was still spent");
        }
        // Dishonor (Justice of the Crane, an Item).
        foreach (var reacts in new[] { false, true })
        {
            var r = MerchantTable(); var wielder = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var item = Add(r.S, JusticeOfTheCrane, 1, Zone.Attached, province: 0, parent: wielder.Id);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "justice-of-crane-dishonor", item.Id, r.Merchant.Id));
            if (reacts) Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate"));
            ClosePassing(e);
            Eq(e.State.Cards[r.Merchant.Id].Dishonorable, !reacts, reacts ? "not dishonored" : "control: dishonored when nobody reacts");
        }
        // A token placed on the Merchant (an Open action of his controller's own: the printed text says "an action").
        foreach (var reacts in new[] { false, true })
        {
            var s = Table(Phase.Action, 0); Add(s, JinnKuen, 0, Zone.Home); var merchant = Add(s, "training-merchant", 0, Zone.Home); var master = Add(s, "training-token-master", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-create-token", master.Id, merchant.Id));
            if (reacts) Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate"));
            ClosePassing(e);
            Eq(e.State.Cards[merchant.Id].Tokens.Count, reacts ? 0 : 1, reacts ? "no token" : "control: the token is placed");
        }
        // An Honor loss the action inflicts on the target's controller is an EFFECT: negated with the rest (Mendon).
        foreach (var reacts in new[] { false, true })
        {
            var r = MerchantTable(); var striker = Add(r.S, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
            var honor = e.State.Players[0].Honor;
            Accept(e, Offer(e, 1, "training-honor-strike", striker.Id, r.Merchant.Id));
            if (reacts) Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate"));
            ClosePassing(e);
            Eq(e.State.Players[0].Honor, reacts ? honor : honor - 2, reacts ? "the Honor loss was negated with the effects" : "control: 2 Honor lost");
        }
        // Destruction (a Personality without attachments).
        {
            var r = MerchantTable(); var destroyer = Add(r.S, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Destroy, destroyer.Id, r.Merchant.Id)); Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); ClosePassing(e);
            Eq(e.State.Cards[r.Merchant.Id].Zone, Zone.Battlefield, "not destroyed");
        }
        // Control for the four above: without the reaction each of them happens (the same action against the same Merchant, both players passing).
        {
            var r = MerchantTable(); var destroyer = Add(r.S, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Destroy, destroyer.Id, r.Merchant.Id)); ClosePassing(e);
            Check(e.State.Cards[r.Merchant.Id].Zone != Zone.Battlefield, "control: destroyed when nobody reacts");
        }
        // An Attack (Ranged 5 against Force 2): held in the attack window, its destruction negated.
        {
            var r = MerchantTable(); var (castle, stronghold) = Siege(r.S); var e = MatchEngine.FromTestState(r.S);
            Accept(e, SiegeAt(e, castle, stronghold, r.Merchant.Id)); Eq(Window(e), "attackReaction");
            Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); ClosePassing(e);
            Eq(e.State.Cards[r.Merchant.Id].Zone, Zone.Battlefield, "the Attack's destruction was negated"); Check(e.State.NegatedEffects.Count == 0 && e.State.PendingAttack is null);
        }
        // A Follower Merchant's destruction (DestroyAttachment) is negated too: a Follower carrying the keyword, controlled by Jinn-Kuen's controller.
        {
            var r = MerchantTable(); var f = Add(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Plain.Id);
            r.S.Cards[f.Id].GrantedKeywords.Add(new("Merchant", "test", "Permanent"));
            var destroyer = Add(r.S, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, Destroy, destroyer.Id, f.Id)); Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); ClosePassing(e);
            Eq(e.State.Cards[f.Id].Zone, Zone.Attached, "a Merchant Follower survives too");
        }
    }

    private static void JinnKuenStraightens()
    {
        var s = Table(Phase.Action, 0);
        var jinn = Add(s, JinnKuen, 0, Zone.Home);
        var myHolding = Add(s, "training-farm", 0, Zone.Home, bowed: true); var theirHolding = Add(s, "training-farm", 1, Zone.Home, bowed: true);
        var myStronghold = s.Cards[s.Players[0].StrongholdId]; myStronghold.Bowed = true; var theirStronghold = s.Cards[s.Players[1].StrongholdId]; theirStronghold.Bowed = true;
        var personality = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == jinn.Id && a.Command?.AbilityId == "jinn-kuen-straighten");
        Check(option.Enabled, option.Reason ?? "");
        var offered = option.TargetIds!.Select(id => e.State.Cards[id]).ToArray();
        Check(offered.All(c => CardCatalog.Get(c.DefinitionId).Type is CardType.Holding or CardType.Stronghold), "only Holdings and Strongholds: " + string.Join(",", offered.Select(c => c.DefinitionId)));
        Check(new[] { myHolding, theirHolding, myStronghold, theirStronghold }.All(c => option.TargetIds!.Contains(c.Id)), "the Holdings and Strongholds of either side, whatever their bow state or owner");
        Check(!option.TargetIds!.Contains(personality.Id), "no Personality");
        Reject(e, option.Command! with { TargetId = personality.Id });
        foreach (var target in new[] { myHolding, theirHolding, myStronghold, theirStronghold })
        {
            var fresh = MatchEngine.FromTestState(s);
            Accept(fresh, option.Command! with { TargetId = target.Id, ExpectedVersion = fresh.Version });
            Check(!fresh.State.Cards[target.Id].Bowed, "straightened " + target.Id);
        }
    }

    // ---- Daigotsu Bofana's announce-time trait on a redirected action (Mendon: once per action per Bofana) -----------------------------
    private const string BofanaCard = "oracle-l5r-1706";
    private static int Discards(MatchEngine e) => e.State.Events.Count(x => x.Type == "handDiscard");
    private static bool AsksForDiscard(MatchEngine e) => e.State.PendingChoice?.Type == "forcedDiscard";
    private static void AnswerDiscard(MatchEngine e)
    {
        var offer = e.QueryActions(1).First(a => a.Type == "forcedDiscard" && a.Enabled);
        Accept(e, offer.Command!);
    }
    private static void GiveHand(MatchState s, int n = 3) { for (var i = 0; i < n; i++) Add(s, "training-farm", 1, Zone.Hand); }

    private static void BofanaTraitFiresOncePerAction()
    {
        {
            // Fired at announcement (the action targets Bofana), then redirected AWAY by the Blade: no second fire.
            var s = Table(); var bofana = Add(s, BofanaCard, 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: bofana.Id);
            var q = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveHand(s);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, bofana.Id));
            Check(AsksForDiscard(e), "announcing the action at Bofana asks for a discard first"); AnswerDiscard(e);
            Eq(Window(e), "beforeActionReaction"); Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", q.Id)); ClosePassing(e);
            Eq(Discards(e), 1, "redirected away: exactly the one discard"); Check(e.State.Cards[q.Id].Bowed && !e.State.Cards[bofana.Id].Bowed);
        }
        {
            // Redirected ONTO Bofana (not judged at announcement): judged once when the window closes, then the action resolves on him.
            var s = Table(); var p = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
            var bofana = Add(s, BofanaCard, 0, Zone.Battlefield, province: 0); var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveHand(s);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, p.Id));
            Check(!AsksForDiscard(e) && Window(e) == "beforeActionReaction", "no discard for an action that does not target him");
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", bofana.Id)); ClosePassing(e);
            Check(AsksForDiscard(e), "the window closed on an action that now targets Bofana: his trait is judged"); Check(!e.State.Cards[bofana.Id].Bowed, "before the action resolves");
            Eq(e.State.PendingChoice!.PlayerId, 1, "the acting player discards"); AnswerDiscard(e);
            Check(!AsksForDiscard(e) && e.State.Cards[bofana.Id].Bowed && !e.State.Cards[p.Id].Bowed, "then the action resolves on him");
            Eq(Discards(e), 1, "exactly one fire");
        }
        {
            // Two redirects (Ezuno, then the Blade on Ezuno) ending on Bofana: still one fire.
            var s = Table(); var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: ez.Id);
            var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var bofana = Add(s, BofanaCard, 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveHand(s);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, shugenja.Id));
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); Accept(e, new GameCommand("declineReaction", 1, e.Version));
            Eq(e.State.PendingNegatableAbility!.TargetId, ez.Id);
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", bofana.Id)); ClosePassing(e);
            Check(AsksForDiscard(e), "two redirects, the last onto Bofana: judged at the close"); AnswerDiscard(e);
            Eq(Discards(e), 1, "exactly one fire"); Check(e.State.Cards[bofana.Id].Bowed && !e.State.Cards[ez.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed);
        }
        {
            // An Attack redirected onto Bofana (Force lowered to 4: Ranged 5 reaches him) is judged the same way, in the attack window's close.
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var p = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
            var bofana = Add(s, BofanaCard, 0, Zone.Battlefield, province: 0); bofana.Modifiers.Add(new("test", "test", "Force", -3)); GiveHand(s);
            var e = MatchEngine.FromTestState(s);
            Accept(e, SiegeAt(e, castle, stronghold, p.Id)); Eq(Window(e), "attackReaction");
            Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", bofana.Id)); ClosePassing(e);
            Check(AsksForDiscard(e) && e.State.Cards[bofana.Id].Zone == Zone.Battlefield, "judged before the Attack resolves"); AnswerDiscard(e);
            Eq(Discards(e), 1); Check(e.State.Cards[bofana.Id].Zone != Zone.Battlefield && e.State.PendingAttack is null, "and then the Attack destroyed him (Force 4 <= 5)");
        }
    }

    // The once-per-action guard holds by itself, not only through the trait's once-per-phase limit: with the phase limit wiped after the first judgement, the
    // window's close does not judge the same Bofana for the same action again, while the NEXT action (a fresh announcement) does.
    private static void BofanaGuardIsPerAction()
    {
        var s = Table(); var bofana = Add(s, BofanaCard, 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: bofana.Id);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var b1 = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); var b2 = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveHand(s, 5);
        var e = MatchEngine.FromTestState(s);
        void WipePhaseLimit() => e = Edit(e, st => { var c = st.Cards[bofana.Id]; c.Bowed = false; foreach (var k in c.AbilityUses.Keys.Where(k => k.Contains("bofana-targeted-discard")).ToArray()) c.AbilityUses.Remove(k); });
        Accept(e, Offer(e, 1, Bow, b1.Id, bofana.Id));
        Check(AsksForDiscard(e), "announcing the action asks for the discard"); AnswerDiscard(e); Eq(Discards(e), 1);
        Eq(Window(e), "beforeActionReaction", "the Blade holds the action open");
        WipePhaseLimit();
        ClosePassing(e);
        Check(!AsksForDiscard(e), "the window's close does not judge Bofana a second time for the same action"); Eq(Discards(e), 1);
        // The next action is a fresh announcement: judged afresh (the limit is wiped again, so only a stale per-action record could wrongly skip it).
        Accept(e, new GameCommand("pass", 0, e.Version)); WipePhaseLimit();
        Accept(e, Offer(e, 1, Bow, b2.Id, bofana.Id));
        Check(AsksForDiscard(e), "a new action targeting him is judged again");
    }


    private sealed record MochitokoRig(MatchState S, CardInstance Moch, CardInstance Host, CardInstance Follower);
    private static MochitokoRig MochitokoTable(Phase phase = Phase.Battle, int actor = 1)
    {
        var s = Table(phase, actor);
        var moch = Add(s, Mochitoko, 0, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
        var host = Add(s, "training-lion-samurai", 0, moch.Zone, province: moch.ProvinceIndex);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: moch.ProvinceIndex, parent: host.Id);
        return new(s, moch, host, follower);
    }

    private static void MochitokoTwicePerTurn()
    {
        var r = MochitokoTable();
        var bowers = Enumerable.Range(0, 5).Select(_ => Add(r.S, "training-card-bower", 1, Zone.Battlefield, province: 0)).ToArray();
        var e = MatchEngine.FromTestState(r.S);
        var uses = () => e.State.Cards[r.Moch.Id].AbilityUses;
        // Uses one and two: offered, taken, the Follower is neither bowed nor destroyed.
        for (var use = 1; use <= 2; use++)
        {
            Accept(e, Offer(e, 1, BowCard, bowers[use - 1].Id, r.Follower.Id));
            Eq(Window(e), "beforeActionReaction", $"use {use}: the window opens"); Check(Listed(e, 0, "mochitoko-negate").Single().Enabled, $"use {use}: offered");
            Accept(e, ReactionCommand(e, 0, "mochitoko-negate"));
            Eq(uses()["mochitoko-negate"], e.State.TurnNumber); Eq(uses()["count:mochitoko-negate"], e.State.TurnNumber * 1024 + use, "the count is what the limit reads");
            Check(e.State.NegatedEffects.Count(n => n.CardId == r.Follower.Id) == 2 && e.State.NegatedEffects.Any(n => n.Kind == "bow") && e.State.NegatedEffects.Any(n => n.Kind == "destroy"), "two kinds negated together");
            Check(!Offered(e, 0, "mochitoko-negate"), $"after use {use} it is not offered again in the same window");
            ClosePassing(e);
            Check(!e.State.Cards[r.Follower.Id].Bowed && e.State.Cards[r.Follower.Id].Zone == Zone.Attached, $"use {use}: the bow was negated");
            Accept(e, new GameCommand("pass", 0, e.Version)); // priority back to the acting player
        }
        // Both uses spent: a third action opens no window and resolves.
        Accept(e, Offer(e, 1, BowCard, bowers[2].Id, r.Follower.Id));
        Check(Window(e) is null && e.State.Cards[r.Follower.Id].Bowed, "twice per turn, and no more: the third bow lands");
        // The next turn resets the count.
        e = Edit(e, s => { s.TurnNumber++; s.Cards[r.Follower.Id].Bowed = false; s.PriorityPlayerId = 1; s.ConsecutivePasses = 0; });
        Accept(e, Offer(e, 1, BowCard, bowers[3].Id, r.Follower.Id));
        Eq(Window(e), "beforeActionReaction", "a new turn: available again");
        Accept(e, ReactionCommand(e, 0, "mochitoko-negate"));
        Eq(e.State.Cards[r.Moch.Id].AbilityUses["count:mochitoko-negate"], e.State.TurnNumber * 1024 + 1, "counting from one again");
    }

    private static void MochitokoKinds()
    {
        // Bow-only, destroy-only: the Follower survives each; the control (nobody reacts) loses it or bows it.
        foreach (var (name, actionCard, ability, cardId) in new[] { ("bow", "training-card-bower", BowCard, ""), ("destroy", "training-no-attachments-destroyer", Destroy, "") })
        {
            foreach (var reacts in new[] { false, true })
            {
                var r = MochitokoTable(); var actor = Add(r.S, actionCard, 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
                Accept(e, Offer(e, 1, ability, actor.Id, r.Follower.Id)); Eq(Window(e), "beforeActionReaction");
                if (reacts) Accept(e, ReactionCommand(e, 0, "mochitoko-negate"));
                ClosePassing(e);
                var f = e.State.Cards[r.Follower.Id];
                if (reacts) Check(!f.Bowed && f.Zone == Zone.Attached, $"{name} negated");
                else Check(name == "bow" ? f.Bowed && f.Zone == Zone.Attached : f.Zone != Zone.Attached, $"{name} control");
            }
            _ = cardId;
        }
        // Both at once: Tokoru's Bear bows a Follower of the ATTACKING army and destroys it as an attachment. Player 0 is the Attacker here.
        foreach (var reacts in new[] { false, true })
        {
            var r = MochitokoTable(actor: 0); r.S.PriorityPlayerId = 1; var bear = Add(r.S, TokoruBear, 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "tokoru-bear-bow", bear.Id, r.Follower.Id)); Eq(Window(e), "beforeActionReaction");
            if (reacts) Accept(e, ReactionCommand(e, 0, "mochitoko-negate"));
            ClosePassing(e);
            var f = e.State.Cards[r.Follower.Id];
            if (reacts) Check(!f.Bowed && f.Zone == Zone.Attached, "bow and destruction both negated: he is neither bowed nor destroyed");
            else Check(f.Zone != Zone.Attached, "control: bowed and destroyed as an attachment");
        }
        // A Melee Attack that would destroy the Follower (Force 2 <= 2).
        foreach (var reacts in new[] { false, true })
        {
            var r = MochitokoTable(); var wielder = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var weapon = Add(r.S, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "training-weapon-attack", weapon.Id, r.Follower.Id)); Eq(Window(e), "attackReaction");
            if (reacts) { Check(Listed(e, 0, "mochitoko-negate").Single().Enabled); Accept(e, ReactionCommand(e, 0, "mochitoko-negate")); }
            ClosePassing(e);
            Eq(e.State.Cards[r.Follower.Id].Zone == Zone.Attached, reacts, reacts ? "the Attack's destruction was negated" : "control: the Attack destroyed the Follower");
            Check(e.State.NegatedEffects.Count == 0, "no negation outlives the Attack");
        }
    }

    private static void MochitokoBlocksAFollowerCost()
    {
        // Hiruma Tensin's "bow your performing Follower" is a COST. Mochitoko may negate that bowing, whoever initiated it (his controller's own action here) -- and then the
        // ENTIRE action is negated and pays nothing: the cost cannot be dodged while the effects are kept.
        foreach (var reacts in new[] { false, true })
        {
            var s = Table(actor: 0);
            var moch = Add(s, Mochitoko, 0, Zone.Battlefield, province: 0); var tensin = Add(s, "oracle-l5r-3332", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: tensin.Id); var enemy = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "tensin-follower-attack", tensin.Id, enemy.Id, follower.Id));
            Eq(Window(e), "beforeActionReaction", "held before its cost is paid: the Follower is not bowed yet");
            Check(!e.State.Cards[follower.Id].Bowed, "nothing paid yet");
            Check(Listed(e, 0, "mochitoko-negate").Single().Enabled && Listed(e, 0, "mochitoko-negate").Single().Command!.TargetId == follower.Id, "offered for the paying Follower");
            if (reacts) Accept(e, ReactionCommand(e, 0, "mochitoko-negate"));
            ClosePassing(e);
            if (reacts)
            {
                Check(!e.State.Cards[follower.Id].Bowed, "the bowing was negated"); Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "and the Attack never happened: the whole action is negated");
                Eq(e.State.Cards[tensin.Id].AbilityUses["tensin-follower-attack"], e.State.TurnNumber, "it was taken (its use is spent)");
                Check(EventLogged(e, "actionNegated") && Window(e) is null && e.State.PendingAttack is null && e.State.NegatedEffects.Count == 0);
            }
            else Check(e.State.Cards[follower.Id].Bowed && e.State.Cards[enemy.Id].Zone != Zone.Battlefield, "control: the Follower paid and the Attack destroyed the Shugenja");
        }
        // An Honor loss paid inside the action is ignored with it when the cost is blocked; without the reaction it is paid.
        foreach (var reacts in new[] { false, true })
        {
            var s = Table(actor: 0);
            Add(s, Mochitoko, 0, Zone.Battlefield, province: 0); var payer = Add(s, "training-follower-payer", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: payer.Id); Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = e.State.Players[0].Honor; var hand = e.State.Players[0].Hand.Count;
            var offer = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.CardId == payer.Id && a.Command?.AbilityId == "training-follower-honor-draw" && a.Command.PerformerId == follower.Id);
            Accept(e, offer.Command!);
            Eq(Window(e), "beforeActionReaction");
            if (reacts) Accept(e, ReactionCommand(e, 0, "mochitoko-negate"));
            ClosePassing(e);
            Eq(e.State.Players[0].Honor, reacts ? honor : honor - 2, reacts ? "the Honor-loss cost was ignored: nothing is paid" : "control: the cost is paid");
            Eq(e.State.Players[0].Hand.Count, reacts ? hand : hand + 1, reacts ? "and no card was drawn" : "control: a card was drawn");
            Eq(e.State.Cards[follower.Id].Bowed, !reacts);
        }
    }

    // ---- Yoritomo Sakuma (#338) beside the redirect and per-card negation reactions ---------------------------------------------------
    private const string SakumaCard = "oracle-l5r-9624", AdiCard = "oracle-l5r-9485";

    private static void SakumaBesideRedirect()
    {
        // Player 1's Yogo Adi (Political Open: "give your provinces +2 strength" unless the target is dishonorable) targets a Shugenja of player 0, who holds Ezuno and
        // Sakuma at Home.
        (MatchEngine E, CardInstance Adi, CardInstance Shugenja, CardInstance Ez, CardInstance Sakuma) Build()
        {
            var s = Table(Phase.Action, 1);
            var ez = Add(s, Ezuno, 0, Zone.Home); var sakuma = Add(s, SakumaCard, 0, Zone.Home); var shugenja = Add(s, "training-shugenja", 0, Zone.Home); var adi = Add(s, AdiCard, 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "yogo-adi-choice", adi.Id, shugenja.Id));
            Eq(Window(e), "beforeActionReaction", "the window opens for player 0");
            return (e, adi, shugenja, ez, sakuma);
        }
        int Strength(MatchEngine e) => e.ProvinceStrengthForTest(1, 0);
        void Answer(MatchEngine e, string choice) =>
            Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == choice && a.Enabled).Command!);

        foreach (var pays in new[] { true, false })
        {
            // Ezuno first: the action now targets Ezuno, and Sakuma (a Personality of his controller's is targeted) still reacts to it.
            var (e, adi, shugenja, ez, sakuma) = Build(); var before = Strength(e);
            Check(Offered(e, 0, "ezuno-redirect") && Offered(e, 0, "sakuma-political-negate"), "both reactions are offered against the original target");
            Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); Eq(e.State.PendingNegatableAbility!.TargetId, ez.Id, "redirected onto Ezuno");
            Accept(e, new GameCommand("declineReaction", 1, e.Version));
            Check(Offered(e, 0, "sakuma-political-negate"), "Sakuma still reacts: the action now targets Ezuno, one of his controller's Personalities");
            Accept(e, ReactionCommand(e, 0, "sakuma-political-negate")); Eq(Window(e), "eitherOr", "the acting player decides");
            Answer(e, pays ? "pay" : "negate");
            Eq(Window(e), "beforeActionReaction", "the window is restored"); Eq(e.State.PendingNegatableAbility?.TargetId, ez.Id, "still held on Ezuno");
            Check(e.State.DeferredActionNegated == !pays);
            ClosePassing(e);
            Eq(Strength(e), pays ? before + 2 : before, pays ? "paid: the action resolved (on Ezuno)" : "not paid: negated as a whole, nothing happened");
            Check(e.State.Cards[adi.Id].AbilityUses.ContainsKey("yogo-adi-choice"), "the action was taken either way");
            Check(!e.State.DeferredActionNegated && e.State.NegatedEffects.Count == 0, "no signal survives the action");
        }
        {
            // Sakuma first, negating: nothing further is offered to Ezuno (an empty redirect), and the action is negated when the window closes.
            var (e, adi, shugenja, ez, sakuma) = Build(); var before = Strength(e);
            Accept(e, ReactionCommand(e, 0, "sakuma-political-negate")); Answer(e, "negate");
            Check(e.State.DeferredActionNegated, "negated as a whole");
            Accept(e, new GameCommand("declineReaction", 1, e.Version)); Eq(e.State.PriorityPlayerId, 0, "priority is Ezuno's controller's");
            Check(!Offered(e, 0, "ezuno-redirect"), "an action already negated is not redirected");
            ClosePassing(e);
            Eq(Strength(e), before, "negated");
        }
        {
            // Sakuma first, paying: the action stands, so Ezuno may still redirect it, and it resolves on Ezuno.
            var (e, adi, shugenja, ez, sakuma) = Build(); var before = Strength(e);
            Accept(e, ReactionCommand(e, 0, "sakuma-political-negate")); Answer(e, "pay");
            Check(!e.State.DeferredActionNegated, "Sakuma was paid: the action stands");
            Accept(e, new GameCommand("declineReaction", 1, e.Version)); Eq(e.State.PriorityPlayerId, 0);
            Check(Offered(e, 0, "ezuno-redirect"), "and can still be redirected"); Accept(e, ReactionCommand(e, 0, "ezuno-redirect"));
            ClosePassing(e);
            Eq(Strength(e), before + 2, "resolved after the redirect");
        }
    }

    private static void CostBlockIsPerVerb()
    {
        // Hiruma Tensin's cost is a BOW. A negated bow (or every effect) on the paying Follower blocks it; a negated destruction alone does not.
        foreach (var (kind, blocked) in new[] { ("bow", true), ("all", true), ("destroy", false), ("move", false) })
        {
            var s = Table(actor: 0);
            Add(s, Mochitoko, 0, Zone.Battlefield, province: 0); var tensin = Add(s, "oracle-l5r-3332", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: tensin.Id); var enemy = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "tensin-follower-attack", tensin.Id, enemy.Id, follower.Id)); Eq(Window(e), "beforeActionReaction");
            e = Edit(e, st => st.NegatedEffects.Add(new(kind, follower.Id)));
            ClosePassing(e);
            Check(e.State.Cards[follower.Id].Bowed != blocked, $"negated {kind}: the bow cost is {(blocked ? "blocked" : "paid")}");
            Check((e.State.Cards[enemy.Id].Zone == Zone.Battlefield) == blocked, $"negated {kind}: the Attack {(blocked ? "never happened" : "went ahead")}");
        }
        // A cost is not an effect: the acting player's own Honor-loss cost is paid even if every effect on one of his own cards has been negated.
        {
            var s = Table(actor: 0);
            Add(s, Mochitoko, 0, Zone.Battlefield, province: 0); var payer = Add(s, "training-follower-payer", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: payer.Id); Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = e.State.Players[0].Honor;
            var offer = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.CardId == payer.Id && a.Command?.AbilityId == "training-follower-honor-draw" && a.Command.PerformerId == follower.Id);
            Accept(e, offer.Command!); Eq(Window(e), "beforeActionReaction");
            e = Edit(e, st => st.NegatedEffects.Add(new("all", payer.Id)));
            ClosePassing(e);
            Eq(e.State.Players[0].Honor, honor - 2, "the Honor cost of his own action is paid: a cost is never negated with a card's effects");
        }
    }

    // ---- Carpenter Castle: "If this targeted a card, gain 2 Honor" (Mendon: a targeting effect, not a hit effect) --------------------------
    // The Attack as the engine offers it (through QueryActions): aimed at a card, or with none.
    private static GameCommand CarpenterOffer(MatchEngine e, CardInstance castle, string stronghold, string? targetId)
    {
        var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == stronghold && a.Command?.AbilityId == "carpenter-castle-siege" && a.Command.PerformerId == castle.Id);
        Check(option.Enabled, option.Reason ?? "Carpenter Castle's Attack was not offered");
        if (targetId is null) { Check(option.NoTarget, "an Attack may always be taken with no target"); return option.Command! with { TargetId = null }; }
        Check(option.TargetIds!.Contains(targetId), "the card is a legal target: " + string.Join(",", option.TargetIds!));
        return option.Command! with { TargetId = targetId };
    }

    private static void CarpenterHonorFollowsTargeting()
    {
        int Honor(MatchEngine e) => e.State.Players[1].Honor;
        // A hit, with nobody able to react (the immediate path): +2, once.
        {
            var s = Table(); var (castle, stronghold) = Siege(s); var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            Accept(e, CarpenterOffer(e, castle, stronghold, enemy.Id));
            Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead); Eq(Honor(e), honor + 2, "hit: +2 Honor, once");
        }
        // The same, held open by an attack reactor and passed: a hit through the window is +2 once as well.
        {
            var s = Table(); var (castle, stronghold) = Siege(s); var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-attack-reactor", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            Accept(e, CarpenterOffer(e, castle, stronghold, enemy.Id)); Eq(Window(e), "attackReaction"); ClosePassing(e);
            Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead); Eq(Honor(e), honor + 2, "hit through the window: +2 Honor, once");
        }
        // Negated (Jinn-Kuen negates the Attack): nothing is destroyed, the Honor is still gained.
        {
            var r = MerchantTable(); var (castle, stronghold) = Siege(r.S); Add(r.S, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
            var e = MatchEngine.FromTestState(r.S); var honor = Honor(e);
            Accept(e, CarpenterOffer(e, castle, stronghold, r.Merchant.Id)); Eq(Window(e), "attackReaction");
            Accept(e, ReactionCommand(e, 0, "jinn-kuen-negate")); Check(e.State.PendingAttack!.Negated, "the Attack is negated"); ClosePassing(e);
            Eq(e.State.Cards[r.Merchant.Id].Zone, Zone.Battlefield, "the negated Attack destroyed nothing"); Eq(Honor(e), honor + 2, "negated: the Honor is still gained (it targeted a card)");
        }
        // Redirected by Ezuno: it targeted a card (the new one); +2 once, and the redirected Attack hits Ezuno.
        {
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var ez = Add(s, Ezuno, 0, Zone.Battlefield, province: 0); var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            Add(s, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
            var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            Accept(e, CarpenterOffer(e, castle, stronghold, sh.Id)); Accept(e, ReactionCommand(e, 0, "ezuno-redirect")); Eq(Honor(e), honor, "nothing is gained before the Attack resolves");
            ClosePassing(e);
            Check(e.State.Cards[ez.Id].Zone != Zone.Battlefield && e.State.Cards[sh.Id].Zone == Zone.Battlefield); Eq(Honor(e), honor + 2, "redirected: +2 Honor, once");
        }
        // Redirected by the Blade (bowed as the cost) onto another of its controller's cards: the Attack targeted a card all the same.
        {
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var p = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var blade = Add(s, Blade, 0, Zone.Attached, province: 0, parent: p.Id);
            var q = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-attack-reactor", 0, Zone.Home); // a further Reaction keeps the window open once the first is played, so the pending Attack can be read (issue #644)
            var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            Accept(e, CarpenterOffer(e, castle, stronghold, p.Id)); Accept(e, ReactionCommand(e, 0, "blade-of-champions-redirect", q.Id));
            Eq(e.State.PendingAttack!.TargetId, q.Id); Check(e.State.Cards[blade.Id].Bowed);
            ClosePassing(e);
            Eq(Honor(e), honor + 2, "redirected by the Blade: +2 Honor, once");
        }
        // Dodged (Yurishi): a miss, but it targeted a card.
        {
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            Accept(e, CarpenterOffer(e, castle, stronghold, y.Id)); Accept(e, ReactionCommand(e, 0, "yurishi-disappear")); ClosePassing(e);
            Eq(e.State.Cards[y.Id].Zone, Zone.Home, "he escaped"); Eq(Honor(e), honor + 2, "dodged: +2 Honor");
        }
        // No target: not a card, no Honor -- taken with every reactor in play, so no window either; and a province is refused for this Attack (it names none).
        {
            var (s, _) = ReactiveDefence(); var (castle, stronghold) = Siege(s); var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: stronghold, PerformerId: castle.Id, AbilityId: "carpenter-castle-siege", Province: 0));
            Accept(e, CarpenterOffer(e, castle, stronghold, null));
            Check(Window(e) is null && e.State.PendingAttack is null); Eq(Honor(e), honor, "no card targeted: no Honor");
        }
        // A province Attack (the fixture) targets no card either. It carries no Honor of its own, so the check is that a targeting-conditional effect never fires for it.
        {
            var s = Table(); var attacker = Add(s, "training-province-attacker", 1, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var honor = Honor(e);
            var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == "training-province-attack");
            Check(option.Provinces!.Contains(0)); Accept(e, option.Command! with { TargetId = null, Province = 0 }); Eq(Honor(e), honor, "a province is not a card: no Honor for a targeting effect");
        }
    }

    private static void MochitokoOnlyWhenApplicable()
    {
        bool Opens(MochitokoRig r, Action<MatchState> tweak, string actorCard, string ability, Func<MochitokoRig, string> target, Phase phase = Phase.Battle, int actor = 1)
        {
            var a = Add(r.S, actorCard, actor, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
            tweak(r.S);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, actor, ability, a.Id, target(r)));
            return Window(e) == "beforeActionReaction" && Offered(e, 0, "mochitoko-negate");
        }
        Check(Opens(MochitokoTable(), _ => { }, "training-card-bower", BowCard, r => r.Follower.Id), "control: an action that bows his Follower at his location");
        Check(!Opens(MochitokoTable(), _ => { }, "training-card-bower", BowCard, r => r.Host.Id), "not a Personality, only a Follower");
        Check(!Opens(MochitokoTable(), s => s.Cards.Values.First(c => c.DefinitionId == Mochitoko).Bowed = true, "training-card-bower", BowCard, r => r.Follower.Id), "a bowed Mochitoko cannot react");
        {
            // Both uses spent this turn -- stamped directly the way the engine stamps them.
            var r = MochitokoTable(); var m = r.S.Cards[r.Moch.Id]; m.AbilityUses["mochitoko-negate"] = r.S.TurnNumber; m.AbilityUses["count:mochitoko-negate"] = r.S.TurnNumber * 1024 + 2;
            Check(!Opens(r, _ => { }, "training-card-bower", BowCard, x => x.Follower.Id), "two uses spent: not offered");
            var once = MochitokoTable(); var m1 = once.S.Cards[once.Moch.Id]; m1.AbilityUses["mochitoko-negate"] = once.S.TurnNumber; m1.AbilityUses["count:mochitoko-negate"] = once.S.TurnNumber * 1024 + 1;
            Check(Opens(once, _ => { }, "training-card-bower", BowCard, x => x.Follower.Id), "one use spent: still offered");
            var stale = MochitokoTable(); var m2 = stale.S.Cards[stale.Moch.Id]; m2.AbilityUses["mochitoko-negate"] = stale.S.TurnNumber - 1; m2.AbilityUses["count:mochitoko-negate"] = (stale.S.TurnNumber - 1) * 1024 + 2;
            Check(Opens(stale, _ => { }, "training-card-bower", BowCard, x => x.Follower.Id), "two uses on an EARLIER turn: available again");
        }
        {
            // A pure Force penalty could neither bow nor destroy him: Spitting Llama's action never offers it.
            var r = MochitokoTable(); var w = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var llama = Add(r.S, Llama, 1, Zone.Attached, province: 0, parent: w.Id);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "spitting-llama-force", llama.Id, r.Follower.Id));
            Check(Window(e) is null, "a plain Force penalty never offers the reaction");
        }
        {
            // An enemy's Follower is not "your Follower": player 0 acts against player 1's Follower, and Mochitoko is player 0's.
            var s = Table(actor: 0); var moch = Add(s, Mochitoko, 0, Zone.Battlefield, province: 0);
            var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
            var bower = Add(s, "training-card-bower", 0, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, BowCard, bower.Id, follower.Id));
            Check(Window(e) is null && e.State.Cards[follower.Id].Bowed, "another player's Follower is not covered");
            Check(moch.Id.Length > 0);
        }
        // Location (Open actions reach anywhere): Follower and Mochitoko at Home together; Mochitoko at Home and the Follower's host at the battlefield.
        Check(Opens(MochitokoTable(Phase.Action), _ => { }, "training-card-bower", "training-bow-enemy-card-open", r => r.Follower.Id, Phase.Action), "both at Home: one location");
        {
            var r = MochitokoTable(Phase.Action);
            Check(!Opens(r, s => { s.Cards[r.Host.Id].Zone = Zone.Battlefield; s.Cards[r.Host.Id].ProvinceIndex = 0; s.Cards[r.Follower.Id].ProvinceIndex = 0; }, "training-card-bower", "training-bow-enemy-card-open", x => x.Follower.Id, Phase.Action), "Mochitoko at Home, the Follower at a battlefield");
        }
    }

    // ---- Attacks that target no card (Mendon): no target, or the province -------------------------------------------------------------------
    // Player 0 defends with everything that reacts to a targeted action or Attack; player 1 attacks. A card-target Attack would open a window here.
    private static (MatchState S, CardInstance Shugenja) ReactiveDefence()
    {
        var s = Table(); Add(s, Ezuno, 0, Zone.Battlefield, province: 0); var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-attack-reactor", 0, Zone.Battlefield, province: 0); Add(s, Yurishi, 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        Add(s, Mochitoko, 0, Zone.Battlefield, province: 0); Add(s, Blade, 0, Zone.Attached, province: 0, parent: sh.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return (s, sh);
    }

    private static void AttacksNeedNoCardTarget()
    {
        {
            // Ranged: offered with no target, alongside the card targets; taken with none, it opens no window although every reactor is in play and pays its cost.
            var (s, sh) = ReactiveDefence(); var (castle, stronghold) = Siege(s);
            var e = MatchEngine.FromTestState(s);
            var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == stronghold && a.Command?.AbilityId == "carpenter-castle-siege" && a.Command.PerformerId == castle.Id);
            Check(option.Enabled && option.NoTarget, "the option says an Attack may be taken with no target"); Check(option.TargetIds!.Contains(sh.Id), "and still lists its card targets");
            Check(option.Provinces is null, "no province target for an Attack that does not name one");
            var honor = e.State.Players[1].Honor;
            Accept(e, option.Command! with { TargetId = null });
            Check(Window(e) is null && e.State.PendingAttack is null && e.State.PendingNegatableAbility is null, "no card is targeted: nothing card-targeted reacts");
            Check(e.State.Cards[castle.Id].Bowed, "the cost is paid"); Eq(e.State.Players[1].Honor, honor, "and it hit nothing (no target, no Honor)");
            Check(e.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Count() > 0);
        }
        {
            // The same Attack aimed at the card DOES open the window (control) -- and a Melee Attack with no target does not.
            var (s, sh) = ReactiveDefence(); var (castle, stronghold) = Siege(s); var e = MatchEngine.FromTestState(s);
            Accept(e, SiegeAt(e, castle, stronghold, sh.Id)); Eq(Window(e), "attackReaction");
        }
        {
            var (s, _) = ReactiveDefence(); var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var weapon = Add(s, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id);
            var e = MatchEngine.FromTestState(s);
            var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == weapon.Id && a.Command?.AbilityId == "training-weapon-attack");
            Check(option.Enabled && option.NoTarget, "Melee too");
            Accept(e, option.Command! with { TargetId = null }); Check(Window(e) is null && e.State.PendingAttack is null);
        }
        {
            // Nothing to shoot at: the only legal command is the one with no target, and it is offered as the option's own command.
            var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == stronghold && a.Command?.AbilityId == "carpenter-castle-siege" && a.Command.PerformerId == castle.Id);
            Check(option.Enabled && option.NoTarget && option.Command!.TargetId is null && (option.TargetIds?.Length ?? 0) == 0, "a legal Attack with no target in sight");
            Accept(e, option.Command!);
        }
        {
            // The province: offered only for an ability that says so, as the current battlefield's province, and it is not a card.
            var (s, sh) = ReactiveDefence(); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var attacker = Add(s, "training-province-attacker", 1, Zone.Battlefield, province: 0); var (castle, stronghold) = Siege(s); _ = sh;
            var e = MatchEngine.FromTestState(s);
            var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == "training-province-attack");
            Check(option.Enabled && option.NoTarget && option.Provinces!.SequenceEqual(new[] { 0 }), "no target, a card, or the province");
            var weak = e.State.Cards.Values.First(c => c.DefinitionId == "training-crane-samurai" && c.Zone == Zone.Battlefield); // not whichever copy the card dictionary happens to list first (issue #611) Check(option.TargetIds!.Contains(weak.Id), "a card target is still offered");
            var strength = e.ProvinceStrengthForTest(0, 0);
            var onProvince = new GameCommand("useAbility", 1, e.Version, CardId: attacker.Id, AbilityId: "training-province-attack", Province: 0);
            Reject(e, onProvince with { TargetId = weak.Id }); Reject(e, onProvince with { Province = 1 });
            var siegeOnProvince = new GameCommand("useAbility", 1, e.Version, CardId: stronghold, PerformerId: castle.Id, AbilityId: "carpenter-castle-siege", Province: 0);
            Reject(e, siegeOnProvince); // an Attack that names no province cannot be aimed at one
            Accept(e, onProvince);
            // A province is a target (issue #1129): the strength reactor at the battlefield may react, so its window opens; nothing card-targeted can (no redirect, no negation), and the Attack waits for it.
            Eq(Window(e), "attackReaction", "a province is a target: the strength reactor present at the battlefield gets its window");
            Check(e.State.PendingAttack is { TargetId: null } && e.State.PendingNegatableAbility is null, "the Attack names no card, so no card-targeted reaction or redirect is held");
            Check(!e.QueryActions(0).Concat(e.QueryActions(1)).Any(a => a.Enabled && a.Command?.AbilityId is { } reactionId && AbilityCatalog.All.TryGetValue(reactionId, out var def) && def.Trigger == ReactionTriggers.TargetedByAction), "no card-targeted reaction is offered against a province");
            for (var guard = 0; guard < 12 && Window(e) is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.ProvinceStrengthForTest(0, 0), Math.Max(0, strength - 3), "the Attack's own effect on the province happened");
            Check(EventLogged(e, "provinceAttacked"));
        }
        {
            // The same Attack with no target at all resolves and does nothing to the province.
            var s = Table(); var attacker = Add(s, "training-province-attacker", 1, Zone.Battlefield, province: 0); Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); var strength = e.ProvinceStrengthForTest(0, 0);
            Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: attacker.Id, AbilityId: "training-province-attack"));
            Eq(e.ProvinceStrengthForTest(0, 0), strength, "no target, no province: nothing happens");
        }
    }

    // ---- every offer validates ----------------------------------------------------------------------------------------
    private static void OfferedCommandsAllValidate()
    {
        var builds = new Func<MatchEngine>[]
        {
            () => { var r = EzunoTable(); var e = MatchEngine.FromTestState(r.S); Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Shugenja.Id)); return e; },
            () => { var r = BladeTable(); var e = MatchEngine.FromTestState(r.S); Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Wielder.Id)); return e; },
            () => { var s = Table(); var y = Add(s, Yurishi, 0, Zone.Battlefield, province: 0); var b = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(s); Accept(e, Offer(e, 1, Bow, b.Id, y.Id)); return e; },
            () => { var r = MerchantTable(); var e = MatchEngine.FromTestState(r.S); Accept(e, Offer(e, 1, Bow, r.Bower.Id, r.Merchant.Id)); return e; },
            () => { var r = MochitokoTable(); var b = Add(r.S, "training-card-bower", 1, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(r.S); Accept(e, Offer(e, 1, BowCard, b.Id, r.Follower.Id)); return e; },
            () => { var s = Table(); var (castle, stronghold) = Siege(s); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, Ezuno, 0, Zone.Battlefield, province: 0); var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var e = MatchEngine.FromTestState(s); Accept(e, SiegeAt(e, castle, stronghold, sh.Id)); return e; },
        };
        foreach (var build in builds)
        {
            var source = build();
            Check(InWindow(source), "each position holds an action in a window");
            OfferedCommandsValidate(source, build);
        }
    }
}
